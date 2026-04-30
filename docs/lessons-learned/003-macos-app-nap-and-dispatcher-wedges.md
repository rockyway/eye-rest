# 003 — macOS App Nap, Dispatcher Wedges, and Always-On Desktop Apps

> Three production hangs in two days, all on macOS, all with the same shape:
> the process stayed alive but every `DispatcherTimer` in the app stopped
> firing simultaneously. This document captures the patterns we learned —
> they apply to any always-on desktop app (Pomodoro timers, screen brighteners,
> health reminders, snooze tools, menu-bar utilities) using Avalonia, WPF, or
> any UI toolkit that schedules timers through a single dispatcher loop.

## The three failure modes we saw

### 1. Re-entering a `DispatcherTimer` from inside its own `Tick` handler

**Symptom.** A new "smart coalesce" path called `_eyeRestTimer.Stop()`
followed by `_ = RestartEyeRestTimerAfterCompletion()` (which internally
calls `_eyeRestTimer.Start()`) — all inside `OnEyeRestTimerTick`. After
the first time this path fired in production, **every** `DispatcherTimer`
in the app went silent at the exact same instant: 1 s tray-tooltip
update, 60 s health monitor, 5 min config save, 5 min performance
metrics, the break tick, the next eye-rest tick. The Avalonia run loop
on the main thread was idle in `_BlockUntilNextEventMatchingListInMode`
— alive, waiting for events that the timer subsystem stopped queuing.

**Root cause.** Avalonia's `DispatcherTimer` keeps an internal sorted
list of pending timers. Calling `Stop()` then `Start()` on a timer
*from within that same timer's `Tick` callback* re-enters the list while
the dispatcher is still processing the slot for the current tick. The
re-entry corrupts the list, and no `DispatcherTimer` in the app fires
afterward. Note that the `_ = ...` on a fire-and-forget `async Task`
that begins with `await Task.CompletedTask` does **not** yield — the
body runs synchronously up through the `Start()` call.

**Fix.** Defer the re-arm with `Dispatcher.UIThread.Post(...)`
(or your dispatcher's equivalent — we used the `IDispatcherService`
abstraction we already had). The current tick fully unwinds before the
next `Start()` runs. Tests that drive dispatch synchronously
(`FakeDispatcherService` running `BeginInvoke` inline) keep passing —
production behavior changes, test behavior doesn't.

```csharp
// BAD — wedges the dispatcher in production
_eyeRestTimer.Stop();
_ = RestartEyeRestTimerAfterCompletion();

// GOOD — restart runs after this tick unwinds
_eyeRestTimer.Stop();
_dispatcherService.BeginInvoke(() => _ = RestartEyeRestTimerAfterCompletion());
```

### 2. False-positive hang detection while the user is intentionally idle

**Symptom.** User goes idle for ~30 minutes. `SmartPauseAsync`
correctly pauses the timer service. The health monitor's
heartbeat-refresh path required `serviceRunningNormally =
IsRunning && !IsPaused && !IsSmartPaused && timersAreActive` — so when
smart-paused, the heartbeat went stale. After 30 min the dynamic
threshold fired `hangDetected = true` and ran an aggressive
`RecoverTimersFromHang()` which disposed and recreated every
`DispatcherTimer`, `GC.Collect()` + `GC.WaitForPendingFinalizers()` on
the UI thread. That recovery hit the same dispatcher-wedge mechanism
from #1 a few minutes later. Two production reproductions had identical
shape: 12:43 idle → 13:13 false hang detection → 13:22 dispatcher
frozen for the rest of the day.

**Root cause.** "No recent timer ticks" is not the same as "timer
system broken." For a Pomodoro-style app, timers are *expected* to be
quiet for long stretches (idle, manual pause, do-not-disturb). Treating
every silence longer than 30 min as a hang produces alarms exactly
when they hurt the most.

**Fix.** Short-circuit the health monitor when paused — refresh the
heartbeat (so the moment the user returns has a clean baseline) and
return without running any of the downstream overdue / disabled-timer
checks (which all assume an actively-running service).

```csharp
if (IsPaused || IsSmartPaused || IsManuallyPaused)
{
    UpdateHeartbeat();
    return;  // Paused is a state, not a hang
}
```

### 3. macOS App Nap throttling under memory pressure

**Symptom.** User's machine goes under memory pressure (Chrome with
80 tabs, Slack, Docker, etc.). After ~1 hour, EyeRest reports "Not
Responding" in Activity Monitor. Closing other apps frees memory but
EyeRest doesn't recover until restarted.

**Root cause.** macOS *App Nap* is exactly designed to do this — it
throttles or suspends apps it considers "background work" (apps without
a visible window, apps not receiving user input, menu-bar utilities).
The OS coalesces or drops timer ticks. To the user, the app appears
frozen. There is **no warning** — the OS doesn't tell the app it's
about to be napped, and the in-tick clock-jump detection only runs
when a tick fires, which by definition isn't happening during nap.

**Fix.** Three things, in priority order:

1. **Opt out of App Nap** with `NSProcessInfo.processInfo
   .beginActivityWithOptions:reason:` at startup, holding the returned
   token until the app exits. Use options
   `(NSActivityUserInitiated & ~NSActivityIdleSystemSleepDisabled) | NSActivityLatencyCritical`
   so timers fire reliably but the laptop can still go to sleep when
   the user closes the lid.
