using Godot;

// A slow cinematic camera that orbits the arena behind the menus. Lives in the 3D world, runs while
// the tree is paused (ProcessMode=Always), and only moves while Active. Drag the mouse (hold left
// button) to orbit it yourself - it eases back to the gentle auto-orbit when you let go, and the
// cursor stays free for clicking menu buttons. DroneController makes it current for the full-screen
// menus and hands back to the drone camera when play starts.
public partial class MenuCamera : Camera3D
{
    [Export] public Vector3 Target = new(0f, 8f, 55f);
    [Export] public float Radius = 95f;
    [Export] public float Speed = 0.06f;             // auto-orbit radians/sec
    [Export] public float MouseSensitivity = 0.006f;

    private float _autoYaw;                           // ambient spin
    private float _userYaw;                           // added by dragging
    private float _pitch = 0.42f;                     // vertical angle (~ the old Height)

    public bool Active { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Fov = 55f;
        Place();
    }

    public override void _Input(InputEvent ev)
    {
        if (!Active) return;
        // orbit while dragging with the left mouse button (cursor still free to click otherwise)
        if (ev is InputEventMouseMotion mm && (mm.ButtonMask & MouseButtonMask.Left) != 0)
        {
            _userYaw -= mm.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * MouseSensitivity, 0.08f, 1.3f);
        }
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        _autoYaw += (float)delta * Speed;
        Place();
    }

    private void Place()
    {
        float yaw = _autoYaw + _userYaw;
        float cp = Mathf.Cos(_pitch);
        GlobalPosition = Target + new Vector3(Mathf.Cos(yaw) * cp, Mathf.Sin(_pitch), Mathf.Sin(yaw) * cp) * Radius;
        LookAt(Target, Vector3.Up);
    }
}
