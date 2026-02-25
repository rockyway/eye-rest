# 002 - Popup Glass-Morphism Redesign

## Overview
Redesign all 6 popups to match the Liquid UI V2 Glass-Morphism design system already used in MainWindow.

## Design Principles
- **Glass-morphism**: Semi-transparent backgrounds with blur effects over gradient mesh backgrounds
- **Reuse existing style classes**: `GlassCard`, `GlassPanel`, `GradientStart/Stop/Pause`, `ActionBtn`, etc.
- **Reuse existing theme brushes**: `MeshGradientBrush`, `GlassCardBackground`, `GlassCardBorder`, `AccentBrush`, etc.
- **No ViewModel changes**: All code-behind APIs (events, methods) remain identical
- **Dark/Light theme support**: All new brushes use `DynamicResource` for theme switching

## Files to Modify

### AXAML Files (visual redesign only)
1. `EyeRest.UI/Views/EyeRestWarningPopup.axaml` - Glass warning card
2. `EyeRest.UI/Views/EyeRestPopup.axaml` - Glass eye rest screen
3. `EyeRest.UI/Views/BreakWarningPopup.axaml` - Glass break warning card
4. `EyeRest.UI/Views/BreakPopup.axaml` - Glass break screen
5. `EyeRest.UI/Views/AboutWindow.axaml` - Glass about dialog

### Code-Behind Files (minimal changes)
6. `EyeRest.UI/ViewModels/MainWindowViewModel.cs` - Replace inline ConfirmExit dialog with glass-styled version

### Theme/Style Files (add popup-specific brushes)
7. `EyeRest.UI/Resources/LightTheme.axaml` - Add popup gradient brushes
8. `EyeRest.UI/Resources/DarkTheme.axaml` - Add popup gradient brushes (dark variants)
9. `EyeRest.UI/Resources/GlassStyles.axaml` - Add popup-specific style classes

### Factory (adjust popup sizes for new design)
10. `EyeRest.UI/Services/AvaloniaPopupWindowFactory.cs` - Update popup window sizes

## Detailed Design Per Popup

### 1. EyeRestWarningPopup (300x380, TopRight)
**Current**: White border with flat SurfaceBrush, emoji icon, vertical progress bar
**New**:
- Outer Border: `MeshGradientBrush` background (gradient fill), `CornerRadius="20"`
- Inner content wrapped in `GlassCard`-style border
- Replace emoji with SVG `EyeIcon` in a circular accent container
- Circular countdown ring (simulated with two overlapping arcs/borders) instead of vertical progress bar
- Keep: `CountdownText`, `ProgressBar` (hidden, still used by code-behind), `CloseButton`
- Add a visible progress element: horizontal slim progress bar (300x4px) with rounded ends
- "Eye Rest Starting Soon" title in bold, countdown in accent color
- "Press ESC to dismiss" hint at bottom in subtle text
- Close button: glass-style circular X in top-right

### 2. EyeRestPopup (480x460, TopRight)
**Current**: White border, blue circle with emoji, horizontal progress bar
**New**:
- Outer Border: `MeshGradientBrush` fill, `CornerRadius="24"`
- Inner `GlassPanel` with centered content
- Replace blue circle+emoji with SVG `EyeIcon` inside a gradient circle (StartButtonGradient green -> teal)
- "Time for an Eye Rest!" heading in bold 26pt
- "Look at something 20ft away" instruction in subtitle color
- Horizontal progress bar: rounded (`CornerRadius="4"`), thicker (8px), green gradient fill
- Time remaining text below progress
- "Press ESC to skip" hint
- Glass close button top-right

### 3. BreakWarningPopup (300x380, TopRight)
**Current**: White border, alarm emoji, vertical progress bar in warning color
**New**:
- Same layout structure as EyeRestWarningPopup but with warning color palette
- Outer Border: warning-tinted gradient (orange mesh)
- Inner glass card with `ClockIcon` SVG in orange circle
- "Break Starting Soon" title
- Horizontal slim progress bar in `PauseButtonGradient` (orange)
- Warning accent colors throughout
- Glass close button

