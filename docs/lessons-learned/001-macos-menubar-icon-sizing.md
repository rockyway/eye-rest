# macOS Menu Bar Icon Sizing - Lessons Learned

## Problem

The macOS menu bar (status bar) icon appeared too small compared to native system icons (Wi-Fi, Bluetooth, Spotlight, etc.). Despite being technically present, the eye icon looked tiny and hard to see.

## Root Causes Identified

There were **two independent issues** compounding the problem:

1. **Icon canvas size was wrong** - Original icons were 18x18 (1x) / 36x36 (@2x), but the macOS menu bar is 22pt tall, so icons should be 22x22 (1x) / 44x44 (@2x).

2. **Icon art didn't fill the canvas** - The eye shape only occupied ~62-75% of the canvas due to coordinate padding in the drawing code. Other system icons fill nearly 100% of their canvas.

## Key Technical Facts

### macOS Menu Bar Sizing

| Property | Value |
|----------|-------|
| Menu bar height | 22pt (44px @2x Retina) |
| Max icon working area | 22x22pt |
| Apple recommended art size | 18x18pt (but most apps use full 22pt) |
| Image format | PNG with transparency |
| Template images | Set `[NSImage setTemplate:YES]` for automatic dark/light adaptation |

### Avalonia-Specific Gotchas

These are critical if your app uses Avalonia UI framework:

| Issue | What Happens | Solution |
|-------|-------------|----------|
| **@2x images interpreted as 2x logical size** | Avalonia treats a 44px PNG as 44pt, then downscales to fit 22pt -> icon appears small | Use 1x images (22px at 72 DPI) instead of @2x |
| **144 DPI trick backfires** | Setting @2x PNGs to 144 DPI (so 44px/144dpi = 22pt) made icons even smaller | Avalonia reads DPI metadata and applies additional scaling |
| **Native NSStatusBarButton inaccessible** | Avalonia renders via a custom NSView on the NSStatusItem, NOT the native button | Cannot easily override Avalonia's rendering pipeline |
| **`setView:nil` removes the icon entirely** | Calling `[statusItem setView:nil]` to restore native button removes Avalonia's rendering | Avalonia re-asserts its view on layout passes, causing flickering or disappearance |

### What Works for Avalonia TrayIcon

```
1x PNG at 22x22 pixels, 72 DPI, RGBA
```

That's it. Don't try to be clever with @2x, DPI metadata, or native interop. Avalonia's `WindowIcon` + `TrayIcon` will handle the rest. Make the art fill the canvas.

## What We Tried (and Results)

| Attempt | Approach | Result |
|---------|----------|--------|
| 1 | Resize PNGs from 18->22px with `sips` | Still small (art padding issue) |
| 2 | Native interop: set NSImage point size on button | Worked momentarily, Avalonia overwrites |
| 3 | Native interop: traverse NSStatusBarWindow -> KVC -> statusItem -> button | Found the button but Avalonia's NSView covers it |
| 4 | Native interop: hide Avalonia's subviews in contentView | Icon disappeared completely |
| 5 | Native interop: `[statusItem setView:nil]` + set native image | Icon disappeared (Avalonia lost its rendering surface) |
| 6 | Set @2x PNGs to 144 DPI | Icon got SMALLER (Avalonia double-scales) |
| 7 | Load 1x images instead of @2x | Better, but art still didn't fill canvas |
| 8 | **Regenerate icons with larger art filling 95% of canvas** | **Success** |

## The Fix That Worked

### 1. Correct Canvas Size
Generate icons at 22x22 (1x) and 44x44 (@2x) to match macOS menu bar height.

### 2. Fill the Canvas
The original drawing coordinates left ~20% padding on each side:

```python
# BEFORE - eye fills ~75% width, ~62% height
eye_rect = [r(4), r(6), r(28), r(26)]   # in 32-unit coordinate system
```

Fixed to fill the full canvas:

```python
# AFTER - eye fills ~95% of canvas
eye_rect = [r(0), r(3), r(32), r(29)]   # minimal padding
inner_rect = [r(5), r(7), r(27), r(25)]
pupil_rect = [r(10), r(10), r(22), r(22)]
```

### 3. Use 1x Images for Avalonia
```csharp
// Load 1x (22px @ 72 DPI = 22pt) - Avalonia renders at full menu bar size
return new Uri($"avares://EyeRest.UI/Assets/TrayIcons/{name}.png");
```

## Checklist for Future Menu Bar Icon Projects

### Design Phase
- [ ] Canvas size: 22x22pt (44x44px @2x)
- [ ] Art fills at least 90% of the canvas (minimal transparent padding)
- [ ] Test at actual menu bar size early - don't just eyeball in an image editor
- [ ] Compare side-by-side with native system icons (Wi-Fi, battery, etc.)
- [ ] Use simple, recognizable shapes - fine detail is lost at 22px

### Implementation Phase (Avalonia)
- [ ] Use 1x PNGs (22x22px) at 72 DPI for `TrayIcon.Icon`
- [ ] Keep @2x PNGs (44x44px) available for future native rendering if needed
- [ ] Don't set custom DPI metadata on icon PNGs
- [ ] Don't attempt native NSStatusItem interop to resize - it conflicts with Avalonia

### Implementation Phase (Native/AppKit)
- [ ] Set `[NSImage setSize:NSMakeSize(22, 22)]` for point size
- [ ] Use @2x images (44px) with 22pt point size for crisp Retina rendering
- [ ] Set `[NSImage setTemplate:YES]` if you want automatic dark/light mode
- [ ] Access button via `[statusItem button]`, set image via `[button setImage:]`

### Verification
- [ ] Icon visible and recognizable at actual menu bar size
- [ ] Icon not clipped or distorted
- [ ] Icon changes correctly for all states (active, paused, error, etc.)
- [ ] No console warnings or ObjC exceptions in logs
- [ ] Test on both standard and Retina displays if possible

## Framework-Agnostic Takeaway

> The most common reason a menu bar icon looks "small" is NOT the canvas size - it's that the art doesn't fill the canvas. Always design menu bar icons edge-to-edge with minimal padding. The macOS squircle mask and padding is for dock icons, NOT menu bar icons.
