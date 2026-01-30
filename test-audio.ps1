# Test script to quickly trigger eye rest popup for audio testing

Write-Host "Testing Eye-rest Audio System" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

# Update config to trigger eye rest in 1 minute (for quick testing)
$configPath = "$env:APPDATA\EyeRest\config.json"

if (Test-Path $configPath) {
    Write-Host "`nBacking up current configuration..." -ForegroundColor Yellow
    Copy-Item $configPath "$configPath.backup" -Force

    Write-Host "Loading configuration..." -ForegroundColor Yellow
    $config = Get-Content $configPath | ConvertFrom-Json

    # Set eye rest to 1 minute interval for quick testing
    $originalInterval = $config.EyeRest.intervalMinutes
    $config.EyeRest.intervalMinutes = 1
    $config.EyeRest.warningSeconds = 5
    $config.EyeRest.durationSeconds = 10

    # Ensure audio is enabled
    $config.Audio.enabled = $true
    $config.EyeRest.startSoundEnabled = $true
    $config.EyeRest.endSoundEnabled = $true

    Write-Host "Setting eye rest interval to 1 minute for testing..." -ForegroundColor Green
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath

    Write-Host "`nConfiguration updated:" -ForegroundColor Green
    Write-Host "  - Eye rest interval: 1 minute" -ForegroundColor White
    Write-Host "  - Warning: 5 seconds before" -ForegroundColor White
    Write-Host "  - Duration: 10 seconds" -ForegroundColor White
    Write-Host "  - Audio enabled: Yes" -ForegroundColor White
    Write-Host "  - Start sound: Enabled" -ForegroundColor White
    Write-Host "  - End sound: Enabled" -ForegroundColor White

    Write-Host "`nRestarting Eye-rest application..." -ForegroundColor Yellow
    Stop-Process -Name "EyeRest" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process "$PSScriptRoot\bin\Release\net8.0-windows10.0.19041.0\EyeRest.exe"

    Write-Host "`n🎵 AUDIO TEST READY!" -ForegroundColor Cyan
    Write-Host "====================`n" -ForegroundColor Cyan
    Write-Host "The eye rest popup will appear in:" -ForegroundColor Yellow
    Write-Host "  - Warning in: ~55 seconds" -ForegroundColor White
    Write-Host "  - Eye rest in: ~60 seconds" -ForegroundColor White
    Write-Host "`nListen for:" -ForegroundColor Yellow
    Write-Host "  1. Warning sound (5 seconds before)" -ForegroundColor White
    Write-Host "  2. Start sound (when popup appears)" -ForegroundColor White
    Write-Host "  3. End sound (after 10 seconds)" -ForegroundColor White

    Write-Host "`nPress Ctrl+C to cancel and restore original settings" -ForegroundColor Gray
    Write-Host "Waiting for eye rest..." -ForegroundColor Yellow

    # Wait for user to test
    Start-Sleep -Seconds 90

    Write-Host "`nRestoring original configuration..." -ForegroundColor Yellow
    $config.EyeRest.intervalMinutes = $originalInterval
    $config.EyeRest.warningSeconds = 15
    $config.EyeRest.durationSeconds = 20
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath

    Write-Host "Configuration restored to original values" -ForegroundColor Green
    Write-Host "Restarting application with normal settings..." -ForegroundColor Yellow
    Stop-Process -Name "EyeRest" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process "$PSScriptRoot\bin\Release\net8.0-windows10.0.19041.0\EyeRest.exe"

} else {
    Write-Host "Configuration file not found at: $configPath" -ForegroundColor Red
}

Write-Host "`nAudio test complete!" -ForegroundColor Green