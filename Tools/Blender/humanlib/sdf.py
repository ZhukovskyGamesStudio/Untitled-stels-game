"""Sculpting with distance fields (numpy) for Tools/Blender/human.py.

A form is a function f(points (N, 3)) -> distances (N,), negative inside. Volumes are blended with smooth unions
(`smin`), carved with smooth subtractions (`smax(d, -cut)`), and the surface is taken where the field is zero by naive
surface nets (`mesh`): an all-quad, even, watertight mesh that follows any topology — an arm growing out of a shoulder,
a hood out of a collar — where the old tubes of cross-sections needed a seam.
"""
import math

import bmesh
import bpy
import numpy as np


# --------------------------------------------------------------------------- fields

def smin(a, b, k):
    """Smooth union: the two volumes melt into each other over about k metres."""
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b + (a - b) * h - k * h * (1 - h)


def smax(a, b, k):
    """Smooth intersection; smax(d, -cut, k) carves cut out of d."""
    return -smin(-a, -b, k)


def ell(p, c, r, rot=None):
    """An ellipsoid: centre, radii, optional 3x3 rotation of its axes (columns are its axes in the world)."""
    r = np.asarray(r, float)
    q = p - np.asarray(c, float)
    if rot is not None:
        q = q @ np.asarray(rot, float)
    k0 = np.linalg.norm(q / r, axis=-1)
    k1 = np.linalg.norm(q / (r * r), axis=-1)
    return np.where(k1 > 1e-9, k0 * (k0 - 1.0) / np.maximum(k1, 1e-9), -r.min())   # (a better metric than (k0 - 1) * min r)


def cone(p, a, b, r0, r1=None, flat=None):
    """A capsule from a to b with radii r0, r1 (a "round cone": a limb). flat = (axis, share): squeezed along a world
    axis to that share of its thickness (a wrist is wider than deep)."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    r1 = r0 if r1 is None else r1
    q = p - a
    if flat is not None:
        axis, share = flat
        q = q.copy()
        ab = (b - a).copy()
        q[..., axis] /= share
        ab[axis] /= share
    else:
        ab = b - a
    t = np.clip((q @ ab) / (ab @ ab), 0, 1)
    d = np.linalg.norm(q - t[..., None] * ab, axis=-1) - (r0 + (r1 - r0) * t)
    return d * (min(1.0, flat[1]) if flat is not None else 1.0)


def chain(p, pts, k=0.0):
    """A limb through points (x, y, z, r): round cones blended at the joints."""
    d = None
    for (a, b) in zip(pts, pts[1:]):
        c = cone(p, a[:3], b[:3], a[3], b[3])
        d = c if d is None else smin(d, c, k)
    return d


def plane(p, point, normal):
    """The half-space behind a plane (negative on the side the normal points away from)."""
    n = np.asarray(normal, float)
    n = n / np.linalg.norm(n)
    return (p - np.asarray(point, float)) @ n


def box(p, c, half, r=0.0):
    q = np.abs(p - np.asarray(c, float)) - (np.asarray(half, float) - r)
    return np.linalg.norm(np.maximum(q, 0), axis=-1) + np.minimum(np.max(q, axis=-1), 0) - r


def seg_dist(p, a, b):
    """Distance to a segment and the share along it."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    ab = b - a
    t = np.clip(((p - a) @ ab) / max(ab @ ab, 1e-12), 0, 1)
    return np.linalg.norm(p - a - t[..., None] * ab, axis=-1), t


def gauss(v, w):
    return np.exp(-(v / w) ** 2)


def win(v, lo, hi, soft=0.03):
    """1 between lo and hi, fading to 0 over `soft` outside."""
    return np.clip((v - lo) / soft + 1, 0, 1) * np.clip((hi - v) / soft + 1, 0, 1)


def ridge(p, a, b, width, taper=True):
    """0..1 along a crease from a to b: a round profile `width` across, fading to nothing at its ends (a fold of
    cloth is highest in its middle)."""
    d, t = seg_dist(p, a, b)
    along = np.sin(np.clip(t, 0, 1) * math.pi) ** 0.6 if taper else 1.0
    return gauss(d, width) * along


def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return np.array(((1, 0, 0), (0, c, -s), (0, s, c)))


def rot_y(a):
    c, s = math.cos(a), math.sin(a)
    return np.array(((c, 0, s), (0, 1, 0), (-s, 0, c)))


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return np.array(((c, -s, 0), (s, c, 0), (0, 0, 1)))


# --------------------------------------------------------------------------- the surface

