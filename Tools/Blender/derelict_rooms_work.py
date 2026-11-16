"""Derelict (abandoned station) replacements for the work-room furniture of rooms2_work.py: lobby, open space, office and
meeting room pieces swapped for station gear of the same size and role (see derelict_props.py for the surfaces).

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_rooms_work.py -- Assets/_Game/Art/Models/StyleProps/Derelict [names...]

Writes <out>/<our model>.fbx, modelled to the bounds of our model (Art/Models/Props/<name>.fbx; x right, -y the front,
z up). Things lying on a surface are sunk into it by a centimetre so nothing floats after the export's dents.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derelict_props as dp  # noqa: E402

box, cyl, rod, cable, sphere, screen = dp.box, dp.cyl, dp.rod, dp.cable, dp.sphere, dp.screen


# --------------------------------------------------------------------------- small shared pieces

def sheet(cx, cy, top, rz=0.0, w=0.21, d=0.297, lift=0.0):
    """A sheet of paper lying on a surface at `top` (sunk 8 mm so it stays attached after the dents)."""
    box("Composite", cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2, top - 0.008 + lift, top + 0.002 + lift, rz=rz, bev=0.0)


def sheets(cx, cy, top, n, seed, spread=0.25):
    r = random.Random(seed)
    for i in range(n):
        sheet(cx + (r.random() - 0.5) * spread, cy + (r.random() - 0.5) * spread * 0.6, top, rz=(r.random() - 0.5) * 0.8,
              lift=i * 0.0015)


def keys(cx, cy, top, w=0.42, d=0.15, seed=1):
    """A cheap keyboard: a case and five rows of key strips, a few gaps where keys fell out."""
    box("Composite", cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2, top - 0.01, top + 0.02, bev=0.003)
    r = random.Random(seed)
    for row in range(5):
        y = cy - d / 2 + 0.015 + row * (d - 0.03) / 5
        x = cx - w / 2 + 0.015
        while x < cx + w / 2 - 0.03:
            seg = 0.04 + r.random() * 0.08
            seg = min(seg, cx + w / 2 - 0.015 - x)
            if r.random() > 0.12:
                box("Rubber", x, x + seg - 0.006, y, y + (d - 0.03) / 5 - 0.005, top + 0.012, top + 0.03 + row * 0.002, bev=0.0)
            x += seg


def bolts(points, y, r=0.008, depth=0.024, axis="Y"):
    """Bolt heads on a face (axis Y: points are (x, z); axis Z: points are (x, y) and `y` is the face's z)."""
    for a, b in points:
        if axis == "Y":
            cyl("Steel", a, b, y - depth / 2, y + depth / 2, r, seg=6, axis="Y")
        else:
            cyl("Steel", a, b, y - depth / 2, y + depth / 2, r, seg=6)


def stripes(x0, x1, y0, y1, z0, z1, n, face="Y"):
    """Hazard stripes: alternating yellowed composite and black rubber blocks on a face (along x)."""
    step = (x1 - x0) / n
    for k in range(n):
        box("Composite" if k % 2 == 0 else "Rubber", x0 + k * step, x0 + (k + 1) * step + 0.002, y0, y1, z0, z1, bev=0.0)


# --------------------------------------------------------------------------- lobby

def info_kiosk():
    """Station directory terminal: a bolted steel pillar with an open service hatch and torn-out cables, a slanted
    head with a CRT-style screen still showing the last report, a card slot and a keypad."""
    box("Steel", -0.3, 0.3, -0.25, 0.25, 0.0, 0.03, panel=True)                                # floor plate
    bolts([(-0.26, -0.21), (0.26, -0.21), (-0.26, 0.21), (0.26, 0.21)], 0.03, axis="Z")
    box("Paint", -0.13, 0.13, -0.1, 0.12, 0.02, 1.12, panel=True)                             # pillar
    box("Rubber", 0.125, 0.14, -0.06, 0.08, 0.35, 0.75, bev=0.0)                              # the hatch hole, its cover gone
    box("Paint", 0.137, 0.157, -0.07, 0.09, 0.02, 0.33, ry=-0.12)                        # the cover leaning at the foot
    cable((0.14, 0.0, 0.7), (0.2, -0.05, 0.02), sag=0.08, r=0.007)
    cable((0.14, 0.03, 0.62), (0.24, 0.12, 0.02), sag=0.05, r=0.005)
    # the head: a slanted housing (front from y -0.15, z 1.1 up to y 0.05, z 1.5) and a back block
    t = -math.atan2(0.2, 0.4)                                                                   # top leans back to +Y
    n = (0.0, -math.cos(-t), math.sin(-t))                                                     # outward normal of the face
    mid = (0.0, -0.05, 1.3)
    c = (0.0, mid[1] - n[1] * 0.05, mid[2] - n[2] * 0.05)
    box("Paint", -0.29, 0.29, c[1] - 0.05, c[1] + 0.05, c[2] - 0.2236, c[2] + 0.2236, rx=t, panel=True)
    box("Paint", -0.28, 0.28, -0.02, 0.15, 1.08, 1.47, panel=True)
    box("Paint", -0.2, 0.2, -0.13, 0.125, 1.06, 1.13)
    # the bezel round the screen and the screen
    def on_face(u, v, off):                                                                     # point on the face plane
        d = (0.0, math.sin(-t), math.cos(-t))
        return (u, mid[1] + d[1] * v + n[1] * off, mid[2] + d[2] * v + n[2] * off)
    for v, hh in ((0.19, 0.03), (-0.19, 0.03)):
        p = on_face(0.0, v, 0.008)
        box("Steel", -0.27, 0.27, p[1] - 0.012, p[1] + 0.012, p[2] - hh / 2, p[2] + hh / 2, rx=t)
    for u in (-0.255, 0.255):
        p = on_face(u, 0.0, 0.008)
        box("Steel", u - 0.015, u + 0.015, p[1] - 0.0105, p[1] + 0.0105, p[2] - 0.18, p[2] + 0.18, rx=t)
    p = on_face(0.0, 0.0, 0.006)
    screen("ScreenText", 0.0, p[1], p[2], 0.48, 0.34, face=-1, bulge=0.012, tilt=t)
    # card slot and keypad under the head
    box("Composite", -0.1, 0.1, -0.14, -0.08, 0.97, 1.03)
    box("Rubber", -0.07, 0.07, -0.145, -0.135, 1.0, 1.005, bev=0.0)
    box("Screen", 0.075, 0.09, -0.146, -0.138, 0.985, 0.995, bev=0.0)
    box("Composite", -0.11, 0.11, -0.16, -0.09, 0.85, 0.95, rx=-0.3)
    for i in range(3):
        for j in range(3):
            if (i, j) == (1, 2):
                continue
            box("Rubber", -0.07 + j * 0.05, -0.04 + j * 0.05, -0.17, -0.155, 0.865 + i * 0.026, 0.885 + i * 0.026, rx=-0.3, bev=0.0)
    dp.export("info_kiosk")


def security_turnstile():
    """Checkpoint gate: a heavy steel cabinet with hazard stripes, a card reader and a lane arrow on top, a service panel
    hanging open with cables; the steel gate leaf on the +Y lane shut, the one on the -Y lane wrenched open and warped."""
    box("Steel", -0.6, 0.6, -0.14, 0.14, 0.0, 0.05, panel=True)
    box("Paint", -0.57, 0.57, -0.112, 0.112, 0.04, 0.93, panel=True)
    box("Steel", -0.59, 0.59, -0.125, 0.125, 0.92, 0.97, panel=True)
    for y0, y1 in ((-0.122, -0.108), (0.108, 0.122)):
        stripes(-0.55, 0.55, y0, y1, 0.08, 0.17, 12)
    # card reader at the entry end, the lane arrow at the far end
    box("Composite", -0.5, -0.34, -0.08, 0.08, 0.96, 1.005)
    box("Screen", -0.47, -0.37, -0.06, 0.06, 0.995, 1.009, bev=0.0)
    box("Rubber", -0.49, -0.35, -0.085, -0.075, 0.97, 0.99, bev=0.0)
    box("Steel", 0.36, 0.48, -0.06, 0.06, 0.96, 0.99)
    box("Screen", 0.39, 0.45, -0.03, 0.03, 0.985, 1.0, rz=math.pi / 4, bev=0.0)
    # the service panel on the -Y side, its door swung down, cables spilling
    box("Rubber", 0.12, 0.42, -0.118, -0.105, 0.35, 0.7, bev=0.0)
    box("Paint", 0.12, 0.42, -0.14, -0.115, 0.13, 0.35, rx=0.25)
    cable((0.2, -0.115, 0.65), (0.28, -0.2, 0.05), sag=0.06, r=0.007)
    cable((0.33, -0.115, 0.6), (0.4, -0.25, 0.04), sag=0.04, r=0.006)
    # steel gate leaves on hinge posts: the +Y one closes its lane, the -Y one is wrenched open and warped
    for sy in (-1, 1):
        cyl("Steel", 0.0, sy * 0.112, 0.48, 0.92, 0.022, seg=12)
    box("Paint", -0.009, 0.009, 0.1, 0.285, 0.52, 0.88)
    for k in range(6):
        z = 0.52 + k * 0.06
        box("Composite" if k % 2 == 0 else "Rubber", -0.013, 0.013, 0.262, 0.292, z, z + 0.062, bev=0.0)
    a = 0.6
    box("Paint", 0.095 * math.sin(a) - 0.009, 0.095 * math.sin(a) + 0.009, -0.105 - 0.095 * math.cos(a) - 0.095,
        -0.105 - 0.095 * math.cos(a) + 0.095, 0.53, 0.87, ry=0.1, rz=a)
    box("Rust", 0.13 * math.sin(a) - 0.012, 0.13 * math.sin(a) + 0.012, -0.105 - 0.13 * math.cos(a) - 0.04,
        -0.105 - 0.13 * math.cos(a) + 0.04, 0.55, 0.66, ry=0.1, rz=a, bev=0.0)
    dp.export("security_turnstile")


def round_bench_planter():
    """Hydroponic column with a ring of bolted seats: a composite vessel, a steel cage of ribs with a few cracked dark
    glass panes, dead stalks and a slumped specimen inside, a pipe manifold on top; one seat pad gone, one sagging."""
    cyl("Steel", 0, 0, 0.0, 0.07, 0.86, seg=24)
    for k in range(6):
        a = k * math.pi / 3 + math.pi / 6
        cx, cy = 0.685 * math.cos(a), 0.685 * math.sin(a)
        box("Paint", cx - 0.13, cx + 0.13, cy - 0.21, cy + 0.21, 0.06, 0.39, rz=a)
        if k == 4:
            box("Steel", cx - 0.15, cx + 0.15, cy - 0.24, cy + 0.24, 0.38, 0.4, rz=a)              # the pad is gone
            continue
        sag = 0.12 if k == 1 else 0.0
        box("Vinyl", cx - 0.16, cx + 0.16, cy - 0.245, cy + 0.245, 0.38, 0.46, rz=a, panel=k == 0)
        if sag:
            box("Composite", cx - 0.1, cx + 0.06, cy - 0.12, cy + 0.12, 0.44, 0.47, rz=a + 0.3)    # foam through a split
    cyl("Composite", 0, 0, 0.06, 0.9, 0.5, seg=24, r2=0.48)                                   # the vessel
    cyl("Steel", 0, 0, 0.88, 0.94, 0.51, seg=24)
    cyl("Rust", 0, 0, 0.9, 0.95, 0.44, seg=20)                                               # dry growth medium
    for k in range(6):
        a = k * math.pi / 3
        rod("Steel", (0.46 * math.cos(a), 0.46 * math.sin(a), 0.9), (0.42 * math.cos(a), 0.42 * math.sin(a), 1.62), 0.02)
    for k in (0, 2, 3):                                                                         # the panes that are left
        a = k * math.pi / 3 + math.pi / 6
        hgt = 0.68 if k != 3 else 0.3
        cx, cy = 0.412 * math.cos(a), 0.412 * math.sin(a)
        box("Glass", cx - 0.008, cx + 0.008, cy - 0.21, cy + 0.21, 0.92, 0.92 + hgt, rz=a, bev=0.0)
    cyl("Steel", 0, 0, 1.58, 1.66, 0.46, seg=24)
    cyl("Paint", 0, 0, 1.64, 1.92, 0.09, seg=12)
    for k in range(3):
        a = k * 2 * math.pi / 3 + 0.4
        rod("Steel", (0.05 * math.cos(a), 0.05 * math.sin(a), 1.85), (0.3 * math.cos(a), 0.3 * math.sin(a), 1.82), 0.025)
        rod("Steel", (0.3 * math.cos(a), 0.3 * math.sin(a), 1.84), (0.33 * math.cos(a), 0.33 * math.sin(a), 1.64), 0.025)
    r = random.Random(41)
    for i in range(10):
        a = r.random() * 2 * math.pi
        rr = 0.1 + r.random() * 0.22
        h = 0.3 + r.random() * 0.4
        bend = 0.1 + r.random() * 0.12
        p0 = (rr * math.cos(a), rr * math.sin(a), 0.92)
        p1 = (rr * 1.05 * math.cos(a), rr * 1.05 * math.sin(a), 0.92 + h * 0.7)
        p2 = ((rr + bend) * math.cos(a), (rr + bend) * math.sin(a), 0.92 + h * 0.8)
        rod("Rust", p0, p1, 0.009, seg=5)
        rod("Rust", p1, p2, 0.007, seg=5)
    sphere("Vinyl", 0.05, -0.08, 1.02, 0.16, seg=12, sz=0.6)                                  # the slumped specimen
    cable((0.43, 0.0, 1.6), (0.55, 0.15, 0.48), sag=0.05, r=0.012)
    dp.export("round_bench_planter")


def logo_totem():
    """Station sector sign: two steel posts on a plinth, the upper sign panel (a dead display on both faces) hanging
    askew from one bolt, a hazard-striped stencil band below and an empty panel bay with cables."""
    box("Steel", -0.4, 0.4, -0.2, 0.2, 0.0, 0.09, panel=True)
    for x in (-0.33, 0.33):
        box("Paint", x - 0.04, x + 0.04, -0.06, 0.06, 0.05, 1.97, panel=True)
    box("Paint", -0.37, 0.37, -0.06, 0.06, 1.9, 1.98)
    # upper panel hanging from its right top bolt, dropped on the left
    ang = -0.1
    w, h = 0.6, 0.78
    hx, hz = 0.3, 1.88
    cx = hx + math.cos(ang) * (-w / 2) + math.sin(ang) * (-h / 2)
    cz = hz - math.sin(ang) * (-w / 2) + math.cos(ang) * (-h / 2)
    box("Paint", cx - w / 2, cx + w / 2, -0.075, 0.075, cz - h / 2, cz + h / 2, ry=ang, panel=True)
    for sy in (-1, 1):
        y0, y1 = sorted((sy * 0.07, sy * 0.082))
        box("Glass", cx - 0.25, cx + 0.25, y0, y1, cz - 0.3, cz + 0.32, ry=ang, bev=0.0)
    cyl("Steel", hx, hz, -0.09, 0.09, 0.02, seg=8, axis="Y")
    rod("Rubber", (cx - 0.15, -0.084, cz + 0.2), (cx + 0.05, -0.084, cz - 0.05), 0.004, seg=4)          # cracks
    rod("Rubber", (cx + 0.05, -0.084, cz - 0.05), (cx + 0.2, -0.084, cz + 0.05), 0.004, seg=4)
    # stencil band: hazard stripes and the sector number in rubber blocks
    box("Paint", -0.3, 0.3, -0.07, 0.07, 0.95, 1.12, panel=True)
    for sy in (-1, 1):
        y0, y1 = sorted((sy * 0.065, sy * 0.078))
        stripes(-0.29, 0.29, y0, y1, 0.96, 0.99, 10)
        for x0, x1, z0, z1 in ((-0.12, -0.04, 1.08, 1.1), (-0.06, -0.04, 1.0, 1.1), (-0.12, -0.04, 1.0, 1.02),
                               (0.02, 0.1, 1.08, 1.1), (0.08, 0.1, 1.0, 1.1)):
            box("Rubber", x0, x1, y0, y1, z0, z1, bev=0.0)
    # the empty bay below with a cable loop, the lower cover plate leaning against the plinth
    box("Rubber", -0.29, 0.29, -0.02, 0.02, 0.3, 0.95, bev=0.0)
    cable((-0.2, -0.03, 0.9), (0.15, -0.03, 0.4), sag=0.15, r=0.01)
    box("Paint", -0.28, 0.24, -0.17, -0.14, 0.06, 0.6, rx=-0.12, panel=True)
    dp.export("logo_totem")


def queue_barrier():
    """Two hazard bollards on heavy rusted bases with a sagging chain and a warning tag; the right post leans."""
    tops = []
    for x, lean in ((-0.65, 0.0), (0.65, -0.05)):
        cyl("Rust", x, 0, 0.0, 0.06, 0.145, seg=20)
        cyl("Steel", x, 0, 0.05, 0.1, 0.06, seg=14, r2=0.04)
        top = (x + lean, 0.02 if lean else 0.0, 0.94)
        rod("Steel", (x, 0, 0.06), top, 0.03, seg=14)
        for k in range(4):
            f0, f1 = (0.4 + k * 0.12) / 0.94, (0.46 + k * 0.12) / 0.94
            a = (x + (top[0] - x) * f0, top[1] * f0, 0.06 + 0.88 * f0)
            b = (x + (top[0] - x) * f1, top[1] * f1, 0.06 + 0.88 * f1)
            rod("Composite" if k % 2 == 0 else "Rubber", a, b, 0.033, seg=14)
        sphere("Composite", top[0], top[1], top[2] + 0.01, 0.045, seg=12, sz=1.2)
        tops.append(top)
    a, b = tops
    pts = []
    for i in range(15):
        u = i / 14
        pts.append((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u, a[2] - 0.06 + (b[2] - a[2]) * u - 0.3 * math.sin(math.pi * u)))
    for i, (p, q) in enumerate(zip(pts, pts[1:])):
        rod("Rust", p, q, 0.011 if i % 2 else 0.008, seg=6)
    m = pts[7]
    box("Composite", m[0] - 0.12, m[0] + 0.12, -0.012, 0.012, m[2] - 0.16, m[2] + 0.005, ry=0.08)          # warning tag
    stripes(m[0] - 0.1, m[0] + 0.1, -0.016, -0.01, m[2] - 0.13, m[2] - 0.09, 6)
    dp.export("queue_barrier")


# --------------------------------------------------------------------------- open space

def standing_desk():
    """Standing operator console: a steel bench on square legs with a lower shelf and a beige tower, an industrial
    monitor in a bolted steel frame still showing the last report, a keyboard, a dangling control pendant."""
    for x in (-0.62, 0.62):
        for y in (-0.3, 0.28):
            box("Paint", x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0.008, 1.07)
        box("Paint", x - 0.025, x + 0.025, -0.3, 0.28, 0.2, 0.24)
        box("Rubber", x - 0.045, x + 0.045, -0.33, 0.31, 0.0, 0.02)
    box("Steel", -0.66, 0.66, -0.32, 0.3, 0.24, 0.27, panel=True)                             # lower shelf
    box("Steel", -0.7, 0.7, -0.35, 0.35, 1.06, 1.1, panel=True)                               # the top
    box("Rust", 0.4, 0.62, -0.35, -0.2, 1.09, 1.101, bev=0.0)
    box("Composite", -0.45, -0.26, -0.2, 0.22, 0.26, 0.68)                                      # a beige tower on the shelf
    for z0, z1 in ((0.6, 0.64), (0.54, 0.58)):
        box("Rubber", -0.42, -0.29, -0.207, -0.19, z0, z1, bev=0.0)
    box("Screen", -0.31, -0.295, -0.206, -0.19, 0.33, 0.345, bev=0.0)
    # the monitor frame (our screen sits at y 0.139, z 1.165..1.485)
    box("Paint", -0.33, 0.33, 0.135, 0.22, 1.15, 1.5, panel=True)
    for z0, z1 in ((1.15, 1.18), (1.47, 1.5)):
        box("Steel", -0.34, 0.34, 0.112, 0.145, z0, z1)
    for x in (-0.325, 0.325):
        box("Steel", x - 0.02, x + 0.02, 0.116, 0.141, 1.17, 1.48)
    screen("ScreenText", 0.0, 0.122, 1.325, 0.6, 0.29, face=-1, bulge=0.0)
    bolts([(-0.325, 1.165), (0.325, 1.165), (-0.325, 1.485), (0.325, 1.485)], 0.112, r=0.007)
    box("Steel", -0.06, 0.06, 0.15, 0.22, 1.09, 1.16)
    keys(0.0, -0.17, 1.1, w=0.44, d=0.15, seed=4)
    sheet(-0.45, -0.12, 1.1, rz=0.3)
    sheet(-0.42, -0.08, 1.1, rz=-0.15, lift=0.002)
    dp.mug(0.42, -0.05, 1.095)
    # the control pendant hanging off the front edge
    cable((0.5, -0.33, 1.08), (0.52, -0.34, 0.74), sag=-0.02, r=0.006)
    box("Composite", 0.48, 0.56, -0.36, -0.32, 0.62, 0.76)
    box("Rubber", 0.5, 0.54, -0.37, -0.355, 0.7, 0.73, bev=0.0)
    cable((0.2, 0.22, 1.2), (0.6, 0.3, 0.27), sag=0.15, r=0.007)
    dp.export("standing_desk")


def planter_divider():
    """Dead hydroponic trough: a steel tank on legs with a stencil plate, dried growth medium, rows of dead stalks,
    the composite cover slid half off, a dead grow-light bar on two posts (one end dropped), a feed pipe and hose."""
    for x in (-0.74, 0.74):
        for y in (-0.13, 0.13):
            box("Paint", x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0.008, 0.15)
            box("Rubber", x - 0.045, x + 0.045, y - 0.045, y + 0.045, 0.0, 0.02)
    box("Steel", -0.79, 0.79, -0.16, 0.16, 0.13, 0.6, panel=True)
    box("Rust", -0.805, 0.805, -0.178, 0.178, 0.58, 0.62)
    box("Rust", -0.77, 0.77, -0.14, 0.14, 0.59, 0.63, bev=0.0)                               # growth medium
    box("Composite", -0.795, -0.32, -0.17, 0.17, 0.62, 0.645, ry=0.03, panel=True)            # the cover, slid aside
    box("Composite", -0.25, 0.25, -0.176, -0.148, 0.38, 0.5)
    for x0, x1 in ((-0.2, -0.14), (-0.1, -0.04), (0.04, 0.1), (0.14, 0.2)):
        box("Rubber", x0, x1, -0.181, -0.17, 0.41, 0.47, bev=0.0)
    cyl("Steel", 0.13, 0.075, -0.76, 0.76, 0.025, seg=10, axis="X")                             # feed pipe under the tank
    cable((0.6, -0.15, 0.45), (0.7, -0.15, 0.012), sag=0.04, r=0.012)
    r = random.Random(29)
    for cx in (-0.12, 0.28, 0.6):                                                                 # three clumps of dead stalks
        for i in range(8):
            a = i * 2 * math.pi / 8 + r.random() * 0.5
            h = 0.22 + r.random() * 0.2
            out = 0.04 + r.random() * 0.06
            base = (cx + 0.02 * math.cos(a), 0.02 * math.sin(a), 0.6)
            p1 = (cx + out * math.cos(a), out * 0.6 * math.sin(a), 0.62 + h)
            tip = (cx + (out + 0.12) * math.cos(a), (out + 0.12) * 0.5 * math.sin(a), 0.62 + h - 0.1 - r.random() * 0.08)
            rod("Rust", base, p1, 0.009, seg=5)
            rod("Rust", p1, tip, 0.006, seg=5)
    for x0 in (-0.6, -0.45):                                                                     # stalks lying on the cover
        rod("Rust", (x0, -0.1, 0.645), (x0 + 0.2, 0.08, 0.645), 0.008, seg=5)
    for x, top in ((-0.7, 1.0), (0.7, 0.955)):
        box("Steel", x - 0.02, x + 0.02, 0.12, 0.16, 0.6, top)
    box("Composite", -0.75, 0.75, 0.1, 0.17, 0.96, 1.02, ry=0.035)
    box("Glass", -0.7, 0.7, 0.11, 0.16, 0.945, 0.97, ry=0.035, bev=0.0)
    cable((-0.72, 0.15, 0.99), (-0.79, 0.16, 0.6), sag=0.02, r=0.006)
    dp.export("planter_divider")


def mobile_whiteboard():
    """Mobile schematic board: a wheeled steel frame holding a perforated steel sheet, slipped on one pivot, with
    taped-up printouts (one torn and hanging), a clipboard on a hook and a tool tray."""
    for x in (-0.68, 0.68):
        box("Paint", x - 0.022, x + 0.022, -0.022, 0.022, 0.06, 1.88)
        box("Paint", x - 0.028, x + 0.028, -0.27, 0.27, 0.05, 0.1)
        box("Steel", x - 0.03, x + 0.03, -0.03, 0.03, 1.86, 1.9)
        for y in (-0.24, 0.24):
            box("Steel", x - 0.02, x + 0.02, y - 0.02, y + 0.02, 0.03, 0.07)
            cyl("Rubber", y, 0.03, x - 0.016, x + 0.016, 0.03, seg=12, axis="X")
    box("Paint", -0.68, 0.68, -0.015, 0.015, 0.33, 0.37)
    ang = 0.03
    box("Steel", -0.64, 0.64, -0.012, 0.012, 0.86, 1.76, ry=ang, panel=True)
    for x in (-0.66, 0.66):
        cyl("Steel", 0.0, 1.31 - x * ang, x - 0.03, x + 0.03, 0.02, seg=10, axis="X")
    r = random.Random(37)
    for k, (cx, cz) in enumerate(((-0.42, 1.5), (-0.18, 1.52), (0.12, 1.47), (0.38, 1.5), (-0.3, 1.12), (0.0, 1.14))):
        cz2 = cz - cx * ang
        rz = (r.random() - 0.5) * 0.12
        box("Composite", cx - 0.105, cx + 0.105, -0.02, -0.006, cz2 - 0.148, cz2 + 0.148, ry=ang + rz, bev=0.0)
        box("Rubber", cx - 0.03, cx + 0.03, -0.023, -0.016, cz2 + 0.13, cz2 + 0.15, bev=0.0)
    box("Composite", 0.3, 0.51, -0.025, -0.006, 0.98, 1.27, ry=0.7, bev=0.0)                    # torn, hanging by a corner
    box("Rubber", 0.47, 0.53, -0.027, -0.016, 1.2, 1.22, bev=0.0)
    box("Steel", 0.55, 0.57, -0.04, -0.008, 1.34, 1.37)                                         # hook and clipboard
    box("Composite", 0.48, 0.64, -0.035, -0.02, 1.05, 1.34, rx=0.03)
    box("Steel", 0.53, 0.59, -0.04, -0.03, 1.31, 1.34)
    box("Steel", -0.5, 0.5, -0.1, 0.01, 0.84, 0.86)
    box("Steel", -0.5, 0.5, -0.11, -0.09, 0.84, 0.88)
    rod("Rust", (-0.3, -0.06, 0.86), (-0.12, -0.04, 0.87), 0.008)                               # a screwdriver
    dp.export("mobile_whiteboard")


def file_pedestal():
    """Small parts cabinet on casters: four steel bins with label plates, one bin missing, a dented rusted corner."""
    box("Paint", -0.195, 0.195, -0.255, 0.27, 0.06, 0.6, panel=True)
    box("Steel", -0.2, 0.2, -0.27, 0.28, 0.59, 0.615)
    for k in range(4):
        z0 = 0.08 + k * 0.128
        if k == 1:
            box("Rubber", -0.18, 0.18, -0.262, -0.25, z0, z0 + 0.12, bev=0.0)
            continue
        box("Paint", -0.185, 0.185, -0.278, -0.248, z0, z0 + 0.12)
        box("Steel", -0.07, 0.07, -0.29, -0.27, z0 + 0.075, z0 + 0.095)
        box("Composite", -0.15, -0.09, -0.284, -0.272, z0 + 0.03, z0 + 0.06, bev=0.0)
    box("Rust", 0.12, 0.2, -0.282, -0.24, 0.06, 0.12, bev=0.0)
    for x in (-0.15, 0.15):
        for y in (-0.2, 0.2):
            box("Steel", x - 0.02, x + 0.02, y - 0.02, y + 0.02, 0.025, 0.07)
            cyl("Rubber", y, 0.025, x - 0.012, x + 0.012, 0.025, seg=10, axis="X")
    dp.export("file_pedestal")


def flipchart():
    """Survey tripod with a status board: steel legs (one splinted with tape), a steel board leaning back with pinned
    printouts and a clamp bar, one sheet half torn off, a tray with a grease pencil."""
    k = 0.15 / 0.96
    t = -math.atan(k)
    n = (0.0, -math.cos(t), -math.sin(t))
    for sx in (-1, 1):
        rod("Steel", (sx * 0.315, -0.255, 0.0), (sx * 0.3, 0.0, 1.72), 0.014)
        sphere("Rubber", sx * 0.315, -0.255, 0.022, 0.022, seg=8)
    rod("Steel", (0.0, 0.0, 1.72), (0.0, 0.28, 0.0), 0.014)
    sphere("Rubber", 0.0, 0.28, 0.022, 0.022, seg=8)
    for zz in (0.55, 0.6, 0.65):
        fz = zz / 1.72
        cyl("Rubber", -0.315 + 0.015 * fz, -0.255 + 0.255 * fz, zz - 0.02, zz + 0.02, 0.022, seg=8)
    box("Paint", -0.34, 0.34, -0.03, 0.03, 1.71, 1.765)
    bc = (0.0, -0.17 + (1.26 - 0.78) * k, 1.26)
    box("Steel", -0.33, 0.33, bc[1] - 0.012, bc[1] + 0.012, bc[2] - 0.47, bc[2] + 0.47, rx=t, panel=True)
    def on(u, v, off):
        d = (0.0, math.sin(-t), math.cos(-t))
        return (u, bc[1] + d[1] * v + n[1] * off, bc[2] + d[2] * v + n[2] * off)
    p = on(0.0, 0.44, 0.015)
    box("Paint", -0.32, 0.32, p[1] - 0.015, p[1] + 0.015, p[2] - 0.025, p[2] + 0.025, rx=t)
    r = random.Random(19)
    for u, v in ((-0.14, 0.2), (0.14, 0.2), (-0.14, -0.15)):
        p = on(u, v, 0.008)
        box("Composite", u - 0.105, u + 0.105, p[1] - 0.005, p[1] + 0.005, p[2] - 0.148, p[2] + 0.148, rx=t,
            ry=(r.random() - 0.5) * 0.08, bev=0.0)
    p = on(0.14, -0.2, 0.01)
    box("Composite", 0.04, 0.25, p[1] - 0.005, p[1] + 0.005, p[2] - 0.14, p[2] + 0.1, rx=t, ry=0.5, bev=0.0)
    p = on(0.0, -0.46, 0.03)
    box("Paint", -0.32, 0.32, p[1] - 0.035, p[1] + 0.035, p[2] - 0.012, p[2] + 0.012)
    rod("Rust", (-0.2, p[1] - 0.01, p[2] + 0.015), (-0.05, p[1] - 0.015, p[2] + 0.015), 0.007, seg=6)
    dp.export("flipchart")


def sofa_pod():
    """Crew rest booth: a steel shell with a canopy, two cracked vinyl bunks (one split and sagging, one back pad
    slumped), a dead status panel and a reading lamp, an oxygen mask hanging on its hose."""
    box("Steel", -0.86, 0.86, -0.42, 0.42, 0.0, 0.05)
    for sx in (-1, 1):
        x0, x1 = sorted((sx * 0.8, sx * 0.893))
        box("Paint", x0, x1, -0.44, 0.44, 0.04, 0.98, panel=True)
        x0, x1 = sorted((sx * 0.805, sx * 0.888))
        box("Paint", x0, x1, 0.0, 0.437, 0.96, 1.36)
        rod("Paint", (sx * 0.85, -0.43, 0.96), (sx * 0.85, 0.02, 1.37), 0.035)
    box("Paint", -0.82, 0.82, 0.3, 0.445, 0.046, 1.355, panel=True)
    box("Steel", -0.9, 0.9, 0.0, 0.452, 1.34, 1.4)
    box("Composite", -0.3, 0.3, -0.008, 0.01, 1.35, 1.39, bev=0.0)
    for x0, x1 in ((-0.25, -0.15), (-0.1, 0.0), (0.05, 0.15), (0.2, 0.25)):
        box("Rubber", x0, x1, -0.012, -0.004, 1.355, 1.385, bev=0.0)
    box("Paint", -0.815, 0.815, -0.43, 0.32, 0.043, 0.34)
    for k in range(6):
        box("Rubber", -0.6 + k * 0.08, -0.56 + k * 0.08, -0.44, -0.42, 0.12, 0.26, bev=0.0)
    box("Vinyl", -0.8, -0.02, -0.42, 0.28, 0.33, 0.45, panel=True)
    box("Vinyl", 0.02, 0.4, -0.42, 0.28, 0.33, 0.45)
    box("Vinyl", 0.42, 0.8, -0.42, 0.28, 0.3, 0.43, rx=0.06, ry=0.05)                # split and sagging
    box("Composite", 0.38, 0.45, -0.3, 0.2, 0.33, 0.44, bev=0.0)                                  # foam in the split
    box("Vinyl", -0.79, -0.03, 0.2, 0.33, 0.48, 1.02, rx=-0.12, panel=True)
    box("Vinyl", 0.03, 0.79, 0.15, 0.31, 0.45, 0.85, rx=-0.32)                         # slumped back pad
    # status panel on the left wing, reading lamp under the canopy, oxygen mask
    box("Composite", -0.81, -0.78, 0.05, 0.3, 1.02, 1.22)
    box("ScreenText", -0.785, -0.775, 0.08, 0.27, 1.05, 1.19, bev=0.0)
    cyl("Composite", 0.5, 0.2, 1.26, 1.345, 0.07, seg=14, r2=0.05)
    cable((-0.2, 0.25, 1.34), (-0.12, 0.0, 0.85), sag=-0.05, r=0.008)
    sphere("Composite", -0.12, -0.01, 0.8, 0.06, seg=10, sz=1.3)
    dp.export("sofa_pod")


# --------------------------------------------------------------------------- office

def executive_desk():
    """Supervisor's desk: a steel desk on two drawer pedestals (one drawer gone, its front on the floor), a slanted
    terminal with the last report, a keypad, an articulated inspection lamp, papers, a mug, a binder."""
    box("Steel", -0.89, 0.89, -0.44, 0.44, 0.715, 0.76, panel=True)
    for sx in (-1, 1):
        x = sx * 0.66
        box("Paint", x - 0.23, x + 0.23, -0.4, 0.4, 0.05, 0.72, panel=True)
        box("Rubber", x - 0.21, x + 0.21, -0.37, 0.37, 0.0, 0.06, bev=0.0)
        for top, bot in ((0.7, 0.55), (0.54, 0.39), (0.38, 0.07)):
            if sx < 0 and top == 0.54:
                box("Rubber", x - 0.2, x + 0.2, -0.41, -0.395, bot + 0.01, top - 0.01, bev=0.0)
                continue
            box("Paint", x - 0.215, x + 0.215, -0.425, -0.395, bot + 0.005, top - 0.005)
            box("Steel", x - 0.08, x + 0.08, -0.44, -0.42, top - 0.05, top - 0.035)
    box("Paint", -0.2, 0.22, -0.32, 0.08, 0.0, 0.03, rz=0.3)                                   # the drawer front on the floor
    box("Paint", -0.44, 0.44, 0.3, 0.34, 0.25, 0.72)
    # terminal: base with a keypad, the display housing tilted back
    box("Composite", 0.02, 0.5, -0.28, 0.21, 0.75, 0.79)
    keys(0.24, -0.15, 0.79, w=0.4, d=0.16, seed=7)
    tt = -0.25
    box("Composite", 0.04, 0.46, 0.08, 0.2, 0.78, 1.0, rx=tt)
    fy, fz = 0.14 - 0.06 * math.cos(tt), 0.89 - 0.06 * math.sin(tt)                             # the front face centre
    ny, nz = -math.cos(tt), -math.sin(tt)
    screen("ScreenText", 0.25, fy + ny * 0.004, fz + nz * 0.004, 0.36, 0.18, face=-1, bulge=0.004, tilt=tt)
    for dz in (0.095, -0.095):
        by, bz = fy + math.sin(-tt) * dz, fz + math.cos(-tt) * dz
        box("Steel", 0.05, 0.45, by - 0.012, by + 0.012, bz - 0.01, bz + 0.01, rx=tt, bev=0.002)
    for x in (0.06, 0.44):
        box("Steel", x - 0.02, x + 0.02, fy - 0.011, fy + 0.011, fz - 0.085, fz + 0.085, rx=tt, bev=0.002)
    cable((0.25, 0.2, 0.8), (0.4, 0.38, 0.0), sag=0.1, r=0.006)
    # inspection lamp where our banker's lamp is
    box("Steel", -0.72, -0.56, 0.24, 0.38, 0.75, 0.78)
    rod("Paint", (-0.64, 0.33, 0.77), (-0.6, 0.36, 0.95), 0.012)
    rod("Paint", (-0.6, 0.36, 0.95), (-0.64, 0.3, 1.04), 0.012)
    box("Composite", -0.79, -0.49, 0.19, 0.31, 1.0, 1.07, rx=0.05)
    box("Screen", -0.76, -0.52, 0.21, 0.29, 0.993, 1.005, bev=0.0)
    sheets(-0.25, -0.15, 0.76, 3, 8)
    dp.mug(-0.75, -0.2, 0.75)
    box("Vinyl", -0.4, -0.1, 0.12, 0.38, 0.75, 0.81, rz=0.15)                                   # a binder lying flat
    dp.export("executive_desk")


def filing_cabinet():
    """Records locker: four stencilled steel drawers with pulls, one drawer front torn off showing the folders, dents
    and rust at the foot."""
    box("Rubber", -0.21, 0.21, -0.26, 0.28, 0.0, 0.05)
    box("Paint", -0.225, 0.225, -0.265, 0.29, 0.04, 1.28, panel=True)
    box("Steel", -0.23, 0.23, -0.275, 0.3, 1.27, 1.3)
    for k in range(4):
        bot = 0.06 + k * 0.302
        top = bot + 0.29
        if k == 1:
            box("Rubber", -0.2, 0.2, -0.275, -0.26, bot + 0.01, top - 0.01, bev=0.0)
            for i in range(7):
                x = -0.17 + i * 0.05
                box("Composite" if i % 3 else "Vinyl", x, x + 0.04, -0.27, -0.2, bot + 0.03, top - 0.05 - (i % 2) * 0.03,
                    ry=(i - 3) * 0.04, bev=0.0)
            continue
        box("Paint", -0.215, 0.215, -0.295, -0.26, bot, top)
        box("Steel", -0.09, 0.09, -0.3, -0.29, top - 0.07, top - 0.05)
        box("Composite", -0.05, 0.05, -0.298, -0.29, top - 0.13, top - 0.1, bev=0.0)
        box("Rubber", 0.1, 0.12, -0.298, -0.29, bot + 0.05, bot + 0.12, bev=0.0)                  # stencil digit
        if k >= 2:
            box("Rubber", 0.14, 0.16, -0.298, -0.29, bot + 0.05, bot + 0.12, bev=0.0)
    box("Rust", -0.225, -0.1, -0.297, -0.27, 0.06, 0.16, bev=0.0)
    cyl("Steel", 0.16, 1.24, -0.302, -0.288, 0.012, seg=10, axis="Y")
    dp.export("filing_cabinet")


def credenza():
    """Low equipment locker: four steel doors on a plinth (one hanging off its top hinge), vent slats, a stencil plate,
    papers and a clipboard on the steel top."""
    for x in (-0.72, 0.72):
        for y in (-0.15, 0.17):
            box("Steel", x - 0.035, x + 0.035, y - 0.035, y + 0.035, 0.0, 0.11)
    box("Paint", -0.79, 0.79, -0.225, 0.225, 0.1, 0.72, panel=True)
    box("Steel", -0.8, 0.8, -0.245, 0.23, 0.71, 0.75, panel=True)
    for k in range(4):
        x0 = -0.785 + k * 0.394
        x1 = x0 + 0.384
        if k == 2:
            box("Rubber", x0 + 0.01, x1 - 0.01, -0.234, -0.218, 0.14, 0.68, bev=0.0)
            box("Paint", x0, x1, -0.247, -0.222, 0.1, 0.64, ry=0.1)                                 # hanging askew
            continue
        box("Paint", x0, x1, -0.247, -0.222, 0.13, 0.69, panel=True)
        hx = x1 - 0.05 if k % 2 == 0 else x0 + 0.05
        box("Steel", hx - 0.012, hx + 0.012, -0.248, -0.232, 0.45, 0.6)
        if k == 0:
            for i in range(5):
                box("Rubber", x0 + 0.06, x1 - 0.06, -0.251, -0.236, 0.2 + i * 0.03, 0.212 + i * 0.03, bev=0.0)
    box("Composite", -0.32, -0.08, -0.252, -0.236, 0.6, 0.65, bev=0.0)                           # stencil plate
    for x0, x1 in ((-0.3, -0.26), (-0.24, -0.2), (-0.17, -0.1)):
        box("Rubber", x0, x1, -0.255, -0.245, 0.61, 0.64, bev=0.0)
    sheets(0.35, 0.0, 0.75, 3, 12)
    box("Composite", -0.6, -0.35, -0.12, 0.2, 0.74, 0.755, rz=-0.2)                              # clipboard
    box("Steel", -0.55, -0.45, 0.1, 0.17, 0.75, 0.76, rz=-0.2)
    dp.export("credenza")


def guest_chair():
    """Bolted visitor chair: a square steel tube frame on floor plates, a cracked vinyl seat pad and back, one armrest
    snapped off."""
    for x in (-0.25, 0.25):
        for y in (-0.22, 0.21):
            box("Paint", x - 0.015, x + 0.015, y - 0.015, y + 0.015, 0.006, 0.42)
            box("Steel", x - 0.035, x + 0.035, y - 0.035, y + 0.035, 0.0, 0.012)
        box("Paint", x - 0.012, x + 0.012, -0.22, 0.21, 0.12, 0.15)
        rod("Paint", (x, 0.21, 0.41), (x, 0.265, 0.89), 0.015, seg=8)
    box("Paint", -0.24, 0.24, -0.012, 0.012, 0.12, 0.15)
    box("Steel", -0.265, 0.265, -0.24, 0.225, 0.4, 0.43, panel=True)
    box("Vinyl", -0.24, 0.0, -0.235, 0.2, 0.42, 0.48, panel=True)
    box("Vinyl", 0.0, 0.24, -0.235, 0.2, 0.42, 0.475, ry=0.04, panel=True)                         # the crack
    box("Composite", -0.01, 0.015, -0.2, 0.18, 0.43, 0.47, bev=0.0)
    box("Steel", -0.24, 0.24, 0.23, 0.255, 0.55, 0.87, rx=-0.11, panel=True)
    box("Vinyl", -0.22, 0.22, 0.2, 0.235, 0.58, 0.84, rx=-0.11, panel=True)
    rod("Paint", (-0.25, -0.2, 0.42), (-0.25, -0.18, 0.63), 0.013, seg=8)
    rod("Paint", (-0.25, 0.2, 0.6), (-0.25, -0.2, 0.63), 0.013, seg=8)
    box("Vinyl", -0.275, -0.225, -0.2, 0.14, 0.62, 0.66)
    rod("Paint", (0.25, -0.2, 0.42), (0.25, -0.19, 0.52), 0.013, seg=8)                            # the snapped arm
    dp.export("guest_chair")


def wall_clock():
    """Stopped station chronometer: a bolted square steel housing, a yellowed dial with a crack, a missing hour mark,
    hands stuck at 3:47, a stencil plate and a dead status LED."""
    cz = 2.175
    box("Paint", -0.17, 0.17, -0.014, 0.03, 2.005, 2.345)
    cyl("Steel", 0.0, cz, -0.022, -0.008, 0.155, seg=28, axis="Y")
    cyl("Composite", 0.0, cz, -0.024, -0.018, 0.135, seg=28, axis="Y")
    for k in range(12):
        if k == 7:
            continue
        a = 2 * math.pi * k / 12
        ln = 0.03 if k % 3 == 0 else 0.016
        rr = 0.12 - ln / 2
        wd = 0.008 if k % 3 == 0 else 0.005
        box("Rubber", rr * math.sin(a) - wd / 2, rr * math.sin(a) + wd / 2, -0.027, -0.021, cz + rr * math.cos(a) - ln / 2,
            cz + rr * math.cos(a) + ln / 2, ry=a, bev=0.0)
    for ln, wd, a, y0 in ((0.075, 0.012, 2 * math.pi * (3 + 47 / 60) / 12, -0.027), (0.11, 0.007, 2 * math.pi * 47 / 60, -0.0275)):
        c = ln / 2 - 0.015
        box("Rubber", c * math.sin(a) - wd / 2, c * math.sin(a) + wd / 2, y0, -0.0195, cz + c * math.cos(a) - ln / 2,
            cz + c * math.cos(a) + ln / 2, ry=a, bev=0.0)
    cyl("Steel", 0.0, cz, -0.029, -0.0215, 0.01, seg=8, axis="Y")
    rod("Rubber", (-0.09, -0.0245, cz + 0.08), (-0.02, -0.0245, cz + 0.02), 0.002, seg=4)
    rod("Rubber", (-0.02, -0.0245, cz + 0.02), (0.03, -0.0245, cz - 0.06), 0.002, seg=4)
    bolts([(-0.15, 2.025), (0.15, 2.025), (-0.15, 2.325), (0.15, 2.325)], -0.016, r=0.008, depth=0.01)
    box("Composite", -0.05, 0.05, -0.019, -0.01, 2.012, 2.03, bev=0.0)
    box("Screen", 0.11, 0.125, -0.019, -0.01, 2.015, 2.027, bev=0.0)
    dp.export("wall_clock")


# --------------------------------------------------------------------------- meeting room

def conference_table():
    """Long steel briefing table: panelled top around a dead holo-projector well (a cracked lens on an emitter ring,
    the well's cover plate lying askew on the table), two pedestal legs, a projector housing with cables below."""
    T0, T1 = 0.71, 0.75
    box("Steel", -1.78, -0.29, -0.58, 0.58, T0, T1, panel=True)
    box("Steel", 0.29, 1.78, -0.58, 0.58, T0, T1, panel=True)
    for y0, y1 in ((-0.58, -0.24), (0.24, 0.58)):
        box("Steel", -0.3, 0.3, y0, y1, T0, T1)
    for sy in (-1, 1):
        y0, y1 = sorted((sy * 0.56, sy * 0.6))
        box("Paint", -1.8, 1.8, y0, y1, 0.69, 0.74)
    box("Rust", -0.3, 0.3, -0.25, 0.25, 0.6, 0.72)                                             # the well
    for x0, x1, y0, y1 in ((-0.305, 0.305, -0.262, -0.235), (-0.305, 0.305, 0.235, 0.262), (-0.315, -0.285, -0.236, 0.236), (0.285, 0.315, -0.236, 0.236)):
        box("Composite", x0, x1, y0, y1, 0.7, 0.755)
    cyl("Steel", 0, 0, 0.7, 0.73, 0.19, seg=20)
    cyl("Glass", 0, 0, 0.725, 0.738, 0.15, seg=20)
    for k in range(8):
        a = k * math.pi / 4
        cyl("Composite" if k != 3 else "Rust", 0.17 * math.cos(a), 0.17 * math.sin(a), 0.72, 0.745, 0.012, seg=6)
    rod("Rubber", (-0.1, 0.02, 0.739), (0.05, -0.06, 0.739), 0.003, seg=4)
    box("Steel", 0.85, 1.45, -0.32, 0.18, 0.742, 0.758, rz=0.22)                    # the well's cover
    for sx in (-1, 1):
        box("Paint", sx * 1.05 - 0.1, sx * 1.05 + 0.1, -0.35, 0.35, 0.03, 0.72)
        box("Steel", sx * 1.05 - 0.2, sx * 1.05 + 0.2, -0.5, 0.5, 0.0, 0.04)
    box("Paint", -1.0, 1.0, -0.04, 0.04, 0.58, 0.64)
    cyl("Composite", 0, 0, 0.02, 0.61, 0.14, seg=16, r2=0.11)
    cyl("Steel", 0, 0, 0.0, 0.04, 0.2, seg=16)
    cable((0.1, -0.08, 0.55), (0.45, -0.35, 0.012), sag=0.12, r=0.01)
    cable((-0.1, 0.1, 0.5), (-0.5, 0.3, 0.01), sag=0.08, r=0.008)
    sheets(-1.2, -0.2, T1, 4, 21)
    sheets(0.6, 0.3, T1, 2, 22)
    box("Composite", -0.8, -0.6, 0.15, 0.4, 0.742, 0.765, rz=0.4)                                   # a data slate
    box("Glass", -0.78, -0.62, 0.18, 0.37, 0.76, 0.768, rz=0.4, bev=0.0)
    dp.export("conference_table")


def projector_screen():
    """Torn screen in a steel frame: a bolted roller case, steel side rails and a bottom bar, the yellowed screen
    ripped across the lower right with a flap curling forward and ragged strips."""
    box("Paint", -0.96, 0.96, -0.05, 0.08, 2.48, 2.6, panel=True)
    for sx in (-1, 1):
        box("Steel", sx * 0.975 - 0.025, sx * 0.975 + 0.025, -0.055, 0.08, 2.47, 2.605)
        box("Steel", sx * 0.95 - 0.02, sx * 0.95 + 0.02, -0.012, 0.028, 0.91, 2.49)
    box("Steel", -0.965, 0.965, -0.02, 0.03, 0.9, 0.94)
    bolts([(-0.8, 2.54), (0.8, 2.54)], -0.048, r=0.01)
    # the screen: the upper part whole, the lower left still hanging, the lower right torn away
    box("Composite", -0.935, 0.935, -0.002, 0.008, 1.55, 2.49, bev=0.0)
    box("Composite", -0.935, 0.05, -0.001, 0.007, 0.93, 1.56, bev=0.0)
    box("Composite", 0.04, 0.2, -0.0015, 0.0065, 1.2, 1.56, ry=-0.15, bev=0.0)
    h, ang = 0.42, -0.1
    hz = 1.555
    yc, zc = 0.003 + 0.5 * h * math.sin(ang), hz - 0.5 * h * math.cos(ang)
    box("Composite", 0.21, 0.6, yc - 0.004, yc + 0.004, zc - h / 2, zc + h / 2, rx=ang, ry=0.08, bev=0.0)
    box("Composite", 0.62, 0.72, -0.002, 0.008, 1.38, 1.56, ry=0.1, bev=0.0)
    box("Composite", 0.78, 0.935, -0.002, 0.008, 0.93, 1.1, ry=-0.08, bev=0.0)
    box("Rust", -0.93, 0.93, -0.007, 0.012, 2.44, 2.485, bev=0.0)                                  # the black border, gone rusty
    for x in (-0.932, 0.932):
        box("Rust", x - 0.02, x + 0.02, -0.0085, 0.0125, 0.935, 2.48, bev=0.0)
    dp.export("projector_screen")


def av_credenza():
    """Comms cabinet: a steel carcass on a plinth, two doors (one sagging off a hinge), an open bay with two rack units
    with status lights and a radio set with dials, a field comms box with a handset on its cord on top."""
    box("Steel", -0.75, 0.75, -0.2, 0.2, 0.0, 0.09)
    box("Paint", -0.79, 0.79, -0.235, 0.24, 0.08, 0.74, panel=True)
    box("Steel", -0.8, 0.8, -0.255, 0.245, 0.73, 0.765, panel=True)
    box("Paint", -0.78, -0.27, -0.258, -0.228, 0.12, 0.7, panel=True)
    box("Steel", -0.33, -0.31, -0.265, -0.244, 0.3, 0.55)
    box("Paint", 0.27, 0.78, -0.258, -0.228, 0.1, 0.68, ry=-0.06, panel=True)                         # sagging door
    box("Steel", 0.31, 0.33, -0.265, -0.244, 0.28, 0.52, ry=-0.06)
    box("Rubber", -0.26, 0.26, -0.245, -0.228, 0.12, 0.72, bev=0.0)                                # the dark bay
    for z, hgt in ((0.212, 0.09), (0.292, 0.08)):
        box("Steel", -0.24, 0.24, -0.255, -0.2, z - hgt / 2 + 0.003, z + hgt / 2 - 0.003)
        for k in range(3):
            surf = "Screen" if k != 1 or z < 0.25 else "Rubber"
            x = 0.14 + k * 0.025
            box(surf, x - 0.006, x + 0.006, -0.262, -0.25, z - 0.006, z + 0.006, bev=0.0)
        for x in (-0.22, -0.18):
            box("Steel", x - 0.008, x + 0.008, -0.263, -0.25, z - hgt / 2 + 0.012, z + hgt / 2 - 0.012, bev=0.001)
    box("Composite", -0.23, 0.23, -0.255, -0.2, 0.4, 0.62, panel=True)                               # radio set
    box("ScreenText", -0.18, 0.0, -0.258, -0.242, 0.53, 0.6, bev=0.0)
    for x in (0.06, 0.14):
        cyl("Rubber", x, 0.565, -0.266, -0.238, 0.025, seg=10, axis="Y")
    for x in (-0.15, -0.05, 0.05, 0.15):
        cyl("Steel", x, 0.45, -0.263, -0.238, 0.012, seg=8, axis="Y")
    cable((-0.1, -0.255, 0.18), (0.12, -0.255, 0.17), sag=0.035, r=0.006)
    # field comms box and its handset on top (where our speakerphone is)
    box("Composite", 0.24, 0.56, -0.13, 0.13, 0.755, 0.8, panel=True)
    box("Screen", 0.36, 0.44, -0.04, 0.04, 0.795, 0.802, bev=0.0)
    box("Composite", 0.0, 0.22, -0.17, -0.1, 0.755, 0.785, rz=0.15)
    cable((0.22, -0.13, 0.775), (0.3, -0.12, 0.77), sag=-0.0, r=0.006, n=3)
    rod("Steel", (0.5, 0.1, 0.79), (-0.2, 0.18, 0.77), 0.007)                                       # the antenna, folded
    dp.export("av_credenza")


def meeting_chair():
    """Crew operator seat: a pedestal bolted to a floor plate, a cracked vinyl seat, a high steel back shell with a
    torn pad showing the foam, a headrest, armrests and harness straps (one cut and hanging)."""
    box("Steel", -0.24, 0.24, -0.24, 0.24, 0.0, 0.03, panel=True)
    bolts([(-0.2, -0.2), (0.2, -0.2), (-0.2, 0.2), (0.2, 0.2)], 0.025, axis="Z")
    cyl("Paint", 0, 0, 0.02, 0.4, 0.06, seg=16)
    box("Steel", -0.16, 0.16, -0.16, 0.16, 0.36, 0.41)
    box("Steel", -0.25, 0.25, -0.25, 0.22, 0.39, 0.42, panel=True)
    box("Vinyl", -0.24, 0.24, -0.24, 0.2, 0.41, 0.49, panel=True)
    box("Paint", -0.26, 0.26, 0.2, 0.265, 0.4, 1.0, rx=-0.05, panel=True)
    box("Vinyl", -0.22, 0.22, 0.165, 0.215, 0.5, 0.8, rx=-0.05, panel=True)
    box("Composite", -0.2, 0.2, 0.18, 0.22, 0.78, 0.93, rx=-0.05)                                     # the foam
    box("Vinyl", -0.21, -0.05, 0.168, 0.21, 0.8, 0.88, rx=-0.05, rz=0.04)                             # a flap of the pad
    box("Vinyl", -0.13, 0.13, 0.2, 0.28, 0.95, 1.05, rx=-0.05, panel=True)
    for sx in (-1, 1):
        box("Paint", sx * 0.25 - 0.02, sx * 0.25 + 0.02, -0.2, 0.22, 0.6, 0.64)
        box("Paint", sx * 0.25 - 0.015, sx * 0.25 + 0.015, -0.17, -0.13, 0.41, 0.61)
    rod("Rubber", (-0.12, 0.21, 0.9), (-0.08, -0.12, 0.47), 0.014, seg=6)
    rod("Rubber", (0.12, 0.21, 0.9), (0.13, 0.12, 0.62), 0.014, seg=6)                               # the cut strap
    box("Steel", -0.1, -0.06, -0.06, -0.02, 0.5, 0.6, rx=0.6)
    dp.export("meeting_chair")


PROPS = [info_kiosk, security_turnstile, round_bench_planter, logo_totem, queue_barrier, standing_desk, planter_divider,
         mobile_whiteboard, file_pedestal, sofa_pod, executive_desk, filing_cabinet, credenza, guest_chair, wall_clock,
         conference_table, projector_screen, flipchart, av_credenza, meeting_chair]

if __name__ == "__main__":
    dp.clear()
    for make in PROPS:
        make()
    print("DONE")
