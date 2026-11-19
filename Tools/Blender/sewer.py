"""The sewer between houses: vaulted brick tunnels, a hall with a cistern, branching tunnels to the next houses and the props.

Run:  blender.exe -b --factory-startup --python Tools/Blender/sewer.py -- Assets/_Game/Art/Models

Writes Art/Models/Sewer/sewer_*.fbx. Blender: x along the tunnel (Unity X), -y = Unity +Z, z up. Part names pick the
material in the Unity builder (AssetBuilder.BuildSewer): Brick, Stone, Concrete, Water, Metal, Rust, Wood, Dark, Glow.
Every part gets box-projected UVs (1 unit = 1 m) so the brick and concrete textures tile in metres.

Layout (metres, Unity X):  0..12 the tunnel from the ladder (main channel in the middle, walkways along the walls)
                          12..22 the hall: wider vault, a cistern basin in the middle
                          22..   three arched openings (y = -4.2, 0, +4.2) into the branch tunnels to the next houses

The drivable modular kit (`kit()`, below the props; `-- <root> kit` builds only it): the team drives a pickup through
the sewer, so the game assembles it at runtime along +X from pieces that join seamlessly at their ends —
sewer_cap (closed start, faces +X) · sewer_seg_0..3 (6 m, any order) · sewer_gate_frame/door + sewer_lever ·
sewer_hub (16 m junction hall: three openings in its end wall, one in each long wall; no longer used: the game lays
the hall out of the sewer_hall_* tiles, different every time — kit_hall, `-- <root> hall` builds only them) ·
sewer_branch (110 m, one per hub opening) · sewer_plug (bricks closing an opening that leads nowhere) · sewer_reducer (a branch opening into the wide kit
tunnel again: the drive on to the next house). Floor = a steel road grate (part Grate,
top at z = 0, texture Art/Textures/Sewer/grate.png = 0.5 m) over a water channel; sewer_seg_<n>_cut lacks the -Y
wall and vault for the side camera.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
KIT_ONLY = "kit" in argv  # `-- <root> kit`: build only the drivable modular kit (sewer_seg_*, gate, hub, branch, cap)
HALL_ONLY = "hall" in argv  # `-- <root> hall`: build only the hall's tiles (sewer_hall_*, kit_hall)
argv = [a for a in argv if a not in ("kit", "hall")]
ROOT = argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
OUT = os.path.join(ROOT, "Sewer")

TUNNEL = dict(half=3.3, wall=2.2, rise=1.5)
HALL = dict(half=6.5, wall=2.6, rise=2.6)
BRANCH = dict(half=1.3, wall=1.8, rise=1.1)
SEG = 20
HALL_X0, HALL_X1 = 12.0, 22.0
BRANCH_Y = (-4.2, 0.0, 4.2)
BRANCH_YAW = 18.0  # degrees the side tunnels bend away from the middle one
SLAB = 0.8         # floors reach this deep: the channel, the trench and the cistern have walls down to their beds


# --------------------------------------------------------------------------- helpers

def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials):
        for item in list(block):
            block.remove(item)


def obj_from(bm, name):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def profile(half, wall, rise, seg=SEG):
    """Inner surface of a vaulted section, left floor → wall → elliptic vault → wall → right floor: (y, z) points."""
    pts = [(-half, 0.0)]
    for i in range(seg + 1):
        t = math.pi - math.pi * i / seg
        pts.append((half * math.cos(t), wall + rise * math.sin(t)))
    pts.append((half, 0.0))
    return pts


def z_at(prof, y):
    """Height of the vault of a profile at y (for bars and pipes that must fit under it)."""
    best = 0.0
    for (y0, z0), (y1, z1) in zip(prof[1:-2], prof[2:-1]):
        if min(y0, y1) <= y <= max(y0, y1) and abs(y1 - y0) > 1e-6:
            best = max(best, z0 + (z1 - z0) * (y - y0) / (y1 - y0))
    return best


def shell(bm, prof, x0, x1, inward_center):
    """The vault and walls between x0 and x1, faces turned towards the inside."""
    a = [bm.verts.new((x0, y, z)) for y, z in prof]
    b = [bm.verts.new((x1, y, z)) for y, z in prof]
    for i in range(len(a) - 1):
        f = bm.faces.new((a[i], a[i + 1], b[i + 1], b[i]))
        f.normal_update()
        c = f.calc_center_median()
        to_axis = Vector((c.x, inward_center[0], inward_center[1])) - c
        if f.normal.dot(to_axis) < 0:
            f.normal_flip()


def band(bm, outer, inner, x0, x1):
    """A rib / frame between two profiles with the same number of points: front, back and inner faces."""
    o0 = [bm.verts.new((x0, y, z)) for y, z in outer]
    i0 = [bm.verts.new((x0, y, z)) for y, z in inner]
    o1 = [bm.verts.new((x1, y, z)) for y, z in outer]
    i1 = [bm.verts.new((x1, y, z)) for y, z in inner]
    for k in range(len(o0) - 1):
        bm.faces.new((o0[k], i0[k], i0[k + 1], o0[k + 1]))   # front
        bm.faces.new((o1[k + 1], i1[k + 1], i1[k], o1[k]))   # back
        bm.faces.new((i0[k], i1[k], i1[k + 1], i0[k + 1]))   # inner surface
    for idx in (0, len(o0) - 1):                             # feet of the band on the floor
        bm.faces.new((o0[idx], o1[idx], i1[idx], i0[idx]))


def inset(p, d):
    return dict(half=p["half"] - d, wall=p["wall"], rise=p["rise"] - d)


def box(bm, x0, x1, y0, y1, z0, z1):
    v = [bm.verts.new(c) for c in [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                                     (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]]
    for f in [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]:
        bm.faces.new([v[i] for i in f])


def cyl(bm, r, depth, center, axis="Z", seg=16, r2=None):
    m = {"Z": None, "X": ("Y", math.pi / 2), "Y": ("X", math.pi / 2)}[axis]
    res = bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=depth)
    verts = res["verts"]
    if m:
        from mathutils import Matrix
        rot = Matrix.Rotation(m[1], 4, m[0])
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=rot)
    bmesh.ops.translate(bm, verts=verts, vec=Vector(center))


def torus(bm, R, r, center, axis="Z", seg=24, seg2=8):
    from mathutils import Matrix
    verts = []
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring = []
        for j in range(seg2):
            b = 2 * math.pi * j / seg2
            p = Vector(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b)))
            ring.append(bm.verts.new(p))
        rings.append(ring)
        verts += ring
    for i in range(seg):
        for j in range(seg2):
            a, b = rings[i], rings[(i + 1) % seg]
            bm.faces.new((a[j], b[j], b[(j + 1) % seg2], a[(j + 1) % seg2]))
    if axis != "Z":
        rot = Matrix.Rotation(math.pi / 2, 4, "Y" if axis == "X" else "X")
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=rot)
    bmesh.ops.translate(bm, verts=verts, vec=Vector(center))


def prism(name, prof, x0, x1, y_off=0.0):
    """A solid with the cross-section of a profile (for booleans)."""
    bm = bmesh.new()
    pts = [(y + y_off, z) for y, z in prof]
    a = [bm.verts.new((x0, y, z)) for y, z in pts]
    b = [bm.verts.new((x1, y, z)) for y, z in pts]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    n = len(a)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], b[i], b[j], a[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from(bm, name)


def subtract(target, cutter):
    mod = target.modifiers.new("cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    mod.object = cutter
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def part(name, build):
    bm = bmesh.new()
    build(bm)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    return obj_from(bm, name)


def branch_yaw(y):
    return math.radians(math.copysign(BRANCH_YAW, y)) if abs(y) > 1e-6 else 0.0


def place_branch(o, y):
    """A part built along +x from a tunnel's mouth, turned and moved to its opening at y in the hall's end wall."""
    from mathutils import Matrix
    o.data.transform(Matrix.Translation((HALL_X1, y, 0.0)) @ Matrix.Rotation(branch_yaw(y), 4, "Z"))
    return o


def clip_to_hall(o):
    """Cut a branch part off at the inner face of the hall's end wall: it starts inside the wall, never in the hall."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(HALL_X1 - 0.38, 0.0, 0.0), plane_no=(1.0, 0.0, 0.0), clear_inner=True)
    bm.to_mesh(o.data)
    bm.free()
    return o


def finish_and_export(filename):
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = o
        for x in bpy.context.scene.objects:
            x.select_set(x == o)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=False, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        for poly in o.data.polygons:
            poly.use_smooth = False
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, filename + ".fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH"},
                             mesh_smooth_type="FACE", add_leaf_bones=False)
    clear()


# --------------------------------------------------------------------------- the tunnel from the ladder (x 0..12)

def tunnel():
    p = TUNNEL
    prof = profile(**p)
    part("Brick", lambda bm: shell(bm, prof, 0.0, HALL_X0 + 0.2, (0.0, p["wall"])))
    # the dead end behind the ladder
    end = prism("Brick", prof, -0.4, 0.0)
    end.name = "BrickEnd"

    def ribs(bm):
        for x in (0.0, 4.0, 8.0):
            band(bm, prof, profile(**inset(p, 0.14)), x + 1.8, x + 2.2)
    part("Stone", ribs)

    def floor(bm):
        box(bm, -0.4, HALL_X0 + 0.2, -p["half"], -1.25, -SLAB, 0.0)    # walkways, walled down to the channel bed
        box(bm, -0.4, HALL_X0 + 0.2, 1.25, p["half"], -SLAB, 0.0)
        box(bm, -0.4, HALL_X0 + 0.2, -1.25, 1.25, -0.75, -0.6)         # channel bed
    part("Concrete", floor)

    def curbs(bm):
        for s in (-1, 1):
            box(bm, -0.4, HALL_X0 + 0.2, s * 1.42 - 0.17, s * 1.42 + 0.17, -0.02, 0.05)
    part("Stone", curbs).name = "Curbs"
    part("Water", lambda bm: box(bm, -0.4, HALL_X0 + 0.6, -1.25, 1.25, -0.3, -0.26))
    finish_and_export("sewer_tunnel")


# --------------------------------------------------------------------------- the hall (x 12..22) with the openings

def hall():
    p = HALL
    prof = profile(**p)
    part("Brick", lambda bm: shell(bm, prof, HALL_X0, HALL_X1, (0.0, p["wall"])))

    def ribs(bm):
        for x in (14.6, 17.2, 19.8):
            band(bm, prof, profile(**inset(p, 0.18)), x - 0.25, x + 0.25)
    part("Stone", ribs)

    # front wall: the hall's section minus the tunnel's
    front = prism("BrickFront", prof, HALL_X0, HALL_X0 + 0.4)
    subtract(front, prism("cut", profile(**TUNNEL), HALL_X0 - 0.2, HALL_X0 + 0.6))
    # end wall with three arched openings, and stone frames around them
    back = prism("BrickBack", prof, HALL_X1 - 0.4, HALL_X1)
    for y in BRANCH_Y:
        # cut along the tunnel's own direction: the opening fits the turned tunnel exactly
        subtract(back, place_branch(prism("cut", profile(**BRANCH), -1.2, 0.6), y))

    def frames(bm):
        outer = dict(half=BRANCH["half"] + 0.22, wall=BRANCH["wall"], rise=BRANCH["rise"] + 0.22)
        for y in BRANCH_Y:
            yc = y - 0.4 * math.tan(branch_yaw(y))  # where the turned opening meets the hall's face of the wall
            o = [(yy + yc, z) for yy, z in profile(**outer)]
            i = [(yy + yc, z) for yy, z in profile(**BRANCH)]
            band(bm, o, i, HALL_X1 - 0.55, HALL_X1 - 0.38)
    part("Stone", frames).name = "Frames"

    # floor with the cistern basin and the trench bringing the channel in
    floor = part("Concrete", lambda bm: box(bm, HALL_X0, HALL_X1, -p["half"], p["half"], -SLAB, 0.0))
    basin = part("cut", lambda bm: cyl(bm, 2.0, 1.2, (16.6, 0.0, -0.1), seg=32))  # down to the basin's bed at -0.7
    subtract(floor, basin)
    subtract(floor, part("cut", lambda bm: box(bm, HALL_X0 - 0.1, 16.0, -1.25, 1.25, -0.6, 0.2)))
    part("Concrete", lambda bm: cyl(bm, 2.05, 0.1, (16.6, 0.0, -0.75), seg=32)).name = "BasinBed"
    part("Concrete", lambda bm: box(bm, HALL_X0 - 0.1, 16.0, -1.25, 1.25, -0.75, -0.6)).name = "TrenchBed"
    part("Stone", lambda bm: torus(bm, 2.08, 0.1, (16.6, 0.0, 0.02), seg=40, seg2=8)).name = "BasinRim"

    def water(bm):
        cyl(bm, 2.0, 0.04, (16.6, 0.0, -0.28), seg=32)
        box(bm, HALL_X0 - 0.2, 16.0, -1.25, 1.25, -0.3, -0.26)
    part("Water", water)
    # sign plates above the openings (the game hangs the route panels there)
    part("Metal", lambda bm: [box(bm, HALL_X1 - 0.5, HALL_X1 - 0.42, y - 0.9, y + 0.9, 3.3, 3.95) for y in BRANCH_Y]).name = "Plates"
    finish_and_export("sewer_hall")


# --------------------------------------------------------------------------- a branch tunnel to a house (local x 0..40)

BRANCH_LENGTH = 40.0  # it looks endless (the haze swallows the far end); the game stops players 2.7 m in

def branch():
    """The three tunnels to the next houses, each turned to its opening and cut off at the hall's end wall (no gap
    between the wall and the tunnel, nothing sticking into the hall). sewer_branch_0 is Unity's left one (z = -4.2)."""
    p = BRANCH
    prof = profile(**p)

    def ribs(bm):
        x = 2.0
        while x < BRANCH_LENGTH - 0.5:
            band(bm, prof, profile(**inset(p, 0.1)), x - 0.15, x + 0.15)
            x += 3.0

    for index, y in enumerate(sorted(BRANCH_Y, reverse=True)):  # Unity z = -y
        n = BRANCH_LENGTH
        parts = [part("Brick", lambda bm: shell(bm, prof, -1.0, n, (0.0, p["wall"]))),
                 part("Stone", ribs),
                 part("Concrete", lambda bm: box(bm, -1.0, n, -p["half"], p["half"], -0.3, 0.0))]
        gutter = part("Dark", lambda bm: box(bm, -1.0, n, -0.12, 0.12, -0.01, 0.005))
        gutter.name = "Gutter"
        end = part("Dark", lambda bm: box(bm, n - 0.4, n, -p["half"], p["half"], 0.0, p["wall"] + p["rise"]))
        end.name = "DarkEnd"
        for o in parts + [gutter, end]:
            clip_to_hall(place_branch(o, y))
        finish_and_export(f"sewer_branch_{index}")


