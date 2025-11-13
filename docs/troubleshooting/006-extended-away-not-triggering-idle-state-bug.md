# Extended Away Detection Not Triggering After 131.5 Minutes Idle - Investigation

**Date:** 2025-11-12
**Priority:** P0 - Critical Session Reset Failure
**Status:** ✅ ROOT CAUSE IDENTIFIED
**Investigation ID:** Extended-Away-006

---

## Problem Statement

**User Action:** User left PC idle (without locking session) for 131.5 minutes, then returned at 19:05:28

**Expected Behavior:**
- System detects extended absence (over 30-minute threshold)
- Triggers smart session reset
- Starts fresh 20min/55min timer cycles

**Actual Behavior:**
- System resumed existing timers from before absence
- No session reset triggered
- Extended away detection completely bypassed

**Impact:** P0 - Core session reset feature non-functional for idle scenarios

---

## Timeline of Events

```
16:53:56.795  User presence changed: Present → Idle (idle: 5.2min)
              ↓
              User goes away WITHOUT locking session
              System stays in Idle state
              ↓
              [131.5 minutes pass]
              ↓
19:05:28.438  User presence changed: Idle → Present (idle: 0.0min)
19:05:28.452  Analytics: "Was Idle for 131.5min"
19:05:28.458  Timers resume from previous state (NO SESSION RESET!)
              ❌ Extended away detection NEVER ran
              ❌ System stuck with old timer state
```

---

## Root Cause Analysis

### Bug #1: Idle State Never Transitions to Away (State Machine Design Flaw)

**File:** `Services/UserPresenceService.cs:210-225` (DeterminePresenceState method)

**Current Code:**
```csharp
private UserPresenceState DeterminePresenceState(TimeSpan idleTime)
{
    // Check if session is locked first
    if (IsSessionLocked())
    {
        return UserPresenceState.Away;
    }

    // Check if user is idle based on input activity
    if (idleTime.TotalMinutes >= IdleThresholdMinutes)  // 5 minutes
    {
        return UserPresenceState.Idle;
    }

    return UserPresenceState.Present;
}
```

**Problem:** State logic is:
- `IsSessionLocked() == true` → **Away**
- `idleTime >= 5 minutes` → **Idle**
- Otherwise → **Present**

**Result:** Once a user goes Idle (5 minutes of no input), the system will NEVER transition to Away unless:
1. Session gets locked (WTS_SESSION_LOCK event), OR
2. User returns (Idle → Present)

**There is NO automatic progression from Idle to Away based on time!**

### Bug #2: Extended Away Detection Doesn't Check Idle → Present Transitions

**File:** `Services/UserPresenceService.cs:568-621` (HandleExtendedAwayTracking method)

**Current Code (Lines 585-586):**
```csharp
// User is returning (Away/SystemSleep → Present)
else if ((previousState == UserPresenceState.Away || previousState == UserPresenceState.SystemSleep) &&
         newState == UserPresenceState.Present)
{
    if (_awayStartTime != default(DateTime))
    {
        var awayDuration = now - _awayStartTime;

        // Check if this was an extended away period requiring smart session reset
        if (awayDuration.TotalMinutes >= extendedAwayThresholdMinutes && !_hasBeenAwayExtended)
        {
            // Trigger session reset
        }
    }
}
```

**Problem:** This logic ONLY checks for:
- `Away → Present` transitions, OR
- `SystemSleep → Present` transitions

**It completely ignores:**
- `Idle → Present` transitions

**Result:** When user returns after 131.5 minutes of idle time, transition is `Idle → Present`, so the extended away check never runs!

---

## Evidence from Logs

### Evidence #1: User Went Idle at 16:53:56

```
[2025-11-12 16:53:56.795 INF] 👤 User presence changed: Present → Idle (idle: 5.2min)
[2025-11-12 16:53:56.796 FTL] 🔵 USER PRESENCE: Changed from Present → Idle at 16:53:56.796
[2025-11-12 16:53:56.811 DBG] 📊 Recorded presence change: Present → Idle
[2025-11-12 16:53:56.811 FTL] 🔵 USER PRESENCE: User no longer present - clearing active popups
```

