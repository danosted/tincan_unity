<#
.SYNOPSIS
One-command feedback loop for a feature: compile, EditMode tests, then live scenarios. Stops at the first red tier.

.DESCRIPTION
Tiers, cheapest first (see .docs/NETWORK_TEST_HARNESS.md, "Scenarios"):
  Compile  unity cmd recompile, then fail on any compiler error.
  Tests    unity cmd run_tests --mode EditMode (optionally filtered).
  Solo     TinCan/Dev/Scenarios/<Scenario> (Host): the host plays every phase against its own player.
  Duo      TinCan/Dev/Scenarios/<Scenario> (Host + Client, Lag100): arrange/assert on the host, act on a lagged client.

A scenario tier triggers the menu, waits for Logs/feature-telemetry/<Scenario>/latest-summary.json (written by the
host once every peer reported; Play mode then ends by itself), prints the verdict and the checkpoint screenshots.

Exit code: 0 all requested tiers passed, 1 a tier failed, 2 the Editor was not usable (busy, modal dialog, timeout).

.PARAMETER Scenario
Scenario name from DevTools/Scenarios/ScenarioCatalog.cs. Required for the Solo and Duo tiers.

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

.EXAMPLE
.\.tools\verify.ps1 -Scenario NetCatch
.\.tools\verify.ps1 -Scenario NetCatch -UpTo Solo -TestFilter Scenario
.\.tools\verify.ps1 -UpTo Tests
#>

#Requires -Version 7.0

param(
    [string]$Scenario,
    [ValidateSet("Compile", "Tests", "Solo", "Duo")]
    [string]$UpTo,
    [string]$TestFilter,
    [int]$ScenarioTimeoutSeconds = 240,
    [switch]$SaveDirtyScenes,
    [switch]$RestartEditor
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Tiers = @("Compile", "Tests", "Solo", "Duo")
if (-not $UpTo) { $UpTo = if ($Scenario) { "Duo" } else { "Tests" } }
if (($UpTo -in @("Solo", "Duo")) -and -not $Scenario) { throw "-Scenario is required for the $UpTo tier." }

function Invoke-Unity {
    param([string[]]$Arguments, [int]$Timeout = 60)
    $output = & unity cmd @Arguments --result-only --timeout $Timeout 2>&1 | Out-String
    try { return $output | ConvertFrom-Json -Depth 20 } catch { return [pscustomobject]@{ raw = $output } }
}

function Write-Tier([string]$name, [string]$state, [string]$detail = "") {
    $color = switch ($state) { "PASS" { "Green" } "FAIL" { "Red" } default { "Yellow" } }
    Write-Host ("[{0,-7}] {1,-4} {2}" -f $name, $state, $detail) -ForegroundColor $color
}

function Stop-Unusable([string]$reason) {
    Write-Tier "Editor" "STOP" $reason
    exit 2
}

function Test-EditorReady {
    $status = Invoke-Unity @("editor_status") 15
    return $status.status -eq "ready" -and -not $status.compiling
}

function Wait-EditorReady([int]$Seconds = 120) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        $modal = Get-UnityModal
        if ($modal) { return $modal }
        if (Test-EditorReady) { return $null }
        Start-Sleep -Seconds 2
    }
    return "editor not ready after $Seconds s"
}

function Get-MainEditorProcessId {
    $line = & unity status 2>$null | Select-String ([regex]::Escape($ProjectRoot) + "\s") | Select-Object -First 1
    if (-not $line) { return $null }
    return [int](($line.ToString().Trim() -split "\s+")[-1])
}

# A wedged pipeline (commands time out, no modal) usually wakes when the Editor window gets focus.
function Invoke-FocusEditor {
    $processId = Get-MainEditorProcessId
    if (-not $processId) { return }
    $handle = (Get-Process -Id $processId -ErrorAction SilentlyContinue).MainWindowHandle
    if (-not $handle -or $handle -eq [IntPtr]::Zero) { return }
    if (-not ("TinCanVerify.Focus" -as [type])) {
        Add-Type -Namespace TinCanVerify -Name Focus -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr h);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool ShowWindow(System.IntPtr h, int c);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.UIntPtr e);
