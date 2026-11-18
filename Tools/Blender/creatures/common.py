"""The creature language: the neutral Lang (bestiary/lang.py) plus organic pieces — chitin segments, tapered
tentacles, spines, claws, eye clusters, membranes. All return lists of bmeshes in world coordinates (metres, Blender
x right, -y front, z up), like Lang.

How the creatures read (bestiary/STYLES.md, "How it is seen"): a top-down-ish camera 10-15 m away, a few lamps in
the dark — so each creature has ONE strong animal idea in its big forms (a hood, a coiled spiral, a bell, a crab
shell), its Eye glow is visible from above and from the front, and nothing that matters is under 3 cm.
Roles: Body grey (the hide), Accent orange (chitin, joints, markings), Eye glowing (eyes, pores, seams).
"""
import math

import bmesh
from mathutils import Vector

import shapes as s
from lang import Lang, align
from designs import Parts, mx  # noqa: F401  (re-exported for the design modules)


def V(p):
    return Vector(p)


def lerp(a, b, t):
    return V(a).lerp(V(b), t)


class Creature(Lang):
    name = "Creature"
    seg = 24
    sharp = 40.0

    # ------------------------------------------------------------------ organic pieces

    def seg_between(self, p0, p1, r0, r1=None, ends=0.35, n=3.0, flat=1.0, seg=None, overlap=0.0):
        """One chitin segment p0 -> p1: a capsule whose radius goes r0 -> r1, rounded ends (ends = share of the end
        radius used as a dome), section superellipse n, flat < 1 flattens it across (y of the local frame)."""
        r1 = r0 if r1 is None else r1
        a, b = V(p0), V(p1)
        d = (b - a)
        L = d.length + 2 * overlap
        rings = [(-L / 2, 0, 0), (-L / 2 + r0 * ends, r0 * .8, r0 * .8 * flat, n), (-L / 2 + r0 * ends * 2, r0, r0 * flat, n),
                 (L / 2 - r1 * ends * 2, r1, r1 * flat, n), (L / 2 - r1 * ends, r1 * .8, r1 * .8 * flat, n), (L / 2, 0, 0)]
        bm = s.loft(rings, seg=seg or (16 if max(r0, r1) > .05 else 12))
        return [align(bm, a, b)]

    def chain(self, points, radii, n=3.0, flat=1.0, seg=None, overlap=0.012):
        """Segments along a polyline (a leg, an antenna, a tail), each overlapping the next."""
        out = []
        for k in range(len(points) - 1):
            out += self.seg_between(points[k], points[k + 1], radii[k], radii[k + 1], n=n, flat=flat, seg=seg,
                                    overlap=overlap)
        return out

    def tentacle(self, points, r0, r1=None, res=3, per=6):
        """A smooth tapering tube through points (closed ends)."""
        r1 = r0 * .25 if r1 is None else r1
        k = len(points) - 1
        return [s.tube(points, r0, res=res, radii=[1 + (r1 / r0 - 1) * i / k for i in range(len(points))], segments_per=per)]

    def cone(self, p0, p1, r, seg=8, tip=0.0):
        """A spine or tooth from its base p0 to its tip p1."""
        a, b = V(p0), V(p1)
        L = (b - a).length
        bm = s.lathe([(r, -L / 2), (r * .7, -L / 6), (max(tip, 1e-4) if tip else 0, L / 2)], seg=seg)
        return [align(bm, a, b)]

    def ellipsoid(self, loc, radii, seg=16):
        return [s.ball(1.0, loc, seg=seg, scale=radii)]

    def claw(self, base, tip, r, bend=(0, 0, 0), seg=8):
        """A curved hook: two cones through a bent midpoint."""
        mid = lerp(base, tip, .5) + V(bend)
        return self.seg_between(base, mid, r, r * .7, seg=seg) + self.cone(mid, tip, r * .7, seg=seg)

    def eyes(self, centres, radius, seg=12):
        """Glowing eye balls; radius may be one number or one per centre."""
        rs = radius if isinstance(radius, (list, tuple)) else [radius] * len(centres)
        return [s.ball(r, c, seg=seg if r > .02 else 8) for c, r in zip(centres, rs)]

    def membrane(self, root, ribs, thick=0.012):
        """A web of skin stretched between ribs fanning from root (bat ear, fin, hood): a fan of quads, thickened.
        ribs: list of tip points in order."""
        bm = bmesh.new()
        c = bm.verts.new(V(root))
        tips = [bm.verts.new(V(p)) for p in ribs]
        for a, b in zip(tips, tips[1:]):
            bm.faces.new((c, a, b))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=thick)
        return [bm]

    def hollow(self, bms, cutter):
        """Cut a cavity (the cutter bmesh) out of a single bmesh list's first piece."""
        return [s.boolean(bms[0], cutter)] + bms[1:]


def ring_points(c, rx, ry, n, z=None, phase=0.0):
    c = V(c)
    return [V((c.x + math.cos(phase + k / n * math.tau) * rx, c.y + math.sin(phase + k / n * math.tau) * ry,
               c.z if z is None else z)) for k in range(n)]
