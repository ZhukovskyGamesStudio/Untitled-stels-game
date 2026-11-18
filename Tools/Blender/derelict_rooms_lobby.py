"""Derelict replacements for the lobby, hall, control-room, library and copy-room pieces (rooms2_tech.py, rooms_paper.py):
deck-plan panels, a specimen case, bolted benches, CRT banks and control consoles, a survey sphere, a tape library.

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_rooms_lobby.py -- Assets/_Game/Art/Models/StyleProps/Derelict [names...]

Same contract as derelict_props.py: <out>/<our model>.fbx modelled to our model's bounds (x right, -y the front, z up),
parts named by surface (Paint, Composite, Rust, Vinyl, Rubber, Steel, Screen, ScreenText, ScreenStatic, Glass),
decoration only, deterministic.
"""
import math
import os
import random
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derelict_props as dp  # noqa: E402

box, cyl, rod, cable, sphere, screen = dp.box, dp.cyl, dp.rod, dp.cable, dp.sphere, dp.screen
DESK_TOP = dp.DESK_TOP_NEW


# --------------------------------------------------------------------------- helpers

def turn(bms, angle, axis, cent):
    """Rotate already built pieces about a point (a slumped head, a leaning torso)."""
    for bm in bms:
        bmesh.ops.rotate(bm, cent=Vector(cent), matrix=Matrix.Rotation(angle, 3, axis), verts=bm.verts)
    return bms


def prism(surface, pts, a0, a1, plane="xz"):
    """A flat polygon extruded: pts in (x, z) extruded along y from a0 to a1, or plane='yz': (y, z) along x."""
    bm = bmesh.new()

    def v3(p, a):
        return Vector((p[0], a, p[1])) if plane == "xz" else Vector((a, p[0], p[1]))
    front = [bm.verts.new(v3(p, a0)) for p in pts]
    back = [bm.verts.new(v3(p, a1)) for p in pts]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], front[j], back[j], back[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return dp.add(surface, bm)


def torus(surface, c, R, r, axis="Z", seg=24, rseg=6):
    """A ring of radius R (tube radius r) round `axis` through c."""
    bm = bmesh.new()
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        rings.append([bm.verts.new(Vector(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a),
                                           r * math.sin(b))))
                      for b in (2 * math.pi * j / rseg for j in range(rseg))])
    for i in range(seg):
        p, q = rings[i], rings[(i + 1) % seg]
        for j in range(rseg):
            k = (j + 1) % rseg
            bm.faces.new((p[j], q[j], q[k], p[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if axis == "X":
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=bm.verts)
    elif axis == "Y":
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "X"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    return dp.add(surface, bm)


def slim_crt(cx, yf, zb, w, h, depth, kind, back=0.65, led=True):
    """A lean CRT (few triangles, for banks of them): its front at yf looking to -y, standing on zb (sink it 1 cm).
    kind None: a gutted housing, the tube gone (a dark hole)."""
    zc = zb + 0.045 + h / 2
    top = zc + h / 2 + 0.04
    dp.tapered_box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, yf, yf + depth, zb, top, back=back, bev=0.006)
    y0, y1 = yf - 0.015, yf + 0.012
    box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, y0, y1, zc + h / 2, top - 0.004, bev=0.003)
    box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, y0, y1, zb + 0.004, zc - h / 2, bev=0.003)
    for sx in (-1, 1):
        box("Composite", cx + sx * w / 2 - 0.005 * sx, cx + sx * (w / 2 + 0.035), y0, y1, zc - h / 2 - 0.005, zc + h / 2 + 0.005,
            bev=0.0)
    if kind:
        screen(kind, cx, yf - 0.007, zc, w + 0.02, h + 0.02, face=-1, bulge=0.006, nx=4, ny=3)
    else:
        box("Rubber", cx - w / 2 - 0.004, cx + w / 2 + 0.004, yf - 0.004, yf + 0.03, zc - h / 2 - 0.004, zc + h / 2 + 0.004, bev=0.0)
    if led:
        box("Screen" if kind and kind != "Glass" else "Rubber", cx + w / 2 - 0.04, cx + w / 2 - 0.022, y0 - 0.004, y0 + 0.012,
            zb + 0.014, zb + 0.026, bev=0.0)
    return zc


def lean_keyboard(cx, cy, z, w=0.44, d=0.15, missing=(), seed=1):
    """A keyboard from strips of keys (cheap): a case, five rows of key blocks, a few keys gone."""
    box("Composite", cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2, z - 0.005, z + 0.022, bev=0.003)
    r = random.Random(seed)
    for row in range(5):
        y = cy - d / 2 + 0.018 + row * (d - 0.03) / 5
        x = cx - w / 2 + 0.02
        cuts = [0.3, 0.55, 0.8, 1.0] if row else [0.2, 0.75, 1.0]
        last = 0.0
        for k, c in enumerate(cuts):
            if (row, k) in missing:
                last = c
                continue
            x0 = x + last * (w - 0.04) + 0.003
            x1 = x + c * (w - 0.04) - 0.003
            box("Rubber" if r.random() < 0.15 else "Composite", x0, x1, y, y + (d - 0.03) / 5 - 0.004, z + 0.015,
                z + 0.03 + row * 0.002, bev=0.0)
            last = c


def forms(cx, cy, z, n, seed):
    """Bundles of yellowed forms lying on a surface at z (thick enough to survive the dents, sunk 6 mm)."""
    r = random.Random(seed)
    for k in range(n):
        x, y = cx + r.uniform(-0.03, 0.03), cy + r.uniform(-0.02, 0.02)
        box("Composite", x - 0.105, x + 0.105, y - 0.15, y + 0.15, z - 0.006 + k * 0.016, z + 0.016 + k * 0.016,
            rz=r.uniform(-0.35, 0.35), bev=0.002)


# --------------------------------------------------------------------------- hall

