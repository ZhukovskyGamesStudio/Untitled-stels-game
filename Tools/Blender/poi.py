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
    s.part("EyeScreen", [band, *cross, screen])
    return export("med_table")


# --------------------------------------------------------------------------- workbench

def screwdriver(x, z_top, length, r):
    handle = s.lathe([(0.0, z_top - 0.1), (r * 0.8, z_top - 0.098), (r, z_top - 0.085), (r, z_top - 0.02),
                      (r * 0.85, z_top), (0.0, z_top + 0.003)], seg=10, loc=(x, 0.355, 0))
    shaft = s.rcyl(0.004, length, (x, 0.355, z_top - 0.1 - length / 2 + 0.01), seg=8, r=0.001)
    return [handle, shaft]


def wrench(x, z, length, r):
    shaft = s.rbox((0.022 * r / 0.03, 0.012, length), (x, 0.362, z), r=0.004, seg=1)
    ends = []
    for dz in (-1, 1):
        e = s.rcyl(r, 0.012, (x, 0.362, z + dz * length / 2), axis="Y", seg=16, r=0.002)
        e = s.boolean(e, s.rbox((r * 0.8, 0.05, r * 1.2), (x, 0.362, z + dz * (length / 2 + r * 0.55)), r=0), "DIFFERENCE")
        ends.append(e)
    return [shaft, *ends]


def model_workbench():
    """Garage workbench 2.0 x 0.8, wooden top at 0.95: steel frame with a three-drawer cabinet on the right and a low
    shelf, a vise on the front-left corner, a pegboard at the back (+Y) with hanging tools and a clamp work lamp."""
    legs = [s.rbox((0.06, 0.06, 0.9), (-0.95, y * 0.35, 0.45), r=0.01, seg=2) for y in (-1, 1)]
    shelf = s.rbox((1.44, 0.7, 0.03), (-0.25, 0, 0.18), r=0.008, seg=1)
    apron = s.rbox((1.42, 0.03, 0.08), (-0.25, -0.36, 0.845), r=0.008, seg=1)
    cabinet = s.rbox((0.52, 0.74, 0.88), (0.71, 0, 0.45), r=0.012, seg=2)
    drawers = [s.rbox((0.48, 0.02, 0.255), (0.71, -0.375, z), r=0.008, seg=2) for z in (0.73, 0.465, 0.2)]
    board = s.rbox((1.96, 0.025, 0.84), (0, 0.385, 1.36), r=0.008, seg=1)
    posts = [s.rbox((0.05, 0.045, 0.92), (x * 0.965, 0.385, 1.39), r=0.01, seg=2) for x in (-1, 1)]
    ledge = s.rbox((0.6, 0.12, 0.02), (-0.15, 0.32, 1.52), r=0.005, seg=1)
    ledge_lip = s.rbox((0.6, 0.012, 0.04), (-0.15, 0.265, 1.535), r=0.004, seg=1)
    # vise: base on the top, fixed jaw at the back, sliding jaw and screw toward the front edge
    vise = [s.rbox((0.17, 0.2, 0.05), (-0.72, -0.27, 0.97), r=0.012, seg=2),
            s.rbox((0.19, 0.06, 0.12), (-0.72, -0.21, 1.045), r=0.012, seg=2),
            s.rbox((0.19, 0.06, 0.1), (-0.72, -0.37, 1.04), r=0.012, seg=2),
            s.rbox((0.08, 0.16, 0.05), (-0.72, -0.29, 1.0), r=0.01, seg=1),
            s.rcyl(0.012, 0.2, (-0.72, -0.43, 1.03), axis="Y", seg=10, r=0.002),
            s.rcyl(0.007, 0.16, (-0.72, -0.53, 1.03), axis="X", seg=8, r=0.002)]
    vise += [s.ball(0.013, (-0.72 + d * 0.08, -0.53, 1.03), seg=10) for d in (-1, 1)]
    lamp_arm = s.tube([(0.62, 0.39, 1.8), (0.62, 0.3, 1.86), (0.62, 0.14, 1.83), (0.62, 0.06, 1.76)], 0.011,
                      segments_per=6)
    clamp = s.rbox((0.06, 0.07, 0.07), (0.62, 0.39, 1.79), r=0.01, seg=2)
    s.part("Body", [*legs, shelf, apron, cabinet, *drawers, board, *posts, ledge, ledge_lip, *vise, lamp_arm, clamp])

    top = s.rbox((2.0, 0.8, 0.06), (0, 0, 0.92), r=0.012, seg=2)
    for x in (-0.6, -0.2, 0.2, 0.6):
        s.groove(top, "X", x, 0.008, 0.003)
    handles = [s.rbox((0.2, 0.03, 0.025), (0.71, -0.392, z + 0.07), r=0.008, seg=1) for z in (0.73, 0.465, 0.2)]
    tools = []
    # hammer
    tools += [s.rbox((0.03, 0.022, 0.3), (-0.75, 0.36, 1.32), r=0.01, seg=1),
              s.rbox((0.14, 0.035, 0.045), (-0.74, 0.358, 1.48), r=0.01, seg=1)]
    tools += wrench(-0.58, 1.33, 0.24, 0.032)
    tools += wrench(-0.48, 1.36, 0.18, 0.025)
    for k, x in enumerate((-0.34, -0.28, -0.22)):
        tools += screwdriver(x, 1.48 - k * 0.02, 0.13 + k * 0.02, 0.016)
    # saw: tapered blade with a closed grip
    blade = s.rbox((0.38, 0.006, 0.11), (0.08, 0.365, 1.3), r=0.002, seg=1, taper=(1, 1))
    for v in blade.verts:
        if v.co.x > 0.08:
            v.co.z = 1.3 + (v.co.z - 1.3) * (1 - 0.55 * (v.co.x - 0.08) / 0.19)
    grip = s.rbox((0.11, 0.025, 0.13), (-0.14, 0.36, 1.33), r=0.02, seg=2)
    grip = s.boolean(grip, s.rbox((0.05, 0.1, 0.06), (-0.135, 0.36, 1.335), r=0.01), "DIFFERENCE")
    tools += [blade, grip]
    # pliers: two crossed handles and jaws
    tools += [s.rbox((0.018, 0.014, 0.2), (0.34 + d * 0.02, 0.362, 1.3), r=0.006, seg=1, rot=(0, d * 0.18, 0))
              for d in (-1, 1)]
    tools += [s.rcyl(0.016, 0.02, (0.34, 0.362, 1.39), axis="Y", seg=12, r=0.003)]
    cans = [s.rcyl(0.045, 0.11, (x, 0.32, 1.585), seg=16, r=0.006) for x in (-0.35, -0.22)]
    cans += [s.rcyl(0.035, 0.08, (0.0, 0.32, 1.57), seg=14, r=0.005)]
    toolbox = s.rbox((0.42, 0.2, 0.16), (0.25, 0.1, 1.03), r=0.02, seg=2)
    box_handle = s.tube([(0.12, 0.1, 1.1), (0.14, 0.1, 1.15), (0.36, 0.1, 1.15), (0.38, 0.1, 1.1)], 0.01,
                        smooth=False, segments_per=1)
    shade = s.lathe([(0.03, 0.0), (0.034, 0.02), (0.06, 0.08), (0.075, 0.12), (0.07, 0.122)], seg=20,
                    axis="-Z", loc=(0.62, 0.05, 1.78))
    s.part("Accent", [top, *handles, *tools, *cans, toolbox, box_handle, shade])

    bulb = s.rcyl(0.064, 0.01, (0.62, 0.05, 1.664), seg=20, r=0.003)
    s.part("EyeLamp", [bulb])
    return export("workbench")


