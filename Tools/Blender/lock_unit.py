"""The electronic lock the robots put on doors (AutoDoor.LockUnits; AssetBuilder.LockUnits hangs it on the leaf): a steel box
0.16 x 0.30 m bolted on the leaf, a status window along its top (Eye: red while it holds the door, green once hacked), cooling
ribs, an opened service port with a board in it and a looped cable, the bolt's housing on its right side reaching the seam.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/lock_unit.py -- Assets/_Game/Art/Models

Facing -Y (Unity +Z), its back on y = 0, its middle at the origin. Parts: Body (steel), Accent (cable, bolt, board), Eye (window).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
W, H, D = 0.16, 0.30, 0.05


def build():
    import bpy
    s.clear_scene()
    body = []
    # the mounting plate with a bolt in each corner, the box on it
    body.append(s.rbox((W + 0.04, 0.012, H + 0.04), (0, -0.006, 0), r=0.008, seg=2))
    for x in (-1, 1):
        for z in (-1, 1):
            body.append(s.rcyl(0.009, 0.01, (x * (W / 2 + 0.008), -0.014, z * (H / 2 + 0.008)), axis="Y", seg=6, r=0.002))
    case = s.rbox((W, D, H), (0, -0.010 - D / 2, 0), r=0.012, seg=3)
    front = -0.010 - D
    # the window's recess and the service port's, cut into the front
    case = s.boolean(case, s.rbox((0.116, 0.02, 0.05), (0, front, 0.098), r=0.006, seg=2), "DIFFERENCE")
    case = s.boolean(case, s.rbox((0.082, 0.04, 0.078), (-0.022, front, -0.078), r=0.004, seg=1), "DIFFERENCE")
    s.bevel_edges(case, 0.0015, 1, angle=50)
    body.append(case)
    # cooling ribs between the window and the port
    for k in range(5):
        body.append(s.rbox((0.118, 0.014, 0.007), (0, front - 0.003, 0.054 - k * 0.0165), r=0.0025, seg=1))
    # the bolt's housing on the seam's side (+X in Blender; the builder turns the unit so it meets the seam)
    body.append(s.rbox((0.05, 0.036, 0.07), (W / 2 + 0.015, -0.028, 0.0), r=0.008, seg=2))
    # the window's hood
    body.append(s.rbox((0.126, 0.022, 0.012), (0, front - 0.004, 0.128), r=0.004, seg=2))
    s.part("Body", body)

    accent = []
    accent.append(s.rcyl(0.014, 0.05, (W / 2 + 0.05, -0.028, 0.0), axis="X", seg=14, r=0.003))                 # the bolt
    accent.append(s.rbox((0.076, 0.006, 0.072), (-0.022, front + 0.017, -0.078), r=0.001, seg=1))             # the board
    for x, z, w, h in ((-0.04, -0.060, 0.018, 0.014), (-0.012, -0.066, 0.022, 0.010), (-0.034, -0.092, 0.026, 0.016), (-0.004, -0.094, 0.010, 0.020)):
        accent.append(s.rbox((w, 0.012, h), (x, front + 0.011, z), r=0.001, seg=1))                            # its chips
    # a cable looped out of the port's side and back into the box's foot
    accent.append(s.tube([(0.014, front + 0.012, -0.070), (0.036, front - 0.030, -0.066), (0.060, front - 0.034, -0.104),
                          (0.050, front - 0.016, -0.140), (0.040, front + 0.022, -0.158)], 0.0055, res=3))
    accent.append(s.rcyl(0.010, 0.02, (0.040, front + 0.022, -0.152), axis="Z", seg=12, r=0.002))              # its gland
    # a conduit off the top, flat along the leaf
    accent.append(s.tube([(-0.045, -0.03, H / 2 - 0.01), (-0.045, -0.03, H / 2 + 0.05), (-0.075, -0.02, H / 2 + 0.085), (-0.12, -0.008, H / 2 + 0.09), (-0.145, 0.008, H / 2 + 0.09)], 0.011, res=3))
    s.part("Accent", accent)

    s.part("Eye", [s.rbox((0.110, 0.010, 0.044), (0, front + 0.006, 0.098), r=0.004, seg=2)])
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=False, name="lock_unit")
    s.export_fbx(os.path.join(ROOT, "Props", "lock_unit.fbx"), objs)
    print("exported lock_unit")


build()
