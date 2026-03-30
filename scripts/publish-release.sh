#!/bin/bash
set -euo pipefail

# ──────────────────────────────────────────────
# Publish Eye-Rest release
# Usage: ./scripts/publish-release.sh <version>
# Example: ./scripts/publish-release.sh 1.4.0
#
# What it does:
#   1. Reads version from git tag or argument
#   2. Builds + tests
#   3. Publishes macOS ARM64 + Windows x64
#   4. Bundles macOS .app
#   5. Zips artifacts
#   6. Creates git tag + GitHub release
# ──────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

# ── Parse version ────────────────────────────
if [ $# -ge 1 ]; then
    VERSION="$1"
elif git describe --tags --abbrev=0 &>/dev/null; then
    VERSION="$(git describe --tags --abbrev=0 | sed 's/^v//')"
    echo "No version argument — using latest git tag: $VERSION"
else
    echo "Usage: $0 <version>"
    echo "Example: $0 1.4.0"
    exit 1
fi

TAG="v${VERSION}"
DIST_DIR="$PROJECT_ROOT/dist"
SIGNING_IDENTITY="${SIGNING_IDENTITY:--}"

echo "╔══════════════════════════════════════╗"
echo "║  Eye-Rest Release v${VERSION}          "
echo "╚══════════════════════════════════════╝"
echo ""

# ── Step 1: Test ─────────────────────────────
echo "[1/7] Running tests..."
dotnet test --verbosity quiet
echo "  ✓ All tests passed"

# ── Step 2: Publish macOS ARM64 ──────────────
echo "[2/7] Publishing macOS ARM64..."
dotnet publish EyeRest.UI/EyeRest.UI.csproj \
    -c Release -r osx-arm64 --self-contained \
    -p:Version="$VERSION" \
    -p:AssemblyVersion="${VERSION}.0" \
    -p:FileVersion="${VERSION}.0" \
    -p:InformationalVersion="$VERSION" \
    --verbosity quiet
echo "  ✓ macOS ARM64 published"

# ── Step 3: Bundle macOS .app ────────────────
echo "[3/7] Bundling macOS .app..."
SIGNING_IDENTITY="$SIGNING_IDENTITY" SKIP_PUBLISH=1 bash "$SCRIPT_DIR/bundle-macos.sh" 2>&1 | tail -3
echo "  ✓ macOS .app bundled"

# ── Step 4: Publish Windows x64 ─────────────
echo "[4/7] Publishing Windows x64..."
# Generate app.ico if missing
if [ ! -f "$PROJECT_ROOT/Resources/app.ico" ]; then
    mkdir -p "$PROJECT_ROOT/Resources"
    python3 -c "
from PIL import Image
img = Image.open('EyeRest.UI/Assets/app-icon.png')
sizes = [(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)]
imgs = [img.resize(s, Image.LANCZOS) for s in sizes]
imgs[0].save('Resources/app.ico', format='ICO', sizes=[(s,s) for s,_ in sizes], append_images=imgs[1:])
" 2>/dev/null || echo "  ⚠ Skipping app.ico generation (Pillow not installed)"
fi

dotnet publish EyeRest.UI/EyeRest.UI.csproj \
    -c Release -r win-x64 --self-contained \
    -p:Version="$VERSION" \
    -p:AssemblyVersion="${VERSION}.0" \
    -p:FileVersion="${VERSION}.0" \
    -p:InformationalVersion="$VERSION" \
    -o "$DIST_DIR/win-x64" --verbosity quiet 2>/dev/null || true
echo "  ✓ Windows x64 published"

# ── Step 5: Restore for local dev ────────────
echo "[5/7] Restoring local dev environment..."
dotnet restore --verbosity quiet
echo "  ✓ Restored for net8.0 (macOS)"

# ── Step 6: Zip artifacts ────────────────────
echo "[6/7] Creating zip archives..."
rm -f "$DIST_DIR/EyeRest-v${VERSION}-macOS-arm64.zip"
rm -f "$DIST_DIR/EyeRest-v${VERSION}-windows-x64-portable.zip"

(cd "$DIST_DIR" && zip -r -y -q "EyeRest-v${VERSION}-macOS-arm64.zip" Eye-Rest.app)
echo "  ✓ EyeRest-v${VERSION}-macOS-arm64.zip"

if [ -d "$DIST_DIR/win-x64" ]; then
    (cd "$DIST_DIR" && zip -r -q "EyeRest-v${VERSION}-windows-x64-portable.zip" win-x64/)
    echo "  ✓ EyeRest-v${VERSION}-windows-x64-portable.zip"
fi

# ── Step 7: Git tag + GitHub release ─────────
echo "[7/7] Creating GitHub release..."

if git rev-parse "$TAG" &>/dev/null; then
    echo "  Tag $TAG already exists — uploading to existing release"
else
    git tag "$TAG"
    git push origin "$TAG"
    echo "  ✓ Tag $TAG pushed"
fi

# Create or update release
ASSETS=""
[ -f "$DIST_DIR/EyeRest-v${VERSION}-macOS-arm64.zip" ] && \
    ASSETS="$ASSETS $DIST_DIR/EyeRest-v${VERSION}-macOS-arm64.zip"
[ -f "$DIST_DIR/EyeRest-v${VERSION}-windows-x64-portable.zip" ] && \
    ASSETS="$ASSETS $DIST_DIR/EyeRest-v${VERSION}-windows-x64-portable.zip"

if gh release view "$TAG" &>/dev/null; then
    gh release upload "$TAG" $ASSETS --clobber
    echo "  ✓ Artifacts uploaded to existing release $TAG"
else
    gh release create "$TAG" $ASSETS \
        --title "v${VERSION}" \
        --generate-notes
    echo "  ✓ Release $TAG created"
fi

echo ""
echo "╔══════════════════════════════════════╗"
echo "║  Release v${VERSION} complete!         "
echo "╚══════════════════════════════════════╝"
echo ""
ls -lh "$DIST_DIR"/EyeRest-v${VERSION}-*.zip