def directory_board():
    """Deck-plan stand: a weighted steel foot, a panel with a cracked deck plan (corridors, rooms, one section gone
    dark, a few pips still lit), deck level tags below (one plate missing, one askew), a stencilled header, a conduit."""
    box("Steel", -0.4, 0.4, -0.15, 0.15, 0.0, 0.05, panel=True)
    for sx in (-1, 1):
        box("Paint", sx * 0.25 - 0.03, sx * 0.25 + 0.03, -0.025, 0.025, 0.04, 0.44)
    box("Paint", -0.37, 0.37, -0.03, 0.03, 0.42, 1.66, panel=True)
    box("Paint", -0.39, 0.39, -0.045, 0.045, 1.64, 1.9, panel=True)
    box("Composite", -0.3, 0.26, -0.055, -0.035, 1.72, 1.83, bev=0.002)                     # stencil plate
    box("Screen", 0.3, 0.33, -0.054, -0.035, 1.75, 1.78, bev=0.0)                              # status pip
    box("Composite", -0.33, 0.33, -0.04, -0.02, 1.02, 1.6, bev=0.003)                          # the plan plaque
    for z in (1.14, 1.32, 1.48):                                                               # corridors
        box("Steel", -0.31, 0.31, -0.047, -0.032, z - 0.012, z + 0.012, bev=0.0)
    for x in (-0.15, 0.1):
        box("Steel", x - 0.012, x + 0.012, -0.049, -0.034, 1.05, 1.57, bev=0.0)
    for x0, x1, z0, z1 in ((-0.3, -0.18, 1.18, 1.29), (-0.12, 0.07, 1.36, 1.45), (-0.3, -0.18, 1.06, 1.11),
                           (0.13, 0.3, 1.18, 1.29), (-0.12, 0.07, 1.06, 1.11)):
        box("Composite", x0, x1, -0.045, -0.03, z0, z1, bev=0.0)                              # rooms
    box("Rubber", 0.115, 0.325, -0.05, -0.03, 1.335, 1.595, bev=0.002)                        # a dead section
    for x, z in ((-0.24, 1.23), (-0.03, 1.4), (0.2, 1.08)):
        box("Screen", x - 0.009, x + 0.009, -0.052, -0.038, z - 0.009, z + 0.009, bev=0.0)
    for a, b in (((-0.28, -0.04, 1.56), (-0.12, -0.04, 1.4)), ((-0.12, -0.04, 1.4), (-0.02, -0.04, 1.22)),
                 ((-0.12, -0.04, 1.4), (0.08, -0.04, 1.5))):
        rod("Rubber", a, b, 0.005, seg=4)                                                       # cracks
    for k, z in enumerate((0.92, 0.8, 0.68, 0.56)):                                            # level tags
        box("Steel", -0.33, -0.25, -0.042, -0.02, z - 0.04, z + 0.04, bev=0.002)
        if k == 1:
            box("Rust", -0.22, 0.33, -0.036, -0.02, z - 0.035, z + 0.035, bev=0.0)            # the plate is gone
        else:
            box("Composite", -0.22, 0.33, -0.042, -0.02, z - 0.035, z + 0.035, ry=0.1 if k == 2 else 0.0, bev=0.002)
    rod("Rubber", (0.3, 0.035, 1.66), (0.33, 0.1, 1.2), 0.014, seg=8)                         # conduit to the floor
    rod("Rubber", (0.33, 0.1, 1.2), (0.33, 0.11, 0.035), 0.014, seg=8)
    dp.export("directory_board")


def map_board():
    """Wall deck plan of the station: a steel frame in three sections — two printed plan plates with corridors,
    rooms and a few dim pips (one sub-plate hanging askew), the third a dead display with a cracked glass."""
    for z0, z1 in ((0.9, 0.96), (2.34, 2.4)):
        box("Paint", -0.99, 0.99, -0.035, 0.05, z0, z1, panel=True)
    for x0, x1 in ((-1.0, -0.94), (0.94, 1.0)):
        box("Paint", x0, x1, -0.035, 0.05, 0.9, 2.4)
    box("Steel", -0.95, 0.95, 0.0, 0.045, 0.95, 2.35)
    for x in (-0.32, 0.33):
        box("Paint", x - 0.025, x + 0.025, -0.032, 0.045, 0.94, 2.36)
    box("Composite", -0.95, 0.33, -0.02, 0.01, 0.95, 2.35, bev=0.0)
    r = random.Random(31)
    for x0, x1 in ((-0.93, -0.35), (-0.3, 0.31)):
        for z in (1.12, 1.5, 1.86, 2.18):
            box("Steel", x0, x1, -0.028, -0.012, z - 0.014, z + 0.014, bev=0.0)
        for k in range(3):
            x = x0 + (k + 0.5) * (x1 - x0) / 3 + r.uniform(-0.05, 0.05)
            box("Steel", x - 0.012, x + 0.012, -0.028, -0.012, 1.0, 2.3, bev=0.0)
        for _ in range(4):
            x = r.uniform(x0 + 0.05, x1 - 0.15)
            z = r.choice((1.2, 1.58, 1.94))
            box("Composite", x, x + 0.11, -0.03, -0.008, z, z + 0.2, bev=0.0)
        for _ in range(4):
            x, z = r.uniform(x0 + 0.05, x1 - 0.05), r.uniform(1.0, 2.3)
            box("Screen", x - 0.01, x + 0.01, -0.034, -0.014, z - 0.01, z + 0.01, bev=0.0)
    box("Composite", -0.9, -0.42, -0.034, -0.014, 1.0, 1.4, ry=0.07, bev=0.002)                # a plate hanging askew
    box("Glass", 0.33, 0.95, -0.02, 0.005, 0.95, 2.35, bev=0.0)                                # the dead third
    for a, b in (((0.55, -0.02, 2.0), (0.72, -0.02, 1.7)), ((0.72, -0.02, 1.7), (0.9, -0.02, 1.85)),
                 ((0.72, -0.02, 1.7), (0.62, -0.02, 1.3)), ((0.62, -0.02, 1.3), (0.4, -0.02, 1.15))):
        rod("Rubber", a, b, 0.004, seg=4)
    box("Composite", -0.4, 0.4, -0.041, -0.025, 2.35, 2.39, bev=0.0)                           # stencil bar
    dp.export("map_board")


