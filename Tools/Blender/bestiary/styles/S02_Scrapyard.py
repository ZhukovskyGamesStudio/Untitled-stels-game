"""S02 Scrapyard — «Барахольщики»: little round toys made of scrap (Toy family: glossy plastic, soft round forms).

Language: every body is a found container — a trash can, a kettle, a watering can, a paint tin, a bucket, a colander,
a teapot, a pie tin — with huge round goggle eyes (thick rim + glowing dome), coil-spring arms and necks, accordion
duct legs, big round mitts and slipper shoes, bolted patches. Proportions are toy-like: oversized heads, stubby limbs.
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


def spring(p0, p1, r, wire, turns, n=6):
    """A coil spring from p0 to p1 (coil radius r, wire radius)."""
    L = (Vector(p1) - Vector(p0)).length
    pts = []
    m = int(turns * n)
    for k in range(m + 1):
        t = k / m
        a = t * turns * TAU
        pts.append((math.cos(a) * r, math.sin(a) * r, -L / 2 + t * L))
    bm = s.tube(pts, wire, res=1, smooth=False)
    return [align(bm, p0, p1)]


def accordion(p0, p1, r0, r1, folds, seg=16):
    """A flexible duct / bellows: alternating rings from p0 to p1."""
    L = (Vector(p1) - Vector(p0)).length
    prof = [(0, -L / 2)]
    for k in range(folds * 2 + 1):
        prof.append((r1 if k % 2 else r0, -L / 2 + L * k / (folds * 2)))
    prof.append((0, L / 2))
    return [align(s.lathe(prof, seg=seg), p0, p1)]


def goggle(r, base, direction=(0, -1, 0), depth=0.05, seg=14):
    """(accent, glow): a big round toy eye — a thick rolled rim and a glowing dome bulging out of it."""
    rim = s.lathe([(r * .72, 0), (r * 1.0, 0), (r * 1.16, depth * .55), (r * 1.06, depth), (r * .8, depth * 1.02)], seg=seg)
    lens = s.lathe([(r * .86, depth * .3), (r * .84, depth + r * .12), (r * .62, depth + r * .32), (r * .3, depth + r * .42),
                    (0, depth + r * .45)], seg=seg)
    return [orient(rim, base, direction)], [orient(lens, base, direction)]


def bolted_patch(L, size, loc, rot=(0, 0, 0), normal=(0, -1, 0)):
    """A scrap patch with four bolt heads on its outer face (the face whose local axis points along `normal`)."""
    from mathutils import Euler
    out = L.box(size, loc, rot=rot, r=min(size) * .35)
    thin = min(range(3), key=lambda k: size[k])
    others = [k for k in range(3) if k != thin]
    m = Euler(rot, "XYZ").to_matrix()
    axis = Vector((0, 0, 0))
    axis[thin] = 1.0
    sign = 1 if (m @ axis).dot(Vector(normal)) > 0 else -1
    n = (m @ axis) * sign
    for a in (-1, 1):
        for b in (-1, 1):
            p = Vector((0, 0, 0))
            p[thin] = sign * size[thin] / 2 * .8
            p[others[0]] = a * (size[others[0]] / 2 - .025)
            p[others[1]] = b * (size[others[1]] / 2 - .025)
            out.append(orient(s.rcyl(.016, .016, (0, 0, .004), seg=8, r=.005, bevel_seg=1), Vector(loc) + m @ p, n))
    return out


def ring_tube(center, radius, wire, axis="Z", n=16):
    """A closed torus-like loop (handle ring, pot handle)."""
    pts = []
    for k in range(n + 1):
        a = k / n * TAU
        c, sn = math.cos(a) * radius, math.sin(a) * radius
        pts.append({"Z": (c, sn, 0), "X": (0, c, sn), "Y": (c, 0, sn)}[axis])
    bm = s.tube([tuple(Vector(p) + Vector(center)) for p in pts], wire, res=1, smooth=False)
    return [bm]


def ball_ant(L, p0, p1, r=0.04):
    """Spring antenna with a ball on top."""
    return spring(p0, p1, .025, .009, 2.5) + L.ball(r, p1)


def toy_track(L, size, loc, side):
    """A soft toy crawler: a fat rounded belt with big road wheels showing on the outer side."""
    w, d, h = size
    x, y, z = loc
    out = L.loft([(-w / 2, h / 2, d / 2, 2.6), (-w / 2 + .03, h / 2 + .015, d / 2 + .015, 2.6),
                  (w / 2 - .03, h / 2 + .015, d / 2 + .015, 2.6), (w / 2, h / 2, d / 2, 2.6)], axis="X", loc=loc)
    for k in (-1, 0, 1):
        yy = y + k * (d / 2 - h * .45)
        out.append(s.rcyl(h * .36, .05, (x + side * (w / 2 + .01), yy, z), axis="X", seg=12, r=.015, bevel_seg=1))
        out.append(s.ball(h * .12, (x + side * (w / 2 + .03), yy, z), seg=8, scale=(.5, 1, 1)))
    return out


class Style(Lang):
    name = "S02_Scrapyard"
    seg = 16
    soft = 0.7
    hull_kind = "soft"
    eye_kind = "bulb"
    wheel_kind = "solid"
    head_scale = 1.3
    limb_scale = 1.2

    def rseg(self):
        return 2

    def cyl(self, radius, depth, loc=(0, 0, 0), axis="Z", radius2=None, rot=(0, 0, 0), seg=None):
        """Soft cylinders, but thin discs and small pegs get a single bevel step (triangle budget)."""
        seg = seg or (16 if radius > .05 else 10)
        r = self.r(min(radius * 2, depth)) * 0.6
        bs = 1 if min(radius * 2, depth) < .06 else 2
        return [s.rcyl(radius, depth, loc, axis=axis, seg=seg, r=r, bevel_seg=bs, radius2=radius2, rot=rot)]

    def wheel(self, center, radius, width):
        """A chunky toy wheel: a fat round tyre and a domed hub on both sides (symmetric: it spins about its centre)."""
        c = Vector(center)
        w = width
        tyre = s.lathe([(radius * .55, -w / 2), (radius * .82, -w / 2), (radius * .97, -w * .32), (radius, 0),
                        (radius * .97, w * .32), (radius * .82, w / 2), (radius * .55, w / 2)], seg=16, axis="X", loc=c)
        hub = s.lathe([(0, -w * .6), (radius * .3, -w * .58), (radius * .58, -w * .42), (radius * .6, 0),
                       (radius * .58, w * .42), (radius * .3, w * .58), (0, w * .6)], seg=12, axis="X", loc=c)
        return [tyre, hub]

    # ------------------------------------------------------------------ furniture: crates, tins, bottle caps
    leg_kind = "round"
    edge_kind = "soft"
    cushion_puff = 1.0
    handle_kind = "knob"
    foliage_kind = "ball"

    def _edge(self, size):
        """Soft toy edges, but never so round that a cabinet turns into a pill."""
        return min(min(size) * .45, .035)

    def slab(self, size, loc, rot=(0, 0, 0)):
        """A soft board, 5 mm fuller on every side than drawn: round edges eat the contact, the fuller board keeps the
        pieces that rest on it touching (a board on the floor stays on the floor)."""
        w, d, h = size
        x, y, z = loc
        if not any(rot) and -.02 < z - h / 2 < .002:
            z = (h + .01) / 2
        return [s.rbox((w + .01, d + .01, h + .01), (x, y, z), r=self._edge(size), seg=2, rot=rot)]

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Mismatched legs tied by crate-slat aprons under the top."""
        out = super().four_legs(top_z, half_w, half_d, length, thick, inset)
        ax, ay = half_w - inset, half_d - inset
        z = top_z - .035
        for sy in (-1, 1):
            out.append(s.rbox((2 * ax, .03, .05), (0, sy * ay, z), r=.01, seg=2))
        for sx in (-1, 1):
            out.append(s.rbox((.03, 2 * ay, .05), (sx * ax, 0, z), r=.01, seg=2))
        return out

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """Mismatched legs: a stack of tins, a pipe on a ball foot or a bottle, picked by where the leg stands."""
        x, y, z = top
        L = min(length, z)
        foot = (x + splay[0], y + splay[1], z - L)
        t = max(thick, .045)
        k = int((abs(x) * 7 + abs(y) * 13) * 10 + (x < 0) + 2 * (y < 0)) % 3
        if k == 0:
            n = max(2, round(L / (.14 if L < .9 else .32)))
            prof = [(0, 0)]
            for i in range(n):
                z0, z1 = L * i / n, L * (i + 1) / n
                prof += [(t * .5, z0 + .002), (t * .72, z0 + .012), (t * .62, z0 + .03), (t * .62, z1 - .03),
                         (t * .72, z1 - .012), (t * .5, z1 - .002)]
            prof.append((0, L))
            return [s.lathe(prof, seg=12, loc=foot)]
        if k == 1:
            b = t * .7
            return [align(s.rcyl(t * .38, L - b, seg=10, r=t * .1), (foot[0], foot[1], foot[2] + b), top),
                    s.ball(b, (foot[0], foot[1], foot[2] + b), seg=12)]
        prof = [(0, 0), (t * .6, 0), (t * .64, .015), (t * .64, L * .55), (t * .38, L * .72), (t * .32, L), (0, L)]
        return [s.lathe(prof, seg=12, loc=foot)]

    def carcass(self, size, loc, kick=0.06):
        """A crate: the shared cabinet with slat bands proud around it."""
        out = super().carcass(size, loc, kick)
        w, d, h = size
        x, y, z = loc
        n = max(1, int((h - kick) / .42))
        for i in range(n):
            zz = z + kick + (h - kick) * (i + .5) / n
            out.append(s.rbox((w + .016, d + .016, .035), (x, y, zz), r=.012, seg=2))
        return out

    def door(self, size, loc, face="-Y"):
        """A plank door with a diagonal brace."""
        w, h = size
        x, y, z = loc
        out = super().door(size, loc, face)
        a = math.atan2(h, w)
        diag = math.hypot(w, h) * .8
        out.append(s.rbox((diag, .014, min(.06, h * .2)), (x, y - .016, z), r=.006, seg=1, rot=(0, -a, 0)))
        return out

    def screen(self, size, loc, face="-Y"):
        """(accent, glow): an old TV face — a fat rounded bezel, the picture standing proud of it."""
        w, h = size
        x, y, z = loc
        sign = -1 if face == "-Y" else 1
        acc = [s.rbox((w, .05, h), (x, y, z), r=min(.03, h * .2), seg=2)]
        glow = [s.rbox((w - .06, .012, h - .06), (x, y + sign * .026, z), r=.02, seg=2)]
        return acc, glow

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """Overstuffed, with a patch sewn on the front of the big ones."""
        out = super().cushion(size, loc, rot)
        w, d, h = size
        if w > .4 and h > .3 and not any(rot):
            x, y, z = loc
            out.append(s.rbox((min(.22, w * .35), .03, min(.16, h * .5)), (x + w * .2, y - d / 2 - .002, z),
                              r=.012, seg=2, rot=(0, .25, 0)))
        return out

    def handle(self, loc, length=0.12, vertical=True):
        """A bottle cap."""
        x, y, z = loc
        return [s.rcyl(.028, .02, (x, y - .004, z), axis="Y", seg=14, r=.005, bevel_seg=1)]

    def shade(self, r_bottom, r_top, height, loc):
        """A funnel shade with a rolled rim."""
        x, y, z = loc
        return [s.lathe([(r_bottom, 0), (r_bottom, .02), (r_top, height - .01), (r_top * .6, height), (0, height)],
                        seg=18, loc=loc),
                s.rcyl(r_bottom + .006, .02, (x, y, z + .012), seg=18, r=.006, bevel_seg=1)]

    def pot(self, r, h, loc):
        """A paint tin with rolled rims and a wire handle."""
        x, y, z = loc
        out = [s.lathe([(0, 0), (r * .95, 0), (r, .02), (r, h - .03), (r * 1.04, h - .02), (r * 1.04, h), (0, h)],
                       seg=18, loc=loc)]
        out.append(s.rcyl(r * 1.03, .025, (x, y, z + .05), seg=18, r=.008, bevel_seg=1))
        out.append(tube([(x - r * 1.02, y, z + h * .8), (x - r * .6, y, z + h + .12), (x + r * .6, y, z + h + .12),
                         (x + r * 1.02, y, z + h * .8)], .01))
        return out

    def signature(self, model, P):
        # devices get a spring antenna with a ball, the scrapyard's trademark
        tops = {"security_camera": ((.05, .27, .08), (.09, .3, .28)), "laser_emitter": ((0, 0, 1.58), (0, 0, 1.78))}
        if model in tops and tops[model]:
            p0, p1 = tops[model]
            P.add("Body", ball_ant(self, p0, p1, .035))
        return P


