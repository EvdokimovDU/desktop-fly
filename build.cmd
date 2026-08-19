@echo off
echo Building DesktopFly for Windows 10 x64...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b %ERRORLEVEL%
)
echo Build succeeded!
echo Output: src\DesktopFly\bin\Release\net10.0-windows\DesktopFly.exe
