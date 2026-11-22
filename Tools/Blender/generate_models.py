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

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402  (no two parts z-fighting in one plane)

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


def split_leg(obj, knee_z):
    """Cut a leg at the knee height into <name>Upper (thigh, from the hip) and <name>Lower (shin with the foot); the cuts
    are closed. The game bends the two parts with inverse kinematics (ProceduralLegs): hip, knee and foot are found from
    their bounds, so the cut is all a model needs."""
    name = obj.name
    for part, keep_above in (("Upper", True), ("Lower", False)):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, knee_z), plane_no=(0, 0, 1),
                                     clear_inner=keep_above, clear_outer=not keep_above)
        cut = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if cut:
            bmesh.ops.holes_fill(bm, edges=cut, sides=0)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        new = _new_object(name + part, bm)
        new.matrix_world = obj.matrix_world.copy()
    bpy.data.objects.remove(obj, do_unlink=True)


def leg(name, parts, knee_z):
    """A leg in two parts that bend at the knee (see split_leg)."""
    split_leg(merge(name, parts), knee_z)


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
    meshfix.separate_coplanar(result)
    return _new_object(name, result)


def export(filename):
    bpy.ops.object.select_all(action="SELECT")
    meshfix.fix_objects(list(bpy.context.selected_objects), floor=category(filename) in ("Props", "Items", "Level"),
                        name=filename, pinned=meshfix.moving)
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

# Where a player's leg hangs from (x to the side, z up): the same in every style, so a style's leg mesh swings around
# the same hip as the original's (the game swaps meshes by name and keeps the pivots).
LEG_PIVOT = (0.11, 0.92)


def set_pivot(obj, point):
    """Moves the object's origin to `point` (world), leaving the geometry where it is."""
    import mathutils
    obj.data.transform(mathutils.Matrix.Translation((-point[0], -point[1], -point[2])))
    obj.location = point
    return obj


def model_player():
    merge("Body", [
        cylinder(0.28, 1.0, (0, 0, 0.75), segments=12),          # torso
        box((0.12, 0.12, 0.6), (-0.33, 0, 0.95)),                  # arms
        box((0.12, 0.12, 0.6), (0.33, 0, 0.95)),
    ])
    merge("Accent", [
        box((0.44, 0.16, 0.5), (0, 0.3, 0.95)),                     # backpack (behind, +Y)
    ])
    # Legs are separate parts hanging from the hip: the game swings them while walking (PlayerController).
    for name, x in (("BodyLegL", -LEG_PIVOT[0]), ("BodyLegR", LEG_PIVOT[0])):
        set_pivot(merge(name, [box((0.2, 0.22, 0.86), (x, 0, 0.43))]), (x, 0, LEG_PIVOT[1]))
    # Head parts are separate objects so the game can tilt them (look direction of teammates).
    merge("AccentHead", [box((0.36, 0.34, 0.34), (0, 0, 1.42))])
    merge("EyeGlasses", [box((0.3, 0.04, 0.09), (0, -0.18, 1.47))])  # glasses strip, front
    export("player")


def model_robot_guard():
    """Bipedal guard robot with a big camera eye. Height ~2.0m."""
    merge("Body", [
        box((0.7, 0.45, 0.8), (0, 0, 1.27)),                        # torso (up into the head)
        box((0.14, 0.14, 0.75), (-0.41, 0, 1.15)),                  # arms (into the torso sides)
        box((0.14, 0.14, 0.75), (0.41, 0, 1.15)),
    ])
    merge("Accent", [
        box((0.45, 0.4, 0.32), (0, 0, 1.82)),                       # head
        box((0.2, 0.2, 0.2), (-0.41, 0, 0.7)),                      # fists
        box((0.2, 0.2, 0.2), (0.41, 0, 0.7)),
    ])
    # Legs (with feet) in two parts: the game plants the feet and bends the knees.
    leg("AccentLeg0", [box((0.18, 0.18, 0.9), (-0.22, 0, 0.45)), box((0.26, 0.32, 0.08), (-0.22, -0.06, 0.04))], 0.45)
    leg("AccentLeg1", [box((0.18, 0.18, 0.9), (0.22, 0, 0.45)), box((0.26, 0.32, 0.08), (0.22, -0.06, 0.04))], 0.45)
    merge("Eye", [cylinder(0.11, 0.08, (0, -0.22, 1.84), axis="Y", segments=16)])
    export("robot_guard")


