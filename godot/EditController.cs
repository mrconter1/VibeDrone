using System.Collections.Generic;
using Godot;
using OpenDrone;

// Free-fly level builder (E to enter/exit). Fly with WASD (+ Space/Shift), mouse to look, wheel for
// speed. Aim near an object to highlight it; C carries it (fly to place, C drops). While carrying,
// 1-6 rotate and [ ] resize a prop. Aiming (not carrying) shows the inspector top-right: arrow
// Left/Right pick a field, Up/Down nudge it. G spawns a rock, T a gate, K clones, F flips, V recolours,
// Del deletes. Ctrl+Z / Ctrl+Shift+Z (or Ctrl+Y) undo/redo. ProcessMode=Always (runs paused).
public partial class EditController : Node3D
{
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float MoveSpeed = 25f;
    [Export] public float Accel = 7f;
    [Export] public float Reach = 120f;
    [Export] public float HighlightRadius = 5f;
    [Export] public float RotSpeed = 90f;

    private const float PosStep = 0.5f, RotStep = 15f, SizeStep = 0.25f;

    private DroneController _ctrl = null!;
    private Camera3D _cam = null!;
    private Camera3D _droneCam = null!;
    private MotorAudio _audio = null!;
    private Arena _arena = null!;
    private Label _hint = null!;
    private EditReticle _reticle = null!;
    private EditInspector _inspector = null!;
    private CanvasLayer _warn = null!;
    private bool _warnShown;
    private MeshInstance3D _highlight = null!;
    private StandardMaterial3D _highlightMat = null!;
    private bool _active;
    private float _yaw, _pitch;
    private Vector3 _vel;

    private Node3D? _hovered;
    private Node3D? _grabbed;
    private Vector3 _grabLocalPos;
    private int _colorIdx;
    private int _field;            // active inspector field
    private bool _dirtyEdit;       // an arrow nudge is in progress (record history on release)

    private readonly EditHistory _history = new();

    private static readonly Color[] Palette =
    {
        new(0.42f, 0.42f, 0.44f), new(0.30f, 0.31f, 0.34f), new(0.52f, 0.44f, 0.34f),
        new(0.68f, 0.60f, 0.44f), new(0.34f, 0.44f, 0.36f), new(0.55f, 0.35f, 0.30f),
    };

    public void Setup(DroneController ctrl, Camera3D droneCam, MotorAudio audio, Arena arena)
    { _ctrl = ctrl; _droneCam = droneCam; _audio = audio; _arena = arena; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _cam = new Camera3D { Fov = 75f, Current = false };
        AddChild(_cam);

        var layer = new CanvasLayer { Layer = 9 };
        AddChild(layer);
        _hint = new Label
        {
            Text = "EDIT   E fly   WASD/Space/Shift move   wheel speed   C grab/drop   1-6 rotate   " +
                   "G rock   T gate   K clone   F flip   V colour   [ ] size   Del delete   arrows nudge   Ctrl+Z undo",
            Position = new Vector2(40, 40),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(_hint);

        _reticle = new EditReticle { Visible = false };
        layer.AddChild(_reticle);
        _inspector = new EditInspector();
        layer.AddChild(_inspector);

        _highlightMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 1f, 0.5f, 0.16f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _highlight = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 5f, Height = 10f },
            MaterialOverride = _highlightMat,
            Visible = false,
        };
        AddChild(_highlight);

