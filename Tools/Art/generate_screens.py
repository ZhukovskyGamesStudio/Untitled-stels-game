"""Pictures on the screens of the things the player uses (SecurityTerminal's monitors): grey on black, the terminal tints
them by its kind (cameras, hacking, the storage door's access, the building's map) and dims them once spent.

    python Tools/Art/generate_screens.py

Writes Assets/_Game/Art/Textures/Screens/screen_<kind>.png (256 x 160, deterministic): a screen's UVs run 0..1 over it
(Tools/Blender/terminals.py screen_uvs).
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

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


def crate(d, x, y, w, h, level=200):
    """A crate: a box with a rim, two straps and a plank line."""
    s = SS
    d.rectangle([x * s, y * s, (x + w) * s, (y + h) * s], fill=level)
    d.rectangle([(x + 3) * s, (y + 3) * s, (x + w - 3) * s, (y + h - 3) * s], fill=level - 70)
    for k in (0.3, 0.7):
        d.rectangle([(x + w * k - 2) * s, y * s, (x + w * k + 2) * s, (y + h) * s], fill=level)
    d.line([(x + 3) * s, (y + h / 2) * s, (x + w - 3) * s, (y + h / 2) * s], fill=level - 30, width=s)


def padlock(d, cx, cy, size, opened, level=240):
    """A padlock (its body's middle at cx, cy): shut, or its shackle swung up out of the body."""
    s = SS * size
    lift = 12 * s if opened else 0
    box = [cx - 14 * s, cy - 34 * s - lift, cx + 14 * s, cy - 6 * s - lift]
    d.arc(box, 180, 360, fill=level, width=int(5 * s))
    d.rectangle([cx - 14 * s, cy - 20 * s - lift, cx - 9 * s, cy - 4 * s], fill=level)
    if not opened:
        d.rectangle([cx + 9 * s, cy - 20 * s, cx + 14 * s, cy - 4 * s], fill=level)
    else:
        d.rectangle([cx + 9 * s, cy - 20 * s - lift, cx + 14 * s, cy - 16 * s - lift + 6 * s], fill=level)
    d.rounded_rectangle([cx - 20 * s, cy - 8 * s, cx + 20 * s, cy + 22 * s], radius=int(4 * s), fill=level)
    d.ellipse([cx - 4 * s, cy + 1 * s, cx + 4 * s, cy + 9 * s], fill=20)
    d.rectangle([cx - 2 * s, cy + 6 * s, cx + 2 * s, cy + 15 * s], fill=20)


def screen_doors(opened=False):
    """The storage door's station: the storeroom — a doorway with crates stacked behind it — and its lock. Locked: the
    roller door is down (its slats) with the crates marked on it, a shut padlock beside it. Open: the door is rolled up under the lintel,
    the crates stand in the open doorway, the padlock hangs open beside it with a tick."""
    img, d = canvas()
    s = SS
    frame(d)
    # the doorway on the left two thirds: jambs, a lintel, the floor line
    x0, x1, y0, y1 = 28, 164, 22, 136
    d.rectangle([(x0 - 8) * s, (y0 - 8) * s, (x1 + 8) * s, y0 * s], fill=170)                       # lintel
    d.rectangle([(x0 - 8) * s, y0 * s, x0 * s, y1 * s], fill=170)                                   # jambs
    d.rectangle([x1 * s, y0 * s, (x1 + 8) * s, y1 * s], fill=170)
    d.rectangle([14 * s, y1 * s, 178 * s, (y1 + 4) * s], fill=110)                                  # floor
    # the crates inside: two below, one on top
    crate(d, x0 + 14, y1 - 46, 50, 46)
    crate(d, x0 + 70, y1 - 46, 50, 46)
    crate(d, x0 + 40, y1 - 88, 54, 42)
    if opened:
        d.rectangle([x0 * s, y0 * s, x1 * s, (y0 + 16) * s], fill=120)                              # the door, rolled up
        for k in range(3):
            d.line([x0 * s, (y0 + 4 + k * 5) * s, x1 * s, (y0 + 4 + k * 5) * s], fill=60, width=s)
    else:
        d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], fill=96)                                      # the door, down: slats
        for y in range(y0 + 6, y1, 9):
            d.line([x0 * s, y * s, x1 * s, y * s], fill=44, width=2 * s)
        d.rectangle([x0 * s, (y1 - 6) * s, x1 * s, y1 * s], fill=150)                               # its bottom bar
        # what is behind it, marked on the door: the same crates, dimmer
        crate(d, x0 + 20, y1 - 52, 44, 40, level=168)
        crate(d, x0 + 72, y1 - 52, 44, 40, level=168)
        crate(d, x0 + 44, y1 - 90, 48, 36, level=168)
    # the lock on the right
    padlock(d, 214 * s, 78 * s, 1.5, opened)
    if opened:
        d.line([196 * s, 126 * s, 208 * s, 138 * s, 234 * s, 112 * s], fill=240, width=6 * s, joint="curve")   # a tick
    else:
        d.rectangle([188 * s, 128 * s, 240 * s, 136 * s], fill=120)                                 # an access bar
    finish(img, "screen_doors_open" if opened else "screen_doors")


