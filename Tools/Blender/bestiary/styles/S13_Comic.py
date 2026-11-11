"""S13 Comic — «Комикс-пустошь»: welded wasteland bandit bots (Clean family with ink outlines).

Language: lopsided boilers (lofts whose sections drift sideways and forward), knobbly tyres worn as armour, bumpers,
collars and wheels, cone spikes, gas-mask faces (two round goggles + a filter canister snout), mismatched chamfered
scrap plates, square rebar struts. Exaggerated chunky proportions: huge forearms and chests, small sunk heads.
Big planes with single chamfers so the ink outline reads.
"""
import math

import bmesh
from mathutils import Vector

import shapes as s
from lang import Lang, align
import designs as D

TAU = math.tau


# --------------------------------------------------------------------------- helpers of this style

def tube(points, radius, res=1, radii=None, smooth=True):
    """shapes.tube with fewer rings per span (the triangle budget)."""
    return s.tube(points, radius, res=res, radii=radii, smooth=smooth, segments_per=5)


def orient(bm, origin, direction):
    """Turn a shape built along +Z (base at z = 0) so +Z points along direction, then move its base to origin."""
    d = Vector(direction).normalized()
    q = Vector((0, 0, 1)).rotation_difference(d)
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(origin), verts=bm.verts)
    return bm


