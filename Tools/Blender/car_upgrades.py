"""Upgrades the team builds into the pickup's cargo bed (car.py): the storage robot's kennel and the bed shelves.

  car_kennel.fbx   a dog house for the hermit-crab storage robot (a grey crate 0.5 w x 0.52 d x 0.62 h) in the bed's
                   rear-left corner: plank walls with a dark painted lining inside (the doorway reads as a hole), a
                   pitched roof of painted corrugated steel with a ridge cap and light barge boards, overhanging the door;
                   an arched door facing the tailgate (+Y) with a light trim and a steel threshold ramp, a bone-shaped
                   red name plate on the trim's crown, a caged lamp on the front corner, a patch on the bed-side wall, two
                   red ratchet straps over the roof and down the walls holding it to the bed.
  car_shelves.fbx  shelves along the right inner wall in three upgrade levels, each level its own objects (the game shows
                   1, 2 or 3 of them; names contain Shelf1 / Shelf2 / Shelf3): a low shelf on brackets (level 1), a second
                   shelf at the wall top (level 2), a raised rack above the wall with a top shelf and a tie-down rail
                   (level 3).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/car_upgrades.py -- Assets/_Game/Art/Models
      (writes <root>/Car/car_kennel.fbx and <root>/Car/car_shelves.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/car_upgrades.py -- <scratch> --review <sheet.png>
      (the car with the kennel and shelf level 1, the car with everything, the upgrades alone; read from <scratch>/Car
      and Assets/_Game/Art/Models/Car/pickup.fbx)

Same frame as car.py and car_bench.py, so both models drop in as children of the car at its origin: metres, Blender
front -Y = Unity +Z, z up, origin on the ground in the middle of the wheelbase. The bed: floor top z 0.85, inside
x -0.85..0.85, walls to z 1.30 (rail caps x 0.845..0.95 up to z 1.33), the tailgate's inner face at y 2.36; the bed
workbench (car_bench.py) takes y 0.36..0.91 at the front.
  kennel:   x -0.84..-0.06, y 1.55..2.35, z 0.85..1.75; walls y 1.585..2.27; clear inside 0.64 w x 0.63 d, 0.687 h
            over the middle 0.62 m (floor top 0.868, roof underside 1.70 at the ridge falling at 25 deg); door 0.54 w,
            top 1.55 with 13 cm corners (the crate's top corners at x +-0.25, z 1.49 clear them)
  shelves:  x 0.58..0.85, y 0.97..2.33; level 1 top z 1.06, level 2 top z 1.30, level 3 top shelf z 1.625, rail to z 1.70
Parts (the game colours car parts by name prefix): Body painted steel (also the kennel's lining), Accent wood, AccentBoard light board, Trim bare
steel, TrimHandle red grips / straps, EyeLamp the glowing bulb. Every object is <Prefix>_<Thing>.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

FLOOR = 0.85               # bed floor top (car.BED_FLOOR_Z)
WALL_X = 0.85              # inner face of the bed's side walls
WALL_TOP = 1.30

# kennel
KX, KY = -0.45, 1.95       # centre of its footprint
HALF_X, HALF_Y = 0.39, 0.40  # footprint (roof overhang): x -0.84..-0.06, y 1.55..2.35
WX = 0.35                  # walls' outer faces at KX +- WX
WY0, WY1 = 1.585, 2.27     # rear and front (door) walls' outer faces: the roof overhangs the door by 8 cm
WT = 0.025                 # wall thickness
KFLOOR = 0.868             # the kennel's floor top
PITCH = math.radians(25)
RIDGE_IN = 1.70            # roof underside at the ridge
ROOF_T = 0.03
DOOR_W, DOOR_TOP, DOOR_R = 0.54, 1.55, 0.13  # the robot's crate (0.5 x 0.62) clears the rounded top corners
LINING = 0.007             # the painted lining inside the walls (dark: the doorway reads as a hole)


def roof_under(d):
    """Height of the roof's underside at distance d from the ridge (across x)."""
    return RIDGE_IN - d * math.tan(PITCH)


