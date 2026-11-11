"""S05 Toon — «Мультяшный рейд»: chunky cartoon stone golems and rock critters (Lowpoly family: flat-shaded facets).

Language: every mass is a faceted boulder (a jittered icosphere — no two alike), cut-stone slabs, hexagonal crystals,
low-poly stone discs for wheels and props; the glow is in rune slits cut into the stone and in gems. Proportions
are pushed hard: huge shoulders and fists, tiny heads sunk between them, stumpy pillar legs, stone props instead
of machinery.
"""
import math
import random

import bmesh
from mathutils import Vector

import shapes as s
from lang import Lang, align
import designs as D

TAU = math.tau


# --------------------------------------------------------------------------- helpers of this style

def orient(bm, origin, direction):
    """Turn a shape built along +Z (base at z = 0) so +Z points along direction, then move its base to origin."""
    d = Vector(direction).normalized()
    q = Vector((0, 0, 1)).rotation_difference(d)
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(origin), verts=bm.verts)
    return bm


def _seed(loc, salt=0):
    x, y, z = loc
    return int(abs(x * 1000) * 7 + abs(y * 1000) * 13 + abs(z * 1000) * 17 + (x < 0) * 5 + salt)


def rock(size, loc, rot=(0, 0, 0), jitter=.14, floor=None, salt=0):
    """A faceted boulder of full size (w, d, h) centred at loc; floor = clamp the bottom flat at that z.
    The icosphere is turned at random first, so no two boulders show the same dice-like pattern."""
    rng = random.Random(_seed(loc, salt))
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    s.place(bm, rot=(rng.uniform(0, TAU), rng.uniform(0, TAU), rng.uniform(0, TAU)))
    for v in bm.verts:
        k = 1 + rng.uniform(-jitter, jitter)
        v.co = Vector((v.co.x * size[0] / 2 * k, v.co.y * size[1] / 2 * k, v.co.z * size[2] / 2 * k))
    s.place(bm, loc, rot)
    if floor is not None:
        for v in bm.verts:
            if v.co.z < floor:
                v.co.z = floor
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=.002)
    return [bm]


def slab(size, loc, rot=(0, 0, 0), taper=(1.0, 1.0)):
    """A cut stone block (hard edges)."""
    return [s.rbox(size, loc, r=0, seg=1, rot=rot, taper=taper)]


def crystal(base, direction, length, r, seg=6):
    """A pointed hexagonal crystal standing on base."""
    bm = s.loft([(0, r * .8, r * .8, 2.0), (length * .15, r, r, 2.0), (length * .7, r, r, 2.0), (length, 0, 0)], seg=seg)
    return [orient(bm, base, direction)]


def rune(p0, p1, w=.035, depth=.04):
    """A glowing rune slit from p0 to p1 (half sunk into the stone)."""
    L = (Vector(p1) - Vector(p0)).length
    return [align(s.rbox((w, depth, L), r=0, seg=1), p0, p1)]


def gem(r, base, direction=(0, -1, 0), h=None):
    """A faceted gem dome (glow)."""
    h = h or r * .6
    bm = s.loft([(0, r, r, 2.0), (h * .5, r * .8, r * .8, 2.0), (h, 0, 0)], seg=7)
    return [orient(bm, base, direction)]


def disc(r, depth, loc, axis="Z", seg=8):
    return [s.rcyl(r, depth, loc, axis=axis, seg=seg, r=0)]


def chunky_limb(p0, p1, r0, r1, n=2, salt=0):
    """A limb of n overlapping boulders from p0 to p1."""
    a, b = Vector(p0), Vector(p1)
    out = []
    for k in range(n):
        t = (k + .5) / n
        c = a.lerp(b, t)
        r = r0 + (r1 - r0) * t
        L = (b - a).length / n * 1.35
        bm = rock((r * 2, r * 2, L), (0, 0, 0), salt=salt + k)[0]
        out.append(align(bm, c - (b - a).normalized() * L / 2, c + (b - a).normalized() * L / 2))
    return out


class Style(Lang):
    name = "S05_Toon"
    seg = 8
    soft = 0.0
    flat = True
    sharp = 1.0
    hull_kind = "facet"
    limb_kind = "square"
    eye_kind = "slit"
    wheel_kind = "facet"

    # furniture: chunky faceted stone and wood (every prop has its own design; these serve the leftovers)
    leg_kind = "block"
    edge_kind = "sharp"
    handle_kind = "ring"
    foliage_kind = "facet"

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """A boulder in the shape of the cushion."""
        bm = rock(tuple(v * .94 for v in size), (0, 0, 0), jitter=.05)[0]
        return [s.place(bm, loc, rot)]

    def wheel(self, center, radius, width):
        """A stone wheel: an octagonal disc with a hexagonal hub boss on both sides."""
        c = Vector(center)
        return [s.rcyl(radius, width, c, axis="X", seg=8, r=0, rot=(math.pi / 8, 0, 0)),
                s.rcyl(radius * .45, width * 1.25, c, axis="X", seg=6, r=0)]


# =========================================================================== walkers

