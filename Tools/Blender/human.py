"""A realistic rigged human for the player (S11 «Заброшенный комплекс»): a station scientist in a grimy lab coat.

Run:  blender.exe -b --factory-startup --python Tools/Blender/human.py -- Assets/_Game/Art/Models/Humans [preview.png]

Writes <out>/human_scientist.fbx: one armature ("Rig") with skinned meshes and six in-place animation takes —
Idle, Walk, Run, CrouchIdle, CrouchWalk, Down. Real adult proportions (1.76 m, head 1/7.6 of the height). Built from
code: limbs and torso are skin-modifier tubes subdivided smooth, the head is a deformed sphere with jaw, cheekbones,
brow, nose, lips, ears and eye sockets, hands have fingers and a thumb.

Mesh names follow the player part convention (PlayerLooks.PartColor colours them per player):
  BodyClothes   jumpsuit / trousers            — the department's colour
  AccentHead    head, neck, hands (skin)       — the player's skin tone
  AccentBand    armband on the left sleeve     — the team colour
  EyeDetails    hair, eyebrows, irises, boots  — dark
  Coat          the lab coat                    — keeps its own (grimy off-white) material
  Sclera        the whites of the eyes          — keeps its own material
Every limb is its own skin-modifier piece weighted only to its own bones (no automatic heat weights: they failed on
thin fingers and left boots behind), then the pieces of one part are joined.
Blender: x right, -y the front, z up; Unity gets +Z forward (bake axis conversion on import).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector, noise
from mathutils.bvhtree import BVHTree
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models", "Humans"))
PREVIEW = os.path.abspath(argv[1]) if len(argv) > 1 else None
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


# --------------------------------------------------------------------------- skin-modifier bodies

def skin_mesh(name, verts, edges, radii, root=0, subdiv=2):
    """A tube body from a graph of points with per-point radii (rx, ry) through the skin modifier, smoothed."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([Vector(v) for v in verts], edges, [])
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    mod = obj.modifiers.new("skin", "SKIN")
    mod.use_smooth_shade = True
    mod.branch_smoothing = 0.6
    for i, sv in enumerate(me.skin_vertices[0].data):
        r = radii[i]
        sv.radius = (r, r) if isinstance(r, (int, float)) else r
        sv.use_root = i == root
    sub = obj.modifiers.new("sub", "SUBSURF")
    sub.levels = subdiv
    sub.render_levels = subdiv
    apply_all(obj)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


class Chain:
    """Collects points and edges for a skin body."""

    def __init__(self):
        self.v, self.e, self.r = [], [], []

    def add(self, p, r, parent=None):
        self.v.append(p)
        self.r.append(r)
        i = len(self.v) - 1
        if parent is not None:
            self.e.append((parent, i))
        return i

    def line(self, parent, pts):
        last = parent
        for p, r in pts:
            last = self.add(p, r, last)
        return last


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


def arm_chain(s, radii, start=None, end_z=0.92):
    """Points of an arm (shoulder to wrist) on side s with radii (shoulder, mid-upper, elbow, forearm, wrist)."""
    c = Chain()
    sh = c.add(mirror(start or (0.15, 0.02, 1.4), s), radii[0])
    c.line(sh, [(mirror(P["shoulder"], s), radii[0]),
                (mirror((0.205, 0.03, 1.29), s), radii[1]),
                (mirror(P["elbow"], s), radii[2]),
                (mirror((0.236, 0.02, 1.02), s), radii[3]),
                (mirror((0.243, 0.004, end_z), s), radii[4])])
    return c


def body_clothes():
    """Jumpsuit: torso, arms and legs (the coat covers most of it)."""
    c = Chain()
    pel = c.add(P["pelvis"], (0.15, 0.1))
    c.line(pel, [(P["waist"], (0.135, 0.092)), (P["chest"], (0.158, 0.105)), (P["upchest"], (0.165, 0.098)),
                 (P["neck0"], (0.07, 0.065))])
    piece("BodyClothes", skin_mesh("torso", c.v, c.e, c.r), TORSO + ["neck"])
    for s in (-1, 1):
        sd = side_bones(s)
        a = arm_chain(s, (0.058, 0.048, 0.042, 0.038, 0.032))
        piece("BodyClothes", skin_mesh("arm", a.v, a.e, a.r), ["chest", f"shoulder.{sd}", f"upper_arm.{sd}", f"forearm.{sd}"])
        lg = Chain()
        hp = lg.add(mirror((0.08, 0.0, 0.98), s), 0.082)
        lg.line(hp, [(mirror(P["hip"], s), 0.08), (mirror((0.1, -0.005, 0.72), s), 0.07), (mirror(P["knee"], s), 0.056),
                     (mirror((0.105, 0.01, 0.3), s), 0.05), (mirror((0.105, 0.03, 0.13), s), 0.042)])
        piece("BodyClothes", skin_mesh("leg", lg.v, lg.e, lg.r), ["hips", f"thigh.{sd}", f"shin.{sd}"])


