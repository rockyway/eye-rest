# EyeRest Blink Twice — v1.7.1

> Released: November 2026
> Builds on **v1.7.0** — https://github.com/rockyway/eye-rest/releases/tag/v1.7.0

A small fix release for Windows. macOS and Linux are rebuilt at the same version and behave as before.

---

## Highlights

- [Windows] **Popups stay above the taskbar** — a popup set to Bottom Right or Bottom Center no longer sinks behind it.
- [Windows] **Dragging a popup between monitors works** — it stays under your cursor when the two monitors use different scaling.

---

## What changed

### Popup sinking under the taskbar

With the popup position set to Bottom Right, the bottom of the popup could end up behind the
taskbar, hiding its last line and buttons. It happened on setups with mixed monitor scaling:
the popup was placed using the scale of the monitor your mouse was on, which isn't always the
monitor the popup opened on. Popups now stay on the monitor they opened on, and the app checks
the popup's real size after placing it and moves it up if any part is under the taskbar.

### Dragging across monitors with different scaling

Dragging a popup from a 100% monitor to a 150% one made it jump away from the mouse, as if
chasing it. The app was moving the popup back to its configured corner in the middle of the
drag. Once you start dragging, the position is yours. The popup resizes for the new monitor
and stays where you drop it.

---

## Install & Update

- **New install:** download for your platform from the release assets below: Windows
  (`EyeRest-win-Setup.exe`), macOS (`EyeRest-osx-Setup.pkg`) or Linux (`EyeRest.AppImage`). You
  can also get them from https://eyerest.net.
- **Existing users:** the app updates itself. Open **About → Check for Updates**, or wait for the
  silent update on next launch.
