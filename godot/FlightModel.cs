using System;
using System.Numerics;

namespace OpenDrone
{
    // Engine-agnostic drone flight model fitted from the reference sim flight logs.
    // Pure C# + System.Numerics, no Godot/Unity types -> drop into anything.
    // Port of python/the model module. Coordinates: Y up, body +Z forward, +X right.
    public sealed class FlightModel
    {
        // --- fitted parameters (the parameter set, clean aligned capture a capture run,
        //     split vertical drag: linear accel R2 0.94, drift 4.2% @0.5s) ---
        public float G = 10.6562f;           // fitted gravity (free param; soaks up model slack)

        // rate curve per axis: omega = lin*s + cubic*s^3 + bias, s = stick in [-1,1]
        public Vector3 RollRate  = new(-2.6535f, -8.9311f,  0.0005f);   // (lin, cubic, bias)
        public Vector3 PitchRate = new(-2.7154f, -8.8841f, -0.0044f);
        public Vector3 YawRate   = new(-2.6760f, -8.8409f,  0.0168f);
        // which body-rate axis each stick drives (x=0,y=1,z=2)
        public int RollAxis = 2, PitchAxis = 0, YawAxis = 1;

        public float ThrustK = 28.0015f;     // accel per proxy unit
        // mixer proxy ~ sum(motor_norm^2): m0*4t^2 + s2*(m1 + m2*t + m3*t^2) + m4 + m5*t,
        // s2 = roll^2+pitch^2+yaw^2. Captures motor spread for rotation + saturation;
        // matches the perfect per-motor thrust ceiling in validation (R2 0.968 vs 0.941).
        public float Mix0 = 0.794f, Mix1 = 0.064f, Mix2 = 0.166f,
                     Mix3 = -0.498f, Mix4 = 0.126f, Mix5 = 0.549f;
        // vertical drag = DragUp + DragUpT * thrust/ThrustK: parasitic airframe part
        // + rotor-inflow damping that vanishes with motors off (realistic free fall).
        public float DragUp = 0.6770f;
        public float DragUpT = 0.6295f;
        public float DragLatKd = 0.0489f;    // lateral linear drag
        public float DragLatKq = 0.0215f;    // lateral quadratic drag
        public float Tau = 0.005f;           // motor spool-up time constant (s)
        public float TauRate = 0.002f;       // angular-rate response lag (s); ~0, kept for parity with the Python model

        // --- state ---
        public Vector3 Pos;
        public Vector3 Vel;
        public Quaternion Rot = Quaternion.Identity;
        public float Thrust;                 // current (lagged) thrust acceleration
        public Vector3 Omega;                // current (lagged) body angular rate

        public FlightModel() => Reset();

        public void Reset()
        {
            Pos = new Vector3(0f, 2f, 0f);
            Vel = Vector3.Zero;
            Rot = Quaternion.Identity;
            Omega = Vector3.Zero;
            Thrust = G;
        }

        // rate curve: omega = lin*s + cubic*s^3 + bias (s = stick in [-1,1]). Public so the
        // Godot controller can integrate orientation natively (right-handed) instead of
        // copying this model's left-handed quaternion.
        public static float RateCurve(Vector3 c, float s) => c.X * s + c.Y * s * s * s + c.Z;
        private static float Curve(Vector3 c, float s) => RateCurve(c, s);

        private Vector3 BodyRates(float roll, float pitch, float yaw)
        {
            // no heap allocation in the per-tick hot path
            Vector3 w = Vector3.Zero;
            SetAxis(ref w, RollAxis, Curve(RollRate, roll));
            SetAxis(ref w, PitchAxis, Curve(PitchRate, pitch));
            SetAxis(ref w, YawAxis, Curve(YawRate, yaw));
            return w;
        }

        private static void SetAxis(ref Vector3 v, int axis, float val)
        {
            if (axis == 0) v.X = val;
            else if (axis == 1) v.Y = val;
            else v.Z = val;
        }

        // rotate v by q (or its inverse). System.Numerics quaternions are (x,y,z,w).
        private static Vector3 Rotate(Quaternion q, Vector3 v, bool inverse = false)
        {
            if (inverse) q = Quaternion.Conjugate(q);
            return Vector3.Transform(v, q);
        }

        private static Quaternion IntegrateQuat(Quaternion q, Vector3 omega, float dt)
        {
            float w = omega.Length();
            if (w < 1e-9f) return Quaternion.Normalize(q);
            float ang = w * dt;
            Vector3 axis = omega / w;
            float s = MathF.Sin(ang * 0.5f);
            var dq = new Quaternion(axis.X * s, axis.Y * s, axis.Z * s, MathF.Cos(ang * 0.5f));
            return Quaternion.Normalize(q * dq);
        }

        private Vector3 Accel(Vector3 vel, Quaternion q, float thrust)
        {
            float spd = vel.Length();
            Vector3 vb = Rotate(q, vel, inverse: true);
            float lat = DragLatKd + DragLatKq * spd;
            float dup = DragUp + DragUpT * (thrust / MathF.Max(ThrustK, 1e-6f));
            var aBody = new Vector3(-lat * vb.X, thrust - dup * vb.Y, -lat * vb.Z);
            return Rotate(q, aBody) + new Vector3(0f, -G, 0f);
        }

        // Advance one fixed step. Controls: roll/pitch/yaw in [-1,1], throttle in [0,1].
        public void Step(float roll, float pitch, float yaw, float throttle, float dt)
        {
            // angular rate lags the commanded target (first-order; matches the model module)
            Vector3 targetRate = BodyRates(roll, pitch, yaw);
            Omega += (targetRate - Omega) * MathF.Min(dt / MathF.Max(TauRate, 1e-4f), 1f);
            Rot = IntegrateQuat(Rot, Omega, dt);

            float s2 = roll * roll + pitch * pitch + yaw * yaw;
            float proxy = MathF.Max(0f, Mix0 * 4f * throttle * throttle
                          + s2 * (Mix1 + Mix2 * throttle + Mix3 * throttle * throttle)
                          + Mix4 + Mix5 * throttle);
            float target = ThrustK * proxy;
            Thrust += (target - Thrust) * MathF.Min(dt / MathF.Max(Tau, 1e-4f), 1f);

            // RK4 on translation, Rot & Thrust held over the step
            Vector3 k1 = Accel(Vel, Rot, Thrust);
            Vector3 k2 = Accel(Vel + 0.5f * dt * k1, Rot, Thrust);
            Vector3 k3 = Accel(Vel + 0.5f * dt * k2, Rot, Thrust);
            Vector3 k4 = Accel(Vel + dt * k3, Rot, Thrust);
            Vector3 newVel = Vel + dt / 6f * (k1 + 2 * k2 + 2 * k3 + k4);
            Vector3 newPos = Pos + dt / 6f * (Vel + 2 * (Vel + 0.5f * dt * k1)
                              + 2 * (Vel + 0.5f * dt * k2) + (Vel + dt * k3));

            if (newPos.Y < 0f)
            {
                newPos.Y = 0f;
                newVel.Y = MathF.Max(0f, newVel.Y);
            }
            Vel = newVel;
            Pos = newPos;
        }
    }
}
