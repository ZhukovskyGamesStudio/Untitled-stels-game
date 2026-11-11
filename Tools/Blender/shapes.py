"""A modelling vocabulary richer than boxes: rounded and lofted forms, lathes, tubes, grooves, subdivision, booleans,
and a finishing pass (smooth by angle + weighted normals + metre UVs) that makes bevels read as real edges.

    import shapes as s
    torso = s.loft([(1.05, .22, .16, 3), (1.35, .30, .19, 3.5), (1.62, .36, .21, 4)], seg=40, round_caps=.03)
    s.groove(torso, "Z", 1.32, .012, .006)
    body = s.part("Body", [torso, *s.mirror_x(s.rbox((.12, .12, .3), (.4, 0, 1.3), r=.03))])
    s.export_fbx(path, [body])

Every shape function returns a bmesh in world coordinates (metres; Blender x right, -y front, z up), so shapes can be
deformed (taper, groove, smooth, boolean) before `part()` merges them into one named object. `part()` finishes the
object: smooth shading with sharp edges above an angle, a Weighted Normal modifier (flat faces stay flat, bevels catch
the light), box-projected UVs in metres. Review the result with review.py — never trust a model you have not looked at.

Rules of thumb (see .claude/skills/blender-modeling):
  * no hard 90° edge on anything a player can see: rbox r >= 1 cm, bevelled cylinder caps
  * forms are lofted / lathed, not stacked: a torso is one loft whose sections change, not three boxes
  * parts must touch or overlap: review.check() reports DETACHED pieces
  * detail goes where the eye goes (head, hands, front); flat zones are rest, not failure
"""
import math

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

# --------------------------------------------------------------------------- helpers


def place(bm, loc=(0, 0, 0), rot=(0, 0, 0), scale=None):
    """Scale (x, y, z), rotate (Euler XYZ, radians) about the origin, then move to loc."""
    if scale is not None:
        bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    if any(rot):
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Euler(rot, "XYZ").to_matrix(), verts=bm.verts)
    if any(loc):
        bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return bm


def _axis_rot(axis):
    """Rotation taking a shape built along +Z onto the given axis ('X', 'Y', 'Z', '-X', '-Y', '-Z')."""
    return {
        "Z": (0, 0, 0), "-Z": (math.pi, 0, 0),
        "X": (0, math.pi / 2, 0), "-X": (0, -math.pi / 2, 0),
        "Y": (-math.pi / 2, 0, 0), "-Y": (math.pi / 2, 0, 0),
    }[axis]


def bevel_edges(bm, r, seg=2, angle=30, profile=0.5):
    """Bevel every edge sharper than `angle` degrees."""
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(angle)]
    if edges and r > 0:
        bmesh.ops.bevel(bm, geom=edges, offset=r, offset_type="OFFSET", segments=seg, profile=profile,
                        affect="EDGES", clamp_overlap=True)
    return bm


def copy(bm):
    return bm.copy()


def mirror_x(bm, keep=True):
    """[bm, its mirror across x = 0] (or only the mirror). Build one side, mirror the other."""
    m = bm.copy()
    bmesh.ops.scale(m, vec=Vector((-1, 1, 1)), verts=m.verts)
    bmesh.ops.reverse_faces(m, faces=m.faces)
    return [bm, m] if keep else [m]


# --------------------------------------------------------------------------- primitives with real edges

def rbox(size, loc=(0, 0, 0), r=0.015, seg=2, rot=(0, 0, 0), taper=(1.0, 1.0), profile=0.5, vertical_only=False):
    """Box with rounded edges. taper = (x, y) scale of the top face. vertical_only rounds only the upright edges
    (a pillar, a cabinet). r is clamped to just under half the smallest side."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if taper != (1.0, 1.0):
        for v in bm.verts:
            if v.co.z > 0:
                v.co.x *= taper[0]
                v.co.y *= taper[1]
    r = min(r, 0.49 * min(size))
    if r > 0:
        edges = bm.edges[:]
        if vertical_only:
            edges = [e for e in edges if abs((e.verts[0].co - e.verts[1].co).normalized().z) > 0.7]
        bmesh.ops.bevel(bm, geom=edges, offset=r, offset_type="OFFSET", segments=seg, profile=profile,
                        affect="EDGES", clamp_overlap=True)
    return place(bm, loc, rot)


def rcyl(radius, depth, loc=(0, 0, 0), axis="Z", seg=24, r=0.006, bevel_seg=2, radius2=None, rot=(0, 0, 0)):
    """Cylinder (or cone with radius2) with chamfered caps."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=depth)
    bevel_edges(bm, min(r, 0.45 * min(radius, depth)), bevel_seg, angle=50)
    place(bm, rot=_axis_rot(axis))
    return place(bm, loc, rot)


