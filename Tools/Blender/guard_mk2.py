"""Guard robot Mk2 — the pilot of shapes.py: the same contract as the base guard, built from lofts, lathes, tubes,
seams and bevels instead of boxes.

Run:  blender.exe -b --factory-startup --python Tools/Blender/guard_mk2.py -- <out.fbx>
Then: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <old.fbx> <out.fbx>

Contract (generate_models.model_robot_guard): height ~2 m, front -Y; parts Body (shell, arms), Accent (head, joints,
hands, details), AccentLeg0/1 hanging from the hip at x = -/+0.22, z ~0.9, cut at the knee z = 0.45 into Upper/Lower,
Eye (glowing in the status colour) with the camera lens at about (0, -0.22, 1.84).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shapes as s  # noqa: E402

KNEE = 0.45
HIP_X = 0.22


ARM_X = 0.46
BATON_TILT = math.radians(40)  # below the horizontal, pointing forward


def baton_at(d):
    """A point d metres from the grip along the baton (the grip is in the right fist)."""
    return (ARM_X + .015, -.03 - d * math.cos(BATON_TILT), .69 - d * math.sin(BATON_TILT))


def torso():
    """Shaped chest over a narrower abdomen, wide pelvis, layered shoulder plates, a backpack seated into the back."""
    chest = s.loft([(1.26, .28, .24, 3.4, 0, -.01), (1.4, .36, .3, 4.0, 0, -.045), (1.54, .37, .29, 4.2, 0, -.035),
                    (1.66, .27, .22, 3.2, 0, -.01)], seg=28, round_caps=.04)
    abdomen = s.loft([(1.0, .2, .16, 3.0), (1.15, .24, .19, 3.2), (1.32, .26, .21, 3.4)], seg=24, round_caps=.02)
    pelvis = s.loft([(.84, .2, .15, 3.2), (.94, .25, .17, 3.6), (1.04, .235, .165, 3.4)], seg=24, round_caps=.03)
    plate = s.rbox((.44, .07, .26), (0, -.33, 1.46), r=.03, seg=2, taper=(.82, 1.0), rot=(math.radians(-14), 0, 0))
    pack = s.rbox((.44, .24, .52), (0, .32, 1.43), r=.05, seg=3, taper=(.9, .95))
    shoulders = s.mirror_x(s.rbox((.22, .27, .1), (.43, 0, 1.6), r=.034, seg=2, taper=(.85, .9), rot=(0, math.radians(25), 0)))
    shoulders += s.mirror_x(s.rbox((.1, .2, .08), (.5, 0, 1.49), r=.028, seg=2, rot=(0, math.radians(62), 0)))
    upper_arms = s.mirror_x(s.loft([(1.2, .075, .078, 3.0, ARM_X, 0), (1.36, .085, .088, 3.2, ARM_X, 0),
                                    (1.5, .08, .082, 3.0, ARM_X - .01, 0)], seg=12, round_caps=.02))
    forearms = s.mirror_x(s.loft([(.88, .1, .105, 3.4, ARM_X + .01, -.01), (1.04, .125, .13, 3.8, ARM_X + .01, -.02),
                                  (1.17, .095, .1, 3.4, ARM_X, -.01)], seg=16, round_caps=.026))
    return [chest, abdomen, pelvis, plate, pack, *shoulders, *upper_arms, *forearms]


def hands():
    """Big mitten hands: palm, curled finger block split by a seam, thumb — they must read at 64 px."""
    x = ARM_X + .01
    palm = s.rbox((.14, .19, .15), (x, -.015, .8), r=.035, seg=2, taper=(1.0, 1.05))
    fingers = s.rbox((.13, .18, .1), (x + .005, -.03, .69), r=.04, seg=2, rot=(math.radians(-12), 0, 0))
    s.groove(fingers, "Y", -.03, .01, .007)
    thumb = s.rbox((.05, .065, .1), (x - .085, -.085, .77), r=.02, seg=2, rot=(math.radians(18), math.radians(28), 0))
    out = []
    for p in (palm, fingers, thumb):
        out += s.mirror_x(p)
    return out


def joints():
    parts = []
    parts += s.mirror_x(s.ball(.09, (ARM_X - .05, 0, 1.53), seg=16))           # shoulders
    parts += s.mirror_x(s.ball(.075, (ARM_X, 0, 1.19), seg=14))                # elbows
    parts += s.mirror_x(s.ball(.07, (ARM_X + .01, -.012, .88), seg=12))     # wrists
    parts.append(s.lathe([(0, 1.58), (.085, 1.58), (.085, 1.625), (.074, 1.64), (.085, 1.655), (.085, 1.67),
                          (.074, 1.685), (.082, 1.70), (.082, 1.72), (0, 1.72)], seg=16))   # ribbed neck
    parts.append(s.loft([(.965, .255, .178, 3.6), (1.032, .245, .173, 3.6)], seg=24))     # belt (ends off the pelvis plane)
    parts.append(s.lathe([(.1, 1.6), (.15, 1.615), (.15, 1.665), (.125, 1.70), (.09, 1.705)], seg=24))  # collar
    parts += s.mirror_x(s.rbox((.08, .05, .075), (.15, -.19, .99), r=.015, seg=2))         # pouches
    # head: bigger, a jutting lens under a brow, angular sensor pods
    head = s.loft([(1.69, .19, .145, 3.4), (1.79, .23, .165, 4.0, 0, -.01), (1.9, .225, .16, 4.0, 0, -.005),
                   (1.99, .185, .135, 3.4)], seg=24, round_caps=.045)
    s.groove(head, "Z", 1.74, .012, .006)
    parts.append(head)
    parts.append(s.lathe([(.125, 0), (.13, .015), (.13, .06), (.115, .08), (.115, .1), (0, .1)],
                         seg=24, axis="-Y", loc=(0, -.15, 1.84)))                     # lens barrel
    parts.append(s.rbox((.34, .14, .045), (0, -.17, 1.985), r=.018, seg=2, rot=(math.radians(10), 0, 0)))  # brow
    parts += s.mirror_x(s.rbox((.06, .11, .1), (.235, .01, 1.86), r=.018, seg=2, taper=(.7, .8),
                               rot=(0, math.radians(10), 0)))                        # sensor pods
    parts.append(s.tube([(0, .1, 1.98), (0, .115, 2.06), (0, .13, 2.12)], .008, res=1))  # antenna
    # back pack vent and bolts
    parts += s.grille((0, .44, 1.42), .2, .22, n=4, axis="Y")
    parts += s.bolts([(x, .435, z) for x in (-.14, .14) for z in (1.26, 1.58)], .015, .016, axis="Y")
    # stun baton in the right fist, angled down and forward: the one big prop that says "guard"
    parts.append(s.place(s.rcyl(.032, .5, axis="Y", seg=12, r=.01), baton_at(.2), (BATON_TILT, 0, 0)))
    parts.append(s.place(s.rcyl(.042, .05, axis="Y", seg=12, r=.01), baton_at(.43), (BATON_TILT, 0, 0)))
    return parts


def eye():
    lens = s.lathe([(.1, 0), (.1, .008), (.08, .03), (.045, .045), (0, .05)], seg=24, axis="-Y", loc=(0, -.245, 1.84))
    beacon = s.rbox((.05, .26, .03), (0, -.01, 1.99), r=.012, seg=2)                       # status seen from above
    tip = s.ball(.016, (0, .13, 2.125), seg=10)
    baton = s.place(s.lathe([(.035, 0), (.035, .01), (.024, .025), (0, .03)], seg=12, axis="-Y"), baton_at(.45), (BATON_TILT, 0, 0))
    return [lens, beacon, tip, baton]


def leg(sign):
    """Thick armoured leg: big thigh and shin, a wedge plate on the shin, knee housing, long foot with heel and toe."""
    x = sign * HIP_X
    thigh = s.loft([(KNEE + .02, .088, .094, 3.2, x, 0), (.62, .112, .12, 3.5, x, -.005), (.82, .13, .135, 3.6, x, -.005),
                    (.93, .12, .125, 3.2, x, 0)],
                   seg=16, round_caps=.025)
    hip = s.ball(.1, (x, 0, .9), seg=14)
    knee = s.rcyl(.08, .2, (x, 0, KNEE), axis="X", seg=16, r=.014)
    kneecap = s.rbox((.16, .1, .16), (x, -.06, KNEE + .01), r=.04, seg=2, taper=(.85, .9))
    shin = s.loft([(.11, .072, .078, 3.2, x, 0), (.22, .098, .115, 3.6, x, .02), (.34, .104, .12, 3.6, x, .015),
                   (KNEE - .01, .09, .095, 3.2, x, 0)],
                  seg=16, round_caps=.025)
    shin_plate = s.rbox((.15, .06, .28), (x, -.1, .28), r=.02, seg=2, taper=(.8, 1.0), rot=(math.radians(8), 0, 0))
    ankle = s.ball(.065, (x, 0, .1), seg=12)
    foot = s.rbox((.19, .34, .1), (x, -.1, .06), r=.035, seg=2, taper=(.85, .65))   # wedge: low toe, high instep
    toe = s.rbox((.18, .11, .06), (x, -.28, .035), r=.028, seg=2, rot=(math.radians(-8), 0, 0))
    heel = s.rbox((.17, .08, .1), (x, .085, .055), r=.03, seg=2)
    sole = s.rbox((.2, .5, .024), (x, -.1, .012), r=.008, seg=1)
    obj = s.part("AccentLeg0" if sign < 0 else "AccentLeg1",
                 [thigh, hip, knee, kneecap, shin, shin_plate, ankle, foot, toe, heel, sole])
    return s.split_at(obj, KNEE)


def build():
    s.clear_scene()
    objs = [s.part("Body", torso()), s.part("Accent", hands() + joints()), s.part("Eye", eye())]
    objs += leg(-1) + leg(1)
    return objs


if __name__ == "__main__":
    out = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.getcwd(), "guard_mk2.fbx")
    s.export_fbx(out, build())
