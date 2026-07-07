using Godot;

// A slim rounded progress pill (0..1). Track + fill drawn as StyleBoxFlat pills so the ends are smooth.
public partial class LoadingBar : Control
{
    public float Progress;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        int r = Mathf.CeilToInt(h * 0.5f);

        var track = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.10f), AntiAliasing = true };
        track.SetCornerRadiusAll(r);
        track.CornerDetail = 12;
        DrawStyleBox(track, new Rect2(0, 0, w, h));

        float fw = Mathf.Max(h, w * Mathf.Clamp(Progress, 0f, 1f));
        var fill = new StyleBoxFlat { BgColor = new Color(0.24f, 0.80f, 0.96f), AntiAliasing = true };
        fill.SetCornerRadiusAll(r);
        fill.CornerDetail = 12;
        DrawStyleBox(fill, new Rect2(0, 0, fw, h));
    }
}
