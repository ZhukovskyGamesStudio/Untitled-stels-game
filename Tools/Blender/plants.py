"""Plants made of leaves, not of green lumps: a leaf is a thin folded blade (closed, seen from both sides), a tree a trunk
with branches and twigs carrying them, a clump a rosette of them. Used by the planters (rooms2_tech.planter_large,
rooms2_work.round_bench_planter and planter_divider).

    import plants
    wood, leaves = plants.tree((0, 0, 0.5), 1.3, seed=3)        # lists of bmeshes: s.part("Trim", wood), s.part("Accent", leaves)
    leaves = plants.rosette((0, 0, 0.6), 14, 0.3, 0.07, seed=1)
"""
import math
import random

import bmesh
from mathutils import Vector

import shapes as s

UP = Vector((0, 0, 1))


def blade(bm, base, along, length, width, droop=0.25, fold=0.18, thick=0.0025):
    """Adds one leaf to bm: from base along `along`, its tip hanging by droop × length, folded along its midrib."""
    along = along.normalized()
    side = along.cross(UP)
    if side.length < 1e-4:
        side = Vector((1, 0, 0))
    side.normalize()
    face = side.cross(along).normalized()                      # the leaf's upper side
    mid = base + along * (length * 0.45) - UP * (droop * length * 0.25)
    tip = base + along * length - UP * (droop * length)
    left, right = mid - side * (width / 2) + face * (fold * width), mid + side * (width / 2) + face * (fold * width)
    b, l, r, t = (bm.verts.new(p) for p in (base, left, right, tip))
    top, bot = bm.verts.new(mid + face * thick), bm.verts.new(mid - face * thick)
    for a, c in ((b, r), (r, t), (t, l), (l, b)):
        bm.faces.new((a, c, top))
        bm.faces.new((c, a, bot))


def leaves_at(bm, rnd, at, out, count, length, width, spread=1.0, droop=0.3):
    """A spray of leaves round a twig's end: pointing out and up, turned every way."""
    out = out.normalized()
    for _ in range(count):
        d = out * 0.7 + Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.3, 0.9))) * spread
        blade(bm, at + d.normalized() * rnd.uniform(0.0, 0.03), d, length * rnd.uniform(0.75, 1.1), width * rnd.uniform(0.8, 1.1), droop * rnd.uniform(0.5, 1.4))


def tree(base, height, seed=1, crown=0.42, branches=7, twigs=4, per_twig=7, leaf=(0.13, 0.062), trunk=0.03):
    """A small indoor tree (a fig): a slightly crooked trunk, branches up and out into a crown `crown` wide and about
    0.45 × height tall, twigs on them, leaves round every twig. Returns (wood, leaves) — lists of bmeshes."""
    rnd = random.Random(seed)
    base = Vector(base)
    fork = base + Vector((rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), height * 0.5))
    top = base + Vector((rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04), height * 0.86))
    wood = [s.tube([tuple(base - UP * 0.03), tuple(base + (fork - base) * 0.5 + Vector((0.025, -0.015, 0))), tuple(fork), tuple(top)], trunk,
                   radii=[1.15, 0.95, 0.75, 0.35], res=1, segments_per=6)]
    green = bmesh.new()
    for k in range(branches):
        a = 2 * math.pi * (k + rnd.uniform(-0.25, 0.25)) / branches
        t = rnd.uniform(0.0, 1.0)
        root = fork.lerp(top, t * 0.85)
        reach = crown * rnd.uniform(0.7, 1.0) * (1.0 - 0.45 * t)
        end = root + Vector((math.cos(a) * reach, math.sin(a) * reach, height * rnd.uniform(0.1, 0.24)))
        elbow = root.lerp(end, 0.5) + Vector((0, 0, height * 0.05))
        wood.append(s.tube([tuple(root), tuple(elbow), tuple(end)], trunk * 0.42, radii=[1.0, 0.7, 0.35], res=1, segments_per=4))
        for j in range(twigs):
            f = (j + 1) / twigs
            at = root.lerp(elbow, f * 2) if f < 0.5 else elbow.lerp(end, f * 2 - 1)
            out = (at - (base + UP * (at.z - base.z))).normalized() + Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), rnd.uniform(0.1, 0.6)))
            tw = at + out.normalized() * rnd.uniform(0.07, 0.14)
            wood.append(s.tube([tuple(at), tuple(tw)], trunk * 0.16, res=1, smooth=False))
            leaves_at(green, rnd, tw, out, per_twig, leaf[0], leaf[1])
    leaves_at(green, rnd, top, UP, per_twig + 3, leaf[0], leaf[1], spread=1.2)
    bmesh.ops.recalc_face_normals(green, faces=green.faces)
    return wood, [green]


def rosette(center, count, length, width, seed=1, lean=(0.25, 1.1), droop=0.35, radius=0.03):
    """A clump of leaves from one crown (a peace lily, a fern): some upright in the middle, the outer ones arching over."""
    rnd = random.Random(seed)
    c = Vector(center)
    bm = bmesh.new()
    for k in range(count):
        a = 2 * math.pi * k / count + rnd.uniform(-0.3, 0.3)
        tilt = lean[0] + (lean[1] - lean[0]) * (k % 3) / 2.0 + rnd.uniform(-0.1, 0.1)
        d = Vector((math.cos(a) * math.sin(tilt), math.sin(a) * math.sin(tilt), math.cos(tilt)))
        blade(bm, c + Vector((math.cos(a), math.sin(a), 0)) * radius, d, length * rnd.uniform(0.7, 1.05), width * rnd.uniform(0.8, 1.1), droop * tilt)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return [bm]


def soil(radius, z, seed=1, chips=14, square=None):
    """Soil with bark chips on it: a disc (or a rectangle `square` = (w, d)) and a scatter of small flat chips."""
    rnd = random.Random(seed)
    out = [s.rcyl(radius, 0.03, (0, 0, z - 0.015), seg=24, r=0.004, bevel_seg=1) if square is None else s.rbox((square[0], square[1], 0.03), (0, 0, z - 0.015), r=0.004, seg=1)]
    for _ in range(chips):
        if square is None:
            a, q = rnd.uniform(0, 6.28), radius * 0.85 * math.sqrt(rnd.random())
            x, y = q * math.cos(a), q * math.sin(a)
        else:
            x, y = rnd.uniform(-0.45, 0.45) * square[0], rnd.uniform(-0.4, 0.4) * square[1]
        out.append(s.rbox((rnd.uniform(0.02, 0.05), rnd.uniform(0.012, 0.025), 0.008), (x, y, z + 0.001), r=0.002, seg=1, rot=(rnd.uniform(-0.3, 0.3), 0, rnd.uniform(0, 3.1))))
    return out