def model_robot_listener():
    """Wheeled blind robot with microphone dishes. Height ~1.2m."""
    merge("Body", [
        box((0.8, 1.0, 0.4), (0, 0, 0.42)),                         # chassis
        cylinder(0.08, 0.6, (0, 0.1, 0.9), segments=8),             # mast
        cylinder(0.03, 0.5, (0, 0.1, 1.2), axis="X", segments=8),   # cross bar holding the ear dishes
    ])
    merge("Accent", [box((0.6, 0.7, 0.05), (0, 0, 0.645))])                       # deck (accent colour)
    for i, (x, y) in enumerate([(-0.43, -0.3), (0.43, -0.3), (-0.43, 0.3), (0.43, 0.3)]):
        merge(f"AccentWheel{i}", [cylinder(0.22, 0.12, (x, y, 0.22), axis="X")])   # wheels roll in game, hubs in the chassis
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

# desks were low next to a 1.8 m person: the same 1.6 x 0.8 m, but the top at 0.918 m instead of 0.765 m (the player
# jumps 1.05 m: high vents are still climbed from a desk; Furnisher, LevelMetrics.DeskTop)
DESK_TOP, DESK_TOP_NEW = 0.765, 0.918


def raise_top(old_top, new_top):
    """Makes a desk taller: what is under its top (legs, panels, drawers) stretches, what stands on it moves up as is
    (the monitor keeps its shape)."""
    k = new_top / old_top
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        mw = obj.matrix_world
        inv = mw.inverted()
        for v in obj.data.vertices:
            w = mw @ v.co
            w.z = w.z * k if w.z <= old_top + 1e-4 else w.z + old_top * (k - 1)
            v.co = inv @ w


def model_desk():
    merge("Body", [box((1.6, 0.8, 0.05), (0, 0, 0.74))])
    merge("Accent", [
        box((0.05, 0.75, 0.72), (-0.76, 0, 0.36)),
        box((0.05, 0.75, 0.72), (0.76, 0, 0.36)),
        box((1.5, 0.03, 0.4), (0, 0.36, 0.52)),
    ])
    merge("Eye", [
        box((0.7, 0.05, 0.43), (0, 0.14, 1.19)),                    # monitor (a big one: the player is 1.8 m)
        box((0.07, 0.07, 0.24), (0, 0.19, 0.885)),                  # its stand, 1.5 cm into the monitor
    ])
    raise_top(DESK_TOP, DESK_TOP_NEW)
    export("desk")


def model_terminal():
    """Security terminal: the office desk (same footprint) turned into a console — three screens on the back edge,
    a keyboard, a computer under the top. The screens glow in the terminal's colour (cameras / hacking)."""
    merge("Body", [box((1.6, 0.8, 0.05), (0, 0, 0.74))])
    merge("Accent", [
        box((0.05, 0.75, 0.72), (-0.76, 0, 0.36)),
        box((0.05, 0.75, 0.72), (0.76, 0, 0.36)),
        box((1.5, 0.03, 0.4), (0, 0.36, 0.52)),
        box((0.22, 0.45, 0.42), (0.45, 0.05, 0.21)),                 # the computer under the top
    ])
    # the console on the top is a part of its own: its bounds stay within the screens (desk lamps stand beside them)
    merge("AccentConsole", [
        box((0.52, 0.17, 0.025), (0, -0.1, 0.778)),                  # keyboard
        box((0.06, 0.06, 0.14), (-0.32, 0.2, 0.83)), box((0.06, 0.06, 0.14), (0, 0.22, 0.83)),
        box((0.06, 0.06, 0.14), (0.32, 0.2, 0.83)),                  # screen stands (into the top and the screens)
    ])
    merge("Eye", [
        box((0.29, 0.03, 0.22), (-0.32, 0.17, 1.0)),
        box((0.3, 0.03, 0.24), (0, 0.19, 1.01)),
        box((0.29, 0.03, 0.22), (0.32, 0.17, 1.0)),
    ])
    raise_top(DESK_TOP, DESK_TOP_NEW)  # the office desk's height (a desk turned into a console)
    export("terminal")


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
    """A 42U rack 0.7 x 1.0 x 2.0: the cabinet, eleven servers in it (each a front plate with two handles and a vent
    strip), a row of status LEDs and a drive lamp on each instead of a glowing bar across."""
    units, lamps = [box((0.7, 1.0, 2.0), (0, 0, 1.0))], []      # (servers in Body: the styles' racks have no Accent part)
    for i in range(11):
        z = 0.2 + i * 0.16
        units.append(box((0.6, 0.02, 0.13), (0, -0.505, z)))
        units += [box((0.02, 0.025, 0.09), (x, -0.52, z)) for x in (-0.28, 0.28)]                 # handles
        units.append(box((0.3, 0.008, 0.05), (-0.06, -0.517, z)))                                   # vent strip
        lamps += [box((0.016, 0.006, 0.012), (0.12 + k * 0.03, -0.518, z + 0.035)) for k in range(4)]
        lamps.append(box((0.05, 0.006, 0.01), (0.17, -0.518, z - 0.035)))
    merge("Body", units)
    merge("Eye", lamps)
    export("server_rack")


