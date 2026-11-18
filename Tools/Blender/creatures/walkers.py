"""Walkers and the tall ones: guard (hooded mantis priest), hound, hunter, warden."""
import math

from mathutils import Vector

import shapes as s
from common import Parts, V, lerp, ring_points  # noqa: F401

GUARD_HIP_X, GUARD_HIP_Z, GUARD_KNEE = 0.22, 0.9, 0.45


def guard(L):
    """A hooded mantis priest, 2.05 m: a deep cowl with a cluster of eyes inside, a long thorax leaning forward, two
    raptorial forelimbs folded in prayer in front of the chest, folded wing-cases on the back with a glowing seam,
    insect legs with a spur at the knee and two-toed claws.
    Contract: AccentLeg0 (x -0.22) / AccentLeg1 (x +0.22) from the hip z 0.9, knee z 0.45, feet on the floor;
    head ~(0, -0.05, 1.86) with its face at y ~ -0.2."""
    P = Parts()
    # ---- trunk: pelvis -> petiole -> thorax, leaning forward
    P.add("Body", L.loft([(.8, .2, .16, 2.6, 0, .02), (.9, .27, .19, 2.8, 0, .02), (1.0, .25, .18, 2.8, 0, .01),
                          (1.08, .15, .12, 2.4, 0, 0)], round_caps=.05))                                 # pelvis
    P.add("Body", L.chain([(0, .02, 1.0), (0, -.01, 1.2), (0, -.05, 1.4), (0, -.07, 1.6)], [.17, .19, .23, .25],
                          n=2.4, flat=.72, seg=24, overlap=.05))                                          # segmented thorax
    # ---- cowl over the shoulders and the hood around the head
    P.add("Body", L.loft([(1.5, .3, .2, 2.4, 0, -.03), (1.6, .37, .25, 2.4, 0, -.02), (1.7, .3, .24, 2.4, 0, -.02),
                          (1.78, .2, .2, 2.4, 0, -.03)], round_caps=.03))                                # mantle
    P.add("Body", L.tentacle([(0, -.02, 1.6), (0, -.05, 1.72), (0, -.07, 1.82)], .07, .06))           # neck
    hood = L.loft([(1.66, .19, .21, 2.4, 0, -.04), (1.8, .21, .23, 2.4, 0, -.05), (1.94, .18, .2, 2.4, 0, -.04),
                   (2.03, .11, .14, 2.4, 0, -.01), (2.1, .0, .0, 2.0, 0, .06)], round_caps=.0)
    cavity = s.ball(1.0, (0, -.24, 1.85), seg=24, scale=(.135, .12, .165))
    P.add("Body", [s.boolean(hood[0], cavity)])
    P.add("Accent", L.loft([(1.7, .1, .06, 2.2, 0, -.12), (1.98, .1, .06, 2.2, 0, -.12)]))          # the face inside
    P.add("Eye", L.eyes([(0, -.17, 1.88)], .045, seg=16))
    P.add("Eye", L.eyes([(-.065, -.16, 1.93), (.065, -.16, 1.93), (-.075, -.16, 1.82), (.075, -.16, 1.82),
                         (0, -.17, 1.79)], [.024, .024, .02, .02, .018]))
    for sx in (-1, 1):                                                                                    # mouth palps
        P.add("Accent", L.claw((sx * .035, -.15, 1.76), (sx * .03, -.22, 1.66), .014, bend=(0, -.02, 0)))
    # ---- wing-cases on the back and the glowing spinal seam between them
    for sx in (-1, 1):
        wing = s.loft([(0, .0, .0), (.08, .1, .03, 2.2), (.4, .14, .04, 2.2), (.7, .07, .025, 2.2), (.78, 0, 0)], seg=16)
        s.place(wing, rot=(math.pi, 0, sx * math.radians(-6)))
        P.add("Body", [s.place(wing, (sx * .11, .175, 1.62))])
    P.add("Eye", L.box((.035, .08, .55), (0, .15, 1.25), r=.012))
    P.add("Eye", L.tentacle([(0, -.13, 2.02), (0, -.03, 2.065), (0, .05, 2.085)], .02, .016, res=2, per=4))  # crown seam
    # ---- raptorial forelimbs: long folded blades reaching below the waist
    for sx in (-1, 1):
        sh, el, wr, tip = (sx * .27, -.1, 1.52), (sx * .3, -.33, 1.24), (sx * .23, -.55, 1.64), (sx * .21, -.5, .98)
        P.add("Accent", L.ball(.08, sh))
        P.add("Accent", L.chain([sh, el], [.065, .055]))                                                  # coxa
        P.add("Accent", L.ball(.06, el))
        P.add("Accent", L.seg_between(el, wr, .09, .06, n=2.4, flat=.45))                                 # femur blade
        for k in range(4):
            b = lerp(el, wr, .2 + k * .18) + V((0, .035, 0))
            P.add("Accent", L.cone(b, b + V((0, .06, -.05)), .02, seg=6))                                 # femur spines
        P.add("Accent", L.ball(.045, wr))
        P.add("Accent", L.seg_between(wr, tip, .055, .035, n=2.4, flat=.4))                               # tibia blade
        for k in range(6):
            b = lerp(wr, tip, .12 + k * .14) + V((0, .025, 0))
            P.add("Accent", L.cone(b, b + V((0, .05, .03)), .016, seg=6))                                 # serrations
        P.add("Accent", L.claw(tip, V(tip) + V((0, .02, .14)), .03, bend=(0, -.05, .02)))                 # hook
    # ---- legs (Upper above the knee, Lower below)
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * GUARD_HIP_X
        P.add(leg, L.ball(.095, (x, 0, GUARD_HIP_Z)))
        P.add(leg, L.seg_between((x, -.005, .9), (x, .035, GUARD_KNEE + .04), .085, .06, n=2.6, flat=.7))      # femur
        P.add(leg, L.cone((x, .06, .5), (x, .19, .6), .035, seg=8))                                     # knee spur
        P.add(leg, L.ball(.068, (x, .03, GUARD_KNEE)))
        P.add(leg, L.seg_between((x, .03, GUARD_KNEE - .04), (x, -.03, .1), .055, .04, n=2.4))                 # tibia
        for k in range(3):
            b = (x, .045 - k * .012, .36 - k * .09)
            P.add(leg, L.cone(b, (x, b[1] + .05, b[2] + .03), .014, seg=6))                             # tibia spines
        P.add(leg, L.seg_between((x, -.02, .12), (x, -.08, .04), .05, .045))                             # ankle
        for dx in (-.035, .035):
            P.add(leg, L.claw((x + dx, -.08, .045), (x + dx * 1.3, -.25, .012), .03, bend=(0, -.01, .02)))  # toes
        P.add(leg, L.claw((x, -.03, .05), (x, .09, .01), .022, bend=(0, 0, .01)))                       # heel claw
    P.split = {"AccentLeg0": GUARD_KNEE, "AccentLeg1": GUARD_KNEE}
    return P


