"""Architecture kit of the visual styles: trims, panels, columns, beams, pipes, light strips.

Run:  blender.exe -b --factory-startup --python Tools/Blender/style_kit.py -- Assets/_Game/Art/Models/StyleKit

Conventions (Unity side, after export): local X runs along the wall, +Z points out of the wall into the room, +Y is up.
The origin is on the wall face. Pieces that run along a wall are 1 m long (x from -0.5 to 0.5) and get stretched to the
wall: their profile does not change. Child meshes by role: Body (main trim colour), Accent (second colour), Eye (glow).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
base_dir = argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models", "StyleKit")
# no family: the plain kit in StyleKit/; with families: StyleKit/<Family>/ reshaped like the family's models
FAMILIES = argv[1:] or [None]
family = None
out_dir = base_dir

# Blender: -Y is the room side (Unity +Z). A depth d out of the wall is y = -d.


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.objects):
        for item in list(block):
            block.remove(item)


def box(x0, x1, d0, d1, z0, z1):
    """Axis-aligned box: x along the wall, d = depth out of the wall (0 = wall face), z up."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((x1 - x0, d1 - d0, z1 - z0)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(((x0 + x1) / 2, -(d0 + d1) / 2, (z0 + z1) / 2)), verts=bm.verts)
    return bm


