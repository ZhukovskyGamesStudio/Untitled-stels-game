"""A model's own baked maps: what its shape is like, pixel by pixel, for the renderer to shade it by.

Our furniture is plain parts under tiling materials: every edge is a knife's, nothing knows where two parts meet, a
steel side is a mathematical plane. A scanned prop looks rich in the same renderer because its textures were made for
that one object. This gives a generated model the same thing without an artist's hand on every one: a second UV set,
unique (every face its own place in one square), and two pictures baked over it with Cycles from a DETAILED VERSION of
the model that exists only for the bake —

  <name>_n.png   normal, tangent space of the second UV set (OpenGL, as Unity reads it): every sharp edge rounded
                 (a bevel of RADIUS that is not in the mesh), sheet metal a little uneven (waves), and the details the
                 model's script designed for it: pressed ribs, rivets, beads, plates (detail())
  <name>_m.png   R occlusion   1 open … 0 shut in (by the model itself, its details and the floor it stands on)
                 G edge        1 flat … 0 a sharp outer edge: where paint wears through
                 B cavity      1 open … 0 a tight inner corner: where grime gathers

The detailed version: a copy of every part, cut fine and pushed about by a low wave where the part is sheet (waves=),
plus the detail meshes. The game mesh only says where the pixels are.

The first UV set stays as it was (metres: the tiling materials go on tiling); the FBX carries both. Faces nobody can
see while everything is shut (the inside of a carcass, of a drawer) get a third of the texels' size.

A model's script names its details before it exports:   shapebake.detail("filing_cabinet", [bmesh, …], waves=0.0005)
shapes.export_fbx bakes the models named in PILOT (or all with SHAPEBAKE=all, none with SHAPEBAKE=0); the pictures go
to <models root>/../Textures/Shapes. Size: SHAPEBAKE_SIZE, or by the model's seen area (TEXELS per metre, 512..2048).
"""
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

PILOT = {"filing_cabinet", "cabinet", "credenza", "copier", "info_kiosk", "programmer_console"}
LAYER = "Shape"
RADIUS = 0.0045      # the bevel every sharp edge is given in the normal map, metres
WAVES = 0.00025      # how uneven a model's big faces are when its script says nothing (nothing made is a mathematical plane), metres
EDGE_REACH = 0.006   # how near an outer edge counts as edge, metres (under the thinnest sheet: the rays look inside the part)
CAVITY_REACH = 0.05  # how tight a corner counts as cavity, metres
AO_REACH = 0.6       # how far the occlusion looks, metres
TEXELS = 800         # texels per metre wanted on what is seen
HIDDEN = 0.3         # the texel size of what is never seen while all is shut, of the seen
SAMPLES = 32
CAGE = 0.004         # how far off the game mesh the detailed version is looked for, metres (details stand out less)
WAVE = 0.35          # metres across a wave of sheet metal

_details = {}        # model name -> (list of bmeshes, waves amplitude in metres, names of the parts that wave)


def detail(name, bms=(), waves=0.0, wavy=("Body",)):
    """The model's details for its bake: meshes that exist only in the detailed version (standing at most a few
    millimetres proud of the model), and how uneven its sheet parts are (the amplitude; the parts whose names begin so)."""
    _details[name] = (list(bms), waves, tuple(wavy))


def wanted(path):
    name = os.path.splitext(os.path.basename(path))[0]
    env = os.environ.get("SHAPEBAKE", "")
    if env == "all":                                     # every piece of furniture: the folders Props, and a style's own models but
        folder = os.path.abspath(path).replace("\\", "/")   # its robots, devices and pieces of the building
        if "/PerStyle/" in folder:
            return not name.startswith(("robot_", "sensor_", "attach_", "security_camera", "laser_", "worker_lift", "hatch", "exit_sign", "ceiling_lamp"))
        return os.path.basename(os.path.dirname(folder)) == "Props"
    return env != "0" and (name in PILOT or name in env.split(","))


