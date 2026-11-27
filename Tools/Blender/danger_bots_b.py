"""Danger robots, set B: the mimic crate, the pushy salesman, the guard with a gun, the driller, the exterminator.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/danger_bots_b.py -- <models_root> [names...]
      names: mimic salesman gunner driller exterminator (default: all). Writes <models_root>/Robots/robot_<name>.fbx,
      where AssetBuilder.Model("robot_<name>") finds it (it searches Art/Models by file name).
Then: blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/review.py -- sheet.png <fbx> --shading

Concepts (ComfyUI, Logs/comfy/danger/): mimic_15764690 (corner brackets, toothy seam), salesman_13656512..14 (screen
face, megaphone, tray, bow tie, flag), gunner_10510362 (bandolier, heavy chest), driller_10023141..43 (tracks with road
wheels, arms holding the tool), exterminator_3851697..99 (booms curving up and out with nozzles, gas-mask head, stripes).

Coordinates: metres, Blender x right, -y front (Unity +z), z up; the pivot is on the floor under the centre.
Part roles (AssetBuilder.Paint): Body* body material, Accent* accent, Eye* glowing; AccentWheel* spin about their own
x (RobotAnimator), AccentLeg{n}Upper / Lower walk by ProceduralLegs (cut at the knee). Rotors (AccentRotor*) spin about
the robot's vertical axis only, so the driller's drill is AccentDrill (static: a spin about its own axis is the
integrator's), and the mimic's lid is AccentLid with its origin on the hinge line, like the searchable crate's.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "bestiary"))

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import meshfix  # noqa: E402
import shapes as s  # noqa: E402
from lang import align  # noqa: E402

R = math.radians
MOVING = ("AccentWheel", "AccentLid", "AccentDrill", "AccentRotor", "AccentHead")


# =========================================================================== helpers

def rod(p0, p1, radius, seg=12, r=None):
    """A round bar from p0 to p1 with chamfered ends."""
    L = (Vector(p1) - Vector(p0)).length
    return align(s.rcyl(radius, L, seg=seg, r=radius * .3 if r is None else r), p0, p1)


def bar(p0, p1, w, d, r=.01, seg=2):
    """A rounded square bar (w across x of its frame, d across y) from p0 to p1."""
    L = (Vector(p1) - Vector(p0)).length
    return align(s.rbox((w, d, L), r=r, seg=seg), p0, p1)


def piston(p0, p1, r0, r1=None, share=.55):
    """Hydraulic ram: a thick cylinder from p0 for `share` of the length, a thin chrome rod on to p1, collars at both."""
    r1 = r1 or r0 * .5
    a, b = Vector(p0), Vector(p1)
    m = a.lerp(b, share)
    return [rod(a, m, r0, seg=14), rod(m - (b - a).normalized() * .02, b, r1, seg=10),
            rod(m - (b - a).normalized() * .015, m + (b - a).normalized() * .01, r0 * 1.12, seg=14)]


def wheel(c, radius, width, seg=24):
    """Tyre + hub turning about x; symmetric about its centre (the game spins it about the centre of its bounds)."""
    prof = [(radius * .6, -width / 2), (radius * .9, -width / 2), (radius, -width * .28), (radius, width * .28),
            (radius * .9, width / 2), (radius * .6, width / 2)]
    tyre = s.lathe(prof, seg=seg, axis="X", loc=c)
    hub = s.rcyl(radius * .64, width * .84, c, axis="X", seg=seg, r=.005)
    cap = s.rcyl(radius * .28, width * 1.06, c, axis="X", seg=12, r=.004)
    return [tyre, hub, cap]


def track(size, loc, n=5):
    """A crawler track (w, d, h): a stadium belt, cleats across it, road wheels showing on both sides."""
    w, d, h = size
    x, y, z = loc
    r = h / 2
    out = [s.loft([(-w / 2, r, d / 2, 6.0), (w / 2, r, d / 2, 6.0)], seg=32, axis="X", loc=loc)]
    for k in range(n):
        yy = y - d / 2 + r + (d - 2 * r) * k / (n - 1)
        out.append(s.rcyl(r * .6, w * 1.08, (x, yy, z), axis="X", seg=12, r=.008))
        out.append(s.rcyl(r * .22, w * 1.16, (x, yy, z), axis="X", seg=8, r=.004))
    # cleats on the top run (the top is what the camera sees)
    for k in range(7):
        yy = y - d / 2 + r + (d - 2 * r) * (k + .5) / 7
        out.append(s.rbox((w * .96, .035, .03), (x, yy, z + r), r=.008, seg=1))
    return out


def set_pivot(obj, p):
    """Origin of obj to the world point p (the mesh stays where it is)."""
    p = Vector(p)
    obj.data.transform(Matrix.Translation(-p))
    obj.location = p


def export(root, name, objs):
    bpy.context.view_layer.update()
    meshfix.fix_objects(objs, floor=False, name=name, pinned=lambda n: n.startswith(MOVING))
    path = os.path.join(root, "Robots", name + ".fbx")
    s.export_fbx(path, objs)
    return path


# =========================================================================== mimic

CRATE_W, CRATE_D, CRATE_H = .8, .6, .6       # the searchable crate (poi.model_crate): 0.8 x 0.6 x 0.6, front -y
MIMIC_KNEE = .25
MIMIC_HINGE = (0.0, 0.30, 0.50)


def mimic_leg(i, sx, sy):
    """A corner bracket that is a leg: hip under the lid, a hinge at the knee, the crate's corner cap as its foot.
    Folded flat on the corner it reads as the crate's angle iron; the knee sits 2 cm proud so it bends outwards."""
    c = Vector((sx * .372, sy * .272, 0))
    out = Vector((sx, sy, 0)).normalized()
    hip, knee, foot = c + out * -.004 + Vector((0, 0, .462)), c + out * .009 + Vector((0, 0, MIMIC_KNEE)), \
        c + Vector((0, 0, .03))
    pieces = [bar(hip, knee, .074, .074, r=.012), bar(knee, foot, .078, .078, r=.012)]
    pieces.append(s.rbox((.086, .086, .05), (hip.x, hip.y, .45), r=.014, seg=2))                  # top cap (hip)
    pieces.append(s.rbox((.096, .096, .11), (c.x, c.y, .055), r=.02, seg=2))                      # foot = corner cap
    axis = Vector((-sy, sx, 0)).normalized() * .04
    k = knee + out * .006
    pieces.append(rod(k - axis, k + axis, .02, seg=12))                                           # knee hinge
    obj = s.part(f"AccentLeg{i}", pieces)
    return s.split_at(obj, MIMIC_KNEE)


