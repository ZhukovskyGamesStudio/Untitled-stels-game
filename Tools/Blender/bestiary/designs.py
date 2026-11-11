"""Every robot, device and sensor module of the game, designed from scratch against the shape language (lang.Lang).

A design is a function design(L) -> Parts. It keeps the CONTRACT of the original model (generate_models.py,
robots.py) because the game swaps meshes by name into prefabs built from the original:
  * exactly the original's part names (Body, Accent, Eye, AccentWheel0..3, AccentRotor0..3, AccentHead, AccentLidar,
    AccentLeg{n}Upper/Lower) — a missing part leaves the original mesh showing, an extra one is never shown;
  * the joints: wheels and rotors spin about the centre of their bounds, legs bend at the hip/knee/foot of the
    original, heads turn about the vertical axis through x = y = 0;
  * the sensor mounts (AssetBuilder.BuildSensorRig): the camera-eye module sits in front of the eye point, microphones
    beside it, ground probes on the floor in front — leave room for them.
Coordinates: metres, Blender x right, -y front (Unity +z), z up.
"""
import math

from mathutils import Vector

import shapes as s
from lang import align


class Parts(dict):
    """role -> list of bmeshes; split = {leg role: knee z} for legs cut into Upper/Lower after merging."""

    def __init__(self):
        super().__init__()
        self.split = {}

    def add(self, role, bms):
        self.setdefault(role, []).extend(bms)
        return self

    def eye(self, pair):
        acc, glow = pair
        return self.add("Accent", acc).add("Eye", glow)


def mx(points):
    """Mirror a list of (x, y, z) across x = 0 and keep both."""
    return list(points) + [(-x, y, z) for x, y, z in points]


# =========================================================================== walkers

GUARD_HIP_X, GUARD_HIP_Z, GUARD_KNEE = 0.22, 0.9, 0.45


def guard(L):
    """Security android, 2 m: a chest wider than the hips, heavy forearms, a helmet with one camera eye, armoured legs.
    Contract: legs AccentLeg0 (x -0.22) / AccentLeg1 (x +0.22) from the hip z 0.9, knee z 0.45, feet forward;
    head centre ~(0, 0, 1.84) with its face at y ~ -0.22 (the camera module mounts at y -0.38)."""
    P = Parts()
    # torso: chest -> waist -> pelvis as one column of lofts
    P.add("Body", L.loft([(1.22, .26, .2, 3.2), (1.42, .36, .24, 3.8, 0, -.02), (1.58, .35, .23, 3.8, 0, -.01),
                          (1.68, .24, .18, 3.0)], round_caps=.04))
    P.add("Body", L.loft([(.86, .21, .16, 3.0), (1.0, .23, .17, 3.2), (1.26, .25, .19, 3.2)], round_caps=.03))
    P.add("Accent", L.loft([(.95, .245, .18, 3.4), (1.03, .245, .18, 3.4)]))                    # belt
    P.add("Accent", L.plate((.4, .06, .24), (0, -.255, 1.44), rot=(math.radians(-10), 0, 0)))    # chest plate
    P.add("Body", L.hull((.42, .2, .46), (0, .25, 1.4)))                                          # back pack
    for sx in (-1, 1):
        sh = (sx * .43, 0, 1.56)
        P.add("Accent", L.plate((.22, .28, .1), (sx * .44, 0, 1.63), rot=(0, sx * math.radians(22), 0)))  # pauldron
        P.add("Accent", L.joint(.085, sh))
        P.add("Body", L.rod(sh, (sx * .47, -.01, 1.2), .07))
        P.add("Accent", L.joint(.07, (sx * .47, -.01, 1.18)))
        P.add("Body", L.loft([(.86, .085, .09, 3.2, sx * .48, -.02), (1.02, .11, .115, 3.6, sx * .48, -.03),
                              (1.17, .09, .095, 3.2, sx * .47, -.01)], round_caps=.025))      # forearm
        P.add("Accent", L.box((.13, .17, .17), (sx * .49, -.03, .78)))                       # fist
    # head: helmet with a brow, the eye in front, a beacon on top
    P.add("Body", L.cyl(.08, .14, (0, 0, 1.7)))                                                # neck
    P.add("Accent", L.loft([(1.7, .17, .15, 3.2), (1.8, .2, .17, 3.6, 0, -.01), (1.92, .19, .16, 3.6),
                            (1.99, .15, .13, 3.0)], round_caps=.04))
    P.add("Accent", L.box((.32, .1, .05), (0, -.15, 1.98), rot=(math.radians(12), 0, 0)))
    P.eye(L.eye(.085 * L.head_scale, (0, -.2, 1.84)))
    P.add("Eye", L.box((.05, .22, .03), (0, 0, 2.0), r=.01))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * GUARD_HIP_X
        P.add(leg, L.ball(.095, (x, 0, GUARD_HIP_Z)))
        P.add(leg, L.loft([(GUARD_KNEE + .02, .085, .09, 3.0, x, 0), (.66, .11, .12, 3.4, x, -.005),
                           (.9, .115, .12, 3.2, x, 0)], round_caps=.02))                      # thigh
        P.add(leg, L.cyl(.075, .19, (x, 0, GUARD_KNEE), axis="X"))                             # knee
        P.add(leg, L.plate((.15, .09, .15), (x, -.07, GUARD_KNEE + .01)))
        P.add(leg, L.loft([(.1, .07, .075, 3.0, x, 0), (.26, .095, .11, 3.4, x, .015),
                           (GUARD_KNEE - .01, .085, .09, 3.0, x, 0)], round_caps=.02))        # shin
        P.add(leg, L.box((.19, .36, .1), (x, -.08, .05), taper=(.85, .7)))                    # boot
    P.split = {"AccentLeg0": GUARD_KNEE, "AccentLeg1": GUARD_KNEE}
    return P


