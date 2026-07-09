using Godot;

// A small hand-drawn vector glyph (no font/asset files), used by the menu rows. Set Glyph + Color and
// it draws a minimal line icon centred in its box. Kept geometric so it matches the flat UI style.
public partial class UiIcon : Control
{
    public string Glyph = "";
    public Color Color = UiTheme.Text;

    public UiIcon() { }
    public UiIcon(string glyph, Color color) { Glyph = glyph; Color = color; }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        float s = Mathf.Min(Size.X, Size.Y);
        var o = new Vector2((Size.X - s) * 0.5f, (Size.Y - s) * 0.5f);   // top-left of the square box
        float w = Mathf.Max(1.6f, s * 0.09f);   // stroke width
        Color c = Color;

        // helpers in local box coords (0..1 -> pixels)
        Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
        void Line(float x1, float y1, float x2, float y2) => DrawLine(P(x1, y1), P(x2, y2), c, w, false);

        switch (Glyph)
        {
            case "play":
                DrawColoredPolygon(new[] { P(0.30f, 0.20f), P(0.30f, 0.80f), P(0.80f, 0.50f) }, c);
                break;

            case "restart":   // circular arrow, gap + head at top-right
                DrawArc(P(0.5f, 0.5f), s * 0.30f, Mathf.DegToRad(-40), Mathf.DegToRad(250), 24, c, w, false);
                DrawColoredPolygon(new[] { P(0.72f, 0.16f), P(0.86f, 0.30f), P(0.66f, 0.34f) }, c);
                break;

            case "mode":      // swap: two opposing arrows
                Line(0.24f, 0.36f, 0.74f, 0.36f); DrawColoredPolygon(new[] { P(0.74f, 0.28f), P(0.86f, 0.36f), P(0.74f, 0.44f) }, c);
                Line(0.26f, 0.64f, 0.76f, 0.64f); DrawColoredPolygon(new[] { P(0.26f, 0.56f), P(0.14f, 0.64f), P(0.26f, 0.72f) }, c);
                break;

            case "levels":    // pennant flag on a pole (a track)
                Line(0.30f, 0.16f, 0.30f, 0.84f);
                DrawColoredPolygon(new[] { P(0.30f, 0.18f), P(0.78f, 0.30f), P(0.30f, 0.46f) }, c);
                break;

            case "watch":     // eye
                DrawArc(P(0.5f, 0.62f), s * 0.34f, Mathf.DegToRad(200), Mathf.DegToRad(340), 20, c, w, false);
                DrawArc(P(0.5f, 0.38f), s * 0.34f, Mathf.DegToRad(20), Mathf.DegToRad(160), 20, c, w, false);
                DrawCircle(P(0.5f, 0.5f), s * 0.11f, c);
                break;

            case "settings":  // gear: ring + radial teeth + hub
                DrawArc(P(0.5f, 0.5f), s * 0.24f, 0, Mathf.Tau, 28, c, w, false);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.Tau / 8f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    DrawLine(P(0.5f, 0.5f) + d * (s * 0.24f), P(0.5f, 0.5f) + d * (s * 0.36f), c, w, false);
                }
                DrawCircle(P(0.5f, 0.5f), s * 0.07f, c);
                break;

            case "controls":  // gamepad
                DrawArc(P(0.5f, 0.52f), s * 0.30f, Mathf.DegToRad(20), Mathf.DegToRad(160), 20, c, w, false);
                Line(0.24f, 0.44f, 0.30f, 0.66f); Line(0.76f, 0.44f, 0.70f, 0.66f); Line(0.30f, 0.66f, 0.70f, 0.66f);
                Line(0.34f, 0.52f, 0.44f, 0.52f); Line(0.39f, 0.47f, 0.39f, 0.57f);   // d-pad
                DrawCircle(P(0.62f, 0.52f), s * 0.045f, c);                            // button
                break;

            case "home":      // house
                DrawColoredPolygon(new[] { P(0.5f, 0.18f), P(0.84f, 0.48f), P(0.16f, 0.48f) }, c);
                Line(0.26f, 0.46f, 0.26f, 0.82f); Line(0.74f, 0.46f, 0.74f, 0.82f); Line(0.26f, 0.82f, 0.74f, 0.82f);
                break;

            case "exit":      // power
                Line(0.5f, 0.16f, 0.5f, 0.46f);
                DrawArc(P(0.5f, 0.54f), s * 0.30f, Mathf.DegToRad(-60), Mathf.DegToRad(240), 24, c, w, false);
                break;

            case "create":    // plus
                Line(0.5f, 0.24f, 0.5f, 0.76f); Line(0.24f, 0.5f, 0.76f, 0.5f);
                break;
        }
    }
}