def coat():
    """Knee-length lab coat: one smooth shell from the collar to below the knees, sleeves a little looser than the arms."""
    c = Chain()
    col = c.add((0, 0.022, 1.5), (0.072, 0.07))
    c.line(col, [((0, 0.01, 1.4), (0.188, 0.114)), ((0, 0.0, 1.25), (0.172, 0.12)), ((0, 0.005, 1.08), (0.152, 0.108)),
                 ((0, 0.0, 0.96), (0.19, 0.13)), ((0, 0.0, 0.8), (0.2, 0.138)), ((0, 0.0, 0.62), (0.208, 0.142)),
                 ((0, 0.0, 0.53), (0.21, 0.145))])
    shell = skin_mesh("coat", c.v, c.e, c.r)
    me = shell.data
    bm = bmesh.new()
    bm.from_mesh(me)
    cap = [f for f in bm.faces if all(v.co.z < 0.545 for v in f.verts)]
    bmesh.ops.delete(bm, geom=cap, context="FACES")
    # the opening at the front below the chest: two overlapping panels read as a seam
    for v in bm.verts:
        if v.co.y < -0.06 and abs(v.co.x) < 0.025 and v.co.z < 1.22:
            v.co.y += 0.01 * (1.0 - abs(v.co.x) / 0.025)
    bm.to_mesh(me)
    bm.free()
    piece("Coat", shell, TORSO + ["neck", "thigh.L", "thigh.R"])
    coat_details(shell)
    for s in (-1, 1):
        sd = side_bones(s)
        a = arm_chain(s, (0.068, 0.056, 0.05, 0.046, 0.043), start=(0.14, 0.02, 1.4), end_z=0.94)
        piece("Coat", skin_mesh("sleeve", a.v, a.e, a.r), ["chest", f"shoulder.{sd}", f"upper_arm.{sd}", f"forearm.{sd}"])


def front_of(obj, x, z):
    """The y of the front (-Y) surface of a mesh at (x, z), by a ray from in front."""
    bvh = BVHTree.FromPolygons([v.co for v in obj.data.vertices], [p.vertices for p in obj.data.polygons])
    hit, _, _, _ = bvh.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
    return hit.y if hit is not None else -0.12


