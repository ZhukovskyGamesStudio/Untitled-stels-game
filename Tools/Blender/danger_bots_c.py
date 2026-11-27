"""Red-zone renegades and their traps: the banshee, the maniac, the spider's cocoon, the drone's net and the cleaner's
goo puddle — base models (no per-style bestiary variants yet).

Concepts (ComfyUI, Logs/comfy/danger/): banshee_7186562.png (a horn cluster for a head with glowing throats, a ribbed
corset torso, a tattered shroud), maniac_9391214.png / maniac_9391215.png (a lanky hunched biped with a cage pack and
clawed hands), cocoon_2108298.png (a silk-wrapped bundle on a strand), net_2388441.png (a rope net draped over a
standing figure, weights on the hem), goo_13703199.png (a lobed blob with a raised rim and bubbles).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/danger_bots_c.py -- <models_root> \
          [robot_banshee] [robot_maniac] [cocoon] [net] [goo_puddle]        (no names = all five)
Writes <models_root>/Robots/<name>.fbx (Assets/_Game/Art/Models for the game, a scratch folder while iterating).

Axes: Blender x right, -y front (= Unity +z), z up (= Unity +y); every pivot on the floor under the centre, except the
cocoon (pivot at its bottom tip, the strand going up).
Roles: Body* (body material), Accent* (accent colour), Eye* (glows in the status colour), Web* (the web material),
Goo (the goo material), AccentLeg{n}Upper / AccentLeg{n}Lower (ProceduralLegs: knee = top of the shin).
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (HERE, os.path.join(HERE, "creatures"), os.path.join(HERE, "bestiary")):
    if _p not in sys.path:
        sys.path.insert(0, _p)

import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

import meshfix  # noqa: E402
import shapes as s  # noqa: E402
from common import Creature  # noqa: E402

L = Creature()


# --------------------------------------------------------------------------- helpers

def V(p):
    return Vector(p)


def along(bm, base, direction):
    """Turn a shape built along +Z from z = 0 so it points along direction, and move its z = 0 to base."""
    d = V(direction).normalized()
    q = Vector((0, 0, 1)).rotation_difference(d)
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=V(base), verts=bm.verts)
    return bm


def spline(points, per=4, closed=False):
    """Catmull-Rom through points: per samples per span (the points themselves included)."""
    P = [V(p) for p in points]
    n = len(P)
    out = []
    spans = n if closed else n - 1
    for i in range(spans):
        p0 = P[(i - 1) % n] if closed or i > 0 else P[0]
        p1, p2 = P[i], P[(i + 1) % n]
        p3 = P[(i + 2) % n] if closed or i + 2 < n else P[-1]
        for k in range(per):
            t = k / per
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    if not closed:
        out.append(P[-1])
    return out


def cord(points, r, sides=4, closed=False, radii=None, phase=0.0):
    """One continuous tube through points (shared rings, no seams between segments): ropes, threads, cables, hoops."""
    P = [V(p) for p in points]
    n = len(P)
    bm = bmesh.new()
    tangents = []
    for i in range(n):
        if closed:
            t = P[(i + 1) % n] - P[i - 1]
        else:
            t = P[min(i + 1, n - 1)] - P[max(i - 1, 0)]
        tangents.append(t.normalized() if t.length > 1e-9 else Vector((0, 0, 1)))
    t0 = tangents[0]
    ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
    nrm = (ref - t0 * ref.dot(t0)).normalized()
    rings = []
    for i in range(n):
        t = tangents[i]
        nrm = (nrm - t * nrm.dot(t))
        nrm = nrm.normalized() if nrm.length > 1e-9 else (Vector((1, 0, 0)) - t * t.x).normalized()
        b = t.cross(nrm)
        rr = r * (radii[i] if radii else 1.0)
        ring = []
        for k in range(sides):
            a = phase + 2 * math.pi * k / sides
            ring.append(bm.verts.new(P[i] + (nrm * math.cos(a) + b * math.sin(a)) * rr))
        rings.append(ring)
    spans = n if closed else n - 1
    for i in range(spans):
        A, B = rings[i], rings[(i + 1) % n]
        for k in range(sides):
            bm.faces.new((A[k], A[(k + 1) % sides], B[(k + 1) % sides], B[k]))
    if not closed:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def cloth(fn, nu, nv, thick=0.012):
    """A thick sheet: fn(u, v) -> point for u, v in 0..1 on an nu x nv grid, solidified (a closed shell)."""
    bm = bmesh.new()
    grid = [[bm.verts.new(V(fn(i / nu, j / nv))) for j in range(nv + 1)] for i in range(nu + 1)]
    for i in range(nu):
        for j in range(nv):
            bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=thick)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def tube_ring(points_fn, n, r, sides=4):
    """A closed hoop through n points points_fn(k / n)."""
    return cord([points_fn(k / n) for k in range(n)], r, sides=sides, closed=True)


def tris(objs):
    return {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs}


# --------------------------------------------------------------------------- banshee

def horn(base, direction, length, bell, seg=14):
    """A resonator horn: a narrow throat flaring to a lipped bell, hollow down to a cone (Accent); the glowing throat
    disc deep inside (Eye). Returns (accent bmesh, eye bmesh)."""
    t = .03
    prof = [(t, 0), (t * 1.05, length * .3), (t * 1.4 + bell * .1, length * .58), (bell * .62, length * .82),
            (bell, length - .012), (bell - .002, length - .002), (bell - .012, length - .004),
            (bell * .55, length * .82 - .006), (t * 1.25, length * .52), (0, length * .5)]
    acc = s.loft([(z, r, r, 2.0) for r, z in prof], seg=seg)
    eye = s.ball(1.0, (0, 0, length * .55), seg=12, scale=(t * 1.6, t * 1.6, .016))
    return along(acc, base, direction), along(eye, base, direction)


def banshee():
    """A tall thin wailing machine, 2.0 m: a cluster of resonator horns for a head (two big ones forward, two to the
    sides, two up and out), a ribbed corset torso on a thin spine, long hanging arms with claw fingers, ragged cables
    hanging from the head like hair, a tattered shroud from the waist down over a small hover skirt.
    Pivot: the bottom of the skirt (the game floats it ~0.3 m up). Eye: the horns' throats; the two forward ones at
    about (±0.1, -0.21, 1.8)."""
    s.clear_scene()
    rnd = random.Random(0xBA45)
    body, acc, eye = [], [], []
    # hover skirt: a squat bell with a rim, an emitter ring under it
    acc.append(s.lathe([(.13, 0.0), (.2, .006), (.25, .03), (.265, .06), (.25, .09), (.17, .13), (.1, .16), (.06, .2),
                        (0, .2)], seg=28))
    body.append(s.lathe([(.18, .1), (.23, .1), (.215, .15), (.13, .19), (0, .19)], seg=24))      # dome on the skirt
    # spine column from the skirt up into the pelvis (under the shroud), the pelvis, the waist
    body.append(s.rcyl(.045, .9, (0, .01, .62), seg=12, r=.01))
    body.append(s.loft([(.95, .11, .09, 2.6, 0, .01), (1.02, .14, .1, 2.8, 0, .01), (1.1, .1, .08, 2.6, 0, .01)],
                       seg=20, round_caps=.03))
    # ribbed corset torso: alternating rings, chest wider, leaning a hair forward
    rings = []
    z0, z1 = 1.08, 1.5
    nr = 13
    for k in range(nr + 1):
        z = z0 + (z1 - z0) * k / nr
        f = (z - z0) / (z1 - z0)
        a = .075 + .085 * math.sin(min(1, f * 1.15) * math.pi * .62)
        b = .06 + .045 * math.sin(min(1, f * 1.15) * math.pi * .62)
        k_rib = 1.0 if k % 2 == 0 else .9
        rings.append((z, a * k_rib, b * k_rib, 2.6, 0, -.012 * f))
    body.append(s.loft(rings, seg=20, round_caps=.02))
    acc.append(s.loft([(1.12, .035, .03, 2.0, 0, .075), (1.48, .035, .03, 2.0, 0, .07)], seg=10, round_caps=.02))  # spine ridge
    # shoulder yoke and arms
    body.append(s.loft([(1.44, .22, .075, 3.0, 0, -.005), (1.5, .24, .085, 3.2, 0, -.005), (1.55, .16, .07, 3.0, 0, 0)],
                       seg=20, round_caps=.02))
    for sx in (-1, 1):
        sh, el, wr = (sx * .225, 0, 1.49), (sx * .25, .04, 1.1), (sx * .245, -.04, .76)
        acc.append(s.ball(.045, sh, seg=12))
        body += L.chain([sh, el], [.03, .026], seg=10)
        acc.append(s.ball(.035, el, seg=12))
        body += L.chain([el, wr], [.028, .022], seg=10)
        acc.append(s.ball(.03, wr, seg=10))
        for k, (dx, dy) in enumerate(((-.02, -.03), (.0, -.035), (.02, -.025), (sx * .03, .01))):
            tip = (wr[0] + dx * .8 + sx * .005, wr[1] + dy * 1.4, wr[2] - .2 + abs(dx) * 1.5)
            mid = (wr[0] + dx, wr[1] + dy, wr[2] - .1)
            acc += L.claw(wr, tip, .011, bend=(V(mid) - (V(wr) + V(tip)) / 2), seg=6)
    # ribbed neck and the hub the horns grow from
    neck = [(1.5, .04)]
    for k in range(7):
        neck.append((1.53 + k * .03, .042 if k % 2 == 0 else .034))
    neck.append((1.74, .038))
    neck.append((1.74, 0))
    body.append(s.lathe([(r, z) for z, r in neck], seg=12))
    body.append(s.loft([(1.69, .07, .07, 2.4), (1.76, .1, .09, 2.6), (1.84, .1, .09, 2.6), (1.9, .06, .055, 2.4)],
                       seg=20, round_caps=.025))
    # horns: (base, direction, length, bell)
    horns = []
    for sx in (-1, 1):
        horns.append(((sx * .04, -.05, 1.8), (sx * .3, -1, .02), .25, .095))   # forward pair: the main voice
        horns.append(((sx * .07, 0, 1.79), (sx * 1, -.18, -.05), .22, .075))   # to the sides
        horns.append(((sx * .035, -.01, 1.86), (sx * .55, -.45, .7), .19, .065))  # up and out
    for b, d, ln, bell in horns:
        a, e = horn(b, d, ln, bell)
        acc.append(a)
        eye.append(e)
    acc.append(s.lathe([(.075, 1.72), (.082, 1.73), (.082, 1.75), (.07, 1.76)], seg=16))      # collar under the hub
    # cables hanging from the back and the sides of the hub like hair: some ending in frayed tips
    for k in range(9):
        a = math.radians(-60 + k * 15 + rnd.uniform(-4, 4)) + math.pi / 2      # behind and to the sides
        ox, oy = math.cos(a) * .07, math.sin(a) * .07
        start = (ox, oy, 1.78 + rnd.uniform(-.03, .03))
        drop = rnd.uniform(.35, .7)
        out = 1.55 + rnd.uniform(0, .5)
        pts = [start, (ox * out, oy * out + .02, start[2] - .08), (ox * (out + .2), oy * (out + .3) + .04, start[2] - drop * .5),
               (ox * (out + .1) + rnd.uniform(-.02, .02), oy * (out + .4) + .05, start[2] - drop)]
        body.append(cord(spline(pts, 3), .011, sides=4, radii=None))
        end = V(pts[-1])
        acc.append(s.rcyl(.016, .04, tuple(end + V((0, 0, .015))), seg=8, r=.004))           # plug / ferrule
    # tattered shroud from the waist down: a flaring cloth with a ragged hem in strips, a few folds
    hem = [rnd.uniform(.12, .42) for _ in range(28)]
    for k in range(0, 28, 3):
        hem[k] = min(hem[k], .16)     # some long tatters

    def shroud(u, v):
        th = 2 * math.pi * u
        k = int(round(u * 28)) % 28
        z_top, z_bot = 1.1, hem[k]
        z = z_top + (z_bot - z_top) * v
        f = (z_top - z) / (z_top - .2)
        fold = 1 + .1 * math.sin(9 * th + 1.3) * f
        a = (.12 + .17 * f) * fold
        b = (.1 + .15 * f) * fold
        return (a * math.cos(th), b * math.sin(th) + .01, z)
    body.append(cloth(shroud, 28, 8, thick=.012))
    return [s.part("Body", body), s.part("Accent", acc), s.part("Eye", eye)]


# --------------------------------------------------------------------------- maniac

M_HIP_X, M_HIP_Z, M_KNEE = 0.15, 1.04, 0.52


def maniac_leg(sign):
    """A long thin leg: a hip ball, a tapered thigh, a knee housing, a shin, a long three-toed claw foot with a spur."""
    x = sign * M_HIP_X
    hip, knee, ankle = V((x, .02, M_HIP_Z)), V((x + sign * .02, -.07, M_KNEE)), V((x + sign * .02, .03, .085))
    parts = [s.ball(.075, hip, seg=14)]
    parts += L.seg_between(hip, knee, .07, .05, n=2.6, seg=14, overlap=.03)
    parts.append(s.ball(.06, knee, seg=14))
    parts.append(s.rbox((.1, .07, .12), tuple(knee + V((0, -.04, .02))), r=.025, seg=2))  # kneecap plate
    parts += L.seg_between(knee, ankle, .052, .035, n=2.6, seg=12, overlap=.03)
    parts.append(s.ball(.045, ankle, seg=12))
    parts.append(s.rbox((.11, .2, .07), tuple(ankle + V((0, -.08, -.05))), r=.025, seg=2, taper=(.85, .8)))  # instep
    for dx in (-.04, 0, .04):                                                     # toes: long claws forward
        b = ankle + V((dx, -.15, -.06))
        parts += L.claw(b, b + V((dx * .6, -.13, -.015)), .022, bend=(0, -.01, .03), seg=8)
    parts += L.claw(ankle + V((0, .02, -.03)), ankle + V((0, .12, -.075)), .02, bend=(0, 0, .02), seg=8)  # heel spur
    obj = s.part("AccentLeg0" if sign < 0 else "AccentLeg1", parts)
    return s.split_at(obj, M_KNEE)


def maniac():
    """A lanky hunched humanoid, 2.0 m: thin legs, a narrow pelvis, a spine bent forward into heavy shoulders, a long
    narrow head thrust forward with one small cold eye, long arms reaching below the knees — a meat hook on the left,
    a three-claw clamp on the right — and an open cradle of bars across its back, lined with torn tarpaulin, where a
    carried player lies (across, along x) at about (0, 0.15, 1.72); rags at the hips.
    Legs: AccentLeg0 (x -0.15) / AccentLeg1 (x +0.15) from the hip z 1.04, knee z 0.52, feet on the floor."""
    s.clear_scene()
    rnd = random.Random(0x3A41)
    body, acc, eye = [], [], []
    # pelvis and the hunched trunk
    body.append(s.loft([(.94, .14, .1, 2.8, 0, .02), (1.04, .2, .12, 3.0, 0, .02), (1.13, .16, .1, 2.8, 0, .01)],
                       seg=20, round_caps=.03))
    spine = [(0, .04, 1.1), (0, .02, 1.25), (0, -.04, 1.4), (0, -.14, 1.55)]
    body.append(s.loft([(1.08, .13, .09, 2.6, 0, .03), (1.2, .1, .08, 2.6, 0, .02), (1.32, .14, .1, 2.6, 0, -.01),
                        (1.43, .2, .13, 2.6, 0, -.06), (1.53, .26, .15, 2.8, 0, -.12), (1.61, .27, .15, 2.8, 0, -.17),
                        (1.67, .2, .12, 2.6, 0, -.2), (1.71, .1, .08, 2.4, 0, -.22)], seg=24, round_caps=.02))  # hunched trunk
    for k in range(6):                                                                            # vertebrae
        p = V(spine[0]).lerp(V(spine[-1]), k / 5) + V((0, .085 + .02 * k / 5, 0))
        acc.append(s.rbox((.05, .06, .045), tuple(p + V((0, .0, 0))), r=.015, seg=1))
    # neck and the long narrow head thrust forward, one small cold eye in a deep socket
    body += L.chain([(0, -.18, 1.62), (0, -.24, 1.68), (0, -.27, 1.7)], [.05, .045, .045], seg=12, overlap=.02)
    head = s.loft([(-.06, .0, .0), (-.05, .05, .055, 2.4), (.0, .07, .08, 2.6), (.07, .065, .075, 2.6),
                   (.12, .045, .05, 2.4), (.15, .028, .028, 2.2), (.16, 0, 0)], seg=18, axis="-Y", loc=(0, -.25, 1.72))
    head = s.boolean(head, s.ball(.034, (0, -.38, 1.735), seg=16))                                # the socket
    body.append(head)
    acc.append(s.rbox((.14, .09, .03), (0, -.3, 1.79), r=.012, seg=2, rot=(math.radians(-8), 0, 0)))   # brow plate
    acc.append(s.rbox((.08, .1, .03), (0, -.33, 1.665), r=.01, seg=1, rot=(math.radians(12), 0, 0)))   # jaw plate
    eye.append(s.ball(.025, (0, -.372, 1.735), seg=12))
    # arms: long, hanging forward; a meat hook on the left, a three-claw clamp on the right
    for sx in (-1, 1):
        sh, el, wr = V((sx * .26, -.16, 1.56)), V((sx * .32, -.1, 1.14)), V((sx * .29, -.24, .78))
        acc.append(s.ball(.07, tuple(sh), seg=14))
        body += L.chain([sh, el], [.05, .04], seg=12, overlap=.02)
        acc.append(s.ball(.05, tuple(el), seg=12))
        body += L.chain([el, wr], [.045, .035], seg=12, overlap=.02)
        acc.append(s.rcyl(.045, .07, tuple(wr), seg=12, r=.01))                                  # wrist collar
        if sx < 0:      # meat hook
            stem_end = wr + V((0, -.02, -.16))
            acc += L.chain([wr, stem_end], [.02, .018], seg=8)
            hook = [stem_end, stem_end + V((0, -.05, -.07)), stem_end + V((0, -.02, -.16)), stem_end + V((0, .07, -.17)),
                    stem_end + V((0, .1, -.1))]
            acc.append(cord(spline(hook, 4), .024, sides=8, radii=[1, 1, 1, .95, .85, .7, .6, .5, .45, .35, .3, .25, .2, .15, .1, .08, .05][:17]))
        else:           # clamp: a palm block and three curved claws
            palm = wr + V((0, -.03, -.08))
            acc.append(s.rbox((.08, .1, .1), tuple(palm), r=.025, seg=2))
            for k, a in enumerate((-50, 70, 190)):
                d = V((math.cos(math.radians(a)) * .5, math.sin(math.radians(a)) * .5 - .2, -1)).normalized()
                b = palm + V((math.cos(math.radians(a)) * .03, math.sin(math.radians(a)) * .03 - .01, -.04))
                acc += L.claw(b, b + d * .22, .016, bend=tuple(-V((d.x, d.y, 0)) * .05), seg=8)
    # the cradle across the back: U ribs, rails along x, a torn tarpaulin lining, struts down to the shoulders
    cy, cz, cr, half = .15, 1.8, .16, .26
    for x in (-half, -half / 2, 0, half / 2, half):
        acc.append(cord([(x, cy + cr * math.cos(math.radians(a)), cz + cr * math.sin(math.radians(a)))
                         for a in range(180, 361, 15)], .014, sides=6))
    for a in (180, 230, 270, 310, 360):
        p = (cy + cr * math.cos(math.radians(a)), cz + cr * math.sin(math.radians(a)))
        acc.append(cord([(-half - .02, p[0], p[1]), (half + .02, p[0], p[1])], .013, sides=6))
    for sx in (-1, 1):
        acc += L.chain([(sx * .12, -.1, 1.58), (sx * .14, .1, 1.66)], [.025, .022], seg=8, overlap=.02)
    lin_t = [rnd.uniform(.0, .12) for _ in range(9)]

    def lining(u, v):
        x = -half + .03 + (2 * half - .06) * u
        k = int(round(u * 8))
        a = math.radians(200 + 150 * v)
        r = cr - .02 - .015 * math.sin(math.pi * u)
        y, z = cy + r * math.cos(a), cz + r * math.sin(a)
        if v > .8:                  # the back edge hangs over the cradle in torn tongues
            z -= lin_t[k] * (v - .8) / .2 * 3
        return (x, y, z)
    body.append(cloth(lining, 8, 8, thick=.012))
    # rags at the hips: a front and a back loincloth, torn

    def rag(front, seed):
        r2 = random.Random(seed)
        tears = [r2.uniform(.18, .38) for _ in range(6)]

        def fn(u, v):
            th = math.radians((-140 + 100 * u) if front else (40 + 100 * u))
            k = int(round(u * 5))
            z = 1.06 - tears[k] * v
            r = .155 + .05 * v
            return (r * math.cos(th) * 1.25, r * math.sin(th) * .82 + .02, z)
        return cloth(fn, 5, 4, thick=.01)
    body.append(rag(True, 1))
    body.append(rag(False, 2))
    objs = [s.part("Body", body), s.part("Accent", acc), s.part("Eye", eye)]
    objs += maniac_leg(-1) + maniac_leg(1)
    return objs


# --------------------------------------------------------------------------- cocoon

COCOON_LEN, STRAND = 1.9, 1.5
# (z, half-width x, half-depth y) of the bundle: feet, knees, hips, crossed arms, shoulders, head
COCOON_RINGS = [(0.0, 0, 0), (.02, .07, .07), (.08, .12, .12), (.2, .15, .14), (.45, .16, .14), (.7, .19, .15),
                (.95, .22, .16), (1.1, .23, .17), (1.25, .26, .2), (1.4, .29, .21), (1.48, .3, .2), (1.56, .22, .17),
                (1.62, .15, .15), (1.7, .14, .15), (1.79, .125, .13), (1.86, .08, .085), (1.9, 0, 0)]


def cocoon_dent(th, z):
    """Lumps of the wrapped body: a crease between the legs front and back, crossed arms on the chest, a face bump."""
    d = .05 * math.sin(3 * th + z * 7) * math.sin(z * 5.3 + 1) + .03 * math.sin(5 * th - z * 11)
    legs = max(0.0, 1 - abs(z - .45) / .4)
    for c in (-math.pi / 2, math.pi / 2):
        dd = (th - c + math.pi) % (2 * math.pi) - math.pi
        d -= .16 * legs * math.exp(-(dd / .28) ** 2)
    dd = (th + math.pi / 2 + math.pi) % (2 * math.pi) - math.pi
    d += .08 * max(0.0, 1 - abs(z - 1.2) / .12) * math.exp(-(dd / .9) ** 2)       # the forearms crossed on the chest
    d += .06 * max(0.0, 1 - abs(z - 1.72) / .07) * math.exp(-(dd / .5) ** 2)      # the face
    return d


def cocoon_size(z):
    for (z0, a0, b0), (z1, a1, b1) in zip(COCOON_RINGS, COCOON_RINGS[1:]):
        if z0 <= z <= z1:
            f = (z - z0) / max(1e-9, z1 - z0)
            return a0 + (a1 - a0) * f, b0 + (b1 - b0) * f
    return 0.0, 0.0


def cocoon_point(th, z, lift=0.0):
    a, b = cocoon_size(z)
    k = 1 + cocoon_dent(th, z)
    return V((a * k * math.cos(th) + lift * math.cos(th), b * k * math.sin(th) + lift * math.sin(th), z))


def cocoon():
    """The spider's cocoon a knocked-out player hangs in: a silk bundle 1.9 m long and 0.6 m across the shoulders,
    head up, the lumps of a wrapped body under it (legs pressed together, forearms crossed on the chest, a face), wound
    round and round with bands of thread, hanging from a thick twisted strand 1.5 m up from its top, a few guy threads
    from the strand onto the shoulders. One part Web (the web material). Pivot: the bottom tip (the feet)."""
    s.clear_scene()
    rnd = random.Random(0xC0C0)
    parts = []
    seg = 24
    bm = bmesh.new()
    loops = []
    for z, a, b in COCOON_RINGS:
        if a == 0:
            loops.append([bm.verts.new((0, 0, z))])
        else:
            loops.append([bm.verts.new(cocoon_point(2 * math.pi * j / seg, z)) for j in range(seg)])
    for lo, up in zip(loops, loops[1:]):
        if len(lo) == 1:
            for j in range(seg):
                bm.faces.new((lo[0], up[j], up[(j + 1) % seg]))
        elif len(up) == 1:
            for j in range(seg):
                bm.faces.new((lo[j], lo[(j + 1) % seg], up[0]))
        else:
            for j in range(seg):
                bm.faces.new((lo[j], lo[(j + 1) % seg], up[(j + 1) % seg], up[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts.append(bm)
    # wrapping bands: helices both ways round the bundle, threads a little proud of the shell
    for k in range(7):
        z0 = rnd.uniform(.05, .5)
        z1 = rnd.uniform(1.3, 1.85)
        turns = rnd.uniform(2.5, 4.5) * (1 if k % 2 else -1)
        ph = rnd.uniform(0, 2 * math.pi)
        n = int(abs(turns) * 12)
        pts = []
        for i in range(n + 1):
            f = i / n
            z = z0 + (z1 - z0) * f
            th = ph + 2 * math.pi * turns * f
            pts.append(cocoon_point(th, z, lift=.004))
        parts.append(cord(pts, rnd.uniform(.006, .011), sides=4))
    # a few tight bands (several turns close together): read as straps at a distance
    for zc in (.35, .9, 1.38, 1.66):
        pts = []
        n = 40
        for i in range(n + 1):
            f = i / n
            z = zc + .05 * f + .02 * math.sin(f * 12)
            pts.append(cocoon_point(2 * math.pi * 2.6 * f, z, lift=.005))
        parts.append(cord(pts, .012, sides=4))
    # the strand: three plies twisted round each other from the top of the bundle up 1.5 m
    top = COCOON_LEN - .05
    for p in range(3):
        pts = []
        n = 20
        for i in range(n + 1):
            f = i / n
            a = 2 * math.pi * (p / 3 + 4 * f)
            r = .012 * (1 - .4 * f) + .004
            pts.append((r * math.cos(a), r * math.sin(a), top + (STRAND + .05) * f))
        parts.append(cord(pts, .011, sides=4))
    # guy threads from the strand down onto the head and shoulders
    for k in range(5):
        th = 2 * math.pi * k / 5 + .3
        z_on = rnd.uniform(1.5, 1.78)
        a = cocoon_point(th, z_on, lift=-.003)
        b = V((0, 0, COCOON_LEN + rnd.uniform(.12, .3)))
        parts.append(cord([b, b.lerp(a, .5) + V((0, 0, -.02)), a], .004, sides=4))
    return [s.part("Web", parts)]


# --------------------------------------------------------------------------- net

NET_PROFILE = [(.06, .46, .44), (.45, .41, .37), (.9, .36, .3), (1.25, .34, .26), (1.42, .335, .24), (1.5, .31, .22),
               (1.56, .2, .17), (1.62, .145, .14), (1.72, .14, .14), (1.8, .1, .1), (1.86, .04, .04)]


def net_size(z):
    for (z0, a0, b0), (z1, a1, b1) in zip(NET_PROFILE, NET_PROFILE[1:]):
        if z0 <= z <= z1:
            f = (z - z0) / (z1 - z0)
            return a0 + (a1 - a0) * f, b0 + (b1 - b0) * f
    return NET_PROFILE[-1][1:]


def net():
    """A drone's weighted capture net thrown over a standing player: a diamond-mesh of cords draped from a gathered
    knot over the head and shoulders, flaring and folding down to a hem rope weighted with lead slugs on the floor.
    Parts: Body (cords, hem rope, knot), Accent (the weights). 1.9 m tall, 0.92 m across the hem; pivot on the floor
    in the middle."""
    s.clear_scene()
    rnd = random.Random(0x4E7)
    J, K = 12, 12
    zs = [.06 + (1.84 - .06) * (k / K) ** 0.92 for k in range(K + 1)]
    nodes = {}
    for k in range(K + 1):
        z = zs[k]
        a, b = net_size(z)
        fold = max(0.0, (1.5 - z) / 1.44)
        for j in range(J):
            th = 2 * math.pi * (j + .5 * k) / J
            r = 1 + .07 * fold * math.sin(5 * th + 1.1) + rnd.uniform(-.025, .025) * fold
            dz = rnd.uniform(-.012, .012) if 0 < k < K else 0
            nodes[(k, j)] = V((a * r * math.cos(th), b * r * math.sin(th), z + dz))
    cords = []
    for j in range(J):
        cords.append(cord([nodes[(k, j % J)] for k in range(K + 1)], .009, sides=4))
        cords.append(cord([nodes[(k, (j - k) % J)] for k in range(K + 1)], .009, sides=4))
    # hem rope through the bottom nodes, the knot at the top
    cords.append(cord(spline([nodes[(0, j)] for j in range(J)], 3, closed=True), .014, sides=6, closed=True))
    cords.append(s.lathe([(.0, 1.8), (.05, 1.81), (.065, 1.85), (.055, 1.89), (.025, 1.91), (0, 1.915)], seg=12))
    cords.append(cord([(0, 0, 1.9), (.03, -.02, 1.95), (.02, -.05, 1.93)], .01, sides=4))   # the knot's loose end
    weights = []
    for j in range(J):
        p = nodes[(0, j)]
        weights.append(s.rcyl(.03, .06, (p.x, p.y, .032), seg=8, r=.008, bevel_seg=1))
    return [s.part("Body", cords), s.part("Accent", weights)]


# --------------------------------------------------------------------------- goo puddle

def goo_puddle():
    """The cleaner robot's sticky puddle: a flat lobed blob reaching 1.0 m at its widest (scaled in game), 2 cm deep
    in the middle with a raised 3 cm rim and a rounded bead edge, a few bubbles on top. One part Goo; pivot on the
    floor in the middle."""
    s.clear_scene()
    seg = 48

    def outline(th):
        return .8 + .1 * math.sin(3 * th + .4) + .06 * math.sin(5 * th + 1.3) + .035 * math.sin(7 * th + 2.0)
    peak = max(outline(2 * math.pi * j / 720) for j in range(720))
    R = [outline(2 * math.pi * j / seg) / peak for j in range(seg)]
    prof = [(0, .02), (.3, .0205), (.55, .021), (.72, .022), (.84, .025), (.91, .029), (.95, .03), (.975, .028),
            (.992, .021), (1.0, .012), (.996, .004)]
    bm = bmesh.new()
    centre = bm.verts.new((0, 0, .02))
    rings = []
    for t, z in prof[1:]:
        ring = []
        for j in range(seg):
            th = 2 * math.pi * j / seg
            wob = .002 * math.sin(4 * th + t * 9) if t < .9 else 0
            ring.append(bm.verts.new((R[j] * t * math.cos(th), R[j] * t * math.sin(th), z + wob)))
        rings.append(ring)
    for j in range(seg):
        bm.faces.new((centre, rings[0][j], rings[0][(j + 1) % seg]))
    for lo, up in zip(rings, rings[1:]):
        for j in range(seg):
            bm.faces.new((lo[j], up[j], up[(j + 1) % seg], lo[(j + 1) % seg]))
    under = bm.verts.new((0, 0, prof[-1][1]))   # the underside: a fan (a lobed n-gon triangulates badly)
    for j in range(seg):
        bm.faces.new((rings[-1][(j + 1) % seg], rings[-1][j], under))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts = [bm]
    for (x, y, r) in ((.18, .12, .06), (-.3, .25, .04), (-.1, -.38, .05), (.45, -.2, .035), (.05, .55, .03), (-.55, -.1, .045)):
        parts.append(s.lathe([(r, .014), (r * .97, .014 + r * .3), (r * .75, .014 + r * .62), (r * .4, .014 + r * .8), (0, .014 + r * .85)],
                             seg=10, loc=(x, y, 0)))
    return [s.part("Goo", parts)]


# --------------------------------------------------------------------------- main

MODELS = {"robot_banshee": banshee, "robot_maniac": maniac, "cocoon": cocoon, "net": net, "goo_puddle": goo_puddle}
FLOOR = {"net", "goo_puddle"}


def build(name):
    objs = MODELS[name]()
    for o in objs:
        o.data.name = o.name
    meshfix.fix_objects(objs, floor=name in FLOOR, name=name, pinned=lambda n: n.startswith("AccentLeg"))
    return objs


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
    names = [a for a in argv[1:] if a in MODELS] or list(MODELS)
    for name in names:
        objs = build(name)
        t = tris(objs)
        print(f"MODEL {name}: {sum(t.values())} tris  " + "  ".join(f"{k} {v}" for k, v in sorted(t.items())))
        s.export_fbx(os.path.join(root, "Robots", name + ".fbx"), objs)
    print("DANGER C DONE")


if __name__ == "__main__":
    main()
