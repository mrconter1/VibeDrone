using Godot;

// Logo browser: L on the main menu opens it, Left/Right cycle through the 10 procedural VibeDrone
// logos, L / Esc closes. Full-screen over the title screen. Runs while paused (ProcessMode=Always).
public partial class LogoMenu : CanvasLayer
{
    private LogoCanvas _logo = null!;
    private Label _label = null!;
    private int _index;
    private bool _open;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 13;
        BuildUi();
        Visible = false;
    }

    public void Toggle()
    {
        _open = !_open;
        Visible = _open;
        if (_open) Refresh();
    }

    public override void _Input(InputEvent ev)
    {
        if (!_open || ev is not InputEventKey { Pressed: true } k) return;
        switch (k.Keycode)
        {
            case Key.Left: _index = (_index - 1 + LogoCanvas.Count) % LogoCanvas.Count; Refresh(); break;
            case Key.Right: _index = (_index + 1) % LogoCanvas.Count; Refresh(); break;
            case Key.L or Key.Escape: Toggle(); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void Refresh()
    {
        _logo.Style = _index;
        _logo.QueueRedraw();
        _label.Text = $"LOGO  {_index + 1} / {LogoCanvas.Count}      ·      {LogoCanvas.Names[_index]}";
    }

    private void BuildUi()
    {
        var root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var bg = new ColorRect { Color = new Color(0.02f, 0.025f, 0.03f, 0.99f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;   // block the title buttons behind
        root.AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);
        _logo = new LogoCanvas { CustomMinimumSize = new Vector2(860, 380) };
        center.AddChild(_logo);

        _label = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _label.AddThemeFontOverride("font", UiTheme.TitleFont);
        _label.AddThemeFontSizeOverride("font_size", 22);
        _label.AddThemeColorOverride("font_color", UiTheme.Accent);
        _label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _label.OffsetTop = 40;
        root.AddChild(_label);

        var hint = new Label { Text = "← →  browse styles        L / Esc  close", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeColorOverride("font_color", UiTheme.TextDim);
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        hint.OffsetTop = -56;
        root.AddChild(hint);
    }
}
