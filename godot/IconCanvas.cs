using Godot;

// Square app-icon / favicon variants for VibeDrone, built from the wordmark's language: a dark
// rounded tile, a "VB" monogram, and the same cyan accent - most often colouring the vertical stem of
// the B (the icon echo of the wordmark's divider pill). Ten variants differ in layout/treatment so
// they can be browsed + voted in the LogoMenu, then the winner rendered to a real icon.png.
public partial class IconCanvas : Control
{
    public static readonly string[] Names =
    {
        "Stem",          // 0 - VB, cyan B-stem (primary)
        "Divider",       // 1 - V | B with the wordmark's cyan divider
        "Cyan Tile",     // 2 - filled cyan tile, dark VB
        "Badge",         // 3 - dark circle badge, cyan stem
        "Underline",     // 4 - VB over a cyan underline
        "Outline",       // 5 - cyan hairline border, cyan stem
        "Two-Tone",      // 6 - white V, cyan B
        "Overhang",      // 7 - cyan stem overhanging the caps (wordmark overhang)
        "Props",         // 8 - VB with cyan prop dots in the corners
        "Band",          // 9 - cyan vertical band on the left, VB
    };
    public const int Count = 10;

    public int Style;

    private static readonly Color White = new(0.95f, 0.96f, 0.98f);
    private static readonly Color Cyan = new(0.24f, 0.80f, 0.96f);
    private static readonly Color Dark = new(0.03f, 0.035f, 0.045f);

