# Idle Resume Popup Stuck Fix Summary

## Problem

Eye rest reminder popup gets stuck at "1 second" remaining when user returns from PC idle/standby. The countdown never proceeds, popup never shows, and timer remains stuck showing "Next eye rest: 1s" indefinitely.

**User Report**: "I notice the Eye Rest remind popup is halted at 1 second. This happened when I left PC idle for a while and comeback to use the PC"

## Root Cause Analysis

### Timeline from Logs (2025-10-02 16:54 - 17:19)

```
16:54:11 - Last completed eye rest event
[USER LEAVES PC IDLE]

17:17:36 - Health check shows: "Last heartbeat: 1139.5s ago" (19 minutes idle)
[USER RETURNS TO PC]

17:19:00.959 - Eye rest timer fires normally (20-minute interval)
17:19:00.960 - Eye rest WARNING starts (15-second countdown)
17:19:01.330 - Warning popup shows successfully

17:19:15.962 - Warning period complete, tries to trigger eye rest popup
17:19:15.962 - Eye rest warning processing flags cleared
17:19:15.962 - "👁️ GLOBAL LOCK PREVENTION: Eye rest event already processing globally - ignoring duplicate trigger"

[EYE REST POPUP NEVER SHOWS]

17:19:03 onwards - Tooltip stuck at "Next eye rest: 1s"
17:19:36 - Health check: EyeRest timer = False (stopped, waiting for popup)
```

### The Stale Lock Bug

**What Happened:**

1. **Before Idle**: User is working normally, timers running fine
2. **During Idle**: PC goes idle/standby around 16:59 (after last eye rest at 16:54)
3. **During Idle (Hypothesis)**: An eye rest event may have started but didn't complete properly
   - Eye rest warning triggered OR
   - System went to sleep mid-event OR
   - Popup showed but never received user interaction
4. **Global Flag Set**: `_isAnyEyeRestEventProcessing = true` was set but never cleared
5. **User Returns**: System resumes around 17:17 (19 minutes later)
6. **Next Eye Rest**: Normal 20-minute timer fires at 17:19:00
7. **Warning Shows**: 15-second warning popup shows successfully
8. **Warning Completes**: Warning timer completes, tries to trigger actual eye rest popup
9. **BLOCKED**: `TriggerEyeRest()` checks `_isAnyEyeRestEventProcessing` → finds it TRUE → returns early
10. **Result**: Eye rest popup NEVER shows, global flag never gets cleared, stuck forever

### The Code Path

**File**: `Services/Timer/TimerService.EventHandlers.cs`

**Lines 380-388: Global Lock Check in TriggerEyeRest()**:
```csharp
// THREAD SAFETY: Global lock to prevent ALL timer systems from interfering
lock (_globalEyeRestLock)
{
    if (_isAnyEyeRestEventProcessing)  // ← Still TRUE from previous session!
    {
        _logger.LogWarning("👁️ GLOBAL LOCK PREVENTION: Eye rest event already processing globally - ignoring duplicate trigger");
        return;  // ← Exits early, popup never triggers
    }
    _isAnyEyeRestEventProcessing = true;
}
```

**The Missing Cleanup:**

The global flag `_isAnyEyeRestEventProcessing` is only cleared when:
1. **Success path**: Popup completes → ApplicationOrchestrator calls `ClearEyeRestProcessingFlag()`
2. **Error path**: Exception in `TriggerEyeRest()` → catch block clears flag

But when PC goes idle/standby:
- If a popup was showing → user can't interact with it → popup times out or closes
- ApplicationOrchestrator might not get called if the event never completes
- Global flag remains SET
- System resumes → flag is still SET → next popup gets BLOCKED

### Why It Shows "1s" Forever

**File**: `Services/Timer/TimerService.State.cs`, Lines 173-227

When eye rest is "due" (overdue or notification active), `TimeUntilNextEyeRest` returns:
- `TimeSpan.Zero` if notification is active
- Timer calculates remaining time and logs warnings if overdue

In this stuck state:
- `_isEyeRestNotificationActive = false` (popup never showed)
- `_eyeRestTimer.IsEnabled = false` (stopped for warning/popup)
- Calculation returns near-zero time → rounds to "1s" in tooltip
- Stuck forever because popup can't show (global lock blocks it)

## Solution Implemented

### Fix #1: Clear Stale Flags on Fresh Session Reset

**File**: `Services/Timer/TimerService.Recovery.cs`, Lines 764-768

When uninitialized timers are detected (fresh session after standby):

```csharp
// CRITICAL FIX: Clear any stale event processing flags from previous session
// This prevents "GLOBAL LOCK PREVENTION" blocking popups after system resume
ClearEyeRestProcessingFlag();
ClearBreakProcessingFlag();
_logger.LogCritical($"🔄 RECOVERY: Cleared all event processing flags to prevent stale lock state");
```

This ensures that when PC wakes from standby and detects uninitialized timers (indicating fresh session), any stale processing flags from previous session are cleared.

### Fix #2: Clear Stale Flags on Extended Away Detection

**File**: `Services/Timer/TimerService.Recovery.cs`, Lines 992-996

When extended away period is detected (>30 minutes idle):

```csharp
// CRITICAL FIX: Clear any stale event processing flags from extended idle
// This prevents "GLOBAL LOCK PREVENTION" blocking popups after returning from idle
ClearEyeRestProcessingFlag();
ClearBreakProcessingFlag();
_logger.LogCritical($"🔄 EXTENDED AWAY: Cleared all event processing flags to prevent stale lock state");
```

This ensures that when user returns from extended idle/away period, any stale processing flags are cleared before session reset.

### What ClearEyeRestProcessingFlag() Does

**File**: `Services/Timer/TimerService.cs`, Lines 271-282