def _tree(objs, floor):
    verts, tris = [], []
    for o in objs:
        base = len(verts)
        me = o.data
        me.calc_loop_triangles()
        verts += [o.matrix_world @ v.co for v in me.vertices]
        tris += [tuple(base + i for i in t.vertices) for t in me.loop_triangles]
    if floor:
        base = len(verts)
        r = 8.0
        verts += [Vector((-r, -r, 0)), Vector((r, -r, 0)), Vector((r, r, 0)), Vector((-r, r, 0))]
        tris += [(base, base + 1, base + 2), (base, base + 2, base + 3)]
    return BVHTree.FromPolygons(verts, tris)


def _hidden(objs, floor):
    """Per object, the faces from which nothing gets out: every ray over them is stopped within two metres."""
    tree = _tree(objs, floor)
    rnd = random.Random(5)
    dirs = []
    for i in range(14):
        u, phi = (i + 0.5) / 14, rnd.random() * 2 * math.pi
        r = math.sqrt(u)
        dirs.append((r * math.cos(phi), r * math.sin(phi), math.sqrt(1 - u)))
    out = []
    for o in objs:
        nm = o.matrix_world.to_3x3().inverted_safe().transposed()
        flags = []
        for p in o.data.polygons:
            n = (nm @ p.normal).normalized()
            t = n.orthogonal().normalized()
            b = n.cross(t)
            c = o.matrix_world @ p.center + n * 0.003
            flags.append(all(tree.ray_cast(c, t * d[0] + b * d[1] + n * d[2], 2.0)[0] is not None for d in dirs))
        out.append(flags)
    return out


def unwrap(objs, size, floor=True):
    """The second UV set of all the objects together: islands by angle, one texel density (hidden faces a third of
    it), packed into one square. Returns the seen area, metres²."""
    margin = 6.0 / size
    for o in objs:
        me = o.data
        if LAYER in me.uv_layers:
            me.uv_layers.remove(me.uv_layers[LAYER])
        while len(me.uv_layers) > 1:                     # a spare set a part came with would push the baked one to the third place
            me.uv_layers.remove(me.uv_layers[len(me.uv_layers) - 1])
        if len(me.uv_layers) == 0:
            me.uv_layers.new(name="UVMap")
        layer = me.uv_layers.new(name=LAYER)
        me.uv_layers.active = layer
        layer.active_render = True

    def edit(fn):
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        fn()
        bpy.ops.object.mode_set(mode="OBJECT")

    def project():
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=margin, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
        bpy.ops.uv.select_all(action="SELECT")
        bpy.ops.uv.average_islands_scale()
    edit(project)

    seen = 0.0
    for o, flags in zip(objs, _hidden(objs, floor)):
        uv = o.data.uv_layers[LAYER].data
        for p, hidden in zip(o.data.polygons, flags):
            if not hidden:
                seen += p.area
                continue
            loops = list(p.loop_indices)
            c = sum((uv[i].uv.copy() for i in loops), Vector((0, 0))) / len(loops)
            for i in loops:                                        # each hidden face shrinks where it lies: an island of its own
                uv[i].uv = c + (uv[i].uv - c) * HIDDEN

    def pack():
        bpy.ops.uv.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(rotate=True, margin_method="FRACTION", margin=margin, scale=True)
    edit(pack)
    # whatever the packer did, everything inside the square with its margin
    uvs = []
    for o in objs:
        co = np.empty(len(o.data.uv_layers[LAYER].data) * 2)
        o.data.uv_layers[LAYER].data.foreach_get("uv", co)
        uvs.append(co.reshape(-1, 2))
    allc = np.concatenate(uvs)
    lo, hi = allc.min(0), allc.max(0)
    if lo.min() < margin * 0.5 - 1e-6 or hi.max() > 1 - margin * 0.5 + 1e-6:
        k = (1 - 2 * margin) / max((hi - lo).max(), 1e-9)
        for o, co in zip(objs, uvs):
            o.data.uv_layers[LAYER].data.foreach_set("uv", ((co - lo) * k + margin).ravel())
    return seen


