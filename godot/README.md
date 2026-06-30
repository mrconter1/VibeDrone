# OpenDrone - Godot 4 (C#) sim

FPV drone sim driven by the flight model fitted from the reference sim flight logs.
The physics is `FlightModel.cs` - a pure C# (`System.Numerics`) port of
`../python/the model module`. `DroneController.cs` runs it each physics tick and
applies the transform (Godot's RigidBody is NOT used - the model is authoritative).

## Run / edit
1. Install **Godot 4.x .NET edition** (the C# build) and the **.NET 8 SDK**.
2. Open this `godot/` folder in Godot (it reads `project.godot`).
3. Press **Play**. Esc quits, R resets. A transmitter is auto-detected; otherwise
   keyboard: W/S throttle, arrows pitch/roll, Q/E yaw.

If a stick is reversed, flip its sign in the Inspector on the `Main` node
(`Sign Roll/Pitch/Yaw/Throttle`) or adjust axis indices. Tune `Camera Tilt Deg`.

## Export a single self-contained .exe (no user install)
Project > Export > add a **Windows Desktop** preset, enable
**Embed PCK**, then Export Project. With the .NET export template the runtime is
bundled, so end users just double-click one file. (Same flow targets a **Web**
build later for a zero-install browser version.)

## Updating the model
Re-fit in Python and copy the numbers from `logs/the parameter set` into the fields at
the top of `FlightModel.cs` (or load the JSON at runtime). Keep `FlightModel.cs`
in sync with `python/the model module` - they are the same model.

## Coordinate note
The model uses the reference sim's convention (Y up, +Z forward). Godot is right-handed
with -Z forward, so a stick direction or the camera yaw may need a sign flip -
that's what the `Sign*` exports and the camera's 180 yaw handle.
