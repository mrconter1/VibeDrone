using Godot;

// The Levels screen: a list of levels (name + best lap) on the left, a details panel on the right
// with the focused level's top times, "Watch best run" and "Clear records". Up/down moves the focus,
// Enter races, Esc/Space go back. Reachable from the main menu and the pause menu.
public partial class LevelSelect : MenuScreen
{
    private VBoxContainer _rows = null!;
    private Label _detName = null!, _detBest = null!, _detList = null!;
    private Button _watch = null!, _delete = null!, _firstRow = null!;
    private MapPreview3D _preview = null!;
    private int _focused;

    protected override void OnShow() { Rebuild(); _firstRow?.CallDeferred(Control.MethodName.GrabFocus); }
    protected override void Back() => Ctrl.MenuBack();

    protected override void Build()
    {
        VBoxContainer col = CenteredPanel(new Vector2(940, 620), sep: 16);

        col.AddChild(UiTheme.Title("LEVELS", 46));

        var split = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 28);
        col.AddChild(split);

        _rows = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        _rows.AddThemeConstantOverride("separation", 6);
        split.AddChild(_rows);

        var det = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        det.AddThemeConstantOverride("separation", 10);
        split.AddChild(det);
        _detName = UiTheme.Heading("", 26);
        det.AddChild(_detName);
        _detBest = UiTheme.Body("", UiTheme.Good, 20);
        det.AddChild(_detBest);
        det.AddChild(UiTheme.Body("MAP", UiTheme.TextDim, 14));
        _preview = new MapPreview3D { CustomMinimumSize = new Vector2(360, 230) };
        det.AddChild(_preview);
        det.AddChild(new HSeparator());
        det.AddChild(UiTheme.Body("TOP LAPS", UiTheme.TextDim, 14));
        _detList = UiTheme.Body("", UiTheme.Text, 18);
        det.AddChild(_detList);
        det.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });   // push actions down
        _watch = UiTheme.MenuItem("▶  Watch best run", () => Ctrl.WatchBest(_focused), 260f);
        det.AddChild(_watch);
        var clear = UiTheme.MenuItem("Clear records", () => { Ctrl.ClearRecords(_focused); Rebuild(); }, 260f);
        clear.AddThemeColorOverride("font_color", UiTheme.TextDim);
        det.AddChild(clear);
        _delete = UiTheme.MenuItem("Delete level", () => { Ctrl.DeleteLevel(_focused); Rebuild(); }, 260f);
        _delete.AddThemeColorOverride("font_color", UiTheme.TextDim);
        det.AddChild(_delete);

        col.AddChild(UiTheme.MenuItem("‹  Back", () => Ctrl.MenuBack(), 200f));
        col.AddChild(UiTheme.Body("↑ ↓  select      Enter  race      Esc / Space  back", UiTheme.TextDim, 15));
    }

    // Rebuild the level rows (best times may have changed) and focus the current level.
    private void Rebuild()
    {
        foreach (Node c in _rows.GetChildren()) c.QueueFree();
        _firstRow = null!;

        for (int i = 0; i < Ctrl.LevelCount; i++)
        {
            int idx = i;
            bool current = i == Ctrl.LevelIndex;
            var row = new Button
            {
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(360, 46),
                FocusMode = Control.FocusModeEnum.All,
                Text = (current ? "▸ " : "   ") + Ctrl.LevelNameAt(i).PadRight(16) + Format.Time(Ctrl.BestLapAt(i)),
            };
            row.Pressed += () => Ctrl.PlayLevel(idx);
            row.FocusEntered += () => SetDetails(idx);
            _rows.AddChild(row);
            if (i == Ctrl.LevelIndex || _firstRow == null) _firstRow = row;
        }
        SetDetails(Ctrl.LevelIndex);
    }

    private void SetDetails(int i)
    {
        _focused = i;
        _delete.Visible = !Ctrl.IsBuiltInLevel(i);   // built-ins can't be deleted
        _preview.SetLevel(Ctrl.PreviewLevel(i));     // top-down map schematic
        _detName.Text = Ctrl.LevelNameAt(i);
        float best = Ctrl.BestLapAt(i);
        _detBest.Text = best > 0f ? $"BEST  {Format.Time(best)}" : "no times yet";

        float[] laps = Ctrl.TopLapsAt(i);
        if (laps.Length == 0) { _detList.Text = "-"; _watch.Disabled = true; return; }
        _watch.Disabled = false;
        string s = "";
        for (int r = 0; r < laps.Length && r < 5; r++) s += $"{r + 1}.   {Format.Time(laps[r])}\n";
        _detList.Text = s;
    }
}
