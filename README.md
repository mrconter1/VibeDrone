# OpenDrone

Tooling to extract **exact flight flight logs** from the reference sim -- per-physics-tick
position, orientation, velocity, acceleration, and the **control input that produced
it** -- so the flight model can be cloned for our own game.

See [`FINDINGS.md`](FINDINGS.md) for the development notes that drive this design.

## TL;DR of the approach

the reference sim is a Unity **Mono** build (fully decompilable, the tooling-moddable) and its
flight feel is a **an FPV quad emulation** (PID + rates) driving a Unity `Rigidbody`.
It also ships **Anti-Cheat Toolkit**, so we **only read, never write**, and capture
**offline / free-fly only**.

```
  the reference sim (Mono)                     this repo
  ┌────────────────────┐   UDP JSON     ┌──────────────────────┐
  │ the tooling plugin      │ ───────────▶  │ the logger.py  │ ─▶ logs/run_*.csv
  │  reads VDroneCtrl    │  :9001        └──────────────────────┘
  │  Rigidbody + inputs  │
  └────────────────────┘
  (optional) transmitter ─▶ the capture tool.py ─▶ logs/sticks_*.csv  (raw sticks)
```

Two input sources, by design:
- **Plugin `in_*` fields** = the *exact processed* input the sim fed to physics (best).
- **`the capture tool.py`** = *raw* sticks from the OS, no hook needed; align via host `wall_t`.

## Layout

| Path | What |
|---|---|
| `mod/Plugin.cs` | the tooling 5 plugin: reads drone Rigidbody + inputs each `FixedUpdate`, sends UDP JSON |
| `mod/OpenDroneTelemetry.csproj` | build file (edit `GameDir`) |
| `python/the logger.py` | UDP -> flat CSV |
| `python/the capture tool.py` | raw controller -> CSV (pygame) |
| `FINDINGS.md` | engine/anti-cheat/class-name findings |

## Setup

Two scripts do everything. Run from the repo root in **PowerShell**.

### 1. Install (one-time)
```powershell
powershell -ExecutionPolicy Bypass -File scripts\install.ps1
```
This downloads + installs the tooling 5 (x64 Mono) and UnityExplorer into the the reference sim
folder, builds the flight logs plugin and drops it in `the tooling\plugins\`, and creates a
Python `.venv` with the deps. Pass `-GameDir "<path>"` if your install isn't at the
default path. Requires the **.NET SDK** (`dotnet`) and **Python**.

### 2. Start a capture session
```powershell
powershell -ExecutionPolicy Bypass -File scripts\start.ps1
# add -Controller to also log raw transmitter sticks
```
This opens the flight logs logger in its own window and launches the reference sim.

### 3. Activate / fly
1. In the reference sim choose **SINGLE PLAYER / FREE-FLY** (offline -- never online/leaderboard).
2. The plugin auto-attaches each physics tick; you'll see sample counts tick up in the
   logger window. Fly around for a long time, varied: punches, rolls, flips, coasting,
   hovering -- the model is only as rich as the manoeuvres you feed it.
3. Press **F7** any time to open UnityExplorer and confirm the `VDroneCtrl` Rigidbody /
   input field names (only needed if some `in_*` columns are missing).
4. Close the logger window (Ctrl+C) when done -> `logs\run_*.csv`.

If `in_*` columns are missing or wrong, edit
`<app>\the tooling\config\se.rektron.opendrone.flight logs.cfg`
(`type_name`, `rigidbody_field`, `input_fields`) and relaunch. On drone spawn the plugin
also writes `<app>\opendrone_fields.txt` listing every numeric `VDroneCtrl` field, to help
identify the real input field names.

**Altitude gate:** `[capture] min_altitude` (default 10 m) means samples are only captured
above that world-Y height, so ground contact and crashes don't pollute the data. Set `0`
to capture always.

### 4. Fit the model
```powershell
.\.venv\Scripts\python python\the fitter.py logs\run_*.csv
```

## CSV columns

`t, dt` (sim time), `pos_{x,y,z}`, `rot_{x,y,z,w}` (quaternion), `euler_{x,y,z}`,
`vel_{x,y,z}`, `acc_{x,y,z}`, `angvel_{x,y,z}`, `angacc_{x,y,z}`,
`in_*` (whatever input fields resolved), `wall_t` (host clock for cross-stream sync).

Acceleration is derived as d(velocity)/dt (Unity exposes no direct accel).

## Cloning the model (next steps, not yet built)

With input->motion pairs you can fit/replicate the reference sim's behaviour. Because it
emulates an FPV quad, the realistic target model is:
1. **Input shaping**: an FPV quad rates + expo + throttle curve (map raw stick -> setpoint).
2. **Rate controller**: PID per axis tracking the angular-rate setpoint.
3. **Thrust + drag**: motor thrust model + aero drag on a rigid body.

Fit each stage against the captured CSV (e.g. recover rate curves from
`in_*` vs `angvel`, drag from `vel` vs `acc` during coast).

## Safety / scope

Read-only, offline, single-player, for studying physics for our own game. Do not run
during ranked/leaderboard/online sessions (ACTk anti-cheat). Don't redistribute
the reference sim assets.
