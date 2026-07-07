using Godot;

// Startup screen: the VibeDrone logo over a dark backdrop with a progress bar that fills, then the
// whole thing fades out to reveal the main menu underneath. Sits above everything (Layer 20) and
// runs while paused. Begin() plays it once.
public partial class LoadingScreen : CanvasLayer
{
    [Export] public float LoadTime = 1.3f;   // seconds the bar takes to fill
    [Export] public float FadeTime = 0.5f;   // seconds to fade out afterwards

    private Control _root = null!;
    private LoadingBar _bar = null!;
    private float _progress, _fade = 1f;
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
        _progress = 0f; _fade = 1f; _fading = false; _active = true;
        _bar.Progress = 0f; _bar.QueueRedraw();
        _root.Modulate = Colors.White;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!_active) return;
        float d = (float)delta;

        if (!_fading)
        {
            _progress = Mathf.Min(1f, _progress + d / LoadTime);
            _bar.Progress = _progress;
            _bar.QueueRedraw();
            if (_progress >= 1f) _fading = true;
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
        _bar = new LoadingBar { CustomMinimumSize = new Vector2(360, 6) };
        v.AddChild(_bar);
        var lbl = UiTheme.Body("L O A D I N G", UiTheme.TextDim, 14);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(lbl);
    }
}