def ball(radius, loc=(0, 0, 0), seg=24, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(8, seg // 2), radius=radius)
    return place(bm, loc, scale=scale)


# --------------------------------------------------------------------------- lofts and lathes

def _ring(a, b, n, seg):
    """Superellipse |x/a|^n + |y/b|^n = 1 sampled with points spread by length *and* turning, so flat sides stay
    light and rounded corners keep enough points. n = 2 ellipse, 3-4 rounded rectangle, 8+ almost a box."""
    dense = 720
    pts = []
    for i in range(dense):
        t = 2 * math.pi * i / dense
        c, s = math.cos(t), math.sin(t)
        pts.append(Vector((a * math.copysign(abs(c) ** (2 / n), c), b * math.copysign(abs(s) ** (2 / n), s))))
    weights = []
    for i in range(dense):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % dense]
        d1, d2 = p1 - p0, p2 - p1
        turn = d1.angle(d2, 0.0) if d1.length > 1e-9 and d2.length > 1e-9 else 0.0
        weights.append((d2.length, turn))
    total_len = sum(w[0] for w in weights) or 1.0
    total_turn = sum(w[1] for w in weights) or 1.0
    acc = [0.0]
    for ln, tn in weights:
        acc.append(acc[-1] + 0.5 * ln / total_len + 0.5 * tn / total_turn)
    out = []
    j = 0
    for k in range(seg):
        target = k / seg
        while acc[j + 1] < target:
            j += 1
        f = (target - acc[j]) / max(1e-12, acc[j + 1] - acc[j])
        out.append(pts[j].lerp(pts[(j + 1) % dense], f))
    return out


def _norm_ring(r):
    r = tuple(r)
    z, a, b = r[0], r[1], r[2]
    n = r[3] if len(r) > 3 else 2.0
    x = r[4] if len(r) > 4 else 0.0
    y = r[5] if len(r) > 5 else 0.0
    return [z, a, b, n, x, y]


def loft(rings, seg=32, axis="Z", loc=(0, 0, 0), round_caps=0.0, cap=True):
    """Skin a solid through cross-sections. rings: (z, a, b[, n[, x, y]]) — position along the axis, half-width (x),
    half-depth (y), superellipse exponent, centre offset. A ring with a == 0 is a pole (pointed / domed end).
    round_caps = r fillets both ends inside the given length. The workhorse for torsos, heads, limbs, seats, bottles."""
    rings = [_norm_ring(r) for r in rings]
    if round_caps > 0:
        for end in (0, -1):
            z, a, b, n, x, y = rings[end]
            if a <= 1e-6:
                continue
            sign = 1 if end == -1 else -1
            inner = [z - sign * round_caps, a, b, n, x, y]
            fillet = []
            for k in range(1, 4):
                th = k / 3 * math.pi / 2
                shrink = round_caps * (1 - math.cos(th))
                fillet.append([z - sign * round_caps + sign * round_caps * math.sin(th),
                               max(1e-4, a - shrink), max(1e-4, b - shrink), n, x, y])
            if end == -1:
                rings = rings[:-1] + [inner] + fillet
            else:
                rings = list(reversed(fillet)) + [inner] + rings[1:]
    bm = bmesh.new()
    loops = []
    for z, a, b, n, x, y in rings:
        if a <= 1e-6 or b <= 1e-6:
            loops.append([bm.verts.new((x, y, z))])
        else:
            loops.append([bm.verts.new((x + p.x, y + p.y, z)) for p in _ring(a, b, n, seg)])
    for lo, up in zip(loops, loops[1:]):
        if len(lo) == 1 and len(up) == 1:
            continue
        if len(lo) == 1:
            for j in range(seg):
                bm.faces.new((lo[0], up[j], up[(j + 1) % seg]))
        elif len(up) == 1:
            for j in range(seg):
                bm.faces.new((lo[j], lo[(j + 1) % seg], up[0]))
        else:
            for j in range(seg):
                bm.faces.new((lo[j], lo[(j + 1) % seg], up[(j + 1) % seg], up[j]))
    if cap:
        if len(loops[0]) > 1:
            bm.faces.new(list(reversed(loops[0])))
        if len(loops[-1]) > 1:
            bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    place(bm, rot=_axis_rot(axis))
    return place(bm, loc)


def lathe(profile, seg=32, axis="Z", loc=(0, 0, 0)):
    """Solid of revolution from (radius, z) pairs, bottom to top; radius 0 closes the end to a point.
    Lenses, knobs, lamps, bottles, joints, wheels."""
    return loft([(z, r, r, 2.0) for r, z in profile], seg=seg, axis=axis, loc=loc)


def tube(points, radius, res=2, smooth=True, caps=True, radii=None, segments_per=8):
    """Round tube along points (a cable, pipe, handle, rail). smooth: a Bézier through the points, else straight runs.
    radii: per-point radius factors (taper)."""
    cu = bpy.data.curves.new("_tube", "CURVE")
    cu.dimensions = "3D"
    cu.bevel_mode = "ROUND"
    cu.bevel_depth = radius
    cu.bevel_resolution = res
    cu.use_fill_caps = caps
    cu.resolution_u = segments_per
    if smooth:
        sp = cu.splines.new("BEZIER")
        sp.bezier_points.add(len(points) - 1)
        for k, (bp, p) in enumerate(zip(sp.bezier_points, points)):
            bp.co = p
            bp.handle_left_type = bp.handle_right_type = "AUTO"
            bp.radius = radii[k] if radii else 1.0
    else:
        sp = cu.splines.new("POLY")
        sp.points.add(len(points) - 1)
        for k, (pt, p) in enumerate(zip(sp.points, points)):
            pt.co = (*p, 1.0)
            pt.radius = radii[k] if radii else 1.0
    obj = bpy.data.objects.new("_tube", cu)
    bpy.context.scene.collection.objects.link(obj)
    bm = _evaluated(obj)
    bpy.data.curves.remove(cu)
    return bm


# --------------------------------------------------------------------------- deformers and detail

def taper(bm, axis="Z", k0=1.0, k1=0.7, center=(0, 0)):
    """Scale cross-sections linearly from k0 at the low end to k1 at the high end of the axis."""
    i = "XYZ".index(axis)
    others = [j for j in range(3) if j != i]
    vals = [v.co[i] for v in bm.verts]
    lo, hi = min(vals), max(vals)
    for v in bm.verts:
        t = (v.co[i] - lo) / max(1e-9, hi - lo)
        k = k0 + (k1 - k0) * t
        for m, j in enumerate(others):
            v.co[j] = center[m] + (v.co[j] - center[m]) * k
    return bm


def groove(bm, axis, value, width=0.012, depth=0.006):
    """A panel seam all around a form: a chamfered channel at `value` along `axis` (cuts the mesh at four planes and
    pushes the middle two rings in). Seams turn a smooth blob into a manufactured shell."""
    i = "XYZ".index(axis)
    no = Vector((0, 0, 0))
    no[i] = 1.0
    for off in (-width, -width / 2, width / 2, width):
        co = Vector((0, 0, 0))
        co[i] = value + off
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no)
    bm.normal_update()
    band = [v for v in bm.verts if abs(v.co[i] - value) <= width / 2 + 1e-6]
    moves = [(v, v.normal.copy()) for v in band]
    for v, n in moves:
        n[i] = 0.0  # push straight in, not along the axis
        if n.length > 1e-6:
            v.co -= n.normalized() * depth
    return bm


