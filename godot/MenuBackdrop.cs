using Godot;

// A frosted-glass backdrop for the full-screen menus: a full-rect quad whose shader blurs the live
// rendered arena, darkens it, and adds a vignette. Sits on a low CanvasLayer so the menu panels draw
// crisply on top. Offers several selectable blur techniques (cycled with B on the main menu).
public partial class MenuBackdrop : CanvasLayer
{
    // technique index -> display name (must match the branches in the shader)
    private static readonly string[] Names =
    {
        "None", "Box (3x3)", "Gaussian (5x5)", "Kawase (8-tap)", "Disc bokeh (13-tap)", "Heavy Gaussian",
    };

    private ColorRect _rect = null!;
    private int _mode;

    public string ModeName => $"{_mode} · {Names[_mode]}";

    public override void _Ready()
    {
        Layer = 8;   // above the 3D world, below the menu panels (11+)
        _rect = new ColorRect { Color = Colors.White };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _rect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _rect.Material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        AddChild(_rect);
        Visible = false;
        SetMode(Config.BlurMode);
    }

    public void SetActive(bool on) => Visible = on;

    public void SetMode(int mode)
    {
        _mode = ((mode % Names.Length) + Names.Length) % Names.Length;
        ((ShaderMaterial)_rect.Material).SetShaderParameter("mode", _mode);
    }

    // Advance to the next technique, persist it, and return its display name.
    public string Cycle()
    {
        SetMode(_mode + 1);
        Config.BlurMode = _mode;
        Config.Save();
        return ModeName;
    }

    private const string ShaderCode = @"
shader_type canvas_item;
// Blur the live arena behind the menus, then darken + vignette. `mode` selects the technique so it
// can be compared at runtime. Full-res linear sampling keeps every mode soft rather than blocky.
uniform sampler2D screen : hint_screen_texture, filter_linear;
uniform int mode = 2;
uniform float radius = 2.2;
uniform vec3 tint : source_color = vec3(0.03, 0.035, 0.045);
uniform float tint_amt = 0.5;
uniform float vignette = 0.5;

vec3 tap(vec2 uv) { return texture(screen, uv).rgb; }

vec3 gaussian5(vec2 uv, vec2 ps) {
    float w[5] = float[](1.0, 4.0, 6.0, 4.0, 1.0);
    vec3 c = vec3(0.0); float s = 0.0;
    for (int x = 0; x < 5; x++)
        for (int y = 0; y < 5; y++) {
            float ww = w[x] * w[y];
            c += tap(uv + vec2(float(x - 2), float(y - 2)) * ps) * ww; s += ww;
        }
    return c / s;
}

vec3 box3(vec2 uv, vec2 ps) {
    vec3 c = vec3(0.0);
    for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++) c += tap(uv + vec2(float(x), float(y)) * ps);
    return c / 9.0;
}

// Kawase-style: 8 diagonal taps on two rings (a cheap Gaussian approximation).
vec3 kawase(vec2 uv, vec2 ps) {
    vec3 c = tap(uv);
    for (float r = 1.0; r <= 2.0; r += 1.0) {
        vec2 o = ps * r;
        c += tap(uv + vec2( o.x,  o.y));
        c += tap(uv + vec2(-o.x,  o.y));
        c += tap(uv + vec2( o.x, -o.y));
        c += tap(uv + vec2(-o.x, -o.y));
    }
    return c / 9.0;
}

// Disc/bokeh: centre + 12 points on two rings (rounder out-of-focus look).
vec3 disc13(vec2 uv, vec2 ps) {
    vec3 c = tap(uv);
    for (int i = 0; i < 6; i++) {
        float a = 6.2831853 * float(i) / 6.0;
        vec2 d = vec2(cos(a), sin(a));
        c += tap(uv + d * ps * 1.5);
        c += tap(uv + d * ps * 3.0);
    }
    return c / 13.0;
}

void fragment() {
    vec2 ps = SCREEN_PIXEL_SIZE * radius;
    vec3 c;
    if (mode == 0)      c = tap(SCREEN_UV);
    else if (mode == 1) c = box3(SCREEN_UV, ps);
    else if (mode == 2) c = gaussian5(SCREEN_UV, ps);
    else if (mode == 3) c = kawase(SCREEN_UV, ps);
    else if (mode == 4) c = disc13(SCREEN_UV, ps);
    else                c = gaussian5(SCREEN_UV, ps * 2.2);   // heavy
    c = mix(c, tint, tint_amt);
    float d = distance(SCREEN_UV, vec2(0.5));
    c *= 1.0 - vignette * smoothstep(0.25, 0.9, d);
    COLOR = vec4(c, 1.0);
}
";
}
