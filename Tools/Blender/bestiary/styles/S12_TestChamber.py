"""S12 TestChamber — «Испытательный комплекс»: white glossy lab robots.

Language: every body is an egg or a pill (Body: white gloss), split by a dark seam where a black core shows through
(Accent: black joints, cores, bezels); limbs are pills hung on black ball joints; one big round optic per robot
(Eye) set into a black bezel. Clinical and calm: no bolts, no plates, no edges — only shells and gaps.
Our own designs: the walking pill, the opened egg, the eyeball on a stalk.
"""
import math

import bmesh
from mathutils import Euler, Vector

import shapes as s
from lang import Lang, align
import designs as D


class Style(Lang):
    name = "S12_TestChamber"
    seg = 20
    soft = 0.6
    edge = 0.02
    hull_kind = "soft"
    eye_kind = "lens"
    wheel_kind = "solid"
    limb_kind = "tube"

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        # joints and knobs: a sphere costs ~1k triangles at the language's 32 segments
        return [s.ball(radius, loc, seg=16 if radius > .05 else 12, scale=scale)]

    # ------------------------------------------------------------------ furniture: white pill shells, black seams
    edge_kind = "soft"
    cushion_puff = 0.9
    handle_kind = "recess"
    foliage_kind = "ball"
    _cases = None

    def slab(self, size, loc, rot=(0, 0, 0)):
        """A white board with pill-round edges (overlapping its neighbours by a few mm)."""
        w, d, h = size
        seg = 1 if max(size) < .4 else (2 if min(size) < .1 else 3)   # small boards (books, keys) stay cheap
        return [s.rbox((w + .02, d + .006, h + .012), loc, r=min(min(size) * .45, .06), seg=seg, rot=rot)]

    def carcass(self, size, loc, kick=0.06):
        """A white cabinet with big round corners on a black recessed plinth; the signature cuts a dark seam round
        it."""
        w, d, h = size
        x, y, z = loc
        if self._cases is None:
            self._cases = []
        self._cases.append((w, d, h, x, y, z, kick))
        out = [s.rbox((w, d, h - kick), (x, y, z + kick + (h - kick) / 2), r=min(.1, min(w, d) * .2), seg=3)]
        if kick > 0:
            out += [s.rbox((w - .06, d - .08, kick + .01), (x, y + .02, z + (kick + .01) / 2), r=min(.02, kick * .4), seg=2)]
        return out

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A pill leg."""
        x, y, z = top
        foot = (x + splay[0], y + splay[1], max(0.0, z - length))
        return capsule(top, foot, thick * .7, thick * .6, seg=12)

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Two white pill panels instead of four legs, on black foot pills."""
        out = []
        hd = half_d - inset
        for sx in (-1, 1):
            x = sx * (half_w - inset)
            out += [s.rbox((max(.05, thick * 1.2), 2 * hd, length + .01), (x, 0, top_z - length / 2 + .005), r=max(.024, thick * .55), seg=3)]
            out += capsule((x, -hd - .02, .03), (x, hd + .02, .03), .03, seg=10)
        return out

    def pot(self, r, h, loc):
        """An egg cup."""
        return [s.lathe([(0, 0), (r * .55, 0), (r * .8, h * .1), (r * 1.0, h * .55), (r * .97, h * .9), (r * .92, h), (0, h * .97)],
                        seg=24, loc=loc)]

    def foliage(self, centre, r, n=5, seed=1):
        """Lab-grown topiary: a few perfect spheres."""
        cx, cy, cz = centre
        out = [s.ball(r * .8, centre, seg=20)]
        for i in range(min(n, 3)):
            a = (i * 2.1 + seed)
            out.append(s.ball(r * .5, (cx + math.cos(a) * r * .55, cy + math.sin(a) * r * .55, cz + r * .2), seg=16))
        return out

    def shade(self, r_bottom, r_top, height, loc):
        """A half-egg shell."""
        return [s.lathe([(r_bottom, 0), (r_bottom * .98, height * .4), (r_top * 1.1, height * .85), (r_top * .6, height), (0, height * 1.02)],
                        seg=24, loc=loc)]

    def signature(self, model, P):
        cases, self._cases = self._cases or [], None
        # sensor modules: black seams and joints like the robots they ride on
        if model == "attach_seismic":
            P.add("Accent", ball(.035, (0, 0, .12), seg=16))
        elif not model.startswith(("robot_", "sensor_", "attach_", "security_", "laser_")):
            for w, d, h, x, y, z, kick in cases:   # a dark seam band round every cabinet at two thirds of its height
                if h > .4:
                    zz = z + kick + (h - kick) * .66
                    P.add("Accent", [s.loft([(zz - .013, w / 2 + .004, d / 2 + .004, 7.0, x, y),
                                             (zz + .013, w / 2 + .004, d / 2 + .004, 7.0, x, y)], seg=40)])
        return P


# =========================================================================== helpers


def capsule(p0, p1, r, r1=None, seg=16, k=3):
    """A pill from p0 to p1 (end radii r and r1)."""
    r1 = r if r1 is None else r1
    p0, p1 = Vector(p0), Vector(p1)
    n = max((p1 - p0).length, r + r1 + .002)
    prof = [(r * math.cos(-math.pi / 2 + i / k * math.pi / 2), -n / 2 + r + r * math.sin(-math.pi / 2 + i / k * math.pi / 2))
            for i in range(k + 1)]
    prof += [(r1 * math.cos(i / k * math.pi / 2), n / 2 - r1 + r1 * math.sin(i / k * math.pi / 2)) for i in range(k + 1)]
    return [align(s.lathe(prof, seg=seg), p0, p1)]