def inset_faces(bm, select, thickness=0.02, depth=-0.008):
    """Inset faces chosen by select(face) -> bool (e.g. by normal and area) and push them (negative depth = recess)."""
    faces = [f for f in bm.faces if select(f)]
    if faces:
        bmesh.ops.inset_individual(bm, faces=faces, thickness=thickness, depth=0.0)
        if depth:
            bmesh.ops.inset_individual(bm, faces=faces, thickness=min(0.004, thickness * 0.3), depth=depth)
    return bm


def facing(direction, min_area=0.0, tol=0.8):
    """Face filter for inset_faces: faces whose normal points along direction (e.g. (0, -1, 0) = front)."""
    d = Vector(direction).normalized()
    return lambda f: f.normal.dot(d) > tol and f.calc_area() >= min_area


def bolts(points, radius=0.012, depth=0.008, axis="-Y", seg=10):
    """Small chamfered studs/rivets at points, their heads facing along axis."""
    return [rcyl(radius, depth, p, axis=axis, seg=seg, r=radius * 0.35, bevel_seg=1) for p in points]


def grille(center, width, height, n=5, bar=0.012, proud=0.01, axis="-Y", frame=0.014):
    """A vent: a frame and n horizontal slats standing proud of a surface whose outside faces `axis`."""
    parts = []
    gap = height / (n + 1)
    for k in range(n):
        z = -height / 2 + gap * (k + 1)
        parts.append(rbox((width - 2 * frame, proud, bar), (0, 0, z), r=bar * 0.3, seg=1, rot=(math.radians(-25), 0, 0)))
    for x in (-width / 2 + frame / 2, width / 2 - frame / 2):
        parts.append(rbox((frame, proud * 1.4, height), (x, 0, 0), r=frame * 0.3, seg=1))
    for z in (-height / 2 + frame / 2, height / 2 - frame / 2):
        parts.append(rbox((width, proud * 1.4, frame), (0, 0, z), r=frame * 0.3, seg=1))
    rot = {"-Y": (0, 0, 0), "Y": (0, 0, math.pi), "X": (0, 0, math.pi / 2), "-X": (0, 0, -math.pi / 2)}[axis]
    for p in parts:
        place(p, Vector(center), rot)
    return parts


