using Godot;

// A real 3D preview of a level for the Levels screen: an Arena (gates, props, ground, sky) built in
// its own SubViewport, framed by a camera that slowly orbits the track. Runs while the menu is paused
// (ProcessMode.Always) and renders continuously. SetLevel swaps in the focused level's geometry.
public partial class MapPreview3D : SubViewportContainer
{
    private SubViewport _vp = null!;
    private Arena _arena = null!;
    private Camera3D _cam = null!;

    private Vector3 _center;
    private float _radius = 30f;
    private float _yaw = 0.7f;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;

        _vp = new SubViewport
        {
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            TransparentBg = false,
            Msaa3D = Viewport.Msaa.Msaa2X,
        };
        AddChild(_vp);

        _arena = new Arena();
        _vp.AddChild(_arena);   // its own World3D: sun, sky, ground, gates, props

        _cam = new Camera3D { Fov = 50f, Current = true };
        _vp.AddChild(_cam);
    }

    public void SetLevel(Level lvl)
    {
        _arena.LoadLevel(lvl);
        if (lvl.Gates.Count == 0) return;

        Vector3 min = lvl.Gates[0].Pos, max = min;
        foreach (Pose g in lvl.Gates) { min = min.Min(g.Pos); max = max.Max(g.Pos); }
        foreach (Prop p in lvl.Props) { min = min.Min(p.Pos); max = max.Max(p.Pos); }
        _center = (min + max) * 0.5f;
        _radius = Mathf.Max((max - min).Length() * 0.5f, 10f);
        Place();
    }

    public override void _Process(double delta)
    {
        _yaw += (float)delta * 0.25f;   // slow orbit
        Place();
    }

    private void Place()
    {
        float dist = _radius * 1.35f + 10f;
        float height = _radius * 0.65f + 8f;
        _cam.GlobalPosition = _center + new Vector3(Mathf.Cos(_yaw) * dist, height, Mathf.Sin(_yaw) * dist);
        _cam.LookAt(_center + Vector3.Up * 3f, Vector3.Up);
    }
}
