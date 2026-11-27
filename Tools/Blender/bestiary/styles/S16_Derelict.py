"""S16 Derelict — «Тихий комплекс» (Quiet facility, realistic family): the facility's own security synths, still on duty.

Smooth station-made armour shells over machinery, kept in working order by nobody: whole and symmetric, panel seams
and service hatches, ring joints and pistons at the waists, both lenses lit. The machines are the only things in the
building that still work — clean, quiet and precise. (The decay is the abandoned station's house look, `DerelictLook`,
not the robots'.)
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


def bent(points, r, bend=0.06, n=5):
    """A bent steel tube: straight runs through `points` with every corner rounded by a radius about `bend`."""
    P = [V(p) for p in points]
    out = [P[0]]
    for i in range(1, len(P) - 1):
        a, p, b = P[i - 1], P[i], P[i + 1]
        d1, d2 = a - p, b - p
        t = min(bend, d1.length * 0.45, d2.length * 0.45)
        q0, q1 = p + d1.normalized() * t, p + d2.normalized() * t
        for k in range(n + 1):
            u = k / n
            out.append(q0 * (1 - u) ** 2 + p * 2 * u * (1 - u) + q1 * u * u)
    out.append(P[-1])
    return s.tube([tuple(o) for o in out], r, res=2, smooth=False)


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

    # ------------------------------------------------------------------ furniture: a 1990s research-complex fit-out
    # Powder-coated steel and enamel, bent round-tube frames, thick laminate tops with soft edges, deep CRT monitors,
    # bar pulls, recessed plinths. Whole and clean — the place is only empty.
    leg_kind = "sled"
    edge_kind = "soft"
    cushion_puff = 0.3
    handle_kind = "bar"
    foliage_kind = "blob"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """Thin boards are thick laminate with a bull-nose edge; big boards are pressed steel with a 1 cm round."""
        m = min(size)
        r = min(m * 0.45, 0.025) if m <= 0.07 else min(m * 0.3, 0.012)
        return [s.rbox(size, loc, r=r, seg=3 if m <= 0.07 else 2, rot=rot)]

    def carcass(self, size, loc, kick=0.06):
        """A powder-coated steel cabinet: softly rounded uprights, a reveal under the top, a deep recessed plinth."""
        w, d, h = size
        x, y, z = loc
        body = s.rbox((w, d, h - kick), (x, y, z + kick + (h - kick) / 2), r=min(min(w, d) * 0.3, 0.018), seg=3)
        if h - kick > 0.3:
            s.groove(body, "Z", z + h - 0.06, 0.012, 0.005)
        out = [body]
        if kick > 0:
            out.append(s.rbox((w - 0.06, d - 0.09, kick + 0.012), (x, y + 0.025, z + (kick + 0.012) / 2), r=0.004, seg=1))
        return out

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Two bent round-tube sled frames (runner on the floor, rounded corners) and a stretcher at the back."""
        xs, ys = half_w - inset, half_d - inset
        r = max(0.012, min(thick * 0.42, 0.022))
        top = top_z + 0.008
        out = []
        for sx in (-1, 1):
            x = sx * xs
            out.append(bent([(x, -ys, top), (x, -ys, r), (x, ys, r), (x, ys, top)], r, bend=min(0.08, length * 0.2)))
        out.append(s.tube([(-xs, ys, top_z - 0.06), (xs, ys, top_z - 0.06)], r * 0.8, res=2, smooth=False))
        return out

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A round steel tube on a nylon glide."""
        x, y, z = top
        foot = (x + splay[0], y + splay[1], max(0.0, z - length))
        r = thick / 2
        return [s.rcyl(r, length - 0.02, ((x + foot[0]) / 2, (y + foot[1]) / 2, foot[2] + 0.015 + (length - 0.02) / 2),
                       seg=12, r=r * 0.3, bevel_seg=1),
                s.rcyl(r * 1.15, 0.025, (foot[0], foot[1], foot[2] + 0.0125), seg=12, r=0.004, bevel_seg=1)]

    def handle(self, loc, length=0.12, vertical=True):
        """A round-bar D pull on two stand-offs."""
        x, y, z = loc
        h = length / 2
        if vertical:
            pts = [(x, y + 0.012, z - h), (x, y - 0.03, z - h), (x, y - 0.03, z + h), (x, y + 0.012, z + h)]
        else:
            pts = [(x - h, y + 0.012, z), (x - h, y - 0.03, z), (x + h, y - 0.03, z), (x + h, y + 0.012, z)]
        return [bent(pts, 0.009, bend=0.015)]

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """Boxy waiting-room upholstery: square foam blocks with a firm 3-4 cm round."""
        m = min(size)
        return [s.rbox(size, loc, r=min(m * 0.3, 0.04), seg=3, rot=rot)]

    def screen(self, size, loc, face="-Y"):
        """(accent, glow): a deep CRT-era monitor — a thick rounded bezel, a tapering hood behind, the convex tube."""
        w, h = size
        x, y, z = loc
        sign = -1 if face == "-Y" else 1
        total = min(0.9 * w, 0.38)
        case = total * 0.42                                       # the full-size front case, then the tube's hood
        depth = total - case + 0.02
        bez = s.rbox((w + 0.05, case, h + 0.05), (x, y - sign * (case / 2 - 0.03), z), r=0.025, seg=3)
        hood = s.rbox((w * 0.95, h * 0.92, depth), (x, y - sign * (case - 0.03 + depth / 2 - 0.02), z - 0.01), r=0.03,
                      seg=2, taper=(0.68, 0.62), rot=(math.radians(90 * sign), 0, 0))
        glow = s.rbox((w - 0.02, 0.02, h - 0.02), (x, y + sign * 0.027, z), r=0.012, seg=2)
        return [bez, hood], [glow]

    def shade(self, r_bottom, r_top, height, loc):
        """An enamelled steel dome shade with a rolled rim."""
        r = max(r_bottom, r_top)
        prof = [(r, 0), (r + 0.006, 0.006), (r * 0.92, height * 0.35), (r * 0.62, height * 0.8), (r * 0.3, height),
                (r * 0.28, height * 0.9), (r * 0.58, height * 0.72), (r * 0.86, height * 0.3), (r - 0.008, 0.008), (0, 0.008)]
        return [s.lathe(prof, seg=24, loc=loc)]

    def pot(self, r, h, loc):
        """A fibreglass drum planter: straight sides, a rolled lip, a recessed foot."""
        return [s.lathe([(0, 0), (r * 0.82, 0), (r * 0.84, 0.03), (r * 0.96, 0.04), (r, h * 0.88), (r * 1.05, h * 0.9),
                         (r * 1.05, h), (r * 0.9, h), (0, h - 0.04)], seg=24, loc=loc)]

    def signature(self, model, P):
        return P


# =========================================================================== walkers

def guard(L):
    """The station synth, whole: smooth armour shells, a chest with a service hatch and a status strip, a waist of
    rings and pistons, an upright egg head with two lit lenses behind a visor band, matched armoured arms with
    pauldrons and gauntlets, armoured shins and boots."""
    P = D.Parts()
    P.add("Body", L.shell([(.84, .18, .13, 2.6), (.94, .2, .145, 2.8), (1.02, .17, .13, 2.6)], caps=.03))      # pelvis
    P.add("Accent", L.cyl(.075, .34, (0, .02, 1.1)))
    for z in (1.02, 1.09, 1.16):
        P.add("Accent", L.cyl(.13, .025, (0, .02, z)))
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .12, 0, .98), (sx * .14, -.02, 1.24), .025, kind="piston"))
    P.add("Body", L.shell([(1.2, .23, .15, 2.6, 0, .01), (1.38, .31, .19, 2.8, 0, -.01), (1.55, .32, .19, 2.8), (1.67, .24, .15, 2.4)]))
    P.add("Accent", L.box((.2, .04, .14), (0, -.18, 1.44), r=.012))                                          # service hatch
    P.add("Eye", L.box((.12, .03, .015), (0, -.19, 1.54), r=.005))                                             # status strip
    P.add("Body", L.box((.34, .16, .36), (0, .2, 1.45)))                                                       # back pack
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.03, .36, (sx * .1, .29, 1.45)))
    # head, upright
    P.add("Body", L.shell([(1.73, .11, .13, 2.2), (1.84, .145, .17, 2.4, 0, -.02), (1.96, .135, .16, 2.3, 0, -.01), (2.03, .09, .1, 2.0)], caps=.03))
    P.add("Accent", L.box((.24, .06, .1), (0, -.165, 1.85)))                                                  # visor band
    P.add("Eye", L.ball(.03, (-.055, -.198, 1.855)) + L.ball(.03, (.055, -.198, 1.855)))
    P.add("Accent", L.cyl(.055, .14, (0, 0, 1.72)))
    for sx in (-1, 1):
        x = sx * .4
        P.add("Body", L.ball(.11, (sx * .38, 0, 1.58), scale=(1, 1.1, .9)))
        P.add("Body", L.ball(.16, (x, 0, 1.67), scale=(1.05, 1.15, .62)))                                     # pauldron
        P.add("Accent", L.cyl(.17, .03, (sx * .42, 0, 1.62), rot=(0, math.radians(20 * sx), 0)))
        P.add("Body", L.rod((x, 0, 1.52), (sx * .43, -.02, 1.22), .06))
        P.add("Accent", L.ball(.055, (sx * .43, -.02, 1.21)))
        P.add("Body", L.shell([(.86, .06, .065, 2.4, sx * .43, -.07), (1.03, .075, .08, 2.6, sx * .43, -.05), (1.2, .06, .065, 2.4, sx * .43, -.02)], caps=.02))
        P.add("Accent", L.box((.1, .12, .13), (sx * .43, -.08, .8)))                                          # gauntlet
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.085, (x, 0, .9)))
        P.add(leg, L.shell([(.47, .075, .085, 2.6, x, 0), (.68, .095, .105, 2.8, x, -.005), (.92, .09, .1, 2.6, x, 0)], caps=.02))
        P.add(leg, L.cyl(.062, .16, (x, 0, D.GUARD_KNEE), axis="X"))
        P.add(leg, L.shell([(.1, .06, .07, 2.4, x, 0), (.28, .075, .095, 2.6, x, .02), (.45, .065, .075, 2.4, x, 0)], caps=.02))
        P.add(leg, L.shell([(.0, .07, .16, 2.6, x, -.05), (.08, .065, .15, 2.6, x, -.05), (.12, .05, .08, 2.4, x, -.02)], caps=.02))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """The sentinel on a repulsor ring: no legs — an armoured torso riding a lit ring over a segmented tail that
    steadies it, a long hooded head held forward with two lenses, both arms long and clawed."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.36, .04), (.36, .1), (.3, .14), (0, .14)], loc=(0, 0, .94)))     # repulsor ring
    P.add("Eye", L.lathe([(0, 0), (.27, 0), (.27, .025), (0, .025)], loc=(0, 0, .925)))
    pts = [V((0, .03 + .1 * t * t, .96 - .66 * t)) for t in [k / 9 for k in range(10)]]
    P.add("Accent", [s.tube([tuple(p) for p in pts], .022, res=1)])
    for k, p in enumerate(pts[1:]):
        P.add("Body", L.cyl(.05 - k * .002, .035, tuple(p), seg=8))                                          # tail segments
    P.add("Body", L.shell([(1.04, .2, .15, 2.6, 0, .02), (1.3, .27, .18, 2.8, 0, 0), (1.6, .36, .22, 2.8, 0, -.03),
                           (1.82, .36, .21, 2.6, 0, -.03), (1.93, .24, .16, 2.4, 0, -.02)], caps=.05))
    P.add("Accent", L.box((.26, .04, .16), (0, -.235, 1.5), r=.012))
    P.add("Accent", L.rod((0, -.02, 1.86), (0, -.14, 1.96), .05))
    P.add("Body", L.shell([(0, .12, .11, 2.4), (.15, .13, .12, 2.6), (.32, .07, .07, 2.2)], axis="-Y", loc=(0, -.02, 1.96), caps=.03))
    P.add("Eye", L.ball(.03, (-.045, -.33, 1.95)) + L.ball(.03, (.045, -.33, 1.95)))
    for sx in (-1, 1):
        P.add("Body", L.ball(.11, (sx * .42, 0, 1.78), scale=(1, 1.1, .9)))
        P.add("Body", L.rod((sx * .43, 0, 1.74), (sx * .5, -.1, 1.38), .06))
        P.add("Accent", L.ball(.055, (sx * .5, -.1, 1.37)))
        P.add("Body", L.rod((sx * .5, -.1, 1.37), (sx * .46, -.42, 1.12), .065))
        P.add("Accent", L.box((.12, .14, .12), (sx * .46, -.44, 1.1)))
        for dx in (-.05, 0, .05):
            P.add("Accent", [lathe_between([(0, 0), (.022, 0), (.016, .6), (0, 1)], (sx * .46 + dx, -.48, 1.08), (sx * .46 + dx * 1.6, -.74, .84), seg=8)])
    return P


