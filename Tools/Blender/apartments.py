"""The apartment block (a lived-in Soviet / post-Soviet panel flat): the bedroom (double bed, wardrobe, chest of drawers),
the bathroom (enamel bathtub, toilet, sink cabinet with a mirror cabinet, washing machine), the hallway (shoe cabinet,
coat rack), the stairwell lobby (post boxes, lift doors), the flat's entry door leaf (quilted dermantin with studs),
and the shelter map: folded on the floor (item_map_model) and held open in first person (held_map).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/apartments.py -- Assets/_Game/Art/Models [--only a,b]
      (furniture -> <root>/Props/<name>.fbx, the leaf -> <root>/Level/flat_leaf.fbx + flat_leaf_b.fbx,
       the folded map -> <root>/Items/item_map_model.fbx, the held map -> <root>/Props/held_map.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--floor] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y, z up, origin on the
floor in the middle of the footprint. Wall pieces (hall_hooks, mailboxes, elevator_doors, the vanity's mirror cabinet,
the bath's taps) keep their real height with the back face flush with the footprint's back (+Y). Object names are
material roles: Body*, Accent*, Trim*, Eye* (glows). Doors that open are BodyDoor{n}, drawers BodyDrawer{n} (separate,
hollow, their handles inside the same object so they move with them; meshfix keeps them where they are). Pieces
overlap by a few mm by volume, never face to face.
The leaf is 0.9 x 2.28 like storage_leaf (origin on the floor in its middle, the meeting edge at +X, both faces alike),
its _b twin has UVs shifted (door_leaf.B_SHIFT). The held map's origin is the sheet's centre (not the floor).
"""
import math
import os
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --)
import meshfix  # noqa: E402
import shapes as s  # noqa: E402
from rooms2_home import flip_y, panel  # noqa: E402
from rooms2_work import flat_ring, prism  # noqa: E402
from rooms_wet import bar_handle  # noqa: E402

DEG = math.pi / 180
MOVING = ("BodyDoor", "BodyDrawer")   # parts the game moves: meshfix must not shift them
B_SHIFT = (0.93, 1.37)                # the _b leaf's UV shift (door_leaf.B_SHIFT)


