using Godot;

// A semi truck + trailer that drives around the level as a moving obstacle. It wanders to random
// points and, some of the time, parks under a gate to block the approach. Cab + trailer are
// AnimatableBody3D (moved in code, collide with the drone) so clipping it resets the lap like a gate
// bar. It only drives while the game is running (pauses with the tree in menus / edit mode).
public partial class Truck : Node3D
{
    [Export] public float Speed = 13f;
    [Export] public float TurnRate = 1.1f;      // rad/s cab steering
    [Export] public float BlockChance = 0.4f;   // odds a new target is "block a gate"

    private const float CabW = 2.4f, CabH = 2.8f, CabL = 3.4f;
    private const float TrW = 2.5f, TrH = 3.0f, TrL = 9f;
    private const float WheelR = 0.7f;

    private Arena _arena = null!;
    private AnimatableBody3D _cab = null!, _trailer = null!;
    private Vector3 _pos, _trailerPos;
    private float _yaw, _trailerYaw;
    private Vector3 _target;
    private float _modeTimer;

    public void Setup(Arena arena) => _arena = arena;

    public override void _Ready()
    {
        _cab = BuildCab();
        AddChild(_cab);
        _trailer = BuildTrailer();
        AddChild(_trailer);
        Reset();
    }

    // Reposition sensibly for a (new) level and pick a fresh target.
    public void Reset()
    {
        _yaw = 0f;
        _pos = new Vector3(0f, CabH * 0.5f, -30f);
        _trailerYaw = 0f;
        _trailerPos = _pos - Fwd(_yaw) * (CabL * 0.5f + TrL * 0.5f);
        _trailerPos.Y = TrH * 0.5f;
        Place();
        PickTarget();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _modeTimer -= dt;

        Vector3 to = _target - _pos; to.Y = 0f;
        if (to.Length() < 6f || _modeTimer <= 0f) { PickTarget(); to = _target - _pos; to.Y = 0f; }

        if (to.LengthSquared() > 1e-4f)
            _yaw = StepAngle(_yaw, Mathf.Atan2(to.X, to.Z), TurnRate * dt);

        Vector3 fwd = Fwd(_yaw);
        _pos += fwd * Speed * dt;
        _pos.Y = CabH * 0.5f;

        // trailer trails the hitch, easing toward the cab
        Vector3 hitch = _pos - fwd * (CabL * 0.5f);
        Vector3 tv = hitch - _trailerPos; tv.Y = 0f;
        if (tv.LengthSquared() > 1e-4f)
            _trailerYaw = StepAngle(_trailerYaw, Mathf.Atan2(tv.X, tv.Z), 2.5f * dt);
        _trailerPos = hitch - Fwd(_trailerYaw) * (TrL * 0.5f);
        _trailerPos.Y = TrH * 0.5f;

        Place();
    }

    private void Place()
    {
        _cab.GlobalTransform = new Transform3D(BasisYaw(_yaw), _pos);
        _trailer.GlobalTransform = new Transform3D(BasisYaw(_trailerYaw), _trailerPos);
    }

    // Choose the next destination: sometimes block a random gate, otherwise wander in the play area.
    private void PickTarget()
    {
        _modeTimer = (float)GD.RandRange(6.0, 14.0);
        int gates = _arena.Gates.Count;
        if (gates > 1 && GD.Randf() < BlockChance)
        {
            int idx = 1 + (int)(GD.Randi() % (uint)(gates - 1));   // a regular gate (not start/finish)
            Vector3 g = _arena.Gates[idx].GlobalPosition;
            _target = new Vector3(g.X, 0f, g.Z);                   // park on the ground under it
        }
        else
        {
            Bounds(out Vector2 min, out Vector2 max);
            _target = new Vector3((float)GD.RandRange(min.X, max.X), 0f, (float)GD.RandRange(min.Y, max.Y));
        }
    }

    private void Bounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(-50, -50); max = new Vector2(50, 50);
        if (_arena.Gates.Count == 0) return;
        min = max = new Vector2(_arena.Gates[0].GlobalPosition.X, _arena.Gates[0].GlobalPosition.Z);
        foreach (Node3D g in _arena.Gates)
        {
            Vector3 p = g.GlobalPosition;
            min = new Vector2(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Z));
            max = new Vector2(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Z));
        }
        min -= new Vector2(15, 15); max += new Vector2(15, 15);
    }

    private static Vector3 Fwd(float yaw) => new(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    private static Basis BasisYaw(float yaw) => Basis.Identity.Rotated(Vector3.Up, yaw);

    // Rotate cur toward tgt by at most maxStep (shortest way).
    private static float StepAngle(float cur, float tgt, float maxStep)
    {
        float diff = tgt - cur;
        while (diff > Mathf.Pi) diff -= Mathf.Tau;
        while (diff < -Mathf.Pi) diff += Mathf.Tau;
        return cur + Mathf.Clamp(diff, -maxStep, maxStep);
    }

    // --- model ---
    private AnimatableBody3D BuildCab()
    {
        var body = new AnimatableBody3D { SyncToPhysics = false };
        Box(body, new Vector3(CabW, CabH, CabL), Vector3.Zero, new Color(0.80f, 0.16f, 0.13f));   // red cab
        Box(body, new Vector3(CabW * 0.9f, CabH * 0.5f, 0.2f), new Vector3(0f, CabH * 0.1f, CabL * 0.5f),
            new Color(0.2f, 0.28f, 0.35f));   // windscreen
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(CabW, CabH, CabL) } });
        Wheels(body, CabW, CabL, 1);
        return body;
    }

    private AnimatableBody3D BuildTrailer()
    {
        var body = new AnimatableBody3D { SyncToPhysics = false };
        Box(body, new Vector3(TrW, TrH, TrL), Vector3.Zero, new Color(0.86f, 0.86f, 0.89f));   // white box
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(TrW, TrH, TrL) } });
        Wheels(body, TrW, TrL, 2);
        return body;
    }

    private static void Box(Node3D parent, Vector3 size, Vector3 pos, Color col)
    {
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = pos,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = col, Roughness = 0.8f },
        });
    }

    // A row of wheels down each side at the rear (axles = local X).
    private static void Wheels(Node3D parent, float width, float length, int axles)
    {
        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.07f, 0.08f), Roughness = 1f };
        var mesh = new CylinderMesh { TopRadius = WheelR, BottomRadius = WheelR, Height = 0.4f };
        for (int a = 0; a < axles; a++)
        {
            float z = -length * 0.5f + 1.2f + a * 1.6f;
            foreach (float sx in new[] { -1f, 1f })
                parent.AddChild(new MeshInstance3D
                {
                    Mesh = mesh,
                    MaterialOverride = mat,
                    Position = new Vector3(sx * (width * 0.5f), -WheelR, z),
                    RotationDegrees = new Vector3(0f, 0f, 90f),   // lay the cylinder on the X axle
                });
        }
    }
}