def hound(L):
    """The maintenance quadruped, whole: an armoured chest shell and a back shell over the core with a lit spine
    strip, a hip shell, a smooth wedge head with two lenses and a short antenna, four armoured legs."""
    P = D.Parts()
    P.add("Body", L.shell([(0, .19, .2, 2.6, 0, .02), (.25, .19, .21, 2.6, 0, .03), (.44, .15, .16, 2.4, 0, .01)],
                          axis="-Y", loc=(0, .02, .84), caps=.05))
    P.add("Body", L.shell([(0, .17, .18, 2.6), (.3, .15, .16, 2.4)], axis="Y", loc=(0, 0, .84), caps=.04))   # back shell
    P.add("Accent", L.rod((0, -.05, 1.02), (0, .44, .98), .026))                                               # spine rail
    P.add("Eye", L.box((.025, .2, .02), (0, .18, 1.02), r=.005))
    P.add("Body", L.shell([(0, .15, .16, 2.4), (.2, .13, .13, 2.4)], axis="Y", loc=(0, .3, .83), caps=.04))   # hip shell
    P.add("Accent", L.rod((0, -.36, .92), (0, -.56, 1.0), .06))
    P.add("Body", L.shell([(0, .11, .12, 2.6), (.2, .11, .1, 2.6), (.44, .05, .05, 2.2)], axis="-Y", loc=(0, -.52, 1.0), caps=.03))
    P.add("Eye", L.ball(.026, (-.09, -.71, 1.07)) + L.ball(.026, (.09, -.71, 1.07)) + L.box((.05, .02, .02), (0, -.96, 1.0), r=.006))
    P.add("Accent", L.rod((.04, -.62, 1.08), (.04, -.6, 1.24), .012))                                         # antenna
    P.add("Accent", L.rod((0, .46, .95), (0, .58, 1.06), .02))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add("Body", L.ball(.09, (x * .9, y, .7)))
        P.add(leg, L.ball(.07, (x, y, .63)))
        P.add(leg, L.shell([(D.HOUND_KNEE, .045, .05, 2.4, x, y), (.48, .065, .08, 2.6, x, y), (.66, .07, .08, 2.4, x, y)], caps=.02))
        P.add(leg, L.ball(.045, (x, y, D.HOUND_KNEE)))
        P.add(leg, L.rod((x, y, D.HOUND_KNEE), (x, y, .05), .035))
        P.add(leg, L.shell([(0, .04, .07, 2.4, x, y - .03), (.05, .035, .06, 2.4, x, y - .03)], caps=.015))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def worker(L):
    """The cargo runner: a shell deck, a shell snout with two headlamps and a visor, a shell mast with service
    panels and a full cable chain, a beacon on top."""
    def deck(L, P):
        D.worker_deck(L, P)

    def pod(L, P):
        P.add("Body", L.shell([(.31, .23, .16, 3.0, 0, -.23), (.5, .24, .17, 3.2, 0, -.23), (.63, .2, .14, 3.0, 0, -.22)], caps=.03))
        P.add("Accent", L.box((.42, .03, .2), (0, -.405, .47)))
        P.add("Eye", L.ball(.03, (-.16, -.41, .4)) + L.ball(.03, (.16, -.41, .4)))
        D.worker_shoulders(L, P, inner=.2)

    def mast(L, P):
        P.add("Body", L.shell([(.3, .23, .15, 2.8, 0, .22), (.9, .22, .15, 2.8, 0, .22), (1.29, .19, .13, 2.6, 0, .22)], caps=.04))
        for sx in (-1, 1):
            P.add("Accent", L.box((.03, .16, .4), (sx * .215, .22, .78), r=.006))                           # service panels
        P.add("Accent", L.box((.5, .34, .05), (0, .22, 1.3)))                                              # cap
        for k in range(7):                                                                                 # cable chain
            P.add("Accent", [s.rbox((.1, .035, .055), (0, .075, .38 + k * .068), r=.005, seg=1)])

    def beacon(L, P):
        P.add("Accent", L.cyl(.07, .035, (0, .25, 1.335)))
        P.add("Eye", L.cyl(.05, .07, (0, .25, 1.38), radius2=.04))

    def bezel(w, h, d):
        """The display's thick rounded CRT bezel: four bars standing proud of the screen plate, a hood behind it."""
        t, k = .03, d + .014
        out = [s.rbox((w + 2 * t, k, t), (0, -k / 2 + .004, sz * (h + t) / 2), r=.01, seg=1) for sz in (-1, 1)]
        out += [s.rbox((t, k, h), (sx * (w + t) / 2, -k / 2 + .004, 0), r=.01, seg=1) for sx in (-1, 1)]
        out.append(s.rbox((w * .9, .05, h * .86), (0, .022, 0), r=.015, seg=1, taper=(.8, .8)))
        return out

    def display(L, P):
        """The warehouse display on the mast's front (the design note: the worker reads as the loot hauler): a small
        CRT-style screen tipped back towards the camera, the box-on-a-pallet pictogram glowing on it."""
        D.worker_plaque(L, P, loc=(0, .07, 1.02), w=.22, h=.19, tilt=22, frame=bezel)

    return D.worker(L, deck=deck, pod=pod, mast=mast, beacon=beacon, extra=display)


def turret(L):
    """The defense pod on its pedestal: a round floor plate with three lit studs, a smooth station pedestal with a
    service hatch, a lit collar, a gun pod (the head turns, the collar and floor lamps do not)."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.36, 0), (.37, .02), (.34, .05), (0, .05)]))
    P.add("Body", L.lathe([(0, .04), (.22, .04), (.2, .2), (.13, .5), (.11, .88), (.14, .96), (.13, 1.02), (0, 1.02)]))
    P.add("Accent", L.box((.1, .04, .2), (0, -.14, .42), r=.01))
    P.add("Accent", L.cyl(.13, .03, (0, 0, .76)))
    for k in range(3):
        a = .9 + k * math.tau / 3
        P.add("Eye", L.ball(.025, (math.cos(a) * .28, math.sin(a) * .28, .05)))
    P.add("Eye", L.loft([(1.0, .15, .15, 2.0), (1.06, .15, .15, 2.0)]))
    P.add("AccentHead", L.shell([(0, .15, .11, 2.4), (.3, .16, .12, 2.6), (.6, .1, .08, 2.4)], axis="-Y", loc=(0, .24, 1.28), caps=.05))
    P.add("AccentHead", L.cyl(.09, .14, (0, 0, 1.12)))
    P.add("AccentHead", L.cyl(.04, .3, (.0, -.48, 1.27), axis="Y") + L.cyl(.05, .05, (0, -.62, 1.27), axis="Y"))
    return P


def spider(L):
    """The cable-drum crawler: a maintenance spool on a chassis plate — a fat drum of cable between two flanges, the
    end paid out behind — a sensor pod with two lenses out in front, eight matched legs."""
    P = D.Parts()
    P.add("Body", L.box((.32, .38, .06), (0, 0, .15)))
    P.add("Body", [s.lathe([(0, 0), (.21, 0), (.21, .03), (.1, .035), (.1, .305), (.21, .31), (.21, .34), (0, .34)], seg=16, axis="X",
                           loc=(-.17, .06, .32))])
    P.add("Accent", L.cyl(.165, .28, (0, .06, .32), axis="X"))                                    # wound cable
    P.add("Accent", [cable([(.05, .2, .38), (.06, .36, .18), (.1, .5, .03)], .016)])
    for sx in (-1, 1):
        P.add("Body", L.box((.03, .1, .22), (sx * .185, .06, .23)))                                # axle brackets
    P.add("Body", L.shell([(0, .1, .08, 2.4), (.1, .08, .065, 2.4)], axis="-Y", loc=(0, -.14, .19), caps=.03))
    P.add("Eye", L.ball(.03, (-.04, -.25, .2)) + L.ball(.03, (.04, -.25, .2)))
    P.add("Accent", L.rod((.05, -.18, .24), (.05, -.18, .38), .01))                               # antenna
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .03, kind="stilt") + L.ball(.04, knee))
        P.add(f"AccentLeg{i}Lower", L.rod(knee, foot, .024, kind="stilt")
              + L.lathe([(0, 0), (.028, .02), (.02, .06), (0, .07)], loc=foot - V((0, 0, .02))))
    return P


def listener(L):
    """The helmet rover: a pressure-suit helmet on a wheeled base — a suit neck ring, a dark visor, comm ear pods on
    both sides listening, a helmet lamp."""
    P = D.Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    P.add("Body", L.shell([(.28, .3, .48, 3.0), (.42, .32, .5, 3.2), (.52, .28, .44, 3.0)], caps=.04))
    for x, y in ((-.43, -.3), (.43, -.3), (-.43, .3), (.43, .3)):
        P.add("Accent", L.cyl(.05, .22, (x * .74, y, .22), axis="X"))
        P.add("Body", L.box((.12, .14, .16), (x * .64, y, .3)))
    P.add("Accent", L.lathe([(0, 0), (.27, 0), (.29, .02), (.29, .07), (.26, .09), (0, .09)], loc=(0, 0, .5)))   # neck ring
    P.add("Body", L.lathe([(0, 0), (.28, .0), (.32, .14), (.32, .3), (.27, .44), (.15, .52), (0, .54)], loc=(0, 0, .56)))
    P.add("Accent", [s.ball(.27, (0, -.11, .82), seg=16, scale=(1, .9, .62))])                                 # visor
    P.add("Eye", L.box((.2, .03, .03), (0, -.29, .555), r=.01))
    P.add("Accent", L.cyl(.04, .08, (0, .0, 1.12)))
    P.add("Eye", L.box((.1, .06, .05), (0, .02, 1.17)))                                                      # helmet lamp
    for sx in (-1, 1):
        P.add("Body", L.cyl(.1, .14, (sx * .36, .02, .84), axis="X"))
        P.add("Eye", L.cyl(.06, .02, (sx * .435, .02, .84), axis="X"))
    return P


def sweeper(L):
    """The scrubber cart: a low shell cart with a big upright turbine intake in front and a dust canister standing
    at the back, a hose between them."""
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
        a = k / 6 * math.tau
        P.add("Accent", [s.rbox((.15, .015, .05), (math.cos(a) * .1, -.27, .62 + math.sin(a) * .1), r=.005, seg=1,
                                rot=(math.radians(25), -a, 0))])
    P.add("Eye", L.lens(.035, (0, -.32, .62)))
    P.add("Body", L.box((.2, .16, .14), (0, -.2, .46)))
    P.add("Body", L.lathe([(0, 0), (.15, 0), (.16, .04), (.16, .44), (.13, .5), (0, .5)], loc=(0, .17, .48)))
    for z in (.62, .86):
        P.add("Accent", L.cyl(.165, .025, (0, .17, z)))
    P.add("Accent", [cable([(.1, .1, .9), (.24, -.05, .95), (.18, -.22, .76)], .03)])
    P.add("Accent", L.cyl(.05, .14, (0, 0, .3)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    return P


def thumper(L):
    """The hull-plating stamper: a tracked deck with a C-frame press — a column at the back, a beam over the front,
    a crosshead driving two rams onto the pads — and a stack of hull plates on the deck; hydraulic lines run along
    the beam."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.24, 1.36, .54), (sx * .56, 0, .28)))
    P.add("Body", L.shell([(.48, .46, .58, 3.2), (.8, .47, .58, 3.4)], caps=.05))
    P.add("Body", L.box((.5, .32, 1.2), (0, .32, 1.36)))
    P.add("Accent", L.box((.3, .04, .5), (0, .165, 1.25), r=.01))                                      # service panel
    P.add("Body", L.box((.46, 1.3, .2), (0, -.3, 1.86)))
    P.add("Body", L.box((.82, .24, .26), (0, -.85, 1.8)))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Accent", L.rod((x, -.85, 1.7), (x, -.85, .08), .055, kind="piston"))
        P.add("Accent", L.box((.3, .3, .06), (x, -.85, .07)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
        P.add("Accent", L.rod((sx * .2, .16, 1.97), (sx * .2, -.72, 1.97), .02))                       # hydraulic lines
    for k in range(4):
        P.add("Accent", L.box((.66, .5, .03), (.02 * k, -.15, .813 + k * .028), rot=(0, 0, .04 * k)))  # hull plates
    P.add("Eye", L.box((.2, .03, .05), (0, .15, 1.6), r=.01))
    return P


def sorter(L):
    """The airlock scanner arch: a base plate, two shell posts with lit scan strips, a lintel with the big scanning
    lens hung under it and a beacon on top."""
    P = D.Parts()
    P.add("Accent", L.box((.9, .5, .04), (0, 0, .02)))
    for x in (-.38, .38):
        P.add("Body", L.shell([(.03, .09, .1, 2.6, x, 0), (1.42, .07, .08, 2.6, x, 0)], caps=.02))
        P.add("Eye", L.box((.03, .03, .6), (x, -.09, .9), r=.008))
    P.add("Body", L.box((.94, .26, .18), (0, 0, 1.5)))
    P.add("Body", L.box((.12, .12, .1), (0, -.04, 1.38)))
    P.add("Body", L.lathe([(0, 0), (.22, 0), (.25, .05), (.25, .12), (0, .12)], axis="-Y", loc=(0, .02, 1.22)))
    P.add("Accent", L.lathe([(.26, 0), (.27, .02), (.26, .05), (.2, .05), (0, .05)], axis="-Y", loc=(0, -.1, 1.22)))
    P.add("Eye", L.lathe([(.2, 0), (.2, .01), (.1, .03), (0, .035)], axis="-Y", loc=(0, -.145, 1.22)))
    P.add("Accent", L.cyl(.05, .04, (.3, 0, 1.6)))
    P.add("Eye", L.cyl(.035, .05, (.3, 0, 1.64)))
    return P


def drone(L):
    """The EVA tether drone: a boxy manoeuvring pack with grab handles, a camera pod on a short neck, four ducted
    fans and a tether reel at the back."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", L.box((.36, .32, .2), (0, .02, 0)))
    for sx in (-1, 1):
        P.add("Accent", [cable([(sx * .12, -.1, .09), (sx * .12, 0, .18), (sx * .12, .1, .09)], .014)])
    P.add("Accent", L.box((.06, .1, .06), (0, -.17, -.05)))
    P.add("Accent", L.ball(.06, (0, -.23, -.09)))
    P.add("Eye", L.lens(.035, (0, -.285, -.1)))
    P.add("Accent", L.cyl(.06, .14, (0, .2, -.02), axis="X"))
    duct = [(.2, 0), (.21, .01), (.21, .05), (.19, .06), (.17, .06), (.17, .01), (0, .01), (0, 0)]
    for i, (x, y) in enumerate(rot):
        P.add("Body", L.rod((x * .3, y * .3, 0), (x, y, .02), .03, kind="square"))
        P.add("Body", L.lathe(duct, loc=(x, y, 0)) + L.cyl(.04, .05, (x, y, .03)))
        P.add(f"AccentRotor{i}", L.rotor((x, y, .065), .32))
    return P


def vacuum(L):
    """The air recycler: a ceiling puck whose room face is an iris of eight petals over a glowing filter, three
    feed lines into its back."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.34, 0), (.36, .02), (.36, .07), (.33, .1), (0, .1)], seg=40))
    P.add("Accent", L.lathe([(.29, 0), (.33, 0), (.33, .03), (.29, .03)], loc=(0, 0, -.025)))
    P.add("Eye", L.cyl(.27, .01, (0, 0, -.004)))
    for k in range(8):
        a = k / 8 * math.tau
        P.add("Accent", [s.rbox((.24, .09, .012), (math.cos(a) * .15, math.sin(a) * .15, -.016), r=.004, seg=1,
                                rot=(0, 0, a + math.radians(20)))])
    P.add("Accent", L.cyl(.05, .03, (0, 0, -.02)))
    for k in range(3):
        a = k / 3 * math.tau + .4
        P.add("Accent", L.cyl(.03, .05, (math.cos(a) * .22, math.sin(a) * .22, .12)))
    return P


def plate(L):
    """A pressure plate with a lit border all round and a stud in every corner."""
    P = D.Parts()
    S, h = 1.1, .012
    P.add("Body", L.box((S - .07, S - .07, h), (0, 0, h / 2), r=.004))
    k = S / 2 - .06
    P.add("Accent", sum([L.box((.07, .07, h + .006), (x * k, y * k, (h + .006) / 2), r=.006) for x, y in ((-1, -1), (1, -1), (-1, 1), (1, 1))], []))
    b, e = .035, h + .004
    P.add("Eye", L.box((S, b, e), (0, S / 2 - b / 2, e / 2), r=.003) + L.box((S, b, e), (0, -S / 2 + b / 2, e / 2), r=.003)
          + L.box((b, S - 2 * b, e), (-S / 2 + b / 2, 0, e / 2), r=.003)
          + L.box((b, S - 2 * b, e), (S / 2 - b / 2, 0, e / 2), r=.003))
    return P


def attach_mic(L):
    """The shared microphone module at a lower segment count."""
    P = D.Parts()
    P.add("Accent", [s.rcyl(.045, .02, (0, 0, .01), seg=12, r=.004, bevel_seg=1)] + L.rod((0, 0, .01), (0, 0, .27), .014))
    P.add("Eye", [s.ball(.06, (0, 0, .32), seg=12)])
    P.add("Body", [s.rcyl(.066, .012, (0, 0, .32 + d), seg=12, r=.003, bevel_seg=1) for d in (-.03, 0, .03)])
    return P


# =========================================================================== the danger-zone robots
# The facility's own service and security machines from the same 1980s research-complex catalogue as the furniture:
# moulded enamel shells (beige-grey) with panel seams and service hatches, louvred vent grilles, ring joints, bent
# steel tube, deep lens housings. Concepts: Logs/comfy/danger_s16/<name>_*.png.

def _ring(center, radius, r, axis="Y", n=12):
    """A closed tube ring (a key bow, a shackle, a hoop) round `axis` through center."""
    c = V(center)
    pts = []
    for k in range(n + 1):
        a = k / n * math.tau
        u, v = math.cos(a) * radius, math.sin(a) * radius
        pts.append(c + {"X": V((0, u, v)), "Y": V((u, 0, v)), "Z": V((u, v, 0))}[axis])
    return s.tube([tuple(p) for p in pts], r, res=1, smooth=False)


def _poly_arc(center, radius, a0, a1, r, plane, n):
    """An open ring segment as straight runs (cheaper than the style's smooth arc)."""
    c = V(center)
    pts = []
    for k in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * k / n)
        u, v = math.cos(a) * radius, math.sin(a) * radius
        pts.append(tuple(c + {"XY": V((u, v, 0)), "XZ": V((u, 0, v)), "YZ": V((0, u, v))}[plane]))
    return s.tube(pts, r, res=1, smooth=False)


def _small_wheels(P, centres, radius, width):
    """Small casters and wheels (r <= 0.1): a rounded tyre and a hub cap at a lower segment count than the style's
    wheel — they are a few pixels on screen. Symmetric about the centre (they spin about it)."""
    for i, c in enumerate(centres):
        P.add(f"AccentWheel{i}", [s.rcyl(radius, width, c, axis="X", seg=12, r=min(.012, width * .25), bevel_seg=1),
                                  s.rcyl(radius * .45, width * 1.12, c, axis="X", seg=8, r=.004, bevel_seg=1)])


def _bent(points, r, bend=0.06, n=3):
    """The style's bent tube at a lower count (six sides, three steps a corner) for frames on small robots."""
    P_ = [V(p) for p in points]
    out = [P_[0]]
    for i in range(1, len(P_) - 1):
        a, p, b = P_[i - 1], P_[i], P_[i + 1]
        d1, d2 = a - p, b - p
        k = min(bend, d1.length * 0.45, d2.length * 0.45)
        q0, q1 = p + d1.normalized() * k, p + d2.normalized() * k
        for j in range(n + 1):
            u = j / n
            out.append(q0 * (1 - u) ** 2 + p * 2 * u * (1 - u) + q1 * u * u)
    out.append(P_[-1])
    return s.tube([tuple(o) for o in out], r, res=1, smooth=False)


def _key(top, length=.13, k=1.0):
    """A big service key hanging down from `top` (its bow): a flat bow ring, a round shank, two bit teeth."""
    x, y, z = top
    out = [_ring((x, y, z - .022 * k), .02 * k, .007 * k, axis="Y", n=10)]
    out.append(s.rcyl(.007 * k, length, (x, y, z - .04 * k - length / 2), seg=8, r=.002, bevel_seg=1))
    for d, h in ((.0, .022), (.026, .016)):
        out.append(s.rbox((.022 * k, .01 * k, h * k), (x + .014 * k, y, z - .04 * k - length + .012 + d), r=.002, seg=1))
    return out


def _padlock(top, w=.13):
    """A heavy padlock hanging from `top`: a round shackle into a thick rounded body with a keyhole plate."""
    x, y, z = top
    h = w * .9
    out = [s.tube([(x - w * .3, y, z - w * .42), (x - w * .3, y, z - w * .12), (x - w * .22, y, z - w * .02), (x, y, z + w * .03),
                   (x + w * .22, y, z - w * .02), (x + w * .3, y, z - w * .12), (x + w * .3, y, z - w * .42)], w * .07, res=1, segments_per=3)]
    out.append(s.rbox((w, w * .42, h), (x, y, z - w * .4 - h / 2), r=w * .14, seg=3))
    out.append(s.rcyl(w * .16, .012, (x, y - w * .21, z - w * .4 - h * .55), axis="Y", seg=12, r=.003, bevel_seg=1))
    return out


def _grille(center, w, h, n=4, axis="-Y"):
    """A louvred vent: a frame and n slats on a backing plate that sinks into the curved shell behind it."""
    back = {"-Y": V((0, 1, 0)), "Y": V((0, -1, 0)), "X": V((-1, 0, 0)), "-X": V((1, 0, 0))}[axis]
    size = (w - .01, .05, h - .01) if axis in ("-Y", "Y") else (.05, w - .01, h - .01)
    rot = {"-Y": (0, 0, 0), "Y": (0, 0, math.pi), "X": (0, 0, math.pi / 2), "-X": (0, 0, -math.pi / 2)}[axis]
    f, gap = .016, h / (n + 1)
    parts = [s.rbox((w - f, .012, .014), (0, 0, -h / 2 + gap * (k + 1)), r=0, seg=1, rot=(math.radians(-25), 0, 0)) for k in range(n)]
    parts += [s.rbox((f, .017, h), (x, 0, 0), r=.004, seg=1) for x in (-w / 2 + f / 2, w / 2 - f / 2)]
    parts += [s.rbox((w - f, .015, f), (0, 0, z), r=.004, seg=1) for z in (-h / 2 + f / 2, h / 2 - f / 2)]
    for q in parts:
        s.place(q, V(center), rot)
    return parts + [s.rbox(size, tuple(V(center) + back * .026), r=0, seg=1)]


def warden(L):
    """The door warden: a squat enamel drum on a four-wheel skirt with a belt and a louvred vent, a domed head on a
    neck ring with one deep lens under the peak of its cap shell, two arms on ring joints — the left holds a heavy
    padlock out in front, the right a bunch of keys on a ring at the hip. Concept: Logs/comfy/danger_s16/warden_*."""
    P = D.Parts()
    _small_wheels(P, D.WARDEN_WHEELS, *D.WARDEN_WHEEL)
    for x, y, _ in D.WARDEN_WHEELS:
        P.add("Accent", L.cyl(.03, .08, (x * .82, y, .085), axis="X"))                                       # axle stubs
    P.add("Body", L.shell([(.1, .28, .24, 3.0), (.16, .3, .26, 3.2), (.2, .29, .25, 3.0)], caps=.02))           # skirt
    P.add("Body", L.shell([(.18, .22, .19, 2.4), (.3, .26, .22, 2.6), (.62, .27, .23, 2.6), (.9, .25, .21, 2.6),
                           (1.0, .2, .17, 2.4)], caps=.06))                                                     # drum
    P.add("Accent", L.loft([(.5, .282, .242, 2.6), (.58, .282, .242, 2.6)]))                                    # belt
    P.add("Accent", L.box((.1, .04, .1), (0, -.24, .54), r=.015))                                               # buckle
    P.add("Accent", _grille((0, -.225, .8), .2, .12))                                                           # vent
    P.add("Eye", L.box((.08, .02, .02), (0, -.236, .69), r=.005))                                               # status strip
    P.add("Accent", L.cyl(.11, .07, (0, 0, 1.03)))                                                              # neck ring
    P.add("Body", L.shell([(1.05, .17, .17, 2.2), (1.16, .2, .2, 2.4), (1.3, .19, .19, 2.3), (1.37, .14, .15, 2.0)], caps=.04))
    P.add("Accent", L.shell([(1.32, .2, .2, 2.4, 0, .01), (1.4, .19, .19, 2.4, 0, .01), (1.47, .15, .15, 2.2, 0, .01)], caps=.03))  # cap
    P.add("Accent", [s.place(s.ball(.18, seg=12, scale=(1, .95, .08)), (0, -.14, 1.335), rot=(math.radians(-10), 0, 0))])  # the peak
    P.add("Eye", L.box((.06, .02, .03), (0, -.2, 1.42), r=.008))                                                # cap badge
    acc, glow = L.eye(.048, (0, -.24, 1.2), depth=.07)
    P.add("Accent", acc).add("Eye", glow)
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.05, .04, (sx * .2, -.01, 1.21), axis="X"))                                       # ear pods
    for sx in (-1, 1):
        sh = V((sx * .29, 0, .86))
        P.add("Accent", L.ball(.06, tuple(sh)))
        if sx > 0:                                                                                               # the left arm: padlock out
            el, wr = V((.33, -.06, .66)), V((.26, -.3, .7))
        else:                                                                                                    # the right: keys at the hip
            el, wr = V((-.35, -.02, .64)), V((-.34, -.08, .44))
        P.add("Body", L.rod(tuple(sh), tuple(el), .05))
        P.add("Accent", L.ball(.055, tuple(el)))
        P.add("Body", L.rod(tuple(el), tuple(wr), .048))
        P.add("Accent", L.box((.1, .1, .09), tuple(wr), r=.025))                                                # fist
    P.add("Accent", _padlock((.26, -.33, .66), w=.19))
    P.add("Accent", [_ring((-.34, -.08, .37), .045, .01, axis="X", n=12)])                                        # key ring
    for k, (dy, dx) in enumerate(((-.04, 0), (0, .025), (.04, -.015))):
        P.add("Accent", _key((-.34 + dx, -.08 + dy, .33), length=.12 + .025 * k, k=1.5))
    return P


