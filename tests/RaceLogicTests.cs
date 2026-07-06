using OpenDrone;
using Xunit;

public class RaceLogicTests
{
    // --- NextGate: advances through the regular gates then targets the finish (0) ---
    [Theory]
    [InlineData(0, 5, 1)]   // none cleared -> gate 1 next
    [InlineData(4, 5, 5)]   // 4 cleared -> gate 5 next
    [InlineData(5, 5, 0)]   // all cleared -> finish line next
    public void NextGate_advances_then_wraps_to_finish(int passed, int regular, int expected) =>
        Assert.Equal(expected, RaceLogic.NextGate(passed, regular));

    [Theory]
    [InlineData(2, 1, true)]    // gate 2 when 1 cleared: next in order
    [InlineData(3, 1, false)]   // gate 3 when 1 cleared: skipped ahead
    [InlineData(1, 1, false)]   // gate 1 again when 1 cleared: not forward
    public void IsNextRegular_only_accepts_the_next_in_order(int index, int passed, bool expected) =>
        Assert.Equal(expected, RaceLogic.IsNextRegular(index, passed));

    [Theory]
    [InlineData(5, 5, true)]    // all cleared -> valid lap
    [InlineData(6, 5, true)]    // more than all (defensive) -> valid
    [InlineData(4, 5, false)]   // one gate missed -> invalid
    [InlineData(0, 5, false)]   // straight to finish -> invalid
    public void LapValid_requires_all_regular_gates(int passed, int regular, bool expected) =>
        Assert.Equal(expected, RaceLogic.LapValid(passed, regular));

    // --- FlewPastGate: crossed the plane (Z: <0 -> >=0) but outside the opening ---
    [Fact]
    public void FlewPastGate_true_when_crossing_outside_opening()
    {
        // came from behind (prevZ -0.1) to in front (curZ 0.1), 4 m off centre in X (> 3.4)
        Assert.True(RaceLogic.FlewPastGate(-0.1f, 4.0f, 0f, 0.1f));
    }

    [Fact]
    public void FlewPastGate_false_when_passing_through_opening()
    {
        // same plane crossing but within the 3.4 m opening on both axes -> a clean pass
        Assert.False(RaceLogic.FlewPastGate(-0.1f, 1.0f, 1.0f, 0.1f));
    }

    [Fact]
    public void FlewPastGate_false_when_plane_not_crossed()
    {
        // still behind the gate (curZ negative): no crossing yet, even if far off centre
        Assert.False(RaceLogic.FlewPastGate(-0.2f, 5.0f, 0f, -0.1f));
    }

    [Fact]
    public void FlewPastGate_false_when_moving_backwards_through_plane()
    {
        // prevZ already in front (>=0): not a forward crossing
        Assert.False(RaceLogic.FlewPastGate(0.1f, 5.0f, 0f, 0.2f));
    }
}
