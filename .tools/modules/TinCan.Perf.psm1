# Perf runs (.docs/plans/performance-budgets.md, .docs/PERFORMANCE.md): environment profiles and load scenarios, one
# folder per run under Logs/perf/runs, budget checks against .docs/perf/budgets.json, and compare / trend / set-budgets
# over the run folders.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 1.0
Import-Module (Join-Path $PSScriptRoot "TinCan.Common.psm1")

$ProjectRoot = Get-ProjectRoot
$RunsRoot = Join-Path $ProjectRoot "Logs/perf/runs"
$BudgetsPath = Join-Path $ProjectRoot ".docs/perf/budgets.json"

# Where a number came from. Numbers are compared only within one profile. Cores are WSL vCPUs of the Podman machine
# (the Windows scheduler maps them to host threads), so pinning separates the containers from each other, not from the
# desktop; the desktop client is not pinned.
$Profiles = [ordered]@{
    "ctr-server-2c" = [ordered]@{ kind = "container"; cpus = 2; cpuset = "2,3"; memory = "1g"; build = "LinuxServerPerf (development)"; engine = "podman" }
    "ctr-bot-1c"    = [ordered]@{ kind = "container"; cpus = 1; cpuset = "4"; memory = "768m"; build = "LinuxServerPerf (development), headless client"; engine = "podman" }
    "ref-desktop"   = [ordered]@{ kind = "desktop"; build = "Win64Perf (development)"; resolution = "1920x1080 windowed" }
}

# Which (role kind, metric, stat) pairs get a budget, and how much headroom a budget leaves over the baseline median:
# a relative margin plus an absolute floor so near-zero values do not flap.
$BudgetedMetrics = @(
    @{ kind = "server"; metric = "tick_ms"; stat = "p95"; margin = 0.25; floor = 0.5 }
    @{ kind = "server"; metric = "tick_ms"; stat = "p99"; margin = 0.30; floor = 1.0 }
    @{ kind = "server"; metric = "main_thread_ms"; stat = "p95"; margin = 0.25; floor = 0.5 }
    @{ kind = "server"; metric = "physics_ms"; stat = "p95"; margin = 0.30; floor = 0.3 }
    @{ kind = "server"; metric = "physics_queries"; stat = "p95"; margin = 0.15; floor = 5 }
    @{ kind = "server"; metric = "gc_alloc_bytes"; stat = "p95"; margin = 0.25; floor = 512 }
    @{ kind = "server"; metric = "gc_alloc_bytes"; stat = "mean"; margin = 0.25; floor = 256 }
    @{ kind = "server"; metric = "tx_bytes_s"; stat = "p95"; margin = 0.20; floor = 1024 }
    @{ kind = "server"; metric = "rx_bytes_s"; stat = "p95"; margin = 0.20; floor = 1024 }
    @{ kind = "server"; metric = "messages_sent_s"; stat = "p95"; margin = 0.20; floor = 10 }
    @{ kind = "server"; metric = "total_used_mb"; stat = "max"; margin = 0.15; floor = 16 }
    @{ kind = "server"; metric = "network_objects"; stat = "max"; margin = 0.25; floor = 5 }
    @{ kind = "bot"; metric = "main_thread_ms"; stat = "p95"; margin = 0.25; floor = 0.5 }
    @{ kind = "bot"; metric = "gc_alloc_bytes"; stat = "p95"; margin = 0.25; floor = 512 }
    @{ kind = "bot"; metric = "tx_bytes_s"; stat = "p95"; margin = 0.20; floor = 512 }
    @{ kind = "bot"; metric = "rx_bytes_s"; stat = "p95"; margin = 0.20; floor = 1024 }
    @{ kind = "client"; metric = "frame_ms"; stat = "p95"; margin = 0.20; floor = 1.0 }
    @{ kind = "client"; metric = "frame_ms"; stat = "p99"; margin = 0.25; floor = 2.0 }
    @{ kind = "client"; metric = "main_thread_ms"; stat = "p95"; margin = 0.20; floor = 1.0 }
    @{ kind = "client"; metric = "gc_alloc_bytes"; stat = "p95"; margin = 0.25; floor = 512 }
    @{ kind = "client"; metric = "draw_calls"; stat = "p95"; margin = 0.10; floor = 10 }
    @{ kind = "client"; metric = "setpass_calls"; stat = "p95"; margin = 0.10; floor = 5 }
    @{ kind = "client"; metric = "rx_bytes_s"; stat = "p95"; margin = 0.20; floor = 1024 }
)

