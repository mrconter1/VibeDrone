using System.Globalization;
using Godot;

// Per-session performance log (truncated fresh each launch). Writes a header then one line
// per second: current FPS, min FPS in the window, average and worst frame time (spike
// detector). Also prints the render backend + GPU so a software fallback is obvious.
// Self-contained node - add it to the scene; call Mark() to annotate events.
public partial class SessionLog : Node
{
    private Godot.FileAccess _log = null!;
    private double _t;              // seconds since launch
    private double _elapsed;        // seconds since last log line
    private int _frames;            // rendered frames in the current window
    private double _worstMs;        // slowest frame in the window
    private double _minFps = 1e9;

    public override void _Ready()
    {
        string renderer = ProjectSettings.GetSetting("rendering/renderer/rendering_method").ToString();
        string gpu = $"{RenderingServer.GetVideoAdapterName()} ({RenderingServer.GetVideoAdapterVendor()})";
        GD.Print($"Renderer: {renderer}  GPU: {gpu}");

        const string path = "user://opendrone_session.log";
        _log = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        if (_log == null) { GD.PrintErr("session log: could not open " + path); return; }
        Vector2I win = DisplayServer.WindowGetSize();
        _log.StoreLine("=== OpenDrone session log ===");
        _log.StoreLine($"renderer : {renderer}");
        _log.StoreLine($"gpu      : {gpu}");
        _log.StoreLine($"window   : {win.X}x{win.Y}   vsync: {DisplayServer.WindowGetVsyncMode()}");
        _log.StoreLine($"physics  : {Engine.PhysicsTicksPerSecond} Hz   max_fps: {Engine.MaxFps}");
        _log.StoreLine("");
        _log.StoreLine("t_sec\tfps\tmin_fps_1s\tavg_ms\tworst_ms");
        _log.Flush();
        GD.Print("session log -> " + ProjectSettings.GlobalizePath(path));
    }

    // Annotate the log with an event (e.g. a reset), stamped with the session time.
    public void Mark(string msg) =>
        _log?.StoreLine(string.Format(CultureInfo.InvariantCulture, "# {0} at {1:0.0}s", msg, _t));

    public override void _Process(double delta)
    {
        if (_log == null) return;
        _t += delta;
        _elapsed += delta;
        _frames++;
        double ms = delta * 1000.0;
        if (ms > _worstMs) _worstMs = ms;
        double fps = Engine.GetFramesPerSecond();
        if (fps < _minFps) _minFps = fps;

        if (_elapsed >= 1.0)
        {
            double avgMs = _elapsed * 1000.0 / System.Math.Max(_frames, 1);
            _log.StoreLine(string.Format(CultureInfo.InvariantCulture,
                "{0:0.0}\t{1:0}\t{2:0}\t{3:0.00}\t{4:0.00}", _t, fps, _minFps, avgMs, _worstMs));
            _log.Flush();
            _elapsed = 0; _frames = 0; _worstMs = 0; _minFps = 1e9;
        }
    }

    public override void _ExitTree()
    {
        _log?.StoreLine("=== session end ===");
        _log?.Flush();
        _log?.Close();
    }
}
