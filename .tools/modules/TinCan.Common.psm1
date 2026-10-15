# Shared by every .tools entry script: project root, console and log output, and the "environment unusable" error.
# Module rules (no exit, no script-scope reads, explicit exports): .tools/README.md, "Modules".

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 1.0

$ExitPass = 0
$ExitFail = 1
$ExitUnusable = 2
$UnusableMarker = "TinCan.Unusable"
# The file Write-Log appends to, set by Start-ToolLog; $null means console only.
$script:LogFile = $null

function Get-ProjectRoot {
    return Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

# Starts .tools/logs/<name>-<timestamp>.log as the file Write-Log appends to, and returns its path.
function Start-ToolLog([string]$name) {
    $logDir = Join-Path (Get-ProjectRoot) ".tools/logs"
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    $script:LogFile = Join-Path $logDir "$name-$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss').log"
    return $script:LogFile
}

function Write-Tier([string]$name, [string]$state, [string]$detail = "") {
    $color = switch ($state) { "PASS" { "Green" } "FAIL" { "Red" } default { "Yellow" } }
    Write-Host ("[{0,-7}] {1,-4} {2}" -f $name, $state, $detail) -ForegroundColor $color
}

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $Timestamp = Get-Date -Format "HH:mm:ss"
    $LogMessage = "[$Timestamp] [$Level] $Message"
    Write-Host $LogMessage
    if ($script:LogFile) { Add-Content -Path $script:LogFile -Value $LogMessage }
}

function Write-Section {
    param([string]$Title)
    Write-Host "`n" -NoNewline
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host $Title -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

# True when a person can answer a question: a console with input that is not redirected, not -NonInteractive. Agents,
# CI and scheduled runs are not interactive.
function Test-Interactive {
    if (-not [Environment]::UserInteractive -or [Console]::IsInputRedirected) { return $false }
    return -not ([Environment]::GetCommandLineArgs() | Where-Object { $_ -like "-NonI*" })
}

# A CLI-style yes/no question, default No. Prints why the step is needed first. Returns $false without asking when
# nobody can answer (Test-Interactive), so an unattended run never blocks on a question.
function Confirm-Step([string]$Question, [string[]]$Explanation = @()) {
    foreach ($line in $Explanation) { Write-Host "  $line" }
    if (-not (Test-Interactive)) { return $false }
    $answer = Read-Host "$Question [y/N]"
    return $answer -match "^(y|yes)$"
}

# The Editor (or another part of the environment) cannot be used: busy, a modal dialog, a timeout. Modules throw this;
# the entry script reports it and exits with $ExitUnusable (see Get-UnusableReason).
function Stop-Unusable([string]$reason) {
    $exception = [System.Exception]::new($reason)
    $exception.Data[$UnusableMarker] = $true
    throw $exception
}

# The reason when the error came from Stop-Unusable, else $null (the entry script then rethrows).
function Get-UnusableReason([System.Management.Automation.ErrorRecord]$record) {
    if ($record.Exception.Data[$UnusableMarker]) { return $record.Exception.Message }
    return $null
}

# One Editor and one working tree, shared by every agent session and person on this machine. Scripts that drive the
# open Editor (compile, tests, Play, builds) hold .tools/logs/editor.lock while they do, so two runs never overlap.
# The file is JSON: who holds it, for what, since when, and the holder's process (pid + start time in UTC ticks). A holder whose
# process is gone is stale and is taken over. Name the holder with $env:TINCAN_LOCK_OWNER (an agent puts its session
# name there); otherwise it is the user name.
function Get-EditorLockPath {
    return Join-Path (Get-ProjectRoot) ".tools/logs/editor.lock"
}

# The current holder as an object, or $null when the Editor is free.
function Get-EditorLock {
    $path = Get-EditorLockPath
    if (-not (Test-Path $path)) { return $null }
    try { return Get-Content $path -Raw | ConvertFrom-Json } catch { return $null }
}

function Test-EditorLockStale($holder) {
    if (-not $holder -or -not $holder.pid) { return $true }
    $process = Get-Process -Id $holder.pid -ErrorAction SilentlyContinue
    if (-not $process) { return $true }
    # Windows reuses process ids: the same id with another start time is another process.
    $started = 0L
    if (-not [long]::TryParse([string]$holder.processStarted, [ref]$started)) { return $true }
    return $process.StartTime.ToUniversalTime().Ticks -ne $started
}

function Format-EditorLockHolder($holder) {
    return "$($holder.owner) ($($holder.purpose), since $($holder.since), pid $($holder.pid))"
}

# Takes the Editor lock for this process, waiting up to $WaitSeconds while someone else holds it (0: do not wait).
# Throws Stop-Unusable (exit code 2) when the wait runs out. Release it with Exit-EditorLock in a finally block.
function Enter-EditorLock([string]$Purpose, [int]$WaitSeconds = 1800) {
    $path = Get-EditorLockPath
    $logDir = Split-Path -Parent $path
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

    $self = Get-Process -Id $PID
    $owner = if ($env:TINCAN_LOCK_OWNER) { $env:TINCAN_LOCK_OWNER } else { $env:USERNAME }
    $record = [ordered]@{
        owner = $owner; purpose = $Purpose; since = [DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss", [CultureInfo]::InvariantCulture)
        pid = $PID; processStarted = $self.StartTime.ToUniversalTime().Ticks
    } | ConvertTo-Json -Compress
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    $waiting = $false

    while ($true) {
        # Written beside the lock and moved into place: the lock appears whole or not at all, and the move fails when
        # someone else's lock is already there.
        $temp = "$path.$PID.tmp"
        Set-Content -Path $temp -Value $record -NoNewline
        try {
            [System.IO.File]::Move($temp, $path, $false)
            if ($waiting) { Write-Tier "Lock" "note" "the Editor is free; continuing" }
            return
        }
        catch {
            Remove-Item $temp -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path $path)) { throw }
        }

        $holder = Get-EditorLock
        if ($holder -and $holder.pid -eq $PID) { return }
        if (Test-EditorLockStale $holder) {
            # Removed only if it is still the same stale lock, not one a faster waiter just took.
            $again = Get-EditorLock
            if (-not $again -or ($holder -and $again.pid -eq $holder.pid -and $again.since -eq $holder.since)) {
                Remove-Item $path -Force -ErrorAction SilentlyContinue
                Write-Tier "Lock" "note" "took over a stale lock$(if ($holder) { ' from ' + (Format-EditorLockHolder $holder) })"
            }
            continue
        }

        if ((Get-Date) -ge $deadline) { Stop-Unusable "the Editor is in use by $(Format-EditorLockHolder $holder). Wait for it, or rerun later." }
        if (-not $waiting) {
            Write-Tier "Lock" "wait" "the Editor is in use by $(Format-EditorLockHolder $holder); waiting up to $WaitSeconds s"
            $waiting = $true
        }
        Start-Sleep -Seconds 5
    }
}

# Releases the Editor lock if this process holds it; safe to call when it does not.
function Exit-EditorLock {
    $holder = Get-EditorLock
    if ($holder -and $holder.pid -eq $PID) { Remove-Item (Get-EditorLockPath) -Force -ErrorAction SilentlyContinue }
}

Export-ModuleMember -Function Get-ProjectRoot, Start-ToolLog, Write-Tier, Write-Log, Write-Section, Test-Interactive, Confirm-Step, Stop-Unusable, Get-UnusableReason, `
    Get-EditorLock, Enter-EditorLock, Exit-EditorLock `
    -Variable ExitPass, ExitFail, ExitUnusable
