# Extended Idle Race Condition Analysis

**Date:** 2025-10-22
**Issue:** Break popup shows immediately when user returns from long idle period instead of starting fresh session

## Timeline of Events

### User Idle Period (~18:28 - 19:26:19)
- User went idle at approximately **18:28** (58.3 minutes before resume at 19:26:19)
- During idle period:
  - Timers continued counting down (health checks show stale heartbeats: 713s, 773s, 833s)
  - Break timer counting down: 13m 32s → 3m 11s → 1m 41s
  - Eye rest timer also counting down
  - No explicit pause was triggered during idle

### User Return (19:26:19)
```
[19:26:19.548] Monitor turned on - user marked as present
[19:26:19.550] USER PRESENCE: Changed from Idle → Present
[19:26:19.565] Session resumed - ID: 205, Was Idle for 58.3min
[19:26:20.570] SYSTEM RESUME RECOVERY: Attempting timer recovery - Monitor power on
[19:26:29.684] SYSTEM RESUME RECOVERY: Attempting timer recovery - Session unlocked (2nd attempt)
[19:26:29.699] SYSTEM RESUME RECOVERY: Attempting timer recovery - Session unlocked (3rd attempt)
```

### The Problem Sequence
```
[19:28:01] PauseReminderService: Pause reminder #1 triggered - paused for 1.0 hours ✓ CORRECT
[19:28:09] HEALTH CHECK: Last heartbeat: 773.0s ago (12.9 minutes) ⚠️ STALE
[19:29:09] HEALTH CHECK: Last heartbeat: 833.0s ago (13.9 minutes) ⚠️ STALE
[19:29:41] Break timer tick fired ❌ BUG - Should have been reset!
[19:29:41] Break warning shown to user ❌ UNEXPECTED
```

## Root Causes

### 1. Extended Away Detection Failure

**Location:** `TimerService.Recovery.cs:786-814`

The extended away detection has **TWO CHECKS**:

#### Check #1: Explicit Pause Time (FAILED)
```csharp
// Lines 792-801
if (wasManuallyPaused && _manualPauseStartTime != DateTime.MinValue)
{
    timeSincePause = DateTime.Now - _manualPauseStartTime;
}
else if ((wasPaused || wasSmartPaused) && _pauseStartTime != DateTime.MinValue)
{
    timeSincePause = DateTime.Now - _pauseStartTime;
}
```

**Problem:** Timer service was RUNNING (not paused) during idle period
- `wasManuallyPaused` = false
- `wasPaused` = false
- `wasSmartPaused` = false
- **Result:** `timeSincePause` remains at `TimeSpan.Zero`

#### Check #2: Timer Elapsed Time Fallback (FAILED)
```csharp
// Lines 806-814
var maxTimerElapsed = Math.Max(eyeRestElapsed.TotalMinutes, breakElapsed.TotalMinutes);
var shouldResetDueToExtendedElapsed = maxTimerElapsed >= (extendedAwayThresholdMinutes * 2);
```

**Problem:** Threshold set to 2x the configured value (60 minutes)
- Break timer elapsed: ~54 minutes
- Threshold required: 60 minutes
- **Result:** Check fails because 54 < 60

### 2. No Integration with UserPresenceService

The `RecoverFromSystemResumeAsync` method doesn't query `UserPresenceService` for actual idle time. It only checks:
1. Internal pause states (manual/smart pause)
2. Timer elapsed times with very high threshold

**UserPresenceService knows:**
- User was idle for **58.3 minutes**
- This exceeds the 30-minute extended away threshold
- But this information is never used by timer recovery!

### 3. Multiple System Resume Recovery Calls

Recovery was triggered **3 times** within 10 seconds:
- 19:26:20.570 - Monitor power on
- 19:26:29.684 - Session unlocked
- 19:26:29.699 - Session unlocked (duplicate)

All 3 recovery attempts failed to detect extended away, allowing timers to continue.

### 4. Stale Heartbeat Not Used for Extended Away Detection

Health checks showed heartbeat was stale for **773 seconds** (12.9 minutes) at 19:28:09.
This indicates the system was asleep/frozen, but the extended away detection doesn't use heartbeat staleness as a trigger.

## Race Conditions Identified

### Race #1: UserPresence vs TimerRecovery
```
UserPresenceService (19:26:19): "User was idle 58.3min - resume session"
                                      ↓
                        No coordination with TimerService
                                      ↓
TimerService (19:26:20):  "Check pause states... none found, check elapsed... below threshold, NO RESET"
                                      ↓
                           Break timer continues counting
                                      ↓
Break Timer (19:29:41):  "Timer reached 0, fire event!" ❌ BUG
```

### Race #2: PauseReminder vs BreakTimer
```
PauseReminderService (19:28:01): Shows "paused 1.0 hour" notification
                                      ↓ (concurrent)
Break Timer (19:29:41):  Shows break warning popup
                                      ↓
                          User sees BOTH notifications!
```

### Race #3: Multiple Recovery Attempts
```
Recovery #1 (19:26:20): Checks extended away → fails → timers continue
                                      ↓ (9 seconds later)
Recovery #2 (19:26:29): Checks extended away → fails → timers continue
                                      ↓ (0.015 seconds later)
Recovery #3 (19:26:29): Checks extended away → fails → timers continue
                                      ↓
              All 3 attempts fail to reset timers!
```

## Expected Behavior

When user returns from **58.3 minutes** of idle time:

