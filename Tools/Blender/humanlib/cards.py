"""Hair cards: hair and beards as many narrow strips of the strand texture (Art/Textures/Humans/hair_strands.png), the
way games draw hair strand by strand without a hundred thousand hairs of geometry.

A card is rooted on the outer surface of a hairstyle's volume (face.hair / face.beard: the sculpted mass), grows along the
style's combing direction — from the crown's whorl, down under its own weight, back to a bun — lying on the volume at a
depth of its own (cards over cards: layers), and stops where the hairline is near (the skin closer than `stop`). Across
the card the strip is a few millimetres wide, narrowing towards the tip; along it the texture runs from the root (v = 0)
to the tip (v = 1), one of its four columns a card (neighbouring cards differ). The sculpted mass stays under the cards,
darker: between strands no skin shows.
"""
import math

import bpy
import numpy as np


def _grad(f, p, eps=2e-4):
    v = f(p)
    g = np.stack([(f(p + np.eye(3)[i] * eps) - v) / eps for i in range(3)], -1)
    return g / np.maximum(np.linalg.norm(g, axis=-1, keepdims=True), 1e-9), v


def roots_on(obj, keep, count, rng):
    """`count` points spread evenly over the faces of a mesh where `keep(points, normals)` holds: (points, normals)."""
    me = obj.data
    me.calc_loop_triangles()
    tris = np.empty(len(me.loop_triangles) * 3, np.int64)
    me.loop_triangles.foreach_get("vertices", tris)
    tris = tris.reshape(-1, 3)
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    a, b, c = co[tris[:, 0]], co[tris[:, 1]], co[tris[:, 2]]
    cross = np.cross(b - a, c - a)
    area = 0.5 * np.linalg.norm(cross, axis=1)
    nrm = cross / np.maximum(2 * area[:, None], 1e-12)
    centre = (a + b + c) / 3
    ok = keep(centre, nrm) & (area > 0)
    if not ok.any():
        return np.zeros((0, 3)), np.zeros((0, 3)), 0.0
    w = area * ok
    pick = rng.choice(len(tris), size=count, p=w / w.sum())
    r1, r2 = rng.random(count), rng.random(count)
    flip = r1 + r2 > 1
    r1, r2 = np.where(flip, 1 - r1, r1), np.where(flip, 1 - r2, r2)
    pts = a[pick] + (b[pick] - a[pick]) * r1[:, None] + (c[pick] - a[pick]) * r2[:, None]
    return pts, nrm[pick], float(w.sum())


def grow(volume, skin, roots, flow, length, segments, width, depth, rng, stop=0.0022, jitter=0.35, cut=None):
    # depth: how far into the volume the deepest layer lies (a card's layer is from 0.6 of it over the surface to all of
    # it under; the sculpted mass under the cards is deeper still)
    """Grows a card from every root: lists of spine points (segments + 1 of them at most; a card that reaches the hairline
    early is shorter) and the surface's normals along them. volume(p) — the hair's field (negative inside); skin(p) — the
    head's; flow(p) — the combing direction (any length; it is laid onto the surface); cut(p) > 0 — a card may not go
    there (inside a beret)."""
    n = len(roots)
    lengths = length * rng.uniform(0.65, 1.2, n)
    seg = lengths / segments
    d = depth * rng.uniform(-0.6, 1.0, n)
    p = roots.copy()
    for _ in range(4):                                                    # the root onto its own layer
        g, v = _grad(volume, p)
        p = p - g * (v + d)[:, None]
    turn = rng.uniform(-jitter, jitter, n)                                # each card's own slant off the combing
    spine = [p.copy()]
    normals = [_grad(volume, p)[0]]
    alive = np.ones(n, bool)
    count = np.ones(n, int)
    for _k in range(segments):
        g, _ = _grad(volume, p)
        t = flow(p)
        t = t - g * np.sum(t * g, -1, keepdims=True)
        t /= np.maximum(np.linalg.norm(t, axis=-1, keepdims=True), 1e-9)
        b = np.cross(g, t)
        t = t * np.cos(turn)[:, None] + b * np.sin(turn)[:, None]
        q = p + t * seg[:, None]
        for _ in range(3):                                                # back onto the layer
            gq, v = _grad(volume, q)
            q = q - gq * (v + d)[:, None]
        ok = alive & (skin(q) > stop) & (np.abs(volume(q) + d) < 0.002)
        if cut is not None:
            ok &= cut(q) > 0.0
        alive = ok
        p = np.where(alive[:, None], q, p)
        count += alive
        spine.append(p.copy())
        normals.append(_grad(volume, p)[0])
    spine = np.stack(spine, 1)                                            # (n, segments + 1, 3)
    normals = np.stack(normals, 1)
    widths = width * rng.uniform(0.75, 1.25, n)
    return spine, normals, count, widths