def model_cabinet():
    """Office drawer cabinet 0.9 x 0.5 x 1.19 (waist-to-chest high next to a 1.8 m person): an open-fronted carcass and
    three drawers that slide out (separate parts BodyDrawer0..2, each a tray with its front and handle: the game moves
    them, Items.Storage)."""
    H, P = 1.19, 0.37                      # height, drawer pitch
    merge("Body", [box((0.02, 0.5, H), (x, 0, H / 2)) for x in (-0.44, 0.44)]          # sides
          + [box((0.9, 0.5, 0.02), (0, 0, H - 0.01)), box((0.9, 0.5, 0.06), (0, 0, 0.03)),  # top, plinth
             box((0.9, 0.02, H), (0, 0.24, H / 2))]                                    # back
          + [box((0.86, 0.48, 0.02), (0, 0.01, 0.06 + k * P)) for k in range(1, 3)])   # runners between the drawers
    for k in range(3):
        z0 = 0.07 + k * P
        merge(f"BodyDrawer{k}", [
            box((0.86, 0.02, P - 0.03), (0, -0.24, z0 + (P - 0.03) / 2)),    # front
            box((0.8, 0.4, 0.015), (0, -0.03, z0 + 0.02)),                   # bottom
            box((0.015, 0.4, 0.19), (-0.4, -0.03, z0 + 0.105)), box((0.015, 0.4, 0.19), (0.4, -0.03, z0 + 0.105)),  # sides
            box((0.8, 0.015, 0.19), (0, 0.17, z0 + 0.105)),                  # back
            box((0.3, 0.03, 0.04), (0, -0.265, z0 + P - 0.1)),               # handle
        ])
    export("cabinet")


def model_meeting_table():
    merge("Body", [box((3.2, 1.3, 0.06), (0, 0, 0.75))])
    merge("Accent", [
        box((0.3, 0.3, 0.72), (-1.1, 0, 0.36)),
        box((0.3, 0.3, 0.72), (1.1, 0, 0.36)),
    ])
    export("meeting_table")


def model_plant():
    """A potted shrub: a pot widening upwards with a rolled rim, a bushy crown of overlapping clumps over it."""
    merge("Accent", [cylinder(0.17, 0.42, (0, 0, 0.21), segments=16, radius2=0.21),
                     cylinder(0.225, 0.05, (0, 0, 0.43), segments=16)])
    merge("Body", [sphere(r, loc, segments=12) for r, loc in (
        (0.26, (0, 0, 0.72)), (0.22, (0.17, 0.05, 0.84)), (0.22, (-0.16, -0.04, 0.82)), (0.2, (0.03, 0.16, 0.88)),
        (0.2, (-0.02, -0.16, 0.9)), (0.24, (0, 0, 1.02)), (0.16, (0.1, -0.05, 1.16)))]
          + [cylinder(0.04, 0.3, (0, 0, 0.58), segments=8)])                                # the stem out of the pot
    export("plant")


