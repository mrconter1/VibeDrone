using System;
using Godot;

// One selectable line in a menu: [icon]  label ................ [value].  A Button underneath (so
// keyboard/gamepad up-down navigation and Enter "just work"), but it draws nothing itself - the
// highlight (rounded accent wash + a growing left bar, plus icon/label brightening) is animated in
// _Process/_Draw from an eased 0..1 "highlight" that tracks focus (full) and hover (partial).
public partial class MenuRow : Button
{
    private readonly UiIcon _icon;
    private readonly Label _label;
    private readonly Label? _value;
    private readonly StyleBoxFlat _fill = new();
    private float _t;   // eased highlight

    public MenuRow(string glyph, string text, float minWidth, Action onPressed, string? value = null)
    {
        CustomMinimumSize = new Vector2(minWidth, 56);
        FocusMode = FocusModeEnum.All;
        Flat = true;
        var empty = new StyleBoxEmpty();
        foreach (var s in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(s, empty);
        if (onPressed != null) Pressed += onPressed;

        _fill.SetCornerRadiusAll(UiTheme.RadMd);

        var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        h.SetAnchorsPreset(LayoutPreset.FullRect);
        h.OffsetLeft = UiTheme.S5; h.OffsetRight = -UiTheme.S5;
        h.AddThemeConstantOverride("separation", UiTheme.S4);
        AddChild(h);

        _icon = new UiIcon(glyph, UiTheme.TextDim)
        {
            CustomMinimumSize = new Vector2(24, 24),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        h.AddChild(_icon);

        _label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        _label.AddThemeFontSizeOverride("font_size", 20);
        _label.AddThemeColorOverride("font_color", new Color(UiTheme.Text, 0.80f));
        _label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(_label);

        if (value != null)
        {
            _value = new Label { Text = value, VerticalAlignment = VerticalAlignment.Center };
            _value.AddThemeFontOverride("font", UiTheme.TitleFont);
            _value.AddThemeFontSizeOverride("font_size", 18);
            _value.AddThemeColorOverride("font_color", UiTheme.Accent);
            h.AddChild(_value);
        }
    }

    public void SetValue(string v) { if (_value != null) _value.Text = v; }

    public override void _Process(double delta)
    {
        float target = HasFocus() ? 1f : (IsHovered() ? 0.4f : 0f);
        float nt = Mathf.MoveToward(_t, target, (float)delta * 8f);
        if (Mathf.IsEqualApprox(nt, _t)) return;
        _t = nt;
        _icon.Color = UiTheme.TextDim.Lerp(UiTheme.Text, _t);
        _icon.QueueRedraw();
        _label.AddThemeColorOverride("font_color", new Color(UiTheme.Text, Mathf.Lerp(0.72f, 1f, _t)));
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_t <= 0.01f) return;
        _fill.BgColor = new Color(UiTheme.Accent, 0.13f * _t);
        DrawStyleBox(_fill, new Rect2(Vector2.Zero, Size));
        float barH = Size.Y * 0.52f;
        DrawRect(new Rect2(new Vector2(2, (Size.Y - barH) * 0.5f), new Vector2(3, barH)),
            new Color(UiTheme.Accent, _t), true);
    }
}