def nets(f, lo, hi, h, chunk=1 << 21):
    """Naive surface nets of f = 0 inside the box lo..hi on a grid of step h: (vertices (V, 3), quads (Q, 4))."""
    lo, hi = np.asarray(lo, float), np.asarray(hi, float)
    n = np.ceil((hi - lo) / h).astype(int) + 1
    gx, gy, gz = (lo[i] + h * np.arange(n[i]) for i in range(3))
    d = np.empty(n, np.float32)
    per = max(1, chunk // (n[1] * n[2]))                       # slabs along x: the memory stays small
    for x0 in range(0, n[0], per):
        x1 = min(n[0], x0 + per)
        X, Y, Z = np.meshgrid(gx[x0:x1], gy, gz, indexing="ij")
        d[x0:x1] = f(np.stack([X, Y, Z], -1).reshape(-1, 3)).reshape(x1 - x0, n[1], n[2])
    inside = d < 0
    tot = np.zeros((n[0] - 1, n[1] - 1, n[2] - 1), np.int8)
    corners = [(dx, dy, dz) for dx in (0, 1) for dy in (0, 1) for dz in (0, 1)]
    for dx, dy, dz in corners:
        tot += inside[dx:n[0] - 1 + dx, dy:n[1] - 1 + dy, dz:n[2] - 1 + dz]
    active = (tot > 0) & (tot < 8)
    cells = np.argwhere(active)
    idx = -np.ones(active.shape, np.int64)
    idx[active] = np.arange(len(cells))
    acc = np.zeros((len(cells), 3))
    cnt = np.zeros(len(cells))
    vals = np.stack([d[cells[:, 0] + a, cells[:, 1] + b, cells[:, 2] + c] for a, b, c in corners], 1).astype(np.float64)
    for i, a in enumerate(corners):
        for j, b in enumerate(corners):
            if j <= i or sum(abs(a[k] - b[k]) for k in range(3)) != 1:
                continue
            va, vb = vals[:, i], vals[:, j]
            cross = (va < 0) != (vb < 0)
            t = np.where(cross, va / np.where(cross, va - vb, 1.0), 0.0)
            pt = np.array(a, float)[None, :] + t[:, None] * (np.array(b, float) - np.array(a, float))[None, :]
            acc += np.where(cross[:, None], pt, 0.0)
            cnt += cross
    verts = lo + (cells + acc / np.maximum(cnt, 1)[:, None]) * h
    quads = []
    for axis in range(3):
        o = [0, 0, 0]
        o[axis] = 1
        a = inside[: n[0] - o[0], : n[1] - o[1], : n[2] - o[2]]
        b = inside[o[0]:, o[1]:, o[2]:]
        e = np.argwhere(a != b)
        u, v = (axis + 1) % 3, (axis + 2) % 3
        ok = (e[:, u] > 0) & (e[:, v] > 0) & (e[:, u] < n[u] - 1) & (e[:, v] < n[v] - 1) & (e[:, axis] < n[axis] - 1)
        e = e[ok]

        def cell(du, dv):
            q = e.copy()
            q[:, u] -= du
            q[:, v] -= dv
            return idx[q[:, 0], q[:, 1], q[:, 2]]
        q = np.stack([cell(1, 1), cell(0, 1), cell(0, 0), cell(1, 0)], 1)
        flip = ~a[e[:, 0], e[:, 1], e[:, 2]]             # (the quad as built faces +axis: right when the inside is below)
        q[flip] = q[flip][:, ::-1]
        quads.append(q)
    return verts, np.concatenate(quads)


def obj_of(name, verts, faces, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata(np.asarray(verts).tolist(), [], np.asarray(faces).tolist())
    me.update()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    if smooth:
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    return obj


def mesh(name, f, lo, hi, h, relax=2, mirror=False):
    """The dense sculpt of a field: surface nets, the few non-manifold spots of touching surfaces repaired, relaxed.
    mirror: f is symmetric in x — only x >= 0 is sampled and the other half is its reflection (welded on x = 0)."""
    if mirror:
        lo = (-h * 1.5, lo[1], lo[2])
    v, q = nets(f, lo, hi, h)
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in v]
    for a in q:
        try:
            bm.faces.new((vs[a[0]], vs[a[1]], vs[a[2]], vs[a[3]]))
        except ValueError:
            pass                                           # (a duplicate face where two sheets pinch)
    if mirror:
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0), plane_no=(-1, 0, 0),
                               clear_outer=True, dist=1e-6)
        bmesh.ops.mirror(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], axis="X", merge_dist=1e-5)
    repair(bm)
    # A net's vertices sit up to a fraction of a cell off the surface, in terraces where it slips from one grid plane to
    # the next (faint contour lines in any shading, and in a baked normal map): the quads are evened out, then every
    # vertex is put on the true surface along the field's gradient.
    for _ in range(relax):
        bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.verts.ensure_lookup_table()
    pts = np.array([v.co[:] for v in bm.verts])
    pts = snap(f, pts, h)
    for v, co in zip(bm.verts, pts):
        v.co = co
    orient(bm, f, h)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    return obj


