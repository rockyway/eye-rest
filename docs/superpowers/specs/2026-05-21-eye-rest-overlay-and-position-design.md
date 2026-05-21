# Eye Rest Overlay & Popup Position — Design Spec

**Date:** 2026-05-21
**Status:** Approved (brainstorming → implementation)
**Author:** Tam Tran (with Claude Code, Opus 4.7)

---

## 1. Goal

Enhance the Eye rest reminder experience with two new user-configurable settings, bringing parity with the existing Break popup behavior:

1. **Overlay** — a dimming overlay shown across all monitors while the eye rest popup is visible, with on/off toggle and 0–100% opacity slider.
2. **Popup Position** — let the user pick where the eye rest reminder popup appears on the active screen, from a set of 9 named positions (center, four corners, four edge-centers).

## 2. Non-Goals

- No changes to the **Break popup** position (stays centered) or its overlay (stays as-is).
- No changes to the **Eye Rest Warning** popup (stays at TopRight, acts as small unobtrusive heads-up).
- No arbitrary X/Y coordinate placement, no drag-to-set-position memory.
- No per-monitor overlay opacity — all dimmed screens share one opacity value.
- No 3x3 visual grid picker UI in this iteration (ComboBox suffices; grid picker is a possible follow-up).
- No new abstraction (`OverlaySettings` shared between EyeRest and Break) — kept separate per YAGNI.

## 3. Data Model

### 3.1 `EyeRestSettings` (`EyeRest.Abstractions/Models/AppConfiguration.cs`)

Three new fields added:

```csharp
public class EyeRestSettings
{
    public int IntervalMinutes { get; set; } = 20;
    public int DurationSeconds { get; set; } = 20;
    public AudioChannelConfig StartAudio { get; set; } = new();
    public AudioChannelConfig EndAudio { get; set; } = new();
    public bool WarningEnabled { get; set; } = true;
    public int WarningSeconds { get; set; } = 15;

    // NEW
    public bool OverlayEnabled { get; set; } = true;
    public int OverlayOpacityPercent { get; set; } = 50;      // 0..100
    public PopupPosition PopupPosition { get; set; } = PopupPosition.TopRight; // preserves current behavior
}
```

### 3.2 New enum `PopupPosition` (`EyeRest.Abstractions/Models/PopupPosition.cs`)

```csharp
public enum PopupPosition
{
    Center,
    TopLeft,
    TopCenter,
    TopRight,
    LeftCenter,
    RightCenter,
    BottomLeft,
    BottomCenter,
    BottomRight,
}
```

Lives in **Abstractions** (not the UI project) because the *setting* is a domain concept; the UI-layer `PopupPlacement` enum is the *implementation* of positioning math and stays in `EyeRest.UI`.

### 3.3 JSON serialization

`PopupPosition` serializes as **integer** to match the existing project convention. `ConfigurationService.s_jsonOptions` does NOT register `JsonStringEnumConverter` today; other enums in the config (`ThemeMode`, `MeetingDetectionMethod`) are also stored as integers. Out of scope to convert the project to string enums in this change.

Important consequence: if the user manually edits config.json with the wrong integer, `MapPlacement` / `ComputePosition` both fall back to `TopRight` for unknown values — safe.

### 3.4 Schema migration

No migration required. New fields receive their defaults when an older config without them is deserialized. `ConfigMetadata.SchemaVersion` is **not** bumped (it tracks BL-002 audio schema, unrelated).

### 3.5 Default values

| Field | Default | Rationale |
|---|---|---|
| `OverlayEnabled` | `true` | Most users want the dim cue (matches Break behavior) |
| `OverlayOpacityPercent` | `50` | User-confirmed |
| `PopupPosition` | `TopRight` | Preserves current hardcoded behavior — no surprise for existing users on upgrade |

## 4. Popup Positioning Logic + Overlay Wiring

### 4.1 Extend `PopupPlacement` enum (`EyeRest.UI/Views/PopupWindow.axaml.cs`)

```csharp
public enum PopupPlacement
{
    Center,
    TopLeft,
    TopCenter,
    TopRight,
    LeftCenter,
    RightCenter,
    BottomLeft,
    BottomCenter,
    BottomRight,
}
```

### 4.2 Extract `ComputePosition` helper

`PopupWindow.PositionOnScreen` (line 171) and `RepositionWithActualSize` (line 103) currently duplicate placement math. Going from 2 cases to 9 would multiply duplication. Extract a single helper:

