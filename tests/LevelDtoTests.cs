using Xunit;

// Serialization is what protects saved levels + records, so pin the on-disk JSON contract and the
// roundtrip. These run without Godot: LevelDto is the engine-free layer Level maps to/from.
public class LevelDtoTests
{
    private static LevelDto Sample()
    {
        return new LevelDto
        {
            version = 1, id = "circuit", name = "Circuit",
            ground = new GroundDto { color = new[] { 0.1f, 0.2f, 0.3f } },
            gates = new[]
            {
                new GateDto { x = 0, y = 8, z = 20, qx = 0, qy = 0.3517f, qz = 0, qw = 0.9361f },
                new GateDto { x = 28, y = 8, z = 52, qw = 1 },
            },
            props = new[]
            {
                new PropDto { type = "rock", pos = new[] { 12f, 0f, 40f }, scale = new[] { 3f, 2f, 3f },
                              color = new[] { 0.4f, 0.4f, 0.42f }, solid = true },
            },
        };
    }

    [Fact]
    public void Roundtrips_losslessly_through_json()
    {
        LevelDto a = Sample();
        LevelDto b = LevelDto.FromJson(a.ToJson());

        Assert.Equal(a.id, b.id);
        Assert.Equal(a.name, b.name);
        Assert.Equal(a.version, b.version);
        Assert.Equal(a.ground.color, b.ground.color);
        Assert.Equal(a.gates.Length, b.gates.Length);
        Assert.Equal(a.gates[0].qw, b.gates[0].qw, 4);
        Assert.Equal(a.gates[0].z, b.gates[0].z);
        Assert.Equal(a.props.Length, b.props.Length);
        Assert.Equal("rock", b.props[0].type);
        Assert.Equal(a.props[0].scale, b.props[0].scale);
        Assert.Equal(a.props[0].color, b.props[0].color);
        Assert.True(b.props[0].solid);
    }

    [Fact]
    public void Empty_json_gives_safe_defaults()
    {
        LevelDto d = LevelDto.FromJson("{}");
        Assert.NotNull(d.gates);
        Assert.Empty(d.gates);
        Assert.NotNull(d.props);
        Assert.Empty(d.props);
        Assert.Equal(1, d.version);
        Assert.NotNull(d.ground);
    }

    [Fact]
    public void Reads_the_on_disk_shape()
    {
        // the exact JSON LevelStore writes - guards the contract against accidental format drift
        const string json = @"{
            ""version"":1, ""id"":""x"", ""name"":""X"",
            ""ground"":{""color"":[0.5,0.5,0.5]},
            ""gates"":[{""x"":1,""y"":2,""z"":3,""qx"":0,""qy"":0,""qz"":0,""qw"":1}],
            ""props"":[{""type"":""rock"",""pos"":[4,5,6],""rot"":[0,0,0,1],""scale"":[2,2,2],""color"":[0.3,0.3,0.3],""solid"":false}]
        }";
        LevelDto d = LevelDto.FromJson(json);
        Assert.Equal("x", d.id);
        Assert.Single(d.gates);
        Assert.Equal(3f, d.gates[0].z);
        Assert.Single(d.props);
        Assert.Equal(new[] { 4f, 5f, 6f }, d.props[0].pos);
        Assert.False(d.props[0].solid);
    }

    [Fact]
    public void Missing_solid_defaults_true_for_old_files()
    {
        // props saved before the Solid flag existed must load as collidable (the default)
        LevelDto d = LevelDto.FromJson(@"{""props"":[{""type"":""rock""}]}");
        Assert.Single(d.props);
        Assert.True(d.props[0].solid);
    }

    [Fact]
    public void Is_case_insensitive_on_read()
    {
        LevelDto d = LevelDto.FromJson(@"{""ID"":""y"",""Name"":""Y""}");
        Assert.Equal("y", d.id);
        Assert.Equal("Y", d.name);
    }
}