def _image(name, size, colour):
    img = bpy.data.images.new(name, size, size, alpha=False, float_buffer=True)
    img.generated_color = colour
    img.colorspace_settings.name = "Non-Color"
    return img


def _material(kind):
    """The surface each pass is baked from: a bevelled normal, or an occlusion node read as light."""
    mat = bpy.data.materials.new("_shape_" + kind)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    if kind == "normal":
        bsdf = nt.nodes.new("ShaderNodeBsdfDiffuse")
        bevel = nt.nodes.new("ShaderNodeBevel")
        bevel.samples = 12
        bevel.inputs["Radius"].default_value = RADIUS
        nt.links.new(bevel.outputs["Normal"], bsdf.inputs["Normal"])
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    elif kind == "ao":
        bsdf = nt.nodes.new("ShaderNodeBsdfDiffuse")
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    else:
        ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
        ao.samples = 24
        ao.inside = kind == "edge"
        ao.only_local = False
        ao.inputs["Distance"].default_value = EDGE_REACH if kind == "edge" else CAVITY_REACH
        emit = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(ao.outputs["AO"], emit.inputs["Color"])
        nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def _detailed(objs, name):
    """The detailed version: every part copied (sheet parts cut fine and waved), and the model's detail meshes."""
    bms, waves, wavy = _details.get(name, ([], WAVES, ("Body",)))
    made = []
    for o in objs:
        me = o.data.copy()
        hi = bpy.data.objects.new("_hi_" + o.name, me)
        hi.matrix_world = o.matrix_world.copy()
        bpy.context.scene.collection.objects.link(hi)
        if waves > 0 and o.name.startswith(wavy):
            bm = bmesh.new()
            bm.from_mesh(me)
            for _ in range(5):
                long_edges = [e for e in bm.edges if e.calc_length() > 0.035]
                if not long_edges:
                    break
                bmesh.ops.subdivide_edges(bm, edges=long_edges, cuts=1, use_grid_fill=True)
            bm.normal_update()
            seed = Vector((sum(map(ord, name)) % 97 * 0.31, 3.7, 1.9))    # (the model's own waves, the same on every run)
            for v in bm.verts:
                # big faces bulge and hollow a little, as pressed sheet does: nothing at an edge moves (a seam would open)
                if any(not e.smooth or e.calc_face_angle(0.0) > math.radians(25) for e in v.link_edges):
                    continue
                p = o.matrix_world @ v.co
                v.co += v.normal * waves * (noise.noise(p / WAVE + seed) + 0.5 * noise.noise(p / (WAVE * 0.37) + seed * 2.0))
            bm.to_mesh(me)
            bm.free()
        made.append(hi)
    if bms:
        joined = bmesh.new()
        for b in bms:
            tmp = bpy.data.meshes.new("_d")
            b.to_mesh(tmp)
            joined.from_mesh(tmp)
            bpy.data.meshes.remove(tmp)
            b.free()
        bmesh.ops.recalc_face_normals(joined, faces=joined.faces)
        me = bpy.data.meshes.new("_hi_detail")
        joined.to_mesh(me)
        joined.free()
        for p in me.polygons:
            p.use_smooth = True
        hi = bpy.data.objects.new("_hi_detail", me)
        bpy.context.scene.collection.objects.link(hi)
        made.append(hi)
    _details.pop(name, None)
    return made