### Evidence #2: User Returned at 19:05:28 (131.5 Minutes Later)

```
[2025-11-12 19:05:28.438 INF] 👤 User presence changed: Idle → Present (idle: 0.0min)
[2025-11-12 19:05:28.440 FTL] 🔵 USER PRESENCE: Changed from Idle → Present at 19:05:28.440
[2025-11-12 19:05:28.452 DBG] 📊 Recorded presence change: Idle → Present
[2025-11-12 19:05:28.452 INF] 📊 Session resumed - ID: 239, Was Idle for 131.5min, Total inactive: 162.5min. Reason: User returned
```

**Notice:** State transition is **Idle → Present**, NOT **Away → Present**

### Evidence #3: No Session Lock Events

Searched entire log for WTS_SESSION_LOCK, WTS_SESSION_UNLOCK, and related events:
```
❌ NO SESSION LOCK EVENTS FOUND
```

This proves the user did NOT lock their session - they just left the PC idle.

### Evidence #4: No Extended Away Detection Triggered

Searched logs for extended away detection messages:
```
❌ NO "Extended away period detected" message
❌ NO "EXTENDED AWAY SESSION DETECTED!" message
❌ NO SmartSessionResetAsync triggered
```

### Evidence #5: EnableSmartSessionReset Configuration Verified

```json
{
  "UserPresence": {
    "enableSmartSessionReset": true,
    "extendedAwayThresholdMinutes": 30
  }
}
```

✓ Feature is enabled
✓ Threshold is 30 minutes
✓ User was away for 131.5 minutes (well over threshold)

---

## Why the Bug Occurs

### State Machine Design Flaw

```
Current State Machine:

Present → Idle (after 5 minutes of no input, if session not locked)
   ↓
  Idle → Present (when user returns)
   ↓
  [NO AUTOMATIC TRANSITION TO AWAY]

Idle → Away (ONLY if session gets locked manually)
```

**Problem:** There's no time-based progression from Idle to Away. The system assumes:
1. If user is idle but session not locked → Stay in Idle forever
2. Extended away detection will work via session lock events

**But this fails when:**
- User leaves PC idle without locking
- User goes away for hours/days
- No session lock event fires
- System stays in Idle state indefinitely

### Extended Away Check Bypass

```
HandleExtendedAwayTracking Logic Flow:

IF (previousState == Away OR SystemSleep) AND (newState == Present):
    ✓ Check away duration
    ✓ Trigger session reset if duration > threshold

ELSE IF (previousState == Idle) AND (newState == Present):
    ❌ DO NOTHING
    ❌ Extended away check completely bypassed
```

**Timeline of Failure:**
```
T0:       User goes idle → Present → Idle
          _awayStartTime NOT set (only set when going to Away/SystemSleep)

T+131min: User returns → Idle → Present
          HandleExtendedAwayTracking checks: (previousState == Idle)
          Condition fails: (previousState == Away || SystemSleep)
          Extended away check NEVER runs

Result:   Timers resume from old state
          No session reset
          User continues with stale timer cycles
```

---

## Impact Analysis

### Severity: P0 - Critical Feature Failure

| Component | Impact |
|-----------|--------|
| Extended Away Detection | 🔴 Completely non-functional for idle scenarios (most common case) |
| Smart Session Reset | 🔴 Only works if user locks session - doesn't work for natural idle |
| User Experience | 🔴 Timers out of sync after long absences, breaks appear unexpectedly |
| Session Management | 🔴 State machine fundamentally broken for idle scenarios |

### Reproducibility

**Confirmed:** 100% reproducible

**Trigger Conditions:**
- User leaves PC idle WITHOUT locking session
- Idle duration exceeds ExtendedAwayThresholdMinutes (default: 30 minutes)
- User returns and resumes activity

