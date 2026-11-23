"""The strand texture of hair cards: Assets/_Game/Art/Textures/Humans/hair_strands.png.

Four columns side by side (a card takes one, so neighbouring cards differ), each a lock of hairs from its root (v = 0,
the bottom of the image) to its tip (v = 1): thin strands of a few pixels, a little wavy, of uneven brightness, dense at
the root and thinning out towards the tip, where each hair ends on its own. White: the material's colour is the hair's.
Alpha is the coverage (the game cuts it at 0.45 and keeps the coverage in the mips — AssetBuilder.ConfigureHumanTextures).

Run:  python Tools/Art/generate_hair_strands.py   (deterministic)
"""
import os

import numpy as np
from PIL import Image

W, H = 512, 1024           # four columns of 128 px; the length of a card along the image's height
COLUMNS = 4
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "Humans", "hair_strands.png")


def column(rng, x0, width, hairs, bright):
    """One lock: (alpha, value) of its column."""
    alpha = np.zeros((H, width), np.float32)
    value = np.zeros((H, width), np.float32)
    v = np.arange(H, dtype=np.float32)[:, None] / (H - 1)              # 0 at the root … 1 at the tip
    x = np.arange(width, dtype=np.float32)[None, :]
    for _ in range(hairs):
        start = rng.uniform(0.0, 0.08)                                 # (some start a little up the card: the root is ragged too)
        end = rng.uniform(0.55, 1.0) ** 0.7                            # each hair ends on its own
        cx = rng.uniform(2, width - 2)
        drift = rng.uniform(-6, 6)                                     # a lock leans a little
        wave_a, wave_f, wave_p = rng.uniform(0.4, 2.0), rng.uniform(2.0, 5.0), rng.uniform(0, 6.28)
        half = rng.uniform(0.55, 1.15)                                 # half-width in pixels
        centre = cx + drift * v + wave_a * np.sin(v * wave_f * 6.283 + wave_p)
        on = np.clip((v - start) / 0.02, 0, 1) * np.clip((end - v) / 0.06, 0, 1)          # fades in at its root, out at its tip
        taper = 1.0 - 0.55 * np.clip((v - start) / max(end - start, 1e-3), 0, 1)        # thinner towards the tip
        d = np.abs(x - centre) - half * taper
        a = np.clip(0.5 - d, 0, 1) * on                                # an anti-aliased line
        b = bright * rng.uniform(0.70, 1.0) * (0.92 + 0.08 * np.sin(v * rng.uniform(20, 40) + rng.uniform(0, 6.28)))
        value = np.where(a > alpha, b, value)
        alpha = np.maximum(alpha, a)
    return alpha, value


def main():
    rng = np.random.default_rng(1907)
    alpha = np.zeros((H, W), np.float32)
    value = np.zeros((H, W), np.float32)
    cw = W // COLUMNS
    for c, (hairs, bright) in enumerate(((160, 1.0), (135, 0.92), (180, 0.96), (120, 0.86))):
        a, b = column(rng, c * cw, cw, hairs, bright)
        alpha[:, c * cw:(c + 1) * cw] = a
        value[:, c * cw:(c + 1) * cw] = b
    rgb = np.repeat(value[..., None], 3, 2)
    img = np.concatenate([rgb, alpha[..., None]], 2)
    img = (np.clip(img, 0, 1) * 255).astype(np.uint8)[::-1]            # (row 0 of a PNG is the top: the root is at the bottom, v = 0)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(img, "RGBA").save(OUT)
    print("wrote", os.path.abspath(OUT), "coverage at root / middle / tip:",
          [round(float(alpha[int(H * f)].mean()), 2) for f in (0.05, 0.5, 0.9)])


if __name__ == "__main__":
    main()