def grate():
    """Rusty bars closing a branch that does not lead anywhere this time."""
    prof = profile(**BRANCH)

    def bars(bm):
        y = -BRANCH["half"] + 0.12
        while y < BRANCH["half"] - 0.1:
            h = z_at(prof, y) - 0.02
            cyl(bm, 0.025, h, (0.0, y, h / 2), seg=6)
            y += 0.16
        for z in (0.4, 1.4, 2.3):
            w = BRANCH["half"] if z < BRANCH["wall"] else math.sqrt(max(0.0, 1 - ((z - BRANCH["wall"]) / BRANCH["rise"]) ** 2)) * BRANCH["half"]
            box(bm, -0.03, 0.03, -w + 0.04, w - 0.04, z - 0.03, z + 0.03)
    part("Rust", bars)
    finish_and_export("sewer_grate")


# --------------------------------------------------------------------------- props

def ladder():
    def rails(bm):
        for y in (-0.28, 0.28):
            box(bm, -0.03, 0.03, y - 0.03, y + 0.03, 0.0, 3.4)
        for z in [0.3 + 0.3 * i for i in range(10)]:
            cyl(bm, 0.018, 0.56, (0.0, 0.0, z), axis="Y", seg=8)
        for z in (0.6, 1.8, 3.0):                                       # wall brackets
            box(bm, -0.25, 0.0, -0.31, -0.25, z - 0.03, z + 0.03)
            box(bm, -0.25, 0.0, 0.25, 0.31, z - 0.03, z + 0.03)
    part("Metal", rails)
    finish_and_export("sewer_ladder")


def manhole():
    """The way up seen from below: a ring and the underside of the lid."""
    part("Metal", lambda bm: torus(bm, 0.55, 0.06, (0, 0, 0), seg=28, seg2=8))
    part("Dark", lambda bm: cyl(bm, 0.5, 0.04, (0, 0, 0.05), seg=28))
    finish_and_export("sewer_manhole")


def cart():
    """The team's two-wheeled cart: the stash lies in its bed (inner 1.80 x 0.92, floor top at z = 0.62)."""
    def wheels(bm):
        for y in (-0.62, 0.62):
            torus(bm, 0.36, 0.035, (0.0, y, 0.4), axis="Y", seg=28, seg2=6)
            cyl(bm, 0.06, 0.12, (0.0, y, 0.4), axis="Y", seg=10)
            for k in range(8):
                a = math.pi * k / 4
                v = Vector((math.cos(a), 0, math.sin(a)))
                c = Vector((0.0, y, 0.4)) + v * 0.18
                bm_s = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.014, radius2=0.014, depth=0.36)
                from mathutils import Matrix
                rot = Matrix.Rotation(-a + math.pi / 2, 4, "Y")
                bmesh.ops.rotate(bm, verts=bm_s["verts"], cent=(0, 0, 0), matrix=rot)
                bmesh.ops.translate(bm, verts=bm_s["verts"], vec=c)
        cyl(bm, 0.03, 1.36, (0.0, 0.0, 0.4), axis="Y", seg=8)                 # axle
    part("Metal", wheels)

    def body(bm):
        for i in range(6):                                                    # floor planks
            y = -0.46 + 0.155 * i
            box(bm, -0.92, 0.92, y + 0.005, y + 0.15, 0.56, 0.62)
        box(bm, -0.95, 0.95, -0.52, -0.46, 0.56, 0.82)                       # sides
        box(bm, -0.95, 0.95, 0.46, 0.52, 0.56, 0.82)
        box(bm, 0.92, 0.98, -0.52, 0.52, 0.56, 0.82)                         # front board
        box(bm, -0.98, -0.92, -0.52, 0.52, 0.56, 0.74)                       # back board, lower
        for y in (-0.42, 0.42):                                              # frame under the bed
            box(bm, -1.0, 1.0, y - 0.04, y + 0.04, 0.48, 0.56)
    part("Wood", body)

    def handles(bm):
        for y in (-0.36, 0.36):
            box(bm, -2.0, -0.95, y - 0.03, y + 0.03, 0.62, 0.68)
        cyl(bm, 0.03, 0.8, (-1.98, 0.0, 0.65), axis="Y", seg=10)
        box(bm, -1.05, -0.99, -0.03, 0.03, 0.0, 0.56)                        # the leg it rests on
        for y in (-0.5, 0.5):                                                # corner irons
            box(bm, 0.94, 1.0, y - 0.04, y + 0.04, 0.56, 0.84)
    part("Metal", handles).name = "Irons"
    finish_and_export("sewer_cart")


def lamp():
    """Bulkhead lamp on a wall: a cage over a glowing glass. The wall is at +X (local); it shines towards -X."""
    part("Metal", lambda bm: cyl(bm, 0.16, 0.06, (0.03, 0.0, 0.0), axis="X", seg=16))
    part("Glow", lambda bm: cyl(bm, 0.12, 0.12, (-0.05, 0.0, 0.0), axis="X", seg=16, r2=0.09))

    def cage(bm):
        for k in range(6):
            a = math.pi * k / 3
            box(bm, -0.13, 0.0, 0.14 * math.cos(a) - 0.008, 0.14 * math.cos(a) + 0.008, 0.14 * math.sin(a) - 0.008, 0.14 * math.sin(a) + 0.008)
        torus(bm, 0.14, 0.01, (-0.13, 0.0, 0.0), axis="X", seg=16, seg2=4)
    part("Rust", cage).name = "Cage"
    finish_and_export("sewer_lamp")


def pipe():
    """A 4 m pipe along X with two flanges and wall brackets (the wall at +Y)."""
    def p(bm):
        cyl(bm, 0.13, 4.0, (2.0, 0.0, 0.0), axis="X", seg=14)
        for x in (0.05, 3.95):
            cyl(bm, 0.18, 0.06, (x, 0.0, 0.0), axis="X", seg=14)
    part("Rust", p)

    def brackets(bm):
        for x in (1.0, 3.0):
            box(bm, x - 0.04, x + 0.04, 0.0, 0.3, -0.16, -0.12)
            box(bm, x - 0.04, x + 0.04, 0.0, 0.3, 0.12, 0.16)
    part("Metal", brackets)
    finish_and_export("sewer_pipe")


def valve():
    def v(bm):
        torus(bm, 0.22, 0.025, (0.0, 0.0, 0.0), axis="X", seg=20, seg2=6)
        for k in range(4):
            a = math.pi * k / 4
            box(bm, -0.015, 0.015, -0.22 * math.cos(a), 0.22 * math.cos(a), -0.01, 0.01) if k == 0 else None
        cyl(bm, 0.04, 0.1, (0.05, 0.0, 0.0), axis="X", seg=10)
        cyl(bm, 0.07, 0.4, (0.25, 0.0, 0.0), axis="X", seg=12)
    part("Rust", v)
    part("Metal", lambda bm: [box(bm, -0.015, 0.015, -0.21, 0.21, -0.012, 0.012), box(bm, -0.015, 0.015, -0.012, 0.012, -0.21, 0.21)]).name = "Spokes"
    finish_and_export("sewer_valve")


def barrel():
    part("Rust", lambda bm: cyl(bm, 0.3, 0.88, (0.0, 0.0, 0.44), seg=18))
    part("Metal", lambda bm: [torus(bm, 0.305, 0.018, (0.0, 0.0, z), seg=18, seg2=4) for z in (0.12, 0.44, 0.76)])
    finish_and_export("sewer_barrel")


def crate():
    part("Wood", lambda bm: box(bm, -0.3, 0.3, -0.3, 0.3, 0.0, 0.6))

    def frame(bm):
        for (x0, x1, y0, y1) in [(-0.32, 0.32, -0.32, -0.27), (-0.32, 0.32, 0.27, 0.32), (-0.32, -0.27, -0.32, 0.32), (0.27, 0.32, -0.32, 0.32)]:
            box(bm, x0, x1, y0, y1, 0.0, 0.06)
            box(bm, x0, x1, y0, y1, 0.54, 0.6)
    part("Stone", frame).name = "Frame"
    finish_and_export("sewer_crate")


# =========================================================================== the drivable modular kit
#
# Pieces line up along +X. Every piece that joins another has the same cross-section at its joining end and no end
# caps there (the neighbour continues the surfaces); half ribs and half beams at the ends make a whole one at a join.
# Cross-section of a segment: walls at y = ±3.6 up to z = 2.6, elliptic vault to 4.2; road grate y ∈ [-2.6, 2.6] at
# z = 0 on transverse I-beams every 1.5 m (tops 6 mm proud, so it reads as panels) and edge rails (1 cm proud);
# water at -0.45 in a channel with its bed at -0.7; walkways y ∈ ±[2.6, 3.6] at 0.15 with a kerb.

KIT = dict(half=3.6, wall=2.6, rise=1.6)            # the segment's vault
KIT_HUB = dict(half=8.0, wall=3.4, rise=2.6)        # the junction hall's vault (top 6.0)
KIT_BRANCH = dict(half=2.3, wall=2.6, rise=1.2)     # a branch tunnel = a hub opening (top 3.8)
KIT_LEN = 6.0
KIT_ROAD = 2.6        # half width of the grate road between the walkways
KIT_WALK = 0.15       # walkway top
KIT_WATER = -0.45
KIT_BED = -0.7
KIT_BOTTOM = -0.75
KIT_BEAM = 1.5        # grate beams every 1.5 m (a multiple of the 0.5 m texture tile)
KIT_ARC = 20          # vault segments (even: the crown has a vertex at y = 0, where the cutaway ends)
KIT_THICK = 0.35      # vault thickness shown at the cutaway
HUB_LEN, HUB_F0, HUB_F1, HUB_WALK_END, HUB_WALL_X = 16.0, 0.5, 5.5, 14.6, 15.4
HUB_EDGE = 7.0        # the hub's walkways start here (y), from x = HUB_F1 to HUB_WALK_END
HUB_OPEN_Y = (-5.5, 0.0, 5.5)
HUB_SIDE_X = 10.0     # the two side openings (one in each long wall, y = ±8) are centred here
HUB_SIDE_MOUTH = 8.6  # ... and their mouth: the side walls are 0.6 m thick there, like the end wall (15.4..16)
HUB_NICHE = (7.3, 12.7)  # the side walls stand up to the vault's 4th arc vertex over this span (a niche in the vault)
HUB_NICHE_ARC = 4     # ... vault vertex index (z 4.93): room for the sign plate (4.1..4.75) over the side opening's frame
HUB_SIDE_RIB = 12.9   # the second hall rib (was at x = 10, now beside the side openings)
KIT_BRANCH_LEN = 110.0
KIT_BRANCH_RIB = 3.0  # rib spacing in a branch
GATE_HALF, GATE_TOP, GATE_SLOT = 2.8, 3.2, 2.95


def kit_profile(p, z0, d=0.0, extra=(), seg=KIT_ARC):
    """Inner surface of a kit vault from the left foot (z0) up the wall, over the vault and down to the right foot.
    d grows the section (ribs, thickness); extra adds wall vertices at these heights on both sides (no T-junctions
    where a neighbouring piece of wall stops at that height)."""
    h, w, r = p["half"] + d, p["wall"], p["rise"] + d
    pts = [(-h, z0)] + [(-h, z) for z in sorted(extra)]
    for i in range(seg + 1):
        t = math.pi - math.pi * i / seg
        pts.append((0.0 if 2 * i == seg else h * math.cos(t), w + r * math.sin(t)))
    pts += [(h, z) for z in sorted(extra, reverse=True)] + [(h, z0)]
    return pts


def far_half(prof):
    """The +Y half of a profile, crown first (the cutaway keeps it)."""
    return [pt for pt in prof if pt[0] > -1e-6]


def kface(bm, verts, expect):
    """A face turned to the expected normal."""
    f = bm.faces.new(verts)
    f.normal_update()
    if f.normal.dot(Vector(expect)) < 0:
        f.normal_flip()
    return f


def poly(bm, pts, expect=(0, 0, 1)):
    return kface(bm, [bm.verts.new(p) for p in pts], expect)


def skin(bm, stations, zc, inward=True, yc=0.0):
    """Surfaces between consecutive (x, profile) stations (same point count within a pair), turned towards the
    tunnel's axis (0 = yc, zc) or away from it."""
    for (x0, p0), (x1, p1) in zip(stations, stations[1:]):
        a = [bm.verts.new((x0, y, z)) for y, z in p0]
        b = [bm.verts.new((x1, y, z)) for y, z in p1]
        for i in range(len(a) - 1):
            f = bm.faces.new((a[i], a[i + 1], b[i + 1], b[i]))
            f.normal_update()
            c = f.calc_center_median()
            if (Vector((0.0, yc - c.y, zc - c.z)).dot(f.normal) > 0) != inward:
                f.normal_flip()


def rib(bm, outer, inner, x0, x1, zc, front=True, back=True, ends=(), yc=0.0, outside=False):
    """A band between two profiles (same point count): front (x0), back (x1) and inner faces; ends = [(index,
    expected normal)] closes the band there (the crown of a cut rib); outside adds the outer faces (a frame standing
    proud of a wall, not lying on a vault)."""
    o0 = [bm.verts.new((x0, y, z)) for y, z in outer]
    i0 = [bm.verts.new((x0, y, z)) for y, z in inner]
    o1 = [bm.verts.new((x1, y, z)) for y, z in outer]
    i1 = [bm.verts.new((x1, y, z)) for y, z in inner]
    for k in range(len(outer) - 1):
        if front:
            kface(bm, (o0[k], i0[k], i0[k + 1], o0[k + 1]), (-1, 0, 0))
        if back:
            kface(bm, (o1[k], i1[k], i1[k + 1], o1[k + 1]), (1, 0, 0))
        f = bm.faces.new((i0[k], i1[k], i1[k + 1], i0[k + 1]))
        f.normal_update()
        c = f.calc_center_median()
        if Vector((0.0, yc - c.y, zc - c.z)).dot(f.normal) < 0:
            f.normal_flip()
        if outside:
            f = bm.faces.new((o0[k], o1[k], o1[k + 1], o0[k + 1]))
            f.normal_update()
            c = f.calc_center_median()
            if Vector((0.0, yc - c.y, zc - c.z)).dot(f.normal) > 0:
                f.normal_flip()
    for idx, expect in ends:
        kface(bm, (o0[idx], o1[idx], i1[idx], i0[idx]), expect)


