"""The leaf of a sliding door (prefab Level/AutoDoor: two leaves, PanelA and PanelB) and its derelict version.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/door_leaf.py -- Assets/_Game/Art/Models

Writes Level/door_leaf.fbx — the plain leaf every style had (a 0.9 x 2.28 m slab, 6 cm thick) — and
Styles/Derelict/Level/door_leaf__derelict.fbx: a heavy riveted steel leaf in the language of the derelict walls
(panels with seams and rivet rows): a stiffener frame and two rails on both faces, rivets along them, a kick plate,
a vision slot with its frame, a pull and a rubber seal on the meeting edge. One mesh, Body (the door's material, so
the style's rust levels land on it). Origin on the floor in the middle of the leaf; both faces alike (a door is
seen from both rooms). The leaf stays within 0.9 x 2.28 m and 7 cm.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODELS = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
W, H, T = 0.9, 2.28, 0.03          # leaf width, height, core plate thickness
PROUD = 0.025                      # how far the frame and rails stand out of the plate (deep enough to cast shade)


# the second leaf (PanelB) is the same leaf with its texture shifted: the two halves of a door never show the same rust
B_SHIFT = (0.93, 1.37)   # metres in UV


def export(path, shift=None):
    """Exports the scene; with a shift, a second file (name_b) whose UVs are moved by it."""
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
        b = path.replace("door_leaf", "door_leaf_b")
        s.export_fbx(b, objs)
        print("exported", b)
    s.clear_scene()


def plain():
    s.clear_scene()
    s.part("Body", [s.rbox((W, 0.06, H), (0, 0, H / 2), r=0.004, seg=1)])
    export(os.path.join(MODELS, "Level", "door_leaf.fbx"), B_SHIFT)


def derelict():
    s.clear_scene()
    parts = [s.rbox((W, T, H), (0, 0, H / 2), r=0.003, seg=1)]                       # the plate
    bar = 0.09
    for side in (-1, 1):
        y = side * (T / 2 + PROUD / 2)
        # the stiffener frame round the edge, and two rails across
        parts += [s.rbox((W, PROUD, bar), (0, y, z), r=0.009, seg=2) for z in (bar / 2, H - bar / 2, 0.82, 1.52)]
        parts += [s.rbox((bar, PROUD, H - 2 * bar), (x * (W / 2 - bar / 2), y, H / 2), r=0.009, seg=2) for x in (-1, 1)]
        # a kick plate at the bottom, a little proud of the plate
        parts.append(s.rbox((W - 2 * bar - 0.02, PROUD * 0.5, 0.3), (0, side * (T / 2 + PROUD * 0.25), bar + 0.16), r=0.003, seg=1))
        # rivets: rows along the frame and the rails
        ry = side * (T / 2 + PROUD + 0.004)
        dots = []
        for z in (bar / 2, H - bar / 2, 0.82, 1.52):
            n = 7
            dots += [(-W / 2 + 0.06 + k * (W - 0.12) / (n - 1), z) for k in range(n)]
        for x in (-1, 1):
            for k in range(1, 18):
                z = bar + k * (H - 2 * bar) / 18
                if abs(z - 0.82) > 0.06 and abs(z - 1.52) > 0.06:
                    dots.append((x * (W / 2 - bar / 2), z))
        parts += [s.rcyl(0.012, 0.008, (x, ry, z), axis="Y", seg=10, r=0.004) for x, z in dots]
        # the vision slot: a frame round a dark recess (a steel shutter behind it)
        vy = side * (T / 2 + PROUD / 2)
        parts += [s.rbox((0.34, PROUD, 0.025), (0, vy, z), r=0.004, seg=1) for z in (1.78, 1.93)]
        parts += [s.rbox((0.025, PROUD, 0.175), (x * 0.1575, vy, 1.855), r=0.004, seg=1) for x in (-1, 1)]
        # a pull bar near the meeting edge (+X: PanelB is this leaf turned round)
        parts.append(s.rbox((0.03, 0.025, 0.32), (W / 2 - 0.13, side * (T / 2 + PROUD + 0.03), 1.08), r=0.008, seg=2))
        parts += [s.rbox((0.03, 0.03, 0.03), (W / 2 - 0.13, side * (T / 2 + PROUD + 0.012), z), r=0.006, seg=1) for z in (0.95, 1.21)]
    # the rubber seal on the meeting edge
    parts.append(s.rbox((0.012, T + 0.006, H - 0.04), (W / 2 - 0.006, 0, H / 2), r=0.004, seg=1))
    s.part("Body", parts)
    export(os.path.join(MODELS, "Styles", "Derelict", "Level", "door_leaf__derelict.fbx"), B_SHIFT)


plain()
derelict()
