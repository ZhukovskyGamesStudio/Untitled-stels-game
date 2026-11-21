"""A realistic rigged human for the player: a station scientist in a grimy lab coat (S16 «Заброшенный комплекс»)
and the players' four departments (every style).

Run:  blender.exe -b --factory-startup --python Tools/Blender/human.py -- Assets/_Game/Art/Models/Humans [preview.png]
                                                                         [backend|qa|sysadmin|designer] [static=<file.fbx>]

Writes <out>/human_scientist.fbx (or human_<department>.fbx): one armature ("Rig") with skinned meshes and six
in-place animation takes — Idle, Walk, Run, CrouchIdle, CrouchWalk, Down — and, building the scientist, the face's
skin map <out>/../../Textures/Humans/skin_face.png. `static=` also writes the meshes alone in the rest pose: that is
the file to show Tools/Blender/review.py (an FBX with takes comes in posed).

A real adult (1.79 m with hair, head 1/7.5 of the height), 11-13 thousand triangles. Everything is sculpted dense
and then cut to a triangle budget (`lowpoly`):
  * the head is clay — volumes of skull, jaw, cheekbones, nose, lips and lids blended into one field, sockets and
    creases carved out of it (`head_sdf`); ears and hair are fields of their own; most triangles stay on the face;
  * clothes are tubes through cross-sections with folds pushed into them (`tube`, `*_folds`): the crook of the elbow,
    the break of the trousers over the shoe, the knee, the skirt of the coat hanging in flutes;
  * hands and shoes are fingers, palm, sole and shaft welded into one skin (`fuse`).

Mesh names follow the player part convention (PlayerLooks.PartColor colours them per player):
  BodyClothes   shirt and trousers             — the department's colour
  AccentHead    head, neck, hands (skin)       — the player's skin tone, times the skin map
  AccentBand    armband, lanyard                — the team colour
  EyeDetails    hair, eyebrows, irises, shoes  — dark
  Coat          the lab coat                    — keeps its own (grimy off-white) material
  Sclera        the whites of the eyes, buttons — keeps its own material
Every piece is weighted only to its own bones (by distance: no automatic heat weights), then the pieces of one part
are joined. The bones and the key points P are the contract with the takes below and with HumanRig: do not move them.
Blender: x right, -y the front, z up; Unity gets +Z forward (bake axis conversion on import).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
# the players' four departments (PlayerLooks.Department): `-- <out> [preview.png] backend` builds human_backend.fbx instead
DEPARTMENTS = ("backend", "qa", "sysadmin", "designer")
DEPT = next((a for a in argv if a in DEPARTMENTS), None)
argv = [a for a in argv if a not in DEPARTMENTS and not a.startswith("static=")]
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models", "Humans"))
PREVIEW = os.path.abspath(argv[1]) if len(argv) > 1 else None
# `static=<file.fbx>`: also the meshes alone in the rest pose (what Tools/Blender/review.py looks at)
STATIC = next((os.path.abspath(a[7:]) for a in sys.argv if a.startswith("static=")), None)
# the skin map of the face (multiplied by the player's skin tone in the game)
SKIN_MAP = os.path.abspath(os.path.join(OUT, "..", "..", "Textures", "Humans", "skin_face.png"))
FPS = 30


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS


def apply_all(obj):
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.scene.objects:
        o.select_set(o == obj)
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


# --------------------------------------------------------------------------- building blocks

def mesh_obj(name, bm, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for p in obj.data.polygons:
        p.use_smooth = smooth
    return obj


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def lowpoly(hi, tris, normals=True, keep=None):
    """The game mesh of a dense sculpted one: collapsed to a triangle budget (the decimator keeps the folds and
    features, flat cloth goes), shaded with the normals of the dense mesh — folds, lips and knuckles the triangles no
    longer hold still catch the light. Removes the dense mesh."""
    for p in hi.data.polygons:
        p.use_smooth = True
    lo = hi.copy()
    lo.data = hi.data.copy()
    bpy.context.collection.objects.link(lo)
    n = tri_count(lo)
    if n > tris:
        dec = lo.modifiers.new("dec", "DECIMATE")
        dec.ratio = tris / n
        bm = bmesh.new()
        bm.from_mesh(lo.data)
        rim = [v.index for v in bm.verts if v.is_boundary]
        bm.free()
        if keep is not None or rim:             # keep(points) -> 0..1: where the triangles should stay (a face, not a
                                                # scalp); 1 is "never collapse", so real weights are well under it.
                                                # The open edge of a cuff or a hem keeps its shape too
            group = lo.vertex_groups.new(name="keep")
            pts = np.array([v.co for v in lo.data.vertices])
            weights = keep(pts) if keep is not None else np.zeros(len(pts))
            weights[rim] = np.maximum(weights[rim], 0.5)
            for i, w in enumerate(weights):
                group.add([i], float(w), "REPLACE")
            dec.vertex_group = "keep"
            dec.invert_vertex_group = True
            dec.vertex_group_factor = 1.0
        apply_all(lo)
        if "keep" in lo.vertex_groups:
            lo.vertex_groups.remove(lo.vertex_groups["keep"])
    if normals:
        # (the Data Transfer modifier applies to nothing without a window, so: the nearest point of the dense mesh
        # and the smooth normal there, per vertex)
        src = hi.data
        src.calc_loop_triangles()
        tris = [tuple(t.vertices) for t in src.loop_triangles]
        co = [v.co for v in src.vertices]
        vn = [v.normal.copy() for v in src.vertices]
        bvh = BVHTree.FromPolygons(co, tris)
        out = []
        for v in lo.data.vertices:
            hit, _, index, _ = bvh.find_nearest(v.co)
            if hit is None:
                out.append(v.normal.copy())
                continue
            a, b, c = tris[index]
            w = poly_3d_calc([co[a], co[b], co[c]], hit)
            n = vn[a] * w[0] + vn[b] * w[1] + vn[c] * w[2]
            out.append(n.normalized() if n.length > 1e-6 and n.normalized().dot(v.normal) > 0.5 else v.normal.copy())
        lo.data.normals_split_custom_set_from_vertices(out)
    me = hi.data
    bpy.data.objects.remove(hi)
    bpy.data.meshes.remove(me)
    return lo


def mirrored(obj):
    """A copy of an object reflected to the other side of the body."""
    o = obj.copy()
    o.data = obj.data.copy()
    bpy.context.collection.objects.link(o)
    for v in o.data.vertices:
        v.co.x = -v.co.x
    o.data.flip_normals()
    return o


def merged(bms):
    """One bmesh of several."""
    bm = bmesh.new()
    for b in bms:
        me = bpy.data.meshes.new("_p")
        b.to_mesh(me)
        b.free()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    return bm


def fuse(obj, voxel, smooth=4):
    """Welds overlapping closed pieces into one skin (fingers into the palm, a shaft into a shoe) and softens it."""
    m = obj.modifiers.new("rm", "REMESH")
    m.mode = "VOXEL"
    m.voxel_size = voxel
    m.use_smooth_shade = True
    if smooth:
        s = obj.modifiers.new("sm", "SMOOTH")
        s.iterations = smooth
        s.factor = 0.5
    apply_all(obj)
    return obj


def _resample(keys, step):
    """Catmull-Rom through the rows of keys, about `step` metres apart."""
    k = np.array(keys, float)
    out = []
    for i in range(len(k) - 1):
        p0, p1, p2, p3 = k[max(i - 1, 0)], k[i], k[i + 1], k[min(i + 2, len(k) - 1)]
        n = max(1, int(math.ceil(np.linalg.norm(p2[:3] - p1[:3]) / step)))
        for j in range(n):
            t = j / n
            out.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3))
    out.append(k[-1])
    return np.array(out)


def tube(keys, seg=48, step=0.006, ref=(0, 1, 0), disp=None, caps=(True, True), recess=(0.0, 0.0), plugs=None):
    """A solid through cross-sections along a path. keys: (x, y, z, a, b[, n]) — the centre, the half-width across
    the path and the half-depth along `ref` (front to back for a standing limb), the superellipse exponent (2 round,
    3-4 boxy). disp(points, radial) -> metres pushes the surface out (folds). A recessed end is a cuff or a hem: it
    stays open, and a flat plug that closes it a little inside goes to `plugs` (a bmesh of its own: it is not cut to
    the budget with the cloth, so its edge stays clean and nobody looks through the back faces)."""
    rows = _resample([tuple(k) + (2.0,) * (6 - len(k)) for k in keys], step)
    c = rows[:, :3]
    t = np.gradient(c, axis=0)
    t /= np.linalg.norm(t, axis=1)[:, None]
    ref = np.array(ref, float)
    w = ref[None, :] - t * (t @ ref)[:, None]
    w /= np.linalg.norm(w, axis=1)[:, None]
    u = np.cross(w, t)
    th = np.linspace(-math.pi, math.pi, seg, endpoint=False)
    cs, sn = np.cos(th), np.sin(th)
    rings = []
    for k in range(len(rows)):
        a, b, n = rows[k, 3], rows[k, 4], max(rows[k, 5], 1.5)
        x = a * np.sign(cs) * np.abs(cs) ** (2 / n)
        y = b * np.sign(sn) * np.abs(sn) ** (2 / n)
        pts = c[k] + u[k] * x[:, None] + w[k] * y[:, None]
        if disp is not None:
            radial = pts - c[k]
            radial /= np.maximum(np.linalg.norm(radial, axis=1), 1e-9)[:, None]
            pts = pts + radial * np.asarray(disp(pts, radial))[:, None]
        rings.append(pts)
    bm = bmesh.new()

    def skin(rs, first, last):
        vs = [[bm.verts.new(p) for p in r] for r in rs]
        for lo, up in zip(vs, vs[1:]):
            for j in range(seg):
                bm.faces.new((lo[j], lo[(j + 1) % seg], up[(j + 1) % seg], up[j]))
        if first:
            bm.faces.new(list(reversed(vs[0])))
        if last:
            bm.faces.new(vs[-1])

    for end, depth in ((0, recess[0]), (-1, recess[1])):
        if depth > 0 and plugs is not None:
            inward = t[end] * (depth if end == 0 else -depth)
            ring = (c[end] + (rings[end] - c[end]) * 0.985 + inward)[::max(1, seg // 24)]
            pb = bmesh.new()
            face = pb.faces.new([pb.verts.new(q) for q in ring])
            face.normal_update()
            if face.normal.dot(Vector(-inward)) < 0:
                face.normal_flip()
            plugs.append(pb)
    skin(rings, caps[0] and recess[0] <= 0, caps[1] and recess[1] <= 0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def win(v, lo, hi, soft=0.03):
    """1 between lo and hi, fading to 0 over `soft` outside."""
    return np.clip((v - lo) / soft + 1, 0, 1) * np.clip((hi - v) / soft + 1, 0, 1)


def gauss(v, w):
    return np.exp(-(v / w) ** 2)


def angle_of(radial):
    return np.arctan2(radial[:, 1], radial[:, 0])


# key points of the body (metres): the skeleton below uses the same ones
P = {
    "pelvis": (0, 0.0, 0.96), "waist": (0, 0.005, 1.08), "chest": (0, 0.0, 1.25), "upchest": (0, 0.01, 1.38),
    "neck0": (0, 0.02, 1.47), "neck1": (0, 0.015, 1.56), "head": (0, 0.0, 1.635),
    "shoulder": (0.18, 0.02, 1.405), "elbow": (0.225, 0.04, 1.14), "wrist": (0.245, 0.0, 0.89), "hand": (0.25, -0.01, 0.8),
    "hip": (0.095, 0.0, 0.92), "knee": (0.105, -0.015, 0.5), "ankle": (0.105, 0.03, 0.085), "toe": (0.11, -0.12, 0.03),
}


def mirror(p, s):
    return (p[0] * s, p[1], p[2])


def side_bones(s):
    return "L" if s > 0 else "R"


# every piece is weighted only to its own bones, then pieces of one part are joined (no weights bleed across limbs)
TORSO = ["hips", "spine", "chest"]
pieces = []   # (part name, object, [bones])


def piece(part, obj, bones):
    pieces.append((part, obj, bones))
    return obj


def cloth(name, keys, recess, **kw):
    """A dense tube of cloth and the plugs of its open ends: (object, [plug objects])."""
    plugs = []
    hi = mesh_obj(name, tube(keys, recess=recess, plugs=plugs, **kw))
    return hi, [mesh_obj("plug", b, smooth=False) for b in plugs]


def cut(hi, plugs, tris):
    """The game mesh of a dense tube of cloth with its plugs."""
    lo = lowpoly(hi, tris)
    name = lo.name
    return join([lo] + plugs, name) if plugs else lo


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    return obj


def both_sides(part, hi, tris, bones, after=None, plugs=()):
    """A dense left-side mesh and its mirror, each cut to the budget; bones: a function of the side letter."""
    other, other_plugs = mirrored(hi), [mirrored(o) for o in plugs]
    out = []
    for o, extra, sd in ((hi, list(plugs), "L"), (other, other_plugs, "R")):
        lo = cut(o, extra, tris)
        if after:
            after(lo)
        out.append(piece(part, lo, bones(sd)))
    return out[0]


# --------------------------------------------------------------------------- the body under the clothes

# the torso from the crotch to the collar: x, y, z, half-width, half-depth, squareness
TORSO_KEYS = [
    (0, 0.004, 0.905, 0.090, 0.086, 2.2), (0, 0.004, 0.930, 0.152, 0.103, 2.3), (0, 0.002, 0.980, 0.157, 0.106, 2.3),
    (0, 0.004, 1.040, 0.146, 0.098, 2.2), (0, 0.006, 1.090, 0.138, 0.093, 2.2), (0, 0.002, 1.170, 0.150, 0.103, 2.3),
    (0, -0.006, 1.250, 0.168, 0.115, 2.4), (0, -0.004, 1.320, 0.178, 0.116, 2.5), (0, 0.006, 1.385, 0.180, 0.102, 2.6),
    (0, 0.014, 1.432, 0.132, 0.084, 2.3), (0, 0.020, 1.468, 0.074, 0.066, 2.0), (0, 0.021, 1.490, 0.059, 0.059, 2.0),
]


def torso_keys(grow=0.0, z0=0.0, z1=9.0):
    """The torso's sections between two heights, let out by `grow` (a hoodie, a vest over it)."""
    rows = _resample(TORSO_KEYS, 0.02)
    return [(r[0], r[1], r[2], r[3] + grow, r[4] + grow, r[5]) for r in rows if z0 <= r[2] <= z1]


