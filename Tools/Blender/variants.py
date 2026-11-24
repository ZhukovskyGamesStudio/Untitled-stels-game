"""Variant pieces the game puts on furniture at random: a bare coat stand and a bare wall hook rail, the garments that
hang on their hooks (each placement a different mix), and books for shelves.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/variants.py -- Assets/_Game/Art/Models [names]
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <root>/Props/Variants/garment_*.fbx --lineup 4

Writes <root>/Props/Variants/<name>.fbx and the contract Tools/Blender/variants_contract.json (merged by name, so a
partial build keeps the other entries). Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), z up.
Object names are material roles: Body (wood / cloth / cover — the game recolours garments by swapping it), Accent
(metal: hooks, zips, buckles, ferrules; on books the page block, tinted paper-white).

  coat_stand_bare   free-standing stand 1.84 m: a weighted round base, a turned pole, six bentwood arms at the top
                    and three short hooks lower down; footprint like coat_rack (within 0.5 x 0.5)
  hall_hooks_bare   the wall rail of hall_hooks without the clothes: a board with five double hooks at 1.7 and a slatted
                    hat shelf at 1.9 on two brackets; same footprint (1.0 x 0.3 in front of the wall, back flush with
                    +Y at y = 0.175, origin on the floor under its middle)
  garment_*         origin AT THE HOOK POINT they hang from (the loop / hood / strap / crook rests there), hanging
                    down -Z, front -Y, modelled all round. The game turns a garment so its front (Unity +Z) points along
                    the hook's direction and puts its origin on the hook point (contract).
  book_0..5         upright books, origin at the bottom centre, spine to -Y; book_lean leans 17 deg towards Blender +X
                    (Unity -X) with its origin on the bottom edge it pivots on; book_stack — four books lying flat

Contract (Unity coordinates: Unity x = -Blender x, Unity y = Blender z, Unity z = -Blender y; metres from the model
origin): hook points and directions of the holders, the bounding box of each garment relative to its hook point,
width (along the shelf) / height / depth of each book.
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import printed  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
ONLY = set(argv[1].split(",")) if len(argv) > 1 and argv[1] else None
OUT = os.path.join(ROOT, "Props", "Variants")
CONTRACT_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "variants_contract.json")
DEG = math.pi / 180
YAW_CAP = 20.0       # the cap hangs a little turned on its strap, so its visor shows in profile from the front

CONTRACT = {"holders": {}, "garments": {}, "books": {}}


def unity(p):
    """Blender point/direction -> Unity axes (the FBX export: -Y forward becomes +Z, x mirrors)."""
    return [round(-p[0], 4), round(p[2], 4), round(-p[1], 4)]


# --------------------------------------------------------------------------- helpers

def catmull(ctrl, per=6):
    """Points along a Catmull-Rom spline through ctrl (per samples between two control points)."""
    pts = [Vector(p) for p in ctrl]
    ext = [pts[0] + (pts[0] - pts[1])] + pts + [pts[-1] + (pts[-1] - pts[-2])]
    out = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for k in range(per):
            t = k / per
            out.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3))
    out.append(pts[-1])
    return out


def circle(r, n=8, k=1.0, phase=0.0):
    """Section points: an ellipse r x r*k (u along the sweep's side vector, v along its normal)."""
    return [(r * math.cos(phase + 2 * math.pi * j / n), k * r * math.sin(phase + 2 * math.pi * j / n)) for j in range(n)]


def rrect(hu, hv, n=10, e=4.0):
    """Section points: a rounded rectangle (superellipse exponent e) of half sizes hu x hv."""
    out = []
    for j in range(n):
        t = 2 * math.pi * (j + 0.5) / n
        c, sn = math.cos(t), math.sin(t)
        out.append((hu * math.copysign(abs(c) ** (2 / e), c), hv * math.copysign(abs(sn) ** (2 / e), sn)))
    return out


def sweep(path, section, up=(0, 0, 1), closed=False, cap=True, twist=None):
    """A solid swept along path: section (a list of (u, v) or a function of t in 0..1) laid out in each point's frame
    — u along `up` made perpendicular to the path, v along tangent x side. twist(t) rotates the section (radians)."""
    path = [Vector(p) for p in path]
    n = len(path)
    upv = Vector(up)
    bm = bmesh.new()
    rings = []
    prev = None
    for i, p in enumerate(path):
        if closed:
            a, b = path[i - 1], path[(i + 1) % n]
        else:
            a, b = path[max(i - 1, 0)], path[min(i + 1, n - 1)]
        tan = (b - a).normalized()
        side = upv - tan * upv.dot(tan)
        side = side.normalized() if side.length > 1e-6 else (prev or tan.orthogonal().normalized())
        prev = side
        nor = tan.cross(side)
        t = i / n if closed else i / max(1, n - 1)
        sec = section(t) if callable(section) else section
        ang = twist(t) if twist else 0.0
        ca, sa = math.cos(ang), math.sin(ang)
        ring = []
        for u, v in sec:
            u, v = u * ca - v * sa, u * sa + v * ca
            ring.append(bm.verts.new(p + side * u + nor * v))
        rings.append(ring)
    m = len(rings[0])
    pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
    for r0, r1 in pairs:
        for j in range(m):
            bm.faces.new((r0[j], r0[(j + 1) % m], r1[(j + 1) % m], r1[j]))
    if cap and not closed:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def ring_loft(rings, cap=True):
    """Skin a solid through rings of Vectors (all the same count; a one-point ring is a pole)."""
    bm = bmesh.new()
    loops = [[bm.verts.new(p) for p in r] for r in rings]
    m = max(len(r) for r in loops)
    for lo, up in zip(loops, loops[1:]):
        if len(lo) == 1:
            for j in range(m):
                bm.faces.new((lo[0], up[j], up[(j + 1) % m]))
        elif len(up) == 1:
            for j in range(m):
                bm.faces.new((lo[j], lo[(j + 1) % m], up[0]))
        else:
            for j in range(m):
                bm.faces.new((lo[j], lo[(j + 1) % m], up[(j + 1) % m], up[j]))
    if cap:
        for ring in (loops[0], loops[-1]):
            if len(ring) > 1:
                f = bm.faces.new(ring)
                bmesh.ops.triangulate(bm, faces=[f], quad_method="BEAUTY", ngon_method="BEAUTY")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def prism(pts, axis, lo, hi):
    """Polygon pts (in the two other axes, X-Y-Z order) extruded along axis from lo to hi; caps triangulated
    (the outlines here are concave)."""
    bm = bmesh.new()

    def co(t, u, v):
        return {"X": (t, u, v), "Y": (u, t, v), "Z": (u, v, t)}[axis]

    a = [bm.verts.new(co(lo, u, v)) for u, v in pts]
    b = [bm.verts.new(co(hi, u, v)) for u, v in pts]
    n = len(pts)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    caps = [bm.faces.new(a), bm.faces.new(b[::-1])]
    bmesh.ops.triangulate(bm, faces=caps, quad_method="BEAUTY", ngon_method="EAR_CLIP")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def strip_prism(outer, inner, lo, hi):
    """A C-shaped prism along Z: the cross-section is the band between two open chains of the same point count
    (outer and inner, both in the same order); the caps are quads between them, so no degenerate triangles."""
    assert len(outer) == len(inner)
    pts = list(outer) + list(reversed(inner))
    n = len(pts)
    bm = bmesh.new()
    a = [bm.verts.new((u, v, lo)) for u, v in pts]
    b = [bm.verts.new((u, v, hi)) for u, v in pts]
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    m = len(outer)
    for ring in (a, b):
        for k in range(m - 1):
            bm.faces.new((ring[k], ring[k + 1], ring[n - 2 - k], ring[n - 1 - k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def knob(r, loc, seg=8):
    """A small round knob (cheaper than a uv sphere)."""
    return s.lathe([(0.0, -r), (0.75 * r, -0.66 * r), (r, 0.0), (0.75 * r, 0.66 * r), (0.0, r)], seg=seg, loc=loc)


def transform(bm, mat):
    bmesh.ops.transform(bm, matrix=mat, verts=bm.verts)
    return bm


# --------------------------------------------------------------------------- garment bodies

def body_rings(prof, n_around=24, dent=None, crease=0.0, phase=0.0):
    """Rings of a hanging garment. prof: (z, a, b, cy, n, fold) — height, half width, half depth, centre y,
    superellipse exponent, fold amplitude (radial ripples, bigger towards the hem); a == 0 is a pole.
    dent(x, z) -> push back along +Y (hood openings); crease: a front centre line pushed in (the opening of a coat)."""
    rings = []
    for z, a, b, cy, n, fold in prof:
        if a <= 0:
            rings.append([Vector((0, cy, z))])
            continue
        ring = []
        for i in range(n_around):
            th = -math.pi / 2 + 2 * math.pi * i / n_around
            c, sn = math.cos(th), math.sin(th)
            x = a * math.copysign(abs(c) ** (2 / n), c)
            y = b * math.copysign(abs(sn) ** (2 / n), sn)
            if fold:
                f = fold * (0.65 * math.sin(5 * th + 0.7 + phase + z * 1.3) + 0.35 * math.sin(3 * th + 2.1 - z * 0.8))
                x += f * c
                y += f * sn
            if crease and i == 0:
                y += crease
            p = Vector((x, cy + y, z))
            if dent is not None and y < 0:
                p.y += dent(p.x, z)
            ring.append(p)
        rings.append(ring)
    return rings


def surface_y(rings, x, z, front=True):
    """The front (or back) surface's y at (x, z) on a body made of rings (interpolated between the two rings
    bracketing z)."""
    def on_ring(ring):
        best = None
        m = len(ring)
        for j in range(m):
            p, q = ring[j], ring[(j + 1) % m]
            if (p.x - x) * (q.x - x) <= 0 and abs(q.x - p.x) > 1e-9:
                y = p.y + (q.y - p.y) * (x - p.x) / (q.x - p.x)
                if best is None or (y < best if front else y > best):
                    best = y
        return best

    full = [r for r in rings if len(r) > 1]
    for r0, r1 in zip(full, full[1:]):
        z0, z1 = r0[0].z, r1[0].z
        if (z0 - z) * (z1 - z) <= 0 and abs(z1 - z0) > 1e-9:
            y0, y1 = on_ring(r0), on_ring(r1)
            if y0 is None or y1 is None:
                continue
            return y0 + (y1 - y0) * (z - z0) / (z1 - z0)
    raise ValueError(f"no surface at x={x} z={z}")


def slope(rings, x, z):
    """Yaw (radians about Z) that turns a -Y-facing detail flat onto the front surface at (x, z)."""
    d = 0.01
    return math.atan2(surface_y(rings, x + d, z) - surface_y(rings, x - d, z), 2 * d)


def lerp_prof(prof, z):
    """(a, b, cy) of a profile at height z."""
    rows = [r for r in prof if r[1] > 0]
    for r0, r1 in zip(rows, rows[1:]):
        if (r0[0] - z) * (r1[0] - z) <= 0 and abs(r1[0] - r0[0]) > 1e-9:
            t = (z - r0[0]) / (r1[0] - r0[0])
            return tuple(r0[k] + (r1[k] - r0[k]) * t for k in (1, 2, 3))
    raise ValueError(z)


def collar_ring(prof, z_back, z_front, out=0.006, hu=0.03, hv=0.013, n_path=14):
    """A rolled collar around the neckline: a closed band following the body, high at the back, low at the front."""
    path = []
    for k in range(n_path):
        ph = 2 * math.pi * k / n_path
        w = (1 - math.sin(ph)) / 2          # 0 at the back (+Y), 1 at the front (-Y)
        z = z_back + (z_front - z_back) * w
        a, b, cy = lerp_prof(prof, z)
        path.append(Vector(((a + out) * math.cos(ph), cy + (b + out) * math.sin(ph), z)))
    return sweep(path, rrect(hu, hv, 6), up=(0, 0, 1), closed=True)


def sleeve(points, r0, r1, k=0.85, cuff=0.0, puff=None, sides=6, per=2):
    """A sleeve hanging along points (shoulder first): an ellipse section tapering r0 -> r1, a cuff step at the end,
    puff(t) -> radius factor (quilted sleeves)."""
    path = catmull(points, per)

    def sec(t):
        r = r0 + (r1 - r0) * t
        if puff:
            r *= puff(t)
        if cuff and t > 0.93:
            r += cuff
        return circle(r, sides, k)
    return sweep(path, sec, up=(1, 0, 0))


def detail_on(rings, size, x, z, proud=0.004, r=0.006, seg=1, taper=(1, 1), roll=0.0):
    """A box detail (pocket, flap, patch) laid flat on the front surface at (x, z): proud of it by `proud`."""
    y = surface_y(rings, x, z) - proud + size[1] / 2
    return s.rbox(size, (x, y, z), r=r, seg=seg, taper=taper, rot=(0, roll, slope(rings, x, z)))


def stud(rings, x, z, r=0.011, depth=0.008, seg=6):
    """A button / snap on the front surface (half sunk in)."""
    y = surface_y(rings, x, z)
    bm = s.rcyl(r, depth, (0, 0, 0), axis="Y", seg=seg, r=0.0)
    return s.place(bm, (x, y, z), (0, 0, slope(rings, x, z)))


# --------------------------------------------------------------------------- export and contract

def tris_of(objs):
    return sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)


def bounds_unity(objs):
    lo = [1e9] * 3
    hi = [-1e9] * 3
    for o in objs:
        for v in o.data.vertices:
            u = unity(o.matrix_world @ v.co)
            lo = [min(a, b) for a, b in zip(lo, u)]
            hi = [max(a, b) for a, b in zip(hi, u)]
    return [round(c, 3) for c in lo], [round(c, 3) for c in hi]


def export(name, floor=True):
    """meshfix, record the bounds, write <root>/Props/Variants/<name>.fbx, clear the scene. Returns (objs info)."""
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=floor, name=name)
    lo, hi = bounds_unity(objs)
    info = {"tris": tris_of(objs), "bounds_min": lo, "bounds_max": hi}
    s.export_fbx(os.path.join(OUT, name + ".fbx"), objs)
    s.clear_scene()
    return info


# --------------------------------------------------------------------------- holders

def cradle_of(ctrl, rad):
    """The point a garment's loop rests on: the top of the hook rod at its lowest point (a fine spline sample)."""
    fine = catmull(ctrl, 24)
    low = min(fine[len(fine) // 4:], key=lambda p: p.z)
    return low + Vector((0, 0, rad))


def model_coat_stand_bare():
    """Stand 1.84 m: a cast weighted base 0.44 across (Accent), a turned pole tapering 60 -> 44 mm with a finial
    (Body), six bentwood arms from a collar at 1.73 curving out and down to a cradle at r 0.19, z 1.66 and up to a knob
    (Body), three short hooks from a collar at 1.43 (cradle r 0.11). Collars Accent. Footprint radius 0.24."""
    base = s.lathe([(0.0, 0.0), (0.208, 0.0), (0.219, 0.007), (0.22, 0.02), (0.207, 0.033), (0.15, 0.044),
                    (0.08, 0.056), (0.05, 0.07), (0.04, 0.09), (0.0, 0.093)], seg=24)
    pole = s.lathe([(0.0, 0.06), (0.036, 0.06), (0.036, 0.1), (0.032, 0.13), (0.03, 0.17), (0.028, 0.6),
                    (0.025, 1.3), (0.022, 1.7), (0.022, 1.765), (0.027, 1.778), (0.031, 1.8), (0.029, 1.815),
                    (0.02, 1.833), (0.008, 1.842), (0.0, 1.844)], seg=16)
    body, acc = [pole], [base]
    acc.append(s.rcyl(0.035, 0.05, (0, 0, 1.725), seg=16, r=0.006, bevel_seg=1))
    acc.append(s.rcyl(0.032, 0.04, (0, 0, 1.43), seg=16, r=0.005, bevel_seg=1))
    acc.append(s.rcyl(0.04, 0.02, (0, 0, 0.115), seg=16, r=0.004, bevel_seg=1))
    hooks = []
    upper = [(0.014, 1.742), (0.08, 1.725), (0.15, 1.684), (0.196, 1.664), (0.217, 1.682), (0.223, 1.712)]
    lower = [(0.014, 1.437), (0.06, 1.424), (0.105, 1.403), (0.125, 1.412), (0.132, 1.438)]
    for tier, prof, count, phase, rad, kr in (("upper", upper, 6, 0.0, 0.013, 0.019),
                                               ("lower", lower, 3, 30.0, 0.011, 0.016)):
        for k in range(count):
            ang = (phase + 360.0 * k / count) * DEG
            d = Vector((math.cos(ang), math.sin(ang), 0))
            side = Vector((-math.sin(ang), math.cos(ang), 0))
            ctrl = [d * r + Vector((0, 0, z)) for r, z in prof]
            path = catmull(ctrl, 2)
            body.append(sweep(path, circle(rad, 6), up=side))
            body.append(knob(kr, path[-1] + d * 0.002 + Vector((0, 0, 0.006)), seg=7))
            hooks.append({"tier": tier, "point": unity(cradle_of(ctrl, rad)), "dir": unity(d)})
    s.part("Body", body)
    s.part("Accent", acc)
    info = export("coat_stand_bare", floor=True)
    info["hooks"] = hooks
    info["note"] = ("garment origin on 'point', its front (Unity +Z) turned to 'dir'; upper arms take coats, lower "
                    "hooks bags, umbrellas, scarves, caps (they hang inside the coats' reach)")
    CONTRACT["holders"]["coat_stand_bare"] = info


def model_hall_hooks_bare():
    """hall_hooks without the clothes: a board 1.0 x 0.16 at 1.7 on the wall (Body), five double hooks on rosettes
    (Accent) — the long lower one with a cradle 9 cm out, the short upper one rising —, a hat shelf of three slats at
    1.9 on two forged brackets with a curved brace (Accent). Back flush with +Y at 0.175, front of the shelf at -0.12
    (Furnisher.Foot: 1.0 x 0.3)."""
    yb = 0.175
    yf = yb - 0.025                                   # the board's front face
    body = [s.rbox((1.0, 0.025, 0.16), (0, yb - 0.0125, 1.70), r=0.008, seg=2)]
    body.append(s.rbox((1.0, 0.03, 0.02), (0, yb - 0.017, 1.774), r=0.008, seg=2))       # a rounded cap rail on top
    for k in range(3):
        w = 0.088
        y0 = yb - 0.003 - k * 0.1
        body.append(s.rbox((1.0, w, 0.022), (0, y0 - w / 2, 1.9), r=0.006, seg=2))
    acc = []
    for x in (-0.46, 0.46):
        acc.append(s.rbox((0.014, 0.28, 0.016), (x, yb - 0.143, 1.883), r=0.004, seg=1))           # under the slats
        acc.append(s.rbox((0.014, 0.016, 0.12), (x, yf - 0.006, 1.83), r=0.004, seg=1))            # down the board
        brace = [Vector((x, yf - 0.006, 1.775)), Vector((x, yf - 0.04, 1.84)), Vector((x, yf - 0.11, 1.872)),
                 Vector((x, -0.06, 1.882))]
        acc.append(sweep(catmull(brace, 3), rrect(0.007, 0.006, 6), up=(1, 0, 0)))
        acc.append(knob(0.008, (x, -0.1, 1.872), seg=8))
    hooks = []
    for x in (-0.4, -0.2, 0.0, 0.2, 0.4):
        acc.append(s.rcyl(0.02, 0.008, (x, yf - 0.002, 1.715), axis="Y", seg=8, r=0.002, bevel_seg=1))
        low_ctrl = [(x, yf, 1.72), (x, 0.12, 1.703), (x, 0.088, 1.688), (x, 0.068, 1.698), (x, 0.062, 1.722)]
        low = catmull(low_ctrl, 2)
        acc.append(sweep(low, circle(0.0065, 6), up=(1, 0, 0)))
        acc.append(knob(0.0095, low[-1] + Vector((0, -0.001, 0.004)), seg=6))
        high = catmull([(x, yf, 1.725), (x, 0.125, 1.748), (x, 0.112, 1.785)], 2)
        acc.append(sweep(high, circle(0.0055, 6), up=(1, 0, 0)))
        acc.append(knob(0.008, high[-1] + Vector((0, 0, 0.004)), seg=6))
        hooks.append({"tier": "lower", "point": unity(cradle_of(low_ctrl, 0.0065)), "dir": unity((0, -1, 0))})
        hooks.append({"tier": "upper", "point": unity(Vector((x, 0.124, 1.754))), "dir": unity((0, -1, 0))})
    s.part("Body", body)
    s.part("Accent", acc)
    info = export("hall_hooks_bare", floor=True)
    info["hooks"] = hooks
    info["note"] = ("lower = the main hooks (coats, bags, umbrellas); upper = the short top hooks (caps, scarves) — a "
                    "garment on a lower hook covers the upper hook above it; the wall is at Unity z = -0.175")
    CONTRACT["holders"]["hall_hooks_bare"] = info


# --------------------------------------------------------------------------- garments

def garment(name, fn):
    fn()
    info = export(name, floor=False)
    info["note"] = "origin = hook point; Unity +Z = the garment's front"
    CONTRACT["garments"][name] = info


def model_garment_coat_long():
    """A knee-length wool overcoat hung by its loop, 1.05 m: the shoulders fall from the hook, a rolled collar and
    lapels to a V, double-breasted buttons, two flap pockets, sleeves hanging at the sides, folds growing to the hem."""
    prof = [(0.0, 0, 0, -0.004, 2, 0),
            (-0.012, 0.075, 0.034, -0.006, 2.0, 0),
            (-0.04, 0.15, 0.048, -0.01, 2.3, 0),
            (-0.08, 0.205, 0.058, -0.012, 2.6, 0),
            (-0.13, 0.232, 0.063, -0.013, 2.8, 0.002),
            (-0.21, 0.24, 0.065, -0.013, 3.0, 0.004),
            (-0.32, 0.24, 0.066, -0.012, 3.0, 0.006),
            (-0.48, 0.236, 0.065, -0.012, 3.0, 0.008),
            (-0.64, 0.245, 0.066, -0.012, 3.0, 0.011),
            (-0.80, 0.258, 0.068, -0.012, 3.0, 0.014),
            (-0.95, 0.27, 0.07, -0.012, 3.0, 0.017),
            (-1.045, 0.277, 0.071, -0.012, 3.0, 0.019),
            (-1.02, 0.25, 0.054, -0.012, 3.0, 0.012)]
    rings = body_rings(prof, 20, crease=0.006)
    body = [ring_loft(rings)]
    body.append(collar_ring(prof, -0.012, -0.15, out=0.006, hu=0.032, hv=0.014))
    for sx in (-1, 1):
        pts = []
        for k in range(5):
            t = k / 4
            z = -0.1 - 0.29 * t
            x = sx * (0.085 - 0.07 * t)
            pts.append(Vector((x, surface_y(rings, x, z) + 0.002, z)))
        body.append(sweep(pts, lambda t: rrect(0.05 - 0.034 * t, 0.006, 6), up=(1, 0, 0)))
        body.append(sleeve([(sx * 0.17, -0.014, -0.09), (sx * 0.226, -0.02, -0.18), (sx * 0.242, -0.024, -0.40),
                            (sx * 0.244, -0.028, -0.64)], 0.058, 0.047, cuff=0.006))
        body.append(detail_on(rings, (0.16, 0.016, 0.055), sx * 0.135, -0.66, proud=0.006))
    acc = [stud(rings, sx * 0.055, z) for sx in (-1, 1) for z in (-0.44, -0.58)]
    s.part("Body", body)
    s.part("Accent", acc)


def model_garment_jacket():
    """A short quilted puffer, 0.7 m: a stand-up collar round the hook, horizontal baffles on the body and the
    sleeves, a knit waistband and cuffs, a zip down the front (Accent)."""
    full, seam = (0.262, 0.088), (0.236, 0.066)
    prof = [(0.008, 0, 0, -0.004, 2, 0),
            (-0.005, 0.045, 0.032, -0.006, 2.0, 0),
            (-0.04, 0.11, 0.055, -0.01, 2.2, 0),
            (-0.09, 0.19, 0.072, -0.01, 2.5, 0),
            (-0.15, 0.245, 0.08, -0.01, 2.8, 0)]
    z = -0.15
    for k in range(4):
        prof.append((z - 0.06, full[0], full[1], -0.01, 3.0, 0.003))
        z -= 0.118
        prof.append((z, seam[0], seam[1], -0.01, 3.0, 0.0))
    prof += [(-0.64, 0.236, 0.072, -0.01, 3.0, 0), (-0.69, 0.232, 0.07, -0.01, 3.0, 0),
             (-0.675, 0.21, 0.055, -0.01, 3.0, 0)]
    rings = body_rings(prof, 18)
    body = [ring_loft(rings)]
    # the stand-up collar: a short tube with a rolled rim round the hook
    col = [(-0.075, 0.105, 0.068), (-0.035, 0.098, 0.064), (0.0, 0.1, 0.066), (0.016, 0.106, 0.07),
           (0.01, 0.09, 0.056)]
    crings = [[Vector((a * math.cos(2 * math.pi * j / 14), -0.008 + b * math.sin(2 * math.pi * j / 14), zc))
               for j in range(14)] for zc, a, b in col]
    body.append(ring_loft(crings))
    for sx in (-1, 1):
        body.append(sleeve([(sx * 0.15, -0.012, -0.1), (sx * 0.235, -0.016, -0.2), (sx * 0.255, -0.02, -0.38),
                            (sx * 0.252, -0.024, -0.58)], 0.07, 0.058, k=0.9, sides=6, per=4,
                           puff=lambda t: 1.0 if t > 0.9 else 0.86 + 0.14 * abs(math.cos(math.pi * t * 3.5))))
        body.append(s.rcyl(0.047, 0.05, (sx * 0.252, -0.024, -0.6), seg=7, r=0.006, bevel_seg=1))
    zpts = [Vector((0, surface_y(rings, 0, zz) + 0.001, zz)) for zz in [-0.05 - 0.6 * k / 18 for k in range(19)]]
    acc = [sweep(zpts, rrect(0.007, 0.003, 4), up=(1, 0, 0))]
    acc.append(s.rbox((0.016, 0.006, 0.034), (0.0, surface_y(rings, 0, -0.1) - 0.004, -0.1), r=0.002, seg=1))
    s.part("Body", body)
    s.part("Accent", acc)


def model_garment_hoodie():
    """A hoodie hung by its hood, 0.74 m: the hood a rounded bump above the shoulders with its face opening (a hollow
    framed by a rim) to the front, drawstrings with metal tips, a kangaroo pocket, ribbed hem and cuffs."""
    hood_prof = [(0.016, 0, 0, -0.012, 2, 0),
                 (0.006, 0.055, 0.04, -0.012, 2.0, 0),
                 (-0.025, 0.11, 0.06, -0.012, 2.2, 0),
                 (-0.07, 0.138, 0.072, -0.012, 2.4, 0),
                 (-0.12, 0.146, 0.077, -0.012, 2.5, 0),
                 (-0.17, 0.142, 0.077, -0.012, 2.5, 0),
                 (-0.22, 0.13, 0.073, -0.012, 2.4, 0),
                 (-0.27, 0.11, 0.064, -0.01, 2.2, 0),
                 (-0.3, 0.07, 0.045, -0.008, 2.0, 0),
                 (-0.31, 0, 0, -0.008, 2, 0)]
    plain = body_rings(hood_prof, 18)
    ox, oz, oa, ob = 0.0, -0.15, 0.088, 0.108       # the face opening (an oval on the front)

    def dent(x, z):
        q = ((x - ox) / oa) ** 2 + ((z - oz) / ob) ** 2
        return 0.06 * math.sqrt(1 - q) if q < 1 else 0.0
    hood = body_rings(hood_prof, 18, dent=dent)
    body = [ring_loft(hood)]
    rim = []
    for k in range(12):
        ph = 2 * math.pi * k / 12
        x, z = ox + oa * math.cos(ph), oz + ob * math.sin(ph)
        rim.append(Vector((x, surface_y(plain, x, z) + 0.004, z)))
    body.append(sweep(rim, circle(0.012, 5, 1.0), up=(0, 1, 0), closed=True))
    prof = [(-0.12, 0.07, 0.04, -0.006, 2.0, 0),
            (-0.18, 0.17, 0.062, -0.006, 2.4, 0),
            (-0.24, 0.236, 0.074, -0.006, 2.8, 0.002),
            (-0.3, 0.25, 0.078, -0.006, 3.0, 0.004),
            (-0.6, 0.252, 0.078, -0.006, 3.0, 0.007),
            (-0.655, 0.25, 0.077, -0.006, 3.0, 0.006),
            (-0.668, 0.234, 0.071, -0.006, 3.0, 0),
            (-0.74, 0.232, 0.07, -0.006, 3.0, 0),
            (-0.72, 0.21, 0.056, -0.006, 3.0, 0)]
    rings = body_rings(prof, 20)
    body.append(ring_loft(rings))
    body.append(detail_on(rings, (0.3, 0.032, 0.17), 0.0, -0.55, proud=0.012, r=0.012, seg=1, taper=(0.78, 1.0)))
    acc = []
    for sx in (-1, 1):
        body.append(sleeve([(sx * 0.15, -0.0, -0.2), (sx * 0.226, -0.006, -0.28), (sx * 0.246, -0.012, -0.44),
                            (sx * 0.246, -0.016, -0.6)], 0.056, 0.046))
        body.append(s.rcyl(0.04, 0.06, (sx * 0.246, -0.016, -0.625), seg=7, r=0.008, bevel_seg=1))
        top = Vector((sx * 0.04, surface_y(plain, sx * 0.04, -0.25) + 0.004, -0.25))
        end = Vector((sx * 0.048, surface_y(rings, sx * 0.048, -0.42) - 0.012, -0.42))
        mid = (top + end) / 2 + Vector((sx * 0.004, -0.008, 0))
        body.append(sweep(catmull([top, mid, end], 3), circle(0.005, 4)))
        acc.append(s.rcyl(0.0065, 0.03, end + Vector((0, 0, -0.012)), seg=6, r=0.0))
    s.part("Body", body)
    s.part("Accent", acc)


def model_garment_raincoat():
    """A belted raincoat, 0.95 m: an A-line skirt flaring to a fluted hem, a cape yoke over the shoulders, pointed
    collar flaps, a belt with a buckle (Accent) and its tail, snaps down the front, strapped cuffs."""
    prof = [(0.012, 0, 0, -0.004, 2, 0),
            (0.0, 0.035, 0.026, -0.006, 2.0, 0),
            (-0.03, 0.08, 0.04, -0.01, 2.2, 0),
            (-0.08, 0.15, 0.05, -0.012, 2.4, 0),
            (-0.14, 0.2, 0.056, -0.012, 2.6, 0),
            (-0.21, 0.222, 0.06, -0.012, 2.8, 0.002),
            (-0.34, 0.222, 0.06, -0.012, 3.0, 0.003),
            (-0.42, 0.186, 0.054, -0.012, 3.0, 0.0),
            (-0.5, 0.222, 0.06, -0.012, 3.0, 0.006),
            (-0.66, 0.285, 0.067, -0.012, 3.0, 0.012),
            (-0.82, 0.335, 0.07, -0.012, 3.0, 0.017),
            (-0.945, 0.37, 0.07, -0.012, 3.0, 0.02),
            (-0.92, 0.32, 0.055, -0.012, 3.0, 0.014)]
    rings = body_rings(prof, 18, crease=0.005, phase=1.1)
    body = [ring_loft(rings)]
    cape = [(0.0, 0.05, 0.036, -0.006, 2.0, 0),
            (-0.09, 0.172, 0.064, -0.012, 2.5, 0),
            (-0.17, 0.232, 0.073, -0.012, 2.8, 0.002),
            (-0.3, 0.252, 0.075, -0.012, 3.0, 0.004),
            (-0.285, 0.228, 0.064, -0.012, 3.0, 0)]
    crings = body_rings(cape, 18)
    body.append(ring_loft(crings))
    for sx in (-1, 1):                               # pointed collar flaps lying on the cape, tips down
        ys = surface_y(crings, sx * 0.065, -0.1)
        tri = [(sx * 0.018, -0.03), (sx * 0.112, -0.062), (sx * 0.055, -0.175)]
        flap = prism(tri if sx < 0 else tri[::-1], "Y", ys - 0.009, ys + 0.006)
        s.bevel_edges(flap, 0.003, 1, angle=30)
        body.append(flap)
    belt = []
    for k in range(16):
        ph = 2 * math.pi * k / 16
        a, b, cy = lerp_prof(prof, -0.42)
        belt.append(Vector(((a + 0.006) * math.cos(ph), cy + (b + 0.006) * math.sin(ph), -0.42)))
    body.append(sweep(belt, rrect(0.022, 0.006, 4, 6), up=(0, 0, 1), closed=True))
    yb = surface_y(rings, 0.0, -0.42)
    tail = [Vector((0.03, yb - 0.012, -0.425)), Vector((0.05, yb - 0.014, -0.47)), Vector((0.058, yb - 0.012, -0.56))]
    body.append(sweep(catmull(tail, 2), rrect(0.018, 0.005, 4, 6), up=(1, 0, 0)))
    sq = [Vector((x, yb - 0.016, -0.42 + z)) for x, z in ((-0.024, -0.024), (0.024, -0.024), (0.024, 0.024),
                                                          (-0.024, 0.024))]
    frame = sweep(sq, circle(0.0055, 4, 1.0, math.pi / 4), up=(0, 1, 0), closed=True)
    acc = [frame, s.rbox((0.008, 0.01, 0.05), (0, yb - 0.016, -0.42), r=0.002, seg=1)]
    acc += [stud(rings, -0.03, z, r=0.009, depth=0.007) for z in (-0.2, -0.56, -0.7)]
    for sx in (-1, 1):
        body.append(sleeve([(sx * 0.15, -0.012, -0.12), (sx * 0.21, -0.018, -0.22), (sx * 0.232, -0.022, -0.40),
                            (sx * 0.236, -0.026, -0.62)], 0.056, 0.05, cuff=0.004))
        body.append(s.rbox((0.07, 0.01, 0.024), (sx * 0.226, -0.026 - 0.046, -0.57), r=0.004, seg=1,
                           rot=(0, 0, -sx * 0.25)))                                         # the cuff strap
        acc.append(s.rbox((0.016, 0.008, 0.02), (sx * 0.248, -0.026 - 0.051, -0.57), r=0.002, seg=1))
    s.part("Body", body)
    s.part("Accent", acc)


def model_garment_scarf():
    """A knitted scarf folded over the hook: the fold bunched round the hook point, two tails 18 cm wide hanging
    down to 0.62 and 0.74 (the front one longer), two stripes near the ends, fringes."""
    body = []
    arc = []
    for k in range(9):
        ang = (200 - 220 * k / 8) * DEG
        arc.append(Vector((0.045 * math.cos(ang), 0.0, -0.034 + 0.045 * math.sin(ang))))
    body.append(sweep(arc, rrect(0.045, 0.02, 8), up=(0, 1, 0)))
    for cx, cy, length, ph, w in ((-0.022, -0.012, 0.74, 0.0, 0.062), (0.04, 0.014, 0.6, 1.7, 0.046)):
        rings = []
        zs = [-0.02, -0.06, -0.12, -0.2] + [-0.2 - (length - 0.2) * k / 6 for k in range(1, 7)]
        stripes = (length - 0.14, length - 0.11, length - 0.08, length - 0.05)
        zs = sorted(set(zs + [-z for z in stripes]), reverse=True)
        for z in zs:
            a = 0.03 + w * min(1.0, -z / 0.16)
            b = 0.011
            if stripes[0] <= -z <= stripes[1] or stripes[2] <= -z <= stripes[3]:
                a, b = a + 0.003, b + 0.004
            x0 = cx + 0.012 * math.sin(-z * 5 + ph)
            tw = 0.18 * math.sin(-z * 3.2 + ph)
            ring = []
            for x, y in rrect(a, b, 10):
                ring.append(Vector((x0 + x * math.cos(tw) - y * math.sin(tw), cy + x * math.sin(tw) + y * math.cos(tw), z)))
            rings.append(ring)
        body.append(ring_loft(rings))
        end = rings[-1]
        x_end = sum(p.x for p in end) / len(end)
        tw = 0.18 * math.sin(length * 3.2 + ph)
        for k in range(6):
            u = -0.07 + 0.14 * k / 5
            p = Vector((x_end + u * math.cos(tw), cy + u * math.sin(tw), -length - 0.025))
            body.append(s.rbox((0.014, 0.009, 0.06), p, r=0.0, rot=(0, 0, tw)))
    s.part("Body", body)


def model_garment_bag():
    """A shoulder bag hung by its strap: the strap over the hook (flat on the hook, turning to face front down the
    sides), a soft rounded bag 0.3 x 0.1 x 0.23 with a flap over the top and front, a buckle and the strap's
    D-rings (Accent)."""
    bag_z = -0.5
    body = [s.rbox((0.3, 0.1, 0.23), (0, 0, bag_z), r=0.03, seg=3, taper=(0.94, 0.86))]
    flap = catmull([(0, 0.035, bag_z + 0.112), (0, -0.03, bag_z + 0.116), (0, -0.052, bag_z + 0.09),
                    (0, -0.056, bag_z - 0.02)], 3)
    body.append(sweep(flap, rrect(0.143, 0.005, 10, 6), up=(1, 0, 0)))
    strap = catmull([(-0.15, 0.0, bag_z + 0.075), (-0.135, 0.0, -0.22), (-0.05, 0.0, -0.06), (0.0, 0.0, 0.004),
                     (0.05, 0.0, -0.06), (0.135, 0.0, -0.22), (0.15, 0.0, bag_z + 0.075)], 4)
    n = len(strap)

    def twist(t):
        return (math.pi / 2) * min(1.0, abs(t - 0.5) * 4)          # flat on the hook, facing front down the sides
    body.append(sweep(strap, rrect(0.017, 0.004, 8, 6), up=(0, 1, 0), twist=twist))
    acc = [s.rbox((0.045, 0.012, 0.034), (0, -0.062, bag_z - 0.005), r=0.004, seg=1)]
    for sx in (-1, 1):
        acc.append(s.rbox((0.012, 0.03, 0.03), (sx * 0.147, 0.0, bag_z + 0.095), r=0.004, seg=1))
    s.part("Body", body)
    s.part("Accent", acc)
    return n


def model_garment_umbrella():
    """A closed umbrella hung by its crook: the crook (Body) round the hook point, the steel shaft (Accent), the
    furled canopy — eight ribs, folds twisting round, widest low down — with a strap and snap, the ferrule (Accent)."""
    R, rh = 0.038, 0.011
    cz = rh - R + 0.0                                # the crook's inner top touches the hook point
    crook = []
    for k in range(11):
        ang = (205 - 205 * k / 10) * DEG
        crook.append(Vector((R * math.cos(ang), 0, cz + R * math.sin(ang))))
    crook += [Vector((R, 0, cz - 0.04)), Vector((R, 0, cz - 0.1))]
    body = [sweep(crook, circle(rh, 8), up=(0, 1, 0))]
    body.append(s.ball(rh * 1.05, crook[0], seg=10))
    ax = R
    acc = [s.rcyl(0.006, 0.2, (ax, 0, cz - 0.19), seg=8, r=0.001, bevel_seg=1)]
    acc.append(s.rcyl(0.0145, 0.02, (ax, 0, cz - 0.105), seg=12, r=0.003, bevel_seg=1))
    prof = [(-0.215, 0.009), (-0.25, 0.02), (-0.33, 0.034), (-0.46, 0.046), (-0.6, 0.05), (-0.7, 0.042),
            (-0.77, 0.024), (-0.8, 0.008)]
    rings = []
    for z, r in prof:
        ring = []
        for j in range(16):
            ang = 2 * math.pi * j / 16 + (-z) * 1.4
            rr = r * (1.0 if j % 2 == 0 else 0.74)
            ring.append(Vector((ax + rr * math.cos(ang), rr * math.sin(ang), z)))
        rings.append(ring)
    body.append(ring_loft(rings))
    band = [Vector((ax + 0.047 * math.cos(2 * math.pi * k / 14), 0.047 * math.sin(2 * math.pi * k / 14), -0.47))
            for k in range(14)]
    body.append(sweep(band, rrect(0.013, 0.004, 6), up=(0, 0, 1), closed=True))
    acc.append(s.rcyl(0.008, 0.008, (ax, -0.05, -0.47), axis="Y", seg=10, r=0.002, bevel_seg=1))
    acc.append(s.rcyl(0.0035, 0.07, (ax, 0, -0.82), seg=8, r=0.002, bevel_seg=1, radius2=0.0075))
    s.part("Body", body)
    s.part("Accent", acc)


def model_garment_cap():
    """A baseball cap hung by its back strap: a hollow six-panel crown (seams pressed in, a top button) tilted so it
    bulges to the front, the curved visor pointing down and out, the strap buckle (Accent) on the hook."""
    a0, b0 = 0.088, 0.097                     # the opening (head size)
    seg = 18
    shell = []
    outer = [(0.0, 1.0), (0.03, 0.99), (0.06, 0.92), (0.082, 0.76), (0.097, 0.5), (0.103, 0.15)]
    inner = [(0.094, 0.13), (0.087, 0.47), (0.073, 0.73), (0.05, 0.89), (0.022, 0.965), (0.004, 0.985)]
    for k, (z, f) in enumerate(outer + inner):
        ring = []
        for j in range(seg):
            th = 2 * math.pi * j / seg
            seam = 0.975 if (j % 3 == 0 and 0 < k < len(outer) - 1) else 1.0    # six panels: seams pressed in
            front = 1.0 + 0.06 * max(0.0, -math.sin(th)) * (z / 0.116)        # the front panels stand taller
            ring.append(Vector((a0 * f * seam * math.cos(th), b0 * f * seam * math.sin(th), z * front)))
        shell.append(ring)
    bm = bmesh.new()
    loops = [[bm.verts.new(p) for p in r] for r in shell]
    for lo, up in zip(loops, loops[1:] + [loops[0]]):
        for j in range(seg):
            bm.faces.new((lo[j], lo[(j + 1) % seg], up[(j + 1) % seg], up[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    body = [bm, s.rcyl(0.014, 0.02, (0, 0, 0.102), seg=10, r=0.006, bevel_seg=1)]
    rings = []
    for y, hw in ((-0.07, 0.08), (-0.097, 0.088), (-0.118, 0.087), (-0.137, 0.08), (-0.152, 0.066), (-0.164, 0.045),
                  (-0.171, 0.02)):
        ring = []
        for x, zz in rrect(hw, 0.0045, 16, 6):
            bend = -1.8 * x * x - 0.2 * max(0.0, -0.097 - y)
            ring.append(Vector((x, y, 0.005 + zz + bend)))
        rings.append(ring)
    body.append(ring_loft(rings))
    acc = [s.rbox((0.032, 0.012, 0.018), (0, b0 - 0.003, 0.016), r=0.003, seg=1)]
    s.part("Body", body, sharp_angle=60)
    s.part("Accent", acc)
    # hang it by the strap: the back (+Y) up, the crown to the front (-Y), the visor down and a little out
    rot = Matrix.Rotation(42 * DEG, 4, "X")
    hook = rot @ Vector((0, 0.985 * b0, 0.006))  # the strap's inside edge
    mat = Matrix.Rotation(YAW_CAP * DEG, 4, "Z") @ Matrix.Translation(-hook) @ rot
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            o.data.transform(mat)
            o.data.update()


# --------------------------------------------------------------------------- books

BOOKS = [  # height, thickness, depth, spine ridges, paperback
    (0.24, 0.035, 0.17, 0, False),
    (0.30, 0.05, 0.22, 4, False),
    (0.19, 0.022, 0.13, 0, True),
    (0.27, 0.06, 0.20, 2, False),
    (0.32, 0.028, 0.24, 0, False),
    (0.21, 0.042, 0.15, 3, False),
]


def book(h, t, d, ridges=0, paper=False, simple=False):
    """An upright book (origin bottom centre, spine to -Y): the cover as one C-shaped prism (two boards and a rounded
    spine) and the page block inset at top, bottom and fore-edge. Returns (cover bms, page bms)."""
    c = 0.0016 if paper else max(0.0022, min(0.0035, t * 0.07))       # board thickness
    o = 0.0008 if paper else 0.003                                    # overhang of the boards over the pages
    bulge = min(t * 0.15, 0.007)
    y0, y1 = -d / 2, d / 2
    ys = y0 + bulge                                                   # where the spine's curve meets the boards
    ti = t / 2 - c
    bi = max(0.0015, bulge - c)
    g = 0.0 if (paper or simple) else 0.6 * c                         # the joint: a groove along the spine's hinge
    arcs = 4 if simple else 6
    outer, inner = [(t / 2, y1)], [(ti, y1)]
    joint = [] if simple else [0.008, 0.0055, 0.003]
    for k, dy in enumerate(joint):
        outer.append((t / 2 - (g if k == 1 else 0.0), ys + dy))
        inner.append((ti, ys + dy))
    for k in range(arcs + 1):
        a = math.pi * k / arcs                                        # 0 .. pi, right to left over the spine
        outer.append((t / 2 * math.cos(a), ys - bulge * math.sin(a)))
        inner.append((ti * math.cos(a), ys - bi * math.sin(a)))
    for k, dy in enumerate(reversed(joint)):
        outer.append((-t / 2 + (g if k == 1 else 0.0), ys + dy))
        inner.append((-ti, ys + dy))
    outer.append((-t / 2, y1))
    inner.append((-ti, y1))
    covers = [strip_prism(outer, inner, 0.0, h)]
    for k in range(ridges):
        z = h * (0.18 + 0.64 * k / max(1, ridges - 1))
        arc = [Vector((0.985 * t / 2 * math.cos(math.pi * j / 4), ys - (bulge + 0.0012) * math.sin(math.pi * j / 4), z))
               for j in range(1, 4)]
        arc = [Vector((0.97 * t / 2, ys - 0.0004, z))] + arc + [Vector((-0.97 * t / 2, ys - 0.0004, z))]
        covers.append(sweep(arc, rrect(0.0028, 0.0018, 4, 2.5), up=(0, 0, 1)))
    pages = s.rbox((t - 2 * c + 0.002, y1 - o - (ys + 0.002), h - 2 * o), (0, (y1 - o + ys + 0.002) / 2, h / 2),
                   r=0.0)
    return covers, [pages]


def model_book(k):
    h, t, d, ridges, paper = BOOKS[k]
    covers, pages = book(h, t, d, ridges, paper)
    printed.paint(s.part("Body", covers), printed.spine_rule(k, h, t))
    s.part("Accent", pages, sharp_angle=30)
    name = f"book_{k}"
    info = export(name, floor=True)
    info.update({"width": t, "height": h, "depth": d})
    CONTRACT["books"][name] = info


def model_book_lean():
    """A book leaning 17 degrees towards Blender +X (Unity -X): origin on the bottom edge it pivots on; its top corner
    rests on the neighbour standing at Unity x = -h sin(17)."""
    h, t, d, lean = 0.25, 0.032, 0.18, 17.0
    covers, pages = book(h, t, d, 2, False)
    mat = Matrix.Rotation(lean * DEG, 4, "Y") @ Matrix.Translation((-t / 2, 0, 0))
    for bm in covers + pages:
        transform(bm, mat)
    printed.paint(s.part("Body", covers), printed.spine_rule(6, h, t, mat.inverted()))
    s.part("Accent", pages, sharp_angle=30)
    info = export("book_lean", floor=True)
    info.update({"width": t, "height": h, "depth": d, "lean_deg": lean, "lean_dir": [-1.0, 0.0, 0.0],
                 "touch_point": [round(-h * math.sin(lean * DEG), 4), round(h * math.cos(lean * DEG), 4), 0.0],
                 "note": "origin = the bottom edge it pivots on; the top corner touch_point leans on the last book "
                         "of the row, whose side face is at Unity x = touch_point.x"})
    CONTRACT["books"]["book_lean"] = info


def model_book_stack():
    """Four books lying flat, largest at the bottom, each a little offset and turned, spines to -Y."""
    specs = [(0.29, 0.042, 0.21, 0.0, 0.0, 0), (0.26, 0.03, 0.18, 0.014, 4.0, 0), (0.24, 0.036, 0.17, -0.01, -3.0, 0),
             (0.2, 0.024, 0.14, 0.006, 6.0, 0)]
    covers_all, pages_all, rules = [], [], []
    z = 0.0
    for h, t, d, dx, yaw, ridges in specs:
        covers, pages = book(h, t, d, ridges, False, simple=True)
        mat = (Matrix.Translation((dx, 0, z + t / 2)) @ Matrix.Rotation(yaw * DEG, 4, "Z") @
               Matrix.Translation((h / 2, 0, 0)) @ Matrix.Rotation(-math.pi / 2, 4, "Y"))
        for bm in covers + pages:
            transform(bm, mat)
        covers_all += covers
        pages_all += pages
        rules.append((z + t, printed.spine_rule(len(rules), h, t, mat.inverted())))
        z += t - 0.0005                  # a hair of overlap, clear of the page block under the board
    printed.paint(s.part("Body", covers_all), lambda n, c, co: next(r for top, r in rules if c.z <= top + 0.002)(n, c, co))
    printed.swap_uv(s.part("Accent", pages_all, sharp_angle=30))
    info = export("book_stack", floor=True)
    lo, hi = info["bounds_min"], info["bounds_max"]
    info.update({"width": round(hi[0] - lo[0], 3), "height": round(hi[1] - lo[1], 3), "depth": round(hi[2] - lo[2], 3)})
    CONTRACT["books"]["book_stack"] = info


# --------------------------------------------------------------------------- run

MODELS = {
    "coat_stand_bare": model_coat_stand_bare,
    "hall_hooks_bare": model_hall_hooks_bare,
    "garment_coat_long": lambda: garment("garment_coat_long", model_garment_coat_long),
    "garment_jacket": lambda: garment("garment_jacket", model_garment_jacket),
    "garment_hoodie": lambda: garment("garment_hoodie", model_garment_hoodie),
    "garment_raincoat": lambda: garment("garment_raincoat", model_garment_raincoat),
    "garment_scarf": lambda: garment("garment_scarf", model_garment_scarf),
    "garment_bag": lambda: garment("garment_bag", model_garment_bag),
    "garment_umbrella": lambda: garment("garment_umbrella", model_garment_umbrella),
    "garment_cap": lambda: garment("garment_cap", model_garment_cap),
    **{f"book_{k}": (lambda k=k: model_book(k)) for k in range(len(BOOKS))},
    "book_lean": model_book_lean,
    "book_stack": model_book_stack,
}


def write_contract():
    data = {}
    if os.path.exists(CONTRACT_PATH):
        with open(CONTRACT_PATH, encoding="utf-8") as f:
            data = json.load(f)
    data["about"] = ("Variant models in Assets/_Game/Art/Models/Props/Variants (Tools/Blender/variants.py). Unity "
                     "axes (x right, y up, z = the model's front), metres from the model origin. holders: hook points "
                     "(where a garment's origin goes) and the horizontal direction each hook points (turn the "
                     "garment's +Z to it). garments: bounds relative to the hook point with the garment facing +Z. "
                     "books: width = thickness along the shelf (Unity x), height (y), depth (z); spine faces +Z.")
    for key in ("holders", "garments", "books"):
        data.setdefault(key, {}).update(CONTRACT[key])
    with open(CONTRACT_PATH, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    print("contract", CONTRACT_PATH)


if __name__ == "__main__":
    s.clear_scene()
    for name, fn in MODELS.items():
        if ONLY is None or name in ONLY:
            fn()
    write_contract()
    print("DONE")
