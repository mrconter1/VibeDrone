using Godot;

// One-time migration from the old index-keyed files (laptimes_0.json, ghost_0.json, gates_0.json)
// to the new stable-id scheme (laptimes_circuit.json, ..., levels/circuit.json). Copies each record
// only if the new file does not already exist, so it is safe to run every boot and never clobbers.
public static class LegacyMigration
{
    private static readonly (int Index, string Id)[] Map =
    {
        (0, "circuit"), (1, "big-oval"), (2, "technical"), (3, "serpentine"), (4, "maelstrom"),
    };

    public static void Run()
    {
        foreach (var (index, id) in Map)
        {
            CopyIfNew($"user://laptimes_{index}.json", $"user://laptimes_{id}.json");
            CopyIfNew($"user://ghost_{index}.json", $"user://ghost_{id}.json");
            MigrateGates(index, id);
        }
    }

    private static void CopyIfNew(string from, string to)
    {
        if (!FileAccess.FileExists(from) || FileAccess.FileExists(to)) return;
        using var src = FileAccess.Open(from, FileAccess.ModeFlags.Read);
        if (src == null) return;
        string text = src.GetAsText();
        using var dst = FileAccess.Open(to, FileAccess.ModeFlags.Write);
        dst?.StoreString(text);
    }

    // Old gate overrides ({"gates":[...]}) become the level's gates in a saved level file.
    private static void MigrateGates(int index, string id)
    {
        string oldPath = $"user://gates_{index}.json";
        if (!FileAccess.FileExists(oldPath)) return;
        if (FileAccess.FileExists($"user://levels/{id}.json")) return;
        if (!Persistence.TryLoad(oldPath, out Variant v) || v.VariantType != Variant.Type.Dictionary) return;
        var root = v.AsGodotDictionary();
        if (!root.ContainsKey("gates")) return;

        Level lvl = LevelStore.Load(id);   // base level from code
        lvl.Gates.Clear();
        foreach (Variant gv in root["gates"].AsGodotArray())
        {
            var gd = gv.AsGodotDictionary();
            lvl.Gates.Add(new Pose { Pos = Persistence.ReadPos(gd), Rot = Persistence.ReadRot(gd) });
        }
        LevelStore.Save(lvl);
    }
}
