"""What is written on boards, and other surfaces that differ from piece to piece: whiteboards and flipcharts by the kind of house,
mirrors (clean, dirty, cracked), a projector's screen (whole, torn, burnt, stained), the globe's map.

Run:  python Tools/Art/generate_boards.py [boards mirror projector globe]
      → Assets/_Game/Art/Textures/Covers/boards.png (4 x 4 cells), mirror.png (3 x 1), projector.png (3 x 2, with alpha), globe.png

The cells' order is PropPictures' (Scripts/Level/PropPictures.cs). Everything written is invented. Deterministic.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "Covers")
FONTS = "C:/Windows/Fonts/"
K = 2
BLACK, BLUE, RED, GREEN = (26, 28, 34), (30, 60, 150), (190, 36, 30), (30, 120, 60)
HAND, HAND2, MONO = "segoepr.ttf", "Inkfree.ttf", "consola.ttf"


def font(name, size):
    return ImageFont.truetype(FONTS + name, max(6, int(size * K)))


class Board:
    """One board, drawn at its real proportions (w x h units), squeezed into its cell afterwards."""

    def __init__(self, w=512, h=320, bg=(238, 240, 240), seed=0):
        self.w, self.h = w, h
        self.img = Image.new("RGB", (w * K, h * K), bg)
        self.d = ImageDraw.Draw(self.img)
        self.r = np.random.default_rng(seed)

    def text(self, x, y, s, size=20, fill=BLACK, name=HAND, anchor="lm"):
        self.d.text((x * K, y * K), s, font=font(name, size), fill=fill, anchor=anchor)

    def line(self, pts, fill=BLACK, width=2.2):
        # a marker's line: a little unsteady
        out = []
        for i, (x, y) in enumerate(pts):
            j = 0 if i in (0, len(pts) - 1) else self.r.normal(0, 0.7)
            out.append(((x + j) * K, (y + self.r.normal(0, 0.5)) * K))
        self.d.line(out, fill=fill, width=int(width * K), joint="curve")

    def box(self, x0, y0, x1, y1, fill=BLACK, width=2.2, inside=None):
        if inside:
            self.d.rectangle([x0 * K, y0 * K, x1 * K, y1 * K], fill=inside)
        self.line([(x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0)], fill, width)

    def arrow(self, a, b, fill=BLACK):
        self.line([a, b], fill)
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        for t in (2.6, -2.6):
            self.line([b, (b[0] + 9 * math.cos(ang + t), b[1] + 9 * math.sin(ang + t))], fill)

    def note(self, x, y, s, colour, tilt=0.0, size=46):
        n = Image.new("RGBA", (size * K, size * K), colour + (255,))
        d = ImageDraw.Draw(n)
        d.rectangle([0, 0, size * K, 6 * K], fill=tuple(int(v * 0.9) for v in colour) + (255,))
        for i, part in enumerate(s.split("|")):
            d.text((size * K / 2, (16 + i * 12) * K), part, font=font(HAND2, 10), fill=BLACK, anchor="mm")
        n = n.rotate(tilt, expand=True, resample=Image.BICUBIC)
        self.img.paste(n, (int(x * K), int(y * K)), n)

    def smear(self, n=5, strength=0.5):
        """Wiped with a hand: the ink dragged about and mostly gone."""
        a = np.asarray(self.img, float)
        bg = np.array([238, 240, 240.0])
        ghost = np.asarray(self.img.filter(ImageFilter.GaussianBlur(7 * K)), float)
        a = bg + (ghost - bg) * strength
        img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
        d = ImageDraw.Draw(img, "RGBA")
        for _ in range(n):                                # the eraser's passes
            x, y = self.r.random() * self.w, self.r.random() * self.h
            dx, dy = (self.r.random() - 0.5) * 300, (self.r.random() - 0.5) * 90
            d.line([(x * K, y * K), ((x + dx) * K, (y + dy) * K)], fill=(238, 240, 240, 120), width=int(40 * K))
        self.img = img.filter(ImageFilter.GaussianBlur(1.2 * K))
        self.d = ImageDraw.Draw(self.img)

    def done(self, cw, ch):
        img = self.img.resize((cw, ch), Image.LANCZOS)
        r = np.random.default_rng(9)
        haze = np.asarray(Image.fromarray((r.random((ch // 8, cw // 8)) * 255).astype(np.uint8)).resize((cw, ch), Image.BICUBIC), float) / 255
        a = np.asarray(img, float) * (0.965 + 0.035 * haze[..., None])          # the film a wiped board keeps
        return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


# ------------------------------------------------------------------------------------------------- whiteboards (512 x 320)

def blank(seed):
    return Board(seed=seed)


def washed(seed):
    b = formulas(seed) if seed % 2 else diagram(seed)
    b.smear(6, 0.42 if seed % 2 else 0.3)
    return b


def formulas(seed):
    b = Board(seed=seed)
    rows = [("E = hν − φ", BLACK), ("∂ψ/∂t = −(i/ħ) Ĥψ", BLUE), ("ΔG = ΔH − TΔS", BLACK), ("σ = √(Σ(x−μ)² / N)", BLACK), ("k = A·e^(−Ea/RT)", RED), ("λ = h / p", BLUE),
            ("∮ B·dl = μ₀I", BLACK), ("pV = nRT", GREEN), ("F = −kx", BLACK), ("n₁ sin θ₁ = n₂ sin θ₂", BLUE)]
    order = b.r.permutation(len(rows))
    y = 34
    for k in order[:5]:
        s, c = rows[k]
        b.text(22 + b.r.random() * 40, y, s, 20, c, "cambria.ttc" if os.path.exists(FONTS + "cambria.ttc") else HAND)
        y += 50
    # a sketch: axes and a curve, a circled result
    b.arrow((330, 250), (330, 60))
    b.arrow((320, 240), (490, 240))
    b.line([(334 + i * 5, 236 - 150 * math.exp(-((i - 16) / 7.0) ** 2)) for i in range(30)], RED)
    b.d.ellipse([18 * K, (y - 76) * K, 250 * K, (y - 24) * K], outline=RED, width=2 * K)
    b.text(420, 44, "run 14 ?", 15, GREEN, HAND2)
    return b


def chart(seed):
    b = Board(seed=seed)
    b.text(20, 26, "SALES / WEEK", 22, BLACK)
    b.arrow((40, 280), (40, 60))
    b.arrow((30, 270), (330, 270))
    vals = [0.35, 0.5, 0.42, 0.66, 0.6, 0.82, 0.74, 0.95]
    for i, v in enumerate(vals):
        x = 56 + i * 33
        b.box(x, 270 - v * 190, x + 22, 270, BLUE, 2, inside=(170, 190, 230))
    b.line([(67 + i * 33, 262 - v * 190) for i, v in enumerate(vals)], RED, 2.6)
    b.text(360, 80, "target 120", 20, RED)
    b.text(360, 124, "now  96", 20, BLACK)
    b.line([(356, 146), (480, 146)], BLACK)
    b.text(360, 176, "+18 %", 34, GREEN)
    b.text(360, 232, "call back: 7", 17, BLUE, HAND2)
    return b


def table(seed):
    b = Board(seed=seed)
    b.text(20, 26, "THIS MONTH", 22, BLACK)
    cols, rows = [20, 170, 270, 380, 492], [50, 92, 134, 176, 218, 260, 302]
    for x in cols:
        b.line([(x, rows[0]), (x, rows[-1])])
    for y in rows:
        b.line([(cols[0], y), (cols[-1], y)])
    for i, h in enumerate(("who", "plan", "done", "")):
        b.text(cols[i] + 8, 72, h, 17, BLUE)
    for j, (n, p, dn) in enumerate((("Anna", 40, 44), ("Boris", 40, 31), ("Chen", 35, 35), ("Dana", 30, 12), ("Emil", 30, 29))):
        y = 114 + j * 42
        b.text(cols[0] + 8, y, n, 18)
        b.text(cols[1] + 20, y, str(p), 18)
        b.text(cols[2] + 20, y, str(dn), 18, GREEN if dn >= p else RED)
        b.text(cols[3] + 30, y, "★" if dn >= p else "!", 22, GREEN if dn >= p else RED, "seguisym.ttf" if os.path.exists(FONTS + "seguisym.ttf") else HAND)
    return b


def diagram(seed):
    b = Board(seed=seed)
    nodes = {"client": (30, 40, 130, 84), "gateway": (200, 40, 320, 84), "auth": (390, 30, 480, 70), "queue": (200, 140, 320, 184), "workers": (380, 140, 490, 184),
             "db": (200, 240, 320, 284), "cache": (40, 150, 130, 190)}
    for name, (x0, y0, x1, y1) in nodes.items():
        b.box(x0, y0, x1, y1, BLUE if name in ("db", "cache") else BLACK)
        b.text((x0 + x1) / 2, (y0 + y1) / 2, name, 17, BLACK, HAND, "mm")
    for a, c in (("client", "gateway"), ("gateway", "auth"), ("gateway", "queue"), ("queue", "workers"), ("queue", "db"), ("gateway", "cache")):
        ax0, ay0, ax1, ay1 = nodes[a]
        cx0, cy0, cx1, cy1 = nodes[c]
        pa, pc = ((ax0 + ax1) / 2, (ay0 + ay1) / 2), ((cx0 + cx1) / 2, (cy0 + cy1) / 2)
        t0, t1 = 0.34, 0.68
        b.arrow((pa[0] + (pc[0] - pa[0]) * t0, pa[1] + (pc[1] - pa[1]) * t0), (pa[0] + (pc[0] - pa[0]) * t1, pa[1] + (pc[1] - pa[1]) * t1), RED if c == "cache" else BLACK)
    b.text(350, 250, "retry x3 !!", 18, RED)
    b.text(30, 260, "who owns this?", 16, GREEN, HAND2)
    return b


def code(seed):
    b = Board(seed=seed)
    lines = [("def route(job):", BLACK), ("    if job.stale():", BLACK), ("        return drop(job)   # ?", RED), ("    for w in workers:", BLACK), ("        if w.free:", BLACK),
             ("            return w.take(job)", BLUE), ("    queue.push(job)", BLACK)]
    for i, (s, c) in enumerate(lines):
        b.text(20, 34 + i * 30, s, 17, c, MONO)
    b.box(14, 232, 300, 300, GREEN)
    b.text(24, 252, "O(n) per job →", 17, GREEN)
    b.text(24, 280, "keep a free list", 17, GREEN)
    b.text(340, 60, "TODO", 22, RED)
    for i, s in enumerate(("tests", "timeouts", "ask Dana", "coffee")):
        b.box(340, 86 + i * 34, 356, 102 + i * 34)
        b.text(366, 94 + i * 34, s, 17)
    b.line([(342, 92), (348, 100), (358, 82)], GREEN, 3)
    return b


def kanban(seed):
    b = Board(seed=seed)
    for i, t in enumerate(("TO DO", "DOING", "DONE")):
        b.text(30 + i * 170, 24, t, 19, BLACK)
        if i:
            b.line([(i * 170 + 4, 10), (i * 170 + 4, 310)])
    b.line([(8, 44), (504, 44)])
    cols = [(250, 226, 110), (250, 170, 190), (160, 216, 250), (180, 236, 160), (250, 190, 120)]
    notes = [(14, 56, "fix login", 0), (74, 64, "dark|mode", 1), (20, 120, "export|CSV", 2), (84, 132, "tests!!", 4), (30, 190, "docs", 3), (96, 206, "bug 412", 1), (26, 252, "refactor", 0),
             (186, 60, "API v2", 2), (244, 76, "search", 3), (196, 150, "deploy|script", 4),
             (356, 56, "signup", 3), (416, 70, "icons", 0), (360, 128, "bug 377", 1), (430, 146, "CI", 2), (372, 200, "logo", 4), (440, 220, "bug 380", 3), (366, 262, "v1.3", 0)]
    for k, (x, y, s, c) in enumerate(notes):
        b.note(x, y, s, cols[c], ((k * 37) % 11) - 5)
    return b


def memes(seed):
    b = Board(seed=seed)
    # a stick figure and a burning server, the team's wisdom round them
    b.d.ellipse([60 * K, 60 * K, 100 * K, 100 * K], outline=BLACK, width=3 * K)
    b.line([(80, 100), (80, 180)], BLACK, 3)
    b.line([(80, 120), (40, 150)], BLACK, 3)
    b.line([(80, 120), (130, 104)], BLACK, 3)
    b.line([(80, 180), (54, 240)], BLACK, 3)
    b.line([(80, 180), (108, 240)], BLACK, 3)
    b.line([(70, 76), (74, 76)], BLACK, 3)
    b.line([(86, 76), (90, 76)], BLACK, 3)
    b.line([(70, 88), (80, 92), (90, 88)], BLACK, 2)
    b.box(150, 70, 230, 190)
    for i in range(4):
        b.line([(158, 90 + i * 26), (222, 90 + i * 26)])
    for i in range(5):
        x = 156 + i * 16
        b.line([(x, 70), (x + 6, 46 - (i * 7) % 16), (x + 12, 70)], RED, 3)
    b.text(34, 28, "it works on my machine", 19, BLUE)
    b.text(150, 222, "\"this is fine\"  — prod, 03:00", 15, BLACK, HAND2)
    b.text(280, 70, "DAYS WITHOUT", 18, BLACK)
    b.text(280, 96, "AN INCIDENT:", 18, BLACK)
    b.box(330, 116, 400, 180, RED, 3)
    b.text(365, 148, "0", 44, RED, HAND, "mm")
    b.note(430, 30, "do NOT|erase", (250, 226, 110), 6)
    b.note(420, 200, "pizza|friday?", (250, 170, 190), -5)
    b.note(300, 216, "ship it", (180, 236, 160), 4)
    return b


def rota(seed):
    b = Board(seed=seed)
    b.text(20, 24, "WARD 3 — NIGHT", 21, BLACK)
    cols, rows = [20, 120, 240, 360, 492], [44, 86, 128, 170, 212, 254, 296]
    for x in cols:
        b.line([(x, rows[0]), (x, rows[-1])])
    for y in rows:
        b.line([(cols[0], y), (cols[-1], y)])
    for i, h in enumerate(("bed", "name", "doctor", "note")):
        b.text(cols[i] + 6, 66, h, 16, BLUE)
    for j, (n, dr, note, c) in enumerate((("Ortiz", "Lind", "fasting", RED), ("—", "", "", BLACK), ("Varga", "Okoye", "obs 2h", BLACK), ("Sato", "Lind", "home tue", GREEN), ("Ivers", "Okoye", "X-ray", BLUE))):
        y = 108 + j * 42
        b.text(cols[0] + 10, y, str(j + 1), 18)
        b.text(cols[1] + 8, y, n, 17)
        b.text(cols[2] + 8, y, dr, 17)
        b.text(cols[3] + 8, y, note, 16, c)
    return b


# ------------------------------------------------------------------------------------------------- flipcharts (300 x 420, squeezed)

def flip_list(seed):
    b = Board(300, 420, (244, 242, 232), seed)
    b.text(24, 34, "Q3 goals", 28, BLUE)
    b.line([(20, 58), (200, 58)], BLUE, 3)
    for i, s in enumerate(("1. fewer meetings", "2. ship the thing", "3. hire two", "4. fix the lift", "5. ???")):
        b.text(24, 100 + i * 56, s, 22, RED if i == 4 else BLACK)
    b.line([(22, 150), (210, 160)], RED, 3)
    return b


def flip_chart(seed):
    b = Board(300, 420, (244, 242, 232), seed)
    b.text(24, 34, "where time goes", 22, BLACK)
    cx, cy, r = 150, 190, 100
    b.d.ellipse([(cx - r) * K, (cy - r) * K, (cx + r) * K, (cy + r) * K], outline=BLACK, width=3 * K)
    for a in (-90, 20, 150, 200):
        b.line([(cx, cy), (cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a)))])
    for (x, y, s, c) in ((176, 150, "mail", BLUE), (150, 250, "meetings", RED), (70, 200, "work", GREEN), (90, 140, "?", BLACK)):
        b.text(x, y, s, 18, c)
    b.text(24, 350, "→ less mail", 24, GREEN)
    return b


def flip_home(seed):
    b = Board(300, 420, (244, 242, 232), seed)
    b.text(24, 34, "to buy", 28, GREEN)
    for i, s in enumerate(("milk", "bulbs x4", "cat food", "tape", "batteries")):
        b.text(40, 90 + i * 50, s, 24, BLACK, HAND2)
        b.line([(16, 90 + i * 50), (28, 90 + i * 50)], BLACK, 3)
    b.line([(36, 92), (120, 88)], RED, 3)
    # a child's drawing in the corner: a house and a sun
    b.box(170, 310, 260, 390, BLUE, 3)
    b.line([(160, 310), (215, 260), (270, 310)], RED, 3)
    b.d.ellipse([40 * K, 320 * K, 90 * K, 370 * K], outline=(236, 180, 30), width=4 * K)
    return b


def boards():
    cw, ch = 512, 384
    cells = [blank(1), washed(2), washed(3), formulas(4), formulas(15), chart(5), table(6), diagram(7), code(8), kanban(9), memes(10), rota(11),
             flip_list(12), flip_chart(13), flip_home(14), Board(300, 420, (244, 242, 232), 16)]
    a = Image.new("RGB", (4 * cw, 4 * ch))
    for k, b in enumerate(cells):
        a.paste(b.done(cw, ch), (k % 4 * cw, k // 4 * ch))
    a.save(os.path.join(OUT, "boards.png"), optimize=True)


# ------------------------------------------------------------------------------------------------- mirrors

def noise(seed, w, h, scale):
    r = np.random.default_rng(seed)
    small = (r.random((max(2, h // scale), max(2, w // scale))) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(small).resize((w, h), Image.BICUBIC), float) / 255


def mirror():
    w, h = 512, 640
    y, x = np.mgrid[0:h, 0:w]
    base = 150 + 46 * (1 - y / h) + 26 * np.exp(-((x - y * 0.5 - 60) / 70.0) ** 2) + 16 * np.exp(-((x - y * 0.5 - 330) / 40.0) ** 2)
    silver = np.stack([base * 0.95, base * 1.0, base * 1.03], -1)
    a = Image.new("RGB", (3 * w, h))

    def put(k, arr):
        a.paste(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)), (k * w, 0))
    put(0, silver * (0.985 + 0.03 * noise(1, w, h, 90)[..., None]))
    # dirty: haze, runs of water marks, specks of paste, a wiped arc
    haze = noise(2, w, h, 60) * 0.5 + noise(3, w, h, 14) * 0.5
    d = silver * (0.74 + 0.22 * haze[..., None]) + 22
    img = Image.fromarray(np.clip(d, 0, 255).astype(np.uint8))
    dr = ImageDraw.Draw(img, "RGBA")
    r = np.random.default_rng(4)
    for _ in range(90):
        px, py, q = r.random() * w, h * (0.35 + 0.65 * r.random() ** 0.6), 1 + r.random() * 3.5
        dr.ellipse([px - q, py - q, px + q, py + q], fill=(236, 238, 232, 150))
    for _ in range(14):
        px, py, ln = r.random() * w, r.random() * h * 0.6, 60 + r.random() * 200
        dr.line([(px, py), (px + r.normal(0, 4), py + ln)], fill=(120, 124, 120, 70), width=2)
    dr.arc([60, 120, 460, 620], 200, 330, fill=(214, 224, 228, 110), width=46)
    put(1, np.asarray(img.filter(ImageFilter.GaussianBlur(0.8)), float))
    # cracked: lines from where it was hit, the shards a little off in tone, the silvering gone at the edges
    img = Image.fromarray(np.clip(silver * 0.93, 0, 255).astype(np.uint8))
    dr = ImageDraw.Draw(img, "RGBA")
    cx, cy = 330, 230
    r = np.random.default_rng(6)
    angles = sorted(r.random(11) * 2 * math.pi)
    ends = []
    for ang in angles:
        pts, px, py, aa = [(cx, cy)], cx, cy, ang
        while 0 <= px < w and 0 <= py < h:
            aa += r.normal(0, 0.16)
            step = 30 + r.random() * 50
            px, py = px + math.cos(aa) * step, py + math.sin(aa) * step
            pts.append((px, py))
        ends.append(pts)
    for i, pts in enumerate(ends):                       # shards between neighbouring cracks
        nxt = ends[(i + 1) % len(ends)]
        tone = int(r.normal(0, 16))
        dr.polygon(pts + nxt[::-1], fill=(128 + tone, 134 + tone, 138 + tone, 60))
    for pts in ends:
        dr.line(pts, fill=(20, 22, 24, 255), width=3)
        dr.line([(px + 2, py + 1) for px, py in pts], fill=(240, 244, 246, 200), width=1)
    for rad in (38, 84, 150):                            # rings round the hit
        for i in range(len(angles)):
            a0, a1 = angles[i], angles[(i + 1) % len(angles)]
            if r.random() < 0.65:
                dr.line([(cx + rad * math.cos(a0), cy + rad * math.sin(a0)), (cx + rad * 1.06 * math.cos(a1), cy + rad * 1.06 * math.sin(a1))], fill=(24, 26, 28, 230), width=2)
    dr.ellipse([cx - 9, cy - 9, cx + 9, cy + 9], fill=(16, 16, 18, 255))
    arr = np.asarray(img, float)
    edge = np.minimum(np.minimum(x, w - x), np.minimum(y, h - y)) / 40.0
    rot = np.clip(1.2 - edge - noise(8, w, h, 20) * 0.9, 0, 1)[..., None]
    put(2, arr * (1 - rot) + np.array([52, 46, 40.0]) * rot)
    a.save(os.path.join(OUT, "mirror.png"), optimize=True)


# ------------------------------------------------------------------------------------------------- the projector's screen

def projector():
    w, h = 512, 448
    a = Image.new("RGBA", (3 * w, 2 * h), (0, 0, 0, 0))

    def cloth(seed, stain=0.0):
        n = noise(seed, w, h, 70)
        y = np.mgrid[0:h, 0:w][0] / h
        v = 226 + 10 * n - 8 * y
        rgb = np.stack([v, v, v * 0.985], -1)
        if stain:
            s = np.clip((noise(seed + 1, w, h, 50) - 0.5) * 4, 0, 1) * np.clip((y - 0.25) * 2.2, 0, 1)
            ring = np.clip(1 - np.abs(s - 0.5) * 9, 0, 1)
            rgb = rgb * (1 - 0.18 * stain * s[..., None]) - stain * ring[..., None] * np.array([30, 44, 70.0])
        return np.clip(rgb, 0, 255)

    def cut(alpha, poly, fray=3.0, seed=0):
        m = Image.new("L", (w, h), 0)
        ImageDraw.Draw(m).polygon(poly, fill=255)
        hole = np.asarray(m.filter(ImageFilter.GaussianBlur(fray)), float) / 255 + (noise(seed, w, h, 4) - 0.5) * 0.5
        return alpha * (hole < 0.5)

    def put(k, rgb, alpha):
        edge = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3)), float) / 255
        rgb = rgb * (0.72 + 0.28 * edge[..., None])              # the torn edge a little darker
        img = np.dstack([rgb, alpha * 255]).astype(np.uint8)
        a.paste(Image.fromarray(img, "RGBA"), (k % 3 * w, k // 3 * h))

    full = np.ones((h, w))
    put(0, cloth(1), full)
    # a long slash, the flap fallen away under it
    al = cut(full, [(90, 60), (120, 54), (400, 300), (420, 448), (300, 448), (250, 300)], 2.5, 11)
    put(1, cloth(2), al)
    # holes with scorched rims
    rgb, al = cloth(3), full.copy()
    r = np.random.default_rng(5)
    for cx, cy, q in ((150, 160, 46), (350, 110, 30), (300, 300, 62), (110, 340, 24)):
        pts = [(cx + q * (0.8 + 0.4 * r.random()) * math.cos(t), cy + q * (0.8 + 0.4 * r.random()) * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 14, endpoint=False)]
        al = cut(al, pts, 2.0, 13)
        yy, xx = np.mgrid[0:h, 0:w]
        burn = np.clip(1.5 - np.hypot(xx - cx, yy - cy) / (q * 1.25), 0, 1)[..., None]
        rgb = rgb * (1 - burn) + np.array([40, 26, 16.0]) * burn
    put(2, rgb, al)
    # water-stained, a small tear at the edge
    put(3, cloth(4, 1.0), cut(full, [(512, 250), (440, 262), (470, 300), (512, 310)], 2.0, 17))
    # the lower right torn off
    put(4, cloth(6, 0.5), cut(full, [(512, 120), (360, 200), (300, 310), (180, 360), (150, 448), (512, 448)], 3.0, 19))
    a.save(os.path.join(OUT, "projector.png"), optimize=True)


# ------------------------------------------------------------------------------------------------- the globe

LAND = [
    [(-168, 66), (-156, 71), (-128, 70), (-110, 68), (-95, 72), (-82, 73), (-78, 62), (-64, 60), (-56, 52), (-66, 45), (-70, 42), (-76, 35), (-81, 31), (-80, 25), (-84, 30), (-90, 29),
     (-97, 26), (-97, 20), (-91, 18), (-88, 21), (-87, 16), (-83, 10), (-78, 8), (-80, 7), (-85, 10), (-95, 16), (-105, 20), (-110, 24), (-114, 31), (-117, 33), (-122, 37), (-124, 43),
     (-124, 48), (-130, 55), (-140, 60), (-150, 60), (-158, 57), (-165, 60)],
    [(-55, 60), (-44, 60), (-22, 70), (-20, 78), (-35, 83), (-60, 82), (-72, 78), (-58, 72)],
    [(-78, 8), (-72, 12), (-62, 10), (-52, 5), (-50, 0), (-36, -6), (-35, -10), (-39, -18), (-48, -26), (-54, -34), (-58, -38), (-65, -42), (-68, -52), (-72, -54), (-75, -48), (-73, -38),
     (-71, -28), (-70, -18), (-76, -14), (-81, -5), (-80, 2)],
    [(-9, 37), (-9, 43), (-2, 44), (-4, 48), (2, 51), (8, 54), (8, 57), (5, 59), (6, 62), (14, 68), (26, 71), (40, 67), (60, 69), (70, 73), (88, 75), (105, 78), (113, 74), (140, 73),
     (160, 70), (180, 69), (180, 65), (172, 62), (163, 60), (160, 53), (156, 51), (155, 58), (142, 59), (135, 54), (141, 48), (133, 43), (128, 40), (126, 35), (122, 39), (118, 38),
     (121, 31), (118, 24), (110, 21), (108, 16), (109, 11), (105, 9), (102, 12), (100, 8), (104, 1), (100, 4), (98, 9), (98, 16), (94, 18), (91, 22), (87, 21), (80, 15), (80, 9), (77, 8),
     (73, 17), (72, 21), (67, 24), (61, 25), (57, 26), (51, 28), (48, 30), (51, 24), (56, 25), (59, 22), (55, 17), (45, 13), (43, 13), (39, 21), (35, 28), (34, 31), (36, 36), (31, 37),
     (27, 37), (26, 40), (23, 40), (22, 37), (19, 40), (18, 42), (13, 45), (12, 44), (16, 41), (18, 40), (16, 38), (12, 42), (9, 44), (3, 43), (0, 39), (-2, 37), (-5, 36)],
    [(-6, 35), (10, 37), (11, 33), (20, 31), (25, 32), (32, 31), (34, 28), (37, 21), (43, 12), (51, 12), (48, 5), (40, -3), (39, -10), (36, -18), (35, -24), (31, -29), (26, -34),
     (19, -34), (15, -27), (12, -17), (13, -8), (9, -1), (9, 4), (5, 5), (-4, 5), (-8, 4), (-13, 8), (-17, 14), (-16, 20), (-13, 27), (-9, 32)],
    [(114, -22), (122, -17), (130, -12), (137, -12), (136, -15), (141, -17), (142, -11), (146, -19), (153, -26), (151, -34), (147, -38), (140, -38), (135, -34), (129, -32), (120, -34), (115, -34)],
    [(-5, 50), (1, 51), (0, 54), (-2, 58), (-6, 57), (-5, 54)], [(-10, 52), (-6, 52), (-6, 55), (-10, 54)],
    [(131, 32), (136, 34), (140, 36), (142, 40), (141, 44), (144, 44), (141, 41), (139, 38), (133, 35)],
    [(44, -25), (47, -25), (50, -15), (49, -12), (44, -17)], [(109, 1), (113, -3), (116, -4), (118, 1), (117, 6), (114, 5)], [(95, 5), (99, 1), (104, -4), (106, -6), (102, -4), (97, 1)],
    [(131, -1), (137, -2), (144, -4), (150, -10), (142, -9), (138, -8), (132, -4)], [(173, -35), (178, -38), (175, -41), (173, -40)], [(167, -46), (171, -44), (174, -41), (172, -44), (168, -47)],
    [(-24, 65), (-14, 66), (-14, 64), (-22, 63)], [(-84, 22), (-80, 23), (-74, 20), (-78, 20)], [(120, 18), (122, 17), (124, 12), (126, 7), (122, 8), (120, 14)], [(80, 9), (82, 7), (80, 6)],
    [(106, -6), (114, -7), (114, -8), (106, -7)], [(-74, 19), (-68, 19), (-69, 18), (-74, 18)],
]


def globe():
    w, h = 2048, 1024
    def p(lon, lat):
        return ((lon + 180) / 360 * w, (90 - lat) / 180 * h)
    mask, ice = Image.new("L", (w, h), 0), Image.new("L", (w, h), 0)
    for poly in LAND:
        ImageDraw.Draw(mask).polygon([p(*q) for q in poly], fill=255)
    ImageDraw.Draw(ice).polygon([p(-180, -72), p(-120, -74), p(-60, -66), p(-40, -78), p(0, -70), p(60, -67), p(120, -66), p(170, -72), p(180, -72), p(180, -90), p(-180, -90)], fill=255)
    land = (np.asarray(mask.filter(ImageFilter.GaussianBlur(2.5)), float) / 255 + (noise(2, w, h, 6) - 0.5) * 0.5 > 0.5)[..., None]
    cold = (np.asarray(ice.filter(ImageFilter.GaussianBlur(3)), float) / 255 > 0.5)[..., None]
    hills, lat = noise(3, w, h, 30)[..., None], np.abs(np.mgrid[0:h, 0:w][0] / h - 0.5)[..., None] * 2
    soil = np.array([206, 186, 132.0]) * (1 - hills * 0.5) + np.array([136, 158, 100.0]) * hills * 0.5 + np.array([70, 70, 60.0]) * np.clip(lat - 0.6, 0, 1)
    sea = np.array([150, 178, 176.0]) * (0.94 + 0.08 * noise(4, w, h, 80)[..., None])
    arr = np.where(cold, np.array([232, 232, 224.0]), np.where(land, soil, sea))
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    coast = img.filter(ImageFilter.FIND_EDGES).convert("L").point(lambda v: 255 if v > 40 else 0).filter(ImageFilter.GaussianBlur(0.8))
    img.paste((92, 80, 56), mask=coast)
    d = ImageDraw.Draw(img, "RGBA")
    for lon in range(-180, 181, 30):
        d.line([p(lon, 90), p(lon, -90)], fill=(60, 70, 80, 90), width=2)
    for lat in range(-60, 61, 30):
        d.line([p(-180, lat), p(180, lat)], fill=(60, 70, 80, 150 if lat == 0 else 90), width=3 if lat == 0 else 2)
    for lat in (23.4, -23.4):
        for x in range(0, w, 24):
            d.line([(x, p(0, lat)[1]), (x + 12, p(0, lat)[1])], fill=(60, 70, 80, 110), width=2)
    f = ImageFont.truetype(FONTS + "georgiai.ttf", 30)
    for s, lon, lat in (("ATLANTIC", -42, 28), ("PACIFIC", -150, 8), ("PACIFIC", 158, 22), ("INDIAN", 76, -22), ("ARCTIC", 0, 84)):
        d.text(p(lon, lat), s, font=f, fill=(70, 96, 110, 220), anchor="mm")
    img.save(os.path.join(OUT, "globe.png"), optimize=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for f in (boards, mirror, projector, globe):
        if len(sys.argv) < 2 or f.__name__ in sys.argv[1:]:
            f()
            print("wrote", f.__name__)
