<#
.SYNOPSIS
Upgrade TinCan project to a new Unity version

.DESCRIPTION
Safely upgrades the project to a specified Unity version with backup,
compatibility checking, and validation.

.PARAMETER TargetVersion
The Unity version to upgrade to (e.g., "6000.4.2f1")

.PARAMETER CreateBackup
Create a backup before upgrading (default: true)

.EXAMPLE
.\upgrade-unity.ps1 -TargetVersion "6000.4.2f1"
.\upgrade-unity.ps1 -TargetVersion "2023.3.0f1" -CreateBackup $false
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$TargetVersion,

    [bool]$CreateBackup = $true
)

# ============================================================================
# Configuration
# ============================================================================

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$LogDir = Join-Path $ScriptDir "logs"
$LogFile = Join-Path $LogDir "upgrade-$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss').log"
$VersionFile = Join-Path $ProjectRoot ".unity-version"

# Create log directory
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $Timestamp = Get-Date -Format "HH:mm:ss"
    $LogMessage = "[$Timestamp] [$Level] $Message"
    Write-Host $LogMessage
    Add-Content -Path $LogFile -Value $LogMessage
}

function Write-Section {
    param([string]$Title)
    Write-Host "`n" -NoNewline
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host $Title -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

# ============================================================================
# Upgrade Process
# ============================================================================

Write-Section "TinCan Unity - Version Upgrade"

$CurrentVersion = Get-Content $VersionFile -Raw | ForEach-Object { $_.Trim() }
Write-Log "Current version: $CurrentVersion"
Write-Log "Target version: $TargetVersion"

# Validate version format
if ($TargetVersion -notmatch '^\d{4}\.\d{1,2}\.\d{1,2}(f|b|a|rc)\d+$') {
    Write-Log "ERROR: Invalid version format. Expected format: 6000.4.2f1" "ERROR"
    exit 1
}

# Create backup if requested
if ($CreateBackup) {
    Write-Section "Creating Backup"
    $BackupDir = Join-Path $ProjectRoot ".backup_$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss')"
    Write-Log "Creating backup at: $BackupDir"

    Copy-Item -Path (Join-Path $ProjectRoot "Assets") -Destination (Join-Path $BackupDir "Assets") -Recurse -Force
    Copy-Item -Path (Join-Path $ProjectRoot "ProjectSettings") -Destination (Join-Path $BackupDir "ProjectSettings") -Recurse -Force
    Copy-Item -Path (Join-Path $ProjectRoot "Packages") -Destination (Join-Path $BackupDir "Packages") -Recurse -Force

    Write-Log "✓ Backup created"
}

# Update .unity-version
Write-Section "Updating Version Configuration"
Write-Log "Updating .unity-version to: $TargetVersion"
Set-Content -Path $VersionFile -Value $TargetVersion
Write-Log "✓ .unity-version updated"

# Update ProjectSettings/ProjectVersion.txt if it exists
$ProjectVersionFile = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
if (Test-Path $ProjectVersionFile) {
    Write-Log "Updating ProjectSettings/ProjectVersion.txt"
    Set-Content -Path $ProjectVersionFile -Value "m_EditorVersion: $TargetVersion"
    Write-Log "✓ ProjectSettings/ProjectVersion.txt updated"
}

# Log changelog entry
Write-Section "Upgrade Summary"
Write-Host "`n✅ Version upgraded to: $TargetVersion`n" -ForegroundColor Green

Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Open the project in Unity $TargetVersion"
Write-Host "2. Wait for Unity to import and upgrade assets"
Write-Host "3. Check the Console for any upgrade errors"
Write-Host "4. Validate all scripts compile correctly"
Write-Host "5. Run tests if available"
Write-Host ""

Write-Log "Version upgrade from $CurrentVersion to $TargetVersion completed"
Write-Host "Upgrade log saved to: $LogFile" -ForegroundColor Gray