def display_case():
    """Specimen case: a painted steel cabinet, a frame of dark glass with the front smashed (shards still in the
    frame and on the deck), a slumped specimen in a cracked canister fed by tubes from the lid."""
    box("Rubber", -0.47, 0.47, -0.22, 0.22, 0.0, 0.08)
    box("Paint", -0.5, 0.5, -0.25, 0.25, 0.07, 0.87, panel=True)
    box("Composite", -0.14, 0.14, -0.262, -0.24, 0.62, 0.7, bev=0.002)                       # stencil
    box("Steel", 0.3, 0.42, -0.258, -0.235, 0.5, 0.53)                                          # handle
    box("Steel", -0.5, 0.5, -0.25, 0.25, 0.86, 0.9, panel=True)                               # the deck
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.5 - sx * 0.035, sx * 0.5, sy * 0.25 - sy * 0.035, sy * 0.25, 0.89, 1.74, bev=0.003)
    box("Paint", -0.5, 0.5, -0.25, 0.25, 1.73, 1.8, panel=True)
    box("Glass", -0.475, 0.475, 0.205, 0.225, 0.89, 1.745, bev=0.0)                           # back and sides
    for sx in (-1, 1):
        box("Glass", sx * 0.48 - 0.01, sx * 0.48 + 0.01, -0.225, 0.225, 0.89, 1.745, bev=0.0)
    prism("Glass", [(-0.48, 0.88), (-0.1, 0.88), (-0.18, 1.1), (-0.11, 1.28), (-0.3, 1.52), (-0.48, 1.6)], -0.235, -0.222)
    prism("Glass", [(-0.3, 1.75), (0.26, 1.75), (0.12, 1.6), (0.02, 1.66), (-0.14, 1.54)], -0.235, -0.222)
    prism("Glass", [(0.48, 0.88), (0.14, 0.88), (0.26, 0.98), (0.37, 1.04), (0.48, 1.16)], -0.235, -0.222)
    for x, y, a in ((-0.05, -0.16, 0.3), (0.12, -0.2, -0.8), (0.32, -0.12, 1.4)):
        box("Glass", x - 0.05, x + 0.05, y - 0.025, y + 0.025, 0.895, 0.905, rz=a, bev=0.0)  # shards on the deck
    cyl("Steel", 0.0, 0.03, 0.89, 0.98, 0.11, seg=16)
    cyl("Steel", 0.0, 0.03, 0.97, 1.01, 0.15, seg=16)
    cyl("Steel", 0.0, 0.03, 1.38, 1.42, 0.15, seg=16)
    for i in range(3):
        a = i * 2 * math.pi / 3 + 0.5
        rod("Steel", (0.135 * math.cos(a), 0.03 + 0.135 * math.sin(a), 1.0), (0.135 * math.cos(a), 0.03 + 0.135 * math.sin(a), 1.39),
            0.01, seg=6)
    sphere("Vinyl", 0.0, 0.04, 1.11, 0.1, seg=12, sz=1.25)                                     # the specimen
    sphere("Vinyl", 0.04, 0.0, 1.22, 0.06, seg=10)
    rod("Rust", (0.0, 0.04, 1.2), (0.07, -0.02, 1.32), 0.012, seg=5)
    prism("Glass", [(-0.12, 1.0), (0.05, 1.0), (-0.02, 1.12), (-0.12, 1.2)], -0.1, -0.09)     # the canister's last pane
    rod("Rubber", (0.06, 0.06, 1.41), (0.1, 0.12, 1.74), 0.015, seg=8)
    rod("Rubber", (-0.06, 0.05, 1.41), (-0.16, 0.14, 1.74), 0.012, seg=8)
    box("Composite", -0.1, 0.1, -0.21, -0.17, 0.895, 0.94, rx=-0.4, bev=0.002)                # specimen tag
    dp.export("display_case")


def hall_bench():
    """Bolted locker-room bench: two steel pedestals on floor plates, a steel pan with vinyl pads — one pad torn
    off the right end, leaving rusted steel and a flap of foam — and a coiled hose underneath."""
    for sx in (-1, 1):
        x = sx * 0.7
        box("Steel", x - 0.09, x + 0.09, -0.22, 0.22, 0.0, 0.035)
        box("Paint", x - 0.04, x + 0.04, -0.18, 0.18, 0.025, 0.4, panel=True)
    box("Paint", -0.68, 0.68, -0.04, 0.04, 0.2, 0.28)
    box("Composite", -0.15, 0.15, -0.05, -0.03, 0.21, 0.27, bev=0.002)                        # stencil on the beam
    box("Steel", -0.9, 0.9, -0.24, 0.24, 0.38, 0.41)
    box("Vinyl", -0.88, -0.015, -0.23, 0.23, 0.4, 0.45, panel=True)
    box("Vinyl", 0.3, 0.88, -0.23, 0.23, 0.4, 0.445, panel=True)
    box("Rust", 0.0, 0.3, -0.2, 0.21, 0.4, 0.412, bev=0.0)                                     # the bare pan
    box("Composite", 0.2, 0.31, -0.22, 0.2, 0.41, 0.425, ry=0.3, bev=0.0)                      # torn foam flap
    torus("Rubber", (0.2, 0.02, 0.025), 0.15, 0.022, seg=16, rseg=6)
    torus("Rubber", (0.22, 0.03, 0.065), 0.14, 0.022, seg=16, rseg=6)
    rod("Rubber", (0.35, 0.05, 0.03), (0.58, -0.15, 0.022), 0.022, seg=6)
    dp.export("hall_bench")


