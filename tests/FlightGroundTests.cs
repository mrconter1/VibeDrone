using System.Numerics;
using OpenDrone;
using Xunit;

// Free-mode ground contact (StepGround): the drone should land + rest stably, stay upright from a
// level drop, take off under thrust, and never blow up numerically.
public class FlightGroundTests
{
    private static FlightModel Drop(float startY, float throttle, int ticks)
    {
        var fm = new FlightModel();
        fm.Reset();
        fm.Pos = new Vector3(0f, startY, 0f);
        fm.Vel = Vector3.Zero;
        fm.Omega = Vector3.Zero;
        fm.Rot = Quaternion.Identity;
        for (int i = 0; i < ticks; i++)
        {
            if (fm.LowestLeg() <= 0.03f) fm.StepGround(0f, 0f, 0f, throttle, 0.004f, 0f);
            else fm.Step(0f, 0f, 0f, throttle, 0.004f);
        }
        return fm;
    }

    [Fact]
    public void Lands_and_settles_near_the_ground()
    {
        FlightModel fm = Drop(0.6f, throttle: 0f, ticks: 750);   // 3 s, no thrust
        Assert.InRange(fm.Pos.Y, -0.05f, 0.15f);   // rests on its legs (doesn't sink through / hover)
        Assert.True(fm.Vel.Length() < 0.5f, $"still moving: {fm.Vel.Length()}");
    }

    [Fact]
    public void Stays_upright_from_a_level_drop()
    {
        FlightModel fm = Drop(0.6f, throttle: 0f, ticks: 750);
        Vector3 up = Vector3.Transform(Vector3.UnitY, fm.Rot);
        Assert.True(up.Y > 0.9f, $"tipped over: up.Y={up.Y}");
    }

    [Fact]
    public void Full_throttle_takes_off()
    {
        var fm = new FlightModel();
        fm.Reset();
        fm.Pos = new Vector3(0f, 0.033f, 0f);   // resting on the pad
        fm.Rot = Quaternion.Identity;
        for (int i = 0; i < 300; i++)   // 1.2 s of climb
        {
            if (fm.LowestLeg() <= 0.03f) fm.StepGround(0f, 0f, 0f, 0.9f, 0.004f, 0f);
            else fm.Step(0f, 0f, 0f, 0.9f, 0.004f);
        }
        Assert.True(fm.Pos.Y > 1.0f, $"did not climb: y={fm.Pos.Y}");
    }

    [Fact]
    public void Contact_stays_finite()
    {
        FlightModel fm = Drop(0.6f, throttle: 0.2f, ticks: 750);
        Assert.False(float.IsNaN(fm.Pos.Y) || float.IsInfinity(fm.Pos.Y));
        Assert.False(float.IsNaN(fm.Vel.Length()));
    }
}
