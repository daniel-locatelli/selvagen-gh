"""
Generate the Yak package icon (icon.png) for the Selvagen plugin.

Renders the Selvagen brand mark (logo.svg) in white (#FAFAFC) centered on a
dark (#0A0A0A) full-bleed rounded-square tile. This is the PACKAGE icon shown
in Rhino's Package Manager — distinct from the 24x24 component icons produced
by generate_icons.py.

Run: python generate_package_icon.py
"""

import os
import cairosvg
from PIL import Image, ImageDraw
from io import BytesIO

# Source logo (white mark on transparent, 50x50 viewBox)
LOGO_SVG = r"C:\repos\selvagen\public\logo.svg"

# Output: overwrite the package icon next to manifest.yml (and the build copy).
HERE = os.path.dirname(os.path.abspath(__file__))
GH_DIR = os.path.dirname(HERE)
OUT_PATHS = [
    os.path.join(GH_DIR, "icon.png"),
    os.path.join(GH_DIR, "bin", "Release", "net8.0-windows", "icon.png"),
]

SIZE = 128                 # final icon dimensions
CORNER_RADIUS = 28         # rounded-tile radius (~22% of SIZE)
TILE_COLOR = (10, 10, 10, 255)      # #0A0A0A dark tile
MARK_FRACTION = 0.60       # visible mark spans 60% of the tile's longest side
RENDER_HI = 1024           # high-res logo render before downscale


def render_logo_mark() -> Image.Image:
    """Render logo.svg at high res and crop to the mark's actual bounds."""
    png = cairosvg.svg2png(url=LOGO_SVG, output_width=RENDER_HI, output_height=RENDER_HI)
    img = Image.open(BytesIO(png)).convert("RGBA")
    bbox = img.getbbox()          # tight box around non-transparent pixels
    return img.crop(bbox)


def build_icon() -> Image.Image:
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))

    # Dark rounded-square tile, full bleed.
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle([0, 0, SIZE - 1, SIZE - 1], radius=CORNER_RADIUS, fill=TILE_COLOR)

    # Scale the cropped mark so its longest side = MARK_FRACTION of the tile.
    mark = render_logo_mark()
    target = int(SIZE * MARK_FRACTION)
    w, h = mark.size
    scale = target / max(w, h)
    mark = mark.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)

    # Center the mark on the tile.
    mw, mh = mark.size
    canvas.alpha_composite(mark, ((SIZE - mw) // 2, (SIZE - mh) // 2))
    return canvas


def main():
    icon = build_icon()
    for path in OUT_PATHS:
        if os.path.isdir(os.path.dirname(path)):
            icon.save(path)
            print(f"wrote {path}  ({icon.size[0]}x{icon.size[1]})")
        else:
            print(f"skipped {path}  (directory missing)")


if __name__ == "__main__":
    main()
