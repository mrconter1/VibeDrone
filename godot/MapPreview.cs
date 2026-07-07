using Godot;

// A small top-down schematic of a level for the Levels screen: the gate loop (start/finish marked)
// plus props, projected onto the XZ plane and scaled to fit. Drawn on the menu canvas, so it needs
// no 3D viewport.
public partial class MapPreview : Control
{
    private Vector2[] _gates = System.Array.Empty<Vector2>();
    private Vector2[] _props = System.Array.Empty<Vector2>();
    private Vector2 _min, _max;

    private static readonly Color Accent = new(0.24f, 0.80f, 0.96f);
    private static readonly Color Start = new(0.60f, 0.98f, 0.64f);

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public void SetLevel(Level lvl)
    {
        _gates = new Vector2[lvl.Gates.Count];
        for (int i = 0; i < lvl.Gates.Count; i++) _gates[i] = new Vector2(lvl.Gates[i].Pos.X, lvl.Gates[i].Pos.Z);
        _props = new Vector2[lvl.Props.Count];
        for (int i = 0; i < lvl.Props.Count; i++) _props[i] = new Vector2(lvl.Props[i].Pos.X, lvl.Props[i].Pos.Z);

        if (_gates.Length > 0)
        {
            _min = _max = _gates[0];
            foreach (Vector2 p in _gates) { _min = _min.Min(p); _max = _max.Max(p); }
            foreach (Vector2 p in _props) { _min = _min.Min(p); _max = _max.Max(p); }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(1, 1, 1, 0.03f));   // faint backdrop
        if (_gates.Length < 2) return;

        const float pad = 14f;
        Vector2 span = _max - _min;
        float s = Mathf.Min(
            span.X > 1e-3f ? (size.X - 2 * pad) / span.X : 1f,
            span.Y > 1e-3f ? (size.Y - 2 * pad) / span.Y : 1f);
        Vector2 off = (size - span * s) * 0.5f;
        Vector2 Map(Vector2 w) => off + (w - _min) * s;

        for (int i = 0; i < _gates.Length; i++)                              // the loop
            DrawLine(Map(_gates[i]), Map(_gates[(i + 1) % _gates.Length]), new Color(Accent, 0.5f), 2f);

        foreach (Vector2 p in _props)                                        // props
            DrawCircle(Map(p), 2.5f, new Color(0.62f, 0.62f, 0.64f, 0.7f));

        for (int i = 0; i < _gates.Length; i++)                             // gates (0 = start)
            DrawCircle(Map(_gates[i]), i == 0 ? 5f : 3.5f, i == 0 ? Start : Accent);
    }
}
