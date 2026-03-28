#!/usr/bin/env python3
"""
Generate Eye-rest application icons and tray state icons.

Produces:
  - EyeRest.UI/Assets/AppIcon.icns  (macOS dock icon)
  - Resources/app.ico               (Windows app icon)
  - EyeRest.UI/Assets/TrayIcons/    (9 state PNGs at 18px and 36px)

Requires: Pillow, iconutil (macOS)
"""

import os
import platform
import shutil
import struct
import subprocess
import sys
import tempfile
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageDraw

# Project root (one level up from scripts/)
ROOT = Path(__file__).resolve().parent.parent


# ---------------------------------------------------------------------------
# Drawing helpers
# ---------------------------------------------------------------------------

def draw_eye_icon(size: int, fill_color: tuple, border_color: tuple,
                   background: bool = False) -> Image.Image:
    """
    Draw the eye-rest icon matching the Windows CreateModernEyeIcon design.
    Returns an RGBA PIL Image at the given size.

    If background=True, draws a white rounded-rect background first (for dock icons).
    macOS applies its own squircle mask, so we fill the square with white.
    """
    # Draw at 4x then downscale for antialiasing
    scale = 4
    s = size * scale
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if background:
        # macOS icon grid: the squircle sits ~10% inset from the canvas edge,
        # matching system icons (Notes, Chrome, VS Code, etc.).
        # Corner radius is ~22.37% of the background rect size.
        inset = int(s * 0.10)
        bg_size = s - 2 * inset
        radius = int(bg_size * 0.2237)
        draw.rounded_rectangle(
            [inset, inset, s - inset - 1, s - inset - 1],
            radius=radius,
            fill=(255, 255, 255, 255),
        )

    # When background is used, draw the eye at ~80% of canvas (centered within squircle).
    # Without background (tray icons), the eye fills the full canvas.
    if background:
        eye_scale = 0.80
        eye_offset = s * (1 - eye_scale) / 2
    else:
        eye_scale = 1.0
        eye_offset = 0

    # Proportions based on the 32px Windows icon
    def r(v: float) -> float:
        """Scale a value from 32-unit coords to current canvas."""
        return eye_offset + v * (s * eye_scale) / 32.0

    # Outer eye ellipse
    eye_rect = [r(4), r(6), r(28), r(26)]
    draw.ellipse(eye_rect, fill=fill_color, outline=border_color, width=max(1, int(r(2))))

    # White inner eye
    inner_rect = [r(8), r(10), r(24), r(22)]
    draw.ellipse(inner_rect, fill=(255, 255, 255, 255))

    # Dark pupil
    pupil_rect = [r(12), r(12), r(20), r(20)]
    draw.ellipse(pupil_rect, fill=(33, 33, 33, 255))

    # Highlight reflection
    hl_rect = [r(13), r(13), r(16), r(16)]
    draw.ellipse(hl_rect, fill=(255, 255, 255, 255))

    # Downscale with high-quality resampling
    img = img.resize((size, size), Image.LANCZOS)
    return img


def draw_tray_eye_icon(size: int, fill_color: tuple, border_color: tuple) -> Image.Image:
    """
    Draw a tray/menu bar eye icon that fills the full canvas (minimal padding).
    The standard draw_eye_icon has ~20% padding which makes menu bar icons look small.
    This version uses tighter coordinates so the eye fills ~95% of the space.
    """
    scale = 4
    s = size * scale
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    def r(v: float) -> float:
        """Scale from 32-unit coords to current canvas."""
        return v * s / 32.0

    # Eye fills nearly the full canvas with minimal padding
    eye_rect = [r(0), r(3), r(32), r(29)]
    draw.ellipse(eye_rect, fill=fill_color, outline=border_color, width=max(1, int(r(2))))

    # White inner eye (proportionally larger)
    inner_rect = [r(5), r(7), r(27), r(25)]
    draw.ellipse(inner_rect, fill=(255, 255, 255, 255))

    # Dark pupil
    pupil_rect = [r(10), r(10), r(22), r(22)]
    draw.ellipse(pupil_rect, fill=(33, 33, 33, 255))

    # Highlight reflection
    hl_rect = [r(11), r(11), r(15), r(15)]
    draw.ellipse(hl_rect, fill=(255, 255, 255, 255))

    img = img.resize((size, size), Image.LANCZOS)
    return img