# =========================================================================== walkers

def guard(L):
    """Trash-can doorman: a ribbed bin torso with its lid as a collar, an oversized kettle head with mismatched goggle
    eyes and a carrying handle, coil-spring arms ending in big mitts, accordion duct legs in slipper shoes."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, .84), (.24, .84), (.27, .88), (.27, .98), (.29, 1.0), (.29, 1.04), (.272, 1.06), (.285, 1.2),
                           (.3, 1.22), (.3, 1.26), (.287, 1.28), (.3, 1.44), (0, 1.44)]))
    P.add("Accent", L.lathe([(0, 1.42), (.33, 1.42), (.345, 1.46), (.3, 1.52), (.17, 1.57), (0, 1.58)]))     # bin lid
    P.add("Accent", bolted_patch(L, (.2, .035, .17), (.1, -.27, 1.13), rot=(0, 0, -.32), normal=(.31, -.95, 0)))
    # the kettle head
    P.add("Body", L.loft([(1.52, .2, .17, 2.0, 0, .07), (1.62, .35, .27, 2.0, 0, .07), (1.84, .39, .29, 2.0, 0, .07),
                          (2.0, .33, .25, 2.0, 0, .07), (2.11, .17, .14, 2.0, 0, .07), (2.13, 0, 0, 2.0, 0, .07)]))
    P.add("Accent", L.cyl(.13, .04, (0, .07, 2.12)) + L.ball(.05, (0, .07, 2.16)))
    P.add("Accent", [tube([(-.27, .07, 1.98), (-.2, .07, 2.27), (.2, .07, 2.27), (.27, .07, 1.98)], .026, res=1)])
    P.eye(goggle(.145, (-.12, -.15, 1.87), (-.25, -1, .05), .06))
    P.eye(goggle(.09, (.18, -.13, 1.92), (.4, -1, .1), .05))
    P.add("Accent", [tube([(-.1, -.2, 1.68), (0, -.225, 1.66), (.1, -.2, 1.68)], .016, res=1)])          # smile
    for sx in (-1, 1):
        P.add("Accent", L.ball(.085, (sx * .3, 0, 1.36)))
        P.add("Accent", spring((sx * .33, 0, 1.33), (sx * .43, -.05, 1.0), .045, .014, 6))
        P.add("Body", L.ball(.12, (sx * .44, -.06, .9), scale=(1, 1, 1.1)) + L.ball(.05, (sx * .37, -.15, .95)))
    P.add("Accent", ball_ant(L, (.16, .26, 1.3), (.24, .34, 1.98), .045))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.1, (x, 0, .9)))
        P.add(leg, accordion((x, 0, .9), (x, 0, .12), .085, .105, 7))
        P.add(leg, L.loft([(-.1, .07, .05, 2.4), (0, .115, .085, 2.4), (.14, .125, .085, 2.2), (.25, .09, .055, 2.0)],
                          axis="-Y", loc=(x, .02, .085), round_caps=.035))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """Propane-tank stalker on a unicycle: a tall gas bottle on one fat wheel, an upturned cooking pot for a head
    with one huge eye and feeler springs, long hose arms ending in salad-tong claws."""
    P = D.Parts()
    P.add("Body", L.wheel((0, 0, .34), .34, .16))
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .1, 0, .34), (sx * .12, 0, .8), .035))
    P.add("Body", L.loft([(.62, .16, .15), (.72, .36, .32), (.95, .46, .4), (1.3, .46, .4), (1.55, .37, .33),
                          (1.7, .16, .15)], round_caps=.05))
    for z, r in ((.9, .465), (1.42, .44)):
        P.add("Accent", L.loft([(z - .025, r, r * .87, 2.0), (z + .025, r, r * .87, 2.0)]))
    P.add("Accent", L.cyl(.14, .12, (0, 0, 1.73)))
    P.add("Accent", bolted_patch(L, (.22, .04, .24), (-.12, -.38, 1.15), rot=(0, 0, .3), normal=(-.3, -.95, 0)))
    # the pot head
    P.add("Body", L.lathe([(0, 1.7), (.41, 1.7), (.42, 1.75), (.36, 1.78), (.36, 2.06), (.29, 2.14), (0, 2.16)],
                          loc=(0, .1, 0)))
    for sx in (-1, 1):
        P.add("Accent", ring_tube((sx * .38, .1, 1.98), .08, .02, axis="Y"))
        P.add("Accent", ball_ant(L, (sx * .1, .1, 2.12), (sx * .26, .14, 2.36), .05))
    P.eye(goggle(.17, (0, -.2, 1.93), (0, -1, -.05), .07))
    # long hose arms with tong claws
    for sx in (-1, 1):
        P.add("Accent", L.ball(.12, (sx * .42, 0, 1.5)))
        hand = Vector((sx * .56, -.5, .98))
        P.add("Body", [tube([(sx * .44, 0, 1.5), (sx * .72, -.06, 1.36), (sx * .72, -.28, 1.1), tuple(hand)], .065, res=1)])
        P.add("Accent", L.ball(.075, tuple(hand)))
        for k in (-1, 1):
            P.add("Accent", [tube([tuple(hand), tuple(hand + Vector((k * .09, -.12, -.03))),
                                     tuple(hand + Vector((k * .035, -.26, -.09)))], .026, res=1)])
    return P


def hound(L):
    """Watering-can hound: the can is the dog, its long spout is the sniffing snout with the rose as the glowing nose,
    the top handle arches over its back, goggle eyes on top, tin-lid ears, stubby pipe legs on round paws."""
    P = D.Parts()
    P.add("Body", L.loft([(-.4, .14, .14, 2.2), (-.3, .19, .2, 2.4), (0, .21, .22, 2.4), (.3, .19, .21, 2.4),
                          (.42, .15, .17, 2.2)], axis="-Y", loc=(0, 0, .82), round_caps=.05))
    for y in (-.2, .22):
        P.add("Accent", L.loft([(-.025, .222, .232, 2.4), (.025, .222, .232, 2.4)], axis="-Y", loc=(0, y, .82)))
    P.add("Accent", [tube([(0, -.32, .92), (0, -.58, .87), (0, -.9, .79)], .07, res=2, radii=[1, .7, .55])])
    P.add("Accent", L.lathe([(.035, 0), (.075, .05), (.085, .08), (0, .085)], axis="-Y", loc=(0, -.89, .785)))
    P.add("Eye", L.cyl(.072, .016, (0, -.976, .785), axis="Y"))
    P.add("Accent", [tube([(0, .36, .96), (0, .26, 1.24), (0, .02, 1.25), (0, -.1, 1.02)], .032, res=1)])
    for sx in (-1, 1):
        P.eye(goggle(.085, (sx * .1, -.28, .96), (sx * .35, -1, .55), .05))
        P.add("Accent", L.cyl(.14, .03, (sx * .25, -.18, .86), axis="X", rot=(0, sx * .7, 0)))
    P.add("Accent", ball_ant(L, (0, .36, .86), (0, .6, 1.04), .045))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add(leg, L.ball(.095, (x, y, .6)))
        P.add(leg, L.rod((x, y, .6), (x, y, D.HOUND_KNEE), .07))
        P.add(leg, L.ball(.085, (x, y, D.HOUND_KNEE)))
        P.add(leg, L.rod((x, y, D.HOUND_KNEE), (x, y, .06), .062))
        P.add(leg, L.ball(.1, (x, y - .03, .06), scale=(1.1, 1.35, .62)))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """Colander spider: an upturned colander dome with a long pan handle for a tail, two big goggle eyes on the rim,
    bent-wire legs with ball knees and ball feet."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, .1), (.25, .1), (.26, .13), (.245, .2), (.2, .29), (.11, .345), (0, .36)], loc=(0, .04, 0)))
    P.add("Body", L.cyl(.29, .035, (0, .04, .125)))
    for k in range(10):  # colander dimples
        a = k / 10 * TAU
        P.add("Body", L.ball(.025, (math.cos(a) * .2, .04 + math.sin(a) * .2, .265)))
    P.add("Body", L.box((.075, .3, .035), (0, .45, .14), taper=(.8, 1)) + ring_tube((0, .65, .14), .055, .018))
    for sx in (-1, 1):
        P.eye(goggle(.075, (sx * .09, -.17, .25), (sx * .3, -1, .55), .045))
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .025) + L.ball(.045, knee))
        P.add(f"AccentLeg{i}Lower", L.rod(knee, foot + Vector((0, 0, .03)), .02) + L.ball(.04, foot + Vector((0, 0, .03))))
    return P


