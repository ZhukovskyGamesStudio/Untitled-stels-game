"""Furniture of the fitness, medical and rest rooms: treadmill, exercise bike, weight bench with a rack, dumbbell rack,
free-standing punching bag, hospital bed, glass medicine cabinet, folding privacy screen, nap pod, hammock on a stand.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms_fit.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--focus x,y,z,r] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), back at Blender +Y (Unity z min), z up,
origin on the floor in the middle of the footprint. Object names are material roles: Body, Accent, Trim, Eye (glows).
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
    """meshfix (floor support), then the FBX into <root>/Props/; prints the bounding box in Unity terms."""
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
          f"  size {hi.x - lo.x:.3f} x {hi.y - lo.y:.3f} x {hi.z - lo.z:.3f}  tris~{tris}")
    path = os.path.join(g.out_dir, "Props", filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


def bar(a, b, radius, seg=12):
    """Straight round bar between two points (chamfered ends)."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = s.rcyl(radius, d.length, (0, 0, 0), seg=seg, r=radius * 0.3, bevel_seg=1)
    rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    bm.transform(rot)
    return s.place(bm, (a + b) / 2)


def place_on(bms, loc=(0, 0, 0), rot=(0, 0, 0)):
    for b in bms:
        s.place(b, loc, rot)
    return bms


# --------------------------------------------------------------------------- treadmill

def model_treadmill():
    """Running machine 0.84 x 1.9: a belt deck between side rails with rubber strips, a motor hood at the back
    (Blender +Y), two leaning uprights carrying the console (screen faces the runner) and handrails with grips."""
    rails = [s.rbox((0.1, 1.5, 0.16), (x * 0.37, -0.2, 0.11), r=0.02, seg=2) for x in (-1, 1)]
    roller = s.rcyl(0.06, 0.8, (0, -0.89, 0.1), axis="X", seg=20, r=0.012)
    hood = s.rbox((0.84, 0.36, 0.27), (0, 0.6, 0.155), r=0.04, seg=3, taper=(0.96, 0.85))
    s.groove(hood, "X", 0.0, 0.012, 0.005)
    th = math.atan2(0.17, 1.02)
    uprights = [s.rbox((0.07, 0.09, 1.08), (x * 0.37, 0.745, 0.72), r=0.02, seg=2, rot=(-th, 0, 0)) for x in (-1, 1)]
    tc = -0.5
    con_c = Vector((0, 0.82, 1.255))
    console = s.rbox((0.8, 0.16, 0.24), con_c, r=0.035, seg=3, rot=(tc, 0, 0))
    n = Vector((0, -math.cos(tc), -math.sin(tc)))
    bezel = s.rbox((0.46, 0.02, 0.17), con_c + n * 0.08, r=0.008, seg=1, rot=(tc, 0, 0))
    cross = s.rbox((0.72, 0.07, 0.07), (0, 0.84, 1.08), r=0.015, seg=2)
    handrails = []
    for x in (-1, 1):
        handrails.append(s.tube([(x * 0.37, 0.81, 1.0), (x * 0.39, 0.7, 1.03), (x * 0.395, 0.45, 1.03),
                                 (x * 0.39, 0.3, 0.99)], 0.02, segments_per=4))
    s.part("Body", [*rails, roller, hood, *uprights, console, bezel, cross, *handrails])

    belt = s.rbox((0.66, 1.44, 0.05), (0, -0.2, 0.152), r=0.012, seg=2)
    for y in (-0.8, -0.4, 0.0, 0.4):
        s.groove(belt, "Y", y, 0.008, 0.003)
    strips = [s.rbox((0.08, 1.3, 0.016), (x * 0.37, -0.25, 0.19), r=0.006, seg=1) for x in (-1, 1)]
    feet = [s.rbox((0.08, 0.08, 0.05), (x * 0.37, y, 0.025), r=0.012, seg=1) for x in (-1, 1) for y in (-0.85, 0.45)]
    grips = [s.tube([(x * 0.393, 0.62, 1.03), (x * 0.395, 0.38, 1.025)], 0.028, smooth=False, segments_per=1)
             for x in (-1, 1)]
    up = Vector((0, -math.sin(tc), math.cos(tc)))
    keys = [s.place(s.rbox((0.05, 0.02, 0.03), (0, 0, 0), r=0.006, seg=1),
                    con_c + n * 0.082 + up * (0.04 - 0.07 * (k % 2)) + Vector((-0.3 if k < 2 else 0.3, 0, 0)), (tc, 0, 0))
            for k in range(4)]
    s.part("Accent", [belt, *strips, *feet, *grips, *keys])

    screen = s.rbox((0.4, 0.012, 0.13), con_c + n * 0.09, r=0.004, seg=1, rot=(tc, 0, 0))
    s.part("Eye", [screen])
    return export("treadmill")


