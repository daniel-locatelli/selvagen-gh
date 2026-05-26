"""
Generate 24x24 Grasshopper component icons for Selvagen plugin.

Downloads solid Material Design Icons from the Iconify API.
Family icons use a shared base with a small badge differentiator.
Rendered at 4x (96x96) then downscaled to 24x24 with LANCZOS.

Run: python generate_icons.py
"""

import os
import urllib.request
import cairosvg
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from io import BytesIO

SCALE = 4
HI = 24 * SCALE          # 96
FINAL = 24
PADDING = 12              # ~2px at final size
ICON_SIZE = HI - PADDING * 2  # 72

# Composite layout: base + badge
BASE_SIZE = 64
BADGE_SIZE = 48
BASE_POS = (2, 4)
BADGE_POS = (52, 52)

OUT = os.path.dirname(os.path.abspath(__file__))
FILL_COLOR = "%23111111"

# ── Icon families ─────────────────────────────────────────────────────
# Each family shares a base icon; the badge differentiates siblings.

TOPO_BASE = "mdi:image-filter-hdr"
GEO_BASE = "mdi:layers"
ANL_BASE = "mdi:chart-box"
OPT_BASE = "mdi:creation"

FAMILY_ICONS = {
    # 03 Topography — mountain base
    "TopoBase":         (TOPO_BASE, None),
    "TopoContours":     (TOPO_BASE, "mdi:approximately-equal"),
    "TopoUrbanization": (TOPO_BASE, "mdi:home-city"),
    "TopoElevation":    (TOPO_BASE, "mdi:arrow-up-bold"),
    "TopoSlope":        (TOPO_BASE, "mdi:angle-acute"),
    "TopoDrainage":     (TOPO_BASE, "mdi:water"),

    # 04 Geology — layers/strata base
    "GeoCoverage":      (GEO_BASE, "mdi:map-marker"),
    "GeoRock":          (GEO_BASE, "mdi:diamond-stone"),
    "GeoRippability":   (GEO_BASE, "mdi:hammer"),
    "GeoSoil":          (GEO_BASE, "mdi:shovel"),
    "GeoDepth":         (GEO_BASE, "mdi:arrow-down-bold"),

    # 05 Analysis — chart base
    "AnlEarthworks":    (ANL_BASE, "mdi:shovel"),
    "AnlRetention":     (ANL_BASE, "mdi:wall"),
    "AnlRock":          (ANL_BASE, "mdi:diamond-stone"),
    "AnlAccess":        (ANL_BASE, "mdi:road-variant"),

    # 06 Optimizations — sparkle/creation base
    "OptAccess":        (OPT_BASE, "mdi:road-variant"),
    "OptEarthTerrain":  (OPT_BASE, "mdi:terrain"),
    "OptEarthLots":     (OPT_BASE, "mdi:grid"),
    "OptEarthTotal":    (OPT_BASE, "mdi:sigma"),
    "OptRetention":     (OPT_BASE, "mdi:wall"),
}

# ── Standalone icons (no family compositing) ──────────────────────────

UPLOAD_DOWNLOAD_ICONS = {
    # Upload — base + up arrow
    "UploadMesh":           ("mdi:vector-triangle",  "mdi:arrow-up-bold"),
    "UploadCurves":         ("mdi:vector-curve",     "mdi:arrow-up-bold"),
    "UploadLabels":         ("mdi:format-text",      "mdi:arrow-up-bold"),
    "UploadAnimation":      ("mdi:animation-play",   "mdi:arrow-up-bold"),

    # Download — base + down arrow
    "DownloadMesh":         ("mdi:vector-triangle",  "mdi:arrow-down-bold"),
    "DownloadCurves":       ("mdi:vector-curve",     "mdi:arrow-down-bold"),
    "DownloadLabels":       ("mdi:format-text",      "mdi:arrow-down-bold"),
    "DownloadAnimation":    ("mdi:animation-play",   "mdi:arrow-down-bold"),
}

# ── Standalone icons (no family compositing) ──────────────────────────

STANDALONE_ICONS = {
    # 01 Auth
    "Login":            "mdi:login",

    # 02 Admin
    "Clients":          "mdi:office-building",
    "Projects":         "mdi:folder-open",
    "Delete":           "mdi:delete",

    # 07 Shared
    "Properties":       "mdi:tune",

    # 08 Assets
    "ListAssets":       "mdi:format-list-bulleted",
}

# ── Numbered family icons (base + badge + number overlay) ─────────────
# Same base+badge as FAMILY_ICONS but with a bold number in the corner.

NUMBERED_ICONS = {
    "TopoAccess5": (TOPO_BASE, "mdi:road-variant", "5"),
    "TopoAccess8": (TOPO_BASE, "mdi:road-variant", "8"),
}

_svg_cache = {}


def download_svg(icon_id: str) -> bytes:
    if icon_id in _svg_cache:
        return _svg_cache[icon_id]
    prefix, name = icon_id.split(":")
    url = f"https://api.iconify.design/{prefix}/{name}.svg?color={FILL_COLOR}"
    req = urllib.request.Request(url, headers={"User-Agent": "Selvagen-IconGen/1.0"})
    with urllib.request.urlopen(req, timeout=15) as resp:
        data = resp.read()
    _svg_cache[icon_id] = data
    return data


def svg_to_png(svg_bytes: bytes, size: int) -> Image.Image:
    png_bytes = cairosvg.svg2png(
        bytestring=svg_bytes,
        output_width=size,
        output_height=size,
    )
    return Image.open(BytesIO(png_bytes)).convert("RGBA")


