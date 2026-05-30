#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────────────────────────────────────
# Bundle Eye-Rest as a signed Mac App Store .pkg
#
# DIFFERS FROM bundle-macos.sh (Developer ID flow):
#   - Signs with "3rd Party Mac Developer Application" (NOT "Developer ID Application")
#   - Embeds the Mac App Store provisioning profile (embedded.provisionprofile)
#   - Uses MAS-specific entitlements (app-sandbox enabled)
#   - Skips notarization (Apple does it during App Review)
#   - Wraps the .app in a .pkg installer signed with the installer cert
#
# USAGE
#   ./scripts/bundle-macos-mas.sh
#
# OVERRIDES (all optional)
#   TEAM_ID              Default: 68M75D67LJ
#   BUNDLE_ID            Default: com.pmtlabs.eyerest.app
#   PROVISIONING_PROFILE Default: ~/key/EyeRest_MAS.provisionprofile
#   ENTITLEMENTS_FILE    Default: EyeRest.UI/EyeRest.MAS.entitlements
#   RID                  Default: osx-arm64 (use osx-x64 for Intel)
#   APP_VERSION          Default: read from EyeRest.UI.csproj
# ──────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
UI_PROJECT="$PROJECT_ROOT/EyeRest.UI"

APP_NAME="Blink Twice EyeRest"
TEAM_ID="${TEAM_ID:-68M75D67LJ}"
BUNDLE_ID="${BUNDLE_ID:-com.pmtlabs.eyerest.app}"
RID="${RID:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
APP_VERSION="${APP_VERSION:-1.4.1}"
PROVISIONING_PROFILE="${PROVISIONING_PROFILE:-$HOME/key/EyeRest_MAS.provisionprofile}"
ENTITLEMENTS_FILE="${ENTITLEMENTS_FILE:-$UI_PROJECT/EyeRest.MAS.entitlements}"

PUBLISH_DIR="$UI_PROJECT/bin/$CONFIGURATION/net8.0/$RID/publish"
OUTPUT_DIR="$PROJECT_ROOT/dist-mas"
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"
PKG_FILE="$OUTPUT_DIR/$APP_NAME.pkg"

# Resolve signing identities
APP_SIGNING_IDENTITY=$(security find-identity -v -p codesigning 2>/dev/null \
    | grep "3rd Party Mac Developer Application" \
    | grep "($TEAM_ID)" \
    | head -1 \
    | sed -E 's/^[[:space:]]*[0-9]+\)[[:space:]]+[A-F0-9]+[[:space:]]+"(.+)"$/\1/')

INSTALLER_SIGNING_IDENTITY=$(security find-identity -v 2>/dev/null \
    | grep "3rd Party Mac Developer Installer" \
    | grep "($TEAM_ID)" \
    | head -1 \
    | sed -E 's/^[[:space:]]*[0-9]+\)[[:space:]]+[A-F0-9]+[[:space:]]+"(.+)"$/\1/')

if [ -z "$APP_SIGNING_IDENTITY" ]; then
    echo "ERROR: No '3rd Party Mac Developer Application' certificate found for team $TEAM_ID" >&2
    exit 1
fi
if [ -z "$INSTALLER_SIGNING_IDENTITY" ]; then
    echo "ERROR: No '3rd Party Mac Developer Installer' certificate found for team $TEAM_ID" >&2
    exit 1
fi
if [ ! -f "$PROVISIONING_PROFILE" ]; then
    echo "ERROR: Provisioning profile not found at $PROVISIONING_PROFILE" >&2
    exit 1
fi
if [ ! -f "$ENTITLEMENTS_FILE" ]; then
    echo "ERROR: Entitlements file not found at $ENTITLEMENTS_FILE" >&2
    exit 1
fi

echo "=== Blink Twice EyeRest Mac App Store Bundler ==="
echo "  Team ID:              $TEAM_ID"
echo "  Bundle ID:            $BUNDLE_ID"
echo "  RID:                  $RID"
echo "  App signing:          $APP_SIGNING_IDENTITY"
echo "  Installer signing:    $INSTALLER_SIGNING_IDENTITY"
echo "  Provisioning profile: $PROVISIONING_PROFILE"
echo "  Entitlements:         $ENTITLEMENTS_FILE"
echo ""