# ---------------------------------------------------------------------------
# State definitions (matching IconService.cs line 75-104)
# ---------------------------------------------------------------------------

STATES = {
    "active":          ((76, 175, 80),   (56, 142, 60)),
    "paused":          ((255, 193, 7),   (255, 160, 0)),
    "smart_paused":    ((255, 152, 0),   (230, 81, 0)),
    "manually_paused": ((255, 183, 77),  (255, 143, 0)),
    "meeting_mode":    ((156, 39, 176),  (123, 31, 162)),
    "user_away":       ((158, 158, 158), (117, 117, 117)),
    "break":           ((33, 150, 243),  (21, 101, 192)),
    "eye_rest":        ((0, 188, 212),   (0, 151, 167)),
    "error":           ((244, 67, 54),   (211, 47, 47)),
}

# Default state for the app icon
DEFAULT_FILL = (76, 175, 80)
DEFAULT_BORDER = (56, 142, 60)


# ---------------------------------------------------------------------------
# ICO generation
# ---------------------------------------------------------------------------

def make_ico(output_path: Path, sizes: list[int] = [16, 32, 48, 256]):
    """Generate a Windows .ico file with multiple sizes (with background for taskbar)."""
    images = []
    for sz in sizes:
        img = draw_eye_icon(sz, DEFAULT_FILL, DEFAULT_BORDER, background=True)
        images.append(img)

    # Save as ICO — the largest image must be first for Pillow to include all sizes
    output_path.parent.mkdir(parents=True, exist_ok=True)
    images_reversed = list(reversed(images))  # largest first
    images_reversed[0].save(
        str(output_path),
        format="ICO",
        append_images=images_reversed[1:],
        sizes=[(img.width, img.height) for img in images_reversed],
    )
    print(f"  Created {output_path} ({', '.join(f'{s}px' for s in sizes)})")


# ---------------------------------------------------------------------------
# ICNS generation (macOS only, uses iconutil)
# ---------------------------------------------------------------------------