def cyl_x(r, d, z, x0=-0.5, x1=0.5, seg=12):
    """Cylinder along the wall at depth d and height z."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=x1 - x0)
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(((x0 + x1) / 2, -d, z)), verts=bm.verts)
    return bm


def merge(name, parts):
    result = bmesh.new()
    for part in parts:
        tmp = bpy.data.meshes.new("tmp")
        part.to_mesh(tmp)
        part.free()
        result.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    bmesh.ops.recalc_face_normals(result, faces=result.faces)
    mesh = bpy.data.meshes.new(name)
    result.to_mesh(mesh)
    result.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def finish(obj):
    """Reshape a finished piece like the models of the family (see style_families.py)."""
    if family is None:
        return False
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    def bevel(offset, segments):
        edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(30)]
        if edges:
            bmesh.ops.bevel(bm, geom=edges, offset=offset, offset_type="OFFSET", segments=segments, profile=0.5,
                            affect="EDGES", clamp_overlap=True)
    smooth = False
    if family == "lowpoly":
        for v in bm.verts:
            v.co.y *= 1.6  # chunkier: everything sticks out further from the wall
        bevel(0.025, 1)
    elif family == "toy":
        for v in bm.verts:
            v.co.y *= 1.4
        bevel(0.03, 4)
        smooth = True
    elif family == "lofi":
        for v in bm.verts:
            v.co = Vector(round(c / 0.02) * 0.02 for c in v.co)
    elif family == "clean":
        bevel(0.006, 1)
    elif family == "realistic":
        bevel(0.004, 2)
        smooth = True
    bm.to_mesh(obj.data)
    bm.free()
    if True:
        # box-projected UVs for textured trims (1 unit = 1 m)
        bpy.context.view_layer.objects.active = obj
        for o in bpy.context.scene.objects:
            o.select_set(o == obj)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=False, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
    return smooth


def export(name, smooth=False):
    bpy.ops.object.select_all(action="SELECT")
    for obj in list(bpy.context.scene.objects):
        if obj.type == "MESH":
            smooth = finish(obj) or smooth
    bpy.ops.object.select_all(action="SELECT")
    for obj in bpy.context.selected_objects:
        for p in obj.data.polygons:
            p.use_smooth = smooth
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z",
                             axis_up="Y", bake_space_transform=True, object_types={"MESH"}, use_mesh_modifiers=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False)
    print("exported", path)
    clear()


# --------------------------------------------------------------------------- along the walls (1 m, stretched)

def baseboard():
    merge("Body", [box(-0.5, 0.5, 0, 0.018, 0, 0.12), box(-0.5, 0.5, 0, 0.026, 0.105, 0.135), box(-0.5, 0.5, 0, 0.012, 0.135, 0.15)])
    export("kit_baseboard")


def crown():
    """Stepped cornice under the ceiling: the origin is the top edge (it hangs down)."""
    merge("Body", [box(-0.5, 0.5, 0, 0.03, -0.2, 0), box(-0.5, 0.5, 0.03, 0.08, -0.12, 0), box(-0.5, 0.5, 0.08, 0.14, -0.05, 0),
                   box(-0.5, 0.5, 0, 0.045, -0.23, -0.2)])
    export("kit_crown")


def rail():
    """Chair rail: the origin is its centre line."""
    merge("Body", [box(-0.5, 0.5, 0, 0.022, -0.035, 0.035), box(-0.5, 0.5, 0, 0.032, -0.012, 0.012)])
    export("kit_rail")


def wainscot():
    """One 1 m wide, 1 m high panel: a board with a raised frame (Accent) and a cap (Body)."""
    w, h = 1.0, 1.0
    merge("Accent", [box(-0.5, 0.5, 0, 0.012, 0, h)])
    frame = [box(-0.5, -0.42, 0.012, 0.03, 0.12, h - 0.05), box(0.42, 0.5, 0.012, 0.03, 0.12, h - 0.05),
             box(-0.42, 0.42, 0.012, 0.03, 0.12, 0.2), box(-0.42, 0.42, 0.012, 0.03, h - 0.13, h - 0.05),
             box(-0.34, 0.34, 0.012, 0.022, 0.28, h - 0.21)]  # the raised field
    frame += [box(-0.5, 0.5, 0, 0.04, h - 0.05, h), box(-0.5, 0.5, 0, 0.022, 0, 0.12)]  # cap and skirting
    merge("Body", frame)
    export("kit_wainscot")


def pipes():
    """Two pipes along the wall at 2.65 m (origin at the floor on the wall face) and their clamps every metre."""
    merge("Body", [cyl_x(0.06, 0.12, 2.62), cyl_x(0.035, 0.26, 2.7)])
    export("kit_pipes")
    merge("Accent", [box(-0.03, 0.03, 0, 0.3, 2.55, 2.58), box(-0.03, 0.03, 0.09, 0.15, 2.55, 2.69), box(-0.025, 0.025, 0.23, 0.29, 2.66, 2.74)])
    export("kit_pipe_clamp")


def strip():
    """A glowing slit in the wall, 1 m tall unit (stretched to the wall height): a recess frame and the light."""
    merge("Body", [box(-0.07, -0.05, 0, 0.03, 0, 1), box(0.05, 0.07, 0, 0.03, 0, 1)])
    merge("Eye", [box(-0.05, 0.05, 0, 0.012, 0, 1)])
    export("kit_strip")


# --------------------------------------------------------------------------- single pieces

def pilaster():
    """A flat column against the wall, full wall height (3 m): base, fluted shaft, capital."""
    H = 3.0
    shaft = [box(-0.15, 0.15, 0, 0.05, 0.25, H - 0.25)]
    flutes = [box(x - 0.012, x + 0.012, 0.05, 0.058, 0.32, H - 0.32) for x in (-0.1, -0.05, 0.0, 0.05, 0.1)]
    merge("Body", shaft)
    merge("Accent", flutes + [box(-0.22, 0.22, 0, 0.09, 0, 0.18), box(-0.19, 0.19, 0, 0.07, 0.18, 0.25),
                              box(-0.19, 0.19, 0, 0.07, H - 0.25, H - 0.17), box(-0.23, 0.23, 0, 0.1, H - 0.17, H - 0.08),
                              box(-0.2, 0.2, 0, 0.06, H - 0.08, H)])
    export("kit_pilaster")


def casing():
    """Door casing around a 1.6 x 2.3 m opening: two jambs and a head with a small cornice (origin: the opening's bottom centre)."""
    w, h = 1.6, 2.3
    merge("Body", [box(-w / 2 - 0.12, -w / 2, 0, 0.025, 0, h), box(w / 2, w / 2 + 0.12, 0, 0.025, 0, h),
                   box(-w / 2 - 0.12, w / 2 + 0.12, 0, 0.025, h, h + 0.12)])
    merge("Accent", [box(-w / 2 - 0.16, w / 2 + 0.16, 0, 0.05, h + 0.12, h + 0.17), box(-0.1, 0.1, 0, 0.04, h - 0.02, h + 0.12)])
    export("kit_casing")


def arch():
    """A flattened arch inside a 1.6 x 2.3 m opening (spandrels fill its top corners) with a moulding: Blue Prince doorways."""
    w, top = 1.6, 3.0
    r = w / 2
    parts = []
    seg = 14
    for i in range(seg):
        a0, a1 = math.pi * i / seg, math.pi * (i + 1) / seg
        x0, x1 = -r * math.cos(a0), -r * math.cos(a1)
        z0, z1 = 2.3 - 0.55 + r * math.sin(a0) * 0.7, 2.3 - 0.55 + r * math.sin(a1) * 0.7
        lo = min(x0, x1)
        hi = max(x0, x1)
        parts.append(box(lo, hi, 0, 0.03, min(z0, z1), 2.3))  # the arch soffit seen from the room
    merge("Accent", [box(-r - 0.12, -r, 0, 0.03, 0, 2.3), box(r, r + 0.12, 0, 0.03, 0, 2.3)])
    # keystone and the moulding following the arch
    ring = []
    for i in range(seg):
        a = math.pi * (i + 0.5) / seg
        x, z = -r * math.cos(a), 2.3 - 0.55 + r * math.sin(a) * 0.7
        ring.append(box(x - 0.1, x + 0.1, 0.03, 0.06, z - 0.03, z + 0.07))
    merge("Body", ring + parts)  # no keystone: the exit sign hangs right above the doorway
    export("kit_arch")


def beam():
    """Timber ceiling beam, 1 m (stretched), 20 x 22 cm, origin on the ceiling at its centre line."""
    merge("Body", [box(-0.5, 0.5, -0.1, 0.1, -0.22, 0)])
    merge("Accent", [box(-0.5, 0.5, -0.11, 0.11, -0.03, 0)])
    export("kit_beam")


def ibeam():
    """Steel I-beam, 1 m (stretched), 25 cm deep."""
    merge("Body", [box(-0.5, 0.5, -0.09, 0.09, -0.025, 0), box(-0.5, 0.5, -0.012, 0.012, -0.225, -0.025),
                   box(-0.5, 0.5, -0.09, 0.09, -0.25, -0.225)])
    merge("Accent", [box(x - 0.008, x + 0.008, -0.1, 0.1, -0.25, 0) for x in (-0.25, 0.25)])
    export("kit_ibeam")


def coffer():
    """Coffered-ceiling rib with a stepped edge, 1 m (stretched)."""
    merge("Body", [box(-0.5, 0.5, -0.12, 0.12, -0.18, 0)])
    merge("Accent", [box(-0.5, 0.5, -0.16, 0.16, -0.05, 0), box(-0.5, 0.5, -0.08, 0.08, -0.21, -0.18)])
    export("kit_coffer")


def tray():
    """Cable tray hung under the ceiling, 1 m (stretched): a U channel (Body) with cables (Accent)."""
    merge("Body", [box(-0.5, 0.5, -0.16, 0.16, -0.32, -0.3), box(-0.5, 0.5, -0.16, -0.145, -0.32, -0.24),
                   box(-0.5, 0.5, 0.145, 0.16, -0.32, -0.24)])
    merge("Accent", [cyl_x(0.025, d, -0.27) for d in (-0.1, -0.04, 0.03, 0.09)])
    export("kit_tray")
    merge("Body", [box(-0.01, 0.01, -0.17, -0.15, -0.32, 0), box(-0.01, 0.01, 0.15, 0.17, -0.32, 0)])
    export("kit_tray_hanger")


if __name__ == "__main__":
    for family in FAMILIES:
        out_dir = base_dir if family is None else os.path.join(base_dir, family.capitalize())
        clear()
        for fn in (baseboard, crown, rail, wainscot, pipes, strip, pilaster, casing, arch, beam, ibeam, coffer, tray):
            fn()
    print("DONE")
