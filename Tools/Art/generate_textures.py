"""Tileable finish textures for the visual styles (floors and walls).

Run:  python Tools/Art/generate_textures.py            (writes Assets/_Game/Art/Textures/Finishes/*.png)

Deterministic (fixed seeds). Every texture is mostly light grey detail: the material's colour tints it, so one texture
serves several styles. *_glow.png are emission masks (white lines on black). Sizes in metres are in FINISH_TILES and
must match AssetBuilder.Styles (the tiling of a material = surface size / tile size).
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

N = 512
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "Finishes")


def rng(seed):
    return np.random.default_rng(seed)


def noise(seed, scale, octaves=4):
    """Tileable value noise in 0..1: each octave is a random grid tiled 3 x 3, upsampled, and the middle tile kept."""
    r = rng(seed)
    out = np.zeros((N, N))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cells = max(2, int(scale * (2 ** o)))
        grid = np.tile(r.random((cells, cells)), (3, 3))
        img = Image.fromarray((grid * 255).astype(np.uint8)).resize((3 * N, 3 * N), Image.BICUBIC)
        a = np.asarray(img, dtype=np.float64)[N:2 * N, N:2 * N] / 255.0
        out += a * amp
        total += amp
        amp *= 0.5
    out /= total
    return (out - out.min()) / (out.max() - out.min() + 1e-9)


def save(name, arr):
    os.makedirs(OUT, exist_ok=True)
    arr = np.clip(arr, 0, 1)
    if arr.ndim == 2:
        arr = np.stack([arr] * 3, -1)
    Image.fromarray((arr * 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"))
    print("wrote", name)


def lines_mask(draw_fn):
    img = Image.new("L", (N, N), 0)
    draw_fn(ImageDraw.Draw(img))
    return np.asarray(img, dtype=np.float64) / 255.0


# --------------------------------------------------------------------------- floors

def planks():
    """1 m: five boards 20 cm wide, staggered butt joints, grain."""
    grain = noise(1, 3)
    streak = noise(2, 1, 2)
    y = np.arange(N)[:, None]
    board = (y * 5) // N
    tone = rng(3).random(5)[board] * 0.18
    fine = np.asarray(Image.fromarray((rng(4).random((N, 8)) * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC), dtype=np.float64) / 255
    a = 0.72 + tone + (grain - 0.5) * 0.12 + (fine.T - 0.5) * 0.08 + (streak - 0.5) * 0.06
    seams = lines_mask(lambda d: [d.line([(0, k * N // 5), (N, k * N // 5)], fill=255, width=3) for k in range(5)]
                       + [d.line([((k * 0.37 + 0.15) % 1 * N, k * N // 5), ((k * 0.37 + 0.15) % 1 * N, (k + 1) * N // 5)], fill=255, width=3) for k in range(5)])
    save("planks", a * (1 - 0.55 * seams))


def parquet():
    """1 m basket weave: 25 cm squares of three 8 cm blocks, turned 90 degrees square by square."""
    y, x = np.mgrid[0:N, 0:N]
    q = N // 4
    sq = ((x // q) + (y // q)) % 2
    u = np.where(sq == 0, x % q, y % q)
    strip = (u * 3) // q
    tone = rng(5).random((4, 4, 3))[(y // q) % 4, (x // q) % 4, strip] * 0.16
    grain = noise(30, 6)
    seam_strip = ((u * 3) % q) < 3
    seam_sq = ((x % q) < 3) | ((y % q) < 3)
    a = 0.7 + tone + (grain - 0.5) * 0.1
    save("parquet", a * (1 - 0.5 * (seam_strip | seam_sq)))


def checker():
    """1 m: 2 x 2 squares of 50 cm, light and dark, with worn noise."""
    y, x = np.mgrid[0:N, 0:N]
    c = ((x * 2 // N) + (y * 2 // N)) % 2
    wear = noise(6, 6)
    a = np.where(c == 0, 0.9, 0.18) + (wear - 0.5) * 0.08
    grout = lines_mask(lambda d: [d.line([(0, k * N // 2), (N, k * N // 2)], fill=255, width=2) for k in range(3)]
                       + [d.line([(k * N // 2, 0), (k * N // 2, N)], fill=255, width=2) for k in range(3)])
    save("checker", a * (1 - 0.3 * grout))


def marble():
    """1 m: four 50 cm slabs with veins."""
    n = noise(7, 2, 5)
    y, x = np.mgrid[0:N, 0:N] / N
    # diagonal veins bent by noise (period 1 in x + y keeps it tileable)
    veins = np.abs(np.sin(((x + y) * 2 + n * 2.5) * math.pi))
    veins = (1 - np.clip(veins * 9, 0, 1)) * 0.55
    a = 0.9 - veins * 0.35 + (noise(9, 8) - 0.5) * 0.05
    grout = lines_mask(lambda d: [d.line([(0, k * N // 2), (N, k * N // 2)], fill=255, width=2) for k in range(3)]
                       + [d.line([(k * N // 2, 0), (k * N // 2, N)], fill=255, width=2) for k in range(3)])
    save("marble", a * (1 - 0.35 * grout))


def bigtiles():
    """1 m: soft two-tone 50 cm tiles with light grout (toon floors)."""
    y, x = np.mgrid[0:N, 0:N]
    c = ((x * 2 // N) + (y * 2 // N)) % 2
    a = np.where(c == 0, 0.95, 0.8) + (noise(31, 3) - 0.5) * 0.04
    grout = lines_mask(lambda d: [d.line([(0, k * N // 2), (N, k * N // 2)], fill=255, width=6) for k in range(3)]
                       + [d.line([(k * N // 2, 0), (k * N // 2, N)], fill=255, width=6) for k in range(3)])
    save("bigtiles", a * (1 - 0.18 * grout))


def diamondplate():
    """50 cm: raised lozenges on steel, rust blotches."""
    y, x = np.mgrid[0:N, 0:N] / N * 8
    u, v = (x + y) % 1 - 0.5, (x - y) % 1 - 0.5
    bump = np.clip(1 - (np.abs(u) * 6 + np.abs(v) * 1.6), 0, 1) + np.clip(1 - (np.abs(v) * 6 + np.abs(u) * 1.6), 0, 1)
    rust = noise(10, 3)
    a = 0.55 + bump * 0.3 + (noise(11, 16) - 0.5) * 0.08 - np.clip(rust - 0.55, 0, 1) * 0.6
    save("diamondplate", a)


def carpet():
    """1 m office carpet tiles (25 cm) with fibre noise."""
    f = rng(12).random((N, N))
    f = np.asarray(Image.fromarray((f * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)), dtype=np.float64) / 255
    y, x = np.mgrid[0:N, 0:N]
    tile = ((x * 4 // N) + (y * 4 // N)) % 2
    a = 0.62 + (f - 0.5) * 0.35 + tile * 0.05
    seams = lines_mask(lambda d: [d.line([(0, k * N // 4), (N, k * N // 4)], fill=255, width=2) for k in range(5)]
                       + [d.line([(k * N // 4, 0), (k * N // 4, N)], fill=255, width=2) for k in range(5)])
    save("carpet", a * (1 - 0.25 * seams))


def concrete():
    """2 m: blotchy concrete with a formwork seam and tie holes."""
    a = 0.7 + (noise(13, 3) - 0.5) * 0.25 + (noise(14, 24, 2) - 0.5) * 0.1
    marks = lines_mask(lambda d: [d.line([(0, N // 2), (N, N // 2)], fill=255, width=2)]
                       + [d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=255) for x in (N // 4, 3 * N // 4) for y in (N // 4, 3 * N // 4)])
    save("concrete", a * (1 - 0.3 * marks))


def gridtiles():
    """1 m: dark tiles of 50 cm; the joints glow (grid_glow)."""
    a = 0.25 + (noise(15, 6) - 0.5) * 0.05
    glow = lines_mask(lambda d: [d.line([(0, k * N // 2), (N, k * N // 2)], fill=255, width=4) for k in range(3)]
                      + [d.line([(k * N // 2, 0), (k * N // 2, N)], fill=255, width=4) for k in range(3)])
    save("gridtiles", a * (1 - glow) + glow * 0.9)
    save("gridtiles_glow", glow)


# --------------------------------------------------------------------------- walls

def damask():
    """1 m wallpaper: stripes and a repeating ornament."""
    y, x = np.mgrid[0:N, 0:N]
    stripes = (np.sin(x / N * math.pi * 2 * 8) > 0.6).astype(float) * 0.06
    img = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(img)
    for cx, cy in ((N // 4, N // 4), (3 * N // 4, 3 * N // 4), (3 * N // 4, N // 4 - N), (N // 4, 3 * N // 4)):
        for oy in (-N, 0, N):
            for ox in (-N, 0, N):
                x0, y0 = cx + ox, cy + oy
                d.ellipse([x0 - 40, y0 - 70, x0 + 40, y0 + 70], outline=255, width=6)
                d.ellipse([x0 - 18, y0 - 30, x0 + 18, y0 + 30], fill=255)
                d.line([(x0, y0 - 100), (x0, y0 + 100)], fill=255, width=4)
                d.arc([x0 - 80, y0 - 40, x0, y0 + 40], 270, 90, fill=255, width=5)
                d.arc([x0, y0 - 40, x0 + 80, y0 + 40], 90, 270, fill=255, width=5)
    orn = np.asarray(img.filter(ImageFilter.GaussianBlur(1)), dtype=np.float64) / 255
    save("damask", 0.62 + stripes + orn * 0.22 + (noise(16, 4) - 0.5) * 0.05)


def boards():
    """1 m: vertical boards 12.5 cm wide (ship planking, shed walls)."""
    x = np.arange(N)[None, :]
    board = (x * 8) // N
    tone = rng(17).random(8)[board] * 0.15
    grain = np.asarray(Image.fromarray((rng(18).random((8, N)) * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC), dtype=np.float64) / 255
    seams = lines_mask(lambda d: [d.line([(k * N // 8, 0), (k * N // 8, N)], fill=255, width=4) for k in range(9)]
                       + [d.line([(k * N // 8, (k * 0.41 % 1) * N), ((k + 1) * N // 8, (k * 0.41 % 1) * N)], fill=255, width=3) for k in range(8)])
    save("boards", (0.72 + tone + (grain - 0.5) * 0.12) * (1 - 0.6 * seams))


def brick():
    """1 m: running bond, 25 x 6.5 cm bricks."""
    rows = 15
    img = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(img)
    r = rng(19)
    h = N / rows
    w = N / 4
    tones = np.zeros((N, N))
    for j in range(rows):
        off = (j % 2) * w / 2
        for i in range(-1, 5):
            x0 = i * w + off
            t = r.random()
            d.rectangle([x0, j * h, x0 + w, (j + 1) * h], outline=255, width=3)
            xs, xe = int(max(0, x0)), int(min(N, x0 + w))
            if xe > xs:
                tones[int(j * h):int((j + 1) * h), xs:xe] = t
    mortar = np.asarray(img, dtype=np.float64) / 255
    a = 0.62 + tones * 0.18 + (noise(20, 12) - 0.5) * 0.1
    save("brick", a * (1 - mortar) + mortar * 0.85)


def corrugated():
    """1 m: corrugated sheet, 7.5 cm pitch, streaks of dirt."""
    x = np.arange(N)[None, :] / N
    ridges = 0.5 + 0.5 * np.sin(x * math.pi * 2 * 13.3)
    dirt = noise(21, 2)
    streaks = np.asarray(Image.fromarray((rng(22).random((4, 64)) * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC), dtype=np.float64) / 255
    save("corrugated", np.broadcast_to(0.55 + ridges * 0.3, (N, N)) - dirt * 0.15 - (streaks - 0.5) * 0.1)


def plaster():
    """2 m: painted plaster, faint stains and cracks."""
    a = 0.86 + (noise(23, 3) - 0.5) * 0.08 + (noise(24, 30, 2) - 0.5) * 0.04
    cracks = lines_mask(lambda d: [d.line([(x, y), (x + dx, y + dy)], fill=255, width=1) for x, y, dx, dy in
                                   ((90, 60, 30, 45), (120, 105, -10, 40), (380, 300, 25, -20), (405, 280, 10, -35))])
    save("plaster", a - cracks * 0.2)


def panels():
    """2 m: big white wall panels (1 m) with thin shadow gaps."""
    a = 0.92 + (noise(25, 4) - 0.5) * 0.03
    gaps = lines_mask(lambda d: [d.line([(0, k * N // 2), (N, k * N // 2)], fill=255, width=3) for k in range(3)]
                      + [d.line([(k * N // 2, 0), (k * N // 2, N)], fill=255, width=3) for k in range(3)])
    save("panels", a * (1 - 0.5 * gaps))


def circuit():
    """1 m: dark panel with circuit traces that glow (circuit_glow)."""
    r = rng(26)
    img = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(img)
    for _ in range(40):
        x, y = int(r.integers(0, 16)) * 32, int(r.integers(0, 16)) * 32
        for _ in range(int(r.integers(2, 6))):
            dx, dy = [(32, 0), (0, 32), (32, 32), (-32, 0), (0, -32)][int(r.integers(0, 5))]
            n = int(r.integers(1, 4))
            x2, y2 = x + dx * n, y + dy * n
            for ox in (-N, 0, N):
                for oy in (-N, 0, N):
                    d.line([(x + ox, y + oy), (x2 + ox, y2 + oy)], fill=255, width=3)
            x, y = x2 % N, y2 % N
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                d.ellipse([x + ox - 6, y + oy - 6, x + ox + 6, y + oy + 6], fill=255)
    glow = np.asarray(img, dtype=np.float64) / 255
    save("circuit", 0.22 + glow * 0.5 + (noise(27, 6) - 0.5) * 0.04)
    save("circuit_glow", glow)


def hatch():
    """1 m: pencil cross-hatching on grey (a scratchy surface)."""
    r = rng(28)
    img = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(img)
    for _ in range(900):
        x, y = r.random() * N, r.random() * N
        ang = (math.pi / 4 if r.random() < 0.5 else -math.pi / 4) + (r.random() - 0.5) * 0.3
        L = 12 + r.random() * 30
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                d.line([(x + ox, y + oy), (x + ox + math.cos(ang) * L, y + oy + math.sin(ang) * L)], fill=int(120 + r.random() * 135), width=1)
    s = np.asarray(img, dtype=np.float64) / 255
    save("hatch", 0.78 - s * 0.35 + (noise(29, 4) - 0.5) * 0.08)


if __name__ == "__main__":
    for fn in (planks, parquet, checker, bigtiles, marble, diamondplate, carpet, concrete, gridtiles,
               damask, boards, brick, corrugated, plaster, panels, circuit, hatch):
        fn()
