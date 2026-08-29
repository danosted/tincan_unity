@echo off
setlocal enabledelayedexpansion

where winget >nul 2>nul
if errorlevel 1 (
    echo ERROR: winget is required. Install App Installer from the Microsoft Store, then run this command again.
    exit /b 1
)

set "PWSH=pwsh.exe"
where pwsh >nul 2>nul
if errorlevel 1 (
    echo PowerShell 7 not found. Installing it with winget...
    winget install --id Microsoft.PowerShell --exact --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity
    if errorlevel 1 (
        echo ERROR: PowerShell 7 installation failed.
        exit /b 1
    )

    set "PWSH=%ProgramFiles%\PowerShell\7\pwsh.exe"
    if not exist "!PWSH!" set "PWSH=%LocalAppData%\Microsoft\WindowsApps\pwsh.exe"
    if not exist "!PWSH!" (
        echo ERROR: PowerShell 7 was installed but pwsh.exe could not be found.
        exit /b 1
    )
)

"%PWSH%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1" %*
exit /b %ERRORLEVEL%
