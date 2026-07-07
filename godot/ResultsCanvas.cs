using Godot;

// The voting results: logos ranked by the two players' average score, drawn as medal + name + a
// score bar. Pure vector drawing. LogoMenu feeds it the sorted rows.
public partial class ResultsCanvas : Control
{
    public readonly record struct Row(string Name, float Avg, int A, int B);

    private Row[] _rows = System.Array.Empty<Row>();

    private static readonly Color White = new(0.95f, 0.96f, 0.98f);
    private static readonly Color Cyan = new(0.24f, 0.80f, 0.96f);
    private static readonly Color Gold = new(0.96f, 0.82f, 0.40f);
    private static readonly Color Silver = new(0.80f, 0.84f, 0.88f);
    private static readonly Color Bronze = new(0.82f, 0.56f, 0.34f);
    private static readonly Color Track = new(1f, 1f, 1f, 0.10f);

    private SystemFont _cond = null!, _reg = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _cond = new SystemFont { FontNames = new[] { "Bahnschrift", "Segoe UI Semibold", "Arial" } };
        _reg = new SystemFont { FontNames = new[] { "Segoe UI", "Arial" } };
    }

    public void SetData(Row[] rows) { _rows = rows; QueueRedraw(); }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        // title
        string title = "RESULTS";
        float tw = _cond.GetStringSize(title, HorizontalAlignment.Left, -1, 40).X;
        DrawString(_cond, new Vector2((w - tw) * 0.5f, 46), title, HorizontalAlignment.Left, -1, 40, White);

        if (_rows.Length == 0) return;
        const float top = 84f, bottom = 20f;
        float rowH = (h - top - bottom) / _rows.Length;

        for (int i = 0; i < _rows.Length; i++)
        {
            Row r = _rows[i];
            float y = top + i * rowH + rowH * 0.5f;

            // medal + rank
            Color medal = i == 0 ? Gold : i == 1 ? Silver : i == 2 ? Bronze : new Color(1, 1, 1, 0.18f);
            DrawCircle(new Vector2(38, y), 15f, medal);
            string rank = (i + 1).ToString();
            float rw = _reg.GetStringSize(rank, HorizontalAlignment.Left, -1, 18).X;
            DrawString(_reg, new Vector2(38 - rw * 0.5f, y + 6), rank, HorizontalAlignment.Left, -1, 18, new Color(0.05f, 0.06f, 0.08f));

            // name + per-player scores
            DrawString(_cond, new Vector2(70, y + 7), r.Name, HorizontalAlignment.Left, -1, 22, White);
            DrawString(_reg, new Vector2(238, y + 6), $"P1 {r.A}   P2 {r.B}", HorizontalAlignment.Left, -1, 15, new Color(White, 0.5f));

            // score bar (0..5)
            float bx = 350, bw = w - bx - 74;
            DrawRect(new Rect2(bx, y - 9, bw, 18), Track);
            float frac = Mathf.Clamp(r.Avg / 5f, 0f, 1f);
            Color fill = i == 0 ? Gold : Cyan;
            DrawRect(new Rect2(bx, y - 9, bw * frac, 18), fill);

            // average value
            DrawString(_reg, new Vector2(w - 60, y + 7), r.Avg.ToString("0.0"), HorizontalAlignment.Left, -1, 20, White);
        }
    }
}
