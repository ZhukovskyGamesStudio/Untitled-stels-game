"""From dense sculpts to game meshes: budgets, UVs, baked maps, the audit of the result.

Every piece of the figure is sculpted dense (humanlib.sdf), painted on its vertices, cut to a triangle budget and laid
out in a texture atlas; the dense sculpt is then baked onto the cut mesh — a tangent-space normal map (folds, lids,
knuckles the triangles no longer hold), the painted colour, ambient occlusion and smoothness. The game lights the
figure with real per-pixel normals instead of the old per-vertex ones.
"""
import math
import os

import bmesh
import bpy
import numpy as np

from . import sdf


class Piece:
    """One sculpted piece: its dense mesh, its game mesh, the part it belongs to (the renderer in the game) and the
    atlas it is baked into."""
    def __init__(self, name, part, atlas, hi, tris, bones, keep=None, texel=1.0, sides=None):
        self.name, self.part, self.atlas, self.hi, self.tris, self.bones = name, part, atlas, hi, tris, bones
        self.keep, self.texel, self.lo, self.sides = keep, texel, None, sides


# --------------------------------------------------------------------------- painting

def paint(obj, rgb, smooth):
    """Vertex colours of a dense mesh: `Col` (albedo, linear) and `Gloss` (smoothness in every channel)."""
    me = obj.data
    n = len(me.vertices)
    rgb = np.clip(np.broadcast_to(np.asarray(rgb, float), (n, 3)), 0, 1)
    smooth = np.clip(np.broadcast_to(np.asarray(smooth, float), (n,)), 0, 1)
    for name, data in (("Col", np.concatenate([rgb, np.ones((n, 1))], 1)), ("Gloss", np.concatenate([np.repeat(smooth[:, None], 3, 1), np.ones((n, 1))], 1))):
        attr = me.color_attributes.get(name) or me.color_attributes.new(name, "FLOAT_COLOR", "POINT")
        attr.data.foreach_set("color", data.astype(np.float32).ravel())


def normals_of(obj):
    n = np.empty(len(obj.data.vertices) * 3)
    obj.data.vertices.foreach_get("normal", n)
    return n.reshape(-1, 3)


def noise(p, scale, seed=0):
    """Cheap smooth value noise in 0..1 (sums of sines: enough for mottling)."""
    q = np.asarray(p, float) * scale
    s = seed * 1.7
    v = np.sin(q[:, 0] * 1.0 + 1.3 * np.sin(q[:, 1] * 1.3 + s) + s) + np.sin(q[:, 1] * 1.1 + 1.2 * np.sin(q[:, 2] * 0.9 + 2 * s))
    v += np.sin(q[:, 2] * 1.2 + 1.4 * np.sin(q[:, 0] * 0.8 + 3 * s) + 0.5)
    return np.clip(v / 6 + 0.5, 0, 1)


def speckle(p, scale, density, seed=0):
    """0..1 dots (freckles, stubble): a hash of the cell a point is in."""
    q = np.floor(np.asarray(p, float) * scale).astype(np.int64)
    h = (q[:, 0] * 73856093) ^ (q[:, 1] * 19349663) ^ (q[:, 2] * 83492791) ^ (seed * 2654435761)
    return ((h % 1000) / 1000.0 < density).astype(float)


# --------------------------------------------------------------------------- the game mesh

def _apply(obj, name):
    bpy.context.view_layer.objects.active = obj
    for o in bpy.context.scene.objects:
        o.select_set(o == obj)
    bpy.ops.object.modifier_apply(modifier=name)


def lowpoly(piece):
    """The game mesh of a dense sculpt: collapsed to its triangle budget (`keep(points) -> 0..1` holds triangles where
    they matter: a face rather than a scalp), triangulated, smooth."""
    hi = piece.hi
    lo = hi.copy()
    lo.data = hi.data.copy()
    lo.name = piece.name + "_lo"
    bpy.context.collection.objects.link(lo)
    for a in list(lo.data.color_attributes):
        lo.data.color_attributes.remove(a)
    n = sdf.tri_count(lo)
    if n > piece.tris:
        dec = lo.modifiers.new("dec", "DECIMATE")
        dec.ratio = piece.tris / n
        dec.use_collapse_triangulate = True
        if piece.keep is not None:
            group = lo.vertex_groups.new(name="keep")
            w = np.clip(piece.keep(sdf.points_of(lo)), 0, 0.95)
            for i, x in enumerate(w):
                if x > 0.01:
                    group.add([i], float(x), "REPLACE")
            dec.vertex_group = "keep"
            dec.invert_vertex_group = True
            dec.vertex_group_factor = 1.0
        _apply(lo, "dec")
        if "keep" in lo.vertex_groups:
            lo.vertex_groups.remove(lo.vertex_groups["keep"])
    bm = bmesh.new()
    bm.from_mesh(lo.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges)
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(lo.data)
    bm.free()
    lo.data.polygons.foreach_set("use_smooth", [True] * len(lo.data.polygons))
    piece.lo = lo
    return lo


