using Godot;

// The chosen "Divider" VibeDrone logo (big, minimal, no tagline): VIBE | DRONE with a vertical accent
// divider centred on the caps, overhanging equally top and bottom. The divider is a pill (rounded
// rect via StyleBoxFlat) so its caps are one smooth, naturally-rounded shape. Variants differ only in
// the divider's thickness / length.
public partial class LogoCanvas : Control
{
    public int Style;

    private static readonly Color White = new(0.95f, 0.96f, 0.98f);
    private static readonly Color Cyan = new(0.24f, 0.80f, 0.96f);

    private SystemFont _thin = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _thin = new SystemFont { FontNames = new[] { "Segoe UI Light", "Segoe UI", "Arial" } };
    }

    public override void _Draw()
    {
        switch (Style)
        {
            case 0: Divider(thick: 4.5f, overhang: 14f); break;   // the chosen one (slightly thinner)
            case 1: Divider(thick: 7f, overhang: 14f); break;     // thicker
            case 2: Divider(thick: 4.5f, overhang: 26f); break;   // longer
            default: Divider(thick: 4.5f, overhang: 7f); break;   // shorter
        }
    }

    private float W => Size.X;
    private float H => Size.Y;
    private Vector2 Measure(string s, int size) => _thin.GetStringSize(s, HorizontalAlignment.Left, -1, size);

    private void Divider(float thick, float overhang)
    {
        const string a = "V I B E", b = "D R O N E";
        const int size = 64;
        float wa = Measure(a, size).X, wb = Measure(b, size).X;
        float gap = size * 0.72f;
        float x = (W - (wa + gap + wb)) * 0.5f, y = H * 0.5f;

        DrawString(_thin, new Vector2(x, y), a, HorizontalAlignment.Left, -1, size, White);
        DrawString(_thin, new Vector2(x + wa + gap, y), b, HorizontalAlignment.Left, -1, size, White);

        // caps span ~[y - capH, y] (baseline = y); centre the divider on that, overhang equally
        float capH = size * 0.72f;
        float top = (y - capH) - overhang;
        float bot = y + overhang;
        float lx = x + wa + gap * 0.5f;

        // one smooth pill: a rounded rect with the corner radius = half the width -> semicircle caps
        var pill = new StyleBoxFlat { BgColor = new Color(Cyan, 0.9f), AntiAliasing = true };
        pill.SetCornerRadiusAll(Mathf.CeilToInt(thick * 0.5f));
        pill.CornerDetail = 16;   // smooth arcs
        DrawStyleBox(pill, new Rect2(lx - thick * 0.5f, top, thick, bot - top));
    }
}