def tyre(R, rw, rh, loc=(0, 0, 0), direction=(0, 0, 1), m=20, n=6, knobs=.012):
    m = max(24, m + 6)
    """A knobbly tyre (torus around +Z turned to `direction`): R ring radius, rw radial and rh axial half-size of the
    rubber; every other tread block stands out by `knobs`."""
    bm = bmesh.new()
    rings = []
    for i in range(m):
        a = i / m * TAU
        ca, sa = math.cos(a), math.sin(a)
        ring = []
        for j in range(n):
            b = (j + .5) / n * TAU
            cb, sb = math.cos(b), math.sin(b)
            cbs = math.copysign(abs(cb) ** .9, cb)
            sbs = math.copysign(abs(sb) ** .9, sb)
            rr = R + rw * cbs + (knobs if (i % 2 == 0 and cb > .5) else 0)
            ring.append(bm.verts.new((ca * rr, sa * rr, rh * sbs)))
        rings.append(ring)
    for i in range(m):
        for j in range(n):
            bm.faces.new((rings[i][j], rings[(i + 1) % m][j], rings[(i + 1) % m][(j + 1) % n], rings[i][(j + 1) % n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    d = Vector(direction).normalized()
    q = Vector((0, 0, 1)).rotation_difference(d)
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return [bm]


def torus_ring(R, r, loc, direction=(0, 0, 1), m=20, n=6):
    """A plain ring (a steering wheel rim)."""
    return tyre(R, r, r, loc, direction, m=m, n=n, knobs=0)


def spike(base, direction, length, r, seg=6):
    """A welded cone spike standing on `base`."""
    return [orient(s.rcyl(r, length, (0, 0, length / 2), seg=seg, r=0, radius2=r * .06), base, direction)]


def goggle(r, base, direction=(0, -1, 0), depth=.05, seg=12):
    """(accent, glow): a gas-mask goggle — a fat rim ring and a flat glass disc."""
    rim = s.lathe([(r * .8, 0), (r * 1.3, 0), (r * 1.34, depth * .6), (r * 1.18, depth), (r * .86, depth)], seg=seg)
    glass = s.rcyl(r * .92, depth * .5, (0, 0, depth * .8), seg=seg, r=.004, bevel_seg=1)
    return [orient(rim, base, direction)], [orient(glass, base, direction)]


def canister(r, length, base, direction, seg=12):
    """(accent, glow): the filter canister of a gas mask with its glowing intake grille at the end."""
    body = s.lathe([(r * .7, 0), (r, length * .15), (r, length * .8), (r * 1.08, length * .85), (r * 1.08, length),
                    (r * .5, length + .005), (0, length + .005)], seg=seg)
    ribs = s.rcyl(r * 1.06, .02, (0, 0, length * .45), seg=seg, r=0)
    glow = s.rcyl(r * .75, .012, (0, 0, length + .004), seg=seg, r=0)
    return [orient(body, base, direction), orient(ribs, base, direction)], [orient(glow, base, direction)]


def plate(size, loc, rot=(0, 0, 0)):
    """A chamfered scrap plate."""
    return [s.rbox(size, loc, r=min(.02, min(size) * .3), seg=1, rot=rot)]


def rivets(points, r=.022):
    return [s.ball(r, p, seg=8, scale=(1, 1, 1)) for p in points]


def dish(r, base, direction, seg=16):
    """A satellite dish shell opening along direction."""
    prof = [(0, 0), (r * .3, .004), (r * .65, r * .1), (r, r * .28), (r * 1.04, r * .34), (r * .95, r * .32),
            (r * .6, r * .14), (r * .25, r * .05), (0, r * .045)]
    return [orient(s.lathe(prof, seg=seg), base, direction)]


class Style(Lang):
    name = "S13_Comic"
    seg = 12
    soft = 0.0
    edge = 0.02
    hull_kind = "plated"
    limb_kind = "square"
    eye_kind = "porthole"
    wheel_kind = "facet"
    limb_scale = 1.25

    def rseg(self):
        return 1

    # ------------------------------------------------------------------ furniture: welded scrap, tyres, rivets
    leg_kind = "square"
    edge_kind = "chamfer"
    cushion_puff = 0.2
    handle_kind = "bar"
    foliage_kind = "spiky"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """A chamfered scrap plate, 5 mm fuller on every side than drawn (pieces resting on it keep touching); big
        horizontal plates get rivets at the corners."""
        w, d, h = size
        x, y, z = loc
        if not any(rot) and -.02 < z - h / 2 < .002:
            z = (h + .01) / 2
        out = [s.rbox((w + .01, d + .01, h + .01), (x, y, z), r=min(.012, min(size) * .3), seg=1, rot=rot)]
        if not any(rot) and w > .5 and d > .3 and h < .1:
            for sx in (-1, 1):
                for sy in (-1, 1):
                    out.append(s.ball(.016, (x + sx * (w / 2 - .04), y + sy * (d / 2 - .04), z + h / 2 + .002), seg=6))
        return out

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A welded square tube on a foot plate."""
        x, y, z = top
        L = min(length, z)
        t = max(thick, .045) * 1.15
        foot = (x + splay[0], y + splay[1], z - L)
        out = [align(s.rbox((t, t, L), r=.006, seg=1), (foot[0], foot[1], foot[2] + .012), top)]
        out.append(s.rbox((t * 2, t * 2, .016), (foot[0], foot[1], foot[2] + .008), r=.004, seg=1))
        return out

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Square tube legs welded to a rebar frame under the top."""
        out = super().four_legs(top_z, half_w, half_d, length, thick, inset)
        ax, ay = half_w - inset, half_d - inset
        z = top_z - .035
        for sy in (-1, 1):
            out.append(s.rbox((2 * ax, .03, .045), (0, sy * ay, z), r=.006, seg=1))
        for sx in (-1, 1):
            out.append(s.rbox((.03, 2 * ay, .045), (sx * ax, 0, z), r=.006, seg=1))
        return out

    def carcass(self, size, loc, kick=0.06):
        """A welded cabinet: angle irons on the four upright corners and a weld band around the middle."""
        out = super().carcass(size, loc, kick)
        w, d, h = size
        x, y, z = loc
        for sx in (-1, 1):
            for sy in (-1, 1):
                out.append(s.rbox((.05, .05, h - kick), (x + sx * (w / 2 - .015), y + sy * (d / 2 - .015), z + kick + (h - kick) / 2),
                                  r=.006, seg=1))
        out.append(s.rbox((w + .02, d + .02, .03), (x, y, z + kick + (h - kick) * .5), r=.006, seg=1))
        return out

    def door(self, size, loc, face="-Y"):
        """A scrap plate tacked on a little askew, with rivets."""
        w, h = size
        x, y, z = loc
        tilt = (((x * 37 + z * 11) * 100) % 7 - 3) * .006
        out = [s.rbox((w - .02, .024, h - .02), (x, y, z), r=.006, seg=1, rot=(0, tilt, 0))]
        for sx in (-1, 1):
            for sz in (-1, 1):
                out.append(s.ball(.014, (x + sx * (w / 2 - .04), y - .012, z + sz * (h / 2 - .04)), seg=6))
        return out

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """A canvas pad with a strap round it."""
        w, d, h = size
        out = [s.place(s.rbox((w, d, h), r=min(.03, min(size) * .3), seg=1), loc, rot)]
        if not any(rot) and w > .3:
            out.append(s.rbox((min(.06, w * .2), d + .016, h + .016), (loc[0] + w * .25, loc[1], loc[2]), r=.004, seg=1))
        return out

    def screen(self, size, loc, face="-Y"):
        """(accent, glow): a chamfered bezel, the picture behind two welded guard bars."""
        w, h = size
        x, y, z = loc
        sign = -1 if face == "-Y" else 1
        acc = [s.rbox((w, .05, h), (x, y, z), r=.01, seg=1)]
        for k in (-1, 1):
            acc.append(s.rbox((w + .01, .015, .02), (x, y + sign * .035, z + k * h * .2), r=.004, seg=1))
        glow = [s.rbox((w - .05, .012, h - .05), (x, y + sign * .026, z), r=.004, seg=1)]
        return acc, glow

    def shade(self, r_bottom, r_top, height, loc):
        return [s.lathe([(r_bottom, 0), (r_bottom, .02), (r_top, height), (0, height)], seg=8, loc=loc)]

    def pot(self, r, h, loc):
        """A stack of tyres with the soil inside."""
        x, y, z = loc
        n = max(1, round(h / (r * .7)))
        out = []
        for i in range(n):
            out += tyre(r * .72, r * .26, h / n / 2 * 1.05, (x, y, z + h / n * (i + .5)), (0, 0, 1), m=16, knobs=.008)
        out.append(s.rcyl(r * .6, h * .96, (x, y, z + h * .5), seg=10, r=0))
        return out

    def signature(self, model, P):
        return P


# =========================================================================== walkers

def guard(L):
    """Boiler brute: a huge lopsided boiler chest leaning forward, a tyre worn on the left shoulder and a spiked plate on
    the right, popeye forearms with a tyre bracer and spiked knuckles, a small sunk helmet with a gas-mask face."""
    P = D.Parts()
    P.add("Body", L.loft([(.82, .2, .15, 3.0), (.95, .23, .16, 3.0), (1.04, .21, .15, 3.0)]))
    P.add("Body", L.loft([(.98, .2, .16, 2.4, 0, 0), (1.15, .3, .24, 2.4, .02, -.02), (1.38, .42, .3, 2.4, .04, -.06),
                          (1.58, .4, .28, 2.4, .02, -.08), (1.7, .26, .2, 2.4, 0, -.06)], round_caps=.05))
    P.add("Accent", L.loft([(1.2, .35, .275, 2.4, .026, -.032), (1.26, .36, .28, 2.4, .028, -.038)]))
    P.add("Accent", L.cyl(.13, .05, (-.12, -.35, 1.38), axis="Y") + rivets([(-.12 + .1 * math.cos(a), -.38, 1.38 + .1 * math.sin(a))
                                                                          for a in (0, 2.1, 4.2)]))
    P.add("Accent", plate((.22, .04, .16), (.16, -.33, 1.12), rot=(0, .25, -.3)))
    # shoulders: tyre left, spiked plate right
    P.add("Accent", tyre(.15, .07, .08, (-.44, -.05, 1.6), (-.6, 0, 1)))
    P.add("Accent", plate((.3, .36, .07), (.45, -.04, 1.66), rot=(0, math.radians(28), 0)))
    for k in range(3):
        x = .4 + k * .06
        P.add("Accent", spike((x, -.14 + k * .1, 1.685 - (x - .45) * .53), (.47, 0, .88), .16, .035))
    for sx in (-1, 1):
        P.add("Body", L.ball(.1, (sx * .45, -.03, 1.52)))
        P.add("Body", L.rod((sx * .45, -.03, 1.52), (sx * .5, -.07, 1.2), .065))
        P.add("Body", L.loft([(.88, .11, .11, 3.0, sx * .52, -.08), (1.04, .16, .15, 3.0, sx * .53, -.08),
                              (1.2, .1, .1, 3.0, sx * .51, -.07)]))
        P.add("Accent", [s.rbox((.2, .22, .2), (sx * .53, -.1, .78), r=.03, seg=1)])
    P.add("Accent", tyre(.15, .05, .07, (-.53, -.08, 1.03), (0, 0, 1), m=18))
    for k in (-1, 0, 1):
        P.add("Accent", spike((.53 + k * .06, -.2, .82), (0, -1, 0), .1, .025))
    # sunk helmet with a gas mask
    P.add("Body", L.loft([(1.66, .17, .16, 2.4), (1.8, .2, .19, 2.4), (1.95, .18, .17, 2.4), (2.0, .1, .1, 2.4)], round_caps=.03))
    P.add("Accent", L.loft([(1.72, .2, .2, 2.4), (1.75, .21, .2, 2.4)]))
    P.add("Accent", spike((-.08, .02, 1.98), (-.3, 0, 1), .14, .035))
    P.add("Accent", L.loft([(0, .16, .1, 2.4), (.06, .15, .09, 2.4)], axis="-Y", loc=(0, -.13, 1.83)))
    for sx in (-1, 1):
        P.eye(goggle(.05, (sx * .075, -.18, 1.87), (sx * .25, -1, .05), .045))
    P.eye(canister(.055, .1, (0, -.16, 1.76), (0, -1, -.5)))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.11, (x, 0, D.GUARD_HIP_Z)))
        P.add(leg, L.loft([(.46, .1, .1, 3.0, x, 0), (.68, .13, .13, 3.0, x, -.01), (.9, .12, .12, 3.0, x, 0)]))
        P.add(leg, plate((.17, .08, .17), (x, -.1, .47), rot=(-.2, 0, 0)))
        P.add(leg, L.loft([(.12, .09, .09, 3.0, x, 0), (.3, .11, .12, 3.0, x, .01), (.46, .09, .09, 3.0, x, 0)]))
        P.add(leg, [s.rbox((.22, .4, .15), (x, -.08, .075), r=.03, seg=1, taper=(.85, .75))])
        P.add(leg, plate((.2, .08, .1), (x, -.26, .09), rot=(.5, 0, 0)))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """Chop-bike raider: a hunched torso welded onto a motorbike — a huge knobbly front tyre in a fork, a small rear
    one, twin exhausts up its back; a buzz-saw arm and a hook arm; a gas-mask helmet with a spiked mohawk."""
    P = D.Parts()
    P.add("Accent", tyre(.3, .1, .12, (0, -.28, .4), (1, 0, 0), m=22))
    P.add("Body", L.rod((-.36, -.2, 1.06), (.36, -.2, 1.06), .03) + L.rod((0, -.12, .98), (0, -.2, 1.06), .04))
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.04, .12, (sx * .4, -.2, 1.06), axis="X") + spike((sx * .46, -.2, 1.06), (sx, 0, .2), .1, .025))
    P.add("Accent", L.cyl(.08, .08, (0, -.26, 1.06), axis="Y"))
    P.add("Eye", L.cyl(.065, .02, (0, -.305, 1.06), axis="Y"))
    P.add("Body", L.cyl(.2, .14, (0, -.28, .4), axis="X", seg=10))
    P.add("Accent", tyre(.2, .08, .07, (0, .42, .28), (1, 0, 0), m=18))
    P.add("Body", L.cyl(.13, .12, (0, .42, .28), axis="X"))
    for sx in (-1, 1):
        P.add("Body", L.rod((sx * .11, -.28, .4), (sx * .1, -.1, .95), .03))
        P.add("Body", L.rod((sx * .08, .42, .28), (sx * .1, .12, .62), .028))
        P.add("Accent", [tube([(sx * .2, .2, .62), (sx * .26, .36, 1.0), (sx * .24, .4, 1.55)], .045, res=1)])
        P.add("Accent", L.cyl(.06, .12, (sx * .24, .4, 1.58), radius2=.075))
    P.add("Body", L.loft([(-.25, .17, .17, 2.4), (.05, .22, .2, 2.4, .03, 0), (.3, .18, .17, 2.4)], axis="-Y", loc=(0, .1, .66)))
    P.add("Body", L.loft([(.75, .16, .14, 3.0), (1.05, .14, .12, 3.0, 0, .02), (1.3, .2, .15, 3.0, 0, .02)]))
    P.add("Body", L.loft([(1.25, .2, .15, 2.4, 0, .02), (1.5, .34, .24, 2.4, .02, -.04), (1.78, .4, .27, 2.4, .03, -.08),
                          (1.94, .28, .2, 2.4, 0, -.06)], round_caps=.05))
    P.add("Accent", plate((.3, .05, .22), (.08, -.33, 1.62), rot=(-.25, 0, .2)))
    P.add("Accent", tyre(.15, .07, .08, (-.44, -.06, 1.86), (-.6, 0, 1)))
    P.add("Accent", plate((.3, .34, .07), (.46, -.06, 1.9), rot=(0, math.radians(30), 0)))
    for k in range(3):
        x = .42 + k * .05
        P.add("Accent", spike((x, -.16 + k * .1, 1.925 - (x - .46) * .577), (.5, 0, .87), .18, .035))
    # saw arm (right) and hook arm (left)
    P.add("Body", L.ball(.1, (.44, -.06, 1.76)) + L.rod((.44, -.06, 1.76), (.56, -.2, 1.42), .06)
          + L.loft([(0, .1, .1, 3.0), (.3, .13, .13, 3.0)], axis="-Y", loc=(.55, -.22, 1.38)))
    saw = [s.rcyl(.3, .025, (.55, -.72, 1.3), axis="X", seg=16, r=0)]
    for k in range(12):
        a = k / 12 * TAU
        saw += spike((.55, -.72 + math.cos(a) * .28, 1.3 + math.sin(a) * .28), (0, math.cos(a + .5), math.sin(a + .5)),
                     .08, .03, seg=4)
    P.add("Accent", saw + L.cyl(.08, .08, (.55, -.72, 1.3), axis="X") + L.rod((.55, -.5, 1.34), (.55, -.68, 1.31), .04))
    P.add("Body", L.ball(.1, (-.44, -.06, 1.76)) + L.rod((-.44, -.06, 1.76), (-.52, -.25, 1.4), .06)
          + L.rod((-.52, -.25, 1.4), (-.46, -.55, 1.22), .055))
    P.add("Accent", [tube([(-.46, -.55, 1.22), (-.46, -.72, 1.12), (-.46, -.74, .96), (-.46, -.62, .92)], .035, res=1)])
    P.add("Accent", spike((-.46, -.62, .92), (0, .4, 1), .08, .03))
    # helmet, mask, mohawk
    P.add("Body", L.loft([(1.9, .17, .16, 2.4, 0, -.04), (2.04, .2, .19, 2.4, 0, -.04), (2.2, .17, .16, 2.4, 0, -.04),
                          (2.25, .08, .08, 2.4, 0, -.04)], round_caps=.03))
    P.add("Accent", L.loft([(0, .16, .1, 2.4), (.06, .15, .09, 2.4)], axis="-Y", loc=(0, -.17, 1.97)))
    for sx in (-1, 1):
        P.eye(goggle(.055, (sx * .08, -.22, 2.0), (sx * .25, -1, .05), .045))
    P.eye(canister(.06, .1, (0, -.2, 1.9), (0, -1, -.5)))
    for k in range(5):
        P.add("Accent", spike((0, .12 - k * .07, 2.2), (0, .25, 1), .2 - abs(k - 2) * .04, .04, seg=4))
    return P