def screen_static():
    """A screen left on with no signal: grey noise in fine horizontal streaks (256 x 256, tiles; the game makes it jump)."""
    rnd = random.Random(21)
    size = 256
    img = Image.new("L", (size, size))
    px = img.load()
    for y in range(size):
        band = 0.82 + 0.18 * math.sin(y * 2 * math.pi * 3 / size)          # three soft bands (tiles)
        x = 0
        while x < size:
            run = rnd.choice((1, 1, 1, 2, 2, 3))
            v = int(min(255, max(0, rnd.gauss(118, 62) * band)))
            for k in range(run):
                if x + k < size:
                    px[x + k, y] = v
            x += run
    img.convert("RGB").save(os.path.join(OUT, "screen_static.png"))
    print("wrote screen_static")


def screen_cracked():
    """Broken glass: a dead dark screen with an impact a little off its middle — jagged cracks running out to the
    edges, a few rings of short cracks round the impact, a crazed patch at it (512 x 512: about a metre of screen)."""
    rnd = random.Random(5)
    size, s = 512, 2
    img = Image.new("L", (size * s, size * s), 9)
    d = ImageDraw.Draw(img)
    cx, cy = 0.42 * size * s, 0.56 * size * s
    spokes = []
    n = 11
    for k in range(n):
        a = 2 * math.pi * (k + rnd.uniform(-0.3, 0.3)) / n
        pts, r = [(cx, cy)], 0.0
        while r < size * s * 0.75:
            r += rnd.uniform(18, 46) * s
            a += rnd.uniform(-0.16, 0.16)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        spokes.append(pts)
        d.line(pts, fill=rnd.randint(150, 215), width=rnd.choice((1, 2, 2)) * s)
        if rnd.random() < 0.6 and len(pts) > 3:                              # a branch
            j = rnd.randint(2, len(pts) - 2)
            b, br, bp = a + rnd.choice((-1, 1)) * rnd.uniform(0.4, 0.8), 0.0, [pts[j]]
            while br < 130 * s:
                br += rnd.uniform(14, 34) * s
                b += rnd.uniform(-0.2, 0.2)
                bp.append((pts[j][0] + br * math.cos(b), pts[j][1] + br * math.sin(b)))
            d.line(bp, fill=rnd.randint(110, 170), width=s)
    for ring in (2, 3, 5):                                                    # short cracks from spoke to spoke
        for k in range(n):
            a, b = spokes[k], spokes[(k + 1) % n]
            if ring < len(a) and ring < len(b) and rnd.random() < 0.75:
                d.line([a[ring], b[ring]], fill=rnd.randint(95, 150), width=s)
    for _ in range(26):                                                       # the crazed patch at the impact
        a, r = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 22 * s)
        p = (cx + r * math.cos(a), cy + r * math.sin(a))
        q = (p[0] + rnd.uniform(-12, 12) * s, p[1] + rnd.uniform(-12, 12) * s)
        d.line([p, q], fill=rnd.randint(140, 220), width=s)
    img = img.resize((size, size), Image.LANCZOS)
    img.convert("RGB").save(os.path.join(OUT, "screen_cracked.png"))
    print("wrote screen_cracked")


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


