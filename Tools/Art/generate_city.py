"""Textures of the city round the house (scenery behind the yard's invisible wall, CityPlan / AssetBuilder.City).

Run:  python Tools/Art/generate_city.py            (writes Assets/_Game/Art/Textures/City/*.png)

Deterministic (fixed seeds). A facade tile is 12 x 12 m: 4 bays of 3 m by 4 storeys of 3 m (CityPlan.Storey), the
ground storey at the bottom; *_glow.png are the lit windows (white on black: the material's emission colour tints them).
asphalt: 4 m, paving: 2 m (CityPlan.Tile).
"""
import os

import numpy as np
from PIL import Image

N = 512
CELL = N // 4
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "City")


def rng(seed):
    return np.random.default_rng(seed)


def noise(seed, scale):
    """Tileable value noise in 0..1 (a random grid tiled 3 x 3, upsampled, the middle kept)."""
    grid = np.tile(rng(seed).random((scale, scale)), (3, 3))
    img = Image.fromarray((grid * 255).astype(np.uint8)).resize((3 * N, 3 * N), Image.BICUBIC)
    return np.asarray(img, dtype=np.float64)[N:2 * N, N:2 * N] / 255.0


def save(name, arr):
    os.makedirs(OUT, exist_ok=True)
    arr = np.clip(arr, 0, 1)
    if arr.ndim == 2:
        arr = np.stack([arr] * 3, -1)
    Image.fromarray((arr * 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"))
    print("wrote", name)


def facade(name, seed, wall, glass, window, lit_share, frame=0.0, seams=False):
    """wall / glass: RGB; window: (x0, y0, x1, y1) of a 3 x 3 m cell in 0..1 (y up from the floor)."""
    r = rng(seed)
    img = np.ones((N, N, 3)) * np.array(wall)
    img *= (0.92 + 0.16 * noise(seed + 1, 8))[..., None]
    glow = np.zeros((N, N))
    y, x = np.mgrid[0:N, 0:N]
    if seams:  # a dark joint between the storeys' panels
        img[(y % CELL) < 3] *= 0.6
    x0, y0, x1, y1 = window
    for by in range(4):
        for bx in range(4):
            cx0, cx1 = bx * CELL + int(x0 * CELL), bx * CELL + int(x1 * CELL)
            # image rows run down: the cell's floor is at the bottom of its row band
            top = N - (by + 1) * CELL
            cy0, cy1 = top + int((1 - y1) * CELL), top + int((1 - y0) * CELL)
            if frame > 0:
                f = int(frame * CELL)
                img[cy0 - f:cy1 + f, cx0 - f:cx1 + f] = np.array(wall) * 0.45
            shade = 0.75 + 0.5 * r.random()
            img[cy0:cy1, cx0:cx1] = np.array(glass) * shade
            if r.random() < lit_share:
                warm = 0.55 + 0.45 * r.random()
                img[cy0:cy1, cx0:cx1] = np.array([0.95, 0.82, 0.55]) * warm
                glow[cy0:cy1, cx0:cx1] = warm
                # a blind half down now and then
                if r.random() < 0.3:
                    cut = cy0 + int((cy1 - cy0) * (0.2 + 0.4 * r.random()))
                    glow[cy0:cut, cx0:cx1] *= 0.25
                    img[cy0:cut, cx0:cx1] *= 0.5
    save(name, img)
    save(name + "_glow", glow)


def asphalt():
    a = 0.55 + 0.25 * noise(60, 32) + 0.12 * (rng(61).random((N, N)) - 0.5)
    save("asphalt", a)


def paving():
    """2 m: 50 cm concrete slabs with dark joints."""
    y, x = np.mgrid[0:N, 0:N]
    k = N // 4
    tone = rng(62).random((4, 4))[y // k, x // k]
    a = 0.8 + 0.08 * (tone - 0.5) + 0.1 * (noise(63, 16) - 0.5)
    joints = ((x % k) < 3) | ((y % k) < 3)
    save("paving", a * (1 - 0.4 * joints))


if __name__ == "__main__":
    facade("facade_a", 70, (0.72, 0.7, 0.66), (0.12, 0.16, 0.22), (0.18, 0.3, 0.82, 0.82), 0.3, frame=0.03, seams=True)
    facade("facade_b", 80, (0.55, 0.3, 0.22), (0.1, 0.12, 0.16), (0.25, 0.28, 0.75, 0.8), 0.25, frame=0.04)
    facade("facade_c", 90, (0.2, 0.22, 0.25), (0.14, 0.2, 0.28), (0.04, 0.12, 0.96, 0.94), 0.35)
    asphalt()
    paving()