2. **Subscribe to `NSWorkspaceDidWakeNotification`** on
   `NSWorkspace.sharedWorkspace.notificationCenter` and trigger a clean
   session reset on wake. Don't depend on the next stale tick to
   eventually trip clock-jump detection — explicitly reset.
3. **Subscribe to `NSWorkspaceWillSleepNotification`** for diagnostic
   logging so you can correlate user-reported hangs with system events.

Windows analogue: `Microsoft.Win32.SystemEvents.PowerModeChanged`
(`PowerModes.Suspend` / `PowerModes.Resume`). No App Nap to opt out
of — Windows DispatcherTimers fire reliably as long as the process
is awake.

## Cross-cutting principles for always-on desktop apps

1. **Never re-enter a `DispatcherTimer`'s state from inside its own
   `Tick` handler.** If you must `Stop()` and `Start()` it, defer the
   `Start()` with `Dispatcher.UIThread.Post`. This applies to
   `Interval` changes too. The `DispatcherTimer` is only safe to
   re-arm once the current tick has fully unwound.

2. **Distinguish *paused* from *hung*.** Any health-monitor or
   heartbeat-watchdog must short-circuit when the service is in a
   known paused state — otherwise you'll fire false alarms whenever
   the user is doing the thing your app is supposed to encourage
   (idling, taking a break, going to lunch).

3. **Don't trust in-tick recovery as your only post-suspension
   signal.** Clock-jump detection inside a timer handler only runs
   when that timer fires — which may be many minutes after wake-up,
   and may not fire at all if the OS swallowed the tick. You need
   an *external* wake notification: `NSWorkspaceDidWakeNotification`
   on macOS, `SystemEvents.PowerModeChanged` on Windows.

4. **Opt out of OS-level throttling explicitly.** macOS App Nap and
   Windows "Modern Standby" are both opt-in to be tame. For an app
   whose entire job is firing scheduled events, you must call
   `NSProcessInfo.beginActivity` (macOS) or set the appropriate
   power-aware flags (Windows). Defaults are wrong for your case.

5. **Recovery code is more dangerous than the bugs it tries to fix.**
   `GC.WaitForPendingFinalizers()` on the UI thread, disposing and
   recreating system-level timers, force-clearing internal state —
   each of these is an opportunity to wedge things further. Prefer
   *prevention* (don't trigger the false-alarm condition in the
   first place) to *recovery* (try to un-wedge after the fact).

6. **A dispatcher-wedge symptom looks identical across causes.**
   If the main thread is alive in its run-loop idle wait but no
   timers fire, the cause is in the timer subsystem, not the
   dispatcher itself. Look for: re-entry from inside a tick, timers
   being disposed/recreated from a non-UI thread, or process-level
   throttling by the OS.

## Diagnostic playbook

When the user reports "the app is showing as Not Responding":

1. **`ps aux | grep <app> | grep -v grep`** — confirm the process is
   alive. Note the memory: a wedged-but-allocating process has
   memory growth (5-10× idle baseline) suggesting work is queued
   but not draining.
2. **`sample <PID> 5`** — capture a 5 s stack sample. The main
   thread should be in `_BlockUntilNextEventMatchingListInMode` (or
   equivalent). If it is, the dispatcher is alive — the timer
   subsystem isn't.
3. **Read the app's last log lines.** A single tick that ran *just
   before* every other timer went silent is your prime suspect.
4. **Correlate with `pmset -g log | grep Sleep`** — was there a
   system sleep/wake right around the silence? If so, you need an
   explicit wake handler, not just in-tick clock-jump detection.
5. **Check whether the app opted out of App Nap.** On macOS
   `lsappinfo info -only ApplicationType <PID>` and check whether
   `NSProcessInfo.processInfo.beginActivity` was ever called. If
   not, App Nap is your default suspect under memory pressure.

## What to copy into another always-on app

The patterns above are codified in the following files in this repo
— copy them as a starting point:

| Concern | Files |
|---|---|
| Lifecycle interface (cross-platform) | `EyeRest.Abstractions/Services/IAppLifecycleService.cs` |
| macOS App Nap opt-out + NSWorkspace observers | `EyeRest.Platform.macOS/Services/MacOSAppLifecycleService.cs`, `EyeRest.Platform.macOS/Interop/MacOSAppLifecycleInterop.cs` |
| Windows PowerModeChanged bridge | `EyeRest.Platform.Windows/Services/WindowsAppLifecycleService.cs` |
| Health-monitor paused-state short-circuit | `EyeRest.Core/Services/Timer/TimerService.Recovery.cs` (`OnHealthMonitorTick`, top of method) |
| Defer DispatcherTimer re-arm out of own tick | `EyeRest.Core/Services/Timer/TimerService.EventHandlers.cs` (`OnEyeRestTimerTick`, coalesce branch) |

The macOS file is the most reusable for any .NET 8 / Avalonia /
menu-bar app — it dynamically registers an Objective-C class with
`objc_allocateClassPair` + `class_addMethod` and binds it to two
`[UnmanagedCallersOnly]` C# entry points. No Xamarin / .NET MAUI
runtime required.

## Related commits in this repo

- `7947135` — initial coalesce path (introduced bug #1)
- `2cfddff` — defer eye-rest restart out of coalesce tick (fix #1)
- `2ca6dfb` — skip false-positive hang detection while paused (fix #2)
- App Nap opt-out + NSWorkspace observers + Windows PowerMode bridge (fix #3 — current work)
