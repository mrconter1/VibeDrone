using System.Globalization;
using Godot;

// In-game sound-test menu. Press S to open (pauses the game) / close. Pick a motor
// sound variant, hold a throttle level (OFF/LOW/MED/MAX) to audition it steadily, and
// shape the tone with low-pass / high-pass / distortion / master-volume tools.
// Runs with ProcessMode=Always so it works while the tree is paused; MotorAudio is
// also Always, so sound keeps generating.
public partial class SoundMenu : CanvasLayer
{
    private MotorAudio _audio = null!;
    private Panel _panel = null!;
    private bool _open;

    // control refs so the Copy button can read current values
    private OptionButton _variantSel = null!;
    private HSlider _lp = null!, _hp = null!, _drive = null!, _vol = null!;
    private Label _status = null!;

    public void Setup(MotorAudio audio) => _audio = audio;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 10;
        BuildUi();
        _panel.Visible = false;
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Keycode: Key.M })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Toggle()
    {
        _open = !_open;
        _panel.Visible = _open;
        GetTree().Paused = _open;
        // cursor only while the menu is open (to click); captured (hidden) otherwise
        Input.MouseMode = _open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        if (_open) _audio.SetEffort(0f);   // start from silence; use the throttle buttons
    }

    private void BuildUi()
    {
        _panel = new Panel { Position = new Vector2(40, 90), Size = new Vector2(430, 560) };
        _panel.SelfModulate = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        AddChild(_panel);

        var v = new VBoxContainer { Position = new Vector2(18, 16), Size = new Vector2(394, 530) };
        v.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(v);

        v.AddChild(new Label { Text = "SOUND TEST   (M to close)" });

        // --- variant selector ---
        _variantSel = new OptionButton();
        _variantSel.AddItem("OFF", 0);
        for (int i = 1; i <= MotorAudio.VariantCount; i++) _variantSel.AddItem($"{i}", i);
        _variantSel.Selected = _audio.Variant;
        _variantSel.ItemSelected += idx => _audio.SetVariant((int)idx);
        v.AddChild(LabeledRow("Variant", _variantSel));

        // --- throttle hold buttons ---
        v.AddChild(new Label { Text = "Hold throttle (audition level)" });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        foreach (var (txt, lvl) in new (string, float)[] { ("OFF", 0f), ("LOW", 0.18f), ("MED", 0.5f), ("MAX", 1f) })
        {
            var b = new Button { Text = txt, CustomMinimumSize = new Vector2(88, 34) };
            float l = lvl;
            b.Pressed += () => _audio.SetEffort(l);
            row.AddChild(b);
        }
        v.AddChild(row);

        // --- audio tools ---
        v.AddChild(Slider("Low-pass cutoff (Hz)", 500, 20000, MotorAudio.DefLowPassHz, 50,
            (val, lbl) => { _audio.SetLowPassHz((float)val); lbl($"{val:0} Hz"); }, out _lp));
        v.AddChild(Slider("High-pass cutoff (Hz)", 20, 2000, MotorAudio.DefHighPassHz, 5,
            (val, lbl) => { _audio.SetHighPassHz((float)val); lbl($"{val:0} Hz"); }, out _hp));
        v.AddChild(Slider("Distortion drive", 0, 1, MotorAudio.DefDrive, 0.01,
            (val, lbl) => { _audio.SetDrive((float)val); lbl($"{val:0.00}"); }, out _drive));
        v.AddChild(Slider("Master volume (dB)", -40, 0, MotorAudio.DefMasterDb, 1,
            (val, lbl) => { _audio.SetMasterDb((float)val); lbl($"{val:0} dB"); }, out _vol));

        // --- copy settings ---
        var copy = new Button { Text = "Copy settings to clipboard", CustomMinimumSize = new Vector2(0, 34) };
        copy.Pressed += OnCopy;
        v.AddChild(copy);
        _status = new Label { Text = "" };
        v.AddChild(_status);
    }

    private void OnCopy()
    {
        var c = CultureInfo.InvariantCulture;
        string text =
            "OpenDrone sound settings\n" +
            $"variant   = {_audio.CurrentName}\n" +
            string.Format(c, "lowpass   = {0:0} Hz\n", _lp.Value) +
            string.Format(c, "highpass  = {0:0} Hz\n", _hp.Value) +
            string.Format(c, "drive     = {0:0.00}\n", _drive.Value) +
            string.Format(c, "master    = {0:0} dB", _vol.Value);
        DisplayServer.ClipboardSet(text);
        _status.Text = "Copied to clipboard - paste it to share.";
    }

    private static HBoxContainer LabeledRow(string label, Control control)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        h.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(120, 0) });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        h.AddChild(control);
        return h;
    }

    // A labelled slider; onChange gets the value and a setter to update the value readout.
    private delegate void SliderChange(double value, System.Action<string> setReadout);
    private static VBoxContainer Slider(string name, double min, double max, double init, double step,
                                        SliderChange onChange, out HSlider slider)
    {
        var box = new VBoxContainer();
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        var caption = new Label { Text = name, CustomMinimumSize = new Vector2(230, 0) };
        var readout = new Label { Text = "" };
        head.AddChild(caption);
        head.AddChild(readout);
        box.AddChild(head);

        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = init,
            CustomMinimumSize = new Vector2(390, 20) };
        System.Action<string> setReadout = t => readout.Text = t;
        s.ValueChanged += val => onChange(val, setReadout);
        box.AddChild(s);
        onChange(init, setReadout);   // initialise effect + readout
        slider = s;
        return box;
    }
}
