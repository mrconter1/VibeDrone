using Godot;

// Centre-screen reticle for edit mode: a white ring, green when a movable object is
// under it, orange while carrying one.
public partial class EditReticle : Control
{
    public bool Highlight;   // an object is under the reticle
    public bool Grabbing;    // currently carrying an object

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        Vector2 c = Size * 0.5f;
        Color col = Grabbing ? new Color(1f, 0.7f, 0.2f)
                  : Highlight ? new Color(0.3f, 1f, 0.4f)
                  : new Color(1f, 1f, 1f, 0.75f);
        DrawArc(c, 9f, 0f, Mathf.Tau, 40, col, 2f, true);   // ring
        DrawArc(c, 1.5f, 0f, Mathf.Tau, 10, col, 2f, true); // centre dot
    }
}
