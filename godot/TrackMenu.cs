using Godot;

// Track selection screen: lists every track with its best lap time; click one to load it.
// Opened from the pause menu, dismissed with Esc. Above the pause menu (Layer 12), runs paused.
public partial class TrackMenu : CanvasLayer
{
    private DroneController _ctrl = null!;
    private Panel _panel = null!;
    private VBoxContainer _rows = null!;
    private bool _open;

    private static readonly Color Accent = new(0.62f, 0.98f, 0.76f);
    private static readonly Color Gold = new(1f, 0.85f, 0.3f);

    public bool IsOpen => _open;
    public void Setup(DroneController ctrl) => _ctrl = ctrl;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 12;
        BuildUi();
        _panel.Visible = false;
    }

    public void SetOpen(bool open)
    {
        _open = open;
        _panel.Visible = open;
        GetTree().Paused = open;
        Input.MouseMode = open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        if (open) RefreshRows();   // best times may have changed since last shown
    }

    // Esc closes (caught before the pause menu sees it).
    public override void _Input(InputEvent ev)
    {
        if (_open && ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            SetOpen(false);
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        Vector2I win = DisplayServer.WindowGetSize();
        _panel = new Panel { Size = new Vector2(420, 340), Position = new Vector2(win.X / 2f - 210, win.Y / 2f - 170) };
        _panel.SelfModulate = new Color(0.05f, 0.06f, 0.09f, 0.96f);
        AddChild(_panel);

        var v = new VBoxContainer { Position = new Vector2(28, 24), Size = new Vector2(364, 292) };
        v.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(v);

        var title = new Label { Text = "SELECT TRACK" };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", Accent);
        v.AddChild(title);
        v.AddChild(new HSeparator());

        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 8);
        v.AddChild(_rows);

        var hint = new Label { Text = "Esc to go back" };
        hint.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.5f));
        v.AddChild(hint);
    }

    // Rebuild one row per track (name button + best time), highlighting the active track.
    private void RefreshRows()
    {
        foreach (Node child in _rows.GetChildren()) child.QueueFree();

        for (int i = 0; i < _ctrl.TrackCount; i++)
        {
            int idx = i;
            bool current = i == _ctrl.TrackIndex;
            var row = new HBoxContainer();

            var pick = new Button
            {
                Text = (current ? "▶ " : "   ") + _ctrl.TrackNameAt(i),
                CustomMinimumSize = new Vector2(240, 40),
            };
            pick.Pressed += () => { _ctrl.SelectTrack(idx); SetOpen(false); };
            if (current) pick.AddThemeColorOverride("font_color", Accent);
            row.AddChild(pick);

            var time = new Label
            {
                Text = FmtBest(_ctrl.BestLapAt(i)),
                CustomMinimumSize = new Vector2(110, 40),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            time.AddThemeColorOverride("font_color", Gold);
            row.AddChild(time);

            _rows.AddChild(row);
        }
    }

    private static string FmtBest(float t)
    {
        if (t <= 0f) return "--";
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return m > 0 ? $"{m}:{s:00.00}" : $"{s:0.00}";
    }
}