def _wrench(p0, p1, w=.05):
    """A big open-ended spanner from p0 (the grip) to p1 (the open jaw), its flat facing forward (-y)."""
    a, b = V(p0), V(p1)
    d = (b - a).normalized()
    out = [align(s.rbox((w, w * .45, (b - a).length), r=.008, seg=1), a, b - d * w * .6)]
    jaw = s.rbox((w * 2.4, w * .5, w * 2.0), b, r=.012, seg=2)
    jaw = s.boolean(jaw, s.rbox((w * 1.1, w * 1.2, w * 1.6), b + d * w * .7, r=.004, seg=1))
    out.append(jaw)
    return out


def repairman(L):
    """The service crawler: a low rounded tool canister (an enamel shell over a grey base band, a louvred vent, a
    carry handle on the lid) between four crab legs on ring joints, a camera block on a post at the front left and a
    tool arm on the front right raising a big spanner. Concept: Logs/comfy/danger_s16/repairman_*."""
    P = D.Parts()
    P.add("Body", L.shell([(.16, .19, .15, 3.0), (.24, .22, .17, 3.2), (.42, .22, .17, 3.2), (.48, .19, .15, 3.0)], caps=.04))
    P.add("Accent", L.loft([(.16, .2, .16, 3.0), (.26, .225, .175, 3.2)]))                                       # base band
    P.add("Accent", _grille((.06, -.172, .36), .18, .1, n=3))
    P.add("Accent", [bent([(-.1, .04, .47), (-.1, .04, .55), (.1, .04, .55), (.1, .04, .47)], .014, bend=.03)])  # handle
    for i in range(4):
        hip, knee, ankle, foot = D.repairman_leg(i)
        P.add("Accent", L.ball(.05, tuple(hip.lerp(V((0, hip.y, hip.z)), .2))))                                  # hip socket
        P.add(f"AccentLeg{i}Upper", L.ball(.05, tuple(hip)) + L.rod(tuple(hip), tuple(knee), .042, kind="piston")
              + L.ball(.048, tuple(knee)))
        P.add(f"AccentLeg{i}Lower", L.rod(tuple(knee), tuple(ankle), .038) + L.ball(.036, tuple(ankle))
              + L.rod(tuple(ankle), tuple(foot + V((0, 0, .03))), .03)
              + [s.rcyl(.05, .03, tuple(foot + V((0, 0, .015))), seg=12, r=.008, bevel_seg=1)])
    # camera block on a post, front left
    P.add("Accent", L.cyl(.018, .14, (-.15, -.13, .5)))
    P.add("Body", L.box((.14, .1, .09), (-.15, -.15, .58), r=.025))
    acc, glow = L.eye(.032, (-.15, -.22, .58), depth=.05)
    P.add("Accent", acc).add("Eye", glow)
    # the tool arm, front right
    sh, el, wr = V((.16, -.1, .44)), V((.24, -.15, .62)), V((.2, -.17, .8))
    P.add("Accent", L.ball(.045, tuple(sh)))
    P.add("Body", L.rod(tuple(sh), tuple(el), .03) + L.rod(tuple(el), tuple(wr), .027))
    P.add("Accent", L.ball(.038, tuple(el)) + L.box((.06, .06, .07), tuple(wr), r=.015))
    P.add("Accent", _wrench(wr + V((0, 0, .02)), wr + V((.01, -.01, .3)), w=.06))
    return P


def _disc_grille(center, radius, axis="-Y", n=5, depth=.04):
    """A round louvred outlet: a rolled rim, horizontal slats across it, a dark backing disc (all Accent)."""
    c = V(center)
    a = {"-Y": V((0, -1, 0)), "Y": V((0, 1, 0))}[axis]
    out = [s.lathe([(radius * .8, 0), (radius, 0), (radius * 1.04, depth * .5), (radius, depth), (radius * .8, depth)], seg=20,
                   axis=axis, loc=tuple(c - a * depth * .5))]
    out.append(s.rcyl(radius * .82, depth * .8, tuple(c - a * depth * .1), axis="Y", seg=20, r=.004, bevel_seg=1))
    for k in range(n):
        z = -radius * .7 + radius * 1.4 * (k + .5) / n
        w = 2 * math.sqrt(max(0.0, (radius * .82) ** 2 - z * z))
        out.append(s.rbox((w, .012, radius * .16), tuple(c + a * depth * .35 + V((0, 0, z))), r=.003, seg=1,
                          rot=(math.radians(-25), 0, 0)))
    return out


