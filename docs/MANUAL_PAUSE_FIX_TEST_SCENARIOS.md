# Manual Pause State Persistence Fix - Test Scenarios

## Overview

This document provides comprehensive test scenarios to verify the fix for the critical manual pause state persistence bug that prevented timers from automatically resuming when users return from extended absence.

## Critical Requirements

**CORE GUARANTEE**: Timers must **ALWAYS** be counting when user returns from any extended absence scenario.

**UI DISPLAY REQUIREMENT**: System tray should never show "Paused (Manual)" after user returns from extended absence unless user actively chose manual pause.

## Test Scenarios

### Scenario 1: System Sleep/Wake After Manual Pause

**Setup**:
1. Start application with normal timers running
2. Manually pause timers via system tray "Pause Timers" option
3. Verify system tray shows "Paused (Manual)" 
4. Put system to sleep/hibernate for >30 minutes

**Test Actions**:
1. Wake system from sleep
2. Log back into Windows session
3. Wait 10 seconds for recovery mechanisms to complete

**Expected Results**:
✅ System tray status shows "Running" (NOT "Paused (Manual)")  
✅ Timer tooltips show active countdown (e.g., "Next eye rest: 18m 30s")  
✅ `IsRunning = true`, `IsManuallyPaused = false` in logs  
✅ Recovery logs show "Manual pause cleared" and "Timer service restarted"

### Scenario 2: Session Lock/Unlock After Manual Pause

**Setup**:
1. Start application with normal timers running
2. Manually pause timers for 60 minutes via "Pause for Meeting"
3. Verify system tray shows manual pause countdown
4. Lock Windows session (Win+L) 
5. Leave locked for >30 minutes

**Test Actions**:
1. Unlock Windows session (Ctrl+Alt+Del + password)
2. Wait 10 seconds for recovery mechanisms

**Expected Results**:
✅ System tray status shows "Running"  
✅ No manual pause countdown visible  
✅ Timers actively counting down  
✅ UserPresenceService logs show session unlock → recovery triggered  
✅ Recovery logs confirm manual pause state cleared

### Scenario 3: Monitor Power Off/On After Extended Manual Pause

**Setup**:
1. Manually pause timers via system tray
2. Turn off monitor (power button or display power settings)
3. Leave monitor off for >30 minutes

**Test Actions**:
1. Turn monitor back on
2. Move mouse to wake system
3. Wait 10 seconds for recovery

**Expected Results**:
✅ System tray status shows "Running"  
✅ Manual pause state completely cleared  
✅ Monitor power on logs trigger recovery  
✅ Property change notifications sent to UI

### Scenario 4: Extended Away Period After Manual Pause

**Setup**:
1. Start timers normally
2. Pause timers manually for "Meeting"
3. Leave computer unattended (no input) for >45 minutes
4. System remains on but user presence = Away

**Test Actions**:
1. Return and provide mouse/keyboard input
2. Wait for user presence detection (should be <15 seconds)

**Expected Results**:
✅ User presence changes from Away → Present  
✅ ApplicationOrchestrator detects manual pause + user return  
✅ Manual pause cleared automatically via `ResumeAsync()`  
✅ Timers restart with fresh intervals  
✅ System tray shows "Running"

### Scenario 5: Health Monitor Coordination Failure Detection

**Setup**:
1. Force a coordination failure state:
   - Set `IsManuallyPaused = false` 
   - Set `IsRunning = false`
   - Ensure heartbeat is >1 minute old

**Test Actions**:
1. Wait for health monitor tick (every 1 minute)
2. Observe health monitor logs

**Expected Results**:
✅ Health monitor detects coordination failure  
✅ Logs show "Manual pause cleared but timer service stopped"  
✅ Automatic restart triggered via `StartAsync()`  
✅ Property change notifications sent  
✅ System tray updates to "Running"

### Scenario 6: Console Disconnect/Reconnect Recovery

**Setup**:
1. Manually pause timers
2. Switch to different console session or use Remote Desktop disconnect
3. Leave disconnected >30 minutes

