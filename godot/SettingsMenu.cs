using Godot;

// Settings screen: UI size, master volume, graphics (MSAA / FXAA / menu supersample), HUD debug and
// the advanced sound test. Reachable from the main menu and the pause menu. Esc/Space go back. The
// panel auto-fits so it never overflows at any UI scale.
public partial class SettingsMenu : MenuScreen
{
    private MotorAudio _audio = null!;
    private SoundMenu _sound = null!;
    private Button _first = null!, _debug = null!, _msaa = null!, _fxaa = null!, _ssaa = null!;

    public void Setup(DroneController ctrl, MotorAudio audio, SoundMenu sound)
    {
        Ctrl = ctrl; _audio = audio; _sound = sound;
    }

    protected override void OnShow() { _debug.Text = DebugLabel(); _first.CallDeferred(Control.MethodName.GrabFocus); }
    protected override void Back() => Ctrl.MenuBack();

    protected override void Build()
    {
        VBoxContainer v = CenteredPanel(Vector2.Zero);   // auto-fit

        v.AddChild(UiTheme.Title("SETTINGS", 46));
        v.AddChild(new HSeparator());

        // UI size
        v.AddChild(Row("UI size", out Label uiReadout));
        var uiScale = Slider(0.8, 1.5, 0.05, Ctrl.UiScale);
        uiScale.ValueChanged += val => { Ctrl.ApplyUiScale((float)val); uiReadout.Text = $"{val:0.00}×"; };
        uiReadout.Text = $"{Ctrl.UiScale:0.00}×";
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
        _debug = UiTheme.MenuItem(DebugLabel(), () => { Ctrl.SetShowDebug(!Ctrl.ShowDebug); _debug.Text = DebugLabel(); }, 440f);
        v.AddChild(_debug);
        v.AddChild(UiTheme.MenuItem("Advanced sound test", () => { Ctrl.MenuBack(); _sound.SetOpen(true); }, 440f));

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.MenuItem("‹  Back", () => Ctrl.MenuBack(), 200f));
        v.AddChild(UiTheme.Body("Esc / Space  back", UiTheme.TextDim, 14));
    }

    private void CycleMsaa()
    {
        Config.Msaa = Config.Msaa switch { 0 => 2, 2 => 4, 4 => 8, _ => 0 };
        Ctrl.ApplyAA(); Config.Save();
        _msaa.Text = MsaaLabel();
    }

    private void ToggleFxaa()
    {
        Config.Fxaa = !Config.Fxaa;
        Ctrl.ApplyAA(); Config.Save();
        _fxaa.Text = FxaaLabel();
    }

    private void ToggleSsaa()
    {
        Config.MenuSsaa = !Config.MenuSsaa;
        Ctrl.RefreshSsaa(); Config.Save();
        _ssaa.Text = SsaaLabel();
    }

    private string DebugLabel() => "HUD debug overlay:   " + (Ctrl.ShowDebug ? "ON" : "OFF");
    private string MsaaLabel() => "Anti-aliasing (MSAA):   " + (Config.Msaa == 0 ? "OFF" : $"{Config.Msaa}×");
    private string FxaaLabel() => "Smoothing (FXAA):   " + (Config.Fxaa ? "ON" : "OFF");
    private string SsaaLabel() => "Menu supersampling:   " + (Config.MenuSsaa ? "ON" : "OFF");

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