def hound(L):
    """Junkyard dog: a barrel-chested boiler body, a spiked tyre collar, a ridge of welded spikes down the back,
    a gas-mask muzzle whose filter canister is the glowing nose, an exhaust-pipe tail."""
    P = D.Parts()
    P.add("Body", L.loft([(-.42, .15, .15, 2.4), (-.25, .18, .19, 2.4), (0, .2, .22, 2.4), (.25, .23, .25, 2.4),
                          (.4, .19, .21, 2.4)], axis="-Y", loc=(0, 0, .82), round_caps=.04))
    for y in (-.12, .2):
        P.add("Accent", L.loft([(0, .225, .245, 2.4), (.045, .225, .245, 2.4)], axis="-Y", loc=(0, y, .82)))
    for k, y in enumerate((.3, .14, -.02, -.18)):
        P.add("Accent", spike((0, y, 1.0 + k * .02), (0, .35, 1), .14 + k * .02, .045, seg=4))
    P.add("Body", L.rod((0, -.34, .92), (0, -.54, 1.0), .1))
    P.add("Accent", tyre(.15, .06, .07, (0, -.44, .96), (0, -1, .4), m=18))
    for k in range(6):
        a = k / 6 * TAU
        d = Vector((math.cos(a), -.4 * math.sin(a) * 0, math.sin(a)))
        P.add("Accent", spike((math.cos(a) * .2, -.44 - math.sin(a) * .08, .96 + math.sin(a) * .2), d, .1, .028, seg=4))
    P.add("Body", L.loft([(0, .12, .12, 2.6), (.14, .13, .12, 2.6), (.22, .1, .09, 2.6)], axis="-Y", loc=(0, -.55, 1.0)))
    for sx in (-1, 1):
        P.add("Accent", [s.rbox((.03, .1, .16), (sx * .09, -.58, 1.16), r=.01, seg=1, taper=(.5, .3), rot=(-.3, sx * .3, 0))])
        P.eye(goggle(.04, (sx * .07, -.68, 1.06), (sx * .3, -1, .2), .035))
    P.eye(canister(.08, .2, (0, -.72, .9), (0, -1, -.3)))
    P.add("Accent", [tube([(0, .38, .9), (0, .52, 1.0), (0, .58, 1.16)], .035, res=1)] + L.cyl(.045, .06, (0, .58, 1.18)))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add("Body", L.ball(.1, (x * .9, y, .69)))
        P.add(leg, L.ball(.09, (x, y, D.HOUND_HIP)))
        P.add(leg, L.loft([(D.HOUND_KNEE, .055, .06, 3.0, x, y), (.48, .08, .09, 3.0, x, y), (D.HOUND_HIP, .07, .08, 3.0, x, y)]))
        P.add(leg, L.ball(.06, (x, y, D.HOUND_KNEE)))
        P.add(leg, L.rod((x, y, D.HOUND_KNEE), (x, y, .06), .045))
        P.add(leg, [s.rbox((.12, .17, .07), (x, y - .03, .035), r=.015, seg=1, taper=(.8, .7))])
        for k in (-1, 1):
            P.add(leg, spike((x + k * .035, y - .11, .03), (0, -1, -.2), .05, .015, seg=4))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """Hubcap tick: a knobbly tyre lying flat with a domed hubcap and a central spike on top, a lopsided head with a
    cluster of mismatched goggles, square rebar legs ending in spikes."""
    P = D.Parts()
    P.add("Body", tyre(.2, .085, .07, (0, .04, .17), (0, 0, 1)))
    P.add("Body", L.lathe([(0, .1), (.21, .1), (.2, .2), (.15, .25), (.07, .275), (0, .28)], loc=(0, .04, 0)))
    P.add("Body", spike((0, .04, .27), (0, 0, 1), .12, .05, seg=6))
    for k in range(5):
        a = k / 5 * TAU
        P.add("Body", rivets([(math.cos(a) * .12, .04 + math.sin(a) * .12, .245)], .025))
    P.add("Body", [s.rbox((.24, .14, .14), (.01, -.2, .18), r=.025, seg=1, rot=(0, .12, 0))])
    for (x, z, r) in ((-.055, .2, .045), (.06, .21, .032), (.03, .135, .024)):
        P.eye(goggle(r, (x, -.255, z), (0, -1, 0), .03))
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .022, kind="square") + [s.rbox((.06, .06, .06), knee, r=.01, seg=1)])
        P.add(f"AccentLeg{i}Lower", L.rod(knee, foot + Vector((0, 0, .05)), .018, kind="square")
              + spike(foot + Vector((0, 0, .06)), (0, 0, -1), .065, .025, seg=4))
    return P


# =========================================================================== wheeled

def comic_wheel(L, c, R, w):
    """A knobbly tyre on a chamfered hub (symmetric about its centre)."""
    return tyre(R * .72, R * .28, w / 2, c, (1, 0, 0), m=16, n=8, knobs=R * .07) + \
        [s.rcyl(R * .52, w * .9, c, axis="X", seg=14, r=.01, bevel_seg=1), s.rcyl(R * .2, w * 1.1, c, axis="X", seg=8, r=.006, bevel_seg=1)]


def listener(L):
    """Dish buggy: a square-tube roll cage on a low chassis with knobbly tyres and a tyre bumper, a boiler for an engine,
    two mismatched satellite dishes bolted on top as ears with glowing feed horns."""
    P = D.Parts()
    W = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", comic_wheel(L, c, .2, .12))
        P.add("Accent", L.rod((c[0] * .7, c[1], .22), (c[0] * .98, c[1], .22), .035))
    P.add("Body", plate((.64, .9, .08), (0, 0, .28)))
    P.add("Body", L.loft([(-.3, .17, .17, 2.4), (0, .2, .19, 2.4, 0, .02), (.3, .16, .16, 2.4)], axis="X", loc=(0, .1, .5)))
    P.add("Accent", tyre(.12, .05, .06, (0, -.5, .36), (0, -1, 0), m=16))
    P.add("Body", L.rod((0, -.4, .3), (0, -.5, .36), .04))
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", L.rod((sx * .28, sy * .38, .3), (sx * .24, sy * .28, .95), .028))
        P.add("Body", L.rod((sx * .24, -.28, .95), (sx * .24, .28, .95), .028))
    for sy in (-1, 1):
        P.add("Body", L.rod((-.24, sy * .28, .95), (.24, sy * .28, .95), .028))
    P.add("Body", L.rod((-.28, -.38, .3), (.24, -.28, .95), .024))
    P.add("Accent", plate((.36, .2, .04), (-.02, -.33, .78), rot=(.6, 0, .1)))
    for sx, r, z, up in ((-1, .27, 1.18, .45), (1, .2, 1.2, .7)):
        base = Vector((sx * .22, .1, 1.0))
        d = Vector((sx, 0, up)).normalized()
        P.add("Body", L.rod((sx * .2, .1, .93), tuple(base + d * .05), .03))
        P.add("Accent", dish(r, base + d * .04, d))
        P.add("Accent", L.rod(tuple(base + d * .06), tuple(base + d * (.06 + r * .7)), .015))
        P.add("Eye", [orient(s.rcyl(.035, .05, (0, 0, .025), seg=10, r=.005), base + d * (.06 + r * .7), d)])
    return P