function Get-PerfProfiles { return $Profiles }

# server -> server, bot1 -> bot, client -> client.
function Get-RoleKind([string]$role) { return ($role -replace "\d+$", "") }

function New-PerfRunFolder([string]$Scenario) {
    $commit = (& git -C $ProjectRoot rev-parse --short HEAD).Trim()
    $folder = Join-Path $RunsRoot ("{0}_{1}_{2}" -f (Get-Date -Format "yyyy-MM-ddTHH-mm-ss"), $Scenario, $commit)
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    return $folder
}

# role -> parsed report, from every <role>.json in a run folder (run.json excluded).
function Read-PerfReports([string]$Folder) {
    $reports = [ordered]@{}
    foreach ($file in Get-ChildItem $Folder -Filter *.json | Where-Object { $_.Name -ne "run.json" } | Sort-Object Name) {
        $report = Get-Content $file.FullName -Raw | ConvertFrom-Json -Depth 20
        $reports[$report.role] = $report
    }
    return $reports
}

function Get-MetricValue($Report, [string]$Metric, [string]$Stat) {
    $entry = $Report.metrics.PSObject.Properties[$Metric]
    if (-not $entry) { return $null }
    $value = $entry.Value.PSObject.Properties[$Stat]
    if (-not $value) { return $null }
    return [double]$value.Value
}

function Read-Budgets {
    if (-not (Test-Path $BudgetsPath)) { return @() }
    return @((Get-Content $BudgetsPath -Raw | ConvertFrom-Json -Depth 20).budgets)
}

# One row per budget that applies to this run: value, limit, verdict (PASS, WARN, FAIL, MISSING).
function Test-PerfBudgets([string]$Scenario, $Reports, [hashtable]$RoleProfiles) {
    $rows = @()
    foreach ($budget in Read-Budgets | Where-Object { $_.scenario -eq $Scenario }) {
        $roles = @($Reports.Keys | Where-Object { (Get-RoleKind $_) -eq $budget.role -and $RoleProfiles[$_] -eq $budget.profile })
        foreach ($role in $roles) {
            $value = Get-MetricValue $Reports[$role] $budget.metric $budget.stat
            $verdict = if ($null -eq $value) { "MISSING" } elseif ($value -gt $budget.max) { "FAIL" } elseif ($value -gt $budget.warn) { "WARN" } else { "PASS" }
            $rows += [pscustomobject]@{
                Role = $role; Metric = "$($budget.metric).$($budget.stat)"; Value = $value; Warn = $budget.warn; Max = $budget.max; Verdict = $verdict
            }
        }
    }
    return $rows
}

# Relative check: each metric against the median of the last $Count runs of the same scenario and set-up (container-only
# or with the desktop client) that did not fail. Never fails a run; prints a drift warning when a value is more than
# $Threshold above that median.
function Get-PerfDrift([string]$Scenario, [string]$CurrentFolder, $Reports, [bool]$Desktop, [int]$Count = 5, [double]$Threshold = 0.15) {
    $previous = @(Get-PerfRuns -Scenario $Scenario | Where-Object {
            $_.Folder -ne $CurrentFolder -and $_.Verdict -ne "FAIL" -and [bool]$_.Record.desktop -eq $Desktop -and -not $_.Record.profileFrames
        } | Select-Object -Last $Count)
    if ($previous.Count -lt 2) { return @() }
    $history = @($previous | ForEach-Object { Read-PerfReports $_.Folder })
    $rows = @()
    foreach ($role in $Reports.Keys) {
        foreach ($spec in $BudgetedMetrics | Where-Object { $_.kind -eq (Get-RoleKind $role) }) {
            $value = Get-MetricValue $Reports[$role] $spec.metric $spec.stat
            $past = @($history | ForEach-Object { if ($_[$role]) { Get-MetricValue $_[$role] $spec.metric $spec.stat } } | Where-Object { $null -ne $_ })
            if ($null -eq $value -or $past.Count -lt 2) { continue }
            $median = Get-Median $past
            if ($value -gt $median * (1 + $Threshold) + $spec.floor) {
                $rows += [pscustomobject]@{ Role = $role; Metric = "$($spec.metric).$($spec.stat)"; Value = $value; Median = $median; Runs = $past.Count }
            }
        }
    }
    return $rows
}