def planter_large():
    """Hydroponic canister: a round composite tank, a cage of steel ribs with dark glass panels — the front ones
    smashed out — a dead tree specimen inside, a lamp cap and a pump box with hoses."""
    cy = 0.005
    cyl("Composite", 0.0, cy, 0.0, 0.45, 0.45, seg=16, r2=0.43)
    cyl("Steel", 0.0, cy, 0.44, 0.52, 0.42, seg=16)
    cyl("Rust", 0.0, cy, 0.51, 0.54, 0.38, seg=16)                                             # dry substrate
    box("Composite", -0.12, 0.12, cy - 0.45, cy - 0.41, 0.2, 0.32, bev=0.002)                  # stencil plate
    cyl("Paint", 0.0, cy, 1.62, 1.72, 0.42, seg=16)
    cyl("Composite", 0.0, cy, 1.71, 1.79, 0.25, seg=12)
    for k in range(8):
        a = math.radians(22.5 + 45 * k)
        rod("Steel", (0.39 * math.cos(a), cy + 0.39 * math.sin(a), 0.5), (0.39 * math.cos(a), cy + 0.39 * math.sin(a), 1.64), 0.018,
            seg=6)
    for k in range(8):
        th = math.radians(45 * k)
        if k in (6, 7):
            continue                                                                            # the front panes are gone
        cx, cyy = 0.36 * math.cos(th), cy + 0.36 * math.sin(th)
        top = 0.95 if k == 5 else 1.63
        box("Glass", cx - 0.16, cx + 0.16, cyy - 0.006, cyy + 0.006, 0.505, top, rz=th - math.pi / 2, bev=0.0)
    box("Glass", 0.2, 0.32, cy - 0.3, cy - 0.29, 1.5, 1.63, rz=math.radians(45), ry=0.3, bev=0.0)  # a shard at the top
    r = random.Random(41)
    rod("Rust", (0.0, cy, 0.52), (0.03, cy + 0.02, 0.95), 0.035, seg=6)
    rod("Rust", (0.03, cy + 0.02, 0.95), (-0.02, cy, 1.3), 0.027, seg=6)
    for i in range(7):
        a = r.random() * 2 * math.pi
        z = 0.95 + i * 0.045
        p0 = (0.02, cy + 0.01, z)
        p1 = (0.27 * math.cos(a), cy + 0.27 * math.sin(a), z + 0.15 + r.random() * 0.15)
        rod("Rust", p0, p1, 0.012, seg=5)
    rod("Rust", (0.0, cy, 1.05), (0.1, cy - 0.42, 0.75), 0.02, seg=5)                          # a branch through the hole
    box("Paint", 0.28, 0.47, 0.26, 0.44, 0.0, 0.3, panel=True)                                 # pump box
    rod("Rubber", (0.38, 0.35, 0.29), (0.43, 0.38, 0.9), 0.02, seg=8)
    rod("Rubber", (0.43, 0.38, 0.9), (0.33, 0.27, 1.66), 0.02, seg=8)
    dp.export("planter_large")


def sculpture():
    """A decommissioned security synth's torso on a plinth: armour shell slumped to one side, the head hanging with a
    dead lens, one arm a stump, the other socket empty with cables dangling, a stencilled plaque."""
    box("Steel", -0.3, 0.3, -0.3, 0.3, 0.0, 0.04, panel=True)
    box("Paint", -0.24, 0.24, -0.24, 0.24, 0.03, 0.95, panel=True)
    box("Composite", -0.1, 0.1, -0.252, -0.23, 0.7, 0.78, bev=0.002)
    box("Steel", -0.25, 0.25, -0.25, 0.25, 0.94, 0.98)
    cyl("Steel", 0.0, 0.02, 0.97, 1.22, 0.045, seg=12)
    box("Paint", -0.12, 0.12, -0.08, 0.12, 0.97, 1.08, panel=True)                             # pelvis block
    torso = [dp.tapered_box("Composite", -0.2, 0.2, -0.13, 0.13, 1.15, 1.48, back=0.78, bev=0.012),
             box("Paint", -0.17, -0.005, -0.145, -0.11, 1.2, 1.44, panel=True),                # one chest plate left
             box("Steel", 0.02, 0.15, -0.135, -0.12, 1.2, 1.24, bev=0.0),                      # ribs behind the gone one
             box("Steel", 0.02, 0.15, -0.135, -0.12, 1.29, 1.33, bev=0.0),
             box("Steel", 0.02, 0.15, -0.135, -0.12, 1.38, 1.42, bev=0.0),
             sphere("Paint", -0.2, 0.0, 1.43, 0.07, seg=10),
             rod("Steel", (-0.21, 0.0, 1.42), (-0.26, -0.06, 1.24), 0.03, seg=8),             # the arm stump
             sphere("Rust", -0.26, -0.06, 1.23, 0.04, seg=8),
             cyl("Rubber", 0.2, 0.0, 1.38, 1.46, 0.05, seg=10),                                # the empty socket
             cyl("Steel", 0.0, 0.0, 1.46, 1.56, 0.032, seg=10)]
    head = [box("Paint", -0.1, 0.1, -0.11, 0.09, 1.53, 1.67, panel=True),
            cyl("Composite", 0.03, 1.61, -0.135, -0.1, 0.045, seg=12, axis="Y"),
            cyl("Glass", 0.03, 1.61, -0.142, -0.125, 0.033, seg=12, axis="Y"),
            box("Rubber", -0.08, -0.02, -0.12, -0.1, 1.56, 1.58, bev=0.0)]
    turn(head, 0.3, "X", (0.0, 0.0, 1.54))
    turn(head, 0.15, "Y", (0.0, 0.0, 1.54))
    turn(torso + head, 0.1, "Y", (0.0, 0.0, 1.1))
    cable((0.22, -0.02, 1.38), (0.25, -0.08, 1.13), sag=0.02, r=0.008, n=4)
    cable((0.2, 0.03, 1.39), (0.21, 0.08, 1.08), sag=-0.02, r=0.006, n=4)
    dp.export("sculpture")


# --------------------------------------------------------------------------- control room

def supervisor_desk():
    """Supervisor's standing console: a steel top on painted side panels, a sloped switch bank at the front edge,
    two CRTs (the last report on one, the other's glass cracked), a keyboard, papers and a mug, a drawer hanging
    open under the top and cables to the floor."""
    T = 1.1
    for sx in (-1, 1):
        box("Paint", sx * 0.85 - 0.04, sx * 0.85 + 0.04, -0.38, 0.38, 0.0, T - 0.03, panel=True)
    box("Steel", -0.9, 0.9, -0.4, 0.4, T - 0.04, T)
    box("Paint", -0.82, 0.82, -0.36, 0.36, T - 0.1, T - 0.03)
    box("Paint", -0.82, 0.82, 0.3, 0.34, 0.4, T - 0.09)                                        # modesty panel
    box("Steel", -0.82, 0.82, -0.2, -0.14, 0.14, 0.19)                                         # foot rest
    a = 0.25
    box("Composite", -0.8, 0.8, -0.4, -0.18, T - 0.01, T + 0.05, rx=a)                         # switch bank
    r = random.Random(51)
    for row in range(2):
        y = -0.36 + row * 0.1
        z = T + 0.02 + 0.03 / math.cos(a) + (y + 0.29) * math.tan(a)                         # on the bank's top
        for i in range(12):
            x = -0.72 + i * 0.125
            k = r.random()
            if k < 0.55:
                box("Rubber", x, x + 0.04, y, y + 0.04, z - 0.012, z + 0.014, rx=a, bev=0.0)
            elif k < 0.75:
                box("Screen", x, x + 0.025, y + 0.008, y + 0.03, z - 0.012, z + 0.008, rx=a, bev=0.0)
            elif k < 0.9:
                box("Steel", x, x + 0.03, y + 0.005, y + 0.035, z - 0.012, z + 0.02, rx=a, bev=0.0)
    slim_crt(-0.3, 0.02, T - 0.01, 0.4, 0.3, 0.36, "ScreenText")
    slim_crt(0.3, 0.02, T - 0.01, 0.4, 0.3, 0.36, "Glass")
    for p, q in (((0.18, 0.006, 1.42), (0.3, 0.006, 1.3)), ((0.3, 0.006, 1.3), (0.42, 0.006, 1.36)),
                 ((0.3, 0.006, 1.3), (0.26, 0.006, 1.2))):
        rod("Rubber", p, q, 0.004, seg=4)                                                       # the crack
    lean_keyboard(-0.1, -0.08, T, missing={(2, 1), (4, 2)}, seed=52)
    forms(0.6, -0.12, T, 2, 53)
    dp.mug(0.72, 0.18, T - 0.012)
    box("Paint", -0.78, -0.42, -0.34, 0.26, T - 0.27, T - 0.09)                                # drawer unit
    box("Paint", -0.76, -0.44, -0.39, -0.33, T - 0.25, T - 0.13, ry=0.12, panel=True)         # its front, sagging out
    cable((-0.3, 0.36, T + 0.08), (-0.45, 0.37, 0.0), sag=0.1, r=0.008)
    cable((0.3, 0.36, T + 0.08), (0.55, 0.37, 0.0), sag=0.08, r=0.008)
    dp.export("supervisor_desk")


