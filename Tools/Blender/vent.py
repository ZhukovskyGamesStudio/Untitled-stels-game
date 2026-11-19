"""The vents through the walls (prefabs Level/VentFrame and HighVentFrame), modelled — they were raw cubes:

- vent_duct: the sheet-metal lining of the hole (1.0 x 1.5 m clear, through a 0.3 m wall) with folded seams along its
  corners, and a flange on both faces of the wall with screws round it;
- vent_grille: the grille (0.96 x 1.46 m): a bevelled frame, seven louvres, a screw in each corner, a pull tab.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/vent.py -- Assets/_Game/Art/Models

Both along X (the wall's length), the duct's floor at z = 0 (the builder lifts it to the sill), the wall's thickness
along Y; the grille's foot at z = 0, its face towards -Y. One part each, Body (the builder paints it metal).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
W, H, T = 1.0, 1.5, 0.3            # clear width, height, the wall's thickness (LevelMetrics)
SHEET, FLANGE = 0.02, 0.07


def export(name):
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=False, name=name)
    s.export_fbx(os.path.join(ROOT, "Props", name + ".fbx"), objs)
    print("exported", name)
    s.clear_scene()


def duct():
    d = T + 0.02
    parts = [s.rbox((W, d, SHEET), (0, 0, SHEET / 2), r=0.004, seg=1), s.rbox((W, d, SHEET), (0, 0, H - SHEET / 2), r=0.004, seg=1)]
    parts += [s.rbox((SHEET, d, H - 2 * SHEET + 0.004), (x * (W / 2 - SHEET / 2), 0, H / 2), r=0.004, seg=1) for x in (-1, 1)]
    # folded seams along the duct's four inner corners
    for x in (-1, 1):
        for z in (SHEET + 0.006, H - SHEET - 0.006):
            parts.append(s.rbox((0.014, d - 0.004, 0.014), (x * (W / 2 - SHEET - 0.005), 0, z), r=0.004, seg=1))
    # the flanges on both faces, a picture frame round the hole, with screws
    for y in (-(T / 2 + 0.015), T / 2 + 0.015):
        parts.append(s.rbox((W + 2 * FLANGE, 0.03, FLANGE), (0, y, H + FLANGE / 2), r=0.008, seg=2))
        parts.append(s.rbox((W + 2 * FLANGE, 0.03, FLANGE), (0, y, -FLANGE / 2), r=0.008, seg=2))
        parts += [s.rbox((FLANGE, 0.03, H + 0.002), (x * (W / 2 + FLANGE / 2), y, H / 2), r=0.008, seg=2) for x in (-1, 1)]
        face = y + math.copysign(0.016, y)
        for k in range(5):
            u = -W / 2 + k * W / 4
            for z in (H + FLANGE / 2, -FLANGE / 2):
                parts.append(s.rcyl(0.009, 0.006, (u, face, z), axis="Y", seg=8, r=0.002))
        for z in (H * 0.25, H * 0.5, H * 0.75):
            for x in (-1, 1):
                parts.append(s.rcyl(0.009, 0.006, (x * (W / 2 + FLANGE / 2), face, z), axis="Y", seg=8, r=0.002))
    s.part("Body", parts)
    export("vent_duct")


def grille():
    gw, gh, b = W - 2 * SHEET, H - 2 * SHEET, 0.05
    parts = [s.rbox((gw, 0.025, b), (0, 0, gh - b / 2), r=0.008, seg=2), s.rbox((gw, 0.025, b), (0, 0, b / 2), r=0.008, seg=2)]
    parts += [s.rbox((b, 0.025, gh - 2 * b + 0.004), (x * (gw / 2 - b / 2), 0, gh / 2), r=0.008, seg=2) for x in (-1, 1)]
    for i in range(1, 8):
        slat = s.rbox((gw - 2 * b + 0.01, 0.06, 0.012), (0, 0, 0), r=0.004, seg=1)
        s.place(slat, rot=(math.radians(35), 0, 0))
        s.place(slat, (0, 0, gh * i / 8))
        parts.append(slat)
    for x in (-1, 1):                                              # a screw in each corner of the frame
        for z in (b / 2, gh - b / 2):
            parts.append(s.rcyl(0.01, 0.008, (x * (gw / 2 - b / 2), -0.014, z), axis="Y", seg=10, r=0.002))
    parts.append(s.rbox((0.08, 0.02, 0.03), (0, -0.02, b / 2), r=0.008, seg=1))       # the pull tab at its foot
    s.part("Body", parts)
    export("vent_grille")


CELL, BOARD = 1.0, 0.05           # a ceiling grate's size (VentPlan.Res) and the ceiling board it sits in


def ceiling_frame():
    """vent_grate_frame: the frame of a grate in the ceiling (its top at the board's top, z = BOARD): a bevelled picture
    frame 6 cm wide with a screw in each corner, seen from the room below."""
    f = 0.06
    parts = [s.rbox((CELL, f, BOARD), (0, y * (CELL / 2 - f / 2), BOARD / 2), r=0.006, seg=2) for y in (-1, 1)]
    parts += [s.rbox((f, CELL - 2 * f + 0.004, BOARD), (x * (CELL / 2 - f / 2), 0, BOARD / 2), r=0.006, seg=2) for x in (-1, 1)]
    for x in (-1, 1):
        for y in (-1, 1):
            parts.append(s.rcyl(0.009, 0.006, (x * (CELL / 2 - f / 2), y * (CELL / 2 - f / 2), -0.002), seg=8, r=0.002))
    s.part("Body", parts)
    export("vent_grate_frame")


def ceiling_lid():
    """vent_grate_lid: the grate that swings down (its hinge line on the origin along X, the grate running along -Y =
    Unity +Z): an edge band, nine louvres tilted between two rails."""
    n = CELL - 0.12
    parts = [s.rbox((n, 0.03, 0.025), (0, -0.015, 0), r=0.006, seg=1), s.rbox((n, 0.03, 0.025), (0, -(n - 0.015), 0), r=0.006, seg=1)]
    parts += [s.rbox((0.03, n, 0.025), (x * (n / 2 - 0.015), -n / 2, 0), r=0.006, seg=1) for x in (-1, 1)]
    for i in range(9):
        slat = s.rbox((n - 0.05, 0.04, 0.012), (0, 0, 0), r=0.004, seg=1)
        s.place(slat, rot=(math.radians(30), 0, 0))
        s.place(slat, (0, -(0.06 + i * (n - 0.12) / 8), 0))
        parts.append(slat)
    s.part("Body", parts)
    export("vent_grate_lid")


if __name__ == "__main__":
    s.clear_scene()
    duct()
    grille()
    ceiling_frame()
    ceiling_lid()
    print("DONE")