def torso_folds(amount=1.0):
    def f(p, radial):
        z, ang = p[:, 2], angle_of(radial)
        side = np.abs(radial[:, 0])
        d = 0.004 * np.sin(2 * math.pi * z / 0.047 + 0.6 * np.sin(2 * ang)) * win(z, 1.0, 1.12) * (0.4 + 0.6 * side)   # bunched at the waist
        d += 0.003 * np.sin(2 * math.pi * z / 0.1 + 3 * ang) * win(z, 1.16, 1.34) * side                                # pulled from the armpits
        d -= 0.004 * gauss(p[:, 0], 0.012) * np.clip(radial[:, 1], 0, 1) * win(z, 1.05, 1.4, 0.06)                      # the spine's furrow
        d += 0.004 * gauss(np.abs(p[:, 0]) - 0.075, 0.05) * gauss(z - 1.3, 0.06) * np.clip(-radial[:, 1], 0, 1)         # chest
        return d * amount
    return f


def arm_keys(grow=0.0, end_z=0.925):
    """The left arm from inside the torso over the shoulder down to the cuff."""
    k = [(0.135, 0.020, 1.362, 0.040, 0.048), (0.190, 0.020, 1.348, 0.052, 0.058), (0.204, 0.025, 1.290, 0.052, 0.056),
         (0.214, 0.032, 1.220, 0.047, 0.050), (0.225, 0.040, 1.140, 0.041, 0.044), (0.232, 0.030, 1.085, 0.042, 0.043),
         (0.240, 0.012, 0.980, 0.034, 0.035), (0.2435, 0.004, end_z, 0.030, 0.029)]
    return [(x, y, z, a + grow, b + grow) for x, y, z, a, b in k]


def arm_folds(cuff):
    def f(p, radial):
        z, ang = p[:, 2], angle_of(radial)
        front = np.clip(-radial[:, 1], 0, 1)
        d = 0.0045 * np.sin(2 * math.pi * z / 0.036) * win(z, 1.085, 1.195, 0.02) * front        # the crook of the elbow
        d += 0.004 * np.sin(2 * math.pi * z / 0.17 + 2 * ang + 1.0) * win(z, 1.0, 1.3)             # two long folds down the sleeve
        d += 0.003 * gauss(z - 1.14, 0.04) * np.clip(radial[:, 1], 0, 1)                           # the elbow itself
        d += 0.003 * np.sin(2 * math.pi * z / 0.042 + ang) * win(z, cuff + 0.01, cuff + 0.07, 0.02)  # stacked above the cuff
        return d
    return f


def leg_folds(p, radial):
    z, ang = p[:, 2], angle_of(radial)
    back, front = np.clip(radial[:, 1], 0, 1), np.clip(-radial[:, 1], 0, 1)
    d = 0.007 * np.sin(2 * math.pi * z / 0.085 + 0.8 * np.sin(ang)) * win(z, 0.12, 0.27, 0.03) * (0.55 + 0.45 * front)  # the break over the shoe
    d += 0.005 * np.sin(2 * math.pi * z / 0.055) * win(z, 0.44, 0.56, 0.02) * back                # behind the knee
    d += 0.007 * gauss(z - 0.5, 0.04) * front                                                      # the knee
    d += 0.0045 * np.sin(2 * math.pi * z / 0.2 + 2 * ang + 0.5) * win(z, 0.58, 0.9)                 # long drag folds of the thigh
    d += 0.003 * np.sin(2 * math.pi * z / 0.05 - 3 * ang) * win(z, 0.86, 0.95, 0.02) * front        # the lap
    return d


