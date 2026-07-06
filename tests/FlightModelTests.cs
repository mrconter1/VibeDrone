using System.Numerics;
using OpenDrone;
using Xunit;

public class FlightModelTests
{
    const float Dt = 1f / 250f;   // matches the 250 Hz physics tick

    static void Run(FlightModel fm, float roll, float pitch, float yaw, float throttle, int ticks)
    {
        for (int i = 0; i < ticks; i++) fm.Step(roll, pitch, yaw, throttle, Dt);
    }

    [Fact]
    public void Reset_puts_the_model_in_a_known_hover_state()
    {
        var fm = new FlightModel();
        Assert.Equal(new Vector3(0f, 2f, 0f), fm.Pos);
        Assert.Equal(Vector3.Zero, fm.Vel);
        Assert.Equal(Quaternion.Identity, fm.Rot);
        Assert.Equal(fm.G, fm.Thrust, 3);   // spun up to support its own weight
    }

    [Fact]
    public void Zero_throttle_falls_under_gravity()
    {
        var fm = new FlightModel();
        Run(fm, 0, 0, 0, 0f, 50);
        Assert.True(fm.Vel.Y < 0f, $"expected downward velocity, got {fm.Vel.Y}");
        Assert.True(fm.Pos.Y < 2f, $"expected to drop below spawn, got {fm.Pos.Y}");
    }

    [Fact]
    public void Full_throttle_climbs()
    {
        var fm = new FlightModel();
        Run(fm, 0, 0, 0, 1f, 100);
        Assert.True(fm.Vel.Y > 0f, $"expected upward velocity, got {fm.Vel.Y}");
        Assert.True(fm.Pos.Y > 2f, $"expected to climb above spawn, got {fm.Pos.Y}");
    }

    [Fact]
    public void Never_sinks_below_the_floor()
    {
        var fm = new FlightModel();
        // drop from spawn with motors off: the floor clamp at Y=0 must hold
        for (int i = 0; i < 500; i++)
        {
            fm.Step(0, 0, 0, 0f, Dt);
            Assert.True(fm.Pos.Y >= 0f, $"fell through the floor at tick {i}: Y={fm.Pos.Y}");
        }
    }

    [Fact]
    public void Is_deterministic_for_identical_inputs()
    {
        var a = new FlightModel();
        var b = new FlightModel();
        Run(a, 0.3f, -0.2f, 0.1f, 0.7f, 200);
        Run(b, 0.3f, -0.2f, 0.1f, 0.7f, 200);
        Assert.Equal(a.Pos, b.Pos);
        Assert.Equal(a.Vel, b.Vel);
        Assert.Equal(a.Rot, b.Rot);
    }

    [Fact]
    public void Yaw_input_rotates_the_drone()
    {
        var fm = new FlightModel();
        Run(fm, 0, 0, 1f, 0.5f, 100);
        // a yaw command should produce a non-identity orientation
        float dot = Quaternion.Dot(fm.Rot, Quaternion.Identity);
        Assert.True(System.MathF.Abs(dot) < 0.999f, $"expected rotation away from identity, dot={dot}");
    }

    [Fact]
    public void RateCurve_matches_its_formula()
    {
        var c = new Vector3(2f, 3f, 0.5f);   // lin, cubic, bias
        float s = 0.5f;
        Assert.Equal(2f * s + 3f * s * s * s + 0.5f, FlightModel.RateCurve(c, s), 5);
    }

    [Fact]
    public void ThrustProxy_is_never_negative_and_rises_with_throttle()
    {
        var fm = new FlightModel();
        float lo = fm.ThrustProxy(0, 0, 0, 0.2f);
        float hi = fm.ThrustProxy(0, 0, 0, 0.9f);
        Assert.True(lo >= 0f);
        Assert.True(hi > lo, $"expected proxy to grow with throttle: lo={lo} hi={hi}");
    }
}
