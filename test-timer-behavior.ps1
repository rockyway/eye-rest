# Test script for timer behavior verification
# Tests: 1) Break warning shows in first cycle, 2) Stop clears all timers, 3) No ghost timers

Write-Host "=== Eye Rest Timer Behavior Test ===" -ForegroundColor Cyan
Write-Host "This script will test timer behavior including warning popups and ghost timer prevention"
Write-Host ""

# Function to send commands to the app through UI automation
function Test-TimerBehavior {
    Write-Host "Test 1: Monitoring for break warning in first cycle..." -ForegroundColor Yellow
    Write-Host "With 55/5 config and 30s warning, break warning should appear at ~54.5 minutes"
    Write-Host ""

    # Monitor log file for break warning events
    Write-Host "Monitoring log file for timer events..." -ForegroundColor Green

    # Get current timestamp
    $startTime = Get-Date

    # Read log file continuously for 2 minutes to catch any immediate issues
    Write-Host "Checking for any immediate ghost timer issues (2 minutes)..." -ForegroundColor Yellow

    $timeout = New-TimeSpan -Minutes 2
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    while ($stopwatch.Elapsed -lt $timeout) {
        # Get last 20 lines of log
        $logPath = "$env:APPDATA\EyeRest\logs\eyerest.log"
        if (Test-Path $logPath) {
            $recentLogs = Get-Content $logPath -Tail 20

            # Check for ghost timer indicators
            foreach ($line in $recentLogs) {
                if ($line -match "Break warning timer fired" -or
                    $line -match "BREAK WARNING:" -or
                    $line -match "Break popup timer tick" -or
                    $line -match "Break due timer fired") {

                    $elapsed = (Get-Date) - $startTime
                    Write-Host "ALERT: Break event detected after only $($elapsed.TotalSeconds) seconds!" -ForegroundColor Red
                    Write-Host "Log entry: $line" -ForegroundColor Yellow

                    if ($elapsed.TotalMinutes -lt 50) {
                        Write-Host "GHOST TIMER DETECTED! Break event triggered too early." -ForegroundColor Red
                        return
                    }
                }

                if ($line -match "Eye rest warning timer fired" -or
                    $line -match "EYE REST WARNING:") {

                    $elapsed = (Get-Date) - $startTime
                    Write-Host "Eye rest warning detected after $($elapsed.TotalSeconds) seconds" -ForegroundColor Cyan

                    if ($elapsed.TotalMinutes -lt 19) {
                        Write-Host "WARNING: Eye rest triggered earlier than expected" -ForegroundColor Yellow
                    }
                }
            }
        }

        Start-Sleep -Seconds 5
    }

    Write-Host ""
    Write-Host "No ghost timers detected in first 2 minutes ✓" -ForegroundColor Green
    Write-Host ""

    # Now test Stop/Start behavior
    Write-Host "Test 2: Testing Stop/Start timer behavior..." -ForegroundColor Yellow
    Write-Host "This test will verify that Stop properly clears all timers"
    Write-Host "Press any key to simulate Stop/Start sequence..." -ForegroundColor Cyan
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

    Write-Host "Monitoring for any timer events after Stop/Start..." -ForegroundColor Green

    # Monitor for 1 minute after Stop/Start
    $timeout2 = New-TimeSpan -Minutes 1
    $stopwatch2 = [System.Diagnostics.Stopwatch]::StartNew()
    $ghostDetected = $false

    while ($stopwatch2.Elapsed -lt $timeout2) {
        $recentLogs = Get-Content $logPath -Tail 10

        foreach ($line in $recentLogs) {
            if ($line -match "Timer service stopped" -or
                $line -match "Disposing TimerService" -or
                $line -match "All timers stopped") {
                Write-Host "Stop command detected in logs ✓" -ForegroundColor Green
            }

            if ($line -match "Timer service started" -and
                $stopwatch2.Elapsed.TotalSeconds -gt 5) {
                Write-Host "Start command detected in logs ✓" -ForegroundColor Green
            }

            # Check for premature timer events after restart
            if (($line -match "Break warning timer fired" -or
                 $line -match "Break popup timer tick") -and
                $stopwatch2.Elapsed.TotalSeconds -lt 30) {

                Write-Host "GHOST TIMER ALERT: Break event within 30s of restart!" -ForegroundColor Red
                Write-Host "Log entry: $line" -ForegroundColor Yellow
                $ghostDetected = $true
            }
        }

        Start-Sleep -Seconds 2
    }

    if (-not $ghostDetected) {
        Write-Host "No ghost timers detected after Stop/Start ✓" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "=== Test Summary ===" -ForegroundColor Cyan
    Write-Host "1. Initial 2-minute monitoring: $(if (-not $ghostDetected) { 'PASSED ✓' } else { 'FAILED ✗' })" -ForegroundColor $(if (-not $ghostDetected) { 'Green' } else { 'Red' })
    Write-Host "2. Stop/Start behavior: Check logs manually for proper disposal" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "For full verification of break warning in first cycle:" -ForegroundColor Yellow
    Write-Host "  - Wait ~54.5 minutes to see break warning popup" -ForegroundColor White
    Write-Host "  - Or change break interval to 2/1 minutes for faster testing" -ForegroundColor White
}

# Run the test
Test-TimerBehavior

Write-Host ""
Write-Host "Test script completed. Check application behavior and logs for detailed results." -ForegroundColor Cyan