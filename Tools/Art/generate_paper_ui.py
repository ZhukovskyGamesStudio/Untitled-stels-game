"""The interface's paper look (UiPalette, AssetBuilder.UI): paper cards and their shadow, sticky notes, masking tape,
a binder clip, a rubber stamp frame, form boxes and checkboxes, a highlighter stroke, a light key cap, a push pin, a
round sticker, graph paper, and the safe's dial drawn on paper.

    D:/AI/ComfyUI_windows_portable/python_embeded/python.exe -I Tools/Art/generate_paper_ui.py

Writes Assets/_Game/Art/Sprites/UI/Paper/*.png (deterministic: the noise has a fixed seed). Most sprites are white or grey
so Unity tints them (Image.color = the paper / note / ink colour of UiPalette); the clip, the key cap, the pin and the
dial's knob carry their own colours. Drawn at 4x and downsampled. 9-slice borders are set by the builder (PaperSprite).
"""
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Game", "Art", "Sprites", "UI", "Paper")
SS = 4
INK = (35, 38, 43)


def canvas(w, h, color=(0, 0, 0, 0)):
    return Image.new("RGBA", (w * SS, h * SS), color)


def save(im, name, w, h):
    os.makedirs(OUT, exist_ok=True)
    im.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name + ".png"))


def rounded_mask(w, h, r, inset=0):
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle((inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1), r * SS, fill=255)
    return m


def solid(mask, rgb):
    im = Image.new("RGBA", mask.size, rgb + (0,))
    im.putalpha(mask)
    return im


def noise(size, seed, lo, hi, blur=0):
    rnd = random.Random(seed)
    n = Image.new("L", size)
    n.putdata([rnd.randint(lo, hi) for _ in range(size[0] * size[1])])
    return n.filter(ImageFilter.GaussianBlur(blur)) if blur else n


# ---------------------------------------------------------------------------------------------- paper and its shadow

def paper():
    """A sheet: white, square corners softened (2 px), full bleed. 9-slice 8."""
    w = h = 48
    save(solid(rounded_mask(w, h, 2), (255, 255, 255)), "paper", w, h)


def card():
    """A HUD card: a sheet with a faint darker rim and its own small shadow to the lower right, baked in (one Image a
    presenter can show, hide and tint). The paper is the top-left 44 x 44; 9-slice 8 (12 right and bottom)."""
    w = h = 48
    im = canvas(w, h)
    sh = Image.new("L", im.size, 0)
    ImageDraw.Draw(sh).rounded_rectangle((2 * SS, 3 * SS, 46 * SS, 47 * SS), 3 * SS, fill=95)
    im = Image.alpha_composite(im, solid(sh.filter(ImageFilter.GaussianBlur(1.4 * SS)), (0, 0, 0)))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, 44 * SS - 1, 44 * SS - 1), 2 * SS, fill=(178, 178, 178, 255))
    d.rounded_rectangle((SS, SS, 43 * SS - 1, 43 * SS - 1), 1.5 * SS, fill=(255, 255, 255, 255))
    save(im, "card", w, h)


def shadow():
    """The soft shadow under a sheet: a blurred rounded rect, 16 px of blur round a 16 px core. 9-slice 24."""
    w = h = 64
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle((16 * SS, 16 * SS, (w - 16) * SS, (h - 16) * SS), 3 * SS, fill=150)
    m = m.filter(ImageFilter.GaussianBlur(5 * SS))
    save(solid(m, (0, 0, 0)), "shadow", w, h)


def sticky():
    """A sticky note (grey, tinted by the note's colour): the glue strip at the top a shade darker, the bottom right
    corner lifting off (darker towards the curl). 9-slice 16."""
    w = h = 64
    im = canvas(w, h)
    body = Image.new("L", (w * SS, h * SS), 255)
    d = ImageDraw.Draw(body)
    d.rectangle((0, 0, w * SS, 9 * SS), fill=238)                        # the glue strip
    # the curl: a diagonal gradient in the last 16 px of the corner
    for i in range(16 * SS):
        v = int(255 - 36 * (i / (16 * SS)) ** 2)
        d.line(((w * SS - 16 * SS + i, h * SS), (w * SS, h * SS - 16 * SS + i)), fill=v, width=1)
    grain = noise(body.size, 7, 244, 255, 1.2)
    body = ImageChops.multiply(body, grain)
    mask = Image.new("L", (w * SS, h * SS), 255)
    ImageDraw.Draw(mask).polygon([(w * SS, h * SS - 5 * SS), (w * SS, h * SS), (w * SS - 5 * SS, h * SS)], fill=0)  # the lifted tip
    rgb = Image.merge("RGB", (body, body, body))
    im = rgb.convert("RGBA")
    im.putalpha(mask)
    save(im, "sticky", w, h)


