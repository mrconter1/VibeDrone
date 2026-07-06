using System.Collections.Generic;
using System.Globalization;
using System.Runtime;
using Godot;
using OpenDrone;
using NVec = System.Numerics.Vector3;
using NQuat = System.Numerics.Quaternion;

// Drives the portable FlightModel and applies the result to this Node3D, OR replays a
// recorded the reference sim flight. Both paths go through ONE verified Unity->Godot conversion
// (ToGodot), so if the replay looks correct the live model is guaranteed consistent.
// The model integrates orientation as a proper quaternion (in the reference sim's frame); we
// convert via a forward/up basis - no fragile per-axis sequential rotation.
public partial class DroneController : Node3D
{
    [Export] public int JoyDevice = 0;
    [Export] public int AxisRoll = 0, AxisPitch = 1, AxisThrottle = 2, AxisYaw = 3;
    [Export] public float SignRoll = 1f, SignPitch = -1f, SignYaw = -1f, SignThrottle = 1f;
    // FOV 95 deg ~ the reference sim's default (90-100); lower than a real cam's 120+ to cut
    // the rectilinear edge-stretch warping. ~30 deg uptilt (freestyle 25-35), nose-mounted.
    [Export] public float CameraFovDeg = 95f;
    [Export] public float CameraTiltDeg = 30f;
    [Export] public float CameraForward = 0.08f;   // metres in front of CG
    [Export] public float CameraUp = 0.03f;        // metres above CG
    [Export] public int Substeps = 1;   // physics already ticks at 250 Hz (project.godot)
    [Export] public bool Replay = false;                 // start live; Tab to watch recorded replay
    [Export] public string ReplayFile = "res://replay.csv";

    private readonly FlightModel _fm = new();
    private Node3D _drone = null!;      // set in _Ready
    private Camera3D _cam = null!;
    private Hud _osd = null!;
    private MotorAudio _audio = null!;
    private float _kThrottle;
    private float _flightTime;
    private float _curThrottle;
    private readonly float[] _axes = new float[16];

    // per-session performance log (truncated fresh each launch)
    private Godot.FileAccess _log = null!;   // set in _Ready (OpenLog); null-checked everywhere
    private double _sessionTime;      // seconds since launch
    private double _logElapsed;       // seconds since last log line
    private int _logFrames;           // rendered frames in the current 1s window
    private double _worstFrameMs;     // slowest frame in the window (spike detector)
    private double _minFpsWindow = 1e9;

    // replay data
    private readonly List<float> _rt = new();
    private readonly List<NVec> _rpos = new();
    private readonly List<NQuat> _rquat = new();
    private float _replayT;

    public override void _Ready()
    {
        // prefer short GC pauses over throughput: fewer gen2 stalls = fewer missed frames
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // report the actual render backend + GPU so a software (llvmpipe) fallback is obvious
        string renderer = ProjectSettings.GetSetting("rendering/renderer/rendering_method").ToString();
        string gpu = $"{RenderingServer.GetVideoAdapterName()} ({RenderingServer.GetVideoAdapterVendor()})";
        GD.Print($"Renderer: {renderer}  GPU: {gpu}");

        OpenLog(renderer, gpu);

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        BuildWorld();

        _drone = new Node3D();
        AddChild(_drone);
        _cam = new Camera3D { Fov = CameraFovDeg };
        _drone.AddChild(_cam);
        _cam.Position = new Vector3(0f, CameraUp, CameraForward);   // nose mount (drone local frame)
        _cam.RotationDegrees = new Vector3(CameraTiltDeg, 180f, 0f);

        var layer = new CanvasLayer();
        AddChild(layer);
        _osd = new Hud();
        layer.AddChild(_osd);

        _audio = new MotorAudio();
        AddChild(_audio);

        var menu = new SoundMenu();
        menu.Setup(_audio);   // set before AddChild: AddChild runs _Ready synchronously
        AddChild(menu);       // S opens/closes it and pauses the game

        if (Replay) LoadReplay();
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        ResetDrone();
    }

