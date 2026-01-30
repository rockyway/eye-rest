# Quick audio test script - triggers eye rest in 30 seconds

Write-Host "🎵 Quick Audio Test for Eye-rest" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Backup and update config for immediate testing
$configPath = "$env:APPDATA\EyeRest\config.json"

if (Test-Path $configPath) {
    Write-Host "`nBacking up current configuration..." -ForegroundColor Yellow
    Copy-Item $configPath "$configPath.backup" -Force

    Write-Host "Loading and updating configuration for quick test..." -ForegroundColor Yellow
    $config = Get-Content $configPath | ConvertFrom-Json

    # Store original values
    $originalInterval = $config.EyeRest.intervalMinutes
    $originalWarning = $config.EyeRest.warningSeconds
    $originalDuration = $config.EyeRest.durationSeconds

    # Set to very short intervals for immediate testing
    $config.EyeRest.intervalMinutes = 1
    $config.EyeRest.warningSeconds = 5
    $config.EyeRest.durationSeconds = 10

    # Ensure all audio is enabled
    $config.Audio.enabled = $true
    $config.EyeRest.startSoundEnabled = $true
    $config.EyeRest.endSoundEnabled = $true

    Write-Host "Saving test configuration..." -ForegroundColor Green
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath

    Write-Host "`n🔄 Restarting Eye-rest application..." -ForegroundColor Yellow
    Stop-Process -Name "EyeRest" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    Start-Process "$PSScriptRoot\bin\Release\net8.0-windows10.0.19041.0\EyeRest.exe"

    Write-Host "`n🎵 READY FOR AUDIO TEST!" -ForegroundColor Cyan
    Write-Host "========================`n" -ForegroundColor Cyan
    Write-Host "Eye rest popup will appear in approximately:" -ForegroundColor Yellow
    Write-Host "  - Warning sound: ~55 seconds" -ForegroundColor White
    Write-Host "  - Start sound: ~60 seconds" -ForegroundColor White
    Write-Host "  - End sound: after 10 seconds" -ForegroundColor White

    Write-Host "`nListening for sounds:" -ForegroundColor Yellow
    Write-Host "  🔊 Warning sound (gentle question tone)" -ForegroundColor White
    Write-Host "  🔊 Start sound (gentle question tone)" -ForegroundColor White
    Write-Host "  🔊 End sound (soft completion tone)" -ForegroundColor White

    Write-Host "`nWaiting for audio test..." -ForegroundColor Yellow

    # Wait for test to complete
    Start-Sleep -Seconds 90

    Write-Host "`n✅ Test complete! Restoring original configuration..." -ForegroundColor Green

    # Restore original values
    $config.EyeRest.intervalMinutes = $originalInterval
    $config.EyeRest.warningSeconds = $originalWarning
    $config.EyeRest.durationSeconds = $originalDuration
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath

    Write-Host "Restarting application with restored settings..." -ForegroundColor Yellow
    Stop-Process -Name "EyeRest" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process "$PSScriptRoot\bin\Release\net8.0-windows10.0.19041.0\EyeRest.exe"

    Write-Host "`n✅ Audio test completed and settings restored!" -ForegroundColor Green
} else {
    Write-Host "❌ Configuration file not found at: $configPath" -ForegroundColor Red
}