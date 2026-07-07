using Godot;
using Xunit;

// TrackBuilder.Flat is the authored-track gate placement (Generated uses Curve3D, which needs the
// engine, so it isn't covered here). Verify gate count, positions and travel-aligned orientation.
public class TrackBuilderTests
{
    [Fact]
    public void One_gate_per_point_at_the_given_height()
    {
        var pts = new Vector2[] { new(0, 20), new(30, 40), new(20, 80), new(-20, 80), new(-30, 40) };
        Transform3D[] g = TrackBuilder.Flat(pts, 8f);

        Assert.Equal(pts.Length, g.Length);
        for (int i = 0; i < pts.Length; i++)
        {
            Assert.Equal(pts[i].X, g[i].Origin.X, 3);
            Assert.Equal(8f, g[i].Origin.Y, 3);
            Assert.Equal(pts[i].Y, g[i].Origin.Z, 3);   // Vector2.Y maps to world Z
        }
    }

    [Fact]
    public void Height_is_configurable()
    {
        Transform3D[] g = TrackBuilder.Flat(new Vector2[] { new(0, 0), new(0, 10) }, 12f);
        Assert.Equal(12f, g[0].Origin.Y, 3);
    }

    [Fact]
    public void Gate_forward_points_along_travel_toward_the_next_gate()
    {
        // straight north then a right-angle east; each gate's local +Z should follow travel (XZ)
        var pts = new Vector2[] { new(0, 0), new(0, 10), new(10, 10) };
        Transform3D[] g = TrackBuilder.Flat(pts);

        Vector3 fwd0 = g[0].Basis.Z.Normalized();   // travel 0->1 is +Z
        Assert.Equal(0f, fwd0.X, 2);
        Assert.Equal(1f, fwd0.Z, 2);

        Vector3 fwd1 = g[1].Basis.Z.Normalized();   // travel 1->2 is +X
        Assert.Equal(1f, fwd1.X, 2);
        Assert.Equal(0f, fwd1.Z, 2);
    }

    [Fact]
    public void Last_gate_orients_back_toward_the_first_closing_the_loop()
    {
        var pts = new Vector2[] { new(0, 0), new(10, 0), new(10, 10) };
        Transform3D[] g = TrackBuilder.Flat(pts);
        // last point (10,10) travels back to first (0,0): direction (-1,-1) normalized
        Vector3 fwd = g[2].Basis.Z.Normalized();
        Assert.Equal(-0.7071f, fwd.X, 2);
        Assert.Equal(-0.7071f, fwd.Z, 2);
    }

    [Fact]
    public void Gates_stay_upright()
    {
        var pts = new Vector2[] { new(0, 0), new(20, 5), new(-10, 30) };
        foreach (Transform3D t in TrackBuilder.Flat(pts))
            Assert.Equal(1f, t.Basis.Y.Normalized().Y, 3);   // up axis stays world-up (yaw only)
    }
}
