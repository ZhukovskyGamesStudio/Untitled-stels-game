"""Derelict replacements for the rec rooms (rooms_fun.py: studio, game room, bar, electronics lab) and the garage cars
(garage.py): crew recreation gear and station vehicles left behind when the facility was abandoned.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/derelict_rooms_rec.py -- Assets/_Game/Art/Models/StyleProps/Derelict [names...]

Writes <out>/<our model>.fbx with the helpers and surfaces of derelict_props.py (Paint, Composite, Rust, Vinyl, Rubber,
Steel, Screen*, Glass), modelled to our model's bounds (x right, -y the front, z up, standing on z = 0).
Decoration only: no colliders, deterministic.
"""
import math
import os
import random
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derelict_props as dp  # noqa: E402

box, cyl, rod, cable, sphere = dp.box, dp.cyl, dp.rod, dp.cable, dp.sphere


# --------------------------------------------------------------------------- helpers

def turn(bms, pivot, axis, ang):
    """Rotate already-built pieces about a pivot point (a hinge)."""
    if not isinstance(bms, (list, tuple)):
        bms = [bms]
    m = Matrix.Rotation(ang, 3, axis)
    for bm in bms:
        bmesh.ops.rotate(bm, cent=Vector(pivot), matrix=m, verts=bm.verts)
    return bms


