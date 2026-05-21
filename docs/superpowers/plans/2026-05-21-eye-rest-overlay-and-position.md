# Eye Rest Overlay & Popup Position — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two new Eye rest settings — a configurable dim overlay (toggle + opacity) and a 9-position popup placement — so the Eye rest reminder reaches Break-popup feature parity for overlay coverage and gains position flexibility.

**Architecture:** Three new fields on `EyeRestSettings`, a new `PopupPosition` enum in `Abstractions`, the existing UI-layer `PopupPlacement` enum extended from 2 to 9 values, the duplicated placement-math switch refactored into a single `ComputePosition` helper, and the existing `ShowDimOverlays` / `HideDimOverlays` infrastructure reused in `ShowEyeRestReminderInternalAsync`. Settings UI follows the existing GlassCard + dirty-tracking VM pattern.

**Tech Stack:** .NET 8, Avalonia 11.3, MVVM, xUnit (Avalonia.Headless test fixture), System.Text.Json (integer enum serialization — matches existing project convention).

**Spec:** `docs/superpowers/specs/2026-05-21-eye-rest-overlay-and-position-design.md`

---

## File Structure

| Operation | File | Responsibility |
|---|---|---|
| Create | `EyeRest.Abstractions/Models/PopupPosition.cs` | Domain enum, 9 named positions |
| Modify | `EyeRest.Abstractions/Models/AppConfiguration.cs` | +3 fields on `EyeRestSettings` |
| Modify | `EyeRest.UI/Views/PopupWindow.axaml.cs` | Extend `PopupPlacement`; extract `ComputePosition` |
| Modify | `EyeRest.UI/Services/AvaloniaNotificationService.cs` | Wire overlay + position in eye rest reminder path |
| Create | `EyeRest.UI/Converters/PopupPositionToLabelConverter.cs` | Friendly UI labels |
| Modify | `EyeRest.UI/ViewModels/MainWindowViewModel.cs` | 3 properties + dirty tracking + plumbing |
| Modify | `EyeRest.UI/Views/MainWindow.axaml` | New GlassCard on Eye Rest tab |
| Create | `EyeRest.Tests.Avalonia/Services/EyeRestOverlayAndPositionTests.cs` | Behavioral tests |

---

## Task 1: Add `PopupPosition` enum (Abstractions)

**Files:**
- Create: `EyeRest.Abstractions/Models/PopupPosition.cs`

- [ ] **Step 1: Create the enum file**

```csharp
namespace EyeRest.Models
{
    /// <summary>
    /// Domain enum for popup placement preference. Maps to the UI-layer
    /// PopupPlacement enum at the notification service boundary.
    /// Persisted as integer to match existing project convention.
    /// </summary>
    public enum PopupPosition
    {
        Center = 0,
        TopLeft = 1,
        TopCenter = 2,
        TopRight = 3,
        LeftCenter = 4,
        RightCenter = 5,
        BottomLeft = 6,
        BottomCenter = 7,
        BottomRight = 8,
    }
}
```

- [ ] **Step 2: Build the Abstractions project**

Run: `dotnet build EyeRest.Abstractions/EyeRest.Abstractions.csproj`
Expected: Build succeeded, 0 Errors.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.Abstractions/Models/PopupPosition.cs
git commit -m "feat(model): add PopupPosition enum with 9 named positions"
```

---

## Task 2: Add `EyeRestSettings` fields

**Files:**
- Modify: `EyeRest.Abstractions/Models/AppConfiguration.cs:27-35`

- [ ] **Step 1: Extend `EyeRestSettings`**

Find the existing class (around line 27) and add three new properties:

```csharp
public class EyeRestSettings
{
    public int IntervalMinutes { get; set; } = 20;
    public int DurationSeconds { get; set; } = 20;
    public AudioChannelConfig StartAudio { get; set; } = new();
    public AudioChannelConfig EndAudio { get; set; } = new();
    public bool WarningEnabled { get; set; } = true;
    public int WarningSeconds { get; set; } = 15;

    // Overlay (parity with Break popup) — opt-in via OverlayEnabled.
    public bool OverlayEnabled { get; set; } = true;
    public int OverlayOpacityPercent { get; set; } = 50; // 0..100

