using Godot;

// Shared JSON persistence helpers. Both the gate layout (Arena) and the ghost recording
// (LapRecorder) store poses as a flat position + quaternion dictionary and read/write it
// through user:// JSON, so that boilerplate lives here once.
public static class Persistence
{
    // A pose as { x,y,z, qx,qy,qz,qw }. Callers may add extra keys (e.g. a timestamp).
    public static Godot.Collections.Dictionary PoseDict(Vector3 pos, Quaternion rot) => new()
    {
        { "x", pos.X }, { "y", pos.Y }, { "z", pos.Z },
        { "qx", rot.X }, { "qy", rot.Y }, { "qz", rot.Z }, { "qw", rot.W },
    };

    public static Vector3 ReadPos(Godot.Collections.Dictionary d) =>
        new(d["x"].AsSingle(), d["y"].AsSingle(), d["z"].AsSingle());

    public static Quaternion ReadRot(Godot.Collections.Dictionary d) =>
        new(d["qx"].AsSingle(), d["qy"].AsSingle(), d["qz"].AsSingle(), d["qw"].AsSingle());

    // Write any Variant (Array/Dictionary) to a user:// path as JSON.
    public static void Save(string path, Variant data)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (f != null) f.StoreString(Json.Stringify(data));
    }

    // Load + parse a user:// JSON file. Returns false (data = default) if it is missing/unreadable.
    public static bool TryLoad(string path, out Variant data)
    {
        data = default;
        if (!FileAccess.FileExists(path)) return false;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return false;
        data = Json.ParseString(f.GetAsText());
        return true;
    }

    // Raw text I/O (used for level files, which serialise themselves via System.Text.Json).
    public static void WriteText(string path, string text)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f?.StoreString(text);
    }

    public static bool TryReadText(string path, out string text)
    {
        text = "";
        if (!FileAccess.FileExists(path)) return false;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return false;
        text = f.GetAsText();
        return true;
    }
}
