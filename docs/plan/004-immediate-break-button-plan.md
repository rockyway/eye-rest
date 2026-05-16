# Immediate Break Button — Implementation Plan

> **Spec:** `docs/plan/003-immediate-break-button.md`
> **Execution mode:** Inline (no subagents needed — change is mechanical, single feature, single language)

**Goal:** Add a "Break Now" button to the main window that triggers an immediate break with the same lifecycle as a timer-fired break, while persisting the trigger source (Manual vs. Automatic) for analytics.

**Architecture:** A new `BreakTriggerSource` enum flows from `MainWindowViewModel` → `ITimerService.TriggerImmediateBreakAsync` → existing `TriggerBreak()` → `BreakDue` event (with `Source` on `TimerEventArgs`) → `OnBreakDue` → analytics. The DB schema gains a backwards-compatible `TriggerSource` column. The Start button is halved and the new button takes the other half on both Simple Mode and Config-Mode sidebar.

**Tech Stack:** C# 12, .NET 8, Avalonia 11, xUnit 2.6, SQLite, Microsoft.Data.Sqlite.

---

## Task 1 — Foundation: enums and interface signatures

**Files:**
- Modify: `EyeRest.Abstractions/Models/AnalyticsEnums.cs`
- Modify: `EyeRest.Abstractions/Services/ITimerService.cs`
- Modify: `EyeRest.Abstractions/Services/IAnalyticsService.cs`

- [ ] **Step 1.1:** Add `BreakTriggerSource` enum to `AnalyticsEnums.cs` (after `UserAction`):
  ```csharp
  public enum BreakTriggerSource
  {
      Automatic,
      Manual
  }
  ```
- [ ] **Step 1.2:** Add `Source` property to `TimerEventArgs` (default `Automatic`).
- [ ] **Step 1.3:** Add `Task TriggerImmediateBreakAsync();` to `ITimerService`.
- [ ] **Step 1.4:** Change `IAnalyticsService.RecordBreakEventAsync` signature to add `BreakTriggerSource source = BreakTriggerSource.Automatic`.
- [ ] **Step 1.5:** Add `Task<(int Automatic, int Manual)> GetBreakCountsBySourceAsync(int days);` to `IAnalyticsService`.
- [ ] **Step 1.6:** `dotnet build` — expected: build fails because TimerService and AnalyticsService don't yet implement the new members. (We'll fix in subsequent tasks; using a single commit at the end of Task 4 once compile is green.)

---

## Task 2 — AnalyticsService: schema migration + write source + new query

**Files:**
- Modify: `EyeRest.Core/Services/AnalyticsService.cs`

- [ ] **Step 2.1:** Update `CREATE TABLE IF NOT EXISTS RestEvents` to include `TriggerSource TEXT NOT NULL DEFAULT 'Automatic'`.
- [ ] **Step 2.2:** Right after the `CREATE TABLE` runs, add an idempotent migration block:
  ```csharp
  command.CommandText = "PRAGMA table_info(RestEvents)";
  bool hasTriggerSource = false;
  using (var reader = command.ExecuteReader())
  {
      while (reader.Read())
      {
          if (string.Equals(reader.GetString(1), "TriggerSource", StringComparison.OrdinalIgnoreCase))
          {
              hasTriggerSource = true;
              break;
          }
      }
  }
  if (!hasTriggerSource)
  {
      command.CommandText = "ALTER TABLE RestEvents ADD COLUMN TriggerSource TEXT NOT NULL DEFAULT 'Automatic'";
      command.ExecuteNonQuery();
      _logger.LogInformation("📊 Migrated RestEvents: added TriggerSource column");
  }
  ```
