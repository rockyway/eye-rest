#!/bin/bash
set -e

# ─────────────────────────────────────────────────────────────────────────────
# publish-velopack-win.sh — Build and package Eye-Rest for Windows using Velopack
#
# Usage (Git Bash, WSL, or bash on Windows):
#   ./scripts/publish-velopack-win.sh [VERSION]
#
# Arguments:
#   VERSION   Semantic version string (default: read from Directory.Build.props)
#
# Optional environment variables for signing (pick ONE):
#   AZURE_SIGN_FILE   Path to Azure Trusted Signing metadata.json (recommended)
#                     Requires AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID
#   SIGN_PARAMS       Authenticode signing parameters for vpk --signParams
#
# Note: This script MUST be run on a Windows machine (or Windows CI runner).
# ─────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SOLUTION_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
VERSION="${1:-1.0.0}"
PUBLISH_DIR="$SOLUTION_ROOT/publish/velopack-win"
PACK_ID="EyeRest"
MAIN_EXE="EyeRest.exe"
ICON="$SOLUTION_ROOT/Resources/app.ico"
RELEASES_DIR="$SOLUTION_ROOT/releases"
SIGNING_DIR="$SOLUTION_ROOT/signing"

# ─── Auto-load signing credentials ──────────────────────────────────────────
if [[ -z "${AZURE_SIGN_FILE}" && -z "${SIGN_PARAMS}" ]]; then
  if [[ -f "$SIGNING_DIR/.env" && -f "$SIGNING_DIR/metadata.json" ]]; then
    echo "=== Loading signing credentials from signing/.env ==="
    set -a
    source "$SIGNING_DIR/.env"
    set +a
    if [[ -n "${AZURE_CLIENT_ID}" && -n "${AZURE_CLIENT_SECRET}" && -n "${AZURE_TENANT_ID}" ]]; then
      export AZURE_SIGN_FILE="$SIGNING_DIR/metadata.json"
      echo "    Azure credentials loaded"
    else
      echo "    Warning: signing/.env found but credentials are incomplete — building unsigned"
    fi
  fi
fi

echo "=== Eye-Rest Windows Publish ==="
echo "    Version : $VERSION"
echo "    Output  : $RELEASES_DIR/"
echo ""

# ─── Stage 1: dotnet publish ──────────────────────────────────────────────────
echo "=== Stage 1: dotnet publish (Release / win-x64) ==="
dotnet publish "$SOLUTION_ROOT/EyeRest.UI/EyeRest.UI.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  -o "$PUBLISH_DIR"

# ─── Stage 2: vpk pack ───────────────────────────────────────────────────────
echo ""
echo "=== Stage 2: vpk pack ==="
mkdir -p "$RELEASES_DIR"

VPK_ARGS=(
  pack
  -u        "$PACK_ID"
  -v        "$VERSION"
  -p        "$PUBLISH_DIR"
  -e        "$MAIN_EXE"
  --icon    "$ICON"
  -o        "$RELEASES_DIR"
)

if [[ -n "${AZURE_SIGN_FILE}" ]]; then
  echo "    Signing mode : Azure Trusted Signing (immediate SmartScreen trust)"
  echo "    Sign scope   : All unsigned binaries (runtime DLLs already vendor-signed)"
  VPK_ARGS+=(--azureTrustedSignFile "$AZURE_SIGN_FILE")
elif [[ -n "${SIGN_PARAMS}" ]]; then
  echo "    Signing mode : Authenticode (--signParams provided)"
  echo "    Sign scope   : All unsigned binaries (runtime DLLs already vendor-signed)"
  VPK_ARGS+=(--signParams "$SIGN_PARAMS")
else
  echo "    Signing mode : Unsigned"
  echo "    Tip: Place Azure credentials in signing/.env + signing/metadata.json for auto-signing"
  echo "         or set AZURE_SIGN_FILE or SIGN_PARAMS env vars"
fi

vpk "${VPK_ARGS[@]}"

# ─── Summary ─────────────────────────────────────────────────────────────────
echo ""
echo "=== Done! ==="
echo ""
echo "Release artifacts written to: $RELEASES_DIR/"
ls -lh "$RELEASES_DIR/" 2>/dev/null || dir "$RELEASES_DIR" 2>/dev/null || true
echo ""
echo "Next step — upload to GitHub Releases:"
echo "  GITHUB_TOKEN=<pat> ./scripts/upload-velopack-release.sh $VERSION"
