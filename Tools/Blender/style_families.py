"""Model families of the visual styles: every model of the game rebuilt in another modelling language.

Run:  blender.exe -b --factory-startup --python Tools/Blender/style_families.py -- Assets/_Game/Art/Models [family ...]

Re-runs every model function of generate_models.py and robots.py with the primitives (box, cylinder, sphere) and the
finishing of a family swapped in, and writes Art/Models/Styles/<Family>/<Category>/<name>__<family>.fbx. Part names
(Body, Accent, Eye, AccentLeg0…) and pivots stay, so the game swaps meshes by name and robot animation keeps working;
every part gets box-projected UVs (1 unit = 1 m) so textured materials tile in metres.

Families:
  lowpoly    PEAK / Risk of Rain 2 / SUPERHOT: chamfered, slightly tapered blocks, 6-sided cylinders, icospheres,
             thin parts thickened, flat shading — chunky low-poly
  toy        R.E.P.O. / Content Warning: heavily rounded blocks, smooth cylinders and spheres, thin parts thickened,
             puffed up, smooth shading — moulded plastic toys
  lofi       Lethal Company / PS1: plain blocks, 6-sided cylinders, vertices snapped to a 2 cm grid, flat shading —
             meant for tiny pixel textures
  clean      Blue Prince / Obra Dinn: crisp 1 cm chamfers that catch the ink lines, smooth round parts
  realistic  PAYDAY / Hitman: small soft bevels, 32-sided round parts, smooth by angle — for PBR materials
  derelict   Dead Space / Alien: Isolation (abandoned station): panelled casings with deep seams, every part a little
             askew, dented and sagging, chipped corners — for the self-made grimy PBR surfaces (derelict_textures.py)
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402
import robots as r  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
FAMILIES = argv[1:] or ["lowpoly", "toy", "lofi", "clean", "realistic", "derelict"]

orig_box, orig_cyl, orig_sphere, orig_merge = g.box, g.cylinder, g.sphere, g.merge
family = None


# --------------------------------------------------------------------------- primitives per family

def thicken(size, k, below=0.05, cap=0.09):
    """Thin parts (legs, boards, rods) get chunkier: a cartoon or toy reads its parts from afar."""
    return tuple(min(cap, s * k) if s < below else s for s in size)


def bevel(bm, offset, segments, angle=30):
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(angle)]
    if edges and offset > 0:
        bmesh.ops.bevel(bm, geom=edges, offset=offset, offset_type="OFFSET", segments=segments, profile=0.5,
                        affect="EDGES", clamp_overlap=True)


def box(size, loc, rot_z=0.0):
    if family == "lowpoly":
        size = thicken(size, 1.9, below=0.08, cap=0.13)
    elif family == "toy":
        size = thicken(size, 1.8, below=0.08, cap=0.12)
    bm = orig_box(size, (0, 0, 0), 0.0)
    m = min(size)
    if family == "lowpoly":
        bevel(bm, min(0.09, 0.28 * m), 1)
        # cartoon proportions: legs and posts narrow towards the floor, bodies towards the top
        if size[2] > 0.15:
            leg = max(size[0], size[1]) < 0.16
            lo_z, hi_z = min(v.co.z for v in bm.verts), max(v.co.z for v in bm.verts)
            for v in bm.verts:
                t = (v.co.z - lo_z) / max(1e-4, hi_z - lo_z)
                k = (0.72 + 0.28 * t) if leg else (1.0 - 0.12 * t)
                v.co.x *= k
                v.co.y *= k
    elif family == "toy":
        bevel(bm, min(0.14, 0.45 * m), 5)
    elif family == "clean":
        bevel(bm, min(0.012, 0.2 * m), 1)
    elif family == "realistic":
        # panelled like a real casing: big faces get an inset panel with a shadow gap, small edges a soft bevel
        big = [f for f in bm.faces if f.calc_area() > 0.03]
        if big:
            res = bmesh.ops.inset_individual(bm, faces=big, thickness=min(0.025, 0.12 * m), depth=0.0)
            bmesh.ops.inset_individual(bm, faces=big, thickness=0.006, depth=-0.005)
        bevel(bm, min(0.006, 0.2 * m), 2)
    elif family == "derelict":
        # industrial casing: a deep seam around every big face, a recessed panel, hard bevels
        big = [f for f in bm.faces if f.calc_area() > 0.02]
        if big:
            bmesh.ops.inset_individual(bm, faces=big, thickness=min(0.03, 0.15 * m), depth=0.0)
            bmesh.ops.inset_individual(bm, faces=big, thickness=0.008, depth=-0.008)
        bevel(bm, min(0.008, 0.2 * m), 1)
        # neglect: every part sits a little askew (deterministic by its position)
        h = math.sin(loc[0] * 12.9898 + loc[1] * 78.233 + loc[2] * 37.719) * 43758.5453
        h -= math.floor(h)
        tilt = (h - 0.5) * math.radians(2.4) if max(size) < 1.2 else 0.0
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(tilt, 3, "X" if h > 0.5 else "Y"), verts=bm.verts)
    if rot_z:
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(rot_z, 3, "Z"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return bm


def cylinder(radius, depth, loc, axis="Z", segments=16, radius2=None):
    seg = {"lowpoly": 6, "lofi": 6, "toy": 24, "clean": 32, "realistic": 32, "derelict": 20}.get(family, segments)
    if family in ("lowpoly", "toy"):
        k = 1.9 if family == "lowpoly" else 1.7
        radius = min(0.065, radius * k) if radius < 0.04 else radius
        if radius2 is not None and radius2 < 0.04:
            radius2 = min(0.065, radius2 * k)
    bm = orig_cyl(radius, depth, (0, 0, 0), axis, seg, radius2)
    if family == "toy":
        bevel(bm, min(0.05, 0.3 * min(radius, depth)), 3)
    elif family in ("realistic", "derelict"):
        bevel(bm, min(0.005, 0.2 * min(radius, depth)), 2)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return bm


def sphere(radius, loc, segments=16):
    if family == "lowpoly":
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=radius)
        bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
        return bm
    seg = {"lofi": 6, "toy": 24, "clean": 24, "realistic": 28, "derelict": 20}.get(family, segments)
    return orig_sphere(radius, loc, seg)


def merge(name, parts):
    obj = orig_merge(name, parts)
    me = obj.data
    if family == "lofi":
        for v in me.vertices:  # PS1-like jitter: everything on a 2 cm grid
            v.co = Vector(round(c / 0.02) * 0.02 for c in v.co)
    if family == "toy":
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.normal_update()
        for v in bm.verts:
            v.co += v.normal * 0.004
        bm.to_mesh(me)
        bm.free()
    if family == "derelict":
        # dents and sag: low-frequency noise pushes surfaces in (never out: nothing grows through walls)
        from mathutils import noise as mnoise
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.normal_update()
        for v in bm.verts:
            n = mnoise.noise(v.co * 6.0)
            v.co -= v.normal * max(0.0, n) * 0.012
        bm.to_mesh(me)
        bm.free()
    # box-projected UVs in metres: textured materials tile the same on every part
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.scene.objects:
        o.select_set(o == obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=False, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def export(filename):
    smooth = family in ("toy", "clean", "realistic", "derelict")
    bpy.ops.object.select_all(action="SELECT")
    for obj in bpy.context.selected_objects:
        for poly in obj.data.polygons:
            poly.use_smooth = smooth
        if smooth and family != "toy":
            # round parts smooth, box edges stay sharp
            bpy.context.view_layer.objects.active = obj
            try:
                bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
            except Exception:  # noqa: BLE001 — older Blender: auto smooth flag
                if hasattr(obj.data, "use_auto_smooth"):
                    obj.data.use_auto_smooth = True
    folder = os.path.join(ROOT, "Styles", family.capitalize(), g.category(filename))
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, f"{filename}__{family}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                             axis_up="Y", bake_space_transform=True, object_types={"MESH"}, use_mesh_modifiers=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False)
    g.clear_scene()


for mod in (g, r):
    mod.box, mod.cylinder, mod.sphere, mod.merge, mod.export = box, cylinder, sphere, merge, export

MODEL_FUNCS = [getattr(g, n) for n in dir(g) if n.startswith("model_")]
ROBOT_FUNCS = [r.drone, r.spider, r.vacuum, r.turret, r.thumper, r.hound, r.sorter, r.worker, r.hunter, r.scanner, r.pir,
               r.plate, r.attach_eye, r.attach_mic, r.attach_seismic, r.attach_nose]

for family in FAMILIES:
    g.clear_scene()
    for fn in MODEL_FUNCS + ROBOT_FUNCS:
        fn()
    print("FAMILY DONE", family)
print("DONE")
