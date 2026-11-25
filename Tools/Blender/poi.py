"""Points of interest and their items: crafted loot (diamond, key block, stun gadget), the lure speaker, the medical
table, the workbench, the robot programming console, a searchable crate, a door key lock, the Warden robot and the
shovel (the first real loot, item_shovel_model; in grids and the cart it is its T piece, item_tet_t).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/poi.py -- Assets/_Game/Art/Models [--only a,b]
      (files land in the sub-folders generate_models.category picks: item_* -> Items, robot_* -> Robots, else Props)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--focus x,y,z,r] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), z up, origin on the floor in the middle of
the footprint unless a model says otherwise. Object names are material roles: Body, Accent*, Eye* (glows).
Forms come from shapes.py (rounded boxes, lofts, lathes, seams) and are finished by shapes.part (smooth shading, sharp
edges, weighted normals); export() goes through meshfix like generate_models.export, but keeps that shading
(generate_models.export flattens every face, which turns lathes and bevels into facets). The diamond is flat on purpose.
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir, category(), set_pivot(), ITEM_CELL)
import meshfix  # noqa: E402
import printed  # noqa: E402
import shapebake  # noqa: E402
import shapes as s  # noqa: E402

MOVING = ("AccentWheel", "AccentHead", "AccentLid")  # parts the game moves: meshfix must not shift them off their pivots


def export(filename):
    """generate_models.export with smooth shading kept: meshfix, then the FBX into Art/Models/<category>/."""
    import bpy
    bpy.context.view_layer.update()  # set_pivot moved origins: matrix_world must be current for meshfix
    objs = bpy_objects()
    meshfix.fix_objects(objs, floor=g.category(filename) in ("Props", "Items", "Level"), name=filename,
                        pinned=lambda n: n.startswith(MOVING))
    path = os.path.join(g.out_dir, g.category(filename), filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


def bpy_objects():
    import bpy
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


# --------------------------------------------------------------------------- extra shapes

def ring(r_out, r_in, depth, loc=(0, 0, 0), axis="Z", seg=32, r=0.003):
    """Flat ring (washer, bezel, LED halo) along an axis, edges chamfered."""
    bm = bmesh.new()
    loops = []
    for rad, z in ((r_out, -depth / 2), (r_out, depth / 2), (r_in, depth / 2), (r_in, -depth / 2)):
        loops.append([bm.verts.new((rad * math.cos(2 * math.pi * k / seg), rad * math.sin(2 * math.pi * k / seg), z))
                      for k in range(seg)])
    for a, b in zip(loops, loops[1:] + loops[:1]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.bevel_edges(bm, min(r, 0.3 * min(depth, r_out - r_in)), 1, angle=50)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


def sweep_rect(path, x, width, thick, r=0.01):
    """A bar of rectangular section (width along X, thick across the path) swept along a path in the YZ plane
    (list of (y, z)); ends capped, edges rounded. Arches, frames, bent rails."""
    bm = bmesh.new()
    secs = []
    n = len(path)
    for k, (y, z) in enumerate(path):
        p0 = Vector(path[max(0, k - 1)])
        p1 = Vector(path[min(n - 1, k + 1)])
        t = (p1 - p0).normalized()
        nrm = Vector((-t.y, t.x))
        c = Vector((y, z))
        sec = []
        for dx, dn in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            q = c + nrm * (dn * thick / 2)
            sec.append(bm.verts.new((x + dx * width / 2, q.x, q.y)))
        secs.append(sec)
    for a, b in zip(secs, secs[1:]):
        for j in range(4):
            bm.faces.new((a[j], a[(j + 1) % 4], b[(j + 1) % 4], b[j]))
    bm.faces.new(secs[0][::-1])
    bm.faces.new(secs[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.bevel_edges(bm, min(r, 0.3 * min(width, thick)), 2, angle=40)
    return bm


def arch_path(radius, z_foot, z_spring, seg=16):
    """Points of an upright arch in the YZ plane: two straight legs from z_foot up to z_spring, a half circle on top."""
    pts = [(-radius, z_foot), (-radius, (z_foot + z_spring) / 2)]
    for k in range(seg + 1):
        a = math.pi - math.pi * k / seg
        pts.append((radius * math.cos(a), z_spring + radius * math.sin(a)))
    pts += [(radius, (z_foot + z_spring) / 2), (radius, z_foot)]
    return pts


def place_on(bms, loc=(0, 0, 0), rot=(0, 0, 0)):
    for b in bms:
        s.place(b, loc, rot)
    return bms


# --------------------------------------------------------------------------- items (one inventory cell = 0.22 m)

def model_item_diamond():
    """Brilliant-cut gem standing on its culet: 8 pavilion mains, a girdle band, a crown of bezels and stars around an
    octagonal table. Flat-shaded so every facet catches its own light. One object (the game tints it)."""
    R = 0.1
    z_mid, z_gb, z_gt, z_bez, z_tab = 0.046, 0.095, 0.104, 0.127, 0.153
    bm = bmesh.new()
    ang = lambda k, n: 2 * math.pi * k / n  # noqa: E731

    def circle(n, rad, z, off=0.0):
        return [bm.verts.new((rad * math.cos(ang(k + off, n)), rad * math.sin(ang(k + off, n)), z)) for k in range(n)]

    culet = bm.verts.new((0, 0, 0))
    p = circle(8, R * 0.53, z_mid)
    gb = circle(16, R, z_gb)
    gt = circle(16, R, z_gt)
    q = circle(8, R * 0.83, z_bez, 0.5)   # bezel corners sit over the odd girdle points
    t = circle(8, R * 0.56, z_tab)
    F = bm.faces.new
    for i in range(8):
        j = (i + 1) % 8
        F((culet, p[j], p[i]))
        F((p[i], gb[2 * i], gb[2 * i + 1]))
        F((p[i], gb[2 * i + 1], p[j]))
        F((p[j], gb[2 * i + 1], gb[(2 * i + 2) % 16]))
    for k in range(16):
        F((gb[k], gb[(k + 1) % 16], gt[(k + 1) % 16], gt[k]))
    for i in range(8):
        j, h = (i + 1) % 8, (i - 1) % 8
        F((gt[2 * i], gt[2 * i + 1], q[i]))
        F((gt[2 * i + 1], gt[(2 * i + 2) % 16], q[i]))
        F((gt[2 * i], q[i], t[i]))
        F((gt[2 * i], t[i], q[h]))
        F((q[i], t[j], t[i]))
    F(t)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.part("Body", [bm], flat=True)
    return export("item_diamond")


def key_shape(z0, h):
    """Key lying along +X centred on the origin: ring bow, shaft, two bits; bottom at z0, height h."""
    zc = z0 + h / 2
    bow = s.rcyl(0.04, h, (-0.058, 0, zc), seg=20, r=0.006)
    bow = s.boolean(bow, s.rcyl(0.017, h * 3, (-0.058, 0, zc), seg=20, r=0.0), "DIFFERENCE")
    s.bevel_edges(bow, 0.003, 1, angle=50)
    shaft = s.rbox((0.135, 0.026, h), (0.028, 0, zc), r=0.006, seg=2)
    bits = [s.rbox((0.019, 0.034, h), (x, -0.026, zc), r=0.005, seg=1) for x in (0.058, 0.084)]
    tip = s.rbox((0.012, 0.034, h), (0.093, -0.004, zc), r=0.004, seg=1)
    collar = s.rbox((0.012, 0.042, h), (-0.012, 0, zc), r=0.004, seg=1)
    return [bow, shaft, *bits, tip, collar]


def model_item_key():
    """Key block: a chunky rounded cube with a sunken top panel and a big key standing out of it on the diagonal."""
    block = s.rbox((0.2, 0.2, 0.15), (0, 0, 0.075), r=0.02, seg=3)
    s.groove(block, "Z", 0.045, 0.012, 0.005)
    s.inset_faces(block, s.facing((0, 0, 1), min_area=0.01, tol=0.95), thickness=0.016, depth=-0.007)
    key = key_shape(0.139, 0.04)  # into the sunken panel (0.143) by 4 mm
    place_on(key, (0.005, 0, 0), (0, 0, math.radians(45)))
    s.part("Body", [block, *key])
    return export("item_key")


def model_item_stun():
    """Stun gadget, 2 x 2 cells: an armoured case with corner posts, seams and a front vent, a ribbed emitter coil
    with a halo ring and an orb on top, two whip antennae. Must read as a device, not as loot."""
    case = s.rbox((0.37, 0.37, 0.25), (0, 0, 0.125), r=0.03, seg=3)
    s.groove(case, "Z", 0.085, 0.012, 0.006)
    s.groove(case, "Z", 0.175, 0.012, 0.006)
    posts = [s.rbox((0.06, 0.06, 0.27), (x * 0.176, y * 0.176, 0.135), r=0.016, seg=2)
             for x in (-1, 1) for y in (-1, 1)]
    vent = s.grille((0, -0.185, 0.13), 0.2, 0.1, n=4, proud=0.012)
    button = s.rcyl(0.018, 0.016, (0.13, -0.188, 0.13), axis="-Y", seg=16, r=0.004)
    collar = s.rcyl(0.13, 0.03, (0, 0, 0.262), seg=32, r=0.008)
    halo = ring(0.165, 0.145, 0.016, (0, 0, 0.252), seg=40, r=0.004)
    coil = s.lathe([(0.075, 0.27), (0.075, 0.292), (0.105, 0.297), (0.105, 0.307), (0.065, 0.313), (0.065, 0.325),
                    (0.09, 0.33), (0.09, 0.34), (0.055, 0.345), (0.055, 0.356), (0.075, 0.361), (0.075, 0.369),
                    (0.04, 0.375), (0.03, 0.385), (0.0, 0.386)], seg=28)
    orb = s.ball(0.034, (0, 0, 0.39), seg=16)
    antennae = []
    for x in (-1, 1):
        base = (x * 0.14, 0.14, 0.245)
        antennae.append(s.rcyl(0.018, 0.03, base, seg=12, r=0.005))
        antennae.append(s.rcyl(0.007, 0.15, (base[0], base[1], 0.33), seg=8, r=0.002))
        antennae.append(s.ball(0.014, (base[0], base[1], 0.405), seg=10))
    s.part("Body", [case, *posts, *vent, button, collar, halo, coil, orb, *antennae])
    return export("item_stun")


# --------------------------------------------------------------------------- lure speaker

def model_lure_speaker():
    """Portable loudspeaker 0.36 x 0.26 x 0.46 on the floor: rounded casing with rubber corner bumpers and a carry
    handle, a woofer with a glowing LED halo, a tweeter, a small vent; EyeLed also has a power dot on top."""
    casing = s.rbox((0.34, 0.24, 0.395), (0, 0, 0.215), r=0.035, seg=3)
    s.inset_faces(casing, s.facing((0, -1, 0), min_area=0.05, tol=0.95), thickness=0.022, depth=-0.012)
    s.groove(casing, "Z", 0.07, 0.01, 0.004)
    mounts = [s.rbox((0.05, 0.07, 0.04), (x * 0.115, 0, 0.415), r=0.012, seg=2) for x in (-1, 1)]
    s.part("Body", [casing, *mounts])

    def driver(radius, cx, cz):
        # face of the cone (profile from the back rim outwards: surround roll, cone dip, dust cap)
        return s.lathe([(radius, 0.0), (radius * 1.01, 0.008), (radius * 0.94, 0.017), (radius * 0.84, 0.013),
                        (radius * 0.4, 0.004), (radius * 0.38, 0.007), (radius * 0.3, 0.015), (0.0, 0.02)],
                       seg=32, axis="-Y", loc=(cx, -0.104, cz))

    woofer = driver(0.112, 0, 0.165)
    tweeter = driver(0.034, -0.075, 0.34)
    t_rim = ring(0.046, 0.033, 0.01, (-0.075, -0.108, 0.34), axis="Y", seg=24)
    vent = s.grille((0.06, -0.106, 0.34), 0.12, 0.06, n=3, proud=0.01)
    bumpers = [s.rbox((0.05, 0.05, 0.41), (x * 0.155, y * 0.105, 0.215), r=0.018, seg=2) for x in (-1, 1) for y in (-1, 1)]
    feet = [s.rcyl(0.022, 0.02, (x * 0.12, y * 0.08, 0.011), seg=12, r=0.004) for x in (-1, 1) for y in (-1, 1)]
    handle = s.tube([(-0.115, 0, 0.42), (-0.1, 0, 0.44), (0, 0, 0.446), (0.1, 0, 0.44), (0.115, 0, 0.42)], 0.013,
                    res=2, segments_per=6)
    s.part("Accent", [woofer, tweeter, t_rim, *vent, *bumpers, *feet, handle])

    halo = ring(0.127, 0.113, 0.012, (0, -0.108, 0.165), axis="Y", seg=40)
    power = s.rcyl(0.009, 0.008, (0.075, -0.06, 0.414), seg=12, r=0.002)
    s.part("EyeLed", [halo, power])
    return export("lure_speaker")


# --------------------------------------------------------------------------- medical table

ARCH_R, ARCH_T, ARCH_X, ARCH_W = 0.44, 0.11, 0.32, 0.3


def model_med_table():
    """Examination table 2.0 x 0.8, mattress top at 0.9, on a hydraulic pedestal, with a scanner arch straddling it
    (a glowing scan band inside the arch), a green-cross sign on the arch and a vitals screen on its front leg."""
    base = s.rbox((1.3, 0.56, 0.06), (0, 0, 0.03), r=0.02, seg=2)
    column = s.rbox((0.34, 0.28, 0.66), (0, 0, 0.385), r=0.04, seg=3, taper=(0.9, 0.9))
    for z in (0.2, 0.3, 0.4, 0.5):
        s.groove(column, "Z", z, 0.012, 0.006)
    frame = s.rbox((1.94, 0.7, 0.07), (0, 0, 0.765), r=0.02, seg=2)
    pillow = s.rbox((0.32, 0.5, 0.07), (-0.76, 0, 0.92), r=0.03, seg=3)
    rails = []
    for y in (-1, 1):
        rails.append(s.tube([(-0.9, y * 0.37, 0.8), (-0.88, y * 0.37, 1.0), (-0.4, y * 0.37, 1.0), (-0.38, y * 0.37, 0.8)],
                            0.012, smooth=False, segments_per=1))
    arch = sweep_rect(arch_path(ARCH_R, 0.04, 1.2), ARCH_X, ARCH_W, ARCH_T, r=0.03)
    sign = s.rbox((0.36, 0.07, 0.22), (ARCH_X, 0, 1.78), r=0.02, seg=2)
    bezel = s.rbox((0.2, 0.035, 0.22), (ARCH_X, -ARCH_R - 0.06, 1.05), r=0.01, seg=2)
    s.part("Body", [base, column, frame, pillow, *rails, arch, sign, bezel])

    mattress = s.rbox((1.9, 0.68, 0.11), (0, 0, 0.845), r=0.04, seg=3)
    s.groove(mattress, "X", -0.5, 0.012, 0.006)
    feet = [s.rbox((0.4, 0.17, 0.06), (ARCH_X, y * ARCH_R, 0.03), r=0.02, seg=2) for y in (-1, 1)]
    caps = [s.rbox((ARCH_W + 0.03, ARCH_T + 0.02, 0.05), (ARCH_X, y * ARCH_R, 0.62), r=0.012, seg=2) for y in (-1, 1)]
    pedal = s.rbox((0.12, 0.08, 0.03), (0.3, -0.24, 0.065), r=0.01, seg=1)
    s.part("Accent", [mattress, *feet, *caps, pedal])

    inner = ARCH_R - ARCH_T / 2
    path = [p for p in arch_path(inner - 0.002, 0.95, 1.2) if p[1] >= 0.94]
    band = sweep_rect(path, ARCH_X, 0.1, 0.012, r=0.004)
    cross = []
    for y in (-1, 1):
        cross.append(s.rbox((0.15, 0.012, 0.05), (ARCH_X, y * 0.036, 1.78), r=0.004, seg=1))
        cross.append(s.rbox((0.05, 0.012, 0.15), (ARCH_X, y * 0.036, 1.78), r=0.004, seg=1))
    screen = s.rbox((0.17, 0.008, 0.18), (ARCH_X, -ARCH_R - 0.079, 1.05), r=0.003, seg=1)
    # the sign and the scan band glow green (their own material); the vitals screen shows its picture
    s.part("EyeSign", [band, *cross])
    s.part("EyeScreen", [screen])
    return export("med_table")


# --------------------------------------------------------------------------- workbench (the car upgrade bench)

WB_TOP = 0.95              # worktop surface
WB_X0, WB_X1 = -0.88, 1.52  # worktop ends (the engine, its dolly and hoist take x -1.55 .. -0.90)
WB_Y0, WB_Y1 = -0.41, 0.462
BOARD_Y = 0.47             # tool wall centre plane (board 0.458 .. 0.482); tools hang just in front of it
BOARD_F = 0.458            # its front face
SHELF_Z = 1.865            # top shelf centre (1.85 .. 1.88)
ENG = Vector((-1.21, -0.12, 0.0))  # engine centre in plan: crank along Y, pulleys to the front (-Y)
ENG_K = 1.2                # the engine is modelled 1:1 and shown 20 % bigger: the bench's main "car" cue
ENG_Z0 = 0.33              # its oil pan's bottom, on the dolly's saddles


def _cyl(radius, depth, loc=(0, 0, 0), axis="Z", seg=12, r=0.006, rot=(0, 0, 0), bevel_seg=1):
    """shapes.rcyl at two thirds of the segments and one bevel step (none under 2.5 cm): the bench has dozens of small
    round parts."""
    if radius < 0.025:
        r = 0.0                                         # a chamfer on a part this small never shows
    return s.rcyl(radius, depth, loc, axis=axis, seg=max(6, round(seg * 0.67)), r=r, bevel_seg=bevel_seg, rot=rot)


def _wrench(x, z_top, length, w):
    """A combination spanner hanging by its ring end: flat shank, a ring at the top, an open jaw at the bottom."""
    y = BOARD_F - 0.006
    shank = s.rbox((w, 0.007, length), (x, y, z_top - length / 2), r=0.0025, seg=1)
    ring_end = s.rcyl(w * 1.15, 0.009, (x, y, z_top), axis="Y", seg=8, r=0.0)
    jaw = s.rbox((w * 2.2, 0.009, w * 1.4), (x, y, z_top - length), r=0.002, seg=1, taper=(1.25, 1.0))  # open end
    return [shank, ring_end, jaw]


def _hook(x, z):
    y = BOARD_F
    return s.tube([(x, y + 0.004, z), (x, y - 0.03, z), (x, y - 0.038, z + 0.012)], 0.0035, res=1, smooth=False)


def _eng_pt(x, y, z):
    """A point of the engine as modelled (around ENG at the old height) -> where it ends up: scaled by ENG_K about the
    oil pan's bottom and set down on the dolly's saddles."""
    return Vector((ENG.x + (x - ENG.x) * ENG_K, ENG.y + (y - ENG.y) * ENG_K, ENG_Z0 + (z - 0.49) * ENG_K))


def _engine():
    """An inline four hanging on the hoist over its dolly: oil pan, ribbed block, head, valve cover with coil packs,
    intake runners and plenum on the left (-x), exhaust manifold on the right, timing cover, pulleys and the belt at
    the front, a flywheel at the back and two lifting brackets. Modelled at 1:1 around z 0.49, then scaled by ENG_K."""
    cx, cy = ENG.x, ENG.y
    y0 = cy - 0.22                                      # block front face
    pan = s.rbox((0.22, 0.40, 0.11), (cx, cy + 0.01, 0.545), r=0.025, seg=1, taper=(1.18, 1.06))
    block = s.rbox((0.245, 0.44, 0.30), (cx, cy, 0.735), r=0.022, seg=2, taper=(1.1, 1.0))  # skirt narrower than the deck
    for y in (cy - 0.11, cy, cy + 0.11):
        s.groove(block, "Y", y, 0.012, 0.006)           # cylinder ribs
    head = s.rbox((0.29, 0.45, 0.10), (cx, cy, 0.925), r=0.02, seg=2)
    s.groove(head, "Z", 0.905, 0.01, 0.005)
    cover = s.loft([(0.965, 0.10, 0.21, 4), (1.01, 0.095, 0.205, 4), (1.045, 0.075, 0.19, 3)], seg=20,
                   round_caps=0.015, loc=(cx, cy, 0))
    parts = [pan, block, head, cover]
    for k in range(4):                                  # coil packs on the cover's ridge
        y = cy - 0.15 + k * 0.1
        parts.append(s.rbox((0.05, 0.035, 0.05), (cx, y, 1.06), r=0.008, seg=1))
    parts.append(_cyl(0.022, 0.03, (cx + 0.05, cy + 0.15, 1.045), seg=12, r=0.004))   # oil filler cap
    for dy in (-0.05, 0.06):                            # freeze plugs on the left of the block
        parts.append(_cyl(0.018, 0.012, (cx - 0.13, cy + dy, 0.70), axis="X", seg=10, r=0.003))
    for sy in (-1, 1):                                  # lifting brackets bolted to the head's ends
        parts.append(s.rbox((0.06, 0.02, 0.09), (cx + sy * 0.03, cy + sy * 0.23, 0.995), r=0.006, seg=1))
    # intake: four runners curving out of the head's left side down into a plenum along Y, throttle body at the front
    px, pz = cx - 0.215, 0.80
    for k in range(4):
        y = cy - 0.165 + k * 0.11
        parts.append(s.tube([(cx - 0.13, y, 0.93), (cx - 0.20, y, 0.95), (cx - 0.225, y, 0.88), (px, y, pz + 0.02)],
                            0.022, res=1, segments_per=2))
    parts.append(_cyl(0.05, 0.42, (px, cy - 0.01, pz), axis="Y", seg=14, r=0.012))
    parts.append(_cyl(0.036, 0.06, (px, cy - 0.245, pz), axis="Y", seg=14, r=0.006))
    # exhaust: four pipes out of the right side of the head into a collector, the down pipe flange below
    ex = cx + 0.20
    for k in range(4):
        y = cy - 0.165 + k * 0.11
        yc = cy + 0.02 + (k - 1.5) * 0.03
        parts.append(s.tube([(cx + 0.13, y, 0.91), (cx + 0.19, y, 0.90), (ex, yc, 0.80), (ex - 0.005, yc, 0.70)],
                            0.019, res=1, segments_per=2))
    parts.append(_cyl(0.045, 0.1, (ex - 0.005, cy + 0.02, 0.66), seg=14, r=0.01))
    parts.append(_cyl(0.062, 0.016, (ex - 0.005, cy + 0.02, 0.605), seg=14, r=0.004))
    # oil filter on the left, low
    parts.append(_cyl(0.042, 0.10, (cx - 0.16, cy + 0.08, 0.62), axis="X", seg=14, r=0.008))
    # front: timing cover, crank / cam / alternator pulleys and the belt round them
    parts.append(s.rbox((0.22, 0.04, 0.34), (cx, y0 - 0.012, 0.79), r=0.02, seg=1, taper=(0.8, 1.0)))
    crank = Vector((cx, y0 - 0.05, 0.63))
    cam = Vector((cx, y0 - 0.05, 0.93))
    alt = Vector((cx - 0.11, y0 - 0.05, 0.76))
    for c, r in ((crank, 0.07), (cam, 0.06), (alt, 0.042)):
        parts.append(s.lathe([(0.0, -0.022), (r, -0.022), (r * 0.86, 0.0), (r, 0.022), (0.0, 0.022)], seg=14,
                             axis="Y", loc=tuple(c)))
        parts.append(_cyl(r * 0.45, 0.06, tuple(c + Vector((0, 0.03, 0))), axis="Y", seg=10, r=0.003))
    parts.append(_cyl(0.06, 0.12, (alt.x - 0.005, y0 + 0.04, alt.z), axis="Y", seg=14, r=0.012))  # alternator
    belt = []
    for c, r, a0, a1 in ((crank, 0.074, 200, 340), (cam, 0.064, -20, 160), (alt, 0.046, 120, 240)):
        for k in range(4):
            a = math.radians(a0 + (a1 - a0) * k / 3)
            belt.append((c.x + r * math.cos(a), c.y, c.z + r * math.sin(a)))
    belt.append(belt[0])
    parts.append(s.tube(belt, 0.006, res=1, smooth=False))
    # back: the flywheel with its ring gear on the crank, the bell face of the block behind it
    parts.append(s.lathe([(0.0, 0.0), (0.13, 0.0), (0.14, 0.006), (0.14, 0.024), (0.13, 0.03), (0.0, 0.03)], seg=20,
                         axis="Y", loc=(cx, cy + 0.215, 0.63)))
    for b in parts:
        s.place(b, (-cx, -cy, -0.49))
        s.place(b, scale=(ENG_K, ENG_K, ENG_K))
        s.place(b, (cx, cy, ENG_Z0))
    return parts


def _dolly():
    """The low engine dolly under the hanging engine: a frame on four casters and two saddles under the oil pan."""
    cx, cy = ENG.x, ENG.y
    parts, wheels = [], []
    for sx in (-1, 1):
        parts.append(s.rbox((0.045, 0.60, 0.045), (cx + sx * 0.17, cy, 0.115), r=0.008, seg=1))         # side rails
    for sy in (-1, 1):
        parts.append(s.rbox((0.39, 0.045, 0.045), (cx, cy + sy * 0.26, 0.115), r=0.008, seg=1))         # cross rails
        for sx in (-1, 1):
            parts.append(s.rbox((0.04, 0.04, 0.20), (cx + sx * 0.17, cy + sy * 0.15, 0.225), r=0.006, seg=1))  # posts
        parts.append(s.rbox((0.40, 0.07, 0.04), (cx, cy + sy * 0.15, ENG_Z0 - 0.017), r=0.008, seg=1))  # saddle
        for sx in (-1, 1):
            x, y = cx + sx * 0.17, cy + sy * 0.26
            wheels.append(_cyl(0.042, 0.03, (x, y, 0.042), axis="X", seg=12, r=0.008))
            wheels.append(s.rbox((0.05, 0.05, 0.05), (x, y, 0.085), r=0.006, seg=1))                     # fork
    return parts + wheels


def _hoist():
    """A jib crane behind the engine: a round column on a floor plate, a boom out over the engine with a brace, a
    trolley with a chain block, the chain down to the hook and two lifting chains from the hook to the engine's
    brackets; the hand chain loop beside it."""
    cx, cy = ENG.x, ENG.y
    col = Vector((cx - 0.02, 0.43, 0))
    top = 2.03
    parts = [s.rbox((0.30, 0.20, 0.025), (col.x, col.y, 0.0125), r=0.006, seg=1),          # floor plate
             _cyl(0.045, top - 0.02, (col.x, col.y, (top - 0.02) / 2 + 0.01), seg=16, r=0.006),
             _cyl(0.06, 0.05, (col.x, col.y, 0.05), seg=16, r=0.008),                     # foot collar
             _cyl(0.06, 0.06, (col.x, col.y, top - 0.04), seg=16, r=0.008)]               # pivot collar
    for sx in (-1, 1):                                                                     # gussets on the plate
        parts.append(s.rbox((0.012, 0.08, 0.08), (col.x + sx * 0.06, col.y, 0.06), r=0.003, seg=1))
    y_end = -0.47
    boom_y0 = col.y + 0.06
    boom_c = (boom_y0 + y_end) / 2
    parts.append(s.rbox((0.022, boom_y0 - y_end, 0.10), (col.x, boom_c, top - 0.04), r=0.004, seg=1))   # web
    for dz in (-0.05, 0.05):                                                                          # flanges
        parts.append(s.rbox((0.07, boom_y0 - y_end, 0.016), (col.x, boom_c, top - 0.04 + dz), r=0.004, seg=1))
    parts.append(s.rbox((0.08, 0.02, 0.12), (col.x, y_end + 0.004, top - 0.04), r=0.004, seg=1))      # end stop
    parts.append(s.tube([(col.x, col.y - 0.03, 1.55), (col.x, -0.05, top - 0.095)], 0.016, res=1, smooth=False))
    # trolley on the lower flange, chain block under it
    ty = cy
    parts.append(s.rbox((0.09, 0.10, 0.04), (col.x, ty, top - 0.105), r=0.008, seg=1))
    parts.append(s.rbox((0.12, 0.13, 0.12), (col.x, ty, top - 0.18), r=0.03, seg=1))
    parts.append(_cyl(0.07, 0.03, (col.x + 0.075, ty, top - 0.18), axis="X", seg=16, r=0.006))      # hand wheel
    chain = []
    z = top - 0.25
    k = 0
    while z > 1.42:
        link = ring(0.017, 0.0095, 0.006, axis="Y", seg=6, r=0.0)
        s.place(link, scale=(1, 1, 2.0))
        if k % 2:
            s.place(link, rot=(0, 0, math.pi / 2))
        s.place(link, (col.x, ty, z))
        chain.append(link)
        z -= 0.055
        k += 1
    hook_top = z + 0.03
    parts.append(_cyl(0.022, 0.05, (col.x, ty, hook_top - 0.02), seg=12, r=0.005))                 # swivel
    bowl = Vector((col.x, ty - 0.035, hook_top - 0.155))
    parts.append(s.tube([(col.x, ty, hook_top - 0.04), (col.x, ty, hook_top - 0.10), tuple(bowl),
                         (col.x, ty - 0.075, hook_top - 0.11), (col.x, ty - 0.07, hook_top - 0.08)], 0.011,
                        res=1, segments_per=2, radii=[1.0, 1.0, 0.9, 0.7, 0.5]))
    for sy in (-1, 1):                                  # lifting chains to the brackets, taut
        b = _eng_pt(cx + sy * 0.03, cy + sy * 0.23, 1.03)
        parts.append(s.tube([tuple(bowl + Vector((0, 0, -0.005))), tuple(b)], 0.007, res=1, smooth=False))
    # hand chain loop hanging off the hand wheel
    hx = col.x + 0.09
    parts.append(s.tube([(hx, ty - 0.05, top - 0.2), (hx, ty - 0.06, 1.45), (hx, ty, 1.38), (hx, ty + 0.06, 1.45),
                         (hx, ty + 0.05, top - 0.2)], 0.005, res=1, segments_per=3))
    return parts + chain


def _bench_frame():
    """Heavy steel frame: square legs with feet, rails, the tool wall's uprights and top bar, the worktop nosing."""
    parts = []
    for x, y in ((-0.835, -0.36), (1.475, -0.36), (0.245, -0.36)):
        parts.append(s.rbox((0.07, 0.07, 0.875), (x, y, 0.4375), r=0.01, seg=1))
        parts.append(s.rbox((0.10, 0.10, 0.02), (x, y, 0.01), r=0.005, seg=1))                    # foot plate
    for x in (-0.85, 1.49):                                                                         # wall uprights
        parts.append(s.rbox((0.06, 0.06, 2.07), (x, BOARD_Y, 1.035), r=0.01, seg=1))
        parts.append(s.rbox((0.10, 0.10, 0.02), (x, BOARD_Y, 0.01), r=0.005, seg=1))
    parts.append(s.rbox((2.42, 0.07, 0.06), (0.32, BOARD_Y, 2.07), r=0.012, seg=1))                # top bar
    for x in (-0.84, 1.48):                                                                         # side rails
        for z in (0.12, 0.83):
            parts.append(s.rbox((0.045, 0.80, 0.05), (x, 0.055, z), r=0.008, seg=1))
    parts.append(s.rbox((2.30, 0.045, 0.07), (0.32, -0.365, 0.83), r=0.008, seg=1))                 # front apron
    parts.append(s.rbox((2.30, 0.045, 0.06), (0.32, BOARD_Y - 0.02, 0.83), r=0.008, seg=1))         # back rail
    parts.append(s.rbox((1.06, 0.045, 0.045), (-0.30, -0.365, 0.12), r=0.008, seg=1))               # shelf front rail
    parts.append(s.rbox((WB_X1 - WB_X0 + 0.01, 0.025, 0.09), ((WB_X0 + WB_X1) / 2, WB_Y0 - 0.002, WB_TOP - 0.04),
                        r=0.006, seg=1))                                                            # steel nosing
    for x in (-0.70, 1.25):                                                                         # shelf brackets
        parts.append(s.rbox((0.02, 0.17, 0.12), (x, 0.375, SHELF_Z - 0.075), r=0.004, seg=1))
    return parts


