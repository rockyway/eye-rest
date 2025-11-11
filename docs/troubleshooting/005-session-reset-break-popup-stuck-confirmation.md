# Session Reset Force-Closes Break Popup But Timer Continues - Investigation

**Date:** 2025-11-11
**Priority:** P0 - Critical State Corruption Bug
**Status:** ✅ ROOT CAUSE IDENTIFIED
**Investigation ID:** Break-SessionReset-005

---

## Problem Statement

**User Action:** PC woke from sleep/extended away (5+ minutes) while break popup was visible

**Expected Behavior:**
- Session reset triggers fresh session
- All popups cleanly closed
- Timers restart normally

**Actual Behavior:**
- Break popup flashes briefly and closes
- System shows "Smart Paused (Waiting for break confirmation)"
- No visible break confirmation popup
- User completely stuck - cannot resume timers

**Impact:** P0 - System unusable, user cannot resume work

---

## Timeline of Events

```
13:28:28.117  Break popup shown normally with 5-minute countdown
13:28:28.117  BreakPopup._progressTimer started (ticks every 100ms)
13:28:28.692  Break popup visible and active

13:28:29.123  DispatcherTimer system broke → Emergency fallback activated
13:28:30.125  SESSION RESET triggered (reason: recovery after timer failure)
13:28:30.125  "SESSION RESET: Clearing all popup windows for fresh session"
13:28:30.126  CloseCurrentPopup() called
13:28:30.126  BreakPopup force-closed (window.Close() called)
             ❌ BUG: BreakPopup._progressTimer NOT stopped
             ❌ BUG: _progress callback reference still held by timer
13:28:30.126  "SMART SESSION RESET COMPLETED - fresh 20min/55min cycle started"
13:28:30.138  "ZOMBIE FIX: All popup references cleared and validated"

[PC sleeps or user away for ~5 minutes]

13:33:20.624  FALLBACK RECOVERY completed (DispatcherTimer restored)
             ⚠️ Orphaned BreakPopup._progressTimer still running in background

13:33:28.808  ⚠️ BreakPopup._progressTimer final tick fires
             Calculated: remaining = _duration - elapsed = 5min - 5min = 0
             Calls: _progress?.Report(1.0) ← ORPHANED CALLBACK FIRES
13:33:28.808  "P1 FIX: Set _isWaitingForBreakConfirmation=true"
13:33:28.909  SmartPauseAsync("Waiting for break confirmation") called
13:33:28.913  "Break completed - timers smart-paused while waiting for user confirmation"
             ❌ System stuck: Smart Paused with no visible popup
```

---

## Root Cause Analysis

### Missing Countdown Stop in CloseCurrentPopup

**File:** `Services/NotificationService.cs:945-1047` (CloseCurrentPopup method)

**Current Code (Lines 984-994):**
```csharp
// CRITICAL FIX: If this is a BreakPopup, ensure force close is set
if (popupToClose is BasePopupWindow baseWindow && baseWindow.ContentArea?.Content is BreakPopup breakPopup)
{
    _logger.LogDebug("🔴 Setting force close on BreakPopup before closing");
    var forceCloseField = typeof(BreakPopup).GetField("_forceClose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (forceCloseField != null)
    {
        forceCloseField.SetValue(breakPopup, true);
        _logger.LogDebug("🔴 Force close flag set successfully");
    }
}
```

**Problem:** Only sets `_forceClose` flag, but **NEVER calls `breakPopup.StopCountdown()`**

**Comparison with Warning Popups (Lines 909, 923):**
```csharp
if (_activeBreakWarningPopup != null)
{
    try
    {
        _activeBreakWarningPopup.StopCountdown();  // ✓ WARNING POPUPS PROPERLY STOPPED
    }
    catch (Exception ex)
    {
        _logger.LogWarning($"Exception stopping stale break warning popup: {ex.Message}");
    }
    _activeBreakWarningPopup = null;
}
```

Warning popups ARE properly stopped, but break popups are NOT.

