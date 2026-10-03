# One-time Windows host setup, as guided steps: each checks what is missing, explains why it matters, asks, and only then
# changes the machine (an administrator prompt included). Unattended (Test-Interactive is false) it changes nothing and
# returns $false, so the caller can stop with the command to run instead of hanging on a Windows prompt.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 1.0
Import-Module (Join-Path $PSScriptRoot "TinCan.Common.psm1")

$ProjectRoot = Get-ProjectRoot

# The game builds that open sockets on Windows. Firewall rules are per exe path, so each build folder needs its own.
function Get-GameExecutables {
    return @("Builds/Win64/TinCan.exe", "Builds/Win64Perf/TinCan.exe") | ForEach-Object { Join-Path $ProjectRoot $_ }
}

# Executables with no enabled inbound Allow rule for UDP (or any protocol). The rule may exist before the exe does.
function Get-MissingFirewallRules {
    $allowed = @{}
    foreach ($filter in Get-NetFirewallApplicationFilter | Where-Object { $_.Program -like "*TinCan.exe" }) {
        $rule = $filter | Get-NetFirewallRule
        if ($rule.Enabled -ne "True" -or $rule.Direction -ne "Inbound" -or $rule.Action -ne "Allow") { continue }
        $protocol = ($rule | Get-NetFirewallPortFilter).Protocol
        if ($protocol -in @("UDP", "Any")) { $allowed[[IO.Path]::GetFullPath($filter.Program).ToLowerInvariant()] = $true }
    }
    return @(Get-GameExecutables | Where-Object { -not $allowed.ContainsKey([IO.Path]::GetFullPath($_).ToLowerInvariant()) })
}

# Creates the rules in one elevated PowerShell (one administrator prompt). Returns $true when they exist afterwards.
function Install-FirewallRules([string[]]$Programs) {
    $commands = foreach ($program in $Programs) {
        $name = "TinCan ($(Split-Path (Split-Path $program -Parent) -Leaf))"
        "New-NetFirewallRule -DisplayName '$name' -Direction Inbound -Action Allow -Protocol UDP -Program '$program' -Profile Private | Out-Null"
    }
    try {
        $process = Start-Process pwsh -Verb RunAs -Wait -PassThru -WindowStyle Hidden `
            -ArgumentList @("-NoProfile", "-Command", ($commands -join "; "))
        if ($process.ExitCode -ne 0) { Write-Host "  The elevated PowerShell exited with $($process.ExitCode)." -ForegroundColor Red }
    }
    catch {
        Write-Host "  The administrator prompt was declined or failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
    return @(Get-MissingFirewallRules).Count -eq 0
}

# The guided step. $true when the rules are in place (already, or created now); $false when the person said no or
# nobody could be asked.
function Confirm-FirewallRules {
    $missing = @(Get-MissingFirewallRules)
    if ($missing.Count -eq 0) { return $true }

    Write-Host ""
    Write-Host "One-time setup: Windows Firewall rules for the game builds" -ForegroundColor Cyan
    $explanation = @(
        "The first time a game build opens a network socket, Windows shows an 'Allow access' prompt and waits.",
        "Unattended (overnight perf runs, agents) nobody answers it, and runs stall. Allowing the builds now avoids that.",
        "Rules to create (inbound UDP, Private networks only):"
    ) + ($missing | ForEach-Object { "  $_" }) + @(
        "Windows will ask for administrator approval once."
    )
    if (-not (Confirm-Step "Create these rules now?" $explanation)) {
        Write-Host "  Skipped. Run it later from an interactive PowerShell: .\.tools\setup.ps1 -Only Firewall" -ForegroundColor Yellow
        return $false
    }

    if (Install-FirewallRules $missing) {
        Write-Host "  Firewall rules created." -ForegroundColor Green
        return $true
    }
    Write-Host "  The rules are still missing. Run .\.tools\setup.ps1 -Only Firewall again, or create them in Windows Defender Firewall." -ForegroundColor Red
    return $false
}

Export-ModuleMember -Function Get-GameExecutables, Get-MissingFirewallRules, Install-FirewallRules, Confirm-FirewallRules
