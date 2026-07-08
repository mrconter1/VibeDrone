using Godot;

// Persisted user preferences (user://config.json): UI scale, menu-blur look, and anti-aliasing.
// Loaded once at startup and saved whenever the player changes a setting.
public static class Config
{
    private const string Path = "user://config.json";

    public static float UiScale = 1.10f;         // whole-UI scale factor

    // menu backdrop blur (tuned defaults; no longer user-editable in-game)
    public static int BlurType = 1;              // 0 Gaussian, 1 Kawase (expanding), 2 Box
    public static float BlurRadius = 2.0f;       // per-pass radius (px)
    public static int BlurIterations = 5;        // how many H+V passes (wider + smoother)
    public static float BlurTint = 0.55f;        // darkening toward the tint colour
    public static float BlurVignette = 0.60f;

    // anti-aliasing (whole game) - exposed in Settings
    public static int Msaa = 4;                  // 0 / 2 / 4 / 8  (MSAA 3D samples)
    public static bool Fxaa = false;             // post AA, smooths shader aliasing (checker flag)
    public static bool MenuSsaa = false;         // supersample the 3D while a menu is open (kills orbit shimmer)

    // race mode - auto-reset when the pilot stops flying (Settings > Race Mode)
    public static bool AutoReset = true;         // reset to the start line after a spell of no input
    public static float AutoResetSeconds = 2.0f; // how long with no input before the reset fires

    // drone - FPV camera uptilt in degrees (Settings > Drone)
    public static float CameraTilt = 30f;

    // last session - so "Start" resumes the last track + mode (+ free-fly pose)
    public static string LastLevelId = "";
    public static int LastMode = 0;                  // 0 Race, 1 Free Fly
    public static bool HasFreePose = false;          // a saved free-fly pose exists
    public static string FreePoseLevel = "";         // which level that pose belongs to
    public static float[] FreePose = new float[7];   // model frame: px py pz  qx qy qz qw

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
        if (d.ContainsKey("auto_reset"))  AutoReset = d["auto_reset"].AsBool();
        if (d.ContainsKey("auto_reset_s")) AutoResetSeconds = Mathf.Clamp(d["auto_reset_s"].AsSingle(), 0.5f, 10f);
        if (d.ContainsKey("cam_tilt"))    CameraTilt = Mathf.Clamp(d["cam_tilt"].AsSingle(), 0f, 60f);
        if (d.ContainsKey("last_level"))  LastLevelId = d["last_level"].AsString();
        if (d.ContainsKey("last_mode"))   LastMode = d["last_mode"].AsInt32();
        if (d.ContainsKey("has_free_pose")) HasFreePose = d["has_free_pose"].AsBool();
        if (d.ContainsKey("free_pose_level")) FreePoseLevel = d["free_pose_level"].AsString();
        if (d.ContainsKey("free_pose"))
        {
            var a = d["free_pose"].AsGodotArray();
            if (a.Count == 7) for (int i = 0; i < 7; i++) FreePose[i] = a[i].AsSingle();
        }
    }

    public static void Save() => Persistence.Save(Path, new Godot.Collections.Dictionary
    {
        { "ui_scale", UiScale },
        { "blur_type", BlurType }, { "blur_radius", BlurRadius }, { "blur_iters", BlurIterations },
        { "blur_tint", BlurTint }, { "blur_vig", BlurVignette },
        { "msaa", Msaa }, { "fxaa", Fxaa }, { "menu_ssaa", MenuSsaa },
        { "auto_reset", AutoReset }, { "auto_reset_s", AutoResetSeconds }, { "cam_tilt", CameraTilt },
        { "last_level", LastLevelId }, { "last_mode", LastMode },
        { "has_free_pose", HasFreePose }, { "free_pose_level", FreePoseLevel },
        { "free_pose", new Godot.Collections.Array {
            FreePose[0], FreePose[1], FreePose[2], FreePose[3], FreePose[4], FreePose[5], FreePose[6] } },
    });

    public const int MaxIterations = 5;   // backdrop builds this many H+V stages; some may be idle
}
