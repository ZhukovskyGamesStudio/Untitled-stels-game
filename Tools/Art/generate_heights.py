"""Height maps for parallax, worked out of the normal maps we already have (no download).

A scan's normal map is the slope of its surface; the scans tile, so the slopes integrate exactly in the frequency domain
(Frankot-Chellappa). For every texture folder with a normal.jpg (Art/External/Textures/<id>, Art/Textures/Derelict/<id>)
this writes height.png beside it — grey, mid-grey the middle level — and one line of heights.csv in the folder above:

    id,parallax        parallax = the relief's depth as a share of one repeat of the texture (URP Lit _Parallax)

A surface flatter than MIN gets no height map (paint, carpet: parallax would only cost). AssetBuilder.SetPbr reads the
table. Deterministic: the same normals give the same bytes.

    python Tools/Art/generate_heights.py [out_root]      out_root: write there instead of the project (to look first)
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SETS = ["Assets/_Game/Art/External/Textures", "Assets/_Game/Art/Textures/Derelict"]
MIN = 0.004   # shallower than this share of a repeat: flat to the eye
MAX = 0.05    # deeper is a scan's noise or a hole: URP's one-step parallax swims beyond it


def height_of(normal):
    """Normal map (h, w, 3 in 0..1, OpenGL: green up) -> height in texels, zero mean."""
    n = normal * 2.0 - 1.0
    nz = np.clip(n[..., 2], 0.2, None)
    p = -n[..., 0] / nz            # dh/dx, x to the right
    q = n[..., 1] / nz             # dh/drow: green is up, rows go down
    h, w = p.shape
    u = np.fft.fftfreq(w)[None, :] * 2 * np.pi
    v = np.fft.fftfreq(h)[:, None] * 2 * np.pi
    d = u * u + v * v
    d[0, 0] = 1.0
    z = (-1j * u * np.fft.fft2(p) - 1j * v * np.fft.fft2(q)) / d
    z[0, 0] = 0.0
    return np.real(np.fft.ifft2(z))


def main():
    out_root = sys.argv[1] if len(sys.argv) > 1 else ROOT
    for rel in SETS:
        src = os.path.join(ROOT, rel)
        if not os.path.isdir(src):
            continue
        rows = []
        for name in sorted(os.listdir(src)):
            path = os.path.join(src, name, "normal.jpg")
            if not os.path.isfile(path):
                continue
            img = Image.open(path).convert("RGB")
            hgt = height_of(np.asarray(img, dtype=np.float64) / 255.0)
            lo, hi = np.percentile(hgt, [1.0, 99.0])           # (a few stray texels do not set the scale)
            depth = float(hi - lo) / img.size[0]
            dst = os.path.join(out_root, rel, name)
            target = os.path.join(dst, "height.png")
            if depth < MIN:
                print(f"{name:28s} {depth:.4f}  flat")
                if out_root == ROOT and os.path.isfile(target):
                    os.remove(target)
                continue
            depth = min(depth, MAX)
            mid = (lo + hi) * 0.5
            grey = np.clip((hgt - mid) / (depth * img.size[0]) + 0.5, 0.0, 1.0)
            os.makedirs(dst, exist_ok=True)
            Image.fromarray((grey * 255.0 + 0.5).astype(np.uint8), "L").save(target, optimize=True)
            rows.append(f"{name},{depth:.4f}")
            print(f"{name:28s} {depth:.4f}")
        os.makedirs(os.path.join(out_root, rel), exist_ok=True)
        with open(os.path.join(out_root, rel, "heights.csv"), "w", newline="\n") as f:
            f.write("id,parallax\n" + "\n".join(rows) + "\n")


if __name__ == "__main__":
    main()