def model_kitchen_counter():
    """Kitchen base cupboards 2.4 x 0.65 under a worktop at 0.93 with a microwave on it: an open carcass (a divider in
    the middle, a shelf) on a kick plinth and four doors that swing open in two pairs from their meeting edges
    (BodyDoor0..3, each with its pull: Items.Storage)."""
    merge("Body", [box((0.03, 0.63, 0.89), (x, 0.01, 0.445)) for x in (-1.185, 1.185)]      # sides (into the worktop), front edge y -0.305
          + [box((2.344, 0.6, 0.098), (0, 0.025, 0.051)),                                    # plinth and bottom
             box((2.344, 0.03, 0.776), (0, 0.307, 0.488)),                                   # back
             box((0.03, 0.601, 0.776), (0, -0.0005, 0.488)),                               # divider
             box((2.344, 0.55, 0.015), (0, 0.03, 0.46))])                                    # shelf
    for i, x in enumerate((-0.9, -0.3, 0.3, 0.9)):
        hx = x + (0.2 if i % 2 == 0 else -0.2)                                              # pull at the meeting edge
        merge(f"BodyDoor{i}", [box((0.592, 0.022, 0.774), (x, -0.314, 0.488)),
                               box((0.14, 0.025, 0.02), (hx, -0.332, 0.8))])
    merge("Accent", [box((2.44, 0.7, 0.05), (0, 0, 0.905))])
    # the microwave: a casing in the cupboards' colour on little feet, the glowing parts only its door window and display
    # (it was one glowing box)
    merge("Body", [box((0.5, 0.42, 0.3), (0.7, 0.01, 1.095)), box((0.46, 0.38, 0.02), (0.7, 0.01, 0.94))])
    merge("Eye", [box((0.3, 0.012, 0.19), (0.63, -0.205, 1.095)), box((0.08, 0.012, 0.035), (0.89, -0.205, 1.18))])
    merge("Accent", [box((0.02, 0.02, 0.2), (0.8, -0.215, 1.095))] + [box((0.03, 0.012, 0.03), (0.89, -0.207, z)) for z in (1.11, 1.06, 1.01)])
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
    # (the taps and the tray are part of Body: the styles' own coolers have only Body and Eye, a part of its own would show
    # through them)
    merge("Body", [box((0.35, 0.35, 1.0), (0, 0, 0.5))]
          + [box((0.03, 0.05, 0.06), (x, -0.195, 0.81)) for x in (-0.07, 0.07)]                 # hot and cold taps
          + [box((0.26, 0.06, 0.02), (0, -0.19, 0.58))])                                       # the drip tray
    # the bottle stands upside down: its neck in the cooler's top, the round bottom up
    merge("Eye", [cylinder(0.045, 0.062, (0, 0, 1.03), segments=12), cylinder(0.045, 0.06, (0, 0, 1.09), segments=12, radius2=0.15),
                  cylinder(0.15, 0.26, (0, 0, 1.25), segments=16), cylinder(0.15, 0.07, (0, 0, 1.415), segments=16, radius2=0.06)])
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
    ] + [box((0.05, 0.05, 0.12), (x, y, 0.06)) for x in (-0.5, 0.5) for y in (-0.7, 0.0)])   # the raised walls stand on feet
    # the toilet against the back wall: a foot, the bowl, the seat with its lid down, the cistern with a flush button
    merge("Accent", [cylinder(0.1, 0.26, (0, 0.42, 0.13), segments=14, radius2=0.12),
                     cylinder(0.12, 0.14, (0, 0.42, 0.32), segments=16, radius2=0.18),
                     box((0.38, 0.15, 0.34), (0, 0.655, 0.57)), box((0.06, 0.1, 0.1), (0, 0.57, 0.42))])
    merge("Body", [cylinder(0.185, 0.03, (0, 0.42, 0.404), segments=16), box((0.4, 0.17, 0.03), (0, 0.655, 0.755)),
                   cylinder(0.025, 0.012, (0, 0.655, 0.776), segments=10)])
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
    """Office copier 0.6 x 0.6 x 1.1: the cabinet with three paper drawers (handles), the scanner raised on a column at
    its right over the inner output tray (copies lying in it), a document feeder on the scanner and the control panel
    sticking out at its front right with a glowing screen. (Pieces inset 2 mm where they meet: no shared planes.)"""
    merge("Body", [box((0.6, 0.6, 0.85), (0, 0, 0.425)),                                    # cabinet with the engine
                   box((0.14, 0.596, 0.076), (0.229, 0, 0.884)),                             # the column under the scanner
                   box((0.6, 0.6, 0.1), (0, 0, 0.97)),                                       # the scanner
                   box((0.4, 0.36, 0.06), (-0.05, 0.05, 1.049)),                             # document feeder
                   box((0.2, 0.12, 0.04), (0.18, -0.33, 0.99))]                              # control panel
          + [box((0.56, 0.012, 0.16), (0, -0.304, z)) for z in (0.1, 0.28, 0.46)])          # drawer fronts
    merge("Accent", [box((0.18, 0.02, 0.022), (0, -0.316, z + 0.05)) for z in (0.1, 0.28, 0.46)]   # drawer handles
          + [box((0.3, 0.36, 0.018), (-0.1, -0.05, 0.859)),                                  # copies in the inner tray
             box((0.3, 0.2, 0.012), (-0.05, -0.04, 1.085))])                                 # the feeder's tray
    merge("Eye", [box((0.12, 0.07, 0.008), (0.18, -0.335, 1.013))])
    export("printer")


