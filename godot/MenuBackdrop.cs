using System.Collections.Generic;
using Godot;

// Frosted backdrop for the full-screen menus. A multi-pass separable Gaussian: each iteration blurs
// horizontally then vertically (13 taps each, BackBufferCopy between passes); iterating several times
// widens and smooths the blur far beyond a single pass. A final pass applies tint + vignette. All
// parameters come from Config and can be retuned live from the blur menu.
public partial class MenuBackdrop : CanvasLayer
{
    private readonly List<ColorRect> _blurRects = new();       // 2 per iteration (H, V)
    private readonly List<ShaderMaterial> _blurMats = new();
    private ShaderMaterial _finalMat = null!;

    public override void _Ready()
    {
        Layer = 8;

        for (int k = 0; k < Config.MaxIterations; k++)
        {
            AddStage(false);   // horizontal
            AddStage(true);    // vertical
        }

        AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
        _finalMat = new ShaderMaterial { Shader = new Shader { Code = FinalShader } };
        AddChild(FullRect(_finalMat));

        Visible = false;
        Refresh();
    }

    public void SetActive(bool on) => Visible = on;

    // Reconfigure from Config (radius/iterations/tint/vignette) - called on load and live edits.
    public void Refresh()
    {
        for (int i = 0; i < _blurRects.Count; i++)
        {
            int iteration = i / 2;                       // two rects (H,V) per iteration
            _blurRects[i].Visible = iteration < Config.BlurIterations;
            // Kawase (type 1): grow the radius each iteration for a wider, dual-Kawase-like spread.
            float r = Config.BlurRadius * (Config.BlurType == 1 ? iteration + 1 : 1);
            _blurMats[i].SetShaderParameter("radius", r);
            _blurMats[i].SetShaderParameter("kind", Config.BlurType == 2 ? 1 : 0);   // 1 = box weights
        }
        _finalMat.SetShaderParameter("tint_amt", Config.BlurTint);
        _finalMat.SetShaderParameter("vignette", Config.BlurVignette);
    }

    private void AddStage(bool vertical)
    {
        AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
        var mat = new ShaderMaterial { Shader = new Shader { Code = BlurShader(vertical) } };
        var rect = FullRect(mat);
        AddChild(rect);
        _blurRects.Add(rect);
        _blurMats.Add(mat);
    }

    private static ColorRect FullRect(ShaderMaterial mat)
    {
        var r = new ColorRect { Color = Colors.White, Material = mat };
        r.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        r.MouseFilter = Control.MouseFilterEnum.Ignore;
        return r;
    }

    // One axis of a separable 13-tap Gaussian (pure blur; tint/vignette happen in the final pass).
    private static string BlurShader(bool vertical)
    {
        string dir = vertical ? "vec2(0.0, 1.0)" : "vec2(1.0, 0.0)";
        return $@"
shader_type canvas_item;
uniform sampler2D screen : hint_screen_texture, filter_linear;
uniform float radius = 4.5;
uniform int kind = 0;   // 0 = Gaussian weights, 1 = box (equal) weights
void fragment() {{
    vec2 stride = SCREEN_PIXEL_SIZE * (radius / 3.0) * {dir};
    vec3 c = vec3(0.0);
    float s = 0.0;
    for (int i = -6; i <= 6; i++) {{
        float w = (kind == 1) ? 1.0 : exp(-float(i * i) / 18.0);   // box or Gaussian (sigma 3 taps)
        c += texture(screen, SCREEN_UV + stride * float(i)).rgb * w;
        s += w;
    }}
    COLOR = vec4(c / s, 1.0);
}}";
    }

    private const string FinalShader = @"
shader_type canvas_item;
uniform sampler2D screen : hint_screen_texture, filter_linear;
uniform vec3 tint : source_color = vec3(0.03, 0.035, 0.045);
uniform float tint_amt = 0.5;
uniform float vignette = 0.5;
void fragment() {
    vec3 c = texture(screen, SCREEN_UV).rgb;
    c = mix(c, tint, tint_amt);
    float d = distance(SCREEN_UV, vec2(0.5));
    c *= 1.0 - vignette * smoothstep(0.25, 0.9, d);
    COLOR = vec4(c, 1.0);
}
";
}