def smoker(L):
    """The fog cart: a lying enamel pressure tank in two cradle straps on a bent-tube chassis with four wheels, a
    round louvred outlet on its nose, three flared stacks and a gauge on top, a compressor box with a vent on the back
    and hoses from it to the stacks. It has no eyes: it senses nothing. Concept: Logs/comfy/danger_s16/smoker_*."""
    P = D.Parts()
    _small_wheels(P, D.SMOKER_WHEELS, *D.SMOKER_WHEEL)
    for sx in (-1, 1):
        x = sx * .19
        P.add("Accent", [_bent([(x, -.3, .26), (x, -.2, .14), (x, .2, .14), (x, .3, .26)], .022, bend=.05)])   # chassis tubes
        for y in (-.2, .2):
            P.add("Accent", L.cyl(.026, .1, (sx * .225, y, .1), axis="X"))                                    # axle stubs
            P.add("Body", L.box((.06, .08, .12), (x, y, .12), r=.015))                                        # axle boxes
    for y in (-.2, .2):
        P.add("Accent", [s.tube([(-.19, y, .14), (.19, y, .14)], .02, res=2, smooth=False)])
        P.add("Accent", [_poly_arc((0, y, .5), .275, 200, 340, .022, "XZ", 8)])                          # cradle straps
        for sx in (-1, 1):
            P.add("Accent", L.rod((sx * .19, y, .14), (sx * .2, y, .33), .02))
    P.add("Body", L.shell([(0, .2, .2, 2.0), (.08, .265, .265, 2.0), (.52, .265, .265, 2.0), (.62, .19, .19, 2.0)], axis="Y",
                          loc=(0, -.32, .5), caps=.05))                                                       # the tank
    P.add("Accent", L.loft([(0, .272, .272, 2.0), (.04, .272, .272, 2.0)], axis="Y", loc=(0, -.02, .5)))       # weld band
    P.add("Accent", _disc_grille((0, -.33, .5), .16))                                                         # outlet
    for k, (x, y, h, lean) in enumerate(((-.12, -.1, .3, -14), (0, .02, .38, 0), (.12, -.1, .3, 14))):
        base = V((x, y, .7))
        P.add("Body", L.cyl(.05, .1, (x, y, .74)))
        P.add("Accent", [lathe_between([(.038, 0), (.038, .7), (.06, .92), (.064, 1), (.05, 1)], base + V((0, 0, .05)),
                                       base + V((math.sin(math.radians(lean)) * h, 0, .05 + math.cos(math.radians(lean)) * h)), seg=12)])
    P.add("Accent", L.rod((.0, -.24, .74), (0, -.26, .9), .012))
    P.add("Accent", L.cyl(.06, .03, (0, -.27, .93), axis="Y"))                                                # gauge
    P.add("Body", L.cyl(.048, .012, (0, -.288, .93), axis="Y"))
    P.add("Body", L.box((.38, .16, .3), (0, .38, .44), r=.03))                                                # compressor
    P.add("Accent", _grille((0, .462, .44), .26, .18, n=4, axis="Y"))
    for x in (-.12, .12):
        P.add("Accent", [cable([(x, .38, .6), (x * 1.2, .3, .82), (x, .12, .8), (x, -.04, .78)], .018)])     # hoses
    P.add("Accent", L.cyl(.05, .02, (.2, .38, .62), axis="Z"))                                               # valve wheel
    P.add("Accent", [_ring((.2, .38, .66), .05, .008, axis="Z", n=12)])
    return P