# =========================================================================== wheeled

def listener(L):
    """Gramophone pram: a paint tin lying on a little cart with four fat toy wheels, an accordion neck and two huge
    gramophone horns for ears, their throats glowing."""
    P = D.Parts()
    W = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .22, .12))
        P.add("Accent", L.rod((c[0] * .7, c[1], .22), (c[0] * 1.02, c[1], .22), .045))
    P.add("Accent", L.box((.66, .82, .1), (0, 0, .27)))
    P.add("Body", L.lathe([(0, -.32), (.27, -.32), (.3, -.29), (.3, .29), (.27, .32), (0, .32)], axis="X", loc=(0, 0, .58)))
    for x in (-.33, .33):
        P.add("Accent", L.cyl(.285, .03, (x, 0, .58), axis="X"))
    P.add("Accent", bolted_patch(L, (.22, .035, .16), (.06, -.3, .56), normal=(0, -1, 0)))
    P.add("Accent", L.box((.26, .05, .05), (0, -.3, .7), r=.02))
    P.add("Body", accordion((0, .06, .84), (0, .06, 1.1), .07, .09, 3))
    P.add("Accent", L.ball(.1, (0, .06, 1.12)))
    for sx in (-1, 1):
        o, d, Lh = Vector((sx * .04, .06, 1.13)), Vector((sx, 0, .75)).normalized(), .42
        prof = [(.05, 0)] + [(.05 + .17 * (k / 6) ** 2.4, Lh * k / 6) for k in range(1, 7)] + [(.235, Lh + .015), (.2, Lh + .02)]
        P.add("Body", [orient(s.lathe(prof, seg=20), o, d)])
        P.add("Eye", [orient(s.rcyl(.17, .02, (0, 0, Lh + .01), seg=20, r=.004), o, d)])
    return P


def worker(L):
    """Kettle porter: a round kettle on a little wheeled tray carries a fruit crate over its head with two stubby arms
    (a spring under the crate keeps the balance), its spout is a headlamp."""
    P = D.Parts()
    W = [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .12, .1))
    P.add("Body", L.box((.5, .68, .07), (0, 0, .13)))
    P.add("Body", L.lathe([(0, .14), (.22, .14), (.26, .2), (.27, .36), (.25, .55), (.19, .68), (.1, .74), (0, .75)],
                          loc=(0, .02, 0)))
    P.add("Accent", L.cyl(.275, .04, (0, .02, .3)))
    P.add("Accent", [tube([(0, -.2, .36), (0, -.33, .43), (0, -.39, .5)], .045, res=1, radii=[1, .8, .7])])
    P.add("Accent", L.cyl(.05, .04, (0, -.4, .5), axis="Y"))
    P.add("Eye", L.cyl(.04, .02, (0, -.42, .5), axis="Y"))
    for sx in (-1, 1):
        P.eye(goggle(.075, (sx * .1, -.19, .56), (sx * .25, -1, .2), .045))
        P.add("Body", [tube([(sx * .2, 0, .6), (sx * .32, -.05, .8), (sx * .3, -.13, .96)], .04, res=1)])
        P.add("Body", L.ball(.07, (sx * .29, -.14, .99)))
    P.add("Accent", spring((0, .02, .73), (0, -.1, .96), .06, .016, 3))
    # fruit crate: bottom board, slats, corner posts, the load inside
    c = (0, -.15)

    def slat(size, loc):
        return [s.rbox(size, loc, r=.01, seg=1)]
    P.add("Accent", slat((.5, .42, .03), (c[0], c[1], .965)))
    for z in (1.05, 1.17, 1.29):
        for y in (-.36, .06):
            P.add("Accent", slat((.5, .03, .08), (0, y, z)))
        for x in (-.24, .24):
            P.add("Accent", slat((.03, .42, .08), (x, c[1], z)))
    for x in (-.235, .235):
        for y in (-.355, .055):
            P.add("Accent", slat((.05, .05, .4), (x, y, 1.15)))
    P.add("Accent", L.box((.44, .36, .3), (0, c[1], 1.12), r=.03))
    return P