def export(filename, folder="Props", floor=True, shift=None):
    """meshfix, then the FBX into <root>/<folder>/; with a shift also <name>_b with its UVs moved (like storage_leaf)."""
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=floor, name=filename, pinned=lambda n: n.startswith(MOVING))
    path = os.path.join(g.out_dir, folder, filename + ".fbx")
    s.export_fbx(path, objs)
    if shift is not None:
        for o in objs:
            for d in o.data.uv_layers.active.data:
                d.uv = (d.uv[0] + shift[0], d.uv[1] + shift[1])
        s.export_fbx(os.path.join(g.out_dir, folder, filename + "_b.fbx"), objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def soft(size, loc, cuts=2, levels=1, rot=(0, 0, 0), fn=None):
    """A soft upholstered / cloth block: a box cage cut into a grid, pushed by fn(co) -> Vector, then subdivided
    smooth (duvets, pillows). The smoothing pulls the corners in by about a grid cell."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    if fn is not None:
        for v in bm.verts:
            v.co += fn(v.co.copy())
    bm = s.smooth(bm, levels=levels)
    return s.place(bm, loc, rot)


def knob(x, y_face, z, r=0.014, stand=0.016, seg=12):
    """A round furniture knob on a short neck, its back sunk 2 mm into a face at y_face looking -Y."""
    neck = s.rcyl(r * 0.45, stand, (x, y_face - stand / 2 + 0.002, z), axis="Y", seg=8, r=0.001, bevel_seg=1)
    head = s.rcyl(r, r * 0.8, (x, y_face - stand - r * 0.3, z), axis="Y", seg=seg, r=r * 0.35, bevel_seg=2)
    return [neck, head]


def transformed(bms, m):
    for b in bms:
        bmesh.ops.transform(b, matrix=m, verts=b.verts)
    return bms


# --------------------------------------------------------------------------- bedroom

def model_double_bed():
    """Double bed 1.6 x 2.05, mattress top 0.55: a wooden frame (side rails, a low footboard, a tall headboard to
    1.0 with a sunk panel and a capping rail, against the wall at +Y), a slatted deck, the mattress, a duvet with soft
    wrinkles and its top turned back, two pillows leaning on the headboard. Body wood, Accent bedding, Trim feet."""
    head = s.rbox((1.6, 0.05, 0.86), (0, 1.0, 0.55), r=0.02, seg=2)
    panel(head, (0, -1, 0), 0.4, thickness=0.07, depth=-0.008)
    cap = s.rbox((1.6, 0.07, 0.04), (0, 0.99, 0.985), r=0.018, seg=2)          # rounded capping rail on top
    foot = s.rbox((1.6, 0.04, 0.38), (0, -1.005, 0.31), r=0.015, seg=2)
    panel(foot, (0, -1, 0), 0.2, thickness=0.06, depth=-0.006)
    rails = [s.rbox((0.04, 1.97, 0.22), (x, -0.005, 0.25), r=0.01, seg=2) for x in (-0.78, 0.78)]
    deck = s.rbox((1.53, 1.95, 0.02), (0, -0.005, 0.31), r=0.004, seg=1)
    s.part("Body", [head, cap, foot, *rails, deck])

    mattress = s.rbox((1.5, 1.93, 0.234), (0, -0.01, 0.433), r=0.04, seg=3)
    s.groove(mattress, "Z", 0.5, 0.008, 0.004)

    def duvet_push(co):
        d = Vector((0, 0, 0))
        if co.z > 0:   # the top: soft lengthwise wrinkles and a sag between the sleepers
            d.z = 0.02 * math.sin(co.y * 11.0 + 0.6) * math.cos(co.x * 3.0) - 0.012 * math.cos(co.x * 4.0)
        else:          # the drape hangs a little lower at the free sides
            d.z = -0.03 * abs(co.x) / 0.8
        return d

    duvet = soft((1.6, 1.34, 0.3), (0, -0.33, 0.47), cuts=3, levels=1, fn=duvet_push)
    fold = soft((1.58, 0.3, 0.08), (0, 0.24, 0.6), cuts=2, levels=1,
                fn=lambda co: Vector((0, 0, 0.008 * math.cos(co.x * 5.0))))
    pillows = [soft((0.66, 0.42, 0.17), (x, 0.73, 0.625), cuts=2, levels=1, rot=(-16 * DEG, 0, x * 0.08))
               for x in (-0.37, 0.37)]
    s.part("Accent", [mattress, duvet, fold, *pillows])
    feet = [s.rcyl(0.03, 0.13, (x, y, 0.064), seg=14, r=0.004, bevel_seg=1, radius2=0.024)
            for x in (-0.765, 0.765) for y in (-0.995, 0.995)]
    s.part("Trim", feet)
    return export("double_bed")


def carcass(W, D, H, t=0.022, plinth=0.08, front=-0.0, y_back=None, back_t=0.012, shelves=()):
    """An open cupboard carcass for doors: two sides to the floor, a bottom over a set-back kick board, a back panel and
    shelves; the front edge of the sides at y = front. Returns (bms, inner half width)."""
    y_back = D / 2 if y_back is None else y_back
    depth = y_back - front
    yc = (front + y_back) / 2
    xi = W / 2 - t                               # inner face of the sides
    out = [s.rbox((t, depth, H), (x * (W / 2 - t / 2), yc, H / 2), r=0.003, seg=1) for x in (-1, 1)]
    out.append(s.rbox((2 * xi + 0.008, depth - 0.012, t), (0, yc - 0.004, plinth + t / 2 - 0.001), r=0.003, seg=1))
    out.append(s.rbox((2 * xi + 0.008, back_t, H - plinth - 0.01), (0, y_back - back_t / 2 - 0.003,
                                                                      plinth + (H - plinth) / 2), r=0.002, seg=1))
    out.append(s.rbox((2 * xi + 0.008, 0.02, plinth), (0, front + 0.03, plinth / 2), r=0.003, seg=1))   # kick board
    for z in shelves:
        out.append(s.rbox((2 * xi + 0.008, depth - 0.04, 0.02), (0, yc + 0.006, z), r=0.003, seg=1))
    return out, xi


def model_wardrobe():
    """Two-door wardrobe 1.2 x 0.6 x 2.1: a hollow carcass on a kick plinth with a hat shelf and a hanging rail
    (Trim) inside, a moulded cornice, two veneer doors with sunk panels (BodyDoor0 -X, BodyDoor1 +X, hinged at the
    outer edges) and long bar pulls at the meeting edges (inside the door objects, so they move with them)."""
    W, H = 1.2, 2.045
    front = -0.262
    body, xi = carcass(1.17, 0.6, H, front=front, y_back=0.292, shelves=(1.76,))
    body.append(s.rbox((1.2, 0.6, 0.06), (0, 0.0, 2.07), r=0.012, seg=2))                  # cornice
    body.append(s.rbox((1.18, 0.58, 0.026), (0, 0.0, 2.03), r=0.008, seg=2))                # its lower step
    s.part("Body", body)
    rail = [s.rcyl(0.012, 2 * xi + 0.008, (0, 0.02, 1.64), axis="X", seg=12, r=0.002, bevel_seg=1)]
    rail += [s.rcyl(0.025, 0.012, (x * (xi - 0.004), 0.02, 1.64), axis="X", seg=14, r=0.002, bevel_seg=1)
             for x in (-1, 1)]                                                                 # end sockets
    s.part("Trim", rail)
    for i, sx in enumerate((-1, 1)):
        x0, x1 = sx * 0.003, sx * (W / 2 - 0.016)
        d = s.rbox((abs(x1 - x0), 0.022, 1.925), ((x0 + x1) / 2, front - 0.009, 0.085 + 1.925 / 2), r=0.006, seg=2)
        panel(d, (0, -1, 0), 0.5, thickness=0.07, depth=-0.006)
        pull = bar_handle(sx * 0.04, 1.08, front - 0.02, 0.3, axis="Z", r=0.008, stand=0.02)
        s.part(f"BodyDoor{i}", [d, *pull])
    return export("wardrobe")


def model_dresser():
    """Chest of drawers 1.0 x 0.5 x 0.85: a top with a small overhang, an open carcass on a set-back plinth with
    dividers, three drawers (BodyDrawer0 bottom .. 2 top: hollow boxes, each front with a sunk field and a bar handle
    in the same object)."""
    top = s.rbox((1.0, 0.5, 0.03), (0, 0, 0.835), r=0.008, seg=2)
    sides = [s.rbox((0.02, 0.47, 0.825), (x, 0.005, 0.4125), r=0.003, seg=1) for x in (-0.48, 0.48)]
    back = s.rbox((0.944, 0.01, 0.76), (0, 0.234, 0.44), r=0.002, seg=1)
    dividers = [s.rbox((0.944, 0.46, 0.02), (0, 0.005, 0.08 + k * 0.243), r=0.003, seg=1) for k in range(3)]
    kick = s.rbox((0.944, 0.02, 0.07), (0, -0.19, 0.035), r=0.003, seg=1)
    s.part("Body", [top, *sides, back, *dividers, kick])
    for k in range(3):
        z0 = 0.09 + k * 0.243
        fr = s.rbox((0.932, 0.02, 0.217), (0, -0.235, z0 + 0.1115), r=0.004, seg=2)
        panel(fr, (0, -1, 0), 0.1, thickness=0.03, depth=-0.004)
        box = [s.rbox((0.868, 0.424, 0.012), (0, -0.015, z0 + 0.004), r=0.002, seg=1),          # bottom
               s.rbox((0.868, 0.012, 0.15), (0, 0.19, z0 + 0.08), r=0.002, seg=1)]                # back
        box += [s.rbox((0.012, 0.42, 0.15), (x, -0.018, z0 + 0.08), r=0.002, seg=1) for x in (-0.434, 0.434)]
        handle = bar_handle(0, z0 + 0.13, -0.245, 0.16, axis="X", r=0.007, stand=0.018)
        s.part(f"BodyDrawer{k}", [fr, *box, *handle])
    return export("dresser")


# --------------------------------------------------------------------------- bathroom

def model_bathtub():
    """Enamel bathtub 1.7 x 0.75, rim at 0.58 (Body): a cast tub with a real hollow basin, the full-size rolled lip
    over a narrower bowl on four feet, a front panel under the lip (Accent), and on the wall side (+Y) a chrome mixer
    with a swivel spout over the water, a hand shower on its hose in a wall holder, a drain (Trim)."""
    outer = s.loft([(0.16, 0.74, 0.28, 3.5), (0.3, 0.8, 0.33, 3.5), (0.5, 0.825, 0.35, 4), (0.52, 0.85, 0.375, 6),
                    (0.58, 0.85, 0.375, 6)], seg=40)
    inner = s.loft([(0.2, 0.64, 0.2, 3), (0.24, 0.7, 0.25, 3.2), (0.4, 0.77, 0.3, 3.5), (0.65, 0.797, 0.322, 3.6)],
                   seg=40)
    tub = s.boolean(outer, inner, "DIFFERENCE")
    s.bevel_edges(tub, 0.012, 2, angle=40)
    legs = [s.rbox((0.08, 0.08, 0.2), (x, y, 0.1), r=0.015, seg=2, taper=(0.8, 0.8)) for x in (-0.6, 0.6) for y in (-0.15, 0.15)]
    s.part("Body", [tub, *legs])
    front = s.rbox((1.69, 0.02, 0.535), (0, -0.36, 0.2675), r=0.006, seg=2)
    for x in (-0.42, 0.42):
        s.groove(front, "X", x, 0.006, 0.003)
    s.part("Accent", [front])

    tx, ty, tz = 0.45, 0.31, 0.78                                    # the mixer on the wall
    trim = [s.rcyl(0.022, 0.2, (tx, ty, tz), axis="X", seg=16, r=0.006, bevel_seg=2)]
    for dx in (-0.075, 0.075):
        trim.append(s.rcyl(0.012, 0.07, (tx + dx, 0.34, tz), axis="Y", seg=10, r=0.002, bevel_seg=1))   # wall pipes
        trim.append(s.rcyl(0.03, 0.008, (tx + dx, 0.37, tz), axis="Y", seg=16, r=0.002, bevel_seg=1))   # rosettes
    for sx in (-1, 1):
        hx = tx + sx * 0.11
        trim.append(s.rcyl(0.017, 0.03, (hx, ty, tz), axis="X", seg=12, r=0.004, bevel_seg=1))          # valve body
        trim.append(s.rbox((0.022, 0.075, 0.016), (hx + sx * 0.016, ty, tz), r=0.006, seg=1))           # cross head
        trim.append(s.rbox((0.022, 0.016, 0.075), (hx + sx * 0.016, ty, tz), r=0.006, seg=1))
    trim.append(s.tube([(tx, ty - 0.005, tz - 0.012), (tx, ty - 0.07, tz - 0.025), (tx, 0.13, tz - 0.045)], 0.011,
                       res=2, segments_per=6))                                                         # spout
    trim.append(s.rcyl(0.014, 0.025, (tx, 0.13, tz - 0.05), seg=12, r=0.003, bevel_seg=1))
    trim.append(s.rcyl(0.008, 0.04, (tx, ty, tz + 0.035), seg=10, r=0.002, bevel_seg=1))              # diverter
    hx, hz = 0.2, 1.17
    trim.append(s.tube([(tx, ty, tz - 0.02), (tx - 0.04, 0.33, 0.64), (0.34, 0.348, 0.586), (0.27, 0.35, 0.66),
                        (0.23, 0.35, 0.9), (hx, 0.345, hz)], 0.008, res=1, segments_per=8))   # the hose, lying on the rim
    trim.append(s.tube([(hx, 0.345, hz - 0.01), (hx, 0.342, 1.33)], 0.014, smooth=False))             # handset grip
    trim.append(s.rcyl(0.04, 0.03, (hx, 0.33, 1.355), axis="Y", seg=20, r=0.008, bevel_seg=2, rot=(18 * DEG, 0, 0)))
    trim.append(s.rbox((0.045, 0.032, 0.045), (hx, 0.359, 1.27), r=0.008, seg=1))                    # wall holder
    trim.append(s.rcyl(0.026, 0.005, (0.62, 0.0, 0.2005), seg=16, r=0.0015, bevel_seg=1))              # drain
    s.part("Trim", trim)
    return export("bathtub")


def model_home_toilet():
    """Close-coupled toilet 0.38 x 0.68: a pedestal bowl with a hollow basin, a rear shelf carrying the cistern
    (Body, to 0.815 with its lid), the seat ring and the raised lid on two hinges (Accent), a flush button (Trim)."""
    bowl_bm = s.loft([(0.0, 0.11, 0.17, 2.5, 0, 0.02), (0.15, 0.1, 0.16, 2.5, 0, 0.0), (0.28, 0.14, 0.21, 2.3, 0, -0.06),
                      (0.36, 0.175, 0.25, 2.3, 0, -0.085), (0.4, 0.18, 0.255, 2.3, 0, -0.085)], seg=32)
    basin = s.loft([(0.2, 0.05, 0.06, 2, 0, -0.11), (0.3, 0.11, 0.15, 2.2, 0, -0.1), (0.45, 0.135, 0.19, 2.2, 0, -0.1)],
                   seg=32)
    bowl_bm = s.boolean(bowl_bm, basin, "DIFFERENCE")
    s.bevel_edges(bowl_bm, 0.008, 2, angle=40)
    shelf = s.rbox((0.3, 0.19, 0.096), (0, 0.245, 0.348), r=0.02, seg=2)
    tank = s.rbox((0.37, 0.165, 0.4), (0, 0.255, 0.59), r=0.025, seg=3)
    tank_lid = s.rbox((0.38, 0.18, 0.03), (0, 0.25, 0.8), r=0.01, seg=2)
    s.part("Body", [bowl_bm, shelf, tank, tank_lid])
    seat = s.loft([(0.398, 0.178, 0.25, 2.3, 0, -0.088), (0.42, 0.178, 0.25, 2.3, 0, -0.088)], seg=32)
    seat = s.boolean(seat, s.loft([(0.38, 0.128, 0.18, 2.2, 0, -0.105), (0.44, 0.128, 0.18, 2.2, 0, -0.105)], seg=32),
                     "DIFFERENCE")
    s.bevel_edges(seat, 0.006, 2, angle=40)
    lid = s.loft([(0.0, 0.175, 0.235, 2.3), (0.018, 0.175, 0.235, 2.3)], seg=32)
    s.bevel_edges(lid, 0.006, 2, angle=40)
    s.place(lid, (0, 0.135, 0.665), (-90 * DEG, 0, 0))        # flipped up: its front edge on top
    hinges = [s.rcyl(0.012, 0.045, (x, 0.148, 0.426), axis="X", seg=12, r=0.003, bevel_seg=1) for x in (-0.08, 0.08)]
    s.part("Accent", [seat, lid, *hinges])
    s.part("Trim", [s.rcyl(0.026, 0.012, (0, 0.255, 0.817), seg=20, r=0.003, bevel_seg=1)])
    return export("home_toilet")


def model_bath_vanity():
    """Sink cabinet 0.6 x 0.46 x 0.85: an open cabinet on a set-back plinth (the trap inside) with two small doors that
    open from the middle (BodyDoor0/1, knobs at the meeting edges), a ceramic basin top with a real bowl, a single-lever
    tap and a drain (Trim); a back splash board up to a mirror cabinet on the wall at 1.2..1.9 (Body, mirror face Trim;
    it does not open: the mirror is its own material), back flush with +Y."""
    cab = s.cupboard(-0.29, 0.29, -0.19, 0.225, 0.098, 0.82, t=0.018, top=False)
    cab.append(s.rbox((0.562, 0.38, 0.018), (0, 0.02, 0.722), r=0.003, seg=1))            # the inside's ceiling
    plinth = s.rbox((0.54, 0.38, 0.1), (0, 0.03, 0.05), r=0.004, seg=1)
    for i, x in enumerate((-0.143, 0.143)):
        d = s.rbox((0.282, 0.018, 0.605), (x, -0.196, 0.4225), r=0.006, seg=2)
        panel(d, (0, -1, 0), 0.08, thickness=0.035, depth=-0.004)
        s.part(f"BodyDoor{i}", [d, *knob(0.04 if x > 0 else -0.04, -0.205, 0.66, r=0.012)])
    basin = s.rbox((0.6, 0.46, 0.12), (0, 0.0, 0.79), r=0.02, seg=3)
    basin = s.boolean(basin, s.loft([(0.76, 0.14, 0.09, 2.5, 0, -0.03), (0.79, 0.2, 0.135, 3, 0, -0.03),
                                     (0.9, 0.228, 0.158, 3.5, 0, -0.03)], seg=32), "DIFFERENCE")
    s.bevel_edges(basin, 0.008, 2, angle=40)
    splash = s.rbox((0.592, 0.012, 0.42), (0, 0.223, 1.0), r=0.004, seg=1)
    mirror_cab = s.rbox((0.6, 0.13, 0.7), (0, 0.165, 1.55), r=0.012, seg=2)
    shelf = s.rbox((0.56, 0.06, 0.018), (0, 0.075, 1.195), r=0.005, seg=1)       # the little shelf under the mirror
    s.part("Body", [*cab, plinth, basin, splash, mirror_cab, shelf])
    trim = [s.rbox((0.556, 0.006, 0.62), (0, 0.098, 1.56), r=0.003, seg=1)]                 # the mirror face
    trim.append(s.tube([(0, -0.03, 0.735), (0, -0.03, 0.58), (0, 0.03, 0.52), (0, 0.09, 0.58), (0, 0.12, 0.6),
                        (0, 0.215, 0.6)], 0.016, res=1, segments_per=6))                    # the trap inside
    trim.append(s.lathe([(0.0, 0.847), (0.024, 0.847), (0.022, 0.86), (0.018, 0.94), (0.0, 0.945)], seg=16,
                        loc=(0, 0.175, 0)))                                                  # tap body
    trim.append(s.tube([(0, 0.17, 0.925), (0, 0.11, 0.93), (0, 0.075, 0.905)], 0.01, res=2, segments_per=6))
    trim.append(s.rbox((0.018, 0.075, 0.014), (0, 0.2, 0.955), r=0.006, seg=1, rot=(12 * DEG, 0, 0)))  # lever
    trim.append(s.rcyl(0.02, 0.004, (0, -0.03, 0.7615), seg=16, r=0.001, bevel_seg=1))       # drain
    s.part("Trim", trim)
    return export("bath_vanity")


def model_washing_machine():
    """Front-loading washing machine 0.6 x 0.6 x 0.85: a white box (Body) with a plinth seam, a control strip with the
    detergent drawer (Accent), a programme knob and buttons (Trim), a small display (Eye), the round door: a chrome
    ring with its handle (Trim) and a domed glass porthole (TrimGlass)."""
    box = s.rbox((0.596, 0.576, 0.835), (0, 0.012, 0.4325), r=0.015, seg=2)
    s.groove(box, "Z", 0.07, 0.006, 0.003)
    s.groove(box, "Z", 0.705, 0.005, 0.003)
    s.part("Body", [box])
    strip = s.rbox((0.58, 0.02, 0.12), (0, -0.28, 0.775), r=0.008, seg=2)
    drawer = s.rbox((0.17, 0.02, 0.09), (-0.19, -0.29, 0.775), r=0.008, seg=2)
    s.groove(drawer, "Z", 0.762, 0.012, 0.004)
    feet = [s.rcyl(0.025, 0.02, (x, y, 0.01), seg=12, r=0.004, bevel_seg=1) for x in (-0.25, 0.25) for y in (-0.22, 0.24)]
    ring2 = flat_ring(0.235, 0.19, 0.008, (0, -0.276, 0.39), seg=36)                    # a grey bezel on the box
    s.part("Accent", [strip, drawer, *feet, ring2])
    trim = [flat_ring(0.205, 0.138, 0.026, (0, -0.287, 0.39), seg=36, rad=0.006)]
    trim.append(s.rbox((0.03, 0.03, 0.07), (0.19, -0.29, 0.39), r=0.012, seg=2))         # door handle
    trim.append(s.rcyl(0.032, 0.022, (0.2, -0.293, 0.775), axis="Y", seg=20, r=0.006, bevel_seg=2))   # knob
    trim.append(s.rbox((0.008, 0.008, 0.024), (0.2, -0.306, 0.79), r=0.002, seg=1))       # its pointer
    trim += [s.rcyl(0.009, 0.01, (x, -0.29, 0.745), axis="Y", seg=10, r=0.002, bevel_seg=1) for x in (0.02, 0.06, 0.1)]
    s.part("Trim", trim)
    s.part("TrimGlass", [s.lathe([(0.145, 0.0), (0.14, 0.01), (0.1, 0.02), (0.0, 0.024)], seg=40, axis="-Y",
                                 loc=(0, -0.274, 0.39))])
    s.part("Eye", [s.rbox((0.09, 0.006, 0.035), (0.06, -0.291, 0.79), r=0.003, seg=1)])
    return export("washing_machine")


# --------------------------------------------------------------------------- hallway

def model_shoe_cabinet():
    """Hallway shoe cabinet 0.9 x 0.32 x 0.95: a carcass with a shelf inside on a set-back plinth, a top with a soft
    edge, two doors BodyDoor0 (-X) and BodyDoor1 (+X) with knobs at the meeting edges (in the door objects), a pair of
    shoes on top (Accent)."""
    front = -0.14
    body, xi = carcass(0.9, 0.32, 0.925, front=front, y_back=0.155, shelves=(0.5,))
    body.append(s.rbox((0.9, 0.32, 0.03), (0, 0.0, 0.935), r=0.01, seg=2))
    s.part("Body", body)
    for i, sx in enumerate((-1, 1)):
        x0, x1 = sx * 0.003, sx * 0.448
        d = s.rbox((abs(x1 - x0), 0.02, 0.83), ((x0 + x1) / 2, front - 0.008, 0.085 + 0.415), r=0.005, seg=2)
        panel(d, (0, -1, 0), 0.2, thickness=0.05, depth=-0.005)
        s.part(f"BodyDoor{i}", [d, *knob(sx * 0.04, front - 0.018, 0.82, r=0.013)])
    shoes = []
    for k, x in enumerate((0.17, 0.29)):
        rings = [(-0.135, 0.02, 0.012, 2.2, 0, -0.012), (-0.12, 0.035, 0.018, 3, 0, -0.018), (-0.08, 0.042, 0.025, 3, 0, -0.025),
                 (0.0, 0.045, 0.035, 3, 0, -0.035), (0.08, 0.045, 0.05, 3, 0, -0.05), (0.125, 0.04, 0.046, 3, 0, -0.046)]
        shoe = s.loft(rings, seg=16, axis="Y")
        s.place(shoe, (x, 0.0 + 0.01 * k, 0.947), (0, 0, (k - 0.5) * 8 * DEG))
        sole = s.rbox((0.094, 0.27, 0.012), (x, 0.0 + 0.01 * k, 0.953), r=0.005, seg=1, rot=(0, 0, (k - 0.5) * 8 * DEG))
        shoes += [shoe, sole]
    s.part("Accent", shoes)
    return export("shoe_cabinet")


def coat(x, y, z0, half_w, half_d, sleeves=True):
    """A coat hanging from a hook at about 1.705 m: a flattened loft from the hem to the collar loop, two sleeves."""
    rings = [(z0, half_w * 1.08, half_d, 2.6, x, y), (z0 + 0.3, half_w * 1.0, half_d * 0.95, 2.6, x, y),
             (1.45, half_w * 0.96, half_d * 0.88, 2.6, x, y), (1.55, half_w * 0.92, half_d * 0.82, 2.6, x, y),
             (1.61, half_w * 0.68, half_d * 0.7, 2.6, x, y + 0.005), (1.655, half_w * 0.32, half_d * 0.55, 2.4, x, y + 0.01),
             (1.69, 0.035, 0.03, 2.2, x, y + 0.012), (1.712, 0.02, 0.018, 2, x, y + 0.012)]
    out = [s.loft(rings, seg=20)]
    if sleeves:
        for sx in (-1, 1):
            out.append(s.tube([(x + sx * half_w * 0.8, y - half_d * 0.4, 1.53), (x + sx * half_w * 0.9, y - half_d * 0.75, 1.3),
                               (x + sx * half_w * 0.86, y - half_d * 0.85, 1.06)], 0.045, res=2, segments_per=4,
                              radii=[1.0, 0.95, 0.85]))
    return out


def model_hall_hooks():
    """Wall coat rack 1.0 wide, 0.35 deep: a board with six double hooks at 1.7 (Trim), a hat shelf at 1.9 on two
    side brackets (Body), two coats and a bag hanging down to about 0.9 (Accent). Back flush with +Y."""
    yb = 0.175
    board = s.rbox((1.0, 0.025, 0.2), (0, yb - 0.0125, 1.7), r=0.008, seg=2)
    shelf = s.rbox((1.0, 0.3, 0.025), (0, yb - 0.15, 1.9), r=0.008, seg=2)
    brackets = [prism([(yb - 0.003, 1.6), (yb - 0.003, 1.895), (-0.11, 1.895), (-0.11, 1.875), (yb - 0.03, 1.6)],
                      "X", x0, x0 + 0.022) for x0 in (-0.497, 0.475)]
    for b in brackets:
        s.bevel_edges(b, 0.004, 1, angle=30)
    s.part("Body", [board, shelf, *brackets])
    hooks = []
    for x in (-0.4, -0.24, -0.08, 0.08, 0.24, 0.4):
        hooks.append(s.rcyl(0.018, 0.008, (x, yb - 0.027, 1.72), axis="Y", seg=12, r=0.002, bevel_seg=1))
        hooks.append(s.tube([(x, yb - 0.028, 1.72), (x, 0.09, 1.705), (x, 0.07, 1.715), (x, 0.065, 1.745)], 0.006,
                            res=1, segments_per=3))
        hooks.append(s.tube([(x, yb - 0.028, 1.725), (x, 0.11, 1.77), (x, 0.1, 1.8)], 0.005, res=1, segments_per=3))
    s.part("Trim", hooks)
    acc = coat(-0.24, 0.085, 0.9, 0.23, 0.085)
    acc += coat(0.08, 0.06, 1.0, 0.2, 0.075)
    bag = s.rbox((0.2, 0.09, 0.24), (0.4, 0.07, 1.2), r=0.03, seg=3, taper=(0.85, 0.9))
    flap = s.rbox((0.19, 0.012, 0.1), (0.4, 0.024, 1.27), r=0.005, seg=1)
    strap = s.tube([(0.32, 0.07, 1.3), (0.36, 0.08, 1.55), (0.4, 0.085, 1.712), (0.44, 0.08, 1.55), (0.48, 0.07, 1.3)],
                   0.008, res=1, segments_per=6)
    s.part("Accent", [*acc, bag, flap, strap])
    return export("hall_hooks")


# --------------------------------------------------------------------------- stairwell

def model_mailboxes():
    """Lobby post boxes 1.2 x 0.25 from 0.9 to 1.8 high: a painted steel cabinet (Body) with a 6 x 3 grid of small
    doors, each with a letter slot, a number plate and a keyhole (Trim). Back flush with +Y."""
    yb = 0.125
    cab = s.rbox((1.196, 0.24, 0.875), (0, yb - 0.12, 1.3375), r=0.01, seg=2)
    cap = s.rbox((1.2, 0.25, 0.03), (0, yb - 0.125, 1.785), r=0.01, seg=2)         # a lip along the top
    body = [cab, cap]
    trim = []
    yf = yb - 0.24                                                                   # the cabinet's front face
    for c in range(6):
        for r_ in range(3):
            x = -0.475 + c * 0.19
            z = 0.9 + 0.03 + 0.14 + r_ * 0.28
            d = s.rbox((0.18, 0.012, 0.27), (x, yf - 0.004, z), r=0.004, seg=1)
            d = s.boolean(d, s.rbox((0.12, 0.012, 0.014), (x, yf - 0.012, z + 0.07), r=0), "DIFFERENCE")
            s.bevel_edges(d, 0.0015, 1, angle=50)
            body.append(d)
            trim.append(s.rbox((0.045, 0.004, 0.024), (x, yf - 0.0105, z + 0.01), r=0.0012, seg=1))
            trim.append(s.rcyl(0.008, 0.006, (x + 0.06, yf - 0.011, z - 0.07), axis="Y", seg=10, r=0.0015, bevel_seg=1))
    s.part("Body", body)
    s.part("Trim", trim)
    return export("mailboxes")


def model_elevator_doors():
    """Closed lift doors on a landing wall: a steel portal 1.3 x 2.2, 0.12 deep (Body: wide jambs, a header, the
    threshold), two leaves with a sunk panel and the meeting gap (Accent), set 6 cm back in the portal; a call plate
    with one button on the right jamb and a floor indicator over the doors (plates Trim, button and display Eye).
    Back flush with +Y."""
    D = 0.12
    jambs = [s.rbox((0.2, D, 2.2), (x, 0, 1.1), r=0.012, seg=2) for x in (-0.55, 0.55)]
    header = s.rbox((0.92, D, 0.2), (0, 0, 2.1), r=0.012, seg=2)
    sill = s.rbox((0.92, 0.1, 0.012), (0, -0.01, 0.006), r=0.003, seg=1)
    s.part("Body", [*jambs, header, sill])
    leaves = []
    for sx in (-1, 1):
        lf = s.rbox((0.462, 0.03, 2.0), (sx * 0.235, 0.015, 1.004), r=0.004, seg=1)
        panel(lf, (0, -1, 0), 0.5, thickness=0.06, depth=-0.004)
        leaves.append(lf)
    s.part("Accent", leaves)
    s.part("Trim", [s.rbox((0.075, 0.012, 0.15), (0.55, -0.064, 1.05), r=0.005, seg=1),
                    s.rbox((0.26, 0.012, 0.09), (0, -0.064, 2.1), r=0.005, seg=1)])
    s.part("Eye", [s.rcyl(0.016, 0.01, (0.55, -0.072, 1.05), axis="Y", seg=16, r=0.003, bevel_seg=1),
                   s.rbox((0.19, 0.006, 0.05), (0, -0.0715, 2.1), r=0.003, seg=1)])
    return export("elevator_doors")


# --------------------------------------------------------------------------- the flat's entry door leaf

def quilted(x0, x1, z0, z1, y_base, y_stud, y_puff, nx, nz):
    """A padded dermantin field on the +Y face: a closed slab whose front is a grid of puffs (y_puff) pinned by studs
    (y_stud) on a diamond lattice; each cell splits along its stud diagonal, so the valleys run stud to stud.
    Returns (bmesh, stud points)."""
    bm = bmesh.new()
    front, back = {}, {}
    studs = []
    for i in range(nx + 1):
        for j in range(nz + 1):
            x = x0 + (x1 - x0) * i / nx
            z = z0 + (z1 - z0) * j / nz
            edge = i in (0, nx) or j in (0, nz)
            stud = (i + j) % 2 == 0
            y = y_stud if (stud or edge) else y_puff
            front[i, j] = bm.verts.new((x, y, z))
            back[i, j] = bm.verts.new((x, y_base, z))
            if stud and not edge:
                studs.append((x, y_stud, z))
    for i in range(nx):
        for j in range(nz):
            a, b, c, d = front[i, j], front[i + 1, j], front[i + 1, j + 1], front[i, j + 1]
            if (i + j) % 2 == 0:       # studs at (i, j) and (i+1, j+1): split along that diagonal
                bm.faces.new((a, b, c))
                bm.faces.new((a, c, d))
            else:
                bm.faces.new((a, b, d))
                bm.faces.new((b, c, d))
            bm.faces.new((back[i, j], back[i, j + 1], back[i + 1, j + 1], back[i + 1, j]))
    ring = [(i, 0) for i in range(nx)] + [(nx, j) for j in range(nz)] + [(i, nz) for i in range(nx, 0, -1)] + \
           [(0, j) for j in range(nz, 0, -1)]
    for k, p in enumerate(ring):
        q = ring[(k + 1) % len(ring)]
        bm.faces.new((front[p], back[p], back[q], front[q]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, studs


def model_flat_leaf():
    """The flat's entry door leaf 0.9 x 2.28, about 7 cm deep: a steel core with a raised edge frame (BodyFrame), on
    both faces a quilted dermantin field with a diamond pattern of upholstery studs (Body, studs TrimStuds), a peephole,
    a lever handle on a long escutcheon with its keyhole and a round escutcheon for the upper lock near the meeting
    edge (+X) (Accent), a small brass flat number plate (Trim). Both faces alike."""
    W, H, T = 0.9, 2.28, 0.03
    bar, proud = 0.05, 0.016
    steel = [s.rbox((W, T, H), (0, 0, H / 2), r=0.004, seg=1)]
    pad, studs, acc, trim = [], [], [], []
    for side in (-1, 1):
        y = side * (T / 2 + proud / 2 - 0.001)
        steel += [s.rbox((W, proud, bar), (0, y, z), r=0.006, seg=2) for z in (bar / 2, H - bar / 2)]
        steel += [s.rbox((bar, proud, H - 2 * bar + 0.01), (x * (W / 2 - bar / 2), y, H / 2), r=0.006, seg=2)
                  for x in (-1, 1)]
        q, pts = quilted(-W / 2 + bar - 0.006, W / 2 - bar + 0.006, bar - 0.006, H - bar + 0.006,
                         T / 2 - 0.002, T / 2 + 0.003, T / 2 + 0.0145, 6, 16)
        st = [s.lathe([(0.0, -0.003), (0.0085, -0.003), (0.0065, 0.0025), (0.0, 0.0045)], seg=7,
                      axis="Y", loc=p) for p in pts]
        if side < 0:
            q = flip_y(q)
            st = [flip_y(b) for b in st]
        pad.append(q)
        studs += st
        f = side
        yf = T / 2 + 0.002                                   # parts start inside the core and pass through the padding
        hx = W / 2 - 0.1
        plate = s.rbox((0.05, 0.022, 0.24), (hx, f * (yf + 0.011 - 0.004), 1.02), r=0.008, seg=2)
        lever = s.tube([(hx, f * (yf + 0.03), 1.08), (hx, f * (yf + 0.06), 1.08), (hx - 0.03, f * (yf + 0.068), 1.08),
                        (hx - 0.12, f * (yf + 0.066), 1.075)], 0.01, res=2, segments_per=4)
        rose = s.rcyl(0.016, 0.035, (hx, f * (yf + 0.033), 1.08), axis="Y", seg=14, r=0.004, bevel_seg=1)
        key = s.rbox((0.008, 0.004, 0.022), (hx, f * (yf + 0.019), 0.96), r=0.002, seg=1)
        upper = s.rcyl(0.026, 0.02, (hx, f * (yf + 0.006), 1.3), axis="Y", seg=18, r=0.005, bevel_seg=1)
        cyl = s.rcyl(0.011, 0.006, (hx, f * (yf + 0.017), 1.3), axis="Y", seg=12, r=0.002, bevel_seg=1)
        peep = s.rcyl(0.013, 0.026, (0, f * (yf + 0.009), 1.55), axis="Y", seg=14, r=0.003, bevel_seg=1)
        lens = s.rcyl(0.007, 0.006, (0, f * (yf + 0.022), 1.55), axis="Y", seg=12, r=0.001, bevel_seg=1)
        acc += [plate, lever, rose, key, upper, cyl, peep, lens]
        trim.append(s.rbox((0.09, 0.018, 0.055), (0, f * (yf + 0.011 - 0.002), 1.72), r=0.006, seg=2))
    s.part("BodyFrame", steel)
    s.part("Body", pad)
    s.part("TrimStuds", studs)
    s.part("Accent", acc)
    s.part("Trim", trim)
    return export("flat_leaf", folder="Level", shift=B_SHIFT)


# --------------------------------------------------------------------------- the shelter map

def paper_layer(w, d, t, z, cx=0.0, bow=0.002, n=4, seg=14):
    """One folded panel of paper lying flat: a thin loft along Y (rounded fold edges at +-X) whose ends curl up by bow."""
    rings = []
    for k in range(n):
        y = -d / 2 + d * k / (n - 1)
        lift = bow * (2 * y / d) ** 2
        rings.append((y, w / 2, t / 2, 7, cx, -(z + t / 2 + lift)))
    return s.loft(rings, seg=seg, axis="Y")


def band(size, loc, wall, axis_open):
    """A thin string band round a block: a rounded box minus its inside (open along axis_open)."""
    outer = s.rbox(size, loc, r=min(size) * 0.3, seg=1)
    hole = [c - 2 * wall for c in size]
    hole["XYZ".index(axis_open)] = size["XYZ".index(axis_open)] + 0.02
    return s.boolean(outer, s.rbox(tuple(hole), loc, r=0), "DIFFERENCE")


def model_item_map_model():
    """The shelter map folded into a packet 0.30 x 0.22 x ~0.024, lying flat along X: five panels of aged paper with
    rounded fold edges stepping at the sides, the ends curling up a little (Body); a string tied across both ways with
    a knot (Accent) and a red wax seal on the knot (Trim). Origin on the floor under its middle."""
    W, D = 0.30, 0.22
    layers = []
    specs = ((0.0, 0.0), (0.004, -0.0015), (-0.002, 0.002), (0.005, -0.001), (0.0, 0.0025))
    for k, (dw, cx) in enumerate(specs):
        layers.append(paper_layer(W - 0.006 - dw, D - 0.004 * (k % 2), 0.005, 0.0042 * k, cx=cx))
    s.part("Body", layers)
    top = 0.0042 * 4 + 0.005
    zc = top / 2 + 0.0014                                            # the strings run under the packet too
    sx, sy = 0.06, -0.035                                             # where the strings cross
    strings = [band((0.006, D + 0.002, top + 0.0028), (sx, 0, zc), 0.0016, "X"),
               band((W + 0.002, 0.006, top + 0.0028), (0, sy, zc), 0.0016, "Y")]
    strings.append(s.ball(0.008, (sx, sy, top + 0.003), seg=10, scale=(1, 1, 0.6)))           # the knot
    for a in (-1, 1):
        strings.append(s.tube([(sx, sy, top + 0.003), (sx + a * 0.018, sy + 0.012, top + 0.004),
                               (sx + a * 0.03, sy - 0.004, top + 0.003), (sx + a * 0.006, sy - 0.003, top + 0.002)],
                              0.0018, res=0, segments_per=4))                                 # the bow's loops
        strings.append(s.tube([(sx, sy, top + 0.002), (sx + a * 0.01, sy - 0.03, top + 0.0012)], 0.0016, smooth=False))
    s.part("Accent", strings)
    s.part("Trim", [s.rcyl(0.013, 0.0035, (sx - 0.002, sy + 0.001, top + 0.0055), seg=14, r=0.0012, bevel_seg=1)])
    return export("item_map_model", folder="Items")


# held_map: the sheet in its own plane (u along X, v up, w toward the viewer), tilted back at the end
SHEET_W, SHEET_H, THICK = 0.62, 0.46, 0.0016
TILT = 20 * DEG


def sheet_w(u, v):
    """How far the sheet stands out toward the viewer at (u, v): the outer panels pulled toward the hands (valley
    creases at u = +-W/6), a valley along the middle fold v = 0, a gentle symmetric wave."""
    a = abs(u)
    c = SHEET_W / 6
    if a <= c:          # the middle panel bows away a little, the outer panels fold toward the hands
        w = -0.006 + 0.012 * (a / c)
    else:
        w = 0.006 + 0.024 * (a - c) / (SHEET_W / 2 - c)
    f = abs(v) / (SHEET_H / 2)  # the horizontal fold: a valley along the middle, top and bottom panels turned in
    w += -0.012 * (1 - f) + 0.004 * (1 - f) ** 2
    w += 0.0018 * math.cos(u * 14.0) * math.sin(v * 9.0 + 0.4)
    return w


def _jag(i, j):
    """Deterministic small in-plane nibbles along the paper's edge (worn edges), metres."""
    h = math.sin(i * 12.9898 + j * 78.233) * 43758.5453
    return (h - math.floor(h)) * 0.003


def sheet_mesh(nu=24, nv=18):
    bm = bmesh.new()
    hw, hh = SHEET_W / 2, SHEET_H / 2
    fr, bk = {}, {}
    torn = {(nu, 0): 0.013, (0, nv): 0.012, (nu, nv): 0.008, (0, 0): 0.01}  # corner: how far it is worn / torn off
    for i in range(nu + 1):
        for j in range(nv + 1):
            u = -hw + SHEET_W * i / nu
            v = -hh + SHEET_H * j / nv
            if i in (0, nu) or j in (0, nv):
                du = (1 if i == 0 else -1 if i == nu else 0) * _jag(i, j)
                dv = (1 if j == 0 else -1 if j == nv else 0) * _jag(j, i)
                u, v = u + du, v + dv
            if (i, j) in torn:
                cut = torn[i, j]
                u -= math.copysign(cut, u)
                v -= math.copysign(cut, v)
            w = sheet_w(u, v)
            fr[i, j] = bm.verts.new((u, w + THICK / 2, v))
            bk[i, j] = bm.verts.new((u, w - THICK / 2, v))
    for i in range(nu):
        for j in range(nv):
            bm.faces.new((fr[i, j], fr[i + 1, j], fr[i + 1, j + 1], fr[i, j + 1]))
            bm.faces.new((bk[i, j + 1], bk[i + 1, j + 1], bk[i + 1, j], bk[i, j]))
    ring = [(i, 0) for i in range(nu)] + [(nu, j) for j in range(nv)] + [(i, nv) for i in range(nu, 0, -1)] + \
           [(0, j) for j in range(nv, 0, -1)]
    for k, p in enumerate(ring):
        q = ring[(k + 1) % len(ring)]
        bm.faces.new((fr[p], bk[p], bk[q], fr[q]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def ink_frame(inset=0.02, width=0.006, step=0.013):
    """The thin printed border on the viewer's face, inset from the edges: a closed strip following the sheet's surface
    0.7 mm into the paper up to 1.2 mm out of it."""
    hw, hh = SHEET_W / 2 - inset, SHEET_H / 2 - inset
    corners = [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)]
    pts, ins = [], []
    for k in range(4):
        a, b = Vector(corners[k]), Vector(corners[(k + 1) % 4])
        n = max(2, int(round((b - a).length / step)))
        for m in range(n):
            pts.append(a.lerp(b, m / n))
    for p in pts:   # inward offset: toward the middle, along the axis of the side the point lies on
        dx = width if abs(abs(p.x) - hw) < 1e-6 and p.x > 0 else -width if abs(abs(p.x) - hw) < 1e-6 else 0
        dy = width if abs(abs(p.y) - hh) < 1e-6 and p.y > 0 else -width if abs(abs(p.y) - hh) < 1e-6 else 0
        ins.append(p - Vector((dx, dy)))
    bm = bmesh.new()
    loops = []
    for p, q in zip(pts, ins):
        quad = []
        for c in (p, q):
            w = sheet_w(c.x, c.y) + THICK / 2
            quad.append(bm.verts.new((c.x, w - 0.0007, c.y)))
            quad.append(bm.verts.new((c.x, w + 0.0012, c.y)))
        loops.append(quad)          # outer-low, outer-high, inner-low, inner-high
    n = len(loops)
    for k in range(n):
        A, B = loops[k], loops[(k + 1) % n]
        for i0, i1 in ((0, 1), (1, 3), (3, 2), (2, 0)):
            bm.faces.new((A[i0], B[i0], B[i1], A[i1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def hand_left(vg=-0.06):
    """The left hand pinching the sheet's left edge (u = -W/2) at height vg, in sheet space: four fingers behind the
    sheet (toward -w), the thumb on the viewer's face, the palm and the ball of the thumb past the edge."""
    eu = -SHEET_W / 2
    out = []
    for k, dv in enumerate((0.036, 0.013, -0.01, -0.033)):
        v = vg + dv
        r = 0.0088 - 0.0006 * k
        pts = []
        for u in (eu - 0.04, eu - 0.012, eu + 0.012, eu + 0.03 - 0.004 * k):
            pts.append((u, sheet_w(max(u, eu), v) - THICK / 2 - r + 0.0012 - (0.012 if u < eu - 0.02 else 0.0), v))
        out.append(s.tube(pts, r, res=2, segments_per=4))
    pw = sheet_w(eu, vg)
    out.append(s.rbox((0.045, 0.034, 0.1), (eu - 0.048, pw - 0.012, vg - 0.004), r=0.015, seg=3, rot=(0, 0, -12 * DEG)))
    r = 0.0105
    tpts = [(eu - 0.045, pw + 0.008, vg - 0.03), (eu - 0.018, pw + THICK / 2 + r - 0.001, vg - 0.012),
            (eu + 0.006, sheet_w(eu + 0.006, vg) + THICK / 2 + r - 0.0012, vg + 0.004),
            (eu + 0.022, sheet_w(eu + 0.022, vg) + THICK / 2 + r - 0.0014, vg + 0.014)]
    out.append(s.tube(tpts, r, res=2, segments_per=4, radii=[1.25, 1.1, 1.0, 0.9]))
    out.append(s.ball(0.022, (eu - 0.05, pw - 0.002, vg - 0.04), seg=14, scale=(1.0, 0.9, 1.15)))   # ball of the thumb
    return out


def model_held_map():
    """What the player sees holding the shelter map open in front of them: an unfolded blank sheet 0.62 x 0.46 tilted
    back 20 degrees, its face to the viewer (+Y), with its 3 x 2 fold creases, worn and torn corners (Body, both
    faces) and a thin dark ink frame inset 2 cm (Trim); two hands pinch its left and right edges a little below the
    middle (AccentHand), forearms in sleeves (AccentSleeve) go down and back toward the viewer. Origin: the sheet's
    centre."""
    rot = Matrix.Rotation(TILT, 4, "X")
    paper = transformed([sheet_mesh()], rot)
    ink = transformed([ink_frame()], rot)
    hands = transformed(hand_left(), rot)
    # the forearm and sleeve in view space, from the wrist (just under the palm) down and back to the elbow
    pw = sheet_w(-SHEET_W / 2, -0.06)
    inside = rot @ Vector((-SHEET_W / 2 - 0.048, pw - 0.012, -0.06 - 0.02))      # in the palm
    wrist = rot @ Vector((-SHEET_W / 2 - 0.052, pw - 0.014, -0.06 - 0.075))
    elbow = Vector((-0.22, wrist.y + 0.24, wrist.z - 0.2))
    mid = wrist.lerp(elbow, 0.45) + Vector((-0.02, 0, 0))
    arm = s.tube([inside, wrist, mid, elbow], 0.028, res=4, segments_per=6, radii=[0.9, 1.0, 1.15, 1.3])
    hands.append(arm)
    cuff0 = wrist.lerp(elbow, 0.2)
    sleeve = [s.tube([cuff0, mid, elbow, elbow + (elbow - mid).normalized() * 0.08], 0.046, res=4, segments_per=6,
                     radii=[1.0, 1.05, 1.12, 1.15]),
              s.tube([cuff0 - (elbow - cuff0).normalized() * 0.008, cuff0 + (elbow - cuff0).normalized() * 0.03],
                     0.05, res=3, segments_per=2)]                                                   # the cuff
    right_hands = [s.mirror_x(b, keep=False)[0] for b in [h.copy() for h in hands]]
    right_sleeve = [s.mirror_x(b, keep=False)[0] for b in [h.copy() for h in sleeve]]
    s.part("Body", paper)
    s.part("Trim", ink)
    s.part("AccentHand", hands + right_hands)
    s.part("AccentSleeve", sleeve + right_sleeve)
    return export("held_map", floor=False)


MODELS = {
    "double_bed": model_double_bed,
    "wardrobe": model_wardrobe,
    "dresser": model_dresser,
    "bathtub": model_bathtub,
    "home_toilet": model_home_toilet,
    "bath_vanity": model_bath_vanity,
    "washing_machine": model_washing_machine,
    "shoe_cabinet": model_shoe_cabinet,
    "hall_hooks": model_hall_hooks,
    "mailboxes": model_mailboxes,
    "elevator_doors": model_elevator_doors,
    "flat_leaf": model_flat_leaf,
    "item_map_model": model_item_map_model,
    "held_map": model_held_map,
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