def model_fridge():
    """Fridge-freezer 0.7 x 0.7 x 1.8: a hollow cabinet (a divider at 1.05, shelves) and two doors that swing open
    (BodyDoor0 the fridge below, BodyDoor1 the freezer above), each with its bar handle; door bins on the fridge door.
    A lone door hangs on its Blender +X edge (Items.StorageMath.Hinge; Unity mirrors X), so the handles are at -X."""
    # boards between the sides stop 2 mm short of the outer faces: no two faces share a plane (meshfix would grow them)
    merge("Body", [box((0.04, 0.65, 1.8), (x, 0.025, 0.9)) for x in (-0.33, 0.33)]           # sides, front edge y -0.3
          + [box((0.624, 0.645, 0.038), (0, 0.0255, 1.779)),                                 # top
             box((0.624, 0.645, 0.078), (0, 0.0255, 0.041)),                                 # bottom
             box((0.624, 0.038, 1.71), (0, 0.327, 0.915)),                                   # back
             box((0.624, 0.62, 0.04), (0, 0.016, 1.05))]                                     # fridge / freezer divider
          + [box((0.624, 0.55, 0.015), (0, 0.05, z)) for z in (0.42, 0.74, 1.42)])           # shelves
    merge("BodyDoor0", [box((0.696, 0.052, 1.035), (0, -0.324, 0.5275)),                    # fridge door
                        box((0.04, 0.06, 0.35), (-0.28, -0.365, 0.6))]                       # handle, 15 mm into the door
          + [box((0.54, 0.05, 0.08), (0, -0.278, z)) for z in (0.3, 0.62)])                  # door bins (inside)
    merge("BodyDoor1", [box((0.696, 0.052, 0.74), (0, -0.324, 1.425)),                      # freezer door
                        box((0.04, 0.06, 0.5), (-0.28, -0.365, 1.35))])
    export("fridge")


