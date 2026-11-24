"""Backpack pictures of the real items: renders of their own models, drawn over the item's cells in the inventory.

Run:  blender.exe -b --factory-startup --python Tools/Blender/item_icons.py -- Assets/_Game/Art/Sprites/Items [Assets/_Game/Art/Models] [--only a,b]

Writes <out>/<id>.png (RGBA, 128 px per inventory cell, the size of the shape's cells in its base turn):
  shovel      1 x 4 cells (ItemCatalog.Shovel)       Art/Models/Items/item_shovel_model.fbx
  flashlight  1 x 3 cells (ItemCatalog.Flashlight)   Art/Models/Items/item_flashlight_model.fbx
  battery     1 x 1 cell  (ItemCatalog.Battery)      Art/Models/Items/item_battery_model.fbx
  book 2 x 2, cans 2 x 2, screwdriver 1 x 3, beltbag 2 x 2   Art/Models/Items/item_<id>_model.fbx
  diamond, key, cagekey, stun (2 x 2), speaker (2 x 1)  the tools and the crafted diamond, seen three-quarter
  map         3 x 3 cells                            Art/Models/Items/item_map_model.fbx (apartments.py)
  radio, keyboard, tablet, medkit, coffee, finder, boots, tomatoes, chess, magazine, pills, toothpaste, tape,
  matches, toiletpaper, applecore, soap, gloves, necklace, mop, plush, socks, ssd, gamepad, gamekeyboard, canister,
  bandage   item_<id>_model.fbx (items_more.py)
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
ONLY = None
if "--only" in argv:  # render just these ids
    i = argv.index("--only")
    ONLY = set(argv[i + 1].split(","))
    argv = argv[:i] + argv[i + 2:]
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Sprites", "Items"))
MODELS = os.path.abspath(argv[1] if len(argv) > 1 else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
CELL = 128

PRINTED = "printed.png"          # a picture of Art/Textures/Covers in place of a colour

# id: (cells wide, cells tall, model file (Items/ unless a folder is given), {part prefix: (colour, metallic, roughness,
# emission)}, view: "top" — lying things seen from above, "tilted" — the same seen more from the near end (a book shows
# its pages), "front" — standing things seen three-quarter from the front)
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
    "book": (2, 2, "item_book_model", {
        "Body": (PRINTED, 0.0, 0.55, 0.0),                               # cloth cover with its gilt title
        "Accent": ((0.9, 0.86, 0.74), 0.0, 0.8, 0.0)}, "tilted"),        # pages
    "cans": (2, 2, "item_cans_model", {
        "Body": ((0.8, 0.22, 0.1), 0.0, 0.5, 0.0),                       # paper labels
        "Accent": ((0.7, 0.72, 0.74), 0.9, 0.3, 0.0)}, "front"),          # tin steel
    "screwdriver": (1, 3, "item_screwdriver_model", {
        "Body": ((0.9, 0.62, 0.08), 0.0, 0.35, 0.0),                     # plastic handle
        "Accent": ((0.62, 0.64, 0.66), 0.9, 0.3, 0.0)}),                 # steel
    "beltbag": (2, 2, "item_beltbag_model", {
        "Body": ((0.18, 0.3, 0.42), 0.0, 0.75, 0.0),                     # nylon
        "AccentZip": ((0.62, 0.64, 0.66), 0.9, 0.3, 0.0),                # zips and pulls
        "Accent": ((0.07, 0.07, 0.08), 0.0, 0.6, 0.0)}),                 # strap and buckle
    # the tools and the crafted diamond: their own models in the world, pictures in the backpack too
    "diamond": (1, 1, "item_diamond", {"Body": ((0.75, 0.92, 1.0), 0.0, 0.05, 0.25)}, "front"),
    "key": (1, 1, "item_key_model", {
        "Body": ((0.95, 0.72, 0.2), 0.95, 0.25, 0.0),
        "Accent": ((0.62, 0.64, 0.66), 0.9, 0.3, 0.0),
        "Trim": ((0.85, 0.16, 0.12), 0.0, 0.4, 0.0)}),
    "cagekey": (1, 2, "item_cagekey_model", {
        "Body": ((0.2, 0.19, 0.18), 0.8, 0.5, 0.0),
        "Accent": ((0.95, 0.72, 0.2), 0.95, 0.25, 0.0)}),
    "stun": (2, 2, "item_stun_model", {
        "Body": ((0.2, 0.24, 0.23), 0.0, 0.5, 0.0),                      # painted steel case
        "Accent": ((0.05, 0.05, 0.052), 0.0, 0.7, 0.0),                  # rubber
        "AccentSteel": ((0.62, 0.64, 0.66), 0.8, 0.35, 0.0),
        "AccentCoil": ((0.85, 0.42, 0.2), 0.8, 0.3, 0.0),                # copper
        "Trim": ((0.9, 0.66, 0.06), 0.0, 0.5, 0.0),
        "Eye": ((0.1, 0.85, 0.95), 0.0, 0.3, 1.6)}, "front"),
    "speaker": (2, 1, "Props/lure_speaker", {
        "Body": ((0.14, 0.15, 0.16), 0.2, 0.5, 0.0),
        "EyeLed": ((0.3, 1.0, 0.6), 0.0, 0.3, 2.0),
        "Accent": ((0.05, 0.05, 0.05), 0.1, 0.6, 0.0)}, "front"),
    # the shelter map, folded and tied (apartments.py)
    "map": (3, 3, "item_map_model", {
        "Body": ((0.86, 0.8, 0.64), 0.0, 0.8, 0.0),
        "Accent": ((0.45, 0.2, 0.12), 0.0, 0.7, 0.0),
        "Trim": ((0.55, 0.08, 0.06), 0.0, 0.35, 0.0)}, "top"),
    # more pickup items (items_more.py)
    "radio": (1, 2, "item_radio_model", {
        "Body": ((0.13, 0.14, 0.12), 0.0, 0.55, 0.0),                    # dark olive plastic
        "EyeLed": ((0.2, 1.0, 0.35), 0.0, 0.3, 2.0),
        "Accent": ((0.05, 0.05, 0.05), 0.0, 0.6, 0.0)}),                 # rubber antenna, knob, grille
    "keyboard": (3, 1, "item_keyboard_model", {
        "Body": ((0.16, 0.17, 0.19), 0.0, 0.5, 0.0),
        "EyeLed": ((0.3, 1.0, 0.4), 0.0, 0.3, 2.0),
        "Accent": ((0.85, 0.85, 0.82), 0.0, 0.45, 0.0)}, "tilted"),      # keycaps
    "tablet": (2, 2, "item_tablet_model", {
        "Body": ((0.92, 0.66, 0.12), 0.0, 0.75, 0.0),                    # rugged rubber case
        "EyeScreen": ((0.25, 0.75, 0.95), 0.0, 0.2, 1.0),
        "Accent": ((0.12, 0.12, 0.13), 0.0, 0.4, 0.0)}),                 # bezel
    "medkit": (2, 2, "item_medkit_model", {
        "Body": ((0.78, 0.1, 0.08), 0.0, 0.4, 0.0),
        "Trim": ((0.95, 0.95, 0.95), 0.0, 0.4, 0.0),                     # the cross
        "Accent": ((0.82, 0.83, 0.85), 0.6, 0.35, 0.0)}, "front"),       # latches, handle
    "coffee": (1, 1, "item_coffee_model", {
        "Body": ((0.25, 0.13, 0.06), 0.0, 0.15, 0.0),                    # brown glass
        "Trim": ((0.75, 0.12, 0.1), 0.0, 0.5, 0.0),                      # label
        "Accent": ((0.75, 0.6, 0.2), 0.8, 0.3, 0.0)}, "front"),          # gold lid
    "finder": (1, 2, "item_finder_model", {
        "Body": ((0.2, 0.3, 0.24), 0.0, 0.5, 0.0),
        "EyeLed": ((1.0, 0.55, 0.1), 0.0, 0.2, 2.5),                     # the lamp that blinks
        "Accent": ((0.7, 0.72, 0.75), 0.8, 0.3, 0.0)}),
    "boots": (2, 2, "item_boots_model", {
        "Body": ((0.36, 0.22, 0.12), 0.0, 0.6, 0.0),                     # leather
        "Trim": ((0.8, 0.65, 0.3), 0.0, 0.7, 0.0),                       # laces
        "Accent": ((0.08, 0.08, 0.08), 0.0, 0.8, 0.0)}, "front"),        # soles
    "tomatoes": (1, 1, "item_tomatoes_model", {
        "Body": ((0.82, 0.14, 0.08), 0.0, 0.5, 0.0),
        "Accent": ((0.7, 0.72, 0.74), 0.9, 0.3, 0.0)}, "front"),
    "chess": (2, 2, "item_chess_model", {
        "Body": ((0.32, 0.17, 0.08), 0.0, 0.4, 0.0),                     # dark wood
        "Accent": ((0.9, 0.85, 0.72), 0.0, 0.35, 0.0)}, "tilted"),       # squares, pieces, hinges
    "magazine": (2, 2, "item_magazine_model", {
        "Body": (PRINTED, 0.0, 0.25, 0.0),                               # glossy cover
        "Accent": ((0.92, 0.9, 0.86), 0.0, 0.7, 0.0)}, "tilted"),        # pages
    "pills": (1, 1, "item_pills_model", {
        "Body": ((0.3, 0.55, 0.9), 0.0, 0.5, 0.0),
        "Accent": ((0.78, 0.8, 0.82), 0.8, 0.25, 0.0)}),                 # blister foil
    "toothpaste": (1, 2, "item_toothpaste_model", {
        "Body": ((0.95, 0.95, 0.97), 0.0, 0.3, 0.0),
        "Accent": ((0.15, 0.4, 0.85), 0.0, 0.35, 0.0)}),                 # cap
    "tape": (1, 1, "item_tape_model", {
        "Body": ((0.5, 0.52, 0.54), 0.2, 0.5, 0.0),
        "Accent": ((0.62, 0.45, 0.27), 0.0, 0.8, 0.0)}, "front"),        # cardboard core
    "matches": (1, 1, "item_matches_model", {
        "Body": ((0.9, 0.7, 0.15), 0.0, 0.6, 0.0),
        "Trim": ((0.75, 0.1, 0.08), 0.0, 0.5, 0.0),                      # heads
        "Accent": ((0.85, 0.68, 0.42), 0.0, 0.7, 0.0)}),                 # drawer and sticks
    "toiletpaper": (1, 1, "item_toiletpaper_model", {
        "Body": ((0.95, 0.95, 0.93), 0.0, 0.9, 0.0),
        "Accent": ((0.6, 0.45, 0.28), 0.0, 0.85, 0.0)}, "front"),
    "applecore": (1, 1, "item_applecore_model", {
        "Body": ((0.95, 0.9, 0.72), 0.0, 0.6, 0.0),                      # flesh
        "Trim": ((0.3, 0.18, 0.08), 0.0, 0.7, 0.0),                      # stem, calyx, seeds
        "Accent": ((0.72, 0.08, 0.06), 0.0, 0.3, 0.0)}, "front"),        # skin
    "soap": (1, 1, "item_soap_model", {
        "Body": ((0.95, 0.8, 0.85), 0.0, 0.4, 0.0),
        "Accent": ((0.25, 0.55, 0.75), 0.0, 0.6, 0.0)}, "front"),        # wrapper band
    "gloves": (1, 2, "item_gloves_model", {
        "Body": ((0.85, 0.72, 0.42), 0.0, 0.85, 0.0),                    # canvas
        "Accent": ((0.2, 0.35, 0.65), 0.0, 0.8, 0.0)}),                  # knitted cuffs
    "necklace": (2, 1, "item_necklace_model", {
        "Body": ((1.0, 0.76, 0.3), 0.45, 0.3, 0.0),                      # gold (half metallic: reads bright here)
        "Accent": ((0.85, 0.05, 0.15), 0.0, 0.05, 0.2)}),                # rubies
    "mop": (1, 4, "item_mop_model", {
        "Body": ((0.2, 0.45, 0.75), 0.0, 0.4, 0.0),                      # handle and clamp
        "Accent": ((0.88, 0.86, 0.8), 0.0, 0.95, 0.0)}),                 # strands
    "plush": (2, 2, "item_plush_model", {
        "Body": ((0.6, 0.4, 0.22), 0.0, 0.95, 0.0),                      # fur
        "Trim": ((0.05, 0.04, 0.04), 0.0, 0.3, 0.0),                     # eyes, nose
        "Accent": ((0.9, 0.78, 0.6), 0.0, 0.9, 0.0)}, "front"),          # muzzle, ears, tummy, pads
    "socks": (1, 2, "item_socks_model", {
        "Body": ((0.95, 0.95, 0.95), 0.0, 0.9, 0.0),
        "Accent": ((0.15, 0.35, 0.75), 0.0, 0.85, 0.0)}),                # stripes
    "ssd": (1, 1, "item_ssd_model", {
        "Body": ((0.1, 0.1, 0.11), 0.6, 0.35, 0.0),                      # anodised case
        "Trim": ((0.85, 0.2, 0.15), 0.0, 0.4, 0.0),                      # label
        "Accent": ((0.78, 0.62, 0.2), 0.9, 0.3, 0.0)}, "tilted"),        # connector, screws
    "gamepad": (2, 1, "item_gamepad_model", {
        "Body": ((0.16, 0.17, 0.2), 0.0, 0.45, 0.0),
        "Trim": ((0.85, 0.25, 0.2), 0.0, 0.4, 0.0),                      # face buttons
        "Eye": ((0.3, 0.7, 1.0), 0.0, 0.3, 1.5),                         # light bar
        "Accent": ((0.06, 0.06, 0.07), 0.0, 0.5, 0.0)}, "tilted"),       # sticks, pad, triggers
    "gamekeyboard": (3, 1, "item_gamekeyboard_model", {
        "Body": ((0.07, 0.07, 0.08), 0.3, 0.45, 0.0),                    # case
        "Trim": ((0.92, 0.92, 0.94), 0.0, 0.45, 0.0),                    # pale keys
        "Eye": ((0.9, 0.25, 0.95), 0.0, 0.3, 1.5),                       # the glow under the keys
        "Accent": ((0.2, 0.21, 0.23), 0.0, 0.5, 0.0)}, "tilted"),        # dark keys
    "canister": (2, 2, "item_canister_model", {
        "Body": ((0.72, 0.1, 0.07), 0.3, 0.45, 0.0),                     # painted steel
        "Trim": ((0.95, 0.8, 0.15), 0.0, 0.5, 0.0),                      # hazard label
        "Accent": ((0.08, 0.08, 0.08), 0.0, 0.5, 0.0)}, "front"),        # cap, spout strap
    "bandage": (1, 1, "item_bandage_model", {
        "Body": ((0.93, 0.91, 0.85), 0.0, 0.95, 0.0),                    # gauze
        "Accent": ((0.78, 0.12, 0.1), 0.0, 0.6, 0.0)}, "front"),         # the stripes round the roll
}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, colour, metallic, roughness, emission):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    if isinstance(colour, str):
        tex = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(os.path.join(MODELS, "..", "Textures", "Covers", colour))
        m.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        colour = (1.0, 1.0, 1.0)
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
    if view in ("top", "tilted"):
        # a lying thing: its length (X in the file) along the picture's longer side, its head up / to the right
        holder.rotation_euler = Euler((0, 0, math.radians(90) if tall > wide else 0))
    bpy.context.view_layer.update()

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    # top: from above with a little tilt (the sides read); front: three-quarter from the front (-Y) and above
    tilt = math.radians({"top": 7, "tilted": 30}.get(view, 58))
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
    if ONLY is None or item in ONLY:
        render(item, *spec)