def sweeper(L):
    """Mop bucket: an upturned bucket with a mop fringe for a skirt, goggle eyes and a handle hoop on top; the lidar
    spins under its belly between the fringe."""
    P = D.Parts()
    W = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .08, .06))
        for sx in (-1, 1):
            P.add("Accent", L.rod((c[0] + sx * .046, c[1], .08), (c[0] + sx * .046, c[1], .34), .015))
    P.add("Body", L.lathe([(0, .3), (.355, .3), (.37, .33), (.35, .36), (.32, .6), (.33, .62), (.33, .65), (.31, .67),
                           (0, .67)]))
    for z in (.44, .52):
        P.add("Body", L.cyl(.345 - (z - .36) * .12, .025, (0, 0, z)))
    for k in range(22):
        a = (k + .5) / 22 * TAU
        c, sn = math.cos(a), math.sin(a)
        strand = s.rcyl(.028, .34, seg=6, r=0, radius2=.02)
        P.add("Accent", [align(strand, (c * .32, sn * .32, .37), (c * .41, sn * .41, .05))])
    P.add("Accent", [tube([(-.3, 0, .6), (-.24, 0, .86), (.24, 0, .86), (.3, 0, .6)], .02, res=1)])
    for sx in (-1, 1):
        P.add("Accent", L.ball(.04, (sx * .31, 0, .6)))
        P.eye(goggle(.08, (sx * .1, -.3, .52), (sx * .25, -1, .2), .045))
    P.add("Accent", L.cyl(.045, .13, (0, 0, .3)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.08, .05, .04), (0, -.12, .22)))
    return P


def thumper(L):
    """Plunger stomper: a fat oil drum on toy tracks with goggle eyes, stamping two big plungers onto the floor
    (their rubber cups ring the glowing seismic pads); a chimney and a spring antenna on top."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", toy_track(L, (.24, 1.34, .52), (sx * .56, 0, .26), sx))
    P.add("Body", L.lathe([(0, -.45), (.36, -.45), (.38, -.43), (.38, .43), (.36, .45), (0, .45)], axis="X", loc=(0, .05, .82)))
    for x in (-.25, .25):
        P.add("Accent", L.cyl(.395, .045, (x, .05, .82), axis="X"))
    for sx in (-1, 1):
        P.eye(goggle(.1, (sx * .15, -.27, .95), (sx * .2, -1, .25), .05))
        x = sx * .3
        P.add("Accent", L.rod((x, -.56, .84), (x, -.85, .18), .035))
        P.add("Accent", L.lathe([(.17, 0), (.17, .02), (.14, .08), (.08, .14), (.04, .17), (0, .18)], loc=(x, -.85, .02)))
        P.add("Eye", L.cyl(.18, .02, (x, -.85, .012)))
        P.add("Body", [tube([(sx * .36, -.18, .66), (sx * .36, -.45, .6), (x, -.62, .64)], .05, res=1)])
        P.add("Body", L.ball(.075, (x, -.66, .64)))
    P.add("Accent", bolted_patch(L, (.24, .035, .18), (.05, -.33, .72), normal=(0, -1, 0)))
    P.add("Body", L.cyl(.075, .3, (.2, .25, 1.25)) + L.cyl(.1, .05, (.2, .25, 1.41)))
    P.add("Accent", ball_ant(L, (-.2, .25, 1.1), (-.24, .3, 1.55), .05))
    return P


# =========================================================================== tall, flying, mounted

def sorter(L):
    """Jack-in-the-box: a toy box with a crank, a long coil spring and a round head that is one giant colour lens."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.3, .04), (.26, .07), (0, .07)]))
    P.add("Body", L.box((.46, .46, .34), (0, 0, .23)))
    P.add("Accent", L.box((.5, .5, .05), (0, 0, .4)))
    P.add("Accent", L.rod((.22, 0, .25), (.33, 0, .25), .025) + L.rod((.33, 0, .25), (.33, -.1, .32), .022)
          + L.ball(.04, (.33, -.11, .33)))
    P.add("Accent", spring((0, 0, .4), (0, 0, 1.0), .12, .025, 7))
    P.add("Body", L.ball(.27, (0, .03, 1.2)))
    P.eye(goggle(.2, (0, -.14, 1.24), (0, -1, 0), .06, seg=28))
    P.add("Accent", ball_ant(L, (.1, .05, 1.42), (.16, .08, 1.58), .04))
    return P


def drone(L):
    """Teapot copter: a squat teapot with its spout as a nose and a goggle eye under it, four bent-spoon arms to
    paddle propellers."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, -.11), (.19, -.11), (.25, -.07), (.27, 0), (.23, .07), (.15, .1), (0, .11)]))
    P.add("Body", L.cyl(.1, .02, (0, 0, .1)) + L.ball(.025, (0, 0, .1)))
    P.add("Body", [tube([(0, -.2, -.02), (0, -.35, .02), (0, -.42, .08)], .045, res=1, radii=[1, .8, .6])])
    P.add("Body", [tube([(0, .22, .06), (0, .35, .05), (0, .35, -.05), (0, .22, -.07)], .022, res=1)])
    P.add("Body", L.cyl(.275, .03, (0, 0, -.02)))
    P.eye(goggle(.085, (0, -.21, -.05), (0, -1, -.4), .04))
    for i, (x, y) in enumerate([(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]):
        P.add("Body", [tube([(x * .4, y * .4, 0), (x * .75, y * .75, -.02), (x, y, .0)], .026, res=1)])
        P.add("Body", L.ball(.04, (x, y, .01)))
        rot = [s.rcyl(.035, .089, (x, y, .0645), seg=12, r=.01)]
        rot += L.box((.34, .07, .016), (x, y, .075), rot=(.25, 0, math.radians(45 if x * y > 0 else -45)), r=.007)
        P.add(f"AccentRotor{i}", rot)
    return P


def turret(L):
    """Pop-gun on a broomstick tripod: a paint-tin mount with a face (its glowing eyes stay still), a sideways tin
    for a head with a fat cork-plugged barrel that turns."""
    P = D.Parts()
    for k in range(3):
        a = math.pi / 2 + k * TAU / 3
        foot = (math.cos(a) * .4, math.sin(a) * .4, .05)
        P.add("Body", L.rod((math.cos(a) * .07, math.sin(a) * .07, .9), foot, .04) + L.ball(.065, foot))
    P.add("Body", L.lathe([(0, .8), (.13, .8), (.15, .83), (.15, 1.09), (.13, 1.12), (0, 1.12)]))
    P.add("Body", L.cyl(.155, .03, (0, 0, .87)))
    P.add("Eye", L.cyl(.155, .03, (0, 0, 1.05)))
    for sx in (-1, 1):
        acc, glow = goggle(.05, (sx * .06, -.12, .96), (sx * .2, -1, 0), .035)
        P.add("Body", acc).add("Eye", glow)
    P.add("AccentHead", L.cyl(.08, .12, (0, 0, 1.15)))
    P.add("AccentHead", L.lathe([(0, -.2), (.15, -.2), (.17, -.17), (.17, .17), (.15, .2), (0, .2)], axis="Y",
                                loc=(0, .04, 1.31)))
    P.add("AccentHead", L.cyl(.175, .035, (0, .13, 1.31), axis="Y") + L.cyl(.175, .035, (0, -.05, 1.31), axis="Y"))
    P.add("AccentHead", L.cyl(.09, .3, (0, -.28, 1.3), axis="Y") + L.cyl(.105, .06, (0, -.42, 1.3), axis="Y"))
    P.add("AccentHead", L.cyl(.08, .1, (0, -.49, 1.3), axis="Y", radius2=.06))
    return P


def vacuum(L):
    """Pie-tin vacuum: a crimped pie tin stuck to the ceiling, one big goggle eye looking down, a bolted patch."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.32, 0), (.355, .03), (.36, .08), (.3, .1), (0, .1)], seg=24))
    for k in range(18):
        a = k / 18 * TAU
        P.add("Accent", L.ball(.03, (math.cos(a) * .35, math.sin(a) * .35, .06)))
    P.eye(goggle(.09, (0, 0, .012), (0, 0, -1), .03))
    P.add("Accent", bolted_patch(L, (.16, .14, .02), (.18, .1, .002), normal=(0, 0, -1)))
    for sx in (-1, 1):
        P.add("Accent", L.ball(.035, (sx * .2, -.16, .0), scale=(1, 1, .6)))
    return P


# =========================================================================== devices and sensor modules

