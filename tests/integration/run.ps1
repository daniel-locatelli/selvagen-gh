# Launch Rhino with the bootstrap Grasshopper file, wait for Cordyceps,
# then run pytest. Assumes Rhino 8 is installed at the default location.
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path "$PSScriptRoot/../.."
$bootstrap = Resolve-Path "$PSScriptRoot/bootstrap.gh"
$rhinoExe = "C:\Program Files\Rhino 8\System\Rhino.exe"

if (-not (Test-Path $rhinoExe)) {
    throw "Rhino 8 not found at $rhinoExe. Edit tests/integration/run.ps1 to point at your install."
}

# Launch Rhino+GH if not already running with Cordyceps responding
$cordycepsUrl = $env:CORDYCEPS_URL
if (-not $cordycepsUrl) { $cordycepsUrl = "http://localhost:26929/mcp" }

function Test-CordycepsAlive {
    try {
        $r = Invoke-WebRequest -Uri $cordycepsUrl -Method POST -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
        return $true
    } catch { return $false }
}

if (-not (Test-CordycepsAlive)) {
    Write-Host "Launching Rhino with bootstrap.gh..."
    Start-Process -FilePath $rhinoExe -ArgumentList "/runscript=`"_-Grasshopper _Open `"$bootstrap`" _Enter`""
    Write-Host "Waiting for Cordyceps server..."
    $waited = 0
    while (-not (Test-CordycepsAlive)) {
        Start-Sleep -Seconds 2
        $waited += 2
        if ($waited -ge 60) { throw "Cordyceps did not come up within 60 seconds." }
    }
}

# Set up Python venv if missing
$venv = Join-Path $PSScriptRoot ".venv"
if (-not (Test-Path $venv)) {
    Write-Host "Creating venv..."
    python -m venv $venv
    & "$venv/Scripts/pip.exe" install -r "$PSScriptRoot/requirements.txt"
}

& "$venv/Scripts/python.exe" -m pytest "$PSScriptRoot/scenarios" -v