# --------------------------------------------------------------------------- exercise bike

def model_exercise_bike():
    """Upright stationary bike: two stabiliser feet on a base beam, a curved main tube up to the handlebar mast,
    seat tube and saddle at the front (-Y), a flywheel shroud at the back bottom, cranks and pedals, a console."""
    base = s.rbox((0.08, 0.96, 0.06), (0, 0.02, 0.07), r=0.015, seg=2)
    stabs = [s.rbox((0.48, 0.07, 0.07), (0, y, 0.04), r=0.02, seg=1) for y in (-0.42, 0.47)]
    shroud = s.rcyl(0.24, 0.12, (0, 0.3, 0.36), axis="X", seg=24, r=0.025)
    s.inset_faces(shroud, s.facing((1, 0, 0), min_area=0.05, tol=0.95), thickness=0.04, depth=-0.008)
    s.inset_faces(shroud, s.facing((-1, 0, 0), min_area=0.05, tol=0.95), thickness=0.04, depth=-0.008)
    main = s.tube([(0, -0.3, 0.08), (0, -0.05, 0.3), (0, 0.16, 0.6), (0, 0.27, 1.0)], 0.036, res=1, segments_per=4)
    mast = bar((0, 0.265, 0.95), (0, 0.33, 1.16), 0.03)
    seat_tube = bar((0, -0.1, 0.22), (0, -0.3, 0.84), 0.03)
    post = bar((0, -0.28, 0.8), (0, -0.33, 0.9), 0.02)
    clamp = s.rbox((0.05, 0.08, 0.04), (0, -0.33, 0.9), r=0.01, seg=1)
    hub = s.rcyl(0.055, 0.12, (0, -0.04, 0.32), axis="X", seg=16, r=0.012)
    arms = [s.rbox((0.025, 0.03, 0.2), (x * 0.075, -0.04 + x * 0.05, 0.32 - x * 0.07), r=0.008, seg=1,
                   rot=(x * 0.62, 0, 0)) for x in (-1, 1)]
    bars = s.tube([(-0.24, 0.13, 1.0), (-0.22, 0.27, 1.05), (0, 0.33, 1.1), (0.22, 0.27, 1.05), (0.24, 0.13, 1.0)],
                  0.016, res=1, segments_per=4)
    con_c = Vector((0, 0.37, 1.2))
    tc = -0.6
    console = s.rbox((0.24, 0.07, 0.16), con_c, r=0.02, seg=2, rot=(tc, 0, 0))
    s.part("Body", [base, *stabs, shroud, main, mast, seat_tube, post, clamp, hub, *arms, bars, console])

    saddle = s.loft([(0, 0.035, 0.035, 2.5), (0.08, 0.05, 0.04, 2.5), (0.2, 0.12, 0.045, 3), (0.3, 0.12, 0.04, 3)],
                    seg=16, axis="-Y", loc=(0, -0.24, 0.94), round_caps=0.02)
    caps = [s.rbox((0.06, 0.09, 0.05), (x * 0.25, y, 0.025), r=0.015, seg=1) for x in (-1, 1) for y in (-0.42, 0.47)]
    pedals = [s.rbox((0.1, 0.07, 0.025), (x * 0.14, -0.04 + x * 0.1, 0.32 - x * 0.14), r=0.008, seg=1)
              for x in (-1, 1)]
    axles = [s.rcyl(0.01, 0.06, (x * 0.11, -0.04 + x * 0.1, 0.32 - x * 0.14), axis="X", seg=8, r=0.002)
             for x in (-1, 1)]
    grips = [s.tube([(x * 0.24, 0.13, 1.0), (x * 0.237, 0.22, 1.03)], 0.024, smooth=False, segments_per=1)
             for x in (-1, 1)]
    fly_hub = s.rcyl(0.07, 0.15, (0, 0.3, 0.36), axis="X", seg=16, r=0.012, bevel_seg=1)
    s.part("Accent", [saddle, *caps, *pedals, *axles, *grips, fly_hub])

    n = Vector((0, -math.cos(tc), -math.sin(tc)))
    screen = s.rbox((0.18, 0.012, 0.11), con_c + n * 0.034, r=0.004, seg=1, rot=(tc, 0, 0))
    s.part("Eye", [screen])
    return export("exercise_bike")


