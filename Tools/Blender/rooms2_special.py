"""Special pieces: the two keys (door key, the hostage cage's old iron key), the bank vault (round door, its frame,
a wall of deposit boxes, a pallet of gold, a cash trolley, a cash shelf) and door parts (a padlock, one leaf of a heavy
street door).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms2_special.py -- Assets/_Game/Art/Models [--only a,b]
      (files land in the sub-folders generate_models.category picks: item_* -> <root>/Items, the rest -> <root>/Props)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--focus x,y,z,r] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y, z up, origin on
the floor in the middle of the footprint, except where a model says otherwise:
  * the keys lie flat along X like item_shovel_model, centred, on the floor (the bow at +X);
  * vault_door: the origin is on the hinge axis at floor level; the disc (d 2.2, 0.4 thick) is centred 1.1 m off it
    and 1.15 m up — Blender x -2.2..0 (Unity x 0..2.2: Unity x = -Blender x), knuckles to Blender x +0.075;
  * padlock: the origin at the top of the shackle, the lock hangs below it.
Object names are material roles: Body, Accent*, Trim*, Eye* (glows). Forms come from shapes.py and are finished by
shapes.part; export() goes through meshfix (floor=True) like poi.export.
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --, category())
import meshfix  # noqa: E402
import shapes as s  # noqa: E402


def export(filename):
    """meshfix (everything rests on its lowest point), then the FBX into <root>/<category>/."""
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=filename)
    path = os.path.join(g.out_dir, g.category(filename), filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def prism(outline, z0, h):
    """A flat plate from a 2D outline (counter-clockwise (x, y) points), bottom at z0, h thick."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z0)) for x, y in outline]
    face = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    bmesh.ops.translate(bm, vec=Vector((0, 0, h)), verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def torus(R, r, loc=(0, 0, 0), seg=24, seg_r=8, rot=(0, 0, 0)):
    """A ring of round wire (key ring, shackle bend) in the XY plane, R to the wire's centre line."""
    bm = bmesh.new()
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        c = Vector((math.cos(a), math.sin(a), 0))
        rings.append([bm.verts.new(c * (R + r * math.cos(2 * math.pi * j / seg_r))
                                   + Vector((0, 0, r * math.sin(2 * math.pi * j / seg_r)))) for j in range(seg_r)])
    for i in range(seg):
        a, b = rings[i], rings[(i + 1) % seg]
        for j in range(seg_r):
            bm.faces.new((a[j], b[j], b[(j + 1) % seg_r], a[(j + 1) % seg_r]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return s.place(bm, loc, rot)


def flat_ring(r_out, r_in, depth, loc=(0, 0, 0), axis="Z", seg=32, r=0.003):
    """Flat ring (washer, bezel, trefoil lobe) along an axis, edges chamfered (poi.ring)."""
    bm = bmesh.new()
    loops = []
    for rad, z in ((r_out, -depth / 2), (r_out, depth / 2), (r_in, depth / 2), (r_in, -depth / 2)):
        loops.append([bm.verts.new((rad * math.cos(2 * math.pi * k / seg), rad * math.sin(2 * math.pi * k / seg), z))
                      for k in range(seg)])
    for a, b in zip(loops, loops[1:] + loops[:1]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if r > 0:
        s.bevel_edges(bm, min(r, 0.3 * min(depth, r_out - r_in)), 1, angle=50)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


def on_disc(centre, radius, theta, y):
    """A point on a disc in the XZ plane (the vault door's face): angle theta from +X towards +Z."""
    return (centre[0] + radius * math.cos(theta), y, centre[2] + radius * math.sin(theta))


def brick(x, y, z0, size, rot_z=0.0, sink=0.002):
    """A strapped brick of banknotes standing on z0 (sunk 2 mm): (brick, paper band round its middle)."""
    sx, sy, sz = size
    b = s.rbox(size, (0, 0, sz / 2), r=min(0.006, sz * 0.12), seg=1, vertical_only=True)
    band = s.rbox((min(0.032, sx * 0.22), sy + 0.004, sz + 0.004), (0, 0, sz / 2), r=0)
    for m in (b, band):
        s.place(m, (x, y, z0 - sink), (0, 0, rot_z))
    return b, band


def money_bag(x, y, z0, h=0.24, w=0.085, rot_z=0.0):
    """A canvas money bag cinched at the neck, a tuft above the tie: (bag, tie)."""
    bag = s.loft([(0.0, 0.0, 0.0), (0.004, w * 0.62, w * 0.5, 2.2), (0.03, w * 0.95, w * 0.78, 2.2),
                  (h * 0.38, w, w * 0.82, 2.2), (h * 0.62, w * 0.82, w * 0.68, 2.2), (h * 0.8, w * 0.3, w * 0.26),
                  (h * 0.9, w * 0.42, w * 0.36), (h * 0.97, w * 0.38, w * 0.3), (h, 0.0, 0.0)], seg=12)
    tie = s.rcyl(w * 0.3, h * 0.06, (0, 0, h * 0.83), seg=10, r=0.003, bevel_seg=1)
    for m in (bag, tie):
        s.place(m, (x, y, z0 - 0.004), (0, 0, rot_z))
    return bag, tie


# --------------------------------------------------------------------------- items

def model_item_key_model():
    """A modern pin-tumbler door key lying flat, 0.085 m with its ring and tag: a brass blade with five V-cuts, a
    keyway groove and a shoulder stop, a rounded bow with a hole; a steel split ring through it holds a coloured
    plastic label tag with a sunk label window. Body = brass, Accent = the steel ring, Trim = the plastic tag."""
    t = 0.0022                                       # key thickness
    sh = -0.0425                                     # centres the whole thing on its length
    pts = [(0.0305, 0.0040), (0.003, 0.0040), (0.0, 0.0024), (0.0, 0.0002), (0.0035, -0.0040)]
    for xc, d in ((0.0075, 0.0016), (0.012, 0.0026), (0.0165, 0.0010), (0.021, 0.0021), (0.0255, 0.0013)):
        pts += [(xc - 0.0018, -0.0040), (xc - 0.0004, -0.0040 + d), (xc + 0.0004, -0.0040 + d), (xc + 0.0018, -0.0040)]
    pts += [(0.0285, -0.0040), (0.0285, -0.0058), (0.038, -0.0058), (0.038, 0.0047), (0.0305, 0.0047)]
    blade = prism(pts, 0.0, t)
    bow = s.loft([(0.0, 0.0115, 0.0122, 2.4), (t, 0.0115, 0.0122, 2.4)], seg=28, loc=(0.0445, 0, 0))
    key = s.boolean(blade, bow, "UNION")
    key = s.boolean(key, s.rcyl(0.0029, 0.01, (0.0505, 0, t / 2), seg=16, r=0, rot=(0, 0, math.pi / 16)), "DIFFERENCE")
    key = s.boolean(key, s.rbox((0.029, 0.0009, 0.002), (0.0165, 0.0012, t + 0.0004), r=0), "DIFFERENCE")  # keyway
    s.bevel_edges(key, 0.00035, 1, angle=50)
    s.place(key, (sh, 0, 0))
    s.part("Body", [key])

    ring = torus(0.0085, 0.0009, (0.059 + sh, 0, 0.0011), seg=28, seg_r=8)
    s.part("Accent", [ring])

    tag = s.loft([(0.0, 0.0115, 0.0085, 3.2), (0.0026, 0.0115, 0.0085, 3.2)], seg=24, loc=(0.0735, 0, 0))
    tag = s.boolean(tag, s.rcyl(0.0026, 0.01, (0.0675, 0, 0.0013), seg=16, r=0, rot=(0, 0, math.pi / 16)), "DIFFERENCE")
    tag = s.boolean(tag, s.rbox((0.0125, 0.0112, 0.001), (0.0775, 0, 0.0026), r=0), "DIFFERENCE")  # label window
    s.bevel_edges(tag, 0.0004, 1, angle=50)
    s.place(tag, (sh, 0, 0))
    s.part("Trim", [tag])
    return export("item_key_model")


def model_item_cagekey_model():
    """The hostage cage's key: a big old iron key lying flat, 0.18 m — a trefoil bow of three lobes with a brass boss,
    brass collars on a round shank, a large flat bit with three ward cuts and a pin tip. Body = dark iron, Accent =
    brass collars and boss."""
    zc = 0.0095                                      # the shank's axis: the brass collars rest on the floor
    iron = []
    iron.append(s.rcyl(0.0065, 0.135, (-0.0205, 0, zc), axis="X", seg=10, r=0.0015, bevel_seg=1))  # shank -0.088 .. 0.047
    iron.append(s.rcyl(0.0078, 0.008, (-0.0865, 0, zc), axis="X", seg=10, r=0.002, bevel_seg=1))   # the pin tip
    bit = s.rbox((0.034, 0.033, 0.0062), (-0.068, -0.0145, zc), r=0)                            # y +0.002 .. -0.031
    for x, depth, w in ((-0.075, 0.013, 0.0045), (-0.064, 0.009, 0.004), (-0.056, 0.016, 0.0035)):
        bit = s.boolean(bit, s.rbox((w, 2 * depth, 0.02), (x, -0.031, zc), r=0), "DIFFERENCE")
    bit = s.boolean(bit, s.rbox((0.008, 0.016, 0.02), (-0.085, -0.031, zc), r=0), "DIFFERENCE")  # stepped corner
    s.bevel_edges(bit, 0.0008, 1, angle=50)
    iron.append(bit)
    # the trefoil bow: the outline of three overlapping circles round c (star-shaped from c), three round holes
    c = Vector((0.064, 0, zc))
    R, d, t = 0.0135, 0.011, 0.0058
    centres = [Vector((d * math.cos(2 * math.pi * k / 3), d * math.sin(2 * math.pi * k / 3), 0)) for k in range(3)]
    outline = []
    for i in range(36):
        u = Vector((math.cos(2 * math.pi * i / 36 + math.pi / 36), math.sin(2 * math.pi * i / 36 + math.pi / 36), 0))
        rho = max(u.dot(p) + math.sqrt(max(0.0, u.dot(p) ** 2 - d * d + R * R)) for p in centres)
        outline.append((c.x + rho * u.x, c.y + rho * u.y))
    bow = prism(outline, zc - t / 2, t)
    for p in centres:
        bow = s.boolean(bow, s.rcyl(0.0074, 0.03, tuple(c + p), seg=12, r=0, rot=(0, 0, math.pi / 12)), "DIFFERENCE")
    s.bevel_edges(bow, 0.0009, 1, angle=50)
    iron.append(bow)
    iron.append(s.rcyl(0.0058, 0.012, (0.046, 0, zc), axis="X", seg=12, r=0.002, bevel_seg=1, radius2=0.0075))
    s.part("Body", iron)
    brass = [s.rcyl(0.0095, 0.008, (x, 0, zc), axis="X", seg=16, r=0.0022) for x in (-0.046, 0.036)]
    brass.append(s.rcyl(0.0085, 0.005, (-0.008, 0, zc), axis="X", seg=16, r=0.0018))
    brass.append(s.rcyl(0.0052, 0.0086, tuple(c), seg=14, r=0.0018))                             # boss in the bow
    s.part("Accent", brass)
    return export("item_cagekey_model")


# --------------------------------------------------------------------------- vault

DOOR_C = (-1.1, 0.0, 1.15)   # disc centre (Blender); the hinge axis is x = 0


def model_vault_door():
    """Round bank-vault door, d 2.2, 0.4 thick, hinged on the axis x = 0: a stepped plug behind (the part that goes into
    the frame) with radial locking bolts, a polished face with a raised rim of bolt heads, a seam ring, a ridge and a
    centre boss, a big three-spoke wheel with ball grips and a combination dial; two hinge arms and knuckles on a pin.
    Body = steel slab, Accent = dark steel bolts and hinge, TrimWheel = the brass wheel (spun about the disc's axis when
    the door opens), TrimDial = the combination dial."""
    cx, _, cz = DOOR_C
    prof = [(0.0, 0.0), (1.0, 0.0), (1.02, 0.02), (1.02, 0.16), (1.09, 0.16), (1.10, 0.17), (1.10, 0.37),
            (1.085, 0.385), (0.94, 0.385), (0.925, 0.37), (0.905, 0.33), (0.89, 0.32), (0.85, 0.32), (0.845, 0.31),
            (0.825, 0.31), (0.82, 0.32), (0.79, 0.32), (0.785, 0.33), (0.765, 0.33), (0.76, 0.32), (0.23, 0.32),
            (0.22, 0.335), (0.19, 0.35), (0.0, 0.35)]
    slab = s.lathe(prof, seg=56, axis="-Y", loc=(cx, 0.2, cz))      # t -> y = 0.2 - t: back +0.2, rim front -0.185
    s.part("Body", [slab])

    bolts = []
    for k in range(20):                                                # bolt heads round the front rim
        th = 2 * math.pi * (k + 0.5) / 20
        bolts.append(s.rcyl(0.034, 0.03, on_disc(DOOR_C, 1.012, th, -0.19), axis="Y", seg=10, r=0.012, bevel_seg=1))
    for k in range(18):                                                # locking bolts out of the plug's edge
        th = 2 * math.pi * k / 18
        if abs(math.atan2(math.sin(th), math.cos(th))) < math.radians(50):
            continue                                                   # none on the hinge side
        p = on_disc(DOOR_C, 1.04, th, 0.11)
        bolts.append(s.rcyl(0.034, 0.12, p, axis="X", seg=10, r=0.008, bevel_seg=1, rot=(0, -th, 0)))
    s.part("AccentBolts", bolts)
    acc = []
    for z in (0.55, 1.75):                                             # hinge arms and knuckles
        acc.append(s.rbox((0.5, 0.17, 0.2), (-0.2, 0.0, z), r=0.02, seg=2))
        acc.append(s.rcyl(0.075, 0.28, (0.0, 0.0, z), seg=20, r=0.012))
    acc.append(s.rcyl(0.036, 1.5, (0.0, 0.0, 1.15), seg=14, r=0.006))  # the pin
    for z in (0.39, 1.91):
        acc.append(s.rcyl(0.05, 0.03, (0.0, 0.0, z), seg=14, r=0.008))
    acc.append(s.rcyl(0.12, 0.032, on_disc(DOOR_C, 0.55, math.pi / 2, -0.124), axis="Y", seg=28, r=0.006))  # dial base
    s.part("Accent", acc)

    brass = [s.rcyl(0.11, 0.06, (cx, -0.17, cz), axis="Y", seg=28, r=0.014),            # hub (t 0.34 .. 0.40)
             s.rcyl(0.06, 0.012, (cx, -0.196, cz), axis="Y", seg=20, r=0.004)]
    for th in (math.radians(30), math.radians(150), math.radians(270)):
        brass.append(s.rcyl(0.032, 0.5, on_disc(DOOR_C, 0.3, th, -0.165), axis="X", seg=12, r=0.005, rot=(0, -th, 0)))
        brass.append(s.ball(0.05, on_disc(DOOR_C, 0.56, th, -0.15), seg=14))
    s.part("TrimWheel", brass)                                         # turns when the door is opened (AutoDoor.Wheel)
    brass = []
    d = on_disc(DOOR_C, 0.55, math.pi / 2, 0)
    brass.append(s.lathe([(0.0, 0.0), (0.088, 0.0), (0.09, 0.006), (0.084, 0.012), (0.09, 0.018), (0.084, 0.024),
                          (0.09, 0.03), (0.082, 0.04), (0.04, 0.046), (0.0, 0.046)], seg=28, axis="-Y",
                         loc=(d[0], -0.133, d[2])))                     # knurled dial, t 0.333 .. 0.379
    brass.append(s.rbox((0.012, 0.012, 0.03), (d[0], -0.178, d[2] + 0.06), r=0.003, seg=1))  # index mark on the dial
    s.part("TrimDial", brass)
    return export("vault_door")


def model_vault_frame():
    """The steel ring of the round door set into the wall (the wall's centre plane at y = 0, its faces at y = +-0.1;
    +Y = the corridor): a chunky collar 0.1 proud of the corridor's face with a stepped lip round the bore, the sleeve
    lining the bore d 2.27 (centre 1.15 up) through the wall and 1 cm out of the vault's face, rivets round the collar;
    all cut at the floor. The corners between the bore and the doorway are wall (AssetBuilder.WallCornerMeshes).
    Body = steel collar and sleeve, Accent = dark steel lip and rivets."""
    cut = lambda bm, z: s.boolean(bm, s.rbox((3.0, 0.6, 1.0), (0, 0, z - 0.5), r=0), "DIFFERENCE")
    collar = cut(flat_ring(1.26, 1.13, 0.105, (0, 0.1475, 1.15), axis="Y", seg=64, r=0.014), 0.003)  # y 0.095 .. 0.2
    sleeve = cut(flat_ring(1.15, 1.135, 0.213, (0, -0.0045, 1.15), axis="Y", seg=64, r=0.004), 0.003)  # y -0.111 .. 0.102
    s.part("Body", [collar, sleeve])
    lip = cut(flat_ring(1.19, 1.125, 0.117, (0, 0.1565, 1.15), axis="Y", seg=64, r=0.01), 0.006)     # y 0.098 .. 0.215
    s.part("Accent", [lip])
    acc = []
    for k in range(24):
        th = 2 * math.pi * (k + 0.5) / 24
        x, z = 1.226 * math.cos(th), 1.15 + 1.226 * math.sin(th)
        if z < 0.06:
            continue
        acc.append(s.rcyl(0.016, 0.012, (x, 0.203, z), axis="Y", seg=8, r=0.003, bevel_seg=1))
    s.part("AccentRivets", acc)
    return export("vault_frame")


def model_deposit_wall():
    """A wall of safe-deposit boxes 2.0 x 0.35 x 2.4: a steel carcass on a plinth under a cornice, six columns of
    small doors (two rows of tall boxes below, seven rows of small ones), each with a brass keyhole escutcheon and a
    number plate. Body = steel, Accent = brass."""
    body = [s.rbox((2.0, 0.33, 2.4), (0, 0.01, 1.2), r=0.012, seg=2),               # back flush at +0.175
            s.rbox((1.99, 0.03, 0.1), (0, -0.157, 0.052), r=0.008, seg=2),           # plinth, front -0.172
            s.rbox((2.01, 0.033, 0.1), (0, -0.1585, 2.348), r=0.01, seg=2)]          # cornice, front -0.175
    acc = []
    cols, pitch_x = 6, 1.96 / 6
    rows = [0.36, 0.36] + [1.44 / 7] * 7
    z = 0.12
    for k, h in enumerate(rows):
        for c in range(cols):
            x = -0.98 + pitch_x * (c + 0.5)
            w, hh = pitch_x - 0.016, h - 0.016
            body.append(s.rbox((w, 0.02, hh), (x, -0.163, z + h / 2), r=0.005, seg=1))   # door, front -0.173
            acc.append(s.rcyl(0.011 if k > 1 else 0.016, 0.008, (x + w * 0.3, -0.1735, z + h / 2), axis="Y",
                              seg=8, r=0))
            acc.append(s.rbox((0.05, 0.004, 0.018), (x - w * 0.18, -0.1735, z + h / 2 + (0.03 if k > 1 else 0.1)),
                              r=0))
        z += h
    s.part("Body", body)
    s.part("Accent", acc)
    return export("deposit_wall")


def model_gold_pallet():
    """A wooden block pallet 1.0 x 0.8 with gold ingots in a neat pyramid of six layers, each layer offset half a
    bar on the one below. Body = gold, Accent = wood."""
    wood = []
    for y in (-0.34, 0.0, 0.34):
        wood.append(s.rbox((1.0, 0.11, 0.022), (0, y, 0.011), r=0.004, seg=1))       # bottom boards
        wood.append(s.rbox((0.98, 0.104, 0.022), (0, y, 0.111), r=0.004, seg=1))     # stringer boards
        for x in (-0.44, 0.0, 0.44):
            wood.append(s.rbox((0.11, 0.1, 0.082), (x, y, 0.061), r=0.006, seg=1))   # blocks
    for k in range(7):
        wood.append(s.rbox((0.095, 0.8, 0.022), (-0.45 + k * 0.15, 0, 0.131), r=0.004, seg=1))  # deck (top 0.142)
    s.part("Accent", wood)

    bar, h = (0.24, 0.12), 0.075
    layers = [(4, 6), (3, 5), (2, 4), (1, 3), (1, 2), (1, 1)]
    gold = []
    z = 0.14
    for nx, ny in layers:
        for i in range(nx):
            for j in range(ny):
                x = (i - (nx - 1) / 2) * 0.245
                y = (j - (ny - 1) / 2) * 0.125
                gold.append(s.rbox((bar[0], bar[1], h), (x, y, z + h / 2), r=0.008, seg=1, taper=(0.86, 0.8)))
        z += h - 0.002
    s.part("Body", gold)
    return export("gold_pallet")


def model_money_cart():
    """A bank cash trolley 0.9 x 0.6 x 1.1: two deep trays on corner posts with a top rail, a push handle with a
    rubber grip on the +x end, four castors; both trays loaded with strapped bricks of banknotes.
    Body = light painted steel frame and the paper bands, Accent = rubber (castors, grip), Trim = the banknotes."""
    x0, x1 = -0.44, 0.36
    xm, ln = (x0 + x1) / 2, x1 - x0
    body = []
    for z in (0.19, 0.625):
        tray = s.rbox((ln, 0.56, 0.05), (xm, 0, z), r=0.012, seg=2)
        s.inset_faces(tray, s.facing((0, 0, 1), min_area=0.1, tol=0.95), thickness=0.02, depth=-0.03)
        body.append(tray)
    posts = [(x0 + 0.016, -0.265), (x0 + 0.016, 0.265), (x1 - 0.016, -0.265), (x1 - 0.016, 0.265)]
    for px, py in posts:
        body.append(s.rcyl(0.015, 0.8, (px, py, 0.565), seg=12, r=0.004))
    for py in (-0.265, 0.265):                                        # top rails
        body.append(s.rcyl(0.014, ln - 0.03, (xm, py, 0.955), axis="X", seg=12, r=0.004))
    body.append(s.rcyl(0.014, 0.53, (x0 + 0.016, 0, 0.955), axis="Y", seg=12, r=0.004))
    half = [(x1 - 0.016, -0.265, 0.8), (x1 + 0.058, -0.252, 0.96), (x1 + 0.07, -0.238, 1.025),
            (x1 + 0.075, -0.215, 1.05), (x1 + 0.075, -0.19, 1.055)]
    body.append(s.tube(half + [(x, -y, z) for x, y, z in reversed(half)], 0.014, res=2, smooth=False))  # push handle
    acc = [s.rcyl(0.022, 0.3, (x1 + 0.075, 0, 1.055), axis="Y", seg=14, r=0.006)]   # grip (top 1.077)
    for px in (x0 + 0.07, x1 - 0.07):
        for py in (-0.21, 0.21):
            body.append(s.rbox((0.07, 0.07, 0.012), (px, py, 0.16), r=0.003, seg=1))      # swivel plate
            body.append(s.rbox((0.022, 0.05, 0.07), (px + 0.01, py, 0.12), r=0.005, seg=1))  # fork
            acc.append(s.rcyl(0.055, 0.032, (px + 0.02, py, 0.055), axis="Y", seg=18, r=0.008))
    cash, bands = [], []
    size = (0.17, 0.088, 0.09)
    for z0, layers in ((0.185, [(4, 5, 0.0, 0.0), (2, 3, 0.09, 0.0)]),
                       (0.62, [(4, 5, 0.0, 0.0), (3, 3, 0.045, 0.047), (1, 2, 0.0, 0.0)])):
        z = z0
        for nx, ny, ox, oy in layers:
            for i in range(nx):
                for j in range(ny):
                    x = xm + (i - (nx - 1) / 2) * 0.178 + ox
                    y = (j - (ny - 1) / 2) * 0.096 + oy
                    b, band = brick(x, y, z, size, rot_z=math.radians(((i * 7 + j * 3) % 5 - 2) * 1.2))
                    cash.append(b)
                    bands.append(band)
            z += size[2] - 0.002
    body += bands
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Trim", cash)
    return export("money_cart")


def model_cash_shelf():
    """Steel shelving 1.6 x 0.5 x 2.0 (wall piece): four posts, five shelves with folded front lips, X-braces on the
    back and the sides; the four lower shelves hold stacks of strapped cash bricks and cinched money bags.
    Body = steel, Accent = the banknotes, Trim = canvas bags and paper bands."""
    body = []
    for x in (-0.779, 0.779):
        for y in (-0.229, 0.229):
            body.append(s.rbox((0.042, 0.042, 2.0), (x, y, 1.0), r=0.005, seg=1))
    tops = []
    for zc in (0.12, 0.58, 1.04, 1.5, 1.96):
        body.append(s.rbox((1.594, 0.494, 0.035), (0, 0, zc), r=0.006, seg=1))
        body.append(s.rbox((1.55, 0.014, 0.06), (0, -0.242, zc - 0.018), r=0.005, seg=1))  # folded lip
        tops.append(zc + 0.0175)
    for (a, b) in (((-0.779, 0.236, 0.15), (0.779, 0.236, 1.93)), ((0.779, 0.236, 0.15), (-0.779, 0.236, 1.93))):
        body.append(s.tube([a, b], 0.007, res=1, smooth=False))
    for x in (-0.785, 0.785):
        for (a, b) in (((x, -0.229, 0.15), (x, 0.229, 1.93)), ((x, 0.229, 0.15), (x, -0.229, 1.93))):
            body.append(s.tube([a, b], 0.007, res=1, smooth=False))
    s.part("Body", body)

    cash, trim, bags_out = [], [], []
    size = (0.16, 0.075, 0.065)
    # per shelf: brick blocks (x of the block's left column, columns, rows deep, layers) and bags (x, y)
    plan = [
        ([(0.05, 4, 2, 3)], [(-0.6, -0.08), (-0.42, 0.05), (-0.27, -0.1)]),
        ([(-0.7, 4, 2, 3), (0.15, 3, 2, 2)], [(0.057, 0.0)]),
        ([(-0.7, 3, 2, 2), (0.2, 3, 2, 3)], [(-0.09, -0.05), (0.08, 0.09)]),
        ([(-0.3, 4, 2, 2)], [(-0.6, -0.04), (0.47, -0.1), (0.65, 0.06)]),
    ]
    for shelf, (blocks, bags) in enumerate(plan):
        z0 = tops[shelf]
        for bx, nx, ny, nl in blocks:
            for layer in range(nl):
                for i in range(nx - (layer % 2)):
                    for j in range(ny):
                        x = bx + size[0] / 2 + i * 0.168 + (0.084 if layer % 2 else 0)
                        y = -0.15 + j * 0.082 + (0.03 if ny < 3 else 0)
                        b, band = brick(x, y, z0 + layer * (size[2] - 0.002), size,
                                        rot_z=math.radians(((i * 5 + j * 3 + layer) % 5 - 2) * 1.5))
                        cash.append(b)
                        trim.append(band)
        for k, (x, y) in enumerate(bags):
            bag, tie = money_bag(x, y, z0, h=0.22 + 0.03 * (k % 2), w=0.08 + 0.01 * ((k + shelf) % 2),
                                 rot_z=0.4 * k + shelf)
            bags_out += [bag, tie]
    s.part("Accent", cash)
    s.part("Trim", trim)
    s.part("TrimBags", bags_out)
    return export("cash_shelf")


# --------------------------------------------------------------------------- doors

def model_padlock():
    """A chunky hanging padlock 0.12 x 0.16 facing -Y: a brass body with two seams and a steel escutcheon with a keyhole,
    a hardened steel shackle above. Origin at the top of the shackle; the lock hangs below it. Body = brass,
    Accent = steel."""
    lock = s.rbox((0.12, 0.046, 0.095), (0, 0, -0.1125), r=0.014, seg=3)     # z -0.16 .. -0.065
    s.groove(lock, "Z", -0.085, 0.006, 0.0025)
    s.groove(lock, "Z", -0.142, 0.006, 0.0025)
    s.part("Body", [lock])
    rr, arc_r, xc = 0.0105, 0.031, 0.0
    zc = -rr - arc_r                                                            # top of the shackle at z 0
    pts = [(-arc_r, 0, -0.08), (-arc_r, 0, zc - 0.012)]
    for k in range(9):
        a = math.pi - math.pi * k / 8
        pts.append((xc + arc_r * math.cos(a), 0, zc + arc_r * math.sin(a)))
    pts += [(arc_r, 0, zc - 0.012), (arc_r, 0, -0.08)]
    shackle = s.tube(pts, rr, res=2, smooth=False)
    plate = s.rcyl(0.019, 0.006, (0, -0.0235, -0.115), axis="Y", seg=20, r=0.002)
    hole = s.boolean(s.rcyl(0.0045, 0.02, (0, -0.03, -0.111), axis="Y", seg=12, r=0),
                     s.rbox((0.0035, 0.02, 0.012), (0, -0.03, -0.118), r=0), "UNION")
    plate = s.boolean(plate, hole, "DIFFERENCE")
    s.bevel_edges(plate, 0.0008, 1, angle=50)
    s.part("Accent", [shackle, plate])
    return export("padlock")


def model_exterior_door_panel():
    """One leaf of a heavy street door for a sliding pair: 0.9 x 2.28 x 0.12 — chunky stiles and rails round a thinner
    core, a small wired-glass window high up in a steel bead, a horizontal push bar on two housings on the front,
    a pull handle on the back, a steel kick plate. Body = dark painted steel, Accent = metal, Eye = the glass."""
    wx, wz = 0.3, 0.5
    zw = 1.62
    core = s.rbox((0.8, 0.064, 2.2), (0, 0, 1.14), r=0.006, seg=1)
    core = s.boolean(core, s.rbox((wx, 0.2, wz), (0, 0, zw), r=0.02, seg=2, vertical_only=False), "DIFFERENCE")
    s.bevel_edges(core, 0.003, 1, angle=50)
    body = [core]
    for x in (-0.395, 0.395):
        body.append(s.rbox((0.11, 0.09, 2.28), (x, 0, 1.14), r=0.014, seg=2))        # stiles (chunky edge)
    body.append(s.rbox((0.7, 0.088, 0.13), (0, 0, 2.21), r=0.01, seg=2))              # top rail
    body.append(s.rbox((0.7, 0.088, 0.3), (0, 0, 0.152), r=0.01, seg=2))              # bottom rail (kick plate)
    body.append(s.rbox((0.7, 0.088, 0.14), (0, 0, 1.02), r=0.01, seg=2))              # lock rail (push bar)
    s.part("Body", body)

    acc = []
    bead = s.rbox((wx + 0.07, 0.084, wz + 0.07), (0, 0, zw), r=0.008, seg=2)
    bead = s.boolean(bead, s.rbox((wx - 0.01, 0.2, wz - 0.01), (0, 0, zw), r=0.015, seg=2), "DIFFERENCE")
    s.bevel_edges(bead, 0.003, 1, angle=50)
    acc.append(bead)
    for k in range(1, 6):                                                 # the wire mesh in the glass
        acc.append(s.rcyl(0.0016, wz - 0.004, (-wx / 2 + k * wx / 6, -0.006, zw), seg=4, r=0))
    for k in range(1, 9):
        acc.append(s.rcyl(0.0016, wx - 0.004, (0, -0.006, zw - wz / 2 + k * wz / 9), axis="X", seg=4, r=0))
    for x in (-0.33, 0.33):                                               # push bar housings and the bar
        acc.append(s.rbox((0.09, 0.03, 0.11), (x, -0.054, 1.02), r=0.01, seg=2))
    acc.append(s.rbox((0.6, 0.02, 0.05), (0, -0.058, 1.02), r=0.008, seg=2))
    acc.append(s.rbox((0.84, 0.005, 0.27), (0, -0.0455, 0.145), r=0.002, seg=1))     # kick plate
    acc.append(s.rcyl(0.011, 0.42, (0.39, 0.06, 1.15), seg=14, r=0.004))            # pull handle on the back stile
    for z in (0.98, 1.32):
        acc.append(s.rcyl(0.009, 0.014, (0.39, 0.049, z), axis="Y", seg=10, r=0.002, bevel_seg=1))
    s.part("Accent", acc)

    s.part("Eye", [s.rbox((wx + 0.004, 0.01, wz + 0.004), (0, 0, zw), r=0.002, seg=1)])
    return export("exterior_door_panel")


# --------------------------------------------------------------------------- the hostage's cage (storybook style)

CAGE_H = 2.2          # top rail of the bars (the prefab's colliders); the dome rises over it
CAGE_HALF = 1.0       # HouseFeatures.CageSize / 2


def _bars(y_or_x, along, fixed, z0, z1, step=0.11, r=0.011):
    """Vertical round bars every step along one side, from z0 to z1 (along: 'x' bars spread on x at y=fixed, 'y' on y)."""
    out = []
    n = int((2 * (CAGE_HALF - 0.09)) / step)
    for k in range(n + 1):
        t = -(CAGE_HALF - 0.09) + k * (2 * (CAGE_HALF - 0.09)) / n
        loc = (t, fixed, (z0 + z1) / 2) if along == "x" else (fixed, t, (z0 + z1) / 2)
        out.append(s.rcyl(r, z1 - z0, loc, seg=8, r=0.002, bevel_seg=1))
    return out


def _band(along, fixed, z, h=0.045, t=0.016):
    """A flat brass band across one side at height z."""
    w = 2 * (CAGE_HALF - 0.05)
    size = (w, t, h) if along == "x" else (t, w, h)
    loc = (0, fixed, z) if along == "x" else (fixed, 0, z)
    return s.rbox(size, loc, r=0.004, seg=1)


def _scrolls(along, fixed, z, n=7, rr=0.045):
    """A row of little brass rings standing in the band zone (the ornament of a birdcage)."""
    out = []
    for k in range(n):
        t = -(CAGE_HALF - 0.2) + k * (2 * (CAGE_HALF - 0.2)) / (n - 1)
        rot = (math.pi / 2, 0, 0) if along == "x" else (math.pi / 2, 0, math.pi / 2)
        loc = (t, fixed, z) if along == "x" else (fixed, t, z)
        out.append(torus(rr, 0.007, loc, seg=16, seg_r=6, rot=rot))
    return out


def model_cage_storybook():
    """The hostage's cage in the storybook style (Blue Prince): a big ornate birdcage on a dark wooden plinth — turned
    brass corner columns with ball finials, rails top and bottom, round bars with two flat bands and a row of little
    rings between them, a dome of eight curved ribs and a ring up to a crown finial with a hanging ring; the front is the
    door (its own parts: bars, bands, hinges, a hasp and a big brass padlock with a red enamel keyhole plate); inside,
    the hostage sits against the back bars hugging the knees, wrists and chest tied with rope.
    Footprint 2 x 2 m (origin in the middle on the floor, front = Blender -Y), bars to 2.2 m, crown at 2.78 m.
    Body = wood, Accent = brass, Door* = the door, Hostage* = the person (Body clothes, Skin, Hair (+ shoes), Rope)."""
    h, half = CAGE_H, CAGE_HALF
    plinth = 0.12
    wood = [s.rbox((2 * half, 2 * half, plinth - 0.02), (0, 0, (plinth - 0.02) / 2), r=0.02, seg=2),
            s.rbox((2 * half - 0.08, 2 * half - 0.08, 0.03), (0, 0, plinth - 0.015), r=0.012, seg=2)]
    s.part("Body", wood)

    brass = []
    col = [(0.0, 0.0), (0.07, 0.0), (0.07, 0.04), (0.055, 0.06), (0.042, 0.09), (0.036, 0.3), (0.048, 0.33), (0.036, 0.36),
           (0.03, h - plinth - 0.36), (0.042, h - plinth - 0.33), (0.03, h - plinth - 0.3), (0.03, h - plinth - 0.06),
           (0.05, h - plinth - 0.03), (0.05, h - plinth), (0.0, h - plinth)]
    for x in (-1, 1):
        for y in (-1, 1):
            cx, cy = x * (half - 0.06), y * (half - 0.06)
            brass.append(s.lathe(col, seg=16, loc=(cx, cy, plinth)))
            brass.append(s.ball(0.055, (cx, cy, h + 0.07), seg=14))
            brass.append(s.rcyl(0.025, 0.06, (cx, cy, h + 0.02), seg=12, r=0.004))
    for z in (plinth + 0.04, h - 0.02):                               # rails round the bottom and the top
        for y in (-1, 1):
            brass.append(s.rbox((2 * half - 0.08, 0.05, 0.045), (0, y * (half - 0.06), z), r=0.008, seg=1))
        for x in (-1, 1):
            brass.append(s.rbox((0.05, 2 * half - 0.08, 0.045), (x * (half - 0.06), 0, z), r=0.008, seg=1))
    zb0, zb1 = plinth + 0.06, h - 0.04
    brass += _bars(None, "x", half - 0.06, zb0, zb1)                  # back
    brass += _bars(None, "y", -(half - 0.06), zb0, zb1)               # the two sides
    brass += _bars(None, "y", half - 0.06, zb0, zb1)
    for z in (0.75, 1.8):
        brass.append(_band("x", half - 0.06, z))
        brass.append(_band("y", -(half - 0.06), z))
        brass.append(_band("y", half - 0.06, z))
    brass += _scrolls("x", half - 0.06, 1.97)
    brass += _scrolls("y", -(half - 0.06), 1.97)
    brass += _scrolls("y", half - 0.06, 1.97)
    # the dome: eight ribs from the top rail curving up to the crown, a ring round them halfway up
    apex = h + 0.5
    ring = []
    for k in range(8):
        a = math.pi / 4 * k
        rr = (half - 0.06) / max(abs(math.cos(a)), abs(math.sin(a)))  # on the square rail
        pts = []
        for i in range(9):
            t = i / 8
            q = math.cos(t * math.pi / 2)
            pts.append(Vector((rr * q * math.cos(a), rr * q * math.sin(a), h + (apex - h) * math.sin(t * math.pi / 2))))
        brass.append(s.tube(pts, 0.016, res=2, smooth=True, segments_per=4))
        ring.append(pts[4])
    ring.append(ring[0])
    brass.append(s.tube(ring, 0.012, res=1, smooth=False))
    crown = [(0.0, 0.0), (0.09, 0.0), (0.1, 0.02), (0.05, 0.06), (0.035, 0.1), (0.06, 0.14), (0.035, 0.18), (0.0, 0.19)]
    brass.append(s.lathe(crown, seg=16, loc=(0, 0, apex - 0.03)))
    brass.append(torus(0.06, 0.01, (0, 0, apex + 0.22), seg=16, seg_r=6, rot=(math.pi / 2, 0, 0)))
    s.part("Accent", brass)

    # the door: the whole front between the columns
    fy = -(half - 0.06)
    door = _bars(None, "x", fy, zb0, zb1)
    for z in (0.75, 1.8):
        door.append(_band("x", fy, z))
    door += _scrolls("x", fy, 1.97)
    for z in (0.5, 1.6):                                              # hinge knuckles on the left column (Unity +X = Blender -X)
        door.append(s.rcyl(0.022, 0.12, (half - 0.1, fy - 0.02, z), seg=10, r=0.004))
    door.append(s.rbox((0.12, 0.03, 0.05), (-(half - 0.3), fy - 0.02, 1.12), r=0.006, seg=1))  # the hasp
    s.part("DoorAccent", door)
    lock = [s.rbox((0.17, 0.06, 0.19), (-(half - 0.3), fy - 0.06, 0.98), r=0.03, seg=3),
            torus(0.055, 0.013, (-(half - 0.3), fy - 0.06, 1.08), seg=18, seg_r=6, rot=(math.pi / 2, 0, 0))]
    s.part("DoorLock", lock)
    s.part("DoorEye", [s.rbox((0.07, 0.01, 0.09), (-(half - 0.3), fy - 0.095, 0.96), r=0.012, seg=2)])

    # the hostage: sitting on the plinth against the back bars, knees up, arms round them, facing the door
    base = plinth
    hip_y, back_y = 0.55, 0.68
    clothes = [
        s.rbox((0.36, 0.22, 0.5), (0, back_y - 0.06, base + 0.42), r=0.07, seg=3, taper=(0.85, 0.9)),         # torso
        s.rbox((0.34, 0.26, 0.16), (0, hip_y, base + 0.1), r=0.07, seg=3),                                     # hips
    ]
    for x in (-0.1, 0.1):
        clothes.append(s.tube([Vector((x, hip_y - 0.02, base + 0.1)), Vector((x, 0.25, base + 0.46))], 0.075, res=2, smooth=False))   # thighs up
        clothes.append(s.tube([Vector((x, 0.25, base + 0.46)), Vector((x, 0.16, base + 0.08))], 0.06, res=2, smooth=False))           # shins down
        clothes.append(s.tube([Vector((x * 2.0, back_y - 0.06, base + 0.6)), Vector((x * 2.1, 0.36, base + 0.42))], 0.048, res=2, smooth=False))  # upper arms
        clothes.append(s.tube([Vector((x * 2.1, 0.36, base + 0.42)), Vector((x * 0.5, 0.2, base + 0.45))], 0.042, res=2, smooth=False))  # forearms round the knees
    s.part("HostageBody", clothes)
    skin = [s.ball(0.1, (0, back_y - 0.1, base + 0.86), seg=16, scale=(0.95, 1.0, 1.1)),                        # head, a little bowed
            s.rcyl(0.045, 0.1, (0, back_y - 0.07, base + 0.72), seg=10, r=0.01),                                 # neck
            s.ball(0.042, (-0.05, 0.19, base + 0.45), seg=10), s.ball(0.042, (0.05, 0.19, base + 0.45), seg=10)]  # hands together
    s.part("HostageSkin", skin)
    hair = [s.ball(0.104, (0, back_y - 0.085, base + 0.9), seg=16, scale=(1.0, 1.05, 0.85))]
    for x in (-0.1, 0.1):
        hair.append(s.rbox((0.1, 0.22, 0.08), (x, 0.11, base + 0.04), r=0.03, seg=2))                           # shoes
    s.part("HostageHair", hair)
    rope = [torus(0.065, 0.014, (0, 0.19, base + 0.45), seg=14, seg_r=5, rot=(0, math.pi / 2, 0)),             # round the wrists
            s.place(torus(0.2, 0.016, (0, 0, 0), seg=20, seg_r=5), (0, back_y - 0.06, base + 0.5), scale=(1.0, 0.65, 1.0))]  # round the chest
    s.part("HostageRope", rope)
    return export("cage_storybook")


MODELS = {
    "item_key_model": model_item_key_model,
    "item_cagekey_model": model_item_cagekey_model,
    "vault_door": model_vault_door,
    "vault_frame": model_vault_frame,
    "deposit_wall": model_deposit_wall,
    "gold_pallet": model_gold_pallet,
    "money_cart": model_money_cart,
    "cash_shelf": model_cash_shelf,
    "padlock": model_padlock,
    "exterior_door_panel": model_exterior_door_panel,
    "cage_storybook": model_cage_storybook,
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
