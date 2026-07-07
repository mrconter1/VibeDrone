using Godot;

// In-game pause menu, styled from UiTheme and driven by DroneController's screen coordinator.
// Esc resumes. Navigation (Levels/Settings/Controls/Main menu) routes back through the controller.
public partial class PauseMenu : CanvasLayer
{
    private DroneController _ctrl = null!;
    private Button _first = null!;

    public void Setup(DroneController ctrl) => _ctrl = ctrl;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 11;
        BuildUi();
        Visible = false;
    }

    public void Show(bool on)
    {
        Visible = on;
        if (on) _first.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Input(InputEvent ev)
    {
        if (Visible && ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            _ctrl.ResumeGame();
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
        panel.CustomMinimumSize = new Vector2(360, 470);
        center.AddChild(panel);

        var pad = new MarginContainer();
        pad.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 28);
        panel.AddChild(pad);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        pad.AddChild(v);

        v.AddChild(UiTheme.Title("PAUSED", 34));
        v.AddChild(new HSeparator());

        _first = UiTheme.MenuItem("Resume", () => _ctrl.ResumeGame(), 300f);
        v.AddChild(_first);
        v.AddChild(UiTheme.MenuItem("Restart lap", () => _ctrl.RestartRace(), 300f));
        v.AddChild(UiTheme.MenuItem("Levels", () => _ctrl.OpenLevels(fromPause: true), 300f));
        v.AddChild(UiTheme.MenuItem("Watch best lap", () => _ctrl.WatchBest(_ctrl.TrackIndex), 300f));
        v.AddChild(UiTheme.MenuItem("Settings", () => _ctrl.OpenSettings(fromPause: true), 300f));
        v.AddChild(UiTheme.MenuItem("Controls", () => _ctrl.OpenHelp(), 300f));
        v.AddChild(UiTheme.MenuItem("Main menu", () => _ctrl.OpenMain(), 300f));
        v.AddChild(UiTheme.MenuItem("Exit game", () => GetTree().Quit(), 300f));
    }
}
