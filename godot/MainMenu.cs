using Godot;

// The title screen: OPENDRONE + Start / Levels / Settings / Exit, over the blurred orbiting arena.
// Shown at launch and reachable from the pause menu. Keyboard-navigable (up/down + Enter).
public partial class MainMenu : CanvasLayer
{
    private DroneController _ctrl = null!;
    private Button _first = null!;
    private Label _blur = null!;

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
        if (on) { _blur.Text = BlurText(_ctrl.BlurName); _first.CallDeferred(Control.MethodName.GrabFocus); }
    }

    // B cycles the menu-blur technique (only from the title screen), for eyeballing which looks best.
    public override void _Input(InputEvent ev)
    {
        if (Visible && ev is InputEventKey { Pressed: true, Keycode: Key.B })
        {
            _blur.Text = BlurText(_ctrl.CycleBlur());
            GetViewport().SetInputAsHandled();
        }
    }

    private static string BlurText(string name) => $"BLUR   {name}      [B to cycle]";

    private void BuildUi()
    {
        var root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        center.AddChild(v);

        v.AddChild(UiTheme.Title("OPENDRONE", 76));
        var sub = UiTheme.Body("FPV TIME TRIAL", UiTheme.TextDim, 18);
        v.AddChild(sub);
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 34) });   // spacer

        _first = UiTheme.MenuItem("Start", () => _ctrl.StartGame());
        v.AddChild(_first);
        v.AddChild(UiTheme.MenuItem("Levels", () => _ctrl.OpenLevels(fromPause: false)));
        v.AddChild(UiTheme.MenuItem("Settings", () => _ctrl.OpenSettings(fromPause: false)));
        v.AddChild(UiTheme.MenuItem("Exit", () => GetTree().Quit()));

        // blur-technique readout, bottom-left
        _blur = UiTheme.Body("", UiTheme.TextDim, 15);
        _blur.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _blur.Position = new Vector2(28, -40);
        root.AddChild(_blur);
    }
}