def card(part, bones, centre, w, h, t, normal_y=-1.0, tilt=0.0):
    """A thin flat box (pocket, lapel, badge) lying on a surface that faces -Y."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((w, t, h)), verts=bm.verts)
    if tilt:
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(tilt, 3, "Y"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(centre), verts=bm.verts)
    piece(part, mesh_obj("card", bm, smooth=False), bones)


def coat_details(shell):
    """Lapels, a breast pocket with a pen, two hip pockets, buttons, and the badge on a team-coloured lanyard."""
    upper = ["spine", "chest"]
    for s in (-1, 1):                                                       # lapels: a folded ribbon from the collar down
        bm = bmesh.new()
        rows = []
        for k in range(9):
            t = k / 8
            z = 1.47 - t * 0.27
            xc = s * (0.04 + 0.03 * t)
            half = 0.01 + 0.016 * math.sin(t * math.pi * 0.8)              # widest at the notch, narrow at both ends
            row = []
            for xx in (xc - half, xc + half):
                row.append(bm.verts.new(Vector((xx, front_of(shell, xx, z) - 0.0035, z))))
            rows.append(row)
        for a, b in zip(rows, rows[1:]):
            bm.faces.new((a[0], a[1], b[1], b[0]) if s > 0 else (a[1], a[0], b[0], b[1]))
        bm.normal_update()
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.003)
        piece("Coat", mesh_obj("lapel", bm, smooth=False), upper + ["neck"])
    bx, bz = 0.085, 1.27                                                    # breast pocket (left chest) and a pen
    card("Coat", upper, (bx, front_of(shell, bx, bz) - 0.003, bz), 0.085, 0.09, 0.004)
    py = front_of(shell, bx + 0.025, bz + 0.05) - 0.008
    pen = bmesh.new()
    bmesh.ops.create_cone(pen, cap_ends=True, segments=10, radius1=0.005, radius2=0.005, depth=0.12)
    bmesh.ops.translate(pen, vec=Vector((bx + 0.025, py, bz + 0.03)), verts=pen.verts)
    piece("EyeDetails", mesh_obj("pen", pen), upper)
    for s in (-1, 1):                                                       # hip pockets
        x, z = s * 0.11, 0.86
        card("Coat", ["hips", "spine"], (x, front_of(shell, x, z) - 0.003, z), 0.11, 0.12, 0.004)
    for z in (1.18, 1.04, 0.9, 0.76):                                       # buttons down the overlap
        y = front_of(shell, 0.018, z) - 0.004
        b = bmesh.new()
        bmesh.ops.create_cone(b, cap_ends=True, segments=12, radius1=0.008, radius2=0.008, depth=0.004)
        bmesh.ops.rotate(b, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "X"), verts=b.verts)
        bmesh.ops.translate(b, vec=Vector((0.018, y, z)), verts=b.verts)
        piece("Sclera", mesh_obj("button", b), ["hips", "spine", "chest"] if z < 1.0 else upper)
    # lanyard round the neck to a badge on the chest
    pts = []
    for s in (-1, 1):
        side = [(s * 0.05, 1.47), (s * 0.045, 1.4), (s * 0.03, 1.32), (s * 0.012, 1.24)]
        pts.append([Vector((x, front_of(shell, x, z) - 0.006, z)) for x, z in side])
    for line in pts:
        for a, b in zip(line, line[1:]):
            d = b - a
            st = bmesh.new()
            bmesh.ops.create_cone(st, cap_ends=True, segments=8, radius1=0.004, radius2=0.004, depth=d.length)
            q = Vector((0, 0, 1)).rotation_difference(d.normalized())
            bmesh.ops.rotate(st, cent=Vector(), matrix=q.to_matrix(), verts=st.verts)
            bmesh.ops.translate(st, vec=(a + b) / 2, verts=st.verts)
            piece("AccentBand", mesh_obj("lanyard", st), upper + ["neck"])
    yb = front_of(shell, 0.0, 1.19) - 0.008
    card("Sclera", upper, (0.0, yb, 1.19), 0.055, 0.08, 0.003)             # the ID card
    card("EyeDetails", upper, (-0.01, yb - 0.002, 1.2), 0.022, 0.028, 0.001)  # its photo


def armband():
    """A team-coloured band around the left upper sleeve."""
    bm = bmesh.new()
    a, b = Vector(mirror((0.192, 0.026, 1.355), 1)), Vector(mirror((0.198, 0.028, 1.315), 1))
    d = b - a
    bmesh.ops.create_cone(bm, cap_ends=False, segments=24, radius1=0.062, radius2=0.06, depth=d.length)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.rotate(bm, cent=Vector(), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    me = bpy.data.meshes.new("band")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("band", me)
    bpy.context.collection.objects.link(obj)
    sol = obj.modifiers.new("sol", "SOLIDIFY")
    sol.thickness = 0.005
    apply_all(obj)
    piece("AccentBand", obj, ["upper_arm.L"])


def skin_parts():
    """Skin: the neck and two hands with four fingers and a thumb each."""
    c = Chain()
    n0 = c.add((0, 0.022, 1.45), 0.052)
    c.add((0, 0.016, 1.57), 0.05, n0)
    piece("AccentHead", skin_mesh("neck", c.v, c.e, c.r), ["chest", "neck", "head"])
    for s in (-1, 1):
        sd = side_bones(s)
        h = Chain()
        w = h.add(mirror((0.243, 0.004, 0.915), s), (0.03, 0.024))
        palm = h.add(mirror((0.25, -0.006, 0.845), s), (0.044, 0.02), w)
        for dy, ln in ((-0.032, 0.078), (-0.011, 0.088), (0.01, 0.083), (0.029, 0.068)):
            base = h.add(mirror((0.254, dy, 0.808), s), 0.0115, palm)
            h.line(base, [(mirror((0.257, dy - 0.005, 0.808 - ln * 0.55), s), 0.0102),
                          (mirror((0.259, dy - 0.012, 0.808 - ln), s), 0.0088)])
        th = h.add(mirror((0.238, -0.032, 0.87), s), 0.0135, palm)
        h.line(th, [(mirror((0.234, -0.058, 0.835), s), 0.0115), (mirror((0.232, -0.07, 0.805), s), 0.0098)])
        piece("AccentHead", skin_mesh("hand", h.v, h.e, h.r), [f"forearm.{sd}", f"hand.{sd}"])


def boots():
    for s in (-1, 1):
        sd = side_bones(s)
        c = Chain()
        top = c.add(mirror((0.105, 0.025, 0.22), s), 0.052)
        ank = c.add(mirror((0.105, 0.03, 0.09), s), (0.054, 0.062), top)
        c.add(mirror((0.105, 0.065, 0.045), s), (0.046, 0.042), ank)
        ball = c.add(mirror((0.11, -0.07, 0.04), s), (0.052, 0.036), ank)
        c.add(mirror((0.112, -0.14, 0.036), s), (0.044, 0.03), ball)
        obj = skin_mesh("boot", c.v, c.e, c.r)
        for v in obj.data.vertices:            # flat soles on the floor
            v.co.z = max(v.co.z, 0.0)
        piece("EyeDetails", obj, [f"shin.{sd}", f"foot.{sd}"])


# --------------------------------------------------------------------------- the head

def head_shape(x, y, z):
    """Unit sphere point -> head surface point (metres, centred at the head point). Face looks to -Y."""
    front = max(0.0, -y)
    sx, sy, sz = 0.079, 0.097, 0.11
    if z < 0:                                                   # jaw: narrower towards the chin
        x *= 1.0 - 0.13 * (-z) ** 1.8
        y *= 1.0 - 0.08 * (-z) * front
    if z > 0.4 and y > 0:                                       # fuller back of the skull
        y *= 1.0 + 0.06 * (z - 0.4)
    def g(cx, cz, wx, wz, mirror_x=False):
        xx = abs(x) if mirror_x else x
        return math.exp(-((xx - cx) / wx) ** 2 - ((z - cz) / wz) ** 2)
    cheek = g(0.6, -0.08, 0.22, 0.2, True) * front
    brow = g(0.0, 0.3, 0.6, 0.08) * front ** 2
    sock = g(0.34, 0.16, 0.16, 0.1, True) * front ** 2
    nose = g(0.0, -0.0, 0.07, 0.2) * front ** 4               # a narrow bridge
    tip = g(0.0, -0.13, 0.09, 0.06) * front ** 4
    nostril = g(0.12, -0.18, 0.06, 0.04, True) * front ** 3
    lip_up = g(0.0, -0.33, 0.3, 0.05) * front ** 3
    lip_lo = g(0.0, -0.44, 0.26, 0.05) * front ** 3
    mouth = g(0.0, -0.385, 0.3, 0.014) * front ** 3
    chin = g(0.0, -0.68, 0.3, 0.12) * front ** 2
    temple = g(0.75, 0.25, 0.15, 0.2, True) * front
    out = (0.05 * cheek + 0.06 * brow - 0.075 * sock + 0.24 * nose + 0.1 * tip + 0.035 * nostril + 0.035 * lip_up
           + 0.03 * lip_lo - 0.025 * mouth + 0.05 * chin - 0.03 * temple)
    p = Vector((x, y, z))
    n = p.normalized() if p.length > 1e-6 else Vector((0, 0, 1))
    p = p + n * out
    return Vector((p.x * sx, p.y * sy, p.z * sz))


HEAD_C = Vector(P["head"]) + Vector((0, -0.004, 0.0))


def head():
    """A sphere sculpted into a face by displacement fields, with ears."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=56, v_segments=36, radius=1.0)
    uv = bm.loops.layers.uv.new("UVMap")
    face_uv = {}
    for v in bm.verts:
        face_uv[v] = head_uv(*v.co)
    for f in bm.faces:
        us = [face_uv[l.vert][0] for l in f.loops]
        wrap = max(us) - min(us) > 0.5                                  # the seam at the back of the head
        for l in f.loops:
            u, w = face_uv[l.vert]
            l[uv].uv = ((u + 1.0) if wrap and u < 0.5 else u, w)
    for v in bm.verts:
        v.co = head_shape(*v.co)
    for s in (-1, 1):
        e = bmesh.new()
        bmesh.ops.create_uvsphere(e, u_segments=14, v_segments=10, radius=1.0)
        for v in e.verts:                                       # flattened shell with a rim
            v.co = Vector((v.co.x * 0.011 * (1.0 if v.co.x * s > 0 else 0.5), v.co.y * 0.021, v.co.z * 0.031))
        bmesh.ops.rotate(e, cent=Vector(), matrix=Matrix.Rotation(-s * 0.3, 3, "Z"), verts=e.verts)
        bmesh.ops.translate(e, vec=Vector((s * 0.074, 0.01, 0.0)), verts=e.verts)
        me = bpy.data.meshes.new("ear")
        e.to_mesh(me)
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        e.free()
    bmesh.ops.translate(bm, vec=HEAD_C, verts=bm.verts)
    me = bpy.data.meshes.new("head")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("head", me)
    bpy.context.collection.objects.link(obj)
    sub = obj.modifiers.new("sub", "SUBSURF")
    sub.levels = 1
    apply_all(obj)
    for p in obj.data.polygons:
        p.use_smooth = True
    piece("AccentHead", obj, ["head"])


