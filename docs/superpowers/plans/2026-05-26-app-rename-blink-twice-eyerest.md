# App Rename — "Blink Twice EyeRest" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the app brand from "Eye-Rest" to "Blink Twice EyeRest" across all user-visible surfaces while keeping C# namespaces, project folder names, and the bundle identifier unchanged.

**Architecture:** Purely a string/identifier substitution across manifest files, csproj, AXAML UI files, C# source strings, and shell/PowerShell build scripts. No logic changes. The binary assembly name changes from `EyeRest` to `BlinkTwiceEyeRest`, which cascades to all `avares://EyeRest/` resource URIs (Avalonia loads assets by assembly name).

**Tech Stack:** .NET 8, Avalonia UI 11, macOS plist, Windows MSIX appxmanifest, bash, PowerShell

---

## Files Modified

| File | Change |
|---|---|
| `EyeRest.UI/Info.plist` | CFBundleDisplayName, CFBundleName, CFBundleExecutable |
| `EyeRest.Package/Package.appxmanifest` | DisplayName, VisualElements, StartupTask, Executable paths |
| `EyeRest.UI/EyeRest.UI.csproj` | AssemblyName, AssemblyTitle |
| `EyeRest.UI/App.axaml` | Application Name attribute |
| `EyeRest.UI/App.axaml.cs` | Tray menu strings, tooltip, update toast, avares:// URIs |
| `EyeRest.UI/Views/AboutWindow.axaml` | Window Title, app name TextBlock, avares:// URIs |
| `EyeRest.UI/Views/MainWindow.axaml` | About menu item header, avares:// URI |
| `EyeRest.UI/Views/MainWindow.axaml.cs` | avares:// URI |
| `EyeRest.UI/Views/AnalyticsWindow.axaml` | avares:// URI (window icon) |
| `EyeRest.UI/Views/DonationBannerView.axaml` | Brand string |
| `EyeRest.UI/ViewModels/MainWindowViewModel.cs` | avares:// theme URIs |
| `EyeRest.UI/Services/BundledSoundCache.cs` | avares:// sound URI |
| `scripts/bundle-macos.sh` | APP_NAME, binary chmod path, echo strings |
| `scripts/bundle-macos-mas.sh` | APP_NAME, binary chmod path |
| `scripts/build-msix.ps1` | MSIX output filename, cert CN/friendly name |
| `scripts/publish-release.sh` | Zip filenames, app bundle name |
| `scripts/publish-velopack-win.ps1` | EyeRest.exe vpk entry point |
| `scripts/publish-velopack-win.sh` | MAIN_EXE variable |
| `scripts/upload-r2.js` | Zip names, exe path |

---

## Task 1: Platform Manifests — macOS Info.plist & Windows appxmanifest

**Files:**
- Modify: `EyeRest.UI/Info.plist`
- Modify: `EyeRest.Package/Package.appxmanifest`

> Note: These are metadata-only changes. No tests exist for plist/manifest content; we verify by inspection and build check.

- [ ] **Step 1: Update Info.plist display name and executable**

Edit `EyeRest.UI/Info.plist`. Replace the three keys:

```xml
<key>CFBundleDisplayName</key>
<string>Blink Twice EyeRest</string>
<key>CFBundleExecutable</key>
<string>BlinkTwiceEyeRest</string>
```

And:

```xml
<key>CFBundleName</key>
<string>Blink Twice EyeRest</string>
```

Full updated file:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDisplayName</key>
	<string>Blink Twice EyeRest</string>
	<key>CFBundleExecutable</key>
	<string>BlinkTwiceEyeRest</string>
	<key>CFBundleIconFile</key>
	<string>AppIcon</string>
	<key>CFBundleIdentifier</key>
	<string>com.pmtlabs.eyerest.app</string>
	<key>CFBundleName</key>
	<string>Blink Twice EyeRest</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>1.2.2</string>
	<key>CFBundleVersion</key>
	<string>1.2.2</string>
	<key>ITSAppUsesNonExemptEncryption</key>
	<false/>
	<key>LSApplicationCategoryType</key>
	<string>public.app-category.productivity</string>
	<key>LSMinimumSystemVersion</key>
	<string>12.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>NSSupportsAutomaticGraphicsSwitching</key>
	<true/>
