# OpenDrone - Godot 4 (C#) sim

FPV drone sim driven by a custom grey-box flight model. The physics is
`FlightModel.cs` - a pure C# (`System.Numerics`) module. `DroneController.cs` runs it
each physics tick and applies the transform (Godot's RigidBody is NOT used - the model
is authoritative).

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
The model's constants live at the top of `FlightModel.cs` - tune them there.

## Coordinate note
The model uses a Y-up, +Z-forward convention. Godot is right-handed with -Z forward, so
a stick direction or the camera yaw may need a sign flip - that's what the `Sign*`
exports and the camera's 180 yaw handle.