HOUND_LEGS = [(.15, -.35), (-.15, -.35), (.15, .35), (-.15, .35)]
HOUND_HIP, HOUND_KNEE = .6, .3


def hound(L):
    """Sniffer dog, 1.1 m at the head: deep chest, tucked waist, a long snout ending in the glowing nose vents.
    Contract: legs AccentLeg0..3 at (±0.15, ∓0.35) from the hip z 0.6, knee z 0.3; nose module at (0, -1.02, 0.75)."""
    P = Parts()
    P.add("Body", L.loft([(.42, .16, .17, 2.6, 0, .02), (.12, .2, .2, 2.8, 0, .03), (-.2, .19, .2, 2.8, 0, .02),
                          (-.42, .15, .16, 2.6, 0, .01)], axis="-Y", loc=(0, 0, .82), round_caps=.06))     # chest -> hips
    P.add("Accent", L.plate((.3, .5, .05), (0, -.05, 1.03)))                                         # spine plate
    P.add("Body", L.rod((0, -.38, .9), (0, -.6, 1.02), .09))                                         # neck
    P.add("Accent", L.loft([(0, .12, .13, 3.0), (.16, .12, .12, 3.0), (.28, .08, .07, 2.6)], axis="-Y",
                           loc=(0, -.62, 1.0), round_caps=.03))                                       # skull
    P.add("Accent", L.loft([(0, .07, .06, 2.6), (.24, .045, .04, 2.4)], axis="-Y", loc=(0, -.86, .92)))  # snout
    P.add("Eye", L.box((.07, .03, .025), (0, -1.11, .92), r=.008))
    P.add("Eye", L.box((.03, .02, .02), (-.05, -.72, 1.06), r=.006) + L.box((.03, .02, .02), (.05, -.72, 1.06), r=.006))
    for sx in (-1, 1):
        P.add("Accent", L.box((.03, .1, .14), (sx * .09, -.6, 1.15), rot=(math.radians(-20), 0, sx * .2)))  # ears
    P.add("Body", L.rod((0, .42, .9), (0, .62, 1.12), .03))                                          # tail antenna
    P.add("Eye", L.ball(.03, (0, .63, 1.13)))
    for i, (x, y) in enumerate(HOUND_LEGS):
        P.add("Body", L.ball(.1, (x * .9, y, .68)))                                                       # hip socket
        leg = f"AccentLeg{i}"
        front = y < 0
        P.add(leg, L.ball(.075, (x, y, HOUND_HIP)))
        P.add(leg, L.loft([(HOUND_KNEE, .05, .055, 3.0, x, y), (.48, .07, .085, 3.2, x, y + (.0 if front else .02)),
                           (HOUND_HIP + .04, .075, .09, 3.0, x, y)], round_caps=.02))
        P.add(leg, L.joint(.05, (x, y, HOUND_KNEE)))
        P.add(leg, L.rod((x, y, HOUND_KNEE), (x, y, .05), .04))
        P.add(leg, L.box((.09, .14, .05), (x, y - .03, .025), taper=(.8, .7)))                    # paw
    P.split = {f"AccentLeg{i}": HOUND_KNEE for i in range(4)}
    return P


