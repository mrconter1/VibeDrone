<#
.SYNOPSIS
    Export an OPTIMIZED (Release) build of the OpenDrone game and run it.

    Running from source (StartGame.ps1) always uses Debug C# - more GC pauses.
    This exports a standalone Release .exe (optimized C#, no debug symbols),
    which is the smooth build for actually flying / measuring performance.

    ONE-TIME PREREQUISITE: export templates for this Godot version must be
    installed. If they are missing, open the editor once:
        .\StartGame.ps1 -Editor
    then: Editor menu -> Manage Export Templates -> Download and Install.
    (~1 GB, must match Godot 4.3.stable.mono.)

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File StartRelease.ps1
    powershell -ExecutionPolicy Bypass -File StartRelease.ps1 -NoRun   # build only
#>
[CmdletBinding()]
param(
    [switch]$NoRun,
    [string]$Godot
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$GodotDir = Join-Path $Root "godot"
$OutExe = Join-Path $Root "build\OpenDrone.exe"

if (-not $Godot) {
    $Godot = (Get-ChildItem "$env:USERPROFILE\tools\godot" -Recurse -Filter "Godot_v*_mono_win64.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notmatch "console" } | Sort-Object Name -Descending | Select-Object -First 1).FullName
}
if (-not $Godot -or -not (Test-Path $Godot)) { throw "Godot .NET executable not found. Pass -Godot '<path>'." }

$templates = Join-Path $env:APPDATA "Godot\export_templates"
if (-not (Test-Path $templates) -or -not (Get-ChildItem $templates -ErrorAction SilentlyContinue)) {
    Write-Host "No export templates installed." -ForegroundColor Yellow
    Write-Host "One-time setup: run  .\StartGame.ps1 -Editor  then Editor -> Manage Export Templates -> Download and Install." -ForegroundColor Yellow
    throw "Export templates missing."
}

New-Item -ItemType Directory -Force (Split-Path $OutExe) | Out-Null

Write-Host "Exporting Release build ..." -ForegroundColor Cyan
# --headless: no editor window; --export-release: optimized C#, no debug symbols
& $Godot --headless --path $GodotDir --export-release "Windows Desktop" $OutExe
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $OutExe)) { throw "Export failed (exit $LASTEXITCODE)." }
Write-Host "Built: $OutExe" -ForegroundColor Green

if (-not $NoRun) {
    Write-Host "Launching Release build ..." -ForegroundColor Green
    & $OutExe
}
