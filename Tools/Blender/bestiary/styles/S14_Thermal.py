"""S14 Thermal — «Тепловизор»: heavy heat-sink machines seen through a thermal camera (Lowpoly family: flat shading).

Through the thermal filter only masses and hot spots survive, so every robot is a massive faceted block carrying a
radiator: fin stacks fanning off the back, radial fin halos and rosettes, finned cylinders like engine heads,
coolant tanks and square hoses. The hot core shows through vents (Eye). Flat-shaded, six- and eight-sided rounds,
no bevels.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import shapes as s
from lang import Lang, align
import designs as D

V = Vector


# =========================================================================== helpers (local to this style)

def bar(p0, p1, w, t, normal):
    """A flat plate from p0 to p1: width w, thickness t along `normal` (no bevel)."""
    p0, p1 = V(p0), V(p1)
    z = (p1 - p0)
    L = z.length
    z.normalize()
    y = V(normal)
    y = (y - z * y.dot(z)).normalized()
    x = y.cross(z)
    bm = s.rbox((w, t, L), r=0)
    m = Matrix((x, y, z)).transposed().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(p0 + p1) / 2, verts=bm.verts)
    return bm


def blk(size, loc, rot=(0, 0, 0), taper=(1, 1)):
    return s.rbox(size, loc, r=0, seg=1, rot=rot, taper=taper)


def prism(rings, axis="Z", loc=(0, 0, 0), n=6):
    """A faceted prism loft (n sides) through (z, a, b[, x, y]) sections."""
    return s.loft([(r[0], r[1], r[2], 2.0) + tuple(r[3:5]) for r in rings], seg=n, axis=axis, loc=loc)


def cyl(r, h, loc, axis="Z", n=8, r2=None):
    return s.rcyl(r, h, loc, axis=axis, seg=n, r=0, radius2=r2)


def fins(center, axis, n, pitch, w, h, t=0.035):
    """A stack of n plates thin along `axis` ('X', 'Y' or 'Z'), w x h in the other two axes (in xyz order)."""
    c = V(center)
    out = []
    i = "XYZ".index(axis)
    for k in range(n):
        off = (k - (n - 1) / 2) * pitch
        size = [w, h]
        size.insert(i, t)
        loc = c.copy()
        loc[i] += off
        out.append(blk(tuple(size), tuple(loc)))
    return out


def disc_fins(center, axis, n, pitch, r, t=0.03, sides=8):
    """Round fins stacked along an axis (an engine head, a finned barrel)."""
    c = V(center)
    i = "XYZ".index(axis)
    out = []
    for k in range(n):
        loc = c.copy()
        loc[i] += (k - (n - 1) / 2) * pitch
        out.append(cyl(r, t, tuple(loc), axis=axis, n=sides))
    return out


def radial_fins(center, axis, n, r0, r1, length, t=0.035, start=0.0):
    """n plates radiating around `axis` from radius r0 to r1, `length` along the axis."""
    c = V(center)
    A = V({"X": (1, 0, 0), "Y": (0, 1, 0), "Z": (0, 0, 1)}[axis])
    u = V((1, 0, 0)) if axis != "X" else V((0, 1, 0))
    w = A.cross(u)
    out = []
    for k in range(n):
        a = start + k / n * math.tau
        d = u * math.cos(a) + w * math.sin(a)
        out.append(bar(c + d * r0, c + d * r1, length, t, A.cross(d)))
    return out


def hose(points, r=0.025):
    return s.tube([tuple(p) for p in points], r, res=0, segments_per=3)


def vents(center, w, n=3, h=0.03, gap=0.05, face=-1):
    """Glowing vent slots on a -y (face=-1) or +y face."""
    c = V(center)
    return [blk((w, 0.03, h), (c.x, c.y, c.z + (k - (n - 1) / 2) * gap)) for k in range(n)]


# =========================================================================== the language

class Style(Lang):
    name = "S14_Thermal"
    seg = 8
    edge = 0.0
    soft = 0.0
    flat = True
    sharp = 20.0
    hull_kind = "block"
    limb_kind = "square"
    eye_kind = "slit"

    def r(self, size):
        return 0.0

    def rings(self, n=None):
        return 8 if n is None else max(6, min(n, 8))

    def loft(self, rings, axis="Z", loc=(0, 0, 0), round_caps=0.0, seg=None):
        return [s.loft(rings, seg=min(seg or 8, 8), axis=axis, loc=loc)]

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        return [s.ball(radius, loc, seg=6, scale=scale)]

    def wheel(self, center, radius, width):
        c = V(center)
        return [s.rcyl(radius, width, c, axis="X", seg=8, r=0.0), s.rcyl(radius * 0.55, width * 1.16, c, axis="X", seg=6, r=0.0)]

    # ------------------------------------------------------------------ furniture: flat blocks that shed heat
    leg_kind = "block"
    edge_kind = "sharp"
    cushion_puff = 0.0
    handle_kind = "bar"
    foliage_kind = "facet"

    def slab(self, size, loc, rot=(0, 0, 0)):
        return [blk(size, loc, rot=rot)]

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """A block of foam with a bevelled top: a flat-shaded wedge."""
        return [blk(size, loc, rot=rot, taper=(.92, .9))]

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        x, y, z = top
        fx, fy = x + splay[0], y + splay[1]
        fz = max(0.0, z - length)
        return [blk((thick * 1.2, thick * 1.2, z - fz), ((x + fx) / 2, (y + fy) / 2, (z + fz) / 2))]

    def carcass(self, size, loc, kick=0.06):
        """A block appliance with a radiator of fins across its back."""
        w, d, h = size
        x, y, z = loc
        out = [blk((w, d - .04, h - kick), (x, y - .02, z + kick + (h - kick) / 2))]
        n = max(2, int(w / .09))
        out += fins((x, y + d / 2 - .025, z + kick + (h - kick) * .5), "X", n, (w - .06) / max(1, n - 1), .05, (h - kick) * .8, t=.025)
        if kick > 0:
            out.append(blk((w - .04, d - .08, kick + .005), (x, y, z + (kick + .005) / 2)))
        return out

    def handle(self, loc, length=0.12, vertical=True):
        x, y, z = loc
        size = (0.024, 0.024, length) if vertical else (length, 0.024, 0.024)
        out = [blk(size, (x, y - 0.03, z))]
        for k in (-1, 1):
            dx, dz = (0, k * length * 0.4) if vertical else (k * length * 0.4, 0)
            out.append(blk((0.016, 0.04, 0.016), (x + dx, y - 0.012, z + dz)))
        return out

    def shade(self, r_bottom, r_top, height, loc):
        return [s.loft([(0, r_bottom, r_bottom, 2.0), (height, r_top, r_top, 2.0)], seg=6, loc=loc)]

    def pot(self, r, h, loc):
        return [s.loft([(0, r * .8, r * .8, 2.0), (h * .85, r, r, 2.0), (h, r * 1.05, r * 1.05, 2.0)], seg=6, loc=loc)]

    def screen(self, size, loc, face="-Y"):
        w, h = size
        x, y, z = loc
        sign = -1 if face == "-Y" else 1
        return [blk((w, 0.04, h), (x, y, z))], [blk((w - 0.05, 0.01, h - 0.05), (x, y + sign * 0.02, z))]

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Block legs tied by a heavy frame under the top."""
        out = []
        xs, ys = half_w - inset, half_d - inset
        for sx in (-1, 1):
            for sy in (-1, 1):
                out += self.leg((sx * xs, sy * ys, top_z), length, thick)
        for sy in (-1, 1):
            out.append(blk((xs * 2, .04, .08), (0, sy * ys, top_z - .04)))
        for sx in (-1, 1):
            out.append(blk((.04, ys * 2, .08), (sx * xs, 0, top_z - .04)))
        return out

    def signature(self, model, P):
        if model == "laser_emitter":
            P.add("Body", fins((0, .05, 1.2), "Z", 5, .07, .2, .16) + fins((0, .05, .4), "Z", 5, .07, .2, .16))
        if model == "sensor_lidar":
            P.add("Body", radial_fins((0, 0, .16), "Z", 6, .04, .17, .12))
        if model == "sensor_plate":
            P.add("Accent", [blk((.9, .04, .008), (0, (k - 2) * .18, .014)) for k in range(5)])
        if model == "security_camera":
            P.add("Accent", fins((0, -.02, .08), "Y", 5, .05, .14, .06))
        return P