</dict>
</plist>
```

- [ ] **Step 2: Update Package.appxmanifest display names and executable paths**

Edit `EyeRest.Package/Package.appxmanifest`. Make these targeted changes:

Change `<Properties><DisplayName>` from `Eye-Rest` to `Blink Twice EyeRest`:
```xml
<Properties>
  <DisplayName>Blink Twice EyeRest</DisplayName>
  <PublisherDisplayName>PMT Labs</PublisherDisplayName>
  <Logo>Images\StoreLogo.scale-400.png</Logo>
</Properties>
```

Change `Application Executable` and `uap:VisualElements DisplayName`:
```xml
<Application
  Id="EyeRest"
  Executable="BlinkTwiceEyeRest.exe"
  EntryPoint="Windows.FullTrustApplication">

  <uap:VisualElements
    DisplayName="Blink Twice EyeRest"
    Description="Automated eye rest and break reminders for healthier screen time"
    ...>
```

Change startup task display name and extension executable:
```xml
<desktop:Extension Category="windows.startupTask"
                   Executable="BlinkTwiceEyeRest.exe"
                   EntryPoint="Windows.FullTrustApplication">
  <desktop:StartupTask
    TaskId="EyeRestStartup"
    Enabled="true"
    DisplayName="Blink Twice EyeRest" />
```

- [ ] **Step 3: Commit manifests**

```bash
git add EyeRest.UI/Info.plist EyeRest.Package/Package.appxmanifest
git commit -m "rename: update macOS Info.plist and Windows appxmanifest to Blink Twice EyeRest"
```

---

## Task 2: Assembly Name — EyeRest.UI.csproj

**Files:**
- Modify: `EyeRest.UI/EyeRest.UI.csproj`

> Changing `AssemblyName` from `EyeRest` to `BlinkTwiceEyeRest` renames the compiled binary. This also changes the Avalonia assembly name used in `avares://` URIs — Task 3 handles those.

- [ ] **Step 1: Update AssemblyName and AssemblyTitle**

In `EyeRest.UI/EyeRest.UI.csproj`, update the `<PropertyGroup>`:

```xml
<AssemblyName>BlinkTwiceEyeRest</AssemblyName>
<AssemblyTitle>Blink Twice EyeRest</AssemblyTitle>
<AssemblyDescription>Cross-platform eye rest reminder</AssemblyDescription>
```

- [ ] **Step 2: Commit**

```bash
git add EyeRest.UI/EyeRest.UI.csproj
git commit -m "rename: set AssemblyName to BlinkTwiceEyeRest"
```

---

## Task 3: Avalonia Resource URIs — avares://EyeRest/ → avares://BlinkTwiceEyeRest/

**Files:**
- Modify: `EyeRest.UI/App.axaml.cs` (2 URIs)
- Modify: `EyeRest.UI/ViewModels/MainWindowViewModel.cs` (2 URIs)
- Modify: `EyeRest.UI/Views/MainWindow.axaml` (1 URI)
- Modify: `EyeRest.UI/Views/MainWindow.axaml.cs` (1 URI)
- Modify: `EyeRest.UI/Views/AboutWindow.axaml` (2 URIs)
- Modify: `EyeRest.UI/Views/AnalyticsWindow.axaml` (1 URI)
- Modify: `EyeRest.UI/Services/BundledSoundCache.cs` (1 URI)

> Avalonia resolves `avares://<AssemblyName>/path` at runtime. If the assembly name changes but the URIs don't, all icons and sounds will silently fail to load. This task is the most safety-critical.

