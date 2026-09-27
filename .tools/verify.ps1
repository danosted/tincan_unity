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
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Tiers = @("Compile", "Tests", "Solo", "Duo")
$Scenario = @($Scenario | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if (-not $UpTo) { $UpTo = if ($Scenario.Count -gt 0 -or $All) { "Duo" } else { "Tests" } }
if (($UpTo -in @("Solo", "Duo")) -and $Scenario.Count -eq 0 -and -not $All) { throw "-Scenario or -All is required for the $UpTo tier." }

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
        Start-Sleep -Seconds 3
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

# Causes outside the Editor that focusing cannot fix: no Editor open, or no Unity Hub. Without the Hub the Editor keeps
# retrying its Hub connection on the main thread and every pipeline command times out; a Hub dialog (for example
# updated terms) also keeps the Editor from starting. Terms are for the developer to accept, never this script.
function Get-EnvironmentProblem {
    if (-not (Get-MainEditorProcessId)) { return "no Unity Editor is open for this project. Open it from Unity Hub." }
    if (-not (Get-Process -Name "Unity Hub" -ErrorAction SilentlyContinue)) {
        return "Unity Hub is not running, so the Editor stalls waiting for it. Start Unity Hub (accept any dialog it shows), then rerun."
    }
    return $null
}

# Recovery ladder: wait, focus the window, and only with -RestartEditor close (discarding unsaved changes) and reopen.
function Restore-Editor {
    $modal = Wait-EditorReady 20
    if (-not $modal) { return $null }
    if ($modal -like "modal*") { return $modal }
    $problem = Get-EnvironmentProblem
    if ($problem) { return $problem }

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
# so before a host + client run, open the host's active scene in every clone. A clone that already has it open reopens
# it too: clones do not reload a scene that changed on disk (for example after Rebuild Scenes), and would run the stale one.
# The clone list is read once per run: each `unity status` costs about 1.5 s.
function Get-ClonePaths {
    if ($null -eq $script:ClonePaths) {
        $script:ClonePaths = @(& unity status 2>$null | Select-String "Library[\\/]VP[\\/]" | ForEach-Object {
            ($_.ToString() -split "\t|\s{2,}") | Where-Object { $_ -match "Library[\\/]VP[\\/]" } | Select-Object -First 1
        })
    }
    return $script:ClonePaths
}

function Sync-CloneScenes {
    $main = Invoke-Unity @("list_open_scenes") 20
    $mainScene = @($main.scenes | Where-Object { $_.isActive })[0].path
    if (-not $mainScene) { return }

    foreach ($clone in Get-ClonePaths) {
        $scenes = Invoke-Unity @("list_open_scenes", "--project-path", $clone) 20
        $active = @($scenes.scenes | Where-Object { $_.isActive })[0].path
        Invoke-Unity @("open_scene", "--project-path", $clone, "--path", $mainScene) 60 | Out-Null
        if ($active -eq $mainScene) { continue }
        Write-Tier "Editor" "note" "clone $(Split-Path $clone -Leaf) had '$active' open; opened $mainScene"
    }
}

# NGO refuses a client whose network prefab hashes differ from the host's ("NetworkConfig mismatch"), which a scenario
# only shows as "no subject player". A clone reads prefabs from disk while the host may hold a newer in-memory hash
# (for example right after a prefab was created from a scene object), so compare them before a host + client run.
# Prefabs do not change during a run, so a batch checks once.
function Confirm-NetworkPrefabsMatch {
    if ($script:PrefabsChecked) { return }
    $script:PrefabsChecked = $true
    $code = 'var sb = new System.Text.StringBuilder(); foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })) { var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid); var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path); var no = go != null ? go.GetComponent<Unity.Netcode.NetworkObject>() : null; if (no != null) sb.Append(path).Append("=").Append(no.PrefabIdHash).Append(";"); } return sb.ToString();'
    $hostPrefabs = (Invoke-Unity @("eval", "--code", $code) 60).result
    if (-not $hostPrefabs) { return }

    foreach ($clone in Get-ClonePaths) {
        $clonePrefabs = (Invoke-Unity @("eval", "--project-path", $clone, "--code", $code) 60).result
        if (-not $clonePrefabs -or $clonePrefabs -eq $hostPrefabs) { continue }

        $hostSet = $hostPrefabs -split ";" | Where-Object { $_ }
        $cloneSet = $clonePrefabs -split ";" | Where-Object { $_ }
        $diff = Compare-Object $hostSet $cloneSet | ForEach-Object { "$($_.SideIndicator -replace '<=','host' -replace '=>','clone'): $($_.InputObject)" }
        Stop-Unusable ("network prefab hashes differ between host and clone $(Split-Path $clone -Leaf); the client would be refused (NetworkConfig mismatch). " +
            "On the host, EditorUtility.SetDirty(prefab) + AssetDatabase.SaveAssets() writes the in-memory hash to disk:`n  " + ($diff -join "`n  "))
    }
}

# Each scenario names its test-range scene (Scenario.ScenePath, see .docs/NETWORK_TEST_HARNESS.md "Test range"). Open it
# in the main Editor before the run, so the clone sync below copies it and the menu has nothing to switch. The scene
# that was open first is reopened when the script finishes.
function Open-ScenarioScene([string]$name) {
    $code = 'return TinCan.DevTools.Scenarios.ScenarioCatalog.TryGet("{0}", out var e) ? (e.Scenario.ScenePath ?? "") : "?";' -f $name
    $path = (Invoke-Unity @("eval", "--code", $code) 30).result
    if ($path -eq "?") { Stop-Unusable "unknown scenario '$name' (see DevTools/Scenarios/ScenarioCatalog.cs)" }
    if (-not $path) { return }

    $open = Invoke-Unity @("list_open_scenes") 20
    $active = @($open.scenes | Where-Object { $_.isActive })[0].path
    if ($active -eq $path) { return }
    if (-not $script:ReturnScene) { $script:ReturnScene = $active }
    Invoke-Unity @("open_scene", "--path", $path) 60 | Out-Null
    Write-Tier "Editor" "note" "opened $path for $name"
}

function Restore-StartScene {
    if ($KeepScene -or -not $script:ReturnScene) { return }
    Invoke-Unity @("open_scene", "--path", $script:ReturnScene) 60 | Out-Null
    Write-Tier "Editor" "note" "reopened $script:ReturnScene"
}

function Invoke-Scenario([string]$Scenario, [string]$mode) {
    Confirm-ScenesClean
    Open-ScenarioScene $Scenario
    if ($mode -eq "Duo") { Sync-CloneScenes; Confirm-NetworkPrefabsMatch }
    $status = Invoke-Unity @("editor_status") 15
    if ($status.playMode -and $status.playMode -ne "stopped") {
        Invoke-Unity @("editor_stop") 30 | Out-Null
        Start-Sleep -Seconds 3
    }

    $directory = Join-Path $ProjectRoot "Logs/feature-telemetry/$Scenario"
    $summaryPath = Join-Path $directory "latest-summary.json"
    $menu = if ($mode -eq "Solo") { "TinCan/Dev/Scenarios/$Scenario (Host)" } else { "TinCan/Dev/Scenarios/$Scenario (Host + Client, Lag100)" }
    $started = (Get-Date).ToUniversalTime()
    $clock = [Diagnostics.Stopwatch]::StartNew()

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

$modal = Restore-Editor
if ($modal) { Stop-Unusable $modal }

$lastTier = [array]::IndexOf($Tiers, $UpTo)

if ($All) {
    # Grouped by test-range scene: each scene switch costs 15-20 s.
    $names = (Invoke-Unity @("eval", "--code", 'return string.Join(",", System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderBy(TinCan.DevTools.Scenarios.ScenarioCatalog.Entries, e => e.Scenario.ScenePath ?? ""), e => e.Scenario.Name));') 30).result
    if (-not $names) { Stop-Unusable "could not read ScenarioCatalog.Names" }
    $Scenario = @($names -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

# Compile and Tests run once, however many scenarios follow.
if (-not (Invoke-Compile)) { exit 1 }
if ($lastTier -ge 1 -and -not (Invoke-Tests)) { exit 1 }
if ($lastTier -lt 2) { exit 0 }

$results = @()
foreach ($name in $Scenario) {
    $solo = if (Invoke-Scenario $name "Solo") { "PASS" } else { "FAIL" }
    $duo = if ($lastTier -lt 3) { "-" } elseif ($solo -eq "FAIL") { "skipped" } elseif (Invoke-Scenario $name "Duo") { "PASS" } else { "FAIL" }
    $results += [pscustomobject]@{ Scenario = $name; Solo = $solo; Duo = $duo }
}
Restore-StartScene

$failed = @($results | Where-Object { $_.Solo -eq "FAIL" -or $_.Duo -in @("FAIL", "skipped") })
if ($results.Count -gt 1) {
    Write-Host ""
    foreach ($row in $results) {
        $state = if ($failed -contains $row) { "FAIL" } else { "PASS" }
        Write-Tier "Batch" $state ("{0,-20} solo {1,-7} duo {2}" -f $row.Scenario, $row.Solo, $row.Duo)
    }
}
exit $(if ($failed.Count -gt 0) { 1 } else { 0 })
