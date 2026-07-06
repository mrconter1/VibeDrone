using System.Collections.Generic;
using Godot;

// A glowing, fading ribbon trail. Callers supply, oldest->newest: world points, the drone's
// right-axis at each point (so the ribbon rolls with the drone), and each point's age in 0..1
// (0 = head/newest, 1 = tail). The ribbon tapers from full width at the head to a point at the
// tail. Shared by the in-race ghost trail and the playback trail.
public partial class TrailRibbon : MeshInstance3D
{
    private ImmediateMesh _mesh = null!;

    public override void _Ready()
    {
        _mesh = new ImmediateMesh();
        Mesh = _mesh;
        Visible = false;
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true,
            Emission = new Color(0.3f, 0.95f, 1f),
            EmissionEnergyMultiplier = 3f,   // blooms via the high-threshold world glow
        };
    }

    public void Build(IReadOnlyList<Vector3> pts, IReadOnlyList<Vector3> right, IReadOnlyList<float> age, float halfWidth)
    {
        _mesh.ClearSurfaces();
        if (pts.Count < 2) { Visible = false; return; }
        Visible = true;
        _mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 r = right[i];
            Vector3 side = (r.LengthSquared() > 1e-6f ? r.Normalized() : Vector3.Right) * (halfWidth * (1f - age[i]));
            float a = 1f - age[i];
            var col = new Color(0.4f, 0.95f, 1f, a * a);   // fade toward the tail (eased)
            _mesh.SurfaceSetColor(col);
            _mesh.SurfaceAddVertex(pts[i] - side);
            _mesh.SurfaceSetColor(col);
            _mesh.SurfaceAddVertex(pts[i] + side);
        }
        _mesh.SurfaceEnd();
    }
}
