"""The team's pickup: a rugged junkyard truck the crew drives through the sewer between houses. The loading screen
shows it in side profile, driving; the engine "eats anything" — loot goes into the hopper on the hood as fuel.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/car.py -- Assets/_Game/Art/Models
      (writes <root>/Car/pickup.fbx)
Then: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png Assets/_Game/Art/Models/Car/pickup.fbx

Contract (metres, Blender: front -Y = Unity +Z, z up, origin on the ground in the middle of the wheelbase):
  length 4.9 (y -2.45 .. +2.45), width 2.0 with the wheels, cab roof z 1.85
  wheels r 0.42, width 0.3, centres x = +-0.82, y = -1.5 (front) / +1.5 (rear), z 0.42
  seats: driver (-0.42, 0.05), passenger (0.42, 0.05), cushion top z 0.75, backrest to z 1.3
  steering wheel hub (-0.42, -0.45, 1.12) facing the driver
  cargo bed: floor top z 0.85, inside x -0.85..0.85, y 0.35..2.35, walls and tailgate to z 1.3 — empty and flat
  hopper mouth centre (0, -1.55, 1.56), radius 0.33, a toothed grinder drum inside spinning about x
Parts: Body, Accent, Dark, Glass, Seat, EyeHead, EyeTail, EyeSpot, EyeDome (the cab's dome light), DarkDome (merged by role) and the animated pivots
AccentWheelFL/FR/RL/RR (origin at the wheel centre; L = -x), AccentSteering (hub), AccentHopperGrinder (spin axis).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

WHEEL_R = 0.42
WHEEL_W = 0.30
WHEEL_X = 0.82
AXLE_Y = 1.5
ARCH_R = 0.49
SILL_Z = 0.56              # bottom of the body panels
HOOD_END_Y = -0.74         # windshield base
CAB_BACK_Y = 0.27
BED_FRONT_Y = 0.28
BED_FLOOR_Z = 0.85
BED_TOP_Z = 1.30
BED_IN_X = 0.85
BED_OUT_X = 0.93
BED_REAR_Y = 2.42
STEER_HUB = Vector((-0.42, -0.45, 1.12))
STEER_AXIS = Vector((0, math.cos(math.radians(25)), math.sin(math.radians(25))))  # wheel face -> driver
HOPPER = Vector((0, -1.55, 0))
GRINDER = Vector((0, -1.55, 1.425))
RIM_Z = 1.56
MOTOR = Vector((0, -1.23, 1.30))   # grinder motor drum behind the funnel, axis x
SPARE_Z = 1.905 + 0.15 * 0.75   # centre of the spare lying on the roof rack
PIVOTS = ("AccentWheel", "AccentSteering", "AccentHopperGrinder")


# --------------------------------------------------------------------------- helpers

def prism(profile, axis, a0, a1, r=0.03, seg=2):
    """A side-profile polygon extruded across `axis` from a0 to a1 (profile in the other two axes, in order
    y,z for X; x,z for Y), its outline bevelled — fenders with arches, wedge hoods, bed walls."""
    bm = bmesh.new()

    def p(u, v, a):
        return {"X": (a, u, v), "Y": (u, a, v), "Z": (u, v, a)}[axis]

    lo = [bm.verts.new(p(u, v, a0)) for u, v in profile]
    hi = [bm.verts.new(p(u, v, a1)) for u, v in profile]
    n = len(profile)
    for k in range(n):
        bm.faces.new((lo[k], lo[(k + 1) % n], hi[(k + 1) % n], hi[k]))
    bm.faces.new(lo)
    bm.faces.new(list(reversed(hi)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if r:
        s.bevel_edges(bm, r, seg, angle=30)
    return bm


def arc(cy, cz, radius, t0, t1, n):
    return [(cy + radius * math.cos(t0 + (t1 - t0) * k / (n - 1)), cz + radius * math.sin(t0 + (t1 - t0) * k / (n - 1)))
            for k in range(n)]


def arch_points(cy, n=15, radius=ARCH_R, reverse=False):
    """The wheel arch above SILL_Z: from the rear end over the top to the front end (reverse: front to rear)."""
    t0 = math.asin((SILL_Z - WHEEL_R) / radius)
    pts = arc(cy, WHEEL_R, radius, t0, math.pi - t0, n)
    return list(reversed(pts)) if reverse else pts


def revolve(profile, seg=32, radial=None):
    """Closed (r, z) profile revolved about +Z: a ring-shaped solid with no caps (tyre, torus, funnel wall).
    radial(i, k, r) -> r changes the radius per angle step i and profile point k (tyre tread)."""
    bm = bmesh.new()
    rings = []
    for i in range(seg):
        t = 2 * math.pi * i / seg
        c, sn = math.cos(t), math.sin(t)
        ring = []
        for k, (r, z) in enumerate(profile):
            rr = radial(i, k, r) if radial else r
            ring.append(bm.verts.new((rr * c, rr * sn, z)))
        rings.append(ring)
    n = len(profile)
    for i in range(seg):
        a, b = rings[i], rings[(i + 1) % seg]
        for k in range(n):
            k2 = (k + 1) % n
            bm.faces.new((a[k], b[k], b[k2], a[k2]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def torus(big, small, seg=32, small_seg=8):
    prof = [(big + small * math.cos(2 * math.pi * k / small_seg), small * math.sin(2 * math.pi * k / small_seg))
            for k in range(small_seg)]
    return revolve(prof, seg)


def orient(bm, direction, loc=(0, 0, 0)):
    """Turn a shape built along +Z so +Z points along `direction`, then move it to loc."""
    q = Vector((0, 0, 1)).rotation_difference(Vector(direction).normalized())
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return bm


def spike(base, tip, radius, seg=5):
    """A pointed tooth from base to tip."""
    d = Vector(tip) - Vector(base)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=radius, radius2=0.0, depth=d.length)
    bmesh.ops.translate(bm, vec=Vector((0, 0, d.length / 2)), verts=bm.verts)
    return orient(bm, d, base)


def studs(points, radius, depth, axis):
    """Plain hex bolt heads (cheap: 20 triangles each)."""
    return [s.rcyl(radius, depth, p, axis=axis, seg=6, r=0.0) for p in points]


def lean(bm, x_in, x_out, dx, z0, z1):
    """Tumblehome of a side panel (+x side, mirrored later): the outer face x_out leans in by dx from z0 to z1, the
    inner face x_in stays, the thickness between blends (no folds while dx < the thickness)."""
    for v in bm.verts:
        w = max(0.0, min(1.0, (v.co.x - x_in) / (x_out - x_in)))
        v.co.x -= w * dx * max(0.0, min(1.0, (v.co.z - z0) / (z1 - z0)))
    return bm


def set_pivot(obj, point):
    """Origin to `point` (world) without moving the geometry: the game spins / steers the part about it."""
    obj.data.transform(Matrix.Translation(-Vector(point)))
    obj.location = Vector(point)
    return obj


# --------------------------------------------------------------------------- body (painted metal)

def front_body():
    """Hood and front fenders in one wedge: blunt nose, hood rising to the windshield, a big arch over the wheel."""
    prof = [(-2.32, SILL_Z), (-2.32, 1.07), (-2.27, 1.15), (HOOD_END_Y, 1.235), (HOOD_END_Y, SILL_Z)]
    prof += arch_points(-AXLE_Y)
    bm = prism(prof, "X", -0.93, 0.93, r=0.0)
    for v in bm.verts:  # tumblehome: the hood top is a little narrower than the fenders' feet
        if v.co.z > 0.95:
            v.co.x *= 1 - 0.045 * (v.co.z - 0.95) / 0.3
    s.bevel_edges(bm, 0.065, 2, angle=30)
    for x in (-0.6, 0.6):
        s.groove(bm, "X", x, 0.012, 0.006)          # hood / fender seams
    # raised centre of the hood that carries the hopper (follows the hood's rise to the windshield)
    bulge = s.rbox((1.1, 1.36, 0.08), (0, -1.48, 1.205), r=0.03, seg=2, taper=(0.88, 0.96),
                   rot=(math.atan2(0.085, 1.53), 0, 0))
    return [bm, bulge]


def cab():
    parts = []
    door = s.rbox((0.07, 1.03, 0.69), (0.90, -0.245, 0.905), r=0.03, seg=2)
    s.groove(door, "Y", 0.17, 0.012, 0.006)          # door rear seam
    lean(door, 0.865, 0.935, 0.02, SILL_Z, 1.25)
    parts += s.mirror_x(door)
    parts.append(s.rbox((1.80, 0.065, 0.68), (0, 0.2325, 0.90), r=0.02, seg=1))             # rear wall below the window
    parts += s.mirror_x(s.rbox((0.08, 0.12, 0.60), (0.895, 0.205, 1.51), r=0.025, seg=1))  # rear posts
    tilt = -math.atan2(0.44, 0.55)
    parts += s.mirror_x(s.rbox((0.075, 0.08, 0.76), (0.895, -0.52, 1.515), r=0.025, seg=1, rot=(tilt, 0, 0)))  # A-pillars
    parts.append(s.rbox((1.86, 0.64, 0.085), (0, -0.04, 1.8075), r=0.035, seg=2, taper=(0.98, 0.97)))         # roof
    return parts


def bed():
    parts = []
    prof = [(BED_FRONT_Y + 0.02, SILL_Z), (BED_FRONT_Y + 0.02, BED_TOP_Z), (BED_REAR_Y, BED_TOP_Z), (BED_REAR_Y, SILL_Z)]
    prof += arch_points(AXLE_Y, n=13)
    wall = prism(prof, "X", BED_IN_X, BED_OUT_X, r=0.022, seg=2)
    lean(wall, BED_IN_X, BED_OUT_X, 0.025, SILL_Z, BED_TOP_Z)
    s.groove(wall, "Z", 1.04, 0.014, 0.007)          # pressed body line
    parts += s.mirror_x(wall)
    parts.append(s.rbox((1.86, 0.07, 0.74), (0, 0.315, 0.93), r=0.022, seg=2))               # front wall
    parts += s.mirror_x(s.rbox((0.105, 2.15, 0.05), (0.8975, 1.345, 1.305), r=0.018, seg=1))  # rail caps
    # floor: flat on top; thick in the middle, a thin sheet over the wheels (the tyre top is at z 0.84)
    floor = [(-0.88, BED_FLOOR_Z), (0.88, BED_FLOOR_Z), (0.88, 0.832), (0.62, 0.832), (0.60, 0.72), (-0.60, 0.72),
             (-0.62, 0.832), (-0.88, 0.832)]
    parts.append(prism(floor, "Y", 0.30, 2.40, r=0.004, seg=1))
    gate = s.rbox((1.69, 0.07, 0.74), (0, 2.395, 0.92), r=0.02, seg=2)
    s.inset_faces(gate, s.facing((0, 1, 0), 0.3), 0.07, -0.012)                             # pressed panel
    parts.append(gate)
    return parts


def hopper_shell():
    """The funnel wall: a revolved closed profile, open at the top."""
    prof = [(0.19, 1.22), (0.235, 1.36), (0.295, 1.47), (0.372, RIM_Z - 0.005), (0.345, RIM_Z + 0.005),
            (0.272, 1.47), (0.212, 1.37), (0.165, 1.25)]
    return [s.place(revolve(prof, 24), HOPPER)]


def body():
    return front_body() + cab() + bed() + hopper_shell()


# --------------------------------------------------------------------------- accent (bars, bumpers, trim)

def accent():
    parts = []
    parts.append(s.rbox((1.96, 0.18, 0.26), (0, -2.38, 0.47), r=0.04, seg=2))               # front bumper
    parts.append(s.rbox((1.90, 0.16, 0.17), (0, 2.38, 0.50), r=0.035, seg=2))               # rear step bumper
    parts += s.mirror_x(s.rbox((0.06, 0.08, 0.1), (0.42, -2.45, 0.44), r=0.015, seg=1))     # tow hooks
    # bull bar: a hoop round the grille, uprights and a cross bar
    hoop = [(-0.56, -2.40, 0.56), (-0.56, -2.43, 1.0), (-0.44, -2.45, 1.15), (0.44, -2.45, 1.15),
            (0.56, -2.43, 1.0), (0.56, -2.40, 0.56)]
    parts.append(s.tube(hoop, 0.034, res=1, segments_per=4))
    parts.append(s.tube([(-0.56, -2.43, 0.86), (0.56, -2.43, 0.86)], 0.026, res=1, smooth=False))
    parts.append(s.rbox((1.24, 0.07, 0.1), (0, -2.43, 0.6), r=0.02, seg=1))                 # push-bar plate
    # grille slats and headlight bezels on the nose
    parts += s.grille((0, -2.345, 0.86), 0.9, 0.32, n=4, bar=0.03, proud=0.03, frame=0.03)
    parts += s.mirror_x(s.rcyl(0.125, 0.04, (0.70, -2.335, 0.92), axis="Y", seg=16, r=0.01, bevel_seg=1))
    # side steps
    parts += s.mirror_x(s.rbox((0.14, 1.86, 0.07), (0.92, 0.0, 0.545), r=0.02, seg=1))
    # mirrors
    for sx in (-1, 1):
        parts.append(s.tube([(sx * 0.92, -0.64, 1.18), (sx * 1.0, -0.64, 1.24), (sx * 1.04, -0.64, 1.30)], 0.015,
                            res=1, segments_per=4))
        parts.append(s.rbox((0.07, 0.1, 0.18), (sx * 1.045, -0.64, 1.36), r=0.02, seg=1))
    # door handles
    parts += s.mirror_x(s.rbox((0.03, 0.13, 0.03), (0.925, 0.04, 1.12), r=0.012, seg=1))
    # roll bar on the bed rails with two braces, spot lamp housings on top
    parts.append(s.tube([(-0.89, 0.34, 1.29), (-0.89, 0.34, 1.86), (-0.78, 0.34, 1.98), (0.78, 0.34, 1.98),
                         (0.89, 0.34, 1.86), (0.89, 0.34, 1.29)], 0.036, res=1, segments_per=4))
    parts += s.mirror_x(s.tube([(0.89, 0.36, 1.78), (0.89, 1.0, 1.31)], 0.026, res=1, smooth=False))
    for x in (-0.6, 0.6):
        parts.append(s.rcyl(0.085, 0.14, (x, 0.335, 2.075), axis="Y", seg=14, r=0.015, bevel_seg=1))
        parts.append(s.rbox((0.05, 0.06, 0.07), (x, 0.34, 2.0), r=0.012, seg=1))
    # roof rack: two cross bars the spare lies on, a strap over it
    for y in (-0.24, 0.16):
        parts.append(s.rbox((1.46, 0.045, 0.04), (0, y, 1.885), r=0.015, seg=1))
    top = SPARE_Z + 0.15 * 0.75
    parts.append(s.rbox((0.72, 0.05, 0.014), (0, -0.04, top + 0.004), r=0.005, seg=1))
    parts += s.mirror_x(s.rbox((0.47, 0.05, 0.014), (0.53, -0.04, (top + 1.86) / 2), r=0.005, seg=1,
                               rot=(0, math.atan2(top - 1.86, 0.36), 0)))
    parts += spare(rim_only=True)
    # tailgate handle
    parts.append(s.rbox((0.3, 0.03, 0.05), (0, 2.435, 1.18), r=0.012, seg=1))
    # hopper: motor drum caps behind the funnel, an exhaust stack, rim and teeth round the mouth
    for sx in (-1, 1):
        parts.append(s.rcyl(0.15, 0.05, (sx * 0.36, MOTOR.y, MOTOR.z), axis="X", seg=14, r=0.014, bevel_seg=1))
        parts += studs([(sx * 0.387, MOTOR.y + 0.095 * math.cos(t), MOTOR.z + 0.095 * math.sin(t))
                        for t in (0.4, 1.97, 3.54, 5.11)], 0.014, 0.012, "X" if sx > 0 else "-X")
    parts.append(s.tube([(0.26, -1.17, 1.38), (0.26, -1.17, 1.72)], 0.045, res=1, smooth=False))
    parts.append(s.place(torus(0.358, 0.034, 20, 6), (HOPPER.x, HOPPER.y, RIM_Z)))
    for k in range(10):
        t = 2 * math.pi * (k + 0.5) / 10
        c, sn = math.cos(t), math.sin(t)
        base = HOPPER + Vector((0.335 * c, 0.335 * sn, RIM_Z - 0.01))
        tip = HOPPER + Vector((0.25 * c, 0.25 * sn, RIM_Z - 0.09))
        parts.append(spike(base, tip, 0.032, seg=4))
    return parts


# --------------------------------------------------------------------------- dark (chassis, interior, rubber)

def dark():
    parts = []
    parts += s.mirror_x(s.rbox((0.12, 4.5, 0.17), (0.5, 0.05, 0.49), r=0.02, seg=1))       # frame rails
    for y in (-AXLE_Y, AXLE_Y):
        parts.append(s.rcyl(0.055, 1.52, (0, y, WHEEL_R), axis="X", seg=8, r=0.01, bevel_seg=1))         # axles into the hubs
    parts.append(s.rbox((1.24, 1.08, 0.66), (0, -AXLE_Y, 0.64), r=0.03, seg=1))             # engine / inner wheel houses
    parts.append(s.rbox((1.2, 2.04, 0.22), (0, 1.33, 0.63), r=0.02, seg=1))                 # bed sub-frame
    parts.append(s.rbox((1.0, 0.03, 0.36), (0, -2.325, 0.86), r=0.008, seg=1))              # grille backing
    parts.append(s.rbox((0.2, 0.1, 0.08), (0, 2.42, 0.48), r=0.01, seg=1))                  # hitch receiver
    # cab interior
    parts.append(s.rbox((1.74, 0.98, 0.1), (0, -0.25, 0.60), r=0.01, seg=1))                # floor
    parts.append(s.rbox((1.72, 0.06, 0.5), (0, -0.73, 0.75), r=0.01, seg=1))                # firewall
    parts.append(s.rbox((1.72, 0.22, 0.22), (0, -0.65, 1.07), r=0.04, seg=2, taper=(1.0, 0.8)))  # dashboard
    col = s.rcyl(0.026, 0.28, (0, 0, 0), axis="Z", seg=10, r=0.006, bevel_seg=1)
    parts.append(orient(col, STEER_AXIS, STEER_HUB - STEER_AXIS * 0.15))                     # steering column
    # wheel arch flares (rubber), mud flaps
    for cy in (-AXLE_Y, AXLE_Y):
        pts = arch_points(cy, n=13, radius=ARCH_R + 0.02)
        parts += s.mirror_x(s.tube([(0.94, y, z) for y, z in pts], 0.06, res=1, smooth=False))
    for y in (-AXLE_Y + 0.52, AXLE_Y + 0.52):
        parts += s.mirror_x(s.rbox((0.3, 0.04, 0.46), (0.82, y, 0.37), r=0.012, seg=1, rot=(math.radians(-6), 0, 0)))
    # hopper: grinder housing on the hood, the floor of the funnel
    parts.append(s.rbox((0.6, 0.48, 0.07), (HOPPER.x, HOPPER.y, 1.235), r=0.025, seg=2))
    parts.append(s.rcyl(0.13, 0.68, MOTOR, axis="X", seg=16, r=0.014, bevel_seg=1))             # motor drum
    parts.append(s.rcyl(0.058, 0.05, (0.26, -1.17, 1.735), seg=12, r=0.01, bevel_seg=1))         # stack cap
    parts.append(s.rcyl(0.2, 0.05, (HOPPER.x, HOPPER.y, 1.255), seg=20, r=0.005, bevel_seg=1))  # funnel floor
    parts += spare(rim_only=False)
    return parts


# --------------------------------------------------------------------------- wheels

TYRE = [  # (r, w, tread row) along the axle, w = +0.15 is the outer side
    (0.245, -0.125, 0), (0.35, -0.15, 0), (0.398, -0.137, 0), (0.42, -0.085, 1), (0.42, -0.018, 1),
    (0.42, 0.018, 2), (0.42, 0.085, 2), (0.398, 0.137, 0), (0.35, 0.15, 0), (0.245, 0.125, 0),
]
RIM = [(0.0, -0.11), (0.25, -0.11), (0.258, -0.095), (0.258, 0.095), (0.24, 0.112), (0.205, 0.078), (0.12, 0.072),
       (0.09, 0.098), (0.055, 0.112), (0.0, 0.112)]


def tread(i, k, r):
    row = TYRE[k][2]
    if not row:
        return r
    lug = (i // 2) % 2 == 0
    if row == 2:
        lug = not lug
    return r if lug else r - 0.024


def wheel_parts(scale=1.0, width=1.0, lugs=True, seg=32):
    """Tyre and steel rim around +Z (outer side +Z), centred on the origin."""
    prof = [(r * scale, w * width) for r, w, _ in TYRE]
    tyre = revolve(prof, seg, radial=(lambda i, k, r: tread(i, k, r)) if lugs else None)
    rim = [s.lathe([(r * scale, w * width) for r, w in RIM], seg=12)]
    for k in range(5):
        t = 2 * math.pi * k / 5
        rim.append(s.rcyl(0.016 * scale, 0.03, (0.15 * scale * math.cos(t), 0.15 * scale * math.sin(t), 0.082 * width),
                          seg=6, r=0.0))
    return tyre, rim


def wheel(name, x_sign, y):
    tyre, rim = wheel_parts()
    centre = Vector((x_sign * WHEEL_X, y, WHEEL_R))
    for bm in [tyre, *rim]:
        orient(bm, (x_sign, 0, 0), centre)
    obj = s.part(name, [tyre, *rim])
    return set_pivot(obj, centre)


def spare(rim_only):
    """The spare lies flat on the roof rack: its tyre is Dark, its rim Accent."""
    tyre, rim = wheel_parts(scale=0.76, width=0.75, lugs=False, seg=16)
    loc = (0, -0.04, SPARE_Z)
    if rim_only:
        tyre.free()
        return [s.place(b, loc) for b in rim]
    for b in rim:
        b.free()
    return [s.place(tyre, loc)]


# --------------------------------------------------------------------------- animated small parts

def steering():
    ring = torus(0.18, 0.022, 20, 6)
    hub = s.rcyl(0.055, 0.05, (0, 0, -0.01), seg=12, r=0.012, bevel_seg=1)
    spokes = []
    for t in (math.pi / 2 + math.pi, math.radians(-20), math.radians(200)):
        d = Vector((math.cos(t), math.sin(t), 0))
        sp = s.rbox((0.15, 0.035, 0.016), (0, 0, 0), r=0.006, seg=1)
        bmesh.ops.rotate(sp, cent=(0, 0, 0), matrix=Matrix.Rotation(t, 3, "Z"), verts=sp.verts)
        bmesh.ops.translate(sp, vec=d * 0.115 + Vector((0, 0, -0.006)), verts=sp.verts)
        spokes.append(sp)
    parts = [ring, hub, *spokes]
    for bm in parts:
        orient(bm, STEER_AXIS, STEER_HUB)
    return set_pivot(s.part("AccentSteering", parts), STEER_HUB)


def grinder():
    """A toothed drum across the funnel throat, its shaft in the funnel walls."""
    parts = [s.rcyl(0.085, 0.26, (0, 0, 0), axis="X", seg=14, r=0.012),
             s.rcyl(0.026, 0.50, (0, 0, 0), axis="X", seg=8, r=0.006, bevel_seg=1)]
    for row, x in enumerate((-0.09, -0.03, 0.03, 0.09)):
        for k in range(7):
            t = 2 * math.pi * (k + 0.5 * (row % 2)) / 7
            d = Vector((0, math.cos(t), math.sin(t)))
            parts.append(spike(Vector((x, 0, 0)) + d * 0.075, Vector((x, 0, 0)) + d * 0.135, 0.026, seg=4))
    for bm in parts:
        bmesh.ops.translate(bm, vec=GRINDER, verts=bm.verts)
    return set_pivot(s.part("AccentHopperGrinder", parts), GRINDER)


# --------------------------------------------------------------------------- glass, seats, lights

def glass():
    tilt = -math.atan2(0.44, 0.55)
    parts = [s.rbox((1.74, 0.014, 0.74), (0, -0.52, 1.515), r=0.005, seg=1, rot=(tilt, 0, 0)),   # windshield
             s.rbox((1.72, 0.012, 0.56), (0, 0.235, 1.505), r=0.004, seg=1)]                       # rear window
    side = prism([(-0.71, 1.235), (0.17, 1.235), (0.17, 1.785), (-0.27, 1.785)], "X", 0.885, 0.9, r=0.003, seg=1)
    parts += s.mirror_x(side)
    parts += s.mirror_x(s.rbox((0.05, 0.012, 0.13), (1.045, -0.586, 1.36), r=0.004, seg=1))       # mirror glass
    return parts


def seats():
    parts = []
    for x in (-0.42, 0.42):
        parts.append(s.rbox((0.5, 0.44, 0.15), (x, -0.01, 0.675), r=0.05, seg=2))              # cushion, top z 0.75
        parts.append(s.rbox((0.5, 0.13, 0.6), (x, 0.125, 1.0), r=0.05, seg=1, rot=(math.radians(-9), 0, 0)))
        parts.append(s.rbox((0.28, 0.1, 0.12), (x, 0.17, 1.33), r=0.035, seg=1))                # headrest
    return parts


def head_lights():
    return s.mirror_x(s.rcyl(0.095, 0.04, (0.70, -2.345, 0.92), axis="Y", seg=14, r=0.012, bevel_seg=1))


def tail_lights():
    parts = s.mirror_x(s.rbox((0.07, 0.04, 0.26), (0.89, 2.43, 1.08), r=0.012, seg=1))
    return parts


def spot_lights():
    return [s.rcyl(0.07, 0.02, (x, 0.262, 2.075), axis="Y", seg=14, r=0.006, bevel_seg=1) for x in (-0.6, 0.6)]


def dome_light():
    """The cab's dome light under the roof, over the seats' middle: a warm lens (EyeDome)."""
    return [s.rbox((0.24, 0.13, 0.022), (0, -0.12, 1.752), r=0.01, seg=2)]


def dome_bezel():
    """Its dark plastic frame against the headliner (DarkDome)."""
    return [s.rbox((0.29, 0.17, 0.014), (0, -0.12, 1.761), r=0.012, seg=2)]


# --------------------------------------------------------------------------- build and export

def build():
    s.clear_scene()
    objs = [s.part("Body", body()), s.part("Accent", accent()), s.part("Dark", dark()), s.part("Glass", glass()),
            s.part("Seat", seats()), s.part("EyeHead", head_lights()), s.part("EyeTail", tail_lights()),
            s.part("EyeSpot", spot_lights()), s.part("EyeDome", dome_light()), s.part("DarkDome", dome_bezel())]
    objs += [wheel("AccentWheelFL", -1, -AXLE_Y), wheel("AccentWheelFR", 1, -AXLE_Y),
             wheel("AccentWheelRL", -1, AXLE_Y), wheel("AccentWheelRR", 1, AXLE_Y)]
    objs += [steering(), grinder()]
    return objs


if __name__ == "__main__":
    root = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.getcwd(), "Assets", "_Game",
                                                                                   "Art", "Models")
    objs = build()
    bpy.context.view_layer.update()  # pivots moved: meshfix reads matrix_world
    meshfix.fix_objects(objs, floor=False, name="pickup", pinned=lambda n: n.startswith(PIVOTS))
    s.export_fbx(os.path.join(root, "Car", "pickup.fbx"), objs)
