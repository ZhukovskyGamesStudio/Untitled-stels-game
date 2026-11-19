"""Furniture for the technical rooms of an office (round 2): server room (network cabinet, UPS, crash cart, CRAC unit,
tape library), storage (boltless shelving, drums on a spill pallet, hand truck, crate stack, roll cage), copy room
(plotter, work table, supply shelf, mail cart, confidential bin), control room (city map board, radio rack, operator
chair, supervisor desk, alarm panel) and the hall (display case, big planter, bench, sculpture, directory board).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms2_tech.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y, z up, origin on the
floor in the middle of the footprint. Wall-hung pieces (map_board, alarm_panel) keep their real height and a back face
flush with the footprint's back. Object names are material roles: Body, Accent, Trim, Eye (glows). Pieces overlap by
volume (2-5 mm), never face to face. Forms come from shapes.py, finished by shapes.part; export() goes through meshfix
(floor=True) like poi.export.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --)
import meshfix  # noqa: E402
import shapes as s  # noqa: E402


def export(filename):
    """meshfix (furniture stands on the floor), then the FBX into <root>/Props/."""
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=filename)
    path = os.path.join(g.out_dir, "Props", filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def merge(bms):
    """One bmesh from several (a multi-box cutter for a single boolean)."""
    out = bmesh.new()
    for b in bms:
        me = bpy.data.meshes.new("_m")
        b.to_mesh(me)
        b.free()
        out.from_mesh(me)
        bpy.data.meshes.remove(me)
    return out


def frame(size, loc, border, r=0.006, seg=2, axis_open="Y"):
    """A rectangular frame (door, picture frame): a rounded box with a through-hole, border wide."""
    outer = s.rbox(size, loc, r=r, seg=seg)
    hole = list(size)
    for k, c in enumerate("XYZ"):
        hole[k] = size[k] + 0.1 if c == axis_open else size[k] - 2 * border
    bm = s.boolean(outer, s.rbox(tuple(hole), loc, r=0), "DIFFERENCE")
    s.bevel_edges(bm, min(r, border * 0.3), 1, angle=50)
    return bm


def vent(center, w, h, n, axis="-Y", proud=0.012, bar=0.012, fw=0.022):
    """A cheap louvred vent on a flat face whose outside points along axis: n plain slats in a chamfered frame."""
    parts = []
    gap = h / (n + 1)
    for k in range(n):
        z = -h / 2 + gap * (k + 1)
        parts.append(s.rbox((w - fw, proud, bar), (0, 0, z), r=0, rot=(math.radians(-30), 0, 0)))
    for x in (-w / 2 + fw / 2, w / 2 - fw / 2):
        parts.append(s.rbox((fw, proud * 1.4, h), (x, 0, 0), r=0.003, seg=1))
    for z in (-h / 2 + fw / 2, h / 2 - fw / 2):
        parts.append(s.rbox((w - fw, proud * 1.2, fw), (0, 0, z), r=0.003, seg=1))
    rot = {"-Y": (0, 0, 0), "Y": (0, 0, math.pi), "X": (0, 0, math.pi / 2), "-X": (0, 0, -math.pi / 2)}[axis]
    for p in parts:
        s.place(p, Vector(center), rot)
    return parts


def caster(x, y, r=0.035, w=0.025, top=0.1):
    """A swivel caster: a wheel touching the floor and a fork/stem up to `top` (sunk 4 mm into what it carries)."""
    wheel = s.rcyl(r, w, (x, y, r), axis="X", seg=12, r=min(0.006, r * 0.2), bevel_seg=1)
    z0 = r * 0.95
    fork = s.rbox((w + 0.012, r * 1.25, top + 0.004 - z0), (x, y + r * 0.2, (z0 + top + 0.004) / 2), r=0.004, seg=1)
    return [wheel, fork]


def carton(w, d, h, x, y, zb, rz=0.0, seam=True, r=0.006):
    """A cardboard box standing at zb (sunk 3 mm), with a tape seam across its top."""
    b = s.rbox((w, d, h), (0, 0, h / 2), r=r, seg=1)
    if seam:
        s.groove(b, "X", 0.0, 0.01, 0.002)
    return s.place(b, (x, y, zb - 0.003), (0, 0, rz))


def tray(w, d, h, zb, y=0.0, lip=0.012, floor=0.01):
    """An open tray (cart shelf): its floor top is returned with it."""
    outer = s.rbox((w, d, h), (0, y, zb + h / 2), r=0.006, seg=1)
    cut = s.rbox((w - 2 * lip, d - 2 * lip, h), (0, y, zb + floor + h / 2), r=0.005, seg=1, vertical_only=True)
    bm = s.boolean(outer, cut, "DIFFERENCE")
    s.bevel_edges(bm, 0.002, 1, angle=50)
    return bm, zb + floor


def leds(xs, y, z, size=(0.012, 0.008, 0.008)):
    """A row of tiny indicator lights (plain boxes)."""
    return [s.rbox(size, (x, y, z), r=0) for x in xs]


def bin_box(w, d, h, x, y, zb):
    """A plastic picking bin: tapered walls, hollow top."""
    b = s.rbox((w, d, h), (x, y, zb - 0.002 + h / 2), r=0.012, seg=1, taper=(1.03, 1.03))
    s.inset_faces(b, s.facing((0, 0, 1), min_area=0.01), thickness=0.012, depth=-(h - 0.035))
    return b


def ream(x, y, zb, rz=0.0, w=0.297, d=0.21, h=0.05):
    """A wrapped ream of paper (A4 lying flat), sunk 2 mm into what it lies on."""
    return s.rbox((w, d, h), (x, y, zb - 0.002 + h / 2), r=0.005, seg=1, rot=(0, 0, rz))


# --------------------------------------------------------------------------- server room

def model_network_cabinet():
    """Half-height 19" network cabinet 0.6 x 0.6 x 1.2: a black carcass on levelling feet, a glazed door with a bar
    handle, rails and a stack of patch panel, switches and servers inside, port lights blinking on the switches."""
    feet = [s.rcyl(0.022, 0.03, (x, y, 0.015), seg=10, r=0.004, bevel_seg=1) for x in (-0.24, 0.24) for y in (-0.22, 0.24)]
    carcass = s.rbox((0.6, 0.57, 1.17), (0, 0.015, 0.61), r=0.012, seg=2)  # z 0.025..1.195, y -0.27..0.30
    carcass = s.boolean(carcass, s.rbox((0.5, 0.5, 1.03), (0, -0.04, 0.61), r=0), "DIFFERENCE")
    s.bevel_edges(carcass, 0.004, 1, angle=50)
    door = frame((0.58, 0.02, 1.13), (0, -0.278, 0.61), 0.045, r=0.005, seg=1)
    roof = [s.rbox((0.012, 0.4, 0.008), (x, 0.03, 1.197), r=0) for x in (-0.15, -0.1, -0.05, 0.0, 0.05, 0.1, 0.15)]
    s.part("Body", [carcass, door, *roof])

    acc = list(feet)
    acc.append(s.rbox((0.024, 0.026, 0.18), (0.265, -0.298, 0.66), r=0.008, seg=2))       # handle on the door frame
    acc.append(s.rcyl(0.011, 0.012, (0.265, -0.292, 0.8), axis="Y", seg=10, r=0.003, bevel_seg=1))  # lock
    acc += [s.rbox((0.035, 0.02, 1.04), (sx * 0.235, -0.2, 0.61), r=0.003, seg=1) for sx in (-1, 1)]
    eye = []
    devices = [(1.07, 0.044, "patch"), (1.01, 0.044, "switch"), (0.95, 0.044, "switch"), (0.86, 0.088, "server"),
               (0.75, 0.088, "server"), (0.57, 0.13, "ups"), (0.3, 0.13, "server")]
    for zc, h, kind in devices:
        dev = s.rbox((0.51, 0.40, h - 0.004), (0, -0.02, zc), r=0.004, seg=1)
        if kind == "server":
            for x in (-0.12, -0.04, 0.04):
                s.groove(dev, "X", x, 0.006, 0.003)
        acc.append(dev)
        if kind == "switch":
            xs = [-0.2 + k * 0.026 for k in range(12)]
            eye += leds(xs, -0.222, zc + 0.009) + leds(xs, -0.222, zc - 0.009)
        elif kind == "patch":
            eye += leds([-0.2 + k * 0.034 for k in range(10)], -0.222, zc, (0.014, 0.008, 0.012))
        elif kind == "server":
            eye += leds([0.17, 0.2], -0.222, zc + h * 0.25)
        else:
            eye.append(s.rbox((0.07, 0.008, 0.03), (0.15, -0.222, zc + 0.02), r=0))
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((0.5, 0.006, 1.05), (0, -0.278, 0.61), r=0.002, seg=1)])  # the glass
    s.part("Eye", eye)
    return export("network_cabinet")


def model_ups_unit():
    """Tall UPS tower 0.5 x 0.8 x 1.0 on small casters: a dark case with a side seam, a black rounded fascia with a
    louvred intake, a status display, three buttons and a thin brand stripe."""
    body = s.rbox((0.48, 0.76, 0.95), (0, 0.02, 0.505), r=0.02, seg=2)  # z 0.03..0.98
    s.groove(body, "Z", 0.5, 0.008, 0.004)
    s.groove(body, "Y", 0.15, 0.008, 0.004)
    body_parts = [body, *vent((0, -0.396, 0.33), 0.36, 0.42, 9)]
    s.part("Body", body_parts)

    fascia = s.rbox((0.5, 0.06, 0.97), (0, -0.365, 0.505), r=0.03, seg=3)
    acc = [fascia]
    for x in (-0.18, 0.18):
        for y in (-0.28, 0.3):
            acc += caster(x, y, r=0.022, w=0.018, top=0.035)
    s.part("Accent", acc)

    trim = [s.rbox((0.19, 0.01, 0.11), (0, -0.394, 0.82), r=0.005, seg=1),
            s.rbox((0.3, 0.006, 0.012), (0, -0.396, 0.93), r=0.002, seg=1)]
    trim += [s.rcyl(0.012, 0.012, (x, -0.398, 0.72), axis="Y", seg=10, r=0.003, bevel_seg=1) for x in (-0.05, 0, 0.05)]
    s.part("Trim", trim)
    s.part("Eye", [s.rbox((0.15, 0.012, 0.07), (0, -0.396, 0.82), r=0.004, seg=1)])
    return export("ups_unit")


def model_crash_cart():
    """Server-room crash cart 0.6 x 0.5 x 1.1: three trays on square posts and casters, a drawer under the top tray,
    a push bar at the back, a small monitor with a keyboard and a mouse on top."""
    body = []
    for zb, h in ((0.1, 0.05), (0.48, 0.045), (0.8, 0.05)):
        t, _ = tray(0.6, 0.46, h, zb)
        body.append(t)
    top_floor = 0.81
    body += [s.rbox((0.03, 0.03, 0.82), (sx * 0.28, sy * 0.21, 0.51), r=0.004, seg=1) for sx in (-1, 1) for sy in (-1, 1)]
    body.append(s.rbox((0.54, 0.42, 0.1), (0, 0, 0.752), r=0.006, seg=1))
    body.append(s.rbox((0.5, 0.02, 0.08), (0, -0.215, 0.752), r=0.005, seg=1))
    s.part("Body", body)

    acc = [s.rbox((0.16, 0.02, 0.018), (0, -0.232, 0.752), r=0.006, seg=1)]
    acc.append(s.tube([(-0.28, 0.21, 0.86), (-0.28, 0.24, 0.92), (0.28, 0.24, 0.92), (0.28, 0.21, 0.86)], 0.012,
                      res=1, smooth=False))
    acc.append(s.rbox((0.2, 0.15, 0.014), (0, 0.1, top_floor + 0.005), r=0.004, seg=1))
    acc.append(s.rbox((0.04, 0.03, 0.13), (0, 0.12, 0.88), r=0.006, seg=1))
    acc.append(s.rbox((0.42, 0.04, 0.22), (0, 0.095, 0.99), r=0.01, seg=2))
    kb = s.rbox((0.38, 0.13, 0.02), (0, -0.12, top_floor + 0.008), r=0.005, seg=1)
    s.inset_faces(kb, s.facing((0, 0, 1), min_area=0.02), thickness=0.012, depth=-0.004)
    acc.append(kb)
    acc.append(s.rbox((0.04, 0.07, 0.022), (0.235, -0.11, top_floor + 0.009), r=0.009, seg=2))
    s.part("Accent", acc)

    trim = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            trim += caster(sx * 0.255, sy * 0.19, r=0.04, w=0.03, top=0.1)
    s.part("Trim", trim)
    s.part("Eye", [s.rbox((0.39, 0.008, 0.19), (0, 0.074, 0.99), r=0.003, seg=1)])
    return export("crash_cart")


def model_crac_unit():
    """Room air-conditioning cabinet 1.0 x 0.8 x 2.0: a light enclosure on a dark plinth, three door panels, each with
    a big louvred grille above and below a middle band; a controller display and latches in the band."""
    body = s.rbox((1.0, 0.78, 1.9), (0, 0.01, 1.045), r=0.016, seg=2)  # z 0.095..1.995, y -0.38..0.40
    for x in (-0.1665, 0.1665):
        s.groove(body, "X", x, 0.01, 0.005)
    s.groove(body, "Z", 1.05 - 0.13, 0.008, 0.004)
    s.groove(body, "Z", 1.05 + 0.13, 0.008, 0.004)
    s.part("Body", [body])

    acc = [s.rbox((0.96, 0.76, 0.1), (0, 0.01, 0.05), r=0.01, seg=1)]
    for x in (-0.333, 0.0, 0.333):
        acc += vent((x, -0.383, 1.56), 0.27, 0.68, 9)
        acc += vent((x, -0.383, 0.5), 0.27, 0.68, 9)
    for x in (-0.333, 0.0):
        acc.append(s.rbox((0.022, 0.024, 0.12), (x + 0.11, -0.388, 1.05), r=0.007, seg=1))
    acc.append(s.rbox((0.2, 0.012, 0.12), (0.333, -0.384, 1.05), r=0.004, seg=1))   # display bezel
    s.part("Accent", acc)
    s.part("Eye", [s.rbox((0.15, 0.01, 0.07), (0.333, -0.389, 1.05), r=0.003, seg=1)])
    return export("crac_unit")


def model_tape_library():
    """Tape library 1.2 x 0.8 x 1.9: a black two-bay cabinet on a plinth; the left bay has a tall window onto six
    shelves of cartridges, the right bay a status screen, a mail-slot station, a handle and a low vent."""
    body = s.rbox((1.2, 0.78, 1.82), (0, 0.01, 0.985), r=0.014, seg=2)  # z 0.075..1.895, y -0.38..0.40
    s.groove(body, "X", 0.0, 0.01, 0.005)
    body = s.boolean(body, s.rbox((0.4, 0.2, 1.0), (-0.3, -0.38, 1.0), r=0.02, seg=2, vertical_only=True), "DIFFERENCE")
    s.bevel_edges(body, 0.004, 1, angle=50)
    s.part("Body", [body, s.rbox((1.16, 0.76, 0.08), (0, 0.01, 0.04), r=0.01, seg=1)])

    acc = []
    for row in range(6):
        z = 0.55 + row * 0.16
        acc.append(s.rbox((0.41, 0.075, 0.012), (-0.3, -0.3125, z), r=0.002, seg=1))
        zt = z + 0.006
        for i in range(14):
            if (row * 7 + i * 3) % 11 == 0:
                continue
            acc.append(s.rbox((0.022, 0.07, 0.105), (-0.48 + i * 0.026 + 0.0, -0.312, zt + 0.0525 - 0.002), r=0))
    acc.append(s.rbox((0.3, 0.012, 0.12), (0.3, -0.384, 1.2), r=0.004, seg=1))
    acc.append(s.rbox((0.025, 0.03, 0.3), (0.1, -0.39, 1.0), r=0.008, seg=2))
    acc.append(s.rbox((0.025, 0.03, 0.3), (-0.06, -0.39, 1.0), r=0.008, seg=2))
    acc += vent((0.3, -0.383, 0.3), 0.4, 0.25, 5)
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((0.42, 0.006, 1.02), (-0.3, -0.36, 1.0), r=0.002, seg=1)])
    s.part("Eye", [s.rbox((0.18, 0.012, 0.11), (0.3, -0.384, 1.5), r=0.004, seg=1)])
    return export("tape_library")


# --------------------------------------------------------------------------- storage

def model_metal_shelving():
    """Boltless steel shelving 1.8 x 0.5 x 2.2: four angle posts, five decks on lipped beams, cartons and blue picking
    bins on the decks (the top deck nearly free)."""
    body = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            xo, yo = sx * 0.9, sy * 0.25
            body.append(s.rbox((0.035, 0.006, 2.2), (xo - sx * 0.0175, yo - sy * 0.003, 1.1), r=0.002, seg=1))
            body.append(s.rbox((0.006, 0.035, 2.196), (xo - sx * 0.003, yo - sy * 0.0175, 1.1), r=0.002, seg=1))
    levels = (0.12, 0.6, 1.08, 1.56, 2.06)
    for z in levels:
        body.append(s.rbox((1.79, 0.47, 0.018), (0, 0, z), r=0.003, seg=1))
        for sy in (-1, 1):
            body.append(s.rbox((1.796, 0.012, 0.045), (0, sy * 0.24, z - 0.01), r=0.003, seg=1))
    s.part("Body", body)

    tops = [z + 0.009 for z in levels]
    boxes = [carton(0.52, 0.4, 0.4, -0.58, 0.0, tops[0], 0.03), carton(0.44, 0.42, 0.34, -0.03, 0.01, tops[0], -0.04),
             carton(0.56, 0.42, 0.38, 0.53, -0.01, tops[0]),
             carton(0.6, 0.4, 0.32, 0.45, 0.0, tops[1], 0.02),
             carton(0.45, 0.38, 0.36, -0.6, 0.0, tops[2], -0.03), carton(0.36, 0.4, 0.25, -0.12, 0.0, tops[2]),
             carton(0.3, 0.32, 0.14, -0.1, 0.01, tops[2] + 0.25, 0.12), carton(0.55, 0.4, 0.38, 0.45, 0.0, tops[2], 0.02),
             carton(0.6, 0.4, 0.3, -0.5, 0.0, tops[3], -0.02),
             carton(0.4, 0.3, 0.18, -0.4, 0.0, tops[4], 0.05)]
    s.part("Trim", boxes)
    bins = [bin_box(0.3, 0.42, 0.2, x, 0.0, tops[1]) for x in (-0.72, -0.4, -0.08)]
    bins += [bin_box(0.28, 0.42, 0.18, x, 0.0, tops[3]) for x in (0.14, 0.44, 0.74)]
    s.part("Accent", bins)
    return export("metal_shelving")


def model_drum_pair():
    """Two 200 l oil drums on a spill pallet 0.9 x 0.6: the pallet sump has fork pockets and a grating, the drums
    rolling hoops, a rolled chime and two bungs each in the dished top."""
    tub = s.rbox((0.9, 0.6, 0.15), (0, 0, 0.075), r=0.015, seg=2)
    tub = s.boolean(tub, s.rbox((0.84, 0.54, 0.15), (0, 0, 0.18), r=0.01, seg=1, vertical_only=True), "DIFFERENCE")
    tub = s.boolean(tub, merge([s.rbox((0.2, 0.7, 0.05), (x, 0, 0.02), r=0) for x in (-0.22, 0.22)]), "DIFFERENCE")
    s.bevel_edges(tub, 0.004, 1, angle=50)
    acc = [tub]
    acc += [s.rbox((0.018, 0.55, 0.025), (-0.38 + k * 0.095, 0, 0.133), r=0) for k in range(9)]
    acc += [s.rbox((0.85, 0.018, 0.02), (0, y, 0.13), r=0) for y in (-0.15, 0.0, 0.15)]
    s.part("Accent", acc)

    prof = [(0.0, 0.142), (0.205, 0.142), (0.21, 0.15), (0.21, 0.37), (0.216, 0.38), (0.216, 0.395), (0.21, 0.405),
            (0.21, 0.62), (0.216, 0.63), (0.216, 0.645), (0.21, 0.655), (0.21, 0.865), (0.214, 0.87), (0.214, 0.89),
            (0.2, 0.89), (0.198, 0.875), (0.0, 0.875)]
    drums, bungs = [], []
    for x, turn in ((-0.222, 1), (0.222, -1)):
        drums.append(s.lathe(prof, seg=24, loc=(x, 0, 0)))
        bungs.append(s.rcyl(0.028, 0.016, (x + turn * 0.11, 0.06, 0.88), seg=12, r=0.004, bevel_seg=1))
        bungs.append(s.rcyl(0.018, 0.014, (x - turn * 0.1, -0.08, 0.879), seg=10, r=0.003, bevel_seg=1))
    s.part("Body", drums)
    s.part("Trim", bungs)
    return export("drum_pair")


def model_hand_truck():
    """Two-wheel hand truck standing upright (0.5 x 0.5 x 1.2): a bent tube frame with a loop handle and three cross
    bars, a toe plate, two pneumatic wheels behind on an axle, two cartons on the plate leaning on the frame."""
    pts = [(-0.16, 0.025, 0.03), (-0.16, 0.045, 0.4), (-0.16, 0.06, 1.08), (-0.14, 0.065, 1.17), (-0.1, 0.068, 1.19),
           (0.1, 0.068, 1.19), (0.14, 0.065, 1.17), (0.16, 0.06, 1.08), (0.16, 0.045, 0.4), (0.16, 0.025, 0.03)]
    body = [s.tube(pts, 0.014, res=1, smooth=False)]
    body += [s.rcyl(0.009, 0.32, (0, y, z), axis="X", seg=8, r=0.002, bevel_seg=1)
             for y, z in ((0.04, 0.3), (0.05, 0.65), (0.058, 1.0))]
    body.append(s.rbox((0.36, 0.24, 0.012), (0, -0.12, 0.006), r=0.004, seg=1))
    body.append(s.rbox((0.36, 0.025, 0.09), (0, 0.012, 0.049), r=0.004, seg=1))
    body += [s.rbox((0.012, 0.14, 0.05), (sx * 0.175, 0.085, 0.12), r=0.003, seg=1) for sx in (-1, 1)]
    body.append(s.rcyl(0.012, 0.46, (0, 0.135, 0.12), axis="X", seg=10, r=0.002, bevel_seg=1))
    body += [s.rcyl(0.05, 0.075, (sx * 0.205, 0.135, 0.12), axis="X", seg=14, r=0.008, bevel_seg=1) for sx in (-1, 1)]
    s.part("Body", body)
    s.part("Accent", [s.rcyl(0.12, 0.065, (sx * 0.205, 0.135, 0.12), axis="X", seg=20, r=0.02, bevel_seg=2)
                      for sx in (-1, 1)])
    s.part("Trim", [carton(0.4, 0.28, 0.44, 0, -0.105, 0.012),
                    carton(0.3, 0.22, 0.24, 0.01, -0.12, 0.449, rz=0.1)])
    return export("hand_truck")


def crate(w, d, h, x, y, zb, rz=0.0):
    """A wooden crate: a planked box (two seams) inside a frame of corner posts, rails and one diagonal brace per long
    face. Returns (planks, frame) placed at (x, y, zb) turned by rz."""
    t = 0.05
    body = s.rbox((w - 0.024, d - 0.024, h - 0.006), (0, 0, h / 2), r=0.004, seg=1)
    for k in (1, 2):
        s.groove(body, "Z", h * k / 3, 0.008, 0.004)
    bat = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            bat.append(s.rbox((t, t, h), (sx * (w / 2 - t / 2), sy * (d / 2 - t / 2), h / 2), r=0.005, seg=1))
    for z in (t / 2 + 0.006, h - t / 2 - 0.008):
        for sy in (-1, 1):
            bat.append(s.rbox((w - 2 * t + 0.02, 0.02, t), (0, sy * (d / 2 - 0.013), z), r=0.004, seg=1))
        for sx in (-1, 1) if z > h / 2 else ():
            bat.append(s.rbox((0.02, d - 2 * t + 0.02, t), (sx * (w / 2 - 0.013), 0, z), r=0.004, seg=1))
    lx, lz = w - 2 * t, h - 2 * t - 0.02
    ang = math.atan2(lz, lx)
    for sy in (-1, 1):
        bat.append(s.rbox((math.hypot(lx, lz), 0.016, t * 0.8), (0, sy * (d / 2 - 0.014), h / 2),
                          r=0.004, seg=1, rot=(0, -ang * sy, 0)))
    for b in [body, *bat]:
        s.place(b, (x, y, zb), (0, 0, rz))
    return [body], bat


def model_crate_stack():
    """Stack of wooden crates 1.2 x 0.8 x 1.4: two big crates side by side, a wide one across them turned a little,
    a small one on top."""
    planks, frames = [], []
    for args in ((0.58, 0.78, 0.5, -0.305, 0.0, 0.0), (0.58, 0.78, 0.5, 0.305, 0.0, 0.0),
                 (0.86, 0.6, 0.44, -0.04, 0.04, 0.49, 0.07), (0.48, 0.4, 0.46, 0.05, 0.0, 0.923, -0.15)):
        p, f = crate(*args)
        planks += p
        frames += f
    s.part("Body", planks)
    s.part("Accent", frames)
    return export("crate_stack")


def model_rolling_cage():
    """Roll cage 0.9 x 0.7 x 1.6: a deck on four casters, corner posts, welded wire mesh on the back and both sides
    (the front open), top and middle rails, parcels stacked inside."""
    acc = [s.rbox((0.88, 0.68, 0.05), (0, 0, 0.16), r=0.008, seg=1)]  # z 0.135..0.185
    for sx in (-1, 1):
        for sy in (-1, 1):
            acc += caster(sx * 0.36, sy * 0.26, r=0.06, w=0.035, top=0.135)
    s.part("Accent", acc)

    body = [s.rcyl(0.012, 1.43, (sx * 0.43, sy * 0.33, 0.885), seg=10, r=0.003, bevel_seg=1)
            for sx in (-1, 1) for sy in (-1, 1)]
    for z in (0.9, 1.585):
        body.append(s.rcyl(0.011, 0.86, (0, 0.33, z), axis="X", seg=8, r=0.002, bevel_seg=1))
        body += [s.rcyl(0.011, 0.66, (sx * 0.43, 0, z), axis="Y", seg=8, r=0.002, bevel_seg=1) for sx in (-1, 1)]
    wire_z = [0.2 + k * 0.155 for k in range(9)]
    for x in [-0.4 + k * 0.1 for k in range(9)]:
        body.append(s.rbox((0.006, 0.006, 1.42), (x, 0.33, 0.88), r=0))
    body += [s.rbox((0.86, 0.006, 0.006), (0, 0.334, z), r=0) for z in wire_z]
    for sx in (-1, 1):
        for y in [-0.275 + k * 0.11 for k in range(6)]:
            body.append(s.rbox((0.006, 0.006, 1.42), (sx * 0.43, y, 0.88), r=0))
        body += [s.rbox((0.006, 0.66, 0.006), (sx * 0.434, 0, z), r=0) for z in wire_z]
    s.part("Body", body)

    deck = 0.185
    s.part("Trim", [carton(0.42, 0.34, 0.32, -0.2, 0.12, deck, 0.05), carton(0.36, 0.3, 0.26, 0.22, 0.13, deck, -0.08),
                    carton(0.3, 0.26, 0.22, 0.2, -0.18, deck, 0.2), carton(0.34, 0.28, 0.24, -0.18, 0.1, deck + 0.317, -0.1),
                    carton(0.24, 0.2, 0.16, 0.22, 0.12, deck + 0.257, 0.15)])
    return export("rolling_cage")


# --------------------------------------------------------------------------- copy room

def model_plotter():
    """Wide-format plotter 1.5 x 0.6 x 1.1: a long printer body with dark end caps and a domed top cover on a stand of
    two legs with feet and a crossbar, a paper roll on arms behind, a sheet coming out of the front slot, a tilted
    control panel with a screen."""
    body = s.rbox((1.32, 0.44, 0.2), (0, 0.02, 0.9), r=0.02, seg=2)  # z 0.8..1.0, y -0.2..0.24
    body = s.boolean(body, s.rbox((1.12, 0.06, 0.014), (0, -0.2, 0.86), r=0), "DIFFERENCE")
    s.bevel_edges(body, 0.003, 1, angle=50)
    cover = s.rbox((1.26, 0.3, 0.1), (0, 0.05, 1.035), r=0.045, seg=3, taper=(1.0, 0.82))
    s.groove(cover, "Y", 0.0, 0.008, 0.004)
    s.part("Body", [body, cover])

    acc = []
    for sx in (-1, 1):
        acc.append(s.rbox((0.12, 0.46, 0.27), (sx * 0.69, 0.02, 0.925), r=0.03, seg=3))   # end caps x 0.63..0.75
        acc.append(s.rbox((0.07, 0.6, 0.04), (sx * 0.62, 0, 0.02), r=0.012, seg=2))          # foot
        acc.append(s.rbox((0.06, 0.06, 0.8), (sx * 0.62, 0.02, 0.42), r=0.01, seg=2))        # leg z 0.02..0.82
        acc.append(s.rbox((0.04, 0.2, 0.04), (sx * 0.6, 0.12, 0.7), r=0.008, seg=1))         # roll arm
    acc.append(s.rbox((1.2, 0.05, 0.06), (0, 0.02, 0.35), r=0.01, seg=2))
    acc.append(s.rcyl(0.012, 1.22, (0, 0.2, 0.7), axis="X", seg=10, r=0.002, bevel_seg=1))
    panel = s.rbox((0.2, 0.1, 0.04), (0, 0, 0), r=0.01, seg=2)
    acc.append(s.place(panel, (0.48, -0.17, 1.0), (math.radians(30), 0, 0)))
    s.part("Accent", acc)

    roll = s.rcyl(0.065, 1.08, (0, 0.2, 0.7), axis="X", seg=20, r=0.006, bevel_seg=1)
    sheet = s.rbox((1.0, 0.004, 0.24), (0, -0.21, 0.75), r=0.001, seg=1, rot=(math.radians(-8), 0, 0))
    s.part("Trim", [roll, sheet])
    screen = s.rbox((0.13, 0.065, 0.01), (0, -0.005, 0.02), r=0.002, seg=1)
    s.part("Eye", [s.place(screen, (0.48, -0.17, 1.0), (math.radians(30), 0, 0))])
    return export("plotter")


def model_work_table():
    """Copy-room work table 1.6 x 0.8, top at 0.9: square legs, an apron and a lower shelf; a comb-binding machine with
    its lever on the left, stacks of reams in the middle and on the right, two paper cases on the shelf."""
    body = [s.rbox((1.6, 0.8, 0.04), (0, 0, 0.88), r=0.01, seg=2),
            s.rbox((1.5, 0.7, 0.08), (0, 0, 0.825), r=0.006, seg=1),
            s.rbox((1.52, 0.68, 0.025), (0, 0, 0.18), r=0.005, seg=1)]
    body += [s.rbox((0.06, 0.06, 0.8), (sx * 0.74, sy * 0.34, 0.4), r=0.008, seg=2) for sx in (-1, 1) for sy in (-1, 1)]
    s.part("Body", body)

    top = 0.9
    base = s.rbox((0.42, 0.3, 0.08), (-0.45, 0.02, top + 0.037), r=0.012, seg=2)
    s.groove(base, "Z", top + 0.05, 0.008, 0.004)
    hood = s.rbox((0.4, 0.13, 0.07), (-0.45, 0.1, top + 0.1), r=0.02, seg=2, taper=(1.0, 0.6))
    hub = s.rcyl(0.032, 0.04, (-0.225, 0.06, top + 0.05), axis="X", seg=12, r=0.006, bevel_seg=1)
    lever = s.tube([(-0.215, 0.06, top + 0.05), (-0.21, 0.0, top + 0.17), (-0.21, -0.09, top + 0.25)], 0.011,
                   segments_per=6)
    knob = s.ball(0.024, (-0.21, -0.095, top + 0.255), seg=12)
    s.part("Accent", [base, hood, hub, lever, knob])

    paper = []
    z = top
    for k, rz in enumerate((0.0, 0.06, -0.04)):
        paper.append(ream(0.05, -0.05, z, rz))
        z += 0.048
    z = top
    for k, rz in enumerate((0.02, -0.05, 0.03, 0.08, -0.02)):
        paper.append(ream(0.42, 0.08, z, rz + 1.57))
        z += 0.048
    paper.append(ream(0.5, -0.24, top, 0.3, h=0.012))   # a loose pile of sheets
    for x in (-0.35, 0.25):
        c = s.rbox((0.45, 0.3, 0.27), (x, 0.05, 0.1925 - 0.003 + 0.135), r=0.006, seg=1)
        s.groove(c, "Z", 0.1925 + 0.22, 0.008, 0.003)
        paper.append(c)
    s.part("Trim", paper)
    return export("work_table")


def model_supply_shelf():
    """Open supply shelf 1.0 x 0.4 x 1.8: sides, a back, a kick board and four shelves; paper cases at the bottom,
    stacks of reams, a row of toner boxes, a mix of reams and toner on the top shelf."""
    body = [s.rbox((0.025, 0.4, 1.8), (sx * 0.4875, 0, 0.9), r=0.005, seg=1) for sx in (-1, 1)]
    body.append(s.rbox((0.96, 0.015, 1.76), (0, 0.19, 0.9), r=0.003, seg=1))
    body.append(s.rbox((0.96, 0.39, 0.025), (0, 0, 1.7825), r=0.005, seg=1))
    body.append(s.rbox((0.96, 0.02, 0.08), (0, -0.18, 0.04), r=0.004, seg=1))
    shelves = (0.1, 0.5, 0.9, 1.3)
    for z in shelves:
        body.append(s.rbox((0.96, 0.37, 0.022), (0, -0.005, z), r=0.004, seg=1))
    s.part("Body", body)
    tops = [z + 0.011 for z in shelves]

    paper = []
    for x in (-0.23, 0.23):
        c = s.rbox((0.43, 0.3, 0.27), (x, 0.0, tops[0] - 0.003 + 0.135), r=0.006, seg=1)
        s.groove(c, "Z", tops[0] + 0.22, 0.008, 0.003)
        paper.append(c)
    for x, n in ((-0.32, 4), (-0.06, 3), (0.2, 5)):
        z = tops[1]
        for k in range(n):
            paper.append(ream(x, 0.0, z, 1.5708 + 0.04 * ((k * 5) % 3 - 1), w=0.297, d=0.21))
            z += 0.048
    z = tops[3]
    for k in range(3):
        paper.append(ream(-0.28, 0.0, z, 1.5708 + 0.05 * (k % 2)))
        z += 0.048
    s.part("Trim", paper)

    toner = [s.rbox((0.11, 0.3, 0.2), (-0.38 + k * 0.125, 0.0, tops[2] - 0.002 + 0.1), r=0.005, seg=1) for k in range(6)]
    toner.append(s.rbox((0.4, 0.15, 0.13), (0.15, 0.0, tops[3] - 0.002 + 0.065), r=0.005, seg=1, rot=(0, 0, 0.05)))
    toner.append(s.rbox((0.11, 0.3, 0.2), (0.4, 0.0, tops[3] - 0.002 + 0.1), r=0.005, seg=1))
    s.part("Accent", toner)
    return export("supply_shelf")


def model_mail_cart():
    """Mail trolley 0.9 x 0.5 x 1.0: a wooden tub on a dark frame with casters, a rack of twelve pigeonholes along its
    back, a push handle on the right end, letters in some slots and bundles in the tub."""
    tub = s.rbox((0.86, 0.48, 0.42), (0, 0, 0.34), r=0.012, seg=2)  # z 0.13..0.55
    tub = s.boolean(tub, s.rbox((0.82, 0.44, 0.5), (0, 0, 0.41), r=0.008, seg=1, vertical_only=True),
                    "DIFFERENCE")
    s.bevel_edges(tub, 0.003, 1, angle=50)
    rack = s.rbox((0.86, 0.24, 0.42), (0, 0.12, 0.758), r=0.01, seg=2)  # y 0.0..0.24, z 0.548..0.968
    holes = []
    cw, ch = 0.19, 0.11
    for i in range(4):
        for j in range(3):
            holes.append(s.rbox((cw, 0.24, ch), (-0.3135 + i * 0.209, 0.09, 0.572 + j * 0.13 + ch / 2), r=0))
    rack = s.boolean(rack, merge(holes), "DIFFERENCE")
    s.bevel_edges(rack, 0.003, 1, angle=50)
    s.part("Body", [tub, rack])

    acc = [s.rbox((0.86, 0.48, 0.04), (0, 0, 0.115), r=0.006, seg=1)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            acc += caster(sx * 0.36, sy * 0.17, r=0.04, w=0.028, top=0.095)
    acc.append(s.tube([(0.428, -0.18, 0.5), (0.462, -0.18, 0.62), (0.462, -0.18, 0.85), (0.462, 0.18, 0.85),
                       (0.462, 0.18, 0.62), (0.428, 0.18, 0.5)], 0.012, res=1, smooth=False))
    s.part("Accent", acc)

    letters = []
    for i, j, n in ((0, 0, 3), (1, 1, 2), (2, 0, 4), (3, 2, 2), (0, 2, 1), (2, 1, 3)):
        x = -0.3135 + i * 0.209
        z = 0.572 + j * 0.13
        for k in range(n):
            letters.append(s.rbox((0.16, 0.2, 0.008), (x + 0.004 * (k % 2), 0.105, z + 0.003 + k * 0.009), r=0.001,
                                  seg=1, rot=(0, 0, 0.03 * (k - 1))))
    letters.append(s.rbox((0.3, 0.2, 0.1), (-0.2, -0.1, 0.16 - 0.003 + 0.05), r=0.01, seg=1, rot=(0, 0, 0.1)))
    letters.append(s.rbox((0.25, 0.16, 0.08), (0.18, -0.12, 0.16 - 0.003 + 0.04), r=0.01, seg=1, rot=(0, 0, -0.2)))
    s.part("Trim", letters)
    return export("mail_cart")


def model_paper_bin():
    """Confidential-paper console 0.6 x 0.4 x 0.7: a cabinet on a plinth with a front door panel, a label plate and a
    lock, a dark lid with a letter slot ringed by a metal rim."""
    body = s.rbox((0.58, 0.38, 0.6), (0, 0, 0.33), r=0.015, seg=2)  # z 0.03..0.63
    s.inset_faces(body, s.facing((0, -1, 0), min_area=0.1), thickness=0.035, depth=-0.006)
    s.part("Body", [body])

    lid = s.rbox((0.6, 0.4, 0.075), (0, 0, 0.6625), r=0.02, seg=2)  # z 0.625..0.7
    lid = s.boolean(lid, s.rbox((0.3, 0.03, 0.2), (0, -0.04, 0.7), r=0.008, seg=1, vertical_only=True), "DIFFERENCE")
    s.bevel_edges(lid, 0.003, 1, angle=50)
    throat = s.rbox((0.29, 0.025, 0.05), (0, -0.04, 0.64), r=0)
    plinth = s.rbox((0.54, 0.34, 0.04), (0, 0, 0.02), r=0.006, seg=1)
    s.part("Accent", [lid, throat, plinth])

    rim = frame((0.36, 0.084, 0.012), (0, -0.04, 0.702), 0.025, r=0.003, seg=1, axis_open="Z")
    lock = s.rcyl(0.018, 0.016, (0.2, -0.19, 0.55), axis="Y", seg=12, r=0.004, bevel_seg=1)
    plate = s.rbox((0.22, 0.012, 0.09), (0, -0.188, 0.48), r=0.003, seg=1)
    s.part("Trim", [rim, lock, plate])
    return export("paper_bin")


# --------------------------------------------------------------------------- control room

def model_map_board():
    """Wall-hung city map board 2.0 x 1.5 at 0.9..2.4: a dark frame, a map panel with raised streets, a diagonal avenue
    and a dark river, a legend card, glowing push pins."""
    frm = frame((2.0, 0.05, 1.5), (0, 0.025, 1.65), 0.05, r=0.008, seg=2)  # y 0..0.05, z 0.9..2.4
    river = []
    pts = [(-0.95, 1.2), (-0.55, 1.32), (-0.2, 1.22), (0.15, 1.45), (0.5, 1.5), (0.95, 1.78)]
    for k, ((x0, z0), (x1, z1)) in enumerate(zip(pts, pts[1:])):
        ln = math.hypot(x1 - x0, z1 - z0) + 0.06
        ang = math.atan2(z1 - z0, x1 - x0)
        river.append(s.rbox((ln, 0.008 + 0.001 * (k % 2), 0.07), ((x0 + x1) / 2, 0.013, (z0 + z1) / 2), r=0,
                            rot=(0, -ang, 0)))
    s.part("Accent", [frm, *river])
    s.part("Body", [s.rbox((1.94, 0.03, 1.44), (0, 0.03, 1.65), r=0.003, seg=1)])

    roads = [s.rbox((1.88, 0.006, 0.022 if k % 2 else 0.014), (0, 0.014, z), r=0)
             for k, z in enumerate((1.03, 1.15, 1.38, 1.6, 1.86, 2.04, 2.25))]
    roads += [s.rbox((0.022 if k % 3 == 0 else 0.014, 0.008, 1.38), (x, 0.0135, 1.65), r=0)
              for k, x in enumerate((-0.82, -0.6, -0.35, -0.12, 0.1, 0.36, 0.58, 0.8))]
    roads.append(s.rbox((1.55, 0.01, 0.03), (0.05, 0.0125, 1.62), r=0, rot=(0, -math.atan2(0.9, 1.5), 0)))
    roads.append(s.rbox((0.32, 0.006, 0.2), (0.74, 0.012, 1.07), r=0.002, seg=1))   # legend card
    s.part("Trim", roads)

    pins = []
    for x, z in ((-0.7, 1.95), (-0.46, 1.45), (-0.22, 2.12), (0.02, 1.7), (0.24, 1.25), (0.46, 1.98), (0.7, 1.58),
                 (-0.6, 1.1), (0.15, 2.18), (0.62, 2.2), (-0.05, 1.3), (0.35, 1.72)):
        pins.append(s.ball(0.017, (x, -0.024, z), seg=10))
        pins.append(s.rcyl(0.003, 0.03, (x, 0.005, z), axis="Y", seg=6, r=0))
    s.part("Eye", pins)
    return export("map_board")


def model_radio_rack():
    """Open radio rack 0.6 x 0.6 x 1.8: a black frame of four posts on a base with a cap, five radio sets with
    displays, knobs, speaker grilles and status lights, a shelf with a desk microphone."""
    body = [s.rbox((0.6, 0.6, 0.08), (0, 0, 0.04), r=0.01, seg=2), s.rbox((0.6, 0.6, 0.06), (0, 0, 1.77), r=0.01, seg=2)]
    body += [s.rbox((0.05, 0.05, 1.7), (sx * 0.27, sy * 0.27, 0.91), r=0.006, seg=1) for sx in (-1, 1) for sy in (-1, 1)]
    body.append(s.rbox((0.5, 0.4, 0.02), (0, -0.05, 0.84), r=0.004, seg=1))           # mic shelf
    s.part("Body", body)

    acc, trim, eye = [], [], []
    sets = [(1.56, 0.2, 2), (1.32, 0.18, 3), (1.08, 0.2, 1), (0.6, 0.26, 0), (0.3, 0.22, 2)]
    for zc, h, n_knobs in sets:
        acc.append(s.rbox((0.5, 0.36, h), (0, -0.1, zc), r=0.008, seg=1))           # front at y -0.28
        eye.append(s.rbox((0.14, 0.008, 0.05), (-0.12, -0.282, zc + h * 0.18), r=0))
        eye += leds([0.1, 0.13, 0.16], -0.282, zc + h * 0.3)
        for k in range(n_knobs):
            trim.append(s.rcyl(0.02, 0.025, (0.07 + k * 0.06, -0.289, zc - h * 0.12), axis="Y", seg=12, r=0.004,
                               bevel_seg=1))
        if n_knobs < 2:
            trim += [s.rbox((0.12, 0.008, 0.006), (0.12, -0.282, zc - h * 0.2 + k * 0.016), r=0) for k in range(4)]
    trim.append(s.rcyl(0.055, 0.02, (0.05, -0.1, 0.858), seg=16, r=0.006, bevel_seg=1))
    trim.append(s.tube([(0.05, -0.1, 0.865), (0.05, -0.1, 0.9), (0.03, -0.14, 0.93)], 0.007, res=1, segments_per=6))
    trim.append(s.rcyl(0.02, 0.07, (0.03, -0.16, 0.93), axis="Y", seg=12, r=0.006, bevel_seg=1,
                       rot=(math.radians(20), 0, 0)))
    s.part("Accent", acc)
    s.part("Trim", trim)
    s.part("Eye", eye)
    return export("radio_rack")


def model_operator_chair():
    """Big ergonomic operator chair 0.7 x 0.7 x 1.15: five-star base on casters, gas column, a deep cushioned seat,
    a tall lumbar backrest on a spine with a headrest, padded armrests."""
    seat = s.loft([(0.46, 0.25, 0.24, 3.5, 0, -0.02), (0.5, 0.265, 0.255, 3.5, 0, -0.02),
                   (0.545, 0.26, 0.25, 3.5, 0, -0.02), (0.565, 0.235, 0.225, 3.5, 0, -0.01)], seg=20, round_caps=0.012)
    back = s.loft([(0.6, 0.2, 0.05, 3, 0, 0.245), (0.68, 0.225, 0.065, 3, 0, 0.255), (0.82, 0.235, 0.06, 3, 0, 0.27),
                   (0.96, 0.22, 0.055, 3, 0, 0.29), (1.03, 0.19, 0.05, 3, 0, 0.3)], seg=20, round_caps=0.02)
    head = s.rbox((0.3, 0.08, 0.12), (0, 0.31, 1.09), r=0.035, seg=2)
    arm_pads = [s.rbox((0.08, 0.27, 0.035), (sx * 0.3, -0.01, 0.645), r=0.015, seg=2) for sx in (-1, 1)]
    s.part("Body", [seat, back, head, *arm_pads])

    acc = [s.rcyl(0.055, 0.06, (0, 0, 0.09), seg=12, r=0.01, bevel_seg=1)]
    for k in range(5):
        a = -math.pi / 2 + k * 2 * math.pi / 5
        arm = s.rbox((0.3, 0.05, 0.04), (0.16, 0, 0.085), r=0.012, seg=1, taper=(1.0, 0.8))
        acc.append(s.place(arm, (0, 0, 0), (0, 0, a)))
        cx, cy = 0.3 * math.cos(a), 0.3 * math.sin(a)
        acc.append(s.rcyl(0.032, 0.045, (cx, cy, 0.0305), axis="X", seg=10, r=0.008, bevel_seg=1, rot=(0, 0, a)))
        acc.append(s.rcyl(0.012, 0.05, (cx, cy, 0.075), seg=6, r=0.002, bevel_seg=1))
    acc.append(s.rcyl(0.036, 0.2, (0, 0, 0.2), seg=14, r=0.004, bevel_seg=1, radius2=0.03))
    acc.append(s.rbox((0.26, 0.26, 0.05), (0, 0.0, 0.44), r=0.01, seg=1))
    acc.append(s.tube([(0, 0.08, 0.44), (0, 0.24, 0.45), (0, 0.305, 0.56), (0, 0.31, 0.74)], 0.024, res=1,
                      segments_per=6))
    acc.append(s.rbox((0.05, 0.03, 0.14), (0, 0.32, 1.02), r=0.01, seg=1))
    for sx in (-1, 1):
        acc.append(s.rbox((0.04, 0.06, 0.22), (sx * 0.3, -0.01, 0.53), r=0.012, seg=1))
        acc.append(s.rbox((0.21, 0.06, 0.03), (sx * 0.205, -0.01, 0.43), r=0.008, seg=1))
    s.part("Accent", acc)
    s.part("Trim", [s.rcyl(0.025, 0.2, (0, 0, 0.36), seg=12, r=0.003, bevel_seg=1)])
    return export("operator_chair")


def model_supervisor_desk():
    """Raised supervisor desk 1.8 x 0.8, top at 1.1: two telescoping columns on long feet, a crossbeam, a modesty
    panel, two toed-in monitors on stands, a keyboard and a mouse."""
    body = [s.rbox((1.8, 0.8, 0.035), (0, 0, 1.0825), r=0.01, seg=2),
            s.rbox((1.4, 0.02, 0.35), (0, 0.3, 0.895), r=0.005, seg=1)]
    s.part("Body", body)

    acc = []
    for sx in (-1, 1):
        x = sx * 0.75
        acc.append(s.rbox((0.08, 0.72, 0.045), (x, 0, 0.0225), r=0.015, seg=2))
        acc.append(s.rbox((0.09, 0.07, 0.55), (x, 0.05, 0.315), r=0.01, seg=2))
        acc.append(s.rbox((0.075, 0.055, 0.5), (x, 0.05, 0.83), r=0.008, seg=2))
    acc.append(s.rbox((1.44, 0.06, 0.06), (0, 0.05, 1.035), r=0.008, seg=1))
    eye = []
    for sx in (-1, 1):
        x = sx * 0.3
        acc.append(s.rbox((0.22, 0.16, 0.012), (x, 0.22, 1.105), r=0.005, seg=1))
        acc.append(s.rbox((0.04, 0.025, 0.2), (x, 0.245, 1.2), r=0.008, seg=1))
        housing = s.rbox((0.55, 0.035, 0.33), (0, 0, 0), r=0.01, seg=2)
        acc.append(s.place(housing, (x, 0.22, 1.37), (0, 0, -sx * math.radians(8))))
        screen = s.rbox((0.52, 0.008, 0.3), (0, -0.018, 0.005), r=0.003, seg=1)
        eye.append(s.place(screen, (x, 0.22, 1.37), (0, 0, -sx * math.radians(8))))
    kb = s.rbox((0.44, 0.14, 0.022), (0, -0.18, 1.108), r=0.005, seg=1)
    s.inset_faces(kb, s.facing((0, 0, 1), min_area=0.03), thickness=0.012, depth=-0.004)
    acc.append(kb)
    acc.append(s.rbox((0.04, 0.07, 0.024), (0.32, -0.17, 1.109), r=0.01, seg=2))
    s.part("Accent", acc)
    s.part("Eye", eye)
    return export("supervisor_desk")


def model_alarm_panel():
    """Wall alarm panel 0.8 x 0.7 at 1.2..1.9: a cabinet with a recessed door, two rows of lamps with label strips,
    a status display, a row of toggle switches, a key switch and a red mushroom button."""
    cab = s.rbox((0.8, 0.1, 0.7), (0, 0.01, 1.55), r=0.012, seg=2)  # y -0.04..0.06
    s.inset_faces(cab, s.facing((0, -1, 0), min_area=0.2), thickness=0.03, depth=-0.005)
    s.part("Body", [cab])

    eye, trim = [], []
    for row, z in enumerate((1.78, 1.68)):
        for k in range(6):
            x = -0.32 + k * 0.09
            eye.append(s.rcyl(0.018, 0.02, (x, -0.044, z), axis="Y", seg=10, r=0.004, bevel_seg=1))
            trim.append(s.rbox((0.06, 0.006, 0.012), (x, -0.036, z - 0.04), r=0))
    eye.append(s.rbox((0.16, 0.008, 0.07), (0.25, -0.037, 1.78), r=0.002, seg=1))
    for k in range(6):
        x = -0.32 + k * 0.09
        trim.append(s.rbox((0.04, 0.008, 0.06), (x, -0.037, 1.45), r=0.003, seg=1))
        trim.append(s.rcyl(0.005, 0.04, (x, -0.055, 1.46), axis="Y", seg=8, r=0.001, bevel_seg=1,
                           rot=(math.radians(-30), 0, 0)))
    trim.append(s.rcyl(0.026, 0.014, (0.25, -0.039, 1.33), axis="Y", seg=14, r=0.003, bevel_seg=1))
    trim.append(s.rbox((0.008, 0.012, 0.03), (0.25, -0.05, 1.33), r=0.002, seg=1))
    s.part("Trim", trim)

    acc = [s.rcyl(0.04, 0.012, (0.25, -0.038, 1.6), axis="Y", seg=16, r=0.003, bevel_seg=1),
           s.rcyl(0.018, 0.02, (0.25, -0.05, 1.6), axis="Y", seg=12, r=0.003, bevel_seg=1),
           s.lathe([(0.0, 0.0), (0.04, 0.0), (0.043, 0.007), (0.038, 0.016), (0.0, 0.02)], seg=16, axis="-Y",
                   loc=(0.25, -0.058, 1.6))]
    s.part("Accent", acc)
    s.part("Eye", eye)
    return export("alarm_panel")


# --------------------------------------------------------------------------- hall

def model_display_case():
    """Trophy vitrine 1.0 x 0.5 x 1.8: a wooden plinth with a recessed toe and a panel, a metal-framed glass case on
    it with a glass shelf, cups and statuettes inside."""
    plinth = s.rbox((1.0, 0.5, 0.78), (0, 0, 0.465), r=0.012, seg=2)  # z 0.075..0.855
    s.inset_faces(plinth, s.facing((0, -1, 0), min_area=0.2), thickness=0.06, depth=-0.008)
    s.part("Body", [plinth])

    acc = [s.rbox((0.94, 0.44, 0.08), (0, 0, 0.04), r=0.006, seg=1),
           s.rbox((1.0, 0.5, 0.03), (0, 0, 0.865), r=0.006, seg=1),
           s.rbox((1.0, 0.5, 0.07), (0, 0, 1.765), r=0.012, seg=2)]
    acc += [s.rbox((0.03, 0.03, 0.88), (sx * 0.485, sy * 0.235, 1.30), r=0.004, seg=1) for sx in (-1, 1) for sy in (-1, 1)]
    s.part("Accent", acc)

    glass = [s.rbox((0.95, 0.006, 0.865), (0, sy * 0.235, 1.305), r=0.001, seg=1) for sy in (-1, 1)]
    glass += [s.rbox((0.006, 0.45, 0.865), (sx * 0.485, 0, 1.305), r=0.001, seg=1) for sx in (-1, 1)]
    glass.append(s.rbox((0.966, 0.466, 0.008), (0, 0, 1.3), r=0.001, seg=1))
    s.part("Eye", glass)

    cup = [(0.0, 0.0), (0.045, 0.0), (0.045, 0.025), (0.03, 0.03), (0.012, 0.04), (0.012, 0.09), (0.022, 0.1),
           (0.05, 0.14), (0.058, 0.2), (0.056, 0.215), (0.0, 0.2)]
    trophies = []
    for x, zb, k in ((-0.28, 0.879, 1.25), (0.05, 0.879, 1.5), (0.3, 0.879, 1.0), (-0.15, 1.303, 1.0)):
        trophies.append(s.lathe([(r * k, zb + z * k) for r, z in cup], seg=12))
        s.place(trophies[-1], (x, 0.02, 0))
        for sx in (-1, 1):
            trophies.append(s.tube([(x + sx * 0.05 * k, 0.02, zb + 0.19 * k), (x + sx * 0.085 * k, 0.02, zb + 0.17 * k),
                                    (x + sx * 0.05 * k, 0.02, zb + 0.13 * k)], 0.006 * k, res=1, segments_per=3))
    trophies.append(s.rbox((0.1, 0.1, 0.06), (0.22, 0.0, 1.303 + 0.028), r=0.006, seg=1))
    trophies.append(s.rcyl(0.012, 0.16, (0.22, 0.0, 1.303 + 0.13), seg=10, r=0.002, bevel_seg=1))
    trophies.append(s.ball(0.045, (0.22, 0.0, 1.303 + 0.24), seg=12))
    trophies.append(s.rbox((0.16, 0.02, 0.22), (-0.05, 0.15, 0.879 + 0.105), r=0.02, seg=2, rot=(math.radians(-12), 0, 0)))
    s.part("Trim", trophies)
    return export("display_case")


def model_planter_large():
    """Big indoor planter 1.0 x 1.0 x 1.8: a tapered pot with a rolled rim, soil, a slim crooked trunk branching into
    a canopy of ten lumpy leaf clusters."""
    pot = s.lathe([(0.0, 0.0), (0.26, 0.0), (0.275, 0.012), (0.33, 0.5), (0.345, 0.515), (0.345, 0.555), (0.33, 0.565),
                   (0.31, 0.55), (0.31, 0.5), (0.0, 0.5)], seg=24)
    s.part("Body", [pot])

    trim = [s.rcyl(0.305, 0.02, (0, 0, 0.505), seg=24, r=0.004, bevel_seg=1)]
    trim.append(s.tube([(0, 0, 0.48), (0.02, 0.01, 0.8), (-0.025, 0.0, 1.1), (0.0, 0.02, 1.38)], 0.04,
                       radii=[1.0, 0.85, 0.7, 0.55], res=1, segments_per=5))
    blobs = [((0, 0.02, 1.56), 0.26)]
    for k in range(6):
        ang = math.radians(60 * k)
        blobs.append(((0.29 * math.cos(ang), 0.29 * math.sin(ang), 1.3 + 0.03 * (k % 2)), 0.2 + 0.005 * (k % 2)))
    for k in range(3):
        ang = math.radians(30 + 120 * k)
        blobs.append(((0.17 * math.cos(ang), 0.17 * math.sin(ang), 1.52), 0.17))
    for (x, y, z), r in blobs[1:7]:
        trim.append(s.tube([(0, 0.01, 1.15), (x * 0.5, y * 0.5, (1.15 + z) / 2 + 0.03), (x * 0.8, y * 0.8, z - r * 0.3)],
                           0.014, res=1, segments_per=3))
    s.part("Trim", trim)

    # the canopy: the clusters melted into one lumpy leaf mass (balls side by side showed seams between them)
    leaves = [s.foliage([((x, y, z), r * 1.05) for (x, y, z), r in blobs], 0.035, 3, 0.012, 2400)]
    s.part("Accent", leaves, sharp_angle=90)
    return export("planter_large")


def model_hall_bench():
    """Long hall bench 1.8 x 0.5 x 0.45: five wooden slats on two square steel frames joined by a stretcher."""
    s.part("Body", [s.rbox((1.8, 0.085, 0.03), (0, y, 0.435), r=0.008, seg=2) for y in (-0.2, -0.1, 0.0, 0.1, 0.2)])
    acc = []
    for sx in (-1, 1):
        x = sx * 0.7
        acc += [s.rbox((0.04, 0.04, 0.415), (x, sy * 0.2, 0.2125), r=0.006, seg=1) for sy in (-1, 1)]
        acc.append(s.rbox((0.044, 0.46, 0.04), (x, 0, 0.02), r=0.006, seg=1))
        acc.append(s.rbox((0.045, 0.47, 0.04), (x, 0, 0.405), r=0.006, seg=1))
    acc.append(s.rbox((1.44, 0.05, 0.04), (0, 0, 0.397), r=0.006, seg=1))
    s.part("Accent", acc)
    return export("hall_bench")


def model_sculpture():
    """Abstract sculpture 0.6 x 0.6 x 1.7: a white plinth on a base plate with a brass plaque, a bronze ribbon that
    twists three quarters of a turn and leans, a polished ball balanced on its top."""
    s.part("Body", [s.rbox((0.6, 0.6, 0.04), (0, 0, 0.02), r=0.008, seg=2),
                    s.rbox((0.5, 0.5, 0.98), (0, 0, 0.52), r=0.01, seg=2)])
    rings = []
    for k in range(13):
        t = k / 12
        z = 1.0 + 0.6 * t
        a = 0.14 - 0.09 * math.sin(math.pi * t) + 0.01 * t
        rings.append((z, a, 0.035, 4, 0.06 * math.sin(math.pi * t * 1.2), 0.0))
    rib = s.loft(rings, seg=20, round_caps=0.0)
    for v in rib.verts:
        t = (v.co.z - 1.0) / 0.6
        ang = 1.5 * math.pi * t
        c, si = math.cos(ang), math.sin(ang)
        v.co.x, v.co.y = v.co.x * c - v.co.y * si, v.co.x * si + v.co.y * c
    s.bevel_edges(rib, 0.006, 1, angle=40)
    foot = s.rcyl(0.12, 0.02, (0, 0, 1.015), seg=24, r=0.005, bevel_seg=1)
    s.part("Accent", [rib, foot])
    s.part("Trim", [s.ball(0.08, (0.0, 0.0, 1.62), seg=20),
                    s.rbox((0.2, 0.008, 0.08), (0, -0.251, 0.75), r=0.002, seg=1)])
    return export("sculpture")


def model_directory_board():
    """Floor-standing directory 0.8 x 0.3 x 1.9: a weighted foot, two posts, a tall panel with a dark header and a
    logo bar, nine rows of floor numbers and name plates."""
    s.part("Body", [s.rbox((0.74, 0.05, 1.25), (0, 0, 1.025), r=0.01, seg=2)])  # z 0.4..1.65
    acc = [s.rbox((0.8, 0.3, 0.05), (0, 0, 0.025), r=0.015, seg=2),
           s.rbox((0.78, 0.07, 0.26), (0, 0, 1.77), r=0.014, seg=2)]
    acc += [s.rbox((0.05, 0.04, 0.41), (sx * 0.25, 0, 0.25), r=0.008, seg=1) for sx in (-1, 1)]
    trim = [s.rbox((0.4, 0.006, 0.08), (0, -0.036, 1.77), r=0.002, seg=1)]
    for k in range(9):
        z = 1.53 - k * 0.125
        acc.append(s.rbox((0.08, 0.006, 0.08), (-0.29, -0.026, z), r=0.002, seg=1))
        trim.append(s.rbox((0.52, 0.006, 0.07), (0.05, -0.026, z), r=0.002, seg=1))
    s.part("Accent", acc)
    s.part("Trim", trim)
    return export("directory_board")


MODELS = {
    "network_cabinet": model_network_cabinet,
    "ups_unit": model_ups_unit,
    "crash_cart": model_crash_cart,
    "crac_unit": model_crac_unit,
    "tape_library": model_tape_library,
    "metal_shelving": model_metal_shelving,
    "drum_pair": model_drum_pair,
    "hand_truck": model_hand_truck,
    "crate_stack": model_crate_stack,
    "rolling_cage": model_rolling_cage,
    "plotter": model_plotter,
    "work_table": model_work_table,
    "supply_shelf": model_supply_shelf,
    "mail_cart": model_mail_cart,
    "paper_bin": model_paper_bin,
    "map_board": model_map_board,
    "radio_rack": model_radio_rack,
    "operator_chair": model_operator_chair,
    "supervisor_desk": model_supervisor_desk,
    "alarm_panel": model_alarm_panel,
    "display_case": model_display_case,
    "planter_large": model_planter_large,
    "hall_bench": model_hall_bench,
    "sculpture": model_sculpture,
    "directory_board": model_directory_board,
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