HOUND_LEGS = [(.15, -.35), (-.15, -.35), (.15, .35), (-.15, .35)]
HOUND_HIP, HOUND_KNEE = .6, .3


def hound(L):
    """An eyeless flayed hound, 1 m at the shoulder: an arched spine with dorsal spikes over an open rib cage, a long
    blind skull ending in a star of glowing feelers (it hunts by smell), a whip tail with a glowing tip.
    Contract: legs AccentLeg0..3 at (±0.15, ∓0.35) from the hip z 0.6, knee z 0.3; nose module at (0, -1.02, 0.75)."""
    P = Parts()
    spine = [(0, .5, .8), (0, .32, .9), (0, .1, .97), (0, -.12, .99), (0, -.34, .95), (0, -.5, .9)]
    for k, (a, b) in enumerate(zip(spine, spine[1:])):
        P.add("Body", L.seg_between(a, b, .07, .07, n=2.2, overlap=.03))                                    # vertebrae
    for k, p in enumerate(spine[1:-1]):
        P.add("Accent", L.cone(V(p) + V((0, 0, .04)), V(p) + V((0, .09, .16 - k * .015)), .03, seg=8))     # dorsal spikes
    P.add("Body", L.ellipsoid((0, -.18, .8), (.155, .3, .16), seg=20))                                       # lung sack
    P.add("Body", L.ellipsoid((0, .34, .76), (.16, .17, .12), seg=20))                                       # pelvis
    P.add("Eye", L.tentacle([V(p) + V((0, 0, .066)) for p in spine[1:-1]], .018, .018, res=2, per=4))         # dorsal seam
    for k in range(5):
        y = -.4 + k * .1
        sweep = k * .02
        pts = [(-.05, y + sweep + .05, .63), (-.17, y + sweep, .72), (-.175, y, .88), (-.08, y - .01, .98),
               (.08, y - .01, .98), (.175, y, .88), (.17, y + sweep, .72), (.05, y + sweep + .05, .63)]
        P.add("Accent", L.tentacle(pts, .02, .016, res=1, per=3))                                            # ribs
    for sx in (-1, 1):
        P.add("Accent", L.ellipsoid((sx * .15, -.36, .78), (.05, .12, .12), seg=12))                        # shoulder blades
        P.add("Body", L.ellipsoid((sx * .15, .35, .7), (.07, .11, .11), seg=12))                            # haunches
    # neck and the blind skull
    P.add("Body", L.tentacle([(0, -.44, .92), (0, -.56, .96), (0, -.66, .95)], .075, .065))
    P.add("Body", L.loft([(0, .12, .13, 2.4), (.14, .14, .14, 2.4, 0, .01), (.32, .09, .095, 2.4, 0, -.02),
                          (.44, .065, .07, 2.4, 0, -.03)], axis="-Y", loc=(0, -.6, .98), round_caps=.03))     # skull
    P.add("Accent", L.ellipsoid((0, -.74, 1.07), (.13, .07, .04), seg=14))                                  # brow
    jaw = s.loft([(0, .1, .05, 2.6), (.2, .075, .04, 2.6), (.4, .045, .025, 2.4)], seg=16, axis="-Y")
    s.place(jaw, rot=(math.radians(24), 0, 0))
    P.add("Accent", [s.place(jaw, (0, -.64, .9))])                                                          # open jaw
    for k in range(4):
        y = -.76 - k * .065
        for sx in (-1, 1):
            w = .055 - k * .007
            P.add("Body", L.cone((sx * w, y, .9), (sx * w, y - .01, .84), .013, seg=6))                       # upper teeth
            d = (-.64 - (y + .02)) / .91
            P.add("Body", L.cone((sx * w * .8, y + .02, .9 - .41 * d + .015), (sx * w * .8, y + .01, .9 - .41 * d + .065), .011, seg=6))
    tip = V((0, -1.04, .96))
    for k in range(8):
        a = (k + .5) / 8 * math.tau
        d = V((math.cos(a) * .05, 0, math.sin(a) * .045))
        P.add("Eye", L.tentacle([tip + d * .5, tip + d * 1.6 + V((0, -.05, 0)), tip + d * 2.3 + V((0, -.1, 0))],
                                .022, .012, res=1, per=3))                                                  # star nose
    P.add("Accent", L.ellipsoid(tip, (.065, .04, .06), seg=12))
    # tail
    tail = [(0, .48, .8), (0, .66, .86), (0, .8, 1.0), (0, .86, 1.16)]
    P.add("Body", L.chain(tail, [.05, .04, .03, .025], n=2.2))
    P.add("Accent", L.cone((0, .86, 1.14), (0, .9, 1.3), .03, seg=8))                                       # tail sting
    # legs
    for i, (x, y) in enumerate(HOUND_LEGS):
        leg = f"AccentLeg{i}"
        front = y < 0
        P.add(leg, L.ball(.075, (x, y, HOUND_HIP)))
        knee = (x, y + (.06 if front else -.06), HOUND_KNEE)
        P.add(leg, L.seg_between((x, y, HOUND_HIP + .02), V(knee) + V((0, 0, .035)), .07, .045, n=2.4, flat=.8))  # thigh
        P.add(leg, L.ball(.045, knee))
        ankle = (x, y - .03, .055)
        P.add(leg, L.seg_between(V(knee) - V((0, 0, .03)), ankle, .04, .03, seg=10))                                                     # shin
        P.add(leg, L.cone((x, knee[1] + (.03 if front else -.03), .34), (x, knee[1] + (.1 if front else -.1), .42), .02, seg=6))
        P.add(leg, L.ellipsoid((x, y - .04, .045), (.045, .07, .04), seg=10))                                # paw
        for dx in (-.025, 0, .025):
            P.add(leg, L.claw((x + dx, y - .08, .04), (x + dx * 1.4, y - .16, .006), .016, bend=(0, 0, .01), seg=6))
    P.split = {f"AccentLeg{i}": HOUND_KNEE for i in range(4)}
    return P


