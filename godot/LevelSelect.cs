using Godot;

// The Levels screen: a list of tracks (name + best lap) on the left, a details panel on the right
// showing the focused track's top times and a "Watch best run" button. Up/down moves the focus,
// Enter races the focused track, Esc goes back. Reachable from the main menu and the pause menu.
public partial class LevelSelect : CanvasLayer
{
    private DroneController _ctrl = null!;
    private VBoxContainer _rows = null!;
    private Label _detName = null!, _detBest = null!, _detList = null!;
    private Button _watch = null!, _firstRow = null!;
    private int _focused;

    public void Setup(DroneController ctrl) => _ctrl = ctrl;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 11;
        BuildUi();
        Visible = false;
    }

    public void Show(bool on)
    {
        Visible = on;
        if (on) { Rebuild(); _firstRow?.CallDeferred(Control.MethodName.GrabFocus); }
    }

    public override void _Input(InputEvent ev)
    {
        if (Visible && UiTheme.IsBack(ev))
        {
            _ctrl.MenuBack();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        var root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = UiTheme.Panel();
        panel.CustomMinimumSize = new Vector2(880, 560);
        center.AddChild(panel);

        var pad = new MarginContainer();
        pad.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (var m in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 32);
        panel.AddChild(pad);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        pad.AddChild(col);

        col.AddChild(UiTheme.Title("LEVELS", 46));

        var split = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 28);
        col.AddChild(split);

        // left: track rows
        _rows = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        _rows.AddThemeConstantOverride("separation", 6);
        split.AddChild(_rows);

        // right: details
        var det = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        det.AddThemeConstantOverride("separation", 10);
        split.AddChild(det);
        _detName = UiTheme.Heading("", 26);
        det.AddChild(_detName);
        _detBest = UiTheme.Body("", UiTheme.Good, 20);
        det.AddChild(_detBest);
        det.AddChild(new HSeparator());
        det.AddChild(UiTheme.Body("TOP LAPS", UiTheme.TextDim, 14));
        _detList = UiTheme.Body("", UiTheme.Text, 18);
        det.AddChild(_detList);
        det.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });   // push actions down
        _watch = UiTheme.MenuItem("▶  Watch best run", () => _ctrl.WatchBest(_focused), 260f);
        det.AddChild(_watch);
        var clear = UiTheme.MenuItem("Clear records", () => { _ctrl.ClearRecords(_focused); Rebuild(); }, 260f);
        clear.AddThemeColorOverride("font_color", UiTheme.TextDim);
        det.AddChild(clear);

        // footer: back button + hint
        col.AddChild(UiTheme.MenuItem("‹  Back", () => _ctrl.MenuBack(), 200f));
        col.AddChild(UiTheme.Body("↑ ↓  select      Enter  race      Esc / Space  back", UiTheme.TextDim, 15));
    }

    // Rebuild the track rows (best times may have changed) and focus the current track.
    private void Rebuild()
    {
        foreach (Node c in _rows.GetChildren()) c.QueueFree();
        _firstRow = null!;

        for (int i = 0; i < _ctrl.TrackCount; i++)
        {
            int idx = i;
            bool current = i == _ctrl.TrackIndex;
            var row = new Button
            {
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(360, 46),
                FocusMode = Control.FocusModeEnum.All,
                Text = (current ? "▸ " : "   ") + _ctrl.TrackNameAt(i).PadRight(16) + FmtTime(_ctrl.BestLapAt(i)),
            };
            row.Pressed += () => _ctrl.PlayTrack(idx);
            row.FocusEntered += () => SetDetails(idx);
            _rows.AddChild(row);
            if (i == _ctrl.TrackIndex || _firstRow == null) _firstRow = row;
        }
        SetDetails(_ctrl.TrackIndex);
    }

    private void SetDetails(int i)
    {
        _focused = i;
        _detName.Text = _ctrl.TrackNameAt(i);
        float best = _ctrl.BestLapAt(i);
        _detBest.Text = best > 0f ? $"BEST  {FmtTime(best)}" : "no times yet";

        float[] laps = LapRecorder.TopLapsFor(i);
        if (laps.Length == 0) { _detList.Text = "-"; _watch.Disabled = true; return; }
        _watch.Disabled = false;
        string s = "";
        for (int r = 0; r < laps.Length && r < 5; r++) s += $"{r + 1}.   {FmtTime(laps[r])}\n";
        _detList.Text = s;
    }

    private static string FmtTime(float t)
    {
        if (t <= 0f) return "--";
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return m > 0 ? $"{m}:{s:00.00}" : $"{s:0.00}";
    }
}
