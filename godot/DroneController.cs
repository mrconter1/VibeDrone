using System.Collections.Generic;
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
    [Export] public float CameraTiltDeg = 25f;
    [Export] public int Substeps = 4;
    [Export] public bool Replay = false;                 // start live; Tab to watch recorded replay
    [Export] public string ReplayFile = "res://replay.csv";

    private readonly FlightModel _fm = new();
    private Node3D _drone;
    private Camera3D _cam;
    private Hud _osd;
    private float _kThrottle;
    private float _flightTime;
    private float _curThrottle;
    private readonly float[] _axes = new float[16];

    // replay data
    private readonly List<float> _rt = new();
    private readonly List<NVec> _rpos = new();
    private readonly List<NQuat> _rquat = new();
    private float _replayT;

    public override void _Ready()
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        BuildWorld();

        _drone = new Node3D();
        AddChild(_drone);
        _cam = new Camera3D { Fov = 100f };
        _drone.AddChild(_cam);
        _cam.RotationDegrees = new Vector3(CameraTiltDeg, 180f, 0f);

        var layer = new CanvasLayer();
        AddChild(layer);
        _osd = new Hud();
        layer.AddChild(_osd);

        if (Replay) LoadReplay();
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        ResetDrone();
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
            else if (k.Keycode == Key.R) ResetDrone();
            else if (k.Keycode == Key.Tab) { Replay = !Replay; if (Replay && _rt.Count == 0) LoadReplay(); _replayT = 0; ResetDrone(); }
        }
    }

    private static float Dead(float v, float dz = 0.04f) => Mathf.Abs(v) < dz ? 0f : v;

    public override void _PhysicsProcess(double delta)
    {
        if (Replay && _rt.Count > 1) { PlayReplay((float)delta); ApplyHud("REPLAY (Tab=live)"); return; }

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

        // speed-scaled FOV for the FPV "rush"
        float spd = _fm.Vel.Length();
        _cam.Fov = Mathf.Lerp(_cam.Fov, 95f + Mathf.Min(spd, 40f) * 0.9f, 0.1f);

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
    }

    private void PlayReplay(float delta)
    {
        _replayT += delta;
        if (_replayT > _rt[_rt.Count - 1]) _replayT = 0f;     // loop
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
            SsaoEnabled = true,
            GlowEnabled = true,   // makes emissive gates pop
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
        // a simple circuit of emissive racing gates (torus) to fly through
        var colors = new[] { new Color(1f, 0.3f, 0.4f), new Color(0.3f, 0.7f, 1f), new Color(1f, 0.8f, 0.2f) };
        Vector2[] layout =
        {
            new(0, 40), new(35, 75), new(0, 110), new(-45, 90),
            new(-60, 40), new(-30, 5), new(30, 10), new(55, 45),
        };
        for (int i = 0; i < layout.Length; i++)
        {
            var mat = new StandardMaterial3D
            {
                AlbedoColor = colors[i % colors.Length],
                EmissionEnabled = true,
                Emission = colors[i % colors.Length],
                EmissionEnergyMultiplier = 2.5f,
            };
            var gate = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 3.0f, OuterRadius = 3.6f },
                Position = new Vector3(layout[i].X, 8f, layout[i].Y),
                MaterialOverride = mat,
            };
            gate.RotateX(Mathf.Pi / 2f); // stand the ring upright to fly through
            AddChild(gate);
        }
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