def hunter(L):
    """The hunter, 2.3 m, arrives after alarms and glides: a cuttlefish mantle hunched forward, broad at the
    shoulders and frayed into fin ribbons that trail off the floor behind it, the head thrust low in front with a
    glowing eye band, arms reaching forward with long blade fingers. No legs (the prefab has no walking parts).
    Contract: eye point (0, -0.3, 1.9) — the head is thrust forward of it (the eyes at y -0.45..-0.7)."""
    P = Parts()
    P.add("Body", L.loft([(.5, .0, .0, 2.0, 0, .12), (.6, .14, .11, 2.2, 0, .12), (.9, .23, .17, 2.4, 0, .1),
                          (1.25, .3, .21, 2.4, 0, .05), (1.55, .38, .25, 2.4, 0, -.02), (1.78, .44, .28, 2.4, 0, -.1),
                          (1.93, .36, .25, 2.4, 0, -.18), (2.02, .2, .17, 2.2, 0, -.24)], seg=28))           # mantle
    for k in range(12):                                                                                       # fin ribbons
        a = (k + .5) / 12 * math.tau
        c, sn = math.cos(a), math.sin(a)
        root = V((c * .2, .1 + sn * .15, .82))
        back = .12 + .18 * max(0.0, sn)
        pts = [root, root + V((c * .12, sn * .08 + back * .4, -.3)), root + V((c * .2, sn * .12 + back, -.6))]
        rib = L.tentacle(pts, .07, .02, res=2, per=5)
        for bm in rib:                                                                                      # flatten across
            for v in bm.verts:
                d = v.co - V((pts[0].x, pts[0].y, v.co.z))
                v.co -= V((-sn, c, 0)) * d.dot(V((-sn, c, 0))) * .7
        P.add("Accent" if k % 2 else "Body", rib)
    P.add("Eye", L.tentacle([(0, .2, .95), (0, .26, 1.35), (0, .24, 1.7), (0, .12, 1.98)], .028, .028, res=2, per=5))  # dorsal seam
    for k in range(3):
        z = 1.2 + k * .22
        P.add("Accent", L.cone((0, .24 - k * .03, z), (0, .38 - k * .04, z + .14), .045, seg=8))           # back fins
    # arms reaching forward with long blade fingers
    for sx in (-1, 1):
        sh, el, wr = (sx * .4, -.12, 1.8), (sx * .56, -.3, 1.42), (sx * .46, -.78, 1.2)
        P.add("Body", L.ellipsoid(sh, (.13, .15, .12), seg=16))
        P.add("Body", L.chain([sh, el, wr], [.075, .06, .05], n=2.4))
        P.add("Accent", L.ball(.06, el) + L.ellipsoid(wr, (.07, .08, .06), seg=12))
        for k, (dx, dz) in enumerate(((-.06, .05), (0, 0), (.06, -.05))):
            base = V(wr) + V((sx * dx * .5, -.05, dz * .5))
            tipp = V(wr) + V((sx * dx * 1.6, -.65, dz - .32))
            P.add("Accent", L.claw(base, tipp, .026, bend=(0, -.02, .07)))
    # neck and head thrust low in front
    P.add("Body", L.tentacle([(0, -.15, 1.92), (0, -.3, 1.97), (0, -.42, 1.93)], .1, .085))
    P.add("Accent", L.loft([(0, .15, .14, 2.4), (.15, .16, .15, 2.4, 0, -.01), (.32, .1, .1, 2.4, 0, -.04),
                            (.46, .055, .055, 2.2, 0, -.07)], axis="-Y", loc=(0, -.38, 1.92), round_caps=.03))  # skull
    for dx in (0, -.09, .09):
        P.add("Body", L.claw((dx, -.44, 2.04), (dx * 2, -.15, 2.2), .035, bend=(0, .02, .05)))            # swept horns
    P.add("Eye", L.box((.34, .05, .045), (0, -.5, 1.93), r=.012, rot=(math.radians(-12), 0, 0)))          # eye band
    P.add("Eye", L.eyes([(sx * x, y, z) for sx in (-1, 1) for x, y, z in ((.15, -.58, 1.97), (.11, -.68, 1.94))],
                        [.032, .032, .024, .024]))
    P.add("Eye", L.box((.08, .05, .02), (0, -.76, 1.84), r=.008))                                           # maw
    P.add("Body", L.loft([(0, .09, .05, 2.4), (.3, .04, .025, 2.4)], axis="-Y", loc=(0, -.45, 1.82), round_caps=.02))  # jaw
    return P


