"""The worker's lift (prefab Robots/RobotWorker): two arms hinged low on the body's sides carrying a level tray.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/worker_lift.py -- Assets/_Game/Art/Models

Writes Robots/worker_lift.fbx in the rest pose, in the robot's coordinates (it is placed at the robot's origin):
  AccentLiftArm  both arms with their hinge stubs and end pins: the builder hangs them on a pivot at the hinge
  BodyLiftTray   the tray (a ribbed platform with a back lip, side rails and pin bosses): a pivot at the arms' end pin
At rest the tray lies over the crate on top; the arm swings forward and down until the tray lies on the floor in
front of the robot (CargoMotion.Reach), the tray staying level. The numbers here are CargoMotion's (keep them equal):
hinge 0.562 m up and 0.15 m forward, arm 0.843 m, pin at 1.405 m at rest.
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
ARM_X = 0.40             # the arms run just outside the wheels (they end at 0.364)
TRAY_W, TRAY_D, TRAY_T = 0.72, 0.40, 0.03


def arm():
    parts = []
    for side in (-1, 1):
        x = side * ARM_X
        parts.append(s.rbox((0.04, 0.055, PIN - HINGE), (x, FWD, (HINGE + PIN) / 2), r=0.012, seg=2))
        parts.append(s.rcyl(0.036, 0.05, (x, FWD, HINGE), axis="X", seg=20))             # the knuckle at the hinge
        parts.append(s.rcyl(0.026, 0.12, (side * 0.355, FWD, HINGE), axis="X", seg=16))   # the axle into the body
        parts.append(s.rcyl(0.03, 0.05, (x, FWD, PIN), axis="X", seg=20))                # the knuckle at the pin
        # a gusset near the hinge: the arm reads as a strong lever, not a stick
        parts.append(s.rbox((0.03, 0.09, 0.16), (x, FWD, HINGE + 0.1), r=0.01, seg=2))
    s.part("AccentLiftArm", parts)


def tray():
    top = PIN + TRAY_T / 2
    parts = [s.rbox((TRAY_W, TRAY_D, TRAY_T), (0, FWD, PIN), r=0.008, seg=2)]
    back = FWD + TRAY_D / 2 - 0.0125                                                      # the robot's side
    parts.append(s.rbox((TRAY_W, 0.025, 0.08), (0, back, top + 0.04 - 0.005), r=0.008, seg=2))
    for side in (-1, 1):
        parts.append(s.rbox((0.025, TRAY_D, 0.045), (side * (TRAY_W / 2 - 0.0125), FWD, top + 0.02), r=0.008, seg=2))
        parts.append(s.rcyl(0.028, 0.045, (side * (TRAY_W / 2 + 0.02), FWD, PIN), axis="X", seg=16))  # pin boss
    for i in range(4):                                                                    # grip ribs
        parts.append(s.rbox((TRAY_W - 0.08, 0.018, 0.008), (0, FWD - 0.14 + i * 0.085, top + 0.004), r=0.003, seg=1))
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