def head_uv(x, y, z):
    """Cylindrical UVs of a point of the unit sphere: u 0.5 at the face, v from the chin (0) to the crown (1)."""
    u = math.atan2(x, -y) / (2 * math.pi) + 0.5
    w = math.asin(max(-1.0, min(1.0, z))) / math.pi + 0.5
    return u, w


def skin_map(path, n=512):
    """The face's skin (a multiplier of the player's skin tone): warm cheeks and nose, darker lips, a shadow of
    stubble on the jaw and upper lip, darker eye sockets, pores. Painted in the head's UV space (head_uv)."""
    v, u = np.mgrid[0:n, 0:n].astype(np.float32) / n
    v = 1.0 - v                                                           # image rows from the top
    a = (u - 0.5) * 2 * np.pi
    lat = (v - 0.5) * np.pi
    x, y, z = np.sin(a) * np.cos(lat), -np.cos(a) * np.cos(lat), np.sin(lat)
    front = np.clip(-y, 0, 1)
    def g(cx, cz, wx, wz, mirror=False):
        xx = np.abs(x) if mirror else x
        return np.exp(-((xx - cx) / wx) ** 2 - ((z - cz) / wz) ** 2)
    rng = np.random.default_rng(5)
    pores = rng.random((n, n)).astype(np.float32)
    rgb = np.ones((n, n, 3), np.float32) * (0.975 + 0.025 * pores[..., None])
    warm = g(0.55, -0.12, 0.25, 0.2, True) * front + 0.8 * g(0.0, -0.12, 0.12, 0.1) * front ** 3
    rgb *= 1 - warm[..., None] * np.array([-0.04, 0.1, 0.12])
    lips = (g(0.0, -0.33, 0.28, 0.05) + g(0.0, -0.44, 0.24, 0.05)) * front ** 3
    rgb *= 1 - np.clip(lips, 0, 1)[..., None] * np.array([0.12, 0.35, 0.32])
    sock = g(0.34, 0.16, 0.2, 0.12, True) * front ** 2
    rgb *= 1 - sock[..., None] * np.array([0.18, 0.2, 0.17])
    stubble_zone = np.clip((-z - 0.25) * 2.5, 0, 1) * front ** 0.7 + g(0.0, -0.27, 0.35, 0.05) * front ** 2
    stubble = np.clip(stubble_zone, 0, 1) * (0.6 + 0.4 * (rng.random((n, n)) > 0.5))
    rgb *= 1 - stubble[..., None] * np.array([0.16, 0.16, 0.14])
    rgb[v < 0.12] = 0.97                                                  # under the chin: plain (hands and neck use it too)
    img = bpy.data.images.new("skin_face", n, n, alpha=False)
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((n, n, 1), np.float32)], -1)
    img.pixels.foreach_set(np.ascontiguousarray(rgba[::-1]).ravel())
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def mesh_obj(name, bm, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for p in obj.data.polygons:
        p.use_smooth = smooth
    return obj


def hair_and_eyes():
    """Short hair (a cap over the skull), eyebrows, irises (dark details) and the whites of the eyes (Sclera)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=28, radius=1.0)
    drop = []
    for v in bm.verts:
        x, y, z = v.co
        front = max(0.0, -y)
        hairline = 0.38 + 0.25 * front ** 2 if y < 0 else -0.25 - 0.2 * max(0.0, y)
        if abs(x) > 0.8 and y < 0.3:
            hairline = max(hairline, 0.2)                       # above the ears
        if z < hairline:
            drop.append(v)
        p = head_shape(x, y, z)
        n = noise.noise(Vector((x, y, z)) * 9.0) * 0.006 + noise.noise(Vector((x, y, z)) * 3.0) * 0.004
        grow = 0.007 + 0.014 * max(0.0, z) ** 1.5 + max(0.0, n)
        if y < -0.2 and z > 0.35:                                       # a fringe falling over the forehead
            grow += 0.006 * max(0.0, noise.noise(Vector((x * 14.0, 0.0, 0.0))))
        v.co = p + p.normalized() * grow
    bmesh.ops.delete(bm, geom=drop, context="VERTS")
    for s in (-1, 1):                                           # eyebrows
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.LocRotScale(
            head_shape(s * 0.38, -0.9, 0.3) + Vector((0, -0.004, 0)), Euler((0, s * 0.1, 0)), Vector((0.03, 0.005, 0.005))))
    eye_at = [head_shape(s * 0.34, -0.93, 0.16) + Vector((0, 0.008, 0)) for s in (-1, 1)]
    for p in eye_at:                                            # irises, a little in front of the whites
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.0062,
                                  matrix=Matrix.Translation(p + Vector((0, -0.0075, 0))))
    bmesh.ops.translate(bm, vec=HEAD_C, verts=bm.verts)
    piece("EyeDetails", mesh_obj("hair", bm), ["head"])
    w = bmesh.new()
    for p in eye_at:
        bmesh.ops.create_uvsphere(w, u_segments=16, v_segments=10, radius=0.0118, matrix=Matrix.Translation(p))
    bmesh.ops.translate(w, vec=HEAD_C, verts=w.verts)
    piece("Sclera", mesh_obj("sclera", w), ["head"])
    lids = bmesh.new()                                                  # eyelids: the upper lid over the top of the eye,
    for p in eye_at:                                                    # a thin lower lid: a relaxed look, not a stare
        lid = bmesh.new()
        bmesh.ops.create_uvsphere(lid, u_segments=16, v_segments=12, radius=0.0128)
        drop = [v for v in lid.verts if not (v.co.z > 0.002 or v.co.z < -0.0095) or v.co.y > 0.003]
        bmesh.ops.delete(lid, geom=drop, context="VERTS")
        bmesh.ops.rotate(lid, cent=Vector(), matrix=Matrix.Rotation(0.12, 3, "X"), verts=lid.verts)
        bmesh.ops.translate(lid, vec=p, verts=lid.verts)
        me = bpy.data.meshes.new("lid")
        lid.to_mesh(me)
        lids.from_mesh(me)
        bpy.data.meshes.remove(me)
        lid.free()
    bmesh.ops.translate(lids, vec=HEAD_C, verts=lids.verts)
    piece("AccentHead", mesh_obj("lids", lids), ["head"])


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

    acts = [key("Idle", 60, idle), key("Walk", 30, walk), key("Run", 20, run), key("CrouchIdle", 60, crouch_idle),
            key("CrouchWalk", 36, crouch_walk), key("Down", 60, down)]
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


def export(rig):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "human_scientist.fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=False,
                             object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False, use_armature_deform_only=True, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0)


if __name__ == "__main__":
    clear()
    skin_map(SKIN_MAP)
    body_clothes(); coat(); armband(); skin_parts(); boots(); head(); hair_and_eyes()
    rig = skeleton()
    bind(rig)
    acts = animate(rig)
    if PREVIEW:
        preview(rig, acts, PREVIEW)
    export(rig)
    print("DONE")