def _vise():
    x, y = 1.28, -0.25
    t = WB_TOP - 0.004
    parts = [s.rbox((0.16, 0.20, 0.04), (x, y + 0.02, t + 0.02), r=0.012, seg=1),
             s.rbox((0.19, 0.06, 0.12), (x, y + 0.07, t + 0.085), r=0.012, seg=1),
             s.rbox((0.19, 0.06, 0.10), (x, y - 0.09, t + 0.08), r=0.012, seg=1),
             s.rbox((0.08, 0.18, 0.05), (x, y - 0.01, t + 0.05), r=0.01, seg=1),
             _cyl(0.012, 0.12, (x, y - 0.18, t + 0.075), axis="Y", seg=10, r=0.002),
             _cyl(0.007, 0.17, (x, y - 0.235, t + 0.075), axis="X", seg=8, r=0.002)]
    parts += [s.ball(0.013, (x + d * 0.085, y - 0.235, t + 0.075), seg=8) for d in (-1, 1)]
    return parts


def _coilover(x, y):
    """A coil-over strut standing on the bench: damper body, spring seats and a coil spring round it."""
    t = WB_TOP - 0.004
    parts = [_cyl(0.032, 0.30, (x, y, t + 0.15), seg=14, r=0.006),
             _cyl(0.012, 0.12, (x, y, t + 0.34), seg=10, r=0.003),
             _cyl(0.058, 0.014, (x, y, t + 0.06), seg=16, r=0.004),
             _cyl(0.058, 0.014, (x, y, t + 0.35), seg=16, r=0.004),
             _cyl(0.03, 0.03, (x, y, t + 0.395), seg=12, r=0.005),
             s.lathe([(0.0, 0.0), (0.03, 0.0), (0.03, 0.03), (0.018, 0.05), (0.0, 0.05)], seg=12, loc=(x, y, t))]
    pts = []
    turns, z0, z1, r = 4.5, t + 0.068, t + 0.343, 0.048
    n = int(turns * 7)
    for k in range(n + 1):
        a = 2 * math.pi * turns * k / n
        pts.append((x + r * math.cos(a), y + r * math.sin(a), z0 + (z1 - z0) * k / n))
    parts.append(s.tube(pts, 0.008, res=1, smooth=False))
    return parts