def body_clothes(covered=False):
    """Shirt and trousers: the torso, the arms and the legs. Under the coat only what can be seen is built — the
    shirt in its V and the legs below the skirt: what is not there cannot poke through the coat when he runs.
    Returns the torso (things lie on its front)."""
    hi, plugs = cloth("torso", torso_keys(z0=1.14 if covered else 0.0), (0, 0.012), seg=72, disp=torso_folds())
    torso = piece("BodyClothes", cut(hi, plugs, 400 if covered else 900), TORSO + ["neck"])
    if not covered:
        hi, plugs = cloth("arm", arm_keys(), (0, 0.012), seg=40, disp=arm_folds(0.925))
        both_sides("BodyClothes", hi, 500, lambda sd: ["chest", f"shoulder.{sd}", f"upper_arm.{sd}", f"forearm.{sd}"], plugs=plugs)
    leg = [(0.085, 0.0, 0.99, 0.07, 0.086), (0.093, 0.0, 0.92, 0.082, 0.098), (0.099, -0.008, 0.74, 0.08, 0.09),
           (0.104, -0.014, 0.57, 0.064, 0.07), (0.105, -0.02, 0.5, 0.06, 0.064), (0.105, -0.004, 0.44, 0.056, 0.062),
           (0.105, 0.014, 0.34, 0.058, 0.067), (0.105, 0.024, 0.2, 0.052, 0.057), (0.105, 0.028, 0.098, 0.058, 0.067)]
    if covered:
        leg = [r for r in _resample(leg, 0.02) if r[2] < 0.66]
    hi, plugs = cloth("leg", leg, (0, 0.015), seg=40, disp=leg_folds, caps=(not covered, True))
    both_sides("BodyClothes", hi, 460 if covered else 560, lambda sd: ["hips", f"thigh.{sd}", f"shin.{sd}"], plugs=plugs)
    return torso


# --------------------------------------------------------------------------- the lab coat

def lapel_edge(z):
    """Half the width of the coat's V opening at a height (0 below the top button)."""
    return 0.058 * np.clip((np.asarray(z, float) - 1.245) / 0.215, 0, 1) ** 0.8


def coat_folds(p, radial):
    x, z, ang = p[:, 0], p[:, 2], angle_of(radial)
    front = np.clip(-radial[:, 1] * 1.6 - 0.3, 0, 1)
    low = np.clip((0.9 - z) / 0.3, 0, 1)
    back = np.clip(radial[:, 1], 0, 1)
    d = 0.006 * low * (np.sin(5 * ang + 0.6 + 3 * z) + 0.5 * np.sin(9 * ang + 2.0))                # the skirt hanging in flutes
    d += 0.0025 * np.sin(2 * math.pi * z / 0.05 + np.sin(2 * ang)) * win(z, 1.06, 1.15) * (1 - front)  # gathered at the waist
    d -= 0.005 * gauss(x, 0.014) * back * win(z, 0.5, 0.95, 0.05)                                  # the back vent
    d += 0.008 * gauss(np.abs(x) - 0.09, 0.05) * gauss(z - 1.31, 0.07) * back                      # shoulder blades
    d -= 0.004 * gauss(x, 0.02) * back * win(z, 1.1, 1.38, 0.04)                                   # the spine between them
    d += 0.006 * gauss(np.abs(x) - 0.08, 0.06) * gauss(z - 1.3, 0.07) * np.clip(-radial[:, 1], 0, 1)  # the chest
    d += 0.0035 * np.clip((x - 0.006) / 0.005, 0, 1) * front * win(z, 0.5, 1.26, 0.01)             # the left front lies over the right
    d -= 0.021 * np.clip((lapel_edge(z) - np.abs(x)) / 0.004, 0, 1) * front * (z > 1.25)           # the V: the shirt shows
    return d


def coat():
    """Knee-length lab coat: one shell from the collar to below the knees, hanging in folds, open in a V at the chest."""
    keys = [(0, 0.006, 0.525, 0.222, 0.158, 2.3), (0, 0.004, 0.62, 0.208, 0.149, 2.3), (0, 0.002, 0.8, 0.19, 0.135, 2.3),
            (0, 0.002, 0.96, 0.18, 0.128, 2.3), (0, 0.006, 1.1, 0.16, 0.112, 2.3), (0, -0.004, 1.25, 0.183, 0.128, 2.4),
            (0, -0.002, 1.33, 0.193, 0.129, 2.5), (0, 0.008, 1.395, 0.196, 0.114, 2.6), (0, 0.016, 1.442, 0.15, 0.096, 2.3),
            (0, 0.021, 1.478, 0.09, 0.08, 2.0), (0, 0.022, 1.5, 0.076, 0.073, 2.0)]
    hi, plugs = cloth("coat", keys, (0.04, 0.02), seg=128, disp=coat_folds)
    shell = piece("Coat", cut(hi, plugs, 1700), TORSO + ["neck", "thigh.L", "thigh.R"])
    coat_details(shell)
    hi, plugs = cloth("sleeve", arm_keys(0.009, 0.94), (0, 0.015), seg=40, disp=arm_folds(0.94))
    both_sides("Coat", hi, 440, lambda sd: ["chest", f"shoulder.{sd}", f"upper_arm.{sd}", f"forearm.{sd}"], plugs=plugs)


def front_of(obj, x, z):
    """The y of the front (-Y) surface of a mesh at (x, z), by a ray from in front."""
    bvh = BVHTree.FromPolygons([v.co for v in obj.data.vertices], [p.vertices for p in obj.data.polygons])
    hit, _, _, _ = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
    return hit.y if hit is not None else -0.12


def card(part, bones, centre, w, h, t, tilt=0.0, sink=0.5):
    """A thin flat box with eased edges (pocket, flap, badge) lying on a surface that faces -Y: `centre` is on the
    surface, the box sinks into it by a share of its thickness."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((w, t, h)), verts=bm.verts)
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=min(w, h, t) * 0.3, segments=1, profile=0.5, affect="EDGES")
    if tilt:
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(tilt, 3, "Y"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(centre) + Vector((0, t * (sink - 0.5), 0)), verts=bm.verts)
    return piece(part, mesh_obj("card", bm, smooth=False), bones)


def lanyard(surface, part="AccentBand"):
    """A team-coloured lanyard round the neck to an ID card on the chest."""
    upper = ["spine", "chest"]
    for s in (-1, 1):
        side = [(s * 0.058, 1.468), (s * 0.05, 1.4), (s * 0.032, 1.32), (s * 0.012, 1.245)]
        line = [Vector((x, front_of(surface, x, z) - 0.003, z)) for x, z in side]
        for a, b in zip(line, line[1:]):
            rod(part, upper + ["neck"], a + (a - b) * 0.08, b + (b - a) * 0.08, 0.004, seg=6)
    yb = front_of(surface, 0.0, 1.2)
    card("Sclera", upper, (0.0, yb, 1.2), 0.056, 0.082, 0.005, sink=0.3)   # the ID card
    card("EyeDetails", upper, (-0.008, yb - 0.004, 1.212), 0.024, 0.03, 0.002, sink=0.5)  # its photo


def coat_details(shell):
    """Lapels, a breast pocket with a pen, two hip pockets with flaps, buttons, and the badge on a team-coloured lanyard."""
    upper = ["spine", "chest"]
    for s in (-1, 1):                                                       # lapels: flat rolls lying along the V
        keys = []
        for k in range(9):
            t = k / 8
            z = 1.478 - t * 0.25
            half = 0.007 + 0.012 * math.sin(min(1.0, t * 1.2) * math.pi) ** 0.8    # widest at the notch
            x = float(lapel_edge(z)) - 0.009 + half
            keys.append((s * x, front_of(shell, s * x, z) - 0.002, z, half, 0.004, 3.0))
        piece("Coat", mesh_obj("lapel", tube(keys, seg=10, step=0.03)), upper + ["neck"])
    bx, bz = 0.092, 1.285                                                   # breast pocket (left chest) and a pen
    card("Coat", upper, (bx, front_of(shell, bx, bz), bz), 0.086, 0.092, 0.007)
    py = front_of(shell, bx + 0.024, bz + 0.03)
    rod("EyeDetails", upper, (bx + 0.024, py - 0.004, bz - 0.02), (bx + 0.024, py - 0.006, bz + 0.075), 0.005, seg=8)
    for s in (-1, 1):                                                       # hip pockets and their flaps
        x, z = s * 0.11, 0.86
        card("Coat", ["hips", "spine"], (x, front_of(shell, x, z), z), 0.115, 0.125, 0.008)
        card("Coat", ["hips", "spine"], (x, front_of(shell, x, z + 0.058) - 0.003, z + 0.058), 0.123, 0.04, 0.014)
    for z in (1.19, 1.05, 0.91, 0.77):                                      # buttons down the overlap
        y = front_of(shell, 0.022, z)
        b = bmesh.new()
        bmesh.ops.create_cone(b, cap_ends=True, segments=10, radius1=0.009, radius2=0.0075, depth=0.006)
        bmesh.ops.rotate(b, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "X"), verts=b.verts)
        bmesh.ops.translate(b, vec=Vector((0.022, y - 0.001, z)), verts=b.verts)
        piece("Sclera", mesh_obj("button", b), ["hips", "spine", "chest"] if z < 1.0 else upper)
    lanyard(shell)


def armband(grow):
    """A team-coloured band round the left upper sleeve: the sleeve's own sections, a little wider."""
    rows = [r for r in _resample(arm_keys(grow), 0.012) if 1.275 <= r[2] <= 1.325]
    bm = tube([(r[0], r[1], r[2], r[3] + 0.0075, r[4] + 0.0075) for r in rows], seg=20, step=0.05, disp=None)
    piece("AccentBand", mesh_obj("band", bm), ["shoulder.L", "upper_arm.L"])


# --------------------------------------------------------------------------- skin: neck and hands

SKIN_PLAIN_UV = (0.5, 0.02)      # the plain corner of the face's skin map: the neck and the hands


def plain_uv(obj):
    uv = obj.data.uv_layers.new(name="UVMap")
    for d in uv.data:
        d.uv = SKIN_PLAIN_UV
    return obj


