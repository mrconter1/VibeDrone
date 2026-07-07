using System;
using System.Text.Json;

// Godot-free serialization layer for levels. These DTOs mirror the on-disk JSON shape exactly (so
// existing files keep parsing) but use only primitives + System.Text.Json - no Godot types - so the
// format is unit-testable without the engine. Level maps its Godot math types to/from these.
public sealed class LevelDto
{
    public int version { get; set; } = 1;
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public GroundDto ground { get; set; } = new();
    public GateDto[] gates { get; set; } = Array.Empty<GateDto>();
    public PropDto[] props { get; set; } = Array.Empty<PropDto>();

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Opts);

    public static LevelDto FromJson(string json) => JsonSerializer.Deserialize<LevelDto>(json, Opts) ?? new LevelDto();
}

public sealed class GroundDto
{
    public float[] color { get; set; } = { 0.13f, 0.16f, 0.20f };
}

// Gate pose stored as flat x,y,z + quaternion, matching Persistence.PoseDict on disk.
public sealed class GateDto
{
    public float x { get; set; }
    public float y { get; set; }
    public float z { get; set; }
    public float qx { get; set; }
    public float qy { get; set; }
    public float qz { get; set; }
    public float qw { get; set; } = 1f;
}

public sealed class PropDto
{
    public string type { get; set; } = "rock";
    public float[] pos { get; set; } = { 0, 0, 0 };
    public float[] rot { get; set; } = { 0, 0, 0, 1 };
    public float[] scale { get; set; } = { 1, 1, 1 };
    public float[] color { get; set; } = { 0.42f, 0.42f, 0.44f };
    public bool solid { get; set; } = true;
}
