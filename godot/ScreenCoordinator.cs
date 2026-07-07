using Godot;

// Owns the menu/screen state machine and the view orchestration that goes with it: which screen is
// visible, pause, cursor, the menu camera + frosted backdrop, and the menu-only AA/supersampling.
// The game (DroneController) delegates here so it can stay about flying + racing. Game-side verbs
// (load a level, start a replay, restart the race) are handed back through IGame.
public sealed class ScreenCoordinator
{
    public enum Screen { None, Main, Levels, Settings, Pause, Help }

    // What the coordinator needs the game to do for the level/replay verbs.
    public interface IGame
    {
        void LoadLevel(int index);
        void StartPlayback();
        void StartRace();
    }

    private readonly IGame _game;
    private readonly SceneTree _tree;
    private readonly Viewport _vp;
    private readonly Camera3D _droneCam;
    private readonly MainMenu _main;
    private readonly LevelSelect _levels;
    private readonly SettingsMenu _settings;
    private readonly PauseMenu _pause;
    private readonly HelpOverlay _help;
    private readonly MenuBackdrop _backdrop;
    private readonly MenuCamera _menuCam;
    private readonly Hud _hud;

    private Screen _screen = Screen.Main;
    private Screen _return = Screen.Main;   // where Levels/Settings/Help back out to

    public ScreenCoordinator(IGame game, SceneTree tree, Viewport vp, Camera3D droneCam,
        MainMenu main, LevelSelect levels, SettingsMenu settings, PauseMenu pause, HelpOverlay help,
        MenuBackdrop backdrop, MenuCamera menuCam, Hud hud)
    {
        _game = game; _tree = tree; _vp = vp; _droneCam = droneCam;
        _main = main; _levels = levels; _settings = settings; _pause = pause; _help = help;
        _backdrop = backdrop; _menuCam = menuCam; _hud = hud;
    }

    public bool MenuActive => _screen is Screen.Main or Screen.Levels or Screen.Settings;

    // Show one screen; drive pause, cursor, the menu camera/backdrop and menu supersampling from it.
    public void Show(Screen s)
    {
        _screen = s;
        _main.Show(s == Screen.Main);
        _levels.Show(s == Screen.Levels);
        _settings.Show(s == Screen.Settings);
        _pause.Show(s == Screen.Pause);
        _help.Show(s == Screen.Help);

        bool fullScreenMenu = s is Screen.Main or Screen.Levels or Screen.Settings;
        _backdrop.SetActive(fullScreenMenu);
        _menuCam.Active = fullScreenMenu;
        _hud.Visible = !fullScreenMenu;   // no stale race HUD behind the blurred title/menus
        ApplyMenuSsaa(fullScreenMenu);
        if (fullScreenMenu) _menuCam.MakeCurrent();
        else if (s == Screen.None) _droneCam.MakeCurrent();   // Pause/Help keep the (frozen) drone view

        _tree.Paused = s != Screen.None;
        // gameplay captures the cursor; menus start with it hidden (CursorAutoHide reveals it on move)
        Input.MouseMode = s == Screen.None ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Hidden;
    }

    // --- navigation verbs (called by the menus via DroneController) ---
    public void StartGame() => PlayLevel(0);
    public void OpenMain() => Show(Screen.Main);
    public void ResumeGame() => Show(Screen.None);
    public void OpenPause() => Show(Screen.Pause);
    public void OpenHelp() { _return = _screen; Show(Screen.Help); }
    public void CloseHelp() => Show(_return == Screen.Help ? Screen.Pause : _return);
    public void OpenLevels(bool fromPause) { _return = fromPause ? Screen.Pause : Screen.Main; Show(Screen.Levels); }
    public void OpenSettings(bool fromPause) { _return = fromPause ? Screen.Pause : Screen.Main; Show(Screen.Settings); }
    public void MenuBack() => Show(_return);
    public void RestartRace() { _game.StartRace(); Show(Screen.None); }
    public void PlayLevel(int index) { _game.LoadLevel(index); Show(Screen.None); }
    public void WatchBest(int index) { _game.LoadLevel(index); Show(Screen.None); _game.StartPlayback(); }

    // --- anti-aliasing (whole game) + menu supersampling ---
    public void ApplyAA()
    {
        _vp.Msaa3D = Config.Msaa switch
        {
            2 => Viewport.Msaa.Msaa2X,
            4 => Viewport.Msaa.Msaa4X,
            8 => Viewport.Msaa.Msaa8X,
            _ => Viewport.Msaa.Disabled,
        };
        _vp.ScreenSpaceAA = Config.Fxaa ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
    }

    public void RefreshSsaa() => ApplyMenuSsaa(MenuActive);

    // Render the 3D above native res and downscale (SSAA) while a menu is open - removes the checker/
    // thin-edge shimmer the slow orbit reveals, with no temporal artefacts, on the D3D12 backend.
    private void ApplyMenuSsaa(bool menu)
    {
        _vp.Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear;
        _vp.Scaling3DScale = menu && Config.MenuSsaa ? 1.5f : 1.0f;
    }
}
