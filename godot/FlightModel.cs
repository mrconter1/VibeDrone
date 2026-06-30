using System;
using System.Numerics;

namespace OpenDrone
{
    // Engine-agnostic drone flight model fitted from the reference sim flight logs.
    // Pure C# + System.Numerics, no Godot/Unity types -> drop into anything.
    // Port of python/the model module. Coordinates: Y up, body +Z forward, +X right.
    public sealed class FlightModel
    {
        // --- fitted parameters (from the validated long flight) ---
        public float G = 9.81f;

        // rate curve per axis: omega = lin*s + cubic*s^3 + bias, s = stick in [-1,1]
        public Vector3 RollRate  = new(-2.8414f, -8.6136f, -0.0008f);   // (lin, cubic, bias)
        public Vector3 PitchRate = new(-2.8138f, -8.7460f, -0.0145f);
        public Vector3 YawRate   = new(-2.9686f, -8.6206f,  0.0108f);
        // which body-rate axis each stick drives (x=0,y=1,z=2)
        public int RollAxis = 2, PitchAxis = 0, YawAxis = 1;

        public float ThrustK = 20.9055f;     // accel per unit (4*throttle^2)
        public float DragUp = 2.1455f;       // vertical body drag
        public float DragLatKd = -0.1032f;   // lateral linear drag
        public float DragLatKq = 0.02747f;   // lateral quadratic drag
        public float Tau = 0.005f;           // motor spool-up time constant (s)

        // --- state ---
        public Vector3 Pos;
        public Vector3 Vel;
        public Quaternion Rot = Quaternion.Identity;
        public float Thrust;                 // current (lagged) thrust acceleration

        public FlightModel() => Reset();

        public void Reset()
        {
            Pos = new Vector3(0f, 2f, 0f);
            Vel = Vector3.Zero;
            Rot = Quaternion.Identity;
            Thrust = G;
        }

        private static float Curve(Vector3 c, float s) => c.X * s + c.Y * s * s * s + c.Z;

        private Vector3 BodyRates(float roll, float pitch, float yaw)
        {
            var w = new float[3];
            w[RollAxis]  = Curve(RollRate, roll);
            w[PitchAxis] = Curve(PitchRate, pitch);
            w[YawAxis]   = Curve(YawRate, yaw);
            return new Vector3(w[0], w[1], w[2]);
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
            var aBody = new Vector3(-lat * vb.X, thrust - DragUp * vb.Y, -lat * vb.Z);
            return Rotate(q, aBody) + new Vector3(0f, -G, 0f);
        }

        // Advance one fixed step. Controls: roll/pitch/yaw in [-1,1], throttle in [0,1].
        public void Step(float roll, float pitch, float yaw, float throttle, float dt)
        {
            Rot = IntegrateQuat(Rot, BodyRates(roll, pitch, yaw), dt);

            float target = ThrustK * 4f * throttle * throttle;
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
