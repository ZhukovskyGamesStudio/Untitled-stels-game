"""Pictures on the screens of the things the player uses (SecurityTerminal's monitors): grey on black, the terminal tints
them by its kind (cameras, hacking, the storage door's access) and dims them once spent.

    python Tools/Art/generate_screens.py

Writes Assets/_Game/Art/Textures/Screens/screen_<kind>.png (256 x 160, deterministic): a screen's UVs run 0..1 over it
(Tools/Blender/terminals.py screen_uvs).
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

W, H, SS = 256, 160, 4
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Game", "Art", "Textures", "Screens")


def canvas():
    img = Image.new("L", (W * SS, H * SS), 8)
    return img, ImageDraw.Draw(img)


def finish(img, name):
    img = img.resize((W, H), Image.LANCZOS)
    glow = img.filter(ImageFilter.GaussianBlur(3))
    img = Image.blend(img, Image.eval(glow, lambda v: min(255, v * 2)), 0.25)
    px = img.load()
    for y in range(H):                                   # scanlines
        if y % 3 == 0:
            for x in range(W):
                px[x, y] = int(px[x, y] * 0.72)
    os.makedirs(OUT, exist_ok=True)
    img.convert("RGB").save(os.path.join(OUT, name + ".png"))
    print("wrote", name)


def frame(d, inset=6, level=70):
    s = SS
    d.rectangle([inset * s, inset * s, (W - inset) * s, (H - inset) * s], outline=level, width=2 * s)


def camera_glyph(d, cx, cy, size, level=235):
    """A security camera on its bracket: the box, the lens hood, the arm to the wall."""
    s = SS * size
    d.rounded_rectangle([cx - 30 * s, cy - 13 * s, cx + 22 * s, cy + 13 * s], radius=5 * s, fill=level)
    d.polygon([(cx + 22 * s, cy - 10 * s), (cx + 36 * s, cy - 16 * s), (cx + 36 * s, cy + 16 * s), (cx + 22 * s, cy + 10 * s)], fill=level)
    d.ellipse([cx + 24 * s, cy - 7 * s, cx + 34 * s, cy + 7 * s], fill=20)
    d.rectangle([cx - 8 * s, cy + 13 * s, cx - 2 * s, cy + 26 * s], fill=level)
    d.rectangle([cx - 22 * s, cy + 24 * s, cx + 12 * s, cy + 30 * s], fill=level)
    d.ellipse([cx - 24 * s, cy - 8 * s, cx - 16 * s, cy], fill=20)                 # the red light, dark here


def screen_cameras():
    img, d = canvas()
    s = SS
    rnd = random.Random(7)
    # four camera feeds: a room in perspective each, a REC dot, a time code bar
    for i, (x0, y0) in enumerate(((10, 10), (130, 10), (10, 84), (130, 84))):
        x1, y1 = x0 + 116, y0 + 66
        d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], fill=26, outline=90, width=s)
        vx, vy = x0 + 58 + rnd.randint(-15, 15), y0 + 30 + rnd.randint(-6, 6)
        for cx, cy in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
            d.line([cx * s, cy * s, vx * s, vy * s], fill=60, width=s)
        d.rectangle([(vx - 20) * s, (vy - 12) * s, (vx + 20) * s, (vy + 12) * s], outline=70, width=s)
        d.ellipse([(x0 + 6) * s, (y0 + 6) * s, (x0 + 12) * s, (y0 + 12) * s], fill=200 if i % 2 == 0 else 120)
        d.rectangle([(x1 - 40) * s, (y1 - 9) * s, (x1 - 6) * s, (y1 - 5) * s], fill=110)
    # the camera in the middle, on a dark plate
    d.ellipse([(W / 2 - 40) * s, (H / 2 - 30) * s, (W / 2 + 40) * s, (H / 2 + 30) * s], fill=14, outline=120, width=2 * s)
    camera_glyph(d, W / 2 * s, (H / 2 - 4) * s, 0.9)
    finish(img, "screen_cameras")


def screen_hack():
    img, d = canvas()
    s = SS
    rnd = random.Random(11)
    # lines of code down the left, a progress bar along the bottom
    y = 12
    while y < H - 26:
        x = 12 + rnd.choice((0, 0, 8, 16))
        while x < 120:
            w = rnd.randint(6, 22)
            d.rectangle([x * s, y * s, (x + w) * s, (y + 4) * s], fill=rnd.choice((90, 120, 160)))
            x += w + 4
        y += 9
    d.rectangle([12 * s, (H - 20) * s, (W - 12) * s, (H - 12) * s], outline=140, width=s)
    d.rectangle([14 * s, (H - 18) * s, (W * 0.62) * s, (H - 14) * s], fill=200)
    # a lightning bolt over a chip on the right
    cx, cy = 190, 66
    d.rounded_rectangle([(cx - 38) * s, (cy - 38) * s, (cx + 38) * s, (cy + 38) * s], radius=6 * s, outline=150, width=2 * s)
    for k in range(5):
        for side in (-1, 1):
            xa, xb = sorted((cx + side * 38, cx + side * 46))
            ya, yb = sorted((cy + side * 38, cy + side * 46))
            d.rectangle([xa * s, (cy - 30 + k * 15) * s, xb * s, (cy - 27 + k * 15) * s], fill=150)
            d.rectangle([(cx - 30 + k * 15) * s, ya * s, (cx - 27 + k * 15) * s, yb * s], fill=150)
    pts = [(0.08, -0.42), (-0.2, 0.06), (0.0, 0.06), (-0.08, 0.42), (0.22, -0.08), (0.02, -0.08), (0.12, -0.42)]
    d.polygon([((cx + px * 80) * s, (cy + py * 80) * s) for px, py in pts], fill=240)
    finish(img, "screen_hack")


def screen_doors():
    img, d = canvas()
    s = SS
    # a padlock on the left, a keypad on the right, an ACCESS bar under them
    cx, cy = 80, 72
    d.arc([(cx - 24) * s, (cy - 46) * s, (cx + 24) * s, (cy + 2) * s], 180, 360, fill=230, width=7 * s)
    d.rectangle([(cx - 24) * s, (cy - 22) * s, (cx - 17) * s, (cy - 4) * s], fill=230)
    d.rectangle([(cx + 17) * s, (cy - 22) * s, (cx + 24) * s, (cy - 4) * s], fill=230)
    d.rounded_rectangle([(cx - 34) * s, (cy - 6) * s, (cx + 34) * s, (cy + 44) * s], radius=6 * s, fill=230)
    d.ellipse([(cx - 7) * s, (cy + 8) * s, (cx + 7) * s, (cy + 22) * s], fill=20)
    d.rectangle([(cx - 3) * s, (cy + 18) * s, (cx + 3) * s, (cy + 34) * s], fill=20)
    for r in range(4):
        for c in range(3):
            x, y = 158 + c * 26, 24 + r * 24
            d.rounded_rectangle([x * s, y * s, (x + 20) * s, (y + 18) * s], radius=3 * s, outline=150, width=s, fill=40 if (r, c) != (1, 1) else 180)
    d.rectangle([20 * s, (H - 22) * s, (W - 20) * s, (H - 12) * s], fill=120)
    finish(img, "screen_doors")


def screen_vault(opened):
    """The vault console's status strip (wide, 256 x 80): a padlock — shut or open — and a row of bars."""
    w, h = 256, 80
    img = Image.new("L", (w * SS, h * SS), 8)
    d = ImageDraw.Draw(img)
    s = SS
    cx, cy = 52, 44
    lift = -10 if opened else 0
    d.arc([(cx - 14) * s, (cy - 34 + lift) * s, (cx + 14) * s, (cy - 6 + lift) * s], 180, 360, fill=235, width=5 * s)
    d.rectangle([(cx - 14) * s, (cy - 20 + lift) * s, (cx - 9) * s, (cy - 6) * s], fill=235)
    if not opened:
        d.rectangle([(cx + 9) * s, (cy - 20) * s, (cx + 14) * s, (cy - 6) * s], fill=235)
    d.rounded_rectangle([(cx - 20) * s, (cy - 8) * s, (cx + 20) * s, (cy + 24) * s], radius=4 * s, fill=235)
    d.ellipse([(cx - 4) * s, (cy + 1) * s, (cx + 4) * s, (cy + 9) * s], fill=20)
    d.rectangle([(cx - 2) * s, (cy + 6) * s, (cx + 2) * s, (cy + 16) * s], fill=20)
    for k in range(6):
        x = 96 + k * 25
        level = 220 if opened or k < 2 else 70
        d.rectangle([x * s, 28 * s, (x + 18) * s, 52 * s], fill=level)
    img = img.resize((w, h), Image.LANCZOS)
    os.makedirs(OUT, exist_ok=True)
    name = "screen_vault_open" if opened else "screen_vault_locked"
    img.convert("RGB").save(os.path.join(OUT, name + ".png"))
    print("wrote", name)


if __name__ == "__main__":
    screen_vault(False)
    screen_vault(True)
    screen_cameras()
    screen_hack()
    screen_doors()