def prism_y(pts, y0, y1, r=0.004):
    """A polygon in the xz plane (counter-clockwise seen from -Y) extruded from y0 to y1, its edges chamfered."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in pts]
    b = [bm.verts.new((x, y1, z)) for x, z in pts]
    n = len(pts)
    bm.faces.new(a[::-1])
    bm.faces.new(b)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if r > 0:
        s.bevel_edges(bm, r, 1, angle=30)
    return bm


def planks(bm, z0, z1, step=0.112):
    """Horizontal plank seams round a wall."""
    z = z0 + step
    while z < z1 - 0.03:
        s.groove(bm, "Z", z, 0.008, 0.004)
        z += step
    return bm


def slope_box(size, d, y, z_under, sx, down=0.0, r=None):
    """A box lying along one roof slope (size along the slope, across y, thick), its top face at the height
    z_under(d) - down at distance d from the ridge on side sx."""
    cs, sn = math.cos(PITCH), math.sin(PITCH)
    b = s.rbox(size, (0, 0, 0), r=min(0.004, 0.3 * min(size)) if r is None else r, seg=1)
    t = size[2]
    s.place(b, (KX + sx * (d - 0.5 * t * sn), y, z_under(d) - down - 0.5 * t * cs), (0, sx * PITCH, 0))
    return b


# --------------------------------------------------------------------------- kennel

def door_cutter(margin=0.0, y=WY1, depth=0.2):
    """The door opening (rounded top corners; the bottom runs below the floor so only the top corners show)."""
    w = DOOR_W + 2 * margin
    top = DOOR_TOP + margin
    z0 = KFLOOR - 0.10
    return s.rbox((w, depth, top - z0), (KX, y, (top + z0) / 2), r=DOOR_R + margin, seg=4)


def kennel_walls():
    """Plank walls: two sides and two gables (the front one with the door)."""
    eave = roof_under(WX) + 0.012                       # walls run 1 cm up into the roof
    apex = RIDGE_IN + 0.012
    parts = []
    for sx in (-1, 1):
        w = s.rbox((WT, WY1 - WY0 - 0.01, eave - KFLOOR + 0.004), (KX + sx * (WX - WT / 2), (WY0 + WY1) / 2,
                   (eave + KFLOOR - 0.004) / 2), r=0.004, seg=1)
        parts.append(planks(w, KFLOOR, eave))
    gable = [(KX - WX, KFLOOR - 0.004), (KX + WX, KFLOOR - 0.004), (KX + WX, eave), (KX, apex), (KX - WX, eave)]
    parts.append(planks(prism_y(gable, WY0, WY0 + WT), KFLOOR, eave))
    front = planks(prism_y(gable, WY1 - WT, WY1), KFLOOR, eave)
    front = s.boolean(front, door_cutter(), "DIFFERENCE")
    s.bevel_edges(front, 0.003, 1, angle=60)
    parts.append(front)
    return parts


def kennel_inside():
    """The floor plate and a painted lining on the inside of the side and back walls: dark through the doorway."""
    xi = WX - WT                                        # walls' inner faces at KX +- xi
    yi0, yi1 = WY0 + WT, WY1 - WT
    top = roof_under(xi) - 0.003
    parts = [s.rbox((2 * WX - 0.01, WY1 - WY0 - 0.01, KFLOOR - FLOOR + 0.002), (KX, (WY0 + WY1) / 2,
                    (KFLOOR + FLOOR - 0.002) / 2), r=0.004, seg=1)]
    for sx in (-1, 1):
        parts.append(s.rbox((LINING, yi1 - yi0 - 0.004, top - KFLOOR + 0.002),
                            (KX + sx * (xi - LINING / 2 + 0.002), (yi0 + yi1) / 2, (top + KFLOOR - 0.002) / 2),
                            r=0.002, seg=1))
    xl = xi - LINING + 0.001
    back = [(KX - xl, KFLOOR - 0.002), (KX + xl, KFLOOR - 0.002), (KX + xl, roof_under(xl) - 0.003),
            (KX, RIDGE_IN - 0.003), (KX - xl, roof_under(xl) - 0.003)]
    parts.append(prism_y(back, yi0 - 0.002, yi0 + LINING - 0.002, r=0.002))
    return parts


def kennel_trim():
    """Light corner posts, the arched door trim proud of the wall, barge boards under the roof's gable edges."""
    eave = roof_under(WX)
    parts = []
    for sx in (-1, 1):
        for y in (WY0 + 0.02, WY1 - 0.02):
            parts.append(s.rbox((0.05, 0.05, eave - FLOOR - 0.004), (KX + sx * (WX - 0.01), y, (eave + FLOOR) / 2),
                                r=0.008, seg=1))
    ring = door_cutter(0.035, WY1 + 0.008, 0.03)
    ring = s.boolean(ring, door_cutter(0.0, WY1 + 0.008, 0.2), "DIFFERENCE")
    ring = s.boolean(ring, s.rbox((1.0, 0.2, 0.4), (KX, WY1, FLOOR + 0.002 - 0.2), r=0.0), "DIFFERENCE")  # down to the bed
    s.bevel_edges(ring, 0.004, 1, angle=60)
    parts.append(ring)
    ln = (HALF_X - 0.01) / math.cos(PITCH)
    for y in (KY - HALF_Y + 0.014, KY + HALF_Y - 0.014):
        for sx in (-1, 1):
            parts.append(slope_box((ln, 0.022, 0.055), (HALF_X - 0.01) / 2, y, roof_under, sx, down=-0.004))
    return parts