def skin_parts():
    """Skin: the neck and two hands — a palm, four fingers curled the way a loose hand hangs, a thumb."""
    def neck(p, radial):
        z = p[:, 2]
        front, side = np.clip(-radial[:, 1], 0, 1), np.abs(radial[:, 0])
        d = 0.005 * gauss(z - 1.51, 0.014) * front ** 6                                     # the Adam's apple
        d += 0.003 * gauss(side - 0.75, 0.2) * front * win(z, 1.46, 1.56, 0.02)             # the two muscles to the collar bones
        d -= 0.003 * front ** 4 * gauss(z - 1.47, 0.02)                                     # the hollow between them
        return d
    keys = [(0, 0.022, 1.44, 0.068, 0.062), (0, 0.02, 1.48, 0.057, 0.057), (0, 0.016, 1.53, 0.051, 0.054),
            (0, 0.012, 1.58, 0.05, 0.055)]
    hi = mesh_obj("neck", tube(keys, seg=32, disp=neck))
    piece("AccentHead", plain_uv(lowpoly(hi, 160)), ["chest", "neck", "head"])

    parts = [tube([(0.2415, 0.009, 0.975, 0.022, 0.025), (0.2425, 0.006, 0.945, 0.021, 0.026), (0.2435, 0.004, 0.915, 0.019, 0.026), (0.2465, 0.0, 0.875, 0.0155, 0.036),
                   (0.251, -0.003, 0.83, 0.0135, 0.042, 2.6), (0.2525, -0.003, 0.812, 0.011, 0.04, 2.6)], seg=24, step=0.004)]
    # fingers: where the knuckle is (y, z), how long, how thick, how much it curls towards the palm (-x)
    for y, z, ln, r, curl in ((-0.033, 0.818, 0.074, 0.0094, 0.8), (-0.0115, 0.813, 0.083, 0.0098, 1.0),
                              (0.0095, 0.815, 0.077, 0.0093, 1.15), (0.029, 0.822, 0.06, 0.0082, 1.3)):
        pts, x, zz, bend, last = [], 0.2525, z + 0.012, 0.2 * curl, 0.0
        for share, rr in ((0.0, 1.0), (0.16, 1.02), (0.45, 0.98), (0.52, 1.04), (0.74, 0.9), (0.8, 0.95), (1.0, 0.78)):
            step = (share - last) * (ln + 0.012)
            x -= math.sin(bend) * step
            zz -= math.cos(bend) * step
            if share in (0.52, 0.8):
                bend += 0.5 * curl                                                         # the two joints
            last = share
            pts.append((x, y, zz, r * rr, r * rr * 1.04))
        parts.append(tube(pts, seg=12, step=0.004))
    thumb = [(0.243, -0.02, 0.895, 0.017, 0.02), (0.239, -0.036, 0.872, 0.0145, 0.016), (0.2345, -0.05, 0.846, 0.0115, 0.0118),
             (0.231, -0.058, 0.82, 0.0102, 0.0104), (0.2285, -0.062, 0.798, 0.0085, 0.0085)]
    parts.append(tube(thumb, seg=12, step=0.004))
    hi = fuse(mesh_obj("hand", merged(parts)), 0.0022, smooth=6)
    wrist = Vector((0.2435, 0.004, 0.93))
    for v in hi.data.vertices:                                              # the hand of a grown man: chin to hairline
        v.co = wrist + (v.co - wrist) * (1.1 if v.co.z < 0.93 else 1.0)
    both_sides("AccentHead", hi, 420, lambda sd: [f"forearm.{sd}", f"hand.{sd}"], after=plain_uv)


def boots():
    """Work shoes under the trouser legs: a foot with a toe cap and a heel, a short shaft, a thick sole."""
    foot = [(0.105, 0.088, 0.05, 0.03, 0.042, 2.6), (0.105, 0.06, 0.057, 0.041, 0.053, 2.6), (0.105, 0.02, 0.064, 0.045, 0.06, 2.5),
            (0.107, -0.04, 0.052, 0.048, 0.047, 2.4), (0.109, -0.09, 0.042, 0.051, 0.038, 2.6), (0.111, -0.135, 0.037, 0.047, 0.033, 2.7),
            (0.112, -0.166, 0.033, 0.032, 0.027, 2.3)]
    sole = [(x, y, 0.014, a + 0.005, 0.014, 4.0) for x, y, z, a, b, n in foot]
    sole[0] = (0.105, 0.092, 0.014, 0.033, 0.014, 4.0)
    sole[-1] = (0.112, -0.171, 0.014, 0.036, 0.014, 4.0)
    heel = [(0.105, 0.09, 0.012, 0.035, 0.012, 4.0), (0.105, 0.06, 0.012, 0.046, 0.012, 4.0), (0.105, 0.02, 0.012, 0.048, 0.012, 4.0)]
    shaft = [(0.105, 0.03, 0.06, 0.044, 0.054), (0.105, 0.028, 0.11, 0.044, 0.052), (0.105, 0.027, 0.155, 0.043, 0.049)]
    parts = [tube(foot, seg=28, step=0.008, ref=(0, 0, 1)), tube(sole, seg=28, step=0.008, ref=(0, 0, 1)),
             tube(heel, seg=28, step=0.008, ref=(0, 0, 1)), tube(shaft, seg=28, step=0.01)]
    hi = fuse(mesh_obj("boot", merged(parts)), 0.004, smooth=3)
    bm = bmesh.new()
    bm.from_mesh(hi.data)
    bm.normal_update()
    for v in bm.verts:                                                      # a welt between the sole and the upper, flat soles
        if 0.024 < v.co.z < 0.034:
            v.co -= v.normal * 0.0025
        v.co.z = max(v.co.z, 0.0)
    bm.to_mesh(hi.data)
    bm.free()
    both_sides("EyeDetails", hi, 300, lambda sd: [f"shin.{sd}", f"foot.{sd}"])


# --------------------------------------------------------------------------- the departments

def rod(part, bones, a, b, r, seg=8):
    """A thin round rod from a to b (a strap, a string, a temple of the glasses)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=max(d.length, 1e-4))
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.rotate(bm, cent=Vector(), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    return piece(part, mesh_obj("rod", bm), bones)


def ring(part, bones, centre, radius, minor, axis="Y", seg=24, scale=(1.0, 1.0, 1.0), sides=8):
    """A torus round an axis (a rim of the glasses, a headband, a scarf round the neck)."""
    bm = bmesh.new()
    pts = []
    for k in range(seg):
        a = 2 * math.pi * k / seg
        ring_pts = []
        for j in range(sides):
            b = 2 * math.pi * j / sides
            rr = radius + minor * math.cos(b)
            ring_pts.append(bm.verts.new(Vector((rr * math.cos(a), rr * math.sin(a), minor * math.sin(b)))))
        pts.append(ring_pts)
    for k in range(seg):
        for j in range(sides):
            bm.faces.new((pts[k][j], pts[(k + 1) % seg][j], pts[(k + 1) % seg][(j + 1) % sides], pts[k][(j + 1) % sides]))
    if axis == "Y":
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "X"), verts=bm.verts)
    elif axis == "X":
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=bm.verts)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(centre), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return piece(part, mesh_obj("ring", bm), bones)


def blob(part, bones, centre, size, rot=(0.0, 0.0, 0.0), seg=(18, 10)):
    """A smooth squashed ball (a beret, a headphone cup)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg[0], v_segments=seg[1], radius=0.5)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    bmesh.ops.rotate(bm, cent=Vector(), matrix=Euler(rot).to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(centre), verts=bm.verts)
    return piece(part, mesh_obj("blob", bm), bones)


def eye_points():
    return [hp((s * EYE[0], EYE[1], EYE[2])) for s in (-1, 1)]


def belt(torso):
    """A dark belt with a buckle where the shirt meets the trousers."""
    keys = [(r[0], r[1], r[2], r[3] + 0.005, r[4] + 0.005, 2.6) for r in torso_keys(0.0, 1.01, 1.05)]
    piece("EyeDetails", mesh_obj("belt", tube(keys, seg=28, step=0.02)), ["hips", "spine"])
    card("Sclera", ["hips", "spine"], (0.0, front_of(torso, 0.0, 1.03) - 0.005, 1.03), 0.04, 0.03, 0.006, sink=0.2)


