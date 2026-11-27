"""Wheeled and tracked creatures: listener (bat), worker + worker_lift (beetle with a mandible tray), sweeper (coiled
centipede), thumper (crab), storage (hermit crab in a crate — new)."""
import math

from mathutils import Matrix, Vector

import designs as D
import shapes as s
from common import Parts, V, lerp  # noqa: F401


def small_wheel(c, r, w, seg=16):
    """A plain tyre and hub turning about X (small wheels: the creature's legs carry the look)."""
    c = V(c)
    return [s.lathe([(r * .6, -w / 2), (r * .9, -w / 2), (r, -w * .3), (r, w * .3), (r * .9, w / 2), (r * .6, w / 2)],
                    seg=seg, axis="X", loc=c),
            s.rcyl(r * .64, w * .86, c, axis="X", seg=seg, r=.005),
            s.rcyl(r * .3, w * 1.08, c, axis="X", seg=10, r=.004)]


def mx(fn):
    """Call fn(sx) for both sides and join the lists."""
    return fn(-1) + fn(1)


# =========================================================================== listener: the bat

LISTENER_WHEELS = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]


def listener_wheel(L, c, r=.22, w=.12):
    """A fat fleshy pad rolled into a wheel, with ridges across the tread (symmetric: it spins about its centre)."""
    c = V(c)
    out = [s.lathe([(r * .55, -w / 2), (r * .86, -w / 2), (r * .97, -w * .3), (r * .97, w * .3), (r * .86, w / 2),
                    (r * .55, w / 2)], seg=24, axis="X", loc=c)]
    out.append(s.rcyl(r * .6, w * .9, c, axis="X", seg=20, r=.01))
    out.append(s.rcyl(r * .28, w * 1.1, c, axis="X", seg=16, r=.008))
    for k in range(10):
        a = k / 10 * math.tau
        out.append(s.rbox((w * .8, .05, .035), c + V((0, math.cos(a) * r * .95, math.sin(a) * r * .95)), r=0, seg=1,
                          rot=(a + math.pi / 2, 0, 0)))
    return out


def listener(L):
    """A blind bat, 1.4 m to the ear tips: a hunched hairless body slung low between four wheels it walks on like
    knuckles (bent forearms humping into the hubs), a wrinkled snout, a glowing seam down the spine, and two huge
    cupped ears tilted up and out, ribs fanning from the base, a scalloped trailing edge, a glowing tragus inside.
    Contract: wheels r 0.22, width 0.12 at (∓0.43, ∓0.3, 0.22) [0:(−,−) 1:(+,−) 2:(−,+) 3:(+,+)]."""
    P = Parts()
    for i, c in enumerate(LISTENER_WHEELS):
        P.add(f"AccentWheel{i}", listener_wheel(L, c))
    # body: a hunched loaf along y, the back humped, the belly between the wheels
    P.add("Body", L.loft([(-.46, .0, .0, 2.0, 0, .46), (-.38, .17, .14, 2.4, 0, .5), (-.18, .24, .2, 2.4, 0, .58),
                          (.05, .26, .22, 2.4, 0, .6), (.3, .2, .16, 2.4, 0, .52), (.42, .08, .07, 2.2, 0, .5)],
                         axis="-Y", round_caps=0, seg=24))
    P.add("Eye", L.tentacle([(0, -.3, .755), (0, -.12, .8), (0, .08, .805), (0, .26, .735), (0, .38, .62)], .02, .014,
                            res=1, per=4))                                                       # spine seam
    # head and snout, low in front
    P.add("Body", L.loft([(0, .13, .12, 2.4), (.1, .14, .13, 2.4), (.2, .1, .09, 2.4), (.27, .06, .055, 2.2)],
                         axis="-Y", loc=(0, -.38, .62), round_caps=.03))
    P.add("Body", L.ellipsoid((0, -.66, .6), (.065, .04, .07), seg=12))                            # nose-leaf
    P.add("Eye", L.ellipsoid((0, -.69, .62), (.03, .015, .04), seg=10))
    for sx in (-1, 1):                                                                           # wrinkles
        P.add("Accent", L.seg_between((sx * .07, -.52, .67), (sx * .06, -.6, .64), .018, .014, seg=8))
        P.add("Accent", L.cone((sx * .04, -.62, .56), (sx * .035, -.66, .5), .012, seg=6))      # fangs
    # four limbs: an upper arm out to a high elbow, a bent forearm thickening into a knuckle on the wheel hub
    for x, y, z in LISTENER_WHEELS:
        sx = 1 if x > 0 else -1
        sh, el = V((sx * .17, y * .8, .58)), V((sx * .38, y * 1.06, .76))
        wr, hub = V((sx * .37, y * 1.02, .42)), V((sx * .35, y, .26))
        P.add("Body", L.tentacle([sh, lerp(sh, el, .5) + V((0, 0, .04)), el], .055, .045, res=2, per=4))
        P.add("Body", L.tentacle([el, V((sx * .4, y * 1.06, .6)), wr, hub], .045, .06, res=2, per=4))   # forearm
        P.add("Body", L.ellipsoid(hub + V((-sx * .005, 0, .02)), (.05, .08, .09), seg=12))       # knuckle on the hub
        P.add("Accent", L.ellipsoid(el, (.05, .05, .045), seg=10))                               # elbow callus
        if y < 0:                                                                                # folded wing skin
            P.add("Body", L.membrane(sh, [el, V((sx * .38, y * 1.02, .5)), V((sx * .22, y * .82, .44))], thick=.012))
    # the ears
    for sx in (-1, 1):
        for role, bms in bat_ear(L, (sx * .11, -.36, .72), sx).items():
            P.add(role, bms)
    return P


