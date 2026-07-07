namespace OpenDrone
{
    public enum GateResult { None, Advanced, FinishValid, FinishInvalid }

    // The lap state machine: armed at the line, running clock, gate progress, and the plane-crossing
    // miss tracker. Pure C# (no Godot) over the decisions in RaceLogic, so it is unit-testable. The
    // controller feeds it (Arm/Launch/Tick/RegisterGate/UpdateMiss) and reacts to the results
    // (record a lap, restart), keeping physics + the recorder on its side.
    public sealed class RaceState
    {
        public bool Armed { get; private set; }
        public bool Running { get; private set; }
        public float LapTime { get; private set; }
        public int GatePassed { get; private set; }

        private int _missGate = -1;
        private float _missPrevZ;

        // Reset to sitting armed at the start line (clock stopped).
        public void Arm()
        {
            Armed = true;
            Running = false;
            LapTime = 0f;
            GatePassed = 0;
            _missGate = -1;
        }

        // First pilot input: the clock starts.
        public void Launch()
        {
            Armed = false;
            Running = true;
            _missGate = -1;
        }

        public void Tick(float dt)
        {
            if (Running) LapTime += dt;
        }

        // Which gate index is expected next (0 = the finish line once all regular gates are cleared).
        public int NextGate(int regularCount) => RaceLogic.NextGate(GatePassed, regularCount);

        // A gate trigger fired. Advances progress, or on the finish line resets for the next lap and
        // reports whether that lap counted (all regular gates cleared).
        public GateResult RegisterGate(int index, int regularCount)
        {
            if (index == 0)
            {
                GateResult r = RaceLogic.LapValid(GatePassed, regularCount) ? GateResult.FinishValid : GateResult.FinishInvalid;
                LapTime = 0f;
                GatePassed = 0;
                _missGate = -1;
                return r;
            }
            if (RaceLogic.IsNextRegular(index, GatePassed)) { GatePassed++; return GateResult.Advanced; }
            return GateResult.None;
        }

        // True if the drone flew past the next gate (crossed its plane outside the opening). Caller
        // supplies the drone's position in that gate's local frame.
        public bool UpdateMiss(int nextIdx, float localX, float localY, float localZ)
        {
            bool missed = nextIdx == _missGate && RaceLogic.FlewPastGate(_missPrevZ, localX, localY, localZ);
            _missGate = nextIdx;
            _missPrevZ = localZ;
            return missed;
        }
    }
}
