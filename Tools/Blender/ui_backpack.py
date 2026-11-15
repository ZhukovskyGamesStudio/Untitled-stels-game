"""The inventory's look: the inside of a backpack (the grid sits on it) and the hand slot's frame.

Run:  blender.exe -b --factory-startup --python Tools/Blender/ui_backpack.py -- Assets/_Game/Art/Sprites/UI

Writes <out>/backpack.png (the open backpack: worn canvas, a padded lining, a zip along the top, seams, and two
pockets in the bottom corners where the grid has no cells — InventoryLayout ".####.") and <out>/hand_slot.png (a
leather holster frame). 3 px per UI pixel; the grid is 6 x 4 cells of 32 px with 20 px of backpack around it.
Deterministic (noise from fixed seeds).
"""
import math
import os
import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Sprites", "UI"))
S = 3                    # pixels per UI pixel
CELL, PAD = 32, 20       # InventoryUI.Cell, the backpack around the grid
GW, GH = 6 * CELL, 4 * CELL


def noise(seed, h, w, scale, octaves=4):
    rng = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32)
    amp = 1.0
    for o in range(octaves):
        fy, fx = np.fft.fftfreq(h)[:, None], np.fft.fftfreq(w)[None, :]
        cut = scale * 2 ** o
        spec = np.fft.fft2(rng.standard_normal((h, w))) * np.exp(-((fx * w) ** 2 + (fy * h) ** 2) / cut ** 2)
        layer = np.real(np.fft.ifft2(spec)).astype(np.float32)
        out += layer / (np.abs(layer).max() + 1e-6) * amp
        amp *= 0.5
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