def department(dept, torso):
    """What tells the four departments apart at a glance (PlayerLooks colours: Body* the department's, Accent* the team's,
    Eye* dark, Sclera white): the backend developer's hoodie and big headphones, the QA engineer's lanyard and big glasses,
    the sysadmin's dark vest with pockets, a coil of cable and a head torch, the designer's scarf and beret."""
    if dept == "backend":
        def folds(p, radial):
            z = p[:, 2]
            d = torso_folds(1.4)(p, radial)
            d += 0.006 * gauss(z - 0.995, 0.02)                                 # the ribbed hem over the belt
            d += 0.006 * win(z, 1.42, 1.5, 0.02)                                # the thick collar
            return d
        hi, plugs = cloth("hoodie", torso_keys(0.014, 0.97, 1.475), (0.02, 0.012), seg=72, disp=folds)
        hoodie = piece("BodyHoodie", cut(hi, plugs, 1100), TORSO + ["neck"])
        hood = [(0, 0.07, 1.375, 0.07, 0.03), (0, 0.088, 1.42, 0.105, 0.05), (0, 0.098, 1.47, 0.1, 0.05), (0, 0.094, 1.51, 0.06, 0.03)]
        piece("BodyHoodie", lowpoly(mesh_obj("hood", tube(hood, seg=24, step=0.01)), 220), ["chest", "neck"])  # the hood lying on the back
        card("BodyHoodie", ["hips", "spine"], (0.0, front_of(hoodie, 0.07, 1.07), 1.07), 0.21, 0.115, 0.016, sink=0.35)  # the front pocket
        for s in (-1, 1):                                               # its drawstrings
            top = (s * 0.03, front_of(hoodie, s * 0.03, 1.44) - 0.002, 1.44)
            low = (s * 0.036, front_of(hoodie, s * 0.036, 1.32) - 0.003, 1.32)
            rod("Sclera", ["chest", "neck"], top, low, 0.0035)
        # big over-ear headphones: a band over the hair, a cup on each ear
        band = [hp((0.099 * math.cos(t), 0.016, 0.158 * math.sin(t) - 0.004)) for t in (math.pi * k / 12 for k in range(13))]
        for a, b in zip(band, band[1:]):
            rod("EyeHeadphones", ["head"], a + (a - b) * 0.15, b + (b - a) * 0.15, 0.008)
        for s in (-1, 1):
            blob("EyeHeadphones", ["head"], hp((s * 0.094, 0.014, -0.004)), (0.04, 0.074, 0.086), seg=(12, 8))
    elif dept == "qa":
        lanyard(torso)
        # big round glasses: two rims, a bridge, temples back to the ears
        eyes = eye_points()
        for e in eyes:
            ring("EyeGlasses", ["head"], e + Vector((0, -0.021, -0.002)), 0.0215, 0.0026, seg=20, sides=6)
        rod("EyeGlasses", ["head"], eyes[0] + Vector((0.021, -0.021, 0.002)), eyes[1] + Vector((-0.021, -0.021, 0.002)), 0.0022)
        for s, e in zip((-1, 1), eyes):
            rod("EyeGlasses", ["head"], e + Vector((s * 0.0215, -0.021, 0.0)), hp((s * 0.081, 0.012, 0.012)), 0.0022)
    elif dept == "sysadmin":
        hi, plugs = cloth("vest", torso_keys(0.012, 1.0, 1.44), (0.02, 0.012), seg=72, disp=torso_folds(0.6))
        vest = piece("EyeVest", cut(hi, plugs, 800), TORSO)             # the vest: over the torso
        for x, z, w, hh in ((-0.085, 1.28, 0.074, 0.074), (0.085, 1.28, 0.074, 0.074), (-0.088, 1.1, 0.085, 0.085), (0.088, 1.1, 0.085, 0.085)):
            card("EyeVest", ["spine", "chest"], (x, front_of(vest, x, z), z), w, hh, 0.02, sink=0.3)  # pockets
            card("EyeVest", ["spine", "chest"], (x, front_of(vest, x, z + hh * 0.42) - 0.004, z + hh * 0.42), w + 0.008, 0.024, 0.016, sink=0.3)  # flaps
        # a coil of cable clipped to the belt at the side
        for k in range(3):
            ring("AccentCable", ["hips"], (0.166 + k * 0.007, 0.0, 0.95), 0.08 - k * 0.006, 0.009, axis="X", seg=16, sides=5)
        # a head torch: a band round the head, the lamp on the forehead
        ring("EyeTorch", ["head"], hp((0, 0.014, 0.052)), 1.0, 0.009, axis="Z", scale=(0.089 * HEAD_S, 0.109 * HEAD_S, 1.0), seg=28, sides=6)
        card("EyeTorch", ["head"], hp((0, -0.095, 0.052)), 0.046, 0.032, 0.03, sink=0.5)
        ring("Sclera", ["head"], hp((0, -0.111, 0.052)), 0.009, 0.004, seg=12, sides=6)
    elif dept == "designer":
        ring("AccentScarf", ["chest", "neck"], (0, 0.018, 1.468), 0.068, 0.03, axis="Z", scale=(1.0, 0.98, 1.0), seg=20)
        for k in range(4):                                              # its end hanging down the front
            z0, z1 = 1.44 - k * 0.07, 1.44 - (k + 1) * 0.07
            card("AccentScarf", ["chest"], (0.045, front_of(torso, 0.045, (z0 + z1) / 2) - 0.004, (z0 + z1) / 2), 0.07, 0.08, 0.02, sink=0.3)
        blob("EyeBeret", ["head"], hp((0.014, 0.014, 0.128)), (0.2, 0.215, 0.07), rot=(0.06, -0.14, 0.0))
        rod("EyeBeret", ["head"], hp((0.017, 0.014, 0.15)), hp((0.019, 0.014, 0.172)), 0.005)


# --------------------------------------------------------------------------- the head
#
# The head is sculpted the way clay is: volumes (skull, jaw, cheekbones, nose, lips, lids) blended smoothly into one
# field, sockets and creases carved out of it, the surface taken where the field is zero. Everything is in the head's
# own frame: metres from its centre, x to its left, -y the face, z up. The face is symmetric (|x|).

HEAD_C = Vector(P["head"]) + Vector((0, -0.004, 0.006))
HEAD_S = 1.06                        # the sculpt below is in its own units; the head on the body is this much bigger


def hp(p):
    """A point of the head's frame on the body."""
    return HEAD_C + Vector(p) * HEAD_S
EYE = (0.032, -0.0735, 0.012)        # the centre of an eyeball
EYE_R = 0.013
HEAD_O = np.array((0.0, 0.005, -0.035))   # the sculpt is sampled along rays from here: low, so they meet the mouth and the chin squarely


def _smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b + (a - b) * h - k * h * (1 - h)


def _smax(a, b, k):
    return -_smin(-a, -b, k)


def _ell(p, c, r):
    r = np.array(r, float)
    return (np.linalg.norm((p - np.array(c, float)) / r, axis=-1) - 1.0) * r.min()


def _cap(p, a, b, r0, r1=None):
    a, b = np.array(a, float), np.array(b, float)
    ab = b - a
    t = np.clip(((p - a) @ ab) / (ab @ ab), 0, 1)
    return np.linalg.norm(p - a - t[..., None] * ab, axis=-1) - (r0 + ((r0 if r1 is None else r1) - r0) * t)


def _eye_slit(q):
    """The almond between the lids: an ellipsoid bored through them, its outer corner a little higher."""
    d = q - np.array((EYE[0], EYE[1] - 0.014, EYE[2] - 0.0016))
    c, s = math.cos(0.1), math.sin(0.1)
    d = np.stack([d[..., 0] * c + d[..., 2] * s, d[..., 1], d[..., 2] * c - d[..., 0] * s], -1)
    return _ell(d, (0, 0, 0), (0.0165, 0.015, 0.0049))


def _lips(q):
    return np.minimum(_ell(q, (0, -0.0935, -0.0568), (0.0235, 0.0092, 0.0064)), _ell(q, (0, -0.092, -0.0698), (0.0195, 0.0098, 0.0072)))


def head_sdf(p):
    """Distance-like field of the head: negative inside."""
    q = np.array(p, float)
    q[..., 0] = np.abs(q[..., 0])
    d = _ell(q, (0, 0.012, 0.035), (0.08, 0.097, 0.088))                              # the skull
    d = _smin(d, _ell(q, (0, -0.022, -0.03), (0.063, 0.07, 0.072)), 0.03)               # the face
    d = _smin(d, _ell(q, (0, -0.03, -0.075), (0.046, 0.056, 0.033)), 0.02)             # the jaw
    d = _smin(d, _ell(q, (0.044, 0.014, -0.05), (0.012, 0.022, 0.022)), 0.022)           # its corners under the ears
    d = _smin(d, _ell(q, (0, 0.004, -0.083), (0.032, 0.046, 0.023)), 0.02)             # under the jaw
    d = _smin(d, _ell(q, (0, -0.087, -0.092), (0.023, 0.018, 0.016)), 0.018)               # the chin
    d = _smin(d, _ell(q, (0.049, -0.06, -0.02), (0.02, 0.02, 0.018)), 0.03)             # cheekbones
    d = _smin(d, _ell(q, (0.036, -0.058, -0.044), (0.016, 0.016, 0.02)), 0.014)         # cheeks
    d = _smin(d, _cap(q, (0.006, -0.087, 0.032), (0.046, -0.076, 0.031), 0.007, 0.005), 0.026)   # the brow ridge
    d = _smax(d, -_ell(q, (0.088, -0.045, 0.03), (0.012, 0.03, 0.03)), 0.03)            # temples
    d = _smax(d, -_ell(q, (0.032, -0.097, 0.011), (0.02, 0.014, 0.012)), 0.018)       # eye sockets
    d = _smin(d, _cap(q, (0, -0.083, 0.02), (0, -0.105, -0.025), 0.0065, 0.008), 0.012)  # the bridge of the nose
    d = _smin(d, _ell(q, (0, -0.109, -0.0295), (0.0078, 0.009, 0.0078)), 0.008)         # its tip
    d = _smin(d, _ell(q, (0.011, -0.096, -0.0345), (0.0062, 0.009, 0.0064)), 0.009)       # its wings
    d = _smax(d, -_ell(q, (0.007, -0.1, -0.0412), (0.0038, 0.0055, 0.003)), 0.003)     # nostrils
    d = _smin(d, _ell(q, (0, -0.074, -0.063), (0.034, 0.022, 0.024)), 0.014)              # the mound of the mouth
    d = _smin(d, _lips(q), 0.005)                                                       # (their own shapes part them)
    d = _smax(d, -_cap(q, (0, -0.103, -0.0445), (0, -0.105, -0.0505), 0.0016), 0.004)     # the philtrum
    d = _smin(d, _ell(q, EYE, (0.0158, 0.0158, 0.0158)), 0.004)                      # the lids over the eyeball
    d = _smax(d, -_cap(q, (0.02, -0.0895, 0.021), (0.045, -0.083, 0.0195), 0.0012), 0.003)  # the crease of the upper lid
    d = _smax(d, -_eye_slit(q), 0.0015)
    return d


