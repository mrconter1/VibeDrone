@echo off
rem Build and launch the Godot OpenDrone game. Run from the repo root: fly
rem (wrapper around StartGame.ps1, which builds the C# then finds+launches Godot)
powershell -ExecutionPolicy Bypass -File "%~dp0StartGame.ps1" %*
