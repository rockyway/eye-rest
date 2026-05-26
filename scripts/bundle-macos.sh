#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────────────────────
# Bundle Eye-Rest as a signed (and optionally notarized) macOS .app
#
# DEFAULTS  ── PMT Labs LLC production identity
#   TEAM_ID                 68M75D67LJ
#   BUNDLE_ID               com.pmtlabs.eyerest.app
#   Expected signing cert   "Developer ID Application: PMT Labs LLC (68M75D67LJ)"
#
# USAGE
#   Sign only (local validation):
#       ./scripts/bundle-macos.sh
#
#   Sign + notarize + staple (production release):
#       NOTARIZE=1 \
#       NOTARY_KEY_ID=ABC123DEF4 \
#       NOTARY_ISSUER_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx \
#       NOTARY_KEY_PATH=~/keys/AuthKey_ABC123DEF4.p8 \
#       ./scripts/bundle-macos.sh
#
# OVERRIDES (all optional)
#   SIGNING_IDENTITY   Full cert name; auto-detected from TEAM_ID if unset.
#   BUNDLE_ID          Override the bundle identifier.
#   TEAM_ID            Override the team ID used for auto-detection.
#   RID                osx-arm64 (default) or osx-x64.
#   CONFIGURATION      Release (default) or Debug.
#   SKIP_PUBLISH       1 = skip `dotnet publish` (caller already built).
#   NOTARIZE           1 = run notarytool + stapler after signing.
#
# PREREQUISITES
#   1. Apple Developer Program membership (PMT Labs LLC).
#   2. "Developer ID Application" certificate installed in login keychain.
#      Create at: https://developer.apple.com/account/resources/certificates/list
#      → "+" → "Developer ID Application" → upload CSR from Keychain Access.
#   3. (Notarization only) App Store Connect API key (.p8) downloaded once
#      from https://appstoreconnect.apple.com/access/integrations/api
#      The .p8 download is one-time — keep a secure backup.
# ──────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
UI_PROJECT="$PROJECT_ROOT/EyeRest.UI"

APP_NAME="Blink Twice EyeRest"
TEAM_ID="${TEAM_ID:-68M75D67LJ}"
BUNDLE_ID="${BUNDLE_ID:-com.pmtlabs.eyerest.app}"
RID="${RID:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
NOTARIZE="${NOTARIZE:-0}"

PUBLISH_DIR="$UI_PROJECT/bin/$CONFIGURATION/net8.0/$RID/publish"
OUTPUT_DIR="$PROJECT_ROOT/dist"
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"

# ── Resolve signing identity ────────────────────────────────────────────────
# Priority: explicit SIGNING_IDENTITY env var → auto-detect by TEAM_ID.
if [ -z "${SIGNING_IDENTITY:-}" ]; then
    # Look for a Developer ID Application cert matching the team.
    MATCH=$(security find-identity -v -p codesigning 2>/dev/null \
        | grep "Developer ID Application" \
        | grep "($TEAM_ID)" \
        | head -1 \
        | sed -E 's/^[[:space:]]*[0-9]+\)[[:space:]]+[A-F0-9]+[[:space:]]+"(.+)"$/\1/')
    if [ -n "$MATCH" ]; then
        SIGNING_IDENTITY="$MATCH"
    else
        cat <<EOF >&2

ERROR: No "Developer ID Application" certificate found for team $TEAM_ID.

Installed code-signing identities:
$(security find-identity -v -p codesigning 2>/dev/null | sed 's/^/    /')

To fix:
  1. Visit https://developer.apple.com/account/resources/certificates/list
  2. Click "+" and choose "Developer ID Application".
  3. In Keychain Access on this Mac, generate a CSR:
       Keychain Access → Certificate Assistant → Request a Certificate from
       a Certificate Authority. Save to disk.
  4. Upload the CSR on the portal, download the .cer, double-click to install.
  5. Re-run this script.

Or override explicitly:
  SIGNING_IDENTITY="Your full cert name here" ./scripts/bundle-macos.sh
EOF
        exit 1
    fi
fi

echo "=== Blink Twice EyeRest macOS Bundler ==="
echo "  Team ID:        $TEAM_ID"
echo "  Bundle ID:      $BUNDLE_ID"
echo "  RID:            $RID"
echo "  Configuration:  $CONFIGURATION"
echo "  Signing:        $SIGNING_IDENTITY"
echo "  Notarize:       $([ "$NOTARIZE" = "1" ] && echo yes || echo no)"
echo ""

# ── Step 1: Publish self-contained ──────────────────────────────────────────
if [ "${SKIP_PUBLISH:-0}" != "1" ]; then
    echo "[1/6] Publishing $CONFIGURATION build for $RID..."
    dotnet publish "$UI_PROJECT/EyeRest.UI.csproj" \
        -c "$CONFIGURATION" \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=false \
        -p:PublishTrimmed=false \
        -p:UseAppHost=true
else
    echo "[1/6] Skipping publish (already built by caller)"
fi

# ── Step 2: Create .app bundle structure ────────────────────────────────────
echo "[2/6] Creating $APP_NAME.app bundle..."
rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy published output to MacOS directory
cp -a "$PUBLISH_DIR/." "$APP_BUNDLE/Contents/MacOS/"

# Copy Info.plist, then defensively rewrite the bundle ID so the BUNDLE_ID
# env var is the single source of truth (Info.plist in source is just the seed).
cp "$UI_PROJECT/Info.plist" "$APP_BUNDLE/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $BUNDLE_ID" \
    "$APP_BUNDLE/Contents/Info.plist"

