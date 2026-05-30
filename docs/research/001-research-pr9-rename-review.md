# Research: PR #9 Rename Review — Blink Twice EyeRest
_Date: 2026-05-26_

## Summary

Reviewed `git diff develop...feature/rename-blink-twice-eyerest`. Build passes and all Avalonia `avares://` URIs are updated, but runtime user-facing platform notifications/tooltips and release/install script outputs still show the old brand name in several platform service files.

## Verdict: FAIL → Fixed in follow-up commit

## Findings

1. **MISSED BRAND** `EyeRest.UI/Views/MainWindow.axaml:63` — `Text="Eye Rest"` renders the top app title bar (brand, not feature label)
2. **MISSED BRAND** `EyeRest.Platform.Windows/Services/SystemTrayService.cs:68` — `EyeRest Application`; `:302`, `:340` — `baseText = "EyeRest"`; `:328` — fallback tooltip `EyeRest - ...`
3. **MISSED BRAND** `EyeRest.Platform.Windows/Services/PauseReminderService.cs:502`, `:536`, `:567` — toast titles `EyeRest - ...`
4. **MISSED BRAND** `EyeRest.Platform.macOS/Services/MacOSPauseReminderService.cs:171`, `:188` — notification titles `Eye Rest - ...`; `EyeRest.Platform.macOS/Services/MacOSAppLifecycleService.cs:65` — `Eye-Rest` in App Nap activity reason
5. **SCRIPTS** `scripts/install-local.ps1:25`, `scripts/upload-velopack-release.sh:39`, `scripts/publish-release.sh:55`, `scripts/publish-velopack-win.sh:47`, `scripts/publish-velopack-win.ps1:66`, `scripts/publish-velopack-mac.sh:37` — still emit `Eye-Rest` in output/log strings
6. **TRAILING WHITESPACE** `docs/superpowers/specs/2026-05-26-app-rename-blink-twice-eyerest-design.md:3`, `:4`

## Pre-existing Issues (not caused by this PR — not fixed here)

7. `scripts/bundle-macos-mas.sh:37` — references untracked `EyeRest.MAS.entitlements` (clean checkout will fail)
8. `scripts/bundle-macos-mas.sh:35` — hardcodes version `1.4.1` instead of reading from csproj

## Already Correct

- No `avares://EyeRest/` URIs remain in source
- All script binary name references updated to `BlinkTwiceEyeRest`
- `MainWindow.axaml:170`, `:384`, `:462`, `DailyDetailsTab.axaml:27` — intentionally kept as feature labels

## Files Examined

- `EyeRest.UI/Views/MainWindow.axaml`
- `EyeRest.Platform.Windows/Services/SystemTrayService.cs`
- `EyeRest.Platform.Windows/Services/PauseReminderService.cs`
- `EyeRest.Platform.macOS/Services/MacOSPauseReminderService.cs`
- `EyeRest.Platform.macOS/Services/MacOSAppLifecycleService.cs`
- `scripts/install-local.ps1`
- `scripts/upload-velopack-release.sh`
- `scripts/publish-release.sh`
- `scripts/publish-velopack-win.sh`
- `scripts/publish-velopack-win.ps1`
- `scripts/publish-velopack-mac.sh`