def mimic():
    """«Мимик»: the searchable crate's double, closed and standing on the floor. Same size and part layout as
    poi.model_crate (walls and ribs = Body, corner pieces, grips, latches and hinges = Accent, the lid AccentLid),
    but the front wall stops 2 cm short of the lid: in that seam a row of teeth and one small eye; the four corner
    brackets are legs. Crate colours come from the paint (Body = Wood, Accent* = Metal), not from the mesh."""
    t = .03
    body = [s.rbox((.78, .58, .05), (0, 0, .025), r=.02, seg=2)]                                   # floor
    body += [s.rbox((t, .58, .5), (x * (.39 - t / 2), 0, .25), r=.012, seg=2) for x in (-1, 1)]    # side walls
    body.append(s.rbox((.78 - 2 * t, t, .5), (0, .29 - t / 2, .25), r=.012, seg=2))                # back wall
    body.append(s.rbox((.78 - 2 * t + .01, t, .48), (0, -.29 + t / 2, .24), r=.012, seg=2))        # front: short
    body.append(s.rbox((.7, .5, .03), (0, 0, .41), r=.01, seg=1))                                  # the mouth's floor
    for z in (.06, .44):
        body += [s.rbox((.02, .6, .045), (x * .39, 0, z), r=.008, seg=1) for x in (-1, 1)]
        body += [s.rbox((.8, .02, .045), (0, y * .29, z), r=.008, seg=1) for y in (-1, 1)]
    s.part("Body", body)

    acc = []
    for x in (-1, 1):                                                                              # side grips
        acc.append(s.rbox((.03, .22, .035), (x * .405, 0, .3), r=.012, seg=2))
        acc += [s.rbox((.04, .03, .05), (x * .395, y * .1, .3), r=.008, seg=1) for y in (-1, 1)]
    for x in (-.22, .22):                                                                          # latches: fangs
        acc.append(s.rbox((.08, .025, .07), (x, -.3, .4), r=.008, seg=2))
        acc.append(s.rbox((.05, .022, .085), (x, -.307, .462), r=.008, seg=2, taper=(.55, 1.0)))
    acc += [s.rbox((.12, .02, .06), (x, .3, .465), r=.006, seg=1) for x in (-.25, .25)]           # hinge leaves
    # lower teeth: little cones up from the front wall into the seam, between the latches
    xs = [-.31, -.14, -.07, .07, .14, .31]
    for x in xs:
        acc.append(s.rcyl(.013, .032, (x, -.279, .482), seg=6, r=.002, radius2=.003))
    s.part("Accent", acc)

    lid = s.rbox((.8, .6, .1), (0, 0, .548), r=.025, seg=3)
    s.inset_faces(lid, s.facing((0, 0, 1), min_area=.1, tol=.95), thickness=.04, depth=-.012)
    knuckles = [s.rcyl(.016, .1, (x, .3, .5), axis="X", seg=12, r=.003) for x in (-.25, .25)]
    leaves = [s.rbox((.1, .016, .07), (x, .303, .54), r=.005, seg=1) for x in (-.25, .25)]
    catches = [s.rbox((.06, .03, .04), (x, -.305, .53), r=.008, seg=1) for x in (-.22, .22)]
    # upper teeth hang from the lid's front edge, between the lower ones
    teeth = [s.rcyl(.003, .03, (x, -.281, .49), seg=6, r=.002, radius2=.013)
             for x in (-.265, -.175, -.105, .105, .175, .265)]
    # a lip hangs from the lid's front over the seam: from outside only a dark slit, the teeth behind it; a notch in
    # the middle for the eye
    lips = [s.rbox((.33, .014, .03), (x * .215, -.293, .505), r=.005, seg=1) for x in (-1, 1)]
    lid_obj = s.part("AccentLid", [lid, *knuckles, *leaves, *catches, *teeth, *lips])
    set_pivot(lid_obj, MIMIC_HINGE)

    # one small eye in the middle of the seam, in a dark socket
    s.part("Eye", [s.lathe([(.017, 0), (.017, .004), (.011, .012), (0, .014)], seg=16, axis="-Y", loc=(0, -.272, .489))])
    s.part("BodySocket", [s.rcyl(.026, .03, (0, -.27, .489), axis="Y", seg=16, r=.004)])
    legs = []
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        legs += mimic_leg(i, sx, sy)
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