def console_desk():
    """Operator console: drawer pedestals (one drawer gone), a steel work top at desk height, behind it a raised
    turret with a sloped bank of toggles and lamps, three CRTs (snow, the last report, dead glass), two keyboards,
    a handset with its cord, papers, a mug and a cable bundle."""
    T = DESK_TOP
    for sx in (-1, 1):
        x = sx * 1.0
        box("Rubber", x - 0.17, x + 0.17, -0.38, 0.4, 0.0, 0.04)
        box("Paint", x - 0.18, x + 0.18, -0.4, 0.42, 0.03, T - 0.03, bev=0.01)
        for k, z in enumerate((0.1, 0.35, 0.6)):
            if sx < 0 and k == 1:
                box("Rubber", x - 0.15, x + 0.15, -0.41, -0.35, z, z + 0.2, bev=0.0)          # a drawer gone
                continue
            box("Paint", x - 0.16, x + 0.16, -0.415, -0.39, z, z + 0.22, bev=0.004)
            box("Steel", x - 0.06, x + 0.06, -0.43, -0.41, z + 0.15, z + 0.18, bev=0.002)
    box("Steel", -1.2, 1.2, -0.45, 0.0, T - 0.04, T)
    prism("Paint", [(-0.02, 0.4), (0.45, 0.4), (0.45, 1.1), (0.16, 1.1), (-0.02, 0.95)], -1.17, 1.17, plane="yz")
    a = math.atan2(0.15, 0.18)
    box("Composite", -1.12, 1.12, 0.07 - 0.115, 0.07 + 0.115, 1.025 - 0.011, 1.025 + 0.011, rx=a, bev=0.0)
    r = random.Random(61)
    n = (-math.sin(a), math.cos(a))
    for t in (0.3, 0.7):
        for x0 in (-0.75, 0.0, 0.75):
            for k in range(6):
                x = x0 - 0.27 + k * 0.1
                y, z = -0.02 + 0.18 * t + n[0] * 0.012, 0.95 + 0.15 * t + n[1] * 0.012
                kind = r.random()
                if kind < 0.55:
                    box("Rubber", x, x + 0.035, y - 0.016, y + 0.016, z - 0.012, z + 0.012, rx=a, bev=0.0)
                elif kind < 0.75:
                    box("Screen", x, x + 0.03, y - 0.01, y + 0.01, z - 0.012, z + 0.006, rx=a, bev=0.0)
    for cx, kind in ((-0.72, "ScreenStatic"), (0.0, "ScreenText"), (0.72, "Glass")):
        slim_crt(cx, 0.16, 1.09, 0.4, 0.24, 0.28, kind)
    lean_keyboard(-0.38, -0.22, T, missing={(1, 2)}, seed=62)
    lean_keyboard(0.38, -0.22, T, missing={(3, 0), (0, 1)}, seed=63)
    box("Rubber", 0.86, 1.06, -0.3, -0.12, T - 0.004, T + 0.05, bev=0.008)                     # handset cradle
    box("Composite", 0.88, 1.04, -0.25, -0.17, T + 0.04, T + 0.07, rz=0.15, bev=0.006)
    cable((0.96, -0.15, T + 0.03), (0.9, 0.0, 0.97), sag=0.06, r=0.005, n=6)
    forms(-0.88, -0.22, T, 2, 64)
    dp.mug(-1.08, -0.06, T - 0.012)
    cable((0.5, 0.44, 0.95), (0.72, 0.43, 0.0), sag=0.12, r=0.012, n=5)
    dp.export("console_desk")


