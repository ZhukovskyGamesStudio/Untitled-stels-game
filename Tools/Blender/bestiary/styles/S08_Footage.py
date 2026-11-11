"""S08 Footage — «Найденная плёнка»: 2000s consumer electronics come alive (Toy family: rounded grey/black plastic).

Language: every robot is a gadget from a found-footage tape — camcorders with lens barrels and flip-out screens,
boomboxes with speaker cones and carry handles, telescopic tripod legs with flip-lock collars and rubber feet,
VCRs, VHS cassettes, a printer, a discman, a subwoofer, a studio pedestal camera, a webcam, a stage spotlight,
a film reel. Rounded plastic boxes, stepped lens barrels, the glow is in glass, LCDs, VU meters and REC lamps.
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


def pbox(size, loc, r=.035, rot=(0, 0, 0), taper=(1.0, 1.0)):
    """A rounded plastic box."""
    return [s.rbox(size, loc, r=min(r, min(size) * .45), seg=2, rot=rot, taper=taper)]


def lens(r, length, base, direction=(0, -1, 0), seg=18):
    """(accent, glow): a stepped camcorder lens barrel with a ribbed focus ring; the glass glows."""
    L = length
    prof = [(r * 1.05, 0), (r * 1.1, L * .08), (r * 1.1, L * .35), (r * 1.18, L * .38), (r * 1.18, L * .62),
            (r * 1.1, L * .65), (r * 1.0, L * .9), (r * 1.08, L), (r * .8, L + .002)]
    barrel = s.lathe(prof, seg=seg)
    glass = s.rcyl(r * .82, .02, (0, 0, L - .004), seg=seg, r=.004, bevel_seg=1)
    return [orient(barrel, base, direction)], [orient(glass, base, direction)]


def tele(p0, p1, r0, r1, n=2):
    """(body, accent): a telescopic tube from p0 to p1, each section thinner, flip-lock collars at the joints."""
    a, b = Vector(p0), Vector(p1)
    body, acc = [], []
    for k in range(n):
        t0, t1 = k / n, (k + 1) / n
        r = r0 + (r1 - r0) * k / max(1, n - 1)
        q0, q1 = a.lerp(b, t0), a.lerp(b, t1)
        d = (q1 - q0)
        ext = d.normalized() * min(.02, d.length * .1)
        body.append(align(s.rcyl(r, d.length + 2 * ext.length, seg=12 if r > .03 else 8, r=r * .25, bevel_seg=1), q0 - ext, q1 + ext))
        if k:
            acc.append(align(s.rcyl(r * 1.45, .045, seg=12, r=.008, bevel_seg=1), q0 - d.normalized() * .02,
                             q0 + d.normalized() * .025))
    return body, acc


def speaker(r, base, direction=(0, -1, 0), seg=20):
    """(accent, body): a speaker — the surround ring and the cone with its dust cap."""
    sur = s.lathe([(r * .82, 0), (r, 0), (r * 1.04, r * .1), (r * .95, r * .2), (r * .82, r * .16)], seg=seg)
    cone = s.lathe([(r * .84, r * .12), (r * .55, r * .06), (r * .3, r * .02), (r * .28, r * .1), (r * .18, r * .17),
                    (0, r * .19)], seg=seg)
    return [orient(sur, base, direction)], [orient(cone, base, direction)]


def torus(R, r, loc, direction=(0, 0, 1), m=24, n=8):
    bm = bmesh.new()
    rings = []
    for i in range(m):
        a = i / m * TAU
        rings.append([bm.verts.new((math.cos(a) * (R + r * math.cos(b)), math.sin(a) * (R + r * math.cos(b)), r * math.sin(b)))
                      for b in (j / n * TAU for j in range(n))])
    for i in range(m):
        for j in range(n):
            bm.faces.new((rings[i][j], rings[(i + 1) % m][j], rings[(i + 1) % m][(j + 1) % n], rings[i][(j + 1) % n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return [orient(bm, loc, direction)]


def rubber_foot(loc, r=.09):
    return [s.lathe([(0, 0), (r, 0), (r * 1.08, r * .25), (r * .85, r * .6), (r * .45, r * .85), (0, r * .9)], seg=14, loc=loc)]


class Style(Lang):
    name = "S08_Footage"
    seg = 16
    soft = 0.5
    edge = 0.02
    hull_kind = "rounded"
    eye_kind = "lens"
    wheel_kind = "solid"

    def rseg(self):
        return 2

    # furniture: glossy 2000s plastic (every prop has its own design below; these serve the leftovers)
    leg_kind = "round"
    edge_kind = "soft"
    cushion_puff = 0.8
    handle_kind = "recess"
    foliage_kind = "blob"

    def _edge(self, size):
        return min(min(size) * .45, .04)

    def wheel(self, center, radius, width):
        """A skate wheel: a rounded tyre, a recessed hub, a bearing cap on both sides."""
        c, w, r = Vector(center), width, radius
        tyre = s.lathe([(r * .6, -w / 2), (r * .86, -w / 2), (r, -w * .22), (r, w * .22), (r * .86, w / 2), (r * .6, w / 2)],
                       seg=14, axis="X", loc=c)
        hub = s.rcyl(r * .63, w * .82, c, axis="X", seg=14, r=.006, bevel_seg=1)
        cap = s.rcyl(r * .22, w * 1.06, c, axis="X", seg=10, r=.005, bevel_seg=1)
        return [tyre, hub, cap]


# =========================================================================== walkers

def guard(L):
    """AV man: a camcorder for a head (lens forward, flip-out screen open like an ear, REC lamp), a boombox chest with
    two speaker cones and a carry handle, a VCR pelvis, telescopic antenna arms, tripod legs with flip-lock knees."""
    P = D.Parts()
    P.add("Body", pbox((.76, .28, .46), (0, 0, 1.36), r=.08))
    for sx in (-1, 1):
        acc, cone = speaker(.15, (sx * .2, -.13, 1.34))
        P.add("Accent", acc).add("Body", cone)
    P.add("Accent", pbox((.13, .03, .1), (0, -.14, 1.34), r=.012))
    P.add("Eye", pbox((.14, .02, .04), (0, -.145, 1.5), r=.008))
    P.add("Accent", [tube([(-.3, 0, 1.57), (-.25, 0, 1.72), (.25, 0, 1.72), (.3, 0, 1.57)], .03, res=1)])
    P.add("Body", pbox((.44, .24, .14), (0, 0, 1.0)))
    P.add("Accent", pbox((.22, .02, .03), (0, -.12, 1.0), r=.008))
    b, a = tele((0, 0, 1.04), (0, 0, 1.2), .055, .05, 1)
    P.add("Body", b)
    b, a = tele((0, .02, 1.56), (0, .03, 1.78), .045, .035, 2)
    P.add("Body", b).add("Accent", a + L.ball(.06, (0, .03, 1.74)))
    # the camcorder head
    P.add("Body", pbox((.26, .42, .22), (0, .06, 1.86), r=.06))
    P.eye(lens(.085, .14, (0, -.13, 1.84)))
    P.add("Accent", pbox((.08, .14, .07), (-.07, .24, 1.99), r=.02))
    P.add("Accent", [tube([(.06, -.08, 1.96), (.06, -.02, 2.03), (.06, .16, 2.03), (.06, .22, 1.96)], .02, res=1)])
    P.add("Accent", pbox((.2, .025, .13), (-.24, -.09, 1.86), r=.012, rot=(0, 0, .25)))
    P.add("Eye", pbox((.15, .01, .09), (-.24, -.104, 1.86), r=.006, rot=(0, 0, .25)))
    P.add("Accent", L.cyl(.03, .05, (-.13, -.08, 1.86)))
    P.add("Eye", L.ball(.022, (.09, -.15, 1.94)))
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.075, .08, (sx * .4, 0, 1.46), axis="X"))
        b, a = tele((sx * .42, 0, 1.44), (sx * .48, -.04, .98), .055, .04, 3)
        P.add("Body", b).add("Accent", a)
        P.add("Accent", pbox((.13, .17, .18), (sx * .49, -.05, .9), r=.05))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.cyl(.095, .18, (x, 0, D.GUARD_HIP_Z), axis="X"))
        P.add(leg, [s.rcyl(.09, .46, (x, 0, .68), seg=14, r=.02, bevel_seg=1, radius2=.075)])
        P.add(leg, pbox((.17, .14, .09), (x, -.01, .47), r=.025))
        P.add(leg, [s.rcyl(.06, .42, (x, 0, .26), seg=14, r=.015, bevel_seg=1)])
        P.add(leg, rubber_foot((x, -.05, 0), .13))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """Studio pedestal camera: a round dolly on casters (it glides), a telescopic column with a big steering ring,
    a broadcast camera with a long hooded lens, a monitor on its back, two long pan bars reaching back like arms."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, .06), (.36, .06), (.38, .1), (.36, .18), (.14, .22), (0, .22)]))
    for k in range(3):
        a = k / 3 * TAU + math.pi / 2
        c = Vector((math.cos(a) * .3, math.sin(a) * .3, .055))
        t = Vector((-math.sin(a), math.cos(a), 0))
        P.add("Accent", [align(s.rcyl(.055, .045, seg=12, r=.01, bevel_seg=1), c - t * .022, c + t * .022)])
        P.add("Body", pbox((.04, .08, .08), tuple(c + Vector((0, 0, .04))), r=.012, rot=(0, 0, a)))
    for (r, z0, z1) in ((.13, .2, .95), (.1, .9, 1.45), (.075, 1.4, 1.75)):
        P.add("Body", [s.rcyl(r, z1 - z0, (0, 0, (z0 + z1) / 2), seg=16, r=.015, bevel_seg=1)])
    for z in (.92, 1.42):
        P.add("Accent", L.cyl(.14 if z < 1 else .11, .05, (0, 0, z)))
    P.add("Accent", torus(.34, .022, (0, 0, 1.05), m=28, n=6))
    for k in range(3):
        a = k / 3 * TAU + .3
        P.add("Accent", L.rod((math.cos(a) * .1, math.sin(a) * .1, 1.05), (math.cos(a) * .34, math.sin(a) * .34, 1.05), .016))
    P.add("Accent", pbox((.24, .24, .12), (0, 0, 1.77), r=.03))
    P.add("Body", pbox((.44, .7, .36), (0, .08, 1.96), r=.08))
    acc, glow = lens(.12, .3, (0, -.26, 1.95), seg=20)
    P.add("Accent", acc + [orient(s.lathe([(.13, 0), (.17, .08), (.19, .1), (.15, .1)], seg=20), Vector((0, -.52, 1.95)),
                                  (0, -1, 0))])
    P.add("Eye", glow)
    P.add("Body", pbox((.3, .16, .22), (0, .34, 2.2), r=.04))
    P.add("Eye", pbox((.24, .01, .15), (0, .423, 2.2), r=.008))
    P.add("Eye", L.ball(.03, (.1, -.2, 2.12)))
    for sx in (-1, 1):
        P.add("Accent", [tube([(sx * .18, .3, 1.88), (sx * .38, .55, 1.74), (sx * .46, .8, 1.52)], .032, res=1)])
        P.add("Body", pbox((.08, .08, .22), (sx * .46, .82, 1.45), r=.035, rot=(.3, 0, 0)))
    return P


