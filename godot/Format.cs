// Shared display formatting, so lap/clock strings read the same everywhere (HUD, level list).
public static class Format
{
    // A lap/clock time as m:ss.00 (or ss.00 under a minute). With blankZero (the default) a
    // non-positive time renders as "--" (no time yet); the running race clock passes false so it
    // shows a live 0.00.
    public static string Time(float seconds, bool blankZero = true)
    {
        if (blankZero && seconds <= 0f) return "--";
        int m = (int)(seconds / 60f);
        float s = seconds - m * 60f;
        return m > 0 ? $"{m}:{s:00.00}" : $"{s:0.00}";
    }
}
