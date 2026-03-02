# Velopack Build & Publish Guide

## Overview

Eye-Rest uses **Velopack** for auto-update distribution via GitHub Releases. This is separate from the Microsoft Store (MSIX) distribution path.

| Channel | Tool | Update Mechanism |
|---------|------|------------------|
| GitHub (direct download) | Velopack | In-app auto-update |
| Microsoft Store | MSIX | Store-managed updates |

**GitHub Releases repo**: `https://github.com/rockyway/eye-rest`

---

## Prerequisites

```bash
# Install Velopack CLI (one-time)
dotnet tool install -g vpk

# Verify
vpk --version
```

For uploading releases, you need a **GitHub Personal Access Token** with `contents:write` permission on `rockyway/eye-rest`.

---

## Building for Velopack

### Windows

```powershell
# From project root:
.\scripts\publish-velopack-win.ps1 -Version 1.0.2
```

This will:
1. `dotnet publish` — self-contained, win-x64
2. `vpk pack` — creates Velopack release artifacts in `releases/`

Output files in `releases/`:
- `RELEASES` — release manifest
- `EyeRest-1.0.2-full.nupkg` — full package
- `EyeRest-1.0.2-delta.nupkg` — delta package (if previous version exists)
- `EyeRest-Setup.exe` — installer

### macOS

```bash
# From project root:
./scripts/publish-velopack-mac.sh 1.0.2

# With code signing:
SIGNING_IDENTITY="Developer ID Application: ..." \
NOTARY_PROFILE="my-notary-profile" \
./scripts/publish-velopack-mac.sh 1.0.2

# For Intel Macs:
RID=osx-x64 ./scripts/publish-velopack-mac.sh 1.0.2
```

This will:
1. `dotnet publish` — self-contained
2. Patch `Info.plist` with version
3. `vpk pack` — creates Velopack release artifacts in `releases/`

---

## Uploading to GitHub Releases

```bash
# Set your GitHub token
export GITHUB_TOKEN=ghp_xxxxxxxxxxxxx

# Upload
./scripts/upload-velopack-release.sh 1.0.2
```

This creates a GitHub Release tagged `v1.0.2` with all artifacts from `releases/`.

---

## Building for Microsoft Store (MSIX)

MSIX builds exclude Velopack entirely via the `StoreBuild` flag:

```powershell
# For Store submission (unsigned):
.\scripts\build-msix.ps1 -ForStore

# For local sideload testing:
.\scripts\build-msix.ps1
```

The `-p:StoreBuild=true` flag:
- Excludes the Velopack NuGet package
- Defines `STORE_BUILD` compile constant
- All Velopack code is compiled out via `#if !STORE_BUILD`

---

## Version Management

Version is centralized in `Directory.Build.props`:

```xml
<Version>1.0.2</Version>
<AssemblyVersion>1.0.2.0</AssemblyVersion>
<FileVersion>1.0.2.0</FileVersion>
<InformationalVersion>1.0.2</InformationalVersion>
```

Update all four values when bumping the version. The version passed to `vpk pack` must match.

---

## Release Workflow

### Full release cycle:

1. **Bump version** in `Directory.Build.props`
2. **Build & pack** (per platform):
   - Windows: `.\scripts\publish-velopack-win.ps1 -Version X.Y.Z`
   - macOS: `./scripts/publish-velopack-mac.sh X.Y.Z`
3. **Test locally** — install and verify the update flow works
4. **Upload**: `./scripts/upload-velopack-release.sh X.Y.Z`
5. **Verify** — open an older installed version, check for updates

### First-time setup:

The first release (e.g., v1.0.1) creates the initial GitHub Release. Subsequent releases enable delta updates automatically.

---

## How Auto-Update Works

1. On app startup (30s delay), `UpdateService` silently checks GitHub Releases for a newer version
2. Users can also manually check via **About > Check for Updates**
3. If an update is found:
   - Download happens automatically with progress indication
   - User clicks "Restart to Update" to apply
4. Velopack handles the install/restart lifecycle transparently

### Dev mode behavior

When running via `dotnet run`, `IsUpdateSupported` returns `false`. The "Check for Updates" button shows "Updates are not available in this build." This is expected — Velopack only works when the app is installed via its own installer.
