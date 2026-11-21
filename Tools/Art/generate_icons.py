"""HUD icons: the reticle's interaction glyphs, robot and device markers (robot_<kind>), the noise speaker, the visibility eye, hearts, key caps and mouse buttons.

    python Tools/Art/generate_icons.py                  # everything
    python Tools/Art/generate_icons.py --only robot_    # only the icons whose name starts with the prefix

Writes Assets/_Game/Art/Sprites/Icons/*.png (deterministic). A glyph is white with a dark rim so it reads on any
background; Unity tints the white part (the Image colour) and the rim stays dark. Drawn at 4x and downsampled.
"""
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Game", "Art", "Sprites", "Icons")
SS = 4  # supersampling


class Glyph:
    """A white mask (the glyph) and a dark mask (holes and details drawn dark), both 'L' at size * SS."""

    def __init__(self, size):
        self.size = size
        self.n = size * SS
        self.white = Image.new("L", (self.n, self.n), 0)
        self.dark = Image.new("L", (self.n, self.n), 0)
        self.w = ImageDraw.Draw(self.white)
        self.d = ImageDraw.Draw(self.dark)

    def p(self, x, y):
        """Unit coordinates (0..1, y down) to pixels."""
        return (x * self.n, y * self.n)

    def box(self, x0, y0, x1, y1):
        return [x0 * self.n, y0 * self.n, x1 * self.n, y1 * self.n]

    def px(self, v):
        return v * self.n

    # --- drawing helpers (target: 'w' white or 'd' dark)
    def ellipse(self, b, fill=True, width=0.0, on="w"):
        dr = self.w if on == "w" else self.d
        if fill:
            dr.ellipse(self.box(*b), fill=255)
        else:
            dr.ellipse(self.box(*b), outline=255, width=max(1, int(self.px(width))))

    def rrect(self, b, r, fill=True, width=0.0, on="w"):
        dr = self.w if on == "w" else self.d
        if fill:
            dr.rounded_rectangle(self.box(*b), radius=self.px(r), fill=255)
        else:
            dr.rounded_rectangle(self.box(*b), radius=self.px(r), outline=255, width=max(1, int(self.px(width))))

    def poly(self, pts, on="w"):
        dr = self.w if on == "w" else self.d
        dr.polygon([self.p(x, y) for x, y in pts], fill=255)

    def line(self, pts, width, on="w"):
        dr = self.w if on == "w" else self.d
        dr.line([self.p(x, y) for x, y in pts], fill=255, width=max(1, int(self.px(width))), joint="curve")
        r = self.px(width) / 2
        for x, y in (pts[0], pts[-1]):  # round caps
            cx, cy = self.p(x, y)
            dr.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)

    def arc(self, b, start, end, width, on="w"):
        dr = self.w if on == "w" else self.d
        dr.arc(self.box(*b), start, end, fill=255, width=max(1, int(self.px(width))))

    def cut(self, other_white):
        """Remove a shape from the white mask (a transparent hole)."""
        self.white = ImageChops.subtract(self.white, other_white)
        self.w = ImageDraw.Draw(self.white)

    def save(self, name, rim=0.045, rim_alpha=0.8):
        os.makedirs(OUT, exist_ok=True)
        self.render(rim, rim_alpha).save(os.path.join(OUT, name + ".png"))

    def render(self, rim=0.045, rim_alpha=0.8):
        """The glyph as an RGBA image at its size: white with a dark rim."""
        dark = self.white.filter(ImageFilter.MaxFilter(_odd(self.px(rim) * 2)))
        dark = ImageChops.lighter(dark, self.dark)
        white = ImageChops.subtract(self.white, self.dark)
        rgba = Image.new("RGBA", (self.n, self.n), (0, 0, 0, 0))
        px_w, px_d = white.load(), dark.load()
        out = rgba.load()
        for y in range(self.n):
            for x in range(self.n):
                a_w = px_w[x, y] / 255.0
                a_d = px_d[x, y] / 255.0 * rim_alpha
                a = a_w + a_d * (1 - a_w)
                if a <= 0:
                    continue
                v = int(255 * a_w / a)
                out[x, y] = (v, v, v, int(255 * a))
        return rgba.resize((self.size, self.size), Image.LANCZOS)


def _odd(v):
    v = int(round(v))
    return v + 1 if v % 2 == 0 else max(3, v)


def mask_of(size, draw):
    g = Glyph(size)
    draw(g)
    return g.white


# ------------------------------------------------------------------ reticle glyphs (64 px)

R = 64
STROKE = 0.085


def hand(g):
    # an open palm, fingers up
    g.rrect((0.30, 0.44, 0.74, 0.86), 0.12)
    for i, (x, top) in enumerate([(0.30, 0.22), (0.41, 0.14), (0.52, 0.16), (0.63, 0.24)]):
        g.rrect((x, top, x + 0.10, 0.60), 0.05)
    # thumb
    g.poly([(0.30, 0.66), (0.17, 0.46), (0.22, 0.41), (0.36, 0.52)])
    g.ellipse((0.15, 0.39, 0.25, 0.49))


