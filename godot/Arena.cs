using Godot;

// The flying environment: sun, procedural sky, grid ground, and the circuit of gates.
// Self-contained - add it to the scene and it builds itself. Kept separate from
// DroneController so the controller is only about flying the drone.
public partial class Arena : Node3D
{
    public override void _Ready()
    {
        BuildWorld();
        BuildGates();
    }

    private void BuildWorld()
    {
        // sun with shadows
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50, -50, 0),
            ShadowEnabled = true,
            LightEnergy = 1.1f,
        });

        // procedural sky -> real horizon + sky-based ambient
        var sky = new Sky { SkyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.30f, 0.55f, 0.90f),
            SkyHorizonColor = new Color(0.70f, 0.80f, 0.92f),
            GroundHorizonColor = new Color(0.70f, 0.80f, 0.92f),
            GroundBottomColor = new Color(0.20f, 0.23f, 0.28f),
            SunAngleMax = 30f,
        } };
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = sky,
                AmbientLightSource = Godot.Environment.AmbientSource.Sky,
                AmbientLightEnergy = 0.6f,
                TonemapMode = Godot.Environment.ToneMapper.Aces,
                // SSAO off: costly on an integrated GPU and adds nothing on a flat grid scene
                SsaoEnabled = false,
                GlowEnabled = false,  // off: bloom was washing the green gates toward yellow
            },
        });

        // crisp anti-aliased grid ground via shader (great motion/altitude reference)
        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(1000, 1000) } };
        ground.MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = GridShaderCode } };
        AddChild(ground);
    }

    private void BuildGates()
    {
        // a circuit of square gates to fly through. Each gate has a GREEN frame on the
        // entry side (-Z) and a RED frame on the far side (+Z), so the correct direction
        // through is obvious. Two shared materials for all gates.
        var green = GateMaterial(new Color(0.10f, 1.0f, 0.30f));
        var red = GateMaterial(new Color(1.0f, 0.15f, 0.20f));
        Vector2[] layout =
        {
            new(0, 40), new(35, 75), new(0, 110), new(-45, 90),
            new(-60, 40), new(-30, 5), new(30, 10), new(55, 45),
        };
        for (int i = 0; i < layout.Length; i++)
        {
            Vector2 p = layout[i];
            var gate = new Node3D { Position = new Vector3(p.X, 8f, p.Y) };
            gate.AddChild(SquareFrame(green, -0.18f));   // fly THROUGH the green side
            gate.AddChild(SquareFrame(red, 0.18f));      // red = wrong side
            gate.AddChild(new Label3D                    // floating billboard number above the gate
            {
                Text = (i + 1).ToString(),
                Position = new Vector3(0f, 4.6f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 96,
                PixelSize = 0.02f,
                Modulate = Colors.White,
                OutlineSize = 16,
                OutlineModulate = Colors.Black,
            });
            gate.AddToGroup("movable");                  // edit mode can grab it
            AddChild(gate);
        }
    }

    // moderate emission (no glow) so the colour stays true instead of blowing out
    private static StandardMaterial3D GateMaterial(Color c) => new()
    {
        AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 1.0f,
    };

    // A square gate frame (4 emissive, collidable bars) in the XY plane, offset along local Z.
    // Every bar spans the FULL outer square, so they overlap at the corners into solid, fused
    // joints (one continuous welded frame; 6 m opening).
    private static Node3D SquareFrame(Material mat, float z)
    {
        var frame = new Node3D { Position = new Vector3(0f, 0f, z) };
        const float half = 3.0f, t = 0.3f;      // 6 m opening, 0.3 m bars
        const float full = half * 2f + 2f * t, off = half + t * 0.5f;   // full outer side, corner-to-corner
        (Vector3 size, Vector3 pos)[] bars =
        {
            (new Vector3(full, t, t), new Vector3(0f, off, 0f)),    // top
            (new Vector3(full, t, t), new Vector3(0f, -off, 0f)),   // bottom
            (new Vector3(t, full, t), new Vector3(-off, 0f, 0f)),   // left  (overlaps top/bottom at corners)
            (new Vector3(t, full, t), new Vector3(off, 0f, 0f)),    // right
        };
        foreach (var (size, pos) in bars)
        {
            var body = new StaticBody3D { Position = pos };
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = mat });
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            frame.AddChild(body);
        }
        return frame;
    }

    private const string GridShaderCode = @"
shader_type spatial;
render_mode cull_disabled;
uniform vec3 base_color : source_color = vec3(0.13, 0.16, 0.20);
uniform vec3 line_color : source_color = vec3(0.35, 0.42, 0.52);
uniform vec3 major_color : source_color = vec3(0.55, 0.65, 0.78);
varying vec3 wpos;
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
float gridline(vec2 p, float step) {
    vec2 g = abs(fract(p / step - 0.5) - 0.5) / fwidth(p / step);
    return 1.0 - min(min(g.x, g.y), 1.0);
}
void fragment() {
    float minor = gridline(wpos.xz, 2.0);
    float major = gridline(wpos.xz, 20.0);
    vec3 col = mix(base_color, line_color, minor);
    col = mix(col, major_color, major);
    ALBEDO = col;
    ROUGHNESS = 1.0;
}
";
}
