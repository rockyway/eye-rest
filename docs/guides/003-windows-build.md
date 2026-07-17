# 003 — Windows Build (Velopack)

How to produce the signed Windows installer (`EyeRest-win-Setup.exe`) and Velopack update packages.

> The Velopack packaging step (`vpk pack`) and the `net8.0-windows10.0.19041.0` target **require Windows** — they can't be produced from macOS. So **from macOS you drive a Windows host over SSH**; **on Windows you run the script directly (no SSH)**.

---

## Prerequisites (on the Windows host)

- .NET SDK + `vpk` (Velopack CLI) on PATH.
- For a **signed** build: Azure Trusted Signing credentials in `signing\.env` + `signing\metadata.json` — auto-loaded by the script (omit them for an unsigned build). A signed installer is verified with `CN=PMT Labs LLC`.

---

## Running FROM macOS (SSH to the Windows host)

Host: `ssh tamtr@192.168.50.3` — default shell is **PowerShell**, key-based auth (no password).

Two repo locations exist on the host — pick based on intent:

| Path on host | What it is | Use for |
|---|---|---|
| `M:\sources\demo\eye-rest` | **Network map to the live macOS project** — the *same* working copy as `/Users/tamtran/sources/demo/eye-rest`, including **uncommitted** changes | **Quick test** of local changes *before* committing/pushing — no `git pull`, builds exactly what's on disk here |
| `D:\sources\demo\eye-rest` | A separate **git clone** on the host | **Clean / release** builds — `git pull --ff-only` first to fetch pushed commits |

### Quick test — build the current macOS working copy (no push)
```bash
ssh tamtr@192.168.50.3 'cd M:\sources\demo\eye-rest; powershell -NoProfile -ExecutionPolicy Bypass -File M:\sources\demo\eye-rest\scripts\publish-velopack-win.ps1 -Version X.Y.Z'
```

### Clean / release build — from pushed commits
```bash
ssh tamtr@192.168.50.3 'cd D:\sources\demo\eye-rest; git pull --ff-only; powershell -NoProfile -ExecutionPolicy Bypass -File D:\sources\demo\eye-rest\scripts\publish-velopack-win.ps1 -Version X.Y.Z'
```

---

## Running FROM Windows directly (no SSH)

Just run the script in the repo root:
```powershell
.\scripts\publish-velopack-win.ps1 -Version X.Y.Z
```

---

## Notes

- **`-Version X.Y.Z` is required** — `Directory.Build.props` rejects publishing at the `0.0.0-dev` default.
- Output → `<repo>\releases\`: `EyeRest-win-Setup.exe` (installer), `EyeRest-X.Y.Z-full.nupkg`, `EyeRest-X.Y.Z-delta.nupkg`, `EyeRest-win-Portable.zip`, `RELEASES`.
- Verify the installer signature: `Get-AuthenticodeSignature .\releases\EyeRest-win-Setup.exe` → expect `Status: Valid`, signer `CN=PMT Labs LLC`.
- Upload to GitHub Releases: `GITHUB_TOKEN=<pat> ./scripts/upload-velopack-release.sh X.Y.Z`.
- macOS app build: `scripts/publish-velopack-mac.sh X.Y.Z`. General Velopack reference: [`001-velopack-build-publish.md`](001-velopack-build-publish.md).