# --------------------------------------------------------------------------- weight bench

def plate(radius, thick, x, y, z):
    """Weight plate around the X axis: raised rim, recessed face, a hub boss."""
    t = thick / 2
    prof = [(0.0, -t - 0.004), (0.045, -t - 0.004), (0.05, -t + 0.006), (radius * 0.8, -t), (radius, -t + 0.008),
            (radius, t - 0.008), (radius * 0.8, t), (0.05, t - 0.006), (0.045, t + 0.004), (0.0, t + 0.004)]
    return [s.lathe(prof, seg=16, axis="X", loc=(x, y, z))]


def model_weight_bench():
    """Flat bench with a padded top, and behind it (Blender +Y) a two-post rack with J-hooks holding a loaded
    barbell along X: two big plates and a small one on each sleeve, collars, knurled bar."""
    beam = s.rbox((0.08, 0.86, 0.06), (0, -0.14, 0.34), r=0.015, seg=2)
    legs = [s.rbox((0.06, 0.06, 0.32), (0, y, 0.18), r=0.012, seg=1) for y in (-0.5, 0.2)]
    bench_feet = [s.rbox((0.4, 0.07, 0.05), (0, y, 0.025), r=0.015, seg=1) for y in (-0.5, 0.2)]
    bases = [s.rbox((0.08, 0.44, 0.06), (x * 0.5, 0.41, 0.03), r=0.015, seg=1) for x in (-1, 1)]
    posts = [s.rbox((0.07, 0.07, 1.14), (x * 0.5, 0.45, 0.6), r=0.015, seg=2) for x in (-1, 1)]
    back = s.rbox((1.06, 0.06, 0.06), (0, 0.55, 0.09), r=0.015, seg=1)
    top = s.rbox((1.06, 0.06, 0.06), (0, 0.45, 1.14), r=0.015, seg=1)
    hooks = []
    for x in (-1, 1):
        hooks.append(s.rbox((0.06, 0.13, 0.04), (x * 0.5, 0.385, 1.0), r=0.01, seg=1))
        hooks.append(s.rbox((0.06, 0.025, 0.07), (x * 0.5, 0.33, 1.03), r=0.008, seg=1))
    s.part("Body", [beam, *legs, *bench_feet, *bases, *posts, back, top, *hooks])

    pad = s.rbox((0.3, 0.98, 0.1), (0, -0.14, 0.415), r=0.04, seg=2)
    s.groove(pad, "Z", 0.385, 0.01, 0.004)
    feet_caps = [s.rbox((0.05, 0.08, 0.055), (x * 0.19, y, 0.027), r=0.012, seg=1) for x in (-1, 1) for y in (-0.5, 0.2)]
    by, bz = 0.385, 1.034
    plates = []
    for x in (-1, 1):
        plates += plate(0.225, 0.05, x * 0.645, by, bz)
        plates += plate(0.225, 0.05, x * 0.7, by, bz)
    s.part("Accent", [pad, *feet_caps, *plates])

    bar_ = s.rcyl(0.014, 1.2, (0, by, bz), axis="X", seg=10, r=0.003, bevel_seg=1)
    sleeves = [s.rcyl(0.025, 0.21, (x * 0.69, by, bz), axis="X", seg=12, r=0.004, bevel_seg=1) for x in (-1, 1)]
    collars = [s.rcyl(0.038, 0.03, (x * 0.6, by, bz), axis="X", seg=12, r=0.005, bevel_seg=1) for x in (-1, 1)]
    clips = [s.rcyl(0.034, 0.02, (x * 0.74, by, bz), axis="X", seg=12, r=0.004, bevel_seg=1) for x in (-1, 1)]
    s.part("Trim", [bar_, *sleeves, *collars, *clips])
    return export("weight_bench")


# --------------------------------------------------------------------------- dumbbell rack

