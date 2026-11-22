"""Small parts of the lift's landing door (prefab Level/LiftDoor), modelled — they were raw cubes:

- lift_indicator: the indicator over the portal: a brushed housing with bevelled edges and a dark glass face; the lamp
  in the middle (EyeHere: the cab is here) and real arrows either side (EyeUp, EyeDown), each a part of its own (lit in
  play by LiftDoor);
- lift_callplate: the plate the call button sits on, bevelled, with two screws.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/lift_parts.py -- Assets/_Game/Art/Models

Both face -Y (Unity +Z: out of the wall), their backs on y = 0, centred on their middle. The import mirrors X: the up
arrow drawn at +X lands at Unity -X (where the builder had the up lamp).
"""
import os
import sys

import bmesh

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))


def export(name):
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=False, name=name, pinned=lambda n: n.startswith("Eye"))
    s.export_fbx(os.path.join(ROOT, "Props", name + ".fbx"), objs)
    print("exported", name)
    s.clear_scene()


def arrow(cx, cz, up, size=0.05, y0=-0.04, depth=0.006):
    """A flat triangular arrow facing -Y, pointing up or down."""
    bm = bmesh.new()
    k = 1 if up else -1
    pts = [(cx, cz + k * size * 0.55), (cx - size * 0.5, cz - k * size * 0.45), (cx + size * 0.5, cz - k * size * 0.45)]
    if not up:
        pts = list(reversed(pts))
    front = [bm.verts.new((x, y0 - depth, z)) for x, z in pts]
    back = [bm.verts.new((x, y0, z)) for x, z in pts]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    for i in range(3):
        j = (i + 1) % 3
        bm.faces.new((front[j], front[i], back[i], back[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm


def indicator():
    s.part("Body", [s.rbox((0.46, 0.04, 0.12), (0, -0.02, 0), r=0.008, seg=2)])
    s.part("Trim", [s.rbox((0.42, 0.006, 0.09), (0, -0.041, 0), r=0.006, seg=1)])           # the dark glass face
    s.part("EyeHere", [s.rbox((0.15, 0.006, 0.055), (0, -0.046, 0), r=0.008, seg=1)])
    s.part("EyeUp", [arrow(0.15, 0.0, True, y0=-0.043)])
    s.part("EyeDown", [arrow(-0.15, 0.0, False, y0=-0.043)])
    export("lift_indicator")


def callplate():
    s.part("Body", [s.rbox((0.09, 0.012, 0.16), (0, -0.006, 0), r=0.004, seg=2)]
           + [s.rcyl(0.005, 0.004, (0, -0.013, z), axis="Y", seg=8, r=0.001) for z in (-0.065, 0.065)])
    export("lift_callplate")


if __name__ == "__main__":
    s.clear_scene()
    indicator()
    callplate()
    print("DONE")