    // Open (and truncate) the per-session performance log. Fresh every launch.
    private void OpenLog(string renderer, string gpu)
    {
        const string path = "user://opendrone_session.log";
        _log = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        if (_log == null)
        {
            GD.PrintErr("session log: could not open " + path);
            return;
        }
        Vector2I win = DisplayServer.WindowGetSize();
        _log.StoreLine("=== OpenDrone session log ===");
        _log.StoreLine($"renderer : {renderer}");
        _log.StoreLine($"gpu      : {gpu}");
        _log.StoreLine($"window   : {win.X}x{win.Y}   vsync: {DisplayServer.WindowGetVsyncMode()}");
        _log.StoreLine($"physics  : {Engine.PhysicsTicksPerSecond} Hz   max_fps: {Engine.MaxFps}   fov: {CameraFovDeg:0}");
        _log.StoreLine("");
        _log.StoreLine("t_sec\tfps\tmin_fps_1s\tavg_ms\tworst_ms");
        _log.Flush();
        GD.Print("session log -> " + ProjectSettings.GlobalizePath(path));
    }

    // Once per second: log current/min FPS and average/worst frame time (spike detector).
    public override void _Process(double delta)
    {
        if (_log == null) return;
        _sessionTime += delta;
        _logElapsed += delta;
        _logFrames++;
        double ms = delta * 1000.0;
        if (ms > _worstFrameMs) _worstFrameMs = ms;
        double fps = Engine.GetFramesPerSecond();
        if (fps < _minFpsWindow) _minFpsWindow = fps;

        if (_logElapsed >= 1.0)
        {
            double avgMs = _logElapsed * 1000.0 / System.Math.Max(_logFrames, 1);
            _log.StoreLine(string.Format(CultureInfo.InvariantCulture,
                "{0:0.0}\t{1:0}\t{2:0}\t{3:0.00}\t{4:0.00}",
                _sessionTime, fps, _minFpsWindow, avgMs, _worstFrameMs));
            _log.Flush();
            _logElapsed = 0; _logFrames = 0; _worstFrameMs = 0; _minFpsWindow = 1e9;
        }
    }

