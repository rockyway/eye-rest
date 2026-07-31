# 007 — Eye-rest warning popup clipped on a mixed-DPI setup (pooled shell)

**Reported:** 2026-07-30 — the "Eye Rest Starting Soon" popup rendered with its title and
countdown cut off on the right and bottom, top-left corner intact.

**Fixed by:** per-scaling selection in `PopupWindow.Rent()` (`EyeRest.UI/Views/PopupWindow.axaml.cs`).

**Earlier, incomplete fix:** 2c24602 `fix(popup): re-fit popup to content on DPI/scaling change`.

---

## Symptom

The popup's frame and its content are rendered at *different* scales. Measured from the reported
screenshot: the eye icon circle is `Width="56"` DIP but rendered **84 px** (= 150 %), inside a frame
only **260 × 226 px** — roughly the content's *DIP* size, i.e. a frame sized as if scaling were 1.0.
The content therefore overflows the frame and is clipped on the right and bottom, while the
top-left corner looks perfectly normal.

The mirror image (frame too large, content too small, leaving a gap) is the same defect with the
stale value pointing the other way.

## Root cause

`PopupWindow` pools window shells (`Rent()` / `ReleaseToPool()`) so the macOS automation-peer leak
is contained — see `docs/plan/009`. A pooled shell is `Hide()`n, then re-`Show()`n later for a
different popup, possibly on a different monitor.

Avalonia 11.3 establishes a window's `RenderScaling` in exactly two places:

1. when the native window is created, and
2. on `WM_DPICHANGED`.

Windows does **not** deliver `WM_DPICHANGED` to a hidden window that is moved and then re-shown.
On top of that, the Win32 `WindowImpl.Position` setter (`WindowImpl.cs:528-540`) silently re-reads
the monitor DPI into its private `_scaling` field **without raising `ScalingChanged`**, and
`PositionOnScreen()` / `RepositionWithActualSize()` set `Position` on every show.

So a shell rented from a different-DPI monitor keeps a **stale `RenderScaling`**, and the frame size
and the content render scale end up derived from two different monitors.

### Why 2c24602 did not fix it

That commit hangs its correction off `ScalingChanged` — which is never raised on this path — and
re-fits via a `SizeToContent` toggle, which does nothing about a stale `RenderScaling`. It does
correctly handle *dragging a visible popup* across a DPI boundary (`WM_DPICHANGED` does fire then),
so it is still in place.

## Evidence

A harness driving the real `PopupWindow` + `EyeRestWarningPopup` across this machine's monitors
(100 % / 150 % / 175 %), showing the popup on the monitor under the cursor:

| step | monitor | `RenderScaling` | real client px | verdict |
|------|---------|-----------------|----------------|---------|
| show on 100 %          | 1.0  | 1.0 | 258 × 232 | ok |
| re-show pooled @ 150 % | 1.5  | **1.0 — stale** | 387 × 348 | **desynced** |

A *freshly constructed* shell on that same 150 % monitor was always correct (`RenderScaling` 1.5,
frame 388 × 350 = `DesiredSize` × 1.5), as was every same-scaling reuse.

## Fix

`Rent()` only hands back a pooled shell whose `RenderScaling` matches the scaling of the monitor
under the cursor (the monitor `PositionOnScreen()` is about to place the popup on). Non-matching
shells are pushed back so they stay available for their own monitor; when nothing matches, a fresh
shell is built. Pooling is therefore preserved for every same-scale reuse.

The monitor scaling is read with `MonitorFromPoint` + `GetDpiForMonitor`. On non-Windows, or if the
query fails, the previous unconditional reuse is kept — this is a Windows per-monitor-DPI defect.

After the fix, all three scalings agree in every direction (`real client px == DesiredSize × scale`):
258 px @ 100 %, 388 px @ 150 %, 452 px @ 175 %.

## Known residual

If the cursor crosses a DPI boundary in the milliseconds between `Rent()` and `PositionOnScreen()`,
the shell can still be rented for the wrong monitor. The window is tiny and this is no worse than
the previous behaviour; the `ScalingChanged` handler covers it once the window is visible.

## Tests

`EyeRest.Tests.Avalonia/Views/PopupWindowShellPoolTests.cs` — pool selection: matching scaling is
reused, non-matching is refused and retained, adjacent Windows scale factors (1.0/1.25/1.5/1.75/2.0)
are never confused, and the pool cap is respected. The rendering itself needs real multi-monitor DPI
and is not automatable in the headless suite.
