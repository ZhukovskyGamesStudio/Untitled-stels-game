"""The pictures of what hangs on walls: prints, photographs, a certificate, a calendar, an evacuation plan, signs.

Run:  python Tools/Art/generate_wall_things.py  → Assets/_Game/Art/Textures/Covers/wall_things.png (2048, 4 x 4 cells)

The cells and their rectangles are Tools/Blender/wall_things_atlas.py's. Everything is invented. Deterministic.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Blender"))
import wall_things_atlas as at  # noqa: E402

OUT = os.path.join(HERE, "..", "..", "Assets", "_Game", "Art", "Textures", "Covers", "wall_things.png")
S = 2                                                       # pixels per atlas unit
FONTS = "C:/Windows/Fonts/"


def font(name, size):
    try:
        return ImageFont.truetype(FONTS + name, int(size))
    except OSError:
        return ImageFont.load_default()


def canvas(index, bg):
    x0, y0, x1, y1 = at.rect(index)
    w, h = int((x1 - x0) * S), int((y1 - y0) * S)
    img = Image.new("RGB", (w, h), bg)
    return img, ImageDraw.Draw(img), w, h


def grain(img, rng, amount=5):
    a = np.asarray(img).astype(np.int16)
    a += rng.integers(-amount, amount + 1, a.shape[:2])[..., None].astype(np.int16)
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def sky(w, h, top, bottom):
    a = np.linspace(0, 1, h)[:, None, None]
    g = np.array(top)[None, None, :] * (1 - a) + np.array(bottom)[None, None, :] * a
    return Image.fromarray(np.repeat(g, w, axis=1).astype(np.uint8))


def ridge(d, w, h, base, amp, colour, rng, rough=6):
    pts = [(0, h)]
    ph = rng.uniform(0, 6.28, 4)
    for x in range(0, w + 8, 8):
        t = x / w
        y = base + amp * (math.sin(t * 5 + ph[0]) * 0.5 + math.sin(t * 11 + ph[1]) * 0.25 + math.sin(t * 23 + ph[2]) * 0.12) + rng.uniform(-rough, rough)
        pts.append((x, y))
    pts.append((w, h))
    d.polygon(pts, fill=colour)


def landscape_a(i, rng):
    img, d, w, h = canvas(i, (0, 0, 0))
    img.paste(sky(w, h, (160, 190, 214), (236, 222, 196)))
    d = ImageDraw.Draw(img)
    d.ellipse((w * 0.68, h * 0.16, w * 0.68 + h * 0.16, h * 0.32), fill=(250, 240, 214))
    ridge(d, w, h, h * 0.5, h * 0.1, (120, 140, 160), rng)
    ridge(d, w, h, h * 0.62, h * 0.08, (84, 112, 110), rng)
    ridge(d, w, h, h * 0.76, h * 0.05, (60, 88, 66), rng)
    ridge(d, w, h, h * 0.9, h * 0.03, (44, 64, 44), rng, 3)
    return grain(img.filter(ImageFilter.GaussianBlur(1.2)), rng)


def landscape_b(i, rng):
    img, d, w, h = canvas(i, (0, 0, 0))
    img.paste(sky(w, h, (70, 80, 130), (240, 160, 100)))
    d = ImageDraw.Draw(img)
    d.ellipse((w * 0.42, h * 0.44, w * 0.42 + h * 0.2, h * 0.64), fill=(255, 220, 150))
    d.rectangle((0, h * 0.58, w, h), fill=(40, 60, 96))
    for k in range(60):                                          # the sun's road on the water
        y = h * 0.6 + k * h * 0.4 / 60
        half = (3 + k * 0.9) * rng.uniform(0.5, 1.2)
        d.line((w * 0.5 - half, y, w * 0.5 + half, y), fill=(240, 180, 120), width=2)
    ridge(d, w, h, h * 0.57, h * 0.025, (30, 34, 60), rng, 2)
    d.polygon([(w * 0.14, h * 0.7), (w * 0.2, h * 0.52), (w * 0.26, h * 0.7)], fill=(24, 26, 40))   # a sail
    d.line((w * 0.1, h * 0.71, w * 0.3, h * 0.71), fill=(24, 26, 40), width=5)
    return grain(img.filter(ImageFilter.GaussianBlur(1.0)), rng)


def abstract_a(i, rng):
    img, d, w, h = canvas(i, (232, 226, 212))
    cols = [(196, 70, 50), (40, 60, 100), (220, 170, 60), (60, 60, 60), (150, 170, 160)]
    for k in range(9):
        x, y = rng.uniform(0.05, 0.7) * w, rng.uniform(0.05, 0.75) * h
        ww, hh = rng.uniform(0.12, 0.4) * w, rng.uniform(0.08, 0.3) * h
        d.rectangle((x, y, x + ww, y + hh), fill=cols[k % len(cols)])
    for k in range(4):
        y = rng.uniform(0.1, 0.9) * h
        d.line((w * 0.05, y, w * 0.95, y + rng.uniform(-30, 30)), fill=(30, 30, 30), width=5)
    return grain(img, rng, 7)


def abstract_b(i, rng):
    img, d, w, h = canvas(i, (28, 40, 60))
    for k in range(7, 0, -1):
        r = k * w * 0.065
        c = (int(230 - k * 22), int(120 + k * 12), int(60 + k * 20))
        d.ellipse((w * 0.42 - r, h * 0.55 - r, w * 0.42 + r, h * 0.55 + r), fill=c)
    d.ellipse((w * 0.62, h * 0.12, w * 0.9, h * 0.4), fill=(236, 226, 200))
    d.rectangle((w * 0.08, h * 0.08, w * 0.2, h * 0.9), fill=(200, 60, 50))
    return grain(img, rng, 7)


def photo(i, rng, city):
    img, d, w, h = canvas(i, (0, 0, 0))
    img.paste(sky(w, h, (70, 70, 72), (200, 198, 192)))
    d = ImageDraw.Draw(img)
    if city:
        x = 0
        while x < w:
            bw = rng.uniform(0.06, 0.14) * w
            top = rng.uniform(0.25, 0.7) * h
            g = int(rng.uniform(30, 80))
            d.rectangle((x, top, x + bw, h), fill=(g, g, g + 4))
            for yy in np.arange(top + 10, h - 6, 16):
                for xx in np.arange(x + 6, x + bw - 8, 12):
                    if rng.random() < 0.35:
                        d.rectangle((xx, yy, xx + 5, yy + 8), fill=(210, 206, 190))
            x += bw + 2
    else:
        ridge(d, w, h, h * 0.45, h * 0.18, (120, 120, 122), rng, 10)
        ridge(d, w, h, h * 0.62, h * 0.1, (70, 70, 72), rng)
        ridge(d, w, h, h * 0.84, h * 0.05, (30, 30, 32), rng, 3)
    return grain(img.filter(ImageFilter.GaussianBlur(0.8)), rng, 9)


def lines(d, x0, x1, y, n, gap, rng, fill=(70, 70, 76), width=3):
    for k in range(n):
        d.line((x0, y + k * gap, x0 + (x1 - x0) * rng.uniform(0.6, 1.0), y + k * gap), fill=fill, width=width)


def certificate(i, rng):
    img, d, w, h = canvas(i, (240, 234, 214))
    d.rectangle((14, 14, w - 14, h - 14), outline=(150, 120, 60), width=6)
    d.rectangle((26, 26, w - 26, h - 26), outline=(150, 120, 60), width=2)
    d.text((w / 2, h * 0.17), "CERTIFICATE", font=font("georgiab.ttf", w * 0.105), fill=(60, 50, 40), anchor="mm")
    lines(d, w * 0.2, w * 0.8, h * 0.3, 2, 16, rng)
    d.text((w / 2, h * 0.47), "of merit", font=font("georgiai.ttf", w * 0.09), fill=(90, 70, 40), anchor="mm")
    lines(d, w * 0.16, w * 0.84, h * 0.58, 4, 15, rng)
    d.ellipse((w * 0.62, h * 0.74, w * 0.62 + h * 0.14, h * 0.88), fill=(170, 40, 36))
    d.line((w * 0.14, h * 0.84, w * 0.46, h * 0.84), fill=(60, 50, 40), width=2)
    d.line((w * 0.16, h * 0.82, w * 0.24, h * 0.78, w * 0.3, h * 0.83, w * 0.4, h * 0.79), fill=(30, 40, 110), width=3)
    return grain(img, rng, 3)


def calendar(i, rng):
    img, d, w, h = canvas(i, (244, 244, 240))
    top = int(h * 0.42)
    img.paste(sky(w, top, (110, 170, 210), (220, 230, 220)), (0, 0))
    d = ImageDraw.Draw(img)
    ridge(d, w, top, top * 0.6, top * 0.16, (70, 110, 80), rng)
    d.rectangle((0, top, w, top + h * 0.07), fill=(180, 40, 36))
    cw, ch = w / 7.6, (h - top - h * 0.12) / 5.4
    f = font("arialbd.ttf", ch * 0.5)
    n = 1
    for r in range(5):
        for c in range(7):
            if r == 0 and c < 2 or n > 30:
                continue
            x, y = w * 0.04 + c * cw + cw / 2, top + h * 0.1 + r * ch + ch / 2
            d.text((x, y), str(n), font=f, fill=(190, 40, 36) if c > 4 else (40, 40, 46), anchor="mm")
            if n in (9, 17):
                d.ellipse((x - cw * 0.42, y - ch * 0.42, x + cw * 0.42, y + ch * 0.42), outline=(30, 60, 160), width=3)
            n += 1
    return grain(img, rng, 3)


def evac_plan(i, rng):
    img, d, w, h = canvas(i, (240, 242, 240))
    d.rectangle((0, 0, w, h * 0.14), fill=(30, 130, 70))
    d.text((w / 2, h * 0.07), "EVACUATION PLAN", font=font("arialbd.ttf", h * 0.075), fill=(250, 250, 250), anchor="mm")
    x0, y0, x1, y1 = w * 0.06, h * 0.22, w * 0.68, h * 0.92
    d.rectangle((x0, y0, x1, y1), outline=(30, 30, 34), width=4)
    for fx in (0.3, 0.55, 0.8):
        d.line((x0 + (x1 - x0) * fx, y0, x0 + (x1 - x0) * fx, y0 + (y1 - y0) * 0.38), fill=(30, 30, 34), width=3)
        d.line((x0 + (x1 - x0) * fx, y1, x0 + (x1 - x0) * fx, y1 - (y1 - y0) * 0.38), fill=(30, 30, 34), width=3)
    mid = (y0 + y1) / 2
    d.line((x0 + 12, mid, x1 - 30, mid), fill=(30, 150, 70), width=6)
    d.polygon([(x1 - 30, mid - 12), (x1 - 8, mid), (x1 - 30, mid + 12)], fill=(30, 150, 70))
    d.ellipse((x0 + (x1 - x0) * 0.4 - 9, mid - 30, x0 + (x1 - x0) * 0.4 + 9, mid - 12), fill=(200, 40, 36))
    for k, c in enumerate(((30, 150, 70), (200, 40, 36), (30, 60, 160))):
        y = h * (0.3 + 0.16 * k)
        d.rectangle((w * 0.73, y, w * 0.78, y + h * 0.05), fill=c)
        lines(d, w * 0.8, w * 0.95, y + h * 0.012, 2, 8, rng, width=2)
    return grain(img, rng, 3)


def first_aid(i, rng):
    img, d, w, h = canvas(i, (30, 130, 70))
    d.rectangle((8, 8, w - 8, h - 8), outline=(245, 245, 245), width=6)
    a, b = w * 0.11, w * 0.3
    d.rectangle((w / 2 - a, h / 2 - b, w / 2 + a, h / 2 + b), fill=(245, 245, 245))
    d.rectangle((w / 2 - b, h / 2 - a, w / 2 + b, h / 2 + a), fill=(245, 245, 245))
    return grain(img, rng, 4)


def tin_sign(i, rng):
    img, d, w, h = canvas(i, (232, 190, 60))
    d.rectangle((10, 10, w - 10, h - 10), outline=(30, 30, 30), width=8)
    cx, cy, r = w * 0.24, h * 0.5, h * 0.3
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(30, 30, 30))
    d.ellipse((cx - r * 0.55, cy - r * 0.55, cx + r * 0.55, cy + r * 0.55), fill=(200, 200, 200))
    for k in range(5):
        a = k * 2 * math.pi / 5
        d.line((cx, cy, cx + math.cos(a) * r * 0.5, cy + math.sin(a) * r * 0.5), fill=(60, 60, 60), width=6)
    d.text((w * 0.7, h * 0.36), "TYRES", font=font("impact.ttf", h * 0.24), fill=(30, 30, 30), anchor="mm")
    d.text((w * 0.7, h * 0.66), "& SERVICE", font=font("arialbd.ttf", h * 0.13), fill=(170, 40, 30), anchor="mm")
    img = grain(img, rng, 8)
    d = ImageDraw.Draw(img)
    for _ in range(40):                                          # chipped enamel
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        d.ellipse((x, y, x + rng.uniform(2, 7), y + rng.uniform(2, 6)), fill=(110, 80, 50))
    return img


def health(i, rng):
    img, d, w, h = canvas(i, (236, 244, 248))
    d.rectangle((0, 0, w, h * 0.16), fill=(30, 110, 170))
    d.text((w / 2, h * 0.08), "WASH YOUR HANDS", font=font("arialbd.ttf", w * 0.085), fill=(250, 250, 250), anchor="mm")
    for k in range(4):
        cx, cy = w * (0.28 + 0.44 * (k % 2)), h * (0.34 + 0.3 * (k // 2))
        r = w * 0.15
        d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(30, 110, 170), width=5, fill=(250, 252, 252))
        d.rounded_rectangle((cx - r * 0.45, cy - r * 0.2, cx + r * 0.45, cy + r * 0.55), radius=10, fill=(236, 190, 160))
        for j in range(3):
            x = cx - r * 0.3 + j * r * 0.3
            d.polygon([(x, cy - r * 0.7), (x - 7, cy - r * 0.42), (x + 7, cy - r * 0.42)], fill=(70, 160, 220))
        d.text((cx - r * 0.95, cy - r * 0.95), str(k + 1), font=font("arialbd.ttf", r * 0.5), fill=(30, 110, 170), anchor="mm")
    lines(d, w * 0.12, w * 0.88, h * 0.9, 2, 14, rng)
    return grain(img, rng, 3)


def family(i, rng):
    img, d, w, h = canvas(i, (0, 0, 0))
    img.paste(sky(w, h, (150, 180, 200), (210, 200, 170)))
    d = ImageDraw.Draw(img)
    ridge(d, w, h, h * 0.62, h * 0.05, (90, 120, 80), rng)
    for x, s, c in ((0.3, 1.0, (60, 70, 110)), (0.52, 0.92, (150, 60, 70)), (0.7, 0.6, (220, 180, 70))):
        hh = h * 0.42 * s
        d.rounded_rectangle((w * x - w * 0.09 * s, h * 0.95 - hh, w * x + w * 0.09 * s, h * 0.98), radius=14, fill=c)
        r = w * 0.065 * s
        d.ellipse((w * x - r, h * 0.95 - hh - 2 * r, w * x + r, h * 0.95 - hh), fill=(226, 190, 160))
    return grain(img.filter(ImageFilter.GaussianBlur(0.8)), rng, 6)


def botanical(i, rng):
    img, d, w, h = canvas(i, (236, 230, 212))
    d.line((w * 0.5, h * 0.9, w * 0.48, h * 0.2), fill=(70, 100, 60), width=5)
    for k in range(9):
        y = h * (0.82 - k * 0.07)
        side = 1 if k % 2 else -1
        ln = w * (0.3 - k * 0.018)
        tip = (w * 0.49 + side * ln, y - h * 0.07)
        d.polygon([(w * 0.49, y), (w * 0.49 + side * ln * 0.5, y - h * 0.085), tip, (w * 0.49 + side * ln * 0.55, y - h * 0.005)], fill=(80 + k * 6, 130 + k * 5, 70))
        d.line((w * 0.49, y, tip[0], tip[1]), fill=(50, 80, 46), width=2)
    d.ellipse((w * 0.42, h * 0.1, w * 0.56, h * 0.2), fill=(200, 110, 130))
    lines(d, w * 0.3, w * 0.7, h * 0.94, 1, 8, rng, (90, 80, 70), 2)
    return grain(img, rng, 4)


def no_smoking(i, rng):
    img, d, w, h = canvas(i, (244, 244, 244))
    r = w * 0.4
    d.rectangle((w * 0.24, h * 0.46, w * 0.68, h * 0.54), fill=(40, 40, 40))
    d.rectangle((w * 0.68, h * 0.46, w * 0.76, h * 0.54), fill=(200, 90, 40))
    d.ellipse((w / 2 - r, h / 2 - r, w / 2 + r, h / 2 + r), outline=(200, 30, 30), width=int(w * 0.07))
    d.line((w / 2 - r * 0.7, h / 2 - r * 0.7, w / 2 + r * 0.7, h / 2 + r * 0.7), fill=(200, 30, 30), width=int(w * 0.07))
    return grain(img, rng, 3)


DRAW = {
    "landscape_a": landscape_a, "landscape_b": landscape_b, "abstract_a": abstract_a, "abstract_b": abstract_b,
    "photo_a": lambda i, r: photo(i, r, False), "photo_b": lambda i, r: photo(i, r, True), "certificate": certificate, "calendar": calendar,
    "evac_plan": evac_plan, "first_aid": first_aid, "tin_sign": tin_sign, "health": health, "family": family, "botanical": botanical, "no_smoking": no_smoking,
}


def main():
    size = 1024 * S
    atlas = Image.new("RGB", (size, size), (128, 128, 128))
    for i, (name, _, _, make) in enumerate(at.THINGS):
        if not make:
            continue
        img = DRAW[name](i, np.random.default_rng(100 + i))
        x0, y0, _, _ = at.rect(i)
        # the picture's own border colour bled round it (mip levels never fetch the grey between cells)
        pad = img.resize((img.width + 16, img.height + 16), Image.BILINEAR)
        atlas.paste(pad, (int(x0 * S) - 8, int(y0 * S) - 8))
        atlas.paste(img, (int(x0 * S), int(y0 * S)))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    atlas.save(OUT)
    print("wrote", os.path.abspath(OUT))


main()