def egg(loc, a, b, down, up, axis="Z", z0=None, z1=None, seg=28, n=2.0, k=6):
    """An egg (half-axes a across, b deep; `down` below the centre, `up` above), optionally only between z0..z1."""
    def f(z):
        h = down if z < 0 else up
        return math.sqrt(max(0.0, 1 - (z / h) ** 2))
    zs = [-down * math.cos(i / k * math.pi / 2) for i in range(k)] + [up * math.sin(i / k * math.pi / 2) for i in range(k + 1)]
    lo_, hi_ = (-down if z0 is None else z0), (up if z1 is None else z1)
    zs = [lo_] + [z for z in zs if lo_ + 1e-4 < z < hi_ - 1e-4] + [hi_]
    rings = []
    for z in zs:
        k_ = f(z)
        rings.append((z, a * k_, b * k_, n) if k_ > 1e-3 else (z, 0, 0, n))
    return [s.loft(rings, seg=seg, axis=axis, loc=loc)]


def split_egg(P, loc, a, b, down, up, cuts, gap=.035, axis="Z", seg=28, n=2.0, shell="Body", core="Accent"):
    """White shells of an egg split at `cuts` (along the egg axis) with a black core showing in the seams."""
    edges = [-down] + sorted(cuts) + [up]
    for i in range(len(edges) - 1):
        z0 = edges[i] + (gap / 2 if i > 0 else 0)
        z1 = edges[i + 1] - (gap / 2 if i < len(edges) - 2 else 0)
        P.add(shell, egg(loc, a, b, down, up, axis, z0, z1, seg, n))
    P.add(core, egg(loc, a * .94, b * .94, down * .96, up * .96, axis, seg=seg, n=n))
    return P


def optic(P, loc, r, axis="-Y", depth=.06):
    """The one big round optic: a black bezel ring and a domed glowing lens."""
    a = {"-Y": Vector((0, -1, 0)), "Y": Vector((0, 1, 0)), "-Z": Vector((0, 0, -1)), "-X": Vector((-1, 0, 0)),
         "X": Vector((1, 0, 0))}[axis]
    c = Vector(loc)
    sg = 24 if r > .08 else 16
    P.add("Accent", [s.lathe([(r * 1.35, 0), (r * 1.4, depth * .5), (r * 1.25, depth), (r * 1.05, depth), (0, depth * .8)],
                             seg=sg, axis=axis, loc=c - a * depth * .6)])
    P.add("Eye", [s.lathe([(r, 0), (r * .95, r * .2), (r * .6, r * .42), (0, r * .5)], seg=sg, axis=axis, loc=c + a * depth * .2)])
    return P


