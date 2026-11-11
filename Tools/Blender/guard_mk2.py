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


def torso():
    shell = s.loft([
        (1.07, .23, .155, 3.0, 0, 0),
        (1.22, .29, .185, 3.4, 0, -.005),
        (1.42, .355, .21, 4.0, 0, -.02),
        (1.56, .35, .205, 4.0, 0, -.015),
        (1.645, .25, .16, 3.0, 0, 0),
    ], seg=48, round_caps=.035)
    s.groove(shell, "Z", 1.30, .014, .007)       # abdomen / chest plate
    s.groove(shell, "X", -.2, .012, .006)        # side panels
    s.groove(shell, "X", .2, .012, .006)
    pelvis = s.loft([(.85, .16, .12, 3.2), (.95, .2, .14, 3.6), (1.03, .19, .135, 3.4)], seg=40, round_caps=.025)
    pack = s.rbox((.42, .17, .42), (0, .255, 1.38), r=.045, seg=3, taper=(.92, .95))
    s.groove(pack, "Z", 1.3, .012, .006)
    pauldrons = s.mirror_x(s.rbox((.2, .25, .13), (.405, 0, 1.6), r=.055, seg=3, taper=(.75, .85), rot=(0, math.radians(-14), 0)))
    upper_arms = s.mirror_x(s.loft([(1.22, .052, .055, 2.6, .43, 0), (1.36, .064, .066, 2.6, .435, 0),
                                    (1.5, .06, .062, 2.6, .425, 0)], seg=20, round_caps=.02))
    forearms = s.mirror_x(s.loft([(.875, .062, .066, 3.0, .44, -.012), (1.04, .085, .09, 3.4, .44, -.02),
                                  (1.18, .068, .072, 3.0, .435, -.01)], seg=28, round_caps=.022))
    for f in forearms:
        pass
    return [shell, pelvis, pack, *pauldrons, *upper_arms, *forearms]


def hands():
    parts = []
    palm = s.rbox((.075, .105, .1), (.445, -.012, .84), r=.022, seg=2)
    parts.append(palm)
    for k, y in enumerate((-.05, -.012, .026)):
        parts.append(s.rbox((.024, .028, .085), (.455, y, .765), r=.01, seg=2, rot=(0, math.radians(-8), 0)))
    parts.append(s.rbox((.024, .03, .07), (.405, -.045, .81), r=.01, seg=2, rot=(math.radians(15), math.radians(25), 0)))
    out = []
    for p in parts:
        out += s.mirror_x(p)
    return out


