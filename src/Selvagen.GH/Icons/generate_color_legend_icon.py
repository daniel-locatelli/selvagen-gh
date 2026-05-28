"""
Generate UploadColorLegend.png — a 24x24 GH-style icon with a horizontal
color gradient bar (the legend glyph) and a small upload arrow.

Follows the icon-design rules from creating-grasshopper-plugin/icons.md:
- Render at 4x (96x96) and downscale with LANCZOS for crisp anti-aliasing.
- 2px safe border, drop shadow (2px blur, 25% black, offset 1,1).
- No black outlines — use darker shade of fill.

Run:  pip install Pillow
      python generate_color_legend_icon.py
"""

import os
from PIL import Image, ImageDraw, ImageFilter

SCALE = 4
HI = 24 * SCALE  # 96
FINAL = 24
OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def s(v):
    return int(v * SCALE)


def add_shadow(img):
    """GH-standard drop shadow."""
    alpha = img.split()[3]
    shadow = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    shadow_layer = Image.new("RGBA", (HI, HI), (0, 0, 0, 65))
    shadow.paste(shadow_layer, mask=alpha)
    offset = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    offset.paste(shadow, (s(1), s(1)))
    offset = offset.filter(ImageFilter.GaussianBlur(radius=s(2)))
    return Image.alpha_composite(offset, img)


def draw_gradient_bar(canvas, x0, y0, x1, y1, stops):
    """Fill a rectangle with a horizontal gradient through the given color stops."""
    x0s, y0s, x1s, y1s = s(x0), s(y0), s(x1), s(y1)
    width = x1s - x0s
    if width <= 0:
        return
    d = ImageDraw.Draw(canvas)
    n_stops = len(stops)
    for px in range(width):
        t = px / max(width - 1, 1)
        # interpolate between stops
        seg = t * (n_stops - 1)
        i = int(seg)
        i_next = min(i + 1, n_stops - 1)
        f = seg - i
        c0 = stops[i]
        c1 = stops[i_next]
        r = int(c0[0] + (c1[0] - c0[0]) * f)
        g = int(c0[1] + (c1[1] - c0[1]) * f)
        b = int(c0[2] + (c1[2] - c0[2]) * f)
        d.line([(x0s + px, y0s), (x0s + px, y1s)], fill=(r, g, b, 255))


def main():
    img = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # ── Gradient bar (the legend glyph) ──
    # Span most of the 20x20 interior, sit slightly low to leave room for arrow
    # Stops mimic a typical analysis gradient: green → yellow → red
    stops = [
        (45, 125, 70),    # green
        (245, 230, 66),   # yellow
        (214, 48, 49),    # red
    ]
    bar_x0, bar_y0, bar_x1, bar_y1 = 3, 13, 21, 19
    draw_gradient_bar(img, bar_x0, bar_y0, bar_x1, bar_y1, stops)

    # Subtle dark outline around the bar (silhouette only, dark shade not black)
    OUTLINE = (40, 40, 40, 255)
    d.rectangle(
        [s(bar_x0), s(bar_y0), s(bar_x1), s(bar_y1)],
        outline=OUTLINE,
        width=s(0.5),
    )

    # ── Upload arrow (top portion) ──
    # An upward arrow positioned above-center of the bar. Dark gray, not black.
    ARROW = (50, 50, 50, 255)
    # Shaft (vertical line)
    shaft_x = 12
    d.line(
        [(s(shaft_x), s(2.5)), (s(shaft_x), s(10))],
        fill=ARROW,
        width=s(1.5),
    )
    # Arrowhead (triangle pointing up)
    head = [
        (s(shaft_x), s(2)),         # tip
        (s(shaft_x - 3), s(6)),     # left base
        (s(shaft_x + 3), s(6)),     # right base
    ]
    d.polygon(head, fill=ARROW)

    # ── Shadow + downscale ──
    img = add_shadow(img)
    img = img.resize((FINAL, FINAL), Image.LANCZOS)

    out_path = os.path.join(OUT_DIR, "UploadColorLegend.png")
    img.save(out_path)
    print(f"Wrote {out_path}  ({os.path.getsize(out_path)} bytes)")


if __name__ == "__main__":
    main()