# --------------------------------------------------------------------------- modifiers through temporary objects

def _temp_object(bm, name="_tmp"):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _evaluated(obj):
    """bmesh of an object with its modifiers applied; removes the object."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    if isinstance(data, bpy.types.Mesh) and data.users == 0:
        bpy.data.meshes.remove(data)
    return bm


def smooth(bm, levels=2, crease_angle=None):
    """Catmull-Clark subdivision. Edges sharper than crease_angle (degrees) keep full crease: soft forms with crisp
    seams. Feed it a coarse cage (a low-seg loft or a box) — subdivision turns cages into organic forms."""
    if crease_angle is not None:
        layer = bm.edges.layers.float.get("crease_edge") or bm.edges.layers.float.new("crease_edge")
        for e in bm.edges:
            if e.is_manifold and e.calc_face_angle(0) > math.radians(crease_angle):
                e[layer] = 1.0
    obj = _temp_object(bm)
    bm.free()
    mod = obj.modifiers.new("subd", "SUBSURF")
    mod.levels = mod.render_levels = levels
    mod.use_creases = True
    return _evaluated(obj)


def boolean(bm, cutter, op="DIFFERENCE"):
    """Boolean with the exact solver: op DIFFERENCE (holes, slots, cut-outs), UNION, INTERSECT.
    Bevel after cutting (bevel_edges) so the cut does not look like a knife slice."""
    a = _temp_object(bm, "_bool_a")
    b = _temp_object(cutter, "_bool_b")
    bm.free()
    cutter.free()
    mod = a.modifiers.new("bool", "BOOLEAN")
    mod.object = b
    mod.operation = op
    mod.solver = "EXACT"
    out = _evaluated(a)
    me = b.data
    bpy.data.objects.remove(b, do_unlink=True)
    bpy.data.meshes.remove(me)
    return out


def skin(points, radii, edges=None, smooth_levels=2):
    """Organic limb/branch from a skeleton: vertices with radii, joined by edges (a chain by default), wrapped with
    the Skin modifier and subdivided. Tentacles, hoses, plant stems, cartoon limbs."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in points]
    for a, b in edges or zip(range(len(vs) - 1), range(1, len(vs))):
        bm.edges.new((vs[a], vs[b]))
    obj = _temp_object(bm, "_skin")
    bm.free()
    obj.modifiers.new("skin", "SKIN")  # creates the skin-vertex layer
    for v, r in zip(obj.data.skin_vertices[0].data, radii):
        v.radius = (r, r)
    if smooth_levels:
        sub = obj.modifiers.new("subd", "SUBSURF")
        sub.levels = sub.render_levels = smooth_levels
    return _evaluated(obj)


