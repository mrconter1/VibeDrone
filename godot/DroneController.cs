using System.Collections.Generic;
using System.Runtime;
using Godot;
using OpenDrone;
using NVec = System.Numerics.Vector3;
using NQuat = System.Numerics.Quaternion;

// Drives the portable FlightModel and applies the result to this Node3D, OR replays a
// recorded flight. Both paths go through ONE verified model->Godot conversion
// (ToGodot), so if the replay looks correct the live model is guaranteed consistent.
// The model integrates orientation as a proper quaternion (in the model's frame); we
// convert via a forward/up basis - no fragile per-axis sequential rotation.
public partial class DroneController : Node3D, ScreenCoordinator.IGame
{
    // FOV 95 deg ~ an FPV cam's mid range (90-100); lower than a real cam's 120+ to cut
    // the rectilinear edge-stretch warping. ~30 deg uptilt (freestyle 25-35), nose-mounted.
    [Export] public float CameraFovDeg = 95f;
    [Export] public float CameraTiltDeg = 30f;
    [Export] public float CameraForward = 0.08f;   // metres in front of CG
    [Export] public float CameraUp = 0.03f;        // metres above CG
    [Export] public int Substeps = 1;   // physics already ticks at 250 Hz (project.godot)
    [Export] public float DroneRadius = 0.15f;   // ~5" quad half-width, for gate collision
    [Export] public float Restitution = 0.18f;   // normal rebound (low: real quads barely bounce)
    [Export] public float HitFriction = 0.55f;   // tangential speed scrubbed off on a hit (deflect, not slide)
    [Export] public float MuzzleSpeed = 22f;      // ball launch speed (the axis-7 switch fires balls)

    private readonly FlightModel _fm = new();
    private readonly FlightInput _input = new();   // gamepad/keyboard -> Sticks each physics tick
    private CharacterBody3D _drone = null!;   // kinematic body so it can collide/bounce off gates
    private Camera3D _cam = null!;
    private Hud _osd = null!;
    private MotorAudio _audio = null!;
    private float _flightTime;
    private float _curThrottle;
    private HudPresenter _hud = null!;   // marshals live state into the HUD each tick
    private SessionLog _sessionLog = null!;
    private Arena _arena = null!;

    // lap timing state
    private readonly RaceState _race = new();   // armed/running/lap-time/gate-progress/miss tracking
    private float _armSettle;      // time armed on the pad before lift-off is allowed
    private bool _gateHit;         // drone touched a gate bar this frame
    private bool _showDebug;       // FPS/FOV/etc overlay (off by default, toggled from the Esc menu)
    private bool _devSupervised;   // launched under StartDebug -> debug R hot-reloads

    // game mode: Race (timed, resets on crash/miss) vs Free Fly (no clock, no resets)
    private enum GameMode { Race, FreeFly }
    private GameMode _mode = GameMode.Race;
    private float _idleTime;       // seconds since meaningful pilot input (race auto-reset)
    private float _prevThrottle;   // to detect throttle changes as "input"
    private MeshInstance3D _startPad = null!;   // launch pad at the start; vanishes on lift-off
    private float _padRestY;                     // CG rest height on the launch pad (StepGround)

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
    private SoundMenu _sound = null!;

    public override void _Ready()
    {
        // prefer short GC pauses over throughput: fewer gen2 stalls = fewer missed frames
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        Config.Load();                                       // UI scale + blur + AA preferences
        _mode = Config.LastMode == 1 ? GameMode.FreeFly : GameMode.Race;   // resume last mode (main-menu shows it)
        LegacyMigration.Run();                               // old index-keyed records -> stable-id files
        _devSupervised = OS.GetEnvironment("OPENDRONE_DEV") == "1";   // StartDebug sets this
        if (_devSupervised) _showDebug = true;                        // debug on by default under StartDebug
        GetTree().Root.ContentScaleFactor = Config.UiScale;  // scale the whole UI (fonts + sizes)
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);

        _sessionLog = new SessionLog();
        AddChild(_sessionLog);

