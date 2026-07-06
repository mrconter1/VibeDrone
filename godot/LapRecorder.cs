using System.Collections.Generic;
using Godot;

// Owns the ghost racer + trail, records the drone's pose through each lap, keeps the ranked
// best laps, and persists both the ghost and the lap board. It is fed poses and lap-complete
// events and has no control over the drone/physics, so it stays decoupled from flight.
public partial class LapRecorder : Node3D
{
    private struct Sample { public float T; public Vector3 Pos; public Quaternion Rot; }

    private readonly List<Sample> _recording = new();    // current lap being recorded
    private List<Sample> _bestGhost = new();             // best lap trajectory (persisted)
    private float _recAccum;
    private int _ghostIdx;

    private DroneModel _ghost = null!;
    private TrailRibbon _trail = null!;
    private readonly List<Vector3> _trailPts = new();
    private readonly List<float> _trailAge = new();
    private readonly List<Vector3> _trailRight = new();

    private readonly List<float> _bestLaps = new();      // ranked fastest laps (persisted)

    public string Ranks { get; private set; } = "";
    public float LastLap { get; private set; }
    public float BestLap => _bestLaps.Count > 0 ? _bestLaps[0] : 0f;
    public bool HasBestLap => _bestGhost.Count >= 2;
    public float BestLapDuration => _bestGhost.Count > 0 ? _bestGhost[^1].T : 0f;

    public override void _Ready()
    {
        _ghost = new DroneModel { Ghost = true, Visible = false };
        AddChild(_ghost);
        _trail = new TrailRibbon();
        AddChild(_trail);
        LoadLaps();
        LoadGhost();
    }

    public void BeginLap()
    {
        _recording.Clear();
        _recAccum = 0f;
        _ghostIdx = 0;
    }

    // Sample the drone's pose ~40x/s into the current lap recording.
    public void Record(float dt, float lapTime, Vector3 pos, Quaternion rot)
    {
        _recAccum += dt;
        if (_recAccum < 0.025f) return;
        _recAccum = 0f;
        _recording.Add(new Sample { T = lapTime, Pos = pos, Rot = rot });
    }

    // Record a completed lap; a new fastest lap becomes the ghost. Returns true if it was best.
    public bool CompleteLap(float lapTime)
    {
        LastLap = lapTime;
        bool isBest = _bestLaps.Count == 0 || lapTime < _bestLaps[0];
        if (isBest) { _bestGhost = new List<Sample>(_recording); SaveGhost(); }
        RecordLap(lapTime);
        return isBest;
    }

    // Called at render rate: place the ghost + draw its trail, or hide when not racing.
    public void UpdateVisuals(float lapTime, bool running)
    {
        if (!running || _bestGhost.Count < 2) { _ghost.Visible = false; _trail.Visible = false; return; }
        _ghost.Visible = true;
        while (_ghostIdx < _bestGhost.Count - 2 && _bestGhost[_ghostIdx + 1].T < lapTime) _ghostIdx++;
        Sample a = _bestGhost[_ghostIdx], b = _bestGhost[_ghostIdx + 1];
        float u = Mathf.Clamp((lapTime - a.T) / Mathf.Max(b.T - a.T, 1e-4f), 0f, 1f);
        _ghost.GlobalTransform = new Transform3D(new Basis(a.Rot.Slerp(b.Rot, u)), a.Pos.Lerp(b.Pos, u));
        BuildTrail(lapTime);
    }

    public void HideGhost() { _ghost.Visible = false; _trail.Visible = false; }

    // Wipe saved best laps + ghost, so a fresh best records a new ghost.
    public void Clear()
    {
        _bestLaps.Clear();
        Ranks = "";
        LastLap = 0f;
        _bestGhost = new List<Sample>();
        HideGhost();
        SaveLaps();
        SaveGhost();
    }

    // Interpolated best-lap pose at time t (used here and by the playback theatre).
    public void SampleBestLap(float t, out Vector3 pos, out Quaternion rot)
    {
        int i = 0;
        while (i < _bestGhost.Count - 2 && _bestGhost[i + 1].T < t) i++;
        Sample a = _bestGhost[i], b = _bestGhost[i + 1];
        float u = Mathf.Clamp((t - a.T) / Mathf.Max(b.T - a.T, 1e-4f), 0f, 1f);
        pos = a.Pos.Lerp(b.Pos, u);
        rot = a.Rot.Slerp(b.Rot, u);
    }

