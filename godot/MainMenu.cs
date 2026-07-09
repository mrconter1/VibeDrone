using System;
using Godot;

// The title screen, rebuilt on the Ui component library: the VibeDrone logo + subtitle over the
// blurred orbiting arena, then a floating card of icon rows - Start / Levels / Create / Settings /
// Exit (play-first, quit-last) - and a footer key-hint bar. Start opens a Race / Free Fly mode
// picker; Esc raises a Yes/No exit confirm; R hot-reloads. Keyboard-navigable (up/down + Enter).
public partial class MainMenu : MenuScreen
{
    private static readonly string[] ModeOptions = { "Race", "Free Fly" };

    private MenuRow _first = null!;
    private VBoxContainer _rows = null!;
    private PanelContainer _card = null!;
    private Control _confirm = null!, _modePopup = null!;
    private Button _cancelBtn = null!, _exitBtn = null!, _raceBtn = null!, _freeBtn = null!;
    private bool _confirmOpen, _modeOpen;

    protected override void OnShow()
    {
        if (_confirmOpen) CloseConfirm();
        else if (_modeOpen) CloseModePopup();
        else { _first.CallDeferred(Control.MethodName.GrabFocus); FadeIn(); }
    }

    // Drive the menu mode to `want` (no respawn; persists LastMode), then start.
    private void StartWithMode(string want)
    {
        for (int guard = 0; Ctrl.GameModeName != want && guard < ModeOptions.Length; guard++)
            Ctrl.ToggleMenuMode();
        _modeOpen = false;
        _modePopup.Visible = false;
        Ctrl.StartGame();
    }

    protected override bool WantsBack(InputEvent ev) => false;   // root screen: handled below, not by the base
    protected override void Back() { }

    // Esc dismisses whichever popup is open, else raises the exit confirm; R hot-reloads. Enter and
    // Left/Right are left to the focused option buttons in the popups.
    public override void _Input(InputEvent ev)
    {
        if (!Visible || ev is not InputEventKey { Pressed: true } k) return;
        if (k.Keycode == Key.Escape)
        {
            if (_confirmOpen) CloseConfirm();
            else if (_modeOpen) CloseModePopup();
            else OpenConfirm();
            GetViewport().SetInputAsHandled();
        }
        else if (!_confirmOpen && !_modeOpen && k.Keycode == Key.R)
        {
            Ctrl.RequestMainReload();
            GetViewport().SetInputAsHandled();
        }
    }

