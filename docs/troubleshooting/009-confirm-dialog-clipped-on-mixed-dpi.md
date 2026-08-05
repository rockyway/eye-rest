# 009 — Exit/Restore confirmation unusable on a mixed-DPI setup

**Reported:** 2026-08-05 — the exit confirmation rendered with its message cut off mid-word and the
**Yes / No buttons entirely outside the window frame**, so the prompt could be cancelled with ESC
but never accepted.

**Fixed by:** replacing the top-level `ConfirmDialog` window with an in-window overlay in
`MainWindow` (`ShowConfirmAsync`). `EyeRest.UI/Views/ConfirmDialog.axaml{,.cs}` deleted.

**Pre-existing:** confirmed by A/B — the defect reproduces identically on the build before the
`Win32RenderingMode.Software` change of `008`, so it is unrelated to that fix. It shipped in v1.6.0
and earlier.

---

## Symptom

The card paints ~1.5× too large, anchored at its top-left, and is clipped on the right and bottom.
The message reads `Are you sure you want to exit Eye` and stops at the frame edge; the Yes/No row
and the "Press ESC to cancel" hint fall outside the frame entirely.

Only reproduces when the owner window sits on a monitor whose scale **differs** from the primary
monitor's. On a matched-scale setup the prompt looks correct.

## Root cause

Same mechanism as `007`, reached by a different path — this time via
`WindowStartupLocation="CenterOwner"` rather than the popup shell pool.

1. `ShowDialog(owner)` creates the dialog's native window. Win32 `CW_USEDEFAULT` places a new HWND
   on the **primary** monitor, so Avalonia establishes the window's `RenderScaling` — and builds
   its **render surface** — at the primary's scale (1.5 on the reported machine).
2. `CenterOwner` then moves the window onto the owner's monitor (scale 1.0).
3. The Win32 `WindowImpl.Position` setter silently re-reads the new monitor's DPI into its private
   `_scaling` field **without raising `ScalingChanged`**, and Windows delivers no `WM_DPICHANGED`
   to a window positioned before it is shown.

Layout therefore switches to the correct 1.0 while the **render surface keeps the 1.5 it was built
with**, and nothing rebuilds it.

### Why this one is worse than 007

After step 3 the public `RenderScaling` reads **1.0 — the correct value**. The mismatch lives
entirely in the render surface, so *managed code cannot detect it at all*. There is no property to
compare and no event to hook.

### Measurements

Owner on a 1.0 monitor, primary at 1.5:

| | value |
|---|---|
| dialog native window / client | 420 × 220 px |
| `GetDpiForWindow` | 96 → scale **1.0** (correct for its monitor) |
| content paint scale | **1.5** |
| content needed | 630 × 330 px |

Predicted icon centre from a "laid out at 1.0, painted at 1.5 from top-left" model: **(455, 132)**.
Observed: **(452, 125)**. A "content simply does not fit" model does not reproduce the offset.

## Two failed fixes (do not retry these)

1. **`ScalingChanged` → re-fit.** The event never fires on this path. This is the same wall
   `2c24602` hit for the popup, and why `b7f7a2f` had to change `Rent()` instead.
2. **`SizeToContent="WidthAndHeight"` + forced re-fit in `OnOpened`.** This *did* correct the frame
   (420 × 220 → 420 × 247, exposing a real 27 DIP content overflow that existed at any scale) and
   proved layout runs at 1.0 — but the card still painted at 1.5, because `SizeToContent` resizes
   the frame and never rebuilds the render surface.

Both fixes address the frame. The defect is in the surface.

## Fix

Do not give the prompt a window of its own. `MainWindow.ShowConfirmAsync(message)` shows a `Border`
overlay (`ConfirmOverlay`) spanning the window and completes a `TaskCompletionSource<bool>` when
the user answers, preserving the `await`-a-bool contract both callers already used.

With no second HWND there is no creation monitor and no separate render surface, so the failure
mode cannot occur: the overlay always paints at `MainWindow`'s scale, which is always correct
because a **visible** window does receive `WM_DPICHANGED` when dragged across a boundary.

Incidental improvements: the prompt is now bounded by the window (the old 420 DIP dialog overhung
the 340 DIP window), and it carries its own dim, so `ShowDimOverlay`/`HideDimOverlay` is no longer
paired around it.

Both callers — `ExitApplication` and `RestoreDefaults` — go through the same helper and are fixed
together. The tray's Exit shuts down directly and never showed a confirmation, so it is unaffected.

## Tests

Not automatable: reproduction needs two real monitors at different scale factors, and the suite has
no headless Avalonia harness. Verify manually on a mixed-DPI setup, with the main window on a
monitor whose scale differs from the primary's:

1. Click **Exit** — the card appears inside the window, fully visible, with Yes/No clickable.
2. ESC cancels; **No** cancels; **Yes** exits.
3. Settings → **Restore Defaults** shows the same prompt correctly.
4. Drag the main window across a DPI boundary and repeat — the overlay follows the window's scale.
