using Xunit;

// Serialization robustness for the on-disk level format.
public class LevelDtoExtraTests
{
    [Fact]
    public void Unknown_fields_are_ignored()
    {
        LevelDto d = LevelDto.FromJson(@"{""id"":""x"",""author"":""nobody"",""difficulty"":5,""gates"":[]}");
        Assert.Equal("x", d.id);
        Assert.Empty(d.gates);
    }

    [Fact]
    public void Unicode_names_survive()
    {
        var a = new LevelDto { id = "u", name = "Ævar's Løøp 龍" };
        LevelDto b = LevelDto.FromJson(a.ToJson());
        Assert.Equal("Ævar's Løøp 龍", b.name);
    }

    [Fact]
    public void Large_and_negative_coordinates_survive()
    {
        var a = new LevelDto
        {
            props = new[] { new PropDto { pos = new[] { -12345.5f, 99999f, -0.0001f }, scale = new[] { 0.01f, 40f, 1f } } },
        };
        LevelDto b = LevelDto.FromJson(a.ToJson());
        Assert.Equal(-12345.5f, b.props[0].pos[0], 2);
        Assert.Equal(99999f, b.props[0].pos[1], 1);
        Assert.Equal(40f, b.props[0].scale[1], 3);
    }

    [Fact]
    public void Solid_false_survives_the_roundtrip()
    {
        var a = new LevelDto { props = new[] { new PropDto { solid = false } } };
        LevelDto b = LevelDto.FromJson(a.ToJson());
        Assert.False(b.props[0].solid);
    }

    [Fact]
    public void Missing_gates_and_props_become_empty_not_null()
    {
        LevelDto d = LevelDto.FromJson(@"{""id"":""y""}");
        Assert.NotNull(d.gates);
        Assert.NotNull(d.props);
        Assert.Empty(d.gates);
        Assert.Empty(d.props);
    }

    [Fact]
    public void A_hundred_gates_roundtrip()
    {
        var a = new LevelDto { gates = new GateDto[100] };
        for (int i = 0; i < 100; i++) a.gates[i] = new GateDto { x = i, y = 8, z = i * 2, qw = 1 };
        LevelDto b = LevelDto.FromJson(a.ToJson());
        Assert.Equal(100, b.gates.Length);
        Assert.Equal(99f, b.gates[99].x, 3);
        Assert.Equal(198f, b.gates[99].z, 3);
    }

    [Fact]
    public void Whitespace_and_pretty_printing_parse()
    {
        LevelDto d = LevelDto.FromJson("{\n  \"id\" : \"z\" ,\n  \"gates\" : [ ]\n}");
        Assert.Equal("z", d.id);
    }
}
