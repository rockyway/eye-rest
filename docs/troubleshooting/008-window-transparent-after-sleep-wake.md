# 008 — Window content goes fully transparent after sleep/resume

**Reported:** 2026-08-05 — after the PC wakes from sleep/standby, an EyeRest window renders as an
empty see-through rectangle: the desktop wallpaper shows through where the UI should be.

**Fixed by:** forcing `Win32RenderingMode.Software` in `BuildAvaloniaApp()`
(`EyeRest.UI/Program.cs`).

**Origin:** this is a general Avalonia-on-Windows defect, not an EyeRest one. Diagnosed first in a
sibling Avalonia app; the cross-project write-up is
`window-ping/docs/troubleshooting/001-avalonia-window-transparent-after-sleep-wake.md`.

---

## Symptom

After a sleep/resume cycle — and, less often, after a window moves between monitors on different
adapters or scale factors — everything the app paints itself stops appearing. The window is not
otherwise broken:

- it still responds to input (draggable, clickable, closable),
- OS-drawn chrome (DWM border, drop shadow, and on `MainWindow` the system title-bar buttons) keeps
  rendering,
- **nothing is thrown and nothing is logged** — a silent rendering desync, not a crash.

That split is the giveaway: DWM-composited chrome survives, app-painted content does not.

## Why it hits EyeRest harder than a typical app

Two things make the failure total rather than cosmetic here:

1. **Every window is transparent by design.** `PopupWindow`, `MainWindow`, `AboutWindow`,
   `AnalyticsWindow`, `ConfirmDialog`, `DonationCodeDialog` and the dim overlays all set
   `TransparencyLevelHint="Transparent"` with `Background="Transparent"`, and the visible UI is
   painted by inner `Border`s. `PopupWindow` additionally sets `SystemDecorations="None"`, so it has
   no OS chrome to fall back on — when app painting stops, the popup is *entirely* invisible.
2. **EyeRest lives in the tray across many sleep/wake cycles.** The trigger is exactly the usage
   pattern the app is designed for, so a defect that is intermittent elsewhere is routine here.

> Note the false positive this resembles: a window that sets a real `TransparencyLevelHint`
> (Acrylic/Mica/Blur) and has *no* opaque content behind it is transparent **by design**, and a
> compositing failure there is a different bug (missing `TransparencyBackgroundFallback`). EyeRest's
> windows are transparent at the window level but their content is opaque, so a blank window is a
> genuine render failure.

## Root cause

`UsePlatformDetect()` picks a GPU-accelerated path on Windows — Skia over ANGLE/Direct3D or
WGL/OpenGL, depending on the machine. That path owns a swapchain/render surface which DWM composites.

A sleep/resume **resets the GPU driver's device context**, invalidating that surface. Avalonia 11.3
has no hook to detect the reset and recreate the surface, so it goes on presenting into a dead one;
DWM shows the result as empty. Cross-adapter and cross-DPI moves can invalidate it the same way.

This is a known upstream class of issue, not application code:

- Avalonia's own [Windows troubleshooting guide](https://docs.avaloniaui.net/troubleshooting/platform-specific-issues/windows)
  documents this "window appears but shows no content" symptom.
- [AvaloniaUI/Avalonia#14139](https://github.com/AvaloniaUI/Avalonia/issues/14139) — GPU overlay
  interaction causes temporary window transparency.
- [AvaloniaUI/Avalonia#5239](https://github.com/AvaloniaUI/Avalonia/issues/5239) — fully transparent
  window with Direct2D + immediate renderer.

## Fix

`BuildAvaloniaApp()` now passes `Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] }`.
With no GPU surface to lose, the failure mode does not exist. `Program.cs` defines a single
`BuildAvaloniaApp()` and it is the only `AppBuilder` construction site in the repo, so there is no
second overload (e.g. a designer one) left on the old path.

**A hardware-first fallback list does not work here.** `new[] { AngleEgl, Software }` only decides
what is selected *at startup*; Avalonia still cannot recover a surface invalidated later at runtime,
so the bug survives. Only unconditional `Software` removes it.

`Win32PlatformOptions` is read solely by the Win32 backend, so macOS and Linux are unaffected and
keep their own platform defaults.

### Why the performance cost is acceptable

The UI is flat `Border`s, `LinearGradientBrush` fills (`MeshGradientBrush`) and text. A repo-wide
search finds **no** `BoxShadow`, `BlurEffect`, `OpacityMask` or any other `Effect` in the AXAML —
those are the primitives that make software rendering expensive. The largest surfaces are the
full-screen dim overlays, which are solid-colour fills and cheap to blit.

## Tests

Not automatable. Reproduction depends on real GPU driver behaviour on sleep/resume, and the suite
has no headless Avalonia harness (`EyeRest.Tests.Avalonia` exercises plain classes, not real
windows). Verify manually:

1. Launch, confirm the UI renders — main window, an eye-rest popup, a break popup, the dim overlays.
2. Sleep the PC, wake it, trigger a popup. Content must still be visible.
3. On a multi-monitor/mixed-DPI setup, drag the main window between displays and let popups fire on
   each — content keeps rendering, and (per `007`) is not clipped.
4. Repeat a few times: the bug is intermittent and driver-state dependent.

Also confirm the custom chrome still composites correctly under the software renderer — rounded
corners, the transparent popup background, and the dim overlays all depend on window transparency,
which is the one behaviour this change could plausibly alter.
