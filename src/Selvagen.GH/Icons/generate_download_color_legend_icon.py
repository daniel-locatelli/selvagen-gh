"""
Generate DownloadColorLegend.png — sister to UploadColorLegend with the
arrow inverted (pointing down into the bar).
"""

import os
from PIL import Image, ImageDraw, ImageFilter

SCALE = 4
HI = 24 * SCALE
FINAL = 24
OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def s(v):
    return int(v * SCALE)


def add_shadow(img):
    alpha = img.split()[3]
    shadow = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    shadow_layer = Image.new("RGBA", (HI, HI), (0, 0, 0, 65))
    shadow.paste(shadow_layer, mask=alpha)
    offset = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    offset.paste(shadow, (s(1), s(1)))
    offset = offset.filter(ImageFilter.GaussianBlur(radius=s(2)))
    return Image.alpha_composite(offset, img)


def draw_gradient_bar(canvas, x0, y0, x1, y1, stops):
    x0s, y0s, x1s, y1s = s(x0), s(y0), s(x1), s(y1)
    width = x1s - x0s
    if width <= 0:
        return
    d = ImageDraw.Draw(canvas)
    n = len(stops)
    for px in range(width):
        t = px / max(width - 1, 1)
        seg = t * (n - 1)
        i = int(seg)
        j = min(i + 1, n - 1)
        f = seg - i
        c0, c1 = stops[i], stops[j]
        r = int(c0[0] + (c1[0] - c0[0]) * f)
        g = int(c0[1] + (c1[1] - c0[1]) * f)
        b = int(c0[2] + (c1[2] - c0[2]) * f)
        d.line([(x0s + px, y0s), (x0s + px, y1s)], fill=(r, g, b, 255))


def main():
    img = Image.new("RGBA", (HI, HI), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    stops = [(45, 125, 70), (245, 230, 66), (214, 48, 49)]
    bar_x0, bar_y0, bar_x1, bar_y1 = 3, 13, 21, 19
    draw_gradient_bar(img, bar_x0, bar_y0, bar_x1, bar_y1, stops)

    OUTLINE = (40, 40, 40, 255)
    d.rectangle(
        [s(bar_x0), s(bar_y0), s(bar_x1), s(bar_y1)],
        outline=OUTLINE,
        width=s(0.5),
    )

    # Downward arrow above the bar — tip near the top of the bar
    ARROW = (50, 50, 50, 255)
    shaft_x = 12
    d.line(
        [(s(shaft_x), s(2)), (s(shaft_x), s(8.5))],
        fill=ARROW,
        width=s(1.5),
    )
    head = [
        (s(shaft_x), s(11)),         # tip (bottom)
        (s(shaft_x - 3), s(7)),      # left base
        (s(shaft_x + 3), s(7)),      # right base
    ]
    d.polygon(head, fill=ARROW)

    img = add_shadow(img)
    img = img.resize((FINAL, FINAL), Image.LANCZOS)

    out_path = os.path.join(OUT_DIR, "DownloadColorLegend.png")
    img.save(out_path)
    print(f"Wrote {out_path}  ({os.path.getsize(out_path)} bytes)")


if __name__ == "__main__":
    main()
