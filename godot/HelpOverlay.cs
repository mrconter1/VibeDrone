using Godot;

// A controls cheat-sheet. Opened from the pause menu (or the H key) and dismissed with any key.
// Sits above the pause menu (Layer 12) and runs while paused (ProcessMode=Always).
public partial class HelpOverlay : CanvasLayer
{
    private Panel _panel = null!;
    private bool _open;

    private static readonly (string, string)[] Bindings =
    {
        ("Throttle", "W / S   (or pad throttle)"),
        ("Roll", "Left / Right   (or pad)"),
        ("Pitch", "Up / Down   (or pad)"),
        ("Yaw", "Q / C   (or pad)"),
        ("Restart race", "R"),
        ("Pause menu", "Esc"),
        ("Sound menu", "M"),
        ("Edit mode", "E   (free-fly; C grabs a gate, 1-6 rotate)"),
        ("This help", "H"),
    };

    public bool IsOpen => _open;

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
    }

    // Any key closes it (caught here before the pause menu sees Esc).
    public override void _Input(InputEvent ev)
    {
        if (_open && ev is InputEventKey { Pressed: true })
        {
            SetOpen(false);
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        Vector2I win = DisplayServer.WindowGetSize();
        _panel = new Panel { Size = new Vector2(460, 360), Position = new Vector2(win.X / 2f - 230, win.Y / 2f - 180) };
        _panel.SelfModulate = new Color(0.05f, 0.06f, 0.09f, 0.96f);
        AddChild(_panel);

        var v = new VBoxContainer { Position = new Vector2(28, 24), Size = new Vector2(404, 312) };
        v.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(v);

        var title = new Label { Text = "CONTROLS" };
        title.AddThemeFontSizeOverride("font_size", 22);
        v.AddChild(title);
        v.AddChild(new HSeparator());

        foreach (var (action, keys) in Bindings)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = action, CustomMinimumSize = new Vector2(150, 0) });
            var k = new Label { Text = keys };
            k.AddThemeColorOverride("font_color", new Color(0.62f, 0.98f, 0.76f));
            row.AddChild(k);
            v.AddChild(row);
        }

        v.AddChild(new HSeparator());
        var hint = new Label { Text = "press any key to close" };
        hint.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.5f));
        v.AddChild(hint);
    }
}
