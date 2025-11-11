# Warning Countdown Frozen After Overnight Resume - Root Cause & Fix

**Date:** 2025-11-11
**Priority:** P1 - Critical user experience issue
**Status:** ANALYSIS COMPLETE - READY FOR IMPLEMENTATION
**Related Issues:** 001-extended-away-race-condition-fix.md, 002-p0-fix-summary.md

---

## Problem Summary

**User Report:**
```
After waking PC from extended overnight standby (8+ hours), the eye rest warning
popup countdown reaches 1 second and freezes. The main eye rest popup never appears.
```

**Observed Behavior:**
1. PC goes to sleep with eye rest timer running normally
2. User returns after 8+ hours, wakes PC
3. Fresh session reset triggers (per P0 fix)
4. If there's a stale eye rest warning countdown active:
   - Countdown displays normally for ~29 seconds
   - Reaches 1 second and **FREEZES**
   - Never transitions to full eye rest popup
   - Countdown animation stops updating
   - User must close popup manually

**Expected Behavior:**
- Fresh session should have clean slate
- No warning popup from before sleep should persist
- If new warning starts, countdown completes normally and transitions to eye rest popup

---

## Root Cause Analysis

### The Orphaned Warning Popup Problem

**Timeline of Failure:**

```
T-30sec (Before Sleep):  Eye rest timer running
                         Eye rest timer countdown: 20min → 19min → ... → 30 seconds remaining

T-0sec (User sleeps):    PC enters sleep/standby mode
                         DispatcherTimer is suspended
                         TimerService event handlers frozen
                         Warning popup (if visible) frozen at some countdown value

T+480min (User wakes):   PC wakes from overnight sleep
                         System resume events trigger
                         OnEyeRestTimerTick() detects large clock jump
                         SmartSessionResetAsync() called

SmartSessionResetAsync:  ✅ Clears popup references via reflection
                         ✅ Calls HideAllNotifications()
                         ✅ Sets _activeEyeRestWarningPopup = null
                         ❌ BUT: Doesn't stop warning timers!
                         ❌ BUT: Doesn't clear warning handler's local state!

T+481sec:                Fresh session timers start
                         New eye rest timer countdown starts (20 minutes)
                         Waits for 19:30 before triggering warning

T+490sec (After 9 min):  BUT IF: Warning popup was VISIBLE before sleep
                         Old _eyeRestWarningTimer still running!
                         Old event handler closure still has:
                         - startTime from before sleep
                         - warningDuration from before sleep
                         - hasTriggered flag (still false!)

T+491sec (10 min later): Old warning countdown completes elapsed time calculation
                         remaining = warningDuration - (now - startTime)
                         remaining ≈ 1 second (because almost all time has elapsed)

T+492sec:                UpdateEyeRestWarningCountdown(remaining: 1sec) called
                         Popup displays "1 second"

T+493sec onwards:        FROZEN STATE:
                         ❌ remaining keeps calculating as 0-1 second range
                         ❌ Check: remaining.TotalMilliseconds <= 50 evaluates
                         ❌ BUT: _activeEyeRestWarningPopup reference is NULL
                         ❌ So UpdateCountdown() has no popup to update!
                         ❌ hasTriggered is still false (old handler context)
                         ❌ warningTickHandler tries to update _activeEyeRestWarningPopup
                         ❌ But it's null, so nothing happens
                         ❌ warningTickHandler never sets hasTriggered = true
                         ❌ Countdown animation freezes at 1 second
```

### Key Architectural Issues

**Issue #1: Warning Timer Not Stopped During Session Reset**

File: `Services/Timer/TimerService.PauseManagement.cs:SmartSessionResetAsync()`

Current code:
```csharp
_eyeRestTimer?.Stop();
_breakTimer?.Stop();
_eyeRestWarningTimer = null;    // ❌ NULLS it but doesn't stop it first!
_breakWarningTimer = null;      // ❌ Same issue
```

Problem: If warning timer is running with active event handlers, setting to null doesn't stop the timer from firing or the handler from executing. The old `EventArgs warningTickHandler` closure still has references to old state.

**Issue #2: Orphaned Handler State**

File: `Services/Timer/TimerService.EventHandlers.cs:StartEyeRestWarningTimerInternal()`

The warning handler is a closure that captures:
```csharp
var startTime = DateTime.Now;           // Captured at warning start
var warningDuration = TimeSpan.From...  // Captured at warning start
var hasTriggered = false;               // Captured in closure

EventHandler<EventArgs> warningTickHandler = (sender, e) =>
{
    // This handler will KEEP RUNNING even if session resets!
    // It still sees the OLD captured variables
};
```

