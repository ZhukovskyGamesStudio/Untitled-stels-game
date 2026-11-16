"""Furniture of the body rooms (round 2): the gym (rowing machine, power cage, kettlebell rack, mat rack, cable
machine), the locker room (towel shelf, laundry cart, double-sided bench, column scale, shoe rack), the infirmary
(examination couch, wheelchair, drip stand, doctor's desk, wall AED box) and the nap room (bunk bed, daybed, curtain
divider, floor cushions, night table).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms2_body.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor [--focus x,y,z,r] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y (Unity z min), z up,
origin on the floor in the middle of the footprint. The wall AED box keeps its real height (1.2..1.6) with its back
flush with the footprint's back. Object names are material roles: Body, Accent, Trim, Eye (glows).
Forms come from shapes.py and are finished by shapes.part; export() goes through meshfix (floor=True) like poi.export.
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --)
import meshfix  # noqa: E402
import shapes as s  # noqa: E402


def export(filename):
    """meshfix (furniture stands on the floor), then the FBX into <root>/Props/; prints the bounding box in Unity terms."""
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=filename)
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    tris = 0
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
        tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
    print(f"BBOX {filename}: x {lo.x:.3f}..{hi.x:.3f}  z(unity) {-hi.y:.3f}..{-lo.y:.3f}  y {lo.z:.3f}..{hi.z:.3f}"
          f"  tris~{tris}")
    path = os.path.join(g.out_dir, "Props", filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def on(bms, loc=(0, 0, 0), rot=(0, 0, 0)):
    """Place every bmesh of a group built around the origin."""
    for b in bms:
        s.place(b, loc, rot)
    return bms


def bar(a, b, radius, seg=12):
    """Straight round bar between two points, chamfered ends."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = s.rcyl(radius, d.length, (0, 0, 0), seg=seg, r=radius * 0.3, bevel_seg=1)
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    bm.transform(rot)
    return s.place(bm, (a + b) / 2)