    private SystemFont _bold = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _bold = new SystemFont { FontNames = new[] { "Bahnschrift", "Segoe UI Semibold", "Arial" } };
    }

    private float W => Size.X;
    private float H => Size.Y;
    private Vector2 Measure(string s, int size) => _bold.GetStringSize(s, HorizontalAlignment.Left, -1, size);

    public override void _Draw()
    {
        float s = Mathf.Min(W, H);
        var c = new Vector2(W * 0.5f, H * 0.5f);
        var tile = new Rect2(c.X - s * 0.5f, c.Y - s * 0.5f, s, s);
        float r = s * 0.22f;   // iOS-ish corner rounding

        switch (Style)
        {
            case 0:   // Stem: dark tile, white VB, cyan B-stem
                Tile(tile, Dark, r);
                Stem(DrawVB(c, s, White), s * 0.055f, 0f);
                break;

            case 1:   // Divider: V | B with the cyan divider pill (icon of the wordmark)
                Tile(tile, Dark, r);
                DrawVBDivider(c, s);
                break;

            case 2:   // Cyan Tile: filled cyan, dark VB
                Tile(tile, Cyan, r);
                DrawVB(c, s, Dark);
                break;

            case 3:   // Badge: dark circle, white VB, cyan stem
                DrawCircle(c, s * 0.5f, Dark);
                Stem(DrawVB(c, s, White), s * 0.055f, 0f);
                break;

            case 4:   // Underline: white VB over a cyan bar
            {
                Tile(tile, Dark, r);
                var g = DrawVB(c, s, White);
                float uy = g.bot + s * 0.10f;
                HPill(c.X, uy, s * 0.34f, s * 0.045f, Cyan);
                break;
            }

            case 5:   // Outline: cyan hairline border, cyan stem
                Tile(tile, Dark, r);
                TileBorder(tile, Cyan, Mathf.Max(2f, s * 0.02f), r);
                Stem(DrawVB(c, s, White), s * 0.055f, 0f);
                break;

            case 6:   // Two-Tone: white V, cyan B
                Tile(tile, Dark, r);
                DrawVB(c, s, White, cyanB: true);
                break;

            case 7:   // Overhang: cyan stem overhanging the caps (like the wordmark divider)
                Tile(tile, Dark, r);
                Stem(DrawVB(c, s, White), s * 0.05f, s * 0.06f);
                break;

            case 8:   // Props: VB with cyan prop dots in two corners
            {
                Tile(tile, Dark, r);
                DrawVB(c, s, White);
                float d = s * 0.055f, m = s * 0.20f;
                DrawCircle(new Vector2(tile.Position.X + m, tile.Position.Y + m), d, Cyan);
                DrawCircle(new Vector2(tile.End.X - m, tile.End.Y - m), d, Cyan);
                break;
            }

            default:  // 9 Band: cyan vertical band on the left, then VB
                Tile(tile, Dark, r);
                var band = new StyleBoxFlat { BgColor = Cyan, AntiAliasing = true };
                band.SetCornerRadiusAll((int)r);
                band.CornerDetail = 16;
                // clip the band's right corners by overlapping a dark rect - simplest: draw a slim pill
                HBand(tile, s * 0.16f);
                DrawVB(new Vector2(c.X + s * 0.08f, c.Y), s, White);
                break;
        }
    }

    // dark/cyan rounded tile
    private void Tile(Rect2 rect, Color col, float radius)
    {
        var b = new StyleBoxFlat { BgColor = col, AntiAliasing = true };
        b.SetCornerRadiusAll((int)radius);
        b.CornerDetail = 16;
        DrawStyleBox(b, rect);
    }

    private void TileBorder(Rect2 rect, Color col, float width, float radius)
    {
        var b = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = col, AntiAliasing = true };
        b.SetBorderWidthAll((int)width);
        b.SetCornerRadiusAll((int)radius);
        b.CornerDetail = 16;
        DrawStyleBox(b, rect);
    }

    // a full-height cyan band down the left of the tile
    private void HBand(Rect2 tile, float width)
    {
        var b = new StyleBoxFlat { BgColor = Cyan, AntiAliasing = true };
        b.SetCornerRadiusAll((int)(width * 0.35f));
        b.CornerDetail = 12;
        DrawStyleBox(b, new Rect2(tile.Position.X + width * 0.6f, tile.Position.Y + tile.Size.Y * 0.16f,
            width, tile.Size.Y * 0.68f));
    }

    // draws "VB" centred in a square of side s; returns the B's left-stem x and the cap top/bottom.
    private (float stemX, float top, float bot, int fs) DrawVB(Vector2 c, float s, Color col, bool cyanB = false)
    {
        int fs = (int)(s * 0.50f);
        float wv = Measure("V", fs).X, wb = Measure("B", fs).X;
        float gap = s * 0.04f;
        float total = wv + gap + wb;
        float x0 = c.X - total * 0.5f;
        float capH = fs * 0.70f;
        float baseY = c.Y + capH * 0.5f;

        DrawString(_bold, new Vector2(x0, baseY), "V", HorizontalAlignment.Left, -1, fs, col);
        float bx = x0 + wv + gap;
        DrawString(_bold, new Vector2(bx, baseY), "B", HorizontalAlignment.Left, -1, fs, cyanB ? Cyan : col);
        return (bx, baseY - capH, baseY, fs);
    }

    // a cyan vertical pill over the B's stem; overhang extends it equally past the caps.
    private void Stem((float stemX, float top, float bot, int fs) g, float thick, float overhang)
    {
        var p = new StyleBoxFlat { BgColor = Cyan, AntiAliasing = true };
        p.SetCornerRadiusAll(Mathf.CeilToInt(thick * 0.5f));
        p.CornerDetail = 16;
        float x = g.stemX + thick * 0.35f;
        DrawStyleBox(p, new Rect2(x - thick * 0.5f, g.top - overhang, thick, (g.bot - g.top) + overhang * 2f));
    }

    // horizontal cyan pill (underline), centred on x
    private void HPill(float cx, float y, float width, float thick, Color col)
    {
        var p = new StyleBoxFlat { BgColor = col, AntiAliasing = true };
        p.SetCornerRadiusAll(Mathf.CeilToInt(thick * 0.5f));
        p.CornerDetail = 16;
        DrawStyleBox(p, new Rect2(cx - width * 0.5f, y - thick * 0.5f, width, thick));
    }

    // V | B with the wordmark's centred cyan divider pill
    private void DrawVBDivider(Vector2 c, float s)
    {
        int fs = (int)(s * 0.50f);
        float wv = Measure("V", fs).X, wb = Measure("B", fs).X;
        float gap = s * 0.16f;
        float total = wv + gap + wb;
        float x0 = c.X - total * 0.5f;
        float capH = fs * 0.70f;
        float baseY = c.Y + capH * 0.5f;

        DrawString(_bold, new Vector2(x0, baseY), "V", HorizontalAlignment.Left, -1, fs, White);
        DrawString(_bold, new Vector2(x0 + wv + gap, baseY), "B", HorizontalAlignment.Left, -1, fs, White);

        float thick = s * 0.045f, overhang = s * 0.05f;
        float lx = x0 + wv + gap * 0.5f;
        var pill = new StyleBoxFlat { BgColor = Cyan, AntiAliasing = true };
        pill.SetCornerRadiusAll(Mathf.CeilToInt(thick * 0.5f));
        pill.CornerDetail = 16;
        DrawStyleBox(pill, new Rect2(lx - thick * 0.5f, (baseY - capH) - overhang, thick, capH + overhang * 2f));
    }
}
