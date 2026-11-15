"""Furniture for the kitchen and the washrooms: kitchen sink counter, cooker, microwave, wall cupboards, urinal,
vanity with a mirror, hand dryer, locker-room bench and a shower cubicle.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms_wet.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--focus x,y,z,r] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y, z up, origin on the
floor in the middle of the footprint (the microwave: on its own bottom). Wall-hung pieces keep their real height and a
back face flush with the footprint's back. Object names are material roles: Body, Accent*, Trim*, Eye* (glows).
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
    """meshfix (furniture stands on the floor), then the FBX into <root>/Props/."""
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=filename)
    path = os.path.join(g.out_dir, "Props", filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def frame(size, loc, border, r=0.006, seg=2, axis_open="Y"):
    """A rectangular frame (mirror frame, window bezel): a rounded box with a through-hole, border wide."""
    outer = s.rbox(size, loc, r=r, seg=seg)
    hole = list(size)
    for k, c in enumerate("XYZ"):
        hole[k] = size[k] + 0.1 if c == axis_open else size[k] - 2 * border
    cut = s.rbox(tuple(hole), loc, r=0)
    bm = s.boolean(outer, cut, "DIFFERENCE")
    s.bevel_edges(bm, min(r, border * 0.3), 1, angle=50)
    return bm


def shaker(bm, min_area, thickness=0.04, depth=-0.006):
    """Recessed panel on the front face of a door (a shaker door: frame and a sunk panel)."""
    return s.inset_faces(bm, s.facing((0, -1, 0), min_area=min_area, tol=0.95), thickness=thickness, depth=depth)


def tile_grooves(bm, axis, values, width=0.006, depth=0.003):
    """Tile joints: a V-groove at each value along axis (three cuts, the middle ring pushed in) — lighter than
    shapes.groove's four cuts, enough for grout lines."""
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


def bar_handle(x, z, y_face, length, axis="Z", r=0.009, stand=0.022):
    """A D-shaped bar pull: a round bar on two stand-offs, its back sunk 3 mm into the face at y_face (front = -y)."""
    y_bar = y_face - stand
    out = []
    if axis == "Z":
        out.append(s.rcyl(r, length, (x, y_bar, z), axis="Z", seg=10, r=r * 0.4, bevel_seg=1))
        for dz in (-1, 1):
            out.append(s.rcyl(r * 0.75, stand + 0.003, (x, y_face - stand / 2 + 0.0015, z + dz * (length / 2 - 0.02)),
                              axis="Y", seg=8, r=0.002, bevel_seg=1))
    else:
        out.append(s.rcyl(r, length, (x, y_bar, z), axis="X", seg=10, r=r * 0.4, bevel_seg=1))
        for dx in (-1, 1):
            out.append(s.rcyl(r * 0.75, stand + 0.003, (x + dx * (length / 2 - 0.02), y_face - stand / 2 + 0.0015, z),
                              axis="Y", seg=8, r=0.002, bevel_seg=1))
    return out


# --------------------------------------------------------------------------- kitchen