        BuildWorld();
        BuildOverlays();
        BuildMenus();
        BootIntoLevelOrMenu();
    }

    // The world + the drone body and its nose-mounted FPV camera.
    private void BuildWorld()
    {
        _arena = new Arena();
        _arena.GatesChanged += WireGates;   // re-wire pass-through triggers whenever gates rebuild
        AddChild(_arena);   // builds the world; a level is loaded below

        _drone = new CharacterBody3D();
        AddChild(_drone);
        _drone.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = DroneRadius } });
        _cam = new Camera3D { Fov = CameraFovDeg };
        _drone.AddChild(_cam);
        _cam.Position = new Vector3(0f, CameraUp, CameraForward);   // nose mount (drone local frame)
        _cam.RotationDegrees = new Vector3(Config.CameraTilt, 180f, 0f);   // uptilt (Settings > Drone)

        _startPad = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.06f, 0.9f) },   // small, thin launch pad
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.24f, 0.80f, 0.96f, 0.85f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission = new Color(0.24f, 0.80f, 0.96f),
                EmissionEnergyMultiplier = 1.4f,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(_startPad);
    }

    // HUD, audio, the ghost/lap recorder + its HUD presenter, and the replay theatre.
    private void BuildOverlays()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        _osd = new Hud();
        layer.AddChild(_osd);

        _audio = new MotorAudio();
        AddChild(_audio);

        _sound = new SoundMenu();
        _sound.Setup(_audio);   // set before AddChild: AddChild runs _Ready synchronously
        AddChild(_sound);       // M opens/closes the dev sound test

        _recorder = new LapRecorder();
        AddChild(_recorder);   // owns the ghost + trail + best-lap board, loads on _Ready

        _hud = new HudPresenter(_osd, _drone, _fm, _cam, _audio, _race, _recorder, _arena);

        _playback = new PlaybackController();
        _playback.Setup(_recorder, _cam);
        AddChild(_playback);

        BuildFireSound();   // pooled "bop" voices for the ball launcher
    }

    // The full-screen menu system (orbit camera, blur backdrop, screens + coordinator) and the editor.
    private void BuildMenus()
    {
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
        _settings.Setup(this, _audio, _sound);
        AddChild(_settings);
        _help = new HelpOverlay();
        _help.Setup(this);
        AddChild(_help);
        _pause = new PauseMenu();
        _pause.Setup(this);
        AddChild(_pause);

        _coord = new ScreenCoordinator(this, GetTree(), GetViewport(), _cam,
            _mainMenu, _levelSelect, _settings, _pause, _help, _backdrop, _menuCam, _osd);
        _coord.ApplyAA();   // MSAA + FXAA (fixes edge/checker shimmer)

        AddChild(new CursorAutoHide());   // hide the menu cursor until the mouse moves, then after idle

        _edit = new EditController();
        _edit.Setup(this, _cam, _audio, _arena);   // E toggles a Minecraft-style free-fly camera (pauses the game)
        AddChild(_edit);
    }

    // A dev rebuild+relaunch (debug R) drops straight back into the level it was on; otherwise
    // load the first level behind the title screen.
    private void BootIntoLevelOrMenu()
    {
        string devLevel = DevReload.Consume();
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
        if (ev is InputEventJoypadMotion m) _input.FeedAxis((int)m.Axis, m.AxisValue);
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
            if (_showDebug && _devSupervised) DevReload.Request(GetTree(), LevelStore.IdAt(_levelIndex));   // hot-reload this level
            else { _sessionLog.Mark("race start"); StartRace(); }
        }
        else if (ev is InputEventKey { Pressed: true, Keycode: Key.H })
        {
            OpenHelp();
        }
    }

    // Visual-only (ghost + trail) at render rate, not the 250 Hz physics rate.
    public override void _Process(double delta)
    {
        if (_race.Running) _recorder.UpdateVisuals(_race.LapTime, true, _drone.GlobalPosition);
        else _recorder.HideGhost();
        UpdateBallLauncher((float)delta);
    }

    private float _fireCooldown;
    private AudioStreamPlayer[] _firePool = null!;   // pooled "bop" voices so rapid fire overlaps
    private int _fireVoice;

    // Fire balls out of the nose while the axis-7 switch is at its -1 detent (idle/default = no fire).
    private void UpdateBallLauncher(float delta)
    {
        _fireCooldown -= delta;
        Godot.Collections.Array<int> pads = Input.GetConnectedJoypads();
        if (pads.Count == 0) return;
        float sw = Input.GetJoyAxis(pads[0], (JoyAxis)7);
        if (sw < -0.5f && _fireCooldown <= 0f)
        {
            FireBall();
            _fireCooldown = 0.12f;
        }
    }

    private void FireBall()
    {
        Vector3 fwd = -_cam.GlobalBasis.Z;                       // where the nose camera looks
        var ball = new Ball();
        AddChild(ball);
        ball.GlobalPosition = _drone.GlobalPosition + fwd * 0.35f;
        var droneVel = new Vector3(_fm.Vel.X, _fm.Vel.Y, -_fm.Vel.Z);   // model -> Godot (Z flip)
        ball.LinearVelocity = droneVel + fwd * MuzzleSpeed;      // inherit the drone's velocity + muzzle
        ball.AddCollisionExceptionWith(_drone);

        _firePool[_fireVoice].PitchScale = 0.9f + GD.Randf() * 0.2f;   // slight variation per shot
        _firePool[_fireVoice].Play();
        _fireVoice = (_fireVoice + 1) % _firePool.Length;
    }

    // A short percussive "bop" launch sound, generated procedurally (no asset files): a sine with a
    // fast pitch-drop and quick decay. Pooled into a few voices so back-to-back shots don't cut off.
    private void BuildFireSound()
    {
        const int rate = 22050;
        int n = (int)(0.10f * rate);
        var data = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 42f);
            float freq = 700f * Mathf.Exp(-t * 30f) + 90f;   // pitch drops ~790 -> ~120 Hz
            float s = Mathf.Sin(Mathf.Tau * freq * t) * env * 0.6f;
            short v = (short)(Mathf.Clamp(s, -1f, 1f) * 32767f);
            data[i * 2] = (byte)(v & 0xff);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
        var wav = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = data };
        _firePool = new AudioStreamPlayer[4];
        for (int i = 0; i < _firePool.Length; i++)
        {
            _firePool[i] = new AudioStreamPlayer { Stream = wav, VolumeDb = -5f };
            AddChild(_firePool[i]);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Sticks s = _input.Sample(delta);
        if (_mode == GameMode.FreeFly)                    // free fly: just fly, no clock/resets
        {
            StepFlight(delta, s);
            DriveAudio(s);
            ApplyHud("FREE");
            return;
        }
        if (_race.Armed)
        {
            float dt0 = (float)delta;
            _armSettle += dt0;

            // rest on the launch pad: tip it with the sticks (controllable up to ~90 deg) and it falls
            // back to level when you release.
            _fm.StepGround(s.Roll, s.Pitch, s.Yaw, s.Throttle, dt0, _padRestY);
            ApplyTransform(_fm.Pos, _fm.Rot);

            // leave the pad + start the clock: rise clear of it (throttle up), OR lean past 90 deg
            // (up-vector below horizontal) so an over-lean falls forward instead of flipping.
            float upY = NVec.Transform(NVec.UnitY, _fm.Rot).Y;
            if (_armSettle >= 0.4f && (_fm.Pos.Y > _padRestY + 0.05f || upY < 0f))
            {
                _startPad.Visible = false;
                _race.Launch();
                _recorder.BeginLap();
            }

            if (_race.Armed)
            {
                _curThrottle = s.Throttle;
                _recorder.HideGhost();
                DriveAudio(s);
                ApplyHud("LIVE");
                return;
            }
        }

        StepFlight(delta, s);
        if (CheckRaceEvents(delta)) return;               // restarted this tick
        if (AutoResetIdle(delta, s)) return;              // reset after a spell of no input
        DriveAudio(s);
        ApplyHud("LIVE");
    }

    // In a running race, if auto-reset is on and the pilot gives no input for the configured time
    // (e.g. after a crash), respawn at the start line. Returns true if it reset this tick.
    private bool AutoResetIdle(double delta, Sticks s)
    {
        if (!Config.AutoReset || !_race.Running) { _idleTime = 0f; _prevThrottle = s.Throttle; return false; }
        bool active = Mathf.Abs(s.Roll) > 0.06f || Mathf.Abs(s.Pitch) > 0.06f || Mathf.Abs(s.Yaw) > 0.06f
                   || Mathf.Abs(s.Throttle - _prevThrottle) > 0.02f;
        _prevThrottle = s.Throttle;
        if (active) { _idleTime = 0f; return false; }
        _idleTime += (float)delta;
        if (_idleTime >= Config.AutoResetSeconds) { StartRace(); return true; }
        return false;
    }

    // Integrate the flight model (substepped) and move the body, bouncing off gate bars.
    private void StepFlight(double delta, Sticks s)
    {
        float dt = (float)delta / Substeps;
        for (int i = 0; i < Substeps; i++)
            _fm.Step(s.Roll, s.Pitch, s.Yaw, s.Throttle, dt);

        MoveDroneWithBounce();
        _curThrottle = s.Throttle;
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
    private void DriveAudio(Sticks s)
    {
        float stick = Mathf.Min(1f, (Mathf.Abs(s.Roll) + Mathf.Abs(s.Pitch) + Mathf.Abs(s.Yaw)) / 2f);
        _audio.SetEffort(Mathf.Clamp(s.Throttle * 0.9f + stick * 0.5f, 0f, 1f));
    }

    // --- the single verified model(LH, Y up, +Z fwd) -> Godot(RH, Y up, -Z fwd) conversion ---
    private static void ToGodot(NVec pU, NQuat qU, out Vector3 pos, out Basis basis)
    {
        pos = new Vector3(pU.X, pU.Y, -pU.Z);                 // flip Z (LH->RH)
        NVec fU = NVec.Transform(new NVec(0, 0, 1), qU);      // body forward (model frame)
        NVec uU = NVec.Transform(new NVec(0, 1, 0), qU);      // body up (model frame)
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

        _startPad.Visible = false;             // no visible platform
        if (_mode == GameMode.Race) _padRestY = pos.Y;   // hold the CG here on an invisible launch pad
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

    // Main-menu Start: resume the last session - same track and mode as last time, and in Free Fly
    // pick up at the position/orientation you left off.
    public void StartGame()
    {
        _mode = Config.LastMode == 1 ? GameMode.FreeFly : GameMode.Race;
        int idx = LevelStore.IndexOf(Config.LastLevelId);
        _coord.PlayLevel(idx < 0 ? 0 : idx);          // load the level (mode-aware start) + unpause
        if (_mode == GameMode.FreeFly) RestoreFreePose();
    }

    public void OpenMain() { SaveSession(); _coord.OpenMain(); }
    public void ResumeGame() => _coord.ResumeGame();
    public void OpenPause() { SaveSession(); _coord.OpenPause(); }

    // Remember the current track + mode (+ the live Free-Fly pose) so Start can resume it.
    private void SaveSession()
    {
        Config.LastLevelId = LevelStore.IdAt(_levelIndex);
        Config.LastMode = _mode == GameMode.FreeFly ? 1 : 0;
        if (_mode == GameMode.FreeFly)
        {
            Config.HasFreePose = true;
            Config.FreePoseLevel = Config.LastLevelId;
            Config.FreePose = new[] { _fm.Pos.X, _fm.Pos.Y, _fm.Pos.Z, _fm.Rot.X, _fm.Rot.Y, _fm.Rot.Z, _fm.Rot.W };
        }
        else Config.HasFreePose = false;
        Config.Save();
    }

    // Free Fly: drop the drone back at the saved model pose (only if it's for this level).
    private void RestoreFreePose()
    {
        if (!Config.HasFreePose || Config.FreePoseLevel != LevelStore.IdAt(_levelIndex)) return;
        float[] f = Config.FreePose;
        _fm.Pos = new NVec(f[0], f[1], f[2]);
        _fm.Rot = NQuat.Normalize(new NQuat(f[3], f[4], f[5], f[6]));
        _fm.Vel = NVec.Zero;
        ApplyTransform(_fm.Pos, _fm.Rot);
        _drone.ResetPhysicsInterpolation();
    }
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

    // Main-menu R (under the supervisor): hot-reload and come back on the title screen.
    public void RequestMainReload() { if (_showDebug && _devSupervised) DevReload.Request(GetTree(), "MAIN"); }

    private void StartRace()
    {
        ResetDrone();          // spawn fixed in the start/finish gate
        _idleTime = 0f;
        _gateHit = false;
        _recorder.HideGhost();
        if (_mode == GameMode.FreeFly) return;   // free fly: fly immediately, no clock/ghost
        _race.Arm();           // clock starts on first input
        _armSettle = 0f;
        _recorder.BeginLap();
    }

    // --- game mode (Race / Free Fly), switched from the pause menu ---
    public string GameModeName => _mode == GameMode.FreeFly ? "Free Fly" : "Race";

    // Main-menu mode toggle (no respawn - Start applies it): flips the mode and persists it.
    public void ToggleMenuMode()
    {
        _mode = _mode == GameMode.Race ? GameMode.FreeFly : GameMode.Race;
        Config.LastMode = _mode == GameMode.FreeFly ? 1 : 0;
        Config.Save();
    }

    public void CycleGameMode(int dir)
    {
        _mode = _mode == GameMode.Race ? GameMode.FreeFly : GameMode.Race;   // two modes: toggle
        Config.LastMode = _mode == GameMode.FreeFly ? 1 : 0;
        Config.Save();
        StartRace();   // respawn for the new mode (armed for Race, free for Free Fly)
    }

    // --- drone FPV camera uptilt (Settings > Drone) ---
    public float CameraTilt => Config.CameraTilt;

    public void ApplyCameraTilt(float deg)
    {
        Config.CameraTilt = Mathf.Clamp(deg, 0f, 60f);
        _cam.RotationDegrees = new Vector3(Config.CameraTilt, 180f, 0f);
        Config.Save();
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

    // Editing changes the layout, so the current level's saved runs no longer match - the editor
    // warns before entering and clears them on confirm.
    public bool CurrentLevelHasRecords() => LapRecorder.TopLapsFor(LevelStore.IdAt(_levelIndex)).Length > 0;

    public void ClearCurrentLevelRecords()
    {
        string id = LevelStore.IdAt(_levelIndex);
        LapRecorder.ClearRecords(id);
        _recorder.SetLevel(id);
    }


    private void ApplyHud(string mode) => _hud.Update(mode, _curThrottle, _flightTime, _showDebug, _devSupervised);
}
