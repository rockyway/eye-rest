# 003 — Immediate Break Button

| Field | Value |
|-------|-------|
| **Status** | Design approved |
| **Created** | 2026-04-29 |
| **Owner** | tamtran |
| **Related** | `docs/features/001-break-done-screen-enhancements.md` |

## Goal

Let the user trigger a break immediately from the main window, using the configured `BreakDurationMinutes`. The break must behave **identically** to a timer-triggered break: same popup, same analytics, same fresh-session reset on completion.

## Non-Goals

- Configurable durations per click. The button uses the existing `BreakDurationMinutes` setting.
- Changes to the eye-rest timer. Only the break flow is exposed.
- A test/preview mode. Test buttons already exist on the Time Break tab.
- Migrating the trigger source for *historical* break rows. Only newly recorded breaks carry the source; older rows display as "Auto" by default (see Schema Migration below).

## User Story

> As a user, when I'm about to step away from my desk, I want a one-click way to start my break now (instead of waiting for the timer or stopping/restarting), so I don't lose my session statistics or have to manually reset timers afterwards.

## Architecture

### Component Diagram

```
MainWindowViewModel
    └─► TriggerImmediateBreakCommand
            └─► ITimerService.TriggerImmediateBreakAsync()             ← new public method
                    └─► TimerService.TriggerBreak(source: Manual)      ← existing method, gains source param
                            └─► BreakDue event (TimerEventArgs.Source = Manual)
                                    └─► ApplicationOrchestrator.OnBreakDue
                                            ├─► NotificationService.ShowBreakReminderAsync
                                            ├─► AnalyticsService.RecordBreakEventAsync(..., source)
                                            │       └─► RestEvents.TriggerSource column
                                            └─► TimerService.SmartSessionResetAsync
```

The new code is **one method + one command + UI changes**. All downstream behavior — popup, analytics, session reset — is reused from the existing timer-driven path.

### Why this path

The codebase already has a complete, tested flow for "break starts → user acts → fresh session." That flow is keyed off the `BreakDue` event. Reusing it guarantees the manual button cannot drift from auto-trigger behavior.