# =========================================================================== salesman

HEAD_C, HEAD_TILT = Vector((0, -.07, 1.33)), R(-16)   # the screen face looks up at the top-down camera


def tilt(bm):
    """Turn a piece of the salesman's head about the head's centre (face up)."""
    import bmesh
    bmesh.ops.rotate(bm, cent=HEAD_C, matrix=Matrix.Rotation(HEAD_TILT, 3, "X"), verts=bm.verts)
    return bm


def salesman():
    """«Назойливый продавец»: a kiosk on casters — a striped kiosk roof for a hat, a big screen face with a grin,
    a megaphone on the left of the head, a bow tie, a tray of wares held out in front, a price-tag flag on an antenna."""
    for i, c in enumerate([(-.25, -.2, .09), (.25, -.2, .09), (-.25, .2, .09), (.25, .2, .09)]):
        s.part(f"AccentWheel{i}", wheel(c, .09, .07, seg=18))
    body = []
    body.append(s.loft([(.07, .33, .29, 3.2), (.15, .37, .32, 3.4), (.27, .35, .3, 3.4)], seg=32, round_caps=.03))  # skirt
    cab = s.loft([(.25, .27, .22, 3.6), (.6, .29, .23, 3.8), (.9, .33, .25, 4.0), (1.0, .34, .26, 4.0)],
                 seg=32, round_caps=.03)                                                                          # kiosk
    s.groove(cab, "Z", .62, .012, .006)
    body.append(cab)
    body.append(s.rbox((.74, .6, .045), (0, 0, 1.0), r=.018, seg=2))                                # counter top
    body.append(s.lathe([(.1, .99), (.1, 1.06), (.085, 1.1), (.085, 1.16), (0, 1.16)], seg=20))     # neck
    head = s.rbox((.5, .22, .36), (0, -.07, 1.33), r=.05, seg=3, taper=(1.04, 1.0))
    s.inset_faces(head, s.facing((0, -1, 0), min_area=.05, tol=.95), thickness=.03, depth=-.015)
    body.append(tilt(head))
    for sx in (-1, 1):                                                                              # arms + hands
        body.append(rod((sx * .3, -.02, .8), (sx * .34, -.18, .86), .04))
        body.append(rod((sx * .34, -.18, .86), (sx * .3, -.36, .9), .04))
        body.append(s.rbox((.08, .09, .06), (sx * .3, -.38, .9), r=.022, seg=2))
    # megaphone on the left of the head: bracket, handle, a big flared horn pointing forward and a little out
    mp0, mp1 = Vector((-.33, -.06, 1.3)), Vector((-.5, -.27, 1.4))
    body.append(rod((-.24, -.06, 1.3), mp0, .03))
    s.part("Body", body)

    acc = []
    horn = s.lathe([(.035, -.1525), (.04, -.0925), (.06, .0075), (.1, .1075), (.125, .1475), (.11, .1525),
                    (.0, .1525)], seg=24, axis="Z")                                          # (centred: align)
    acc.append(align(horn, mp0 - (mp1 - mp0).normalized() * .03, mp0 + (mp1 - mp0).normalized() * .275))
    acc.append(rod(mp0 + Vector((0, 0, -.02)), mp0 + Vector((0, -.05, -.11)), .022))                 # horn handle
    roof = s.loft([(1.49, .28, .15, 4, 0, .0), (1.53, .3, .16, 4.5, 0, .0), (1.58, .19, .1, 3.2, 0, .01)], seg=32)          # kiosk roof
    acc.append(roof)
    for k in range(7):                                                                              # awning stripes
        x = -.24 + k * .08
        acc.append(s.rbox((.045, .05, .06), (x, -.165, 1.495), r=.012, seg=1, rot=(R(-30), 0, 0)))
    acc.append(s.rbox((.42, .05, .26), (0, -.225, .76), r=.02, seg=2, rot=(R(-4), 0, 0)))           # sale sign
    acc.append(s.rbox((.36, .05, .1), (0, -.215, .42), r=.02, seg=2))                               # kick plate
    # bow tie
    acc.append(s.rbox((.06, .05, .06), (0, -.11, 1.1), r=.018, seg=2))
    for sx in (-1, 1):
        acc.append(s.rbox((.1, .04, .1), (sx * .07, -.105, 1.1), r=.015, seg=2, taper=(1.0, .8),
                          rot=(0, sx * R(90), 0)))
    # face strokes on the screen: eyes and a wide grin
    for sx in (-1, 1):
        acc.append(tilt(s.rcyl(.03, .02, (sx * .09, -.185, 1.39), axis="Y", seg=14, r=.006)))
    smile = [(math.sin(a) * .13, -.19, 1.3 - math.cos(a) * .05) for a in [R(-70 + 14 * k) for k in range(11)]]
    acc.append(tilt(s.tube(smile, .016, res=1)))
    # the tray held out at chest height, piled with wares
    acc.append(s.rbox((.62, .3, .025), (0, -.43, .925), r=.008, seg=1))
    acc += [s.rbox((.62, .016, .045), (0, -.43 + y * .142, .948), r=.006, seg=1) for y in (-1, 1)]
    acc += [s.rbox((.016, .27, .045), (x * .302, -.43, .948), r=.006, seg=1) for x in (-1, 1)]
    acc.append(s.rbox((.13, .1, .11), (-.18, -.44, .99), r=.014, seg=2))
    acc.append(s.rbox((.12, .14, .08), (-.02, -.45, .975), r=.014, seg=2, rot=(0, 0, R(15))))
    acc.append(s.rbox((.1, .08, .06), (-.08, -.43, 1.06), r=.012, seg=2, rot=(0, 0, R(-10))))
    acc.append(s.lathe([(.04, .93), (.042, 1.04), (.018, 1.09), (.015, 1.14), (0, 1.14)], seg=12, loc=(.18, -.44, 0)))
    acc.append(s.lathe([(.035, .93), (.035, 1.0), (0, 1.0)], seg=12, loc=(.1, -.39, 0)))
    # antenna with a price-tag flag (behind the roof, right side)
    acc.append(s.tube([(.17, .06, 1.55), (.17, .06, 1.7), (.17, .065, 1.84)], .009, res=1))
    acc.append(s.rbox((.17, .012, .1), (.26, .065, 1.775), r=.006, seg=1, taper=(.7, 1.0)))
    acc.append(s.rbox((.02, .2, .02), (0, -.195, .99), r=.006, seg=1))
    s.part("Accent", acc)

    eye = [tilt(s.rbox((.4, .02, .26), (0, -.168, 1.335), r=.025, seg=2))]                          # the screen
    eye.append(s.ball(.022, (.17, .065, 1.85), seg=10))                                              # antenna tip
    s.part("Eye", eye)
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


