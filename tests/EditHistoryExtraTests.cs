using OpenDrone;
using Xunit;

// Deeper coverage of the undo/redo stack: capacity, long walks, branch behaviour.
public class EditHistoryExtraTests
{
    [Fact]
    public void Long_walk_undo_all_then_redo_all()
    {
        var h = new EditHistory();
        h.Reset("s0");
        for (int i = 1; i <= 20; i++) h.Record("s" + i);

        for (int i = 19; i >= 0; i--) Assert.Equal("s" + i, h.Undo());
        Assert.False(h.CanUndo);
        for (int i = 1; i <= 20; i++) Assert.Equal("s" + i, h.Redo());
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void History_is_capped_and_drops_the_oldest()
    {
        var h = new EditHistory();
        h.Reset("start");
        for (int i = 0; i < 500; i++) h.Record("s" + i);   // far over the cap

        Assert.True(h.Count <= 200, $"history grew to {h.Count}");

        // undo as far as possible; we should never get back to the dropped "start"
        string? last = null;
        while (h.CanUndo) last = h.Undo();
        Assert.NotEqual("start", last);
    }

    [Fact]
    public void Branching_after_a_deep_undo()
    {
        var h = new EditHistory();
        h.Reset("a");
        h.Record("b");
        h.Record("c");
        h.Record("d");
        h.Undo();            // c
        h.Undo();            // b
        h.Record("x");       // branch from b -> drops c,d
        Assert.False(h.CanRedo);
        Assert.Equal("b", h.Undo());
        Assert.Equal("x", h.Redo());
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void Consecutive_identical_records_are_collapsed()
    {
        var h = new EditHistory();
        h.Reset("a");
        h.Record("b");
        h.Record("b");
        h.Record("b");
        Assert.Equal(2, h.Count);
    }
}