# =========================================================================== walkers

def guard(L):
    """The reactor trooper: a faceted block torso, a fan of radiator fins rising high off the back like a crest,
    finned shoulder blocks, coolant tanks with square hoses, a hot core in the chest, a low helmet with a slit."""
    P = D.Parts()
    P.add("Body", [blk((.46, .3, .22), (0, 0, .96))])                                                  # pelvis
    P.add("Body", [blk((.34, .26, .24), (0, .01, 1.12))])                                              # waist
    P.add("Body", [prism([(1.18, .3, .2), (1.48, .4, .25), (1.72, .36, .23)], n=6)])                    # torso
    P.add("Accent", [blk((.3, .06, .24), (0, -.22, 1.42))])                                            # core frame
    P.add("Eye", vents((0, -.25, 1.42), .22, 3, .035, .065))
    # radiator crest
    P.add("Body", [blk((.5, .26, .46), (0, .27, 1.5))])
    P.add("Body", [blk((.12, .36, .7), (0, .34, 1.72))])                                               # fin spine
    for k, h in enumerate((.78, .7, .6, .5, .4)):
        y = .2 + k * .06
        P.add("Accent", [blk((.62 - k * .05, .035, h), (0, y, 1.38 + h / 2))])
    for sx in (-1, 1):
        P.add("Accent", [cyl(.08, .36, (sx * .2, .26, 1.12), n=8)])                                    # coolant tanks
        P.add("Body", [hose([(sx * .2, .3, 1.27), (sx * .34, .2, 1.4), (sx * .42, .08, 1.62)], .025)])
    P.add("Body", [cyl(.07, .14, (0, -.02, 1.73), n=6)])
    P.add("Accent", [blk((.32, .3, .22), (0, -.06, 1.84), taper=(.85, .85))])                           # helmet
    P.add("Eye", [blk((.24, .03, .05), (0, -.215, 1.83))])
    P.add("Accent", [blk((.36, .1, .05), (0, -.17, 1.94))])
    for sx in (-1, 1):
        x = sx
        P.add("Accent", [blk((.28, .34, .26), (x * .45, 0, 1.66))])                                    # shoulder
        P.add("Accent", fins((x * .45, 0, 1.84), "Y", 4, .08, .22, .14))
        P.add("Body", L.rod((x * .45, 0, 1.56), (x * .5, -.02, 1.2), .07))
        P.add("Body", [blk((.22, .26, .42), (x * .52, -.06, 1.0), rot=(math.radians(-8), 0, 0))])    # forearm
        P.add("Body", fins((x * .52, -.06, 1.02), "Z", 4, .07, .28, .3))
        P.add("Accent", [blk((.2, .22, .2), (x * .52, -.1, .72))])                                    # fist
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, [blk((.22, .24, .14), (x, 0, .9))])
        P.add(leg, [blk((.24, .26, .44), (x, 0, .67), taper=(1.1, 1.05))])
        P.add(leg, [blk((.2, .18, .16), (x, -.06, D.GUARD_KNEE))])
        P.add(leg, [blk((.2, .22, .36), (x, .0, .26), taper=(1.12, 1.1))])
        P.add(leg, [blk((.26, .38, .11), (x, -.06, .055), taper=(.9, .8))])
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """The furnace: glides on a radial-fin heat exchanger, a tall faceted body, a halo of radiator fins behind the
    head, long arms in finned gauntlets."""
    P = D.Parts()
    P.add("Body", [cyl(.2, .34, (0, 0, .26), n=8)])
    P.add("Accent", radial_fins((0, 0, .26), "Z", 12, .17, .38, .28, start=math.pi / 12))
    P.add("Eye", [cyl(.15, .04, (0, 0, .08), n=8)])
    P.add("Body", [prism([(.4, .12, .1), (.9, .16, .12), (1.3, .22, .15)], n=6)])
    P.add("Body", [prism([(1.25, .26, .17), (1.55, .38, .23), (1.82, .4, .23), (1.96, .3, .18)], n=6)])
    P.add("Accent", [blk((.26, .06, .3), (0, -.2, 1.55))])
    P.add("Eye", vents((0, -.23, 1.55), .18, 4, .03, .06))
    # head and fin halo
    P.add("Body", [cyl(.08, .14, (0, -.04, 1.97), n=6)])
    P.add("Accent", [blk((.26, .3, .22), (0, -.12, 2.02), taper=(.85, .85))])
    P.add("Eye", [blk((.2, .03, .045), (0, -.275, 2.0))])
    P.add("Body", [cyl(.14, .1, (0, .08, 2.02), axis="Y", n=8)])
    P.add("Accent", radial_fins((0, .1, 2.02), "Y", 9, .12, .38, .08, start=math.pi / 2))
    for sx in (-1, 1):
        x = sx
        P.add("Accent", [blk((.24, .3, .22), (x * .42, 0, 1.84))])
        P.add("Body", L.rod((x * .44, 0, 1.78), (x * .54, -.05, 1.4), .065))
        P.add("Body", [blk((.14, .14, .14), (x * .54, -.05, 1.4))])
        P.add("Body", L.rod((x * .54, -.05, 1.4), (x * .5, -.42, 1.08), .06))
        P.add("Accent", fins((x * .52, -.25, 1.24), "Y", 5, .07, .22, .24))
        P.add("Body", [blk((.16, .18, .18), (x * .5, -.46, 1.04))])
        for dx in (-.05, .05):
            P.add("Accent", [bar((x * .5 + dx, -.5, 1.0), (x * .5 + dx * 1.4, -.62, .8), .04, .05, (0, -1, .5))])
    return P