def guard(L):
    """Boulder golem: a massive chest boulder under two huge shoulder rocks, a tiny head sunk between them with one rune
    slit, long arms of stacked stones ending in giant fists, pillar legs on slab feet, runes on the chest."""
    P = D.Parts()
    P.add("Body", rock((.4, .32, .3), (0, 0, 1.0)))
    P.add("Body", rock((.82, .56, .62), (0, -.03, 1.42), rot=(.15, 0, 0)))
    for sx in (-1, 1):
        P.add("Accent", rock((.46, .5, .42), (sx * .46, 0, 1.64), rot=(0, sx * .3, 0)))
        P.add("Body", chunky_limb((sx * .56, 0, 1.5), (sx * .62, -.04, 1.2), .11, .12, 1, salt=3))
        P.add("Body", rock((.3, .32, .4), (sx * .65, -.07, 1.0)))
        P.add("Accent", rock((.42, .44, .38), (sx * .68, -.12, .72)))
    P.add("Body", rock((.24, .24, .24), (0, -.04, 1.68), jitter=.05))
    P.add("Body", rock((.22, .22, .22), (0, -.08, 1.83), jitter=.05))
    P.add("Eye", slab((.15, .04, .035), (0, -.195, 1.85)))
    P.add("Eye", rune((-.14, -.32, 1.56), (0, -.34, 1.36)) + rune((.14, -.32, 1.56), (0, -.34, 1.36))
          + rune((0, -.34, 1.36), (0, -.31, 1.22)))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        # stacked boulders, wider than tall, not a smooth pin
        for (sz, z) in (((.26, .26, .2), .9), ((.32, .31, .22), .74), ((.29, .3, .21), .58), ((.24, .24, .18), D.GUARD_KNEE),
                        ((.28, .28, .22), .31), ((.27, .27, .2), .17)):
            P.add(leg, rock(sz, (x, 0, z), jitter=.05 if z > .85 else .12))
        P.add(leg, slab((.3, .4, .12), (x, -.07, .06), taper=(.8, .7)))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """Spinning-top monolith: a huge chest of rock tapering into an upside-down crystal whose point stands on a glowing
    rune disc (it glides on it), long stone arms with crystal claws, a tiny head with a rune visor and two horns."""
    P = D.Parts()
    P.add("Eye", disc(.36, .03, (0, 0, .015)))
    P.add("Accent", disc(.28, .05, (0, 0, .055), seg=8))
    P.add("Body", [orient(s.loft([(0, .02, .02, 2.0), (.5, .16, .14, 2.0), (1.0, .26, .2, 2.0), (1.25, .28, .22, 2.0)], seg=6),
                          Vector((0, 0, .06)), (0, 0, 1))])
    P.add("Body", rock((.8, .55, .6), (0, -.04, 1.6), rot=(.2, 0, 0)))
    for sx in (-1, 1):
        P.add("Accent", rock((.4, .44, .36), (sx * .42, -.02, 1.84), rot=(0, sx * .35, 0)))
        P.add("Body", chunky_limb((sx * .5, -.02, 1.75), (sx * .58, -.2, 1.25), .1, .1, 2, salt=5))
        P.add("Body", chunky_limb((sx * .58, -.2, 1.25), (sx * .5, -.55, 1.0), .09, .11, 2, salt=9))
        P.add("Accent", rock((.2, .2, .18), (sx * .5, -.58, 1.0), jitter=.05))
        for k in (-1, 0, 1):
            P.add("Accent", crystal((sx * .5 + k * .05, -.62, 1.0), (k * .3, -1, -.5), .26, .035))
    P.add("Body", rock((.24, .22, .22), (0, -.14, 1.9), jitter=.05))
    P.add("Eye", slab((.18, .04, .035), (0, -.25, 1.91)))
    for sx in (-1, 1):
        P.add("Accent", crystal((sx * .06, -.12, 1.96), (sx * .5, .2, 1), .26, .035))
    P.add("Eye", rune((0, -.32, 1.75), (0, -.33, 1.45)) + rune((-.12, -.3, 1.62), (.12, -.3, 1.62)))
    return P


def hound(L):
    """Rock boar: a big front boulder chest and a smaller rump, a ridge of crystals down the back, a small head with a
    long stone snout ending in glowing nostril runes, rune-slit eyes, pillar legs on slab paws."""
    P = D.Parts()
    P.add("Body", rock((.48, .58, .52), (0, -.14, .86)))
    P.add("Body", rock((.4, .46, .4), (0, .22, .8)))
    for k, (y, z) in enumerate(((-.2, 1.1), (.0, 1.07), (.2, 1.0), (.36, .94))):
        P.add("Body", crystal((0, y, z - .06), (0, .4, 1), .22 - k * .03, .045))
    P.add("Body", rock((.26, .28, .24), (0, -.5, .98)))
    P.add("Body", rock((.17, .36, .15), (0, -.75, .86), rot=(.25, 0, 0), jitter=.05))
    P.add("Eye", slab((.1, .04, .03), (0, -.935, .8), rot=(.25, 0, 0)))
    for sx in (-1, 1):
        P.add("Eye", slab((.06, .06, .025), (sx * .07, -.6, 1.03), rot=(0, sx * .3, 0)))
        P.add("Body", crystal((sx * .08, -.48, 1.03), (sx * .4, .3, 1), .16, .03))
        P.add("Body", crystal((sx * .07, -.85, .8), (sx * .4, -.3, .6), .12, .025))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add("Body", rock((.22, .22, .24), (x * .85, y, .72), jitter=.05))
        for (sz, z) in (((.18, .18, .16), .6), ((.21, .2, .16), .48), ((.19, .19, .15), .37), ((.16, .16, .12), D.HOUND_KNEE),
                        ((.19, .19, .14), .2), ((.18, .18, .13), .1)):
            P.add(leg, rock(sz, (x, y, z), jitter=.04 if z > .55 else .12))
        P.add(leg, slab((.2, .24, .07), (x, y - .03, .035), taper=(.8, .75)))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """Slate crab: a flat slate shell and a boulder abdomen, a cluster of glowing gem eyes in front, crystal-shard legs
    with pebble knees and pointed tips."""
    P = D.Parts()
    P.add("Body", rock((.5, .5, .18), (0, .02, .2)))
    P.add("Body", rock((.38, .4, .28), (0, .28, .25)))
    P.add("Body", crystal((0, .3, .34), (0, .3, 1), .14, .04))
    for (x, z, r) in ((-.05, .24, .04), (.05, .24, .04), (-.1, .2, .028), (.1, .2, .028)):
        P.add("Eye", gem(r, (x, -.21, z), (x * 2, -1, .3)))
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        up = (knee - hip)
        P.add(f"AccentLeg{i}Upper", [orient(s.loft([(0, .035, .035, 2.0), (up.length, .03, .03, 2.0)], seg=6), hip, up)]
              + rock((.07, .07, .07), knee, jitter=.04))
        P.add(f"AccentLeg{i}Lower", crystal(knee, foot - knee + Vector((0, 0, -.01)), (foot - knee).length + .01, .03))
    return P


# =========================================================================== wheeled

def listener(L):
    """Ear idol: a stone cart on octagonal stone wheels carrying a squat stone head with a heavy brow and two giant
    shell ears, rune light glowing deep in each ear."""
    P = D.Parts()
    W = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .22, .12))
        P.add("Body", slab((.14, .08, .08), (c[0] * .82, c[1], .22)))
    P.add("Body", slab((.66, .9, .14), (0, 0, .32), taper=(.95, .95)))
    P.add("Body", rock((.6, .56, .55), (0, .05, .66)))
    P.add("Body", rock((.62, .3, .16), (0, -.17, .86), rot=(-.2, 0, 0)))
    for sx in (-1, 1):
        P.add("Eye", slab((.1, .08, .025), (sx * .12, -.19, .76)))
    P.add("Body", rock((.4, .4, .3), (0, .1, 1.0)))
    for sx in (-1, 1):
        o = Vector((sx * .14, .1, 1.08))
        d = Vector((sx, 0, .45)).normalized()
        shell = s.lathe([(.08, 0), (.16, .08), (.27, .2), (.3, .27), (.26, .3), (.16, .2), (0, .17)], seg=7)
        P.add("Body", [orient(shell, o, d)])
        P.add("Eye", gem(.1, o + d * .16, d, h=.05))
    return P


