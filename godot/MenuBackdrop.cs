using Godot;

// A frosted-glass backdrop for the full-screen menus: a full-rect quad whose shader blurs the live
// rendered arena behind it, darkens it, and adds a vignette. Sits on a low CanvasLayer so the menu
// panels draw crisply on top. Only visible while a full-screen menu is active.
public partial class MenuBackdrop : CanvasLayer
{
    private ColorRect _rect = null!;

    public override void _Ready()
    {
        Layer = 8;   // above the 3D world, below the menu panels (11+)
        _rect = new ColorRect { Color = Colors.White };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _rect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _rect.Material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        AddChild(_rect);
        Visible = false;
    }

    public void SetActive(bool on) => Visible = on;

    private const string ShaderCode = @"
shader_type canvas_item;
// blur the frame rendered so far (the 3D arena), then darken + vignette for a frosted backdrop.
uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform float radius = 3.0;
uniform vec3 tint : source_color = vec3(0.03, 0.035, 0.045);
uniform float tint_amt = 0.58;
uniform float vignette = 0.55;
void fragment() {
    vec2 px = SCREEN_PIXEL_SIZE * radius;
    vec3 c = vec3(0.0);
    for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
            c += texture(screen, SCREEN_UV + vec2(float(x), float(y)) * px).rgb;
    c /= 9.0;
    c = mix(c, tint, tint_amt);
    float d = distance(SCREEN_UV, vec2(0.5));
    c *= 1.0 - vignette * smoothstep(0.25, 0.85, d);
    COLOR = vec4(c, 1.0);
}
";
}