def dumbbell(x, y, z_floor, r, head, gap):
    """Hex dumbbell along Y lying on a flat at z_floor: two hex heads (a flat face down), a handle."""
    zc = z_floor + r * math.cos(math.pi / 6)
    heads = [s.rcyl(r, head, (x, y + d * (gap + head) / 2, zc), axis="Y", seg=6, r=0.008, bevel_seg=1) for d in (-1, 1)]
    handle = s.rcyl(0.017, gap + 0.02, (x, y, zc), axis="Y", seg=8, r=0.003, bevel_seg=1)
    return heads, handle


def model_dumbbell_rack():
    """Two-tier rack: side frames with a short front post and a tall back post, two trays with lips, the upper tray
    set back; five hex dumbbells on each tier along X, heavier ones below."""
    frame = []
    for x in (-1, 1):
        xx = x * 0.67
        frame.append(s.rbox((0.06, 0.5, 0.05), (xx, 0, 0.025), r=0.012, seg=1))
        frame.append(s.rbox((0.05, 0.05, 0.5), (xx, -0.17, 0.27), r=0.012, seg=1))
        frame.append(s.rbox((0.05, 0.05, 0.84), (xx, 0.17, 0.44), r=0.012, seg=1))
        frame.append(s.rbox((0.05, 0.42, 0.05), (xx, 0.0, 0.38), r=0.012, seg=1))
        frame.append(s.rbox((0.05, 0.27, 0.05), (xx, 0.055, 0.7), r=0.012, seg=1))
    lo = s.rbox((1.3, 0.32, 0.03), (0, -0.065, 0.405), r=0.008, seg=1)
    lo_lips = [s.rbox((1.3, 0.025, 0.05), (0, y, 0.425), r=0.008, seg=1) for y in (-0.215, 0.085)]
    hi = s.rbox((1.3, 0.26, 0.03), (0, 0.09, 0.725), r=0.008, seg=1)
    hi_lips = [s.rbox((1.3, 0.025, 0.05), (0, y, 0.745), r=0.008, seg=1) for y in (-0.03, 0.21)]
    s.part("Body", [*frame, lo, *lo_lips, hi, *hi_lips])

    heads, handles = [], []
    xs = (-0.52, -0.26, 0.0, 0.26, 0.52)
    for k, x in enumerate(xs):
        h, hd = dumbbell(x, -0.065, 0.42, 0.09 - 0.006 * k, 0.085, 0.12)
        heads += h
        handles.append(hd)
        h, hd = dumbbell(x, 0.09, 0.74, 0.066 - 0.004 * k, 0.065, 0.11)
        heads += h
        handles.append(hd)
    caps = [s.rbox((0.06, 0.06, 0.04), (x * 0.67, 0.17, 0.875), r=0.012, seg=1) for x in (-1, 1)]
    caps += [s.rbox((0.06, 0.06, 0.035), (x * 0.67, -0.17, 0.53), r=0.012, seg=1) for x in (-1, 1)]
    s.part("Accent", [*heads, *caps])
    s.part("Trim", handles)
    return export("dumbbell_rack")


# --------------------------------------------------------------------------- punching bag

def model_punching_bag():
    """Free-standing bag: a domed weighted base with a fill cap, a sprung steel stem and collar, a tall bag with
    stitched bands and a vertical seam, a strap cap on top."""
    base = s.lathe([(0.0, 0.0), (0.29, 0.0), (0.3, 0.012), (0.3, 0.14), (0.285, 0.19), (0.2, 0.26), (0.11, 0.3),
                    (0.06, 0.31), (0.0, 0.31)], seg=28)
    s.groove(base, "Z", 0.1, 0.012, 0.006)
    cap = s.rcyl(0.06, 0.04, (0.17, 0.0, 0.245), seg=16, r=0.01, rot=(0, 0.7, 0))
    s.part("Body", [base, cap])

    bag = s.lathe([(0.12, 0.54), (0.165, 0.56), (0.178, 0.62), (0.178, 1.48), (0.17, 1.54), (0.14, 1.58),
                   (0.08, 1.597), (0.0, 1.6)], seg=24)
    for z in (0.75, 1.32):
        s.groove(bag, "Z", z, 0.014, 0.006)
    s.groove(bag, "X", 0.0, 0.008, 0.004)
    s.part("Accent", [bag])

    stem = s.rcyl(0.035, 0.26, (0, 0, 0.42), seg=12, r=0.006, bevel_seg=1)
    coil = []
    for k in range(19):
        a = k / 18 * 3 * 2 * math.pi
        coil.append((0.05 * math.cos(a), 0.05 * math.sin(a), 0.32 + 0.17 * k / 18))
    spring = s.tube(coil, 0.011, res=1, segments_per=2)
    collar = s.lathe([(0.0, 0.48), (0.07, 0.48), (0.08, 0.49), (0.08, 0.53), (0.15, 0.545), (0.155, 0.575),
                      (0.0, 0.575)], seg=20)
    top = s.rcyl(0.07, 0.03, (0, 0, 1.603), seg=16, r=0.008, bevel_seg=1)
    flange = s.rcyl(0.08, 0.03, (0, 0, 0.31), seg=16, r=0.008, bevel_seg=1)
    s.part("Trim", [stem, spring, collar, top, flange])
    return export("punching_bag")


