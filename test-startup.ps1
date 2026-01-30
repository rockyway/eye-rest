param(
    [int]$WaitSeconds = 10
)

Write-Host "Testing Eye Rest Application Startup..." -ForegroundColor Cyan
Write-Host "Watching for popup issues during first $WaitSeconds seconds" -ForegroundColor Yellow

# Start the application
$appPath = ".\bin\Debug\net8.0-windows\EyeRest.exe"
if (-not (Test-Path $appPath)) {
    Write-Host "Building application first..." -ForegroundColor Yellow
    dotnet build
}

Write-Host "`nStarting Eye Rest application..." -ForegroundColor Green
$process = Start-Process -FilePath $appPath -PassThru

# Monitor for popup windows
$startTime = Get-Date
$popupDetected = $false

Write-Host "Monitoring for $WaitSeconds seconds..." -ForegroundColor Yellow

while ((Get-Date) -lt $startTime.AddSeconds($WaitSeconds)) {
    # Check for popup windows (eye rest or break windows)
    $windows = Get-Process | Where-Object {
        $_.MainWindowTitle -like "*Eye Rest*" -or 
        $_.MainWindowTitle -like "*Break*" -or
        $_.MainWindowTitle -like "*Warning*"
    }
    
    if ($windows.Count -gt 0) {
        $popupDetected = $true
        Write-Host "`n[ERROR] Popup detected during startup!" -ForegroundColor Red
        foreach ($window in $windows) {
            Write-Host "  - Window: $($window.MainWindowTitle)" -ForegroundColor Red
        }
        break
    }
    
    Start-Sleep -Milliseconds 500
    Write-Host "." -NoNewline
}

Write-Host "`n"

if ($popupDetected) {
    Write-Host "TEST FAILED: Popups appeared during startup (within first $WaitSeconds seconds)" -ForegroundColor Red
    Write-Host "This indicates the race condition fix was not successful." -ForegroundColor Red
} else {
    Write-Host "TEST PASSED: No popups detected during startup!" -ForegroundColor Green
    Write-Host "The application started cleanly without showing any popups." -ForegroundColor Green
}

# Clean up
Write-Host "`nStopping application..." -ForegroundColor Yellow
Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue

Write-Host "Test complete." -ForegroundColor Cyan