def bat_ear(L, root, sx, length=.72, nu=12, nv=8):
    """A bat ear: a deep cupped leaf rising from root, tilted up and out (it shows from above), opening forward;
    ribs fan from the base, the trailing (outer) edge is scalloped. Returns role -> pieces."""
    import bmesh
    R = V(root)
    U = V((sx * .5, .18, .85)).normalized()                    # up the ear
    N = V((sx * .45, -.8, .5)).normalized()                     # the way it listens: forward, out and up
    S = U.cross(N).normalized()                                 # across the ear
    N = S.cross(U).normalized()

    def width(u, v=0.0):
        w = .23 * math.sin(math.pi * (.16 + .84 * u)) ** .8
        if v * sx * S.x > 0:                                    # the outer edge: scalloped
            w *= 1 - .22 * abs(math.sin(u * math.pi * 3.5)) * abs(v)
        return w

    def at(u, v, front=0.0):
        w = width(u, v)
        cup = .15 * (1 - v * v) * (width(u) / .23) * min(1.0, u * 4)
        return R + U * (u * length) + S * (v * w) - N * cup + N * front

    bm = bmesh.new()
    grid = [[bm.verts.new(at(i / nu, -1 + 2 * j / nv)) for j in range(nv + 1)] for i in range(nu + 1)]
    for i in range(nu):
        for j in range(nv):
            bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=.016)
    out = {"Body": [bm], "Accent": [], "Eye": []}
    rim = [at(i / nu, -1) for i in range(nu)] + [at(1, 0)] + [at(i / nu, 1) for i in range(nu - 1, -1, -1)]
    out["Accent"] += [s.tube(rim, .013, res=1, segments_per=2)]
    for ue, ve in ((.5, -1), (.78, -.85), (.97, 0), (.78, .85), (.5, 1)):                         # ribs fanning out
        out["Accent"] += [s.tube([at(ue * k / 4, ve * k / 4, .007) for k in range(5)], .012, res=1, radii=[1.3, 1, .9, .8, .7],
                                 segments_per=2)]
    out["Eye"] += L.tentacle([at(.02, 0, .01), at(.22, 0, .014), at(.42, 0, .012)], .018, .01, res=1, per=4)  # tragus
    out["Body"] += L.ellipsoid(R + V((0, .01, -.02)), (.06, .06, .05), seg=10)                  # the ear's root
    return out


# =========================================================================== worker: the beetle