def name_plate():
    """A bone-shaped plate on the door trim's crown (the classic dog house sign): a bar with two lobes at each end."""
    z = DOOR_TOP + 0.05
    y = WY1 + 0.03
    bar = s.rbox((0.18, 0.018, 0.045), (KX, y, z), r=0.007, seg=1)
    lobes = [s.rcyl(0.026, 0.018, (KX + sx * 0.09, y, z + sz * 0.022), axis="Y", seg=12, r=0.0)
             for sx in (-1, 1) for sz in (-1, 1)]
    return [bar, *lobes]


def roof():
    """Two painted corrugated steel panels on the 25 deg pitch and a round ridge cap."""
    cs, sn = math.cos(PITCH), math.sin(PITCH)
    edge = HALF_X - 0.006                               # the eave's outer top corner, inside the footprint
    length = (edge - 0.0065 + 0.012) / cs               # from 1.2 cm past the ridge to the eave
    dc = edge - 0.0065 - 0.5 * length * cs              # panel centre (distance from the ridge)
    parts = []
    for sx in (-1, 1):
        zc = roof_under(dc) + 0.5 * ROOF_T / cs
        p = s.rbox((length, 2 * HALF_Y - 0.004, ROOF_T), (0, 0, 0), r=0.006, seg=1)
        s.place(p, (KX + sx * (dc + 0.5 * ROOF_T * sn), KY, zc - 0.5 * ROOF_T / cs + 0.5 * ROOF_T * cs),
                (0, sx * PITCH, 0))
        for k in range(5):                              # corrugation: ribs running down the slope
            s.groove(p, "Y", KY - 0.30 + k * 0.15, 0.016, 0.007)
        parts.append(p)
    top = RIDGE_IN + ROOF_T / cs
    parts.append(s.rcyl(0.024, 2 * HALF_Y - 0.006, (KX, KY, top - 0.006), axis="Y", seg=12, r=0.004, bevel_seg=1))
    return parts


def straps():
    """Two red ratchet straps over the roof, round the eaves, down the side walls to anchors on the bed floor; the
    ratchets on the right (bed-side) wall."""
    cs = math.cos(PITCH)
    red, steel = [], []
    d_edge = HALF_X - 0.008
    top = lambda d: roof_under(d) + ROOF_T / cs        # noqa: E731  the roof's top surface
    for y in (KY - 0.22, KY + 0.22):
        for sx in (-1, 1):
            red.append(slope_box((d_edge / cs + 0.01, 0.04, 0.005), d_edge / 2, y, top, sx, down=-0.004, r=0.0))  # over
            z0, z1 = roof_under(d_edge) - 0.004, top(d_edge) + 0.004
            red.append(s.rbox((0.005, 0.04, z1 - z0), (KX + sx * (d_edge + 0.0025), y, (z0 + z1) / 2), r=0.0))                                                                   # eave edge
            mid = (WX + d_edge) / 2
            red.append(slope_box(((d_edge - WX) / cs + 0.006, 0.04, 0.005), mid, y, roof_under, sx, down=-0.002, r=0.0))  # under
            xw = KX + sx * (WX + 0.0035)
            zt = roof_under(WX) - 0.002
            red.append(s.rbox((0.005, 0.04, zt - FLOOR), (xw, y, (zt + FLOOR) / 2), r=0.0))   # down the wall
            steel.append(s.rbox((0.014, 0.05, 0.02), (xw + sx * 0.006, y, FLOOR + 0.008), r=0.003, seg=1))  # anchor
        xw = KX + WX
        steel.append(s.rbox((0.022, 0.055, 0.09), (xw + 0.013, y, 1.12), r=0.006, seg=1))               # ratchet
        steel.append(s.rcyl(0.016, 0.07, (xw + 0.018, y, 1.155), axis="Y", seg=10, r=0.0))
    return red, steel


