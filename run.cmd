@echo off
setlocal
cd /d "%~dp0"

if not exist "%~dp0DesktopFly.exe" (
    echo DesktopFly.exe not found! Building it first...
    call "%~dp0build.cmd"
    exit /b
)

echo Starting DesktopFly...
start "" "%~dp0DesktopFly.exe"