def lidar(L):
    """A little bucket with a spring post, a glowing collar (still) and a tin-can head that spins."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.17, 0), (.18, .03), (.14, .12), (0, .12)]))
    P.add("Body", L.cyl(.025, .3, (0, 0, .25)) + spring((0, 0, .1), (0, 0, .37), .045, .012, 5))
    P.add("Body", L.cyl(.08, .02, (0, 0, .36)))
    P.add("Eye", L.cyl(.085, .022, (0, 0, .381)))
    P.add("AccentHead", L.lathe([(0, .39), (.09, .39), (.1, .405), (.1, .5), (.09, .515), (0, .515)]))
    P.add("AccentHead", L.box((.09, .04, .045), (0, -.095, .455), r=.012) + L.ball(.03, (0, 0, .52)))
    return P


def pir(L):
    """A jar lid on the wall with one goggle eye."""
    P = D.Parts()
    P.add("Body", L.cyl(.08, .05, (0, .045, 0), axis="Y") + L.cyl(.085, .02, (0, .02, 0), axis="Y"))
    P.eye(goggle(.055, (0, .015, 0), (0, -1, 0), .025))
    return P


def plate(L):
    """The shared pressure plate, riveted like a scrap patch."""
    P = D.plate(L)
    k = .47
    for t in (-1, -.5, 0, .5, 1):
        for x, y in ((t * k, k), (t * k, -k), (k, t * k), (-k, t * k)):
            P.add("Accent", [s.rcyl(.022, .008, (x, y, .014), seg=10, r=.003, bevel_seg=1)])
    return P


def security_camera(L):
    """A tin can on a spring arm, a lid on the wall, a goggle lens."""
    P = D.Parts()
    P.add("Body", L.cyl(.1, .06, (0, .27, 0), axis="Y"))
    P.add("Accent", spring((0, .26, 0), (0, .09, -.02), .035, .011, 4) + L.cyl(.02, .18, (0, .17, -.01), axis="Y"))
    P.add("Accent", L.lathe([(0, -.15), (.08, -.15), (.09, -.13), (.09, .1), (.08, .12), (0, .12)], axis="-Y",
                            loc=(0, -.03, -.02)))
    for y in (-.1, .05):
        P.add("Accent", L.cyl(.095, .02, (0, y, -.02), axis="Y"))
    P.eye(goggle(.055, (0, -.14, -.02), (0, -1, 0), .03))
    return P


def laser_emitter(L):
    """A post of stacked tins with the emitter strip down its front."""
    P = D.Parts()
    prof = [(0, 0), (.1, 0), (.1, .06), (.06, .09)]
    for k in range(5):
        z = .1 + k * .3
        prof += [(.06, z), (.066, z + .02), (.066, z + .05), (.06, z + .07)]
    prof += [(.06, 1.58), (0, 1.6)]
    P.add("Body", L.lathe(prof, seg=16))
    P.add("Eye", L.box((.05, .04, 1.4), (0, -.065, .85), r=.008))
    return P


def attach_eye(L):
    P = D.Parts()
    P.add("Accent", L.ball(.09, (0, .03, 0), scale=(1.15, .65, 1.15)))
    P.eye(goggle(.07, (0, .0, 0), (0, -1, 0), .035))
    return P


def attach_mic(L):
    """A tiny gramophone horn on a spring stem."""
    P = D.Parts()
    P.add("Accent", L.cyl(.045, .02, (0, 0, .01)) + spring((0, 0, .03), (0, 0, .25), .022, .008, 3)
          + L.cyl(.012, .27, (0, 0, .14), seg=6))
    P.add("Body", L.lathe([(.02, .25), (.03, .3), (.045, .34), (.068, .37), (.07, .38), (.06, .375)]))
    P.add("Eye", L.cyl(.055, .01, (0, 0, .37)))
    return P


def attach_seismic(L):
    """A little plunger: wooden stick, rubber cup glowing on the floor."""
    P = D.Parts()
    P.add("Accent", L.cyl(.08, .03, (0, 0, .3)) + L.cyl(.018, .22, (0, 0, .19)))
    P.add("Eye", L.lathe([(.04, 0), (.04, .015), (.03, .06), (.015, .09), (0, .1)]))
    return P


def attach_nose(L):
    """A watering-can spout with its rose."""
    P = D.Parts()
    P.add("Accent", L.lathe([(.075, 0), (.06, .06), (.045, .16), (.05, .2), (.07, .24), (0, .25)], axis="-Y"))
    P.add("Eye", L.cyl(.06, .012, (0, -.257, 0), axis="Y"))
    return P


# =========================================================================== furniture with the scrapyard's own take

def arcade_machine(L):
    """Crate arcade: a crate, an old round-cornered TV and a small crate stacked into a cabinet, a slanted plank
    control panel with a joystick and bottle-cap buttons."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .8, .9), (0, 0, 0), kick=.04))
    P.add("Body", [s.rbox((.7, .6, .62), (0, .1, 1.215), r=.12, seg=2)])
    P.add("Body", L.carcass((.6, .5, .3), (0, .14, 1.5), kick=0))
    P.add("Eye", [s.rbox((.5, .03, .4), (0, -.2, 1.25), r=.06, seg=2)])
    P.add("Accent", [s.rbox((.72, .34, .05), (0, -.37, .915), r=.02, seg=2, rot=(math.radians(-12), 0, 0))])
    P.add("Accent", L.rod((-.16, -.4, .95), (-.16, -.43, 1.06), .012) + L.ball(.035, (-.16, -.43, 1.07)))
    for k in range(3):
        P.add("Accent", [s.rcyl(.03, .03, (.04 + k * .1, -.42, .962), seg=12, r=.006, bevel_seg=1, rot=(math.radians(-12), 0, 0))])
    return P


def vending_machine(L):
    """Gumball machine: a crate base with a crank and a chute, a huge glass globe on top (the glowing window)."""
    P = D.Parts()
    P.add("Body", L.carcass((.88, .78, 1.0), (0, -.01, 0), kick=.05))
    P.add("Accent", [s.rcyl(.3, .06, (0, -.01, 1.02), seg=20, r=.015, bevel_seg=2)])
    P.add("Eye", [s.ball(.41, (0, -.01, 1.43), seg=24)])
    P.add("Accent", [s.lathe([(0, 0), (.16, 0), (.17, .02), (.12, .05), (0, .06)], seg=18, loc=(0, -.01, 1.82)),
                     s.ball(.025, (0, -.01, 1.885), seg=10)])
    P.add("Accent", [s.rbox((.3, .04, .24), (0, -.405, .62), r=.03, seg=2),
                     s.rcyl(.08, .03, (0, -.425, .62), axis="Y", seg=16, r=.008, bevel_seg=1),
                     s.rbox((.2, .02, .03), (0, -.432, .62), r=.01, seg=1),
                     s.rbox((.22, .04, .12), (0, -.405, .25), r=.02, seg=2)])
    return P


def safe(L):
    """Strongbox: a braced crate with a bottle-cap dial and a padlock on a hasp."""
    P = D.Parts()
    P.add("Body", L.carcass((.8, .7, 1.0), (0, 0, 0), kick=.04))
    P.add("Body", L.door((.66, .82), (0, -.36, .52)))
    dial = [s.rcyl(.1, .05, (0, -.39, .6), axis="Y", seg=16, r=.012, bevel_seg=1)]
    for k in range(10):
        a = k / 10 * TAU
        dial.append(s.ball(.016, (math.cos(a) * .1, -.39, .6 + math.sin(a) * .1), seg=8))
    P.add("Accent", dial)
    P.add("Accent", [s.rbox((.05, .03, .14), (-.24, -.375, .44), r=.01, seg=1), s.rbox((.12, .05, .11), (-.24, -.395, .33), r=.02, seg=2),
                     tube([(-.28, -.395, .37), (-.28, -.395, .43), (-.2, -.395, .43), (-.2, -.395, .37)], .012, smooth=False)])
    P.add("Eye", [s.ball(.025, (.27, -.375, .86), seg=10)])
    return P


def plant(L):
    """A lollipop tree of round balls on a stick, in a paint tin."""
    P = D.Parts()
    P.add("Accent", L.pot(.2, .42, (0, 0, 0)))
    P.add("Body", L.rod((0, 0, .38), (0, 0, .95), .035))
    P.add("Body", [s.ball(.2, (sx * .14, 0, .95), seg=16) for sx in (-1, 1)] + [s.ball(.2, (0, sy * .15, 1.0), seg=16)
                                                                              for sy in (-1, 1)]
          + [s.ball(.22, (0, 0, 1.18), seg=16)])
    return P


def _tin_pole(z0, z1, r, n, x=0.0, y=0.0):
    """A pole of n stacked tins from z0 to z1."""
    prof = [(0, 0)]
    L = z1 - z0
    for i in range(n):
        a, b = L * i / n, L * (i + 1) / n
        prof += [(r, a + .003), (r * 1.25, a + .012), (r * 1.25, b - .012), (r, b - .003)]
    prof.append((0, L))
    return s.lathe(prof, seg=12, loc=(x, y, z0))