def strip(bm, pts, x0, x1, closed=False):
    """Faces between x0 and x1 along a (y, z) polyline; each faces to the right of the walking direction (y right,
    z up): walk -y along a floor, down the +y wall of a channel."""
    a = [bm.verts.new((x0, y, z)) for y, z in pts]
    b = [bm.verts.new((x1, y, z)) for y, z in pts]
    n = len(pts)
    for i in range(n if closed else n - 1):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))


def yz_prism(bm, pts, x0, x1):
    """A closed solid: a convex (y, z) polygon from x0 to x1."""
    a = [bm.verts.new((x0, y, z)) for y, z in pts]
    b = [bm.verts.new((x1, y, z)) for y, z in pts]
    faces = [bm.faces.new(a), bm.faces.new(list(reversed(b)))]
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        faces.append(bm.faces.new((a[i], b[i], b[j], a[j])))
    bmesh.ops.recalc_face_normals(bm, faces=faces)


def xy_prism(bm, pts, z0, z1):
    """A closed solid: a convex plan polygon (x, y) from z0 to z1."""
    a = [bm.verts.new((x, y, z0)) for x, y in pts]
    b = [bm.verts.new((x, y, z1)) for x, y in pts]
    faces = [bm.faces.new(a), bm.faces.new(list(reversed(b)))]
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        faces.append(bm.faces.new((a[i], b[i], b[j], a[j])))
    bmesh.ops.recalc_face_normals(bm, faces=faces)


def sweep(bm, path, section, side=1.0, caps=(False, False)):
    """A closed (d, z) section swept along a plan polyline (x, y) with mitred corners; d is measured to the left of
    the walking direction (towards +y for a path along +x). side = -1 mirrors the result to y < 0."""
    P = [Vector(p) for p in path]
    tang = [(P[i + 1] - P[i]).normalized() for i in range(len(P) - 1)]
    left = lambda t: Vector((-t.y, t.x))
    lefts = []
    for i in range(len(P)):
        if i == 0 or i == len(P) - 1:
            lefts.append(left(tang[0] if i == 0 else tang[-1]))
        else:
            n = (left(tang[i - 1]) + left(tang[i])).normalized()
            lefts.append(n / max(0.3, n.dot(left(tang[i]))))
    area = sum(d0 * z1 - d1 * z0 for (d0, z0), (d1, z1) in zip(section, section[1:] + section[:1]))
    rings = []
    for i in range(len(P)):
        rings.append([bm.verts.new((q.x, q.y * side, z)) for q, z in ((P[i] + lefts[i] * d, z) for d, z in section)])
    k = len(section)
    for i in range(len(P) - 1):
        n2 = left(tang[i])
        for j in range(k):
            jj = (j + 1) % k
            ed, ez = section[jj][0] - section[j][0], section[jj][1] - section[j][1]
            nd, nz = (ez, -ed) if area > 0 else (-ez, ed)
            kface(bm, (rings[i][j], rings[i][jj], rings[i + 1][jj], rings[i + 1][j]), (n2.x * nd, n2.y * nd * side, nz))
    for use, i, t, sgn in ((caps[0], 0, tang[0], -1), (caps[1], len(P) - 1, tang[-1], 1)):
        if use:
            kface(bm, rings[i], (t.x * sgn, t.y * sgn * side, 0.0))


def rounded(pts, rad, steps=4):
    """A polyline with its inner corners rounded (quadratic arcs of about rad)."""
    P = [Vector(p) for p in pts]
    out = [P[0]]
    for i in range(1, len(P) - 1):
        a, b, c = P[i - 1], P[i], P[i + 1]
        r = min(rad, (b - a).length * 0.45, (c - b).length * 0.45)
        p0, p2 = b + (a - b).normalized() * r, b + (c - b).normalized() * r
        for s in range(steps + 1):
            t = s / steps
            out.append(p0 * (1 - t) ** 2 + b * 2 * t * (1 - t) + p2 * t * t)
    out.append(P[-1])
    return out


def tube(bm, pts, r, seg=10, radii=None, caps=True):
    """A pipe / cable through points (parallel-transported rings), closed at both ends."""
    P = [Vector(p) for p in pts]
    n = len(P)
    T = []
    for i in range(n):
        if i == 0:
            t = P[1] - P[0]
        elif i == n - 1:
            t = P[-1] - P[-2]
        else:
            t = (P[i + 1] - P[i]).normalized() + (P[i] - P[i - 1]).normalized()
        T.append(t.normalized())
    up = Vector((0, 0, 1)) if abs(T[0].z) < 0.9 else Vector((1, 0, 0))
    N = T[0].cross(up).normalized()
    rings = []
    for i in range(n):
        N = (N - T[i] * N.dot(T[i])).normalized()
        B = T[i].cross(N)
        rr = radii[i] if radii else r
        rings.append([bm.verts.new(P[i] + (N * math.cos(2 * math.pi * k / seg) + B * math.sin(2 * math.pi * k / seg)) * rr)
                      for k in range(seg)])
    faces = []
    for i in range(n - 1):
        for k in range(seg):
            kk = (k + 1) % seg
            faces.append(bm.faces.new((rings[i][k], rings[i][kk], rings[i + 1][kk], rings[i + 1][k])))
    if caps:
        faces += [bm.faces.new(rings[0]), bm.faces.new(list(reversed(rings[-1])))]
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    return [v for ring in rings for v in ring]


def rot_box(bm, center, size, rot=(0.0, 0.0, 0.0)):
    from mathutils import Euler
    vs = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    bmesh.ops.scale(bm, vec=Vector(size), verts=vs)
    bmesh.ops.transform(bm, matrix=Euler(rot).to_matrix().to_4x4(), verts=vs)
    bmesh.ops.translate(bm, vec=Vector(center), verts=vs)


def ring_tube(bm, r_out, r_in, x0, x1, cy, cz, seg=24, inner=True):
    """A thick pipe end along +x at (cy, cz): outer and inner surfaces and the annulus at x1 (x0 is in the wall)."""
    o0, o1, i0, i1 = [], [], [], []
    for k in range(seg):
        a = 2 * math.pi * (k + 0.5) / seg
        c, s = math.cos(a), math.sin(a)
        o0.append(bm.verts.new((x0, cy + r_out * c, cz + r_out * s)))
        o1.append(bm.verts.new((x1, cy + r_out * c, cz + r_out * s)))
        i0.append(bm.verts.new((x0, cy + r_in * c, cz + r_in * s)))
        i1.append(bm.verts.new((x1, cy + r_in * c, cz + r_in * s)))
    for k in range(seg):
        kk = (k + 1) % seg
        a = 2 * math.pi * (k + 1) / seg
        radial = (0.0, math.cos(a), math.sin(a))
        kface(bm, (o0[k], o0[kk], o1[kk], o1[k]), radial)
        if inner:
            kface(bm, (i0[k], i0[kk], i1[kk], i1[k]), tuple(-c for c in radial))
        kface(bm, (o1[k], o1[kk], i1[kk], i1[k]), (1, 0, 0))


def valve_wheel(rust, metal, center, axis, R=0.2):
    """A hand wheel: rim (rust) and a cross of spokes (metal), turned to its axis ("X" / "Y")."""
    cx, cy, cz = center
    torus(rust, R, 0.022, center, axis=axis, seg=18, seg2=6)
    cyl(metal, 0.035, 0.07, center, axis=axis, seg=10)
    if axis == "Y":
        box(metal, cx - R, cx + R, cy - 0.012, cy + 0.012, cz - 0.014, cz + 0.014)
        box(metal, cx - 0.014, cx + 0.014, cy - 0.009, cy + 0.009, cz - R, cz + R)
    else:
        box(metal, cx - 0.012, cx + 0.012, cy - R, cy + R, cz - 0.014, cz + 0.014)
        box(metal, cx - 0.009, cx + 0.009, cy - 0.014, cy + 0.014, cz - R, cz + R)


def kit_export(filename):
    """Like finish_and_export (box UVs, 1 unit = 1 m), but rounded parts shade smooth (sharp above 35°)."""
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = o
        for x in bpy.context.scene.objects:
            x.select_set(x == o)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=False, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
        bm.faces.index_update()
        seen = set()
        for f in bm.faces:                       # closed pieces (tori, boxes, tubes) face outwards
            if f.index in seen:
                continue
            island, stack = [], [f]
            seen.add(f.index)
            while stack:
                g = stack.pop()
                island.append(g)
                for e in g.edges:
                    for h in e.link_faces:
                        if h.index not in seen:
                            seen.add(h.index)
                            stack.append(h)
            if all(not e.is_boundary for g in island for e in g.edges):
                bmesh.ops.recalc_face_normals(bm, faces=island)
        bm.to_mesh(o.data)
        bm.free()
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, filename + ".fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH"},
                             mesh_smooth_type="FACE", add_leaf_bones=False)
    clear()
    print("KIT", filename)


# --------------------------------------------------------------------------- the road: grate, frame, water

def beam(bm, c, yend, x0, x1, web_end=None):
    """A transverse I-beam under the grate centred at x = c, cut off at the piece's ends x0 / x1. yend(x) is how far
    its top flange reaches (into the edge rails), web_end how far the web and bottom flange go (to the channel wall)."""
    def plate(xa, xb, za, zb, ya, yb):
        xa, xb = max(xa, x0), min(xb, x1)
        if xb - xa > 1e-4:
            xy_prism(bm, [(xa, -ya), (xb, -yb), (xb, yb), (xa, ya)], za, zb)
    plate(c - 0.05, c + 0.05, -0.05, 0.006, yend(c - 0.05), yend(c + 0.05))   # top flange, 6 mm proud of the grate
    w = web_end if web_end is not None else yend(c)
    plate(c - 0.012, c + 0.012, -0.28, -0.05, w, w)
    plate(c - 0.05, c + 0.05, -0.32, -0.28, w, w)


def stringers(bm, ys, x0, x1, start=None):
    """Longitudinal bearers under the grate (seen through it); start(y) — where one may begin. They stop 6 mm short
    of the piece's ends, inside the half beams' webs (no faces in the joining plane)."""
    for y in ys:
        xa = max(x0 + 0.006, start(abs(y))) if start else x0 + 0.006
        if x1 - 0.006 - xa > 0.05:
            box(bm, xa, x1 - 0.006, y - 0.015, y + 0.015, -0.2, -0.02)


def kit_road(x0, x1, road, beam_xs, stringer_ys):
    """Straight road between x0 and x1: grate, edge rails, beams, stringers, water."""
    part("Grate", lambda bm: poly(bm, [(x0, -road, 0.0), (x1, -road, 0.0), (x1, road, 0.0), (x0, road, 0.0)]))

    def frame(bm):
        for s in (-1, 1):
            ya, yb = sorted((s * (road - 0.12), s * (road + 0.02)))
            box(bm, x0, x1, ya, yb, -0.07, 0.01)
        for c in beam_xs:
            beam(bm, c, lambda x: road - 0.12, x0, x1, web_end=road)
        stringers(bm, stringer_ys, x0, x1)
    part("Metal", frame).name = "MetalFrame"
    part("Water", lambda bm: poly(bm, [(x0, -road - 0.02, KIT_WATER), (x1, -road - 0.02, KIT_WATER),
                                       (x1, road + 0.02, KIT_WATER), (x0, road + 0.02, KIT_WATER)]))


def channel_pts(road, outer=None):
    """(y, z) polyline of the channel under the road: walkway tops (if outer), channel walls with a ledge under the
    edge rails, the bed; with outer it is a closed loop round the walkways' solid."""
    r = road
    right = [(r, -0.06), (r - 0.18, -0.06), (r - 0.18, -0.18), (r, -0.18), (r, KIT_BED)]
    left = [(-y, z) for y, z in reversed(right)]
    if outer is None:
        return [(r, 0.0)] + right + left + [(-r, 0.0)]
    return [(outer, KIT_WALK), (r, KIT_WALK)] + right + left + [(-r, KIT_WALK), (-outer, KIT_WALK), (-outer, -0.9), (outer, -0.9)]


# --------------------------------------------------------------------------- straight segments (6 m, four variants)

def kit_segment(variant, cut=False):
    """sewer_seg_<variant>[_cut]: 0 cable tray · 1 pipes and valves on the left (-Y) wall · 2 a niche with debris and a
    dead lamp on the right · 3 brick arches on pillars at x = 1 and 4. _cut: without the -Y wall and vault."""
    p, L = KIT, KIT_LEN
    keep = far_half if cut else (lambda pr: pr)
    niche = variant == 2
    NX0, NX1, NTOP, NBACK = 2.0, 4.2, 2.2, 4.8
    prof = kit_profile(p, KIT_WALK, extra=(NTOP,) if niche else ())
    top = p["wall"] + p["rise"]

    def vault(bm):
        ranges = [(0.0, NX0, prof), (NX0, NX1, prof[:-1]), (NX1, L, prof)] if niche else [(0.0, L, prof)]
        for xa, xb, pr in ranges:
            skin(bm, [(xa, keep(pr)), (xb, keep(pr))], p["wall"])
        if niche:  # the alcove in the right wall, walls turned into it
            poly(bm, [(NX0, NBACK, KIT_WALK), (NX1, NBACK, KIT_WALK), (NX1, NBACK, NTOP), (NX0, NBACK, NTOP)], (0, -1, 0))
            poly(bm, [(NX0, p["half"], KIT_WALK), (NX0, NBACK, KIT_WALK), (NX0, NBACK, NTOP), (NX0, p["half"], NTOP)], (1, 0, 0))
            poly(bm, [(NX1, p["half"], KIT_WALK), (NX1, NBACK, KIT_WALK), (NX1, NBACK, NTOP), (NX1, p["half"], NTOP)], (-1, 0, 0))
            poly(bm, [(NX0, p["half"], NTOP), (NX1, p["half"], NTOP), (NX1, NBACK, NTOP), (NX0, NBACK, NTOP)], (0, 0, -1))
        if cut:  # the section through the vault: crown cap and the outer surface of the far half
            strip(bm, [(0.0, top + KIT_THICK), (0.0, top)], 0.0, L)
            outer = kit_profile(p, KIT_WALK, d=KIT_THICK, extra=(NTOP,) if niche else ())
            ranges = [(0.0, NX0, outer), (NX0, NX1, outer[:-1]), (NX1, L, outer)] if niche else [(0.0, L, outer)]
            for xa, xb, pr in ranges:
                skin(bm, [(xa, far_half(pr)), (xb, far_half(pr))], p["wall"], inward=False)
    part("Brick", vault)

    rout, rin = kit_profile(p, KIT_WALK - 0.02), kit_profile(p, KIT_WALK - 0.02, d=-0.12)
    ends = [(0, (0, -1, 0))] if cut else []

    def end_ribs(bm):  # half ribs: two segments make a whole rib at the join
        rib(bm, keep(rout), keep(rin), 0.0, 0.2, p["wall"], front=False, ends=ends)
        rib(bm, keep(rout), keep(rin), L - 0.2, L, p["wall"], back=False, ends=ends)
    part("Stone", end_ribs).name = "StoneRibs"
    part("Concrete", lambda bm: strip(bm, channel_pts(KIT_ROAD, outer=3.75), 0.0, L, closed=True)).name = "ConcreteFloor"
    part("Stone", lambda bm: [box(bm, 0.0, L, *sorted((s * 2.58, s * 2.82)), 0.10, 0.26) for s in (-1, 1)]).name = "StoneCurb"
    kit_road(0.0, L, KIT_ROAD, [0.0, 1.5, 3.0, 4.5, 6.0], [-1.3, 0.0, 1.3])

    if variant == 0:
        seg_tray(L)
    elif variant == 1 and not cut:
        seg_pipes(L)
    elif variant == 2:
        seg_niche(NX0, NX1, NTOP, NBACK)
    elif variant == 3:
        seg_arches(cut)
    kit_export(f"sewer_seg_{variant}" + ("_cut" if cut else ""))