SPIDER_ANGLES = (52, 18, -14, -46)


def spider_leg(i):
    """Hip, knee and foot of leg i (the original's layout: 0-3 right side front to back, 4-7 left)."""
    side = 1 if i < 4 else -1
    a = math.radians(SPIDER_ANGLES[i % 4])
    d = Vector((side * math.cos(a), -math.sin(a), 0))
    c = Vector((0, -.02, 0))
    return c + d * .1 + Vector((0, 0, .16)), c + d * .36 + Vector((0, 0, .34)), c + d * .62 + Vector((0, 0, .02)), d


def spider(L):
    """Wall crawler: a flat armoured thorax and a bulbous abdomen hugging the surface, eight legs with high knees,
    a cluster of eyes in front. Contract: the original's hips/knees/feet (spider_leg), up = away from the wall."""
    P = Parts()
    P.add("Body", L.loft([(0, .14, .11, 2.6), (.16, .17, .13, 2.8), (.3, .12, .09, 2.6)], axis="-Y", loc=(0, .12, .16),
                         round_caps=.04))                                                          # thorax
    P.add("Body", L.loft([(0, .16, .15, 2.4), (.2, .2, .2, 2.2), (.38, .12, .13, 2.2)], axis="Y", loc=(0, .06, .2),
                         round_caps=.06))                                                          # abdomen
    P.add("Accent", L.plate((.2, .26, .04), (0, -.04, .28)))
    P.add("Accent", L.loft([(0, .1, .09, 2.6), (.08, .09, .08, 2.6)], axis="-Y", loc=(0, -.16, .17), round_caps=.02))  # head
    for dx, dz, r in ((-.045, .04, .03), (.045, .04, .03), (-.085, 0, .022), (.085, 0, .022), (0, -.02, .02)):
        P.add("Eye", L.ball(r, (dx, -.255, .17 + dz)))
    for i in range(8):
        hip, knee, foot, d = spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .028) + L.ball(.04, knee))
        P.add(f"AccentLeg{i}Lower", L.rod(knee + (foot - knee) * .02, foot, .022)
              + L.lathe([(0, 0), (.03, .02), (.02, .06), (0, .07)], loc=foot - Vector((0, 0, .02))))
    return P


# =========================================================================== wheeled and tracked

def wheels(P, L, centres, radius, width):
    for i, c in enumerate(centres):
        P.add(f"AccentWheel{i}", L.wheel(c, radius, width))


