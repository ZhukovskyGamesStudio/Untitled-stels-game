"""The guard robot designed anew for every visual style (it is the robot of the comparison shots).

Run:  blender.exe -b --factory-startup --python Tools/Blender/guards.py -- Assets/_Game/Art/Models [S11_Derelict ...]

Writes Art/Models/Styles/Guards/robot_guard__<StyleAsset>.fbx. Every design keeps the parts of the original guard so
the game swaps them in by name and its animation keeps working:
  Body        torso and arms (robot body colour)
  Accent      head, hands, details (accent colour)
  AccentLeg0  left leg with its foot, hanging from the hip at x = -0.22, z ~ 0.9 (it swings from there)
  AccentLeg1  right leg, x = +0.22
  Eye         the lenses / visor / glowing seams (they glow in the status colour)
The head sits around z = 1.84 with its face towards -Y (Unity +Z), where the camera-eye module is mounted.

Each design is finished in the language of its style's model family (see style_families.py): low-poly facets, toy
roundness, PS1 grid snapping, clean chamfers, realistic panels.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
OUT = os.path.join(ROOT, "Styles", "Guards")
FINISH = "clean"


# --------------------------------------------------------------------------- primitives (Blender: x right, -y front, z up)

def box(sx, sy, sz, x=0.0, y=0.0, z=0.0, rx=0.0, ry=0.0, rz=0.0, taper=1.0):
    """Box of size sx, sy, sz centred at x, y, z; taper scales the top face; rx/ry/rz rotate it (radians)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((sx, sy, sz)), verts=bm.verts)
    if taper != 1.0:
        for v in bm.verts:
            if v.co.z > 0:
                v.co.x *= taper
                v.co.y *= taper
    rot(bm, rx, ry, rz)
    bmesh.ops.translate(bm, vec=Vector((x, y, z)), verts=bm.verts)
    return bm


def cyl(r, depth, x=0.0, y=0.0, z=0.0, axis="Z", seg=16, r2=None, rx=0.0, ry=0.0, rz=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=depth)
    if axis == "X":
        rot(bm, 0, math.pi / 2, 0)
    elif axis == "Y":
        rot(bm, math.pi / 2, 0, 0)
    rot(bm, rx, ry, rz)
    bmesh.ops.translate(bm, vec=Vector((x, y, z)), verts=bm.verts)
    return bm


def sph(r, x=0.0, y=0.0, z=0.0, seg=16, sx=1.0, sy=1.0, sz=1.0, ico=False):
    bm = bmesh.new()
    if ico:
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)
    else:
        bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
    bmesh.ops.scale(bm, vec=Vector((sx, sy, sz)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((x, y, z)), verts=bm.verts)
    return bm


def capsule(r, length, x=0.0, y=0.0, z=0.0, seg=16, axis="Z"):
    """A rounded stick: cylinder with half-spheres (length = end to end)."""
    body = max(0.001, length - 2 * r)
    parts = [cyl(r, body, x, y, z, axis, seg)]
    d = {"Z": Vector((0, 0, body / 2)), "X": Vector((body / 2, 0, 0)), "Y": Vector((0, body / 2, 0))}[axis]
    parts += [sph(r, x + d.x, y + d.y, z + d.z, seg), sph(r, x - d.x, y - d.y, z - d.z, seg)]
    return parts


def rot(bm, rx, ry, rz):
    for a, ax in ((rx, "X"), (ry, "Y"), (rz, "Z")):
        if a:
            bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(a, 3, ax), verts=bm.verts)