- [ ] **Step 2.3:** Replace the body of `RecordBreakEventAsync` (currently just delegates to `RecordEyeRestEventAsync`) with an implementation that writes the new column. Match the existing INSERT pattern at line 313 but add `TriggerSource`:
  ```csharp
  public async Task RecordBreakEventAsync(
      RestEventType type, UserAction action, TimeSpan duration,
      BreakTriggerSource source = BreakTriggerSource.Automatic)
  {
      try
      {
          using var connection = new SqliteConnection(_connectionString);
          await connection.OpenAsync();
          using var command = connection.CreateCommand();
          command.CommandText = @"
              INSERT INTO RestEvents
                (EventType, TriggeredAt, UserAction, Duration, ConfiguredDuration, TriggerSource)
              VALUES
                (@eventType, @triggeredAt, @action, @duration, @configuredDuration, @triggerSource)";
          command.Parameters.AddWithValue("@eventType", type.ToString());
          command.Parameters.AddWithValue("@triggeredAt", DateTime.Now);
          command.Parameters.AddWithValue("@action", action.ToString());
          command.Parameters.AddWithValue("@duration", (long)duration.TotalMilliseconds);
          command.Parameters.AddWithValue("@configuredDuration", type == RestEventType.EyeRest ? 20000 : 5000);
          command.Parameters.AddWithValue("@triggerSource", source.ToString());
          await command.ExecuteNonQueryAsync();
          _logger.LogDebug($"📊 Recorded {type} event: {action} ({source}), Duration: {duration.TotalSeconds}s");
      }
      catch (Exception ex)
      {
          _logger.LogError(ex, "Error recording break event");
      }
  }
  ```
- [ ] **Step 2.4:** Add `GetBreakCountsBySourceAsync`:
  ```csharp
  public async Task<(int Automatic, int Manual)> GetBreakCountsBySourceAsync(int days)
  {
      try
      {
          var cutoff = DateTime.Now.AddDays(-Math.Max(days, 0));
          int autoCount = 0, manualCount = 0;
          using var connection = new SqliteConnection(_connectionString);
          await connection.OpenAsync();
          using var command = connection.CreateCommand();
          command.CommandText = @"
              SELECT TriggerSource, COUNT(*) FROM RestEvents
              WHERE EventType = 'Break' AND TriggeredAt >= @cutoff
              GROUP BY TriggerSource";
          command.Parameters.AddWithValue("@cutoff", cutoff);
          using var reader = await command.ExecuteReaderAsync();
          while (await reader.ReadAsync())
          {
              var src = reader.GetString(0);
              var count = reader.GetInt32(1);
              if (string.Equals(src, "Manual", StringComparison.OrdinalIgnoreCase)) manualCount = count;
              else autoCount += count;
          }
          return (autoCount, manualCount);
      }
      catch (Exception ex)
      {
          _logger.LogError(ex, "Error querying break counts by source");
          return (0, 0);
      }
  }
  ```

---

## Task 3 — TimerService: source param + immediate trigger

**Files:**
- Modify: `EyeRest.Core/Services/Timer/TimerService.EventHandlers.cs`

- [ ] **Step 3.1:** Change `private void TriggerBreak()` signature to `private void TriggerBreak(BreakTriggerSource source = BreakTriggerSource.Automatic)`.
- [ ] **Step 3.2:** Replace the pause guard at the top with:
  ```csharp
  bool ignorePauseGuard = source == BreakTriggerSource.Manual;
  if (!ignorePauseGuard && (IsPaused || IsManuallyPaused || IsSmartPaused))
  {
      _logger.LogInformation("☕ TriggerBreak blocked — service is paused (Manual={Manual}, Smart={Smart}, Paused={Paused})",
          IsManuallyPaused, IsSmartPaused, IsPaused);
      return;
  }
  ```
