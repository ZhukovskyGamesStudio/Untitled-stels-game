"""The guard robot designed anew for every visual style (it is the robot of the comparison shots).

Run:  blender.exe -b --factory-startup --python Tools/Blender/guards.py -- Assets/_Game/Art/Models [S16_Derelict ...]

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
import meshfix  # noqa: E402

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
    meshfix.separate_coplanar(bm)  # the finish (grid snap, noise) can bring faces back into one plane
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
    meshfix.fix_objects(list(bpy.context.selected_objects), name=f"robot_guard__{style}")
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
                     box(0.06, 0.06, 0.3, x=0.18, y=0.05, z=2.0), cyl(0.04, 0.12, x=-0.18, y=0.36, z=1.6, seg=6)])  # antenna, exhaust on the battery
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
    legs(lambda s: capsule(0.11, 0.72, x=s * 0.2, z=0.5) + [sph(0.16, x=s * 0.2, y=-0.06, z=0.1, seg=16, sy=1.4, sz=0.6)])
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
    """Blue Prince: an art-deco doorman automaton — fluted column body, round face plate with a monocle, bell-hop cap,
    stepped brass epaulettes and a big wind-up key on the back (the automaton reads from above and behind)."""
    flutes = [cyl(0.03, 0.62, x=0.27 * math.cos(a), y=0.2 * math.sin(a), z=1.3, seg=8) for a in
              [math.pi * (0.55 + 0.1 * i) for i in range(10)]]
    merge("Body", [cyl(0.28, 0.66, z=1.3, seg=24, r2=0.24), cyl(0.33, 0.08, z=0.98, seg=24), cyl(0.27, 0.06, z=1.63, seg=24),
                   cyl(0.34, 0.22, z=1.02, seg=24, r2=0.29)] + flutes + [                          # coat tails flare at the hips
                   limb((-0.3, 0, 1.55), (-0.37, -0.04, 1.12), 0.065), limb((0.3, 0, 1.55), (0.37, -0.04, 1.12), 0.065),
                   limb((-0.37, -0.04, 1.12), (-0.32, -0.15, 0.86), 0.06), limb((0.37, -0.04, 1.12), (0.32, -0.15, 0.86), 0.06)])
    epaulettes = []
    for s in (-1, 1):  # three stepped brass discs on each shoulder
        epaulettes += [cyl(0.12 - 0.03 * k, 0.035, x=s * 0.27, z=1.6 + 0.035 * k, seg=20) for k in range(3)]
    key = [cyl(0.035, 0.2, y=0.3, z=1.36, axis="Y", seg=12),                                         # wind-up key: shaft
           cyl(0.11, 0.035, x=-0.11, y=0.42, z=1.36, axis="Y", seg=20), cyl(0.11, 0.035, x=0.11, y=0.42, z=1.36, axis="Y", seg=20),
           box(0.12, 0.04, 0.06, y=0.42, z=1.36)]
    merge("Accent", [cyl(0.21, 0.32, z=1.84, seg=24), cyl(0.22, 0.04, y=-0.17, z=1.84, axis="Y", seg=24),   # head and face plate
                     cyl(0.22, 0.14, z=2.05, seg=24), cyl(0.24, 0.025, y=-0.02, z=1.98, seg=24),           # cap and its band
                     cyl(0.08, 0.08, z=1.68, seg=16),
                     sph(0.08, x=-0.32, y=-0.16, z=0.84, seg=12), sph(0.08, x=0.32, y=-0.16, z=0.84, seg=12)] + epaulettes + key)
    merge("Eye", [cyl(0.095, 0.03, x=0.07, y=-0.2, z=1.86, axis="Y", seg=20),                       # monocle, bigger
                  cyl(0.045, 0.03, y=-0.235, z=2.05, axis="Y", seg=16)])                            # cap badge
    legs(lambda s: [limb((s * 0.13, 0, 0.96), (s * 0.13, 0, 0.1), 0.075), box(0.15, 0.3, 0.09, x=s * 0.13, y=-0.06, z=0.045),
                    cyl(0.085, 0.12, x=s * 0.13, z=0.16, seg=16)])
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
          capsule(0.075, 0.5, x=-0.34, z=1.22) + capsule(0.075, 0.5, x=0.34, z=1.22))
    merge("Accent", [box(0.3, 0.52, 0.3, y=0.02, z=1.83),                                          # camcorder body
                     cyl(0.13, 0.22, y=-0.3, z=1.83, axis="Y", seg=20, r2=0.15),                    # lens barrel
                     box(0.06, 0.18, 0.12, x=0.18, y=0.05, z=1.86),                                 # side screen
                     cyl(0.05, 0.17, z=1.6, seg=12), box(0.1, 0.12, 0.08, y=0.1, z=2.02),           # neck, top handle
                     sph(0.09, x=-0.4, y=-0.04, z=0.92, seg=12), sph(0.09, x=0.4, y=-0.04, z=0.92, seg=12)])
    merge("Eye", [cyl(0.11, 0.03, y=-0.42, z=1.83, axis="Y", seg=20), sph(0.025, x=-0.1, y=-0.12, z=1.99, seg=8)])
    legs(lambda s: capsule(0.085, 0.88, x=s * 0.2, z=0.46) + [box(0.16, 0.3, 0.07, x=s * 0.2, y=-0.06, z=0.035)])
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
    """TRON: a sleek suit with glowing seams — smooth helmet with a wrap-around visor, broad shoulder plates, bold light
    strips down the chest, arms and legs, and the identity disc on the back (a glowing ring, reads from above)."""
    merge("Body", [box(0.56, 0.32, 0.6, z=1.3, taper=1.22), box(0.4, 0.28, 0.24, z=0.95),
                   limb((-0.34, 0, 1.52), (-0.39, -0.02, 1.12), 0.075), limb((0.34, 0, 1.52), (0.39, -0.02, 1.12), 0.075),
                   limb((-0.39, -0.02, 1.12), (-0.36, -0.1, 0.8), 0.07), limb((0.39, -0.02, 1.12), (0.36, -0.1, 0.8), 0.07)])
    merge("Accent", [sph(0.2, z=1.86, seg=24, sy=1.12, sz=1.2), cyl(0.08, 0.12, z=1.66, seg=16),
                     box(0.6, 0.34, 0.07, z=1.6, taper=0.9),                                          # shoulder yoke
                     box(0.2, 0.3, 0.08, x=-0.36, z=1.57, ry=0.35), box(0.2, 0.3, 0.08, x=0.36, z=1.57, ry=-0.35),  # shoulder plates
                     sph(0.08, x=-0.36, y=-0.1, z=0.77, seg=12), sph(0.08, x=0.36, y=-0.1, z=0.77, seg=12),
                     cyl(0.2, 0.04, y=0.2, z=1.36, axis="Y", seg=32)])                                # identity disc
    seams = [box(0.04, 0.025, 0.56, x=x, y=-0.17, z=1.3) for x in (-0.13, 0.13)] + [box(0.44, 0.025, 0.04, y=-0.165, z=1.05)]
    seams += [box(0.03, 0.03, 0.42, x=s * 0.4, y=-0.07, z=1.3, ry=s * 0.12) for s in (-1, 1)]
    seams += [cyl(0.16, 0.05, y=0.205, z=1.36, axis="Y", seg=32)]                                    # the disc's glowing ring
    merge("Eye", [box(0.36, 0.05, 0.08, y=-0.2, z=1.88), box(0.42, 0.3, 0.06, y=-0.06, z=1.88)] + seams)  # wrap-around visor
    legs(lambda s: [limb((s * 0.15, 0, 0.92), (s * 0.16, 0, 0.08), 0.085), box(0.16, 0.3, 0.1, x=s * 0.16, y=-0.06, z=0.05),
                    box(0.03, 0.03, 0.72, x=s * 0.16, y=-0.085, z=0.5)])
    export("S10_Grid")


def noir():
    """Noir detective (after Shadows of Doubt): a robot private eye — long coat with turned-up collar, a fedora pulled
    low, a glowing visor slit under its brim, hands in the coat pockets. Built out of chunky blocks."""
    merge("Body", [box(0.56, 0.36, 0.7, z=1.3, taper=1.1),                                        # coat, shoulders to hips
                   box(0.6, 0.38, 0.12, z=0.96),                                                  # coat hem
                   box(0.16, 0.2, 0.46, x=-0.36, y=-0.02, z=1.2, ry=-0.12), box(0.16, 0.2, 0.46, x=0.36, y=-0.02, z=1.2, ry=0.12)])
    merge("Accent", [box(0.3, 0.32, 0.3, y=-0.02, z=1.8),                                          # square head
                     box(0.62, 0.62, 0.03, z=1.97), box(0.34, 0.34, 0.16, z=2.06, taper=0.85),    # hat brim and crown
                     box(0.2, 0.08, 0.2, x=-0.14, y=-0.16, z=1.62, rz=0.35), box(0.2, 0.08, 0.2, x=0.14, y=-0.16, z=1.62, rz=-0.35),  # collar
                     box(0.14, 0.06, 0.12, x=-0.22, y=-0.19, z=1.02), box(0.14, 0.06, 0.12, x=0.22, y=-0.19, z=1.02)])  # pocket flaps
    merge("Eye", [box(0.24, 0.03, 0.04, y=-0.18, z=1.86)])
    legs(lambda s: [box(0.14, 0.16, 0.86, x=s * 0.14, z=0.5), box(0.16, 0.3, 0.08, x=s * 0.14, y=-0.06, z=0.04)])
    export("S11_Noir")


def test_chamber():
    """Test chamber (after Portal): a white lab android — smooth shell plates on dark joints, a round head with one
    big round lens, thin limbs. Clean and quiet."""
    merge("Body", [capsule(0.24, 0.42, z=1.32, seg=24), box(0.36, 0.26, 0.16, z=0.98),
                   limb((-0.3, 0, 1.5), (-0.34, -0.04, 1.12), 0.035), limb((0.3, 0, 1.5), (0.34, -0.04, 1.12), 0.035),
                   limb((-0.34, -0.04, 1.1), (-0.32, -0.12, 0.82), 0.03), limb((0.34, -0.04, 1.1), (0.32, -0.12, 0.82), 0.03)])
    merge("Accent", [sph(0.2, z=1.84, seg=24), cyl(0.05, 0.12, z=1.64, seg=12),                    # head, neck
                     sph(0.09, x=-0.3, z=1.5, seg=16), sph(0.09, x=0.3, z=1.5, seg=16),            # shoulder balls
                     box(0.3, 0.08, 0.36, y=-0.2, z=1.34, taper=0.8),                              # chest plate
                     sph(0.05, x=-0.32, y=-0.13, z=0.8, seg=12), sph(0.05, x=0.32, y=-0.13, z=0.8, seg=12)])
    merge("Eye", [cyl(0.085, 0.04, y=-0.19, z=1.86, axis="Y", seg=24)])
    legs(lambda s: [limb((s * 0.12, 0, 0.92), (s * 0.13, 0, 0.04), 0.045), sph(0.06, x=s * 0.13, z=0.5, seg=12),
                    box(0.12, 0.24, 0.06, x=s * 0.13, y=-0.05, z=0.03)])
    export("S12_TestChamber")


def comic():
    """Wasteland comic (after Borderlands): a welded-together bandit bot — a lopsided boiler body with rivets, a gas-mask
    face with two round eyes and a hose, one arm ending in a big wrench claw, a spiked shoulder plate. Pushed proportions:
    a big boiler chest over short legs, thick limbs, a heavy jaw."""
    rivets = [sph(0.03, x=0.36 * math.cos(a), y=0.29 * math.sin(a), z=1.48, seg=6) for a in [i * math.pi / 5 for i in range(10)]]
    merge("Body", [cyl(0.36, 0.66, z=1.32, seg=10, r2=0.42), box(0.5, 0.36, 0.22, z=0.95)] + rivets + [
                   limb((-0.4, 0, 1.5), (-0.52, -0.02, 1.12), 0.09), limb((0.4, 0, 1.5), (0.5, -0.06, 1.14), 0.1),
                   limb((-0.52, -0.02, 1.12), (-0.48, -0.12, 0.86), 0.08), limb((0.5, -0.06, 1.14), (0.52, -0.2, 0.92), 0.09)])
    merge("Accent", [box(0.36, 0.34, 0.32, y=-0.04, z=1.86, rz=0.08, taper=0.9),                     # tilted head
                     box(0.32, 0.22, 0.14, y=-0.12, z=1.7, taper=0.8),                                # heavy jaw
                     cyl(0.08, 0.12, y=-0.26, z=1.74, axis="Y", seg=8), limb((0, -0.3, 1.72), (0.22, -0.24, 1.5), 0.035),  # filter, hose
                     box(0.36, 0.4, 0.14, x=0.48, z=1.66, ry=0.45), box(0.06, 0.06, 0.26, x=0.56, z=1.82, ry=0.45),  # shoulder plate, spike
                     box(0.06, 0.06, 0.2, x=0.42, y=0.12, z=1.8, ry=0.2),
                     box(0.18, 0.18, 0.3, x=0.52, y=-0.26, z=0.84),                                   # wrench handle
                     box(0.3, 0.14, 0.1, x=0.52, y=-0.3, z=0.66), box(0.08, 0.14, 0.14, x=0.4, y=-0.3, z=0.6),
                     box(0.08, 0.14, 0.14, x=0.64, y=-0.3, z=0.6),                                    # open jaws
                     sph(0.09, x=-0.48, y=-0.14, z=0.8, seg=8)])
    merge("Eye", [cyl(0.08, 0.03, x=-0.08, y=-0.22, z=1.9, axis="Y", seg=12), cyl(0.065, 0.03, x=0.09, y=-0.22, z=1.88, axis="Y", seg=12)])
    legs(lambda s: [limb((s * 0.18, 0, 0.92), (s * 0.2, -0.02, 0.48), 0.09), cyl(0.1, 0.12, x=s * 0.2, y=-0.02, z=0.48, seg=8),
                    limb((s * 0.2, -0.02, 0.48), (s * 0.21, 0, 0.08), 0.08), box(0.22, 0.34, 0.12, x=s * 0.21, y=-0.06, z=0.06)])
    export("S13_Comic")


def thermal():
    """Thermal camera (our own): a heavy heat-sink trooper — radiator fins on the back and shoulders that glow hot
    on the camera, a barrel helmet with a wide sensor slit, armoured forearms, coolant tanks and hoses on the back."""
    fins = [box(0.035, 0.22, 0.56, x=-0.24 + i * 0.08, y=0.28, z=1.32) for i in range(7)]
    merge("Body", [box(0.68, 0.42, 0.62, z=1.3, taper=1.08), box(0.5, 0.36, 0.26, z=0.96)] + fins + [
                   limb((-0.42, 0, 1.5), (-0.47, -0.02, 1.12), 0.09), limb((0.42, 0, 1.5), (0.47, -0.02, 1.12), 0.09)])
    tanks = []
    for s in (-1, 1):
        tanks += [cyl(0.085, 0.5, x=s * 0.27, y=0.3, z=1.3, seg=16), sph(0.085, x=s * 0.27, y=0.3, z=1.55, seg=16),
                  limb((s * 0.27, 0.3, 1.6), (s * 0.24, 0.1, 1.72), 0.03), limb((s * 0.24, 0.1, 1.72), (s * 0.18, -0.1, 1.55), 0.03)]
    merge("Accent", [cyl(0.22, 0.38, z=1.86, seg=16), box(0.48, 0.07, 0.09, y=-0.02, z=2.07),          # barrel helmet, crest
                     box(0.24, 0.3, 0.24, x=-0.45, z=1.56), box(0.24, 0.3, 0.24, x=0.45, z=1.56),     # shoulder blocks
                     box(0.035, 0.24, 0.2, x=-0.53, z=1.64), box(0.035, 0.24, 0.2, x=0.53, z=1.64),   # shoulder fins
                     box(0.035, 0.24, 0.2, x=-0.47, z=1.66), box(0.035, 0.24, 0.2, x=0.47, z=1.66),
                     box(0.2, 0.22, 0.36, x=-0.47, y=-0.06, z=0.94), box(0.2, 0.22, 0.36, x=0.47, y=-0.06, z=0.94)] + tanks)
    merge("Eye", [box(0.4, 0.04, 0.08, y=-0.215, z=1.88), box(0.42, 0.14, 0.03, y=-0.13, z=2.03)])   # sensor slit, crest light
    legs(lambda s: [box(0.2, 0.24, 0.48, x=s * 0.2, z=0.68), box(0.18, 0.22, 0.46, x=s * 0.21, y=-0.02, z=0.24),
                    box(0.24, 0.36, 0.1, x=s * 0.21, y=-0.06, z=0.05)])
    export("S14_Thermal")


def blueprint():
    """Blueprint (our own): a drafting automaton drawn with a ruler — an open frame of square beams for a body, a
    triangular head with a crosshair lens, a set square on its back, thin square limbs."""
    frame = [limb(a, b, 0.025, square=True) for a, b in [((-0.25, 0, 1.0), (-0.3, 0, 1.58)), ((0.25, 0, 1.0), (0.3, 0, 1.58)),
                                                        ((-0.3, 0, 1.58), (0.3, 0, 1.58)), ((-0.25, 0, 1.0), (0.25, 0, 1.0)),
                                                        ((-0.25, 0, 1.0), (0.3, 0, 1.58)), ((0.25, 0, 1.0), (-0.3, 0, 1.58))]]
    merge("Body", frame + [box(0.3, 0.2, 0.3, z=1.3),
                   limb((-0.3, 0, 1.56), (-0.36, -0.04, 1.12), 0.03, square=True), limb((0.3, 0, 1.56), (0.36, -0.04, 1.12), 0.03, square=True),
                   limb((-0.36, -0.04, 1.1), (-0.32, -0.14, 0.84), 0.025, square=True), limb((0.36, -0.04, 1.1), (0.32, -0.14, 0.84), 0.025, square=True)])
    tri = bmesh.new()
    v = [tri.verts.new(p) for p in [(-0.2, -0.12, 1.7), (0.2, -0.12, 1.7), (0.0, -0.12, 2.04), (-0.2, 0.12, 1.7), (0.2, 0.12, 1.7), (0.0, 0.12, 2.04)]]
    for f in [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]:
        tri.faces.new([v[i] for i in f])
    merge("Accent", [tri, cyl(0.04, 0.12, z=1.64, seg=8),                                            # triangular head, neck
                     box(0.02, 0.36, 0.36, x=0.0, y=0.16, z=1.3, rx=0.785),                          # set square on the back
                     box(0.08, 0.08, 0.08, x=-0.32, y=-0.14, z=0.8), box(0.08, 0.08, 0.08, x=0.32, y=-0.14, z=0.8)])
    merge("Eye", [cyl(0.06, 0.03, y=-0.14, z=1.82, axis="Y", seg=16), box(0.2, 0.03, 0.012, y=-0.15, z=1.82),
                  box(0.012, 0.03, 0.2, y=-0.15, z=1.82)])
    legs(lambda s: [limb((s * 0.15, 0, 0.98), (s * 0.16, 0, 0.035), 0.035, square=True), box(0.08, 0.08, 0.08, x=s * 0.16, z=0.5),
                    box(0.12, 0.26, 0.05, x=s * 0.16, y=-0.06, z=0.025)])
    export("S15_Blueprint")


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
    export("S16_Derelict")


DESIGNS = [("lofi", night_shift), ("toy", scrapyard), ("realistic", heist), ("realistic", hunted), ("lowpoly", toon),
           ("clean", storybook), ("clean", logbook), ("toy", footage), ("crystal", white_room), ("clean", grid),
           ("lofi", noir), ("clean", test_chamber), ("lowpoly", comic), ("realistic", thermal), ("clean", blueprint),
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
