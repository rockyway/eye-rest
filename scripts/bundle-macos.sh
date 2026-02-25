#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────
# Bundle Eye-Rest as a signed macOS .app
# ──────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
UI_PROJECT="$PROJECT_ROOT/EyeRest.UI"

APP_NAME="Eye-Rest"
BUNDLE_ID="com.eyerest.app"
RID="${RID:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
SIGNING_IDENTITY="${SIGNING_IDENTITY:-Apple Development: you@example.com (YOUR_TEAM_ID)}"

PUBLISH_DIR="$UI_PROJECT/bin/$CONFIGURATION/net8.0/$RID/publish"
OUTPUT_DIR="$PROJECT_ROOT/dist"
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"

echo "=== Eye-Rest macOS Bundler ==="
echo "  RID:            $RID"
echo "  Configuration:  $CONFIGURATION"
echo "  Signing:        $SIGNING_IDENTITY"
echo ""

# ── Step 1: Publish self-contained ───────────
echo "[1/5] Publishing $CONFIGURATION build for $RID..."
dotnet publish "$UI_PROJECT/EyeRest.UI.csproj" \
    -c "$CONFIGURATION" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:UseAppHost=true

# ── Step 2: Create .app bundle structure ─────
echo "[2/5] Creating $APP_NAME.app bundle..."
rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy published output to MacOS directory
cp -a "$PUBLISH_DIR/." "$APP_BUNDLE/Contents/MacOS/"

# Copy Info.plist
cp "$UI_PROJECT/Info.plist" "$APP_BUNDLE/Contents/"

# Copy icon
cp "$UI_PROJECT/Assets/AppIcon.icns" "$APP_BUNDLE/Contents/Resources/"

# Set executable permission
chmod +x "$APP_BUNDLE/Contents/MacOS/EyeRest"

# ── Step 3: Sign all binaries ────────────────
echo "[3/5] Signing bundle with: $SIGNING_IDENTITY"

ENTITLEMENTS="$UI_PROJECT/EyeRest.entitlements"

# Sign all dylibs individually first (innermost → outermost)
echo "  Signing native libraries..."
find "$APP_BUNDLE" -name "*.dylib" -print0 | while IFS= read -r -d '' f; do
    codesign --force --timestamp --options runtime \
        --sign "$SIGNING_IDENTITY" \
        --entitlements "$ENTITLEMENTS" \
        "$f" 2>&1 | grep -v "replacing existing signature" || true
done

# Sign the native executables (createdump, etc.)
echo "  Signing native executables..."
find "$APP_BUNDLE/Contents/MacOS" -type f -print0 | while IFS= read -r -d '' f; do
    if file "$f" | grep -q "Mach-O" && [[ "$f" != *.dylib ]]; then
        codesign --force --timestamp --options runtime \
            --sign "$SIGNING_IDENTITY" \
            --entitlements "$ENTITLEMENTS" \
            "$f" 2>&1 | grep -v "replacing existing signature" || true
    fi
done

# Deep-sign the entire bundle (covers managed DLLs + bundle seal)
echo "  Signing app bundle (deep)..."
codesign --force --deep --timestamp --options runtime \
    --sign "$SIGNING_IDENTITY" \
    --entitlements "$ENTITLEMENTS" \
    "$APP_BUNDLE"

# ── Step 4: Verify signature ─────────────────
echo "[4/5] Verifying signature..."
codesign --verify --verbose=2 "$APP_BUNDLE"

# ── Step 5: Summary ──────────────────────────
BUNDLE_SIZE=$(du -sh "$APP_BUNDLE" | cut -f1)
echo ""
echo "[5/5] Done!"
echo "  Bundle:  $APP_BUNDLE"
echo "  Size:    $BUNDLE_SIZE"
echo ""
echo "To run:    open \"$APP_BUNDLE\""
echo "To check:  spctl --assess --verbose \"$APP_BUNDLE\""
