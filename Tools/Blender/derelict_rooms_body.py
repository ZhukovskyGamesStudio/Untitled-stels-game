"""Derelict-style replacements for the fitness, medical and rest-room furniture (rooms2_body.py, rooms_fit.py): the crew
gym, the sick bay and the bunk room of the abandoned station, in the language of derelict_props.py.

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_rooms_body.py -- Assets/_Game/Art/Models/StyleProps/Derelict [names…]

Writes <out>/<our model>.fbx, modelled to the bounds of our model (Art/Models/Props/<name>.fbx; Blender: x right, -y the
front, z up, standing on z = 0). Parts are named by surface (see derelict_props.py). Where our model has glowing Eye
parts, these bring their own Screen/Glass parts, so ours are hidden. Decoration only: no colliders, deterministic.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derelict_props as dp  # noqa: E402

box, cyl, rod, cable, sphere, screen = dp.box, dp.cyl, dp.rod, dp.cable, dp.sphere, dp.screen


# --------------------------------------------------------------------------- helpers

def ring(surface, c, R, r, plane, seg=16, a0=0.0, a1=2 * math.pi, rseg=8):
    """A round bar bent into an arc of radius R about c: plane XZ (around Y), YZ (around X) or XY."""
    pts = []
    for i in range(seg + 1):
        a = a0 + (a1 - a0) * i / seg
        u, v = R * math.cos(a), R * math.sin(a)
        if plane == "XZ":
            pts.append((c[0] + u, c[1], c[2] + v))
        elif plane == "YZ":
            pts.append((c[0], c[1] + u, c[2] + v))
        else:
            pts.append((c[0] + u, c[1] + v, c[2]))
    for p, q in zip(pts, pts[1:]):
        rod(surface, p, q, r, seg=rseg)


def pbox(surface, cx, cy, th, lx0, lx1, ly0, ly1, z0, z1, **kw):
    """A box in a frame turned by th about the vertical through (cx, cy)."""
    mx, my = (lx0 + lx1) / 2, (ly0 + ly1) / 2
    wx = cx + mx * math.cos(th) - my * math.sin(th)
    wy = cy + mx * math.sin(th) + my * math.cos(th)
    hx, hy = (lx1 - lx0) / 2, (ly1 - ly0) / 2
    return box(surface, wx - hx, wx + hx, wy - hy, wy + hy, z0, z1, rz=th, **kw)


def sbox(surface, x0, x1, a0, a1, h0, h1, oy, oz, th, **kw):
    """A box along a slope: a runs along the slope from (oy, oz), h across it; th tilts +a up."""
    ma, mh = (a0 + a1) / 2, (h0 + h1) / 2
    wy = oy + ma * math.cos(th) - mh * math.sin(th)
    wz = oz + ma * math.sin(th) + mh * math.cos(th)
    ha, hh = (a1 - a0) / 2, (h1 - h0) / 2
    return box(surface, x0, x1, wy - ha, wy + ha, wz - hh, wz + hh, rx=th, **kw)


def sheets(cx, cy, top, n, seed, spread=0.2):
    """A few yellowed sheets sunk a centimetre into the surface they lie on (so the dents never lift them off)."""
    r = random.Random(seed)
    for i in range(n):
        x, y = cx + (r.random() - 0.5) * spread, cy + (r.random() - 0.5) * spread * 0.6
        box("Composite", x - 0.105, x + 0.105, y - 0.148, y + 0.148, top - 0.008, top + 0.002 + i * 0.001,
            rz=(r.random() - 0.5) * 0.8, bev=0.0)


def plate(surface, x0, x1, y0, y1, z0, z1):
    """A stencil plate or patch: pass the box with its inner side reaching ~1 cm into the part it sits on."""
    box(surface, x0, x1, y0, y1, z0, z1, bev=0.001)


def strap(x0, x1, y0, y1, top, drop=None, side_x=None):
    """A restraint strap across a pad (Rubber), its loose end hanging down the side, a steel buckle."""
    box("Rubber", x0, x1, y0, y1, top - 0.012, top + 0.008, bev=0.002)
    if drop is not None:
        sx = x1 if side_x is None else side_x
        box("Rubber", sx - 0.012, sx + 0.002, y0 + 0.002, y1 - 0.002, drop, top + 0.005, bev=0.001)
        box("Steel", sx - 0.017, sx + 0.004, y0 - 0.008, y1 + 0.008, drop + 0.05, drop + 0.1, bev=0.002)


def frame4(surface, x0, x1, z0, z1, y0, y1, t):
    """A rectangular frame in the XZ plane with bars t wide; no two bars share a face (no z-fighting)."""
    box(surface, x0, x1, y0, y1, z1 - t, z1)
    box(surface, x0, x1, y0, y1, z0, z0 + t)
    for xa, xb in ((x0 + 0.001, x0 + t), (x1 - t, x1 - 0.001)):
        box(surface, xa, xb, y0 - 0.001, y1 + 0.001, z0 + t - 0.01, z1 - t + 0.01)


def framed_screen(kind, cx, y, cz, w, h, face=-1, back=0.0, surface="Composite", t=0.025):
    """A screen at plane y looking to -Y (face=-1) or +Y, its edges buried 8 mm in a bezel frame (so it never floats),
    and optionally a housing `back` deep behind it."""
    screen(kind, cx, y, cz, w, h, face=face, bulge=0.004)
    ya, yb = sorted((y - face * 0.01, y + face * 0.012))
    frame4(surface, cx - w / 2 - t + 0.008, cx + w / 2 + t - 0.008, cz - h / 2 - t + 0.008, cz + h / 2 + t - 0.008, ya, yb, t)
    if back:
        ya, yb = sorted((y - face * 0.004, y - face * back))
        box(surface, cx - w / 2 - t + 0.012, cx + w / 2 + t - 0.012, ya, yb, cz - h / 2 - t + 0.012, cz + h / 2 + t - 0.012)


def keyboard(cx, cy, top, w=0.44, d=0.15, missing=(), seed=3):
    """A flat keyboard lying on a surface at `top`: the case sunk 1 cm into it, keys sunk 5 mm into the case."""
    box("Composite", cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2, top - 0.01, top + 0.022)
    rows, cols = 3, 12
    pitch = (w - 0.04) / cols
    for r in range(rows):
        for c in range(cols):
            if (r, c) in missing or (r == 0 and 5 <= c <= 8):
                continue
            kx0 = cx - w / 2 + 0.02 + c * pitch
            kx1 = kx0 + pitch - 0.004 + (4 * pitch if (r == 0 and c == 4) else 0)
            ky0 = cy - d / 2 + 0.015 + r * (d - 0.03) / rows
            box("Composite" if (r + c) % 7 else "Rubber", kx0, kx1, ky0, ky0 + (d - 0.03) / rows - 0.004,
                top + 0.017, top + 0.032, bev=0.0)


def caster(x, y, z_top=0.08, r=0.035):
    cyl("Steel", x, y, 0.04, z_top, 0.018, seg=8)
    sphere("Rubber", x, y, r, r, seg=10)


# --------------------------------------------------------------------------- the gym

def bench_island():
    """Decontamination bench: steel slat seats back to back on bolted pedestals, a panelled spine with a hook rail, a
    respirator hanging on its hose, a rag on a hook, the front slat broken off at one end."""
    for x in (-0.78, 0.0, 0.78):
        box("Paint", x - 0.06, x + 0.06, -0.32, 0.32, 0.01, 0.44, panel=True)
        box("Rubber", x - 0.08, x + 0.08, -0.34, 0.34, 0.0, 0.02)
    for side in (-1, 1):
        for k in range(3):
            y0 = side * (0.06 + k * 0.11)
            ya, yb = sorted((y0, y0 + side * 0.09))
            x0 = -0.1 if (side < 0 and k == 2) else -0.88                   # the outer front slat broke off
            box("Steel", x0, 0.88, ya, yb, 0.43, 0.47)
    for x in (-0.84, 0.76):
        box("Paint", x + 0.002, x + 0.075, -0.04, 0.04, 0.4, 1.585)
    box("Paint", -0.77, 0.77, -0.025, 0.025, 0.43, 1.25, panel=True)
    box("Paint", -0.88, 0.88, -0.045, 0.045, 1.52, 1.6)
    box("Steel", -0.77, 0.77, -0.03, 0.03, 1.32, 1.38)
    for x in (-0.6, -0.3, 0.0, 0.3, 0.6):
        for s in (-1, 1):
            rod("Steel", (x, s * 0.02, 1.35), (x, s * 0.12, 1.33), 0.009, seg=6)
            rod("Steel", (x, s * 0.12, 1.33), (x, s * 0.14, 1.4), 0.009, seg=6)
    plate("Composite", -0.3, 0.3, -0.028, -0.013, 0.9, 0.98)                 # stencil band
    plate("Rust", 0.4, 0.62, -0.028, -0.013, 0.5, 0.7)
    rod("Rubber", (-0.3, -0.13, 1.37), (-0.3, -0.17, 1.25), 0.01, seg=6)     # respirator on its strap
    sphere("Composite", -0.3, -0.17, 1.2, 0.08, seg=12, sz=1.1)
    cyl("Rubber", -0.3, 1.18, -0.28, -0.22, 0.035, seg=12, axis="Y")
    cable((-0.3, -0.26, 1.18), (0.2, -0.33, 0.46), sag=0.12, r=0.012, n=7)
    box("Vinyl", 0.22, 0.38, 0.11, 0.15, 0.95, 1.42, ry=0.04)                # a rag on a hook
    dp.export("bench_island")


def cable_machine():
    """Resistance rig: two steel columns, a counterweight stack in a rusted housing behind a cracked window, pulley
    carriages with cables and D-handles, one cable snapped and its handle on the floor, hydraulic hoses, a pull-up bar."""
    for x in (-0.5, 0.5):
        box("Paint", x - 0.04, x + 0.04, -0.04, 0.04, 0.05, 2.15, panel=True)
        box("Paint", x - 0.05, x + 0.05, -0.48, 0.48, 0.008, 0.08)
        box("Rubber", x - 0.06, x + 0.06, -0.46, -0.38, 0.0, 0.015)
    box("Paint", -0.54, 0.54, 0.3, 0.42, 0.0, 0.07)
    box("Paint", -0.58, 0.58, -0.06, 0.42, 2.1, 2.19, panel=True)            # top plate
    box("Paint", -0.26, 0.26, 0.14, 0.46, 0.06, 1.95, panel=True)            # weight housing
    box("Glass", -0.1, 0.1, 0.128, 0.147, 0.5, 1.5)
    frame4("Steel", -0.12, 0.12, 0.47, 1.53, 0.124, 0.15, 0.025)
    rod("Rubber", (-0.08, 0.126, 1.3), (0.03, 0.126, 1.1), 0.003, seg=4)     # cracks in the window
    rod("Rubber", (0.03, 0.126, 1.1), (0.07, 0.126, 0.9), 0.003, seg=4)
    rod("Rubber", (0.03, 0.126, 1.1), (-0.06, 0.126, 0.95), 0.003, seg=4)
    for x in (-0.08, 0.08):
        rod("Steel", (x, 0.3, 1.93), (x, 0.3, 2.12), 0.012)                 # guide rods
    plate("Composite", -0.2, 0.2, 0.126, 0.15, 1.62, 1.7)
    box("Screen", 0.15, 0.19, 0.13, 0.15, 1.8, 1.82, bev=0.001)
    plate("Rust", 0.14, 0.27, 0.13, 0.2, 0.08, 0.4)
    for s in (-1, 1):
        box("Steel", s * 0.5 - 0.08, s * 0.5 + 0.08, -0.09, 0.06, 1.25, 1.4)
        cyl("Rust", -0.12, 1.3, s * 0.5 - 0.015, s * 0.5 + 0.015, 0.05, seg=16, axis="X")
        box("Paint", s * 0.5 - 0.04, s * 0.5 + 0.04, -0.45, 0.0, 2.1, 2.17)  # pull-up arms
    rod("Steel", (-0.58, -0.4, 2.13), (0.58, -0.4, 2.13), 0.016)
    rod("Rubber", (0.5, -0.16, 1.28), (0.48, -0.17, 0.76), 0.005, seg=6)   # right cable and handle
    box("Steel", 0.42, 0.54, -0.19, -0.15, 0.68, 0.77)
    rod("Rubber", (-0.5, -0.16, 1.28), (-0.49, -0.17, 1.02), 0.005, seg=6)  # left cable snapped
    box("Steel", -0.4, -0.28, -0.38, -0.32, 0.0, 0.04, rz=0.5)              # its handle on the floor
    rod("Rubber", (-0.3, -0.35, 0.02), (-0.08, -0.2, 0.01), 0.005, seg=6)
    cable((0.26, 0.2, 1.7), (0.5, 0.03, 1.9), sag=0.1, r=0.01)
    cable((-0.26, 0.25, 1.4), (-0.5, 0.03, 1.6), sag=0.12, r=0.01)
    dp.export("cable_machine")


def dumbbell_rack():
    """Crew weights rack: a battered two-tier steel rack of rusted hex dumbbells, two slots empty, one dumbbell
    dropped on the floor underneath, stencilled weight plates."""
    for s in (-1, 1):
        box("Paint", s * 0.665 - 0.035, s * 0.665 + 0.035, -0.25, 0.25, 0.0, 0.05)
        box("Paint", s * 0.665 - 0.025, s * 0.665 + 0.025, -0.22, -0.16, 0.03, 0.5)
        box("Paint", s * 0.665 - 0.025, s * 0.665 + 0.025, 0.14, 0.2, 0.03, 0.89)
        box("Paint", s * 0.665 - 0.02, s * 0.665 + 0.02, -0.2, 0.18, 0.3, 0.36, rx=-0.1)
    box("Steel", -0.68, 0.68, -0.22, 0.06, 0.35, 0.38, panel=True)
    box("Steel", -0.68, 0.68, -0.02, 0.2, 0.72, 0.75, panel=True)
    box("Paint", -0.68, 0.68, -0.24, -0.21, 0.36, 0.41)
    box("Paint", -0.68, 0.68, -0.04, -0.01, 0.73, 0.78)
    def bell(x, ya, yb, zc, r):
        cyl("Rust", x, zc, ya - 0.05, ya, r, seg=6, axis="Y")
        cyl("Rust", x, zc, yb, yb + 0.05, r, seg=6, axis="Y")
        rod("Steel", (x, ya - 0.01, zc), (x, yb + 0.01, zc), 0.016, seg=8)
    for i, x in enumerate((-0.52, -0.26, 0.0, 0.26, 0.52)):
        if i != 2:
            bell(x, -0.13, 0.0, 0.44, 0.07)
        if i != 4:
            bell(x, 0.07, 0.15, 0.795, 0.055)
        plate("Composite", x - 0.04, x + 0.04, -0.245, -0.23, 0.37, 0.4)
    cyl("Rust", 0.06, 0.05, -0.1, -0.05, 0.06, seg=6, axis="X")             # the dropped one, on its side
    cyl("Rust", 0.06, 0.05, 0.07, 0.12, 0.06, seg=6, axis="X")
    rod("Steel", (-0.06, 0.06, 0.05), (0.08, 0.06, 0.05), 0.016, seg=8)
    dp.export("dumbbell_rack")


def kettlebell_rack():
    """Calibration-mass rack: stepped steel side plates, a low and a high shelf of rusted spherical masses with
    handles, one slot empty and its mass rolled under the shelf, stencil numbers on the lips."""
    for s in (-1, 1):
        box("Paint", s * 0.485 - 0.02, s * 0.485 + 0.02, -0.2, 0.01, 0.0, 0.25, panel=True)
        box("Paint", s * 0.485 - 0.02, s * 0.485 + 0.02, -0.01, 0.2, 0.0, 0.57, panel=True)
        box("Rubber", s * 0.485 - 0.025, s * 0.485 + 0.025, -0.19, 0.19, 0.0, 0.015)
    box("Steel", -0.47, 0.47, -0.19, 0.0, 0.2, 0.24)
    box("Steel", -0.47, 0.47, 0.0, 0.19, 0.52, 0.56)
    box("Paint", -0.47, 0.47, -0.2, -0.18, 0.22, 0.28)
    box("Paint", -0.47, 0.47, -0.005, 0.015, 0.54, 0.6)
    def mass(x, y, z0, r):
        sphere("Rust", x, y, z0 + r * 0.95, r, seg=14, sz=0.95)
        for sx in (-1, 1):
            rod("Rust", (x + sx * 0.5 * r, y, z0 + 1.5 * r), (x + sx * 0.5 * r, y, z0 + 2.1 * r), 0.012, seg=6)
        rod("Rust", (x - 0.5 * r, y, z0 + 2.1 * r), (x + 0.5 * r, y, z0 + 2.1 * r), 0.012, seg=6)
    for x in (-0.3, 0.3):
        mass(x, -0.09, 0.23, 0.1)
    for x in (-0.33, -0.11, 0.11, 0.33):
        mass(x, 0.1, 0.55, 0.095)
    mass(0.15, -0.08, 0.0, 0.08)                                           # rolled under the low shelf
    for k, x in enumerate((-0.3, 0.0, 0.3)):
        plate("Composite", x - 0.04, x + 0.04, -0.205, -0.19, 0.235, 0.265)
    dp.export("kettlebell_rack")


def mat_rack():
    """Stretcher store: a steel rack of rolled emergency stretchers and thermal blankets strapped tight, a folded
    stretcher on the bottom shelf, one roll come loose and hanging over the front."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.37 - 0.02, sx * 0.37 + 0.02, sy * 0.22 - 0.02, sy * 0.22 + 0.02, 0.0, 1.2)
    for z in (0.05, 0.42, 0.79, 1.15):
        box("Steel", -0.39, 0.39, -0.24, 0.24, z, z + 0.03 if z < 1.1 else z + 0.04, panel=True)
    for sx in (-1, 1):
        for z in (0.25, 0.62, 0.99):
            box("Paint", sx * 0.37 - 0.02, sx * 0.37 + 0.02, -0.22, 0.22, z, z + 0.03)
    for z0, ys in ((0.45, (-0.12, 0.11)), (0.82, (-0.11, 0.11))):
        for i, y in enumerate(ys):
            kind = "Vinyl" if (i + int(z0 * 10)) % 2 else "Composite"
            cyl(kind, y, z0 + 0.1, -0.33, 0.33, 0.11, seg=18, axis="X")
            for x in (-0.2, 0.17):
                cyl("Rubber", y, z0 + 0.1, x, x + 0.03, 0.115, seg=18, axis="X")
    box("Vinyl", -0.28, 0.28, -0.245, -0.22, 0.32, 0.6, rx=0.03)            # the loose roll's flap
    for y in (-0.15, 0.0):
        rod("Steel", (-0.36, y, 0.088), (0.36, y, 0.088), 0.015)
    box("Vinyl", -0.3, 0.3, -0.16, 0.01, 0.092, 0.11)
    plate("Composite", -0.12, 0.12, -0.245, -0.23, 1.155, 1.185)
    dp.export("mat_rack")


