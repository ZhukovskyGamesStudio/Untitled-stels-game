"""Contact sheets of the figure while it is being built (Workbench, a few seconds): the same fixed views every time,
so two builds can be compared side by side. `sheet(path, objects, shots)` writes one PNG of the shots in a row."""
import math
import os

import bpy
import numpy as np
from mathutils import Vector

# name, looking from (direction to the camera), target, the height of the view in metres
FULL = [("front", (0, -1, 0), (0, 0, 0.9), 1.95), ("3/4", (0.7, -0.7, 0.12), (0, 0, 0.9), 1.95),
        ("side", (1, 0, 0), (0, 0, 0.9), 1.95), ("back", (0, 1, 0), (0, 0, 0.9), 1.95)]
FACE = [("face", (0, -1, 0), (0, -0.02, 1.655), 0.34), ("face 3/4", (0.65, -0.75, 0.05), (0, -0.02, 1.655), 0.34),
        ("profile", (1, 0, 0), (0, -0.02, 1.655), 0.34)]
BUST = [("bust", (0.25, -1, 0.1), (0, 0, 1.45), 0.75), ("bust back", (-0.5, 1, 0.15), (0, 0, 1.45), 0.75)]
HANDS = [("hand", (0.2, -1, 0.3), (0.55, -0.01, 0.93), 0.3), ("palm", (-1, -0.1, -0.5), (0.55, -0.01, 0.93), 0.3),
         ("shoes", (0.6, -1, 0.35), (0.0, -0.03, 0.1), 0.45)]


def _camera(scene):
    cam = scene.objects.get("_sheet_cam")
    if cam is None:
        cam = bpy.data.objects.new("_sheet_cam", bpy.data.cameras.new("_sheet_cam"))
        scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    scene.camera = cam
    return cam


def sheet(path, objs=None, shots=FULL, width=420, height=None, color="OBJECT", matcap=None, textured=False, engine="BLENDER_WORKBENCH"):
    """Renders the shots and writes them side by side. color: OBJECT / MATERIAL / TEXTURE / SINGLE (Workbench)."""
    s = bpy.context.scene
    cam = _camera(s)
    s.render.engine = engine
    if engine == "BLENDER_WORKBENCH":
        sh = s.display.shading
        sh.light = "MATCAP" if matcap else "STUDIO"
        if matcap:
            sh.studio_light = matcap
        sh.color_type = color
        sh.show_cavity = True
        sh.cavity_type = "BOTH"
        sh.curvature_ridge_factor = 0.6
        sh.curvature_valley_factor = 0.8
        sh.show_shadows = False
        s.display.render_aa = "8"
    s.render.film_transparent = False
    s.view_settings.view_transform = "Standard"
    if s.world is None:
        s.world = bpy.data.worlds.new("w")
    s.world.color = (0.32, 0.33, 0.35)
    hidden = []
    if objs is not None:
        keep = set(objs)
        for o in s.objects:
            if o.type == "MESH" and o not in keep and not o.hide_render:
                o.hide_render = True
                hidden.append(o)
    tiles = []
    for name, frm, target, span in shots:
        h = height or int(width * (2.0 if span > 1.2 else 1.25))
        s.render.resolution_x, s.render.resolution_y = width, h
        d = Vector(frm).normalized()
        cam.location = Vector(target) + d * 6.0
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        cam.data.ortho_scale = span * (width / h if width > h else 1.0) if width > h else span
        cam.data.sensor_fit = "VERTICAL"
        cam.data.clip_end = 30
        tmp = os.path.join(os.path.dirname(path), f"_tile_{os.getpid()}_{len(tiles)}.png")   # (several builds may share the folder)
        s.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(tmp)
        px = np.array(img.pixels[:], np.float32).reshape(h, width, 4)
        bpy.data.images.remove(img)
        os.remove(tmp)
        tiles.append(px)
    for o in hidden:
        o.hide_render = False
    hmax = max(t.shape[0] for t in tiles)
    row = np.concatenate([np.pad(t, ((0, hmax - t.shape[0]), (0, 0), (0, 0)), constant_values=0.2) for t in tiles], 1)
    out = bpy.data.images.new("_sheet", row.shape[1], row.shape[0], alpha=False)
    out.pixels.foreach_set(row.ravel())
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    return path
