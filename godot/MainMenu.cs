using Godot;

// The title screen: the VibeDrone logo + Start / Levels / Create / Settings / Exit, over the blurred
// orbiting arena. Shown at launch and reachable from the pause menu. Keyboard-navigable (up/down +
// Enter). Esc raises a Cancel/Exit confirm (Left/Right + Enter) instead of quitting outright.
public partial class MainMenu : MenuScreen
{
    private Button _first = null!;
    private VBoxContainer _buttons = null!;
    private Control _confirm = null!;
    private Button _cancelBtn = null!, _exitBtn = null!;
    private bool _confirmOpen;

    protected override void OnShow()
    {
        if (_confirmOpen) CloseConfirm();
        else _first.CallDeferred(Control.MethodName.GrabFocus);
    }

    protected override bool WantsBack(InputEvent ev) => false;   // root screen: handled below, not by the base

    protected override void Back() { }

    // Esc raises / dismisses the exit confirm; R hot-reloads (under StartDebug). Enter and Left/Right
    // are left to the focused buttons (so Enter selects the highlighted confirm option).
    public override void _Input(InputEvent ev)
    {
        if (!Visible || ev is not InputEventKey { Pressed: true } k) return;
        if (k.Keycode == Key.Escape)
        {
            if (_confirmOpen) CloseConfirm(); else OpenConfirm();
            GetViewport().SetInputAsHandled();
        }
        else if (!_confirmOpen && k.Keycode == Key.R)
        {
            Ctrl.RequestMainReload();
            GetViewport().SetInputAsHandled();
        }
    }

    protected override void Build()
    {
        VBoxContainer v = CenteredBox(out _);

        v.AddChild(new LogoCanvas { Style = 0, CustomMinimumSize = new Vector2(620, 130) });   // VibeDrone divider logo
        var sub = UiTheme.Body("F P V   T I M E   T R I A L", UiTheme.TextDim, 13);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(sub);
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });   // spacer

        _buttons = new VBoxContainer();
        _buttons.AddThemeConstantOverride("separation", 10);
        v.AddChild(_buttons);

        _first = UiTheme.MenuItem("Start", () => Ctrl.StartGame());
        _buttons.AddChild(_first);
        _buttons.AddChild(UiTheme.MenuItem("Levels", () => Ctrl.OpenLevels(fromPause: false)));
        _buttons.AddChild(UiTheme.MenuItem("Create", () => Ctrl.CreateLevel()));
        _buttons.AddChild(UiTheme.MenuItem("Settings", () => Ctrl.OpenSettings(fromPause: false)));
        _buttons.AddChild(UiTheme.MenuItem("Exit", OpenConfirm));

        BuildConfirm();
    }

    // A modal "Exit VibeDrone?" over a dimmed background, with Cancel / Exit side by side.
    private void BuildConfirm()
    {
        _confirm = new Control { Theme = UiTheme.Get(), Visible = false };
        _confirm.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_confirm);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _confirm.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _confirm.AddChild(center);

        var panel = new PanelContainer { Theme = UiTheme.Get() };
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 30);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 20);
        margin.AddChild(box);

        var q = UiTheme.Heading("Exit VibeDrone?", 22);
        q.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(q);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        box.AddChild(row);

        _cancelBtn = ConfirmButton("Cancel", CloseConfirm);
        _exitBtn = ConfirmButton("Exit", () => GetTree().Quit());
        row.AddChild(_cancelBtn);
        row.AddChild(_exitBtn);

        // Left/Right move between the two options; Enter activates the focused one.
        _cancelBtn.FocusNeighborRight = _cancelBtn.GetPathTo(_exitBtn);
        _exitBtn.FocusNeighborLeft = _exitBtn.GetPathTo(_cancelBtn);
    }

    private static Button ConfirmButton(string text, System.Action onPressed)
    {
        Button b = UiTheme.MenuItem(text, onPressed, 150f);
        b.Alignment = HorizontalAlignment.Center;
        return b;
    }

    private void OpenConfirm()
    {
        _confirmOpen = true;
        _buttons.Visible = false;          // hidden buttons drop out of keyboard focus
        _confirm.Visible = true;
        _cancelBtn.CallDeferred(Control.MethodName.GrabFocus);   // default to the safe option
    }

    private void CloseConfirm()
    {
        _confirmOpen = false;
        _confirm.Visible = false;
        _buttons.Visible = true;
        _first.CallDeferred(Control.MethodName.GrabFocus);
    }
}