def hound(L):
    """VCR dog: a video recorder for a body with a VHS tape sticking out of its back like a fin, a handycam head whose
    long zoom lens is the sniffing snout, a rabbit-ear TV antenna, telescopic legs, a cable tail with a plug."""
    P = D.Parts()
    P.add("Body", pbox((.38, .84, .25), (0, 0, .8), r=.06))
    for sx in (-1, 1):
        P.add("Accent", pbox((.02, .4, .03), (sx * .19, -.05, .8), r=.008))
        P.add("Eye", pbox((.012, .1, .03), (sx * .193, .25, .8), r=.004))
    P.add("Accent", pbox((.05, .34, .2), (0, .1, 1.0), r=.015, rot=(.18, 0, 0)))
    for y in (.02, .18):
        P.add("Body", L.cyl(.045, .07, (0, y, 1.0), axis="X"))
    P.add("Body", pbox((.24, .06, .03), (0, .1, .93), r=.01))
    b, a = tele((0, -.36, .86), (0, -.52, .98), .055, .045, 1)
    P.add("Body", b)
    P.add("Body", pbox((.18, .32, .17), (0, -.58, 1.0), r=.05))
    acc, glow = lens(.075, .27, (0, -.71, .96), (0, -1, -.35))
    P.add("Accent", acc).add("Eye", glow)
    P.add("Eye", L.ball(.018, (.07, -.72, 1.06)))
    P.add("Accent", L.ball(.045, (0, -.55, 1.08)))
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .02, -.55, 1.08), (sx * .22, -.48, 1.42), .012) + L.ball(.025, (sx * .22, -.48, 1.42)))
    P.add("Accent", [tube([(0, .4, .78), (0, .56, .68), (0, .64, .8), (0, .6, .95)], .02, res=1)]
          + pbox((.05, .05, .09), (0, .6, .98), r=.012))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add("Body", pbox((.13, .13, .1), (x, y, .67), r=.03))
        P.add(leg, L.cyl(.075, .15, (x, y, .6), axis="X"))
        P.add(leg, [s.rcyl(.062, .3, (x, y, .45), seg=12, r=.015, bevel_seg=1)])
        P.add(leg, pbox((.12, .11, .07), (x, y, .31), r=.02))
        P.add(leg, [s.rcyl(.048, .28, (x, y, .17), seg=12, r=.012, bevel_seg=1)])
        P.add(leg, rubber_foot((x, y - .01, 0), .085))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """Webcam spider: a round webcam ball with one big lens and a REC lamp, a USB cable for a tail, eight thin
    plastic legs with button joints and rubber tips."""
    P = D.Parts()
    P.add("Body", L.ball(.17, (0, .02, .21)))
    P.add("Body", L.cyl(.172, .035, (0, .02, .21), axis="Y"))
    P.add("Body", pbox((.2, .26, .05), (0, .04, .05), r=.02))
    acc, glow = lens(.075, .07, (0, -.12, .22))
    P.add("Body", acc).add("Eye", glow)
    P.add("Eye", L.ball(.022, (.075, -.11, .29)))
    P.add("Body", [tube([(0, .17, .17), (0, .32, .12), (0, .42, .2)], .02, res=1)] + pbox((.05, .08, .03), (0, .45, .22), r=.01))
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .022) + L.cyl(.035, .04, tuple(knee)))
        f = foot + Vector((0, 0, .028))
        P.add(f"AccentLeg{i}Lower", L.rod(knee, f, .017) + L.ball(.03, tuple(f)))
    return P


# =========================================================================== wheeled

