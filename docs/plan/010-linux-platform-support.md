# 010 — Linux Platform Support (EyeRest.Platform.Linux)

**Goal:** Run EyeRest on Linux (initial target: Linux Mint / Cinnamon on X11) with feature parity with Windows/macOS.

**Verified target environment:** Linux Mint (Ubuntu 24.04 base), Cinnamon, Xorg (X11), .NET SDK 8.0.422 at `~/.dotnet`.

## Architecture

New class library `EyeRest.Platform.Linux` (plain `net8.0`, Avalonia-free), mirroring `EyeRest.Platform.macOS`. The Avalonia `TrayIcon`/`NativeMenu`, popups, overlays, and `BundledSoundCache` in `EyeRest.UI` are already cross-platform and work on Linux unchanged.

## Service-by-service plan (13 DI registrations)

| Service | Approach |
|---|---|
| `IDispatcherService` | Not registered — inherit cross-platform `AvaloniaDispatcherService` (same as Windows). |
| `IUrlOpener` | Register shared `EyeRest.Core.DefaultUrlOpener` (`UseShellExecute` → `xdg-open`). |
| `ITimerFactory` / `ITimer` | `LinuxTimer(Factory)` — copy of macOS `System.Threading.Timer` impl (no native code). |
| `ISystemTrayService` | `LinuxSystemTrayService` — copy of macOS event-router/state-holder; `ShowBalloonTip` via `notify-send` (graceful no-op if absent). Visual tray icon stays in Avalonia (`App.axaml.cs`). |
| `IStartupManager` | `LinuxStartupManager` — XDG autostart `.desktop` file at `~/.config/autostart/`, dev-build path guard like macOS. |
| `IScreenOverlayService` | `LinuxScreenOverlayService` — same minimal stub as macOS (real dimming is Avalonia-side). |
| `IUserPresenceService` | `LinuxUserPresenceService` — copy macOS polling state machine verbatim (idle / away / extended-away session reset, injectable `Func<TimeSpan>` test seam); idle probe via X11 `XScreenSaverQueryInfo` (libXss P/Invoke, `X11Interop.cs`). Returns idle 0 (never idle) if X11/libXss unavailable, with warning log. |
| `IScreenDimmingService` | `LinuxScreenDimmingService` — no-op, `IsSupported=false` (same graceful degradation macOS has on modern hardware). |
| `IPauseReminderService` | `LinuxPauseReminderService` — copy macOS timer/state logic; notifications via `notify-send`. |
| `IAudioService` | `LinuxAudioService : AudioServiceBase` + `LinuxWavePlayer` ported from `WindowsWavePlayer` (same RIFF/WAV decoder + PortAudioSharp2 stream; prefer PulseAudio/ALSA host APIs; drop the Windows `SoundPlayer` fallback, fall back to `paplay`/`aplay` process). Defaults: bundled WAVs where cache provides them; otherwise freedesktop theme sounds via `canberra-gtk-play`/`paplay`, else silent. |
| `ISecureStorageService` | `LinuxSecureStorageService` — file-based store at `~/.config/EyeRest/secure-storage.json` with `0600` permissions. (Stores only the donation license key; libsecret can be added later.) |
| `IAppLifecycleService` | `LinuxAppLifecycleService` — minimal, modeled on `WindowsAppLifecycleService`. No watchdog (App Nap is macOS-only). |
| `MacOSIconService` equivalent | Omitted — empty placeholder with no consumers. |

Meeting detection: not registered on macOS or Windows (orchestrator has it disabled) — nothing to do on Linux.

## UI wiring changes

- `EyeRest.UI.csproj`: `PLATFORM_LINUX` define + `linux-*` RID / host-OS-Linux fallback conditions + project reference (mirror the win/osx blocks).
- `App.axaml.cs`: `#elif PLATFORM_LINUX` → `AddLinuxPlatformServices(services)`.
- Window chrome (`MainWindow.axaml.cs:50/:307`, `AnalyticsWindow.axaml.cs:15`): keep **native chrome on Linux** (macOS path) initially; verify visually on the VM and switch to NoChrome if double-chrome artifacts appear.
- All macOS-native helpers already early-return off-macOS; popups rely on `Topmost` on Linux (already handled per comment in `PopupWindow.axaml.cs`).

## Tests

- Add `EyeRest.Platform.Linux` reference to `EyeRest.Tests.Avalonia`.
- Add `LinuxUserPresenceService` state-machine tests mirroring the macOS idle-seam tests.
- Full suite must pass on Windows and on the Linux VM.

## Verification on VM (tam@172.29.103.99, repo `/home/tam/sources/demo/eyerest`)

1. `dotnet build` + `dotnet test` on the VM.
2. Launch under the Cinnamon X session (`DISPLAY=:0`), verify: tray icon + menu + live countdown, eye-rest/break popups + warning popups, audio (bundled WAV via PortAudio and default sounds), idle detection pause/resume (xdotool-free check via XScreenSaver), autostart registration, config at `~/.config/EyeRest/`, analytics dashboard, single-instance activation.

## Risks

- **Wayland:** XScreenSaver idle probe and tray behavior differ; out of scope (target is X11). Service degrades to "never idle" rather than crashing.
- **libportaudio.so** must be installed (`libportaudio2` package); `LinuxWavePlayer` falls back to `paplay`/`aplay` if PortAudio init fails.
- Named-mutex/named-pipe single instance works on Linux but is per-`/tmp` scoped — verify manually.
