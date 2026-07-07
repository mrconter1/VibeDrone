using Godot;

// In-game pause menu. Esc/Space/Backspace resume. Navigation routes through DroneController -> the
// ScreenCoordinator.
public partial class PauseMenu : MenuScreen
{
    private Button _first = null!;

    protected override void OnShow() => _first.CallDeferred(Control.MethodName.GrabFocus);
    protected override void Back() => Ctrl.ResumeGame();

    protected override void Build()
    {
        VBoxContainer v = CenteredPanel(new Vector2(420, 660), pad: 28, sep: 8);

        v.AddChild(UiTheme.Title("PAUSED", 38));
        v.AddChild(new HSeparator());

        _first = UiTheme.MenuItem("Resume", () => Ctrl.ResumeGame(), 300f);
        v.AddChild(_first);
        v.AddChild(UiTheme.MenuItem("Restart lap", () => Ctrl.RestartRace(), 300f));
        v.AddChild(UiTheme.MenuItem("Levels", () => Ctrl.OpenLevels(fromPause: true), 300f));
        v.AddChild(UiTheme.MenuItem("Watch best lap", () => Ctrl.WatchBest(Ctrl.LevelIndex), 300f));
        v.AddChild(UiTheme.MenuItem("Settings", () => Ctrl.OpenSettings(fromPause: true), 300f));
        v.AddChild(UiTheme.MenuItem("Controls", () => Ctrl.OpenHelp(), 300f));
        v.AddChild(UiTheme.MenuItem("Main menu", () => Ctrl.OpenMain(), 300f));
        v.AddChild(UiTheme.MenuItem("Exit game", () => GetTree().Quit(), 300f));
    }
}