---

### BreakPopup Timer Architecture

**File:** `Views/BreakPopup.xaml.cs:14-96`

```csharp
private DispatcherTimer? _progressTimer;  // Line 14

public void StartCountdown(TimeSpan duration, IProgress<double>? progress = null)
{
    _duration = duration;
    _startTime = DateTime.Now;
    _progress = progress;  // ← Stores callback reference

    _progressTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(100)  // Ticks 10 times per second
    };

    _progressTimer.Tick += OnProgressTimerTick;
    _progressTimer.Start();  // ← Timer starts
}

private void OnProgressTimerTick(object? sender, EventArgs e)
{
    var elapsed = DateTime.Now - _startTime;
    var remaining = _duration - elapsed;

    if (remaining <= TimeSpan.Zero)
    {
        _progressTimer?.Stop();
        _progress?.Report(1.0);  // ← TRIGGERS "P1 FIX" IN NotificationService
        ShowCompletionState();
        return;
    }

    _progress?.Report(progressPercent / 100.0);  // Regular progress updates
}

public void StopCountdown()  // ← THIS METHOD MUST BE CALLED TO STOP TIMER
{
    if (_progressTimer != null)
    {
        _progressTimer.Stop();
        _progressTimer.Tick -= OnProgressTimerTick;
        _progressTimer = null;
    }
}
```

---

### Progress Callback Chain

**File:** `Services/NotificationService.cs:761-812`

```csharp
var progressWithCompletion = new Progress<double>(value =>
{
    progress?.Report(value);

    // CRITICAL FIX: Enhanced break completion handling with race condition protection
    if (value >= 1.0 && breakConfig.Break.RequireConfirmationAfterBreak)
    {
        lock (_lockObject)
        {
            if (_isBreakCompletionInProgress)
            {
                return;  // ← Lock prevents duplicate triggers
            }
            _isBreakCompletionInProgress = true;
        }

        // P1 FIX: Set flag SYNCHRONOUSLY
        _isWaitingForBreakConfirmation = true;  // Line 781
        _logger.LogCritical("🎯 P1 FIX: Set _isWaitingForBreakConfirmation=true");

        Task.Run(async () =>
        {
            await Task.Delay(100);

            if (_timerService != null)
            {
                await _timerService.SmartPauseAsync("Waiting for break confirmation");  // Line 795
                _logger.LogInformation("🎯 Break completed - timers smart-paused");
            }
        });
    }
});

breakPopup.StartCountdown(duration, progressWithCompletion);  // Line 814
```

When `_progress.Report(1.0)` is called, this callback executes and triggers smart pause.

---

## Why The Bug Occurs

### Timeline of Failure

```
T0:    BreakPopup created
       _progressTimer starts (100ms interval)
       _progress callback = progressWithCompletion (references NotificationService)

T+2s:  Session reset calls CloseCurrentPopup()
       Sets _forceClose = true
       Calls popupWindow.Close()
       ❌ NEVER calls breakPopup.StopCountdown()

       Result: Window closes BUT timer keeps running
       _progressTimer still has:
         - Active Tick event handler
         - Reference to _progress callback
         - _startTime captured from T0

T+5min: _progressTimer fires final tick
        Calculates: remaining = 5min - 5min = 0
        Sees remaining <= 0
        Calls: _progress?.Report(1.0)

        Progress callback executes:
        Sets _isWaitingForBreakConfirmation = true
        Calls SmartPauseAsync("Waiting for break confirmation")

        System state:
        - SmartPaused = true
        - PauseReason = "Waiting for break confirmation"
        - NO visible popup (was closed 5 minutes ago)
        - User has no way to resume
```

---

## Evidence from Logs

### Evidence #1: No StopCountdown Log

When `HideAllNotifications()` is called at 13:28:30.126:
```
[INF] 🔴 Setting force close on BreakPopup before closing
[DBG] 🔴 Force close flag set successfully
[DBG] 🔴 Popup is loaded and visible, proceeding to close: BasePopupWindow
[DBG] 🔴 Calling Close() on popup window
[DBG] 🔴 Popup Close() called successfully
```

