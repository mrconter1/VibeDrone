using Godot;

// Playback theatre: replays the best lap with a chase camera following behind a solid drone.
// Started from the Esc menu; Esc exits. Pauses the game and runs (ProcessMode=Always).
public partial class PlaybackController : Node3D
{
    [Export] public float Distance = 4.5f;   // chase distance behind the drone
    [Export] public float Height = 1.4f;     // chase height above the drone

    private DroneController _ctrl = null!;
    private Camera3D _droneCam = null!;
    private Camera3D _cam = null!;
    private DroneModel _drone = null!;
    private Label _hint = null!;
    private bool _active;
    private float _time;

    public void Setup(DroneController ctrl, Camera3D droneCam) { _ctrl = ctrl; _droneCam = droneCam; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _cam = new Camera3D { Fov = 70f, Current = false };
        AddChild(_cam);

        _drone = new DroneModel { Ghost = false, Visible = false, ProcessMode = ProcessModeEnum.Always };
        AddChild(_drone);

        var layer = new CanvasLayer { Layer = 9 };
        AddChild(layer);
        _hint = new Label
        {
            Text = "PLAYBACK - best lap   |   Esc to exit",
            Position = new Vector2(40, 40),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(_hint);
    }

    public void Start()
    {
        if (!_ctrl.HasBestLap) return;   // nothing recorded yet
        _active = true;
        _time = 0f;
        _drone.Visible = true;
        _hint.Visible = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _cam.MakeCurrent();
        PlaceDroneAndCam(0f, snap: true);
    }

    private void Stop()
    {
        _active = false;
        _drone.Visible = false;
        _hint.Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _droneCam.MakeCurrent();
    }

    // _Input (not _UnhandledInput) so Esc is caught before the pause menu sees it.
    public override void _Input(InputEvent ev)
    {
        if (_active && ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Stop();
            GetViewport().SetInputAsHandled();   // don't also open the pause menu
        }
    }

    public override void _Process(double delta)
    {
        if (!_active) return;
        _time += (float)delta;
        if (_time > _ctrl.BestLapDuration) _time = 0f;   // loop
        PlaceDroneAndCam((float)delta, snap: false);
    }

    private void PlaceDroneAndCam(float delta, bool snap)
    {
        _ctrl.SampleBestLap(_time, out Vector3 pos, out Quaternion rot);
        _drone.GlobalTransform = new Transform3D(new Basis(rot), pos);

        Vector3 fwd = new Basis(rot).Z;                  // drone forward (Godot +Z)
        Vector3 want = pos - fwd * Distance + Vector3.Up * Height;
        _cam.GlobalPosition = snap ? want
            : _cam.GlobalPosition.Lerp(want, 1f - Mathf.Exp(-6f * delta));
        _cam.LookAt(pos + fwd * 2f, Vector3.Up);
    }
}