def ear_sdf(p):
    """An ear in its own frame: x out of the head, y to the back, z up."""
    d = _ell(p, (0.004, 0.002, 0.0), (0.0065, 0.0165, 0.031))                           # the shell
    d = _smin(d, _ell(p, (0.003, -0.006, -0.026), (0.005, 0.008, 0.009)), 0.006)        # the lobe
    d = _smin(d, _ell(p, (-0.004, -0.008, -0.002), (0.009, 0.01, 0.02)), 0.006)         # where it grows from the head
    d = _smax(d, -_ell(p, (0.01, -0.003, -0.004), (0.006, 0.008, 0.011)), 0.004)        # the bowl
    d = _smax(d, -_ell(p, (0.0105, 0.004, 0.013), (0.005, 0.007, 0.01)), 0.004)         # the hollow under the rim
    d = _smin(d, _ell(p, (0.006, -0.014, -0.006), (0.004, 0.004, 0.005)), 0.003)        # the tragus
    return d


EAR_AT = np.array((0.0755, 0.014, -0.004))
EAR_ROT = np.array(Matrix.Rotation(math.radians(-15), 3, "Z") @ Matrix.Rotation(math.radians(-8), 3, "X")) * 0.88


def hair_sdf(p):
    """Short hair: a layer over the skull, thicker on top, a fringe swept to one side; cut away from the face,
    round the ears and under the nape."""
    p = np.array(p, float)
    q = p.copy()
    q[..., 0] = np.abs(q[..., 0])
    d = _ell(p, (0.0, 0.014, 0.036), (0.0885, 0.111, 0.106))
    d = _smin(d, head_sdf(p) - 0.0045, 0.012)
    d = _smin(d, _ell(p, (0.016, -0.062, 0.094), (0.05, 0.024, 0.022)), 0.018)
    d = _smax(d, -_ell(q, (0, -0.135, -0.035), (0.1, 0.125, 0.13)), 0.0025)
    d = _smax(d, -(p[..., 2] + 0.44 * (p[..., 1] + 0.02) + 0.02) / 1.09, 0.008)
    d = _smax(d, -_ell(q, (0.084, 0.016, -0.006), (0.02, 0.019, 0.029)), 0.003)
    return d


def project(f, dirs, far, steps=200, refine=12, origin=(0.0, 0.0, 0.0)):
    """Where rays from the origin leave the solid f for the last time: the radius per direction."""
    if any(origin):
        inner, origin = f, np.array(origin)
        f = lambda p: inner(p + origin)
    ts = np.linspace(far, 0.0, steps)
    lo = np.zeros(len(dirs))
    hi = np.full(len(dirs), ts[1])
    done = np.zeros(len(dirs), bool)
    for t0, t1 in zip(ts, ts[1:]):
        hit = ~done & (f(dirs * t1) < 0)
        lo[hit], hi[hit] = t1, t0
        done |= hit
    hi[~done] = 0.0
    for _ in range(refine):
        mid = 0.5 * (lo + hi)
        inside = f(dirs * mid[:, None]) < 0
        lo = np.where(inside, mid, lo)
        hi = np.where(inside, hi, mid)
    return 0.5 * (lo + hi)


def ico(level):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=level, radius=1.0)
    bm.verts.ensure_lookup_table()
    dirs = np.array([v.co.normalized() for v in bm.verts])
    faces = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return dirs, faces


def from_arrays(sets):
    """A bmesh of (points, faces, flipped) sets."""
    bm = bmesh.new()
    for pts, faces, flip in sets:
        vs = [bm.verts.new(p) for p in pts]
        for f in faces:
            bm.faces.new([vs[i] for i in (reversed(f) if flip else f)])
    return bm


def head_uv(d):
    """UVs of a direction from the head's sampling origin (HEAD_O): u 0.5 at the face, v from under the chin (0) to the crown (1)."""
    d = d / np.maximum(np.linalg.norm(d, axis=-1, keepdims=True), 1e-9)
    return np.arctan2(d[..., 0], -d[..., 1]) / (2 * math.pi) + 0.5, np.arcsin(np.clip(d[..., 2], -1, 1)) / math.pi + 0.5


def head():
    """The head with its ears: the dense sculpt cut to the budget, UVs for the skin map."""
    dirs, faces = ico(6)
    dirs = dirs + np.array((0, -0.55, -0.1))                    # the sculpt is denser on the face: thin lips, lids
    dirs /= np.linalg.norm(dirs, axis=1)[:, None]
    sets = [((dirs * project(head_sdf, dirs, 0.2, origin=HEAD_O)[:, None] + HEAD_O) * HEAD_S + np.array(HEAD_C), faces, False)]
    edirs, efaces = ico(4)
    ear = (edirs * project(ear_sdf, edirs, 0.05, steps=60)[:, None]) @ EAR_ROT.T + EAR_AT
    for s in (1, -1):
        sets.append((ear * np.array((s, 1, 1)) * HEAD_S + np.array(HEAD_C), efaces, s < 0))
    hi = mesh_obj("head", from_arrays(sets))
    def face(pts):
        """The front of the head below the hairline; the scalp under the hair can go."""
        p = (pts - np.array(HEAD_C)) / HEAD_S
        w = np.clip((-p[:, 1] - 0.03) / 0.03, 0, 1) * np.clip((0.06 - p[:, 2]) / 0.02, 0, 1)
        w *= 0.6 + 0.4 * np.clip((0.045 - np.abs(p[:, 0])) / 0.01, 0, 1)                           # most of all its middle
        w = np.maximum(w, np.clip((np.abs(p[:, 0]) - 0.07) / 0.004, 0, 1) * (p[:, 2] < 0.04))      # and the ears
        q = np.abs(p) * (1, 0, 0) + p * (0, 1, 1)
        fine = sum(np.exp(-(np.linalg.norm((q - c) / r, axis=1)) ** 2) for c, r in (
            ((0, -0.095, -0.066), (0.03, 0.02, 0.014)), ((EYE[0], -0.085, EYE[2]), (0.02, 0.02, 0.012)),
            ((0, -0.105, -0.034), (0.02, 0.02, 0.014))))                                           # the mouth, the eyes, the nose
        return np.maximum(0.4 * w + 0.4 * np.clip(fine, 0, 1), np.where(hair_sdf(p) > 0, 0.15, 0.03))
    lo = lowpoly(hi, 3800, keep=face)
    me = lo.data
    uv = me.uv_layers.new(name="UVMap")
    centre = np.array(HEAD_C) + HEAD_O * HEAD_S
    for poly in me.polygons:
        us = [head_uv(np.array(me.vertices[me.loops[i].vertex_index].co) - centre) for i in poly.loop_indices]
        wrap = max(u for u, _ in us) - min(u for u, _ in us) > 0.5      # the seam at the back of the head
        for i, (u, w) in zip(poly.loop_indices, us):
            uv.data[i].uv = (u + 1.0 if wrap and u < 0.5 else u, w)
    piece("AccentHead", lo, ["head"])


