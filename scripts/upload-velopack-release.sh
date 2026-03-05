#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────
# Upload Velopack releases to GitHub
# Usage: ./upload-velopack-release.sh <version>
# Requires: GITHUB_TOKEN env var with contents:write on rockyway/eye-rest
# ──────────────────────────────────────────────

VERSION="${1:?Usage: $0 <version>}"
# Allow vpk (net9 tool) to run on .NET 10 without needing .NET 9 installed
export DOTNET_ROLL_FORWARD=LatestMajor
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RELEASES_DIR="$PROJECT_ROOT/releases"
REPO_URL="https://github.com/rockyway/eye-rest"

echo "=== Uploading to GitHub Releases ==="
echo "  Version: $VERSION"
echo "  Repo:    $REPO_URL"

: "${GITHUB_TOKEN:?Set GITHUB_TOKEN env var (PAT with contents:write)}"

vpk upload github \
    --repoUrl "$REPO_URL" \
    --tag "v$VERSION" \
    --releaseName "Eye-Rest $VERSION" \
    --publish \
    --token "$GITHUB_TOKEN" \
    --outputDir "$RELEASES_DIR"

echo ""
echo "Done! Release v$VERSION published at:"
echo "  $REPO_URL/releases/tag/v$VERSION"
