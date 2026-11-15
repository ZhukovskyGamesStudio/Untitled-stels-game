"""The spider's web panel: white threads on transparent, one 1 m x 0.5 m piece of web strung along a wire.

    python Tools/Art/generate_web.py        (writes Assets/_Game/Art/Textures/Web/web_panel.png, deterministic)

The wire (the line the spider feels) runs through the middle from edge to edge, so panels laid end to end make one
long web; around it a small orb web (spokes and a spiral) and anchor threads sagging to the corners.
"""
import math
import os
import random

from PIL import Image, ImageDraw

W, H, SS = 512, 256, 4
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "_Game", "Art", "Textures", "Web")


def sag(draw, a, b, depth, width, steps=24):
    """A thread from a to b, hanging down by depth in the middle (pixels at SS)."""
    pts = []
    for i in range(steps + 1):
        t = i / steps
        x = a[0] + (b[0] - a[0]) * t
        y = a[1] + (b[1] - a[1]) * t + math.sin(math.pi * t) * depth
        pts.append((x, y))
    draw.line(pts, fill=255, width=width, joint="curve")


def main():
    rnd = random.Random(0x5E8)
    n, m = W * SS, H * SS
    img = Image.new("L", (n, m), 0)
    d = ImageDraw.Draw(img)
    mid = m / 2
    # the wire: edge to edge through the middle (meets the next panel's)
    d.line([(0, mid), (n, mid)], fill=255, width=5 * SS)
    # an orb web around a centre a little off the middle
    cx, cy = n * (0.5 + rnd.uniform(-0.08, 0.08)), mid + rnd.uniform(-0.06, 0.06) * m
    spokes = 11
    reach = []
    for k in range(spokes):
        a = 2 * math.pi * k / spokes + rnd.uniform(-0.12, 0.12)
        r = m * rnd.uniform(0.42, 0.5) if abs(math.sin(a)) > 0.3 else n * rnd.uniform(0.22, 0.3)
        reach.append((a, r))
        d.line([(cx, cy), (cx + math.cos(a) * r, cy + math.sin(a) * r)], fill=255, width=2 * SS)
    # the spiral: rings of straight pieces between the spokes, wider each turn
    rings = 9
    for ring in range(1, rings + 1):
        f = ring / (rings + 1)
        pts = []
        for a, r in reach + [reach[0]]:
            rr = r * f * rnd.uniform(0.92, 1.0)
            pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
        for p, q in zip(pts, pts[1:]):
            sag(d, p, q, SS * rnd.uniform(1, 4), int(1.5 * SS), steps=6)
    # anchor threads from the wire to the corners and a few loose strands
    for corner in [(0, 0), (n, 0), (0, m), (n, m)]:
        start = (n * rnd.uniform(0.15, 0.35) if corner[0] == 0 else n * rnd.uniform(0.65, 0.85), mid)
        sag(d, start, corner, SS * 6, 2 * SS)
    for _ in range(6):
        x0 = n * rnd.random()
        sag(d, (x0, mid), (x0 + n * rnd.uniform(-0.2, 0.2), m * rnd.choice([0.02, 0.98])), SS * 8, int(1.5 * SS))
    alpha = img.resize((W, H), Image.LANCZOS)
    out = Image.merge("RGBA", (Image.new("L", (W, H), 255),) * 3 + (alpha,))
    os.makedirs(OUT, exist_ok=True)
    out.save(os.path.join(OUT, "web_panel.png"))
    print("web ->", os.path.abspath(os.path.join(OUT, "web_panel.png")))


if __name__ == "__main__":
    main()
