<#
.SYNOPSIS
  Builds the Linux dedicated server into Builds/LinuxServer and, with -Image, the container image around it; with
  -Client also the Windows client (Builds/Win64) from the same code.

.DESCRIPTION
  With an Editor open on the project, each build runs inside it (menus TinCan > Build > Linux Server / Windows Client)
  and this script polls the build folder's build-result.txt. Without one, it starts the Editor in batch mode
  (-executeMethod TinCan.DevTools.Editor.PlayerBuild.<Target>FromCommandLine).
  Needs the Editor module "Linux Dedicated Server Build Support", installed before the Editor was started:
    unity editors module add <version> -m linux-server
  The image is built with Podman; its machine must be running (podman machine start).
  Plan: .docs/plans/dedicated-server-container.md

.EXAMPLE
  .\.tools\build.ps1                  # build the server
  .\.tools\build.ps1 -Image -Client   # server, image tincan-server:<commit> (+ :local), Windows client
  .\.tools\build.ps1 -ImageOnly       # rebuild only the image from the existing server build
#>
param(
    [switch]$Image,
    [switch]$ImageOnly,
    [switch]$Client,
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = "Stop"
foreach ($module in "Common", "Build", "Container") {
    Import-Module (Join-Path $PSScriptRoot "modules/TinCan.$module.psm1") -Force
}

$ProjectRoot = Get-ProjectRoot
$ServerDir = Join-Path $ProjectRoot "Builds/LinuxServer"

if (-not $ImageOnly) {
    if (-not (Invoke-PlayerBuild "Linux server" "TinCan/Build/Linux Server" "LinuxServerFromCommandLine" $ServerDir -TimeoutMinutes $TimeoutMinutes)) { exit $ExitFail }
}
if ($Image -or $ImageOnly) {
    if (-not (Invoke-ServerImageBuild $ServerDir)) { exit $ExitFail }
}
if ($Client) {
    if (-not (Invoke-PlayerBuild "Windows client" "TinCan/Build/Windows Client" "WindowsClientFromCommandLine" (Join-Path $ProjectRoot "Builds/Win64") -TimeoutMinutes $TimeoutMinutes)) { exit $ExitFail }
}