def worker(L):
    """Pack mule: a round boulder critter on stone wheels with a tiny head and a rune headlamp, lifting the crate over its
    back on two stubby stone arms."""
    P = D.Parts()
    W = [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .12, .1))
        P.add("Body", slab((.16, .08, .06), (c[0] * .7, c[1], .12)))
    P.add("Body", slab((.46, .64, .14), (0, 0, .17)))
    P.add("Body", rock((.52, .6, .56), (0, .04, .47)))
    P.add("Body", rock((.2, .2, .18), (0, -.3, .5), jitter=.05))
    P.add("Eye", slab((.14, .04, .04), (0, -.4, .5)))
    for sx in (-1, 1):
        P.add("Body", chunky_limb((sx * .2, 0, .66), (sx * .27, -.12, .98), .07, .08, 2, salt=11))
    P.add("Accent", slab((.5, .42, .36), (0, -.15, 1.13)))
    for z in (1.0, 1.26):
        P.add("Accent", slab((.53, .45, .05), (0, -.15, z)))
    P.add("Eye", rune((-.1, -.37, 1.06), (.1, -.37, 1.2)) + rune((.1, -.37, 1.06), (-.1, -.37, 1.2)))
    return P


def sweeper(L):
    """Rock turtle: a faceted dome shell with glowing cracks, a rim of stone chunks, a tiny head poking out in front,
    stubby feet; it rolls on hidden wheels and the lidar spins under the shell."""
    P = D.Parts()
    W = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .08, .06))
        for sx in (-1, 1):
            P.add("Body", slab((.03, .06, .26), (c[0] + sx * .05, c[1], .2)))
    P.add("Body", slab((.6, .6, .06), (0, 0, .33)))
    P.add("Body", [s.lathe([(0, .32), (.38, .32), (.4, .4), (.33, .56), (.18, .68), (0, .72)], seg=7)])
    for k in range(7):
        a = (k + .5) / 7 * TAU
        P.add("Accent", rock((.16, .16, .1), (math.cos(a) * .38, math.sin(a) * .38, .36), salt=k))
    for k in range(3):
        a = k / 3 * TAU + .3
        c, sn = math.cos(a), math.sin(a)
        P.add("Eye", rune((c * .2, sn * .2, .66), (c * .33, sn * .33, .53), w=.03))
    P.add("Body", rock((.2, .22, .18), (0, -.42, .42)))
    for sx in (-1, 1):
        P.add("Eye", slab((.05, .04, .025), (sx * .045, -.52, .45)))
    P.add("Body", disc(.05, .12, (0, 0, .29), seg=6))
    P.add("AccentLidar", disc(.12, .07, (0, 0, .22)) + slab((.07, .05, .04), (0, -.12, .22)))
    return P


def thumper(L):
    """Fist slammer: a squat golem torso with huge shoulders and a tiny head rides stone tracks, its two giant stone fists
    hammer the floor on glowing rune rings (the seismic pads)."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .56
        for k in range(5):
            y = -.56 + k * .28
            P.add("Accent", slab((.26, .3, .5), (x, y, .27), taper=(1, .9)))
        P.add("Accent", slab((.22, 1.3, .3), (x, 0, .27)))
    P.add("Body", slab((.9, 1.0, .2), (0, .05, .55)))
    P.add("Body", rock((.86, .8, .6), (0, .1, .9)))
    for sx in (-1, 1):
        P.add("Body", rock((.46, .5, .44), (sx * .44, -.05, 1.1), rot=(0, sx * .3, 0)))
        x = sx * .3
        P.add("Body", chunky_limb((sx * .46, -.15, 1.0), (x, -.8, .33), .12, .14, 2, salt=21))
        P.add("Accent", rock((.36, .38, .32), (x, -.85, .17), floor=.03))
        P.add("Eye", disc(.18, .04, (x, -.85, .02)))
    P.add("Body", rock((.24, .22, .22), (0, -.22, 1.1), jitter=.05))
    P.add("Eye", slab((.14, .04, .03), (0, -.33, 1.11)))
    P.add("Eye", rune((-.2, -.27, .85), (.2, -.27, .85), depth=.07))
    return P


# =========================================================================== tall, flying, mounted

def sorter(L):
    """Gem totem: stacked faceted stones on a stone base, a stone ring on top holding one huge glowing gem lens."""
    P = D.Parts()
    P.add("Accent", disc(.3, .08, (0, 0, .04)))
    P.add("Body", rock((.4, .36, .3), (0, 0, .22)))
    P.add("Body", rock((.32, .3, .32), (0, .02, .48)))
    P.add("Body", rock((.26, .24, .34), (0, 0, .76)))
    P.add("Body", rock((.22, .22, .2), (0, .04, .98)))
    ring = s.loft([(0, .27, .27, 2.0), (.08, .27, .27, 2.0)], seg=8)
    hole = s.rcyl(.19, .2, (0, 0, .04), seg=8, r=0)
    ring = s.boolean(ring, hole)
    P.add("Accent", [orient(ring, Vector((0, -.07, 1.25)), (0, -1, 0))])
    P.add("Eye", gem(.2, Vector((0, -.08, 1.25)), (0, -1, 0), h=.1))
    P.add("Body", rock((.26, .22, .32), (0, .0, 1.2), jitter=.05))
    for sx in (-1, 1):
        P.add("Body", crystal((sx * .14, -.08, 1.46), (sx * .6, 0, 1), .16, .03))
    return P


def drone(L):
    """Floating rock: a flat boulder with a glowing crystal underneath for an eye, four stone branches to stone
    propellers, runes on top."""
    P = D.Parts()
    P.add("Body", rock((.56, .5, .2), (0, 0, 0)))
    P.add("Body", crystal((0, -.14, -.04), (0, -.6, -1), .12, .06))
    P.add("Eye", gem(.05, Vector((0, -.22, -.11)), (0, -.6, -1), h=.04))
    P.add("Eye", rune((-.12, -.12, .1), (.12, -.12, .1), w=.03) + rune((0, -.2, .1), (0, .1, .1), w=.03))
    for i, (x, y) in enumerate([(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]):
        P.add("Body", chunky_limb((x * .35, y * .35, 0), (x, y, .0), .045, .04, 2, salt=31 + i))
        P.add("Body", disc(.035, .05, (x, y, .0), seg=6))
        rot = [s.rcyl(.03, .089, (x, y, .0645), seg=6, r=0)]
        rot += [s.rbox((.34, .07, .014), (x, y, .07), r=0, seg=1, rot=(.25, 0, math.radians(45 if x * y > 0 else -45)),
                       taper=(1, .6))]
        P.add(f"AccentRotor{i}", rot)
    return P


def turret(L):
    """Cairn cannon: a cairn of stacked boulders with a glowing rune band (still), a rough stone block on top turning a
    flared octagonal stone cannon with a crystal sight."""
    P = D.Parts()
    P.add("Body", rock((.7, .64, .3), (0, 0, .16), floor=0))
    P.add("Body", rock((.54, .52, .36), (0, .02, .38)))
    P.add("Body", rock((.4, .38, .3), (0, 0, .68)))
    P.add("Body", rock((.3, .28, .26), (0, 0, .9)))
    P.add("Body", disc(.12, .18, (0, 0, 1.04), seg=6))
    for k in range(5):
        a = k / 5 * TAU
        P.add("Eye", rune((math.cos(a) * .2, math.sin(a) * .2, .5), (math.cos(a) * .17, math.sin(a) * .17, .64), w=.03))
    P.add("Eye", slab((.12, .07, .03), (0, -.13, .9)))
    P.add("AccentHead", disc(.1, .1, (0, 0, 1.16), seg=6))
    P.add("AccentHead", rock((.32, .36, .22), (0, .04, 1.3), jitter=.05))
    P.add("AccentHead", [s.rcyl(.075, .4, (0, -.32, 1.3), axis="Y", seg=8, r=0, radius2=.09)])
    P.add("AccentHead", [s.rcyl(.11, .06, (0, -.53, 1.3), axis="Y", seg=8, r=0)])
    P.add("AccentHead", crystal((0, .02, 1.38), (0, 0, 1), .1, .03))
    return P


def vacuum(L):
    """Millstone: a ten-sided stone disc with a stone hub ring, radial rune grooves facing the room, a gem eye in the
    centre."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.34, .1, (0, 0, .05), seg=10, r=0, radius2=.3)])
    P.add("Accent", [s.rcyl(.12, .04, (0, 0, -.005), seg=8, r=0)])
    for k in range(5):
        a = k / 5 * TAU + .3
        P.add("Eye", [align(s.rbox((.03, .02, .17), r=0, seg=1), (math.cos(a) * .15, math.sin(a) * .15, .002),
                            (math.cos(a) * .32, math.sin(a) * .32, .002))])
    P.add("Eye", gem(.07, Vector((0, 0, -.02)), (0, 0, -1), h=.03))
    millstone_chunks(P)
    return P


