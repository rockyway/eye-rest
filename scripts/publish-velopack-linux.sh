#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────
# Publish Eye-Rest for Linux via Velopack (AppImage)
# Usage: ./publish-velopack-linux.sh [version]
# If version is omitted, reads from Directory.Build.props <Version>
# Example: ./publish-velopack-linux.sh          # auto from Directory.Build.props
# Example: ./publish-velopack-linux.sh 1.5.0    # explicit override
#
# Produces in releases/ (linux channel):
#   BlinkTwiceEyeRest-<ver>-linux-x64-full.nupkg
#   BlinkTwiceEyeRest-<ver>-linux-x64-delta.nupkg   (when a prior full nupkg exists)
#   BlinkTwiceEyeRest-linux-x64.AppImage            (the distributable)
#   assets.linux.json / releases.linux.json / RELEASES-linux
#
# Run this ON Linux (native). vpk auto-selects the `linux` channel and builds
# the AppImage. Cross-compiling from Windows is possible via `vpk [linux] pack`
# — see docs/plan/011-v1.5.0-all-os-release.md — but a native Linux build is the
# supported, smoke-testable path.
#
# Signing note: Linux has no OS-level notarization/gatekeeper. Trust comes from
# the distribution channel, not a per-binary cert — so there is nothing to sign
# here. Integrity is provided by the Velopack feed (SHA256 in releases.linux.json)
# and, if desired later, an optional GPG signature on the AppImage.
# ──────────────────────────────────────────────

# Allow vpk (net9 tool) to run on newer .NET without needing .NET 9 installed
export DOTNET_ROLL_FORWARD=LatestMajor
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Resolve version: explicit arg → Directory.Build.props → error
if [ -n "${1:-}" ]; then
    VERSION="$1"
else
    PROPS_FILE="$PROJECT_ROOT/Directory.Build.props"
    VERSION=$(grep -o '<Version>[^<]*</Version>' "$PROPS_FILE" | sed 's/<[^>]*>//g')
    if [ -z "$VERSION" ]; then
        echo "ERROR: Could not read <Version> from $PROPS_FILE. Pass version explicitly." >&2
        exit 1
    fi
fi
UI_PROJECT="$PROJECT_ROOT/EyeRest.UI"
RID="${RID:-linux-x64}"
CONFIGURATION="${CONFIGURATION:-Release}"
PUBLISH_DIR="$PROJECT_ROOT/publish/velopack-linux"
RELEASES_DIR="$PROJECT_ROOT/releases"

echo "=== Blink Twice EyeRest Velopack Publish (Linux) ==="
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
    -p:Version="$VERSION" \
    -p:AssemblyVersion="${VERSION}.0" \
    -p:FileVersion="${VERSION}.0" \
    -p:InformationalVersion="$VERSION" \
    -o "$PUBLISH_DIR"

# Step 2: vpk pack (AppImage)
echo "[2/3] Packing with vpk..."
mkdir -p "$RELEASES_DIR"

VPK_ARGS=(
    pack
    -u EyeRest
    -v "$VERSION"
    -p "$PUBLISH_DIR"
    -e BlinkTwiceEyeRest
    -o "$RELEASES_DIR"
    --categories Utility
)

# Add icon if available (PNG required for the AppImage / .desktop entry)
if [ -f "$UI_PROJECT/Assets/app-icon.png" ]; then
    VPK_ARGS+=(--icon "$UI_PROJECT/Assets/app-icon.png")
fi

# vpk comes from THIS repo's local tool manifest (.config/dotnet-tools.json), not the global
# dotnet tool. eye-rest must use the vpk matching its Velopack NuGet or auto-update silently
# breaks, while other projects on the same machine need other versions -- and there is only one
# global slot. Running from PROJECT_ROOT is what lets the manifest resolve.
dotnet tool restore --tool-manifest "$PROJECT_ROOT/.config/dotnet-tools.json"
(cd "$PROJECT_ROOT" && dotnet vpk "${VPK_ARGS[@]}")

# Summary
echo ""
echo "[3/3] Done!"
echo "  Releases in: $RELEASES_DIR"
ls -lh "$RELEASES_DIR"
