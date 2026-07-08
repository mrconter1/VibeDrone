using System;
using Godot;

// In-game pause menu, rebuilt on the Ui component library: a floating card with a header + context
// line (track / best lap), three labelled groups of icon rows - PLAY (resume / restart / mode
// toggle), OPTIONS (levels / watch / settings / controls), LEAVE (main menu / exit) - and a footer
// key-hint bar. Resume is pre-selected. Order follows the resume-first / quit-last convention.
public partial class PauseMenu : MenuScreen
{
    private static readonly string[] ModeOptions = { "Race", "Free Fly" };

    private Button _first = null!;
    private Segmented _mode = null!;
    private PanelContainer _card = null!;
    private Label _context = null!;

    protected override void OnShow()
    {
        _context.Text = ContextLine();
        _mode.SetIndex(Array.IndexOf(ModeOptions, Ctrl.GameModeName));
        _first.CallDeferred(Control.MethodName.GrabFocus);
        FadeIn();
    }

    protected override void Back() => Ctrl.ResumeGame();

    private string ContextLine()
    {
        float best = Ctrl.BestLapAt(Ctrl.LevelIndex);
        string time = best > 0f ? $"best {Format.Time(best)}" : "no times yet";
        return $"{Ctrl.LevelName}     ·     {time}";
    }

    // Drive the game mode to match the segmented selection (order-agnostic: cycle until it matches).
    private void ApplyMode(int index)
    {
        string want = ModeOptions[index];
        for (int guard = 0; Ctrl.GameModeName != want && guard < ModeOptions.Length; guard++)
            Ctrl.CycleGameMode(1);
    }

    private void FadeIn()
    {
        _card.Modulate = new Color(1, 1, 1, 0);
        _card.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic)
            .TweenProperty(_card, "modulate:a", 1f, 0.16);
    }

    protected override void Build()
    {
        VBoxContainer outer = CenteredBox(out _, sep: 0);

        VBoxContainer v = Ui.Card(out _card);
        _card.CustomMinimumSize = new Vector2(440, 0);
        outer.AddChild(_card);

        var head = Ui.Header("PAUSED", null);
        _context = UiTheme.Body(ContextLine(), UiTheme.TextDim, 15);
        head.AddChild(_context);
        v.AddChild(head);

        v.AddChild(Ui.SectionRow("Play"));
        _first = Row("play", "Resume", () => Ctrl.ResumeGame());
        v.AddChild(_first);
        v.AddChild(Row("restart", "Restart", () => Ctrl.RestartRace()));
        _mode = new Segmented("mode", "Mode", ModeOptions,
            Array.IndexOf(ModeOptions, Ctrl.GameModeName), ApplyMode, RowWidth);
        v.AddChild(_mode);

        v.AddChild(Ui.SectionRow("Options"));
        v.AddChild(Row("levels", "Levels", () => Ctrl.OpenLevels(fromPause: true)));
        v.AddChild(Row("watch", "Watch best lap", () => Ctrl.WatchBest(Ctrl.LevelIndex)));
        v.AddChild(Row("settings", "Settings", () => Ctrl.OpenSettings(fromPause: true)));
        v.AddChild(Row("controls", "Controls", () => Ctrl.OpenHelp()));

        v.AddChild(Ui.SectionRow("Leave"));
        v.AddChild(Row("home", "Main menu", () => Ctrl.OpenMain()));
        v.AddChild(Row("exit", "Exit game", () => GetTree().Quit()));

        v.AddChild(Ui.Divider(UiTheme.S3));
        v.AddChild(Ui.Hints(("↑↓", "navigate"), ("←→", "mode"), ("↵", "select"), ("esc", "resume")));
    }

    private const float RowWidth = 388f;

    private MenuRow Row(string glyph, string text, Action onPressed)
    {
        var r = new MenuRow(glyph, text, RowWidth, onPressed);
        r.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        return r;
    }
}