def relay(L):
    """The paging post: a domed enamel weighted base on the floor with a rubber foot ring, a fluted steel mast with
    collars and a junction box, a rounded speaker housing on top with a louvred front speaker and lit indicator lamps,
    two horn loudspeakers turned out to the sides and a whip antenna. Eye: the speakers' membranes and the lamps.
    Concept: Logs/comfy/danger_s16/relay_*."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.34, 0), (.345, .015), (.33, .03), (0, .03)]))                          # foot ring
    P.add("Body", L.lathe([(0, .02), (.32, .02), (.33, .05), (.3, .09), (.2, .14), (.08, .17), (0, .17)]))    # domed base
    P.add("Eye", [s.ball(.018, (math.cos(a) * .27, math.sin(a) * .27, .08), seg=8) for a in (-1.57, 0.52, 2.6)])
    P.add("Accent", L.cyl(.06, .06, (0, 0, .19)))
    P.add("Body", L.cyl(.04, 1.12, (0, 0, .74)))                                                              # mast
    for z in (.32, .9, 1.22):
        P.add("Accent", L.cyl(.055, .04, (0, 0, z)))
    P.add("Body", L.box((.12, .08, .16), (0, -.05, .6), r=.02))                                               # junction box
    P.add("Accent", [cable([(.02, -.05, .68), (.05, -.03, 1.0), (.04, -.02, 1.3)], .012)])
    # the speaker housing
    P.add("Body", L.shell([(1.28, .18, .15, 2.6), (1.36, .23, .18, 2.8), (1.58, .23, .18, 2.8), (1.66, .18, .15, 2.6)], caps=.04))
    P.add("Accent", L.lathe([(0, 0), (.13, 0), (.135, .015), (.12, .03), (0, .03)], axis="-Y", loc=(0, -.165, 1.46)))
    P.add("Eye", L.lathe([(.1, 0), (.075, .012), (.03, .026), (0, .03)], axis="-Y", loc=(0, -.19, 1.46)))     # membrane
    for x in (-.12, .12):
        P.add("Eye", L.box((.05, .02, .025), (x, -.185, 1.6), r=.006))                                       # lamps
    P.add("Accent", L.cyl(.06, .03, (0, .02, 1.675)))
    P.add("Eye", L.cyl(.045, .05, (0, .02, 1.71), radius2=.035))                                              # beacon
    for sx in (-1, 1):
        a = V((sx * math.cos(math.radians(30)), math.sin(math.radians(30)), .12)).normalized()
        base = V((sx * .21, .0, 1.5))
        P.add("Accent", L.cyl(.05, .08, tuple(base), axis="X"))
        P.add("Body", [lathe_between([(.04, 0), (.045, .12), (.08, .26), (.16, .36), (.17, .38), (.15, .38), (.12, .33),
                                      (.05, .22), (0, .2)], base, base + a, seg=16)])                       # hollow bell
        P.add("Eye", [lathe_between([(.07, 0), (.05, .66), (0, 1)], base + a * .2, base + a * .23, seg=12)])  # throat
    P.add("Accent", L.cyl(.03, .05, (.1, .05, 1.66)))
    P.add("Accent", L.rod((.1, .05, 1.68), (.1, .05, 1.8), .008))
    P.add("Eye", L.ball(.018, (.1, .05, 1.8)))
    return P


def _syringe(base, tip, r=.035):
    """A big syringe from its plunger end (base) to the needle tip: a thumb ring, a plunger rod, a finger flange,
    a barrel with graduation rings, a hub and the needle."""
    a, b = V(base), V(tip)
    d = (b - a).normalized()
    L_ = (b - a).length
    out = [lathe_between([(0, 0), (r * .9, 0), (r * .9, 1), (0, 1)], a, a + d * .014, seg=12)]
    out.append(lathe_between([(r * .25, 0), (r * .25, 1)], a, a + d * L_ * .3, seg=8))
    out.append(lathe_between([(0, 0), (r * 1.8, 0), (r * 1.8, .6), (r * 1.1, 1), (0, 1)], a + d * L_ * .27, a + d * L_ * .3, seg=12))
    out.append(lathe_between([(0, 0), (r, 0), (r, .85), (r * .5, .93), (r * .3, 1), (0, 1)], a + d * L_ * .28,
                             a + d * L_ * .86, seg=12))
    out.append(lathe_between([(r * .14, 0), (r * .1, .9), (0, 1)], a + d * L_ * .84, b, seg=6))
    return out


def nurse(L):
    """The ward nurse: a white enamel skirt shell flaring onto a round base over three hidden casters, a fitted bodice
    with a lit name plate and a waist ring, slim arms on ring joints — the right raises a big syringe, the left ends in
    a clamp — an egg head on a neck collar with one lens and two ear pods, a folded cap with a cross.
    Concept: Logs/comfy/danger_s16/nurse_*."""
    P = D.Parts()
    _small_wheels(P, D.NURSE_WHEELS, *D.NURSE_WHEEL)
    for x, y, _ in D.NURSE_WHEELS:
        P.add("Accent", L.cyl(.012, .07, (x, y, .085)) + L.box((.05, .02, .04), (x, y, .07), r=.006))         # caster forks
    P.add("Accent", L.lathe([(0, .09), (.25, .09), (.27, .11), (.27, .14), (.24, .15), (0, .15)]))            # base ring
    P.add("Body", L.lathe([(0, .12), (.24, .12), (.26, .17), (.23, .36), (.19, .58), (.15, .8), (.13, .92), (0, .92)]))  # skirt
    P.add("Accent", L.cyl(.14, .05, (0, 0, .94)))                                                             # waist ring
    P.add("Body", L.shell([(.95, .13, .1, 2.4), (1.12, .16, .11, 2.6), (1.28, .21, .12, 2.6), (1.36, .2, .11, 2.4)], caps=.03))
    P.add("Accent", L.box((.14, .03, .06), (0, -.115, 1.2), r=.01))
    P.add("Eye", L.box((.09, .02, .025), (0, -.13, 1.2), r=.005))                                            # name plate
    P.add("Accent", L.cyl(.055, .1, (0, 0, 1.4)))                                                            # neck collar
    P.add("Body", L.ball(.14, (0, -.02, 1.55), scale=(1, 1.05, 1.2)))                                        # egg head
    acc, glow = L.eye(.04, (0, -.2, 1.53), depth=.07)
    P.add("Accent", acc).add("Eye", glow)
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.04, .04, (sx * .14, -.02, 1.55), axis="X"))
    P.add("Body", L.shell([(1.64, .155, .13, 3.0, 0, .0), (1.74, .16, .135, 3.0, 0, .0), (1.81, .145, .12, 3.0, 0, .01)], caps=.015))  # cap
    P.add("Body", L.box((.3, .03, .1), (0, -.13, 1.76), r=.01, rot=(math.radians(10), 0, 0)))                # its front fold
    P.add("Accent", L.box((.075, .02, .024), (0, -.15, 1.76), r=.005) + L.box((.024, .02, .075), (0, -.15, 1.76), r=.005))  # cross
    for sx in (-1, 1):
        sh = V((sx * .24, 0, 1.3))
        P.add("Accent", L.ball(.05, tuple(sh)))
        if sx < 0:                                                                                               # right: syringe up
            el, wr = V((-.3, -.05, 1.08)), V((-.3, -.16, 1.3))
        else:                                                                                                    # left: clamp
            el, wr = V((.29, .0, 1.06)), V((.27, -.1, .9))
        P.add("Body", L.rod(tuple(sh), tuple(el), .036) + L.rod(tuple(el), tuple(wr), .032))
        P.add("Accent", L.ball(.04, tuple(el)) + L.box((.06, .06, .06), tuple(wr), r=.015))
    P.add("Accent", _syringe((-.3, -.14, 1.18), (-.3, -.2, 1.72), r=.036))
    for dx in (-.02, .02):
        P.add("Accent", L.rod((.27 + dx, -.12, .87), (.27 + dx * 1.6, -.16, .78), .012))                      # clamp jaws
    return P


def _sq_bar(p0, p1, w, r=.012):
    return align(s.rbox((w, w, (V(p1) - V(p0)).length), r=r, seg=2), V(p0), V(p1))


def mimic(L):
    """The crate that is not a crate. The searchable crate is the same in every style (poi.model_crate), so the mimic
    keeps its every big form — the 3 cm walls, the two rib bands, the corner caps, the side grips, the front latches,
    the hinged lid with its inset panel — and the tells are this style's: the front wall stops short of the lid, and in
    that dark slot sit a comb of steel blades (lower on the wall, upper on the lid's lip) and one small deep lens in a
    bezel; the corner angle irons are folded legs on hinge knuckles. Concept: Logs/comfy/danger_s16/mimic_*."""
    P = D.Parts()
    t = .03
    body = [s.rbox((.78, .58, .05), (0, 0, .025), r=.02, seg=2)]                                      # floor
    body += [s.rbox((t, .58, .5), (x * (.39 - t / 2), 0, .25), r=.012, seg=2) for x in (-1, 1)]       # side walls
    body.append(s.rbox((.78 - 2 * t, t, .5), (0, .29 - t / 2, .25), r=.012, seg=2))                   # back wall
    body.append(s.rbox((.78 - 2 * t + .01, t, .47), (0, -.29 + t / 2, .235), r=.012, seg=2))          # front: short
    body.append(s.rbox((.7, .5, .03), (0, 0, .41), r=.01, seg=1))                                     # the slot's floor
    for z in (.06, .44):
        body += [s.rbox((.02, .6, .045), (x * .39, 0, z), r=.008, seg=1) for x in (-1, 1)]
        body += [s.rbox((.8, .02, .045), (0, y * .29, z), r=.008, seg=1) for y in (-1, 1)]
    P.add("Body", body)
    acc = []
    for x in (-1, 1):                                                                                  # side grips
        acc.append(s.rbox((.03, .22, .035), (x * .405, 0, .3), r=.012, seg=2))
        acc += [s.rbox((.04, .03, .05), (x * .395, y * .1, .3), r=.008, seg=1) for y in (-1, 1)]
    for x in (-.22, .22):                                                                              # latches
        acc.append(s.rbox((.08, .025, .07), (x, -.3, .4), r=.008, seg=2))
        acc.append(s.rbox((.05, .022, .08), (x, -.307, .462), r=.008, seg=2, taper=(.6, 1.0)))
    acc += [s.rbox((.12, .02, .06), (x, .3, .465), r=.006, seg=1) for x in (-.25, .25)]               # hinge leaves
    for x in (-.32, -.27, -.16, -.11, .11, .16, .27, .32):                                             # lower blades
        acc.append(s.rbox((.026, .012, .04), (x, -.278, .47), r=.003, seg=1, taper=(.25, 1.0)))
    P.add("Accent", acc)
    lid = s.rbox((.8, .6, .1), (0, 0, .548), r=.025, seg=3)
    s.inset_faces(lid, s.facing((0, 0, 1), min_area=.1, tol=.95), thickness=.04, depth=-.012)
    lidp = [lid]
    lidp += [s.rcyl(.016, .1, (x, .3, .5), axis="X", seg=12, r=.003) for x in (-.25, .25)]           # knuckles
    lidp += [s.rbox((.1, .016, .07), (x, .303, .54), r=.005, seg=1) for x in (-.25, .25)]            # leaves
    lidp += [s.rbox((.06, .03, .04), (x, -.305, .53), r=.008, seg=1) for x in (-.22, .22)]           # catches
    lidp += [s.rbox((.33, .014, .03), (x * .215, -.293, .503), r=.005, seg=1) for x in (-1, 1)]      # the lip
    for x in (-.295, -.135, .135, .295):                                                               # upper blades
        b = s.rbox((.026, .012, .036), (x, -.282, .487), r=.003, seg=1, taper=(1.0, 1.0))
        s.taper(b, "Z", .25, 1.0)
        lidp.append(b)
    P.add("AccentLid", lidp)
    P.add("BodySocket", [s.rcyl(.03, .03, (0, -.27, .489), axis="Y", seg=16, r=.004)])
    P.add("Accent", [s.lathe([(.024, 0), (.026, .006), (.02, .012), (0, .012)], seg=16, axis="-Y", loc=(0, -.284, .489))])
    P.add("Eye", [s.lathe([(.015, 0), (.015, .004), (.009, .011), (0, .013)], seg=16, axis="-Y", loc=(0, -.29, .489))])
    for i, (cx, cy) in enumerate(D.MIMIC_CORNERS):
        sx, sy = (1 if cx > 0 else -1), (1 if cy > 0 else -1)
        c = V((cx, cy, 0))
        out = V((sx, sy, 0)).normalized()
        hip, knee, foot = c + out * -.004 + V((0, 0, .462)), c + out * .009 + V((0, 0, D.MIMIC_KNEE)), c + V((0, 0, .03))
        leg = f"AccentLeg{i}"
        P.add(leg, [_sq_bar(hip, knee, .074), _sq_bar(knee, foot, .078)])
        P.add(leg, [s.rbox((.086, .086, .05), (hip.x, hip.y, .45), r=.014, seg=2)])                     # top cap
        P.add(leg, [s.rbox((.096, .096, .11), (c.x, c.y, .055), r=.02, seg=2)])                         # foot: corner cap
        ax = V((-sy, sx, 0)).normalized() * .04
        k = knee + out * .006
        P.add(leg, [align(s.rcyl(.02, .08, seg=12, r=.004, bevel_seg=1), k - ax, k + ax)])              # knee knuckle
    P.split = {f"AccentLeg{i}": D.MIMIC_KNEE for i in range(4)}
    return P


def salesman(L):
    """The vending kiosk: a tall rounded enamel cabinet on a grey plinth with four casters, a coin-and-keypad panel and
    a louvred vent, an awning roof with alternating stripes for a hat, a deep CRT face tipped up at the camera (the
    screen glows), a megaphone on a bent-tube bracket at its side, an arm holding out a tray of boxed wares, a price
    flag on a whip. Concept: Logs/comfy/danger_s16/salesman_*."""
    P = D.Parts()
    _small_wheels(P, D.SALESMAN_WHEELS, *D.SALESMAN_WHEEL)
    for x, y, _ in D.SALESMAN_WHEELS:
        P.add("Accent", L.box((.05, .07, .07), (x, y, .17), r=.01) + L.cyl(.02, .09, (x, y, .09), axis="X"))  # forks
    P.add("Accent", L.box((.6, .5, .08), (0, 0, .24), r=.025))                                          # plinth
    P.add("Body", L.box((.48, .38, .86), (0, .02, .7), r=.05))                                          # cabinet
    P.add("Accent", _grille((0, -.17, .42), .26, .12, n=3))
    P.add("Accent", L.box((.14, .03, .2), (.12, -.172, .8), r=.012))                                     # keypad panel
    for k in range(3):
        for j in range(2):
            P.add("Body", L.box((.035, .02, .03), (.095 + j * .05, -.188, .86 - k * .05), r=.005))
    P.add("Body", L.box((.18, .03, .06), (-.1, -.17, .66), r=.01))                                       # pickup flap
    # head: a deep CRT tipped up 16 degrees on a neck block
    P.add("Accent", L.box((.2, .16, .1), (0, .02, 1.17), r=.02))
    head = [s.rbox((.44, .2, .34), (0, -.03, 1.33), r=.04, seg=3),
            s.rbox((.36, .24, .28), (0, .14, 1.33), r=.05, seg=2, taper=(.8, .8), rot=(math.radians(90), 0, 0))]
    glow = [s.rbox((.34, .03, .25), (0, -.135, 1.33), r=.025, seg=2)]
    for bm in head + glow:
        bmesh.ops.rotate(bm, cent=V((0, -.03, 1.33)), matrix=Euler((math.radians(-16), 0, 0)).to_matrix(), verts=bm.verts)
    P.add("Body", head).add("Eye", glow)
    # the awning: a stepped shallow roof, eight panels alternating Body / Accent
    roof = s.lathe([(0, 0), (.3, 0), (.31, .02), (.27, .05), (.05, .12), (0, .13)], seg=16, loc=(0, .03, 1.5))
    P.add("Body", [roof])
    for k in range(8):                                                                                  # stripes: wedge ribs
        a = k / 8 * math.tau
        if k % 2:
            rib = s.loft([(0, .014, .035), (.24, .014, .09)], seg=8, axis="X")
            bmesh.ops.rotate(rib, cent=V((0, 0, 0)), matrix=Euler((0, math.radians(18), a)).to_matrix(), verts=rib.verts)
            bmesh.ops.translate(rib, vec=V((math.cos(a) * .045, .03 + math.sin(a) * .045, 1.622)), verts=rib.verts)
            P.add("Accent", [rib])
    P.add("Accent", L.cyl(.045, .04, (0, .03, 1.63)))
    P.add("Accent", L.rod((.12, .06, 1.57), (.12, .06, 1.95), .008))                                    # flag whip
    P.add("Accent", [s.rbox((.12, .01, .07), (.18, .06, 1.9), r=.004, seg=1)])
    # megaphone on a bracket, its side
    P.add("Accent", [bent([(-.22, .02, 1.05), (-.3, .02, 1.05), (-.3, .0, 1.3)], .014, bend=.04)])
    mp = V((-.32, -.02, 1.32))
    P.add("Accent", L.cyl(.03, .1, tuple(mp), axis="Y"))
    P.add("Body", [lathe_between([(.03, 0), (.035, .3), (.1, .9), (.105, 1), (.085, 1), (0, .55)], mp + V((0, -.03, 0)),
                                 mp + V((0, -.3, .03)), seg=16)])
    # the tray arm
    sh, wr = V((.25, -.05, 1.0)), V((.24, -.28, .98))
    P.add("Accent", L.ball(.045, tuple(sh)))
    P.add("Body", L.rod(tuple(sh), tuple(wr), .03))
    P.add("Accent", [s.rbox((.34, .2, .02), (.12, -.33, .96), r=.006, seg=1)])                          # the tray
    P.add("Accent", [s.rbox((.34, .012, .03), (.12, -.43, .975), r=.004, seg=1)])
    for k, (x, y, w, h) in enumerate(((-.0, -.3, .07, .08), (.09, -.36, .06, .11), (.18, -.3, .08, .06), (.24, -.37, .05, .09))):
        P.add("Body", [s.rbox((w, .06, h), (x, y, .97 + h / 2), r=.008, seg=1)])
    return P


def gunner(L):
    """The armed guard: the station synth in pressure-suit armour — a domed chest shell over a ribbed waist, big
    domed pauldrons, a helmet with one deep lens behind a visor ring — its right forearm replaced by a rotary gun (a
    drum housing, six barrels, a muzzle ring), an ammo canister on its back feeding a belt over the shoulder to the
    gun, armoured legs with knee domes and broad boots. Concept: Logs/comfy/danger_s16/gunner_*."""
    P = D.Parts()
    hx, hz, kz = D.GUNNER_HIP_X, D.GUNNER_HIP_Z, D.GUNNER_KNEE
    P.add("Body", L.shell([(.86, .22, .15, 2.6), (.96, .25, .17, 2.8), (1.06, .22, .15, 2.6)], caps=.03))   # pelvis
    P.add("Body", L.cyl(.11, .34, (0, .02, 1.14)))                                                          # waist core
    for z in (1.08, 1.14, 1.2):
        P.add("Accent", L.cyl(.15 - abs(z - 1.14) * .4, .045, (0, .02, z)))                                 # ribbed waist
    P.add("Body", L.shell([(1.2, .26, .19, 2.6, 0, .01), (1.38, .36, .25, 2.8, 0, -.01), (1.58, .37, .25, 2.8),
                           (1.72, .27, .19, 2.4)], caps=.06))                                                   # chest dome
    P.add("Accent", L.box((.22, .04, .12), (0, -.245, 1.42), r=.015))                                       # chest hatch
    P.add("Eye", L.box((.12, .02, .02), (0, -.262, 1.52), r=.005))
    P.add("Accent", L.cyl(.09, .1, (0, 0, 1.74)))                                                           # neck ring
    P.add("Body", L.ball(.17, (0, -.02, 1.87), scale=(1, 1.1, .95)))                                      # helmet
    P.add("Accent", L.cyl(.175, .04, (0, -.02, 1.95), rot=(math.radians(-8), 0, 0)))                      # brow band
    P.add("Accent", L.lathe([(0, 0), (.075, 0), (.08, .015), (.075, .03), (0, .03)], axis="-Y", loc=(0, -.18, 1.84)))  # visor ring
    acc, glow = L.eye(.05, (0, -.23, 1.84), depth=.06)
    P.add("Accent", acc).add("Eye", glow)
    # ammo canister on the back, the belt over the right shoulder to the gun
    P.add("Body", L.box((.38, .2, .34), (0, .32, 1.42), r=.04))
    P.add("Accent", L.box((.4, .22, .04), (0, .32, 1.5), r=.01))
    belt = [V((-.18, .25, 1.56)), V((-.3, .12, 1.66)), V((-.44, -.04, 1.55)), V((-.5, -.2, 1.26)), V((-.5, -.3, 1.18))]
    P.add("Accent", [s.tube([tuple(p) for p in belt], .028, res=1, segments_per=4)])
    for sx in (-1, 1):
        sh = V((sx * .44, 0, 1.58))
        P.add("Body", L.ball(.17, (sx * .46, 0, 1.64), scale=(1, 1.1, .8)))                                # pauldron
        P.add("Accent", L.cyl(.18, .03, (sx * .47, 0, 1.58), rot=(0, math.radians(18 * sx), 0)))
        el = V((sx * .5, -.06, 1.18)) if sx > 0 else V((-.5, -.18, 1.12))
        P.add("Body", L.rod(tuple(sh), tuple(el), .07))
        P.add("Accent", L.ball(.075, tuple(el)))
        if sx > 0:                                                                                           # left: forearm and fist
            P.add("Body", L.shell([(.86, .07, .075, 2.4, .5, -.12), (1.02, .085, .09, 2.6, .5, -.09), (1.16, .07, .075, 2.4, .5, -.06)], caps=.02))
            P.add("Accent", L.box((.12, .14, .14), (.5, -.13, .8), r=.03))
    # the rotary gun on the right forearm
    gx, gz = -.5, 1.1
    P.add("Body", L.shell([(0, .1, .1, 2.2), (.12, .11, .11, 2.2), (.3, .09, .09, 2.2)], axis="-Y", loc=(gx, -.16, gz), caps=.03))
    P.add("Accent", L.cyl(.085, .05, (gx, -.48, gz), axis="Y") + L.cyl(.07, .04, (gx, -.75, gz), axis="Y"))  # barrel clamps
    for k in range(6):
        a = k / 6 * math.tau
        P.add("Accent", L.cyl(.017, .52, (gx + math.cos(a) * .045, -.72, gz + math.sin(a) * .045), axis="Y", seg=8))
    P.add("Accent", L.cyl(.02, .53, (gx, -.72, gz), axis="Y", seg=8))
    P.add("Accent", [s.lathe([(.05, 0), (.075, 0), (.075, .04), (.05, .04)], seg=16, axis="-Y", loc=(gx, -.94, gz))])  # muzzle ring
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * hx
        P.add(leg, L.ball(.1, (x, 0, hz)))
        P.add(leg, L.shell([(kz + .02, .09, .1, 2.6, x, 0), (.7, .12, .13, 2.8, x, -.005), (hz, .11, .12, 2.6, x, 0)], caps=.03))
        P.add(leg, L.ball(.09, (x, -.04, kz)))                                                                # knee dome
        P.add(leg, L.shell([(.1, .08, .09, 2.6, x, 0), (.3, .1, .12, 2.8, x, .015), (kz - .01, .085, .095, 2.6, x, 0)], caps=.03))
        P.add(leg, L.shell([(0, .1, .19, 2.6, x, -.06), (.09, .095, .18, 2.6, x, -.06), (.14, .07, .1, 2.4, x, -.02)], caps=.025))
    P.split = {"AccentLeg0": kz, "AccentLeg1": kz}
    return P


def driller(L):
    """The breaching crawler: a low enamel hull with a grey skirt on two crawler tracks, a fluted cone drill on a
    round gearbox out in front carried by two hydraulic rams, a camera box on a post with a lit visor slit, a louvred
    engine deck and two exhaust stacks behind. The drill (AccentDrill) is round about its axis: it spins.
    Concept: Logs/comfy/danger_s16/driller_*."""
    P = D.Parts()
    for sx in (-1, 1):                                    # (the style's track() lays its belt on edge: build it here)
        x, w, d, r = sx * .4, .22, 1.0, .21
        P.add("BodyTracks", [s.loft([(-w / 2, r, d / 2, 6.0), (w / 2, r, d / 2, 6.0)], seg=16, axis="X", loc=(x, .1, r))])
        for k in range(4):
            P.add("BodyTracks", [s.rcyl(r * .62, w * 1.1, (x, .1 - d / 2 + r + (d - 2 * r) * k / 3, r), axis="X", seg=12, r=.008, bevel_seg=1)])
    P.add("Accent", L.box((.56, .9, .14), (0, .1, .3), r=.03))                                              # grey skirt
    P.add("Body", L.shell([(.34, .3, .44, 3.2, 0, .12), (.6, .33, .46, 3.4, 0, .12), (.74, .28, .4, 3.0, 0, .14)], caps=.05))  # hull
    for sx in (-1, 1):
        P.add("Body", L.box((.12, .9, .06), (sx * .4, .1, .44), r=.02))                                     # track guards
    P.add("Accent", L.box((.4, .3, .03), (0, .32, .755), r=.01))                                            # engine deck
    for x in (-.12, -.04, .04, .12):
        P.add("Body", L.box((.03, .26, .02), (x, .32, .775), r=.006))                                       # its louvres
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.035, .3, (sx * .2, .48, .9)))                                                # exhaust stacks
        P.add("Body", L.cyl(.045, .04, (sx * .2, .48, 1.05)))
    # the drill head: a gearbox drum, the rams from the hull, the cone
    P.add("Body", L.shell([(0, .2, .2, 2.0), (.06, .22, .22, 2.0), (.16, .2, .2, 2.0)], axis="-Y", loc=(0, -.22, .62), caps=.03))
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .2, -.1, .56), (sx * .17, -.3, .62), .03, kind="piston"))
    P.add("AccentDrill", L.cyl(.08, .05, (0, -.385, .62), axis="Y"))                                             # hub into the gearbox
    P.add("AccentDrill", [s.lathe([(0, 0), (.27, 0), (.295, .02), (.295, .07), (.27, .09)], seg=20, axis="-Y", loc=(0, -.39, .62))])
    P.add("AccentDrill", [s.lathe([(.27, 0), (.2, .14), (.11, .3), (.03, .42), (0, .43)], seg=20, axis="-Y", loc=(0, -.47, .62))])
    flute = []
    for k in range(43):                                                                                       # one spiral flute
        t = k / 42
        y = -.48 - .4 * t
        rr = .27 - .25 * t + .006
        a = t * math.tau * 2.2
        flute.append((math.cos(a) * rr, y, .62 + math.sin(a) * rr))
    P.add("AccentDrill", [s.tube(flute, .016, res=1, segments_per=2, radii=[1.0] * 30 + [.8, .7, .6, .5, .4, .35, .3, .25, .2, .15, .1, .08, .05])])
    # the camera on a post
    P.add("Accent", L.cyl(.05, .1, (0, -.06, .79)) + L.cyl(.025, .25, (0, -.06, .95)))
    P.add("Body", L.box((.24, .2, .14), (0, -.1, 1.15), r=.03))
    P.add("Accent", L.box((.2, .04, .06), (0, -.19, 1.15), r=.012))
    P.add("Eye", L.box((.15, .02, .022), (0, -.212, 1.15), r=.006))                                         # visor slit
    P.add("Accent", L.rod((.09, -.05, 1.2), (.09, -.05, 1.38), .007))
    return P


def exterminator(L):
    """The pest-control cart: a rounded enamel body on a four-wheel tube chassis, a gas-mask head with one big lens and
    a filter canister for a snout, two tall gas cylinders on the back with hazard bands, hoses up to two spray booms
    curving out and up with nozzles and a third nozzle straight up, a striped bumper. Concept:
    Logs/comfy/danger_s16/exterminator_*."""
    P = D.Parts()
    D.wheels(P, L, D.EXTERMINATOR_WHEELS, *D.EXTERMINATOR_WHEEL)
    for sx in (-1, 1):
        P.add("Accent", [_bent([(sx * .26, -.36, .26), (sx * .26, -.24, .2), (sx * .26, .24, .2), (sx * .26, .36, .26)], .025, bend=.05)])
        for y in (-.24, .24):
            P.add("Accent", L.cyl(.035, .12, (sx * .3, y, .14), axis="X"))
            P.add("Body", L.box((.06, .1, .12), (sx * .26, y, .17), r=.015))
    for y in (-.24, .24):
        P.add("Accent", [s.tube([(-.26, y, .2), (.26, y, .2)], .022, res=2, smooth=False)])
    P.add("Accent", L.box((.5, .56, .06), (0, 0, .22), r=.015))                                             # deck
    P.add("Body", L.box((.62, .14, .08), (0, -.4, .24), r=.03))                                              # bumper
    for k in range(5):
        P.add("Accent", L.box((.05, .02, .07), (-.22 + k * .11, -.475, .24), r=.006, rot=(0, math.radians(30), 0)))
    P.add("Body", L.shell([(.22, .26, .22, 3.0), (.3, .3, .26, 3.2), (.78, .29, .25, 3.2), (.9, .23, .2, 2.8)], caps=.05))  # body
    P.add("Accent", L.loft([(.28, .305, .265, 3.2), (.36, .305, .265, 3.2)]))
    P.add("Accent", _grille((.12, -.252, .6), .18, .2, n=4))
    P.add("Body", L.box((.12, .03, .1), (-.14, -.25, .62), r=.012))                                         # service hatch
    # the gas mask head
    P.add("Accent", L.cyl(.12, .06, (0, -.02, .92)))
    P.add("Body", L.ball(.16, (0, -.06, 1.06), scale=(1, 1.0, 1.05)))
    P.add("Body", L.shell([(0, .13, .12, 2.4), (.06, .14, .13, 2.6)], axis="-Y", loc=(0, -.14, 1.05), caps=.02))  # face plate
    acc, glow = L.eye(.055, (0, -.24, 1.06), depth=.06)
    P.add("Accent", acc).add("Eye", glow)
    P.add("Accent", L.cyl(.05, .1, (0, -.24, .93), axis="Y", rot=(math.radians(-30), 0, 0)))                # snout
    P.add("Body", L.cyl(.07, .08, (0, -.31, .9), axis="Y", rot=(math.radians(-30), 0, 0)))                  # filter can
    for sx in (-1, 1):
        P.add("Body", L.cyl(.055, .07, (sx * .15, -.14, .96), axis="X"))                                    # side filters
    # the tanks
    for sx in (-1, 1):
        x = sx * .17
        P.add("Body", L.lathe([(0, 0), (.11, 0), (.12, .03), (.12, .62), (.09, .72), (.04, .76), (0, .76)], loc=(x, .3, .42)))
        P.add("BodyBands", L.cyl(.125, .05, (x, .3, .58)) + L.cyl(.125, .05, (x, .3, .88)))
        P.add("Accent", L.cyl(.025, .06, (x, .3, 1.2)) + L.cyl(.04, .02, (x, .3, 1.23)))                   # valves
        P.add("Body", L.box((.06, .1, .05), (x, .2, .5), r=.01))                                            # straps
    # the booms
    for sx in (-1, 1):
        p0 = V((sx * .2, .1, .9))
        pts = [p0, V((sx * .34, .05, 1.0)), V((sx * .44, -.05, 1.18)), V((sx * .46, -.12, 1.36))]
        P.add("BodyBooms", [s.tube([tuple(p) for p in pts], .028, res=1, segments_per=4)])
        P.add("BodyBooms", L.cyl(.04, .06, tuple(p0), axis="X"))
        P.add("Accent", [lathe_between([(.03, 0), (.03, .5), (.05, 1), (0, 1)], pts[-1] + V((0, .02, -.06)), pts[-1] + V((0, -.08, .04)), seg=10)])
        P.add("Accent", [cable([(sx * .17, .3, 1.2), (sx * .26, .2, 1.18), (sx * .36, .02, 1.06)], .016)])
    P.add("BodyBooms", L.rod((0, .12, .88), (0, .12, 1.25), .022))
    P.add("Accent", L.cyl(.035, .08, (0, .12, 1.28), radius2=.05))
    return P


def banshee(L):
    """The wailer: a tall slim station synth over an enamel hover cone with a lit ring at its foot — a coiled spring
    waist, a chest shell with a louvred amplifier vent, long thin arms hanging past the hips with claw fingers, and for
    a head a cluster of paging horns on a neck collar: two forward (their throats glow: the eyes), two to the sides,
    two up and out; loose cables hang from the cluster. Concept: Logs/comfy/danger_s16/banshee_*."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.24, 0), (.26, .015), (.25, .05), (0, .05)]))                         # hover ring
    P.add("Body", L.lathe([(0, .04), (.25, .04), (.26, .08), (.22, .4), (.17, .7), (.13, .9), (0, .9)]))    # the cone
    P.add("Eye", [s.ball(.018, (math.cos(a) * .238, math.sin(a) * .238, .12), seg=8) for a in (-1.57, -.5, .6, 1.57, 2.6, -2.6)])
    for k in range(5):
        P.add("Accent", L.cyl(.1 - abs(k - 2) * .008, .03, (0, 0, .94 + k * .05), seg=12))                          # spring waist
    P.add("Body", L.cyl(.05, .3, (0, 0, 1.04)))
    P.add("Body", L.shell([(1.14, .15, .11, 2.4), (1.3, .2, .13, 2.6), (1.5, .22, .14, 2.6), (1.6, .17, .12, 2.4)], caps=.04))
    P.add("Accent", _grille((0, -.135, 1.38), .16, .14, n=4))
    P.add("Accent", L.cyl(.06, .1, (0, 0, 1.65)))                                                            # neck collar
    P.add("Body", L.ball(.085, (0, -.01, 1.76)))                                                             # horn hub
    horns = [((-1, -.18, .0), .33, .1, True), ((1, -.18, .0), .33, .1, True),
             ((-1, .35, .1), .22, .06, False), ((1, .35, .1), .22, .06, False),
             ((-.5, .2, 1), .22, .055, False), ((.5, .2, 1), .22, .055, False)]
    for (sx, dy, dz), ln, bell, front in horns:
        if front:
            base = V((sx * .045, -.03, 1.8))
            d = V((sx * .32, -1, .0)).normalized()
        else:
            base = V((sx * .06, 0, 1.78 + dz * .04))
            d = V((sx, dy, dz)).normalized()
        P.add("Accent", [lathe_between([(.03, 0), (.032, .3), (bell * .6, .7), (bell, .96), (bell * 1.04, 1), (bell * .85, 1),
                                        (bell * .55, .82), (0, .65)], base, base + d * ln, seg=12)])
        P.add("Eye", [lathe_between([(bell * .5, 0), (bell * .35, .5), (0, 1)], base + d * ln * .64, base + d * ln * .7, seg=12)])
    for k, (x, y) in enumerate(((-.07, .06), (.05, .07), (.08, .02), (-.04, .08))):                         # hanging cables
        P.add("Accent", [s.tube([(x * .6, y * .6, 1.76), (x * 1.3, y * 1.2, 1.66), (x * 1.6, y * 1.4, 1.5 - .04 * k)], .009, res=1, segments_per=2)])
    for sx in (-1, 1):
        sh, el, wr = V((sx * .24, 0, 1.54)), V((sx * .28, .02, 1.1)), V((sx * .27, -.04, .66))
        P.add("Accent", L.ball(.045, tuple(sh)) + L.ball(.035, tuple(el)))
        P.add("Body", L.rod(tuple(sh), tuple(el), .026) + L.rod(tuple(el), tuple(wr), .022))
        P.add("Accent", L.box((.05, .05, .06), tuple(wr), r=.012))
        for dx in (-.018, 0, .018):
            P.add("Accent", [lathe_between([(.008, 0), (.006, .7), (0, 1)], wr + V((dx, -.01, -.02)), wr + V((dx * 1.6, -.05, -.18)), seg=6)])
    return P


def maniac(L):
    """The hunter: a lanky synth bent forward — long thin legs on ring joints with long three-toed feet, a narrow
    pelvis, a spine of collars into a heavy hunched shoulder shell, a long narrow head thrust forward on its neck with
    one small cold lens, long arms past its knees ending in a hook (left) and a three-claw clamp (right), and across its
    back an empty cradle of bent steel tube — a bed of slats between two rails — where a carried player lies.
    Concept: Logs/comfy/danger_s16/maniac_*."""
    P = D.Parts()
    hx, hz, kz = D.MANIAC_HIP_X, D.MANIAC_HIP_Z, D.MANIAC_KNEE
    P.add("Body", L.shell([(.98, .19, .11, 2.6, 0, .02), (1.08, .21, .12, 2.8, 0, .02), (1.16, .16, .1, 2.6, 0, .01)], caps=.03))  # pelvis
    spine = [V((0, .02, 1.16)), V((0, .0, 1.3)), V((0, -.04, 1.42))]
    for k, p in enumerate(spine):
        P.add("Accent", L.cyl(.075 - k * .005, .05, tuple(p)))
    P.add("Body", L.rod((0, .02, 1.1), (0, -.06, 1.5), .045))
    P.add("Body", L.shell([(1.4, .2, .14, 2.6, 0, -.08), (1.52, .3, .18, 2.8, 0, -.09), (1.63, .32, .16, 2.8, 0, -.08),
                           (1.7, .25, .11, 2.4, 0, -.07)], caps=.04))                                        # hunched shoulders
    P.add("Eye", L.box((.1, .03, .02), (0, -.262, 1.55), r=.005))
    # the neck and the long head
    P.add("Accent", L.rod((0, -.12, 1.66), (0, -.22, 1.73), .04))
    P.add("Body", L.shell([(0, .08, .09, 2.4), (.1, .095, .1, 2.6), (.2, .075, .08, 2.4), (.235, .05, .055, 2.0)], axis="-Y",
                          loc=(0, -.13, 1.75), caps=.02))
    acc, glow = L.eye(.024, (0, -.36, 1.735), depth=.04)
    P.add("Accent", acc).add("Eye", glow)
    # the cradle on its back: two rails and slats under and round the clear box (+-0.2, 0.07..0.22, 1.73..1.86)
    (x0, y0, z0), (x1, y1, z1) = D.MANIAC_CLEAR
    for y in (y0 - .025, y1 + .025):
        P.add("Accent", [_bent([(-.3, y, z1 + .02), (-.3, y, z0 - .03), (.3, y, z0 - .03), (.3, y, z1 + .02)], .016, bend=.06)])
    for x in (-.24, -.12, 0, .12, .24):
        P.add("Accent", [s.tube([(x, y0 - .03, z0 - .035), (x, y1 + .03, z0 - .035)], .013, res=2, smooth=False)])
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .16, -.02, 1.62), (sx * .2, y0 - .02, z0 - .04), .022))                 # brackets
    # arms
    for sx in (-1, 1):
        sh, el, wr = V((sx * .34, -.08, 1.6)), V((sx * .38, -.1, 1.18)), V((sx * .34, -.2, .76))
        P.add("Body", L.ball(.08, tuple(sh)))
        P.add("Body", L.rod(tuple(sh), tuple(el), .036) + L.rod(tuple(el), tuple(wr), .03))
        P.add("Accent", L.ball(.045, tuple(el)) + L.box((.07, .07, .08), tuple(wr), r=.018))
        if sx > 0:                                                                                           # left: the hook
            P.add("Accent", L.rod(tuple(wr), tuple(wr + V((0, -.02, -.14))), .014))
            P.add("Accent", [_poly_arc(wr + V((0, -.07, -.18)), .055, -10, 200, .014, "YZ", 8)])
        else:                                                                                                # right: the clamp
            for a in (0, 2.1, 4.2):
                d = V((math.cos(a) * .04, math.sin(a) * .04 - .01, 0))
                P.add("Accent", [lathe_between([(.012, 0), (.009, .6), (0, 1)], wr + d * .5 + V((0, 0, -.03)), wr + d * 1.6 + V((0, -.02, -.2)), seg=6)])
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * hx
        P.add(leg, L.ball(.07, (x, .02, hz)))
        P.add(leg, L.shell([(kz + .02, .045, .05, 2.4, x, .02), (.8, .06, .065, 2.6, x, .02), (hz - .02, .055, .06, 2.4, x, .02)], caps=.02))
        P.add(leg, L.ball(.055, (x, .0, kz)))
        P.add(leg, L.rod((x, .02, kz), (x, .03, .07), .035))
        P.add(leg, L.ball(.04, (x, .03, .07)))
        for dx, dy in ((-.04, -.16), (0, -.19), (.04, -.16), (0, .07)):                                      # three toes and a spur
            P.add(leg, [lathe_between([(.02, 0), (.015, .7), (0, 1)], (x, .03, .06), (x + dx, .03 + dy, .015), seg=6)])
    P.split = {"AccentLeg0": kz, "AccentLeg1": kz}
    return P


