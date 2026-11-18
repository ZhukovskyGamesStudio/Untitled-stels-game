"""The worker's lift (prefab Robots/RobotWorker): two booms hinged on the flanks of the sensor pod carrying a level
fork tray.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/worker_lift.py -- Assets/_Game/Art/Models

Writes Robots/worker_lift.fbx in the rest pose, in the robot's coordinates (it is placed at the robot's origin):
  AccentLiftArm  both booms with their shoulder knuckles, axles into the shoulder drums, end knuckles and the cable
                 run along their backs: the builder hangs them on a pivot at the hinge
  BodyLiftTray   the fork tray: a deck with two raised fork tines (what the loot rests on), a chamfered front lip that
                 slides under things, a load backrest on the robot's side, pin bosses: a pivot at the booms' end pin
At rest the tray sits on the dock at the top of the mast (robots.py worker); the booms swing forward and down until
the tray lies on the floor in front of the robot (CargoMotion.Reach), the tray staying level. The numbers here are
CargoMotion's (keep them equal): hinge 0.562 m up and 0.15 m forward, boom 0.843 m, pin at 1.405 m at rest, the tray's
bottom 0.015 under the pin (TrayHalf) and the loot's seat 0.023 over it (OnTray: the tops of the tines).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODELS = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))

# Blender: -Y is the robot's front (Unity +Z), Z is up
FWD = -0.15              # the hinge and the tray centre, 15 cm in front of the robot's centre
HINGE = 0.562            # hinge height
PIN = 1.405              # the arms' end pin at rest (the tray's mid-thickness)
ARM_X = 0.40             # the booms run just outside the deck (it ends at 0.35, the wheels at 0.36)
TRAY_W, TRAY_D = 0.70, 0.40
BOTTOM = PIN - 0.015     # CargoMotion.TrayHalf
SEAT = PIN + 0.023       # CargoMotion.OnTray: the loot rests on the tines' tops


def arm():
    parts = []
    for side in (-1, 1):
        x = side * ARM_X
        # the boom: a flat bar, deep at the shoulder, tapering to the pin (it is the robot's silhouette)
        parts.append(s.loft([(HINGE + 0.02, 0.026, 0.066, 3.2, x, FWD), (HINGE + 0.3, 0.026, 0.055, 3.2, x, FWD),
                             (PIN - 0.03, 0.024, 0.036, 3.0, x, FWD)], seg=16))
        parts.append(s.rcyl(0.058, 0.06, (x, FWD, HINGE), axis="X", seg=24, r=0.008))       # shoulder knuckle
        parts.append(s.rcyl(0.03, 0.03, (x + side * 0.03, FWD, HINGE), axis="X", seg=16))   # its bolt head
        parts.append(s.rcyl(0.026, 0.12, (side * 0.345, FWD, HINGE), axis="X", seg=16))     # the axle into the drum
        parts.append(s.rcyl(0.026, 0.05, (x, FWD, PIN), axis="X", seg=20))                  # the end knuckle (clears the floor)
        # the cable run along the boom's back (the tray's side), clipped on twice
        cx = x + side * 0.004
        parts.append(s.tube([(cx, FWD + 0.07, HINGE + 0.06), (cx, FWD + 0.061, HINGE + 0.3), (cx, FWD + 0.043, PIN - 0.08)],
                            0.011, res=2, segments_per=6))
        for z in (HINGE + 0.22, PIN - 0.2):
            parts.append(s.rbox((0.058, 0.03, 0.03), (x, FWD + 0.052 - (z - HINGE) * 0.016, z), r=0.006, seg=1))
    s.part("AccentLiftArm", parts)


def tray():
    back = FWD + TRAY_D / 2                                                       # the robot's side (+y)
    front = FWD - TRAY_D / 2
    parts = []
    # the deck: its front edge bevelled into a lip so it slides under what lies on the floor
    deck_t = 0.022
    parts.append(s.rbox((TRAY_W - 0.04, TRAY_D - 0.04, deck_t), (0, FWD + 0.02, BOTTOM + deck_t / 2), r=0.006, seg=2))
    parts.append(s.rbox((TRAY_W - 0.04, 0.07, 0.018), (0, front + 0.04, BOTTOM + 0.009), r=0.006, seg=2,
                        taper=(1.0, 0.45)))
    # two chunky fork tines along the deck, their tips sticking out past its front: the loot rests on them (their tops
    # are the seat); from above they are what says "forklift"
    for sx in (-1, 1):
        parts.append(s.rbox((0.12, TRAY_D + 0.03, SEAT - BOTTOM), (sx * 0.17, FWD - 0.02, (SEAT + BOTTOM) / 2),
                            r=0.01, seg=2, taper=(0.86, 1.0)))
    # the load backrest on the robot's side: a solid plate with a rolled top, the loot leans on it (at rest it docks
    # against the cradle's pads on the mast)
    rest_h, rest_t = 0.12, 0.025
    y = back - rest_t / 2
    parts.append(s.rbox((TRAY_W, rest_t + 0.015, 0.03), (0, y - 0.005, BOTTOM + 0.015), r=0.006, seg=2))   # sill
    parts.append(s.rbox((TRAY_W - 0.08, rest_t - 0.006, rest_h - 0.02), (0, y, BOTTOM + rest_h / 2 + 0.005), r=0.006, seg=1))
    parts.append(s.rcyl(0.016, TRAY_W, (0, y, BOTTOM + rest_h), axis="X", seg=10, r=0.004))               # rolled top
    # side rails and the pin bosses the booms hold
    for sx in (-1, 1):
        parts.append(s.rbox((0.025, TRAY_D - 0.03, 0.045), (sx * (TRAY_W / 2 - 0.0125), FWD + 0.005, BOTTOM + 0.0225),
                            r=0.008, seg=2))
        parts.append(s.rcyl(0.025, 0.05, (sx * (TRAY_W / 2 + 0.02), FWD, PIN), axis="X", seg=16))
    s.part("BodyLiftTray", parts)


def main():
    import bpy
    s.clear_scene()
    arm()
    tray()
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=False, name="worker_lift")
    s.export_fbx(os.path.join(MODELS, "Robots", "worker_lift.fbx"), objs)
    s.clear_scene()


main()
