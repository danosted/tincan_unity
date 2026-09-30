<#
.SYNOPSIS
  Builds the Linux dedicated server into Builds/LinuxServer and, with -Image, the Docker image around it; with
  -Client also the Windows client (Builds/Win64) from the same code.

.DESCRIPTION
  With an Editor open on the project, each build runs inside it (menus TinCan > Build > Linux Server / Windows Client)
  and this script polls the build folder's build-result.txt. Without one, it starts the Editor in batch mode
  (-executeMethod TinCan.DevTools.Editor.PlayerBuild.<Target>FromCommandLine).
  Needs the Editor module "Linux Dedicated Server Build Support", installed before the Editor was started:
    unity editors module add <version> -m linux-server
  Plan: .docs/plans/dedicated-server-container.md

.EXAMPLE
  .\.tools\build-server.ps1                  # build the server
  .\.tools\build-server.ps1 -Image -Client   # server, image tincan-server:<commit> (+ :local), Windows client
  .\.tools\build-server.ps1 -ImageOnly       # rebuild only the image from the existing server build
#>
param(
    [switch]$Image,
    [switch]$ImageOnly,
    [switch]$Client,
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$ServerDir = Join-Path $ProjectRoot "Builds/LinuxServer"

function Read-Result([string]$resultFile) {
    if (-not (Test-Path $resultFile)) { return $null }
    return (Get-Content $resultFile -Raw).Trim()
}

function Invoke-PlayerBuild([string]$name, [string]$menu, [string]$method, [string]$outputDir) {
    $resultFile = Join-Path $outputDir "build-result.txt"
    if (Test-Path $resultFile) { Remove-Item $resultFile }

    $status = & unity cmd editor_status --result-only --timeout 15 2>$null | Out-String
    if ($status -match '"status"\s*:\s*"ready"') {
        Write-Host "[Build ] $name in the open Editor ($menu)"
        & unity cmd menu --path $menu --result-only --timeout 60 | Out-Null
    }
    else {
        $version = (Get-Content (Join-Path $ProjectRoot ".unity-version") -Raw).Trim()
        $editorDir = (& unity editors path $version).Trim()
        $unityExe = Join-Path $editorDir "Unity.exe"
        if (-not (Test-Path $unityExe)) { $unityExe = Join-Path $editorDir "Editor/Unity.exe" }
        $log = Join-Path $ProjectRoot ".tools/logs/build-$($name -replace ' ', '-').log"
        Write-Host "[Build ] $name in a batch-mode Editor ($version), log $log"
        $process = Start-Process -FilePath $unityExe -PassThru -NoNewWindow -ArgumentList @(
            "-batchmode", "-quit", "-projectPath", "`"$ProjectRoot`"",
            "-executeMethod", "TinCan.DevTools.Editor.PlayerBuild.$method",
            "-logFile", "`"$log`"")
        $process.WaitForExit()
    }

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        $result = Read-Result $resultFile
        if ($result -and $result -ne "building") { break }
        Start-Sleep -Seconds 5
    }

    $result = Read-Result $resultFile
    if ($result -notlike "succeeded*") {
        Write-Host "[Build ] FAIL $name $result" -ForegroundColor Red
        exit 1
    }
    Write-Host "[Build ] PASS $name $result" -ForegroundColor Green
}

function Invoke-ImageBuild {
    $commit = (& git -C $ProjectRoot rev-parse --short HEAD).Trim()
    $dockerfile = Join-Path $ProjectRoot "Docker/server/Dockerfile"
    Write-Host "[Image ] tincan-server:$commit (+ :local)"
    & docker build -q -f $dockerfile -t "tincan-server:$commit" -t "tincan-server:local" $ServerDir | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[Image ] FAIL docker build" -ForegroundColor Red
        exit 1
    }
    Write-Host "[Image ] PASS tincan-server:$commit" -ForegroundColor Green
}

if (-not $ImageOnly) {
    Invoke-PlayerBuild "Linux server" "TinCan/Build/Linux Server" "LinuxServerFromCommandLine" $ServerDir
}
if ($Image -or $ImageOnly) { Invoke-ImageBuild }
if ($Client) {
    Invoke-PlayerBuild "Windows client" "TinCan/Build/Windows Client" "WindowsClientFromCommandLine" (Join-Path $ProjectRoot "Builds/Win64")
}
