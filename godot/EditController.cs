using Godot;

// Free-fly "edit" camera, Minecraft-creative style. Press E to toggle: the game pauses,
// the mouse is captured for look, and you fly the camera with WASD (+ Space up / Shift
// down); mouse wheel changes fly speed. Press E again to drop back into the drone.
// ProcessMode=Always so it works while the tree is paused.
public partial class EditController : Node3D
{
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MoveSpeed = 25f;      // m/s target, adjustable with the mouse wheel
    [Export] public float Accel = 7f;           // lower = floatier (slower to reach speed / coast to stop)

    private Camera3D _cam = null!;
    private Camera3D _droneCam = null!;          // restored as current when leaving edit mode
    private Label _hint = null!;
    private bool _active;
    private float _yaw, _pitch;
    private Vector3 _vel;                        // carried momentum, for Minecraft-style float

    public void Setup(Camera3D droneCam) => _droneCam = droneCam;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _cam = new Camera3D { Fov = 75f, Current = false };
        AddChild(_cam);

        var layer = new CanvasLayer { Layer = 9 };
        AddChild(layer);
        _hint = new Label
        {
            Text = "EDIT MODE   E exit   WASD move   Space up   Shift down   wheel speed",
            Position = new Vector2(40, 40),
            Visible = false,
        };
        layer.AddChild(_hint);
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Keycode: Key.E })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!_active) return;

        if (ev is InputEventMouseMotion mm)
        {
            _yaw -= mm.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * MouseSensitivity, -1.55f, 1.55f);
            _cam.Rotation = new Vector3(_pitch, _yaw, 0f);
        }
        else if (ev is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) MoveSpeed = Mathf.Min(MoveSpeed * 1.15f, 400f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) MoveSpeed = Mathf.Max(MoveSpeed / 1.15f, 2f);
        }
    }

    private void Toggle()
    {
        _active = !_active;
        GetTree().Paused = _active;
        _hint.Visible = _active;

        if (_active)
        {
            // start where the drone camera is, looking the same way, for a seamless hand-off
            _cam.GlobalPosition = _droneCam.GlobalPosition;
            Vector3 e = _droneCam.GlobalRotation;
            _yaw = e.Y; _pitch = Mathf.Clamp(e.X, -1.55f, 1.55f);
            _cam.Rotation = new Vector3(_pitch, _yaw, 0f);
            _cam.Current = true;
            _vel = Vector3.Zero;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            _droneCam.Current = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;   // keep the cursor hidden
        }
    }

    public override void _Process(double delta)
    {
        if (!_active) return;
        Basis b = _cam.GlobalBasis;
        Vector3 dir = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) dir -= b.Z;      // forward (camera -Z)
        if (Input.IsKeyPressed(Key.S)) dir += b.Z;
        if (Input.IsKeyPressed(Key.A)) dir -= b.X;
        if (Input.IsKeyPressed(Key.D)) dir += b.X;
        if (Input.IsKeyPressed(Key.Space)) dir += Vector3.Up;
        if (Input.IsKeyPressed(Key.Shift)) dir += Vector3.Down;

        // ease velocity toward the target (or toward 0 when keys released) -> floaty, no instant stop
        Vector3 target = dir == Vector3.Zero ? Vector3.Zero : dir.Normalized() * MoveSpeed;
        _vel = _vel.Lerp(target, 1f - Mathf.Exp(-Accel * (float)delta));
        _cam.GlobalPosition += _vel * (float)delta;
    }
}