def worker(L):
    """Scrap crane: a lopsided boiler on knobbly wheels with a smokestack, a gallows crane of square tube hanging the
    strapped loot crate in front, a caged headlamp."""
    P = D.Parts()
    W = [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", comic_wheel(L, c, .12, .1))
        P.add("Accent", L.rod((c[0] * .55, c[1], .12), (c[0] * .9, c[1], .12), .03))
    P.add("Body", plate((.48, .66, .08), (0, 0, .17)))
    P.add("Body", L.loft([(.18, .22, .24, 2.4, 0, .1), (.4, .25, .27, 2.4, .02, .12), (.62, .22, .24, 2.4, .04, .16),
                          (.75, .14, .16, 2.4, .05, .18)], round_caps=.04))
    P.add("Accent", L.loft([(.38, .26, .28, 2.4, .02, .12), (.43, .265, .285, 2.4, .02, .12)]))
    P.add("Body", L.cyl(.05, .4, (.14, .28, .9)) + L.cyl(.07, .06, (.14, .28, 1.1), radius2=.05))
    P.add("Body", L.rod((-.17, .28, .5), (-.17, .24, 1.6), .035) + L.rod((-.17, .24, 1.6), (0, -.18, 1.6), .035)
          + L.rod((-.17, .28, 1.2), (-.04, -.1, 1.6), .022))
    P.add("Accent", L.rod((0, -.15, 1.62), (0, -.15, 1.33), .015) + L.cyl(.05, .03, (0, -.15, 1.52), axis="X"))
    P.add("Accent", [s.rbox((.5, .42, .36), (0, -.15, 1.15), r=.025, seg=1)])
    for sx in (-1, 1):
        P.add("Body", plate((.06, .44, .38), (sx * .21, -.15, 1.15)))
        P.add("Body", [align(s.rbox((.05, .025, .5), r=.008, seg=1), (sx * .22, -.37, .99), (-sx * .22, -.37, 1.31))])
    P.add("Accent", L.rod((0, -.12, .45), (0, -.32, .49), .03))
    P.add("Accent", L.cyl(.075, .08, (0, -.35, .5), axis="Y"))
    P.add("Eye", L.cyl(.06, .02, (0, -.39, .5), axis="Y", seg=12))
    for k in (-1, 0, 1):
        P.add("Accent", [s.rbox((.012, .02, .16), (k * .035, -.405, .5), r=0, seg=1)])
    P.add("Accent", plate((.04, .3, .2), (.27, .1, .45), rot=(.2, 0, 0)))
    return P


def sweeper(L):
    """Plough roller: a dome boiler sitting in a knobbly tyre bumper, a welded dozer blade with rake teeth in front,
    goggle eyes and a stubby exhaust; the lidar spins under its belly."""
    P = D.Parts()
    W = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", comic_wheel(L, c, .08, .06))
        P.add("Accent", [s.rbox((.025, .05, .26), (c[0] + .045, c[1], .2), r=.006, seg=1),
                         s.rbox((.025, .05, .26), (c[0] - .045, c[1], .2), r=.006, seg=1)])
    P.add("Body", plate((.62, .62, .06), (0, 0, .32)))
    P.add("Accent", tyre(.31, .07, .07, (0, 0, .36), (0, 0, 1), m=24))
    P.add("Body", L.lathe([(0, .32), (.29, .32), (.3, .44), (.26, .57), (.16, .67), (0, .7)], loc=(.02, .02, 0), seg=16))
    P.add("Accent", L.cyl(.29, .03, (.02, .02, .47), seg=16))
    P.add("Body", L.cyl(.04, .2, (-.12, .14, .72)) + spike((.1, .12, .62), (.3, .3, 1), .12, .035))
    for sx in (-1, 1):
        P.eye(goggle(.055, (sx * .09, -.23, .54), (sx * .25, -1, .3), .04))
        P.add("Accent", L.rod((sx * .2, -.25, .34), (sx * .26, -.44, .2), .025))
    P.add("Accent", [s.rbox((.82, .05, .2), (0, -.47, .16), r=.015, seg=1, rot=(-.25, 0, 0))])
    for k in range(7):
        P.add("Accent", spike((-.36 + k * .12, -.5, .08), (0, -.6, -1), .07, .02, seg=4))
    P.add("Accent", L.cyl(.045, .13, (0, 0, .29)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + [s.rbox((.08, .05, .04), (0, -.12, .22), r=.008, seg=1)])
    return P


def thumper(L):
    """Pile-driver tank: a lopsided boiler hull on chunky tracks with a tall bent smokestack and a small gas-mask cab,
    two big pile-driver hammers in guide frames ramming the glowing seismic pads, tyres as dampers."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .56
        P.add("Accent", L.loft([(-.12, .26, .66, 3.0), (.12, .26, .66, 3.0)], axis="X", loc=(x, 0, .27)))
        for k in (-1, 0, 1):
            P.add("Accent", L.cyl(.13, .04, (x + sx * .13, k * .4, .27), axis="X"))
    P.add("Body", plate((1.0, 1.1, .14), (0, 0, .5)))
    P.add("Body", L.loft([(-.55, .38, .3, 2.4), (-.3, .46, .36, 2.4), (.2, .46, .38, 2.4, .03, .04), (.5, .4, .32, 2.4, .03, .02)],
                         axis="-Y", loc=(0, .05, .82), round_caps=.05))
    P.add("Accent", L.loft([(0, .475, .39, 2.4), (.05, .475, .39, 2.4)], axis="-Y", loc=(0, .1, .84)))
    P.add("Body", [tube([(.25, .3, 1.1), (.3, .36, 1.5), (.36, .34, 1.8)], .07, res=1)] + L.cyl(.1, .08, (.36, .34, 1.83)))
    P.add("Body", L.loft([(1.05, .17, .15, 2.4, -.1, -.22), (1.2, .18, .16, 2.4, -.1, -.24), (1.32, .12, .12, 2.4, -.1, -.22)]))
    for sx in (-1, 1):
        P.eye(goggle(.045, (-.1 + sx * .065, -.36, 1.22), (sx * .2, -1, 0), .035))
    P.eye(canister(.05, .08, (-.1, -.37, 1.12), (0, -1, -.4)))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", L.rod((x, -.38, 1.0), (x, -.92, 1.0), .045))
        for k in (-1, 1):
            P.add("Body", L.rod((x + k * .11, -.85, .99), (x + k * .11, -.85, .35), .025))
        P.add("Accent", L.cyl(.05, .6, (x, -.85, .7)))
        P.add("Accent", [s.rcyl(.14, .4, (x, -.85, .23), seg=12, r=.02, bevel_seg=1)])
        P.add("Accent", tyre(.12, .05, .05, (x, -.85, .38), (0, 0, 1), m=14))
        P.add("Eye", L.cyl(.18, .036, (x, -.85, .018)))
    P.add("Eye", L.cyl(.06, .03, (0, -.42, .55), axis="Y"))
    P.add("Accent", plate((.3, .05, .2), (.12, -.4, .78), rot=(-.3, .1, .15)))
    return P


# =========================================================================== tall, flying, mounted

def sorter(L):
    """Gas-mask periscope: a bent welded pipe on a tyre base, a lopsided gas-mask head with one giant goggle (the colour
    lens) and one small one, the filter canister hanging off its chin, spikes on top."""
    P = D.Parts()
    P.add("Accent", tyre(.22, .08, .07, (0, 0, .07), (0, 0, 1), m=20))
    P.add("Accent", L.cyl(.16, .06, (0, 0, .07)))
    P.add("Body", [tube([(0, 0, .08), (.05, .02, .45), (-.04, 0, .8), (0, .05, 1.02)], .05, res=1)])
    for z, x in ((.45, .05), (.8, -.04)):
        P.add("Accent", L.cyl(.07, .05, (x, .01, z)))
    P.add("Body", L.loft([(.98, .17, .14, 2.4, 0, .06), (1.1, .27, .2, 2.4, .02, .04), (1.3, .29, .21, 2.4, .02, .04),
                          (1.46, .22, .17, 2.4, 0, .05), (1.5, .1, .09, 2.4, 0, .06)], round_caps=.03))
    acc, glow = goggle(.18, (-.04, -.1, 1.25), (0, -1, 0), .07, seg=24)
    P.add("Accent", acc).add("Eye", glow)
    P.eye(goggle(.05, (.2, -.12, 1.38), (.3, -1, .1), .04))
    P.eye(canister(.07, .13, (.1, -.05, 1.02), (.25, -.5, -1)))
    for k in range(3):
        P.add("Accent", spike((-.12 + k * .12, .06, 1.47 - abs(k - 1) * .02), (-.3 + k * .3, 0, 1), .12 + k * .03, .035))
    return P


def drone(L):
    """Gas-mask copter: the body is a flattened gas mask flying face-first — two big goggles, the filter canister under
    the chin is the camera — on four welded square arms with elbow spikes and two-blade props."""
    P = D.Parts()
    P.add("Body", L.loft([(-.2, .14, .06, 2.4), (-.08, .22, .1, 2.4), (.12, .2, .1, 2.4), (.22, .12, .07, 2.4)],
                         axis="-Y", loc=(0, 0, 0), round_caps=.03))
    P.add("Body", tyre(.26, .05, .055, (0, .02, 0), (0, 0, 1), m=20))
    for k in range(6):
        a = (k + .5) / 6 * TAU
        P.add("Body", spike((math.cos(a) * .3, .02 + math.sin(a) * .3, 0), (math.cos(a), math.sin(a), 0), .07, .022, seg=4))
    for sx in (-1, 1):
        P.eye(goggle(.068, (sx * .1, -.15, .03), (sx * .2, -1, .25), .045))
    acc, glow = canister(.055, .08, (0, -.15, -.05), (0, -.6, -1))
    P.add("Body", acc).add("Eye", glow)
    for i, (x, y) in enumerate([(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]):
        P.add("Body", L.rod((x * .3, y * .3, 0), (x, y, .0), .024, kind="square"))
        P.add("Body", spike((x * .65, y * .65, .02), (x, y, .7), .07, .02, seg=4))
        P.add("Body", L.cyl(.04, .04, (x, y, .005)))
        rot = [s.rcyl(.03, .089, (x, y, .0645), seg=8, r=.008, bevel_seg=1)]
        rot += [s.rbox((.34, .05, .012), (x, y, .07), r=.005, seg=1, rot=(.3, 0, math.radians(45 if x * y > 0 else -45)))]
        P.add(f"AccentRotor{i}", rot)
    return P


def turret(L):
    """Tyre-stack gun: three tyres stacked around a pipe post with welded braces and a glowing lamp ring (still);
    the turning head is a lopsided boiler gun behind a spiked scrap shield with a muzzle brake and an ammo drum."""
    P = D.Parts()
    for k, z in enumerate((.09, .26, .43)):
        P.add("Body", tyre(.24, .09, .08, (0, 0, z), (0, 0, 1), m=18))
    P.add("Body", L.cyl(.17, .5, (0, 0, .26)))
    P.add("Body", L.cyl(.07, .7, (0, 0, .8)))
    for k in range(3):
        a = k / 3 * TAU + .5
        P.add("Body", L.rod((math.cos(a) * .25, math.sin(a) * .25, .5), (math.cos(a) * .06, math.sin(a) * .06, .95), .022))
    P.add("Body", L.cyl(.1, .06, (0, 0, 1.08)))
    P.add("Eye", L.cyl(.11, .035, (0, 0, .99)))
    for k in range(3):
        a = k / 3 * TAU + 1.5
        P.add("Eye", L.ball(.03, (math.cos(a) * .3, math.sin(a) * .3, .5)))
    P.add("AccentHead", L.cyl(.09, .1, (0, 0, 1.15)))
    P.add("AccentHead", L.loft([(-.2, .14, .12, 2.4), (0, .17, .14, 2.4, .01, .01), (.15, .15, .12, 2.4, .02, 0),
                                (.22, .1, .09, 2.4)], axis="-Y", loc=(0, .02, 1.3)))
    P.add("AccentHead", L.cyl(.055, .5, (0, -.42, 1.3), axis="Y") + [s.rbox((.17, .14, .11), (0, -.66, 1.3), r=.015, seg=1)])
    P.add("AccentHead", L.cyl(.075, .08, (0, -.2, 1.3), axis="Y"))
    P.add("AccentHead", plate((.34, .04, .2), (-.03, -.22, 1.3), rot=(.15, 0, .12)))
    P.add("AccentHead", [tube([(.19, .05, 1.22), (.12, -.08, 1.2), (.06, -.2, 1.26)], .025, res=1)])
    for k in (-1, 0, 1):
        P.add("AccentHead", spike((k * .1 - .03, -.22, 1.38), (k * .3, 0, 1), .07, .024, seg=4))
    P.add("AccentHead", L.cyl(.08, .08, (.19, .05, 1.28), axis="X"))
    return P


def vacuum(L):
    """Spiked hubcap: a car hubcap clamped to the ceiling, its dome and spokes facing the room, spikes around the rim,
    one gas-mask goggle in the hub."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, -.03), (.15, -.025), (.28, 0), (.36, .04), (.36, .08), (.3, .1), (0, .1)], seg=24))
    for k in range(5):
        a = k / 5 * TAU
        P.add("Accent", [align(s.rbox((.05, .04, .2), r=.01, seg=1), (math.cos(a) * .1, math.sin(a) * .1, -.025),
                               (math.cos(a) * .29, math.sin(a) * .29, .01))])
    for k in range(10):
        a = (k + .5) / 10 * TAU
        P.add("Accent", spike((math.cos(a) * .34, math.sin(a) * .34, .06), (math.cos(a), math.sin(a), 0), .07, .025, seg=4))
    P.eye(goggle(.07, (0, 0, -.02), (0, 0, -1), .025))
    return P


# =========================================================================== devices and sensor modules

def lidar(L):
    """A tyre on the floor, a square pipe post, a lopsided drum head with a slit and a spike that spins."""
    P = D.Parts()
    P.add("Body", tyre(.13, .05, .05, (0, 0, .05), (0, 0, 1), m=18) + L.cyl(.1, .06, (0, 0, .05)))
    P.add("Body", L.rod((0, 0, .06), (0, 0, .4), .03, kind="square") + L.cyl(.06, .03, (0, 0, .41)))
    P.add("Eye", L.cyl(.07, .02, (0, 0, .43)) + L.box((.04, .06, .04), (0, -.1, .45), r=.008))
    P.add("Body", L.rod((0, -.03, .43), (0, -.08, .45), .012, kind="square"))
    P.add("AccentHead", L.loft([(.395, .09, .09, 2.4), (.45, .1, .095, 2.4, .01, 0), (.51, .08, .08, 2.4)]))
    P.add("AccentHead", spike((.03, .02, .5), (.3, .2, 1), .07, .02, seg=4))
    return P


def pressure_plate(L):
    """The pressure plate with welded X straps."""
    P = D.plate(L)
    for a in (1, -1):
        P.add("Accent", [s.rbox((1.3, .06, .006), (0, 0, .014), r=.002, seg=1, rot=(0, 0, a * math.pi / 4))])
    return P


def attach_mic(L):
    """A small dish on a bent square pipe."""
    P = D.Parts()
    P.add("Accent", L.cyl(.045, .02, (0, 0, .01)) + L.rod((0, 0, .01), (0, 0, .27), .014, kind="square"))
    P.add("Body", dish(.068, (0, .02, .3), (0, -1, .2), seg=12))
    P.add("Accent", L.rod((0, .02, .27), (0, .02, .31), .014, kind="square"))
    P.add("Eye", L.ball(.025, (0, -.03, .32)))
    P.add("Accent", L.rod((0, .0, .31), (0, -.03, .32), .006))
    return P


def attach_seismic(L):
    """A welded stake: a square plate on a square bar, the glowing spike in the floor."""
    P = D.Parts()
    P.add("Accent", plate((.16, .16, .03), (0, 0, .3), rot=(0, 0, .3)) + L.rod((0, 0, .3), (0, 0, .09), .02, kind="square"))
    P.add("Eye", [s.rcyl(.04, .1, (0, 0, .05), seg=6, r=0, radius2=.004, rot=(math.pi, 0, 0))])
    return P


def pir(L):
    """A goggle welded to a scrap plate."""
    P = D.Parts()
    P.add("Body", plate((.16, .06, .16), (0, .04, 0), rot=(0, .15, 0)))
    acc, glow = goggle(.045, (0, .01, 0), (0, -1, 0), .03)
    P.add("Body", acc).add("Eye", glow)
    return P


def security_camera(L):
    """A gas-mask head on a square bracket: two goggles, the lens is the filter canister."""
    P = D.Parts()
    P.add("Body", plate((.16, .06, .22), (0, .27, 0)))
    P.add("Accent", L.rod((0, .26, 0), (0, .08, -.02), .022))
    P.add("Accent", L.loft([(-.1, .08, .07, 2.4), (.06, .09, .08, 2.4), (.14, .07, .06, 2.4)], axis="-Y", loc=(0, 0, -.02)))
    acc, glow = canister(.05, .07, (0, -.13, -.03), (0, -1, 0))
    P.add("Accent", acc).add("Eye", glow)
    P.add("Accent", spike((0, .02, .05), (0, .3, 1), .08, .025, seg=4))
    return P


def laser_emitter(L):
    """A square post of welded plates with spikes on top."""
    P = D.laser_emitter(L)
    for k in (-1, 1):
        P.add("Body", plate((.15, .15, .08), (0, 0, .2 + (k + 1) * .6), rot=(0, 0, .4)))
    P.add("Body", spike((0, 0, 1.59), (0, 0, 1), .12, .04, seg=4))
    return P


def attach_eye(L):
    P = D.Parts()
    P.add("Accent", L.loft([(-.09, .1, .1, 2.4), (.05, .11, .11, 2.4)], axis="-Y", loc=(0, 0, 0)))
    acc, glow = goggle(.06, (0, -.04, 0), (0, -1, 0), .035)
    P.add("Accent", acc).add("Eye", glow)
    return P


def attach_nose(L):
    """A gas-mask filter snout."""
    P = D.Parts()
    acc, glow = canister(.085, .25, (0, 0, 0), (0, -1, 0))
    P.add("Accent", acc).add("Eye", glow)
    return P


# =========================================================================== furniture with the wasteland's own take

def safe(L):
    """Bandit vault: a welded box with a riveted door plate, a car steering wheel for the handle, big hinges."""
    P = D.Parts()
    P.add("Body", L.carcass((.8, .7, 1.0), (0, 0, 0), kick=.04))
    P.add("Body", [s.rbox((.68, .04, .84), (0, -.365, .52), r=.008, seg=1)]
          + rivets([(sx * .3, -.385, .52 + sz * .38) for sx in (-1, 0, 1) for sz in (-1, 1)], .018))
    wheel = torus_ring(.13, .018, (0, -.405, .58), (0, -1, 0))
    wheel += [s.rcyl(.04, .04, (0, -.39, .58), axis="Y", seg=10, r=.006, bevel_seg=1)]
    for k in range(3):
        a = k / 3 * TAU + math.pi / 2
        wheel.append(align(s.rbox((.02, .02, .13), r=.004, seg=1), (0, -.4, .58),
                           (math.cos(a) * .13, -.405, .58 + math.sin(a) * .13)))
    P.add("Accent", wheel)
    P.add("Accent", [s.rcyl(.03, .14, (-.37, -.37, z), seg=8, r=.004, bevel_seg=1) for z in (.3, .76)])
    P.add("Eye", [s.ball(.025, (.27, -.385, .88), seg=8)])
    return P


def plant(L):
    """A spiky cactus planted in a stack of two tyres."""
    P = D.Parts()
    P.add("Accent", tyre(.235, .095, .1, (0, 0, .1), (0, 0, 1), m=20) + tyre(.235, .095, .1, (0, 0, .3), (0, 0, 1), m=20))
    P.add("Accent", [s.rcyl(.16, .38, (0, 0, .19), seg=10, r=0)])
    cactus = [s.loft([(.3, .1, .1, 3.0), (1.25, .1, .1, 3.0), (1.4, .03, .03, 3.0)], seg=8)]
    for sx, z0, z1 in ((-1, .7, 1.12), (1, .82, 1.25)):
        cactus.append(tube([(0, 0, z0), (sx * .22, 0, z0 + .02), (sx * .25, 0, z1)], .06, res=1, smooth=False))
        cactus.append(s.ball(.06, (sx * .25, 0, z1), seg=8))
    for k in range(10):
        a = k * 2.4
        z = .45 + k * .085
        cactus += spike((math.cos(a) * .09, math.sin(a) * .09, z), (math.cos(a), math.sin(a), .3), .06, .012, seg=3)
    P.add("Body", cactus)
    return P


def floor_lamp(L):
    """A car headlight hanging from a rebar pole stuck in a tyre."""
    P = D.Parts()
    P.add("Body", tyre(.12, .05, .05, (0, 0, .05), (0, 0, 1), m=18) + [s.rcyl(.09, .09, (0, 0, .05), seg=10, r=0)])
    P.add("Body", [s.rbox((.035, .035, 1.32), (0, 0, .72), r=.005, seg=1)]
          + [align(s.rbox((.025, .025, .2), r=.004, seg=1), (0, 0, 1.15), (.0, .08, 1.3))])
    P.add("Accent", [s.lathe([(.2, 0), (.2, .025), (.17, .1), (.11, .2), (.04, .24), (0, .25)], seg=10, loc=(0, 0, 1.37))])
    for k in range(3):
        a = k / 3 * TAU
        P.add("Accent", spike((math.cos(a) * .08, math.sin(a) * .08, 1.58), (math.cos(a) * .4, math.sin(a) * .4, 1), .07, .02, seg=4))
    P.add("Eye", [s.rcyl(.14, .02, (0, 0, 1.36), seg=10, r=.003, bevel_seg=1)])
    return P


def desk_lamp(L):
    """A car headlight on a bent rebar arm over a small tyre."""
    P = D.Parts()
    P.add("Body", tyre(.05, .025, .02, (0, 0, .02), (0, 0, 1), m=14, knobs=.004) + [s.rcyl(.06, .03, (0, 0, .02), seg=10, r=0)])
    P.add("Body", [tube([(0, 0, .03), (0, .03, .24), (0, -.1, .33)], .011, smooth=False)])
    P.add("Accent", [orient(s.lathe([(0, 0), (.04, 0), (.06, .05), (.068, .1), (.06, .105)], seg=10), Vector((0, -.13, .34)),
                            (0, -.55, -1))])
    P.add("Eye", [s.ball(.03, (0, -.16, .26), seg=8)])
    return P


def server_rack(L):
    """A welded frame stacked with car batteries, each with a glowing charge lamp."""
    P = D.Parts()
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", [s.rbox((.05, .05, 2.0), (sx * .32, sy * .47, 1.0), r=.006, seg=1)])
    for z in (.03, 1.975):
        P.add("Body", [s.rbox((.7, 1.0, .05), (0, 0, z), r=.006, seg=1)])
    for i in range(7):
        z = .17 + i * .26
        P.add("Body", [s.rbox((.6, .9, .2), (0, 0, z), r=.012, seg=1), s.rbox((.68, .96, .03), (0, 0, z - .115), r=.004, seg=1)])
        P.add("Body", [s.rcyl(.025, .04, (sx * .18, .3, z + .11), seg=8, r=.004, bevel_seg=1) for sx in (-1, 1)])
        P.add("Eye", [s.rbox((.12, .02, .05), (-.15, -.455, z + .03), r=.006, seg=1)])
    return P


def vending_machine(L):
    """A caged vending machine: welded cabinet, the glowing window behind rebar bars, a fuel nozzle on a hose."""
    P = D.Parts()
    P.add("Body", L.carcass((.88, .78, 1.9), (0, -.01, 0), kick=.05))
    P.add("Eye", [s.rbox((.6, .02, 1.1), (-.1, -.405, 1.15), r=.006, seg=1)])
    for k in range(6):
        P.add("Accent", [s.rbox((.025, .025, 1.18), (-.37 + k * .108, -.425, 1.15), r=.004, seg=1)])
    for z in (.58, 1.72):
        P.add("Accent", [s.rbox((.66, .03, .04), (-.1, -.425, z), r=.004, seg=1)])
    P.add("Accent", [s.rbox((.14, .03, .5), (.33, -.41, 1.2), r=.006, seg=1), s.rbox((.6, .03, .15), (-.1, -.41, .25), r=.006, seg=1)])
    P.add("Accent", [tube([(.36, -.415, 1.0), (.4, -.425, .7), (.36, -.42, .45), (.32, -.415, .62)], .018),
                     s.rbox((.05, .04, .14), (.32, -.42, .67), r=.008, seg=1)])
    return P


def arcade_machine(L):
    """A welded arcade cabinet with a tyre bumper, a spiked marquee and guard bars over the slanted screen."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .8, 1.78), (0, 0, 0), kick=.05))
    P.add("Accent", [s.rbox((.72, .4, .06), (0, -.38, .95), r=.01, seg=1, rot=(math.radians(-12), 0, 0)),
                     s.rbox((.72, .25, .2), (0, -.1, 1.7), r=.01, seg=1)])
    P.add("Accent", tyre(.19, .07, .07, (0, -.36, .3), (0, -1, 0), m=18))
    for sx in (-1, 1):
        P.add("Accent", spike((sx * .3, -.12, 1.79), (sx * .3, 0, 1), .03, .025, seg=4))
    P.add("Eye", [s.rbox((.56, .03, .44), (0, -.39, 1.3), r=.006, seg=1, rot=(math.radians(-15), 0, 0))])
    for k in (-1, 0, 1):
        P.add("Accent", [s.rbox((.62, .02, .025), (0, -.42, 1.3 + k * .14), r=.004, seg=1, rot=(math.radians(-15), 0, 0))])
    P.add("Accent", L.rod((-.16, -.42, .97), (-.16, -.44, 1.06), .012) + L.ball(.035, (-.16, -.44, 1.07)))
    return P


def trash_bin(L):
    """A dented oil drum with hoops and a spiked rim."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.17, 0), (.18, .02), (.18, .4), (.17, .41), (0, .41)], seg=14))
    for z in (.13, .28):
        P.add("Body", [s.rcyl(.19, .025, (0, 0, z), seg=14, r=.004, bevel_seg=1)])
    P.add("Accent", [s.rcyl(.19, .03, (0, 0, .415), seg=14, r=.006, bevel_seg=1)])
    for k in range(6):
        a = k / 6 * TAU
        P.add("Accent", spike((math.cos(a) * .17, math.sin(a) * .17, .42), (math.cos(a) * .3, math.sin(a) * .3, 1), .025, .015, seg=4))
    return P


# --------------------------------------------------------------------------- the wasteland's construction kit

def drum(r, h, loc, seg=14):
    """An oil drum: chamfered rims and two rolling hoops."""
    x, y, z = loc
    return [s.lathe([(0, 0), (r * .95, 0), (r, .02), (r, h - .02), (r * .95, h), (0, h)], seg=seg, loc=loc)] + \
        [s.rcyl(r * 1.04, .025, (x, y, z + h * k), seg=seg, r=.004, bevel_seg=1) for k in (.3, .7)]


def frame(size, loc, bar=.04):
    """A welded box frame of square bars standing on the floor (loc = centre of its footprint)."""
    w, d, h = size
    x, y, z = loc
    out = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            out.append(s.rbox((bar, bar, h), (x + sx * (w / 2 - bar / 2), y + sy * (d / 2 - bar / 2), z + h / 2), r=0, seg=1))
    for zz in (z + bar / 2, z + h - bar / 2):
        for sy in (-1, 1):
            out.append(s.rbox((w - bar * 2 + .004, bar, bar), (x, y + sy * (d / 2 - bar / 2), zz), r=0, seg=1))
        for sx in (-1, 1):
            out.append(s.rbox((bar, d - bar * 2 + .004, bar), (x + sx * (w / 2 - bar / 2), y, zz), r=0, seg=1))
    return out


def scrap_box(size, loc, seed=0):
    """A welded frame skinned with mismatched plates tacked on a little askew."""
    w, d, h = size
    x, y, z = loc
    out = frame(size, loc)
    out.append(s.rbox((w - .03, d - .03, h - .03), (x, y, z + h / 2), r=.006, seg=1))
    tilt = ((seed * 7) % 5 - 2) * .02
    out.append(s.rbox((w * .55, .02, h * .5), (x - w * .18, y - d / 2 - .004, z + h * .62), r=.004, seg=1, rot=(0, tilt, 0)))
    out.append(s.rbox((w * .5, .02, h * .4), (x + w * .2, y - d / 2 - .004, z + h * .3), r=.004, seg=1, rot=(0, -tilt * 1.5, 0)))
    return out


def corrugated(w, h, loc, along="X", ribs=None):
    """A corrugated sheet standing on edge: a thin sheet with vertical ribs on both faces."""
    x, y, z = loc
    n = ribs or max(3, int(w / .1))
    if along == "X":
        out = [s.rbox((w, .016, h), (x, y, z), r=0, seg=1)]
        for k in range(n):
            xx = x - w / 2 + (k + .5) * w / n
            out.append(s.rbox((.025, .03 if k % 2 else .034, h), (xx, y, z), r=0, seg=1))
    else:
        out = [s.rbox((.016, w, h), (x, y, z), r=0, seg=1)]
        for k in range(n):
            yy = y - w / 2 + (k + .5) * w / n
            out.append(s.rbox((.03 if k % 2 else .034, .025, h), (x, yy, z), r=0, seg=1))
    return out


def car_seat(size, loc, back=True):
    """A car seat: a tapered pad, its back leaning, straps across."""
    w, d, h = size
    x, y, z = loc
    out = [s.rbox((w, d, h), (x, y, z), r=.03, seg=1, taper=(.95, .95))]
    out.append(s.rbox((.05, d + .016, h + .012), (x + w * .25, y, z), r=.004, seg=1))
    return out


def caged_tv(size, loc):
    """(accent, glow): a TV in a welded cage."""
    w, d, h = size
    x, y, z = loc
    acc = [s.rbox((w, d, h), (x, y, z), r=.012, seg=1)]
    for k in (-1, 0, 1):
        acc.append(s.rbox((w + .02, .02, .02), (x, y - d / 2 - .02, z + k * h * .3), r=0, seg=1))
    for sx in (-1, 1):
        acc.append(s.rbox((.02, .05, h + .02), (x + sx * (w / 2 + .005), y - d / 2 - .01, z), r=0, seg=1))
    glow = [s.rbox((w - .06, .012, h - .06), (x, y - d / 2 - .004, z), r=.004, seg=1)]
    return acc, glow


# =========================================================================== every prop as welded scrap

def desk(L):
    """A riveted plate on two oil drums, a caged TV for a monitor."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    for x in (-.5, .5):
        P.add("Accent", drum(.29, .715, (x, 0, 0)))
    a, g = caged_tv((.5, .3, .38), (0, .17, .955))
    P.add("Accent", a + [s.rbox((.44, .15, .02), (0, -.12, .775), r=.006, seg=1)]).add("Eye", g)
    return P


def terminal(L):
    """The console: a riveted plate on square tube legs, an oil-drum computer, three caged screens."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", L.four_legs(.72, .8, .4, .72, .05) + drum(.17, .5, (.45, .05, 0), seg=12))
    for x in (-.36, 0, .36):
        a, g = caged_tv((.3, .2, .33), (x, .19, .945))
        P.add("AccentConsole", a).add("Eye", g)
    P.add("AccentConsole", [s.rbox((.52, .17, .025), (0, -.1, .778), r=.006, seg=1)])
    return P


def chair(L):
    """A car seat bolted on two stacked tyres."""
    P = D.Parts()
    P.add("Accent", tyre(.19, .09, .1, (0, 0, .11), (0, 0, 1), m=16) + tyre(.19, .09, .1, (0, 0, .31), (0, 0, 1), m=16)
          + [s.rcyl(.15, .4, (0, 0, .21), seg=10, r=0)])
    P.add("Body", car_seat((.52, .5, .12), (0, -.02, .47)) + [s.place(s.rbox((.48, .13, .56), r=.03, seg=1, taper=(.9, .9)),
                                                                      (0, .22, .79), (-.15, 0, 0))])
    return P


def cabinet(L):
    """A welded frame holding three ammo boxes as drawers."""
    P = D.Parts()
    P.add("Body", frame((.9, .5, 1.4), (0, -.01, 0)))
    for i in range(3):
        z = .06 + i * .45
        P.add("Body", [s.rbox((.82, .44, .4), (0, -.03, z + .2), r=.012, seg=1)])
        P.add("Accent", [s.rbox((.2, .04, .03), (0, -.27, z + .3), r=.006, seg=1), s.rbox((.84, .02, .03), (0, -.25, z + .37), r=0, seg=1)])
    return P


def meeting_table(L):
    """A riveted plate on two stacks of tyres."""
    P = D.Parts()
    P.add("Body", L.slab((3.2, 1.3, .06), (0, 0, .75)))
    for x in (-1.1, 1.1):
        P.add("Accent", sum([tyre(.28, .1, .11, (x, 0, .11 + k * .22), (0, 0, 1), m=18) for k in range(3)], [])
              + [s.rcyl(.32, .06, (x, 0, .69), seg=12, r=.006, bevel_seg=1), s.rcyl(.22, .66, (x, 0, .33), seg=10, r=0)])
    return P


def kitchen_counter(L):
    """Scrap cupboards under a plate top, a microwave in a cage."""
    P = D.Parts()
    for i in range(4):
        P.add("Body", scrap_box((.59, .64, .88), (-.9 + i * .6, 0, 0), seed=i))
    P.add("Accent", L.slab((2.44, .7, .05), (0, 0, .905)))
    a, g = caged_tv((.5, .42, .3), (.7, .02, 1.085))
    P.add("Accent", a).add("Eye", g)
    return P


def shelf(L):
    """A rebar rack with X braces, oil drums and ammo boxes on the shelves."""
    P = D.Parts()
    for x in (-.97, .97):
        for y in (-.27, .27):
            P.add("Body", [s.rbox((.05, .05, 2.0), (x, y, 1.0), r=0, seg=1)])
        P.add("Body", [align(s.rbox((.025, .025, 2.1), r=0, seg=1), (x, .27, .05), (x, -.27, 1.95))])
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", L.slab((2.0, .6, .04), (0, 0, h)))
    P.add("Accent", drum(.17, .38, (-.6, 0, .47)) + [s.rbox((.4, .3, .22), (.5, .05, .58), r=.01, seg=1)]
          + drum(.2, .45, (.3, 0, 1.07)) + [s.rbox((.45, .35, .25), (-.55, 0, 1.79), r=.01, seg=1)])
    return P


def boxes(L):
    """Three ammo crates with lid bands and side handles."""
    P = D.Parts()
    for (sz, c) in (((.6, .6, .5), (0, 0, .25)), ((.5, .5, .4), (.05, -.05, .7)), ((.45, .45, .4), (.545, .1, .2))):
        P.add("Accent", [s.rbox(sz, c, r=.015, seg=1), s.rbox((sz[0] + .016, sz[1] + .016, .04), (c[0], c[1], c[2] + sz[2] * .32), r=0, seg=1)]
              + rivets([(c[0] + sx * (sz[0] / 2 - .05), c[1] - sz[1] / 2 - .005, c[2] - sz[2] * .2) for sx in (-1, 1)], .018))
    return P


def water_cooler(L):
    """A welded stand with a glowing jug locked in a rebar cage."""
    P = D.Parts()
    P.add("Body", scrap_box((.34, .34, 1.0), (0, 0, 0), seed=3))
    P.add("Eye", L.lathe([(0, 0), (.06, 0), (.06, .05), (.14, .11), (.14, .33), (.1, .43), (0, .465)], loc=(0, 0, .975)))
    for k in range(4):
        a = k / 4 * TAU + math.pi / 4
        P.add("Body", [s.rbox((.02, .02, .46), (math.cos(a) * .155, math.sin(a) * .155, 1.21), r=0, seg=1)])
    P.add("Body", torus_ring(.155, .012, (0, 0, 1.43), m=12, n=4) + torus_ring(.155, .012, (0, 0, 1.2), m=12, n=4))
    return P


def whiteboard(L):
    """A riveted scrap sheet on square tube legs, spikes on the top corners."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.8, .03, 1.0), (0, 0, 1.5), r=.006, seg=1)]
          + rivets([(sx * .85, -.02, 1.5 + sz * .45) for sx in (-1, 1) for sz in (-1, 1)], .016))
    P.add("Accent", [s.rbox((1.86, .05, .05), (0, 0, z), r=0, seg=1) for z in (1.0, 1.95)])
    for x in (-.9, .9):
        P.add("Accent", [s.rbox((.05, .05, 1.95), (x, 0, .975), r=0, seg=1), s.rbox((.1, .07, .02), (x, 0, .01), r=0, seg=1)]
              + spike((x, 0, 1.97), (0, 0, 1), .05, .025, seg=4))
    return P


def sofa(L):
    """A car bench seat on a welded frame with two tyres standing up as armrests."""
    P = D.Parts()
    P.add("Accent", frame((1.5, .7, .34), (0, -.02, 0)))
    P.add("Body", car_seat((1.5, .76, .16), (0, -.05, .42)) + [s.place(s.rbox((1.5, .2, .5), r=.03, seg=1, taper=(.95, .9)),
                                                                       (0, .31, .64), (-.12, 0, 0))])
    for sx in (-1, 1):
        P.add("Accent", tyre(.26, .1, .1, (sx * .99, 0, .37), (1, 0, 0), m=20)
              + [s.rbox((.3, .06, .06), (sx * .86, 0, .37), r=0, seg=1), s.rcyl(.2, .16, (sx * .99, 0, .37), axis="X", seg=10, r=0)])
    return P


def toilet_stall(L):
    """A cubicle of corrugated sheets on square posts, an oil drum for a toilet."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Body", corrugated(1.5, 1.88, (sx * .5, 0, 1.06), along="Y"))
    P.add("Body", corrugated(1.04, 1.88, (0, .75, 1.06)))
    for sx in (-1, 1):
        for y in (-.7, 0.0):
            P.add("Body", [s.rbox((.05, .05, .14), (sx * .5, y, .07), r=0, seg=1)])
    P.add("Accent", drum(.2, .42, (0, .4, 0)) + [s.rbox((.36, .14, .36), (0, .66, .55), r=.01, seg=1)])
    return P


