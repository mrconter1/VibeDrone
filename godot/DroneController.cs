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
    private Arena _arena = null!;

    // lap timing state
    private bool _raceArmed;       // sitting fixed at the start, waiting for the first input
    private bool _raceRunning;     // clock ticking
    private float _lapTime;        // current lap elapsed
    private float _armThrottle;    // throttle baseline (settles over the first moments)
    private float _armSettle;      // time armed; input is ignored until it settles
    private int _gatePassed;       // regular gates cleared this lap (need all before the finish counts)
    private int _missGate = -1;    // gate index the miss-detector is tracking
    private float _missPrevZ;      // drone's previous local-Z vs that gate (plane-crossing test)
    private bool _gateHit;         // drone touched a gate bar this frame
    private float _lastLap;        // last completed lap (0 = none yet)
    private readonly List<float> _bestLaps = new();     // ranked fastest laps (persisted)
    private string _ranks = "";    // cached top-laps board text
    private bool _showDebug;       // FPS/FOV/etc overlay (off by default, toggled from the Esc menu)

    // ghost: replay of the best lap, raced against
    private struct Sample { public float T; public Vector3 Pos; public Quaternion Rot; }
    private readonly List<Sample> _recording = new();   // current lap being recorded
    private List<Sample> _bestGhost = new();            // best lap trajectory (persisted)
    private float _recAccum;
    private int _ghostIdx;
    private DroneModel _ghost = null!;
    private MeshInstance3D _trail = null!;
    private ImmediateMesh _trailMesh = null!;

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
        _arena = new Arena();
        AddChild(_arena);   // builds gates + triggers + start marker synchronously

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
        AddChild(menu);       // M opens/closes it and pauses the game

        var pause = new PauseMenu();
        pause.Setup(this, menu);
        AddChild(pause);      // Esc opens it

        var edit = new EditController();
        edit.Setup(_cam, _audio, _arena);   // E toggles a Minecraft-style free-fly camera (pauses the game)
        AddChild(edit);

        // race gate pass-through triggers (gate 0 = start/finish, 1..n-1 = regular in order)
        for (int i = 0; i < _arena.GateTriggers.Count; i++)
        {
            int idx = i;
            _arena.GateTriggers[i].BodyEntered += body => OnGatePassed(idx, body);
        }
        _ghost = new DroneModel { Ghost = true, Visible = false };
        AddChild(_ghost);

        _trailMesh = new ImmediateMesh();
        _trail = new MeshInstance3D
        {
            Mesh = _trailMesh,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true,
                Emission = new Color(0.3f, 0.95f, 1f),
                EmissionEnergyMultiplier = 3f,   // blooms via the high-threshold glow
            },
        };
        AddChild(_trail);

        LoadLaps();
        LoadGhost();

        if (Replay) LoadReplay();
        Input.MouseMode = Input.MouseModeEnum.Captured;   // cursor never shown during flight
        StartRace();   // begin in fly mode, held fixed at the start line, engine off
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
            else if (k.Keycode == Key.R) { _sessionLog.Mark("race start"); StartRace(); }
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

        // armed at the start line: hold fixed until the pilot gives input, then the clock starts.
        // The first ~0.4s just settles the input baseline (controller axes arrive a few frames
        // after launch, so an early baseline would misfire and let the drone drop).
        if (_raceArmed)
        {
            _armSettle += (float)delta;
            if (_armSettle < 0.4f)
            {
                _armThrottle = throttle;   // keep tracking the resting value while it settles
                _curThrottle = throttle; _audio.SetEffort(0f); _ghost.Visible = false; _trail.Visible = false;
                ApplyHud("LIVE"); return;
            }
            bool go = Mathf.Abs(throttle - _armThrottle) > 0.08f
                   || Mathf.Abs(roll) > 0.06f || Mathf.Abs(pitch) > 0.06f || Mathf.Abs(yaw) > 0.06f;
            if (go) { _raceArmed = false; _raceRunning = true; BeginLapRecording(); }
            else { _curThrottle = throttle; _audio.SetEffort(0f); _ghost.Visible = false; _trail.Visible = false; ApplyHud("LIVE"); return; }
        }

        float dt = (float)delta / Substeps;
        for (int i = 0; i < Substeps; i++)
            _fm.Step(roll, pitch, yaw, throttle, dt);

        MoveDroneWithBounce();
        _curThrottle = throttle;
        _flightTime += (float)delta;
        if (_raceRunning)
        {
            _lapTime += (float)delta;
            RecordSample((float)delta);
            UpdateGhost();
            // touch a bar / fly past a gate / hit the ground -> back to the start line (same as R)
            if (_gateHit || MissedGate() || _drone.GlobalPosition.Y < 0.5f) { StartRace(); return; }
        }

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
        _gateHit = col != null;                             // touched a gate bar
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
        if (Replay && _rt.Count > 0)
        {
            ApplyTransform(_rpos[0], _rquat[0]);
        }
        else
        {
            // spawn in the centre of the start/finish gate, level, facing OUT toward gate 1
            Vector3 pos = _arena.StartTransform.Origin;
            Vector3 look = _arena.Gates.Count > 1
                ? _arena.Gates[1].GlobalPosition - pos
                : -_arena.StartTransform.Basis.Z;
            look.Y = 0f;
            if (look.LengthSquared() < 1e-4f) look = Vector3.Forward;
            look = look.Normalized();
            _fm.Pos = new NVec(pos.X, pos.Y, -pos.Z);
            float yawModel = Mathf.Atan2(look.X, -look.Z);          // Godot dir -> model yaw (+Z fwd, Z flip)
            _fm.Rot = NQuat.CreateFromAxisAngle(NVec.UnitY, yawModel);
            ApplyTransform(_fm.Pos, _fm.Rot);
        }
        _drone.ResetPhysicsInterpolation();   // teleport: don't sweep from the old pose
    }

    private void StartRace()
    {
        ResetDrone();          // spawn fixed in the start/finish gate
        _lapTime = 0f;
        _gatePassed = 0;
        _raceRunning = false;
        _raceArmed = true;     // clock starts on first input
        _armSettle = 0f;
        _gateHit = false;
        _ghost.Visible = false;
        _trail.Visible = false;
        BeginLapRecording();
    }

    private void BeginLapRecording()
    {
        _recording.Clear();
        _recAccum = 0f;
        _ghostIdx = 0;
        _missGate = -1;
    }

    // True if the drone flew forward THROUGH the next expected gate's plane but outside its
    // opening (i.e. flew past it) - which can never be completed, so restart.
    private bool MissedGate()
    {
        int regular = _arena.GateTriggers.Count - 1;
        int nextIdx = _gatePassed < regular ? _gatePassed + 1 : 0;
        if (nextIdx >= _arena.Gates.Count) return false;
        Vector3 local = _arena.Gates[nextIdx].GlobalTransform.AffineInverse() * _drone.GlobalPosition;
        bool missed = false;
        if (nextIdx == _missGate && _missPrevZ < 0f && local.Z >= 0f)
            missed = Mathf.Abs(local.X) > 3.4f || Mathf.Abs(local.Y) > 3.4f;   // crossed plane, outside opening
        _missGate = nextIdx;
        _missPrevZ = local.Z;
        return missed;
    }

    // Fired by a gate's Area3D when a body enters it. Regular gates (1..n-1) advance the lap in
    // order; crossing the start/finish (gate 0) records the lap if all gates were cleared and
    // (re)starts the timer for the next lap.
    private void OnGatePassed(int index, Node3D body)
    {
        if (!_raceRunning || body != _drone) return;
        int regular = _arena.GateTriggers.Count - 1;    // gates 1..n-1
        if (index == 0)                                  // start/finish line
        {
            if (_gatePassed >= regular)                  // valid lap: all gates cleared
            {
                bool isBest = _bestLaps.Count == 0 || _lapTime < _bestLaps[0];
                _lastLap = _lapTime;
                RecordLap(_lapTime);
                if (isBest)                              // new record -> this run becomes the ghost
                {
                    _bestGhost = new List<Sample>(_recording);
                    SaveGhost();
                }
            }
            _lapTime = 0f;                               // (re)start the lap timer
            _gatePassed = 0;
            BeginLapRecording();                         // and the recording + ghost
        }
        else if (index == _gatePassed + 1)               // next regular gate, in order
        {
            _gatePassed++;
        }
    }

    // Sample the drone's pose ~40x/s into the current lap recording.
    private void RecordSample(float delta)
    {
        _recAccum += delta;
        if (_recAccum < 0.025f) return;
        _recAccum = 0f;
        _recording.Add(new Sample
        {
            T = _lapTime,
            Pos = _drone.GlobalPosition,
            Rot = _drone.GlobalTransform.Basis.GetRotationQuaternion(),
        });
    }

    // Place the ghost at the best-lap pose for the current lap time, and draw its fading trail.
    private void UpdateGhost()
    {
        if (_bestGhost.Count < 2) { _ghost.Visible = false; _trail.Visible = false; return; }
        _ghost.Visible = true;
        while (_ghostIdx < _bestGhost.Count - 2 && _bestGhost[_ghostIdx + 1].T < _lapTime) _ghostIdx++;
        Sample a = _bestGhost[_ghostIdx];
        Sample b = _bestGhost[_ghostIdx + 1];
        float u = Mathf.Clamp((_lapTime - a.T) / Mathf.Max(b.T - a.T, 1e-4f), 0f, 1f);
        _ghost.GlobalTransform = new Transform3D(new Basis(a.Rot.Slerp(b.Rot, u)), a.Pos.Lerp(b.Pos, u));
        BuildTrail();
    }

    // A fading ribbon along the ghost's recent path (~1 s behind it).
    private void BuildTrail()
    {
        _trailMesh.ClearSurfaces();
        const float window = 1.2f, halfW = 0.35f;
        int end = Mathf.Min(_ghostIdx + 1, _bestGhost.Count - 1);
        int start = end;
        while (start > 0 && _bestGhost[start].T > _lapTime - window) start--;
        if (end - start < 1) { _trail.Visible = false; return; }
        _trail.Visible = true;
        _trailMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = start; i <= end; i++)
        {
            Vector3 p = _bestGhost[i].Pos;
            Vector3 dir = _bestGhost[Mathf.Min(i + 1, end)].Pos - _bestGhost[Mathf.Max(i - 1, start)].Pos;
            Vector3 side = dir.LengthSquared() > 1e-6f ? dir.Normalized().Cross(Vector3.Up) : Vector3.Right;
            if (side.LengthSquared() < 1e-6f) side = Vector3.Right;
            side = side.Normalized() * halfW;
            float age = Mathf.Clamp((_lapTime - _bestGhost[i].T) / window, 0f, 1f);
            var col = new Color(0.4f, 0.95f, 1f, 1f - age);   // fade toward the tail
            _trailMesh.SurfaceSetColor(col);
            _trailMesh.SurfaceAddVertex(p - side);
            _trailMesh.SurfaceSetColor(col);
            _trailMesh.SurfaceAddVertex(p + side);
        }
        _trailMesh.SurfaceEnd();
    }

    private void SaveGhost()
    {
        var arr = new Godot.Collections.Array();
        foreach (Sample s in _bestGhost)
            arr.Add(new Godot.Collections.Dictionary
            {
                { "t", s.T }, { "x", s.Pos.X }, { "y", s.Pos.Y }, { "z", s.Pos.Z },
                { "qx", s.Rot.X }, { "qy", s.Rot.Y }, { "qz", s.Rot.Z }, { "qw", s.Rot.W },
            });
        using var f = FileAccess.Open("user://ghost.json", FileAccess.ModeFlags.Write);
        if (f != null) f.StoreString(Json.Stringify(arr));
    }

    private void LoadGhost()
    {
        const string path = "user://ghost.json";
        if (!FileAccess.FileExists(path)) return;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return;
        Variant parsed = Json.ParseString(f.GetAsText());
        if (parsed.VariantType != Variant.Type.Array) return;
        _bestGhost = new List<Sample>();
        foreach (Variant v in parsed.AsGodotArray())
        {
            var d = v.AsGodotDictionary();
            _bestGhost.Add(new Sample
            {
                T = d["t"].AsSingle(),
                Pos = new Vector3(d["x"].AsSingle(), d["y"].AsSingle(), d["z"].AsSingle()),
                Rot = new Quaternion(d["qx"].AsSingle(), d["qy"].AsSingle(), d["qz"].AsSingle(), d["qw"].AsSingle()),
            });
        }
    }

    private void RecordLap(float t)
    {
        _bestLaps.Add(t);
        _bestLaps.Sort();
        if (_bestLaps.Count > 5) _bestLaps.RemoveRange(5, _bestLaps.Count - 5);
        _ranks = "";
        for (int i = 0; i < _bestLaps.Count; i++)
            _ranks += $"{i + 1}.  {_bestLaps[i]:00.00}\n";
        SaveLaps();
    }

    private void LoadLaps()
    {
        const string path = "user://laptimes.json";
        if (!FileAccess.FileExists(path)) return;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return;
        Variant parsed = Json.ParseString(f.GetAsText());
        if (parsed.VariantType != Variant.Type.Array) return;
        foreach (Variant v in parsed.AsGodotArray()) _bestLaps.Add(v.AsSingle());
        _bestLaps.Sort();
        _ranks = "";
        for (int i = 0; i < _bestLaps.Count && i < 5; i++)
            _ranks += $"{i + 1}.  {_bestLaps[i]:00.00}\n";
    }

    private void SaveLaps()
    {
        var arr = new Godot.Collections.Array();
        foreach (float t in _bestLaps) arr.Add(t);
        using var f = FileAccess.Open("user://laptimes.json", FileAccess.ModeFlags.Write);
        if (f != null) f.StoreString(Json.Stringify(arr));
    }

    public void SetShowDebug(bool on) => _showDebug = on;
    public bool ShowDebug => _showDebug;

    // Wipe saved best laps + ghost (from the Esc menu), so a fresh best records a new ghost.
    public void ClearResults()
    {
        _bestLaps.Clear();
        _ranks = "";
        _lastLap = 0f;
        _bestGhost = new List<Sample>();
        _ghost.Visible = false;
        _trail.Visible = false;
        SaveLaps();    // writes an empty list
        SaveGhost();   // writes an empty list
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
        _osd.ShowDebug = _showDebug;
        int regular = _arena.GateTriggers.Count - 1;
        _osd.LapTime = _lapTime;
        _osd.LastLap = _lastLap;
        _osd.BestLap = _bestLaps.Count > 0 ? _bestLaps[0] : 0f;
        _osd.Ranks = _ranks;
        _osd.RaceStatus = _raceArmed ? "GO!  (throttle up)"
                        : _raceRunning ? $"gate {_gatePassed}/{regular}" : "R to start";
        // climb angle (pitch) and roll from the drone basis, for the artificial horizon
        _osd.PitchDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(b.Z.Y, -1f, 1f)));
        _osd.RollDeg = Mathf.RadToDeg(Mathf.Atan2(b.X.Y, b.Y.Y));
    }
}
