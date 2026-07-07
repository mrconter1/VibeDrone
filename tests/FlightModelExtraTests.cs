using System;
using System.Numerics;
using OpenDrone;
using Xunit;

// Robustness/regression tests for the fitted flight model: it must stay finite and deterministic
// under any input, and its curves must behave.
public class FlightModelExtraTests
{
    const float Dt = 1f / 250f;

    private static bool Finite(Vector3 v) => !(float.IsNaN(v.X) || float.IsInfinity(v.X) ||
        float.IsNaN(v.Y) || float.IsInfinity(v.Y) || float.IsNaN(v.Z) || float.IsInfinity(v.Z));

    [Theory]
    [InlineData(1f, 1f, 1f, 1f)]
    [InlineData(-1f, -1f, -1f, 0f)]
    [InlineData(1f, -1f, 1f, 0.5f)]
    [InlineData(0f, 0f, 0f, 1f)]
    public void Never_produces_nan_or_infinity(float roll, float pitch, float yaw, float thr)
    {
        var fm = new FlightModel();
        for (int i = 0; i < 2000; i++)
        {
            fm.Step(roll, pitch, yaw, thr, Dt);
            Assert.True(Finite(fm.Pos), $"pos went non-finite at {i}");
            Assert.True(Finite(fm.Vel), $"vel went non-finite at {i}");
        }
    }

    [Fact]
    public void Is_deterministic_over_a_long_run()
    {
        var a = new FlightModel();
        var b = new FlightModel();
        for (int i = 0; i < 1000; i++)
        {
            float s = i * 0.001f;
            a.Step(MathF.Sin(s), MathF.Cos(s), 0.2f, 0.6f, Dt);
            b.Step(MathF.Sin(s), MathF.Cos(s), 0.2f, 0.6f, Dt);
        }
        Assert.Equal(a.Pos, b.Pos);
        Assert.Equal(a.Vel, b.Vel);
        Assert.Equal(a.Rot, b.Rot);
    }

    [Fact]
    public void Floor_holds_even_with_downward_velocity()
    {
        var fm = new FlightModel { Pos = new Vector3(0, 0.1f, 0), Vel = new Vector3(0, -50f, 0) };
        for (int i = 0; i < 100; i++)
        {
            fm.Step(0, 0, 0, 0f, Dt);
            Assert.True(fm.Pos.Y >= 0f, $"fell through floor: {fm.Pos.Y}");
        }
    }

    [Fact]
    public void Velocity_stays_bounded_in_a_long_hover()
    {
        var fm = new FlightModel();
        for (int i = 0; i < 3000; i++) fm.Step(0, 0, 0, 0.5f, Dt);
        Assert.True(fm.Vel.Length() < 100f, $"hover velocity ran away: {fm.Vel.Length()}");
    }

    [Fact]
    public void Reset_returns_to_the_spawn_state()
    {
        var fm = new FlightModel();
        for (int i = 0; i < 200; i++) fm.Step(0.5f, 0.5f, 0.5f, 0.8f, Dt);
        fm.Reset();
        Assert.Equal(new Vector3(0, 2, 0), fm.Pos);
        Assert.Equal(Vector3.Zero, fm.Vel);
        Assert.Equal(Vector3.Zero, fm.Omega);
        Assert.Equal(Quaternion.Identity, fm.Rot);
    }

    [Theory]
    [InlineData(0.2f, 0.4f)]
    [InlineData(0.4f, 0.7f)]
    [InlineData(0.7f, 1.0f)]
    public void ThrustProxy_rises_with_throttle(float lo, float hi)
    {
        var fm = new FlightModel();
        Assert.True(fm.ThrustProxy(0, 0, 0, hi) > fm.ThrustProxy(0, 0, 0, lo));
    }

    [Fact]
    public void ThrustProxy_is_never_negative_across_the_input_space()
    {
        var fm = new FlightModel();
        for (float t = 0; t <= 1f; t += 0.1f)
            for (float s = -1f; s <= 1f; s += 0.5f)
                Assert.True(fm.ThrustProxy(s, s, s, t) >= 0f);
    }

    [Fact]
    public void RateCurve_is_odd_about_its_bias()
    {
        var c = new Vector3(-2.6f, -8.9f, 0.05f);   // lin, cubic, bias
        for (float s = 0.1f; s <= 1f; s += 0.1f)
        {
            float plus = FlightModel.RateCurve(c, s) - c.Z;
            float minus = FlightModel.RateCurve(c, -s) - c.Z;
            Assert.Equal(plus, -minus, 4);
        }
    }

    [Fact]
    public void RateCurve_at_zero_stick_is_just_the_bias()
    {
        var c = new Vector3(1f, 2f, 0.3f);
        Assert.Equal(0.3f, FlightModel.RateCurve(c, 0f), 5);
    }

    [Fact]
    public void Full_throttle_gains_more_altitude_than_half()
    {
        var full = new FlightModel();
        var half = new FlightModel();
        for (int i = 0; i < 150; i++) { full.Step(0, 0, 0, 1f, Dt); half.Step(0, 0, 0, 0.5f, Dt); }
        Assert.True(full.Pos.Y > half.Pos.Y);
    }
}
