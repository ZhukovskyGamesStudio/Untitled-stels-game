"""Generates placeholder FBX models for the prototype.

Run:  blender.exe -b --factory-startup --python Tools/Blender/generate_models.py -- Assets/_Game/Art/Models
      (models land in sub-folders by category: Props, Level, Player, Items, Robots, Sensors)

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
out_dir = argv[argv.index("--") + 1] if "--" in argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")


def category(filename):
    """Sub-folder of Art/Models for a model (keeps the project sorted the same way the Unity builder expects)."""
    if filename.startswith("item_"):
        return "Items"
    if filename.startswith("robot_") or filename.startswith("attach_"):
        return "Robots"
    if filename.startswith("sensor_") or filename in ("security_camera", "laser_emitter"):
        return "Sensors"
    if filename == "player":
        return "Player"
    if filename in ("hatch", "window", "ceiling_lamp", "exit_sign"):
        return "Level"
    return "Props"


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
    folder = os.path.join(out_dir, category(filename))
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, filename + ".fbx")
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
        box((0.14, 0.14, 0.75), (-0.45, 0, 1.15)),                  # arms
        box((0.14, 0.14, 0.75), (0.45, 0, 1.15)),
    ])
    merge("Accent", [
        box((0.45, 0.4, 0.32), (0, 0, 1.82)),                       # head
        box((0.2, 0.2, 0.2), (-0.45, 0, 0.7)),                      # fists
        box((0.2, 0.2, 0.2), (0.45, 0, 0.7)),
    ])
    # Legs (with feet) are separate so the game can swing them.
    merge("AccentLeg0", [box((0.18, 0.18, 0.9), (-0.22, 0, 0.45)), box((0.26, 0.32, 0.08), (-0.22, -0.06, 0.04))])
    merge("AccentLeg1", [box((0.18, 0.18, 0.9), (0.22, 0, 0.45)), box((0.26, 0.32, 0.08), (0.22, -0.06, 0.04))])
    merge("Eye", [cylinder(0.11, 0.08, (0, -0.22, 1.84), axis="Y", segments=16)])
    export("robot_guard")


def model_robot_listener():
    """Wheeled blind robot with microphone dishes. Height ~1.2m."""
    merge("Body", [
        box((0.8, 1.0, 0.4), (0, 0, 0.42)),                         # chassis
        cylinder(0.08, 0.6, (0, 0.1, 0.9), segments=8),             # mast
    ])
    for i, (x, y) in enumerate([(-0.46, -0.3), (0.46, -0.3), (-0.46, 0.3), (0.46, 0.3)]):
        merge(f"AccentWheel{i}", [cylinder(0.22, 0.12, (x, y, 0.22), axis="X")])   # wheels roll in game
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


# --------------------------------------------------------------------------- more office furniture (layouts after office planning guides:
# back-to-back desk clusters with privacy screens, storage walls, kitchenette along a wall + tables, lounge corners)

def model_partition():
    """Privacy screen between back-to-back desks: 1.6 x 0.05 x 1.25, fabric with a dark frame."""
    merge("Body", [box((1.56, 0.04, 1.2), (0, 0, 0.62))])
    merge("Accent", [box((1.6, 0.06, 0.04), (0, 0, 1.24)), box((0.04, 0.06, 1.25), (-0.79, 0, 0.62)), box((0.04, 0.06, 1.25), (0.79, 0, 0.62))])
    export("partition")


def model_bookshelf():
    """Office bookcase 1.0 x 0.35 x 2.0 with rows of folders."""
    parts = [box((0.04, 0.35, 2.0), (x, 0, 1.0)) for x in (-0.48, 0.48)]
    parts += [box((1.0, 0.35, 0.03), (0, 0, h)) for h in (0.02, 0.5, 1.0, 1.5, 1.98)]
    parts.append(box((1.0, 0.02, 2.0), (0, 0.17, 1.0)))
    merge("Body", parts)
    folders = []
    for i, h in enumerate((0.04, 0.53, 1.03, 1.53)):
        x = -0.42
        for k in range(6 + i % 2):
            w = 0.07 + 0.03 * ((k + i) % 3)
            folders.append(box((w, 0.26, 0.32 - 0.04 * ((k * 7 + i) % 3)), (x + w / 2, 0.02, h + 0.17)))
            x += w + 0.015
    merge("Accent", folders)
    export("bookshelf")


def model_printer():
    """Office copier 0.6 x 0.6 x 1.1 with a paper tray and a glowing panel."""
    merge("Body", [box((0.6, 0.6, 0.95), (0, 0, 0.475)), box((0.55, 0.5, 0.12), (0, 0.02, 1.01))])
    merge("Accent", [box((0.35, 0.3, 0.03), (-0.1, -0.35, 0.8)), box((0.5, 0.05, 0.25), (0, -0.31, 0.3))])
    merge("Eye", [box((0.15, 0.03, 0.08), (0.18, -0.29, 0.97))])
    export("printer")


def model_fridge():
    merge("Body", [box((0.7, 0.7, 1.8), (0, 0, 0.9))])
    merge("Accent", [box((0.04, 0.05, 0.5), (0.28, -0.37, 1.35)), box((0.04, 0.05, 0.35), (0.28, -0.37, 0.6)), box((0.7, 0.02, 0.02), (0, -0.351, 1.05))])
    export("fridge")


def model_coffee_machine():
    """Stands on a counter (0.93 m): 0.35 x 0.4 x 0.5."""
    merge("Body", [box((0.35, 0.4, 0.5), (0, 0, 0.25))])
    merge("Accent", [box((0.2, 0.15, 0.05), (0, -0.17, 0.08)), cylinder(0.04, 0.09, (0, -0.15, 0.17), segments=10)])
    merge("Eye", [box((0.12, 0.02, 0.06), (0, -0.205, 0.4))])
    export("coffee_machine")


def model_round_table():
    merge("Body", [cylinder(0.5, 0.04, (0, 0, 0.74), segments=24)])
    merge("Accent", [cylinder(0.05, 0.72, (0, 0, 0.36), segments=10), cylinder(0.28, 0.03, (0, 0, 0.015), segments=16)])
    export("round_table")


def model_armchair():
    merge("Body", [box((0.75, 0.75, 0.42), (0, 0, 0.21)), box((0.75, 0.18, 0.45), (0, 0.29, 0.64))])
    merge("Accent", [box((0.14, 0.75, 0.6), (-0.37, 0, 0.3)), box((0.14, 0.75, 0.6), (0.37, 0, 0.3))])
    export("armchair")


def model_coffee_table():
    merge("Body", [box((1.0, 0.55, 0.04), (0, 0, 0.4))])
    merge("Accent", [box((0.04, 0.04, 0.38), (x, y, 0.19)) for x in (-0.46, 0.46) for y in (-0.23, 0.23)] + [box((0.9, 0.45, 0.02), (0, 0, 0.12))])
    export("coffee_table")


def model_tv_stand():
    """Low cabinet with a big screen on it, 1.4 wide."""
    merge("Body", [box((1.4, 0.4, 0.5), (0, 0, 0.25))])
    merge("Accent", [box((1.36, 0.06, 0.8), (0, 0.08, 1.05)), box((0.3, 0.2, 0.05), (0, 0.08, 0.525)), box((0.06, 0.06, 0.15), (0, 0.1, 0.6))])
    merge("Eye", [box((1.28, 0.02, 0.72), (0, 0.045, 1.05))])
    export("tv_stand")


def model_reception_desk():
    """Front counter 2.4 x 0.8: high front panel towards the visitor (-Y), work top behind."""
    merge("Body", [box((2.4, 0.08, 1.1), (0, -0.36, 0.55)), box((0.08, 0.8, 1.1), (-1.16, 0, 0.55)), box((0.08, 0.8, 1.1), (1.16, 0, 0.55))])
    merge("Accent", [box((2.5, 0.3, 0.04), (0, -0.33, 1.12)), box((2.3, 0.7, 0.04), (0, 0.02, 0.74))])
    merge("Eye", [box((0.5, 0.04, 0.32), (0.4, 0.15, 0.96)), box((0.06, 0.06, 0.2), (0.4, 0.2, 0.82))])
    export("reception_desk")


def model_trash_bin():
    merge("Body", [cylinder(0.17, 0.42, (0, 0, 0.21), segments=14, radius2=0.19)])
    merge("Accent", [cylinder(0.195, 0.03, (0, 0, 0.43), segments=14)])
    export("trash_bin")


def model_coat_rack():
    merge("Accent", [cylinder(0.03, 1.8, (0, 0, 0.9), segments=8), cylinder(0.25, 0.04, (0, 0, 0.02), segments=12)]
          + [box((0.25, 0.03, 0.03), (0.1 * math.cos(a), 0.1 * math.sin(a), 1.7), rot_z=a) for a in (0.0, 2.1, 4.2)])
    merge("Body", [box((0.35, 0.12, 0.6), (0.12, 0, 1.35)), box((0.3, 0.12, 0.55), (-0.08, 0.1, 1.38), rot_z=2.1)])
    export("coat_rack")


def model_lockers():
    """Three staff lockers, 0.9 x 0.5 x 1.9."""
    merge("Body", [box((0.9, 0.5, 1.9), (0, 0, 0.95))])
    merge("Accent", [box((0.01, 0.02, 1.85), (x, -0.255, 0.95)) for x in (-0.15, 0.15)]
          + [box((0.03, 0.03, 0.12), (x + 0.1, -0.265, 1.0)) for x in (-0.3, 0.0, 0.3)]
          + [box((0.2, 0.02, 0.02), (x, -0.258, 1.75 - k * 0.04)) for x in (-0.3, 0.0, 0.3) for k in range(3)])
    export("lockers")


def model_pallet():
    """Warehouse pallet 1.2 x 1.0 with a wrapped stack of boxes."""
    merge("Accent", [box((1.2, 1.0, 0.04), (0, 0, 0.13))] + [box((1.2, 0.1, 0.1), (0, y, 0.05)) for y in (-0.45, 0, 0.45)])
    merge("Body", [box((0.55, 0.45, 0.4), (x, y, 0.35)) for x in (-0.3, 0.3) for y in (-0.24, 0.24)]
          + [box((0.55, 0.45, 0.4), (-0.3, y, 0.75)) for y in (-0.24, 0.24)])
    export("pallet")


def model_wash_basin():
    """Restroom counter 1.6 x 0.5 with two sinks and a mirror on the wall behind (+Y)."""
    merge("Body", [box((1.6, 0.5, 0.06), (0, 0, 0.85)), box((1.6, 0.45, 0.8), (0, 0.02, 0.4))])
    merge("Accent", [cylinder(0.17, 0.08, (x, -0.02, 0.86), segments=14) for x in (-0.4, 0.4)]
          + [box((0.04, 0.12, 0.15), (x, 0.17, 0.95)) for x in (-0.4, 0.4)])
    merge("Eye", [box((1.5, 0.02, 0.8), (0, 0.24, 1.55))])
    export("wash_basin")


def model_vending_machine():
    merge("Body", [box((0.9, 0.8, 1.9), (0, 0, 0.95))])
    merge("Accent", [box((0.6, 0.02, 0.15), (-0.1, -0.41, 0.25)), box((0.12, 0.02, 0.5), (0.33, -0.41, 1.2))])
    merge("Eye", [box((0.6, 0.02, 1.1), (-0.1, -0.405, 1.15))])
    export("vending_machine")


def model_window():
    """
    Window for the facade: 2.4 x 1.2 opening, centred in a 0.2 m wall. The glass slab shows on both faces (5 mm proud
    of the wall). Outside (+Y): a frame with a mullion and a sill. Inside (-Y = Unity +Z, the room): a slim frame,
    half-closed blinds and a sill — everything within 15 cm of the wall's centre line, so furniture can stand under it.
    """
    w, h = 2.4, 1.2
    merge("Eye", [box((w - 0.1, 0.21, h - 0.1), (0, 0, 0))])  # night glass, glows a little
    frame = [box((w + 0.12, 0.05, 0.08), (0, 0.135, h / 2)), box((w + 0.12, 0.05, 0.08), (0, 0.135, -h / 2)),
             box((0.08, 0.05, h), (-w / 2, 0.135, 0)), box((0.08, 0.05, h), (w / 2, 0.135, 0)),
             box((0.05, 0.05, h), (0, 0.135, 0)),                                     # mullion (outside)
             box((w + 0.25, 0.1, 0.04), (0, 0.15, -h / 2 - 0.03))]                    # exterior sill
    frame += [box((w + 0.12, 0.03, 0.08), (0, -0.115, h / 2)), box((w + 0.12, 0.03, 0.08), (0, -0.115, -h / 2)),
              box((0.08, 0.03, h), (-w / 2, -0.115, 0)), box((0.08, 0.03, h), (w / 2, -0.115, 0)),
              box((w + 0.2, 0.05, 0.04), (0, -0.125, -h / 2 - 0.05))]               # interior sill
    merge("Accent", frame)
    slats = [box((w - 0.14, 0.015, 0.03), (0, -0.12, h / 2 - 0.1 - i * 0.07)) for i in range(8)]
    slats.append(box((w - 0.14, 0.04, 0.05), (0, -0.125, h / 2 - 0.06)))                # blind head rail
    merge("Body", slats)
    export("window")


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


def model_robot_sweeper():
    """Floor cleaner on three wheels, ~0.55 m high. Its lidar puck sits under the body at ankle height (spins in game)."""
    merge("Body", [cylinder(0.38, 0.22, (0, 0, 0.44), segments=24), sphere(0.2, (0, 0.05, 0.56), segments=14)])
    for i, (x, y) in enumerate([(-0.28, 0.18), (0.28, 0.18), (0.0, -0.3)]):
        merge(f"AccentWheel{i}", [cylinder(0.08, 0.06, (x, y, 0.08), axis="X", segments=12)])
    merge("Accent", [cylinder(0.05, 0.16, (0, 0, 0.28), segments=10),                      # stem to the lidar
                     cylinder(0.14, 0.012, (-0.3, -0.25, 0.012), segments=12),             # side brushes
                     cylinder(0.14, 0.012, (0.3, -0.25, 0.012), segments=12)])
    merge("AccentLidar", [cylinder(0.12, 0.07, (0, 0, 0.22), segments=16), box((0.06, 0.05, 0.035), (0, -0.12, 0.22))])
    merge("Eye", [box((0.24, 0.03, 0.05), (0, -0.37, 0.5)), sphere(0.03, (0, -0.15, 0.22), segments=8)])
    export("robot_sweeper")


# --------------------------------------------------------------------------- more furniture

def model_ping_pong_table():
    """Table tennis table 2.74 x 1.525, 0.76 high, net across the middle (long side along X)."""
    merge("Body", [box((2.74, 1.525, 0.03), (0, 0, 0.745))])
    merge("Accent", [box((0.02, 1.7, 0.15), (0, 0, 0.835)), box((2.74, 0.02, 0.005), (0, 0, 0.762))]
          + [box((0.06, 0.06, 0.73), (x, y, 0.365)) for x in (-1.2, 1.2) for y in (-0.6, 0.6)])
    merge("Eye", [box((0.02, 0.02, 0.15), (0, 0.86, 0.835))])                     # net post (no glow: same material as accent)
    export("ping_pong_table")


def model_bean_bag():
    """Squashed bean bag ~0.9 m across."""
    merge("Body", [cylinder(0.45, 0.32, (0, 0, 0.16), segments=16, radius2=0.36), sphere(0.33, (0, 0.08, 0.36), segments=14)])
    export("bean_bag")


def model_arcade_machine():
    """Arcade cabinet 0.7 x 0.8 x 1.75 facing -Y; a glowing screen and a control panel."""
    merge("Body", [box((0.7, 0.7, 1.75), (0, 0.05, 0.875))])
    merge("Accent", [box((0.7, 0.3, 0.08), (0, -0.4, 1.0)), box((0.72, 0.1, 0.25), (0, -0.25, 1.68))])
    merge("Eye", [box((0.56, 0.03, 0.42), (0, -0.31, 1.33))])
    export("arcade_machine")


def model_safe():
    """Office safe 0.8 x 0.7 x 1.0 with a handle wheel and a keypad on the door (-Y)."""
    merge("Body", [box((0.8, 0.7, 1.0), (0, 0, 0.5))])
    merge("Accent", [box((0.7, 0.03, 0.9), (0, -0.36, 0.5)), cylinder(0.1, 0.04, (0.12, -0.39, 0.55), axis="Y", segments=12)])
    merge("Eye", [box((0.12, 0.02, 0.16), (-0.22, -0.385, 0.65))])
    export("safe")


# --------------------------------------------------------------------------- light fixtures

def model_ceiling_lamp():
    """Recessed fluorescent panel 1.2 x 0.6, hanging from the ceiling: the origin is the ceiling surface, it goes down."""
    merge("Body", [box((1.24, 0.64, 0.06), (0, 0, -0.03))])                       # housing
    merge("Accent", [box((1.24, 0.03, 0.03), (0, y, -0.065)) for y in (-0.31, 0.31)]
          + [box((0.03, 0.64, 0.03), (x, 0, -0.065)) for x in (-0.61, 0.61)])      # trim
    merge("Eye", [box((1.16, 0.56, 0.02), (0, 0, -0.07))])                         # glowing diffuser
    export("ceiling_lamp")


def model_desk_lamp():
    """Desk lamp ~0.45 m: round base, angled arm, cone shade looking down-forward (-Y = Unity +Z)."""
    import mathutils
    merge("Body", [cylinder(0.08, 0.025, (0, 0, 0.0125), segments=16),
                   cylinder(0.012, 0.32, (0, 0.02, 0.17), segments=8),
                   box((0.02, 0.18, 0.02), (0, -0.06, 0.33))])
    shade = cylinder(0.075, 0.12, (0, 0, 0), segments=16, radius2=0.03)
    bmesh.ops.rotate(shade, cent=Vector((0, 0, 0)), matrix=mathutils.Matrix.Rotation(-0.5, 3, "X"), verts=shade.verts)
    bmesh.ops.translate(shade, vec=Vector((0, -0.15, 0.31)), verts=shade.verts)
    merge("Accent", [shade])
    merge("Eye", [sphere(0.03, (0, -0.16, 0.27), segments=10)])                   # bulb
    export("desk_lamp")


def model_floor_lamp():
    """Standing lamp 1.6 m with a fabric drum shade."""
    merge("Body", [cylinder(0.16, 0.03, (0, 0, 0.015), segments=16), cylinder(0.015, 1.45, (0, 0, 0.75), segments=8)])
    merge("Accent", [cylinder(0.2, 0.3, (0, 0, 1.5), segments=20, radius2=0.16)])
    merge("Eye", [cylinder(0.14, 0.02, (0, 0, 1.36), segments=16)])               # glowing bottom of the shade
    export("floor_lamp")


def model_exit_sign():
    """Emergency exit sign above a door: 0.36 x 0.3 thick (sticks out of both faces of a 0.2 m wall) x 0.15 high."""
    merge("Body", [box((0.38, 0.26, 0.16), (0, 0, 0))])
    merge("Eye", [box((0.34, 0.3, 0.12), (0, 0, 0))])                              # green glowing faces
    merge("Accent", [box((0.05, 0.31, 0.07), (-0.1, 0, 0)), box((0.12, 0.31, 0.02), (0.05, 0, 0))])  # white arrow / figure
    export("exit_sign")


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
    model_partition()
    model_bookshelf()
    model_printer()
    model_fridge()
    model_coffee_machine()
    model_round_table()
    model_armchair()
    model_coffee_table()
    model_tv_stand()
    model_reception_desk()
    model_trash_bin()
    model_coat_rack()
    model_lockers()
    model_pallet()
    model_wash_basin()
    model_vending_machine()
    model_window()
    model_ceiling_lamp()
    model_desk_lamp()
    model_floor_lamp()
    model_exit_sign()
    model_robot_sweeper()
    model_ping_pong_table()
    model_bean_bag()
    model_arcade_machine()
    model_safe()
    model_items()
    print("DONE")