def model_coffee_machine():
    """Espresso machine on a counter (0.93 m): 0.35 x 0.4 x 0.5 — a plinth, a back tower and a hood over a recessed brewing
    bay; under the hood the group head with two spouts over a cup on the grated drip tray, a display and buttons on the
    hood's front. (Boards inset 2 mm where they meet: no two faces share a plane.)"""
    merge("Body", [box((0.35, 0.4, 0.06), (0, 0, 0.03)),                                    # plinth
                   box((0.346, 0.216, 0.346), (0, 0.09, 0.229)),                             # the back tower
                   box((0.35, 0.4, 0.1), (0, 0, 0.45)),                                      # the hood
                   cylinder(0.034, 0.07, (0, -0.1, 0.115), segments=14)])                    # the cup
    merge("Accent", [box((0.24, 0.17, 0.016), (0, -0.105, 0.068)),                         # drip tray
                     cylinder(0.04, 0.045, (0, -0.1, 0.3775), segments=16),                  # group head
                     cylinder(0.007, 0.03, (-0.016, -0.1, 0.34), segments=8),
                     cylinder(0.007, 0.03, (0.016, -0.1, 0.34), segments=8)]
           + [box((0.22, 0.012, 0.004), (0, -0.105 + k * 0.035, 0.078)) for k in (-2, -1, 0, 1, 2)]     # the tray's grate
           + [box((0.03, 0.012, 0.022), (x, -0.206, 0.45)) for x in (-0.12, -0.075)])      # buttons
    merge("Eye", [box((0.12, 0.01, 0.045), (0.07, -0.204, 0.45))])
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
    """Low cabinet with a big screen on it, 1.4 wide: an open carcass with a divider and two inset doors that open from
    the middle (BodyDoor0..1, each with its pull: Items.Storage)."""
    merge("Body", [box((1.4, 0.4, 0.03), (0, 0, 0.485)),                                     # top
                   box((0.03, 0.4, 0.472), (-0.685, 0, 0.236)), box((0.03, 0.4, 0.472), (0.685, 0, 0.236)),  # sides
                   box((1.344, 0.4, 0.06), (0, 0, 0.03)),                                    # plinth
                   box((1.344, 0.02, 0.412), (0, 0.188, 0.265)),                             # back
                   box((0.02, 0.35, 0.412), (0, 0.01, 0.265))])                              # divider
    for i, sx in enumerate((-1, 1)):
        merge(f"BodyDoor{i}", [box((0.666, 0.02, 0.404), (sx * 0.335, -0.18, 0.266)),          # inset door
                               box((0.02, 0.02, 0.12), (sx * 0.05, -0.189, 0.3))])            # pull at the meeting edge
    merge("Accent", [box((1.36, 0.06, 0.8), (0, 0.08, 1.05)), box((0.3, 0.2, 0.05), (0, 0.08, 0.525)), box((0.06, 0.06, 0.15), (0, 0.1, 0.6))])
    merge("Eye", [box((1.28, 0.02, 0.72), (0, 0.045, 1.05))])
    export("tv_stand")


def model_reception_desk():
    """Front counter 2.4 x 0.8: high front panel towards the visitor (-Y), work top behind."""
    # the sides start inside the front panel, a hair within its outline: flush with it, their faces met its in one plane
    # (the styles' bevels made them flicker)
    merge("Body", [box((2.4, 0.08, 1.1), (0, -0.36, 0.55)), box((0.078, 0.73, 1.096), (-1.159, 0.035, 0.548)),
                   box((0.078, 0.73, 1.096), (1.159, 0.035, 0.548))])
    merge("Accent", [box((2.5, 0.3, 0.04), (0, -0.33, 1.12)), box((2.3, 0.7, 0.04), (0, 0.02, 0.74))])
    merge("Eye", [box((0.62, 0.05, 0.4), (0.4, 0.15, 1.0)), box((0.07, 0.07, 0.2), (0.4, 0.2, 0.84))])
    raise_top(0.76, DESK_TOP_NEW)  # the work top at the office desk's height
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
    """Staff lockers, 1.0 x 0.5 x 2.05 (a head above a 1.8 m person): an open-fronted carcass with two compartments
    0.47 m wide inside (any loot item stands in one on end: Items.StorageMath.LootPose; a hat shelf in each) and two
    doors that swing open (separate parts BodyDoor0..1 with their vents and handle: the game moves them, Items.Storage)."""
    W, H, C = 1.0, 2.05, 0.49              # width, height, compartment pitch
    xs = (-C / 2, C / 2)
    merge("Body", [box((0.02, 0.5, H), (x, 0, H / 2)) for x in (-W / 2 + 0.01, 0.0, W / 2 - 0.01)]  # sides and the divider
          + [box((W, 0.5, 0.02), (0, 0, H - 0.01)), box((W, 0.5, 0.06), (0, 0, 0.03)),     # top, plinth
             box((W, 0.02, H), (0, 0.24, H / 2))]                                         # back
          + [box((C - 0.03, 0.46, 0.02), (x, 0.01, H - 0.38)) for x in xs])              # hat shelves
    for i, x in enumerate(xs):
        merge(f"BodyDoor{i}", [box((C - 0.02, 0.02, H - 0.08), (x, -0.245, H / 2 + 0.02))]  # the door
              + [box((0.32, 0.012, 0.02), (x, -0.258, H - 0.17 - k * 0.04)) for k in range(3)]  # vents
              + [box((0.03, 0.03, 0.12), (x + C / 2 - 0.05, -0.265, 1.05))])                    # handle
    export("lockers")