function Get-Median([double[]]$values) {
    $sorted = @($values | Sort-Object)
    $n = $sorted.Count
    if ($n -eq 0) { return 0 }
    if ($n % 2 -eq 1) { return $sorted[[int][math]::Floor($n / 2)] }
    return ($sorted[$n / 2 - 1] + $sorted[$n / 2]) / 2
}

# run.json: what ran, where, and the verdict. The reports next to it are the data.
function Write-PerfRunRecord([string]$Folder, [System.Collections.IDictionary]$Info, $BudgetRows, $DriftRows) {
    $failed = @($BudgetRows | Where-Object { $_.Verdict -in @("FAIL", "MISSING") })
    # A profiled run is a diagnosis: the profiler's own cost skews it, so it neither passes nor fails.
    $verdict = if ($Info["profileFrames"]) { "DIAGNOSIS" } elseif ($failed.Count -gt 0) { "FAIL" } elseif (@($BudgetRows).Count -eq 0) { "NO-BUDGETS" } else { "PASS" }
    $record = [ordered]@{}
    foreach ($key in $Info.Keys) { $record[$key] = $Info[$key] }
    $record["verdict"] = $verdict
    $record["budgets"] = @($BudgetRows)
    $record["drift"] = @($DriftRows)
    $record | ConvertTo-Json -Depth 10 | Out-File (Join-Path $Folder "run.json") -Encoding utf8
    return $verdict
}

# Every run folder (oldest first), optionally for one scenario, with its run.json verdict.
function Get-PerfRuns([string]$Scenario = "") {
    if (-not (Test-Path $RunsRoot)) { return @() }
    $runs = @()
    foreach ($dir in Get-ChildItem $RunsRoot -Directory | Sort-Object Name) {
        $recordPath = Join-Path $dir.FullName "run.json"
        if (-not (Test-Path $recordPath)) { continue }
        $record = Get-Content $recordPath -Raw | ConvertFrom-Json -Depth 10
        if ($Scenario -and $record.scenario -ne $Scenario) { continue }
        $runs += [pscustomobject]@{ Id = $dir.Name; Folder = $dir.FullName; Scenario = $record.scenario; Commit = $record.commit; Verdict = $record.verdict; Record = $record }
    }
    return $runs
}

function Resolve-PerfRun([string]$Id) {
    $match = @(Get-PerfRuns | Where-Object { $_.Id -like "*$Id*" })
    if ($match.Count -ne 1) { throw "run '$Id' matches $($match.Count) run folders under $RunsRoot" }
    return $match[0]
}

