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

Export-ModuleMember -Function Get-ProjectRoot, Start-ToolLog, Write-Tier, Write-Log, Write-Section, Stop-Unusable, Get-UnusableReason `
    -Variable ExitPass, ExitFail, ExitUnusable