DESIGNS = {
    "robot_guard": guard, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_plate": plate, "attach_mic": attach_mic,
    "robot_warden": warden, "robot_repairman": repairman, "robot_smoker": smoker, "robot_relay": relay,
    "robot_nurse": nurse, "robot_mimic": mimic, "robot_salesman": salesman, "robot_gunner": gunner,
    "robot_driller": driller, "robot_exterminator": exterminator, "robot_banshee": banshee, "robot_maniac": maniac,
}


# =========================================================================== furniture and level pieces
# A late-1980s government research complex: enamel and powder-coated steel, bent round tube, thick laminate with soft
# edges, beige CRT terminals, bar pulls, recessed plinths. Built whole; nobody has been here for a while.

def desk(L):
    """A steel-frame office desk: a laminate top on two sled frames, a three-drawer pedestal, a deep CRT on a riser and
    a beige keyboard. (Drawn at the old top 0.765; the build raises it to the game's desk height.)"""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", L.four_legs(.72, .8, .4, .72, .05) + L.slab((1.3, .02, .34), (0, .3, .5)))
    P.add("Body", L.carcass((.42, .62, .7), (.52, -.02, 0), kick=.06))
    for k, z in enumerate((.22, .42, .6)):
        P.add("Body", L.door((.38, .17 if k else .2), (.52, -.335, z)))
        P.add("Accent", L.handle((.52, -.35, z + .02), .16, vertical=False))
    acc, glow = L.screen((.44, .42), (0, -.02, 1.16), "-Y")
    P.add("Eye", glow)
    P.add("Accent", acc + L.slab((.34, .36, .05), (0, .12, .79)) + [s.rcyl(.08, .1, (0, .1, .86), seg=16, r=.01, bevel_seg=1)])
    P.add("Accent", L.slab((.46, .17, .035), (-.05, -.3, .78), rot=(math.radians(4), 0, 0)))
    return P


