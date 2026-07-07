using Godot;

// A live scene node for one placed Prop. Holds its Data and rebuilds mesh/material/transform from
// it, so the editor can resize/recolor/move by mutating Data and calling Refresh(). The node itself
// carries only rotation + position (never scale), the visual mesh is scaled, and a Solid prop gets a
// uniform sphere collider (sized to the mean scale) so it can't get a non-uniform-scale collision
// shape. In the "movable" group (edit mode can grab it) and "prop" group (identify + serialise).
public partial class PropNode : Node3D
{
    public Prop Data = null!;

    private MeshInstance3D _mesh = null!;
    private StandardMaterial3D _mat = null!;
    private SphereShape3D _shape = null!;   // null Shape reference stays null when the prop isn't solid

    public void Init(Prop data)
    {
        Data = data;
        _mesh = new MeshInstance3D();
        _mat = new StandardMaterial3D { Roughness = 1f };
        _mesh.MaterialOverride = _mat;
        AddChild(_mesh);

        if (Data.Solid)
        {
            _shape = new SphereShape3D { Radius = 1f };
            var body = new StaticBody3D();
            body.AddChild(new CollisionShape3D { Shape = _shape });
            AddChild(body);   // hitting it resets the lap, exactly like a gate bar
        }

        AddToGroup("movable");
        AddToGroup("prop");
        Refresh();
    }

    // Re-apply everything from Data: mesh, colour, the rotation+position node transform, the mesh
    // scale, and the collider radius.
    public void Refresh()
    {
        _mesh.Mesh = PropTypes.Mesh(Data.Type);
        _mat.AlbedoColor = Data.Color;
        Transform = new Transform3D(new Basis(Data.Rot), Data.Pos);
        ApplyScale();
    }

    // Set rotation from Data and keep the given world position (used while carrying in the editor).
    public void SetPoseKeepPos(Vector3 worldPos)
    {
        GlobalTransform = new Transform3D(new Basis(Data.Rot), worldPos);
        ApplyScale();
    }

    public void SetColor(Color c) { Data.Color = c; _mat.AlbedoColor = c; }

    // Pull the current node transform back into Data (used after an edit-mode move).
    public void CaptureTransform()
    {
        Data.Pos = Position;
        Data.Rot = Basis.GetRotationQuaternion();
        Data.Scale = _mesh.Scale;
    }

    private void ApplyScale()
    {
        _mesh.Scale = Data.Scale;
        if (_shape != null) _shape.Radius = (Data.Scale.X + Data.Scale.Y + Data.Scale.Z) / 3f;
    }
}