def hardware():
    """Steel bits: the threshold ramp, a sheet-metal patch on the right wall, the caged lamp on the front corner."""
    parts = []
    ramp = s.rbox((DOOR_W - 0.02, 0.05, 0.008), (0, 0, 0), r=0.002, seg=1)
    s.place(ramp, (KX, WY1 + 0.012, (KFLOOR + FLOOR) / 2 + 0.002), (math.radians(-20), 0, 0))
    parts.append(ramp)
    # a patch over a rotten plank, slightly askew, four rivets
    px = KX + WX + 0.002
    patch = s.rbox((0.006, 0.20, 0.15), (0, 0, 0), r=0.002, seg=1)
    s.place(patch, (px, KY, 1.27), (math.radians(4), 0, 0))
    parts.append(patch)
    parts += [s.rcyl(0.007, 0.008, (px + 0.004, KY + sy * 0.085, 1.27 + sz * 0.06 + sy * 0.006), axis="X", seg=8, r=0.0)
              for sy in (-1, 1) for sz in (-1, 1)]
    # a caged lamp on an arm off the front right corner post, under the eave
    cx, cy = KX + WX - 0.01, WY1 - 0.02
    lz = 1.46
    ly = WY1 + 0.03
    parts.append(s.tube([(cx, cy + 0.02, lz + 0.03), (cx, ly, lz + 0.03), (cx, ly, lz + 0.012)], 0.006, res=1,
                        smooth=False))
    parts.append(s.lathe([(0.008, 0.0), (0.02, -0.004), (0.046, -0.03), (0.048, -0.036), (0.0, -0.036)], seg=14,
                         loc=(cx, ly, lz + 0.014)))
    for a in range(4):                                  # cage wires under the shade
        t = math.radians(45 + 90 * a)
        parts.append(s.tube([(cx + 0.04 * math.cos(t), ly + 0.04 * math.sin(t), lz - 0.02),
                             (cx + 0.03 * math.cos(t), ly + 0.03 * math.sin(t), lz - 0.05),
                             (cx, ly, lz - 0.058)], 0.0025, res=0, smooth=False))
    bulb = s.ball(0.024, (cx, ly, lz - 0.03), seg=12)
    return parts, [bulb]


def build_kennel():
    s.clear_scene()
    red, steel = straps()
    hw, bulb = hardware()
    objs = [s.part("Accent_Walls", kennel_walls()),
            s.part("Body_Interior", kennel_inside()),
            s.part("AccentBoard_Trim", kennel_trim()),
            s.part("TrimHandle_NamePlate", name_plate()),         # a red bone on the light trim
            s.part("Body_Roof", roof()),
            s.part("Trim_Hardware", hw + steel),
            s.part("TrimHandle_Straps", red),
            s.part("EyeLamp_Bulb", bulb)]
    return objs


# --------------------------------------------------------------------------- shelves

SX0, SX1 = 0.58, WALL_X + 0.003  # shelf boards run 3 mm into the bed wall
SY0, SY1 = 0.97, 2.33
POSTS_Y = (0.99, 1.65, 2.31)     # level 3's posts
BRACKETS_Y = (1.25, 2.05)        # levels 1 and 2's brackets (clear of the posts)
POST_X = 0.823                   # level 3's square posts, 5 cm, against the wall


def board(z_top, x0=SX0, notches=False):
    b = s.rbox((SX1 - x0, SY1 - SY0, 0.025), ((x0 + SX1) / 2, (SY0 + SY1) / 2, z_top - 0.0125), r=0.005, seg=1)
    s.groove(b, "Y", (SY0 + SY1) / 2, 0.006, 0.003)    # two boards butted end to end
    if notches:
        for y in POSTS_Y:
            b = s.boolean(b, s.rbox((0.06, 0.06, 0.1), (POST_X, y, z_top), r=0.0), "DIFFERENCE")
    return b