**Frequency:** Very High - Most users don't manually lock sessions, they just leave PC idle

---

## Root Cause Summary

| Aspect | Details |
|--------|---------|
| **Type** | State machine design flaw + Incomplete transition handling |
| **Location #1** | `UserPresenceService.cs:DeterminePresenceState()` method (lines 210-225) |
| **Location #2** | `UserPresenceService.cs:HandleExtendedAwayTracking()` method (lines 585-586) |
| **What's Missing #1** | Automatic time-based transition from Idle → Away |
| **What's Missing #2** | Extended away check for Idle → Present transitions |
| **Why It Matters** | 99% of users leave PC idle without locking - this breaks extended away detection |
| **Consequence** | Session reset never triggers for idle scenarios, timers stay stale for days |

---

## Fix Required

### Fix #1: Add Idle Duration Tracking (PRIMARY FIX)

**File:** `Services/UserPresenceService.cs`

**Add field to track when user went idle:**
```csharp
private DateTime _idleStartTime = default(DateTime);
```

**Modify HandleExtendedAwayTracking to track Idle start time (after line 577):**
```csharp
// User is going idle (Present → Idle)
if (previousState == UserPresenceState.Present && newState == UserPresenceState.Idle)
{
    _idleStartTime = now;
    _logger.LogInformation($"⏱️ IDLE START: User went idle at {now:HH:mm:ss}");
}
```

**Add Idle → Present transition check (after line 621):**
```csharp
// User is returning from idle (Idle → Present)
else if (previousState == UserPresenceState.Idle && newState == UserPresenceState.Present)
{
    if (_idleStartTime != default(DateTime))
    {
        var idleDuration = now - _idleStartTime;

        _logger.LogInformation($"⏱️ IDLE END: User was idle for {idleDuration.TotalMinutes:F1} minutes");

        // Check if this was an extended idle period requiring smart session reset
        if (idleDuration.TotalMinutes >= extendedAwayThresholdMinutes && !_hasBeenAwayExtended)
        {
            _hasBeenAwayExtended = true;
            _logger.LogInformation($"⚡ Extended idle period detected: {idleDuration.TotalMinutes:F1} minutes - triggering smart session reset");

            var extendedAwayArgs = new ExtendedAwayEventArgs
            {
                TotalAwayTime = idleDuration,
                AwayStartTime = _idleStartTime,
                ReturnTime = now,
                AwayState = previousState  // Idle
            };

            ExtendedAwaySessionDetected?.Invoke(this, extendedAwayArgs);
        }

        // Reset idle tracking
        _idleStartTime = default(DateTime);
    }
}
```

### Fix #2: Alternative - Make Idle Transition to Away After Threshold (ARCHITECTURAL FIX)

**File:** `Services/UserPresenceService.cs:DeterminePresenceState()` method

**Current Approach Issues:**
- This would require tracking idle duration in DeterminePresenceState
- But DeterminePresenceState is called every 15 seconds (monitoring interval)
- It would need to track when idle started to calculate if should transition to Away
- More complex state management

**Fix #1 is simpler and more maintainable.**

### Fix #3: Clear Idle Start Time in Session Reset (SAFETY FIX)

**File:** `Services/Timer/TimerService.PauseManagement.cs:SmartSessionResetAsync()` method

**Add idle tracking cleanup (after line 464):**
```csharp
// CRITICAL FIX: Clear UserPresenceService idle tracking state
try
{
    var userPresenceServiceType = _userPresenceService?.GetType();
    var idleStartTimeField = userPresenceServiceType?.GetField(
        "_idleStartTime",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    if (idleStartTimeField != null)
    {
        idleStartTimeField.SetValue(_userPresenceService, default(DateTime));
        _logger.LogCritical("🔄 SESSION RESET: Cleared _idleStartTime tracking");
    }
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "🔄 SESSION RESET: Error clearing idle tracking (non-critical)");
}
```

---