def model_pallet():
    """Warehouse pallet 1.2 x 1.0 with a wrapped stack of boxes."""
    merge("Accent", [box((1.2, 1.0, 0.04), (0, 0, 0.13))] + [box((1.2, 0.1, 0.1), (0, y, 0.05)) for y in (-0.45, 0, 0.45)])
    merge("Body", [box((0.55, 0.45, 0.4), (x, y, 0.35)) for x in (-0.3, 0.3) for y in (-0.24, 0.24)]
          + [box((0.55, 0.45, 0.4), (-0.3, y, 0.75)) for y in (-0.24, 0.24)])
    export("pallet")


def model_wash_basin():
    """Restroom counter 1.6 x 0.5 with two sinks and a mirror on the wall behind (+Y)."""
    merge("Body", [box((1.6, 0.5, 0.06), (0, 0, 0.85)), box((1.6, 0.45, 0.8), (0, 0.02, 0.4)),
                   box((1.6, 0.03, 0.4), (0, 0.24, 1.06))])                # backsplash up to the mirror
    merge("Accent", [cylinder(0.17, 0.08, (x, -0.02, 0.86), segments=14) for x in (-0.4, 0.4)]
          + [box((0.04, 0.12, 0.15), (x, 0.17, 0.95)) for x in (-0.4, 0.4)])
    merge("Eye", [box((1.5, 0.02, 0.8), (0, 0.235, 1.55))])
    export("wash_basin")


def model_vending_machine():
    merge("Body", [box((0.9, 0.8, 1.9), (0, 0, 0.95))])
    # the glass in a frame, the side panel with a keypad (3 x 4 keys), a coin slot and a note slot, the pickup flap below
    frame = [box((0.66, 0.03, 0.03), (-0.1, -0.413, z)) for z in (0.585, 1.715)]
    frame += [box((0.03, 0.03, 1.1), (x, -0.413, 1.15)) for x in (-0.415, 0.215)]
    keys = [box((0.025, 0.012, 0.02), (0.3 + c * 0.035, -0.418, 1.3 - r * 0.032)) for r in range(4) for c in range(3)]
    merge("Accent", frame + keys + [box((0.6, 0.02, 0.15), (-0.1, -0.41, 0.25)), box((0.12, 0.02, 0.5), (0.335, -0.41, 1.2)),
                                    box((0.3, 0.03, 0.03), (-0.1, -0.425, 0.31)),            # the flap's handle
                                    box((0.012, 0.012, 0.05), (0.31, -0.418, 1.0)), box((0.07, 0.012, 0.012), (0.355, -0.418, 1.02))])
    merge("Eye", [box((0.6, 0.02, 1.1), (-0.1, -0.405, 1.15)), box((0.1, 0.012, 0.05), (0.335, -0.418, 1.39))])  # glass, display
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
    "shovel": [(0, 0), (0, 1), (0, 2), (0, 3)],                    # the shovel's piece in grids and the cart (ItemCatalog.Shovel, 1 x 4)
    "flashlight": [(0, 0), (0, 1), (0, 2)],                       # ItemCatalog.Flashlight (1 x 3)
    "battery": [(0, 0)],                                          # ItemCatalog.Battery
    "book": [(0, 0), (1, 0), (0, 1), (1, 1)],                      # book, shape 16 (2 x 2)
    "cans": [(0, 0), (1, 0), (0, 1), (1, 1)],                      # tins, shape 17 (2 x 2)
    "screwdriver": [(0, 0), (0, 1), (0, 2)],                      # screwdriver, shape 18 (1 x 3)
    "beltbag": [(0, 0), (1, 0), (0, 1), (1, 1)],                   # belt bag, shape 19 (2 x 2)
    # more pickup items (their world models: items_more.py)
    "radio": [(0, 0), (0, 1)],                                    # walkie-talkie (1 x 2)
    "keyboard": [(0, 0), (1, 0), (2, 0)],                         # keyboard (3 x 1)
    "tablet": [(0, 0), (1, 0), (0, 1), (1, 1)],                    # rugged tablet (2 x 2)
    "medkit": [(0, 0), (1, 0), (0, 1), (1, 1)],                    # first-aid box (2 x 2)
    "coffee": [(0, 0)],                                           # jar of coffee
    "finder": [(0, 0), (0, 1)],                                   # fuel finder (1 x 2)
    "boots": [(0, 0), (1, 0), (0, 1), (1, 1)],                     # pair of boots (2 x 2)
    "tomatoes": [(0, 0)],                                         # tin of tomatoes
    "chess": [(0, 0), (1, 0), (0, 1), (1, 1)],                     # folded chessboard (2 x 2)
    "magazine": [(0, 0), (1, 0), (0, 1), (1, 1)],                  # magazine (2 x 2)
    "pills": [(0, 0)],                                            # pills
    "toothpaste": [(0, 0), (0, 1)],                               # toothpaste (1 x 2)
    "tape": [(0, 0)],                                             # duct tape
    "matches": [(0, 0)],                                          # matchbox
    "toiletpaper": [(0, 0)],                                      # toilet roll
    "applecore": [(0, 0)],                                        # apple core
    "soap": [(0, 0)],                                             # soap
    "gloves": [(0, 0), (0, 1)],                                   # work gloves (1 x 2)
    "necklace": [(0, 0), (1, 0)],                                 # necklace (2 x 1)
    "mop": [(0, 0), (0, 1), (0, 2), (0, 3)],                      # mop (1 x 4)
    "plush": [(0, 0), (1, 0), (0, 1)],                             # teddy bear (L of 3)
    "socks": [(0, 0), (0, 1)],                                    # rolled socks (1 x 2)
    "ssd": [(0, 0)],                                              # SSD drive
    "gamepad": [(0, 0), (1, 0)],                                  # game controller (2 x 1)
    "gamekeyboard": [(0, 0), (1, 0), (2, 0)],                     # gaming keyboard (3 x 1)
    "canister": [(0, 0), (1, 0), (0, 1), (1, 1)],                  # fuel canister (2 x 2)
    "bandage": [(0, 0)],                                          # roll of bandages
}


