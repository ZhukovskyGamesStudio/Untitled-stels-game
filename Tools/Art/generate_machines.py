"""Pictures of machines: an arcade game, a vending machine and what it sells, a jukebox, keyboards, the small displays of
appliances and instruments, a warning sign.

Run:  python Tools/Art/generate_machines.py      (writes Assets/_Game/Art/Textures/Covers/machines.png, machine_glow.png,
      keyboard.png, displays.png; where each picture lies: Tools/Blender/machine_atlas.py)

Every brand and game is invented. Deterministic.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Blender"))
import machine_atlas as at  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "Covers")
FONTS = "C:/Windows/Fonts/"
K = 2                                                    # everything is drawn twice as large


def font(name, size):
    return ImageFont.truetype(FONTS + name, max(6, int(size * K)))


class Cell:
    """A cell drawn in its own pixels (the size given), pasted into its atlas."""

    def __init__(self, w, h, c=(0, 0, 0)):
        self.w, self.h = w, h
        self.img = Image.new("RGB", (int(w * K), int(h * K)), c)
        self.d = ImageDraw.Draw(self.img)

    def rect(self, x0, y0, x1, y1, fill=None, outline=None, width=1, r=0):
        box = [x0 * K, y0 * K, x1 * K, y1 * K]
        if r:
            self.d.rounded_rectangle(box, r * K, fill=fill, outline=outline, width=int(width * K))
        else:
            self.d.rectangle(box, fill=fill, outline=outline, width=int(width * K))

    def oval(self, x0, y0, x1, y1, fill=None, outline=None, width=1):
        self.d.ellipse([x0 * K, y0 * K, x1 * K, y1 * K], fill=fill, outline=outline, width=int(width * K))

    def poly(self, pts, fill=None):
        self.d.polygon([(x * K, y * K) for x, y in pts], fill=fill)

    def line(self, pts, fill, width=1):
        self.d.line([(x * K, y * K) for x, y in pts], fill=fill, width=max(1, int(width * K)), joint="curve")

    def text(self, x, y, s, name, size, fill, anchor="mm"):
        self.d.text((x * K, y * K), s, font=font(name, size), fill=fill, anchor=anchor)

    def fit(self, x, y, s, name, width, top, fill, anchor="mm"):
        size = 5
        while size < top and self.d.textlength(s, font=font(name, size + 1)) < width * K:
            size += 1
        self.text(x, y, s, name, size, fill, anchor)

    def grad(self, a, b, y0=0, y1=None):
        y1 = self.h if y1 is None else y1
        for y in range(int(y0 * K), int(y1 * K)):
            t = (y - y0 * K) / max(1, (y1 - y0) * K)
            self.d.line([(0, y), (self.w * K, y)], fill=tuple(int(p + (q - p) * t) for p, q in zip(a, b)))

    def into(self, atlas, box):
        atlas.paste(self.img.resize((box[2] - box[0], box[3] - box[1]), Image.LANCZOS), (box[0], box[1]))


def drone(c, x, y, s, fill):
    """One of the game's drones, 8 s wide: a split hexagon round a single eye, two fins."""
    cx, cy, dark = x + 4 * s, y + 4 * s, tuple(v // 4 for v in fill)
    c.poly([(cx - 3.4 * s, cy), (cx - 1.7 * s, cy - 2.6 * s), (cx + 1.7 * s, cy - 2.6 * s), (cx + 3.4 * s, cy), (cx + 1.7 * s, cy + 2.6 * s), (cx - 1.7 * s, cy + 2.6 * s)], fill=fill)
    c.poly([(cx - 4.4 * s, cy - 1.8 * s), (cx - 2.9 * s, cy - 0.4 * s), (cx - 4.4 * s, cy + 1.8 * s)], fill=fill)
    c.poly([(cx + 4.4 * s, cy - 1.8 * s), (cx + 2.9 * s, cy - 0.4 * s), (cx + 4.4 * s, cy + 1.8 * s)], fill=fill)
    c.rect(cx - 0.25 * s, cy - 2.6 * s, cx + 0.25 * s, cy + 2.6 * s, fill=dark)
    c.oval(cx - 1.3 * s, cy - 1.3 * s, cx + 1.3 * s, cy + 1.3 * s, fill=dark)
    c.oval(cx - 0.6 * s, cy - 0.6 * s, cx + 0.6 * s, cy + 0.6 * s, fill=(255, 255, 255))


def stars(c, seed, n, top=1.0):
    r = np.random.default_rng(seed)
    for _ in range(n):
        x, y, q = r.random() * c.w, r.random() * c.h * top, 0.4 + r.random() * 0.9
        c.oval(x - q, y - q, x + q, y + q, fill=(200, 210, 255))


# ------------------------------------------------------------------------------------------------- the arcade game "VOID RUNNER"

def ship(c, x, y, s, body=(240, 240, 250), wing=(230, 60, 60)):
    c.poly([(x, y - 3 * s), (x + s, y + s), (x, y + 0.4 * s), (x - s, y + s)], fill=body)
    c.poly([(x + 0.8 * s, y - 0.2 * s), (x + 2.2 * s, y + 1.4 * s), (x + 0.9 * s, y + s)], fill=wing)
    c.poly([(x - 0.8 * s, y - 0.2 * s), (x - 2.2 * s, y + 1.4 * s), (x - 0.9 * s, y + s)], fill=wing)
    c.poly([(x - 0.4 * s, y + s), (x + 0.4 * s, y + s), (x, y + 2.6 * s)], fill=(255, 200, 60))


def arcade_side():
    c = Cell(256, 512)
    c.grad((24, 8, 60), (96, 14, 92), 0, 330)
    c.grad((96, 14, 92), (230, 96, 40), 330, 512)
    stars(c, 1, 120, 0.6)
    c.oval(-60, 250, 316, 620, fill=(250, 170, 60))
    for k in range(7):                                   # a sun in bands
        c.rect(0, 360 + k * 22, 256, 366 + k * 22 + k * 1.6, fill=(150, 30, 80))
    for k in range(9):
        c.line([(128, 330), (-200 + k * 82, 540)], (255, 90, 200), 1.2)
    ship(c, 128, 190, 34)
    for k, (x, y, s) in enumerate(((30, 40, 5), (170, 60, 6), (96, 20, 4), (200, 130, 5))):
        drone(c, x, y, s, ((90, 240, 140), (250, 220, 80), (90, 200, 255), (255, 110, 200))[k])
    return c


def arcade_panel():
    c = Cell(256, 128, (18, 16, 26))
    for k in range(10):
        c.line([(k * 28, 128), (128, -40)], (90, 30, 120), 1)
    for k, (x, col) in enumerate(((70, (230, 60, 60)), (150, (250, 210, 60)), (182, (90, 200, 255)), (214, (90, 240, 140)))):
        c.oval(x - 15, 62 - 15, x + 15, 62 + 15, outline=col, width=2.5)
    c.text(70, 100, "MOVE", "arialbd.ttf", 13, (236, 236, 240))
    for x, s in ((150, "FIRE"), (182, "BOOST"), (214, "SHIELD")):
        c.text(x, 100, s, "arialbd.ttf", 8.5, (236, 236, 240))
    c.text(128, 16, "VOID RUNNER", "impact.ttf", 19, (255, 110, 200))
    return c


def arcade_front():
    c = Cell(256, 192, (20, 12, 40))
    c.grad((30, 12, 66), (12, 8, 26))
    stars(c, 4, 60)
    c.fit(128, 60, "VOID", "impact.ttf", 190, 62, (255, 220, 80))
    c.fit(128, 124, "RUNNER", "impact.ttf", 220, 62, (255, 90, 170))
    for k in range(5):
        drone(c, 30 + k * 42, 162, 3, (90, 200, 255))
    return c


def coin():
    c = Cell(128, 128, (34, 34, 36))
    c.rect(4, 4, 124, 124, outline=(120, 122, 124), width=3)
    for x in (40, 88):
        c.rect(x - 14, 22, x + 14, 60, fill=(150, 20, 18), outline=(200, 200, 200), width=1.5)
        c.rect(x - 2, 28, x + 2, 52, fill=(12, 12, 12))
        c.text(x, 70, "COIN", "arialbd.ttf", 8, (230, 230, 230))
        c.rect(x - 12, 84, x + 12, 108, fill=(14, 14, 14), outline=(130, 130, 130), width=1.5)
    c.oval(60, 60, 68, 68, fill=(190, 192, 196))
    return c


def bezel():
    c = Cell(128, 128, (14, 14, 20))
    c.text(64, 18, "HOW TO PLAY", "arialbd.ttf", 12, (255, 220, 80))
    for i, s in enumerate(("dodge the walls", "boost through gates", "three lights: shield", "one coin, one life")):
        c.text(64, 42 + i * 20, s, "arial.ttf", 10, (220, 222, 230))
    return c


def screen():
    c = Cell(512, 512)
    c.grad((6, 4, 26), (40, 8, 60))
    stars(c, 7, 220, 0.55)
    for k in range(13):                                  # the grid running to the horizon
        c.line([(256, 300), (-520 + k * 130, 512)], (200, 40, 170), 2)
    for k in range(8):
        y = 300 + 212 * (k / 7.0) ** 2
        c.line([(0, y), (512, y)], (200, 40, 170), 2)
    c.fit(256, 92, "VOID RUNNER", "impact.ttf", 440, 90, (255, 220, 80))
    ship(c, 256, 385, 24)
    for j in range(2):
        for k in range(6):
            drone(c, 82 + k * 60, 165 + j * 56, 5, ((90, 240, 140), (90, 200, 255))[j])
    c.text(256, 470, "INSERT COIN", "arialbd.ttf", 34, (255, 255, 255))
    c.text(24, 24, "1UP 000000", "consolab.ttf", 22, (255, 110, 110), "lm")
    c.text(488, 24, "HI 084200", "consolab.ttf", 22, (110, 220, 255), "rm")
    a = np.asarray(c.img, float)
    a[:: 2 * K] *= 0.72                                  # scan lines
    c.img = Image.fromarray(a.astype(np.uint8))
    return c


def marquee():
    c = Cell(512, 170)
    c.grad((90, 14, 110), (230, 70, 60))
    stars(c, 9, 60)
    c.fit(264, 92, "VOID RUNNER", "impact.ttf", 440, 110, (40, 6, 50))
    c.fit(256, 84, "VOID RUNNER", "impact.ttf", 440, 110, (255, 236, 130))
    return c


# ------------------------------------------------------------------------------------------------- vending

BRANDS = [("ZIRK", (200, 30, 30), (255, 250, 240)), ("Lumo", (250, 200, 40), (60, 30, 10)), ("AQVA", (40, 120, 200), (255, 255, 255)), ("Hop!", (60, 150, 60), (255, 246, 200)),
          ("KORA", (70, 30, 100), (255, 180, 70)), ("Brisk", (240, 110, 30), (255, 255, 255)), ("Nord", (230, 234, 238), (30, 60, 110)), ("Tako", (20, 20, 24), (110, 240, 160))]


def label(w, h, k, kind):
    name, bg, fg = BRANDS[(k * 3 + {"bottle": 0, "can": 1, "bag": 2, "bar": 5}[kind]) % 8]
    c = Cell(w * 2, h * 2, bg)
    W, H = w * 2, h * 2
    dark = tuple(int(v * 0.62) for v in bg)
    if kind == "bag":
        c.grad(bg, dark)
        c.oval(W * 0.14, H * 0.42, W * 0.86, H * 0.86, fill=tuple(min(255, int(v * 1.25 + 24)) for v in bg))
        r = np.random.default_rng(k)
        for _ in range(9):
            x, y = W * (0.3 + 0.4 * r.random()), H * (0.52 + 0.24 * r.random())
            c.oval(x - 9, y - 6, x + 9, y + 6, fill=(236, 190, 90), outline=(190, 130, 40), width=1)
        for x in range(0, int(W), 6):
            c.line([(x, 0), (x, H * 0.05)], dark, 1)
            c.line([(x, H * 0.95), (x, H)], dark, 1)
        c.fit(W / 2, H * 0.25, name, "impact.ttf", W * 0.8, 52, fg)
    elif kind == "bar":
        c.rect(0, 0, W * 0.07, H, fill=dark)
        c.rect(W * 0.93, 0, W, H, fill=dark)
        c.poly([(W * 0.1, H), (W * 0.35, 0), (W * 0.45, 0), (W * 0.2, H)], fill=tuple(min(255, v + 40) for v in bg))
        c.fit(W / 2, H * 0.5, name, "ariblk.ttf", W * 0.6, 34, fg)
    else:                                                # wraps a can or a bottle: the name in the middle of the front
        c.rect(0, 0, W, H * 0.1, fill=dark)
        c.rect(0, H * 0.9, W, H, fill=dark)
        c.poly([(0, H * 0.62), (W, H * 0.46), (W, H * 0.56), (0, H * 0.72)], fill=fg)
        c.fit(W / 2, H * 0.32, name, "ariblk.ttf", W * 0.78, 40, fg)
        for i in range(4):
            c.rect(W * 0.2, H * (0.74 + i * 0.035), W * (0.5 + 0.08 * (i % 3)), H * (0.755 + i * 0.035), fill=dark)
    return c


def tag(k):
    c = Cell(128, 64, (244, 242, 232))
    c.rect(0, 0, 40, 64, fill=(190, 30, 26))
    c.text(20, 32, f"{'ABCDE'[k % 5]}{k + 1}", "arialbd.ttf", 24, (255, 255, 255))
    c.text(84, 32, f"{0.8 + 0.35 * ((k * 3) % 8):.2f}", "arialbd.ttf", 30, (24, 24, 26))
    return c


def keypad():
    c = Cell(128, 160, (30, 31, 34))
    for j, row in enumerate(("A1 ", "B2 ", "C3 ", "D4 ", "E5 ")):
        for i in range(3):
            s = ("ABCDE"[j], str(j * 2 + 1), str((j * 2 + 2) % 10))[i]
            c.rect(10 + i * 38, 8 + j * 30, 40 + i * 38, 32 + j * 30, fill=(196, 198, 200) if i else (190, 60, 40), r=3)
            c.text(25 + i * 38, 20 + j * 30, s, "arialbd.ttf", 15, (24, 24, 26) if i else (255, 255, 255))
    return c


def slots():
    c = Cell(128, 160, (34, 35, 38))
    c.rect(14, 10, 114, 46, fill=(12, 12, 13), outline=(150, 152, 154), width=2, r=4)
    c.rect(26, 24, 102, 31, fill=(60, 220, 110))
    c.text(64, 56, "NOTES", "arialbd.ttf", 10, (230, 230, 230))
    c.rect(52, 70, 76, 112, fill=(150, 152, 154), r=3)
    c.rect(62, 76, 66, 106, fill=(8, 8, 8))
    c.text(64, 120, "COINS", "arialbd.ttf", 10, (230, 230, 230))
    c.rect(30, 132, 98, 154, fill=(12, 12, 13), outline=(150, 152, 154), width=2, r=3)
    return c


def header():
    c = Cell(512, 130)
    c.grad((250, 246, 236), (214, 226, 236))
    c.poly([(0, 130), (150, 0), (260, 0), (110, 130)], fill=(214, 40, 34))
    c.poly([(130, 130), (280, 0), (310, 0), (160, 130)], fill=(250, 200, 40))
    c.fit(350, 52, "SNACK & GO", "impact.ttf", 290, 62, (30, 40, 70))
    c.text(350, 102, "cold drinks  -  hot days  -  any time", "arial.ttf", 16, (40, 50, 80))
    return c


def display():
    c = Cell(256, 80, (4, 14, 10))
    c.text(128, 28, "SELECT ITEM", "consolab.ttf", 30, (110, 255, 170))
    c.text(128, 60, "CREDIT 0.00", "consola.ttf", 20, (70, 190, 120))
    return c


# ------------------------------------------------------------------------------------------------- jukebox, boxes, signs

SONGS = [("Midnight Lift", "The Landings"), ("Paper Moon Cafe", "Ida Rowe"), ("Slow Signal", "Nine Lamps"), ("Concrete Waltz", "The Landings"), ("Hold the Door", "Mira Vale"),
         ("Last Bus North", "Otto & Sons"), ("Blue Filament", "Ida Rowe"), ("Walk Like Rain", "Cordoba Five"), ("No Reply", "Nine Lamps"), ("Copper Sun", "Mira Vale"),
         ("Echo Street", "Cordoba Five"), ("Goodnight, Guard", "Otto & Sons")]


def cards():
    c = Cell(512, 320, (60, 20, 24))
    for i, (a, b) in enumerate(SONGS):
        x, y = 10 + (i % 2) * 250, 8 + (i // 2) * 52
        c.rect(x, y, x + 240, y + 46, fill=(246, 238, 214), r=3)
        c.rect(x, y, x + 34, y + 46, fill=(190, 40, 34), r=3)
        c.text(x + 17, y + 23, f"{'ABCDEF'[i // 2]}{i % 2 + 1}", "arialbd.ttf", 18, (255, 255, 255))
        c.text(x + 44, y + 15, a, "georgiab.ttf", 16, (30, 26, 24), "lm")
        c.text(x + 44, y + 34, b, "georgiai.ttf", 13, (90, 60, 40), "lm")
    return c


def record():
    c = Cell(256, 256, (12, 12, 13))
    for r in range(126, 50, -2):
        v = 18 + (r * 7) % 12
        c.oval(128 - r, 128 - r, 128 + r, 128 + r, outline=(v, v, v + 1), width=1)
    c.oval(80, 80, 176, 176, fill=(200, 44, 36))
    c.text(128, 106, "ORBIT", "impact.ttf", 20, (255, 236, 200))
    c.text(128, 152, "45 RPM", "arial.ttf", 10, (255, 236, 200))
    c.oval(123, 123, 133, 133, fill=(10, 10, 10))
    return c


def danger():
    c = Cell(128, 128, (236, 190, 30))
    c.poly([(64, 8), (122, 112), (6, 112)], fill=(20, 20, 20))
    c.poly([(64, 24), (108, 104), (20, 104)], fill=(236, 190, 30))
    c.poly([(70, 38), (48, 74), (62, 74), (54, 98), (82, 62), (67, 62), (78, 38)], fill=(20, 20, 20))
    return c


def circuits():
    c = Cell(128, 128, (232, 230, 220))
    c.text(64, 12, "CIRCUITS", "arialbd.ttf", 13, (30, 30, 32))
    for i, s in enumerate(("1 lights N", "2 lights S", "3 sockets", "4 lift", "5 fans", "6 pumps", "7 spare")):
        c.text(10, 32 + i * 14, s, "consola.ttf", 11, (40, 42, 70), "lm")
        c.line([(8, 39 + i * 14), (120, 39 + i * 14)], (170, 170, 164), 0.6)
    return c


def boxlabel(k):
    c = Cell(128, 64, (238, 234, 222))
    c.rect(2, 2, 126, 62, outline=(60, 60, 60), width=1.5)
    c.text(12, 22, ("WINTER", "CABLES")[k], "arialbd.ttf", 22, (40, 44, 60), "lm")
    c.line([(12, 42), (112, 42)], (60, 90, 150), 1.5)
    c.line([(12, 52), (84, 52)], (60, 90, 150), 1.5)
    return c


def carton(k):
    c = Cell(128, 128, (176, 140, 96))
    c.rect(0, 54, 128, 72, fill=(150, 110, 60))                           # the tape
    c.rect(10, 12, 74, 46, fill=(240, 238, 228))
    c.text(14, 22, ("PARTS", "FRAGILE")[k], "arialbd.ttf", 12, (30, 30, 34), "lm")
    for i in range(3):
        c.rect(14, 31 + i * 5, 50 + (i * 7) % 18, 33 + i * 5, fill=(70, 70, 76))
    for x in (92, 108):                                                   # this side up
        c.poly([(x, 16), (x + 7, 28), (x + 2, 28), (x + 2, 44), (x - 2, 44), (x - 2, 28), (x - 7, 28)], fill=(60, 40, 24))
    c.text(64, 100, ("lot 0417 / 12 pcs", "handle with care")[k], "consola.ttf", 9, (70, 48, 28))
    return c


def paperbox():
    c = Cell(128, 128, (238, 238, 232))
    c.rect(0, 0, 128, 34, fill=(30, 90, 170))
    c.text(64, 17, "COPY PAPER", "arialbd.ttf", 14, (255, 255, 255))
    c.text(64, 62, "A4", "impact.ttf", 40, (30, 90, 170))
    c.text(64, 100, "80 g  5 x 500", "arial.ttf", 11, (40, 44, 60))
    return c


def ream():
    c = Cell(128, 64, (240, 240, 236))
    c.rect(0, 20, 128, 44, fill=(30, 90, 170))
    c.text(64, 32, "A4  500", "arialbd.ttf", 15, (255, 255, 255))
    return c


def toner(k):
    col = ((20, 150, 200), (200, 30, 120), (236, 200, 30), (24, 24, 26))[k]
    c = Cell(128, 96, (232, 232, 228))
    c.rect(0, 0, 128, 30, fill=(40, 42, 48))
    c.text(64, 15, "TONER", "arialbd.ttf", 15, (255, 255, 255))
    c.oval(44, 40, 84, 80, fill=col)
    c.text(64, 88, "CMYK"[k] + "-204", "consola.ttf", 9, (40, 42, 48))
    return c


def paint(k):
    col = ((196, 40, 34), (236, 190, 40), (40, 96, 170), (236, 236, 230), (40, 130, 80), (30, 30, 34), (220, 120, 40), (120, 124, 130))[k]
    c = Cell(128, 128, (226, 226, 222))
    c.rect(0, 0, 128, 26, fill=(40, 42, 48))
    c.text(64, 13, "MIXLINE", "arialbd.ttf", 13, (255, 255, 255))
    c.rect(26, 38, 102, 96, fill=col, outline=(40, 42, 48), width=2)
    for i in range(3):                                                    # a run of paint down the label
        c.rect(40 + i * 22, 96, 46 + i * 22, 104 + (i * 5) % 9, fill=col)
    c.text(64, 116, f"RAL {3020 + k * 517 % 6000}", "consola.ttf", 11, (40, 42, 48))
    return c


def binlabel(k):
    c = Cell(128, 64, (242, 242, 236))
    c.rect(1, 1, 127, 63, outline=(60, 60, 64), width=2)
    c.text(64, 22, ("M4x12", "M6 NUT", "R 10k", "C 100n", "FUSE 2A", "LED 5mm", "WASHER", "M3x8")[k], "arialbd.ttf", 17, (24, 24, 28))
    r = np.random.default_rng(40 + k)
    x = 12
    while x < 114:                                                        # a barcode
        w = 1 + int(r.random() * 3)
        c.rect(x, 38, x + w, 56, fill=(20, 20, 20))
        x += w + 1 + int(r.random() * 3)
    return c


def notes():
    c = Cell(128, 64, (150, 176, 140))
    c.rect(3, 3, 125, 61, outline=(60, 96, 62), width=2)
    c.oval(46, 12, 82, 52, outline=(60, 96, 62), width=2)
    c.text(64, 32, "50", "georgiab.ttf", 17, (40, 76, 46))
    for x in (18, 110):
        c.text(x, 14, "50", "georgiab.ttf", 9, (40, 76, 46))
        c.text(x, 50, "50", "georgiab.ttf", 9, (40, 76, 46))
    return c


def noteedge():
    c = Cell(64, 64, (196, 206, 184))
    r = np.random.default_rng(3)
    for y in range(0, 64):
        v = int(r.normal(0, 14))
        c.line([(0, y + 0.5), (64, y + 0.5)], (170 + v, 186 + v, 160 + v), 0.5)
    return c


def band():
    c = Cell(64, 64, (236, 226, 190))
    c.rect(0, 22, 64, 42, fill=(190, 60, 40))
    c.text(32, 32, "5000", "arialbd.ttf", 11, (255, 255, 255))
    return c


def bagmark():
    c = Cell(64, 64, (196, 184, 156))
    c.oval(10, 10, 54, 54, outline=(60, 50, 36), width=3)
    c.text(32, 31, "B", "georgiab.ttf", 26, (60, 50, 36))
    c.line([(20, 32), (44, 32)], (60, 50, 36), 2)
    return c


def grain(img, seed, amount=3.0):
    r = np.random.default_rng(seed)
    n = np.asarray(Image.fromarray((r.random((img.height, img.width)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)), float)
    n = (n - n.mean()) / n.std() * amount
    return Image.fromarray(np.clip(np.asarray(img, float) + n[..., None], 0, 255).astype(np.uint8))


def machines():
    a = Image.new("RGB", (at.SIZE, at.SIZE), (60, 60, 62))
    for name, f in (("arcade_side", arcade_side), ("arcade_panel", arcade_panel), ("arcade_front", arcade_front), ("coin", coin), ("bezel", bezel), ("keypad", keypad),
                    ("slots", slots), ("cards", cards), ("record", record), ("danger", danger), ("circuits", circuits),
                    ("boxlabel", lambda: boxlabel(0)), ("boxlabel2", lambda: boxlabel(1))):
        f().into(a, at.PRINT[name])
    for name, f in (("carton", lambda: carton(0)), ("carton2", lambda: carton(1)), ("paperbox", paperbox), ("ream", ream), ("notes", notes), ("noteedge", noteedge),
                    ("band", band), ("bagmark", bagmark)):
        f().into(a, at.PRINT[name])
    for k in range(8):
        paint(k).into(a, at.PRINT[f"paint{k}"])
        binlabel(k).into(a, at.PRINT[f"binlabel{k}"])
    for k in range(4):
        toner(k).into(a, at.PRINT[f"toner{k}"])
    for k in range(8):
        for kind, w, h in (("bottle", 64, 128), ("can", 64, 96), ("bag", 64, 96), ("bar", 64, 32)):
            label(w, h, k, kind).into(a, at.PRINT[f"{kind}{k}"])
        tag(k).into(a, at.PRINT[f"tag{k}"])
    d = ImageDraw.Draw(a)
    for n, c in at.SWATCHES.items():
        x0, y0, x1, y1 = at.PRINT[n]
        d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=c)
    grain(a, 3).save(os.path.join(OUT, "machines.png"), optimize=True)


def glow():
    a = Image.new("RGB", (at.SIZE, at.SIZE), (0, 0, 0))
    for name, f in (("screen", screen), ("marquee", marquee), ("header", header), ("display", display), ("cards", cards)):
        f().into(a, at.GLOW[name])
    d = ImageDraw.Draw(a)
    for n, c in (("lit", (236, 240, 244)), ("button", (255, 236, 190)), ("coinslot", (255, 90, 60))):
        x0, y0, x1, y1 = at.GLOW[n]
        d.rectangle([x0, y0, x1 - 1, y1 - 1], fill=c)
    x0, y0, x1, y1 = at.GLOW["tube"]                    # a jukebox's light tube: amber, rose, violet, with bubbles
    stops = [(0.0, (255, 190, 60)), (0.35, (255, 120, 50)), (0.6, (230, 50, 90)), (0.85, (130, 60, 220)), (1.0, (70, 110, 255))]
    for y in range(y0, y1):
        t = (y - y0) / (y1 - y0)
        for (ta, ca), (tb, cb) in zip(stops, stops[1:]):
            if ta <= t <= tb:
                q = (t - ta) / (tb - ta)
                d.line([(x0, y), (x1, y)], fill=tuple(int(p + (r - p) * q) for p, r in zip(ca, cb)))
    r = np.random.default_rng(5)
    for _ in range(70):
        x, y, q = x0 + 8 + r.random() * 112, y0 + r.random() * 510, 2 + r.random() * 5
        d.ellipse([x - q, y - q, x + q, y + q], fill=(255, 240, 214))
    a.save(os.path.join(OUT, "machine_glow.png"), optimize=True)


def keyboard():
    a = Image.new("RGB", (at.SIZE, at.SIZE), (40, 40, 42))
    P = at.KEY_PX
    for name, (ox, oy) in at.KEYBOARDS.items():
        case, cap, side, ink = {"black": ((20, 20, 22), (38, 39, 42), (26, 27, 29), (206, 208, 210)), "light": ((150, 150, 144), (214, 212, 204), (176, 174, 166), (46, 46, 50))}[name]
        c = Cell(at.KEYS_W * P, at.KEYS_H * P, case)
        for legend, x, y, w, h in at.keys():
            x0, y0, x1, y1 = x * P + 1, y * P + 1, (x + w) * P - 1, (y + h) * P - 1
            grey = name == "light" and (len(legend) > 1 or legend in "^<v>")
            top = (186, 184, 178) if grey else cap
            c.rect(x0, y0, x1, y1, fill=side, r=4)
            c.rect(x0 + 5, y0 + 3, x1 - 5, y1 - 8, fill=top, r=5)
            worn = legend in ("E", "A", "S", "Space", "", "Enter", "T", "O")
            if worn:
                c.rect(x0 + 11, y0 + 8, x1 - 11, y1 - 14, fill=tuple(min(255, v + 7) for v in top), r=6)
            if len(legend) == 1:
                c.text(x0 + 15, y0 + 15, legend, "arialbd.ttf", 17, ink)
            elif legend:
                c.text(x0 + 9, y0 + 13, legend, "arial.ttf", 11, ink, "lm")
        box = (ox, oy, ox + int(at.KEYS_W * P), oy + int(at.KEYS_H * P))
        c.into(a, box)
    d = ImageDraw.Draw(a)
    for n, col in (("black", (24, 24, 26)), ("light", (186, 184, 178)), ("wheel", (90, 92, 94)), ("cable", (14, 14, 15))):
        x, y = at.KEY_SWATCH[n]
        d.rectangle([x - 16, y - 16, x + 47, y + 47], fill=col)
    a.save(os.path.join(OUT, "keyboard.png"), optimize=True)


# ------------------------------------------------------------------------------------------------- small displays

def displays():
    W, H = 256, 160
    a = Image.new("RGB", (W * at.DISPLAY_COLS, H * at.DISPLAY_ROWS), (0, 0, 0))
    G, B, R, Y, WH = (110, 255, 170), (110, 190, 255), (255, 100, 80), (255, 210, 90), (230, 236, 240)

    def lcd(c, bg=(8, 20, 34)):
        c.rect(0, 0, W, H, fill=bg)
        c.rect(4, 4, W - 4, H - 4, outline=tuple(min(255, v + 26) for v in bg), width=2)

    def wave(c, y, amp, n, col, seed=0, sharp=False):
        r = np.random.default_rng(seed)
        pts = []
        for x in range(12, W - 12, 2):
            t = (x - 12) / (W - 24) * n
            v = math.sin(t * 2 * math.pi) if not sharp else (max(0, 1 - abs((t % 1) - 0.5) * 14) - 0.4 * max(0, 1 - abs((t % 1) - 0.62) * 16))
            pts.append((x, y - amp * v - r.normal(0, 0.4)))
        c.line(pts, col, 2)

    def draw(name):
        c = Cell(W, H)
        lcd(c)
        big, mid, mono = "bahnschrift.ttf", "arialbd.ttf", "consolab.ttf"
        if name == "synth":
            c.text(16, 34, "A-017", mono, 30, B, "lm")
            c.text(16, 76, "Glass Strings", mid, 30, WH, "lm")
            for k in range(12):
                c.rect(16 + k * 19, 140 - (8 + (k * 13) % 34), 30 + k * 19, 144, fill=B)
        elif name == "clock":
            c.text(128, 82, "03:47", big, 92, G)
        elif name == "program":
            c.text(128, 58, "COTTON 40", mid, 34, WH)
            c.text(128, 112, "1:12", big, 60, B)
        elif name == "printer":
            c.text(128, 44, "READY", mid, 40, G)
            c.text(128, 92, "TRAY 2: A4", mid, 28, WH)
            c.rect(30, 124, 226, 138, outline=WH, width=2)
            c.rect(32, 126, 150, 136, fill=B)
        elif name == "treadmill":
            c.text(64, 46, "0.0", big, 60, G)
            c.text(190, 46, "00:00", big, 46, WH)
            c.text(64, 92, "km/h", mid, 22, B)
            c.line([(16, 140), (60, 128), (100, 134), (150, 112), (200, 124), (240, 106)], R, 3)
        elif name == "score":
            c.text(72, 84, "07", big, 96, R)
            c.text(184, 84, "05", big, 96, R)
            c.text(128, 80, ":", big, 70, Y)
        elif name == "vitals":
            wave(c, 52, 34, 4, G, 1, True)
            wave(c, 104, 14, 5, B, 2)
            wave(c, 142, 8, 3, Y, 3)
            c.text(236, 24, "--", big, 30, G, "rm")
        elif name == "wave":
            for x in range(1, 8):
                c.line([(x * 32, 6), (x * 32, 154)], (30, 60, 60), 1)
            for y in range(1, 5):
                c.line([(6, y * 32), (250, y * 32)], (30, 60, 60), 1)
            wave(c, 80, 46, 3, G)
        elif name == "server":
            for k in range(5):
                c.text(14, 22 + k * 28, f"node{k + 1}", mono, 20, WH, "lm")
                c.rect(110, 14 + k * 28, 110 + 20 + (k * 37) % 110, 30 + k * 28, fill=G if k != 3 else R)
        elif name == "power":
            c.text(128, 46, "230 V", big, 50, G)
            c.text(128, 92, "LOAD 34 %", mid, 28, WH)
            c.rect(30, 118, 226, 140, outline=WH, width=2)
            c.rect(32, 120, 98, 138, fill=G)
        elif name == "temp":
            c.text(128, 66, "18.5°", big, 78, B)
            c.text(128, 130, "FAN 2   55 % RH", mid, 22, WH)
        elif name == "plain":
            c.rect(0, 0, W, H, fill=(84, 160, 235))
        elif name == "scan":
            c.oval(80, 22, 176, 138, outline=B, width=3)
            c.oval(100, 48, 156, 112, outline=B, width=2)
            c.text(20, 22, "SCAN", mid, 20, WH, "lm")
            c.text(20, 142, "STANDBY", mid, 20, Y, "lm")
        elif name == "turnstile":
            c.poly([(128, 24), (190, 92), (150, 92), (150, 138), (106, 138), (106, 92), (66, 92)], fill=R)
        elif name == "welder":
            c.text(128, 54, "120 A", big, 64, Y)
            c.text(128, 118, "MIG  0.8 mm", mid, 28, WH)
        elif name == "balance":
            c.text(64, 60, "15", big, 64, R)
            c.text(192, 60, "30", big, 64, R)
            c.text(128, 124, "g   INNER   OUTER   g", mid, 18, WH)
        elif name == "kiosk":
            c.text(128, 32, "WELCOME", mid, 32, WH)
            for k in range(3):
                c.rect(30, 56 + k * 32, 226, 82 + k * 32, fill=(30, 70, 120), r=6)
                c.text(128, 69 + k * 32, ("Floors", "People", "Exit")[k], mid, 18, WH)
        elif name == "game":                             # a console's paused game
            c.rect(0, 0, W, H, fill=(14, 10, 34))
            for k in range(9):
                c.line([(128, 70), (-140 + k * 67, 160)], (150, 40, 150), 1)
            ship(c, 128, 96, 9)
            c.text(128, 34, "PAUSED", big, 34, Y)
            for k, t in enumerate(("Resume", "Options", "Quit")):
                c.text(128, 116 + k * 15, t, mid, 13, WH if k else G)
        elif name == "rower":
            c.text(128, 50, "0 m", big, 60, G)
            c.text(128, 112, "00:00  /500", mid, 28, WH)
        elif name == "bars":
            for k in range(8):
                c.rect(20, 14 + k * 17, 40 + (k * 53) % 190, 26 + k * 17, fill=B)
        elif name == "paint":
            for k, col in enumerate(((200, 40, 40), (240, 200, 60), (60, 120, 200), (240, 240, 240))):
                c.oval(16 + k * 58, 30, 66 + k * 58, 80, fill=col)
            c.text(128, 116, "MIX 14-B", mid, 30, WH)
        elif name == "plot":
            c.text(128, 30, "ROLL 24 in", mid, 26, WH)
            c.line([(20, 140), (70, 70), (120, 110), (170, 56), (236, 90)], G, 3)
        elif name == "print3d":
            c.text(70, 40, "205°", big, 44, R)
            c.text(188, 40, "60°", big, 44, B)
            c.rect(20, 92, 236, 112, outline=WH, width=2)
            c.rect(22, 94, 158, 110, fill=Y)
            c.text(128, 136, "63 %", mid, 24, WH)
        else:
            c.text(128, 80, "OK", big, 90, G)
        return c

    for k, name in enumerate(at.DISPLAYS):
        x, y = k % at.DISPLAY_COLS * W, k // at.DISPLAY_COLS * H
        draw(name).into(a, (x, y, x + W, y + H))
    a.save(os.path.join(OUT, "displays.png"), optimize=True)


if __name__ == "__main__":
    for f in (machines, glow, keyboard, displays):
        if len(sys.argv) < 2 or f.__name__ in sys.argv[1:]:
            f()
            print("wrote", f.__name__)
