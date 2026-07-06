using System;
using Godot;

// The race tracks. Each track builds an array of gate poses (position + orientation); gate 0 is the
// black/white START-FINISH, the rest are numbered gates in order. The first three are the original
// hand-authored flat loops; the last two are procedurally generated 3D loops (seeded, so they build
// identically every time and their saved records stay valid). Each track keeps its own layout,
// ghost and lap times keyed by index, so records never mix.
public static class TrackLibrary
{
    public readonly struct Track
    {
        public readonly string Name;
        public readonly Func<Transform3D[]> Build;
        public Track(string name, Func<Transform3D[]> build) { Name = name; Build = build; }
    }

    public static readonly Track[] Tracks =
    {
        new("Circuit", () => TrackBuilder.Flat(new Vector2[]
        {
            new(0, 20), new(28, 52), new(16, 92), new(-26, 88), new(-32, 48), new(-12, 22),
        })),
        new("Big Oval", () => TrackBuilder.Flat(new Vector2[]
        {
            new(0, 20), new(50, 45), new(55, 95), new(0, 120), new(-55, 95), new(-50, 45),
        })),
        new("Technical", () => TrackBuilder.Flat(new Vector2[]
        {
            new(0, 15), new(18, 30), new(10, 55), new(24, 72),
            new(2, 80), new(-20, 66), new(-14, 40), new(-20, 20),
        })),
        new("Serpentine", () => TrackBuilder.Generated(seed: 1337, gateCount: 10)),
        new("Maelstrom",  () => TrackBuilder.Generated(seed: 8675309, gateCount: 12, radius: 72f, heightAmp: 9f)),
    };

    public static int Count => Tracks.Length;
    public static string Name(int index) => Tracks[Wrap(index)].Name;
    public static Transform3D[] BuildGates(int index) => Tracks[Wrap(index)].Build();
    public static int Wrap(int index) => ((index % Count) + Count) % Count;
}
