# 002 — macOS Production Build & Notarization Guide

How to produce a signed + notarized + stapled `Eye-Rest.app` ready for direct
distribution outside the Mac App Store. Customers on any modern macOS will be
able to download and open the resulting `.app` without seeing a Gatekeeper
"unidentified developer" warning.

| Field | Value |
|-------|-------|
| Team | PMT Labs LLC |
| Team ID | `68M75D67LJ` |
| Bundle ID | `com.pmtlabs.eyerest.app` |
| Cert type required | `Developer ID Application` (NOT `Apple Distribution`, NOT `Apple Development`) |
| Target | `osx-arm64` (Apple Silicon, self-contained .NET 8) |
| Output | `dist/Eye-Rest.app` (~109 MB) |
| Build script | `scripts/bundle-macos.sh` |

---

## 1. The mental model

Three independent steps, three different credentials, three different failure
modes. Mixing them up is the most common source of confusion.

| Step | What it proves | Credential | Tool |
|------|----------------|------------|------|
| **Code sign** | "PMT Labs LLC produced this binary" | `Developer ID Application` cert in keychain | `codesign` |
| **Notarize** | "Apple scanned this and found no malware" | App Store Connect API key (`.p8` + Key ID + Issuer ID) | `notarytool` |
| **Staple** | Attaches the notarization ticket to the bundle so it works offline | (none — uses bundle's `cdhash` only) | `stapler` |

A bundle can be signed but not notarized (runs on the dev's Mac only).
A bundle can be notarized but not stapled (runs anywhere with internet).
For a production `.app` you want **all three**.

---

## 2. One-time setup

### 2.1 Install the `Developer ID Application` certificate

This is separate from the Bundle ID registration on the Apple Developer
Portal. Registering a bundle ID does NOT create the signing cert.

1. **Generate a CSR locally.**
   - Open `Keychain Access` → menu → **Certificate Assistant → Request a
     Certificate from a Certificate Authority…**
   - Email: your Apple ID email
   - Common Name: `PMT Labs LLC`
   - Request is: **Saved to disk** + tick "Let me specify key pair information"
   - Key Size: 2048, Algorithm: RSA
   - Save to `~/Downloads/CertificateSigningRequest.certSigningRequest`

   > The CSR file contains only the **public** key. The matching **private**
   > key stays in your login keychain — that's what `codesign` will use. If
   > you wipe this Mac without exporting the cert as `.p12`, you'll lose the
   > ability to sign with this identity.

2. **Mint the cert on the portal.**
   - Visit https://developer.apple.com/account/resources/certificates/list
   - **Top-right team switcher → choose PMT Labs LLC `(68M75D67LJ)`**
     (most common mistake: staying on a personal team, which produces a cert
     with the wrong team ID)
   - Click **+** → choose **Developer ID Application** (NOT "Developer ID
     Installer" — that's for `.pkg` files)
   - Continue → Profile Type: **G2 Sub-CA** (newer; valid through 2035)
   - Upload the CSR → download the `.cer` → double-click to install into
     login keychain.

3. **Verify.**
   ```bash
   security find-identity -v -p codesigning | grep "68M75D67LJ"
   ```
   You must see a line like:
   ```
   3) FD68C00B4555... "Developer ID Application: PMT Labs LLC (68M75D67LJ)"
   ```
   If this command prints nothing, signing will fail.

### 2.2 Create an App Store Connect API key

This is for the `notarize` step. **The `.p8` file can only be downloaded
ONCE** from App Store Connect — keep a secure backup.

1. Visit https://appstoreconnect.apple.com/access/integrations/api
2. Click **+** → role: **Developer** → generate.
3. Download the `.p8` immediately. Note the **Key ID** (10 chars) and
   **Issuer ID** (UUID).
4. Move the `.p8` somewhere safe and lock down permissions:
   ```bash
   mv ~/Downloads/AuthKey_XXXXXXXXXX.p8 ~/key/
   chmod 600 ~/key/AuthKey_XXXXXXXXXX.p8
   ```

### 2.3 Confirm Xcode tools are present

```bash
xcrun --find codesign       # /Applications/Xcode.app/.../codesign
xcrun --find notarytool     # /Applications/Xcode.app/.../notarytool
xcrun --find stapler        # /Applications/Xcode.app/.../stapler
```

If any of these fail: install Xcode from the App Store, or
`xcode-select --install` for command-line tools.

---

## 3. Producing a production build

After the one-time setup above, the build is a single command.

### 3.1 The recommended invocation

```bash
NOTARIZE=1 \
NOTARY_KEY_ID=33Y825QZ7R \
NOTARY_ISSUER_ID=d0e5f0c0-fac5-4f41-98ce-aeb6abe4b609 \
NOTARY_KEY_PATH=/Users/tamtran/key/AuthKey_33Y825QZ7R.p8 \
./scripts/bundle-macos.sh
```

What this runs:

1. `dotnet publish` → `osx-arm64`, Release, self-contained
2. Bundle structure (`Eye-Rest.app/Contents/{MacOS,Resources,Info.plist}`)
3. `codesign` every Mach-O inside-out (dylibs → executables → bundle seal)
4. `codesign --verify` sanity check
5. `notarytool submit --wait` — uploads zip, blocks until Apple verdict
6. `stapler staple` + `stapler validate` + `spctl --assess` for final verification

Total wall time on a healthy day: **2–5 minutes**.

### 3.2 What "success" looks like

The final lines should print:

```
The staple and validate action worked!
/Users/tamtran/.../dist/Eye-Rest.app: accepted
source=Notarized Developer ID
```

The `source=Notarized Developer ID` line is the definitive Gatekeeper
verdict — that's what tells you the `.app` will open on any customer's Mac
without the "unidentified developer" warning.

### 3.3 Sign-only (no notarization) for local dev

If you just want a locally-runnable `.app` and don't need it to open on
other Macs:

```bash
./scripts/bundle-macos.sh    # NOTARIZE defaults to 0
```

The resulting `.app` opens on your Mac but Gatekeeper will reject it on any
other machine. Useful for fast iteration.

---

## 4. Verifying a finished `.app`

Three independent checks, in order of trust:

```bash
# Most strict — what Gatekeeper actually does:
spctl --assess --type execute --verbose=4 dist/Eye-Rest.app
# Want to see: "accepted, source=Notarized Developer ID"

# Confirms the staple is attached and valid offline:
xcrun stapler validate dist/Eye-Rest.app

# Inspects the embedded signature for trust chain, team ID, runtime flags:
codesign -dvv dist/Eye-Rest.app
# Want to see:
#   Identifier=com.pmtlabs.eyerest.app
#   Authority=Developer ID Application: PMT Labs LLC (68M75D67LJ)
#   Authority=Developer ID Certification Authority
#   Authority=Apple Root CA
#   Notarization Ticket=stapled
#   flags=0x10000(runtime)
#   TeamIdentifier=68M75D67LJ
```

If `spctl` reports anything other than `source=Notarized Developer ID`, the
bundle is NOT production-ready, regardless of what the other tools say.

---

## 5. Troubleshooting

### 5.1 `codesign` fails: no Developer ID cert

Symptom: script aborts at step 3 with
`"no Developer ID Application certificate found for team 68M75D67LJ"`.

Fix: complete §2.1 above. Re-verify with
`security find-identity -v -p codesigning | grep 68M75D67LJ`.

### 5.2 `notarytool --wait` hangs silently for hours

**This is a real failure mode and it bit us during the initial setup.**
After submission, `notarytool --wait` polls Apple every few seconds. If
Apple's response takes longer than the underlying TCP keepalive timeout, the
client's socket can silently die while the client process stays alive,
waiting forever on a dead connection. CPU drops to 0.0% — the giveaway.

**Recovery procedure:**

1. **Check Apple's authoritative status** out-of-band:
   ```bash
   xcrun notarytool history \
     --key /Users/tamtran/key/AuthKey_XXX.p8 \
     --key-id XXX \
     --issuer XXX
   ```

   ⚠️ Use **`history`**, not `info`. The `info` command sometimes returns
   stale "In Progress" for completed submissions; `history` shows the real
   final status. (This caught us during initial setup — `info` kept saying
   "In Progress" while `history` showed the same submission as "Accepted".)

2. **If `history` shows the submission as `Accepted`** — the bundle is
   already notarized. Just kill the stuck `notarytool` process and run
   `stapler` manually:
   ```bash
   pkill -f "notarytool submit"
   xcrun stapler staple dist/Eye-Rest.app
   ```
   Stapling works because Apple's CDN already has a ticket for the bundle's
   `cdhash`. No submission ID required.

3. **If `history` shows `In Progress` even after hours** — Apple's backend
   is genuinely stuck on this submission. Resubmit (it costs nothing):
   ```bash
   pkill -f "notarytool submit"
   xcrun notarytool submit dist/Eye-Rest-notary.zip \
     --key /Users/tamtran/key/AuthKey_XXX.p8 \
     --key-id XXX --issuer XXX
   # Note the new submission ID, poll it with notarytool info or history.
   ```
   The original submission is now abandoned — harmless, it just sits in
   Apple's history forever.

### 5.3 `spctl --assess` returns "source=Unnotarized Developer ID"

Notarization didn't run, or didn't complete, or the ticket wasn't stapled.
Re-run with `NOTARIZE=1` and confirm the script reaches step 5 and prints
`The staple and validate action worked!`.

### 5.4 `spctl --assess` returns "rejected, source=No usable signature"

Code signing didn't take. Usually caused by signing the bundle from inside
out incorrectly. The script signs dylibs → executables → bundle seal in
that order. If you bypassed the script, replicate that ordering.

### 5.5 Submission expires / disappears from history

Apple keeps notary submissions for ~30 days. If you can't find a submission,
either resubmit the same zip or rebuild from source — both work.

### 5.6 Cert pickers fail in System Settings prompt

If you have multiple Developer ID certs in the keychain, `codesign` may
default to the wrong one. Set `SIGNING_IDENTITY` explicitly to override the
script's auto-detect:

```bash
SIGNING_IDENTITY="Developer ID Application: PMT Labs LLC (68M75D67LJ)" \
NOTARIZE=1 NOTARY_KEY_ID=... ...
./scripts/bundle-macos.sh
```

---

## 6. Distribution

After a successful production build:

```bash
# Zip for download — `ditto` preserves resource forks + extended attributes,
# which `zip` does not. Required if customers download via web browser
# (xattrs carry the quarantine bit that Gatekeeper reads).
ditto -c -k --keepParent dist/Eye-Rest.app dist/Eye-Rest-arm64.zip

# Or DMG (prettier for users, requires create-dmg or hdiutil)
hdiutil create -volname "Eye-Rest" -srcfolder dist/Eye-Rest.app \
    -ov -format UDZO dist/Eye-Rest-arm64.dmg
```

If you distribute via Velopack instead, see
[`001-velopack-build-publish.md`](001-velopack-build-publish.md) — that's a
separate path that produces auto-updating release artifacts rather than a
naked `.app` for direct download.

---

## 7. Reference: full env-var matrix for `bundle-macos.sh`

| Variable | Default | Purpose |
|----------|---------|---------|
| `TEAM_ID` | `68M75D67LJ` | Used by the auto-detect to pick the right cert |
| `BUNDLE_ID` | `com.pmtlabs.eyerest.app` | Written into `Info.plist` at bundle time |
| `RID` | `osx-arm64` | .NET runtime ID; use `osx-x64` for Intel |
| `CONFIGURATION` | `Release` | Set to `Debug` only for debugging the bundler itself |
| `SIGNING_IDENTITY` | (auto-detect) | Override if multiple certs exist |
| `SKIP_PUBLISH` | `0` | Set to `1` if a parent script already ran `dotnet publish` |
| `NOTARIZE` | `0` | Set to `1` to enable notarize+staple |
| `NOTARY_KEY_ID` | (required if `NOTARIZE=1`) | App Store Connect API key ID |
| `NOTARY_ISSUER_ID` | (required if `NOTARIZE=1`) | App Store Connect issuer UUID |
| `NOTARY_KEY_PATH` | (required if `NOTARIZE=1`) | Path to `.p8` private key file |