def add_halo(img, radius=3):
    """White halo around non-transparent pixels so badge reads over the base."""
    alpha = img.split()[3]
    expanded = alpha.filter(ImageFilter.MaxFilter(radius * 2 + 1))
    halo = Image.new("RGBA", img.size, (0, 0, 0, 0))
    white = Image.new("RGBA", img.size, (255, 255, 255, 255))
    halo = Image.composite(white, halo, expanded)
    halo.paste(img, (0, 0), img)
    return halo


def generate_standalone(component_name: str, icon_id: str):
    svg = download_svg(icon_id)
    icon_img = svg_to_png(svg, ICON_SIZE)
    canvas = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    canvas.paste(icon_img, (PADDING, PADDING), icon_img)
    img_final = canvas.resize((FINAL, FINAL), Image.LANCZOS)
    path = os.path.join(OUT, f"{component_name}.png")
    img_final.save(path)
    print(f"  {component_name}.png  <-  {icon_id}")


def generate_composite(component_name: str, base_id: str, badge_id: str | None):
    base_svg = download_svg(base_id)

    if badge_id is None:
        base_img = svg_to_png(base_svg, ICON_SIZE)
        canvas = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
        canvas.paste(base_img, (PADDING, PADDING), base_img)
    else:
        base_img = svg_to_png(base_svg, BASE_SIZE)
        badge_svg = download_svg(badge_id)
        badge_img = svg_to_png(badge_svg, BADGE_SIZE)
        badge_img = add_halo(badge_img, radius=3)

        canvas = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
        canvas.paste(base_img, BASE_POS, base_img)
        canvas.paste(badge_img, BADGE_POS, badge_img)

    img_final = canvas.resize((FINAL, FINAL), Image.LANCZOS)
    path = os.path.join(OUT, f"{component_name}.png")
    img_final.save(path)
    badge_label = f" + {badge_id}" if badge_id else ""
    print(f"  {component_name}.png  <-  {base_id}{badge_label}")


def generate_numbered_composite(component_name: str, base_id: str, badge_id: str, number: str):
    """Composite icon with a bold number drawn over the badge position."""
    base_svg = download_svg(base_id)
    base_img = svg_to_png(base_svg, BASE_SIZE)
    badge_svg = download_svg(badge_id)
    badge_img = svg_to_png(badge_svg, BADGE_SIZE)
    badge_img = add_halo(badge_img, radius=3)

    canvas = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    canvas.paste(base_img, BASE_POS, base_img)
    canvas.paste(badge_img, BADGE_POS, badge_img)

    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype("arialbd.ttf", 36)
    except OSError:
        try:
            font = ImageFont.truetype("Arial Bold.ttf", 36)
        except OSError:
            font = ImageFont.load_default()

    num_bbox = draw.textbbox((0, 0), number, font=font)
    num_w = num_bbox[2] - num_bbox[0]
    num_h = num_bbox[3] - num_bbox[1]
    num_x = HI - num_w - 4 - 8
    num_y = 0

    draw.text((num_x + 1, num_y + 1), number, fill=(255, 255, 255, 200), font=font)
    draw.text((num_x, num_y), number, fill=(17, 17, 17, 255), font=font)

    img_final = canvas.resize((FINAL, FINAL), Image.LANCZOS)
    path = os.path.join(OUT, f"{component_name}.png")
    img_final.save(path)
    print(f"  {component_name}.png  <-  {base_id} + {badge_id} [{number}]")


def main():
    total = len(FAMILY_ICONS) + len(NUMBERED_ICONS) + len(UPLOAD_DOWNLOAD_ICONS) + len(STANDALONE_ICONS)
    print(f"Generating {total} icons ({len(FAMILY_ICONS)} family, {len(NUMBERED_ICONS)} numbered, {len(UPLOAD_DOWNLOAD_ICONS)} upload/download, {len(STANDALONE_ICONS)} standalone)...\n")

    errors = []

    print("-- Family icons (base + badge) --")
    for comp, (base_id, badge_id) in FAMILY_ICONS.items():
        try:
            generate_composite(comp, base_id, badge_id)
        except Exception as e:
            errors.append((comp, str(e)))
            print(f"  ERROR {comp}: {e}")

    print("\n-- Numbered family icons (base + badge + number) --")
    for comp, (base_id, badge_id, number) in NUMBERED_ICONS.items():
        try:
            generate_numbered_composite(comp, base_id, badge_id, number)
        except Exception as e:
            errors.append((comp, str(e)))
            print(f"  ERROR {comp}: {e}")

    print("\n-- Upload/Download icons (base + arrow badge) --")
    for comp, (base_id, badge_id) in UPLOAD_DOWNLOAD_ICONS.items():
        try:
            generate_composite(comp, base_id, badge_id)
        except Exception as e:
            errors.append((comp, str(e)))
            print(f"  ERROR {comp}: {e}")

    print("\n-- Standalone icons --")
    for comp, icon_id in STANDALONE_ICONS.items():
        try:
            generate_standalone(comp, icon_id)
        except Exception as e:
            errors.append((comp, str(e)))
            print(f"  ERROR {comp}: {e}")

    print(f"\nDone: {total - len(errors)} succeeded, {len(errors)} failed")
    if errors:
        print("\nFailed:")
        for comp, err in errors:
            print(f"  {comp}: {err}")


if __name__ == "__main__":
    main()