# =========================================================================== gunner

G_KNEE, G_HIP_X, G_HIP_Z = .46, .24, .92
G_ARM_X = .5
GUN_X, GUN_Z = -.5, 1.1
MUZZLE = (GUN_X, -1.0, GUN_Z)


def gunner_leg(sign):
    """Guard leg, heavier: a thigh plate, a bigger knee housing and a broad boot."""
    x = sign * G_HIP_X
    thigh = s.loft([(G_KNEE + .02, .095, .1, 3.2, x, 0), (.62, .125, .13, 3.5, x, -.005), (.84, .14, .145, 3.6, x, -.005),
                    (.95, .13, .135, 3.2, x, 0)], seg=14, round_caps=.025)
    hip = s.ball(.11, (x, 0, G_HIP_Z), seg=12)
    plate = s.rbox((.2, .07, .26), (x, -.11, .7), r=.025, seg=2, taper=(1.1, 1.0), rot=(R(-6), 0, 0))
    knee = s.rcyl(.088, .22, (x, 0, G_KNEE), axis="X", seg=16, r=.014)
    kneecap = s.rbox((.18, .11, .18), (x, -.07, G_KNEE + .01), r=.045, seg=2, taper=(.85, .9))
    shin = s.loft([(.11, .08, .085, 3.2, x, 0), (.22, .11, .125, 3.6, x, .02), (.34, .115, .13, 3.6, x, .015),
                   (G_KNEE - .01, .1, .105, 3.2, x, 0)], seg=14, round_caps=.025)
    shin_plate = s.rbox((.17, .07, .3), (x, -.11, .28), r=.022, seg=2, taper=(.8, 1.0), rot=(R(8), 0, 0))
    ankle = s.ball(.07, (x, 0, .1), seg=10)
    foot = s.rbox((.22, .38, .11), (x, -.1, .065), r=.035, seg=2, taper=(.85, .65))
    toe = s.rbox((.21, .12, .065), (x, -.3, .038), r=.028, seg=2, rot=(R(-8), 0, 0))
    heel = s.rbox((.2, .09, .11), (x, .09, .06), r=.03, seg=2)
    sole = s.rbox((.23, .54, .026), (x, -.1, .013), r=.008, seg=1)
    obj = s.part("AccentLeg0" if sign < 0 else "AccentLeg1",
                 [thigh, hip, plate, knee, kneecap, shin, shin_plate, ankle, foot, toe, heel, sole])
    return s.split_at(obj, G_KNEE)