def terminal(L):
    """The security console: a sloped control deck in front of three CRTs on a shelf, a tower under the top."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", L.four_legs(.72, .8, .4, .72, .05) + L.slab((1.3, .02, .34), (0, .3, .5)))
    P.add("Accent", L.carcass((.24, .5, .45), (-.5, .04, 0), kick=.03))
    P.add("AccentConsole", L.slab((.66, .26, .045), (0, -.07, .79), rot=(math.radians(10), 0, 0)))
    P.add("AccentConsole", L.slab((1.02, .3, .04), (0, .2, .83)) + L.slab((.06, .06, .08), (-.4, .2, .79))
          + L.slab((.06, .06, .08), (.4, .2, .79)))
    for x in (-.33, 0, .33):
        acc, glow = L.screen((.25, .21), (x, .14, .985), "-Y")
        P.add("Eye", glow)
        P.add("AccentConsole", acc)
    return P


def chair(L):
    """A stacking chair on two bent-tube sleds: a moulded seat pan and a curved back shell."""
    P = D.Parts()
    r = .012
    for sx in (-1, 1):
        x = sx * .235
        P.add("Accent", [bent([(x, .22, .44), (x, -.21, .44), (x, -.27, r), (x, .27, r), (x, .22, .44)], r, bend=.07)])
        P.add("Accent", [bent([(x, .22, .43), (x, .25, .7), (x, .27, .98)], r, bend=.12)])
    P.add("Accent", [s.tube([(-.235, -.19, .44), (.235, -.19, .44)], r, res=2, smooth=False)])
    P.add("Body", L.cushion((.5, .46, .06), (0, 0, .475)))
    back = s.loft([(-.17, .24, .03, 3.0), (0, .25, .035, 3.0), (.17, .23, .03, 3.0)], seg=24, round_caps=.02)
    P.add("Body", [s.place(back, (0, .25, .84), rot=(math.radians(-10), 0, 0))])
    return P


def server_rack(L):
    """A mainframe cabinet: a hooded top, two tape reels behind a smoked window, a louvred lower door and a column of
    square status lamps."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, 1.0, 1.92), (0, 0, 0), kick=.07))
    P.add("Body", [s.rbox((.72, 1.02, .1), (0, 0, 1.95), r=.03, seg=3)])
    P.add("Body", L.slab((.64, .03, 1.76), (0, -.5, .98)))
    P.add("Accent", [s.rbox((.42, .03, .62), (-.08, -.52, 1.45), r=.02, seg=2)])
    for z in (1.6, 1.3):
        P.add("Body", [s.rcyl(.12, .03, (-.08, -.535, z), axis="Y", seg=24, r=.006, bevel_seg=1),
                       s.rcyl(.035, .045, (-.08, -.54, z), axis="Y", seg=12, r=.006, bevel_seg=1)])
    P.add("Accent", s.grille((-.08, -.52, .55), .42, .6, n=7, axis="-Y"))
    for i in range(11):
        P.add("Eye", [s.rbox((.06, .02, .05), (.18, -.515, .2 + i * .16), r=.006, seg=1)])
    P.add("Accent", L.handle((.26, -.515, 1.0), .3))
    return P


def vending_machine(L):
    """A 1990s snack machine: a big lit window in a thick rounded frame, a selector column with a key grid and a coin
    slot, a pickup flap, a lit header."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .8, 1.9), (0, 0, 0), kick=.08))
    P.add("Accent", [s.rbox((.68, .05, 1.18), (-.09, -.41, 1.15), r=.03, seg=3)])
    P.add("Eye", [s.rbox((.6, .03, 1.1), (-.09, -.428, 1.15), r=.01, seg=2)])
    P.add("Accent", [s.rbox((.16, .04, .9), (.33, -.41, 1.2), r=.02, seg=2)])
    for k in range(4):
        for j in range(3):
            P.add("Body", [s.rbox((.035, .02, .035), (.29 + j * .04, -.43, 1.42 - k * .05), r=.005, seg=1)])
    P.add("Body", [s.rbox((.05, .02, .1), (.33, -.43, 1.1), r=.006, seg=1)])
    P.add("Accent", [s.rbox((.56, .05, .2), (-.09, -.41, .32), r=.02, seg=2)])
    P.add("Body", [s.rbox((.46, .02, .08), (-.09, -.432, .32), r=.008, seg=1)])
    P.add("Accent", [s.rbox((.86, .05, .14), (0, -.405, 1.81), r=.02, seg=2)])
    return P


def water_cooler(L):
    """A floor cooler: a steel cabinet on feet with a side vent, a recessed dispensing niche with two lever taps over a
    drip tray with its grate, a cup tube at the side, a collar on top and a 19-litre bottle upside down in it — smooth
    shoulders, a few grip rings, the neck."""
    P = D.Parts()
    cab = s.rbox((.34, .34, .9), (0, 0, .49), r=.022, seg=3)
    cab = s.boolean(cab, s.rbox((.25, .12, .3), (0, -.17, .7), r=.02, seg=2), "DIFFERENCE")       # the niche
    s.bevel_edges(cab, .004, 1, angle=50)
    s.groove(cab, "Z", .42, .006, .003)                                                           # the door's top edge
    P.add("Body", [cab, s.rbox((.35, .35, .07), (0, 0, .955), r=.03, seg=3)])
    P.add("Body", [s.rcyl(.02, .05, (x, y, .025), seg=10, r=.004, bevel_seg=1) for x in (-.13, .13) for y in (-.13, .13)])
    P.add("Body", [s.rbox((.24, .1, .022), (0, -.15, .565), r=.006, seg=1)])                      # the tray
    P.add("Body", [s.rbox((.2, .008, .006), (0, -.19 + k * .02, .578), r=.002, seg=1) for k in range(5)])
    for x in (-.06, .06):                                                                          # taps: a spout and a lever
        P.add("Body", [s.rcyl(.014, .06, (x, -.14, .8), axis="Y", seg=10, r=.003, bevel_seg=1), s.rcyl(.011, .04, (x, -.165, .78), seg=10, r=.003, bevel_seg=1),
                       s.rbox((.03, .012, .05), (x, -.172, .815), r=.004, seg=1, rot=(math.radians(-18), 0, 0))])
    P.add("Body", [s.rbox((.006, .2, .012), (.171, 0, .2 + k * .03), r=.002, seg=1) for k in range(6)])          # the vent's louvres
    P.add("Body", [s.rcyl(.042, .4, (-.19, .05, .6), seg=14, r=.006, bevel_seg=1), s.rbox((.03, .05, .04), (-.178, .05, .7), r=.006, seg=1)])  # the cup tube
    prof = [(0, 0), (.028, 0), (.03, .05), (.05, .07), (.115, .12), (.135, .15), (.136, .2), (.13, .206), (.136, .212), (.136, .285), (.13, .291), (.136, .297),
            (.136, .36), (.128, .4), (.09, .425), (.05, .422), (0, .415)]
    P.add("Eye", [s.lathe(prof, seg=28, loc=(0, 0, .985))])
    return P


def coffee_machine(L):
    """A filter brewer: a C-shaped body (base with a warming plate, a back column, a head with a swing-out basket),
    a glass carafe with a black handle, a red power lamp."""
    P = D.Parts()
    P.add("Body", [s.rbox((.34, .38, .06), (0, 0, .03), r=.02, seg=2), s.rbox((.34, .16, .5), (0, .11, .25), r=.03, seg=3),
                   s.rbox((.34, .38, .14), (0, 0, .43), r=.03, seg=3)])
    P.add("Accent", [s.rcyl(.09, .02, (0, -.07, .065), seg=24, r=.004, bevel_seg=1)])
    P.add("Accent", [s.lathe([(0, 0), (.08, 0), (.09, .04), (.09, .12), (.06, .17), (.05, .2), (.065, .21), (0, .21)],
                             seg=20, loc=(0, -.07, .07))])
    P.add("Accent", [bent([(.07, -.07, .25), (.12, -.07, .24), (.12, -.07, .12), (.07, -.07, .11)], .012, bend=.02)])
    P.add("Accent", [s.lathe([(0, 0), (.09, 0), (.1, .06), (0, .06)], seg=20, loc=(0, -.08, .33))])
    P.add("Eye", [s.rbox((.1, .02, .04), (.08, -.195, .43), r=.008, seg=1)])
    return P


def safe(L):
    """An institutional floor safe: a heavy rounded body on four cast feet, a door with a reveal, two barrel hinges,
    a combination dial in a ring and a T-lever handle."""
    P = D.Parts()
    P.add("Body", [s.rbox((.8, .7, .92), (0, 0, .54), r=.05, seg=3)])
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", [s.rbox((.12, .12, .1), (sx * .3, sy * .25, .05), r=.02, seg=2)])
    P.add("Body", [s.rbox((.64, .04, .74), (0, -.36, .55), r=.025, seg=2)])
    for z in (.32, .78):
        P.add("Accent", [s.rcyl(.025, .16, (-.345, -.37, z), seg=12, r=.006, bevel_seg=1)])
    P.add("Accent", [s.rcyl(.1, .03, (.06, -.385, .64), axis="Y", seg=24, r=.006, bevel_seg=1),
                     s.rcyl(.07, .06, (.06, -.395, .64), axis="Y", seg=24, r=.008, bevel_seg=1)])
    P.add("Accent", [s.rcyl(.035, .05, (.06, -.39, .38), axis="Y", seg=12, r=.006, bevel_seg=1),
                     s.rbox((.26, .04, .04), (.06, -.405, .38), r=.015, seg=2)])
    P.add("Eye", [s.rbox((.06, .02, .1), (-.22, -.385, .65), r=.008, seg=1)])
    return P


def plant(L):
    """A corn plant in a drum planter: three bare canes of different heights, each crowned with a rosette of long
    arching strap leaves."""
    P = D.Parts()
    P.add("Accent", L.pot(.215, .45, (0, 0, 0)))
    P.add("Body", [s.rcyl(.19, .03, (0, 0, .425), seg=20, r=.005, bevel_seg=1)])   # soil
    canes = (((-.05, .03), .74, 0.3), ((.06, -.02), .98, 1.7), ((-.01, -.04), 1.22, 3.1))
    for (cx, cy), h, seed in canes:
        P.add("Body", [s.rcyl(.028, h - .4, (cx, cy, .4 + (h - .4) / 2), seg=10, r=.006, bevel_seg=1)])
        c = V((cx, cy, h))
        for k in range(8):
            a = seed + k * math.tau / 8 + (k % 2) * .2
            up = .55 - .5 * (k % 3) / 2
            n = V((math.cos(a), math.sin(a), up)).normalized()
            ln = .29 + .04 * ((k * 5) % 3) / 2
            mid = c + n * ln * .55
            tip = c + V((math.cos(a) * ln, math.sin(a) * ln, n.z * ln * .9 - .05))
            P.add("Body", [s.tube([tuple(c), tuple(mid), tuple(tip)], .02, res=1, radii=[.6, 1.0, .25], segments_per=3)])
    return P


def desk_lamp(L):
    """A task lamp: a heavy round foot, a bent-tube arm with a spring barrel, an enamel dome shade over the bulb."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.08, 0), (.08, .015), (.06, .035), (.03, .045), (0, .045)], seg=24)])
    P.add("Body", [bent([(0, 0, .04), (0, .03, .2), (0, -.13, .33)], .009, bend=.03)])
    P.add("Body", [s.rcyl(.014, .1, (0, .02, .14), seg=10, r=.004, bevel_seg=1, rot=(math.radians(-10), 0, 0))])
    shade = L.shade(.075, .03, .11, (0, 0, 0))
    for b in shade:
        bmesh.ops.rotate(b, cent=V((0, 0, 0)), matrix=Euler((math.radians(-20), 0, 0)).to_matrix(), verts=b.verts)
        bmesh.ops.translate(b, vec=V((0, -.165, .235)), verts=b.verts)
    P.add("Accent", shade)
    P.add("Eye", [s.ball(.03, (0, -.16, .27), seg=10)])
    return P


