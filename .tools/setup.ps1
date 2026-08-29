<#
.SYNOPSIS
Initialize TinCan project with required folders, packages, and configuration

.DESCRIPTION
One-command setup for new developers. Installs the Unity CLI via winget if missing,
configures telemetry consent, installs the required Unity Editor, and prepares the
project for development.

.PARAMETER UnityPath
Optional: Direct path to Unity executable. If not provided, searches Unity Hub.

.PARAMETER EnableUnityTelemetry
Opt in to Unity CLI analytics. Setup opts out by default so fresh installs never
stop at the first-run telemetry prompt.

.EXAMPLE
.\setup.ps1
.\setup.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.4.5f1"
#>

#Requires -Version 7.0

param(
    [string]$UnityPath,
    [switch]$EnableUnityTelemetry
)

# ============================================================================
# Configuration
# ============================================================================

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$LogDir = Join-Path $ScriptDir "logs"
$LogFile = Join-Path $LogDir "setup-$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss').log"

# Folders git cannot guarantee (tracked project folders carry .gitkeep files instead)
$RequiredFolders = @(
    ".tools\logs"
)

# Create log directory if it doesn't exist
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

function Update-ProcessPath {
    $MachinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $env:Path = "$MachinePath;$UserPath"
}

# ============================================================================
# Validation Functions
# ============================================================================

function Test-UnityVersion {
    param([string]$TargetVersion)

    Write-Log "Checking for Unity version: $TargetVersion"

    # Check if .unity-version file exists
    $VersionFile = Join-Path $ProjectRoot ".unity-version"
    if (-not (Test-Path $VersionFile)) {
        Write-Log "ERROR: .unity-version file not found at $VersionFile" "ERROR"
        return $false
    }

    $ActualVersion = Get-Content $VersionFile -Raw | ForEach-Object { $_.Trim() }
    Write-Log "Detected version from .unity-version: $ActualVersion"

    return $true
}

function Find-UnityEditor {
    param([string]$TargetVersion)

    Write-Log "Searching for Unity Editor: $TargetVersion"

    if ($UnityPath -and (Test-Path $UnityPath)) {
        Write-Log "Using provided Unity path: $UnityPath"
        return $UnityPath
    }

    # Prefer the Unity CLI (https://docs.unity.com/en-us/unity-cli) when available
    if (Get-Command unity -ErrorAction SilentlyContinue) {
        Write-Log "Unity CLI detected, checking installed editors..."
        $Installed = unity editors -i --format json 2>$null | ConvertFrom-Json
        $Match = $Installed.data | Where-Object { $_.version -eq $TargetVersion } | Select-Object -First 1
        if ($Match -and $Match.location) {
            Write-Log "Found Unity via CLI at: $($Match.location)"
            return $Match.location
        }
        Write-Log "Unity CLI installed, but version $TargetVersion is not installed" "WARN"
    }

    # Fallback: default Unity Hub installation paths
    $UnityHubPaths = @(
        "C:\Program Files\Unity\Hub\Editor\$TargetVersion\Editor\Unity.exe",
        "C:\Program Files (x86)\Unity\Hub\Editor\$TargetVersion\Editor\Unity.exe"
    )

    foreach ($Path in $UnityHubPaths) {
        if (Test-Path $Path) {
            Write-Log "Found Unity at: $Path"
            return $Path
        }
    }

    Write-Log "Could not find Unity Editor for version $TargetVersion" "WARN"
    if (Get-Command unity -ErrorAction SilentlyContinue) {
        Write-Log "Install it with: unity install $TargetVersion" "WARN"
    } else {
        Write-Log "Install the Unity CLI (https://docs.unity.com/en-us/unity-cli) or Unity Hub: https://unity.com/download" "WARN"
    }
    return $null
}

function Install-UnityCli {
    if (Get-Command unity -ErrorAction SilentlyContinue) {
        Write-Log "Unity CLI already installed"
        return $true
    }

    Write-Log "Unity CLI not found, installing via winget..."
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        Write-Log "winget not available. Install the Unity CLI manually: https://docs.unity.com/en-us/unity-cli" "ERROR"
        return $false
    }

    winget install --id Unity.CLI --exact --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Unity CLI installation failed with exit code $LASTEXITCODE" "ERROR"
        return $false
    }

    Update-ProcessPath
    if (Get-Command unity -ErrorAction SilentlyContinue) {
        Write-Log "Unity CLI installed"
        return $true
    }

    Write-Log "Unity CLI installed, but its executable was not found after refreshing PATH." "ERROR"
    return $false
}

function Set-UnityCliTelemetry {
    $ConsentAction = if ($EnableUnityTelemetry) { "opt-in" } else { "opt-out" }
    Write-Log "Setting Unity CLI analytics consent to: $ConsentAction"

    & unity --non-interactive --no-banner analytics $ConsentAction
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Could not set Unity CLI analytics consent (exit code $LASTEXITCODE)" "ERROR"
        return $false
    }

    return $true
}

function Install-UnityEditor {
    param([string]$TargetVersion)

    Write-Log "Installing Unity Editor $TargetVersion. This download may take a while..."
    & unity --non-interactive --no-banner install $TargetVersion --yes --accept-eula
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Unity Editor installation failed with exit code $LASTEXITCODE" "ERROR"
        return $false
    }

    Write-Log "Unity Editor $TargetVersion installed"
    return $true
}

function Test-RequiredFolders {
    Write-Log "Validating required folders..."

    $AllExist = $true
    foreach ($Folder in $RequiredFolders) {
        $FullPath = Join-Path $ProjectRoot $Folder
        if (Test-Path $FullPath) {
            Write-Log "✓ $Folder exists"
        } else {
            Write-Log "✗ $Folder missing" "WARN"
            $AllExist = $false
        }
    }

    return $AllExist
}

