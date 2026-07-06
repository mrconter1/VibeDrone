using System.Collections.Generic;
using Godot;

// The flying environment: sun, procedural sky, grid ground, and the circuit of gates.
// Self-contained - add it to the scene and it builds itself. Kept separate from
// DroneController so the controller is only about flying the drone. Gate positions
// persist to user://gates.json (saved on edit, loaded on launch).
public partial class Arena : Node3D
{
    private const string SavePath = "user://gates.json";
    private readonly List<Node3D> _gates = new();
    private readonly List<Area3D> _triggers = new();

    public IReadOnlyList<Node3D> Gates => _gates;
    public IReadOnlyList<Area3D> GateTriggers => _triggers;
    public Transform3D StartTransform => _gates[0].GlobalTransform;   // gate 0 = start/finish

    public override void _Ready()
    {
        BuildWorld();
        BuildGates();
        LoadLayout();
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
                // glow back on but with a high HDR threshold, so only the very bright ghost/trail
                // (emission energy ~4) bloom - the gates (energy ~1) stay crisp, not washed yellow
                GlowEnabled = true,
                GlowHdrThreshold = 1.5f,
                GlowIntensity = 0.9f,
                GlowStrength = 1.1f,
                GlowBloom = 0.05f,
            },
        });

        // crisp anti-aliased grid ground via shader (great motion/altitude reference)
        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(1000, 1000) } };
        ground.MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = GridShaderCode } };
        AddChild(ground);
    }

    // A looped track. Gate 0 is the black/white START-FINISH gate (spawn inside it); gates
    // 1..5 are the numbered green/red gates. Each is yaw-oriented along the track.
    private static readonly Vector2[] Track =
    {
        new(0, 20), new(28, 52), new(16, 92), new(-26, 88), new(-32, 48), new(-12, 22),
    };

    private void BuildGates()
    {
        var green = GateMaterial(new Color(0.10f, 1.0f, 0.30f));
        var red = GateMaterial(new Color(1.0f, 0.15f, 0.20f));
        for (int i = 0; i < Track.Length; i++)
        {
            Vector2 fwd = TrackDir(i);                    // travel direction at this gate (loops)
            float yaw = Mathf.Atan2(fwd.X, fwd.Y);        // align the gate's Z with travel
            var gate = new Node3D
            {
                Position = new Vector3(Track[i].X, 8f, Track[i].Y),
                Rotation = new Vector3(0f, yaw, 0f),
            };
            gate.AddChild(SquareFrame(green, -0.18f));     // fly THROUGH the green side
            gate.AddChild(SquareFrame(red, 0.18f));        // red = wrong side

            bool startFinish = i == 0;
            if (startFinish) BuildFlag(gate);              // checkered start/finish flag above it
            gate.AddChild(new Label3D
            {
                Text = startFinish ? "START / FINISH" : i.ToString(),
                Position = new Vector3(0f, 4.6f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = startFinish ? 48 : 96, PixelSize = 0.02f,
                Modulate = Colors.White, OutlineSize = 16, OutlineModulate = Colors.Black,
            });

            var trigger = new Area3D();                    // pass-through detector in the opening
            trigger.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(6f, 6f, 1f) } });
            gate.AddChild(trigger);
            _triggers.Add(trigger);

            gate.AddToGroup("movable");                    // edit mode can grab it
            AddChild(gate);
            _gates.Add(gate);
        }
    }

    // A checkered racing flag on a pole, mounted above the start/finish gate.
    private static void BuildFlag(Node3D gate)
    {
        gate.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 4f },
            Position = new Vector3(0f, 5.5f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.1f) },
        });
        gate.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(3f, 2f) },
            Position = new Vector3(1.6f, 6.4f, 0f),
            MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = CheckerShaderCode } },
        });
    }

    // Travel direction at gate i, wrapping (it is a loop).
    private static Vector2 TrackDir(int i) =>
        (Track[(i + 1) % Track.Length] - Track[i]).Normalized();

    private static Transform3D DictXform(Godot.Collections.Dictionary d) =>
        new(new Basis(Persistence.ReadRot(d)), Persistence.ReadPos(d));

    // Persist every gate's position/rotation (gate 0 = start/finish). Called after an edit move.
    public void SaveLayout()
    {
        var gates = new Godot.Collections.Array();
        foreach (Node3D g in _gates)
        {
            Transform3D t = g.GlobalTransform;
            gates.Add(Persistence.PoseDict(t.Origin, t.Basis.GetRotationQuaternion()));
        }
        Persistence.Save(SavePath, new Godot.Collections.Dictionary { { "gates", gates } });
    }

    // Apply a saved layout, if any, over the default positions.
    private void LoadLayout()
    {
        if (!Persistence.TryLoad(SavePath, out Variant parsed) || parsed.VariantType != Variant.Type.Dictionary) return;
        var root = parsed.AsGodotDictionary();
        if (!root.ContainsKey("gates")) return;
        var gates = root["gates"].AsGodotArray();
        for (int i = 0; i < gates.Count && i < _gates.Count; i++)
            _gates[i].GlobalTransform = DictXform(gates[i].AsGodotDictionary());
    }

    private const string CheckerShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled;
void fragment() {
    vec2 g = floor(UV * 8.0);
    float c = mod(g.x + g.y, 2.0);
    ALBEDO = mix(vec3(0.02), vec3(0.95), c);
}
";

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
