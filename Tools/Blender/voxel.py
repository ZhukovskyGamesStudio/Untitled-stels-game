"""Voxel look for a style (Shadows-of-Doubt-like): every part of a finished model rebuilt from cubes on a grid.

Blender's Remesh modifier in Blocks mode, applied to each object separately (part names, pivots and the parts the game
animates stay as they were). The grid is centred on each part's bounds, so wheel and rotor centres do not move.
A part must keep its size and stay in one piece: one that loses its thin bits (legs of a whiteboard, a spider's feet) or
falls apart into loose cubes (a thin leg came out as a dotted line) is tried again on a finer grid, and one that cannot be voxelized without falling apart (leaves, sheets of paper) is left as it was.
Glowing parts (Eye*) start on a finer grid: a status lamp or a slit must not vanish into one cube.
"""
import math

import bmesh
import bpy
import mathutils


def _islands(mesh):
    """How many separate pieces a mesh is made of (faces joined through shared vertices)."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    seen, n = set(), 0
    for f0 in bm.faces:
        if f0.index in seen:
            continue
        n += 1
        stack = [f0]
        seen.add(f0.index)
        while stack:
            f = stack.pop()
            for v in f.verts:
                for g in v.link_faces:
                    if g.index not in seen:
                        seen.add(g.index)
                        stack.append(g)
    bm.free()
    return n


def _bounds(mesh, matrix):
    if len(mesh.vertices) == 0:
        return None
    ws = [matrix @ v.co for v in mesh.vertices]
    return [min(w[i] for w in ws) for i in range(3)], [max(w[i] for w in ws) for i in range(3)]


def _blocks(obj, size, world=False):
    """The object's mesh rebuilt from cubes of about `size` (a new mesh datablock; the object is unchanged). With
    `world`, the cubes sit on one grid for the whole model (two loose corner vertices make the part's bounds a cube of
    2^n cells starting on a grid line): a box on a shelf stays on it — on grids of their own the two surfaces rounded
    apart and the box floated."""
    added = 0
    if world:
        me = obj.data
        mw = obj.matrix_world
        ws = [mw @ v.co for v in me.vertices]
        lo = [math.floor(min(w[k] for w in ws) / size) * size - size for k in range(3)]
        hi = [max(w[k] for w in ws) for k in range(3)]
        span = max(hi[k] - lo[k] for k in range(3)) + size
        depth = min(10, max(1, math.ceil(math.log2(span / size))))
        side = size * 2 ** depth
        inv = mw.inverted()
        base = len(me.vertices)
        me.vertices.add(2)
        me.vertices[base].co = inv @ mathutils.Vector(lo)
        me.vertices[base + 1].co = inv @ mathutils.Vector([lo[k] + side for k in range(3)])
        me.update()
        added = 2
        scale = 1.0
    else:
        dims = obj.dimensions
        big = max(dims.x, dims.y, dims.z)
        depth = min(10, max(1, math.ceil(math.log2(big / size))))
        scale = min(0.99, big / (size * 2 ** depth))
    mod = obj.modifiers.new("Voxels", "REMESH")
    mod.mode = "BLOCKS"
    mod.octree_depth = depth
    mod.scale = scale
    mod.use_remove_disconnected = False
    deps = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
    obj.modifiers.remove(mod)
    if added:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.verts[i] for i in range(len(bm.verts) - added, len(bm.verts))], context="VERTS")
        bm.to_mesh(obj.data)
        bm.free()
    return mesh


def voxelize_object(obj, size, tolerance=0.03, world=False):
    """Rebuilds one mesh object from cubes of about `size` metres; finer if it would lose its shape, untouched if even
    that fails. Returns the cube size used (0 = left as it was)."""
    dims = obj.dimensions
    if max(dims.x, dims.y, dims.z) < size * 1.5:
        return 0.0
    want = _bounds(obj.data, obj.matrix_world)
    if want is None:
        return 0.0
    pieces = _islands(obj.data)
    for s in (size, size / 2, size / 4, size / 8):
        if max(dims.x, dims.y, dims.z) < s * 1.5:
            break
        mesh = _blocks(obj, s, world)
        got = _bounds(mesh, obj.matrix_world)
        off = 1e9 if got is None else max(max(abs(a - b) for a, b in zip(want[0], got[0])), max(abs(a - b) for a, b in zip(want[1], got[1])))
        if off <= max(tolerance, s * 0.6) and _islands(mesh) <= pieces:
            old = obj.data
            mesh.name = old.name
            obj.data = mesh
            bpy.data.meshes.remove(old)
            for p in obj.data.polygons:
                p.use_smooth = False
            return s
        bpy.data.meshes.remove(mesh)
    return 0.0


PROUD = 0.0015
"""How far every part but the body stands out of its cubes, metres: two parts on the same grid touch face to face where
they meet (an accent on the body) and those faces flickered."""


def voxelize(objs, size, world=False):
    """Every mesh object of a model; glowing parts on a grid twice as fine; every part but the body a hair proud.
    `world`: all parts on one grid (furniture, nothing moves; a robot's wheels and legs keep their own, centred)."""
    for o in objs:
        if o.type != "MESH":
            continue
        if voxelize_object(o, size * 0.5 if o.name.startswith("Eye") else size, world=world) and not o.name.startswith("Body"):
            o.data.update()
            for v in o.data.vertices:
                v.co += v.normal * PROUD