def millstone_chunks(P):
    for k in range(8):
        a = (k + .5) / 8 * TAU
        P.add("Accent", rock((.13, .13, .1), (math.cos(a) * .34, math.sin(a) * .34, .07), salt=k))


# =========================================================================== devices and sensor modules

def lidar(L):
    """A cairn post: a flat boulder base, a hexagonal stone pillar, a rune ring (still), a crystal drum that spins."""
    P = D.Parts()
    P.add("Body", rock((.36, .34, .14), (0, 0, .06), floor=0))
    P.add("Body", disc(.05, .32, (0, 0, .25), seg=6))
    P.add("Body", disc(.075, .03, (0, 0, .4), seg=6))
    P.add("Eye", disc(.08, .02, (0, 0, .425), seg=6) + slab((.04, .05, .03), (0, -.08, .45)))
    P.add("Body", slab((.02, .05, .02), (0, -.05, .44)))
    P.add("AccentHead", [s.rcyl(.1, .12, (0, 0, .455), seg=6, r=0)] + crystal((0, 0, .5), (0, 0, 1), .06, .04))
    return P


def pir(L):
    """A stone tablet on the wall with one gem eye."""
    P = D.Parts()
    P.add("Body", slab((.16, .06, .16), (0, .04, 0), taper=(.85, 1)))
    P.add("Eye", gem(.055, (0, .01, 0), (0, -1, 0), h=.05))
    return P


def plate(L):
    """The pressure plate as a stone slab with a carved rune cross."""
    P = D.plate(L)
    for a in (1, -1):
        P.add("Accent", [s.rbox((1.2, .05, .006), (0, 0, .014), r=0, seg=1, rot=(0, 0, a * math.pi / 4))])
    return P


def security_camera(L):
    """A boulder on a stone bracket with a gem for a lens."""
    P = D.Parts()
    P.add("Body", slab((.16, .06, .22), (0, .27, 0)))
    P.add("Accent", slab((.06, .2, .06), (0, .16, -.02)))
    P.add("Accent", rock((.17, .28, .15), (0, -.04, -.02), jitter=.06))
    P.add("Eye", gem(.05, (0, -.17, -.02), (0, -1, 0), h=.07))
    return P


def laser_emitter(L):
    """A stone obelisk with a rune strip."""
    P = D.Parts()
    P.add("Body", slab((.12, .12, 1.5), (0, 0, .75), taper=(.8, .8)))
    P.add("Body", [s.loft([(1.49, .05, .05, 8.0), (1.6, 0, 0)], seg=4, loc=(0, 0, 0))])
    P.add("Eye", slab((.04, .04, 1.4), (0, -.055, .85)))
    return P


def attach_eye(L):
    P = D.Parts()
    P.add("Accent", rock((.22, .16, .2), (0, .03, 0), jitter=.05))
    P.add("Eye", gem(.065, (0, -.04, 0), (0, -1, 0), h=.045))
    return P


def attach_mic(L):
    """A crystal stalk with a glowing gem on top."""
    P = D.Parts()
    P.add("Accent", disc(.045, .03, (0, 0, .015), seg=6) + [s.rcyl(.02, .27, (0, 0, .14), seg=6, r=0)])
    P.add("Body", disc(.06, .03, (0, 0, .28), seg=6))
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=.06)
    P.add("Eye", [s.place(bm, (0, 0, .32))])
    return P


def attach_seismic(L):
    """A stone stake: a slab head, a hexagonal shaft, a glowing crystal tip in the floor."""
    P = D.Parts()
    P.add("Accent", slab((.16, .16, .03), (0, 0, .3)) + [s.rcyl(.022, .22, (0, 0, .2), seg=6, r=0)])
    P.add("Eye", crystal((0, 0, .1), (0, 0, -1), .1, .04))
    return P


