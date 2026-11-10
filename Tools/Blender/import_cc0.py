"""Converts the downloaded CC0 models (Tools/Art/fetch_cc0.py) into FBX for Unity.

Run:  blender.exe -b --factory-startup --python Tools/Blender/import_cc0.py -- <cache dir> <out dir> [preview.png]

For every model in <cache>/models/<id>/*.gltf: everything is joined into one object per material, the base is centred
on the origin (x, y) and stands on z = 0, the front faces -Y (Unity +Z) — YAW below turns models that face elsewhere.
Writes <out>/<id>.fbx and <out>/manifest.json (material name -> source diffuse / normal / ARM images), which
Tools/Art/fetch_cc0.py --models turns into Unity textures.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
cache, out = argv[0], argv[1]
preview = argv[2] if len(argv) > 2 else None

# Degrees around Z so that the front of the model faces -Y (checked on the preview render).
YAW = {"modern_coffee_table_01": 90, "metal_trash_can": 0}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def images_of(mat):
    """The image files feeding a Principled BSDF: base colour, normal map, and the packed ARM (rough/metal)."""
    res = {}
    if not mat or not mat.use_nodes:
        return res
    for node in mat.node_tree.nodes:
        if node.type != "TEX_IMAGE" or not node.image:
            continue
        path = bpy.path.abspath(node.image.filepath)
        name = os.path.basename(path).lower()
        if "_diff" in name or "_col" in name:
            res["diff"] = path
        elif "_nor" in name:
            res["nor"] = path
        elif "_arm" in name or "_rough" in name:
            res["arm"] = path
    return res


def convert(model_id, gltf):
    clear()
    bpy.ops.import_scene.gltf(filepath=gltf)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    # apply parent transforms, then join into one object (one submesh per material)
    for o in bpy.context.scene.objects:
        o.select_set(o.type == "MESH")
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = "Body"
    for o in list(bpy.context.scene.objects):
        if o != obj:
            bpy.data.objects.remove(o, do_unlink=True)
    yaw = YAW.get(model_id, 0)
    if yaw:
        obj.data.transform(Matrix.Rotation(math.radians(yaw), 4, "Z"))
    pts = [v.co for v in obj.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    shift = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for v in obj.data.vertices:
        v.co -= shift
    mats = {}
    for i, m in enumerate(obj.data.materials):
        if m is None:
            continue
        m.name = f"{model_id}_{i}"
        mats[m.name] = images_of(m)
    os.makedirs(out, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, model_id + ".fbx"), use_selection=False, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH"},
                             mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="STRIP")
    return {"size": list(hi - lo), "materials": mats}


def render_preview(ids, path):
    clear()
    scene = bpy.context.scene
    for k, model_id in enumerate(ids):
        bpy.ops.import_scene.fbx(filepath=os.path.join(out, model_id + ".fbx"))
        for o in bpy.context.selected_objects:
            o.location.x += (k % 5) * 3.0
            o.location.z -= (k // 5) * 3.0
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 1500, 1000
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 15.5
    cam.location = (6, -20, -1.6)
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


manifest = {}
models_dir = os.path.join(cache, "models")
for model_id in sorted(os.listdir(models_dir)):
    folder = os.path.join(models_dir, model_id)
    gltf = [f for f in os.listdir(folder) if f.endswith(".gltf")]
    if not gltf:
        continue
    manifest[model_id] = convert(model_id, os.path.join(folder, gltf[0]))
    print("converted", model_id, manifest[model_id]["size"])
with open(os.path.join(out, "manifest.json"), "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=1)
if preview:
    render_preview(sorted(manifest), preview)
print("DONE", len(manifest))
