using Godot;

// Shared scaffolding for the full-screen menus: runs while paused (ProcessMode.Always), builds its
// UI once, starts hidden, and routes the back keys. Subclasses implement Build()/Back() and can
// override the layer, the show hook, and what counts as "back" (Main never backs out; Help closes
// on any key). CenteredPanel() builds the common themed, centred panel so each screen only fills it.
public abstract partial class MenuScreen : CanvasLayer
{
    protected DroneController Ctrl = null!;

    public void Setup(DroneController ctrl) => Ctrl = ctrl;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = LayerNum;
        Build();
        Visible = false;
    }

    protected virtual int LayerNum => 11;
    protected abstract void Build();

    public virtual void Show(bool on)
    {
        Visible = on;
        if (on) OnShow();
    }

    protected virtual void OnShow() { }

    public override void _Input(InputEvent ev)
    {
        if (Visible && WantsBack(ev)) { Back(); GetViewport().SetInputAsHandled(); }
    }

    // Default: Esc / Backspace / Space go back. Help overrides (any key); Main overrides (never).
    protected virtual bool WantsBack(InputEvent ev) => UiTheme.IsBack(ev);
    protected abstract void Back();

    // A themed, centred panel; returns the VBox to fill. A zero minSize auto-fits (PanelContainer).
    protected VBoxContainer CenteredPanel(Vector2 minSize, int pad = 32, int sep = 12)
    {
        var root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        bool fixedSize = minSize != Vector2.Zero;
        Control panel = fixedSize
            ? new Panel { Theme = UiTheme.Get(), CustomMinimumSize = minSize }
            : new PanelContainer { Theme = UiTheme.Get() };
        center.AddChild(panel);

        var margin = new MarginContainer();
        if (fixedSize) margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(m, pad);
        panel.AddChild(margin);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", sep);
        margin.AddChild(v);
        return v;
    }

    // A centred VBox with no panel background (for the title screen). Returns the box to fill.
    protected VBoxContainer CenteredBox(out Control root, int sep = 10)
    {
        root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", sep);
        center.AddChild(v);
        return v;
    }
}
