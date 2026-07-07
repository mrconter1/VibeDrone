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
// Frosted backdrop: a smooth 5x5 Gaussian blur of the live arena (full-res linear sampling, so it
// stays soft rather than blocky), then a dark tint + vignette. Runs only while a menu is open.
uniform sampler2D screen : hint_screen_texture, filter_linear;
uniform float radius = 2.2;
uniform vec3 tint : source_color = vec3(0.03, 0.035, 0.045);
uniform float tint_amt = 0.5;
uniform float vignette = 0.5;
void fragment() {
    vec2 ps = SCREEN_PIXEL_SIZE * radius;
    float w[5] = float[](1.0, 4.0, 6.0, 4.0, 1.0);   // Gaussian weights
    vec3 c = vec3(0.0);
    float sum = 0.0;
    for (int x = 0; x < 5; x++)
        for (int y = 0; y < 5; y++) {
            float ww = w[x] * w[y];
            c += texture(screen, SCREEN_UV + vec2(float(x - 2), float(y - 2)) * ps).rgb * ww;
            sum += ww;
        }
    c /= sum;
    c = mix(c, tint, tint_amt);
    float d = distance(SCREEN_UV, vec2(0.5));
    c *= 1.0 - vignette * smoothstep(0.25, 0.9, d);
    COLOR = vec4(c, 1.0);
}
";
}
