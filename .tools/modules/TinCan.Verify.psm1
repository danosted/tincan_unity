# The verify tiers (see .docs/NETWORK_TEST_HARNESS.md, "Scenarios"): compile, EditMode tests and live scenarios in
# the Editor, plus the batch summary.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 1.0
Import-Module (Join-Path $PSScriptRoot "TinCan.Common.psm1")
Import-Module (Join-Path $PSScriptRoot "TinCan.Editor.psm1")
Import-Module (Join-Path $PSScriptRoot "TinCan.Mppm.psm1")

$ProjectRoot = Get-ProjectRoot

# Refresh imports what changed on disk (recompile alone does not) and compiles only if scripts changed, so an unchanged
# tree passes in seconds. Errors come from the Editor's compile state, so scripts broken in an earlier run still fail.
function Invoke-Compile {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    Invoke-Unity @("eval", "--code", 'UnityEditor.AssetDatabase.Refresh(); return "ok";') 120 | Out-Null

    # A compile triggered by the refresh starts on a later Editor update, and while it runs (and during the domain
    # reload) the pipeline may not answer. Wait for two quiet readings in a row; an unanswered status counts as busy.
    $deadline = (Get-Date).AddSeconds(300)
    $quiet = 0
    while ($quiet -lt 2 -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        $status = Invoke-Unity @("editor_status") 15
        $busy = -not $status.status -or $status.compiling -or $status.domainReloadInProgress
        $quiet = if ($busy) { 0 } else { $quiet + 1 }
    }
    if ($quiet -lt 2) { Stop-Unusable "Editor still compiling after 300 s" }

    # The Editor's own flag also covers a compile that failed in an earlier run (unchanged broken scripts do not recompile).
    $result = $null
    for ($try = 0; $try -lt 5 -and $result -notin @("ok", "failed"); $try++) {
        if ($try -gt 0) { Start-Sleep -Seconds 3 }
        $result = (Invoke-Unity @("eval", "--code", 'return UnityEditor.EditorUtility.scriptCompilationFailed ? "failed" : "ok";') 30).result
    }
    if ($result -eq "ok") {
        # Unity only rewrites the IDE's .csproj/.slnx files when the IDE asks. After an asmdef is added, the stale
        # projects list a file in two assemblies and the IDE reports "type exists in both". Keep them in step.
        Invoke-Unity @("eval", "--code", 'Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll(); return "ok";') 60 | Out-Null
        Write-Tier "Compile" "PASS" "$([int]$clock.Elapsed.TotalSeconds) s"
        return $true
    }
    if ($result -ne "failed") { Stop-Unusable "could not read the compile state from the Editor" }

    # Messages: the last compilation's result, else the Console. Never call recompile here: it answers "up to date"
    # and forgets the errors.
    $errors = @((Invoke-Unity @("recompile_status") 15).errors | Where-Object { $_ })
    if ($errors.Count -eq 0) {
        $console = Invoke-Unity @("console", "--level", "error") 30
        $errors = @($console.entries | ForEach-Object { $_.message } | Where-Object { $_ -match "error CS\d+" } | Select-Object -Unique)
    }
    Write-Tier "Compile" "FAIL" $(if ($errors.Count -gt 0) { "$($errors.Count) error(s)" } else { "compilation failed; see the Unity Console" })
    $errors | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
    return $false
}

function Invoke-Tests([string]$TestFilter, [switch]$SaveDirtyScenes) {
    Confirm-ScenesClean -SaveDirtyScenes:$SaveDirtyScenes
    $arguments = @("run_tests", "--mode", "EditMode")
    if ($TestFilter) { $arguments += @("--filter", $TestFilter) }
    $result = Invoke-Unity $arguments 600
    if (-not $result.Summary) {
        $modal = Get-UnityModal
        if ($modal) { Stop-Unusable $modal }
        Stop-Unusable "run_tests returned no summary: $($result | ConvertTo-Json -Depth 5 -Compress)"
    }

    $summary = $result.Summary
    $detail = "$($summary.Passed)/$($summary.Total) passed" + $(if ($TestFilter) { " (filter '$TestFilter')" } else { "" })
    $failed = @($result.Results | Where-Object { $_.Status -ne "Passed" -and $_.Status -ne "Skipped" })
    if ($failed.Count -gt 0 -or $summary.Total -eq 0) {
        Write-Tier "Tests" "FAIL" $detail
        $failed | Select-Object -First 15 | ForEach-Object {
            $message = if ($_.Message) { ($_.Message -split "`n")[0] } else { "" }
            Write-Host "  $($_.FullName): $message"
        }
        return $false
    }
    Write-Tier "Tests" "PASS" $detail
    return $true
}