def gunner():
    """«Охранник с пушкой»: the guard's family (helmet with one camera eye, two legs cut at the knee, mitten fist)
    made heavier — a broad armoured chest, layered pauldrons, an ammo box on the back feeding a belt to a rotary gun
    that replaces the right forearm (Blender -x = the robot's right), a bandolier across the chest."""
    body = []
    body.append(s.loft([(1.24, .3, .25, 3.4, 0, -.01), (1.38, .4, .31, 4.0, 0, -.045), (1.52, .41, .3, 4.2, 0, -.035),
                        (1.64, .3, .23, 3.2, 0, -.01)], seg=24, round_caps=.04))                       # chest
    body.append(s.loft([(.98, .21, .17, 3.0), (1.14, .25, .2, 3.2), (1.3, .28, .22, 3.4)], seg=24, round_caps=.02))
    body.append(s.loft([(.84, .22, .16, 3.2), (.94, .27, .18, 3.6), (1.03, .25, .17, 3.4)], seg=24, round_caps=.03))
    body.append(s.rbox((.5, .08, .3), (0, -.34, 1.45), r=.035, seg=2, taper=(.82, 1.0), rot=(R(-14), 0, 0)))  # plate
    pack = s.rbox((.52, .3, .54), (0, .38, 1.38), r=.05, seg=3, taper=(.92, .95))                     # ammo box
    s.groove(pack, "Z", 1.5, .014, .008)
    body.append(pack)
    # left arm (blender +x): the guard's arm and mitten
    x = G_ARM_X
    body.append(s.loft([(1.2, .08, .083, 3.0, x, 0), (1.36, .09, .093, 3.2, x, 0), (1.5, .085, .087, 3.0, x - .01, 0)],
                       seg=12, round_caps=.02))
    body.append(s.loft([(.86, .11, .115, 3.4, x + .01, -.01), (1.03, .135, .14, 3.8, x + .01, -.02),
                        (1.17, .1, .105, 3.4, x, -.01)], seg=16, round_caps=.026))
    # right arm (blender -x): upper arm down to the elbow, the gun forward from it
    body.append(s.loft([(1.17, .085, .088, 3.0, -x, 0), (1.36, .095, .098, 3.2, -x, 0), (1.5, .088, .09, 3.0, -x + .01, 0)],
                       seg=12, round_caps=.02))
    gun = s.loft([(-.1, .1, .115, 3.6), (.12, .125, .14, 4.0), (.36, .11, .125, 3.8), (.42, .095, .1, 3.2)],
                 seg=16, axis="-Y", loc=(GUN_X, .02, GUN_Z), round_caps=.03)                           # receiver
    s.groove(gun, "Y", -.18, .012, .007)
    body.append(gun)
    body.append(s.rbox((.06, .16, .07), (GUN_X, -.12, GUN_Z + .14), r=.02, seg=2))                 # top rail
    body.append(s.rbox((.12, .14, .1), (GUN_X + .02, .02, GUN_Z - .1), r=.03, seg=2))               # feed housing
    s.part("Body", body)

    acc = []
    for sx in (-1, 1):                                                                              # pauldrons
        big = 1.3 if sx < 0 else 1.15
        acc.append(s.rbox((.26 * big, .32 * big, .11), (sx * .45, 0, 1.64), r=.04, seg=2, taper=(.85, .9),
                          rot=(0, sx * R(24), 0)))
        acc.append(s.rbox((.14 * big, .26 * big, .09), (sx * .55, 0, 1.52), r=.03, seg=2, rot=(0, sx * R(58), 0)))
        acc.append(s.ball(.1, (sx * (G_ARM_X - .05), 0, 1.53), seg=12))                             # shoulder joint
    acc.append(s.ball(.08, (x, 0, 1.19), seg=12))                                                   # left elbow
    acc.append(s.ball(.075, (x + .01, -.012, .88), seg=12))                                         # left wrist
    acc.append(s.ball(.09, (-x, .0, 1.14), seg=12))                                                 # right elbow
    xh = x + .01                                                                                    # left mitten
    acc.append(s.rbox((.15, .2, .16), (xh, -.015, .79), r=.035, seg=2))
    f = s.rbox((.14, .19, .11), (xh + .005, -.03, .68), r=.04, seg=2, rot=(R(-12), 0, 0))
    s.groove(f, "Y", -.03, .01, .007)
    acc.append(f)
    acc.append(s.rbox((.055, .07, .1), (xh - .09, -.09, .76), r=.02, seg=2, rot=(R(18), R(28), 0)))
    # neck, belt, collar, head (the guard's helmet, a touch lower)
    acc.append(s.lathe([(0, 1.6), (.09, 1.6), (.09, 1.64), (.078, 1.655), (.09, 1.67), (.09, 1.72), (0, 1.72)], seg=16))
    acc.append(s.loft([(.965, .27, .19, 3.6), (1.035, .26, .185, 3.6)], seg=20))
    acc.append(s.lathe([(.1, 1.62), (.16, 1.635), (.16, 1.68), (.13, 1.71), (.09, 1.715)], seg=20))
    head = s.loft([(1.7, .19, .15, 3.4), (1.79, .235, .17, 4.0, 0, -.01), (1.89, .23, .165, 4.0, 0, -.005),
                   (1.965, .19, .14, 3.4)], seg=20, round_caps=.04)
    s.groove(head, "Z", 1.75, .012, .006)
    acc.append(head)
    acc.append(s.lathe([(.125, 0), (.13, .015), (.13, .06), (.115, .08), (.115, .1), (0, .1)], seg=20, axis="-Y",
                       loc=(0, -.15, 1.84)))                                                        # lens barrel
    acc.append(s.rbox((.36, .15, .05), (0, -.17, 1.955), r=.02, seg=2, rot=(R(10), 0, 0)))          # brow
    acc += s.mirror_x(s.rbox((.06, .11, .1), (.24, .01, 1.85), r=.018, seg=2, taper=(.7, .8), rot=(0, R(10), 0)))
    # bandolier from the left shoulder to the right hip, rounds standing on it
    bp = [Vector((.3, -.3, 1.58)), Vector((.05, -.355, 1.36)), Vector((-.22, -.27, 1.08))]
    acc.append(s.tube([tuple(p) for p in bp], .04, res=1))
    for k in range(7):
        t = (k + .5) / 7
        p = (bp[0].lerp(bp[1], t * 2) if t < .5 else bp[1].lerp(bp[2], t * 2 - 1))
        acc.append(s.rcyl(.022, .09, (p.x, p.y - .045, p.z), seg=8, r=.005, bevel_seg=1, rot=(0, R(48), 0)))
    # the gun: rotary barrel cluster in a shroud, a muzzle ring
    for k in range(3):
        a = k / 3 * math.tau + R(90)
        bx, bz = GUN_X + math.cos(a) * .045, GUN_Z + math.sin(a) * .045
        acc.append(s.rcyl(.032, .56, (bx, -.7, bz), axis="Y", seg=10, r=.006, bevel_seg=1))
    acc.append(s.rcyl(.11, .12, (GUN_X, -.45, GUN_Z), axis="Y", seg=20, r=.012))                  # shroud collar
    acc.append(s.rcyl(.1, .07, (GUN_X, -.955, GUN_Z), axis="Y", seg=20, r=.012))                  # muzzle shroud
    acc.append(s.rcyl(.1, .04, (GUN_X, -.72, GUN_Z), axis="Y", seg=20, r=.008))                   # barrel clamp
    acc.append(s.rcyl(.15, .12, (GUN_X - .02, -.06, GUN_Z - .17), axis="X", seg=20, r=.015))      # ammo drum
    acc.append(s.rcyl(.04, .03, (GUN_X, -.5, GUN_Z), axis="Y", seg=12, r=.006))                   # (spindle)
    acc.append(rod((GUN_X, -.42, GUN_Z), (GUN_X, -.97, GUN_Z), .03, seg=10))
    # ammo belt: ribbed, from the box's right side round under the shoulder to the feed housing
    path = [Vector((-.25, .42, 1.22)), Vector((-.4, .34, 1.12)), Vector((-.5, .2, 1.0)), Vector((-.49, .06, 1.0))]
    pts, radii = [], []
    for k in range(25):
        t = k / 24
        seg_i = min(2, int(t * 3))
        u = t * 3 - seg_i
        pts.append(tuple(path[seg_i].lerp(path[seg_i + 1], u)))
        radii.append(1.0 if k % 2 == 0 else .7)
    acc.append(s.tube(pts, .045, res=1, smooth=False, radii=radii))
    acc += s.bolts([(xx, .535, zz) for xx in (-.17, .17) for zz in (1.22, 1.56)], .016, .016, axis="Y")
    s.part("Accent", acc)

    eye = [s.lathe([(.1, 0), (.1, .008), (.08, .03), (.045, .045), (0, .05)], seg=24, axis="-Y", loc=(0, -.245, 1.84))]
    eye.append(s.rbox((.05, .26, .03), (0, -.01, 1.975), r=.012, seg=2))                            # status on top
    s.part("Eye", eye)
    gunner_leg(-1)
    gunner_leg(1)
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