WARDEN_WHEELS = [(-.24, -.2), (.24, -.2), (-.24, .2), (.24, .2)]


def warden(L):
    """The warden, 1.8 m: an armoured isopod reared up into a totem — banded segments narrowing upward over a skirt
    of small legs that hides its wheels, a cobra hood that turns, with one huge eye in the middle and a glowing crest.
    Contract (poi.py): wheels r 0.085 at (±0.24, ±0.2, 0.085); AccentHead z 1.3..1.75 turning about the vertical
    axis; EyeLens on the hood's face (y ~ -0.24, z 1.37..1.73) and EyeBeacon on its top (z ~1.71..1.79)."""
    P = Parts()
    for i, (x, y) in enumerate(WARDEN_WHEELS):
        c = (x, y, .085)
        P.add(f"AccentWheel{i}", small_wheel(c, .085, .08))
        P.add("Body", L.cyl(.025, .1, (x * .72, y, .085), axis="X", seg=10))                               # axles
    P.add("Body", L.ellipsoid((0, 0, .2), (.33, .3, .1), seg=24))                                           # belly

    def arch(z):  # the reared body arches back, its belly (front) hollow like an isopod standing up
        return .02 + .09 * math.sin(math.pi * min(1.0, z / 1.3))

    bands = [(.24, .38, .3), (.42, .37, .29), (.6, .34, .26), (.78, .3, .23), (.95, .25, .2), (1.1, .2, .16),
             (1.24, .15, .13)]
    for k, (z, a, b) in enumerate(bands):
        h = .2 if k < len(bands) - 1 else .16
        y = arch(z)
        P.add("Body", L.loft([(z - h / 2, a * .88, b * .88, 2.2, 0, y), (z - h / 4, a, b, 2.2, 0, y),
                              (z + h / 4, a * .97, b * .97, 2.2, 0, y), (z + h / 2 + .02, a * .82, b * .82, 2.2, 0, y)],
                             seg=24))                                                                      # body rings
        P.add("Body", L.ellipsoid((0, y + b * .62, z + .02), (a * .92, b * .5, h * .62), seg=12))          # dorsal plate
        P.add("Accent", L.loft([(z + h / 2 - .02, a * .9, b * .9, 2.2, 0, y), (z + h / 2 + .01, a * .9, b * .9, 2.2, 0, y)],
                               seg=28))
        if 1 <= k <= 5:                                                                                       # belly legs
            for sx in (-1, 1):
                root = V((sx * a * .4, y - b * .7, z))
                knee = root + V((sx * .13, -.12, .02))
                foot = knee + V((-sx * .04, -.04, -.14))
                P.add("Accent", L.chain([root, knee, foot], [.03, .024, .013], n=2.2, seg=10))
    for sx in (-1, 1):                                                                                        # side legs
        for k in range(5):
            yy = -.28 + k * .14
            root = V((sx * .3, yy + .04, .3))
            knee = V((sx * .47, yy, .3))
            foot = V((sx * .45, yy - .02, .04))
            P.add("Accent", L.chain([root, knee, foot], [.035, .03, .018], n=2.2, seg=10))
    P.add("Eye", L.loft([(.6, .345, .265, 2.2, 0, arch(.6)), (.62, .345, .265, 2.2, 0, arch(.6))], seg=28))   # seam
    P.add("Body", L.cyl(.1, .22, (0, 0, 1.36)))                                                             # neck
    # the hood (turns): a cobra's fan framing one huge eye
    hood = L.loft([(-.2, .12, .1, 2.0), (-.1, .38, .08, 2.2), (.04, .56, .075, 2.2), (.18, .52, .07, 2.2),
                   (.3, .34, .06, 2.2), (.38, .08, .05, 2.0)], loc=(0, .02, 1.5), round_caps=.0)
    P.add("AccentHead", hood)
    P.add("AccentHead", L.cyl(.12, .1, (0, 0, 1.36)))                                                       # collar
    for sx in (-1, 1):
        for k in range(5):
            a = -.4 + k * .42
            P.add("AccentHead", L.tentacle([(sx * .12, .08, 1.54), (sx * .33 * math.cos(a), .095, 1.54 + .26 * math.sin(a)),
                                           (sx * .52 * math.cos(a), .085, 1.54 + .32 * math.sin(a))], .022, .014,
                                           res=1, per=4))                                                     # hood ribs
        P.add("Eye", L.ellipsoid((sx * .4, -.06, 1.6), (.04, .02, .04), seg=10))                            # hood spots
    P.add("EyeLens", L.lathe([(.17, 0), (.17, .008), (.14, .02), (.08, .028), (0, .03)], axis="-Y",
                             loc=(0, -.218, 1.55)))
    P.add("AccentHead", L.ellipsoid((0, -.1, 1.55), (.17, .12, .17), seg=24))                              # eye socket
    P.add("AccentHead", L.lathe([(.19, -.03), (.19, 0), (.17, .012), (0, .012)], axis="-Y",
                                loc=(0, -.2, 1.55)))                                                         # lens rim
    P.add("EyeBeacon", L.ellipsoid((0, -.16, 1.75), (.055, .05, .04), seg=16))
    return P


def small_wheel(c, r, w):
    """A low wheel: a rounded tyre and a hub (16 segments: under the skirt only its spin shows)."""
    tyre = s.lathe([(r * .6, -w / 2), (r * .9, -w / 2), (r, -w * .25), (r, w * .25), (r * .9, w / 2), (r * .6, w / 2)],
                   seg=16, axis="X", loc=c)
    return [tyre, s.rcyl(r * .64, w * .86, c, axis="X", seg=16, r=.004, bevel_seg=1)]


DESIGNS = {"robot_guard": guard, "robot_hound": hound, "robot_hunter": hunter, "robot_warden": warden}
