<#
.SYNOPSIS
    Build and launch the OpenDrone Godot game (the fitted the reference sim flight model).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File StartGame.ps1
    powershell -ExecutionPolicy Bypass -File StartGame.ps1 -Editor   # open the editor instead
    powershell -ExecutionPolicy Bypass -File StartGame.ps1 -NoBuild  # skip the C# build
#>
[CmdletBinding()]
param(
    [switch]$Editor,     # open the Godot editor instead of running the scene
    [switch]$NoBuild,    # skip the dotnet build (faster if code is unchanged)
    [string]$Godot       # explicit path to the Godot .NET executable
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$GodotDir = Join-Path $Root "godot"

# 1. locate the Godot .NET (mono) executable
if (-not $Godot) {
    $candidates = @(
        Get-ChildItem "$env:USERPROFILE\tools\godot" -Recurse -Filter "Godot_v*_mono_win64.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch "console" } | Sort-Object Name -Descending
    ) + @(
        Get-ChildItem "$env:USERPROFILE\Downloads" -Recurse -Filter "Godot_v*_mono_win64.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch "console" } | Sort-Object Name -Descending
    )
    if ($candidates.Count -gt 0) { $Godot = $candidates[0].FullName }
}
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw "Godot .NET executable not found. Pass -Godot '<path to Godot_v*_mono_win64.exe>'."
}
Write-Host "Godot: $Godot" -ForegroundColor Cyan

# 2. build the C# assembly (Godot needs it before running)
if (-not $NoBuild) {
    Write-Host "Building C# ..." -ForegroundColor Cyan
    Push-Location $GodotDir
    try { dotnet build | Out-Host; if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." } }
    finally { Pop-Location }
}

# 3. launch
if ($Editor) {
    Write-Host "Opening editor ..." -ForegroundColor Green
    & $Godot -e --path $GodotDir
} else {
    Write-Host "Launching game (Esc quit, R reset, Tab replay) ..." -ForegroundColor Green
    & $Godot --path $GodotDir
}