def power_cage():
    """Multi-axis trainer: a steel cage frame holding a gimbal — an outer ring on side axles, an inner ring jammed
    askew, a padded harness board with restraint straps in the middle, a control box with the last readout, cables."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.65 - 0.03, sx * 0.65 + 0.03, sy * 0.65 - 0.03, sy * 0.65 + 0.03, 0.012, 2.14)
        box("Paint", sx * 0.65 - 0.04, sx * 0.65 + 0.04, -0.69, 0.69, 0.0, 0.06)
        box("Paint", -0.684, 0.684, sx * 0.65 - 0.037, sx * 0.65 + 0.037, 0.003, 0.056)
        box("Paint", sx * 0.65 - 0.03, sx * 0.65 + 0.03, -0.69, 0.69, 2.1, 2.18)
        box("Paint", -0.684, 0.684, sx * 0.65 - 0.027, sx * 0.65 + 0.027, 2.103, 2.176)
        box("Paint", sx * 0.65 - 0.026, sx * 0.65 + 0.026, -0.65, 0.65, 1.07, 1.13)
        rod("Steel", (sx * 0.55, 0, 1.1), (sx * 0.66, 0, 1.1), 0.03)          # axle
        cyl("Rust", 0, 1.1, sx * 0.58, sx * 0.62, 0.05, seg=8, axis="X")
    zc = 1.1
    ring("Paint", (0, 0, zc), 0.56, 0.025, "XZ", seg=18, rseg=6)              # outer ring
    rod("Steel", (0, 0, zc + 0.45), (0, 0, zc + 0.57), 0.02)                  # inner ring pivots
    rod("Steel", (0, 0, zc - 0.45), (0, 0, zc - 0.57), 0.02)
    a = 0.5                                                                   # the inner ring, jammed askew
    dx, dy = math.sin(a), math.cos(a)
    pts = [(0.46 * math.sin(t) * dx, 0.46 * math.sin(t) * dy, zc + 0.46 * math.cos(t))
           for t in (2 * math.pi * i / 16 for i in range(17))]
    for p, q in zip(pts, pts[1:]):
        rod("Rust", p, q, 0.022, seg=6)
    th = math.atan2(dy, dx)
    pbox("Vinyl", 0, 0, th, -0.2, 0.2, -0.04, 0.04, 0.8, 1.42)      # harness board
    for s in (-1, 1):
        rod("Steel", (s * 0.17 * dx, s * 0.17 * dy, zc), (s * 0.45 * dx, s * 0.45 * dy, zc), 0.016)
    for z0 in (0.95, 1.2):
        pbox("Rubber", 0, 0, th, -0.21, 0.21, -0.05, 0.05, z0, z0 + 0.05)
    pbox("Steel", 0, 0, th, -0.15, 0.15, -0.14, 0.05, 0.77, 0.81)
    cable((0.3, 0.65, 2.1), (0.05, 0.09, 1.38), sag=0.2, r=0.01, n=5)
    cable((-0.65, 0.4, 1.9), (-0.3, -0.2, 0.0), sag=0.3, r=0.01, n=5)
    box("Composite", 0.5, 0.63, -0.68, -0.6, 1.2, 1.45, panel=True)          # control box
    framed_screen("ScreenText", 0.565, -0.684, 1.36, 0.08, 0.065, face=-1, t=0.02)
    box("Screen", 0.52, 0.55, -0.687, -0.672, 1.25, 1.27, bev=0.001)
    cable((0.6, -0.62, 1.2), (0.62, -0.66, 0.06), sag=0.03, r=0.008)
    plate("Composite", -0.3, 0.3, -0.685, -0.668, 2.11, 2.17)
    dp.export("power_cage")


def rowing_machine():
    """Hydraulic rowing ergometer: a composite fan drum on a pedestal at the front, a steel rail sloping to the rear
    foot, a cracked vinyl seat on its carriage, footplates with straps, the handle parked on its chain, a small CRT on
    an arm facing the seat."""
    cyl("Composite", -0.82, 0.36, -0.12, 0.12, 0.2, seg=24, axis="X")       # fan drum
    for s in (-1, 1):
        cyl("Rust", -0.82, 0.36, s * 0.12 - 0.01, s * 0.12 + 0.012, 0.16, seg=20, axis="X")
        for k in range(4):
            ang = k * math.pi / 4
            p = (s * 0.134, -0.82 + 0.14 * math.cos(ang), 0.36 + 0.14 * math.sin(ang))
            q = (s * 0.134, -0.82 - 0.14 * math.cos(ang), 0.36 - 0.14 * math.sin(ang))
            rod("Steel", p, q, 0.006, seg=4)
    box("Paint", -0.1, 0.1, -0.9, -0.74, 0.0, 0.2, panel=True)
    box("Paint", -0.25, 0.25, -0.98, -0.88, 0.008, 0.06)
    box("Rubber", -0.27, 0.27, -0.99, -0.87, 0.0, 0.02)
    L, th = math.hypot(1.72, 0.215), math.atan2(-0.215, 1.72)
    sbox("Steel", -0.04, 0.04, 0.0, L, -0.04, 0.04, -0.72, 0.35, th)       # the rail
    box("Paint", -0.2, 0.2, 0.88, 1.02, 0.008, 0.15)
    box("Rubber", -0.22, 0.22, 0.89, 1.01, 0.0, 0.02)
    sbox("Steel", -0.08, 0.08, 0.93, 1.13, 0.03, 0.08, -0.72, 0.35, th)    # carriage
    sbox("Vinyl", -0.15, 0.15, 0.9, 1.16, 0.07, 0.14, -0.72, 0.35, th, panel=True)
    sphere("Composite", 0.05, 0.36, 0.33, 0.035, seg=8, sz=0.6)              # foam out of the crack
    for s in (-1, 1):
        xa, xb = sorted((s * 0.02, s * 0.16))
        box("Composite", xa, xb, -0.49, -0.35, 0.355, 0.385, rx=0.7)
        xa, xb = sorted((s * 0.05, s * 0.14))
        box("Rubber", xa, xb, -0.45, -0.41, 0.34, 0.41, rx=0.7)
    rod("Steel", (-0.22, -0.63, 0.5), (0.22, -0.63, 0.5), 0.016)            # the handle on its chain
    for s in (-1, 1):
        cyl("Rubber", -0.63, 0.5, s * 0.12, s * 0.23, 0.022, seg=10, axis="X")
    rod("Steel", (0, -0.63, 0.5), (0, -0.67, 0.44), 0.006, seg=6)
    rod("Paint", (0, -0.8, 0.54), (0, -0.62, 0.79), 0.018)                  # monitor arm
    box("Composite", -0.11, 0.11, -0.66, -0.597, 0.74, 0.89, panel=True)
    framed_screen("ScreenText", 0.0, -0.592, 0.815, 0.16, 0.1, face=1)
    cable((0.08, -0.66, 0.76), (0.1, -0.78, 0.5), sag=0.06, r=0.006)
    dp.export("rowing_machine")


def treadmill():
    """Gait-test treadmill: a steel deck with a cracked rubber belt (a torn flap over the front roller), a rusted
    motor housing, leaning uprights carrying a CRT console with the last readout, handrails with a harness strap."""
    for s in (-1, 1):
        box("Paint", s * 0.375 - 0.045, s * 0.375 + 0.045, -0.9, 0.62, 0.03, 0.2, panel=True)
        for y in (-0.85, 0.45):
            box("Rubber", s * 0.375 - 0.04, s * 0.375 + 0.04, y - 0.04, y + 0.04, 0.0, 0.04)
    box("Rubber", -0.33, 0.33, -0.86, 0.5, 0.12, 0.17)
    for y in (-0.6, -0.2, 0.2):
        plate("Steel", -0.33, 0.33, y, y + 0.012, 0.165, 0.172)
    cyl("Steel", -0.89, 0.1, -0.35, 0.35, 0.06, seg=16, axis="X")
    box("Rubber", -0.25, 0.1, -0.952, -0.935, 0.02, 0.14, rx=0.08)            # torn belt flap
    box("Composite", -0.42, 0.42, 0.42, 0.78, 0.0, 0.3, panel=True)          # motor housing
    plate("Rust", 0.15, 0.4, 0.41, 0.43, 0.04, 0.2)
    plate("Composite", -0.35, -0.05, 0.41, 0.43, 0.2, 0.26)
    for s in (-1, 1):
        sbox("Paint", s * 0.375 - 0.035, s * 0.375 + 0.035, -0.045, 0.045, 0.0, 0.98, 0.64, 0.15, -0.17)
    box("Paint", -0.4, 0.4, 0.74, 0.8, 0.9, 0.96)
    box("Paint", -0.42, 0.42, 0.72, 0.9, 1.05, 1.1)
    dp.tapered_box("Composite", -0.24, 0.24, 0.755, 0.92, 1.08, 1.37, back=0.75, back_side=1)   # console housing
    framed_screen("ScreenText", 0.0, 0.748, 1.225, 0.3, 0.19, face=-1, t=0.03)
    box("Screen", 0.3, 0.34, 0.712, 0.73, 1.07, 1.09, bev=0.001)
    for s in (-1, 1):
        rod("Steel", (s * 0.375, 0.78, 1.0), (s * 0.39, 0.3, 1.0), 0.02)
        cyl("Rubber", s * 0.39, 1.0, 0.32, 0.56, 0.026, seg=10, axis="Y")
    box("Rubber", 0.37, 0.41, 0.4, 0.44, 0.62, 1.01)                          # harness strap
    box("Steel", 0.365, 0.415, 0.39, 0.45, 0.6, 0.65)
    cable((0.15, 0.74, 1.12), (0.2, -0.2, 0.16), sag=0.2, r=0.006)
    dp.export("treadmill")


def exercise_bike():
    """Cardio ergometer: a welded steel frame, a heavy composite flywheel drum with a rusted face, a split vinyl saddle,
    a CRT console on the mast with electrode leads hanging to the grips."""
    box("Paint", -0.04, 0.04, -0.46, 0.5, 0.04, 0.1)
    for y in (-0.42, 0.46):
        box("Paint", -0.24, 0.24, y - 0.035, y + 0.035, 0.0, 0.075)
        for s in (-1, 1):
            box("Rubber", s * 0.245 - 0.025, s * 0.245 + 0.025, y - 0.045, y + 0.045, 0.0, 0.05)
    box("Paint", -0.05, 0.05, 0.2, 0.36, 0.05, 0.2)
    cyl("Composite", 0.28, 0.36, -0.06, 0.06, 0.24, seg=24, axis="X")
    for s in (-1, 1):
        cyl("Rust", 0.28, 0.36, s * 0.06 - 0.01, s * 0.06 + 0.012, 0.19, seg=20, axis="X")
        cyl("Steel", 0.28, 0.36, s * 0.07 - 0.01, s * 0.07 + 0.012, 0.05, seg=12, axis="X")
    pts = [(0, -0.3, 0.08), (0, -0.05, 0.3), (0, 0.16, 0.6), (0, 0.27, 1.0)]
    for p, q in zip(pts, pts[1:]):
        rod("Paint", p, q, 0.036, seg=12)
    rod("Paint", (0, 0.265, 0.95), (0, 0.33, 1.16), 0.03, seg=12)
    rod("Paint", (0, -0.1, 0.22), (0, -0.3, 0.84), 0.03, seg=12)
    rod("Steel", (0, -0.29, 0.8), (0, -0.33, 0.92), 0.02, seg=10)
    box("Vinyl", -0.09, 0.09, -0.48, -0.2, 0.9, 0.97, rx=0.06, panel=True)   # saddle
    sphere("Composite", 0.02, -0.36, 0.965, 0.03, seg=8, sz=0.6)
    cyl("Steel", -0.04, 0.32, -0.07, 0.07, 0.055, seg=14, axis="X")         # crank hub
    box("Paint", 0.05, 0.09, -0.08, 0.3, 0.28, 0.42)                          # chain guard
    for s in (-1, 1):
        a, b = (s * 0.08, -0.04, 0.32), (s * 0.08, -0.04 + s * 0.1, 0.32 - s * 0.14)
        rod("Steel", a, b, 0.014, seg=6)
        box("Rubber", s * 0.08 - 0.06 if s < 0 else s * 0.08 - 0.01, s * 0.08 + 0.01 if s < 0 else s * 0.08 + 0.06,
            b[1] - 0.035, b[1] + 0.035, b[2] - 0.012, b[2] + 0.012)
    bar = [(-0.24, 0.13, 1.0), (-0.22, 0.27, 1.05), (0, 0.33, 1.1), (0.22, 0.27, 1.05), (0.24, 0.13, 1.0)]
    for p, q in zip(bar, bar[1:]):
        rod("Steel", p, q, 0.016, seg=8)
    for s in (-1, 1):
        rod("Rubber", (s * 0.24, 0.13, 1.0), (s * 0.235, 0.22, 1.03), 0.024, seg=8)
    dp.tapered_box("Composite", -0.13, 0.13, 0.33, 0.45, 1.1, 1.27, back=0.7, back_side=1)
    screen("ScreenText", 0.0, 0.326, 1.185, 0.18, 0.11, face=-1, bulge=0.005)
    for s in (-1, 1):
        cable((s * 0.12, 0.35, 1.15), (s * 0.235, 0.15, 1.0), sag=0.12, r=0.005)
    dp.export("exercise_bike")


def weight_bench():
    """Press station: a padded vinyl bench with restraint straps, a two-post rack behind it holding a rusted barbell,
    two plates on one side and one on the other, the missing plate lying on the floor."""
    box("Paint", -0.04, 0.04, -0.55, 0.25, 0.28, 0.35)
    for y in (-0.5, 0.2):
        box("Paint", -0.03, 0.03, y - 0.03, y + 0.03, 0.03, 0.3)
        box("Paint", -0.22, 0.22, y - 0.04, y + 0.04, 0.008, 0.05)
        box("Rubber", -0.24, 0.24, y - 0.045, y + 0.045, 0.0, 0.015)
    box("Vinyl", -0.16, 0.16, -0.62, 0.3, 0.34, 0.45, panel=True)
    for y in (-0.35, 0.05):
        strap(-0.17, 0.17, y - 0.03, y + 0.03, 0.45, drop=0.24)
    for s in (-1, 1):
        box("Paint", s * 0.55 - 0.03, s * 0.55 + 0.03, 0.42, 0.5, 0.03, 1.2)
        box("Paint", s * 0.55 - 0.1, s * 0.55 + 0.1, 0.3, 0.62, 0.0, 0.06)
        box("Steel", s * 0.55 - 0.04, s * 0.55 + 0.04, 0.33, 0.46, 1.07, 1.115)
        box("Steel", s * 0.55 - 0.042, s * 0.55 + 0.042, 0.327, 0.35, 1.067, 1.16)
    box("Paint", -0.55, 0.55, 0.43, 0.49, 0.08, 0.14)
    rod("Steel", (-0.775, 0.38, 1.12), (0.775, 0.38, 1.12), 0.015)
    for s in (-1, 1):
        cyl("Steel", 0.38, 1.12, s * 0.62, s * 0.785, 0.025, seg=8, axis="X")
        cyl("Rubber", 0.38, 1.12, s * 0.605 - 0.01, s * 0.605 + 0.01, 0.04, seg=10, axis="X")
    for x0 in (0.63, 0.68, -0.67):
        cyl("Rust", 0.38, 1.12, x0, x0 + 0.04, 0.15, seg=16, axis="X")
        cyl("Steel", 0.38, 1.12, x0 - 0.004, x0 + 0.044, 0.045, seg=8, axis="X")
    cyl("Rust", -0.3, 0.45, 0.0, 0.04, 0.15, seg=20)                          # the missing plate, on the floor
    plate("Composite", -0.575, -0.525, 0.412, 0.43, 0.6, 0.7)
    dp.export("weight_bench")


def punching_bag():
    """Impact test dummy: a weighted drum base, a sprung steel stem, a battered composite torso with a target plate,
    one arm torn off with its cables hanging, a dented head with a lifting eye."""
    cyl("Paint", 0, 0, 0.008, 0.18, 0.29, seg=24, r2=0.27)
    cyl("Rubber", 0, 0, 0.0, 0.03, 0.295, seg=24)
    cyl("Paint", 0, 0, 0.17, 0.28, 0.26, seg=24, r2=0.1)
    cyl("Rust", 0.12, -0.08, 0.2, 0.25, 0.04, seg=10)
    cyl("Steel", 0, 0, 0.27, 0.75, 0.025, seg=12)
    pts = [(0.03 * math.cos(t), 0.03 * math.sin(t), 0.3 + 0.2 * t / (8 * math.pi)) for t in
           (8 * math.pi * i / 20 for i in range(21))]
    for p, q in zip(pts, pts[1:]):
        rod("Steel", p, q, 0.008, seg=4)
    box("Composite", -0.14, 0.14, -0.1, 0.1, 0.62, 0.76)                      # pelvis
    box("Composite", -0.17, 0.17, -0.11, 0.11, 0.74, 1.22, panel=True)        # chest
    box("Vinyl", -0.178, 0.178, -0.118, 0.118, 0.85, 0.95)                    # vest band
    plate("Paint", -0.06, 0.06, -0.118, -0.1, 1.0, 1.12)                     # target plate
    plate("Rust", 0.06, 0.16, 0.1, 0.118, 0.8, 1.0)
    cyl("Composite", 0, 1.2, -0.25, 0.25, 0.08, seg=16, axis="X")             # shoulders
    cyl("Composite", 0.22, 0, 0.92, 1.2, 0.055, seg=14)                        # the right arm stub
    cyl("Rubber", 0, 1.2, -0.28, -0.23, 0.045, seg=12, axis="X")              # torn socket, cables
    cable((-0.25, 0.0, 1.2), (-0.27, -0.04, 0.98), sag=-0.03, r=0.009, n=4)
    cable((-0.25, 0.02, 1.19), (-0.24, 0.05, 1.05), sag=-0.02, r=0.006, n=4)
    cyl("Steel", 0, 0, 1.25, 1.38, 0.03, seg=12)
    sphere("Composite", 0.0, 0.01, 1.46, 0.1, seg=12, sz=1.15)
    plate("Rust", -0.07, 0.03, -0.1, -0.08, 1.42, 1.5)
    cyl("Steel", 0, 0, 1.55, 1.6, 0.012, seg=8)
    ring("Steel", (0, 0, 1.597), 0.011, 0.005, "XZ", seg=8, a0=0.0, a1=math.pi, rseg=5)
    cable((0.2, 0.15, 0.1), (0.1, 0.08, 0.7), sag=-0.05, r=0.01, n=4)
    dp.export("punching_bag")


def floor_cushions():
    """Collapsed crash mats: two cracked vinyl gym mats on the floor, a third folded and slumped across them with its
    foam spilling, a loose strap and a fallen ceiling panel."""
    box("Vinyl", -0.56, 0.1, -0.57, 0.55, 0.0, 0.12, panel=True)
    box("Vinyl", 0.05, 0.53, -0.55, 0.5, 0.0, 0.1, rz=0.04, panel=True)
    box("Vinyl", -0.45, 0.3, -0.42, 0.32, 0.11, 0.22, rz=-0.15, ry=0.06, panel=True)
    for x, y, z, r in ((0.3, -0.2, 0.12, 0.06), (0.36, -0.12, 0.11, 0.045), (0.24, -0.3, 0.11, 0.04)):
        sphere("Composite", x, y, z, r, seg=10, sz=0.7)
    box("Rubber", -0.5, -0.1, 0.38, 0.43, 0.11, 0.13, rz=0.3)
    box("Steel", -0.2, 0.4, 0.35, 0.37, 0.05, 0.29, rx=-0.4)
    plate("Composite", -0.5, -0.3, -0.58, -0.56, 0.03, 0.09)
    dp.export("floor_cushions")


def towel_shelf():
    """Supply shelving: steel uprights, folded grey thermal blankets on the upper shelves, sealed composite med boxes
    in the middle (one shelf sagging), rolled blankets at the bottom, a stencil band."""
    for s in (-1, 1):
        box("Paint", s * 0.435 - 0.015, s * 0.435 + 0.015, -0.2, 0.2, 0.0, 1.6, panel=True)
    box("Paint", -0.42, 0.42, -0.18, 0.18, 0.0, 0.08)
    for z, sag in ((0.08, 0.0), (0.45, 0.0), (0.82, 0.025), (1.19, 0.0), (1.56, 0.0)):
        box("Steel", -0.43, 0.43, -0.19, 0.19, z, z + 0.03 if z < 1.5 else z + 0.04, ry=sag)
    r = random.Random(41)
    for z in (1.22, 0.85 + 0.01):
        for x in (-0.2, 0.2):
            zz = z - 0.01
            for k in range(3):
                h = 0.05 + r.random() * 0.02
                box("Vinyl", x - 0.15, x + 0.15, -0.15, 0.15, zz, zz + h, rz=(r.random() - 0.5) * 0.12)
                zz += h - 0.01
    for x, h in ((-0.28, 0.2), (-0.05, 0.14), (0.2, 0.24)):
        box("Composite", x - 0.11, x + 0.11, -0.16, 0.14, 0.47, 0.47 + h, panel=True)
        plate("Rust" if x > 0 else "Paint", x - 0.05, x + 0.05, -0.165, -0.15, 0.5, 0.56)
    for x in (-0.28, 0.0, 0.28):
        cyl("Vinyl", x, 0.17, -0.16, 0.16, 0.07, seg=14, axis="Y")
    plate("Composite", -0.448, -0.422, -0.206, -0.19, 1.3, 1.5)
    dp.export("towel_shelf")


def shoe_rack():
    """Boot rack: a steel rack with two sloping tiers of bars holding heavy magnetic work boots (composite uppers,
    thick rubber soles), heels out, one boot missing."""
    for s in (-1, 1):
        box("Paint", s * 0.485 - 0.015, s * 0.485 + 0.015, -0.18, 0.18, 0.0, 0.59, panel=True)
    box("Steel", -0.5, 0.5, -0.18, 0.18, 0.56, 0.6)
    plate("Composite", -0.15, 0.15, -0.185, -0.17, 0.565, 0.59)
    th = math.atan2(0.1, 0.24)
    for t, zm in enumerate((0.15, 0.38)):
        for y, z in ((-0.12, zm - 0.05), (0.12, zm + 0.05)):
            rod("Steel", (-0.48, y, z), (0.48, y, z), 0.012)
        oz = zm + 0.012
        for p, cx in enumerate((-0.33, 0.0, 0.33)):
            for b in (-1, 1):
                if t == 1 and p == 2 and b == 1:
                    continue                                                 # one boot missing
                x = cx + b * 0.055
                sbox("Rubber", x - 0.045, x + 0.045, -0.14, 0.14, -0.012, 0.025, 0.0, oz, th)
                sbox("Composite", x - 0.04, x + 0.04, -0.03, 0.13, 0.015, 0.075, 0.0, oz, th)
                heel_z = oz - 0.12 * math.sin(th)
                box("Composite", x - 0.042, x + 0.042, -0.135, -0.04, heel_z - 0.01, heel_z + 0.17)
                box("Rubber", x - 0.043, x + 0.043, -0.137, -0.038, heel_z + 0.13, heel_z + 0.15, bev=0.002)
    dp.export("shoe_rack")


# --------------------------------------------------------------------------- the sick bay

def daybed():
    """Triage cot: a steel frame on X legs, a split vinyl pad with the stuffing showing, restraint straps hanging over
    the edge, a rolled blanket, a back rail with a clipboard."""
    box("Paint", -0.98, 0.98, -0.42, 0.42, 0.3, 0.37, panel=True)
    for x in (-0.8, 0.0, 0.8):
        rod("Steel", (x, -0.39, 0.02), (x, 0.39, 0.32), 0.018, seg=8)
        rod("Steel", (x, 0.39, 0.02), (x, -0.39, 0.32), 0.018, seg=8)
        for y in (-0.39, 0.39):
            box("Rubber", x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0.0, 0.025)
    box("Vinyl", -0.96, -0.02, -0.4, 0.36, 0.36, 0.46, panel=True)
    box("Vinyl", 0.0, 0.96, -0.4, 0.36, 0.36, 0.46, ry=0.03, panel=True)
    sphere("Composite", -0.01, -0.05, 0.45, 0.05, seg=10, sz=0.6)
    box("Paint", -0.97, 0.97, 0.36, 0.44, 0.31, 0.65, panel=True)
    for x in (-0.5, 0.5):
        box("Rubber", x - 0.03, x + 0.03, -0.43, 0.37, 0.45, 0.47, bev=0.002)
        box("Rubber", x - 0.028, x + 0.028, -0.438, -0.425, 0.15, 0.465, bev=0.001)
        box("Steel", x - 0.04, x + 0.04, -0.441, -0.42, 0.2, 0.25, bev=0.002)
    cyl("Vinyl", 0.2, 0.52, -0.9, -0.4, 0.07, seg=14, axis="X")
    box("Composite", 0.5, 0.72, 0.345, 0.37, 0.42, 0.6)
    plate("Composite", 0.53, 0.69, 0.335, 0.35, 0.44, 0.57)
    plate("Rust", -0.7, -0.4, -0.425, -0.41, 0.31, 0.36)
    dp.export("daybed")


def defib_cabinet():
    """Trauma-kit wall box: a chipped steel case on a wall plate, a stencilled header with a faint status lamp, a
    steel-framed door with cracked dark glass, a rusted side."""
    box("Paint", -0.2, 0.2, 0.06, 0.08, 1.2, 1.6)
    box("Paint", -0.18, 0.18, -0.07, 0.07, 1.22, 1.52, panel=True)
    box("Paint", -0.19, 0.19, -0.08, 0.07, 1.5, 1.59, panel=True)
    box("Screen", -0.03, 0.03, -0.086, -0.072, 1.53, 1.57, bev=0.001)
    plate("Composite", 0.06, 0.16, -0.086, -0.072, 1.53, 1.57)
    plate("Composite", -0.16, -0.06, -0.086, -0.072, 1.53, 1.57)
    frame4("Steel", -0.17, 0.17, 1.24, 1.49, -0.085, -0.065, 0.02)
    box("Glass", -0.156, 0.156, -0.078, -0.068, 1.254, 1.476, bev=0.0)
    for a, b in (((-0.12, 1.44), (0.0, 1.36)), ((0.0, 1.36), (0.1, 1.28)), ((0.0, 1.36), (-0.05, 1.27)), ((0.0, 1.36), (0.12, 1.42))):
        rod("Rubber", (a[0], -0.079, a[1]), (b[0], -0.079, b[1]), 0.002, seg=4)
    box("Steel", 0.12, 0.14, -0.095, -0.08, 1.33, 1.4, bev=0.002)
    plate("Rust", 0.17, 0.185, -0.04, 0.04, 1.25, 1.36)
    cable((0.1, -0.02, 1.23), (0.14, -0.05, 1.2), sag=0.01, r=0.004, n=3)
    dp.export("defib_cabinet")


def doctor_desk():
    """Sick-bay station: a steel desk with a drawer pedestal (one drawer ajar), a CRT showing the last vitals report, a
    keyboard, a coiled stethoscope hose, a specimen tray with vials and papers."""
    top = 0.76
    box("Steel", -0.7, 0.7, -0.35, 0.35, 0.72, top, panel=True)
    box("Paint", -0.68, 0.68, -0.33, 0.33, 0.68, 0.73)
    box("Paint", -0.68, -0.28, -0.31, 0.33, 0.04, 0.69, panel=True)
    for k in range(3):
        z = 0.08 + k * 0.2
        out = 0.02 if k == 1 else 0.0
        box("Paint", -0.66, -0.3, -0.33 - out, -0.3, z, z + 0.18)
        box("Steel", -0.53, -0.43, -0.345 - out, -0.32 - out, z + 0.13, z + 0.15)
    box("Paint", 0.62, 0.68, -0.31, 0.33, 0.012, 0.69)
    box("Paint", -0.3, 0.62, 0.28, 0.31, 0.3, 0.69)
    for x in (-0.66, -0.3, 0.65):
        box("Rubber", x - 0.033, x + 0.033, -0.3, 0.32, 0.0, 0.045)
    cyl("Composite", 0.1, 0.16, top - 0.01, top + 0.012, 0.11, seg=14)                 # CRT on its swivel foot
    cyl("Composite", 0.1, 0.16, top + 0.005, top + 0.11, 0.05, seg=14, r2=0.07)
    dp.tapered_box("Composite", -0.145, 0.345, 0.0, 0.32, top + 0.05, top + 0.43, back=0.62, back_side=1)
    framed_screen("ScreenText", 0.1, -0.006, top + 0.24, 0.38, 0.28, face=-1, t=0.035)
    box("Composite", -0.02, 0.06, -0.025, -0.012, top + 0.38, top + 0.41, bev=0.0)       # a sticky note
    keyboard(0.05, -0.22, top, missing={(2, 5), (3, 9)})
    ring("Rubber", (-0.45, -0.12, top - 0.002), 0.09, 0.008, "XY", seg=9, rseg=4)
    ring("Rubber", (-0.445, -0.115, top + 0.01), 0.088, 0.008, "XY", seg=7, a0=0.5, a1=5.5, rseg=4)
    cyl("Steel", -0.33, -0.2, top - 0.01, top + 0.012, 0.025, seg=12)
    rod("Rubber", (-0.36, -0.14, top), (-0.34, -0.19, top + 0.005), 0.006, seg=6)
    box("Steel", -0.62, -0.36, 0.05, 0.25, top - 0.01, top + 0.025)
    for k in range(3):
        cyl("Composite", -0.56 + k * 0.07, 0.15, top + 0.01, top + 0.1, 0.018, seg=8)
    sheets(-0.4, 0.2, top, 2, 6)
    cable((0.1, 0.3, 0.85), (0.35, 0.34, 0.0), sag=0.1, r=0.008, n=4)
    dp.export("doctor_desk")


def exam_table():
    """Restraint examination couch: a panelled steel cabinet on a rusted plinth, a cracked vinyl pad with the head end
    raised on a wedge, two restraint straps with buckles, a torn paper sheet, a drawer at the foot."""
    box("Rust", -0.27, 0.27, -0.72, 0.68, 0.0, 0.06)
    box("Paint", -0.3, 0.3, -0.75, 0.7, 0.04, 0.6, panel=True)
    box("Paint", -0.26, 0.26, -0.77, -0.74, 0.35, 0.55, panel=True)
    box("Steel", -0.08, 0.08, -0.785, -0.765, 0.48, 0.51)
    box("Steel", -0.34, 0.34, -0.88, 0.88, 0.58, 0.63)
    box("Vinyl", -0.33, 0.33, -0.87, 0.3, 0.62, 0.71, panel=True)
    box("Paint", -0.3, 0.3, 0.35, 0.85, 0.6, 0.7)
    sbox("Vinyl", -0.33, 0.33, 0.0, 0.58, 0.0, 0.09, 0.3, 0.62, 0.25, panel=True)
    box("Composite", -0.28, 0.28, -0.8, 0.25, 0.70, 0.716, bev=0.0)
    box("Composite", 0.05, 0.25, -0.84, -0.72, 0.665, 0.7, rx=-0.3, bev=0.0)  # the torn end of the sheet
    for y in (-0.55, -0.05):
        strap(-0.345, 0.345, y - 0.035, y + 0.035, 0.716, drop=0.45)
    plate("Composite", 0.29, 0.31, -0.4, -0.15, 0.4, 0.5)
    cable((0.2, 0.7, 0.3), (0.3, 0.85, 0.0), sag=0.04, r=0.007)
    dp.export("exam_table")


def hospital_bed():
    """Med-bay gurney: a steel frame on casters with lift columns, a cracked vinyl mattress with the head section
    raised, restraint straps across, one side rail up and the other dropped askew, head and foot boards, a push bar."""
    box("Paint", -0.33, 0.33, -0.9, 0.9, 0.14, 0.22)
    for y in (-0.5, 0.5):
        box("Paint", -0.08, 0.08, y - 0.08, y + 0.08, 0.14, 0.43)
    box("Steel", -0.42, 0.42, -0.95, 0.95, 0.42, 0.47)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl("Steel", sx * 0.3, sy * 0.85, 0.07, 0.16, 0.022, seg=8)
            if not (sx < 0 and sy < 0):
                cyl("Rubber", sy * 0.85, 0.06, sx * 0.3 - 0.015, sx * 0.3 + 0.015, 0.06, seg=10, axis="X")
    cyl("Steel", -0.3, -0.85, 0.0, 0.08, 0.025, seg=8)                        # one caster gone, a bare stub
    box("Vinyl", -0.4, 0.4, -0.92, 0.2, 0.46, 0.58, panel=True)
    sbox("Steel", -0.39, 0.39, 0.0, 0.72, -0.03, 0.01, 0.2, 0.47, 0.5)
    sbox("Vinyl", -0.4, 0.4, 0.0, 0.72, 0.0, 0.12, 0.2, 0.47, 0.5)
    rod("Paint", (0, 0.6, 0.45), (0, 0.62, 0.68), 0.02)
    for y in (-0.6, -0.1):
        strap(-0.41, 0.41, y - 0.035, y + 0.035, 0.58, drop=0.36)
    box("Paint", -0.46, 0.46, 0.94, 1.02, 0.2, 1.0, panel=True)
    box("Paint", -0.46, 0.46, -1.02, -0.94, 0.2, 0.75, panel=True)
    plate("Composite", -0.15, 0.15, -1.026, -1.01, 0.55, 0.65)
    for x in (-0.4, 0.4):
        rod("Steel", (x, -0.98, 0.72), (x, -0.98, 0.82), 0.014)
    rod("Steel", (-0.42, -0.98, 0.82), (0.42, -0.98, 0.82), 0.016)
    box("Paint", 0.42, 0.47, 0.0, 0.85, 0.6, 0.65)                            # rail up
    for y in (0.1, 0.75):
        rod("Paint", (0.43, y, 0.45), (0.43, y, 0.62), 0.012)
    box("Paint", -0.47, -0.42, 0.0, 0.85, 0.33, 0.38, rx=0.1)                 # rail dropped askew
    rod("Paint", (-0.445, 0.1, 0.31), (-0.4, 0.1, 0.45), 0.012)
    cable((0.3, 0.98, 0.9), (0.4, 0.9, 0.0), sag=0.1, r=0.008, n=4)
    dp.export("hospital_bed")


def iv_stand():
    """Drip stand: a steel pole on a four-leg base (one caster gone), a cracked composite fluid canister and a sagging
    bag on the hooks, an infusion pump clamped to the pole with a faint lamp, the line running to the floor."""
    cyl("Steel", 0, 0, 0.06, 0.14, 0.045, seg=12)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        ex, ey = 0.2 * math.cos(a), 0.2 * math.sin(a)
        rod("Steel", (0, 0, 0.1), (ex, ey, 0.05 if k else 0.012), 0.014, seg=8)
        if k:
            sphere("Rubber", ex, ey, 0.035, 0.035, seg=8)
    rod("Steel", (0, 0, 0.1), (0, 0, 1.86), 0.014, seg=10)
    cyl("Paint", 0, 0, 1.0, 1.05, 0.025, seg=10)
    for k in range(4):
        a = k * math.pi / 2
        hx, hy = 0.12 * math.cos(a), 0.12 * math.sin(a)
        rod("Steel", (0, 0, 1.84), (hx, hy, 1.86), 0.007, seg=6)
        rod("Steel", (hx, hy, 1.86), (hx, hy, 1.89), 0.007, seg=6)
    rod("Rubber", (0.12, 0, 1.88), (0.12, 0, 1.74), 0.006, seg=6)
    cyl("Steel", 0.12, 0, 1.72, 1.76, 0.045, seg=12)
    cyl("Composite", 0.12, 0, 1.45, 1.74, 0.06, seg=14)
    rod("Rubber", (0.1, -0.06, 1.7), (0.15, -0.055, 1.52), 0.003, seg=4)      # the crack
    cyl("Steel", 0.12, 0, 1.41, 1.46, 0.03, seg=10)
    box("Vinyl", -0.07, 0.07, -0.14, -0.1, 1.55, 1.87, rx=0.04)
    box("Composite", 0.0, 0.14, -0.07, 0.07, 1.1, 1.26, panel=True)
    box("Screen", 0.05, 0.1, -0.078, -0.065, 1.2, 1.23, bev=0.001)
    cable((0.12, 0.0, 1.42), (0.1, -0.03, 1.25), sag=0.02, r=0.004, n=4)
    cable((0.1, 0.05, 1.11), (0.18, 0.2, 0.0), sag=0.1, r=0.004)
    dp.export("iv_stand")


def wheelchair():
    """Patient transport chair: a tubular steel frame on big rubber wheels, a cracked vinyl sling seat and back,
    restraint straps on the armrests, an oxygen bottle strapped behind, one hand rim gone."""
    wy, wz, R = 0.2, 0.295, 0.27
    for s in (-1, 1):
        x = s * 0.28
        ring("Rubber", (x, wy, wz), R, 0.025, "YZ", seg=14, rseg=6)
        cyl("Steel", wy, wz, x - 0.03 if s > 0 else x - 0.02, x + 0.02 if s > 0 else x + 0.03, 0.03, seg=12, axis="X")
        for k in range(6):
            a = k * math.pi / 3 + 0.2
            rod("Steel", (x, wy + 0.02 * math.cos(a), wz + 0.02 * math.sin(a)),
                (x, wy + 0.255 * math.cos(a), wz + 0.255 * math.sin(a)), 0.004, seg=4)
        if s > 0:
            ring("Steel", (0.312, wy, wz), 0.24, 0.008, "YZ", seg=12, rseg=4)
            for k in range(4):
                a = k * math.pi / 2 + 0.4
                p = (0.27, wy + 0.25 * math.cos(a), wz + 0.25 * math.sin(a))
                rod("Steel", p, (0.315, p[1] - 0.01 * math.cos(a), p[2] - 0.01 * math.sin(a)), 0.006, seg=4)
    rod("Steel", (-0.28, wy, wz), (0.28, wy, wz), 0.012)
    for s in (-1, 1):
        x = s * 0.22
        rod("Paint", (x, -0.35, 0.1), (x, -0.2, 0.48), 0.015)
        rod("Paint", (x, -0.21, 0.48), (x, 0.25, 0.48), 0.015)
        rod("Paint", (x, 0.2, 0.2), (x, 0.3, 0.92), 0.015)
        rod("Rubber", (x, 0.3, 0.92), (x, 0.4, 0.94), 0.02)
        rod("Paint", (x, -0.35, 0.1), (x, 0.21, 0.2), 0.015)
        rod("Steel", (x, -0.35, 0.1), (x, -0.38, 0.07), 0.012)
        cyl("Rubber", -0.38, 0.06, x - 0.015, x + 0.015, 0.06, seg=12, axis="X")
        xa, xb = sorted((s * 0.2, s * 0.25))
        box("Paint", xa, xb, -0.15, 0.27, 0.66, 0.7)
        rod("Paint", (x, -0.1, 0.48), (x, -0.1, 0.67), 0.012)
        xa, xb = sorted((s * 0.195, s * 0.255))
        box("Rubber", xa, xb, 0.0, 0.05, 0.65, 0.715, bev=0.002)
        xa, xb = sorted((s * 0.04, s * 0.2))
        box("Steel", xa, xb, -0.48, -0.36, 0.1, 0.12)
        rod("Paint", (x, -0.25, 0.4), (s * 0.18, -0.42, 0.115), 0.012)
    rod("Steel", (-0.22, 0.0, 0.165), (0.22, 0.0, 0.48), 0.012)
    rod("Steel", (0.22, 0.0, 0.165), (-0.22, 0.0, 0.48), 0.012)
    box("Vinyl", -0.23, 0.23, -0.2, 0.22, 0.46, 0.5)
    box("Vinyl", -0.23, 0.23, 0.25, 0.28, 0.5, 0.88, rx=-0.138)
    plate("Rust", -0.1, 0.05, -0.21, 0.0, 0.495, 0.505)
    box("Steel", -0.23, 0.23, 0.24, 0.33, 0.55, 0.58)
    cyl("Composite", 0, 0.36, 0.3, 0.8, 0.06, seg=14)
    cyl("Steel", 0, 0.36, 0.79, 0.86, 0.02, seg=8)
    cable((0.0, 0.36, 0.84), (0.1, 0.2, 0.5), sag=0.08, r=0.006)
    dp.export("wheelchair")


def curtain_divider():
    """Quarantine screen: a steel frame on casters with heavy pleated plastic sheeting, a biohazard stencil, one panel
    torn short and another hanging loose."""
    for s in (-1, 1):
        box("Paint", s * 0.77 - 0.02, s * 0.77 + 0.02, -0.02, 0.02, 0.06, 1.86)
        box("Paint", s * 0.76 - 0.03, s * 0.76 + 0.03, -0.095, 0.095, 0.04, 0.08)
        for y in (-0.065, 0.065):
            sphere("Rubber", s * 0.76, y, 0.03, 0.03, seg=8)
    rod("Steel", (-0.78, 0, 1.84), (0.78, 0, 1.84), 0.015)
    for i in range(7):
        c = -0.66 + i * 0.22
        th = 0.25 if i % 2 == 0 else -0.25
        z0 = {5: 0.95, 6: 0.3}.get(i, 0.2)
        box("Vinyl", c - 0.12, c + 0.12, -0.006, 0.006, z0, 1.85, rz=th, ry=0.06 if i == 6 else 0.0, bev=0.002)
        cyl("Steel", 0, 1.84, c - 0.01, c + 0.01, 0.025, seg=10, axis="X")
    pbox("Composite", -0.0, 0.0, -0.25, -0.08, 0.08, -0.012, 0.004, 1.2, 1.36)   # stencil on the middle panel
    pbox("Rust", -0.44, 0.0, -0.25, -0.06, 0.06, -0.01, 0.004, 0.3, 0.6)
    dp.export("curtain_divider")


def privacy_screen():
    """Folding radiation shield: three lead-lined steel panels in a zig-zag on posts, hazard stencils, a dark viewing
    window in the middle panel, one foot snapped off and propped on a rusted block."""
    th, cx = 0.38, (-0.52, 0.0, 0.52)
    for i, c in enumerate(cx):
        t = th if i != 1 else -th
        pbox("Paint", c, 0, t, -0.28, 0.28, -0.02, 0.02, 0.1, 1.62, panel=True)
        pbox("Steel", c, 0, t, -0.275, 0.275, -0.028, 0.028, 1.6, 1.64)
        pbox("Steel", c, 0, t, -0.274, 0.274, -0.026, 0.026, 0.08, 0.13)
        if i != 1:
            pbox("Composite", c, 0, t, -0.12, 0.12, -0.03, -0.012, 0.8, 1.0)
    pbox("Glass", 0, 0, -th, -0.12, 0.12, -0.025, 0.025, 1.15, 1.4, bev=0.0)
    for lx0, lx1, z0, z1, ly in ((-0.14, 0.14, 1.4, 1.43, 0.03), (-0.14, 0.14, 1.12, 1.15, 0.03),
                                 (-0.139, -0.115, 1.14, 1.41, 0.031), (0.115, 0.139, 1.14, 1.41, 0.031)):
        pbox("Steel", 0, 0, -th, lx0, lx1, -ly, ly, z0, z1)
    s, c = math.sin(th) * 0.28, math.cos(th) * 0.28
    posts = [(-0.52 - c, -s), (-0.26, s), (0.26, -s), (0.52 + c - 0.005, s)]
    for k, (x, y) in enumerate(posts):
        cyl("Steel", x, y, 0.05 if k != 3 else 0.06, 1.66, 0.015, seg=10)
        sphere("Steel", x, y, 1.665, 0.022, seg=8)
        if k != 3:
            cyl("Rubber", x, y, 0.0, 0.06, 0.03, seg=10)
    box("Rust", x - 0.04, x + 0.0, y - 0.025, y + 0.02, 0.0, 0.07)
    dp.export("privacy_screen")


# --------------------------------------------------------------------------- the bunk room

def bunk_bed():
    """Crew bunk: a steel two-tier frame, thin cracked vinyl mattresses with restraint straps, the top mattress slipped,
    a guard rail with a broken half, a ladder up the foot end, a stencilled bunk number."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.46 - 0.03, sx * 0.46 + 0.03, sy * 0.96 - 0.03, sy * 0.96 + 0.03, 0.01, 1.8)
            box("Rubber", sx * 0.46 - 0.035, sx * 0.46 + 0.035, sy * 0.96 - 0.035, sy * 0.96 + 0.035, 0.0, 0.03)
    for z in (0.3, 1.15):
        for sx in (-1, 1):
            box("Paint", sx * 0.46 - 0.025, sx * 0.46 + 0.025, -0.95, 0.95, z, z + 0.07)
        for sy in (-1, 1):
            box("Paint", -0.45, 0.45, sy * 0.96 - 0.02, sy * 0.96 + 0.02, z, z + 0.07)
        box("Steel", -0.44, 0.44, -0.94, 0.94, z + 0.02, z + 0.05)
    box("Vinyl", -0.42, 0.42, -0.92, 0.92, 0.34, 0.47, panel=True)
    box("Vinyl", -0.42, 0.42, -0.86, 0.92, 1.19, 1.32, rz=0.03, panel=True)
    box("Vinyl", -0.3, 0.3, 0.7, 0.9, 0.46, 0.53)
    box("Vinyl", -0.3, 0.3, 0.7, 0.9, 1.31, 1.38, rz=0.06)
    for y in (-0.3, 0.3):
        strap(-0.44, 0.44, y, y + 0.06, 0.47)
        strap(-0.44, 0.44, y, y + 0.06, 1.32)
    box("Rubber", -0.475, -0.452, -0.3, -0.24, 0.85, 1.22)                     # a strap end hanging down
    box("Paint", -0.447, 0.447, 0.933, 0.97, 0.36, 0.75, panel=True)
    box("Paint", -0.447, 0.447, 0.933, 0.97, 1.21, 1.6, panel=True)
    plate("Composite", -0.15, 0.15, 0.926, 0.946, 1.4, 1.48)
    box("Paint", 0.435, 0.485, -0.95, 0.95, 1.45, 1.5)
    box("Paint", -0.485, -0.435, 0.006, 0.95, 1.45, 1.5)
    box("Paint", -0.483, -0.437, 0.0, 0.04, 1.2, 1.49)
    rod("Paint", (-0.46, 0.025, 1.47), (-0.46, -0.42, 1.27), 0.02)              # the broken half, hanging
    for z in (0.6, 0.85, 1.4, 1.62):
        rod("Steel", (-0.46, -0.96, z), (0.46, -0.96, z), 0.015)
    cable((0.46, 0.96, 1.75), (0.2, 0.95, 1.62), sag=0.12, r=0.01, n=5)
    dp.export("bunk_bed")


