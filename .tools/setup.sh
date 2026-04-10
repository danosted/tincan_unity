#!/bin/bash

# ============================================================================
# TinCan Unity - Project Setup (macOS/Linux)
# ============================================================================

set -e  # Exit on error

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LOG_DIR="$SCRIPT_DIR/logs"
LOG_FILE="$LOG_DIR/setup-$(date +'%Y-%m-%d_%H-%M-%S').log"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Create log directory
mkdir -p "$LOG_DIR"

# ============================================================================
# Logging Functions
# ============================================================================

log() {
    local timestamp=$(date '+%H:%M:%S')
    local level=${2:-INFO}
    local message="[$timestamp] [$level] $1"
    
    echo -e "$message" | tee -a "$LOG_FILE"
}

log_section() {
    echo -e "\n${CYAN}========================================${NC}"
    echo -e "${CYAN}$1${NC}"
    echo -e "${CYAN}========================================${NC}" | tee -a "$LOG_FILE"
}

# ============================================================================
# Validation Functions
# ============================================================================

test_required_folders() {
    log "Validating required folders..."
    
    local required_folders=(
        "Assets/Scripts/Core"
        "Assets/Scripts/Network"
        "Assets/Scripts/Player"
        "Assets/Scripts/UI"
        "Assets/Scripts/Utils"
        "Assets/Prefabs"
        "Assets/Scenes"
        "Assets/Resources"
        ".docs"
        ".tools/logs"
    )
    
    local all_exist=true
    for folder in "${required_folders[@]}"; do
        full_path="$PROJECT_ROOT/$folder"
        if [ -d "$full_path" ]; then
            log "✓ $folder exists"
        else
            log "✗ $folder missing" "WARN"
            all_exist=false
        fi
    done
    
    return $([ "$all_exist" = true ] && echo 0 || echo 1)
}

create_env_file() {
    log "Creating .env configuration file..."
    
    local env_file="$PROJECT_ROOT/.env"
    local unity_version=$(cat "$PROJECT_ROOT/.unity-version" | tr -d '[:space:]')
    
    cat > "$env_file" << EOF
# Auto-generated environment configuration
# Created: $(date)

PROJECT_ROOT=$PROJECT_ROOT
UNITY_VERSION=$unity_version
ASSETS_PATH=$PROJECT_ROOT/Assets
SCRIPTS_PATH=$PROJECT_ROOT/Assets/Scripts
LOG_PATH=$PROJECT_ROOT/.tools/logs

# CI/CD Variables (uncomment for CI systems)
# CI_BUILD_ENABLED=true
# CI_TEST_ENABLED=true
EOF
    
    log "✓ Created .env at $env_file"
}

find_unity_editor() {
    local target_version=$1
    log "Searching for Unity Editor: $target_version"
    
    # macOS paths
    if [ "$(uname)" = "Darwin" ]; then
        local unity_paths=(
            "/Applications/Unity/Hub/Editor/$target_version/Unity.app/Contents/MacOS/Unity"
            "$HOME/Applications/Unity/Hub/Editor/$target_version/Unity.app/Contents/MacOS/Unity"
        )
    else
        # Linux paths
        local unity_paths=(
            "$HOME/.local/share/unity3d/hub/$target_version/Editor/Unity"
            "/opt/Unity/Editor/Unity"
        )
    fi
    
    for path in "${unity_paths[@]}"; do
        if [ -f "$path" ] || [ -d "$(dirname "$path")" ]; then
            log "Found Unity at: $path"
            echo "$path"
            return 0
        fi
    done
    
    log "Could not find Unity Editor for version $target_version" "WARN"
    return 1
}

# ============================================================================
# Main Setup Execution
# ============================================================================

log_section "TinCan Unity - Project Setup"

log "Starting setup process..."
log "Project Root: $PROJECT_ROOT"

# Step 1: Validate folder structure
log_section "Step 1: Validating Folder Structure"

