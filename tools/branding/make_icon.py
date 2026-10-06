"""Generates the Plasma launcher icon (1024 px) + adaptive layers. Original art, no third-party assets.
Usage: python3 tools/branding/make_icon.py  -> Assets/Plasma/Branding/icon*.png"""
import math, os
from PIL import Image, ImageDraw, ImageFilter

S = 1024
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Plasma", "Branding")

def bg():
    im = Image.new("RGB", (S, S))
    px = im.load()
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - S * 0.5, y - S * 0.38) / (S * 0.75)
            t = min(1, d)
            px[x, y] = (int(40 * (1 - t) + 8 * t), int(70 * (1 - t) + 12 * t), int(170 * (1 - t) + 40 * t))
    return im

def fg():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # red horde band at the top
    for row in range(4):
        for col in range(9):
            cx = 200 + col * 78 + (row % 2) * 39; cy = 215 + row * 52
            d.ellipse([cx - 36, cy - 30, cx + 36, cy + 30], fill=(230, 40, 35, 255), outline=(120, 10, 15, 255), width=5)
    # glowing tracers
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0)); g = ImageDraw.Draw(glow)
    for i, x in enumerate([430, 512, 594]):
        g.rounded_rectangle([x - 16, 420 + (i % 2) * 40, x + 16, 610 + (i % 2) * 40], radius=16, fill=(255, 190, 40, 255))
    glow = glow.filter(ImageFilter.GaussianBlur(10))
    im.alpha_composite(glow)
    d = ImageDraw.Draw(im)
    for i, x in enumerate([430, 512, 594]):
        d.rounded_rectangle([x - 9, 430 + (i % 2) * 40, x + 9, 600 + (i % 2) * 40], radius=9, fill=(255, 245, 200, 255))
    # blue squad (helmets) at the bottom
    pts = []
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(19):
        r = 62 * math.sqrt(i + 0.5); a = i * golden
        pts.append((512 + r * math.cos(a), 760 + r * math.sin(a) * 0.62))
    for (x, y) in sorted(pts, key=lambda p: p[1]):
        d.ellipse([x - 52, y - 28, x + 52, y + 56], fill=(30, 30, 45, 255))           # body/shadow
        d.ellipse([x - 48, y - 46, x + 48, y + 34], fill=(40, 115, 255, 255), outline=(15, 50, 140, 255), width=6)
        d.ellipse([x - 26, y - 34, x + 2, y - 14], fill=(170, 210, 255, 255))         # helmet shine
    return im

def rounded(im, r=200):
    m = Image.new("L", (S, S), 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, S - 1, S - 1], radius=r, fill=255)
    out = im.convert("RGBA"); out.putalpha(m); return out

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    b, f = bg(), fg()
    full = b.convert("RGBA"); full.alpha_composite(f)
    full.convert("RGB").save(os.path.join(OUT, "icon.png"))
    b.save(os.path.join(OUT, "icon_bg.png"))
    # adaptive foreground must sit inside the inner 66% safe zone
    small = f.resize((int(S * 0.72), int(S * 0.72)), Image.LANCZOS)
    fg_layer = Image.new("RGBA", (S, S), (0, 0, 0, 0)); fg_layer.alpha_composite(small, ((S - small.width) // 2, (S - small.height) // 2))
    fg_layer.save(os.path.join(OUT, "icon_fg.png"))
    rounded(full).resize((512, 512), Image.LANCZOS).save(os.path.join(os.path.dirname(__file__), "..", "..", "docs", "store", "icon_store_512.png"))
    print("icons written to", os.path.abspath(OUT))