When session reset happens:
- `_eyeRestWarningTimer = null` (timer object ref cleared)
- But the old timer object is still running!
- The old handler closure still has captured `startTime` from BEFORE sleep
- Calculating `remaining = warningDuration - (DateTime.Now - startTime)` uses the OLD start time
- After 8 hours, this calculation gives tiny remaining time (1 second range)

**Issue #3: Popup Reference Mismatch**

The handler tries to update:
```csharp
_notificationService?.UpdateEyeRestWarningCountdown(remaining);
```

Which does:
```csharp
if (_activeEyeRestWarningPopup != null)
{
    _dispatcher.InvokeAsync(() =>
    {
        _activeEyeRestWarningPopup?.UpdateCountdown(remaining);
    });
}
```

But `_activeEyeRestWarningPopup` was set to null by `SmartSessionResetAsync()`. So the update silently fails, and the countdown freezes.

**Issue #4: hasTriggered Never Gets Set**

The completion check:
```csharp
if (remaining.TotalMilliseconds <= 50)
{
    hasTriggered = true;
    _eyeRestWarningTimer?.Stop();
    // Trigger eye rest
}
```

But this check happens inside the handler closure. If the handler is checking a null popup reference and failing silently, we need to ensure:
1. Handler actually executes the trigger code
2. handler marks `hasTriggered = true` to prevent re-triggering
3. The transition from Warning → EyeRestDue is atomic

---

## Affected Code Locations

| File | Issue | Impact |
|------|-------|--------|
| `Services/Timer/TimerService.PauseManagement.cs:254-330` | SmartSessionResetAsync doesn't stop warning timers | Orphaned timers continue running |
| `Services/Timer/TimerService.EventHandlers.cs:613-825` | StartEyeRestWarningTimerInternal captures old state in closure | Old handler calculations use stale timestamps |
| `Services/NotificationService.cs:1050-1059` | UpdateEyeRestWarningCountdown silently fails on null popup | Countdown display freezes |
| `Views/EyeRestWarningPopup.xaml.cs:45-68` | UpdateCountdown doesn't verify valid remaining state | Displays frozen "1 second" |

---

## Fix Strategy

### Fix #1: Stop Warning Timers in SmartSessionResetAsync

**Location:** `Services/Timer/TimerService.PauseManagement.cs:SmartSessionResetAsync()`

```csharp
// CRITICAL FIX: Explicitly stop warning timers BEFORE nulling references
_logger.LogCritical("🧹 SESSION RESET: Stopping all warning timers to prevent orphaned handlers");

// Stop eye rest warning timer
if (_eyeRestWarningTimer?.IsEnabled == true)
{
    _eyeRestWarningTimer.Stop();
    _logger.LogCritical("🧹 SESSION RESET: Eye rest warning timer stopped");
}

// Stop break warning timer
if (_breakWarningTimer?.IsEnabled == true)
{
    _breakWarningTimer.Stop();
    _logger.LogCritical("🧹 SESSION RESET: Break warning timer stopped");
}

// Stop fallback timers
_eyeRestWarningFallbackTimer?.Stop();
_breakWarningFallbackTimer?.Stop();
_logger.LogCritical("🧹 SESSION RESET: Fallback warning timers stopped");

// Now dispose/null the timers
_eyeRestWarningTimer = null;
_breakWarningTimer = null;
_eyeRestWarningFallbackTimer = null;
_breakWarningFallbackTimer = null;
```

**Benefit:** Ensures warning timers are stopped before session reset completes, preventing orphaned handlers from continuing to execute.

### Fix #2: Force Complete Any Active Warning Popup

**Location:** `Services/Timer/TimerService.PauseManagement.cs:SmartSessionResetAsync()`

Add warning popup completion before clearing references:

```csharp
// CRITICAL FIX: Force complete any active warning countdowns to prevent frozen popups
_logger.LogCritical("🧹 SESSION RESET: Forcing warning popup completion before session reset");

try
{
    // Get the active warning popup reference before it's cleared
    var warningPopupType = _notificationService?.GetType();
    var activeWarningField = warningPopupType?.GetField(
        "_activeEyeRestWarningPopup",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    var activeWarningPopup = activeWarningField?.GetValue(_notificationService);
    if (activeWarningPopup != null)
    {
        // Force the warning to complete by invoking WarningCompleted event
        var warningCompletedField = activeWarningPopup.GetType()
            .GetEvent("WarningCompleted", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        _logger.LogCritical("🧹 SESSION RESET: Active warning popup found - forcing completion");

        // Invoke completion event handlers
        var raiseMethod = warningCompletedField?.GetRaiseMethod(true);
        if (raiseMethod != null)
        {
            raiseMethod.Invoke(activeWarningPopup, new object[] { activeWarningPopup, EventArgs.Empty });
            _logger.LogCritical("🧹 SESSION RESET: Warning popup completion event forced");
        }
    }
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "🧹 SESSION RESET: Could not force warning popup completion (non-critical)");
}
```