# --------------------------------------------------------------------------- hospital bed

def model_hospital_bed():
    """Adjustable bed 0.96 x 2.1: a white frame on four casters with two lift columns, a mattress whose head section
    (Blender +Y) is raised on a backrest, a pillow, side rails at the head half, head and foot boards, a push bar."""
    frame = s.rbox((0.66, 1.78, 0.08), (0, 0, 0.18), r=0.02, seg=1)
    cols = [s.rbox((0.26, 0.2, 0.28), (0, y, 0.33), r=0.03, seg=1) for y in (-0.45, 0.45)]
    for c in cols:
        for z in (0.3, 0.37):
            s.groove(c, "Z", z, 0.012, 0.006)
    platform = s.rbox((0.9, 2.06, 0.07), (0, 0.0, 0.48), r=0.02, seg=2)
    th = 0.45
    hinge = Vector((0, 0.2, 0.515))
    d = Vector((0, math.cos(th), math.sin(th)))
    nrm = Vector((0, -math.sin(th), math.cos(th)))
    backrest = s.rbox((0.86, 0.66, 0.05), hinge + d * 0.33 + nrm * 0.01, r=0.015, seg=2, rot=(th, 0, 0))
    head = s.rbox((0.96, 0.06, 0.62), (0, 1.02, 0.69), r=0.03, seg=3)
    s.inset_faces(head, s.facing((0, -1, 0), min_area=0.2, tol=0.95), thickness=0.06, depth=-0.012)
    foot = s.rbox((0.96, 0.06, 0.42), (0, -1.02, 0.59), r=0.03, seg=3)
    s.inset_faces(foot, s.facing((0, -1, 0), min_area=0.15, tol=0.95), thickness=0.06, depth=-0.012)
    s.part("Body", [frame, *cols, platform, backrest, head, foot])

    mattress = s.rbox((0.84, 1.2, 0.14), (0, -0.39, 0.58), r=0.05, seg=3)
    upper = s.rbox((0.84, 0.66, 0.14), hinge + d * 0.33 + nrm * 0.1, r=0.05, seg=3, rot=(th, 0, 0))
    pillow = s.rbox((0.58, 0.3, 0.11), hinge + d * 0.47 + nrm * 0.2, r=0.05, seg=2, rot=(th, 0, 0))
    s.part("Accent", [mattress, upper, pillow])

    metal = []
    for x in (-1, 1):
        for y in (-1, 1):
            cx, cy = x * 0.33, y * 0.8
            metal.append(s.rcyl(0.06, 0.045, (cx, cy, 0.06), axis="X", seg=10, r=0.012, bevel_seg=1))
            metal.append(s.rbox((0.07, 0.06, 0.07), (cx, cy, 0.125), r=0.012, seg=1))
            metal.append(s.rcyl(0.018, 0.05, (cx, cy, 0.16), seg=8, r=0.003, bevel_seg=1))
        rx = x * 0.455
        for z in (0.72, 0.86):
            metal.append(bar((rx, -0.05, z), (rx, 0.78, z), 0.014, seg=8))
        for y in (0.0, 0.7):
            metal.append(bar((rx, y, 0.49), (rx, y, 0.87), 0.014, seg=8))
            metal.append(s.rbox((0.04, 0.05, 0.05), (x * 0.44, y, 0.49), r=0.01, seg=1))
    metal.append(s.tube([(-0.4, -1.03, 0.8), (-0.37, -1.03, 0.86), (0.37, -1.03, 0.86), (0.4, -1.03, 0.8)], 0.016,
                        smooth=False, segments_per=1))
    s.part("Trim", metal)
    return export("hospital_bed")


