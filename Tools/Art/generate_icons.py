"""HUD icons: the reticle's interaction glyphs, the noise speaker, the visibility eye, hearts, key caps and mouse buttons.

    python Tools/Art/generate_icons.py

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
        rgba = rgba.resize((self.size, self.size), Image.LANCZOS)
        os.makedirs(OUT, exist_ok=True)
        rgba.save(os.path.join(OUT, name + ".png"))


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


def make(name, draw, size=R, rim=0.045):
    g = Glyph(size)
    draw(g)
    g.save(name, rim=rim)


def main():
    os.makedirs(OUT, exist_ok=True)
    glyphs = {
        "ret_hand": hand, "ret_lock": lock, "ret_key": key, "ret_hack": bolt, "ret_revive": plus_ring,
        "ret_heal": cross, "ret_search": magnifier, "ret_seat": wheel, "ret_fuel": drop, "ret_terminal": monitor,
        "ret_craft": gem, "ret_lever": lever, "ret_safe": dial, "ret_crate": box, "ret_door": door,
    }
    for name, draw in glyphs.items():
        make(name, draw)
    for level in range(4):
        make(f"noise_{level}", speaker(level))
    frames = 8
    for i in range(frames):
        make(f"eye_{i}", eye(i / (frames - 1)))
    make("heart_full", heart(True))
    make("heart_empty", heart(False))
    keycap()
    for b in ("lmb", "rmb", "mmb", "wheel"):
        make(f"mouse_{b}", mouse(b), rim=0.03)
    print("icons ->", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