def attach_nose(L):
    """A stone snout with glowing nostril slits."""
    P = D.Parts()
    P.add("Accent", rock((.18, .27, .17), (0, -.12, 0), jitter=.05))
    P.add("Eye", slab((.025, .03, .02), (-.025, -.255, .005)) + slab((.025, .03, .02), (.025, -.255, .005)))
    return P


# --------------------------------------------------------------------------- the stone world's construction kit

def stone(size, loc, rot=(0, 0, 0), j=None):
    """Hand-cut stone: chamfered blocks whose corners are knocked out of true; a tall block is laid in courses, each
    course a little out of line with the one below."""
    w, d, h = size
    x, y, z = loc
    rng = random.Random(_seed(loc, 7))
    j = min(.02, min(size) * .08) if j is None else j
    if not any(rot) and h > .7 and h > 1.6 * min(w, d):
        n = max(2, round(h / .45))
        ch = h / n
        out = []
        for i in range(n):
            zz = z - h / 2 + ch * (i + .5)
            ox, oy = (rng.uniform(-.008, .008), rng.uniform(-.008, .008)) if 0 < i else (0, 0)
            out += stone((w - .006, d - .006, ch + .012 if i < n - 1 else ch), (x + ox, y + oy, zz + (.006 if i < n - 1 else 0)), j=j)
        return out
    bm = s.rbox(size, (0, 0, 0), r=min(.05, min(size) * .2), seg=1)
    for v in bm.verts:
        v.co += Vector((rng.uniform(-j, j), rng.uniform(-j, j), rng.uniform(-j, j)))
    return [s.place(bm, loc, rot)]


def log(p0, p1, r, seg=7):
    """A hewn log from p0 to p1 with lighter end rings."""
    a, b = Vector(p0), Vector(p1)
    d = (b - a).normalized()
    out = [align(s.rcyl(r, (b - a).length, seg=seg, r=0), a, b)]
    for e in (a + d * .01, b - d * .01):
        out.append(align(s.rcyl(r * 1.06, .03, seg=seg, r=0), e - d * .015, e + d * .015))
    return out


def tablet(w, h, loc, depth=.08):
    """(stone, glow): a stone frame with a glowing rune panel in front."""
    x, y, z = loc
    return stone((w, depth, h), loc), [s.rbox((w - .1, .02, h - .1), (x, y - depth / 2 - .004, z), r=0, seg=1)]


def iron_ring(loc, r=.04):
    x, y, z = loc
    return [s.rcyl(r, .02, (x, y - .012, z), axis="Y", seg=6, r=0), s.rcyl(r * .5, .03, (x, y - .005, z), axis="Y", seg=6, r=0)]


# =========================================================================== every prop in stone and wood

def desk(L):
    """A stone slab on two stone blocks, a rune tablet for a monitor."""
    P = D.Parts()
    P.add("Body", stone((1.6, .8, .08), (0, 0, .725)))
    for x in (-.58, .58):
        P.add("Accent", stone((.36, .7, .7), (x, 0, .35)))
    st, g = tablet(.56, .41, (0, .17, .95), depth=.1)
    P.add("Accent", st + stone((.44, .15, .03), (0, -.12, .78))).add("Eye", g)
    return P


def terminal(L):
    """A stone console on two log trestles with three rune tablets."""
    P = D.Parts()
    P.add("Body", stone((1.6, .8, .08), (0, 0, .725)))
    for x in (-.6, .6):
        P.add("Accent", log((x, -.3, 0), (x, -.3, .7), .06) + log((x, .3, 0), (x, .3, .7), .06) + log((x, -.34, .62), (x, .34, .62), .05))
    for x in (-.36, 0, .36):
        st, g = tablet(.3, .38, (x, .17, .945))
        P.add("AccentConsole", st).add("Eye", g)
    P.add("AccentConsole", stone((.52, .17, .03), (0, -.12, .78)))
    return P


def chair(L):
    """A tree-stump stool with a boulder cushion and a standing stone for a back."""
    P = D.Parts()
    P.add("Accent", log((0, 0, .0), (0, 0, .44), .28))
    P.add("Body", rock((.5, .5, .15), (0, 0, .48), jitter=.04) + stone((.5, .1, .6), (0, .21, .77), rot=(-.12, 0, 0)))
    return P


def server_rack(L):
    """A pillar of five stacked stone slabs, glowing rune slits on each."""
    P = D.Parts()
    for i in range(5):
        z = .205 + i * .4
        P.add("Body", stone((.7, 1.0, .41), (0, 0, z)))
        P.add("Eye", [s.rbox((.42, .03, .05), (0, -.505, z + .05), r=0, seg=1), s.rbox((.05, .03, .16), (-.12, -.505, z - .02), r=0, seg=1)])
    return P


def cabinet(L):
    """Three stone blocks stacked as drawers, iron ring pulls."""
    P = D.Parts()
    for i in range(3):
        P.add("Body", stone((.9, .5, .47), (0, -.01, .235 + i * .465)))
        P.add("Accent", iron_ring((0, -.26, .26 + i * .465)))
    return P


def meeting_table(L):
    """A dolmen: a huge stone slab on two boulders."""
    P = D.Parts()
    P.add("Body", stone((3.2, 1.3, .1), (0, 0, .73)))
    for x in (-1.0, 1.0):
        P.add("Accent", rock((.7, .9, .76), (x, 0, .36), jitter=.05, floor=0))
    return P


def plant(L):
    """A faceted low-poly tree on a log trunk in a stone pot."""
    P = D.Parts()
    P.add("Accent", [s.lathe([(0, 0), (.16, 0), (.18, .03), (.22, .36), (.24, .4), (0, .4)], seg=7)])
    P.add("Body", log((0, 0, .35), (0, 0, .95), .05) + rock((.66, .68, .5), (0, 0, 1.0), jitter=.05)
          + rock((.44, .44, .4), (.02, 0, 1.19), jitter=.05))
    return P


def kitchen_counter(L):
    """Stone cupboard blocks under a thick plank, a stone oven with a glowing mouth."""
    P = D.Parts()
    for i in range(4):
        P.add("Body", stone((.59, .64, .88), (-.9 + i * .6, 0, .44)))
        P.add("Accent", iron_ring((-.9 + i * .6, -.32, .7), .03))
    P.add("Accent", stone((2.44, .7, .06), (0, 0, .905)) + stone((.5, .42, .32), (.7, .02, 1.085)))
    P.add("Eye", [s.rbox((.28, .02, .16), (.7, -.2, 1.06), r=0, seg=1)])
    return P


