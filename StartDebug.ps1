<#
.SYNOPSIS
    Dev supervisor for OpenDrone: build, launch, and re-launch on hot-reload.

.DESCRIPTION
    Runs the game in a loop. With debug mode on, pressing R in-game writes a reload
    marker and quits; this supervisor then rebuilds the C# and relaunches Godot,
    which resumes the same level. A normal quit (menu / window close) ends the loop.
    Because the game is closed while the build runs, there is never an assembly lock,
    and a failed build just relaunches the previous binary (with the errors shown).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File StartDebug.ps1
    powershell -ExecutionPolicy Bypass -File StartDebug.ps1 -Godot '<path to Godot_v*_mono_win64.exe>'
#>
[CmdletBinding()]
param([string]$Godot)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$GodotDir = Join-Path $Root "godot"

# locate the Godot .NET (mono) executable (same search as StartGame.ps1)
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

# the game's reload marker (user:// -> %APPDATA%\Godot\app_userdata\OpenDrone\)
$Marker = Join-Path $env:APPDATA "Godot\app_userdata\OpenDrone\dev_relaunch.txt"
Remove-Item $Marker -Force -ErrorAction SilentlyContinue

$env:OPENDRONE_DEV = "1"   # tells the game it is supervised (debug R hot-reloads)

do {
    Write-Host "`nBuilding C# ..." -ForegroundColor Cyan
    Push-Location $GodotDir
    try { dotnet build | Out-Host; $ok = $LASTEXITCODE -eq 0 } finally { Pop-Location }
    if (-not $ok) { Write-Host "Build failed - relaunching the previous build (fix + press R again)." -ForegroundColor Yellow }

    Write-Host "Launching (debug R = rebuild + relaunch this level) ..." -ForegroundColor Green
    & $Godot --path $GodotDir

    $reload = Test-Path $Marker   # written by debug R; consumed by the game on the next boot
} while ($reload)

Write-Host "Dev session ended." -ForegroundColor DarkGray
