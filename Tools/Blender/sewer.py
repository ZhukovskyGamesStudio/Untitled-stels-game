"""The sewer between houses: vaulted brick tunnels, a hall with a cistern, branching tunnels to the next houses and the props.

Run:  blender.exe -b --factory-startup --python Tools/Blender/sewer.py -- Assets/_Game/Art/Models

Writes Art/Models/Sewer/sewer_*.fbx. Blender: x along the tunnel (Unity X), -y = Unity +Z, z up. Part names pick the
material in the Unity builder (AssetBuilder.BuildSewer): Brick, Stone, Concrete, Water, Metal, Rust, Wood, Dark, Glow.
Every part gets box-projected UVs (1 unit = 1 m) so the brick and concrete textures tile in metres.

Layout (metres, Unity X):  0..12 the tunnel from the ladder (main channel in the middle, walkways along the walls)
                          12..22 the hall: wider vault, a cistern basin in the middle
                          22..   three arched openings (y = -4.2, 0, +4.2) into the branch tunnels to the next houses
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
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


# --------------------------------------------------------------------------- a branch tunnel to a house (local x 0..12)

def branch():
    """The three tunnels to the next houses, each turned to its opening and cut off at the hall's end wall (no gap
    between the wall and the tunnel, nothing sticking into the hall). sewer_branch_0 is Unity's left one (z = -4.2)."""
    p = BRANCH
    prof = profile(**p)

    def ribs(bm):
        for x in (2.0, 5.0, 8.0, 11.0):
            band(bm, prof, profile(**inset(p, 0.1)), x - 0.15, x + 0.15)

    for index, y in enumerate(sorted(BRANCH_Y, reverse=True)):  # Unity z = -y
        parts = [part("Brick", lambda bm: shell(bm, prof, -1.0, 12.0, (0.0, p["wall"]))),
                 part("Stone", ribs),
                 part("Concrete", lambda bm: box(bm, -1.0, 12.0, -p["half"], p["half"], -0.3, 0.0))]
        gutter = part("Dark", lambda bm: box(bm, -1.0, 12.0, -0.12, 0.12, -0.01, 0.005))
        gutter.name = "Gutter"
        end = part("Dark", lambda bm: box(bm, 11.6, 12.0, -p["half"], p["half"], 0.0, p["wall"] + p["rise"]))
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


if __name__ == "__main__":
    clear()
    for fn in (tunnel, hall, branch, grate, ladder, manhole, cart, lamp, pipe, valve, barrel, crate):
        fn()
        print("SEWER", fn.__name__)
    print("DONE")
