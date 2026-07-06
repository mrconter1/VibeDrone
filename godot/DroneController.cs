using System.Collections.Generic;
using System.Runtime;
using Godot;
using OpenDrone;
using NVec = System.Numerics.Vector3;
using NQuat = System.Numerics.Quaternion;

// Drives the portable FlightModel and applies the result to this Node3D, OR replays a
// recorded the reference sim flight. Both paths go through ONE verified Unity->Godot conversion
// (ToGodot), so if the replay looks correct the live model is guaranteed consistent.
// The model integrates orientation as a proper quaternion (in the reference sim's frame); we
// convert via a forward/up basis - no fragile per-axis sequential rotation.
public partial class DroneController : Node3D
{
    [Export] public int JoyDevice = 0;
    [Export] public int AxisRoll = 0, AxisPitch = 1, AxisThrottle = 2, AxisYaw = 3;
    [Export] public float SignRoll = 1f, SignPitch = -1f, SignYaw = -1f, SignThrottle = 1f;
    // FOV 95 deg ~ the reference sim's default (90-100); lower than a real cam's 120+ to cut
    // the rectilinear edge-stretch warping. ~30 deg uptilt (freestyle 25-35), nose-mounted.
    [Export] public float CameraFovDeg = 95f;
    [Export] public float CameraTiltDeg = 30f;
    [Export] public float CameraForward = 0.08f;   // metres in front of CG
    [Export] public float CameraUp = 0.03f;        // metres above CG
    [Export] public int Substeps = 1;   // physics already ticks at 250 Hz (project.godot)
    [Export] public float DroneRadius = 0.15f;   // ~5" quad half-width, for gate collision
    [Export] public float Restitution = 0.18f;   // normal rebound (low: real quads barely bounce)
    [Export] public float HitFriction = 0.55f;   // tangential speed scrubbed off on a hit (deflect, not slide)
    [Export] public bool Replay = false;                 // start live; Tab to watch recorded replay
    [Export] public string ReplayFile = "res://replay.csv";

    private readonly FlightModel _fm = new();
    private CharacterBody3D _drone = null!;   // kinematic body so it can collide/bounce off gates
    private Camera3D _cam = null!;
    private Hud _osd = null!;
    private MotorAudio _audio = null!;
    private float _kThrottle;
    private float _flightTime;
    private float _curThrottle;
    private readonly float[] _axes = new float[16];
    private SessionLog _sessionLog = null!;

    // replay data
    private readonly List<float> _rt = new();
    private readonly List<NVec> _rpos = new();
    private readonly List<NQuat> _rquat = new();
    private float _replayT;