- [ ] **Step 3.3:** Set `Source = source` on the `TimerEventArgs` before invoking `BreakDue`.
- [ ] **Step 3.4:** Add the public wrapper directly above `TriggerBreak`:
  ```csharp
  public Task TriggerImmediateBreakAsync()
  {
      _logger.LogInformation("☕ Manual break requested by user");
      if (!IsRunning)
      {
          _logger.LogInformation("☕ Manual break ignored — timer service is not running");
          return Task.CompletedTask;
      }
      if (IsAnyNotificationActive)
      {
          _logger.LogInformation("☕ Manual break ignored — popup already active");
          return Task.CompletedTask;
      }
      _dispatcherService.BeginInvoke(() => TriggerBreak(BreakTriggerSource.Manual));
      return Task.CompletedTask;
  }
  ```

---

## Task 4 — Orchestrator: propagate source to analytics

**Files:**
- Modify: `EyeRest.Core/Services/ApplicationOrchestrator.cs` (`OnBreakDue`, line 381)

- [ ] **Step 4.1:** Replace each `RecordBreakEventAsync(RestEventType.Break, ..., duration)` call with `RecordBreakEventAsync(RestEventType.Break, ..., duration, e.Source)`.
- [ ] **Step 4.2:** Update the `EventHistoryType.BreakShown`, `BreakCompleted`, `BreakSkipped`, `BreakDelayed` calls so each metadata bag includes `["triggerSource"] = e.Source.ToString()`. For `BreakShown` (currently no metadata):
  ```csharp
  await _analyticsService.RecordEventAsync(
      EventHistoryType.BreakShown, "Break popup shown",
      new Dictionary<string, object?> { ["triggerSource"] = e.Source.ToString() });
  ```
  For existing metadata bags, add the key alongside the existing entries (e.g., `BreakDelayed` already has `delayMinutes`).
- [ ] **Step 4.3:** `dotnet build` — expected: PASS. Commit checkpoint.
  ```bash
  git add EyeRest.Abstractions EyeRest.Core
  git commit -m "feat: track break trigger source (manual vs automatic) end-to-end"
  ```

---

## Task 5 — ViewModel: TriggerImmediateBreakCommand