def limb(a, b, r, seg=10, square=False):
    """A rod from point a to point b (thigh, arm, cane)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = box(r * 2, r * 2, d.length) if square else cyl(r, d.length, seg=seg)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    return bm


# --------------------------------------------------------------------------- finishing per family

def finish(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)

    def bevel(offset, segments, angle=30):
        edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(angle)]
        if edges:
            bmesh.ops.bevel(bm, geom=edges, offset=offset, offset_type="OFFSET", segments=segments, profile=0.5,
                            affect="EDGES", clamp_overlap=True)

    smooth = False
    if FINISH == "lowpoly":
        bevel(0.02, 1)
        off = Vector((3.1, 7.7, 1.3))
        for v in bm.verts:
            v.co += noise.noise_vector(v.co * 3.0 + off) * 0.012
    elif FINISH == "crystal":
        off = Vector((5.3, 2.9, 4.1))
        for v in bm.verts:
            v.co += noise.noise_vector(v.co * 4.0 + off) * 0.02
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
    elif FINISH == "toy":
        bevel(0.035, 4)
        smooth = True
    elif FINISH == "lofi":
        for v in bm.verts:
            v.co = Vector(round(c / 0.02) * 0.02 for c in v.co)
    elif FINISH == "clean":
        bevel(0.008, 1)
        smooth = True
    elif FINISH == "realistic":
        big = [f for f in bm.faces if f.calc_area() > 0.02 and len(f.verts) == 4]
        if big:
            bmesh.ops.inset_individual(bm, faces=big, thickness=0.018, depth=0.0)
            bmesh.ops.inset_individual(bm, faces=big, thickness=0.005, depth=-0.004)
        bevel(0.005, 2)
        smooth = True
    bm.to_mesh(obj.data)
    bm.free()
    for p in obj.data.polygons:
        p.use_smooth = smooth
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.scene.objects:
        o.select_set(o == obj)
    if smooth and FINISH != "toy":
        try:
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
        except Exception:  # noqa: BLE001
            pass
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=False, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def merge(name, parts):
    flat = []
    for p in parts:
        flat.extend(p if isinstance(p, list) else [p])
    obj = g.merge(name, flat)
    finish(obj)
    return obj


def export(style):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"robot_guard__{style}.fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             object_types={"MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False)
    g.clear_scene()


def legs(make):
    """Both legs from one function of the side (-1 left, +1 right), each cut at the knee into Upper and Lower."""
    g.split_leg(merge("AccentLeg0", make(-1)), KNEE)
    g.split_leg(merge("AccentLeg1", make(1)), KNEE)


KNEE = 0.45  # every design: hip ~0.9 m, knee at half of it, like the original guard


# --------------------------------------------------------------------------- the designs

def night_shift():
    """Lethal Company: a hunched industrial security mech — hazard-striped chest, one big headlamp eye, piston legs."""
    stripes = [box(0.09, 0.03, 0.5, x=-0.27 + i * 0.135, y=-0.29, z=1.28, ry=0.5) for i in range(5)]
    merge("Body", [box(0.78, 0.55, 0.62, z=1.3, taper=0.88),                                     # chest crate
                   box(0.6, 0.45, 0.25, z=0.95),                                                 # pelvis
                   box(0.18, 0.2, 0.5, x=-0.5, z=1.3), box(0.18, 0.2, 0.5, x=0.5, z=1.3),         # upper arms
                   box(0.16, 0.18, 0.5, x=-0.52, y=-0.1, z=0.9), box(0.16, 0.18, 0.5, x=0.52, y=-0.1, z=0.9),
                   box(0.5, 0.25, 0.4, y=0.36, z=1.35)] + stripes)                               # back battery
    merge("Accent", [box(0.42, 0.42, 0.3, y=-0.05, z=1.8, taper=0.85),                           # squat head
                     cyl(0.16, 0.08, y=-0.27, z=1.82, axis="Y", seg=8),                          # lamp rim
                     box(0.24, 0.24, 0.2, x=-0.53, y=-0.15, z=0.6), box(0.24, 0.24, 0.2, x=0.53, y=-0.15, z=0.6),  # claws
                     box(0.06, 0.06, 0.3, x=0.18, y=0.05, z=2.0), cyl(0.04, 0.12, x=-0.5, y=0.36, z=1.62, seg=6)])  # antenna, exhaust
    merge("Eye", [cyl(0.12, 0.06, y=-0.31, z=1.82, axis="Y", seg=8)])
    legs(lambda s: [box(0.2, 0.22, 0.45, x=s * 0.22, z=0.66), box(0.14, 0.16, 0.45, x=s * 0.22, y=0.04, z=0.25),
                    box(0.3, 0.36, 0.08, x=s * 0.22, y=-0.06, z=0.04)])
    export("S01_NightShift")


def scrapyard():
    """R.E.P.O.: a round little trash-can robot with huge eyes, stubby arms and feet — a toy."""
    merge("Body", [cyl(0.36, 0.7, z=1.25, seg=24), sph(0.36, z=0.9, seg=24, sz=0.5),
                   capsule(0.09, 0.42, x=-0.44, z=1.25), capsule(0.09, 0.42, x=0.44, z=1.25)])
    merge("Accent", [sph(0.36, z=1.62, seg=24, sz=0.55), cyl(0.38, 0.08, z=1.6, seg=24),            # lid
                     sph(0.12, x=-0.46, y=-0.04, z=0.98, seg=16), sph(0.12, x=0.46, y=-0.04, z=0.98, seg=16),  # mitts
                     cyl(0.03, 0.25, x=0.15, z=1.95, seg=8), sph(0.06, x=0.15, z=2.08, seg=12)])     # antenna bobble
    merge("Eye", [sph(0.14, x=-0.15, y=-0.31, z=1.84, seg=20, sy=0.6), sph(0.14, x=0.15, y=-0.31, z=1.84, seg=20, sy=0.6)])
    legs(lambda s: capsule(0.11, 0.6, x=s * 0.2, z=0.55) + [sph(0.16, x=s * 0.2, y=-0.06, z=0.1, seg=16, sy=1.4, sz=0.6)])
    export("S02_Scrapyard")


def heist():
    """PAYDAY: an armoured security android — chest plate, shoulder pads, ribbed abdomen, helmet with a visor slit."""
    ribs = [box(0.42, 0.3, 0.05, z=1.0 + i * 0.07) for i in range(4)]
    merge("Body", [box(0.6, 0.36, 0.42, z=1.42, taper=1.08),                                      # chest
                   box(0.46, 0.3, 0.3, z=1.05), box(0.38, 0.26, 0.18, z=0.92)] + ribs + [         # waist, hips
                   limb((-0.38, 0, 1.5), (-0.44, -0.04, 1.14), 0.07), limb((0.38, 0, 1.5), (0.44, -0.04, 1.14), 0.07),
                   limb((-0.44, -0.04, 1.12), (-0.42, -0.14, 0.82), 0.06), limb((0.44, -0.04, 1.12), (0.42, -0.14, 0.82), 0.06)])
    merge("Accent", [box(0.62, 0.4, 0.2, y=-0.02, z=1.55),                                        # chest plate
                     box(0.24, 0.3, 0.14, x=-0.4, z=1.6, ry=-0.3), box(0.24, 0.3, 0.14, x=0.4, z=1.6, ry=0.3),  # pauldrons
                     box(0.32, 0.36, 0.36, y=-0.02, z=1.86, taper=0.9),                          # helmet
                     box(0.36, 0.06, 0.08, y=0.15, z=1.94), cyl(0.07, 0.1, z=1.7, seg=16),        # crest, neck
                     box(0.11, 0.13, 0.2, x=-0.42, y=-0.14, z=0.74), box(0.11, 0.13, 0.2, x=0.42, y=-0.14, z=0.74),  # gloves
                     box(0.12, 0.16, 0.24, x=-0.45, y=-0.06, z=0.98), box(0.12, 0.16, 0.24, x=0.45, y=-0.06, z=0.98)])  # forearm guards
    merge("Eye", [box(0.26, 0.04, 0.05, y=-0.2, z=1.88)])
    legs(lambda s: [limb((s * 0.2, 0, 0.9), (s * 0.21, -0.03, 0.5), 0.085), box(0.15, 0.12, 0.14, x=s * 0.21, y=-0.08, z=0.5),
                    limb((s * 0.21, -0.03, 0.5), (s * 0.22, 0.02, 0.12), 0.07), box(0.17, 0.32, 0.12, x=s * 0.22, y=-0.06, z=0.06)])
    export("S03_Heist")


def hunted():
    """Sir, You Are Being Hunted: a lanky gentleman robot — tweed jacket, stilt legs, round dark head with a bowler, a cane."""
    merge("Body", [box(0.5, 0.36, 0.62, z=1.28, taper=1.12),                                       # jacket
                   box(0.52, 0.3, 0.12, y=-0.02, z=0.98),                                         # jacket hem
                   limb((-0.3, 0, 1.52), (-0.36, -0.1, 1.05), 0.045), limb((0.3, 0, 1.52), (0.38, -0.12, 1.08), 0.045),
                   limb((-0.36, -0.1, 1.05), (-0.34, -0.2, 0.7), 0.04), limb((0.38, -0.12, 1.08), (0.46, -0.32, 0.86), 0.04)])
    merge("Accent", [sph(0.2, z=1.8, seg=20),                                                      # dark round head
                     cyl(0.26, 0.02, z=1.95, seg=20), cyl(0.16, 0.14, z=2.03, seg=20, r2=0.15),  # bowler
                     box(0.1, 0.06, 0.22, x=-0.1, y=-0.18, z=1.42, rz=0.3), box(0.1, 0.06, 0.22, x=0.1, y=-0.18, z=1.42, rz=-0.3),  # lapels
                     limb((0.46, -0.32, 0.86), (0.6, -0.42, 0.02), 0.018),                       # cane
                     sph(0.045, x=-0.34, y=-0.2, z=0.68, seg=10), sph(0.045, x=0.46, y=-0.32, z=0.86, seg=10)])
    merge("Eye", [sph(0.035, x=-0.07, y=-0.18, z=1.83, seg=10), sph(0.035, x=0.07, y=-0.18, z=1.83, seg=10)])
    legs(lambda s: [limb((s * 0.13, 0, 0.95), (s * 0.15, 0, 0.06), 0.032), box(0.1, 0.24, 0.05, x=s * 0.15, y=-0.07, z=0.03)])
    export("S04_Hunted")


def toon():
    """Risk of Rain 2: a stone-golem guard — huge shoulders and fists, tiny head with one eye slit, short thick legs."""
    merge("Body", [box(0.9, 0.55, 0.5, z=1.45, taper=1.15),                                        # shoulders
                   box(0.6, 0.45, 0.4, z=1.05, taper=1.2),                                        # gut
                   box(0.28, 0.3, 0.55, x=-0.62, y=-0.05, z=1.2, ry=0.15), box(0.28, 0.3, 0.55, x=0.62, y=-0.05, z=1.2, ry=-0.15)])
    merge("Accent", [box(0.36, 0.3, 0.3, y=-0.06, z=1.82, taper=0.8),                               # small head
                     box(0.38, 0.42, 0.36, x=-0.66, y=-0.1, z=0.78), box(0.38, 0.42, 0.36, x=0.66, y=-0.1, z=0.78),  # fists
                     box(0.2, 0.2, 0.2, x=-0.3, y=0.12, z=1.76, rx=0.4), box(0.2, 0.2, 0.2, x=0.3, y=0.12, z=1.76, rx=0.4)])  # shoulder rocks
    merge("Eye", [box(0.24, 0.05, 0.05, y=-0.22, z=1.84)])
    legs(lambda s: [box(0.26, 0.3, 0.78, x=s * 0.22, z=0.52, taper=1.2), box(0.34, 0.42, 0.14, x=s * 0.22, y=-0.05, z=0.07)])
    export("S05_Toon")


def storybook():
    """Blue Prince: an art-deco doorman automaton — fluted column body, round face plate with a monocle, bell-hop cap."""
    flutes = [cyl(0.03, 0.62, x=0.27 * math.cos(a), y=0.2 * math.sin(a), z=1.3, seg=8) for a in
              [math.pi * (0.55 + 0.1 * i) for i in range(10)]]
    merge("Body", [cyl(0.28, 0.66, z=1.3, seg=24, r2=0.24), cyl(0.3, 0.06, z=0.98, seg=24), cyl(0.27, 0.06, z=1.63, seg=24)] + flutes + [
                   limb((-0.3, 0, 1.55), (-0.36, -0.05, 1.1), 0.04), limb((0.3, 0, 1.55), (0.36, -0.05, 1.1), 0.04),
                   limb((-0.36, -0.05, 1.1), (-0.3, -0.16, 0.86), 0.035), limb((0.36, -0.05, 1.1), (0.3, -0.16, 0.86), 0.035)])
    merge("Accent", [cyl(0.19, 0.3, z=1.84, seg=24), cyl(0.2, 0.04, y=-0.15, z=1.84, axis="Y", seg=24),   # head and face plate
                     cyl(0.2, 0.12, z=2.03, seg=24), cyl(0.22, 0.02, y=-0.02, z=1.97, seg=24),             # cap and its band
                     cyl(0.07, 0.06, z=1.68, seg=16),
                     sph(0.05, x=-0.3, y=-0.17, z=0.84, seg=12), sph(0.05, x=0.3, y=-0.17, z=0.84, seg=12)])
    merge("Eye", [cyl(0.07, 0.03, x=0.07, y=-0.19, z=1.86, axis="Y", seg=20)])
    legs(lambda s: [limb((s * 0.12, 0, 0.96), (s * 0.13, 0, 0.12), 0.05), box(0.12, 0.28, 0.08, x=s * 0.13, y=-0.06, z=0.04),
                    cyl(0.065, 0.1, x=s * 0.13, z=0.16, seg=16)])
    export("S06_Storybook")


def logbook():
    """Return of the Obra Dinn: a ship's diving automaton — brass helmet with a porthole, barrel chest, lead boots."""
    bolts = [sph(0.025, x=0.3 * math.cos(a), y=0.3 * math.sin(a), z=1.62, seg=8) for a in [i * math.pi / 6 for i in range(12)]]
    merge("Body", [cyl(0.34, 0.6, z=1.27, seg=20, r2=0.3), cyl(0.3, 0.2, z=0.93, seg=20)] + [
                   limb((-0.36, 0, 1.45), (-0.42, -0.06, 1.05), 0.08), limb((0.36, 0, 1.45), (0.42, -0.06, 1.05), 0.08),
                   limb((-0.42, -0.06, 1.05), (-0.4, -0.14, 0.8), 0.075), limb((0.42, -0.06, 1.05), (0.4, -0.14, 0.8), 0.075)])
    merge("Accent", [sph(0.26, z=1.84, seg=24), cyl(0.33, 0.06, z=1.62, seg=24)] + bolts + [            # helmet, collar ring
                     cyl(0.11, 0.06, y=-0.23, z=1.85, axis="Y", seg=20),                                # porthole rim
                     cyl(0.07, 0.08, x=-0.22, z=1.86, axis="X", seg=16), cyl(0.07, 0.08, x=0.22, z=1.86, axis="X", seg=16),
                     sph(0.1, x=-0.4, y=-0.16, z=0.76, seg=12), sph(0.1, x=0.4, y=-0.16, z=0.76, seg=12)])
    merge("Eye", [cyl(0.085, 0.03, y=-0.265, z=1.85, axis="Y", seg=20)])
    legs(lambda s: [limb((s * 0.18, 0, 0.9), (s * 0.19, 0, 0.18), 0.09), box(0.22, 0.34, 0.14, x=s * 0.19, y=-0.05, z=0.07)])
    export("S07_Logbook")