def skin_map(path, n=512):
    """The face's skin (a multiplier of the player's skin tone), painted from the sculpt itself in the head's UVs:
    creases and sockets shaded, lips, the lash lines, warm cheeks, nose and ears, a shadow of stubble, pores."""
    v, u = np.mgrid[0:n, 0:n].astype(np.float64) / n
    v = 1.0 - v                                                           # image rows from the top
    a, lat = ((u - 0.5) * 2 * np.pi).ravel(), ((v - 0.5) * np.pi).ravel()
    dirs = np.stack([np.sin(a) * np.cos(lat), -np.cos(a) * np.cos(lat), np.sin(lat)], -1)
    p = dirs * project(head_sdf, dirs, 0.2, steps=200, refine=10, origin=HEAD_O)[:, None] + HEAD_O
    q = p.copy()
    q[:, 0] = np.abs(q[:, 0])
    e = 0.0008
    base = head_sdf(p)
    nrm = np.stack([head_sdf(p + np.array(o)) - base for o in ((e, 0, 0), (0, e, 0), (0, 0, e))], -1)
    nrm /= np.maximum(np.linalg.norm(nrm, axis=1), 1e-9)[:, None]
    fine = np.clip(head_sdf(p + nrm * 0.004) / 0.004, 0, 1)             # 1 on open skin, less in a crease
    wide = np.clip(head_sdf(p + nrm * 0.014) / 0.014, 0, 1)
    rng = np.random.default_rng(5)
    rgb = np.ones((n * n, 3)) * (0.975 + 0.025 * rng.random(n * n))[:, None]
    rgb *= ((0.7 + 0.3 * fine) * (0.88 + 0.12 * wide))[:, None]
    near = lambda c, w: np.exp(-(np.linalg.norm(q - np.array(c), axis=1) / w) ** 2)
    warm = near((0.046, -0.07, -0.03), 0.03) + 0.8 * near((0, -0.124, -0.033), 0.018) + 0.8 * near((0.08, 0.014, -0.004), 0.03)
    rgb *= 1 - np.clip(warm, 0, 1)[:, None] * np.array([-0.03, 0.09, 0.11])
    lips = np.clip(1 - _lips(q) / 0.0015, 0, 1) * (p[:, 1] < -0.085)
    rgb *= 1 - lips[:, None] * np.array([0.1, 0.27, 0.25])
    slit = _eye_slit(q)
    lash = np.clip(1 - np.abs(slit - 0.0006) / 0.0012, 0, 1) * np.where(q[:, 2] > EYE[2], 0.6, 0.25)
    rgb *= 1 - lash[:, None]
    lower = np.clip((-p[:, 2] - 0.05) / 0.025, 0, 1) * np.clip((0.02 - p[:, 1]) / 0.03, 0, 1) * np.clip((p[:, 2] + 0.125) / 0.02, 0, 1)
    stubble = np.clip(lower + near((0.012, -0.1, -0.049), 0.013), 0, 1) * (1 - lips) * (0.55 + 0.45 * (rng.random(n * n) > 0.5))
    rgb *= 1 - stubble[:, None] * np.array([0.12, 0.12, 0.1])
    rgb = rgb.reshape(n, n, 3)
    rgb[v < 0.06] = 0.97                                                  # under the chin: plain (hands and neck use it too)
    img = bpy.data.images.new("skin_face", n, n, alpha=False)
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((n, n, 1))], -1).astype(np.float32)
    img.pixels.foreach_set(np.ascontiguousarray(rgba[::-1]).ravel())
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def surface_y(x, z):
    """The y of the face at (x, z) in the head's frame."""
    lo, hi = -0.16, 0.0
    for _ in range(24):
        mid = 0.5 * (lo + hi)
        if head_sdf(np.array([[x, mid, z]]))[0] < 0:
            hi = mid
        else:
            lo = mid
    return hi


def hair_and_eyes():
    """Hair with combed strands, eyebrows and irises (dark details), the whites of the eyes (Sclera)."""
    dirs, faces = ico(6)
    pts = dirs * project(hair_sdf, dirs, 0.2, steps=100)[:, None]
    # strands: clumps combed away from the whorl at the back of the crown
    w = np.array((0.25, 0.5, 0.83))
    w /= np.linalg.norm(w)
    e1 = np.cross(w, (0, -1, 0))
    e1 /= np.linalg.norm(e1)
    e2 = np.cross(w, e1)
    phi, polar = np.arctan2(dirs @ e2, dirs @ e1), np.arccos(np.clip(dirs @ w, -1, 1))
    clumps = 1 - 2 * np.abs(np.sin(6 * phi + 1.2 * np.sin(3 * polar) + 0.5 * np.sin(11 * dirs[:, 0] + 3)))
    fine = np.sin(19 * phi + 4 * polar)
    edge = np.abs(pts.copy())
    edge[:, 1:] = pts[:, 1:]
    thick = np.clip(head_sdf(pts) / 0.008, 0, 1) * np.clip(_ell(edge, (0, -0.135, -0.035), (0.1, 0.125, 0.13)) / 0.02, 0.15, 1)
    pts = pts + dirs * ((0.0036 * clumps + 0.0012 * fine) * thick)[:, None]
    keep = head_sdf(pts) > -0.003                                        # the rest is under the skin
    index = np.cumsum(keep) - 1
    faces = [[index[i] for i in f] for f in faces if all(keep[i] for i in f)]
    bm = from_arrays([(pts[keep] * HEAD_S + np.array(HEAD_C), faces, False)])
    for _ in range(3):                                                  # the hairline's steps (rays graze its edge) eased
        bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    hi = mesh_obj("hair", bm)
    piece("EyeDetails", lowpoly(hi, 1200), ["head"])
    for s in (-1, 1):                                                   # eyebrows lying on the brow ridge
        keys = []
        for k in range(7):
            t = k / 6
            x = 0.011 + 0.036 * t
            z = 0.0285 + 0.004 * math.sin(math.pi * min(1.0, t * 1.3)) - 0.006 * t * t
            keys.append((s * x, surface_y(x, z) + 0.002, z, 0.0046 * (1 - 0.7 * t ** 2), 0.0032 * (1 - 0.5 * t ** 2)))
        bm = tube(keys, seg=6, step=0.012)
        bmesh.ops.scale(bm, vec=Vector((HEAD_S,) * 3), verts=bm.verts)
        bmesh.ops.translate(bm, vec=HEAD_C, verts=bm.verts)
        piece("EyeDetails", mesh_obj("brow", bm), ["head"])
    irises, whites = bmesh.new(), bmesh.new()
    for e in eye_points():
        bmesh.ops.create_uvsphere(whites, u_segments=10, v_segments=7, radius=EYE_R * HEAD_S, matrix=Matrix.Translation(e))
        cap = bmesh.new()
        r = (EYE_R + 0.0004) * HEAD_S
        bmesh.ops.create_uvsphere(cap, u_segments=12, v_segments=12, radius=r, matrix=Matrix.Rotation(math.pi / 2, 4, "X"))
        bmesh.ops.delete(cap, geom=[v for v in cap.verts if -v.co.y < r * math.cos(math.radians(31))], context="VERTS")
        bmesh.ops.translate(cap, vec=e, verts=cap.verts)
        me = bpy.data.meshes.new("_iris")
        cap.to_mesh(me)
        cap.free()
        irises.from_mesh(me)
        bpy.data.meshes.remove(me)
    piece("EyeDetails", mesh_obj("iris", irises), ["head"])
    piece("Sclera", mesh_obj("sclera", whites), ["head"])


# --------------------------------------------------------------------------- skeleton

BONES = [  # name, head, tail, parent
    ("hips", (0, 0, 0.94), (0, 0, 1.06), None),
    ("spine", (0, 0, 1.06), (0, 0.005, 1.24), "hips"),
    ("chest", (0, 0.005, 1.24), (0, 0.015, 1.44), "spine"),
    ("neck", (0, 0.02, 1.46), (0, 0.015, 1.56), "chest"),
    ("head", (0, 0.015, 1.56), (0, 0.0, 1.78), "neck"),
]
for side, s in (("L", 1), ("R", -1)):
    BONES += [
        (f"shoulder.{side}", (s * 0.04, 0.02, 1.43), mirror(P["shoulder"], s), "chest"),
        (f"upper_arm.{side}", mirror(P["shoulder"], s), mirror(P["elbow"], s), f"shoulder.{side}"),
        (f"forearm.{side}", mirror(P["elbow"], s), mirror(P["wrist"], s), f"upper_arm.{side}"),
        (f"hand.{side}", mirror(P["wrist"], s), mirror((0.258, -0.01, 0.72), s), f"forearm.{side}"),
        (f"thigh.{side}", mirror(P["hip"], s), mirror(P["knee"], s), "hips"),
        (f"shin.{side}", mirror(P["knee"], s), mirror(P["ankle"], s), f"thigh.{side}"),
        (f"foot.{side}", mirror(P["ankle"], s), mirror(P["toe"], s), f"shin.{side}"),
    ]


def skeleton():
    arm = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, h, t, parent in BONES:
        b = arm.edit_bones.new(name)
        b.head, b.tail = Vector(h), Vector(t)
        b.roll = 0.0
        if parent:
            b.parent = arm.edit_bones[parent]
            b.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
    return (a + ab * t - p).length


def bind(rig):
    """Weights by distance to the piece's own bones (smooth falloff), then the pieces of each part are joined."""
    bones = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in rig.data.bones}
    by_part = {}
    for part, obj, allowed in pieces:
        for name in bones:
            obj.vertex_groups.new(name=name)
        for v in obj.data.vertices:
            ds = sorted((seg_dist(v.co, *bones[n]), n) for n in allowed)[:3]
            ws = [1.0 / max(d, 0.01) ** 6 for d, _ in ds]
            tot = sum(ws)
            for (d, n), w in zip(ds, ws):
                if w / tot > 0.01:
                    obj.vertex_groups[n].add([v.index], w / tot, "REPLACE")
        by_part.setdefault(part, []).append(obj)
    result = []
    for part, objs in by_part.items():
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        obj.name = part
        obj.data.name = part
        obj.parent = rig
        mod = obj.modifiers.new("Rig", "ARMATURE")
        mod.object = rig
        result.append(obj)
    return result


# --------------------------------------------------------------------------- animation

def forward_sign(rig, bone, axis="X"):
    """+1 if a positive rotation about the bone's local axis swings its tail to the front (-Y), else -1."""
    pb = rig.pose.bones[bone]
    pb.rotation_mode = "XYZ"
    before = (rig.matrix_world @ pb.tail).y
    setattr(pb.rotation_euler, axis.lower(), 0.3)
    bpy.context.view_layer.update()
    after = (rig.matrix_world @ pb.tail).y
    pb.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    return 1.0 if after < before else -1.0


def outward_sign(rig, bone, side):
    """+1 if a positive rotation about local Z (bone roll axis) moves the tail away from the body (|x| grows)."""
    pb = rig.pose.bones[bone]
    pb.rotation_mode = "XYZ"
    before = abs((rig.matrix_world @ pb.tail).x)
    pb.rotation_euler.z = 0.3
    bpy.context.view_layer.update()
    after = abs((rig.matrix_world @ pb.tail).x)
    pb.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    return 1.0 if after > before else -1.0


