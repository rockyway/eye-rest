# Review: PR #14 — Disable Break Timer (eye-rest-only mode)

_Date: 2026-07-10_
_Branch: `feature/disable-break-timer` → `develop`_
_Reviewers: Internal (Claude), External agy (Gemini 3.5 Flash High), External codex (gpt-5.6-terra). All three: **request-changes**._

## Consensus summary

The three entry-point gates (`OnBreakTimerTick`, `StartBreakWarningTimerInternal`, `TriggerBreak(Automatic)`) correctly block every **user-visible** automatic break, and manual "Break Now" is correctly preserved. The problems are in the **surrounding state machine**, which was not made eye-rest-only-aware. Two automatic, no-user-action regressions to the eye-rest flow (the feature this PR is meant to protect) must be fixed.

## Verified findings (by severity)

### H1 — Health monitor false-hang resets the eye-rest schedule ~hourly  ✅ VERIFIED
_Reporter: Internal (unique). Files: `TimerService.Recovery.cs:120-121,198-206,242-288`; `Lifecycle.cs:54`._

`StartAsync` sets `_breakStartTime = _clock.Now` unconditionally even when the break timer is never started. After ~55 min `breakMaybeOverdue` latches true → heartbeat stops refreshing → `serviceRunningButTimersDisabled` fires (because `_breakTimer.IsEnabled != true` in eye-rest-only mode) → "TIMER STATE RECOVERY" resets `_eyeRestStartTime = now` and restarts both timers. Net: on any ~1 h unpaused session, eye-rest reminders are periodically delayed/reset, plus hourly timer churn/log spam. Triggers automatically.

**Fix:** Don't set `_breakStartTime` when `!IsBreakEnabled` (leave `DateTime.MinValue`, which neutralizes the overdue guards), AND make `breakMaybeOverdue` / `breakOverdue` / `serviceRunningButTimersDisabled` treat the break timer as "not expected to run" when `!IsBreakEnabled`.

### H2 — Disabling during a break warning leaves eye-rest paused permanently  ✅ VERIFIED
_Reporter: all three. Files: `TimerService.cs` `DisableBreakTimerLive`; `Coordination.cs:89-151`._

A break warning calls `SmartPauseEyeRestTimerForBreak()` (sets `_eyeRestTimerPausedForBreak = true`, stops eye-rest). Eye-rest is normally re-armed only on break completion. `DisableBreakTimerLive` stops the warning timer and clears warning flags but never clears `_eyeRestTimerPausedForBreak` or restarts eye-rest — so aborting the warning leaves eye-rest stopped indefinitely. (`SmartResumeEyeRestTimerAfterBreak()` does the right thing but has **zero callers**.)

**Fix:** In `DisableBreakTimerLive`, after clearing warning flags, call `SmartResumeEyeRestTimerAfterBreak()` (re-arms eye-rest when it was paused-for-break) and hide any active break-warning popup.

### M1 / B — Break-timer starts outside the changed files aren't gated  ✅ VERIFIED
_Reporter: all three. Files: `Recovery.cs:272,784,900,1314,1394`; `PauseManagement.cs:96,367,692,857`; `Coordination.cs:181-214`; `SmartSessionResetAsync`._

Resume/reset/recovery/coordination paths call `_breakTimer.Start()` with no `IsBreakEnabled` check. No visible break results (the gated tick self-stops it), but the timer repeatedly flips enabled↔disabled — feeding H1 churn and the coalesce issue below. codex's sharp case: after a manual break, `SmartSessionResetAsync` re-arms the break timer; `ShouldCoalesceEyeRestIntoBreak` (`Coordination.cs:51`, true only when the break timer is running) then **skips the eye-rest reminder** — user gets neither.

**Fix:** Centralize break-timer starts behind a single `StartBreakTimerIfEnabled()` gated helper applied to all start sites; add a defensive `if (!IsBreakEnabled) return false;` to `ShouldCoalesceEyeRestIntoBreak`.

### M2 — `IsBreakEnabled` gate has no cross-thread publication  ⚠️ LOW-RISK (benign in practice)
_Reporter: all three. File: `TimerService.cs:37`._

`IsBreakEnabled` reads the mutable `_configuration` while `UpdateConfiguration` reassigns it. Internal notes timer ticks + config-save both run on the UI thread in practice, so it's benign; externals want `Volatile.Read`/snapshot. **Fix (consistency):** publish a `volatile bool _isBreakEnabled` updated in `StartAsync`/`UpdateConfiguration`, mirroring the recent "volatile presence" hardening.

### M3 — Tests miss the failing transitions; global parallel-disable masks static-state smell
_Reporter: all three. Files: `TimerServiceBreakEnabledTests.cs`; `xunit.runner.json`._

Tests cover only startup/direct-warning/manual/active-toggle. `parallelizeTestCollections: false` serializes the whole assembly (slower CI) and papers over process-wide **static** processing guards. **Fix:** use a narrowly-scoped `[Collection]` for the racy timer classes instead of the assembly-wide switch; add regression tests for: disable-during-warning (H2), long-run health-monitor no-reset (H1), gated `OnBreakTimerTick` self-stop, disable-then-manual, re-enable-while-smart-paused. Consider making the static guards instance-scoped as a follow-up.

### M4 — Fire-and-forget `ResetBreakTimer()` rethrows into a discarded Task
_Reporter: Internal. Files: `TimerService.cs` re-enable path; `Lifecycle.cs:261`._ `_ = ResetBreakTimer();` where `ResetBreakTimer` rethrows → unobserved task exception. **Fix:** don't rethrow in a fire-and-forget path (or await on the UI thread).

### Low / Nit
- **L1** UI "Off" status is ~1 s late and shows "Paused"/meeting text (not "Off") in paused states — `BreakEnabled` setter doesn't notify countdown properties, and the "Off" override is only in the running-not-paused branch. **Fix:** notify countdown properties on toggle; apply the disabled presentation before the pause-specific display.
- **L2** `config.Break.Enabled` deref without `?.` in `UpdateConfiguration` (robustness nit; consistent with existing usage).
- **L3** Reflection-by-name static reset in tests silently no-ops if a field is renamed.

## Agreed-good
- Manual "Break Now" bypass is correct; `CanTriggerImmediateBreak` correctly stays independent of `BreakEnabled` (confirmed by codex + internal).
- The three visible-break gates are sound.

## Verdict
**Request changes.** Must-fix before merge: **H1** and **H2** (automatic eye-rest regressions), plus **M1** (root cause of the coalesce-skip). M2–M4 and Low items are strongly recommended.
