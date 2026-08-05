#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────
# Upload Velopack releases to GitHub
# Usage: ./upload-velopack-release.sh [version]
# If version is omitted, reads from Directory.Build.props <Version>
# Requires: GITHUB_TOKEN env var with contents:write on rockyway/eye-rest
# ──────────────────────────────────────────────

# Allow vpk (net9 tool) to run on .NET 10 without needing .NET 9 installed
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
RELEASES_DIR="$PROJECT_ROOT/releases"
REPO_URL="https://github.com/rockyway/eye-rest"

echo "=== Uploading to GitHub Releases ==="
echo "  Version: $VERSION"
echo "  Repo:    $REPO_URL"

: "${GITHUB_TOKEN:?Set GITHUB_TOKEN env var (PAT with contents:write)}"

# vpk comes from THIS repo's local tool manifest (.config/dotnet-tools.json), not the global
# dotnet tool -- see the publish scripts for why. Running from PROJECT_ROOT resolves it.
dotnet tool restore --tool-manifest "$PROJECT_ROOT/.config/dotnet-tools.json"
cd "$PROJECT_ROOT"

dotnet vpk upload github \
    --repoUrl "$REPO_URL" \
    --tag "v$VERSION" \
    --releaseName "Blink Twice EyeRest $VERSION" \
    --publish \
    --token "$GITHUB_TOKEN" \
    --outputDir "$RELEASES_DIR"

echo ""
echo "Done! Release v$VERSION published at:"
echo "  $REPO_URL/releases/tag/v$VERSION"