def tape():
    """Masking tape: a cream strip, half see-through, its ends torn. 9-slice 10 left and right."""
    w, h = 64, 24
    rnd = random.Random(11)
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    pts = [(0, 0)]
    # torn ends: little zigzags
    left = [(rnd.uniform(0, 3) * SS, y * SS) for y in range(0, h + 1, 2)]
    right = [((w - rnd.uniform(0, 3)) * SS, y * SS) for y in range(h, -1, -2)]
    d.polygon(left + right, fill=200)
    grain = noise(m.size, 12, 200, 255, 2)
    m = ImageChops.multiply(m, grain)
    save(solid(m, (255, 255, 255)), "tape", w, h)


def clip():
    """A black binder clip seen from the front, its two wire handles up."""
    w, h = 48, 44
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    s = SS
    # the handles: two grey wire loops
    for x0 in (13, 23):
        d.rounded_rectangle((x0 * s, 1 * s, (x0 + 12) * s, 26 * s), 4 * s, outline=(150, 154, 160, 255), width=int(2.2 * s))
    # the body: a black trapezoid with a light edge along the top
    d.polygon([(6 * s, 22 * s), (42 * s, 22 * s), (46 * s, 42 * s), (2 * s, 42 * s)], fill=(28, 29, 32, 255))
    d.line(((7 * s, 23 * s), (41 * s, 23 * s)), fill=(90, 94, 100, 255), width=int(1.5 * s))
    d.line(((3 * s, 41 * s), (45 * s, 41 * s)), fill=(10, 10, 12, 255), width=s)
    save(im, "clip", w, h)