def bracket(y, z_top, reach=0.24, drop=0.16):
    """A pressed steel shelf bracket: a wall plate and a triangular gusset under the board, one piece."""
    zt = z_top - 0.024                                   # board underside, 1 mm up into it
    plate = s.rbox((0.01, 0.05, drop + 0.02), (WALL_X - 0.004, y, zt - drop / 2), r=0.003, seg=1)
    tri = [(WALL_X - 0.006, zt - drop), (WALL_X - 0.006, zt), (WALL_X - reach, zt), (WALL_X - reach, zt - 0.025)]
    g = bmesh.new()                                     # the gusset, 12 mm thick
    pts = tri
    a = [g.verts.new((x, y - 0.006, z)) for x, z in pts]
    b = [g.verts.new((x, y + 0.006, z)) for x, z in pts]
    g.faces.new(a)
    g.faces.new(b[::-1])
    for k in range(len(pts)):
        g.faces.new((a[k], a[(k + 1) % len(pts)], b[(k + 1) % len(pts)], b[k]))
    bmesh.ops.recalc_face_normals(g, faces=g.faces)
    s.bevel_edges(g, 0.002, 1, angle=30)
    flange = s.rbox((reach + 0.01, 0.03, 0.006), (WALL_X - reach / 2 - 0.004, y, zt - 0.002), r=0.002, seg=1)
    bolts = [s.rcyl(0.007, 0.008, (WALL_X - 0.011, y, zt - drop * f), axis="X", seg=8, r=0.0) for f in (0.3, 0.8)]
    return [plate, g, flange], bolts


def lip(z_top, h=0.05):
    """A board on edge along the shelf's inner side, its ends rounded: stops cargo sliding off in a turn."""
    return s.rbox((0.018, SY1 - SY0, h + 0.004), (SX0 + 0.009, (SY0 + SY1) / 2, z_top + h / 2 - 0.002), r=0.006, seg=1)


def shelf_level(k):
    """(board, brackets/rack, steel) bmesh lists of upgrade level k."""
    if k == 1:
        zt = 1.06
        steel, frame = [], []
        for y in BRACKETS_Y:
            b, bolts = bracket(y, zt, drop=0.15)
            frame += b
            steel += bolts
        for y in (1.6, 2.2):                             # tie-down hooks under the board's edge
            steel.append(s.tube([(SX0 + 0.04, y, zt - 0.02), (SX0 + 0.01, y, zt - 0.045), (SX0 + 0.03, y, zt - 0.065)],
                                0.005, res=1, smooth=False))
        return [board(zt), lip(zt)], frame, steel
    if k == 2:
        zt = WALL_TOP
        steel, frame = [], []
        for y in BRACKETS_Y:
            b, bolts = bracket(y, zt, drop=0.14)
            frame += b
            steel += bolts
        return [board(zt, notches=True), lip(zt)], frame, steel
    # level 3: posts bolted to the inner wall rising above it, arms and braces under a top shelf, a tie-down rail
    zt = 1.625
    frame, steel = [], []
    for y in POSTS_Y:
        frame.append(s.rbox((0.05, 0.05, zt - 0.008 - 1.12), (POST_X, y, (zt - 0.008 + 1.12) / 2), r=0.006,
                            seg=1))                                                     # post, up into the board
        frame.append(s.rbox((POST_X - SX0, 0.035, 0.035), ((POST_X + SX0) / 2, y, zt - 0.035), r=0.005, seg=1))  # arm
        frame.append(s.tube([(POST_X - 0.01, y, 1.42), (SX0 + 0.06, y, zt - 0.055)], 0.011, res=1, smooth=False))
        steel.append(s.rbox((0.012, 0.07, 0.11), (WALL_X - 0.005, y, 1.18), r=0.003, seg=1))           # wall plate
        steel += [s.rcyl(0.007, 0.012, (WALL_X - 0.013, y + sy * 0.022, 1.18 + sz * 0.035), axis="X", seg=8, r=0.0)
                  for sy in (-1, 1) for sz in (-1, 1)]
    frame.append(s.rbox((0.035, SY1 - SY0, 0.035), (POST_X, (SY0 + SY1) / 2, zt - 0.035), r=0.005, seg=1))  # top beam
    for y0, y1 in zip(POSTS_Y, POSTS_Y[1:]):            # diagonal flat bars between the posts, above the wall
        z0, z1 = WALL_TOP + 0.07, zt - 0.06
        ln = math.hypot(y1 - y0 - 0.05, z1 - z0)
        bar = s.rbox((0.012, ln, 0.03), (0, 0, 0), r=0.003, seg=1)
        s.place(bar, (POST_X, (y0 + y1) / 2, (z0 + z1) / 2), (math.atan2(z1 - z0, y1 - y0 - 0.05), 0, 0))
        frame.append(bar)
    # tie-down rail on the inner edge, on stanchions, with rings for straps
    rx, rz = SX0 + 0.02, zt + 0.075
    steel.append(s.tube([(rx, SY0 + 0.02, zt - 0.005), (rx, SY0 + 0.02, rz), (rx, SY1 - 0.02, rz),
                         (rx, SY1 - 0.02, zt - 0.005)], 0.011, res=1, smooth=False))
    for y in POSTS_Y[1:2]:
        steel.append(s.rcyl(0.009, 0.08, (rx, y, zt + 0.035), seg=8, r=0.0))
    for y in (1.3, 2.0):
        ring = s.lathe([(0.0, -0.004), (0.022, -0.004), (0.022, 0.004), (0.0, 0.004)], seg=12, axis="X",
                       loc=(rx, y, rz - 0.032))
        steel.append(ring)
    return [board(zt, x0=SX0)], frame, steel