def bake(objs, name, out_dir, size=None, floor=True):
    """Unwraps and bakes (see the module's text). Returns the paths of the two pictures."""
    objs = list(objs)
    probe = 1024
    seen = unwrap(objs, probe, floor)
    if size is None:
        env = os.environ.get("SHAPEBAKE_SIZE")
        hidden = sum(p.area for o in objs for p in o.data.polygons) - seen
        side = math.sqrt((seen + hidden * HIDDEN * HIDDEN) / 0.6) * TEXELS    # (the packing fills about six tenths of the square)
        size = int(env) if env else min(2048, max(512, 1 << int(math.ceil(math.log2(max(side, 1.0))))))
    if size != probe:
        unwrap(objs, size, floor)

    s = bpy.context.scene
    was_engine = s.render.engine
    s.render.engine = "CYCLES"
    s.cycles.device = "CPU"
    s.cycles.use_denoising = False
    if s.world is None:
        s.world = bpy.data.worlds.new("_shape_world")
    s.world.light_settings.distance = AO_REACH
    bk = s.render.bake
    bk.use_selected_to_active = True
    bk.use_cage = False
    bk.cage_extrusion = CAGE
    bk.max_ray_distance = CAGE * 2
    bk.margin = 4
    bk.margin_type = "EXTEND"
    bk.use_clear = False
    bk.normal_space = "TANGENT"

    his = _detailed(objs, name)
    ground = None
    if floor:
        me = bpy.data.meshes.new("_shape_floor")
        r = 6.0
        me.from_pydata([(-r, -r, 0), (r, -r, 0), (r, r, 0), (-r, r, 0)], [], [(0, 1, 2, 3)])
        ground = bpy.data.objects.new("_shape_floor", me)
        s.collection.objects.link(ground)

    images = {"normal": _image(name + "_bn", size, (0.5, 0.5, 1, 1)), "ao": _image(name + "_bao", size, (1, 1, 1, 1)),
              "edge": _image(name + "_bedge", size, (1, 1, 1, 1)), "cavity": _image(name + "_bcav", size, (1, 1, 1, 1))}
    target = bpy.data.materials.new("_shape_target")
    target.use_nodes = True
    node = target.node_tree.nodes.new("ShaderNodeTexImage")
    target.node_tree.nodes.active = node
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(target)
        # the game mesh only says where the pixels are: it neither shades nor blocks anything of the detailed version
        o.visible_diffuse = o.visible_glossy = o.visible_shadow = o.visible_transmission = o.visible_volume_scatter = False
    made = [target]
    for kind, bake_type, samples in (("normal", "NORMAL", 1), ("ao", "AO", SAMPLES), ("edge", "EMIT", 4), ("cavity", "EMIT", 4)):
        mat = _material(kind)
        made.append(mat)
        node.image = images[kind]
        s.cycles.samples = samples
        # the outer edges are the parts' own: a pressed panel a millimetre thick is all "edge" to rays looking inside it
        # (its whole face came out worn to the metal)
        sources = [h for h in his if not (kind == "edge" and h.name == "_hi_detail")]
        for h in his:
            h.data.materials.clear()
            h.data.materials.append(mat)
            h.hide_render = h not in sources
        for o in objs:
            for other in objs:
                other.hide_render = other is not o               # the other game parts neither catch nor block rays
            bpy.ops.object.select_all(action="DESELECT")
            for h in sources:
                h.select_set(True)
            o.select_set(True)
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.bake(type=bake_type)

    def px(img):
        a = np.empty(size * size * 4, np.float32)
        img.pixels.foreach_get(a)
        return a.reshape(size, size, 4)

    nrm = px(images["normal"])
    mask = np.ones((size, size, 4), np.float32)
    mask[..., 0] = np.clip(px(images["ao"])[..., 0], 0, 1)
    mask[..., 1] = np.clip(px(images["edge"])[..., 0], 0, 1)
    mask[..., 2] = np.clip(px(images["cavity"])[..., 0], 0, 1)
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    for suffix, data in (("n", nrm), ("m", mask)):
        img = bpy.data.images.new(f"{name}_{suffix}_out", size, size, alpha=False)
        img.colorspace_settings.name = "Non-Color"
        data = data.copy()
        data[..., 3] = 1
        img.pixels.foreach_set(data.ravel())
        img.filepath_raw = os.path.join(out_dir, f"{name}_{suffix}.png")
        img.file_format = "PNG"
        img.save()
        paths.append(img.filepath_raw)
        bpy.data.images.remove(img)

    for img in images.values():
        bpy.data.images.remove(img)
    for o in objs:
        o.hide_render = False
        o.visible_diffuse = o.visible_glossy = o.visible_shadow = o.visible_transmission = o.visible_volume_scatter = True
        o.data.materials.clear()
        o.data.uv_layers.active = o.data.uv_layers[0]            # (the first set stays the one materials tile by)
        o.data.uv_layers[0].active_render = True
    for h in his:
        me = h.data
        bpy.data.objects.remove(h, do_unlink=True)
        bpy.data.meshes.remove(me)
    for mat in made:
        bpy.data.materials.remove(mat)
    if ground is not None:
        bpy.data.objects.remove(ground, do_unlink=True)
    s.render.engine = was_engine
    print(f"SHAPEBAKE {name}: {size} px, {seen:.2f} m2 seen -> {paths[0]}")
    return paths