def hound(L):
    """The radiator dog: a faceted barrel body with a dorsal row of fins, a square hose from the jaw to the chest,
    a hot vent muzzle."""
    P = D.Parts()
    P.add("Body", [prism([(.42, .15, .15), (.15, .2, .2), (-.2, .19, .19), (-.42, .15, .15)], axis="-Y", loc=(0, 0, .83), n=6)])
    P.add("Accent", fins((0, -.02, 1.02), "Y", 7, .1, .26, .22))
    P.add("Body", [blk((.12, .3, .14), (0, -.46, .95), rot=(math.radians(-20), 0, 0))])               # neck
    P.add("Accent", [blk((.24, .3, .2), (0, -.68, 1.02), taper=(.9, .9))])                             # skull
    P.add("Accent", [blk((.18, .3, .14), (0, -.92, .95), taper=(.85, .85))])                           # muzzle
    P.add("Eye", [blk((.12, .03, .025), (0, -1.07, .97 + k * .04)) for k in range(2)])
    for sx in (-1, 1):
        P.add("Eye", [blk((.05, .03, .03), (sx * .07, -.835, 1.06))])
        P.add("Accent", [blk((.04, .12, .12), (sx * .1, -.62, 1.16), rot=(math.radians(-20), 0, 0))])
    P.add("Body", [hose([(.08, -.95, .88), (.12, -.7, .78), (.1, -.36, .75)], .025)])
    P.add("Accent", [cyl(.06, .3, (0, .5, .9), axis="Y", n=6)])                                        # tank tail
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add(leg, [blk((.13, .14, .12), (x, y, .62))])
        P.add(leg, [blk((.12, .13, .32), (x, y, .46), taper=(.8, .8))])
        P.add(leg, [blk((.1, .11, .1), (x, y, D.HOUND_KNEE))])
        P.add(leg, [blk((.08, .09, .28), (x, y, .17))])
        P.add(leg, [blk((.12, .17, .06), (x, y - .03, .03))])
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """The cooler crab: a square base carrying a block of parallel fins like a processor heat sink, a hot slot in
    front, square legs."""
    P = D.Parts()
    P.add("Body", [blk((.36, .44, .14), (0, .02, .18))])
    P.add("Accent", fins((0, .03, .3), "X", 7, .05, .38, .14))
    P.add("Body", [blk((.24, .12, .14), (0, -.24, .2))])
    P.add("Eye", [blk((.18, .03, .04), (0, -.305, .2))])
    for sx in (-1, 1):
        P.add("Eye", [blk((.04, .03, .04), (sx * .08, -.305, .25))])
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .03) + [blk((.08, .08, .08), tuple(knee))])
        P.add(f"AccentLeg{i}Lower", L.rod(knee, foot + V((0, 0, .02)), .022) + [blk((.07, .07, .04), tuple(foot))])
    return P


# =========================================================================== wheeled and tracked

def listener(L):
    """The finned ears: a block rover, a square mast with a yoke holding two finned cylinders turned sideways like
    engine heads, their hot ends glowing."""
    P = D.Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    P.add("Body", [blk((.62, 1.0, .26), (0, 0, .42), taper=(.9, .9))])
    for x, y in ((-.43, -.3), (.43, -.3), (-.43, .3), (.43, .3)):
        P.add("Body", [blk((.2, .12, .16), (x * .74, y, .25))])
        P.add("Accent", [blk((.2, .5, .05), (x * .86, y, .48))])
    P.add("Accent", fins((0, .1, .6), "Y", 5, .09, .4, .12))
    P.add("Eye", [blk((.3, .03, .05), (0, -.48, .44))])
    P.add("Body", [blk((.12, .12, .6), (0, .1, .86))])
    P.add("Body", [blk((.5, .12, .1), (0, .1, 1.16))])
    for sx in (-1, 1):
        P.add("Body", [cyl(.08, .34, (sx * .38, .1, 1.16), axis="X", n=8)])
        P.add("Accent", disc_fins((sx * .38, .1, 1.16), "X", 6, .055, .2, t=.03))
        P.add("Eye", [cyl(.07, .03, (sx * .56, .1, 1.16), axis="X", n=8)])
    return P


