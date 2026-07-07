using Godot;

// A slow cinematic camera that orbits the arena behind the menus. Lives in the 3D world, runs
// while the tree is paused (ProcessMode=Always), and only moves while Active. DroneController makes
// it current for the full-screen menus and hands back to the drone camera when play starts.
public partial class MenuCamera : Camera3D
{
    [Export] public Vector3 Target = new(0f, 8f, 55f);
    [Export] public float Radius = 95f;
    [Export] public float Height = 42f;
    [Export] public float Speed = 0.06f;   // radians/sec

    private float _angle;

    public bool Active { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Fov = 55f;
        Place();
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        _angle += (float)delta * Speed;
        Place();
    }

    private void Place()
    {
        GlobalPosition = Target + new Vector3(Mathf.Cos(_angle) * Radius, Height, Mathf.Sin(_angle) * Radius);
        LookAt(Target, Vector3.Up);
    }
}