# --------------------------------------------------------------------------- medicine cabinet

def bottle(x, y, z, r, h, cap=True):
    """Small medicine bottle standing at z: body, shoulder, neck, cap."""
    pts = [(0.0, z), (r, z), (r, z + h * 0.72), (r * 0.5, z + h * 0.84), (r * 0.55, z + h * 0.86),
           (r * 0.55, z + h), (0.0, z + h)]
    return [s.lathe(pts, seg=8, loc=(x, y, 0))]


def model_medicine_cabinet():
    """Glass-front medical cabinet against the wall (back at Blender +Y): white carcass with a cornice and plinth,
    two solid doors below, two glass doors with metal frames above, three shelves of bottles and boxes, a red-cross
    sign on top."""
    sides = [s.rbox((0.04, 0.4, 1.6), (x * 0.43, 0, 0.8), r=0.008, seg=1) for x in (-1, 1)]
    back = s.rbox((0.84, 0.03, 1.56), (0, 0.18, 0.8), r=0.005, seg=1)
    cornice = s.rbox((0.92, 0.41, 0.05), (0, -0.01, 1.615), r=0.012, seg=2)
    plinth = s.rbox((0.84, 0.34, 0.08), (0, 0.01, 0.05), r=0.01, seg=1)
    floors = [s.rbox((0.84, 0.37, 0.03), (0, 0.0, z), r=0.005, seg=1) for z in (0.1, 0.52)]
    shelves = [s.rbox((0.84, 0.33, 0.02), (0, 0.015, z), r=0.004, seg=1) for z in (0.88, 1.24)]
    doors = [s.rbox((0.4, 0.025, 0.39), (x * 0.205, -0.192, 0.31), r=0.008, seg=2) for x in (-1, 1)]
    for dd in doors:
        s.inset_faces(dd, s.facing((0, -1, 0), min_area=0.08, tol=0.95), thickness=0.035, depth=-0.005)
    sign = s.rbox((0.26, 0.03, 0.2), (0, -0.02, 1.73), r=0.012, seg=2)
    s.part("Body", [*sides, back, cornice, plinth, *floors, *shelves, sign])
    # the two lower doors swing open (Items.Storage), each with its pull
    for i, (x, dd) in enumerate(zip((-1, 1), doors)):
        pull = s.rbox((0.12, 0.03, 0.018), (x * 0.205, -0.212, 0.46), r=0.007, seg=1)
        s.part(f"BodyDoor{i}", [dd, pull])

    frames, glass = [], []
    for x in (-1, 1):
        cx = x * 0.205
        frames.append(s.rbox((0.04, 0.025, 1.02), (cx - 0.18, -0.192, 1.065), r=0.006, seg=1))
        frames.append(s.rbox((0.04, 0.025, 1.02), (cx + 0.18, -0.192, 1.065), r=0.006, seg=1))
        frames.append(s.rbox((0.32, 0.024, 0.04), (cx, -0.192, 0.575), r=0.006, seg=1))
        frames.append(s.rbox((0.32, 0.024, 0.04), (cx, -0.192, 1.555), r=0.006, seg=1))
        glass.append(s.rbox((0.34, 0.006, 0.98), (cx, -0.192, 1.065), r=0.002, seg=1))
    handles = [s.rbox((0.018, 0.03, 0.16), (x * 0.04, -0.212, 1.05), r=0.007, seg=1) for x in (-1, 1)]
    s.part("Accent", [*frames, *handles])

    stuff = []
    for z, row in ((0.535, ((-0.33, "b", 0.035, 0.16), (-0.25, "x", 0.1, 0.12), (-0.14, "b", 0.03, 0.12),
                            (-0.07, "b", 0.03, 0.14), (0.06, "x", 0.12, 0.18), (0.2, "b", 0.04, 0.2),
                            (0.3, "b", 0.035, 0.15))),
                   (0.89, ((-0.32, "x", 0.12, 0.1), (-0.32, "x2", 0.1, 0.08), (-0.18, "b", 0.03, 0.13),
                           (-0.1, "b", 0.03, 0.13), (-0.02, "b", 0.03, 0.13), (0.12, "b", 0.045, 0.22),
                           (0.27, "x", 0.14, 0.14))),
                   (1.25, ((-0.3, "b", 0.04, 0.18), (-0.2, "b", 0.035, 0.15), (-0.06, "x", 0.16, 0.09),
                           (0.1, "b", 0.03, 0.12), (0.17, "b", 0.03, 0.1), (0.3, "x", 0.1, 0.16)))):
        for x, kind, w, h in row:
            if kind == "b":
                stuff += bottle(x, 0.02, z - 0.004, w, h)
            elif kind == "x":
                stuff.append(s.rbox((w, 0.12, h), (x, 0.03, z - 0.004 + h / 2), r=0.006, seg=1))
            else:  # a box on top of the previous box
                stuff.append(s.rbox((w, 0.1, h), (x, 0.03, z + 0.1 - 0.008 + h / 2), r=0.006, seg=1))
    cross = [s.rbox((0.16, 0.02, 0.05), (0, -0.04, 1.73), r=0.005, seg=1),
             s.rbox((0.05, 0.02, 0.16), (0, -0.041, 1.73), r=0.005, seg=1)]
    s.part("Trim", [*stuff, *cross])
    s.part("Eye", glass)
    return export("medicine_cabinet")