def listener(L):
    """Headphones on an AV cart: a two-shelf school AV trolley with a VCR below, an amplifier with glowing VU meters
    on top, a telescopic mast carrying a giant pair of headphones — the ear cups are its ears."""
    P = D.Parts()
    W = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .22, .12))
        P.add("Accent", L.rod((c[0] * .7, c[1], .32), (c[0] * 1.02, c[1], .22), .03))
    for z in (.33, .72):
        P.add("Body", pbox((.66, .86, .05), (0, 0, z), r=.02))
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Accent", L.cyl(.025, .42, (sx * .3, sy * .4, .53)))
    P.add("Body", pbox((.5, .36, .12), (0, 0, .41)))
    P.add("Accent", pbox((.22, .02, .03), (0, -.18, .42), r=.008))
    P.add("Body", pbox((.52, .36, .2), (0, .06, .84)))
    for sx in (-1, 1):
        P.add("Eye", pbox((.13, .02, .07), (sx * .13, -.12, .86), r=.01))
        P.add("Accent", L.cyl(.03, .03, (sx * .03, -.12, .8), axis="Y"))
    b, a = tele((0, .12, .92), (0, .12, 1.54), .045, .03, 3)
    P.add("Body", b).add("Accent", a)
    P.add("Accent", [tube([(-.35, .12, 1.22), (-.31, .12, 1.45), (0, .12, 1.56), (.31, .12, 1.45), (.35, .12, 1.22)],
                            .035, res=1)])
    for sx in (-1, 1):
        P.add("Body", [s.rcyl(.16, .1, (sx * .36, .12, 1.2), axis="X", seg=16, r=.03, bevel_seg=2)])
        P.add("Accent", [s.rcyl(.14, .06, (sx * .29, .12, 1.2), axis="X", seg=16, r=.025, bevel_seg=1)])
        P.add("Eye", L.cyl(.1, .015, (sx * .412, .12, 1.2), axis="X"))
    return P


def worker(L):
    """Printer porter: an inkjet printer on skate wheels, its paper tray sticking up at the back, a telescopic lift in
    front carrying a milk crate of VHS tapes, the status lamp under the fork."""
    P = D.Parts()
    W = [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .12, .1))
        P.add("Accent", L.rod((c[0] * .7, c[1], .12), (c[0] * .95, c[1], .12), .03))
    P.add("Body", pbox((.5, .62, .1), (0, 0, .16)))
    P.add("Body", pbox((.56, .5, .3), (0, .08, .36), r=.05))
    P.add("Accent", pbox((.42, .03, .03), (0, -.17, .46), r=.01))
    P.add("Accent", pbox((.46, .03, .38), (0, .35, .66), r=.015, rot=(-.35, 0, 0)))
    P.add("Body", pbox((.4, .03, .3), (0, .345, .7), r=.008, rot=(-.35, 0, 0)))
    for sx in (-1, 1):
        P.add("Body", pbox((.06, .2, .08), (sx * .22, -.24, .3), r=.02))
        b, a = tele((sx * .22, -.33, .22), (sx * .22, -.33, .98), .032, .025, 2)
        P.add("Body", b).add("Accent", a)
        P.add("Accent", L.cyl(.025, .04, (sx * .1, -.17, .46), axis="Y"))
    P.add("Body", pbox((.47, .09, .1), (0, -.35, .5), r=.025))
    P.add("Eye", pbox((.26, .02, .045), (0, -.4, .5), r=.01))
    P.add("Accent", pbox((.52, .44, .03), (0, -.15, .955), r=.01))
    P.add("Accent", pbox((.5, .42, .3), (0, -.15, 1.12), r=.04))
    P.add("Accent", pbox((.52, .44, .04), (0, -.15, 1.27), r=.015))
    for k, x in enumerate((-.14, -.04, .06, .16)):
        P.add("Body", pbox((.035, .2, .14), (x, -.15, 1.31 - (k % 2) * .03), r=.008))
    return P