def footage():
    """Content Warning: a camera-man robot — a big camcorder for a head, rounded body, a tripod-like stance."""
    merge("Body", [box(0.56, 0.38, 0.6, z=1.25), box(0.46, 0.32, 0.22, z=0.94)] +
          capsule(0.075, 0.5, x=-0.38, z=1.22) + capsule(0.075, 0.5, x=0.38, z=1.22))
    merge("Accent", [box(0.3, 0.52, 0.3, y=0.02, z=1.83),                                          # camcorder body
                     cyl(0.13, 0.22, y=-0.3, z=1.83, axis="Y", seg=20, r2=0.15),                    # lens barrel
                     box(0.06, 0.18, 0.12, x=0.18, y=0.05, z=1.86),                                 # side screen
                     cyl(0.05, 0.1, z=1.65, seg=12), box(0.1, 0.12, 0.08, y=0.1, z=2.02),           # neck, top handle
                     sph(0.09, x=-0.4, y=-0.04, z=0.92, seg=12), sph(0.09, x=0.4, y=-0.04, z=0.92, seg=12)])
    merge("Eye", [cyl(0.11, 0.03, y=-0.42, z=1.83, axis="Y", seg=20), sph(0.025, x=-0.1, y=-0.12, z=1.99, seg=8)])
    legs(lambda s: capsule(0.085, 0.8, x=s * 0.2, z=0.5) + [box(0.16, 0.3, 0.07, x=s * 0.2, y=-0.06, z=0.035)])
    export("S08_Footage")


