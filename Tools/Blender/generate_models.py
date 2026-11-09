"""Generates placeholder FBX models for the prototype.

Run:  blender.exe -b --factory-startup --python Tools/Blender/generate_models.py -- <output_dir>

Conventions:
  * meters, model front faces Blender -Y  ->  Unity +Z (forward)
  * every model is exported as a few child meshes named by material role:
      Body   - main color
      Accent - secondary color
      Eye    - emissive / sensor parts
    The Unity builder assigns materials by these names.
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector

argv = sys.argv
out_dir = argv[argv.index("--") + 1] if "--" in argv else os.path.join(os.getcwd(), "Assets", "_Game", "Models")
os.makedirs(out_dir, exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials):
        for item in list(block):
            block.remove(item)


def _new_object(name, bm):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def box(size, loc, rot_z=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if rot_z:
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=__import__("mathutils").Matrix.Rotation(rot_z, 3, "Z"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return bm


def cylinder(radius, depth, loc, axis="Z", segments=16, radius2=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=depth)
    import mathutils
    if axis == "X":
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=mathutils.Matrix.Rotation(math.pi / 2, 3, "Y"), verts=bm.verts)
    elif axis == "Y":
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=mathutils.Matrix.Rotation(math.pi / 2, 3, "X"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return bm


def sphere(radius, loc, segments=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=max(6, segments // 2), radius=radius)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return bm


def merge(name, parts):
    """Merge several bmeshes into one object."""
    result = bmesh.new()
    for part in parts:
        tmp = bpy.data.meshes.new("tmp")
        part.to_mesh(tmp)
        part.free()
        result.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    bmesh.ops.recalc_face_normals(result, faces=result.faces)
    return _new_object(name, result)


def export(filename):
    bpy.ops.object.select_all(action="SELECT")
    for obj in bpy.context.selected_objects:
        for poly in obj.data.polygons:
            poly.use_smooth = False
    path = os.path.join(out_dir, filename + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
    )
    print("exported", path)
    clear_scene()


# --------------------------------------------------------------------------- characters

def model_player():
    merge("Body", [
        cylinder(0.28, 1.0, (0, 0, 0.75), segments=12),          # torso
        box((0.22, 0.22, 0.7), (-0.14, 0, 0.35)),                  # legs
        box((0.22, 0.22, 0.7), (0.14, 0, 0.35)),
        box((0.12, 0.12, 0.6), (-0.38, 0, 0.95)),                  # arms
        box((0.12, 0.12, 0.6), (0.38, 0, 0.95)),
    ])
    merge("Accent", [
        box((0.44, 0.16, 0.5), (0, 0.3, 0.95)),                     # backpack (behind, +Y)
    ])
    # Head parts are separate objects so the game can tilt them (look direction of teammates).
    merge("AccentHead", [box((0.36, 0.34, 0.34), (0, 0, 1.47))])
    merge("EyeGlasses", [box((0.3, 0.04, 0.09), (0, -0.18, 1.52))])  # glasses strip, front
    export("player")


def model_robot_guard():
    """Bipedal guard robot with a big camera eye. Height ~2.0m."""
    merge("Body", [
        box((0.7, 0.45, 0.75), (0, 0, 1.25)),                       # torso
        box((0.18, 0.18, 0.9), (-0.22, 0, 0.45)),                   # legs
        box((0.18, 0.18, 0.9), (0.22, 0, 0.45)),
        box((0.14, 0.14, 0.75), (-0.45, 0, 1.15)),                  # arms
        box((0.14, 0.14, 0.75), (0.45, 0, 1.15)),
    ])
    merge("Accent", [
        box((0.45, 0.4, 0.32), (0, 0, 1.82)),                       # head
        box((0.26, 0.32, 0.08), (-0.22, -0.06, 0.04)),              # feet
        box((0.26, 0.32, 0.08), (0.22, -0.06, 0.04)),
        box((0.2, 0.2, 0.2), (-0.45, 0, 0.7)),                      # fists
        box((0.2, 0.2, 0.2), (0.45, 0, 0.7)),
    ])
    merge("Eye", [cylinder(0.11, 0.08, (0, -0.22, 1.84), axis="Y", segments=16)])
    export("robot_guard")


def model_robot_listener():
    """Wheeled blind robot with microphone dishes. Height ~1.2m."""
    merge("Body", [
        box((0.8, 1.0, 0.4), (0, 0, 0.42)),                         # chassis
        cylinder(0.08, 0.6, (0, 0.1, 0.9), segments=8),             # mast
    ])
    merge("Accent", [
        cylinder(0.22, 0.12, (-0.46, -0.3, 0.22), axis="X"),        # wheels
        cylinder(0.22, 0.12, (0.46, -0.3, 0.22), axis="X"),
        cylinder(0.22, 0.12, (-0.46, 0.3, 0.22), axis="X"),
        cylinder(0.22, 0.12, (0.46, 0.3, 0.22), axis="X"),
    ])
    merge("Eye", [
        cylinder(0.25, 0.12, (-0.25, 0.1, 1.2), axis="X", radius2=0.05),  # ear dishes
        cylinder(0.25, 0.12, (0.25, 0.1, 1.2), axis="X", radius2=0.05),
        sphere(0.07, (0, -0.52, 0.5)),                                    # front mic
    ])
    export("robot_listener")


# --------------------------------------------------------------------------- security

def model_security_camera():
    """Mounted on a wall: wall is at +Y side (behind), camera looks to -Y (Unity +Z)."""
    merge("Body", [box((0.16, 0.06, 0.22), (0, 0.27, 0))])           # wall plate
    merge("Accent", [
        box((0.05, 0.22, 0.05), (0, 0.15, 0)),                       # arm
        box((0.18, 0.34, 0.16), (0, -0.05, -0.02)),                  # housing
    ])
    merge("Eye", [cylinder(0.055, 0.04, (0, -0.24, -0.02), axis="Y")])
    export("security_camera")


def model_laser_emitter():
    merge("Body", [box((0.12, 0.12, 1.6), (0, 0, 0.8))])
    merge("Eye", [box((0.06, 0.04, 1.4), (0, -0.07, 0.85))])          # emitter strip facing -Y
    export("laser_emitter")


def model_hatch():
    merge("Body", [cylinder(0.75, 0.06, (0, 0, 0.03), segments=24)])
    merge("Accent", [
        box((1.2, 0.08, 0.02), (0, -0.25, 0.07)),
        box((1.2, 0.08, 0.02), (0, 0, 0.07)),
        box((1.2, 0.08, 0.02), (0, 0.25, 0.07)),
    ])
    merge("Eye", [cylinder(0.9, 0.02, (0, 0, 0.01), segments=24, radius2=0.9)])
    export("hatch")


# --------------------------------------------------------------------------- office props

def model_desk():
    merge("Body", [box((1.6, 0.8, 0.05), (0, 0, 0.74))])
    merge("Accent", [
        box((0.05, 0.75, 0.72), (-0.76, 0, 0.36)),
        box((0.05, 0.75, 0.72), (0.76, 0, 0.36)),
        box((1.5, 0.03, 0.4), (0, 0.36, 0.52)),
    ])
    merge("Eye", [
        box((0.55, 0.04, 0.34), (0, 0.15, 0.98)),                   # monitor
        box((0.06, 0.06, 0.2), (0, 0.2, 0.84)),
    ])
    export("desk")


def model_chair():
    merge("Body", [
        box((0.5, 0.5, 0.08), (0, 0, 0.48)),
        box((0.5, 0.08, 0.55), (0, 0.24, 0.8)),
    ])
    merge("Accent", [
        cylinder(0.04, 0.42, (0, 0, 0.24), segments=8),
        box((0.6, 0.06, 0.04), (0, 0, 0.03)),
        box((0.06, 0.6, 0.04), (0, 0, 0.03)),
    ])
    export("chair")


def model_server_rack():
    merge("Body", [box((0.7, 1.0, 2.0), (0, 0, 1.0))])
    parts = [box((0.6, 0.02, 0.06), (0, -0.505, 0.2 + i * 0.16)) for i in range(11)]
    merge("Eye", parts)
    export("server_rack")


def model_cabinet():
    merge("Body", [box((0.9, 0.5, 1.4), (0, 0, 0.7))])
    merge("Accent", [box((0.3, 0.03, 0.04), (0, -0.26, 0.4 + i * 0.4)) for i in range(3)])
    export("cabinet")


def model_meeting_table():
    merge("Body", [box((3.2, 1.3, 0.06), (0, 0, 0.75))])
    merge("Accent", [
        box((0.3, 0.3, 0.72), (-1.1, 0, 0.36)),
        box((0.3, 0.3, 0.72), (1.1, 0, 0.36)),
    ])
    export("meeting_table")


def model_plant():
    merge("Accent", [cylinder(0.22, 0.45, (0, 0, 0.225), segments=12, radius2=0.18)])
    merge("Body", [
        sphere(0.35, (0, 0, 0.8), segments=10),
        sphere(0.25, (0.1, 0.05, 1.15), segments=10),
    ])
    export("plant")


def model_kitchen_counter():
    merge("Body", [box((2.4, 0.65, 0.88), (0, 0, 0.44))])
    merge("Accent", [box((2.44, 0.7, 0.05), (0, 0, 0.905))])
    merge("Eye", [box((0.5, 0.45, 0.32), (0.7, 0, 1.09))])         # microwave
    export("kitchen_counter")


def model_shelf():
    """Storage rack 2.0 x 0.6 x 2.0 with three shelves (0.45, 1.05, 1.65) and a few boxes."""
    parts = [box((0.05, 0.6, 2.0), (x, 0, 1.0)) for x in (-0.98, 0.98)]
    parts += [box((2.0, 0.6, 0.04), (0, 0, h)) for h in (0.05, 0.45, 1.05, 1.65)]
    merge("Body", parts)
    merge("Accent", [
        box((0.45, 0.4, 0.3), (-0.6, 0, 0.62)), box((0.35, 0.35, 0.25), (0.5, 0.05, 0.6)),
        box((0.5, 0.45, 0.35), (0.3, 0, 1.25)), box((0.4, 0.4, 0.3), (-0.55, 0, 1.82)),
    ])
    export("shelf")


def model_boxes():
    merge("Accent", [box((0.6, 0.6, 0.5), (0, 0, 0.25)), box((0.5, 0.5, 0.4), (0.05, -0.05, 0.7)), box((0.45, 0.45, 0.4), (0.55, 0.1, 0.2))])
    export("boxes")


def model_water_cooler():
    merge("Body", [box((0.35, 0.35, 1.0), (0, 0, 0.5))])
    merge("Eye", [cylinder(0.15, 0.45, (0, 0, 1.25), segments=12)])
    export("water_cooler")


def model_whiteboard():
    merge("Body", [box((1.8, 0.05, 1.0), (0, 0.0, 1.5))])
    merge("Accent", [box((0.05, 0.05, 1.4), (-0.8, 0, 0.7)), box((0.05, 0.05, 1.4), (0.8, 0, 0.7)), box((1.9, 0.08, 0.06), (0, 0, 1.0))])
    export("whiteboard")


def model_sofa():
    merge("Body", [box((2.0, 0.85, 0.45), (0, 0, 0.22)), box((2.0, 0.2, 0.45), (0, 0.33, 0.66))])
    merge("Accent", [box((0.2, 0.85, 0.65), (-1.0, 0, 0.33)), box((0.2, 0.85, 0.65), (1.0, 0, 0.33))])
    export("sofa")


def model_toilet_stall():
    """1.0 wide, 1.5 deep cubicle walls (2.0 high, raised), door side open towards -Y."""
    merge("Body", [
        box((0.05, 1.5, 1.9), (-0.5, 0, 1.05)), box((0.05, 1.5, 1.9), (0.5, 0, 1.05)), box((1.0, 0.05, 1.9), (0, 0.75, 1.05)),
    ])
    merge("Accent", [cylinder(0.2, 0.42, (0, 0.4, 0.21), segments=12)])
    export("toilet_stall")


# --------------------------------------------------------------------------- tetris items

ITEM_CELL = 0.22
SHAPES = {
    "mono":   [(0, 0)],
    "domino": [(0, 0), (1, 0)],
    "tri_l":  [(0, 0), (1, 0), (0, 1)],
    "tet_i":  [(0, 0), (1, 0), (2, 0), (3, 0)],
    "tet_o":  [(0, 0), (1, 0), (0, 1), (1, 1)],
    "tet_t":  [(0, 0), (1, 0), (2, 0), (1, 1)],
    "tet_l":  [(0, 0), (0, 1), (0, 2), (1, 0)],
    "tet_s":  [(0, 0), (1, 0), (1, 1), (2, 1)],
}


def model_items():
    c = ITEM_CELL
    for name, cells in SHAPES.items():
        cx = (min(x for x, _ in cells) + max(x for x, _ in cells)) / 2.0
        cy = (min(y for _, y in cells) + max(y for _, y in cells)) / 2.0
        # Shape lies flat on the ground: grid X -> Blender X, grid Y -> Blender -Y (Unity +Z).
        parts = [box((c * 0.96, c * 0.96, c * 0.96), ((x - cx) * c, -(y - cy) * c, c / 2)) for x, y in cells]
        merge("Body", parts)
        export("item_" + name)


if __name__ == "__main__":
    clear_scene()
    model_player()
    model_robot_guard()
    model_robot_listener()
    model_security_camera()
    model_laser_emitter()
    model_hatch()
    model_desk()
    model_chair()
    model_server_rack()
    model_cabinet()
    model_meeting_table()
    model_plant()
    model_kitchen_counter()
    model_shelf()
    model_boxes()
    model_water_cooler()
    model_whiteboard()
    model_sofa()
    model_toilet_stall()
    model_items()
    print("DONE")