def lock(g):
    g.arc((0.30, 0.12, 0.70, 0.58), 180, 360, STROKE)
    g.line([(0.30 + STROKE / 2, 0.35), (0.30 + STROKE / 2, 0.46)], STROKE)
    g.line([(0.70 - STROKE / 2, 0.35), (0.70 - STROKE / 2, 0.46)], STROKE)
    g.rrect((0.22, 0.44, 0.78, 0.88), 0.07)
    g.ellipse((0.44, 0.55, 0.56, 0.67), on="d")
    g.poly([(0.475, 0.62), (0.525, 0.62), (0.54, 0.77), (0.46, 0.77)], on="d")


def key(g):
    g.ellipse((0.12, 0.30, 0.48, 0.66), fill=False, width=STROKE)
    g.line([(0.47, 0.48), (0.88, 0.48)], STROKE)
    g.line([(0.76, 0.48), (0.76, 0.64)], STROKE)
    g.line([(0.86, 0.48), (0.86, 0.60)], STROKE)


def bolt(g):
    g.poly([(0.58, 0.08), (0.24, 0.55), (0.47, 0.55), (0.40, 0.92), (0.78, 0.42), (0.54, 0.42), (0.64, 0.08)])


def plus_ring(g):
    g.ellipse((0.12, 0.12, 0.88, 0.88), fill=False, width=STROKE)
    g.rrect((0.44, 0.27, 0.56, 0.73), 0.03)
    g.rrect((0.27, 0.44, 0.73, 0.56), 0.03)


def cross(g):
    g.rrect((0.38, 0.14, 0.62, 0.86), 0.05)
    g.rrect((0.14, 0.38, 0.86, 0.62), 0.05)


def magnifier(g):
    g.ellipse((0.14, 0.12, 0.62, 0.60), fill=False, width=STROKE * 1.1)
    g.line([(0.57, 0.56), (0.84, 0.84)], STROKE * 1.5)


def wheel(g):
    g.ellipse((0.10, 0.10, 0.90, 0.90), fill=False, width=STROKE)
    g.ellipse((0.40, 0.40, 0.60, 0.60))
    g.line([(0.14, 0.46), (0.86, 0.46)], STROKE)
    g.line([(0.50, 0.52), (0.50, 0.88)], STROKE)


def drop(g):
    pts = []
    for i in range(64):
        t = i / 64 * 2 * math.pi
        # a teardrop: round at the bottom, a point at the top
        x = 0.5 + 0.30 * math.sin(t) * (1 - math.cos(t)) * 0.5 * 1.25
        y = 0.12 + 0.76 * (1 - math.cos(t)) / 2
        pts.append((x, y))
    g.poly(pts)
    g.arc((0.33, 0.48, 0.60, 0.78), 110, 180, STROKE * 0.7, on="d")


def monitor(g):
    g.rrect((0.10, 0.16, 0.90, 0.68), 0.06, fill=False, width=STROKE)
    g.line([(0.50, 0.68), (0.50, 0.82)], STROKE)
    g.line([(0.32, 0.84), (0.68, 0.84)], STROKE)
    g.rrect((0.22, 0.28, 0.48, 0.38), 0.02)
    g.rrect((0.22, 0.45, 0.62, 0.53), 0.02)


def gem(g):
    g.poly([(0.22, 0.36), (0.36, 0.18), (0.64, 0.18), (0.78, 0.36), (0.50, 0.86)])
    w = 0.03
    g.line([(0.22, 0.36), (0.78, 0.36)], w, on="d")
    g.line([(0.40, 0.36), (0.50, 0.80)], w, on="d")
    g.line([(0.60, 0.36), (0.50, 0.80)], w, on="d")
    g.line([(0.40, 0.36), (0.50, 0.20)], w, on="d")
    g.line([(0.60, 0.36), (0.50, 0.20)], w, on="d")


def lever(g):
    g.rrect((0.18, 0.70, 0.82, 0.86), 0.04)
    g.line([(0.50, 0.74), (0.70, 0.24)], STROKE)
    g.ellipse((0.61, 0.10, 0.81, 0.30))


def dial(g):
    g.ellipse((0.12, 0.12, 0.88, 0.88), fill=False, width=STROKE)
    for i in range(12):
        a = i / 12 * 2 * math.pi
        r0, r1 = 0.27, 0.32 if i % 3 else 0.25
        g.line([(0.5 + math.cos(a) * r1, 0.5 + math.sin(a) * r1), (0.5 + math.cos(a) * 0.30, 0.5 + math.sin(a) * 0.30)], 0.035)
    g.ellipse((0.40, 0.40, 0.60, 0.60))
    g.line([(0.5, 0.5), (0.5, 0.24)], 0.06)


