<#
.SYNOPSIS
    Build and launch the OpenDrone Godot game (the fitted the reference sim flight model).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File StartGame.ps1
    powershell -ExecutionPolicy Bypass -File StartGame.ps1 -Editor   # open the editor instead
    powershell -ExecutionPolicy Bypass -File StartGame.ps1 -NoBuild  # never build
    powershell -ExecutionPolicy Bypass -File StartGame.ps1 -Build    # force a build

    By default the build is skipped automatically when no .cs/.csproj file has changed
    since the last build, so an unchanged relaunch starts without the ~2s MSBuild step.

.NOTES
    The project defaults to the D3D12 backend on Windows (project.godot) to avoid the
    AMD Radeon 740M Vulkan startup crash. If D3D12 fails to start, the script retries
    once on Vulkan. A couple of harmless D3D12 driver log lines (PSO-cache note, exit-time
    PagedAllocator warning) are filtered from the console; all other output is shown.
#>
[CmdletBinding()]
param(
    [switch]$Editor,     # open the Godot editor instead of running the scene
    [switch]$NoBuild,    # always skip the dotnet build
    [switch]$Build,      # always build, even if nothing looks stale
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
#    Skip the ~2s MSBuild step when the built assembly is already newer than every
#    source file (.cs / .csproj) - an unchanged relaunch then starts straight away.
#    -NoBuild forces skip; -Build forces a build even if nothing looks stale.
function Test-BuildStale {
    $dll = Join-Path $GodotDir ".godot\mono\temp\bin\Debug\OpenDrone.dll"
    if (-not (Test-Path $dll)) { return $true }   # never built
    $built = (Get-Item $dll).LastWriteTimeUtc
    $newest = Get-ChildItem $GodotDir -Recurse -Include *.cs, *.csproj -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\\.godot\\' } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    return ($newest -and $newest.LastWriteTimeUtc -gt $built)
}

if (-not $NoBuild -and ($Build -or (Test-BuildStale))) {
    Write-Host "Building C# ..." -ForegroundColor Cyan
    Push-Location $GodotDir
    try { dotnet build | Out-Host; if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." } }
    finally { Pop-Location }
} else {
    Write-Host "C# unchanged - skipping build." -ForegroundColor DarkGray
}

# Known-benign D3D12 driver chatter (startup PSO note + shutdown buffer-cleanup warning).
# These are engine/driver rough edges in Godot 4.3's D3D12 backend, not game problems, so we
# drop them from the console while leaving every other line (including real errors) visible.
$Noise = @(
    'PSO caching is not implemented',
    'pipeline_cache_create',
    'Pages in use exist at exit',
    'PagedAllocator'
)

# Run Godot and echo its output minus the noise lines. Returns Godot's exit code.
function Start-Godot([string[]]$GodotArgs) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # native stderr (via 2>&1) must not abort the script
    try {
        & $Godot @GodotArgs 2>&1 | ForEach-Object {
            $line = if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { "$_" }
            foreach ($p in $Noise) { if ($line -like "*$p*") { return } }
            $line
        }
    } finally { $ErrorActionPreference = $prev }
    return $LASTEXITCODE
}

# 3. launch
if ($Editor) {
    Write-Host "Opening editor ..." -ForegroundColor Green
    & $Godot -e --path $GodotDir
} else {
    Write-Host "Launching game (Esc quit, R reset, Tab replay) ..." -ForegroundColor Green
    $code = Start-Godot @('--path', $GodotDir)
    # The project defaults to the D3D12 backend (project.godot) to avoid the AMD Radeon 740M
    # Vulkan startup crash. If D3D12 ever fails to start on some other GPU, retry once on Vulkan.
    if ($code -ne 0) {
        Write-Host "Godot exited with code $code - retrying on the Vulkan backend ..." -ForegroundColor Yellow
        Start-Godot @('--rendering-driver', 'vulkan', '--path', $GodotDir) | Out-Null
    }
}