**Test Actions**:
1. Reconnect to console session
2. Wait for recovery mechanisms

**Expected Results**:
✅ Console connect triggers recovery  
✅ Manual pause state cleared  
✅ Timer service restarted  
✅ UI synchronized with running state

## Verification Commands

### Check Timer State
```csharp
// Verify in logs or debugger:
TimerService.IsRunning         // Must be true
TimerService.IsManuallyPaused  // Must be false  
TimerService.IsPaused          // Must be false
TimerService.IsSmartPaused     // Must be false
```

### Check Health Monitor
```bash
# Search logs for coordination checks:
grep "COORDINATION" *.log
grep "Manual pause cleared but timer service stopped" *.log
```

### Check Recovery Triggers
```bash
# Verify recovery mechanisms fired:
grep "SYSTEM RESUME RECOVERY" *.log
grep "RecoverFromSystemResumeAsync" *.log  
grep "Recovery completed" *.log
```

## Expected Log Patterns

### Successful Recovery Logs
```
🔧 MANUAL PAUSE FIX: Manual pause timer disposed during recovery
🔧 RECOVERY COORDINATION FIX: Timer service stopped with cleared pause states - restarting
🔧 COORDINATION SUCCESS: Timer service restarted after manual pause clearing: IsRunning=True
🔧 COORDINATION SUCCESS: Property change notifications sent to update UI state
```

### Health Monitor Detection
```
🚨 COORDINATION FAILURE: Manual pause cleared (True) but service stopped (False)
🔧 COORDINATION RECOVERY: Attempting direct timer service restart for manual pause issue
🔧 COORDINATION FIX: UI state synchronized after manual pause coordination repair
```

### User Presence Integration
```
👤 USER PRESENT FIX: Manual pause active when user returned - clearing manual pause state  
👤 MANUAL PAUSE CLEARED: Timer service resumed, IsRunning=True, ManuallyPaused=False
```

## Integration Test Execution

### Automated Test Framework
```csharp
[Test]
public async Task SystemWakeAfterManualPause_ShouldAlwaysResumeTimers()
{
    // Setup: Start app, manually pause, simulate system sleep >30min
    await timerService.PauseForDurationAsync(TimeSpan.FromHours(1), "Test pause");
    await SimulateSystemSleep(TimeSpan.FromMinutes(35));
    
    // Action: Trigger system wake recovery
    await userPresenceService.TriggerSystemWakeRecovery();
    
    // Verify: Timers ALWAYS running after recovery
    Assert.That(timerService.IsRunning, Is.True);
    Assert.That(timerService.IsManuallyPaused, Is.False);
    Assert.That(systemTrayService.CurrentStatus, Is.Not.EqualTo("Paused (Manual)"));
}
```

## Success Criteria

**✅ PASS**: All scenarios result in timers counting when user returns  
**✅ PASS**: System tray never shows "Paused (Manual)" after extended absence recovery  
**✅ PASS**: Health monitor catches and fixes any coordination failures  
**✅ PASS**: UI state properly synchronized via property change notifications

**❌ FAIL**: Any scenario where timers remain paused after user returns  
**❌ FAIL**: "Paused (Manual)" persists in system tray after recovery  
**❌ FAIL**: Coordination failures not detected or fixed by health monitor

## Manual Testing Checklist

- [ ] Test Scenario 1: System Sleep/Wake
- [ ] Test Scenario 2: Session Lock/Unlock  
- [ ] Test Scenario 3: Monitor Power Off/On
- [ ] Test Scenario 4: Extended Away Period
- [ ] Test Scenario 5: Health Monitor Detection
- [ ] Test Scenario 6: Console Disconnect/Reconnect
- [ ] Verify all expected log patterns appear
- [ ] Confirm no "Paused (Manual)" persistence in any scenario
- [ ] Validate UI updates match internal timer states

**CRITICAL SUCCESS METRIC**: 100% of test scenarios must result in timers counting when user returns.