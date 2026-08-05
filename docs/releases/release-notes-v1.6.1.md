# Blink Twice EyeRest — v1.6.1

> Released: August 2026
> Builds on **v1.6.0** — https://github.com/rockyway/eye-rest/releases/tag/v1.6.0

Blink Twice EyeRest runs on **Windows, macOS, and Linux**. Items below note the platform when a
change applies to only one.

---

## Fixes & Improvements

### 🖥 Windows: windows no longer turn invisible after your PC wakes up

- **Windows:** after waking your PC from sleep, an app window could turn completely see-through —
  you'd see your desktop wallpaper where the reminder or the settings window should be. The window
  was still there and still responded to clicks, it just didn't draw anything.
- This was most likely to bite exactly the way the app is meant to be used: left running in the
  system tray for days across many sleep/wake cycles.
- Windows now draws the app in a way that survives waking from sleep, so reminders always show up.

### 🖥 Windows: reminder popups no longer cut off on mixed-scaling monitors

- **Windows:** if your monitors use different scaling levels (for example 100% on one and 150% on
  another), a reminder popup could still appear with its title, countdown or buttons clipped off
  the right and bottom edges.
- v1.6.0 fixed one cause of this, but not all of them — a popup that had already been shown on a
  different monitor could still come back the wrong size. Popups are now always sized for the
  display they actually appear on.

### ⚙️ Windows: the exit confirmation is usable again on mixed-scaling monitors

- **Windows:** on a setup with monitors at different scaling levels, the "Are you sure you want to
  exit?" prompt could appear oversized and cut off, with the **Yes** and **No** buttons pushed
  outside the window — leaving no way to actually confirm.
- The prompt now appears neatly inside the main window and always fits, so **Yes** and **No** are
  always clickable. Pressing **ESC** still cancels.
- The same prompt is used by **Restore Defaults** in settings, which is fixed too.

---

## Platforms in this release

All three platforms update to 1.6.1. The fixes above are **Windows-only**, so on macOS and Linux
this is a maintenance update with no behaviour changes.

---

## Install & Update

- **New install:** download for your platform from the release assets below — Windows
  (`EyeRest-win-Setup.exe`), macOS (`EyeRest-osx-Setup.pkg`), Linux (`EyeRest.AppImage`) — or
  from https://eyerest.net.
- **macOS now has a proper signed installer.** `EyeRest-osx-Setup.pkg` is signed and notarised by
  Apple, so it installs without Gatekeeper warnings — just double-click it. The drag-and-drop
  `EyeRest-osx-Portable.zip` is still there if you prefer it.
- **Existing users:** the app updates itself — open **About → Check for Updates**, or it updates
  silently on next launch.

<!--
DRAFT NOTE (remove before publishing):
Audience: public end users — GitHub Release body on rockyway/eye-rest + eyerest.net.
Baseline = published v1.6.0 release.
Source changes since v1.6.0 (internal ref only — do NOT publish):
  eeffe2f fix(dialog): render the confirmation prompt inside MainWindow  -> section 3
  03a7ef9 fix(render): force software rendering on Windows               -> section 1
  b7f7a2f fix(popup): stop reusing a pooled shell across DPI scalings    -> section 2
  2ddd3d1 fix(r2): make upload-r2.js work with Velopack artifacts        -> OMITTED, release
          tooling only, no user-visible effect.
Note: v1.6.0's note already claimed the multi-monitor popup clipping was fixed (2c24602). That fix
was incomplete, so section 2 is a genuine delta, not a re-announcement — worded to say so plainly
rather than silently repeating the earlier claim.
All three user-facing fixes are Windows-only; macOS/Linux behaviour is unchanged this cycle.
-->