function Create-EnvFile {
    Write-Log "Creating .env configuration file..."

    $EnvFile = Join-Path $ProjectRoot ".env"
    $EnvContent = @"
# Auto-generated environment configuration
# Created: $(Get-Date)

PROJECT_ROOT=$ProjectRoot
UNITY_VERSION=$(Get-Content (Join-Path $ProjectRoot ".unity-version") -Raw).Trim()
ASSETS_PATH=$ProjectRoot\Assets
SCRIPTS_PATH=$ProjectRoot\Assets\Scripts
LOG_PATH=$ProjectRoot\.tools\logs

# CI/CD Variables (uncomment for CI systems)
# CI_BUILD_ENABLED=true
# CI_TEST_ENABLED=true
"@

    Set-Content -Path $EnvFile -Value $EnvContent
    Write-Log "✓ Created .env at $EnvFile"
}

# ============================================================================
# Main Setup Execution
# ============================================================================

Write-Section "TinCan Unity - Project Setup"

Write-Log "Starting setup process..."
Write-Log "Project Root: $ProjectRoot"

# Step 1: Validate folder structure
Write-Section "Step 1: Validating Folder Structure"
if (-not (Test-RequiredFolders)) {
    Write-Log "Creating missing folders..." "INFO"
    $FoldersCreated = 0

    foreach ($Folder in $RequiredFolders) {
        $FullPath = Join-Path $ProjectRoot $Folder
        if (-not (Test-Path $FullPath)) {
            New-Item -ItemType Directory -Path $FullPath -Force | Out-Null
            $FoldersCreated++
        }
    }

    Write-Log "✓ Created $FoldersCreated folders"
}

# Step 2: Check Unity version
Write-Section "Step 2: Checking Unity Version"
if (-not (Install-UnityCli)) {
    Write-Log "Setup cannot continue without the Unity CLI." "ERROR"
    exit 1
}

if (-not (Set-UnityCliTelemetry)) {
    exit 1
}

$VersionFile = Join-Path $ProjectRoot ".unity-version"
$UnityVersion = Get-Content $VersionFile -Raw | ForEach-Object { $_.Trim() }
Write-Log "Target Unity version: $UnityVersion"

# Try to find Unity
$UnityEditorPath = Find-UnityEditor -TargetVersion $UnityVersion
if ($null -eq $UnityEditorPath) {
    if (-not (Install-UnityEditor -TargetVersion $UnityVersion)) {
        Write-Log "Setup could not install the required Unity Editor." "ERROR"
        exit 1
    }

    $UnityEditorPath = Find-UnityEditor -TargetVersion $UnityVersion
    if ($null -eq $UnityEditorPath) {
        Write-Log "Unity Editor installation completed, but version $UnityVersion still could not be found." "ERROR"
        exit 1
    }
}

# Step 3: Create configuration files
Write-Section "Step 3: Creating Configuration Files"
Create-EnvFile

# Step 4: Create placeholder manifests if needed
Write-Section "Step 4: Checking Package Configuration"
$ManifestPath = Join-Path $ProjectRoot "Packages\manifest.json"
if (-not (Test-Path $ManifestPath)) {
    Write-Log "Creating placeholder Packages/manifest.json..."

    $ManifestContent = @"
{
  "dependencies": {
    "com.unity.netcode.gameobjects": "1.8.1",
    "com.unity.transport": "1.4.0",
    "com.unity.burst": "1.8.4",
    "com.unity.collections": "1.4.0"
  }
}
"@

    Set-Content -Path $ManifestPath -Value $ManifestContent
    Write-Log "✓ Created manifest.json with core multiplayer packages"
}

# Step 5: Final validation
Write-Section "Step 5: Final Validation"
$AllGood = $true

$ValidationChecks = @(
    @{ Name = "Project Root"; Path = $ProjectRoot; IsDir = $true },
    @{ Name = "Assets folder"; Path = (Join-Path $ProjectRoot "Assets"); IsDir = $true },
    @{ Name = ".docs folder"; Path = (Join-Path $ProjectRoot ".docs"); IsDir = $true },
    @{ Name = ".tools folder"; Path = (Join-Path $ProjectRoot ".tools"); IsDir = $true },
    @{ Name = ".unity-version file"; Path = $VersionFile; IsDir = $false }
)

foreach ($Check in $ValidationChecks) {
    if (Test-Path $Check.Path) {
        Write-Log "✓ $($Check.Name)"
    } else {
        Write-Log "✗ $($Check.Name) NOT FOUND" "ERROR"
        $AllGood = $false
    }
}

# ============================================================================
# Summary
# ============================================================================

Write-Section "Setup Summary"

if ($AllGood) {
    Write-Host "`n✅ Setup completed successfully!`n" -ForegroundColor Green

    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Open the project in Unity Editor"
    Write-Host "   Path: $UnityEditorPath"
    Write-Host ""
    Write-Host "2. Review documentation:"
    Write-Host "   - PROJECT_MANIFEST.md (project overview)"
    Write-Host "   - .docs/README.md (documentation index)"
    Write-Host "   - .docs/CONTRIBUTING.md (contribution guidelines)"
    Write-Host ""
    Write-Host "3. Read SETUP_AND_UPGRADES.md for version management"
    Write-Host ""

    Write-Log "Setup completed successfully"
} else {
    Write-Host "`n❌ Setup had issues. Review log:" -ForegroundColor Red
    Write-Host $LogFile -ForegroundColor Yellow
    exit 1
}

Write-Host "Setup log saved to: $LogFile" -ForegroundColor Gray