def shelf(L):
    """Plank shelves between four log uprights, boulders and stone pots on them."""
    P = D.Parts()
    for x in (-.95, .95):
        for y in (-.25, .25):
            P.add("Body", log((x, y, 0), (x, y, 2.0), .05))
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", stone((2.0, .6, .05), (0, 0, h)))
    P.add("Accent", rock((.4, .36, .3), (-.6, 0, .6), jitter=.04) + [s.lathe([(0, 0), (.13, 0), (.16, .2), (.1, .26), (0, .26)], seg=7,
                                                                            loc=(.5, .05, .47))]
          + rock((.45, .4, .34), (.3, 0, 1.22), jitter=.04) + [s.lathe([(0, 0), (.15, 0), (.18, .22), (0, .3)], seg=7, loc=(-.55, 0, 1.67))])
    return P


def boxes(L):
    """Three wooden chests with iron bands."""
    P = D.Parts()
    for (sz, c) in (((.6, .6, .5), (0, 0, .25)), ((.5, .5, .4), (.05, -.05, .695)), ((.45, .45, .4), (.545, .1, .2))):
        P.add("Accent", stone(sz, c) + [s.rbox((sz[0] + .02, .05, sz[2] + .02), (c[0], c[1] + k * sz[1] * .3, c[2]), r=0, seg=1)
                                        for k in (-1, 1)])
    return P


def water_cooler(L):
    """A stone urn pedestal with a crystal jug on top."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.17, 0), (.17, .1), (.12, .2), (.12, .8), (.17, .9), (.17, 1.0), (0, 1.0)], seg=7)]
          + stone((.06, .08, .04), (0, -.15, .7)))
    P.add("Eye", [s.lathe([(0, 0), (.06, 0), (.06, .06), (.15, .14), (.15, .34), (.08, .44), (0, .46)], seg=6, loc=(0, 0, .985))])
    return P


def whiteboard(L):
    """A stone tablet hung between two log posts under a log crossbar."""
    P = D.Parts()
    P.add("Body", stone((1.76, .05, 1.0), (0, 0, 1.47), j=.006))
    P.add("Accent", log((-.9, 0, 0), (-.9, 0, 2.0), .04) + log((.9, 0, 0), (.9, 0, 2.0), .04) + log((-.92, 0, 1.97), (.92, 0, 1.97), .03))
    return P


def sofa(L):
    """A log-and-plank bench heaped with boulder cushions, stumps for armrests."""
    P = D.Parts()
    P.add("Accent", stone((2.0, .8, .3), (0, 0, .15)))
    for sx in (-1, 1):
        P.add("Accent", log((sx * 1.0, 0, 0), (sx * 1.0, 0, .62), .1, seg=6))
    P.add("Body", rock((.92, .7, .26), (-.47, -.05, .4), jitter=.05) + rock((.92, .7, .26), (.47, -.05, .4), jitter=.05)
          + rock((.92, .28, .5), (-.47, .28, .62), jitter=.05) + rock((.92, .28, .5), (.47, .28, .62), jitter=.05))
    return P


def toilet_stall(L):
    """A log cabin cubicle on stone footings, a stone seat and a bucket."""
    P = D.Parts()
    for k in range(19):
        z = .15 + k * .1
        for sx in (-1, 1):
            P.add("Body", log((sx * .47, -.75, z), (sx * .47, .73, z), .052, seg=6)[:1])
        P.add("Body", log((-.52, .73, z), (.52, .73, z), .052, seg=6)[:1])
    for sx in (-1, 1):
        for y in (-.7, 0.0, .7):
            P.add("Body", stone((.12, .12, .12), (sx * .47, y, .06)))
    P.add("Accent", [s.lathe([(0, 0), (.15, 0), (.2, .3), (.22, .4), (0, .42)], seg=7, loc=(0, .4, 0))] + stone((.36, .14, .36), (0, .64, .55)))
    return P


def partition(L):
    """A palisade of pointed split planks on two log rails."""
    P = D.Parts()
    for k in range(13):
        x = -.75 + k * .125
        h = 1.1 + (k % 3) * .04
        P.add("Body", stone((.11, .05, h), (x, 0, h / 2), j=.004) + [s.loft([(h - .005, .055, .025, 8.0), (1.26 - (2 - k % 3) * .02, 0, 0)],
                                                                             seg=4, loc=(x, 0, 0))])
    P.add("Accent", log((-.81, 0, .3), (.81, 0, .3), .028) + log((-.81, 0, .9), (.81, 0, .9), .028))
    return P


def bookshelf(L):
    """A log frame with stone shelves holding stone tablets for books."""
    P = D.Parts()
    for x in (-.45, .45):
        for y in (-.13, .13):
            P.add("Body", log((x, y, 0), (x, y, 2.0), .06))
    for h in (.04, .5, 1.0, 1.5, 1.96):
        P.add("Body", stone((1.0, .35, .05), (0, 0, h)))
    for i, h in enumerate((.06, .52, 1.02, 1.52)):
        x = -.4
        for k in range(5 + i % 2):
            w = .09 + .04 * ((k + i) % 3)
            hh = .34 - .06 * ((k * 7 + i) % 3)
            P.add("Accent", stone((w, .24, hh), (x + w / 2, .02, h + hh / 2 - .01), rot=(0, ((k + i) % 3 - 1) * .08, 0)))
            x += w + .02
    return P


def printer(L):
    """A stone block press with a slot, a slab tray and a glowing rune."""
    P = D.Parts()
    P.add("Body", stone((.6, .6, .95), (0, 0, .475)) + stone((.56, .52, .14), (0, .02, 1.0)))
    P.add("Accent", stone((.36, .3, .04), (-.1, -.35, .82), rot=(math.radians(8), 0, 0)) + stone((.08, .2, .08), (-.1, -.25, .78)))
    P.add("Eye", [s.rbox((.16, .03, .06), (.18, -.25, 1.0), r=0, seg=1)])
    return P


def fridge(L):
    """A stone icebox: a tall block with two slab doors and iron handles."""
    P = D.Parts()
    P.add("Body", stone((.7, .68, 1.78), (0, .01, .89)) + stone((.66, .05, .48), (0, -.35, 1.5)) + stone((.66, .05, 1.16), (0, -.35, .66)))
    P.add("Accent", [s.rbox((.05, .05, .36), (.26, -.385, h), r=0, seg=1) for h in (1.38, 1.0)])
    return P


def coffee_machine(L):
    """A faceted stone kettle on a hearth block, glowing coals in front, a wooden handle."""
    P = D.Parts()
    P.add("Body", stone((.32, .38, .12), (0, 0, .06)) + [s.lathe([(0, 0), (.12, 0), (.15, .1), (.13, .22), (.06, .26), (0, .27)],
                                                                   seg=7, loc=(0, .03, .115))])
    P.add("Body", crystal((0, -.08, .25), (0, -1, .5), .16, .03))
    P.add("Accent", [s.tube([(-.1, .03, .33), (-.06, .03, .48), (.06, .03, .48), (.1, .03, .33)], .018, smooth=False)])
    P.add("Eye", [s.rbox((.16, .02, .06), (0, -.19, .06), r=0, seg=1)])
    return P


def round_table(L):
    """A big tree stump with roots and a stone disc on top."""
    P = D.Parts()
    P.add("Accent", log((0, 0, 0), (0, 0, .71), .28))
    for k in range(3):
        a = k / 3 * TAU + .4
        P.add("Accent", rock((.22, .16, .14), (math.cos(a) * .3, math.sin(a) * .3, .07), floor=0, jitter=.05))
    P.add("Body", [s.rcyl(.5, .06, (0, 0, .73), seg=9, r=0)])
    return P


def armchair(L):
    """A boulder throne: a stone base, a boulder seat, a standing stone back and boulder arms."""
    P = D.Parts()
    P.add("Accent", stone((.7, .6, .4), (0, -.02, .2)))
    P.add("Body", rock((.58, .6, .16), (0, -.06, .45), jitter=.04) + stone((.72, .16, .64), (0, .29, .55)))
    for sx in (-1, 1):
        P.add("Body", rock((.14, .66, .3), (sx * .36, -.02, .5), jitter=.04))
    return P


def coffee_table(L):
    """A flat stone slab on two boulders."""
    P = D.Parts()
    P.add("Body", stone((1.0, .55, .08), (0, 0, .38)))
    for x in (-.3, .3):
        P.add("Accent", rock((.3, .4, .38), (x, 0, .18), jitter=.04, floor=0))
    return P


def tv_stand(L):
    """A stone block with a big rune tablet standing on it."""
    P = D.Parts()
    P.add("Body", stone((1.4, .4, .5), (0, 0, .25)))
    st, g = tablet(1.3, .9, (0, .05, 1.0), depth=.12)
    P.add("Accent", st + stone((.3, .2, .12), (0, .05, .53))).add("Eye", g)
    return P


def reception_desk(L):
    """A counter of stone blocks with a plank top, a worktop on stumps behind, a rune tablet."""
    P = D.Parts()
    for i in range(4):
        P.add("Body", stone((.61, .28, 1.08), (-.93 + i * .62, -.33, .54)))
    for sx in (-1, 1):
        P.add("Body", stone((.12, .7, 1.08), (sx * 1.18, .03, .54)))
    P.add("Accent", stone((2.5, .3, .05), (0, -.33, 1.115)) + stone((2.3, .7, .05), (0, .03, .74)))
    P.add("Accent", log((-.6, .2, 0), (-.6, .2, .72), .08) + log((.6, .2, 0), (.6, .2, .72), .08))
    st, g = tablet(.5, .35, (.4, .15, .94))
    P.add("Eye", g + st)
    return P


def trash_bin(L):
    """A wooden barrel with iron hoops."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.16, 0), (.19, .22), (.17, .44), (0, .44)], seg=8)])
    P.add("Accent", [s.rcyl(.188, .03, (0, 0, z), seg=8, r=0) for z in (.08, .36)])
    return P


