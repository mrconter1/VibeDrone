using System.Collections.Generic;
using Godot;

// A level is data: metadata, ground, the ordered gates (gate 0 = start/finish) and a list of props
// (decorative/obstacle objects). Built-ins are defined in code (LevelStore) and player edits are
// saved as JSON in user://levels/<id>.json. The Godot math types here map to/from a Godot-free DTO
// (LevelDto) which owns the JSON shape, so serialization can be unit-tested without the engine.
public sealed class Level
{
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;
    public string Id = "";
    public string Name = "";
    public GroundDef Ground = new();
    public List<Pose> Gates = new();
    public List<Prop> Props = new();

    public string ToJson() => ToDto().ToJson();
    public static Level FromJson(string json) => FromDto(LevelDto.FromJson(json));

    public LevelDto ToDto()
    {
        var dto = new LevelDto
        {
            version = Version, id = Id, name = Name,
            ground = new GroundDto { color = new[] { Ground.Color.R, Ground.Color.G, Ground.Color.B } },
            gates = new GateDto[Gates.Count],
            props = new PropDto[Props.Count],
        };
        for (int i = 0; i < Gates.Count; i++)
        {
            Pose g = Gates[i];
            dto.gates[i] = new GateDto { x = g.Pos.X, y = g.Pos.Y, z = g.Pos.Z, qx = g.Rot.X, qy = g.Rot.Y, qz = g.Rot.Z, qw = g.Rot.W };
        }
        for (int i = 0; i < Props.Count; i++)
        {
            Prop p = Props[i];
            dto.props[i] = new PropDto
            {
                type = p.Type, solid = p.Solid,
                pos = new[] { p.Pos.X, p.Pos.Y, p.Pos.Z },
                rot = new[] { p.Rot.X, p.Rot.Y, p.Rot.Z, p.Rot.W },
                scale = new[] { p.Scale.X, p.Scale.Y, p.Scale.Z },
                color = new[] { p.Color.R, p.Color.G, p.Color.B },
            };
        }
        return dto;
    }

    public static Level FromDto(LevelDto dto)
    {
        var lvl = new Level { Version = dto.version, Id = dto.id, Name = dto.name };
        if (dto.ground?.color is { Length: >= 3 } gc) lvl.Ground.Color = new Color(gc[0], gc[1], gc[2]);
        foreach (GateDto g in dto.gates ?? System.Array.Empty<GateDto>())
            lvl.Gates.Add(new Pose { Pos = new Vector3(g.x, g.y, g.z), Rot = new Quaternion(g.qx, g.qy, g.qz, g.qw) });
        foreach (PropDto p in dto.props ?? System.Array.Empty<PropDto>())
            lvl.Props.Add(new Prop
            {
                Type = p.type ?? "rock", Solid = p.solid,
                Pos = Vec3(p.pos), Rot = Quat(p.rot), Scale = Vec3(p.scale, 1f), Color = Col(p.color),
            });
        return lvl;
    }

    private static Vector3 Vec3(float[] a, float fallback = 0f) =>
        a is { Length: >= 3 } ? new Vector3(a[0], a[1], a[2]) : new Vector3(fallback, fallback, fallback);
    private static Quaternion Quat(float[] a) =>
        a is { Length: >= 4 } ? new Quaternion(a[0], a[1], a[2], a[3]) : Quaternion.Identity;
    private static Color Col(float[] a) =>
        a is { Length: >= 3 } ? new Color(a[0], a[1], a[2]) : new Color(0.42f, 0.42f, 0.44f);

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
    public Color Color = new(0.13f, 0.16f, 0.20f);
}

// A placed object: a registered type, a full transform (position/rotation/scale = resizable), a tint
// colour and a solid flag (collidable obstacle vs decoration). Extend by registering a new type in
// PropTypes - the format never changes.
public sealed class Prop
{
    public string Type = "rock";
    public Vector3 Pos;
    public Quaternion Rot = Quaternion.Identity;
    public Vector3 Scale = Vector3.One;
    public Color Color = new(0.42f, 0.42f, 0.44f);
    public bool Solid = true;

    public Prop Clone() => new() { Type = Type, Pos = Pos, Rot = Rot, Scale = Scale, Color = Color, Solid = Solid };
}
