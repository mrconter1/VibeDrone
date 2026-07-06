using Godot;

// Free-fly "edit" camera, Minecraft-creative style. The game STARTS in edit mode.
// Press E to toggle in/out (drone <-> edit). Fly with WASD (+ Space up / Shift down),
// mouse to look, wheel to change speed. Point near a gate to highlight it (green sphere);
// press C to carry it (follows your position, keeps its orientation), C again to drop.
// While carrying, 1/2 roll, 3/4 pitch, 5/6 yaw the object. ProcessMode=Always (runs paused).
public partial class EditController : Node3D
{
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MoveSpeed = 25f;      // m/s target, adjustable with the mouse wheel
    [Export] public float Accel = 7f;           // lower = floatier (slower to reach speed / coast to stop)
    [Export] public float Reach = 120f;         // max distance to highlight an object
    [Export] public float HighlightRadius = 5f; // how near the aim line must pass a gate to highlight it
    [Export] public float RotSpeed = 90f;       // deg/s for 1-6 object rotation

    private Camera3D _cam = null!;
    private Camera3D _droneCam = null!;          // restored as current when leaving edit mode
    private MotorAudio _audio = null!;           // silenced while in edit mode
    private Arena _arena = null!;                // gate layout is saved through it after a move
    private Label _hint = null!;
    private EditReticle _reticle = null!;
    private MeshInstance3D _highlight = null!;
    private StandardMaterial3D _highlightMat = null!;
    private bool _active;
    private bool _startPending = true;           // enter edit mode on the first frame
    private float _yaw, _pitch;
    private Vector3 _vel;                         // carried momentum, for floaty movement

    private Node3D? _hovered;                     // object under the reticle (highlighted)
    private Node3D? _grabbed;                     // object being carried
    private Vector3 _grabLocalPos;                // carried object position in camera-local space

