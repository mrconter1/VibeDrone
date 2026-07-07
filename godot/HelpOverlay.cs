using Godot;

// Controls cheat-sheet. Shown from the pause menu (or H); any key returns. Above the pause menu.
public partial class HelpOverlay : MenuScreen
{
    private static readonly (string, string)[] Bindings =
    {
        ("Throttle", "W / S   (or pad throttle)"),
        ("Roll", "Left / Right   (or pad)"),
        ("Pitch", "Up / Down   (or pad)"),
        ("Yaw", "Q / C   (or pad)"),
        ("Restart race", "R"),
        ("Pause menu", "Esc"),
        ("This help", "H"),
        ("Edit mode", "E   (free-fly; C grabs, G rock, [ ] size, V colour, Del delete)"),
        ("Sound test", "M"),
    };

    protected override int LayerNum => 12;
    protected override bool WantsBack(InputEvent ev) => ev is InputEventKey { Pressed: true };   // any key
    protected override void Back() => Ctrl.CloseHelp();

    protected override void Build()
    {
        VBoxContainer v = CenteredPanel(new Vector2(600, 470), sep: 10);

        v.AddChild(UiTheme.Title("CONTROLS", 38));
        v.AddChild(new HSeparator());

        foreach (var (action, keys) in Bindings)
        {
            var row = new HBoxContainer();
            row.AddChild(UiTheme.Body(action, UiTheme.Text, 17));
            row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(24, 0) });
            row.AddChild(UiTheme.Body(keys, UiTheme.Accent, 16));
            v.AddChild(row);
        }

        v.AddChild(new HSeparator());
        v.AddChild(UiTheme.MenuItem("‹  Back", () => Ctrl.CloseHelp(), 200f));
        v.AddChild(UiTheme.Body("press any key to close", UiTheme.TextDim, 15));
    }
}
