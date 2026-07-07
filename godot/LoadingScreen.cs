using Godot;

// Startup screen: the VibeDrone logo over a dark backdrop, held briefly then faded out to reveal the
// main menu underneath. Continues the engine boot splash (same logo) so boot -> menu reads as one
// smooth thing rather than a loading step - there's no real loading to wait on. Sits above everything
// (Layer 20) and runs while paused. Begin() plays it once.
public partial class LoadingScreen : CanvasLayer
{
    [Export] public float HoldTime = 0.4f;   // seconds to hold the logo before fading
    [Export] public float FadeTime = 0.5f;   // seconds to fade out afterwards

    private Control _root = null!;
    private float _hold, _fade = 1f;
    private bool _active, _fading;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 20;
        BuildUi();
        Visible = false;
    }

    public void Begin()
    {
        _hold = 0f; _fade = 1f; _fading = false; _active = true;
        _root.Modulate = Colors.White;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!_active) return;
        float d = (float)delta;

        if (!_fading)
        {
            _hold += d;
            if (_hold >= HoldTime) _fading = true;
        }
        else
        {
            _fade -= d / FadeTime;
            _root.Modulate = new Color(1, 1, 1, Mathf.Max(0f, _fade));
            if (_fade <= 0f) { _active = false; Visible = false; }
        }
    }

    private void BuildUi()
    {
        _root = new Control { Theme = UiTheme.Get() };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        var bg = new ColorRect { Color = new Color(0.02f, 0.025f, 0.03f, 1f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var v = new VBoxContainer { CustomMinimumSize = new Vector2(620, 0) };
        v.AddThemeConstantOverride("separation", 22);
        center.AddChild(v);

        v.AddChild(new LogoCanvas { Style = 0, CustomMinimumSize = new Vector2(620, 130) });
        var lbl = UiTheme.Body("FPV TIME TRIAL", UiTheme.TextDim, 15);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(lbl);
    }
}
