<#
.SYNOPSIS
Perf runs: play a load scenario in containers (and optionally on the desktop), record a run folder, and check it
against the budgets in .docs/perf/budgets.json.

.DESCRIPTION
Commands:
  run <Scenario>        Run a load scenario. Needs the perf builds (.\.tools\build.ps1 -Perf -Image -Client) and the
                        Podman machine running. Writes Logs/perf/runs/<time>_<Scenario>_<commit>/ with one report per
                        role, the container logs and run.json (verdict, budget rows, drift warnings).
  list                  Every run folder with its verdict.
  compare <A> <B>       Budgeted metrics of two runs side by side (a run is any unique part of its folder name).
  trend <Scenario> <role.metric.stat>   One metric across every run of a scenario, e.g. server.tick_ms.p95.
  run <Scenario> -ServerFps <n>   Override the server's frame cap (default: the tick rate, 30). An experiment knob:
                        such runs only compare with runs of the same cap.
  run <Scenario> -NetSim <preset>   Simulated latency on the server and every bot (Lag50, Lag100, Lag200, Lossy);
                        such runs only compare with runs of the same preset.
  run <Scenario> -ProfileFrames <n>   A diagnosis run: the server also records a Unity profiler capture (server.raw)
                        of the window's first n frames. Profiling skews the numbers, so such runs are left out of
                        drift history and set-budgets refuses them.
  analyze-gc <run>      For a -ProfileFrames run: load server.raw in the open Editor and list where the server
                        allocates (bytes and allocations per frame, by the innermost TinCan method on the call stack).
  set-budgets <runs...> Set budgets from the median of these runs (-Rationale says why; -Roles server,bot limits it to
                        those role kinds). Replaces only the role kinds and profiles these runs measured.

Scenarios:
  CrewLoad   A dedicated server and three headless bot clients on DeckWalk (looping), each in its own container with
             pinned cores; the fourth player is a fourth bot, or with -Desktop the Windows perf client rendering at
             1920x1080 on this PC (it opens a window).

Exit code: 0 pass (or no budgets yet), 1 a budget failed, 2 the environment was not usable.
Plan: .docs/plans/performance-budgets.md. Reading the results: .docs/PERFORMANCE.md.

.EXAMPLE
.\.tools\perf.ps1 run CrewLoad
.\.tools\perf.ps1 run CrewLoad -Desktop -Repeat 3
.\.tools\perf.ps1 compare 2026-10-03T01-10 2026-10-03T01-20
.\.tools\perf.ps1 trend CrewLoad server.tick_ms.p95
.\.tools\perf.ps1 set-budgets 01-10 01-20 01-30 -Rationale "first baseline"
#>

#Requires -Version 7.0

param(
    [Parameter(Position = 0, Mandatory)]
    [ValidateSet("run", "list", "compare", "trend", "set-budgets", "analyze-gc")]
    [string]$Command,
    [Parameter(Position = 1, ValueFromRemainingArguments)]
    [string[]]$Arguments = @(),
    [switch]$Desktop,
    [int]$Repeat = 1,
    [double]$WarmupSeconds = 30,
    [double]$DurationSeconds = 90,
    [string]$Rationale = "baseline",
    [string[]]$Roles = @(),
    [int]$ProfileFrames = 0,
    [int]$Seed = 1003,
    [int]$ServerFps = 0,
    [string]$NetSim = ""
)

$ErrorActionPreference = "Stop"
foreach ($module in "Common", "Editor", "Host", "Container", "Perf") {
    Import-Module (Join-Path $PSScriptRoot "modules/TinCan.$module.psm1") -Force
}

