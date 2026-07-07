using Godot;

// Frosted backdrop for the full-screen menus. Uses a proper multi-pass separable Gaussian: copy the
// live arena, blur horizontally, copy that, blur vertically (and tint + vignette). Two 13-tap passes
// give a genuinely smooth blur instead of the blocky single-pass look. B on the title screen cycles
// the strength so you can pick one. Sits below the menu panels (Layer 8).
public partial class MenuBackdrop : CanvasLayer
{
    private static readonly string[] Names = { "None", "Light", "Medium", "Strong", "Very strong" };
    private static readonly float[] Radius = { 0f, 2.5f, 4.5f, 7f, 10f };

    private ShaderMaterial _matH = null!, _matV = null!;
    private int _mode;

    public string ModeName => $"{_mode} · {Names[_mode]}";

    public override void _Ready()
    {
        Layer = 8;

        AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });  // capture the arena
        _matH = new ShaderMaterial { Shader = new Shader { Code = BlurShader(false) } };
        AddChild(FullRect(_matH));                                                          // horizontal pass
        AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });  // capture that
        _matV = new ShaderMaterial { Shader = new Shader { Code = BlurShader(true) } };
        AddChild(FullRect(_matV));                                                          // vertical pass + tint

        Visible = false;
        SetMode(Config.BlurMode);
    }

    public void SetActive(bool on) => Visible = on;

    public void SetMode(int mode)
    {
        _mode = ((mode % Names.Length) + Names.Length) % Names.Length;
        _matH.SetShaderParameter("radius", Radius[_mode]);
        _matV.SetShaderParameter("radius", Radius[_mode]);
    }

    // Advance to the next strength, persist it, and return its display name.
    public string Cycle()
    {
        SetMode(_mode + 1);
        Config.BlurMode = _mode;
        Config.Save();
        return ModeName;
    }

    private static ColorRect FullRect(ShaderMaterial mat)
    {
        var r = new ColorRect { Color = Colors.White, Material = mat };
        r.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        r.MouseFilter = Control.MouseFilterEnum.Ignore;
        return r;
    }

    // One axis of a separable 13-tap Gaussian. The vertical pass also applies the tint + vignette.
    private static string BlurShader(bool vertical)
    {
        string dir = vertical ? "vec2(0.0, 1.0)" : "vec2(1.0, 0.0)";
        string finish = vertical
            ? @"c = mix(c, tint, tint_amt);
    float dv = distance(SCREEN_UV, vec2(0.5));
    c *= 1.0 - vignette * smoothstep(0.25, 0.9, dv);"
            : "";
        return $@"
shader_type canvas_item;
uniform sampler2D screen : hint_screen_texture, filter_linear;
uniform float radius = 4.5;
uniform vec3 tint : source_color = vec3(0.03, 0.035, 0.045);
uniform float tint_amt = 0.5;
uniform float vignette = 0.5;
void fragment() {{
    if (radius <= 0.001) {{
        vec3 c = texture(screen, SCREEN_UV).rgb;
        {finish}
        COLOR = vec4(c, 1.0);
    }} else {{
        vec2 stride = SCREEN_PIXEL_SIZE * (radius / 3.0) * {dir};
        vec3 c = vec3(0.0);
        float s = 0.0;
        for (int i = -6; i <= 6; i++) {{
            float w = exp(-float(i * i) / 18.0);   // Gaussian, sigma = 3 taps
            c += texture(screen, SCREEN_UV + stride * float(i)).rgb * w;
            s += w;
        }}
        c /= s;
        {finish}
        COLOR = vec4(c, 1.0);
    }}
}}";
    }
}