def model_kitchen_sink():
    """Kitchen counter module 1.2 m: plinth, carcass, two shaker doors, a wooden worktop at 0.92 with an inset steel
    basin (left of centre, a drainer zone on the right) and a gooseneck tap at the back."""
    bx, by = -0.14, -0.02  # basin centre
    plinth = s.rbox((1.14, 0.5, 0.1), (0, 0.04, 0.05), r=0.01, seg=1)
    carcass = s.rbox((1.2, 0.56, 0.81), (0, 0.03, 0.485), r=0.012, seg=2)
    carcass = s.boolean(carcass, s.rbox((0.51, 0.39, 0.3), (bx, by, 0.88), r=0), "DIFFERENCE")  # room for the tub
    doors = []
    for x in (-0.2975, 0.2975):
        d = s.rbox((0.586, 0.022, 0.73), (x, -0.257, 0.485), r=0.008, seg=2)
        shaker(d, 0.2, thickness=0.05)
        doors.append(d)
    s.part("Body", [plinth, carcass, *doors])

    top = s.rbox((1.2, 0.62, 0.042), (0, 0, 0.899), r=0.01, seg=2)
    top = s.boolean(top, s.rbox((0.52, 0.4, 0.2), (bx, by, 0.9), r=0.045, seg=3, vertical_only=True), "DIFFERENCE")
    s.part("Trim", [top])

    # basin: a flange on the worktop, a tub under it, the bowl cut out of both
    tub = s.rbox((0.56, 0.44, 0.012), (bx, by, 0.924), r=0.005, seg=1)
    tub = s.boolean(tub, s.rbox((0.54, 0.42, 0.21), (bx, by, 0.815), r=0.05, seg=2, vertical_only=True), "UNION")
    tub = s.boolean(tub, s.rbox((0.49, 0.37, 0.3), (bx, by, 0.89), r=0.04, seg=3), "DIFFERENCE")
    s.bevel_edges(tub, 0.003, 1, angle=50)
    drain = s.rcyl(0.04, 0.008, (bx, by, 0.742), seg=20, r=0.002)
    # gooseneck tap behind the basin, lever on its right
    tx, ty = bx, 0.25
    base = s.rcyl(0.03, 0.05, (tx, ty, 0.94), seg=20, r=0.006)
    neck = s.tube([(tx, ty, 0.95), (tx, ty, 1.12), (tx, ty - 0.05, 1.2), (tx, ty - 0.13, 1.19), (tx, ty - 0.17, 1.12)],
                  0.012, segments_per=8)
    nozzle = s.rcyl(0.015, 0.03, (tx, ty - 0.17, 1.115), seg=12, r=0.003)
    lever = s.rbox((0.012, 0.09, 0.016), (tx + 0.035, ty - 0.02, 0.968), r=0.005, seg=1, rot=(math.radians(-12), 0, 0))
    hub = s.rcyl(0.014, 0.03, (tx + 0.025, ty, 0.965), axis="X", seg=10, r=0.003)
    handles = []
    for x in (-0.06, 0.06):
        handles += bar_handle(x, 0.72, -0.268, 0.16)
    s.part("Accent", [tub, drain, base, neck, nozzle, lever, hub, *handles])
    return export("kitchen_sink")


def model_stove():
    """Free-standing cooker 0.6 m: white enamel body on a recessed plinth, a hob with four burners and pan supports, a
    control strip with four knobs and a clock across the front top edge, an oven door with a window and a bar handle,
    a storage drawer below."""
    plinth = s.rbox((0.56, 0.52, 0.07), (0, 0.04, 0.035), r=0.008, seg=1)
    body = s.rbox((0.6, 0.58, 0.85), (0, 0.02, 0.475), r=0.014, seg=2)
    hob = s.rbox((0.6, 0.6, 0.028), (0, 0.008, 0.906), r=0.01, seg=2)
    strip = s.rbox((0.594, 0.03, 0.1), (0, -0.276, 0.835), r=0.01, seg=2)
    door = s.rbox((0.57, 0.024, 0.56), (0, -0.276, 0.47), r=0.012, seg=2)
    door = s.boolean(door, s.rbox((0.42, 0.02, 0.26), (0, -0.291, 0.5), r=0.02, seg=2, vertical_only=True),
                     "DIFFERENCE")
    s.bevel_edges(door, 0.003, 1, angle=50)
    drawer = s.rbox((0.57, 0.024, 0.13), (0, -0.276, 0.12), r=0.01, seg=2)
    s.groove(body, "Z", 0.765, 0.008, 0.004)
    s.part("Body", [plinth, body, hob, strip, door, drawer])

    acc = []
    glass_frame = frame((0.44, 0.008, 0.28), (0, -0.289, 0.5), 0.025, r=0.004, seg=1)
    acc.append(glass_frame)
    acc += bar_handle(0, 0.715, -0.288, 0.48, axis="X", r=0.011)
    acc += bar_handle(0, 0.16, -0.288, 0.2, axis="X", r=0.008, stand=0.016)
    for x in (-0.235, -0.135, 0.135, 0.235):
        knob = s.rcyl(0.021, 0.026, (x, -0.297, 0.835), axis="Y", seg=12, r=0.005, bevel_seg=1)
        mark = s.rbox((0.006, 0.01, 0.026), (x, -0.31, 0.835), r=0.002, seg=1)
        acc += [knob, mark]
    for x, y in ((-0.145, -0.135), (0.145, -0.135), (-0.145, 0.15), (0.145, 0.15)):
        big = 0.06 if y < 0 else 0.075
        acc.append(s.lathe([(big, 0.917), (big, 0.926), (big * 0.82, 0.93), (big * 0.6, 0.932), (big * 0.6, 0.938),
                            (0.0, 0.94)], seg=16, loc=(x, y, 0)))
        for rot in (math.pi / 4, -math.pi / 4):
            acc.append(s.rbox((2 * big + 0.05, 0.012, 0.012), (x, y, 0.94), r=0.003, seg=1, rot=(0, 0, rot)))
    s.part("Accent", acc)

    clock = s.rbox((0.09, 0.01, 0.036), (0, -0.292, 0.835), r=0.003, seg=1)
    s.part("Eye", [clock])
    return export("stove")