def worker_belly(L, P):
    # the beetle's belly: a low oval between the wheels (|x| <= .35 in front of y -0.1: the booms swing past)
    P.add("Body", L.loft([(-.47, .0, .0, 2.0, 0, .24), (-.42, .26, .1, 2.6, 0, .25), (-.2, .34, .13, 2.6, 0, .26),
                          (.3, .32, .12, 2.6, 0, .25), (.43, .2, .08, 2.6, 0, .24)], axis="-Y", seg=20))
    for x, y, _ in D.WORKER_WHEELS:
        P.add("Accent", L.cyl(.035, .12, (x * .78, y, .12), axis="X", seg=10))               # axles


def worker_pod(L, P):
    # the prothorax in front (|x| <= .25, y -.42..-.06, z <= .7): an armoured shield carrying the mandibles'
    # shoulders; the camera module seats on its face at z .5
    P.add("Body", L.loft([(.06, .2, .15, 2.6, 0, .45), (.2, .24, .17, 2.6, 0, .47), (.34, .23, .15, 2.6, 0, .47),
                          (.41, .16, .1, 2.4, 0, .46)], axis="-Y", round_caps=.03, seg=20))
    P.add("Accent", L.loft([(.3, .2, .07, 2.6), (.4, .15, .05, 2.4)], axis="-Y", loc=(0, 0, .58), seg=16))  # brow ridge
    P.add("Eye", L.box((.2, .02, .03), (0, -.415, .42), r=.008))                                  # a low glowing slit
    P.add("Body", L.box((.4, .2, .12), (0, 0, .37)))                                                  # neck to the shell
    D.worker_shoulders(L, P)


def worker_shell(L, P):
    # the back (y >= 0.07): two domed wing-cases meeting at a glowing centre seam, a neck rising from between them
    # to the head, which sits right behind the tray at rest, so the tray reads as its jaw
    for sx in (-1, 1):
        P.add("Body", L.loft([(-.52, .0, .0, 2.0, sx * .17, .6), (-.47, .15, .2, 2.4, sx * .17, .6),
                              (-.3, .19, .29, 2.4, sx * .17, .6), (-.14, .18, .27, 2.4, sx * .17, .57),
                              (-.07, .15, .2, 2.4, sx * .17, .54)], axis="-Y", seg=20))       # wing-case
        P.add("Accent", L.tentacle([(sx * .33, .1, .54), (sx * .36, .3, .56), (sx * .3, .47, .58)], .02, .016, res=1))  # rim
    P.add("Eye", L.tentacle([(0, .09, .8), (0, .25, .88), (0, .42, .8), (0, .5, .66)], .02, .016, res=1, per=4))  # seam
    P.add("Body", L.tentacle([(0, .26, .8), (0, .25, .98), (0, .24, 1.1)], .1, .085, res=2, per=4))  # neck
    # the head: a wide skull behind the tray, two compound eyes, a mouth plate with palps under the tray's back
    P.add("Body", L.ellipsoid((0, .24, 1.19), (.17, .15, .13), seg=20))
    P.add("Accent", L.ellipsoid((0, .26, 1.27), (.15, .13, .06), seg=16))                         # head shield
    for sx in (-1, 1):
        P.add("Eye", L.ellipsoid((sx * .12, .14, 1.22), (.055, .05, .06), seg=14))               # compound eyes
        P.add("Accent", L.chain([(sx * .13, .2, 1.28), (sx * .26, .22, 1.33), (sx * .36, .3, 1.3)], [.016, .012, .008], seg=6))  # antennae
        P.add("Accent", L.claw((sx * .05, .1, 1.08), (sx * .03, .085, .96), .02, bend=(0, -.01, 0), seg=6))  # palps
    P.add("Accent", L.box((.16, .05, .08), (0, .1, 1.12), r=.02))                                  # mouth plate


def worker_dock(L, P):
    z = D.worker_clear_z(-.08) - .02
    P.add("Accent", L.box((.4, .2, .04), (0, .02, z)))
    for sx in (-1, 1):
        P.add("Accent", L.box((.04, .16, .12), (sx * .17, .04, z - .07)))
    P.add("Accent", L.box((.12, .24, .12), (0, .12, z - .06)))
    D.worker_cradle(L, P)