def box(g):
    # a crate: a box with planks
    g.rrect((0.14, 0.22, 0.86, 0.84), 0.05)
    g.line([(0.14, 0.43), (0.86, 0.43)], 0.04, on="d")
    g.line([(0.14, 0.63), (0.86, 0.63)], 0.04, on="d")
    g.line([(0.24, 0.30), (0.24, 0.76)], 0.035, on="d")
    g.line([(0.76, 0.30), (0.76, 0.76)], 0.035, on="d")


def grille(g):
    """A vent grille: a frame with slats and a screw in each corner (taken off by hand or with a screwdriver)."""
    g.rrect((0.12, 0.18, 0.88, 0.82), 0.05, fill=False, width=STROKE)
    for y in (0.36, 0.50, 0.64):
        g.line([(0.27, y), (0.73, y)], STROKE * 0.7)
    for x, y in ((0.20, 0.26), (0.80, 0.26), (0.20, 0.74), (0.80, 0.74)):
        g.ellipse((x - 0.035, y - 0.035, x + 0.035, y + 0.035), on="d")


def door(g):
    g.rrect((0.24, 0.10, 0.76, 0.90), 0.03, fill=False, width=STROKE)
    g.ellipse((0.60, 0.48, 0.68, 0.56))


# ------------------------------------------------------------------ status icons (64 px)

def speaker(level):
    def draw(g):
        g.rrect((0.10, 0.38, 0.28, 0.62), 0.03)
        g.poly([(0.24, 0.38), (0.46, 0.18), (0.46, 0.82), (0.24, 0.62)])
        for i in range(3):
            if i >= level:
                break
            r = 0.15 + i * 0.13
            g.arc((0.42 - r, 0.5 - r, 0.42 + r, 0.5 + r), -50, 50, STROKE * 0.85)
        if level == 0:  # silent: a small cross
            g.line([(0.60, 0.40), (0.78, 0.60)], STROKE * 0.75)
            g.line([(0.78, 0.40), (0.60, 0.60)], STROKE * 0.75)
    return draw


def eye(openness):
    """0 = closed (a lid with lashes), 1 = wide open."""
    def draw(g):
        x0, x1, cy = 0.08, 0.92, 0.52
        up = 0.30 * openness
        dn = 0.22 * openness
        def lid(h, n=40):
            return [(x0 + (x1 - x0) * i / n, cy - h * math.sin(math.pi * i / n)) for i in range(n + 1)]
        upper = lid(up)
        lower = lid(-dn)
        if openness < 0.06:
            # closed: the lid curves down, three lashes below it
            curve = lid(-0.10)
            g.line(curve, STROKE)
            for t in (0.3, 0.5, 0.7):
                x = x0 + (x1 - x0) * t
                y = cy + 0.10 * math.sin(math.pi * t)
                g.line([(x, y), (x + (t - 0.5) * 0.18, y + 0.12)], STROKE * 0.7)
            return
        inside = Glyph(g.size)
        inside.poly(upper + lower[::-1])
        # outline of the almond
        g.line(upper, STROKE)
        g.line(lower, STROKE)
        # iris, clipped to the opening
        iris = Glyph(g.size)
        r = 0.17
        iris.ellipse((0.5 - r, cy - r, 0.5 + r, cy + r))
        clipped = ImageChops.multiply(iris.white, inside.white)
        g.white = ImageChops.lighter(g.white, clipped)
        g.w = ImageDraw.Draw(g.white)
        pupil = Glyph(g.size)
        rp = 0.065
        pupil.ellipse((0.5 - rp, cy - rp, 0.5 + rp, cy + rp))
        g.dark = ImageChops.lighter(g.dark, ImageChops.multiply(pupil.white, inside.white))
        g.d = ImageDraw.Draw(g.dark)
    return draw


def heart(filled):
    def draw(g):
        pts = []
        for i in range(96):
            t = i / 96 * 2 * math.pi
            x = 16 * math.sin(t) ** 3
            y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
            pts.append((0.5 + x / 36, 0.47 - y / 36))
        if filled:
            g.poly(pts)
        else:
            g.line(pts + [pts[0]], STROKE * 0.9)
    return draw


# ------------------------------------------------------------------ key caps and mouse

def keycap():
    """48 px, 9-sliced at 16: a soft translucent fill and a lighter rim."""
    n = 48 * SS
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([2 * SS, 2 * SS, n - 2 * SS, n - 2 * SS], radius=10 * SS, fill=(255, 255, 255, 70), outline=(255, 255, 255, 230), width=3 * SS)
    # a thicker bottom edge: reads as a key
    d.rounded_rectangle([2 * SS, n - 9 * SS, n - 2 * SS, n - 2 * SS], radius=8 * SS, fill=(255, 255, 255, 120))
    img = img.resize((48, 48), Image.LANCZOS)
    img.save(os.path.join(OUT, "keycap.png"))