    // Where the eye rest reminder popup appears on the cursor's screen.
    // Default TopRight preserves the previously hardcoded placement.
    public PopupPosition PopupPosition { get; set; } = PopupPosition.TopRight;
}
```

- [ ] **Step 2: Build**

Run: `dotnet build EyeRest.Abstractions/EyeRest.Abstractions.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.Abstractions/Models/AppConfiguration.cs
git commit -m "feat(model): add OverlayEnabled, OverlayOpacityPercent, PopupPosition to EyeRestSettings"
```

---

## Task 3: Extend `PopupPlacement` and extract `ComputePosition`

**Files:**
- Modify: `EyeRest.UI/Views/PopupWindow.axaml.cs:11-15` (enum)
- Modify: `EyeRest.UI/Views/PopupWindow.axaml.cs:103-130` (`RepositionWithActualSize`)
- Modify: `EyeRest.UI/Views/PopupWindow.axaml.cs:171-204` (`PositionOnScreen`)

- [ ] **Step 1: Extend `PopupPlacement` enum**

Replace the existing enum (lines 11-15):

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

- [ ] **Step 2: Add the `ComputePosition` helper**

Add this private static method anywhere inside the `PopupWindow` class body (suggested placement: right above `RepositionWithActualSize`):

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
                            workArea.Y + marginPx),
    };
}
```

- [ ] **Step 3: Refactor `RepositionWithActualSize` to call the helper**

Replace the existing method body (lines 103-130) with:

```csharp
private void RepositionWithActualSize(PopupPlacement placement)
{
    var screen = GetScreenWithCursor();
    if (screen == null || !FrameSize.HasValue) return;

    var scaling = screen.Scaling;
    var actualWidth = (int)(FrameSize.Value.Width * scaling);
    var actualHeight = (int)(FrameSize.Value.Height * scaling);

    Position = ComputePosition(placement, screen.WorkingArea, scaling, actualWidth, actualHeight);
}
```

- [ ] **Step 4: Refactor `PositionOnScreen` to call the helper**

Replace the existing method body (lines 171-204) with:

```csharp
public void PositionOnScreen(PopupPlacement placement = PopupPlacement.TopRight)
{
    _pendingPlacement = placement;
    _currentPlacement = placement;
    var screen = GetScreenWithCursor();
    if (screen == null) return;

    var scaling = screen.Scaling;
    var widthPx = (int)(_positionHintWidth * scaling);
    var heightPx = (int)(_positionHintHeight * scaling);

    Position = ComputePosition(placement, screen.WorkingArea, scaling, widthPx, heightPx);
}
```

- [ ] **Step 5: Build the UI project**

Run: `dotnet build EyeRest.UI/EyeRest.UI.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add EyeRest.UI/Views/PopupWindow.axaml.cs
git commit -m "refactor(ui): extract ComputePosition; extend PopupPlacement to 9 values"
```

---

## Task 4: Wire overlay + configurable position in `ShowEyeRestReminderInternalAsync`

**Files:**
- Modify: `EyeRest.UI/Services/AvaloniaNotificationService.cs:155-207`

- [ ] **Step 1: Add the placement-mapping helper**

Add this private static method anywhere inside the `AvaloniaNotificationService` class (suggested placement: right above `#region Screen Dimming Overlays`):

