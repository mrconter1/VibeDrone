using System.Collections.Generic;
using Godot;

// Playback theatre: replays the best lap with a chase camera following behind a solid drone.
// Started from the Esc menu; Esc exits. Pauses the game and runs (ProcessMode=Always).
public partial class PlaybackController : Node3D
{
    [Export] public float Distance = 4.5f;         // chase distance from the drone (wheel zooms)
    [Export] public float MouseSensitivity = 0.005f;

    private const float BasePitch = 0.30f;         // default look-down (~ the old 1.4 m height)

    private LapRecorder _ctrl = null!;
    private Camera3D _droneCam = null!;
    private Camera3D _cam = null!;
    private DroneModel _drone = null!;
    private Label _hint = null!;
    private TrailRibbon _trail = null!;
    private readonly List<Vector3> _trailPts = new();
    private readonly List<float> _trailAge = new();
    private readonly List<Vector3> _trailRight = new();
    private bool _active;
    private float _time;
    private float _orbitYaw, _orbitPitch;          // mouse-controlled offset from behind the drone

    public void Setup(LapRecorder recorder, Camera3D droneCam) { _ctrl = recorder; _droneCam = droneCam; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _cam = new Camera3D { Fov = 70f, Current = false };
        AddChild(_cam);

        _drone = new DroneModel { Ghost = false, Visible = false, ProcessMode = ProcessModeEnum.Always };
        AddChild(_drone);

        _trail = new TrailRibbon();
        AddChild(_trail);

        var layer = new CanvasLayer { Layer = 9 };
        AddChild(layer);
        _hint = new Label
        {
            Text = "PLAYBACK - best lap   |   mouse orbit   wheel zoom   Esc exit",
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
        _orbitYaw = 0f; _orbitPitch = 0f;   // start behind the drone
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
        _trail.Visible = false;
        _hint.Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _droneCam.MakeCurrent();
    }

    // _Input (not _UnhandledInput) so Esc is caught before the pause menu sees it.
    public override void _Input(InputEvent ev)
    {
        if (!_active) return;
        if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Stop();
            GetViewport().SetInputAsHandled();   // don't also open the pause menu
        }
        else if (ev is InputEventMouseMotion mm)   // orbit around the drone
        {
            _orbitYaw -= mm.Relative.X * MouseSensitivity;
            _orbitPitch = Mathf.Clamp(_orbitPitch - mm.Relative.Y * MouseSensitivity, -1.1f, 1.0f);
        }
        else if (ev is InputEventMouseButton { Pressed: true } mb)   // wheel zoom
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) Distance = Mathf.Max(Distance / 1.1f, 2f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) Distance = Mathf.Min(Distance * 1.1f, 30f);
        }
    }

    public override void _Process(double delta)
    {
        if (!_active) return;
        _time += (float)delta;
        bool snap = false;
        if (_time > _ctrl.BestLapDuration) { _time = 0f; snap = true; }   // loop -> snap the cam
        PlaceDroneAndCam((float)delta, snap);
        BuildTrail();
    }

    private void PlaceDroneAndCam(float delta, bool snap)
    {
        _ctrl.SampleBestLap(_time, out Vector3 pos, out Quaternion rot);
        _drone.GlobalTransform = new Transform3D(new Basis(rot), pos);

        // orbit around the drone: behind its heading by default, offset by the mouse. Looks at it.
        Vector3 fwd = new Basis(rot).Z;                  // drone forward (Godot +Z)
        float yaw = Mathf.Atan2(fwd.X, fwd.Z) + Mathf.Pi + _orbitYaw;   // behind (+PI) + mouse
        float pitch = Mathf.Clamp(BasePitch + _orbitPitch, -1.3f, 1.3f);
        var dir = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
        Vector3 want = pos + dir * Distance;
        _cam.GlobalPosition = snap ? want
            : _cam.GlobalPosition.Lerp(want, 1f - Mathf.Exp(-8f * delta));
        _cam.LookAt(pos, Vector3.Up);
    }

    // Fading ribbon behind the replay drone, sampled from the best-lap path.
    private void BuildTrail()
    {
        const float window = 1.2f, halfW = 0.4f, step = 0.03f;
        float t0 = Mathf.Max(_time - window, 0f);
        if (_time - t0 < 0.05f) { _trail.Visible = false; return; }

        _trailPts.Clear();
        _trailAge.Clear();
        _trailRight.Clear();
        for (float t = t0; t < _time; t += step)
        {
            _ctrl.SampleBestLap(t, out Vector3 p, out Quaternion r);
            _trailPts.Add(p); _trailAge.Add((_time - t) / window); _trailRight.Add(new Basis(r).X);
        }
        _ctrl.SampleBestLap(_time, out Vector3 head, out Quaternion hr);
        _trailPts.Add(head); _trailAge.Add(0f); _trailRight.Add(new Basis(hr).X);

        _trail.Build(_trailPts, _trailRight, _trailAge, halfW);
    }
}