**Files:**
- Modify: `EyeRest.UI/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 5.1:** Declare command property near the other commands (e.g., near line 1063): `public ICommand TriggerImmediateBreakCommand { get; }`.
- [ ] **Step 5.2:** Add backing computed property:
  ```csharp
  public bool CanTriggerImmediateBreak => _timerService.IsRunning && !_timerService.IsAnyNotificationActive;
  ```
- [ ] **Step 5.3:** Initialize command in the constructor next to `StartTimersCommand`:
  ```csharp
  TriggerImmediateBreakCommand = new EyeRest.ViewModels.CrossPlatformRelayCommand(
      async () => await TriggerImmediateBreakAsync(),
      () => CanTriggerImmediateBreak);
  ```
- [ ] **Step 5.4:** Add the method (after the `TestBreakPopup` method around line 1937):
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
- [ ] **Step 5.5:** Wherever `(StartTimersCommand as ...)?.RaiseCanExecuteChanged()` is called (line 1552 and any timer-state-change handlers), add the same call for `TriggerImmediateBreakCommand`. Also add `OnPropertyChanged(nameof(CanTriggerImmediateBreak));` in those same spots so the XAML `IsEnabled` binding refreshes.

---

## Task 6 — UI styles: GradientBreakNow

**Files:**
- Modify: `EyeRest.UI/Resources/GlassStyles.axaml`

- [ ] **Step 6.1:** After the `GradientStart:pressed` selector (around line 167), add four selectors mirroring `GradientStart` but using `{DynamicResource AccentBrush}` for both `Background` setters:
  ```xml
  <Style Selector="Button.GradientBreakNow">
      <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
      <Setter Property="Foreground" Value="White" />
      <Setter Property="CornerRadius" Value="12" />
      <Setter Property="Height" Value="44" />
      <Setter Property="FontWeight" Value="SemiBold" />
      <Setter Property="FontSize" Value="14" />
      <Setter Property="Cursor" Value="Hand" />
      <Setter Property="HorizontalContentAlignment" Value="Center" />
      <Setter Property="VerticalContentAlignment" Value="Center" />
      <Setter Property="HorizontalAlignment" Value="Stretch" />
      <Setter Property="Margin" Value="8,3" />
  </Style>
  <Style Selector="Button.GradientBreakNow /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
      <Setter Property="Foreground" Value="White" />
  </Style>
  <Style Selector="Button.GradientBreakNow:pointerover /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
      <Setter Property="Foreground" Value="White" />
      <Setter Property="Opacity" Value="0.9" />
  </Style>
  <Style Selector="Button.GradientBreakNow:pressed /template/ ContentPresenter#PART_ContentPresenter">
      <Setter Property="Background" Value="{DynamicResource AccentBrush}" />
      <Setter Property="Foreground" Value="White" />
      <Setter Property="Opacity" Value="0.8" />
  </Style>
  ```

---

## Task 7 — UI XAML: Simple Mode + Config Mode sidebar

**Files:**
- Modify: `EyeRest.UI/Views/MainWindow.axaml`

- [ ] **Step 7.1:** **Simple Mode** (around lines 256–265). Replace the full-width `<Button Classes="GradientStart">` with:
  ```xml
  <Grid ColumnDefinitions="*,4,*" Margin="8,0">
      <Button Grid.Column="0" Classes="GradientStart"
              Command="{Binding StartTimersCommand}"
              ToolTip.Tip="{Binding StartButtonTooltip}"
              Height="42" Margin="0">
          <StackPanel Orientation="Horizontal" Spacing="6" HorizontalAlignment="Center">
              <PathIcon Data="{StaticResource PlayIcon}" Width="14" Height="14" Foreground="White" />
              <TextBlock Text="Start" Foreground="White" FontSize="14" />
          </StackPanel>
      </Button>
      <Button Grid.Column="2" Classes="GradientBreakNow"
              Command="{Binding TriggerImmediateBreakCommand}"
              IsEnabled="{Binding CanTriggerImmediateBreak}"
              ToolTip.Tip="Take a break now (uses configured break duration)"
              Height="42" Margin="0">
          <StackPanel Orientation="Horizontal" Spacing="6" HorizontalAlignment="Center">
              <PathIcon Data="{StaticResource ClockIcon}" Width="14" Height="14" Foreground="White" />
              <TextBlock Text="Break Now" Foreground="White" FontSize="14" />
          </StackPanel>
      </Button>
  </Grid>
  ```
  Note: explicit `Margin="0"` on each child overrides the `GradientStart`/`GradientBreakNow` `Margin="8,3"` so the grid controls spacing.
- [ ] **Step 7.2:** **Config Mode sidebar** (around lines 525–533). Apply the same replacement, but inside the existing sidebar `Margin="4,0"` context (use `Margin="0"` on children).

---

## Task 8 — Analytics dashboard: Auto/Manual counts

**Files:**
- Modify: `EyeRest.UI/ViewModels/AnalyticsDashboardViewModel.cs`
- Modify: `EyeRest.UI/Views/AnalyticsDashboardView.axaml` (or wherever break stats render)

- [ ] **Step 8.1:** In `AnalyticsDashboardViewModel`, add two backed properties: `BreaksAutoCount` and `BreaksManualCount` (with `OnPropertyChanged`).
- [ ] **Step 8.2:** In whichever method loads the period summary (search for the existing `AnalyticsTotalBreaksCompleted` load site), add a call:
  ```csharp
  var (autoCount, manualCount) = await _analyticsService.GetBreakCountsBySourceAsync(_analyticsPeriodDays);
  BreaksAutoCount = autoCount.ToString();
  BreaksManualCount = manualCount.ToString();
  ```
- [ ] **Step 8.3:** In the Analytics tab of `MainWindow.axaml` (around line 1322 — "Breaks Completed Card"), add a small text row directly below the count showing "Auto: {value} · Manual: {value}":
  ```xml
  <TextBlock Text="{Binding BreaksAutoManualSummary}" FontSize="10"
             Foreground="{DynamicResource TextSecondaryBrush}"
             HorizontalAlignment="Center" Margin="0,2,0,0" />
  ```
  (Add a derived property `BreaksAutoManualSummary => $"Auto: {BreaksAutoCount} · Manual: {BreaksManualCount}";` to MainWindowViewModel which holds the analytics summary fields.)

> **Decision:** these summary fields already live on `MainWindowViewModel`, not `AnalyticsDashboardViewModel` (per the existing `AnalyticsTotalBreaksCompleted` etc.). Add the new fields to `MainWindowViewModel` next to the existing analytics fields. The full `AnalyticsDashboardViewModel` (used by the larger window) can defer this enhancement.

---

## Task 9 — Event History: Manual badge

**Files:**
- Modify: `EyeRest.Abstractions/Models/EventHistoryEntry.cs`

- [ ] **Step 9.1:** Modify `EventTypeText` to suffix `(Manual)` when metadata indicates a manual break:
  ```csharp
  public string EventTypeText
  {
      get
      {
          var label = FormatEventType(EventType);
          if (Metadata.TryGetValue("triggerSource", out var src)
              && src is string s
              && string.Equals(s, "Manual", StringComparison.OrdinalIgnoreCase)
              && (EventType == EventHistoryType.BreakShown
                  || EventType == EventHistoryType.BreakCompleted
                  || EventType == EventHistoryType.BreakSkipped
                  || EventType == EventHistoryType.BreakDelayed))
          {
              return $"{label} (Manual)";
          }
          return label;
      }
  }
  ```

---

## Task 10 — Tests

**Files:**
- Create: `EyeRest.Tests.Avalonia/Services/TimerServiceImmediateBreakTests.cs`
- Modify: existing analytics tests (or create `AnalyticsServiceTriggerSourceTests.cs`)
- Modify: `EyeRest.Tests.Avalonia/ViewModels/MainWindowViewModelTests.cs`

- [ ] **Step 10.1:** Add `TimerServiceImmediateBreakTests` covering:
  - `TriggerImmediateBreakAsync_WhenNotRunning_DoesNothing`
  - `TriggerImmediateBreakAsync_WhenRunning_FiresBreakDueWithManualSource`
  - `TriggerImmediateBreakAsync_WhenManuallyPaused_StillFires`
  - `TriggerImmediateBreakAsync_WhenNotificationActive_DoesNothing`
- [ ] **Step 10.2:** Add analytics tests for source persistence and `GetBreakCountsBySourceAsync`. Use a temp-file SQLite DB.
- [ ] **Step 10.3:** Add ViewModel tests:
  - `TriggerImmediateBreakCommand_WhenStopped_CannotExecute`
  - `TriggerImmediateBreakCommand_WhenRunning_CanExecute`
  - `TriggerImmediateBreakCommand_WhenNotificationActive_CannotExecute`

---

## Task 11 — Build + run + commit

- [ ] **Step 11.1:** `dotnet build` — expected: clean.
- [ ] **Step 11.2:** `dotnet test --filter Category=Unit` — expected: all pass (including new tests).
- [ ] **Step 11.3:** `dotnet run --project EyeRest.UI` — manually click Start, then Break Now; verify popup, completion, fresh session, and database row with `TriggerSource='Manual'`.
- [ ] **Step 11.4:** Final commit:
  ```bash
  git add -A
  git commit -m "feat: add 'Break Now' button with manual-vs-auto trigger tracking"
  ```

---

## Self-Review Notes

- Spec coverage: §0 (enum), §0a (TimerEventArgs), §1 (ITimerService), §2 (TimerService), §3 (ViewModel), §3a (IAnalyticsService), §3b (schema), §3c (orchestrator), §3d (dashboard), §4 (XAML), §5 (style) — all map to tasks 1–9.
- Tests cover unit cases from spec §"Test Plan".
- Manual smoke step is preserved in Task 11.
- No placeholders; every step has concrete code or commands.