def worker(L):
    """The coolant hauler: a block tug with two coolant tanks and a fin stack behind the forks, the crate on the
    forks, hot vents on the bumper."""
    P = D.Parts()
    D.wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    P.add("Body", [blk((.56, .78, .22), (0, 0, .28))])
    for x, y in ((.31, .25), (-.31, .25), (.31, -.25), (-.31, -.25)):
        P.add("Body", [blk((.14, .1, .08), (x * .82, y, .13))])
    for sx in (-1, 1):
        P.add("Body", [cyl(.11, .5, (sx * .14, .24, .5), axis="Y", n=8)])
    P.add("Accent", fins((0, .24, .78), "Y", 5, .09, .44, .32))
    P.add("Body", [blk((.48, .4, .06), (0, .24, .63))])
    for sx in (-1, 1):
        P.add("Body", [blk((.06, .06, .86), (sx * .2, -.02, .78))])
        P.add("Body", [blk((.06, .5, .05), (sx * .14, -.16, .93))])
    P.add("Body", [blk((.46, .06, .1), (0, -.02, 1.18))])
    P.add("Accent", [blk((.48, .44, .4), (0, -.15, 1.15))])
    P.add("Accent", [blk((.5, .46, .05), (0, -.15, 1.32))])
    P.add("Body", [blk((.6, .1, .14), (0, -.42, .32))])
    P.add("Eye", vents((0, -.47, .32), .44, 2, .03, .06))
    return P


def sweeper(L):
    """The finned scrubber: a low faceted wedge with a radiator grille along its back, a hot exhaust slot, a slit eye
    in front, the lidar under the belly."""
    P = D.Parts()
    D.wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18)):
        P.add("Body", [blk((.12, .06, .06), (x * .82, y, .08)), blk((.08, .12, .24), (x * .66, y, .2))])
    P.add("Body", [blk((.14, .1, .14), (0, -.3, .24)), blk((.03, .06, .14), (-.05, -.3, .12)), blk((.03, .06, .14), (.05, -.3, .12))])
    P.add("Body", [prism([(.3, .38, .38), (.5, .38, .38), (.66, .26, .26)], n=8)])
    P.add("Accent", radial_fins((0, 0, .26), "Z", 16, .28, .4, .14, t=.03, start=math.pi / 16))    # fin skirt
    P.add("Accent", fins((0, .12, .68), "X", 5, .09, .34, .14))
    P.add("Eye", [blk((.3, .03, .04), (0, .375, .44))])
    P.add("Eye", [blk((.28, .03, .05), (0, -.365, .46))])
    P.add("Body", [cyl(.05, .1, (0, 0, .25), n=6)])
    P.add("AccentLidar", [cyl(.12, .07, (0, 0, .22), n=8), blk((.06, .06, .04), (0, -.12, .22))])
    for sx in (-1, 1):
        P.add("Accent", [cyl(.11, .02, (sx * .28, -.28, .02), n=6), cyl(.02, .3, (sx * .28, -.28, .17), n=6)])
    return P


def thumper(L):
    """The cooling towers: a block hull on tracks with two tall finned cooling towers at the back, a hot core grille
    in front and two hammer rams pressing the pads."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.24, 1.36, .54), (sx * .56, 0, .28)))
    P.add("Body", [blk((.96, 1.12, .44), (0, .02, .74), taper=(.92, .9))])
    P.add("Body", [blk((.6, .5, .3), (0, -.12, 1.08), taper=(.9, .9))])
    P.add("Eye", vents((0, -.36, 1.06), .4, 3, .04, .065))
    for sx in (-1, 1):
        P.add("Body", [cyl(.12, .9, (sx * .26, .32, 1.35), n=8)])
        P.add("Accent", disc_fins((sx * .26, .32, 1.36), "Z", 8, .1, .24, t=.04))
        P.add("Eye", [cyl(.08, .03, (sx * .26, .32, 1.81), n=8)])
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", [blk((.14, .36, .16), (x, -.6, .7))])
        P.add("Accent", L.rod((x, -.82, .66), (x, -.85, .1), .06))
        P.add("Accent", [blk((.26, .26, .08), (x, -.85, .08))])
        P.add("Eye", [cyl(.18, .04, (x, -.85, .02), n=8)])
    return P


def sorter(L):
    """The heat rosette: a block base, a faceted column, the big colour lens framed by a sunflower of radial fins."""
    P = D.Parts()
    P.add("Accent", [cyl(.3, .08, (0, 0, .04), n=8)])
    P.add("Body", [blk((.34, .3, .22), (0, .02, .19))])
    P.add("Body", [prism([(.28, .12, .1), (1.05, .09, .08)], n=6)])
    P.add("Body", [cyl(.24, .16, (0, .02, 1.25), axis="Y", n=8)])
    P.add("Accent", radial_fins((0, .02, 1.25), "Y", 12, .2, .42, .1, start=math.pi / 12))
    P.add("Accent", [cyl(.25, .04, (0, -.07, 1.25), axis="Y", n=8)])
    P.add("Eye", [s.lathe([(.2, 0), (.15, .03), (0, .045)], seg=8, axis="-Y", loc=(0, -.09, 1.25))])
    P.add("Body", [hose([(.05, .06, 1.1), (.15, .12, .7), (.1, .08, .26)], .022)])
    return P


# =========================================================================== devices

def drone(L):
    """The finned quad: a faceted block body with a fin stack, four finned motor stacks, a hot slit eye under the
    nose."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", [prism([(-.26, .1, .06), (-.1, .17, .09), (.2, .15, .08), (.28, .08, .05)], axis="Y", n=6)])
    P.add("Accent", fins((0, .04, .1), "Y", 5, .07, .2, .08))
    for i, (x, y) in enumerate(rot):
        P.add("Body", L.rod((x * .2, y * .2, 0), (x, y, 0), .03))
        P.add("Body", [cyl(.04, .1, (x, y, .0), n=6)])
        P.add("Accent", disc_fins((x, y, -.01), "Z", 3, .03, .075, t=.015, sides=8))
        P.add(f"AccentRotor{i}", [cyl(.025, .04, (x, y, .065), n=6), bar((x - .17, y, .065), (x + .17, y, .065), .05, .01, (0, 0, 1))])
    P.add("Accent", [blk((.12, .08, .08), (0, -.22, -.08))])
    P.add("Eye", [blk((.09, .03, .03), (0, -.265, -.09))])
    return P