def stamp_frame():
    """A rubber stamp's frame: a double rounded outline whose ink did not take everywhere. White, tinted red. 9-slice 14."""
    w = h = 64
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle((2 * SS, 2 * SS, (w - 2) * SS, (h - 2) * SS), 6 * SS, outline=255, width=3 * SS)
    d.rounded_rectangle((7 * SS, 7 * SS, (w - 7) * SS, (h - 7) * SS), 3 * SS, outline=255, width=1 * SS)
    worn = noise((w * SS // 3, h * SS // 3), 21, 0, 255).resize(m.size).filter(ImageFilter.GaussianBlur(SS))
    worn = worn.point(lambda v: 255 if v > 108 else max(0, int((v - 88) * 12)))
    save(solid(ImageChops.multiply(m, worn), (255, 255, 255)), "stamp_frame", w, h)


def stamp_ink():
    """The worn ink of a stamp's letters: white with gaps, laid over the stamp text as a mask-like veil (tinted paper)."""
    w = h = 64
    n = noise((w * SS // 2, h * SS // 2), 33, 0, 255).resize((w * SS, h * SS)).filter(ImageFilter.GaussianBlur(SS * 0.8))
    n = n.point(lambda v: 255 if v < 100 else 0)
    save(solid(n, (255, 255, 255)), "stamp_ink", w, h)


def box():
    """A form box: a white fill and a 1 px ink rim (tint changes the fill; the rim stays dark). 9-slice 4."""
    w = h = 16
    im = canvas(w, h, INK + (255,))
    ImageDraw.Draw(im).rectangle((1 * SS, 1 * SS, (w - 1) * SS - 1, (h - 1) * SS - 1), fill=(255, 255, 255, 255))
    save(im, "box", w, h)


def outline():
    """An empty frame: a 1.5 px rim, white (tinted ink), nothing inside. 9-slice 4."""
    w = h = 16
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rectangle((0, 0, w * SS - 1, h * SS - 1), outline=255, width=int(1.5 * SS))
    save(solid(m, (255, 255, 255)), "outline", w, h)


def button():
    """A paper button: white fill (tinted: paper, a highlighter yellow under the mouse), a 1.5 px ink rim, rounded 4 px,
    a thicker rim along the bottom (it stands off the sheet). 9-slice 8."""
    w = h = 32
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, w * SS - 1, h * SS - 1), 4 * SS, fill=INK + (255,))
    d.rounded_rectangle((int(1.5 * SS), int(1.5 * SS), w * SS - int(1.5 * SS) - 1, h * SS - int(3 * SS) - 1), int(3 * SS), fill=(255, 255, 255, 255))
    save(im, "button", w, h)


def checkbox():
    """A form checkbox: a 2 px ink square (white, tinted ink)."""
    w = h = 24
    m = Image.new("L", (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rectangle((2 * SS, 2 * SS, (w - 2) * SS, (h - 2) * SS), outline=255, width=2 * SS)
    save(solid(m, (255, 255, 255)), "checkbox", w, h)


def tick():
    """A pen tick that runs out of the box a little (white, tinted pen blue / ink)."""
    w = h = 32
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    pts = [(5, 17), (9, 21), (13, 26), (19, 15), (25, 7), (30, 2)]
    for (x0, y0), (x1, y1), wd in zip(pts, pts[1:], (3.2, 3.6, 3.4, 3.0, 2.4)):
        d.line(((x0 * SS, y0 * SS), (x1 * SS, y1 * SS)), fill=255, width=int(wd * SS))
        d.ellipse(((x1 - wd / 2) * SS, (y1 - wd / 2) * SS, (x1 + wd / 2) * SS, (y1 + wd / 2) * SS), fill=255)
    save(solid(m, (255, 255, 255)), "tick", w, h)


def cross():
    """A pen cross (a struck out box): white, tinted."""
    w = h = 32
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    d.line(((6 * SS, 6 * SS), (26 * SS, 26 * SS)), fill=255, width=int(3.2 * SS))
    d.line(((26 * SS, 7 * SS), (7 * SS, 25 * SS)), fill=255, width=int(3 * SS))
    save(solid(m, (255, 255, 255)), "cross", w, h)


def highlight():
    """A highlighter stroke: a band with streaks along it and soft, slanted ends (white, tinted the marker's colour).
    9-slice 10 left and right."""
    w, h = 64, 20
    m = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(m)
    d.polygon([(3 * SS, 1 * SS), (w * SS, 1 * SS), ((w - 3) * SS, (h - 1) * SS), (0, (h - 1) * SS)], fill=230)
    streak = Image.new("L", (1, h * SS))
    rnd = random.Random(5)
    streak.putdata([int(200 + 55 * rnd.random()) for _ in range(h * SS)])
    streak = streak.resize((w * SS, h * SS)).filter(ImageFilter.GaussianBlur(SS * 0.6))
    save(solid(ImageChops.multiply(m, streak), (255, 255, 255)), "highlight", w, h)


def keycap():
    """A light key cap of the kit: a pale face, a darker lip along the bottom, an ink rim. 9-slice 10 (13 at the bottom)."""
    w = h = 40
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, w * SS - 1, h * SS - 1), 7 * SS, fill=(48, 50, 55, 255))
    d.rounded_rectangle((int(1.5 * SS), int(1.5 * SS), w * SS - int(1.5 * SS) - 1, h * SS - int(1.5 * SS) - 1), 6 * SS, fill=(176, 176, 170, 255))
    d.rounded_rectangle((int(1.5 * SS), int(1.5 * SS), w * SS - int(1.5 * SS) - 1, h * SS - int(5 * SS) - 1), 6 * SS, fill=(236, 235, 230, 255))
    d.rounded_rectangle((3 * SS, 3 * SS, w * SS - 3 * SS - 1, int(10 * SS)), 4 * SS, fill=(246, 245, 241, 255))  # a light top
    save(im, "keycap", w, h)


def pin():
    """A red push pin seen from above: the head with a glint and its shadow to the lower right."""
    w = h = 40
    im = canvas(w, h)
    sh = Image.new("L", im.size, 0)
    ImageDraw.Draw(sh).ellipse((12 * SS, 13 * SS, 36 * SS, 37 * SS), fill=110)
    im = Image.alpha_composite(im, solid(sh.filter(ImageFilter.GaussianBlur(2.5 * SS)), (0, 0, 0)))
    d = ImageDraw.Draw(im)
    d.ellipse((6 * SS, 6 * SS, 32 * SS, 32 * SS), fill=(150, 34, 28, 255))
    d.ellipse((7 * SS, 7 * SS, 30 * SS, 30 * SS), fill=(205, 58, 46, 255))
    d.ellipse((11 * SS, 10 * SS, 21 * SS, 18 * SS), fill=(240, 140, 128, 255))
    save(im, "pin", w, h)


def sticker():
    """A round sticker: a white disc with a faint rim and a soft shine (tinted the lamp's colour), its shadow under it."""
    w = h = 64
    im = canvas(w, h)
    sh = Image.new("L", im.size, 0)
    ImageDraw.Draw(sh).ellipse((7 * SS, 9 * SS, 59 * SS, 61 * SS), fill=90)
    im = Image.alpha_composite(im, solid(sh.filter(ImageFilter.GaussianBlur(2 * SS)), (0, 0, 0)))
    d = ImageDraw.Draw(im)
    d.ellipse((5 * SS, 5 * SS, 57 * SS, 57 * SS), fill=(210, 210, 210, 255))
    d.ellipse((6 * SS, 6 * SS, 56 * SS, 56 * SS), fill=(255, 255, 255, 255))
    shine = Image.new("L", im.size, 0)
    ImageDraw.Draw(shine).ellipse((14 * SS, 11 * SS, 34 * SS, 24 * SS), fill=70)
    im = Image.alpha_composite(im, solid(shine.filter(ImageFilter.GaussianBlur(3 * SS)), (255, 255, 255)))
    save(im, "sticker", w, h)


def graph():
    """Graph paper to tile: faint lines every 4 px, stronger every 16 (white lines on clear, tinted rule blue)."""
    w = h = 32
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    for i in range(0, w, 4):
        v = 255 if i % 16 == 0 else 110
        d.line(((i, 0), (i, h)), fill=v)
        d.line(((0, i), (w, i)), fill=v)
    im = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    im.putalpha(m)
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, "graph.png"))


