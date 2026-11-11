"""Look at models the way an artist does: a contact sheet of fixed views plus a structural check of the mesh.

Run:  blender.exe -b --factory-startup --python Tools/Blender/review.py -- <sheet.png> <model.fbx> [<model2.fbx> ...]
      options (anywhere after --):  --tile 400  --no-check  --json <report.json>
                                    --focus x,y,z,r   adds a row of 4 close-ups around that point (model coordinates)
                                    --floor           the model stands on the floor (props): resting pieces are supported
                                    --lineup N        many models on one sheet instead: front + 3/4 each, N per row
                                                      (order in <sheet>_legend.txt)
                                    --shading         adds a row per model: clay matcap | normal-check matcap |
                                                      back faces culled on magenta (as Unity draws: magenta = hole) |
                                                      clay from below

Sheet layout — one block of 4 x 2 tiles per model, models stacked top to bottom in the order given (before / after):
  row 1:  front (ortho)  |  3/4 front-left  |  right side (ortho)  |  back (ortho)
  row 2:  top (ortho)    |  3/4 rear, high  |  wireframe 3/4       |  thumbnail (the 3/4 view at 64 px, scaled up:
                                                                       what still reads from across a room)
Parts are coloured by role (Body grey, Accent orange, Eye cyan, unknown names lilac); cavity shading darkens
creases and lights up ridges, so bevels, seams and panel lines show; outlines mark the silhouette.

The check (printed, and saved with --json) per model:
  * triangles per part and in total, bounding box size (m)
  * islands: loose pieces of mesh; touching / intersecting islands are grouped — every group after the first is a
    DETACHED piece floating in the air (3DCodeBench: the most common defect of generated models that render fine)
  * open edges (holes) and degenerate faces
  * GATE — scenario-labs bx_audit.deliverable(profile="game") (vendor/scenario): floating and hidden parts, z-fighting
    between parts, inside-out parts, faces pointing inward, unapplied scale, duplicate faces, no UVs. Errors must be
    fixed before a model goes into the game; warnings need a reason.
Use it from another script as well:  import review; review.sheet([[objs...], [objs...]], path); review.check(objs)
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "vendor", "scenario"))
import bx_audit  # noqa: E402  (scenario-labs, MIT)

ROLE_COLORS = {
    "Body": (0.60, 0.62, 0.66),
    "Accent": (0.92, 0.52, 0.16),
    "Eye": (0.15, 0.85, 1.00),
}
OTHER_COLOR = (0.70, 0.60, 0.90)
BACKGROUND = (0.16, 0.17, 0.19)
TOUCH = 0.004  # islands closer than 4 mm count as touching


def role_color(name):
    for role, color in ROLE_COLORS.items():
        if name.startswith(role):
            return color
    if name.startswith("AccentLeg"):
        return ROLE_COLORS["Accent"]
    return OTHER_COLOR


# --------------------------------------------------------------------------- scene

def clear():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.images):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def import_model(path):
    """Imports an FBX/GLB/OBJ; returns its mesh objects (game orientation: front faces -Y, z up)."""
    before = set(bpy.data.objects)
    ext = os.path.splitext(path)[1].lower()
    if ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        raise ValueError("unknown model format: " + path)
    return [o for o in bpy.data.objects if o not in before and o.type == "MESH"]


def bounds(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for corner in o.bound_box:
            p = o.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, p))
            hi = Vector(map(max, hi, p))
    return lo, hi


def setup_render(tile):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = scene.render.resolution_y = tile
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.display.render_aa = "8"
    scene.view_settings.view_transform = "Standard"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "OBJECT"
    sh.show_cavity = True
    sh.cavity_type = "BOTH"
    sh.cavity_ridge_factor = 1.4
    sh.cavity_valley_factor = 1.4
    sh.curvature_ridge_factor = 1.2
    sh.curvature_valley_factor = 1.2
    sh.show_object_outline = True
    sh.object_outline_color = (0.02, 0.02, 0.03)
    sh.show_shadows = False
    sh.show_specular_highlight = True
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.color = BACKGROUND
    cam = bpy.data.objects.get("ReviewCam") or bpy.data.objects.new("ReviewCam", bpy.data.cameras.new("ReviewCam"))
    if cam.name not in scene.collection.objects:
        scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def look_at(cam, eye, target):
    cam.location = eye
    direction = Vector(target) - Vector(eye)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


# view name -> (direction from the model centre towards the camera, ortho?)
VIEWS = [
    ("front", Vector((0, -1, 0)), True),
    ("q34", Vector((-0.75, -1, 0.45)), False),
    ("side", Vector((1, 0, 0)), True),
    ("back", Vector((0, 1, 0)), True),
    ("top", Vector((0, -0.0001, 1)), True),
    ("q34_rear", Vector((0.8, 1, 0.9)), False),
    ("wire", Vector((-0.75, -1, 0.45)), False),
    ("thumb", Vector((-0.75, -1, 0.45)), False),
]


def frame(cam, objs, direction, ortho, focus=None):
    lo, hi = bounds(objs)
    if focus is not None:  # close-up: a sphere (x, y, z, r) around the detail
        c, r = Vector(focus[:3]) + FOCUS_SHIFT, focus[3]
        lo, hi = c - Vector((r, r, r)), c + Vector((r, r, r))
    centre = (lo + hi) / 2
    size = hi - lo
    radius = max(size.length / 2, 0.05)
    d = direction.normalized()
    if ortho:
        cam.data.type = "ORTHO"
        # the extent seen from this side: the two axes across the view direction
        if abs(d.z) > 0.9:
            extent = max(size.x, size.y)
        elif abs(d.x) > 0.9:
            extent = max(size.y, size.z)
        else:
            extent = max(size.x, size.z)
        cam.data.ortho_scale = max(extent * 1.15, 0.1)
        dist = radius * 4 + 1
    else:
        cam.data.type = "PERSP"
        cam.data.lens = 50
        fov = 2 * math.atan(cam.data.sensor_width / (2 * cam.data.lens))
        dist = radius / math.sin(fov / 2) * 1.05
    cam.data.clip_start = 0.01
    cam.data.clip_end = dist + radius * 4 + 10
    look_at(cam, centre + d * dist, centre)


def colour(objs, wire):
    """Role colours on the solid models; in the wire view the solid goes pale and a dark wireframe copy is shown."""
    extras = []
    lo, hi = bounds(objs)
    thickness = max(0.002, (hi - lo).length * 0.0022)  # ~1.5 px on a 400 px tile
    for o in objs:
        o.color = (0.85, 0.86, 0.88, 1) if wire else (*role_color(o.name), 1)
        if wire:
            copy = o.copy()
            copy.data = o.data
            copy.modifiers.clear()
            copy.name = "_wire_" + o.name
            bpy.context.scene.collection.objects.link(copy)
            mod = copy.modifiers.new("wire", "WIREFRAME")
            mod.thickness = thickness
            mod.use_relative_offset = False
            mod.use_even_offset = True
            mod.offset = 0.0
            copy.color = (0.05, 0.06, 0.08, 1)
            extras.append(copy)
    return extras


def render_tile(path, tile):
    bpy.context.scene.render.resolution_x = bpy.context.scene.render.resolution_y = tile
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def load_pixels(path, tile):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    if w != tile:  # nearest-neighbour upscale (thumbnail)
        idx = (np.arange(tile) * w // tile).astype(int)
        px = px[idx][:, idx]
    return px


SHADING = [  # (name, direction, ortho, studio light / mode)
    ("clay", Vector((-0.75, -1, 0.45)), False, "clay_studio.exr"),
    ("normals", Vector((-0.75, -1, 0.45)), False, "check_normal+y.exr"),
    ("culled", Vector((0.75, -1, 0.35)), False, "culling"),
    ("low", Vector((0.8, -1, -0.45)), False, "clay_studio.exr"),
]


def shading_mode(mode):
    """Workbench set-up of one shading tile; None restores the role-colour look."""
    scene = bpy.context.scene
    sh = scene.display.shading
    if mode is None:
        setup_render(scene.render.resolution_x)
        sh.show_backface_culling = False
        return
    sh.show_object_outline = False
    sh.show_cavity = False
    if mode == "culling":
        sh.light, sh.color_type = "STUDIO", "SINGLE"
        sh.single_color = (0.8, 0.8, 0.8)
        sh.show_backface_culling = True
        scene.world.color = (1.0, 0.0, 1.0)  # a face pointing inward shows as a magenta hole
    else:
        sh.light, sh.color_type = "MATCAP", "SINGLE"
        sh.studio_light = mode
        sh.single_color = (0.8, 0.8, 0.8)
        sh.show_backface_culling = False
        scene.world.color = (0.18, 0.18, 0.18)


FOCUS_SHIFT = Vector((0, 0, 0))
CLOSE_UPS = [("front", Vector((0, -1, 0)), False), ("q34", Vector((-0.75, -1, 0.45)), False),
             ("side", Vector((1, 0, 0)), False), ("q34_rear", Vector((0.8, 1, 0.9)), False)]


def sheet(groups, path, tile=400, tmp=None, focus=None, offsets=None, shading=False):
    """groups: list of object lists (one per model, all already in the scene). Writes the contact sheet PNG.
    focus (x, y, z, r) adds a row of close-ups around that point of every model (offsets: where each model was moved)."""
    global FOCUS_SHIFT
    tmp = tmp or os.path.join(os.path.dirname(os.path.abspath(path)), "_review_tiles")
    os.makedirs(tmp, exist_ok=True)
    cam = setup_render(tile)
    all_objs = [o for g in groups for o in g]
    rows = []
    for gi, objs in enumerate(groups):
        for o in all_objs:
            o.hide_render = o not in objs
        tiles = []
        for name, direction, ortho in VIEWS:
            extras = colour(objs, name == "wire")
            frame(cam, objs, direction, ortho)
            size = 64 if name == "thumb" else tile
            p = os.path.join(tmp, f"m{gi}_{name}.png")
            render_tile(p, size)
            for e in extras:
                bpy.data.objects.remove(e, do_unlink=True)
            tiles.append(load_pixels(p, tile))
        rows.append(np.concatenate(tiles[:4], axis=1))
        rows.append(np.concatenate(tiles[4:], axis=1))
        if shading:
            shaded = []
            for name, direction, ortho, mode in SHADING:
                shading_mode(mode)
                frame(cam, objs, direction, ortho)
                p = os.path.join(tmp, f"m{gi}_shade_{name}.png")
                render_tile(p, tile)
                shaded.append(load_pixels(p, tile))
            shading_mode(None)
            rows.append(np.concatenate(shaded, axis=1))
        if focus is not None:
            FOCUS_SHIFT = Vector(offsets[gi]) if offsets else Vector((0, 0, 0))
            close = []
            for name, direction, ortho in CLOSE_UPS:
                colour(objs, False)
                frame(cam, objs, direction, ortho, focus)
                p = os.path.join(tmp, f"m{gi}_close_{name}.png")
                render_tile(p, tile)
                close.append(load_pixels(p, tile))
            rows.append(np.concatenate(close, axis=1))
        sep = np.ones((6, tile * 4, 4), dtype=np.float32)
        sep[..., :3] = 0.9
        rows.append(sep)
    for o in all_objs:
        o.hide_render = False
    # Blender pixels start at the bottom row: stack the rows in reverse so the first one ends up on top
    full = np.ascontiguousarray(np.concatenate(rows[:-1][::-1], axis=0))
    h, w = full.shape[:2]
    out = bpy.data.images.new("review_sheet", w, h, alpha=True)
    out.pixels.foreach_set(full.ravel())
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    print("SHEET", path, f"{w}x{h}", "models:", len(groups))
    return path


def lineup(groups, path, cols=4, tile=256, names=None, tmp=None):
    """Many models on one sheet to compare them: each model gets a front and a 3/4 tile (role colours), models in
    rows of `cols`, in the order given. The order is printed and written next to the sheet (<sheet>_legend.txt)."""
    tmp = tmp or os.path.join(os.path.dirname(os.path.abspath(path)), "_review_tiles")
    os.makedirs(tmp, exist_ok=True)
    cam = setup_render(tile)
    all_objs = [o for g in groups for o in g]
    views = [VIEWS[0], VIEWS[1]]
    cells = []
    for gi, objs in enumerate(groups):
        for o in all_objs:
            o.hide_render = o not in objs
        pair = []
        for name, direction, ortho in views:
            colour(objs, False)
            frame(cam, objs, direction, ortho)
            p = os.path.join(tmp, f"lineup_{gi}_{name}.png")
            render_tile(p, tile)
            pair.append(load_pixels(p, tile))
        cell = np.concatenate(pair, axis=1)
        cell[:, :2, :3] = 0.9  # a thin light line between models
        cells.append(cell)
    for o in all_objs:
        o.hide_render = False
    blank = np.zeros_like(cells[0])
    blank[..., 3] = 1.0
    blank[..., :3] = BACKGROUND
    rows = []
    for r in range(0, len(cells), cols):
        row = cells[r:r + cols] + [blank] * (cols - len(cells[r:r + cols]))
        rows.append(np.concatenate(row, axis=1))
        sep = np.ones((3, rows[-1].shape[1], 4), dtype=np.float32)
        sep[..., :3] = 0.9
        rows.append(sep)
    full = np.ascontiguousarray(np.concatenate(rows[:-1][::-1], axis=0))
    h, w = full.shape[:2]
    out = bpy.data.images.new("lineup_sheet", w, h, alpha=True)
    out.pixels.foreach_set(full.ravel())
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    names = names or [str(k) for k in range(len(groups))]
    legend = [f"row {k // cols + 1}, col {k % cols + 1}: {n}" for k, n in enumerate(names)]
    with open(os.path.splitext(path)[0] + "_legend.txt", "w", encoding="utf-8") as f:
        f.write("\n".join(legend))
    print("LINEUP", path, f"{w}x{h}", "models:", len(groups))
    for line in legend:
        print("  ", line)
    return path


# --------------------------------------------------------------------------- structural check

def _islands(obj):
    """Loose pieces of one object: list of (verts world, tris) for BVH building."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.verts.ensure_lookup_table()
    seen = set()
    result = []
    for start in bm.verts:
        if start.index in seen:
            continue
        stack = [start]
        comp = []
        seen.add(start.index)
        while stack:
            v = stack.pop()
            comp.append(v)
            for e in v.link_edges:
                w = e.other_vert(v)
                if w.index not in seen:
                    seen.add(w.index)
                    stack.append(w)
        ids = {v.index for v in comp}
        local = {i: k for k, i in enumerate(sorted(ids))}
        verts = [bm.verts[i].co.copy() for i in sorted(ids)]
        faces = set()
        for v in comp:
            for f in v.link_faces:
                faces.add(f)
        tris = [[local[v.index] for v in f.verts] for f in faces]
        result.append((verts, tris))
    open_edges = sum(1 for e in bm.edges if e.is_boundary)
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-9)
    bm.free()
    return result, open_edges, degenerate