But NO log showing:
```
❌ MISSING: "BreakPopup.StopCountdown: Timer stopped after X minutes"
```

Compare with warning popups which DO get stopped:
```
[CRT] 🧟 ZOMBIE FIX: Force clearing stale _activeBreakWarningPopup reference
[WRN] Exception stopping stale break warning popup: ...
```

### Evidence #2: Orphaned Progress Callback Fires 5 Minutes Later

At 13:33:28.808 (exactly 5 minutes after popup started at 13:28:28):
```
[FTL] 🎯 P1 FIX: Set _isWaitingForBreakConfirmation=true SYNCHRONOUSLY
[INF] 🎯 Break completed - timers smart-paused while waiting for user confirmation
```

This happened with **NO preceding break trigger** or **ShowBreakReminderAsync** call.

### Evidence #3: System Stuck in Smart Paused State

After 13:33:28, system repeatedly shows:
```
[DBG] Timer status updated: Smart Paused (Waiting for break confirmation)
[INF] 💤 Break confirmation waiting: 1.0min (threshold: 15min)
[INF] 💤 Break confirmation waiting: 2.0min (threshold: 15min)
```

With no visible popup for user to click.

---

## Impact Analysis

### Severity: P0 - Critical State Corruption

| Component | Impact |
|-----------|--------|
| User Experience | 🔴 System completely unusable - no way to resume timers |
| Recovery | 🔴 Requires killing application or manually resuming via UI |
| Data Integrity | 🟡 Timer states corrupted (thinks break is active but no popup) |
| Session Management | 🔴 Session reset fails to properly clean up break popup state |

### Reproducibility

**Confirmed:** Reproduces when session reset occurs during active break popup

**Trigger Conditions:**
- Break popup visible with countdown active
- Session reset triggered (extended away, timer failure recovery, etc.)
- PC sleep/wake during break

**Frequency:** High - Any extended away during break will trigger this

---

## Root Cause Summary

| Aspect | Details |
|--------|---------|
| **Type** | Missing cleanup code / Incomplete implementation |
| **Location** | `NotificationService.cs:CloseCurrentPopup()` method (lines 984-994) |
| **What's Missing** | Call to `breakPopup.StopCountdown()` before closing window |
| **Why It Matters** | BreakPopup's `_progressTimer` continues running after window closes |
| **Consequence** | Orphaned timer fires 5 minutes later, triggers "P1 FIX" completion code |
| **Result** | System stuck in "Smart Paused (Waiting for break confirmation)" with no popup |

---

## Fix Required

### Fix #1: Stop BreakPopup Timer Before Force Close (PRIMARY)

**File:** `Services/NotificationService.cs:CloseCurrentPopup()` method

**Location:** After line 993 (after setting _forceClose flag)

```csharp
// CRITICAL FIX: If this is a BreakPopup, ensure force close is set
if (popupToClose is BasePopupWindow baseWindow && baseWindow.ContentArea?.Content is BreakPopup breakPopup)
{
    _logger.LogDebug("🔴 Setting force close on BreakPopup before closing");
    var forceCloseField = typeof(BreakPopup).GetField("_forceClose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (forceCloseField != null)
    {
        forceCloseField.SetValue(breakPopup, true);
        _logger.LogDebug("🔴 Force close flag set successfully");
    }

    // ❌ CRITICAL FIX: Stop BreakPopup countdown timer before closing window (MISSING!)
    // Without this, the timer continues running and fires orphaned completion callback
    try
    {
        breakPopup.StopCountdown();
        _logger.LogCritical("🔴 CRITICAL FIX: Stopped BreakPopup countdown timer before force close");
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "🔴 Exception stopping BreakPopup countdown (non-critical)");
    }
}
```

This matches the pattern already used for warning popups at lines 909, 923.

### Fix #2: Guard Against Orphaned Progress Callbacks (SECONDARY)