def seg_box(surface, a, b, w, d, bev=0.006, panel=False):
    """A box from point a to point b, w wide (across, horizontal) and d deep (the third axis)."""
    a, b = Vector(a), Vector(b)
    length = (b - a).length
    bm = box(surface, -length / 2, length / 2, -w / 2, w / 2, -d / 2, d / 2, panel=panel, bev=bev)
    x = (b - a).normalized()
    up = Vector((0, 0, 1)) if abs(x.z) < 0.95 else Vector((1, 0, 0))
    y = up.cross(x).normalized()
    z = x.cross(y)
    bmesh.ops.transform(bm, matrix=Matrix((x, y, z)).transposed().to_4x4(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    return bm


def hose(pts, r=0.012):
    """A rubber hose through the points."""
    for p, q in zip(pts, pts[1:]):
        rod("Rubber", p, q, r, seg=6)


def crt(*args, **kw):
    """derelict_props.crt (stand=False) without its top vents and its 6 mm button (on a small set the housing slopes
    away under the vents, and the dents turn the button inside out)."""
    zc = dp.crt(*args, **kw)
    for bm in dp.parts["Rubber"][-7:]:
        bm.free()
    del dp.parts["Rubber"][-7:]
    return zc


def cyl0(surface, x, y, z0, z1, r, seg=12, r2=None, axis="Z"):
    """derelict_props.cyl without the edge bevel (half the triangles: wheels, drums)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=abs(z1 - z0))
    if axis == "X":
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=bm.verts)
        c = Vector(((z0 + z1) / 2, x, y))
    elif axis == "Y":
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "X"), verts=bm.verts)
        c = Vector((x, (z0 + z1) / 2, y))
    else:
        c = Vector((x, y, (z0 + z1) / 2))
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    return dp.add(surface, bm)


def built():
    """Every piece built so far (to move or turn a whole assembly)."""
    return [bm for bms in dp.parts.values() for bm in bms]


# --------------------------------------------------------------------------- studio

def acoustic_panel():
    """Sound-damping bulkhead panel: quilted vinyl cushions on a steel backplate in a painted frame; one cushion
    is gone (rust behind it), one hangs askew from its last screw."""
    box("Steel", -0.58, 0.58, 0.016, 0.038, 0.92, 2.08)
    box("Paint", -0.585, 0.585, -0.02, 0.036, 2.035, 2.095)
    box("Paint", -0.585, 0.585, -0.02, 0.036, 0.905, 0.965)
    for sx in (-1, 1):
        box("Paint", sx * 0.57 - 0.03, sx * 0.57 + 0.03, -0.024, 0.04, 0.9, 2.1)
    for i in range(3):
        for j in range(3):
            xc, zc = (i - 1) * 0.36, 1.14 + j * 0.36
            if (i, j) == (2, 0):
                box("Rust", xc - 0.17, xc + 0.17, 0.004, 0.028, zc - 0.17, zc + 0.17, bev=0.002)
                for sx in (-1, 1):
                    cyl("Steel", xc + sx * 0.13, zc + 0.13, -0.002, 0.03, 0.008, seg=8, axis="Y")
                continue
            if (i, j) == (0, 2):
                continue
            box("Vinyl", xc - 0.17, xc + 0.17, -0.03, 0.03, zc - 0.17, zc + 0.17, panel=True, bev=0.012)
    # the hanging one, a little in front of the others, turned about its top-left screw
    hang = box("Vinyl", -0.53, -0.19, -0.04, -0.006, 1.69, 2.03, panel=True, bev=0.012)
    turn(hang, (-0.51, 0, 2.01), "Y", 0.2)
    rod("Steel", (-0.51, 0.03, 2.01), (-0.51, -0.045, 2.01), 0.008)
    box("Composite", 0.2, 0.5, -0.025, -0.012, 2.05, 2.09, bev=0.0)                            # stencil plate
    export("acoustic_panel")


def mic_stand():
    """PA microphone on a weighted stand: a brake disc for a base, a steel pole and boom with a rusty
    counterweight, a chunky composite intercom mic in a clamp, its coiled cord down the pole."""
    cyl("Rust", 0, 0, 0.0, 0.05, 0.225, seg=20)
    cyl("Steel", 0, 0, 0.04, 0.1, 0.09, seg=14)
    cyl("Steel", 0, 0, 0.08, 1.33, 0.016, seg=10)
    cyl("Paint", 0, 0, 0.8, 0.87, 0.026, seg=10)                                                # clutch
    cyl("Paint", 0.0, 1.33, -0.035, 0.035, 0.032, seg=12, axis="X")                            # boom joint
    rod("Paint", (0, 0.17, 1.194), (0, -0.15, 1.45), 0.012)
    cyl("Rust", 0, 1.19, 0.14, 0.22, 0.045, seg=12, axis="Y")                                  # counterweight
    box("Steel", -0.03, 0.03, -0.17, -0.12, 1.42, 1.46)                                         # clamp
    box("Composite", -0.045, 0.045, -0.215, -0.11, 1.44, 1.57, panel=True)                      # the mic
    box("Rubber", -0.035, 0.035, -0.225, -0.205, 1.47, 1.555, bev=0.002)                        # grille
    box("Rubber", -0.02, 0.02, -0.16, -0.12, 1.4, 1.445)                                        # cord gland
    pts = [(0.0, -0.14, 1.41)]
    for k in range(12):                                                                         # coiled cord
        a = k * 1.3
        pts.append((0.03 * math.cos(a), 0.03 * math.sin(a) - 0.0, 1.3 - k * 0.07))
    pts += [(0.05, -0.08, 0.3), (0.12, -0.16, 0.03), (0.2, -0.2, 0.008)]
    hose(pts, 0.008)
    box("Composite", 0.016, 0.022, -0.03, 0.03, 1.0, 1.08, bev=0.0)                             # asset tag
    export("mic_stand")


def mixing_desk():
    """Comms console: steel desk on rack pedestals, a slanted switch panel (toggles, sliders, a few keys gone), a
    meter bridge with a CRT showing the last log, two speaker cabinets (one knocked over), a headset by its cord."""
    for sx in (-1, 1):
        box("Paint", sx * 0.6 - 0.18, sx * 0.6 + 0.18, -0.37, 0.37, 0.0, 0.71)
        for k, z in enumerate((0.55, 0.4, 0.25)):
            if sx == 1 and k == 1:
                box("Rubber", sx * 0.6 - 0.15, sx * 0.6 + 0.15, -0.38, -0.36, z + 0.005, z + 0.115, bev=0.0)  # missing unit
                continue
            box("Steel", sx * 0.6 - 0.15, sx * 0.6 + 0.15, -0.395, -0.36, z, z + 0.12)
            box("Screen" if (k + sx) % 3 else "Rubber", sx * 0.6 + 0.08, sx * 0.6 + 0.1, -0.4, -0.383, z + 0.08, z + 0.1, bev=0.0)
    box("Steel", -0.8, 0.8, -0.4, 0.4, 0.7, 0.75, panel=True)
    box("Paint", -0.42, 0.42, 0.3, 0.36, 0.2, 0.71)
    # the slanted switch panel
    yc, zc, ang = -0.13, 0.765, 0.18
    box("Composite", -0.5, 0.5, yc - 0.23, yc + 0.23, zc - 0.04, zc + 0.04, rx=ang, panel=True)

    def on_slope(x, yr, hw, hd, hh, surface):
        h = 0.04 + hh / 2 - (0.008 if hh < 0.01 else 0.012)
        cy = yc + yr * math.cos(ang) - h * math.sin(ang)
        cz = zc + yr * math.sin(ang) + h * math.cos(ang)
        box(surface, x - hw, x + hw, cy - hd, cy + hd, cz - hh / 2, cz + hh / 2, rx=ang, bev=0.0)
    r = random.Random(31)
    for k in range(12):
        x = -0.41 + k * 0.075
        on_slope(x, -0.12, 0.003, 0.07, 0.006, "Rubber")                                       # slider slot
        if k not in (3, 8):
            on_slope(x, -0.12 + (r.random() - 0.5) * 0.1, 0.016, 0.01, 0.024, "Composite")     # slider cap
        for yr in (0.02, 0.1):
            if r.random() < 0.85:
                on_slope(x, yr, 0.012, 0.012, 0.02, "Rubber" if r.random() < 0.6 else "Steel")
    # meter bridge, its LEDs mostly dead, the CRT on it
    box("Composite", -0.49, 0.49, 0.06, 0.24, 0.735, 0.86)
    for k in range(12):
        x = -0.42 + k * 0.075
        box("Screen" if k % 4 == 1 else "Rubber", x - 0.008, x + 0.008, 0.05, 0.073, 0.79, 0.84, bev=0.0)
    crt(0.0, 0.12, 0.85, w=0.4, h=0.22, depth=0.26, face=-1, kind="ScreenText", stand=False, tilt=0.03)
    # speakers: the left one turned to the operator, the right one knocked onto its side
    left = box("Composite", -0.74, -0.54, 0.05, 0.29, 0.74, 1.04)
    cone = cyl("Rubber", -0.64, 0.92, 0.035, 0.06, 0.07, seg=14, axis="Y")
    turn([left, cone], (-0.64, 0.17, 0), "Z", 0.35)
    right = box("Composite", 0.48, 0.78, 0.05, 0.29, 0.74, 0.94)
    cone = cyl("Rubber", 0.68, 0.84, 0.035, 0.06, 0.07, seg=14, axis="Y")
    turn([right, cone], (0.63, 0.17, 0), "Z", -0.15)
    # the headset hanging off the front edge
    cable((0.3, -0.36, 0.74), (0.42, -0.375, 0.44), sag=0.05, r=0.007)
    cyl("Composite", 0.42, 0.42, -0.39, -0.35, 0.04, seg=12, axis="Y")
    rod("Composite", (0.42, -0.37, 0.46), (0.5, -0.37, 0.5), 0.008)
    dp.papers(-0.62, -0.18, 0.749, 3, 32, spread=0.15)
    export("mixing_desk")


def drum_kit():
    """A kit made of what was lying around: a chemical drum on its side for the bass, two canisters on a pipe mount
    for toms, an upturned bucket on a tripod for the snare, a paint drum for the floor tom, hubcaps for cymbals, a
    crate with a folded seat cushion for the stool."""
    cyl("Rust", 0, 0.272, -0.6, -0.12, 0.262, seg=18, axis="Y")                                # bass: a drum on its side
    for y0 in (-0.6, -0.15):
        cyl("Paint", 0, 0.272, y0 - 0.005, y0 + 0.035, 0.272, seg=18, axis="Y")
    box("Composite", -0.18, 0.18, -0.615, -0.59, 0.2, 0.3, bev=0.0)                              # stencil band
    for sx in (-1, 1):
        box("Rubber", sx * 0.2 - 0.04, sx * 0.2 + 0.04, -0.5, -0.22, 0.0, 0.06)                  # chocks
    for x, r, z in ((-0.15, 0.11, 0.62), (0.15, 0.12, 0.64)):                                    # toms
        cyl("Composite", x, -0.3, z, z + 0.18, r, seg=16)
        cyl("Rubber", x, -0.3, z + 0.17, z + 0.19, r * 0.4, seg=10)                               # cap
        rod("Steel", (0, -0.36, 0.5), (x * 0.8, -0.3, z + 0.02), 0.012)
    cyl("Composite", -0.32, 0.06, 0.5, 0.64, 0.16, seg=18, r2=0.13)                              # snare: a bucket
    for i in range(3):
        a = i * 2 * math.pi / 3 + 0.4
        rod("Steel", (-0.32, 0.06, 0.52), (-0.32 + 0.2 * math.cos(a), 0.06 + 0.2 * math.sin(a), 0.0), 0.009)
    rod("Composite", (-0.42, -0.02, 0.636), (-0.22, 0.14, 0.64), 0.008)                         # sticks
    rod("Composite", (-0.4, 0.12, 0.636), (-0.2, 0.0, 0.64), 0.008)
    cyl("Paint", 0.4, 0.12, 0.0, 0.48, 0.17, seg=18)                                             # floor tom
    for z in (0.08, 0.4):
        cyl("Rust", 0.4, 0.12, z, z + 0.03, 0.175, seg=18)
    cyl("Rubber", 0.4, 0.12, 0.47, 0.49, 0.15, seg=18)
    rod("Steel", (-0.62, -0.05, 0.0), (-0.62, -0.05, 0.98), 0.012)                              # hi-hat
    for i in range(3):
        a = i * 2 * math.pi / 3 + 1.0
        rod("Steel", (-0.62, -0.05, 0.25), (-0.62 + 0.14 * math.cos(a), -0.05 + 0.14 * math.sin(a), 0.0), 0.008)
    cyl("Steel", -0.62, -0.05, 0.86, 0.875, 0.16, seg=18, r2=0.12)
    cyl("Steel", -0.62, -0.05, 0.895, 0.91, 0.12, seg=18, r2=0.16)
    rod("Steel", (0.6, -0.45, 0.0), (0.6, -0.45, 0.9), 0.012)                                  # crash: a hubcap
    for i in range(3):
        a = i * 2 * math.pi / 3 + 0.2
        rod("Steel", (0.6, -0.45, 0.25), (0.6 + 0.14 * math.cos(a), -0.45 + 0.14 * math.sin(a), 0.0), 0.008)
    rod("Steel", (0.6, -0.45, 0.9), (0.42, -0.36, 1.12), 0.01)
    cap = [cyl("Steel", 0.42, -0.36, 1.115, 1.13, 0.19, seg=20, r2=0.06)]
    turn(cap, (0.42, -0.36, 1.12), "X", 0.22)
    box("Rust", -0.2, 0.2, 0.3, 0.64, 0.0, 0.44, panel=True)                                    # stool: a crate
    box("Vinyl", -0.17, 0.17, 0.33, 0.61, 0.43, 0.49, rz=0.08, panel=True)
    cable((-0.15, 0.6, 0.3), (0.3, 0.6, 0.007), sag=0.05, r=0.007)
    export("drum_kit")


# --------------------------------------------------------------------------- game room / bar

def pool_table():
    """Crew rec table gone to dust: painted steel rails and apron, the felt (vinyl) torn back to the slate, rubber
    pocket nets, a few balls, a cue across the rail and a snapped one on the floor; one leg replaced by a crate."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            if (sx, sy) == (1, -1):
                box("Rust", 0.94, 1.18, -0.62, -0.38, 0.0, 0.34, panel=True)
                box("Steel", sx * 1.05 - 0.06, sx * 1.05 + 0.06, sy * 0.5 - 0.06, sy * 0.5 + 0.06, 0.32, 0.66)
                continue
            box("Steel", sx * 1.05 - 0.06, sx * 1.05 + 0.06, sy * 0.5 - 0.06, sy * 0.5 + 0.06, 0.012, 0.66)
            box("Rubber", sx * 1.05 - 0.07, sx * 1.05 + 0.07, sy * 0.5 - 0.07, sy * 0.5 + 0.07, 0.0, 0.02)
    box("Paint", -1.15, 1.15, -0.6, 0.6, 0.62, 0.78, panel=True)
    box("Vinyl", -1.12, 1.12, -0.57, 0.57, 0.76, 0.8)
    box("Steel", 0.05, 0.6, -0.15, 0.22, 0.78, 0.806, rz=0.25, bev=0.0)                        # bare slate
    box("Vinyl", 0.1, 0.55, 0.22, 0.25, 0.79, 0.825, rx=0.5, rz=0.25, bev=0.0)                 # felt flap peeled up
    for sy in (-1, 1):
        box("Paint", -1.25, 1.25, sy * 0.635 - 0.065, sy * 0.635 + 0.065, 0.76, 0.85)
        box("Rubber", -1.12, 1.12, sy * 0.56 - 0.02, sy * 0.56 + 0.02, 0.785, 0.83)
    for sx in (-1, 1):
        box("Paint", sx * 1.185 - 0.065, sx * 1.185 + 0.065, -0.575, 0.575, 0.765, 0.845)
        box("Rubber", sx * 1.11 - 0.02, sx * 1.11 + 0.02, -0.54, 0.54, 0.785, 0.83)
    for x, y in ((-1.12, -0.57), (-1.12, 0.57), (1.12, -0.57), (1.12, 0.57), (0.0, -0.6), (0.0, 0.6)):
        cyl("Rubber", x, y, 0.53, 0.66, 0.07, seg=10, r2=0.05)                                  # pocket nets
    r = random.Random(41)
    for i in range(5):
        x, y = -0.9 + r.random() * 0.8, (r.random() - 0.5) * 0.8
        sphere("Composite", x, y, 0.818, 0.028, seg=10)
    rod("Composite", (-1.2, -0.64, 0.86), (0.4, 0.0, 0.806), 0.012)
    rod("Composite", (-0.8, -0.66, 0.012), (0.15, -0.6, 0.012), 0.012)
    export("pool_table")


def foosball():
    """Crew rec table, dusty and stripped: a painted steel box on four legs, rods across with stamped steel
    players, two rods gone, one dropped onto the field, a player snapped off, rubber grips worn."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.6 - 0.04, sx * 0.6 + 0.04, sy * 0.18 - 0.04, sy * 0.18 + 0.04, 0.0, 0.74)
        box("Steel", sx * 0.6 - 0.025, sx * 0.6 + 0.025, -0.18, 0.18, 0.1, 0.14)
    box("Steel", -0.6, 0.6, -0.025, 0.025, 0.1, 0.14)
    box("Composite", -0.66, 0.66, -0.22, 0.22, 0.7, 0.75)                          # field
    for sy in (-1, 1):
        box("Paint", -0.7, 0.7, sy * 0.24 - 0.025, sy * 0.24 + 0.025, 0.7, 0.93, panel=True)
    for sx in (-1, 1):
        box("Paint", sx * 0.68 - 0.025, sx * 0.68 + 0.025, -0.22, 0.22, 0.7, 0.93, panel=True)
        box("Rubber", sx * 0.68 - 0.03, sx * 0.68 + 0.03, -0.07, 0.07, 0.74, 0.8, bev=0.0)       # goal slot
    r = random.Random(43)
    for k in range(8):
        x = -0.56 + k * 0.16
        if k in (2, 5):
            continue
        side = -1 if k % 2 else 1
        if k == 6:                                                                              # dropped onto the field
            rod("Steel", (x, -0.2, 0.752), (x + 0.06, 0.2, 0.752), 0.01)
            box("Paint", x + 0.01, x + 0.05, -0.05, 0.05, 0.75, 0.78, rz=0.15)
            continue
        rod("Steel", (x, -0.33 if side < 0 else -0.27, 0.86), (x, 0.27 if side < 0 else 0.33, 0.86), 0.008)
        cyl("Rubber", x, 0.86, side * 0.27, side * 0.37, 0.02, seg=10, axis="Y")
        for j, py in enumerate((-0.14, 0.0, 0.14)):
            if (k, j) == (3, 1):
                continue
            ang = 1.4 if (k, j) == (4, 2) else 0.0
            p = box("Paint" if k % 2 else "Composite", x - 0.014, x + 0.014, py - 0.025, py + 0.025, 0.775, 0.895, bev=0.004)
            if ang:
                turn(p, (x, py, 0.86), "Y", ang)
    rod("Steel", (-0.6, 0.24, 0.94), (0.6, 0.24, 0.94), 0.006)                                   # score rail
    for x in (-0.5, -0.45, 0.4):
        cyl("Composite", x, 0.24, 0.925, 0.955, 0.018, seg=10)
    export("foosball")


def dart_board():
    """Junction box on the wall turned dartboard: its doors swung flat to the sides (a tally sheet taped inside one),
    a cable-reel end with painted rings for the board, three screwdrivers stuck in it."""
    zc = 1.73
    box("Paint", -0.158, 0.158, 0.02, 0.04, 1.555, 1.905)
    for z0 in (1.552, 1.878):
        box("Paint", -0.16, 0.16, -0.035, 0.03, z0, z0 + 0.03)
    for sx in (-1, 1):
        box("Paint", sx * 0.147 - 0.017, sx * 0.147 + 0.017, -0.037, 0.032, 1.55, 1.91)
        box("Paint", sx * 0.225 - 0.075, sx * 0.225 + 0.075, -0.045, -0.025, 1.553, 1.907, panel=True)
    box("Composite", -0.28, -0.17, -0.053, -0.04, 1.6, 1.86, rz=0.0, bev=0.0)                  # tally sheet
    box("Composite", -0.1, 0.1, -0.04, -0.03, 1.885, 1.905, bev=0.0)                           # stencil
    cyl("Rust", 0.0, zc, -0.008, 0.03, 0.125, seg=24, axis="Y")
    cyl("Composite", 0.0, zc, -0.016, 0.0, 0.085, seg=24, axis="Y")
    cyl("Paint", 0.0, zc, -0.022, -0.01, 0.045, seg=16, axis="Y")
    cyl("Rust", 0.0, zc, -0.026, -0.015, 0.015, seg=10, axis="Y")
    for x, z in ((0.04, zc + 0.05), (-0.06, zc - 0.02), (0.02, zc - 0.08)):
        rod("Steel", (x, 0.0, z), (x - 0.004, -0.04, z + 0.006), 0.003, seg=6)
        cyl("Composite", x - 0.004, z + 0.006, -0.056, -0.036, 0.008, seg=8, axis="Y")
    export("dart_board")


def bar_counter():
    """Mess-hall serving counter: painted steel carcass (one front panel hanging off), a steel top with rusted food
    wells, a tray rail, a dead drinks dispenser on the back edge, a stack of trays and a mug."""
    box("Paint", -0.97, 0.97, -0.22, 0.28, 0.08, 0.92, panel=True)
    box("Rubber", -0.95, 0.95, -0.18, 0.26, 0.0, 0.1)
    for i, x in enumerate((-0.72, -0.24, 0.24, 0.72)):
        if i == 2:
            box("Rust", x - 0.21, x + 0.21, -0.235, -0.205, 0.15, 0.85, bev=0.002)                   # the bare frame
            p = box("Paint", x - 0.22, x + 0.22, -0.27, -0.235, 0.15, 0.85, panel=True)
            turn(p, (x - 0.2, -0.25, 0.83), "Y", -0.18)
            rod("Steel", (x - 0.2, -0.26, 0.83), (x - 0.2, -0.2, 0.83), 0.008)
            continue
        box("Paint", x - 0.22, x + 0.22, -0.25, -0.2, 0.15, 0.85, panel=True)
    box("Steel", -1.0, 1.0, -0.3, 0.3, 0.9, 0.95, panel=True)
    for x in (-0.65, -0.15):
        box("Rust", x - 0.22, x + 0.22, -0.2, 0.06, 0.93, 0.957, bev=0.002)                      # food wells
    rod("Steel", (-0.98, -0.275, 0.86), (0.98, -0.275, 0.86), 0.014)                             # tray rail
    for x in (-0.8, 0.0, 0.8):
        rod("Steel", (x, -0.28, 0.86), (x, -0.2, 0.86), 0.01)
    # the dispenser on the back edge
    box("Composite", 0.15, 0.6, 0.1, 0.3, 0.94, 1.05, panel=True)
    box("Steel", 0.15, 0.6, -0.02, 0.11, 0.94, 0.965)                                            # drip grate
    for k in range(4):
        x = 0.22 + k * 0.1
        if k == 2:
            continue
        rod("Steel", (x, 0.12, 1.01), (x, 0.06, 0.99), 0.01)
        box("Rubber", x - 0.012, x + 0.012, 0.09, 0.103, 1.015, 1.035, bev=0.0)                  # dead buttons
    box("Screen", 0.5, 0.53, 0.09, 0.103, 1.025, 1.035, bev=0.0)
    box("Composite", 0.65, 0.95, -0.05, 0.25, 0.94, 1.02, panel=True)                            # tray stack
    box("Composite", 0.64, 0.94, -0.08, 0.22, 1.01, 1.04, rz=0.2)
    dp.mug(-0.4, 0.15, 0.94)
    cable((0.6, 0.29, 0.98), (0.8, 0.31, 0.007), sag=0.05, r=0.007)
    export("bar_counter")


def bar_stool():
    """Bolted mess stool: a steel floor flange with rusty bolts, a painted column, a square foot ring with one bar
    hanging loose, a cracked vinyl seat with its foam showing, a little askew."""
    cyl("Steel", 0, 0, 0.0, 0.03, 0.19, seg=20)
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        cyl("Rust", 0.15 * math.cos(a), 0.15 * math.sin(a), 0.02, 0.045, 0.014, seg=8)
    cyl("Paint", 0, 0, 0.02, 0.67, 0.035, seg=14)
    c = 0.135
    corners = [(c, c), (-c, c), (-c, -c), (c, -c)]
    for i, (x, y) in enumerate(corners):
        rod("Paint", (0, 0, 0.3), (x, y, 0.3), 0.01)
        nx, ny = corners[(i + 1) % 4]
        if i == 3:
            rod("Paint", (x, y, 0.3), (x - 0.02, 0.06, 0.13), 0.011)                             # the loose bar
            continue
        rod("Paint", (x, y, 0.3), (nx, ny, 0.3), 0.011)
    cyl("Steel", 0, 0, 0.64, 0.69, 0.15, seg=16)
    seat = [cyl("Vinyl", 0, 0, 0.68, 0.75, 0.19, seg=20),
            box("Composite", -0.04, 0.08, -0.03, 0.07, 0.735, 0.757, rz=0.4, bev=0.003)]          # foam through the crack
    turn(seat, (0, 0, 0.68), "X", 0.05)
    export("bar_stool")


# --------------------------------------------------------------------------- electronics lab

def lab_bench():
    """Diagnostics bench: steel top on painted legs, a pegboard riser with a CRT scope (snow) and a dead power supply,
    a synth's sensor head opened up on the bench, a magnifier lamp slumped over it, trays, papers, cables."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.85 - 0.025, sx * 0.85 + 0.025, sy * 0.33 - 0.025, sy * 0.33 + 0.025, 0.0, 0.87)
        box("Paint", sx * 0.85 - 0.02, sx * 0.85 + 0.02, -0.33, 0.33, 0.1, 0.14)
        box("Paint", sx * 0.86 - 0.02, sx * 0.86 + 0.02, 0.315, 0.355, 0.86, 1.5)                 # riser posts
    box("Steel", -0.86, 0.86, -0.33, 0.33, 0.19, 0.215)
    box("Steel", -0.9, 0.9, -0.375, 0.375, 0.86, 0.9, panel=True)
    box("Composite", -0.85, 0.85, 0.345, 0.37, 0.89, 1.49)                          # pegboard
    box("Steel", -0.88, 0.88, 0.085, 0.365, 1.19, 1.215)
    box("Steel", -0.88, 0.88, 0.165, 0.365, 1.475, 1.5, ry=0.02)
    crt(-0.5, 0.1, 1.205, w=0.2, h=0.15, depth=0.24, face=-1, kind="ScreenStatic", stand=False, tilt=0.0)
    box("Composite", -0.16, 0.12, 0.11, 0.33, 1.205, 1.36)                         # power supply
    box("Rubber", -0.13, 0.0, 0.1, 0.115, 1.29, 1.34, bev=0.0)
    box("Screen", 0.03, 0.05, 0.1, 0.115, 1.32, 1.335, bev=0.0)
    for x in (0.04, 0.08):
        cyl("Rubber", x, 1.26, 0.095, 0.115, 0.013, seg=8, axis="Y")
    for k in range(5):                                                                          # hanging tools
        x = 0.25 + k * 0.1
        box("Composite" if k % 2 else "Rubber", x - 0.012, x + 0.012, 0.32, 0.355, 1.22, 1.38, bev=0.0)
    # the opened sensor head
    head = [box("Composite", -0.05, 0.3, -0.22, 0.08, 0.89, 1.04, panel=True),
            cyl("Glass", 0.125, 0.965, -0.24, -0.2, 0.05, seg=16, axis="Y"),
            box("Rust", 0.0, 0.25, -0.18, 0.04, 1.03, 1.06, bev=0.003)]
    turn(head, (0.125, -0.07, 0.9), "Z", 0.3)
    cable((0.25, 0.0, 1.0), (0.6, 0.2, 0.895), sag=0.04, r=0.007)
    cable((0.0, -0.05, 0.95), (-0.15, 0.25, 0.895), sag=0.03, r=0.007)
    # the magnifier lamp, slumped down over the head
    box("Paint", 0.69, 0.75, 0.26, 0.32, 0.89, 0.96)
    rod("Paint", (0.72, 0.29, 0.92), (0.72, 0.29, 1.12), 0.011)
    rod("Paint", (0.72, 0.29, 1.12), (0.55, 0.05, 1.18), 0.009)
    rod("Paint", (0.55, 0.05, 1.18), (0.42, -0.14, 1.11), 0.008)
    cyl("Composite", 0.42, -0.16, 1.07, 1.12, 0.09, seg=18)
    cyl("Glass", 0.42, -0.16, 1.065, 1.08, 0.07, seg=18)
    for x in (-0.76, -0.62, -0.48):                                                             # parts trays
        box("Composite", x - 0.065, x + 0.065, -0.3, -0.2, 0.89, 0.93)
    dp.papers(-0.35, -0.12, 0.899, 3, 51, spread=0.2)
    cable((-0.6, 0.36, 0.95), (-0.8, 0.38, 0.007), sag=0.1, r=0.007)
    cable((0.3, 0.36, 1.0), (0.5, 0.38, 0.007), sag=0.15, r=0.007)
    export("lab_bench")


def robot_arm():
    """Station manipulator arm on a pedestal, powered down and slumped: painted links on steel joints, hydraulic
    hoses (one torn loose), a two-finger claw with one finger snapped off, the access hatch gone, a status lamp."""
    box("Steel", -0.3, 0.3, -0.3, 0.3, 0.0, 0.06)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl("Rust", sx * 0.26, sy * 0.26, 0.05, 0.075, 0.018, seg=8)
    box("Paint", -0.22, 0.22, -0.22, 0.22, 0.05, 0.56, panel=True)
    box("Rubber", 0.0, 0.16, -0.23, -0.2, 0.15, 0.42, bev=0.0)                                   # the open hatch
    cable((0.06, -0.21, 0.3), (0.16, -0.36, 0.008), sag=-0.02, r=0.008)
    cable((0.1, -0.21, 0.25), (0.02, -0.3, 0.007), sag=-0.02, r=0.007)
    box("Steel", -0.25, 0.25, -0.25, 0.25, 0.54, 0.58)
    cyl("Steel", 0, 0, 0.57, 0.68, 0.19, seg=24)
    cyl("Composite", 0.19, -0.19, 0.57, 0.6, 0.03, seg=12)                                       # status lamp
    cyl("Screen", 0.19, -0.19, 0.595, 0.635, 0.02, seg=12)
    S, El, W, F = Vector((0, 0.02, 0.86)), Vector((0, 0.1, 1.45)), Vector((0, -0.31, 1.17)), Vector((0, -0.36, 0.98))
    box("Paint", -0.15, 0.15, -0.14, 0.18, 0.66, 0.9, panel=True)                                # shoulder housing
    cyl("Steel", S.y, S.z, -0.17, 0.17, 0.11, seg=20, axis="X")
    seg_box("Paint", S, El, 0.16, 0.17, panel=True)
    cyl("Steel", El.y, El.z, -0.12, 0.12, 0.085, seg=18, axis="X")
    cyl("Composite", El.x, El.z + 0.02, El.y + 0.08, El.y + 0.21, 0.065, seg=16, axis="Y")      # elbow motor
    seg_box("Paint", El, W, 0.12, 0.12)
    cyl("Steel", W.y, W.z, -0.08, 0.08, 0.055, seg=14, axis="X")
    seg_box("Steel", W, F, 0.08, 0.08)
    box("Steel", -0.06, 0.06, F.y - 0.035, F.y + 0.035, F.z - 0.04, F.z + 0.01)                  # claw palm
    rod("Steel", (-0.035, F.y, F.z - 0.03), (-0.045, F.y - 0.02, F.z - 0.13), 0.011, seg=8)
    rod("Steel", (0.035, F.y, F.z - 0.03), (0.04, F.y - 0.01, F.z - 0.07), 0.011, seg=8)       # snapped finger
    hose([(0.12, 0.17, 0.66), (0.13, 0.19, 0.95), (0.1, 0.22, 1.25), (0.07, 0.2, 1.45)], 0.014)
    hose([(-0.07, 0.05, 1.44), (-0.08, -0.12, 1.34), (-0.07, -0.25, 1.2)], 0.011)
    hose([(0.07, 0.03, 1.4), (0.09, -0.08, 1.31), (0.12, -0.14, 1.12), (0.1, -0.12, 1.0)], 0.011)  # torn loose
    export("robot_arm")


# --------------------------------------------------------------------------- the station rover (garage cars)

TRACK, WB, WR, TW = 0.79, 1.35, 0.33, 0.21   # our sedan's wheel centres (x = +-TRACK, y = +-WB, z = WR)


def rover(hood_open=False, wheels=True, flat=(), missing=(), door=0.0, tailgate=False, cargo=True):
    """An abandoned station utility rover in our sedan's footprint: engine hood at the front (-y), a two-row cab,
    a pickup bed at the back. flat / missing: wheel corners (sx, sy) with a flat tyre / without a tyre.
    Neighbouring panels never share a face plane (they differ by a few millimetres)."""
    box("Steel", -0.66, 0.66, -2.2, 2.14, 0.31, 0.62, bev=0.004)                                # chassis tub
    for s in (-1, 1):
        def xs(a, b):
            return sorted((s * a, s * b))
        box("Paint", *xs(0.64, 0.84), -2.22, -1.68, 0.3, 0.86)                                     # front corner
        box("Paint", *xs(0.645, 0.87), -1.72, -0.98, 0.68, 0.865)                                  # front fender
        box("Paint", *xs(0.64, 0.84), 1.68, 2.16, 0.3, 0.88)                                       # rear corner
        box("Paint", *xs(0.648, 0.87), 0.98, 1.72, 0.68, 0.885)                                    # rear fender
        box("Steel", *xs(0.65, 0.83), -0.995, 0.995, 0.29, 0.42, bev=0.004)                             # rocker
        box("Paint", *xs(0.645, 0.838), 0.64, 1.0, 0.405, 0.875)                                   # bed side, low
        box("Rust", *xs(0.828, 0.852), 1.72, 2.12, 0.31, 0.52, bev=0.002)
        box("Rust", *xs(0.858, 0.882), -1.68, -1.2, 0.685, 0.74, bev=0.002)
        # doors with their windows (the rear-left window is smashed out)
        front = [box("Paint", *xs(0.642, 0.845), -1.0, -0.06, 0.4, 0.92),
                 box("Glass", *xs(0.775, 0.8), -0.9, -0.12, 0.9, 1.3, bev=0.0),
                 box("Paint", *xs(0.77, 0.81), -0.92, -0.08, 1.28, 1.32, bev=0.004)]
        if s < 0 and door:
            turn(front, (s * 0.845, -1.0, 0), "Z", door)
        box("Paint", *xs(0.64, 0.84), -0.08, 0.66, 0.402, 0.918)                       # rear door
        if s > 0:
            box("Glass", *xs(0.775, 0.8), -0.02, 0.58, 0.9, 1.3, bev=0.0)
        box("Paint", *xs(0.74, 0.815), -0.1, -0.03, 0.88, 1.38, bev=0.004)                          # B pillar
        box("Paint", *xs(0.74, 0.815), 0.58, 0.68, 0.87, 1.385, bev=0.004)                          # C pillar
        rod("Paint", (s * 0.78, -0.98, 0.88), (s * 0.74, -0.62, 1.37), 0.035, seg=8)               # A pillar
    # cab: floor, dash, seats, roof, windscreen with a crack star, the back wall with its small window
    box("Steel", -0.655, 0.655, -0.995, 0.655, 0.58, 0.88, bev=0.004)
    box("Composite", -0.72, 0.72, -0.98, -0.68, 0.86, 1.0)
    cyl("Rubber", -0.38, 1.02, -0.69, -0.58, 0.025, seg=8, axis="Y")                            # steering column
    cyl0("Rubber", -0.38, 1.1, -0.6, -0.57, 0.17, seg=14, r2=0.15, axis="Y")                     # steering wheel
    for x0, x1 in ((-0.62, -0.14), (0.14, 0.62)):
        box("Vinyl", x0, x1, -0.55, -0.1, 0.86, 0.98, bev=0.0)
        box("Vinyl", x0, x1, -0.14, -0.04, 0.95, 1.3, rx=-0.12, bev=0.0)
    box("Vinyl", -0.62, 0.62, 0.1, 0.56, 0.86, 0.98, bev=0.0)
    box("Vinyl", -0.62, 0.62, 0.48, 0.58, 0.95, 1.3, rx=-0.1, bev=0.0)
    box("Paint", -0.79, 0.79, -0.66, 0.68, 1.34, 1.4)                                 # roof
    box("Paint", -0.785, 0.785, 0.62, 0.7, 0.86, 1.38)                                           # cab back
    box("Glass", -0.45, 0.45, 0.605, 0.625, 1.02, 1.3, bev=0.0)
    a = math.atan2(0.33, 0.46)
    centre, length = Vector((0, -0.805, 1.13)), math.hypot(0.33, 0.46)
    box("Glass", -0.73, 0.73, centre.y - 0.007, centre.y + 0.007, centre.z - length / 2, centre.z + length / 2, rx=-a, bev=0.0)
    up, out = Vector((0, math.sin(a), math.cos(a))), Vector((0, -math.cos(a), math.sin(a)))

    def on_glass(u, v):
        return centre + Vector((u, 0, 0)) + up * v + out * 0.008
    hit = (0.28, 0.06)
    rcr = random.Random(61)
    for i in range(5):
        ang = i * 2 * math.pi / 5 + rcr.random() * 0.5
        p0 = on_glass(*hit)
        p1 = on_glass(hit[0] + 0.12 * math.cos(ang), hit[1] + 0.1 * math.sin(ang))
        p2 = on_glass(min(0.7, max(-0.7, hit[0] + (0.22 + rcr.random() * 0.2) * math.cos(ang + 0.2))),
                      max(-0.26, min(0.26, hit[1] + (0.15 + rcr.random() * 0.1) * math.sin(ang + 0.2))))
        rod("Rubber", p0, p1, 0.007, seg=4)
        rod("Rubber", p1, p2, 0.007, seg=4)
    # front end: grille, firewall, the hood, lamps, bumper with a push bar
    box("Composite", -0.668, 0.668, -2.24, -2.12, 0.58, 0.858)
    box("Paint", -0.65, 0.65, -1.06, -0.94, 0.6, 0.92, bev=0.004)
    hood = box("Paint", -0.8, 0.8, -2.215, -0.97, 0.84, 0.89, panel=True)
    for s in (-1, 1):
        box("Composite", *sorted((s * 0.42, s * 0.66)), -2.27, -2.18, 0.64, 0.78, bev=0.004)
        if s < 0:
            box("Glass", -0.64, -0.44, -2.285, -2.26, 0.66, 0.76, bev=0.0)
        else:
            box("Rubber", 0.47, 0.61, -2.28, -2.26, 0.67, 0.75, bev=0.0)                         # lamp gone
    box("Steel", -0.86, 0.86, -2.3, -2.17, 0.34, 0.52)
    for x in (-0.3, 0.3):
        rod("Steel", (x, -2.27, 0.5), (x, -2.27, 0.84), 0.022, seg=8)
    rod("Steel", (-0.3, -2.27, 0.84), (0.3, -2.27, 0.84), 0.022, seg=8)
    if hood_open:
        turn(hood, (0, -0.97, 0.88), "X", -0.6)
        rod("Steel", (0.6, -1.95, 0.6), (0.6, -1.81, 1.44), 0.008, seg=6)                       # hood prop
        box("Rust", -0.3, 0.3, -1.85, -1.22, 0.6, 0.84)                                # engine block
        box("Composite", 0.36, 0.6, -2.02, -1.76, 0.6, 0.8)                                       # battery
        cyl("Composite", -0.45, -1.5, 0.6, 0.78, 0.09, seg=10)                                    # coolant tank
        hose([(-0.45, -1.5, 0.76), (-0.32, -1.7, 0.84), (0.0, -1.95, 0.8), (0.0, -2.1, 0.72)], 0.03)
        hose([(0.25, -1.3, 0.8), (0.45, -1.45, 0.86), (0.5, -1.8, 0.79)], 0.015)
    # the bed and the tailgate
    box("Steel", -0.835, 0.835, 0.645, 2.15, 0.84, 0.9, bev=0.004)
    for s in (-1, 1):
        box("Paint", *sorted((s * 0.79, s * 0.85)), 0.66, 2.155, 0.865, 1.12)
    gate = box("Paint", -0.8, 0.8, 2.12, 2.18, 0.86, 1.115)
    if tailgate:
        turn(gate, (0, 2.15, 0.88), "X", -3.0)
    box("Steel", -0.86, 0.86, 2.08, 2.21, 0.34, 0.5)
    if cargo:
        cyl0("Composite", 1.28, 1.06, -0.5, 0.2, 0.18, seg=12, axis="X")                         # a canister lying
        box("Rust", 0.15, 0.7, 1.62, 2.04, 0.88, 1.2, rz=0.08)                                     # a crate
    rod("Steel", (0.7, 0.5, 1.39), (0.7, 1.15, 1.46), 0.007, seg=6)                              # bent antenna
    for x in (-0.6, 0.6):                                                                        # roof rack
        rod("Steel", (x, -0.55, 1.43), (x, 0.6, 1.43), 0.014, seg=6)
        for y in (-0.5, 0.55):
            rod("Steel", (x, y, 1.385), (x, y, 1.44), 0.011, seg=6)
    for y in (-0.5, 0.05, 0.55):
        rod("Steel", (-0.6, y, 1.43), (0.6, y, 1.43), 0.01, seg=6)
    cyl0("Rubber", 0.3, 1.13, 0.72, 0.92, 0.28, seg=14, axis="Y")                               # spare against the cab
    cyl0("Rust", 0.3, 1.13, 0.71, 0.93, 0.16, seg=10, axis="Y")
    cyl0("Composite", -0.35, 0.35, 1.39, 1.45, 0.07, seg=10)                                      # dead beacon
    # wheels
    for sx in (-1, 1):
        for sy in (-1, 1):
            y = sy * WB
            x0, x1 = sorted((sx * 0.6, sx * (TRACK + 0.05)))
            if not wheels:
                cyl0("Rust", y, WR, x0, x1, 0.15, seg=12, axis="X")                                 # brake drum on the hub
                continue
            z = WR - 0.05 if (sx, sy) in flat else WR
            rod("Steel", (sx * 0.6, y, z), (sx * 0.8, y, z), 0.05, seg=8)
            cyl0("Rust", y, z, sx * TRACK - 0.115, sx * TRACK + 0.115, 0.2, seg=10, axis="X")
            if (sx, sy) in missing:
                box("Rust", sx * TRACK - 0.18, sx * TRACK + 0.18, y - 0.2, y + 0.2, 0.0, z - 0.18)  # a block under it
                continue
            t = cyl0("Rubber", y, z, sx * TRACK - TW / 2, sx * TRACK + TW / 2, WR, seg=16, axis="X")
            if (sx, sy) in flat:
                for v in t.verts:
                    if v.co.z < 0.04:
                        v.co.z = max(0.0, v.co.z * 0.3)
                        v.co.x = sx * TRACK + (v.co.x - sx * TRACK) * 1.25


def car_parked():
    """Abandoned station rover: a boxy utility truck, front-left tyre flat, driver's door hanging ajar, the
    windscreen cracked, a lamp gone, the tailgate dropped, a canister and a crate left in the bed."""
    rover(flat={(-1, -1)}, door=0.12, tailgate=True)
    export("car_parked")


def car_hood_open():
    """The same rover being stripped: hood propped up over a rusted engine, the front-right wheel gone and the hub on
    a block."""
    rover(hood_open=True, missing={(1, -1)}, cargo=False)
    box("Rust", -0.5, -0.1, 0.9, 1.4, 0.88, 1.04, rz=-0.1)                         # one box in the bed
    export("car_hood_open")


def car_on_stands():
    """The same rover up on four jack stands, wheels off: two tyres stacked by the front left, one leaning against
    the right flank."""
    rover(wheels=False, cargo=False)
    for bm in built():
        bmesh.ops.translate(bm, vec=Vector((0, 0, 0.28)), verts=bm.verts)
    for sy in (-1, 1):
        for sx in (-1, 1):
            x, y = sx * 0.5, sy * 0.95
            cyl0("Rust", x, y, 0.0, 0.5, 0.12, seg=6, r2=0.03)                                    # a jack stand
            cyl0("Steel", x, y, 0.44, 0.6, 0.035, seg=6)
    for z0 in (0.0, 0.2):
        cyl0("Rubber", -1.25, -0.6, z0, z0 + TW, WR, seg=16)
        cyl0("Rust", -1.25, -0.6, z0 + 0.004, z0 + TW + 0.004, 0.2, seg=10)
    cyl0("Rubber", 0.9, WR, 1.12 - TW / 2, 1.12 + TW / 2, WR, seg=16, axis="X")
    cyl0("Rust", 0.9, WR, 1.12 - 0.115, 1.12 + 0.115, 0.2, seg=10, axis="X")
    export("car_on_stands")


def export(name):
    dp.export(name)


PROPS = [acoustic_panel, mic_stand, mixing_desk, drum_kit, pool_table, foosball, dart_board, bar_counter, bar_stool,
         lab_bench, robot_arm, car_parked, car_hood_open, car_on_stands]

if __name__ == "__main__":
    dp.clear()
    for make in PROPS:
        make()
    print("DONE")