def hammock():
    """Cargo-net crew bunk: a steel A-frame stand with a sagging cargo net strung between spreader bars on chains, a
    thin vinyl pad in it and a rolled blanket."""
    for s in (-1, 1):
        for y in (-0.38, 0.38):
            rod("Paint", (s * 1.0, y, 0.03), (s * 1.22, y * 0.8, 1.12), 0.03, seg=10)
        box("Paint", s * 1.0 - 0.04, s * 1.0 + 0.04, -0.43, 0.43, 0.008, 0.06)
        box("Rubber", s * 1.0 - 0.05, s * 1.0 + 0.05, -0.44, -0.36, 0.0, 0.015)
        rod("Paint", (s * 1.22, -0.33, 1.12), (s * 1.22, 0.33, 1.12), 0.028, seg=10)
        rod("Steel", (s * 1.0, -0.38, 0.93), (s * 1.0, 0.38, 0.93), 0.02, seg=8)    # spreader bar
        for y in (-0.36, 0.0, 0.36):
            rod("Steel", (s * 1.0, y, 0.93), (s * 1.22, 0.0, 1.12), 0.007, seg=5)
    box("Steel", -1.0, 1.0, -0.03, 0.03, 0.0, 0.06)
    plate("Composite", -0.15, 0.15, -0.035, -0.02, 0.01, 0.05)
    def node(i, j):
        t = -1.0 + 2 * i / 10
        w = 0.36 - 0.08 * t * t
        return (t, w * (j - 2) / 2, 0.48 + 0.45 * t * t)
    for j in range(5):
        for i in range(10):
            rod("Rubber", node(i, j), node(i + 1, j), 0.008, seg=4)
    for i in range(1, 10):
        rod("Rubber", node(i, 0), node(i, 4), 0.008, seg=4)
    for i in range(2, 8):
        t0, t1 = -1.0 + 2 * i / 10, -1.0 + 2 * (i + 1) / 10
        x, tm = (t0 + t1) / 2, (t0 + t1) / 2
        z = 0.48 + 0.45 * tm * tm + 0.008
        ang = -math.atan(0.9 * tm)
        half = (t1 - t0) / 2 / math.cos(ang) + 0.01
        box("Vinyl", x - half, x + half, -0.22, 0.22, z - 0.01, z + 0.012, ry=ang, bev=0.002)
    tz = 0.48 + 0.45 * 0.49
    cyl("Vinyl", 0.0 - 0.7, tz + 0.07, -0.2, 0.2, 0.07, seg=14, axis="Y")
    dp.export("hammock")