- [ ] **Step 1: Global replace avares://EyeRest/ in all source files**

Run this command from the repo root to do a safe, targeted replacement across all non-generated source files:

```bash
find EyeRest.UI -type f \( -name "*.axaml" -o -name "*.cs" \) \
  ! -path "*/obj/*" \
  -exec sed -i '' 's|avares://EyeRest/|avares://BlinkTwiceEyeRest/|g' {} +
```

- [ ] **Step 2: Verify no old URIs remain**

```bash
grep -rn "avares://EyeRest/" EyeRest.UI --include="*.axaml" --include="*.cs" | grep -v "obj/"
```

Expected: no output (zero matches).

- [ ] **Step 3: Spot-check two representative replacements**

```bash
grep -n "avares://BlinkTwiceEyeRest" EyeRest.UI/App.axaml.cs | head -4
grep -n "avares://BlinkTwiceEyeRest" EyeRest.UI/Services/BundledSoundCache.cs
```

Expected: lines showing the new URIs in both files.

- [ ] **Step 4: Commit**

```bash
git add EyeRest.UI/App.axaml.cs \
        EyeRest.UI/ViewModels/MainWindowViewModel.cs \
        EyeRest.UI/Views/MainWindow.axaml \
        EyeRest.UI/Views/MainWindow.axaml.cs \
        EyeRest.UI/Views/AboutWindow.axaml \
        EyeRest.UI/Views/AnalyticsWindow.axaml \
        EyeRest.UI/Services/BundledSoundCache.cs
git commit -m "rename: update all avares:// URIs from EyeRest to BlinkTwiceEyeRest assembly"
```

---

## Task 4: In-App UI Strings — Brand Labels

**Files:**
- Modify: `EyeRest.UI/App.axaml`
- Modify: `EyeRest.UI/App.axaml.cs`
- Modify: `EyeRest.UI/Views/AboutWindow.axaml`
- Modify: `EyeRest.UI/Views/MainWindow.axaml`
- Modify: `EyeRest.UI/Views/DonationBannerView.axaml`

> Only brand-identity strings change. Feature labels like "Eye Rest Timer", "Eye Rest Configuration", and popup headings like "Time for an Eye Rest!" are intentionally left unchanged.

- [ ] **Step 1: Update App.axaml application Name**

In `EyeRest.UI/App.axaml`, change the `Name` attribute on the `<Application>` element:

```xml
Name="Blink Twice EyeRest"
```

- [ ] **Step 2: Update App.axaml.cs tray menu strings**

In `EyeRest.UI/App.axaml.cs`, update these string literals:

| Old | New |
|---|---|
| `"Eye Rest v{version} is ready to download. Click here to update."` | `"Blink Twice EyeRest v{version} is ready to download. Click here to update."` |
| `"Eye Rest: --m \| Break: --m"` | Keep as-is (feature/timer label, not brand) |
| `"Show Eye Rest"` | `"Show Blink Twice EyeRest"` |
| `"About Eye-Rest"` | `"About Blink Twice EyeRest"` |
| `"Quit Eye Rest"` | `"Quit Blink Twice EyeRest"` |
| `ToolTipText = "Eye Rest"` | `ToolTipText = "Blink Twice EyeRest"` |

Note: The line `_ => $"Eye Rest {FormatTs(eyeRest)}  |  Break {FormatTs(breakTime)}"` is a tray timer display string showing the feature name — leave it unchanged.

- [ ] **Step 3: Update AboutWindow.axaml**

In `EyeRest.UI/Views/AboutWindow.axaml`:

```xml
Title="About Blink Twice EyeRest"
```

```xml
<TextBlock Text="Blink Twice EyeRest"
```

- [ ] **Step 4: Update MainWindow.axaml About menu item**

In `EyeRest.UI/Views/MainWindow.axaml`:

```xml
<MenuItem Header="About Blink Twice EyeRest" Click="AboutMenuItem_Click" />
```

- [ ] **Step 5: Update DonationBannerView.axaml**

In `EyeRest.UI/Views/DonationBannerView.axaml`:

```xml
Text="If Blink Twice EyeRest helps you, consider supporting its development."
```

- [ ] **Step 6: Commit UI string changes**

```bash
git add EyeRest.UI/App.axaml \
        EyeRest.UI/App.axaml.cs \
        EyeRest.UI/Views/AboutWindow.axaml \
        EyeRest.UI/Views/MainWindow.axaml \
        EyeRest.UI/Views/DonationBannerView.axaml
git commit -m "rename: update in-app brand strings to Blink Twice EyeRest"
```

---

## Task 5: Build Scripts

**Files:**
- Modify: `scripts/bundle-macos.sh`
- Modify: `scripts/bundle-macos-mas.sh`
- Modify: `scripts/build-msix.ps1`
- Modify: `scripts/publish-release.sh`
- Modify: `scripts/publish-velopack-mac.sh`
- Modify: `scripts/publish-velopack-win.ps1`
- Modify: `scripts/publish-velopack-win.sh`
- Modify: `scripts/upload-r2.js`

- [ ] **Step 1: Update bundle-macos.sh**

In `scripts/bundle-macos.sh`:

Change line `APP_NAME="Eye-Rest"`:
```bash
APP_NAME="Blink Twice EyeRest"
```

Change the `chmod` line for the binary:
```bash
chmod +x "$APP_BUNDLE/Contents/MacOS/BlinkTwiceEyeRest"
```

Change the echo header:
```bash
echo "=== Blink Twice EyeRest macOS Bundler ==="
```

- [ ] **Step 2: Update bundle-macos-mas.sh**

In `scripts/bundle-macos-mas.sh`:

Change line `APP_NAME="Eye-Rest"`:
```bash
APP_NAME="Blink Twice EyeRest"
```

Change the `chmod` line:
```bash
chmod +x "$APP_BUNDLE/Contents/MacOS/BlinkTwiceEyeRest"
```

Change the echo header:
```bash
echo "=== Blink Twice EyeRest Mac App Store Bundler ==="
```

- [ ] **Step 3: Update build-msix.ps1**

In `scripts/build-msix.ps1`:

Change the MSIX output path variable:
```powershell
$MsixPath = "$DistDir\BlinkTwiceEyeRest.msix"
```

Change cert subject and friendly name:
```powershell
-Subject "CN=BlinkTwiceEyeRest" `
-FriendlyName "BlinkTwiceEyeRest Dev Test" `
```

Change cert file path:
```powershell
$CertPath = "$DistDir\BlinkTwiceEyeRest-Test.pfx"
```

- [ ] **Step 4: Update publish-release.sh**

In `scripts/publish-release.sh`, update all zip filename references:

```bash
rm -f "$DIST_DIR/BlinkTwiceEyeRest-v${VERSION}-macOS-arm64.zip"
rm -f "$DIST_DIR/BlinkTwiceEyeRest-v${VERSION}-windows-x64-portable.zip"
```

```bash
(cd "$DIST_DIR" && zip -r -y -q "BlinkTwiceEyeRest-v${VERSION}-macOS-arm64.zip" "Blink Twice EyeRest.app")
echo "  ✓ BlinkTwiceEyeRest-v${VERSION}-macOS-arm64.zip"
```

```bash
(cd "$DIST_DIR" && zip -r -q "BlinkTwiceEyeRest-v${VERSION}-windows-x64-portable.zip" win-x64/)
echo "  ✓ BlinkTwiceEyeRest-v${VERSION}-windows-x64-portable.zip"
```