def turret(L):
    """The finned gun: a block base with fins, a faceted column with a hot collar, a block head with a finned
    cooling jacket on the barrel (the head turns, the collar and base vents do not)."""
    P = D.Parts()
    P.add("Body", [blk((.6, .6, .14), (0, 0, .07))])
    P.add("Body", [prism([(.14, .18, .18), (.93, .12, .12)], n=6)])
    P.add("Accent", radial_fins((0, 0, .45), "Z", 6, .1, .3, .5))
    P.add("Body", [cyl(.14, .1, (0, 0, .96), n=8)])
    P.add("Eye", [cyl(.15, .06, (0, 0, 1.03), n=8)])
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        P.add("Eye", [blk((.1, .03, .03), (math.cos(a) * .3, math.sin(a) * .3, .15), rot=(0, 0, a + math.pi / 2))])
    P.add("AccentHead", [cyl(.1, .12, (0, 0, 1.11), n=8)])
    P.add("AccentHead", [blk((.4, .5, .26), (0, .0, 1.29))])
    P.add("AccentHead", [cyl(.05, .44, (0, -.42, 1.27), axis="Y", n=6)])
    P.add("AccentHead", disc_fins((0, -.38, 1.27), "Y", 5, .06, .11, t=.025))
    P.add("AccentHead", fins((0, .1, 1.45), "Y", 5, .07, .34, .1))
    return P


def vacuum(L):
    """A faceted ceiling puck with radial fins on its back and a hot vent ring facing the room."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.36, 0), (.36, .07), (.3, .1), (0, .1)], seg=8)])
    P.add("Accent", radial_fins((0, 0, .1), "Z", 8, .08, .28, .04, t=.03, start=math.pi / 8))
    P.add("Accent", [cyl(.1, .03, (0, 0, -.01), n=8)])
    P.add("Eye", [s.lathe([(.07, 0), (.05, .02), (0, .03)], seg=8, axis="-Z", loc=(0, 0, -.02))])
    P.add("Eye", [blk((.22, .04, .02), (0, -.25, -.005))])
    return P


# =========================================================================== furniture and level pieces

def arcade_machine(L):
    """A block cabinet crowned by a fin stack instead of a marquee, the slanted screen, a hot coin slot."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .8, 1.6), (0, .02, 0), kick=.05))
    P.add("Accent", [blk((.72, .4, .06), (0, -.38, .95), rot=(math.radians(-12), 0, 0))])
    P.add("Accent", [blk((.72, .3, .06), (0, -.08, 1.6))])
    P.add("Accent", fins((0, -.08, 1.7), "X", 7, .1, .28, .16, t=.04))
    P.add("Eye", [blk((.56, .03, .44), (0, -.39, 1.3), rot=(math.radians(-15), 0, 0))])
    P.add("Eye", [blk((.1, .03, .03), (0, -.39, .55))])
    return P


def vending_machine(L):
    """A block vending unit: the hot window, fin stacks down both flanks, a vent row over the flap."""
    P = D.Parts()
    P.add("Body", L.carcass((.86, .8, 1.9), (0, 0, 0), kick=.05))
    P.add("Eye", [blk((.6, .02, 1.1), (-.1, -.405, 1.15))])
    for sx in (-1, 1):
        P.add("Accent", fins((sx * .44, -.05, 1.2), "Z", 9, .1, .05, .6, t=.035))
    P.add("Accent", [blk((.14, .03, .5), (.33, -.41, 1.2)), blk((.6, .03, .15), (-.1, -.41, .3))])
    P.add("Eye", vents((-.1, -.41, .48), .5, 2, .025, .05))
    return P


def safe(L):
    """A heavy block safe: a thick door, a dial of radial fins, hinge blocks, a hot status slot."""
    P = D.Parts()
    P.add("Body", L.carcass((.8, .7, 1.0), (0, 0, 0), kick=.04))
    P.add("Body", [blk((.66, .04, .8), (0, -.36, .52))])
    P.add("Accent", [cyl(.06, .05, (.05, -.39, .58), axis="Y", n=8)])
    P.add("Accent", radial_fins((.05, -.39, .58), "Y", 8, .05, .14, .03, t=.025, start=math.pi / 8))
    for z in (.25, .8):
        P.add("Accent", [blk((.06, .06, .14), (-.36, -.37, z))])
    P.add("Eye", [blk((.06, .03, .06), (-.22, -.39, .66))])
    return P


