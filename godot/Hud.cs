using Godot;

// FPV race HUD: centre lap clock + last/best, a best-laps board (top right), speed/alt,
// throttle, crosshair and artificial horizon. Debug readouts (FPS/FOV/etc) are hidden
// unless ShowDebug is set from the Esc menu.
public partial class Hud : Control
{
    public float Speed, Alt, Throttle, RollDeg, PitchDeg, TimeSec, Fov, Fps;
    public string Mode = "LIVE", Sound = "OFF";
    public bool ShowDebug, DevReload, FreeFly;

    public float LapTime, LastLap, BestLap;
    public string RaceStatus = "", Ranks = "", LevelName = "";

    private Font _font = null!;
    private StyleBoxFlat _timerBox = null!, _pillBox = null!, _barBox = null!;

    public override void _Ready()
    {
        _font = ThemeDB.FallbackFont;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);

        // cached rounded panels (avoid per-frame allocation in _Draw)
        _timerBox = RoundBox(new Color(0.04f, 0.05f, 0.07f, 0.58f), 14, new Color(1, 1, 1, 0.08f));
        _pillBox  = RoundBox(new Color(0.04f, 0.05f, 0.07f, 0.55f), 13, new Color(0.24f, 0.80f, 0.96f, 0.35f));
        _barBox   = RoundBox(new Color(0f, 0f, 0f, 0.35f), 6, null);
    }

    private static StyleBoxFlat RoundBox(Color bg, int radius, Color? border)
    {
        var s = new StyleBoxFlat { BgColor = bg, CornerDetail = 6 };
        s.SetCornerRadiusAll(radius);
        if (border is Color b) { s.BorderColor = b; s.SetBorderWidthAll(1); }
        return s;
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

        var accent = new Color(0.24f, 0.80f, 0.96f);

        // free fly: the whole race clock is hidden - just a small rounded mode pill, top centre
        if (FreeFly)
        {
            const float pw = 120, ph = 30, py = 18;
            DrawStyleBox(_pillBox, new Rect2(cx - pw / 2f, py, pw, ph));
            DrawString(_font, new Vector2(cx - pw / 2f, py + 20), "FREE FLY", HorizontalAlignment.Center, pw, 15, accent);
        }
        else
        {
            // race lap clock: a rounded panel with the running time, an optional last/best row, and a
            // status caption below. Panel grows to include last/best only once a lap has been set.
            bool hasLaps = BestLap > 0f;
            float pw = 236f, py = 16f, ph = hasLaps ? 92f : 60f, px = cx - pw / 2f;
            DrawStyleBox(_timerBox, new Rect2(px, py, pw, ph));

            DrawString(_font, new Vector2(px, py + 46), Format.Time(LapTime, blankZero: false), HorizontalAlignment.Center, pw, 40, Colors.White);

            if (hasLaps)
            {
                DrawRect(new Rect2(px + 20, py + 58, pw - 40, 1), new Color(1, 1, 1, 0.08f));   // divider
                DrawString(_font, new Vector2(px + 20, py + 82), $"LAST  {Format.Time(LastLap, blankZero: false)}", HorizontalAlignment.Left, pw / 2f - 24, 16, dim);
                DrawString(_font, new Vector2(cx, py + 82), $"BEST  {Format.Time(BestLap, blankZero: false)}", HorizontalAlignment.Right, pw / 2f - 20, 16, gold);
            }

            if (RaceStatus.Length > 0)
            {
                Color sc = RaceStatus.StartsWith("GO") ? hud : dim;
                DrawString(_font, new Vector2(px, py + ph + 16), RaceStatus, HorizontalAlignment.Center, pw, 14, sc);
            }
        }

        // track name + best-laps board (top right)
        if (LevelName.Length > 0)
            DrawString(_font, new Vector2(sz.X - 196, 24), LevelName, HorizontalAlignment.Left, -1, 15, gold);
        if (!FreeFly && Ranks.Length > 0)
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
        DrawStyleBox(_barBox, new Rect2(bx, by, 16, bh));
        DrawRect(new Rect2(bx + 2, by + 2 + (bh - 4) * (1 - Throttle), 12, (bh - 4) * Throttle), new Color(0.3f, 0.8f, 1f, 0.9f));

        // minimal hint
        Text(40, 42, "Esc menu     H help", 15, dim);

        // debug overlay (opt-in via Esc menu)
        if (ShowDebug)
        {
            string s = $"[{Mode}]  {Fps:0} FPS   FOV {Fov:0}   SND {Sound}   t {TimeSec:0.0}s";
            if (DevReload) s += "   R rebuild+relaunch";
            Text(40, sz.Y - 40, s, 15, dim);
        }
    }

    private void Text(float x, float y, string s, int size, Color col)
    {
        if (_font != null)
            DrawString(_font, new Vector2(x, y), s, HorizontalAlignment.Left, -1, size, col);
    }
}
