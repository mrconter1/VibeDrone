using Godot;

// Dev hot-reload marker, used only under the StartDebug supervisor. Debug R writes a target and
// quits; the supervisor rebuilds the latest code and relaunches Godot, and on boot the controller
// reads the target back to decide where to resume: a level id resumes that level, "MAIN" opens the
// title screen. A no-op path in a normal (non-supervised) run.
public static class DevReload
{
    private const string MarkerPath = "user://dev_relaunch.txt";

    // Save the reload target and quit so the supervisor can rebuild + relaunch.
    public static void Request(SceneTree tree, string target)
    {
        Persistence.WriteText(MarkerPath, target);
        GD.Print($"[dev] reload requested (target='{target}') - quitting for the supervisor to rebuild");
        tree.Quit();
    }

    // Read + clear the marker; returns the resume target ("MAIN" or a level id), or "" if none.
    public static string Consume()
    {
        if (Persistence.TryReadText(MarkerPath, out string id) && id.Trim().Length > 0)
        {
            DirAccess.RemoveAbsolute(MarkerPath);
            return id.Trim();
        }
        return "";
    }
}