def listener(L):
    """Blind listener, 1.2 m: a low rover with fenders over four big wheels, a telescopic mast carrying two parabolic
    ear dishes (glowing receivers) and a grilled front microphone. Contract: wheels r 0.22, width 0.12 at
    (±0.43, ∓0.3, 0.22); the mast top ~1.2, microphones mount at (±0.41, 0.1, 1.25)."""
    P = Parts()
    wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    P.add("Body", L.hull((.66, .96, .3), (0, 0, .44), taper=(.9, .92)))
    for x, y in ((-.43, -.3), (.43, -.3), (-.43, .3), (.43, .3)):
        P.add("Accent", L.rod((x * .5, y, .22), (x, y, .22), .045))                                   # axles
        P.add("Body", L.box((.12, .14, .16), (x * .66, y, .3)))                                        # axle boxes
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", L.loft([(0, .08, .17, 2.8), (.12, .08, .16, 2.8)], axis="X",
                                 loc=(sx * .34, sy * .3, .4), round_caps=.02))                         # fender
    P.add("Accent", L.box((.5, .3, .08), (0, .12, .62)))
    P.add("Body", L.cyl(.09, .26, (0, .12, .76)))                                                     # mast sleeve
    P.add("Accent", L.cyl(.055, .36, (0, .12, 1.0)))                                                  # mast
    P.add("Accent", L.rod((-.32, .12, 1.18), (.32, .12, 1.18), .035))                                 # yoke
    for sx in (-1, 1):
        x = sx * .33
        P.add("Body", L.lathe([(0, 0), (.08, .005), (.19, .05), (.25, .11), (.24, .12), (.18, .07), (.07, .03), (0, .03)],
                              axis="X" if sx > 0 else "-X", loc=(x, .12, 1.18)))                     # dish shell
        P.add("Eye", L.lathe([(.035, 0), (.035, .08), (.05, .1), (0, .12)], axis="X" if sx > 0 else "-X",
                             loc=(x, .12, 1.18)))                                                    # receiver horn
    P.add("Accent", L.box((.3, .08, .14), (0, -.5, .5)))
    P.add("Eye", L.ball(.06, (0, -.56, .5)))
    for k in range(3):
        P.add("Accent", L.box((.22, .02, .012), (0, -.545, .46 + k * .04), r=.004))
    return P


def worker(L):
    """Little forklift worker: a low chassis on four wheels, a lift mast in front carrying the crate of loot,
    a headlight bar under it. Contract: wheels r 0.12, width 0.1 at (±0.31, ±0.25, 0.12) — 0: (+,+), 1: (-,+),
    2: (+,-), 3: (-,-); the crate (Accent) at y -0.38..0.08, z 0.95..1.35."""
    P = Parts()
    wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    P.add("Body", L.hull((.56, .7, .3), (0, .02, .34), taper=(.94, .94)))
    for x, y in ((.31, .25), (-.31, .25), (.31, -.25), (-.31, -.25)):
        P.add("Accent", L.rod((x * .4, y, .12), (x, y, .12), .035))                                   # axles
        P.add("Body", L.box((.1, .12, .14), (x * .62, y, .2)))                                        # axle boxes
    P.add("Body", L.hull((.46, .3, .42), (0, .22, .66), taper=(.85, .9)))                             # cab
    P.add("Accent", L.box((.3, .02, .16), (0, .06, .72), r=.01))                                    # cab window frame
    P.add("Eye", L.box((.26, .02, .12), (0, .065, .72), r=.01))
    for sx in (-1, 1):
        P.add("Body", L.box((.05, .07, .9), (sx * .22, -.33, .72)))                                    # mast rails
    P.add("Body", L.box((.48, .05, .06), (0, -.33, 1.16)))
    P.add("Accent", L.box((.5, .42, .05), (0, -.16, .93)))                                           # fork plate
    P.add("Accent", L.box((.48, .4, .34), (0, -.16, 1.13)))                                          # crate
    for k in (-1, 1):
        P.add("Accent", L.box((.5, .03, .05), (0, -.16 + k * .2, 1.13), r=.006))
    P.add("Accent", L.box((.44, .1, .1), (0, -.37, .45)))
    P.add("Eye", L.box((.32, .03, .05), (0, -.42, .45), r=.012))
    P.add("Accent", L.rod((.18, .3, .8), (.18, .32, 1.05), .012))
    P.add("Eye", L.ball(.03, (.18, .32, 1.07)))
    return P


