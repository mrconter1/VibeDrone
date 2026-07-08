using Godot;

// One frame of pilot input: roll/pitch/yaw in [-1, 1], throttle in [0, 1].
public readonly struct Sticks
{
    public readonly float Roll, Pitch, Yaw, Throttle;
    public Sticks(float roll, float pitch, float yaw, float throttle)
    {
        Roll = roll; Pitch = pitch; Yaw = yaw; Throttle = throttle;
    }
}

// Samples the pilot's stick + throttle from the gamepad (or a keyboard fallback) each physics tick,
// applying dead-zone and axis sign/orientation config. Kept out of DroneController so the flight loop
// just asks for the current Sticks. Not a Node: the controller forwards joypad axis events to
// FeedAxis and calls Sample() from _PhysicsProcess.
public sealed class FlightInput
{
    // gamepad axis mapping + orientation (defaults match a standard Mode 2 layout)
    public int AxisRoll = 0, AxisPitch = 1, AxisThrottle = 2, AxisYaw = 3;
    public float SignRoll = 1f, SignPitch = -1f, SignYaw = -1f, SignThrottle = 1f;

    private readonly float[] _axes = new float[16];
    private float _kThrottle;      // keyboard throttle integrates W/S over time
    private bool _hasJoypad;       // refreshed every ~15 ticks, not polled per frame (alloc)

    // Forward a joypad axis event (from the node's _Input).
    public void FeedAxis(int axis, float value)
    {
        if (axis >= 0 && axis < _axes.Length) _axes[axis] = value;
    }

    private static float Dead(float v, float dz = 0.04f) => Mathf.Abs(v) < dz ? 0f : v;

    // Current stick + throttle, sign-corrected. delta advances the keyboard throttle integrator.
    public Sticks Sample(double delta)
    {
        if (Engine.GetPhysicsFrames() % 15 == 0)   // refresh joypad presence occasionally (avoids per-tick alloc)
            _hasJoypad = Input.GetConnectedJoypads().Count > 0;

        float roll, pitch, yaw, throttle;
        if (_hasJoypad)
        {
            roll = Dead(_axes[AxisRoll]); pitch = Dead(_axes[AxisPitch]); yaw = Dead(_axes[AxisYaw]);
            throttle = (_axes[AxisThrottle] + 1f) * 0.5f;
        }
        else
        {
            roll = (Input.IsKeyPressed(Key.Right) ? 1 : 0) - (Input.IsKeyPressed(Key.Left) ? 1 : 0);
            pitch = (Input.IsKeyPressed(Key.Up) ? 1 : 0) - (Input.IsKeyPressed(Key.Down) ? 1 : 0);
            yaw = (Input.IsKeyPressed(Key.C) ? 1 : 0) - (Input.IsKeyPressed(Key.Q) ? 1 : 0);  // E is edit-mode
            _kThrottle = Mathf.Clamp(_kThrottle +
                ((Input.IsKeyPressed(Key.W) ? 1 : 0) - (Input.IsKeyPressed(Key.S) ? 1 : 0)) * (float)delta, 0f, 1f);
            throttle = _kThrottle;
        }
        roll *= SignRoll; pitch *= SignPitch; yaw *= SignYaw;
        if (SignThrottle < 0) throttle = 1f - throttle;
        return new Sticks(roll, pitch, yaw, throttle);
    }
}