# --------------------------------------------------------------------------- the alcove with the shaft up to the house

# A clean alcove in the +Y (Unity +Z, left) wall beside the parked car, and over it a brick shaft up to the lobby's floor
# (Unity: Basement — the shaft's middle at x 3.0, y 4.345; its hole in the floor is 1.5 m square; the floor is 4.9 m
# over the road). The ladder stands against the alcove's back wall and goes up the shaft.
ALC_X0, ALC_X1, ALC_TOP, ALC_BACK = 1.8, 4.2, 2.4, 5.09
SHAFT_X0, SHAFT_X1, SHAFT_Y0, SHAFT_TOP = 2.26, 3.74, 3.6, 4.89
LADDER_X, LADDER_Y = 3.0, ALC_BACK - 0.25


def kit_alcove():
    """sewer_seg_alcove: a plain 6 m segment with the alcove and the shaft. Parts: the tunnel like sewer_seg_*, BrickAlcove
    (its walls, the ceiling round the shaft, the shaft), StoneAlcove (lintel and jambs), ConcreteAlcove (its floor), MetalLadder
    (up to just under the lid), RustLamp + GlowLamp (a caged bulb on the shaft's wall: the game puts the light there),
    MetalShutLid (the lid seen from below: the sewer shows it, the house's basement hides it — the real lid is in its floor)."""
    p, L = KIT, KIT_LEN
    prof = kit_profile(p, KIT_WALK, extra=(ALC_TOP,))

    def vault(bm):
        for xa, xb, pr in ((0.0, ALC_X0, prof), (ALC_X0, ALC_X1, prof[:-1]), (ALC_X1, L, prof)):
            skin(bm, [(xa, pr), (xb, pr)], p["wall"])
    part("Brick", vault)

    def alcove(bm):
        W, H, B = KIT_WALK, p["half"], ALC_BACK
        poly(bm, [(ALC_X0, B, W), (ALC_X1, B, W), (ALC_X1, B, ALC_TOP), (ALC_X0, B, ALC_TOP)], (0, -1, 0))      # back wall
        poly(bm, [(ALC_X0, H, W), (ALC_X0, B, W), (ALC_X0, B, ALC_TOP), (ALC_X0, H, ALC_TOP)], (1, 0, 0))       # sides
        poly(bm, [(ALC_X1, H, W), (ALC_X1, B, W), (ALC_X1, B, ALC_TOP), (ALC_X1, H, ALC_TOP)], (-1, 0, 0))
        for xa, xb in ((ALC_X0, SHAFT_X0), (SHAFT_X1, ALC_X1)):                                                  # ceiling round the shaft
            poly(bm, [(xa, H, ALC_TOP), (xb, H, ALC_TOP), (xb, B, ALC_TOP), (xa, B, ALC_TOP)], (0, 0, -1))
        # the shaft: four walls from the alcove's ceiling up to the house's floor
        poly(bm, [(SHAFT_X0, B, ALC_TOP), (SHAFT_X1, B, ALC_TOP), (SHAFT_X1, B, SHAFT_TOP), (SHAFT_X0, B, SHAFT_TOP)], (0, -1, 0))
        poly(bm, [(SHAFT_X0, SHAFT_Y0, ALC_TOP), (SHAFT_X1, SHAFT_Y0, ALC_TOP), (SHAFT_X1, SHAFT_Y0, SHAFT_TOP), (SHAFT_X0, SHAFT_Y0, SHAFT_TOP)], (0, 1, 0))
        poly(bm, [(SHAFT_X0, SHAFT_Y0, ALC_TOP), (SHAFT_X0, B, ALC_TOP), (SHAFT_X0, B, SHAFT_TOP), (SHAFT_X0, SHAFT_Y0, SHAFT_TOP)], (1, 0, 0))
        poly(bm, [(SHAFT_X1, SHAFT_Y0, ALC_TOP), (SHAFT_X1, B, ALC_TOP), (SHAFT_X1, B, SHAFT_TOP), (SHAFT_X1, SHAFT_Y0, SHAFT_TOP)], (-1, 0, 0))
    part("Brick", alcove).name = "BrickAlcove"

    def stone(bm):
        box(bm, ALC_X0 - 0.15, ALC_X1 + 0.15, 3.48, 3.7, ALC_TOP - 0.05, ALC_TOP + 0.24)
        for x0, x1 in ((ALC_X0 - 0.15, ALC_X0 + 0.05), (ALC_X1 - 0.05, ALC_X1 + 0.15)):
            box(bm, x0, x1, 3.48, 3.7, KIT_WALK - 0.02, ALC_TOP - 0.04)
    part("Stone", stone).name = "StoneAlcove"
    part("Concrete", lambda bm: poly(bm, [(ALC_X0, 3.75, KIT_WALK), (ALC_X1, 3.75, KIT_WALK), (ALC_X1, ALC_BACK, KIT_WALK), (ALC_X0, ALC_BACK, KIT_WALK)])).name = "ConcreteAlcove"

    def ladder(bm):
        top = SHAFT_TOP - 0.05
        for x in (LADDER_X - 0.28, LADDER_X + 0.28):
            box(bm, x - 0.03, x + 0.03, LADDER_Y - 0.03, LADDER_Y + 0.03, KIT_WALK, top)
            for z in (0.7, 2.0, 3.3, 4.5):                                       # brackets to the back wall
                box(bm, x - 0.03, x + 0.03, LADDER_Y + 0.03, ALC_BACK, z - 0.03, z + 0.03)
        z = KIT_WALK + 0.3
        while z < top - 0.1:
            cyl(bm, 0.018, 0.56, (LADDER_X, LADDER_Y, z), axis="X", seg=8)
            z += 0.3
    part("Metal", ladder).name = "MetalLadder"

    def lamp_cage(bm):
        c = (SHAFT_X0, 4.1, 3.7)
        box(bm, c[0], c[0] + 0.05, c[1] - 0.09, c[1] + 0.09, c[2] - 0.12, c[2] + 0.12)                       # wall plate
        torus(bm, 0.075, 0.008, (c[0] + 0.12, c[1], c[2]), axis="X", seg=14, seg2=4)
        for k in range(4):
            a = math.pi * k / 2 + math.pi / 4
            cy, cz = c[1] + 0.075 * math.cos(a), c[2] + 0.075 * math.sin(a)
            box(bm, c[0] + 0.05, c[0] + 0.13, cy - 0.007, cy + 0.007, cz - 0.007, cz + 0.007)
    part("Rust", lamp_cage).name = "RustLamp"
    part("Glow", lambda bm: cyl(bm, 0.055, 0.08, (SHAFT_X0 + 0.09, 4.1, 3.7), axis="X", seg=14, r2=0.04)).name = "GlowLamp"

    def lid(bm):
        box(bm, SHAFT_X0, SHAFT_X1, SHAFT_Y0, ALC_BACK, SHAFT_TOP - 0.05, SHAFT_TOP - 0.01)
        for k in range(4):
            y = SHAFT_Y0 + 0.25 + k * (ALC_BACK - SHAFT_Y0 - 0.5) / 3
            box(bm, SHAFT_X0 + 0.08, SHAFT_X1 - 0.08, y - 0.025, y + 0.025, SHAFT_TOP - 0.1, SHAFT_TOP - 0.05)
    part("Metal", lid).name = "MetalShutLid"

    rout, rin = kit_profile(p, KIT_WALK - 0.02), kit_profile(p, KIT_WALK - 0.02, d=-0.12)

    def end_ribs(bm):
        rib(bm, rout, rin, 0.0, 0.2, p["wall"], front=False)
        rib(bm, rout, rin, L - 0.2, L, p["wall"], back=False)
    part("Stone", end_ribs).name = "StoneRibs"
    part("Concrete", lambda bm: strip(bm, channel_pts(KIT_ROAD, outer=3.75), 0.0, L, closed=True)).name = "ConcreteFloor"
    part("Stone", lambda bm: [box(bm, 0.0, L, *sorted((s * 2.58, s * 2.82)), 0.10, 0.26) for s in (-1, 1)]).name = "StoneCurb"
    kit_road(0.0, L, KIT_ROAD, [0.0, 1.5, 3.0, 4.5, 6.0], [-1.3, 0.0, 1.3])
    kit_export("sewer_seg_alcove")


def seg_tray(L):
    """A cable tray on the right (+Y) wall and a culvert outlet in the channel wall below the far walkway."""
    def tray(bm):
        X0, X1, yi, yw, z = 0.45, L - 0.45, 3.16, 3.62, 1.98
        box(bm, X0, X1, yi, yw - 0.01, z - 0.02, z)                  # tray bottom
        box(bm, X0, X1, yi, yi + 0.025, z - 0.02, z + 0.12)          # lip
        for x in (0.75, 2.25, 3.75, 5.25):
            box(bm, x - 0.03, x + 0.03, yi - 0.02, yw - 0.02, z - 0.09, z - 0.015)   # arm
            box(bm, x - 0.04, x + 0.04, 3.56, yw + 0.02, z - 0.42, z + 0.02)          # wall plate
            rot_box(bm, (x, 3.4, z - 0.24), (0.04, 0.035, 0.42), (math.radians(-40), 0.0, 0.0))  # strut
        box(bm, 3.53, 3.97, 2.56, 2.64, -0.44, -0.4)                 # culvert sill
    part("Metal", tray).name = "MetalTray"

    def cables(bm):
        for y, r, dz in ((3.28, 0.032, 0.0), (3.38, 0.024, 0.0), (3.47, 0.036, 0.0), (3.36, 0.02, 0.05)):
            zc = 1.98 + r - 0.004 + dz
            tube(bm, rounded([(0.18, 3.75, zc), (0.32, y, zc), (L - 0.32, y, zc), (L - 0.18, 3.75, zc)], 0.12), r, seg=8)
        box(bm, 3.55, 3.95, 2.585, 2.62, -0.40, -0.2)                # the culvert's dark mouth
    part("Dark", cables).name = "DarkCables"

    def grille(bm):
        for k in range(5):
            x = 3.59 + k * 0.08
            box(bm, x - 0.01, x + 0.01, 2.572, 2.595, -0.41, -0.19)
        box(bm, 3.53, 3.97, 2.565, 2.6, -0.21, -0.18)
    part("Rust", grille).name = "RustGrille"


def seg_pipes(L):
    """Pipes and valves along the left (-Y) wall: a big rusty main with flanges, a thin pipe above, a drop pipe with a
    valve down into the walkway; both mains turn into the wall before the segment ends."""
    zb, yb, rb = 1.75, -3.28, 0.17
    zs, ys, rs = 2.3, -3.44, 0.075
    rust, metal = bmesh.new(), bmesh.new()
    tube(rust, rounded([(0.4, -3.9, zb), (0.4, yb, zb), (L - 0.4, yb, zb), (L - 0.4, -3.9, zb)], 0.32, 6), rb, seg=16)
    for x in (1.7, 4.3):
        cyl(rust, rb + 0.045, 0.07, (x, yb, zb), axis="X", seg=16)
        for k in range(8):
            a = 2 * math.pi * (k + 0.5) / 8
            cyl(metal, 0.016, 0.1, (x, yb + (rb + 0.025) * math.cos(a), zb + (rb + 0.025) * math.sin(a)), axis="X", seg=6)
    tube(metal, rounded([(0.75, -3.9, zs), (0.75, ys, zs), (L - 0.75, ys, zs), (L - 0.75, -3.9, zs)], 0.2, 5), rs, seg=10)
    # drop pipe from the main to the walkway, with a valve body and a wheel
    tube(rust, [(3.0, yb, zb), (3.0, yb, KIT_WALK - 0.05)], 0.07, seg=12)
    cyl(rust, 0.12, 0.3, (3.0, yb, 0.95), seg=14)
    cyl(metal, 0.03, 0.3, (3.0, yb + 0.2, 0.95), axis="Y", seg=8)
    valve_wheel(rust, metal, (3.0, yb + 0.33, 0.95), "Y", 0.17)
    box(rust, 2.86, 3.14, yb - 0.14, yb + 0.14, KIT_WALK - 0.02, KIT_WALK + 0.03)   # floor flange
    # a stem valve on the main
    cyl(metal, 0.028, 0.38, (2.0, yb + 0.29, zb), axis="Y", seg=8)
    cyl(rust, 0.09, 0.12, (2.0, yb + 0.2, zb), axis="Y", seg=12)
    valve_wheel(rust, metal, (2.0, yb + 0.5, zb), "Y", 0.22)
    # wall brackets and clamps
    for x in (1.0, 3.6, 5.0):
        box(metal, x - 0.035, x + 0.035, -3.62, yb - rb + 0.02, zb - 0.045, zb + 0.045)
        torus(metal, rb + 0.012, 0.018, (x, yb, zb), axis="X", seg=18, seg2=5)
        box(metal, x - 0.03, x + 0.03, -3.62, ys - rs + 0.015, zs - 0.03, zs + 0.03)
        torus(metal, rs + 0.01, 0.014, (x, ys, zs), axis="X", seg=12, seg2=4)
    # a gauge on the thin pipe
    cyl(metal, 0.016, 0.12, (4.4, ys + 0.1, zs), axis="Y", seg=6)
    cyl(metal, 0.07, 0.05, (4.4, ys + 0.18, zs), axis="Y", seg=14)
    for name, bm in (("Rust", rust), ("Metal", metal)):
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        obj_from(bm, name).name = name + "Pipes"


