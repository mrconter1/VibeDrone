using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace VibeDroneLauncher;

public record ReleaseInfo(string Tag, string Body, string ZipUrl, string HtmlUrl, string Sha256Url);

// Owns the game install under %LocalAppData%\VibeDrone\app (user-writable, so updates never need
// admin), talks to the GitHub Releases API, and downloads/extracts the game zip. All version
// comparison is numeric on the leading x.y.z; a missing install always counts as "needs update".
public class Updater
{
    private const string LatestApi = "https://api.github.com/repos/mrconter1/VibeDrone/releases/latest";

    private static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VibeDrone");

    public string AppDir => Path.Combine(Root, "app");
    private string VersionFile => Path.Combine(AppDir, "version.txt");
    public string GameExe => Path.Combine(AppDir, "OpenDrone.exe");
    public bool IsInstalled => File.Exists(GameExe);

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("VibeDroneLauncher");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        c.Timeout = TimeSpan.FromMinutes(10);
        return c;
    }

    public string? GetInstalledVersion() =>
        File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : null;

    public async Task<ReleaseInfo> GetLatestAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(LatestApi));
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        string html = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

        string zip = "", sha = "";
        if (root.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
            {
                string name = a.GetProperty("name").GetString() ?? "";
                string url = a.GetProperty("browser_download_url").GetString() ?? "";
                if (name.EndsWith("windows.zip", StringComparison.OrdinalIgnoreCase)) zip = url;
                else if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)) sha = url;
            }
        return new ReleaseInfo(tag, body, zip, html, sha);
    }

    // Download the game zip (reporting 0..100), then replace the app folder with its contents.
    public async Task DownloadAndInstallAsync(ReleaseInfo rel, IProgress<double> progress)
    {
        if (string.IsNullOrEmpty(rel.ZipUrl))
            throw new InvalidOperationException("This release has no Windows build attached.");

        Directory.CreateDirectory(Root);
        string tmp = Path.Combine(Root, "download.zip");

        using (var resp = await Http.GetAsync(rel.ZipUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? -1;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(tmp);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n));
                read += n;
                if (total > 0) progress.Report(read * 100.0 / total);
            }
        }

        // Verify the download against the published SHA-256 before touching the installed game. A
        // mismatch (corrupt/truncated/tampered) aborts without deleting the working install.
        await VerifyChecksumAsync(tmp, rel.Sha256Url);

        if (Directory.Exists(AppDir)) Directory.Delete(AppDir, recursive: true);
        Directory.CreateDirectory(AppDir);
        ZipFile.ExtractToDirectory(tmp, AppDir);
        File.Delete(tmp);
        File.WriteAllText(VersionFile, Normalize(rel.Tag));
    }

    private async Task VerifyChecksumAsync(string file, string sha256Url)
    {
        if (string.IsNullOrEmpty(sha256Url)) return;   // no checksum published for this release

        string text = await Http.GetStringAsync(sha256Url);
        string expected = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } p
            ? p[0].ToLowerInvariant() : "";

        string actual;
        await using (var fs = File.OpenRead(file))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();

        if (expected.Length != 64 || actual != expected)
        {
            File.Delete(file);
            throw new InvalidOperationException("Download failed verification (checksum mismatch). Please try again.");
        }
    }

    public void LaunchGame()
    {
        Process.Start(new ProcessStartInfo(GameExe) { UseShellExecute = true, WorkingDirectory = AppDir });
    }

    // true when the latest release is newer than what's installed (or nothing is installed yet).
    public static bool NeedsUpdate(string latestTag, string? installed)
    {
        if (string.IsNullOrEmpty(installed)) return true;
        int[] a = Parse(latestTag), b = Parse(installed);
        for (int i = 0; i < 3; i++)
            if (a[i] != b[i]) return a[i] > b[i];
        return false;
    }

    private static string Normalize(string tag) => tag.TrimStart('v', 'V');

    private static int[] Parse(string v)
    {
        var parts = v.TrimStart('v', 'V').Split('.', '-');
        var r = new int[3];
        for (int i = 0; i < 3 && i < parts.Length; i++) int.TryParse(parts[i], out r[i]);
        return r;
    }
}
