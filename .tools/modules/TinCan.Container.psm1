# Container images and (later) containers, through Podman. The engine is named in one place: $Engine.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 1.0
Import-Module (Join-Path $PSScriptRoot "TinCan.Common.psm1")

$ProjectRoot = Get-ProjectRoot
$Engine = "podman"

# The Podman machine (a WSL2 VM) must be running; `podman machine start` is the fix, and is left to the developer.
function Test-ContainerEngine {
    & $Engine info --format "{{.Host.OS}}" 2>&1 | Out-Null
    return $LASTEXITCODE -eq 0
}

# Builds Container/server/Containerfile around the Linux server build, tagged with the commit and :local. The ignore file is
# passed explicitly, so the build never depends on which ignore-file names the engine looks for.
# Returns $true on success; prints the PASS/FAIL line either way.
function Invoke-ServerImageBuild([string]$ServerDir) {
    if (-not (Test-ContainerEngine)) {
        Write-Host "[Image ] FAIL $Engine is not reachable; start it with: podman machine start" -ForegroundColor Red
        return $false
    }
    $commit = (& git -C $ProjectRoot rev-parse --short HEAD).Trim()
    $containerfile = Join-Path $ProjectRoot "Container/server/Containerfile"
    Write-Host "[Image ] tincan-server:$commit (+ :local)"
    & $Engine build -q -f $containerfile --ignorefile "$containerfile.containerignore" -t "tincan-server:$commit" -t "tincan-server:local" $ServerDir | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[Image ] FAIL $Engine build" -ForegroundColor Red
        return $false
    }
    Write-Host "[Image ] PASS tincan-server:$commit" -ForegroundColor Green
    return $true
}

Export-ModuleMember -Function Test-ContainerEngine, Invoke-ServerImageBuild
