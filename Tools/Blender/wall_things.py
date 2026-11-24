"""What hangs on walls: framed prints and photographs, a certificate, a calendar, an evacuation plan, signs, a thermostat, a
return-air grille (the list, sizes and picture cells — wall_things_atlas.py; the game's order — Scripts/Level/WallThings.cs).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/wall_things.py -- Assets/_Game/Art/Models [--only a,b]
      → StyleKit/Things/thing_<name>.fbx

Decor of the style kit (no colliders, merged into the house's decor mesh). The origin is the thing's middle on the wall's
face; it stands out of the wall towards -Y (the game's +Z), x along the wall. Parts: Body (the frame or the casing), Accent
(the mat round a picture, a grille's dark throat), Print (the picture: wall_things.png).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import machines as mc  # noqa: E402
import meshfix  # noqa: E402
import shapes as s  # noqa: E402
import wall_things_atlas as at  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
ONLY = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None


def framed(name, w, h, bar, depth, mat):
    """A picture in a moulded frame: four bars with a stepped profile, a mat of card round the picture, the picture set back."""
    body = [s.rbox((w - 0.01, 0.008, h - 0.01), (0, -0.004, 0), r=0.0, seg=1)]                       # the back board
    for sx in (-1, 1):
        body.append(s.rbox((bar, depth, h), (sx * (w - bar) / 2, -depth / 2, 0), r=0.004, seg=1))
        body.append(s.rbox((bar * 0.45, depth * 0.35, h - bar), (sx * (w / 2 - bar * 0.78), -depth * 1.1, 0), r=0.002, seg=1))
    for sz in (-1, 1):
        body.append(s.rbox((w, depth, bar), (0, -depth / 2, sz * (h - bar) / 2), r=0.004, seg=1))
        body.append(s.rbox((w - bar, depth * 0.35, bar * 0.45), (0, -depth * 1.1, sz * (h / 2 - bar * 0.78)), r=0.002, seg=1))
    s.part("Body", body)
    iw, ih = w - 2 * bar + 0.006, h - 2 * bar + 0.006
    if mat > 0:
        s.part("Accent", [s.rbox((iw, 0.004, ih), (0, -depth * 0.45, 0), r=0.0, seg=1)])
    mc.plates("Print", [mc.Plate(name, (0, -depth * 0.45 - 0.003, 0), iw - 2 * mat, ih - 2 * mat, table=at.CELLS, thick=0.004)])


def clip_frame(name, w, h):
    """A sheet behind a pane in a thin aluminium edge."""
    body = [s.rbox((w, 0.012, h), (0, -0.006, 0), r=0.002, seg=1)]
    s.part("Body", body)
    mc.plates("Print", [mc.Plate(name, (0, -0.0135, 0), w - 0.02, h - 0.02, table=at.CELLS, thick=0.003)])


def plate(name, w, h):
    """An enamelled sign screwed to the wall at its corners."""
    mc.plates("Print", [mc.Plate(name, (0, -0.004, 0), w, h, table=at.CELLS, thick=0.006)])
    s.part("Body", [s.rcyl(0.007, 0.004, (sx * (w / 2 - 0.018), -0.008, sz * (h / 2 - 0.018)), axis="Y", seg=8, r=0.001, bevel_seg=1) for sx in (-1, 1) for sz in (-1, 1)])


def paper(name, w, h):
    """A wall calendar on its nail: the pages, the wire binding along the top."""
    mc.plates("Print", [mc.Plate(name, (0, -0.005, -0.01), w, h - 0.02, table=at.CELLS, thick=0.008)])
    body = [s.rcyl(0.006, w * 0.92, (0, -0.009, h / 2 - 0.018), axis="X", seg=8, r=0.001, bevel_seg=1),
            s.rcyl(0.005, 0.012, (0, -0.006, h / 2 - 0.004), axis="Y", seg=8, r=0.001, bevel_seg=1)]
    s.part("Body", body)


def thermostat(w, h):
    """A room thermostat: a rounded plastic case, its dial, the slots of its sensor."""
    body = [s.rbox((w, 0.026, h), (0, -0.013, 0), r=0.01, seg=3), s.rcyl(0.026, 0.012, (0, -0.03, 0.012), axis="Y", seg=20, r=0.003)]
    s.part("Body", body)
    dark = [s.rbox((w * 0.6, 0.004, 0.004), (0, -0.0265, -h / 2 + 0.014 + k * 0.009), r=0.0, seg=1) for k in range(3)]
    dark.append(s.rbox((0.004, 0.006, 0.016), (0, -0.036, 0.026), r=0.001, seg=1))
    s.part("Accent", dark)


def vent_grille(w, h):
    """A return-air grille low in the wall: a pressed steel frame, slanted louvres over a dark throat, two screws."""
    body = []
    for sx in (-1, 1):
        body.append(s.rbox((0.022, 0.012, h), (sx * (w / 2 - 0.011), -0.006, 0), r=0.003, seg=1))
    for sz in (-1, 1):
        body.append(s.rbox((w, 0.012, 0.022), (0, -0.006, sz * (h / 2 - 0.011)), r=0.003, seg=1))
    n = 9
    for k in range(n):
        z = -h / 2 + 0.03 + k * (h - 0.06) / (n - 1)
        body.append(s.rbox((w - 0.04, 0.014, 0.003), (0, -0.007, z), r=0.0, seg=1, rot=(math.radians(-35), 0, 0)))
    for sx in (-1, 1):
        body.append(s.rcyl(0.005, 0.004, (sx * (w / 2 - 0.011), -0.013, 0), axis="Y", seg=8, r=0.001, bevel_seg=1))
    s.part("Body", body)
    s.part("Accent", [s.rbox((w - 0.03, 0.003, h - 0.03), (0, -0.0015, 0), r=0.0, seg=1)])


def main():
    import bpy
    out = os.path.join(ROOT, "StyleKit", "Things")
    os.makedirs(out, exist_ok=True)
    for name, w, h, make in at.THINGS:
        if ONLY and name not in ONLY:
            continue
        s.clear_scene()
        if make in ("wood", "black", "metal"):
            small = max(w, h) < 0.45
            framed(name, w, h, bar={"wood": 0.04, "black": 0.022, "metal": 0.014}[make] * (0.75 if small else 1), depth=0.024 if make != "wood" else 0.03,
                   mat=0.0 if name.startswith(("landscape", "abstract")) else 0.035)
        elif make == "thin":
            clip_frame(name, w, h)
        elif make == "plate":
            plate(name, w, h)
        elif make == "paper":
            paper(name, w, h)
        elif name == "thermostat":
            thermostat(w, h)
        else:
            vent_grille(w, h)
        bpy.context.view_layer.update()
        objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
        meshfix.fix_objects(objs, floor=False, snap=None, name=name)
        s.export_fbx(os.path.join(out, f"thing_{name}.fbx"), objs)
        print("exported", name)


main()