def _tyre(c, rad=0.30, w=0.21):
    """A wheel standing on its tread, face to the front: a fat tyre (rounded shoulders, two tread grooves, sidewall
    bulge) with a clear step down to a steel rim, its dish, a raised hub ring and five nuts."""
    hw = w / 2
    prof = [(0.0, -hw + 0.06), (0.06, -hw + 0.06), (0.075, -hw + 0.035), (0.13, -hw + 0.035), (0.165, -hw + 0.012),
            (0.185, -hw + 0.012), (0.19, -hw - 0.004), (0.196, -hw + 0.01),          # rim flange, the bead seat
            (0.212, -hw + 0.004), (rad - 0.05, -hw - 0.016), (rad - 0.012, -hw + 0.012), (rad, -hw + 0.045),
            (rad, hw - 0.045), (rad - 0.012, hw - 0.012), (rad - 0.05, hw + 0.016), (0.212, hw - 0.004),
            (0.196, hw - 0.01), (0.19, hw + 0.004), (0.185, hw - 0.012), (0.0, hw - 0.012)]
    tyre = s.lathe(prof, seg=22, axis="Y", loc=tuple(c))
    for dy in (-0.035, 0.035):
        s.groove(tyre, "Y", c.y + dy, 0.014, 0.01)
    nuts = [_cyl(0.012, 0.03, (c.x + 0.045 * math.cos(a), c.y - hw + 0.05, c.z + 0.045 * math.sin(a)), axis="Y",
                 seg=6, r=0.0) for a in (math.radians(90 + 72 * k) for k in range(5))]
    return [tyre, *nuts]


def _battery(x, y, z):
    box = s.rbox((0.26, 0.17, 0.19), (x, y, z + 0.095), r=0.012, seg=1)
    lid = s.rbox((0.27, 0.18, 0.025), (x, y, z + 0.19), r=0.008, seg=1)
    posts = [_cyl(0.013, 0.03, (x + sx * 0.09, y - 0.04, z + 0.21), seg=10, r=0.003) for sx in (-1, 1)]
    handle = s.tube([(x - 0.07, y + 0.03, z + 0.2), (x - 0.05, y + 0.03, z + 0.25), (x + 0.05, y + 0.03, z + 0.25),
                     (x + 0.07, y + 0.03, z + 0.2)], 0.006, res=1, segments_per=2)
    return [box, lid, handle], posts


