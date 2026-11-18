"""Home-like furniture for the kitchen, the restroom, the lounge and the corridors (round 2): dining table, kitchen island,
dishwasher, recycling bins, pantry cabinet, double sink, janitor cart, baby changing table, paper-towel dispenser,
sanitary bin, sectional sofa, egg chair, side table with a lamp, magazine rack, area rug, corridor bench, fire
extinguisher cabinet, notice board, twin waste bins and an umbrella stand.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms2_home.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y, z up, origin on the
floor in the middle of the footprint. Wall-hung pieces keep their real height and a back face flush with the footprint's
back (Blender +Y). Object names are material roles: Body*, Accent*, Trim*, Eye* (glows); doors that open are BodyDoor{n}.
Pieces overlap by volume (a few mm), never face to face. Forms come from shapes.py, finished by shapes.part; export()
goes through meshfix (floor=True) like rooms_wet.export.
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
from rooms_wet import bar_handle, frame  # noqa: E402  (shared helpers, read only)

DEG = math.pi / 180


def export(filename):
    """meshfix (furniture stands on the floor), then the FBX into <root>/Props/."""
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=filename, pinned=meshfix.moving)  # doors and drawers stay put
    path = os.path.join(g.out_dir, "Props", filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def prism(poly, a0, a1, axis="Z"):
    """A straight prism from a 2D polygon (counter-clockwise or not) extruded from a0 to a1 along axis.
    axis Z: poly is (x, y); axis X: poly is (y, z); axis Y: poly is (x, z). Bevel it afterwards (s.bevel_edges)."""
    bm = bmesh.new()

    def co(p, a):
        if axis == "Z":
            return (p[0], p[1], a)
        if axis == "X":
            return (a, p[0], p[1])
        return (p[0], a, p[1])

    lo = [bm.verts.new(co(p, a0)) for p in poly]
    hi = [bm.verts.new(co(p, a1)) for p in poly]
    bm.faces.new(lo)
    bm.faces.new(list(reversed(hi)))
    n = len(poly)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def flip_y(bm):
    """Mirror a shape across y = 0 (a front detail turned into a back one)."""
    bmesh.ops.scale(bm, vec=Vector((1, -1, 1)), verts=bm.verts)
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    return bm


def panel(bm, direction, min_area, thickness=0.04, depth=-0.006):
    """Recessed (shaker) panel on the faces of bm looking along direction."""
    return s.inset_faces(bm, s.facing(direction, min_area=min_area, tol=0.95), thickness=thickness, depth=depth)


def bowl(rx, ry, z0, h, wall=0.008, seg=24, loc=(0, 0)):
    """A hollow open bowl / tub as one closed solid: the outer wall up, the rim, the inner wall down to a pole."""
    x, y = loc
    rings = [(z0, rx * 0.62, ry * 0.62, 2, x, y), (z0 + h * 0.25, rx * 0.86, ry * 0.86, 2, x, y),
             (z0 + h * 0.75, rx * 0.98, ry * 0.98, 2, x, y), (z0 + h, rx, ry, 2, x, y),
             (z0 + h + 0.0001, rx - wall, ry - wall, 2, x, y),
             (z0 + h * 0.72, rx * 0.97 - wall, ry * 0.97 - wall, 2, x, y),
             (z0 + h * 0.25 + wall, rx * 0.8 - wall, ry * 0.8 - wall, 2, x, y),
             (z0 + wall, 0.0, 0.0, 2, x, y)]
    return s.loft(rings, seg=seg)


# --------------------------------------------------------------------------- kitchen

def model_dining_table():
    """Dining table 1.6 x 0.9, top at 0.75: a thick wooden top with a soft edge, an apron rail all round and four
    tapered square legs set in from the corners."""
    top = s.rbox((1.6, 0.9, 0.04), (0, 0, 0.73), r=0.014, seg=3)
    apron = []
    for y in (-0.36, 0.36):
        apron.append(s.rbox((1.38, 0.024, 0.09), (0, y, 0.668), r=0.006, seg=1))
    for x in (-0.716, 0.716):
        apron.append(s.rbox((0.024, 0.66, 0.088), (x, 0, 0.668), r=0.006, seg=1))
    s.part("Body", [top, *apron])
    legs = []
    for x in (-0.71, 0.71):
        for y in (-0.355, 0.355):
            legs.append(s.loft([(0.0, 0.019, 0.019, 4, x, y), (0.02, 0.02, 0.02, 4, x, y),
                                (0.715, 0.03, 0.03, 4, x, y)], seg=16))
    s.part("Accent", legs)
    return export("dining_table")


def model_kitchen_island():
    """Kitchen island 2.0 x 0.9, worktop at 0.92: a stone worktop that falls to the floor at both ends (waterfall
    panels) and overhangs the front by a third for stools; the cupboards (three doors with bar pulls) face the back
    (the cook's side), panelled fronts under the overhang, a kick plinth, a fruit bowl on top."""
    slab = prism([(-1.0, 0.0), (-0.96, 0.0), (-0.96, 0.88), (0.96, 0.88), (0.96, 0.0), (1.0, 0.0), (1.0, 0.92),
                  (-1.0, 0.92)], -0.45, 0.45, axis="Y")
    s.bevel_edges(slab, 0.008, 2, angle=30)
    s.part("Trim", [slab])

    carcass = s.rbox((1.93, 0.53, 0.8), (0, 0.155, 0.48), r=0.008, seg=1)
    fronts = []
    for x in (-0.64, 0.0, 0.64):  # panelled face under the overhang (stool side)
        p = s.rbox((0.6, 0.02, 0.66), (x, -0.12, 0.5), r=0.006, seg=2)
        panel(p, (0, -1, 0), 0.2, thickness=0.05)
        fronts.append(p)
    doors = []
    for i, x in enumerate((-0.64, 0.0, 0.64)):  # cupboard doors on the back
        d = s.rbox((0.62, 0.02, 0.7), (x, 0.428, 0.475), r=0.006, seg=2)
        panel(d, (0, 1, 0), 0.2, thickness=0.05)
        doors.append(d)
        doors += [flip_y(h) for h in bar_handle(x, 0.78, -0.437, 0.22, axis="X", r=0.008, stand=0.014)]
    bowl_bm = bowl(0.14, 0.14, 0.918, 0.075, wall=0.008, seg=24, loc=(0.48, -0.1))
    s.part("Body", [carcass, *fronts, *doors, bowl_bm])

    plinth = s.rbox((1.9, 0.5, 0.1), (0, 0.15, 0.05), r=0.006, seg=1)
    fruit = []
    for (dx, dy, r) in ((-0.04, -0.03, 0.04), (0.05, -0.02, 0.038), (0.0, 0.05, 0.04), (0.01, 0.0, 0.036)):
        z = 0.96 if (dx, dy) != (0.01, 0.0) else 1.005
        fruit.append(s.ball(r, (0.48 + dx, -0.1 + dy, z), seg=12))
    stalk = s.rcyl(0.004, 0.02, (0.49, -0.1, 1.045), seg=6, r=0.001, bevel_seg=1)
    s.part("Accent", [plinth, *fruit, stalk])
    return export("kitchen_island")


def model_dishwasher():
    """Built-in dishwasher module 0.6 x 0.62, worktop at 0.92: a hollow carcass, a dark control strip with buttons and
    a little display, a recessed kick plinth; the front pulls out like a drawer (BodyDrawer0: Items.Storage moves
    drawers straight out, a drop-down door is not one of its motions) with its pull bar and two dish baskets - plates
    below, cups above - so the opened machine shows its load."""
    carcass = s.cupboard(-0.297, 0.297, -0.258, 0.31, 0.08, 0.89, t=0.016)
    strip = s.rbox((0.58, 0.024, 0.08), (0, -0.264, 0.83), r=0.006, seg=2)
    buttons = [s.rcyl(0.009, 0.01, (x, -0.277, 0.83), axis="Y", seg=10, r=0.002, bevel_seg=1)
               for x in (0.1, 0.14, 0.18, 0.22)]
    s.part("Body", [*carcass, strip, *buttons])
    top = s.rbox((0.6, 0.62, 0.04), (0, 0, 0.9), r=0.008, seg=2)
    s.part("Trim", [top])
    door = s.rbox((0.58, 0.022, 0.68), (0, -0.27, 0.445), r=0.008, seg=2)
    s.groove(door, "Z", 0.27, 0.008, 0.003)
    handle = bar_handle(0, 0.735, -0.28, 0.42, axis="X", r=0.008, stand=0.014)
    load = s.tray(-0.26, 0.26, -0.262, 0.27, 0.13, 0.14, t=0.008) + s.tray(-0.26, 0.26, -0.262, 0.27, 0.5, 0.1, t=0.008)
    load += [s.rcyl(0.1, 0.012, (x, 0.0, 0.235), axis="X", seg=20, r=0.003, bevel_seg=1)
             for x in (-0.16, -0.08, 0.0, 0.08, 0.16)]                                      # plates on edge
    load += [s.rcyl(0.04, 0.09, (x, y, 0.552), seg=14, r=0.004, bevel_seg=1)
             for x in (-0.15, 0.0, 0.15) for y in (-0.1, 0.12)]                             # cups upside down
    s.part("BodyDrawer0", [door, *handle, *load])
    plinth = s.rbox((0.58, 0.5, 0.11), (0, 0.04, 0.055), r=0.006, seg=1)
    feet = [s.rcyl(0.018, 0.012, (x, -0.2, 0.006), seg=10, r=0.003, bevel_seg=1) for x in (-0.24, 0.24)]
    s.part("Accent", [plinth, *feet])
    display = s.rbox((0.09, 0.008, 0.026), (-0.17, -0.276, 0.83), r=0.003, seg=1)
    s.part("Eye", [display])
    return export("dishwasher")


def model_recycling_bins():
    """Three sorting bins side by side, 0.38 wide each: tapered tubs with a recessed label panel, lids with their
    openings (a round hole for bottles, a slot for paper, a swing flap for the rest). One colour role per bin."""
    roles = ("Body", "Accent", "Trim")
    for k, x in enumerate((-0.405, 0.0, 0.405)):
        tub = s.loft([(0.0, 0.165, 0.165, 4, x, 0), (0.06, 0.172, 0.172, 4, x, 0), (0.82, 0.188, 0.19, 4, x, 0)],
                     seg=24, round_caps=0.01)
        s.groove(tub, "Z", 0.74, 0.008, 0.004)
        lid = s.rbox((0.396, 0.4, 0.07), (x, 0.0, 0.86), r=0.02, seg=2)
        if k == 0:
            cut = s.rcyl(0.07, 0.2, (x, -0.04, 0.9), seg=16, r=0)
        elif k == 1:
            cut = s.rbox((0.24, 0.035, 0.2), (x, -0.06, 0.9), r=0.012, seg=2, vertical_only=True)
        else:
            cut = s.rbox((0.22, 0.16, 0.2), (x, -0.06, 0.9), r=0.02, seg=2, vertical_only=True)
        lid = s.boolean(lid, cut, "DIFFERENCE")
        s.bevel_edges(lid, 0.004, 1, angle=50)
        parts = [tub, lid]
        if k == 2:  # the flap, tilted in a little
            parts.append(s.rbox((0.21, 0.15, 0.012), (x, -0.06, 0.875), r=0.004, seg=1, rot=(-8 * DEG, 0, 0)))
        parts.append(s.rbox((0.12, 0.03, 0.03), (x, 0.17, 0.83), r=0.008, seg=1))  # lid hinge block at the back
        parts.append(s.rbox((0.2, 0.012, 0.22), (x, -0.181, 0.5), r=0.006, seg=2, rot=(-1.4 * DEG, 0, 0)))  # label
        s.part(roles[k], parts)
    return export("recycling_bins")


def model_pantry_cabinet():
    """Tall pantry cupboard 0.9 x 0.6 x 2.0: an open carcass with shelves and a few tins and jars inside, two doors
    (BodyDoor0/1, two sunk panels each and a long bar pull at the meeting edges), a cornice and a kick plinth."""
    carcass = [s.rbox((0.024, 0.57, 1.92), (x * 0.436, 0.012, 1.01), r=0.004, seg=1) for x in (-1, 1)]
    carcass.append(s.rbox((0.9, 0.596, 0.04), (0, 0.0, 1.98), r=0.01, seg=2))           # top / cornice
    carcass.append(s.rbox((0.856, 0.56, 0.024), (0, 0.015, 0.1), r=0.004, seg=1))       # bottom
    carcass.append(s.rbox((0.856, 0.02, 1.88), (0, 0.285, 1.02), r=0.003, seg=1))       # back
    shelves = [s.rbox((0.856, 0.5, 0.02), (0, 0.03, z), r=0.003, seg=1) for z in (0.5, 0.9, 1.3, 1.65)]
    s.part("Body", [*carcass, *shelves])
    for i, x in enumerate((-0.2225, 0.2225)):
        d = s.rbox((0.44, 0.022, 1.86), (x, -0.276, 1.03), r=0.008, seg=2)
        for zc, h in ((1.45, 0.86), (0.53, 0.76)):
            d = s.boolean(d, s.rbox((0.34, 0.02, h), (x, -0.293, zc), r=0.006, seg=1, vertical_only=True),
                          "DIFFERENCE")
        s.bevel_edges(d, 0.003, 1, angle=50)
        hx = x + (0.17 if x < 0 else -0.17)
        pull = bar_handle(hx, 1.0, -0.287, 0.36, r=0.009, stand=0.016)
        s.part(f"BodyDoor{i}", [d, *pull])
    plinth = s.rbox((0.84, 0.5, 0.1), (0, 0.04, 0.05), r=0.006, seg=1)
    goods = []
    for z in (0.51, 0.91, 1.31):
        for k, x in enumerate((-0.3, -0.18, -0.05, 0.12, 0.27)):
            if (k + int(z * 10)) % 3 == 0:
                goods.append(s.rbox((0.11, 0.16, 0.24), (x, 0.1, z + 0.115), r=0.008, seg=1))   # a cereal box
            else:
                goods.append(s.rcyl(0.045, 0.12 + 0.03 * (k % 2), (x, 0.02 + 0.05 * (k % 2), z + 0.055 + 0.015 * (k % 2)),
                                    seg=14, r=0.004, bevel_seg=1))
    s.part("Accent", [plinth, *goods])
    return export("pantry_cabinet")


# --------------------------------------------------------------------------- restroom

def model_double_sink():
    """Double vanity 1.4 x 0.56, counter at 0.85: an open cabinet (a divider, a shelf) on a recessed plinth with four
    shaker doors that open in two pairs from their meeting edges (BodyDoor0..3, knobs in them: Items.Storage), a stone
    counter with an upstand, two vessel basins with tall taps, and one long framed mirror on the wall above
    (1.1..1.9, glass Eye)."""
    cab = s.cupboard(-0.68, 0.68, -0.225, 0.275, 0.11, 0.83, t=0.018, top=False, dividers=(0.0,), shelves=(0.42,))
    plinth = s.rbox((1.32, 0.44, 0.12), (0, 0.04, 0.06), r=0.006, seg=1)
    for i, x in enumerate((-0.5, -0.17, 0.17, 0.5)):
        d = s.rbox((0.32, 0.02, 0.64), (x, -0.226, 0.47), r=0.008, seg=2)
        panel(d, (0, -1, 0), 0.1, thickness=0.04)
        kx = x + (0.12 if x in (-0.5, 0.17) else -0.12)                                    # knob at the meeting edge
        knob = [s.rcyl(0.016, 0.014, (kx, -0.248, 0.7), axis="Y", seg=12, r=0.005, bevel_seg=1),
                s.rcyl(0.007, 0.016, (kx, -0.238, 0.7), axis="Y", seg=8, r=0.002, bevel_seg=1)]
        s.part(f"BodyDoor{i}", [d, *knob])
    basins = [bowl(0.2, 0.16, 0.848, 0.12, wall=0.01, seg=20, loc=(x, -0.02)) for x in (-0.36, 0.36)]
    s.part("Body", [*cab, plinth, *basins])
    counter = s.rbox((1.4, 0.56, 0.04), (0, 0, 0.83), r=0.01, seg=2)
    upstand = s.rbox((1.396, 0.02, 0.28), (0, 0.269, 0.975), r=0.006, seg=1)  # splashback up to the mirror
    s.groove(upstand, "Z", 0.975, 0.006, 0.003)
    s.part("Trim", [counter, upstand])

    acc = []
    for x in (-0.36, 0.36):
        acc.append(s.rcyl(0.026, 0.02, (x, 0.2, 0.858), seg=12, r=0.004, bevel_seg=1))
        acc.append(s.tube([(x, 0.2, 0.86), (x, 0.2, 1.04), (x, 0.17, 1.08), (x, 0.1, 1.075), (x, 0.08, 1.05)],
                          0.012, res=1, segments_per=4))
        acc.append(s.rcyl(0.015, 0.025, (x, 0.08, 1.04), seg=12, r=0.003, bevel_seg=1))
        acc.append(s.rbox((0.012, 0.07, 0.014), (x + 0.03, 0.2, 0.95), r=0.005, seg=1, rot=(-20 * DEG, 0, 0)))
        acc.append(s.rcyl(0.014, 0.03, (x + 0.02, 0.2, 0.95), axis="X", seg=10, r=0.003, bevel_seg=1))
    acc.append(frame((1.3, 0.024, 0.8), (0, 0.268, 1.5), 0.035, r=0.008, seg=1))
    s.part("Accent", acc)
    glass = s.rbox((1.245, 0.008, 0.745), (0, 0.256, 1.5), r=0.003, seg=1)
    s.part("Eye", [glass])
    return export("double_sink")


def model_janitor_cart():
    """Cleaning trolley 0.9 x 0.5 x 1.0 on four casters: a moulded base tray, a mop bucket with a wringer and its lever
    on the left with a mop standing in it, a two-shelf caddy on the right with bins and a spray bottle, a push handle."""
    base = s.rbox((0.86, 0.46, 0.06), (-0.02, 0, 0.12), r=0.02, seg=2)
    s.inset_faces(base, s.facing((0, 0, 1), min_area=0.2, tol=0.95), thickness=0.025, depth=-0.012)
    posts = [s.rbox((0.03, 0.03, 0.78), (x, y, 0.53), r=0.008, seg=1) for x in (0.05, 0.395) for y in (-0.2, 0.2)]
    mid = s.rbox((0.38, 0.43, 0.025), (0.222, 0, 0.5), r=0.008, seg=1)
    topt = s.rbox((0.39, 0.44, 0.05), (0.222, 0, 0.9), r=0.012, seg=2)
    s.inset_faces(topt, s.facing((0, 0, 1), min_area=0.1, tol=0.95), thickness=0.02, depth=-0.03)
    handle = s.tube([(0.39, -0.17, 0.9), (0.43, -0.17, 0.95), (0.432, -0.12, 0.985), (0.432, 0.12, 0.985),
                     (0.43, 0.17, 0.95), (0.39, 0.17, 0.9)], 0.014, res=1, segments_per=4)
    stick = s.tube([(-0.25, 0.06, 0.2), (-0.31, 0.13, 0.99)], 0.012, smooth=False)
    s.part("Body", [base, *posts, mid, topt, handle, stick])

    wheels = []
    for x in (-0.38, 0.34):
        for y in (-0.17, 0.17):
            wheels.append(s.rcyl(0.045, 0.034, (x, y, 0.046), axis="X", seg=14, r=0.008, bevel_seg=1))
            wheels.append(s.rbox((0.05, 0.05, 0.05), (x, y, 0.088), r=0.008, seg=1))
    # wringer on the back half of the bucket, its lever up beside the mop
    wringer = s.rbox((0.3, 0.14, 0.12), (-0.22, 0.09, 0.46), r=0.015, seg=2)
    lever = s.tube([(-0.11, 0.12, 0.5), (-0.1, 0.14, 0.75), (-0.12, 0.15, 0.9)], 0.013, res=1, segments_per=4)
    grip = s.rcyl(0.018, 0.08, (-0.12, 0.15, 0.92), seg=12, r=0.006, bevel_seg=1)
    bottle = s.lathe([(0.0, 0.89), (0.035, 0.89), (0.035, 0.99), (0.02, 1.01), (0.012, 1.03), (0.0, 1.03)], seg=14,
                     loc=(0.3, -0.12, 0))
    trigger = s.rbox((0.03, 0.05, 0.03), (0.3, -0.14, 1.025), r=0.008, seg=1)
    s.part("Accent", [*wheels, wringer, lever, grip, bottle, trigger])

    bucket = bowl(0.18, 0.2, 0.14, 0.3, wall=0.012, seg=22, loc=(-0.22, 0))
    mop = s.loft([(0.2, 0.06, 0.04, 2, -0.25, 0.06), (0.26, 0.09, 0.07, 2, -0.25, 0.06),
                  (0.32, 0.05, 0.04, 2, -0.25, 0.06)], seg=12)
    bins = [s.rbox((0.15, 0.17, 0.18), (0.14, y, 0.6), r=0.02, seg=2) for y in (-0.1, 0.1)]
    bins.append(s.rbox((0.15, 0.36, 0.16), (0.31, 0, 0.59), r=0.02, seg=2))
    rag = s.rbox((0.14, 0.12, 0.03), (0.15, 0.1, 0.89), r=0.012, seg=2)
    s.part("Trim", [bucket, mop, *bins, rag])
    return export("janitor_cart")


def model_changing_table():
    """Wall-mounted fold-down baby changing table, folded open: a moulded wall housing (0.75..1.1) with a hinge at its
    foot, the bed out horizontal at 0.86 with raised rims and a dished pad area, two gas-strut arms, a safety strap."""
    housing = s.rbox((0.9, 0.05, 0.36), (0, 0.224, 0.92), r=0.02, seg=3)
    s.inset_faces(housing, s.facing((0, -1, 0), min_area=0.2, tol=0.95), thickness=0.04, depth=-0.012)
    bed = s.rbox((0.86, 0.46, 0.06), (0, -0.02, 0.85), r=0.025, seg=3)
    s.inset_faces(bed, s.facing((0, 0, 1), min_area=0.2, tol=0.95), thickness=0.045, depth=-0.025)
    s.part("Body", [housing, bed])
    acc = [s.rcyl(0.02, 0.84, (0, 0.2, 0.83), axis="X", seg=14, r=0.005, bevel_seg=1)]
    for x in (-0.4, 0.4):
        acc.append(s.tube([(x, 0.2, 1.05), (x, -0.1, 0.88)], 0.011, smooth=False))
        acc.append(s.rcyl(0.02, 0.02, (x, 0.2, 1.05), axis="X", seg=12, r=0.004, bevel_seg=1))
    acc.append(s.rbox((0.5, 0.015, 0.04), (0, 0.205, 1.0), r=0.006, seg=1))  # a label plate on the housing
    s.part("Accent", acc)
    pad = s.rbox((0.76, 0.36, 0.03), (0, -0.02, 0.85), r=0.012, seg=2)
    strap = s.rbox((0.04, 0.37, 0.012), (0, -0.02, 0.866), r=0.004, seg=1)
    buckle = s.rbox((0.06, 0.04, 0.018), (0, -0.16, 0.868), r=0.006, seg=1)
    s.part("Trim", [pad, strap, buckle])
    return export("changing_table")


def model_towel_dispenser():
    """Wall paper-towel dispenser 0.3 x 0.12 at 1.2..1.6: a rounded shell with a lid seam and a keyhole, a dark level
    window, a push plate low on the front, a towel tongue hanging out of the slot underneath."""
    shell = s.loft([(1.22, 0.14, 0.035, 4, 0, 0.02), (1.25, 0.148, 0.052, 4, 0, 0.004),
                    (1.55, 0.15, 0.055, 4, 0, 0.002), (1.6, 0.145, 0.05, 4, 0, 0.007)], seg=28, round_caps=0.012)
    s.groove(shell, "Z", 1.52, 0.008, 0.004)
    plate = s.rbox((0.29, 0.016, 0.38), (0, 0.051, 1.41), r=0.01, seg=2)
    s.part("Body", [shell, plate])
    window = s.rbox((0.16, 0.012, 0.025), (0, -0.05, 1.45), r=0.005, seg=1)
    push = s.rbox((0.2, 0.014, 0.08), (0, -0.051, 1.31), r=0.01, seg=2)
    key = s.rcyl(0.008, 0.01, (0.1, -0.052, 1.565), axis="Y", seg=10, r=0.002, bevel_seg=1)
    slot = s.rbox((0.22, 0.04, 0.012), (0, -0.005, 1.218), r=0.005, seg=1)
    s.part("Accent", [window, push, key, slot])
    towel = s.rbox((0.19, 0.004, 0.1), (0, -0.005, 1.18), r=0.0015, seg=1, rot=(8 * DEG, 0, 0))
    s.part("Trim", [towel])
    return export("towel_dispenser")


def model_sanitary_bin():
    """Small pedal bin 0.3 x 0.24 x 0.45: an oval body, a domed lid on a back hinge, a foot pedal and a base ring."""
    can = s.loft([(0.02, 0.13, 0.1, 2.6), (0.3, 0.143, 0.112, 2.6), (0.4, 0.145, 0.114, 2.6)], seg=28, round_caps=0.008)
    s.groove(can, "Z", 0.36, 0.006, 0.003)
    s.part("Body", [can])
    lid = s.loft([(0.39, 0.15, 0.119, 2.6), (0.42, 0.147, 0.116, 2.6), (0.45, 0.1, 0.08, 2.6), (0.452, 0.0, 0.0, 2.6)],
                 seg=28)
    ring = s.loft([(0.0, 0.128, 0.098, 2.6), (0.035, 0.137, 0.106, 2.6)], seg=28, round_caps=0.006)
    pedal = s.rbox((0.1, 0.05, 0.022), (0, -0.103, 0.022), r=0.008, seg=2)
    hinge = s.rcyl(0.012, 0.08, (0, 0.116, 0.395), axis="X", seg=10, r=0.003, bevel_seg=1)
    s.part("Accent", [lid, ring, pedal, hinge])
    return export("sanitary_bin")


# --------------------------------------------------------------------------- lounge

def model_sectional_sofa():
    """L-shaped corner sofa 2.4 x 1.6 x 0.85: an upholstered L base on low dark feet, a back along the rear and down the
    +x side, an arm at the free end of each wing, three seat cushions on the long wing and two on the return, plump back
    cushions leaning on the back."""
    L = [(-1.2, -0.05), (0.35, -0.05), (0.35, -0.8), (1.2, -0.8), (1.2, 0.8), (-1.2, 0.8)]
    base = prism(L, 0.06, 0.32)
    s.bevel_edges(base, 0.035, 3, angle=30)
    backL = [(-1.2, 0.6), (1.0, 0.6), (1.0, -0.6), (1.2, -0.6), (1.2, 0.8), (-1.2, 0.8)]
    back = prism(backL, 0.28, 0.66)
    s.bevel_edges(back, 0.05, 2, angle=30)
    arm1 = s.rbox((0.2, 0.85, 0.56), (-1.1, 0.375, 0.34), r=0.07, seg=2)
    arm2 = s.rbox((0.85, 0.2, 0.56), (0.775, -0.7, 0.34), r=0.07, seg=2)
    seats = []
    for x0, x1 in ((-1.0, -0.36), (-0.35, 0.34)):
        seats.append(s.rbox((x1 - x0, 0.66, 0.15), ((x0 + x1) / 2, 0.27, 0.385), r=0.05, seg=3))
    seats.append(s.rbox((0.65, 0.66, 0.15), (0.675, 0.27, 0.385), r=0.05, seg=3))   # the corner
    seats.append(s.rbox((0.65, 0.65, 0.15), (0.675, -0.27, 0.385), r=0.05, seg=3))  # the return
    s.part("Body", [base, back, arm1, arm2, *seats])

    cushions = []
    for x in (-0.66, 0.0, 0.66):
        cushions.append(s.rbox((0.63, 0.18, 0.4), (x, 0.55, 0.64), r=0.07, seg=2, rot=(-12 * DEG, 0, 0)))
    for y in (0.24, -0.38):
        cushions.append(s.rbox((0.18, 0.6, 0.4), (0.95, y, 0.64), r=0.07, seg=2, rot=(0, -12 * DEG, 0)))
    s.part("Trim", cushions)
    feet = [s.rcyl(0.025, 0.07, (x, y, 0.035), seg=12, r=0.006, bevel_seg=1)
            for x, y in ((-1.14, -0.0), (-1.14, 0.74), (0.3, 0.0), (0.4, -0.74), (1.14, -0.74), (1.14, 0.74))]
    s.part("Accent", feet)
    return export("sectional_sofa")


def model_egg_chair():
    """Swivel egg chair 0.8 x 0.8 x 1.3: a moulded shell (outer loft minus inner loft, a wide oval mouth cut out of the
    front so the sides wrap forward as wings and the top hoods over), a plump seat cushion and a back pad inside, a
    stem and a round domed foot."""
    outer = s.loft([(0.27, 0.2, 0.2), (0.32, 0.33, 0.33), (0.42, 0.385, 0.385), (0.6, 0.4, 0.4), (0.85, 0.4, 0.4),
                    (1.05, 0.37, 0.37), (1.14, 0.34, 0.34), (1.21, 0.29, 0.29), (1.26, 0.22, 0.22), (1.29, 0.13, 0.13),
                    (1.3, 0.0, 0.0)], seg=28)
    inner = s.loft([(0.36, 0.26, 0.26), (0.42, 0.345, 0.345), (0.6, 0.368, 0.368), (0.85, 0.368, 0.368),
                    (1.05, 0.338, 0.338), (1.14, 0.308, 0.308), (1.2, 0.262, 0.262), (1.245, 0.19, 0.19),
                    (1.27, 0.1, 0.1), (1.278, 0.0, 0.0)], seg=28)
    hug = inner.copy()  # the back pad hugs the inside of the shell
    s.place(hug, scale=(1.035, 1.035, 1.0))
    shell = s.boolean(outer, inner, "DIFFERENCE")
    mouth = s.ball(1.0, (0, -0.45, 0.9), seg=24, scale=(0.33, 0.4, 0.5))
    s.place(mouth, rot=(0, 0, math.pi / 28))
    shell = s.boolean(shell, mouth, "DIFFERENCE")
    s.bevel_edges(shell, 0.008, 2, angle=40)
    s.part("Body", [shell])
    seat = s.loft([(0.37, 0.3, 0.3, 2.2, 0, -0.03), (0.43, 0.325, 0.33, 2.2, 0, -0.03),
                   (0.47, 0.31, 0.315, 2.2, 0, -0.03)], seg=24, round_caps=0.03)
    pad = s.rbox((0.44, 0.24, 0.4), (0, 0.28, 0.74), r=0.04, seg=2)
    pad = s.boolean(pad, hug, "INTERSECT")
    s.bevel_edges(pad, 0.012, 2, angle=40)
    s.part("Trim", [seat, pad])
    foot = s.lathe([(0.0, 0.0), (0.28, 0.0), (0.285, 0.008), (0.27, 0.018), (0.12, 0.04), (0.05, 0.05), (0.0, 0.05)],
                   seg=32)
    stem = s.rcyl(0.035, 0.27, (0, 0, 0.17), seg=16, r=0.006, bevel_seg=1)
    collar = s.rcyl(0.06, 0.03, (0, 0, 0.29), seg=20, r=0.008, bevel_seg=1)
    s.part("Accent", [foot, stem, collar])
    return export("egg_chair")


def model_side_table():
    """Round side table 0.5 x 0.55 on a pedestal with a domed foot, a little table lamp on it (shade = Eye) towards the
    back and a book beside it (the front half of the top stays free)."""
    top = s.lathe([(0.0, 0.52), (0.245, 0.52), (0.25, 0.53), (0.25, 0.545), (0.244, 0.55), (0.0, 0.55)], seg=32)
    s.part("Body", [top])
    foot = s.lathe([(0.0, 0.0), (0.17, 0.0), (0.175, 0.008), (0.16, 0.018), (0.05, 0.035), (0.0, 0.035)], seg=28)
    stem = s.rcyl(0.022, 0.5, (0, 0, 0.275), seg=14, r=0.004, bevel_seg=1)
    lx, ly = 0.07, 0.08
    lbase = s.lathe([(0.0, 0.545), (0.06, 0.545), (0.062, 0.555), (0.05, 0.57), (0.015, 0.575), (0.0, 0.575)], seg=20,
                    loc=(lx, ly, 0))
    lstem = s.rcyl(0.008, 0.26, (lx, ly, 0.69), seg=10, r=0.002, bevel_seg=1)
    s.part("Accent", [foot, stem, lbase, lstem])
    shade = s.lathe([(0.0, 0.79), (0.115, 0.79), (0.112, 0.8), (0.08, 0.95), (0.0, 0.95)], seg=28, loc=(lx, ly, 0))
    s.part("Eye", [shade])
    book = s.rbox((0.15, 0.2, 0.028), (-0.1, -0.06, 0.562), r=0.005, seg=1, rot=(0, 0, 18 * DEG))
    s.part("Trim", [book])
    return export("side_table")


def model_magazine_rack():
    """Floor magazine rack 0.6 x 0.36 x 1.0: two leaning trapezoid side frames (open in the middle), three stepped
    tiers — each a shelf with a front lip and a slanted back board — and magazines leaning on the boards."""
    side_poly = [(-0.17, 0.0), (0.17, 0.0), (0.17, 1.0), (0.01, 1.0)]
    hole_poly = [(-0.09, 0.07), (0.1, 0.07), (0.1, 0.93), (0.045, 0.93)]
    sides = []
    for x in (-0.29, 0.29):
        sp = prism(side_poly, x - 0.012, x + 0.012, axis="X")
        sp = s.boolean(sp, prism(hole_poly, x - 0.05, x + 0.05, axis="X"), "DIFFERENCE")
        s.bevel_edges(sp, 0.006, 2, angle=30)
        sides.append(sp)
    body, lips, mags = [], [], []
    tilt = -12 * DEG
    for k, zb in enumerate((0.08, 0.4, 0.72)):
        yf = -0.17 + 0.16 * zb + 0.012
        body.append(s.rbox((0.568, 0.14, 0.02), (0, yf + 0.07, zb), r=0.005, seg=1))
        lips.append(s.rbox((0.568, 0.016, 0.06), (0, yf + 0.008, zb + 0.022), r=0.006, seg=2))
        h = 0.28
        body.append(s.rbox((0.568, 0.014, h), (0, yf + 0.137 + 0.5 * h * math.sin(12 * DEG), zb + 0.5 * h * math.cos(12 * DEG)),
                           r=0.005, seg=1, rot=(tilt, 0, 0)))
        for j, (mx, w, mh) in enumerate(((-0.13, 0.22, 0.27), (0.12, 0.21, 0.25))):
            mx += 0.02 * ((k + j) % 2)
            yb, zb2 = yf + 0.108, zb + 0.012
            c = (mx, yb + 0.5 * mh * math.sin(13 * DEG), zb2 + 0.5 * mh * math.cos(13 * DEG))
            mags.append(s.rbox((w, 0.01, mh), c, r=0.003, seg=1, rot=(-13 * DEG, 0, (j - 0.5) * 3 * DEG)))
    s.part("Body", [*sides, *body])
    s.part("Accent", lips)
    s.part("Trim", mags)
    return export("magazine_rack")


def model_area_rug():
    """Flat rug 2.0 x 1.4 x 0.02: the field (Body) in a woven border band (Accent), an inner line, a central medallion
    (a diamond with a diamond hole and a dot) and four corner motifs inlaid flush-ish on the field."""
    field = s.rbox((1.86, 1.26, 0.014), (0, 0, 0.009), r=0.003, seg=1)
    s.part("Body", [field])
    border = frame((2.0, 1.4, 0.02), (0, 0, 0.01), 0.08, r=0.004, seg=1, axis_open="Z")
    line = frame((1.6, 1.0, 0.012), (0, 0, 0.012), 0.025, r=0.002, seg=1, axis_open="Z")
    med = prism([(0.0, -0.32), (0.45, 0.0), (0.0, 0.32), (-0.45, 0.0)], 0.006, 0.0185)
    med = s.boolean(med, prism([(0.0, -0.2), (0.28, 0.0), (0.0, 0.2), (-0.28, 0.0)], 0.0, 0.03), "DIFFERENCE")
    s.bevel_edges(med, 0.002, 1, angle=50)
    dot = prism([(0.0, -0.08), (0.11, 0.0), (0.0, 0.08), (-0.11, 0.0)], 0.006, 0.0185)
    corners = [prism([(x, y - 0.06), (x + 0.08, y), (x, y + 0.06), (x - 0.08, y)], 0.006, 0.0185)
               for x in (-0.64, 0.64) for y in (-0.36, 0.36)]
    s.part("Accent", [border, line, med, dot, *corners])
    return export("area_rug")


# --------------------------------------------------------------------------- corridor

def model_corridor_bench():
    """Slim corridor bench 1.4 x 0.44 x 0.45: an upholstered pad on a wooden board, two flat steel frame legs (a closed
    rectangle each) and a low stretcher between them."""
    pad = s.rbox((1.4, 0.44, 0.055), (0, 0, 0.42), r=0.02, seg=3)
    s.part("Body", [pad])
    board = s.rbox((1.36, 0.4, 0.03), (0, 0, 0.385), r=0.008, seg=2)
    s.part("Trim", [board])
    steel = []
    for x in (-0.56, 0.56):
        steel += [s.rbox((0.03, 0.03, 0.372), (x, y, 0.19), r=0.006, seg=1) for y in (-0.17, 0.17)]
        steel.append(s.rbox((0.026, 0.37, 0.028), (x, 0, 0.364), r=0.006, seg=1))
        steel.append(s.rbox((0.026, 0.37, 0.028), (x, 0, 0.016), r=0.006, seg=1))
        steel.append(s.rbox((0.022, 0.024, 0.34), (x, 0, 0.19), r=0.005, seg=1))  # middle post (holds the stretcher)
    steel.append(s.rbox((1.1, 0.02, 0.024), (0, 0, 0.12), r=0.006, seg=1))
    s.part("Accent", steel)
    return export("corridor_bench")


def model_fire_cabinet():
    """Wall fire-extinguisher cabinet 0.4 x 0.24 at 0.8..1.5: an open box on the wall, a framed glass door with a pull,
    a red extinguisher inside on its bracket (body, valve head, lever, hose curling down the side)."""
    box = s.rbox((0.4, 0.21, 0.7), (0, 0.015, 1.15), r=0.012, seg=2)
    box = s.boolean(box, s.rbox((0.36, 0.22, 0.66), (0, -0.016, 1.15), r=0.006, seg=1), "DIFFERENCE")
    s.bevel_edges(box, 0.003, 1, angle=50)
    door = frame((0.396, 0.026, 0.696), (0, -0.108, 1.15), 0.04, r=0.006, seg=2)
    pull = s.rbox((0.022, 0.016, 0.12), (0.155, -0.124, 1.15), r=0.006, seg=1)
    sign = s.rbox((0.3, 0.008, 0.035), (0, -0.124, 1.47), r=0.003, seg=1)
    s.part("Body", [box, door, pull, sign])
    glass = s.rbox((0.33, 0.006, 0.63), (0, -0.108, 1.15), r=0.002, seg=1)
    s.part("Trim", [glass])
    ex, ey = -0.03, 0.0
    body = s.lathe([(0.0, 0.815), (0.068, 0.815), (0.072, 0.83), (0.072, 1.2), (0.062, 1.25), (0.03, 1.275),
                    (0.0, 1.278)], seg=24, loc=(ex, ey, 0))
    neck = s.rcyl(0.018, 0.06, (ex, ey, 1.29), seg=12, r=0.004, bevel_seg=1)
    head = s.rbox((0.07, 0.035, 0.03), (ex + 0.01, ey, 1.33), r=0.008, seg=1)
    lever = s.rbox((0.12, 0.022, 0.012), (ex + 0.03, ey, 1.346), r=0.004, seg=1, rot=(0, 8 * DEG, 0))
    hose = s.tube([(ex + 0.04, ey, 1.32), (0.1, ey - 0.02, 1.25), (0.11, ey - 0.03, 1.0), (0.1, ey - 0.04, 0.9)],
                  0.011, segments_per=6)
    nozzle = s.rcyl(0.016, 0.06, (0.1, ey - 0.04, 0.88), seg=10, r=0.004, bevel_seg=1)
    band = s.rcyl(0.074, 0.12, (ex, ey, 1.02), seg=24, r=0.003, bevel_seg=1)
    bracket = s.rbox((0.08, 0.1, 0.03), (ex, 0.06, 1.12), r=0.006, seg=1)
    s.part("Accent", [body, neck, head, lever, hose, nozzle, band, bracket])
    return export("fire_cabinet")


def model_notice_board():
    """Wall cork board 1.2 x 0.8 at 1.1..1.9: a wooden frame, the cork field, pinned papers at slight angles (a poster,
    notes, a list) with their pins."""
    board = frame((1.2, 0.04, 0.8), (0, 0.009, 1.5), 0.04, r=0.008, seg=2)
    pins = []
    papers = []
    specs = ((-0.37, 1.58, 0.3, 0.4, 3), (0.0, 1.66, 0.21, 0.28, -4), (0.02, 1.33, 0.25, 0.18, 2),
             (0.33, 1.6, 0.21, 0.3, -2), (0.36, 1.28, 0.16, 0.16, 6), (-0.4, 1.23, 0.18, 0.12, -5))
    for x, z, w, h, a in specs:
        papers.append(s.rbox((w, 0.004, h), (x, -0.003, z), r=0.0015, seg=1, rot=(0, a * DEG, 0)))
        pins.append(s.ball(0.009, (x - 0.5 * h * math.sin(a * DEG), -0.009, z + 0.5 * h * math.cos(a * DEG) - 0.02),
                           seg=10))
    s.part("Trim", [board, *pins])
    cork = s.rbox((1.13, 0.03, 0.73), (0, 0.013, 1.5), r=0.003, seg=1)
    s.part("Body", [cork])
    s.part("Accent", papers)
    return export("notice_board")


def model_bin_pair():
    """Twin waste bins 0.7 x 0.36 x 0.85 on a shared plinth: two tall rounded tubs (one per colour role) with domed lids,
    each lid with a push flap in a sunk mouth at the front."""
    plinth = s.rbox((0.7, 0.34, 0.04), (0, 0, 0.02), r=0.01, seg=2)
    lids = [plinth]
    for role, x in (("Body", -0.176), ("Trim", 0.176)):
        tub = s.loft([(0.035, 0.155, 0.16, 4, x, 0), (0.2, 0.16, 0.168, 4, x, 0), (0.75, 0.166, 0.175, 4, x, 0)],
                     seg=24, round_caps=0.008)
        s.groove(tub, "Z", 0.68, 0.008, 0.004)
        s.part(role, [tub])
        lid = s.loft([(0.74, 0.17, 0.179, 4, x, 0), (0.79, 0.168, 0.177, 4, x, 0), (0.83, 0.15, 0.16, 3, x, 0),
                      (0.85, 0.1, 0.11, 2.5, x, 0)], seg=24, round_caps=0.006)
        lid = s.boolean(lid, s.rbox((0.2, 0.2, 0.08), (x, -0.17, 0.8), r=0.015, seg=2, vertical_only=True),
                        "DIFFERENCE")
        s.bevel_edges(lid, 0.004, 1, angle=50)
        lids.append(lid)
        lids.append(s.rbox((0.18, 0.012, 0.07), (x, -0.075, 0.8), r=0.004, seg=1, rot=(-12 * DEG, 0, 0)))
    s.part("Accent", lids)
    return export("bin_pair")


def model_umbrella_stand():
    """Umbrella stand: a steel cylinder 0.3 x 0.6 with a rolled rim and a base ring, two closed umbrellas standing in it
    (furled canopies, shafts and crook handles up to ~0.9)."""
    can = s.lathe([(0.0, 0.0), (0.14, 0.0), (0.145, 0.006), (0.145, 0.6), (0.15, 0.6), (0.15, 0.61), (0.128, 0.61),
                   (0.128, 0.03), (0.0, 0.03)], seg=32)
    s.part("Body", [can])
    acc = [s.lathe([(0.0, 0.0), (0.15, 0.0), (0.152, 0.01), (0.15, 0.04), (0.143, 0.045), (0.0, 0.045)], seg=32)]
    rim = s.rcyl(0.153, 0.026, (0, 0, 0.597), seg=32, r=0)
    rim = s.boolean(rim, s.rcyl(0.131, 0.1, (0, 0, 0.6), seg=32, r=0, rot=(0, 0, math.pi / 32)), "DIFFERENCE")
    s.bevel_edges(rim, 0.006, 2, angle=50)
    acc.append(rim)
    s.part("Accent", acc)
    umb = []
    for (bx, by, ax, ay, hand) in ((-0.04, 0.03, -0.035, 0.02, 1), (0.045, -0.03, 0.035, -0.03, -1)):
        # tilt the umbrella from its tip (bx, by at the floor) to lean ax, ay at 0.9
        def at(z):
            return (bx + ax * z / 0.85, by + ay * z / 0.85, z)
        canopy = s.loft([(0.025, 0.006, 0.006), (0.1, 0.018, 0.018), (0.45, 0.038, 0.036, 2.3),
                         (0.62, 0.032, 0.03, 2.3), (0.7, 0.012, 0.012)], seg=12)
        s.place(canopy, (bx, by, 0), (math.atan2(-ay, 0.85), math.atan2(ax, 0.85), 0))
        shaft_top = at(0.86)
        shaft = s.tube([at(0.66), shaft_top], 0.0055, smooth=False)
        hx, hy, hz = shaft_top
        crook = s.tube([(hx, hy, hz - 0.01), (hx, hy, hz + 0.03), (hx + 0.04 * hand, hy, hz + 0.05),
                        (hx + 0.07 * hand, hy, hz + 0.02), (hx + 0.068 * hand, hy, hz - 0.005)], 0.011, segments_per=6)
        umb += [canopy, shaft, crook]
    s.part("Trim", umb)
    return export("umbrella_stand")


MODELS = {
    "dining_table": model_dining_table,
    "kitchen_island": model_kitchen_island,
    "dishwasher": model_dishwasher,
    "recycling_bins": model_recycling_bins,
    "pantry_cabinet": model_pantry_cabinet,
    "double_sink": model_double_sink,
    "janitor_cart": model_janitor_cart,
    "changing_table": model_changing_table,
    "towel_dispenser": model_towel_dispenser,
    "sanitary_bin": model_sanitary_bin,
    "sectional_sofa": model_sectional_sofa,
    "egg_chair": model_egg_chair,
    "side_table": model_side_table,
    "magazine_rack": model_magazine_rack,
    "area_rug": model_area_rug,
    "corridor_bench": model_corridor_bench,
    "fire_cabinet": model_fire_cabinet,
    "notice_board": model_notice_board,
    "bin_pair": model_bin_pair,
    "umbrella_stand": model_umbrella_stand,
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
