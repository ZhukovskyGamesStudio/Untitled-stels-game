"""Labels of the small things one picks up: the coffee jar's, the tins', the pill box's, the matchbox's.

Run:  python Tools/Art/generate_labels.py  → Assets/_Game/Art/Textures/Covers/labels.png (1024 x 1024)

Rows of 256 px (Tools/Blender/items_more.py label_wrap / label_face read the same layout): 0 the coffee jar's label and
1, 2 the tins' — each runs once round its can, the full width of the sheet (the texture repeats sideways); 3 — the pill box's
face (left half) and the matchbox's (right half). Every brand is invented. Deterministic.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "Covers", "labels.png")
FONTS = "C:/Windows/Fonts/"
W, ROW = 1024, 256


def font(name, size):
    try:
        return ImageFont.truetype(FONTS + name, int(size))
    except OSError:
        return ImageFont.load_default()


def small_print(d, x, y, w, n, rng, fill):
    for k in range(n):
        d.line((x, y + k * 9, x + w * rng.uniform(0.6, 1.0), y + k * 9), fill=fill, width=2)


def barcode(d, x, y, w, h, rng):
    d.rectangle((x - 4, y - 4, x + w + 4, y + h + 4), fill=(245, 245, 240))
    xx = x
    while xx < x + w:
        bw = int(rng.integers(1, 4))
        d.rectangle((xx, y, xx + bw, y + h), fill=(20, 20, 20))
        xx += bw + int(rng.integers(1, 4))


def coffee(rng):
    img = Image.new("RGB", (W, ROW), (120, 24, 20))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, W, 22), fill=(214, 170, 70))
    d.rectangle((0, ROW - 22, W, ROW), fill=(214, 170, 70))
    for x0 in (0, 512):                                          # the front, twice round the jar
        d.ellipse((x0 + 110, 40, x0 + 400, 216), fill=(245, 232, 206))
        d.text((x0 + 255, 104), "MORNA", font=font("georgiab.ttf", 62), fill=(92, 40, 22), anchor="mm")
        d.text((x0 + 255, 158), "instant coffee", font=font("georgiai.ttf", 28), fill=(120, 24, 20), anchor="mm")
        for k in range(3):                                       # beans
            cx, cy = x0 + 200 + k * 55, 192
            d.ellipse((cx - 13, cy - 9, cx + 13, cy + 9), fill=(70, 36, 20))
            d.line((cx - 10, cy, cx + 10, cy), fill=(140, 90, 50), width=2)
        small_print(d, x0 + 430, 70, 70, 9, rng, (236, 200, 170))
    barcode(d, 440, 170, 60, 40, rng)
    return img


def tomatoes(rng):
    img = Image.new("RGB", (W, ROW), (236, 228, 204))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, W, 60), fill=(30, 110, 60))
    d.rectangle((0, ROW - 26, W, ROW), fill=(30, 110, 60))
    for x0 in (0, 512):
        d.text((x0 + 256, 30), "ROSSA", font=font("impact.ttf", 46), fill=(250, 240, 210), anchor="mm")
        for cx, cy, r in ((x0 + 190, 150, 62), (x0 + 300, 160, 54), (x0 + 250, 120, 48)):
            d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(200, 34, 26))
            d.ellipse((cx - r * 0.5, cy - r * 0.6, cx - r * 0.15, cy - r * 0.3), fill=(236, 120, 100))
            for a in range(5):
                t = a * 2 * math.pi / 5
                d.polygon([(cx, cy - r * 0.86), (cx + math.cos(t) * r * 0.36, cy - r * 0.86 + math.sin(t) * r * 0.2 - 4), (cx + math.cos(t + 0.5) * r * 0.12, cy - r * 0.8)], fill=(40, 120, 50))
        d.text((x0 + 256, ROW - 44), "peeled tomatoes", font=font("georgiai.ttf", 24), fill=(120, 30, 24), anchor="mm")
        small_print(d, x0 + 400, 80, 80, 12, rng, (120, 110, 90))
    barcode(d, 440, 190, 56, 34, rng)
    return img


def beans(rng):
    img = Image.new("RGB", (W, ROW), (30, 60, 110))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 96, W, 176), fill=(236, 200, 70))
    for x0 in (0, 512):
        d.text((x0 + 256, 136), "BAKED BEANS", font=font("impact.ttf", 52), fill=(30, 44, 80), anchor="mm")
        d.text((x0 + 256, 52), "Harbour", font=font("georgiai.ttf", 44), fill=(240, 236, 220), anchor="mm")
        for k in range(9):
            cx, cy = x0 + 150 + k * 26 + rng.uniform(-4, 4), 214 + rng.uniform(-10, 10)
            d.ellipse((cx - 12, cy - 8, cx + 12, cy + 8), fill=(200, 110, 50))
        small_print(d, x0 + 410, 190, 70, 5, rng, (190, 200, 220))
    barcode(d, 30, 190, 56, 36, rng)
    return img


def pills(rng):
    img = Image.new("RGB", (512, ROW), (240, 244, 248))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, 512, 64), fill=(40, 110, 200))
    d.rectangle((0, 64, 512, 74), fill=(230, 90, 40))
    d.text((24, 32), "CALMEX", font=font("arialbd.ttf", 40), fill=(250, 250, 250), anchor="lm")
    d.text((488, 32), "500 mg", font=font("arial.ttf", 24), fill=(250, 250, 250), anchor="rm")
    d.text((24, 120), "paracetamol", font=font("arial.ttf", 30), fill=(40, 60, 100), anchor="lm")
    d.text((24, 160), "10 tablets", font=font("arial.ttf", 22), fill=(90, 100, 120), anchor="lm")
    for k in range(2):
        cx = 380 + k * 60
        d.ellipse((cx - 24, 116, cx + 24, 164), fill=(250, 250, 250), outline=(150, 170, 200), width=3)
        d.line((cx - 18, 140, cx + 18, 140), fill=(150, 170, 200), width=2)
    small_print(d, 24, 196, 300, 5, rng, (150, 160, 175))
    return img


def matches(rng):
    img = Image.new("RGB", (512, ROW), (236, 190, 50))
    d = ImageDraw.Draw(img)
    d.rectangle((10, 10, 502, ROW - 10), outline=(150, 30, 24), width=6)
    d.text((256, 62), "LIGHTHOUSE", font=font("impact.ttf", 44), fill=(150, 30, 24), anchor="mm")
    d.polygon([(236, 200), (246, 110), (266, 110), (276, 200)], fill=(240, 240, 232))
    d.rectangle((240, 96, 272, 112), fill=(150, 30, 24))
    d.polygon([(244, 96), (256, 78), (268, 96)], fill=(40, 40, 40))
    for a in (-0.5, 0.0, 0.5):
        d.line((256, 100, 256 + math.sin(a + 1.57) * 120, 100 - math.cos(a + 1.57) * 120), fill=(250, 244, 200), width=4)
        d.line((256, 100, 256 - math.sin(a + 1.57) * 120, 100 - math.cos(a + 1.57) * 120), fill=(250, 244, 200), width=4)
    d.text((256, 226), "safety matches", font=font("georgiai.ttf", 24), fill=(90, 40, 20), anchor="mm")
    return img


def main():
    rng = np.random.default_rng(31)
    sheet = Image.new("RGB", (W, W), (128, 128, 128))
    sheet.paste(coffee(rng), (0, 0))
    sheet.paste(tomatoes(rng), (0, ROW))
    sheet.paste(beans(rng), (0, 2 * ROW))
    sheet.paste(pills(rng), (0, 3 * ROW))
    sheet.paste(matches(rng), (512, 3 * ROW))
    sheet.save(OUT)
    print("wrote", os.path.abspath(OUT))


main()
