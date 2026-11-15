"""Backpack pictures of the real items: renders of their own models, drawn over the item's cells in the inventory.

Run:  blender.exe -b --factory-startup --python Tools/Blender/item_icons.py -- Assets/_Game/Art/Sprites/Items [Assets/_Game/Art/Models]

Writes <out>/<id>.png (RGBA, 128 px per inventory cell, the size of the shape's cells in its base turn):
  shovel      1 x 4 cells (ItemCatalog.Shovel)       Art/Models/Items/item_shovel_model.fbx
  flashlight  1 x 3 cells (ItemCatalog.Flashlight)   Art/Models/Items/item_flashlight_model.fbx
  battery     1 x 1 cell  (ItemCatalog.Battery)      Art/Models/Items/item_battery_model.fbx
  diamond, key, cagekey, stun (2 x 2), speaker (2 x 1)  the tools and the crafted diamond, seen three-quarter
Each model lies along X in its file; it is turned to stand along the picture's height (its head up), seen from
above with a little tilt, lit by a soft key and a rim light, rendered with Cycles on a transparent film, then given
a dark outline so it reads on any cell colour. The UI turns and mirrors the picture with the item.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Euler, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Sprites", "Items"))
MODELS = os.path.abspath(argv[1] if len(argv) > 1 else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
CELL = 128

# id: (cells wide, cells tall, model file (Items/ unless a folder is given), {part prefix: (colour, metallic, roughness,
# emission)}, view: "top" — lying things seen from above, "front" — standing things seen three-quarter from the front)
ITEMS = {
    "shovel": (1, 4, "item_shovel_model", {
        "Body": ((0.42, 0.26, 0.12), 0.0, 0.6, 0.0),
        "Accent": ((0.55, 0.57, 0.6), 0.85, 0.35, 0.0)}),
    "flashlight": (1, 3, "item_flashlight_model", {
        "Body": ((0.09, 0.09, 0.1), 0.6, 0.45, 0.0),
        "AccentLens": ((0.85, 0.9, 1.0), 0.0, 0.05, 0.6),
        "Accent": ((0.62, 0.64, 0.66), 0.9, 0.3, 0.0)}),
    "battery": (1, 1, "item_battery_model", {
        "Body": ((0.1, 0.38, 0.85), 0.0, 0.35, 0.0),
        "Accent": ((0.7, 0.72, 0.74), 0.9, 0.3, 0.0)}),
    # the tools and the crafted diamond: their own models in the world, pictures in the backpack too
    "diamond": (1, 1, "item_diamond", {"Body": ((0.75, 0.92, 1.0), 0.0, 0.05, 0.25)}, "front"),
    "key": (1, 1, "item_key", {"Body": ((0.95, 0.72, 0.2), 0.95, 0.25, 0.0)}, "front"),
    "cagekey": (1, 1, "item_key", {"Body": ((0.55, 0.85, 0.2), 0.9, 0.3, 0.0)}, "front"),
    "stun": (2, 2, "item_stun", {"Body": ((0.15, 0.75, 0.85), 0.3, 0.3, 0.35)}, "front"),
    "speaker": (2, 1, "Props/lure_speaker", {
        "Body": ((0.14, 0.15, 0.16), 0.2, 0.5, 0.0),
        "EyeLed": ((0.3, 1.0, 0.6), 0.0, 0.3, 2.0),
        "Accent": ((0.05, 0.05, 0.05), 0.1, 0.6, 0.0)}, "front"),
}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, colour, metallic, roughness, emission):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission > 0:
        for key in ("Emission Color", "Emission"):
            if key in bsdf.inputs:
                bsdf.inputs[key].default_value = (*colour, 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission
    return m


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def render(item_id, wide, tall, model, looks, view="top"):
    clear()
    scene = bpy.context.scene
    path = os.path.join(MODELS, model + ".fbx") if "/" in model else os.path.join(MODELS, "Items", model + ".fbx")
    bpy.ops.import_scene.fbx(filepath=path)
    objs = [o for o in scene.objects if o.type == "MESH"]
    for o in objs:
        role = next((k for k in sorted(looks, key=len, reverse=True) if o.name.startswith(k)), "Body")
        o.data.materials.clear()
        o.data.materials.append(material(f"{item_id}_{role}", *looks[role]))
    # one pivot for the whole model, centred, turned so its length (X in the file) runs up the picture, head up
    holder = bpy.data.objects.new("holder", None)
    scene.collection.objects.link(holder)
    bpy.context.view_layer.update()
    lo, hi = bounds(objs)
    centre = (lo + hi) / 2
    for o in [o for o in scene.objects if o.parent is None and o is not holder]:
        o.location -= centre
        o.parent = holder
    if view == "top":
        # a lying thing: its length (X in the file) along the picture's longer side, its head up / to the right
        holder.rotation_euler = Euler((0, 0, math.radians(90) if tall > wide else 0))
    bpy.context.view_layer.update()

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    # top: from above with a little tilt (the sides read); front: three-quarter from the front (-Y) and above
    tilt = math.radians(7 if view == "top" else 58)
    cam.location = (0, -3.0 * math.sin(tilt), 3.0 * math.cos(tilt))
    cam.rotation_euler = Euler((tilt, 0, 0))
    if view == "front":
        holder.rotation_euler = Euler((0, 0, math.radians(-25)))  # turned a little: two sides show
    scene.camera = cam
    bpy.context.view_layer.update()
    # fit: the model's corners seen from the camera, the frame's aspect from the cells
    inv = cam.matrix_world.inverted()
    pts = [inv @ (o.matrix_world @ Vector(c)) for o in objs for c in o.bound_box]
    w = max(p.x for p in pts) - min(p.x for p in pts)
    h = max(p.y for p in pts) - min(p.y for p in pts)
    cx = (max(p.x for p in pts) + min(p.x for p in pts)) / 2
    cy = (max(p.y for p in pts) + min(p.y for p in pts)) / 2
    cam.location = cam.matrix_world @ Vector((cx, cy, 0))
    margin = 1.12
    if tall >= wide:  # Blender's ortho scale covers the longer side of the frame
        cam_data.ortho_scale = max(h * margin, w * margin * tall / wide)
    else:
        cam_data.ortho_scale = max(w * margin, h * margin * wide / tall)

    for name, rot, energy, size in (("key", (math.radians(40), math.radians(-30), math.radians(20)), 3.5, 0.4),
                                    ("rim", (math.radians(-55), math.radians(35), 0), 2.0, 0.2)):
        ld = bpy.data.lights.new(name, "SUN")
        ld.energy = energy
        ld.angle = size
        lobj = bpy.data.objects.new(name, ld)
        lobj.rotation_euler = Euler(rot)
        scene.collection.objects.link(lobj)
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.35, 0.36, 0.38, 1)
    bg.inputs["Strength"].default_value = 0.6
    scene.world = world

    try:
        scene.render.engine = "CYCLES"
    except TypeError:
        pass
    scene.cycles.samples = 48
    scene.cycles.device = "CPU"
    try:
        scene.cycles.use_denoising = True
    except Exception:  # noqa: BLE001
        pass
    scene.render.film_transparent = True
    scene.render.resolution_x = wide * CELL
    scene.render.resolution_y = tall * CELL
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, f"{item_id}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    outline(path)
    print("wrote", path)


def outline(path, px=4):
    """A dark rim round the picture (where its alpha ends) so it reads on any cell colour."""
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.array(im.pixels[:], np.float32).reshape(h, w, 4)
    alpha = a[..., 3]
    grown = alpha.copy()
    for dy in range(-px, px + 1):
        for dx in range(-px, px + 1):
            if dx * dx + dy * dy <= px * px:
                grown = np.maximum(grown, np.roll(np.roll(alpha, dy, 0), dx, 1))
    ring = np.clip(grown - alpha, 0, 1)
    out = a.copy()
    premul = a[..., :3] * alpha[..., None] + np.array([0.04, 0.04, 0.04], np.float32) * ring[..., None]
    out[..., 3] = np.clip(alpha + ring * 0.9, 0, 1)
    out[..., :3] = premul / np.maximum(out[..., 3:], 1e-4)    # straight alpha again
    im.pixels.foreach_set(out.ravel())
    im.save()


for item, spec in ITEMS.items():
    render(item, *spec)