def sweeper(L):
    """Floor cleaner, 0.75 m: a domed puck on three small wheels, side brushes on the floor in front, the lidar
    puck spinning under its belly. Contract: wheels r 0.08, width 0.06 at (-0.28, 0.18), (0.28, 0.18), (0, -0.3),
    z 0.08; AccentLidar centred on x = y = 0 at z 0.22 (it turns about the vertical axis)."""
    P = Parts()
    wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18), (0, -.3)):
        P.add("Accent", L.box((.14, .12, .18), (x, y, .26)))                                            # wheel housings
        for sx in (-1, 1):
            P.add("Accent", L.box((.03, .06, .12), (x + sx * .045, y, .13), r=.008))                    # forks
    P.add("Body", L.loft([(.32, .37, .37, 2.0), (.46, .39, .39, 2.0), (.56, .34, .34, 2.0), (.66, .22, .22, 2.0),
                          (.72, .0, .0)], seg=L.rings(32)))                                            # dome
    P.add("Accent", L.loft([(.42, .395, .395, 2.0), (.48, .395, .395, 2.0)], seg=L.rings(32)))       # bumper ring
    P.add("Accent", L.cyl(.05, .14, (0, 0, .3)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    for sx in (-1, 1):
        c = (sx * .27, -.25, .015)
        P.add("Accent", L.cyl(.13, .02, c))
        for k in range(6):
            a = k / 6 * math.tau
            P.add("Accent", L.box((.02, .14, .012), (c[0] + math.cos(a) * .07, c[1] + math.sin(a) * .07, .028),
                                  rot=(0, 0, a + math.pi / 2), r=.004))
        P.add("Accent", L.rod((sx * .27, -.25, .02), (sx * .27, -.25, .36), .015))
    P.add("Eye", L.box((.26, .03, .05), (0, -.385, .5), r=.012))
    P.add("Eye", L.ball(.03, (0, -.15, .22)))
    P.add("Eye", L.ball(.035, (0, 0, .72)))
    return P


def thumper(L):
    """Seismic thumper: a heavy tracked base, a hunched hull with a hydraulic stamp frame in front pressing two
    sensor pads onto the floor. Contract: tracks (Accent) at x ±0.56, 1.4 long, 0.56 high; the pads (Eye) on the
    floor at (±0.3, -0.85); ground probes mount at (±0.53, -0.8, 0)."""
    P = Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.24, 1.36, .54), (sx * .56, 0, .28)))
    P.add("Body", L.hull((.92, 1.1, .5), (0, .02, .72), taper=(.9, .86)))
    P.add("Body", L.hull((.62, .62, .34), (0, .14, 1.12), taper=(.82, .85)))
    P.add("Accent", L.plate((.5, .06, .3), (0, -.5, .8), rot=(math.radians(-15), 0, 0)))
    P.add("Accent", L.box((.8, .2, .1), (0, .5, 1.0)))                                              # exhaust rack
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.05, .3, (sx * .3, .52, 1.17)))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", L.rod((x, -.42, .7), (x, -.8, .55), .06))                                       # arm
        P.add("Accent", L.rod((x, -.82, .58), (x, -.85, .1), .05, kind="piston"))                  # stamp piston
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
        P.add("Accent", L.cyl(.13, .06, (x, -.85, .07)))
    P.add("Body", L.rod((-.3, -.82, .55), (.3, -.82, .55), .05))
    P.add("Eye", L.box((.3, .03, .06), (0, -.47, .98), r=.012))
    return P


# =========================================================================== tall

def sorter(L):
    """Colour sorter, 1.45 m: a weighted base, a slender column with a cable, a big round scanner face (iris rings
    around a coloured lens). Contract: base disc (Accent) r 0.3; the lens (Eye) at (0, -0.2, 1.25)."""
    P = Parts()
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.3, .04), (.24, .09), (0, .09)]))
    P.add("Body", L.loft([(.08, .12, .1, 2.6), (.6, .08, .07, 2.4), (1.0, .07, .06, 2.4)]))
    P.add("Body", L.hull((.26, .2, .22), (0, 0, 1.0)))
    P.add("Body", L.lathe([(0, 0), (.23, 0), (.25, .05), (.25, .16), (.2, .2), (0, .2)], axis="-Y", loc=(0, .12, 1.25)))
    P.add("Accent", L.lathe([(.25, 0), (.27, .02), (.26, .05), (.21, .05), (0, .05)], axis="-Y", loc=(0, -.08, 1.25)))
    P.add("Eye", L.lathe([(.2, 0), (.2, .01), (.1, .03), (0, .035)], axis="-Y", loc=(0, -.11, 1.25)))
    P.add("Accent", [s.tube([(.06, .1, .1), (.12, .12, .5), (.08, .1, .95)], .015, res=1)])          # cable
    return P


