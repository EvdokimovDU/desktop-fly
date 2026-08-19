Write-Host "Building DesktopFly for Windows 10 x64..." -ForegroundColor Cyan
dotnet build -c Release
if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded!" -ForegroundColor Green
    Write-Host "Executable: src\DesktopFly\bin\Release\net10.0-windows\DesktopFly.exe" -ForegroundColor Yellow
} else {
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
