<#
.SYNOPSIS
  Builds the Linux dedicated server into Builds/LinuxServer and, with -Image, the container image around it; with
  -Client also the Windows client (Builds/Win64) from the same code. -Perf builds the development "perf" variants
  instead (Builds/LinuxServerPerf, Builds/Win64Perf, image tincan-server-perf) used by .tools/perf.ps1.

.DESCRIPTION
  With an Editor open on the project, each build runs inside it (menus TinCan > Build > Linux Server / Windows Client,
  and their "(Perf)" variants) and this script polls the build folder's build-result.txt. Without one, it starts the
  Editor in batch mode (-executeMethod TinCan.DevTools.Editor.PlayerBuild.<Target>FromCommandLine).
  Needs the Editor module "Linux Dedicated Server Build Support", installed before the Editor was started:
    unity editors module add <version> -m linux-server
  The image is built with Podman; its machine must be running (podman machine start).
  Plans: .docs/plans/dedicated-server-container.md, .docs/plans/performance-budgets.md

.EXAMPLE
  .\.tools\build.ps1                  # build the server
  .\.tools\build.ps1 -Image -Client   # server, image tincan-server:<commit> (+ :local), Windows client
  .\.tools\build.ps1 -ImageOnly       # rebuild only the image from the existing server build
  .\.tools\build.ps1 -Perf -Image -Client   # the perf variants: server, image tincan-server-perf, client
#>
param(
    [switch]$Image,
    [switch]$ImageOnly,
    [switch]$Client,
    [switch]$Perf,
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = "Stop"
foreach ($module in "Common", "Editor", "Build", "Container") {
    Import-Module (Join-Path $PSScriptRoot "modules/TinCan.$module.psm1") -Force
}

$ProjectRoot = Get-ProjectRoot
$suffix = if ($Perf) { "Perf" } else { "" }
$menuSuffix = if ($Perf) { " (Perf)" } else { "" }
$ServerDir = Join-Path $ProjectRoot "Builds/LinuxServer$suffix"
$imageName = if ($Perf) { "tincan-server-perf" } else { "tincan-server" }

if (-not $ImageOnly) {
    if (-not (Invoke-PlayerBuild "Linux server$menuSuffix" "TinCan/Build/Linux Server$menuSuffix" "LinuxServer${suffix}FromCommandLine" $ServerDir -TimeoutMinutes $TimeoutMinutes)) { exit $ExitFail }
}
if ($Image -or $ImageOnly) {
    if (-not (Invoke-ServerImageBuild $ServerDir -Image $imageName)) { exit $ExitFail }
}
if ($Client) {
    if (-not (Invoke-PlayerBuild "Windows client$menuSuffix" "TinCan/Build/Windows Client$menuSuffix" "WindowsClient${suffix}FromCommandLine" (Join-Path $ProjectRoot "Builds/Win64$suffix") -TimeoutMinutes $TimeoutMinutes)) { exit $ExitFail }
}