def _jerrycan(x, y, z):
    """A fuel jerrycan standing on the low shelf: pressed X panels on its flanks, three-bar handle, the spout."""
    w, d, h = 0.17, 0.36, 0.44
    body = s.rbox((w, d, h), (x, y, z + h / 2), r=0.022, seg=1)
    s.inset_faces(body, s.facing((1, 0, 0), 0.05), 0.03, -0.008)
    s.inset_faces(body, s.facing((-1, 0, 0), 0.05), 0.03, -0.008)
    parts = [body]
    for dy in (-0.07, 0.0, 0.07):                       # handle posts
        parts.append(s.rbox((0.03, 0.02, 0.05), (x, y + 0.04 + dy, z + h + 0.02), r=0.006, seg=1))
    parts.append(s.rbox((0.03, 0.18, 0.025), (x, y + 0.04, z + h + 0.05), r=0.01, seg=1))      # handle bar
    spout = _cyl(0.026, 0.07, (0, 0, 0), seg=10, r=0.005)
    s.place(spout, (x, y - 0.135, z + h + 0.02), (math.radians(-30), 0, 0))
    parts.append(spout)
    return parts


def _screen():
    """The diagnostic scanner: a monitor on the tool wall, a cable down to the OBD plug lying on the bench."""
    x, z = 1.08, 1.43
    yb = BOARD_F
    bezel = s.rbox((0.44, 0.05, 0.31), (x, yb - 0.085, z), r=0.018, seg=1)
    back = s.rbox((0.24, 0.10, 0.16), (x, yb - 0.048, z), r=0.012, seg=1)
    knobs = [_cyl(0.011, 0.012, (x + 0.13 + k * 0.035, yb - 0.112, z - 0.13), axis="Y", seg=10, r=0.002)
             for k in range(2)]
    cable = s.tube([(x - 0.18, yb - 0.08, z - 0.15), (x - 0.24, yb - 0.10, z - 0.3), (x - 0.30, yb - 0.25, WB_TOP + 0.02),
                    (x - 0.40, yb - 0.30, WB_TOP + 0.008)], 0.006, res=1, segments_per=3)
    plug = s.rbox((0.05, 0.03, 0.022), (x - 0.43, yb - 0.30, WB_TOP + 0.008), r=0.006, seg=1)
    glass = s.rbox((0.38, 0.012, 0.23), (x, yb - 0.111, z + 0.012), r=0.006, seg=1)
    return [bezel, back, cable, plug] + knobs, [glass]


def _wall_tools():
    """Few, big, car-shaped things on the tool wall: a licence plate, a lug wrench, a hubcap, a torque wrench, a
    drive belt on a hook, four big spanners on a rail and a hammer."""
    parts = []
    y = BOARD_F - 0.006
    # licence plate screwed to the board, its face pressed in inside the border
    plate = s.rbox((0.52, 0.014, 0.12), (-0.45, BOARD_F - 0.005, 1.74), r=0.008, seg=1)
    s.inset_faces(plate, s.facing((0, -1, 0), 0.01), 0.014, -0.004)
    parts.append(plate)
    parts += [_cyl(0.008, 0.014, (-0.45 + sx * 0.19, BOARD_F - 0.012, 1.74), axis="Y", seg=8) for sx in (-1, 1)]
    # four-way lug wrench
    cx, cz, arm = -0.58, 1.43, 0.22
    for a in (45, -45):
        bar = _cyl(0.011, 2 * arm + 0.01, (0, 0, 0), axis="X", seg=8)
        s.place(bar, (cx, y - 0.014, cz), (0, math.radians(a), 0))
        parts.append(bar)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        parts.append(_cyl(0.019, 0.04, (cx + arm * math.cos(a), y - 0.014, cz + arm * math.sin(a)), axis="X", seg=10,
                          rot=(0, -a, 0)))
    parts.append(_cyl(0.026, 0.035, (cx, y - 0.014, cz), axis="Y", seg=10))
    parts.append(_hook(cx, cz + 0.026))
    # hubcap: a shallow dish with a raised centre
    parts.append(s.lathe([(0.0, 0.0), (0.165, 0.0), (0.17, 0.012), (0.155, 0.03), (0.07, 0.042), (0.05, 0.055),
                          (0.0, 0.058)], seg=20, axis="-Y", loc=(-0.13, BOARD_F + 0.004, 1.45)))
    # torque wrench lying on two hooks: square drive head, long tube, knurled grip
    tz = 1.19
    parts.append(_cyl(0.012, 0.50, (-0.44, y - 0.02, tz), axis="X", seg=8))
    parts.append(_cyl(0.019, 0.15, (-0.13, y - 0.02, tz), axis="X", seg=10))
    parts.append(_cyl(0.026, 0.03, (-0.71, y - 0.02, tz), axis="Y", seg=10))
    parts.append(s.rbox((0.05, 0.03, 0.04), (-0.685, y - 0.02, tz), r=0.006, seg=1))
    parts += [_hook(x, tz - 0.026) for x in (-0.58, -0.30)]
    # a drive belt hanging on a hook
    bx, bz = 0.20, 1.42
    pts = [(bx + 0.12 * math.sin(2 * math.pi * k / 12), BOARD_F - 0.03, bz - 0.17 + 0.17 * math.cos(2 * math.pi * k / 12))
           for k in range(13)]
    parts.append(s.tube(pts, 0.009, res=1, smooth=False))
    parts.append(s.tube([(bx, BOARD_F + 0.004, bz + 0.0), (bx, BOARD_F - 0.04, bz + 0.0), (bx, BOARD_F - 0.045, bz + 0.02)],
                        0.0045, res=1, smooth=False))
    # four big spanners on a rail
    parts.append(s.rbox((0.30, 0.02, 0.02), (0.50, BOARD_F - 0.008, 1.775), r=0.005, seg=1))
    for k in range(4):
        parts += _wrench(0.40 + k * 0.066, 1.755, 0.19 + k * 0.03, 0.02 + k * 0.003)
    # hammer
    x = 0.74
    parts.append(s.rbox((0.03, 0.022, 0.28), (x, y - 0.006, 1.31), r=0.01, seg=1))
    parts.append(s.rbox((0.14, 0.035, 0.045), (x + 0.01, y - 0.01, 1.465), r=0.01, seg=1))
    parts.append(_hook(x - 0.035, 1.425))
    parts.append(_hook(x + 0.045, 1.425))
    return parts


def _shelf_stuff():
    z = SHELF_Z + 0.015 - 0.004                           # top shelf surface (things sink in a little)
    parts = []
    for x in (-0.68, -0.55):                              # oil jugs: rounded body, cap
        parts.append(s.rbox((0.11, 0.15, 0.15), (x, 0.35, z + 0.075), r=0.03, seg=1, taper=(0.85, 0.9)))
        parts.append(_cyl(0.02, 0.03, (x + 0.02, 0.32, z + 0.16), seg=10, r=0.004))
    for x in (-0.22, -0.16, -0.10):                       # spray cans
        parts.append(s.lathe([(0.0, 0.0), (0.03, 0.0), (0.032, 0.006), (0.032, 0.12), (0.022, 0.14), (0.01, 0.155),
                              (0.0, 0.155)], seg=8, loc=(x, 0.35, z)))
    parts.append(s.rbox((0.38, 0.17, 0.11), (0.45, 0.36, z + 0.055), r=0.012, seg=1))       # parts bin
    parts.append(s.rbox((0.39, 0.025, 0.05), (0.45, 0.273, z + 0.04), r=0.006, seg=1))      # its label lip
    parts.append(s.rbox((0.24, 0.18, 0.08), (1.15, 0.36, z + 0.04), r=0.015, seg=1))        # filter boxes
    parts.append(s.rbox((0.17, 0.14, 0.07), (1.13, 0.36, z + 0.11), r=0.012, seg=1))
    return parts


def _cabinet():
    """A rolling tool cabinet under the middle of the bench: carcass on four casters, a top lip, four drawers with
    full-width pulls and a push bar on its side."""
    x0, x1 = 0.29, 0.89
    xc = (x0 + x1) / 2
    zb, zt = 0.10, 0.76
    carcass = s.rbox((x1 - x0, 0.74, zt - zb), (xc, 0.04, (zb + zt) / 2), r=0.012, seg=2)
    lip = s.rbox((x1 - x0 + 0.02, 0.77, 0.03), (xc, 0.035, zt + 0.005), r=0.01, seg=1)
    fronts, steel = [], []
    z = zt - 0.015
    for h in (0.11, 0.13, 0.17, 0.22):
        fronts.append(s.rbox((x1 - x0 - 0.04, 0.022, h - 0.012), (xc, -0.335, z - h / 2), r=0.008, seg=1))
        steel.append(s.rbox((x1 - x0 - 0.10, 0.03, 0.018), (xc, -0.352, z - 0.028), r=0.007, seg=1))
        z -= h
    for x in (x0 + 0.05, x1 - 0.05):
        for y in (-0.28, 0.36):
            steel.append(_cyl(0.04, 0.03, (x, y, 0.04), axis="X", seg=12, r=0.008))
            steel.append(s.rbox((0.05, 0.06, 0.04), (x, y, 0.085), r=0.006, seg=1))
    steel.append(s.tube([(x1 - 0.01, -0.25, 0.70), (x1 + 0.05, -0.23, 0.70), (x1 + 0.05, 0.33, 0.70),
                         (x1 - 0.01, 0.35, 0.70)], 0.012, res=1, smooth=False))
    return [carcass, lip, *fronts], steel


def _toolbox(x, y):
    t = WB_TOP - 0.004
    box = s.rbox((0.40, 0.20, 0.14), (x, y, t + 0.07), r=0.018, seg=1)
    s.groove(box, "Z", t + 0.105, 0.008, 0.004)
    handle = s.tube([(x - 0.12, y, t + 0.135), (x - 0.10, y, t + 0.19), (x + 0.10, y, t + 0.19), (x + 0.12, y, t + 0.135)],
                    0.01, smooth=False, segments_per=1)
    latches = [s.rbox((0.03, 0.012, 0.04), (x + sx * 0.12, y - 0.105, t + 0.105), r=0.004, seg=1) for sx in (-1, 1)]
    return [box], [handle, *latches]


ST_W, ST_D = 4.7, 1.5         # the station's footprint: x -2.35..2.35, y -0.75..0.75 (front -Y, back against the wall)
ST_TOP = 0.95                  # the worktop
ST_BENCH = (-1.75, 1.6)        # the bench's ends along x
ST_BACK = 0.62                 # the tool wall's plane (y)
ST_BEAM_Z = 2.36               # the gantry beam's underside (its top 2.66: under the 2.75 m ceiling)


