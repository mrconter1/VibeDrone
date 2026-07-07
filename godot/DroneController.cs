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
public partial class DroneController : Node3D, ScreenCoordinator.IGame
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

    private readonly FlightModel _fm = new();
    private CharacterBody3D _drone = null!;   // kinematic body so it can collide/bounce off gates
    private Camera3D _cam = null!;
    private Hud _osd = null!;
    private MotorAudio _audio = null!;
    private float _kThrottle;
    private float _flightTime;
    private float _curThrottle;
    private bool _hasJoypad;          // refreshed every ~15 ticks, not polled per frame (alloc)
    private int _statusKey = int.MinValue;   // caches the HUD race-status string
    private string _statusText = "";
    private readonly float[] _axes = new float[16];
    private SessionLog _sessionLog = null!;
    private Arena _arena = null!;

    // lap timing state
    private readonly RaceState _race = new();   // armed/running/lap-time/gate-progress/miss tracking
    private float _armThrottle;    // throttle baseline (settles over the first moments)
    private float _armSettle;      // time armed; input is ignored until it settles
    private bool _gateHit;         // drone touched a gate bar this frame
    private bool _showDebug;       // FPS/FOV/etc overlay (off by default, toggled from the Esc menu)
    private bool _devSupervised;   // launched under StartDebug -> debug R hot-reloads

    private LapRecorder _recorder = null!;   // ghost + trail + best-lap board + persistence
    private PlaybackController _playback = null!;
    private EditController _edit = null!;

    // --- menu system ---
    private ScreenCoordinator _coord = null!;
    private MenuCamera _menuCam = null!;
    private MenuBackdrop _backdrop = null!;
    private MainMenu _mainMenu = null!;
    private LevelSelect _levelSelect = null!;
    private SettingsMenu _settings = null!;
    private PauseMenu _pause = null!;
    private HelpOverlay _help = null!;
    private LogoMenu _logoMenu = null!;

    public override void _Ready()
    {
        // prefer short GC pauses over throughput: fewer gen2 stalls = fewer missed frames
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        Config.Load();                                       // UI scale + blur + AA preferences
        LegacyMigration.Run();                               // old index-keyed records -> stable-id files
        _devSupervised = OS.GetEnvironment("OPENDRONE_DEV") == "1";   // StartDebug sets this
        if (_devSupervised) _showDebug = true;                        // debug on by default under StartDebug
        GetTree().Root.ContentScaleFactor = Config.UiScale;  // scale the whole UI (fonts + sizes)

        _sessionLog = new SessionLog();
        AddChild(_sessionLog);

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        _arena = new Arena();
        _arena.GatesChanged += WireGates;   // re-wire pass-through triggers whenever gates rebuild
        AddChild(_arena);   // builds the world; a level is loaded below

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

        var sound = new SoundMenu();
        sound.Setup(_audio);   // set before AddChild: AddChild runs _Ready synchronously
        AddChild(sound);       // M opens/closes the dev sound test

        _recorder = new LapRecorder();
        AddChild(_recorder);   // owns the ghost + trail + best-lap board, loads on _Ready

        _playback = new PlaybackController();
        _playback.Setup(_recorder, _cam);
        AddChild(_playback);

        _menuCam = new MenuCamera();
        AddChild(_menuCam);    // orbits the arena behind the full-screen menus
        _backdrop = new MenuBackdrop();
        AddChild(_backdrop);   // frosted blur behind those menus

        _mainMenu = new MainMenu();
        _mainMenu.Setup(this);
        AddChild(_mainMenu);
        _levelSelect = new LevelSelect();
        _levelSelect.Setup(this);
        AddChild(_levelSelect);
        _settings = new SettingsMenu();
        _settings.Setup(this, _audio, sound);
        AddChild(_settings);
        _help = new HelpOverlay();
        _help.Setup(this);
        AddChild(_help);
        _pause = new PauseMenu();
        _pause.Setup(this);
        AddChild(_pause);
        _logoMenu = new LogoMenu();
        AddChild(_logoMenu);   // L on the title screen opens the logo browser

        _coord = new ScreenCoordinator(this, GetTree(), GetViewport(), _cam,
            _mainMenu, _levelSelect, _settings, _pause, _help, _backdrop, _menuCam);
        _coord.ApplyAA();   // MSAA + FXAA (fixes edge/checker shimmer)

        _edit = new EditController();
        _edit.Setup(_cam, _audio, _arena);   // E toggles a Minecraft-style free-fly camera (pauses the game)
        AddChild(_edit);

        // A dev rebuild+relaunch (debug R) drops straight back into the level it was on; otherwise
        // load the first level behind the title screen.
        string devLevel = ConsumeDevRelaunch();
        if (devLevel.Length > 0 && devLevel != "MAIN")   // reload into a level
        {
            SetLevelIndex(LevelStore.IndexOf(devLevel));
            _coord.ResumeGame();
        }
        else                                             // fresh boot, or reload to the title screen
        {
            SetLevelIndex(0);
            _coord.OpenMain();   // the engine boot splash (boot.png) covers load; straight to the title
        }
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventJoypadMotion m && (int)m.Axis < _axes.Length)
            _axes[(int)m.Axis] = m.AxisValue;
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        // Only reached while playing (menus pause the tree + handle their own input).
        // Esc opens the pause menu, R restarts, H shows the controls.
        if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            OpenPause();
        }
        else if (ev is InputEventKey { Pressed: true, Keycode: Key.R })
        {
            if (_showDebug && _devSupervised) RequestDevReload(LevelStore.IdAt(_levelIndex));   // hot-reload this level
            else { _sessionLog.Mark("race start"); StartRace(); }
        }
        else if (ev is InputEventKey { Pressed: true, Keycode: Key.H })
        {
            OpenHelp();
        }
    }

    private static float Dead(float v, float dz = 0.04f) => Mathf.Abs(v) < dz ? 0f : v;

    // Visual-only (ghost + trail) at render rate, not the 250 Hz physics rate.
    public override void _Process(double delta)
    {
        if (_race.Running) _recorder.UpdateVisuals(_race.LapTime, true);
        else _recorder.HideGhost();
    }

    public override void _PhysicsProcess(double delta)
    {
        ReadInput(delta, out float roll, out float pitch, out float yaw, out float throttle);
        if (_race.Armed && !TryStart(delta, roll, pitch, yaw, throttle)) return;   // holding at the start line
        StepFlight(delta, roll, pitch, yaw, throttle);
        if (CheckRaceEvents(delta)) return;                                        // restarted this tick
        DriveAudio(roll, pitch, yaw, throttle);
        ApplyHud("LIVE");
    }

    // Read the current stick/throttle from the pad (or keyboard fallback) and apply axis signs.
    private void ReadInput(double delta, out float roll, out float pitch, out float yaw, out float throttle)
    {
        if (Engine.GetPhysicsFrames() % 15 == 0)   // refresh joypad presence occasionally (avoids per-tick alloc)
            _hasJoypad = Input.GetConnectedJoypads().Count > 0;

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
    }

    // Armed at the start line: hold fixed and idle until the pilot gives input, then the clock
    // starts. The first ~0.4s just settles the input baseline (controller axes arrive a few frames
    // after launch, so an early baseline would misfire and let the drone drop). Returns true once
    // the race has started (flight should run this tick), false while still holding at the line.
    private bool TryStart(double delta, float roll, float pitch, float yaw, float throttle)
    {
        _armSettle += (float)delta;
        if (_armSettle < 0.4f)
        {
            _armThrottle = throttle;   // keep tracking the resting value while it settles
        }
        else
        {
            bool go = Mathf.Abs(throttle - _armThrottle) > 0.08f
                   || Mathf.Abs(roll) > 0.06f || Mathf.Abs(pitch) > 0.06f || Mathf.Abs(yaw) > 0.06f;
            if (go) { _race.Launch(); _recorder.BeginLap(); return true; }
        }
        _curThrottle = throttle; _audio.SetEffort(0f); _recorder.HideGhost();   // idle: no clock, no ghost
        ApplyHud("LIVE");
        return false;
    }

    // Integrate the flight model (substepped) and move the body, bouncing off gate bars.
    private void StepFlight(double delta, float roll, float pitch, float yaw, float throttle)
    {
        float dt = (float)delta / Substeps;
        for (int i = 0; i < Substeps; i++)
            _fm.Step(roll, pitch, yaw, throttle, dt);

        MoveDroneWithBounce();
        _curThrottle = throttle;
        _flightTime += (float)delta;
    }

    // Advance the lap clock + recording, and restart on a fatal event (touch a bar / fly past a
    // gate / hit the ground -> back to the start line, same as R). Returns true if it restarted.
    private bool CheckRaceEvents(double delta)
    {
        if (!_race.Running) return false;
        _race.Tick((float)delta);
        _recorder.Record((float)delta, _race.LapTime, _drone.GlobalPosition, _drone.GlobalTransform.Basis.GetRotationQuaternion());
        if (_gateHit || MissedGate() || _drone.GlobalPosition.Y < 0.5f) { StartRace(); return true; }
        return false;
    }

    // Drive motor audio from throttle AND stick activity, so rolls/pitches/yaws audibly rev the
    // motors (the thrust proxy saturates and would hide that). Silent at rest.
    private void DriveAudio(float roll, float pitch, float yaw, float throttle)
    {
        float stick = Mathf.Min(1f, (Mathf.Abs(roll) + Mathf.Abs(pitch) + Mathf.Abs(yaw)) / 2f);
        _audio.SetEffort(Mathf.Clamp(throttle * 0.9f + stick * 0.5f, 0f, 1f));
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
            // reflect the model velocity about the bar normal in Godot space (Z-flipped from the
            // model frame), then flip back. Bounce.Respond no-ops on a departing contact.
            var vG = new Vector3(_fm.Vel.X, _fm.Vel.Y, -_fm.Vel.Z);
            vG = Bounce.Respond(vG, col.GetNormal(), Restitution, HitFriction);
            _fm.Vel = new NVec(vG.X, vG.Y, -vG.Z);
        }
        Vector3 gp = _drone.GlobalPosition;                 // keep the model in sync with the real position
        _fm.Pos = new NVec(gp.X, gp.Y, -gp.Z);
    }

    private void ResetDrone()
    {
        _fm.Reset();
        _flightTime = 0f;
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
        _drone.ResetPhysicsInterpolation();   // teleport: don't sweep from the old pose
    }

    // (Re)connect each gate's pass-through trigger. Called on launch and after a track rebuild,
    // since LoadTrack frees the old gates (and their signals) and creates fresh ones.
    private void WireGates()
    {
        for (int i = 0; i < _arena.GateTriggers.Count; i++)
        {
            int idx = i;
            _arena.GateTriggers[i].BodyEntered += body => OnGatePassed(idx, body);
        }
    }

    private int _levelIndex;
    public string LevelName => _arena.LevelName;
    public int LevelIndex => _levelIndex;
    public int LevelCount => LevelStore.Count;
    public string LevelNameAt(int i) => LevelStore.NameAt(i);
    public float BestLapAt(int i) => LapRecorder.BestLapFor(LevelStore.IdAt(i));
    public float[] TopLapsAt(int i) => LapRecorder.TopLapsFor(LevelStore.IdAt(i));
    public bool IsBuiltInLevel(int i) => LevelStore.IsBuiltIn(i);
    public Level PreviewLevel(int i) => LevelStore.Load(LevelStore.IdAt(i));   // for the map preview

    // Main-menu Create: make a fresh user level (starter loop) and drop into the builder on it.
    public void CreateLevel()
    {
        SetLevelIndex(LevelStore.Create());
        _coord.ResumeGame();   // leave the menus, gameplay camera, unpause
        _edit.Open();          // ...and open the free-fly builder
    }

    // Delete a user level (built-ins are protected); if it was active, fall back to the first level.
    public void DeleteLevel(int index)
    {
        if (LevelStore.IsBuiltIn(index)) return;
        string delId = LevelStore.IdAt(index);
        string curId = LevelStore.IdAt(_levelIndex);
        LevelStore.Delete(delId);
        if (curId == delId) SetLevelIndex(0);
        else _levelIndex = LevelStore.IndexOf(curId);
    }

    // --- menu navigation: thin delegators to the ScreenCoordinator (called by the menu screens) ---
    public void StartGame() => _coord.StartGame();
    public void OpenMain() => _coord.OpenMain();
    public void OpenLogos() => _logoMenu.Toggle();   // main-menu L
    public void ResumeGame() => _coord.ResumeGame();
    public void OpenPause() => _coord.OpenPause();
    public void OpenHelp() => _coord.OpenHelp();
    public void CloseHelp() => _coord.CloseHelp();
    public void OpenLevels(bool fromPause) => _coord.OpenLevels(fromPause);
    public void OpenSettings(bool fromPause) => _coord.OpenSettings(fromPause);
    public void MenuBack() => _coord.MenuBack();
    public void RestartRace() => _coord.RestartRace();
    public void PlayLevel(int index) => _coord.PlayLevel(index);
    public void WatchBest(int index) => _coord.WatchBest(index);
    public void ApplyAA() => _coord.ApplyAA();
    public void RefreshSsaa() => _coord.RefreshSsaa();

    // ScreenCoordinator.IGame: the level/replay/race verbs the coordinator hands back to the game.
    void ScreenCoordinator.IGame.LoadLevel(int index) => SetLevelIndex(index);
    void ScreenCoordinator.IGame.StartPlayback() => _playback.Start();
    void ScreenCoordinator.IGame.StartRace() => StartRace();

    // Load a level (by catalogue index), re-wire its gates, load that level's records, restart.
    private void SetLevelIndex(int index)
    {
        _levelIndex = LevelStore.Wrap(index);
        string id = LevelStore.IdAt(_levelIndex);
        _arena.LoadLevel(LevelStore.Load(id));   // fires GatesChanged -> WireGates
        _recorder.SetLevel(id);
        StartRace();
    }

    private const string DevRelaunchPath = "user://dev_relaunch.txt";

    // Main-menu R (under the supervisor): hot-reload and come back on the title screen.
    public void RequestMainReload() { if (_showDebug && _devSupervised) RequestDevReload("MAIN"); }

    // Debug R (only under the StartDebug supervisor): save the reload target and quit. The supervisor
    // rebuilds the latest code and relaunches Godot; on boot "MAIN" opens the title screen, a level id
    // resumes that level.
    private void RequestDevReload(string target)
    {
        Persistence.WriteText(DevRelaunchPath, target);
        GD.Print($"[dev] reload requested (target='{target}') - quitting for the supervisor to rebuild");
        GetTree().Quit();
    }

    // Read + clear the dev-relaunch marker; returns the level id to resume into, or "".
    private static string ConsumeDevRelaunch()
    {
        if (Persistence.TryReadText(DevRelaunchPath, out string id) && id.Trim().Length > 0)
        {
            DirAccess.RemoveAbsolute(DevRelaunchPath);
            return id.Trim();
        }
        return "";
    }

    private void StartRace()
    {
        ResetDrone();          // spawn fixed in the start/finish gate
        _race.Arm();           // clock starts on first input
        _armSettle = 0f;
        _gateHit = false;
        _recorder.HideGhost();
        _recorder.BeginLap();
    }

    // True if the drone flew forward THROUGH the next expected gate's plane but outside its
    // opening (i.e. flew past it) - which can never be completed, so restart.
    private bool MissedGate()
    {
        int regular = _arena.GateTriggers.Count - 1;
        int nextIdx = _race.NextGate(regular);
        if (nextIdx >= _arena.Gates.Count) return false;
        Vector3 local = _arena.Gates[nextIdx].GlobalTransform.AffineInverse() * _drone.GlobalPosition;
        return _race.UpdateMiss(nextIdx, local.X, local.Y, local.Z);
    }

    // Fired by a gate's Area3D when a body enters it. Regular gates (1..n-1) advance the lap in
    // order; crossing the start/finish (gate 0) records the lap if all gates were cleared and
    // (re)starts the timer for the next lap.
    private void OnGatePassed(int index, Node3D body)
    {
        if (!_race.Running || body != _drone) return;
        int regular = _arena.GateTriggers.Count - 1;    // gates 1..n-1
        float lap = _race.LapTime;                       // captured before RegisterGate resets it
        GateResult result = _race.RegisterGate(index, regular);
        if (result == GateResult.FinishValid) _recorder.CompleteLap(lap);   // records the lap + best ghost
        if (result is GateResult.FinishValid or GateResult.FinishInvalid) _recorder.BeginLap();
    }

    public void SetShowDebug(bool on) => _showDebug = on;
    public bool ShowDebug => _showDebug;

    public float UiScale => Config.UiScale;
    public void ApplyUiScale(float scale)
    {
        Config.UiScale = Mathf.Clamp(scale, 0.7f, 1.6f);
        GetTree().Root.ContentScaleFactor = Config.UiScale;
        Config.Save();
    }

    // Wipe one track's saved records (from the Levels screen); reload if it's the active track.
    public void ClearRecords(int index)
    {
        string id = LevelStore.IdAt(index);
        LapRecorder.ClearRecords(id);
        if (index == _levelIndex) _recorder.SetLevel(id);
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
        _osd.DevReload = _devSupervised;
        int regular = _arena.GateTriggers.Count - 1;
        _osd.LapTime = _race.LapTime;
        _osd.LastLap = _recorder.LastLap;
        _osd.BestLap = _recorder.BestLap;
        _osd.Ranks = _recorder.Ranks;
        _osd.LevelName = _arena.LevelName;
        // rebuild the status string only when it changes (avoids a per-tick string alloc)
        int key = _race.Armed ? -1 : _race.Running ? _race.GatePassed : -2;
        if (key != _statusKey)
        {
            _statusKey = key;
            _statusText = _race.Armed ? "GO!  (throttle up)"
                        : _race.Running ? $"gate {_race.GatePassed}/{regular}" : "R to start";
        }
        _osd.RaceStatus = _statusText;
        // climb angle (pitch) and roll from the drone basis, for the artificial horizon
        _osd.PitchDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(b.Z.Y, -1f, 1f)));
        _osd.RollDeg = Mathf.RadToDeg(Mathf.Atan2(b.X.Y, b.Y.Y));
    }
}