def worker_beacon(L, P, loc=(0, .26, 1.34)):
    x, y, z = loc
    P.add("Accent", L.cyl(.075, .04, (x, y, z + .015)))
    P.add("Eye", L.cyl(.055, .075, (x, y, z + .065), radius2=.045))                          # sunk 1 cm into its collar


def worker(L):
    """A beetle, 1.4 m: a low oval belly on four wheels with the warehouse plaque in front, an armoured prothorax
    carrying the mandibles' shoulders, two domed wing-cases with a glowing seam, a neck rising to the head right behind
    the tray, so at rest the tray is its jaw and the booms (worker_lift) its mandibles. Contract: designs.worker's plan
    (deck, pod, mast, dock, beacon)."""
    return D.worker(L, deck=worker_belly, pod=worker_pod, mast=worker_shell, dock=worker_dock, beacon=worker_beacon,
                    wheel=lambda c, r, w: small_wheel(c, r, w, seg=16), extra=worker_plaque)


def worker_plaque(L, P):
    # the warehouse plaque (a box on a pallet glowing on a plate) on the front of the belly under the prothorax, tipped
    # back to the camera: players read the beetle as the loot hauler
    D.worker_plaque(L, P, loc=(0, -.425, .25), w=.24, h=.15, tilt=32)


def mandible_booms(L, P):
    hy, hz = D.WORKER_HINGE
    pin = D.LIFT_PIN
    for sx in (-1, 1):
        x = sx * D.LIFT_ARM_X
        # a curved chitin blade, deep at the shoulder, bowing forward and tapering to the pin
        rings = []
        for k in range(7):
            f = k / 6
            z = hz + .02 + (pin - .005 - hz - .02) * f
            rings.append((z, .03 - .008 * f, .062 - .03 * f, 2.6, x, hy - .035 * math.sin(math.pi * f)))
        P.add("AccentLiftArm", [s.loft(rings, seg=12)])
        for k in range(5):                                                                    # serrated inner edge
            f = .25 + k * .11
            z = hz + .02 + (pin - .05 - hz) * f
            yy = hy - .035 * math.sin(math.pi * f)
            P.add("AccentLiftArm", L.cone((x - sx * .018, yy, z), (x - sx * .06, yy, z + .03), .016, seg=6))
        P.add("AccentLiftArm", L.cyl(.056, .06, (x, hy, hz), axis="X"))                       # shoulder knuckle
        P.add("AccentLiftArm", L.cyl(.026, .12, (sx * .345, hy, hz), axis="X", seg=12))       # axle into the drum
        P.add("AccentLiftArm", L.cyl(.025, .05, (x, hy, pin), axis="X", seg=12))              # end knuckle


def mandible_tray(L, P):
    D.lift_tray(L, P)
    hy, pin = D.WORKER_HINGE[0], D.LIFT_PIN
    bottom = pin - .015
    front = hy - D.LIFT_TRAY_D / 2
    for k in range(4):                                                                        # teeth hooked inward
        for sx in (-1, 1):
            x = sx * (.07 + k * .085)
            P.add("BodyLiftTray", L.claw((x, front + .03, bottom + .03), (x - sx * .05, front - .09, bottom + .03), .03,
                                         bend=(sx * .015, -.03, 0), seg=8))


def worker_lift(L):
    return D.worker_lift(L, boom=mandible_booms, tray=mandible_tray)


# =========================================================================== sweeper: the coiled centipede

SWEEPER_WHEELS = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]


def spiral(t):
    """Point on the coil, t 0 (the head, outer, low at the front) .. 1 (the tail, in the middle on top)."""
    a = -math.pi / 2 - t * 2.15 * math.tau            # turning clockwise seen from above
    r = .37 - t * .29
    z = .3 + t * .26
    return V((math.cos(a) * r, math.sin(a) * r, z)), a