    // A fading ribbon along the ghost's recent path (~1.2 s), both ends interpolated so it
    // grows/recedes smoothly; side vector = drone right-axis so it rolls with the drone.
    private void BuildTrail(float lapTime)
    {
        const float window = 1.2f, halfW = 0.4f;
        float tailT = Mathf.Max(lapTime - window, _bestGhost[0].T);
        if (lapTime - tailT < 0.05f) { _trail.Visible = false; return; }

        _trailPts.Clear();
        _trailAge.Clear();
        _trailRight.Clear();
        SampleBestLap(tailT, out Vector3 tp, out Quaternion tr);
        _trailPts.Add(tp); _trailAge.Add(1f); _trailRight.Add(new Basis(tr).X);
        for (int i = 0; i < _bestGhost.Count; i++)
        {
            float t = _bestGhost[i].T;
            if (t > tailT && t < lapTime)
            {
                _trailPts.Add(_bestGhost[i].Pos);
                _trailAge.Add((lapTime - t) / window);
                _trailRight.Add(new Basis(_bestGhost[i].Rot).X);
            }
        }
        SampleBestLap(lapTime, out Vector3 hp, out Quaternion hr);
        _trailPts.Add(hp); _trailAge.Add(0f); _trailRight.Add(new Basis(hr).X);

        _trail.Build(_trailPts, _trailRight, _trailAge, halfW);
    }

    private void RecordLap(float t)
    {
        _bestLaps.Add(t);
        _bestLaps.Sort();
        if (_bestLaps.Count > 5) _bestLaps.RemoveRange(5, _bestLaps.Count - 5);
        RebuildRanks();
        SaveLaps();
    }

    private void RebuildRanks()
    {
        Ranks = "";
        for (int i = 0; i < _bestLaps.Count && i < 5; i++)
            Ranks += $"{i + 1}.  {_bestLaps[i]:00.00}\n";
    }

    // --- persistence ---
    private void SaveGhost()
    {
        var arr = new Godot.Collections.Array();
        foreach (Sample s in _bestGhost)
            arr.Add(new Godot.Collections.Dictionary
            {
                { "t", s.T }, { "x", s.Pos.X }, { "y", s.Pos.Y }, { "z", s.Pos.Z },
                { "qx", s.Rot.X }, { "qy", s.Rot.Y }, { "qz", s.Rot.Z }, { "qw", s.Rot.W },
            });
        using var f = FileAccess.Open("user://ghost.json", FileAccess.ModeFlags.Write);
        if (f != null) f.StoreString(Json.Stringify(arr));
    }

    private void LoadGhost()
    {
        const string path = "user://ghost.json";
        if (!FileAccess.FileExists(path)) return;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return;
        Variant parsed = Json.ParseString(f.GetAsText());
        if (parsed.VariantType != Variant.Type.Array) return;
        _bestGhost = new List<Sample>();
        foreach (Variant v in parsed.AsGodotArray())
        {
            var d = v.AsGodotDictionary();
            _bestGhost.Add(new Sample
            {
                T = d["t"].AsSingle(),
                Pos = new Vector3(d["x"].AsSingle(), d["y"].AsSingle(), d["z"].AsSingle()),
                Rot = new Quaternion(d["qx"].AsSingle(), d["qy"].AsSingle(), d["qz"].AsSingle(), d["qw"].AsSingle()),
            });
        }
    }

    private void LoadLaps()
    {
        const string path = "user://laptimes.json";
        if (!FileAccess.FileExists(path)) return;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return;
        Variant parsed = Json.ParseString(f.GetAsText());
        if (parsed.VariantType != Variant.Type.Array) return;
        foreach (Variant v in parsed.AsGodotArray()) _bestLaps.Add(v.AsSingle());
        _bestLaps.Sort();
        RebuildRanks();
    }

    private void SaveLaps()
    {
        var arr = new Godot.Collections.Array();
        foreach (float t in _bestLaps) arr.Add(t);
        using var f = FileAccess.Open("user://laptimes.json", FileAccess.ModeFlags.Write);
        if (f != null) f.StoreString(Json.Stringify(arr));
    }
}
