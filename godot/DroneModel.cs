using System.Collections.Generic;
using Godot;

// A simple but recognisable 5" quad: centre body, 4 arms in an X, motors, and spinning
// props. Used for the ghost racer. Ghost=true makes it translucent/cyan. ~0.3 m across.
public partial class DroneModel : Node3D
{
    public bool Ghost;
    private readonly List<Node3D> _props = new();

    public override void _Ready()
    {
        bool g = Ghost;
        // ghost = bright emissive cyan (glows via the high-threshold world glow); real = dark carbon
        var body = g ? Glow(new Color(0.2f, 0.8f, 1f), 0.85f) : Solid(new Color(0.12f, 0.12f, 0.14f));
        var arm = g ? Glow(new Color(0.2f, 0.8f, 1f), 0.8f) : Solid(new Color(0.08f, 0.08f, 0.10f));
        var prop = g ? Glow(new Color(0.6f, 0.95f, 1f), 0.7f) : Trans(new Color(0.7f, 0.7f, 0.75f, 0.6f));

        Add(new BoxMesh { Size = new Vector3(0.16f, 0.05f, 0.20f) }, Vector3.Zero, body);          // frame body
        Add(new BoxMesh { Size = new Vector3(0.10f, 0.04f, 0.06f) }, new Vector3(0, 0.045f, 0), body); // canopy

        const float a = 0.13f;    // motor offset from centre
        Vector2[] corners = { new(a, a), new(a, -a), new(-a, a), new(-a, -a) };
        foreach (Vector2 cn in corners)
        {
            var motorPos = new Vector3(cn.X, 0.01f, cn.Y);

            // arm: thin box from centre out to the motor
            var armNode = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.025f, 0.02f, cn.Length() * 2f) }, MaterialOverride = arm };
            armNode.Position = new Vector3(cn.X * 0.5f, 0f, cn.Y * 0.5f);
            armNode.Rotation = new Vector3(0, Mathf.Atan2(cn.X, cn.Y), 0);
            AddChild(armNode);

            Add(new CylinderMesh { TopRadius = 0.022f, BottomRadius = 0.022f, Height = 0.05f }, motorPos + new Vector3(0, 0.03f, 0), arm); // motor

            // spinning propeller (two blades)
            var hub = new Node3D { Position = motorPos + new Vector3(0, 0.06f, 0) };
            hub.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.004f, 0.022f) }, MaterialOverride = prop });
            AddChild(hub);
            _props.Add(hub);
        }
    }

    private void Add(Mesh mesh, Vector3 pos, Material mat) =>
        AddChild(new MeshInstance3D { Mesh = mesh, Position = pos, MaterialOverride = mat });

    private static StandardMaterial3D Solid(Color c) => new() { AlbedoColor = c };

    private static StandardMaterial3D Trans(Color c) => new()
    { AlbedoColor = c, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };

    private static StandardMaterial3D Glow(Color c, float alpha) => new()
    {
        AlbedoColor = new Color(c.R, c.G, c.B, alpha),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        EmissionEnabled = true,
        Emission = c,
        EmissionEnergyMultiplier = 4f,
    };

    public override void _Process(double delta)
    {
        float spin = (float)delta * 90f;   // fast visual spin
        foreach (Node3D p in _props) p.RotateY(spin);
    }

    // Fade the whole model (0 = opaque, 1 = invisible) - used to fade the ghost when the live drone
    // gets close so it doesn't block the view.
    public void SetFade(float t)
    {
        foreach (Node c in GetChildren()) Fade(c, t);
    }

    private static void Fade(Node n, float t)
    {
        if (n is GeometryInstance3D gi) gi.Transparency = t;
        foreach (Node c in n.GetChildren()) Fade(c, t);
    }
}