def screen_map():
    """The map terminal: a floor plan — rooms, doorways, a stair — with a "you are here" dot and a floor strip."""
    img, d = canvas()
    s = SS
    x0, y0, x1, y1 = 14, 12, 196, 148
    d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], fill=24)
    # the walls: the outline, a corridor across, rooms either side
    wall = 200
    d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], outline=wall, width=3 * s)
    cy0, cy1 = 70, 86
    d.line([x0 * s, cy0 * s, x1 * s, cy0 * s], fill=wall, width=2 * s)
    d.line([x0 * s, cy1 * s, x1 * s, cy1 * s], fill=wall, width=2 * s)
    for x in (70, 128, 160):
        d.line([x * s, y0 * s, x * s, cy0 * s], fill=wall, width=2 * s)
    for x in (58, 112, 150):
        d.line([x * s, cy1 * s, x * s, y1 * s], fill=wall, width=2 * s)
    # doorways: gaps in the corridor walls
    for x in (40, 96, 144, 178):
        d.rectangle([(x - 5) * s, (cy0 - 2) * s, (x + 5) * s, (cy0 + 2) * s], fill=24)
    for x in (30, 84, 130, 172):
        d.rectangle([(x - 5) * s, (cy1 - 2) * s, (x + 5) * s, (cy1 + 2) * s], fill=24)
    # a stair in a corner: steps
    for k in range(6):
        d.line([(162 + k * 5) * s, (cy1 + 6) * s, (162 + k * 5) * s, (cy1 + 26) * s], fill=120, width=s)
    # furniture blocks
    for bx, by, bw, bh in ((22, 22, 30, 14), (82, 24, 34, 10), (20, 104, 26, 26), (70, 100, 30, 12), (120, 120, 22, 16)):
        d.rectangle([bx * s, by * s, (bx + bw) * s, (by + bh) * s], fill=70)
    # you are here: a bright dot with a ring, in the corridor
    cx, cy = 100, 78
    d.ellipse([(cx - 9) * s, (cy - 9) * s, (cx + 9) * s, (cy + 9) * s], outline=235, width=2 * s)
    d.ellipse([(cx - 4) * s, (cy - 4) * s, (cx + 4) * s, (cy + 4) * s], fill=245)
    # the floors on the right: a stack of bars, one lit
    for k in range(4):
        y = 30 + k * 26
        d.rounded_rectangle([210 * s, y * s, 242 * s, (y + 18) * s], radius=3 * s, outline=140, width=s, fill=200 if k == 2 else 34)
    finish(img, "screen_map")