def seg_niche(NX0, NX1, NTOP, NBACK):
    """An alcove in the right wall: a stone lintel and jambs, a rubble pile with broken bricks, a barrel on its side,
    a leaning slab and plank, a dead bulkhead lamp with a hanging cable."""
    import random
    rnd = random.Random(7)
    W = KIT_WALK

    def stone(bm):
        box(bm, NX0 - 0.15, NX1 + 0.15, 3.48, 3.7, NTOP - 0.05, NTOP + 0.24)
        for x0, x1 in ((NX0 - 0.15, NX0 + 0.05), (NX1 - 0.05, NX1 + 0.15)):
            box(bm, x0, x1, 3.48, 3.7, W - 0.02, NTOP - 0.04)
    part("Stone", stone).name = "StoneNiche"
    part("Concrete", lambda bm: poly(bm, [(NX0, 3.75, W), (NX1, 3.75, W), (NX1, NBACK, W), (NX0, NBACK, W)])).name = "ConcreteNiche"

    def rubble(bm):
        for _ in range(9):                         # broken concrete chunks heaped under the bricks
            a = rnd.uniform(0, 2 * math.pi)
            d = rnd.uniform(0.0, 0.45)
            sx, sy, sz = rnd.uniform(0.22, 0.4), rnd.uniform(0.18, 0.3), rnd.uniform(0.08, 0.16)
            h = 0.2 * max(0.0, 1 - d / 0.5)
            rot_box(bm, (3.25 + d * math.cos(a), 4.4 + 0.6 * d * math.sin(a), W + h + sz * 0.3), (sx, sy, sz),
                    (rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35), rnd.uniform(0, math.pi)))
        rot_box(bm, (3.92, 4.62, 0.62), (0.62, 0.08, 1.02), (-0.3, 0.0, 0.0))   # broken slab against the back wall
    part("Concrete", rubble).name = "ConcreteRubble"

    def bricks(bm):
        for _ in range(16):
            a = rnd.uniform(0, 2 * math.pi)
            d = rnd.uniform(0.1, 0.75)
            x, y = 3.25 + d * math.cos(a), 4.38 + 0.6 * d * math.sin(a)
            y = min(max(y, 3.85), NBACK - 0.12)
            x = min(max(x, NX0 + 0.12), NX1 - 0.12)
            h = 0.2 * max(0.0, 1 - d / 0.55)
            rot_box(bm, (x, y, W + h + 0.02), (0.22, 0.1, 0.065),
                    (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.4, 0.4), rnd.uniform(0, math.pi)))
    part("Brick", bricks).name = "BrickDebris"
    part("Wood", lambda bm: rot_box(bm, (2.25, 4.62, 0.88), (0.2, 0.04, 1.55), (-0.25, 0.0, 0.12))).name = "WoodPlank"

    def barrel(bm):
        cyl(bm, 0.28, 0.85, (2.75, 4.1, W + 0.27), axis="X", seg=16)
        for x in (2.5, 3.0):
            torus(bm, 0.283, 0.016, (x, 4.1, W + 0.27), axis="X", seg=16, seg2=4)
        # the dead lamp's cage on the back wall
        torus(bm, 0.14, 0.01, (3.1, NBACK - 0.2, 1.75), axis="Y", seg=14, seg2=4)
        for k in range(6):
            a = math.pi * k / 3
            box(bm, 3.1 + 0.14 * math.cos(a) - 0.008, 3.1 + 0.14 * math.cos(a) + 0.008, NBACK - 0.21, NBACK - 0.06,
                1.75 + 0.14 * math.sin(a) - 0.008, 1.75 + 0.14 * math.sin(a) + 0.008)
    part("Rust", barrel).name = "RustBarrel"
    part("Metal", lambda bm: cyl(bm, 0.17, 0.09, (3.1, NBACK - 0.03, 1.75), axis="Y", seg=16)).name = "MetalLamp"

    def dead(bm):
        cyl(bm, 0.12, 0.13, (3.1, NBACK - 0.13, 1.75), axis="Y", seg=16, r2=0.1)   # dark, broken glass
        tube(bm, rounded([(3.22, NBACK - 0.05, 1.62), (3.36, NBACK - 0.12, 1.05), (3.3, NBACK - 0.3, 0.45),
                          (3.42, NBACK - 0.45, W + 0.012), (3.7, NBACK - 0.55, W + 0.012)], 0.2), 0.012, seg=6)
    part("Dark", dead).name = "DarkLamp"


def seg_arches(cut):
    """Transverse brick arches at x = 1 and 4 on brick pillars with stone plinths, imposts and a keystone."""
    p = KIT
    keep = far_half if cut else (lambda pr: pr)
    outer = kit_profile(p, 2.42)
    inner = kit_profile(dict(half=3.15, wall=2.6, rise=1.15), 2.42)
    sides = (1,) if cut else (-1, 1)

    def arches(bm):
        for c in (1.0, 4.0):
            rib(bm, keep(outer), keep(inner), c - 0.3, c + 0.3, 2.6)   # a cut crown is closed by the keystone
            for s in sides:
                box(bm, c - 0.28, c + 0.28, *sorted((s * 3.2, s * 3.61)), KIT_WALK, 2.4)
    part("Brick", arches).name = "BrickArches"

    def stone(bm):
        for c in (1.0, 4.0):
            for s in sides:
                box(bm, c - 0.34, c + 0.34, *sorted((s * 3.12, s * 3.62)), KIT_WALK - 0.02, 0.4)
                box(bm, c - 0.34, c + 0.34, *sorted((s * 3.08, s * 3.62)), 2.38, 2.52)
            box(bm, c - 0.32, c + 0.32, 0.0 if cut else -0.17, 0.17, 3.68, 4.22)
    part("Stone", stone).name = "StoneArches"


# --------------------------------------------------------------------------- the gate: frame, shutter, lever

def kit_gate_frame():
    """A heavy concrete portal at x = 0 (x ∈ [-0.35, 0.35]) with a clear opening y ∈ [-2.8, 2.8], z ∈ [0, 3.2]: steel
    guide channels for the shutter, a steel header on both faces, hazard stripes (part Stripe: yellow) and two hazard
    lamps on the -X face; a housing above the vault hides the raised shutter (up to z = 6.7)."""
    frame = prism("ConcreteFrame", kit_profile(KIT, KIT_WALK - 0.02, d=0.06), -0.35, 0.35)
    subtract(frame, part("cut", lambda bm: box(bm, -0.6, 0.6, -GATE_SLOT, GATE_SLOT, -1.0, GATE_TOP)))
    part("Concrete", lambda bm: box(bm, -0.3, 0.3, -3.05, 3.05, GATE_TOP + 0.05, 6.7)).name = "ConcreteHousing"

    def steel(bm):
        for s in (-1, 1):
            for xa, xb in ((-0.16, -0.09), (0.09, 0.16)):   # guide flanges: the door's edges run between them
                box(bm, xa, xb, *sorted((s * GATE_HALF, s * (GATE_SLOT + 0.02))), KIT_WALK - 0.01, GATE_TOP + 0.02)
            box(bm, -0.155, 0.155, *sorted((s * (GATE_SLOT - 0.01), s * (GATE_SLOT + 0.04))), KIT_WALK - 0.012, GATE_TOP + 0.015)
            box(bm, *sorted((s * 0.33, s * 0.47)), -2.98, 2.98, GATE_TOP + 0.04, 3.46)           # header
            box(bm, *sorted((s * 0.33, s * 0.42)), *sorted((s * 3.0, s * 3.4)), KIT_WALK - 0.01, KIT_WALK + 0.1)  # kick plate
        for y in (-1.9, 1.9):                                                                      # lamp housings
            cyl(bm, 0.13, 0.1, (-0.39, y, 3.7), axis="X", seg=16)
    part("Metal", steel).name = "MetalGate"

    def stripes(bm):
        P, S = 0.32, 0.16
        for face, sx in ((-0.35, -1), (0.35, 1)):
            xa, xb = sorted((face + sx * 0.008, face - sx * 0.002))
            for s in (-1, 1):                     # jambs: y ∈ ±[2.98, 3.3]
                ya, yb = s * 2.98, s * 3.3
                z = 0.25
                while z + 0.32 + S <= 2.95:
                    yz_prism(bm, [(ya, z), (yb, z + 0.32), (yb, z + 0.32 + S), (ya, z + S)], xa, xb)
                    z += P
            hxa, hxb = sorted((sx * 0.47 + sx * 0.006, sx * 0.47 - sx * 0.002))
            y = -2.9
            while y + S + 0.16 <= 2.9:            # header: slanted stripes
                yz_prism(bm, [(y, 3.27), (y + S, 3.27), (y + S + 0.16, 3.43), (y + 0.16, 3.43)], hxa, hxb)
                y += P
    part("Stripe", stripes)

    def lamps(bm):
        for y in (-1.9, 1.9):
            cyl(bm, 0.1, 0.15, (-0.505, y, 3.7), axis="X", seg=16, r2=0.075)
    part("Glow", lamps).name = "GlowLamps"

    def cages(bm):
        for y in (-1.9, 1.9):
            torus(bm, 0.11, 0.01, (-0.565, y, 3.7), axis="X", seg=14, seg2=4)
            for k in range(4):
                a = math.pi * k / 2 + math.pi / 4
                cy, cz = y + 0.11 * math.cos(a), 3.7 + 0.11 * math.sin(a)
                box(bm, -0.57, -0.43, cy - 0.008, cy + 0.008, cz - 0.008, cz + 0.008)
    part("Rust", cages).name = "RustCages"
    kit_export("sewer_gate_frame")


def kit_gate_door():
    """The shutter: 5.8 (y) x 3.3 (z) x 0.15 (x), origin at its bottom centre; horizontal ribs, vertical stiffeners,
    a rusty bottom bar and a few rusty ribs. The game slides it up into the frame's housing."""
    W2, H = 2.9, 3.3

    def steel(bm):
        box(bm, -0.04, 0.04, -W2 + 0.005, W2 - 0.005, 0.11, H - 0.11)     # sheet
        box(bm, -0.075, 0.075, -W2, W2, H - 0.12, H)                      # top bar
        for y in (-1.95, 0.0, 1.95):
            box(bm, -0.075, 0.075, y - 0.05, y + 0.05, 0.12, H - 0.12)    # stiffeners
        for k in range(10):
            if k not in (0, 6):
                z = 0.33 + 0.3 * k
                box(bm, -0.062, 0.062, -W2 + 0.02, W2 - 0.02, z - 0.03, z + 0.03)
    part("Metal", steel).name = "MetalDoor"

    def rust(bm):
        box(bm, -0.075, 0.075, -W2, W2, 0.0, 0.12)                        # bottom bar
        for k in (0, 6):
            z = 0.33 + 0.3 * k
            box(bm, -0.062, 0.062, -W2 + 0.02, W2 - 0.02, z - 0.03, z + 0.03)
    part("Rust", rust).name = "RustDoor"
    kit_export("sewer_gate_door")


def kit_lever():
    """A wall lever box facing -Y, origin at the back of the box at its centre; the handle AccentHandle has its origin
    at its pivot (axis X) and points up: the game swings it about its local X."""
    from mathutils import Matrix

    def body(bm):
        box(bm, -0.15, 0.15, -0.14, 0.0, -0.22, 0.22)
        box(bm, -0.17, 0.17, -0.155, -0.004, 0.215, 0.25)                # rain hood
        for z in (-0.18, 0.18):
            box(bm, -0.19, 0.19, -0.025, -0.002, z - 0.02, z + 0.02)      # mounting lugs
        box(bm, -0.05, 0.05, -0.175, -0.13, -0.045, 0.045)                # pivot boss
    part("Metal", body).name = "MetalLever"
    part("Dark", lambda bm: box(bm, -0.03, 0.03, -0.146, -0.135, -0.17, 0.17)).name = "DarkSlot"
    pivot = Vector((0.0, -0.19, 0.0))

    def handle(bm):
        cyl(bm, 0.035, 0.1, tuple(pivot), axis="X", seg=12)
        box(bm, -0.014, 0.014, pivot.y - 0.015, pivot.y + 0.015, 0.0, 0.3)
        cyl(bm, 0.026, 0.17, (0.0, pivot.y, 0.31), axis="X", seg=12)
    h = part("AccentHandle", handle)
    h.data.transform(Matrix.Translation(-pivot))
    h.location = pivot
    kit_export("sewer_lever")


# --------------------------------------------------------------------------- the junction hall (x 0..16)

def hub_edge(x):
    """Half width of the hub's road (its walkways' inner edge) at x."""
    if x <= HUB_F0:
        return KIT_ROAD
    if x <= HUB_F1:
        return KIT_ROAD + (HUB_EDGE - KIT_ROAD) * (x - HUB_F0) / (HUB_F1 - HUB_F0)
    return HUB_EDGE if x < HUB_WALK_END else KIT_HUB["half"]


def hub_pieces(m):
    """Convex plan pieces covering the hub's road and channel, grown by m into the walls; the road runs on through
    the gaps in the walkways out to the side openings' mouths (y = ±HUB_SIDE_MOUTH)."""
    r0, r1, h = KIT_ROAD + m, HUB_EDGE + m, KIT_HUB["half"] + m
    g0, g1 = HUB_SIDE_X - KIT_BRANCH["half"] - m, HUB_SIDE_X + KIT_BRANCH["half"] + m
    return [[(0.0, -r0), (HUB_F0, -r0), (HUB_F0, r0), (0.0, r0)],
            [(HUB_F0, -r0), (HUB_F1, -r1), (HUB_F1, r1), (HUB_F0, r0)],
            [(HUB_F1, -r1), (g0, -r1), (g1, -r1), (HUB_WALK_END, -r1),
             (HUB_WALK_END, r1), (g1, r1), (g0, r1), (HUB_F1, r1)],   # g0 / g1: shared corners with the side pieces
            [(HUB_WALK_END, -h), (HUB_LEN, -h), (HUB_LEN, h), (HUB_WALK_END, h), (HUB_WALK_END, r1), (HUB_WALK_END, -r1)],
            [(g0, -HUB_SIDE_MOUTH), (g1, -HUB_SIDE_MOUTH), (g1, -r1), (g0, -r1)],
            [(g0, r1), (g1, r1), (g1, HUB_SIDE_MOUTH), (g0, HUB_SIDE_MOUTH)]]


