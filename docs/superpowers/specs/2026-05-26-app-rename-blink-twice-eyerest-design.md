# Design Spec: App Rename — "Blink Twice EyeRest"

**Date:** 2026-05-26  
**Status:** Approved  

---

## Summary

Rename the app brand from **Eye-Rest** to **Blink Twice EyeRest** across all user-visible surfaces: macOS App Store metadata, Windows Store metadata, the compiled binary name, and in-app UI strings. C# namespaces, project folder names, and the bundle identifier are intentionally left unchanged to preserve App Store continuity and minimize refactor risk.

---

## Decisions

| Decision | Choice | Reason |
|---|---|---|
| Bundle ID | Keep `com.pmtlabs.eyerest.app` | Changing it creates a new App Store app — loses ratings and update continuity |
| Binary name | Rename to `BlinkTwiceEyeRest` | User explicitly requested binary rename |
| C# namespaces | Keep `EyeRest.*` | Not user-visible; renaming touches hundreds of files for no user benefit |
| Feature labels | Keep "Eye Rest Timer / Configuration" | These describe the feature concept, not the brand |

---

## Change Surface 1 — macOS Info.plist

File: `EyeRest.UI/Info.plist`

| Key | Before | After |
|---|---|---|
| `CFBundleDisplayName` | `Eye-Rest` | `Blink Twice EyeRest` |
| `CFBundleName` | `Eye-Rest` | `Blink Twice EyeRest` |
| `CFBundleExecutable` | `EyeRest` | `BlinkTwiceEyeRest` |

---

## Change Surface 2 — Windows Package Manifest

File: `EyeRest.Package/Package.appxmanifest`

| Element | Before | After |
|---|---|---|
| `<Properties><DisplayName>` | `Eye-Rest` | `Blink Twice EyeRest` |
| `<uap:VisualElements DisplayName>` | `Eye-Rest` | `Blink Twice EyeRest` |
| `<desktop:StartupTask DisplayName>` | `Eye Rest` | `Blink Twice EyeRest` |
| `Application Executable` | `EyeRest.exe` | `BlinkTwiceEyeRest.exe` |
| Extension `Executable` | `EyeRest.exe` | `BlinkTwiceEyeRest.exe` |

Note: `Identity Name="EyeRestSoftware.Eye-Rest"` is **not** changed — this is the Windows Store package identity and changing it would break update continuity (same as bundle ID on macOS).

---

## Change Surface 3 — Project File (assembly/binary name)

File: `EyeRest.UI/EyeRest.UI.csproj`

| Property | Before | After |
|---|---|---|
| `<AssemblyName>` | `EyeRest` | `BlinkTwiceEyeRest` |
| `<AssemblyTitle>` | `Eye-Rest` | `Blink Twice EyeRest` |

---

## Change Surface 4 — In-App UI Strings (brand only)

### `EyeRest.UI/App.axaml`
- `Name="Eye-Rest"` → `Name="Blink Twice EyeRest"`

### `EyeRest.UI/App.axaml.cs`
- Tray menu: `"Show Eye Rest"` → `"Show Blink Twice EyeRest"`
- Tray menu: `"About Eye-Rest"` → `"About Blink Twice EyeRest"`
- Tray menu: `"Quit Eye Rest"` → `"Quit Blink Twice EyeRest"`
- Tray tooltip: `"Eye Rest"` → `"Blink Twice EyeRest"`
- Update toast: `"Eye Rest v{version} is ready to download"` → `"Blink Twice EyeRest v{version} is ready to download"`

### `EyeRest.UI/Views/AboutWindow.axaml`
- Window `Title="About Eye-Rest"` → `"About Blink Twice EyeRest"`
- App name TextBlock: `"Eye-Rest"` → `"Blink Twice EyeRest"`

### `EyeRest.UI/Views/MainWindow.axaml`
- Menu item: `Header="About Eye-Rest"` → `"About Blink Twice EyeRest"`

### `EyeRest.UI/Views/DonationBannerView.axaml`
- `"If Eye-Rest helps you, consider supporting its development."` → `"If Blink Twice EyeRest helps you, consider supporting its development."`

---

## Change Surface 5 — Build Scripts

### `scripts/bundle-macos.sh`
- `APP_NAME="Eye-Rest"` → `APP_NAME="Blink Twice EyeRest"`
- `chmod +x "$APP_BUNDLE/Contents/MacOS/EyeRest"` → `…/MacOS/BlinkTwiceEyeRest`
- Any other hardcoded `EyeRest` binary paths inside the bundle

### `scripts/bundle-macos-mas.sh`
- Same changes as `bundle-macos.sh`

### `scripts/build-msix.ps1`
- `$MsixPath = "$DistDir\EyeRest.msix"` → `…\BlinkTwiceEyeRest.msix`
- Certificate subject/friendly name references to `EyeRest`

---

## What Is NOT Changed

- `com.pmtlabs.eyerest.app` bundle identifier
- `EyeRestSoftware.Eye-Rest` Windows package identity name
- C# namespaces (`EyeRest.UI`, `EyeRest.Core`, `EyeRest.Abstractions`)
- Project folder names (`EyeRest.UI/`, `EyeRest.Core/`)
- Feature labels: "Eye Rest Timer", "Eye Rest Configuration", "Time for an Eye Rest!", "Eye Rest Starting Soon"
- `avares://EyeRest/...` Avalonia resource URIs (these reference the assembly name and will need updating after the assembly is renamed)

---

## Side Effect: Avalonia Resource URIs

**Important:** Avalonia resource URIs use the assembly name. After renaming `AssemblyName` from `EyeRest` to `BlinkTwiceEyeRest`, all `avares://EyeRest/...` URIs in AXAML files must become `avares://BlinkTwiceEyeRest/...`. These are currently in:
- `AboutWindow.axaml` (icon assets)
- Any other AXAML files referencing `avares://EyeRest/`

---

## Acceptance Criteria

1. `dotnet build` succeeds with no errors
2. macOS `.app` bundle is named `Blink Twice EyeRest.app`, executable inside is `BlinkTwiceEyeRest`
3. Windows task manager shows process as `BlinkTwiceEyeRest.exe`
4. About dialog shows "Blink Twice EyeRest"
5. Tray menu shows "Blink Twice EyeRest" in all items
6. App Store metadata fields (`CFBundleDisplayName`, Windows `DisplayName`) show "Blink Twice EyeRest"
7. No broken `avares://` resource URIs
