using System;
using System.Text;
using System.Text.Json;
using Godot;

// Fire-and-forget: on launch, ask the GitHub Releases API for the latest tag and, if it's newer than
// this build, call back (on the main thread, via HttpRequest's signal) so the menu can show an
// "update available" badge. Version compare is numeric on the leading x.y.z, matching the launcher's
// Updater. Silent on any failure/offline and skipped for non-numeric "dev" builds - the badge is a
// nicety and must never block or error the menu.
public partial class UpdateCheck : Node
{
    private const string LatestApi = "https://api.github.com/repos/mrconter1/VibeDrone/releases/latest";

    private string _current = "";
    private Action<string> _onNewer = null!;

    // Kick off a check; onNewer(tag) runs only when a strictly-newer release exists.
    public static void Start(Node parent, string currentVersion, Action<string> onNewer)
    {
        if (!IsNumeric(currentVersion)) return;   // dev/untagged: nothing meaningful to compare against
        parent.AddChild(new UpdateCheck { _current = currentVersion, _onNewer = onNewer });
    }

    public override void _Ready()
    {
        var req = new HttpRequest { UseThreads = true };
        AddChild(req);
        req.RequestCompleted += OnCompleted;
        string[] headers = { "User-Agent: VibeDrone", "Accept: application/vnd.github+json" };
        if (req.Request(LatestApi, headers) != Error.Ok) QueueFree();
    }

    private void OnCompleted(long result, long code, string[] headers, byte[] body)
    {
        if (code == 200)
        {
            try
            {
                using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(body));
                string tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
                if (IsNewer(tag, _current)) _onNewer(tag);
            }
            catch { /* malformed / offline / rate-limited: stay silent */ }
        }
        QueueFree();
    }

    private static bool IsNumeric(string v) { int[] p = Parse(v); return p[0] + p[1] + p[2] > 0; }

    // true when latest is strictly newer than current on the leading x.y.z.
    private static bool IsNewer(string latest, string current)
    {
        int[] a = Parse(latest), b = Parse(current);
        for (int i = 0; i < 3; i++)
            if (a[i] != b[i]) return a[i] > b[i];
        return false;
    }

    private static int[] Parse(string v)
    {
        var parts = v.TrimStart('v', 'V').Split('.', '-');
        var r = new int[3];
        for (int i = 0; i < 3 && i < parts.Length; i++) int.TryParse(parts[i], out r[i]);
        return r;
    }
}
