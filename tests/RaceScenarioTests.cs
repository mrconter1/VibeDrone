using OpenDrone;
using Xunit;

// Full race scenarios over RaceState (arm -> launch -> gates -> finish), including multi-lap and
// failure paths.
public class RaceScenarioTests
{
    private static RaceState Racing()
    {
        var r = new RaceState();
        r.Arm();
        r.Launch();
        return r;
    }

    private static GateResult RunLap(RaceState r, int regular)
    {
        for (int g = 1; g <= regular; g++) r.RegisterGate(g, regular);
        return r.RegisterGate(0, regular);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void A_clean_lap_counts_on_any_track_size(int regular)
    {
        var r = Racing();
        Assert.Equal(GateResult.FinishValid, RunLap(r, regular));
    }

    [Fact]
    public void Two_clean_laps_both_count_and_the_clock_resets()
    {
        var r = Racing();
        Assert.Equal(GateResult.FinishValid, RunLap(r, 6));
        r.Tick(3f);
        Assert.Equal(3f, r.LapTime, 4);
        Assert.Equal(GateResult.FinishValid, RunLap(r, 6));
        Assert.Equal(0f, r.LapTime, 4);   // reset for the next lap
    }

    [Fact]
    public void A_skipped_gate_invalidates_the_lap_but_the_next_can_count()
    {
        var r = Racing();
        r.RegisterGate(1, 6);
        r.RegisterGate(3, 6);   // skipped gate 2 -> ignored
        Assert.Equal(GateResult.FinishInvalid, r.RegisterGate(0, 6));
        // fresh lap, done properly
        Assert.Equal(GateResult.FinishValid, RunLap(r, 6));
    }

    [Fact]
    public void Passing_the_same_gate_twice_does_not_double_count()
    {
        var r = Racing();
        r.RegisterGate(1, 6);
        Assert.Equal(GateResult.None, r.RegisterGate(1, 6));   // already cleared
        Assert.Equal(1, r.GatePassed);
    }

    [Fact]
    public void Arm_mid_race_wipes_progress()
    {
        var r = Racing();
        r.RegisterGate(1, 6);
        r.RegisterGate(2, 6);
        r.Tick(5f);
        r.Arm();
        Assert.False(r.Running);
        Assert.True(r.Armed);
        Assert.Equal(0, r.GatePassed);
        Assert.Equal(0f, r.LapTime);
    }

    [Fact]
    public void Gates_do_nothing_before_launch()
    {
        var r = new RaceState();
        r.Arm();                       // armed, not running
        r.Tick(1f);
        Assert.Equal(0f, r.LapTime);   // clock not running
    }

    [Fact]
    public void Miss_then_clean_reapproach_of_the_next_gate()
    {
        var r = Racing();
        int next = r.NextGate(6);                       // gate 1
        Assert.False(r.UpdateMiss(next, 0f, 0f, -0.3f)); // first sample behind
        Assert.True(r.UpdateMiss(next, 6f, 0f, 0.3f));   // flew past outside the opening
        // a subsequent within-opening crossing is not a miss
        Assert.False(r.UpdateMiss(next, 0.5f, 0.5f, -0.3f));
        Assert.False(r.UpdateMiss(next, 0.5f, 0.5f, 0.3f));
    }
}
