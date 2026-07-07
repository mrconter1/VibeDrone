using Godot;

namespace OpenDrone
{
    // Pure collision response for the drone hitting a gate bar: reflect the incoming velocity about
    // the surface normal, keeping only a little of the normal component (restitution) and scrubbing
    // the tangential slide (friction) so a hit thuds/deflects instead of bouncing cleanly. No engine
    // calls - just vector algebra - so it is unit-tested. Vector3 here is the managed Godot math type.
    public static class Bounce
    {
        // `vel` and `normal` must be in the same frame; `normal` unit-length, pointing out of the
        // surface. Returns the post-hit velocity, or `vel` unchanged when already moving out of the
        // surface (so we never add energy to a glancing/departing contact).
        public static Vector3 Respond(Vector3 vel, Vector3 normal, float restitution, float friction)
        {
            float into = vel.Dot(normal);
            if (into >= 0f) return vel;          // moving away from / along the surface: no response
            Vector3 vN = into * normal;          // component heading into the surface
            Vector3 vT = vel - vN;               // tangential (sliding along the bar)
            return vT * (1f - friction) - restitution * vN;
        }
    }
}
