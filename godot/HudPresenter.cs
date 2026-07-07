using Godot;
using OpenDrone;

// Marshals live game state into the HUD each frame. Kept out of DroneController so the flight loop
// just hands over the per-tick scalars and this owns the read-from-everything view assembly - plus
// the cached race-status string, rebuilt only when it changes to avoid a per-tick alloc.
public sealed class HudPresenter
{
    private readonly Hud _osd;
    private readonly Node3D _drone;
    private readonly FlightModel _fm;
    private readonly Camera3D _cam;
    private readonly MotorAudio _audio;
    private readonly RaceState _race;
    private readonly LapRecorder _recorder;
    private readonly Arena _arena;

    private int _statusKey = int.MinValue;   // caches the race-status string
    private string _statusText = "";

    public HudPresenter(Hud osd, Node3D drone, FlightModel fm, Camera3D cam, MotorAudio audio,
        RaceState race, LapRecorder recorder, Arena arena)
    {
        _osd = osd; _drone = drone; _fm = fm; _cam = cam; _audio = audio;
        _race = race; _recorder = recorder; _arena = arena;
    }

    public void Update(string mode, float throttle, float flightTime, bool showDebug, bool devReload)
    {
        Basis b = _drone.GlobalTransform.Basis;
        _osd.Mode = mode;
        _osd.Speed = _fm.Vel.Length();
        _osd.Alt = _drone.GlobalPosition.Y;
        _osd.Throttle = throttle;
        _osd.TimeSec = flightTime;
        _osd.Fov = _cam.Fov;
        _osd.Fps = (float)Engine.GetFramesPerSecond();
        _osd.Sound = _audio.CurrentName;
        _osd.ShowDebug = showDebug;
        _osd.DevReload = devReload;
        _osd.FreeFly = mode == "FREE";
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