def make_icns(output_path: Path):
    """Generate a macOS .icns file using iconutil."""
    if platform.system() != "Darwin":
        print("  Skipping .icns (not on macOS)")
        return

    # iconutil expects an .iconset directory with specific filenames
    iconset_sizes = {
        "icon_16x16.png": 16,
        "icon_16x16@2x.png": 32,
        "icon_32x32.png": 32,
        "icon_32x32@2x.png": 64,
        "icon_128x128.png": 128,
        "icon_128x128@2x.png": 256,
        "icon_256x256.png": 256,
        "icon_256x256@2x.png": 512,
        "icon_512x512.png": 512,
        "icon_512x512@2x.png": 1024,
    }

    with tempfile.TemporaryDirectory() as tmpdir:
        iconset_dir = Path(tmpdir) / "AppIcon.iconset"
        iconset_dir.mkdir()

        for filename, sz in iconset_sizes.items():
            img = draw_eye_icon(sz, DEFAULT_FILL, DEFAULT_BORDER, background=True)
            img.save(str(iconset_dir / filename), format="PNG")

        output_path.parent.mkdir(parents=True, exist_ok=True)
        result = subprocess.run(
            ["iconutil", "-c", "icns", str(iconset_dir), "-o", str(output_path)],
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            print(f"  ERROR: iconutil failed: {result.stderr}")
            sys.exit(1)

    print(f"  Created {output_path}")


# ---------------------------------------------------------------------------
# Tray state PNGs
# ---------------------------------------------------------------------------

def make_tray_icons(output_dir: Path):
    """Generate 22px and 44px (2x) state icons for macOS menu bar / tray.
    Uses 22px to match the macOS menu bar max height (22pt)."""
    output_dir.mkdir(parents=True, exist_ok=True)

    for state_name, (fill, border) in STATES.items():
        for sz, suffix in [(22, ""), (44, "@2x")]:
            img = draw_tray_eye_icon(sz, fill, border)
            filename = f"tray_{state_name}{suffix}.png"
            img.save(str(output_dir / filename), format="PNG")

    count = len(STATES) * 2
    print(f"  Created {count} tray icons in {output_dir}")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def make_app_icon_png(output_path: Path, size: int = 512):
    """Generate a high-res PNG for Avalonia resource embedding (used for macOS dock icon)."""
    img = draw_eye_icon(size, DEFAULT_FILL, DEFAULT_BORDER, background=True)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    img.save(str(output_path), format="PNG")
    print(f"  Created {output_path} ({size}px)")


def make_taskbar_icon_png(output_path: Path, size: int = 256):
    """Generate a Windows taskbar icon PNG — eye on transparent background (matches tray style)."""
    img = draw_tray_eye_icon(size, DEFAULT_FILL, DEFAULT_BORDER)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    img.save(str(output_path), format="PNG")
    print(f"  Created {output_path} ({size}px, transparent background)")


def make_msix_icons(output_dir: Path):
    """Generate all MSIX visual assets for Package.appxmanifest."""
    output_dir.mkdir(parents=True, exist_ok=True)

    # Square44x44Logo — app list, taskbar
    for scale, px in [(100, 44), (125, 55), (150, 66), (200, 88), (400, 176)]:
        img = draw_eye_icon(px, DEFAULT_FILL, DEFAULT_BORDER, background=True)
        img.save(str(output_dir / f"Square44x44Logo.scale-{scale}.png"))

    # Square44x44Logo target sizes (unplated, for taskbar)
    for px in [16, 24, 32, 48, 256]:
        img = draw_eye_icon(px, DEFAULT_FILL, DEFAULT_BORDER, background=True)
        img.save(str(output_dir / f"Square44x44Logo.targetsize-{px}.png"))

    # Square150x150Logo — medium tile
    for scale, px in [(100, 150), (125, 188), (150, 225), (200, 300), (400, 600)]:
        img = draw_eye_icon(px, DEFAULT_FILL, DEFAULT_BORDER, background=True)
        img.save(str(output_dir / f"Square150x150Logo.scale-{scale}.png"))

    # Wide310x150Logo — wide tile (eye centered on wide canvas)
    for scale, w, h in [(100, 310, 150), (200, 620, 300)]:
        canvas = Image.new("RGBA", (w, h), (255, 255, 255, 255))
        icon_size = h - 20
        eye = draw_eye_icon(icon_size, DEFAULT_FILL, DEFAULT_BORDER, background=False)
        x = (w - icon_size) // 2
        y = (h - icon_size) // 2
        canvas.paste(eye, (x, y), eye)
        canvas.save(str(output_dir / f"Wide310x150Logo.scale-{scale}.png"))

    # StoreLogo — Store listing
    for scale, px in [(100, 50), (125, 63), (150, 75), (200, 100), (400, 200)]:
        img = draw_eye_icon(px, DEFAULT_FILL, DEFAULT_BORDER, background=True)
        img.save(str(output_dir / f"StoreLogo.scale-{scale}.png"))

    count = 5 + 5 + 5 + 2 + 5  # 22 total
    print(f"  Created {count} MSIX visual assets in {output_dir}")


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Generate Eye-rest icons")
    parser.add_argument("--msix", action="store_true", help="Also generate MSIX visual assets")
    parser.add_argument("--msix-only", action="store_true", help="Only generate MSIX visual assets")
    args = parser.parse_args()

    print("Generating Eye-rest icons...")
    print()

    if not args.msix_only:
        # 1. Windows .ico
        print("[1/5] Windows app icon (.ico)")
        make_ico(ROOT / "Resources" / "app.ico")
        print()

        # 2. macOS .icns
        print("[2/5] macOS dock icon (.icns)")
        make_icns(ROOT / "EyeRest.UI" / "Assets" / "AppIcon.icns")
        print()

        # 3. App icon PNG (Avalonia embedded resource for macOS dock icon — white background)
        print("[3/5] App icon PNG (macOS dock — white background)")
        make_app_icon_png(ROOT / "EyeRest.UI" / "Assets" / "app-icon.png")
        print()

        # 4. Taskbar icon PNG (Windows taskbar — transparent background, matches tray style)
        print("[4/5] Taskbar icon PNG (Windows taskbar — transparent background)")
        make_taskbar_icon_png(ROOT / "EyeRest.UI" / "Assets" / "taskbar-icon.png")
        print()

        # 5. Tray state icons
        print("[5/5] Menu bar / tray state icons")
        make_tray_icons(ROOT / "EyeRest.UI" / "Assets" / "TrayIcons")
        print()

    if args.msix or args.msix_only:
        step = "[6/6]" if not args.msix_only else "[1/1]"
        print(f"{step} MSIX visual assets")
        make_msix_icons(ROOT / "EyeRest.Package" / "Images")
        print()

    print("Done!")


if __name__ == "__main__":
    main()