        BuildWarning();
    }

    // A blocking "editing deletes saved runs" confirm, shown before entering the editor on a level
    // that still has records.
    private void BuildWarning()
    {
        _warn = new CanvasLayer { Layer = 12, Visible = false };
        AddChild(_warn);

        var root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _warn.AddChild(root);

        var dim = new ColorRect { Color = new Color(0.01f, 0.015f, 0.02f, 0.66f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer { Theme = UiTheme.Get() };
        center.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 34);
        panel.AddChild(margin);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        box.AddThemeConstantOverride("separation", 12);
        margin.AddChild(box);

        var title = UiTheme.Heading("Edit this level?", 24);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        var body = UiTheme.Body("Editing changes the layout, so this level's saved runs (lap times + "
            + "ghost) will be deleted.", UiTheme.TextDim, 15);
        body.HorizontalAlignment = HorizontalAlignment.Center;
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(body);
        var hint = UiTheme.Body("Enter  edit + clear runs        Esc  cancel", UiTheme.Accent, 15);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(hint);
    }

    public override void _Input(InputEvent ev)
    {
        if (_warnShown)   // modal: Enter confirms (clear + edit), Esc cancels
        {
            if (ev is InputEventKey { Pressed: true } wk)
            {
                if (wk.Keycode is Key.Enter or Key.KpEnter) { ConfirmEdit(); GetViewport().SetInputAsHandled(); }
                else if (wk.Keycode == Key.Escape) { CancelEdit(); GetViewport().SetInputAsHandled(); }
            }
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

    public void Open() { if (!_active) RequestToggle(); }   // enter the builder from code

    // Enter the editor, but first warn (and clear on confirm) if the current level has saved runs.
    private void RequestToggle()
    {
        if (_active) { Toggle(); return; }
        if (_ctrl.CurrentLevelHasRecords()) ShowWarning();
        else Toggle();
    }

    private void ShowWarning()
    {
        _warnShown = true;
        _warn.Visible = true;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void ConfirmEdit()
    {
        _warnShown = false;
        _warn.Visible = false;
        _ctrl.ClearCurrentLevelRecords();
        Toggle();   // enters the editor (pauses + captures the cursor)
    }

    private void CancelEdit()
    {
        _warnShown = false;
        _warn.Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;   // back to flying
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is not InputEventKey key) return;
        if (_warnShown) return;   // the modal handles its own keys in _Input
        if (key is { Pressed: true, Keycode: Key.E, Echo: false }) { RequestToggle(); GetViewport().SetInputAsHandled(); return; }
        if (!_active) return;

        if (key.Pressed) HandleKeyDown(key);
        else if (key.Keycode is Key.Up or Key.Down && _dirtyEdit) { RecordHistory(); _dirtyEdit = false; }
    }

    private void HandleKeyDown(InputEventKey key)
    {
        bool ctrl = key.CtrlPressed, shift = key.ShiftPressed, echo = key.Echo;
        switch (key.Keycode)
        {
            case Key.C when !echo: GrabOrDrop(); break;
            case Key.G when !echo: SpawnRock(); break;
            case Key.T when !echo: SpawnGate(); break;
            case Key.K when !echo: CloneFocused(); break;
            case Key.F when !echo: FlipFocused(); break;
            case Key.V when !echo: RecolorFocused(); break;
            case Key.Delete when !echo: DeleteFocused(); break;
            case Key.R when _grabbed != null && !echo: _grabbed.GlobalRotation = Vector3.Zero; break;
            case Key.Z when ctrl && !echo: if (shift) Redo(); else Undo(); break;
            case Key.Y when ctrl && !echo: Redo(); break;
            case Key.Left when !echo: SwitchField(-1); break;
            case Key.Right when !echo: SwitchField(1); break;
            case Key.Up: EditValue(1f); break;
            case Key.Down: EditValue(-1f); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void Toggle()
    {
        _active = !_active;
        GetTree().Paused = _active;
        _hint.Visible = _active;
        _reticle.Visible = _active;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        if (_active)
        {
            _cam.GlobalPosition = _droneCam.GlobalPosition;
            Vector3 e = _droneCam.GlobalRotation;
            _yaw = e.Y; _pitch = Mathf.Clamp(e.X, -1.55f, 1.55f);
            _cam.Rotation = new Vector3(_pitch, _yaw, 0f);
            _cam.MakeCurrent();
            _vel = Vector3.Zero;
            _audio.SetEffort(0f);
            _field = 0; _dirtyEdit = false;
            _history.Reset(_arena.SnapshotJson());
        }
        else
        {
            if (_grabbed != null) { _grabbed = null; _arena.SaveEdits(); }
            _hovered = null;
            _highlight.Visible = false;
            _inspector.Hide();
            _droneCam.MakeCurrent();
        }
    }

    // --- object actions ---

    private void GrabOrDrop()
    {
        if (_grabbed != null) { _grabbed = null; Commit(); return; }   // drop + record
        if (_hovered != null) Grab(_hovered);
    }

    private void Grab(Node3D node)
    {
        _grabbed = node;
        _hovered = null;
        _grabLocalPos = _cam.GlobalTransform.AffineInverse() * node.GlobalPosition;
    }

    private void SpawnRock()
    {
        if (_grabbed != null) return;
        Grab(_arena.AddProp(_cam.GlobalPosition - _cam.GlobalBasis.Z * 15f));   // grab it to place; drop records
    }

    private void SpawnGate()
    {
        if (_grabbed != null) return;
        Grab(_arena.AddGate(_cam.GlobalPosition - _cam.GlobalBasis.Z * 20f, Quaternion.Identity));
    }

    private void CloneFocused()
    {
        Node3D? f = _grabbed ?? _hovered;
        if (f is PropNode pn)
        {
            Prop d = pn.Data.Clone();
            d.Pos += new Vector3(4f, 0f, 0f);
            Grab(_arena.AddProp(d));
        }
        else if (f != null && _arena.GateIndexOf(f) >= 0)
        {
            Grab(_arena.AddGate(f.GlobalPosition + new Vector3(4f, 0f, 0f), f.GlobalTransform.Basis.GetRotationQuaternion()));
        }
    }

    private void FlipFocused()
    {
        Node3D? f = _grabbed ?? _hovered;
        if (f is PropNode pn) { pn.Data.Rot = (pn.Data.Rot * new Quaternion(Vector3.Forward, Mathf.Pi)).Normalized(); pn.Refresh(); Commit(); }
        else if (f != null) { f.RotateObjectLocal(Vector3.Forward, Mathf.Pi); Commit(); }
    }

    private void RecolorFocused()
    {
        if ((_grabbed ?? _hovered) is not PropNode pn) return;
        _colorIdx = (_colorIdx + 1) % Palette.Length;
        pn.SetColor(Palette[_colorIdx]);
        Commit();
    }

    private void DeleteFocused()
    {
        Node3D? f = _grabbed ?? _hovered;
        if (f is PropNode pn) { ClearSelection(); _arena.RemoveProp(pn); Commit(); }
        else if (f != null)
        {
            int idx = _arena.GateIndexOf(f);
            if (idx > 0) { ClearSelection(); if (_arena.RemoveGate(idx)) Commit(); }
        }
    }

    // --- inspector field editing ---

    private void SwitchField(int dir)
    {
        Node3D? f = _grabbed ?? _hovered;
        if (f == null) return;
        int n = f is PropNode ? 9 : 6;
        _field = ((_field + dir) % n + n) % n;
    }

    private void EditValue(float dir)
    {
        if (_grabbed != null) return;                 // only nudge the aimed (not carried) object
        if (_hovered is PropNode pn)
        {
            switch (_field)
            {
                case 0: pn.Data.Pos.X += PosStep * dir; break;
                case 1: pn.Data.Pos.Y += PosStep * dir; break;
                case 2: pn.Data.Pos.Z += PosStep * dir; break;
                case 3: pn.Data.Rot = RotBy(pn.Data.Rot, Vector3.Right, RotStep * dir); break;
                case 4: pn.Data.Rot = RotBy(pn.Data.Rot, Vector3.Up, RotStep * dir); break;
                case 5: pn.Data.Rot = RotBy(pn.Data.Rot, Vector3.Forward, RotStep * dir); break;
                case 6: pn.Data.Scale.X = Mathf.Clamp(pn.Data.Scale.X + SizeStep * dir, 0.2f, 40f); break;
                case 7: pn.Data.Scale.Y = Mathf.Clamp(pn.Data.Scale.Y + SizeStep * dir, 0.2f, 40f); break;
                case 8: pn.Data.Scale.Z = Mathf.Clamp(pn.Data.Scale.Z + SizeStep * dir, 0.2f, 40f); break;
            }
            pn.Refresh();
        }
        else if (_hovered is { } g)
        {
            switch (_field)
            {
                case 0: g.GlobalPosition += new Vector3(PosStep * dir, 0, 0); break;
                case 1: g.GlobalPosition += new Vector3(0, PosStep * dir, 0); break;
                case 2: g.GlobalPosition += new Vector3(0, 0, PosStep * dir); break;
                case 3: g.RotateObjectLocal(Vector3.Right, Mathf.DegToRad(RotStep * dir)); break;
                case 4: g.RotateObjectLocal(Vector3.Up, Mathf.DegToRad(RotStep * dir)); break;
                case 5: g.RotateObjectLocal(Vector3.Forward, Mathf.DegToRad(RotStep * dir)); break;
            }
        }
        else return;
        _arena.SaveEdits();
        _dirtyEdit = true;   // history recorded on key release
    }

    private static Quaternion RotBy(Quaternion q, Vector3 axis, float deg) =>
        (q * new Quaternion(axis, Mathf.DegToRad(deg))).Normalized();

    // --- history ---
    private void Commit() { _arena.SaveEdits(); RecordHistory(); }
    private void RecordHistory() => _history.Record(_arena.CurrentLevel.ToJson());
    private void Undo() { if (_history.Undo() is { } s) { _arena.RestoreJson(s); ClearSelection(); } }
    private void Redo() { if (_history.Redo() is { } s) { _arena.RestoreJson(s); ClearSelection(); } }

    private void ClearSelection() { _grabbed = null; _hovered = null; _highlight.Visible = false; }

    private Node3D? FindNearAim()
    {
        Vector3 o = _cam.GlobalPosition;
        Vector3 d = -_cam.GlobalBasis.Z;
        Node3D? best = null;
        float bestPerp = HighlightRadius;
        foreach (Node node in GetTree().GetNodesInGroup("movable"))
        {
            if (node is not Node3D g) continue;
            Vector3 v = g.GlobalPosition - o;
            float t = v.Dot(d);
            if (t < 0f || t > Reach) continue;
            float perp = (v - d * t).Length();
            if (perp < bestPerp) { bestPerp = perp; best = g; }
        }
        return best;
    }

    private void UpdatePropRotScale(PropNode pn, float delta)
    {
        float a = Mathf.DegToRad(RotSpeed) * delta;
        Quaternion q = pn.Data.Rot;
        if (Input.IsKeyPressed(Key.Key1)) q *= new Quaternion(Vector3.Forward, a);
        if (Input.IsKeyPressed(Key.Key2)) q *= new Quaternion(Vector3.Forward, -a);
        if (Input.IsKeyPressed(Key.Key3)) q *= new Quaternion(Vector3.Right, a);
        if (Input.IsKeyPressed(Key.Key4)) q *= new Quaternion(Vector3.Right, -a);
        if (Input.IsKeyPressed(Key.Key5)) q *= new Quaternion(Vector3.Up, a);
        if (Input.IsKeyPressed(Key.Key6)) q *= new Quaternion(Vector3.Up, -a);
        pn.Data.Rot = q.Normalized();

        float s = 1f;
        if (Input.IsKeyPressed(Key.Bracketright)) s *= 1f + 1.5f * delta;
        if (Input.IsKeyPressed(Key.Bracketleft)) s *= 1f - 1.5f * delta;
        if (s != 1f) pn.Data.Scale = (pn.Data.Scale * s).Clamp(Vector3.One * 0.4f, Vector3.One * 40f);
    }

    private void RotateGrabbed(float delta)
    {
        float a = Mathf.DegToRad(RotSpeed) * delta;
        if (Input.IsKeyPressed(Key.Key1)) _grabbed!.RotateObjectLocal(Vector3.Forward, a);
        if (Input.IsKeyPressed(Key.Key2)) _grabbed!.RotateObjectLocal(Vector3.Forward, -a);
        if (Input.IsKeyPressed(Key.Key3)) _grabbed!.RotateObjectLocal(Vector3.Right, a);
        if (Input.IsKeyPressed(Key.Key4)) _grabbed!.RotateObjectLocal(Vector3.Right, -a);
        if (Input.IsKeyPressed(Key.Key5)) _grabbed!.RotateObjectLocal(Vector3.Up, a);
        if (Input.IsKeyPressed(Key.Key6)) _grabbed!.RotateObjectLocal(Vector3.Up, -a);
    }

    public override void _Process(double delta)
    {
        if (!_active) return;

        if (_grabbed != null)
        {
            Vector3 pos = _cam.GlobalTransform * _grabLocalPos;
            if (_grabbed is PropNode pn) { UpdatePropRotScale(pn, (float)delta); pn.SetPoseKeepPos(pos); }
            else { _grabbed.GlobalPosition = pos; RotateGrabbed((float)delta); }
        }
        else
        {
            _hovered = FindNearAim();
        }

        Node3D? focus = _grabbed ?? _hovered;
        if (focus != null)
        {
            _highlight.GlobalPosition = focus.GlobalPosition;
            _highlightMat.AlbedoColor = _grabbed != null ? new Color(1f, 0.7f, 0.2f, 0.18f) : new Color(0.3f, 1f, 0.5f, 0.16f);
            _highlight.Visible = true;
            _inspector.Render(FocusTitle(focus), BuildRows(focus), "arrows nudge   K clone   F flip   V colour   Del delete   Ctrl+Z undo");
        }
        else { _highlight.Visible = false; _inspector.Hide(); }

        _reticle.Highlight = _hovered != null;
        _reticle.Grabbing = _grabbed != null;

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

    private string FocusTitle(Node3D focus)
    {
        if (focus is PropNode pn) return pn.Data.Type.ToUpperInvariant();
        int idx = _arena.GateIndexOf(focus);
        return idx == 0 ? "START / FINISH" : $"GATE {idx}";
    }

    private List<EditInspector.Row> BuildRows(Node3D focus)
    {
        Vector3 pos, rot, size;
        bool prop = focus is PropNode;
        if (focus is PropNode pn) { pos = pn.Data.Pos; rot = DegVec(new Basis(pn.Data.Rot).GetEuler()); size = pn.Data.Scale; }
        else { pos = focus.GlobalPosition; rot = focus.GlobalRotationDegrees; size = Vector3.One; }

        var rows = new List<EditInspector.Row>
        {
            new("Pos X", pos.X.ToString("0.0"), _field == 0),
            new("Pos Y", pos.Y.ToString("0.0"), _field == 1),
            new("Pos Z", pos.Z.ToString("0.0"), _field == 2),
            new("Rot X", rot.X.ToString("0"), _field == 3),
            new("Rot Y", rot.Y.ToString("0"), _field == 4),
            new("Rot Z", rot.Z.ToString("0"), _field == 5),
        };
        if (prop && focus is PropNode p)
        {
            rows.Add(new("Size X", size.X.ToString("0.00"), _field == 6));
            rows.Add(new("Size Y", size.Y.ToString("0.00"), _field == 7));
            rows.Add(new("Size Z", size.Z.ToString("0.00"), _field == 8));
            rows.Add(new("Colour", "#" + p.Data.Color.ToHtml(false), false));
            rows.Add(new("Solid", p.Data.Solid ? "yes" : "no", false));
        }
        return rows;
    }

    private static Vector3 DegVec(Vector3 rad) =>
        new(Mathf.RadToDeg(rad.X), Mathf.RadToDeg(rad.Y), Mathf.RadToDeg(rad.Z));
}
