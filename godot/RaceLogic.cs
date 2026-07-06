using System;

namespace OpenDrone
{
    // Pure race book-keeping: which gate is expected next, whether the drone flew past a gate
    // outside its opening, and whether a finish-line crossing counts as a valid lap. No Godot
    // types so it can be unit-tested directly. DroneController feeds it gate geometry.
    public static class RaceLogic
    {
        // The trigger opening is 6 m (half 3 m); allow a little slack before calling it a miss.
        public const float MissHalf = 3.4f;

        // The gate index expected next, given how many regular gates (1..regularCount) are cleared.
        // After the last regular gate the next target is the start/finish line (gate 0).
        public static int NextGate(int gatePassed, int regularCount) =>
            gatePassed < regularCount ? gatePassed + 1 : 0;

        // True if this regular gate is the next one expected in order.
        public static bool IsNextRegular(int index, int gatePassed) => index == gatePassed + 1;

        // A finish crossing records a lap only if every regular gate was cleared first.
        public static bool LapValid(int gatePassed, int regularCount) => gatePassed >= regularCount;

        // The drone crossed the gate plane this tick (local Z went from behind, <0, to in front,
        // >=0) but outside the opening on X or Y - i.e. it flew past instead of through.
        public static bool FlewPastGate(float prevLocalZ, float curLocalX, float curLocalY, float curLocalZ,
                                        float missHalf = MissHalf) =>
            prevLocalZ < 0f && curLocalZ >= 0f &&
            (MathF.Abs(curLocalX) > missHalf || MathF.Abs(curLocalY) > missHalf);
    }
}
