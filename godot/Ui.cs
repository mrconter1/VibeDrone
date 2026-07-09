using Godot;

// Component factory for the menu library: floating card, header + context line, faint section
// labels, hairline dividers, and the footer key-hint bar. Everything reads its colours/spacing from
// UiTheme tokens, so the whole UI restyles from one place. Rows themselves are MenuRow / Segmented.
public static class Ui
{
    // A floating card: near-opaque fill, hairline border, soft drop shadow, big radius. Returns the
    // inner VBox to fill (rows, sections, dividers).
    public static VBoxContainer Card(out PanelContainer panel, int padX = 26, int padTop = 22, int padBottom = 18, int sep = 2)
    {
        var sb = new StyleBoxFlat { BgColor = UiTheme.Card };
        sb.SetCornerRadiusAll(UiTheme.RadLg);
        sb.CornerDetail = 6;
        sb.BorderColor = UiTheme.CardBorder;
        sb.SetBorderWidthAll(1);

        panel = new PanelContainer { Theme = UiTheme.Get() };
        panel.AddThemeStyleboxOverride("panel", sb);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", padX);
        margin.AddThemeConstantOverride("margin_right", padX);
        margin.AddThemeConstantOverride("margin_top", padTop);
        margin.AddThemeConstantOverride("margin_bottom", padBottom);
        panel.AddChild(margin);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", sep);
        margin.AddChild(v);
        return v;
    }

    // Title (big) with a dim context line under it (e.g. track name + best lap). Pass null context to omit.
    public static VBoxContainer Header(string title, string? context)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        var t = UiTheme.Title(title, 34);
        box.AddChild(t);
        if (context != null)
        {
            var c = UiTheme.Body(context, UiTheme.TextDim, 15);
            box.AddChild(c);
        }
        return box;
    }

    // A faint upper-case group label ("PLAY", "OPTIONS"), wrapped with spacing, ready to AddChild.
    public static Control SectionRow(string text)
    {
        var l = new Label { Text = Spread(text.ToUpperInvariant()) };
        l.AddThemeFontOverride("font", UiTheme.TitleFont);
        l.AddThemeFontSizeOverride("font_size", 12);
        l.AddThemeColorOverride("font_color", UiTheme.TextMuted);
        var wrap = new MarginContainer();
        wrap.AddThemeConstantOverride("margin_left", UiTheme.S5);
        wrap.AddThemeConstantOverride("margin_top", UiTheme.S4);
        wrap.AddThemeConstantOverride("margin_bottom", UiTheme.S1);
        wrap.AddChild(l);
        return wrap;
    }

    // A hairline divider with breathing room above and below.
    public static Control Divider(int space = UiTheme.S3)
    {
        var box = new VBoxContainer();
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, space / 2) });
        var line = new ColorRect { Color = new Color(1, 1, 1, 0.06f), CustomMinimumSize = new Vector2(0, 1) };
        box.AddChild(line);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, space / 2) });
        return box;
    }

    // The footer hint bar: small key chips + dim labels, centred.
    public static Control Hints(params (string key, string label)[] hints)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", UiTheme.S5);
        foreach (var (key, label) in hints)
        {
            var group = new HBoxContainer();
            group.AddThemeConstantOverride("separation", UiTheme.S2);
            group.AddChild(KeyChip(key));
            var l = UiTheme.Body(label, UiTheme.TextDim, 13);
            l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            group.AddChild(l);
            row.AddChild(group);
        }
        var wrap = new MarginContainer();
        wrap.AddThemeConstantOverride("margin_top", UiTheme.S4);
        wrap.AddChild(row);
        return wrap;
    }

    private static Control KeyChip(string text)
    {
        var sb = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.06f) };
        sb.SetCornerRadiusAll(UiTheme.RadSm);
        sb.BorderColor = new Color(1, 1, 1, 0.10f);
        sb.SetBorderWidthAll(1);
        sb.SetContentMarginAll(0);
        sb.ContentMarginLeft = sb.ContentMarginRight = 7;
        sb.ContentMarginTop = sb.ContentMarginBottom = 2;
        var p = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        p.AddThemeStyleboxOverride("panel", sb);
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", UiTheme.TitleFont);
        l.AddThemeFontSizeOverride("font_size", 13);
        l.AddThemeColorOverride("font_color", new Color(UiTheme.Text, 0.7f));
        l.HorizontalAlignment = HorizontalAlignment.Center;
        p.AddChild(l);
        return p;
    }

    // Insert thin spaces between characters to fake letter-spacing on section labels.
    private static string Spread(string s) => string.Join(" ", s.ToCharArray());
}