def coat_rack(L):
    """A dead tree on a rock: a log trunk with branch hooks, two coats and a hat."""
    P = D.Parts()
    P.add("Accent", rock((.5, .5, .2), (0, 0, .1), floor=0, jitter=.04) + log((0, 0, .1), (0, 0, 1.68), .04))
    for a in (0.0, 2.1, 4.2):
        P.add("Accent", log((0, 0, 1.5), (math.cos(a) * .2, math.sin(a) * .2, 1.78), .022, seg=5))
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.3)) + L.cushion((.3, .12, .55), (-.08, .1, 1.32), rot=(0, 0, 2.1)))
    P.add("Body", [s.lathe([(0, 0), (.12, 0), (.12, .015), (.07, .03), (.07, .1), (0, .11)], seg=6, loc=(0, 0, 1.66))])
    return P


def lockers(L):
    """Three standing stones with plank doors and iron rings."""
    P = D.Parts()
    for i, x in enumerate((-.3, 0, .3)):
        P.add("Body", stone((.29, .48, 1.9), (x, .005, .95), j=.012))
        P.add("Body", stone((.22, .04, 1.6), (x, -.25, .95), j=.006))
        P.add("Accent", iron_ring((x + .06, -.27, 1.0), .03))
    return P


def pallet(L):
    """A raft of logs with stone blocks stacked on it."""
    P = D.Parts()
    for k in range(7):
        P.add("Accent", log((-.6, -.45 + k * .15, .075), (.6, -.45 + k * .15, .075), .075, seg=6)[:1])
    P.add("Body", sum([stone((.55, .45, .4), (x, y, .345)) for x in (-.3, .3) for y in (-.24, .24)], [])
          + sum([stone((.55, .45, .4), (-.3, y, .74)) for y in (-.24, .24)], []))
    return P


def wash_basin(L):
    """A stone counter with two stone bowls, stone spouts and a still pool for a mirror in a stone frame."""
    P = D.Parts()
    P.add("Body", stone((1.6, .45, .82), (0, .02, .41)) + stone((1.6, .5, .07), (0, 0, .85)) + stone((1.6, .04, .4), (0, .235, 1.06)))
    for x in (-.4, .4):
        P.add("Accent", [s.lathe([(0, 0), (.13, 0), (.18, .07), (.18, .1), (0, .1)], seg=7, loc=(x, -.02, .86))]
              + stone((.05, .2, .05), (x, .13, 1.0)))
    P.add("Eye", [s.rbox((1.36, .02, .66), (0, .235, 1.55), r=0, seg=1)])
    P.add("Body", stone((1.5, .05, .08), (0, .245, 1.89)) + stone((1.5, .05, .08), (0, .245, 1.2))
          + stone((.08, .05, .7), (-.71, .245, 1.55)) + stone((.08, .05, .7), (.71, .245, 1.55)))
    return P


