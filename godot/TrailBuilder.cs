using System.Collections.Generic;
using Godot;

// Shared assembly for the fading motion-ribbon drawn behind a drone, used by both the in-race ghost
// (LapRecorder) and the replay theatre (PlaybackController). It builds a ribbon over the window
// [head-window, head]: an interpolated tail sample, the caller's interior sample times, and an
// interpolated head sample - age runs 1 (oldest) -> 0 (newest) and the ribbon side is the drone's
// right-axis so it rolls with the drone. The caller passes its own reused scratch lists, so this
// allocates nothing per frame.
public static class TrailBuilder
{
    public delegate void Sampler(float t, out Vector3 pos, out Quaternion rot);

    public static void Build(TrailRibbon trail, float head, float floor, float window, float halfW,
        IReadOnlyList<float> interior, Sampler sample,
        List<Vector3> pts, List<float> age, List<Vector3> right)
    {
        float tail = Mathf.Max(head - window, floor);
        if (head - tail < 0.05f) { trail.Visible = false; return; }

        pts.Clear();
        age.Clear();
        right.Clear();

        sample(tail, out Vector3 tp, out Quaternion tr);
        pts.Add(tp); age.Add(1f); right.Add(new Basis(tr).X);

        for (int i = 0; i < interior.Count; i++)
        {
            float t = interior[i];
            if (t <= tail || t >= head) continue;
            sample(t, out Vector3 p, out Quaternion r);
            pts.Add(p); age.Add((head - t) / window); right.Add(new Basis(r).X);
        }

        sample(head, out Vector3 hp, out Quaternion hr);
        pts.Add(hp); age.Add(0f); right.Add(new Basis(hr).X);

        trail.Build(pts, right, age, halfW);
    }
}