def hunter(L):
    """The hunter, 2.3 m, arrives after alarms: it glides — a hunched armoured torso on a tapering spine over a
    thruster ring, long arms with blades held forward, a long head with a wide visor. No legs: the prefab has no
    walking parts, so it hovers. Contract: eye point (0, -0.3, 1.9)."""
    P = Parts()
    P.add("Body", L.loft([(1.25, .2, .15, 3.0, 0, .02), (1.5, .32, .22, 3.4, 0, -.02), (1.78, .38, .24, 3.6, 0, -.04),
                          (1.92, .28, .2, 3.0, 0, -.02)], round_caps=.05))                       # chest
    P.add("Body", L.loft([(.3, .06, .06, 2.4), (.8, .1, .09, 2.6, 0, .02), (1.3, .17, .13, 2.8, 0, .02)]))  # spine
    P.add("Accent", L.lathe([(0, 0), (.24, .02), (.3, .08), (.26, .14), (.1, .18), (0, .18)], loc=(0, 0, .2)))  # thruster
    P.add("Eye", L.loft([(.17, .25, .25, 2.0), (.2, .2, .2, 2.0)]))
    for k in range(3):
        P.add("Accent", L.box((.08, .3, .04), (0, .1 + k * .02, 1.4 - k * .14), rot=(math.radians(20), 0, 0)))  # vertebrae
    for sx in (-1, 1):
        sh = (sx * .42, -.02, 1.78)
        P.add("Accent", L.plate((.24, .32, .1), (sx * .43, -.02, 1.86), rot=(0, sx * math.radians(25), 0)))
        P.add("Accent", L.joint(.08, sh))
        P.add("Body", L.rod(sh, (sx * .5, -.12, 1.32), .06))
        P.add("Accent", L.joint(.065, (sx * .5, -.12, 1.32)))
        P.add("Body", L.rod((sx * .5, -.12, 1.32), (sx * .44, -.5, 1.02), .055))
        P.add("Accent", L.box((.13, .16, .14), (sx * .44, -.5, 1.02)))                             # wrist
        P.add("Accent", L.box((.05, .5, .1), (sx * .44, -.76, .94), rot=(math.radians(-20), 0, 0)))  # blade
        P.add("Eye", L.box((.015, .42, .02), (sx * .41, -.78, .92), rot=(math.radians(-20), 0, 0), r=.004))
    P.add("Body", L.cyl(.07, .14, (0, -.08, 1.98)))
    P.add("Accent", L.loft([(0, .14, .11, 3.0), (.22, .15, .1, 3.2), (.4, .08, .06, 2.6)], axis="-Y", loc=(0, .06, 2.06),
                           round_caps=.03))                                                        # long head
    P.add("Eye", L.box((.3, .04, .05), (0, -.22, 2.04), r=.012))
    P.add("Accent", L.box((.04, .2, .12), (0, .02, 2.2), rot=(math.radians(25), 0, 0)))            # crest
    return P


# =========================================================================== devices

def drone(L):
    """Quadcopter, 0.9 m across, hovering at head height: a streamlined body, four arms to ducted rotors, a camera
    gimbal under the nose. Contract: rotors AccentRotor0..3 at (+.42,+.42), (-.42,+.42), (+.42,-.42), (-.42,-.42),
    z ~0.065, span 0.36; body within z ±0.09; eye point (0, -0.25, -0.1)."""
    P = Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", L.loft([(.26, 0, 0), (.2, .11, .06, 2.4), (0, .16, .085, 2.6), (-.18, .13, .07, 2.4), (-.26, .05, .04, 2.0)],
                         axis="Y", loc=(0, 0, 0)))                                                   # body, nose at -y
    P.add("Accent", L.plate((.18, .3, .03), (0, .02, .085)))
    for i, (x, y) in enumerate(rot):
        P.add("Body", L.rod((x * .2, y * .2, 0), (x, y, .02), .03))
        P.add("Body", L.lathe([(.2, 0), (.21, .01), (.21, .05), (.19, .06), (.17, .06), (.17, .01), (0, .01), (0, 0)],
                              loc=(x, y, 0)))                                                       # duct
        P.add(f"AccentRotor{i}", L.rotor((x, y, .065), .32))
    P.add("Accent", L.cyl(.05, .06, (0, -.2, -.06)))
    P.eye(L.eye(.06 * L.head_scale, (0, -.24, -.12), depth=.07))
    P.add("Eye", L.box((.12, .02, .02), (0, .26, .02), r=.006))
    return P


