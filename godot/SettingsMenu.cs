using Godot;

// Settings screen: UI size, master volume, graphics (MSAA / FXAA / menu supersample), HUD debug,
// and the advanced sound test. Reachable from the main menu and the pause menu. Esc/Space go back.
// The panel auto-fits its contents so it never overflows at any UI scale.
public partial class SettingsMenu : CanvasLayer
{
    private DroneController _ctrl = null!;
    private MotorAudio _audio = null!;
    private SoundMenu _sound = null!;
    private Button _first = null!, _debug = null!, _msaa = null!, _fxaa = null!, _ssaa = null!;

    public void Setup(DroneController ctrl, MotorAudio audio, SoundMenu sound)
    {
        _ctrl = ctrl; _audio = audio; _sound = sound;
    }

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
        if (on) { _debug.Text = DebugLabel(); _first.CallDeferred(Control.MethodName.GrabFocus); }
    }

    public override void _Input(InputEvent ev)
    {
        if (Visible && UiTheme.IsBack(ev))
        {
            _ctrl.MenuBack();
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

        var panel = new PanelContainer { Theme = UiTheme.Get() };
        center.AddChild(panel);

        var pad = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 32);
        panel.AddChild(pad);

        var v = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        v.AddThemeConstantOverride("separation", 12);
        pad.AddChild(v);

        v.AddChild(UiTheme.Title("SETTINGS", 46));
        v.AddChild(new HSeparator());

        // UI size
        v.AddChild(Row("UI size", out Label uiReadout));
        var uiScale = Slider(0.8, 1.5, 0.05, _ctrl.UiScale);
        uiScale.ValueChanged += val => { _ctrl.ApplyUiScale((float)val); uiReadout.Text = $"{val:0.00}×"; };
        uiReadout.Text = $"{_ctrl.UiScale:0.00}×";
        v.AddChild(uiScale);

        // master volume
        v.AddChild(UiTheme.Body("Master volume", UiTheme.TextDim, 15));
        var vol = Slider(-40, 0, 1, MotorAudio.DefMasterDb);
        vol.ValueChanged += val => _audio.SetMasterDb((float)val);
        v.AddChild(vol);

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.Body("Graphics", UiTheme.TextDim, 15));
        _msaa = UiTheme.MenuItem(MsaaLabel(), CycleMsaa, 440f);
        v.AddChild(_msaa);
        _fxaa = UiTheme.MenuItem(FxaaLabel(), ToggleFxaa, 440f);
        v.AddChild(_fxaa);
        _ssaa = UiTheme.MenuItem(SsaaLabel(), ToggleSsaa, 440f);
        v.AddChild(_ssaa);
        _first = _msaa;

        v.AddChild(new HSeparator());
        _debug = UiTheme.MenuItem(DebugLabel(), () => { _ctrl.SetShowDebug(!_ctrl.ShowDebug); _debug.Text = DebugLabel(); }, 440f);
        v.AddChild(_debug);
        v.AddChild(UiTheme.MenuItem("Advanced sound test", () => { _ctrl.MenuBack(); _sound.SetOpen(true); }, 440f));

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.MenuItem("‹  Back", () => _ctrl.MenuBack(), 200f));
        v.AddChild(UiTheme.Body("Esc / Space  back", UiTheme.TextDim, 14));
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
        _ssaa.Text = SsaaLabel();
    }

    private string DebugLabel() => "HUD debug overlay:   " + (_ctrl.ShowDebug ? "ON" : "OFF");
    private string MsaaLabel() => "Anti-aliasing (MSAA):   " + (Config.Msaa == 0 ? "OFF" : $"{Config.Msaa}×");
    private string FxaaLabel() => "Smoothing (FXAA):   " + (Config.Fxaa ? "ON" : "OFF");
    private string SsaaLabel() => "Menu supersampling:   " + (Config.MenuSsaa ? "ON" : "OFF");

    // A "name .......... value" header row with a right-aligned accent readout.
    private static HBoxContainer Row(string name, out Label readout)
    {
        var h = new HBoxContainer();
        h.AddChild(UiTheme.Body(name, UiTheme.TextDim, 15));
        readout = UiTheme.Body("", UiTheme.Accent, 15);
        readout.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        readout.HorizontalAlignment = HorizontalAlignment.Right;
        h.AddChild(readout);
        return h;
    }

    private static HSlider Slider(double min, double max, double step, double value) => new()
    {
        MinValue = min, MaxValue = max, Step = step, Value = value,
        CustomMinimumSize = new Vector2(440, 24), FocusMode = Control.FocusModeEnum.All,
    };
}