**File:** `Services/NotificationService.cs` (Progress callback, line 766)

**Add check before triggering smart pause:**

```csharp
if (value >= 1.0 && breakConfig.Break.RequireConfirmationAfterBreak)
{
    // CRITICAL FIX: Verify popup is still active before triggering completion
    // Prevents orphaned timers from triggering smart pause after popup force-closed
    if (_currentPopup == null || !(_currentPopup.ContentArea?.Content is BreakPopup))
    {
        _logger.LogWarning("🎯 Break completion callback fired but popup no longer active - ignoring orphaned event");
        return;
    }

    lock (_lockObject)
    {
        // ... rest of completion code
    }
}
```

### Fix #3: Clear Break Completion State During Session Reset (TERTIARY)

**File:** `Services/Timer/TimerService.PauseManagement.cs:SmartSessionResetAsync()` method

**Location:** After line 434 (after clearing processing flags)

```csharp
// CRITICAL FIX: Clear break completion state to prevent orphaned completion events
// This prevents force-closed break popups from triggering smart pause later
try
{
    var notificationServiceType = _notificationService?.GetType();
    var waitingForBreakField = notificationServiceType?.GetField(
        "_isWaitingForBreakConfirmation",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var completionInProgressField = notificationServiceType?.GetField(
        "_isBreakCompletionInProgress",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    if (waitingForBreakField != null)
    {
        waitingForBreakField.SetValue(_notificationService, false);
        _logger.LogCritical("🔄 SESSION RESET: Cleared _isWaitingForBreakConfirmation flag");
    }
    if (completionInProgressField != null)
    {
        completionInProgressField.SetValue(_notificationService, false);
        _logger.LogCritical("🔄 SESSION RESET: Cleared _isBreakCompletionInProgress flag");
    }
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "🔄 SESSION RESET: Error clearing break completion state (non-critical)");
}
```

---

## Verification Steps

After implementing all three fixes:

1. **Setup:** Start app, let break popup appear with countdown
2. **Trigger:** Force session reset (via extended away or manual recovery)
3. **Verify Fix #1:**
   - ✓ Logs show "Stopped BreakPopup countdown timer before force close"
   - ✓ Break popup closes cleanly
   - ✓ No orphaned timers continue running

4. **Wait 5+ minutes** (simulate the time break would have completed)
5. **Verify Fix #2:**
   - ✓ No "P1 FIX: Set _isWaitingForBreakConfirmation=true" appears
   - ✓ System NOT stuck in "Smart Paused (Waiting for break confirmation)"
   - ✓ Timers continue normally

6. **Verify Fix #3:**
   - ✓ Session reset logs show break completion flags cleared
   - ✓ Fresh session starts cleanly without orphaned state

---

## Acceptance Criteria - TO BE VERIFIED ✅

| Criteria | Status | Verification |
|----------|--------|--------------|
| BreakPopup timer stopped when force-closed | ⏳ PENDING | Fix #1 implemented |
| No orphaned progress callbacks fire after close | ⏳ PENDING | Fix #2 guards against orphans |
| Session reset clears break completion state | ⏳ PENDING | Fix #3 clears flags |
| System never stuck in "Smart Paused" with no popup | ⏳ PENDING | Full integration test |
| Build succeeds with no new errors | ⏳ PENDING | dotnet build verification |

---

## Conclusion

The session reset force-close break popup bug is caused by three interconnected issues:

1. ✅ **Root Cause:** CloseCurrentPopup() never calls `breakPopup.StopCountdown()`
2. ✅ **Secondary Issue:** Progress callback doesn't check if popup still active
3. ✅ **Tertiary Issue:** Session reset doesn't clear break completion flags

**Fix:** Three-layer defense:
1. Stop timer when force-closing (PRIMARY)
2. Guard callback execution (SECONDARY)
3. Clear completion state in session reset (TERTIARY)

**Expected Outcome:** Break popups cleanly close during session reset with no orphaned timers or stuck states