def font(size, bold=False):
    """A monospaced face for what a computer prints (Windows' Consolas; any other machine: PIL's own)."""
    for name in (("consolab.ttf",) if bold else ()) + ("consola.ttf", "DejaVuSansMono.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default(size)


def screen_nosignal():
    """A monitor with no computer behind it: its own little box on black (512 x 320; the game moves it about). The box is
    0.48 x 0.34 of the picture (ScreenLooks.BoxHalf)."""
    w, h = 512, 320
    img = Image.new("RGB", (w, h), (0, 0, 0))
    d = ImageDraw.Draw(img)
    bw, bh = 0.44 * w, 0.30 * h
    x0, y0 = (w - bw) / 2, (h - bh) / 2
    d.rounded_rectangle([x0, y0, x0 + bw, y0 + bh], radius=6, fill=(16, 40, 120), outline=(120, 150, 230), width=3)
    d.text((w / 2, y0 + bh * 0.38), "NO SIGNAL", font=font(34, True), fill=(235, 240, 255), anchor="mm")
    d.text((w / 2, y0 + bh * 0.74), "HDMI 1  -  entering power save", font=font(12), fill=(170, 190, 240), anchor="mm")
    img.save(os.path.join(OUT, "screen_nosignal.png"))
    print("wrote screen_nosignal")


def screen_boot():
    """A computer waiting at its boot menu (512 x 320): the firmware's lines, the list of drives with one picked, the keys."""
    w, h = 512, 320
    img = Image.new("RGB", (w, h), (2, 3, 5))
    d = ImageDraw.Draw(img)
    f, b = font(13), font(15, True)
    grey, white = (168, 174, 182), (236, 240, 245)
    y = 18
    for line in ("System firmware 4.12        Memory test: 16384 MB OK", "Keyboard ....... not found", "Disk 0 ......... no boot record", "Network ........ no link"):
        d.text((24, y), line, font=f, fill=grey)
        y += 17
    x0, y0, x1, y1 = 84, 106, w - 84, 262
    d.rectangle([x0, y0, x1, y1], fill=(10, 22, 74), outline=(200, 206, 216), width=2)
    d.text((w / 2, y0 + 18), "Please select boot device:", font=b, fill=white, anchor="mm")
    d.line([x0, y0 + 34, x1, y0 + 34], fill=(200, 206, 216), width=2)
    for k, item in enumerate(("Disk 0: system (no boot record)", "Removable drive", "Network boot", "Enter setup")):
        yy = y0 + 46 + k * 25
        if k == 0:
            d.rectangle([x0 + 10, yy - 2, x1 - 10, yy + 18], fill=(198, 204, 214))
        d.text((x0 + 20, yy), item, font=f, fill=(8, 14, 40) if k == 0 else white)
    d.text((w / 2, h - 34), "Up / Down: move   Enter: boot   Esc: defaults", font=f, fill=grey, anchor="mm")
    img.save(os.path.join(OUT, "screen_boot.png"))
    print("wrote screen_boot")


def screen_program():
    """The robot programming console's three monitors side by side, one picture three screens wide (poi.py
    model_programmer_console gives each monitor its third): a program listing with the running line, the four cube
    docks and how full each is, the arm with its joint angles. No words: bars, numbers of dots, shapes."""
    s = SS
    img = Image.new("RGB", (3 * W * s, H * s), (5, 9, 12))
    d = ImageDraw.Draw(img)
    cyan, dim, amber, green, red = (110, 220, 235), (40, 96, 110), (240, 180, 70), (110, 225, 130), (235, 90, 70)
    rnd = random.Random(23)

    def box(x0, y0, x1, y1, **kw):
        d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], **kw)

    for k in range(3):                                   # each screen's frame and title bar
        box(k * W + 5, 5, (k + 1) * W - 5, H - 5, outline=dim, width=s)
        box(k * W + 5, 5, (k + 1) * W - 5, 16, fill=dim)
        for i in range(3):
            box(k * W + 10 + i * 9, 8, k * W + 15 + i * 9, 13, fill=cyan if i == k else (20, 50, 58))

    # 1: the listing — line numbers as ticks, indented bars of code, the running line lit, a breakpoint
    y, line = 24, 0
    while y < H - 14:
        indent = rnd.choice((0, 0, 10, 10, 20))
        box(12, y + 1, 18, y + 3, fill=dim)
        running = line == 5
        if running:
            box(22, y - 2, W - 12, y + 6, fill=(60, 48, 16))
        if line == 9:
            d.ellipse([20 * s, (y - 1) * s, 26 * s, (y + 5) * s], fill=red)
        x = 30 + indent
        while x < W - 60 - rnd.randint(0, 80):
            w = rnd.randint(8, 30)
            box(x, y, x + w, y + 4, fill=amber if running else rnd.choice((cyan, cyan, dim, green)))
            x += w + 5
        y += 9
        line += 1

    # 2: the docks — four cubes in a row, a level in each, a mark under it: ready, charging, empty
    for i, (level, state) in enumerate(((1.0, "ok"), (0.62, "busy"), (1.0, "ok"), (0.0, "none"))):
        cx = W + 38 + i * 60
        colour = green if state == "ok" else amber if state == "busy" else dim
        box(cx - 22, 34, cx + 22, 98, outline=colour, width=2 * s)
        if level > 0:
            box(cx - 17, 93 - 54 * level, cx + 17, 93, fill=colour)
        for j in range(4):                                # the dock's number: dots
            if j <= i:
                d.ellipse([(cx - 15 + j * 9) * s, 106 * s, (cx - 9 + j * 9) * s, 112 * s], fill=cyan)
        if state == "ok":
            d.line([(cx - 10) * s, 128 * s, (cx - 3) * s, 136 * s, (cx + 11) * s, 121 * s], fill=green, width=3 * s, joint="curve")
        elif state == "busy":
            for j in range(3):
                box(cx - 13 + j * 10, 126, cx - 7 + j * 10, 132, fill=amber if j < 2 else dim)
        else:
            box(cx - 10, 128, cx + 10, 131, fill=dim)

    # 3: the arm — base, two links and a gripper, an arc and a tick scale at each joint, a target cross
    ox = 2 * W
    base, elbow, wrist = (ox + 62, 128), (ox + 112, 70), (ox + 178, 92)
    box(ox + 38, 128, ox + 86, 138, fill=dim)
    for a, b in ((base, elbow), (elbow, wrist)):
        d.line([a[0] * s, a[1] * s, b[0] * s, b[1] * s], fill=cyan, width=7 * s)
    for (jx, jy), (a0, a1) in ((base, (230, 310)), (elbow, (330, 60)), (wrist, (300, 40))):
        d.ellipse([(jx - 8) * s, (jy - 8) * s, (jx + 8) * s, (jy + 8) * s], fill=(5, 9, 12), outline=amber, width=2 * s)
        d.arc([(jx - 22) * s, (jy - 22) * s, (jx + 22) * s, (jy + 22) * s], a0, a1, fill=amber, width=2 * s)
    for side in (-1, 1):                                  # the gripper's two fingers
        d.line([wrist[0] * s, wrist[1] * s, (wrist[0] + 20) * s, (wrist[1] + side * 9) * s, (wrist[0] + 32) * s, (wrist[1] + side * 6) * s],
               fill=cyan, width=3 * s)
    tx, ty = ox + 226, 96
    box(tx - 12, ty - 12, tx + 12, ty + 12, outline=green, width=s)
    d.line([(tx - 18) * s, ty * s, (tx + 18) * s, ty * s], fill=green, width=s)
    d.line([tx * s, (ty - 18) * s, tx * s, (ty + 18) * s], fill=green, width=s)
    for i in range(12):                                   # a scale along the bottom
        box(ox + 18 + i * 19, 146, ox + 19 + i * 19, 150 if i % 3 else 153, fill=dim)

    img = img.resize((6 * W, 2 * H), Image.LANCZOS)      # (twice the terminals' size: these monitors are looked at up close)
    glow = img.filter(ImageFilter.GaussianBlur(6))
    img = Image.blend(img, Image.eval(glow, lambda v: min(255, v * 2)), 0.25)
    px = img.load()
    for y in range(2 * H):                                # scanlines
        if y % 6 < 2:
            for x in range(6 * W):
                r, g, b = px[x, y]
                px[x, y] = (int(r * 0.78), int(g * 0.78), int(b * 0.78))
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, "screen_program.png"))
    print("wrote screen_program")


if __name__ == "__main__":
    screen_vault(False)
    screen_vault(True)
    screen_cameras()
    screen_hack()
    screen_doors()
    screen_doors(opened=True)
    screen_static()
    screen_cracked()
    screen_nosignal()
    screen_boot()
    screen_map()
    screen_program()