```csharp
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

- [ ] **Step 2: Replace `ShowEyeRestReminderInternalAsync` (lines 155-207)**

```csharp
private async Task ShowEyeRestReminderInternalAsync(TimeSpan duration)
{
    // Load config OFF the UI thread (mirrors the Break popup pattern).
    var config = await _configurationService.LoadConfigurationAsync();

    var tcs = new TaskCompletionSource<bool>();
    PopupWindow? myPopup = null;

    Dispatcher.UIThread.Post(() =>
    {
        try
        {
            _logger.LogInformation("Showing eye rest reminder popup for {Duration}", duration);
            CloseCurrentPopup(); // Close any existing popup (e.g., warning) first

            if (config.EyeRest.OverlayEnabled)
                ShowDimOverlays(config.EyeRest.OverlayOpacityPercent);

            myPopup = (PopupWindow)_popupWindowFactory.CreateEyeRestPopup();
            _currentPopup = myPopup;
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
            HideDimOverlays();
        CloseSpecificPopup(myPopup);
    });
}
```

- [ ] **Step 3: Build the UI project**

Run: `dotnet build EyeRest.UI/EyeRest.UI.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add EyeRest.UI/Services/AvaloniaNotificationService.cs
git commit -m "feat(notification): wire overlay + configurable position into eye rest reminder"
```

---

## Task 5: Add `PopupPositionToLabelConverter`

**Files:**
- Create: `EyeRest.UI/Converters/PopupPositionToLabelConverter.cs`

- [ ] **Step 1: Create the converter**

```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using EyeRest.Models;

namespace EyeRest.UI.Converters
{
    public sealed class PopupPositionToLabelConverter : IValueConverter
    {
        public static readonly PopupPositionToLabelConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is PopupPosition p
                ? p switch
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
                    _                          => p.ToString(),
                }
                : value?.ToString();

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build EyeRest.UI/EyeRest.UI.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.UI/Converters/PopupPositionToLabelConverter.cs
git commit -m "feat(ui): add PopupPositionToLabelConverter for friendly UI labels"
```

---

## Task 6: Add ViewModel properties + plumbing

**Files:**
- Modify: `EyeRest.UI/ViewModels/MainWindowViewModel.cs` (several locations)

- [ ] **Step 1: Add backing fields and properties**

Find the section after `EyeRestWarningSeconds` (around line 390-393) and insert these three properties. Use the exact same dirty-tracking pattern as `EyeRestWarningEnabled`:

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

If `using System.Collections.Generic;` is missing at the top of the file, add it. Same for `using EyeRest.Models;` (likely already present).

- [ ] **Step 2: Wire into `ReapplyConfigurationValues`**

Find the block around line 1349-1355 (where `EyeRestWarningEnabled` is loaded) and add three lines:

```csharp
EyeRestIntervalMinutes = _configuration.EyeRest.IntervalMinutes;
EyeRestDurationSeconds = _configuration.EyeRest.DurationSeconds;
// ... existing lines ...
EyeRestWarningEnabled = _configuration.EyeRest.WarningEnabled;
EyeRestWarningSeconds = _configuration.EyeRest.WarningSeconds;

// NEW
EyeRestOverlayEnabled = _configuration.EyeRest.OverlayEnabled;
EyeRestOverlayOpacityPercent = _configuration.EyeRest.OverlayOpacityPercent;
EyeRestPopupPosition = _configuration.EyeRest.PopupPosition;
```

- [ ] **Step 3: Wire into `UpdateConfigurationFromUI`**

Find the block around line 2949-2960 (where `EyeRestWarningEnabled` is conditionally written) and add three blocks:

```csharp
if (changed.Contains(nameof(EyeRestWarningEnabled)))
    config.EyeRest.WarningEnabled = EyeRestWarningEnabled;
if (changed.Contains(nameof(EyeRestWarningSeconds)))
    config.EyeRest.WarningSeconds = EyeRestWarningSeconds;

// NEW
if (changed.Contains(nameof(EyeRestOverlayEnabled)))
    config.EyeRest.OverlayEnabled = EyeRestOverlayEnabled;
if (changed.Contains(nameof(EyeRestOverlayOpacityPercent)))
    config.EyeRest.OverlayOpacityPercent = EyeRestOverlayOpacityPercent;
if (changed.Contains(nameof(EyeRestPopupPosition)))
    config.EyeRest.PopupPosition = EyeRestPopupPosition;
```

- [ ] **Step 4: Add validation**

Find the validation method (around line 1999, where `OverlayOpacityPercent` is range-checked) and add:

```csharp
if (EyeRestOverlayOpacityPercent < 0 || EyeRestOverlayOpacityPercent > 100)
    return false;
```

- [ ] **Step 5: Build**

Run: `dotnet build EyeRest.UI/EyeRest.UI.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add EyeRest.UI/ViewModels/MainWindowViewModel.cs
git commit -m "feat(vm): add eye-rest overlay+position properties with dirty tracking"
```

---

## Task 7: Add Settings UI card in `MainWindow.axaml`

**Files:**
- Modify: `EyeRest.UI/Views/MainWindow.axaml` (insert new Border around current line 803, between the "Warning + End Sound" card and the Info Card)

- [ ] **Step 1: Insert the new GlassCard**

Find the closing `</Border>` of the "Warning + End Sound" card (around line 802) and the opening of the "Info Card: Tip" (around line 805). Between them, insert:

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

The `xmlns:converters="using:EyeRest.UI.Converters"` declaration is already at the top of MainWindow.axaml (line 4), so the `{x:Static converters:PopupPositionToLabelConverter.Instance}` reference will resolve.

- [ ] **Step 2: Build & smoke-run**

Run: `dotnet build EyeRest.UI/EyeRest.UI.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.UI/Views/MainWindow.axaml
git commit -m "feat(ui): add overlay + popup position card to eye rest settings"
```

---

## Task 8: Add behavioral tests

**Files:**
- Create: `EyeRest.Tests.Avalonia/Services/EyeRestOverlayAndPositionTests.cs`

- [ ] **Step 1: Write the failing test fixture**

```csharp
using EyeRest.Models;
using Xunit;

namespace EyeRest.Tests.Avalonia.Services
{
    public class EyeRestOverlayAndPositionTests
    {
        [Fact]
        public void EyeRestSettings_NewFields_HaveExpectedDefaults()
        {
            var settings = new EyeRestSettings();

            Assert.True(settings.OverlayEnabled);
            Assert.Equal(50, settings.OverlayOpacityPercent);
            Assert.Equal(PopupPosition.TopRight, settings.PopupPosition);
        }

        [Fact]
        public void PopupPosition_HasNineValues()
        {
            var values = System.Enum.GetValues<PopupPosition>();

            Assert.Equal(9, values.Length);
            Assert.Contains(PopupPosition.Center, values);
            Assert.Contains(PopupPosition.TopCenter, values);
            Assert.Contains(PopupPosition.BottomCenter, values);
            Assert.Contains(PopupPosition.LeftCenter, values);
            Assert.Contains(PopupPosition.RightCenter, values);
        }

        [Theory]
        [InlineData(PopupPosition.Center)]
        [InlineData(PopupPosition.TopLeft)]
        [InlineData(PopupPosition.TopCenter)]
        [InlineData(PopupPosition.TopRight)]
        [InlineData(PopupPosition.LeftCenter)]
        [InlineData(PopupPosition.RightCenter)]
        [InlineData(PopupPosition.BottomLeft)]
        [InlineData(PopupPosition.BottomCenter)]
        [InlineData(PopupPosition.BottomRight)]
        public void EyeRestSettings_PopupPosition_RoundTripsAllValues(PopupPosition position)
        {
            var settings = new EyeRestSettings { PopupPosition = position };
            Assert.Equal(position, settings.PopupPosition);
        }
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test EyeRest.Tests.Avalonia/EyeRest.Tests.Avalonia.csproj --filter FullyQualifiedName~EyeRestOverlayAndPositionTests`
Expected: All 11 tests pass.

- [ ] **Step 3: Commit**

```bash
git add EyeRest.Tests.Avalonia/Services/EyeRestOverlayAndPositionTests.cs
git commit -m "test: behavioral coverage for eye rest overlay+position defaults and enum"
```

---

## Task 9: Final build, full test run, and smoke test

- [ ] **Step 1: Full solution build**

Run: `dotnet build EyeRest.sln`
Expected: Build succeeded. 0 Errors. (Warnings as errors is on per `Directory.Build.props`.)

- [ ] **Step 2: Full test suite**

Run: `dotnet test EyeRest.sln`
Expected: All tests pass (118 existing + ~11 new).

- [ ] **Step 3: Manual smoke test checklist**

Launch the app: `dotnet run --project EyeRest.UI`

For each item, mark pass/fail in the PR description:

- [ ] Fresh-install behavior (config deleted): default top-right placement, 50% dim overlay.
- [ ] All 9 positions selectable via "Test Eye Rest" button on Eye Rest tab — each visually correct.
- [ ] Multi-monitor (if available): popup appears on cursor's monitor at chosen position.
- [ ] Overlay toggle ON → all screens dim. Click a screen → that screen's dim clears.
- [ ] Overlay toggle OFF → no dim.
- [ ] Opacity slider disabled when toggle is off.
- [ ] Restart app → settings persisted.
- [ ] Hand-edit config.json: remove the three new fields → relaunch → defaults restored, no exception in logs.

- [ ] **Step 4: Update PROJECT_STRUCTURE.md timestamp if relevant**

Only if `PROJECT_STRUCTURE.md` already mentions the field counts (148 cs files, etc.), bump them. Otherwise skip.

- [ ] **Step 5: Final commit**

```bash
git status   # confirm clean tree
git log --oneline feature/eye-rest-overlay-and-position ^develop
```

No additional commit required if everything previous was committed cleanly.

---

## Acceptance Criteria

- ☐ All 9 manual smoke tests pass
- ☐ `dotnet build EyeRest.sln` clean with TreatWarningsAsErrors=true
- ☐ `dotnet test EyeRest.sln` all green
- ☐ Default behavior unchanged on existing user configs (TopRight + 50% overlay)
- ☐ Old configs missing the new fields load without exceptions; defaults applied
- ☐ Code-reviewer + Technical Architect + QA reviews returned with no critical issues
- ☐ Spec-vs-implementation audit shows no gaps

## Self-Review Checklist (run after writing the plan)

- ☐ Spec § 3 (Data Model) → Tasks 1, 2
- ☐ Spec § 4 (Popup Positioning Logic + Overlay Wiring) → Tasks 3, 4
- ☐ Spec § 5 (Settings UI) → Tasks 5, 6, 7
- ☐ Spec § 6 (Testing Plan) → Task 8
- ☐ Spec § 7 (Risks & Edge Cases) → covered by Task 9 manual checklist
- ☐ No "TBD", no "implement later"
- ☐ Each task: exact file paths, code blocks, exact commands
- ☐ Type names match across tasks (`PopupPosition`, `PopupPlacement`, `EyeRestPopupPosition`, etc.)
