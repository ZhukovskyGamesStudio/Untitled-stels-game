"""A chest-high cube shelf (PropKind.LowShelf): what stands under a ceiling grate in a room where filing cabinets or crates
would not belong — a home, a lounge, a ward, a locker room. Its whole top carries a player: 0.9 x 0.46 m, 1.36 m high.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/low_shelf.py -- Assets/_Game/Art/Models

Two columns of three open cubes on a plinth: baskets, folded towels, books, a vase. Front to Blender -Y (Unity +Z), origin on
the floor in the middle of the footprint. Parts: Body (the wood), Accent (baskets, boxes), Trim (books, towels, the vase).
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
W, D, H = 0.90, 0.46, 1.36
PLINTH, SIDE, TOP, BOARD, BACK = 0.07, 0.034, 0.042, 0.026, 0.014


def build():
    import bpy
    rnd = random.Random(16)
    s.clear_scene()
    body = [s.rbox((W, D, TOP), (0, 0, H - TOP / 2), r=0.008, seg=3)]
    for x in (-1, 1):
        body.append(s.rbox((SIDE, D - 0.012, H - TOP + 0.006), (x * (W / 2 - SIDE / 2 - 0.004), 0.002, (H - TOP) / 2 + 0.003), r=0.005, seg=2))
    body.append(s.rbox((W - 2 * SIDE, D - 0.05, PLINTH), (0, 0.012, PLINTH / 2), r=0.004, seg=1))
    body.append(s.rbox((W - 0.05, BACK, H - TOP), (0, D / 2 - 0.016, (H - TOP) / 2 + 0.002), r=0.003, seg=1))
    inner0, inner1 = PLINTH, H - TOP
    step = (inner1 - inner0) / 3
    for k in range(3):
        body.append(s.rbox((W - 2 * SIDE + 0.004, D - 0.03, BOARD), (0, 0.004, inner0 + k * step + (BOARD / 2 if k == 0 else 0)), r=0.004, seg=2))
    body.append(s.rbox((BOARD, D - 0.03, inner1 - inner0), (0, 0.004, (inner0 + inner1) / 2), r=0.004, seg=2))
    s.part("Body", body)

    cell = (W - 2 * SIDE - BOARD) / 2
    cx = (BOARD + cell) / 2 + 0.002
    floor = [inner0 + BOARD - 0.001, inner0 + step + BOARD / 2 - 0.001, inner0 + 2 * step + BOARD / 2 - 0.001]
    accent, trim = [], []

    def basket(x, z, h):
        w, d = cell - 0.036, D - 0.11
        b = s.rbox((w, d, h), (x, -0.012, z + h / 2), r=0.018, seg=3, taper=(0.96, 0.96))
        accent.append(b)
        accent.append(s.rbox((w + 0.012, d + 0.012, 0.024), (x, -0.012, z + h - 0.006), r=0.010, seg=2))
        trim.append(s.rbox((0.085, 0.008, 0.026), (x, -0.012 - d / 2 - 0.004, z + h * 0.62), r=0.006, seg=2))        # its label

    def books(x, z):
        u = x - cell / 2 + 0.018
        while u < x + cell / 2 - 0.10:
            t, h = rnd.uniform(0.022, 0.044), rnd.uniform(0.21, 0.29)
            (trim if rnd.random() < 0.6 else accent).append(s.rbox((t, 0.19, h), (u + t / 2, -0.06, z + h / 2 - 0.001), r=0.004, seg=1))
            u += t + 0.0015
        lean = s.rbox((0.03, 0.19, 0.235), (u + 0.045, -0.06, z + 0.118), r=0.004, seg=1, rot=(0, math.radians(-13), 0))
        accent.append(lean)

    def towels(x, z):
        for k in range(4):
            t = 0.062
            trim.append(s.rbox((cell - 0.10 - 0.012 * (k % 2), 0.25, t), (x + 0.006 * (k % 2), -0.04, z + t * 0.93 * k + t / 2), r=0.028, seg=4))

    def vase(x, z):
        trim.append(s.lathe([(0.0, 0.0), (0.042, 0.0), (0.062, 0.05), (0.068, 0.11), (0.045, 0.18), (0.028, 0.215), (0.036, 0.24), (0.030, 0.24), (0.0, 0.20)],
                            seg=20, loc=(x - 0.06, -0.05, z)))
        accent.append(s.rbox((0.15, 0.20, 0.035), (x + 0.09, -0.04, z + 0.017), r=0.006, seg=2))
        accent.append(s.rbox((0.13, 0.18, 0.028), (x + 0.085, -0.045, z + 0.047), r=0.006, seg=2, rot=(0, 0, 0.16)))

    basket(-cx, floor[0], 0.27)
    basket(cx, floor[0], 0.24)
    books(-cx, floor[1])
    towels(cx, floor[1])
    vase(-cx, floor[2])
    basket(cx, floor[2], 0.25)
    s.part("Accent", accent)
    s.part("Trim", trim)
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name="low_shelf")
    s.export_fbx(os.path.join(ROOT, "Props", "low_shelf.fbx"), objs)
    print("exported low_shelf")


build()
