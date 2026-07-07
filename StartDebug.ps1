<#
.SYNOPSIS
    Dev supervisor for OpenDrone: build, launch, and re-launch on hot-reload, with a log file.

.DESCRIPTION
    Runs the game in a loop. With debug mode on, pressing R in-game writes a reload marker and
    quits; this supervisor then rebuilds the C# and relaunches Godot, which resumes the same level.
    A normal quit ends the loop. The game is closed while the build runs, so there is never an
    assembly lock; a failed build relaunches the previous binary (errors shown + logged).

    Every step (launch / exit / marker check / build start+result / relaunch) plus the game's and
    dotnet's output is written to dev-supervisor.log next to this script.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File StartDebug.ps1
#>
[CmdletBinding()]
param([string]$Godot)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$GodotDir = Join-Path $Root "godot"
$Log = Join-Path $Root "dev-supervisor.log"

function Log([string]$msg) {
    $line = "{0}  {1}" -f (Get-Date -Format 'HH:mm:ss.fff'), $msg
    Add-Content -Path $Log -Value $line
    Write-Host $line -ForegroundColor DarkCyan
}

"=== OpenDrone dev session  $(Get-Date) ===" | Set-Content -Path $Log

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
Log "Godot:  $Godot"

# the game's reload marker (user:// -> %APPDATA%\Godot\app_userdata\OpenDrone\)
$Marker = Join-Path $env:APPDATA "Godot\app_userdata\OpenDrone\dev_relaunch.txt"
Log "marker: $Marker"
Remove-Item $Marker -Force -ErrorAction SilentlyContinue

$env:OPENDRONE_DEV = "1"        # tells the game it is supervised (debug R hot-reloads)
$ErrorActionPreference = "Continue"   # native stderr via 2>&1 must not abort the loop

$iteration = 0
do {
    $iteration++
    Log "---- iteration $iteration ----"

    Log "BUILD start"
    Push-Location $GodotDir
    dotnet build 2>&1 | Tee-Object -FilePath $Log -Append | Out-Host
    $ok = $LASTEXITCODE -eq 0
    Pop-Location
    Log ("BUILD result: " + $(if ($ok) { "OK" } else { "FAILED (launching previous build - fix and press R again)" }))

    Log "LAUNCH game"
    & $Godot --path $GodotDir 2>&1 | Tee-Object -FilePath $Log -Append | Out-Host
    Log "GAME exited (code $LASTEXITCODE)"

    $reload = Test-Path $Marker    # written by debug R, consumed by the game on the next boot
    Log ("reload marker present: $reload")
} while ($reload)

Log "session ended"
