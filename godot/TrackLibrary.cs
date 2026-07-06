using Godot;

// The built-in race tracks. Each is a closed loop of gate positions (XZ metres); gate 0 is the
// black/white START-FINISH, the rest are numbered green/red gates in order. Arena builds gates
// from the selected track and the player cycles tracks from the pause menu. Each track keeps its
// own saved gate layout, ghost and lap times (keyed by index), so records never mix.
public static class TrackLibrary
{
    public readonly struct Track
    {
        public readonly string Name;
        public readonly Vector2[] Gates;
        public Track(string name, Vector2[] gates) { Name = name; Gates = gates; }
    }

    public static readonly Track[] Tracks =
    {
        new("Circuit", new Vector2[]   // the original medium loop
        {
            new(0, 20), new(28, 52), new(16, 92), new(-26, 88), new(-32, 48), new(-12, 22),
        }),
        new("Big Oval", new Vector2[]  // wide, fast, few direction changes
        {
            new(0, 20), new(50, 45), new(55, 95), new(0, 120), new(-55, 95), new(-50, 45),
        }),
        new("Technical", new Vector2[] // tight and twisty, more gates
        {
            new(0, 15), new(18, 30), new(10, 55), new(24, 72),
            new(2, 80), new(-20, 66), new(-14, 40), new(-20, 20),
        }),
    };

    public static int Count => Tracks.Length;
    public static string Name(int index) => Tracks[Wrap(index)].Name;
    public static Vector2[] Gates(int index) => Tracks[Wrap(index)].Gates;
    public static int Wrap(int index) => ((index % Count) + Count) % Count;
}