def sweeper(L):
    """A centipede coiled into a domed spiral, 0.6 m: overlapping armoured segments whose outer turn skirts the
    chassis down to the casters, thick legs along it, a glowing seam running along the spine of the coil, the head
    lifted at the front with a cluster of glowing eyes, fangs and antennae; the lidar disc spins under the belly.
    Contract: wheels r 0.08, width 0.06 at (-0.28, 0.18), (0.28, 0.18), (0, -0.3), z 0.08; AccentLidar centred on
    x = y = 0 at z 0.22; eye (0, -0.35, 0.5)."""
    P = Parts()
    for i, c in enumerate(SWEEPER_WHEELS):
        P.add(f"AccentWheel{i}", small_wheel(c, .08, .06, seg=12))
        x, y, _ = c
        P.add("Body", L.box((.1, .09, .17), (x, y, .245), r=.02))                               # wheel housings
        for sx in (-1, 1):
            P.add("Accent", L.box((.026, .06, .12), (x + sx * .04, y, .13), r=.008))            # forks
    P.add("Body", L.lathe([(0, .25), (.27, .25), (.29, .28), (.28, .34), (0, .36)], seg=24))     # belly plate
    P.add("Accent", L.cyl(.05, .1, (0, 0, .27), seg=12))                                         # lidar hub
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    P.add("Eye", L.ball(.03, (0, -.15, .22)))
    n = 48
    seam = []
    for k in range(n):
        t = k / (n - 1)
        p, a = spiral(t)
        tang = V((math.sin(a), -math.cos(a), 0))                                                # along the coil
        out = V((math.cos(a), math.sin(a), 0))
        w = .095 - t * .035
        seg = s.loft([(-.055, w * .72, w * .42, 2.4), (-.03, w * .96, w * .62, 2.6), (.025, w, w * .64, 2.6),
                      (.055, w * .74, w * .44, 2.4)], seg=10)
        # loft axis z -> along the coil, ring x -> outward, ring y -> up
        seg.transform(Matrix((out, V((0, 0, 1)), tang)).transposed().to_4x4())
        seg.transform(Matrix.Translation(p))
        P.add("Body", [seg])
        if k % 2 == 0:
            seam.append(p + V((0, 0, w * .6)))
        if t < .44 and k % 2 == 1:                                                              # thick legs on the outer turn
            base = p + out * w * .5 - V((0, 0, .01))
            knee = p + out * (w + .06) + V((0, 0, .03))
            foot = p + out * (w + .1) - V((0, 0, .13)) - tang * .03
            P.add("Accent", L.tentacle([base, knee, foot], .024, .012, res=1, per=3))
    P.add("Eye", L.tentacle(seam, .014, .01, res=1, per=2))                                      # the glowing seam
    # the head, lifted at the front on a short neck
    h0, a0 = spiral(0)
    hc = V((0, -.43, .44))
    P.add("Body", L.tentacle([h0 + V((0, 0, .02)), V((0, -.4, .36)), hc], .08, .075, res=2, per=3))  # neck
    P.add("Body", L.loft([(0, .13, .08, 2.6), (.07, .135, .085, 2.6), (.14, .1, .06, 2.4)], axis="-Y",
                         loc=(hc.x, hc.y + .04, hc.z), round_caps=.025, seg=16))                 # head
    P.add("Accent", L.loft([(.0, .11, .025, 2.6), (.12, .09, .02, 2.6)], axis="-Y", loc=(0, hc.y + .03, hc.z + .07),
                           seg=12))                                                                # head plate
    P.add("Eye", L.eyes([(-.05, -.55, .48), (.05, -.55, .48), (-.1, -.53, .46), (.1, -.53, .46), (-.025, -.56, .43),
                         (.025, -.56, .43)], [.026, .026, .02, .02, .016, .016]))                  # eye cluster
    for sx in (-1, 1):
        P.add("Accent", L.claw((sx * .07, -.52, .38), (sx * .02, -.62, .34), .025, bend=(sx * .02, -.02, -.01), seg=6))  # fangs
        P.add("Accent", L.tentacle([(sx * .06, -.52, .5), (sx * .14, -.6, .6), (sx * .22, -.58, .68)], .013, .006, res=1))  # antennae
    return P


# =========================================================================== thumper: the crab

def carapace_ring(a, rx=.64, ry=.5, n=2.6, cy=.03):
    """A point on the carapace's widest ring (superellipse) at angle a."""
    c, s_ = math.cos(a), math.sin(a)
    return V((rx * math.copysign(abs(c) ** (2 / n), c), cy + ry * math.copysign(abs(s_) ** (2 / n), s_), 0))


