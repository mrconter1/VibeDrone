using Godot;

// Esc pause menu: Resume, toggle Debug overlay, open Sound settings, or Exit.
// Pauses the game and shows the cursor while open. ProcessMode=Always (runs paused).
public partial class PauseMenu : CanvasLayer
{
    private DroneController _ctrl = null!;
    private SoundMenu _sound = null!;
    private Panel _panel = null!;
    private Button _debugBtn = null!;
    private bool _open;

    public void Setup(DroneController ctrl, SoundMenu sound) { _ctrl = ctrl; _sound = sound; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 11;
        BuildUi();
        _panel.Visible = false;
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            SetOpen(!_open);
            GetViewport().SetInputAsHandled();
        }
    }

    private void SetOpen(bool open)
    {
        _open = open;
        _panel.Visible = _open;
        GetTree().Paused = _open;
        Input.MouseMode = _open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    private void BuildUi()
    {
        Vector2I win = DisplayServer.WindowGetSize();
        _panel = new Panel { Size = new Vector2(300, 434), Position = new Vector2(win.X / 2f - 150, win.Y / 2f - 217) };
        _panel.SelfModulate = new Color(0.05f, 0.06f, 0.09f, 0.94f);
        AddChild(_panel);

        var v = new VBoxContainer { Position = new Vector2(24, 24), Size = new Vector2(252, 386) };
        v.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(v);

        v.AddChild(new Label { Text = "PAUSED" });

        AddButton(v, "Resume", () => SetOpen(false));

        _debugBtn = AddButton(v, DebugLabel(), () =>
        {
            _ctrl.SetShowDebug(!_ctrl.ShowDebug);
            _debugBtn.Text = DebugLabel();
        });

        AddButton(v, "Playback best lap", () => { SetOpen(false); _ctrl.StartPlayback(); });

        AddButton(v, "Sound settings", () => { SetOpen(false); _sound.SetOpen(true); });

        Button clearBtn = null!;
        clearBtn = AddButton(v, "Clear results", () => { _ctrl.ClearResults(); clearBtn.Text = "Cleared!"; });

        AddButton(v, "Exit game", () => GetTree().Quit());
    }

    private string DebugLabel() => "Debug overlay: " + (_ctrl.ShowDebug ? "ON" : "OFF");

    private static Button AddButton(Control parent, string text, System.Action onPressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(252, 40) };
        b.Pressed += onPressed;
        parent.AddChild(b);
        return b;
    }
}