def model_microwave():
    """Countertop microwave 0.5 x 0.36 x 0.3, pivot on its own bottom: a rounded case on four feet, a door with a dark
    window and a bar handle on the left, a control panel with a display, buttons and a dial on the right."""
    feet = [s.rcyl(0.018, 0.02, (x, y, 0.01), seg=10, r=0.003) for x in (-0.2, 0.2) for y in (-0.11, 0.13)]
    case = s.rbox((0.5, 0.33, 0.285), (0, 0.015, 0.1575), r=0.022, seg=3)
    s.groove(case, "Y", 0.13, 0.008, 0.004)
    door = s.rbox((0.335, 0.018, 0.262), (-0.077, -0.157, 0.1575), r=0.01, seg=2)
    door = s.boolean(door, s.rbox((0.24, 0.02, 0.165), (-0.085, -0.168, 0.16), r=0.012, seg=2, vertical_only=True),
                     "DIFFERENCE")
    s.bevel_edges(door, 0.002, 1, angle=50)
    panel = s.rbox((0.13, 0.012, 0.262), (0.172, -0.152, 0.1575), r=0.008, seg=2)
    buttons = [s.rbox((0.034, 0.008, 0.02), (0.172 + dx * 0.04, -0.159, z), r=0.003, seg=1)
               for dx in (-0.5, 0.5) for z in (0.205, 0.175, 0.145)]
    vents = [s.rbox((0.006, 0.2, 0.012), (0.2505, 0.03, z), r=0.002, seg=1) for z in (0.17, 0.19, 0.21, 0.23)]
    s.part("Body", [*feet, case, door, panel, *buttons, *vents])

    bezel = frame((0.25, 0.008, 0.175), (-0.085, -0.164, 0.16), 0.018, r=0.003, seg=1)
    handle = s.rbox((0.022, 0.024, 0.22), (0.061, -0.175, 0.1575), r=0.008, seg=2)
    dial = s.rcyl(0.026, 0.02, (0.172, -0.163, 0.075), axis="Y", seg=18, r=0.004)
    s.part("Accent", [bezel, handle, dial])

    display = s.rbox((0.09, 0.008, 0.034), (0.172, -0.159, 0.25), r=0.003, seg=1)
    s.part("Eye", [display])
    return export("microwave")


def model_wall_cabinet():
    """Upper kitchen cupboards 1.2 m hung at 1.45..2.15, depth 0.35 with the back on the wall: a carcass with a slim
    cornice, two shaker doors and bar pulls at their lower inner corners."""
    carcass = s.rbox((1.2, 0.316, 0.7), (0, 0.017, 1.8), r=0.01, seg=2)
    cornice = s.rbox((1.2, 0.03, 0.04), (0, -0.152, 2.128), r=0.008, seg=2)
    doors = []
    for x in (-0.2975, 0.2975):
        d = s.rbox((0.586, 0.022, 0.64), (x, -0.148, 1.785), r=0.008, seg=2)
        shaker(d, 0.15, thickness=0.05)
        doors.append(d)
    s.part("Body", [carcass, cornice, *doors])
    handles = []
    for x in (-0.06, 0.06):
        handles += bar_handle(x, 1.56, -0.158, 0.13, r=0.008, stand=0.016)
    s.part("Accent", handles)
    return export("wall_cabinet")