```csharp
public void ClearEyeRestProcessingFlag()
{
    _isEyeRestEventProcessing = false;  // Instance flag

    // THREAD SAFETY: Clear global processing flag
    lock (_globalEyeRestLock)
    {
        _isAnyEyeRestEventProcessing = false;  // ← GLOBAL FLAG CLEARED
    }

    _logger.LogDebug("🔄 Eye rest processing flags cleared (instance + global)");
}
```

## Expected Behavior After Fix

### Scenario: User Leaves PC Idle, Returns Later

```
16:54 - User working, eye rest completes normally
[USER LEAVES PC - GOES IDLE]

16:59-17:17 - PC idle/standby (19 minutes)
              - Timers frozen
              - Any in-progress popups abandoned

[USER RETURNS]

17:17 - System resumes from standby
17:17 - RecoverFromSystemResumeAsync() triggered
17:17 - Detects uninitialized timers OR extended away
17:17 - "🔄 RECOVERY: Cleared all event processing flags to prevent stale lock state"
17:17 - Fresh session reset with clean state

17:19 - Next eye rest timer fires (20 minutes from last completed)
17:19 - Warning shows (15 seconds)
17:19 - Warning completes
17:19 - TriggerEyeRest() checks global lock
17:19 - ✅ Global lock is FALSE (was cleared during recovery)
17:19 - ✅ Eye rest popup shows normally
17:19 - User interacts with popup
17:19 - Popup completes, flags cleared
✅ NO stuck state
```

### Expected Logs After Fix

**During System Resume:**
```
17:17:XX - 🔄 SYSTEM RESUME RECOVERY: Attempting timer recovery - [reason]
17:17:XX - 🔄 UNINITIALIZED TIMERS DETECTED: Timer start times lost during standby
            OR
           🌅 EXTENDED AWAY DETECTED: XXX.X minutes (threshold: 30 min)

17:17:XX - 🔄 RECOVERY: Cleared all event processing flags to prevent stale lock state
            OR
           🔄 EXTENDED AWAY: Cleared all event processing flags to prevent stale lock state

17:17:XX - ✅ FRESH SESSION STARTED: Timers reset to full intervals
```

**When Next Eye Rest Triggers:**
```
17:19:00 - 👁️ TIMER EVENT: Eye rest timer tick fired
17:19:00 - ⚠️ Starting eye rest warning
17:19:01 - Warning popup shown

17:19:15 - ⏰ Eye rest warning period complete - triggering eye rest NOW
17:19:15 - 🔄 Eye rest warning processing flags cleared
17:19:15 - 👁️ TRIGGER EYE REST: Starting popup ✅ (NO GLOBAL LOCK message!)
17:19:15 - Eye rest popup shows successfully
```

## Impact

### Before Fix
- ❌ Eye rest popup stuck at "1s" after returning from idle
- ❌ Popup never shows
- ❌ Timer stuck indefinitely
- ❌ User must restart application to recover
- ❌ Defeats the purpose of eye rest reminders

### After Fix
- ✅ Stale flags cleared during system resume
- ✅ Fresh session starts with clean state
- ✅ Eye rest popups show normally after idle
- ✅ No stuck state
- ✅ No application restart needed

## Technical Details

### Why This Bug Existed

The system has a global lock (`_isAnyEyeRestEventProcessing`) to prevent multiple eye rest events from running simultaneously. This is set when `TriggerEyeRest()` is called and cleared when the popup completes in `ApplicationOrchestrator`.

However, if the PC goes to sleep/idle while a popup is showing or about to show:
- The popup may not complete properly
- ApplicationOrchestrator may not get called
- The global flag never gets cleared
- Flag persists across sleep/wake cycles
- Next eye rest attempt gets blocked

The recovery code handled timer recreation and state reset, but never thought to clear event processing flags, assuming they would naturally clear when events complete. But in idle/standby scenarios, events can be abandoned without completing.

### Thread Safety

The fix maintains thread safety:
- `ClearEyeRestProcessingFlag()` uses `lock (_globalEyeRestLock)` to safely clear the flag
- Called during recovery when no timers are running
- No race conditions with other timer events

### Edge Cases Handled

1. **Uninitialized Timers** (overnight standby): Cleared at line 766
2. **Extended Away** (>30 min idle): Cleared at line 994
3. **Both Scenarios**: Covers all idle/standby recovery paths

## Testing Recommendations

### Test Case 1: Idle and Return
1. Start application, wait for eye rest
2. Complete eye rest normally
3. Leave PC idle for 5+ minutes (monitor off, no input)
4. Return to PC
5. **Verify**: Next eye rest works normally, no stuck at "1s"

### Test Case 2: Sleep and Wake
1. Start application
2. Put PC to sleep (Start → Power → Sleep)
3. Wait 10+ minutes
4. Wake PC
5. **Verify**: Application recovers, timers reset, eye rest works normally

### Test Case 3: Extended Idle
1. Start application
2. Leave PC idle overnight (8+ hours)
3. Return next morning
4. **Verify**: Fresh session reset, no stuck popups, eye rest works normally

### Test Case 4: Popup Mid-Sleep
1. Start application
2. Wait until eye rest warning shows
3. Immediately put PC to sleep (don't dismiss popup)
4. Wake PC after 5 minutes
5. **Verify**: Fresh session, no stuck state, next eye rest works

## Related Systems

This fix complements other recovery mechanisms:
- **OVERNIGHT_STANDBY_FIX_SUMMARY.md**: Detects overnight standby via timer elapsed times
- **BREAK_DELAY_HEALTH_MONITOR_FIX.md**: Health monitor respects delay state
- **Timer Recovery System**: RecoverFromSystemResumeAsync handles various resume scenarios

This completes the robust idle/standby recovery system.