# =========================================================================== driller

D_AXIS_Z = .62
DRILL_BASE_Y, DRILL_LEN = -.4, .5
DRILL_TIP = (0, DRILL_BASE_Y - DRILL_LEN, D_AXIS_Z)


def driller():
    """«Бурильщик»: a heavy tracked breacher — a low armoured hull, a big fluted cone drill on a gearbox in front,
    carried by two hydraulic arms, a small armoured camera box with a visor slit, exhaust stacks behind."""
    tracks = []
    for sx in (-1, 1):
        tracks += track((.22, 1.0, .42), (sx * .4, .1, .21))
    s.part("BodyTracks", tracks)                    # grey: the accent stays on the drill and its arms
    acc = []
    body = []
    body.append(s.rbox((.6, .9, .26), (0, .1, .36), r=.03, seg=2))                                 # chassis
    hull = s.loft([(.42, .37, .44, 6.0, 0, .08), (.8, .36, .43, 5.0, 0, .1), (1.08, .3, .34, 4.0, 0, .14)],
                  seg=32, round_caps=.04)
    s.groove(hull, "Z", .62, .014, .008)
    body.append(hull)
    body.append(s.rbox((.62, .1, .36), (0, -.37, .66), r=.03, seg=2, taper=(.92, 1.0), rot=(R(-32), 0, 0)))  # glacis
    for sx in (-1, 1):
        body.append(s.rbox((.2, .5, .04), (sx * .4, .2, .44), r=.012, seg=2))                       # rear fenders
    body.append(s.rbox((.34, .3, .17), (0, -.06, 1.15), r=.04, seg=2, taper=(.85, .9)))             # camera box
    body.append(s.rbox((.4, .08, .05), (0, -.2, 1.235), r=.018, seg=2, rot=(R(12), 0, 0)))          # its brow
    body.append(s.lathe([(.29, 0), (.3, .02), (.3, .1), (.27, .13), (.26, .14)], seg=28, axis="-Y",
                        loc=(0, -.27, D_AXIS_Z)))                                                   # gearbox
    body.append(s.rcyl(.05, .5, (.2, .4, 1.22), seg=12, r=.01))                                     # exhaust stack
    body += s.grille((0, .532, .7), .4, .2, n=4, axis="Y")
    s.part("Body", body)

    for sx in (-1, 1):
        sh, wr = (sx * .3, -.1, .98), (sx * .27, -.36, .78)
        acc += piston(sh, wr, .06)
        acc.append(s.ball(.06, sh, seg=14))
        acc.append(s.ball(.05, wr, seg=12))
        lo0, lo1 = (sx * .3, -.25, .36), (sx * .22, -.36, .44)
        acc += piston(lo0, lo1, .045)
        acc.append(s.ball(.045, lo0, seg=12))
        acc.append(s.rbox((.06, .12, .2), (sx * .3, -.04, .94), r=.02, seg=2))                     # shoulder lugs
    acc.append(s.rcyl(.315, .05, (0, -.395, D_AXIS_Z), axis="Y", seg=28, r=.01))                  # drive ring
    acc.append(s.rcyl(.06, .05, (.2, .4, 1.475), seg=12, r=.01))                                    # exhaust cap
    s.part("Accent", acc)

    # the drill: a fluted cone along -y, symmetric about its axis (two-start helix)
    y0 = DRILL_BASE_Y
    cone = s.lathe([(.29, 0), (.295, .03), (.25, .12), (.17, .28), (.075, .42), (.0, DRILL_LEN)], seg=20, axis="-Y",
                   loc=(0, y0, D_AXIS_Z))
    drill = [cone]
    prof = [(.29, 0), (.295, .03), (.25, .12), (.17, .28), (.075, .42), (.0, DRILL_LEN)]

    def cone_r(d):
        for (r0, d0), (r1, d1) in zip(prof, prof[1:]):
            if d <= d1:
                return r0 + (r1 - r0) * (d - d0) / (d1 - d0)
        return 0.0

    for start in (0, math.pi):
        pts, radii = [], []
        for k in range(17):
            t = .04 + k / 16 * .82
            a = start + t * 2.2 * math.tau
            rr = cone_r(t * DRILL_LEN) + .004
            pts.append((math.cos(a) * rr, y0 - t * DRILL_LEN, D_AXIS_Z + math.sin(a) * rr))
            radii.append(max(.25, 1 - t))
        drill.append(s.tube(pts, .034, res=1, radii=radii, segments_per=3))
    s.part("AccentDrill", drill)

    eye = [s.lathe([(.05, 0), (.05, .01), (.035, .025), (0, .03)], seg=16, axis="-Y", loc=(0, -.195, 1.15))]
    eye.append(s.rbox((.2, .012, .025), (0, -.203, 1.195), r=.006, seg=1))                         # visor slit
    s.part("Eye", eye)
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