def mouse(button):
    """button: 'lmb', 'rmb', 'mmb', 'wheel'."""
    def draw(g):
        g.rrect((0.26, 0.10, 0.74, 0.90), 0.24, fill=False, width=0.07)
        g.line([(0.26, 0.42), (0.74, 0.42)], 0.05)
        g.line([(0.50, 0.12), (0.50, 0.42)], 0.05)
        if button in ("lmb", "rmb"):
            # the pressed half: the body's inside clipped to that button
            inside = Glyph(g.size)
            inside.rrect((0.26, 0.10, 0.74, 0.90), 0.24)
            half = Glyph(g.size)
            half.rrect((0.26, 0.08, 0.50, 0.42) if button == "lmb" else (0.50, 0.08, 0.74, 0.42), 0.0)
            g.white = ImageChops.lighter(g.white, ImageChops.multiply(inside.white, half.white))
            g.w = ImageDraw.Draw(g.white)
        else:
            g.rrect((0.45, 0.18, 0.55, 0.36), 0.04)
            if button == "wheel":
                g.poly([(0.84, 0.30), (0.94, 0.42), (0.74, 0.42)])
                g.poly([(0.84, 0.70), (0.94, 0.58), (0.74, 0.58)])
    return draw


# ------------------------------------------------------------------ route signs: the kind of house and its main job


def _windows(g, x0, y0, x1, y1, cols, rows):
    w, h = (x1 - x0) / cols, (y1 - y0) / rows
    for c in range(cols):
        for r in range(rows):
            g.rrect((x0 + c * w + w * 0.22, y0 + r * h + h * 0.22, x0 + (c + 1) * w - w * 0.22, y0 + (r + 1) * h - h * 0.22), 0.01, on="d")


def house_office(g):
    g.rrect((0.24, 0.16, 0.76, 0.88), 0.03)
    _windows(g, 0.28, 0.22, 0.72, 0.70, 3, 4)
    g.rrect((0.44, 0.72, 0.56, 0.88), 0.01, on="d")


def house_business(g):
    g.rrect((0.16, 0.10, 0.50, 0.88), 0.03)
    g.rrect((0.54, 0.34, 0.84, 0.88), 0.03)
    _windows(g, 0.20, 0.16, 0.46, 0.82, 2, 6)
    _windows(g, 0.58, 0.40, 0.80, 0.82, 2, 4)


def house_datacenter(g):
    g.rrect((0.26, 0.10, 0.74, 0.90), 0.05)
    for i in range(4):
        y = 0.18 + i * 0.18
        g.rrect((0.32, y, 0.68, y + 0.11), 0.02, on="d")
        g.ellipse((0.58, y + 0.03, 0.63, y + 0.08))


def house_warehouse(g):
    g.poly([(0.10, 0.44), (0.50, 0.18), (0.90, 0.44), (0.90, 0.86), (0.10, 0.86)])
    g.rrect((0.30, 0.54, 0.70, 0.86), 0.01, on="d")
    for i in range(3):
        y = 0.60 + i * 0.08
        g.line([(0.32, y), (0.68, y)], 0.025)


def house_hq(g):
    g.poly([(0.36, 0.30), (0.64, 0.30), (0.70, 0.88), (0.30, 0.88)])
    g.line([(0.50, 0.30), (0.50, 0.06)], 0.04)
    g.poly([(0.52, 0.06), (0.74, 0.11), (0.52, 0.17)])
    _windows(g, 0.38, 0.36, 0.62, 0.80, 2, 4)


def house_hospital(g):
    g.rrect((0.14, 0.14, 0.86, 0.86), 0.12)
    g.rrect((0.42, 0.26, 0.58, 0.74), 0.02, on="d")
    g.rrect((0.26, 0.42, 0.74, 0.58), 0.02, on="d")


def house_garage(g):
    # a wrench across the glyph
    g.line([(0.30, 0.72), (0.62, 0.40)], 0.13)
    g.ellipse((0.54, 0.14, 0.88, 0.48))
    g.poly([(0.68, 0.14), (0.86, 0.14), (0.86, 0.30), (0.74, 0.30)], on="d")
    g.cut(mask_of(g.size, lambda m: m.poly([(0.70, 0.10), (0.92, 0.10), (0.92, 0.30), (0.74, 0.30)])))
    g.ellipse((0.16, 0.66, 0.38, 0.88))
    g.ellipse((0.23, 0.73, 0.31, 0.81), on="d")


def house_apartments(g):
    # a tall block of flats: rows of windows, the one entrance with its canopy at the bottom
    g.rrect((0.28, 0.06, 0.72, 0.92), 0.03)
    _windows(g, 0.32, 0.10, 0.68, 0.70, 3, 6)
    g.rrect((0.43, 0.76, 0.57, 0.92), 0.01, on="d")
    g.rrect((0.38, 0.72, 0.62, 0.75), 0.005, on="d")