$Roles = @($Roles | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$ProjectRoot = Get-ProjectRoot
$Image = "tincan-server-perf:local"
$Network = "tincan-perf"
$ServerName = "perf-server"
$Scenarios = @("CrewLoad")

function Invoke-CrewLoad([string]$Folder) {
    $profiles = Get-PerfProfiles
    $server = $profiles["ctr-server-2c"]
    $bot = $profiles["ctr-bot-1c"]
    $botCount = if ($Desktop) { 3 } else { 4 }
    $botCores = @("4", "5", "6", "7")
    $common = @("-perf", "-perflabel", "CrewLoad", "-perfduration", "$DurationSeconds", "-perfout", "/perf", "-perfquit")
    $serverExtra = @()
    if ($ProfileFrames -gt 0) { $serverExtra += @("-perfprofile", "$ProfileFrames") }
    if ($ServerFps -gt 0) { $serverExtra += @("-serverfps", "$ServerFps") }
    # Simulated network conditions (harness presets; each peer delays what it sends): the server and every bot get the same.
    $netsim = if ($NetSim) { @("-netsim", $NetSim) } else { @() }
    $serverExtra += $netsim

    Initialize-ContainerNetwork $Network
    # Only the desktop client connects from outside the container network; bots use it directly. Publishing 7777 otherwise
    # would collide with a server container already running on this PC.
    $publish = if ($Desktop) { @("7777:7777/udp") } else { @() }
    $names = @($ServerName)
    $roleProfiles = @{ server = "ctr-server-2c" }

    Write-Tier "Perf" "note" "server ($($server.cpus) cpus, $($server.memory)), $botCount bot(s)$(if ($Desktop) { ' + desktop client' })"
    Start-GameContainer -Name $ServerName -Image $Image -Network $Network -Cpus $server.cpus -CpusetCpus $server.cpuset `
        -Memory $server.memory -HostFolder $Folder -Publish $publish `
        -Arguments (@("-server", "-seed", "$Seed", "-perfrole", "server", "-perfwarmup", "$WarmupSeconds") + $serverExtra + $common) | Out-Null
    if (-not (Wait-ContainerLog $ServerName "Starting a dedicated server" 60)) { Stop-Unusable "the perf server did not start (see $(Join-Path $Folder "$ServerName.log"))" }
    $serverUp = Get-Date

    # Every client's window should match the server's: its warm-up ends when the server's does.
    for ($i = 1; $i -le $botCount; $i++) {
        $name = "perf-bot$i"
        $warmup = [math]::Max(5, $WarmupSeconds - ((Get-Date) - $serverUp).TotalSeconds - 2)
        Start-GameContainer -Name $name -Image $Image -Network $Network -Cpus $bot.cpus -CpusetCpus $botCores[$i - 1] `
            -Memory $bot.memory -HostFolder $Folder `
            -Arguments (@("-autojoin", "${ServerName}:7777", "-bot", "DeckWalk", "-botloop", "-perfrole", "bot$i", "-perfwarmup", "$([int]$warmup)") + $netsim + $common) | Out-Null
        $names += $name
        $roleProfiles["bot$i"] = "ctr-bot-1c"
    }

    $client = $null
    if ($Desktop) {
        $exe = Join-Path $ProjectRoot "Builds/Win64Perf/TinCan.exe"
        if (-not (Test-Path $exe)) { Stop-Unusable "no desktop perf client at $exe; run .\.tools\build.ps1 -Perf -Client" }
        $warmup = [math]::Max(5, $WarmupSeconds - ((Get-Date) - $serverUp).TotalSeconds - 2)
        $client = Start-Process -FilePath $exe -PassThru -ArgumentList (@("-autojoin", "127.0.0.1:7777", "-bot", "DeckWalk", "-botloop",
            "-perfrole", "client", "-perfwarmup", "$([int]$warmup)", "-screen-width", "1920", "-screen-height", "1080",
            "-screen-fullscreen", "0", "-logFile", "`"$(Join-Path $Folder 'client.log')`"") + ($common -replace "^/perf$", "`"$Folder`""))
        $roleProfiles["client"] = "ref-desktop"
    }

    $timeout = [int]($WarmupSeconds + $DurationSeconds + 90)
    $stuck = @(Wait-ContainersExit $names $timeout)
    if ($client) {
        if (-not $client.WaitForExit(30000)) { $client.Kill(); Write-Tier "Perf" "note" "desktop client did not quit; killed" }
    }
    Save-ContainerLogs $names $Folder
    if ($stuck.Count -gt 0) { Write-Tier "Perf" "note" "still running at the timeout, removed: $($stuck -join ', ')" }
    return $roleProfiles
}

function Invoke-Run([string]$Scenario) {
    if ($Scenario -notin $Scenarios) { throw "unknown scenario '$Scenario' (known: $($Scenarios -join ', '))" }
    if (-not (Test-ContainerEngine)) { Stop-Unusable "Podman is not reachable; start it with: podman machine start" }
    # Without its firewall rule the desktop client makes Windows show a prompt and wait, and an unattended run stalls.
    # Guided: asks a person; with nobody to ask it stops here instead of launching the client.
    if ($Desktop -and -not (Confirm-FirewallRules)) {
        Stop-Unusable "the desktop client needs its Windows Firewall rule first (one-time): run .\.tools\setup.ps1 -Only Firewall in an interactive PowerShell"
    }

    $folder = New-PerfRunFolder $Scenario
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $roleProfiles = Invoke-CrewLoad $folder
    $reports = Read-PerfReports $folder
    $missing = @($roleProfiles.Keys | Where-Object { -not $reports.Contains($_) })

    foreach ($role in $reports.Keys) {
        $r = $reports[$role]
        $line = @("frame_ms", "main_thread_ms", "tick_ms", "gc_alloc_bytes", "tx_bytes_s", "rx_bytes_s") | ForEach-Object {
            $p95 = Get-MetricValue $r $_ "p95"
            if ($null -ne $p95) { "$_ p95 $p95" }
        }
        Write-Host ("  {0,-7} {1}" -f $role, ($line -join " | "))
    }

    $ack = Get-BotAckLatency $folder
    if ($ack.Count -gt 0) { Write-Host ("  input ack (ms): " + (($ack.Keys | ForEach-Object { "$_ $($ack[$_])" }) -join ", ")) }

    $budgetRows = @(Test-PerfBudgets $Scenario $reports $roleProfiles)
    foreach ($role in $missing) { $budgetRows += [pscustomobject]@{ Role = $role; Metric = "report"; Value = $null; Warn = $null; Max = $null; Verdict = "MISSING" } }
    $driftRows = @(Get-PerfDrift $Scenario $folder $reports ([bool]$Desktop) $Seed $ServerFps $NetSim)
    $commit = (& git -C $ProjectRoot rev-parse --short HEAD).Trim()
    $dirty = [bool](& git -C $ProjectRoot status --porcelain)
    $verdict = Write-PerfRunRecord $folder ([ordered]@{
        scenario = $Scenario; commit = $commit; dirty = $dirty; startedLocal = (Get-Date).AddSeconds(-$clock.Elapsed.TotalSeconds).ToString("s")
        durationS = [int]$clock.Elapsed.TotalSeconds; warmupS = $WarmupSeconds; windowS = $DurationSeconds; desktop = [bool]$Desktop
        roleProfiles = $roleProfiles; profiles = (Get-PerfProfiles); image = $Image; profileFrames = $ProfileFrames; seed = $Seed; serverFps = $ServerFps; netsim = $NetSim
        botAckMs = (Get-BotAckLatency $folder)
    }) $budgetRows $driftRows

    foreach ($row in $budgetRows | Where-Object { $_.Verdict -ne "PASS" }) {
        Write-Tier "Budget" $(if ($row.Verdict -eq "WARN") { "note" } else { "FAIL" }) ("{0,-7} {1,-24} {2} (warn {3}, max {4})" -f $row.Role, $row.Metric, $row.Value, $row.Warn, $row.Max)
    }
    foreach ($row in $driftRows) { Write-Tier "Drift" "note" ("{0,-7} {1,-24} {2} vs median {3} of {4} runs" -f $row.Role, $row.Metric, $row.Value, $row.Median, $row.Runs) }
    $state = switch ($verdict) { "PASS" { "PASS" } "NO-BUDGETS" { "note" } "DIAGNOSIS" { "note" } "EXPERIMENT" { "note" } default { "FAIL" } }
    Write-Tier "Perf" $state "$Scenario $verdict ($([int]$clock.Elapsed.TotalSeconds) s, $(@($budgetRows).Count) budget checks) -> $folder"
    return $verdict
}

try {
    switch ($Command) {
        "run" {
            if ($Arguments.Count -lt 1) { throw "run needs a scenario: $($Scenarios -join ', ')" }
            $failed = 0
            for ($i = 1; $i -le $Repeat; $i++) {
                if ($Repeat -gt 1) { Write-Tier "Perf" "note" "run $i of $Repeat" }
                if ((Invoke-Run $Arguments[0]) -eq "FAIL") { $failed++ }
            }
            exit $(if ($failed -gt 0) { $ExitFail } else { $ExitPass })
        }
        "analyze-gc" {
            if ($Arguments.Count -ne 1) { throw "analyze-gc needs a run made with -ProfileFrames" }
            $raw = Join-Path (Resolve-PerfRun $Arguments[0]).Folder "server.raw"
            if (-not (Test-Path $raw)) { throw "no server.raw in that run; make one with: perf.ps1 run CrewLoad -ProfileFrames 150" }
            if (-not (Test-EditorAlive)) { Stop-Unusable "the Editor does not answer; analyze-gc loads the capture in the open Editor" }
            $result = Invoke-Unity @("eval", "--code", (Get-GcAttributionCode $raw)) 900
            if ($result.result) { Write-Host $result.result } else { throw "analysis failed: $($result | ConvertTo-Json -Depth 5 -Compress)" }
        }
        "list" { Get-PerfRuns | Select-Object Id, Verdict | Format-Table -AutoSize | Out-String | Write-Host }
        "compare" {
            if ($Arguments.Count -ne 2) { throw "compare needs two runs" }
            Compare-PerfRuns $Arguments[0] $Arguments[1] | Format-Table -AutoSize | Out-String | Write-Host
        }
        "trend" {
            if ($Arguments.Count -ne 2) { throw "trend needs a scenario and role.metric.stat" }
            Get-PerfTrend $Arguments[0] $Arguments[1] | Format-Table -AutoSize | Out-String | Write-Host
        }
        "set-budgets" {
            if ($Arguments.Count -lt 1) { throw "set-budgets needs runs" }
            $written = Set-PerfBudgetsFromRuns $Arguments $Rationale $Roles
            $written | ForEach-Object { [pscustomobject]$_ } | Select-Object role, profile, metric, stat, baseline, spread, warn, max |
                Format-Table -AutoSize | Out-String | Write-Host
            Write-Tier "Budgets" "PASS" "$(@($written).Count) budgets written to .docs/perf/budgets.json"
        }
    }
    exit $ExitPass
}
catch {
    $reason = Get-UnusableReason $_
    if (-not $reason) { throw }
    Write-Tier "Perf" "STOP" $reason
    exit $ExitUnusable
}
