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
    [Export] public float SignRoll = 1f, SignPitch = 1f, SignYaw = 1f, SignThrottle = 1f;
    [Export] public float CameraTiltDeg = 25f;
    [Export] public int Substeps = 4;
    [Export] public bool Replay = true;                  // start in replay (ground truth); Tab to fly live
    [Export] public string ReplayFile = "res://replay.csv";

    private readonly FlightModel _fm = new();
    private Node3D _drone;
    private Camera3D _cam;
    private Label _hud;
    private float _kThrottle;
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
        _hud = new Label { Position = new Vector2(14, 12) };
        layer.AddChild(_hud);

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
        ApplyHud("LIVE (Tab=replay)");
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
        if (_hud == null) return;
        int pads = Input.GetConnectedJoypads().Count;
        string name = pads > 0 ? Input.GetJoyName(JoyDevice) : "none";
        _hud.Text =
            $"[{mode}]   alt {_drone.GlobalPosition.Y,6:0.0} m   speed {_fm.Vel.Length(),6:0.0} m/s\n" +
            $"joypad: {name}  ({pads})\n" +
            $"raw axes: 0={_axes[0]:+0.00;-0.00} 1={_axes[1]:+0.00;-0.00} 2={_axes[2]:+0.00;-0.00} 3={_axes[3]:+0.00;-0.00}\n" +
            "Esc quit   R reset   Tab live/replay";
    }

    private void BuildWorld()
    {
        var sun = new DirectionalLight3D { RotationDegrees = new Vector3(-55, -40, 0) };
        AddChild(sun);
        var env = new WorldEnvironment();
        env.Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.09f, 0.11f, 0.16f),
            AmbientLightColor = new Color(0.4f, 0.45f, 0.55f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
        };
        AddChild(env);

        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(400, 400) } };
        ground.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.18f, 0.22f) };
        AddChild(ground);

        var pmat = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.5f, 0.3f) };
        for (int x = -90; x <= 90; x += 15)
        for (int z = -90; z <= 90; z += 15)
        {
            if (x == 0 && z == 0) continue;
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.6f, 6f, 0.6f) },
                Position = new Vector3(x, 3f, z),
                MaterialOverride = pmat,
            });
        }
    }
}
