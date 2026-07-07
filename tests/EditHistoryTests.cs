using OpenDrone;
using Xunit;

public class EditHistoryTests
{
    [Fact]
    public void Undo_then_redo_walks_the_states()
    {
        var h = new EditHistory();
        h.Reset("a");
        h.Record("b");
        h.Record("c");

        Assert.True(h.CanUndo);
        Assert.False(h.CanRedo);
        Assert.Equal("b", h.Undo());
        Assert.Equal("a", h.Undo());
        Assert.Null(h.Undo());          // nothing before the first state
        Assert.Equal("b", h.Redo());
        Assert.Equal("c", h.Redo());
        Assert.Null(h.Redo());          // nothing after the last
    }

    [Fact]
    public void Recording_after_undo_drops_the_redo_branch()
    {
        var h = new EditHistory();
        h.Reset("a");
        h.Record("b");
        h.Record("c");
        h.Undo();                       // back at "b"
        h.Record("d");                  // new branch from "b"

        Assert.False(h.CanRedo);
        Assert.Equal("b", h.Undo());
        Assert.Equal("d", h.Redo());
    }

    [Fact]
    public void No_op_records_are_ignored()
    {
        var h = new EditHistory();
        h.Reset("a");
        h.Record("a");                  // identical state
        Assert.Equal(1, h.Count);
        Assert.False(h.CanUndo);
    }

    [Fact]
    public void Fresh_history_has_nothing_to_undo_or_redo()
    {
        var h = new EditHistory();
        h.Reset("only");
        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
    }
}