    public override void _ExitTree()
    {
        _log?.StoreLine("=== session end ===");
        _log?.Flush();
        _log?.Close();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventJoypadMotion m && (int)m.Axis < _axes.Length)
            _axes[(int)m.Axis] = m.AxisValue;
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode == Key.Escape) GetTree().Quit();
            else if (k.Keycode == Key.R) { _log?.StoreLine(string.Format(CultureInfo.InvariantCulture, "# reset at {0:0.0}s", _sessionTime)); ResetDrone(); }
            else if (k.Keycode == Key.Tab) { Replay = !Replay; if (Replay && _rt.Count == 0) LoadReplay(); _replayT = 0; ResetDrone(); }
            // S opens the sound menu (handled by SoundMenu, which also pauses the game)
        }
    }

    private static float Dead(float v, float dz = 0.04f) => Mathf.Abs(v) < dz ? 0f : v;

    public override void _PhysicsProcess(double delta)
    {
        if (Replay && _rt.Count > 1) { PlayReplay((float)delta); _audio.SetEffort(0f); ApplyHud("REPLAY (Tab=live)"); return; }

        float roll, pitch, yaw, throttle;
        if (Input.GetConnectedJoypads().Count > 0)
        {
            roll = Dead(_axes[AxisRoll]); pitch = Dead(_axes[AxisPitch]); yaw = Dead(_axes[AxisYaw]);
            throttle = (_axes[AxisThrottle] + 1f) * 0.5f;
        }
        else
        {
            roll = (Input.IsKeyPressed(Key.Right) ? 1 : 0) - (Input.IsKeyPressed(Key.Left) ? 1 : 0);
            pitch = (Input.IsKeyPressed(Key.Up) ? 1 : 0) - (Input.IsKeyPressed(Key.Down) ? 1 : 0);
            yaw = (Input.IsKeyPressed(Key.E) ? 1 : 0) - (Input.IsKeyPressed(Key.Q) ? 1 : 0);
            _kThrottle = Mathf.Clamp(_kThrottle +
                ((Input.IsKeyPressed(Key.W) ? 1 : 0) - (Input.IsKeyPressed(Key.S) ? 1 : 0)) * (float)delta, 0f, 1f);
            throttle = _kThrottle;
        }
        roll *= SignRoll; pitch *= SignPitch; yaw *= SignYaw;
        if (SignThrottle < 0) throttle = 1f - throttle;

        float dt = (float)delta / Substeps;
        for (int i = 0; i < Substeps; i++)
            _fm.Step(roll, pitch, yaw, throttle, dt);

        ApplyTransform(_fm.Pos, _fm.Rot);
        _curThrottle = throttle;
        _flightTime += (float)delta;

        // drive motor audio from throttle AND stick activity, so rolls/pitches/yaws audibly
        // rev the motors (the thrust proxy saturates and would hide that). Silent at rest.
        float stick = Mathf.Min(1f, (Mathf.Abs(roll) + Mathf.Abs(pitch) + Mathf.Abs(yaw)) / 2f);
        _audio.SetEffort(Mathf.Clamp(throttle * 0.9f + stick * 0.5f, 0f, 1f));

        // fixed FOV (typical FPV camera); no speed-scaling

        ApplyHud("LIVE");
    }

    // --- the single verified Unity(LH, Y up, +Z fwd) -> Godot(RH, Y up, -Z fwd) conversion ---
    private static void ToGodot(NVec pU, NQuat qU, out Vector3 pos, out Basis basis)
    {
        pos = new Vector3(pU.X, pU.Y, -pU.Z);                 // flip Z (LH->RH)
        NVec fU = NVec.Transform(new NVec(0, 0, 1), qU);      // body forward (Unity)
        NVec uU = NVec.Transform(new NVec(0, 1, 0), qU);      // body up (Unity)
        Vector3 f = new Vector3(fU.X, fU.Y, -fU.Z).Normalized();
        Vector3 u = new Vector3(uU.X, uU.Y, -uU.Z).Normalized();
        Vector3 r = u.Cross(f).Normalized();
        u = f.Cross(r).Normalized();
        basis = new Basis(r, u, f);                            // local X=right, Y=up, Z=forward
    }

    private void ApplyTransform(NVec pU, NQuat qU)
    {
        ToGodot(pU, qU, out Vector3 pos, out Basis basis);
        _drone.GlobalTransform = new Transform3D(basis, pos);
    }

    private void ResetDrone()
    {
        _fm.Reset();
        _replayT = 0f;
        _flightTime = 0f;
        if (Replay && _rt.Count > 0) ApplyTransform(_rpos[0], _rquat[0]);
        else ApplyTransform(_fm.Pos, _fm.Rot);
        _drone.ResetPhysicsInterpolation();   // teleport: don't sweep from the old pose
    }

    private void PlayReplay(float delta)
    {
        _replayT += delta;
        if (_replayT > _rt[_rt.Count - 1])                     // loop = teleport
        {
            _replayT = 0f;
            _drone.ResetPhysicsInterpolation();
        }
        int i = 1;
        while (i < _rt.Count && _rt[i] < _replayT) i++;
        if (i >= _rt.Count) i = _rt.Count - 1;
        float a = (_replayT - _rt[i - 1]) / Mathf.Max(_rt[i] - _rt[i - 1], 1e-5f);
        NVec p = NVec.Lerp(_rpos[i - 1], _rpos[i], a);
        NQuat q = NQuat.Slerp(_rquat[i - 1], _rquat[i], a);
        ApplyTransform(p, q);
    }

    private void LoadReplay()
    {
        _rt.Clear(); _rpos.Clear(); _rquat.Clear();
        using var f = FileAccess.Open(ReplayFile, FileAccess.ModeFlags.Read);
        if (f == null) { GD.PrintErr("replay file not found: " + ReplayFile); return; }
        f.GetLine(); // header
        while (!f.EofReached())
        {
            string line = f.GetLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] c = line.Split(',');
            if (c.Length < 8) continue;
            _rt.Add(c[0].ToFloat());
            _rpos.Add(new NVec(c[1].ToFloat(), c[2].ToFloat(), c[3].ToFloat()));
            _rquat.Add(new NQuat(c[4].ToFloat(), c[5].ToFloat(), c[6].ToFloat(), c[7].ToFloat()));
        }
        GD.Print($"replay loaded: {_rt.Count} frames");
    }

    private void ApplyHud(string mode)
    {
        if (_osd == null) return;
        Basis b = _drone.GlobalTransform.Basis;
        _osd.Mode = mode;
        _osd.Speed = _fm.Vel.Length();
        _osd.Alt = _drone.GlobalPosition.Y;
        _osd.Throttle = _curThrottle;
        _osd.TimeSec = _flightTime;
        _osd.Fov = _cam.Fov;
        _osd.Fps = (float)Engine.GetFramesPerSecond();
        _osd.Sound = _audio.CurrentName;
        // climb angle (pitch) and roll from the drone basis, for the artificial horizon
        _osd.PitchDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(b.Z.Y, -1f, 1f)));
        _osd.RollDeg = Mathf.RadToDeg(Mathf.Atan2(b.X.Y, b.Y.Y));
    }

    private void BuildWorld()
    {
        // sun with shadows
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -50, 0),
            ShadowEnabled = true,
            LightEnergy = 1.1f,
        };
        AddChild(sun);

        // procedural sky -> real horizon + sky-based ambient
        var sky = new Sky { SkyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.30f, 0.55f, 0.90f),
            SkyHorizonColor = new Color(0.70f, 0.80f, 0.92f),
            GroundHorizonColor = new Color(0.70f, 0.80f, 0.92f),
            GroundBottomColor = new Color(0.20f, 0.23f, 0.28f),
            SunAngleMax = 30f,
        } };
        var env = new WorldEnvironment();
        env.Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.6f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            // SSAO off: costly on an integrated GPU and adds nothing on a flat grid scene
            SsaoEnabled = false,
            GlowEnabled = true,   // makes emissive gates pop (cheap enough, keep)
        };
        AddChild(env);

        // crisp anti-aliased grid ground via shader (great motion/altitude reference)
        var gridShader = new Shader { Code = GridShaderCode };
        var gmat = new ShaderMaterial { Shader = gridShader };
        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(1000, 1000) } };
        ground.MaterialOverride = gmat;
        AddChild(ground);

        BuildGates();
    }

    private void BuildGates()
    {
        // a circuit of square gates to fly through. Each gate has a GREEN frame on the
        // entry side (-Z) and a RED frame on the far side (+Z), so the correct direction
        // through is obvious.
        Vector2[] layout =
        {
            new(0, 40), new(35, 75), new(0, 110), new(-45, 90),
            new(-60, 40), new(-30, 5), new(30, 10), new(55, 45),
        };
        foreach (var p in layout)
        {
            var gate = new Node3D { Position = new Vector3(p.X, 8f, p.Y) };
            gate.AddChild(SquareFrame(new Color(0.10f, 1.0f, 0.30f), -0.18f));  // green: fly THROUGH this side
            gate.AddChild(SquareFrame(new Color(1.0f, 0.15f, 0.20f), 0.18f));   // red: wrong side
            AddChild(gate);
        }
    }

    // A square gate frame (4 emissive bars) in the XY plane, offset along local Z.
    private static Node3D SquareFrame(Color c, float z)
    {
        var frame = new Node3D { Position = new Vector3(0f, 0f, z) };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2.5f,
        };
        const float half = 3.0f, t = 0.3f;      // 6 m opening, 0.3 m bars
        const float outer = half * 2f + t, off = half + t * 0.5f;
        (Vector3 size, Vector3 pos)[] bars =
        {
            (new Vector3(outer, t, t), new Vector3(0f, off, 0f)),      // top
            (new Vector3(outer, t, t), new Vector3(0f, -off, 0f)),     // bottom
            (new Vector3(t, half * 2f, t), new Vector3(-off, 0f, 0f)), // left
            (new Vector3(t, half * 2f, t), new Vector3(off, 0f, 0f)),  // right
        };
        foreach (var (size, pos) in bars)
            frame.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = mat });
        return frame;
    }

    private const string GridShaderCode = @"
shader_type spatial;
render_mode cull_disabled;
uniform vec3 base_color : source_color = vec3(0.13, 0.16, 0.20);
uniform vec3 line_color : source_color = vec3(0.35, 0.42, 0.52);
uniform vec3 major_color : source_color = vec3(0.55, 0.65, 0.78);
varying vec3 wpos;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
float gridline(vec2 p, float step) {
    vec2 g = abs(fract(p / step - 0.5) - 0.5) / fwidth(p / step);
    return 1.0 - min(min(g.x, g.y), 1.0);
}
void fragment() {
    float minor = gridline(wpos.xz, 2.0);
    float major = gridline(wpos.xz, 20.0);
    vec3 col = mix(base_color, line_color, minor);
    col = mix(col, major_color, major);
    ALBEDO = col;
    ROUGHNESS = 1.0;
}
";
}