    private void FadeIn()
    {
        _card.Modulate = new Color(1, 1, 1, 0);
        _card.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic)
            .TweenProperty(_card, "modulate:a", 1f, 0.16);
    }

    protected override void Build()
    {
        VBoxContainer v = CenteredBox(out _, sep: 0);

        v.AddChild(new LogoCanvas { Style = 0, CustomMinimumSize = new Vector2(620, 130) });   // VibeDrone divider logo
        var sub = UiTheme.Body("R E A L I S T I C   F P V   S I M", UiTheme.TextDim, 22);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(sub);
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, UiTheme.S6) });   // spacer

        VBoxContainer rows = Ui.Card(out _card);
        _card.CustomMinimumSize = new Vector2(440, 0);
        _card.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _rows = rows;
        v.AddChild(_card);

        _first = Row("play", "Start", OpenModePopup);
        rows.AddChild(_first);
        rows.AddChild(Row("levels", "Levels", () => Ctrl.OpenLevels(fromPause: false)));
        rows.AddChild(Row("create", "Create", () => Ctrl.CreateLevel()));
        rows.AddChild(Row("settings", "Settings", () => Ctrl.OpenSettings(fromPause: false)));
        rows.AddChild(Row("exit", "Exit", OpenConfirm));

        rows.AddChild(Ui.Divider(UiTheme.S3));
        rows.AddChild(Ui.Hints(("↑↓", "navigate"), ("↵", "select"), ("esc", "exit")));

        BuildVersion();
        BuildConfirm();
        BuildModePopup();
    }

    // Build version, pinned to the bottom-right corner (baked at export by CI). Updates are handled by
    // the external launcher, so the game only shows which build this is.
    private void BuildVersion()
    {
        var overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Get() };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(overlay);

        string ver = ProjectSettings.GetSetting("application/config/version", "dev").AsString();
        var label = UiTheme.Body("v" + ver, UiTheme.TextMuted, 13);
        label.HorizontalAlignment = HorizontalAlignment.Right;
        label.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        label.OffsetLeft = -220; label.OffsetTop = -32;
        label.OffsetRight = -18; label.OffsetBottom = -14;
        overlay.AddChild(label);
    }

    private const float RowWidth = 388f;

    private MenuRow Row(string glyph, string text, Action onPressed)
    {
        var r = new MenuRow(glyph, text, RowWidth, onPressed);
        r.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        return r;
    }

    // Shared modal scaffolding: a dimmed full-screen overlay with a floating card. Returns the hidden
    // root (toggle .Visible) and the inner VBox to fill with the card's content.
    private Control BuildModal(out VBoxContainer box)
    {
        var root = new Control { Theme = UiTheme.Get(), Visible = false };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var dim = new ColorRect { Color = new Color(0.01f, 0.015f, 0.02f, 0.66f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var card = new StyleBoxFlat { BgColor = new Color(0.10f, 0.115f, 0.14f) };
        card.SetCornerRadiusAll(UiTheme.RadLg);
        card.CornerDetail = 6;
        card.BorderColor = new Color(1, 1, 1, 0.10f);
        card.SetBorderWidthAll(1);

        var panel = new PanelContainer { Theme = UiTheme.Get() };
        panel.AddThemeStyleboxOverride("panel", card);
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 44);
        margin.AddThemeConstantOverride("margin_right", 44);
        margin.AddThemeConstantOverride("margin_top", 34);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        panel.AddChild(margin);

        box = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);
        return root;
    }

    // A modal "Exit VibeDrone?" card that floats over the dimmed title screen, with No / Yes.
    private void BuildConfirm()
    {
        _confirm = BuildModal(out VBoxContainer box);

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

        _cancelBtn.FocusNeighborRight = _cancelBtn.GetPathTo(_exitBtn);
        _exitBtn.FocusNeighborLeft = _exitBtn.GetPathTo(_cancelBtn);
    }

    // A modal "Select mode" card: pick Race or Free Fly, then start in that mode.
    private void BuildModePopup()
    {
        _modePopup = BuildModal(out VBoxContainer box);

        var q = UiTheme.Heading("Select mode", 24);
        q.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(q);

        var note = UiTheme.Body("How do you want to fly?", UiTheme.TextDim, 15);
        note.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(note);

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });   // spacer

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 14);
        box.AddChild(row);

        _raceBtn = ConfirmButton("Race", () => StartWithMode("Race"));
        _freeBtn = ConfirmButton("Free Fly", () => StartWithMode("Free Fly"));
        row.AddChild(_raceBtn);
        row.AddChild(_freeBtn);

        _raceBtn.FocusNeighborRight = _raceBtn.GetPathTo(_freeBtn);
        _freeBtn.FocusNeighborLeft = _freeBtn.GetPathTo(_raceBtn);
    }

    private static Button ConfirmButton(string text, Action onPressed)
    {
        Button b = UiTheme.MenuItem(text, onPressed, 170f);
        b.Alignment = HorizontalAlignment.Center;
        return b;
    }

    // Keep the title menu visible behind the card, but drop its rows out of keyboard focus.
    private void SetMenuFocusable(bool on)
    {
        var fm = on ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
        foreach (Node child in _rows.GetChildren())
            if (child is Control c and (MenuRow or Segmented)) c.FocusMode = fm;
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

    private void OpenModePopup()
    {
        _modeOpen = true;
        SetMenuFocusable(false);
        _modePopup.Visible = true;
        // default focus to the last-used mode, so Enter-Enter repeats it
        Button def = Ctrl.GameModeName == "Free Fly" ? _freeBtn : _raceBtn;
        def.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void CloseModePopup()
    {
        _modeOpen = false;
        _modePopup.Visible = false;
        SetMenuFocusable(true);
        _first.CallDeferred(Control.MethodName.GrabFocus);
    }
}