# --------------------------------------------------------------------------- washrooms

def model_urinal():
    """Wall-hung porcelain urinal: a lofted bowl flat against the wall (0.45..1.05) with its front scooped open, a chrome
    flush valve on a pipe up to 1.35 with a wall flange, a lever, and the trap outlet under the bowl."""
    rings = [(0.45, 0.12, 0.11, 2.4, 0, 0.055), (0.52, 0.16, 0.16, 2.4, 0, 0.022), (0.6, 0.175, 0.18, 2.4, 0, 0.005),
             (0.8, 0.185, 0.185, 2.4, 0, 0.01), (0.98, 0.18, 0.175, 2.4, 0, 0.015), (1.05, 0.17, 0.17, 2.4, 0, 0.03)]
    bowl = s.loft(rings, seg=28, round_caps=0.03)
    bowl = s.boolean(bowl, s.rbox((0.5, 0.4, 0.8), (0, -0.025, 0.75), r=0), "INTERSECT")  # flat back at y = 0.175
    cavity = s.ball(1.0, (0, -0.09, 0.9), seg=24, scale=(0.14, 0.18, 0.34))
    bowl = s.boolean(bowl, cavity, "DIFFERENCE")
    s.bevel_edges(bowl, 0.006, 2, angle=40)
    s.part("Body", [bowl])

    py = 0.12
    spud = s.rcyl(0.022, 0.03, (0, py, 1.05), seg=12, r=0.004, bevel_seg=1)
    pipe = s.rcyl(0.013, 0.2, (0, py, 1.15), seg=10, r=0.003, bevel_seg=1)
    valve = s.lathe([(0.0, 1.245), (0.03, 1.248), (0.034, 1.26), (0.034, 1.31), (0.03, 1.325), (0.0, 1.33)],
                    seg=16, loc=(0, py, 0))
    cap = s.rcyl(0.022, 0.03, (0, py, 1.34), seg=12, r=0.006, bevel_seg=1)
    supply = s.rcyl(0.012, 0.06, (0, 0.14, 1.29), axis="Y", seg=10, r=0.002, bevel_seg=1)
    flange = s.rcyl(0.032, 0.012, (0, 0.169, 1.29), axis="Y", seg=14, r=0.003, bevel_seg=1)
    lever = s.rbox((0.1, 0.014, 0.014), (0.06, py - 0.005, 1.27), r=0.005, seg=1, rot=(0, math.radians(-12), 0))
    knob = s.ball(0.013, (0.11, py - 0.005, 1.259), seg=10)
    trap = s.rcyl(0.025, 0.06, (0, 0.135, 0.47), axis="Y", seg=12, r=0.004, bevel_seg=1)
    outlet = s.rcyl(0.04, 0.012, (0, 0.169, 0.47), axis="Y", seg=14, r=0.003, bevel_seg=1)
    s.part("Accent", [spud, pipe, valve, cap, supply, flange, lever, knob, trap, outlet])
    return export("urinal")