def partition(L):
    """A corrugated sheet in a rebar frame with spikes along the top."""
    P = D.Parts()
    P.add("Body", corrugated(1.52, 1.1, (0, 0, .63)))
    P.add("Accent", [s.rbox((1.6, .05, .04), (0, 0, z), r=0, seg=1) for z in (.04, 1.18)]
          + [s.rbox((.04, .05, 1.2), (x, 0, .6), r=0, seg=1) for x in (-.79, .79)])
    for k in range(7):
        P.add("Accent", spike((-.72 + k * .24, 0, 1.19), (0, 0, 1), .07, .02, seg=4))
    return P


def bookshelf(L):
    """A welded frame with tilted plate shelves and a patchwork back."""
    P = D.Parts()
    P.add("Body", frame((1.0, .35, 2.0), (0, 0, 0)))
    for i, h in enumerate((.04, .5, 1.0, 1.5, 1.97)):
        P.add("Body", [s.rbox((.96, .33, .03), (0, 0, h), r=0, seg=1, rot=(0, ((i * 3) % 3 - 1) * .015, 0))])
    P.add("Body", [s.rbox((.6, .02, 1.1), (-.18, .165, .6), r=0, seg=1), s.rbox((.55, .02, 1.0), (.2, .165, 1.45), r=0, seg=1)])
    for i, h in enumerate((.052, .512, 1.012, 1.512)):
        x = -.43
        for k in range(6 + i % 2):
            w = .07 + .03 * ((k + i) % 3)
            hh = .32 - .04 * ((k * 7 + i) % 3)
            P.add("Accent", [s.rbox((w, .26, hh), (x + w / 2, .02, h + hh / 2), r=.006, seg=1)])
            x += w + .012
    return P


