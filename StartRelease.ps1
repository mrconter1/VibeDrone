<#
.SYNOPSIS
    Export an OPTIMIZED (Release) build of the OpenDrone game and run it.

    Running from source (StartGame.ps1) always uses Debug C# - more GC pauses.
    This exports a standalone Release .exe (optimized C#, no debug symbols),
    which is the smooth build for actually flying / measuring performance.

    Export templates are fetched and installed automatically on first run
    (~1 GB, one time) - no editor / manual steps needed.

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

# One-time: fetch + install export templates matching this Godot version if absent.
$tplVer = "4.3.stable.mono"
$tplDir = Join-Path $env:APPDATA "Godot\export_templates\$tplVer"
if (-not (Test-Path (Join-Path $tplDir "windows_release_x86_64.exe"))) {
    Write-Host "Export templates for $tplVer missing - downloading (~1 GB, one time) ..." -ForegroundColor Yellow
    $old = $ProgressPreference; $ProgressPreference = "SilentlyContinue"
    $url = "https://github.com/godotengine/godot-builds/releases/download/4.3-stable/Godot_v4.3-stable_mono_export_templates.tpz"
    $tpz = Join-Path $env:TEMP "godot_tpl_$tplVer.tpz"
    $zip = Join-Path $env:TEMP "godot_tpl_$tplVer.zip"
    $ex  = Join-Path $env:TEMP "godot_tpl_ex_$tplVer"
    if (-not (Test-Path $tpz)) { Invoke-WebRequest -Uri $url -OutFile $tpz }
    Copy-Item $tpz $zip -Force
    if (Test-Path $ex) { Remove-Item $ex -Recurse -Force }
    Expand-Archive $zip $ex -Force
    New-Item -ItemType Directory -Force $tplDir | Out-Null
    Copy-Item "$ex\templates\*" $tplDir -Recurse -Force
    Remove-Item $zip, $ex -Recurse -Force -ErrorAction SilentlyContinue
    $ProgressPreference = $old
    Write-Host "Templates installed to $tplDir" -ForegroundColor Green
}

# Godot's export needs a solution with its ExportDebug/ExportRelease configs.
# `dotnet new sln` omits those, so generate a Godot-shaped one if absent.
$slnPath = Join-Path $GodotDir "OpenDrone.sln"
if (-not (Test-Path $slnPath)) {
    Write-Host "Generating C# solution (Godot export configs) ..." -ForegroundColor Cyan
    $guid = [guid]::NewGuid().ToString().ToUpper()
    $sln = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "OpenDrone", "OpenDrone.csproj", "{$guid}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		ExportDebug|Any CPU = ExportDebug|Any CPU
		ExportRelease|Any CPU = ExportRelease|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{$guid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{$guid}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{$guid}.ExportDebug|Any CPU.ActiveCfg = ExportDebug|Any CPU
		{$guid}.ExportDebug|Any CPU.Build.0 = ExportDebug|Any CPU
		{$guid}.ExportRelease|Any CPU.ActiveCfg = ExportRelease|Any CPU
		{$guid}.ExportRelease|Any CPU.Build.0 = ExportRelease|Any CPU
	EndGlobalSection
EndGlobal
"@
    Set-Content -Path $slnPath -Value $sln -Encoding UTF8
}

New-Item -ItemType Directory -Force (Split-Path $OutExe) | Out-Null

# Compile the C# in the ExportRelease config FIRST. Headless export otherwise can pack
# the previously-built assembly before its own compile finishes, so a code change only
# showed up on the second run. Building here guarantees the fresh DLL is present.
Write-Host "Building C# (ExportRelease) ..." -ForegroundColor Cyan
dotnet build $slnPath -c ExportRelease -v quiet | Out-Host
if ($LASTEXITCODE -ne 0) { throw "C# build (ExportRelease) failed." }

Write-Host "Exporting Release build ..." -ForegroundColor Cyan
# --headless: no editor window; --export-release: optimized C#, no debug symbols
& $Godot --headless --path $GodotDir --export-release "Windows Desktop" $OutExe
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $OutExe)) { throw "Export failed (exit $LASTEXITCODE)." }
Write-Host "Built: $OutExe" -ForegroundColor Green

if (-not $NoRun) {
    Write-Host "Launching Release build ..." -ForegroundColor Green
    & $OutExe
}