def turret(L):
    """Sentry turret: a tripod, a mount with a glowing status collar (it does not turn), a head with a twin barrel
    that turns. Contract: AccentHead within z ~1.17..1.45 turning about x = y = 0; the Eye stays still, so it is the
    collar and the feet, not a lens on the head."""
    P = Parts()
    for k in range(3):
        a = .5 + k * math.tau / 3
        foot = (math.cos(a) * .34, math.sin(a) * .34, .03)
        P.add("Body", L.rod((math.cos(a) * .08, math.sin(a) * .08, .98), foot, .035))
        P.add("Body", L.cyl(.06, .04, foot))
        P.add("Eye", L.ball(.022, (foot[0], foot[1], .07)))
    P.add("Body", L.lathe([(0, 0), (.14, 0), (.15, .04), (.11, .2), (.09, .22), (0, .22)], loc=(0, 0, .9)))
    P.add("Eye", L.loft([(1.1, .125, .125, 2.0), (1.14, .125, .125, 2.0)]))
    P.add("AccentHead", L.hull((.34, .4, .22), (0, .02, 1.28), taper=(.85, .9)))
    P.add("AccentHead", L.cyl(.09, .1, (0, 0, 1.15)))
    for sx in (-1, 1):
        P.add("AccentHead", L.cyl(.04, .4, (sx * .09, -.32, 1.27), axis="Y") + L.cyl(.052, .06, (sx * .09, -.5, 1.27), axis="Y"))
    P.add("AccentHead", L.box((.14, .06, .08), (0, -.18, 1.38)))
    return P


def vacuum(L):
    """Ceiling vacuum (the game turns it upside down): a flat puck with a bumper, a scanner dome and slot on the
    face that looks into the room (-z here). Contract: r 0.36, z -0.05..0.12."""
    P = Parts()
    P.add("Body", L.lathe([(0, 0), (.34, 0), (.36, .02), (.36, .07), (.33, .1), (0, .1)], seg=L.rings(40)))
    P.add("Accent", L.loft([(.03, .37, .37, 2.0), (.07, .37, .37, 2.0)], seg=L.rings(40)))
    P.add("Accent", L.lathe([(0, 0), (.24, 0), (.26, .015), (0, .022)], loc=(0, 0, .1)))
    P.add("Accent", L.cyl(.09, .03, (0, 0, -.005)))
    P.add("Eye", L.lathe([(.07, 0), (.06, .02), (0, .035)], axis="-Z", loc=(0, 0, -.015)))
    P.add("Eye", L.box((.22, .04, .02), (0, -.25, -.002), r=.008))
    return P


def lidar(L):
    """Floor lidar post: a weighted base, a pole, a glowing ring (still), the scanning drum on top (turns).
    Contract: AccentHead z 0.39..0.52 about x = y = 0."""
    P = Parts()
    P.add("Body", L.lathe([(0, 0), (.18, 0), (.18, .03), (.12, .07), (.05, .1), (0, .1)]))
    P.add("Body", L.cyl(.045, .32, (0, 0, .25)))
    P.add("Eye", L.loft([(.38, .07, .07, 2.0), (.4, .07, .07, 2.0)]))
    P.add("AccentHead", L.lathe([(0, 0), (.08, 0), (.1, .02), (.1, .1), (.08, .12), (0, .12)], loc=(0, 0, .395)))
    P.add("AccentHead", L.box((.07, .05, .05), (0, -.1, .455)))
    return P


def pir(L):
    """Wall motion sensor: a mounting plate on the wall (+y) and a faceted fresnel dome looking along -y."""
    P = Parts()
    P.add("Body", L.box((.16, .06, .16), (0, .04, 0)))
    P.add("Eye", L.lathe([(.06, 0), (.058, .02), (.045, .045), (.025, .06), (0, .066)], axis="-Y", loc=(0, .01, 0), seg=10))
    return P


