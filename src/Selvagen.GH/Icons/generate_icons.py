"""
Generate 24x24 Grasshopper component icons for Selvagen plugin.

Design follows official GH icon guidelines:
  - 24x24 final size, 2px safe border (draw within 2-22)
  - Filled shapes with subtle gradients (not line art)
  - Dark gray/brown outlines on silhouette edges only
  - Drop shadow: 2px blur, 25% black, 1px right + 1px down
  - 1-2 colors per icon, distinct silhouettes
  - Rendered at 4x (96x96) then downscaled with LANCZOS

Run: python generate_icons.py
"""

from PIL import Image, ImageDraw, ImageFilter, ImageChops
import math
import os

SCALE = 4
HI = 24 * SCALE  # 96
FINAL = 24
OUT = os.path.dirname(os.path.abspath(__file__))

# ── Palette ──────────────────────────────────────────────────────────
# Near-black bodies with bright white features for high contrast.

OUTLINE = (10, 10, 10)          # almost black for silhouettes
OUTLINE_LIGHT = (25, 25, 25)    # slightly lighter for internal edges

# Grayscale tonal ranges (light, main, dark) — very dark bodies
GREEN_L  = (55, 55, 55)
GREEN    = (35, 35, 35)
GREEN_D  = (18, 18, 18)

GOLD_L   = (62, 62, 62)
GOLD     = (40, 40, 40)
GOLD_D   = (22, 22, 22)

BLUE_L   = (50, 50, 50)
BLUE     = (32, 32, 32)
BLUE_D   = (16, 16, 16)

TEAL_L   = (58, 58, 58)
TEAL     = (38, 38, 38)
TEAL_D   = (20, 20, 20)

RED_L    = (48, 48, 48)
RED      = (30, 30, 30)
RED_D    = (15, 15, 15)

BROWN_L  = (60, 60, 60)
BROWN    = (40, 40, 40)
BROWN_D  = (22, 22, 22)

PURPLE_L = (50, 50, 50)
PURPLE   = (32, 32, 32)
PURPLE_D = (16, 16, 16)

ORANGE_L = (58, 58, 58)
ORANGE   = (38, 38, 38)
ORANGE_D = (20, 20, 20)

SLATE_L  = (48, 48, 48)
SLATE    = (30, 30, 30)
SLATE_D  = (16, 16, 16)

# Uniform feature/highlight color — bright white for all secondary details
WHITE    = (220, 220, 220)
WHITE_D  = (185, 185, 185)

# ── Standardized widths (in 24-space) ────────────────────────────────
W_SILHOUETTE = 0.6    # outer silhouette outlines
W_INTERNAL   = 0.4    # internal detail lines / edges
W_GROUND     = 0.7    # ground lines, axis lines


def s(v):
    """Scale a coordinate from 24-space to HI-space."""
    return int(v * SCALE)


def sp(pts):
    """Scale a list of (x, y) tuples."""
    return [(s(x), s(y)) for x, y in pts]


def new():
    return Image.new("RGBA", (HI, HI), (0, 0, 0, 0))


def add_shadow(img):
    """Add GH-standard drop shadow: 2px blur, 25% black, offset 1,1."""
    alpha = img.split()[3]
    shadow = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    shadow_layer = Image.new("RGBA", (HI, HI), (0, 0, 0, 65))
    shadow.paste(shadow_layer, mask=alpha)
    offset = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    offset.paste(shadow, (s(1), s(1)))
    offset = offset.filter(ImageFilter.GaussianBlur(radius=s(2)))
    result = Image.alpha_composite(offset, img)
    return result


def gradient_fill(draw, pts, color_top, color_bot, bbox=None):
    """Fill a polygon with a vertical gradient by drawing horizontal slices."""
    temp = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    td = ImageDraw.Draw(temp)
    td.polygon(pts, fill=color_top)

    if bbox is None:
        ys = [p[1] for p in pts]
        y_min, y_max = min(ys), max(ys)
    else:
        y_min, y_max = bbox[1], bbox[3]

    height = max(y_max - y_min, 1)
    for y in range(y_min, y_max + 1):
        t = (y - y_min) / height
        r = int(color_top[0] + (color_bot[0] - color_top[0]) * t)
        g = int(color_top[1] + (color_bot[1] - color_top[1]) * t)
        b = int(color_top[2] + (color_bot[2] - color_top[2]) * t)
        td.line([(0, y), (HI, y)], fill=(r, g, b, 255))

    mask = Image.new("L", (HI, HI), 0)
    md = ImageDraw.Draw(mask)
    md.polygon(pts, fill=255)

    result = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    result.paste(temp, mask=mask)
    return result


