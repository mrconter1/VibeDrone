using OpenDrone;
using Xunit;

public class RaceStateTests
{
    private const int Regular = 5;   // 6-gate loop: gates 1..5 regular, gate 0 = finish

    [Fact]
    public void Arm_sits_stopped_at_the_line()
    {
        var r = new RaceState();
        r.Arm();
        Assert.True(r.Armed);
        Assert.False(r.Running);
        Assert.Equal(0f, r.LapTime);
        Assert.Equal(0, r.GatePassed);
    }

    [Fact]
    public void Launch_starts_the_clock()
    {
        var r = new RaceState();
        r.Arm();
        r.Launch();
        Assert.False(r.Armed);
        Assert.True(r.Running);
    }

    [Fact]
    public void Tick_only_advances_while_running()
    {
        var r = new RaceState();
        r.Arm();
        r.Tick(1f);
        Assert.Equal(0f, r.LapTime);   // armed, not running
        r.Launch();
        r.Tick(0.5f);
        r.Tick(0.5f);
        Assert.Equal(1f, r.LapTime, 4);
    }

    [Fact]
    public void Gates_advance_only_in_order()
    {
        var r = new RaceState();
        r.Arm();
        r.Launch();
        Assert.Equal(GateResult.Advanced, r.RegisterGate(1, Regular));
        Assert.Equal(1, r.GatePassed);
        Assert.Equal(GateResult.None, r.RegisterGate(3, Regular));   // skipped 2
        Assert.Equal(1, r.GatePassed);
        Assert.Equal(GateResult.Advanced, r.RegisterGate(2, Regular));
        Assert.Equal(2, r.GatePassed);
    }

    [Fact]
    public void Finish_counts_only_when_all_gates_cleared()
    {
        var r = new RaceState();
        r.Arm();
        r.Launch();
        for (int g = 1; g <= Regular; g++) r.RegisterGate(g, Regular);
        r.Tick(7.5f);
        float before = r.LapTime;
        Assert.Equal(7.5f, before, 4);

        GateResult res = r.RegisterGate(0, Regular);   // cross the finish
        Assert.Equal(GateResult.FinishValid, res);
        Assert.Equal(0f, r.LapTime);   // reset for the next lap
        Assert.Equal(0, r.GatePassed);
    }

    [Fact]
    public void Finish_without_all_gates_is_invalid()
    {
        var r = new RaceState();
        r.Arm();
        r.Launch();
        r.RegisterGate(1, Regular);   // only one gate
        Assert.Equal(GateResult.FinishInvalid, r.RegisterGate(0, Regular));
    }

    [Fact]
    public void Miss_fires_only_on_a_same_gate_forward_crossing_outside_the_opening()
    {
        var r = new RaceState();
        r.Arm();
        r.Launch();
        int next = r.NextGate(Regular);   // gate 1

        // first sample behind the gate (no previous sample yet -> not a miss)
        Assert.False(r.UpdateMiss(next, 5f, 0f, -0.2f));
        // next tick: crossed the plane (z -0.2 -> 0.2) far off centre -> miss
        Assert.True(r.UpdateMiss(next, 5f, 0f, 0.2f));
    }

    [Fact]
    public void Clean_pass_through_the_opening_is_not_a_miss()
    {
        var r = new RaceState();
        r.Arm();
        r.Launch();
        int next = r.NextGate(Regular);
        r.UpdateMiss(next, 1f, 1f, -0.2f);
        Assert.False(r.UpdateMiss(next, 1f, 1f, 0.2f));   // within the opening
    }
}