def printer(L):
    """A scrap-box copier with a plate tray."""
    P = D.Parts()
    P.add("Body", scrap_box((.6, .6, .95), (0, 0, 0), seed=1) + [s.rbox((.56, .52, .14), (0, .02, 1.0), r=.01, seg=1)])
    P.add("Accent", [s.rbox((.36, .3, .03), (-.1, -.35, .82), r=.006, seg=1, rot=(math.radians(8), 0, 0)),
                     s.rbox((.06, .2, .06), (-.1, -.25, .78), r=0, seg=1)])
    P.add("Eye", [s.rbox((.16, .03, .08), (.18, -.29, .97), r=.006, seg=1)])
    return P


def fridge(L):
    """A dented fridge wrapped in a chain with a padlock."""
    P = D.Parts()
    P.add("Body", [s.rbox((.7, .68, 1.76), (0, .01, .9), r=.03, seg=1)])
    P.add("Body", [s.rbox((.66, .03, .48), (0, -.34, 1.5), r=.006, seg=1, rot=(0, .02, 0)),
                   s.rbox((.66, .03, 1.16), (0, -.34, .66), r=.006, seg=1, rot=(0, -.015, 0))])
    links = []
    for k in range(9):
        links += torus_ring(.035, .01, (-.32 + k * .08, -.36, 1.05), (1, 0, 0) if k % 2 else (0, 1, 0), m=8, n=4)
    P.add("Accent", links + [s.rbox((.1, .04, .12), (0, -.375, .97), r=.01, seg=1)])
    P.add("Accent", [s.rbox((.04, .04, .4), (.27, -.37, 1.3), r=.006, seg=1)])
    return P


