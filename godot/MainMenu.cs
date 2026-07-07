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
        var sub = UiTheme.Body("F P V   T I M E   T R I A L", UiTheme.TextDim, 22);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(sub);
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 34) });   // spacer

        _buttons = new VBoxContainer();
        _buttons.AddThemeConstantOverride("separation", 10);
        v.AddChild(_buttons);

        // Inset the labels so the text column (as wide as the longest label) is centered inside the
        // wide pill; the labels stay left-aligned to a common edge, and that column sits at screen centre.
        string[] labels = { "Start", "Levels", "Create", "Settings", "Exit" };
        float widest = 0f;
        foreach (string s in labels)
            widest = Mathf.Max(widest, UiTheme.BodyFont.GetStringSize(s, HorizontalAlignment.Left, -1, 21).X);
        float pad = Mathf.Max(12f, (MenuWidth - widest) * 0.5f);

        _first = MainItem("Start", () => Ctrl.StartGame(), pad);
        _buttons.AddChild(_first);
        _buttons.AddChild(MainItem("Levels", () => Ctrl.OpenLevels(fromPause: false), pad));
        _buttons.AddChild(MainItem("Create", () => Ctrl.CreateLevel(), pad));
        _buttons.AddChild(MainItem("Settings", () => Ctrl.OpenSettings(fromPause: false), pad));
        _buttons.AddChild(MainItem("Exit", OpenConfirm, pad));

        BuildConfirm();
    }

    private const float MenuWidth = 440f;

    // A title-menu item: a wide pill centered on screen (ShrinkCenter), with the label left-aligned
    // but inset by `pad` on both sides so the (left-aligned) text column is centered within the pill.
    private Button MainItem(string text, System.Action onPressed, float pad)
    {
        Button b = UiTheme.MenuItem(text, onPressed, MenuWidth);
        b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        b.Alignment = HorizontalAlignment.Left;
        foreach (string state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
        {
            var sb = (StyleBox)UiTheme.Get().GetStylebox(state, "Button").Duplicate();
            sb.ContentMarginLeft = pad;
            sb.ContentMarginRight = pad;
            sb.ContentMarginTop = 10;
            sb.ContentMarginBottom = 10;
            b.AddThemeStyleboxOverride(state, sb);
        }
        return b;
    }

    // A modal "Exit VibeDrone?" card that floats over the dimmed title screen, with Cancel / Exit.
    private void BuildConfirm()
    {
        _confirm = new Control { Theme = UiTheme.Get(), Visible = false };
        _confirm.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_confirm);

        // dim the whole title screen behind the card so it clearly sits on top of the menu
        var dim = new ColorRect { Color = new Color(0.01f, 0.015f, 0.02f, 0.66f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _confirm.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _confirm.AddChild(center);

        // a solid floating card (opaque, hairline border, soft drop shadow) - reads clearly over the
        // dimmed menu rather than the theme's near-transparent frosted panel.
        var card = new StyleBoxFlat { BgColor = new Color(0.10f, 0.115f, 0.14f) };
        card.SetCornerRadiusAll(16);
        card.CornerDetail = 8;
        card.BorderColor = new Color(1, 1, 1, 0.10f);
        card.SetBorderWidthAll(1);
        card.ShadowColor = new Color(0, 0, 0, 0.5f);
        card.ShadowSize = 26;

        var panel = new PanelContainer { Theme = UiTheme.Get() };
        panel.AddThemeStyleboxOverride("panel", card);
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 44);
        margin.AddThemeConstantOverride("margin_right", 44);
        margin.AddThemeConstantOverride("margin_top", 34);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        panel.AddChild(margin);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        var q = UiTheme.Heading("Exit VibeDrone?", 24);
        q.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(q);

        var note = UiTheme.Body("Your best laps are saved.", UiTheme.TextDim, 15);
        note.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(note);

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });   // spacer

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 14);
        box.AddChild(row);

        _cancelBtn = ConfirmButton("No", CloseConfirm);
        _exitBtn = ConfirmButton("Yes", () => GetTree().Quit());
        row.AddChild(_cancelBtn);
        row.AddChild(_exitBtn);

        // Left/Right move between the two options; Enter activates the focused one.
        _cancelBtn.FocusNeighborRight = _cancelBtn.GetPathTo(_exitBtn);
        _exitBtn.FocusNeighborLeft = _exitBtn.GetPathTo(_cancelBtn);
    }

    private static Button ConfirmButton(string text, System.Action onPressed)
    {
        Button b = UiTheme.MenuItem(text, onPressed, 170f);
        b.Alignment = HorizontalAlignment.Center;
        return b;
    }

    // Keep the title menu visible behind the card, but drop its buttons out of keyboard focus.
    private void SetMenuFocusable(bool on)
    {
        foreach (Node child in _buttons.GetChildren())
            if (child is Button b) b.FocusMode = on ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
    }

    private void OpenConfirm()
    {
        _confirmOpen = true;
        SetMenuFocusable(false);
        _confirm.Visible = true;
        _cancelBtn.CallDeferred(Control.MethodName.GrabFocus);   // default to the safe option
    }

    private void CloseConfirm()
    {
        _confirmOpen = false;
        _confirm.Visible = false;
        SetMenuFocusable(true);
        _first.CallDeferred(Control.MethodName.GrabFocus);
    }
}