```csharp
private static PixelPoint ComputePosition(
    PopupPlacement placement,
    PixelRect workArea,
    double scaling,
    int widthPx,
    int heightPx)
{
    var marginPx = (int)(8 * scaling);
    return placement switch
    {
        PopupPlacement.Center =>
            new PixelPoint(workArea.X + (workArea.Width - widthPx) / 2,
                           workArea.Y + (workArea.Height - heightPx) / 2),
        PopupPlacement.TopLeft =>
            new PixelPoint(workArea.X + marginPx, workArea.Y + marginPx),
        PopupPlacement.TopCenter =>
            new PixelPoint(workArea.X + (workArea.Width - widthPx) / 2,
                           workArea.Y + marginPx),
        PopupPlacement.TopRight =>
            new PixelPoint(workArea.Right - widthPx - marginPx,
                           workArea.Y + marginPx),
        PopupPlacement.LeftCenter =>
            new PixelPoint(workArea.X + marginPx,
                           workArea.Y + (workArea.Height - heightPx) / 2),
        PopupPlacement.RightCenter =>
            new PixelPoint(workArea.Right - widthPx - marginPx,
                           workArea.Y + (workArea.Height - heightPx) / 2),
        PopupPlacement.BottomLeft =>
            new PixelPoint(workArea.X + marginPx,
                           workArea.Bottom - heightPx - marginPx),
        PopupPlacement.BottomCenter =>
            new PixelPoint(workArea.X + (workArea.Width - widthPx) / 2,
                           workArea.Bottom - heightPx - marginPx),
        PopupPlacement.BottomRight =>
            new PixelPoint(workArea.Right - widthPx - marginPx,
                           workArea.Bottom - heightPx - marginPx),
        _ => new PixelPoint(workArea.Right - widthPx - marginPx,
                            workArea.Y + marginPx), // safe TopRight fallback
    };
}
```

Both `PositionOnScreen` and `RepositionWithActualSize` then call `ComputePosition(...)`.

### 4.3 `ShowEyeRestReminderInternalAsync` changes (`EyeRest.UI/Services/AvaloniaNotificationService.cs`)

```csharp
private async Task ShowEyeRestReminderInternalAsync(TimeSpan duration)
{
    var config = await _configurationService.LoadConfigurationAsync(); // NEW (was missing)
    var tcs = new TaskCompletionSource<bool>();
    PopupWindow? myPopup = null;

    Dispatcher.UIThread.Post(() =>
    {
        try
        {
            _logger.LogInformation("Showing eye rest reminder popup for {Duration}", duration);
            CloseCurrentPopup();

            // NEW: conditional overlay (matches Break behavior)
            if (config.EyeRest.OverlayEnabled)
                ShowDimOverlays(config.EyeRest.OverlayOpacityPercent);

            myPopup = (PopupWindow)_popupWindowFactory.CreateEyeRestPopup();
            _currentPopup = myPopup;

            // NEW: configurable placement
            myPopup.PositionOnScreen(MapPlacement(config.EyeRest.PopupPosition));

            myPopup.Closed += (_, _) =>
            {
                FireChannelAudio(AudioChannel.EyeRestEnd, c => c.EyeRest.EndAudio);
                Dispatcher.UIThread.Post(
                    () => tcs.TrySetResult(false),
                    DispatcherPriority.Background);
            };

            myPopup.Show();
            FireChannelAudio(AudioChannel.EyeRestStart, c => c.EyeRest.StartAudio);

            if (myPopup.PopupContent is EyeRestPopup eyeRestPopup)
            {
                eyeRestPopup.Completed += (s, e) => tcs.TrySetResult(true);
                eyeRestPopup.StartCountdown(duration);
            }
            else
            {
                tcs.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing eye rest reminder");
            tcs.TrySetResult(false);
        }
    });

    await Task.WhenAny(tcs.Task, Task.Delay(duration + TimeSpan.FromSeconds(2)));

    Dispatcher.UIThread.Post(() =>
    {
        if (config.EyeRest.OverlayEnabled)
            HideDimOverlays();                              // NEW: symmetric cleanup
        CloseSpecificPopup(myPopup);
    });
}

private static PopupPlacement MapPlacement(PopupPosition position) => position switch
{
    PopupPosition.Center        => PopupPlacement.Center,
    PopupPosition.TopLeft       => PopupPlacement.TopLeft,
    PopupPosition.TopCenter     => PopupPlacement.TopCenter,
    PopupPosition.TopRight      => PopupPlacement.TopRight,
    PopupPosition.LeftCenter    => PopupPlacement.LeftCenter,
    PopupPosition.RightCenter   => PopupPlacement.RightCenter,
    PopupPosition.BottomLeft    => PopupPlacement.BottomLeft,
    PopupPosition.BottomCenter  => PopupPlacement.BottomCenter,
    PopupPosition.BottomRight   => PopupPlacement.BottomRight,
    _                           => PopupPlacement.TopRight,
};
```