def server_rack(L):
    """A heat-sink rack: a fin plate between every row of hot units across the front."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .96, 2.0), (0, .02, 0), kick=.05))
    for i in range(11):
        z = .22 + i * .16
        P.add("Eye", [blk((.5, .02, .05), (0, -.47, z))])
        P.add("Body", [blk((.62, .05, .025), (0, -.48, z + .08))])
    for x in (-.32, .32):
        P.add("Body", [blk((.04, .05, 1.88), (x, -.48, 1.0))])
    return P


def fridge(L):
    """A fridge with a compressor block on top and a coil of fins down its side."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .7, 1.66), (0, 0, 0), kick=.04))
    P.add("Body", [blk((.6, .5, .14), (0, .05, 1.73))])
    P.add("Body", [blk((.68, .022, .5), (0, -.355, 1.36)), blk((.68, .022, 1.04), (0, -.355, .58))])
    P.add("Accent", L.handle((.28, -.37, 1.3), .36) + L.handle((.28, -.37, .9), .3))
    P.add("Accent", fins((-.36, .0, .9), "Z", 12, .09, .03, .5, t=.03))
    P.add("Accent", fins((0, .05, 1.81), "X", 6, .1, .4, .03, t=.03))
    return P


def coffee_machine(L):
    """A finned boiler: a six-sided column ringed with fins, a spout, a tray, a lit gauge."""
    P = D.Parts()
    P.add("Body", [blk((.32, .34, .08), (0, 0, .04))])
    P.add("Body", [prism([(.08, .13, .13), (.5, .12, .12)], n=6)])
    P.add("Body", disc_fins((0, 0, .32), "Z", 5, .045, .165, t=.02, sides=6))
    P.add("Accent", [blk((.2, .1, .03), (0, -.17, .095)), blk((.05, .12, .04), (0, -.17, .22)), blk((.03, .03, .05), (0, -.21, .185))])
    P.add("Eye", [blk((.1, .02, .05), (0, -.11, .45))])
    return P


def water_cooler(L):
    """A finned chiller block under a six-sided bottle."""
    P = D.Parts()
    P.add("Body", L.carcass((.34, .34, 1.0), (0, 0, 0), kick=.03))
    P.add("Body", fins((0, 0, .3), "Z", 5, .06, .37, .37, t=.02))
    P.add("Body", [blk((.16, .05, .04), (0, -.17, .58))])
    P.add("Eye", [s.loft([(0, .13, .13, 2.0), (.05, .15, .15, 2.0), (.36, .15, .15, 2.0), (.44, .06, .06, 2.0), (.46, 0, 0)],
                         seg=6, loc=(0, 0, .99))])
    return P


def floor_lamp(L):
    """A radiant heater: a block base, a square pole, a six-sided reflector with fins on its back."""
    P = D.Parts()
    P.add("Body", [blk((.3, .3, .05), (0, 0, .025)), blk((.04, .04, 1.4), (0, 0, .72))])
    P.add("Accent", L.shade(.2, .1, .2, (0, 0, 1.36)))
    P.add("Accent", radial_fins((0, 0, 1.6), "Z", 6, .03, .14, .1, t=.025))
    P.add("Eye", [s.loft([(0, .14, .14, 2.0), (.02, .14, .14, 2.0)], seg=6, loc=(0, 0, 1.35))])
    return P


def desk_lamp(L):
    """A block lamp: a heavy base, a square arm, a six-sided shade with fins, a hot cube bulb."""
    P = D.Parts()
    P.add("Body", [blk((.15, .15, .04), (0, 0, .02))])
    P.add("Body", L.rod((0, .02, .03), (0, .04, .23), .014) + L.rod((0, .04, .23), (0, -.12, .32), .014))
    P.add("Accent", L.shade(.07, .035, .09, (0, -.16, .27)))
    P.add("Accent", fins((0, -.16, .37), "X", 3, .025, .05, .03, t=.012))
    P.add("Eye", [blk((.05, .05, .05), (0, -.16, .27))])
    return P


def ceiling_lamp(L):
    """A heat-sink fixture: a housing with rows of fins hanging along both long edges around the lit panel."""
    P = D.Parts()
    P.add("Body", [blk((1.24, .66, .05), (0, 0, -.025))])
    P.add("Eye", [blk((1.0, .44, .03), (0, 0, -.06))])
    for y in (-.29, .29):
        P.add("Accent", fins((0, y, -.06), "X", 12, .1, .07, .04, t=.025))
    return P


def shelf(L):
    """A block rack whose boxes rest on the shelves."""
    P = D.Parts()
    for x in (-.97, .97):
        for y in (-.27, .27):
            P.add("Body", L.leg((x, y, 2.0), 2.0, .05))
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", L.slab((2.0, .6, .04), (0, 0, h)))
    P.add("Accent", L.slab((.45, .4, .3), (-.6, 0, .618)) + L.slab((.35, .35, .25), (.5, .05, .593))
          + L.slab((.5, .45, .35), (.3, 0, 1.243)) + L.slab((.4, .4, .3), (-.55, 0, 1.818)))
    return P


def pallet(L):
    """A pallet of crates, the deck on its runners."""
    P = D.Parts()
    P.add("Accent", L.slab((1.2, 1.0, .04), (0, 0, .12)))
    for y in (-.45, 0, .45):
        P.add("Accent", L.slab((1.2, .1, .105), (0, y, .0525)))
    P.add("Body", sum([L.slab((.55, .45, .4), (x, y, .337)) for x in (-.3, .3) for y in (-.24, .24)], [])
          + sum([L.slab((.52, .42, .4), (-.3, y, .732)) for y in (-.24, .24)], []))
    return P


def plant(L):
    """A six-sided pot with a block stem and a cluster of faceted leaf masses."""
    P = D.Parts()
    P.add("Accent", L.pot(.21, .45, (0, 0, 0)))
    P.add("Body", [blk((.05, .05, .85), (0, 0, .82))])
    for (x, y, z), r in (((0, 0, .8), .28), ((.12, .1, .97), .2), ((-.12, -.1, 1.0), .2), ((.04, .02, 1.22), .18)):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)
        P.add("Body", [s.place(bm, (x, y, z), rot=(x * 3, y * 3, 0))])
    return P


