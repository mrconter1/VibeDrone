using System;
using Godot;

// A live tuning panel for the menu backdrop, toggled with B over the full-screen menus. Sliders for
// blur radius / iterations / tint / vignette and buttons for MSAA + FXAA, all applied and persisted
// immediately so you can eyeball the result. Sits above the other menus (Layer 13).
public partial class BlurMenu : CanvasLayer
{
    private DroneController _ctrl = null!;
    private Button _msaa = null!, _fxaa = null!;
    private bool _open;

    public void Setup(DroneController ctrl) => _ctrl = ctrl;

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
    }

    // B toggles (only over a full-screen menu); Esc closes.
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Keycode: Key.B } && (_open || _ctrl.MenuActive))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (_open && ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        var root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        Vector2I win = DisplayServer.WindowGetSize();
        var panel = UiTheme.Panel();
        panel.Size = new Vector2(360, 470);
        panel.Position = new Vector2(win.X - 400, 80);
        root.AddChild(panel);

        var pad = new MarginContainer();
        pad.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 24);
        panel.AddChild(pad);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);
        pad.AddChild(v);

        v.AddChild(UiTheme.Title("BLUR & AA", 30));
        v.AddChild(new HSeparator());

        Slider(v, "Radius", 0, 12, 0.5, Config.BlurRadius, "0.0",
            val => { Config.BlurRadius = (float)val; _ctrl.RefreshBackdrop(); Config.Save(); });
        Slider(v, "Iterations", 0, Config.MaxIterations, 1, Config.BlurIterations, "0",
            val => { Config.BlurIterations = (int)val; _ctrl.RefreshBackdrop(); Config.Save(); });
        Slider(v, "Darken", 0, 1, 0.05, Config.BlurTint, "0.00",
            val => { Config.BlurTint = (float)val; _ctrl.RefreshBackdrop(); Config.Save(); });
        Slider(v, "Vignette", 0, 1, 0.05, Config.BlurVignette, "0.00",
            val => { Config.BlurVignette = (float)val; _ctrl.RefreshBackdrop(); Config.Save(); });

        v.AddChild(new HSeparator());
        _msaa = UiTheme.MenuItem(MsaaLabel(), CycleMsaa, 300f);
        v.AddChild(_msaa);
        _fxaa = UiTheme.MenuItem(FxaaLabel(), ToggleFxaa, 300f);
        v.AddChild(_fxaa);

        v.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        v.AddChild(UiTheme.Body("B / Esc  close", UiTheme.TextDim, 14));
    }

    private void CycleMsaa()
    {
        Config.Msaa = Config.Msaa switch { 0 => 2, 2 => 4, 4 => 8, _ => 0 };
        _ctrl.ApplyAA(); Config.Save();
        _msaa.Text = MsaaLabel();
    }

    private void ToggleFxaa()
    {
        Config.Fxaa = !Config.Fxaa;
        _ctrl.ApplyAA(); Config.Save();
        _fxaa.Text = FxaaLabel();
    }

    private string MsaaLabel() => "MSAA:   " + (Config.Msaa == 0 ? "OFF" : $"{Config.Msaa}×");
    private string FxaaLabel() => "FXAA:   " + (Config.Fxaa ? "ON" : "OFF");

    // A labelled slider with a live value readout.
    private static void Slider(VBoxContainer v, string name, double min, double max, double step,
                               double value, string fmt, Action<double> onChange)
    {
        var head = new HBoxContainer();
        head.AddChild(UiTheme.Body(name, UiTheme.TextDim, 15));
        var readout = UiTheme.Body(value.ToString(fmt), UiTheme.Accent, 15);
        readout.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        readout.HorizontalAlignment = HorizontalAlignment.Right;
        head.AddChild(readout);
        v.AddChild(head);

        var s = new HSlider
        {
            MinValue = min, MaxValue = max, Step = step, Value = value,
            CustomMinimumSize = new Vector2(300, 22), FocusMode = Control.FocusModeEnum.All,
        };
        s.ValueChanged += val => { readout.Text = val.ToString(fmt); onChange(val); };
        v.AddChild(s);
    }
}
