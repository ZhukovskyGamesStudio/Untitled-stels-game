"""The surfaces things are made of: tileable PBR textures of paint, plastic, laminate, cloth, leather, steel, wood…

Run:  python -I Tools/Art/fetch_surfaces.py              (once: the scans, CC0, into Tools/Art/cache/surfaces)
      python Tools/Art/generate_surfaces.py [names]      (writes Assets/_Game/Art/Textures/Surfaces/<name>/ and wear.png)
      python Tools/Art/generate_surfaces.py sheet x.png  (a contact sheet)

Most are made from scans, the plain ones (gloss paint, melamine, rubber, leaves, stone) from scratch. Every surface is a
folder: albedo.jpg (neutral round BASE: the material's colour tints it), normal.jpg (OpenGL, +Y up) and mask.png (R metal,
G occlusion, A smoothness). wear.png is laid over all of them by the Stealth/Surface shader, which also rubs the edges and
puts dirt into the corners: no wear is painted into a surface itself, and none of it is rust. How many metres one tile
covers is in AssetBuilder.Surfaces (the models' UVs are metres).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

N = int(os.environ.get("SURFACE_SIZE", "1024"))         # (2048 only for the scanned surfaces: the drawn ones take their relief from pixel steps)
BASE = 0.86
SCANS = os.environ.get("SURFACE_SCANS") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "cache", "surfaces")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "Surfaces")
Y, X = np.mgrid[0:N, 0:N] / N
FX = np.fft.fftfreq(N) * N
FXX, FYY = np.meshgrid(FX, FX)


def unit(a):
    return (a - a.min()) / (a.max() - a.min() + 1e-9)


def band(seed, lo, hi, sx=1.0, sy=1.0, fall=1.0):
    """Tileable noise (zero mean, unit deviation) of the frequencies lo..hi cycles a tile; sx/sy < 1 stretch it along x/y."""
    f = np.sqrt((FXX / sx) ** 2 + (FYY / sy) ** 2)
    keep = np.exp(-(f / hi) ** 4) * (1 - np.exp(-(f / lo) ** 4)) / np.maximum(f, 1.0) ** fall
    spec = np.fft.fft2(np.random.default_rng(seed).standard_normal((N, N))) * keep
    a = np.real(np.fft.ifft2(spec))
    return (a - a.mean()) / (a.std() + 1e-9)


def step(lo, hi, a):
    t = np.clip((a - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def scratches(seed, count, length, width=1.2, spread=3.14159, angle=0.0):
    """Thin straight scuffs (0..1), wrapped round the tile."""
    r = np.random.default_rng(seed)
    s = 2
    img = Image.new("L", (N * s, N * s), 0)
    d = ImageDraw.Draw(img)
    for _ in range(count):
        x, y = r.random(2) * N
        a = angle + (r.random() - 0.5) * spread
        n = length * N * (0.3 + 0.7 * r.random())
        x2, y2 = x + np.cos(a) * n, y + np.sin(a) * n
        bend = (r.random(2) - 0.5) * n * 0.12
        v = int(90 + 165 * r.random())
        for ox in (-N, 0, N):
            for oy in (-N, 0, N):
                p = [((x + ox) * s, (y + oy) * s), (((x + x2) / 2 + bend[0] + ox) * s, ((y + y2) / 2 + bend[1] + oy) * s),
                     ((x2 + ox) * s, (y2 + oy) * s)]
                d.line(p, fill=v, width=max(1, int(width * s)))
    return np.asarray(img.resize((N, N), Image.LANCZOS), dtype=np.float64) / 255.0


def blobs(seed, lo, hi, amount, soft=0.06, sx=1.0, sy=1.0, fall=1.0):
    """Patches covering about `amount` of the tile (0..1, soft edges)."""
    a = band(seed, lo, hi, sx, sy, fall)
    cut = np.quantile(a, 1 - amount)
    return step(cut - soft, cut + soft, a)


def save(name, albedo, height, smooth, metal=0.0, ao=None, strength=1.0):
    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    albedo = np.asarray(albedo, dtype=np.float64)
    if albedo.ndim == 2:
        albedo = np.stack([albedo] * 3, -1)
    Image.fromarray((np.clip(albedo, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(folder, "albedo.jpg"), quality=90)
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5 * strength
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5 * strength
    n = np.stack([-dx, dy, np.ones_like(dx)], -1)      # row 0 is the top: +Y is towards smaller rows
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8)).save(os.path.join(folder, "normal.jpg"), quality=92, subsampling=0)
    full = np.ones((N, N))
    ao = full if ao is None else ao
    mask = np.stack([full * metal, ao, full * 0, full * smooth], -1)
    Image.fromarray((np.clip(mask, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(folder, "mask.png"), optimize=True)
    print(f"wrote {name}: albedo {albedo.mean():.3f} smooth {np.mean(mask[..., 3]):.2f} metal {np.mean(mask[..., 0]):.2f}")


def tone(base, warm=0.0):
    """A near-grey colour from a grey field: warm > 0 a little yellow."""
    return np.stack([base * (1 + warm * 0.5), base, base * (1 - warm)], -1)


SMUDGE = dict(lo=2, hi=14, fall=1.2)


# ------------------------------------------------------------------------------------------------- clean surfaces

def gloss():
    """Enamel and lacquer: a mirror-smooth coat with a slow wave in it, fingermarks only in its shine."""
    wave = band(11, 3, 30, fall=1.0)
    marks = blobs(12, 8, 40, 0.14, 0.5)
    hair = scratches(13, 60, 0.07, 0.6)
    a = BASE + 0.006 * wave + 0.02 * hair
    save("gloss", a, wave, 0.84 - 0.05 * marks - 0.12 * hair + 0.006 * wave, strength=0.035)


def laminate():
    """Melamine board: a soft pebble, pale flecks, wiping marks, hairline scratches from things pushed over it."""
    pebble = band(31, 50, 190, fall=0.4)
    fleck = blobs(32, 60, 200, 0.06, 0.4)
    wipe = band(33, 2, 40, sx=0.3, sy=2.0, fall=1.0)
    hair = scratches(34, 46, 0.2, 0.6)
    a = BASE + 0.01 * pebble - 0.06 * fleck + 0.004 * wipe + 0.03 * hair
    save("laminate", a, pebble - 0.6 * hair, 0.5 + 0.008 * wipe - 0.1 * hair, strength=0.12)


def rubber():
    """Rubber: matt, faintly mottled, dusty in the low spots."""
    speck = band(81, 60, 300, fall=0.2)
    cloud = band(82, 3, 24, fall=1.0)
    a = BASE + 0.02 * speck + 0.012 * cloud
    save("rubber", a, speck, 0.2 + 0.03 * cloud, strength=0.22)


def leaf():
    """Leaves: long veins, a waxy mottle."""
    mottle = band(151, 8, 60, fall=0.8)
    vein = band(152, 10, 160, sx=0.15, fall=0.6)
    a = BASE + 0.035 * vein + 0.04 * mottle
    a *= BASE / a.mean()
    save("leaf", tone(a, 0.05), vein + 0.5 * mottle, 0.42 + 0.06 * mottle, strength=0.35)


def stone():
    """Terrazzo-like pots and bases: a matt mineral body with fine grit."""
    grit = band(161, 40, 300, fall=0.2)
    cloud = band(162, 2, 20, fall=1.0)
    a = BASE + 0.035 * grit + 0.02 * cloud
    save("stone", a, grit, 0.22 + 0.02 * cloud, strength=0.35)


def wallpaint():
    """Painted plaster, a tile of 2.5 m: the stipple of the roller, the plasterer's slow unevenness."""
    roll = band(171, 90, 260, sx=0.8, fall=0.3)
    lap = band(172, 10, 70, sx=2.2, sy=0.35, fall=0.8)                  # the roller's passes, up and down
    trowel = band(173, 3, 18, fall=1.1)
    h = 1.0 * roll + 0.5 * lap + 6.0 * trowel
    a = BASE + 0.006 * roll + 0.004 * lap + 0.008 * trowel
    a *= BASE / a.mean()
    save("wallpaint", a, h, 0.3 + 0.012 * lap + 0.02 * trowel - 0.02 * np.clip(roll, 0, 3), strength=0.06)


