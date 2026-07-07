using Godot;

// Hides the mouse cursor in the menus until the mouse moves, then hides it again after a short idle.
// Only acts while the cursor is in "menu" mode (Visible/Hidden) - during gameplay it is Captured and
// this stays out of the way. Runs while paused (ProcessMode.Always).
public partial class CursorAutoHide : Node
{
    [Export] public float IdleHide = 2.0f;   // seconds of no motion before the cursor hides

    private float _idle;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventMouseMotion && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            _idle = 0f;
            if (Input.MouseMode == Input.MouseModeEnum.Hidden) Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public override void _Process(double delta)
    {
        if (Input.MouseMode != Input.MouseModeEnum.Visible) return;   // gameplay (Captured) or already hidden
        _idle += (float)delta;
        if (_idle > IdleHide) Input.MouseMode = Input.MouseModeEnum.Hidden;
    }
}