def floor_lamp(L):
    """A bucket base, a pole of stacked tins, an upturned funnel for a shade over the glowing disc."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.16, 0), (.16, .02), (.12, .1), (0, .1)], seg=18), _tin_pole(.09, 1.38, .025, 6)])
    P.add("Accent", L.shade(.2, .045, .28, (0, 0, 1.37)))
    P.add("Eye", [s.rcyl(.14, .02, (0, 0, 1.36), seg=16, r=.004, bevel_seg=1)])
    return P


def desk_lamp(L):
    """A tin base, a spring arm, a little tin can for a shade."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.075, .04, (0, 0, .02), seg=16, r=.01, bevel_seg=1)] + spring((0, 0, .03), (0, -.01, .22), .025, .007, 4)
          + L.ball(.025, (0, -.01, .23)) + L.rod((0, -.01, .23), (0, -.11, .32), .01))
    tin = s.lathe([(0, 0), (.052, 0), (.058, .01), (.058, .11), (.062, .12), (.05, .12)], seg=16)
    P.add("Accent", [orient(tin, Vector((0, -.12, .34)), (0, -.35, -1))])
    P.add("Eye", [s.ball(.03, (0, -.16, .25), seg=10)])
    return P


def trash_bin(L):
    """A ribbed trash can with a domed lid and a handle (the guard's family)."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.15, 0), (.16, .02), (.16, .08), (.175, .09), (.175, .12), (.163, .13), (.17, .24),
                           (.185, .25), (.185, .28), (.172, .29), (.18, .39), (0, .39)]))
    P.add("Accent", L.lathe([(0, .38), (.195, .38), (.2, .4), (.17, .415), (.08, .425), (0, .427)])
          + [tube([(-.06, 0, .42), (-.04, 0, .44), (.04, 0, .44), (.06, 0, .42)], .01, smooth=False)])
    return P


def coffee_machine(L):
    """A kettle on a hot plate with a mug in front."""
    P = D.Parts()
    P.add("Body", [s.rbox((.34, .4, .06), (0, 0, .03), r=.02, seg=2)])
    P.add("Body", L.lathe([(0, 0), (.12, 0), (.14, .04), (.145, .15), (.12, .25), (.06, .3), (0, .31)], loc=(0, .03, .055)))
    P.add("Body", [tube([(0, -.07, .16), (0, -.15, .24), (0, -.18, .3)], .028, radii=[1, .8, .6])])
    P.add("Accent", [tube([(0, .12, .3), (0, .1, .47), (0, -.03, .47), (0, -.06, .33)], .016)] + L.ball(.03, (0, .03, .375)))
    P.add("Accent", L.lathe([(0, 0), (.045, 0), (.048, .09), (0, .09)], loc=(0, -.2, .055)))
    P.add("Eye", [s.rbox((.06, .02, .025), (.1, -.2, .03), r=.006, seg=1)])
    return P


# --------------------------------------------------------------------------- the scrapyard's construction kit

def planks(size, loc, n=None):
    """A top of planks running along x with gaps, tied by two battens underneath."""
    w, d, h = size
    x, y, z = loc
    n = n or max(2, round(d / .15))
    gap = .014
    pw = (d - gap * (n - 1)) / n
    out = [s.rbox((w, pw, h), (x, y - d / 2 + pw / 2 + i * (pw + gap), z), r=min(.01, h * .3), seg=1) for i in range(n)]
    for bx in (-.36, .36):
        out.append(s.rbox((.06, d - .04, .03), (x + bx * w, y, z - h / 2 - .012), r=0, seg=1))
    return out


def crate(size, loc, inner=True):
    """A slatted wooden crate standing on the floor (loc = centre of its footprint): corner posts, slats with gaps on
    every side, the load inside."""
    w, d, h = size
    x, y, z = loc
    out = []
    if inner:
        out.append(s.rbox((w - .05, d - .05, h - .008), (x, y, z + h / 2), r=.01, seg=1))
    for sx in (-1, 1):
        for sy in (-1, 1):
            out.append(s.rbox((.056, .056, h), (x + sx * (w / 2 - .028), y + sy * (d / 2 - .028), z + h / 2), r=0, seg=1))
    n = max(2, int(h / .17))
    sh = h / n * .66
    for i in range(n):
        zz = z + h * (i + .5) / n
        out.append(s.rbox((w - .07, .026, sh), (x, y - (d / 2 - .013), zz), r=0, seg=1))
        for sx in (-1, 1):
            out.append(s.rbox((.026, d - .07, sh), (x + sx * (w / 2 - .013), y, zz), r=0, seg=1))
    return out


def open_crate(size, loc):
    """A crate turned on its side as a cubby: back, sides, bottom and top boards, open to the front (-y)."""
    w, d, h = size
    x, y, z = loc
    t = .03
    return [s.rbox((w, t, h), (x, y + d / 2 - t / 2, z + h / 2), r=.008, seg=1),
            s.rbox((t, d, h), (x - w / 2 + t / 2, y, z + h / 2), r=.008, seg=1),
            s.rbox((t, d, h), (x + w / 2 - t / 2, y, z + h / 2), r=.008, seg=1),
            s.rbox((w, d, t), (x, y, z + t / 2), r=.008, seg=1), s.rbox((w, d, t), (x, y, z + h - t / 2), r=.008, seg=1)]


def tin(r, h, loc, seg=18):
    """A paint tin / barrel: rolled rims top and bottom and a hoop round the middle."""
    x, y, z = loc
    return [s.lathe([(0, 0), (r * .96, 0), (r, .015), (r, h - .015), (r * .96, h), (0, h)], seg=seg, loc=loc),
            s.rcyl(r * 1.035, .025, (x, y, z + .03), seg=seg, r=.008, bevel_seg=1),
            s.rcyl(r * 1.035, .025, (x, y, z + h - .03), seg=seg, r=.008, bevel_seg=1),
            s.rcyl(r * 1.02, .02, (x, y, z + h * .5), seg=seg, r=.006, bevel_seg=1)]


def tv(size, loc, face=-1):
    """(body, glow): an old television — a fat rounded box, a bulging picture, two bottle-cap knobs."""
    w, d, h = size
    x, y, z = loc
    body = [s.rbox((w, d, h), (x, y, z), r=min(.08, h * .25), seg=2)]
    body += [s.rcyl(.025, .02, (x + w / 2 - .06, y + face * (d / 2 + .004), z + k * .07), axis="Y", seg=10, r=.004, bevel_seg=1)
             for k in (-1, 1)]
    glow = [s.rbox((w - .16, .02, h - .1), (x - .04, y + face * d / 2, z), r=min(.05, h * .2), seg=2)]
    return body, glow


# =========================================================================== every prop as scrap furniture

def desk(L):
    """A plank top on two crates, an old TV for a monitor."""
    P = D.Parts()
    P.add("Body", planks((1.6, .8, .05), (0, 0, .74)))
    for x in (-.56, .56):
        P.add("Accent", crate((.44, .76, .7), (x, 0, 0)))
    b, g = tv((.5, .3, .4), (0, .17, .945))
    P.add("Accent", b + [s.rbox((.44, .15, .02), (0, -.12, .775), r=.008, seg=1)]).add("Eye", g)
    return P


def terminal(L):
    """A plank console on crates with three old TVs and a keyboard."""
    P = D.Parts()
    P.add("Body", planks((1.6, .8, .05), (0, 0, .74)))
    for x in (-.56, .56):
        P.add("Accent", crate((.44, .76, .7), (x, 0, 0)))
    for x in (-.36, 0, .36):
        b, g = tv((.32, .26, .37), (x, .17, .945))
        P.add("AccentConsole", b).add("Eye", g)
    P.add("AccentConsole", [s.rbox((.52, .17, .025), (0, -.12, .778), r=.008, seg=1)])
    return P


def chair(L):
    """A paint-tin stool with a pillow and a plank back on two tin posts."""
    P = D.Parts()
    P.add("Accent", tin(.25, .42, (0, 0, .01)))
    P.add("Body", L.cushion((.52, .52, .1), (0, 0, .47)))
    for sx in (-1, 1):
        P.add("Accent", [_tin_pole(.36, 1.0, .028, 3, sx * .14, .19)])
    P.add("Body", [s.rbox((.56, .07, .32), (0, .22, .9), r=.03, seg=2, rot=(-.12, 0, 0))])
    return P


def server_rack(L):
    """Four crates stacked to the ceiling, bottle-cap lamps blinking on their fronts."""
    P = D.Parts()
    for i in range(4):
        P.add("Body", crate((.7, 1.0, .5), (0, 0, i * .5)))
        for k in range(3):
            P.add("Eye", [s.rcyl(.03, .04, (-.2 + k * .2, -.495, i * .5 + .25), axis="Y", seg=10, r=.004, bevel_seg=1)])
    return P


def cabinet(L):
    """Three crates stacked as drawers, each with a bottle-cap pull."""
    P = D.Parts()
    for i in range(3):
        P.add("Body", crate((.9, .5, .47), (0, -.01, i * .465)))
        P.add("Accent", [s.rcyl(.035, .03, (0, -.27, i * .465 + .25), axis="Y", seg=12, r=.006, bevel_seg=1)])
    return P


def meeting_table(L):
    """A long plank top on three barrels."""
    P = D.Parts()
    P.add("Body", planks((3.2, 1.3, .06), (0, 0, .75)))
    for x in (-1.15, 0, 1.15):
        P.add("Accent", tin(.32, .705, (x, 0, 0)))
    return P


def kitchen_counter(L):
    """Four crates under a plank top, an old radio-microwave glowing on it."""
    P = D.Parts()
    for i in range(4):
        P.add("Body", crate((.6, .64, .88), (-.9 + i * .6, 0, 0)))
    P.add("Accent", planks((2.44, .7, .05), (0, 0, .905), n=4))
    P.add("Eye", [s.rbox((.5, .4, .32), (.7, .02, 1.09), r=.07, seg=2)])
    P.add("Accent", [s.rcyl(.03, .03, (.88, -.19, 1.1 + k * .08), axis="Y", seg=10, r=.005, bevel_seg=1) for k in (-1, 1)])
    return P


def shelf(L):
    """Plank shelves on four poles of stacked tins, crates on the shelves."""
    P = D.Parts()
    for x in (-.96, .96):
        for y in (-.27, .27):
            P.add("Body", [_tin_pole(0, 2.0, .03, 4, x, y)])
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", planks((2.0, .6, .04), (0, 0, h), n=3))
    P.add("Accent", crate((.45, .4, .3), (-.6, 0, .47)) + crate((.35, .35, .25), (.5, .05, .47))
          + crate((.5, .45, .35), (.3, 0, 1.07)) + crate((.4, .4, .3), (-.55, 0, 1.67)))
    return P


def boxes(L):
    """Three crates."""
    P = D.Parts()
    P.add("Accent", crate((.6, .6, .5), (0, 0, 0)) + crate((.5, .5, .4), (.05, -.05, .5)) + crate((.45, .45, .4), (.545, .1, 0)))
    return P


def water_cooler(L):
    """A crate stand with bottle-cap taps under a big glass jug."""
    P = D.Parts()
    P.add("Body", crate((.34, .34, .99), (0, 0, 0)))
    P.add("Body", [s.rcyl(.025, .03, (sx * .06, -.18, .8), axis="Y", seg=10, r=.005, bevel_seg=1) for sx in (-1, 1)])
    P.add("Eye", L.lathe([(0, 0), (.06, 0), (.06, .06), (.15, .12), (.16, .32), (.13, .44), (0, .48)], loc=(0, 0, .965)))
    return P


def whiteboard(L):
    """A chalkboard between two tin-stack poles standing on bottle caps."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.72, .03, 1.0), (0, 0, 1.5), r=.01, seg=1)])
    P.add("Accent", [s.rbox((1.78, .05, .05), (0, 0, z), r=.012, seg=2) for z in (1.0, 2.0)])
    for x in (-.9, .9):
        P.add("Accent", [_tin_pole(.02, 1.98, .025, 6, x, 0), s.rcyl(.04, .025, (x, 0, .0125), seg=12, r=.006, bevel_seg=1)])
    return P