def sleep_pod():
    """Cryo bunk: a cramped composite tub on a panelled base under a faceted dark-glass lid on steel ribs, one facet
    cracked out and its shards on the padding, hoses to the head-end unit, a status screen at the foot."""
    box("Paint", -0.5, 0.5, -0.95, 0.95, 0.0, 0.34, panel=True)
    box("Composite", -0.47, 0.47, -0.92, 0.92, 0.33, 0.6, panel=True)
    box("Vinyl", -0.4, 0.4, -0.85, 0.85, 0.58, 0.63, panel=True)
    box("Vinyl", -0.28, 0.28, 0.6, 0.82, 0.62, 0.69)
    for s in (-1, 1):
        box("Rubber", s * 0.445 - 0.025, s * 0.445 + 0.025, -0.9, 0.9, 0.59, 0.62)
    R, zc, n = 0.43, 0.6, 5
    for k in range(n):
        if k == 3:
            continue                                                         # this facet is broken out
        am = (k + 0.5) * math.pi / n
        rr = R * math.cos(math.pi / (2 * n))
        x, z = rr * math.cos(am), zc + rr * math.sin(am)
        half = R * math.sin(math.pi / (2 * n)) + 0.006
        box("Glass", x - half, x + half, -0.88, 0.88, z - 0.006, z + 0.006, ry=math.pi / 2 - am, bev=0.0)
    for k, (x0, y0) in enumerate(((-0.12, -0.3), (0.05, 0.1), (-0.2, 0.35))):
        box("Glass", x0, x0 + 0.1, y0, y0 + 0.07, 0.62, 0.632, rz=0.5 * k + 0.3, bev=0.0)
    for y in (-0.88, 0.0, 0.88):
        ring("Steel", (0, y, zc), 0.44, 0.018, "XZ", seg=10, a0=0.0, a1=math.pi, rseg=8)
    box("Paint", -0.3, 0.3, 0.9, 0.98, 0.6, 0.95, panel=True)
    box("Screen", 0.18, 0.24, 0.9, 0.988, 0.88, 0.9, bev=0.001)
    for x in (-0.2, 0.0, 0.2):
        cable((x, 0.95, 0.62), (x * 1.2, 0.96, 0.25), sag=-0.05, r=0.012, n=5)
    box("Composite", 0.14, 0.36, -0.96, -0.94, 0.14, 0.26)
    screen("ScreenText", 0.25, -0.962, 0.2, 0.18, 0.1, face=-1, bulge=0.002)
    plate("Composite", -0.4, -0.05, -0.96, -0.94, 0.2, 0.26)
    plate("Rust", -0.5, -0.2, -0.96, -0.94, 0.0, 0.08)
    dp.export("sleep_pod")