# Copy icon
cp "$UI_PROJECT/Assets/AppIcon.icns" "$APP_BUNDLE/Contents/Resources/"

# Set executable permission
chmod +x "$APP_BUNDLE/Contents/MacOS/BlinkTwiceEyeRest"

# ── Step 3: Sign all binaries (inside-out) ──────────────────────────────────
echo "[3/6] Signing bundle with: $SIGNING_IDENTITY"
ENTITLEMENTS="$UI_PROJECT/EyeRest.entitlements"

# Sign all dylibs first (innermost → outermost)
echo "  Signing native libraries..."
find "$APP_BUNDLE" -name "*.dylib" -print0 | while IFS= read -r -d '' f; do
    codesign --force --timestamp --options runtime \
        --sign "$SIGNING_IDENTITY" \
        --entitlements "$ENTITLEMENTS" \
        "$f" 2>&1 | grep -v "replacing existing signature" || true
done

# Sign other native executables (createdump, etc.)
echo "  Signing native executables..."
find "$APP_BUNDLE/Contents/MacOS" -type f -print0 | while IFS= read -r -d '' f; do
    if file "$f" | grep -q "Mach-O" && [[ "$f" != *.dylib ]]; then
        codesign --force --timestamp --options runtime \
            --sign "$SIGNING_IDENTITY" \
            --entitlements "$ENTITLEMENTS" \
            "$f" 2>&1 | grep -v "replacing existing signature" || true
    fi
done

# Deep-sign the entire bundle (covers managed DLLs + final bundle seal)
echo "  Signing app bundle (deep)..."
codesign --force --deep --timestamp --options runtime \
    --sign "$SIGNING_IDENTITY" \
    --entitlements "$ENTITLEMENTS" \
    "$APP_BUNDLE"

# ── Step 4: Verify signature ────────────────────────────────────────────────
echo "[4/6] Verifying signature..."
codesign --verify --verbose=2 "$APP_BUNDLE"

# Confirm the embedded TeamIdentifier matches what we expect.
EMBEDDED_TEAM=$(codesign -dvv "$APP_BUNDLE" 2>&1 | awk -F= '/TeamIdentifier/{print $2}')
if [ "$EMBEDDED_TEAM" != "$TEAM_ID" ]; then
    echo "WARNING: Embedded TeamIdentifier ($EMBEDDED_TEAM) != expected ($TEAM_ID)" >&2
fi

# ── Step 5: Notarize + staple (optional) ────────────────────────────────────
if [ "$NOTARIZE" = "1" ]; then
    : "${NOTARY_KEY_ID:?Error: NOTARY_KEY_ID required when NOTARIZE=1}"
    : "${NOTARY_ISSUER_ID:?Error: NOTARY_ISSUER_ID required when NOTARIZE=1}"
    : "${NOTARY_KEY_PATH:?Error: NOTARY_KEY_PATH required when NOTARIZE=1 (path to .p8 file)}"

    if [ ! -f "$NOTARY_KEY_PATH" ]; then
        echo "ERROR: NOTARY_KEY_PATH does not exist: $NOTARY_KEY_PATH" >&2
        exit 1
    fi

    echo "[5/6] Notarizing with Apple..."
    NOTARY_ZIP="$OUTPUT_DIR/${APP_NAME}-notary.zip"
    rm -f "$NOTARY_ZIP"

    # ditto preserves resource forks and extended attributes; required for notarization.
    ditto -c -k --keepParent "$APP_BUNDLE" "$NOTARY_ZIP"

    echo "  Submitting $(du -h "$NOTARY_ZIP" | cut -f1) zip to notary service..."
    xcrun notarytool submit "$NOTARY_ZIP" \
        --key "$NOTARY_KEY_PATH" \
        --key-id "$NOTARY_KEY_ID" \
        --issuer "$NOTARY_ISSUER_ID" \
        --wait

    rm -f "$NOTARY_ZIP"

    echo "  Stapling notarization ticket..."
    xcrun stapler staple "$APP_BUNDLE"

    echo "  Validating staple + Gatekeeper assessment..."
    xcrun stapler validate "$APP_BUNDLE"
    spctl --assess --type execute --verbose=4 "$APP_BUNDLE"
else
    echo "[5/6] Skipping notarization (set NOTARIZE=1 to enable)"
fi

# ── Step 6: Summary ─────────────────────────────────────────────────────────
BUNDLE_SIZE=$(du -sh "$APP_BUNDLE" | cut -f1)
echo ""
echo "[6/6] Done!"
echo "  Bundle:  $APP_BUNDLE"
echo "  Size:    $BUNDLE_SIZE"
echo ""
if [ "$NOTARIZE" = "1" ]; then
    echo "  ✓ Signed, notarized, and stapled — ready for distribution."
else
    echo "  ⚠ Signed locally only. For external distribution, re-run with:"
    echo "      NOTARIZE=1 NOTARY_KEY_ID=… NOTARY_ISSUER_ID=… NOTARY_KEY_PATH=… $0"
fi
echo ""
echo "Quick checks:"
echo "  open      \"$APP_BUNDLE\""
echo "  spctl     spctl --assess --verbose \"$APP_BUNDLE\""
echo "  codesign  codesign -dvv \"$APP_BUNDLE\""
