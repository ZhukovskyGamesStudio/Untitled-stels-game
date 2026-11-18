"""The spider's web as threads, not a picture: one metre of web strung along the trip wire, in three variants.

Real webs (orb weavers, cobweb spiders): a taut dragline the spider feels through, frame threads sagging between anchor
points, an orb's radial spokes with a sticky spiral crossing them, and tangled loose threads of a cobweb. Thin
geometry (no alpha card): an alpha-blended picture of hair-thin threads turns into a milky sheet in the distance, where
its mipmaps average the threads with the air.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/web.py -- Assets/_Game/Art/Models
      (writes <root>/Robots/web_piece.fbx: parts Web0, Web1, Web2 — SpiderBot shows one per piece)

Contract: Unity +Z = Blender -Y is along the wire, from 0 to 1 m (Blender y 0 .. -1); the wire at the origin's height,
the rest hangs below it (to -0.3 m) within a few centimetres of its vertical plane.
"""
import math
import os
import random
import sys

import bmesh

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402
import shapes as s  # noqa: E402

WIRE = 0.0024   # the dragline's radius
SILK = 0.0014   # every other thread


def segment(bm, a, b, r):
    """A thin four-sided tube from a to b, added to bm."""
    ax, ay, az = a
    bx, by, bz = b
    d = (bx - ax, by - ay, bz - az)
    length = math.sqrt(sum(c * c for c in d)) or 1e-6
    d = tuple(c / length for c in d)
    up = (0, 0, 1) if abs(d[2]) < 0.9 else (1, 0, 0)
    u = (d[1] * up[2] - d[2] * up[1], d[2] * up[0] - d[0] * up[2], d[0] * up[1] - d[1] * up[0])
    ul = math.sqrt(sum(c * c for c in u)) or 1e-6
    u = tuple(c / ul for c in u)
    v = (d[1] * u[2] - d[2] * u[1], d[2] * u[0] - d[0] * u[2], d[0] * u[1] - d[1] * u[0])
    ring = [(math.cos(k * math.pi / 2), math.sin(k * math.pi / 2)) for k in range(4)]
    va = [bm.verts.new((ax + r * (c * u[0] + q * v[0]), ay + r * (c * u[1] + q * v[1]), az + r * (c * u[2] + q * v[2]))) for c, q in ring]
    vb = [bm.verts.new((bx + r * (c * u[0] + q * v[0]), by + r * (c * u[1] + q * v[1]), bz + r * (c * u[2] + q * v[2]))) for c, q in ring]
    for k in range(4):
        bm.faces.new((va[k], va[(k + 1) % 4], vb[(k + 1) % 4], vb[k]))
    bm.faces.new(va[::-1])
    bm.faces.new(vb)


def thread(bm, pts, r=SILK):
    for p, q in zip(pts, pts[1:]):
        segment(bm, p, q, r)


def sag(a, b, depth, n=8):
    """Points of a thread hanging between a and b, lowest by depth in the middle."""
    return [(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t - math.sin(math.pi * t) * depth)
            for t in (i / n for i in range(n + 1))]


def orb(bm, rnd, cy, cz, radius, spokes=9, rings=5):
    """A fragment of an orb web hanging under the wire: spokes from the hub (those that point up end on the wire, the
    rest end in the air on frame threads), a spiral of straight pieces between neighbouring spokes."""
    x = rnd.uniform(-0.01, 0.01)
    ends = []
    for k in range(spokes):
        a = 2 * math.pi * k / spokes + rnd.uniform(-0.15, 0.15)
        dy, dz = math.cos(a), math.sin(a)
        r = radius * rnd.uniform(0.8, 1.0)
        if dz > 0:                                   # up: reach the wire (z = 0)
            r = min(r * 1.6, -cz / max(dz, 1e-3))
        end = (x, cy + dy * r, cz + dz * r)
        ends.append((a, r, end))
        thread(bm, [(x, cy, cz), end])
    # the frame: the lower spoke ends joined by sagging threads
    low = [e for e in ends if e[2][2] < -0.02]
    for (_, _, p), (_, _, q) in zip(low, low[1:]):
        thread(bm, sag(p, q, 0.01, n=4))
    for ring in range(1, rings + 1):
        f = ring / (rings + 1)
        pts = [(x, cy + math.cos(a) * r * f, cz + math.sin(a) * r * f) for a, r, _ in ends]
        pts.append(pts[0])
        for p, q in zip(pts, pts[1:]):
            thread(bm, sag(p, q, 0.004, n=2))


def piece(name, seed, kind):
    rnd = random.Random(seed)
    bm = bmesh.new()
    # the dragline: taut, the whole metre (meets the next piece's)
    thread(bm, [(0, 0, 0), (0, -1, 0)], WIRE)
    # frame threads: sagging from the wire between anchor points along it
    for _ in range(2 if kind != 1 else 3):
        a, b = sorted((rnd.uniform(0.0, 0.5), rnd.uniform(0.5, 1.0)))
        x = rnd.uniform(-0.02, 0.02)
        thread(bm, sag((0, -a, 0), (x, -b, 0), rnd.uniform(0.08, 0.2)))
    if kind == 0:
        orb(bm, rnd, -rnd.uniform(0.35, 0.65), -rnd.uniform(0.13, 0.17), 0.12)
    elif kind == 2:
        orb(bm, rnd, -rnd.uniform(0.25, 0.4), -0.1, 0.085, spokes=7, rings=4)
        orb(bm, rnd, -rnd.uniform(0.65, 0.8), -0.13, 0.1, spokes=8, rings=4)
    else:
        # a cobweb's tangle: short threads criss-crossing under the wire, a few tied to it
        for _ in range(22):
            p = (rnd.uniform(-0.05, 0.05), -rnd.uniform(0.05, 0.95), -rnd.uniform(0.0, 0.22))
            q = (p[0] + rnd.uniform(-0.05, 0.05), p[1] + rnd.uniform(-0.18, 0.18), min(0.0, p[2] + rnd.uniform(-0.12, 0.12)))
            thread(bm, sag(p, q, rnd.uniform(0.0, 0.02), n=3))
        for _ in range(5):
            y = -rnd.uniform(0.05, 0.95)
            thread(bm, [(0, y, 0), (rnd.uniform(-0.04, 0.04), y + rnd.uniform(-0.1, 0.1), -rnd.uniform(0.08, 0.2))])
    # loose ends hanging down with a tiny tuft of dust
    for _ in range(rnd.randint(1, 3)):
        y = -rnd.uniform(0.1, 0.9)
        drop = rnd.uniform(0.12, 0.3)
        thread(bm, [(0, y, 0), (rnd.uniform(-0.01, 0.01), y + rnd.uniform(-0.03, 0.03), -drop)])
        dust = s.ball(0.006, (0, y, -drop), seg=6)
        m = bmesh.new()
        dust.to_mesh(_tmp := __import__("bpy").data.meshes.new("_d"))
        bm.from_mesh(_tmp)
        __import__("bpy").data.meshes.remove(_tmp)
        dust.free()
        m.free()
    return s.part(name, [bm], flat=True)


if __name__ == "__main__":
    s.clear_scene()
    piece("Web0", 0x5E80, 0)
    piece("Web1", 0x5E81, 1)
    piece("Web2", 0x5E82, 2)
    s.export_fbx(os.path.join(g.out_dir, "Robots", "web_piece.fbx"))
    print("DONE")
