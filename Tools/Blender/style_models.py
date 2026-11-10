"""Style variants of every model: the same parts, reshaped for a visual style.

Run (after generate_models.py / robots.py):
  blender.exe -b --factory-startup --python Tools/Blender/style_models.py -- Assets/_Game/Art/Models

For every Art/Models/<Category>/<name>.fbx writes Art/Models/Styles/<Treatment>/<Category>/<name>__<treatment>.fbx with the
same child objects (Body, Accent, Eye, AccentRotor0…): the game swaps the meshes by name (VisualStyles), materials,
pivots and colliders stay. Deterministic: the noise is seeded by the file and object names.

Treatments:
  faceted  crystal facets: denser where the model is simple, fewer where it is round, every vertex nudged, flat shading
           (SUPERHOT, Deep Rock Galactic, PEAK)
  toy      rounded bevels on every hard edge and smooth shading: a chunky plastic toy (R.E.P.O., Content Warning)
  wonky    hand-made: a slight lean and twist, wobbling corners (Sir, You Are Being Hunted, Lethal Company)
"""
import hashlib
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

argv = sys.argv
root = argv[argv.index("--") + 1] if "--" in argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
CATEGORIES = ("Items", "Level", "Player", "Props", "Robots", "Sensors")
TREATMENTS = ("faceted", "toy", "wonky")


def seed_of(*names):
    return int(hashlib.md5("/".join(names).encode()).hexdigest()[:8], 16)


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for item in list(block):
            block.remove(item)


def size_of(obj):
    m = obj.matrix_world
    pts = [m @ v.co for v in obj.data.vertices]
    if not pts:
        return Vector((0, 0, 0))
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return hi - lo


def world_bm(obj):
    """bmesh of the object in world space (metres, model axes) — every model was exported with its parts at the origin."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    return bm


def write_back(obj, bm, smooth):
    bm.transform(obj.matrix_world.inverted())
    for f in bm.faces:
        f.smooth = smooth
    bm.normal_update()
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def islands(bm):
    """Connected parts of a merged mesh (a desk = top + legs): each is reshaped by its own size."""
    seen, out = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, part = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            part.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        verts = {v for g in part for v in g.verts}
        lo = Vector((min(v.co.x for v in verts), min(v.co.y for v in verts), min(v.co.z for v in verts)))
        hi = Vector((max(v.co.x for v in verts), max(v.co.y for v in verts), max(v.co.z for v in verts)))
        out.append((part, verts, lo, hi))
    return out


def thinnest(lo, hi):
    d = hi - lo
    return max(1e-3, min(d.x, d.y, d.z))


def faceted(obj, seed):
    bm = world_bm(obj)
    if len(bm.faces) > 80:
        # Round parts: fewer, bigger facets.
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.to_mesh(obj.data)
        bm.free()
        mod = obj.modifiers.new("dec", "DECIMATE")
        mod.ratio = 0.35
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bm.transform(obj.matrix_world)
    else:
        # Boxes: cut every face once so that the facets can tilt.
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    bm.faces.ensure_lookup_table()
    off = Vector(((seed % 97) * 1.3, (seed % 89) * 0.7, (seed % 83) * 1.1))
    for part, verts, lo, hi in islands(bm):
        amp = min(0.09, 0.35 * thinnest(lo, hi))  # crystal facets, scaled to the part so thin legs survive
        for v in verts:
            v.co += noise.noise_vector(v.co * 2.1 + off) * amp
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    write_back(obj, bm, smooth=False)


def toy(obj, seed):
    bm = world_bm(obj)
    bm.faces.ensure_lookup_table()
    for part, verts, lo, hi in islands(bm):
        width = min(0.12, 0.3 * thinnest(lo, hi))
        edges = list({e for f in part for e in f.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(30)})
        if edges and width > 0.004:
            bmesh.ops.bevel(bm, geom=edges, offset=width, offset_type="OFFSET", segments=4, profile=0.5,
                            affect="EDGES", clamp_overlap=True)
        bm.faces.ensure_lookup_table()
    # A little puffed up, like moulded plastic.
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * 0.006
    write_back(obj, bm, smooth=True)


def wonky(obj, seed):
    bm = world_bm(obj)
    rnd = (seed % 1000) / 1000.0
    lean = (rnd - 0.5) * 0.16          # shear: up to ±8 cm per metre
    twist = ((seed // 1000) % 1000 / 1000.0 - 0.5) * 0.3  # radians per metre of height
    off = Vector(((seed % 61) * 0.9, (seed % 53) * 1.7, (seed % 47) * 0.5))
    bm.faces.ensure_lookup_table()
    parts = islands(bm)
    for k, (part, verts, lo, hi) in enumerate(parts):
        # every board and leg sits a little crooked on its own
        c = (lo + hi) / 2
        tilt = Matrix.Rotation(noise.noise(c * 3.1 + off) * 0.07, 3, "X") @ Matrix.Rotation(noise.noise(c * 2.7 - off) * 0.07, 3, "Y")
        amp = min(0.03, 0.15 * thinnest(lo, hi))
        for v in verts:
            p = c + tilt @ (v.co - c)
            h = max(0.0, p.z)
            p = Matrix.Rotation(twist * h, 3, "Z") @ p
            p.x += lean * h
            p += noise.noise_vector(p * 1.6 + off) * amp
            v.co = p
    write_back(obj, bm, smooth=False)


def export(path, smooth):
    bpy.ops.object.select_all(action="SELECT")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, object_types={"MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE",
        add_leaf_bones=False)


def main():
    done = 0
    for cat in CATEGORIES:
        folder = os.path.join(root, cat)
        if not os.path.isdir(folder):
            continue
        for file in sorted(os.listdir(folder)):
            if not file.endswith(".fbx"):
                continue
            name = file[:-4]
            for t in TREATMENTS:
                clear()
                bpy.ops.import_scene.fbx(filepath=os.path.join(folder, file), axis_forward="-Z", axis_up="Y",
                                         bake_space_transform=True)
                for obj in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
                    s = seed_of(name, obj.name)
                    {"faceted": faceted, "toy": toy, "wonky": wonky}[t](obj, s)
                export(os.path.join(root, "Styles", t.capitalize(), cat, f"{name}__{t}.fbx"), smooth=(t == "toy"))
                done += 1
    print("DONE", done)


main()
