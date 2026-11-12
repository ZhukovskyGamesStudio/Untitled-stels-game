"""Voxel look for a style (Shadows-of-Doubt-like): every part of a finished model rebuilt from cubes on a grid.

Blender's Remesh modifier in Blocks mode, applied to each object separately (part names, pivots and the parts the game
animates stay as they were). The grid is centred on each part's bounds, so wheel and rotor centres do not move.
A part must keep its size: one that loses its thin bits (legs of a whiteboard, a spider's feet) is tried again on a
finer grid, and one that cannot be voxelized without falling apart (leaves, sheets of paper) is left as it was.
Glowing parts (Eye*) start on a finer grid: a status lamp or a slit must not vanish into one cube.
"""
import math

import bpy


def _bounds(mesh, matrix):
    if len(mesh.vertices) == 0:
        return None
    ws = [matrix @ v.co for v in mesh.vertices]
    return [min(w[i] for w in ws) for i in range(3)], [max(w[i] for w in ws) for i in range(3)]


def _blocks(obj, size):
    """The object's mesh rebuilt from cubes of about `size` (a new mesh datablock; the object is unchanged)."""
    dims = obj.dimensions
    big = max(dims.x, dims.y, dims.z)
    depth = min(10, max(1, math.ceil(math.log2(big / size))))
    mod = obj.modifiers.new("Voxels", "REMESH")
    mod.mode = "BLOCKS"
    mod.octree_depth = depth
    mod.scale = min(0.99, big / (size * 2 ** depth))
    mod.use_remove_disconnected = False
    deps = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
    obj.modifiers.remove(mod)
    return mesh


def voxelize_object(obj, size, tolerance=0.03):
    """Rebuilds one mesh object from cubes of about `size` metres; finer if it would lose its shape, untouched if even
    that fails. Returns the cube size used (0 = left as it was)."""
    dims = obj.dimensions
    if max(dims.x, dims.y, dims.z) < size * 1.5:
        return 0.0
    want = _bounds(obj.data, obj.matrix_world)
    if want is None:
        return 0.0
    for s in (size, size / 2, size / 4):
        if max(dims.x, dims.y, dims.z) < s * 1.5:
            break
        mesh = _blocks(obj, s)
        got = _bounds(mesh, obj.matrix_world)
        off = 1e9 if got is None else max(max(abs(a - b) for a, b in zip(want[0], got[0])), max(abs(a - b) for a, b in zip(want[1], got[1])))
        if off <= max(tolerance, s * 0.6):
            old = obj.data
            mesh.name = old.name
            obj.data = mesh
            bpy.data.meshes.remove(old)
            for p in obj.data.polygons:
                p.use_smooth = False
            return s
        bpy.data.meshes.remove(mesh)
    return 0.0


def voxelize(objs, size):
    """Every mesh object of a model; glowing parts on a grid twice as fine."""
    for o in objs:
        if o.type != "MESH":
            continue
        voxelize_object(o, size * 0.5 if o.name.startswith("Eye") else size)