def sofa(L):
    """A bathtub sofa: an old tub on ball feet, pillows heaped inside."""
    P = D.Parts()
    P.add("Accent", [s.rbox((2.1, .84, .58), (0, 0, .37), r=.2, seg=3), s.rbox((2.19, .86, .07), (0, 0, .64), r=.03, seg=2)])
    P.add("Accent", [s.ball(.07, (sx * .85, sy * .3, .06), seg=10) for sx in (-1, 1) for sy in (-1, 1)])
    P.add("Body", L.cushion((.9, .6, .16), (-.45, -.06, .7)) + L.cushion((.9, .6, .16), (.45, -.06, .7))
          + L.cushion((1.8, .22, .4), (0, .26, .68), rot=(math.radians(-8), 0, 0)))
    return P


def toilet_stall(L):
    """A cubicle of upright planks on tin feet with a bucket for a toilet."""
    P = D.Parts()
    for sx in (-1, 1):
        for k in range(8):
            P.add("Body", [s.rbox((.04, .2, 1.88), (sx * .5 + (k % 2) * .006 * sx, -.66 + k * .19, 1.06), r=.008, seg=1)])
    for k in range(6):
        P.add("Body", [s.rbox((.19, .04, 1.88), (-.42 + k * .17, .755 + (k % 2) * .006, 1.06), r=.008, seg=1)])
    for sx in (-1, 1):
        for y in (-.7, 0.0, .7):
            P.add("Body", tin(.05, .13, (sx * .5, y, 0), seg=10)[:1])
    P.add("Accent", tin(.2, .42, (0, .4, 0)))
    P.add("Accent", tin(.14, .3, (0, .62, .45))[:2])
    return P


def partition(L):
    """A picket fence with two rails."""
    P = D.Parts()
    for k in range(13):
        x = -.78 + k * .13
        P.add("Body", [s.rbox((.1, .03, 1.1), (x, 0, .56), r=.008, seg=1),
                       s.loft([(1.1, .05, .015, 8.0), (1.26, 0, 0)], seg=4, loc=(x, 0, 0))])
    P.add("Accent", [s.rbox((1.62, .05, .06), (0, 0, z), r=.012, seg=2) for z in (.3, .95)])
    return P


def bookshelf(L):
    """Four crates turned on their sides as cubbies, stuffed with folders."""
    P = D.Parts()
    for i in range(4):
        P.add("Body", open_crate((1.0, .35, .5), (0, 0, i * .5)))
        x = -.44
        for k in range(6 + i % 2):
            w = .07 + .03 * ((k + i) % 3)
            hh = .3 - .04 * ((k * 7 + i) % 3)
            P.add("Accent", [s.rbox((w, .26, hh), (x + w / 2, .02, i * .5 + .03 + hh / 2), r=.008, seg=1)])
            x += w + .012
    return P


def fridge(L):
    """A round-shouldered old fridge on tin feet with big lever handles and a bottle-cap badge."""
    P = D.Parts()
    P.add("Body", [s.rbox((.7, .68, 1.74), (0, .01, .93), r=.14, seg=3)])
    P.add("Body", [s.rbox((.62, .04, .44), (0, -.345, 1.48), r=.06, seg=2), s.rbox((.62, .04, 1.08), (0, -.345, .66), r=.06, seg=2)])
    P.add("Body", [s.rcyl(.05, .07, (sx * .26, sy * .24, .035), seg=10, r=.008, bevel_seg=1) for sx in (-1, 1) for sy in (-1, 1)])
    P.add("Accent", [s.rbox((.05, .05, .3), (.24, -.37, 1.36), r=.02, seg=2), s.rbox((.05, .05, .3), (.24, -.37, 1.0), r=.02, seg=2),
                     s.rcyl(.05, .02, (0, -.37, 1.65), axis="Y", seg=14, r=.005, bevel_seg=1)])
    return P


