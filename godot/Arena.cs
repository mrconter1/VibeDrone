using System.Collections.Generic;
using Godot;

// The flying environment: sun, procedural sky, grid ground, plus the level's gates and props.
// Self-contained - add it to the scene and it builds the world. A Level (data) is then loaded to
// populate gates + props + ground colour; edits are captured back into the Level and saved.
public partial class Arena : Node3D
{
    private readonly List<Node3D> _gates = new();
    private readonly List<Area3D> _triggers = new();
    private readonly List<PropNode> _props = new();
    private Level _level = null!;
    private ShaderMaterial _groundMat = null!;
    private Truck _truck = null!;

    public Level CurrentLevel => _level;
    public string LevelName => _level?.Name ?? "";
    public IReadOnlyList<Node3D> Gates => _gates;
    public IReadOnlyList<Area3D> GateTriggers => _triggers;
    public IReadOnlyList<PropNode> Props => _props;
    public Transform3D StartTransform => _gates[0].GlobalTransform;   // gate 0 = start/finish

    // Fired whenever the gates are rebuilt (load / add / remove) so the controller can re-wire the
    // pass-through triggers.
    public event System.Action? GatesChanged;

    // Set by the controller: the gate index the pilot must clear next (0 = finish, -1 = not racing).
    // The truck uses it to block the pilot's path.
    public System.Func<int>? NextGateIndex;

    public int GateIndexOf(Node3D n) => _gates.IndexOf(n);

    public override void _Ready()
    {
        BuildWorld();   // world only; a Level is loaded next by the controller
        _truck = new Truck();
        _truck.Setup(this);
        AddChild(_truck);   // wandering obstacle; repositioned per level in LoadLevel
    }

    // Tear down the current gates/props and build the given level (gates + props + ground colour).
    public void LoadLevel(Level lvl)
    {
        _level = lvl;
        foreach (Node3D g in _gates) g.QueueFree();
        foreach (PropNode p in _props) p.QueueFree();
        _gates.Clear();
        _triggers.Clear();
        _props.Clear();

        BuildGates(lvl.Gates);
        foreach (Prop prop in lvl.Props)
        {
            PropNode node = PropTypes.Build(prop);
            AddChild(node);
            _props.Add(node);
        }
        _groundMat.SetShaderParameter("base_color", lvl.Ground.Color);
        _truck?.Reset();   // drop the truck back into the (new) play area
        GatesChanged?.Invoke();
    }

    // Capture the current gate + prop transforms into the Level (no file write).
    private void CaptureToLevel()
    {
        if (_level == null) return;
        _level.Gates.Clear();
        foreach (Node3D g in _gates)
        {
            Transform3D t = g.GlobalTransform;
            _level.Gates.Add(new Pose { Pos = t.Origin, Rot = t.Basis.GetRotationQuaternion() });
        }
        _level.Props.Clear();
        foreach (PropNode p in _props) { p.CaptureTransform(); _level.Props.Add(p.Data); }
    }

    // Capture the current edits into the Level and persist it (from edit mode).
    public void SaveEdits()
    {
        CaptureToLevel();
        if (_level != null) LevelStore.Save(_level);
    }

    // JSON snapshot of the current state (for the undo history).
    public string SnapshotJson()
    {
        CaptureToLevel();
        return _level.ToJson();
    }

    // Restore a snapshot: rebuild everything from it (re-wires triggers) and persist.
    public void RestoreJson(string json)
    {
        Level lvl = Level.FromJson(json);
        LoadLevel(lvl);
        LevelStore.Save(lvl);
    }

    // Spawn a new prop at a position (edit mode); returns the node so the caller can select it.
    public PropNode AddProp(Vector3 pos)
    {
        var data = new Prop { Type = "rock", Pos = pos, Scale = new Vector3(3f, 2.5f, 3f) };
        PropNode node = PropTypes.Build(data);
        AddChild(node);
        _props.Add(node);
        return node;
    }

    // Add a copy of an existing prop (for cloning); returns the new node.
    public PropNode AddProp(Prop data)
    {
        PropNode node = PropTypes.Build(data.Clone());
        AddChild(node);
        _props.Add(node);
        return node;
    }

    public void RemoveProp(PropNode node)
    {
        _props.Remove(node);
        node.QueueFree();
    }

    // Append a new gate (becomes the last regular gate before the finish) and rebuild. Returns the
    // new gate node. Rebuilding renumbers labels + re-wires triggers via GatesChanged.
    public Node3D AddGate(Vector3 pos, Quaternion rot)
    {
        CaptureToLevel();
        _level.Gates.Add(new Pose { Pos = pos, Rot = rot });
        LoadLevel(_level);
        SaveEdits();
        return _gates[^1];
    }

    // Remove a gate by index (never gate 0, and keep at least 2). Returns true if removed.
    public bool RemoveGate(int index)
    {
        CaptureToLevel();
        if (index <= 0 || index >= _level.Gates.Count || _level.Gates.Count <= 2) return false;
        _level.Gates.RemoveAt(index);
        LoadLevel(_level);
        SaveEdits();
        return true;
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
        _groundMat = new ShaderMaterial { Shader = new Shader { Code = GridShaderCode } };
        ground.MaterialOverride = _groundMat;
        AddChild(ground);
    }

    private void BuildGates(List<Pose> poses)
    {
        var green = GateMaterial(new Color(0.10f, 1.0f, 0.30f));
        var red = GateMaterial(new Color(1.0f, 0.15f, 0.20f));
        for (int i = 0; i < poses.Count; i++)
        {
            var gate = new Node3D { Transform = new Transform3D(new Basis(poses[i].Rot), poses[i].Pos) };
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

    private const string CheckerShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled;
void fragment() {
    vec2 p = UV * 8.0;
    vec2 g = floor(p);
    float c = mod(g.x + g.y, 2.0);
    // analytic anti-alias: when a checker cell shrinks below ~1 pixel (far / oblique), fade to the
    // average grey instead of letting the black/white alternate and shimmer.
    float texel = max(fwidth(p).x, fwidth(p).y);
    float aa = clamp(texel - 0.5, 0.0, 1.0);
    ALBEDO = mix(mix(vec3(0.02), vec3(0.95), c), vec3(0.485), aa);
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
