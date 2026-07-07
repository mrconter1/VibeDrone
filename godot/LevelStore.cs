using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

// The catalogue of levels: the built-ins defined here plus any player-created levels found in
// user://levels/. Built-ins have stable code ids and their gates come from TrackBuilder; user levels
// are self-contained files. Load(id) returns the saved edit if present, else a fresh build from the
// code definition. Ids are stable strings so records never depend on list order.
public static class LevelStore
{
    private static readonly Color DefaultGround = new(0.13f, 0.16f, 0.20f);

    private sealed record Def(string Id, string Name, Func<Transform3D[]> Gates);

    private static readonly Def[] BuiltIns =
    {
        new("circuit", "Circuit", () => TrackBuilder.Flat(new Vector2[]
        {
            new(0, 20), new(28, 52), new(16, 92), new(-26, 88), new(-32, 48), new(-12, 22),
        })),
        new("big-oval", "Big Oval", () => TrackBuilder.Flat(new Vector2[]
        {
            new(0, 20), new(50, 45), new(55, 95), new(0, 120), new(-55, 95), new(-50, 45),
        })),
        new("technical", "Technical", () => TrackBuilder.Flat(new Vector2[]
        {
            new(0, 15), new(18, 30), new(10, 55), new(24, 72),
            new(2, 80), new(-20, 66), new(-14, 40), new(-20, 20),
        })),
        new("serpentine", "Serpentine", () => TrackBuilder.Generated(seed: 1337, gateCount: 10)),
        new("maelstrom", "Maelstrom", () => TrackBuilder.Generated(seed: 8675309, gateCount: 12, radius: 72f, heightAmp: 9f)),
    };

    private static readonly HashSet<string> BuiltInIds = new(BuiltIns.Select(d => d.Id));

    private readonly record struct Entry(string Id, string Name, bool BuiltIn);
    private static List<Entry> _catalogue = new();

    static LevelStore() => Refresh();

    // Rebuild the catalogue: built-ins first, then user levels scanned from user://levels/.
    public static void Refresh()
    {
        var list = new List<Entry>();
        foreach (Def d in BuiltIns) list.Add(new Entry(d.Id, d.Name, true));

        foreach (string path in UserLevelFiles())
        {
            if (!Persistence.TryReadText(path, out string json)) continue;
            try
            {
                Level lvl = Level.FromJson(json);
                if (!string.IsNullOrEmpty(lvl.Id) && !BuiltInIds.Contains(lvl.Id))   // skip built-in overlays
                    list.Add(new Entry(lvl.Id, string.IsNullOrEmpty(lvl.Name) ? lvl.Id : lvl.Name, false));
            }
            catch { /* skip corrupt file */ }
        }
        _catalogue = list;
    }

    public static int Count => _catalogue.Count;
    public static string IdAt(int i) => _catalogue[Wrap(i)].Id;
    public static string NameAt(int i) => _catalogue[Wrap(i)].Name;
    public static bool IsBuiltIn(int i) => _catalogue[Wrap(i)].BuiltIn;
    public static int Wrap(int i) => Count == 0 ? 0 : ((i % Count) + Count) % Count;

    public static int IndexOf(string id)
    {
        for (int i = 0; i < _catalogue.Count; i++) if (_catalogue[i].Id == id) return i;
        return 0;
    }

    private static string PathFor(string id) => $"user://levels/{id}.json";

    // The current state of a level: the saved file if present, else a fresh build from code.
    public static Level Load(string id)
    {
        if (Persistence.TryReadText(PathFor(id), out string json))
        {
            try
            {
                Level saved = Level.FromJson(json);
                if (saved.Gates.Count > 0) { saved.Id = id; return saved; }
            }
            catch { /* corrupt -> fall back (built-ins only) */ }
        }
        return BuildBase(id);
    }

    public static Level Load(int index) => Load(IdAt(index));

    public static void Save(Level lvl)
    {
        EnsureDir();
        Persistence.WriteText(PathFor(lvl.Id), lvl.ToJson());
    }

    // Create a new user level with a starter gate loop; returns its catalogue index.
    public static int Create()
    {
        EnsureDir();
        int n = _catalogue.Count(e => !e.BuiltIn) + 1;
        string name = $"Custom {n}";
        string id = UniqueId(name);
        var lvl = new Level { Id = id, Name = name, Ground = new GroundDef { Color = DefaultGround } };
        foreach (Transform3D t in StarterGates())
            lvl.Gates.Add(new Pose { Pos = t.Origin, Rot = t.Basis.GetRotationQuaternion() });
        Save(lvl);
        Refresh();
        return IndexOf(id);
    }

    // Delete a user level and its records (built-ins can't be deleted).
    public static void Delete(string id)
    {
        if (BuiltInIds.Contains(id)) return;
        RemoveFile(PathFor(id));
        RemoveFile($"user://laptimes_{id}.json");
        RemoveFile($"user://ghost_{id}.json");
        Refresh();
    }

    private static Level BuildBase(string id)
    {
        Def def = BuiltIns.FirstOrDefault(d => d.Id == id) ?? BuiltIns[0];
        var lvl = new Level { Id = def.Id, Name = def.Name, Ground = new GroundDef { Color = DefaultGround } };
        foreach (Transform3D t in def.Gates())
            lvl.Gates.Add(new Pose { Pos = t.Origin, Rot = t.Basis.GetRotationQuaternion() });
        return lvl;
    }

    // A small, immediately-raceable loop for a fresh level (the pilot then reshapes/decorates it).
    private static Transform3D[] StarterGates() => TrackBuilder.Flat(new Vector2[]
    {
        new(0, 20), new(30, 42), new(20, 82), new(-20, 82), new(-30, 42),
    });

    private static string[] UserLevelFiles()
    {
        if (!DirAccess.DirExistsAbsolute("user://levels")) return Array.Empty<string>();
        return DirAccess.GetFilesAt("user://levels")
            .Where(f => f.EndsWith(".json"))
            .Select(f => $"user://levels/{f}")
            .ToArray();
    }

    private static string UniqueId(string name)
    {
        string baseId = "user-" + Slug(name);
        string id = baseId;
        int n = 2;
        while (FileAccess.FileExists(PathFor(id))) id = $"{baseId}-{n++}";
        return id;
    }

    private static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-');
    }

    private static void EnsureDir()
    {
        if (!DirAccess.DirExistsAbsolute("user://levels")) DirAccess.MakeDirAbsolute("user://levels");
    }

    private static void RemoveFile(string path)
    {
        if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(path);
    }
}