def side_frame(s):
    """Matrix from an opening's own frame (x out of the hall from its mouth at x = 0, as a branch tunnel starts; y
    across, z up — the frame of sewer_branch and sewer_plug) to the hub's frame, for the side opening at y = s * 8."""
    from mathutils import Matrix
    return Matrix.Translation((HUB_SIDE_X, s * HUB_SIDE_MOUTH, 0.0)) @ Matrix.Rotation(s * math.pi / 2, 4, "Z")


def at_sides(bm, build):
    """build(bm) in an opening's own frame, once per side opening, moved to it."""
    for s in (-1, 1):
        n0 = len(bm.verts)
        build(bm)
        bm.verts.ensure_lookup_table()
        bmesh.ops.transform(bm, matrix=side_frame(s), verts=bm.verts[n0:])


def kit_hub():
    """sewer_hub: from the segment's section at x = 0 a flare (x 0.5..5.5) to the hall (half width 8, vault top 6);
    the grate road over the full width, walkways along the long walls up to x = 14.6. Five arched openings of the
    branch profile (4.6 x 3.8 above the road, down to the channel bed), each in a 0.6 m wall with a stone frame on
    its inner face and a sign plate (z 4.1..4.75) over it; a branch tunnel (or sewer_plug) starts at the mouth:
      * three in the end wall (x 15.4..16): centres y = -5.5, 0, 5.5, mouths at x = 16, facing +x; pillars between;
      * two in the long walls: centre x = 10, inner faces y = ±8, mouths at y = ±8.6, facing ±y. The walkways stop
        at them (x 7.7..12.3 is open) and the grate road runs straight out at z = 0; the channel, its ledges and the
        edge rails turn out along the side road. Over each one the wall stands up to z 4.93 (a niche in the vault,
        x 7.3..12.7) to carry the plate. The hall's second rib moved from x = 10 to 12.9, the pipes and junction
        boxes on the long walls to x 13.65..15.05 and 6.25..6.95."""
    prof_s = kit_profile(KIT, KIT_WALK)
    prof_h = kit_profile(KIT_HUB, KIT_WALK)
    prof_e = kit_profile(KIT_HUB, KIT_BOTTOM, extra=(KIT_WALK,))
    k = HUB_NICHE_ARC + 1                         # prof_h = [foot, arc 0..KIT_ARC, foot]
    prof_n = prof_h[k:len(prof_h) - k]            # the vault over the niches: arc vertex HUB_NICHE_ARC to its mirror
    NX0, NX1 = HUB_NICHE
    ny, nz = abs(prof_h[k][0]), prof_h[k][1]      # the niche's top edge on the vault (6.47, 4.93)
    W, B = KIT_HUB["half"], KIT_BRANCH["half"]
    g0, g1 = HUB_SIDE_X - B, HUB_SIDE_X + B       # the side openings' jambs = the gap in the walkways

    def vault(bm):
        skin(bm, [(0.0, prof_s), (HUB_F0, prof_s), (HUB_F1, prof_h), (NX0, prof_h)], 3.0)
        skin(bm, [(NX0, prof_n), (NX1, prof_n)], 3.0)
        skin(bm, [(NX1, prof_h), (HUB_WALK_END, prof_h)], 3.0)
        skin(bm, [(HUB_WALK_END, prof_e), (HUB_LEN, prof_e)], 3.0)
        arc = prof_h[1:k + 1]                     # (-8, 3.4) .. (-6.47, 4.93): the vault the niche cuts away
        for s in (-1, 1):
            poly(bm, [(NX0, s * ny, nz), (NX1, s * ny, nz), (NX1, s * W, nz), (NX0, s * W, nz)], (0, 0, -1))  # ceiling
            for x, nx in ((NX0, 1), (NX1, -1)):   # sides: fans from the niche's top back corner, turned into it
                c = bm.verts.new((x, s * W, nz))
                vs = [bm.verts.new((x, s * abs(y), z)) for y, z in arc]
                for a, b in zip(vs, vs[1:]):
                    kface(bm, (c, a, b), (nx, 0, 0))
    part("Brick", vault)

    end = prism("BrickEnd", kit_profile(KIT_HUB, KIT_BOTTOM - 0.05, d=0.06), HUB_WALL_X, HUB_LEN)
    for y in HUB_OPEN_Y:
        subtract(end, prism("cut", kit_profile(KIT_BRANCH, KIT_BED - 0.03), HUB_WALL_X - 0.2, HUB_LEN + 0.2, y_off=y))
    # the side walls under the niches, 0.6 m thick, from the walkway's top (the road's channel walls are below)
    side = part("BrickSide", lambda bm: at_sides(bm, lambda b: box(b, -0.6, 0.0, HUB_SIDE_X - NX1, HUB_SIDE_X - NX0, KIT_WALK, nz)))
    for s in (-1, 1):
        cut = prism("cut", kit_profile(KIT_BRANCH, KIT_BED - 0.03), -0.8, 0.2)
        cut.data.transform(side_frame(s))
        subtract(side, cut)

    def ribs(bm):
        rib(bm, kit_profile(KIT, KIT_WALK - 0.02), kit_profile(KIT, KIT_WALK - 0.02, d=-0.12), 0.0, 0.2, KIT["wall"], front=False)
        for x in (HUB_F1, HUB_SIDE_RIB):
            rib(bm, kit_profile(KIT_HUB, KIT_WALK - 0.02), kit_profile(KIT_HUB, KIT_WALK - 0.02, d=-0.18), x, x + 0.5, KIT_HUB["wall"])
        for y in HUB_OPEN_Y:   # frames round the openings
            o = [(yy + y, z) for yy, z in kit_profile(KIT_BRANCH, KIT_BED, d=0.22)]
            i = [(yy + y, z) for yy, z in kit_profile(KIT_BRANCH, KIT_BED)]
            rib(bm, o, i, HUB_WALL_X - 0.15, HUB_WALL_X, KIT_BRANCH["wall"], back=False, yc=y, outside=True)
        for y in (-2.75, 2.75):  # pillars between the openings
            box(bm, 15.1, HUB_WALL_X + 0.02, y - 0.28, y + 0.28, KIT_BED, 4.32)
            box(bm, 15.02, HUB_WALL_X + 0.03, y - 0.35, y + 0.35, 4.3, 4.46)
            box(bm, 15.04, HUB_WALL_X + 0.04, y - 0.33, y + 0.33, -0.065, 0.3)
        o, i = kit_profile(KIT_BRANCH, KIT_WALK, d=0.22), kit_profile(KIT_BRANCH, KIT_WALK)
        # the side frames stand on the walkways' ends (open feet: the walkway's top closes them)
        at_sides(bm, lambda b: rib(b, o, i, -0.75, -0.6, KIT_BRANCH["wall"], back=False, outside=True))
    part("Stone", ribs).name = "StoneRibs"

    start = [(0.0, KIT_ROAD), (HUB_F0, KIT_ROAD), (HUB_F1, HUB_EDGE)]
    walk_a, walk_b = start + [(g0, HUB_EDGE)], [(g1, HUB_EDGE), (HUB_WALK_END, HUB_EDGE)]
    edge_a, edge_b = walk_a + [(g0, HUB_SIDE_MOUTH)], [(g1, HUB_SIDE_MOUTH)] + walk_b   # turn out along the side road

    def walkways(bm):
        body = [(0.0, KIT_BOTTOM), (W - HUB_EDGE, KIT_BOTTOM), (W - HUB_EDGE, KIT_WALK), (0.0, KIT_WALK)]
        ledge = [(-0.18, -0.18), (0.02, -0.18), (0.02, -0.06), (-0.18, -0.06)]
        for s in (-1, 1):
            sweep(bm, walk_a, body, s, (False, True))
            sweep(bm, walk_b, body, s, (True, True))
            sweep(bm, edge_a, ledge, s)
            sweep(bm, edge_b, ledge, s, (False, True))
        for pc in hub_pieces(0.02):
            poly(bm, [(x, y, KIT_BED) for x, y in pc])
        # the side road's channel walls inside the wall (the walkways' ends are the walls up to y = ±8)
        at_sides(bm, lambda b: [poly(b, [(-0.6, y, KIT_BED), (0.0, y, KIT_BED), (0.0, y, KIT_WALK), (-0.6, y, KIT_WALK)],
                                     (0, -y, 0)) for y in (-B, B)])
    part("Concrete", walkways).name = "ConcreteFloor"
    curb = [(-0.02, 0.1), (0.22, 0.1), (0.22, 0.26), (-0.02, 0.26)]
    part("Stone", lambda bm: [sweep(bm, pth, curb, s, caps) for s in (-1, 1) for pth, caps in
                              ((start + [(g0 + 0.02, HUB_EDGE)], (False, True)),
                               ([(g1 - 0.02, HUB_EDGE), (HUB_WALK_END + 0.02, HUB_EDGE)], (True, True)))]).name = "StoneCurb"

    part("Grate", lambda bm: [poly(bm, [(x, y, 0.0) for x, y in pc]) for pc in hub_pieces(0.0)])
    k_flare = math.hypot(HUB_F1 - HUB_F0, HUB_EDGE - KIT_ROAD) / (HUB_F1 - HUB_F0)

    def rail_inner(x):
        e = hub_edge(x)
        return e - 0.12 * (k_flare if HUB_F0 < x < HUB_F1 else 1.0)

    def frame(bm):
        rail = [(-0.12, -0.07), (0.02, -0.07), (0.02, 0.01), (-0.12, 0.01)]
        for s in (-1, 1):
            sweep(bm, edge_a, rail, s)
            sweep(bm, edge_b + [(HUB_WALK_END, W), (HUB_LEN - 0.3, W)], rail, s)   # ends in the end wall
        reach = HUB_SIDE_MOUTH - 0.06   # beams under the side roads run out to them (1 cm short of the half beams)
        for c in [1.5 * k for k in range(10)] + [15.0]:   # the branch's half beam finishes the road at x = 16
            if g0 < c < g1:
                beam(bm, c, lambda x: reach, 0.0, HUB_LEN, web_end=reach)
            else:
                beam(bm, c, lambda x: rail_inner(x) + 0.02, 0.0, HUB_LEN, web_end=hub_edge(c))

        def begin(ay):
            if ay + 0.15 <= KIT_ROAD:
                return 0.0
            if ay + 0.15 <= HUB_EDGE:
                return HUB_F0 + (ay + 0.15 - KIT_ROAD) / (HUB_EDGE - KIT_ROAD) * (HUB_F1 - HUB_F0)
            return HUB_WALK_END + 0.05
        stringers(bm, [1.3 * k for k in range(-6, 7)], 0.0, HUB_LEN, begin)

        def mouth(b):  # the side road at its mouth: a half beam (the branch's own completes it) and the branch's stringers
            beam(b, 0.0, lambda x: B - 0.12, -1.0, 0.0, web_end=B)
            stringers(b, [-1.15, 0.0, 1.15], 6.52 - HUB_SIDE_MOUTH, 0.0)
            box(b, -0.68, -0.59, -0.9, 0.9, 4.1, 4.75)   # its sign plate, on the wall over the frame
        at_sides(bm, mouth)
        for y in HUB_OPEN_Y:  # sign plates above the openings (the game hangs the route panels there)
            box(bm, HUB_WALL_X - 0.08, HUB_WALL_X + 0.01, y - 0.9, y + 0.9, 4.1, 4.75)
        for s in (-1, 1):     # junction boxes on the long walls, clear of the side openings
            box(bm, 6.25, 6.95, *sorted((s * 7.76, s * 8.02)), 1.0, 1.85)
            box(bm, 13.95, 14.35, *sorted((s * 7.82, s * 8.02)), 1.1, 1.5)
    part("Metal", frame).name = "MetalFrame"
    part("Water", lambda bm: [poly(bm, [(x, y, KIT_WATER) for x, y in pc]) for pc in hub_pieces(0.02)])

    def pipes(bm):   # between the second rib and the end wall
        for s in (-1, 1):
            tube(bm, rounded([(13.65, s * 8.25, 2.9), (13.65, s * 7.75, 2.9), (15.05, s * 7.75, 2.9), (15.05, s * 8.25, 2.9)], 0.3, 6),
                 0.15, seg=14)
            tube(bm, rounded([(13.85, s * 8.2, 3.25), (13.85, s * 7.88, 3.25), (14.85, s * 7.88, 3.25), (14.85, s * 8.2, 3.25)], 0.2, 5),
                 0.07, seg=10)
            for x in (14.45,):
                cyl(bm, 0.19, 0.07, (x, s * 7.75, 2.9), axis="X", seg=14)
                box(bm, x - 0.2, x - 0.13, *sorted((s * 7.75, s * 8.02)), 2.85, 2.95)
    part("Rust", pipes).name = "RustPipes"
    kit_export("sewer_hub")


# --------------------------------------------------------------------------- a branch tunnel (x 0..110)

