# Player builds (DevTools/Editor/PlayerBuild.cs): in the open Editor through its menu, or in a batch-mode Editor when
# none has the project open. Both write <output>/build-result.txt, which is polled here.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 1.0
Import-Module (Join-Path $PSScriptRoot "TinCan.Common.psm1")

$ProjectRoot = Get-ProjectRoot

function Read-Result([string]$resultFile) {
    if (-not (Test-Path $resultFile)) { return $null }
    return (Get-Content $resultFile -Raw).Trim()
}

# Returns $true when the build succeeded; prints the PASS/FAIL line either way.
function Invoke-PlayerBuild([string]$name, [string]$menu, [string]$method, [string]$outputDir, [int]$TimeoutMinutes = 30) {
    $resultFile = Join-Path $outputDir "build-result.txt"
    if (Test-Path $resultFile) { Remove-Item $resultFile }

    $status = & unity cmd editor_status --result-only --timeout 15 2>$null | Out-String
    if ($status -match '"status"\s*:\s*"ready"') {
        Write-Host "[Build ] $name in the open Editor ($menu)"
        & unity cmd menu --path $menu --result-only --timeout 60 | Out-Null
    }
    else {
        $version = (Get-Content (Join-Path $ProjectRoot ".unity-version") -Raw).Trim()
        $editorDir = (& unity editors path $version).Trim()
        $unityExe = Join-Path $editorDir "Unity.exe"
        if (-not (Test-Path $unityExe)) { $unityExe = Join-Path $editorDir "Editor/Unity.exe" }
        $log = Join-Path $ProjectRoot ".tools/logs/build-$($name -replace ' ', '-').log"
        Write-Host "[Build ] $name in a batch-mode Editor ($version), log $log"
        $process = Start-Process -FilePath $unityExe -PassThru -NoNewWindow -ArgumentList @(
            "-batchmode", "-quit", "-projectPath", "`"$ProjectRoot`"",
            "-executeMethod", "TinCan.DevTools.Editor.PlayerBuild.$method",
            "-logFile", "`"$log`"")
        $process.WaitForExit()
    }

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        $result = Read-Result $resultFile
        if ($result -and $result -ne "building") { break }
        Start-Sleep -Seconds 5
    }

    $result = Read-Result $resultFile
    if ($result -notlike "succeeded*") {
        Write-Host "[Build ] FAIL $name $result" -ForegroundColor Red
        return $false
    }
    Write-Host "[Build ] PASS $name $result" -ForegroundColor Green
    return $true
}

Export-ModuleMember -Function Invoke-PlayerBuild
