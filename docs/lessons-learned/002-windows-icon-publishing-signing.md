# 002 - Windows Icon, Publishing & Code Signing Lessons

**Date:** 2026-03-28
**Context:** Setting up Azure Trusted Signing, Velopack publishing, and fixing Windows icon display issues for v1.3.0 release.

---

## 1. Azure Trusted Signing Certificate Rotation

**Problem:** Signing suddenly failed with `403 Forbidden` after working fine days earlier.

**Root Cause:** Azure Trusted Signing auto-rotates certificates on a 3-day rolling window. The auto-rotation silently stopped after 3 days, and all certificates expired. The 403 was not a permissions issue — there were simply no valid certificates left to sign with.

**Fix:** Recreated the `PMTLabs-PublicTrust` certificate profile in Azure Portal, which triggered fresh certificate issuance.

**Lesson:** If Azure Trusted Signing returns 403 and credentials/roles are correct, check certificate expiry dates via:
```bash
az rest --method get --url "https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.CodeSigning/codeSigningAccounts/{account}/certificateProfiles/{profile}?api-version=2024-02-05-preview" --query "properties.certificates[].{created:createdDate, expires:expiryDate, status:status}"
```

**Prevention:** Monitor certificate rotation. If the last cert is approaching its expiry date without a newer one being created, investigate before it expires.

---

## 2. Windows EXE Icon Embedding

**Problem:** Start Menu, taskbar, and Windows search showed a generic/default icon for EyeRest instead of the app's eye icon.

**Root Cause:** Three compounding issues:
1. **No `<ApplicationIcon>` in csproj** — .NET was not embedding any icon into the EXE's Win32 resources. The shortcut at `%APPDATA%\Microsoft\Windows\Start Menu\Programs\EyeRest.lnk` points to `EyeRest.exe,0` for its icon.
2. **Pillow ICO generation bug** — The `generate-icons.py` script was saving the ICO with the smallest image first. Pillow's ICO plugin only embedded the first image (16x16), silently dropping the 32/48/256px sizes. Result: 576-byte ICO with only 16x16.
3. **Wrong icon style** — The generated ICO used a white-background rounded-corner style, while the system tray used a green eye on transparent background (`taskbar-icon.png`).

**Fix:**
1. Added `<ApplicationIcon>..\Resources\app.ico</ApplicationIcon>` to `EyeRest.UI.csproj`
2. Fixed Pillow ICO save by reversing image order (largest first) before calling `.save()`
3. Created `app.ico` from `taskbar-icon.png` (256x256 RGBA) instead of the script-generated icon, so all icons are consistent

**Lesson:**
- Always set `<ApplicationIcon>` in the csproj for Windows apps — Velopack's `--icon` flag only applies to the Setup.exe, not the app EXE
- When generating ICO with Pillow, save with the **largest image first** and pass `append_images` for the rest
- After changing icons, users must uninstall, clear Windows icon cache (`ie4uinit.exe -show`), and reinstall

---

## 3. Pillow ICO Save Gotcha

**Problem:** `images[0].save(path, format="ICO", append_images=images[1:], sizes=...)` only saved 1 image.

**Root Cause:** Pillow's ICO plugin uses the first image as the base and `sizes` to filter — but when the first image is 16x16, it can't upscale to create 256x256. The `append_images` are silently ignored if smaller than the base.

**Fix:**
```python
images_reversed = list(reversed(images))  # largest first (256, 48, 32, 16)
images_reversed[0].save(
    path, format="ICO",
    append_images=images_reversed[1:],
    sizes=[(img.width, img.height) for img in images_reversed],
)
```

**Verification:** Always verify ICO contents after generation:
```python
import struct
with open('app.ico', 'rb') as f:
    _, _, count = struct.unpack('<HHH', f.read(6))
    print(f'{count} images')  # Should be 4, not 1
```

---

## 4. Velopack Update.exe Missing on Uninstall

**Problem:** Clicking "Uninstall" in Windows Apps & Features showed "Windows cannot find Update.exe".

**Root Cause:** Velopack's in-place update overwrote the directory structure, and `Update.exe` was not preserved. The Windows uninstall registry entry still pointed to it.

**Fix:** Manual cleanup:
```bash
rm -rf "$LOCALAPPDATA/EyeRest"
# Remove registry uninstall entry:
powershell "Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*' | Where-Object { $_.DisplayName -like '*EyeRest*' } | ForEach-Object { Remove-Item $_.PSPath -Force }"
```

Then reinstall from the latest Setup.exe.

---

## 5. Windows Avalonia UI Issues

### 5a. Duplicate Caption Buttons
**Problem:** Analytics window showed both custom and system minimize/maximize/close buttons.
**Cause:** `ExtendClientAreaToDecorationsHint="True"` with `ExtendClientAreaChromeHints="PreferSystemChrome"` shows system buttons alongside custom ones on Windows.
**Fix:** Set `NoChrome` in code-behind for Windows only:
```csharp
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
```

### 5b. Right-Side Gap After Tray Restore
**Problem:** Restoring the main window from system tray left a gap on the right edge.
**Cause:** `Hide()`/`Show()` with `ExtendClientAreaToDecorationsHint` leaves stale DWM frame metrics.
**Fix:** Toggle the property to force recalculation:
```csharp
ExtendClientAreaToDecorationsHint = false;
ExtendClientAreaToDecorationsHint = true;
```

### 5c. Taskbar Icon on Secondary Windows
**Problem:** About and Analytics windows showed generic icon in taskbar.
**Fix:** Add `Icon="avares://EyeRest/Assets/taskbar-icon.png"` to AXAML, or set `<ApplicationIcon>` in csproj (covers all windows).

---

## 6. Tray Timer Countdown Not Updating (Windows)

**Problem:** Avalonia TrayIcon context menu showed `Eye Rest: --m | Break: --m` permanently.

**Root Cause:** Windows `SystemTrayService.UpdateTimerDetails()` updated the WinForms `NotifyIcon` tooltip but did not fire the `TimerDetailsUpdated` event. The Avalonia tray menu subscribed to that event but never received updates.

**Fix:** Added `TimerDetailsUpdated?.Invoke(...)` to the Windows implementation of `UpdateTimerDetails()`.

**Lesson:** When a platform service has both a native implementation (WinForms NotifyIcon) and a cross-platform one (Avalonia TrayIcon), ensure both code paths are kept in sync.

---

## 7. Package.appxmanifest Version & Publisher Sync

**Problem:** MS Store submission rejected with "PublisherDisplayName doesn't match".

**Root Cause:** `Package.appxmanifest` had `PublisherDisplayName=TTT Software` while the Partner Center account was `PMT Labs`. Also, the version was stuck at `1.0.1.0` while `Directory.Build.props` was at `1.3.0`.

**Lesson:** Before every Store submission, verify:
1. `<PublisherDisplayName>` matches Partner Center exactly
2. `Version` in appxmanifest matches `Directory.Build.props`
3. `<Identity Publisher="CN=...">` matches the Store certificate subject

---

## Summary Checklist for Windows Releases

- [ ] Version bumped in `Directory.Build.props` AND `Package.appxmanifest`
- [ ] `PublisherDisplayName` in appxmanifest matches Partner Center
- [ ] `app.ico` has all sizes (16/32/48/256) — verify with struct parse, not just file existence
- [ ] `<ApplicationIcon>` set in csproj
- [ ] Azure Trusted Signing certs not expired
- [ ] Build, sign, verify signature: `Get-AuthenticodeSignature`
- [ ] Upload to GitHub release
- [ ] Build MSIX with `-ForStore` flag
- [ ] Upload MSIX to Partner Center
