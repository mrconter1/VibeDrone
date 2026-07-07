using System.Collections.Generic;
using Godot;

// Logo browser + two-player vote. L on the main menu opens it; Left/Right browse the 10 minimal
// VibeDrone variants. Two players rate each 1-5 (turn based, keys 1-5, auto-advancing); once both
// have rated all, it shows a ranked results screen. Runs while paused (ProcessMode=Always).
public partial class LogoMenu : CanvasLayer
{
    private enum Mode { Vote, Results }

    private CenterContainer _logoBox = null!, _resBox = null!;
    private LogoCanvas _logo = null!;
    private ResultsCanvas _results = null!;
    private Label _label = null!, _vote = null!, _hint = null!;

    private readonly int[,] _votes = new int[2, LogoCanvas.Count];   // [player, logo] = 0 unvoted, 1-5
    private int _player, _index;
    private Mode _mode = Mode.Vote;
    private bool _open;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 13;
        BuildUi();
        Visible = false;
    }

    public void Toggle()
    {
        _open = !_open;
        Visible = _open;
        if (_open) Refresh();
    }

    public override void _Input(InputEvent ev)
    {
        if (!_open || ev is not InputEventKey { Pressed: true } k) return;
        bool handled = true;

        if (_mode == Mode.Results)
        {
            switch (k.Keycode)
            {
                case Key.R: ResetVotes(); break;
                case Key.L or Key.Escape: Toggle(); break;
                default: handled = false; break;
            }
        }
        else   // Vote
        {
            int n = (int)k.Keycode - (int)Key.Key0;   // 1..5 from the number row
            if (n is >= 1 and <= 5) Vote(n);
            else switch (k.Keycode)
            {
                case Key.Left: _index = (_index - 1 + LogoCanvas.Count) % LogoCanvas.Count; Refresh(); break;
                case Key.Right: _index = (_index + 1) % LogoCanvas.Count; Refresh(); break;
                case Key.Backspace: _votes[_player, _index] = 0; Refresh(); break;
                case Key.Enter or Key.KpEnter when AllVoted(): EnterResults(); break;
                case Key.L or Key.Escape: Toggle(); break;
                default: handled = false; break;
            }
        }
        if (handled) GetViewport().SetInputAsHandled();
    }

    private void Vote(int n)
    {
        _votes[_player, _index] = n;
        if (AllVoted()) { EnterResults(); return; }
        if (PlayerDone(_player)) { _player = 1 - _player; _index = FirstUnvoted(_player); }
        else _index = NextUnvoted(_player, _index);
        Refresh();
    }

    private bool PlayerDone(int p) { for (int i = 0; i < LogoCanvas.Count; i++) if (_votes[p, i] == 0) return false; return true; }
    private bool AllVoted() => PlayerDone(0) && PlayerDone(1);
    private int FirstUnvoted(int p) { for (int i = 0; i < LogoCanvas.Count; i++) if (_votes[p, i] == 0) return i; return 0; }
    private int NextUnvoted(int p, int from)
    {
        for (int k = 1; k <= LogoCanvas.Count; k++) { int i = (from + k) % LogoCanvas.Count; if (_votes[p, i] == 0) return i; }
        return from;
    }

    private void ResetVotes()
    {
        System.Array.Clear(_votes, 0, _votes.Length);
        _player = 0; _index = 0; _mode = Mode.Vote;
        Refresh();
    }

    private void EnterResults()
    {
        var rows = new List<ResultsCanvas.Row>();
        for (int i = 0; i < LogoCanvas.Count; i++)
        {
            int a = _votes[0, i], b = _votes[1, i];
            rows.Add(new ResultsCanvas.Row(LogoCanvas.Names[i], (a + b) / 2f, a, b));
        }
        rows.Sort((x, y) => y.Avg.CompareTo(x.Avg));
        _results.SetData(rows.ToArray());
        _mode = Mode.Results;
        Refresh();
    }

    private void Refresh()
    {
        bool vote = _mode == Mode.Vote;
        _logoBox.Visible = vote;
        _resBox.Visible = !vote;
        _label.Visible = vote;
        _vote.Visible = vote;

        if (vote)
        {
            _logo.Style = _index;
            _logo.QueueRedraw();
            _label.Text = $"PLAYER {_player + 1}      ·      LOGO {_index + 1} / {LogoCanvas.Count}      ·      {LogoCanvas.Names[_index]}";
            _vote.Text = $"{Mark(0)}Player 1   {Stars(_votes[0, _index])}          {Mark(1)}Player 2   {Stars(_votes[1, _index])}";
            _hint.Text = "1-5  rate      ← →  browse      Backspace  clear" + (AllVoted() ? "      Enter  results" : "") + "      L  close";
        }
        else
        {
            _hint.Text = "R  vote again          L  close";
        }
    }

    private string Mark(int p) => _player == p ? "▸ " : "   ";

    private static string Stars(int v)
    {
        var s = "";
        for (int k = 1; k <= 5; k++) s += (k <= v ? "● " : "○ ");
        return s.TrimEnd();
    }

    private void BuildUi()
    {
        var root = new Control { Theme = UiTheme.Get() };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var bg = new ColorRect { Color = new Color(0.02f, 0.025f, 0.03f, 0.99f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        root.AddChild(bg);

        _logoBox = new CenterContainer();
        _logoBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_logoBox);
        _logo = new LogoCanvas { CustomMinimumSize = new Vector2(860, 360) };
        _logoBox.AddChild(_logo);

        _resBox = new CenterContainer { Visible = false };
        _resBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_resBox);
        _results = new ResultsCanvas { CustomMinimumSize = new Vector2(860, 560) };
        _resBox.AddChild(_results);

        _label = TopLabel(40, 22, UiTheme.Accent);
        _label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _label.OffsetTop = 40;
        root.AddChild(_label);

        _vote = TopLabel(0, 24, UiTheme.Text);
        _vote.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _vote.OffsetTop = -116;
        root.AddChild(_vote);

        _hint = TopLabel(0, 15, UiTheme.TextDim);
        _hint.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _hint.OffsetTop = -56;
        root.AddChild(_hint);
    }

    private static Label TopLabel(int _, int size, Color color)
    {
        var l = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }
}