# Every scenario in ScenarioCatalog, grouped by test-range scene: each scene switch costs 15-20 s.
function Get-CatalogScenarioNames {
    $names = (Invoke-Unity @("eval", "--code", 'return string.Join(",", System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderBy(TinCan.DevTools.Scenarios.ScenarioCatalog.Entries, e => e.Scenario.ScenePath ?? ""), e => e.Scenario.Name));') 30).result
    if (-not $names) { Stop-Unusable "could not read ScenarioCatalog.Names" }
    return @($names -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

# True when nothing on this PC holds the UDP port (binding it briefly is the reliable test: a port owned by a WSL or
# container process does not show up in Get-NetUDPEndpoint).
function Test-UdpPortFree([int]$Port) {
    try {
        $probe = [System.Net.Sockets.UdpClient]::new($Port)
        $probe.Close()
        return $true
    }
    catch {
        return $false
    }
}

# Triggers the scenario menu, waits for Logs/feature-telemetry/<Scenario>/latest-summary.json (written by the host once
# every peer reported; Play mode then ends by itself), prints the verdict and the checkpoint screenshots.
function Invoke-Scenario([string]$Scenario, [string]$mode, [int]$TimeoutSeconds = 240, [switch]$SaveDirtyScenes) {
    Confirm-ScenesClean -SaveDirtyScenes:$SaveDirtyScenes
    Open-ScenarioScene $Scenario
    Set-PlayUnfocused
    if ($mode -eq "Duo") { Sync-CloneScenes; Confirm-NetworkPrefabsMatch; foreach ($clone in Get-ClonePaths) { Set-PlayUnfocused $clone } }
    $status = Invoke-Unity @("editor_status") 15
    if ($status.playMode -and $status.playMode -ne "stopped") {
        Invoke-Unity @("editor_stop") 30 | Out-Null
        Start-Sleep -Seconds 3
    }

    # The scenario's host listens on UDP 7777. Anything else holding it (a dedicated server container: with WSL
    # mirrored networking it owns the PC's port) makes the host fail to bind and the run time out with no summary.
    if (-not (Test-UdpPortFree 7777)) {
        Stop-Unusable "UDP port 7777 is in use, so the scenario host cannot start. A dedicated server container? (podman ps; podman stop tincan-server)"
    }

    $directory = Join-Path $ProjectRoot "Logs/feature-telemetry/$Scenario"
    $summaryPath = Join-Path $directory "latest-summary.json"
    $menu = if ($mode -eq "Solo") { "TinCan/Dev/Scenarios/$Scenario (Host)" } else { "TinCan/Dev/Scenarios/$Scenario (Host + Client, Lag100)" }
    $started = (Get-Date).ToUniversalTime()
    $clock = [Diagnostics.Stopwatch]::StartNew()

    $trigger = Invoke-Unity @("menu", "--path", $menu) 30
    if ($trigger.raw -and $trigger.raw -match "error|not found") { Stop-Unusable "menu '$menu' failed: $($trigger.raw)" }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        if ((Test-Path $summaryPath) -and (Get-Item $summaryPath).LastWriteTimeUtc -gt $started) { break }
        $modal = Get-UnityModal
        if ($modal) { Stop-Unusable $modal }
    }

    if (-not ((Test-Path $summaryPath) -and (Get-Item $summaryPath).LastWriteTimeUtc -gt $started)) {
        Write-Tier $mode "FAIL" "no summary within $TimeoutSeconds s (did the scenario start? check: unity cmd console --level error)"
        Invoke-Unity @("editor_stop") 30 | Out-Null
        return $false
    }

    $summary = Get-Content $summaryPath -Raw | ConvertFrom-Json -Depth 20
    $state = if ($summary.passed) { "PASS" } else { "FAIL" }
    Write-Tier $mode $state "$Scenario ($($summary.mode), $([int]$clock.Elapsed.TotalSeconds) s) -> $summaryPath"
    foreach ($peer in $summary.peers) {
        Write-Host ("  {0,-8} {1,-8} {2}" -f $peer.role, $peer.status, $peer.report)
        foreach ($failure in @($peer.failures)) { if ($failure) { Write-Host "    - $failure" -ForegroundColor Red } }
    }
    Get-ChildItem $directory -Recurse -Filter *.png -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTimeUtc -gt $started } |
        ForEach-Object { Write-Host "  capture: $($_.FullName)" }
    return [bool]$summary.passed
}

# Rows are [pscustomobject]@{ Scenario; Solo; Duo }. Returns the failed rows; prints a table when there is more than one.
function Write-ScenarioBatch([object[]]$results) {
    $failed = @($results | Where-Object { $_.Solo -eq "FAIL" -or $_.Duo -in @("FAIL", "skipped") })
    if ($results.Count -gt 1) {
        Write-Host ""
        foreach ($row in $results) {
            $state = if ($failed -contains $row) { "FAIL" } else { "PASS" }
            Write-Tier "Batch" $state ("{0,-20} solo {1,-7} duo {2}" -f $row.Scenario, $row.Solo, $row.Duo)
        }
    }
    return $failed
}

Export-ModuleMember -Function Invoke-Compile, Invoke-Tests, Get-CatalogScenarioNames, Invoke-Scenario, Write-ScenarioBatch