**Key behaviors preserved:**
- `ShowEyeRestReminderTestAsync` flows through the same internal method → Test button automatically exercises both new settings.
- `ShowEyeRestWarningInternalAsync` is **not** modified — warning stays at TopRight.
- `ShowDimOverlays` / `HideDimOverlays` are **not** modified — already generic and correct.
- `GetScreenWithCursor` is **not** modified — popup still appears on cursor's monitor.

### 4.4 Cleanup symmetry (critical)

Per the 2026-04-28 lesson learned (CLAUDE.md), every popup close path must hide its overlay. The cleanup block above runs unconditionally at the end of `ShowEyeRestReminderInternalAsync`. The error catch + the `Task.WhenAny` timeout both fall through to the same `Dispatcher.UIThread.Post` cleanup block, so overlay cannot get stuck.

## 5. Settings UI

### 5.1 ViewModel properties (`EyeRest.UI/ViewModels/MainWindowViewModel.cs`)

Three new properties, each following the dirty-tracking convention to avoid the LaunchAgent-class config-corruption bug:

```csharp
private bool _eyeRestOverlayEnabled = true;
public bool EyeRestOverlayEnabled
{
    get => _eyeRestOverlayEnabled;
    set
    {
        if (SetProperty(ref _eyeRestOverlayEnabled, value))
        {
            _pendingTimerChanges.Add(nameof(EyeRestOverlayEnabled));
            _ = SaveTimerSettingAsync();
        }
    }
}

private int _eyeRestOverlayOpacityPercent = 50;
public int EyeRestOverlayOpacityPercent
{
    get => _eyeRestOverlayOpacityPercent;
    set
    {
        if (SetProperty(ref _eyeRestOverlayOpacityPercent, value))
        {
            _pendingTimerChanges.Add(nameof(EyeRestOverlayOpacityPercent));
            _ = SaveTimerSettingAsync();
        }
    }
}

private PopupPosition _eyeRestPopupPosition = PopupPosition.TopRight;
public PopupPosition EyeRestPopupPosition
{
    get => _eyeRestPopupPosition;
    set
    {
        if (SetProperty(ref _eyeRestPopupPosition, value))
        {
            _pendingTimerChanges.Add(nameof(EyeRestPopupPosition));
            _ = SaveTimerSettingAsync();
        }
    }
}

public IReadOnlyList<PopupPosition> PopupPositionOptions { get; } = new[]
{
    PopupPosition.TopLeft,    PopupPosition.TopCenter,    PopupPosition.TopRight,
    PopupPosition.LeftCenter, PopupPosition.Center,       PopupPosition.RightCenter,
    PopupPosition.BottomLeft, PopupPosition.BottomCenter, PopupPosition.BottomRight,
};
```

### 5.2 Wiring into existing config-flow methods

**`ReapplyConfigurationValues` (around line 1349):**
```csharp
EyeRestOverlayEnabled = _configuration.EyeRest.OverlayEnabled;
EyeRestOverlayOpacityPercent = _configuration.EyeRest.OverlayOpacityPercent;
EyeRestPopupPosition = _configuration.EyeRest.PopupPosition;
```

**`UpdateConfigurationFromUI` (around line 2949):**
```csharp
if (changed.Contains(nameof(EyeRestOverlayEnabled)))
    config.EyeRest.OverlayEnabled = EyeRestOverlayEnabled;
if (changed.Contains(nameof(EyeRestOverlayOpacityPercent)))
    config.EyeRest.OverlayOpacityPercent = EyeRestOverlayOpacityPercent;
if (changed.Contains(nameof(EyeRestPopupPosition)))
    config.EyeRest.PopupPosition = EyeRestPopupPosition;
```

**Validation block (around line 1999):**
```csharp
if (EyeRestOverlayOpacityPercent < 0 || EyeRestOverlayOpacityPercent > 100)
    return false;
```

### 5.3 XAML — new GlassCard on the Eye Rest tab

Insert **after** the "Warning + End Sound" GlassCard (around line 802) and **before** the Info Card (line 805), mirroring the Break tab's "Overlay + Warning Time + Reset" card placement:

```xml
<!-- Overlay + Popup Position Card -->
<Border Classes="GlassCard">
    <StackPanel Spacing="16">
        <!-- Show Overlay Toggle -->
        <Grid ColumnDefinitions="Auto,*,Auto">
            <Border Grid.Column="0" Width="32" Height="32" CornerRadius="8"
                    Background="{DynamicResource ToggleCardIconBackground}">
                <PathIcon Data="{StaticResource VisibilityIcon}" Width="16" Height="16"
                          Foreground="{DynamicResource ToggleCardIconColor}" />
            </Border>
            <TextBlock Grid.Column="1" Text="Show Overlay" Margin="12,0,0,0"
                       FontSize="15" FontWeight="SemiBold" VerticalAlignment="Center" />
            <ToggleSwitch Grid.Column="2"
                          IsChecked="{Binding EyeRestOverlayEnabled}"
                          VerticalAlignment="Center"
                          OnContent="" OffContent="" />
        </Grid>

        <!-- Overlay Opacity (disabled when toggle is off) -->
        <Grid ColumnDefinitions="*,Auto"
              IsEnabled="{Binding EyeRestOverlayEnabled}">
            <StackPanel Grid.Column="0" Spacing="2">
                <TextBlock Text="Overlay Opacity" FontSize="14" FontWeight="SemiBold" />
                <Slider Minimum="0" Maximum="100"
                        Value="{Binding EyeRestOverlayOpacityPercent}"
                        TickFrequency="5" IsSnapToTickEnabled="True" />
            </StackPanel>
            <Border Grid.Column="1" Classes="SliderBadge"
                    VerticalAlignment="Top" Margin="16,0,0,0">
                <StackPanel Orientation="Horizontal" Spacing="2">
                    <TextBlock Text="{Binding EyeRestOverlayOpacityPercent}"
                               FontWeight="Bold" FontSize="14"
                               Foreground="{DynamicResource SliderBadgeText}" />
                    <TextBlock Text="%" FontSize="12"
                               Foreground="{DynamicResource SliderBadgeText}" />
                </StackPanel>
            </Border>
        </Grid>

        <!-- Popup Position -->
        <Grid ColumnDefinitions="*,Auto">
            <StackPanel Grid.Column="0" Spacing="2">
                <TextBlock Text="Popup Position" FontSize="14" FontWeight="SemiBold" />
                <ComboBox ItemsSource="{Binding PopupPositionOptions}"
                          SelectedItem="{Binding EyeRestPopupPosition}"
                          HorizontalAlignment="Stretch">
                    <ComboBox.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding Converter={x:Static converters:PopupPositionToLabelConverter.Instance}}" />
                        </DataTemplate>
                    </ComboBox.ItemTemplate>
                </ComboBox>
            </StackPanel>
        </Grid>
    </StackPanel>
</Border>
```

### 5.4 Label converter (`EyeRest.UI/Converters/PopupPositionToLabelConverter.cs`)

```csharp
public sealed class PopupPositionToLabelConverter : IValueConverter
{
    public static readonly PopupPositionToLabelConverter Instance = new();
    public object? Convert(object? value, Type _, object? __, CultureInfo ___) =>
        value is PopupPosition p ? p switch
        {
            PopupPosition.Center       => "Center",
            PopupPosition.TopLeft      => "Top Left",
            PopupPosition.TopCenter    => "Top Middle",
            PopupPosition.TopRight     => "Top Right",
            PopupPosition.LeftCenter   => "Left Middle",
            PopupPosition.RightCenter  => "Right Middle",
            PopupPosition.BottomLeft   => "Bottom Left",
            PopupPosition.BottomCenter => "Bottom Middle",
            PopupPosition.BottomRight  => "Bottom Right",
            _ => p.ToString(),
        } : value?.ToString();
    public object? ConvertBack(object? v, Type _, object? __, CultureInfo ___) => v;
}
```

## 6. Testing Plan

### 6.1 Automated tests (`EyeRest.Tests.Avalonia`)

| # | Test name | Scenario | Expected |
|---|---|---|---|
| 1 | `EyeRestPopup_RespectsConfiguredPosition` | Set `EyeRest.PopupPosition = BottomLeft` | Popup position equals work-area bottom-left + margin |
| 2 | `EyeRestPopup_OverlayEnabled_True_ShowsOverlay` | `OverlayEnabled = true`, opacity = 30 | `IScreenOverlayService.IsOverlayVisible` true; alpha ≈ 30% |
| 3 | `EyeRestPopup_OverlayEnabled_False_NoOverlay` | `OverlayEnabled = false` | No overlay windows during popup display |
| 4 | `EyeRestPopup_Cleanup_HidesOverlay` | Show + dismiss reminder | Overlay count back to 0 after dismissal |
| 5 | `ConfigMigration_OldConfigWithoutNewFields_GetsDefaults` | Load config JSON missing new fields | Defaults applied: enabled=true, 50%, TopRight |
| 6 | `MainWindowViewModel_DirtyTracking_OnlyChangedFieldsSaved` | Mutate only `EyeRestPopupPosition` | `_pendingTimerChanges` contains only that one name |