def round_table(L):
    """A riveted round plate on three stacked tyres."""
    P = D.Parts()
    P.add("Accent", sum([tyre(.3, .12, .12, (0, 0, .12 + k * .24), (0, 0, 1), m=18) for k in range(3)], [])
          + [s.rcyl(.25, .7, (0, 0, .36), seg=10, r=0)])
    P.add("Body", [s.rcyl(.5, .04, (0, 0, .74), seg=16, r=.008, bevel_seg=1)]
          + rivets([(math.cos(k / 6 * TAU) * .44, math.sin(k / 6 * TAU) * .44, .762) for k in range(6)], .016))
    return P


def armchair(L):
    """A car seat on a lying tyre, two tyres standing up as armrests."""
    P = D.Parts()
    P.add("Accent", tyre(.24, .1, .12, (0, 0, .12), (0, 0, 1), m=16) + tyre(.24, .1, .12, (0, 0, .34), (0, 0, 1), m=16)
          + [s.rcyl(.2, .44, (0, 0, .22), seg=10, r=0)])
    for sx in (-1, 1):
        P.add("Accent", tyre(.18, .07, .06, (sx * .37, 0, .46), (1, 0, 0), m=16))
    P.add("Body", car_seat((.6, .7, .14), (0, -.03, .5)) + [s.place(s.rbox((.58, .16, .4), r=.03, seg=1, taper=(.9, .9)),
                                                                     (0, .26, .66), (-.15, 0, 0))])
    return P