# ------------------------------------------------------------------------------------------------- scanned surfaces

def grey(path):
    return np.asarray(Image.open(path).convert("L").resize((N, N), Image.LANCZOS), dtype=np.float64) / 255


def even(a, keep=0.45, size=0.12):
    """A scan's lightness without its large shadows (keep: how much of them stays), around 0."""
    low = ndimage.gaussian_filter(a, N * size, mode="wrap")
    return (a - low) + keep * (low - low.mean())


def scanned(name, set_name, contrast=1.0, smooth=None, sheen=1.0, metal=0.0, relief=1.0, turn=False, colour=0.0, gain=None):
    """A surface from a scan (Tools/Art/fetch_surfaces.py): its lightness made neutral round BASE, its relief, its sheen as
    scanned (smooth: the mean it is moved to, sheen: how much of its unevenness)."""
    d = os.path.join(SCANS, set_name)
    img = Image.open(os.path.join(d, "color.jpg")).convert("RGB").resize((N, N), Image.LANCZOS)
    nor = Image.open(os.path.join(d, "normal.jpg")).convert("RGB").resize((N, N), Image.LANCZOS)
    ao = np.ones((N, N))
    if os.path.exists(os.path.join(d, "arm.jpg")):
        arm = Image.open(os.path.join(d, "arm.jpg")).convert("RGB").resize((N, N), Image.LANCZOS).split()
        ao, rough = np.asarray(arm[0], float) / 255, np.asarray(arm[1], float) / 255
    else:
        rough = grey(os.path.join(d, "rough.jpg"))
        if os.path.exists(os.path.join(d, "ao.jpg")):
            ao = grey(os.path.join(d, "ao.jpg"))
    n = np.asarray(nor, float) / 127.5 - 1
    rgb = np.asarray(img, float) / 255
    if turn:                                              # a quarter turn clockwise: the grain along x
        rgb, rough, ao, n = np.rot90(rgb, -1), np.rot90(rough, -1), np.rot90(ao, -1), np.rot90(n, -1)
        n = np.stack([n[..., 1], -n[..., 0], n[..., 2]], -1)
    lum = rgb @ np.array([0.299, 0.587, 0.114])
    k = contrast / max(lum.mean(), 0.05) if gain is None else gain
    a = BASE * (1 + k * even(lum))
    hue = rgb / np.maximum(lum[..., None], 0.03)
    hue = hue / hue.mean((0, 1))
    albedo = a[..., None] * (1 + colour * (hue - 1))
    albedo *= BASE / albedo.mean()
    s = 1 - rough
    s = (s.mean() if smooth is None else smooth) + sheen * (s - s.mean())
    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    Image.fromarray((np.clip(albedo, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(folder, "albedo.jpg"), quality=91)
    n = np.stack([n[..., 0] * relief, n[..., 1] * relief, np.maximum(n[..., 2], 0.05)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8)).save(os.path.join(folder, "normal.jpg"), quality=93, subsampling=0)
    full = np.ones((N, N))
    mask = np.stack([full * metal, ao, full * 0, s], -1)
    Image.fromarray((np.clip(mask, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(folder, "mask.png"), optimize=True)
    print(f"wrote {name} ({set_name}): albedo {albedo.mean():.3f} sd {albedo.std():.3f} smooth {s.mean():.2f} sd {s.std():.3f}")


def powdercoat():
    """Powder-coated sheet steel."""
    scanned("powdercoat", "Metal027", contrast=0.45, smooth=0.47, sheen=1.6, relief=1.8)


def steel():
    """Brushed, handled steel."""
    scanned("steel", "Metal009", contrast=1.0, smooth=0.58, sheen=1.5, metal=0.8, relief=1.5)


def plastic():
    """Moulded plastic, used."""
    scanned("plastic", "Plastic013A", contrast=2.2, smooth=0.45, sheen=1.8, relief=1.3)


def plastic_rough():
    """Plastic that has stood for years."""
    scanned("plastic_rough", "Plastic018B", contrast=1.2, smooth=0.4, sheen=0.9, relief=1.3)


def leather():
    """Leatherette."""
    scanned("leather", "Leather026", gain=2.0, smooth=0.42, sheen=1.6, relief=1.6)


def ceramic():
    """Glazed ware."""
    scanned("ceramic", "Porcelain001", contrast=1.5, smooth=0.87, sheen=1.4, relief=1.0)


def cardboard():
    """Kraft board."""
    scanned("cardboard", "Cardboard004", contrast=1.3, smooth=0.2, relief=1.6)


def paper():
    """Paper."""
    scanned("paper", "Paper001", contrast=2.0, smooth=0.22, relief=1.2)


def felt():
    """Felt and baize."""
    scanned("felt", "Fabric034", contrast=1.2, smooth=0.1, relief=1.2)


def wood():
    """Walnut veneer under a satin lacquer."""
    scanned("wood", "american_walnut_veneer", contrast=1.6, smooth=0.46, sheen=1.4, relief=1.6, colour=0.8)


def oak():
    """Oak veneer."""
    scanned("oak", "oak_veneer_01", contrast=1.2, smooth=0.42, sheen=0.9, relief=1.5, turn=True, colour=0.8)


def timber():
    """Plain sawn wood."""
    scanned("timber", "plywood", contrast=0.75, smooth=0.22, relief=1.6, colour=0.8)


def fabric():
    """Woven upholstery."""
    scanned("fabric", "rough_linen", contrast=1.6, smooth=0.12, relief=1.5)


def wool():
    """Wool upholstery (its relief; its plaid evened out)."""
    scanned("wool", "wool_boucle", gain=0.28, smooth=0.08, relief=1.4)


def cloth():
    """Thin cloth: clothes, curtains, drapes."""
    scanned("cloth", "cotton_jersey", contrast=2.0, smooth=0.12, relief=1.6)


def wear():
    """What every surface is covered with, Stealth/Surface's wear map: R fingerprints and greasy smears, G scratches, B stains
    and spots, A runs down a wall."""
    def o(name):
        return grey(os.path.join(SCANS, name, "opacity.jpg"))
    r = np.clip(o("Fingerprints002") * 1.15 + o("SurfaceImperfections003") * 0.9 - 0.08, 0, 1)
    g = np.clip(o("Scratches003") * 3.2, 0, 1)
    spots = o("SurfaceImperfections013")
    blot = step(0.45, 1.15, even(1 - grey(os.path.join(SCANS, "Plastic018B", "rough.jpg")), keep=0.8) * 3 + 0.45)
    b = np.clip(0.75 * blot + 1.2 * spots, 0, 1)
    runs = np.asarray(Image.open(os.path.join(SCANS, "Leaking001", "opacity.jpg")).convert("L").resize((N, N), Image.LANCZOS), float) / 255
    ramp = step(0.0, 0.12, Y) * 1.0
    a = np.clip(runs * ramp + 0.25 * o("SurfaceImperfections003"), 0, 1)
    Image.fromarray((np.stack([r, g, b, a], -1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, "wear.png"), optimize=True)
    print(f"wrote wear: smudges {r.mean():.2f} scratches {g.mean():.2f} stains {b.mean():.2f} runs {a.mean():.2f}")


def relief():
    """What lies under every grain, Stealth/Surface's relief map (a tile of 1.6 m): RG the slopes (x, y, over 0.2) of slow
    waves and a few dents, B clouds."""
    n = 512
    wave = band(181, 1.6, 11, fall=1.1)
    dent = ndimage.gaussian_filter(blobs(182, 5, 12, 0.006, 0.3), 16, mode="wrap")
    h = wave - 0.12 * dent / (dent.std() + 1e-9)
    h = h[::2, ::2]
    dx, dy = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5, (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    k = 0.035 / np.sqrt((dx ** 2 + dy ** 2).mean())                     # about 2 degrees, to strengthen or weaken per surface
    clouds = np.clip(0.5 + 0.17 * band(183, 1.0, 7, fall=1.2)[::2, ::2], 0, 1)
    img = np.stack([0.5 - dx * k / 0.4, 0.5 + dy * k / 0.4, clouds, np.ones((n, n))], -1)
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, "relief.png"), optimize=True)
    print(f"wrote relief: slope {np.sqrt(((dx * k) ** 2 + (dy * k) ** 2).mean()):.3f}")


SURFACES = [powdercoat, gloss, plastic, plastic_rough, laminate, fabric, wool, cloth, felt, leather, rubber, steel, ceramic, cardboard, paper, wood, oak,
            timber, leaf, stone, wallpaint]


def sheet(path):
    """A contact sheet: every surface's albedo lit from the side (with its normal map) at half size."""
    n, t = len(SURFACES), 256
    cols = 7
    img = Image.new("RGB", (cols * t, ((n + cols - 1) // cols) * t))
    for i, f in enumerate(SURFACES):
        d = os.path.join(OUT, f.__name__)
        a = np.asarray(Image.open(os.path.join(d, "albedo.jpg")).resize((512, 512), Image.LANCZOS), float) / 255
        nm = np.asarray(Image.open(os.path.join(d, "normal.jpg")).resize((512, 512), Image.LANCZOS), float) / 127.5 - 1
        l = np.array([-0.55, 0.45, 0.7])
        s = np.clip((nm * l).sum(-1), 0, 1)[..., None] / 0.75
        p = Image.fromarray((np.clip(a * s, 0, 1) * 255).astype(np.uint8)).crop((0, 0, t, t))
        ImageDraw.Draw(p).text((4, 4), f.__name__, fill=(200, 30, 30))
        img.paste(p, ((i % cols) * t, (i // cols) * t))
    img.save(path)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:]]
    if args and args[0] == "sheet":
        sheet(args[1])
    else:
        for f in SURFACES:
            if not args or f.__name__ in args:
                f()
        if not args or "wear" in args:
            wear()
        if not args or "relief" in args:
            relief()
