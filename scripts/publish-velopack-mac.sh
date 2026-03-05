#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────
# Publish Eye-Rest for macOS via Velopack
# Usage: ./publish-velopack-mac.sh <version>
# Example: ./publish-velopack-mac.sh 1.0.2
# ──────────────────────────────────────────────

VERSION="${1:?Usage: $0 <version>}"
# Allow vpk (net9 tool) to run on .NET 10 without needing .NET 9 installed
export DOTNET_ROLL_FORWARD=LatestMajor
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
UI_PROJECT="$PROJECT_ROOT/EyeRest.UI"
RID="${RID:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
PUBLISH_DIR="$PROJECT_ROOT/publish/velopack-mac"
RELEASES_DIR="$PROJECT_ROOT/releases"
# Optional signing (set env vars to enable)
SIGN_APP="${SIGNING_IDENTITY:-}"
NOTARY_PROFILE="${NOTARY_PROFILE:-}"

echo "=== Eye-Rest Velopack Publish (macOS) ==="
echo "  Version:  $VERSION"
echo "  RID:      $RID"
echo ""

# Step 1: Publish self-contained
echo "[1/3] Publishing..."
dotnet publish "$UI_PROJECT/EyeRest.UI.csproj" \
    -c "$CONFIGURATION" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -o "$PUBLISH_DIR"

# Step 2: Patch Info.plist version if it exists
if [ -f "$UI_PROJECT/Info.plist" ]; then
    cp "$UI_PROJECT/Info.plist" "$PUBLISH_DIR/Info.plist"
    /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$PUBLISH_DIR/Info.plist"
    /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$PUBLISH_DIR/Info.plist"
fi

# Step 3: vpk pack
echo "[2/3] Packing with vpk..."
mkdir -p "$RELEASES_DIR"

VPK_ARGS=(
    pack
    -u EyeRest
    -v "$VERSION"
    -p "$PUBLISH_DIR"
    -e EyeRest
    -o "$RELEASES_DIR"
)

# Add icon if available
if [ -f "$UI_PROJECT/Assets/AppIcon.icns" ]; then
    VPK_ARGS+=(--icon "$UI_PROJECT/Assets/AppIcon.icns")
fi

# Add signing if identity is set
if [ -n "$SIGN_APP" ]; then
    VPK_ARGS+=(--signAppIdentity "$SIGN_APP")
fi
if [ -n "$NOTARY_PROFILE" ]; then
    VPK_ARGS+=(--notaryProfile "$NOTARY_PROFILE")
fi

vpk "${VPK_ARGS[@]}"

# Summary
echo ""
echo "[3/3] Done!"
echo "  Releases in: $RELEASES_DIR"
ls -lh "$RELEASES_DIR"
