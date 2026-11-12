# EyeRest Blink Twice — v1.7.0

> Released: November 2026
> Builds on **v1.6.1** — https://github.com/rockyway/eye-rest/releases/tag/v1.6.1

Everything here applies to Windows, macOS and Linux.

---

## Highlights

- **Smaller reminder popups** — pick Standard, Compact or Minimal in settings.
- **Choose where the break popup appears** — the break warning shows up in the same spot. Default is Center.
- **Turn off the break overlay** — get the break reminder without the screen dimming behind it.
- **A changelog when you update** — the highlights show before you update and once afterwards.
- **New display name** — the app is now called EyeRest Blink Twice.

---

## What changed

### Popup display mode

The popups used to come in one size. Settings now has a Standard, Compact and Minimal option, and
it applies to the eye rest popup, the break popup and both warnings. The countdown and buttons stay
fully visible at every size. Earlier builds of the smaller sizes clipped the break popup's buttons,
and that's fixed.

### Break popup position and overlay

The break popup has a new Popup Position setting, set to Center by default. The break warning now
follows it, so the warning and the break no longer appear in different places. There is also a
Show Overlay switch for the break popup, in case you don't want the dimmed background.

### Changelog when updating

Before an update installs, the app lists what's in the new version. After the update it shows the
same list once and doesn't repeat it.

### New display name

The window titles, tray, About dialog and installers now say EyeRest Blink Twice. Your settings
and data are unchanged.

---

## Install & Update

- **New install:** download for your platform from the release assets below: Windows
  (`EyeRest-win-Setup.exe`), macOS (`EyeRest-osx-Setup.pkg`) or Linux (`EyeRest.AppImage`). You
  can also get them from https://eyerest.net.
- **Existing users:** the app updates itself. Open **About → Check for Updates**, or wait for the
  silent update on next launch.

<!--
DRAFT NOTE (remove before publishing):
Audience: public end users — GitHub Release body on rockyway/eye-rest + eyerest.net.
Baseline = published v1.6.1 release. Source changes since v1.6.1 (internal ref only):
  eea784e/6bf37d8/01c9816/6305f5e/2fd3313  popup display mode + button clipping fix
  1140b86, edb60e3                        break popup position + warning follows
  d7cec0f/eea784e                         break overlay toggle
  9470d59                                 changelog popup
  cbc7a6f                                 display-name rename
Setting labels checked against MainWindow.axaml: "Show Overlay", "Popup Position".
Not yet checked: the exact label of the display-mode control.
-->
