#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────
# Publish Eye-Rest for macOS via Velopack
# Usage: ./publish-velopack-mac.sh [version]
# If version is omitted, reads from Directory.Build.props <Version>
# Example: ./publish-velopack-mac.sh          # auto from Directory.Build.props
# Example: ./publish-velopack-mac.sh 1.0.3    # explicit override
# ──────────────────────────────────────────────

# Applies to `dotnet publish` below, which builds net8.0 with whatever SDK is installed.
export DOTNET_ROLL_FORWARD=LatestMajor

# vpk is a net9 tool and gets its own runtime, resolved just before it runs (see VPK_DOTNET_ENV
# at the pack step). Do NOT export DOTNET_ROOT here: it would also redirect `dotnet publish`
# onto a different SDK and change how the shipped binary is built.
#
# Why this matters: on this build host (macOS 26.5.2, runtimes 8 + 10, NO 9) vpk running under
# LatestMajor was SIGKILLed 6 times at varying stages -- post-process, notarize, codesign -- with
# no crash report, no jetsam entry, gigabytes free and a fresh reboot. notarytool then aborted on
# EPIPE writing into the dead process's pipe, which made it look like a notarization fault.
# rephlo-desktop builds reliably on this same Mac because it runs vpk on the Homebrew .NET 9.
# See docs/troubleshooting/010.
VPK_DOTNET_ENV=()
if [ -d "/opt/homebrew/Cellar/dotnet@9" ]; then
    DOTNET9_DIR="$(ls -d /opt/homebrew/Cellar/dotnet@9/*/libexec 2>/dev/null | head -1)"
    if [ -n "$DOTNET9_DIR" ]; then
        # A real net9 runtime: use it and drop the roll-forward for vpk only.
        VPK_DOTNET_ENV=(env "DOTNET_ROOT=$DOTNET9_DIR" "PATH=$DOTNET9_DIR:$PATH" "DOTNET_ROLL_FORWARD=Disable")
    fi
fi
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
RID="${RID:-osx-arm64}"
CONFIGURATION="${CONFIGURATION:-Release}"
PUBLISH_DIR="$PROJECT_ROOT/publish/velopack-mac"
RELEASES_DIR="$PROJECT_ROOT/releases"
# Optional signing (set env vars to enable)
SIGN_APP="${SIGNING_IDENTITY:-}"
NOTARY_PROFILE="${NOTARY_PROFILE:-}"

echo "=== Blink Twice EyeRest Velopack Publish (macOS) ==="
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
    -e BlinkTwiceEyeRest
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

# vpk comes from THIS repo's local tool manifest (.config/dotnet-tools.json), not the global
# dotnet tool. eye-rest must use the vpk matching its Velopack NuGet or auto-update silently
# breaks, while other projects on the same machine need other versions -- and there is only one
# global slot. Running from PROJECT_ROOT is what lets the manifest resolve.
dotnet tool restore --tool-manifest "$PROJECT_ROOT/.config/dotnet-tools.json"
if [ ${#VPK_DOTNET_ENV[@]} -gt 0 ]; then
    echo "    vpk runtime  : ${DOTNET9_DIR} (real .NET 9)"
else
    echo "    vpk runtime  : roll-forward (no .NET 9 found) -- see docs/troubleshooting/010"
fi
(cd "$PROJECT_ROOT" && "${VPK_DOTNET_ENV[@]}" dotnet vpk "${VPK_ARGS[@]}")

# Summary
echo ""
echo "[3/3] Done!"
echo "  Releases in: $RELEASES_DIR"
ls -lh "$RELEASES_DIR"