def torus(R, r, loc=(0, 0, 0), axis="Z", seg=24, rseg=8):
    """Ring of round section (tyre, hand rim, curtain ring) around the given axis."""
    bm = bmesh.new()
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        c = Vector((math.cos(a), math.sin(a), 0))
        rings.append([bm.verts.new(c * (R + r * math.cos(2 * math.pi * j / rseg))
                                   + Vector((0, 0, r * math.sin(2 * math.pi * j / rseg)))) for j in range(rseg)])
    for i in range(seg):
        A, B = rings[i], rings[(i + 1) % seg]
        for j in range(rseg):
            bm.faces.new((A[j], B[j], B[(j + 1) % rseg], A[(j + 1) % rseg]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rot = {"Z": (0, 0, 0), "X": (0, math.pi / 2, 0), "Y": (math.pi / 2, 0, 0)}[axis]
    return s.place(bm, loc, rot)


def prism(pts, axis, lo, hi, bevel=0.0):
    """Convex-or-not polygon (pts in the two other axes, in X-Y-Z order) extruded along axis from lo to hi."""
    bm = bmesh.new()

    def co(t, u, v):
        return {"X": (t, u, v), "Y": (u, t, v), "Z": (u, v, t)}[axis]

    a = [bm.verts.new(co(lo, u, v)) for u, v in pts]
    b = [bm.verts.new(co(hi, u, v)) for u, v in pts]
    n = len(pts)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bm.faces.new(a)
    bm.faces.new(b[::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    if bevel:
        s.bevel_edges(bm, bevel, 1, angle=30)
    return bm


def seams(bm, axis, values, width=0.006, depth=0.003):
    """Joint lines (weight plates, folded towels): a V-groove at each value along axis."""
    i = "XYZ".index(axis)
    no = Vector((0, 0, 0))
    no[i] = 1.0
    for value in values:
        for off in (-width / 2, 0.0, width / 2):
            co = Vector((0, 0, 0))
            co[i] = value + off
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no)
        bm.normal_update()
        band = [v for v in bm.verts if abs(v.co[i] - value) <= 1e-5]
        moves = [(v, v.normal.copy()) for v in band]
        for v, n in moves:
            n[i] = 0.0
            if n.length > 1e-6:
                v.co -= n.normalized() * depth
    return bm


def caster(x, y, r, w, top_z, yaw=0.0):
    """A swivel caster with its tyre on the floor: (tyre, [fork, cheeks, stem]) — the stem reaches up to top_z."""
    wheel = s.rcyl(r, w, (0, 0, r), axis="X", seg=14, r=min(0.006, r * 0.25), bevel_seg=1)
    fork = [s.rbox((w + 0.016, r * 1.2, r * 0.5), (0, 0, r * 2.05), r=0.004, seg=1)]
    for sx in (-1, 1):
        fork.append(s.rbox((0.008, r * 1.0, r * 1.25), (sx * (w / 2 + 0.002), 0, r * 1.45), r=0.002, seg=1))
    stem_lo = r * 2.2
    if top_z > stem_lo:
        fork.append(s.rcyl(min(0.012, r * 0.35), top_z - stem_lo + 0.01, (0, 0, (top_z + stem_lo) / 2), seg=10,
                           r=0.002, bevel_seg=1))
    on([wheel], (x, y, 0), (0, 0, yaw))
    on(fork, (x, y, 0), (0, 0, yaw))
    return wheel, fork


def frame(size, loc, border, r=0.006, seg=2):
    """A rectangular bezel open along Y: a rounded box with a through-hole, border wide."""
    outer = s.rbox(size, loc, r=r, seg=seg)
    cut = s.rbox((size[0] - 2 * border, size[1] + 0.1, size[2] - 2 * border), loc, r=0)
    bm = s.boolean(outer, cut, "DIFFERENCE")
    s.bevel_edges(bm, min(r, border * 0.3), 1, angle=50)
    return bm


def bar_pull(x, z, y_face, length, axis="X", r=0.007, stand=0.02):
    """A D-shaped bar pull on two stand-offs, its back sunk 3 mm into the face at y_face (front = -y)."""
    y_bar = y_face - stand
    out = [s.rcyl(r, length, (x, y_bar, z), axis=axis, seg=10, r=r * 0.4, bevel_seg=1)]
    for d in (-1, 1):
        p = (x + d * (length / 2 - 0.015), y_face - stand / 2 + 0.0015, z) if axis == "X" else \
            (x, y_face - stand / 2 + 0.0015, z + d * (length / 2 - 0.015))
        out.append(s.rcyl(r * 0.75, stand + 0.003, p, axis="Y", seg=8, r=0.002, bevel_seg=1))
    return out


def plate(R, t, seg=24):
    """A weight plate around +Z, centred on the origin: rolled rim, a recessed face and a raised hub on +Z."""
    h = t / 2
    prof = [(0.0, -h), (R - 0.01, -h), (R, -h + 0.007), (R, h - 0.007), (R - 0.02, h),
            (R - 0.03, h - 0.007), (0.07, h - 0.007), (0.05, h + 0.004), (0.0, h + 0.004)]
    return s.lathe(prof, seg=seg)


def orient(bm, direction):
    """A shape built around +Z turned to point along direction (about the origin)."""
    q = Vector((0, 0, 1)).rotation_difference(Vector(direction).normalized())
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    return bm


def pillow(w, d, h, n=3.5, seg=32):
    """A plump cushion on the floor (bottom at z 0): bulging sides, a soft domed top."""
    a, b = w / 2, d / 2
    rings = [(0.0, a - 0.04, b - 0.04, n), (0.015, a - 0.012, b - 0.012, n), (h * 0.45, a, b, n),
             (h * 0.8, a - 0.015, b - 0.015, n), (h * 0.95, a - 0.06, b - 0.06, n),
             (h, a - 0.16, b - 0.16, n)]
    return s.loft(rings, seg=seg)


# =========================================================================== gym

def model_rowing_machine():
    """Air rower 2.1 m along z: a flywheel housing with a grille face at the front (Blender -y) on a pedestal and a
    front foot, a monorail sloping down to a rear foot, a seat on a carriage, two angled footrests with straps, the
    handle parked on the housing on its chain, and the monitor on an arm facing the seat."""
    body = []
    body.append(s.rbox((0.6, 0.08, 0.045), (0, -0.92, 0.0325), r=0.015, seg=2))          # front foot
    body.append(s.rbox((0.5, 0.08, 0.05), (0, 0.99, 0.025), r=0.015, seg=2))            # rear foot
    body.append(s.rbox((0.08, 0.06, 0.26), (0, 0.975, 0.16), r=0.012, seg=1))           # rear post
    body.append(s.rbox((0.16, 0.34, 0.26), (0, -0.74, 0.16), r=0.03, seg=2, taper=(0.8, 0.9)))  # pedestal
    th = math.atan2(0.07, 1.45)
    body.append(s.rbox((0.075, 1.48, 0.05), (0, 0.255, 0.325), r=0.014, seg=2, rot=(-th, 0, 0)))  # monorail
    body.append(s.rbox((0.14, 0.24, 0.05), (0, 0.25, 0.37), r=0.012, seg=1))             # seat carriage
    body.append(s.rbox((0.32, 0.2, 0.12), (0, -0.44, 0.4), r=0.02, seg=2))                # footrest bracket
    for x in (-0.1, 0.1):
        foot = [s.rbox((0.12, 0.28, 0.02), (0, 0, 0), r=0.006, seg=1)]
        foot.append(s.rbox((0.13, 0.03, 0.05), (0, 0.14, 0.02), r=0.006, seg=1))           # heel cup
        body += on(foot, (x, -0.38, 0.5), (-math.radians(55), 0, 0))
    body.append(s.tube([(0, -0.8, 0.66), (0, -0.74, 0.76), (0, -0.66, 0.8)], 0.016, segments_per=6))  # monitor arm
    body.append(s.rbox((0.2, 0.05, 0.15), (0, -0.63, 0.82), r=0.015, seg=2))
    s.part("Body", body)

    acc = []
    housing = s.rcyl(0.27, 0.22, (0, -0.76, 0.42), axis="X", seg=32, r=0.03, bevel_seg=2)
    for d in (1, -1):
        s.inset_faces(housing, s.facing((d, 0, 0), min_area=0.05, tol=0.95), thickness=0.035, depth=-0.012)
    acc.append(housing)
    for d in (-1, 1):
        acc.append(s.rcyl(0.06, 0.03, (d * 0.1, -0.76, 0.42), axis="X", seg=16, r=0.008, bevel_seg=1))  # hubs
    acc.append(s.rbox((0.56, 0.035, 0.035), (0, -0.475, 0.62), r=0.012, seg=2))         # handle
    acc.append(bar((0, -0.47, 0.62), (0, -0.56, 0.55), 0.008, seg=8))                    # chain into the housing
    s.part("Accent", acc)

    trim = [s.rbox((0.3, 0.32, 0.06), (0, 0.25, 0.42), r=0.025, seg=3, taper=(0.95, 0.95))]  # seat
    for x in (-0.28, 0.28):
        trim.append(s.rcyl(0.02, 0.1, (x * 0.89, -0.475, 0.62), axis="X", seg=12, r=0.006, bevel_seg=1))  # grips
    for x in (-0.1, 0.1):
        strap = [s.rbox((0.135, 0.05, 0.025), (0, 0.04, 0.02), r=0.008, seg=1)]
        trim += on(strap, (x, -0.38, 0.5), (-math.radians(55), 0, 0))
    for x in (-0.27, 0.27):
        trim.append(s.rbox((0.06, 0.085, 0.02), (x * 1.0, -0.92, 0.008), r=0.006, seg=1))   # rubber feet
    s.part("Trim", trim)

    s.part("Eye", [s.rbox((0.16, 0.008, 0.11), (0, -0.606, 0.82), r=0.003, seg=1)])
    return export("rowing_machine")


def model_power_cage():
    """Squat cage 1.4 x 1.4 x 2.2: four uprights on floor runners, a top frame with a pull-up bar at the front,
    J-hooks holding a loaded barbell, safety arms inside, and plate horns on the back uprights with stored plates."""
    body = []
    Y, BY = 0.4, -0.47  # uprights at y = +-Y, the barbell racked in front of the front pair
    for x in (-0.47, 0.47):
        body.append(s.rbox((0.08, 1.4, 0.05), (x, 0, 0.025), r=0.012, seg=2))            # floor runners
        for y in (-Y, Y):
            body.append(s.rbox((0.07, 0.07, 2.19), (x, y, 1.105), r=0.01, seg=2))        # uprights
        body.append(s.rbox((0.07, 2 * Y + 0.07, 0.07), (x, 0, 2.15), r=0.01, seg=2))     # top side members
        body.append(s.rbox((0.05, 2 * Y + 0.2, 0.05), (x * 0.894, 0, 0.78), r=0.008, seg=1))  # safety arms
        body.append(s.rbox((0.08, 0.1, 0.04), (x, -0.46, 1.33), r=0.008, seg=1))          # J-hook arm
        body.append(s.rbox((0.08, 0.025, 0.06), (x, -0.5, 1.36), r=0.008, seg=1))          # J-hook lip
        body.append(bar((x, Y - 0.02, 0.42), (x, 0.66, 0.42), 0.022, seg=12))              # plate horn
    for y in (-Y, Y):
        body.append(s.rbox((1.0, 0.07, 0.07), (0, y, 2.145), r=0.01, seg=2))             # top cross members
        body.append(s.rbox((0.88, 0.07, 0.045), (0, y, 0.0275), r=0.01, seg=2))          # floor cross bars
    for x in (-0.47, 0.47):
        body.append(s.rbox((0.07, 0.14, 0.05), (x, -0.46, 2.12), r=0.01, seg=1))          # pull-up brackets
    body.append(bar((-0.53, -0.51, 2.12), (0.53, -0.51, 2.12), 0.017, seg=12))           # pull-up bar
    s.part("Body", body)

    acc = []
    for sx in (1, -1):
        for R, t, x in ((0.225, 0.045, 0.565), (0.16, 0.032, 0.61)):
            p = plate(R, t)
            orient(p, (sx, 0, 0))
            acc.append(s.place(p, (sx * x, BY, 1.362)))
        p = plate(0.225, 0.045)                                                              # stored on the horn
        orient(p, (0, 1, 0))
        acc.append(s.place(p, (sx * 0.47, 0.56, 0.42)))
    s.part("Accent", acc)

    trim = [bar((-0.52, BY, 1.362), (0.52, BY, 1.362), 0.014, seg=12)]                   # shaft
    for sx in (-1, 1):
        trim.append(s.rcyl(0.026, 0.19, (sx * 0.61, BY, 1.362), axis="X", seg=14, r=0.004, bevel_seg=1))  # sleeve
        trim.append(s.rcyl(0.036, 0.025, (sx * 0.52, BY, 1.362), axis="X", seg=14, r=0.005, bevel_seg=1))  # collar
        trim.append(s.rcyl(0.04, 0.03, (sx * 0.645, BY, 1.362), axis="X", seg=14, r=0.006, bevel_seg=1))  # clamp
    s.part("Trim", trim)
    return export("power_cage")


def kettlebell(k):
    """A kettlebell of scale k standing at the origin: a flat-bottomed bell and an arched handle, (bell, handle)."""
    bell = s.lathe([(0.0, 0.0), (0.055 * k, 0.0), (0.068 * k, 0.006 * k), (0.096 * k, 0.05 * k),
                    (0.104 * k, 0.1 * k), (0.094 * k, 0.148 * k), (0.07 * k, 0.183 * k), (0.035 * k, 0.2 * k),
                    (0.0, 0.205 * k)], seg=16)
    handle = s.tube([(-0.062 * k, 0, 0.16 * k), (-0.07 * k, 0, 0.25 * k), (-0.035 * k, 0, 0.295 * k),
                     (0.035 * k, 0, 0.295 * k), (0.07 * k, 0, 0.25 * k), (0.062 * k, 0, 0.16 * k)],
                    0.015 * k, res=1, segments_per=3)
    return bell, handle


def model_kettlebell_rack():
    """Two-tier kettlebell rack 1.0 x 0.4: stepped steel side plates, a low front shelf with three heavy bells and a
    high back shelf with four lighter ones, lips at the shelf fronts, rubber feet."""
    side = [(-0.2, 0.012), (0.2, 0.012), (0.2, 0.52), (0.02, 0.52), (-0.02, 0.24), (-0.2, 0.24)]
    body = []
    for x in (-0.485, 0.485):
        body.append(prism(side, "X", x - 0.015, x + 0.015, bevel=0.006))
    body.append(s.rbox((0.95, 0.205, 0.03), (0, -0.095, 0.2), r=0.008, seg=1))           # low shelf
    body.append(s.rbox((0.95, 0.2, 0.03), (0, 0.1, 0.495), r=0.008, seg=1))               # high shelf
    body.append(s.rbox((0.95, 0.025, 0.05), (0, -0.19, 0.225), r=0.008, seg=1))           # lips
    body.append(s.rbox((0.95, 0.025, 0.05), (0, 0.01, 0.52), r=0.008, seg=1))
    body.append(s.rbox((0.95, 0.04, 0.06), (0, 0.17, 0.06), r=0.008, seg=1))              # back stretcher
    s.part("Body", body)
    bells, handles = [], []
    for x in (-0.3, 0.0, 0.3):
        b, h = kettlebell(1.05)
        bells += on([b], (x, -0.085, 0.212))
        handles += on([h], (x, -0.085, 0.212))
    for x in (-0.33, -0.11, 0.11, 0.33):
        b, h = kettlebell(0.85)
        bells += on([b], (x, 0.1, 0.507))
        handles += on([h], (x, 0.1, 0.507))
    s.part("Accent", bells)
    for x in (-0.485, 0.485):
        for y in (-0.17, 0.17):
            handles.append(s.rbox((0.04, 0.05, 0.02), (x, y, 0.008), r=0.005, seg=1))
    s.part("Trim", handles)
    return export("kettlebell_rack")


def mat_roll(L, r):
    """A rolled yoga mat along X centred on the origin: the end shows the layers as two stepped rings."""
    bm = s.rcyl(r, L, (0, 0, 0), axis="X", seg=16, r=0.01, bevel_seg=1)
    for d in (1, -1):
        s.inset_faces(bm, s.facing((d, 0, 0), min_area=r * r, tol=0.95), thickness=r * 0.3, depth=-0.005)
        s.inset_faces(bm, s.facing((d, 0, 0), min_area=r * r * 0.3, tol=0.95), thickness=r * 0.3, depth=-0.005)
    return bm


def model_mat_rack():
    """Mat stand 0.8 x 0.5 x 1.2: four posts, three shelves and a top board, end rails keeping the rolls in; rolled
    mats lie across (two on each shelf, a third on top of the middle pair), each with a strap."""
    body = []
    for x in (-0.38, 0.38):
        for y in (-0.23, 0.23):
            body.append(s.rbox((0.04, 0.04, 1.18), (x, y, 0.59), r=0.008, seg=1))
        for z in (0.17, 0.55, 0.93):
            body.append(s.rbox((0.03, 0.44, 0.03), (x, 0, z), r=0.008, seg=1))           # end rails
    for z in (0.07, 0.45, 0.83):
        body.append(s.rbox((0.78, 0.48, 0.025), (0, 0, z), r=0.008, seg=1))
    body.append(s.rbox((0.8, 0.5, 0.03), (0, 0, 1.185), r=0.01, seg=2))                   # top board
    s.part("Body", body)

    rolls, straps = [], []
    r, L = 0.074, 0.62
    layout = [(0.0825, False), (0.4625, True), (0.8425, False)]
    for k, (top, third) in enumerate(layout):
        cz = top + r - 0.003
        spots = [(-0.115, cz), (0.115, cz)]
        if third:
            spots.append((0.0, cz + math.sqrt((2 * r) ** 2 - 0.115 ** 2) - 0.004))
        for j, (y, z) in enumerate(spots):
            dx = 0.02 * ((k + j) % 3 - 1)
            rolls.append(s.place(mat_roll(L, r), (dx, y, z)))
            for sx in (-0.17, 0.17):
                straps.append(torus(r + 0.0015, 0.006, (dx + sx, y, z), axis="X", seg=12, rseg=4))
    s.part("Accent", rolls)
    s.part("Trim", straps)
    return export("mat_rack")


def model_cable_machine():
    """Dual adjustable pulley tower 1.2 x 1.0 x 2.2: an H base, two side columns with pulley carriages and D-handles,
    a top beam with a pull-up bar, a weight stack on guide rods behind a shroud with a window, cables."""
    body = []
    for x in (-0.5, 0.5):
        body.append(s.rbox((0.1, 1.0, 0.06), (x, 0, 0.03), r=0.012, seg=2))              # base runners
        body.append(s.rbox((0.08, 0.08, 2.14), (x * 1.04, -0.05, 1.1), r=0.01, seg=2))      # side columns
        body.append(s.rbox((0.12, 0.12, 0.18), (x * 1.04, -0.05, 1.2), r=0.012, seg=2))     # carriages
        body.append(s.rbox((0.05, 0.08, 0.08), (x * 1.04, -0.13, 1.16), r=0.008, seg=1))    # pulley brackets
        body.append(s.rbox((0.06, 0.06, 2.11), (x * 0.5, 0.4, 1.065), r=0.008, seg=1))      # stack frame posts
        body.append(s.rbox((0.07, 0.2, 0.06), (x * 1.04, -0.2, 2.12), r=0.008, seg=1))      # pull-up brackets
    body.append(s.rbox((1.0, 0.1, 0.05), (0, 0.4, 0.025), r=0.012, seg=2))                # base cross member
    body.append(s.rbox((1.2, 0.12, 0.1), (0, -0.05, 2.15), r=0.015, seg=2))               # top beam
    body.append(s.rbox((0.58, 0.55, 0.06), (0, 0.17, 2.13), r=0.012, seg=2))              # top plate over the stack
    body.append(bar((-0.56, -0.28, 2.1), (0.56, -0.28, 2.1), 0.017, seg=12))              # pull-up bar
    for x in (-0.13, 0.13):
        body.append(bar((x, 0.25, 0.02), (x, 0.25, 2.11), 0.011, seg=10))                 # guide rods
    s.part("Body", body)

    acc = []
    stack = s.rbox((0.34, 0.18, 0.64), (0, 0.25, 0.42), r=0.01, seg=1)
    seams(stack, "Z", [0.14 + 0.04 * k for k in range(14)], width=0.006, depth=0.004)
    acc.append(stack)
    acc.append(s.rbox((0.3, 0.14, 0.08), (0, 0.25, 0.045), r=0.01, seg=1))                # bumper base
    acc.append(s.rbox((0.36, 0.19, 0.05), (0, 0.25, 0.76), r=0.01, seg=1))                # top plate of the stack
    shroud = s.rbox((0.62, 0.025, 2.09), (0, 0.13, 1.065), r=0.01, seg=2)
    shroud = s.boolean(shroud, s.rbox((0.38, 0.1, 0.75), (0, 0.13, 0.5), r=0.03, seg=2), "DIFFERENCE")
    s.bevel_edges(shroud, 0.003, 1, angle=50)
    acc.append(shroud)
    for x in (-0.52, 0.52):
        acc.append(s.rcyl(0.055, 0.03, (x, -0.16, 1.16), axis="X", seg=18, r=0.006, bevel_seg=1))   # pulleys
        acc.append(s.rcyl(0.06, 0.035, (x, -0.05, 2.07), axis="X", seg=18, r=0.006, bevel_seg=1))   # top pulleys
    s.part("Accent", acc)

    trim = []
    for x in (-0.52, 0.52):
        trim.append(bar((x, -0.2, 1.14), (x, -0.22, 0.98), 0.005, seg=6))                  # cable to the handle
        for d in (-1, 1):
            trim.append(s.tube([(x + d * 0.006, -0.22, 0.983), (x + d * 0.045, -0.22, 0.95), (x + d * 0.06, -0.22, 0.89)],
                               0.008, res=1, segments_per=4))                                # D-handle sides
        trim.append(s.rcyl(0.016, 0.13, (x, -0.22, 0.89), axis="X", seg=10, r=0.004, bevel_seg=1))  # grip
        trim.append(bar((x * 0.98, -0.09, 1.3), (x * 0.98, -0.09, 2.04), 0.005, seg=6))     # cable up the column
    trim.append(bar((0, 0.25, 0.78), (0, 0.25, 2.1), 0.006, seg=6))                       # stack cable
    s.part("Trim", trim)
    return export("cable_machine")


# =========================================================================== locker room

def towel(w, d, h, loc, yaw=0.0):
    """A folded towel: a soft slab with the rolled fold at the front (Blender -y) and a fold line along it."""
    bm = s.rbox((w, d, h), (0, 0, 0), r=h * 0.45, seg=2)
    return s.place(bm, loc, (0, 0, yaw))


def model_towel_shelf():
    """Open shelf 0.9 x 0.4 x 1.6: two sides, a plinth, four shelves and a top; folded towel stacks on the three upper
    shelves, rolled towels on the lowest one."""
    body = []
    for x in (-0.435, 0.435):
        body.append(s.rbox((0.03, 0.4, 1.58), (x, 0, 0.79), r=0.008, seg=2))
    for z in (0.09, 0.47, 0.85, 1.23):
        body.append(s.rbox((0.85, 0.37, 0.025), (0, 0.01, z), r=0.006, seg=1))
    body.append(s.rbox((0.9, 0.4, 0.03), (0, 0, 1.585), r=0.01, seg=2))                  # top
    body.append(s.rbox((0.85, 0.02, 0.08), (0, -0.17, 0.04), r=0.006, seg=1))             # plinth
    body.append(s.rbox((0.85, 0.02, 0.05), (0, 0.185, 1.35), r=0.006, seg=1))             # back rail
    s.part("Body", body)
    acc = []
    for k, z in enumerate((0.47, 0.85, 1.23)):
        for j, x in enumerate((-0.2, 0.2)):
            n = 4 if (k + j) % 2 == 0 else 3
            for i in range(n):
                jit = 0.012 * (((k * 3 + j * 5 + i * 7) % 5) - 2)
                acc.append(towel(0.34, 0.3, 0.06, (x + jit, -0.02 + jit * 0.5, z + 0.0095 + 0.03 + i * 0.056),
                                 yaw=0.03 * ((i + j) % 3 - 1)))
    s.part("Accent", acc)
    rolled = [s.rcyl(0.07, 0.3, (x, 0.0, 0.09 + 0.0125 + 0.067), axis="Y", seg=18, r=0.02, bevel_seg=2)
              for x in (-0.3, -0.1, 0.1, 0.3)]
    s.part("Trim", rolled)
    return export("towel_shelf")


def model_laundry_cart():
    """Laundry bin cart 0.8 x 0.6 x 0.9: a canvas bag hanging from a tubular top rim with its cuff folded over, four
    corner posts, a bottom frame and four casters."""
    body = []
    rim = [(-0.385, -0.285, 0.875), (0.385, -0.285, 0.875), (0.385, 0.285, 0.875), (-0.385, 0.285, 0.875)]
    for k in range(4):
        body.append(bar(rim[k - 1], rim[k], 0.013, seg=10))                               # top rim
        body.append(s.ball(0.0145, rim[k], seg=12))                                       # rim corners
    for x in (-0.37, 0.37):
        for y in (-0.27, 0.27):
            body.append(bar((x, y, 0.12), (x * 0.99, y * 0.99, 0.87), 0.012, seg=10))
    for y in (-0.27, 0.27):
        body.append(bar((-0.38, y, 0.135), (0.38, y, 0.135), 0.012, seg=10))
    for x in (-0.37, 0.37):
        body.append(bar((x, -0.28, 0.14), (x, 0.28, 0.14), 0.012, seg=10))
    tyres = []
    for x in (-0.33, 0.33):
        for y in (-0.23, 0.23):
            body.append(s.rbox((0.06, 0.08, 0.02), (x, y, 0.13), r=0.004, seg=1))             # caster plates
            w, f = caster(x, y, 0.045, 0.028, 0.13)
            tyres.append(w)
            body += f
    for x in (-0.33, 0.33):
        body.append(bar((x, -0.27, 0.13), (x, 0.27, 0.13), 0.01, seg=8))
    s.part("Body", body)

    bag = s.loft([(0.22, 0.3, 0.2, 3.0), (0.3, 0.345, 0.25, 3.5), (0.55, 0.37, 0.27, 4.0),
                  (0.86, 0.372, 0.272, 4.0)], seg=32, round_caps=0.04)
    for z in (0.42, 0.66):
        s.groove(bag, "Z", z, 0.01, 0.005)                                                   # canvas seams
    inner = s.loft([(0.245, 0.285, 0.185, 3.0), (0.32, 0.33, 0.235, 3.5), (0.55, 0.356, 0.256, 4.0),
                    (0.95, 0.358, 0.258, 4.0)], seg=32, round_caps=0.03)
    bag = s.boolean(bag, inner, "DIFFERENCE")
    cuff = s.loft([(0.76, 0.392, 0.292, 4.0), (0.895, 0.392, 0.292, 4.0)], seg=32)
    cuff = s.boolean(cuff, s.loft([(0.7, 0.362, 0.262, 4.0), (0.95, 0.362, 0.262, 4.0)], seg=32), "DIFFERENCE")
    s.bevel_edges(cuff, 0.004, 1, angle=50)
    s.part("Accent", [bag, cuff])
    s.part("Trim", tyres)
    return export("laundry_cart")


def model_bench_island():
    """Double-sided changing bench 1.8 x 0.8 x 1.6: two slatted seats back to back on three supports, a central spine
    of two end posts with a backrest rail and a hook rail, five double hooks on each side."""
    body = []
    for x in (-0.82, 0.82):
        body.append(s.rbox((0.08, 0.08, 1.57), (x, 0, 0.795), r=0.012, seg=2))            # end posts
        body.append(s.rbox((0.08, 0.7, 0.04), (x, 0, 0.02), r=0.01, seg=1))               # feet
    for x in (-0.82, 0.0, 0.82):
        body.append(s.rbox((0.06, 0.76, 0.05), (x, 0, 0.395), r=0.01, seg=1))             # seat arms
    body.append(s.rbox((0.07, 0.07, 0.38), (0, 0, 0.2), r=0.01, seg=1))                   # middle leg
    body.append(s.rbox((0.07, 0.5, 0.04), (0, 0, 0.02), r=0.01, seg=1))                   # middle foot
    body.append(s.rbox((1.6, 0.05, 0.05), (0, 0, 0.2), r=0.01, seg=1))                    # stretcher
    body.append(s.rbox((0.1, 0.1, 0.03), (-0.82, 0, 1.585), r=0.01, seg=1))               # post caps
    body.append(s.rbox((0.1, 0.1, 0.03), (0.82, 0, 1.585), r=0.01, seg=1))
    s.part("Body", body)

    acc = []
    for sy in (-1, 1):
        for y in (0.13, 0.24, 0.35):
            acc.append(s.rbox((1.8, 0.098, 0.035), (0, sy * y, 0.435), r=0.008, seg=2))   # slats
    acc.append(s.rbox((1.6, 0.04, 0.14), (0, 0, 0.75), r=0.01, seg=2))                    # backrest rail
    acc.append(s.rbox((1.6, 0.04, 0.16), (0, 0, 1.45), r=0.01, seg=2))                    # hook rail
    s.part("Accent", acc)

    hooks = []
    for sy in (-1, 1):
        for x in (-0.6, -0.3, 0.0, 0.3, 0.6):
            hooks.append(s.rbox((0.03, 0.012, 0.06), (x, sy * 0.022, 1.46), r=0.004, seg=1))  # hook plate
            hooks.append(s.tube([(x, sy * 0.025, 1.47), (x, sy * 0.07, 1.455), (x, sy * 0.095, 1.49)],
                                0.006, res=1, segments_per=4))
            hooks.append(s.tube([(x, sy * 0.025, 1.475), (x, sy * 0.05, 1.5), (x, sy * 0.055, 1.53)],
                                0.005, res=1, segments_per=3))
    s.part("Trim", hooks)
    return export("bench_island")


def model_weighing_scale():
    """Doctor's column scale: a low platform with a rubber mat, a column at the back, a balance head with two beams and
    sliding poises, and a height rod rising out of the column with a fold-out headpiece."""
    body = [s.rbox((0.4, 0.4, 0.07), (0, 0, 0.035), r=0.015, seg=2, taper=(0.96, 0.96))]
    body.append(s.rbox((0.12, 0.08, 0.06), (0, 0.15, 0.095), r=0.012, seg=1))             # column foot
    body.append(s.rbox((0.06, 0.05, 1.0), (0, 0.15, 0.58), r=0.01, seg=2))                # column
    body.append(s.rbox((0.1, 0.07, 0.15), (0, 0.15, 1.1), r=0.015, seg=2))                # balance head
    body.append(s.rbox((0.025, 0.02, 0.46), (0, 0.182, 1.07), r=0.005, seg=1))            # height rod
    body.append(s.rbox((0.03, 0.04, 0.03), (0, 0.18, 1.295), r=0.006, seg=1))             # headpiece hinge
    s.part("Body", body)
    acc = []
    for z in (1.13, 1.075):
        acc.append(s.rbox((0.4, 0.014, 0.026), (0, 0.11, z), r=0.005, seg=1))             # beams
    acc.append(s.rbox((0.03, 0.03, 0.04), (-0.08, 0.104, 1.13), r=0.006, seg=1))          # poises
    acc.append(s.rbox((0.022, 0.026, 0.034), (0.07, 0.104, 1.075), r=0.005, seg=1))
    acc.append(s.rbox((0.02, 0.016, 0.09), (0.19, 0.11, 1.1), r=0.004, seg=1))             # beam end stop
    acc.append(s.rbox((0.016, 0.2, 0.01), (0, 0.08, 1.292), r=0.003, seg=1))              # headpiece
    s.part("Accent", acc)
    trim = [s.rbox((0.32, 0.27, 0.012), (0, -0.04, 0.072), r=0.005, seg=1)]
    for x in (-0.12, 0.12):
        trim.append(s.rcyl(0.022, 0.02, (x, 0.19, 0.025), axis="X", seg=12, r=0.004, bevel_seg=1))  # wheels
    s.part("Trim", trim)
    return export("weighing_scale")


def shoe():
    """A sneaker with its heel at the origin and the toe towards +y: (upper, sole)."""
    rings = [(0.0, 0.03, 0.042, 2.6, 0, -0.05), (0.03, 0.04, 0.05, 2.6, 0, -0.052),
             (0.11, 0.045, 0.04, 2.8, 0, -0.042), (0.2, 0.044, 0.03, 3.0, 0, -0.032),
             (0.26, 0.032, 0.02, 2.6, 0, -0.022), (0.275, 0.0, 0.0, 2.0, 0, -0.02)]
    upper = s.loft(rings, seg=12, axis="Y")
    upper = s.boolean(upper, s.ball(1.0, (0, 0.06, 0.11), seg=12, scale=(0.032, 0.055, 0.03)), "DIFFERENCE")
    sole = s.rbox((0.094, 0.285, 0.018), (0, 0.138, 0.009), r=0.007, seg=1, taper=(1.0, 1.0))
    return upper, sole


def model_shoe_rack():
    """Low shoe rack 1.0 x 0.36 x 0.6: two side panels, a top board, two sloping tiers of round bars, three pairs of
    sneakers on each tier, heels out."""
    body = []
    for x in (-0.485, 0.485):
        body.append(s.rbox((0.03, 0.36, 0.58), (x, 0, 0.29), r=0.008, seg=2))
    body.append(s.rbox((1.0, 0.36, 0.025), (0, 0, 0.5875), r=0.008, seg=2))
    slope = 0.25
    for z0 in (0.1, 0.33):
        for y in (-0.12, 0.12):
            z = z0 + (y + 0.12) * slope
            body.append(bar((-0.475, y, z), (0.475, y, z), 0.011, seg=10))
    s.part("Body", body)
    uppers, soles = [], []
    ang = math.atan(slope)
    for z0 in (0.1, 0.33):
        z_heel = z0 + 0.011 + (-0.14 + 0.12) * slope - 0.004
        for k, x in enumerate((-0.31, 0.0, 0.31)):
            for d in (-1, 1):
                u, so = shoe()
                xx = x + d * 0.056 + 0.008 * ((k * 2 + d) % 3 - 1)
                uppers += on([u], (xx, -0.14, z_heel), (ang, 0, 0.04 * d))
                soles += on([so], (xx, -0.14, z_heel), (ang, 0, 0.04 * d))
    s.part("Accent", uppers)
    s.part("Trim", soles)
    return export("shoe_rack")


# =========================================================================== infirmary

def model_exam_table():
    """Examination couch 0.7 x 1.8: a cabinet base on a plinth with door seams and pulls on both long sides, a top
    frame, a padded couch with its head end (Blender +y) raised on a wedge, a paper sheet over it and the paper roll
    under the foot end."""
    body = [s.rbox((0.54, 1.44, 0.06), (0, -0.04, 0.03), r=0.01, seg=1)]                   # plinth
    cab = s.rbox((0.6, 1.5, 0.555), (0, -0.04, 0.3325), r=0.015, seg=2)
    s.groove(cab, "Z", 0.43, 0.008, 0.004)
    for y in (-0.38, 0.3):
        s.groove(cab, "Y", y, 0.008, 0.004)
    body.append(cab)
    body.append(s.rbox((0.7, 1.8, 0.04), (0, 0, 0.625), r=0.012, seg=2))                  # top frame
    th = math.radians(15)
    hinge = Vector((0, 0.32, 0.645))
    d = Vector((0, math.cos(th), math.sin(th)))
    body.append(prism([(0.32, 0.64), (0.86, 0.64), (0.86, 0.645 + 0.54 * math.sin(th))], "X", -0.3, 0.3,
                      bevel=0.004))                                                           # wedge
    for sx in (-1, 1):
        for y in (-0.71, -0.04, 0.64):
            pull = bar_pull(0, 0.5, 0, 0.12)
            body += on(pull, (sx * 0.3, y, 0), (0, 0, sx * math.pi / 2))
    for x in (-0.28, 0.28):
        body.append(s.rbox((0.02, 0.06, 0.09), (x, -0.85, 0.575), r=0.005, seg=1))        # roll brackets
    s.part("Body", body)
    nrm = Vector((0, -math.sin(th), math.cos(th)))
    acc = [s.rbox((0.66, 1.2, 0.08), (0, -0.28, 0.68), r=0.03, seg=3)]
    acc.append(s.rbox((0.66, 0.56, 0.08), hinge + d * 0.28 + nrm * 0.03, r=0.03, seg=3, rot=(th, 0, 0)))
    s.part("Accent", acc)
    trim = [s.rbox((0.5, 1.12, 0.006), (0, -0.28, 0.721), r=0.002, seg=1)]
    trim.append(s.rbox((0.5, 0.5, 0.006), hinge + d * 0.28 + nrm * 0.074, r=0.002, seg=1, rot=(th, 0, 0)))
    trim.append(s.rcyl(0.04, 0.55, (0, -0.85, 0.56), axis="X", seg=18, r=0.006, bevel_seg=1))  # paper roll
    s.part("Trim", trim)
    return export("exam_table")


def model_wheelchair():
    """Folding wheelchair, front to Blender -y: big rear wheels with hand rims and spokes, front casters, a tubular
    frame with an X brace, sling seat and back, armrests, footplates on hangers, push handles with grips."""
    body, trim, acc = [], [], []
    wc = (0.2, 0.3)  # rear wheel centre (y, z)
    for sx in (-1, 1):
        x = sx * 0.28
        acc.append(torus(0.285, 0.016, (x, *wc), axis="X", seg=32, rseg=8))              # tyres
        trim.append(torus(0.266, 0.009, (x, *wc), axis="X", seg=24, rseg=5))              # rims
        trim.append(torus(0.258, 0.008, (sx * 0.316, *wc), axis="X", seg=24, rseg=5))      # hand rims
        for k in range(4):
            a = k * math.pi / 2 + math.pi / 4
            p = Vector((0, math.cos(a), math.sin(a))) * 0.262
            trim.append(bar((sx * 0.29, wc[0] + p.y, wc[1] + p.z), (sx * 0.318, wc[0] + p.y, wc[1] + p.z),
                            0.004, seg=6))                                                    # rim stand-offs
        for k in range(8):
            a = k * math.pi / 4 + math.pi / 8
            pi = Vector((0, math.cos(a), math.sin(a)))
            off = sx * 0.012 * (1 if k % 2 else -1)
            trim.append(bar((x + off, wc[0] + pi.y * 0.03, wc[1] + pi.z * 0.03),
                            (x, wc[0] + pi.y * 0.266, wc[1] + pi.z * 0.266), 0.0028, seg=4))  # spokes
        body.append(s.rcyl(0.035, 0.06, (x, *wc), axis="X", seg=14, r=0.006, bevel_seg=1))  # hubs
        body.append(s.rbox((0.06, 0.08, 0.07), (sx * 0.245, wc[0], wc[1]), r=0.01, seg=1))  # axle plate
        # side frame tubes
        fx = sx * 0.22
        body.append(bar((fx, -0.34, 0.27), (fx, 0.26, 0.29), 0.012, seg=8))
        body.append(bar((fx, -0.24, 0.48), (fx, 0.27, 0.48), 0.012, seg=8))
        body.append(bar((fx, -0.25, 0.26), (fx, -0.23, 0.49), 0.012, seg=8))
        body.append(bar((fx, -0.23, 0.48), (sx * 0.16, -0.42, 0.11), 0.011, seg=8))       # footrest hanger
        body.append(s.tube([(fx, 0.25, 0.27), (fx, 0.27, 0.6), (fx, 0.29, 0.88), (fx, 0.33, 0.925),
                            (fx, 0.4, 0.93)], 0.012, res=1, segments_per=3))                # back post + handle
        body.append(bar((fx, -0.06, 0.48), (fx, -0.06, 0.71), 0.01, seg=8))                # armrest posts
        body.append(bar((fx, 0.2, 0.48), (fx, 0.2, 0.71), 0.01, seg=8))
        acc.append(s.rcyl(0.018, 0.1, (fx, 0.42, 0.93), axis="Y", seg=12, r=0.006, bevel_seg=1))  # grips
        # caster
        w, f = caster(sx * 0.22, -0.36, 0.07, 0.03, 0.27)
        acc.append(w)
        body += f
        acc.append(s.rbox((0.17, 0.14, 0.016), (sx * 0.1, -0.43, 0.105), r=0.005, seg=1, rot=(0.12, 0, 0)))
        acc.append(s.rbox((0.06, 0.3, 0.035), (fx, 0.07, 0.725), r=0.012, seg=2))         # armrest pads
    body.append(bar((-0.21, 0.02, 0.47), (0.21, 0.02, 0.29), 0.01, seg=8))                 # X brace
    body.append(bar((0.21, 0.04, 0.47), (-0.21, 0.04, 0.29), 0.01, seg=8))
    body.append(bar((-0.28, wc[0], wc[1]), (0.28, wc[0], wc[1]), 0.01, seg=8))            # axle
    s.part("Body", body)
    acc.append(s.rbox((0.45, 0.46, 0.05), (0, 0.01, 0.5), r=0.02, seg=2))                  # seat
    acc.append(s.rbox((0.45, 0.035, 0.36), (0, 0.285, 0.72), r=0.012, seg=2, rot=(-0.064, 0, 0)))  # back sling
    s.part("Accent", acc)
    s.part("Trim", trim)
    return export("wheelchair")


def model_iv_stand():
    """Drip stand 1.9 m: a four-leg base on casters, a pole with a height collar, a crown of four up-turned hooks, a
    bag on one hook with a drip chamber and a line clipped to the pole."""
    body = [s.rcyl(0.045, 0.08, (0, 0, 0.11), seg=16, r=0.01, bevel_seg=1)]                # hub
    acc = []
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        e = Vector((math.cos(a), math.sin(a), 0)) * 0.305
        body.append(bar((0, 0, 0.11), (e.x, e.y, 0.075), 0.014, seg=10))                   # legs
        w, f = caster(e.x, e.y, 0.03, 0.022, 0.09)
        acc.append(w)
        body += f
    body.append(bar((0, 0, 0.12), (0, 0, 1.2), 0.016, seg=12))                             # lower pole
    body.append(bar((0, 0, 1.15), (0, 0, 1.86), 0.012, seg=12))                            # upper pole
    body.append(s.lathe([(0.0, 1.16), (0.026, 1.16), (0.028, 1.17), (0.028, 1.21), (0.02, 1.22), (0.0, 1.22)],
                        seg=14))                                                              # collar
    body.append(s.rcyl(0.008, 0.04, (0.04, 0, 1.19), axis="X", seg=8, r=0.002, bevel_seg=1))
    body.append(s.ball(0.014, (0.06, 0, 1.19), seg=10))                                    # collar knob
    body.append(s.rcyl(0.022, 0.04, (0, 0, 1.8), seg=12, r=0.005, bevel_seg=1))            # crown hub
    for k in range(4):
        a = k * math.pi / 2
        c, sn = math.cos(a), math.sin(a)
        body.append(s.tube([(0, 0, 1.8), (0.09 * c, 0.09 * sn, 1.8), (0.125 * c, 0.125 * sn, 1.83),
                            (0.12 * c, 0.12 * sn, 1.89)], 0.0065, res=1, segments_per=4))
    s.part("Body", body)
    acc.append(s.rbox((0.01, 0.006, 0.04), (0.115, 0, 1.8), r=0.002, seg=1))               # bag clip
    # the line comes out of the drip chamber, loops out and is clipped to the pole
    line = s.tube([(0.115, 0, 1.445), (0.15, -0.03, 1.25), (0.08, -0.04, 1.08), (0.02, -0.012, 1.02)],
                  0.003, res=1, segments_per=8)
    acc.append(s.rbox((0.03, 0.02, 0.03), (0.018, -0.012, 1.02), r=0.004, seg=1))         # clip on the pole
    acc.append(s.rbox((0.016, 0.016, 0.03), (0.15, -0.03, 1.25), r=0.003, seg=1))          # roller clamp
    s.part("Accent", acc)
    bag = s.loft([(1.5, 0.03, 0.008, 2.5, 0.115), (1.54, 0.065, 0.02, 3.0, 0.115), (1.74, 0.07, 0.022, 3.5, 0.115),
                  (1.8, 0.05, 0.012, 3.0, 0.115)], seg=20, round_caps=0.006)
    chamber = s.rcyl(0.01, 0.07, (0.115, 0, 1.47), seg=12, r=0.003, bevel_seg=1)
    s.part("Trim", [bag, chamber, line])
    return export("iv_stand")


def model_doctor_desk():
    """Doctor's desk 1.4 x 0.7, top at 0.76: a three-drawer pedestal on the left, a panel leg on the right, a
    modesty panel, a monitor (Eye) on a stand, a keyboard and mouse, and a stethoscope lying on the desk."""
    body = [s.rbox((1.4, 0.7, 0.03), (0, 0, 0.745), r=0.01, seg=2)]                        # top
    ped = s.rbox((0.42, 0.62, 0.72), (-0.47, 0.02, 0.37), r=0.012, seg=2)
    for z in (0.28, 0.5):
        s.groove(ped, "Z", z, 0.008, 0.004)
    body.append(ped)
    body.append(s.rbox((0.42, 0.6, 0.04), (-0.47, 0.03, 0.02), r=0.008, seg=1))             # plinth
    body.append(s.rbox((0.03, 0.66, 0.73), (0.67, 0, 0.365), r=0.008, seg=2))              # panel leg
    body.append(s.rbox((0.94, 0.02, 0.4), (0.2, 0.3, 0.52), r=0.006, seg=1))               # modesty panel
    s.part("Body", body)
    acc = []
    acc.append(s.rbox((0.22, 0.16, 0.014), (0.1, 0.17, 0.764), r=0.005, seg=1))            # monitor foot
    acc.append(s.rbox((0.05, 0.03, 0.22), (0.1, 0.19, 0.87), r=0.008, seg=1))             # neck
    acc.append(s.rbox((0.56, 0.04, 0.34), (0.1, 0.16, 1.07), r=0.012, seg=2))             # housing
    acc.append(s.rbox((0.44, 0.15, 0.02), (0.1, -0.13, 0.765), r=0.006, seg=1, taper=(1.0, 0.92)))  # keyboard
    acc.append(s.loft([(0.755, 0.03, 0.05, 2.5), (0.77, 0.032, 0.052, 2.5), (0.785, 0.02, 0.04, 2.5)], seg=14,
                      loc=(0.43, -0.12, 0)))                                                 # mouse
    # stethoscope tubing: chest piece -> Y joint, then the two ear tubes
    zs = 0.764
    acc.append(s.tube([(-0.36, -0.16, zs), (-0.24, -0.22, zs), (-0.13, -0.12, zs), (-0.17, 0.0, zs),
                       (-0.29, 0.02, zs)], 0.006, res=1, segments_per=6))
    s.part("Accent", acc)
    trim = []
    for z in (0.17, 0.39, 0.62):
        trim += bar_pull(-0.47, z, -0.29, 0.14)
    trim.append(s.lathe([(0.0, 0.758), (0.026, 0.758), (0.028, 0.764), (0.024, 0.772), (0.008, 0.776),
                         (0.0, 0.776)], seg=16, loc=(-0.38, -0.15, 0)))                    # chest piece
    for k, end in enumerate(((-0.41, 0.06), (-0.39, 0.13))):
        trim.append(s.tube([(-0.293 + 0.006 * k, 0.018 + 0.004 * k, zs),
                            ((-0.29 + end[0]) / 2, (0.02 + end[1]) / 2 + 0.02, zs),
                            (end[0], end[1], zs)], 0.0035, res=1, segments_per=5))        # binaurals
        trim.append(s.ball(0.008, (end[0], end[1], zs), seg=10))                           # ear tips
    trim.append(s.rcyl(0.008, 0.02, (-0.29, 0.02, zs), seg=10, r=0.002, bevel_seg=1))     # Y joint
    s.part("Trim", trim)
    s.part("Eye", [s.rbox((0.52, 0.008, 0.3), (0.1, 0.139, 1.075), r=0.003, seg=1)])
    return export("doctor_desk")


def heart(cx, cz, size, y0, y1):
    """A heart outline in the XZ plane extruded along Y."""
    pts = []
    n = 28
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        z = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((cx + x * size / 32, cz + z * size / 32))
    return prism(pts, "Y", y0, y1, bevel=0.0015)


def model_defib_cabinet():
    """Wall AED cabinet 0.4 x 0.4 hung at 1.2..1.6, depth 0.16 with its back on the wall: a green box with a header
    carrying a glowing heart sign, a glazed door with a handle, the defibrillator visible inside."""
    body = [s.rbox((0.4, 0.02, 0.4), (0, 0.07, 1.4), r=0.008, seg=2)]                     # wall plate
    box = s.rbox((0.38, 0.14, 0.38), (0, -0.005, 1.4), r=0.015, seg=2)
    box = s.boolean(box, s.rbox((0.32, 0.12, 0.24), (0, -0.03, 1.355), r=0.01, seg=1), "DIFFERENCE")
    s.bevel_edges(box, 0.003, 1, angle=50)
    body.append(box)
    body.append(frame((0.36, 0.012, 0.27), (0, -0.075, 1.355), 0.025, r=0.005, seg=1))     # door frame
    s.part("Body", body)
    acc = [s.rbox((0.24, 0.09, 0.19), (0, -0.01, 1.328), r=0.025, seg=2)]                   # AED
    acc.append(s.tube([(-0.06, -0.01, 1.42), (-0.05, -0.01, 1.45), (0.05, -0.01, 1.45), (0.06, -0.01, 1.42)],
                      0.008, res=1, segments_per=4))                                         # carry handle
    acc.append(s.rbox((0.012, 0.02, 0.08), (0.162, -0.085, 1.35), r=0.004, seg=1))          # door handle
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((0.32, 0.006, 0.23), (0, -0.074, 1.355), r=0.002, seg=1)])        # glass
    eye = [heart(0, 1.535, 0.07, -0.082, -0.07)]
    eye.append(s.rcyl(0.016, 0.01, (0.05, -0.058, 1.33), axis="Y", seg=14, r=0.002, bevel_seg=1))  # AED button
    s.part("Eye", eye)
    return export("defib_cabinet")


# =========================================================================== nap room

def model_bunk_bed():
    """Bunk bed 1.0 x 2.0 x 1.8, long along Blender y, head at +y: four posts, two bed frames with slats and
    mattresses, pillows, guard rails round the top bunk, head boards, and the foot end doubling as the ladder."""
    body = []
    for x in (-0.47, 0.47):
        for y in (-0.97, 0.97):
            body.append(s.rbox((0.06, 0.06, 1.8), (x, y, 0.9), r=0.012, seg=2))
    for z in (0.3, 1.2):
        for x in (-0.47, 0.47):
            body.append(s.rbox((0.04, 1.9, 0.14), (x, 0, z), r=0.01, seg=2))              # side rails
        for y in (-0.97, 0.97):
            body.append(s.rbox((0.9, 0.04, 0.14), (0, y, z), r=0.01, seg=2))              # end rails
        body.append(s.rbox((0.88, 1.88, 0.02), (0, 0, z + 0.03), r=0.005, seg=1))          # slat deck
    for x in (-0.47, 0.47):
        for z in (1.5, 1.7):
            body.append(s.rbox((0.035, 1.9, 0.045), (x, 0, z), r=0.01, seg=2))           # top guards
    for z, h in ((0.62, 0.36), (1.55, 0.36)):
        body.append(s.rbox((0.9, 0.03, h), (0, 0.97, z), r=0.01, seg=2))                 # head boards
    for z in (0.58, 0.82, 1.48, 1.7):
        body.append(s.rbox((0.9, 0.04, 0.04), (0, -0.975, z), r=0.012, seg=2))            # ladder rungs
    body.append(s.rbox((0.9, 0.04, 0.04), (0, -0.975, 1.02), r=0.012, seg=2))
    s.part("Body", body)
    acc = [s.rbox((0.86, 1.86, 0.16), (0, 0, z + 0.115), r=0.045, seg=3) for z in (0.3, 1.2)]
    s.part("Accent", acc)
    trim = [s.rbox((0.56, 0.32, 0.11), (0, 0.72, z + 0.235), r=0.045, seg=3) for z in (0.3, 1.2)]
    trim += [s.rbox((0.84, 0.36, 0.05), (0, -0.7, z + 0.215), r=0.022, seg=2) for z in (0.3, 1.2)]  # folded blankets
    s.part("Trim", trim)
    return export("bunk_bed")


def model_daybed():
    """Daybed 2.0 x 0.9 x 0.6: a wooden box frame on short legs with a low back and arms, a mattress with a piped
    seam, three back cushions and two round bolsters against the arms."""
    body = [s.rbox((1.96, 0.86, 0.18), (0, 0.0, 0.17), r=0.015, seg=2)]                     # frame
    body.append(s.rbox((2.0, 0.08, 0.36), (0, 0.41, 0.42), r=0.02, seg=2))                  # back
    for x in (-0.96, 0.96):
        body.append(s.rbox((0.07, 0.88, 0.28), (x, 0, 0.385), r=0.02, seg=2))             # arms
    for x in (-0.9, 0.9):
        for y in (-0.37, 0.37):
            body.append(s.rcyl(0.026, 0.1, (x, y, 0.05), seg=14, r=0.004, bevel_seg=1, radius2=0.034))  # legs
    s.part("Body", body)
    mat = s.rbox((1.86, 0.78, 0.16), (0, -0.03, 0.33), r=0.04, seg=3)
    s.groove(mat, "Z", 0.33, 0.01, 0.005)
    s.part("Accent", [mat])
    trim = []
    for x in (-0.55, 0.0, 0.55):
        c = s.rbox((0.52, 0.14, 0.26), (0, 0, 0), r=0.06, seg=3, taper=(0.94, 0.8))
        trim += on([c], (x, 0.29, 0.53), (-0.2, 0, 0))
    for x in (-0.84, 0.84):
        trim.append(s.rcyl(0.085, 0.56, (x, -0.1, 0.49), axis="Y", seg=20, r=0.03, bevel_seg=2))
    s.part("Trim", trim)
    return export("daybed")


def curtain(x0, x1, z0, z1, amp, period, thick=0.008, n_per=8):
    """A pleated curtain: a closed sheet in the XZ plane whose plan view waves along x (amp, period)."""
    bm = bmesh.new()
    n = max(2, int(round((x1 - x0) / period * n_per)))
    cols = []
    for i in range(n + 1):
        x = x0 + (x1 - x0) * i / n
        y = amp * math.sin(2 * math.pi * (x - x0) / period)
        # offset along the normal of the wave so the sheet keeps its thickness on the slopes
        dy = amp * 2 * math.pi / period * math.cos(2 * math.pi * (x - x0) / period)
        nrm = Vector((-dy, 1, 0)).normalized() * (thick / 2)
        cols.append([bm.verts.new((x - nrm.x * sgn, y - nrm.y * sgn, z)) for sgn in (1, -1) for z in (z0, z1)])
    # per column: [front-bottom, front-top, back-bottom, back-top]
    for a, b in zip(cols, cols[1:]):
        bm.faces.new((a[0], b[0], b[1], a[1]))
        bm.faces.new((a[2], a[3], b[3], b[2]))
        bm.faces.new((a[1], b[1], b[3], a[3]))
        bm.faces.new((a[0], a[2], b[2], b[0]))
    bm.faces.new((cols[0][0], cols[0][1], cols[0][3], cols[0][2]))
    bm.faces.new((cols[-1][0], cols[-1][2], cols[-1][3], cols[-1][1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def model_curtain_divider():
    """Ward screen 1.6 x 1.9: two uprights on caster feet joined by a top rail, a pleated curtain hanging from rings."""
    body = []
    trim = []
    for x in (-0.77, 0.77):
        body.append(s.rbox((0.05, 0.2, 0.035), (x, 0, 0.0675), r=0.01, seg=1))           # feet
        body.append(bar((x, 0, 0.07), (x, 0, 1.84), 0.016, seg=12))                       # uprights
        body.append(s.ball(0.024, (x, 0, 1.86), seg=14))                                   # elbows
        for y in (-0.07, 0.07):
            w, f = caster(x, y, 0.025, 0.02, 0.06)
            trim.append(w)
            body += f
    body.append(bar((-0.77, 0, 1.86), (0.77, 0, 1.86), 0.014, seg=12))                     # top rail
    period = 0.16
    for k in range(10):
        x = -0.72 + period * k
        body.append(torus(0.022, 0.004, (x, 0, 1.854), axis="X", seg=12, rseg=5))           # rings
    s.part("Body", body)
    s.part("Accent", [curtain(-0.72, 0.72, 0.3, 1.834, 0.04, period)])
    s.part("Trim", trim)
    return export("curtain_divider")


def model_floor_cushions():
    """A cluster of big floor cushions 1.2 x 1.2: two square cushions, a round pouf with a piped waist and a smaller
    cushion stacked on the first, each tufted with a button."""
    a = pillow(0.62, 0.62, 0.17)
    a_btn = s.ball(0.022, (0, 0, 0.168), seg=12, scale=(1, 1, 0.5))
    on([a, a_btn], (-0.26, 0.23, 0), (0, 0, math.radians(10)))
    d = pillow(0.44, 0.44, 0.13)
    d_btn = s.ball(0.02, (0, 0, 0.128), seg=12, scale=(1, 1, 0.5))
    on([d, d_btn], (0, 0, 0), (0.06, 0, math.radians(35)))
    on([d, d_btn], (-0.28, 0.24, 0.155))
    s.part("Body", [a, a_btn, d, d_btn])
    pouf = s.lathe([(0.0, 0.0), (0.2, 0.0), (0.235, 0.03), (0.245, 0.1), (0.25, 0.13), (0.24, 0.14),
                    (0.25, 0.15), (0.245, 0.19), (0.225, 0.24), (0.16, 0.262), (0.0, 0.27)], seg=32)
    p_btn = s.ball(0.022, (0, 0, 0.27), seg=12, scale=(1, 1, 0.5))
    on([pouf, p_btn], (0.32, 0.33, 0))
    s.part("Accent", [pouf, p_btn])
    c = pillow(0.56, 0.5, 0.15)
    c_btn = s.ball(0.02, (0, 0, 0.148), seg=12, scale=(1, 1, 0.5))
    on([c, c_btn], (0.24, -0.32, 0), (0, 0, math.radians(-18)))
    s.part("Trim", [c, c_btn])
    return export("floor_cushions")


def model_night_table():
    """Bedside table 0.5 x 0.44: a carcass on tapered legs with a drawer and an open niche, a top, a little lamp with
    a glowing shade and a book."""
    body = [s.rbox((0.5, 0.44, 0.025), (0, 0, 0.3875), r=0.008, seg=2)]                    # top
    cab = s.rbox((0.46, 0.4, 0.26), (0, 0.01, 0.255), r=0.01, seg=2)
    cab = s.boolean(cab, s.rbox((0.4, 0.36, 0.1), (0, -0.04, 0.185), r=0.008, seg=1), "DIFFERENCE")  # niche
    s.bevel_edges(cab, 0.003, 1, angle=50)
    body.append(cab)
    body.append(s.rbox((0.42, 0.02, 0.1), (0, -0.192, 0.315), r=0.006, seg=2))             # drawer front
    for x in (-0.2, 0.2):
        for y in (-0.16, 0.18):
            body.append(s.rbox((0.026, 0.026, 0.14), (x, y, 0.07), r=0.005, seg=1, taper=(1.4, 1.4)))
    s.part("Body", body)
    acc = [s.ball(0.014, (0, -0.205, 0.315), seg=12)]                                        # knob
    acc.append(s.lathe([(0.0, 0.398), (0.055, 0.398), (0.058, 0.405), (0.05, 0.42), (0.016, 0.428), (0.0, 0.43)],
                       seg=20, loc=(0.11, 0.06, 0)))                                         # lamp base
    acc.append(s.rcyl(0.008, 0.1, (0.11, 0.06, 0.475), seg=10, r=0.002, bevel_seg=1))       # stem
    acc.append(s.rbox((0.15, 0.21, 0.035), (-0.1, -0.02, 0.416), r=0.004, seg=1, rot=(0, 0, 0.25)))  # book
    s.part("Accent", acc)
    s.part("Eye", [s.lathe([(0.0, 0.48), (0.085, 0.48), (0.088, 0.485), (0.064, 0.595), (0.06, 0.6), (0.0, 0.6)],
                           seg=24, loc=(0.11, 0.06, 0))])
    return export("night_table")


MODELS = {
    "rowing_machine": model_rowing_machine,
    "power_cage": model_power_cage,
    "kettlebell_rack": model_kettlebell_rack,
    "mat_rack": model_mat_rack,
    "cable_machine": model_cable_machine,
    "towel_shelf": model_towel_shelf,
    "laundry_cart": model_laundry_cart,
    "bench_island": model_bench_island,
    "weighing_scale": model_weighing_scale,
    "shoe_rack": model_shoe_rack,
    "exam_table": model_exam_table,
    "wheelchair": model_wheelchair,
    "iv_stand": model_iv_stand,
    "doctor_desk": model_doctor_desk,
    "defib_cabinet": model_defib_cabinet,
    "bunk_bed": model_bunk_bed,
    "daybed": model_daybed,
    "curtain_divider": model_curtain_divider,
    "floor_cushions": model_floor_cushions,
    "night_table": model_night_table,
}


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    if "--only" in args:
        only = set(args[args.index("--only") + 1].split(","))
    s.clear_scene()
    for name, fn in MODELS.items():
        if only is None or name in only:
            fn()
    print("DONE")
