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

# Builds Container/server/Containerfile around a Linux server build ($ServerDir) as $Image, tagged with the commit and :local. The ignore file is
# passed explicitly, so the build never depends on which ignore-file names the engine looks for.
# Returns $true on success; prints the PASS/FAIL line either way.
function Invoke-ServerImageBuild([string]$ServerDir, [string]$Image = "tincan-server") {
    if (-not (Test-ContainerEngine)) {
        Write-Host "[Image ] FAIL $Engine is not reachable; start it with: podman machine start" -ForegroundColor Red
        return $false
    }
    $commit = (& git -C $ProjectRoot rev-parse --short HEAD).Trim()
    $containerfile = Join-Path $ProjectRoot "Container/server/Containerfile"
    Write-Host "[Image ] ${Image}:$commit (+ :local)"
    & $Engine build -q -f $containerfile --ignorefile "$containerfile.containerignore" -t "${Image}:$commit" -t "${Image}:local" $ServerDir | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[Image ] FAIL $Engine build" -ForegroundColor Red
        return $false
    }
    Write-Host "[Image ] PASS ${Image}:$commit" -ForegroundColor Green
    return $true
}

# A user-defined network, so containers reach each other by name (the bots join "<server name>:7777") without going
# through the host.
function Initialize-ContainerNetwork([string]$Name) {
    & $Engine network exists $Name 2>$null
    if ($LASTEXITCODE -ne 0) { & $Engine network create $Name | Out-Null }
}

# Starts one game instance from a server image with explicit limits, the Unity player as entrypoint (so the same image
# runs a server or a headless client), and $HostFolder mounted at /perf for its report. Returns the container id.
function Start-GameContainer {
    param(
        [string]$Name, [string]$Image, [string[]]$Arguments, [string]$Network,
        [double]$Cpus, [string]$CpusetCpus, [string]$Memory, [string]$HostFolder, [string[]]$Publish = @()
    )
    & $Engine rm -f $Name 2>$null | Out-Null
    $run = @("run", "-d", "--name", $Name, "--network", $Network, "--cpus", "$Cpus", "--cpuset-cpus", $CpusetCpus,
        "--memory", $Memory, "-v", "${HostFolder}:/perf", "--entrypoint", "./TinCanServer.x86_64")
    foreach ($port in $Publish) { $run += @("-p", $port) }
    $run += $Image
    $run += @("-logFile", "-")
    $run += $Arguments
    $id = & $Engine @run 2>&1
    if ($LASTEXITCODE -ne 0) { Stop-Unusable "$Engine could not start ${Name}: $id" }
    return "$id".Trim()
}

function Test-ContainerRunning([string]$Name) {
    $state = & $Engine inspect -f "{{.State.Running}}" $Name 2>$null
    return "$state".Trim() -eq "true"
}

# Waits until a container's log has a line matching $Pattern; returns $false at the timeout or if it exits first.
function Wait-ContainerLog([string]$Name, [string]$Pattern, [int]$TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ((& $Engine logs $Name 2>&1 | Out-String) -match $Pattern) { return $true }
        if (-not (Test-ContainerRunning $Name)) { return $false }
        Start-Sleep -Seconds 1
    }
    return $false
}

# Waits for every named container to exit; returns the names still running at the timeout.
function Wait-ContainersExit([string[]]$Names, [int]$TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $running = @($Names | Where-Object { Test-ContainerRunning $_ })
        if ($running.Count -eq 0) { return @() }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)
    return $running
}

# Saves each container's log to <Folder>/<name>.log and removes the containers.
function Save-ContainerLogs([string[]]$Names, [string]$Folder) {
    foreach ($name in $Names) {
        & $Engine logs $name 2>&1 | Out-File -FilePath (Join-Path $Folder "$name.log") -Encoding utf8
        & $Engine rm -f $name 2>$null | Out-Null
    }
}

Export-ModuleMember -Function Test-ContainerEngine, Invoke-ServerImageBuild, Initialize-ContainerNetwork, Start-GameContainer,
    Test-ContainerRunning, Wait-ContainerLog, Wait-ContainersExit, Save-ContainerLogs