# --------------------------------------------------------------------------- objects

def _box_uv(bm):
    """UVs in metres by box projection (textured materials tile the same on every part)."""
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda k: abs(n[k]))
        u, v = [(1, 2), (0, 2), (0, 1)][ax]
        for loop in f.loops:
            loop[uv].uv = (loop.vert.co[u], loop.vert.co[v])


def part(name, bms, sharp_angle=35.0, weighted=True, flat=False):
    """Merge bmeshes into one named object (a material role: Body, Accent, Eye, …) and finish it."""
    if isinstance(bms, bmesh.types.BMesh):
        bms = [bms]
    result = bmesh.new()
    for b in bms:
        me = bpy.data.meshes.new("_merge")
        b.to_mesh(me)
        b.free()
        result.from_mesh(me)
        bpy.data.meshes.remove(me)
    bmesh.ops.recalc_face_normals(result, faces=result.faces)
    bmesh.ops.dissolve_degenerate(result, dist=1e-6, edges=result.edges)
    result.normal_update()
    _box_uv(result)
    me = bpy.data.meshes.new(name)
    result.to_mesh(me)
    result.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    finish(obj, sharp_angle, weighted, flat)
    return obj


def finish(obj, sharp_angle=35.0, weighted=True, flat=False):
    """Smooth shading with sharp edges above sharp_angle, plus Weighted Normals: flat faces stay flat and the bevel
    strips take the gradient — the cheapest way to make a low-poly part look manufactured."""
    me = obj.data
    if flat:
        for p in me.polygons:
            p.use_smooth = False
        return obj
    bm = bmesh.new()
    bm.from_mesh(me)
    for f in bm.faces:
        f.smooth = True
    limit = math.radians(sharp_angle)
    for e in bm.edges:
        e.smooth = not (not e.is_manifold or e.calc_face_angle(0) > limit)
    bm.to_mesh(me)
    bm.free()
    if weighted and not any(m.type == "WEIGHTED_NORMAL" for m in obj.modifiers):
        mod = obj.modifiers.new("wn", "WEIGHTED_NORMAL")
        mod.keep_sharp = True
        mod.mode = "FACE_AREA"
    return obj


def split_at(obj, z, names=("Upper", "Lower")):
    """Cut an object at height z into <name>Upper (above) and <name>Lower (below), both cuts closed — for the legs
    the game bends at the knee (ProceduralLegs)."""
    out = []
    for suffix, keep_above in zip(names, (True, False)):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1),
                                     clear_inner=keep_above, clear_outer=not keep_above)
        cut = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if cut:
            bmesh.ops.holes_fill(bm, edges=cut, sides=0)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(obj.name + suffix)
        bm.to_mesh(me)
        bm.free()
        new = bpy.data.objects.new(obj.name + suffix, me)
        bpy.context.scene.collection.objects.link(new)
        new.matrix_world = obj.matrix_world.copy()
        finish(new)
        out.append(new)
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.meshes.remove(data)
    return out


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for item in list(block):
            block.remove(item)


def export_fbx(path, objs=None):
    """FBX in the game's orientation (Blender -Y front -> Unity +Z), modifiers applied, custom normals kept."""
    import os
    objs = objs or [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in bpy.context.scene.objects:
        o.select_set(o in objs)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH"},
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False)
    print("exported", path)
    return path