def white_room():
    """SUPERHOT: a crystal man — angular, gem-like shards for a body, a faceted skull, no visible machinery."""
    merge("Body", [sph(0.34, z=1.32, ico=True, sx=1.0, sy=0.62, sz=1.15), sph(0.24, z=0.98, ico=True, sy=0.7),
                   limb((-0.3, 0, 1.55), (-0.38, -0.05, 1.12), 0.06, square=True), limb((0.3, 0, 1.55), (0.38, -0.05, 1.12), 0.06, square=True),
                   limb((-0.38, -0.05, 1.1), (-0.36, -0.1, 0.8), 0.05, square=True), limb((0.38, -0.05, 1.1), (0.36, -0.1, 0.8), 0.05, square=True)])
    merge("Accent", [sph(0.17, z=1.84, ico=True, sz=1.15), sph(0.07, z=1.64, ico=True),
                     sph(0.07, x=-0.36, y=-0.1, z=0.76, ico=True), sph(0.07, x=0.36, y=-0.1, z=0.76, ico=True)])
    merge("Eye", [box(0.16, 0.03, 0.03, y=-0.16, z=1.86)])
    legs(lambda s: [limb((s * 0.13, 0, 0.92), (s * 0.15, -0.02, 0.5), 0.07, square=True),
                    limb((s * 0.15, -0.02, 0.5), (s * 0.16, 0, 0.08), 0.055, square=True), box(0.12, 0.26, 0.07, x=s * 0.16, y=-0.06, z=0.035)])
    export("S09_WhiteRoom")


