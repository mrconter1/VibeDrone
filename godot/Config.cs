using Godot;

// Persisted user preferences (user://config.json): UI scale, menu-blur look, and anti-aliasing.
// Loaded once at startup and saved whenever the player changes a setting.
public static class Config
{
    private const string Path = "user://config.json";

    public static float UiScale = 1.0f;          // whole-UI scale factor

    // menu backdrop blur (tuned defaults; no longer user-editable in-game)
    public static int BlurType = 1;              // 0 Gaussian, 1 Kawase (expanding), 2 Box
    public static float BlurRadius = 2.0f;       // per-pass radius (px)
    public static int BlurIterations = 5;        // how many H+V passes (wider + smoother)
    public static float BlurTint = 0.55f;        // darkening toward the tint colour
    public static float BlurVignette = 0.60f;

    // anti-aliasing (whole game) - exposed in Settings
    public static int Msaa = 8;                  // 0 / 2 / 4 / 8  (MSAA 3D samples)
    public static bool Fxaa = true;              // post AA, smooths shader aliasing (checker flag)
    public static bool MenuSsaa = true;          // supersample the 3D while a menu is open (kills orbit shimmer)

    public static void Load()
    {
        if (!Persistence.TryLoad(Path, out Variant v) || v.VariantType != Variant.Type.Dictionary) return;
        var d = v.AsGodotDictionary();
        if (d.ContainsKey("ui_scale"))    UiScale = Mathf.Clamp(d["ui_scale"].AsSingle(), 0.7f, 1.6f);
        if (d.ContainsKey("blur_type"))   BlurType = d["blur_type"].AsInt32();
        if (d.ContainsKey("blur_radius")) BlurRadius = d["blur_radius"].AsSingle();
        if (d.ContainsKey("blur_iters"))  BlurIterations = Mathf.Clamp(d["blur_iters"].AsInt32(), 0, MaxIterations);
        if (d.ContainsKey("blur_tint"))   BlurTint = d["blur_tint"].AsSingle();
        if (d.ContainsKey("blur_vig"))    BlurVignette = d["blur_vig"].AsSingle();
        if (d.ContainsKey("msaa"))        Msaa = d["msaa"].AsInt32();
        if (d.ContainsKey("fxaa"))        Fxaa = d["fxaa"].AsBool();
        if (d.ContainsKey("menu_ssaa"))   MenuSsaa = d["menu_ssaa"].AsBool();
    }

    public static void Save() => Persistence.Save(Path, new Godot.Collections.Dictionary
    {
        { "ui_scale", UiScale },
        { "blur_type", BlurType }, { "blur_radius", BlurRadius }, { "blur_iters", BlurIterations },
        { "blur_tint", BlurTint }, { "blur_vig", BlurVignette },
        { "msaa", Msaa }, { "fxaa", Fxaa }, { "menu_ssaa", MenuSsaa },
    });

    public const int MaxIterations = 5;   // backdrop builds this many H+V stages; some may be idle
}