def model_items():
    c = ITEM_CELL
    for name, cells in SHAPES.items():
        cx = (min(x for x, _ in cells) + max(x for x, _ in cells)) / 2.0
        cy = (min(y for _, y in cells) + max(y for _, y in cells)) / 2.0
        # Shape lies flat on the ground: grid X -> Blender X, grid Y -> Blender -Y (Unity +Z).
        pos = lambda x, y: ((x - cx) * c, -(y - cy) * c)  # noqa: E731
        parts = [box((c * 0.96, c * 0.96, c * 0.96), (*pos(x, y), c / 2)) for x, y in cells]
        # neighbouring cells are joined inside by a bridge (the shape is one piece, the cell gaps stay; the bridge
        # stays within the two cells, so the cart still shows the item on its own cells only)
        for x, y in cells:
            for nx, ny in ((x + 1, y), (x, y + 1)):
                if (nx, ny) in cells:
                    (ax, ay), (bx, by) = pos(x, y), pos(nx, ny)
                    size = (abs(bx - ax) + c * 0.5, abs(by - ay) + c * 0.5, c * 0.5)
                    parts.append(box(size, ((ax + bx) / 2, (ay + by) / 2, c / 2)))
        merge("Body", parts)
        export("item_" + name)


def model_robot_sweeper():
    """Floor cleaner on three wheels, ~0.55 m high. Its lidar puck sits under the body at ankle height (spins in game)."""
    merge("Body", [cylinder(0.38, 0.22, (0, 0, 0.44), segments=24), sphere(0.2, (0, 0.05, 0.56), segments=14)])
    wheels = [(-0.28, 0.18), (0.28, 0.18), (0.0, -0.3)]
    for i, (x, y) in enumerate(wheels):
        merge(f"AccentWheel{i}", [cylinder(0.08, 0.06, (x, y, 0.08), axis="X", segments=12)])
    merge("Accent", [cylinder(0.05, 0.16, (0, 0, 0.28), segments=10),                      # stem to the lidar
                     cylinder(0.14, 0.012, (-0.27, -0.24, 0.012), segments=12),            # side brushes
                     cylinder(0.14, 0.012, (0.27, -0.24, 0.012), segments=12),
                     cylinder(0.015, 0.33, (-0.27, -0.24, 0.175), segments=8),             # their stems up to the body
                     cylinder(0.015, 0.33, (0.27, -0.24, 0.175), segments=8)]
          + [box((0.025, 0.05, 0.27), (x + s * 0.04, y, 0.2)) for x, y in wheels for s in (-1, 1)])  # wheel forks
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
    model_terminal()
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
