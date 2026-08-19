@echo off
setlocal
cd /d "%~dp0"

echo ===================================================
echo   Building DesktopFly for Windows 10 x64...
echo ===================================================

dotnet publish src\DesktopFly\DesktopFly.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed with exit code %ERRORLEVEL%!
    echo.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ===================================================
echo   DesktopFly successfully built!
echo   Executable: %~dp0DesktopFly.exe
echo ===================================================
echo.
echo Press any key to launch DesktopFly now (or close this window)...
pause >nul

start "" "%~dp0DesktopFly.exe"
