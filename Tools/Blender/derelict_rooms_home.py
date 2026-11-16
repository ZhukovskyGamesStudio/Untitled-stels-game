"""Derelict replacements of the home group (kitchen, washroom, lounge, corridor; rooms2_home.py and rooms_wet.py):
the galley, the crew quarters and the decon washrooms of the abandoned station, in stainless steel and composite.

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_rooms_home.py -- Assets/_Game/Art/Models/StyleProps/Derelict [names…]

Same contract as derelict_props.py (its helpers, surfaces and export): <out>/<our model>.fbx, modelled to our model's
bounds (Art/Models/Props/<name>.fbx; x right, -y the front, standing as ours). Decoration only, deterministic.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derelict_props as dp  # noqa: E402

box, cyl, rod, cable, sphere, screen = dp.box, dp.cyl, dp.rod, dp.cable, dp.sphere, dp.screen


def floor_plate(x, y, w=0.12, d=0.12):
    """A floor anchor plate with four bolts: everything here is bolted down."""
    box("Steel", x - w / 2, x + w / 2, y - d / 2, y + d / 2, 0.0, 0.015)
    xs = (x - w / 2 + 0.018, x + w / 2 - 0.018) if w > 0.07 else (x,)
    for bx in xs:
        for by in (y - d / 2 + 0.018, y + d / 2 - 0.018):
            box("Steel", bx - 0.008, bx + 0.008, by - 0.008, by + 0.008, 0.008, 0.024, rz=0.3, bev=0.0)


def crack(points, y, r=0.0035):
    """Cracks across a pane lying in the plane y (a chain of thin dark rods)."""
    for a, b in zip(points, points[1:]):
        rod("Rubber", (a[0], y, a[1]), (b[0], y, b[1]), r, seg=4)


# --------------------------------------------------------------------------- floor and lounge

def area_rug():
    """A stained rubber anti-slip floor mat: ribbed tread, a steel drain grate in the middle, a rust stain, one corner
    torn off."""
    box("Rubber", -1.0, 0.94, -0.7, 0.7, 0.0, 0.012, bev=0.003)
    box("Rubber", 0.92, 1.0, -0.7, 0.55, 0.0, 0.012, bev=0.003)                          # the torn corner (+x, -y.. missing)
    for k in range(19):
        x = -0.9 + k * 0.1
        if abs(x) < 0.2:
            continue
        box("Rubber", x - 0.018, x + 0.018, -0.62, 0.62 if x < 0.85 else 0.5, 0.004, 0.02, bev=0.003)
    box("Steel", -0.18, 0.18, -0.24, 0.24, 0.002, 0.018, bev=0.003)          # drain grate
    for k in range(6):
        y = -0.18 + k * 0.072
        box("Rust", -0.15, 0.15, y - 0.012, y + 0.012, 0.006, 0.0215, bev=0.002)
    box("Rust", -0.75, -0.35, 0.15, 0.55, 0.004, 0.0135, rz=0.4, bev=0.002)              # a rust stain
    box("Rust", 0.3, 0.6, -0.55, -0.35, 0.004, 0.0135, rz=-0.2, bev=0.002)
    dp.export("area_rug")


def sectional_sofa():
    """Bolted lounge seating in an L: five vinyl bucket seats on two steel beams, one seat broken and hanging, the
    corner taken by a battered equipment case with a dead intercom panel."""
    # long wing along the back
    box("Paint", -1.15, 0.62, 0.3, 0.4, 0.28, 0.38)
    for x in (-0.95, 0.4):
        box("Paint", x - 0.04, x + 0.04, 0.31, 0.39, 0.005, 0.3)
        floor_plate(x, 0.35, 0.16, 0.2)
    for i, x in enumerate((-0.86, -0.26, 0.32)):
        broken = i == 1
        box("Paint", x - 0.05, x + 0.05, 0.05, 0.5, 0.36, 0.4)                          # seat bracket
        box("Vinyl", x - 0.27, x + 0.27, -0.04, 0.52, 0.38, 0.47, rx=0.35 if broken else 0.0, panel=broken)
        box("Vinyl", x - 0.26, x + 0.26, 0.58, 0.7, 0.46, 0.83, rx=-0.15 - (0.3 if broken else 0.0))
        box("Paint", x - 0.045, x + 0.045, 0.42, 0.64, 0.36, 0.52, rx=-0.4)                # back bracket
    for x in (-1.16, -0.56, 0.03, 0.6):                                                   # armrests
        box("Paint", x - 0.025, x + 0.025, 0.0, 0.52, 0.46, 0.5)
        box("Paint", x - 0.02, x + 0.02, 0.33, 0.39, 0.33, 0.49)
    sphere("Composite", -0.2, 0.25, 0.47, 0.08, seg=10, sz=0.5)                           # foam out of the broken seat
    # the return wing along +x
    box("Paint", 0.8, 0.9, -0.8, 0.3, 0.28, 0.38)
    for y in (-0.62, 0.15):
        box("Paint", 0.81, 0.89, y - 0.04, y + 0.04, 0.005, 0.3)
        floor_plate(0.85, y, 0.2, 0.16)
    for y in (-0.48, 0.06):
        box("Paint", 0.6, 1.05, y - 0.05, y + 0.05, 0.36, 0.4)
        box("Vinyl", 0.58, 1.06, y - 0.26, y + 0.26, 0.38, 0.47)
        box("Vinyl", 1.07, 1.19, y - 0.25, y + 0.25, 0.46, 0.83, ry=0.15)
        box("Paint", 1.0, 1.12, y - 0.045, y + 0.045, 0.36, 0.52, ry=0.4)
    box("Paint", 0.56, 1.06, -0.8, -0.75, 0.46, 0.5)
    box("Paint", 0.58, 0.64, -0.8, -0.74, 0.36, 0.48)
    # the corner: an equipment case with an intercom on its face
    box("Composite", 0.61, 1.19, 0.35, 0.79, 0.0, 0.62)
    box("Steel", 0.6, 1.2, 0.34, 0.8, 0.6, 0.64)
    for x in (0.7, 1.1):
        box("Steel", x - 0.04, x + 0.04, 0.325, 0.37, 0.42, 0.52)                          # latches
    box("Paint", 0.78, 1.02, 0.335, 0.365, 0.14, 0.34)
    for k in range(4):
        box("Rubber", 0.8, 0.94, 0.328, 0.345, 0.27 - k * 0.025, 0.28 - k * 0.025, bev=0.0)
    box("Rubber", 0.96, 0.99, 0.328, 0.345, 0.27, 0.3, bev=0.0)
    cable((0.9, 0.7, 0.6), (1.15, 0.78, 0.0), sag=-0.05, r=0.008)
    dp.export("sectional_sofa")


def egg_chair():
    """Crew rest pod seat: a composite shell with a hood on a swivel column, its vinyl cushion torn open to the foam, a
    reading lamp dead in the hood, a cable to the floor."""
    cyl("Paint", 0, 0, 0.0, 0.05, 0.3, seg=20)
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        cyl("Steel", 0.24 * math.cos(a), 0.24 * math.sin(a), 0.04, 0.062, 0.016, seg=6)
    cyl("Steel", 0, 0, 0.04, 0.4, 0.05, seg=14)
    cyl("Rubber", 0, 0, 0.3, 0.36, 0.065, seg=14)                                         # gas spring boot
    box("Composite", -0.32, 0.32, -0.3, 0.28, 0.37, 0.46)                     # seat pan
    box("Vinyl", -0.29, 0.29, -0.32, 0.22, 0.44, 0.53, rx=0.04, panel=True)                # cushion
    box("Vinyl", -0.2, 0.05, -0.36, -0.28, 0.42, 0.5, rx=0.6, ry=0.2)                      # the torn flap
    sphere("Composite", -0.06, -0.18, 0.51, 0.1, seg=10, sz=0.45)                          # the foam
    box("Composite", -0.37, 0.37, 0.24, 0.38, 0.4, 1.22, rx=-0.06, panel=True)             # back shell
    for sx in (-1, 1):                                                                      # side wings
        box("Composite", sx * 0.385 - 0.05 * sx, sx * 0.385, -0.2, 0.32, 0.4, 1.16)
        box("Paint", sx * 0.385 - 0.05 * sx, sx * 0.385, -0.24, -0.17, 0.4, 1.1)            # the wings' edge band
    box("Composite", -0.385, 0.385, -0.12, 0.37, 1.16, 1.28, rx=0.12)          # hood
    box("Vinyl", -0.28, 0.28, 0.17, 0.27, 0.5, 1.08, rx=-0.06, panel=True)                 # back pad
    box("Rust", 0.12, 0.27, 0.165, 0.2, 0.7, 0.95, rx=-0.06, bev=0.002)                    # a tear down the back pad
    cyl("Composite", 0.0, -0.02, 1.1, 1.19, 0.06, seg=12, r2=0.05)                         # the reading lamp
    cyl("Rubber", 0.0, -0.02, 1.095, 1.11, 0.045, seg=12)
    cable((0.2, 0.36, 0.5), (0.25, 0.28, 0.03), sag=-0.04, r=0.007)
    dp.export("egg_chair")


def side_table():
    """Instrument stand: a steel tray on a tripod column, a caged work lamp at the back (its bulb where our shade is),
    a clipboard on the tray."""
    cyl("Steel", 0, 0, 0.52, 0.55, 0.245, seg=24)
    cyl("Steel", 0, 0, 0.54, 0.56, 0.245, seg=24, r2=0.238)                                # the tray's lip
    cyl("Rust", 0, 0, 0.55, 0.566, 0.19, seg=20)
    cyl("Paint", 0, 0, 0.06, 0.54, 0.025, seg=12)
    cyl("Paint", 0, 0, 0.3, 0.33, 0.04, seg=12)                                            # height clamp
    for k in range(3):
        a = k * 2 * math.pi / 3 + 0.3
        rod("Paint", (0, 0, 0.1), (0.22 * math.cos(a), 0.22 * math.sin(a), 0.02), 0.014)
        sphere("Rubber", 0.22 * math.cos(a), 0.22 * math.sin(a), 0.022, 0.022, seg=8)
    lx, ly = 0.07, 0.08
    cyl("Composite", lx, ly, 0.535, 0.6, 0.06, seg=14)
    rod("Steel", (lx, ly, 0.58), (lx, ly, 0.8), 0.008)
    cyl("Composite", lx, ly, 0.84, 0.95, 0.1, seg=14, r2=0.06)                              # shade
    sphere("Screen", lx, ly, 0.83, 0.04, seg=10)                                            # the bulb, barely alive
    for k in range(4):                                                                       # the cage
        a = k * math.pi / 2 + 0.6
        rod("Steel", (lx + 0.1 * math.cos(a), ly + 0.1 * math.sin(a), 0.86), (lx + 0.05 * math.cos(a), ly + 0.05 * math.sin(a), 0.77), 0.004, seg=4)
    cyl("Steel", lx, ly, 0.765, 0.775, 0.055, seg=12)
    box("Composite", -0.2, -0.0, -0.2, 0.06, 0.552, 0.574, rz=0.3)                            # clipboard
    box("Steel", -0.14, -0.06, -0.0, 0.03, 0.562, 0.585, rz=0.3)
    dp.papers(-0.1, -0.07, 0.5732, 2, 31, spread=0.02)
    cable((lx, ly + 0.05, 0.58), (0.2, 0.2, 0.02), sag=0.05, r=0.006)
    dp.export("side_table")


def magazine_rack():
    """Procedure-manual rack: a steel wall rack of three slanted pockets with laminated checklists and binders, one
    pocket bent, a sheet fallen to the foot."""
    for x in (-0.28, 0.28):
        box("Paint", x - 0.02, x + 0.02, 0.1, 0.16, 0.005, 1.0)                              # rear upright
        rod("Paint", (x, -0.15, 0.01), (x, 0.12, 0.98), 0.018)                                # leaning front strut
        floor_plate(x, -0.09, 0.04, 0.12)
    box("Paint", -0.297, 0.297, 0.11, 0.165, 0.92, 0.98)
    r = random.Random(41)
    for k, zb in enumerate((0.1, 0.42, 0.74)):
        yf = -0.15 + 0.27 * zb / 0.98
        bent = k == 1
        box("Steel", -0.285, 0.285, yf - 0.01, yf + 0.1, zb, zb + 0.02, rz=0.02 if bent else 0.0)
        box("Paint", -0.275, 0.275, yf - 0.015, yf + 0.005, zb - 0.005, zb + 0.08, ry=0.05 if bent else 0.0)   # pocket lip
        box("Steel", -0.283, 0.283, yf + 0.08, yf + 0.095, zb + 0.01, zb + 0.25)                         # back board
        for j in range(3):
            mx = -0.17 + j * 0.17 + r.uniform(-0.02, 0.02)
            surf = ("Composite", "Vinyl", "Rust")[(k + j) % 3]
            th = 0.012 if surf == "Composite" else 0.035
            box(surf, mx - 0.075, mx + 0.075, yf + 0.03, yf + 0.03 + th, zb + 0.01, zb + 0.23 + r.uniform(-0.03, 0.02),
                rx=-0.15, ry=r.uniform(-0.05, 0.05))
    box("Composite", -0.12, 0.08, -0.1, 0.06, 0.0, 0.003, rz=0.4, bev=0.0)                  # the fallen sheet
    dp.export("magazine_rack")


def area_bench_frame(x0, x1, z_top, y=0.17):
    """Two trestles under a bench seat: square tube posts, a top rail and a floor foot bolted down."""
    for x in (x0, x1):
        for sy in (-y, y):
            box("Paint", x - 0.02, x + 0.02, sy - 0.02, sy + 0.02, 0.005, z_top - 0.01)
        box("Paint", x - 0.025, x + 0.025, -y - 0.03, y + 0.03, z_top - 0.04, z_top)
        box("Paint", x - 0.018, x + 0.018, -y, y, 0.09, 0.14)                                 # low cross bar
        box("Steel", x - 0.04, x + 0.04, -y - 0.03, y + 0.03, 0.0, 0.015)
        for sy in (-y - 0.016, y + 0.016):
            box("Steel", x - 0.008, x + 0.008, sy - 0.007, sy + 0.007, 0.008, 0.024, bev=0.0)


def corridor_bench():
    """Corridor bench of the station: a perforated steel seat on two bolted trestles, the vinyl pad torn off half of
    it and curling up, a rusty stretcher."""
    area_bench_frame(-0.56, 0.56, 0.39, y=0.17)
    box("Rust", -0.56, 0.56, -0.012, 0.012, 0.1, 0.13)
    box("Steel", -0.7, 0.7, -0.22, 0.22, 0.37, 0.4, panel=True)
    for k in range(13):                                                                     # perforation rows
        x = -0.6 + k * 0.1
        box("Rubber", x - 0.03, x + 0.03, -0.17, 0.17, 0.393, 0.403, bev=0.0)
    box("Vinyl", -0.68, 0.02, -0.21, 0.21, 0.39, 0.44, panel=True)                         # the half that stays
    box("Vinyl", 0.0, 0.32, -0.2, 0.2, 0.4, 0.425, ry=-0.12)                                # peeling off
    sphere("Composite", -0.4, 0.05, 0.43, 0.07, seg=10, sz=0.4)                              # a split in the pad
    dp.export("corridor_bench")


def bench():
    """Locker-room bench: a steel tread-plate seat with one plank bent down, bolted trestles, a rag left on it."""
    area_bench_frame(-0.6, 0.6, 0.4, y=0.15)
    box("Paint", -0.6, 0.6, -0.012, 0.012, 0.1, 0.13)
    for i, y in enumerate((-0.15, -0.05, 0.05, 0.15)):
        bent = i == 2
        box("Steel", -0.8, 0.8 if not bent else 0.55, y - 0.046, y + 0.046, 0.39, 0.43, panel=True)
        if bent:
            box("Steel", 0.52, 0.79, y - 0.046, y + 0.046, 0.33, 0.41, ry=0.28)
    for x in (-0.6, 0.6):
        for y in (-0.1, 0.1):
            cyl("Steel", x, y, 0.42, 0.438, 0.01, seg=6)
    box("Vinyl", -0.55, -0.27, -0.17, 0.03, 0.42, 0.448, rz=0.15)                            # rag
    box("Vinyl", -0.5, -0.32, -0.2, -0.17, 0.25, 0.44, rz=0.05)                                # hanging over the edge
    dp.export("bench")


# --------------------------------------------------------------------------- galley

def dining_table():
    """Mess-hall table bolted to the floor: a steel top on a central beam and two T-feet, four swing-out stools on
    arms (one seat gone), a food tray and a mug left on it."""
    box("Steel", -0.8, 0.8, -0.45, 0.45, 0.71, 0.75, panel=True)
    box("Paint", -0.75, 0.75, -0.4, 0.4, 0.66, 0.72)
    box("Paint", -0.58, 0.58, -0.04, 0.04, 0.2, 0.3)                                           # stool beam
    for x in (-0.55, 0.55):
        box("Paint", x - 0.05, x + 0.05, -0.05, 0.05, 0.03, 0.68)
        box("Paint", x - 0.06, x + 0.06, -0.35, 0.35, 0.006, 0.06)
        floor_plate(x, -0.3)
        floor_plate(x, 0.3)
    for i, (x, y) in enumerate(((-0.38, -0.3), (0.38, -0.3), (-0.38, 0.3), (0.38, 0.3))):
        sy = 1 if y > 0 else -1
        if y < 0:
            rod("Paint", (x, -0.3, 0.25), (x, 0.3, 0.25), 0.018)                                   # the swing arm
        rod("Paint", (x, y, 0.24), (x, y, 0.43), 0.02)
        if i == 1:
            cyl("Rust", x, y, 0.43, 0.46, 0.04, seg=10)                                       # the seat torn off
            continue
        cyl("Paint", x, y, 0.42, 0.45, 0.1, seg=16)
        cyl("Vinyl", x, y, 0.44, 0.48, 0.12 if abs(y) + 0.12 <= 0.44 else 0.11, seg=16)
    box("Composite", -0.45, -0.05, -0.38, -0.1, 0.745, 0.765, rz=0.05, panel=True)           # food tray
    for cx, cy in ((-0.35, -0.3), (-0.17, -0.2)):
        box("Rust", cx - 0.06, cx + 0.06, cy - 0.05, cy + 0.05, 0.755, 0.766, rz=0.05, bev=0.002)
    box("Vinyl", 0.2, 0.42, -0.3, -0.05, 0.74, 0.758, rz=-0.2)                                 # a logbook left open
    box("Composite", 0.2, 0.42, -0.06, 0.19, 0.74, 0.755, rz=-0.2)
    dp.export("dining_table")


def kitchen_island():
    """Galley prep counter: a stainless top on a painted steel carcass with drawers and lockers to the cook's side,
    a pipe footrest under the overhang, stacked steel trays and a stock pot left on top."""
    box("Steel", -1.0, 1.0, -0.45, 0.45, 0.88, 0.92, panel=True)
    box("Steel", -1.0, 1.0, 0.43, 0.459, 0.88, 0.96)                                         # back upstand lip
    box("Paint", -0.97, 0.97, -0.1, 0.4, 0.1, 0.89)
    box("Paint", -0.95, 0.95, -0.08, 0.38, 0.005, 0.12)
    for sx in (-1, 1):                                                                        # end panels to the floor
        box("Paint", sx * 0.98 - 0.02, sx * 0.98 + 0.02, -0.44, 0.44, 0.0, 0.89)
    r = random.Random(43)
    for i, x in enumerate((-0.64, 0.0, 0.64)):                                                # cook's side (+y)
        ajar = 0.08 if i == 1 else 0.0
        box("Paint", x - 0.29, x + 0.29, 0.39, 0.425, 0.62, 0.84, rx=ajar, panel=True)       # drawer (one hanging askew)
        box("Steel", x - 0.1, x + 0.1, 0.415, 0.445, 0.76, 0.78, rx=ajar)
        if i != 2:
            box("Paint", x - 0.29, x + 0.29, 0.39, 0.425, 0.14, 0.6, panel=True)              # locker door
            box("Steel", x + 0.22, x + 0.25, 0.415, 0.445, 0.3, 0.5)
        else:
            box("Rust", x - 0.27, x + 0.27, 0.38, 0.405, 0.14, 0.6)                            # the door gone: rusty inside
    for x in (-0.64, 0.0, 0.64):                                                              # under the overhang
        box("Paint", x - 0.3, x + 0.3, -0.12, -0.09, 0.15, 0.82, panel=True)
    rod("Steel", (-0.96, -0.3, 0.2), (0.96, -0.3, 0.2), 0.02)
    for x in (-0.6, 0.6):
        rod("Steel", (x, -0.3, 0.2), (x, -0.1, 0.2), 0.016)
    for k in range(5):                                                                        # tray stack
        box("Steel", -0.7, -0.2, -0.38, -0.05, 0.905 + k * 0.012, 0.925 + k * 0.012, rz=r.uniform(-0.08, 0.08))
    cyl("Steel", 0.45, -0.15, 0.91, 1.04, 0.14, seg=20)
    cyl("Rust", 0.45, -0.15, 1.035, 1.045, 0.125, seg=20)
    for sx in (-1, 1):
        box("Steel", 0.45 + sx * 0.14 - 0.02, 0.45 + sx * 0.14 + 0.02, -0.17, -0.13, 0.99, 1.02)
    dp.export("kitchen_island")


def dishwasher():
    """Galley sanitiser: a stainless cabinet under the counter, a heavy door with a latch lever, a control strip with a
    small status window (where our display is) and dead buttons, a drain hose at the foot."""
    box("Paint", -0.297, 0.297, -0.27, 0.3, 0.1, 0.88, panel=True)
    box("Steel", -0.3, 0.3, -0.31, 0.31, 0.88, 0.92, panel=True)
    box("Paint", -0.29, 0.29, -0.25, 0.27, 0.0, 0.11)
    box("Steel", -0.29, 0.29, -0.3, -0.26, 0.11, 0.78, panel=True)                            # the door
    box("Rust", -0.285, 0.285, -0.302, -0.29, 0.115, 0.16, bev=0.0)
    box("Steel", -0.2, 0.2, -0.31, -0.29, 0.68, 0.71)                                          # latch bar
    box("Rubber", -0.29, 0.29, -0.285, -0.262, 0.78, 0.875, bev=0.002)                         # control strip
    box("Composite", -0.23, -0.11, -0.295, -0.27, 0.81, 0.85)                                  # status window bezel
    box("Screen", -0.215, -0.125, -0.3, -0.29, 0.818, 0.842, bev=0.001)
    for x in (0.1, 0.14, 0.18, 0.22):
        cyl("Steel", x, 0.83, -0.297, -0.275, 0.009, seg=8, axis="Y")
    box("Composite", -0.05, 0.05, -0.31, -0.29, 0.5, 0.56)                                     # a stencil plate
    cable((0.25, -0.24, 0.05), (0.26, -0.28, 0.012), sag=0.0, r=0.012)
    dp.export("dishwasher")


def stove():
    """Galley hotplate range: a steel body, three square hotplates (one torn out, its wiring hanging), a control strip
    with knobs and a status LED where our clock is, an oven door with a dead glass window and a bar handle."""
    box("Paint", -0.295, 0.295, -0.27, 0.305, 0.07, 0.9, panel=True)
    box("Paint", -0.28, 0.28, -0.24, 0.29, 0.0, 0.08)
    box("Steel", -0.3, 0.3, -0.3, 0.31, 0.88, 0.92, panel=True)
    for i, (x, y) in enumerate(((-0.14, -0.13), (0.14, -0.13), (-0.14, 0.15), (0.14, 0.15))):
        if i == 3:
            box("Rust", x - 0.11, x + 0.11, y - 0.11, y + 0.11, 0.905, 0.925)                  # the hole
            cable((x - 0.05, y, 0.92), (x + 0.07, y - 0.06, 0.92), sag=-0.025, r=0.009)
            continue
        box("Rust", x - 0.11, x + 0.11, y - 0.11, y + 0.11, 0.91, 0.935, panel=True)
        for k in range(3):
            box("Steel", x - 0.1 + k * 0.08, x - 0.08 + k * 0.08, y - 0.1, y + 0.1, 0.922, 0.945, bev=0.002)
    box("Steel", -0.3, 0.3, 0.27, 0.31, 0.91, 0.947)                                            # back guard
    box("Rubber", -0.29, 0.29, -0.295, -0.26, 0.79, 0.88)                                       # control strip
    for x in (-0.235, -0.135, 0.135, 0.235):
        cyl("Composite", x, 0.84, -0.315, -0.285, 0.022, seg=10, axis="Y")
        box("Steel", x - 0.003, x + 0.003, -0.321, -0.3, 0.84, 0.858, bev=0.0)
    box("Composite", -0.06, 0.06, -0.305, -0.29, 0.815, 0.855)
    box("Screen", -0.04, 0.04, -0.31, -0.298, 0.825, 0.845, bev=0.001)
    box("Paint", -0.285, 0.285, -0.3, -0.26, 0.2, 0.76, panel=True)                            # oven door
    box("Steel", -0.22, 0.22, -0.31, -0.29, 0.38, 0.63, panel=True)
    box("Glass", -0.18, 0.18, -0.312, -0.3, 0.42, 0.6, bev=0.0)
    crack([(-0.15, 0.58), (-0.05, 0.5), (0.02, 0.52), (0.12, 0.44)], -0.313)
    for x in (-0.2, 0.2):
        box("Steel", x - 0.015, x + 0.015, -0.32, -0.28, 0.69, 0.73)
    rod("Steel", (-0.22, -0.315, 0.71), (0.22, -0.315, 0.71), 0.011)
    box("Paint", -0.285, 0.285, -0.29, -0.26, 0.09, 0.19, panel=True)                           # drawer
    dp.export("stove")


def microwave():
    """Ration heater: a painted steel box on rubber feet, a latched door with a dead glass window, a keypad and a
    status LED where our display is, a vent grille, a cable out of the back."""
    for x in (-0.2, 0.2):
        for y in (-0.11, 0.13):
            cyl("Rubber", x, y, 0.0, 0.025, 0.018, seg=8)
    box("Paint", -0.25, 0.253, -0.15, 0.18, 0.015, 0.3, panel=True)
    box("Steel", -0.245, 0.1, -0.175, -0.14, 0.03, 0.285, panel=True)                         # the door
    box("Glass", -0.205, 0.04, -0.18, -0.165, 0.08, 0.24, bev=0.0)
    crack([(-0.19, 0.22), (-0.11, 0.16), (-0.03, 0.17)], -0.181)
    box("Steel", 0.05, 0.085, -0.187, -0.165, 0.1, 0.22)                                       # latch handle
    box("Rubber", 0.11, 0.24, -0.165, -0.14, 0.03, 0.285)                                      # control panel
    box("Composite", 0.122, 0.222, -0.168, -0.155, 0.23, 0.27)
    box("Screen", 0.13, 0.215, -0.172, -0.16, 0.24, 0.262, bev=0.001)
    for i in range(3):
        for j in range(3):
            if (i, j) == (1, 2):
                continue                                                                          # a key missing
            box("Composite", 0.135 + j * 0.03, 0.155 + j * 0.03, -0.17, -0.155, 0.08 + i * 0.04, 0.1 + i * 0.04, bev=0.002)
    for k in range(5):
        box("Rubber", 0.255 - 0.012, 0.255, -0.08 + k * 0.035, -0.06 + k * 0.035, 0.12, 0.26, bev=0.0)
    box("Rust", -0.245, -0.1, 0.1, 0.175, 0.29, 0.305, bev=0.002)
    dp.export("microwave")


def kitchen_sink():
    """Scullery sink: a stainless counter with a deep rust-stained tub, a painted steel base (one door gone, the trap
    and a canister inside), a pre-rinse spray on a tall riser with its hose."""
    bx = -0.14
    box("Paint", -0.595, 0.595, -0.25, 0.305, 0.1, 0.88)
    box("Paint", -0.57, 0.57, -0.245, 0.29, 0.0, 0.11)
    box("Steel", -0.6, 0.6, -0.31, 0.31, 0.86, 0.92, panel=True)
    box("Steel", -0.6, 0.6, 0.27, 0.31, 0.9, 1.04)                                              # splash back
    box("Rust", bx - 0.24, bx + 0.24, -0.2, 0.17, 0.9, 0.925)                                   # the tub's mouth
    box("Steel", bx - 0.27, bx + 0.27, -0.23, 0.2, 0.62, 0.9, panel=True)                       # the tub below the top
    for k in range(5):                                                                            # drainer ribs
        x = 0.25 + k * 0.06
        box("Steel", x - 0.012, x + 0.012, -0.22, 0.2, 0.915, 0.93, bev=0.002)
    box("Paint", -0.585, -0.01, -0.29, -0.24, 0.12, 0.84, panel=True)                            # left door
    box("Steel", -0.08, -0.05, -0.305, -0.28, 0.6, 0.76)
    box("Rust", 0.02, 0.58, -0.255, -0.243, 0.12, 0.84, bev=0.002)                              # the door gone: rust inside
    for x in (0.02, 0.58):
        box("Steel", x - 0.012, x + 0.012, -0.27, -0.24, 0.3, 0.36)                               # its torn hinges
    cable((0.3, -0.245, 0.8), (0.42, -0.235, 0.11), sag=0.05, r=0.008)                            # a hose hanging out
    tx, ty = bx, 0.22
    cyl("Steel", tx, ty, 0.91, 0.98, 0.03, seg=12)
    rod("Steel", (tx, ty, 0.95), (tx, ty, 1.2), 0.014)
    box("Steel", tx - 0.03, tx + 0.03, ty - 0.03, ty + 0.03, 1.02, 1.07)                         # mixer
    rod("Steel", (tx, ty, 1.19), (tx, ty - 0.18, 1.2), 0.012)                                      # the spring arm
    cyl("Steel", tx, ty - 0.18, 1.09, 1.21, 0.012, seg=8)
    cyl("Rubber", tx, ty - 0.18, 1.04, 1.1, 0.022, seg=10)                                        # spray head
    cable((tx, ty - 0.18, 1.06), (tx + 0.1, ty, 0.96), sag=0.08, r=0.008)
    dp.export("kitchen_sink")


# --------------------------------------------------------------------------- washrooms

def double_sink():
    """Decon wash trough: one long stainless trough on painted brackets, two knee-valve taps, a pipe run, and a
    polished steel mirror above with a crack (where our mirror is)."""
    box("Steel", -0.7, 0.7, -0.28, 0.22, 0.74, 0.86, panel=True)
    box("Rust", -0.64, 0.64, -0.22, 0.14, 0.845, 0.865)                                          # inside of the trough
    for x in (-0.5, 0.5):
        box("Paint", x - 0.03, x + 0.03, -0.2, 0.27, 0.6, 0.76)                                    # bracket
        box("Paint", x - 0.025, x + 0.025, -0.18, -0.13, 0.005, 0.62)                              # front leg
        floor_plate(x, -0.155, 0.1, 0.1)
        rod("Paint", (x, 0.26, 0.2), (x, -0.15, 0.7), 0.016)
        box("Steel", x - 0.05, x + 0.05, 0.25, 0.28, 0.1, 0.3)
    box("Steel", -0.7, 0.7, 0.22, 0.28, 0.72, 1.08, panel=True)                                 # upstand
    for x in (-0.36, 0.36):
        cyl("Steel", x, 0.18, 0.84, 0.9, 0.025, seg=10)
        rod("Steel", (x, 0.18, 0.88), (x, 0.18, 1.02), 0.013)
        rod("Steel", (x, 0.18, 1.02), (x, 0.04, 1.0), 0.012)
        box("Paint", x - 0.06, x + 0.06, -0.28, -0.255, 0.6, 0.68)                                # knee valve paddle
        rod("Steel", (x, -0.265, 0.66), (x, -0.15, 0.76), 0.01)
    rod("Rust", (-0.66, 0.26, 0.7), (0.66, 0.26, 0.7), 0.022)                                     # supply pipe
    rod("Steel", (0.0, 0.0, 0.75), (0.0, 0.0, 0.4), 0.03)                                         # drain
    rod("Steel", (0.0, 0.0, 0.4), (0.0, 0.26, 0.4), 0.03)
    box("Paint", -0.66, 0.66, 0.24, 0.278, 1.07, 1.9, panel=True)                                  # mirror frame
    box("Glass", -0.62, 0.62, 0.232, 0.256, 1.13, 1.87, bev=0.0)
    crack([(-0.45, 1.86), (-0.3, 1.6), (-0.1, 1.5), (0.05, 1.25)], 0.231)
    crack([(-0.3, 1.6), (-0.55, 1.4)], 0.231)
    box("Composite", 0.48, 0.62, 0.2, 0.25, 1.15, 1.32, panel=True)                               # soap dispenser
    dp.export("double_sink")


def mirror_sink():
    """Crew washstand: a steel basin on a painted pedestal cabinet (its door gone, the trap showing), a foot pedal,
    a polished steel mirror cracked across (where our mirror is), a conduit up the side."""
    box("Paint", -0.33, 0.33, -0.2, 0.24, 0.06, 0.75, panel=True)
    box("Paint", -0.3, 0.3, -0.18, 0.22, 0.0, 0.08)
    box("Rust", -0.29, 0.29, -0.21, -0.19, 0.1, 0.68)                                            # the open front
    rod("Steel", (0.0, 0.0, 0.75), (0.0, 0.0, 0.4), 0.025)
    rod("Steel", (0.0, 0.0, 0.4), (0.0, 0.22, 0.4), 0.025)
    box("Steel", -0.35, 0.35, -0.266, 0.25, 0.74, 0.88, panel=True)                              # basin
    box("Rust", -0.25, 0.25, -0.19, 0.12, 0.865, 0.885)
    box("Steel", -0.35, 0.35, 0.212, 0.248, 0.86, 1.08, panel=True)                                # upstand
    cyl("Steel", 0.0, 0.17, 0.87, 0.93, 0.025, seg=10)
    rod("Steel", (0.0, 0.17, 0.91), (0.0, 0.17, 1.0), 0.012)
    rod("Steel", (0.0, 0.17, 1.0), (0.0, 0.05, 0.98), 0.011)
    box("Steel", -0.08, 0.08, -0.26, -0.18, 0.006, 0.035, rx=0.1)                                  # foot pedal
    box("Paint", -0.31, 0.31, 0.21, 0.25, 1.07, 1.9, panel=True)                                  # mirror frame
    box("Glass", -0.27, 0.27, 0.2, 0.226, 1.13, 1.87, bev=0.0)
    crack([(-0.25, 1.75), (-0.08, 1.55), (0.06, 1.58), (0.24, 1.3)], 0.199)
    crack([(-0.08, 1.55), (-0.12, 1.2)], 0.199)
    rod("Rubber", (0.33, 0.22, 0.88), (0.33, 0.22, 1.89), 0.012)                                 # conduit
    dp.export("mirror_sink")


def hand_dryer():
    """Wall hot-air dryer of the decon block: a boxy painted steel housing with a vent grille on its back plate, the
    nozzle pointing down, the status LED where ours is, a conduit to the ceiling side and a rust run."""
    box("Steel", -0.15, 0.15, 0.07, 0.1, 1.1, 1.4, panel=True)                                   # back plate
    box("Paint", -0.135, 0.135, -0.085, 0.08, 1.165, 1.38, panel=True)                          # housing
    box("Paint", -0.12, 0.12, -0.075, 0.08, 1.14, 1.18, rx=-0.25)                                # slanted belly
    cyl("Steel", 0.0, -0.025, 1.094, 1.17, 0.032, seg=14, r2=0.026)
    cyl("Rust", 0.0, -0.025, 1.092, 1.1, 0.024, seg=12)
    for k in range(5):                                                                            # grille on the front
        z = 1.2 + k * 0.022
        box("Rubber", -0.09, 0.09, -0.092, -0.078, z, z + 0.01, bev=0.001)
    box("Composite", -0.04, 0.04, -0.09, -0.078, 1.315, 1.345)
    box("Screen", -0.012, 0.012, -0.094, -0.084, 1.32, 1.34, bev=0.001)
    box("Rust", 0.06, 0.1, -0.087, -0.08, 1.17, 1.3, bev=0.0)
    rod("Rubber", (0.11, 0.08, 1.37), (0.11, 0.08, 1.4), 0.015)
    dp.export("hand_dryer")


def towel_dispenser():
    """Decon wipe dispenser: a painted steel box on the wall, its lid sprung ajar, a stencilled label, a level slot, a
    yellowed sheet hanging out of the bottom slot askew."""
    box("Steel", -0.15, 0.15, 0.035, 0.059, 1.14, 1.6, panel=True)
    box("Paint", -0.14, 0.14, -0.045, 0.045, 1.22, 1.54, panel=True)
    box("Paint", -0.14, 0.14, -0.053, 0.045, 1.53, 1.585, rx=0.12)                                # the lid ajar
    box("Rubber", -0.08, 0.08, -0.052, -0.04, 1.43, 1.45, bev=0.0)                               # level slot
    box("Composite", -0.1, 0.1, -0.056, -0.04, 1.29, 1.34)                                         # label
    box("Rubber", -0.11, 0.11, -0.02, 0.02, 1.21, 1.23)                                            # bottom slot
    box("Composite", -0.09, 0.08, -0.008, -0.004, 1.13, 1.225, rx=0.1, ry=0.12, bev=0.0)           # the sheet
    box("Rust", -0.14, -0.09, -0.047, -0.04, 1.22, 1.32, bev=0.0)
    dp.export("towel_dispenser")


def urinal():
    """Stainless trough urinal on a wall plate: a box bowl with a rusty floor, a flush pipe and valve, a drain trap."""
    box("Steel", -0.17, 0.17, 0.145, 0.175, 0.44, 1.31, panel=True)                                # wall plate
    box("Steel", -0.18, 0.18, -0.16, 0.165, 0.46, 0.6, panel=True)                               # bowl bottom
    box("Rust", -0.14, 0.14, -0.12, 0.14, 0.59, 0.61)
    for sx in (-1, 1):
        box("Steel", sx * 0.174 - sx * 0.029, sx * 0.174, -0.125, 0.16, 0.58, 1.06)                # side walls
    box("Steel", -0.176, 0.176, -0.175, -0.12, 0.56, 0.66)                                         # front lip
    rod("Steel", (0.0, 0.12, 1.04), (0.0, 0.12, 1.3), 0.014)
    box("Steel", -0.03, 0.03, 0.1, 0.155, 1.15, 1.19)                                              # pipe clamp
    cyl("Steel", 0.0, 0.12, 1.24, 1.32, 0.032, seg=12)
    cyl("Steel", 0.0, 0.12, 1.32, 1.355, 0.022, seg=10)
    rod("Steel", (0.0, 0.12, 1.29), (0.0, 0.17, 1.29), 0.012)
    rod("Steel", (0.02, 0.1, 1.27), (0.1, 0.08, 1.25), 0.007)
    cyl("Rubber", 0.0, 0.08, 0.43, 0.48, 0.035, seg=10)
    rod("Rubber", (0.0, 0.08, 0.45), (0.0, 0.17, 0.45), 0.025)
    dp.export("urinal")


def shower_stall():
    """Decon shower: a steel tray with a drain grate, panelled walls with a nozzle ring and a valve box with a status
    LED, a cracked glass panel on the right, a torn hazard strip curtain on the left."""
    box("Steel", -0.481, 0.481, -0.481, 0.481, 0.0, 0.08, panel=True)
    box("Rust", -0.38, 0.38, -0.38, 0.38, 0.07, 0.085)
    box("Steel", -0.08, 0.08, -0.08, 0.08, 0.075, 0.09)
    for k in range(4):
        box("Rubber", -0.06, 0.06, -0.06 + k * 0.035, -0.045 + k * 0.035, 0.08, 0.092, bev=0.0)
    box("Paint", -0.43, 0.43, 0.42, 0.481, 0.06, 2.08, panel=True)                               # back wall
    for sx in (-1, 1):
        box("Paint", sx * 0.481 - sx * 0.06, sx * 0.481, -0.47, 0.47, 0.06, 2.08)
    box("Steel", -0.477, 0.477, -0.477, 0.477, 2.04, 2.1)                            # the roof
    for k in range(6):                                                                             # nozzle ring
        a = k * math.pi / 3
        cyl("Steel", 0.25 * math.cos(a), 0.25 * math.sin(a), 1.9, 1.95, 0.02, seg=6, r2=0.026)
    rod("Steel", (0.0, 0.445, 1.95), (0.0, 0.0, 1.95), 0.015)
    cyl("Steel", 0.0, 0.0, 1.93, 1.97, 0.26, seg=20)
    cyl("Rust", 0.0, 0.0, 1.925, 1.975, 0.23, seg=20)
    box("Paint", 0.1, 0.34, 0.36, 0.43, 0.95, 1.25, panel=True)                                   # valve box
    box("Screen", 0.27, 0.31, 0.355, 0.37, 1.19, 1.22, bev=0.001)
    cyl("Steel", 0.18, 1.065, 0.33, 0.37, 0.035, seg=10, axis="Y")
    box("Steel", 0.15, 0.3, 0.34, 0.36, 1.06, 1.08)
    rod("Rubber", (-0.3, 0.43, 1.6), (-0.3, 0.39, 0.3), 0.012)
    rod("Rubber", (-0.3, 0.39, 0.3), (-0.2, 0.3, 0.075), 0.012)                              # hand hose
    # front: a frame, the cracked pane on the right, strip curtain on the left
    box("Paint", -0.476, 0.476, -0.481, -0.43, 1.98, 2.06)
    box("Paint", -0.03, 0.03, -0.481, -0.43, 0.06, 2.0)
    box("Glass", 0.0, 0.43, -0.468, -0.444, 0.08, 1.99, bev=0.0)
    crack([(0.05, 1.7), (0.17, 1.45), (0.3, 1.5), (0.4, 1.1)], -0.47)
    crack([(0.17, 1.45), (0.1, 1.0), (0.2, 0.7)], -0.47)
    box("Steel", -0.43, -0.0, -0.475, -0.445, 1.96, 1.99)                                          # curtain rail
    for k in range(6):
        if k == 2:
            continue                                                                                # a strip torn away
        x = -0.4 + k * 0.067
        low = 0.25 if k == 4 else 0.12
        box("Vinyl", x - 0.033, x + 0.033, -0.465, -0.455, low, 1.97, ry=0.02 * (k - 2))
    dp.export("shower_stall")


def sanitary_bin():
    """Biohazard waste canister: a small composite drum with a hazard band, its domed lid sprung on the hinge, a steel
    foot pedal."""
    cyl("Composite", 0, 0, 0.02, 0.4, 0.124, seg=18, r2=0.126)
    cyl("Rubber", 0, 0, 0.0, 0.04, 0.122, seg=18)
    cyl("Paint", 0, 0, 0.2, 0.26, 0.127, seg=18)
    cyl("Composite", 0, 0.01, 0.395, 0.43, 0.125, seg=18, r2=0.1)
    box("Composite", -0.09, 0.09, -0.05, 0.08, 0.425, 0.452, rx=-0.1)                           # lid knob plate askew
    box("Steel", -0.05, 0.05, -0.128, -0.09, 0.0, 0.03)
    rod("Steel", (-0.04, 0.11, 0.4), (0.04, 0.11, 0.4), 0.012)
    for bms in dp.parts.values():                                                               # oval, like ours
        for bm in bms:
            for v in bm.verts:
                v.co.x *= 1.2
    dp.export("sanitary_bin")


def bin_pair():
    """Twin waste drums on a steel skid: biohazard and sharps, hazard bands and stencil plates, a locking bar across,
    one lid sitting askew."""
    box("Steel", -0.35, 0.35, -0.17, 0.17, 0.0, 0.05, panel=True)
    for i, x in enumerate((-0.176, 0.176)):
        cyl("Composite", x, 0, 0.03, 0.76, 0.164, seg=20, r2=0.166)
        cyl("Paint", x, 0, 0.5, 0.56, 0.169, seg=20)
        cyl("Rust", x, 0, 0.04, 0.09, 0.168, seg=20)
        box("Paint", x - 0.07, x + 0.07, -0.172, -0.15, 0.25, 0.42)                                 # stencil plate
        if i == 0:
            cyl("Composite", x, 0, 0.74, 0.8, 0.168, seg=20)
            cyl("Composite", x, 0, 0.79, 0.85, 0.09, seg=16)
        else:
            box("Composite", x - 0.14, x + 0.14, -0.14, 0.14, 0.75, 0.81, rx=0.08, rz=0.12)       # askew lid
            box("Vinyl", x - 0.08, x + 0.08, -0.1, 0.08, 0.7, 0.8, rz=0.4)                            # bag hanging out
    rod("Steel", (-0.34, -0.16, 0.62), (0.34, -0.16, 0.62), 0.012)
    for x in (-0.34, 0.34):
        box("Steel", x - 0.01, x + 0.01, -0.165, 0.0, 0.58, 0.66)
    dp.export("bin_pair")


def recycling_bins():
    """Three sorting bins of the station: composite waste lockers with steel lids (chute, slot, flap), a strap bolted
    across, the middle lid missing and a bag bulging out."""
    for k, x in enumerate((-0.405, 0.0, 0.405)):
        box("Composite", x - 0.19, x + 0.19, -0.19, 0.19, 0.01, 0.82, panel=True)
        box("Rubber", x - 0.195, x + 0.195, -0.195, 0.195, 0.0, 0.05)
        box("Paint", x - 0.12, x + 0.12, -0.2, -0.17, 0.4, 0.62)                                    # label plate
        if k == 1:
            sphere("Vinyl", x, 0.0, 0.84, 0.17, seg=12, sz=0.4)
            box("Rust", x - 0.17, x + 0.17, -0.17, 0.17, 0.8, 0.83)
            continue
        box("Paint", x - 0.198, x + 0.198, -0.198, 0.198, 0.81, 0.87, panel=True)
        if k == 0:
            cyl("Rubber", x, -0.04, 0.86, 0.88, 0.07, seg=14)
        else:
            box("Steel", x - 0.1, x + 0.1, -0.15, 0.0, 0.86, 0.89, rx=0.2)                            # flap
    box("Steel", -0.603, 0.603, -0.198, -0.183, 0.7, 0.74)                                              # strap
    for x in (-0.55, 0.0, 0.55):
        cyl("Steel", x, 0.72, -0.2, -0.19, 0.012, seg=6, axis="Y")
    dp.export("recycling_bins")


def changing_table():
    """Fold-down wall stretcher shelf: a steel housing on the wall, the bed folded out on two struts, a cracked vinyl
    pad shifted off-centre, a restraint strap hanging off the edge."""
    box("Paint", -0.45, 0.45, 0.2, 0.249, 0.74, 1.1, panel=True)
    box("Composite", -0.2, 0.2, 0.19, 0.205, 1.0, 1.04)                                              # label plate
    box("Steel", -0.425, 0.425, -0.245, 0.215, 0.82, 0.86, panel=True)                                   # the bed
    for sx in (-1, 1):
        box("Steel", sx * 0.435 - sx * 0.03, sx * 0.435, -0.245, 0.215, 0.85, 0.89)                        # side rims
        rod("Paint", (sx * 0.4, 0.22, 1.05), (sx * 0.4, -0.12, 0.85), 0.011)                            # strut
        cyl("Steel", sx * 0.4, 0.21, 1.03, 1.07, 0.02, seg=10)
    box("Steel", -0.42, 0.42, -0.25, -0.228, 0.852, 0.885)
    rod("Steel", (-0.42, 0.21, 0.83), (0.42, 0.21, 0.83), 0.02)                                         # hinge
    box("Vinyl", -0.33, 0.39, -0.19, 0.17, 0.85, 0.88, rz=0.04, panel=True)                           # the pad
    box("Rust", 0.0, 0.25, -0.05, 0.1, 0.865, 0.882, rz=0.3, bev=0.0)
    box("Rubber", -0.06, -0.02, -0.25, -0.2, 0.75, 0.88, rx=0.15)                                     # strap
    box("Steel", -0.07, -0.01, -0.25, -0.215, 0.75, 0.78)
    dp.export("changing_table")


def fire_cabinet():
    """Emergency locker: a painted steel box on the wall, its glass door smashed (shards left in the frame), a rusty
    extinguisher on its bracket with the hose, a stencil plate over the top."""
    box("Paint", -0.197, 0.197, -0.105, 0.12, 0.8, 1.5, panel=True)
    box("Rust", -0.17, 0.17, -0.11, -0.09, 0.83, 1.4)                                                 # dark inside
    for z0, z1 in ((0.803, 0.84), (1.37, 1.41)):
        box("Paint", -0.2, 0.2, -0.132, -0.098, z0, z1)
    for x in (-0.2, 0.16):
        box("Paint", x + 0.002, x + 0.038, -0.129, -0.099, 0.81, 1.4)
    box("Glass", 0.04, 0.17, -0.122, -0.11, 1.18, 1.38, bev=0.0)                              # a shard top right
    box("Glass", -0.17, -0.06, -0.122, -0.11, 0.83, 0.96, bev=0.0)                                   # and one bottom left
    box("Steel", 0.165, 0.19, -0.14 + 0.008, -0.12, 1.05, 1.15)                                       # pull
    ex = -0.03
    cyl("Rust", ex, 0.0, 0.84, 1.24, 0.07, seg=16)
    cyl("Rust", ex, 0.0, 1.24, 1.28, 0.07, seg=16, r2=0.04)
    cyl("Steel", ex, 0.0, 1.27, 1.33, 0.02, seg=10)
    box("Steel", ex - 0.02, ex + 0.06, -0.02, 0.02, 1.31, 1.35)
    rod("Rubber", (ex + 0.05, 0.0, 1.32), (0.11, -0.03, 1.2), 0.011)
    rod("Rubber", (0.11, -0.03, 1.2), (0.11, -0.04, 0.9), 0.011)
    box("Steel", ex - 0.05, ex + 0.05, 0.05, 0.12, 1.08, 1.12)
    box("Composite", -0.15, 0.15, -0.125, -0.095, 1.43, 1.48)                                         # stencil plate
    dp.export("fire_cabinet")


def notice_board():
    """Station bulletin panel: a steel sheet in a painted frame, curled safety notices and a duty roster held by
    magnets and tape, a hazard placard, one sheet hanging by a corner."""
    box("Paint", -0.6, 0.6, -0.005, 0.029, 1.1, 1.9, panel=True)
    box("Steel", -0.56, 0.56, -0.012, 0.02, 1.14, 1.86, bev=0.002)
    specs = ((-0.37, 1.58, 0.3, 0.4, 0.05), (0.0, 1.66, 0.21, 0.28, -0.07), (0.02, 1.33, 0.25, 0.18, 0.035),
             (0.33, 1.6, 0.21, 0.3, -0.035), (-0.4, 1.23, 0.18, 0.12, -0.09))
    for i, (x, z, w, h, a) in enumerate(specs):
        box("Composite", x - w / 2, x + w / 2, -0.0165, -0.009, z - h / 2, z + h / 2, ry=a, bev=0.0)
        box("Rubber", x - 0.015, x + 0.015, -0.018, -0.01, z + h / 2 - 0.04, z + h / 2 - 0.01)          # magnet
    box("Composite", 0.27, 0.45, -0.017, -0.009, 1.13, 1.3, ry=0.6, bev=0.0)                            # hanging sheet
    box("Paint", 0.3, 0.5, -0.017, -0.009, 1.78, 1.86, bev=0.0)                                         # hazard placard
    box("Rust", -0.6, -0.4, -0.006, 0.029, 1.1, 1.14, bev=0.0)
    dp.export("notice_board")


def janitor_cart():
    """Hazmat waste trolley: a steel frame on casters (one gone, the cart leans), a rusty drum with a wringer, a
    composite bin with a yellow sack, a hose coil, a push handle."""
    box("Steel", -0.43, 0.43, -0.22, 0.22, 0.1, 0.15, panel=True)
    for x, y in ((-0.38, -0.17), (-0.38, 0.17), (0.34, -0.17), (0.34, 0.17)):
        box("Paint", x - 0.025, x + 0.025, y - 0.025, y + 0.025, 0.06, 0.12)
        if (x, y) == (0.34, -0.17):
            cyl("Rust", x, y, 0.03, 0.065, 0.015, seg=8)                                                 # the caster gone
            box("Composite", x - 0.05, x + 0.05, y - 0.04, y + 0.04, 0.002, 0.04)                         # a block under it
            continue
        cyl("Rubber", y, 0.046, x - 0.017, x + 0.017, 0.044, seg=12, axis="X")
    for x in (0.05, 0.395):
        for y in (-0.2, 0.2):
            box("Paint", x - 0.015, x + 0.015, y - 0.015, y + 0.015, 0.12, 0.92)
    box("Steel", 0.03, 0.415, -0.21, 0.21, 0.49, 0.52)
    box("Steel", 0.03, 0.415, -0.22, 0.22, 0.88, 0.92, panel=True)
    rod("Paint", (0.41, -0.17, 0.9), (0.44, -0.17, 0.98), 0.014)
    rod("Paint", (0.41, 0.17, 0.9), (0.44, 0.17, 0.98), 0.014)
    rod("Paint", (0.44, -0.18, 0.98), (0.44, 0.18, 0.98), 0.016)
    cyl("Rust", -0.22, 0.0, 0.14, 0.46, 0.19, seg=18)                                                   # drum
    cyl("Steel", -0.22, 0.0, 0.44, 0.47, 0.195, seg=18)
    box("Steel", -0.37, -0.07, 0.03, 0.15, 0.44, 0.56, panel=True)                                     # wringer
    rod("Steel", (-0.1, 0.12, 0.5), (-0.12, 0.15, 0.9), 0.012)
    rod("Paint", (-0.26, -0.05, 0.3), (-0.32, 0.1, 1.03), 0.012)                                       # mop stick
    box("Composite", 0.07, 0.38, -0.19, 0.19, 0.51, 0.75, panel=True)                                  # bin
    sphere("Vinyl", 0.22, 0.0, 0.76, 0.14, seg=12, sz=0.6)                                              # sack
    for k in range(4):                                                                                    # hose coil on the top
        cyl("Rubber", 0.22, 0.0, 0.915 + k * 0.022, 0.935 + k * 0.022, 0.13, seg=16)
    cyl("Steel", 0.22, 0.0, 0.91, 1.0, 0.08, seg=12)
    dp.export("janitor_cart")


def umbrella_stand():
    """Tool drum: a rusty steel canister by the door with a crowbar and a dead flashlight baton standing in it."""
    cyl("Rust", 0, 0, 0.0, 0.6, 0.145, seg=20)
    for z in (0.0, 0.3, 0.58):
        cyl("Steel", 0, 0, z, z + 0.035, 0.152, seg=20)
    cyl("Rubber", 0, 0, 0.59, 0.605, 0.13, seg=20)
    # crowbar: a straight bar leaning, a hooked top
    rod("Steel", (-0.05, 0.03, 0.05), (-0.08, 0.05, 0.85), 0.012, seg=6)
    rod("Steel", (-0.08, 0.05, 0.84), (-0.04, 0.05, 0.91), 0.012, seg=6)
    rod("Steel", (-0.04, 0.05, 0.9), (0.0, 0.05, 0.88), 0.012, seg=6)
    # flashlight baton
    rod("Rubber", (0.05, -0.04, 0.05), (0.07, -0.06, 0.78), 0.022, seg=10)
    cyl("Steel", 0.07, -0.06, 0.76, 0.86, 0.032, seg=12)
    cyl("Glass", 0.07, -0.06, 0.855, 0.865, 0.026, seg=12)
    dp.export("umbrella_stand")


PROPS = [area_rug, sectional_sofa, egg_chair, side_table, magazine_rack, corridor_bench, bench, dining_table, kitchen_island,
         dishwasher, stove, microwave, kitchen_sink, double_sink, mirror_sink, hand_dryer, towel_dispenser, urinal,
         shower_stall, sanitary_bin, bin_pair, recycling_bins, changing_table, fire_cabinet, notice_board, janitor_cart,
         umbrella_stand]

if __name__ == "__main__":
    dp.clear()
    for make in PROPS:
        make()
    print("DONE")
