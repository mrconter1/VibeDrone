using System.Collections.Generic;
using Godot;

// Settings screen, organised into tabs: Graphics (UI size, volume, AA, HUD debug, sound test),
// Race Mode (auto-reset on no input), and Drone (FPV camera uptilt). Reachable from the main menu and
// the pause menu. Esc/Space go back. The panel auto-fits so it never overflows at any UI scale.
public partial class SettingsMenu : MenuScreen
{
    private MotorAudio _audio = null!;
    private SoundMenu _sound = null!;
    private Button _debug = null!, _msaa = null!, _fxaa = null!, _ssaa = null!, _autoReset = null!;

    private readonly List<Button> _tabBtns = new();
    private readonly List<Control> _tabPanels = new();
    private int _active;

    public void Setup(DroneController ctrl, MotorAudio audio, SoundMenu sound)
    {
        Ctrl = ctrl; _audio = audio; _sound = sound;
    }

    protected override void OnShow()
    {
        _debug.Text = DebugLabel();
        _tabBtns[_active].CallDeferred(Control.MethodName.GrabFocus);
    }

    protected override void Back() => Ctrl.MenuBack();

    protected override void Build()
    {
        VBoxContainer v = CenteredPanel(Vector2.Zero);

        v.AddChild(UiTheme.Title("SETTINGS", 46));

        var tabBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        tabBar.AddThemeConstantOverride("separation", 6);
        v.AddChild(tabBar);
        v.AddChild(new HSeparator());

        AddTab(tabBar, v, "Graphics", BuildGraphicsTab());
        AddTab(tabBar, v, "Race Mode", BuildRaceTab());
        AddTab(tabBar, v, "Drone", BuildDroneTab());

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.MenuItem("‹  Back", () => Ctrl.MenuBack(), 200f));
        v.AddChild(UiTheme.Body("Esc / Space  back", UiTheme.TextDim, 14));

        ShowTab(0);
    }

    private void AddTab(HBoxContainer bar, VBoxContainer host, string name, Control panel)
    {
        int idx = _tabPanels.Count;
        var b = UiTheme.MenuItem(name, () => ShowTab(idx), 150f);
        b.Alignment = HorizontalAlignment.Center;
        bar.AddChild(b);
        _tabBtns.Add(b);
        panel.CustomMinimumSize = new Vector2(480, 0);   // keep the panel width steady across tabs
        host.AddChild(panel);
        _tabPanels.Add(panel);
    }

    private void ShowTab(int i)
    {
        _active = i;
        for (int k = 0; k < _tabPanels.Count; k++)
        {
            _tabPanels[k].Visible = k == i;
            _tabBtns[k].Modulate = k == i ? Colors.White : new Color(1, 1, 1, 0.55f);
        }
    }

    // --- Graphics tab ---
    private Control BuildGraphicsTab()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);

        v.AddChild(Row("UI size", out Label uiReadout));
        var uiScale = Slider(0.8, 1.5, 0.05, Ctrl.UiScale);
        uiScale.ValueChanged += val => { Ctrl.ApplyUiScale((float)val); uiReadout.Text = $"{val:0.00}×"; };
        uiReadout.Text = $"{Ctrl.UiScale:0.00}×";
        v.AddChild(uiScale);

        v.AddChild(UiTheme.Body("Master volume", UiTheme.TextDim, 15));
        var vol = Slider(-40, 0, 1, MotorAudio.DefMasterDb);
        vol.ValueChanged += val => _audio.SetMasterDb((float)val);
        v.AddChild(vol);

        v.AddChild(new HSeparator());
        _msaa = UiTheme.MenuItem(MsaaLabel(), CycleMsaa, 440f);
        v.AddChild(_msaa);
        _fxaa = UiTheme.MenuItem(FxaaLabel(), ToggleFxaa, 440f);
        v.AddChild(_fxaa);
        _ssaa = UiTheme.MenuItem(SsaaLabel(), ToggleSsaa, 440f);
        v.AddChild(_ssaa);

        v.AddChild(new HSeparator());
        _debug = UiTheme.MenuItem(DebugLabel(), () => { Ctrl.SetShowDebug(!Ctrl.ShowDebug); _debug.Text = DebugLabel(); }, 440f);
        v.AddChild(_debug);
        v.AddChild(UiTheme.MenuItem("Advanced sound test", () => { Ctrl.MenuBack(); _sound.SetOpen(true); }, 440f));
        return v;
    }

    // --- Race Mode tab ---
    private Control BuildRaceTab()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);

        v.AddChild(UiTheme.Body("Auto-reset the run to the start line when you stop flying "
            + "(e.g. after a crash). Only in Race mode.", UiTheme.TextDim, 14));

        _autoReset = UiTheme.MenuItem(AutoResetLabel(), ToggleAutoReset, 440f);
        v.AddChild(_autoReset);

        v.AddChild(Row("Reset after", out Label secReadout));
        var sec = Slider(0.5, 5.0, 0.5, Config.AutoResetSeconds);
        sec.ValueChanged += val => { Config.AutoResetSeconds = (float)val; Config.Save(); secReadout.Text = $"{val:0.0} s"; };
        secReadout.Text = $"{Config.AutoResetSeconds:0.0} s";
        v.AddChild(sec);
        return v;
    }

    // --- Drone tab ---
    private Control BuildDroneTab()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);

        v.AddChild(UiTheme.Body("FPV camera uptilt - how far the nose camera looks up. Higher tilt "
            + "suits faster, more forward flying.", UiTheme.TextDim, 14));

        v.AddChild(Row("Camera angle", out Label tiltReadout));
        var tilt = Slider(0, 50, 1, Ctrl.CameraTilt);
        tilt.ValueChanged += val => { Ctrl.ApplyCameraTilt((float)val); tiltReadout.Text = $"{val:0}°"; };
        tiltReadout.Text = $"{Ctrl.CameraTilt:0}°";
        v.AddChild(tilt);
        return v;
    }

    private void ToggleAutoReset()
    {
        Config.AutoReset = !Config.AutoReset;
        Config.Save();
        _autoReset.Text = AutoResetLabel();
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
    private string AutoResetLabel() => "Auto-reset:   " + (Config.AutoReset ? "ON" : "OFF");

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