def check(objs, name="model", verbose=True, floor=False):
    """floor: the model stands on the floor (furniture, props): pieces resting on it count as supported, so a chair
    beside a desk is not 'detached'. Leave it off for robots and characters — a wheel touching the floor but not the
    body is a defect there."""
    lo, hi = bounds(objs)
    parts = {}
    pieces = []  # (part name, verts, bvh)
    total_tris = 0
    for o in objs:
        islands, open_edges, degenerate = _islands(o)
        tris = sum(len(t) for _, t in islands)
        total_tris += tris
        parts[o.name] = {"tris": tris, "islands": len(islands), "open_edges": open_edges, "degenerate": degenerate}
        for verts, t in islands:
            if t:
                pieces.append((o.name, verts, BVHTree.FromPolygons(verts, t)))
    # union-find over touching pieces; with floor, node len(pieces) is the floor
    parent = list(range(len(pieces) + (1 if floor else 0)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    for i in range(len(pieces)):
        for j in range(i + 1, len(pieces)):
            if find(i) == find(j):
                continue
            a, b = pieces[i], pieces[j]
            touching = bool(a[2].overlap(b[2]))
            if not touching:  # both ways: the corners of a big slab can be far from a small part lying on it
                for src, dst in ((a, b), (b, a)):
                    step = max(1, len(src[1]) // 400)
                    if any(dst[2].find_nearest(v, TOUCH)[0] is not None for v in src[1][::step]):
                        touching = True
                        break
            if touching:
                parent[find(i)] = find(j)
    ground = len(pieces)
    if floor:
        for i, piece in enumerate(pieces):
            if min(v.z for v in piece[1]) <= lo.z + TOUCH:
                parent[find(i)] = find(ground)
    groups = {}
    for i in range(len(pieces)):
        groups.setdefault(find(i), []).append(i)
    if floor:
        main = groups.get(find(ground), [])
        ordered = [main] + [g for g in groups.values() if g is not main]
    else:
        ordered = sorted(groups.values(), key=lambda g: -sum(len(pieces[i][1]) for i in g))
    detached = []
    for g in ordered[1:]:
        pts = [v for i in g for v in pieces[i][1]]
        glo = Vector(map(min, *pts)) if len(pts) > 1 else pts[0]
        ghi = Vector(map(max, *pts)) if len(pts) > 1 else pts[0]
        detached.append({
            "parts": sorted({pieces[i][0] for i in g}),
            "centre": [round(c, 3) for c in (glo + ghi) / 2],
            "size": [round(c, 3) for c in ghi - glo],
        })
    report = {
        "model": name,
        "size_m": [round(c, 3) for c in hi - lo],
        "min_z": round(lo.z, 3),
        "tris": total_tris,
        "parts": parts,
        "pieces": len(pieces),
        "detached": detached,
    }
    gate = bx_audit.deliverable(objs, profile="game")
    if floor:  # the gate knows no floor: its floating verdicts are replaced by DETACHED above
        gate["errors"] = [e for e in gate["errors"] if "floating" not in e and "fragment" not in e]
        gate["warnings"] = [w for w in gate["warnings"] if "not touching" not in w and "detached fragment" not in w]
        gate["pass"] = not gate["errors"]
    report["gate"] = {k: gate[k] for k in ("pass", "errors", "warnings")}
    if not verbose:
        return report
    print(f"CHECK {name}: size {report['size_m']} m, floor z {report['min_z']}, {total_tris} tris, "
          f"{len(pieces)} pieces, {len(detached)} detached")
    for pname, p in parts.items():
        flags = []
        if p["open_edges"]:
            flags.append(f"{p['open_edges']} open edges")
        if p["degenerate"]:
            flags.append(f"{p['degenerate']} degenerate faces")
        print(f"   {pname:<16} {p['tris']:>6} tris  {p['islands']:>3} islands  {'  '.join(flags)}")
    for d in detached:
        print(f"   DETACHED {d['parts']} at {d['centre']} size {d['size']}")
    print(f"   GATE {'PASS' if gate['pass'] else 'FAIL'}: {len(gate['errors'])} errors, {len(gate['warnings'])} warnings")
    for e in gate["errors"]:
        print("   GATE ERROR", e)
    for w in gate["warnings"][:12]:
        print("   GATE WARN ", w)
    return report


# --------------------------------------------------------------------------- command line

def main():
    if "--" not in sys.argv:
        return
    args = sys.argv[sys.argv.index("--") + 1:]
    tile, do_check, json_path, focus, shading, cols, floor = 400, True, None, None, False, 0, False
    rest = []
    i = 0
    while i < len(args):
        if args[i] == "--tile":
            tile = int(args[i + 1])
            i += 2
        elif args[i] == "--focus":
            focus = [float(c) for c in args[i + 1].split(",")]
            i += 2
        elif args[i] == "--lineup":
            cols = int(args[i + 1])
            i += 2
        elif args[i] == "--shading":
            shading = True
            i += 1
        elif args[i] == "--floor":
            floor = True
            i += 1
        elif args[i] == "--no-check":
            do_check = False
            i += 1
        elif args[i] == "--json":
            json_path = args[i + 1]
            i += 2
        else:
            rest.append(args[i])
            i += 1
    out, models = rest[0], rest[1:]
    clear()
    groups, reports, offsets = [], [], []
    for k, path in enumerate(models):
        objs = import_model(path)
        # keep each model at its own spot so the views never see a neighbour
        for o in objs:
            if o.parent is None:
                o.location.x += k * 50
        bpy.context.view_layer.update()
        offsets.append((k * 50, 0, 0))
        groups.append(objs)
        if do_check:
            reports.append(check(objs, os.path.basename(path), floor=floor))
    if cols:
        lineup(groups, out, cols, tile if tile != 400 else 256, [os.path.basename(m) for m in models])
    else:
        sheet(groups, out, tile, focus=focus, offsets=offsets, shading=shading)
    if json_path:
        with open(json_path, "w", encoding="utf-8") as f:
            json.dump(reports, f, indent=1)


if __name__ == "__main__":
    main()
