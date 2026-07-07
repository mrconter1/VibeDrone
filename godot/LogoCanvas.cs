using Godot;

// The three top-voted minimal "VibeDrone" logos, kept for the final round: Classic, Divider, Marker.
// Thin, tracked, mostly monochrome with a single restrained accent. Vector drawing + system fonts.
public partial class LogoCanvas : Control
{
    public static readonly string[] Names = { "Classic", "Divider", "Marker" };
    public const int Count = 3;

    public int Style;

    private static readonly Color White = new(0.95f, 0.96f, 0.98f);
    private static readonly Color Cyan = new(0.24f, 0.80f, 0.96f);

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
            case 1: Divider(); break;
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

    private void Classic()   // thin tracked word, single accent dot, dim tagline
    {
        CenterText(_thin, Word, H * 0.5f, 58, White);
        DrawCircle(new Vector2(W * 0.5f, H * 0.5f + 34), 4f, Cyan);
        Tag(new Color(White, 0.35f), H * 0.5f + 72);
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