def monitor_wall():
    """Bank of CRTs: a painted steel rack of three shelves with eight monitors (snow, the last report, dead glass)
    and one empty slot with its cables hanging, a cabinet below with one door missing."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 1.15 - 0.025, sx * 1.15 + 0.025, sy * 0.12 - 0.025, sy * 0.12 + 0.025, 0.0, 2.37)
    for z in (0.82, 1.34, 1.86):
        box("Steel", -1.19, 1.19, -0.15, 0.15, z, z + 0.03)
    box("Paint", -1.19, 1.19, -0.15, 0.15, 2.34, 2.398, panel=True)
    box("Composite", -0.3, 0.3, -0.16, -0.14, 2.35, 2.39, bev=0.0)                             # stencil
    box("Paint", -1.14, 1.14, -0.13, 0.13, 0.04, 0.83, panel=True)                             # the cabinet
    box("Paint", -1.12, -0.02, -0.145, -0.125, 0.07, 0.8, panel=True)
    box("Rubber", 0.02, 1.12, -0.135, -0.11, 0.07, 0.8, bev=0.0)                               # the other door gone
    for k in range(2):
        cable((0.25 + k * 0.45, -0.1, 0.78), (0.2 + k * 0.5, -0.12, 0.09), sag=-0.06 - 0.04 * k, r=0.012, n=4)
    kinds = [["ScreenStatic", "Glass", "ScreenText"], ["ScreenStatic", None, "ScreenStatic"], ["Glass", "ScreenStatic", "ScreenText"]]
    for row, z in enumerate((0.85, 1.37, 1.89)):
        for col, cx in enumerate((-0.75, 0.0, 0.75)):
            kind = kinds[row][col]
            if row == 1 and col == 1:
                cable((-0.05, 0.05, 1.86), (0.06, -0.1, 1.41), sag=-0.04, r=0.01, n=4)        # the empty slot
                cable((0.08, 0.08, 1.86), (-0.12, -0.08, 1.48), sag=-0.03, r=0.008, n=3)
                continue
            slim_crt(cx, -0.14, z - 0.01, 0.5, 0.34, 0.27, kind)
    dp.export("monitor_wall")


def globe():
    """Planetary survey sphere: a steel tripod and column, an outer gimbal ring and a turned inner ring on pivots,
    a plated sphere with seam bands and one plate gone, a sensor nub on top, a loose cable."""
    for i in range(3):
        a = math.radians(90 + 120 * i)
        rod("Steel", (0.0, 0.0, 0.3), (0.26 * math.cos(a), 0.26 * math.sin(a), 0.03), 0.018, seg=8)
        sphere("Rubber", 0.26 * math.cos(a), 0.26 * math.sin(a), 0.03, 0.03, seg=8)
    cyl("Paint", 0.0, 0.0, 0.25, 0.35, 0.06, seg=12)
    cyl("Steel", 0.0, 0.0, 0.33, 0.48, 0.03, seg=10)
    zc = 0.7
    torus("Paint", (0.0, 0.0, zc), 0.27, 0.016, axis="Y", seg=28, rseg=6)
    inner = torus("Steel", (0.0, 0.0, 0.0), 0.24, 0.012, axis="X", seg=28, rseg=6)
    bmesh.ops.rotate(inner, cent=Vector(), matrix=Matrix.Rotation(math.radians(30), 3, "Z"), verts=inner.verts)
    bmesh.ops.translate(inner, vec=Vector((0, 0, zc)), verts=inner.verts)
    cyl("Steel", 0.0, 0.0, zc + 0.22, zc + 0.28, 0.014, seg=8)                                 # pivots
    cyl("Steel", 0.0, 0.0, zc - 0.28, zc - 0.235, 0.014, seg=8)
    d = Vector((-math.sin(math.radians(30)), math.cos(math.radians(30)), 0.0))
    rod("Steel", tuple(Vector((0, 0, zc)) - d * 0.245), tuple(Vector((0, 0, zc)) + d * 0.245), 0.012, seg=8)
    sph = sphere("Composite", 0.0, 0.0, 0.0, 0.2, seg=16)
    bands = [torus("Rubber", (0.0, 0.0, 0.0), 0.199, 0.006, axis="Z", seg=24, rseg=4),
             torus("Rubber", (0.0, 0.0, 0.0), 0.199, 0.006, axis="X", seg=24, rseg=4)]
    patch = box("Rust", -0.06, 0.06, -0.205, -0.17, -0.05, 0.06, bev=0.0)
    nub = cyl("Steel", 0.0, 0.0, 0.19, 0.24, 0.02, seg=8)
    for bm in [sph, *bands, patch, nub]:
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(0.4, 3, "Y"), verts=bm.verts)
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.radians(-20), 3, "Z"), verts=bm.verts)
        bmesh.ops.translate(bm, vec=Vector((0, 0, zc)), verts=bm.verts)
    cable((0.0, 0.03, 0.42), (0.12, -0.22, 0.0), sag=0.04, r=0.006, n=5)
    dp.export("globe")


# --------------------------------------------------------------------------- library and copy room

def reading_table():
    """Document inspection table: a steel top on painted legs, a light box with film, two hooded inspection lamps
    on clamped arms (one drooping, its bulb dead), papers, folders and a mug, archive cases on the shelf below."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.9 - 0.03, sx * 0.9 + 0.03, sy * 0.37 - 0.03, sy * 0.37 + 0.03, 0.0, 0.7)
    box("Paint", -0.95, 0.95, -0.41, 0.41, 0.66, 0.73)
    box("Steel", -1.0, 1.0, -0.45, 0.45, 0.72, 0.76)
    box("Steel", -0.94, 0.94, -0.38, 0.38, 0.15, 0.18)
    r = random.Random(71)
    x = -0.85
    while x < 0.7:
        w = 0.25 + r.random() * 0.15
        if r.random() < 0.8:
            box("Composite", x, x + w, -0.3, 0.3, 0.17, 0.17 + 0.18 + r.random() * 0.1, rz=(r.random() - 0.5) * 0.1, bev=0.008)
        x += w + 0.06
    for x, droop, bulb in ((-0.5, 0.0, "Screen"), (0.5, 0.09, "Glass")):
        box("Paint", x - 0.04, x + 0.04, 0.37, 0.46, 0.69, 0.8)
        rod("Paint", (x, 0.42, 0.79), (x, 0.3, 1.02 - droop), 0.014, seg=8)
        rod("Paint", (x, 0.3, 1.02 - droop), (x + 0.02, 0.04, 1.04 - droop), 0.012, seg=8)
        sphere("Steel", x, 0.3, 1.02 - droop, 0.022, seg=8)
        cyl("Paint", x + 0.02, 0.0, 0.98 - droop, 1.08 - droop, 0.12, seg=14, r2=0.06)
        cyl(bulb, x + 0.02, 0.0, 0.974 - droop, 0.99 - droop, 0.1, seg=14)
        cable((x, 0.44, 0.75), (x + 0.15, 0.44, 0.0), sag=0.05, r=0.006)
    box("Composite", -0.32, 0.18, -0.32, 0.04, 0.755, 0.79, bev=0.004)                         # light box
    box("Glass", -0.29, 0.15, -0.29, 0.01, 0.786, 0.794, bev=0.0)
    for k in range(3):
        box("Rubber", -0.25 + k * 0.13, -0.15 + k * 0.13, -0.24, -0.06, 0.792, 0.796, rz=0.05 * k, bev=0.0)
    forms(0.42, -0.22, 0.76, 3, 72)
    for k in range(3):
        box("Vinyl" if k % 2 else "Composite", 0.62, 0.92, 0.05, 0.3, 0.752 + k * 0.025, 0.787 + k * 0.025, rz=0.08 * k - 0.05, bev=0.003)
    dp.mug(-0.8, -0.2, 0.75)
    dp.export("reading_table")