def grid():
    """TRON: a sleek suit with glowing seams — smooth helmet, a visor line, light strips down the chest, arms and legs."""
    merge("Body", [box(0.5, 0.3, 0.6, z=1.3, taper=1.12), box(0.38, 0.26, 0.24, z=0.95),
                   limb((-0.32, 0, 1.52), (-0.36, -0.02, 1.1), 0.055), limb((0.32, 0, 1.52), (0.36, -0.02, 1.1), 0.055),
                   limb((-0.36, -0.02, 1.1), (-0.34, -0.1, 0.8), 0.05), limb((0.36, -0.02, 1.1), (0.34, -0.1, 0.8), 0.05)])
    merge("Accent", [sph(0.18, z=1.84, seg=24, sy=1.1, sz=1.2), cyl(0.07, 0.1, z=1.66, seg=16),
                     box(0.54, 0.32, 0.06, z=1.6), sph(0.06, x=-0.34, y=-0.1, z=0.78, seg=12), sph(0.06, x=0.34, y=-0.1, z=0.78, seg=12)])
    seams = [box(0.02, 0.02, 0.56, x=x, y=-0.16, z=1.3) for x in (-0.12, 0.12)] + [box(0.4, 0.02, 0.02, y=-0.155, z=1.05)]
    seams += [box(0.015, 0.015, 0.4, x=s * 0.37, y=-0.07, z=1.3, ry=s * 0.09) for s in (-1, 1)]
    merge("Eye", [box(0.26, 0.03, 0.045, y=-0.19, z=1.86)] + seams)
    legs(lambda s: [limb((s * 0.15, 0, 0.92), (s * 0.16, 0, 0.1), 0.065), box(0.13, 0.28, 0.08, x=s * 0.16, y=-0.06, z=0.04),
                    box(0.015, 0.015, 0.7, x=s * 0.16, y=-0.07, z=0.5)])
    export("S10_Grid")