def animate(rig):
    fwd = {b.name: forward_sign(rig, b.name) for b in rig.pose.bones}
    out = {b.name: outward_sign(rig, b.name, b.name[-1]) for b in rig.pose.bones if b.name.endswith((".L", ".R"))}
    rig.animation_data_create()
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"

    def key(action, frames, pose_fn):
        """Keys every bone at the frames with the pose returned by pose_fn(t in 0..1)."""
        act = bpy.data.actions.new(action)
        act.use_fake_user = True
        rig.animation_data.action = act
        for f in range(frames + 1):
            t = f / frames
            for pb in rig.pose.bones:
                pb.rotation_euler = (0, 0, 0)
                pb.location = (0, 0, 0)
            pose_fn(t)
            for pb in rig.pose.bones:
                pb.keyframe_insert("rotation_euler", frame=f + 1)
                pb.keyframe_insert("location", frame=f + 1)
        return act

    def rot(bone, forward=0.0, outward=0.0, twist=0.0):
        pb = rig.pose.bones[bone]
        e = pb.rotation_euler
        e.x += math.radians(forward) * fwd[bone]
        if outward:
            e.z += math.radians(outward) * out.get(bone, 1.0)
        if twist:
            e.y += math.radians(twist)

    def lower(dz):
        # hips bone points up: its local Y is world Z
        rig.pose.bones["hips"].location = (0, dz, 0)

    def arms_down():
        for s in ("L", "R"):
            rot(f"upper_arm.{s}", outward=-4)

    def idle(t):
        b = math.sin(t * 2 * math.pi)
        arms_down()
        rot("chest", forward=1.2 * b)
        rot("spine", forward=0.6 * b)
        rot("head", forward=-1.0 * b)
        for s in ("L", "R"):
            rot(f"upper_arm.{s}", forward=1.5 * b)
            rot(f"forearm.{s}", forward=8)

    def cycle(t, stride, knee, arm, elbow, lean, bob, crouch=0.0):
        ph = t * 2 * math.pi
        lower(-crouch + bob * (abs(math.cos(ph)) - 1.0))
        rot("spine", forward=lean * 0.5)
        rot("chest", forward=lean * 0.5)
        rot("head", forward=-lean * 0.7)
        rot("hips", twist=4 * math.sin(ph))
        rot("chest", twist=-6 * math.sin(ph))
        arms_down()
        for s, sgn in (("L", 1.0), ("R", -1.0)):
            ph_s = ph + (0 if sgn > 0 else math.pi)
            swing = math.sin(ph_s)
            lift = max(0.0, -math.cos(ph_s))                    # the leg in the air bends its knee
            base_thigh = crouch * 160
            rot(f"thigh.{s}", forward=stride * swing + base_thigh)
            rot(f"shin.{s}", forward=-(knee * lift + 6 + crouch * 300))
            rot(f"foot.{s}", forward=-0.4 * stride * swing + crouch * 140)
            rot(f"upper_arm.{s}", forward=-arm * swing)
            rot(f"forearm.{s}", forward=elbow + 0.4 * arm * max(0.0, -swing))

    def walk(t):
        cycle(t, stride=24, knee=45, arm=16, elbow=12, lean=3, bob=0.025)

    def run(t):
        cycle(t, stride=42, knee=95, arm=30, elbow=50, lean=12, bob=0.05)

    def crouch_idle(t):
        b = math.sin(t * 2 * math.pi)
        lower(-0.38)
        rot("spine", forward=18)
        rot("chest", forward=12 + b)
        rot("head", forward=-25)
        arms_down()
        for s in ("L", "R"):
            rot(f"thigh.{s}", forward=80)
            rot(f"shin.{s}", forward=-120)
            rot(f"foot.{s}", forward=40)
            rot(f"upper_arm.{s}", forward=25)
            rot(f"forearm.{s}", forward=55)

    def crouch_walk(t):
        cycle(t, stride=18, knee=30, arm=10, elbow=55, lean=0, bob=0.015, crouch=0.0)
        lower(-0.38)
        rot("spine", forward=18)
        rot("chest", forward=12)
        rot("head", forward=-25)
        for s, sgn in (("L", 1.0), ("R", -1.0)):
            rot(f"thigh.{s}", forward=80)
            rot(f"shin.{s}", forward=-120)
            rot(f"foot.{s}", forward=40)
            rot(f"upper_arm.{s}", forward=25)

    def down(t):
        # lying on the back: the whole body rotated about the hips, limbs loose
        hips = rig.pose.bones["hips"]
        hips.rotation_euler.x += math.radians(-88) * fwd["hips"]
        lower(-0.82)
        for s in ("L", "R"):
            rot(f"upper_arm.{s}", outward=-35)
            rot(f"forearm.{s}", forward=20)
            rot(f"thigh.{s}", forward=10)
            rot(f"shin.{s}", forward=-15)
        rot("head", forward=10 + 2 * math.sin(t * 2 * math.pi))

    def sit(t):
        # in the car's seat: the pelvis stays where it is (the model stands 0.85 m under the seat, the hips at 0.94),
        # thighs level, shins down, a little reclined, hands forward on the wheel, breathing
        b = math.sin(t * 2 * math.pi)
        lower(-0.02)
        rot("spine", forward=-6)
        rot("chest", forward=2 + 0.8 * b)
        rot("head", forward=4 - 0.8 * b)
        arms_down()
        for s in ("L", "R"):
            rot(f"thigh.{s}", forward=88, outward=4)
            rot(f"shin.{s}", forward=-84)
            rot(f"foot.{s}", forward=-4)
            rot(f"upper_arm.{s}", forward=38 + 1.0 * b, outward=6)
            rot(f"forearm.{s}", forward=42)

    acts = [key("Idle", 60, idle), key("Walk", 30, walk), key("Run", 20, run), key("CrouchIdle", 60, crouch_idle),
            key("CrouchWalk", 36, crouch_walk), key("Down", 60, down), key("Sit", 60, sit)]
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
    # every action as an NLA strip so the exporter writes one take per action
    for a in acts:
        tr = rig.animation_data.nla_tracks.new()
        tr.name = a.name
        tr.strips.new(a.name, 1, a)
        tr.mute = True
    return acts


# --------------------------------------------------------------------------- export / preview

def preview(rig, acts, path):
    """Workbench render: the six takes side by side at a quarter of their cycle."""
    s = bpy.context.scene
    objs = [o for o in s.objects if o.type == "MESH"]
    cols = {"BodyClothes": (0.22, 0.25, 0.3), "Coat": (0.78, 0.77, 0.7), "AccentHead": (0.85, 0.66, 0.54),
            "AccentBand": (0.95, 0.55, 0.2), "EyeDetails": (0.06, 0.05, 0.05), "Sclera": (0.85, 0.83, 0.78)}
    for o in objs:
        o.color = (*cols.get(o.name, (1, 0, 1)), 1)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    s.collection.objects.link(cam)
    cam.location = (0.35, -3.2, 1.1)
    cam.rotation_euler = (math.radians(88), 0, math.radians(6))
    cam.data.lens = 50
    s.camera = cam
    s.render.engine = "BLENDER_WORKBENCH"
    s.display.shading.color_type = "OBJECT"
    s.display.shading.light = "STUDIO"
    s.display.shading.show_cavity = True
    s.render.resolution_x, s.render.resolution_y = 600, 900
    base, ext = os.path.splitext(path)
    for a in acts:
        rig.animation_data.action = a
        s.frame_set(int(a.frame_range[1] * 0.25) + 1)
        s.render.filepath = f"{base}_{a.name}{ext}"
        bpy.ops.render.render(write_still=True)
    # a close-up of the face
    rig.animation_data.action = acts[0]
    s.frame_set(1)
    cam.location = (0.12, -0.55, 1.66)
    cam.rotation_euler = (math.radians(90), 0, math.radians(12))
    s.render.filepath = f"{base}_Face{ext}"
    bpy.ops.render.render(write_still=True)
    rig.animation_data.action = None


def drop_loose():
    """Vertices without faces (left by the fingers' skin bodies) go: nothing draws them, the model check counts them."""
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
            bm.to_mesh(obj.data)
        bm.free()


def export(rig):
    os.makedirs(OUT, exist_ok=True)
    drop_loose()
    if STATIC:
        bpy.ops.object.select_all(action="DESELECT")
        for o in bpy.data.objects:
            o.select_set(o.type == "MESH")
        bpy.ops.export_scene.fbx(filepath=STATIC, use_selection=True, axis_forward="-Z", axis_up="Y",
                                 bake_space_transform=True, object_types={"MESH"}, use_mesh_modifiers=True, bake_anim=False,
                                 apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"human_{DEPT or 'scientist'}.fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=False,
                             object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False, use_armature_deform_only=True, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0)


if __name__ == "__main__":
    clear()
    if not DEPT:
        skin_map(SKIN_MAP)
    torso = body_clothes(covered=not DEPT)
    if DEPT:
        belt(torso)
        department(DEPT, torso)
    else:
        coat()
    armband(0.0 if DEPT else 0.009); skin_parts(); boots(); head(); hair_and_eyes()
    rig = skeleton()
    bind(rig)
    acts = animate(rig)
    if PREVIEW:
        preview(rig, acts, PREVIEW)
    export(rig)
    print("DONE")