"@
    }
    # Tapping Alt lets this process pass the foreground lock.
    [TinCanVerify.Focus]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [TinCanVerify.Focus]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
    [TinCanVerify.Focus]::ShowWindow($handle, 9) | Out-Null
    [TinCanVerify.Focus]::SetForegroundWindow($handle) | Out-Null
}

# Recovery ladder: wait, focus the window, and only with -RestartEditor close (discarding unsaved changes) and reopen.
function Restore-Editor {
    $modal = Wait-EditorReady 20
    if (-not $modal) { return $null }
    if ($modal -like "modal*") { return $modal }

    Write-Tier "Editor" "note" "pipeline not answering; focusing the Editor window"
    Invoke-FocusEditor
    $modal = Wait-EditorReady 60
    if (-not $modal -or $modal -like "modal*") { return $modal }

    if (-not $RestartEditor) {
        return "$modal. Focusing did not help; restart the Editor, or rerun with -RestartEditor (discards unsaved changes)."
    }

    Write-Tier "Editor" "note" "restarting the Editor (unity close --force; unity open)"
    & unity close $ProjectRoot --force --timeout 60 2>&1 | Out-Null
    & unity open $ProjectRoot 2>&1 | Out-Null
    return Wait-EditorReady 600
}

# Unity modal dialogs block every pipeline command, so look for them by window title before blaming the code.
function Get-UnityModal {
    if (-not ("TinCanVerify.Windows" -as [type])) {
        Add-Type -Namespace TinCanVerify -Name Windows -MemberDefinition @"
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern int GetWindowText(System.IntPtr h, System.Text.StringBuilder s, int n);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint p);
[System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
public static System.Collections.Generic.List<string> Titles(uint pid) {
    var r = new System.Collections.Generic.List<string>();
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p);
        if (p == pid && IsWindowVisible(h)) { var s = new System.Text.StringBuilder(256); GetWindowText(h, s, 256); r.Add(s.ToString()); }
        return true; }, System.IntPtr.Zero);
    return r;
}
"@
    }
    $editors = & unity status 2>$null | Select-String "ready|busy" | ForEach-Object { ($_ -split "\s+")[-1] }
    foreach ($processId in $editors) {
        $titles = [TinCanVerify.Windows]::Titles([uint32]$processId)
        $blocking = $titles | Where-Object { $_ -match "Have Been Modified|Compilation errors|Unity - Error|Safe Mode" }
        if ($blocking) { return "modal dialog '$($blocking -join "', '")' in Unity (pid $processId). Answer it, then rerun." }
    }
    return $null
}

function Confirm-ScenesClean {
    $scenes = Invoke-Unity @("list_open_scenes") 20
    $dirty = @($scenes.scenes | Where-Object { $_.isDirty })
    if ($dirty.Count -eq 0) { return }

    $names = ($dirty | ForEach-Object { $_.name }) -join ", "
    if (-not $SaveDirtyScenes) {
        Stop-Unusable "open scene(s) marked modified: $names. Unity would block on a save dialog. Save them, or rerun with -SaveDirtyScenes."
    }
    Invoke-Unity @("save_all") 60 | Out-Null
    Write-Tier "Editor" "note" "saved modified scene(s): $names (check git diff)"
}

function Invoke-Compile {
    Invoke-Unity @("recompile") 60 | Out-Null
    $deadline = (Get-Date).AddSeconds(300)
    do {
        Start-Sleep -Seconds 2
        $status = Invoke-Unity @("recompile_status") 15
    } while ($status.status -eq "compiling" -and (Get-Date) -lt $deadline)

    if ($status.status -notin @("completed", "up_to_date")) {
        Stop-Unusable "recompile did not complete (status '$($status.status)'): $($status.raw)"
    }
    $errors = @($status.errors | Where-Object { $_ })
    if ($status.failed -or $errors.Count -gt 0) {
        Write-Tier "Compile" "FAIL" "$($errors.Count) error(s)"
        $errors | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
        return $false
    }
    Write-Tier "Compile" "PASS"
    return $true
}

