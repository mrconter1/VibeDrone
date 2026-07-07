using Godot;

// A live scene node for one placed Prop. Holds its Data and rebuilds mesh/material/transform from
// it, so the editor can resize/recolor/move by mutating Data and calling Refresh(). In the "movable"
// group (edit mode can grab it) and "prop" group (so it can be identified and serialised back).
public partial class PropNode : Node3D
{
    public Prop Data = null!;

    private MeshInstance3D _mesh = null!;
    private StandardMaterial3D _mat = null!;

    public void Init(Prop data)
    {
        Data = data;
        _mesh = new MeshInstance3D();
        _mat = new StandardMaterial3D { Roughness = 1f };
        _mesh.MaterialOverride = _mat;
        AddChild(_mesh);
        AddToGroup("movable");
        AddToGroup("prop");
        Refresh();
    }

    // Re-apply everything from Data (mesh for the type, colour, and the pos/rot/scale transform).
    public void Refresh()
    {
        _mesh.Mesh = PropTypes.Mesh(Data.Type);
        _mat.AlbedoColor = Data.Color;
        Transform = new Transform3D(new Basis(Data.Rot).Scaled(Data.Scale), Data.Pos);
    }

    // Pull the current node transform back into Data (used after an edit-mode move).
    public void CaptureTransform()
    {
        Data.Pos = Position;
        Data.Rot = Basis.GetRotationQuaternion();
        Data.Scale = Basis.Scale;
    }
}
