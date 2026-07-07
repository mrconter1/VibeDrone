using Godot;

// 10 variants of the minimal "VibeDrone" logo (the style picked from the first round): thin, tracked,
// mostly monochrome with a single restrained accent. Vary casing, colour split, and the accent
// element (dot / underline / brackets / divider / marker). Vector drawing + system fonts only.
public partial class LogoCanvas : Control
{
    public static readonly string[] Names =
    {
        "Classic", "Two-tone", "Underline", "Lime", "Amber",
        "Lowercase", "Bracketed", "Divider", "Airy", "Marker",
    };
    public const int Count = 10;

    public int Style;

    private static readonly Color White = new(0.95f, 0.96f, 0.98f);
    private static readonly Color Cyan = new(0.24f, 0.80f, 0.96f);
    private static readonly Color Lime = new(0.60f, 0.98f, 0.64f);
    private static readonly Color Amber = new(0.95f, 0.82f, 0.45f);

    private const string Word = "V I B E D R O N E";

    private SystemFont _thin = null!, _cond = null!, _reg = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _thin = Sf("Segoe UI Light", "Segoe UI", "Arial");
        _cond = Sf("Bahnschrift", "Arial Narrow", "Segoe UI Semibold", "Arial");
        _reg = Sf("Segoe UI", "Arial");
    }

    private static SystemFont Sf(params string[] names) => new() { FontNames = names };

    public override void _Draw()
    {
        switch (Style)
        {
            case 0: Classic(); break;
            case 1: TwoTone(); break;
            case 2: Underline(); break;
            case 3: Accent(Lime); break;
            case 4: Accent(Amber); break;
            case 5: Lowercase(); break;
            case 6: Bracketed(); break;
            case 7: Divider(); break;
            case 8: Airy(); break;
            default: Marker(); break;
        }
    }

    private float W => Size.X;
    private float H => Size.Y;
    private Vector2 Measure(Font f, string s, int size) => f.GetStringSize(s, HorizontalAlignment.Left, -1, size);

    private void CenterText(Font f, string s, float y, int size, Color c)
    {
        float x = (W - Measure(f, s, size).X) * 0.5f;
        DrawString(f, new Vector2(x, y), s, HorizontalAlignment.Left, -1, size, c);
    }

    private void Tag(Color c, float y) => CenterText(_cond, "R E A L I S T I C   F P V   D R O N E   S I M", y, 17, c);

    // --- variants ---
    private void Classic()   // the winner: thin tracked word, single accent dot, dim tagline
    {
        CenterText(_thin, Word, H * 0.5f, 58, White);
        DrawCircle(new Vector2(W * 0.5f, H * 0.5f + 34), 4f, Cyan);
        Tag(new Color(White, 0.35f), H * 0.5f + 72);
    }

    private void Accent(Color accent)   // Classic with a lime / amber accent
    {
        CenterText(_thin, Word, H * 0.5f, 58, White);
        DrawCircle(new Vector2(W * 0.5f, H * 0.5f + 34), 4f, accent);
        Tag(new Color(accent, 0.55f), H * 0.5f + 72);
    }

    private void TwoTone()   // "VIBE" white, "DRONE" cyan
    {
        const string a = "V I B E  ", b = "D R O N E";
        int size = 56;
        float wa = Measure(_thin, a, size).X, wb = Measure(_thin, b, size).X;
        float x = (W - (wa + wb)) * 0.5f, y = H * 0.5f;
        DrawString(_thin, new Vector2(x, y), a, HorizontalAlignment.Left, -1, size, White);
        DrawString(_thin, new Vector2(x + wa, y), b, HorizontalAlignment.Left, -1, size, Cyan);
        Tag(new Color(White, 0.35f), y + 72);
    }

    private void Underline()   // thin accent bar under the word instead of a dot
    {
        int size = 54;
        CenterText(_thin, Word, H * 0.5f, size, White);
        float bw = Measure(_thin, Word, size).X;
        DrawRect(new Rect2((W - bw) * 0.5f, H * 0.5f + 18, bw, 2f), Cyan);
        Tag(new Color(White, 0.35f), H * 0.5f + 60);
    }

    private void Lowercase()   // lowercase, softer
    {
        CenterText(_thin, "v i b e d r o n e", H * 0.5f, 60, White);
        DrawCircle(new Vector2(W * 0.5f, H * 0.5f + 34), 4f, Cyan);
        Tag(new Color(White, 0.35f), H * 0.5f + 72);
    }

    private void Bracketed()   // [ VIBEDRONE ] with accent brackets
    {
        int size = 52;
        float bx = Measure(_thin, "[", size).X, ww = Measure(_thin, Word, size).X;
        const float gap = 26;
        float total = bx + gap + ww + gap + bx;
        float x = (W - total) * 0.5f, y = H * 0.5f;
        DrawString(_thin, new Vector2(x, y), "[", HorizontalAlignment.Left, -1, size, Cyan);
        DrawString(_thin, new Vector2(x + bx + gap, y), Word, HorizontalAlignment.Left, -1, size, White);
        DrawString(_thin, new Vector2(x + bx + gap + ww + gap, y), "]", HorizontalAlignment.Left, -1, size, Cyan);
        Tag(new Color(White, 0.35f), y + 60);
    }

    private void Divider()   // VIBE | DRONE with a thin accent divider
    {
        const string a = "V I B E", b = "D R O N E";
        int size = 52;
        float wa = Measure(_thin, a, size).X, wb = Measure(_thin, b, size).X;
        const float gap = 34;
        float x = (W - (wa + gap + wb)) * 0.5f, y = H * 0.5f;
        DrawString(_thin, new Vector2(x, y), a, HorizontalAlignment.Left, -1, size, White);
        float lx = x + wa + gap * 0.5f;
        DrawLine(new Vector2(lx, y - size * 0.72f), new Vector2(lx, y + size * 0.12f), new Color(Cyan, 0.75f), 2f);
        DrawString(_thin, new Vector2(x + wa + gap, y), b, HorizontalAlignment.Left, -1, size, White);
        Tag(new Color(White, 0.35f), y + 60);
    }

    private void Airy()   // bigger, no tagline, tiny trailing accent dot
    {
        int size = 68;
        CenterText(_thin, Word, H * 0.5f, size, White);
        float bw = Measure(_thin, Word, size).X;
        DrawCircle(new Vector2((W + bw) * 0.5f + 16, H * 0.5f - size * 0.32f), 3.5f, Cyan);
    }

    private void Marker()   // filled accent square before the word + a plain subtitle
    {
        int size = 50;
        float ww = Measure(_thin, Word, size).X;
        float x = (W - ww) * 0.5f, y = H * 0.5f;
        DrawRect(new Rect2(x - 26, y - size * 0.28f, 12, 12), Cyan);
        DrawString(_thin, new Vector2(x, y), Word, HorizontalAlignment.Left, -1, size, White);
        CenterText(_reg, "F P V   S I M U L A T O R", y + 40, 15, new Color(White, 0.4f));
    }
}
