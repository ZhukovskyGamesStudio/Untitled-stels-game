"""An apartment balcony (prefab Level/Balcony: PlanBalcony, a 3.2 x 1.1 m slab out of a living room's outer wall, a 1 m
parapet), modelled — it was raw cubes: a concrete slab with chamfered edges and a drip groove under its lip, a parapet
of concrete panels with recessed fields and a rounded coping, a steel handrail on short posts along its top.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/balcony.py -- Assets/_Game/Art/Models

Writes Props/balcony.fbx: origin on the slab's top at the wall's face, the balcony out along -Y (Unity +Z); parts Body
(concrete: the builder gives the wall's material) and Accent (the steel rail). Colliders are the prefab's own boxes.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
L, D, SLAB, WALL, H = 3.2, 1.1, 0.18, 0.08, 1.0


def main():
    import bpy
    s.clear_scene()
    slab = s.rbox((L, D, SLAB), (0, -D / 2, -SLAB / 2), r=0.015, seg=2)
    s.groove(slab, "Y", -D + 0.06, 0.012, 0.01)                         # the drip groove round the lip's underside
    front = s.rbox((L, WALL, H), (0, -D + WALL / 2, H / 2), r=0.01, seg=2)
    sides = [s.rbox((WALL, D - WALL + 0.01, H), (x * (L / 2 - WALL / 2), -(D - WALL) / 2 + 0.005, H / 2), r=0.01, seg=2)
             for x in (-1, 1)]
    # recessed fields on both faces of the front panel
    s.inset_faces(front, s.facing((0, -1, 0), min_area=0.5), thickness=0.08, depth=-0.012)
    s.inset_faces(front, s.facing((0, 1, 0), min_area=0.5), thickness=0.08, depth=-0.012)
    coping = [s.rbox((L + 0.03, WALL + 0.04, 0.04), (0, -D + WALL / 2, H + 0.015), r=0.015, seg=2)]
    coping += [s.rbox((WALL + 0.04, D - WALL, 0.04), (x * (L / 2 - WALL / 2), -(D - WALL) / 2 - 0.02, H + 0.015), r=0.015, seg=2)
               for x in (-1, 1)]
    s.part("Body", [slab, front] + sides + coping)
    rail = [s.tube([(-L / 2 + 0.06, -0.02, H + 0.14), (-L / 2 + 0.06, -D + 0.06, H + 0.14), (L / 2 - 0.06, -D + 0.06, H + 0.14),
                    (L / 2 - 0.06, -0.02, H + 0.14)], 0.02, res=2, smooth=False, segments_per=1)]
    posts = [(-L / 2 + 0.06, -0.3), (-L / 2 + 0.06, -D + 0.06), (-0.8, -D + 0.06), (0.0, -D + 0.06), (0.8, -D + 0.06),
             (L / 2 - 0.06, -D + 0.06), (L / 2 - 0.06, -0.3)]
    rail += [s.rcyl(0.012, 0.13, (x, y, H + 0.085), seg=10, r=0.003) for x, y in posts]
    s.part("Accent", rail)
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=False, name="balcony")
    s.export_fbx(os.path.join(ROOT, "Props", "balcony.fbx"), objs)
    print("exported balcony")


if __name__ == "__main__":
    main()