def joints():
    parts = []
    parts += s.mirror_x(s.ball(.082, (.395, 0, 1.53), seg=24))                     # shoulders
    parts += s.mirror_x(s.ball(.064, (.43, 0, 1.2), seg=20))                       # elbows
    parts += s.mirror_x(s.ball(.042, (.445, -.012, .885), seg=16))                 # wrists
    # ribbed waist and neck
    parts.append(s.lathe([(0, 1.0), (.13, 1.0), (.13, 1.03), (.118, 1.045), (.13, 1.06), (.13, 1.075), (.118, 1.09),
                          (.128, 1.105), (.128, 1.12), (0, 1.12)], seg=32))
    parts.append(s.lathe([(0, 1.6), (.08, 1.6), (.08, 1.625), (.07, 1.64), (.08, 1.655), (.08, 1.67), (.07, 1.685),
                          (.078, 1.70), (.078, 1.72), (0, 1.72)], seg=24))
    # belt, pouches, baton
    belt = s.loft([(.965, .207, .148, 3.6), (1.03, .2, .143, 3.6)], seg=40)
    parts.append(belt)
    parts += s.mirror_x(s.rbox((.075, .05, .07), (.13, -.16, .985), r=.014, seg=2))
    parts.append(s.rbox((.04, .05, .04), (.205, .05, .985), r=.01, seg=1))      # baton holster
    parts.append(s.rcyl(.019, .34, (.215, .05, .84), seg=12, r=.008))           # baton
    # head
    head = s.loft([(1.69, .165, .155, 3.4), (1.79, .2, .18, 4.0, 0, -.01), (1.9, .195, .175, 4.0, 0, -.005),
                   (1.98, .16, .145, 3.4)], seg=40, round_caps=.04)
    s.groove(head, "Z", 1.735, .012, .006)
    parts.append(head)
    parts += s.mirror_x(s.rcyl(.062, .05, (.205, .01, 1.84), axis="X", seg=24, r=.012))       # ear pods
    parts.append(s.lathe([(.088, 0), (.092, .012), (.092, .055), (.078, .07), (.078, .085), (0, .085)],
                         seg=32, axis="-Y", loc=(0, -.165, 1.845)))                          # lens barrel
    parts.append(s.tube([(.13, .07, 1.95), (.15, .09, 2.04), (.155, .11, 2.12)], .007, res=1))  # antenna
    # chest badge and back-pack details
    parts.append(s.rbox((.09, .02, .11), (-.17, -.228, 1.45), r=.012, seg=2, rot=(math.radians(-6), 0, 0)))
    parts += s.grille((0, .34, 1.42), .26, .2, n=5, axis="Y")
    parts += s.bolts([(x, .345, z) for x in (-.17, .17) for z in (1.22, 1.55)], .014, .012, axis="Y")
    parts.append(s.tube([(.17, .23, 1.24), (.25, .16, 1.17), (.21, .06, 1.09)], .014, res=1))   # hose to the waist
    return parts


def eye():
    lens = s.lathe([(.068, 0), (.068, .006), (.055, .02), (.03, .028), (0, .03)], seg=32, axis="-Y", loc=(0, -.248, 1.845))
    tip = s.ball(.016, (.155, .11, 2.125), seg=12)
    visor = s.rbox((.2, .02, .026), (0, -.188, 1.92), r=.008, seg=2)
    badge = s.rbox((.05, .012, .016), (-.17, -.24, 1.475), r=.005, seg=1, rot=(math.radians(-6), 0, 0))
    return [lens, tip, visor, badge]


def leg(sign):
    x = sign * HIP_X
    thigh = s.loft([(KNEE + .02, .066, .07, 3.0, x, 0), (.7, .086, .092, 3.3, x, -.005), (.88, .08, .085, 3.0, x, 0)],
                   seg=28, round_caps=.02)
    hip = s.ball(.078, (x, 0, .9), seg=20)
    knee = s.rcyl(.058, .13, (x, 0, KNEE), axis="X", seg=20, r=.012)
    kneecap = s.rbox((.1, .05, .11), (x, -.07, KNEE + .01), r=.024, seg=2, taper=(.85, 1))
    shin = s.loft([(.1, .058, .062, 3.0, x, 0), (.28, .074, .084, 3.4, x, .008), (KNEE - .01, .064, .07, 3.0, x, 0)],
                  seg=28, round_caps=.02)
    s.groove(shin, "Z", .2, .01, .005)
    ankle = s.ball(.048, (x, 0, .1), seg=16)
    foot = s.rbox((.135, .27, .075), (x, -.045, .05), r=.03, seg=3, taper=(.92, .85))
    toe = s.rbox((.13, .075, .06), (x, -.165, .035), r=.026, seg=2)
    sole = s.rbox((.145, .3, .022), (x, -.05, .011), r=.007, seg=1)
    obj = s.part("AccentLeg0" if sign < 0 else "AccentLeg1", [thigh, hip, knee, kneecap, shin, ankle, foot, toe, sole])
    return s.split_at(obj, KNEE)


def build():
    s.clear_scene()
    objs = [s.part("Body", torso()), s.part("Accent", hands() + joints()), s.part("Eye", eye())]
    objs += leg(-1) + leg(1)
    return objs


if __name__ == "__main__":
    out = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.getcwd(), "guard_mk2.fbx")
    s.export_fbx(out, build())
