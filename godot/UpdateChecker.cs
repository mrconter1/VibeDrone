using System;
using Godot;

// Fire-and-forget check against GitHub Releases for a build newer than the running one. Skips "dev"
// builds and silently ignores any failure (offline, rate limit, parse error). On finding a newer
// release it calls the supplied callback once with (tag, htmlUrl) so the caller can surface it.
public partial class UpdateChecker : Node
{
    private const string LatestApi = "https://api.github.com/repos/mrconter1/VibeDrone/releases/latest";

    private string _current = "";
    private Action<string, string> _onUpdate = null!;

    public void Check(string currentVersion, Action<string, string> onUpdate)
    {
        _current = currentVersion;
        _onUpdate = onUpdate;
        if (string.IsNullOrEmpty(currentVersion) || currentVersion == "dev") return;

        var http = new HttpRequest { UseThreads = true };
        AddChild(http);
        http.RequestCompleted += OnCompleted;
        // GitHub's API rejects requests without a User-Agent.
        http.Request(LatestApi, new[] { "User-Agent: VibeDrone", "Accept: application/vnd.github+json" });
    }

    private void OnCompleted(long result, long code, string[] headers, byte[] body)
    {
        if (result != (long)HttpRequest.Result.Success || code != 200) return;

        Variant json = Json.ParseString(body.GetStringFromUtf8());
        if (json.VariantType != Variant.Type.Dictionary) return;
        var dict = json.AsGodotDictionary();

        string tag = dict.TryGetValue("tag_name", out Variant tv) ? tv.AsString() : "";
        string url = dict.TryGetValue("html_url", out Variant uv) ? uv.AsString() : "";
        if (tag.Length > 0 && IsNewer(tag, _current))
            _onUpdate(tag, url);
    }

    // true when version a is strictly newer than b (both like "v1.2.3" / "1.2.3"; missing/odd parts = 0).
    public static bool IsNewer(string a, string b)
    {
        int[] pa = Parse(a), pb = Parse(b);
        for (int i = 0; i < 3; i++)
            if (pa[i] != pb[i]) return pa[i] > pb[i];
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
