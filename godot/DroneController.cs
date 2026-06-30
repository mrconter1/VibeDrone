using Godot;
using OpenDrone;

// Drives the portable FlightModel and applies the result to this Node3D.
// Attach to the root Node3D of Main.tscn. Builds an FPV camera + a simple world
// in code so the scene file stays trivial. Run the model in _PhysicsProcess
// (fixed timestep) and SET the transform - do NOT use Godot's RigidBody.
public partial class DroneController : Node3D
{
    [Export] public int JoyDevice = 0;
    [Export] public int AxisRoll = 0, AxisPitch = 1, AxisThrottle = 2, AxisYaw = 3;
    // per-channel sign (match your radio + the fitted convention)
    [Export] public float SignRoll = -1f, SignPitch = -1f, SignYaw = 1f, SignThrottle = 1f;
    [Export] public float CameraTiltDeg = 25f;
    [Export] public int Substeps = 4;

    private readonly FlightModel _fm = new();
    private Node3D _drone;   // the moving node (root stays static so the world doesn't move with it)
    private Camera3D _cam;
    private Label _hud;
    private float _kThrottle; // keyboard throttle fallback
    private readonly float[] _axes = new float[16]; // raw joypad axes by index

    public override void _Ready()
    {
        // start maximised/fullscreen
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);

        BuildWorld();  // static world stays on the root (this), so it does NOT move

        // moving drone node carries the FPV camera
        _drone = new Node3D();
        AddChild(_drone);
        _cam = new Camera3D { Fov = 100f };
        _drone.AddChild(_cam);
        // Godot camera looks -Z; drone forward is +Z -> yaw 180, then uptilt about X.
        _cam.RotationDegrees = new Vector3(CameraTiltDeg, 180f, 0f);

        // on-screen HUD (also shows raw axes so you can calibrate)
        var layer = new CanvasLayer();
        AddChild(layer);
        _hud = new Label { Position = new Vector2(14, 12) };
        layer.AddChild(_hud);

        Input.MouseMode = Input.MouseModeEnum.Hidden;
        ApplyState();
    }

    // Raw joypad axes work even for non-gamepad radios (no SDL mapping needed).
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventJoypadMotion m && (int)m.Axis < _axes.Length)
            _axes[(int)m.Axis] = m.AxisValue;
    }

    private static float Dead(float v, float dz = 0.04f) => Mathf.Abs(v) < dz ? 0f : v;

    public override void _PhysicsProcess(double delta)
    {
        float roll, pitch, yaw, throttle;
        if (Input.GetConnectedJoypads().Count > 0)
        {
            roll = Dead(_axes[AxisRoll]);
            pitch = Dead(_axes[AxisPitch]);
            yaw = Dead(_axes[AxisYaw]);
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

        ApplyState();
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode == Key.Escape) GetTree().Quit();
            else if (k.Keycode == Key.R) _fm.Reset();
        }
    }

    private void ApplyState()
    {
        var p = _fm.Pos;
        _drone.GlobalPosition = new Vector3(p.X, p.Y, p.Z);
        var q = _fm.Rot;
        _drone.Quaternion = new Quaternion(q.X, q.Y, q.Z, q.W);

        if (_hud != null)
        {
            int pads = Input.GetConnectedJoypads().Count;
            string name = pads > 0 ? Input.GetJoyName(JoyDevice) : "none";
            _hud.Text =
                $"alt {p.Y,6:0.0} m   speed {_fm.Vel.Length(),6:0.0} m/s\n" +
                $"joypad: {name}  ({pads} connected)\n" +
                $"raw axes: 0={_axes[0]:+0.00;-0.00} 1={_axes[1]:+0.00;-0.00} " +
                $"2={_axes[2]:+0.00;-0.00} 3={_axes[3]:+0.00;-0.00} 4={_axes[4]:+0.00;-0.00}\n" +
                "Esc quit   R reset";
        }
    }

    private void BuildWorld()
    {
        // light + environment
        var sun = new DirectionalLight3D { RotationDegrees = new Vector3(-55, -40, 0) };
        AddChild(sun);
        var env = new WorldEnvironment();
        var e = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.09f, 0.11f, 0.16f),
            AmbientLightColor = new Color(0.4f, 0.45f, 0.55f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
        };
        env.Environment = e;
        AddChild(env);

        // ground
        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(400, 400) } };
        var gmat = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.18f, 0.22f) };
        ground.MaterialOverride = gmat;
        AddChild(ground);

        // pylons for motion reference
        var pmat = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.5f, 0.3f) };
        for (int x = -90; x <= 90; x += 15)
        for (int z = -90; z <= 90; z += 15)
        {
            if (x == 0 && z == 0) continue;
            var pole = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.6f, 6f, 0.6f) },
                Position = new Vector3(x, 3f, z),
                MaterialOverride = pmat,
            };
            AddChild(pole);
        }
    }
}