def sweeper(L):
    """Discman: a round CD player with its lid flipped open behind like a shell, the disc on top, an LCD strip and
    buttons in front, headphone cables curling forward like whiskers to two earbuds; the lidar spins underneath."""
    P = D.Parts()
    W = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .08, .06))
        for sx in (-1, 1):
            P.add("Accent", pbox((.022, .05, .24), (c[0] + sx * .044, c[1], .19), r=.008))
    P.add("Body", L.lathe([(0, .28), (.34, .28), (.37, .31), (.37, .42), (.35, .45), (0, .45)], seg=28))
    P.add("Accent", L.cyl(.3, .014, (0, 0, .455), seg=28))
    P.add("Body", L.cyl(.05, .03, (0, 0, .465)))
    P.add("Accent", L.cyl(.03, .36, (0, .33, .465), axis="X"))
    P.add("Body", [s.rcyl(.34, .03, (0, .25, .79), seg=28, r=.01, bevel_seg=2, rot=(math.radians(-75), 0, 0))])
    P.add("Accent", [s.rcyl(.24, .012, (0, .23, .79), seg=24, r=.003, bevel_seg=1, rot=(math.radians(-75), 0, 0))])
    P.add("Eye", pbox((.18, .02, .045), (0, -.365, .38), r=.01))
    for k in (-1, 0, 1):
        P.add("Accent", L.cyl(.022, .03, (k * .07, -.325, .44)))
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.025, .04, (sx * .26, -.25, .4), axis="Y", rot=(0, 0, sx * .7)))
        P.add("Accent", [tube([(sx * .27, -.26, .4), (sx * .38, -.38, .38), (sx * .46, -.42, .3), (sx * .44, -.5, .22)],
                                .012, res=1)])
        P.add("Body", L.ball(.045, (sx * .44, -.52, .2)))
    P.add("Accent", L.cyl(.045, .1, (0, 0, .27)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + pbox((.08, .05, .04), (0, -.12, .22), r=.01))
    return P


def thumper(L):
    """Subwoofer stomper: a big speaker cabinet with a huge woofer cone, a boombox handle on top and VU meters, on
    rubber tracks; two telescopic stamps press speaker cones onto the floor (the glowing seismic pads)."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .56
        P.add("Accent", L.loft([(-.12, .26, .68, 3.0), (.12, .26, .68, 3.0)], axis="X", loc=(x, 0, .27)))
        for k in (-1.5, -.5, .5, 1.5):
            P.add("Body", L.cyl(.11, .04, (x + sx * .13, k * .3, .27), axis="X", seg=10))
    P.add("Body", pbox((.94, .9, .72), (0, .05, .86), r=.09))
    acc, cone = speaker(.3, (0, -.39, .86), seg=24)
    P.add("Accent", acc).add("Body", cone)
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.05, .04, (sx * .3, -.39, .6), axis="Y"))
        P.add("Eye", pbox((.14, .02, .06), (sx * .25, -.405, 1.12), r=.012))
    P.add("Accent", [tube([(-.36, .05, 1.2), (-.3, .05, 1.42), (.3, .05, 1.42), (.36, .05, 1.2)], .04, res=1)])
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", pbox((.12, .58, .1), (x, -.62, .62), r=.03))
        b, a = tele((x, -.85, .62), (x, -.85, .2), .055, .042, 2)
        P.add("Body", b).add("Accent", a)
        P.add("Body", [s.rcyl(.17, .16, (x, -.85, .14), seg=18, r=.03, bevel_seg=2, radius2=.13)])
        acc, cone = speaker(.16, (x, -.85, .062), (0, 0, -1), seg=18)
        P.add("Accent", acc + cone)
        P.add("Eye", L.cyl(.18, .03, (x, -.85, .015)))
    return P


# =========================================================================== tall, flying, mounted

def sorter(L):
    """Studio flash on a light stand: a tripod stand, a telescopic column, a flash head with a huge round reflector
    whose diffuser is the colour lens."""
    P = D.Parts()
    P.add("Accent", L.cyl(.07, .06, (0, 0, .38)))
    for k in range(3):
        a = k / 3 * TAU + math.pi / 2
        foot = (math.cos(a) * .3, math.sin(a) * .3, .035)
        P.add("Body", L.rod((math.cos(a) * .05, math.sin(a) * .05, .38), foot, .02))
        P.add("Accent", L.ball(.035, foot))
    b, a = tele((0, 0, .36), (0, 0, 1.12), .04, .03, 2)
    P.add("Body", b).add("Accent", a)
    P.add("Accent", pbox((.08, .08, .08), (0, .04, 1.14), r=.02) + L.cyl(.03, .1, (.06, .04, 1.14), axis="X"))
    P.add("Body", pbox((.24, .3, .24), (0, .2, 1.25), r=.05))
    P.add("Body", [orient(s.lathe([(.1, 0), (.15, .04), (.24, .13), (.27, .17), (.26, .18), (0, .18)], seg=28),
                          Vector((0, .06, 1.25)), (0, -1, 0))])
    P.add("Accent", torus(.265, .018, (0, -.11, 1.25), (0, -1, 0), m=28, n=6))
    P.add("Eye", L.cyl(.23, .02, (0, -.125, 1.25), axis="Y", seg=28))
    P.add("Accent", L.cyl(.03, .04, (.08, .36, 1.32), axis="Y"))
    return P


def drone(L):
    """Digicam copter: a compact digital camera with its lens pushed out and a flash window, four telescopic arms to
    ducted toy propellers."""
    P = D.Parts()
    P.add("Body", pbox((.36, .15, .21), (0, 0, 0), r=.05))
    P.add("Body", pbox((.08, .07, .19), (.15, -.05, 0), r=.03))
    acc, glow = lens(.07, .1, (-.03, -.07, -.02))
    P.add("Body", acc).add("Eye", glow)
    P.add("Body", pbox((.08, .02, .04), (.08, -.075, .06), r=.008))
    P.add("Eye", L.ball(.016, (-.13, -.075, .07)))
    P.add("Body", L.cyl(.022, .02, (.1, .0, .105)))
    for i, (x, y) in enumerate([(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]):
        b, a = tele((x * .3, y * .1, 0), (x, y, 0), .022, .016, 2)
        P.add("Body", b + a)
        P.add("Body", L.cyl(.032, .04, (x, y, .0)))
        d = Vector((x, y, 0)).normalized()
        for k in (-1, 1):
            P.add("Body", L.rod((x, y, .005), (x + k * d.x * .19, y + k * d.y * .19, .06), .01))
        P.add("Body", torus(.2, .018, (x, y, .065), m=16, n=5))
        rot = [s.rcyl(.03, .089, (x, y, .0645), seg=10, r=.008, bevel_seg=1)]
        rot += pbox((.34, .055, .012), (x, y, .072), r=.005, rot=(.25, 0, math.radians(45 if x * y > 0 else -45)))
        P.add(f"AccentRotor{i}", rot)
    return P


def turret(L):
    """Stage spotlight: a tripod light stand with a little control box whose LEDs glow (still), a yoke turning a
    spotlight can with cooling rings and four barn doors."""
    P = D.Parts()
    for k in range(3):
        a = math.pi / 2 + k * TAU / 3
        foot = (math.cos(a) * .4, math.sin(a) * .4, .03)
        P.add("Body", L.rod((math.cos(a) * .05, math.sin(a) * .05, .75), foot, .022) + rubber_foot((foot[0], foot[1], 0), .045))
    P.add("Body", L.cyl(.06, .06, (0, 0, .75)) + L.cyl(.04, .4, (0, 0, .93)))
    P.add("Body", pbox((.14, .12, .14), (0, -.05, .96), r=.03))
    P.add("Eye", pbox((.1, .02, .035), (0, -.112, .99), r=.008) + L.cyl(.06, .025, (0, 0, 1.1)))
    P.add("AccentHead", L.cyl(.055, .08, (0, 0, 1.14)))
    P.add("AccentHead", pbox((.38, .06, .04), (0, 0, 1.18), r=.015))
    for sx in (-1, 1):
        P.add("AccentHead", pbox((.03, .06, .18), (sx * .18, 0, 1.27), r=.012) + L.cyl(.035, .04, (sx * .16, 0, 1.3), axis="X"))
    P.add("AccentHead", L.lathe([(0, -.2), (.1, -.2), (.13, -.16), (.13, .14), (.15, .16), (.15, .2), (0, .2)], axis="-Y",
                                loc=(0, .0, 1.3)))
    for y in (.08, .14):
        P.add("AccentHead", L.cyl(.145, .02, (0, y, 1.3), axis="Y"))
    P.add("AccentHead", pbox((.24, .12, .014), (0, -.25, 1.484), r=.004, rot=(-.6, 0, 0)))
    P.add("AccentHead", pbox((.24, .12, .014), (0, -.25, 1.116), r=.004, rot=(.6, 0, 0)))
    for sx in (-1, 1):
        P.add("AccentHead", pbox((.014, .12, .2), (sx * .184, -.25, 1.3), r=.004, rot=(0, 0, sx * .6)))
    P.add("AccentHead", L.cyl(.12, .02, (0, -.2, 1.3), axis="Y"))
    return P


def vacuum(L):
    """Film reel: two flanges with round cut-outs, the wound film between them and its loose tail, a lens in the hub
    looking down."""
    P = D.Parts()
    low = s.rcyl(.36, .016, (0, 0, .008), seg=32, r=.004, bevel_seg=1)
    for k in range(5):
        a = k / 5 * TAU
        low = s.boolean(low, s.rcyl(.075, .06, (math.cos(a) * .2, math.sin(a) * .2, .008), seg=14, r=0))
    P.add("Body", [low])
    P.add("Body", L.cyl(.36, .016, (0, 0, .092), seg=32))
    P.add("Accent", L.cyl(.24, .07, (0, 0, .05), seg=28))
    P.add("Accent", L.cyl(.07, .04, (0, 0, -.005)))
    P.add("Eye", L.cyl(.05, .015, (0, 0, -.025)))
    P.add("Accent", [s.rbox((.05, .2, .006), (.27, -.12, .05), r=.002, seg=1, rot=(0, .2, .6))])
    return P


# =========================================================================== devices and sensor modules

def lidar(L):
    """A 360 camera on a telescopic stand: rubber base, a power LED ring (still), a drum with a lens that spins."""
    P = D.Parts()
    P.add("Body", rubber_foot((0, 0, 0), .17))
    b, a = tele((0, 0, .12), (0, 0, .4), .035, .028, 2)
    P.add("Body", b + a + L.cyl(.06, .03, (0, 0, .4)))
    P.add("Eye", L.cyl(.065, .02, (0, 0, .425)) + pbox((.04, .05, .03), (0, -.08, .45), r=.008))
    P.add("Body", L.rod((0, -.03, .43), (0, -.07, .45), .012))
    P.add("AccentHead", pbox((.18, .18, .12), (0, 0, .455), r=.05))
    acc, glow = lens(.04, .03, (0, -.085, .465))
    P.add("AccentHead", acc + glow)
    return P


def pir(L):
    """A baby monitor: a rounded plastic box on the wall, a small lens in front."""
    P = D.Parts()
    P.add("Body", pbox((.16, .07, .16), (0, .04, 0), r=.04))
    acc, glow = lens(.045, .04, (0, .005, 0))
    P.add("Body", acc).add("Eye", glow)
    return P


def plate(L):
    """A dance mat: a 3 x 3 grid of pads on a rubber slab, the border glows."""
    P = D.Parts()
    S, h = 1.1, .012
    P.add("Body", pbox((S - .07, S - .07, h), (0, 0, h / 2), r=.004))
    for i in (-1, 0, 1):
        for j in (-1, 0, 1):
            P.add("Accent", [s.rbox((.28, .28, .008), (i * .32, j * .32, .014), r=.003, seg=1)])
    b, e = .035, h + .004
    P.add("Eye", [s.rbox((S, b, e), (0, S / 2 - b / 2, e / 2), r=.003, seg=1),
                  s.rbox((S, b, e), (0, -S / 2 + b / 2, e / 2), r=.003, seg=1),
                  s.rbox((b, S - 2 * b, e), (S / 2 - b / 2, 0, e / 2), r=.003, seg=1),
                  s.rbox((b, S - 2 * b, e), (-S / 2 + b / 2, 0, e / 2), r=.003, seg=1)])
    return P


def security_camera(L):
    """A camcorder on a wall bracket with its screen flipped out."""
    P = D.Parts()
    P.add("Body", pbox((.16, .06, .22), (0, .27, 0), r=.025))
    P.add("Accent", L.rod((0, .26, -.04), (0, .1, -.06), .022) + L.ball(.035, (0, .1, -.06)))
    P.add("Accent", pbox((.15, .27, .13), (0, -.02, -.02), r=.04))
    acc, glow = lens(.05, .07, (0, -.15, -.025))
    P.add("Accent", acc).add("Eye", glow)
    P.add("Accent", pbox((.015, .1, .07), (-.08, -.04, -.02), r=.005))
    P.add("Eye", L.ball(.012, (.05, -.155, .03)))
    return P


def laser_emitter(L):
    """A tower speaker: a rounded plastic column with seams, the emitter strip in front."""
    P = D.Parts()
    P.add("Body", pbox((.12, .12, 1.6), (0, 0, .8), r=.04))
    for z in (.1, 1.5):
        P.add("Body", pbox((.13, .13, .04), (0, 0, z), r=.015))
    P.add("Eye", pbox((.05, .04, 1.3), (0, -.065, .8), r=.012))
    return P


def attach_eye(L):
    P = D.Parts()
    P.add("Accent", pbox((.2, .12, .17), (0, .03, 0), r=.04))
    acc, glow = lens(.06, .06, (0, -.03, 0))
    P.add("Accent", acc).add("Eye", glow)
    return P


def attach_mic(L):
    """A handheld microphone standing on its handle."""
    P = D.Parts()
    P.add("Accent", L.cyl(.045, .02, (0, 0, .01)) + [s.rcyl(.03, .26, (0, 0, .14), seg=12, r=.008, bevel_seg=1, radius2=.04)])
    P.add("Body", L.cyl(.05, .03, (0, 0, .28)))
    P.add("Eye", L.ball(.062, (0, 0, .325)))
    return P


def attach_seismic(L):
    """A tripod leg stake: a clamp, a telescopic tube, a rubber foot that glows."""
    P = D.Parts()
    P.add("Accent", pbox((.16, .16, .03), (0, 0, .3), r=.012))
    b, a = tele((0, 0, .3), (0, 0, .02), .025, .02, 2)
    P.add("Accent", b + a)
    P.add("Eye", rubber_foot((0, 0, 0), .04))
    return P


def attach_nose(L):
    """A zoom lens snout."""
    P = D.Parts()
    acc, glow = lens(.07, .25, (0, 0, 0))
    P.add("Accent", acc).add("Eye", glow)
    return P


# --------------------------------------------------------------------------- the camcorder world's construction kit

def tripod(top, spread, leg_r=.02, rot0=math.pi / 2):
    """(body, accent): a camera tripod — a collar at `top`, three telescopic legs splayed to the floor on rubber feet."""
    x, y, z = top
    body, acc = [], [s.rcyl(leg_r * 2.4, .07, (x, y, z - .035), seg=12, r=.008, bevel_seg=1)]
    for k in range(3):
        a = rot0 + k * TAU / 3
        fx, fy = x + math.cos(a) * spread, y + math.sin(a) * spread
        b, c = tele((x + math.cos(a) * leg_r, y + math.sin(a) * leg_r, z - .04), (fx, fy, .03), leg_r, leg_r * .75, 2)
        body += b + [s.lathe([(0, 0), (leg_r * 1.7, 0), (leg_r * 1.5, leg_r * 1.2), (0, leg_r * 1.5)], seg=8, loc=(fx, fy, 0))]
        acc += c
    return body, acc


def case(size, loc, bottom_caps=True):
    """A flight case standing on the floor (loc = centre of its footprint): aluminium body, corner caps, edge band."""
    w, d, h = size
    x, y, z = loc
    out = [s.rbox((w - .02, d - .02, h), (x, y, z + h / 2), r=.015, seg=1)]
    c = .065
    for sx in (-1, 1):
        for sy in (-1, 1):
            for zz in ((z + c / 2, z + h - c / 2) if bottom_caps else (z + h - c / 2,)):
                out.append(s.rbox((c, c, c), (x + sx * (w / 2 - c / 2), y + sy * (d / 2 - c / 2), zz), r=.02, seg=1))
    out.append(s.rbox((w - .006, d - .006, .03), (x, y, z + h * .5), r=.008, seg=1))
    return out


def crt(w, h, loc, depth=.26):
    """(body, glow): a CRT monitor — a rounded bezel, a deep tapered back, the picture."""
    x, y, z = loc
    body = pbox((w, .08, h), (x, y, z), r=.03) + pbox((w * .78, depth, h * .8), (x, y + .03 + depth / 2, z - h * .04), r=.05)
    glow = [s.rbox((w - .08, .012, h - .08), (x, y - .043, z), r=.02, seg=2)]
    return body, glow


# =========================================================================== every prop as a gadget

def desk(L):
    """A plastic top on two flight cases, a CRT monitor and a keyboard."""
    P = D.Parts()
    P.add("Body", pbox((1.6, .8, .05), (0, 0, .74), r=.02))
    for x in (-.56, .56):
        P.add("Accent", case((.42, .76, .72), (x, 0, 0)))
    b, g = crt(.5, .4, (0, .06, .955))
    P.add("Accent", b + pbox((.44, .15, .025), (0, -.15, .775), r=.01)).add("Eye", g)
    return P


def terminal(L):
    """The console top on two camera tripods, three CRTs and a keyboard."""
    P = D.Parts()
    P.add("Body", pbox((1.6, .8, .05), (0, 0, .74), r=.02))
    for x in (-.55, .55):
        b, a = tripod((x, 0, .72), .22, .025)
        P.add("Accent", b + a)
    for x in (-.36, 0, .36):
        b, g = crt(.3, .36, (x, .02, .94), depth=.22)
        P.add("AccentConsole", b).add("Eye", g)
    P.add("AccentConsole", pbox((.52, .17, .025), (0, -.22, .778), r=.01))
    return P


def chair(L):
    """A camera operator's stool: a round seat on a tripod, a curved back on a chrome bar."""
    P = D.Parts()
    b, a = tripod((0, 0, .42), .27, .022)
    P.add("Accent", b + a + [s.rcyl(.05, .06, (0, 0, .41), seg=12, r=.01, bevel_seg=1)])
    P.add("Body", [s.lathe([(0, 0), (.24, 0), (.27, .03), (.26, .08), (.2, .11), (0, .12)], seg=20, loc=(0, 0, .42))])
    P.add("Accent", [tube([(0, .2, .5), (0, .25, .62), (0, .25, .72)], .018)])
    P.add("Body", pbox((.46, .09, .36), (0, .25, .88), r=.04, rot=(-.1, 0, 0)))
    return P


def server_rack(L):
    """A rolling AV rack stacked with VCRs and DVD decks, their displays glowing."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Body", pbox((.05, 1.0, 2.0), (sx * .325, 0, 1.0), r=.02))
    for z in (.04, 1.96):
        P.add("Body", pbox((.7, 1.0, .08), (0, 0, z), r=.02))
    for i in range(11):
        z = .18 + i * .16
        P.add("Body", pbox((.62, .9, .13), (0, -.01, z), r=.03))
        P.add("Eye", [s.rbox((.2, .012, .04), (-.12, -.462, z + .01), r=.005, seg=1)])
        P.add("Body", [s.rbox((.22, .012, .025), (.14, -.461, z), r=.005, seg=1)])
    return P


def cabinet(L):
    """Three flight cases stacked as drawers, latches on their fronts."""
    P = D.Parts()
    for i in range(3):
        P.add("Body", case((.9, .5, .47), (0, -.01, i * .465)))
        P.add("Accent", [s.rbox((.08, .03, .05), (sx * .25, -.265, i * .465 + .3), r=.01, seg=1) for sx in (-1, 1)]
              + [s.rbox((.22, .03, .025), (0, -.265, i * .465 + .16), r=.01, seg=1)])
    return P


def meeting_table(L):
    """A long table top on two big camera tripods."""
    P = D.Parts()
    P.add("Body", pbox((3.2, 1.3, .06), (0, 0, .75), r=.025))
    for x in (-1.0, 1.0):
        b, a = tripod((x, 0, .725), .5, .035)
        P.add("Accent", b + a)
    return P


def plant(L):
    """An office palm in a glossy plastic pot."""
    P = D.Parts()
    P.add("Accent", [s.lathe([(0, 0), (.15, 0), (.17, .03), (.21, .38), (.22, .42), (.2, .44), (0, .44)], seg=20)])
    P.add("Body", [s.rcyl(.035, .66, (0, 0, .73), seg=10, r=.008, bevel_seg=1, radius2=.025)])
    fronds = []
    for k in range(8):
        a = k / 8 * TAU
        c, sn = math.cos(a), math.sin(a)
        fronds.append(tube([(0, 0, 1.04), (c * .18, sn * .18, 1.18), (c * .33, sn * .33, 1.05), (c * .34, sn * .34, .9)], .028,
                           radii=[1, 1.4, 1, .4]))
    for k in range(3):
        a = k / 3 * TAU + .5
        fronds.append(tube([(0, 0, 1.04), (math.cos(a) * .08, math.sin(a) * .08, 1.28), (math.cos(a) * .14, math.sin(a) * .14, 1.37)],
                           .026, radii=[1, 1.3, .4]))
    P.add("Body", fronds + [s.ball(.06, (0, 0, 1.05), seg=10)])
    return P


def kitchen_counter(L):
    """Rounded plastic cupboards under a top, a microwave with a glowing window and a button column."""
    P = D.Parts()
    for i in range(4):
        x = -.9 + i * .6
        P.add("Body", pbox((.595, .64, .86), (x, 0, .45), r=.05) + pbox((.55, .58, .06), (x, .02, .03), r=.02))
        P.add("Accent", [s.rbox((.25, .02, .025), (x, -.322, .8), r=.008, seg=1)])
    P.add("Accent", pbox((2.44, .7, .05), (0, 0, .905), r=.02))
    P.add("Accent", pbox((.5, .4, .3), (.7, .02, 1.075), r=.05) + [s.rcyl(.02, .02, (.88, -.18, 1.13 - k * .06), axis="Y", seg=8,
                                                                          r=.004, bevel_seg=1) for k in range(3)])
    P.add("Eye", [s.rbox((.3, .012, .2), (.64, -.182, 1.075), r=.02, seg=2)])
    return P


def shelf(L):
    """A chrome media rack: round posts, plastic shelves, rows of VHS tapes and a CRT."""
    P = D.Parts()
    for x in (-.97, .97):
        for y in (-.27, .27):
            P.add("Body", [s.rcyl(.025, 2.0, (x, y, 1.0), seg=10, r=.006, bevel_seg=1)])
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", pbox((2.0, .6, .035), (0, 0, h), r=.015))
    for h in (.0775, .4675, 1.0675):
        for k in range(3):
            P.add("Accent", [s.rbox((.42, .19, .2), (-.65 + k * .62, -.05, h + .1), r=.01, seg=1)])
    b, g = crt(.5, .34, (-.3, -.15, 1.84), depth=.2)
    P.add("Accent", b).add("Accent", g)
    return P


def boxes(L):
    """Three flight cases."""
    P = D.Parts()
    P.add("Accent", case((.6, .6, .5), (0, 0, 0)) + case((.5, .5, .4), (.05, -.05, .495)) + case((.45, .45, .4), (.545, .1, 0)))
    return P


def water_cooler(L):
    """A glossy plastic tower with a drip tray and the bottle on top."""
    P = D.Parts()
    P.add("Body", pbox((.34, .34, 1.0), (0, 0, .5), r=.08))
    P.add("Body", pbox((.2, .08, .03), (0, -.15, .55), r=.01) + [s.rcyl(.02, .04, (sx * .05, -.17, .72), axis="Y", seg=8, r=.004,
                                                                         bevel_seg=1) for sx in (-1, 1)])
    P.add("Eye", L.lathe([(0, 0), (.06, 0), (.06, .05), (.14, .1), (.15, .32), (.12, .42), (0, .46)], loc=(0, 0, .985)))
    return P


def whiteboard(L):
    """A pull-up projection screen: the roll case on the floor, a telescopic pole, the screen hung from a bar."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.8, .02, 1.0), (0, 0, 1.47), r=.006, seg=1)])
    P.add("Accent", pbox((1.9, .08, .1), (0, 0, .05), r=.04) + pbox((1.82, .05, .04), (0, 0, 1.98), r=.015))
    b, a = tele((0, 0, .09), (0, 0, 1.97), .02, .014, 3)
    P.add("Accent", b + a)
    return P


def sofa(L):
    """A pleather sofa of fat capsule cushions on a chrome tube sled."""
    P = D.Parts()
    P.add("Body", pbox((.99, .74, .2), (-.5, -.04, .42), r=.09) + pbox((.99, .74, .2), (.5, -.04, .42), r=.09)
          + pbox((2.0, .26, .48), (0, .29, .64), r=.12, rot=(-.1, 0, 0)))
    P.add("Accent", pbox((2.0, .76, .14), (0, 0, .25), r=.04))
    for sx in (-1, 1):
        x = sx * 1.06
        P.add("Accent", [tube([(x, -.4, .02), (x, .4, .02), (x, .4, .6), (x, -.38, .6), (x, -.4, .3), (x, -.4, .02)], .025,
                              smooth=False), s.rbox((.12, .06, .06), (sx * .99, .37, .3), r=.01, seg=1)])
    return P


def toilet_stall(L):
    """Rounded plastic panels on chrome feet with a chrome top rail, a plastic toilet."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Body", pbox((.04, 1.5, 1.86), (sx * .5, 0, 1.05), r=.018))
    P.add("Body", pbox((1.04, .04, 1.86), (0, .755, 1.05), r=.018))
    P.add("Accent", [tube([(-.5, -.75, 1.99), (-.5, .76, 1.99), (.5, .76, 1.99), (.5, -.75, 1.99)], .015, smooth=False)])
    for sx in (-1, 1):
        for y in (-.7, 0.0):
            P.add("Body", [s.rcyl(.025, .14, (sx * .5, y, .07), seg=8, r=.004, bevel_seg=1)])
    P.add("Accent", L.lathe([(0, 0), (.15, 0), (.2, .3), (.22, .4), (0, .42)], loc=(0, .4, 0)) + pbox((.36, .14, .36), (0, .66, .55), r=.05))
    return P


def partition(L):
    """A folding screen of three rounded plastic panels on chrome hinges."""
    P = D.Parts()
    for k in (-1, 0, 1):
        P.add("Body", pbox((.52, .03, 1.24), (k * .55, 0, .64), r=.08))
    P.add("Accent", [s.rcyl(.02, 1.2, (sx * .275, 0, .62), seg=8, r=.005, bevel_seg=1) for sx in (-1, 1)]
          + [s.rbox((.12, .05, .04), (x, 0, .02), r=.01, seg=1) for x in (-.7, 0, .7)])
    return P


def bookshelf(L):
    """A rounded plastic media tower full of CD and DVD cases."""
    P = D.Parts()
    P.add("Body", pbox((.06, .35, 2.0), (-.47, 0, 1.0), r=.025) + pbox((.06, .35, 2.0), (.47, 0, 1.0), r=.025)
          + pbox((.96, .02, 1.96), (0, .165, 1.0), r=.008))
    for h in (.03, .5, 1.0, 1.5, 1.97):
        P.add("Body", pbox((.9, .33, .035), (0, 0, h), r=.012))
    for i, h in enumerate((.047, .517, 1.017, 1.517)):
        x = -.42
        for k in range(5 + i % 2):
            w = .1 + .04 * ((k + i) % 3)
            hh = .3 - .06 * ((k * 7 + i) % 3)
            P.add("Accent", [s.rbox((w, .24, hh), (x + w / 2, .03, h + hh / 2), r=.01, seg=1)])
            x += w + .02
    return P


def printer(L):
    """A bulky 2000s inkjet tower: rounded plastic, a paper stack on the top, a tray, a glowing LCD."""
    P = D.Parts()
    P.add("Body", pbox((.6, .6, .95), (0, 0, .475), r=.08) + pbox((.56, .52, .14), (0, .02, 1.0), r=.05))
    P.add("Accent", pbox((.36, .3, .03), (-.1, -.35, .82), r=.012, rot=(math.radians(8), 0, 0)) + pbox((.06, .2, .06), (-.1, -.25, .78), r=.015)
          + pbox((.4, .3, .02), (0, .1, 1.075), r=.006))
    P.add("Eye", [s.rbox((.16, .03, .07), (.18, -.245, 1.0), r=.02, seg=2)])
    return P


def fridge(L):
    """A rounded plastic fridge with long recessed handles and a magnet note."""
    P = D.Parts()
    P.add("Body", pbox((.7, .7, 1.76), (0, 0, .9), r=.08) + pbox((.6, .6, .05), (0, 0, .025), r=.01))
    P.add("Body", pbox((.66, .04, .48), (0, -.35, 1.5), r=.05) + pbox((.66, .04, 1.16), (0, -.35, .66), r=.05))
    P.add("Accent", pbox((.04, .04, .4), (.28, -.375, 1.35), r=.015) + pbox((.04, .04, .5), (.28, -.375, 1.0), r=.015)
          + pbox((.12, .015, .14), (-.15, -.372, 1.1), r=.006))
    return P


def coffee_machine(L):
    """A capsule coffee maker: a glossy rounded tower, a drip tray, a cup, a glowing round button."""
    P = D.Parts()
    P.add("Body", pbox((.3, .36, .48), (0, .02, .24), r=.07))
    P.add("Accent", pbox((.24, .16, .03), (0, -.17, .03), r=.01) + [s.lathe([(0, 0), (.04, 0), (.046, .08), (0, .08)], seg=14,
                                                                              loc=(0, -.17, .045))]
          + pbox((.14, .1, .06), (0, -.14, .3), r=.02))
    P.add("Eye", [s.rcyl(.03, .02, (0, -.165, .42), axis="Y", seg=14, r=.004, bevel_seg=1)])
    return P


def round_table(L):
    """A round top on a camera tripod."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.5, .04, (0, 0, .74), seg=28, r=.015, bevel_seg=2)])
    b, a = tripod((0, 0, .72), .38, .03)
    P.add("Accent", b + a)
    return P


def armchair(L):
    """An egg-pod chair on a pedestal, a cushion inside."""
    P = D.Parts()
    shell = s.lathe([(0, .1), (.3, .1), (.4, .25), (.44, .45), (.4, .7), (.28, .85), (0, .87)], seg=22)
    s.place(shell, scale=(1, .86, 1))
    shell = s.boolean(shell, s.ball(.36, (0, -.28, .55), seg=16, scale=(1.05, 1, 1.0)))
    P.add("Accent", [shell, s.rcyl(.28, .06, (0, 0, .03), seg=20, r=.015, bevel_seg=1), s.rcyl(.06, .1, (0, 0, .1), seg=12, r=.01,
                                                                                              bevel_seg=1)])
    P.add("Body", pbox((.56, .66, .14), (0, -.05, .3), r=.06) + pbox((.5, .14, .36), (0, .18, .52), r=.06))
    return P


def coffee_table(L):
    """A road case on casters as a coffee table."""
    P = D.Parts()
    P.add("Accent", case((1.0, .55, .36), (0, 0, .05)))
    P.add("Body", pbox((.97, .52, .03), (0, 0, .405), r=.01))
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", [s.rcyl(.028, .02, (sx * .42, sy * .2, .028), axis="X", seg=10, r=.005, bevel_seg=1),
                           s.rbox((.05, .05, .03), (sx * .42, sy * .2, .055), r=.008, seg=1)])
    return P


