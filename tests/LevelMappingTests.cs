using Godot;
using Xunit;

// Level <-> DTO <-> JSON mapping with real Godot math types (Vector3/Quaternion/Color work without
// the engine). This covers the layer LevelDtoTests can't: the Godot-side packing/unpacking.
public class LevelMappingTests
{
    private static Level Sample()
    {
        var l = new Level { Id = "lvl", Name = "My Level", Version = 1 };
        l.Ground.Color = new Color(0.1f, 0.2f, 0.3f);
        l.Gates.Add(new Pose { Pos = new Vector3(0, 8, 20), Rot = Quaternion.Identity });
        l.Gates.Add(new Pose { Pos = new Vector3(5, 8, 40), Rot = new Quaternion(Vector3.Up, 0.5f) });
        l.Props.Add(new Prop { Type = "rock", Pos = new Vector3(12, 0, 40), Rot = new Quaternion(Vector3.Right, 0.3f), Scale = new Vector3(3, 2, 4), Color = new Color(0.4f, 0.4f, 0.42f), Solid = true });
        l.Props.Add(new Prop { Type = "rock", Pos = new Vector3(-8, 1, 30), Scale = new Vector3(2, 2, 2), Color = new Color(0.5f, 0.3f, 0.2f), Solid = false });
        return l;
    }

    private static void EqVec(Vector3 a, Vector3 b)
    {
        Assert.Equal(a.X, b.X, 4); Assert.Equal(a.Y, b.Y, 4); Assert.Equal(a.Z, b.Z, 4);
    }

    private static void EqQuat(Quaternion a, Quaternion b)
    {
        Assert.Equal(a.X, b.X, 4); Assert.Equal(a.Y, b.Y, 4); Assert.Equal(a.Z, b.Z, 4); Assert.Equal(a.W, b.W, 4);
    }

    [Fact]
    public void Json_roundtrip_preserves_gates_props_ground()
    {
        Level a = Sample();
        Level b = Level.FromJson(a.ToJson());

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.Version, b.Version);
        Assert.Equal(a.Gates.Count, b.Gates.Count);
        Assert.Equal(a.Props.Count, b.Props.Count);

        EqVec(a.Gates[1].Pos, b.Gates[1].Pos);
        EqQuat(a.Gates[1].Rot, b.Gates[1].Rot);
        EqVec(a.Props[0].Pos, b.Props[0].Pos);
        EqQuat(a.Props[0].Rot, b.Props[0].Rot);
        EqVec(a.Props[0].Scale, b.Props[0].Scale);
        Assert.Equal(a.Props[0].Color.R, b.Props[0].Color.R, 4);
        Assert.True(b.Props[0].Solid);
        Assert.False(b.Props[1].Solid);
        Assert.Equal(0.2f, b.Ground.Color.G, 4);
    }

    [Fact]
    public void Dto_roundtrip_matches_json_roundtrip()
    {
        Level a = Sample();
        Level b = Level.FromDto(a.ToDto());
        EqVec(a.Props[1].Pos, b.Props[1].Pos);
        Assert.Equal(a.Props[1].Type, b.Props[1].Type);
    }

    [Fact]
    public void Clone_is_a_deep_copy()
    {
        Level a = Sample();
        Level c = a.Clone();
        c.Props[0].Pos = new Vector3(99, 99, 99);
        c.Gates[0] = new Pose { Pos = new Vector3(1, 1, 1), Rot = Quaternion.Identity };
        c.Ground.Color = Colors.Red;

        Assert.NotEqual(99f, a.Props[0].Pos.X);       // original untouched
        Assert.NotEqual(1f, a.Gates[0].Pos.X);
        Assert.NotEqual(1f, a.Ground.Color.R);
    }

    [Fact]
    public void Prop_clone_is_independent()
    {
        var p = new Prop { Pos = new Vector3(1, 2, 3), Solid = false };
        Prop q = p.Clone();
        q.Pos = new Vector3(9, 9, 9);
        q.Solid = true;
        EqVec(new Vector3(1, 2, 3), p.Pos);
        Assert.False(p.Solid);
    }

    [Fact]
    public void Empty_json_gives_an_empty_level()
    {
        Level l = Level.FromJson("{}");
        Assert.Empty(l.Gates);
        Assert.Empty(l.Props);
        Assert.Equal(1, l.Version);
    }

    [Fact]
    public void Missing_solid_loads_as_collidable()
    {
        Level l = Level.FromJson(@"{""props"":[{""type"":""rock"",""pos"":[1,2,3]}]}");
        Assert.Single(l.Props);
        Assert.True(l.Props[0].Solid);
    }

    [Fact]
    public void New_prop_has_sensible_defaults()
    {
        var p = new Prop();
        Assert.Equal("rock", p.Type);
        Assert.True(p.Solid);
        EqVec(Vector3.One, p.Scale);
        EqQuat(Quaternion.Identity, p.Rot);
    }

    [Fact]
    public void Ground_defaults_to_the_grid_colour()
    {
        var g = new GroundDef();
        Assert.Equal(0.13f, g.Color.R, 4);
        Assert.Equal(0.16f, g.Color.G, 4);
        Assert.Equal(0.20f, g.Color.B, 4);
    }

    [Fact]
    public void Many_props_survive_the_roundtrip()
    {
        var a = new Level { Id = "big", Name = "Big" };
        for (int i = 0; i < 50; i++)
            a.Props.Add(new Prop { Pos = new Vector3(i, i * 0.5f, -i), Scale = new Vector3(1 + i * 0.1f, 2, 3) });
        Level b = Level.FromJson(a.ToJson());
        Assert.Equal(50, b.Props.Count);
        EqVec(a.Props[49].Pos, b.Props[49].Pos);
        EqVec(a.Props[49].Scale, b.Props[49].Scale);
    }
}
