using Godot;

// Registry of placeable object types. Each type maps to a procedural mesh; a Prop carries the
// transform + colour, so adding a new object type is just another case here - the level format and
// loader never change. Meshes are unit-sized; the Prop's scale sizes them.
public static class PropTypes
{
    public static readonly string[] Types = { "rock" };

    public static string Next(string type)
    {
        int i = System.Array.IndexOf(Types, type);
        return Types[(i + 1) % Types.Length];
    }

    // One shared mesh resource per type (props scale via their node, so the mesh stays unit-sized).
    private static Mesh _rock = null!;

    public static Mesh Mesh(string type) => type switch
    {
        "rock" => _rock ??= Rock(),
        _ => _rock ??= Rock(),
    };

    public static PropNode Build(Prop data)
    {
        var node = new PropNode();
        node.Init(data);
        return node;
    }

    // A faceted low-poly boulder (unit radius). Non-uniform Prop scale turns it into varied rocks.
    private static Mesh Rock() => new SphereMesh
    {
        Radius = 1f, Height = 2f, RadialSegments = 7, Rings = 4,
    };
}