def model_workbench():
    """The car upgrade station of the auto repair shop (concept Logs/comfy/upgrades/bench_800.png): a big construction
    4.8 x 1.5 m and 2.66 m tall (the rooms' ceiling is at 2.75). A blue steel gantry (two posts at the ends, an I-beam across) with a yellow chain hoist
    holding an engine over the bench; a long steel bench with three drawer cabinets and a black-and-yellow edge; a tall
    pegboard tool wall with spanners, a hammer, red tools, a shelf of cans and a strip light; on the left an engine on its
    stand, on the right a diagnostic cart with a screen on an arm and a tyre leaning on it.
    Footprint x -2.35..2.35, y -0.75..0.75, origin on the floor at its centre.
    Parts: Body / Accent steel, BluePaint the gantry, Hazard the warning yellow, Dark black paint and rubber, TrimChrome
    bright steel, TrimHandle red, AccentBoard the pegboard, EyeLamp / EyeScreen glow."""
    x0, x1 = ST_BENCH
    bx, bw = (x0 + x1) / 2, x1 - x0
    by0, by1 = -0.42, ST_BACK - 0.02            # the bench's depth
    body, steel, blue, hazard, dark, chrome, red, board = [], [], [], [], [], [], [], []
    # the bench: a thick top, three drawer cabinets under it, a low shelf, legs between the cabinets
    steel.append(s.rbox((bw, by1 - by0, 0.06), (bx, (by0 + by1) / 2, ST_TOP - 0.03), r=0.012, seg=2))
    for cx in (x0 + 0.42, bx, x1 - 0.42):
        body.append(s.rbox((0.78, by1 - by0 - 0.06, ST_TOP - 0.12), (cx, (by0 + by1) / 2 + 0.02, (ST_TOP - 0.06) / 2 + 0.03), r=0.02, seg=2))
        for k in range(4):                                              # drawer fronts: seams and handles
            z = 0.16 + k * 0.19
            dark.append(s.rbox((0.74, 0.008, 0.012), (cx, by0 + 0.035, z + 0.09), r=0.003, seg=1))
            chrome.append(s.rbox((0.22, 0.03, 0.02), (cx, by0 + 0.03, z + 0.03), r=0.006, seg=1))
        dark.append(s.rbox((0.8, by1 - by0 - 0.04, 0.04), (cx, (by0 + by1) / 2 + 0.02, 0.02), r=0.01, seg=1))   # plinth
    for k in range(int(bw / 0.12)):                                     # the black-and-yellow front edge
        (hazard if k % 2 == 0 else dark).append(s.rbox((0.12, 0.012, 0.05), (x0 + 0.06 + k * 0.12, by0 - 0.004, ST_TOP - 0.035), r=0.002, seg=1))
    # the tool wall: a light pegboard in a steel frame, a shelf on top with cans, a strip light under it, tools on hooks
    board.append(s.rbox((bw - 0.1, 0.03, 1.55), (bx, ST_BACK + 0.04, ST_TOP + 0.85), r=0.01, seg=1))
    steel.append(s.rbox((bw - 0.04, 0.06, 0.05), (bx, ST_BACK + 0.04, ST_TOP + 1.65), r=0.01, seg=1))
    for xx in (x0 + 0.02, x1 - 0.02):
        steel.append(s.rbox((0.06, 0.06, 1.7), (xx, ST_BACK + 0.04, ST_TOP + 0.83), r=0.01, seg=1))
    steel.append(s.rbox((bw - 0.2, 0.24, 0.03), (bx, ST_BACK - 0.08, ST_TOP + 1.42), r=0.006, seg=1))    # the shelf
    for k, xx in enumerate((x0 + 0.4, x0 + 0.55, x0 + 0.7, bx + 0.6, bx + 0.75, x1 - 0.5)):
        (red if k % 3 == 0 else body).append(s.rcyl(0.045, 0.14 + 0.04 * (k % 2), (xx, ST_BACK - 0.08, ST_TOP + 1.51 + 0.02 * (k % 2)), seg=14, r=0.006))
    lamp = [s.rcyl(0.016, bw - 0.6, (bx, ST_BACK - 0.12, ST_TOP + 1.38), axis="X", seg=10, r=0.004)]
    for k in range(9):                                                  # spanners, big to small
        xx, l = x0 + 0.3 + k * 0.09, 0.32 - k * 0.022
        chrome.append(s.rbox((0.022, 0.012, l), (xx, ST_BACK + 0.01, ST_TOP + 1.05 - l / 2 + 0.2), r=0.004, seg=1))
        chrome.append(s.rcyl(0.03 - k * 0.001, 0.012, (xx, ST_BACK + 0.01, ST_TOP + 1.05 + 0.2), axis="Y", seg=12, r=0.003))
    red += [s.rbox((0.04, 0.03, 0.24), (bx - 0.2 + k * 0.12, ST_BACK + 0.005, ST_TOP + 0.55), r=0.012, seg=2) for k in range(4)]   # screwdriver grips
    chrome += [s.rcyl(0.007, 0.16, (bx - 0.2 + k * 0.12, ST_BACK + 0.005, ST_TOP + 0.35), seg=8, r=0.002) for k in range(4)]
    body.append(s.rbox((0.03, 0.03, 0.34), (bx + 0.45, ST_BACK + 0.005, ST_TOP + 0.5), r=0.01, seg=1))           # hammer
    steel.append(s.rbox((0.16, 0.05, 0.05), (bx + 0.45, ST_BACK + 0.005, ST_TOP + 0.68), r=0.01, seg=1))
    dark.append(s.tube([(bx + 0.8 + 0.12 * math.cos(a), ST_BACK + 0.01, ST_TOP + 0.8 + 0.12 * math.sin(a))
                        for a in [k * 0.5 for k in range(14)]], 0.012, res=1, smooth=False))                       # a coiled hose
    # on the bench: a vise, a toolbox, a coil-over strut
    steel.append(s.rbox((0.2, 0.14, 0.1), (x1 - 0.25, by0 + 0.12, ST_TOP + 0.05), r=0.01, seg=1))
    red.append(s.rbox((0.42, 0.2, 0.18), (bx - 0.6, (by0 + by1) / 2, ST_TOP + 0.09), r=0.02, seg=2))
    chrome.append(s.rbox((0.3, 0.03, 0.03), (bx - 0.6, (by0 + by1) / 2, ST_TOP + 0.2), r=0.01, seg=1))
    red.append(s.rcyl(0.035, 0.36, (bx + 0.2, by0 + 0.2, ST_TOP + 0.18), seg=14, r=0.008))
    dark.append(s.tube([(bx + 0.2 + 0.05 * math.cos(a), by0 + 0.2 + 0.05 * math.sin(a), ST_TOP + 0.04 + a * 0.012)
                        for a in [k * 0.6 for k in range(48)]], 0.008, res=1, smooth=False))                       # its spring
    # the gantry: two blue posts on foot plates, an I-beam across, braces, warning stripes low on the posts
    for xx in (-ST_W / 2 + 0.1, ST_W / 2 - 0.1):
        blue.append(s.rbox((0.16, 0.16, ST_BEAM_Z + 0.02), (xx, 0.05, (ST_BEAM_Z + 0.02) / 2), r=0.012, seg=2))
        steel.append(s.rbox((0.34, 0.34, 0.03), (xx, 0.05, 0.015), r=0.008, seg=1))
        for k in range(3):
            hazard.append(s.rbox((0.17, 0.17, 0.05), (xx, 0.05, 0.12 + k * 0.12), r=0.006, seg=1))
        blue.append(s.tube([(xx, 0.05, ST_BEAM_Z - 0.55), (xx + (0.55 if xx < 0 else -0.55), 0.05, ST_BEAM_Z + 0.05)], 0.04, res=1, smooth=False))
    blue.append(s.rbox((ST_W, 0.2, 0.06), (0, 0.05, ST_BEAM_Z + 0.03), r=0.01, seg=1))                  # the I-beam's flanges
    blue.append(s.rbox((ST_W, 0.2, 0.06), (0, 0.05, ST_BEAM_Z + 0.27), r=0.01, seg=1))
    blue.append(s.rbox((ST_W - 0.02, 0.05, 0.22), (0, 0.05, ST_BEAM_Z + 0.15), r=0.008, seg=1))
    # the hoist: a yellow body on a trolley, its chain, a hook, an engine hanging over the bench
    hx = -0.35
    H = ST_BEAM_Z - 0.38                                               # the hook
    hazard.append(s.rbox((0.3, 0.26, 0.22), (hx, 0.05, ST_BEAM_Z - 0.14), r=0.03, seg=2))
    steel.append(s.rbox((0.34, 0.24, 0.06), (hx, 0.05, ST_BEAM_Z - 0.01), r=0.01, seg=1))
    dark.append(s.tube([(hx, 0.05, ST_BEAM_Z - 0.25), (hx, 0.05, H - 0.02)], 0.012, res=1, smooth=False))
    steel.append(s.tube([(hx, 0.05, H), (hx + 0.04, 0.05, H - 0.09), (hx, 0.05, H - 0.15), (hx - 0.035, 0.05, H - 0.11)], 0.014, res=2))
    for sx in (-0.18, 0.18):                                           # the lifting chains to the engine
        dark.append(s.tube([(hx, 0.05, H - 0.14), (hx + sx, 0.05, H - 0.35)], 0.008, res=1, smooth=False))
    engine = [s.rbox((0.5, 0.32, 0.28), (hx, 0.05, H - 0.51), r=0.03, seg=2),                               # block
              s.rbox((0.46, 0.22, 0.12), (hx, 0.05, H - 0.31), r=0.02, seg=2)]                              # head
    dark += [s.rcyl(0.075, 0.03, (hx - 0.27, 0.05 - 0.02, H - 0.57), axis="X", seg=18, r=0.006),                # pulley, sump
             s.rbox((0.42, 0.24, 0.08), (hx, 0.05, H - 0.68), r=0.02, seg=1)]
    steel += engine
    red += [s.rcyl(0.022, 0.05, (hx - 0.15 + k * 0.1, 0.05, H - 0.24), seg=10, r=0.004) for k in range(4)]   # the plug caps
    # the engine stand on the left: an upright on castors, a block bolted to its plate
    ex = -ST_W / 2 + 0.55
    dark.append(s.rbox((0.08, 0.08, 0.95), (ex, -0.35, 0.5), r=0.01, seg=1))
    dark.append(s.rbox((0.7, 0.08, 0.06), (ex, -0.35, 0.06), r=0.01, seg=1))
    dark.append(s.rbox((0.08, 0.6, 0.06), (ex, -0.35, 0.06), r=0.01, seg=1))
    for wx, wy in ((ex - 0.32, -0.35), (ex + 0.32, -0.35), (ex, -0.63), (ex, -0.07)):
        dark.append(s.rcyl(0.04, 0.035, (wx, wy, 0.04), axis="X", seg=12, r=0.006))
    steel.append(s.rbox((0.38, 0.3, 0.3), (ex, -0.35, 1.08), r=0.03, seg=2))
    red.append(s.rbox((0.34, 0.22, 0.08), (ex, -0.35, 1.27), r=0.02, seg=1))
    # the diagnostic cart on the right: a cabinet on castors, an arm, a screen, a keyboard; a tyre leaning on it
    cx = ST_W / 2 - 0.42
    body.append(s.rbox((0.42, 0.44, 0.9), (cx, -0.2, 0.5), r=0.02, seg=2))
    for wx in (cx - 0.16, cx + 0.16):
        for wy in (-0.38, -0.02):
            dark.append(s.rcyl(0.035, 0.03, (wx, wy, 0.035), axis="X", seg=12, r=0.005))
    hazard.append(s.rbox((0.42, 0.012, 0.06), (cx, -0.425, 0.88), r=0.003, seg=1))
    dark.append(s.rbox((0.4, 0.2, 0.025), (cx, -0.3, 0.97), r=0.006, seg=1))                            # keyboard
    steel.append(s.rbox((0.04, 0.04, 0.55), (cx, -0.05, 1.22), r=0.008, seg=1))                         # the arm
    dark.append(s.rbox((0.46, 0.05, 0.32), (cx, -0.08, 1.58), r=0.015, seg=2))                          # the screen's case
    screen = [s.rbox((0.4, 0.01, 0.26), (cx, -0.106, 1.58), r=0.004, seg=1)]
    tyre = s.rcyl(0.34, 0.22, (cx - 0.05, -0.62, 0.34), axis="Y", seg=28, r=0.06, bevel_seg=3)
    dark.append(tyre)
    chrome.append(s.rcyl(0.2, 0.235, (cx - 0.05, -0.62, 0.34), axis="Y", seg=20, r=0.02))
    s.part("Body", body)
    s.part("Accent", steel)
    s.part("BluePaint", blue)
    s.part("Hazard", hazard)
    s.part("Dark", dark)
    s.part("TrimChrome", chrome)
    s.part("TrimHandle", red)
    s.part("AccentBoard", board)
    s.part("EyeLamp", lamp)
    s.part("EyeScreen", screen)
    return export("workbench")