def thumper(L):
    """A crab, 1.4 m wide: a low carapace with a raised rim, a notched front edge, spines on the flanks and a front
    plate carrying two eye stalks; four bent walking legs a side, their knees above the shell; two great claws pressing
    the glowing seismic pads onto the floor in front.
    Contract: legs within x ±0.69, y ±0.7; pads (Eye) on the floor at (±0.3, -0.85); probes at (±0.53, -0.8, 0)."""
    P = Parts()
    rz = .6                                                                                       # the rim's height
    P.add("Body", L.loft([(.42, .48, .38, 2.4, 0, .02), (.52, .6, .47, 2.6, 0, .02), (rz, .64, .5, 2.6, 0, .03),
                          (.7, .55, .43, 2.4, 0, .05), (.79, .36, .28, 2.2, 0, .08), (.84, .0, .0, 2.0, 0, .1)],
                         seg=32))                                                                  # carapace
    P.add("Body", L.loft([(.34, .3, .3, 2.4), (.46, .38, .38, 2.4)], seg=20))                    # belly
    rim = [carapace_ring(math.radians(-90 + k * 15)) + V((0, 0, rz + .01)) for k in range(26)]   # overlaps its start
    P.add("Accent", [s.tube(rim, .03, res=1, segments_per=2, radii=[1.0] * 25 + [.6])])                       # raised rim
    for sx in (-1, 1):                                                                           # the H-groove on the back
        P.add("Accent", L.tentacle([(sx * .12, -.28, .78), (sx * .2, .0, .81), (sx * .13, .28, .77)], .02, .016, res=1))
        for a in (5, 25, 45):                                                                  # flank spines
            p = carapace_ring(math.radians(a if sx > 0 else 180 - a)) + V((0, 0, rz + .01))
            P.add("Accent", L.cone(p - V((sx * .03, 0, 0)), p + V((sx * .1, .02, .03)), .035, seg=8))
    for k in range(6):                                                                         # notched front edge: teeth
        a = math.radians(-125 + k * 14)
        p = carapace_ring(a) + V((0, 0, rz + .01))
        d = V((math.cos(a), math.sin(a), 0)).normalized()
        if abs(p.x) < .14:
            continue                                                                           # the eye plate sits there
        P.add("Accent", L.cone(p - d * .03, p + d * .09 + V((0, 0, .01)), .04, seg=6))
    # the front plate with the eye stalks and the mouth under it
    P.add("Accent", L.box((.3, .12, .1), (0, -.5, rz + .03), r=.03))
    for sx in (-1, 1):
        P.add("Body", L.tentacle([(sx * .08, -.5, rz + .06), (sx * .1, -.54, .78), (sx * .13, -.56, .9)], .032, .024, res=1))
        P.add("Eye", L.ball(.05, (sx * .13, -.565, .93)))
    P.add("Eye", L.box((.22, .04, .05), (0, -.47, .5), r=.015))                                 # mouth slit
    for sx in (-1, 1):
        P.add("Accent", L.box((.08, .05, .1), (sx * .07, -.47, .44), r=.02))                   # mouth plates
    # walking legs: up from the hip to a knee above the shell, down and out, the last joint hooking onto the floor — each a
    # thigh (AccentLeg{n}Upper: hip to knee) and a shin (AccentLeg{n}Lower: knee to the tip): ProceduralLegs plants the tips
    n = 0
    for sx in (-1, 1):
        for y in (-.22, .02, .25, .46):
            hip = V((sx * .46, y, .5))
            knee = V((sx * .5, y * 1.15, .98))
            ankle = V((sx * .655, y * 1.35, .5))
            tip = V((sx * .62, max(-.66, min(.66, y * 1.45)), .02))
            P.add(f"AccentLeg{n}Upper", L.chain([hip, knee + (hip - knee).normalized() * .02], [.055, .045], flat=.8, seg=10))
            P.add(f"AccentLeg{n}Upper", L.ellipsoid(knee, (.068, .062, .06), seg=10))
            P.add(f"AccentLeg{n}Lower", L.chain([knee + (ankle - knee).normalized() * .02, ankle], [.045, .035], flat=.8, seg=10))
            P.add(f"AccentLeg{n}Lower", L.chain([ankle, tip], [.035, .012], flat=.8, seg=8))
            n += 1
    # great claws pressing the pads
    for sx in (-1, 1):
        x = sx * .3
        sh, el = (sx * .3, -.42, .55), (sx * .5, -.62, .72)
        P.add("Accent", L.chain([sh, el], [.08, .07], flat=.8, seg=12))
        P.add("Accent", L.ellipsoid(el, (.075, .075, .075), seg=12))
        palm = (x, -.84, .32)
        P.add("Accent", L.chain([el, palm], [.07, .1], flat=.8, seg=12))
        P.add("Accent", L.ellipsoid(palm, (.13, .15, .16), seg=14))                            # the claw's palm
        for fy, bend in ((-.08, -.04), (.08, .04)):                                            # pincer fingers pressing down
            P.add("Accent", L.claw((x, -.85 + fy, .25), (x, -.85 + fy * 1.6, .07), .05, bend=(0, bend, 0)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))                                          # the pad
        P.add("Accent", L.cyl(.13, .06, (x, -.85, .07)))
    return P


