"""Devices and the stationary/hovering ones: sorter (many-eyed stalk), drone (jellyfish), turret (anemone), vacuum
(ceiling leech), security camera (eyestalk), lidar (anglerfish lure), motion sensor (cluster of eyes), pressure plate
(flat mimic), laser emitter (spine column)."""
import math

import bmesh
from mathutils import Euler, Vector

import shapes as s
from common import Parts, V, lerp, ring_points  # noqa: F401
from lang import align  # noqa: F401


def along_y(rings, loc, seg=24, round_caps=0.0):
    """A loft lying along -y: rings (t, half-width, half-height, n[, x, z]) with t measured from loc towards -y."""
    bm = s.loft(rings, seg=seg, round_caps=round_caps)
    return s.place(bm, loc, rot=(math.pi / 2, 0, 0))


def turn(bms, centre, rot):
    """Rotate bmeshes (Euler XYZ, radians) about a centre."""
    m = Euler(rot, "XYZ").to_matrix()
    for bm in bms:
        bmesh.ops.rotate(bm, cent=Vector(centre), matrix=m, verts=bm.verts)
    return bms


def ring(r_in, r_out, z0, z1, loc=(0, 0, 0), seg=24, axis="Z"):
    """A real ring (washer) with a hole: a revolved closed profile."""
    bm = bmesh.new()
    prof = [(r_in, z0), (r_out, z0), (r_out, z1), (r_in, z1)]
    loops = []
    for k in range(seg):
        a = k / seg * math.tau
        c, sn = math.cos(a), math.sin(a)
        loops.append([bm.verts.new((c * r, sn * r, z)) for r, z in prof])
    n = len(prof)
    for k in range(seg):
        a, b = loops[k], loops[(k + 1) % seg]
        for j in range(n):
            bm.faces.new((a[j], b[j], b[(j + 1) % n], a[(j + 1) % n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rot = {"Z": (0, 0, 0), "-Z": (math.pi, 0, 0), "Y": (-math.pi / 2, 0, 0), "-Y": (math.pi / 2, 0, 0)}[axis]
    return s.place(bm, loc, rot=rot)


# =========================================================================== sorter

def sorter(L):
    """A many-eyed stalk, 1.5 m: root-feet gripping the floor, a thick knotted stalk leaning forward, a pod head tipped
    forward so its big colour lens shows from above under a heavy lid, eyes on stalks of different lengths rising from
    its crown. Contract: base (Accent) about r 0.3; the lens (Eye) around (0, -0.2, 1.25)."""
    P = Parts()
    P.add("Accent", L.lathe([(0, 0), (.19, 0), (.2, .03), (.15, .1), (.1, .18), (0, .18)]))                    # foot
    for k in range(6):
        a = k / 6 * math.tau + .3
        c, sn = math.cos(a), math.sin(a)
        P.add("Accent", L.tentacle([(c * .1, sn * .1, .14), (c * .23, sn * .23, .1), (c * .32, sn * .32, .03)], .055, .024,
                                   res=2, per=3))
    P.add("Body", L.tentacle([(0, .05, .1), (.03, .07, .45), (-.02, .04, .8), (0, -.03, 1.08)], .14, .1, per=4))  # stalk
    for z, y in ((.36, .07), (.66, .06), (.92, .02)):
        P.add("Body", L.ellipsoid((.0, y, z), (.15, .15, .07), seg=14))                                        # knots
    # the head, built level around C and tipped forward 30 degrees
    C = V((0, -.03, 1.24))
    head = [along_y([(0, .12, .12, 2.2), (.1, .23, .22, 2.2), (.22, .27, .26, 2.2), (.32, .24, .23, 2.2), (.37, .19, .19, 2.2)],
                    C + V((0, .2, 0)), seg=24, round_caps=.06)]
    acc, glow = L.eye(.15, C + V((0, -.2, 0)), depth=.06)
    lid = [s.tube([C + V((math.sin(a) * .2, -.16, math.cos(a) * .2)) for a in [math.radians(d) for d in range(-110, 111, 22)]],
                  .055, res=2, segments_per=4)]                                                                # heavy lid
    low = [s.tube([C + V((math.sin(a) * .19, -.17, math.cos(a) * .19)) for a in [math.radians(d) for d in range(130, 231, 25)]],
                  .028, res=2, segments_per=3)]
    tilt = (math.radians(-28), 0, 0)
    P.add("Body", turn(head + lid + low, C, tilt))
    P.add("Accent", turn(acc, C, tilt))
    P.add("Eye", turn(glow, C, tilt))
    # eyes on stalks from the crown, biased to the front and the top; tips look up
    stalks = [((-.45, -.45, .77), .24), ((.5, -.35, .78), .18), ((.05, -.55, .83), .13), ((-.15, .15, .98), .26),
              ((.7, .05, .7), .16), ((-.75, -.1, .65), .13), ((.25, .3, .92), .2)]
    for d, ln in stalks:
        d = V(d).normalized()
        base = C + d * .2
        tip = base + d * ln + V((0, 0, ln * .45))
        P.add("Body", L.tentacle([base, lerp(base, tip, .5) + V((0, 0, .03)), tip], .036, .022, res=2, per=3))
        P.add("Accent", [s.ball(.044, tip - V((0, 0, .012)), seg=10)])                                          # cup
        P.add("Eye", [s.ball(.036, tip + V((0, 0, .014)), seg=10)])
    return P


# =========================================================================== drone

ROTORS = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]


def drone(L):
    """A jellyfish, 0.95 m across: a tall domed bell with a scalloped rim, glowing crown and rim ocelli, four little
    bells cupping the rotors on arms, a few thick oral arms and tentacles trailing under it, a camera eye under the
    front. Contract: rotors at (+-.42, +-.42, .065), span .34; bell within z -0.1..0.12; eye near (0, -0.25, -0.1)."""
    P = Parts()
    P.add("Body", L.lathe([(0, -.08), (.27, -.08), (.33, -.06), (.345, -.03), (.33, .02), (.28, .07), (.19, .105), (.09, .12),
                           (0, .12)], seg=24))                                                               # bell
    for k in range(12):                                                                                     # scallops
        a = k / 12 * math.tau
        P.add("Body", L.ellipsoid((math.cos(a) * .32, math.sin(a) * .32, -.085), (.07, .07, .03), seg=6))
    P.add("Eye", [s.ball(1.0, (0, 0, .115), seg=16, scale=(.1, .1, .03))])                                     # crown glow
    P.add("Eye", L.eyes([(math.cos(k / 8 * math.tau + .2) * .36, math.sin(k / 8 * math.tau + .2) * .36, -.06)
                         for k in range(8)], .024, seg=8))
    for i, (x, y) in enumerate(ROTORS):
        d = V((x, y, 0)).normalized()
        P.add("Body", L.tentacle([d * .3 + V((0, 0, -.02)), d * .45 + V((0, 0, -.03)), V((x, y, -.01))], .03, .026,
                                 res=2, per=3))
        P.add("Body", L.lathe([(0, -.1), (.06, -.095), (.09, -.07), (.1, -.03), (.09, .0), (.06, .02), (0, .028)],
                              loc=(x, y, 0), seg=16))                                                       # little bell
        P.add("Body", L.cyl(.022, .04, (x, y, .04), seg=10))                                               # hub stem
        P.add("Eye", L.eyes([(x, y, -.1)], .028, seg=8))
        P.add(f"AccentRotor{i}", L.rotor((x, y, .065), .34))
    for k in range(4):                                                                                      # oral arms
        a = k / 4 * math.tau + math.pi / 4
        c, sn = math.cos(a), math.sin(a)
        P.add("Accent", L.tentacle([(c * .06, sn * .06, -.07), (c * .11, sn * .11, -.17), (c * .06, sn * .06, -.25),
                                    (c * .1, sn * .1, -.33)], .05, .015, res=2, per=3))
    for k in range(4):                                                                                      # tentacles
        a = k / 4 * math.tau
        c, sn = math.cos(a), math.sin(a)
        P.add("Body", L.tentacle([(c * .29, sn * .29, -.08), (c * .31, sn * .31, -.18), (c * .27, sn * .27, -.28)], .022, .008,
                                 res=1, per=3))
    P.add("Accent", L.tentacle([(0, -.16, -.07), (0, -.21, -.09)], .035, .03, res=2, per=2))
    acc, glow = L.eye(.05, (0, -.25, -.1), depth=.06)
    P.add("Accent", acc)
    P.add("Eye", glow)
    return P


# =========================================================================== turret

COLUMN = [(.06, .22), (.25, .165), (.5, .165), (.75, .13), (.97, .145), (1.08, .17)]


def column_radius(z):
    for (z0, r0), (z1, r1) in zip(COLUMN, COLUMN[1:]):
        if z0 <= z <= z1:
            return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return COLUMN[-1][1]


def turret(L):
    """An anemone, 1.45 m: a sucker foot with root tendrils whose tips glow, a fleshy column with big warts and ridges,
    a ring of large glowing pores under the oral disc (still), and the turning crown — a wide ring of tentacles around
    a fat ribbed siphon barrel with a flared mouth pointing forward.
    Contract: AccentHead within z 1.17..1.45, turning about x = y = 0; the Eye never on the head."""
    P = Parts()
    P.add("Body", L.lathe([(0, 0), (.27, 0), (.3, .02), (.27, .06), (.17, .11), (0, .11)]))                    # foot
    for k in range(5):
        a = k / 5 * math.tau + .6
        c, sn = math.cos(a), math.sin(a)
        tip = (c * .5, sn * .5, .03)
        P.add("Body", L.tentacle([(c * .2, sn * .2, .07), (c * .34, sn * .34, .06), (c * .44, sn * .44, .02), tip], .05, .022,
                                 res=2, per=3))
        P.add("Eye", L.eyes([tip], .032, seg=8))
    P.add("Body", L.loft([(z, r, r) for z, r in COLUMN], seg=20))                                            # column
    for z, a, r in ((.3, .4, .065), (.55, 2.6, .055), (.78, 4.4, .05), (.42, 5.5, .045), (.9, 1.3, .045)):  # warts
        rr = column_radius(z)
        P.add("Body", L.ellipsoid((math.cos(a) * rr, math.sin(a) * rr, z), (r, r, r * 1.3), seg=10))
    for a in (1.0, 3.3, 5.0):                                                                               # ridges
        pts = [(math.cos(a + k * .15) * (column_radius(z) - .005), math.sin(a + k * .15) * (column_radius(z) - .005), z)
               for k, z in enumerate((.15, .45, .75, 1.0))]
        P.add("Body", L.tentacle(pts, .03, .022, res=1, per=3))
    P.add("Body", L.lathe([(.15, 1.04), (.21, 1.1), (.24, 1.16), (.22, 1.18), (0, 1.18)], seg=24))           # collar lip
    P.add("Eye", L.eyes(ring_points((0, 0, 1.1), .185, .185, 8), .042, seg=10))                          # pores
    H = "AccentHead"
    P.add(H, L.lathe([(0, 1.165), (.22, 1.165), (.23, 1.19), (.17, 1.23), (0, 1.25)], seg=24))              # oral disc
    for k in range(13):
        a = k / 13 * math.tau + .2
        if abs(math.sin(a) + 1) < .25:      # leave the siphon's way clear in front
            continue
        c, sn = math.cos(a), math.sin(a)
        P.add(H, L.tentacle([(c * .16, sn * .16, 1.2), (c * .34, sn * .34, 1.24), (c * .48, sn * .48, 1.33),
                             (c * .5, sn * .5, 1.43)], .06, .016, res=2, per=3))
    # the siphon: a fat ribbed barrel with a flared mouth
    prof = [(0, 0)]
    for k in range(6):
        prof += [(.1, .02 + k * .06), (.085, .05 + k * .06)]
    prof += [(.1, .4), (.13, .45), (.12, .47), (.08, .45), (0, .45)]
    P.add(H, L.lathe(prof, axis="-Y", loc=(0, -.04, 1.31), seg=20))
    return P


# =========================================================================== vacuum

def vacuum(L):
    """A ceiling leech (the game hangs it on the ceiling: +z on the ceiling, -z looks into the room): a long tapered
    annulated body with ridged segment bands, a radial sucker mouth of concentric rings on the face with a glowing
    inner ring and throat, eyes along the front edge. Contract: within r 0.36; the lens (the fan's apex) under the
    centre."""
    P = Parts()
    rings = []
    n = 18
    for k in range(n + 1):
        t = k / n
        env = math.sin(math.pi * (.06 + .88 * t)) ** .8
        w = .23 * env * (.75 + .25 * t) * (1.06 if k % 2 else .97)
        h = .06 * env ** .6 * (1.08 if k % 2 else .96)
        rings.append((t * .68, max(w, .03), max(h, .02), 2.4, 0, .03))
    P.add("Body", [along_y(rings, (0, .34, 0), seg=24, round_caps=.02)])
    P.add("Body", L.ellipsoid((0, .12, .065), (.13, .17, .04), seg=16))                                     # mount hump
    for k in (3, 7, 11, 15):                                                                               # bands
        t = k / n
        env = math.sin(math.pi * (.06 + .88 * t)) ** .8
        y = .34 - t * .68
        w, h = .23 * env * (.75 + .25 * t) * 1.1, .06 * env ** .6 * 1.12
        P.add("Accent", [along_y([(0, w, h, 2.4, 0, .03), (.022, w, h, 2.4, 0, .03)], (0, y + .011, 0), seg=24)])
    # the sucker: concentric steps down to a glowing ring and throat
    P.add("Accent", L.lathe([(0, -.0), (.17, -.0), (.18, -.02), (.165, -.035), (0, -.035)], seg=24))          # lip
    P.add("Accent", L.lathe([(0, -.03), (.13, -.03), (.13, -.045), (0, -.045)], seg=24))                      # step
    P.add("Eye", [ring(.085, .115, -.052, -.04, seg=24)])                                                   # glow ring
    P.add("Accent", L.lathe([(0, -.04), (.088, -.04), (.088, -.055), (.06, -.06), (0, -.06)], seg=24))        # inner step
    for k in range(10):                                                                                     # teeth
        a = k / 10 * math.tau
        b = V((math.cos(a) * .075, math.sin(a) * .075, -.055))
        P.add("Accent", L.cone(b, V((math.cos(a) * .035, math.sin(a) * .035, -.07)), .012, seg=6))
    P.add("Eye", L.lathe([(.035, 0), (.03, -.012), (0, -.02)], loc=(0, 0, -.058), seg=16))                    # throat
    P.add("Eye", L.eyes([(sx * (.05 + k * .03), -.27 + k * .04, .0) for sx in (-1, 1) for k in range(3)], [.026, .022, .018] * 2,
                        seg=10))
    return P


# =========================================================================== security camera

def security_camera(L):
    """An eyestalk growing from a fleshy mound on the wall: a ribbed stalk, an eyeball sitting in a lidded socket
    that covers its back, a big glowing iris in front. Contract: the wall mound (Body, still) at y 0.23..0.3; the
    stalk, socket and eye (Accent, Eye) swivel at the bracket y 0.24; the iris at y ~ -0.24."""
    P = Parts()
    P.add("Body", L.ellipsoid((0, .265, 0), (.12, .035, .14), seg=18))                                       # mound
    for sx in (-1, 1):
        for sz in (-1, 1):
            P.add("Body", L.tentacle([(sx * .06, .275, sz * .07), (sx * .12, .285, sz * .12), (sx * .14, .29, sz * .17)],
                                     .03, .01, res=2, per=3))
    path = [V((0, .27, 0)), V((0, .18, -.06)), V((0, .05, -.06)), V((0, -.05, -.03))]
    P.add("Accent", L.tentacle(path, .036, .03, res=2, per=4))
    for k in range(7):                                                                                      # rings
        t = .12 + k * .12
        q = lerp(path[0], path[1], t * 3) if t < 1 / 3 else lerp(path[1], path[2], (t - 1 / 3) * 3) if t < 2 / 3 \
            else lerp(path[2], path[3], (t - 2 / 3) * 3)
        P.add("Accent", L.ellipsoid(q, (.048, .02, .048), seg=10))
    c = V((0, -.12, -.02))
    P.add("Accent", [s.ball(.095, c, seg=20)])                                                               # eyeball
    cup = s.ball(.11, c, seg=20)
    cut = s.ball(1.0, c + V((0, -.1, -.01)), seg=20, scale=(.14, .12, .1))
    P.add("Accent", [s.boolean(cup, cut)])                                                                   # socket + lid
    P.add("Eye", L.lens(.075, c + V((0, -.085, 0))))
    return P


# =========================================================================== lidar

def lidar(L):
    """An anglerfish lure: a squat fish with fangs on the floor, webbed fins with rays, big glowing eyes, its illicium
    curving up from the forehead through a large glowing collar to the lure, which spins (AccentHead).
    Contract: AccentHead z 0.39..0.52 about x = y = 0; the Eye still (fish eyes, collar)."""
    P = Parts()
    body = along_y([(0, .05, .05, 2), (.08, .1, .085, 2.4), (.2, .15, .125, 2.6), (.3, .165, .135, 2.8),
                    (.38, .15, .12, 2.8), (.42, .11, .085, 2.6), (.43, .07, .05, 2.4)], (0, .2, .135), seg=24, round_caps=.03)
    mouth = s.ball(1.0, (0, -.24, .1), seg=20, scale=(.14, .08, .045))
    P.add("Body", [s.boolean(body, mouth)])
    for sx in (-1, 1):
        for k in range(3):                                                                                  # teeth
            x = sx * (.03 + k * .03)
            P.add("Body", L.cone((x, -.18 + abs(x) * .4, .15), (x * 1.05, -.215 + abs(x) * .4, .085), .014, seg=6))
        P.add("Body", L.cone((sx * .05, -.215, .05), (sx * .055, -.255, .16), .018, seg=6))                  # fangs
        P.add("Body", L.cone((sx * .1, -.2, .06), (sx * .11, -.24, .14), .015, seg=6))
        P.add("Body", [s.ball(.058, (sx * .09, -.1, .22), seg=14)])                                          # eye bulges
        root = V((sx * .13, 0, .08))
        tips = [V((sx * .24, .08, .14)), V((sx * .28, .02, .08)), V((sx * .25, -.04, .02))]
        P.add("Body", L.membrane(root, tips, .02))                                                          # pectoral
        for tp in tips:
            P.add("Body", L.tentacle([root, tp], .012, .008, res=1, per=2))                                 # rays
    P.add("Eye", L.eyes([(-.1, -.125, .24), (.1, -.125, .24)], .046, seg=14))
    root = V((0, .21, .14))
    tips = [V((0, .3, .27)), V((0, .34, .2)), V((0, .35, .13)), V((0, .33, .05))]
    P.add("Body", L.membrane(root, tips, .024))                                                             # tail
    for tp in tips:
        P.add("Body", L.tentacle([root, tp], .014, .009, res=1, per=2))
    P.add("Body", L.tentacle([(0, -.1, .24), (0, -.1, .32), (0, -.04, .36), (0, 0, .37)], .024, .018, per=4))  # illicium
    P.add("Eye", [ring(.025, .075, .368, .393, seg=20)])                                                     # collar
    P.add("Eye", L.cyl(.03, .03, (0, 0, .38), seg=12))
    H = "AccentHead"
    P.add(H, L.lathe([(0, .395), (.045, .4), (.07, .43), (.075, .46), (.06, .49), (.03, .512), (0, .518)], seg=20))
    for k in range(3):
        a = k / 3 * math.tau
        c, sn = math.cos(a), math.sin(a)
        P.add(H, L.cone((c * .06, sn * .06, .455), (c * .12, sn * .12, .47 + .02 * (k % 2)), .014, seg=6))   # filaments
    return P


# =========================================================================== motion sensor

def pir(L):
    """A cluster of eyes in a fleshy mound grown onto the wall, each eye in a smooth lidded socket, the lower ones
    looking down. Contract: wall at +y (y 0..0.08), looks to -y, within +-0.08."""
    P = Parts()
    P.add("Body", L.lathe([(.085, 0), (.084, .015), (.075, .04), (.055, .062), (.03, .074), (0, .078)], axis="-Y",
                          loc=(0, .08, 0), seg=24))                                                         # mound
    eyes = [((0, .0, .008), .034, 0), ((-.046, .012, .04), .02, 0), ((.048, .012, .036), .022, 0),
            ((-.05, .012, -.028), .022, 22), ((.046, .012, -.034), .02, 22), ((0, .015, -.055), .018, 30),
            ((.0, .018, .06), .015, 0)]
    for c, r, down in eyes:
        lid = s.lathe([(r * 1.35, -r * .9), (r * 1.32, -r * .1), (r * 1.15, r * .35), (r * 1.0, r * .45), (0, r * .45)],
                      seg=16, axis="Y")
        s.place(lid, rot=(math.radians(-down), 0, 0))
        P.add("Body", [s.place(lid, c)])                                                                      # socket
        P.add("Eye", L.eyes([V(c) + V((0, -r * .2, -r * .25 * math.sin(math.radians(down))))], r, seg=12))
    return P


# =========================================================================== pressure plate

def plate(L):
    """A flat mimic lying in the doorway: a hide with a ridged spine, two big glowing slit eyes, and its whole edge one
    shut jaw — an outer and an inner lip meeting along a glowing wavy seam. Contract: 1.1 x 1.1, at most 2 cm high."""
    P = Parts()
    S = 1.1
    P.add("Body", L.box((S - .1, S - .1, .01), (0, 0, .005), r=.004))
    for k in range(5):                                                                                       # spine
        y = .34 - k * .1
        P.add("Body", [s.rbox((.09 - k * .008, .07, .005), (0, y, .0115), r=.002, seg=1, rot=(0, 0, math.pi / 4))])
    # the jaw: two rounded lips, a wavy glowing seam between them
    def frame(half_out, half_in, z1, role):
        out = []
        w = half_out - half_in
        for k in range(4):
            length = 2 * half_out if k % 2 == 0 else 2 * half_in
            out.append(s.place(s.rbox((length, w, z1), (0, (half_out + half_in) / 2, z1 / 2 + .0005), r=.004, seg=1),
                               rot=(0, 0, k * math.pi / 2)))
        P.add(role, out)
    frame(S / 2, S / 2 - .035, .014, "Accent")                                                                # outer lip
    frame(S / 2 - .065, S / 2 - .1, .013, "Accent")                                                           # inner lip
    e = S / 2 - .05
    seam = []
    m = 64
    for k in range(m + 1):
        t = k / m * 4                       # around the square, one side per unit
        side, f = int(t) % 4, t - int(t)
        along = -e + 2 * e * f
        off = .014 * math.sin(f * math.pi * 14)
        x, y = [(along, e + off), (e + off, -along), (-along, -e - off), (-e - off, along)][side]
        seam.append((x, y, .0115))
    P.add("Eye", [s.tube(seam, .008, res=1, segments_per=1)])
    for sx in (-1, 1):                                                                                     # shut eyes
        c = (sx * .17, -.15, .0)
        P.add("Accent", L.ellipsoid((c[0], c[1], .009), (.16, .055, .007), seg=16))
        P.add("Eye", L.ellipsoid((c[0], c[1], .0115), (.15, .032, .007), seg=16))
    return P


# =========================================================================== laser emitter

def laser_emitter(L):
    """A spine column, 1.6 m, tapering and bending slightly: vertebrae with big asymmetric processes and broken ribs,
    a glowing eye per vertebra on the beam side, a glowing cap on top.
    Contract: a 0.12 wide post; the emitter on the -y face, z 0.15..1.55."""
    P = Parts()
    P.add("Body", L.lathe([(0, 0), (.08, 0), (.082, .02), (.062, .06), (.05, .1), (0, .1)], seg=16))               # foot
    n = 8
    z0, z1 = .1, 1.52
    step = (z1 - z0) / n

    def bend(z):
        return .012 * math.sin(z / 1.6 * math.tau)

    cord = []
    for k in range(n):
        z = z0 + (k + .5) * step
        x = bend(z)
        r = .058 - .014 * k / (n - 1)
        P.add("Body", L.lathe([(0, -step * .42), (r * .8, -step * .42), (r, -step * .3), (r * .85, 0), (r, step * .3),
                               (r * .8, step * .42), (0, step * .42)], loc=(x, 0, z), seg=12))           # centrum
        P.add("Body", L.claw((x, .03, z + .03), (x, .14 + .03 * (k % 2), z - .12), .028, bend=(0, .03, .02), seg=6))  # spinous
        side = 1 if k % 2 else -1
        P.add("Body", L.claw((x + side * .03, .02, z + .01), (x + side * (.1 if k % 3 else .13), .07, z - .1), .022,
                             bend=(side * .03, .0, .02), seg=6))                                         # rib
        P.add("Body", L.cone((x - side * .035, .01, z), (x - side * .07, .02, z - .03), .018, seg=6))     # short process
        if k < n - 1:
            P.add("Body", L.cyl(r * 1.1, .045, (bend(z0 + (k + 1) * step), 0, z0 + (k + 1) * step), seg=12))  # disc
        P.add("Eye", L.eyes([(x, -r * .8, z)], .022, seg=8))                                          # emitter eyes
        cord.append((x, -r * .75, z))
    P.add("Eye", [s.tube([(bend(.15), -.05, .15)] + cord + [(bend(1.55), -.04, 1.55)], .009, res=1, segments_per=3)])
    P.add("Body", L.ellipsoid((bend(1.56), 0, 1.555), (.055, .06, .04), seg=16))                                # cap
    P.add("Eye", L.ellipsoid((bend(1.56), 0, 1.585), (.03, .03, .014), seg=12))
    return P


DESIGNS = {
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "security_camera": security_camera, "sensor_lidar": lidar, "sensor_pir": pir, "sensor_plate": plate,
    "laser_emitter": laser_emitter,
}
