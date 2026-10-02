# Driving the open Unity Editor through the unity CLI pipeline: commands, readiness and recovery, modal dialogs,
# scenes, and Game view focus behaviour.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 1.0
Import-Module (Join-Path $PSScriptRoot "TinCan.Common.psm1")

$ProjectRoot = Get-ProjectRoot
# The scene that was active before Open-ScenarioScene switched it; reopened by Restore-StartScene.
$script:ReturnScene = $null

function Invoke-Unity {
    param([string[]]$Arguments, [int]$Timeout = 60)
    $output = & unity cmd @Arguments --result-only --timeout $Timeout 2>&1 | Out-String
    try { return $output | ConvertFrom-Json -Depth 20 } catch { return [pscustomobject]@{ raw = $output } }
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
function Restore-Editor([switch]$RestartEditor) {
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

function Confirm-ScenesClean([switch]$SaveDirtyScenes) {
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

# Each scenario names its test-range scene (Scenario.ScenePath, see .docs/NETWORK_TEST_HARNESS.md "Test range"). Open it
# in the main Editor before the run, so the clone sync copies it and the menu has nothing to switch. The scene that was
# open first is reopened by Restore-StartScene when the script finishes.
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

function Restore-StartScene([switch]$KeepScene) {
    if ($KeepScene -or -not $script:ReturnScene) { return }
    Invoke-Unity @("open_scene", "--path", $script:ReturnScene) 60 | Out-Null
    Write-Tier "Editor" "note" "reopened $script:ReturnScene"
}

# Play runs happen while the developer works elsewhere. A Game view set to "Play Focused" brings the Editor (or Player
# 2) to the front on every Play, so set every Game view to "Play Unfocused" before playing. The setting has been seen to
# revert, so it is enforced per run rather than trusted. PlayModeView is internal, hence reflection.
function Set-PlayUnfocused([string]$ProjectPath = "") {
    $code = 'var t = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.PlayModeView"); ' +
            'var p = t?.GetProperty("enterPlayModeBehavior", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic); ' +
            'if (p == null) return "unsupported"; var changed = 0; ' +
            'foreach (var v in UnityEngine.Resources.FindObjectsOfTypeAll(t)) { if (p.GetValue(v).ToString() != "PlayUnfocused") { p.SetValue(v, System.Enum.Parse(p.PropertyType, "PlayUnfocused")); changed++; } } ' +
            'return changed.ToString();'
    $arguments = @("eval", "--code", $code)
    if ($ProjectPath) { $arguments += @("--project-path", $ProjectPath) }
    $result = (Invoke-Unity $arguments 20).result
    $where = if ($ProjectPath) { "clone $(Split-Path $ProjectPath -Leaf)" } else { "Editor" }
    if ($result -eq "unsupported") { Write-Tier "Editor" "note" "$where has no PlayModeView.enterPlayModeBehavior; Play may take focus" }
    elseif ($result -and $result -ne "0") { Write-Tier "Editor" "note" "$where Game view set to Play Unfocused ($result changed)" }
}

Export-ModuleMember -Function Invoke-Unity, Test-EditorReady, Wait-EditorReady, Get-MainEditorProcessId, Invoke-FocusEditor,
    Get-EnvironmentProblem, Restore-Editor, Get-UnityModal, Confirm-ScenesClean, Open-ScenarioScene, Restore-StartScene,
    Set-PlayUnfocused