# --------------------------------------------------------------------------- privacy screen

def model_privacy_screen():
    """Folding three-panel screen standing in a zig-zag: tube posts with ball finials and round feet, top and bottom
    rails, hinge clips, fabric panels between the posts."""
    pts = [(-0.75, -0.08), (-0.25, 0.08), (0.25, -0.08), (0.75, 0.08)]
    frame, fabric = [], []
    for x, y in pts:
        frame.append(s.rcyl(0.016, 1.62, (x, y, 0.85), seg=10, r=0.004, bevel_seg=1))
        frame.append(s.ball(0.024, (x, y, 1.668), seg=10))
        frame.append(s.rcyl(0.05, 0.03, (x, y, 0.015), seg=14, r=0.008, bevel_seg=1))
        frame.append(s.rcyl(0.022, 0.06, (x, y, 0.05), seg=10, r=0.006, bevel_seg=1))
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        for z in (0.3, 1.6):
            frame.append(bar((x0, y0, z), (x1, y1, z), 0.012, seg=8))
        a = math.atan2(y1 - y0, x1 - x0)
        ln = math.hypot(x1 - x0, y1 - y0)
        fabric.append(s.rbox((ln - 0.02, 0.012, 1.29), ((x0 + x1) / 2, (y0 + y1) / 2, 0.95), r=0.004, seg=1,
                             rot=(0, 0, a)))
    for x, y in pts[1:3]:
        for z in (0.65, 1.25):
            frame.append(s.rcyl(0.026, 0.05, (x, y, z), seg=10, r=0.005, bevel_seg=1))
    s.part("Body", frame)
    s.part("Accent", fabric)
    return export("privacy_screen")


# --------------------------------------------------------------------------- sleep pod

def pod_rings(k):
    zc = 0.6
    return [(-1.0 * k, 0.24 * k, 0.21 * k, 2.4), (-0.96 * k, 0.4 * k, 0.35 * k, 2.4), (-0.86 * k, 0.51 * k, 0.44 * k, 2.5),
            (-0.6 * k, 0.55 * k, 0.475 * k, 2.6), (0.6 * k, 0.55 * k, 0.475 * k, 2.6), (0.86 * k, 0.51 * k, 0.44 * k, 2.5),
            (0.96 * k, 0.4 * k, 0.35 * k, 2.4), (1.0 * k, 0.24 * k, 0.21 * k, 2.4)], zc