The alternative — shipping the popup directly from the ViewModel via `ShowBreakReminderTestAsync` — was rejected because that path bypasses analytics and session reset by design (it's a test/preview path).

## Detailed Changes

### 0. New enum — `BreakTriggerSource`

**File:** `EyeRest.Abstractions/Models/AnalyticsEnums.cs`

```csharp
public enum BreakTriggerSource
{
    Automatic, // Fired by the break timer (default)
    Manual     // Fired by the user clicking "Break Now"
}
```

This enum is propagated end-to-end so analytics queries can filter by source. `Automatic` is the default for back-compat with rows recorded before the feature shipped.

### 0a. `TimerEventArgs` — add source

**File:** `EyeRest.Abstractions/Services/ITimerService.cs`

```csharp
public class TimerEventArgs : EventArgs
{
    public DateTime TriggeredAt { get; set; }
    public TimeSpan NextInterval { get; set; }
    public TimerType Type { get; set; }
    public BreakTriggerSource Source { get; set; } = BreakTriggerSource.Automatic; // NEW
}
```

The default of `Automatic` keeps every existing call site (timer-tick, recovery, sleep-resume) correct without modification — only the manual path explicitly sets `Manual`.

### 1. `ITimerService` — new method

**File:** `EyeRest.Abstractions/Services/ITimerService.cs`

```csharp
/// Triggers a break immediately, as if the break timer had fired.
/// Fires <see cref="BreakDue"/> so the orchestrator handles the full lifecycle
/// (popup, analytics, session reset). Bypasses pause-state guards because the
/// user has explicitly requested the break.
Task TriggerImmediateBreakAsync();
```

### 2. `TimerService` — implementation

**File:** `EyeRest.Core/Services/Timer/TimerService.EventHandlers.cs` (or a new partial — see Decisions below)

```csharp
public Task TriggerImmediateBreakAsync()
{
    _logger.LogInformation("☕ Manual break requested by user");

    if (!IsRunning)
    {
        _logger.LogInformation("☕ Manual break ignored — timer service is not running");
        return Task.CompletedTask;
    }

    if (_isBreakNotificationActive)
    {
        _logger.LogInformation("☕ Manual break ignored — break popup already active");
        return Task.CompletedTask;
    }

    // Marshal to UI thread; TriggerBreak touches DispatcherTimer state.
    _dispatcherService.BeginInvoke(TriggerBreak);
    return Task.CompletedTask;
}
```

**Notes:**
- The existing `TriggerBreak()` already has the `Interlocked.CompareExchange` atomic guard against duplicates and the per-trigger setup (pause eye-rest timers, stop warning timers, set `_isBreakNotificationActive`).
- `TriggerBreak()` gains an optional `BreakTriggerSource source = BreakTriggerSource.Automatic` parameter. The auto-call sites stay unchanged (they get the default). The manual wrapper passes `BreakTriggerSource.Manual`. The constructed `TimerEventArgs.Source` carries this through to `OnBreakDue`.
- The pause guard inside `TriggerBreak()` (`IsPaused || IsManuallyPaused || IsSmartPaused`) **will block manual triggers**. We pair the source param with a `bool ignorePauseGuard` derived from `source == BreakTriggerSource.Manual` so the manual path skips the guard automatically.

### 3. `MainWindowViewModel` — new command

**File:** `EyeRest.UI/ViewModels/MainWindowViewModel.cs`

- Add field: `public ICommand TriggerImmediateBreakCommand { get; }`
- Initialize in constructor next to other commands:
  ```csharp
  TriggerImmediateBreakCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(
      async () => await TriggerImmediateBreakAsync(),
      () => CanTriggerImmediateBreak);
  ```
- Add property: `public bool CanTriggerImmediateBreak => _timerService.IsRunning && !_timerService.IsAnyNotificationActive;`
- Add method:
  ```csharp
  private async Task TriggerImmediateBreakAsync()
  {
      try
      {
          _logger.LogInformation("User clicked Break Now");
          await _timerService.TriggerImmediateBreakAsync();
      }
      catch (Exception ex)
      {
          _logger.LogError(ex, "Failed to trigger immediate break");
      }
  }
  ```
- Wire `RaiseCanExecuteChanged()` for the new command anywhere `StartTimersCommand`/`PauseTimersCommand` already raise theirs (timer state-change handlers).

### 3a. Analytics — `IAnalyticsService` + `AnalyticsService`

**File:** `EyeRest.Abstractions/Services/IAnalyticsService.cs`

`RecordBreakEventAsync` gains an optional source parameter:

```csharp
Task RecordBreakEventAsync(
    RestEventType type,
    UserAction action,
    TimeSpan duration,
    BreakTriggerSource source = BreakTriggerSource.Automatic);
```

**File:** `EyeRest.Core/Services/AnalyticsService.cs`

The existing implementation delegates to `RecordEyeRestEventAsync`. Replace this with a dedicated implementation that writes the new `TriggerSource` column on the `RestEvents` insert (the eye-rest path keeps a default of `Automatic`). The SQL becomes:

```sql
INSERT INTO RestEvents (EventType, TriggeredAt, UserAction, Duration, ConfiguredDuration, TriggerSource)
VALUES (@eventType, @triggeredAt, @action, @duration, @configuredDuration, @triggerSource)
```

### 3b. Schema migration

**File:** `EyeRest.Core/Services/AnalyticsService.cs` (`InitializeDatabaseAsync` / table creation block at line ~122)

The `CREATE TABLE IF NOT EXISTS RestEvents` statement gains a `TriggerSource TEXT NOT NULL DEFAULT 'Automatic'` column:

```sql
CREATE TABLE IF NOT EXISTS RestEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventType TEXT NOT NULL,
    TriggeredAt DATETIME NOT NULL,
    UserAction TEXT NOT NULL,
    Duration INTEGER,
    ConfiguredDuration INTEGER,
    SessionId INTEGER,
    TriggerSource TEXT NOT NULL DEFAULT 'Automatic',
    FOREIGN KEY (SessionId) REFERENCES UserSessions (Id)
)
```

For users with an existing database, add a migration step right after `CREATE TABLE` that runs:

```sql
ALTER TABLE RestEvents ADD COLUMN TriggerSource TEXT NOT NULL DEFAULT 'Automatic';
```

…wrapped in a `PRAGMA table_info(RestEvents)` check so the migration is idempotent. (SQLite raises an error if the column already exists.) Existing rows backfill to `'Automatic'`, which is the correct historical default.

### 3c. `OnBreakDue` — propagate source to analytics

**File:** `EyeRest.Core/Services/ApplicationOrchestrator.cs` (`OnBreakDue`, line 381)

Read `e.Source` from the event args and pass it through to every `RecordBreakEventAsync` call (Completed, ConfirmedAfterCompletion, CompletedWithoutConfirmation paths) and to the `BreakShown` `EventHistory` metadata:

```csharp
await _analyticsService.RecordEventAsync(
    EventHistoryType.BreakShown,
    "Break popup shown",
    new Dictionary<string, object?> { ["triggerSource"] = e.Source.ToString() });

// later, in each result branch:
await _analyticsService.RecordBreakEventAsync(
    RestEventType.Break, UserAction.Completed, duration, e.Source);
```

The `BreakSkipped` / `BreakDelayed` / `BreakCompleted` event-history entries also gain the `triggerSource` metadata key for consistency in the Event History tab.

### 3d. Dashboard surfacing

**File:** `EyeRest.UI/ViewModels/AnalyticsDashboardViewModel.cs` and the analytics view that renders break stats.

Existing summary cards (Compliance Rate, Breaks Completed, Breaks Skipped, etc.) stay unchanged — they aggregate across both sources. **Two additions**:

1. A new pair of small KPI fields below "Breaks Completed":
   `Auto: 18 · Manual: 5` (counts within the selected period).
2. The Event History list (rendered via `EventHistoryEntry`) gets a small badge / suffix when `metadata["triggerSource"] == "Manual"` so users can scan their history and tell which breaks they took on demand. Suggested format: `"Break (Manual)"` vs `"Break"`.

To support (1), `AnalyticsService` exposes a new query method:

```csharp
Task<(int Automatic, int Manual)> GetBreakCountsBySourceAsync(int days);
```

Implementation: `SELECT TriggerSource, COUNT(*) FROM RestEvents WHERE EventType='Break' AND TriggeredAt >= @cutoff GROUP BY TriggerSource`.

### 4. UI changes — `MainWindow.axaml`

Two locations need the same change: **Simple Mode** (currently around lines 256–265) and **Config Mode sidebar** (currently around lines 525–533).

Replace the full-width Start button with a 2-column grid:

```xml
<!-- Start + Break Now row -->
<Grid ColumnDefinitions="*,4,*" Margin="8,0">
    <Button Grid.Column="0" Classes="GradientStart"
            Command="{Binding StartTimersCommand}"
            ToolTip.Tip="{Binding StartButtonTooltip}"
            Height="42">
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center">
            <PathIcon Data="{StaticResource PlayIcon}" Width="14" Height="14" Foreground="White" />
            <TextBlock Text="Start" Foreground="White" FontSize="14" />
        </StackPanel>
    </Button>

    <Button Grid.Column="2" Classes="GradientBreakNow"
            Command="{Binding TriggerImmediateBreakCommand}"
            IsEnabled="{Binding CanTriggerImmediateBreak}"
            ToolTip.Tip="Take a break now (uses configured break duration)"
            Height="42">
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Center">
            <PathIcon Data="{StaticResource ClockIcon}" Width="14" Height="14" Foreground="White" />
            <TextBlock Text="Break Now" Foreground="White" FontSize="14" />
        </StackPanel>
    </Button>
</Grid>
```

The Config Mode sidebar variant uses the same structure but inherits its sibling buttons' `Margin="4,0"` and lacks the explicit `Height="42"` (matching its current Start button).

### 5. New style — `GradientBreakNow`

**File:** `EyeRest.UI/Resources/GlassStyles.axaml` (alongside `Button.GradientStart` at line 141, `Button.GradientStop` at line 169, `Button.GradientPause` at line 194).

A new gradient using the existing `AccentBrush` palette (the blue used by the Break timer card) so "Break Now" reads as a break action rather than a start action. Mirror the four-selector pattern of `GradientStart` (base, content presenter, `:pointerover`, `:pressed`).

## Behavior Specification

| Scenario | Expected behavior |
|----------|-------------------|
| Timers running, idle (no popup), user clicks **Break Now** | Break popup opens immediately with full configured duration; on completion, `SmartSessionResetAsync` runs |
| Timers stopped | Button disabled |
| Eye-rest popup currently visible | Button disabled |
| Break popup already visible | Button disabled (`IsAnyNotificationActive == true`) |
| Timers manually paused (Meeting 30m / 1h) | Button enabled; clicking it triggers the break (intentional override of pause) |
| Timers smart-paused (idle / screen lock) | Button enabled; clicking forces the break |
| User clicks Break Now mid-warning countdown | Warning popup is closed by `TriggerBreak()` and break popup opens (existing `TriggerBreak()` behavior) |
| Break completes / skipped / confirmed | Identical post-conditions to auto-triggered break (analytics recorded, fresh session) — **but** the `RestEvents.TriggerSource` column is `'Manual'` and the Event History entry shows the manual badge |
| Auto break fires | Behavior unchanged; `TriggerSource = 'Automatic'` written to DB |
| Existing DB rows on first launch with new schema | Backfill to `'Automatic'` via the column default; dashboard counts them as auto |

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Bypassing the pause guard introduces a state inconsistency | The atomic `Interlocked.CompareExchange` lock + `_isBreakNotificationActive` already guard duplicate fires. Pre-checking `IsRunning` & `IsAnyNotificationActive` in the wrapper covers the only intentional bypass — paused state — which is the desired behavior for an explicit user action. |
| Analytics double-counts a break | `OnBreakDue` records exactly once on the user-action result. The new path uses the same handler, so no risk of double-recording. |
| Smart-pause cycles after manual trigger restore stale interval (per the 2026-04-28 lessons learned) | The `SmartSessionResetAsync` call inside `OnBreakDue` already reapplies the configured intervals. The fix from `a84afd2` ensures the timer interval is refreshed on resume. No new exposure. |
| Button position confuses users (Start should not initiate a break) | Different gradient color (`AccentBrush` blue vs `GradientStart` green), distinct icon (clock vs play), and tooltip make the affordance unambiguous. |
| Race: user clicks Break Now while a backup-trigger is firing | `TriggerBreak()` uses `Interlocked.CompareExchange` to ensure only one wins. The other returns early. |

## Test Plan

### Unit tests (xUnit, Avalonia test project)

Add to a new file `EyeRest.Tests.Avalonia/Services/TimerServiceImmediateBreakTests.cs`:

1. `TriggerImmediateBreakAsync_WhenNotRunning_DoesNothing` — service is stopped, calling the method does not fire `BreakDue`.
2. `TriggerImmediateBreakAsync_WhenRunning_FiresBreakDue` — running service fires the event with `Type == TimerType.Break` and the duration from config.
3. `TriggerImmediateBreakAsync_WhenBreakNotificationActive_DoesNothing` — flag set, no event fires.
4. `TriggerImmediateBreakAsync_WhenManuallyPaused_StillFiresBreakDue` — verifies the pause-guard bypass for explicit user action.
5. `TriggerImmediateBreakAsync_WhenAlreadyProcessing_AtomicGuardPreventsDoubleFire` — concurrent calls fire the event only once.
6. `TriggerImmediateBreakAsync_FiresBreakDueWithManualSource` — `TimerEventArgs.Source == BreakTriggerSource.Manual`.
7. `AutoTimerTick_FiresBreakDueWithAutomaticSource` — regression: existing auto-fire path keeps `BreakTriggerSource.Automatic`.

For `AnalyticsServiceTests`:

8. `RecordBreakEventAsync_WithManualSource_PersistsManualInTriggerSourceColumn`.
9. `RecordBreakEventAsync_WithoutSource_DefaultsToAutomatic` — back-compat for callers not passing the parameter.
10. `GetBreakCountsBySourceAsync_ReturnsSeparateCountsForAutoAndManual`.
11. `InitializeDatabase_OnExistingTableWithoutTriggerSourceColumn_AddsColumnIdempotently` — migration runs cleanly twice, existing rows backfill to `'Automatic'`.

Add to `MainWindowViewModelTests`:

6. `TriggerImmediateBreakCommand_WhenTimerStopped_CannotExecute`.
7. `TriggerImmediateBreakCommand_WhenTimerRunning_CanExecute`.
8. `TriggerImmediateBreakCommand_WhenNotificationActive_CannotExecute`.

### Manual smoke test

1. Launch app, click Start, wait 5s, click **Break Now** → break popup opens with configured duration.
2. Click "Done" on break popup → all timers reset, status pill shows running, eye-rest and break countdowns reset to configured intervals.
3. Click "Skip" on break popup → same fresh session reset.
4. Click Start, click Pause, click Break Now → break popup opens (pause bypass).
5. Click Start, click "Meet 30m", click Break Now → break popup opens.
6. Click Start, wait until eye-rest popup appears, click Break Now (if visible behind it) → button is disabled.
7. Click Start → click Stop → button is disabled.
8. Open the SQLite DB (`%APPDATA%\EyeRest\analytics.db` or macOS equivalent), run `SELECT TriggerSource, COUNT(*) FROM RestEvents GROUP BY TriggerSource;` → confirm both `Automatic` and `Manual` rows exist after a mixed-trigger session.
9. Open Analytics Dashboard → confirm the new "Auto / Manual" KPI fields show non-zero values, and the Event History list visibly distinguishes a manual break from an auto break.

### Visual / regression

- Verify both Simple Mode and Config Mode sidebar render the new 2-column row without overflow at 340px window width.
- Verify dark theme and light theme both render the `GradientBreakNow` style correctly.
- Verify tooltip text is readable.

## Decision

`TriggerImmediateBreakAsync` lives in `TimerService.EventHandlers.cs` next to the existing `TriggerBreak()`. Creating a new partial for a single thin wrapper would be over-engineered.

## Out of Scope

- Tray menu entry for "Take Break Now" (could be a follow-up).
- Keyboard shortcut.
- Configurable durations (e.g., "Quick 2-minute break").
- Charting manual-break trends over time (only the period summary count + history badge are in scope).
- CSV / HTML / JSON exporters surfacing `TriggerSource`. The column will be present in raw SQLite, but updating each export format is a follow-up.