def tall_bookcase():
    """Tape library: a tall painted cabinet with shelves of cartridge magazines (gaps, a few fallen), a picker
    gantry stuck halfway up its rail with its cable sagging, a stencilled header with status pips."""
    for sx in (-1, 1):
        box("Paint", sx * 0.482 - 0.015, sx * 0.482 + 0.015, -0.17, 0.172, 0.0, 2.15, panel=True)
    box("Paint", -0.48, 0.48, 0.14, 0.175, 0.05, 2.15)
    box("Rubber", -0.48, 0.48, -0.16, 0.15, 0.0, 0.08)
    box("Paint", -0.5, 0.5, -0.175, 0.175, 2.08, 2.2, panel=True)
    box("Composite", -0.32, 0.22, -0.18, -0.165, 2.11, 2.17, bev=0.0)
    for k in range(3):
        box("Screen" if k != 1 else "Rubber", 0.28 + k * 0.04, 0.3 + k * 0.04, -0.18, -0.165, 2.13, 2.15, bev=0.0)
    r = random.Random(81)
    shelves = (0.08, 0.5, 0.92, 1.34, 1.76)
    for i, z in enumerate(shelves):
        box("Steel", -0.48, 0.48, -0.165, 0.15, z, z + 0.03, bev=0.003)
        box("Steel", -0.48, 0.48, -0.15, 0.14, z + 0.17, z + 0.19, bev=0.0)                     # a middle rail
        for tier in range(2):                                                                   # cartridge magazines
            zb = z + 0.02 + tier * 0.16
            for m in range(4):
                x0 = -0.41 + m * 0.215
                k = r.random()
                if k < 0.12 or (i, tier, m) == (2, 1, 1):
                    continue                                                                   # an empty slot
                if (i, tier, m) == (3, 0, 2):                                                  # one knocked askew
                    box("Composite", x0, x0 + 0.2, -0.14, 0.12, zb, zb + 0.12, rz=0.18, bev=0.0)
                    continue
                out = 0.025 if (i, tier, m) == (1, 0, 0) else 0.0                              # one pulled out a little
                box("Composite", x0, x0 + 0.2, -0.14 - out, 0.12 - out, zb, zb + 0.16, bev=0.0)
                box("Rubber", x0 + 0.015, x0 + 0.185, -0.147 - out, -0.13 - out, zb + 0.035, zb + 0.125, bev=0.0)
                box("Paint", x0 + 0.07, x0 + 0.13, -0.152 - out, -0.13 - out, zb + 0.008, zb + 0.03, bev=0.0)
    box("Steel", -0.465, -0.43, -0.175, -0.145, 0.1, 2.09, bev=0.002)                         # the picker rail
    box("Paint", -0.47, -0.3, -0.175, -0.12, 1.1, 1.24, rz=0.06, panel=True)                   # the picker, stuck
    box("Rubber", -0.42, -0.34, -0.176, -0.16, 1.13, 1.16, bev=0.0)
    cable((-0.35, -0.15, 1.12), (-0.2, -0.16, 0.12), sag=-0.04, r=0.008, n=6)
    dp.export("tall_bookcase")