def derelict():
    """Dead Space / Alien: Isolation: a decommissioned station security synth — scuffed armour shell over a bare
    mechanical frame, a cracked faceplate with one eye still lit, torn cables hanging from the shoulder and the spine."""
    vents = [box(0.05, 0.03, 0.16, x=-0.12 + i * 0.08, y=-0.19, z=1.08) for i in range(4)]
    merge("Body", [box(0.54, 0.34, 0.5, z=1.4, taper=1.1),                                         # chest shell
                   cyl(0.12, 0.3, z=1.06, seg=16), box(0.4, 0.26, 0.16, z=0.92)] + vents + [   # exposed waist actuator, hips
                   box(0.12, 0.1, 0.6, y=0.2, z=1.25),                                             # spine housing
                   limb((-0.34, 0, 1.55), (-0.4, -0.04, 1.15), 0.055), limb((0.34, 0, 1.55), (0.4, -0.04, 1.15), 0.055),
                   limb((-0.4, -0.04, 1.13), (-0.38, -0.14, 0.82), 0.045), limb((0.4, -0.04, 1.13), (0.38, -0.14, 0.82), 0.045)])
    merge("Accent", [box(0.3, 0.32, 0.34, y=-0.02, z=1.86, taper=0.88),                            # skull casing
                     box(0.26, 0.05, 0.22, y=-0.19, z=1.85, rz=0.05),                              # faceplate, askew
                     cyl(0.065, 0.12, z=1.68, seg=12),                                              # neck
                     box(0.22, 0.28, 0.12, x=-0.36, z=1.62, ry=-0.35),                              # one surviving pauldron
                     limb((0.3, 0.05, 1.62), (0.36, 0.1, 1.2), 0.012), limb((0.3, 0.08, 1.6), (0.26, 0.16, 1.28), 0.01),  # torn cables
                     limb((0.0, 0.25, 1.5), (0.06, 0.3, 1.0), 0.012),
                     box(0.1, 0.12, 0.16, x=-0.38, y=-0.15, z=0.76), box(0.1, 0.12, 0.16, x=0.38, y=-0.15, z=0.76)])  # hands
    merge("Eye", [sph(0.03, x=-0.07, y=-0.22, z=1.88, seg=10)])                                     # the one live eye
    legs(lambda s: [limb((s * 0.2, 0, 0.9), (s * 0.21, -0.04, 0.5), 0.07), cyl(0.06, 0.12, x=s * 0.21, y=-0.04, z=0.5, axis="X", seg=12),
                    limb((s * 0.21, -0.04, 0.5), (s * 0.22, 0.02, 0.12), 0.055), box(0.15, 0.3, 0.1, x=s * 0.22, y=-0.06, z=0.05)])
    export("S11_Derelict")


DESIGNS = [("lofi", night_shift), ("toy", scrapyard), ("realistic", heist), ("realistic", hunted), ("lowpoly", toon),
           ("clean", storybook), ("clean", logbook), ("toy", footage), ("crystal", white_room), ("clean", grid),
           ("realistic", derelict)]

if __name__ == "__main__":
    g.clear_scene()
    only = argv[1:]  # optional: style asset names to build (default: all)
    for FINISH, make in DESIGNS:
        if only and not any(make.__name__ in n.lower() for n in only):
            continue
        make()
        print("guard", make.__name__)
    print("DONE")
