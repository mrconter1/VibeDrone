using Godot;

// Controls cheat-sheet, styled from UiTheme. Shown from the pause menu (or H); Esc/any key returns.
// Driven by DroneController's screen coordinator.
public partial class HelpOverlay : CanvasLayer
{
    private DroneController _ctrl = null!;

    private static readonly (string, string)[] Bindings =
    {
        ("Throttle", "W / S   (or pad throttle)"),
        ("Roll", "Left / Right   (or pad)"),
        ("Pitch", "Up / Down   (or pad)"),
        ("Yaw", "Q / C   (or pad)"),
        ("Restart race", "R"),
        ("Pause menu", "Esc"),
        ("This help", "H"),
        ("Edit mode", "E   (free-fly; C grabs a gate, 1-6 rotate)"),
        ("Sound test", "M"),
    };

    public void Setup(DroneController ctrl) => _ctrl = ctrl;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 12;
        BuildUi();
        Visible = false;
    }

    public void Show(bool on) => Visible = on;

    // Any key returns (Esc handled here before other overlays).
    public override void _Input(InputEvent ev)
    {
        if (Visible && ev is InputEventKey { Pressed: true })
        {
            _ctrl.CloseHelp();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        var root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = UiTheme.Panel();
        panel.CustomMinimumSize = new Vector2(600, 470);
        center.AddChild(panel);

        var pad = new MarginContainer();
        pad.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 32);
        panel.AddChild(pad);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        pad.AddChild(v);

        v.AddChild(UiTheme.Title("CONTROLS", 38));
        v.AddChild(new HSeparator());

        foreach (var (action, keys) in Bindings)
        {
            var row = new HBoxContainer();
            row.AddChild(UiTheme.Body(action, UiTheme.Text, 17));
            var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(24, 0) };
            row.AddChild(spacer);
            row.AddChild(UiTheme.Body(keys, UiTheme.Accent, 16));
            v.AddChild(row);
        }

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.MenuItem("‹  Back", () => _ctrl.CloseHelp(), 200f));
        v.AddChild(UiTheme.Body("press any key to close", UiTheme.TextDim, 15));
    }
}