def kit_branch():
    """sewer_branch: matches a hub opening at x = 0 (half width 2.3, walls to 2.6, vault to 3.8); grate road over the
    full width above the channel; ribs every 3 m; no lamps, it ends in a dark wall at x = L - 0.4 (haze hides it)."""
    p, L = KIT_BRANCH, KIT_BRANCH_LEN
    prof = kit_profile(p, 0.0)
    n = int((L - 0.5) // KIT_BRANCH_RIB)        # ribs at 3, 6 .. (none in the dark wall)
    xs = [KIT_BRANCH_RIB * k for k in range(n + 1)] + [L]
    part("Brick", lambda bm: skin(bm, [(x, prof) for x in xs], p["wall"]))

    def ribs(bm):
        o, i = kit_profile(p, -0.02), kit_profile(p, -0.02, d=-0.1)
        rib(bm, o, i, 0.0, 0.2, p["wall"], front=False)
        for k in range(1, n + 1):
            rib(bm, o, i, KIT_BRANCH_RIB * k - 0.15, KIT_BRANCH_RIB * k + 0.15, p["wall"])
    part("Stone", ribs).name = "StoneRibs"
    part("Concrete", lambda bm: strip(bm, channel_pts(p["half"]), 0.0, L)).name = "ConcreteFloor"
    kit_road(0.0, L, p["half"], [KIT_BEAM * k for k in range(int(L / KIT_BEAM) + 1)], [-1.15, 0.0, 1.15])
    prism("DarkEnd", kit_profile(p, KIT_BOTTOM, d=0.05), L - 0.4, L - 0.02)
    kit_export("sewer_branch")


# --------------------------------------------------------------------------- a bricked-up opening

def kit_plug():
    """sewer_plug: bricks closing one opening of the branch profile (any of the hub's five) that leads nowhere this
    time. Its frame is a branch tunnel's: origin in the middle of the opening at its mouth (the wall's outer face) at
    road level (z = 0, the grate's top), +x out of the hall, y across, z up — put it where that branch would go
    (Unity, turned by KitYaw like the other kit pieces: the same position and rotation as the branch's root).
    The hub's walls are 0.6 m thick at every opening, so the infill fills x -0.575..-0.225: its face 2.5 cm behind
    the wall's inner face (the stone frame stands 15 cm proud of that), 35 cm thick, from the channel bed (z -0.73,
    the water and the grate run into it) up into the arch (top 3.8), 1 cm into the jambs all round. A few bricks are
    missing (dark behind) or pushed out, a damp band along the foot with a streak from an old rusty pipe stub."""
    import random
    rnd = random.Random(31)
    X0, X1 = -0.575, -0.225
    body = prism("Brick", kit_profile(KIT_BRANCH, KIT_BED - 0.03, d=0.01), X0, X1)
    holes = [(-1.25, 2.02, 0.26, 0.085), (-1.12, 2.105, 0.26, 0.085), (0.95, 0.82, 0.38, 0.17), (1.07, 0.99, 0.26, 0.085),
             (-0.35, 3.02, 0.26, 0.085), (1.62, 1.95, 0.13, 0.085), (-1.85, 1.05, 0.26, 0.17)]
    depth = (0.11, 0.09, 0.13, 0.1, 0.09, 0.08, 0.12)
    for (y, z, w, h), d in zip(holes, depth):
        subtract(body, part("cut", lambda bm: box(bm, X0 - 0.1, X0 + d, y - w / 2, y + w / 2, z - h / 2, z + h / 2)))

    def dark(bm):   # the shadow behind the missing bricks
        for (y, z, w, h), d in zip(holes, depth):
            poly(bm, [(X0 + d - 0.004, y - w / 2, z - h / 2), (X0 + d - 0.004, y + w / 2, z - h / 2),
                      (X0 + d - 0.004, y + w / 2, z + h / 2), (X0 + d - 0.004, y - w / 2, z + h / 2)], (-1, 0, 0))
        cyl(bm, 0.08, 0.01, (PX, PY, PZ), axis="X", seg=14)   # the stub's bore, 5 mm proud of its end

    def loose(bm):  # bricks pushed out of the infill, next to the gaps
        for y, z in ((-0.72, 2.02), (0.5, 0.65), (0.15, 3.1), (1.9, 1.6)):
            rot_box(bm, (X0 + 0.035, y, z), (0.13, 0.25, 0.08),
                    (rnd.uniform(-0.06, 0.06), rnd.uniform(-0.05, 0.05), rnd.uniform(-0.12, 0.12)))
    PX, PY, PZ = X0 - 0.16, 1.2, 1.5            # the pipe stub's open end

    def rust(bm):
        cyl(bm, 0.105, 0.36, (PX + 0.18, PY, PZ), axis="X", seg=16)          # from inside the bricks out to PX
        cyl(bm, 0.165, 0.035, (X0 - 0.01, PY, PZ), axis="X", seg=16)          # wall flange, 2.5 cm into the bricks
        cyl(bm, 0.135, 0.03, (PX + 0.02, PY, PZ), axis="X", seg=16)           # end collar, 5 mm back from the end
    part("Brick", loose).name = "BrickLoose"
    part("Rust", rust).name = "RustStub"
    part("Dark", dark).name = "DarkGaps"

    def damp(bm):   # a wet film just in front of the face: a band at the foot with a ragged top, a streak from the pipe
        xf = X0 - 0.003
        n = 26
        ys = [-KIT_BRANCH["half"] + 0.01 + (2 * KIT_BRANCH["half"] - 0.02) * i / n for i in range(n + 1)]
        tops = [0.38 + 0.1 * math.sin(3.1 * y + 0.7) + 0.06 * math.sin(7.3 * y) + rnd.uniform(-0.03, 0.03) for y in ys]
        for (ya, ta), (yb, tb) in zip(zip(ys, tops), zip(ys[1:], tops[1:])):
            poly(bm, [(xf, ya, KIT_WATER - 0.1), (xf, yb, KIT_WATER - 0.1), (xf, yb, tb), (xf, ya, ta)], (-1, 0, 0))
        z, y = PZ - 0.17, PY
        while z > 0.55:   # the streak, narrowing and wandering down to the band
            z2 = z - 0.12
            y2 = y + rnd.uniform(-0.025, 0.025)
            w1, w2 = 0.03 + 0.04 * (z - 0.4) / PZ, 0.03 + 0.04 * (z2 - 0.4) / PZ
            poly(bm, [(xf, y - w1, z), (xf, y2 - w2, z2), (xf, y2 + w2, z2), (xf, y + w1, z)], (-1, 0, 0))
            z, y = z2, y2
        poly(bm, [(xf, y - 0.03, z), (xf, y - 0.03, 0.3), (xf, y + 0.03, 0.3), (xf, y + 0.03, z)], (-1, 0, 0))
    part("Water", damp).name = "WaterDamp"
    kit_export("sewer_plug")


# --------------------------------------------------------------------------- the closed start (faces +X)

def kit_cap():
    """sewer_cap: a brick end wall over the whole section (x -0.5..0, front at x = 0) with a stone cornice, a big rusty
    outlet pipe dripping a stream of water through the grate into the channel, and a small capped pipe."""
    prism("BrickWall", kit_profile(KIT, KIT_BOTTOM - 0.05, d=0.08), -0.5, 0.0)
    cz, ro, ri = 1.45, 0.6, 0.5

    def stone(bm):
        box(bm, -0.02, 0.12, -3.62, 3.62, 2.52, 2.68)
        box(bm, -0.02, 0.1, -3.62, 3.62, KIT_BOTTOM, 0.3)          # base course, down to the channel bed
    part("Stone", stone).name = "StoneCap"

    def rust(bm):
        ring_tube(bm, ro, ri, -0.05, 0.55, 0.0, cz, seg=24)
        ring_tube(bm, 0.76, ro - 0.02, -0.05, 0.07, 0.0, cz, seg=24, inner=False)
        cyl(bm, 0.13, 0.4, (0.15, 2.4, 0.62), axis="X", seg=14)
        cyl(bm, 0.17, 0.05, (0.0, 2.4, 0.62), axis="X", seg=14)
        cyl(bm, 0.15, 0.06, (0.36, 2.4, 0.62), axis="X", seg=14)
    part("Rust", rust).name = "RustPipe"

    def bolts(bm):
        for k in range(12):
            a = 2 * math.pi * k / 12
            cyl(bm, 0.025, 0.05, (0.085, 0.68 * math.cos(a), cz + 0.68 * math.sin(a)), axis="X", seg=6)
    part("Metal", bolts).name = "MetalBolts"
    part("Dark", lambda bm: poly(bm, [(0.1, (ri + 0.01) * math.cos(2 * math.pi * k / 24), cz + (ri + 0.01) * math.sin(2 * math.pi * k / 24))
                                      for k in range(24)], (1, 0, 0))).name = "DarkPipe"

    def water(bm):
        zw = cz - ri + 0.05
        hw = math.sqrt(ri ** 2 - (ri - 0.05) ** 2) + 0.01
        poly(bm, [(0.1, -hw, zw), (0.56, -hw, zw), (0.56, hw, zw), (0.1, hw, zw)])
        pts = [(0.45, 0, zw - 0.02), (0.6, 0, zw - 0.03), (0.7, 0, zw - 0.12), (0.78, 0, zw - 0.32), (0.84, 0, zw - 0.6),
               (0.88, 0, 0.05), (0.9, 0, -0.25), (0.9, 0, KIT_WATER - 0.03)]
        radii = [0.05, 0.06, 0.065, 0.06, 0.055, 0.05, 0.045, 0.045]
        vs = tube(bm, pts, 0.06, seg=10, radii=radii)
        for v in vs:
            v.co.y *= 2.4  # a flat ribbon of water, wider than it is thick
        torus(bm, 0.13, 0.035, (0.9, 0.0, KIT_WATER + 0.005), seg=18, seg2=5)
    part("Water", water)
    kit_export("sewer_cap")


# --------------------------------------------------------------------------- the grate texture

def kit_texture():
    """Art/Textures/Sewer/grate.png: 512 x 512 RGBA, one seamless tile = 0.5 m of pressed steel grating — bearing bars
    along v, cross bars along u (8 x 8 cells, ~52 % open: alpha 0), bevelled bars with wear and rust (alpha 1)."""
    import numpy as np
    N, cells = 512, 8
    pitch = N // cells
    bear, cross = 20, 16                      # bar widths in pixels (6.25 cm pitch: 2.4 and 1.9 cm bars)
    rng = np.random.default_rng(480)
    u = np.arange(N)

    def noise(freq):                          # periodic value noise (seamless: the lattice wraps)
        g = rng.random((freq, freq))
        t = u * freq / N
        i0 = np.floor(t).astype(int)
        f = t - i0
        f = f * f * (3 - 2 * f)
        i1 = (i0 + 1) % freq
        a = g[i0][:, i0] * (1 - f)[None, :] + g[i0][:, i1] * f[None, :]
        b = g[i1][:, i0] * (1 - f)[None, :] + g[i1][:, i1] * f[None, :]
        return a * (1 - f)[:, None] + b * f[:, None]

    fbm = sum(noise(fq) / (k + 1) for k, fq in enumerate((8, 16, 32, 64, 128)))
    fbm = (fbm - fbm.min()) / (fbm.max() - fbm.min())
    rust = noise(16) * 0.6 + noise(64) * 0.4
    d = (u % pitch).astype(float)
    # distance from each bar's centre line, 0 at the centre and 1 at its edge
    du = np.abs(d - pitch / 2) / (bear / 2)      # bearing bars run along v at the middle of each cell column
    dv = np.abs(d - pitch / 2) / (cross / 2)
    on_b = du[None, :] <= 1.0
    on_c = dv[:, None] <= 1.0
    solid = on_b | on_c
    hb = np.sqrt(np.clip(1 - du ** 2, 0, 1))[None, :].repeat(N, 0)
    hc = np.sqrt(np.clip(1 - dv ** 2, 0, 1))[:, None].repeat(N, 1)
    h = np.where(on_b, hb, 0.0)
    h = np.maximum(h, np.where(on_c, hc * 0.85, 0.0))   # cross bars sit a little lower
    shade = 0.55 + 0.45 * h
    joint = on_b & on_c
    shade = np.where(joint, shade * 0.92, shade)          # pressed joints darker
    steel = np.array([0.36, 0.37, 0.38])
    rust_c = np.array([0.40, 0.24, 0.13])
    worn = np.array([0.62, 0.62, 0.6])
    rmask = np.clip((rust - 0.55) * 4.0, 0, 1) * (1 - h * 0.5)
    wmask = np.clip((h - 0.75) * 4, 0, 1) * np.clip((fbm - 0.35) * 2.5, 0, 1)
    col = steel[None, None, :] * (0.85 + 0.3 * fbm)[:, :, None]
    col = col * (1 - rmask[:, :, None]) + rust_c[None, None, :] * rmask[:, :, None]
    col = col * (1 - wmask[:, :, None] * 0.6) + worn[None, None, :] * (wmask[:, :, None] * 0.6)
    col = col * shade[:, :, None]
    col = np.where(solid[:, :, None], col, steel[None, None, :] * 0.4)   # open cells: dark bar colour (no fringes)
    alpha = solid.astype(float)
    rgba = np.dstack([np.clip(col, 0, 1), alpha])
    path = os.path.abspath(os.path.join(ROOT, "..", "Textures", "Sewer", "grate.png"))  # Blender saves relative paths elsewhere
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = bpy.data.images.new("grate", N, N, alpha=True)
    img.pixels.foreach_set(rgba[::-1].astype(np.float32).ravel())   # Blender rows go bottom-up
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("KIT grate.png open fraction", round(1 - alpha.mean(), 3), path)


def kit_reducer():
    """sewer_reducer: where a branch tunnel (sewer_branch, 110 m) opens into the drivable kit's wide tunnel, the way on to
    the next house: a brick wall over the kit's whole section (x 0..0.5, its face to the branch at x = 0) with the
    branch's opening through it, a stone frame round the opening on the wide side, the road's grate and the channel
    through the wall. The kit's segments carry on from x = 0.5 (Level.DriveTunnel)."""
    wall = prism("Brick", kit_profile(KIT, KIT_BOTTOM - 0.05, d=0.08), 0.0, 0.5)
    hole = prism("Hole", kit_profile(KIT_BRANCH, KIT_BED - 0.05), -0.2, 0.7)
    subtract(wall, hole)
    o, i = kit_profile(KIT_BRANCH, -0.02, d=0.2), kit_profile(KIT_BRANCH, -0.02)
    part("Stone", lambda bm: rib(bm, o, i, 0.48, 0.64, KIT_BRANCH["wall"], back=False, outside=True)).name = "StoneFrame"
    part("Concrete", lambda bm: strip(bm, channel_pts(KIT_BRANCH["half"]), 0.0, 0.5)).name = "ConcreteFloor"
    kit_road(0.0, 0.5, KIT_BRANCH["half"], [0.0, 0.5], [-1.15, 0.0, 1.15])
    kit_export("sewer_reducer")


# --------------------------------------------------------------------------- the junction hall, modular (Level.SewerHall)
#
# The game lays the hall out of these tiles on a grid of HALL_CELL m cells, different every time (SewerHall): a floor
# and a groin vault per cell, a wall on every side of the boundary (plain, with a mouth of the branch's profile, or the
# way in from the sewer's tunnel), a column at every corner of the cells (half in the wall on the boundary), a stone
# arch between neighbouring cells. Two heights (HALL_HEIGHTS: where the vaults spring, how far they rise).
# Frames: a floor, a vault, a column and an arch are centred on their cell / corner / side (an arch spans y); every wall
# piece is in a branch tunnel's frame: origin in the middle of the wall's OUTER face at road level, +x out of the hall,
# the inner face at x = -HALL_WALL on the cell's side, the side running y -4..4 (a branch tunnel or sewer_plug starts at
# that origin). The edge arch of a cell (where its vault meets a neighbour's or a wall): z = H + r * sqrt(1 - (2t / HALL_CELL)^2),
# springing at the corners (the columns' axes); the stone arches under it spring from the capitals' edges (HALL_SPAN).

HALL_CELL, HALL_WALL = 8.0, 0.6                 # SewerHall.Cell, SewerHall.Wall
HALL_HEIGHTS = ((3.4, 2.2), (4.2, 2.8))         # (springing, rise): SewerHall.Springing
HALL_SHAFT, HALL_BASE, HALL_CAP = 0.32, 0.38, 0.46  # column halves: shaft, base (SewerHall.ColumnHalf), capital
HALL_SPAN = HALL_CELL / 2 - HALL_CAP            # the stone arches' half span (capital edge to capital edge)
HALL_ARC = 24                                   # vault / arch segments (even: a vertex at the crown)
HALL_BAND = 0.3                                 # the stone arches' depth under the vault


def hall_arch_pts(H, r, lift=0.0):
    """The edge arch of a cell, (y, z) from the corner at -HALL_CELL / 2 over the crown to the other corner."""
    h = HALL_CELL / 2
    return [(-h * math.cos(math.pi * i / HALL_ARC), H + lift + r * math.sin(math.pi * i / HALL_ARC)) for i in range(HALL_ARC + 1)]


def hall_band_curves(H, r, depth=HALL_BAND, sink=0.05):
    """A stone arch's two edges, (y, z), point for point: the intrados (HALL_SPAN, from capital edge to capital edge,
    `depth` under the vault's crown) and the extrados, the edge arch `sink` up into the vault (none at the feet: they
    lie on the capitals from the capital's edge to the corner)."""
    h = HALL_CELL / 2
    inner, outer = [], []
    for i in range(HALL_ARC + 1):
        th = math.pi - math.pi * i / HALL_ARC
        inner.append((HALL_SPAN * math.cos(th), H + (r - depth) * math.sin(th)))
        outer.append((h * math.cos(th), H + (r + sink) * math.sin(th)))
    return inner, outer


def hall_band(bm, x0, x1, H, r, back=True):
    """A stone arch between x0 and x1 following the edge arch: its faces (front at x0, back at x1 unless it lies on a
    wall, the intrados). It lands on the capitals; its top is in the vault."""
    inner, outer = hall_band_curves(H, r)
    for k in range(HALL_ARC):
        (yi0, zi0), (yi1, zi1) = inner[k], inner[k + 1]
        (yo0, zo0), (yo1, zo1) = outer[k], outer[k + 1]
        kface(bm, [bm.verts.new((x0, yi0, zi0)), bm.verts.new((x0, yi1, zi1)), bm.verts.new((x0, yo1, zo1)), bm.verts.new((x0, yo0, zo0))], (-1, 0, 0))
        if back:
            kface(bm, [bm.verts.new((x1, yi0, zi0)), bm.verts.new((x1, yi1, zi1)), bm.verts.new((x1, yo1, zo1)), bm.verts.new((x1, yo0, zo0))], (1, 0, 0))
        ym, zm = (yi0 + yi1) / 2, (zi0 + zi1) / 2
        kface(bm, [bm.verts.new((x0, yi0, zi0)), bm.verts.new((x1, yi0, zi0)), bm.verts.new((x1, yi1, zi1)), bm.verts.new((x0, yi1, zi1))],
              (0, -ym, H + r * 0.3 - zm))


def hall_wall_slab(name, H, r):
    """The wall on a side: 0.6 m of brick from below the channel's bed up 0.3 m past the cell's edge arch (the vault's
    edge sinks into it), along the whole side."""
    h = HALL_CELL / 2
    pts = [(h, KIT_BOTTOM - 0.05), (-h, KIT_BOTTOM - 0.05)] + hall_arch_pts(H, r, 0.3)
    return prism(name, pts, -HALL_WALL, 0.0)


def hall_base(bm, gaps=()):
    """The stone plinth along a wall's foot (6 cm proud, from the channel's bed to 0.3 over the road), broken where an
    opening is: gaps = [(y0, y1)]."""
    h = HALL_CELL / 2
    edges = [-h] + [v for g in gaps for v in g] + [h]
    for y0, y1 in zip(edges[0::2], edges[1::2]):
        box(bm, -HALL_WALL - 0.06, -HALL_WALL + 0.01, y0, y1, KIT_BED, 0.3)


def kit_hall_floor():
    """sewer_hall_floor: a cell's floor (x, y -4..4): the road's grate over the whole cell at z = 0, beams across it
    every 2 m (x = -3, -1, 1, 3) on piers, stringers along it, the water, the bed."""
    h = HALL_CELL / 2
    part("Grate", lambda bm: poly(bm, [(-h, -h, 0.0), (h, -h, 0.0), (h, h, 0.0), (-h, h, 0.0)]))

    def frame(bm):
        for c in (-3.0, -1.0, 1.0, 3.0):
            beam(bm, c, lambda x: h - 0.006, -h, h, web_end=h - 0.006)
        stringers(bm, [-3.0, -1.0, 1.0, 3.0], -h, h)
    part("Metal", frame).name = "MetalFrame"
    part("Water", lambda bm: poly(bm, [(-h, -h, KIT_WATER), (h, -h, KIT_WATER), (h, h, KIT_WATER), (-h, h, KIT_WATER)]))

    def bed(bm):
        poly(bm, [(-h, -h, KIT_BED), (h, -h, KIT_BED), (h, h, KIT_BED), (-h, h, KIT_BED)])
        for c in (-3.0, -1.0, 1.0, 3.0):
            for y in (-2.0, 2.0):
                box(bm, c - 0.1, c + 0.1, y - 0.12, y + 0.12, KIT_BED - 0.02, -0.315)
    part("Concrete", bed).name = "ConcreteBed"
    kit_export("sewer_hall_floor")


def kit_hall_vault(v):
    """sewer_hall_vault_<v>: a cell's brick groin vault (x, y -4..4, faces down): two barrels crossing, the creases on
    the diagonals, down to the corners (the columns' axes); on each side the edge arch."""
    H, r = HALL_HEIGHTS[v]
    h = HALL_CELL / 2
    arch = hall_arch_pts(H, r)
    ys = [y for y, _ in arch]
    zs = [z for _, z in arch]

    def vault(bm):
        for q in range(4):
            c, s = round(math.cos(q * math.pi / 2)), round(math.sin(q * math.pi / 2))

            def P(x, y, z):
                return (c * x - s * y, s * x + c * y, z)
            for k in range(len(ys) - 1):   # the sector |y| <= x of this quarter: the barrel along x, z by y alone
                y0, y1, z0, z1 = ys[k], ys[k + 1], zs[k], zs[k + 1]
                pts = [P(abs(y0), y0, z0), P(h, y0, z0), P(h, y1, z1), P(abs(y1), y1, z1)]
                if abs(abs(y0) - h) < 1e-6:
                    pts = pts[1:]
                elif abs(abs(y1) - h) < 1e-6:
                    pts = pts[:3]
                poly(bm, pts, (0, 0, -1))
    part("Brick", vault).name = "BrickVault"
    kit_export(f"sewer_hall_vault_{v}")


def kit_hall_wall(v):
    """sewer_hall_wall_<v>: a plain wall on a side (branch frame: outer face at x = 0), its plinth and a stone arch on its
    inner face under the vault's edge arch."""
    H, r = HALL_HEIGHTS[v]
    hall_wall_slab("BrickWall", H, r)

    def stone(bm):
        hall_base(bm)
        hall_band(bm, -HALL_WALL - 0.12, -HALL_WALL + 0.01, H, r, back=False)
    part("Stone", stone).name = "StoneTrim"
    kit_export(f"sewer_hall_wall_{v}")


def kit_hall_mouth(v):
    """sewer_hall_mouth_<v>: a wall with a mouth of the branch's profile (4.6 x 3.8 over the road, down to the channel's
    bed) in the middle of the side; a stone frame on the inner face, a half rib meeting the branch's at x = 0, the road's
    grate and its beams through the wall, the sign plate over the frame (the game hangs the route panel there)."""
    H, r = HALL_HEIGHTS[v]
    wall = hall_wall_slab("BrickWall", H, r)
    subtract(wall, prism("cut", kit_profile(KIT_BRANCH, KIT_BED - 0.03), -HALL_WALL - 0.2, 0.2))
    o, i = kit_profile(KIT_BRANCH, KIT_BED, d=0.22), kit_profile(KIT_BRANCH, KIT_BED)
    ro, ri = kit_profile(KIT_BRANCH, -0.02), kit_profile(KIT_BRANCH, -0.02, d=-0.1)
    gap = KIT_BRANCH["half"] + 0.15

    def stone(bm):
        rib(bm, o, i, -HALL_WALL - 0.15, -HALL_WALL, KIT_BRANCH["wall"], back=False, outside=True)
        rib(bm, ro, ri, -0.2, 0.0, KIT_BRANCH["wall"], back=False)
        hall_base(bm, [(-gap, gap)])
        hall_band(bm, -HALL_WALL - 0.12, -HALL_WALL + 0.01, H, r, back=False)
    part("Stone", stone).name = "StoneTrim"
    kit_road(-HALL_WALL, 0.0, KIT_BRANCH["half"], [-HALL_WALL, 0.0], [-1.15, 0.0, 1.15])
    part("Metal", lambda bm: box(bm, -HALL_WALL - 0.08, -HALL_WALL + 0.01, -0.9, 0.9, 4.1, 4.75)).name = "MetalFrameSign"
    kit_export(f"sewer_hall_mouth_{v}")


def kit_hall_entry(v):
    """sewer_hall_entry_<v>: the wall the sewer's tunnel comes in through (branch frame: x = 0 is where the last segment
    ends, -x into the hall): the segment's section cut through it, its walkways, kerbs, road and half rib running on
    through the wall, the walkways' ends closed towards the hall (a step down onto the hall's grate)."""
    H, r = HALL_HEIGHTS[v]
    wall = hall_wall_slab("BrickWall", H, r)
    subtract(wall, prism("cut", kit_profile(KIT, KIT_BED - 0.03), -HALL_WALL - 0.2, 0.2))
    outer, x0 = KIT["half"] + 0.02, -HALL_WALL - 0.002   # 2 cm into the reveal's brick, the ends 2 mm proud of the wall

    def walkways(bm):
        strip(bm, channel_pts(KIT_ROAD, outer=outer), x0, 0.0, closed=True)
        rr = KIT_ROAD
        for s in (-1, 1):   # the ends towards the hall
            ya, yb = sorted((s * rr, s * outer))
            poly(bm, [(x0, ya, -0.9), (x0, yb, -0.9), (x0, yb, KIT_WALK), (x0, ya, KIT_WALK)], (-1, 0, 0))
            ya, yb = sorted((s * (rr - 0.18), s * rr))
            poly(bm, [(x0, ya, -0.18), (x0, yb, -0.18), (x0, yb, -0.06), (x0, ya, -0.06)], (-1, 0, 0))
        poly(bm, [(x0, -rr, -0.9), (x0, rr, -0.9), (x0, rr, KIT_BED), (x0, -rr, KIT_BED)], (-1, 0, 0))
    part("Concrete", walkways).name = "ConcreteFloor"
    rout, rin = kit_profile(KIT, KIT_WALK - 0.02), kit_profile(KIT, KIT_WALK - 0.02, d=-0.12)

    def stone(bm):
        for s in (-1, 1):
            box(bm, -HALL_WALL, 0.0, *sorted((s * 2.58, s * 2.82)), 0.10, 0.26)
        rib(bm, rout, rin, -0.2, 0.0, KIT["wall"], back=False)
        hall_band(bm, -HALL_WALL - 0.12, -HALL_WALL + 0.01, H, r, back=False)
    part("Stone", stone).name = "StoneTrim"
    kit_road(-HALL_WALL, 0.0, KIT_ROAD, [-HALL_WALL, 0.0], [-1.3, 0.0, 1.3])
    kit_export(f"sewer_hall_entry_{v}")


def kit_hall_column(v):
    """sewer_hall_column_<v>: a column at a corner of the cells, from the channel's bed up to where the vaults spring:
    a square brick pier (low) or a round one (high), a stone base and a capital (the arches land on it)."""
    H, r = HALL_HEIGHTS[v]
    top = H - 0.3
    if v == 0:
        part("Brick", lambda bm: box(bm, -HALL_SHAFT, HALL_SHAFT, -HALL_SHAFT, HALL_SHAFT, KIT_BED, top + 0.01)).name = "BrickColumn"
    else:
        part("Brick", lambda bm: cyl(bm, HALL_SHAFT + 0.02, top - KIT_BED + 0.01, (0.0, 0.0, (top + KIT_BED + 0.01) / 2), seg=16)).name = "BrickColumn"

    def stone(bm):
        box(bm, -HALL_BASE, HALL_BASE, -HALL_BASE, HALL_BASE, KIT_BED - 0.02, 0.32)
        box(bm, -0.39, 0.39, -0.39, 0.39, top, H - 0.17)
        box(bm, -HALL_CAP, HALL_CAP, -HALL_CAP, HALL_CAP, H - 0.17, H)
    part("Stone", stone).name = "StoneColumn"
    kit_export(f"sewer_hall_column_{v}")


def kit_hall_arch(v):
    """sewer_hall_arch_<v>: the stone arch between two neighbouring cells (across x -0.2..0.2, spanning y from capital to
    capital under the vaults' shared edge arch)."""
    H, r = HALL_HEIGHTS[v]
    part("Stone", lambda bm: hall_band(bm, -0.2, 0.2, H, r)).name = "StoneArch"
    kit_export(f"sewer_hall_arch_{v}")


def kit_hall():
    kit_hall_floor()
    for v in range(len(HALL_HEIGHTS)):
        for fn in (kit_hall_vault, kit_hall_wall, kit_hall_mouth, kit_hall_entry, kit_hall_column, kit_hall_arch):
            fn(v)


def kit():
    kit_texture()
    for v in range(4):
        for cut in (False, True):
            kit_segment(v, cut)
    for fn in (kit_alcove, kit_gate_frame, kit_gate_door, kit_lever, kit_hub, kit_branch, kit_plug, kit_cap, kit_reducer):
        fn()
    kit_hall()


if __name__ == "__main__":
    clear()
    if HALL_ONLY:
        kit_hall()
    else:
        if not KIT_ONLY:
            for fn in (tunnel, hall, branch, grate, ladder, manhole, cart, lamp, pipe, valve, barrel, crate):
                fn()
                print("SEWER", fn.__name__)
        kit()
    print("DONE")