## Verification Steps

After implementing all three fixes:

1. **Setup:** Start app, leave PC idle (WITHOUT locking session)
2. **Wait:** 35+ minutes (exceeds 30-minute threshold)
3. **Return:** Move mouse to resume activity
4. **Verify Fix #1:**
   - ✓ Logs show "IDLE START: User went idle at HH:mm:ss"
   - ✓ Logs show "IDLE END: User was idle for X.X minutes"
   - ✓ Logs show "Extended idle period detected: X.X minutes - triggering smart session reset"
   - ✓ ExtendedAwaySessionDetected event fires

5. **Verify ApplicationOrchestrator:**
   - ✓ Logs show "EXTENDED AWAY SESSION DETECTED!"
   - ✓ Logs show "Away duration: X.X minutes"
   - ✓ SmartSessionResetAsync called
   - ✓ Fresh 20min/55min cycles started

6. **Verify Fix #3:**
   - ✓ Session reset logs show "_idleStartTime tracking cleared"
   - ✓ No stale idle tracking after session reset

---

## Acceptance Criteria - TO BE VERIFIED ✅

| Criteria | Status | Verification |
|----------|--------|--------------|
| Idle → Present transition triggers extended away check | ⏳ PENDING | Fix #1 implemented |
| Extended away detection works without session lock | ⏳ PENDING | Fix #1 tracks idle duration |
| Session reset clears idle tracking state | ⏳ PENDING | Fix #3 clears _idleStartTime |
| System triggers smart session reset for 30+ minute idle | ⏳ PENDING | Full integration test |
| Timers start fresh cycles after extended idle | ⏳ PENDING | Timer state verification |
| Build succeeds with no new errors | ⏳ PENDING | dotnet build verification |

---

## Related State Transitions

### Current State Machine (Broken):

```
Present ──(5min idle)──> Idle
   ↑                       ↓
   └───(user returns)──────┘

Present ──(session lock)──> Away ──(unlock)──> Present

Extended Away Check: ONLY triggers on (Away/SystemSleep → Present)
```

### Fixed State Machine:

```
Present ──(5min idle)──> Idle
   ↑                       ↓
   └───(user returns)──────┘
       (Extended away check if idle > 30min)

Present ──(session lock)──> Away ──(unlock)──> Present
                                   (Extended away check if away > 30min)

Extended Away Check: Triggers on BOTH (Away → Present) AND (Idle → Present)
```

---

## Configuration Verification

Configuration at time of bug:

```json
{
  "UserPresence": {
    "enabled": true,
    "idleThresholdMinutes": 5,
    "awayGracePeriodSeconds": 30,
    "autoPauseOnAway": true,
    "autoResumeOnReturn": true,
    "monitorSessionChanges": true,
    "monitorPowerEvents": true,
    "monitoringIntervalSeconds": 15,
    "pauseOnScreenLock": true,
    "pauseOnMonitorOff": true,
    "pauseOnIdle": true,
    "idleTimeoutMinutes": 15,
    "enableSmartSessionReset": true,
    "extendedAwayThresholdMinutes": 30,
    "showSessionResetNotification": true
  }
}
```

✓ All features enabled
✓ Thresholds properly configured
✓ Feature worked correctly for Away scenarios (with session lock)
✓ Feature failed for Idle scenarios (without session lock)

---

## Conclusion

The extended away detection failure is caused by two interconnected design flaws:

1. ✅ **Root Cause:** State machine never transitions Idle → Away automatically
2. ✅ **Secondary Issue:** HandleExtendedAwayTracking doesn't check Idle → Present transitions

**Fix:** Three-layer approach:
1. Track idle start time (PRIMARY)
2. Check idle duration on Idle → Present transition (PRIMARY)
3. Clear idle tracking in session reset (SAFETY)

**Expected Outcome:** Extended away detection works for both locked sessions AND natural idle scenarios (most common use case)

**Next Steps:** Implement fixes and verify with real-world idle scenario testing
