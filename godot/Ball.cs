using Godot;

// A physics ball fired from the drone. A RigidBody that arcs under gravity and bounces off gates,
// props, and the ball-ground; despawns after a few seconds so they don't pile up. On collision layer
// 4, colliding with world (layer 1) + ball-ground (layer 2); the launcher excepts the drone.
public partial class Ball : RigidBody3D
{
    public override void _Ready()
    {
        const float r = 0.09f;
        Mass = 0.06f;
        ContactMonitor = false;
        CollisionLayer = 0b0100;   // layer 3 (balls)
        CollisionMask = 0b0011;    // hit world (layer 1) + ball-ground (layer 2)
        PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.55f, Friction = 0.6f };

        AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = r } });
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = r, Height = r * 2f, RadialSegments = 12, Rings = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.55f, 0.15f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.5f, 0.1f),
                EmissionEnergyMultiplier = 1.6f,   // blooms via the world glow
                Roughness = 0.5f,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        var life = new Timer { WaitTime = 5f, OneShot = true, Autostart = true };
        life.Timeout += QueueFree;
        AddChild(life);
    }
}