def revolve(profile, seg=24, axis="Z", loc=(0, 0, 0)):
    """Solid of revolution from a CLOSED (r, z) profile (rings, tori)."""
    bm = bmesh.new()
    rows = [[bm.verts.new((r * math.cos(k / seg * math.tau), r * math.sin(k / seg * math.tau), z)) for k in range(seg)]
            for r, z in profile]
    for i in range(len(rows)):
        a, b = rows[i], rows[(i + 1) % len(rows)]
        for j in range(seg):
            bm.faces.new((a[j], a[(j + 1) % seg], b[(j + 1) % seg], b[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


def torus(R, r, loc=(0, 0, 0), axis="Z", seg=24, rseg=10, zs=1.0):
    return [revolve([(R + r * math.cos(k / rseg * math.tau), r * zs * math.sin(k / rseg * math.tau)) for k in range(rseg)],
                    seg, axis, loc)]


def ball(r, loc, seg=14):
    return [s.ball(r, loc, seg=seg)]


def soft_wheel(c, r, w, seg=18):
    prof = [(0, -w / 2), (r * .55, -w / 2), (r * .62, -w * .38), (r * .85, -w / 2), (r * .97, -w * .35), (r, 0),
            (r * .97, w * .35), (r * .85, w / 2), (r * .62, w * .38), (r * .55, w / 2), (0, w / 2)]
    return [s.lathe(prof, seg=seg, axis="X", loc=c)]


# =========================================================================== walkers

def guard(L):
    """The walking pill: one tall white capsule is head and torso at once, split by two dark seams, with the big
    optic high on its front; pill arms and legs hang on black ball joints."""
    P = D.Parts()
    split_egg(P, (0, 0, 1.47), .27, .23, .52, .57, [1.18 - 1.47, 1.62 - 1.47], gap=.04)
    optic(P, (0, -.165, 1.84), .1, depth=.07)
    P.add("Accent", L.lathe([(0, .86), (.2, .86), (.22, .9), (.2, .97), (0, .99)]))           # pelvis
    for sx in (-1, 1):
        sh, el, wr = (sx * .29, 0, 1.5), (sx * .35, -.02, 1.18), (sx * .35, -.07, .9)
        P.add("Accent", ball(.075, sh) + ball(.055, el) + ball(.045, wr))
        P.add("Body", capsule(sh, el, .058, .052, seg=16))
        P.add("Body", capsule(el, wr, .055, .07, seg=16))
        P.add("Body", [s.place(s.ball(.075, seg=20, scale=(.8, .95, 1.15)), (sx * .35, -.08, .82))])       # mitten
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, ball(.085, (x, 0, D.GUARD_HIP_Z)) + ball(.068, (x, 0, D.GUARD_KNEE)) + ball(.055, (x, 0, .1)))
        P.add(leg, capsule((x, 0, .89), (x, 0, .49), .085, .07))
        P.add(leg, capsule((x, 0, .42), (x, 0, .13), .072, .06))
        P.add(leg, [s.loft([(0, .075, .06, 2.2), (.1, .09, .065, 2.4), (.22, .07, .05, 2.2), (.26, 0, 0, 2.0)],
                           seg=24, axis="-Y", loc=(x, .07, .06), round_caps=.03)])
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hound(L):
    """A long lying pill split by two dark seams with an egg head: one optic and a long white snout with black
    nostrils; pill legs hang on big black hip balls."""
    P = D.Parts()
    split_egg(P, (0, 0, .84), .2, .2, .56, .56, [-.16, .22], gap=.035, axis="-Y", seg=22)
    P.add("Accent", capsule((0, -.42, .92), (0, -.62, 1.02), .075))
    split_egg(P, (0, -.72, 1.0), .14, .13, .15, .17, [.0], gap=.03, axis="-Y", seg=18)
    optic(P, (0, -.86, 1.04), .065, depth=.05)
    P.add("Body", capsule((0, -.78, .92), (0, -1.04, .86), .055, .045))
    P.add("Accent", ball(.04, (0, -1.04, .86), seg=16))
    P.add("Eye", [s.rbox((.06, .02, .02), (0, -1.075, .87), r=.007, seg=1)])
    P.add("Body", capsule((0, .5, .9), (0, .72, 1.06), .035, .03, seg=16))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        P.add("Body", [s.place(s.ball(.1, seg=12, scale=(1, 1.1, .85)), (x * .9, y, .72))])
        leg = f"AccentLeg{i}"
        P.add(leg, ball(.1, (x, y, D.HOUND_HIP), 12) + ball(.055, (x, y, D.HOUND_KNEE), 10))
        P.add(leg, capsule((x, y, .6), (x, y, .32), .065, .055, seg=12))
        P.add(leg, capsule((x, y, .3), (x, y, .06), .045, .04, seg=12))
        P.add(leg, [s.loft([(0, .05, .035, 2.2), (.08, .055, .035, 2.2), (.13, 0, 0, 2.0)], seg=16, axis="-Y",
                           loc=(x, y + .04, .035), round_caps=.015)])
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """A white half-egg dome on a black belly, one optic in front, pill legs with black knees."""
    P = D.Parts()
    P.add("Body", egg((0, .05, .12), .25, .3, .1, .2, z0=.03))
    P.add("Accent", egg((0, .05, .12), .24, .29, .1, .2, z1=.0, seg=24))
    optic(P, (0, -.22, .17), .065, depth=.05)
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", ball(.035, hip, 12) + capsule(hip, knee, .032, .028, seg=12) + ball(.04, knee, 14))
        P.add(f"AccentLeg{i}Lower", capsule(knee, foot + Vector((0, 0, -.01)), .026, .014, seg=12))
    return P


# =========================================================================== wheeled and tracked

def listener(L):
    """An upright egg on a low pill chassis, blind: two big round ear cups glow on its sides."""
    P = D.Parts()
    W = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", soft_wheel(c, .22, .12))
        P.add("Accent", capsule((c[0] * .3, c[1], .23), (c[0] * 1.02, c[1], .22), .045, seg=16))
    P.add("Body", [s.loft([(-.5, .22, .08, 2.6), (-.4, .32, .12, 3.0), (.4, .32, .12, 3.0), (.5, .22, .08, 2.6)], seg=24,
                          axis="-Y", loc=(0, 0, .36), round_caps=.05)])
    split_egg(P, (0, .04, .8), .28, .25, .34, .46, [.12], gap=.035, seg=22)
    for sx in (-1, 1):
        ax = "X" if sx > 0 else "-X"
        c = Vector((sx * .25, .06, .98))
        P.add("Body", [s.lathe([(.16, 0), (.2, .03), (.21, .08), (.18, .12), (0, .13)], seg=24, axis=ax, loc=c)])
        P.add("Accent", torus(.17, .025, c + Vector((sx * .125, 0, 0)), axis=ax, seg=24, rseg=8))
        P.add("Eye", [s.lathe([(.14, 0), (.13, .02), (0, .035)], seg=24, axis=ax, loc=c + Vector((sx * .12, 0, 0)))])
    P.add("Accent", [s.rbox((.26, .05, .04), (0, -.215, .86), r=.018, seg=2)])
    return P


def worker(L):
    """A pill cart with a white egg cab; two pill arms hold the black sample case up front."""
    P = D.Parts()
    W = [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", soft_wheel(c, .12, .1, seg=14))
        P.add("Accent", capsule((c[0] * .4, c[1], .13), (c[0] * 1.0, c[1], .12), .04, seg=10))
    split_egg(P, (0, .03, .27), .25, .15, .42, .42, [.0], gap=.03, axis="-Y", seg=16)
    for sx in (-1, 1):
        P.add("Accent", capsule((sx * .2, .33, .14), (sx * .2, -.33, .14), .055, seg=12))
    P.add("Body", egg((0, .22, .59), .2, .16, .2, .26, seg=16))
    P.add("Accent", [s.loft([(.63, .2, .16), (.65, .2, .16)], seg=16, loc=(0, .22, 0))])
    for sx in (-1, 1):
        P.add("Accent", ball(.05, (sx * .2, -.15, .38)) + ball(.045, (sx * .2, -.18, .66)))
        P.add("Body", capsule((sx * .2, -.15, .38), (sx * .2, -.18, .66), .045, seg=12) + capsule((sx * .2, -.18, .66), (sx * .2, -.16, .93), .04, seg=12))
    P.add("Accent", [s.rbox((.5, .44, .04), (0, -.15, .93), r=.015, seg=2)])
    P.add("Accent", [s.rbox((.46, .42, .36), (0, -.15, 1.13), r=.07, seg=2)])
    P.add("Body", [s.rbox((.48, .44, .06), (0, -.15, 1.15), r=.025, seg=2)])
    optic(P, (0, -.39, .36), .07, depth=.05)
    return P


def sweeper(L):
    """A white dome over a black spinning lidar ring (the whole waist turns) and a black base on three wheels;
    one optic in front."""
    P = D.Parts()
    W = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", soft_wheel(c, .08, .06))
        x, y, _ = c
        P.add("Accent", capsule((x - .055, y, .08), (x + .055, y, .08), .022, seg=12))
        for sx in (-1, 1):
            P.add("Accent", capsule((x + sx * .045, y, .08), (x + sx * .045, y, .17), .02, seg=12))
    P.add("Accent", L.lathe([(0, .13), (.3, .13), (.34, .15), (.34, .17), (.3, .19), (0, .19)], seg=32))
    P.add("AccentLidar", L.lathe([(0, .18), (.34, .18), (.37, .2), (.37, .24), (.34, .262), (0, .262)], seg=32)
          + L.box((.1, .06, .05), (0, -.37, .22)))
    P.add("Body", L.lathe([(0, .257), (.37, .257), (.4, .29), (.39, .38), (.33, .48), (.2, .55), (0, .57)], seg=32))
    optic(P, (0, -.36, .37), .06, depth=.05)
    for sx in (-1, 1):
        c = (sx * .27, -.25, .015)
        P.add("Accent", L.cyl(.12, .02, c) + capsule((c[0], c[1], .02), (sx * .18, -.17, .16), .018, seg=12))
    return P


def thumper(L):
    """A big lying egg on two white pill pods, a large optic in its nose and two white pestles stamping the floor."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .55
        P.add("Body", [s.place(s.lathe([(0, -.68), (.18, -.64), (.24, -.5), (.25, .5), (.18, .64), (0, .68)], seg=18, axis="Y"),
                               (x, 0, .29), scale=(.52, 1, 1))])
        P.add("Accent", [s.place(s.lathe([(0, -.62), (.16, -.58), (.22, -.45), (.22, .45), (.16, .58), (0, .62)], seg=18, axis="Y"),
                                 (x, 0, .22), scale=(.5, 1, 1))])
    split_egg(P, (0, .08, .76), .44, .34, .6, .5, [.05], gap=.04, axis="-Y", seg=24)
    for y in (-.2, .35):
        P.add("Accent", capsule((-.6, y, .44), (.6, y, .44), .065, seg=16))
    optic(P, (0, -.455, .8), .12, depth=.07)
    for sx in (-1, 1):
        x = sx * .3
        P.add("Accent", capsule((x * .7, -.2, .72), (x, -.85, .62), .055, seg=16) + ball(.075, (x, -.85, .62)))
        P.add("Body", capsule((x, -.85, .6), (x, -.85, .055), .06, .11))
        P.add("Accent", L.cyl(.15, .05, (x, -.85, .06)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
    return P


# =========================================================================== tall

def sorter(L):
    """The lens is the robot: one white egg on a short black neck, its face filled by a huge colour lens sunk in a
    black barrel, a dark seam low on the egg."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.3, 0), (.3, .03), (.26, .08), (.14, .13), (0, .14)], seg=32))
    P.add("Accent", torus(.29, .018, (0, 0, .03), seg=32, rseg=8))
    P.add("Accent", capsule((0, 0, .1), (0, .03, .7), .06, .055, seg=16))
    split_egg(P, (0, .04, 1.1), .3, .28, .48, .5, [-.27], gap=.035)
    P.add("Accent", [s.lathe([(.25, 0), (.255, .08), (.235, .11), (.21, .11), (0, .1)], seg=32, axis="-Y", loc=(0, -.13, 1.25))])
    P.add("Eye", [s.lathe([(.205, 0), (.19, .03), (.12, .05), (0, .055)], seg=32, axis="-Y", loc=(0, -.225, 1.25))])
    return P


def hunter(L):
    """The opened egg: a 2 m white egg glides on a black emitter; its crown is lifted and tilted back like a hood,
    the black core inside shows the optic; long pill arms end in black pincers."""
    P = D.Parts()
    c = (0, 0, 1.35)
    P.add("Body", egg(c, .42, .38, .85, .75, z1=.36))
    P.add("Accent", egg(c, .4, .36, .82, .72, z0=-.2, z1=.62))
    crown = egg((0, 0, 0), .42, .38, .85, .75, z0=.44)[0]
    bmesh.ops.rotate(crown, cent=Vector((0, .33, .44)), matrix=Euler((math.radians(-24), 0, 0)).to_matrix(), verts=crown.verts)
    P.add("Body", [s.place(crown, (0, 0, 1.35 + .04))])
    optic(P, (0, -.29, 1.88), .1, depth=.07)
    P.add("Accent", L.lathe([(0, .26), (.13, .3), (.2, .42), (.12, .55), (0, .58)], seg=24))
    P.add("Eye", L.lathe([(0, .2), (.2, .22), (.22, .26), (.12, .3), (0, .3)], seg=24))
    for sx in (-1, 1):
        sh, el, wr = (sx * .42, 0, 1.45), (sx * .55, -.04, 1.05), (sx * .5, -.28, .74)
        P.add("Accent", ball(.08, sh) + ball(.06, el) + ball(.055, wr))
        P.add("Body", capsule(sh, el, .065, .055) + capsule(el, wr, .055, .05))
        for k in (-1, 1):
            P.add("Accent", capsule(wr, (sx * .5 + k * .06, -.42, .6), .025, .015, seg=12))
    return P


# =========================================================================== devices

def drone(L):
    """A white pebble wing (a soft rounded square) carries the egg body and the four ring ducts at its corners;
    the optic hangs under the nose."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", [s.loft([(-.035, .4, .4, 3.2), (0, .44, .44, 3.2), (.03, .4, .4, 3.2)], seg=24, round_caps=.02)])
    P.add("Accent", [s.loft([(-.012, .452, .452, 3.2), (.012, .452, .452, 3.2)], seg=28)])
    split_egg(P, (0, 0, .03), .16, .08, .22, .25, [.02], gap=.03, axis="-Y", seg=16)
    for i, (x, y) in enumerate(rot):
        P.add("Body", torus(.2, .035, (x, y, .03), seg=16, rseg=6, zs=1.3))
        P.add("Accent", L.cyl(.032, .05, (x, y, .025)) + capsule((x * .72, y * .72, .02), (x, y, .025), .025, seg=12))
        P.add(f"AccentRotor{i}", L.rotor((x, y, .065), .32))
    P.add("Accent", ball(.06, (0, -.15, -.065)))
    optic(P, (0, -.22, -.08), .045, depth=.04)
    return P


def turret(L):
    """A wide low white dome with a thick glowing ring at its foot and a glowing collar on a black stalk; the head
    is one lying egg with a dark lens ring and two side bores (the head turns; the glow stays on the mount)."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.42, 0), (.43, .03), (.4, .09), (.3, .16), (.16, .2), (0, .21)], seg=36))
    P.add("Eye", torus(.42, .03, (0, 0, .045), seg=36, rseg=8))
    P.add("Accent", capsule((0, 0, .15), (0, 0, 1.12), .055, seg=16))
    P.add("Accent", L.lathe([(0, .94), (.11, .94), (.12, .97), (.12, 1.05), (.11, 1.08), (0, 1.08)], seg=24))
    P.add("Eye", torus(.12, .022, (0, 0, 1.01), seg=24, rseg=8))
    P.add("AccentHead", ball(.075, (0, 0, 1.16)))
    P.add("AccentHead", egg((0, .03, 1.3), .17, .14, .2, .27, axis="-Y"))
    P.add("AccentHead", torus(.085, .02, (0, -.205, 1.31), axis="-Y", seg=24, rseg=8))
    P.add("AccentHead", torus(.15, .018, (0, .03, 1.3), axis="-Y", seg=24, rseg=8))
    for sx in (-1, 1):
        P.add("AccentHead", capsule((sx * .17, .05, 1.28), (sx * .17, -.24, 1.28), .04, seg=16))
    return P


def vacuum(L):
    """A white disc whose room face (-z) swells into a shallow dome with one big optic; a dark seam rings it."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, .025), (.35, .025), (.36, .05), (.36, .08), (.32, .11), (0, .12)], seg=36))
    P.add("Accent", L.lathe([(0, .0), (.35, .0), (.35, .035), (0, .035)], seg=36))
    P.add("Body", L.lathe([(0, .01), (.34, .01), (.32, -.01), (.24, -.03), (.12, -.045), (0, -.05)], seg=36))
    optic(P, (0, 0, -.05), .1, axis="-Z", depth=.04)
    return P


def lidar(L):
    """A white egg post with a dark seam band; the black drum on top turns."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.17, 0), (.17, .03), (.1, .08), (.06, .15), (.055, .3), (.07, .36), (.08, .39), (0, .395)], seg=24))
    P.add("Eye", torus(.065, .014, (0, 0, .33), seg=24, rseg=6))
    P.add("AccentHead", L.lathe([(0, .385), (.08, .385), (.1, .41), (.1, .48), (.08, .52), (0, .525)], seg=24))
    P.add("AccentHead", L.box((.07, .05, .04), (0, -.085, .45)))
    return P


def security_camera(L):
    """A white pill camera on a black ball joint, one optic in its nose."""
    P = D.Parts()
    P.add("Body", L.box((.15, .06, .22), (0, .27, 0)))
    P.add("Accent", capsule((0, .27, 0), (0, .09, -.02), .025, seg=12) + ball(.045, (0, .08, -.02)))
    P.add("Accent", capsule((0, .16, -.02), (0, -.2, -.02), .075, .07, seg=24))
    optic(P, (0, -.2, -.02), .045, depth=.04)
    return P


def laser_emitter(L):
    """A white rounded post on a black foot, split by a dark seam; the emitter strip glows on its face."""
    P = D.Parts()
    P.add("Body", [s.rbox((.12, .12, 1.5), (0, 0, .83), r=.05, seg=3)])
    P.add("Accent", [s.rbox((.11, .11, 1.58), (0, 0, .79), r=.045, seg=2)])
    P.add("Accent", [s.rbox((.15, .15, .06), (0, 0, .03), r=.02, seg=2)])
    P.add("Eye", L.box((.05, .04, 1.4), (0, -.062, .85), r=.012))
    return P


def plate(L):
    """A white tile flush with the floor, a glowing seam around it and a black seam cross."""
    P = D.Parts()
    S, h = 1.1, .012
    P.add("Body", L.box((S - .07, S - .07, h), (0, 0, h / 2), r=.005))
    e = h + .004
    P.add("Accent", L.box((S - .1, .03, e), (0, 0, e / 2), r=.004) + L.box((.03, S - .1, e), (0, 0, e / 2), r=.004))
    b = .035
    P.add("Eye", L.box((S, b, e), (0, S / 2 - b / 2, e / 2), r=.004) + L.box((S, b, e), (0, -S / 2 + b / 2, e / 2), r=.004)
          + L.box((b, S - 2 * b, e), (S / 2 - b / 2, 0, e / 2), r=.004) + L.box((b, S - 2 * b, e), (-S / 2 + b / 2, 0, e / 2), r=.004))
    return P


def pir(L):
    """A white rounded wall plate with one small optic."""
    P = D.Parts()
    P.add("Body", [s.rbox((.16, .06, .16), (0, .04, 0), r=.028, seg=3)])
    optic(P, (0, -.0, 0), .045, depth=.05)
    return P


def attach_mic(L):
    """A small white egg on a black stalk; its seam glows."""
    P = D.Parts()
    P.add("Accent", L.cyl(.045, .02, (0, 0, .01), seg=16) + capsule((0, 0, .0), (0, 0, .28), .016, seg=10))
    P.add("Body", egg((0, 0, .32), .06, .06, .06, .07, seg=14, k=4))
    P.add("Eye", torus(.058, .012, (0, 0, .32), seg=14, rseg=5))
    return P


def attach_nose(L):
    """A white snout pill with a black tip and two glowing nostrils."""
    P = D.Parts()
    P.add("Body", capsule((0, .02, 0), (0, -.22, -.005), .07, .05, seg=14))
    P.add("Accent", ball(.045, (0, -.225, -.005), seg=12))
    P.add("Eye", [s.rbox((.025, .02, .02), (sx * .02, -.265, .005), r=.005, seg=1) for sx in (-1, 1)])
    return P


# =========================================================================== furniture and level pieces

def vending_machine(L):
    """A tall white pill cabinet with a round-cornered window, a black seam round its waist and a black keypad
    strip."""
    P = D.Parts()
    P.add("Body", [s.rbox((.9, .78, 1.86), (0, .01, .93), r=.16, seg=3)])
    P.add("Accent", [s.loft([(.53, .454, .394, 7.0, 0, .01), (.555, .454, .394, 7.0, 0, .01)], seg=40)])
    P.add("Accent", [s.rbox((.66, .03, 1.18), (-.1, -.375, 1.15), r=.1, seg=3)])
    P.add("Eye", [s.rbox((.6, .03, 1.1), (-.1, -.39, 1.15), r=.08, seg=3)])
    P.add("Accent", [s.rbox((.12, .03, .5), (.32, -.385, 1.2), r=.05, seg=3), s.rbox((.6, .03, .14), (-.1, -.385, .3), r=.06, seg=3)])
    return P


def water_cooler(L):
    """A white egg stand with a black seam; the bottle a glass sphere."""
    P = D.Parts()
    P.add("Body", egg((0, 0, .55), .17, .17, .56, .45, seg=28))
    P.add("Accent", [s.loft([(.6, .175, .175), (.625, .175, .175)], seg=28)])
    P.add("Accent", capsule((-.05, -.17, .62), (.05, -.17, .62), .025, seg=10))
    P.add("Eye", [s.ball(.155, (0, 0, 1.215), seg=24, scale=(.97, .97, 1.45))])
    return P


def coffee_machine(L):
    """An egg on its side, cut flat at the base: a black spout under its belly and one small optic for a display."""
    P = D.Parts()
    P.add("Body", egg((0, -.01, .25), .175, .2, .25, .25, axis="Z", z0=-.25, seg=28))
    P.add("Accent", [s.loft([(.33, .17, .195), (.35, .17, .195)], seg=28, loc=(0, -.01, 0))])
    P.add("Accent", capsule((0, -.18, .2), (0, -.235, .17), .03, seg=10))
    P.add("Accent", [s.lathe([(0, 0), (.09, 0), (.1, .02), (0, .025)], seg=20, loc=(0, -.16, .0))])
    P.add("Eye", [s.lathe([(.04, 0), (.035, .01), (0, .016)], seg=16, axis="-Y", loc=(0, -.165, .4))])
    return P


def plant(L):
    """Lab topiary: an egg cup, a white stem, three perfect spheres."""
    P = D.Parts()
    P.add("Accent", L.pot(.2, .45, (0, 0, 0)))
    P.add("Body", capsule((0, 0, .4), (0, 0, 1.0), .025, seg=10))
    P.add("Body", [s.ball(.3, (0, 0, .78), seg=24), s.ball(.21, (.06, .03, 1.18), seg=20), s.ball(.16, (-.17, -.17, 1.0), seg=16)])
    return P


def floor_lamp(L):
    """A white pill stem on a domed foot; an egg-shell shade over a glowing disc."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.18, 0), (.18, .02), (.12, .05), (0, .06)], seg=28) + capsule((0, 0, .03), (0, 0, 1.4), .02, seg=10))
    P.add("Accent", [s.lathe([(.2, 1.36), (.2, 1.42), (.17, 1.55), (.1, 1.63), (0, 1.65), (0, 1.62), (.08, 1.6), (.15, 1.52), (.18, 1.4),
                             (.17, 1.37)], seg=28)])
    P.add("Eye", [s.lathe([(0, 1.35), (.14, 1.35), (.14, 1.37), (0, 1.375)], seg=24)])
    return P


def desk_lamp(L):
    """A pill arm on a white puck, an egg head with a black seam over the bulb."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.075, 0), (.078, .015), (.05, .03), (0, .032)], seg=20))
    P.add("Body", capsule((0, .02, .02), (0, .03, .24), .014, seg=10) + capsule((0, .03, .24), (0, -.12, .32), .013, seg=10))
    P.add("Accent", ball(.022, (0, .03, .24), seg=10))
    P.add("Accent", egg((0, -.16, .3), .065, .065, .035, .06, z0=-.03, seg=20))
    P.add("Eye", [s.ball(.03, (0, -.16, .27), seg=12)])
    return P


def safe(L):
    """A white rounded block whose round door is a black ring with a glowing dial in the middle."""
    P = D.Parts()
    P.add("Body", [s.rbox((.8, .7, .96), (0, 0, .5), r=.12, seg=3)])
    P.add("Accent", torus(.3, .03, (0, -.35, .5), axis="-Y", seg=36, rseg=8))
    P.add("Body", [s.lathe([(.29, 0), (.28, .025), (.2, .04), (0, .045)], seg=36, axis="-Y", loc=(0, -.34, .5))])
    P.add("Accent", [s.lathe([(.1, 0), (.1, .02), (.08, .035), (0, .04)], seg=24, axis="-Y", loc=(0, -.38, .5))])
    P.add("Eye", [s.lathe([(.05, 0), (.045, .01), (0, .016)], seg=16, axis="-Y", loc=(0, -.415, .5))])
    P.add("Eye", [s.rbox((.06, .02, .06), (-.24, -.36, .82), r=.02, seg=2)])
    return P


def trash_bin(L):
    """An egg cut open at the top, a black seam lid ring."""
    P = D.Parts()
    P.add("Body", egg((0, 0, .26), .19, .19, .26, .3, z0=-.26, z1=.15, seg=28))
    P.add("Accent", [revolve([(.16, .4), (.195, .4), (.195, .445), (.16, .445)], seg=28)])
    P.add("Body", [s.loft([(.39, .182, .182), (.41, .182, .182)], seg=28)])
    return P


def chair(L):
    """An egg chair: a white shell egg opened at the front on a black stem and disc foot, a black cushion inside."""
    P = D.Parts()
    shell = egg((0, .02, .7), .3, .28, .36, .36, seg=28)[0]
    cutter = s.rbox((.7, .4, .5), (0, -.25, .78), r=0, seg=1, rot=(math.radians(-20), 0, 0))
    P.add("Body", [s.boolean(shell, cutter)])
    P.add("Accent", egg((0, .04, .7), .26, .24, .3, .32, z1=-.18, seg=24))
    P.add("Accent", [s.rbox((.42, .4, .1), (0, -.02, .5), r=.045, seg=2)])
    P.add("Accent", capsule((0, 0, .02), (0, 0, .4), .035, seg=12))
    P.add("Accent", [s.lathe([(0, .008), (.28, .008), (.29, .02), (.2, .04), (0, .045)], seg=28)])
    return P


def sofa(L):
    """Two fat pill cushions and a pill back on a black plinth, capsule arms."""
    P = D.Parts()
    P.add("Accent", [s.rbox((1.86, .66, .14), (0, 0, .07), r=.05, seg=2)])
    for x in (-.45, .45):
        P.add("Body", [s.rbox((.88, .72, .3), (x, -.06, .3), r=.14, seg=3)])
    P.add("Body", capsule((-.86, .2, .65), (.86, .2, .65), .23, seg=20))
    for x in (-.98, .98):
        P.add("Body", capsule((x, -.28, .42), (x, .3, .42), .12, seg=16) + capsule((x, .0, .02), (x, .0, .42), .1, seg=16))
    return P


def armchair(L):
    """A pill seat and a tall pill back between two capsule arms on a black plinth."""
    P = D.Parts()
    P.add("Accent", [s.rbox((.62, .62, .14), (0, 0, .07), r=.05, seg=2)])
    P.add("Body", [s.rbox((.58, .66, .3), (0, -.05, .29), r=.14, seg=3)] + capsule((-.22, .15, .63), (.22, .15, .63), .22, seg=20))
    for x in (-.36, .36):
        P.add("Body", capsule((x, -.27, .45), (x, .26, .45), .08, seg=16) + capsule((x, 0, .02), (x, 0, .45), .07, seg=16))
    return P


def bean_bag(L):
    """A white egg pouf split by a black seam."""
    P = D.Parts()
    P.add("Body", egg((0, 0, .3), .45, .45, .3, .39, seg=32))
    P.add("Body", [s.loft([(.3, .455, .455), (.32, .455, .455)], seg=32)])
    return P


def round_table(L):
    """A white disc with a black seam edge on an egg pedestal."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, .71), (.48, .71), (.5, .735), (.48, .76), (0, .76)], seg=40))
    P.add("Accent", [s.loft([(.727, .502, .502), (.743, .502, .502)], seg=40)])
    P.add("Accent", egg((0, 0, .32), .22, .22, .32, .4, seg=28))
    return P


def meeting_table(L):
    """A long pill-edged top on two white egg pedestals with black seams."""
    P = D.Parts()
    P.add("Body", [s.rbox((3.2, 1.3, .06), (0, 0, .75), r=.028, seg=3)])
    for x in (-1.1, 1.1):
        P.add("Accent", egg((x, 0, .3), .25, .32, .3, .45, seg=28))
        P.add("Body", [s.loft([(.36, .245, .315), (.38, .245, .315)], seg=28, loc=(x, 0, 0))])
    return P


def coat_rack(L):
    """A white pill stem on a domed foot with ball-tipped hooks; two soft coats."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.24, 0), (.24, .02), (.15, .05), (0, .06)], seg=28) + capsule((0, 0, .03), (0, 0, 1.8), .025, seg=12))
    for a in (0.0, 2.1, 4.2):
        tip = (math.cos(a) * .2, math.sin(a) * .2, 1.74)
        P.add("Accent", capsule((0, 0, 1.66), tip, .014, seg=10) + ball(.025, tip, seg=10))
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.36)) + L.cushion((.3, .12, .55), (-.08, .1, 1.38), rot=(0, 0, 2.1)))
    return P