    public override void _Ready()
    {
        // prefer short GC pauses over throughput: fewer gen2 stalls = fewer missed frames
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        _sessionLog = new SessionLog();
        AddChild(_sessionLog);

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        AddChild(new Arena());

        _drone = new CharacterBody3D();
        AddChild(_drone);
        _drone.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = DroneRadius } });
        _cam = new Camera3D { Fov = CameraFovDeg };
        _drone.AddChild(_cam);
        _cam.Position = new Vector3(0f, CameraUp, CameraForward);   // nose mount (drone local frame)
        _cam.RotationDegrees = new Vector3(CameraTiltDeg, 180f, 0f);

        var layer = new CanvasLayer();
        AddChild(layer);
        _osd = new Hud();
        layer.AddChild(_osd);

        _audio = new MotorAudio();
        AddChild(_audio);

        var menu = new SoundMenu();
        menu.Setup(_audio);   // set before AddChild: AddChild runs _Ready synchronously
        AddChild(menu);       // S opens/closes it and pauses the game

        var edit = new EditController();
        edit.Setup(_cam, _audio);   // E toggles a Minecraft-style free-fly camera (pauses the game)
        AddChild(edit);

        if (Replay) LoadReplay();
        Input.MouseMode = Input.MouseModeEnum.Captured;   // cursor never shown during flight
        ResetDrone();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventJoypadMotion m && (int)m.Axis < _axes.Length)
            _axes[(int)m.Axis] = m.AxisValue;
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode == Key.Escape) GetTree().Quit();
            else if (k.Keycode == Key.R) { _sessionLog.Mark("reset"); ResetDrone(); }
            else if (k.Keycode == Key.Tab) { Replay = !Replay; if (Replay && _rt.Count == 0) LoadReplay(); _replayT = 0; ResetDrone(); }
            // S opens the sound menu (handled by SoundMenu, which also pauses the game)
        }
    }

    private static float Dead(float v, float dz = 0.04f) => Mathf.Abs(v) < dz ? 0f : v;

    public override void _PhysicsProcess(double delta)
    {
        if (Replay && _rt.Count > 1) { PlayReplay((float)delta); _audio.SetEffort(0f); ApplyHud("REPLAY (Tab=live)"); return; }

        float roll, pitch, yaw, throttle;
        if (Input.GetConnectedJoypads().Count > 0)
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

        float dt = (float)delta / Substeps;
        for (int i = 0; i < Substeps; i++)
            _fm.Step(roll, pitch, yaw, throttle, dt);

        MoveDroneWithBounce();
        _curThrottle = throttle;
        _flightTime += (float)delta;

        // drive motor audio from throttle AND stick activity, so rolls/pitches/yaws audibly
        // rev the motors (the thrust proxy saturates and would hide that). Silent at rest.
        float stick = Mathf.Min(1f, (Mathf.Abs(roll) + Mathf.Abs(pitch) + Mathf.Abs(yaw)) / 2f);
        _audio.SetEffort(Mathf.Clamp(throttle * 0.9f + stick * 0.5f, 0f, 1f));

        // fixed FOV (typical FPV camera); no speed-scaling

        ApplyHud("LIVE");
    }

    // --- the single verified Unity(LH, Y up, +Z fwd) -> Godot(RH, Y up, -Z fwd) conversion ---
    private static void ToGodot(NVec pU, NQuat qU, out Vector3 pos, out Basis basis)
    {
        pos = new Vector3(pU.X, pU.Y, -pU.Z);                 // flip Z (LH->RH)
        NVec fU = NVec.Transform(new NVec(0, 0, 1), qU);      // body forward (Unity)
        NVec uU = NVec.Transform(new NVec(0, 1, 0), qU);      // body up (Unity)
        Vector3 f = new Vector3(fU.X, fU.Y, -fU.Z).Normalized();
        Vector3 u = new Vector3(uU.X, uU.Y, -uU.Z).Normalized();
        Vector3 r = u.Cross(f).Normalized();
        u = f.Cross(r).Normalized();
        basis = new Basis(r, u, f);                            // local X=right, Y=up, Z=forward
    }

    private void ApplyTransform(NVec pU, NQuat qU)
    {
        ToGodot(pU, qU, out Vector3 pos, out Basis basis);
        _drone.GlobalTransform = new Transform3D(basis, pos);
    }

    // Move the drone toward the model's new pose using MoveAndCollide, so it bounces off
    // gate bars. On contact: reflect the model velocity about the surface normal (scaled by
    // Restitution) and sync the model position to where the body actually ended up.
    // Model frame <-> Godot frame is just a Z flip (see ToGodot), for both position and velocity.
    private void MoveDroneWithBounce()
    {
        ToGodot(_fm.Pos, _fm.Rot, out Vector3 target, out Basis basis);
        _drone.GlobalBasis = basis;                         // orientation (sphere shape: rotation is moot)
        KinematicCollision3D col = _drone.MoveAndCollide(target - _drone.GlobalPosition);
        if (col != null)
        {
            Vector3 n = col.GetNormal();
            var vG = new Vector3(_fm.Vel.X, _fm.Vel.Y, -_fm.Vel.Z);
            float into = vG.Dot(n);
            if (into < 0f)                                  // only respond if moving into the surface
            {
                // real-drone hit: only a small normal rebound, and the tangential slide is
                // heavily scrubbed (energy lost) so it thuds/deflects instead of bouncing.
                Vector3 vN = into * n;                      // component into the surface
                Vector3 vT = vG - vN;                       // tangential (along the bar)
                vG = vT * (1f - HitFriction) - Restitution * vN;
                _fm.Vel = new NVec(vG.X, vG.Y, -vG.Z);
            }
        }
        Vector3 gp = _drone.GlobalPosition;                 // keep the model in sync with the real position
        _fm.Pos = new NVec(gp.X, gp.Y, -gp.Z);
    }

    private void ResetDrone()
    {
        _fm.Reset();
        _replayT = 0f;
        _flightTime = 0f;
        if (Replay && _rt.Count > 0) ApplyTransform(_rpos[0], _rquat[0]);
        else ApplyTransform(_fm.Pos, _fm.Rot);
        _drone.ResetPhysicsInterpolation();   // teleport: don't sweep from the old pose
    }

    private void PlayReplay(float delta)
    {
        _replayT += delta;
        if (_replayT > _rt[_rt.Count - 1])                     // loop = teleport
        {
            _replayT = 0f;
            _drone.ResetPhysicsInterpolation();
        }
        int i = 1;
        while (i < _rt.Count && _rt[i] < _replayT) i++;
        if (i >= _rt.Count) i = _rt.Count - 1;
        float a = (_replayT - _rt[i - 1]) / Mathf.Max(_rt[i] - _rt[i - 1], 1e-5f);
        NVec p = NVec.Lerp(_rpos[i - 1], _rpos[i], a);
        NQuat q = NQuat.Slerp(_rquat[i - 1], _rquat[i], a);
        ApplyTransform(p, q);
    }

    private void LoadReplay()
    {
        _rt.Clear(); _rpos.Clear(); _rquat.Clear();
        using var f = FileAccess.Open(ReplayFile, FileAccess.ModeFlags.Read);
        if (f == null) { GD.PrintErr("replay file not found: " + ReplayFile); return; }
        f.GetLine(); // header
        while (!f.EofReached())
        {
            string line = f.GetLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] c = line.Split(',');
            if (c.Length < 8) continue;
            _rt.Add(c[0].ToFloat());
            _rpos.Add(new NVec(c[1].ToFloat(), c[2].ToFloat(), c[3].ToFloat()));
            _rquat.Add(new NQuat(c[4].ToFloat(), c[5].ToFloat(), c[6].ToFloat(), c[7].ToFloat()));
        }
        GD.Print($"replay loaded: {_rt.Count} frames");
    }

    private void ApplyHud(string mode)
    {
        if (_osd == null) return;
        Basis b = _drone.GlobalTransform.Basis;
        _osd.Mode = mode;
        _osd.Speed = _fm.Vel.Length();
        _osd.Alt = _drone.GlobalPosition.Y;
        _osd.Throttle = _curThrottle;
        _osd.TimeSec = _flightTime;
        _osd.Fov = _cam.Fov;
        _osd.Fps = (float)Engine.GetFramesPerSecond();
        _osd.Sound = _audio.CurrentName;
        // climb angle (pitch) and roll from the drone basis, for the artificial horizon
        _osd.PitchDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(b.Z.Y, -1f, 1f)));
        _osd.RollDeg = Mathf.RadToDeg(Mathf.Atan2(b.X.Y, b.Y.Y));
    }
}