def coffee_table(L):
    """A riveted plate on two pairs of stacked tyres."""
    P = D.Parts()
    for x in (-.27, .27):
        P.add("Accent", tyre(.17, .07, .1, (x, 0, .1), (0, 0, 1), m=14) + tyre(.17, .07, .1, (x, 0, .29), (0, 0, 1), m=14)
              + [s.rcyl(.14, .38, (x, 0, .19), seg=10, r=0)])
    P.add("Body", L.slab((1.0, .55, .04), (0, 0, .4)))
    return P


def tv_stand(L):
    """A scrap box with a caged TV on a square post."""
    P = D.Parts()
    P.add("Body", scrap_box((1.4, .4, .5), (0, 0, 0), seed=2))
    a, g = caged_tv((1.36, .1, .8), (0, .08, 1.05))
    P.add("Accent", a + [s.rbox((.08, .06, .2), (0, .09, .58), r=0, seg=1), s.rbox((.3, .2, .04), (0, .08, .51), r=0, seg=1)])
    P.add("Eye", g)
    return P


def reception_desk(L):
    """A counter front of mismatched plates welded askew, a plate top, a worktop on drums, a caged screen."""
    P = D.Parts()
    for i in range(4):
        tilt = (i % 3 - 1) * .03
        P.add("Body", [s.rbox((.64, .06, 1.08), (-.93 + i * .62, -.36, .54), r=.006, seg=1, rot=(0, tilt, 0))])
    for sx in (-1, 1):
        P.add("Body", [s.rbox((.06, .76, 1.08), (sx * 1.18, 0, .54), r=.006, seg=1)])
    P.add("Accent", L.slab((2.5, .3, .04), (0, -.33, 1.11)) + L.slab((2.3, .7, .04), (0, .02, .72)))
    P.add("Accent", sum([drum(.14, .7, (x, .2, 0), seg=10) for x in (-.6, .6)], []))
    a, g = caged_tv((.5, .1, .32), (.4, .15, .93))
    P.add("Eye", g + a + [s.rbox((.06, .05, .2), (.4, .19, .83), r=0, seg=1)])
    return P


def coat_rack(L):
    """A rebar pole in a tyre with spike hooks, two coats and a spiked helmet on top."""
    P = D.Parts()
    P.add("Accent", tyre(.17, .07, .05, (0, 0, .05), (0, 0, 1), m=16) + [s.rcyl(.13, .09, (0, 0, .05), seg=10, r=0),
                                                                         s.rbox((.035, .035, 1.6), (0, 0, .85), r=0, seg=1)])
    for a in (0.0, 2.1, 4.2):
        P.add("Accent", spike((0, 0, 1.55), (math.cos(a), math.sin(a), .5), .26, .02, seg=4))
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.25)) + L.cushion((.3, .12, .55), (-.08, .1, 1.27), rot=(0, 0, 2.1)))
    P.add("Body", [s.lathe([(0, 0), (.14, 0), (.14, .02), (.11, .09), (.05, .13), (0, .14)], seg=10, loc=(0, 0, 1.6))]
          + spike((0, 0, 1.72), (0, 0, 1), .08, .025, seg=4))
    return P


def lockers(L):
    """Three lopsided lockers of different heights, askew doors, padlocks."""
    P = D.Parts()
    for i, (x, h) in enumerate(((-.3, 1.9), (0, 1.84), (.3, 1.88))):
        P.add("Body", [s.rbox((.29, .48, h), (x, 0, h / 2), r=.008, seg=1, rot=(0, (i - 1) * .006, 0))])
        P.add("Body", L.door((.26, h - .14), (x, -.25, h / 2 + .02)))
        P.add("Accent", [s.rbox((.06, .03, .08), (x + .08, -.272, 1.0), r=.006, seg=1)]
              + [s.rbox((.16, .02, .02), (x, -.262, h - .2 + k * .04), r=0, seg=1) for k in range(3)])
    return P


def pallet(L):
    """A plate deck on three steel beams with oil drums on it."""
    P = D.Parts()
    P.add("Accent", L.slab((1.2, 1.0, .04), (0, 0, .13)))
    for y in (-.45, 0, .45):
        P.add("Accent", [s.rbox((1.2, .1, .02), (0, y, .01), r=0, seg=1), s.rbox((1.2, .02, .1), (0, y, .06), r=0, seg=1),
                         s.rbox((1.2, .1, .02), (0, y, .1), r=0, seg=1)])
    P.add("Body", sum([drum(.27, .4, (x, y, .15)) for x in (-.31, .31) for y in (-.22, .22)], [])
          + sum([drum(.27, .39, (-.31, y, .55)) for y in (-.22, .22)], []))
    return P


def wash_basin(L):
    """A plate counter on a welded frame, half-drum sinks, bent pipe taps, a cracked mirror hung askew."""
    P = D.Parts()
    P.add("Body", frame((1.6, .45, .82), (0, .02, 0)) + L.slab((1.6, .5, .06), (0, 0, .85))
          + [s.rbox((1.6, .03, .4), (0, .24, 1.06), r=0, seg=1)])
    for x in (-.4, .4):
        P.add("Accent", L.lathe([(0, 0), (.12, 0), (.17, .07), (.17, .09), (0, .09)], loc=(x, -.02, .82)))
        P.add("Accent", [tube([(x, .17, .87), (x, .17, 1.0), (x, .08, .99)], .016, smooth=False)])
    P.add("Eye", [s.rbox((1.4, .02, .7), (0, .235, 1.53), r=.004, seg=1, rot=(0, .03, 0))])
    P.add("Body", [s.rbox((1.5, .02, .05), (0, .245, z), r=0, seg=1, rot=(0, .03, 0)) for z in (1.17, 1.88)])
    return P


def ping_pong_table(L):
    """A riveted plate on four oil drums, a chain-link net between rebar posts."""
    P = D.Parts()
    P.add("Body", L.slab((2.74, 1.52, .04), (0, 0, .74)))
    for x in (-.95, .95):
        for y in (-.45, .45):
            P.add("Accent", drum(.2, .715, (x, y, 0), seg=12))
    P.add("Eye", [s.rbox((.02, 1.66, .15), (0, 0, .835), r=0, seg=1)])
    P.add("Accent", [s.rbox((.03, .03, .18), (0, sy * .845, .84), r=0, seg=1) for sy in (-1, 1)])
    return P


def bean_bag(L):
    """A tractor tyre lying on the floor, a fat canvas cushion stuffed into it."""
    P = D.Parts()
    P.add("Body", tyre(.3, .14, .16, (0, 0, .16), (0, 0, 1), m=20, knobs=.012))
    P.add("Body", [s.loft([(.1, .26, .26, 2.0), (.38, .34, .34, 2.0), (.6, .2, .2, 2.0), (.69, 0, 0)], seg=12)])
    P.add("Body", [s.rbox((.06, .7, .1), (.1, 0, .48), r=0, seg=1)])
    return P


def ceiling_lamp(L):
    """A welded plate with two car headlights behind cage bars."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.24, .64, .04), (0, 0, -.02), r=.006, seg=1)])
    for x in (-.33, .33):
        P.add("Accent", [s.lathe([(.15, 0), (.14, .03), (.1, .05), (0, .05)], seg=12, loc=(x, 0, -.075))])
        P.add("Eye", [s.rcyl(.13, .008, (x, 0, -.075), seg=12, r=0)])
    P.add("Accent", [s.rbox((1.2, .02, .02), (0, y, -.07), r=0, seg=1) for y in (-.3, .3)]
          + [s.rbox((.02, .62, .02), (x, 0, -.07), r=0, seg=1) for x in (-.6, 0, .6)])
    return P


def exit_sign(L):
    """A riveted plate box, a welded rim round each glowing face."""
    P = D.Parts()
    P.add("Body", [s.rbox((.38, .26, .16), (0, 0, 0), r=.01, seg=1)])
    P.add("Eye", [s.rbox((.32, .3, .1), (0, 0, 0), r=.004, seg=1)])
    for sy in (-1, 1):
        P.add("Accent", [s.rbox((.38, .02, .02), (0, sy * .15, z), r=0, seg=1) for z in (-.07, .07)]
              + [s.rbox((.02, .02, .16), (x, sy * .15, 0), r=0, seg=1) for x in (-.18, .18)])
    P.add("Accent", [s.rbox((.05, .31, .07), (-.1, 0, 0), r=0, seg=1), s.rbox((.12, .31, .02), (.05, 0, 0), r=0, seg=1)])
    return P


def hatch(L):
    """A riveted manhole lid ringed by a flat tyre, welded grip bars."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.72, .06, (0, 0, .03), seg=24, r=.008, bevel_seg=1)]
          + rivets([(math.cos(k / 10 * TAU) * .62, math.sin(k / 10 * TAU) * .62, .062) for k in range(10)], .018))
    P.add("Accent", tyre(.8, .08, .035, (0, 0, .035), (0, 0, 1), m=28, knobs=.006)
          + [s.rbox((1.0, .08, .02), (0, y, .07), r=0, seg=1) for y in (-.25, 0, .25)])
    P.add("Eye", [s.rcyl(.9, .02, (0, 0, .01), seg=28, r=0)])
    return P


PROP_DESIGNS = [desk, terminal, chair, cabinet, meeting_table, kitchen_counter, shelf, boxes, water_cooler, whiteboard,
                sofa, toilet_stall, partition, bookshelf, printer, fridge, round_table, armchair, coffee_table, tv_stand,
                reception_desk, coat_rack, lockers, pallet, wash_basin, ping_pong_table, bean_bag, ceiling_lamp,
                exit_sign, hatch]


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_plate": pressure_plate, "attach_mic": attach_mic, "attach_seismic": attach_seismic,
    "sensor_pir": pir, "security_camera": security_camera, "laser_emitter": laser_emitter,
    "attach_eye": attach_eye, "attach_nose": attach_nose,
    "safe": safe, "plant": plant, "floor_lamp": floor_lamp, "desk_lamp": desk_lamp, "server_rack": server_rack,
    "vending_machine": vending_machine, "arcade_machine": arcade_machine, "trash_bin": trash_bin,
}
DESIGNS.update({f.__name__: f for f in PROP_DESIGNS})