# ── Step 1: Publish self-contained ──────────────────────────────────────────
echo "[1/6] Publishing $CONFIGURATION build for $RID (v$APP_VERSION)..."
dotnet publish "$UI_PROJECT/EyeRest.UI.csproj" \
    -c "$CONFIGURATION" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:UseAppHost=true \
    -p:Version="$APP_VERSION"

# ── Step 2: Create .app bundle structure ────────────────────────────────────
echo "[2/6] Creating $APP_NAME.app bundle..."
mkdir -p "$OUTPUT_DIR"
rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

cp -a "$PUBLISH_DIR/." "$APP_BUNDLE/Contents/MacOS/"

cp "$UI_PROJECT/Info.plist" "$APP_BUNDLE/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier $BUNDLE_ID" \
    "$APP_BUNDLE/Contents/Info.plist"

cp "$UI_PROJECT/Assets/AppIcon.icns" "$APP_BUNDLE/Contents/Resources/"

chmod +x "$APP_BUNDLE/Contents/MacOS/BlinkTwiceEyeRest"

# Embed the provisioning profile (MAS requirement)
cp "$PROVISIONING_PROFILE" "$APP_BUNDLE/Contents/embedded.provisionprofile"

# Bump CFBundleVersion and CFBundleShortVersionString to match APP_VERSION
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $APP_VERSION" \
    "$APP_BUNDLE/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $APP_VERSION" \
    "$APP_BUNDLE/Contents/Info.plist"

# CRITICAL: Strip com.apple.quarantine xattr from ALL files
# Apple rejects builds with quarantine attributes (error 91109)
echo "  Stripping extended attributes..."
xattr -cr "$APP_BUNDLE"

# ── Step 3: Sign all binaries (inside-out) ──────────────────────────────────
echo "[3/6] Signing bundle with: $APP_SIGNING_IDENTITY"

echo "  Signing native libraries..."
find "$APP_BUNDLE" -name "*.dylib" -print0 | while IFS= read -r -d '' f; do
    codesign --force --timestamp --options runtime \
        --sign "$APP_SIGNING_IDENTITY" \
        --entitlements "$ENTITLEMENTS_FILE" \
        "$f" 2>&1 | grep -v "replacing existing signature" || true
done

echo "  Signing native executables..."
find "$APP_BUNDLE/Contents/MacOS" -type f -print0 | while IFS= read -r -d '' f; do
    if file "$f" | grep -q "Mach-O" && [[ "$f" != *.dylib ]]; then
        codesign --force --timestamp --options runtime \
            --sign "$APP_SIGNING_IDENTITY" \
            --entitlements "$ENTITLEMENTS_FILE" \
            "$f" 2>&1 | grep -v "replacing existing signature" || true
    fi
done

echo "  Signing app bundle (deep)..."
codesign --force --deep --timestamp --options runtime \
    --sign "$APP_SIGNING_IDENTITY" \
    --entitlements "$ENTITLEMENTS_FILE" \
    "$APP_BUNDLE"

# ── Step 4: Verify signature ────────────────────────────────────────────────
echo "[4/6] Verifying signature..."
codesign --verify --verbose=2 "$APP_BUNDLE"

EMBEDDED_TEAM=$(codesign -dvv "$APP_BUNDLE" 2>&1 | awk -F= '/TeamIdentifier/{print $2}')
echo "  Embedded TeamIdentifier: $EMBEDDED_TEAM (expected $TEAM_ID)"

# ── Step 5: Build the .pkg installer ────────────────────────────────────────
echo "[5/6] Building signed .pkg installer..."
rm -f "$PKG_FILE"
productbuild \
    --component "$APP_BUNDLE" /Applications \
    --sign "$INSTALLER_SIGNING_IDENTITY" \
    "$PKG_FILE"

# ── Step 6: Summary ─────────────────────────────────────────────────────────
PKG_SIZE=$(du -sh "$PKG_FILE" | cut -f1)
echo ""
echo "[6/6] Done!"
echo "  .app bundle: $APP_BUNDLE"
echo "  .pkg file:   $PKG_FILE ($PKG_SIZE)"
echo ""
echo "Upload to App Store Connect:"
echo "  xcrun altool --upload-app -f \"$PKG_FILE\" -t osx \\"
echo "    --apiKey YOUR_KEY_ID --apiIssuer YOUR_ISSUER_ID"
echo ""
echo "  Or use Transporter.app (drag the .pkg in)."