def night_table():
    """Bunk-side locker: a small steel cabinet with a drawer and a door hanging askew, a caged work lamp with a dead
    bulb, pill bottles, one fallen over."""
    box("Paint", -0.23, 0.23, -0.19, 0.21, 0.04, 0.4, panel=True)
    box("Steel", -0.25, 0.25, -0.22, 0.22, 0.38, 0.41)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Rubber", sx * 0.2 - 0.025, sx * 0.2 + 0.025, sy * 0.17 - 0.025, sy * 0.17 + 0.025, 0.0, 0.05)
    box("Paint", -0.21, 0.21, -0.205, -0.185, 0.08, 0.3, rz=0.035, panel=True)
    box("Steel", 0.13, 0.16, -0.22, -0.19, 0.17, 0.22, bev=0.002)
    box("Paint", -0.21, 0.21, -0.205, -0.185, 0.31, 0.37, panel=True)
    box("Steel", -0.05, 0.05, -0.218, -0.2, 0.33, 0.35, bev=0.002)
    plate("Rust", -0.22, -0.12, -0.2, -0.185, 0.05, 0.12)
    top = 0.41
    cyl("Composite", 0.1, 0.05, top - 0.01, top + 0.02, 0.07, seg=16)
    rod("Steel", (0.1, 0.05, top + 0.01), (0.1, 0.05, 0.58), 0.009, seg=6)
    rod("Steel", (0.1, 0.05, 0.58), (0.03, -0.02, 0.585), 0.009, seg=6)
    cyl("Composite", 0.03, -0.02, 0.53, 0.595, 0.055, seg=16, r2=0.035)
    sphere("Glass", 0.03, -0.02, 0.53, 0.035, seg=12)
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        rod("Steel", (0.03 + 0.05 * math.cos(a), -0.02 + 0.05 * math.sin(a), 0.54), (0.03, -0.02, 0.49), 0.003, seg=4)
    cyl("Composite", -0.12, -0.05, top - 0.01, top + 0.08, 0.03, seg=10)
    cyl("Rubber", -0.12, -0.05, top + 0.07, top + 0.09, 0.032, seg=10)
    cyl("Composite", 0.1, top + 0.015, -0.19, -0.1, 0.025, seg=10, axis="X")
    dp.export("night_table")