    public void Setup(Camera3D droneCam, MotorAudio audio, Arena arena)
    { _droneCam = droneCam; _audio = audio; _arena = arena; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _cam = new Camera3D { Fov = 75f, Current = false };
        AddChild(_cam);

        var layer = new CanvasLayer { Layer = 9 };
        AddChild(layer);
        _hint = new Label
        {
            Text = "EDIT MODE   E fly drone   WASD/Space/Shift move   wheel speed   C grab/drop   1-6 rotate",
            Position = new Vector2(40, 40),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,   // don't let it eat mouse events
        };
        layer.AddChild(_hint);

        _reticle = new EditReticle { Visible = false };
        layer.AddChild(_reticle);

        _highlightMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 1f, 0.5f, 0.16f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,   // visible from inside too
        };
        _highlight = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 5f, Height = 10f },
            MaterialOverride = _highlightMat,
            Visible = false,
        };
        AddChild(_highlight);
    }

    // Mouse in _Input so GUI/paused state can never swallow it (look was dropping while WASD held).
    public override void _Input(InputEvent ev)
    {
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

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true } key) return;
        switch (key.Keycode)
        {
            case Key.Escape:
                GetTree().Quit();                       // close the app from anywhere
                break;
            case Key.E:
                Toggle();
                GetViewport().SetInputAsHandled();
                break;
            case Key.C when _active:
                GrabOrDrop();
                GetViewport().SetInputAsHandled();
                break;
            case Key.R when _active && _grabbed != null:
                _grabbed.GlobalRotation = Vector3.Zero;  // reset carried object's orientation
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void Toggle()
    {
        _active = !_active;
        GetTree().Paused = _active;
        _hint.Visible = _active;
        _reticle.Visible = _active;
        Input.MouseMode = Input.MouseModeEnum.Captured;   // hidden either way

        if (_active)
        {
            // start where the drone camera is, looking the same way, for a seamless hand-off
            _cam.GlobalPosition = _droneCam.GlobalPosition;
            Vector3 e = _droneCam.GlobalRotation;
            _yaw = e.Y; _pitch = Mathf.Clamp(e.X, -1.55f, 1.55f);
            _cam.Rotation = new Vector3(_pitch, _yaw, 0f);
            _cam.MakeCurrent();              // switch view to the edit camera
            _vel = Vector3.Zero;
            _audio.SetEffort(0f);            // engine off in edit mode
        }
        else
        {
            if (_grabbed != null) { _grabbed = null; _arena.SaveLayout(); }   // drop + persist
            _hovered = null;
            _highlight.Visible = false;
            _droneCam.MakeCurrent();         // switch view back to the drone
        }
    }

    // C: pick up the highlighted object (carry it), or drop the carried one in place.
    private void GrabOrDrop()
    {
        if (_grabbed != null) { _grabbed = null; _arena.SaveLayout(); return; }   // drop / lock in place + persist
        if (_hovered != null)
        {
            _grabbed = _hovered;
            // camera-local point: stays in front of you as you fly/look, orientation untouched
            _grabLocalPos = _cam.GlobalTransform.AffineInverse() * _grabbed.GlobalPosition;
        }
    }

    // Nearest movable gate whose centre the aim line passes within HighlightRadius (so you only
    // have to point NEAR it, not exactly at a bar). null if none within reach.
    private Node3D? FindNearAim()
    {
        Vector3 o = _cam.GlobalPosition;
        Vector3 d = -_cam.GlobalBasis.Z;         // forward, unit
        Node3D? best = null;
        float bestPerp = HighlightRadius;
        foreach (Node node in GetTree().GetNodesInGroup("movable"))
        {
            if (node is not Node3D g) continue;
            Vector3 v = g.GlobalPosition - o;
            float t = v.Dot(d);                   // distance along the aim line
            if (t < 0f || t > Reach) continue;
            float perp = (v - d * t).Length();    // how far the line misses the centre
            if (perp < bestPerp) { bestPerp = perp; best = g; }
        }
        return best;
    }

    private void RotateGrabbed(float delta)
    {
        if (_grabbed == null) return;
        float a = Mathf.DegToRad(RotSpeed) * delta;
        if (Input.IsKeyPressed(Key.Key1)) _grabbed.RotateObjectLocal(Vector3.Forward, a);   // roll
        if (Input.IsKeyPressed(Key.Key2)) _grabbed.RotateObjectLocal(Vector3.Forward, -a);
        if (Input.IsKeyPressed(Key.Key3)) _grabbed.RotateObjectLocal(Vector3.Right, a);      // pitch
        if (Input.IsKeyPressed(Key.Key4)) _grabbed.RotateObjectLocal(Vector3.Right, -a);
        if (Input.IsKeyPressed(Key.Key5)) _grabbed.RotateObjectLocal(Vector3.Up, a);         // yaw
        if (Input.IsKeyPressed(Key.Key6)) _grabbed.RotateObjectLocal(Vector3.Up, -a);
    }

    public override void _Process(double delta)
    {
        if (_startPending) { _startPending = false; Toggle(); }   // begin in edit mode
        if (!_active) return;

        if (_grabbed != null)
        {
            _grabbed.GlobalPosition = _cam.GlobalTransform * _grabLocalPos;   // follow camera pos, keep orientation
            RotateGrabbed((float)delta);                                     // 1-6 spin it
        }
        else
        {
            _hovered = FindNearAim();
        }

        // translucent sphere around the current target (green = hover, orange = carrying)
        Node3D? focus = _grabbed ?? _hovered;
        if (focus != null)
        {
            _highlight.GlobalPosition = focus.GlobalPosition;
            _highlightMat.AlbedoColor = _grabbed != null
                ? new Color(1f, 0.7f, 0.2f, 0.18f) : new Color(0.3f, 1f, 0.5f, 0.16f);
            _highlight.Visible = true;
        }
        else _highlight.Visible = false;

        _reticle.Highlight = _hovered != null;
        _reticle.Grabbing = _grabbed != null;

        // floaty movement (ease velocity toward target; coast to a stop when keys released)
        Basis b = _cam.GlobalBasis;
        Vector3 dir = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) dir -= b.Z;
        if (Input.IsKeyPressed(Key.S)) dir += b.Z;
        if (Input.IsKeyPressed(Key.A)) dir -= b.X;
        if (Input.IsKeyPressed(Key.D)) dir += b.X;
        if (Input.IsKeyPressed(Key.Space)) dir += Vector3.Up;
        if (Input.IsKeyPressed(Key.Shift)) dir += Vector3.Down;
        Vector3 targetVel = dir == Vector3.Zero ? Vector3.Zero : dir.Normalized() * MoveSpeed;
        _vel = _vel.Lerp(targetVel, 1f - Mathf.Exp(-Accel * (float)delta));
        _cam.GlobalPosition += _vel * (float)delta;
    }
}
