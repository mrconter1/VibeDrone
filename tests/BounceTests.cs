using Godot;
using OpenDrone;
using Xunit;

public class BounceTests
{
    // Head-on into a wall whose normal is +X: the normal component reverses and is scaled by
    // restitution; there is no tangential motion to scrub.
    [Fact]
    public void Head_on_reverses_and_scales_by_restitution()
    {
        Vector3 v = Bounce.Respond(new Vector3(-10, 0, 0), new Vector3(1, 0, 0), restitution: 0.3f, friction: 0.5f);
        Assert.Equal(3f, v.X, 3);      // -(-10)*0.3 = +3, pushed back out
        Assert.Equal(0f, v.Y, 3);
        Assert.Equal(0f, v.Z, 3);
    }

    // A departing/along-surface contact must not gain energy: velocity is returned unchanged.
    [Fact]
    public void Departing_contact_is_unchanged()
    {
        var v0 = new Vector3(5, 0, 0);
        Vector3 v = Bounce.Respond(v0, new Vector3(1, 0, 0), 0.9f, 0.9f);   // moving out along +X
        Assert.Equal(v0, v);

        Vector3 along = Bounce.Respond(new Vector3(0, 0, 7), new Vector3(1, 0, 0), 0.9f, 0.9f);   // grazing
        Assert.Equal(new Vector3(0, 0, 7), along);
    }

    // An angled hit: the normal (X) part rebounds by restitution, the tangential (Z) slide is
    // scrubbed by friction, each component independently.
    [Fact]
    public void Angled_hit_splits_normal_and_tangential()
    {
        Vector3 v = Bounce.Respond(new Vector3(-8, 0, 6), new Vector3(1, 0, 0), restitution: 0.25f, friction: 0.4f);
        Assert.Equal(2f, v.X, 3);      // -(-8)*0.25
        Assert.Equal(0f, v.Y, 3);
        Assert.Equal(3.6f, v.Z, 3);    // 6 * (1 - 0.4)
    }

    // No restitution + full friction: a head-on hit stops dead.
    [Fact]
    public void Full_friction_no_restitution_stops()
    {
        Vector3 v = Bounce.Respond(new Vector3(-4, 0, 3), new Vector3(1, 0, 0), restitution: 0f, friction: 1f);
        Assert.Equal(Vector3.Zero, v);
    }
}
