using Godot;

// Persisted user preferences (user://config.json): UI scale, menu-blur look, and anti-aliasing.
// Loaded once at startup and saved whenever the player changes a setting.
public static class Config
{
    private const string Path = "user://config.json";

    public static float UiScale = 1.0f;          // whole-UI scale factor

    // menu backdrop blur
    public static float BlurRadius = 4.5f;       // per-pass Gaussian radius (px)
    public static int BlurIterations = 3;        // how many H+V passes (wider + smoother)
    public static float BlurTint = 0.5f;         // darkening toward the tint colour
    public static float BlurVignette = 0.5f;

    // anti-aliasing (whole game)
    public static int Msaa = 4;                  // 0 / 2 / 4 / 8  (MSAA 3D samples)
    public static bool Fxaa = true;              // post AA, smooths shader aliasing (checker flag)

    public static void Load()
    {
        if (!Persistence.TryLoad(Path, out Variant v) || v.VariantType != Variant.Type.Dictionary) return;
        var d = v.AsGodotDictionary();
        if (d.ContainsKey("ui_scale"))    UiScale = Mathf.Clamp(d["ui_scale"].AsSingle(), 0.7f, 1.6f);
        if (d.ContainsKey("blur_radius")) BlurRadius = d["blur_radius"].AsSingle();
        if (d.ContainsKey("blur_iters"))  BlurIterations = Mathf.Clamp(d["blur_iters"].AsInt32(), 0, MaxIterations);
        if (d.ContainsKey("blur_tint"))   BlurTint = d["blur_tint"].AsSingle();
        if (d.ContainsKey("blur_vig"))    BlurVignette = d["blur_vig"].AsSingle();
        if (d.ContainsKey("msaa"))        Msaa = d["msaa"].AsInt32();
        if (d.ContainsKey("fxaa"))        Fxaa = d["fxaa"].AsBool();
    }

    public static void Save() => Persistence.Save(Path, new Godot.Collections.Dictionary
    {
        { "ui_scale", UiScale },
        { "blur_radius", BlurRadius }, { "blur_iters", BlurIterations },
        { "blur_tint", BlurTint }, { "blur_vig", BlurVignette },
        { "msaa", Msaa }, { "fxaa", Fxaa },
    });

    public const int MaxIterations = 5;   // backdrop builds this many H+V stages; some may be idle
}