def mesh(name, spine, normals, count, widths, rng, lift=0.0003, tip_lift=0.003, taper=0.55, min_points=3, flyaways=0.07, v_from=0.0):
    """The cards' strips as one mesh object: two vertices across every spine point, a quad (two triangles) between
    neighbours, UVs on one column of the strand texture (root v = 0, tip v = 1). A card's tip stands off the head by
    `tip_lift` (some, the flyaways, by three times that): the hair's outline breaks up instead of a smooth helmet.
    `v_from` > 0 starts the card up the texture, past its dense root (short cards of a beard: wisps, not scales)."""
    verts, faces, uvs = [], [], []
    for i in range(len(spine)):
        m = int(count[i])
        if m < min_points:
            continue
        pts, nrm = spine[i, :m], normals[i, :m]
        along = np.gradient(pts, axis=0)
        along /= np.maximum(np.linalg.norm(along, axis=1, keepdims=True), 1e-9)
        side = np.cross(nrm, along)
        side /= np.maximum(np.linalg.norm(side, axis=1, keepdims=True), 1e-9)
        col = int(rng.integers(0, 4))
        off = tip_lift * (2.4 if rng.random() < flyaways else rng.uniform(0.4, 1.2))
        u0, u1 = col / 4 + 0.008, (col + 1) / 4 - 0.008
        base = len(verts)
        for k in range(m):
            s = k / (m - 1)
            half = 0.5 * widths[i] * (1.0 - taper * s)
            c = pts[k] + nrm[k] * (lift + off * s ** 1.6)
            verts += [tuple(c - side[k] * half), tuple(c + side[k] * half)]
            uvs += [(u0, v_from + (1 - v_from) * s), (u1, v_from + (1 - v_from) * s)]
        for k in range(m - 1):
            a = base + 2 * k
            faces += [(a, a + 2, a + 3), (a, a + 3, a + 1)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    uv = me.uv_layers.new(name="UVMap")
    loop_uv = np.empty(len(me.loops) * 2)
    vi = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", vi)
    loop_uv = np.asarray(uvs, float)[vi].ravel()
    uv.data.foreach_set("uv", loop_uv)
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    return obj


def flow_of(style, H):
    """The combing direction of a style (points of the body → directions): `H` takes body points to the head's frame."""
    whorl = np.array((0.02, 0.045, 0.118))
    knot = np.array((0.0, 0.100, 0.010))

    def down(q, share):
        return np.array((0.0, 0.0, -1.0)) * share

    def f(p):
        q = H(p)
        if style in ("messy", "crop", "buzz"):
            d = q - whorl
            d[..., 2] *= 0.5
            weight = np.clip((0.07 - q[..., 2]) / 0.09, 0, 1)[..., None]
            return d / np.maximum(np.linalg.norm(d, axis=-1, keepdims=True), 1e-9) + weight * down(q, 1.2)
        if style == "bob":
            d = q - np.array((-0.012, 0.0, 0.135))                          # from a parting just off the middle, then down
            return d / np.maximum(np.linalg.norm(d, axis=-1, keepdims=True), 1e-9) + down(q, 1.6)
        if style == "bun":
            return knot - q                                               # pulled back to the knot at the nape
        if style == "beard":
            return np.broadcast_to(np.array((0.0, -0.35, -1.0)), q.shape).copy()
        raise ValueError(style)
    return f