def plate(L):
    """Pressure plate in a doorway, flush with the floor: a steel slab, a glowing border, four studs."""
    P = Parts()
    S, h = 1.1, .012
    P.add("Body", L.box((S - .07, S - .07, h), (0, 0, h / 2), r=.004))
    k = S / 2 - .06
    P.add("Accent", sum([L.box((.07, .07, h + .006), (x * k, y * k, (h + .006) / 2), r=.006) for x in (-1, 1) for y in (-1, 1)], []))
    b, e = .035, h + .004
    P.add("Eye", L.box((S, b, e), (0, S / 2 - b / 2, e / 2), r=.003) + L.box((S, b, e), (0, -S / 2 + b / 2, e / 2), r=.003)
          + L.box((b, S - 2 * b, e), (S / 2 - b / 2, 0, e / 2), r=.003) + L.box((b, S - 2 * b, e), (-S / 2 + b / 2, 0, e / 2), r=.003))
    return P


def security_camera(L):
    """Wall camera: a plate on the wall (+y), an arm, a housing with a sun hood, the lens at y -0.24."""
    P = Parts()
    P.add("Body", L.box((.16, .06, .22), (0, .27, 0)))
    P.add("Accent", L.rod((0, .26, 0), (0, .08, -.02), .025))
    P.add("Accent", L.box((.16, .3, .14), (0, -.05, -.02)))
    P.add("Accent", L.box((.19, .12, .02), (0, -.17, .06), r=.006))
    P.add("Eye", L.lens(.055, (0, -.2, -.02)))
    return P


def laser_emitter(L):
    """Laser gate post, 1.6 m: a square post with a glowing emitter strip on its -y face."""
    P = Parts()
    P.add("Body", L.box((.12, .12, 1.6), (0, 0, .8)))
    P.add("Eye", L.box((.06, .04, 1.4), (0, -.065, .85), r=.008))
    return P


# =========================================================================== sensor modules (origin = mount point)

def attach_eye(L):
    P = Parts()
    P.eye(L.eye(.07, (0, -.04, 0), depth=.12))
    top = .045 if L.eye_kind in ("visor", "slit") else .09                       # the housing's top: the brow sits on it
    P.add("Accent", L.box((.2, .04, .03), (0, -.08, top), r=.008))
    return P


def attach_mic(L):
    P = Parts()
    P.add("Accent", L.cyl(.045, .02, (0, 0, .01)) + L.rod((0, 0, .01), (0, 0, .27), .014))
    P.add("Eye", L.ball(.06, (0, 0, .32)))
    P.add("Body", sum([L.cyl(.066, .012, (0, 0, .32 + d)) for d in (-.03, 0, .03)], []))
    return P


def attach_seismic(L):
    P = Parts()
    P.add("Accent", L.cyl(.08, .03, (0, 0, .3)) + L.rod((0, 0, .3), (0, 0, .09), .02))
    P.add("Eye", L.lathe([(0, 0), (.035, .03), (.04, .1), (0, .1)]))
    return P


def attach_nose(L):
    P = Parts()
    P.add("Accent", L.loft([(0, .09, .085, 2.4), (.2, .06, .055, 2.4), (.26, .05, .045, 2.4)], axis="-Y"))
    P.add("Eye", L.box((.025, .02, .02), (-.02, -.265, .005), r=.005) + L.box((.025, .02, .02), (.02, -.265, .005), r=.005)
          + L.box((.06, .02, .012), (0, -.265, -.025), r=.004))
    return P


# name of the original model -> design
DESIGNS = {
    "robot_guard": guard, "robot_hound": hound, "robot_spider": spider, "robot_listener": listener,
    "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper, "robot_sorter": sorter,
    "robot_hunter": hunter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_pir": pir, "sensor_plate": plate, "security_camera": security_camera,
    "laser_emitter": laser_emitter, "attach_eye": attach_eye, "attach_mic": attach_mic,
    "attach_seismic": attach_seismic, "attach_nose": attach_nose,
}
