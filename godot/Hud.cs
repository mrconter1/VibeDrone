using Godot;

// FPV race HUD: centre lap clock + last/best, a best-laps board (top right), speed/alt,
// throttle, crosshair and artificial horizon. Debug readouts (FPS/FOV/etc) are hidden
// unless ShowDebug is set from the Esc menu.
public partial class Hud : Control
{
    public float Speed, Alt, Throttle, RollDeg, PitchDeg, TimeSec, Fov, Fps;
    public string Mode = "LIVE", Sound = "OFF";
    public bool ShowDebug;

    public float LapTime, LastLap, BestLap;
    public string RaceStatus = "", Ranks = "", LevelName = "";

    private Font _font = null!;

    public override void _Ready()
    {
        _font = ThemeDB.FallbackFont;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        Vector2 sz = GetViewportRect().Size;
        Vector2 c = sz / 2f;
        float cx = sz.X * 0.5f;
        var hud = new Color(0.62f, 0.98f, 0.76f);
        var faded = new Color(hud.R, hud.G, hud.B, 0.35f);
        var dim = new Color(1f, 1f, 1f, 0.5f);
        var gold = new Color(1f, 0.85f, 0.3f);

        // artificial horizon
        float roll = Mathf.DegToRad(RollDeg);
        Vector2 d = new Vector2(Mathf.Cos(roll), Mathf.Sin(roll));
        Vector2 mid = c + new Vector2(0, PitchDeg * 7f);
        DrawLine(mid - d * 260, mid - d * 40, faded, 2);
        DrawLine(mid + d * 40, mid + d * 260, faded, 2);

        // crosshair
        DrawLine(c + new Vector2(-16, 0), c + new Vector2(-5, 0), hud, 2);
        DrawLine(c + new Vector2(5, 0), c + new Vector2(16, 0), hud, 2);
        DrawLine(c + new Vector2(0, -5), c + new Vector2(0, 5), hud, 2);

        if (_font == null) return;

        // lap clock (top centre)
        DrawRect(new Rect2(cx - 130, 14, 260, 62), new Color(0f, 0f, 0f, 0.45f));
        DrawString(_font, new Vector2(cx - 130, 60), FmtTime(LapTime), HorizontalAlignment.Center, 260, 46, Colors.White);
        if (RaceStatus.Length > 0)
            DrawString(_font, new Vector2(cx - 130, 74), RaceStatus, HorizontalAlignment.Center, 260, 14, dim);
        if (LastLap > 0f)
            DrawString(_font, new Vector2(cx - 132, 98), $"LAST {FmtTime(LastLap)}", HorizontalAlignment.Center, 132, 17, dim);
        if (BestLap > 0f)
            DrawString(_font, new Vector2(cx, 98), $"BEST {FmtTime(BestLap)}", HorizontalAlignment.Center, 132, 17, gold);

        // track name + best-laps board (top right)
        if (LevelName.Length > 0)
            DrawString(_font, new Vector2(sz.X - 196, 24), LevelName, HorizontalAlignment.Left, -1, 15, gold);
        if (Ranks.Length > 0)
        {
            DrawString(_font, new Vector2(sz.X - 196, 44), "BEST LAPS", HorizontalAlignment.Left, -1, 17, hud);
            DrawMultilineString(_font, new Vector2(sz.X - 196, 68), Ranks, HorizontalAlignment.Left, -1, 18, -1, Colors.White);
        }

        // speed + altitude (bottom)
        Text(40, sz.Y - 88, $"{Speed,4:0}", 54, hud);
        Text(46, sz.Y - 94, "m/s", 17, dim);
        Text(sz.X - 200, sz.Y - 88, $"{Alt,4:0}", 54, hud);
        Text(sz.X - 200, sz.Y - 94, "alt m", 17, dim);

        // throttle bar (right)
        float bh = 240, bx = sz.X - 52, by = c.Y - bh / 2;
        DrawRect(new Rect2(bx, by, 16, bh), new Color(0f, 0f, 0f, 0.35f));
        DrawRect(new Rect2(bx, by + bh * (1 - Throttle), 16, bh * Throttle), new Color(0.3f, 0.8f, 1f, 0.9f));

        // minimal hint
        Text(40, 42, "Esc menu     H help", 15, dim);

        // debug overlay (opt-in via Esc menu)
        if (ShowDebug)
            Text(40, sz.Y - 40, $"[{Mode}]  {Fps:0} FPS   FOV {Fov:0}   SND {Sound}   t {TimeSec:0.0}s", 15, dim);
    }

    private static string FmtTime(float t)
    {
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return m > 0 ? $"{m}:{s:00.00}" : $"{s:0.00}";
    }

    private void Text(float x, float y, string s, int size, Color col)
    {
        if (_font != null)
            DrawString(_font, new Vector2(x, y), s, HorizontalAlignment.Left, -1, size, col);
    }
}
