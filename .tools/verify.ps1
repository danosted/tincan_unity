<#
.SYNOPSIS
One-command feedback loop for a feature: compile, EditMode tests, then live scenarios. Stops at the first red tier.

.DESCRIPTION
Tiers, cheapest first (see .docs/NETWORK_TEST_HARNESS.md, "Scenarios"):
  Compile  AssetDatabase.Refresh (imports edits made outside the Editor, compiles if scripts changed), then fail on
           any compiler error (including scripts that were already broken before the run).
  Tests    unity cmd run_tests --mode EditMode (optionally filtered).
  Solo     TinCan/Dev/Scenarios/<Scenario> (Host): the host plays every phase against its own player.
  Duo      TinCan/Dev/Scenarios/<Scenario> (Host + Client, Lag100): arrange/assert on the host, act on a lagged client.

A scenario tier triggers the menu, waits for Logs/feature-telemetry/<Scenario>/latest-summary.json (written by the
host once every peer reported; Play mode then ends by itself), prints the verdict and the checkpoint screenshots.

Several scenarios (a list, or -All) form a batch: Compile and Tests run once, then Solo and Duo per scenario. A
failing scenario does not stop the batch (its Duo is skipped when Solo failed); a table at the end lists every result.

Exit code: 0 all requested tiers passed, 1 a tier failed, 2 the Editor was not usable (busy, modal dialog, timeout).

.PARAMETER Scenario
Scenario name(s) from DevTools/Scenarios/ScenarioCatalog.cs, comma-separated. Required for the Solo and Duo tiers
unless -All is given.

.PARAMETER All
Run every scenario in ScenarioCatalog as one batch.

.PARAMETER UpTo
Last tier to run: Compile, Tests, Solo or Duo (default: Duo when a scenario is given, else Tests).

.PARAMETER TestFilter
Optional EditMode filter (for example "ScenarioRunner"). Omit to run the whole suite.

.PARAMETER SaveDirtyScenes
If an open scene is marked modified, save it instead of stopping. Unity otherwise shows a blocking
"Scene(s) Have Been Modified" dialog when tests start or Play ends. After Play sessions the flag is often spurious.

.PARAMETER RestartEditor
If the Editor pipeline stays unresponsive after focusing its window, close it (unity close --force, which discards
unsaved changes) and reopen it (unity open). Without this switch the script stops with exit code 2 instead.

.PARAMETER KeepScene
Leave the scenario's test-range scene open instead of reopening the scene that was open when the script started.
Faster when running several verifies in a row; the next run then skips the scene switch.

.EXAMPLE
.\.tools\verify.ps1 -Scenario NetCatch
.\.tools\verify.ps1 -Scenario NetCatch -UpTo Solo -TestFilter Scenario
.\.tools\verify.ps1 -Scenario NetCatch,RepairLoop
.\.tools\verify.ps1 -All
.\.tools\verify.ps1 -UpTo Tests
#>

#Requires -Version 7.0

param(
    [string[]]$Scenario = @(),
    [switch]$All,
    [ValidateSet("Compile", "Tests", "Solo", "Duo")]
    [string]$UpTo,
    [string]$TestFilter,
    [int]$ScenarioTimeoutSeconds = 240,
    [switch]$SaveDirtyScenes,
    [switch]$RestartEditor,
    [switch]$KeepScene
)

$ErrorActionPreference = "Stop"
foreach ($module in "Common", "Editor", "Mppm", "Verify") {
    Import-Module (Join-Path $PSScriptRoot "modules/TinCan.$module.psm1") -Force
}

$Tiers = @("Compile", "Tests", "Solo", "Duo")
$Scenario = @($Scenario | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if (-not $UpTo) { $UpTo = if ($Scenario.Count -gt 0 -or $All) { "Duo" } else { "Tests" } }
if (($UpTo -in @("Solo", "Duo")) -and $Scenario.Count -eq 0 -and -not $All) { throw "-Scenario or -All is required for the $UpTo tier." }
$lastTier = [array]::IndexOf($Tiers, $UpTo)

try {
    $modal = Restore-Editor -RestartEditor:$RestartEditor
    if ($modal) { Stop-Unusable $modal }

    if ($All) { $Scenario = Get-CatalogScenarioNames }

    # Compile and Tests run once, however many scenarios follow.
    if (-not (Invoke-Compile)) { exit $ExitFail }
    if ($lastTier -ge 1 -and -not (Invoke-Tests -TestFilter $TestFilter -SaveDirtyScenes:$SaveDirtyScenes)) { exit $ExitFail }
    if ($lastTier -lt 2) { exit $ExitPass }

    $scenarioOptions = @{ TimeoutSeconds = $ScenarioTimeoutSeconds; SaveDirtyScenes = $SaveDirtyScenes }
    $results = @()
    foreach ($name in $Scenario) {
        $solo = if (Invoke-Scenario $name "Solo" @scenarioOptions) { "PASS" } else { "FAIL" }
        $duo = if ($lastTier -lt 3) { "-" } elseif ($solo -eq "FAIL") { "skipped" } elseif (Invoke-Scenario $name "Duo" @scenarioOptions) { "PASS" } else { "FAIL" }
        $results += [pscustomobject]@{ Scenario = $name; Solo = $solo; Duo = $duo }
    }
    Restore-StartScene -KeepScene:$KeepScene

    $failed = @(Write-ScenarioBatch $results)
    exit $(if ($failed.Count -gt 0) { $ExitFail } else { $ExitPass })
}
catch {
    $reason = Get-UnusableReason $_
    if (-not $reason) { throw }
    Write-Tier "Editor" "STOP" $reason
    exit $ExitUnusable
}
