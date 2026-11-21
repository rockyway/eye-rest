# EyeRest Blink Twice — v1.7.1

> Released: November 2026
> Builds on **v1.7.0** — https://github.com/rockyway/eye-rest/releases/tag/v1.7.0

A fix release. The pause fix applies to Windows, macOS and Linux; the two popup fixes are Windows only.

---

## Highlights

- **Reminders come back on time after a pause** — after a 30 or 60 minute pause, the first eye rest reminder now arrives 20 minutes after the pause ends, not 40.
- [Windows] **Popups stay above the taskbar** — a popup set to Bottom Right or Bottom Center no longer sinks behind it.
- [Windows] **Dragging a popup between monitors works** — it stays under your cursor when the two monitors use different scaling.

---

## What changed

### First reminder after a pause came 20 minutes late

When a 30 or 60 minute pause ended, the timers did resume on time, but the first eye rest
reminder skipped a cycle and arrived about 40 minutes later instead of 20. The app checks how
long it has been since the last reminder to notice when your PC wakes from sleep, and it was
counting the pause as part of that gap. It decided the PC had just woken up and restarted the
countdown. Pausing now resets that check, so the first reminder comes one normal interval after
the pause ends. The same applies when you pause and press Resume yourself.

### Logs kept for 14 days

The app's log files now cover the last 14 days instead of the last day. If something goes wrong
and you report it a few days later, the logs still show what happened.

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
