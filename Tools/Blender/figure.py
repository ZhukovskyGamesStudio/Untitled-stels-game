"""Simple primitives and the per-family finishing for the player figures (players.py). The guards that used to live
here are designed per style in bestiary/ now (Tools/Blender/build_bestiary.py).
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


def legs(make):
    """Both legs from one function of the side (-1 left, +1 right), each cut at the knee into Upper and Lower."""
    g.split_leg(merge("AccentLeg0", make(-1)), KNEE)
    g.split_leg(merge("AccentLeg1", make(1)), KNEE)


KNEE = 0.45  # every design: hip ~0.9 m, knee at half of it, like the original guard
