"""The coffee's speed lines (GameHUD.SpeedLines): thin white streaks round the screen's edges pointing at its middle, the
middle clear. White with alpha (the HUD tints and fades it). Deterministic (seeded).

Run:  python Tools/Art/generate_speed_lines.py   ->  Assets/_Game/Art/Sprites/UI/speed_lines.png
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Game", "Art", "Sprites", "UI", "speed_lines.png")


def main():
    w, h = 1024, 576
    rng = np.random.default_rng(7)
    yy, xx = np.mgrid[0:h, 0:w]
    dx, dy = (xx - w / 2) / (w / 2), (yy - h / 2) / (h / 2)
    r = np.sqrt(dx ** 2 + dy ** 2)
    ang = np.arctan2(dy, dx)
    alpha = np.zeros((h, w))
    for _ in range(150):
        a0 = rng.uniform(-np.pi, np.pi)
        width = rng.uniform(0.0025, 0.009)        # radians
        start = rng.uniform(0.62, 0.95)           # where the streak begins, from the middle
        strength = rng.uniform(0.35, 1.0)
        d = np.abs(np.angle(np.exp(1j * (ang - a0))))
        alpha = np.maximum(alpha, np.clip(1 - d / width, 0, 1) * np.clip((r - start) / 0.25, 0, 1) * strength)
    alpha *= np.clip((r - 0.55) / 0.35, 0, 1)    # the middle stays clear
    img = np.zeros((h, w, 4), np.uint8)
    img[..., :3] = 255
    img[..., 3] = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(img, "RGBA").save(OUT)
    print("speed lines ->", OUT)


if __name__ == "__main__":
    main()