def unwrap(piece, angle=66.0):
    """UVs of a game mesh: islands by the angle between faces, sized by their area times the piece's texel weight."""
    lo = piece.lo
    bpy.ops.object.select_all(action="DESELECT")
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=0.003, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.average_islands_scale()
    bpy.ops.object.mode_set(mode="OBJECT")
    if piece.texel != 1.0:
        uv = lo.data.uv_layers.active.data
        co = np.empty(len(uv) * 2)
        uv.foreach_get("uv", co)
        uv.foreach_set("uv", co * piece.texel)


def pack(pieces, margin=0.004):
    """Packs the islands of several game meshes into one square: the meshes are joined for the packing and split again."""
    los = [p.lo for p in pieces]
    counts = [len(o.data.loops) for o in los]
    # (pack_islands works on the edit mesh of all selected objects at once)
    bpy.ops.object.select_all(action="DESELECT")
    for o in los:
        o.select_set(True)
    bpy.context.view_layer.objects.active = los[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=margin, scale=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    return counts


# --------------------------------------------------------------------------- baking

def _image(name, size, data=False, colour=(0, 0, 0, 1)):
    img = bpy.data.images.new(name, size, size, alpha=False, float_buffer=False)
    img.generated_color = colour
    if data:
        img.colorspace_settings.name = "Non-Color"
    return img


def _bake_material(img):
    mat = bpy.data.materials.new("_bake_target")
    mat.use_nodes = True
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    mat.node_tree.nodes.active = node
    return mat, node


def _emit_material(attribute):
    mat = bpy.data.materials.new("_emit_" + attribute)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    col = nt.nodes.new("ShaderNodeVertexColor")
    col.layer_name = attribute
    em = nt.nodes.new("ShaderNodeEmission")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(col.outputs["Color"], em.inputs["Color"])
    nt.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return mat


def bake(pieces, size, path, name, ray=0.012, ao_samples=24):
    """Bakes every piece's dense sculpt onto its game mesh into one atlas and writes <path>/<name>_base.png (the painted
    colour with a share of the occlusion in it), _normal.png (tangent space, OpenGL) and _mask.png (G occlusion,
    A smoothness: URP's metallic-gloss and occlusion maps in one)."""
    s = bpy.context.scene
    s.render.engine = "CYCLES"
    s.cycles.device = "CPU"
    s.cycles.use_denoising = False
    bk = s.render.bake
    bk.use_selected_to_active = True
    bk.use_cage = False
    bk.cage_extrusion = ray
    bk.max_ray_distance = ray * 2
    bk.margin = max(4, size // 128)
    bk.margin_type = "EXTEND"
    bk.use_clear = False
    bk.normal_space = "TANGENT"
    imgs = {"normal": _image(name + "_normal", size, True, (0.5, 0.5, 1, 1)), "col": _image(name + "_col", size, False, (0.5, 0.5, 0.5, 1)),
            "ao": _image(name + "_ao", size, True, (1, 1, 1, 1)), "gloss": _image(name + "_gloss", size, True, (0.3, 0.3, 0.3, 1))}
    target, node = _bake_material(imgs["normal"])
    emit_col, emit_gloss = _emit_material("Col"), _emit_material("Gloss")
    los = [p.lo for p in pieces]
    for o in los:
        o.data.materials.clear()
        o.data.materials.append(target)
    plain = bpy.data.materials.new("_plain")
    his = [h for p in pieces for h in (p.hi if isinstance(p.hi, (list, tuple)) else [p.hi])]
    for p in pieces:
        srcs = p.hi if isinstance(p.hi, (list, tuple)) else [p.hi]
        for o in los:
            o.hide_render = o is not p.lo                      # the other game meshes neither catch nor block rays
        for kind, mat, samples in (("normal", plain, 1), ("col", emit_col, 1), ("gloss", emit_gloss, 1), ("ao", plain, ao_samples)):
            node.image = imgs[kind]
            s.cycles.samples = samples
            for h in his:
                h.data.materials.clear()
                h.data.materials.append(mat)
                h.hide_render = False if kind == "ao" else h not in srcs      # occlusion sees every sculpt; the rest only its own
            bpy.ops.object.select_all(action="DESELECT")
            for h in srcs:
                h.hide_render = False
                h.select_set(True)
            p.lo.select_set(True)
            bpy.context.view_layer.objects.active = p.lo
            bpy.ops.object.bake(type={"normal": "NORMAL", "col": "EMIT", "gloss": "EMIT", "ao": "AO"}[kind])
    for o in los:
        o.hide_render = False
        o.data.materials.clear()

    def px(img):
        a = np.empty(size * size * 4, np.float32)
        img.pixels.foreach_get(a)
        return a.reshape(size, size, 4)
    col, ao, gloss, nrm = px(imgs["col"]), px(imgs["ao"]), px(imgs["gloss"]), px(imgs["normal"])
    occ = np.clip(ao[..., 0:1], 0, 1)
    base = col.copy()
    base[..., :3] = np.clip(col[..., :3] * (0.55 + 0.45 * occ ** 0.8), 0, 1)        # crevices stay dark under any light and any style's filter
    base[..., 3] = 1
    mask = np.zeros_like(col)
    mask[..., 1] = occ[..., 0]
    mask[..., 3] = gloss[..., 0]
    os.makedirs(path, exist_ok=True)
    out = {}
    for suffix, data, is_data, alpha in (("base", base, False, False), ("normal", nrm, True, False), ("mask", mask, True, True)):
        img = bpy.data.images.new(f"{name}_{suffix}_out", size, size, alpha=alpha)
        if is_data:
            img.colorspace_settings.name = "Non-Color"
        if not alpha:
            data = data.copy()
            data[..., 3] = 1
        img.pixels.foreach_set(data.ravel())
        img.filepath_raw = os.path.join(path, f"{name}_{suffix}.png")
        img.file_format = "PNG"
        img.save()
        out[suffix] = img
    for img in imgs.values():
        bpy.data.images.remove(img)
    for m in (target, emit_col, emit_gloss, plain):
        bpy.data.materials.remove(m)
    return out


# --------------------------------------------------------------------------- weights

def weights(points, bones, allowed, radius, power=5.0, gate=0.02):
    """Skin weights of points by the distance to the allowed bones' segments, each measured in the bone's own radius
    (a thigh reaches further than a finger); a bone of one side has no say on the other side of the body. Returns
    {bone: (N,) weights} of the four strongest bones per point, normalised."""
    names = list(allowed)
    w = np.zeros((len(points), len(names)))
    for i, name in enumerate(names):
        a, b = bones[name]
        d, _ = sdf.seg_dist(points, a, b)
        v = 1.0 / np.maximum(d / radius.get(name.split(".")[0], 0.08), 0.05) ** power
        if name.endswith(".L"):
            v *= np.clip(points[:, 0] / gate * 0.5 + 0.5, 0, 1)
        elif name.endswith(".R"):
            v *= np.clip(-points[:, 0] / gate * 0.5 + 0.5, 0, 1)
        w[:, i] = v
    if len(names) > 4:
        cut = np.sort(w, axis=1)[:, -4][:, None]
        w = np.where(w >= cut, w, 0.0)
    w /= np.maximum(w.sum(1, keepdims=True), 1e-12)
    w[w < 0.02] = 0.0
    w /= np.maximum(w.sum(1, keepdims=True), 1e-12)
    return {n: w[:, i] for i, n in enumerate(names)}


def assign(obj, wts):
    for name, w in wts.items():
        g = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
        for i in np.nonzero(w > 0)[0]:
            g.add([int(i)], float(w[i]), "REPLACE")


# --------------------------------------------------------------------------- the audit

def audit(obj, label=None):
    """Topology of a game mesh, printed as one TOPO line; returns the number of hard errors (non-manifold edges, faces
    with no area, loose vertices, vertices without weights or with more than four)."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.normal_update()
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    nonman = sum(1 for e in bm.edges if not e.is_manifold and not e.is_boundary)
    rim = sum(1 for e in bm.edges if e.is_boundary)
    zero = sum(1 for f in bm.faces if f.calc_area() < 1e-10)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    slivers = 0
    for f in bm.faces:
        ls = sorted(e.calc_length() for e in f.edges)
        if ls[0] > 0 and f.calc_area() / (ls[-1] ** 2) < 0.02:
            slivers += 1
    poles = sum(1 for v in bm.verts if len(v.link_edges) > 9)
    flipped = sum(1 for e in bm.edges if e.is_manifold and e.calc_face_angle(0.0) > math.radians(170))
    nverts = len(bm.verts)
    bm.free()
    unweighted = heavy = 0
    if obj.vertex_groups:
        for v in me.vertices:
            n = sum(1 for g in v.groups if g.weight > 1e-4)
            unweighted += n == 0
            heavy += n > 4
    uv_out = 0
    if me.uv_layers.active:
        co = np.empty(len(me.uv_layers.active.data) * 2)
        me.uv_layers.active.data.foreach_get("uv", co)
        uv_out = int(((co < -1e-4) | (co > 1 + 1e-4)).sum())
    errors = nonman + rim + zero + loose + unweighted + heavy
    print(f"TOPO {label or obj.name}: {tris} tris, {nverts} verts | non-manifold {nonman}, open edges {rim}, zero-area {zero}, "
          f"loose {loose}, slivers {slivers} ({100.0 * slivers / max(1, tris):.1f}%), poles>9 {poles}, folded {flipped} | "
          f"unweighted {unweighted}, >4 bones {heavy}, uv outside {uv_out}" + ("  <-- ERRORS" if errors else ""))
    return errors


def clean(obj):
    """Repairs what a collapse can leave — faces without area, edges shared by three faces, holes, loose vertices —
    until the mesh is closed and manifold: the faces round a bad edge go (a ring wider every few attempts) and the
    simple hole left is closed again."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges)
    for attempt in range(14):
        wire = [e for e in bm.edges if not e.link_faces]
        if wire:
            bmesh.ops.delete(bm, geom=wire, context="EDGES")
        bad = [e for e in bm.edges if not e.is_manifold]            # (shared by three faces, or by one: a hole's rim)
        if not bad:
            break
        if attempt or any(not e.is_boundary for e in bad):
            verts = {v for e in bad for v in e.verts}
            for _ring in range(attempt // 3):
                verts |= {e.other_vert(v) for v in verts for e in v.link_edges}
            bmesh.ops.delete(bm, geom=list({f for v in verts for f in v.link_faces}), context="FACES")
            loose = [v for v in bm.verts if not v.link_faces]
            if loose:
                bmesh.ops.delete(bm, geom=loose, context="VERTS")
        rim = [e for e in bm.edges if e.is_boundary]
        if rim:
            bmesh.ops.holes_fill(bm, edges=rim, sides=0)
            rim = [e for e in bm.edges if e.is_boundary]
            if rim:
                bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=rim)
        big = [f for f in bm.faces if len(f.verts) > 3]
        if big:
            bmesh.ops.triangulate(bm, faces=big)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges)
    bm.normal_update()
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.polygons.foreach_set("use_smooth", [True] * len(obj.data.polygons))
    sdf.keep_biggest(obj, share=0.01)                              # (a repair can cut a crumb loose)


def orient_like(lo, hi):
    """Turns every face of a game mesh the way the dense sculpt faces there (a repaired hole's faces come out either way,
    and recalc_face_normals has turned whole meshes inside out: the bake then reads the sculpt from within)."""
    from mathutils.bvhtree import BVHTree
    src = hi.data
    bvh = BVHTree.FromPolygons([v.co for v in src.vertices], [tuple(p.vertices) for p in src.polygons])
    normals = [p.normal.copy() for p in src.polygons]
    bm = bmesh.new()
    bm.from_mesh(lo.data)
    bm.normal_update()
    wrong = []
    for f in bm.faces:
        hit, n, index, _ = bvh.find_nearest(f.calc_center_median())
        if index is not None and f.normal.dot(normals[index]) < 0:
            wrong.append(f)
    if wrong:
        bmesh.ops.reverse_faces(bm, faces=wrong)
    # then the odd face out (in a crease the nearest sculpt face may look the other way): a face wound against two of
    # its three neighbours is turned, until none is left
    turned = len(wrong)
    for _ in range(12):
        odd = []
        for f in bm.faces:
            against = 0
            for l in f.loops:
                other = [x for x in l.edge.link_loops if x is not l]
                if len(other) == 1 and other[0].vert is l.vert:        # (two faces of a manifold edge run it in opposite directions)
                    against += 1
            if against >= 2:
                odd.append(f)
        if not odd:
            break
        bmesh.ops.reverse_faces(bm, faces=odd)
        turned += len(odd)
    bm.normal_update()
    bm.to_mesh(lo.data)
    bm.free()
    # and a whole closed piece inside out (a strap thinner than the distance to its own other side): its volume is negative
    comps = sdf.islands(lo)
    pts = sdf.points_of(lo)
    part = np.zeros(len(pts), np.int64)
    for k, c in enumerate(comps):
        part[c] = k
    volume = np.zeros(len(comps))
    for poly in lo.data.polygons:
        a, b_, c = (pts[i] for i in poly.vertices)
        volume[part[poly.vertices[0]]] += a @ np.cross(b_, c) / 6.0
    inside_out = {k for k in range(len(comps)) if volume[k] < 0}
    if inside_out:
        bm = bmesh.new()
        bm.from_mesh(lo.data)
        flip = [f for f in bm.faces if part[f.verts[0].index] in inside_out]
        bmesh.ops.reverse_faces(bm, faces=flip)
        bm.normal_update()
        bm.to_mesh(lo.data)
        bm.free()
        turned += len(flip)
    return turned