def round_table(L):
    """A cable-spool table."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.5, .05, (0, 0, .735), seg=24, r=.012, bevel_seg=2), s.rcyl(.42, .05, (0, 0, .025), seg=24, r=.012, bevel_seg=2)])
    P.add("Accent", [s.rcyl(.2, .68, (0, 0, .38), seg=20, r=.01, bevel_seg=1)])
    P.add("Accent", [s.rcyl(.205, .03, (0, 0, z), seg=20, r=.006, bevel_seg=1) for z in (.25, .5)])
    return P


def armchair(L):
    """A barrel chair: a barrel cut open at the front, pillows inside."""
    P = D.Parts()
    barrel = s.lathe([(0, 0), (.38, 0), (.42, .3), (.42, .55), (.39, .87), (0, .87)], seg=20)
    s.place(barrel, scale=(1, .9, 1))
    barrel = s.boolean(barrel, s.rbox((1.0, .5, .6), (0, -.2, .79), r=0, seg=1))
    P.add("Accent", [barrel] + [s.place(s.rcyl(.425, .03, (0, 0, z), seg=20, r=.006, bevel_seg=1), scale=(1, .9, 1))
                                for z in (.12, .4)])
    P.add("Body", L.cushion((.62, .5, .14), (0, -.06, .52)) + L.cushion((.62, .16, .32), (0, .2, .68)))
    return P


def coffee_table(L):
    """A crate lying on the floor with a plank lid."""
    P = D.Parts()
    P.add("Accent", crate((.92, .5, .37), (0, 0, 0)))
    P.add("Body", planks((1.0, .55, .04), (0, 0, .4), n=3))
    return P


def tv_stand(L):
    """Two crates under a big old TV with knobs."""
    P = D.Parts()
    for x in (-.35, .35):
        P.add("Body", crate((.68, .4, .5), (x, 0, 0)))
    b, g = tv((1.3, .34, .96), (0, 0, .97))
    P.add("Accent", b).add("Eye", g)
    return P


def reception_desk(L):
    """A wall of crates for the counter, a plank top, a worktop on barrels behind, an old TV and a desk bell."""
    P = D.Parts()
    for i in range(4):
        P.add("Body", crate((.59, .3, 1.08), (-.9 + i * .6, -.32, 0)))
    for sx in (-1, 1):
        P.add("Body", crate((.3, .5, 1.08), (sx * 1.05, .15, 0)))
    P.add("Accent", planks((2.5, .3, .04), (0, -.33, 1.1), n=2) + planks((2.3, .7, .04), (0, .02, .74), n=4))
    for x in (-.5, .5):
        P.add("Accent", tin(.08, .7, (x, .28, 0), seg=12)[:1])
    b, g = tv((.42, .3, .36), (.4, .15, .93))
    P.add("Eye", g + b)
    P.add("Accent", [s.lathe([(0, 0), (.05, 0), (.05, .01), (.03, .04), (0, .05)], seg=12, loc=(-.6, -.38, 1.12))])
    return P


def coat_rack(L):
    """A broom handle in a paint tin, bent-fork hooks, two coats and a hat."""
    P = D.Parts()
    P.add("Accent", tin(.22, .2, (0, 0, 0)) + [s.rcyl(.022, 1.62, (0, 0, .97), seg=10, r=.005, bevel_seg=1)])
    for a in (0.0, 2.1, 4.2):
        P.add("Accent", [tube([(0, 0, 1.6), (math.cos(a) * .16, math.sin(a) * .16, 1.62), (math.cos(a) * .22, math.sin(a) * .22, 1.72)],
                              .012)])
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.3)) + L.cushion((.3, .12, .55), (-.08, .1, 1.32), rot=(0, 0, 2.1)))
    P.add("Body", [s.lathe([(0, 0), (.1, 0), (.1, .012), (.06, .02), (.06, .08), (0, .09)], seg=14, loc=(0, 0, 1.71))])
    return P


def lockers(L):
    """Three tall crates side by side, braced doors and bottle-cap pulls."""
    P = D.Parts()
    for x in (-.3, 0, .3):
        P.add("Body", crate((.3, .5, 1.9), (x, 0, 0)))
        P.add("Body", L.door((.24, 1.7), (x, -.262, .97)))
        P.add("Accent", [s.rcyl(.03, .03, (x + .08, -.28, 1.0), axis="Y", seg=12, r=.006, bevel_seg=1)])
    return P


def pallet(L):
    """A plank pallet stacked with crates."""
    P = D.Parts()
    P.add("Accent", planks((1.2, 1.0, .04), (0, 0, .13), n=6))
    for y in (-.45, 0, .45):
        P.add("Accent", [s.rbox((1.2, .1, .1), (0, y, .05), r=.01, seg=1)])
    P.add("Body", sum([crate((.55, .45, .4), (x, y, .15)) for x in (-.3, .3) for y in (-.24, .24)], [])
          + sum([crate((.55, .45, .38), (-.3, y, .55)) for y in (-.24, .24)], []))
    return P


def wash_basin(L):
    """Two crates under a plank counter, two buckets for sinks, a round-cornered mirror."""
    P = D.Parts()
    for x in (-.4, .4):
        P.add("Body", crate((.78, .45, .82), (x, .02, 0)))
    P.add("Body", planks((1.6, .5, .06), (0, 0, .85), n=3) + [s.rbox((1.6, .03, .4), (0, .24, 1.06), r=.01, seg=1)])
    for x in (-.4, .4):
        P.add("Accent", tin(.16, .12, (x, -.02, .82), seg=14)[:2])
        P.add("Accent", L.rod((x, .17, .88), (x, .17, 1.0), .02) + L.rod((x, .17, 1.0), (x, .08, .99), .015))
    P.add("Eye", [s.rbox((1.5, .02, .76), (0, .235, 1.53), r=.2, seg=3)])
    P.add("Body", [s.rbox((1.56, .025, .82), (0, .245, 1.53), r=.22, seg=3)])
    return P


def ping_pong_table(L):
    """A plank top on four barrels, a net between tin posts."""
    P = D.Parts()
    P.add("Body", planks((2.74, 1.52, .04), (0, 0, .74), n=8))
    for x in (-.95, .95):
        for y in (-.45, .45):
            P.add("Accent", tin(.2, .705, (x, y, 0), seg=14))
    P.add("Eye", [s.rbox((.02, 1.66, .15), (0, 0, .835), r=.004, seg=1)])
    P.add("Accent", [s.rcyl(.025, .16, (0, sy * .845, .835), seg=10, r=.005, bevel_seg=1) for sy in (-1, 1)])
    return P


def printer(L):
    """A crate copier: the paper tray a plank, the glowing panel a bottle-cap row."""
    P = D.Parts()
    P.add("Body", crate((.6, .6, .95), (0, 0, 0)) + [s.rbox((.56, .52, .14), (0, .02, 1.0), r=.04, seg=2)])
    P.add("Accent", [s.rbox((.36, .3, .03), (-.1, -.35, .82), r=.01, seg=1, rot=(math.radians(8), 0, 0)),
                     s.rbox((.06, .2, .06), (-.1, -.25, .78), r=.01, seg=1)])
    P.add("Eye", [s.rbox((.16, .03, .08), (.18, -.29, .97), r=.02, seg=2)])
    return P


def bean_bag(L):
    """A stuffed sack tied at the top, with a patch."""
    P = D.Parts()
    P.add("Body", [s.loft([(0, .42, .42, 2.0), (.18, .45, .45, 2.0), (.42, .36, .36, 2.0), (.56, .14, .14, 2.0), (.6, .08, .08, 2.0)],
                          seg=20, round_caps=.06)])
    P.add("Body", [s.ball(.06, (0, 0, .6), seg=10)] + [s.loft([(0, .05, .03, 2.0), (.1, 0, 0)], seg=6, loc=(0, 0, .6))])
    P.add("Body", [s.rbox((.18, .03, .14), (.2, -.4, .26), r=.012, seg=2, rot=(.35, 0, .45))])
    return P


def ceiling_lamp(L):
    """A plank board on the ceiling with three tin-can shades, their lids glowing."""
    P = D.Parts()
    P.add("Body", planks((1.24, .64, .035), (0, 0, -.018), n=4))
    for x in (-.4, 0, .4):
        P.add("Accent", tin(.11, .05, (x, 0, -.08), seg=16)[:1])
        P.add("Eye", [s.rcyl(.1, .006, (x, 0, -.081), seg=16, r=0)])
    return P


def exit_sign(L):
    """A tin sign box with rolled rims round both glowing faces."""
    P = D.Parts()
    P.add("Body", [s.rbox((.38, .26, .16), (0, 0, 0), r=.04, seg=2)])
    P.add("Eye", [s.rbox((.34, .3, .12), (0, 0, 0), r=.03, seg=2)])
    P.add("Accent", [s.rbox((.05, .31, .07), (-.1, 0, 0), r=.01, seg=1), s.rbox((.12, .31, .02), (.05, 0, 0), r=.006, seg=1)])
    return P


def hatch(L):
    """A manhole lid crimped like a giant bottle cap, a bottle-cap grip in the middle."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.75, .06, (0, 0, .03), seg=32, r=.015, bevel_seg=2)])
    P.add("Accent", [s.ball(.035, (math.cos(k / 18 * TAU) * .72, math.sin(k / 18 * TAU) * .72, .055), seg=8) for k in range(18)]
          + [s.rcyl(.12, .03, (0, 0, .07), seg=16, r=.008, bevel_seg=1)])
    P.add("Eye", [s.rcyl(.9, .02, (0, 0, .01), seg=32, r=0)])
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_pir": pir, "sensor_plate": plate, "security_camera": security_camera,
    "laser_emitter": laser_emitter, "attach_eye": attach_eye, "attach_mic": attach_mic,
    "attach_seismic": attach_seismic, "attach_nose": attach_nose,
    "arcade_machine": arcade_machine, "vending_machine": vending_machine, "safe": safe, "plant": plant,
    "floor_lamp": floor_lamp, "desk_lamp": desk_lamp, "trash_bin": trash_bin, "coffee_machine": coffee_machine,
}
PROP_DESIGNS = [desk, terminal, chair, server_rack, cabinet, meeting_table, kitchen_counter, shelf, boxes, water_cooler,
                whiteboard, sofa, toilet_stall, partition, bookshelf, fridge, round_table, armchair, coffee_table, tv_stand,
                reception_desk, coat_rack, lockers, pallet, wash_basin, ping_pong_table, bean_bag, ceiling_lamp, exit_sign,
                hatch, printer]
DESIGNS.update({f.__name__: f for f in PROP_DESIGNS})
