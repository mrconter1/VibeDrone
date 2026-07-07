using System;
using Godot;

// The catalogue of levels. Built-ins are defined here (id + name + gate layout + default ground);
// gates still come from TrackBuilder (authored flat loops or seeded 3D generators). Load(id) returns
// the saved level from user://levels/<id>.json if the player has edited it, otherwise a fresh copy
// built from the code definition. Ids are stable strings so records never depend on list order.
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

    public static int Count => BuiltIns.Length;
    public static string IdAt(int i) => BuiltIns[Wrap(i)].Id;
    public static string NameAt(int i) => BuiltIns[Wrap(i)].Name;
    public static int Wrap(int i) => ((i % Count) + Count) % Count;

    public static int IndexOf(string id)
    {
        for (int i = 0; i < BuiltIns.Length; i++) if (BuiltIns[i].Id == id) return i;
        return 0;
    }

    private static string PathFor(string id) => $"user://levels/{id}.json";

    // The current state of a level: the saved edit if present, else a fresh build from code.
    public static Level Load(string id)
    {
        if (Persistence.TryLoad(PathFor(id), out Variant v) && v.VariantType == Variant.Type.Dictionary)
        {
            Level saved = Level.FromDict(v.AsGodotDictionary());
            if (saved.Gates.Count > 0) { saved.Id = id; return saved; }
        }
        return BuildBase(id);
    }

    public static Level Load(int index) => Load(IdAt(index));

    public static void Save(Level lvl)
    {
        EnsureDir();
        Persistence.Save(PathFor(lvl.Id), lvl.ToDict());
    }

    private static Level BuildBase(string id)
    {
        Def def = BuiltIns[IndexOf(id)];
        var lvl = new Level { Id = def.Id, Name = def.Name, Ground = new GroundDef { Color = DefaultGround } };
        foreach (Transform3D t in def.Gates())
            lvl.Gates.Add(new Pose { Pos = t.Origin, Rot = t.Basis.GetRotationQuaternion() });
        return lvl;
    }

    private static void EnsureDir()
    {
        if (!DirAccess.DirExistsAbsolute("user://levels"))
            DirAccess.MakeDirAbsolute("user://levels");
    }
}