# Budgets from the median of several runs: limit = median * (1 + margin) + floor, warn halfway. Replaces the budgets of the
# (role kind, profile) pairs these runs measured (optionally only the role kinds in $Roles) and keeps every other entry;
# the diff of budgets.json is the review.
function Set-PerfBudgetsFromRuns([string[]]$RunIds, [string]$Rationale, [string[]]$Roles = @()) {
    $runs = @($RunIds | ForEach-Object { Resolve-PerfRun $_ })
    $profiled = @($runs | Where-Object { $_.Record.profileFrames })
    if ($profiled.Count -gt 0) { throw "set-budgets refuses diagnosis runs (profiled): $(($profiled | ForEach-Object { $_.Id }) -join ', ')" }
    $scenario = $runs[0].Scenario
    if (@($runs | Where-Object { $_.Scenario -ne $scenario }).Count -gt 0) { throw "set-budgets takes runs of one scenario" }
    $commit = (& git -C $ProjectRoot rev-parse --short HEAD).Trim()
    $reportsByRun = @($runs | ForEach-Object { Read-PerfReports $_.Folder })
    $roleProfiles = [ordered]@{}
    foreach ($run in $runs) {
        foreach ($p in $run.Record.roleProfiles.PSObject.Properties) {
            if ($Roles.Count -gt 0 -and (Get-RoleKind $p.Name) -notin $Roles) { continue }
            $roleProfiles[$p.Name] = $p.Value
        }
    }

    $budgets = @()
    foreach ($role in $roleProfiles.Keys) {
        $kind = Get-RoleKind $role
        foreach ($spec in $BudgetedMetrics | Where-Object { $_.kind -eq $kind }) {
            $values = @($reportsByRun | ForEach-Object { if ($_[$role]) { Get-MetricValue $_[$role] $spec.metric $spec.stat } } | Where-Object { $null -ne $_ })
            if ($values.Count -eq 0) { continue }
            $median = Get-Median $values
            $budgets += [ordered]@{
                scenario = $scenario; role = $kind; profile = $roleProfiles[$role]; metric = $spec.metric; stat = $spec.stat
                baseline = [math]::Round($median, 3); runs = $values.Count; spread = [string]::Format([cultureinfo]::InvariantCulture, "{0:0.###}..{1:0.###}", ($values | Measure-Object -Minimum).Minimum, ($values | Measure-Object -Maximum).Maximum)
                warn = [math]::Round($median * (1 + $spec.margin / 2) + $spec.floor / 2, 3)
                max = [math]::Round($median * (1 + $spec.margin) + $spec.floor, 3)
                margin = $spec.margin; floor = $spec.floor; setAt = $commit; rationale = $Rationale
            }
        }
    }

    # One entry per (kind, profile, metric, stat): several bots of one kind share a budget, at their worst median.
    $merged = [ordered]@{}
    foreach ($b in $budgets) {
        $key = "$($b.role)|$($b.profile)|$($b.metric)|$($b.stat)"
        if (-not $merged.Contains($key) -or $merged[$key].max -lt $b.max) { $merged[$key] = $b }
    }

    $replaced = @($merged.Values | ForEach-Object { "$($_.role)|$($_.profile)" } | Select-Object -Unique)
    $existing = @(Read-Budgets | Where-Object { $_.scenario -ne $scenario -or "$($_.role)|$($_.profile)" -notin $replaced })
    # A stable order (scenario, role kind, profile, then the order of $BudgetedMetrics) and LF line endings, so a
    # re-baseline diffs as changed numbers only: the diff of budgets.json is the review.
    $kindOrder = @{ server = 0; bot = 1; client = 2 }
    $specOrder = @{}
    for ($i = 0; $i -lt $BudgetedMetrics.Count; $i++) { $specOrder["$($BudgetedMetrics[$i].kind)|$($BudgetedMetrics[$i].metric)|$($BudgetedMetrics[$i].stat)"] = $i }
    $all = @(@($existing) + @($merged.Values) | Sort-Object `
        @{ Expression = { $_.scenario } },
        @{ Expression = { $o = $kindOrder[[string]$_.role]; if ($null -eq $o) { 9 } else { $o } } },
        @{ Expression = { $_.profile } },
        @{ Expression = { $o = $specOrder["$($_.role)|$($_.metric)|$($_.stat)"]; if ($null -eq $o) { 999 } else { $o } } })
    $document = [ordered]@{
        format = 1
        note = "Perf budgets: limits per scenario, role kind, environment profile, metric and stat. Written by .tools/perf.ps1 set-budgets from baseline runs (median * (1 + margin) + floor; warn halfway); edit by hand only with a rationale. See .docs/PERFORMANCE.md."
        profiles = $Profiles
        budgets = $all
    }
    New-Item -ItemType Directory -Path (Split-Path $BudgetsPath) -Force | Out-Null
    $json = ($document | ConvertTo-Json -Depth 10) -replace "`r`n", "`n"
    [IO.File]::WriteAllText($BudgetsPath, $json + "`n", [Text.UTF8Encoding]::new($false))
    return @($merged.Values)
}

# Table of the budgeted metrics for two runs side by side.
function Compare-PerfRuns([string]$A, [string]$B) {
    $runA = Resolve-PerfRun $A
    $runB = Resolve-PerfRun $B
    $reportsA = Read-PerfReports $runA.Folder
    $reportsB = Read-PerfReports $runB.Folder
    $rows = @()
    foreach ($role in @($reportsA.Keys) + @($reportsB.Keys) | Select-Object -Unique) {
        foreach ($spec in $BudgetedMetrics | Where-Object { $_.kind -eq (Get-RoleKind $role) }) {
            $va = if ($reportsA[$role]) { Get-MetricValue $reportsA[$role] $spec.metric $spec.stat } else { $null }
            $vb = if ($reportsB[$role]) { Get-MetricValue $reportsB[$role] $spec.metric $spec.stat } else { $null }
            $change = if ($null -ne $va -and $null -ne $vb -and $va -ne 0) { "{0:+0.0;-0.0;0}%" -f (($vb - $va) / $va * 100) } else { "" }
            $rows += [pscustomobject]@{ Role = $role; Metric = "$($spec.metric).$($spec.stat)"; A = $va; B = $vb; Change = $change }
        }
    }
    return $rows
}

# One metric (role.metric.stat, e.g. server.tick_ms.p95) across every run of a scenario.
function Get-PerfTrend([string]$Scenario, [string]$Path) {
    $parts = $Path -split "\."
    if ($parts.Count -ne 3) { throw "metric path is role.metric.stat, e.g. server.tick_ms.p95" }
    $rows = @()
    foreach ($run in Get-PerfRuns -Scenario $Scenario) {
        $reports = Read-PerfReports $run.Folder
        if (-not $reports[$parts[0]]) { continue }
        $rows += [pscustomobject]@{ Run = $run.Id; Commit = $run.Commit; Verdict = $run.Verdict; Value = (Get-MetricValue $reports[$parts[0]] $parts[1] $parts[2]) }
    }
    return $rows
}

# C# for `unity cmd eval`: loads a capture made with -perfprofile (it records allocation call stacks) into the Editor's
# Profiler and sums every GC.Alloc sample by the innermost TinCan frame of its call stack (the innermost frame overall
# after "<="), bytes and allocations per frame, top $Top. Main thread only.
function Get-GcAttributionCode([string]$RawPath, [int]$Top = 30) {
    $escaped = $RawPath.Replace('"', '""')
    return @"
var path = @"$escaped";
UnityEditorInternal.ProfilerDriver.ClearAllFrames();
if (!UnityEditorInternal.ProfilerDriver.LoadProfile(path, false)) return "could not load " + path;
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
var bytesBy = new System.Collections.Generic.Dictionary<string, double>();
var countBy = new System.Collections.Generic.Dictionary<string, int>();
int frames = 0, noStack = 0;
int gcCol = UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory;
var addrs = new System.Collections.Generic.List<ulong>();
var values = new System.Collections.Generic.List<float>();
var children = new System.Collections.Generic.List<int>();
for (int f = first; f <= last; f++)
{
    using (var view = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, gcCol, false))
    {
        if (view == null || !view.valid) continue;
        frames++;
        var stack = new System.Collections.Generic.Stack<int>();
        stack.Push(view.GetRootItemID());
        while (stack.Count > 0)
        {
            int id = stack.Pop();
            children.Clear();
            view.GetItemChildren(id, children);
            foreach (var c in children)
            {
                if (view.GetItemName(c) != "GC.Alloc") { stack.Push(c); continue; }
                int n = view.GetItemMergedSamplesCount(c);
                view.GetItemMergedSamplesColumnDataAsFloats(c, gcCol, values);
                for (int s = 0; s < n; s++)
                {
                    view.GetItemMergedSampleCallstack(c, s, addrs);
                    if (addrs.Count == 0) { noStack++; continue; }
                    string game = null, leaf = null;
                    foreach (var addr in addrs)
                    {
                        string name = view.ResolveMethodInfo(addr).methodName;
                        if (string.IsNullOrEmpty(name)) continue;
                        if (leaf == null) leaf = name;
                        if (name.Contains("TinCan.")) { game = name; break; }
                    }
                    string key = (game ?? "(no TinCan frame)") + "  <=  " + (leaf ?? "?");
                    bytesBy.TryGetValue(key, out var b); bytesBy[key] = b + (s < values.Count ? values[s] : 0f);
                    countBy.TryGetValue(key, out var k); countBy[key] = k + 1;
                }
            }
        }
    }
}
double all = 0; foreach (var v in bytesBy.Values) all += v;
var sb = new System.Text.StringBuilder();
sb.Append("frames ").Append(frames).Append(", GC ").Append((all / System.Math.Max(1, frames)).ToString("0")).Append(" B/frame").Append(noStack > 0 ? ", samples without a call stack " + noStack : "").Append("\n");
foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(bytesBy, x => x.Value), $Top))
    sb.Append((kv.Value / frames).ToString("0").PadLeft(7)).Append(" B/f").Append(((double)countBy[kv.Key] / frames).ToString("0.0").PadLeft(7)).Append(" allocs/f  ").Append(kv.Key).Append("\n");
return sb.ToString();
"@
}

Export-ModuleMember -Function Get-GcAttributionCode, Get-PerfProfiles, Get-RoleKind, New-PerfRunFolder, Read-PerfReports, Get-MetricValue, Read-Budgets,
    Test-PerfBudgets, Get-PerfDrift, Get-Median, Write-PerfRunRecord, Get-PerfRuns, Resolve-PerfRun, Set-PerfBudgetsFromRuns,
    Compare-PerfRuns, Get-PerfTrend