# --------------------------------------------------------------------------- programming console

SHELF_TOP = 1.0
SLOT_PITCH = 0.47
SLOT_Y = 0.215
SLOTS_X = [(-1.5 + k) * SLOT_PITCH for k in range(4)]  # -0.705, -0.235, 0.235, 0.705


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
    s.part("EyeScreen", [*screens, *leds, code])
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
    bands, the page block inset between the boards (page lines on its edges), a title label on the front cover."""
    boards = [s.rbox((0.24, 0.16, 0.0045), (0, 0.005, z), r=0.0018, seg=2) for z in (0.00225, 0.03775)]
    spine = s.rbox((0.24, 0.02, 0.04), (0, -0.075, 0.02), r=0.009, seg=3)
    bands = [s.rbox((0.007, 0.012, 0.034), (x, -0.0795, 0.02), r=0.003, seg=2) for x in (-0.085, 0.085)]
    s.part("Body", [*boards, spine, *bands])
    pages = s.rbox((0.232, 0.15, 0.033), (0, 0.004, 0.02), r=0.0015, seg=1)
    for z in (0.011, 0.0165, 0.0235, 0.029):
        s.groove(pages, "Z", z, width=0.0012, depth=0.0006)
    s.part("Accent", [pages])
    s.part("AccentLabel", [s.rbox((0.13, 0.08, 0.002), (0.01, 0.012, 0.0405), r=0.0008, seg=1)])
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
    # the hand: four fingers curled round the grip from the right, the palm on the right side, the thumb over the top
    fingers = []
    for k in range(4):
        y = 0.035 - k * 0.022
        pts = [(0.03 + 0.012 * m.cos(a), y, 0.03 * m.sin(a)) if a > 2.6 else (0.031 * m.cos(a), y, 0.031 * m.sin(a))
               for a in [m.radians(d) for d in (40, 0, -40, -90, -140, -185)]]
        fingers.append(s.tube(pts, 0.0095 - 0.0008 * k, res=2, segments_per=4))
    palm = s.rbox((0.04, 0.095, 0.075), (0.042, 0.0, -0.004), r=0.018, seg=3)
    thumb = s.tube([(0.035, 0.03, 0.028), (0.018, 0.0, 0.034), (0.002, -0.03, 0.03)], 0.011, res=2, segments_per=4)
    wrist = s.rcyl(0.032, 0.06, (0.05, 0.075, -0.012), axis="Y", seg=20, r=0.01, rot=(m.radians(8), 0, m.radians(-12)))
    forearm = s.tube([(0.052, 0.09, -0.014), (0.085, 0.22, -0.05), (0.12, 0.38, -0.09)], 0.034, res=5, segments_per=6,
                     radii=[1.0, 1.15, 1.3])
    s.part("Body", [*fingers, palm, thumb, wrist, forearm])
    sleeve = s.tube([(0.095, 0.25, -0.058), (0.13, 0.42, -0.1)], 0.048, res=5, segments_per=4)
    cuff = s.tube([(0.088, 0.235, -0.054), (0.096, 0.26, -0.06)], 0.05, res=5, segments_per=2)
    s.part("AccentSleeve", [sleeve, cuff])
    return export("view_flashlight")


MODELS = {
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