def laundry_cart():
    """Biohazard waste cart: a steel frame on casters, a heavy vinyl bin liner sagging with a stencil, sealed waste
    bags heaped at the top, a glove hose over the rim."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            rod("Steel", (sx * 0.37, sy * 0.27, 0.07), (sx * 0.37, sy * 0.27, 0.874), 0.015)
            caster(sx * 0.33, sy * 0.24, 0.09)
    corners = [(-0.37, -0.27), (0.37, -0.27), (0.37, 0.27), (-0.37, 0.27)]
    for z in (0.1, 0.875):
        for (ax, ay), (bx, by) in zip(corners, corners[1:] + corners[:1]):
            rod("Steel", (ax, ay, z), (bx, by, z), 0.014)
    box("Steel", -0.37, 0.37, -0.27, 0.27, 0.08, 0.11)
    box("Vinyl", -0.35, 0.35, -0.25, 0.25, 0.2, 0.86, panel=True)
    box("Vinyl", -0.385, 0.385, -0.285, 0.285, 0.8, 0.885)
    for x, y, r in ((-0.15, 0.05, 0.13), (0.12, -0.06, 0.12), (0.14, 0.12, 0.1)):
        sphere("Vinyl", x, y, 0.83, r, seg=12, sz=0.5)
    plate("Composite", -0.15, 0.15, -0.258, -0.24, 0.45, 0.6)
    cable((0.3, -0.285, 0.87), (0.38, -0.29, 0.5), sag=-0.02, r=0.01, n=5)
    dp.export("laundry_cart")


def weighing_scale():
    """Biometric scale: a steel platform with a worn rubber mat, a column carrying a readout box with the last
    measurement, a bent height rod, electrode grips hanging on their leads."""
    box("Steel", -0.19, 0.19, -0.19, 0.19, 0.0, 0.07, panel=True)
    box("Rubber", -0.16, 0.16, -0.17, 0.1, 0.06, 0.08)
    plate("Rust", 0.12, 0.192, -0.192, -0.12, 0.02, 0.073)
    box("Paint", -0.04, 0.04, 0.12, 0.2, 0.05, 1.0, panel=True)
    box("Composite", -0.15, 0.15, 0.085, 0.21, 0.95, 1.15, panel=True)
    framed_screen("ScreenText", 0.0, 0.08, 1.06, 0.2, 0.12, face=-1)
    rod("Steel", (0, 0.16, 1.14), (0.01, 0.16, 1.28), 0.008, seg=6)
    box("Steel", -0.015, 0.015, -0.05, 0.17, 1.26, 1.28, rx=0.15)
    for s in (-1, 1):
        cable((s * 0.15, 0.14, 1.0), (s * 0.18, 0.0, 0.52), sag=0.05, r=0.005)
        cyl("Composite", s * 0.18, 0.0, 0.42, 0.53, 0.02, seg=10)
    dp.export("weighing_scale")


PROPS = [bench_island, cable_machine, dumbbell_rack, kettlebell_rack, mat_rack, power_cage, rowing_machine, treadmill,
         exercise_bike, weight_bench, punching_bag, floor_cushions, towel_shelf, shoe_rack,
         daybed, defib_cabinet, doctor_desk, exam_table, hospital_bed, iv_stand, wheelchair, curtain_divider,
         privacy_screen, bunk_bed, hammock, sleep_pod, night_table, laundry_cart, weighing_scale]

if __name__ == "__main__":
    dp.clear()
    for make in PROPS:
        make()
    print("DONE")