def lines():
    """Ruled lines to tile vertically: one faint line in 22 px (white, tinted rule blue)."""
    w, h = 8, 22
    im = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    ImageDraw.Draw(im).line(((0, h - 1), (w, h - 1)), fill=(255, 255, 255, 255))
    os.makedirs(OUT, exist_ok=True)
    im.save(os.path.join(OUT, "lines.png"))


# ---------------------------------------------------------------------------------------------- the safe's dial on paper

def dial_face():
    """The dial printed on a paper disc: two ink rings and a band for the ticks (the view adds ticks and numbers)."""
    w = h = 256
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    c = w * SS / 2
    def ring(r, fill=None, outline=None, width=0):
        d.ellipse((c - r * SS, c - r * SS, c + r * SS, c + r * SS), fill=fill, outline=outline, width=width)
    ring(127, fill=(36, 38, 43, 255))                    # the ink edge
    ring(124, fill=(246, 242, 232, 255))                 # the paper
    ring(117, outline=INK + (255,), width=int(1.5 * SS))
    ring(66, outline=INK + (255,), width=int(1.5 * SS))
    ring(62, outline=(140, 145, 152, 255), width=int(1 * SS))
    save(im, "dial_face", w, h)


def dial_knob():
    """The knob: an ink disc with a fluted rim and a paper notch up (it turns with the dial)."""
    w = h = 128
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    c = w * SS / 2
    r0, r1 = 62 * SS, 56 * SS
    pts = []
    for i in range(96):
        a = i / 96 * math.tau
        r = r0 if i % 2 == 0 else r1
        pts.append((c + r * math.cos(a), c + r * math.sin(a)))
    d.polygon(pts, fill=(40, 42, 47, 255))
    d.ellipse((c - 50 * SS, c - 50 * SS, c + 50 * SS, c + 50 * SS), fill=(58, 61, 67, 255))
    d.ellipse((c - 44 * SS, c - 44 * SS, c + 44 * SS, c + 44 * SS), fill=(46, 48, 54, 255))
    d.rounded_rectangle((c - 3 * SS, c - 58 * SS, c + 3 * SS, c - 38 * SS), 2 * SS, fill=(243, 239, 228, 255))
    save(im, "dial_knob", w, h)


def pointer():
    """The dial's pointer: a stamp-red triangle pointing down with an ink rim."""
    w = h = 32
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    d.polygon([(3 * SS, 4 * SS), (29 * SS, 4 * SS), (16 * SS, 29 * SS)], fill=INK + (255,))
    d.polygon([(7 * SS, 6.5 * SS), (25 * SS, 6.5 * SS), (16 * SS, 24 * SS)], fill=(194, 59, 46, 255))
    save(im, "pointer", w, h)


if __name__ == "__main__":
    for f in (paper, card, shadow, sticky, tape, clip, stamp_frame, stamp_ink, box, outline, button, checkbox, tick, cross,
              highlight, keycap, pin, sticker, graph, lines, dial_face, dial_knob, pointer):
        f()
    print("ok", OUT)
