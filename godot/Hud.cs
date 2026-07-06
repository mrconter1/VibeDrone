using Godot;

// FPV OSD overlay: artificial horizon, crosshair, speed/alt/throttle, timer.
// DroneController updates the public fields each frame.
public partial class Hud : Control
{
    public float Speed, Alt, Throttle, RollDeg, PitchDeg, TimeSec, Fov, Fps;
    public string Mode = "LIVE";
    public string Sound = "OFF";
    public string Joypad = "";

    private Font _font = null!;   // set in _Ready

    public override void _Ready()
    {
        _font = ThemeDB.FallbackFont;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    // Redraw ~30 Hz, not every frame: the HUD rebuilds several strings each _Draw,
    // and doing that 60x/s adds managed-GC pressure that can cost a vsync frame.
    private double _redrawAccum;
    public override void _Process(double delta)
    {
        _redrawAccum += delta;
        if (_redrawAccum >= 1.0 / 30.0) { _redrawAccum = 0; QueueRedraw(); }
    }

    public override void _Draw()
    {
        Vector2 c = Size / 2f;
        var hudCol = new Color(0.55f, 0.95f, 0.70f);
        var dim = new Color(0.55f, 0.95f, 0.70f, 0.45f);

        // artificial horizon (rolls + pitches like the drone)
        float roll = Mathf.DegToRad(RollDeg);
        Vector2 dir = new Vector2(Mathf.Cos(roll), Mathf.Sin(roll));
        Vector2 mid = c + new Vector2(0, PitchDeg * 7f);
        DrawLine(mid - dir * 260, mid - dir * 40, dim, 2);
        DrawLine(mid + dir * 40, mid + dir * 260, dim, 2);

        // crosshair
        DrawLine(c + new Vector2(-16, 0), c + new Vector2(-5, 0), hudCol, 2);
        DrawLine(c + new Vector2(5, 0), c + new Vector2(16, 0), hudCol, 2);
        DrawLine(c + new Vector2(0, -5), c + new Vector2(0, 5), hudCol, 2);

        // throttle bar (right)
        float bh = 260, bx = Size.X - 60, by = c.Y - bh / 2;
        DrawRect(new Rect2(bx, by, 20, bh), new Color(0, 0, 0, 0.35f));
        DrawRect(new Rect2(bx, by + bh * (1 - Throttle), 20, bh * Throttle), new Color(0.3f, 0.8f, 1f, 0.9f));
        Text(bx - 4, by + bh + 26, $"THR {Throttle * 100,3:0}%", 18, hudCol);

        // speed + alt (left, big)
        Text(40, Size.Y - 90, $"{Speed,4:0}", 56, hudCol);
        Text(46, Size.Y - 96, "m/s", 18, dim);
        Text(Size.X - 230, Size.Y - 90, $"{Alt,5:0}", 56, hudCol);
        Text(Size.X - 232, Size.Y - 96, "alt m", 18, dim);

        // top bar
        Text(40, 50, $"[{Mode}]   {TimeSec,5:0.0}s   FOV {Fov:0}   {Fps:0} FPS   SND {Sound}", 22, hudCol);
        Text(40, 78, "Esc quit   R reset   Tab replay   S sound   E edit-fly", 16, dim);
    }

    private void Text(float x, float y, string s, int size, Color col)
    {
        if (_font != null)
            DrawString(_font, new Vector2(x, y), s, HorizontalAlignment.Left, -1, size, col);
    }
}