def model_mirror_sink():
    """Single washbasin vanity: a porcelain cabinet 0.85 high with an oval basin sunk into its top, two doors with
    knobs, a tiled upstand, a chrome tap, and a framed mirror flat on the wall above (1.1..1.9)."""
    plinth = s.rbox((0.66, 0.42, 0.09), (0, 0.03, 0.045), r=0.008, seg=1)
    cab = s.rbox((0.7, 0.48, 0.77), (0, 0.01, 0.465), r=0.012, seg=2)
    s.groove(cab, "Z", 0.79, 0.008, 0.004)
    cab = s.boolean(cab, s.ball(1.0, (0, -0.035, 0.875), seg=28, scale=(0.24, 0.16, 0.135)), "DIFFERENCE")
    s.bevel_edges(cab, 0.004, 1, angle=50)
    doors = []
    for x in (-0.1725, 0.1725):
        d = s.rbox((0.337, 0.02, 0.6), (x, -0.237, 0.445), r=0.008, seg=2)
        shaker(d, 0.1, thickness=0.04)
        doors.append(d)
    upstand = s.rbox((0.7, 0.02, 0.3), (0, 0.238, 0.97), r=0.006, seg=1)
    s.part("Body", [plinth, cab, *doors, upstand])

    acc = [s.rcyl(0.025, 0.008, (0, -0.035, 0.742), seg=16, r=0.002)]
    acc.append(s.rcyl(0.024, 0.06, (0, 0.17, 0.875), seg=18, r=0.005))
    acc.append(s.tube([(0, 0.17, 0.89), (0, 0.17, 0.96), (0, 0.13, 0.985), (0, 0.06, 0.975), (0, 0.045, 0.955)],
                      0.011, segments_per=7))
    acc.append(s.rcyl(0.024, 0.035, (0, 0.17, 0.92), axis="Z", seg=16, r=0.004))
    acc.append(s.rbox((0.014, 0.08, 0.012), (0, 0.15, 0.944), r=0.005, seg=1, rot=(math.radians(-15), 0, 0)))
    for x in (-0.05, 0.05):
        acc.append(s.ball(0.016, (x, -0.25, 0.68), seg=12))
        acc.append(s.rcyl(0.008, 0.02, (x, -0.246, 0.68), axis="Y", seg=8, r=0.002))
    acc.append(frame((0.6, 0.026, 0.8), (0, 0.237, 1.5), 0.035, r=0.008, seg=2))
    s.part("Accent", acc)

    glass = s.rbox((0.545, 0.008, 0.745), (0, 0.236, 1.5), r=0.003, seg=1)
    s.part("Eye", [glass])
    return export("mirror_sink")


def model_hand_dryer():
    """Wall-mounted hand dryer 0.3 x 0.2, hung at 1.1..1.4: a back plate on the wall, a rounded shell with a seam and a
    slanted belly, a chrome nozzle pointing down, a status LED on the front."""
    plate = s.rbox((0.3, 0.024, 0.3), (0, 0.088, 1.25), r=0.012, seg=2)
    shell = s.loft([(1.14, 0.12, 0.062, 3, 0, 0.03), (1.18, 0.138, 0.085, 3, 0, 0.01), (1.3, 0.142, 0.09, 3, 0, 0.002),
                    (1.38, 0.135, 0.085, 3, 0, 0.008)], seg=28, round_caps=0.025)
    s.groove(shell, "Z", 1.215, 0.008, 0.004)
    s.part("Body", [plate, shell])
    nozzle = s.lathe([(0.0, 1.17), (0.034, 1.17), (0.036, 1.14), (0.03, 1.105), (0.022, 1.1), (0.0, 1.1)], seg=20,
                     loc=(0, -0.025, 0))
    tip = s.rcyl(0.025, 0.012, (0, -0.025, 1.1), seg=18, r=0.003)
    s.part("Accent", [nozzle, tip])
    led = s.rcyl(0.009, 0.012, (0, -0.088, 1.33), axis="Y", seg=12, r=0.002)
    s.part("Eye", [led])
    return export("hand_dryer")


def model_bench():
    """Locker-room bench 1.6 m: four wooden slats on two steel trestles (posts, a top rail, a foot), a stretcher low
    between the feet."""
    slats = [s.rbox((1.6, 0.086, 0.032), (0, y, 0.434), r=0.008, seg=2) for y in (-0.157, -0.0525, 0.0525, 0.157)]
    s.part("Body", slats)
    steel = []
    for x in (-0.6, 0.6):
        steel.append(s.rbox((0.045, 0.37, 0.035), (x, 0, 0.405), r=0.008, seg=2))
        steel += [s.rbox((0.038, 0.04, 0.39), (x, y, 0.21), r=0.008, seg=2) for y in (-0.15, 0.15)]
        steel.append(s.rbox((0.055, 0.4, 0.03), (x, 0, 0.015), r=0.008, seg=2))
    steel.append(s.rbox((1.2, 0.04, 0.03), (0, 0, 0.028), r=0.008, seg=2))
    s.part("Accent", steel)
    return export("bench")