def tv_stand(L):
    """An AV cabinet with a glass door, a fat CRT TV with rabbit-ear antennae."""
    P = D.Parts()
    P.add("Body", pbox((1.4, .4, .5), (0, 0, .25), r=.04) + pbox((.5, .02, .1), (-.3, -.2, .38), r=.01))
    P.add("Accent", pbox((.6, .02, .3), (.3, -.2, .22), r=.01))
    b, g = crt(1.0, .74, (0, -.1, .87), depth=.24)
    P.add("Accent", b + [s.ball(.04, (0, .05, 1.14), seg=10)]).add("Eye", g)
    for sx in (-1, 1):
        P.add("Accent", L.rod((0, .05, 1.14), (sx * .3, .06, 1.43), .008) + [s.ball(.02, (sx * .3, .06, 1.43), seg=8)])
    return P


def reception_desk(L):
    """A rounded plastic counter with a curved front, a worktop behind on two pedestals, a CRT."""
    P = D.Parts()
    P.add("Body", [s.loft([(0, 1.25, .15, 4.0, 0, -.33), (1.11, 1.25, .15, 4.0, 0, -.33)], seg=28, round_caps=.04)])
    P.add("Body", pbox((.4, .6, .72), (-.8, .08, .36), r=.04) + pbox((.4, .6, .72), (.8, .08, .36), r=.04))
    P.add("Accent", pbox((2.5, .3, .04), (0, -.33, 1.12), r=.015) + pbox((2.3, .7, .04), (0, .03, .74), r=.015))
    b, g = crt(.42, .32, (.4, .05, .92), depth=.22)
    P.add("Eye", g + b)
    return P


