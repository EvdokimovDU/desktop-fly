Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "  Building DesktopFly for Windows 10 x64...        " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

dotnet publish src\DesktopFly\DesktopFly.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "===================================================" -ForegroundColor Green
    Write-Host "  DesktopFly successfully built!" -ForegroundColor Green
    Write-Host "  Executable: $scriptDir\DesktopFly.exe" -ForegroundColor Yellow
    Write-Host "===================================================" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[ERROR] Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
