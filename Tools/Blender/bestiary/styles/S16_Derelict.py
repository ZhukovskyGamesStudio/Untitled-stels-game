"""S16 Derelict — «Заброшенный комплекс» (Sasha's style): decommissioned space-station security synths (scuffed PBR).

Smooth station-made armour shells, but nothing is whole: shells are cracked open over dark machinery, ribcages and
spines show where panels fell off, every machine has one limb broken or replaced by a bare strut or pipe, cables hang
loose, lenses are dim — usually one of two still lit. The asymmetry is the style: no other world's robots are lopsided.
"""
import math

import bmesh
from mathutils import Euler, Matrix, Vector

import shapes as s
from lang import Lang, align
import designs as D

V = Vector


# =========================================================================== helpers (local to this style)

def _to_axis(bm, a, loc):
    q = V((0, 0, 1)).rotation_difference(V(a).normalized())
    bmesh.ops.rotate(bm, cent=V((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=V(loc), verts=bm.verts)
    return bm


def lathe_between(profile, p0, p1, seg=16):
    p0, p1 = V(p0), V(p1)
    L = (p1 - p0).length
    bm = s.lathe([(r, t * L) for r, t in profile], seg=seg)
    return _to_axis(bm, (p1 - p0).normalized(), p0)


def tilt(bms, center, rot):
    """Rotate pieces about a centre (a slumped head, a sagging dish)."""
    m = Euler(rot, "XYZ").to_matrix()
    for bm in bms:
        bmesh.ops.rotate(bm, cent=V(center), matrix=m, verts=bm.verts)
    return bms


def crack(bm, loc, size=0.12, seed=0, depth=1.0):
    """Break a jagged notch out of a shell: two rotated cutters."""
    loc = V(loc)
    k = seed * 1.7
    for i, (dx, dz, rz, rx) in enumerate(((0, 0, 0.5 + k, 0.4), (size * 0.45, -size * 0.35, -0.3 + k, -0.5))):
        cutter = s.rbox((size * (1.0 - 0.3 * i), size * depth, size * (0.7 + 0.2 * i)), loc + V((dx, 0, dz)), r=0,
                        rot=(rx, 0.6 * (i + 1), rz))
        bm = s.boolean(bm, cutter)
    return bm


def cable(points, r=0.012):
    return s.tube([tuple(p) for p in points], r, res=1, segments_per=5)


def arc(center, radius, a0, a1, r=0.02, plane="XY", n=10):
    """An open ring segment (ribs, a broken bumper)."""
    c = V(center)
    pts = []
    for k in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * k / n)
        u, v = math.cos(a) * radius, math.sin(a) * radius
        pts.append(c + {"XY": V((u, v, 0)), "XZ": V((u, 0, v)), "YZ": V((0, u, v))}[plane])
    return s.tube([tuple(p) for p in pts], r, res=1, segments_per=4)


# =========================================================================== the language

class Style(Lang):
    name = "S16_Derelict"
    seg = 16
    edge = 0.02
    soft = 0.25
    sharp = 35.0
    hull_kind = "rounded"
    limb_kind = "tube"
    eye_kind = "lens"

    def rings(self, n=None):
        return self.seg if n is None else min(n, self.seg)

    def loft(self, rings, axis="Z", loc=(0, 0, 0), round_caps=0.0, seg=None):
        return [s.loft(rings, seg=seg or 16, axis=axis, loc=loc, round_caps=round_caps)]

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        return [s.ball(radius, loc, seg=14 if radius > .08 else 10, scale=scale)]

    def cyl(self, radius, depth, loc=(0, 0, 0), axis="Z", radius2=None, rot=(0, 0, 0), seg=None):
        seg = seg or (16 if radius > .1 else 12 if radius > .04 else 8)
        r = min(self.r(min(radius * 2, depth)) * 0.6, 0.01)
        return [s.rcyl(radius, depth, loc, axis=axis, seg=seg, r=r, bevel_seg=1, radius2=radius2, rot=rot)]

    def wheel(self, center, radius, width):
        c = V(center)
        tyre = s.lathe([(radius * 0.62, -width / 2), (radius * 0.9, -width / 2), (radius, -width * 0.3), (radius, width * 0.3),
                        (radius * 0.9, width / 2), (radius * 0.62, width / 2)], seg=16, axis="X", loc=c)
        hub = s.rcyl(radius * 0.66, width * 0.86, c, axis="X", seg=16, r=0.006, bevel_seg=1)
        cap = s.rcyl(radius * 0.3, width * 1.08, c, axis="X", seg=8, r=0.006, bevel_seg=1)
        return [tyre, hub, cap]

    def track(self, size, loc):
        """The shared crawler track with four rollers instead of six (triangle budget)."""
        w, d, h = size
        x, y, z = loc
        r = h / 2
        out = [s.loft([(-w / 2, d / 2, r, 6.0), (w / 2, d / 2, r, 6.0)], seg=24, axis="X", loc=loc)]
        for k in range(4):
            yy = y - d / 2 + r + (d - 2 * r) * k / 3
            out.append(s.rcyl(r * 0.62, w * 1.1, (x, yy, z), axis="X", seg=12, r=0.008, bevel_seg=1))
        return out


    def shell(self, rings, axis="Z", loc=(0, 0, 0), caps=0.04):
        """A smooth station armour shell."""
        return self.loft(rings, axis=axis, loc=loc, round_caps=caps)

    def signature(self, model, P):
        if model == "laser_emitter":
            P.add("Body", [cable([(.05, .06, 1.4), (.1, .1, 1.0), (.07, .09, .7)], .012)])
        if model == "security_camera":
            P.add("Accent", [cable([(.04, .25, -.05), (.07, .22, -.25), (.05, .24, -.38)], .01)])
        if model == "sensor_pir":
            P.add("Body", [cable([(.06, .05, -.06), (.07, .02, -.2), (.06, .03, -.28)], .008)])
        return P


# =========================================================================== walkers

def guard(L):
    """The station synth: smooth armour shells, the chest cracked open over machinery, a bare waist of rings and
    pistons, a slumped egg head with one live lens, the right arm replaced by a bare manipulator with cables hanging
    from the socket, the right shin replaced by a piston."""
    P = D.Parts()
    P.add("Body", L.shell([(.84, .18, .13, 2.6), (.94, .2, .145, 2.8), (1.02, .17, .13, 2.6)], caps=.03))      # pelvis
    P.add("Accent", L.cyl(.075, .34, (0, .02, 1.1)))
    for z in (1.02, 1.09, 1.16):
        P.add("Accent", L.cyl(.13, .025, (0, .02, z)))
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .12, 0, .98), (sx * .14, -.02, 1.24), .025, kind="piston"))
    chest = L.shell([(1.2, .23, .15, 2.6, 0, .01), (1.38, .31, .19, 2.8, 0, -.01), (1.55, .32, .19, 2.8), (1.67, .24, .15, 2.4)])[0]
    P.add("Body", [crack(chest, (.17, -.18, 1.47), .14)])
    P.add("Accent", L.cyl(.045, .12, (.14, -.1, 1.47), axis="Y") + L.cyl(.03, .14, (.2, -.08, 1.42), axis="Y"))
    P.add("Body", L.box((.34, .16, .36), (0, .2, 1.45), rot=(0, 0, math.radians(4))))                        # back pack
    P.add("Accent", L.cyl(.03, .4, (-.1, .29, 1.45)))
    # slumped head
    head = L.shell([(1.73, .11, .13, 2.2), (1.84, .145, .17, 2.4, 0, -.02), (1.96, .135, .16, 2.3, 0, -.01), (2.03, .09, .1, 2.0)], caps=.03)
    face = L.box((.22, .06, .12), (0, -.17, 1.84), rot=(math.radians(-10), 0, 0))
    glow = L.ball(.032, (-.055, -.2, 1.86))
    socket = L.cyl(.032, .03, (.055, -.195, 1.86), axis="Y")
    hc = (0, 0, 1.78)
    tilt(head + face + glow + socket, hc, (math.radians(-8), math.radians(12), 0))
    P.add("Body", head).add("Accent", face + socket).add("Eye", glow)
    P.add("Accent", L.cyl(.055, .14, (0, 0, 1.72)))
    P.add("Accent", [cable([(-.06, .1, 1.62), (-.08, .06, 1.75), (-.05, .05, 1.82)], .014)])
    # left arm: armoured, a heavy pauldron the other side has lost
    P.add("Body", L.ball(.11, (-.38, 0, 1.58), scale=(1, 1.1, .9)))
    P.add("Body", L.ball(.16, (-.4, 0, 1.67), scale=(1.05, 1.15, .62)))
    P.add("Accent", L.cyl(.17, .03, (-.42, 0, 1.62), rot=(0, math.radians(-20), 0)))
    P.add("Body", L.rod((-.4, 0, 1.52), (-.43, -.02, 1.22), .06))
    P.add("Accent", L.ball(.055, (-.43, -.02, 1.21)))
    P.add("Body", L.shell([(.86, .06, .065, 2.4, -.43, -.07), (1.03, .075, .08, 2.6, -.43, -.05), (1.2, .06, .065, 2.4, -.43, -.02)], caps=.02))
    P.add("Accent", L.box((.1, .12, .13), (-.43, -.08, .8)))
    # right arm: a bare manipulator
    P.add("Accent", L.cyl(.08, .1, (.33, 0, 1.56), axis="X"))
    for dy in (-.025, .025):
        P.add("Accent", L.rod((.36, dy, 1.55), (.46, -.06 + dy, 1.2), .022))
    P.add("Accent", L.cyl(.05, .1, (.46, -.06, 1.2), axis="X"))
    P.add("Accent", L.rod((.46, -.06, 1.2), (.44, -.24, .98), .025))
    for dx in (-.035, .035):
        P.add("Accent", L.rod((.44 + dx * .5, -.24, .98), (.44 + dx, -.32, .88), .016))
    P.add("Accent", [cable([(.37, .04, 1.52), (.45, .08, 1.32), (.4, .12, 1.12)], .012)])
    P.add("Accent", [cable([(.36, -.02, 1.5), (.39, .02, 1.36), (.35, .04, 1.26)], .01)])
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.085, (x, 0, .9)))
        P.add(leg, L.shell([(.47, .075, .085, 2.6, x, 0), (.68, .095, .105, 2.8, x, -.005), (.92, .09, .1, 2.6, x, 0)], caps=.02))
        P.add(leg, L.cyl(.062, .16, (x, 0, D.GUARD_KNEE), axis="X"))
        if sx < 0:
            P.add(leg, L.shell([(.1, .06, .07, 2.4, x, 0), (.28, .075, .095, 2.6, x, .02), (.45, .065, .075, 2.4, x, 0)], caps=.02))
            P.add(leg, L.shell([(.0, .07, .16, 2.6, x, -.05), (.08, .065, .15, 2.6, x, -.05), (.12, .05, .08, 2.4, x, -.02)], caps=.02))
        else:
            P.add(leg, L.rod((x, 0, .44), (x, .0, .1), .04, kind="piston"))
            P.add(leg, L.box((.13, .24, .06), (x, -.04, .03)))
            P.add(leg, L.cyl(.05, .1, (x, 0, .1), axis="X"))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """The dragged torso: no legs left — a cracked armoured torso hung on a station repulsor ring, its spine and
    cables dangling to the floor; the left arm long with three claws, the right torn off at the elbow."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.36, .04), (.36, .1), (.3, .14), (0, .14)], loc=(0, 0, .94)))     # repulsor ring
    P.add("Eye", L.lathe([(0, 0), (.27, 0), (.27, .025), (0, .025)], loc=(0, 0, .925)))
    pts = [V((0, .03 + .1 * t * t, .96 - .66 * t)) for t in [k / 9 for k in range(10)]]
    P.add("Accent", [s.tube([tuple(p) for p in pts], .022, res=1)])
    for k, p in enumerate(pts[1:]):
        P.add("Body", L.cyl(.05 - k * .002, .035, tuple(p), seg=8))                                          # vertebrae
    for x, y, end in ((.16, -.1, .45), (-.14, .08, .55), (.05, .2, .38), (-.2, -.12, .7)):
        P.add("Accent", [cable([(x, y, .95), (x * 1.2, y * 1.3, (.95 + end) / 2), (x * 1.1, y * 1.5 + .03, end)], .012)])
    torso = L.shell([(1.04, .2, .15, 2.6, 0, .02), (1.3, .27, .18, 2.8, 0, 0), (1.6, .36, .22, 2.8, 0, -.03),
                     (1.82, .36, .21, 2.6, 0, -.03), (1.93, .24, .16, 2.4, 0, -.02)], caps=.05)[0]
    P.add("Body", [crack(torso, (-.2, -.22, 1.56), .16, seed=1)])
    P.add("Accent", L.cyl(.06, .14, (-.16, -.14, 1.56), axis="Y"))
    # head hung low and forward
    P.add("Accent", L.rod((0, -.02, 1.86), (0, -.14, 1.96), .05))
    head = L.shell([(0, .12, .11, 2.4), (.15, .13, .12, 2.6), (.32, .07, .07, 2.2)], axis="-Y", loc=(0, -.02, 1.96), caps=.03)
    glow = L.ball(.03, (-.04, -.335, 1.94))
    dead = L.ball(.025, (.05, -.325, 1.95))
    tilt(head + glow + dead, (0, -.1, 1.96), (math.radians(10), math.radians(-10), 0))
    P.add("Body", head).add("Eye", glow).add("Accent", dead)
    # left arm: long, clawed
    P.add("Body", L.ball(.11, (-.42, 0, 1.78), scale=(1, 1.1, .9)))
    P.add("Body", L.rod((-.43, 0, 1.74), (-.5, -.1, 1.38), .06))
    P.add("Accent", L.ball(.055, (-.5, -.1, 1.37)))
    P.add("Body", L.rod((-.5, -.1, 1.37), (-.46, -.42, 1.12), .065))
    P.add("Accent", L.box((.12, .14, .12), (-.46, -.44, 1.1)))
    for dx in (-.05, 0, .05):
        P.add("Accent", [lathe_between([(0, 0), (.022, 0), (.016, .6), (0, 1)], (-.46 + dx, -.48, 1.08), (-.46 + dx * 1.6, -.74, .84), seg=8)])
    # right arm: torn at the elbow
    P.add("Body", L.ball(.11, (.42, 0, 1.78), scale=(1, 1.1, .9)))
    P.add("Body", L.rod((.43, 0, 1.74), (.5, -.06, 1.42), .06))
    P.add("Accent", L.cyl(.055, .06, (.5, -.06, 1.4)))
    for dx, end in ((-.02, 1.08), (.03, 1.16), (0, 1.0)):
        P.add("Accent", [cable([(.5 + dx, -.06, 1.4), (.52 + dx, -.04, (1.4 + end) / 2), (.5 + dx * 2, -.02, end)], .011)])
    return P


def hound(L):
    """The maintenance quadruped: an armoured chest shell, the back half stripped to a ribcage over a core, a hip
    shell, a smooth wedge head with one live lens, the rear left leg replaced by a bare strut."""
    P = D.Parts()
    P.add("Body", L.shell([(0, .19, .2, 2.6, 0, .02), (.25, .19, .21, 2.6, 0, .03), (.44, .15, .16, 2.4, 0, .01)],
                          axis="-Y", loc=(0, .02, .84), caps=.05))
    P.add("Accent", L.cyl(.08, .44, (0, .2, .84), axis="Y"))                                                    # core
    P.add("Accent", L.rod((0, -.05, 1.0), (0, .44, .96), .028))                                                # spine
    for y in (.08, .17, .26):
        P.add("Accent", [arc((0, y, .84), .16, -35, 215, .016, plane="XZ")])                                  # ribs
    P.add("Body", L.shell([(0, .15, .16, 2.4), (.2, .13, .13, 2.4)], axis="Y", loc=(0, .3, .83), caps=.04))   # hip shell
    P.add("Accent", [cable([(.13, .17, .75), (.15, .2, .6), (.12, .24, .52)], .012)])
    P.add("Accent", L.rod((0, -.36, .92), (0, -.56, 1.0), .06))
    head = L.shell([(0, .11, .12, 2.6), (.2, .11, .1, 2.6), (.44, .05, .05, 2.2)], axis="-Y", loc=(0, -.52, 1.0), caps=.03)
    glow = L.ball(.026, (-.09, -.72, 1.07))
    dead = L.cyl(.025, .03, (.09, -.72, 1.07), axis="X")
    nose = L.box((.05, .02, .02), (0, -.965, 1.0), r=.006)
    tilt(head + glow + dead + nose, (0, -.52, 1.0), (0, math.radians(-8), 0))
    P.add("Body", head).add("Eye", glow + nose).add("Accent", dead)
    P.add("Accent", L.rod((.04, -.62, 1.1), (.07, -.6, 1.2), .012))                                            # broken antenna
    P.add("Accent", L.rod((0, .46, .95), (0, .58, 1.06), .02))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add("Body", L.ball(.09, (x * .9, y, .7)))
        P.add(leg, L.ball(.07, (x, y, .63)))
        if i == 3:     # replaced: a bare strut
            P.add(leg, L.rod((x, y, .62), (x, y, D.HOUND_KNEE), .028))
            P.add(leg, L.cyl(.04, .09, (x, y, D.HOUND_KNEE), axis="X"))
            P.add(leg, L.rod((x, y, D.HOUND_KNEE), (x, y, .03), .022))
            P.add(leg, L.cyl(.05, .03, (x, y, .015)))
        else:
            P.add(leg, L.shell([(D.HOUND_KNEE, .045, .05, 2.4, x, y), (.48, .065, .08, 2.6, x, y), (.66, .07, .08, 2.4, x, y)], caps=.02))
            P.add(leg, L.ball(.045, (x, y, D.HOUND_KNEE)))
            P.add(leg, L.rod((x, y, D.HOUND_KNEE), (x, y, .05), .035))
            P.add(leg, L.shell([(0, .04, .07, 2.4, x, y - .03), (.05, .035, .06, 2.4, x, y - .03)], caps=.015))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def worker(L):
    """The cargo loader: a shell chassis with a cab whose window is split by a crack, bare mast rails, the crate
    slid askew on the forks, one headlamp out, a strap hanging."""
    P = D.Parts()
    D.wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    P.add("Body", L.shell([(.18, .27, .37, 3.0), (.3, .29, .39, 3.2), (.4, .26, .36, 3.0)], caps=.03))
    for k, (x, y) in enumerate(((.31, .25), (-.31, .25), (.31, -.25), (-.31, -.25))):
        P.add("Accent", L.cyl(.035, .14, (x * .8, y, .12), axis="X"))
        P.add("Body", L.box((.1, .12, .14), (x * .62, y, .2)))
        if k != 3:
            P.add("Body", L.loft([(0, .07, .1, 2.8), (.1, .07, .095, 2.8)], axis="X", loc=(x * .84 - .05, y, .32), round_caps=.015))
    P.add("Body", L.shell([(.36, .22, .15, 2.6, 0, .22), (.7, .22, .15, 2.8, 0, .22), (.92, .18, .12, 2.4, 0, .24)], caps=.05))
    P.add("Accent", L.box((.3, .03, .18), (0, .065, .72)))
    P.add("Eye", L.box((.12, .02, .14), (-.07, .05, .72), r=.008))
    P.add("Eye", L.box((.1, .02, .1), (.08, .05, .7), r=.008, rot=(0, .3, 0)))
    P.add("Accent", L.box((.02, .025, .2), (0, .048, .72), rot=(0, .5, 0), r=.004))
    for sx in (-1, 1):
        P.add("Accent", L.box((.05, .07, .9), (sx * .22, -.33, .76)))
    P.add("Accent", L.box((.48, .05, .06), (0, -.33, 1.18)))
    P.add("Accent", L.box((.5, .42, .05), (0, -.16, .93)))
    P.add("Accent", L.box((.46, .4, .34), (-.02, -.16, 1.125), rot=(0, 0, math.radians(7))))
    P.add("Body", [cable([(.2, -.33, 1.2), (.26, -.38, 1.0), (.22, -.36, .82)], .015)])
    P.add("Accent", L.box((.44, .1, .1), (0, -.37, .45)))
    P.add("Eye", L.ball(.035, (-.12, -.42, .45)))
    P.add("Body", L.cyl(.035, .03, (.12, -.42, .45), axis="Y"))
    return P


def turret(L):
    """The defense pod on a cracked pedestal: a floor plate with a broken corner, a smooth station pedestal split
    open, a bare strut braced against it, cables spilling out, a dim collar, a cracked gun pod (the head turns,
    the collar and floor lamps do not)."""
    P = D.Parts()
    base = L.lathe([(0, 0), (.36, 0), (.37, .02), (.34, .05), (0, .05)])[0]
    P.add("Body", [s.boolean(base, s.rbox((.2, .2, .2), (-.34, .16, .03), r=0, rot=(0, 0, .5)))])
    ped = L.lathe([(0, .04), (.22, .04), (.2, .2), (.13, .5), (.11, .88), (.14, .96), (.13, 1.02), (0, 1.02)])[0]
    P.add("Body", [crack(ped, (.12, -.1, .52), .13, seed=7)])
    P.add("Accent", L.cyl(.05, .2, (.06, -.05, .52)))
    P.add("Accent", L.rod((.3, .14, .05), (.1, .05, .76), .022) + L.cyl(.13, .03, (0, 0, .76)))
    P.add("Accent", [cable([(0, -.08, .6), (.2, -.2, .25), (.26, -.24, .05)], .013)])
    P.add("Accent", [cable([(0, -.1, .62), (.14, -.24, .3), (.12, -.3, .06)], .011)])
    for k in range(3):
        a = .9 + k * math.tau / 3
        if k != 1:
            P.add("Eye", L.ball(.025, (math.cos(a) * .28, math.sin(a) * .28, .05)))
        else:
            P.add("Accent", L.cyl(.025, .02, (math.cos(a) * .28, math.sin(a) * .28, .055)))
    P.add("Eye", L.loft([(1.0, .15, .15, 2.0), (1.06, .15, .15, 2.0)]))
    pod = L.shell([(0, .15, .11, 2.4), (.3, .16, .12, 2.6), (.6, .1, .08, 2.4)], axis="-Y", loc=(0, .24, 1.28), caps=.05)[0]
    P.add("AccentHead", [crack(pod, (.15, .0, 1.34), .12, seed=5)])
    P.add("AccentHead", L.cyl(.09, .14, (0, 0, 1.12)))
    P.add("AccentHead", L.cyl(.04, .3, (.0, -.48, 1.27), axis="Y") + L.cyl(.05, .05, (0, -.62, 1.27), axis="Y"))
    P.add("AccentHead", [cable([(-.12, .1, 1.24), (-.16, .16, 1.12), (-.13, .18, 1.02)], .01)])
    return P


def spider(L):
    """The cable-drum crawler: a maintenance spool on a chassis plate — a fat drum of cable between two flanges, the
    loose end trailing behind — a sensor pod with one lens out in front. Damage: a flange broken, one leg a pipe."""
    P = D.Parts()
    P.add("Body", L.box((.32, .38, .06), (0, 0, .15)))
    drum = s.lathe([(0, 0), (.21, 0), (.21, .03), (.1, .035), (.1, .305), (.21, .31), (.21, .34), (0, .34)], seg=16, axis="X",
                   loc=(-.17, .06, .32))
    P.add("Body", [crack(drum, (.17, .12, .5), .1, seed=8)])
    P.add("Accent", L.cyl(.165, .28, (0, .06, .32), axis="X"))                                    # wound cable
    P.add("Accent", [cable([(.05, .2, .38), (.06, .36, .18), (.1, .5, .03)], .016)])
    for sx in (-1, 1):
        P.add("Body", L.box((.03, .1, .22), (sx * .185, .06, .23)))                                # axle brackets
    P.add("Body", L.shell([(0, .1, .08, 2.4), (.1, .08, .065, 2.4)], axis="-Y", loc=(0, -.14, .19), caps=.03))
    P.add("Eye", L.ball(.03, (-.04, -.25, .2)))
    P.add("Accent", L.cyl(.026, .02, (.04, -.25, .2), axis="Y"))
    P.add("Accent", L.rod((.05, -.18, .26), (.08, -.15, .36), .01))                               # bent antenna
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        if i == 5:
            P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .02) + L.cyl(.032, .05, tuple(knee)))
            P.add(f"AccentLeg{i}Lower", L.rod(knee, foot + V((0, 0, .01)), .017) + L.cyl(.03, .02, tuple(foot - V((0, 0, .01)))))
        else:
            P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .03, kind="stilt") + L.ball(.04, knee))
            P.add(f"AccentLeg{i}Lower", L.rod(knee, foot, .024, kind="stilt")
                  + L.lathe([(0, 0), (.028, .02), (.02, .06), (0, .07)], loc=foot - V((0, 0, .02))))
    return P


def listener(L):
    """The helmet rover: a pressure-suit helmet on a wheeled base — a suit neck ring, a dark visor, comm ear pods on
    both sides listening. Damage: the visor cracked, the right ear pod torn loose and hanging by its cable."""
    P = D.Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    P.add("Body", L.shell([(.28, .3, .48, 3.0), (.42, .32, .5, 3.2), (.52, .28, .44, 3.0)], caps=.04))
    for x, y in ((-.43, -.3), (.43, -.3), (-.43, .3), (.43, .3)):
        P.add("Accent", L.cyl(.05, .22, (x * .74, y, .22), axis="X"))
        P.add("Body", L.box((.12, .14, .16), (x * .64, y, .3)))
    P.add("Accent", L.lathe([(0, 0), (.27, 0), (.29, .02), (.29, .07), (.26, .09), (0, .09)], loc=(0, 0, .5)))   # neck ring
    P.add("Body", L.lathe([(0, 0), (.28, .0), (.32, .14), (.32, .3), (.27, .44), (.15, .52), (0, .54)], loc=(0, 0, .56)))
    visor = s.ball(.27, (0, -.11, .82), seg=16, scale=(1, .9, .62))
    P.add("Accent", [crack(visor, (.12, -.3, .9), .1, seed=9)])
    P.add("Eye", L.box((.2, .03, .03), (0, -.29, .555), r=.01))
    P.add("Accent", L.cyl(.04, .08, (0, .0, 1.12)) + L.box((.1, .06, .05), (0, .02, 1.18)))   # dead helmet lamp
    P.add("Body", L.cyl(.1, .14, (-.36, .02, .84), axis="X"))
    P.add("Eye", L.cyl(.06, .02, (-.435, .02, .84), axis="X"))
    pod = L.cyl(.1, .14, (.38, .02, .8), axis="X") + L.cyl(.06, .02, (.455, .02, .8), axis="X")
    tilt(pod, (.31, .02, .84), (0, math.radians(35), math.radians(-10)))
    P.add("Body", pod[:1]).add("Eye", pod[1:])
    P.add("Accent", [cable([(.31, .02, .86), (.36, .0, .8), (.38, .02, .76)], .015)])
    P.add("Accent", [cable([(-.1, .25, .58), (-.2, .35, .45), (-.16, .4, .32)], .012)])
    return P


def sweeper(L):
    """The scrubber cart: a low shell cart with a big upright turbine intake in front and a dust canister standing
    at the back, a hose between them. Damage: a fan blade missing, the canister cracked, the hose split."""
    P = D.Parts()
    D.wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18)):
        P.add("Accent", L.cyl(.03, .12, (x * .8, y, .08), axis="X"))
        P.add("Body", L.box((.08, .1, .24), (x * .66, y, .2)))
    P.add("Body", L.box((.14, .1, .14), (0, -.3, .24)))
    P.add("Body", L.box((.03, .06, .14), (-.05, -.3, .12)) + L.box((.03, .06, .14), (.05, -.3, .12)))
    P.add("Body", L.shell([(.3, .36, .38, 3.0), (.42, .37, .39, 3.2), (.5, .32, .34, 3.0)], caps=.04))
    P.add("Body", [arc((0, -.27, .62), .22, 0, 360, .05, plane="XZ", n=16)])                      # intake ring
    P.add("Accent", L.cyl(.06, .1, (0, -.27, .62), axis="Y"))
    for k in range(6):
        if k == 2:
            continue
        a = k / 6 * math.tau
        P.add("Accent", [s.rbox((.15, .015, .05), (math.cos(a) * .1, -.27, .62 + math.sin(a) * .1), r=.005, seg=1,
                                rot=(math.radians(25), -a, 0))])
    P.add("Eye", L.lens(.035, (0, -.32, .62)))
    P.add("Body", L.box((.2, .16, .14), (0, -.2, .46)))
    can = L.lathe([(0, 0), (.15, 0), (.16, .04), (.16, .44), (.13, .5), (0, .5)], loc=(0, .17, .48))[0]
    P.add("Body", [crack(can, (-.13, .25, .8), .09, seed=10)])
    for z in (.62, .86):
        P.add("Accent", L.cyl(.165, .025, (0, .17, z)))
    P.add("Accent", [cable([(.1, .1, .9), (.24, -.05, .95), (.18, -.22, .76)], .03)])
    P.add("Accent", L.cyl(.05, .14, (0, 0, .3)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    return P


def thumper(L):
    """The hull-plating stamper: a tracked deck with a C-frame press — a column at the back, a beam over the front,
    a crosshead driving two rams onto the pads — and a stack of hull plates on the deck. Damage: the column cracked,
    a hydraulic line torn and hanging."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.24, 1.36, .54), (sx * .56, 0, .28)))
    P.add("Body", L.shell([(.48, .46, .58, 3.2), (.8, .47, .58, 3.4)], caps=.05))
    col = L.box((.5, .32, 1.2), (0, .32, 1.36))[0]
    P.add("Body", [crack(col, (.25, .2, 1.3), .14, seed=11)])
    P.add("Body", L.box((.46, 1.3, .2), (0, -.3, 1.86)))
    P.add("Body", L.box((.82, .24, .26), (0, -.85, 1.8)))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Accent", L.rod((x, -.85, 1.7), (x, -.85, .08), .055, kind="piston"))
        P.add("Accent", L.box((.3, .3, .06), (x, -.85, .07)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
    for k in range(4):
        P.add("Accent", L.box((.66, .5, .03), (.02 * k, -.15, .813 + k * .028), rot=(0, 0, .04 * k)))  # hull plates
    P.add("Eye", L.box((.2, .03, .05), (0, .155, 1.6), r=.01))
    P.add("Accent", [cable([(.2, -.6, 1.76), (.32, -.65, 1.4), (.28, -.6, 1.15)], .02)])
    P.add("Accent", [cable([(-.2, .16, 1.8), (-.26, -.4, 1.75), (-.3, -.75, 1.72)], .02)])
    return P


def sorter(L):
    """The airlock scanner arch: a base plate, a shell post on one side and a bare replacement strut on the other,
    a lintel with the big scanning lens hung under it. Damage: the beacon dead, cables hanging from the lintel."""
    P = D.Parts()
    P.add("Accent", L.box((.9, .5, .04), (0, 0, .02)))
    P.add("Body", L.shell([(.03, .09, .1, 2.6, -.38, 0), (1.42, .07, .08, 2.6, -.38, 0)], caps=.02))
    for dy in (-.05, .05):
        P.add("Accent", L.rod((.38, dy, .03), (.38, dy, 1.42), .02))
    for z in (.4, .8, 1.2):
        P.add("Accent", L.rod((.38, -.06, z), (.38, .06, z + .12), .012))
    P.add("Body", L.box((.94, .26, .18), (0, 0, 1.5)))
    P.add("Body", L.box((.12, .12, .1), (0, -.04, 1.38)))
    back = L.lathe([(0, 0), (.22, 0), (.25, .05), (.25, .12), (0, .12)], axis="-Y", loc=(0, .02, 1.22))
    P.add("Body", back)
    P.add("Accent", L.lathe([(.26, 0), (.27, .02), (.26, .05), (.2, .05), (0, .05)], axis="-Y", loc=(0, -.1, 1.22)))
    P.add("Eye", L.lathe([(.2, 0), (.2, .01), (.1, .03), (0, .035)], axis="-Y", loc=(0, -.145, 1.22)))
    for x in (-.38, .38):
        P.add("Eye" if x < 0 else "Accent", L.box((.03, .03, .6), (x, -.1 if x < 0 else -.075, .9), r=.008))
    P.add("Accent", L.cyl(.05, .08, (.3, 0, 1.63)))
    P.add("Accent", [cable([(.2, .05, 1.42), (.24, .08, 1.15), (.21, .06, .98)], .012)])
    P.add("Accent", [cable([(-.15, .08, 1.42), (-.18, .1, 1.25), (-.14, .09, 1.12)], .01)])
    return P


def drone(L):
    """The EVA tether drone: a boxy manoeuvring pack with grab handles, a camera pod on a short neck, four ducted
    fans and a long tether paid out from a reel at the back. Damage: a duct cracked, a handle bent, one fan arm a
    bare pipe."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    pack = L.box((.36, .32, .2), (0, .02, 0))[0]
    P.add("Body", [crack(pack, (.17, -.12, .08), .08, seed=12)])
    for sx in (-1, 1):
        h = [cable([(sx * .12, -.1, .09), (sx * .12, 0, .18), (sx * .12, .1, .09)], .014)]
        if sx > 0:
            tilt(h, (.12, 0, .09), (0, math.radians(30), 0))
        P.add("Accent", h)
    P.add("Accent", L.box((.06, .1, .06), (0, -.17, -.05)))
    P.add("Accent", L.ball(.06, (0, -.23, -.09)))
    P.add("Eye", L.lens(.035, (0, -.285, -.1)))
    P.add("Accent", L.cyl(.06, .14, (0, .2, -.02), axis="X"))
    P.add("Accent", [cable([(0, .22, -.05), (.02, .4, -.16), (.06, .62, -.34)], .01)])
    duct = [(.2, 0), (.21, .01), (.21, .05), (.19, .06), (.17, .06), (.17, .01), (0, .01), (0, 0)]
    for i, (x, y) in enumerate(rot):
        if i == 3:
            P.add("Accent", L.rod((x * .3, y * .3, 0), (x, y, .02), .018))
            P.add("Body", L.cyl(.04, .08, (x, y, .02)))
        else:
            P.add("Body", L.rod((x * .3, y * .3, 0), (x, y, .02), .03, kind="square"))
            d = L.lathe(duct, loc=(x, y, 0))[0]
            if i == 1:
                d = s.boolean(d, s.rbox((.2, .2, .2), (x - .19, y + .1, .03), r=0, rot=(0, 0, .6)))
            P.add("Body", [d] + L.cyl(.04, .05, (x, y, .03)))
        P.add(f"AccentRotor{i}", L.rotor((x, y, .065), .32))
    return P


def vacuum(L):
    """The air recycler: a ceiling puck whose room face is an iris of petals over a glowing filter, three feed lines
    into its back. Damage: a petal gone, the rim cracked, a feed line hanging."""
    P = D.Parts()
    puck = L.lathe([(0, 0), (.34, 0), (.36, .02), (.36, .07), (.33, .1), (0, .1)], seg=40)[0]
    P.add("Body", [s.boolean(puck, s.rbox((.16, .2, .2), (.34, .1, .05), r=0, rot=(0, 0, .5)))])
    P.add("Accent", L.lathe([(.29, 0), (.33, 0), (.33, .03), (.29, .03)], loc=(0, 0, -.025)))
    P.add("Eye", L.cyl(.27, .01, (0, 0, -.004)))
    for k in range(8):
        if k == 5:
            continue
        a = k / 8 * math.tau
        P.add("Accent", [s.rbox((.24, .09, .012), (math.cos(a) * .15, math.sin(a) * .15, -.016), r=.004, seg=1,
                                rot=(0, 0, a + math.radians(20)))])
    P.add("Accent", L.cyl(.05, .03, (0, 0, -.02)))
    for k in range(3):
        a = k / 3 * math.tau + .4
        P.add("Accent", L.cyl(.03, .05, (math.cos(a) * .22, math.sin(a) * .22, .12)))
    P.add("Accent", [cable([(-.24, -.18, .0), (-.28, -.22, -.1), (-.25, -.2, -.16)], .012)])
    return P


def plate(L):
    """A pressure plate whose lit border has a dead stretch and a corner stud gone."""
    P = D.Parts()
    S, h = 1.1, .012
    P.add("Body", L.box((S - .07, S - .07, h), (0, 0, h / 2), r=.004))
    k = S / 2 - .06
    P.add("Accent", sum([L.box((.07, .07, h + .006), (x * k, y * k, (h + .006) / 2), r=.006) for x, y in ((-1, -1), (1, -1), (-1, 1))], []))
    b, e = .035, h + .004
    P.add("Eye", L.box((S, b, e), (0, S / 2 - b / 2, e / 2), r=.003) + L.box((S, b, e), (0, -S / 2 + b / 2, e / 2), r=.003)
          + L.box((b, S - 2 * b, e), (-S / 2 + b / 2, 0, e / 2), r=.003)
          + L.box((b, .5, e), (S / 2 - b / 2, -.27, e / 2), r=.003))
    P.add("Accent", L.box((b, .5, e - .002), (S / 2 - b / 2, .25, (e - .002) / 2), r=.003))
    return P


def attach_mic(L):
    """The shared microphone module at a lower segment count."""
    P = D.Parts()
    P.add("Accent", [s.rcyl(.045, .02, (0, 0, .01), seg=12, r=.004, bevel_seg=1)] + L.rod((0, 0, .01), (0, 0, .27), .014))
    P.add("Eye", [s.ball(.06, (0, 0, .32), seg=12)])
    P.add("Body", [s.rcyl(.066, .012, (0, 0, .32 + d), seg=12, r=.003, bevel_seg=1) for d in (-.03, 0, .03)])
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_plate": plate, "attach_mic": attach_mic,
}