def copier():
    """Records duplicator: a painted cabinet with paper drawers (one missing) on a rubber plinth, a composite print
    engine, a scanner lid propped open, a feeder, a finisher tower with sheets jammed in its trays, a tilted control
    panel with a snowy status screen where ours glows, and a power cable to the floor."""
    box("Rubber", -0.46, 0.45, -0.28, 0.3, 0.0, 0.06)
    box("Paint", -0.48, 0.2, -0.3, 0.31, 0.05, 0.8, panel=True)
    for k, z in enumerate((0.1, 0.3, 0.5)):
        if k == 1:
            box("Rubber", -0.45, 0.17, -0.315, -0.28, z, z + 0.17, bev=0.0)                   # a drawer gone
            continue
        box("Paint", -0.46, 0.18, -0.32, -0.295, z, z + 0.18, panel=True)
        box("Steel", -0.22, -0.06, -0.333, -0.305, z + 0.12, z + 0.145, bev=0.002)
    box("Composite", -0.48, 0.2, -0.3, 0.31, 0.79, 0.96, panel=True)
    box("Rust", -0.48, -0.38, -0.305, -0.29, 0.8, 0.86, bev=0.0)
    box("Paint", -0.47, 0.19, -0.28, 0.3, 0.95, 1.0, panel=True)
    box("Glass", -0.43, 0.15, -0.24, 0.26, 0.995, 1.003, bev=0.0)
    box("Composite", -0.47, 0.19, -0.05, 0.31, 1.0, 1.04, rx=-0.55, ry=0.03, panel=True)       # the lid, propped open
    box("Composite", -0.46, -0.28, -0.27, 0.0, 0.99, 1.08, bev=0.02)                           # the feeder
    box("Paint", 0.19, 0.47, -0.26, 0.3, 0.05, 0.95, panel=True)                               # finisher tower
    for z in (0.55, 0.75):
        box("Composite", 0.21, 0.46, -0.27, 0.28, z, z + 0.015, rx=0.0, bev=0.0)
        for k in range(3):
            box("Composite", 0.23, 0.45, -0.24 + k * 0.02, -0.03 + k * 0.03, z + 0.012 + k * 0.004, z + 0.016 + k * 0.004,
                rz=0.1 * k - 0.1, bev=0.0)
    box("Composite", 0.25, 0.44, -0.18, 0.12, 0.94, 1.0, rx=0.2, ry=-0.15, bev=0.0)          # a jammed sheet
    box("Composite", -0.12, 0.16, -0.333, -0.22, 0.95, 1.0, rx=-0.35, bev=0.004)              # control panel
    box("ScreenStatic", -0.06, 0.1, -0.336, -0.27, 0.975, 1.003, rx=-0.35, bev=0.0)
    for k in range(4):
        box("Rubber", 0.11 + (k % 2) * 0.025, 0.13 + (k % 2) * 0.025, -0.29 + (k // 2) * 0.03, -0.27 + (k // 2) * 0.03, 0.96, 0.995,
            bev=0.0)
    cable((0.0, 0.31, 0.6), (0.1, 0.32, 0.0), sag=0.05, r=0.008)
    dp.export("copier")


def paper_stack():
    """Supply pallet: a steel pallet with strapped composite cases, one strap snapped, an opened case with bundles
    of yellowed forms and its lid propped up, loose sheets on the other."""
    for x in (-0.34, 0.0, 0.34):
        box("Steel", x - 0.05, x + 0.05, -0.29, 0.29, 0.0, 0.1)
    box("Steel", -0.4, 0.4, -0.3, 0.3, 0.09, 0.125, panel=True)
    for x0, x1 in ((-0.39, -0.005), (0.005, 0.39)):
        box("Composite", x0, x1, -0.29, 0.29, 0.115, 0.4, panel=True)
        box("Paint", x0 + 0.1, x1 - 0.1, -0.302, -0.28, 0.28, 0.36, bev=0.0)                  # labels
    for x in (-0.2, 0.2):
        box("Rubber", x - 0.02, x + 0.02, -0.295, 0.295, 0.11, 0.405, bev=0.0)                 # straps
    rod("Rubber", (0.2, -0.293, 0.36), (0.25, -0.29, 0.13), 0.008, seg=4)                     # the snapped one hangs
    box("Composite", -0.37, 0.04, -0.27, 0.27, 0.39, 0.62, rz=0.05, panel=True)
    forms(-0.17, 0.0, 0.612, 2, 91)
    box("Composite", 0.06, 0.38, -0.25, 0.22, 0.39, 0.41)                                      # the open case
    for x0, x1, y0, y1 in ((0.075, 0.365, -0.248, -0.228), (0.075, 0.365, 0.198, 0.218), (0.06, 0.08, -0.25, 0.22), (0.36, 0.38, -0.25, 0.22)):
        box("Composite", x0, x1, y0, y1, 0.4, 0.592 if x1 - x0 > 0.1 else 0.6, bev=0.003)
    for k in range(3):
        box("Composite" if k != 1 else "Vinyl", 0.08 + k * 0.095, 0.165 + k * 0.095, -0.22, 0.19, 0.405, 0.53 + 0.02 * k,
            rz=0.03 * (k - 1), bev=0.002)
    box("Composite", 0.06, 0.38, 0.215, 0.25, 0.59, 0.785, rx=-0.25, bev=0.004)               # lid propped up
    dp.export("paper_stack")


def shredder():
    """Secure disposal unit: a painted steel bin on casters with its hatch ajar, a composite head with a steel slot
    plate and torn strips sticking out, a dim status lamp where ours glows, a stencil plate."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            sphere("Rubber", sx * 0.17, sy * 0.13, 0.025, 0.025, seg=8)
    box("Paint", -0.21, 0.21, -0.17, 0.16, 0.03, 0.58, panel=True)
    box("Paint", -0.17, 0.17, -0.185, -0.162, 0.1, 0.46, rz=0.07, panel=True)                  # the hatch, ajar
    box("Steel", 0.05, 0.13, -0.192, -0.165, 0.36, 0.39, rz=0.07, bev=0.002)
    box("Rust", -0.2, -0.1, -0.175, -0.165, 0.04, 0.12, bev=0.0)
    box("Composite", -0.12, 0.08, -0.183, -0.16, 0.5, 0.55, bev=0.0)                           # stencil plate
    box("Composite", -0.23, 0.23, -0.19, 0.18, 0.57, 0.69, panel=True)
    box("Steel", -0.17, 0.17, -0.045, 0.045, 0.68, 0.705)
    box("Rubber", -0.15, 0.15, -0.008, 0.008, 0.69, 0.707, bev=0.0)
    for k, x in enumerate((-0.1, -0.04, 0.05, 0.11)):
        box("Composite", x, x + 0.03, -0.005, 0.005, 0.69, 0.718 + 0.004 * (k % 2), ry=0.15 * (k - 1.5), bev=0.0)
    box("Screen", 0.15, 0.17, -0.194, -0.18, 0.635, 0.655, bev=0.0)
    dp.export("shredder")


def operator_chair():
    """Operator chair: a five-star base with one caster gone (that leg resting on the floor), a gas column, a torn
    vinyl seat with foam showing, a tall back with a crooked headrest, one armrest, the other snapped off."""
    for i in range(5):
        a = -math.pi / 2 + i * 2 * math.pi / 5
        tip = 0.28
        z_end = 0.03 if i == 2 else 0.065
        rod("Paint", (0, 0, 0.1), (tip * math.cos(a), tip * math.sin(a), z_end), 0.02, seg=8)
        if i != 2:
            sphere("Rubber", tip * math.cos(a), tip * math.sin(a), 0.035, 0.035, seg=10)
    cyl("Paint", 0, 0, 0.06, 0.14, 0.05, seg=12)
    cyl("Steel", 0, 0, 0.12, 0.46, 0.025, seg=12)
    box("Paint", -0.12, 0.12, -0.12, 0.12, 0.42, 0.47)
    box("Vinyl", -0.27, 0.27, -0.31, 0.2, 0.45, 0.55, panel=True)
    sphere("Composite", 0.1, -0.12, 0.545, 0.08, seg=10, sz=0.35)                              # foam through the tear
    box("Paint", -0.035, 0.035, 0.17, 0.27, 0.45, 0.8)                                         # spine
    box("Vinyl", -0.24, 0.24, 0.22, 0.31, 0.6, 1.05, rx=-0.1, panel=True)
    box("Vinyl", -0.14, 0.14, 0.27, 0.34, 1.02, 1.145, rz=0.12, panel=True)                    # headrest, crooked
    box("Paint", -0.3, -0.25, -0.08, -0.02, 0.5, 0.68)
    box("Vinyl", -0.335, -0.255, -0.2, 0.12, 0.66, 0.7, bev=0.01)
    box("Paint", 0.25, 0.3, -0.08, -0.02, 0.5, 0.6)
    box("Rust", 0.248, 0.302, -0.082, -0.018, 0.59, 0.61, bev=0.0)                             # snapped off
    cable((0.3, -0.05, 0.6), (0.31, -0.12, 0.47), sag=-0.02, r=0.005, n=3)
    dp.export("operator_chair")


PROPS = [directory_board, map_board, display_case, hall_bench, planter_large, sculpture, operator_chair, supervisor_desk,
         console_desk, monitor_wall, globe, reading_table, tall_bookcase, copier, paper_stack, shredder]

if __name__ == "__main__":
    dp.clear()
    for make in PROPS:
        make()
    print("DONE")