# =========================================================================== exterminator

def exterminator():
    """«Дезинсектор»: a pest-control sprayer on four wheels — a gas-mask head with one camera lens and two filter
    cans, two tall gas tanks with hazard bands on the back, hoses up to two booms curving out and up with nozzles at
    their ends, a third nozzle straight up, a striped bumper."""
    for i, c in enumerate([(-.36, -.24, .14), (.36, -.24, .14), (-.36, .24, .14), (.36, .24, .14)]):
        s.part(f"AccentWheel{i}", wheel(c, .14, .1))
    body = []
    body.append(s.rbox((.62, .74, .2), (0, 0, .27), r=.035, seg=2))                                 # chassis
    torso = s.loft([(.34, .27, .24, 3.6, 0, -.04), (.62, .28, .23, 3.8, 0, -.05), (.86, .23, .19, 3.4, 0, -.05)],
                   seg=28, round_caps=.04)
    s.groove(torso, "Z", .55, .012, .007)
    body.append(torso)
    body.append(s.rbox((.64, .07, .13), (0, -.4, .24), r=.025, seg=2))                              # bumper
    head = s.loft([(.84, .14, .13, 2.6, 0, -.08), (.96, .19, .17, 2.6, 0, -.09), (1.1, .18, .16, 2.4, 0, -.09),
                   (1.17, .13, .12, 2.4, 0, -.085), (1.21, .0, .0, 2.0, 0, -.08)], seg=24)                                            # hooded head
    body.append(head)
    for sx in (-1, 1):                                                                              # tank saddle
        body.append(s.rbox((.1, .12, .5), (sx * .25, .24, .62), r=.02, seg=2))
    body.append(s.rbox((.56, .1, .08), (0, .24, .9), r=.02, seg=2))                                 # boom yoke
    s.part("Body", body)

    acc = []
    for k in range(7):                                                                              # hazard stripes
        acc.append(s.rbox((.04, .02, .1), (-.27 + k * .09, -.436, .24), r=.006, seg=1, rot=(0, R(35), 0)))
    for k in range(4):
        acc.append(s.rbox((.05, .03, .12), (-.15 + k * .1, -.27, .66), r=.006, seg=1, rot=(0, R(35), 0)))
    # gas mask: a snout with a round filter, two filter cans, a rim round the lens
    acc.append(s.lathe([(.1, 0), (.105, .03), (.085, .09), (.075, .12)], seg=20, axis="-Y", loc=(0, -.2, .9)))
    acc += s.mirror_x(align(s.rcyl(.055, .11, seg=16, r=.008), (.08, -.25, .92), (.19, -.29, .83)))
    acc.append(s.lathe([(.075, 0), (.075, .02), (.06, .035), (.0, .036)], seg=24, axis="-Y", loc=(0, -.22, 1.04)))
    # tanks: tall cylinders with domed tops, two hazard bands each, a valve on top
    for sx in (-1, 1):
        x = sx * .16
        acc.append(s.lathe([(0, .3), (.1, .3), (.115, .33), (.115, 1.18), (.1, 1.26), (.05, 1.3), (0, 1.3)],
                           seg=24, loc=(x, .26, 0)))
        acc.append(s.rcyl(.025, .06, (x, .26, 1.32), seg=10, r=.006))
        acc.append(s.rcyl(.04, .015, (x, .26, 1.355), seg=10, r=.004))
    s.part("Accent", acc)
    bands = []
    for sx in (-1, 1):
        for z in (.55, 1.05):
            bands.append(s.rcyl(.122, .06, (sx * .16, .26, z), seg=24, r=.008))
    s.part("BodyBands", bands)

    booms = []
    for sx in (-1, 1):
        pts = [(sx * .22, .24, .92), (sx * .38, .2, 1.02), (sx * .44, .05, 1.2), (sx * .42, -.12, 1.24)]
        booms.append(s.tube(pts, .032, res=1))
        booms.append(s.ball(.04, (sx * .22, .24, .92), seg=12))
        # nozzle head: a block and a wide flared spray cone pointing forward, out and down
        n0 = Vector((sx * .42, -.12, 1.24))
        booms.append(s.rbox((.09, .11, .09), tuple(n0), r=.018, seg=2))
        tip_dir = Vector((sx * .45, -1, -.45)).normalized()
        L = .15
        booms.append(align(s.lathe([(.026, -L / 2), (.026, .0), (.065, L / 2 - .006), (.058, L / 2), (0, L / 2)], seg=14),
                           n0, n0 + tip_dir * L))
        # hose from the tank valve to the boom root
        booms.append(s.tube([(sx * .16, .26, 1.34), (sx * .19, .32, 1.36), (sx * .24, .32, 1.05), (sx * .23, .25, .95)],
                            .015, res=1))
    booms.append(s.tube([(0, .24, .94), (0, .26, 1.15), (0, .24, 1.31)], .022, res=1))              # up nozzle
    booms.append(s.lathe([(.018, 0), (.018, .025), (.045, .06), (.0, .065)], seg=12, loc=(0, .24, 1.3)))
    s.part("BodyBooms", booms)

    s.part("Eye", [s.lathe([(.055, 0), (.055, .008), (.04, .022), (0, .028)], seg=20, axis="-Y", loc=(0, -.245, 1.04)),
                   s.ball(.02, (0, .24, 1.37), seg=10)])
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


ROBOTS = {"mimic": mimic, "salesman": salesman, "gunner": gunner, "driller": driller, "exterminator": exterminator}


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = args[0] if args else os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Game", "Art", "Models"))
    names = args[1:] or list(ROBOTS)
    for n in names:
        s.clear_scene()
        objs = ROBOTS[n]()
        export(root, "robot_" + n, objs)


if __name__ == "__main__":
    main()
