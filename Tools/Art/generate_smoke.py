"""The puff of the smoke and gas clouds robots leave: Assets/_Game/Art/Sprites/SmokePuff.png.

A 2 x 2 sheet of four different billowy puffs (the particle system picks one per particle): white, the alpha is the
cloud — several octaves of value noise shaped by a soft round falloff, so a puff has lobes and wisps and no hard edge
or square corner. The colour comes from the material (grey smoke, green gas), the light from the room's lamps.

  python Tools/Art/generate_smoke.py
"""
import os

import numpy as np
from PIL import Image

SIZE = 256  # one puff
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Game", "Art", "Sprites", "SmokePuff.png")


def value_noise(rng, size, cells):
    """Smooth value noise in 0..1: a random grid of `cells` blown up with a smoothstep."""
    grid = rng.random((cells + 1, cells + 1))
    t = np.linspace(0, cells, size, endpoint=False)
    i = t.astype(int)
    f = t - i
    f = f * f * (3 - 2 * f)
    a = grid[np.ix_(i, i)]
    b = grid[np.ix_(i, i + 1)]
    c = grid[np.ix_(i + 1, i)]
    d = grid[np.ix_(i + 1, i + 1)]
    fx = f[None, :]
    fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def puff(seed):
    rng = np.random.default_rng(seed)
    noise = np.zeros((SIZE, SIZE))
    weight = 0.0
    for octave, cells in enumerate((3, 6, 12, 24, 48)):
        w = 0.55 ** octave
        noise += value_noise(rng, SIZE, cells) * w
        weight += w
    noise /= weight
    # a few lobes: blobs off the middle, so the outline is a cloud's and not a disc's
    y, x = np.mgrid[0:SIZE, 0:SIZE] / (SIZE - 1) * 2 - 1
    keep = np.ones((SIZE, SIZE))
    for _ in range(6):
        cx, cy = rng.uniform(-0.36, 0.36, 2)
        r = rng.uniform(0.3, 0.46)
        keep *= 1 - 0.85 * np.exp(-((x - cx) ** 2 + (y - cy) ** 2) / (r * r) * 1.6)  # a soft union of round lobes: no creases
    shape = 1 - keep
    edge = np.clip(1 - np.hypot(x, y), 0, 1)  # nothing reaches the tile's border
    alpha = np.clip(shape * 1.35 - 0.3 + (noise - 0.5) * 1.5, 0, 1) * np.clip(edge * 3.0, 0, 1)
    alpha = alpha * alpha * (3 - 2 * alpha)
    shade = np.clip(0.78 + (noise - 0.5) * 0.9, 0, 1)  # darker folds inside the puff
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = (shade * 255).astype(np.uint8)
    rgba[..., 3] = (alpha * 235).astype(np.uint8)
    return rgba


def main():
    sheet = np.zeros((SIZE * 2, SIZE * 2, 4), dtype=np.uint8)
    for k in range(4):
        r, c = divmod(k, 2)
        sheet[r * SIZE:(r + 1) * SIZE, c * SIZE:(c + 1) * SIZE] = puff(1907 + k * 31)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(sheet, "RGBA").save(OUT)
    print("written", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