if ! test_required_folders; then
    log "Creating missing folders..."
    
    local required_folders=(
        "Assets/Scripts/Core"
        "Assets/Scripts/Network"
        "Assets/Scripts/Player"
        "Assets/Scripts/UI"
        "Assets/Scripts/Utils"
        "Assets/Prefabs"
        "Assets/Scenes"
        "Assets/Resources"
        ".docs"
        ".tools/logs"
    )
    
    for folder in "${required_folders[@]}"; do
        mkdir -p "$PROJECT_ROOT/$folder"
    done
    
    log "✓ Created missing folders"
fi

# Step 2: Check Unity version
log_section "Step 2: Checking Unity Version"

version_file="$PROJECT_ROOT/.unity-version"
if [ ! -f "$version_file" ]; then
    log "ERROR: .unity-version file not found" "ERROR"
    exit 1
fi

unity_version=$(cat "$version_file" | tr -d '[:space:]')
log "Target Unity version: $unity_version"

# Try to find Unity
if ! find_unity_editor "$unity_version" > /dev/null; then
    echo -e "\n${YELLOW}⚠️  Unity Editor not found!${NC}"
    echo -e "${YELLOW}Install from: https://unity.com/download${NC}"
    echo -e "${YELLOW}Then run setup again.${NC}"
    log "Setup partially complete (Unity not found)" "WARN"
    exit 1
fi

# Step 3: Create configuration files
log_section "Step 3: Creating Configuration Files"
create_env_file

# Step 4: Create placeholder manifests if needed
log_section "Step 4: Checking Package Configuration"

manifest_path="$PROJECT_ROOT/Packages/manifest.json"
if [ ! -f "$manifest_path" ]; then
    log "Creating placeholder Packages/manifest.json..."
    
    mkdir -p "$PROJECT_ROOT/Packages"
    cat > "$manifest_path" << 'EOF'
{
  "dependencies": {
    "com.unity.netcode.gameobjects": "1.8.1",
    "com.unity.transport": "1.4.0",
    "com.unity.burst": "1.8.4",
    "com.unity.collections": "1.4.0"
  }
}
EOF
    
    log "✓ Created manifest.json with core multiplayer packages"
fi

# Step 5: Final validation
log_section "Step 5: Final Validation"

local all_good=true

validation_checks=(
    "$PROJECT_ROOT:Project Root:d"
    "$PROJECT_ROOT/Assets:Assets folder:d"
    "$PROJECT_ROOT/.docs:.docs folder:d"
    "$PROJECT_ROOT/.tools:.tools folder:d"
    "$version_file:.unity-version file:f"
)

for check in "${validation_checks[@]}"; do
    IFS=':' read -r path name type <<< "$check"
    
    if [ "$type" = "d" ] && [ -d "$path" ]; then
        log "✓ $name"
    elif [ "$type" = "f" ] && [ -f "$path" ]; then
        log "✓ $name"
    else
        log "✗ $name NOT FOUND" "ERROR"
        all_good=false
    fi
done

# ============================================================================
# Summary
# ============================================================================

log_section "Setup Summary"

if [ "$all_good" = true ]; then
    echo -e "\n${GREEN}✅ Setup completed successfully!${NC}\n"
    
    echo -e "${YELLOW}Next steps:${NC}"
    echo "1. Open the project in Unity Editor"
    echo ""
    echo "2. Review documentation:"
    echo "   - PROJECT_MANIFEST.md (project overview)"
    echo "   - .docs/README.md (documentation index)"
    echo "   - .docs/CONTRIBUTING.md (contribution guidelines)"
    echo ""
    echo "3. Read SETUP_AND_UPGRADES.md for version management"
    echo ""
    
    log "Setup completed successfully"
else
    echo -e "\n${RED}❌ Setup had issues. Review log:${NC}"
    echo -e "${YELLOW}$LOG_FILE${NC}"
    exit 1
fi

echo -e "${CYAN}Setup log saved to: $LOG_FILE${NC}"