def model_sleep_pod():
    """Nap pod: a hollow rounded capsule along Y on a plinth, a large oval opening on its +X side (toward the
    front) showing a mattress and a head cushion inside, a soft light strip under the ceiling, a rim around the
    opening."""
    rings, zc = pod_rings(1.0)
    shell = s.loft(rings, seg=32, axis="Y", loc=(0, 0, zc))
    inner_rings, _ = pod_rings(0.93)
    inner = s.loft(inner_rings, seg=32, axis="Y", loc=(0, 0, zc))
    shell = s.boolean(shell, inner, "DIFFERENCE")
    cut = s.loft([(0.0, 0.3, 0.62, 2.3), (0.8, 0.3, 0.62, 2.3)], seg=40, axis="X", loc=(0, -0.08, zc + 0.04))
    shell = s.boolean(shell, cut, "DIFFERENCE")
    s.bevel_edges(shell, 0.008, 2, angle=50)
    plinth = s.rbox((0.8, 1.6, 0.16), (0, 0, 0.08), r=0.03, seg=2, taper=(1.0, 1.0))
    s.inset_faces(plinth, s.facing((0, -1, 0), min_area=0.05, tol=0.95), thickness=0.02, depth=-0.01)
    s.part("Body", [shell, plinth])

    mattress = s.rbox((0.74, 1.6, 0.14), (0, 0.0, 0.33), r=0.05, seg=3)
    cushion = s.rbox((0.5, 0.22, 0.12), (0, 0.66, 0.44), r=0.05, seg=3, rot=(0.25, 0, 0))
    s.part("Accent", [mattress, cushion])

    strip = s.rbox((0.06, 1.2, 0.03), (0.08, -0.05, 1.03), r=0.01, seg=1)
    lamp = s.rcyl(0.05, 0.05, (-0.25, 0.6, 0.98), seg=16, r=0.01, rot=(0, -0.5, 0))
    glow = s.rbox((0.84, 1.64, 0.02), (0, 0, 0.145), r=0.008, seg=1)
    s.part("Eye", [strip, lamp, glow])
    return export("sleep_pod")


# --------------------------------------------------------------------------- hammock

def model_hammock():
    """Hammock along X on its own stand: two arched steel arms joined by floor feet and top hook bars, a fabric bed
    sagging between two spreader bars, short chains from the bars to the hooks."""
    arc = [(-1.24, 1.13), (-1.14, 0.9), (-0.96, 0.38), (-0.6, 0.09), (0.0, 0.06), (0.6, 0.09), (0.96, 0.38),
           (1.14, 0.9), (1.24, 1.13)]
    stand = []
    for y in (-0.3, 0.3):
        stand.append(s.tube([(x, y, z) for x, z in arc], 0.03, res=2, segments_per=3))
    for x in (-0.55, 0.55):
        stand.append(s.rbox((0.08, 0.9, 0.06), (x, 0, 0.03), r=0.02, seg=2))
    stand.append(s.rbox((0.06, 0.64, 0.05), (0, 0, 0.06), r=0.015, seg=2))
    for x in (-1, 1):
        stand.append(bar((x * 1.24, -0.33, 1.13), (x * 1.24, 0.33, 1.13), 0.028))
        stand.append(s.rcyl(0.035, 0.05, (x * 1.24, 0, 1.11), seg=12, r=0.008))
        stand.append(s.tube([(x * 1.24, 0, 1.1), (x * 1.18, 0, 1.03), (x * 1.08, 0, 0.95)], 0.009, res=1,
                            segments_per=3))
    for x in (-1, 1):
        stand.append(bar((x * 1.02, -0.36, 0.93), (x * 1.02, 0.36, 0.93), 0.02))
    s.part("Body", stand)

    rings = []
    for k in range(13):
        t = -1.0 + 2 * k / 12
        x = t * 1.0
        z = 0.48 + 0.45 * t * t
        w = 0.36 - 0.06 * t * t
        rings.append((x, 0.018, w, 2.2, -z, 0.0))
    bed = s.loft(rings, seg=20, axis="X")
    pillow = s.rbox((0.22, 0.42, 0.08), (-0.74, 0, 0.77), r=0.035, seg=3, rot=(0, 0.6, 0))
    s.part("Accent", [bed, pillow])
    return export("hammock")


MODELS = {
    "treadmill": model_treadmill,
    "exercise_bike": model_exercise_bike,
    "weight_bench": model_weight_bench,
    "dumbbell_rack": model_dumbbell_rack,
    "punching_bag": model_punching_bag,
    "hospital_bed": model_hospital_bed,
    "medicine_cabinet": model_medicine_cabinet,
    "privacy_screen": model_privacy_screen,
    "sleep_pod": model_sleep_pod,
    "hammock": model_hammock,
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