def trash_bin(L):
    """A swing-top plastic bin."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.15, 0), (.16, .02), (.18, .34), (0, .34)]))
    P.add("Accent", L.lathe([(0, .33), (.19, .33), (.19, .37), (.16, .42), (.08, .44), (0, .44)])
          + [s.rbox((.16, .03, .06), (0, -.13, .4), r=.01, seg=1)])
    return P


def coat_rack(L):
    """A light stand: a tripod, a telescopic pole, a T-bar with knobs, two coats."""
    P = D.Parts()
    b, a = tripod((0, 0, .5), .24, .02, rot0=-math.pi / 2)
    P.add("Accent", b + a)
    b, a = tele((0, 0, .46), (0, 0, 1.75), .022, .016, 3)
    P.add("Accent", b + a + [s.rbox((.5, .03, .03), (.02, 0, 1.76), r=.01, seg=1)] + [s.ball(.022, (x, 0, 1.78), seg=8) for x in (-.22, .27)])
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.36)) + L.cushion((.3, .12, .55), (-.08, .1, 1.38), rot=(0, 0, 2.1)))
    return P


def lockers(L):
    """Three rounded plastic lockers with combination dials and vent slots."""
    P = D.Parts()
    for x in (-.3, 0, .3):
        P.add("Body", pbox((.29, .5, 1.9), (x, 0, .95), r=.04))
        P.add("Accent", [s.rcyl(.04, .03, (x + .07, -.26, 1.0), axis="Y", seg=14, r=.006, bevel_seg=1)]
              + [s.rbox((.16, .015, .02), (x, -.255, 1.72 - k * .045), r=.006, seg=1) for k in range(3)])
    return P


def pallet(L):
    """A plastic pallet stacked with flight cases."""
    P = D.Parts()
    P.add("Accent", pbox((1.2, 1.0, .04), (0, 0, .13), r=.015))
    for x in (-.5, 0, .5):
        for y in (-.4, 0, .4):
            P.add("Accent", pbox((.14, .14, .12), (x, y, .06), r=.03))
    P.add("Body", sum([case((.55, .45, .4), (x, y, .15), False) for x in (-.3, .3) for y in (-.24, .24)], [])
          + sum([case((.55, .45, .39), (-.3, y, .545), False) for y in (-.24, .24)], []))
    return P


def wash_basin(L):
    """A rounded vanity with two round basins, chrome taps and a pill-shaped mirror."""
    P = D.Parts()
    P.add("Body", pbox((1.6, .46, .82), (0, .02, .41), r=.06) + pbox((1.6, .5, .06), (0, 0, .85), r=.02)
          + pbox((1.6, .03, .4), (0, .24, 1.06), r=.01))
    for x in (-.4, .4):
        P.add("Accent", L.lathe([(0, 0), (.15, 0), (.19, .06), (.19, .1), (0, .1)], loc=(x, -.02, .85)))
        P.add("Accent", [tube([(x, .17, .87), (x, .17, 1.02), (x, .08, 1.0)], .016)])
    P.add("Eye", [s.rbox((1.5, .02, .76), (0, .235, 1.53), r=.3, seg=3)])
    return P


def vending_machine(L):
    """A DVD rental kiosk: a rounded plastic tower, a big glowing touch screen, a disc slot and a keypad."""
    P = D.Parts()
    P.add("Body", pbox((.9, .8, 1.9), (0, -.01, .95), r=.12))
    P.add("Eye", [s.rbox((.6, .02, .8), (-.05, -.405, 1.25), r=.04, seg=2), s.rbox((.7, .02, .1), (0, -.405, 1.78), r=.03, seg=2)])
    P.add("Accent", pbox((.3, .03, .05), (-.05, -.41, .72), r=.012) + pbox((.18, .03, .2), (.3, -.41, .7), r=.02)
          + pbox((.5, .03, .18), (0, -.41, .3), r=.03))
    return P


def ping_pong_table(L):
    """The table top on two camera tripods, a net between chrome posts."""
    P = D.Parts()
    P.add("Body", pbox((2.74, 1.52, .04), (0, 0, .74), r=.015))
    for x in (-.8, .8):
        b, a = tripod((x, 0, .72), .55, .035)
        P.add("Accent", b + a)
    P.add("Eye", [s.rbox((.02, 1.66, .15), (0, 0, .835), r=.004, seg=1)])
    P.add("Accent", [s.rcyl(.02, .18, (0, sy * .845, .84), seg=8, r=.004, bevel_seg=1) for sy in (-1, 1)])
    return P


def bean_bag(L):
    """An inflatable chair: two fat plastic rings and a small back ring."""
    P = D.Parts()
    P.add("Body", torus(.3, .13, (0, 0, .13), m=20, n=10) + torus(.27, .12, (0, 0, .37), m=20, n=10)
          + [s.rcyl(.22, .4, (0, 0, .25), seg=16, r=.02, bevel_seg=1)] + torus(.12, .07, (0, .25, .52), (0, 1, 0), m=16, n=8))
    return P


def arcade_machine(L):
    """A base cabinet, a fat CRT on top, a marquee, a control panel with two game pads."""
    P = D.Parts()
    P.add("Body", pbox((.7, .7, 1.0), (0, .05, .5), r=.05))
    b, g = crt(.72, .58, (0, -.2, 1.29), depth=.48)
    P.add("Accent", b + pbox((.72, .3, .26), (0, -.12, 1.68), r=.05)).add("Eye", g)
    P.add("Accent", pbox((.72, .35, .06), (0, -.36, .95), r=.02, rot=(math.radians(-12), 0, 0)))
    for sx in (-1, 1):
        P.add("Accent", [s.ball(.06, (sx * .15, -.4, 1.0), seg=12, scale=(1.6, 1, .5))])
    return P


def safe(L):
    """A hotel room safe: a rounded metal box, a keypad with a glowing display, a round knob."""
    P = D.Parts()
    P.add("Body", pbox((.8, .7, 1.0), (0, 0, .5), r=.04) + pbox((.7, .04, .86), (0, -.355, .5), r=.03))
    P.add("Accent", pbox((.2, .03, .32), (.2, -.385, .62), r=.02) + [s.rcyl(.06, .04, (-.15, -.385, .55), axis="Y", seg=16, r=.008,
                                                                          bevel_seg=1)])
    P.add("Accent", [s.rbox((.04, .02, .03), (.2 + (k % 3 - 1) * .05, -.4, .52 + (k // 3) * .05), r=.006, seg=1) for k in range(9)])
    P.add("Eye", [s.rbox((.15, .012, .05), (.2, -.402, .74), r=.008, seg=1)])
    return P


def desk_lamp(L):
    """A little LED panel on a mini tripod."""
    P = D.Parts()
    P.add("Body", rubber_foot((0, 0, 0), .075) + [s.rcyl(.014, .26, (0, 0, .17), seg=10, r=.004, bevel_seg=1),
                                                    s.rcyl(.02, .04, (0, 0, .2), seg=10, r=.005, bevel_seg=1)]
          + [s.ball(.022, (0, 0, .3), seg=8)])
    P.add("Accent", pbox((.15, .05, .14), (0, -.16, .29), r=.015, rot=(.5, 0, 0)) + [align(s.rcyl(.01, .16, seg=8, r=0), (0, 0, .3),
                                                                                      (0, -.15, .3))])
    P.add("Eye", [s.rbox((.12, .012, .11), (0, -.188, .275), r=.008, seg=1, rot=(.5, 0, 0))])
    return P


def floor_lamp(L):
    """A studio strobe under a photo umbrella on a light stand."""
    P = D.Parts()
    b, a = tripod((0, 0, .5), .19, .02)
    P.add("Body", b + a + tele((0, 0, .46), (0, 0, 1.38), .02, .015, 2)[0])
    P.add("Accent", [s.lathe([(.2, 0), (.19, .06), (.15, .14), (.08, .2), (.02, .22), (.012, .27), (0, .27)], seg=16,
                             loc=(0, 0, 1.38))])
    P.add("Eye", [s.rcyl(.14, .03, (0, 0, 1.365), seg=16, r=.006, bevel_seg=1)])
    return P


def ceiling_lamp(L):
    """A plastic housing with two bare fluorescent tubes."""
    P = D.Parts()
    P.add("Body", pbox((1.24, .64, .05), (0, 0, -.025), r=.02))
    for y in (-.15, .15):
        P.add("Accent", [s.rbox((.04, .06, .05), (x, y, -.055), r=.01, seg=1) for x in (-.58, .58)])
        P.add("Eye", [s.rcyl(.025, 1.12, (0, y, -.055), axis="X", seg=10, r=.008, bevel_seg=1)])
    return P


def exit_sign(L):
    """A rounded plastic lightbox."""
    P = D.Parts()
    P.add("Body", pbox((.38, .26, .16), (0, 0, 0), r=.05))
    P.add("Eye", [s.rbox((.33, .3, .11), (0, 0, 0), r=.04, seg=2)])
    P.add("Accent", [s.rbox((.05, .31, .07), (-.1, 0, 0), r=.01, seg=1), s.rbox((.12, .31, .02), (.05, 0, 0), r=.006, seg=1)])
    return P


def hatch(L):
    """A floor subwoofer grille: concentric rings and a dust cap."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.75, .06, (0, 0, .03), seg=32, r=.015, bevel_seg=2)])
    P.add("Accent", torus(.3, .018, (0, 0, .06), m=24, n=6) + torus(.5, .018, (0, 0, .06), m=28, n=6) + torus(.68, .018, (0, 0, .06), m=32, n=6)
          + [s.lathe([(.12, 0), (.1, .02), (0, .03)], seg=16, loc=(0, 0, .055))])
    P.add("Eye", [s.rcyl(.9, .02, (0, 0, .01), seg=32, r=0)])
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