### 4. BreakPopup (720x680, Center)
**Current**: White border, large timer display with emoji, action buttons with flat colors
**New**:
- Outer Border: `MeshGradientBrush` fill, `CornerRadius="24"`
- Timer display: minutes | eye icon in gradient circle | seconds - all inside glass card
- Large bold timer numbers (100pt) in accent blue
- "Time for a Break!" heading
- Instructions in subtitle text
- Progress bar: thick (12px), rounded, blue gradient fill
- Time remaining text
- Stretching link in glass pill button
- Action buttons row: use `GradientPause` (Delay 1m, Delay 5m) and `GradientStop` (Skip) style classes
- Confirmation button: `GradientStart` style (green gradient)
- Bottom hint text in subtle color
- No explicit close button (actions handle dismissal)

### 5. AboutWindow (380x360)
**Current**: Plain window with system decorations, flat layout
**New**:
- Convert from `Window` to borderless window with custom title bar (like MainWindow)
- `ExtendClientAreaToDecorationsHint="True"`, `Background="Transparent"`, `TransparencyLevelHint="Transparent"`
- Outer Border: `MeshGradientBrush` fill, `CornerRadius="20"`
- Inner glass panel with centered content
- App icon (existing asset), "Eye-Rest" title in bold, version text
- Description in subtitle color
- Close button: `GradientStart` style (green "Close" or a glass X button)
- macOS-safe: respect traffic lights

### 6. ConfirmExitDialog (in MainWindowViewModel)
**Current**: Programmatic plain Window(400x180) with basic Yes/No buttons
**New**:
- Create a new `ConfirmDialog.axaml` / `ConfirmDialog.axaml.cs` UserControl + PopupWindow host
- OR: Update the inline code to use glass-morphism styling
- Approach chosen: **Update inline code** to apply glass styling (simpler, no new files needed)
- Use `MeshGradientBrush` background, glass card inner panel
- Warning icon (`WarningTestIcon` SVG), title, message
- Two buttons: "Yes" (`GradientStop` red) and "No" (`ActionBtn` neutral)
- Borderless, center-owner, `CornerRadius="20"`

## New Theme Brushes Needed

### LightTheme additions:
```
PopupWarningGradient: LinearGradient #FCEABB -> #FCD5A0 -> #FBB87A (warm orange mesh)
PopupGlassOverlay: #59FFFFFF (slightly more opaque glass for popup readability)
PopupProgressTrack: #26000000 (subtle track for progress bars)
```

### DarkTheme additions:
```
PopupWarningGradient: LinearGradient #3D2200 -> #4A2800 -> #5C3000
PopupGlassOverlay: #591E293B
PopupProgressTrack: #26FFFFFF
```

## New GlassStyles Additions

```
Style: Border.PopupGlass - Glass overlay for popup content panels
Style: Button.PopupCloseBtn - Circular glass close button (24x24)
Style: ProgressBar.PopupProgress - Rounded, themed progress bar for popups
Style: Border.PopupIconCircle - Circular icon container with gradient fill
```

## Implementation Order

1. Add new theme brushes to LightTheme.axaml and DarkTheme.axaml
2. Add new popup style classes to GlassStyles.axaml
3. Redesign EyeRestWarningPopup.axaml (simplest popup, test the pattern)
4. Redesign BreakWarningPopup.axaml (mirrors warning pattern)
5. Redesign EyeRestPopup.axaml
6. Redesign BreakPopup.axaml (most complex)
7. Redesign AboutWindow.axaml
8. Update ConfirmExit dialog in MainWindowViewModel.cs
9. Update popup sizes in AvaloniaPopupWindowFactory.cs
10. Build and verify

## Constraints
- Zero code-behind API changes (same events, same public methods)
- All named controls (ProgressBar, CountdownText, etc.) must keep their x:Name
- PopupWindow host remains unchanged (transparent, borderless, topmost)
- Dark theme must work via DynamicResource switching