`InternalsVisibleTo("EyeRest.Tests.Avalonia")` may need to be added to `AvaloniaNotificationService` for overlay-list assertions.

### 6.2 Manual smoke test

1. Fresh install — default top-right, 50% overlay matches existing behavior.
2. Cycle all 9 positions — verify each visually via "Test Eye Rest" button.
3. Multi-monitor: cursor on secondary screen → popup appears on that screen at the chosen position.
4. Multi-monitor overlay: all screens dim; click one to clear that one's dim only.
5. Toggle overlay off → no dim. Toggle on → dim returns.
6. Opacity = 0 → still renders (transparent), distinct from "Overlay off".
7. Persistence: set BottomCenter / opacity 80 / overlay on → quit → relaunch → preserved.
8. Migration: hand-edit config.json to remove new fields → launch → defaults restored.
9. macOS notch / dock: TopCenter doesn't slide under menu bar; BottomCenter doesn't slide under dock.

### 6.3 Regression — smart pause/resume

Per the 2026-04-28 lesson, exercise: while Eye rest popup + overlay are showing, go idle → smart-pause must no-op → return from idle → overlay tears down cleanly when popup completes.

## 7. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| Overlay stuck visible if popup close path errors | Cleanup `Dispatcher.UIThread.Post` runs unconditionally at the end; matches Break popup safety. |
| User sets opacity to 0 expecting "no overlay" | Toggle is the off-switch; opacity = 0 is a valid transparent state. Documented in UI: opacity slider is disabled when toggle is off. |
| Config migration drops position to invalid enum | `MapPlacement` and `ComputePosition` both fall back to `TopRight` on unknown values. |
| Multi-monitor screen disconnect mid-popup | Existing Break behavior; not a new risk introduced by this change. |
| ComboBox SelectedItem doesn't update VM on first show | Default `TopRight` matches initial enum default; first-show is correct without explicit `SelectedIndex`. |
| Bulk-save corrupting config (LaunchAgent class) | Each property adds itself to `_pendingTimerChanges`; only dirty fields are written. |

## 8. Out of Scope

- Per-monitor overlay opacity
- Configurable margin distance from screen edges
- Animations between positions
- Persisting manually-dragged popup position
- Sharing an `OverlaySettings` type between EyeRest and Break (deferred — would be a separate refactor)
- 3x3 visual grid position picker (UX upgrade for a future iteration)
- Changes to Break popup position or overlay
- Changes to Eye rest **Warning** popup positioning

## 9. File-Touch Summary

| File | Change |
|---|---|
| `EyeRest.Abstractions/Models/AppConfiguration.cs` | +3 properties on `EyeRestSettings` |
| `EyeRest.Abstractions/Models/PopupPosition.cs` | **NEW** enum |
| `EyeRest.UI/Views/PopupWindow.axaml.cs` | Extend `PopupPlacement` enum; extract `ComputePosition`; consume in both `PositionOnScreen` and `RepositionWithActualSize` |
| `EyeRest.UI/Services/AvaloniaNotificationService.cs` | Load config; conditional `ShowDimOverlays`; map enum → placement; symmetric `HideDimOverlays` cleanup |
| `EyeRest.UI/ViewModels/MainWindowViewModel.cs` | +3 properties with dirty tracking; +`PopupPositionOptions` list; wire `ReapplyConfigurationValues` + `UpdateConfigurationFromUI` + validation |
| `EyeRest.UI/Views/MainWindow.axaml` | New GlassCard with toggle + slider + ComboBox on Eye Rest tab |
| `EyeRest.UI/Converters/PopupPositionToLabelConverter.cs` | **NEW** converter |
| `EyeRest.Tests.Avalonia/Services/AvaloniaNotificationServiceTests.cs` | New (or extended) test fixture covering the 6 cases above |

## 10. Acceptance Criteria

- ☐ All 6 automated tests pass
- ☐ Manual smoke test (9 items) all green
- ☐ Build clean with `TreatWarningsAsErrors=true`
- ☐ Default behavior on fresh install matches pre-change behavior (TopRight + dim overlay 50%)
- ☐ Existing config files without new fields load successfully with defaults
- ☐ Smart-pause/resume regression test passes
- ☐ Code reviewer + Technical Architect + QA sign-off