def house_abandoned(g):
    # a research block with a broken roof line, boarded windows and a hazard mark on its door
    g.poly([(0.16, 0.30), (0.40, 0.22), (0.50, 0.32), (0.62, 0.18), (0.84, 0.26), (0.84, 0.88), (0.16, 0.88)])
    g.line([(0.24, 0.42), (0.40, 0.56)], 0.05, on="d")
    g.line([(0.24, 0.56), (0.40, 0.42)], 0.05, on="d")
    g.rrect((0.58, 0.40, 0.76, 0.56), 0.01, on="d")
    g.poly([(0.50, 0.62), (0.60, 0.80), (0.40, 0.80)], on="d")


def job_vault(g):
    g.rrect((0.12, 0.12, 0.88, 0.88), 0.08)
    g.ellipse((0.24, 0.24, 0.76, 0.76), fill=False, width=0.06, on="d")
    g.ellipse((0.43, 0.43, 0.57, 0.57), on="d")
    for a in range(0, 360, 60):
        r = math.radians(a)
        g.line([(0.5 + math.cos(r) * 0.07, 0.5 + math.sin(r) * 0.07), (0.5 + math.cos(r) * 0.2, 0.5 + math.sin(r) * 0.2)], 0.04, on="d")


def job_hostage(g):
    g.ellipse((0.38, 0.16, 0.62, 0.40))
    g.rrect((0.28, 0.44, 0.72, 0.90), 0.14)
    for x in (0.22, 0.40, 0.60, 0.78):
        g.line([(x, 0.08), (x, 0.92)], 0.05, on="d")
    g.line([(0.14, 0.08), (0.86, 0.08)], 0.05, on="d")


# ------------------------------------------------------------------ robots and devices (map markers, hack slots)
# Bold silhouettes read at 20 px: thick parts, few details, every kind a different outline.

RS = 0.10  # robot stroke: limbs, rails, stems


