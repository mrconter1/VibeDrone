using Godot;

// In-game pause menu, grouped: play actions (resume / restart / mode), navigation (levels / watch /
// settings / controls), then leave (main menu / exit). The Mode row switches Race <-> Free Fly with
// Left/Right (or Enter). Esc/Space/Backspace resume. Navigation routes through DroneController.
public partial class PauseMenu : MenuScreen
{
    private Button _first = null!, _modeBtn = null!;

    protected override void OnShow()
    {
        _modeBtn.Text = ModeLabel();
        _first.CallDeferred(Control.MethodName.GrabFocus);
    }

    protected override void Back() => Ctrl.ResumeGame();

    // Left/Right switch game mode while the Mode row is focused; everything else falls through to the
    // base (which handles the back keys).
    public override void _Input(InputEvent ev)
    {
        if (Visible && _modeBtn.HasFocus() && ev is InputEventKey { Pressed: true } k
            && k.Keycode is Key.Left or Key.Right)
        {
            Ctrl.CycleGameMode(k.Keycode == Key.Right ? 1 : -1);
            _modeBtn.Text = ModeLabel();
            GetViewport().SetInputAsHandled();
            return;
        }
        base._Input(ev);
    }

    private string ModeLabel() => $"‹  Mode:  {Ctrl.GameModeName}  ›";

    protected override void Build()
    {
        VBoxContainer v = CenteredPanel(Vector2.Zero, pad: 28, sep: 8);

        v.AddChild(UiTheme.Title("PAUSED", 38));
        v.AddChild(new HSeparator());

        _first = UiTheme.MenuItem("Resume", () => Ctrl.ResumeGame(), 320f);
        v.AddChild(_first);
        v.AddChild(UiTheme.MenuItem("Restart", () => Ctrl.RestartRace(), 320f));

        _modeBtn = UiTheme.MenuItem(ModeLabel(), () => { Ctrl.CycleGameMode(1); _modeBtn.Text = ModeLabel(); }, 320f);
        _modeBtn.Alignment = HorizontalAlignment.Center;
        v.AddChild(_modeBtn);

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.MenuItem("Levels", () => Ctrl.OpenLevels(fromPause: true), 320f));
        v.AddChild(UiTheme.MenuItem("Watch best lap", () => Ctrl.WatchBest(Ctrl.LevelIndex), 320f));
        v.AddChild(UiTheme.MenuItem("Settings", () => Ctrl.OpenSettings(fromPause: true), 320f));
        v.AddChild(UiTheme.MenuItem("Controls", () => Ctrl.OpenHelp(), 320f));

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.MenuItem("Main menu", () => Ctrl.OpenMain(), 320f));
        v.AddChild(UiTheme.MenuItem("Exit game", () => GetTree().Quit(), 320f));

        v.AddChild(UiTheme.Body("‹ ›  switch mode on the Mode row", UiTheme.TextDim, 13));
    }
}
