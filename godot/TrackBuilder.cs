using System;
using System.Collections.Generic;
using Godot;

// Turns a set of waypoints into placed gates, and can generate a random waypoint loop from a
// seed. Path: convex hull -> displaced edge midpoints -> push-apart -> a closed Catmull-Rom
// Curve3D, sampled at even arc-length for gate positions. Gate orientation comes from the curve
// tangent (forward) plus the baked up-vector (a rotation-minimizing approximation), so gates roll
// and pitch with a 3D track. Seeded, so a given track always builds identically (records stay valid).
public static class TrackBuilder
{
    // --- authored flat track: one gate per point, yaw along travel, fixed height (the original path)
    public static Transform3D[] Flat(Vector2[] pts, float y = 8f)
    {
        var poses = new Transform3D[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 dir = (pts[(i + 1) % pts.Length] - pts[i]).Normalized();
            float yaw = Mathf.Atan2(dir.X, dir.Y);
            var basis = Basis.Identity.Rotated(Vector3.Up, yaw);
            poses[i] = new Transform3D(basis, new Vector3(pts[i].X, y, pts[i].Y));
        }
        return poses;
    }

    // --- procedurally generated 3D loop ---
    public static Transform3D[] Generated(int seed, int gateCount = 10, float radius = 60f,
                                          float baseHeight = 10f, float heightAmp = 6f)
    {
        var rng = new Random(seed);
        List<Vector2> shape = GenerateLoop(rng, radius);
        Curve3D curve = BuildClosedCurve(shape, rng, baseHeight, heightAmp);
        return PlaceGates(curve, gateCount);
    }

    // convex hull of random points, with each hull edge's midpoint displaced outward, then relaxed.
    private static List<Vector2> GenerateLoop(Random rng, float radius)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i < 14; i++)
            pts.Add(new Vector2(Rand(rng, -radius, radius), Rand(rng, -radius, radius)));

        List<Vector2> hull = ConvexHull(pts);

        // insert a displaced midpoint on every hull edge -> concavities and interest
        var loop = new List<Vector2>();
        for (int i = 0; i < hull.Count; i++)
        {
            Vector2 a = hull[i], b = hull[(i + 1) % hull.Count];
            loop.Add(a);
            Vector2 mid = (a + b) * 0.5f;
            Vector2 outward = mid.Normalized();               // roughly away from centre
            float disp = Rand(rng, -0.15f, 0.35f) * (b - a).Length();
            loop.Add(mid + outward * disp);
        }

        PushApart(loop, minDist: radius * 0.35f, iterations: 3);
        return loop;
    }

    // Andrew's monotone chain convex hull (counter-clockwise).
    private static List<Vector2> ConvexHull(List<Vector2> pts)
    {
        var p = new List<Vector2>(pts);
        p.Sort((u, v) => u.X != v.X ? u.X.CompareTo(v.X) : u.Y.CompareTo(v.Y));
        int n = p.Count;
        if (n < 3) return p;

        var hull = new List<Vector2>();
        for (int i = 0; i < n; i++)   // lower chain
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p[i]) <= 0f) hull.RemoveAt(hull.Count - 1);
            hull.Add(p[i]);
        }
        int lower = hull.Count + 1;
        for (int i = n - 2; i >= 0; i--)   // upper chain
        {
            while (hull.Count >= lower && Cross(hull[^2], hull[^1], p[i]) <= 0f) hull.RemoveAt(hull.Count - 1);
            hull.Add(p[i]);
        }
        hull.RemoveAt(hull.Count - 1);   // last point == first
        return hull;
    }

    private static float Cross(Vector2 o, Vector2 a, Vector2 b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    // relax so no two adjacent points are closer than minDist (keeps gates from bunching)
    private static void PushApart(List<Vector2> pts, float minDist, int iterations)
    {
        float min2 = minDist * minDist;
        for (int it = 0; it < iterations; it++)
            for (int i = 0; i < pts.Count; i++)
                for (int j = i + 1; j < pts.Count; j++)
                {
                    Vector2 d = pts[j] - pts[i];
                    float dist2 = d.LengthSquared();
                    if (dist2 > 1e-4f && dist2 < min2)
                    {
                        float dist = Mathf.Sqrt(dist2);
                        Vector2 push = d / dist * ((minDist - dist) * 0.5f);
                        pts[i] -= push;
                        pts[j] += push;
                    }
                }
    }

    // closed Catmull-Rom through the (x,z) points, with a smooth sinusoidal height profile.
    private static Curve3D BuildClosedCurve(List<Vector2> shape, Random rng, float baseH, float amp)
    {
        int n = shape.Count;
        var pts = new Vector3[n];
        float phase = Rand(rng, 0f, Mathf.Tau);
        int harmonics = rng.Next(1, 3);   // 1 or 2 humps around the loop
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float h = baseH + amp * Mathf.Sin(Mathf.Tau * harmonics * t + phase);
            pts[i] = new Vector3(shape[i].X, Mathf.Max(3f, h), shape[i].Y);
        }

        var curve = new Curve3D { UpVectorEnabled = true };
        for (int i = 0; i < n; i++)
        {
            Vector3 prev = pts[(i - 1 + n) % n], next = pts[(i + 1) % n];
            Vector3 tangent = (next - prev) / 6f;             // Catmull-Rom -> cubic Bezier handles
            curve.AddPoint(pts[i], -tangent, tangent);
        }
        curve.AddPoint(pts[0], -(pts[1] - pts[n - 1]) / 6f, (pts[1] - pts[n - 1]) / 6f);   // close the seam
        return curve;
    }

    // sample the baked curve at even arc-length; orient each gate to the tangent + baked up-vector.
    private static Transform3D[] PlaceGates(Curve3D curve, int gateCount)
    {
        float len = curve.GetBakedLength();
        var poses = new Transform3D[gateCount];
        const float eps = 0.5f;
        for (int i = 0; i < gateCount; i++)
        {
            float d = len * i / gateCount;
            Vector3 pos = curve.SampleBaked(d);
            Vector3 ahead = curve.SampleBaked(Mathf.PosMod(d + eps, len));
            Vector3 fwd = (ahead - pos);
            if (fwd.LengthSquared() < 1e-6f) fwd = Vector3.Forward;
            fwd = fwd.Normalized();
            Vector3 up = curve.SampleBakedUpVector(d).Normalized();
            Vector3 right = up.Cross(fwd).Normalized();
            if (right.LengthSquared() < 1e-6f) right = Vector3.Right;
            up = fwd.Cross(right).Normalized();
            poses[i] = new Transform3D(new Basis(right, up, fwd), pos);
        }
        return poses;
    }

    private static float Rand(Random rng, float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
}