def build_shelves():
    s.clear_scene()
    levels = []
    for k in (1, 2, 3):
        boards, frame, steel = shelf_level(k)
        frame_name = "Body_Shelf3_Rack" if k == 3 else f"Body_Shelf{k}_Brackets"
        objs = [s.part(f"Accent_Shelf{k}_Board", boards),
                s.part(frame_name, frame),
                s.part(f"Trim_Shelf{k}_Rail", steel)]
        levels.append(objs)
    return levels


# --------------------------------------------------------------------------- run

def bounds(objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    lo = [round(min(p[k] for p in pts), 3) for k in range(3)]
    hi = [round(max(p[k] for p in pts), 3) for k in range(3)]
    return lo, hi


def export(root):
    objs = build_kennel()
    bpy.context.view_layer.update()
    meshfix.fix_objects(objs, floor=False, name="car_kennel")
    print("KENNEL blender lo", *bounds(objs))
    s.export_fbx(os.path.join(root, "Car", "car_kennel.fbx"), objs)
    levels = build_shelves()
    bpy.context.view_layer.update()
    for k, objs in enumerate(levels, 1):          # each level on its own: meshfix must not pull one level onto another
        meshfix.fix_objects(objs, floor=False, name=f"car_shelves level {k}")
        print(f"SHELF{k} blender lo", *bounds(objs))
    s.export_fbx(os.path.join(root, "Car", "car_shelves.fbx"), [o for lv in levels for o in lv])


def review(scratch, sheet_path):
    import review as rv
    s.clear_scene()
    car = os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models", "Car")
    groups = []
    for with_car, levels in ((True, "1"), (True, "123"), (False, "123")):
        before = set(bpy.context.scene.objects)
        if with_car:
            bpy.ops.import_scene.fbx(filepath=os.path.join(car, "pickup.fbx"))
            bpy.ops.import_scene.fbx(filepath=os.path.join(car, "car_workbench.fbx"))
        for f in ("car_kennel.fbx", "car_shelves.fbx"):
            bpy.ops.import_scene.fbx(filepath=os.path.join(scratch, "Car", f))
        new = [o for o in bpy.context.scene.objects if o not in before and o.type == "MESH"]
        for o in [o for o in new if "Shelf" in o.name and o.name.split("Shelf")[1][0] not in levels]:
            new.remove(o)
            bpy.data.objects.remove(o, do_unlink=True)
        groups.append(new)
    for g, name in zip(groups, ("car + kennel + shelf level 1", "car + all upgrades", "upgrades")):
        rv.check(g, name)
    rv.sheet(groups, sheet_path, tile=500, focus=(0.0, 1.75, 1.3, 0.9))


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = args[0] if args else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
    if "--review" in args:
        review(root, args[args.index("--review") + 1])
    else:
        export(root)