def vending_machine(L):
    """A stone shrine under a gabled stone roof, a glowing gem window, an offering bowl."""
    P = D.Parts()
    P.add("Body", stone((.86, .78, 1.6), (0, -.01, .8)))
    for sx in (-1, 1):
        P.add("Body", stone((.52, .8, .07), (sx * .22, -.01, 1.72), rot=(0, sx * .55, 0)))
    P.add("Eye", [s.rbox((.5, .02, .9), (-.05, -.405, 1.0), r=0, seg=1)])
    P.add("Accent", [s.lathe([(0, 0), (.07, 0), (.09, .05), (0, .05)], seg=7, loc=(.28, -.34, .45))] + stone((.2, .1, .05), (.28, -.37, .44))
          + stone((.14, .05, .3), (.3, -.41, 1.0)))
    return P


def ping_pong_table(L):
    """A stone slab on four log legs, a net between two stone posts."""
    P = D.Parts()
    P.add("Body", stone((2.74, 1.52, .06), (0, 0, .73)))
    for x in (-1.05, 1.05):
        for y in (-.55, .55):
            P.add("Accent", log((x, y, 0), (x, y, .71), .11))
    P.add("Eye", [s.rbox((.02, 1.66, .15), (0, 0, .835), r=0, seg=1)])
    P.add("Accent", stone((.05, .05, .14), (0, .84, .84)) + stone((.05, .05, .14), (0, -.84, .84)))
    return P


def bean_bag(L):
    """A big mossy boulder to flop onto."""
    P = D.Parts()
    P.add("Body", rock((.92, .92, .74), (0, 0, .34), jitter=.03, floor=0))
    return P


def arcade_machine(L):
    """A stone monolith with a slanted rune screen, a stone ledge with round stone buttons."""
    P = D.Parts()
    P.add("Body", stone((.7, .74, 1.8), (0, .03, .9)))
    P.add("Eye", [s.rbox((.56, .03, .44), (0, -.36, 1.3), r=0, seg=1, rot=(math.radians(-15), 0, 0))])
    P.add("Accent", stone((.72, .4, .08), (0, -.36, .95), rot=(math.radians(-12), 0, 0)))
    P.add("Accent", [s.rcyl(.035, .04, (-.15 + k * .15, -.43, 1.0), seg=6, r=0, rot=(math.radians(-12), 0, 0)) for k in range(3)])
    return P


def safe(L):
    """A stone chest bound in iron with a big ring and a rune lock."""
    P = D.Parts()
    P.add("Body", stone((.8, .7, 1.0), (0, 0, .5)))
    P.add("Accent", [s.rbox((.06, .73, 1.02), (x, 0, .5), r=0, seg=1) for x in (-.25, .25)]
          + iron_ring((0, -.36, .6), .08) + stone((.14, .04, .16), (.0, -.37, .82)))
    P.add("Eye", [s.rbox((.06, .02, .08), (0, -.395, .82), r=0, seg=1)])
    return P


def desk_lamp(L):
    """A stick in a rock with a stone cap, a glowing crystal hanging under it."""
    P = D.Parts()
    P.add("Body", rock((.16, .2, .12), (0, -.02, .06), floor=0, jitter=.04) + log((0, -.02, .08), (0, -.13, .33), .012, seg=5))
    P.add("Accent", stone((.15, .17, .04), (0, -.16, .34), rot=(.3, 0, 0)))
    P.add("Eye", crystal((0, -.16, .33), (0, 0, -1), .09, .03))
    return P


def floor_lamp(L):
    """A hexagonal stone column on a boulder, a stone cap over a glowing crystal disc."""
    P = D.Parts()
    P.add("Body", rock((.36, .36, .2), (0, 0, .1), floor=0, jitter=.04) + [s.rcyl(.07, 1.3, (0, 0, .72), seg=6, r=0)])
    P.add("Accent", [s.lathe([(.2, 0), (.2, .05), (.12, .2), (0, .28)], seg=6, loc=(0, 0, 1.37))])
    P.add("Eye", [s.rcyl(.14, .03, (0, 0, 1.36), seg=6, r=0)])
    return P


def ceiling_lamp(L):
    """A wooden plank with three gems set in stone rims."""
    P = D.Parts()
    P.add("Body", stone((1.24, .64, .05), (0, 0, -.025), j=.004))
    for x in (-.4, 0, .4):
        P.add("Accent", [s.rcyl(.13, .03, (x, 0, -.06), seg=6, r=0)])
        P.add("Eye", gem(.1, Vector((x, 0, -.06)), (0, 0, -1), h=.02))
    return P


def exit_sign(L):
    """A stone tablet with glowing rune faces."""
    P = D.Parts()
    P.add("Body", stone((.38, .26, .16), (0, 0, 0), j=.004))
    P.add("Eye", [s.rbox((.33, .3, .11), (0, 0, 0), r=0, seg=1)])
    P.add("Accent", [s.rbox((.05, .31, .07), (-.1, 0, 0), r=0, seg=1), s.rbox((.12, .31, .02), (.05, 0, 0), r=0, seg=1)])
    return P


def hatch(L):
    """A millstone lid ringed by rough stones, two log grips, a glowing rune ring."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.74, .06, (0, 0, .03), seg=10, r=0)])
    for k in range(8):
        a = (k + .5) / 8 * TAU
        P.add("Accent", rock((.16, .12, .07), (math.cos(a) * .76, math.sin(a) * .76, .035), floor=0, jitter=.04))
    P.add("Accent", log((-.4, -.15, .07), (.4, -.15, .07), .02, seg=5) + log((-.4, .15, .07), (.4, .15, .07), .02, seg=5))
    P.add("Eye", [s.rcyl(.9, .02, (0, 0, .01), seg=12, r=0)])
    return P


PROP_DESIGNS = [desk, terminal, chair, server_rack, cabinet, meeting_table, plant, kitchen_counter, shelf, boxes, water_cooler,
                whiteboard, sofa, toilet_stall, partition, bookshelf, printer, fridge, coffee_machine, round_table, armchair,
                coffee_table, tv_stand, reception_desk, trash_bin, coat_rack, lockers, pallet, wash_basin, vending_machine,
                ping_pong_table, bean_bag, arcade_machine, safe, desk_lamp, floor_lamp, ceiling_lamp, exit_sign, hatch]


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_pir": pir, "sensor_plate": plate, "security_camera": security_camera,
    "laser_emitter": laser_emitter, "attach_eye": attach_eye, "attach_mic": attach_mic,
    "attach_seismic": attach_seismic, "attach_nose": attach_nose,
}
DESIGNS.update({f.__name__: f for f in PROP_DESIGNS})