def _rot(pts, cx, cy, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def _dot(g, x, y, r, on="w"):
    g.ellipse((x - r, y - r, x + r, y + r), on=on)


def robot_guard(g):
    # a biped: a boxy head with one camera eye, a torso, two legs bent at the knee, flat feet
    g.rrect((0.32, 0.06, 0.68, 0.32), 0.07)
    _dot(g, 0.50, 0.19, 0.075, on="d")
    _dot(g, 0.50, 0.19, 0.03)
    g.rrect((0.27, 0.36, 0.73, 0.62), 0.07)
    g.line([(0.40, 0.60), (0.35, 0.75), (0.38, 0.87)], RS)
    g.line([(0.60, 0.60), (0.65, 0.75), (0.62, 0.87)], RS)
    g.rrect((0.24, 0.84, 0.44, 0.93), 0.03)
    g.rrect((0.56, 0.84, 0.76, 0.93), 0.03)


def robot_listener(g):
    # a blind wheeled box with two big round ear dishes on stalks
    g.line([(0.40, 0.46), (0.24, 0.26)], RS * 0.8)
    g.line([(0.60, 0.46), (0.76, 0.26)], RS * 0.8)
    for cx in (0.21, 0.79):
        _dot(g, cx, 0.22, 0.16)
        g.ellipse((cx - 0.09, 0.13, cx + 0.09, 0.31), fill=False, width=0.035, on="d")
        _dot(g, cx, 0.22, 0.03, on="d")
    g.rrect((0.26, 0.42, 0.74, 0.74), 0.08)
    g.rrect((0.34, 0.52, 0.66, 0.60), 0.03, on="d")
    for cx in (0.32, 0.68):
        _dot(g, cx, 0.80, 0.11)
        _dot(g, cx, 0.80, 0.04, on="d")


def robot_drone(g):
    # a quadcopter seen from the front: two rotors on arms, a body, a camera ball hanging below
    g.line([(0.20, 0.38), (0.80, 0.38)], RS)
    for cx in (0.20, 0.80):
        g.line([(cx, 0.38), (cx, 0.24)], RS * 0.8)
        g.rrect((cx - 0.17, 0.17, cx + 0.17, 0.25), 0.04)
    g.rrect((0.34, 0.32, 0.66, 0.56), 0.08)
    g.line([(0.50, 0.56), (0.50, 0.64)], RS * 0.7)
    _dot(g, 0.50, 0.72, 0.12)
    _dot(g, 0.50, 0.74, 0.05, on="d")
    g.line([(0.30, 0.56), (0.24, 0.66)], RS * 0.6)
    g.line([(0.70, 0.56), (0.76, 0.66)], RS * 0.6)


def robot_spider(g):
    # top view: a body and eight legs, four a side, bent at the knee
    for s in (-1, 1):
        for knee, foot, y0 in (((0.26, 0.14), (0.14, 0.04), 0.36), ((0.18, 0.32), (0.06, 0.30), 0.42),
                               ((0.18, 0.56), (0.06, 0.66), 0.48), ((0.26, 0.74), (0.16, 0.92), 0.54)):
            g.line([(0.5 + s * 0.08, y0), (0.5 + s * (0.5 - knee[0]), knee[1]), (0.5 + s * (0.5 - foot[0]), foot[1])], RS * 0.7)
    g.ellipse((0.36, 0.26, 0.64, 0.54))
    g.ellipse((0.33, 0.46, 0.67, 0.90))
    _dot(g, 0.44, 0.33, 0.035, on="d")
    _dot(g, 0.56, 0.33, 0.035, on="d")


def robot_vacuum(g):
    # a disc hanging from a ceiling rail, a fan of laser lines down to the floor
    g.rrect((0.06, 0.06, 0.94, 0.14), 0.03)
    g.line([(0.50, 0.12), (0.50, 0.22)], RS)
    g.ellipse((0.20, 0.20, 0.80, 0.42))
    _dot(g, 0.50, 0.33, 0.05, on="d")
    for x in (0.16, 0.33, 0.50, 0.67, 0.84):
        g.line([(0.50, 0.42), (x, 0.90)], 0.05)


def robot_turret(g):
    # a gun turret: a tripod base, a round housing, a double barrel to the right
    g.poly([(0.30, 0.66), (0.50, 0.66), (0.62, 0.88), (0.18, 0.88)])
    g.rrect((0.12, 0.84, 0.68, 0.93), 0.03)
    g.line([(0.40, 0.66), (0.40, 0.56)], RS * 1.2)
    g.rrect((0.14, 0.28, 0.64, 0.60), 0.15)
    g.rrect((0.56, 0.32, 0.94, 0.41), 0.03)
    g.rrect((0.56, 0.47, 0.94, 0.56), 0.03)
    _dot(g, 0.32, 0.44, 0.06, on="d")


def robot_thumper(g):
    # a body on tracks, a piston down to a heavy foot on the ground, shock waves rising to both sides of it
    g.rrect((0.30, 0.04, 0.70, 0.24), 0.05)
    _dot(g, 0.50, 0.14, 0.045, on="d")
    g.rrect((0.12, 0.24, 0.88, 0.40), 0.08)
    for x in (0.24, 0.42, 0.58, 0.76):
        _dot(g, x, 0.32, 0.035, on="d")
    g.rrect((0.44, 0.38, 0.56, 0.62), 0.02)
    g.rrect((0.33, 0.60, 0.67, 0.80), 0.04)
    g.rrect((0.04, 0.84, 0.96, 0.92), 0.03)
    for r in (0.30, 0.44):
        g.arc((0.50 - r, 0.84 - r, 0.50 + r, 0.84 + r), 198, 240, RS * 0.75)
        g.arc((0.50 - r, 0.84 - r, 0.50 + r, 0.84 + r), 300, 342, RS * 0.75)


def robot_hound(g):
    # a robot dog in profile, head down to the floor, sniffing (scent dots at the nose)
    g.rrect((0.12, 0.30, 0.64, 0.52), 0.09)
    g.line([(0.14, 0.34), (0.06, 0.16)], RS * 0.7)
    for x0, x1 in ((0.20, 0.16), (0.32, 0.30), (0.50, 0.50), (0.60, 0.64)):
        g.line([(x0, 0.48), (x1, 0.88)], RS)
    g.line([(0.58, 0.38), (0.72, 0.58)], RS * 1.4)
    g.poly(_rot([(0.62, 0.54), (0.86, 0.54), (0.94, 0.66), (0.86, 0.72), (0.62, 0.72)], 0.74, 0.63, 30))
    _dot(g, 0.72, 0.60, 0.03, on="d")
    _dot(g, 0.94, 0.88, 0.035)
    _dot(g, 0.84, 0.92, 0.03)


def robot_sorter(g):
    # a sorting bin with three slots, a circle, a square and a triangle dropping into them
    _dot(g, 0.22, 0.20, 0.11)
    g.rrect((0.40, 0.09, 0.60, 0.29), 0.02)
    g.poly([(0.78, 0.07), (0.90, 0.30), (0.66, 0.30)])
    g.rrect((0.10, 0.40, 0.90, 0.92), 0.06)
    for x in (0.22, 0.50, 0.78):
        g.poly([(x - 0.10, 0.40), (x + 0.10, 0.40), (x + 0.05, 0.54), (x - 0.05, 0.54)], on="d")
    g.rrect((0.20, 0.68, 0.80, 0.76), 0.02, on="d")


def robot_worker(g):
    # a small robot on a tread holding a big crate over its head with both arms
    g.rrect((0.10, 0.04, 0.90, 0.36), 0.03)
    g.rrect((0.17, 0.10, 0.83, 0.30), 0.0, fill=False, width=0.035, on="d")
    g.line([(0.19, 0.12), (0.81, 0.28)], 0.035, on="d")
    g.line([(0.38, 0.56), (0.22, 0.48), (0.22, 0.36)], RS * 0.9)
    g.line([(0.62, 0.56), (0.78, 0.48), (0.78, 0.36)], RS * 0.9)
    g.rrect((0.34, 0.44, 0.66, 0.74), 0.08)
    _dot(g, 0.44, 0.56, 0.035, on="d")
    _dot(g, 0.56, 0.56, 0.035, on="d")
    g.rrect((0.26, 0.78, 0.74, 0.93), 0.07)
    for x in (0.36, 0.50, 0.64):
        _dot(g, x, 0.855, 0.03, on="d")


def robot_hunter(g):
    # a sharp mask: swept-back horns, a pointed jaw, two angry slit eyes
    g.poly([(0.08, 0.08), (0.36, 0.30), (0.64, 0.30), (0.92, 0.08), (0.86, 0.52), (0.62, 0.78),
            (0.50, 0.94), (0.38, 0.78), (0.14, 0.52)])
    g.poly([(0.20, 0.42), (0.44, 0.52), (0.42, 0.60), (0.26, 0.54)], on="d")
    g.poly([(0.80, 0.42), (0.56, 0.52), (0.58, 0.60), (0.74, 0.54)], on="d")


def robot_lidar(g):
    # a pole on a floor base, a head with a laser line through it and its spin orbit
    g.rrect((0.30, 0.84, 0.70, 0.93), 0.03)
    g.line([(0.50, 0.86), (0.50, 0.44)], RS)
    g.ellipse((0.14, 0.30, 0.86, 0.54), fill=False, width=0.045)
    g.rrect((0.06, 0.39, 0.94, 0.45), 0.03)
    g.rrect((0.38, 0.24, 0.62, 0.56), 0.06)
    _dot(g, 0.50, 0.40, 0.045, on="d")
    g.poly([(0.20, 0.29), (0.30, 0.26), (0.28, 0.36)])


def robot_motionsensor(g):
    # a dome sensor in the top-right corner of two walls, motion waves going out of it
    g.rrect((0.86, 0.06, 0.94, 0.94), 0.02)
    g.rrect((0.06, 0.06, 0.94, 0.14), 0.02)
    g.poly(_rot([(0.62, 0.18), (0.82, 0.18), (0.82, 0.38)], 0.82, 0.18, 0))
    _dot(g, 0.70, 0.30, 0.12)
    for r in (0.30, 0.46, 0.62):
        g.arc((0.82 - r, 0.18 - r, 0.82 + r, 0.18 + r), 110, 160, RS * 0.75)


def robot_pressureplate(g):
    # a floor tile in perspective with an arrow pressing down onto it
    g.poly([(0.20, 0.64), (0.80, 0.64), (0.94, 0.88), (0.06, 0.88)])
    g.poly([(0.30, 0.69), (0.70, 0.69), (0.78, 0.83), (0.22, 0.83)], on="d")
    g.poly([(0.34, 0.71), (0.66, 0.71), (0.72, 0.81), (0.28, 0.81)])
    g.rrect((0.41, 0.06, 0.59, 0.36), 0.03)
    g.poly([(0.24, 0.32), (0.76, 0.32), (0.50, 0.58)])


def robot_camera(g):
    # a wall CCTV camera: a wall plate, an arm, the body tilted down to the right with its lens and hood
    g.rrect((0.06, 0.14, 0.16, 0.50), 0.02)
    g.line([(0.14, 0.32), (0.30, 0.32), (0.36, 0.40)], RS)
    body = _rot([(0.26, 0.30), (0.80, 0.30), (0.80, 0.54), (0.26, 0.54)], 0.53, 0.42, 22)
    g.poly(body)
    hood = _rot([(0.30, 0.24), (0.92, 0.24), (0.86, 0.32), (0.30, 0.32)], 0.53, 0.42, 22)
    g.poly(hood)
    lens = _rot([(0.80, 0.42)], 0.53, 0.42, 22)[0]
    _dot(g, lens[0] - 0.05, lens[1] - 0.01, 0.07, on="d")


def robot_sweeper(g):
    # a flat floor disc in 3/4 view with a bumper, a sensor bump and two spinning side brushes
    g.ellipse((0.14, 0.36, 0.86, 0.66))
    g.rrect((0.14, 0.51, 0.86, 0.62), 0.0)
    g.ellipse((0.14, 0.47, 0.86, 0.77))
    g.arc((0.14, 0.36, 0.86, 0.66), 0, 180, 0.035, on="d")
    g.rrect((0.40, 0.28, 0.60, 0.44), 0.05)
    for cx in (0.20, 0.80):
        for a in range(0, 180, 45):
            r = math.radians(a)
            g.line([(cx - math.cos(r) * 0.15, 0.82 - math.sin(r) * 0.07), (cx + math.cos(r) * 0.15, 0.82 + math.sin(r) * 0.07)], 0.05)


def robot_warden(g):
    # a heavy guard behind a tall shield: wide shoulders, a head with its eye over the rim, feet below
    g.rrect((0.32, 0.04, 0.68, 0.26), 0.06)
    _dot(g, 0.50, 0.15, 0.06, on="d")
    g.rrect((0.06, 0.26, 0.94, 0.44), 0.08)
    g.rrect((0.20, 0.84, 0.40, 0.94), 0.03)
    g.rrect((0.60, 0.84, 0.80, 0.94), 0.03)
    shield = [(0.16, 0.32), (0.84, 0.32), (0.84, 0.64), (0.50, 0.90), (0.16, 0.64)]
    g.poly(shield)
    g.cut(mask_of(g.size, lambda m: [m.line([a, b], 0.035) for a, b in zip(shield, shield[1:] + shield[:1])]))
    g.line([(0.32, 0.46), (0.50, 0.60), (0.68, 0.46)], 0.07, on="d")


ROBOTS = {
    "robot_guard": robot_guard, "robot_listener": robot_listener, "robot_drone": robot_drone, "robot_spider": robot_spider,
    "robot_vacuum": robot_vacuum, "robot_turret": robot_turret, "robot_thumper": robot_thumper, "robot_hound": robot_hound,
    "robot_sorter": robot_sorter, "robot_worker": robot_worker, "robot_hunter": robot_hunter, "robot_lidar": robot_lidar,
    "robot_motionsensor": robot_motionsensor, "robot_pressureplate": robot_pressureplate, "robot_camera": robot_camera,
    "robot_sweeper": robot_sweeper, "robot_warden": robot_warden,
}


def make(name, draw, size=R, rim=0.045):
    g = Glyph(size)
    draw(g)
    g.save(name, rim=rim)


def app_icon(size=512):
    """The application's icon (for now): the HUD's open eye, white on the studio's navy rounded plate."""
    g = Glyph(size)
    eye(1.0)(g)
    glyph = g.render(rim=0.03, rim_alpha=0.9)
    plate = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
    ImageDraw.Draw(plate).rounded_rectangle([0, 0, size * SS - 1, size * SS - 1], radius=size * SS * 0.2, fill=(0x2A, 0x32, 0x4B, 255))
    plate = plate.resize((size, size), Image.LANCZOS)
    inner = int(size * 0.86)
    plate.alpha_composite(glyph.resize((inner, inner), Image.LANCZOS), ((size - inner) // 2, (size - inner) // 2))
    path = os.path.join(OUT, "..", "AppIcon.png")
    plate.save(path)
    print("app icon ->", os.path.abspath(path))


def main(only=None):
    """only: a name prefix (python Tools/Art/generate_icons.py --only robot_) writes just the icons it matches."""
    os.makedirs(OUT, exist_ok=True)
    jobs = []
    glyphs = {
        "ret_hand": hand, "ret_lock": lock, "ret_key": key, "ret_hack": bolt, "ret_revive": plus_ring,
        "ret_heal": cross, "ret_search": magnifier, "ret_seat": wheel, "ret_fuel": drop, "ret_terminal": monitor,
        "ret_craft": gem, "ret_lever": lever, "ret_safe": dial, "ret_crate": box, "ret_door": door, "ret_vent": grille,
    }
    for name, draw in glyphs.items():
        jobs.append((name, lambda n=name, d=draw: make(n, d)))
    for name, draw in {"house_office": house_office, "house_business": house_business, "house_datacenter": house_datacenter,
                       "house_warehouse": house_warehouse, "house_hq": house_hq, "house_hospital": house_hospital,
                       "house_garage": house_garage, "house_apartments": house_apartments, "house_abandoned": house_abandoned, "job_vault": job_vault, "job_hostage": job_hostage}.items():
        jobs.append((name, lambda n=name, d=draw: make(n, d, size=96)))
    for name, draw in ROBOTS.items():
        jobs.append((name, lambda n=name, d=draw: make(n, d)))
    for level in range(4):
        jobs.append((f"noise_{level}", lambda lv=level: make(f"noise_{lv}", speaker(lv))))
    frames = 8
    for i in range(frames):
        jobs.append((f"eye_{i}", lambda k=i: make(f"eye_{k}", eye(k / (frames - 1)))))
    jobs.append(("heart_full", lambda: make("heart_full", heart(True))))
    jobs.append(("heart_empty", lambda: make("heart_empty", heart(False))))
    jobs.append(("keycap", keycap))
    for b in ("lmb", "rmb", "mmb", "wheel"):
        jobs.append((f"mouse_{b}", lambda bb=b: make(f"mouse_{bb}", mouse(bb), rim=0.03)))
    jobs.append(("AppIcon", app_icon))
    for name, job in jobs:
        if only is None or name.startswith(only):
            job()
    print("icons ->", os.path.abspath(OUT))

if __name__ == "__main__":
    import sys
    args = sys.argv[1:]
    main(args[args.index("--only") + 1] if "--only" in args else None)