1. ✓ UserPresenceService detects return from idle
2. ✓ PauseReminderService shows notification (after 1 hour threshold)
3. ❌ **TimerService should reset to fresh session** (extended away > 30min)
   - Reset eye rest timer to full 20 minutes
   - Reset break timer to full 55 minutes
   - Clear any stale timer states
4. ❌ **Break popup should NOT show** - user just returned, needs fresh start

## Actual Behavior (Bug)

1. ✓ UserPresenceService detects return
2. ✓ PauseReminderService shows notification
3. ❌ TimerService does NOT reset (detection failed)
4. ❌ Break timer continues from where it left off (1m 41s remaining)
5. ❌ Break popup shows at 19:29:41 (~3 minutes after user returns)

## Why Current Code Fails

### Extended Away Detection Logic Flow

```
Is user manually paused? NO
    ↓
Is user smart/regular paused? NO
    ↓
timeSincePause = TimeSpan.Zero
    ↓
Is timer elapsed >= 60min? NO (only 54min)
    ↓
Extended away detection = FALSE ❌
    ↓
Proceed with normal recovery (no reset)
    ↓
Break timer continues and fires at 19:29:41
```

### The Missing Link

**UserPresenceService has the answer:**
- `Session resumed - Was Idle for 58.3min`
- 58.3min > 30min extended away threshold
- **But TimerService never asks!**

## Recommended Fixes

### Fix #1: Query UserPresenceService for Idle Time (CRITICAL)

```csharp
// In RecoverFromSystemResumeAsync, after line 786
var actualIdleTime = await _userPresenceService.GetIdleTimeAsync(); // NEW
var userWasIdleForExtendedPeriod = actualIdleTime.TotalMinutes >= extendedAwayThresholdMinutes;

if (userWasIdleForExtendedPeriod && config.UserPresence.EnableSmartSessionReset)
{
    _logger.LogCritical($"🌅 EXTENDED IDLE DETECTED: User was idle for {actualIdleTime.TotalMinutes:F1}min");
    // Proceed with fresh session reset...
}
```

### Fix #2: Lower Timer Elapsed Threshold

Change from 2x threshold (60min) to 1x threshold (30min):

```csharp
// Line 807 - change from:
var shouldResetDueToExtendedElapsed = maxTimerElapsed >= (extendedAwayThresholdMinutes * 2);

// To:
var shouldResetDueToExtendedElapsed = maxTimerElapsed >= extendedAwayThresholdMinutes;
```

### Fix #3: Use Stale Heartbeat as Extended Away Indicator

```csharp
// After line 788
var heartbeatStaleness = DateTime.Now - _lastHeartbeat;
var heartbeatIndicatesExtendedAway = heartbeatStaleness.TotalMinutes >= extendedAwayThresholdMinutes;

if (heartbeatIndicatesExtendedAway && config.UserPresence.EnableSmartSessionReset)
{
    _logger.LogCritical($"🌅 STALE HEARTBEAT DETECTED: {heartbeatStaleness.TotalMinutes:F1}min");
    // Proceed with fresh session reset...
}
```

### Fix #4: Prevent Duplicate Recovery Calls

Add debouncing to prevent multiple recovery attempts within short time window:

```csharp
private DateTime _lastRecoveryAttempt = DateTime.MinValue;
private const int RECOVERY_DEBOUNCE_SECONDS = 5;

// At start of RecoverFromSystemResumeAsync
var timeSinceLastRecovery = DateTime.Now - _lastRecoveryAttempt;
if (timeSinceLastRecovery.TotalSeconds < RECOVERY_DEBOUNCE_SECONDS)
{
    _logger.LogInformation($"🔄 Skipping duplicate recovery - last attempt {timeSinceLastRecovery.TotalSeconds:F1}s ago");
    return;
}
_lastRecoveryAttempt = DateTime.Now;
```

### Fix #5: Coordinate with PauseReminderService

When PauseReminderService detects extended idle, it should trigger timer reset:

```csharp
// In PauseReminderService, after detecting extended idle
if (idleTime.TotalMinutes >= extendedAwayThreshold)
{
    await _timerService.ResetToFreshSessionAsync("Extended idle detected by PauseReminderService");
}
```

## Testing Scenarios

### Scenario 1: Overnight Laptop Close
1. User closes laptop at 6pm (timer at 15min remaining)
2. User opens laptop at 9am next day
3. **Expected:** Fresh session starts (20min eye rest, 55min break)
4. **Actual (bug):** Break popup shows immediately

### Scenario 2: Lunch Break (1 hour)
1. User locks PC and leaves for 1 hour lunch
2. User returns and unlocks PC
3. **Expected:** Fresh session starts
4. **Actual (bug):** Timer continues from where it left off

### Scenario 3: Short Break (15 minutes)
1. User steps away for 15 minutes
2. User returns
3. **Expected:** Timer continues (no reset needed)
4. **Actual:** Correct behavior (below 30min threshold)

## Configuration Impact

**Current Setting:**
```json
{
  "UserPresence": {
    "ExtendedAwayThresholdMinutes": 30,
    "EnableSmartSessionReset": true
  }
}
```

The configuration exists and is enabled, but the detection logic has the bugs described above.

## Summary

**Primary Bug:** Extended away detection fails when:
1. Timer service is running (not explicitly paused) during idle
2. Timer elapsed time is below 2x threshold (60min) but above 1x threshold (30min)
3. No integration with UserPresenceService idle time data

**Impact:** Users returning from lunch/overnight see immediate break popups instead of fresh session reset.

**Priority:** HIGH - Affects core user experience and timer health/rest rhythm

**Recommended Solution:** Implement Fix #1 (query UserPresenceService) + Fix #2 (lower threshold) as minimum viable fix.