**Benefit:** Ensures any active warning popup properly completes/closes instead of remaining orphaned.

### Fix #3: Add Safeguard in Warning Handler

**Location:** `Services/Timer/TimerService.EventHandlers.cs:StartEyeRestWarningTimerInternal()`

Add validation inside the warning tick handler to detect stale state:

```csharp
EventHandler<EventArgs> warningTickHandler = (sender, e) =>
{
    try
    {
        if (hasTriggered) return;

        var elapsed = DateTime.Now - startTime;
        var remaining = warningDuration - elapsed;

        // CRITICAL FIX: Detect orphaned warning handler (stale state after session reset)
        // If remaining time is negative and exceeds reasonable bounds, this is likely
        // an orphaned handler from before session reset
        if (remaining.TotalSeconds < -1) // More than 1 second overdue
        {
            _logger.LogCritical($"🚨 ORPHANED HANDLER DETECTED: Warning timer shows {remaining.TotalSeconds:F1}s remaining (before elapsed). Session likely reset!");
            _logger.LogCritical($"🚨 Aborting handler execution - session has been reset and this handler is stale");
            hasTriggered = true; // Prevent further execution
            return;
        }

        // ... rest of handler code
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "⏰ ERROR in warning handler - marking as triggered to prevent loop");
        hasTriggered = true; // Ensure handler is marked triggered on any error
    }
};
```

**Benefit:** Prevents stale handlers from getting stuck in frozen countdown state.

### Fix #4: Add Validation in UpdateCountdown

**Location:** `Views/EyeRestWarningPopup.xaml.cs:UpdateCountdown()`

Add check to prevent infinite loops on invalid state:

```csharp
public void UpdateCountdown(TimeSpan remaining)
{
    System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup: UpdateCountdown called with {remaining.TotalSeconds} seconds remaining");

    // CRITICAL FIX: Validate remaining time is not getting stuck in frozen state
    // If remaining stays at 0-1 second for multiple updates, something is wrong
    if (remaining >= TimeSpan.Zero && remaining < TimeSpan.FromMilliseconds(100))
    {
        System.Diagnostics.Debug.WriteLine($"👁 EyeRestWarningPopup: Remaining time in critical zone ({remaining.TotalMilliseconds:F1}ms) - may be orphaned");
    }

    if (remaining <= TimeSpan.Zero)
    {
        // Final state - animate completion
        // ... completion code
    }
    else
    {
        UpdateDisplay(remaining);
    }
}
```

**Benefit:** Adds logging to detect frozen countdown state for debugging.

---

## Implementation Plan

### Phase 1: Stop Warning Timers
- Modify `SmartSessionResetAsync()` to explicitly stop warning timers before nulling references
- Add logging to track timer lifecycle

### Phase 2: Force Warning Completion
- Add forced completion logic to `SmartSessionResetAsync()` using reflection
- Ensure warning popups properly transition to closed state

### Phase 3: Add Orphaned Handler Detection
- Modify warning handler to detect stale elapsed time calculations
- Prevent frozen countdown by aborting stale handlers

### Phase 4: Add Validation and Logging
- Add safeguards in `UpdateCountdown()` to detect frozen state
- Improve logging for debugging future issues

---

## Files to Modify

1. `Services/Timer/TimerService.PauseManagement.cs` - SmartSessionResetAsync (main fix)
2. `Services/Timer/TimerService.EventHandlers.cs` - Warning handler safeguards
3. `Views/EyeRestWarningPopup.xaml.cs` - Validation and logging
4. `Services/NotificationService.cs` - Optional: Add debug logging

---

## Acceptance Criteria

**Given** PC wakes from 8+ hour overnight standby with stale warning countdown active:
- **When** session reset is triggered
- **Then** any active warning countdown is forcibly completed
- **And** old warning timers are stopped (don't continue running)
- **And** new fresh session starts without orphaned handlers
- **And** no countdown gets stuck at "1 second"
- **And** if new warning appears, it counts down normally and transitions to eye rest popup

---

## Verification Steps

1. Setup: Configure eye rest warning duration to 30 seconds
2. Start app, let timer run to show warning popup (9:30 remaining on eye rest)
3. Sleep PC for 30+ seconds (simulates overnight)
4. Wake PC
5. Observe: Warning should NOT freeze at 1 second
6. Verify: Fresh session starts normally

---

## Build & Testing

After implementation:
```bash
dotnet build
dotnet test --filter Category=Integration
```

Should pass all existing tests + new orphaned handler detection scenario.
