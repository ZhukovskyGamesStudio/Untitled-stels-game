"""The storage room's own pieces: an empty pallet rack (PropKind.StorageRack, the workers shelve loot on it) and the leaf
of the storage room's steel sliding door (prefab Level/StorageDoor: two leaves, PanelA and PanelB).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/storage.py -- Assets/_Game/Art/Models
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor

Writes Props/storage_rack.fbx and Level/storage_leaf.fbx + Level/storage_leaf_b.fbx (UVs shifted, like door_leaf).
Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), z up, origin on the floor in the middle of
the footprint. Object names are material roles: Body, Accent, Trim. Pieces overlap by volume (2-5 mm).

The rack: 2.0 x 0.6 x 2.0, three decks whose tops are the furnisher's shelf levels (0.47, 1.07, 1.67 m) and nothing
on them — 0.55 m of clear height over each deck for loot. Upright frames (Body) with braces, orange step beams
front and back (Accent), steel decks (Trim).
The leaf: 0.9 x 2.28 m, 7 cm deep: a steel plate with a raised frame and rails (Body), a yellow hazard band with dark
chevrons at the bottom and a yellow edge stripe (Accent) on both faces — the one door in the house that looks like this.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODELS = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))


def export(path, shift=None, old="", new=""):
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=os.path.basename(path))
    s.export_fbx(path, objs)
    print("exported", path)
    if shift is not None:
        for o in objs:
            for d in o.data.uv_layers.active.data:
                d.uv = (d.uv[0] + shift[0], d.uv[1] + shift[1])
        b = path.replace(old, new)
        s.export_fbx(b, objs)
        print("exported", b)
    s.clear_scene()


DECKS = (0.47, 1.07, 1.67)   # deck tops: Furnisher.ShelfLevels


def rack():
    s.clear_scene()
    W, D, H = 2.0, 0.6, 2.0
    body = []
    post = 0.07
    for sx in (-1, 1):
        x = sx * (W / 2 - post / 2)
        for sy in (-1, 1):
            body.append(s.rbox((post, 0.05, H), (x, sy * (D / 2 - 0.025), H / 2), r=0.006, seg=1))
            body.append(s.rbox((0.07, 0.1, 0.02), (x, sy * (D / 2 - 0.05), 0.01), r=0.004, seg=1))   # foot plate
        # the frame's braces: horizontals and a zigzag between the front and back posts
        for z in (0.15, 1.9):
            body.append(s.rbox((0.04, D - 0.06, 0.035), (x, 0, z), r=0.004, seg=1))
        zs = [0.15, 0.6, 1.05, 1.5, 1.9]
        for k in range(len(zs) - 1):
            a, b = zs[k], zs[k + 1]
            y0, y1 = (-1, 1) if k % 2 == 0 else (1, -1)
            span = math.hypot(D - 0.1, b - a)
            ang = math.atan2(b - a, (y1 - y0) * (D - 0.1) / 2)
            body.append(s.rbox((0.03, span, 0.03), (x, 0, (a + b) / 2), r=0.003, seg=1, rot=(ang, 0, 0)))
    s.part("Body", body)
    beams, decks = [], []
    for top in DECKS:
        for sy in (-1, 1):
            beams.append(s.rbox((W - 2 * post + 0.01, 0.05, 0.1), (0, sy * (D / 2 - 0.03), top - 0.05), r=0.006, seg=1))
        decks.append(s.rbox((W - 2 * post - 0.02, D - 0.1, 0.02), (0, 0, top - 0.01), r=0.003, seg=1))
        # three cross bars under the deck
        decks += [s.rbox((0.04, D - 0.1, 0.03), (x, 0, top - 0.035), r=0.003, seg=1) for x in (-0.5, 0.0, 0.5)]
    s.part("Accent", beams)
    s.part("Trim", decks)
    export(os.path.join(MODELS, "Props", "storage_rack.fbx"))


def leaf():
    s.clear_scene()
    W, H, T = 0.9, 2.28, 0.03
    PROUD = 0.02
    body = [s.rbox((W, T, H), (0, 0, H / 2), r=0.003, seg=1)]
    accent = []
    band0, band1 = 0.075, 0.46       # the hazard band, from the floor
    bar = 0.08
    for side in (-1, 1):
        y = side * (T / 2 + PROUD / 2)
        # the raised frame and two rails (the band sits between the frame's sides, under the lower rail)
        body += [s.rbox((W, PROUD, bar), (0, y, z), r=0.006, seg=1) for z in (bar / 2, H - bar / 2, 1.0, 1.6)]
        body += [s.rbox((bar, PROUD, H - 2 * bar + 0.01), (x * (W / 2 - bar / 2), y, H / 2), r=0.006, seg=1) for x in (-1, 1)]
        # the yellow band, a little less proud than the frame, and dark chevrons on it
        by = side * (T / 2 + PROUD * 0.3)
        accent.append(s.rbox((W - 2 * bar + 0.01, PROUD * 0.6, band1 - band0), (0, by, (band0 + band1) / 2), r=0.003, seg=1))
        cy = side * (T / 2 + PROUD * 0.6 + 0.002)
        n = 5
        inner = W - 2 * bar - 0.12
        for k in range(n):
            x = -inner / 2 + k * inner / (n - 1)
            body.append(s.rbox((0.07, PROUD * 0.3, (band1 - band0) * 1.0), (x, cy, (band0 + band1) / 2), r=0.002, seg=1,
                               rot=(0, math.radians(35), 0)))
        # a yellow stripe up the meeting edge (+X: PanelB is this leaf turned round) between the rails
        accent.append(s.rbox((0.05, PROUD * 0.6, 0.5), (W / 2 - bar - 0.035, by, 1.3), r=0.003, seg=1))
        # a pull bar
        body.append(s.rbox((0.03, 0.025, 0.32), (W / 2 - 0.16, side * (T / 2 + PROUD + 0.03), 1.3), r=0.008, seg=2))
        body += [s.rbox((0.03, 0.035, 0.03), (W / 2 - 0.16, side * (T / 2 + PROUD + 0.012), z), r=0.006, seg=1) for z in (1.17, 1.43)]
    s.part("Body", body)
    s.part("Accent", accent)
    export(os.path.join(MODELS, "Level", "storage_leaf.fbx"), (0.93, 1.37), "storage_leaf", "storage_leaf_b")


rack()
leaf()
