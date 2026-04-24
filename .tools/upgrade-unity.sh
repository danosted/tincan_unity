#!/bin/bash

# ============================================================================
# TinCan Unity - Upgrade Version (macOS/Linux)
# ============================================================================

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LOG_DIR="$SCRIPT_DIR/logs"
LOG_FILE="$LOG_DIR/upgrade-$(date +'%Y-%m-%d_%H-%M-%S').log"
VERSION_FILE="$PROJECT_ROOT/.unity-version"

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

# Create log directory
mkdir -p "$LOG_DIR"

# ============================================================================
# Functions
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
# Main
# ============================================================================

if [ -z "$1" ]; then
    echo -e "${RED}ERROR: Target version required${NC}"
    echo "Usage: ./upgrade-unity.sh <version>"
    echo "Example: ./upgrade-unity.sh 6000.4.2f1"
    exit 1
fi

TARGET_VERSION="$1"

log_section "TinCan Unity - Version Upgrade"

CURRENT_VERSION=$(cat "$VERSION_FILE" | tr -d '[:space:]')
log "Current version: $CURRENT_VERSION"
log "Target version: $TARGET_VERSION"

# Validate version format
if ! [[ "$TARGET_VERSION" =~ ^[0-9]{4}\.[0-9]{1,2}\.[0-9]{1,2}(f|b|a|rc)[0-9]+$ ]]; then
    log "ERROR: Invalid version format. Expected format: 6000.4.2f1" "ERROR"
    exit 1
fi

# Create backup
log_section "Creating Backup"
BACKUP_DIR="$PROJECT_ROOT/.backup_$(date +'%Y-%m-%d_%H-%M-%S')"
log "Creating backup at: $BACKUP_DIR"

mkdir -p "$BACKUP_DIR"
cp -r "$PROJECT_ROOT/Assets" "$BACKUP_DIR/Assets" 2>/dev/null || true
cp -r "$PROJECT_ROOT/ProjectSettings" "$BACKUP_DIR/ProjectSettings" 2>/dev/null || true
cp -r "$PROJECT_ROOT/Packages" "$BACKUP_DIR/Packages" 2>/dev/null || true

log "✓ Backup created"

# Update .unity-version
log_section "Updating Version Configuration"
log "Updating .unity-version to: $TARGET_VERSION"
echo "$TARGET_VERSION" > "$VERSION_FILE"
log "✓ .unity-version updated"

# Update ProjectSettings/ProjectVersion.txt if it exists
PROJECT_VERSION_FILE="$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt"
if [ -f "$PROJECT_VERSION_FILE" ]; then
    log "Updating ProjectSettings/ProjectVersion.txt"
    echo "m_EditorVersion: $TARGET_VERSION" > "$PROJECT_VERSION_FILE"
    log "✓ ProjectSettings/ProjectVersion.txt updated"
fi

# Summary
log_section "Upgrade Summary"
echo -e "\n${GREEN}✅ Version upgraded to: $TARGET_VERSION${NC}\n"

echo -e "${YELLOW}Next steps:${NC}"
echo "1. Open the project in Unity $TARGET_VERSION"
echo "2. Wait for Unity to import and upgrade assets"
echo "3. Check the Console for any upgrade errors"
echo "4. Validate all scripts compile correctly"
echo "5. Run tests if available"
echo ""

log "Version upgrade from $CURRENT_VERSION to $TARGET_VERSION completed"
echo -e "${CYAN}Upgrade log saved to: $LOG_FILE${NC}"
