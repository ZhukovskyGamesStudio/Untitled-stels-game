"""The workbench built into the front of the pickup's cargo bed (car.py), against the cab wall: a low bench with a
butcher-block top, a shelf and a two-drawer unit under it, a vise on the left front corner, a pegboard rising above the
bed's front wall with tools on hooks, and a clamp lamp. The rest of the bed stays free for cargo.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/car_bench.py -- Assets/_Game/Art/Models
      (writes <root>/Car/car_workbench.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/car_bench.py -- <scratch> --review <sheet.png>
      (the car and the bench together on one sheet, read from <scratch>/Car and Assets/_Game/Art/Models/Car/pickup.fbx)

Same frame as car.py, so the model drops in as a child of the car at its origin: metres, Blender front -Y = Unity +Z,
z up, origin on the ground in the middle of the wheelbase (the Unity import mirrors Blender X). The bed (car.py): floor
top z 0.85, inside x -0.85..0.85, the front wall's inner face at y 0.35, walls to z 1.30.
  bench top: x -0.75..0.75, y 0.36..0.81, top surface z 1.20 (0.35 above the bed floor); vise to y 0.91
  pegboard: y 0.363..0.381, z 1.195..1.56, posts and rail to z 1.59; lamp to z ~1.70
Parts: Body (painted steel frame, drawers, vise body), Accent (wooden top), AccentBoard (pegboard), Trim (steel: vise
jaws and screw, tools, hooks, drawer pulls), TrimHandle (red tool grips), EyeLamp (the lamp's bulb).
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

FLOOR = 0.85               # bed floor top (car.BED_FLOOR_Z)
WALL_Y = 0.35              # inner face of the bed's front wall
TOP = 1.20                 # bench top surface
X0, X1 = -0.75, 0.75
Y0, Y1 = 0.36, 0.81
BOARD_Y = 0.372            # pegboard centre plane
BOARD_TOP = 1.56


def frame():
    parts = []
    for x in (X0 + 0.03, X1 - 0.03):
        for y in (Y0 + 0.025, Y1 - 0.03):
            parts.append(s.rbox((0.04, 0.04, TOP - FLOOR - 0.03), (x, y, (FLOOR + TOP - 0.03) / 2 + 0.002), r=0.006, seg=1))
            parts.append(s.rbox((0.06, 0.06, 0.012), (x, y, FLOOR + 0.006), r=0.003, seg=1))            # foot pad
        parts.append(s.rbox((0.03, Y1 - Y0 - 0.04, 0.03), (x, (Y0 + Y1) / 2 - 0.002, FLOOR + 0.075), r=0.005, seg=1))
        parts.append(s.rbox((0.03, Y1 - Y0 - 0.04, 0.035), (x, (Y0 + Y1) / 2 - 0.002, TOP - 0.06), r=0.005, seg=1))
    parts.append(s.rbox((X1 - X0 - 0.06, 0.03, 0.035), (0, Y1 - 0.03, TOP - 0.06), r=0.005, seg=1))     # front apron
    parts.append(s.rbox((X1 - X0 - 0.07, Y1 - Y0 - 0.07, 0.018), (0, (Y0 + Y1) / 2 - 0.002, FLOOR + 0.095),
                        r=0.004, seg=1))                                                                # the shelf
    # the drawer unit on the right, standing on the shelf
    parts.append(s.rbox((0.44, Y1 - Y0 - 0.06, TOP - 0.05 - (FLOOR + 0.1)), (0.47, (Y0 + Y1) / 2 - 0.002,
                        (FLOOR + 0.1 + TOP - 0.05) / 2), r=0.006, seg=1))
    for z in (0.99, 1.085):
        parts.append(s.rbox((0.41, 0.02, 0.085), (0.47, Y1 - 0.03, z), r=0.004, seg=1))                  # drawer fronts
    # pegboard posts and the top rail
    for x in (X0 + 0.02, X1 - 0.02):
        parts.append(s.rbox((0.04, 0.035, BOARD_TOP + 0.03 - (TOP - 0.04)), (x, BOARD_Y, (BOARD_TOP + 0.03 + TOP - 0.04) / 2),
                            r=0.006, seg=1))
    parts.append(s.rbox((X1 - X0 - 0.02, 0.035, 0.035), (0, BOARD_Y, BOARD_TOP + 0.012), r=0.006, seg=1))
    return parts


def vise_body():
    x = -0.5
    base = s.rbox((0.13, 0.13, 0.03), (x, 0.73, TOP + 0.013), r=0.006, seg=2)
    fixed = s.rbox((0.15, 0.045, 0.075), (x, 0.725, TOP + 0.06), r=0.008, seg=2)
    moving = s.rbox((0.15, 0.045, 0.075), (x, 0.835, TOP + 0.06), r=0.008, seg=2)
    slide = s.rbox((0.07, 0.15, 0.04), (x, 0.79, TOP + 0.04), r=0.006, seg=1)
    return [base, fixed, moving, slide]


def vise_steel():
    x = -0.5
    parts = [s.rbox((0.15, 0.012, 0.03), (x, 0.752, TOP + 0.085), r=0.002, seg=1),        # jaw plates
             s.rbox((0.15, 0.012, 0.03), (x, 0.808, TOP + 0.085), r=0.002, seg=1),
             s.rcyl(0.011, 0.14, (x, 0.88, TOP + 0.045), axis="Y", seg=14, r=0.002),     # screw
             s.rcyl(0.007, 0.2, (x, 0.945, TOP + 0.045), axis="X", seg=10, r=0.002),     # tommy bar
             s.rcyl(0.018, 0.02, (x, 0.945, TOP + 0.045), axis="Y", seg=14, r=0.003)]    # its hub
    parts += [s.ball(0.013, (x + sx * 0.1, 0.945, TOP + 0.045), seg=12) for sx in (-1, 1)]
    return parts


def board():
    b = s.rbox((X1 - X0 - 0.06, 0.018, BOARD_TOP - TOP + 0.005), (0, BOARD_Y, (BOARD_TOP + TOP - 0.005) / 2),
               r=0.004, seg=1)
    return [b]


def hook(x, z):
    """A pegboard hook: a bent wire out of the board and up."""
    yb = BOARD_Y + 0.006
    return s.tube([(x, yb, z), (x, yb + 0.035, z), (x, yb + 0.045, z + 0.012)], 0.0035, res=1, segments_per=3)


def tools():
    steel, grips = [], []
    yb = BOARD_Y + 0.009 + 0.012                    # tools hang 1-2 cm in front of the board
    # claw hammer, head up
    x = -0.55
    grips.append(s.rbox((0.03, 0.022, 0.2), (x, yb + 0.03, 1.33), r=0.009, seg=2))
    steel.append(s.rbox((0.13, 0.026, 0.03), (x + 0.015, yb + 0.03, 1.445), r=0.006, seg=2))
    steel.append(hook(x - 0.03, 1.43))
    steel.append(hook(x + 0.04, 1.43))
    # two spanners
    for k, (x, ln) in enumerate(((-0.36, 0.2), (-0.29, 0.17))):
        zc = 1.43 - ln / 2
        steel.append(s.rbox((0.018, 0.007, ln), (x, yb + 0.004, zc), r=0.003, seg=1))
        steel.append(s.rcyl(0.02, 0.008, (x, yb + 0.004, zc + ln / 2), axis="Y", seg=14, r=0.002))
        steel.append(s.rcyl(0.016, 0.008, (x, yb + 0.004, zc - ln / 2), axis="Y", seg=14, r=0.002))
        steel.append(hook(x, zc + ln / 2 + 0.03))
    # screwdrivers, tip down
    for x, c in ((-0.18, 0), (-0.13, 1), (-0.08, 2)):
        grips.append(s.rcyl(0.014, 0.09, (x, yb + 0.012, 1.43), seg=12, r=0.004))
        steel.append(s.rcyl(0.004, 0.11 - 0.015 * c, (x, yb + 0.012, 1.43 - 0.045 - (0.11 - 0.015 * c) / 2 + 0.003),
                            seg=8, r=0.001))
    steel.append(s.rbox((0.15, 0.03, 0.012), (-0.13, BOARD_Y + 0.02, 1.48), r=0.003, seg=1))   # their rack
    # pliers: two handles crossing at the pivot
    x = 0.05
    for sgn in (-1, 1):
        grips.append(s.rbox((0.016, 0.01, 0.12), (x + sgn * 0.022, yb + 0.004, 1.3), r=0.005, seg=1,
                            rot=(0, sgn * math.radians(10), 0)))
        steel.append(s.rbox((0.014, 0.009, 0.08), (x - sgn * 0.004, yb + 0.004, 1.39), r=0.003, seg=1,
                            rot=(0, -sgn * math.radians(8), 0)))
    steel.append(s.rcyl(0.01, 0.014, (x, yb + 0.004, 1.37), axis="Y", seg=12, r=0.002))
    steel.append(hook(x, 1.45))
    # hand saw hanging by its grip
    x = 0.3
    grips.append(s.rbox((0.1, 0.022, 0.07), (x, yb + 0.008, 1.47), r=0.012, seg=2))
    steel.append(s.rbox((0.075, 0.004, 0.24), (x - 0.01, yb + 0.008, 1.32), r=0.0015, seg=1, taper=(0.7, 1.0)))
    steel.append(hook(x, 1.52))
    # drawer pulls
    for z in (0.99, 1.085):
        steel.append(s.rbox((0.12, 0.022, 0.016), (0.47, Y1 - 0.012, z), r=0.006, seg=1))
    return steel, grips


def lamp():
    body, bulb = [], []
    a = Vector((0.6, BOARD_Y + 0.012, BOARD_TOP + 0.012))
    b = Vector((0.56, 0.55, 1.68))
    body.append(s.rbox((0.05, 0.05, 0.05), tuple(a), r=0.008, seg=1))                         # clamp
    body.append(s.tube([tuple(a), (0.59, 0.42, 1.66), tuple(b)], 0.008, res=1, segments_per=4))
    shade = s.lathe([(0.012, 0.0), (0.02, 0.012), (0.045, 0.05), (0.052, 0.062), (0.0, 0.062)], seg=20)
    s.place(shade, (0, 0, -0.012))
    tilt = (math.radians(-140), 0, 0)               # the shade's mouth points down and towards the bench front
    s.place(shade, tuple(b), tilt)
    body.append(shade)
    mouth = Vector((0, 0, 0.05))
    mouth.rotate(__import__("mathutils").Euler(tilt, "XYZ"))
    bulb.append(s.ball(0.03, tuple(b + mouth * 0.8), seg=14))
    return body, bulb


def build():
    s.clear_scene()
    steel, grips = tools()
    lamp_body, bulb = lamp()
    objs = [s.part("Body", frame() + vise_body() + lamp_body),
            s.part("Accent", [s.rbox((X1 - X0, Y1 - Y0, 0.045), (0, (Y0 + Y1) / 2, TOP - 0.0225), r=0.006, seg=2)]),
            s.part("AccentBoard", board()),
            s.part("Trim", steel + vise_steel()),
            s.part("TrimHandle", grips),
            s.part("EyeLamp", bulb)]
    return objs


def review(scratch, sheet_path):
    import review as rv
    s.clear_scene()
    car = os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models", "Car", "pickup.fbx")
    bpy.ops.import_scene.fbx(filepath=car)
    bpy.ops.import_scene.fbx(filepath=os.path.join(scratch, "Car", "car_workbench.fbx"))
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    rv.sheet([objs], sheet_path, tile=500, focus=(0.0, 0.6, 1.3, 0.8))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = args[0] if args else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
    if "--review" in args:
        review(root, args[args.index("--review") + 1])
    else:
        objs = build()
        bpy.context.view_layer.update()
        meshfix.fix_objects(objs, floor=False, name="car_workbench")
        pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
        lo = [min(p[k] for p in pts) for k in range(3)]
        hi = [max(p[k] for p in pts) for k in range(3)]
        print("BENCH blender lo", [round(v, 3) for v in lo], "hi", [round(v, 3) for v in hi])
        s.export_fbx(os.path.join(root, "Car", "car_workbench.fbx"), objs)
