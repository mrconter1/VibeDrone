using System;
using Godot;

// The single source of truth for the menu look: a dark, minimal palette with a restrained cyan
// accent, system fonts, and a shared Theme (panels + buttons with a clear keyboard-focus state).
// Every menu sets its root .Theme = UiTheme.Get() and builds from these helpers, so the whole UI
// restyles from this one file.
public static class UiTheme
{
    // --- palette ---
    public static readonly Color Bg      = new(0.03f, 0.035f, 0.045f);        // deepest background
    public static readonly Color Surface = new(0.075f, 0.085f, 0.10f, 0.72f); // frosted panel fill
    public static readonly Color Border  = new(1f, 1f, 1f, 0.07f);            // hairline
    public static readonly Color Text     = new(0.90f, 0.93f, 0.96f);
    public static readonly Color TextDim  = new(0.90f, 0.93f, 0.96f, 0.42f);
    public static readonly Color TextMuted = new(0.90f, 0.93f, 0.96f, 0.26f);  // section labels / faint
    public static readonly Color Accent   = new(0.24f, 0.80f, 0.96f);         // cyan
    public static readonly Color Good     = new(0.60f, 0.98f, 0.64f);         // best/records (used sparingly)

    // --- component library tokens (Ui.cs / MenuRow / UiIcons build from these) ---
    public static readonly Color Card      = new(0.055f, 0.065f, 0.085f, 0.94f);  // menu card fill
    public static readonly Color CardBorder = new(1f, 1f, 1f, 0.08f);             // card hairline
    public static readonly Color RowHover  = new(1f, 1f, 1f, 0.05f);              // row hover fill (x focus t)
    public static readonly Color RowFocus  = new(Accent.R, Accent.G, Accent.B, 0.16f);  // selected row fill

    // spacing scale (px)
    public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 20, S6 = 24, S8 = 36;
    // corner radii (kept tight - subtle rounding, not pills)
    public const int RadSm = 5, RadMd = 7, RadLg = 9;

    private static Theme _theme = null!;
    private static SystemFont _title = null!, _body = null!;

    public static SystemFont TitleFont => _title ??= Sf("Bahnschrift", "Segoe UI Semibold", "Arial");
    public static SystemFont BodyFont  => _body  ??= Sf("Segoe UI", "Arial");

    private static SystemFont Sf(params string[] names) => new() { FontNames = names };

    public static Theme Get()
    {
        if (_theme != null) return _theme;
        var t = new Theme { DefaultFont = BodyFont, DefaultFontSize = 20 };

        // panels: frosted dark fill + hairline border + rounded corners
        t.SetStylebox("panel", "Panel", Sb(Surface, Border, 1, RadLg, 0));
        t.SetStylebox("panel", "PanelContainer", Sb(Surface, Border, 1, RadLg, 0));

        // buttons: transparent by default, subtle fill on hover, accent left-bar + wash on focus
        t.SetStylebox("normal",   "Button", Sb(new Color(1, 1, 1, 0f), null, 0, RadSm, 10));
        t.SetStylebox("hover",    "Button", Sb(new Color(1, 1, 1, 0.06f), null, 0, RadSm, 10));
        t.SetStylebox("pressed",  "Button", Sb(new Color(Accent, 0.20f), null, 0, RadSm, 10));
        t.SetStylebox("focus",    "Button", FocusBox());
        t.SetStylebox("disabled", "Button", Sb(new Color(1, 1, 1, 0f), null, 0, RadSm, 10));
        t.SetColor("font_color",         "Button", new Color(Text, 0.82f));
        t.SetColor("font_hover_color",   "Button", Text);
        t.SetColor("font_focus_color",   "Button", Accent);
        t.SetColor("font_pressed_color", "Button", Accent);
        t.SetFontSize("font_size", "Button", 21);

        t.SetColor("font_color", "Label", Text);
        return _theme = t;
    }

    // --- component factories ---

    public static Label Title(string text, int size = 44)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", TitleFont);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", Text);
        return l;
    }

    public static Label Heading(string text, int size = 20)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", TitleFont);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", Accent);
        return l;
    }

    public static Label Body(string text, Color? color = null, int size = 17)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color ?? Text);
        return l;
    }

    // A full-width menu item (left aligned), focusable for arrow-key navigation.
    public static Button MenuItem(string text, Action onPressed, float minWidth = 340f)
    {
        var b = new Button
        {
            Text = text,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(minWidth, 56),
            FocusMode = Control.FocusModeEnum.All,
        };
        b.Pressed += onPressed;
        return b;
    }

    // The "go back" keys, shared by every menu: Esc, Backspace, or Space.
    public static bool IsBack(InputEvent ev) =>
        ev is InputEventKey { Pressed: true, Keycode: Key.Escape or Key.Backspace or Key.Space };

    // --- stylebox helpers ---
    private static StyleBoxFlat Sb(Color bg, Color? border, int borderW, int radius, int pad)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.SetCornerRadiusAll(radius);
        if (border is Color b && borderW > 0) { s.BorderColor = b; s.SetBorderWidthAll(borderW); }
        if (pad > 0) { s.SetContentMarginAll(pad); }
        return s;
    }

    // focus: a filled accent pill (no edge bar) - the whole item lights up as the keyboard cursor
    private static StyleBoxFlat FocusBox()
    {
        var s = new StyleBoxFlat { BgColor = new Color(Accent, 0.18f) };
        s.SetCornerRadiusAll(RadSm);
        s.SetContentMarginAll(10);
        return s;
    }
}
