using System;
using System.Numerics;
using OpenDrone;
using Xunit;

// Launch-pad contact (StepGround): the drone should rest on the platform, stay upright from a level
// drop, take off under thrust, not slide, and never blow up numerically.
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
    public void Lands_and_settles_on_the_pad()
    {
        FlightModel fm = Drop(0.6f, throttle: 0f, ticks: 750);
        Assert.InRange(fm.Pos.Y, -0.05f, 0.15f);
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
        fm.Pos = new Vector3(0f, 0.033f, 0f);
        fm.Rot = Quaternion.Identity;
        for (int i = 0; i < 300; i++)
        {
            if (fm.LowestLeg() <= 0.03f) fm.StepGround(0f, 0f, 0f, 0.9f, 0.004f, 0f);
            else fm.Step(0f, 0f, 0f, 0.9f, 0.004f);
        }
        Assert.True(fm.Pos.Y > 1.0f, $"did not climb: y={fm.Pos.Y}");
    }

    [Fact]
    public void Rests_without_sliding()
    {
        var fm = new FlightModel();
        fm.Reset();
        fm.Pos = new Vector3(0f, 0.033f, 0f);
        fm.Rot = Quaternion.Identity;
        fm.Vel = new Vector3(1.5f, 0f, 0.8f);
        fm.Omega = Vector3.Zero;
        for (int i = 0; i < 500; i++)
        {
            if (fm.LowestLeg() <= 0.03f) fm.StepGround(0f, 0f, 0f, 0.1f, 0.004f, 0f);
            else fm.Step(0f, 0f, 0f, 0.1f, 0.004f);
        }
        float vh = MathF.Sqrt(fm.Vel.X * fm.Vel.X + fm.Vel.Z * fm.Vel.Z);
        Assert.True(vh < 0.1f, $"still sliding: {vh}");
    }

    [Fact]
    public void Tilted_drop_settles_finite()
    {
        var fm = new FlightModel();
        fm.Reset();
        fm.Pos = new Vector3(0f, 0.6f, 0f);
        fm.Rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.35f);
        fm.Vel = Vector3.Zero;
        fm.Omega = Vector3.Zero;
        for (int i = 0; i < 1000; i++)
        {
            if (fm.LowestLeg() <= 0.03f) fm.StepGround(0f, 0f, 0f, 0.15f, 0.004f, 0f);
            else fm.Step(0f, 0f, 0f, 0.15f, 0.004f);
        }
        Assert.False(float.IsNaN(fm.Pos.Y) || float.IsInfinity(fm.Pos.Y));
        Assert.InRange(fm.Pos.Y, -0.2f, 0.5f);
        Assert.True(fm.Vel.Length() < 1.0f);
    }
}
