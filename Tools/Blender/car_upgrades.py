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

  car_shredder.fbx the engine's three levels as the shredder on the hood grows (concept: Logs/comfy/upgrades/shredder_*.png),
                   each level its own objects (names contain Engine1 / Engine2 / Engine3; the game shows every level up to the
                   engine's): a riveted steel band round the funnel with gussets down to the hood and a jagged steel crown on
                   its rim (1), a second electric motor in front of the funnel with a belt to the grinder's drum (2), a heavy
                   black housing round the funnel's foot with hazard stripes, hydraulic rams and two chrome stacks on the
                   fenders with a beacon (3).
  car_panic.fbx    the teleport's big red mushroom button in a yellow-and-black housing with a steel guard hoop, on the dash
                   top over the glovebox (the passenger's side of the right-hand-drive model).

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
steel, TrimHandle red grips / straps / the panic button, TrimChrome polished steel, Hazard warning yellow, Dark black rubber
and paint, EyeLamp the glowing bulb, EyeBeacon the orange beacon. Every object is <Prefix>_<Thing>.
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


# --------------------------------------------------------------------------- the shredder (engine levels) and the panic button

HOP = Vector((0.0, -1.55, 0.0))    # car.HOPPER: the funnel's axis
RIM = 1.56                         # car.RIM_Z: the mouth
DRUM = Vector((0.0, -1.23, 1.30))  # car.MOTOR: the grinder's motor drum behind the funnel (axis x)


def ring(r0, r1, z0, z1, seg=28):
    """A band round the funnel: a closed (r, z) quad revolved about the hopper's axis (inner face inside the funnel wall)."""
    import car as c
    return s.place(c.revolve([(r0, z0), (r1, z0 + 0.004), (r1, z1 - 0.004), (r0, z1)], seg), HOP)


def engine1():
    """A riveted steel band round the funnel, four gussets down to the hood, a jagged steel crown inside the rim."""
    import car as c
    band = [ring(0.25, 0.30, 1.39, 1.47)]
    rivets, steel = [], []
    for k in range(12):
        t = 2 * math.pi * (k + 0.5) / 12
        d = Vector((math.cos(t), math.sin(t), 0))
        rivets.append(c.orient(s.rcyl(0.011, 0.016, (0, 0, 0), seg=8, r=0.003, bevel_seg=1), d, HOP + d * 0.302 + Vector((0, 0, 1.43))))
    for k in range(4):
        t = 2 * math.pi * (k + 0.5) / 4
        d = Vector((math.cos(t), math.sin(t), 0))
        # a strut from the band down onto the grinder housing's top (z 1.27), a foot plate under it
        band.append(s.tube([HOP + d * 0.285 + Vector((0, 0, 1.40)), HOP + d * 0.32 + Vector((0, 0, 1.262))], 0.02, res=1, smooth=False))
        band.append(s.rbox((0.07, 0.07, 0.016), HOP + d * 0.32 + Vector((0, 0, 1.268)), r=0.004, seg=1, rot=(0, 0, t)))
    # the crown: a steel ring on the rim, taller, sharper teeth on it between the car's own (car.accent: 10 at the rim),
    # held to the band by four straps down the funnel's outside
    steel.append(s.place(c.torus(0.36, 0.022, 28, 6), HOP + Vector((0, 0, RIM + 0.018))))
    for k in range(10):
        t = 2 * math.pi * k / 10
        cc, sn = math.cos(t), math.sin(t)
        steel.append(c.spike(HOP + Vector((0.35 * cc, 0.35 * sn, RIM + 0.01)), HOP + Vector((0.24 * cc, 0.24 * sn, RIM + 0.09)), 0.03, seg=4))
    for k in range(4):
        t = 2 * math.pi * k / 4
        d = Vector((math.cos(t), math.sin(t), 0))
        steel.append(s.tube([HOP + d * 0.29 + Vector((0, 0, 1.45)), HOP + d * 0.385 + Vector((0, 0, RIM + 0.01))], 0.012, res=1, smooth=False))
    return [s.part("Body_Engine1_Band", band), s.part("Trim_Engine1_Rivets", rivets), s.part("TrimChrome_Engine1_Crown", steel)]


MOTOR2 = Vector((-0.40, -1.95, 1.31))   # the second motor: in front of the funnel, axis x, left of the middle
BELT_X = -0.62                          # the belt's plane, outside the hood's bulge


def engine2():
    """A second electric motor (finned, a fan cover) on a mount in front of the funnel, its shaft out to a pulley that
    drives a belt to a pulley on the grinder drum's end; a junction box and a cable to the firewall."""
    body, dark, trim = [], [], []
    x0, x1 = MOTOR2.x - 0.15, MOTOR2.x + 0.15
    prof = [(0.0, -0.152)] + [(0.098 if i % 2 == 0 else 0.086, -0.15 + i * 0.3 / 8) for i in range(9)] + [(0.0, 0.152)]
    body.append(s.lathe(prof, seg=20, axis="X", loc=MOTOR2))
    body.append(s.rbox((0.32, 0.16, 0.05), (MOTOR2.x, MOTOR2.y, 1.215), r=0.012, seg=1))                       # the mount plate
    for dx in (-0.09, 0.09):
        body.append(s.rbox((0.05, 0.1, 0.1), (MOTOR2.x + dx, MOTOR2.y, 1.25), r=0.01, seg=1))                  # its feet
    body.append(s.rbox((0.09, 0.07, 0.06), (MOTOR2.x, MOTOR2.y + 0.02, 1.43), r=0.01, seg=1))                  # junction box
    dark += s.grille((x1 + 0.01, MOTOR2.y, MOTOR2.z), 0.14, 0.14, n=4, bar=0.01, proud=0.012, axis="X", frame=0.012)  # fan cover
    # pulleys: on the motor's shaft and on the drum's end (its cap at x -0.385)
    trim.append(s.rcyl(0.018, abs(BELT_X - x0) + 0.03, ((BELT_X + x0) / 2, MOTOR2.y, MOTOR2.z), axis="X", seg=10, r=0.004, bevel_seg=1))
    trim.append(s.rcyl(0.022, abs(BELT_X + 0.385) + 0.03, ((BELT_X - 0.385) / 2, DRUM.y, DRUM.z), axis="X", seg=10, r=0.004, bevel_seg=1))
    small, big = 0.055, 0.08
    trim.append(s.rcyl(small, 0.05, (BELT_X, MOTOR2.y, MOTOR2.z), axis="X", seg=20, r=0.006, bevel_seg=1))
    trim.append(s.rcyl(big, 0.05, (BELT_X, DRUM.y, DRUM.z), axis="X", seg=24, r=0.006, bevel_seg=1))
    # the belt round both pulleys (open-belt halves joined by straight runs, in the y-z plane)
    pts = []
    for (cy, cz, rr, a0, a1) in ((MOTOR2.y, MOTOR2.z, small + 0.006, math.pi / 2, 3 * math.pi / 2),
                                 (DRUM.y, DRUM.z, big + 0.006, -math.pi / 2, math.pi / 2)):
        for i in range(7):
            a = a0 + (a1 - a0) * i / 6
            pts.append((BELT_X, cy + rr * math.cos(a), cz + rr * math.sin(a)))
    pts.append(pts[0])
    dark.append(s.tube(pts, 0.009, res=1, smooth=False))
    # the cable from the junction box back to the firewall, along the hood
    dark.append(s.tube([(MOTOR2.x, MOTOR2.y + 0.05, 1.44), (MOTOR2.x - 0.12, MOTOR2.y + 0.25, 1.30), (-0.55, -1.2, 1.235), (-0.55, -0.8, 1.245)], 0.012, res=2))
    return [s.part("Body_Engine2_Motor", body), s.part("Dark_Engine2_Belt", dark), s.part("Trim_Engine2_Pulleys", trim)]


def engine3():
    """A heavy black housing round the funnel's foot with hazard stripes, two hydraulic rams up to the rim, two chrome
    stacks on the fenders with slanted tips, an orange beacon on the housing's corner."""
    dark, hazard, chrome, trim, beacon = [], [], [], [], []
    hw, hd, z0, z1 = 0.40, 0.36, 1.22, 1.38          # the housing: a frame round the funnel's foot on the hood
    for sy in (-1, 1):
        dark.append(s.rbox((2 * hw, 0.06, z1 - z0), (0, HOP.y + sy * (hd - 0.03), (z0 + z1) / 2), r=0.012, seg=2))
    for sx in (-1, 1):
        dark.append(s.rbox((0.06, 2 * hd - 0.06, z1 - z0), (sx * (hw - 0.03), HOP.y, (z0 + z1) / 2), r=0.012, seg=2))
    # hazard stripes on the front face and the sides: yellow bars slanted 40 deg, proud of the black
    for k in range(-3, 4):
        hazard.append(s.rbox((0.05, 0.012, 0.13), (k * 0.1, HOP.y - hd - 0.002, (z0 + z1) / 2), r=0.003, seg=1, rot=(0, math.radians(40), 0)))
    for sx in (-1, 1):
        for k in range(-2, 3):
            hazard.append(s.rbox((0.012, 0.05, 0.13), (sx * (hw + 0.002), HOP.y + k * 0.11, (z0 + z1) / 2), r=0.003, seg=1, rot=(math.radians(-40 * sx), 0, 0)))
    # two hydraulic rams from the housing's front corners up to the rim's band
    for sx in (-1, 1):
        a = Vector((sx * 0.33, HOP.y - 0.30, z1 - 0.02))
        b = Vector((sx * 0.21, HOP.y - 0.21, RIM - 0.10))
        mid = a.lerp(b, 0.55)
        trim.append(s.tube([a, mid], 0.026, res=1, smooth=False))
        chrome.append(s.tube([mid - (b - a) * 0.1, b], 0.013, res=1, smooth=False))
    # chrome stacks on the fenders (beside the windshield's foot, clear of the driver's view), slanted tips
    for sx in (-1, 1):
        x, y = sx * 0.80, -1.02
        chrome.append(s.rcyl(0.045, 0.62, (x, y, 1.21 + 0.31), seg=16, r=0.006, bevel_seg=1))
        chrome.append(s.tube([(x, y, 1.80), (x, y + 0.03, 1.86), (x, y + 0.09, 1.9)], 0.045, res=1))
        dark.append(s.rbox((0.12, 0.12, 0.03), (x, y, 1.215), r=0.01, seg=1))                         # the foot plate
        dark.append(s.rcyl(0.05, 0.05, (x, y, 1.42), seg=16, r=0.006, bevel_seg=1))                   # a clamp band
    # the beacon on the housing's front right corner
    dark.append(s.rcyl(0.05, 0.03, (hw - 0.05, HOP.y - hd + 0.05, z1 + 0.012), seg=16, r=0.006, bevel_seg=1))
    beacon.append(s.lathe([(0.0, 0.0), (0.04, 0.0), (0.04, 0.05), (0.032, 0.075), (0.0, 0.085)], seg=16,
                          loc=(hw - 0.05, HOP.y - hd + 0.05, z1 + 0.02)))
    return [s.part("Dark_Engine3_Housing", dark), s.part("Hazard_Engine3_Stripes", hazard), s.part("TrimChrome_Engine3_Stacks", chrome),
            s.part("Trim_Engine3_Rams", trim), s.part("EyeBeacon_Engine3", beacon)]


PANIC = Vector((0.42, -0.62, 1.18))    # the dash's top over the glovebox (car.dark: the dashboard's top at z 1.18)
PANIC_TILT = math.radians(28)          # tipped back towards the seats


def build_panic():
    """The teleport's button: a yellow-and-black housing on the dash top, a big red mushroom on a chrome stem, a steel
    guard hoop over it (the concept's clear flip cover reads as a hoop at this size)."""
    from mathutils import Euler
    s.clear_scene()
    tilt = (-PANIC_TILT, 0, 0)

    def at(dx, dy, dz):
        """A point in the housing's tipped frame (y towards the seats, z up out of its face)."""
        v = Vector((dx, dy, dz))
        v.rotate(Euler(tilt))
        return PANIC + v

    housing = [s.rbox((0.15, 0.13, 0.05), at(0, 0, 0.02), r=0.012, seg=2, rot=tilt)]
    # black stripes on its face towards the seats, slanted, inside its edges
    stripes = [s.rbox((0.014, 0.006, 0.026), at(-0.045 + k * 0.03, 0.064, 0.02), r=0.002, seg=1, rot=(tilt[0], math.radians(35), 0)) for k in range(4)]
    stem = [s.rcyl(0.026, 0.05, at(0, 0, 0.06), seg=16, r=0.004, bevel_seg=1, rot=tilt)]
    dome = s.lathe([(0.0, 0.0), (0.052, 0.0), (0.056, 0.012), (0.05, 0.03), (0.032, 0.046), (0.0, 0.052)], seg=24)
    dome = s.place(dome, at(0, 0, 0.082), tilt)
    hoop = s.tube([at(-0.07, -0.045, 0.04), at(-0.07, -0.02, 0.15), at(0.07, -0.02, 0.15), at(0.07, -0.045, 0.04)], 0.007, res=2)
    feet = [s.rbox((0.02, 0.02, 0.012), at(sx * 0.07, -0.045, 0.045), r=0.003, seg=1, rot=tilt) for sx in (-1, 1)]
    return [s.part("Hazard_Panic_Housing", housing), s.part("Dark_Panic_Stripes", stripes), s.part("TrimChrome_Panic_Stem", stem + feet),
            s.part("TrimHandle_Panic_Button", [dome]), s.part("Trim_Panic_Guard", [hoop])]


def build_shredder():
    s.clear_scene()
    return [engine1(), engine2(), engine3()]


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
    levels = build_shredder()
    bpy.context.view_layer.update()
    for k, objs in enumerate(levels, 1):
        meshfix.fix_objects(objs, floor=False, name=f"car_shredder level {k}")
        print(f"ENGINE{k} blender lo", *bounds(objs))
    s.export_fbx(os.path.join(root, "Car", "car_shredder.fbx"), [o for lv in levels for o in lv])
    objs = build_panic()
    bpy.context.view_layer.update()
    meshfix.fix_objects(objs, floor=False, name="car_panic")
    print("PANIC blender lo", *bounds(objs))
    s.export_fbx(os.path.join(root, "Car", "car_panic.fbx"), objs)


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
