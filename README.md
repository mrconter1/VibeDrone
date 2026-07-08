# OpenDrone

An FPV drone flight game (Godot 4 + C#) with a custom grey-box flight model tuned for a
realistic FPV feel.

The model (`godot/FlightModel.cs`) is a compact, engine-agnostic grey-box: acro-style
rate curves + a mixer-aware thrust proxy + gravity and thrust/drag on a rigid body.
Validated to ~4% position drift over 0.5 s of aggressive flight.

## Run

```powershell
.\StartGame.ps1        # build + run from source (fast iteration; Debug C#)
.\StartRelease.ps1     # export + run an optimized standalone build (smoother)
```

`.\StartGame.ps1 -Editor` opens the Godot editor. `StartRelease.ps1` auto-installs the
Godot export templates on first use.

## Controls

- Gamepad (auto-detected) or keyboard: **W/S** throttle, **arrows** pitch/roll, **Q/E** yaw
- **R** reset · **Tab** replay · **S** sound menu · **Esc** quit

## Sound menu (S)

Pauses the game; pick a motor-sound variant, hold a throttle level to audition it, and
shape the tone (low-pass / high-pass / distortion / volume). "Copy settings" puts the
current setup on the clipboard.

## Layout

| Path | What |
|---|---|
| `godot/FlightModel.cs` | portable flight model (pure `System.Numerics`) |
| `godot/DroneController.cs` | input, integration, camera, world, replay, perf log |
| `godot/MotorAudio.cs` | procedural motor audio |
| `godot/SoundMenu.cs` | in-game sound-test menu |
| `godot/Hud.cs` | FPV HUD |
