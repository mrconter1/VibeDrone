using Godot;

// Persisted user preferences (user://config.json): UI scale and chosen menu-blur mode.
// Loaded once at startup and saved whenever the player changes a setting.
public static class Config
{
    private const string Path = "user://config.json";

    public static float UiScale = 1.0f;   // whole-UI scale factor
    public static int BlurMode = 2;       // MenuBackdrop blur technique

    public static void Load()
    {
        if (!Persistence.TryLoad(Path, out Variant v) || v.VariantType != Variant.Type.Dictionary) return;
        var d = v.AsGodotDictionary();
        if (d.ContainsKey("ui_scale")) UiScale = Mathf.Clamp(d["ui_scale"].AsSingle(), 0.7f, 1.6f);
        if (d.ContainsKey("blur")) BlurMode = d["blur"].AsInt32();
    }

    public static void Save() =>
        Persistence.Save(Path, new Godot.Collections.Dictionary { { "ui_scale", UiScale }, { "blur", BlurMode } });
}