def model_programmer_console():
    """Robot programming terminal: a sloped control desk (keyboard, joystick, buttons, a docked robot arm in its
    cradle) in front of a long shelf at z = 1.0 with four docking slots for 0.43 m cubes, three monitors above them."""
    tilt = math.radians(12)
    desk = s.rbox((1.6, 0.36, 0.78), (0, -0.21, 0.39), r=0.025, seg=2)
    s.inset_faces(desk, s.facing((0, -1, 0), min_area=0.3, tol=0.95), thickness=0.05, depth=-0.012)
    slope = s.rbox((1.62, 0.4, 0.05), (0, -0.215, 0.81), r=0.015, seg=2, rot=(tilt, 0, 0))
    cabinet = s.rbox((1.96, 0.5, 0.97), (0, 0.215, 0.485), r=0.02, seg=2)
    shelf = s.rbox((1.96, 0.52, 0.05), (0, 0.215, SHELF_TOP - 0.025), r=0.012, seg=2)
    panel = s.rbox((1.96, 0.05, 1.05), (0, 0.475, 1.47), r=0.02, seg=2)
    monitors, screens = [], []
    n = Vector((0, -math.cos(0.17), -math.sin(0.17)))
    for x in (-0.64, 0, 0.64):
        c = Vector((x, 0.415, 1.73))
        monitors.append(s.rbox((0.6, 0.045, 0.36), c, r=0.015, seg=2, rot=(0.17, 0, 0)))
        screens.append(s.rbox((0.54, 0.01, 0.3), c + n * 0.024, r=0.004, seg=1, rot=(0.17, 0, 0)))
        monitors.append(s.rbox((0.12, 0.06, 0.1), (x, 0.44, 1.58), r=0.01, seg=1))
    # robot arm cradle: a flat pad on the left of the desk with two forks
    pad = s.rbox((0.42, 0.26, 0.06), (-0.58, -0.22, 0.855), r=0.015, seg=2)
    forks = []
    for x in (-0.66, -0.44):
        f = s.rbox((0.05, 0.12, 0.08), (x, -0.22, 0.915), r=0.01, seg=2)
        f = s.boolean(f, s.rcyl(0.04, 0.1, (x, -0.22, 0.955), axis="X", seg=16, r=0), "DIFFERENCE")
        s.bevel_edges(f, 0.003, 1, angle=50)
        forks.append(f)
    s.part("Body", [desk, slope, cabinet, shelf, panel, *monitors, pad, *forks])

    def on_slope(x, y, h):
        """Centre of a part of height h lying on the desk slope at (x, y)."""
        up = Vector((0, -math.sin(tilt), math.cos(tilt)))
        surf = Vector((x, -0.215, 0.81)) + Vector((0, math.cos(tilt), math.sin(tilt))) * (y + 0.215) + up * 0.025
        return surf + up * (h / 2 - 0.004)

    keyboard = s.rbox((0.62, 0.2, 0.03), (0, 0, 0), r=0.008, seg=2)
    for x in (-0.2, -0.1, 0.0, 0.1, 0.2):
        s.groove(keyboard, "X", x, 0.008, 0.004)
    for y in (-0.05, 0.0, 0.05):
        s.groove(keyboard, "Y", y, 0.008, 0.004)
    s.place(keyboard, on_slope(0, -0.24, 0.03), (tilt, 0, 0))
    stick = [s.rcyl(0.045, 0.03, (0, 0, 0), seg=16, r=0.008), s.rcyl(0.009, 0.1, (0, 0, 0.06), seg=8, r=0.002),
             s.ball(0.022, (0, 0, 0.115), seg=12)]
    place_on(stick, on_slope(0.48, -0.24, 0.03), (tilt, 0, 0))
    buttons = []
    for i in range(3):
        for j in range(2):
            b = s.rcyl(0.018, 0.02, (0, 0, 0), seg=12, r=0.004)
            buttons.append(s.place(b, on_slope(0.63 + i * 0.05, -0.18 - j * 0.09, 0.02), (tilt, 0, 0)))
    arm_y, arm_z = -0.22, 0.955
    arm = [s.rcyl(0.06, 0.05, (-0.75, arm_y, 0.9), seg=20, r=0.008),
           s.ball(0.042, (-0.75, arm_y, 0.94), seg=16),
           s.loft([(0, 0.032, 0.034, 3), (0.2, 0.036, 0.038, 3.2), (0.32, 0.028, 0.03, 3)], seg=16, axis="X",
                  loc=(-0.75, arm_y, arm_z), round_caps=0.01),
           s.rcyl(0.035, 0.09, (-0.43, arm_y, arm_z), axis="Y", seg=16, r=0.006),
           s.loft([(0, 0.025, 0.027, 3), (0.16, 0.022, 0.024, 3)], seg=14, axis="X", loc=(-0.43, arm_y, arm_z),
                  round_caps=0.008),
           s.rcyl(0.03, 0.03, (-0.26, arm_y, arm_z), axis="X", seg=14, r=0.005)]
    for d in (-1, 1):
        arm.append(s.rbox((0.07, 0.012, 0.022), (-0.215, arm_y + d * 0.018, arm_z), r=0.004, seg=1,
                          rot=(0, 0, d * 0.25)))
    connectors = [s.rbox((0.14, 0.03, 0.06), (x, 0.445, 1.2), r=0.008, seg=1) for x in SLOTS_X]
    lip = s.rbox((1.94, 0.02, 0.03), (0, -0.05, SHELF_TOP - 0.03), r=0.006, seg=1)
    canopy = s.rbox((1.98, 0.22, 0.05), (0, 0.39, 2.0), r=0.015, seg=2)
    s.part("Accent", [keyboard, *stick, *buttons, *arm, *connectors, lip, canopy])

    leds = [s.rbox((0.08, 0.01, 0.022), (x, 0.448, 1.33), r=0.004, seg=1) for x in SLOTS_X]
    code = s.rbox((0.3, 0.012, 0.1), (0, 0, 0), r=0.003, seg=1)
    s.place(code, on_slope(0.0, -0.08, 0.012), (tilt, 0, 0))
    s.part("Eye", [*leds, code])
    # the three monitors: each its own third of one picture (Tools/Art/generate_screens.py screen_program), left to right
    obj = s.part("EyeScreen", screens)
    up = Vector((0, -math.sin(0.17), math.cos(0.17)))
    uv = obj.data.uv_layers.active.data
    for loop in obj.data.loops:
        w = obj.matrix_world @ obj.data.vertices[loop.vertex_index].co
        k = min(range(3), key=lambda i: abs(w.x - (-0.64 + 0.64 * i)))
        d = w - (Vector((-0.64 + 0.64 * k, 0.415, 1.73)) + n * 0.024)
        u = min(1.0, max(0.0, d.x / 0.54 + 0.5))
        uv[loop.index].uv = ((k + u) / 3.0, min(1.0, max(0.0, d.dot(up) / 0.3 + 0.5)))
    # what only its baked maps show (shapebake): screws in the corners of the back panel and of the desk's front,
    # louvres pressed into the cabinet's sides, the sheet a little uneven
    extra = shapebake.rivets([(x, 0.45, z) for x in (-0.94, -0.32, 0.32, 0.94) for z in (1.02, 1.95)], "-Y", r=0.005, h=0.0015)
    extra += shapebake.rivets([(x, -0.39, z) for x in (-0.76, 0.76) for z in (0.06, 0.72)], "-Y", r=0.005, h=0.0015)
    for sx, axis in ((-1, "-X"), (1, "X")):
        for i in range(9):
            extra.append(shapebake.plate((0.3, 0.014), (sx * 0.98, 0.215, 0.28 + i * 0.045), axis, proud=0.0015))
    shapebake.detail("programmer_console", extra, waves=0.0004, wavy=("Body",))
    return export("programmer_console")


# --------------------------------------------------------------------------- crate

CRATE_HINGE = (0.0, 0.30, 0.50)


def model_crate():
    """Storage crate 0.8 x 0.6 x 0.6: a ribbed box with corner caps, side grips and front latches; the lid is its own
    object (AccentLid) with the origin on the hinge line at the back edge where the lid meets the box (+Y), so it
    swings open around X (negative angle about +X in Blender lifts the front). The box is hollow (a floor and four
    walls, 3 cm steel): what was inside lies on its floor when the lid opens (GameManager.SearchCrateServer)."""
    t = 0.03
    shell = [s.rbox((0.78, 0.58, 0.05), (0, 0, 0.025), r=0.02, seg=2)]                          # the floor
    shell += [s.rbox((t, 0.58, 0.5), (x * (0.39 - t / 2), 0, 0.25), r=0.012, seg=2) for x in (-1, 1)]
    shell += [s.rbox((0.78 - 2 * t, t, 0.5), (0, y * (0.29 - t / 2), 0.25), r=0.012, seg=2) for y in (-1, 1)]
    # the ribs: bands round the outside of the walls, not slabs through the inside
    ribs = []
    for z in (0.06, 0.44):
        ribs += [s.rbox((0.02, 0.6, 0.045), (x * 0.39, 0, z), r=0.008, seg=1) for x in (-1, 1)]
        ribs += [s.rbox((0.8, 0.02, 0.045), (0, y * 0.29, z), r=0.008, seg=1) for y in (-1, 1)]
    s.part("Body", [*shell, *ribs])

    caps = [s.rbox((0.09, 0.09, 0.12), (x * 0.36, y * 0.26, 0.06), r=0.02, seg=2) for x in (-1, 1) for y in (-1, 1)]
    grips = []
    for x in (-1, 1):
        grips.append(s.rbox((0.03, 0.22, 0.035), (x * 0.405, 0, 0.3), r=0.012, seg=2))
        grips += [s.rbox((0.04, 0.03, 0.05), (x * 0.395, y * 0.1, 0.3), r=0.008, seg=1) for y in (-1, 1)]
    latches = []
    for x in (-0.22, 0.22):
        latches.append(s.rbox((0.08, 0.025, 0.07), (x, -0.3, 0.4), r=0.008, seg=2))
        latches.append(s.rbox((0.05, 0.022, 0.08), (x, -0.31, 0.46), r=0.008, seg=2))
    hinges = [s.rbox((0.12, 0.02, 0.06), (x, 0.3, 0.465), r=0.006, seg=1) for x in (-0.25, 0.25)]
    s.part("Accent", [*caps, *grips, *latches, *hinges])

    lid = s.rbox((0.8, 0.6, 0.1), (0, 0, 0.548), r=0.025, seg=3)
    s.inset_faces(lid, s.facing((0, 0, 1), min_area=0.1, tol=0.95), thickness=0.04, depth=-0.012)
    knuckles = [s.rcyl(0.016, 0.1, (x, 0.3, 0.5), axis="X", seg=12, r=0.003) for x in (-0.25, 0.25)]
    leaves = [s.rbox((0.1, 0.016, 0.07), (x, 0.303, 0.54), r=0.005, seg=1) for x in (-0.25, 0.25)]
    catches = [s.rbox((0.06, 0.03, 0.04), (x, -0.305, 0.53), r=0.008, seg=1) for x in (-0.22, 0.22)]
    lid_obj = s.part("AccentLid", [lid, *knuckles, *leaves, *catches])
    g.set_pivot(lid_obj, CRATE_HINGE)
    return export("crate")


# --------------------------------------------------------------------------- key lock

def model_key_lock():
    """Lock panel on a door frame, 0.22 x 0.32 x 0.06: origin at the centre of the back (wall side), front -Y. A
    plate with screws, a raised housing, a keyhole boss with a glowing ring, a slot window above."""
    plate = s.rbox((0.22, 0.03, 0.32), (0, -0.015, 0), r=0.01, seg=2)
    housing = s.rbox((0.17, 0.03, 0.24), (0, -0.04, -0.01), r=0.012, seg=3)
    screws = s.bolts([(x * 0.088, -0.0315, z * 0.138) for x in (-1, 1) for z in (-1, 1)], radius=0.007, depth=0.006)
    boss = s.rcyl(0.032, 0.012, (0, -0.054, 0.0), axis="-Y", seg=28, r=0.003)
    hole = s.boolean(s.rcyl(0.009, 0.05, (0, -0.06, 0.006), axis="Y", seg=16, r=0),
                     s.rbox((0.009, 0.05, 0.026), (0, -0.06, -0.008), r=0), "UNION")
    boss = s.boolean(boss, hole, "DIFFERENCE")
    s.bevel_edges(boss, 0.0015, 1, angle=50)
    window = s.rbox((0.09, 0.012, 0.03), (0, -0.056, 0.085), r=0.004, seg=1)
    slot = s.rbox((0.05, 0.012, 0.012), (0, -0.056, -0.075), r=0.004, seg=1)
    s.part("Body", [plate, housing, *screws, boss, window, slot])
    halo = ring(0.044, 0.034, 0.008, (0, -0.0555, 0.0), axis="Y", seg=32, r=0.002)
    s.part("EyeLock", [halo])
    return export("key_lock")


# --------------------------------------------------------------------------- Warden robot

HEAD_Z = 1.30          # the head turns around the vertical axis through (0, 0); its origin is at (0, 0, HEAD_Z)
LAMP_Z = 1.55          # floodlight axis height
WHEELS = [(-0.24, -0.2), (0.24, -0.2), (-0.24, 0.2), (0.24, 0.2)]
WHEEL_R, WHEEL_W = 0.085, 0.07


