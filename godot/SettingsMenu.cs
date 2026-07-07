using Godot;

// Settings screen: master volume + HUD debug toggle (+ a shortcut to the advanced sound test).
// Reachable from the main menu and the pause menu. Esc goes back. Built from UiTheme components.
public partial class SettingsMenu : CanvasLayer
{
    private DroneController _ctrl = null!;
    private MotorAudio _audio = null!;
    private SoundMenu _sound = null!;
    private Button _first = null!, _debugBtn = null!;

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
        if (on) { _debugBtn.Text = DebugLabel(); _first.CallDeferred(Control.MethodName.GrabFocus); }
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

        var panel = UiTheme.Panel();
        panel.CustomMinimumSize = new Vector2(600, 430);
        center.AddChild(panel);

        var pad = new MarginContainer();
        pad.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 32);
        panel.AddChild(pad);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 16);
        pad.AddChild(v);

        v.AddChild(UiTheme.Title("SETTINGS", 46));
        v.AddChild(new HSeparator());

        // master volume
        v.AddChild(UiTheme.Body("Master volume", UiTheme.TextDim, 15));
        var vol = new HSlider
        {
            MinValue = -40, MaxValue = 0, Step = 1, Value = MotorAudio.DefMasterDb,
            CustomMinimumSize = new Vector2(440, 24), FocusMode = Control.FocusModeEnum.All,
        };
        vol.ValueChanged += val => _audio.SetMasterDb((float)val);
        _first = null!;   // slider isn't a Button; focus the debug button first instead
        v.AddChild(vol);

        // HUD debug toggle
        _debugBtn = UiTheme.MenuItem(DebugLabel(), () =>
        {
            _ctrl.SetShowDebug(!_ctrl.ShowDebug);
            _debugBtn.Text = DebugLabel();
        }, 440f);
        v.AddChild(_debugBtn);
        _first = _debugBtn;

        // advanced sound test (the existing dev panel)
        v.AddChild(UiTheme.MenuItem("Advanced sound test", () => { _ctrl.MenuBack(); _sound.SetOpen(true); }, 440f));

        v.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        v.AddChild(UiTheme.MenuItem("‹  Back", () => _ctrl.MenuBack(), 200f));
        v.AddChild(UiTheme.Body("Esc / Space  back", UiTheme.TextDim, 15));
    }

    private string DebugLabel() => "HUD debug overlay:   " + (_ctrl.ShowDebug ? "ON" : "OFF");
}
