using Godot;

// Variants of the chosen "Divider" VibeDrone logo: VIBE | DRONE with a vertical accent divider that
// overhangs the text equally above and below, with rounded ends. Thin, tracked, monochrome + accent.
public partial class LogoCanvas : Control
{
    public static readonly string[] Names = { "Divider", "Divider Bold", "Divider Tall", "Divider Minimal" };
    public const int Count = 4;

    public int Style;

    private static readonly Color White = new(0.95f, 0.96f, 0.98f);
    private static readonly Color Cyan = new(0.24f, 0.80f, 0.96f);

    private SystemFont _thin = null!, _cond = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _thin = Sf("Segoe UI Light", "Segoe UI", "Arial");
        _cond = Sf("Bahnschrift", "Arial Narrow", "Segoe UI Semibold", "Arial");
    }

    private static SystemFont Sf(params string[] names) => new() { FontNames = names };

    public override void _Draw()
    {
        switch (Style)
        {
            case 0: Divider(52, 3f, 10f, tag: true); break;    // the base
            case 1: Divider(52, 5f, 10f, tag: true); break;    // thicker bar
            case 2: Divider(52, 3f, 22f, tag: true); break;    // longer overhang
            default: Divider(64, 3.5f, 12f, tag: false); break; // bigger, airy, no tagline
        }
    }

    private float W => Size.X;
    private float H => Size.Y;
    private Vector2 Measure(Font f, string s, int size) => f.GetStringSize(s, HorizontalAlignment.Left, -1, size);

    // VIBE | DRONE. The divider is centred on the caps and overhangs them equally top and bottom,
    // with rounded ends.
    private void Divider(int size, float thick, float overhang, bool tag)
    {
        const string a = "V I B E", b = "D R O N E";
        float wa = Measure(_thin, a, size).X, wb = Measure(_thin, b, size).X;
        float gap = size * 0.7f;
        float x = (W - (wa + gap + wb)) * 0.5f, y = H * 0.5f;

        DrawString(_thin, new Vector2(x, y), a, HorizontalAlignment.Left, -1, size, White);
        DrawString(_thin, new Vector2(x + wa + gap, y), b, HorizontalAlignment.Left, -1, size, White);

        // caps span roughly [y - capH, y] (baseline = y); centre the divider on that and overhang it
        float capH = size * 0.72f;
        float top = (y - capH) - overhang;
        float bot = y + overhang;
        float lx = x + wa + gap * 0.5f;
        var col = new Color(Cyan, 0.85f);
        DrawLine(new Vector2(lx, top), new Vector2(lx, bot), col, thick, antialiased: true);
        DrawCircle(new Vector2(lx, top), thick * 0.5f, col);   // rounded caps
        DrawCircle(new Vector2(lx, bot), thick * 0.5f, col);

        if (tag)
        {
            string t = "R E A L I S T I C   F P V   D R O N E   S I M";
            float tw = Measure(_cond, t, 17).X;
            DrawString(_cond, new Vector2((W - tw) * 0.5f, bot + 34), t, HorizontalAlignment.Left, -1, 17, new Color(White, 0.35f));
        }
    }
}
