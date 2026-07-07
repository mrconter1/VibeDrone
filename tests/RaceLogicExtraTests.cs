using OpenDrone;
using Xunit;

// Boundary coverage for the pure race decisions.
public class RaceLogicExtraTests
{
    [Theory]
    [InlineData(0, 6, 1)]
    [InlineData(1, 6, 2)]
    [InlineData(5, 6, 6)]
    [InlineData(6, 6, 0)]    // all cleared -> finish
    [InlineData(0, 8, 1)]
    [InlineData(7, 8, 8)]
    [InlineData(8, 8, 0)]
    public void NextGate_covers_the_sequence(int passed, int regular, int expected) =>
        Assert.Equal(expected, RaceLogic.NextGate(passed, regular));

    [Fact]
    public void FlewPastGate_true_just_outside_the_opening()
    {
        // just past the miss half (3.4) on X, plane crossed
        Assert.True(RaceLogic.FlewPastGate(-0.1f, 3.5f, 0f, 0.1f));
    }

    [Fact]
    public void FlewPastGate_false_just_inside_the_opening()
    {
        Assert.False(RaceLogic.FlewPastGate(-0.1f, 3.3f, 3.3f, 0.1f));
    }

    [Fact]
    public void FlewPastGate_needs_a_forward_crossing()
    {
        Assert.False(RaceLogic.FlewPastGate(0.1f, 10f, 0f, 0.2f));    // already in front last tick
        Assert.False(RaceLogic.FlewPastGate(-0.2f, 10f, 0f, -0.1f));  // still behind this tick
    }

    [Fact]
    public void FlewPastGate_catches_a_Y_overshoot_too()
    {
        Assert.True(RaceLogic.FlewPastGate(-0.1f, 0f, 5f, 0.1f));   // way above the opening
    }

    [Theory]
    [InlineData(6, 6, true)]
    [InlineData(5, 6, false)]
    [InlineData(0, 6, false)]
    [InlineData(8, 8, true)]
    public void LapValid_needs_every_regular_gate(int passed, int regular, bool expected) =>
        Assert.Equal(expected, RaceLogic.LapValid(passed, regular));

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(3, 1, false)]
    [InlineData(1, 1, false)]
    public void IsNextRegular_is_strictly_the_next_index(int index, int passed, bool expected) =>
        Assert.Equal(expected, RaceLogic.IsNextRegular(index, passed));
}