def model_robot_warden():
    """Warden: a lighthouse-like sentinel (1.8 m with the beacon) on a wheeled skirt. Tapered column with accent bands, a balcony
    collar, a searchlight drum on a yoke (the head turns), a beacon on the drum, a folded barrier boom on its right,
    a radio whip on its back. It raises the alarm, it does not fight."""
    skirt = s.loft([(0.11, 0.39, 0.38, 2.6), (0.17, 0.4, 0.39, 2.6), (0.27, 0.36, 0.34, 2.6), (0.36, 0.25, 0.235, 2.8)],
                   seg=32, round_caps=0.025)
    s.groove(skirt, "Z", 0.21, 0.012, 0.006)
    chassis = s.rbox((0.6, 0.5, 0.05), (0, 0, 0.13), r=0.015, seg=2)
    column = s.loft([(0.33, 0.23, 0.21, 3), (0.6, 0.2, 0.185, 3), (0.95, 0.175, 0.162, 3), (1.12, 0.17, 0.158, 3)],
                    seg=28)
    s.groove(column, "Z", 0.47, 0.012, 0.006)
    s.groove(column, "Z", 0.82, 0.012, 0.006)
    neck = s.lathe([(0.11, 1.18), (0.075, 1.22), (0.07, 1.3), (0.08, 1.33), (0.0, 1.335)], seg=24)
    pack = s.rbox((0.24, 0.12, 0.34), (0, 0.17, 0.8), r=0.03, seg=3, taper=(0.9, 1.0))
    s.inset_faces(pack, s.facing((0, 1, 0), min_area=0.03, tol=0.95), thickness=0.02, depth=-0.006)
    vent = s.grille((0, 0.237, 0.8), 0.14, 0.18, n=4, proud=0.008, axis="Y")
    # barrier boom folded upright on the right side, on a hub out of the column
    hub = s.rcyl(0.055, 0.1, (0.21, -0.01, 0.98), axis="X", seg=20, r=0.01)
    boom = s.rbox((0.045, 0.06, 0.78), (0.285, -0.01, 0.72), r=0.015, seg=2)
    tip = s.rbox((0.06, 0.075, 0.06), (0.285, -0.01, 0.33), r=0.02, seg=2)
    whip_base = s.rcyl(0.025, 0.05, (-0.13, 0.14, 1.12), seg=12, r=0.006)
    whip = s.rcyl(0.006, 0.5, (-0.13, 0.14, 1.36), seg=8, r=0.002)
    whip_tip = s.ball(0.013, (-0.13, 0.14, 1.615), seg=10)
    s.part("Body", [skirt, chassis, column, neck, pack, *vent, hub, whip_base, whip, whip_tip])

    bumper = ring(0.415, 0.37, 0.05, (0, 0, 0.14), seg=36, r=0.01)
    bands = [s.loft([(z, 0.205 - (z - 0.33) * 0.042, 0.19 - (z - 0.33) * 0.04, 3),
                     (z + 0.07, 0.2 - (z - 0.33) * 0.042, 0.186 - (z - 0.33) * 0.04, 3)], seg=28, round_caps=0.008)
             for z in (0.6, 0.86)]
    balcony = s.loft([(1.1, 0.2, 0.19, 2.6), (1.14, 0.25, 0.24, 2.6), (1.18, 0.24, 0.23, 2.6), (1.2, 0.16, 0.15, 2.6)],
                     seg=28, round_caps=0.008)
    stripes = [s.rbox((0.05, 0.065, 0.08), (0.285, -0.01, z), r=0.012, seg=1) for z in (0.55, 0.75, 0.95)]
    badge = s.rbox((0.14, 0.04, 0.1), (0, -0.185, 0.72), r=0.015, seg=2)
    s.part("Accent", [bumper, *bands, balcony, *stripes, tip, boom, badge])

    for i, (x, y) in enumerate(WHEELS):
        tyre = s.rcyl(WHEEL_R, WHEEL_W, (x, y, WHEEL_R), axis="X", seg=16, r=0.018)
        rim = s.rcyl(0.045, WHEEL_W + 0.012, (x, y, WHEEL_R), axis="X", seg=12, r=0.006)
        w = s.part(f"AccentWheel{i}", [tyre, rim])
        g.set_pivot(w, (x, y, WHEEL_R))

    # head: yoke on a turning hub + searchlight drum (front -Y) with cooling fins
    hub = s.rcyl(0.1, 0.06, (0, 0, HEAD_Z + 0.03), seg=28, r=0.01)
    yoke = s.rbox((0.56, 0.1, 0.05), (0, -0.02, HEAD_Z + 0.07), r=0.015, seg=2)
    arms = [s.rbox((0.05, 0.1, 0.3), (x * 0.255, -0.02, HEAD_Z + 0.2), r=0.015, seg=2) for x in (-1, 1)]
    pins = [s.rcyl(0.04, 0.09, (x * 0.2, -0.02, LAMP_Z), axis="X", seg=16, r=0.006) for x in (-1, 1)]
    drum = s.lathe([(0.0, 0.0), (0.11, 0.004), (0.145, 0.02), (0.155, 0.03), (0.172, 0.034), (0.172, 0.05),
                    (0.155, 0.056), (0.155, 0.07), (0.172, 0.076), (0.172, 0.092), (0.155, 0.098), (0.165, 0.13),
                    (0.175, 0.25), (0.198, 0.275), (0.198, 0.31)], seg=32, axis="-Y", loc=(0, 0.085, LAMP_Z))
    head = s.part("AccentHead", [hub, yoke, *arms, *pins, drum])
    g.set_pivot(head, (0, 0, HEAD_Z))

    lens = s.lathe([(0.178, 0.0), (0.178, 0.006), (0.155, 0.016), (0.09, 0.025), (0.0, 0.028)], seg=32, axis="-Y",
                   loc=(0, -0.222, LAMP_Z))
    s.part("EyeLens", [lens])
    top = LAMP_Z + 0.174  # drum top where it crosses the turn axis
    beacon = s.lathe([(0.055, top - 0.015), (0.058, top + 0.015), (0.05, top + 0.045), (0.03, top + 0.06),
                      (0.0, top + 0.066)], seg=20)
    s.part("EyeBeacon", [beacon])
    return export("robot_warden")


# --------------------------------------------------------------------------- shovel (the first real loot)

SHOVEL_SHIFT = -0.025  # centres the shovel on its length (the game puts items by their middle)


