"""Printed things: the covers of magazines and books, the spines of the books on a shelf, the edges of their pages.

Run:  python Tools/Art/generate_covers.py      (writes Assets/_Game/Art/Textures/Covers/*.png)

printed.png     4 x 3 cells of 256 x 341 (cell 0 is top left): 0-5 magazine covers, 6 a magazine's back (an advertisement),
                7 a book's cloth front with its gilt title, 8 and 9 an open book's two pages, 10 plain book cloth, 11 paper
spines_<c>.png  one per book colour (AssetBuilder BookColours): eight spines side by side over the top three quarters, the
                cover's cloth below them
pages.png       the edges of a block of pages (lines along v); pages_flat.png the same lying down (lines along u)
The models' UVs: Tools/Blender/printed.py. Every title is invented. Deterministic.
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "Covers")
FONTS = "C:/Windows/Fonts/"
CW, CH = 256, 341
K = 3                                                    # drawn three times larger, then shrunk


def font(name, size):
    return ImageFont.truetype(FONTS + name, int(size * K))


def grain(img, seed, amount=5.0, blur=0.6):
    r = np.random.default_rng(seed)
    n = Image.fromarray((r.random((img.height, img.width)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(blur))
    n = np.asarray(n, float)
    n = (n - n.mean()) / n.std() * amount
    return Image.fromarray(np.clip(np.asarray(img, float) + n[..., None], 0, 255).astype(np.uint8))


def text(d, xy, s, f, fill, anchor="la"):
    d.text((xy[0] * K, xy[1] * K), s, font=f, fill=fill, anchor=anchor)


def rect(d, x0, y0, x1, y1, fill=None, outline=None, width=1, r=0):
    if r:
        d.rounded_rectangle([x0 * K, y0 * K, x1 * K, y1 * K], r * K, fill=fill, outline=outline, width=width * K)
    else:
        d.rectangle([x0 * K, y0 * K, x1 * K, y1 * K], fill=fill, outline=outline, width=width * K)


def oval(d, x0, y0, x1, y1, fill=None, outline=None, width=1):
    d.ellipse([x0 * K, y0 * K, x1 * K, y1 * K], fill=fill, outline=outline, width=width * K)


def poly(d, pts, fill=None, outline=None, width=1):
    d.polygon([(x * K, y * K) for x, y in pts], fill=fill, outline=outline, width=width * K)


def line(d, pts, fill, width=1):
    d.line([(x * K, y * K) for x, y in pts], fill=fill, width=width * K)


def fit(d, s, name, width, top):
    size = 8
    while size < top and d.textlength(s, font=font(name, size + 1)) < width * K:
        size += 1
    return font(name, size)


def gradient(a, b):
    y = np.linspace(0, 1, CH * K)[:, None, None]
    return Image.fromarray((np.array(a) * (1 - y) + np.array(b) * y).repeat(CW * K, 1).astype(np.uint8))


def barcode(d, x, y, w, h, seed):
    r = np.random.default_rng(seed)
    rect(d, x, y, x + w, y + h, fill=(245, 245, 243))
    cx = x + 3
    while cx < x + w - 3:
        b = 0.6 + r.random() * 1.6
        rect(d, cx, y + 3, cx + b, y + h - 7, fill=(20, 20, 20))
        cx += b + 0.6 + r.random() * 1.4


def lines(d, items, x, y, f1, f2, c1, c2, anchor="la", gap=30):
    for head, sub in items:
        text(d, (x, y), head, f1, c1, anchor)
        text(d, (x, y + f1.size / K * 1.02), sub, f2, c2, anchor)
        y += gap


def cars():
    img = gradient((214, 92, 40), (60, 22, 30))
    d = ImageDraw.Draw(img)
    oval(d, 40, 96, 216, 272, fill=(240, 160, 70))
    rect(d, 0, 224, CW, CH, fill=(32, 26, 34))
    poly(d, [(22, 232), (34, 206), (78, 200), (104, 176), (160, 174), (186, 200), (226, 208), (236, 232)], fill=(150, 20, 24))
    poly(d, [(86, 200), (108, 180), (130, 179), (130, 200)], fill=(40, 46, 60))
    poly(d, [(136, 200), (136, 179), (156, 179), (176, 200)], fill=(40, 46, 60))
    for x in (66, 194):
        oval(d, x - 19, 212, x + 19, 250, fill=(14, 14, 16))
        oval(d, x - 9, 222, x + 9, 240, fill=(170, 172, 176))
    text(d, (128, 8), "TORQUE", fit(d, "TORQUE", "impact.ttf", 236, 90), (255, 240, 214), "ma")
    lines(d, [("NIGHT DRIVE", "six coupés, one mountain road")], 14, 268, font("bahnschrift.ttf", 24), font("arial.ttf", 11),
          (255, 255, 255), (236, 200, 170))
    barcode(d, 196, 296, 48, 34, 1)
    return img


def homes():
    img = gradient((226, 214, 190), (236, 228, 212))
    d = ImageDraw.Draw(img)
    rect(d, 0, 250, CW, CH, fill=(168, 126, 86))
    rect(d, 46, 176, 212, 236, fill=(74, 106, 96), r=12)
    rect(d, 30, 196, 66, 262, fill=(60, 90, 82), r=10)
    rect(d, 192, 196, 228, 262, fill=(60, 90, 82), r=10)
    rect(d, 54, 216, 204, 258, fill=(88, 124, 112), r=6)
    line(d, [(222, 100), (222, 200)], (40, 40, 40), 2)
    poly(d, [(206, 100), (238, 100), (248, 132), (196, 132)], fill=(246, 208, 120))
    rect(d, 70, 96, 140, 152, fill=(250, 248, 240), outline=(60, 50, 40), width=3)
    oval(d, 84, 106, 126, 146, fill=(206, 96, 60))
    text(d, (128, 10), "interior", fit(d, "interior", "georgia.ttf", 220, 72), (50, 44, 40), "ma")
    lines(d, [("Small rooms, big calm", "40 ideas for a quiet flat")], 14, 286, font("georgiab.ttf", 17), font("georgia.ttf", 11),
          (255, 250, 240), (250, 236, 214))
    barcode(d, 204, 302, 42, 30, 2)
    return img


def space():
    img = gradient((8, 12, 34), (24, 30, 74))
    d = ImageDraw.Draw(img)
    r = np.random.default_rng(5)
    for _ in range(260):
        x, y, q = r.random() * CW, r.random() * CH, r.random() ** 3 * 1.6
        oval(d, x - q, y - q, x + q, y + q, fill=(220, 226, 255))
    oval(d, 70, 130, 250, 310, fill=(196, 150, 110))
    for i, c in enumerate(((214, 176, 136), (176, 128, 92), (222, 190, 150), (186, 140, 104))):
        half = 84 - abs(i - 1.2) * 14
        oval(d, 160 - half, 172 + i * 30, 160 + half, 182 + i * 30, fill=c)
    d.arc([24 * K, 186 * K, 296 * K, 256 * K], 350, 200, fill=(236, 214, 180), width=5 * K)
    text(d, (128, 8), "ORBIT", fit(d, "ORBIT", "bahnschrift.ttf", 232, 88), (240, 244, 255), "ma")
    lines(d, [("WHO IS LISTENING?", "machines that hear for us"), ("DEEP COLD", "a year under the ice")], 14, 100,
          font("arialbd.ttf", 14), font("arial.ttf", 10), (255, 210, 90), (214, 222, 250), gap=40)
    barcode(d, 14, 300, 46, 30, 3)
    return img


def food():
    img = gradient((250, 246, 236), (240, 226, 200))
    d = ImageDraw.Draw(img)
    oval(d, 34, 112, 222, 300, fill=(252, 252, 250), outline=(206, 200, 190), width=3)
    oval(d, 58, 136, 198, 276, fill=(206, 70, 36))
    r = np.random.default_rng(8)
    tints = [(70, 120, 48), (236, 196, 84), (150, 34, 24), (246, 232, 200)]
    for _ in range(26):
        a, q = r.random() * 6.28, r.random() ** 0.5 * 54
        x, y, s = 128 + np.cos(a) * q, 206 + np.sin(a) * q, 5 + r.random() * 9
        oval(d, x - s, y - s * 0.7, x + s, y + s * 0.7, fill=tints[int(r.random() * 4)])
    text(d, (128, 6), "Spoon", fit(d, "Spoon", "georgiaz.ttf", 210, 80), (176, 34, 30), "ma")
    lines(d, [("30 one-pot suppers", "nothing to wash but the spoon")], 128, 306, font("georgiab.ttf", 15), font("georgia.ttf", 9),
          (80, 30, 24), (110, 60, 44), "ma")
    return img


def gadgets():
    img = gradient((236, 238, 240), (206, 212, 218))
    d = ImageDraw.Draw(img)
    rect(d, 0, 0, CW, 84, fill=(210, 30, 44))
    rect(d, 66, 112, 190, 290, fill=(26, 28, 32), r=16)
    rect(d, 76, 128, 180, 262, fill=(60, 170, 190))
    poly(d, [(76, 262), (120, 190), (150, 230), (166, 206), (180, 226), (180, 262)], fill=(30, 90, 120))
    oval(d, 118, 268, 138, 286, outline=(120, 126, 134), width=2)
    text(d, (128, 4), "VOLT", fit(d, "VOLT", "impact.ttf", 200, 66), (255, 255, 255), "ma")
    lines(d, [("ROBOT", "that does"), ("YOUR", "shopping")], 10, 130, font("impact.ttf", 18), font("arial.ttf", 9), (24, 26, 30),
          (60, 64, 70), gap=44)
    lines(d, [("12", "phones"), ("0", "cables")], 246, 130, font("impact.ttf", 28), font("arial.ttf", 10), (210, 30, 44),
          (60, 64, 70), "ra", 56)
    barcode(d, 12, 302, 46, 30, 4)
    return img


def walks():
    img = gradient((120, 172, 196), (214, 226, 206))
    d = ImageDraw.Draw(img)
    hills = (((104, 140, 150), 196, 46), ((70, 112, 96), 228, 36), ((40, 78, 56), 262, 26), ((24, 50, 36), 300, 16))
    oval(d, 176, 96, 214, 134, fill=(252, 240, 206))
    for i, (c, base, amp) in enumerate(hills):
        top = [(x, base - amp * (np.sin(x * 0.03 + i * 1.7) * 0.6 + np.sin(x * 0.071 + i) * 0.4)) for x in range(0, CW + 4, 4)]
        poly(d, [(0, CH)] + top + [(CW, CH)], fill=c)
    text(d, (128, 10), "FIELD & FELL", fit(d, "FIELD & FELL", "timesbd.ttf", 232, 60), (255, 255, 255), "ma")
    lines(d, [("The long way home", "eleven walks without a map")], 14, 296, font("timesbd.ttf", 18), font("times.ttf", 12),
          (255, 255, 255), (214, 230, 214))
    barcode(d, 204, 300, 42, 30, 5)
    return img


def back():
    img = gradient((20, 34, 52), (10, 14, 24))
    d = ImageDraw.Draw(img)
    rect(d, 96, 70, 160, 220, fill=(150, 96, 40), r=12)
    rect(d, 116, 46, 140, 74, fill=(150, 96, 40))
    rect(d, 96, 120, 160, 180, fill=(238, 226, 196))
    text(d, (128, 138), "OAK", font("georgiab.ttf", 22), (60, 34, 16), "ma")
    text(d, (128, 236), "Time, bottled.", font("georgiai.ttf", 24), (236, 220, 186), "ma")
    text(d, (128, 300), "drink it slowly", font("arial.ttf", 10), (150, 150, 160), "ma")
    return img


def weave(color, seed, w, h, k=1.0):
    """Book cloth: a fine irregular weave of the colour."""
    r = np.random.default_rng(seed)
    a = Image.fromarray((r.random((h // 2, w)) * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR)
    b = Image.fromarray((r.random((h, w // 2)) * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR)
    n = (np.asarray(a, float) + np.asarray(b, float) - 255) / 255 * 0.16 * k
    return Image.fromarray(np.clip(np.array(color, float)[None, None] * (1 + n[..., None]), 0, 255).astype(np.uint8))


GOLD, SILVER, CREAM, DARK = (206, 164, 82), (190, 192, 196), (232, 222, 196), (34, 28, 26)
BOOK_RED = (112, 28, 26)


def book_front():
    img = weave(BOOK_RED, 31, CW * K, CH * K, 0.7)
    d = ImageDraw.Draw(img)
    rect(d, 12, 12, CW - 12, CH - 12, outline=GOLD, width=3)
    rect(d, 19, 19, CW - 19, CH - 19, outline=GOLD, width=1)
    text(d, (128, 84), "THE QUIET", font("georgiab.ttf", 30), GOLD, "ma")
    text(d, (128, 120), "ENGINE", font("georgiab.ttf", 40), GOLD, "ma")
    poly(d, [(128, 190), (146, 216), (128, 242), (110, 216)], outline=GOLD, width=2)
    oval(d, 121, 209, 135, 223, fill=GOLD)
    line(d, [(64, 216), (104, 216)], GOLD, 2)
    line(d, [(152, 216), (192, 216)], GOLD, 2)
    text(d, (128, 282), "A. N. WARREN", font("georgia.ttf", 17), GOLD, "ma")
    return img


def page(seed, head):
    img = grain(Image.new("RGB", (CW * K, CH * K), (236, 228, 208)), seed, 3)
    d = ImageDraw.Draw(img)
    r = np.random.default_rng(seed)
    y = 34
    if head:
        text(d, (128, 30), "IV", font("georgiab.ttf", 18), (40, 34, 30), "ma")
        y = 66
    while y < 306:
        short = r.random() < 0.14
        x, end = 28, 28 + (60 + r.random() * 90 if short else 196)
        if r.random() < 0.1:
            x += 12
        while x < end - 6:
            w = min(6 + r.random() * 18, end - x)
            v = int(r.random() * 30)
            rect(d, x, y, x + w, y + 3.2, fill=(70 + v, 62 + v, 56 + v))
            x += w + 4
        y += 9.5
    text(d, (128, 322), str(100 + seed), font("georgia.ttf", 9), (80, 70, 60), "ma")
    return img


def printed():
    atlas = Image.new("RGB", (CW * 4, CH * 3 + 1))
    cells = [cars(), homes(), space(), food(), gadgets(), walks(), back(), book_front(), page(41, False), page(42, True),
             weave(BOOK_RED, 33, CW * K, CH * K, 0.7), grain(Image.new("RGB", (CW * K, CH * K), (232, 224, 204)), 35, 3)]
    for i, c in enumerate(cells):
        atlas.paste(c.resize((CW, CH), Image.LANCZOS), ((i % 4) * CW, (i // 4) * CH))
    atlas.resize((1024, 1024), Image.LANCZOS).save(os.path.join(OUT, "printed.png"), optimize=True)


COLOURS = {"red": (140, 31, 26), "blue": (36, 61, 122), "green": (41, 92, 51), "ochre": (184, 140, 46), "brown": (97, 61, 36),
           "black": (26, 26, 28), "cream": (219, 209, 178), "teal": (31, 107, 112)}
TITLES = ["THE LONG CORRIDOR", "CIRCUITS", "A HISTORY OF LOCKS", "Salt & Iron", "WINTER GARDENS", "ELEMENTS OF MECHANICS", "Night Shift",
          "ATLAS OF QUIET PLACES", "PATIENCE", "The Glass Orchard", "SIGNALS", "Of Moths", "FIELD NOTES", "THE SECOND KEY", "Low Tide",
          "ARCHITECTURE", "Copper", "TWELVE DOORS", "HARBOUR", "Machines & Men", "THE CELLAR", "Echo", "PRIME", "Letters Home"]
AUTHORS = ["WARREN", "M. LIND", "OKAFOR", "Hale", "J. BRANDT", "SOREN", "VALE", "Ito", "K. MARSH", "DUNNE", "ROWE", "Castell"]
FACES = ["georgiab.ttf", "timesbd.ttf", "bahnschrift.ttf", "arialbd.ttf", "georgia.ttf"]
BANDS = [(196, 76, 40), (226, 206, 130), (60, 110, 130), (236, 232, 222)]


def spine(seed, base, w, h):
    """One spine upright: a label panel or bands, the title reading downwards, the author."""
    r = np.random.default_rng(seed)
    img = weave(base, seed, w * K, h * K)
    d = ImageDraw.Draw(img)
    light = sum(base) > 380
    ink = [DARK, (120, 30, 26), (40, 60, 90)][int(r.random() * 3)] if light else [GOLD, GOLD, CREAM, SILVER][int(r.random() * 4)]
    kind = int(r.random() * 4)
    if kind == 0:                                         # a leather label with bands
        rect(d, 0, h * 0.14, w, h * 0.62, fill=(120, 34, 30) if light else tuple(int(c * 0.45) for c in base))
        ink = GOLD
        for y in (0.06, 0.12, 0.64, 0.9, 0.95):
            rect(d, 0, h * y, w, h * y + 3, fill=GOLD)
    elif kind == 1:                                       # double rules
        for y in (0.05, 0.07, 0.78, 0.8, 0.94):
            rect(d, 0, h * y, w, h * y + 2, fill=ink)
    elif kind == 2:                                       # a paperback's bands of another colour
        other = BANDS[int(r.random() * 4)]
        rect(d, 0, 0, w, h * 0.2, fill=other)
        rect(d, 0, h * 0.8, w, h, fill=other)
    else:
        rect(d, 0, h * 0.9, w, h * 0.9 + 4, fill=ink)
    title = TITLES[int(r.random() * len(TITLES))]
    strip = Image.new("L", (int(h * 0.46 * K), w * K), 0)
    sd = ImageDraw.Draw(strip)
    f = fit(sd, title, FACES[int(r.random() * 5)], h * 0.43, w * 0.56)
    sd.text((strip.width / 2, strip.height / 2), title, font=f, fill=255, anchor="mm")
    mask = strip.rotate(-90, expand=True)
    img.paste(Image.new("RGB", mask.size, ink), (0, int(h * (0.15 if kind == 0 else 0.26) * K)), mask)
    who = AUTHORS[int(r.random() * len(AUTHORS))]
    if kind == 2:
        ink = DARK
    d.text((w * K / 2, h * (0.74 if kind == 0 else 0.86) * K), who, font=fit(d, who, "arialbd.ttf", w * 0.84, 22), fill=ink, anchor="mm")
    img = img.resize((w, h), Image.LANCZOS)
    k = 1 - 0.28 * np.abs(np.linspace(-1, 1, w)) ** 3     # the round of the spine, baked gently
    return Image.fromarray(np.clip(np.asarray(img, float) * k[None, :, None], 0, 255).astype(np.uint8))


def spines():
    for c, (name, base) in enumerate(COLOURS.items()):
        atlas = weave(base, 900 + c, 1024, 1024)
        for i in range(8):
            atlas.paste(spine(1000 + c * 10 + i, base, 128, 768), (i * 128, 0))
        atlas.save(os.path.join(OUT, f"spines_{name}.png"), optimize=True)


def pages():
    r = np.random.default_rng(77)
    a = np.clip(226 + r.normal(0, 1, 256) * 9 - 26 * (r.random(256) < 0.06), 0, 255)
    img = np.stack([a, a * 0.975, a * 0.9], -1)[None].repeat(64, 0) + r.normal(0, 2, (64, 256, 1))
    img = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).resize((256, 256))
    img.save(os.path.join(OUT, "pages.png"), optimize=True)
    img.rotate(90).save(os.path.join(OUT, "pages_flat.png"), optimize=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    printed()
    spines()
    pages()
    print("DONE")