def rrect(x, y, x0, y0, x1, y1, r):
    """Signed distance to a rounded rectangle (pixels)."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    qx = np.abs(x - cx) - ((x1 - x0) / 2 - r)
    qy = np.abs(y - cy) - ((y1 - y0) / 2 - r)
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r


def cover(d, soft=1.0):
    return np.clip(0.5 - d / soft, 0, 1)


def paint(img, a, rgb):
    a = a[..., None]
    img[..., :3] = img[..., :3] * (1 - a) + np.array(rgb, np.float32) * a
    img[..., 3:] = np.maximum(img[..., 3:], a)


def stitches(x, y, d, along, period=7.0, width=1.0):
    """A dashed seam along the zero line of d (along = the coordinate the dashes follow)."""
    dash = (np.mod(along, period * S) < period * S * 0.55)
    return cover(np.abs(d) - width * S, S * 0.8) * dash


def backpack():
    w, h = (GW + 2 * PAD) * S, (GH + 2 * PAD) * S
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    img = np.zeros((h, w, 4), np.float32)
    weave = (np.sin(x * 1.9) * np.sin(y * 1.9) * 0.5 + 0.5)
    grime = noise(3, h, w, 6.0)
    folds = noise(4, h, w, 2.5)

    # the outer shell: worn olive canvas with a dark rim
    shell = rrect(x, y, 0, 0, w, h, 16 * S)
    base = np.array([0.26, 0.27, 0.2], np.float32)
    shade = (0.75 + 0.15 * weave + 0.25 * folds - 0.25 * grime)[..., None]
    a = cover(shell)
    img[..., :3] = base * shade * a[..., None]
    img[..., 3] = a
    paint(img, stitches(x, y, rrect(x, y, 5 * S, 5 * S, w - 5 * S, h - 5 * S, 12 * S), x + y), (0.55, 0.52, 0.38))

    # the zip along the top: tape, teeth, a pull
    tape = cover(np.abs(y - 8 * S) - 4 * S) * cover(np.abs(x - w / 2) - (w / 2 - 18 * S))
    paint(img, tape, (0.08, 0.08, 0.07))
    teeth = tape * (np.mod(x, 4 * S) < 2 * S) * cover(np.abs(y - 8 * S) - 2 * S)
    paint(img, teeth, (0.55, 0.53, 0.48))
    pull = cover(rrect(x, y, w - 30 * S, 3 * S, w - 18 * S, 20 * S, 3 * S))
    paint(img, pull, (0.6, 0.58, 0.5))

    # the inside: a darker padded lining under the grid, quilted
    inner = rrect(x, y, (PAD - 6) * S, (PAD - 4) * S, w - (PAD - 6) * S, h - (PAD - 8) * S, 8 * S)
    quilt = (np.sin(x / (CELL * S) * math.pi * 2) * np.sin(y / (CELL * S) * math.pi * 2)) * 0.06
    lining = np.array([0.1, 0.11, 0.1], np.float32) * (0.85 + 0.2 * weave + quilt - 0.15 * grime)[..., None]
    a = cover(inner)[..., None]
    img[..., :3] = img[..., :3] * (1 - a) + lining * a
    paint(img, stitches(x, y, inner + 3 * S, x - y, 6.0, 0.8), (0.3, 0.3, 0.25))

    # two pockets in the bottom corners (the grid has no cells there)
    for px0 in (PAD, PAD + 5 * CELL):
        x0, x1 = (px0 + 1) * S, (px0 + CELL - 1) * S
        y0, y1 = (PAD + 3 * CELL + 1) * S, (PAD + 4 * CELL + 6) * S
        pocket = rrect(x, y, x0, y0, x1, y1, 5 * S)
        pa = cover(pocket)
        pshade = (0.7 + 0.25 * weave + 0.2 * folds - 0.2 * grime)
        img[..., :3] = img[..., :3] * (1 - pa[..., None]) + (base * 1.1)[None, None, :] * pshade[..., None] * pa[..., None]
        paint(img, stitches(x, y, pocket + 2.5 * S, x + y, 5.0, 0.7), (0.6, 0.57, 0.42))
        flap = rrect(x, y, x0 - 1 * S, y0 - 1 * S, x1 + 1 * S, y0 + 10 * S, 4 * S)
        paint(img, cover(flap), (0.2, 0.21, 0.15))
        paint(img, cover(np.hypot(x - (x0 + x1) / 2, y - (y0 + 9 * S)) - 2.6 * S), (0.55, 0.5, 0.35))   # the snap
    # a little dark at the edges, light falling in from the opening above
    vign = np.clip(1.0 - 0.35 * ((np.abs(x - w / 2) / (w / 2)) ** 3) - 0.25 * (y / h) ** 2, 0, 1)
    img[..., :3] *= vign[..., None]
    return img


def hand_slot():
    w, h = (2 * CELL + 12) * S, (3 * CELL + 30) * S
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    img = np.zeros((h, w, 4), np.float32)
    grain = noise(7, h, w, 5.0)
    leather = rrect(x, y, 0, 0, w, h, 10 * S)
    a = cover(leather)
    img[..., :3] = (np.array([0.24, 0.15, 0.09], np.float32) * (0.8 + 0.35 * grain)[..., None]) * a[..., None]
    img[..., 3] = a
    paint(img, stitches(x, y, rrect(x, y, 4 * S, 4 * S, w - 4 * S, h - 4 * S, 7 * S), x + y, 6.0, 0.8), (0.75, 0.62, 0.4))
    hole = rrect(x, y, 8 * S, 8 * S, w - 8 * S, h - 24 * S, 5 * S)
    paint(img, cover(hole), (0.06, 0.05, 0.04))
    return img


def save(name, img):
    rgb = np.clip(img[..., :3], 0, 1)
    srgb = np.where(rgb <= 0.0031308, rgb * 12.92, 1.055 * np.power(rgb, 1 / 2.4) - 0.055)
    rgba = np.concatenate([srgb, np.clip(img[..., 3:], 0, 1)], axis=2)
    os.makedirs(OUT, exist_ok=True)
    h, w = rgba.shape[:2]
    im = bpy.data.images.new(name, w, h, alpha=True)
    im.colorspace_settings.name = "Non-Color"  # before the pixels: setting it resets them
    im.pixels.foreach_set(np.ascontiguousarray(rgba[::-1]).astype(np.float32).ravel())
    im.filepath_raw = os.path.join(OUT, f"{name}.png")
    im.file_format = "PNG"
    im.save()
    print("wrote", im.filepath_raw)


save("backpack", backpack())
save("hand_slot", hand_slot())
