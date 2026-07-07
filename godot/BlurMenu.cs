using System;
using Godot;

// A live tuning panel for the menu backdrop, toggled with B over the full-screen menus. Type,
// radius, iterations, darken and vignette for the blur, plus MSAA / FXAA / menu-TAA for aliasing.
// Everything applies and persists immediately. The panel auto-fits its contents (PanelContainer)
// and parks top-right. Sits above the other menus (Layer 13).
public partial class BlurMenu : CanvasLayer
{
    private DroneController _ctrl = null!;
    private Control _root = null!;
    private PanelContainer _panel = null!;
    private Button _type = null!, _msaa = null!, _fxaa = null!, _taa = null!;
    private bool _open;

    private static readonly string[] BlurTypes = { "Gaussian", "Kawase (wide)", "Box" };

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
        if (_open) CallDeferred(MethodName.Reposition);
    }

    private void Reposition() => _panel.Position = new Vector2(_root.Size.X - _panel.Size.X - 28, 28);

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
        _root = new Control { Theme = UiTheme.Get() };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_root);

        _panel = new PanelContainer { Theme = UiTheme.Get(), Position = new Vector2(1200, 28) };
        _root.AddChild(_panel);

        var pad = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 24);
        _panel.AddChild(pad);

        var v = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        v.AddThemeConstantOverride("separation", 12);
        pad.AddChild(v);

        v.AddChild(UiTheme.Title("BLUR & AA", 30));
        v.AddChild(new HSeparator());

        _type = UiTheme.MenuItem(TypeLabel(), CycleType, 300f);
        v.AddChild(_type);
        Slider(v, "Radius", 0, 12, 0.5, Config.BlurRadius, "0.0",
            val => { Config.BlurRadius = (float)val; _ctrl.RefreshBackdrop(); Config.Save(); });
        Slider(v, "Iterations", 0, Config.MaxIterations, 1, Config.BlurIterations, "0",
            val => { Config.BlurIterations = (int)val; _ctrl.RefreshBackdrop(); Config.Save(); });
        Slider(v, "Darken", 0, 1, 0.05, Config.BlurTint, "0.00",
            val => { Config.BlurTint = (float)val; _ctrl.RefreshBackdrop(); Config.Save(); });
        Slider(v, "Vignette", 0, 1, 0.05, Config.BlurVignette, "0.00",
            val => { Config.BlurVignette = (float)val; _ctrl.RefreshBackdrop(); Config.Save(); });

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.Body("Anti-aliasing (fixes shimmer)", UiTheme.TextDim, 14));
        _msaa = UiTheme.MenuItem(MsaaLabel(), CycleMsaa, 300f);
        v.AddChild(_msaa);
        _fxaa = UiTheme.MenuItem(FxaaLabel(), ToggleFxaa, 300f);
        v.AddChild(_fxaa);
        _taa = UiTheme.MenuItem(SsaaLabel(), ToggleSsaa, 300f);
        v.AddChild(_taa);

        v.AddChild(UiTheme.Body("B / Esc  close", UiTheme.TextDim, 14));
    }

    private void CycleType()
    {
        Config.BlurType = (Config.BlurType + 1) % BlurTypes.Length;
        _ctrl.RefreshBackdrop(); Config.Save();
        _type.Text = TypeLabel();
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

    private void ToggleSsaa()
    {
        Config.MenuSsaa = !Config.MenuSsaa;
        _ctrl.RefreshSsaa(); Config.Save();
        _taa.Text = SsaaLabel();
    }

    private string TypeLabel() => $"Type:   {BlurTypes[Config.BlurType]}";
    private string MsaaLabel() => "MSAA:   " + (Config.Msaa == 0 ? "OFF" : $"{Config.Msaa}×");
    private string FxaaLabel() => "FXAA:   " + (Config.Fxaa ? "ON" : "OFF");
    private string SsaaLabel() => "Supersample (menu):   " + (Config.MenuSsaa ? "ON" : "OFF");

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
