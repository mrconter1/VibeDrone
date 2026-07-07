using Godot;

// The title screen: OPENDRONE + Start / Levels / Settings / Exit, over the blurred orbiting arena.
// Shown at launch and reachable from the pause menu. Keyboard-navigable (up/down + Enter).
public partial class MainMenu : MenuScreen
{
    private Button _first = null!;

    protected override void OnShow() => _first.CallDeferred(Control.MethodName.GrabFocus);

    protected override bool WantsBack(InputEvent ev) => false;   // root screen: Space activates buttons
    protected override void Back() { }

    protected override void Build()
    {
        VBoxContainer v = CenteredBox(out _);

        v.AddChild(UiTheme.Title("OPENDRONE", 76));
        v.AddChild(UiTheme.Body("FPV TIME TRIAL", UiTheme.TextDim, 18));
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 34) });   // spacer

        _first = UiTheme.MenuItem("Start", () => Ctrl.StartGame());
        v.AddChild(_first);
        v.AddChild(UiTheme.MenuItem("Levels", () => Ctrl.OpenLevels(fromPause: false)));
        v.AddChild(UiTheme.MenuItem("Settings", () => Ctrl.OpenSettings(fromPause: false)));
        v.AddChild(UiTheme.MenuItem("Exit", () => GetTree().Quit()));
    }
}
