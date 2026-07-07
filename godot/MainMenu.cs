using Godot;

// The title screen: OPENDRONE + Start / Levels / Settings / Exit, over the blurred orbiting arena.
// Shown at launch and reachable from the pause menu. Keyboard-navigable (up/down + Enter).
public partial class MainMenu : MenuScreen
{
    private Button _first = null!;

    protected override void OnShow() => _first.CallDeferred(Control.MethodName.GrabFocus);

    protected override bool WantsBack(InputEvent ev) => false;   // root screen: Space activates buttons
    protected override void Back() { }

    // L opens the wordmark logo browser, I the app-icon browser; R hot-reloads (under StartDebug).
    public override void _Input(InputEvent ev)
    {
        if (!Visible || ev is not InputEventKey { Pressed: true } k) return;
        if (k.Keycode == Key.L) { Ctrl.OpenLogos(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.I) { Ctrl.OpenIcons(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.R) { Ctrl.RequestMainReload(); GetViewport().SetInputAsHandled(); }
    }

    protected override void Build()
    {
        VBoxContainer v = CenteredBox(out Control root);

        v.AddChild(new LogoCanvas { Style = 0, CustomMinimumSize = new Vector2(620, 130) });   // VibeDrone divider logo
        v.AddChild(UiTheme.Body("FPV TIME TRIAL", UiTheme.TextDim, 18));
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });   // spacer

        _first = UiTheme.MenuItem("Start", () => Ctrl.StartGame());
        v.AddChild(_first);
        v.AddChild(UiTheme.MenuItem("Levels", () => Ctrl.OpenLevels(fromPause: false)));
        v.AddChild(UiTheme.MenuItem("Create", () => Ctrl.CreateLevel()));
        v.AddChild(UiTheme.MenuItem("Settings", () => Ctrl.OpenSettings(fromPause: false)));
        v.AddChild(UiTheme.MenuItem("Exit", () => GetTree().Quit()));

        var hint = UiTheme.Body("L  logos      I  icons", UiTheme.TextDim, 15);
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        hint.Position = new Vector2(28, -40);
        root.AddChild(hint);
    }
}