def whiteboard(L):
    """A big round-cornered board with a black seam frame on two pill legs with egg feet."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.8, .04, 1.0), (0, 0, 1.5), r=.019, seg=3)])
    P.add("Accent", [s.loft([(1.49, .91, .025, 6.0), (1.51, .91, .025, 6.0)], seg=40)])
    for x in (-.85, .85):
        P.add("Accent", capsule((x, 0, .03), (x, 0, 1.97), .03, seg=12) + egg((x, 0, .0), .07, .04, .0, .05, z0=0, seg=16))
    return P


def ceiling_lamp(L):
    """A white pill hanging flat under the ceiling, its underside one glowing pill."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.24, .66, .05), (0, 0, -.025), r=.024, seg=3)])
    P.add("Accent", [s.loft([(-.06, .6, .3, 3.0), (-.045, .6, .3, 3.0)], seg=40)])
    P.add("Eye", [s.loft([(-.08, .55, .26, 3.0), (-.05, .58, .28, 3.0)], seg=40)])
    return P


def hatch(L):
    """A round white lid with a black seam ring and pill grips; a glowing ring round it."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.76, 0), (.78, .03), (.74, .06), (0, .065)], seg=48))
    P.add("Accent", torus(.6, .02, (0, 0, .065), seg=48, rseg=6))
    for y in (-.25, 0, .25):
        P.add("Accent", capsule((-.45, y, .07), (.45, y, .07), .02, seg=10))
    P.add("Eye", [revolve([(.77, 0), (.9, 0), (.9, .02), (.77, .02)], seg=48)])
    return P


def server_rack(L):
    """A white pill tower with a black seam, its door a column of round glowing ports."""
    P = D.Parts()
    P.add("Body", [s.rbox((.7, 1.0, 1.96), (0, 0, .98), r=.15, seg=3)])
    P.add("Body", [s.loft([(1.6, .352, .502, 6.0), (1.63, .352, .502, 6.0)], seg=40)])
    for i in range(11):
        z = .2 + i * .14
        for x in (-.15, 0, .15):
            P.add("Eye", [s.lathe([(.035, 0), (.03, .01), (0, .015)], seg=12, axis="-Y", loc=(x, -.495, z))])
    return P


def reception_desk(L):
    """A counter whose front is half a lying pill; a white work top and a monitor behind."""
    P = D.Parts()
    P.add("Body", [s.place(s.loft([(-1.2, .55, .15, 2.0), (1.2, .55, .15, 2.0)], seg=28, axis="X", round_caps=.14), (0, -.33, .55))])
    P.add("Accent", [s.loft([(.89, 1.08, .125, 6.0), (.91, 1.08, .125, 6.0)], seg=40, loc=(0, -.33, 0))])
    P.add("Body", [s.rbox((2.3, .66, .04), (0, .07, .74), r=.019, seg=3), s.rbox((.12, .62, .74), (-1.1, .08, .37), r=.05, seg=3),
                   s.rbox((.12, .62, .74), (1.1, .08, .37), r=.05, seg=3)])
    acc, glow = L.screen((.5, .32), (.4, .17, .96), "-Y")
    P.add("Eye", glow + acc + L.slab((.06, .05, .2), (.4, .21, .83)))
    return P


def arcade_machine(L):
    """A white egg-shouldered cabinet: a round-cornered body, a hood of half an egg over a sunk screen, a black
    control pill."""
    P = D.Parts()
    P.add("Body", [s.rbox((.7, .74, 1.3), (0, .03, .65), r=.12, seg=3)])
    P.add("Body", egg((0, .0, 1.3), .36, .38, .0, .5, z0=0, seg=28))
    P.add("Accent", capsule((-.3, -.42, .97), (.3, -.42, .97), .08, seg=16))
    P.add("Accent", [s.rbox((.56, .06, .5), (0, -.33, 1.35), r=.12, seg=3, rot=(math.radians(-12), 0, 0))])
    P.add("Eye", [s.rbox((.48, .03, .4), (0, -.36, 1.35), r=.1, seg=3, rot=(math.radians(-12), 0, 0))])
    return P


def printer(L):
    """A white pill copier: a round-cornered body, a pill lid on top, the panel glowing on the lid's front."""
    P = D.Parts()
    P.add("Body", L.carcass((.6, .6, .95), (0, 0, 0), kick=.04))
    P.add("Body", [s.rbox((.56, .54, .14), (0, .02, 1.0), r=.06, seg=3)])
    P.add("Accent", L.slab((.36, .3, .03), (-.1, -.38, .82), rot=(math.radians(8), 0, 0)) + L.door((.5, .3), (0, -.31, .3)))
    P.add("Eye", [s.rbox((.16, .03, .06), (.15, -.255, 1.0), r=.012, seg=2)])
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_plate": plate, "security_camera": security_camera, "laser_emitter": laser_emitter,
    "sensor_pir": pir, "attach_mic": attach_mic, "attach_nose": attach_nose,
    "vending_machine": vending_machine,
    "water_cooler": water_cooler,
    "coffee_machine": coffee_machine,
    "plant": plant,
    "floor_lamp": floor_lamp,
    "desk_lamp": desk_lamp,
    "safe": safe,
    "trash_bin": trash_bin,
    "chair": chair,
    "sofa": sofa,
    "armchair": armchair,
    "bean_bag": bean_bag,
    "round_table": round_table,
    "meeting_table": meeting_table,
    "coat_rack": coat_rack,
    "whiteboard": whiteboard,
    "ceiling_lamp": ceiling_lamp,
    "hatch": hatch,
    "server_rack": server_rack,
    "reception_desk": reception_desk,
    "arcade_machine": arcade_machine,
    "printer": printer,
}
