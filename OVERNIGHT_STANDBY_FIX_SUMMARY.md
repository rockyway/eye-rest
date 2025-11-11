# Overnight Standby Detection Fix Summary

## Problem

After overnight PC standby (6-7 hours), when user wakes PC and logs in, break popup appears immediately instead of performing fresh session reset.

**User's Report**: "I notice the case that I just wake up the PC from long standby (overnight), then I login, the break popup show, I expect the refresh session."

## Root Cause Analysis

### Timeline from Logs (2025-10-02 08:48:31-08:48:32)

```
08:48:31 - Monitor turned on, user marked present
08:48:32 - SYSTEM RESUME RECOVERY: Attempting timer recovery
08:48:32 - Timer start times: EyeRest: 01:57:45, Break: 01:37:24
08:48:32 - Timer elapsed times: EyeRest: 24646.9s (410min), Break: 25867.2s (431min)
08:48:32 - Timer functionality test PASSED - timers working, no recovery needed
08:48:32 - [System continues with overdue timers]
08:48:32 - Break popup appears immediately (6+ hours overdue)
```

### The Logic Gap

**Previous Extended Standby Detection** (lines 771-804):
- Only checked `_pauseStartTime` or `_manualPauseStartTime` to detect overnight standby
- Assumed system would be in "paused" state before long standby

**The Missing Case**:
- User closes laptop mid-session (NOT in paused state)
- PC enters standby overnight (6-7 hours)
- Timers continue conceptually running but frozen
- When PC wakes: `wasPaused=FALSE`, `wasSmartPaused=FALSE`, `wasManuallyPaused=FALSE`
- Result: `timeSincePause = TimeSpan.Zero`
- Extended standby check doesn't trigger (0 minutes < 30 minutes threshold)
- Code proceeds to timer functionality test
- Test passes (DispatcherTimer infrastructure works fine after wake)
- Method returns early WITHOUT fresh session reset
- Timers resume from 6+ hours overdue state → immediate break popup

## Solution Implemented

### Code Changes in `TimerService.Recovery.cs`

**Lines 789-800: New Overnight Standby Detection**

```csharp
// CRITICAL FIX: Also check timer elapsed times for overnight standby detection
// If timers have been running for extended period (e.g., overnight), treat as fresh session
// This handles the case where user closes laptop mid-session without explicit pause
var maxTimerElapsed = Math.Max(eyeRestElapsed.TotalMinutes, breakElapsed.TotalMinutes);
var shouldResetDueToExtendedElapsed = maxTimerElapsed >= (extendedAwayThresholdMinutes * 2); // 2x threshold

if (shouldResetDueToExtendedElapsed)
{
    _logger.LogCritical($"🌙 OVERNIGHT STANDBY DETECTED: Timer elapsed {maxTimerElapsed:F1} minutes (threshold: {extendedAwayThresholdMinutes * 2} min)");
    _logger.LogCritical($"🌙 System was NOT paused before standby, but timer elapsed time indicates overnight gap");
    timeSincePause = TimeSpan.FromMinutes(maxTimerElapsed); // Use elapsed time as pause time for reset logic
}
```

### Detection Logic

**Two-Path Detection**:

1. **Pause-Based Detection** (existing, lines 776-787):
   - Checks `_pauseStartTime` or `_manualPauseStartTime`
   - Handles explicit pause before standby
   - Example: User manually pauses, then closes laptop

2. **Timer-Elapsed Detection** (new, lines 789-800):
   - Checks actual timer elapsed times from `_eyeRestStartTime` and `_breakStartTime`
   - Handles mid-session standby (no explicit pause)
   - Example: User closes laptop while timers running

**Threshold Logic**:
- Extended away threshold: 30 minutes (default from config)
- Timer elapsed threshold: 60 minutes (2x extended away threshold)
- Reason for 2x: Timer elapsed represents active session time, not pause time
- 60 minutes is reasonable threshold for "overnight or extended away"

### Expected Behavior After Fix

**Scenario: Overnight Standby (User's Case)**

```
01:37 - Break timer starts
01:57 - Eye rest timer starts
[User closes laptop - NOT paused]
[Overnight standby - 6-7 hours]
08:48 - PC wakes up, user logs in
08:48 - RecoverFromSystemResumeAsync() called
08:48 - Timer elapsed: 410min (eye rest), 431min (break)
08:48 - maxTimerElapsed = 431 minutes
08:48 - shouldResetDueToExtendedElapsed = true (431 > 60)
08:48 - OVERNIGHT STANDBY DETECTED log
08:48 - timeSincePause = 431 minutes
08:48 - Extended away check triggers (431 > 30)
08:48 - Fresh session reset performed
08:48 - New 20min/55min cycle starts
✅ NO immediate break popup
```

### Log Output After Fix

**New logs you'll see**:
```
🌙 OVERNIGHT STANDBY DETECTED: Timer elapsed 431.1 minutes (threshold: 60 min)
🌙 System was NOT paused before standby, but timer elapsed time indicates overnight gap
🌅 EXTENDED AWAY DETECTED: 431.1 minutes (threshold: 30 min)
🌅 Treating as NEW WORKING SESSION after overnight/extended standby
✅ NEW SESSION STARTED: Fresh timers after extended standby with complete manual pause cleanup
```

## Impact

### Before Fix
- ❌ Break popup appears immediately after overnight standby
- ❌ User forced to interact with overdue break popup
- ❌ Confusing UX - "I just started working!"

### After Fix
- ✅ Fresh session reset after overnight standby
- ✅ New 20min/55min cycle starts cleanly
- ✅ Expected behavior: "I just woke my PC, timers should reset"
- ✅ Handles both pause-before-standby AND mid-session-standby cases

## Testing Recommendations

### Test Case 1: Overnight Standby (Mid-Session)
1. Start application with timers running
2. Do NOT manually pause
3. Close laptop for 6+ hours (or set system clock forward)
4. Wake PC and log in
5. **Expected**: Fresh session reset, no immediate popups
6. **Verify logs**: "🌙 OVERNIGHT STANDBY DETECTED" message

### Test Case 2: Extended Away (With Pause)
1. Start application
2. Manually pause or let it smart-pause
3. Close laptop for 2+ hours
4. Wake PC
5. **Expected**: Fresh session reset (existing behavior maintained)
6. **Verify logs**: "🌅 EXTENDED AWAY DETECTED" message

### Test Case 3: Short Standby (< 60 minutes)
1. Start application with timers running
2. Close laptop for 30 minutes
3. Wake PC
4. **Expected**: Timers resume with time compensation (existing behavior maintained)
5. **Verify logs**: "Timer functionality test PASSED" or normal recovery

## Configuration

The detection uses `ExtendedAwayThresholdMinutes` from configuration:
- Default: 30 minutes
- Timer elapsed threshold: 2x this value (60 minutes)
- Can be adjusted in config if needed

## Technical Notes

### Why 2x Threshold for Timer Elapsed?

- Extended away threshold (30 min) represents explicit pause/away time
- Timer elapsed represents active session continuation during standby
- 60 minutes is more reasonable for "overnight standby" detection
- Prevents false positives for shorter standby periods (lunch break, meeting)
- Still catches genuine overnight cases (6+ hours >> 60 minutes)

### Thread Safety

- All timer operations use DispatcherTimer (UI thread)
- DateTime comparisons are atomic
- No race conditions in detection logic

### Graceful Degradation

- If config unavailable, uses default 30 min threshold
- If timer start times corrupted, falls back to uninitialized timer detection
- Multiple safety checks prevent false positives