def gradient_rect(color_top, color_bot, bbox):
    """Fill a rectangle with a vertical gradient."""
    x1, y1, x2, y2 = bbox
    pts = [(x1, y1), (x2, y1), (x2, y2), (x1, y2)]
    return gradient_fill(None, pts, color_top, color_bot, bbox)


def finalize(img, name):
    """Apply shadow and downscale to 24x24."""
    img = add_shadow(img)
    img = img.resize((FINAL, FINAL), Image.LANCZOS)
    path = os.path.join(OUT, f"{name}.png")
    img.save(path)
    print(f"  {name}.png")


# ── Auth ─────────────────────────────────────────────────────────────

def icon_login():
    """Golden key"""
    img = new()
    d = ImageDraw.Draw(img)

    # Key ring (filled circle)
    ring_box = [s(3), s(5), s(12), s(14)]
    d.ellipse(ring_box, fill=GOLD, outline=OUTLINE, width=s(W_SILHOUETTE))
    # Inner hole
    d.ellipse([s(5.5), s(7.5), s(9.5), s(11.5)], fill=(0, 0, 0, 0), outline=OUTLINE, width=s(W_INTERNAL))

    # Gradient overlay on ring
    ring_grad = gradient_fill(d,
        sp([(3, 5), (12, 5), (12, 14), (3, 14)]),
        GOLD_L, GOLD_D)
    mask = Image.new("L", (HI, HI), 0)
    md = ImageDraw.Draw(mask)
    md.ellipse(ring_box, fill=200)
    md.ellipse([s(5.5), s(7.5), s(9.5), s(11.5)], fill=0)
    img = Image.alpha_composite(img, Image.composite(ring_grad, Image.new("RGBA", (HI, HI), (0,0,0,0)), mask))
    d = ImageDraw.Draw(img)

    # Shaft
    shaft_pts = sp([(11, 8.5), (21, 8.5), (21, 10.5), (11, 10.5)])
    d.polygon(shaft_pts, fill=GOLD, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Teeth
    for tx in [17, 19.5]:
        tooth = sp([(tx, 10.5), (tx + 1.5, 10.5), (tx + 1.5, 14), (tx, 14)])
        d.polygon(tooth, fill=GOLD_D, outline=OUTLINE, width=s(W_INTERNAL))

    d.line([s(11), s(8.5), s(11), s(10.5)], fill=OUTLINE, width=s(W_INTERNAL))
    finalize(img, "Login")


def icon_clients():
    """Building with windows"""
    img = new()
    d = ImageDraw.Draw(img)

    # Main building body
    body = sp([(5, 5), (19, 5), (19, 21), (5, 21)])
    bgrad = gradient_fill(d, body, BLUE_L, BLUE_D)
    img = Image.alpha_composite(img, bgrad)
    d = ImageDraw.Draw(img)
    d.polygon(body, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Roof accent
    d.rectangle([s(5), s(5), s(19), s(7.5)], fill=BLUE_D, outline=OUTLINE, width=s(W_INTERNAL))

    # Windows (white — uniform feature color)
    for wx, wy in [(7.5, 9), (14, 9), (7.5, 14), (14, 14)]:
        d.rectangle([s(wx), s(wy), s(wx + 2.5), s(wy + 2.5)], fill=WHITE, outline=OUTLINE_LIGHT, width=s(W_INTERNAL))

    # Door
    d.rectangle([s(10), s(17), s(14), s(21)], fill=WHITE_D, outline=OUTLINE, width=s(W_INTERNAL))

    # Ground line
    d.line([s(2), s(21), s(22), s(21)], fill=OUTLINE, width=s(W_GROUND))

    finalize(img, "Clients")


def icon_projects():
    """Yellow folder"""
    img = new()
    d = ImageDraw.Draw(img)

    # Folder tab
    tab = sp([(3, 6), (10, 6), (12, 9), (3, 9)])
    d.polygon(tab, fill=GOLD_D, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Folder body
    body = sp([(3, 9), (21, 9), (21, 20), (3, 20)])
    bgrad = gradient_fill(d, body, GOLD_L, GOLD)
    img = Image.alpha_composite(img, bgrad)
    d = ImageDraw.Draw(img)
    d.polygon(body, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Fold line
    d.line([s(3), s(11), s(21), s(11)], fill=GOLD_D, width=s(W_INTERNAL))

    finalize(img, "Projects")


def icon_list_assets():
    """List with white bullets and bars"""
    img = new()
    d = ImageDraw.Draw(img)

    for i, y in enumerate([6, 11.5, 17]):
        # Bullet (white — uniform feature color)
        d.ellipse([s(4), s(y), s(7), s(y + 3)], fill=WHITE, outline=OUTLINE, width=s(W_INTERNAL))
        # Line bar
        bar = sp([(9, y + 0.3), (20, y + 0.3), (20, y + 2.7), (9, y + 2.7)])
        d.polygon(bar, fill=WHITE_D, outline=OUTLINE_LIGHT, width=s(W_INTERNAL))

    finalize(img, "ListAssets")


def icon_modules():
    """2x2 grid of rounded boxes — modular blocks"""
    img = new()
    d = ImageDraw.Draw(img)

    boxes = [
        (3, 3, 11, 11),
        (13, 3, 21, 11),
        (3, 13, 11, 21),
        (13, 13, 21, 21),
    ]
    grays = [
        (BLUE_L, BLUE_D),
        (TEAL_L, TEAL_D),
        (GREEN_L, GREEN_D),
        (SLATE_L, SLATE_D),
    ]
    for (x1, y1, x2, y2), (ct, cb) in zip(boxes, grays):
        pts = sp([(x1, y1), (x2, y1), (x2, y2), (x1, y2)])
        bgrad = gradient_fill(d, pts, ct, cb)
        img = Image.alpha_composite(img, bgrad)
        d = ImageDraw.Draw(img)
        d.rounded_rectangle([s(x1), s(y1), s(x2), s(y2)], radius=s(1), outline=OUTLINE, width=s(W_SILHOUETTE))

    finalize(img, "Modules")


def icon_delete():
    """Red trash can"""
    img = new()
    d = ImageDraw.Draw(img)

    # Lid
    d.rectangle([s(5), s(5.5), s(19), s(8)], fill=RED, outline=OUTLINE, width=s(W_SILHOUETTE))
    # Handle
    d.rectangle([s(9), s(3.5), s(15), s(5.5)], fill=RED_L, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Body (trapezoid)
    body = sp([(6, 8), (18, 8), (17, 21), (7, 21)])
    bgrad = gradient_fill(d, body, RED_L, RED_D)
    img = Image.alpha_composite(img, bgrad)
    d = ImageDraw.Draw(img)
    d.polygon(body, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Ribs (white — uniform feature color)
    for x in [10, 12, 14]:
        d.line([s(x), s(10.5), s(x), s(18.5)], fill=WHITE_D, width=s(W_INTERNAL))

    finalize(img, "Delete")


# ── Upload ───────────────────────────────────────────────────────────

def _up_arrow(d, cx, top, fill_c, outline_c):
    """Small filled upload arrow."""
    arrow_pts = sp([
        (cx, top),
        (cx - 3.5, top + 4),
        (cx - 1.5, top + 4),
        (cx - 1.5, top + 7),
        (cx + 1.5, top + 7),
        (cx + 1.5, top + 4),
        (cx + 3.5, top + 4),
    ])
    d.polygon(arrow_pts, fill=fill_c, outline=outline_c, width=s(W_INTERNAL))


def icon_upload_mesh():
    """Green wireframe triangle with up arrow"""
    img = new()
    d = ImageDraw.Draw(img)

    # Filled triangle
    tri = sp([(3, 21), (12, 5), (21, 21)])
    tgrad = gradient_fill(d, tri, GREEN_L, GREEN_D)
    img = Image.alpha_composite(img, tgrad)
    d = ImageDraw.Draw(img)
    d.polygon(tri, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Wireframe internal lines (white — uniform feature color)
    d.line([s(7.5), s(13), s(16.5), s(13)], fill=WHITE_D, width=s(W_INTERNAL))
    d.line([s(12), s(5), s(7.5), s(13)], fill=WHITE_D, width=s(W_INTERNAL))
    d.line([s(12), s(5), s(16.5), s(13)], fill=WHITE_D, width=s(W_INTERNAL))

    # Up arrow (top-right)
    _up_arrow(d, 18.5, 2, GREEN, OUTLINE)

    finalize(img, "UploadMesh")


def icon_upload_curves():
    """Teal S-curve with up arrow"""
    img = new()
    d = ImageDraw.Draw(img)

    # Thick s-curve as a filled ribbon
    pts_top = []
    pts_bot = []
    for i in range(80):
        x = s(2) + i * (s(20) // 80)
        y_center = s(14) + int(s(5) * math.sin(i * math.pi / 25))
        pts_top.append((x, y_center - s(1.5)))
        pts_bot.append((x, y_center + s(1.5)))

    ribbon = pts_top + list(reversed(pts_bot))
    rgrad = gradient_fill(d, ribbon, TEAL_L, TEAL_D)
    img = Image.alpha_composite(img, rgrad)
    d = ImageDraw.Draw(img)
    d.polygon(ribbon, outline=OUTLINE, width=s(W_INTERNAL))

    _up_arrow(d, 18.5, 2, TEAL, OUTLINE)

    finalize(img, "UploadCurves")


def icon_upload_labels():
    """Purple 'T' with up arrow"""
    img = new()
    d = ImageDraw.Draw(img)

    # T - top bar
    bar = sp([(3, 7), (16, 7), (16, 10.5), (3, 10.5)])
    bgrad = gradient_fill(d, bar, PURPLE_L, PURPLE)
    img = Image.alpha_composite(img, bgrad)
    d = ImageDraw.Draw(img)
    d.polygon(bar, outline=OUTLINE, width=s(W_SILHOUETTE))

    # T - stem
    stem = sp([(8, 10.5), (11, 10.5), (11, 21), (8, 21)])
    sgrad = gradient_fill(d, stem, PURPLE, PURPLE_D)
    img = Image.alpha_composite(img, sgrad)
    d = ImageDraw.Draw(img)
    d.polygon(stem, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Serif
    d.polygon(sp([(6, 21), (13, 21), (13, 19.5), (6, 19.5)]), fill=PURPLE_D, outline=OUTLINE, width=s(W_INTERNAL))

    _up_arrow(d, 18.5, 2, PURPLE, OUTLINE)

    finalize(img, "UploadLabels")


def icon_upload_animation():
    """Orange film frame with play triangle"""
    img = new()
    d = ImageDraw.Draw(img)

    # Frame
    frame = sp([(3, 4), (21, 4), (21, 21), (3, 21)])
    fgrad = gradient_fill(d, frame, ORANGE_L, ORANGE_D)
    img = Image.alpha_composite(img, fgrad)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([s(3), s(4), s(21), s(21)], radius=s(1.5), outline=OUTLINE, width=s(W_SILHOUETTE))

    # Sprocket holes (white — uniform feature color)
    for x in [5.5, 9, 12.5, 16]:
        d.rectangle([s(x), s(5), s(x + 1.5), s(6.5)], fill=WHITE_D)
        d.rectangle([s(x), s(18.5), s(x + 1.5), s(20)], fill=WHITE_D)

    # Play triangle (white — uniform feature color)
    play = sp([(9, 8.5), (9, 17), (17, 12.75)])
    d.polygon(play, fill=WHITE, outline=OUTLINE_LIGHT, width=s(W_INTERNAL))

    finalize(img, "UploadAnimation")


# ── Modules ──────────────────────────────────────────────────────────

def icon_topography():
    """Green mountain with snow cap"""
    img = new()
    d = ImageDraw.Draw(img)

    # Main mountain
    mtn = sp([(2, 21), (11, 4), (22, 21)])
    mgrad = gradient_fill(d, mtn, GREEN_L, GREEN_D)
    img = Image.alpha_composite(img, mgrad)
    d = ImageDraw.Draw(img)
    d.polygon(mtn, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Secondary peak
    pk2 = sp([(13, 21), (18, 9), (22, 21)])
    p2grad = gradient_fill(d, pk2, GREEN, GREEN_D)
    img = Image.alpha_composite(img, p2grad)
    d = ImageDraw.Draw(img)
    d.polygon(pk2, outline=OUTLINE, width=s(W_SILHOUETTE))

    # Snow cap on main peak (white — uniform feature color)
    snow = sp([(11, 4), (8.5, 9), (13.5, 9)])
    d.polygon(snow, fill=WHITE, outline=OUTLINE_LIGHT, width=s(W_INTERNAL))

    # Ground line
    d.line([s(2), s(21), s(22), s(21)], fill=OUTLINE, width=s(W_GROUND))

    finalize(img, "Topography")


def icon_geology():
    """Layered rock strata with white top layer"""
    img = new()

    colors = [
        (WHITE, WHITE_D),                      # top layer — white (uniform feature)
        ((90, 90, 90), (50, 50, 50)),          # second — medium
        ((55, 55, 55), (28, 28, 28)),          # third — dark
        ((25, 25, 25), (10, 10, 10)),          # bottom — near-black
    ]

    for i, ((ct, cb), y_base) in enumerate(zip(colors, [4, 8.5, 13, 17.5])):
        wave_top = []
        wave_bot = []
        for x in range(s(3), s(21) + 1):
            xn = (x - s(3)) / max(s(18), 1)
            yt = s(y_base) + int(s(1.2) * math.sin(xn * math.pi * 2.5 + i * 1.5))
            yb = yt + s(3.8)
            wave_top.append((x, yt))
            wave_bot.append((x, yb))

        poly = wave_top + list(reversed(wave_bot))
        layer = gradient_fill(None, poly, ct, cb)
        img = Image.alpha_composite(img, layer)
        d = ImageDraw.Draw(img)
        d.polygon(poly, outline=OUTLINE, width=s(W_INTERNAL))

    finalize(img, "Geology")


def icon_analyses():
    """Bar chart with axes"""
    img = new()
    d = ImageDraw.Draw(img)

    # Bars
    bars_data = [(6, 14, BLUE_L, BLUE), (10, 8, TEAL_L, TEAL),
                 (14, 11, BLUE, BLUE_D), (18, 5, TEAL, TEAL_D)]
    for x, top, ct, cb in bars_data:
        pts = sp([(x, top), (x + 3, top), (x + 3, 20), (x, 20)])
        bgrad = gradient_fill(d, pts, ct, cb)
        img = Image.alpha_composite(img, bgrad)
        d = ImageDraw.Draw(img)
        d.polygon(pts, outline=OUTLINE, width=s(W_INTERNAL))

    # Axes (white — uniform feature color)
    d.line([s(4), s(3), s(4), s(21)], fill=WHITE, width=s(W_GROUND))
    d.line([s(3), s(20.5), s(22), s(20.5)], fill=WHITE, width=s(W_GROUND))

    finalize(img, "Analyses")


def icon_optimizations():
    """Sparkle stars"""
    img = new()
    d = ImageDraw.Draw(img)

    def sparkle(cx, cy, size, ct, cb):
        """4-pointed star."""
        pts = sp([
            (cx, cy - size),
            (cx + size * 0.3, cy - size * 0.3),
            (cx + size, cy),
            (cx + size * 0.3, cy + size * 0.3),
            (cx, cy + size),
            (cx - size * 0.3, cy + size * 0.3),
            (cx - size, cy),
            (cx - size * 0.3, cy - size * 0.3),
        ])
        sg = gradient_fill(d, pts, ct, cb)
        nonlocal img
        img = Image.alpha_composite(img, sg)
        d2 = ImageDraw.Draw(img)
        d2.polygon(pts, outline=OUTLINE, width=s(W_INTERNAL))
        return d2

    # Large sparkle
    d = sparkle(10, 12, 6.5, TEAL_L, TEAL_D)
    # Medium sparkle (top-right)
    d = sparkle(19, 6, 3.5, TEAL_L, TEAL)
    # Small sparkle (bottom-right)
    d = sparkle(19.5, 18, 2, TEAL, TEAL_D)

    finalize(img, "Optimizations")


if __name__ == "__main__":
    print("Generating Selvagen GH icons (24x24, GH style)...")
    icon_login()
    icon_clients()
    icon_projects()
    icon_list_assets()
    icon_modules()
    icon_delete()
    icon_upload_mesh()
    icon_upload_curves()
    icon_upload_labels()
    icon_upload_animation()
    icon_topography()
    icon_geology()
    icon_analyses()
    icon_optimizations()
    print(f"\nDone! Icons saved to: {OUT}")