def shovel_parts():
    """A T-handle spade lying flat on the floor along X, 1.09 m: a wooden crossbar grip and shaft, a steel socket with
    rivets and a pointed blade with rolled treads. Returns (wood, steel) bmesh lists."""
    x0 = SHOVEL_SHIFT
    h = 0.03                                             # the shaft's axis: the socket's wide end rests on the floor
    wood = [s.rcyl(0.018, 0.70, (x0 - 0.14, 0, h), axis="X", seg=20, r=0.004),        # shaft -0.49 .. 0.21
            s.rcyl(0.021, 0.15, (x0 - 0.505, 0, h), axis="Y", seg=20, r=0.006)]        # crossbar grip, on the shaft's end
    steel = [s.rcyl(0.021, 0.05, (x0 - 0.46, 0, h), axis="X", seg=20, r=0.003),        # grip ferrule
             s.rcyl(0.021, 0.11, (x0 + 0.245, 0, h), axis="X", seg=20, r=0.003, radius2=0.03)]  # socket (tapers to the blade)
    steel += [s.rcyl(0.006, 0.006, (x0 + 0.23 + k * 0.05, 0, h + 0.022), seg=10, r=0.001) for k in range(2)]  # rivets
    # the blade: a flat plate, square shoulders, straight sides, a rounded point; tilted so its tip touches the floor
    bm = bmesh.new()
    outline = [(0.30, -0.11), (0.48, -0.105), (0.53, -0.08), (0.565, -0.04), (0.58, 0.0),
               (0.565, 0.04), (0.53, 0.08), (0.48, 0.105), (0.30, 0.11)]
    vs = [bm.verts.new((x + x0, y, 0.0)) for x, y in outline]
    face = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    bmesh.ops.translate(bm, vec=Vector((0, 0, 0.005)), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
    import mathutils
    bmesh.ops.rotate(bm, cent=Vector((0.30 + x0, 0, 0)), matrix=mathutils.Matrix.Rotation(math.radians(4.9), 3, "Y"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((0, 0, h - 0.004)), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    steel.append(bm)
    steel.append(s.rcyl(0.007, 0.22, (x0 + 0.303, 0, h + 0.004), axis="Y", seg=12, r=0.002))  # the rolled tread
    return wood, steel


def model_item_shovel_model():
    """The shovel as it is seen: Body = wood, Accent = steel (WorldItem shows it instead of the tinted piece)."""
    wood, steel = shovel_parts()
    s.part("Body", wood)
    s.part("Accent", steel)
    return export("item_shovel_model")


# --------------------------------------------------------------------------- flashlight and batteries

def model_item_flashlight_model():
    """A metal flashlight lying along X, 0.26 m: a ribbed rubber grip, a tail cap, a push switch, the flared head
    with its bezel and lens at +X (the beam comes from the player's head, PlayerController.Flashlight)."""
    h = 0.031                                                   # its axis: the head's rim rests on the floor
    body = [s.rcyl(0.017, 0.19, (-0.035, 0, h), axis="X", seg=20, r=0.003),          # the tube -0.13 .. 0.06
            s.rcyl(0.019, 0.02, (-0.12, 0, h), axis="X", seg=20, r=0.004)]           # tail cap
    body += [s.rcyl(0.0185, 0.006, (-0.09 + k * 0.016, 0, h), axis="X", seg=20, r=0.001) for k in range(5)]  # grip rings
    s.part("Body", body)
    head = [s.rcyl(0.017, 0.045, (0.0825, 0, h), axis="X", seg=24, r=0.002, radius2=0.03),  # the flared head
            s.rcyl(0.031, 0.024, (0.117, 0, h), axis="X", seg=24, r=0.003),               # bezel
            s.rcyl(0.007, 0.02, (-0.01, 0, h + 0.015), seg=12, r=0.002)]                    # the switch
    s.part("Accent", head)
    s.part("AccentLens", [s.rcyl(0.026, 0.004, (0.13, 0, h), axis="X", seg=24, r=0.001)])
    return export("item_flashlight_model")


def model_item_battery_model():
    """A pair of AA cells taped together lying along X, 0.07 m: a coloured label, steel ends, the + nub at +X."""
    h = 0.017
    cells = [s.rcyl(0.0145, 0.05, (0.0, y, h), axis="X", seg=20, r=0.002) for y in (-0.0145, 0.0145)]
    s.part("Body", cells)
    ends = [s.rcyl(0.0145, 0.004, (x, y, h), axis="X", seg=20, r=0.001) for x in (-0.027, 0.027) for y in (-0.0145, 0.0145)]
    ends += [s.rcyl(0.005, 0.005, (0.0315, y, h), axis="X", seg=12, r=0.001) for y in (-0.0145, 0.0145)]
    ends.append(s.rbox((0.012, 0.06, 0.031), (0.0, 0.0, h), r=0.002))                  # the tape round both
    s.part("Accent", ends)
    return export("item_battery_model")


# --------------------------------------------------------------------------- book, tins, screwdriver, belt bag

def model_item_book_model():
    """A hardback lying flat, 0.24 x 0.17, 0.04 thick: two cover boards and a rounded spine (at -Y) with two raised
    bands, the page block inset between the boards (page lines on its edges); the cover is printed (printed.py)."""
    boards = [s.rbox((0.24, 0.16, 0.0045), (0, 0.005, z), r=0.0018, seg=2) for z in (0.00225, 0.03775)]
    spine = s.rbox((0.24, 0.02, 0.04), (0, -0.075, 0.02), r=0.009, seg=3)
    bands = [s.rbox((0.007, 0.012, 0.034), (x, -0.0795, 0.02), r=0.003, seg=2) for x in (-0.085, 0.085)]
    cover = s.part("Body", [*boards, spine, *bands])

    def print_on(n, c, co):                      # the top end is at -X, the spine (-Y) on the left
        if n.z > 0.7 and c.z > 0.03:
            return printed.cell_uv(printed.BOOK, (co.y + 0.075) / 0.16, 0.5 - co.x / 0.24)
        return printed.cell_uv(printed.CLOTH, 0.5 + co.x * 2, 0.5 + (co.y + co.z) * 2)
    printed.paint(cover, print_on)
    pages = s.rbox((0.232, 0.15, 0.033), (0, 0.004, 0.02), r=0.0015, seg=1)
    for z in (0.011, 0.0165, 0.0235, 0.029):
        s.groove(pages, "Z", z, width=0.0012, depth=0.0006)
    s.part("Accent", [pages])
    return export("item_book_model")


def _tin(length, turn):
    """One food tin standing on its base along Z, centred on the axis: a steel shell with thin rolled rims, a paper
    label over nearly all its height, a ring pull on the lid. Returns (steel, label) bmesh lists."""
    L = length
    shell = s.lathe([(0.0, 0.0), (0.036, 0.0), (0.039, 0.0015), (0.0397, 0.0042), (0.0386, 0.0065), (0.0386, L - 0.0065),
                     (0.0397, L - 0.0042), (0.039, L - 0.0015), (0.036, L - 0.0002), (0.0, L - 0.0002)], seg=24)
    label = s.rcyl(0.0392, L - 0.017, (0, 0, L / 2), seg=24, r=0.0005, bevel_seg=1)
    pull = ring(0.011, 0.0075, 0.0018, (0.012 * math.cos(turn), 0.012 * math.sin(turn), L + 0.0004), seg=12, r=0.0006)
    rivet = s.rcyl(0.004, 0.0018, (0, 0, L + 0.0003), seg=8, r=0.0005)        # the rivet the ring hangs on
    return [shell, pull, rivet], [label]


def model_item_cans_model():
    """Canned food: one tin standing (0.08 across, 0.11 tall) and one lying beside it on its side, lid to the front;
    steel shells with rims and ring pulls (Accent), paper labels (Body)."""
    steel, labels = _tin(0.11, 0.6)
    for b in steel + labels:
        s.place(b, (-0.0405, 0, 0))
    lying_steel, lying_labels = _tin(0.1, 2.0)
    for b in lying_steel + lying_labels:                    # on its side: its axis along Y, the lid at -Y
        s.place(b, (0.039, 0.05, 0.04), rot=(math.pi / 2, 0, 0))
    s.part("Body", labels + lying_labels)
    s.part("Accent", steel + lying_steel)
    return export("item_cans_model")


def model_item_screwdriver_model():
    """A flat screwdriver lying along X, 0.23 m: a fluted plastic handle (Body) with a domed butt at -X, a steel
    bolster, shaft and flat blade tip at +X (Accent)."""
    h = 0.0175                                              # the handle's axis: its widest ring rests on the floor
    x0 = -0.115
    handle = s.lathe([(0.0, 0.0), (0.008, 0.0006), (0.0135, 0.003), (0.0165, 0.008), (0.0175, 0.016), (0.0168, 0.04),
                      (0.0162, 0.058), (0.0168, 0.07), (0.0158, 0.08), (0.0118, 0.09), (0.0098, 0.098), (0.0098, 0.104),
                      (0.0, 0.1042)], seg=36, axis="X", loc=(x0, 0, h))
    for v in handle.verts:                                  # six deep flutes along the grip, fading out at both ends
        t = (v.co.x - x0) / 0.104
        if 0.06 < t < 0.84:
            ang = math.atan2(v.co.z - h, v.co.y)
            k = 1 - 0.16 * max(0.0, math.cos(6 * ang + math.pi)) ** 1.5 * math.sin(math.pi * (t - 0.06) / 0.78) ** 0.6
            v.co.y *= k
            v.co.z = h + (v.co.z - h) * k
    s.part("Body", [handle])
    bolster = s.rcyl(0.0085, 0.012, (x0 + 0.106, 0, h), axis="X", seg=20, r=0.0015, radius2=0.0048)
    shaft = s.rcyl(0.0034, 0.12, (x0 + 0.165, 0, h), axis="X", seg=14, r=0.0006)       # 0.105 .. 0.225 from the end
    tip = s.loft([(0.0, 0.0034, 0.0034, 2.0), (0.006, 0.0016, 0.0036, 3.0), (0.011, 0.0007, 0.0034, 4.0)],
                 seg=14, axis="X", loc=(x0 + 0.219, 0, h))
    s.part("Accent", [bolster, shaft, tip])
    return export("item_screwdriver_model")


BAG_RINGS = [(-0.119, 0.0, 0.0), (-0.116, 0.014, 0.026), (-0.108, 0.025, 0.046), (-0.092, 0.032, 0.058),
             (-0.06, 0.036, 0.066), (0.0, 0.037, 0.069)]   # (x, half height, half width) of the pouch, mirrored at +X


def _bag_ring(x):
    """The pouch's (half height, half width) at x, as the loft joins its rings (straight between them)."""
    rings = BAG_RINGS + [(-r[0], r[1], r[2]) for r in reversed(BAG_RINGS[:-1])]
    for (xa, ha, wa), (xb, hb, wb) in zip(rings, rings[1:]):
        if xa <= x <= xb:
            f = (x - xa) / (xb - xa)
            return ha + (hb - ha) * f, wa + (wb - wa) * f
    return 0.0, 0.0


def _pillow(rings, n, z0, y0=0.0, seg=32):
    """A soft pouch along X from (x, half height, half width) rings: superellipse sections, flat bottom at z0."""
    loft_rings = [(x, hz, wy, n, -(z0 + hz), y0) for x, hz, wy in rings]
    bm = s.loft(loft_rings, seg=seg)
    return s.place(bm, rot=(0, math.pi / 2, 0))             # local z -> world x, local x -> world -z (offset above)


def model_item_beltbag_model():
    """A bum bag lying on its back, 0.26 x 0.14, 0.08 high: a soft pouch with a front pocket on top (Body), its strap
    folded under it and out round both ends with a buckle at +X (Accent), zips with pulls (AccentZip)."""
    n = 2.6
    rings = BAG_RINGS + [(-r[0], r[1], r[2]) for r in reversed(BAG_RINGS[:-1])]
    bag = _pillow(rings, n, 0.002, seg=36)
    pocket_rings = [(-0.082, 0.0, 0.0), (-0.078, 0.008, 0.028), (-0.066, 0.013, 0.042), (0.0, 0.015, 0.047),
                    (0.066, 0.013, 0.042), (0.078, 0.008, 0.028), (0.082, 0.0, 0.0)]
    pocket = _pillow(pocket_rings, 2.6, 0.048, y0=-0.012, seg=28)
    s.part("Body", [bag, pocket])
    # the strap: under the bag and out round both ends, back into the bag's sides at mid height
    path = [(-0.09, 0.032), (-0.114, 0.032), (-0.122, 0.029), (-0.127, 0.019), (-0.125, 0.008), (-0.117, 0.0015),
            (-0.05, 0.0015), (0.05, 0.0015), (0.117, 0.0015), (0.125, 0.008), (0.127, 0.019), (0.122, 0.029),
            (0.114, 0.032), (0.09, 0.032)]
    strap = sweep_rect(path, 0.0, 0.034, 0.003, r=0.001)
    s.place(strap, rot=(0, 0, -math.pi / 2))
    buckle = [s.rbox((0.008, 0.042, 0.024), (0.1275, 0, 0.018), r=0.0025, seg=2),
              s.rbox((0.004, 0.03, 0.016), (0.1315, 0, 0.018), r=0.0012, seg=1)]
    s.part("Accent", [strap, *buckle])

    def on_surface(x, ang, hz, wy, z0, y0, proud=0.0004):
        c, sn = math.cos(ang), math.sin(ang)
        y = y0 + (wy + proud) * math.copysign(abs(c) ** (2 / n), c)
        z = z0 + hz + (hz + proud) * abs(sn) ** (2 / n)
        return (x, y, z)

    # main zip along the back top edge, the pocket zip along the pocket's back edge; a pull on each
    zp = [on_surface(x, math.radians(55), *_bag_ring(x), 0.002, 0.0) for x in [-0.092 + 0.184 * k / 8 for k in range(9)]]
    zip_main = s.tube(zp, 0.0022, res=1, segments_per=2)
    pz = [on_surface(x, math.radians(60), 0.015 if abs(x) < 0.066 else 0.013 - (abs(x) - 0.066) * 0.4, 0.047 if abs(x) < 0.066
                     else 0.042, 0.048, -0.012) for x in [-0.07 + 0.14 * k / 6 for k in range(7)]]
    zip_pocket = s.tube(pz, 0.002, res=1, segments_per=2)
    pulls = [s.rbox((0.018, 0.006, 0.0024), (pz[5][0] + 0.004, pz[5][1] - 0.004, pz[5][2] + 0.0005), r=0.0009, seg=1),
             s.rbox((0.018, 0.006, 0.0024), (zp[6][0] + 0.004, zp[6][1] - 0.004, zp[6][2] + 0.0005), r=0.0009, seg=1)]
    s.part("AccentZip", [zip_main, zip_pocket, *pulls])
    return export("item_beltbag_model")


# --------------------------------------------------------------------------- the first-person hand with the flashlight

def model_view_flashlight():
    """What the player sees holding the flashlight: a right forearm in a sleeve coming from the bottom right of the view,
    the hand closed round the flashlight's grip, the light pointing ahead (Blender -Y = the view's forward). Origin at
    the grip; the lens is 0.19 m ahead of it (PlayerController.Flashlight's beam starts there)."""
    import math as m
    r = 0.017
    # the flashlight along -Y: tail cap at +0.09, the flared head and lens at -0.19
    torch = [s.rcyl(r, 0.2, (0, 0.0, 0), axis="Y", seg=20, r=0.003),
             s.rcyl(0.019, 0.02, (0, 0.09, 0), axis="Y", seg=20, r=0.004)]
    torch += [s.rcyl(0.0185, 0.006, (0, 0.05 - k * 0.016, 0), axis="Y", seg=20, r=0.001) for k in range(3)]
    s.part("AccentTorch", torch)
    s.part("AccentHead", [s.rcyl(r, 0.045, (0, -0.1225, 0), axis="Y", seg=24, r=0.002, radius2=0.03),
                          s.rcyl(0.031, 0.024, (0, -0.157, 0), axis="Y", seg=24, r=0.003)])
    s.part("AccentLens", [s.rcyl(0.026, 0.004, (0, -0.17, 0), axis="Y", seg=24, r=0.001)])
    _view_hand()                                               # the right hand closed round the grip (and its sleeve)
    return export("view_flashlight")


def _view_hand(grip=0.017):
    """The right hand closed round a grip along -Y (its axis through the origin, `grip` m thick) and the forearm in its
    sleeve going off to the bottom right: Body = skin, AccentSleeve = the sleeve (shared by the flashlight and the grip)."""
    import math as m
    # a fist, not a claw: four fingers thick enough to touch each other, curled tight round the grip (the knuckles a
    # ridge on top, the tips tucked under), the thumb lying across them; the palm a soft block behind
    fingers = []
    for k in range(4):
        y = 0.033 - k * 0.0205
        rf = 0.0118 - 0.0009 * k
        rr = grip + rf + 0.001                                   # round the grip, just touching it
        pts = [(0.034, y, 0.028)] + [(rr * m.cos(a), y, rr * m.sin(a)) for a in [m.radians(d) for d in (25, -30, -90, -150, -200)]]
        fingers.append(s.tube(pts, rf, res=2, segments_per=4))
    palm = s.rbox((0.042, 0.092, 0.07), (0.044, 0.0, -0.002), r=0.02, seg=3)
    thumb = s.tube([(0.038, 0.034, 0.03), (0.022, 0.008, 0.036), (0.004, -0.02, 0.033), (-0.012, -0.03, 0.022)], 0.0125,
                   res=2, segments_per=4, radii=[1.0, 1.0, 0.92, 0.85])
    wrist = s.rcyl(0.032, 0.06, (0.05, 0.075, -0.012), axis="Y", seg=20, r=0.01, rot=(m.radians(8), 0, m.radians(-12)))
    forearm = s.tube([(0.052, 0.09, -0.014), (0.085, 0.22, -0.05), (0.12, 0.38, -0.09)], 0.034, res=5, segments_per=6,
                     radii=[1.0, 1.15, 1.3])
    s.part("Body", [*fingers, palm, thumb, wrist, forearm])
    sleeve = s.tube([(0.095, 0.25, -0.058), (0.13, 0.42, -0.1)], 0.048, res=5, segments_per=4)
    cuff = s.tube([(0.088, 0.235, -0.054), (0.096, 0.26, -0.06)], 0.05, res=5, segments_per=2)
    s.part("AccentSleeve", [sleeve, cuff])


def model_view_grip():
    """The hand alone, closed round whatever the hand slot holds (PlayerController.HeldHand): the thing is put in the fist
    in Unity (ItemCatalog.HeldPoseOf), its grip on the origin, pointing along -Y (the view's forward). A tight fist (a
    key's bow, a screwdriver's handle): round the flashlight's thickness it read as an empty C."""
    _view_hand(grip=0.009)
    return export("view_grip")


MODELS = {
    "view_grip": model_view_grip,
    "item_diamond": model_item_diamond,
    "item_key": model_item_key,
    "item_stun": model_item_stun,
    "lure_speaker": model_lure_speaker,
    "med_table": model_med_table,
    "workbench": model_workbench,
    "programmer_console": model_programmer_console,
    "crate": model_crate,
    "key_lock": model_key_lock,
    "robot_warden": model_robot_warden,
    "item_shovel_model": model_item_shovel_model,
    "item_flashlight_model": model_item_flashlight_model,
    "item_battery_model": model_item_battery_model,
    "item_book_model": model_item_book_model,
    "item_cans_model": model_item_cans_model,
    "item_screwdriver_model": model_item_screwdriver_model,
    "item_beltbag_model": model_item_beltbag_model,
    "view_flashlight": model_view_flashlight,
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
