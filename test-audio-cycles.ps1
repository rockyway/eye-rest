# 🔍 ULTRATHINK AUDIO CYCLE DIAGNOSTICS
# This script triggers multiple eye rest cycles to diagnose the start sound issue

Write-Host "🔍 ULTRATHINK AUDIO CYCLE DIAGNOSTICS" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Backup and update config for rapid testing
$configPath = "$env:APPDATA\EyeRest\config.json"

if (Test-Path $configPath) {
    Write-Host "`nBacking up current configuration..." -ForegroundColor Yellow
    Copy-Item $configPath "$configPath.backup" -Force

    Write-Host "Loading configuration..." -ForegroundColor Yellow
    $config = Get-Content $configPath | ConvertFrom-Json

    # Store original values
    $originalInterval = $config.EyeRest.intervalMinutes
    $originalWarning = $config.EyeRest.warningSeconds
    $originalDuration = $config.EyeRest.durationSeconds

    # Set to very short intervals for rapid testing (3 cycles)
    $config.EyeRest.intervalMinutes = 1      # 1 minute between cycles
    $config.EyeRest.warningSeconds = 3       # 3 second warning
    $config.EyeRest.durationSeconds = 5      # 5 second duration

    # Ensure all audio is enabled
    $config.Audio.enabled = $true
    $config.EyeRest.startSoundEnabled = $true
    $config.EyeRest.endSoundEnabled = $true

    Write-Host "Saving test configuration..." -ForegroundColor Green
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath

    Write-Host "`n🔄 Restarting Eye-rest application..." -ForegroundColor Yellow
    Stop-Process -Name "EyeRest" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    Start-Process "$PSScriptRoot\bin\Debug\net8.0-windows10.0.19041.0\EyeRest.exe"

    Write-Host "`n🔍 RAPID CYCLE TEST - 3 CYCLES" -ForegroundColor Cyan
    Write-Host "==============================" -ForegroundColor Cyan
    Write-Host "CYCLE 1: ~57 seconds (warning at ~54s)" -ForegroundColor White
    Write-Host "CYCLE 2: ~57 seconds (warning at ~54s)" -ForegroundColor White
    Write-Host "CYCLE 3: ~57 seconds (warning at ~54s)" -ForegroundColor White
    Write-Host "`nWATCH FOR:" -ForegroundColor Yellow
    Write-Host "  - Start sound: Should play at popup appearance" -ForegroundColor White
    Write-Host "  - End sound: Should play at popup closure" -ForegroundColor White
    Write-Host "  - Check if start sound stops working after cycle 1" -ForegroundColor Red

    Write-Host "`nRunning 3 rapid cycles for diagnostics..." -ForegroundColor Yellow
    Write-Host "Press any key after 3 cycles complete to restore config..." -ForegroundColor Yellow

    # Wait for user to observe the cycles
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

    Write-Host "`n✅ Test complete! Restoring original configuration..." -ForegroundColor Green

    # Restore original values
    $config.EyeRest.intervalMinutes = $originalInterval
    $config.EyeRest.warningSeconds = $originalWarning
    $config.EyeRest.durationSeconds = $originalDuration
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath

    Write-Host "Restarting application with restored settings..." -ForegroundColor Yellow
    Stop-Process -Name "EyeRest" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Start-Process "$PSScriptRoot\bin\Debug\net8.0-windows10.0.19041.0\EyeRest.exe"

    Write-Host "`n✅ Diagnostics completed!" -ForegroundColor Green
    Write-Host "Check the log file at: $env:APPDATA\EyeRest\logs\eyerest.log" -ForegroundColor Cyan
    Write-Host "Look for cycle-specific diagnostic messages with 🔍 markers" -ForegroundColor Cyan

} else {
    Write-Host "❌ Configuration file not found at: $configPath" -ForegroundColor Red
}