def orient(bm, f, h):
    """Every face turned to look out of the solid (along the field's gradient). (recalc_face_normals is not to be
    trusted here: it left half of a pair of trousers inside out.)"""
    bm.faces.ensure_lookup_table()
    bm.normal_update()
    c = np.array([fc.calc_center_median()[:] for fc in bm.faces])
    n = np.array([fc.normal[:] for fc in bm.faces])
    e = h * 0.5
    g = np.stack([f(c + o) - f(c - o) for o in (np.array((e, 0, 0)), np.array((0, e, 0)), np.array((0, 0, e)))], -1)
    wrong = [bm.faces[i] for i in np.nonzero((n * g).sum(1) < 0)[0]]
    if wrong:
        bmesh.ops.reverse_faces(bm, faces=wrong)
    bm.normal_update()


def snap(f, pts, h, steps=3):
    """Newton steps onto f = 0 along the gradient (never further than half a cell a step: the net is already close)."""
    e = h * 0.25
    pts = np.array(pts, float)
    for _ in range(steps):
        d = f(pts)
        g = np.stack([f(pts + o) - f(pts - o) for o in (np.array((e, 0, 0)), np.array((0, e, 0)), np.array((0, 0, e)))], -1) / (2 * e)
        g2 = np.maximum((g * g).sum(-1), 1e-6)
        step = -(d / g2)[:, None] * g
        ln = np.linalg.norm(step, axis=-1)
        step *= np.minimum(1.0, (0.5 * h) / np.maximum(ln, 1e-12))[:, None]
        pts += step
    return pts


def repair(bm):
    """Makes a net watertight and manifold: faces round a non-manifold edge go and the holes are filled."""
    for _ in range(3):
        bad = [e for e in bm.edges if not e.is_manifold and not e.is_boundary]
        if not bad:
            break
        faces = {f for e in bad for f in e.link_faces}
        bmesh.ops.delete(bm, geom=list(faces), context="FACES")
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        rim = [e for e in bm.edges if e.is_boundary]
        if rim:
            bmesh.ops.holes_fill(bm, edges=rim, sides=0)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges)
    rim = [e for e in bm.edges if e.is_boundary]
    if rim:
        bmesh.ops.holes_fill(bm, edges=rim, sides=0)
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big)


def islands(obj):
    """Vertex index sets of the connected pieces of a mesh, biggest first."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    seen, out = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop()
            comp.append(x.index)
            for e in x.link_edges:
                o = e.other_vert(x)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        out.append(comp)
    bm.free()
    return sorted(out, key=len, reverse=True)


def keep_biggest(obj, share=0.02):
    """Drops what is not the sculpt itself: stray crumbs of a net (pieces smaller than `share` of the biggest) and
    closed cavities inside it (pieces turned inside out: their volume is negative; nobody sees them, and a model check
    counts them as parts with their normals inward)."""
    comps = islands(obj)
    if len(comps) < 2:
        return obj
    me = obj.data
    pts = points_of(obj)
    part = np.zeros(len(pts), np.int64)
    for k, c in enumerate(comps):
        part[c] = k
    volume = np.zeros(len(comps))
    for poly in me.polygons:
        vs = list(poly.vertices)
        a = pts[vs[0]]
        for b, c in zip(vs[1:], vs[2:]):
            volume[part[vs[0]]] += a @ np.cross(pts[b], pts[c]) / 6.0
    drop = [i for k, c in enumerate(comps) if k > 0 and (len(c) < share * len(comps[0]) or volume[k] < 0) for i in c]   # (never the sculpt itself)
    if drop:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.verts[i] for i in drop], context="VERTS")
        bm.to_mesh(obj.data)
        bm.free()
    return obj


def points_of(obj):
    co = np.empty(len(obj.data.vertices) * 3)
    obj.data.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def set_points(obj, pts):
    obj.data.vertices.foreach_set("co", np.asarray(pts, float).ravel())
    obj.data.update()


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)