def floor_lamp(L):
    """A standard lamp: a weighted stepped foot, a steel tube with a switch collar, a big enamel dome over a diffuser."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.16, 0), (.16, .02), (.11, .045), (.05, .055), (0, .055)], seg=24)])
    P.add("Body", [s.rcyl(.016, 1.43, (0, 0, .76), seg=12, r=.004, bevel_seg=1)])
    P.add("Body", [s.rcyl(.028, .07, (0, 0, .95), seg=12, r=.008, bevel_seg=1)])
    P.add("Body", [s.rcyl(.04, .06, (0, 0, 1.45), seg=16, r=.01, bevel_seg=1)])
    P.add("Accent", L.shade(.2, .1, .3, (0, 0, 1.35)))
    P.add("Eye", [s.rcyl(.14, .02, (0, 0, 1.362), seg=24, r=.004, bevel_seg=1)])
    return P


def printer(L):
    """A floor photocopier: two paper cassettes with bar pulls, a glass bed under a lid, a sloped control strip with a
    display at the front, an output tray in a recess."""
    P = D.Parts()
    P.add("Body", L.carcass((.6, .58, .98), (0, .01, 0), kick=.07))
    for z in (.25, .5):
        P.add("Body", L.door((.54, .2), (0, -.285, z)))
        P.add("Accent", L.handle((0, -.3, z + .03), .2, vertical=False))
    P.add("Body", [s.rbox((.58, .54, .07), (0, .02, 1.035), r=.02, seg=2)])
    P.add("Body", [s.rbox((.6, .14, .06), (0, -.3, .97), r=.02, seg=2, rot=(math.radians(14), 0, 0))])
    P.add("Eye", [s.rbox((.12, .02, .05), (.18, -.355, .99), r=.006, seg=1, rot=(math.radians(14), 0, 0))])
    P.add("Accent", [s.rbox((.2, .025, .04), (-.12, -.36, .985), r=.008, seg=1, rot=(math.radians(14), 0, 0))])
    P.add("Accent", [s.rbox((.44, .05, .05), (0, -.285, .8), r=.012, seg=2)])
    return P


def reception_desk(L):
    """A guard desk: a high enamel front with a recessed plinth and a round bumper rail, a thick laminate counter, a
    work top behind with a deep CRT."""
    P = D.Parts()
    front = s.rbox((2.4, .08, 1.0), (0, -.36, .6), r=.03, seg=3)
    for x in (-.4, .4):
        s.groove(front, "X", x, .014, .008)
    P.add("Body", [front])
    P.add("Body", [s.rbox((2.3, .06, .12), (0, -.33, .07), r=.01, seg=1)])
    P.add("Body", L.slab((.08, .76, 1.1), (-1.16, 0, .55)) + L.slab((.08, .76, 1.1), (1.16, 0, .55)))
    P.add("Accent", [s.tube([(-1.12, -.41, .55), (1.12, -.41, .55)], .02, res=2, smooth=False)])
    for x in (-1.0, 0, 1.0):
        P.add("Accent", [s.rcyl(.012, .05, (x, -.39, .55), axis="Y", seg=8, r=.003, bevel_seg=1)])
    P.add("Accent", L.slab((2.5, .3, .05), (0, -.33, 1.12)) + L.slab((2.3, .7, .04), (0, .02, .74)))
    # the CRT faces whoever sits behind the desk (+Y), its hood towards the high front; only its glass is the screen (Eye:
    # the game lays the screen's picture on every flat face of that part — the housing wore it on all sides)
    acc, glow = L.screen((.4, .3), (.4, .1, .96), "+Y")
    P.add("Accent", acc + [s.rcyl(.07, .05, (.4, -.05, .78), seg=16, r=.008, bevel_seg=1)])
    P.add("Eye", glow)
    return P


def trash_bin(L):
    """An enamelled office bin: a tapered pail on a recessed foot ring, a thick rolled rim (Accent)."""
    P = D.Parts()
    prof = [(0, 0), (.13, 0), (.135, .02), (.145, .03), (.185, .42), (.17, .42), (.13, .04), (0, .04)]
    P.add("Body", [s.lathe(prof, seg=24)])
    P.add("Accent", [s.lathe([(.168, 0), (.19, 0), (.198, .015), (.19, .03), (.168, .03)], seg=24, loc=(0, 0, .415))])
    return P


def _tube_arm(x, y0, y1, top, r=.02):
    """A bent-tube armrest frame: a loop from the floor up to the arm and back down."""
    return bent([(x, y0, r), (x, y0, top), (x, y1, top), (x, y1, r), (x, y0 + .06, r)], r, bend=.08)


def sofa(L):
    """A waiting-room sofa: square foam blocks on a recessed plinth between two bent-tube arm frames."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.96, .72, .14), (0, -.02, .19), r=.02, seg=2), s.rbox((1.8, .6, .14), (0, 0, .07), r=.01, seg=1)])
    P.add("Body", L.cushion((.98, .7, .16), (-.49, -.05, .33)) + L.cushion((.98, .7, .16), (.49, -.05, .33)))
    P.add("Body", L.cushion((.98, .18, .5), (-.49, .32, .62), rot=(math.radians(-6), 0, 0))
          + L.cushion((.98, .18, .5), (.49, .32, .62), rot=(math.radians(-6), 0, 0)))
    for x in (-1.0, 1.0):
        P.add("Accent", [_tube_arm(x * 1.08, -.4, .4, .62)])
        P.add("Accent", [s.tube([(x * 1.08, 0, .2), (x * .97, 0, .2)], .018, res=2, smooth=False)])
    return P


def armchair(L):
    """The matching lounge chair: one seat block and one back block between two bent-tube arm frames."""
    P = D.Parts()
    P.add("Body", [s.rbox((.68, .7, .14), (0, -.02, .19), r=.02, seg=2), s.rbox((.56, .56, .14), (0, 0, .07), r=.01, seg=1)])
    P.add("Body", L.cushion((.68, .68, .16), (0, -.05, .33)))
    P.add("Body", L.cushion((.68, .18, .5), (0, .3, .62), rot=(math.radians(-8), 0, 0)))
    for x in (-1, 1):
        P.add("Accent", [_tube_arm(x * .42, -.35, .35, .6)])
        P.add("Accent", [s.tube([(x * .42, 0, .2), (x * .33, 0, .2)], .018, res=2, smooth=False)])
    return P


def meeting_table(L):
    """A conference table: a boat-shaped laminate top with a thick radiused edge, a cable trough with two flip lids down
    its middle, on two steel T-legs (a column, a long foot with levellers, a plate under the top) tied by a cable tray."""
    P = D.Parts()
    top = s.loft([(-1.6, .028, .5, 6), (-1.3, .03, .61, 6), (0, .03, .65, 6), (1.3, .03, .61, 6), (1.6, .028, .5, 6)], seg=28, axis="X", loc=(0, 0, .75), round_caps=.03)
    P.add("Body", [top])
    for x in (-.45, .45):                                                                          # the cable lids, a finger notch each
        P.add("Accent", [s.rbox((.42, .13, .012), (x, 0, .7785), r=.004, seg=1), s.rbox((.08, .03, .014), (x, -.05, .779), r=.004, seg=1)])
    for x in (-1.1, 1.1):
        P.add("Accent", [s.rbox((.09, .16, .68), (x, 0, .38), r=.012, seg=2), s.rbox((.1, 1.0, .04), (x, 0, .02), r=.012, seg=2),
                         s.rbox((.2, .5, .012), (x, 0, .716), r=.004, seg=1)])
        P.add("Accent", [s.rcyl(.022, .012, (x, y, .004), seg=10, r=.003, bevel_seg=1) for y in (-.45, .45)])
    P.add("Accent", [s.rbox((2.12, .14, .05), (0, 0, .62), r=.01, seg=1)])                         # the tray under the top
    return P


def round_table(L):
    """A cafe table: a thick soft-edged top on a tube column and a four-star foot of bent tube."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.5, .04, (0, 0, .74), seg=32, r=.018, bevel_seg=3)])
    P.add("Accent", [s.rcyl(.03, .66, (0, 0, .39), seg=12, r=.005, bevel_seg=1),
                     s.rcyl(.11, .02, (0, 0, .715), seg=16, r=.005, bevel_seg=1)])
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        c, d = math.cos(a), math.sin(a)
        P.add("Accent", [bent([(0, 0, .1), (c * .05, d * .05, .03), (c * .27, d * .27, .022)], .022, bend=.04),
                         s.rcyl(.03, .016, (c * .27, d * .27, .008), seg=10, r=.004, bevel_seg=1)])
    return P


def shelf(L):
    """Steel stock shelving: angle uprights on foot plates, enamelled shelves with turned-down lips, archive boxes."""
    P = D.Parts()
    for x in (-.97, .97):
        for y in (-.27, .27):
            P.add("Body", [s.rbox((.05, .012, 2.0), (x, y - math.copysign(.019, y), 1.0), r=.003, seg=1),
                           s.rbox((.012, .05, 2.0), (x - math.copysign(.019, x), y, 1.0), r=.003, seg=1),
                           s.rbox((.06, .06, .02), (x - math.copysign(.005, x), y - math.copysign(.005, y), .01), r=.004, seg=1)])
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", [s.rbox((1.98, .58, .02), (0, 0, h + .01), r=.004, seg=1),
                       s.rbox((1.98, .02, .05), (0, -.29, h - .005), r=.005, seg=1),
                       s.rbox((1.98, .02, .05), (0, .29, h - .005), r=.005, seg=1)])
    for (x, y, w, d, hh, z) in ((-.6, 0, .45, .4, .3, .45), (.5, .05, .35, .35, .25, .45), (.3, 0, .5, .45, .35, 1.05),
                                (-.55, 0, .4, .4, .3, 1.65)):
        P.add("Accent", [s.rbox((w, d, hh), (x, y, z + .02 + hh / 2 - .005), r=.01, seg=2)])
    return P


def bean_bag(L):
    """A lounge ottoman: a firm round upholstered drum on a recessed plinth, a buttoned seat cushion on top."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.38, .08, (0, 0, .04), seg=32, r=.01, bevel_seg=1),
                   s.rcyl(.45, .5, (0, 0, .33), seg=32, r=.05, bevel_seg=3),
                   s.rcyl(.42, .12, (0, 0, .62), seg=32, r=.05, bevel_seg=3)])
    for k in range(6):
        a = k * math.tau / 6
        P.add("Body", [s.ball(.025, (math.cos(a) * .22, math.sin(a) * .22, .677), seg=8, scale=(1, 1, .5))])
    P.add("Body", [s.ball(.025, (0, 0, .677), seg=8, scale=(1, 1, .5))])
    return P


def bookshelf(L):
    """A powder-coated steel bookcase: closed side panels, a plinth and a top cap, shelves with folded front edges,
    rows of box files."""
    P = D.Parts()
    for x in (-.48, .48):
        P.add("Body", [s.rbox((.04, .35, 2.0), (x, 0, 1.0), r=.012, seg=2)])
    P.add("Body", [s.rbox((1.0, .36, .04), (0, 0, 1.98), r=.015, seg=2), s.rbox((.94, .3, .08), (0, .02, .04), r=.008, seg=1)])
    P.add("Body", [s.rbox((.94, .02, 1.94), (0, .165, 1.0), r=.004, seg=1)])
    for h in (.5, 1.0, 1.5):
        P.add("Body", [s.rbox((.94, .33, .02), (0, 0, h), r=.004, seg=1), s.rbox((.94, .02, .045), (0, -.16, h - .01), r=.006, seg=1)])
    for i, h in enumerate((.08, .51, 1.01, 1.51)):
        x = -.43
        for k in range(6 + i % 2):
            w = .07 + .03 * ((k + i) % 3)
            hh = .32 - .04 * ((k * 7 + i) % 3)
            P.add("Accent", [s.rbox((w, .26, hh), (x + w / 2, .02, h + hh / 2), r=.008, seg=1)])
            x += w + .012
    return P


def ceiling_lamp(L):
    """A recessed fluorescent troffer: a steel housing, a white frame and a parabolic louvre grid over the tubes."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.24, .64, .06), (0, 0, -.03), r=.01, seg=1)])
    P.add("Accent", [s.rbox((1.25, .05, .03), (0, sy * .3, -.065), r=.008, seg=1) for sy in (-1, 1)]
          + [s.rbox((.05, .6, .03), (sx * .6, 0, -.065), r=.008, seg=1) for sx in (-1, 1)])
    P.add("Accent", [s.rbox((1.18, .02, .025), (0, y, -.065), r=.006, seg=1) for y in (-.095, .095)]
          + [s.rbox((.015, .58, .025), (x, 0, -.065), r=.005, seg=1) for x in (-.39, -.2, 0, .2, .39)])
    P.add("Eye", [s.rbox((1.16, .56, .02), (0, 0, -.06), r=.004, seg=1)])
    return P


DESIGNS.update({
    "desk": desk, "terminal": terminal, "chair": chair, "server_rack": server_rack, "vending_machine": vending_machine,
    "water_cooler": water_cooler, "coffee_machine": coffee_machine, "safe": safe, "plant": plant, "desk_lamp": desk_lamp,
    "floor_lamp": floor_lamp, "printer": printer, "reception_desk": reception_desk, "trash_bin": trash_bin,
    "ceiling_lamp": ceiling_lamp, "sofa": sofa, "armchair": armchair, "meeting_table": meeting_table,
    "round_table": round_table, "shelf": shelf, "bean_bag": bean_bag, "bookshelf": bookshelf,
})