def ping_pong_table(L):
    """A block table, the net sunk into the top between two posts."""
    P = D.Parts()
    P.add("Body", L.slab((2.74, 1.52, .04), (0, 0, .74)))
    P.add("Accent", L.four_legs(.72, 1.25, .7, .72, .06, inset=.25))
    P.add("Eye", [blk((.02, 1.66, .15), (0, 0, .83))])
    for y in (-.84, .84):
        P.add("Accent", [blk((.04, .04, .2), (0, y, .8))])
    return P


def armchair(L):
    """A block armchair whose arms are radiator stacks."""
    P = D.Parts()
    P.add("Body", [blk((.64, .7, .3), (0, 0, .15)), blk((.6, .62, .12), (0, -.02, .36)), blk((.6, .16, .5), (0, .27, .64), taper=(1, .7))])
    for sx in (-1, 1):
        P.add("Accent", fins((sx * .37, 0, .3), "Y", 8, .095, .12, .58, t=.04))
        P.add("Accent", [blk((.1, .74, .04), (sx * .37, 0, .02))])
    return P


def sofa(L):
    """A block sofa: a heavy base, two seat blocks, a back of upright fins, finned arms."""
    P = D.Parts()
    P.add("Body", [blk((1.84, .76, .24), (0, 0, .14)), blk((.88, .66, .14), (-.45, -.05, .33)), blk((.88, .66, .14), (.45, -.05, .33))])
    P.add("Body", [blk((1.84, .1, .5), (0, .37, .5))])
    P.add("Body", fins((0, .3, .63), "X", 17, .11, .12, .5, t=.035))
    for sx in (-1, 1):
        P.add("Accent", fins((sx * 1.0, 0, .32), "Y", 9, .1, .2, .62, t=.04))
        P.add("Accent", [blk((.2, .86, .04), (sx * 1.0, 0, .02))])
    return P


def bean_bag(L):
    """A stepped six-sided heap: tiers like a cooling slag mound."""
    P = D.Parts()
    for z0, z1, r in ((0, .24, .45), (.22, .44, .37), (.42, .6, .27), (.58, .69, .15)):
        P.add("Body", [prism([(z0, r, r / .866), (z1, r * .94, r * .94 / .866)], n=6)])
    return P


def bookshelf(L):
    """A heavy block case on a plinth, files packed like fins."""
    P = D.Parts()
    for x in (-.46, .46):
        P.add("Body", [blk((.08, .36, 2.0), (x, 0, 1.0))])
    P.add("Body", [blk((1.0, .36, .12), (0, 0, .06)), blk((1.0, .36, .06), (0, 0, 1.97))])
    for h in (.53, 1.02, 1.5):
        P.add("Body", [blk((.86, .36, .05), (0, 0, h))])
    P.add("Body", [blk((.86, .03, 1.86), (0, .165, 1.0))])
    for z0 in (.12, .555, 1.045, 1.525):
        P.add("Accent", fins((-.05, .02, z0 + .16), "X", 13, .055, .26, .32, t=.035))
    return P


def boxes(L):
    """Block crates ribbed with fins on their faces."""
    P = D.Parts()
    for (w, d, h), (x, y, z) in (((.6, .6, .5), (0, 0, .25)), ((.5, .5, .4), (.05, -.05, .7)), ((.45, .45, .4), (.55, .1, .2))):
        P.add("Accent", [blk((w, d, h), (x, y, z))])
        n = int(w / .1)
        for sy in (-1, 1):
            P.add("Accent", fins((x, y + sy * (d / 2 + .005), z), "X", n, (w - .1) / max(1, n - 1), .03, h * .7, t=.03))
    return P


def cabinet(L):
    """A block cabinet: three deep drawer blocks with vent slots, a fin grille down each side."""
    P = D.Parts()
    P.add("Body", [blk((.86, .5, 1.36), (0, 0, .7)), blk((.9, .5, .04), (0, 0, 1.38))])
    for i in range(3):
        z = .3 + i * .42
        P.add("Body", [blk((.8, .04, .38), (0, -.265, z))])
        P.add("Accent", [blk((.4, .03, .03), (0, -.29, z + .12)), blk((.4, .03, .03), (0, -.29, z + .06))])
    for sx in (-1, 1):
        P.add("Body", fins((sx * .44, 0, .7), "Z", 9, .13, .03, .4, t=.03))
    return P


def coffee_table(L):
    """A slab on a radiator block."""
    P = D.Parts()
    P.add("Body", [blk((1.0, .56, .05), (0, 0, .395))])
    P.add("Accent", fins((0, 0, .21), "X", 11, .08, .4, .32, t=.035) + [blk((.86, .44, .05), (0, 0, .025))])
    return P


def desk(L):
    """A slab top on two finned pedestals, a block monitor."""
    P = D.Parts()
    P.add("Body", [blk((1.6, .8, .05), (0, 0, .745))])
    for x in (-.6, .6):
        P.add("Accent", fins((x, 0, .37), "Y", 7, .11, .3, .7, t=.04) + [blk((.36, .74, .05), (x, 0, .025))])
    acc, glow = L.screen((.56, .34), (0, .17, .98), "-Y")
    P.add("Accent", acc + [blk((.08, .06, .2), (0, .2, .86)), blk((.26, .18, .02), (0, .2, .78))])
    P.add("Eye", glow)
    P.add("Accent", [blk((.44, .15, .02), (0, -.12, .78))])
    return P