def model_shower_stall():
    """Shower cubicle 0.96 x 0.96 x 2.1: a tray with a sunk floor, tiled back and side walls (grout grid), a chrome
    riser with a rain head and a mixer, a fixed glass panel on the right and a glass door on the left, swung 40° in."""
    tray = s.rbox((0.96, 0.96, 0.08), (0, 0, 0.04), r=0.012, seg=2)
    s.inset_faces(tray, s.facing((0, 0, 1), min_area=0.3, tol=0.95), thickness=0.05, depth=-0.012)
    zs = [0.5 + 0.4 * k for k in range(4)]  # 0.28 x 0.4 m tiles
    back = s.rbox((0.846, 0.057, 2.077), (0, 0.4485, 1.0585), r=0.008, seg=1)
    tile_grooves(back, "X", (-0.14, 0.14))
    tile_grooves(back, "Z", zs)
    sides = []
    for x in (-0.45, 0.45):
        w = s.rbox((0.06, 0.95, 2.08), (x, 0.005, 1.06), r=0.008, seg=1)
        tile_grooves(w, "Y", (-0.18, 0.12))
        tile_grooves(w, "Z", zs)
        sides.append(w)
    s.part("Body", [tray, back, *sides])

    acc = []
    acc.append(s.rbox((0.12, 0.12, 0.012), (0, 0.05, 0.07), r=0.004, seg=1))
    acc += [s.rbox((0.09, 0.008, 0.006), (0, 0.05 + dy, 0.0745), r=0.002, seg=1) for dy in (-0.03, 0, 0.03)]
    rx, ry = 0.0, 0.41
    acc.append(s.rcyl(0.013, 1.05, (rx, ry, 1.42), seg=10, r=0.003, bevel_seg=1))
    for z in (0.92, 1.92):
        acc.append(s.rcyl(0.022, 0.035, (rx, 0.408, z), axis="Y", seg=10, r=0.004, bevel_seg=1))
    acc.append(s.tube([(rx, ry, 1.93), (rx, ry, 1.98), (rx, 0.36, 2.0), (rx, 0.26, 2.0)], 0.012, res=1, segments_per=5))
    acc.append(s.lathe([(0.0, 1.965), (0.1, 1.97), (0.105, 1.985), (0.09, 2.0), (0.03, 2.01), (0.0, 2.012)], seg=20,
                       loc=(rx, 0.24, 0)))
    acc.append(s.rcyl(0.04, 0.05, (rx, 0.4, 1.1), axis="Y", seg=14, r=0.008, bevel_seg=1))
    acc.append(s.rbox((0.12, 0.018, 0.018), (rx + 0.045, 0.37, 1.1), r=0.006, seg=1))
    # front: top rail between the side walls, a fixed pane on the right, the door hinged on the left wall
    acc.append(s.rbox((0.86, 0.025, 0.03), (0, -0.455, 2.0), r=0.006, seg=1))
    acc.append(s.rbox((0.03, 0.022, 1.93), (0.0, -0.455, 1.035), r=0.006, seg=1))
    hinge_x, hinge_y = -0.42, -0.455
    open_a = math.radians(40)
    d = Vector((math.cos(open_a), math.sin(open_a), 0))
    for z in (0.35, 1.75):
        acc.append(s.rbox((0.05, 0.03, 0.07), (hinge_x + 0.012, hinge_y, z), r=0.006, seg=1, rot=(0, 0, open_a / 2)))
    hp = Vector((hinge_x, hinge_y, 1.2)) + d * 0.36
    acc.append(s.rbox((0.022, 0.03, 0.3), tuple(hp), r=0.008, seg=2, rot=(0, 0, open_a)))
    s.part("Accent", acc)

    pane = s.rbox((0.415, 0.008, 1.915), (0.2175, -0.455, 1.0325), r=0.003, seg=1)
    door = s.rbox((0.41, 0.008, 1.88), (0, 0, 0), r=0.003, seg=1)
    s.place(door, (0.212, 0, 0))
    s.place(door, (hinge_x, hinge_y, 1.06), (0, 0, open_a))
    s.part("Eye", [pane, door])
    return export("shower_stall")


MODELS = {
    "kitchen_sink": model_kitchen_sink,
    "stove": model_stove,
    "microwave": model_microwave,
    "wall_cabinet": model_wall_cabinet,
    "urinal": model_urinal,
    "mirror_sink": model_mirror_sink,
    "hand_dryer": model_hand_dryer,
    "bench": model_bench,
    "shower_stall": model_shower_stall,
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
