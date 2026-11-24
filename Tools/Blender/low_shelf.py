"""A chest-high cube shelf (PropKind.LowShelf): what stands under a ceiling grate in a room where filing cabinets or crates
would not belong — a home, a lounge, a ward, a locker room. Its whole top carries a player: 0.9 x 0.46 m, 1.36 m high.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/low_shelf.py -- Assets/_Game/Art/Models

Two columns of three open cubes on a plinth (low_shelf: the wood alone) and what may stand in a cube (cube_box_0/1, cube_towels, cube_vase,
cube_basket; the books are variants.py's): AssetBuilder.Variants.CubeThings. Front to Blender -Y (Unity +Z), origin on the floor in
the middle of the footprint.
"""
import os
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

    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name="low_shelf")
    s.export_fbx(os.path.join(ROOT, "Props", "low_shelf.fbx"), objs)
    print("exported low_shelf")


# what stands in a cube (origin: the middle of its floor's front edge, going back to +Y; AssetBuilder.Variants puts it there)
CELL, DEEP = (W - 2 * SIDE - BOARD) / 2, D - 0.06


def content(name, fn):
    s.clear_scene()
    fn()
    mc.export(name, floor=False)


def box(label):
    """A lidded archive box with a hand hole and a label."""
    def make():
        w, d, h = CELL - 0.05, DEEP - 0.05, 0.25
        b = s.rbox((w, d, h), (0, 0.03 + d / 2, h / 2), r=0.006, seg=2, taper=(0.985, 0.985))
        b = s.boolean(b, s.rbox((0.09, 0.05, 0.03), (0, 0.03, h * 0.66), r=0.012, seg=3))
        lid = s.rbox((w + 0.012, d + 0.012, 0.055), (0, 0.03 + d / 2, h - 0.012), r=0.006, seg=2)
        s.part("Body", [b, lid])
        mc.plates("Print", [mc.Plate(label, (0, 0.0285, h * 0.34), 0.13, 0.062, thick=0.003, rest="white")],
                  [(s.rbox((0.1, 0.02, 0.04), (0, 0.05, h * 0.66), r=0), "black")])
    return make


def towels():
    """A stack of folded towels: each fold a soft roll at the front, hems at the sides."""
    b = []
    for k in range(4):
        w, t = CELL - 0.11 - 0.014 * (k % 2), 0.058
        z = k * t * 0.95
        b.append(s.loft([(-w / 2, 0.115, t / 2, 3.2), (-w / 2 + 0.01, 0.125, t / 2, 3.0), (w / 2 - 0.01, 0.125, t / 2, 3.0), (w / 2, 0.115, t / 2, 3.2)], seg=24, axis="X",
                        loc=(0.006 * (k % 2), 0.16, z + t / 2)))
        b.append(s.rbox((w - 0.02, 0.22, 0.004), (0.006 * (k % 2), 0.15, z + t / 2), r=0.001, seg=1))
    s.part("Body", b)


def vase():
    """A glazed vase."""
    s.part("Body", [s.lathe([(0.0, 0.0), (0.045, 0.0), (0.05, 0.006), (0.066, 0.05), (0.072, 0.1), (0.06, 0.15), (0.036, 0.19), (0.028, 0.215), (0.034, 0.24), (0.04, 0.246),
                                     (0.034, 0.244), (0.024, 0.215), (0.0, 0.205)], seg=28, loc=(-0.05, 0.16, 0))])


def basket():
    """A woven basket: rows of weave round a tapered body, a rolled rim, two handles."""
    w, d, h = CELL - 0.06, DEEP - 0.08, 0.19
    rows = []
    n = 9
    for k in range(n):
        z = 0.008 + k * (h - 0.016) / (n - 1)
        g = 0.94 + 0.06 * z / h
        rows.append(s.rbox((w * g, d * g, h / n * 1.12), (0, 0.03 + d / 2, z), r=0.009, seg=3))
    rim = s.rbox((w + 0.016, d + 0.016, 0.022), (0, 0.03 + d / 2, h), r=0.01, seg=3)
    body = s.rbox((w * 0.95, d * 0.95, h - 0.01), (0, 0.03 + d / 2, h / 2), r=0.01, seg=2)
    s.part("Body", [body, *rows, rim])
    s.part("Accent", [s.tube([(-0.05, 0.022, h - 0.02), (-0.045, 0.006, h + 0.01), (0.045, 0.006, h + 0.01), (0.05, 0.022, h - 0.02)], 0.006)])


build()
import machines as mc  # noqa: E402

mc.ROOT = ROOT
content("cube_box_0", box("boxlabel"))
content("cube_box_1", box("boxlabel2"))
content("cube_towels", towels)
content("cube_vase", vase)
content("cube_basket", basket)