function Invoke-Tests {
    Confirm-ScenesClean
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

# MPPM clones can come back from a relaunch with an empty untitled scene. They then "play" nothing and never join,
# so before a host + client run, open the host's active scene in every clone that has a different one.
function Sync-CloneScenes {
    $main = Invoke-Unity @("list_open_scenes") 20
    $mainScene = @($main.scenes | Where-Object { $_.isActive })[0].path
    if (-not $mainScene) { return }

    $clones = & unity status 2>$null | Select-String "Library[\\/]VP[\\/]" | ForEach-Object {
        ($_.ToString() -split "\t|\s{2,}") | Where-Object { $_ -match "Library[\\/]VP[\\/]" } | Select-Object -First 1
    }
    foreach ($clone in $clones) {
        $scenes = Invoke-Unity @("list_open_scenes", "--project-path", $clone) 20
        $active = @($scenes.scenes | Where-Object { $_.isActive })[0].path
        if ($active -eq $mainScene) { continue }

        Invoke-Unity @("open_scene", "--project-path", $clone, "--path", $mainScene) 60 | Out-Null
        Write-Tier "Editor" "note" "clone $(Split-Path $clone -Leaf) had '$active' open; opened $mainScene"
    }
}

function Invoke-Scenario([string]$mode) {
    Confirm-ScenesClean
    if ($mode -eq "Duo") { Sync-CloneScenes }
    $status = Invoke-Unity @("editor_status") 15
    if ($status.playMode -and $status.playMode -ne "stopped") {
        Invoke-Unity @("editor_stop") 30 | Out-Null
        Start-Sleep -Seconds 3
    }

    $directory = Join-Path $ProjectRoot "Logs/feature-telemetry/$Scenario"
    $summaryPath = Join-Path $directory "latest-summary.json"
    $menu = if ($mode -eq "Solo") { "TinCan/Dev/Scenarios/$Scenario (Host)" } else { "TinCan/Dev/Scenarios/$Scenario (Host + Client, Lag100)" }
    $started = (Get-Date).ToUniversalTime()

    $trigger = Invoke-Unity @("menu", "--path", $menu) 30
    if ($trigger.raw -and $trigger.raw -match "error|not found") { Stop-Unusable "menu '$menu' failed: $($trigger.raw)" }

    $deadline = (Get-Date).AddSeconds($ScenarioTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        if ((Test-Path $summaryPath) -and (Get-Item $summaryPath).LastWriteTimeUtc -gt $started) { break }
        $modal = Get-UnityModal
        if ($modal) { Stop-Unusable $modal }
    }

    if (-not ((Test-Path $summaryPath) -and (Get-Item $summaryPath).LastWriteTimeUtc -gt $started)) {
        Write-Tier $mode "FAIL" "no summary within $ScenarioTimeoutSeconds s (did the scenario start? check: unity cmd console --level error)"
        Invoke-Unity @("editor_stop") 30 | Out-Null
        return $false
    }

    $summary = Get-Content $summaryPath -Raw | ConvertFrom-Json -Depth 20
    $state = if ($summary.passed) { "PASS" } else { "FAIL" }
    Write-Tier $mode $state "$Scenario ($($summary.mode)) -> $summaryPath"
    foreach ($peer in $summary.peers) {
        Write-Host ("  {0,-8} {1,-8} {2}" -f $peer.role, $peer.status, $peer.report)
        foreach ($failure in @($peer.failures)) { if ($failure) { Write-Host "    - $failure" -ForegroundColor Red } }
    }
    Get-ChildItem $directory -Recurse -Filter *.png -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTimeUtc -gt $started } |
        ForEach-Object { Write-Host "  capture: $($_.FullName)" }
    return [bool]$summary.passed
}

$modal = Restore-Editor
if ($modal) { Stop-Unusable $modal }

foreach ($tier in $Tiers) {
    $ok = switch ($tier) {
        "Compile" { Invoke-Compile }
        "Tests" { Invoke-Tests }
        "Solo" { Invoke-Scenario "Solo" }
        "Duo" { Invoke-Scenario "Duo" }
    }
    if (-not $ok) { exit 1 }
    if ($tier -eq $UpTo) { break }
}
exit 0
