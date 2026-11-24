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
        for k in range(10):                                                                                # cable chain
            P.add("Accent", [s.rbox((.1, .035, .055), (0, .075, .38 + k * .068), r=.005, seg=1)])

    def beacon(L, P):
        P.add("Accent", L.cyl(.07, .035, (0, .25, 1.335)))
        P.add("Eye", L.cyl(.05, .07, (0, .25, 1.38), radius2=.04))

    return D.worker(L, deck=deck, pod=pod, mast=mast, beacon=beacon)


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


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_plate": plate, "attach_mic": attach_mic,
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