def lockers(L):
    """Block lockers with fin vents across every door."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .5, 1.9), (0, 0, 0), kick=.06))
    for x in (-.3, 0.0, .3):
        P.add("Body", [blk((.28, .03, 1.78), (x, -.265, .98))])
        P.add("Accent", fins((x, -.29, 1.6), "Z", 5, .05, .22, .03, t=.02))
        P.add("Accent", [blk((.04, .04, .14), (x + .09, -.29, 1.0))])
    return P


def meeting_table(L):
    """A slab on two radiator pedestals."""
    P = D.Parts()
    P.add("Body", [blk((3.2, 1.3, .06), (0, 0, .75))])
    for x in (-1.1, 1.1):
        P.add("Accent", fins((x, 0, .37), "X", 6, .08, .5, .7, t=.035) + [blk((.5, .7, .04), (x, 0, .02))])
    return P


def round_table(L):
    """A six-sided top on a base of radial fins."""
    P = D.Parts()
    P.add("Body", [prism([(.72, .5, .577), (.76, .5, .577)], n=6)])
    P.add("Accent", [prism([(0, .12, .12), (.72, .08, .08)], n=6)] + radial_fins((0, 0, .36), "Z", 6, .06, .3, .7, t=.04))
    return P


def partition(L):
    """A screen of upright fins in a block frame."""
    P = D.Parts()
    P.add("Accent", [blk((1.62, .05, .06), (0, 0, 1.23)), blk((1.62, .05, .06), (0, 0, .05))])
    for x in (-.78, .78):
        P.add("Accent", [blk((.06, .05, 1.26), (x, 0, .63))])
    for k in range(15):
        P.add("Body", [blk((.025, .05, 1.14), (-.7 + k * .1, 0, .64))])
    return P


def kitchen_counter(L):
    """A block counter with a fin kick, flush doors, a block microwave with fins on top."""
    P = D.Parts()
    P.add("Body", L.carcass((2.4, .65, .88), (0, 0, 0)))
    for i in range(4):
        x = -.9 + i * .6
        P.add("Body", L.door((.58, .7), (x, -.33, .46)))
        P.add("Accent", L.handle((x + .22, -.345, .7), .1))
    P.add("Body", fins((0, -.3, .03), "X", 24, .1, .06, .05, t=.03))
    P.add("Accent", [blk((2.44, .7, .05), (0, 0, .905))])
    P.add("Eye", [blk((.5, .42, .28), (.7, .02, 1.07))])
    P.add("Body", fins((.7, .02, 1.23), "X", 5, .1, .36, .04, t=.03))
    return P


def tv_stand(L):
    """A block cabinet with a fin front, a block screen with a fin crest."""
    P = D.Parts()
    P.add("Body", L.carcass((1.4, .4, .5), (0, 0, 0), kick=.04))
    P.add("Body", fins((0, -.21, .27), "X", 13, .1, .03, .34, t=.03))
    acc, glow = L.screen((1.36, .8), (0, .06, 1.03), "-Y")
    P.add("Accent", acc + [blk((.3, .2, .04), (0, .08, .52)), blk((.08, .06, .1), (0, .09, .58))])
    P.add("Accent", fins((0, .06, 1.44), "X", 9, .14, .04, .04, t=.03))
    P.add("Eye", glow)
    return P


def toilet_stall(L):
    """Block cubicle walls on block feet with fin grilles along their tops."""
    P = D.Parts()
    for x in (-.5, .5):
        P.add("Body", [blk((.05, 1.5, 1.88), (x, 0, 1.06))])
        P.add("Body", fins((x, 0, 1.88), "Y", 9, .16, .07, .14, t=.03))
    P.add("Body", [blk((1.05, .05, 1.88), (0, .75, 1.06))])
    for x in (-.5, .5):
        for y in (-.7, 0.0):
            P.add("Body", [blk((.08, .08, .14), (x, y, .07))])
    P.add("Accent", [prism([(0, .17, .2), (.3, .2, .24), (.42, .21, .25)], n=6, loc=(0, .4, 0))])
    P.add("Accent", [blk((.36, .14, .36), (0, .66, .55))])
    return P


def whiteboard(L):
    """A block board under a header of fins, on block legs and feet."""
    P = D.Parts()
    P.add("Body", [blk((1.8, .05, 1.0), (0, 0, 1.5))])
    P.add("Accent", fins((0, 0, 1.97), "X", 13, .14, .08, .06, t=.04) + [blk((1.86, .07, .05), (0, 0, 1.0))])
    for x in (-.88, .88):
        P.add("Accent", [blk((.06, .06, 1.94), (x, 0, .97)), blk((.12, .08, .04), (x, 0, .02))])
    return P


def printer(L):
    """A block copier with a fin vent on top, a block paper tray, a hot panel."""
    P = D.Parts()
    P.add("Body", L.carcass((.6, .6, .95), (0, 0, 0), kick=.04))
    P.add("Body", fins((0, .02, 1.0), "X", 6, .09, .5, .12, t=.035))
    P.add("Accent", [blk((.36, .3, .04), (-.1, -.38, .82), rot=(math.radians(8), 0, 0)), blk((.5, .04, .3), (0, -.31, .3))])
    P.add("Eye", [blk((.16, .03, .08), (.18, -.3, .97))])
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "arcade_machine": arcade_machine,
    "vending_machine": vending_machine,
    "safe": safe,
    "server_rack": server_rack,
    "fridge": fridge,
    "coffee_machine": coffee_machine,
    "water_cooler": water_cooler,
    "floor_lamp": floor_lamp,
    "desk_lamp": desk_lamp,
    "ceiling_lamp": ceiling_lamp,
    "shelf": shelf,
    "pallet": pallet,
    "plant": plant,
    "ping_pong_table": ping_pong_table,
    "armchair": armchair,
    "sofa": sofa,
    "bean_bag": bean_bag,
    "bookshelf": bookshelf,
    "boxes": boxes,
    "cabinet": cabinet,
    "coffee_table": coffee_table,
    "desk": desk,
    "lockers": lockers,
    "meeting_table": meeting_table,
    "round_table": round_table,
    "partition": partition,
    "kitchen_counter": kitchen_counter,
    "tv_stand": tv_stand,
    "toilet_stall": toilet_stall,
    "whiteboard": whiteboard,
    "printer": printer,
}