```bash
[ -f "$DIST_DIR/BlinkTwiceEyeRest-v${VERSION}-macOS-arm64.zip" ] && \
    ASSETS="$ASSETS $DIST_DIR/BlinkTwiceEyeRest-v${VERSION}-macOS-arm64.zip"
[ -f "$DIST_DIR/BlinkTwiceEyeRest-v${VERSION}-windows-x64-portable.zip" ] && \
    ASSETS="$ASSETS $DIST_DIR/BlinkTwiceEyeRest-v${VERSION}-windows-x64-portable.zip"
```

```bash
ls -lh "$DIST_DIR"/BlinkTwiceEyeRest-v${VERSION}-*.zip
```

- [ ] **Step 5: Update publish-velopack-mac.sh**

In `scripts/publish-velopack-mac.sh`, change the `-e` (executable) argument. Leave `-u EyeRest` unchanged (Velopack update ID — changing it breaks existing user update continuity):

```bash
-e BlinkTwiceEyeRest \
```

- [ ] **Step 6: Update publish-velopack-win.ps1**

In `scripts/publish-velopack-win.ps1`, change the vpk pack arguments to reference the new executable. Leave `"-u", "EyeRest"` unchanged:

```powershell
"-e",  "BlinkTwiceEyeRest.exe"
```

- [ ] **Step 7: Update publish-velopack-win.sh**

In `scripts/publish-velopack-win.sh`, change:

```bash
MAIN_EXE="BlinkTwiceEyeRest.exe"
```

- [ ] **Step 8: Update upload-r2.js**

In `scripts/upload-r2.js`, update the zip names and exe path:

```js
const macZipDist = join(distDir, "BlinkTwiceEyeRest-macOS-arm64.zip");
const macZipPub = join(publishDir, "BlinkTwiceEyeRest-macOS-arm64.zip");
...
files.push({ path: macZipDist, name: "BlinkTwiceEyeRest-macOS-arm64.zip", type: "application/zip" });
...
files.push({ path: macZipPub, name: "BlinkTwiceEyeRest-macOS-arm64.zip", type: "application/zip" });
...
const winZip = join(publishDir, "BlinkTwiceEyeRest-Windows-x64.zip");
...
files.push({ path: winZip, name: "BlinkTwiceEyeRest-Windows-x64.zip", type: "application/octet-stream" });
...
"net8.0-windows10.0.19041.0", "win-x64", "publish", "BlinkTwiceEyeRest.exe"
...
files.push({ path: winExe, name: "BlinkTwiceEyeRest.exe", type: "application/octet-stream" });
```

- [ ] **Step 9: Commit build scripts**

```bash
git add scripts/bundle-macos.sh \
        scripts/bundle-macos-mas.sh \
        scripts/build-msix.ps1 \
        scripts/publish-release.sh \
        scripts/publish-velopack-mac.sh \
        scripts/publish-velopack-win.ps1 \
        scripts/publish-velopack-win.sh \
        scripts/upload-r2.js
git commit -m "rename: update build scripts for BlinkTwiceEyeRest binary name"
```

---

## Task 6: Build Verification

- [ ] **Step 1: Clean and build**

```bash
dotnet build EyeRest.UI/EyeRest.UI.csproj -c Debug
```

Expected: `Build succeeded` with 0 errors. The output binary will be named `BlinkTwiceEyeRest` (macOS) or `BlinkTwiceEyeRest.exe` (Windows).

- [ ] **Step 2: Confirm binary name**

On macOS:
```bash
ls EyeRest.UI/bin/Debug/net8.0/ | grep -i "Blink\|blink"
```

Expected output includes `BlinkTwiceEyeRest` (no extension on macOS).

- [ ] **Step 3: Confirm no stale avares:// URIs remain**

```bash
grep -rn "avares://EyeRest/" EyeRest.UI --include="*.axaml" --include="*.cs" | grep -v "obj/"
```

Expected: no output.

- [ ] **Step 4: Final commit if any loose files**

```bash
git status
# If any untracked/modified files remain, add and commit them
```
