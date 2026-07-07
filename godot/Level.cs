using System.Collections.Generic;
using Godot;

// A level is data: metadata, ground, the ordered gates (gate 0 = start/finish) and a list of props
// (decorative/obstacle objects). Built-ins are defined in code (LevelStore) and player edits are
// saved as JSON in user://levels/<id>.json. Kept engine-agnostic-ish: only Godot math types, and
// all (de)serialization goes through Persistence so the on-disk shape lives in one place.
public sealed class Level
{
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;
    public string Id = "";
    public string Name = "";
    public GroundDef Ground = new();
    public List<Pose> Gates = new();
    public List<Prop> Props = new();

    public Godot.Collections.Dictionary ToDict()
    {
        var gates = new Godot.Collections.Array();
        foreach (Pose g in Gates) gates.Add(Persistence.PoseDict(g.Pos, g.Rot));
        var props = new Godot.Collections.Array();
        foreach (Prop p in Props) props.Add(p.ToDict());
        return new Godot.Collections.Dictionary
        {
            { "version", Version }, { "id", Id }, { "name", Name },
            { "ground", Ground.ToDict() },
            { "gates", gates }, { "props", props },
        };
    }

    public static Level FromDict(Godot.Collections.Dictionary d)
    {
        var lvl = new Level
        {
            Version = d.ContainsKey("version") ? d["version"].AsInt32() : 1,
            Id = d.ContainsKey("id") ? d["id"].AsString() : "",
            Name = d.ContainsKey("name") ? d["name"].AsString() : "",
            Ground = d.ContainsKey("ground") ? GroundDef.FromDict(d["ground"].AsGodotDictionary()) : new GroundDef(),
        };
        if (d.ContainsKey("gates"))
            foreach (Variant v in d["gates"].AsGodotArray())
            {
                var gd = v.AsGodotDictionary();
                lvl.Gates.Add(new Pose { Pos = Persistence.ReadPos(gd), Rot = Persistence.ReadRot(gd) });
            }
        if (d.ContainsKey("props"))
            foreach (Variant v in d["props"].AsGodotArray())
                lvl.Props.Add(Prop.FromDict(v.AsGodotDictionary()));
        return lvl;
    }

    public Level Clone()
    {
        var c = new Level { Version = Version, Id = Id, Name = Name, Ground = new GroundDef { Color = Ground.Color } };
        foreach (Pose g in Gates) c.Gates.Add(new Pose { Pos = g.Pos, Rot = g.Rot });
        foreach (Prop p in Props) c.Props.Add(p.Clone());
        return c;
    }
}

public struct Pose { public Vector3 Pos; public Quaternion Rot; }

// The environment floor. Only a colour today; the socket for real terrain later (height, type, ...).
public sealed class GroundDef
{
    public Color Color = new(0.13f, 0.16f, 0.20f);   // matches the default grid ground base

    public Godot.Collections.Dictionary ToDict() => new() { { "color", ColorArr(Color) } };

    public static GroundDef FromDict(Godot.Collections.Dictionary d) =>
        new() { Color = d.ContainsKey("color") ? ArrColor(d["color"].AsGodotArray()) : new Color(0.13f, 0.16f, 0.20f) };

    public static Godot.Collections.Array ColorArr(Color c) => new() { c.R, c.G, c.B };
    public static Color ArrColor(Godot.Collections.Array a) =>
        a.Count >= 3 ? new Color(a[0].AsSingle(), a[1].AsSingle(), a[2].AsSingle()) : Colors.Gray;
}

// A placed object: a registered type, a full transform (position/rotation/scale = resizable) and a
// tint colour. Extend by registering a new type in PropTypes - the format never changes.
public sealed class Prop
{
    public string Type = "rock";
    public Vector3 Pos;
    public Quaternion Rot = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;
    public Color Color = new(0.42f, 0.42f, 0.44f);
    public bool Solid = true;   // collidable obstacle (hitting it resets the lap); false = decorative

    public Godot.Collections.Dictionary ToDict() => new()
    {
        { "type", Type },
        { "pos", new Godot.Collections.Array { Pos.X, Pos.Y, Pos.Z } },
        { "rot", new Godot.Collections.Array { Rot.X, Rot.Y, Rot.Z, Rot.W } },
        { "scale", new Godot.Collections.Array { Scale.X, Scale.Y, Scale.Z } },
        { "color", GroundDef.ColorArr(Color) },
        { "solid", Solid },
    };

    public static Prop FromDict(Godot.Collections.Dictionary d)
    {
        var p = new Prop { Type = d.ContainsKey("type") ? d["type"].AsString() : "rock" };
        if (d.ContainsKey("pos")) { var a = d["pos"].AsGodotArray(); p.Pos = new Vector3(a[0].AsSingle(), a[1].AsSingle(), a[2].AsSingle()); }
        if (d.ContainsKey("rot")) { var a = d["rot"].AsGodotArray(); p.Rot = new Quaternion(a[0].AsSingle(), a[1].AsSingle(), a[2].AsSingle(), a[3].AsSingle()); }
        if (d.ContainsKey("scale")) { var a = d["scale"].AsGodotArray(); p.Scale = new Vector3(a[0].AsSingle(), a[1].AsSingle(), a[2].AsSingle()); }
        if (d.ContainsKey("color")) p.Color = GroundDef.ArrColor(d["color"].AsGodotArray());
        if (d.ContainsKey("solid")) p.Solid = d["solid"].AsBool();
        return p;
    }

    public Prop Clone() => new() { Type = Type, Pos = Pos, Rot = Rot, Scale = Scale, Color = Color, Solid = Solid };
}