# =========================================================================== storage: hermit crab in a crate

STORAGE_HINGE = (.335, .61)   # (y, z) of the lid's hinge axis, along X: the crate's back top edge


def storage(L):
    """The team's storage robot (new), 0.62 m: a hermit crab carrying a grey crate as its shell — orange only on the
    crate's corners and edges, a friendly glowing ring on the lid. The lid (BodyLid + EyeLid, both hinged on the crate's
    back top edge, STORAGE_HINGE) opens. The crab comes out of an opening in the crate's front: thorax, eye stalks,
    one big claw and one small, two pairs of curled jointed walking legs a side, a pair of short legs at the back.
    Its legs walk (ProceduralLegs). Fits a 0.9 m duct."""
    P = Parts()
    cw, cd, z0, z1, cy = .5, .52, .13, .58, .07
    zc = (z0 + z1) / 2
    crate = s.rbox((cw, cd, z1 - z0), (0, cy, zc), r=.015, seg=2)
    crate = s.boolean(crate, s.rbox((cw - .06, cd - .06, z1 - z0), (0, cy, zc + .03), r=.01, seg=1))     # open top
    crate = s.boolean(crate, s.rbox((.34, .1, .26), (0, cy - cd / 2, .32), r=.04, seg=2))                # front opening
    P.add("Body", [crate])
    for z in (.26, .42):                                                                     # board seams (grey)
        for sx in (-1, 1):
            P.add("Body", [s.rbox((.016, cd - .08, .03), (sx * (cw / 2 + .004), cy, z), r=.006, seg=1)])
        P.add("Body", [s.rbox((cw - .08, .016, .03), (0, cy + cd / 2 + .004, z), r=.006, seg=1)])
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Accent", L.box((.05, .05, z1 - z0 + .01), (sx * (cw / 2 - .01), cy + sy * (cd / 2 - .01), zc), r=.012))  # corner irons
        P.add("Accent", L.box((.03, cd - .04, .03), (sx * (cw / 2 - .005), cy, z1 - .01), r=.01))  # top edge trims
    P.add("Accent", L.box((cw - .04, .03, .03), (0, cy + cd / 2 - .005, z1 - .01), r=.01))
    P.add("Accent", L.box((cw - .04, .03, .03), (0, cy - cd / 2 + .005, z1 - .01), r=.01))
    # the lid: a grey plate with orange-free top and a glowing ring marker, hinged at the back top edge
    hy, hz = STORAGE_HINGE
    P.add("BodyLid", L.box((cw + .02, cd + .02, .035), (0, cy, z1 + .0155), r=.01))
    P.add("BodyLid", L.box((cw - .12, .035, .03), (0, cy - cd / 2 + .03, z1 + .04), r=.01))  # front grip
    for sx in (-1, 1):
        P.add("BodyLid", L.cyl(.018, .1, (sx * .15, hy, hz), axis="X", seg=10))              # hinge knuckles
    P.add("EyeLid", [s.boolean(s.rcyl(.11, .012, (0, cy, z1 + .036), seg=24, r=.003),
                               s.rcyl(.065, .05, (0, cy, z1 + .036), seg=24, r=0))])          # glowing ring marker
    # the crab out in front: thorax, carapace plate, eye stalks, antennae
    P.add("Body", L.loft([(0, .15, .1, 2.4), (.14, .16, .11, 2.4), (.28, .14, .09, 2.4), (.36, .0, .0)], axis="-Y",
                         loc=(0, -.1, .27), seg=16))                                           # thorax
    P.add("Accent", L.loft([(.1, .15, .045, 2.6), (.3, .12, .035, 2.6)], axis="-Y", loc=(0, -.1, .34), seg=12))  # carapace
    for sx in (-1, 1):
        P.add("Body", L.tentacle([(sx * .05, -.34, .32), (sx * .07, -.4, .44), (sx * .09, -.42, .53)], .022, .016, res=1))
        P.add("Eye", L.ball(.035, (sx * .09, -.425, .555)))
        P.add("Accent", L.tentacle([(sx * .03, -.4, .3), (sx * .09, -.5, .34), (sx * .14, -.56, .4)], .009, .005, res=1))  # antennae
    # claws: the right one big, both out in front of the crate
    for sx, k in ((1, 1.0), (-1, .65)):
        sh, el = V((sx * .12, -.32, .24)), V((sx * .22, -.44, .22))
        P.add("Accent", L.chain([sh, el], [.035 * k + .01, .03 * k + .01], seg=8))
        palm = V((sx * .19, -.56, .18))
        P.add("Accent", L.chain([el, palm], [.03 * k + .01, .05 * k + .01], seg=8))
        P.add("Accent", L.ellipsoid(palm, (.07 * k + .01, .08 * k + .01, .06 * k + .01), seg=12))
        P.add("Accent", L.claw(palm + V((0, -.04 * k, .02)), palm + V((-sx * .03, -.15 * k - .02, .0)), .03 * k,
                               bend=(0, -.02, .03), seg=6))
        P.add("Accent", L.claw(palm + V((0, -.04 * k, -.02)), palm + V((-sx * .03, -.13 * k - .02, -.04)), .025 * k,
                               bend=(0, -.02, -.02), seg=6))
    # curled jointed legs: up to a knee, down to an ankle, the last joint curling back under — each cut at the knee, a
    # thigh (AccentLeg{n}Upper: hip to knee, with the knee's joint) and a shin (AccentLeg{n}Lower: knee to the tip):
    # ProceduralLegs plants the tips and walks them
    n = 0
    for sx in (-1, 1):
        for y in (-.3, -.18):
            hip, knee = V((sx * .12, y, .22)), V((sx * .26, y - .03, .34))
            ankle, tip = V((sx * .34, y - .06, .14)), V((sx * .3, y - .02, .01))
            P.add(f"AccentLeg{n}Upper", L.chain([hip, knee + (hip - knee).normalized() * .01], [.026, .022], seg=8))
            P.add(f"AccentLeg{n}Upper", L.ellipsoid(knee, (.03, .03, .03), seg=8))
            P.add(f"AccentLeg{n}Lower", L.chain([knee + (ankle - knee).normalized() * .01, ankle], [.022, .018], seg=8))
            P.add(f"AccentLeg{n}Lower", L.ellipsoid(ankle, (.03, .03, .03), seg=8))
            P.add(f"AccentLeg{n}Lower", L.claw(ankle, tip, .016, bend=(sx * .02, 0, 0), seg=6))
            n += 1
    for sx in (-1, 1):
        hip, knee = V((sx * .18, .26, .15)), V((sx * .29, .3, .16))                           # short back legs
        P.add(f"AccentLeg{n}Upper", L.chain([hip, knee + (hip - knee).normalized() * .01], [.022, .018], seg=8))
        P.add(f"AccentLeg{n}Lower", L.claw(knee, V((sx * .3, .32, .01)), .016, bend=(sx * .02, 0, 0), seg=6))
        n += 1
    return P


DESIGNS = {"robot_listener": listener, "robot_worker": worker, "worker_lift": worker_lift, "robot_sweeper": sweeper,
           "robot_thumper": thumper, "robot_storage": storage}