def for_export(path, objs):
    """shapes.export_fbx's hook: the pictures of a model about to be written, beside the models' root."""
    name = os.path.splitext(os.path.basename(path))[0]
    if not wanted(path):
        _details.pop(name, None)
        return None
    full = os.path.abspath(path).replace("\\", "/")
    if "/Art/Models/" in full:                           # in the project, whatever folder of the models: one folder of pictures
        out = full[:full.index("/Art/Models/")] + "/Art/Textures/Shapes"
    else:                                                # elsewhere (a trial): beside the models' root, <root>/<Category>/<name>.fbx
        out = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(full))), "Textures", "Shapes")
    objs = [o for o in objs if o.type == "MESH" and len(o.data.polygons) > 0]
    if not objs:
        return None
    try:
        return bake(objs, name, out)
    except Exception as e:  # noqa: BLE001 — one model's bake must not stop the others' export
        import traceback
        traceback.print_exc()
        print(f"SHAPEBAKE FAILED {name}: {e}")
        for o in objs:                                   # leave the model as a model without maps
            o.hide_render = False
            o.data.materials.clear()
            if LAYER in o.data.uv_layers:
                o.data.uv_layers.remove(o.data.uv_layers[LAYER])
        for h in [h for h in bpy.data.objects if h.name.startswith(("_hi_", "_shape_"))]:
            bpy.data.objects.remove(h, do_unlink=True)
        return None


# --------------------------------------------------------------------------- details: what stands a little proud of a model

def rivets(points, axis="-Y", r=0.004, h=0.0012, seg=10):
    """Low domes (screw heads, rivets, spot welds) at the points, their flat side on the plane across the axis."""
    import shapes as s
    out = []
    for p in points:
        b = s.ball(r, (0, 0, 0), seg=seg, scale=(1, 1, h / r))
        rot = {"Z": (0, 0, 0), "-Z": (math.pi, 0, 0), "Y": (-math.pi / 2, 0, 0), "-Y": (math.pi / 2, 0, 0),
               "X": (0, math.pi / 2, 0), "-X": (0, -math.pi / 2, 0)}[axis]
        out.append(s.place(b, p, rot))
    return out


def plate(size, loc, axis="-Y", r=0.01, proud=0.0012):
    """A pressed panel or a rib: a rounded slab lying on a face (size across the face: width, height), proud of it by
    <proud>, its edges soft."""
    import shapes as s
    w, h = size
    dims = {"Y": (w, proud * 2, h), "-Y": (w, proud * 2, h), "X": (proud * 2, w, h), "-X": (proud * 2, w, h), "Z": (w, h, proud * 2), "-Z": (w, h, proud * 2)}[axis]
    return s.rbox(dims, loc, r=min(r, proud * 0.95), seg=2)
