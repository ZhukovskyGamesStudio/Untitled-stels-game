"""Abandoned-facility (derelict family) replacements for the game-room, lab and studio pieces of rooms2_fun.py: crew
recreation gear gone dead, research gear left behind, a comms booth stripped of its foam.

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_rooms_fun.py -- Assets/_Game/Art/Models/StyleProps/Derelict [names...]

Writes <out>/<our model>.fbx, modelled to the size of our model (Art/Models/Props/<name>.fbx; x right, -y the front,
standing on z = 0). Parts are named by surface (see derelict_props.py); screens come with their own Screen/Glass parts, so
the game hides our glowing Eye parts. Decoration only: no colliders, deterministic.
"""
import math
import os
import random
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import derelict_props as dp  # noqa: E402


# --------------------------------------------------------------------------- helpers

def obox(surface, c, size, rx=0.0, ry=0.0, rz=0.0, **kw):
    """A box by centre and size, rotated about its centre."""
    return dp.box(surface, c[0] - size[0] / 2, c[0] + size[0] / 2, c[1] - size[1] / 2, c[1] + size[1] / 2,
                  c[2] - size[2] / 2, c[2] + size[2] / 2, rx=rx, ry=ry, rz=rz, **kw)


def on(c, a, x, v, w):
    """A point on a plane through c tilted by rx = a: x across, v along the slope, w along its normal."""
    return (c[0] + x, c[1] + v * math.cos(a) - w * math.sin(a), c[2] + v * math.sin(a) + w * math.cos(a))


def framed(kind, c, w, h, rx=0.0, frame=0.025, depth=0.02, surf="Steel", bulge=0.006):
    """A screen facing -Y (tilted by rx about X; rx = -pi/2 looks up) in a frame its edges run into; the frame reaches
    `depth` behind the screen, into the housing."""
    bms = [dp.screen(kind, 0, 0, 0, w + 0.016, h + 0.016, face=-1, bulge=bulge, nx=6, ny=4)]
    for z0, z1 in ((h / 2, h / 2 + frame), (-h / 2 - frame, -h / 2)):
        bms.append(dp.box(surf, -w / 2 - frame, w / 2 + frame, -0.008, depth, z0, z1, bev=0.003))
    for x0, x1 in ((-w / 2 - frame, -w / 2), (w / 2, w / 2 + frame)):
        bms.append(dp.box(surf, x0, x1, -0.008, depth, -h / 2, h / 2, bev=0.003))
    m = Matrix.Rotation(rx, 3, "X")
    for bm in bms:
        if rx:
            bmesh.ops.rotate(bm, cent=Vector(), matrix=m, verts=bm.verts)
        bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)


def turn(bms, ang, px, py):
    """Swing pieces about a vertical axis through (px, py)."""
    m = Matrix.Rotation(ang, 3, "Z")
    for bm in bms:
        bmesh.ops.rotate(bm, cent=Vector((px, py, 0)), matrix=m, verts=bm.verts)


def sheets(cx, cy, z, n, seed, spread=0.2):
    """A few yellowed sheets on a surface at height z (sunk a little into it)."""
    r = random.Random(seed)
    for i in range(n):
        x, y = cx + (r.random() - 0.5) * spread, cy + (r.random() - 0.5) * spread * 0.6
        dp.box("Composite", x - 0.105, x + 0.105, y - 0.148, y + 0.148, z - 0.003 + i * 0.002, z + 0.002 + i * 0.002,
               rz=(r.random() - 0.5) * 0.8, bev=0.0)


def crt(cx, y_front, z_base, w=0.4, h=0.32, depth=0.38, face=-1, kind="ScreenText", stand=True):
    """dp.crt with its top vents kept on the front of the tapered housing (where the top still is)."""
    s = -face
    zc = z_base + (0.06 if stand else 0.0) + h / 2 + 0.03
    yb = y_front + s * depth
    y0, y1 = sorted((y_front, yb))
    dp.tapered_box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, y0, y1, zc - h / 2 - 0.05, zc + h / 2 + 0.045,
                   back=0.62, back_side=s)
    lip = sorted((y_front, y_front + face * 0.02))
    dp.box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, lip[0], lip[1], zc + h / 2, zc + h / 2 + 0.045)
    dp.box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, lip[0], lip[1], zc - h / 2 - 0.05, zc - h / 2)
    for sx in (-1, 1):
        dp.box("Composite", cx + sx * (w / 2 + 0.0175) - 0.0175, cx + sx * (w / 2 + 0.0175) + 0.0175, lip[0], lip[1],
               zc - h / 2, zc + h / 2)
    dp.screen(kind, cx, y_front + face * 0.004, zc, w + 0.01, h + 0.01, face=face, bulge=0.01, nx=6, ny=4)
    yl = y_front + face * 0.02
    dp.box("Screen", cx + w / 2 - 0.03, cx + w / 2 - 0.015, min(yl + face * 0.006, yl - face * 0.01), max(yl + face * 0.006, yl - face * 0.01),
           zc - h / 2 - 0.035, zc - h / 2 - 0.022, bev=0.0)
    for k in range(5):
        vx = cx - w / 2 + 0.04 + k * (w - 0.08) / 4
        ya, yb2 = sorted((y_front + s * 0.03, y_front + s * 0.1))
        dp.box("Rubber", vx - 0.012, vx + 0.012, ya, yb2, zc + h / 2 + 0.025, zc + h / 2 + 0.05, bev=0.0)
    if stand:
        dp.cyl("Composite", cx, (y0 + y1) / 2, z_base, z_base + 0.015, 0.11, seg=16)
        dp.cyl("Composite", cx, (y0 + y1) / 2, z_base + 0.01, z_base + 0.07, 0.05, seg=12, r2=0.07)
    return zc


# --------------------------------------------------------------------------- game room

def air_hockey():
    """Air-flow inspection table: a steel cabinet with a perforated deck between raised rails (the right rail torn off
    and lying underneath), sample trays and a toppled specimen jar on it, a fan housing with a torn hose, a dead
    readout on the front."""
    for x in (-0.85, 0.85):
        for y in (-0.4, 0.4):
            dp.box("Paint", x - 0.05, x + 0.05, y - 0.05, y + 0.05, 0.02, 0.64)
            dp.box("Rubber", x - 0.07, x + 0.07, y - 0.07, y + 0.07, 0.0, 0.03)
    dp.box("Paint", -0.98, 0.98, -0.53, 0.53, 0.62, 0.79, panel=True)
    dp.box("Paint", -0.3, 0.3, -0.22, 0.22, 0.46, 0.64)                                     # fan housing
    dp.cyl("Rubber", 0.0, 0.0, 0.43, 0.47, 0.17, seg=16)
    dp.cable((0.28, -0.1, 0.52), (0.55, -0.3, 0.03), sag=0.08, r=0.03, n=6)                      # torn hose
    dp.box("Steel", -0.93, 0.93, -0.48, 0.48, 0.775, 0.80)                                  # the deck
    for i in range(11):
        for j in range(5):
            x, y = -0.8 + i * 0.16, -0.36 + j * 0.18
            if (-0.78 < x < -0.33 and -0.37 < y < -0.03) or (-0.35 < x < 0.15 and y > -0.05):
                continue                                                                    # under the tray, the sheets
            dp.box("Rubber", x - 0.014, x + 0.014, y - 0.014, y + 0.014, 0.795, 0.803, bev=0.0)
    dp.box("Paint", -1.0, 1.0, -0.55, -0.475, 0.775, 0.85)                                  # rails
    dp.box("Paint", -1.0, 1.0, 0.475, 0.55, 0.775, 0.85)
    dp.box("Paint", -1.0, -0.925, -0.48, 0.48, 0.775, 0.85)
    dp.box("Rubber", -0.99, -0.92, -0.15, 0.15, 0.79, 0.83, bev=0.0)                        # intake slot
    dp.box("Paint", 0.925, 1.0, -0.48, -0.15, 0.775, 0.85)                                  # the torn rail's stub
    dp.box("Rust", 0.9, 0.99, -0.16, -0.12, 0.77, 0.84, bev=0.0)
    dp.box("Paint", 0.25, 0.95, -0.25, -0.175, 0.0, 0.075, rz=0.25)                         # ... lying underneath
    dp.box("Composite", -0.22, 0.22, -0.55, -0.5, 0.64, 0.775)                              # readout
    framed("ScreenText", (0.0, -0.553, 0.705), 0.3, 0.08, frame=0.02)
    dp.box("Screen", 0.6, 0.64, -0.556, -0.54, 0.81, 0.822, bev=0.0)
    dp.box("Rubber", 0.68, 0.72, -0.556, -0.54, 0.81, 0.822, bev=0.0)
    dp.box("Steel", -0.75, -0.35, -0.35, -0.05, 0.79, 0.825)                                # sample tray
    for k in range(3):
        dp.cyl("Composite", -0.68 + k * 0.12, -0.2, 0.815, 0.86, 0.03, seg=10)
    dp.cyl("Glass", 0.45, 0.15, 0.795, 0.85, 0.06, seg=14)                                  # specimen jars
    dp.cyl("Steel", 0.45, 0.15, 0.845, 0.862, 0.064, seg=14)
    dp.rod("Glass", (0.1, 0.25, 0.82), (0.3, 0.3, 0.82), 0.04, seg=12)
    sheets(-0.1, 0.15, 0.80, 3, 31)
    dp.export("air_hockey")


def poker_table():
    """Cargo-crate card table: a ribbed cargo crate with corner castings and a stencil plate, a salvaged round hatch
    cover laid on top, playing cards, a fanned hand left face down, stacks of nuts for chips and a rusty can."""
    dp.box("Paint", -0.42, 0.42, -0.42, 0.42, 0.02, 0.735, panel=True)
    for sx in (-1, 1):
        for sy in (-1, 1):
            for z0, z1 in ((0.0, 0.08), (0.66, 0.74)):
                dp.box("Rust", sx * 0.4 - 0.04, sx * 0.4 + 0.04, sy * 0.4 - 0.04, sy * 0.4 + 0.04, z0, z1)
    for t in (-0.14, 0.14):
        for s in (-1, 1):
            dp.box("Paint", t - 0.03, t + 0.03, s * 0.42 - 0.015, s * 0.42 + 0.015, 0.08, 0.66)
            dp.box("Paint", s * 0.42 - 0.015, s * 0.42 + 0.015, t - 0.03, t + 0.03, 0.08, 0.66)
    dp.box("Composite", -0.32, -0.19, -0.428, -0.41, 0.3, 0.42, bev=0.0)                    # stencil plate
    dp.box("Rust", 0.05, 0.09, -0.426, -0.41, 0.1, 0.6, bev=0.0)                            # a rust run
    dp.cyl("Paint", 0, 0, 0.725, 0.77, 0.62, seg=28)                                        # the hatch cover
    dp.cyl("Steel", 0.03, -0.02, 0.76, 0.792, 0.56, seg=28)
    for k in range(6):
        a = k * math.pi / 3 + 0.2
        obox("Steel", (0.6 * math.cos(a), 0.6 * math.sin(a), 0.77), (0.06, 0.05, 0.035), rz=a)
    r = random.Random(1707)
    for k, (x, y, yaw) in enumerate(((-0.12, -0.1, 0.2), (-0.06, -0.12, -0.15), (0.1, 0.12, 0.6), (-0.2, 0.2, 1.1),
                                     (0.25, -0.18, -0.4))):
        obox("Composite", (x, y, 0.79 + 0.001 * k), (0.064, 0.089, 0.0075), rz=yaw, bev=0.0)
    for k in range(4):                                                                       # a hand left on the table
        obox("Composite", (-0.33 + 0.012 * k, -0.25, 0.79 + 0.0015 * k), (0.064, 0.089, 0.0075), rz=-0.5 + 0.25 * k,
             bev=0.0)
    for k in range(6):                                                                       # nuts for chips
        a = 2 * math.pi * k / 6 + 0.3
        h = 0.012 + 0.006 * r.randint(1, 5)
        dp.cyl("Steel", 0.42 * math.cos(a), 0.42 * math.sin(a), 0.786, 0.792 + h, 0.017, seg=6)
    dp.cyl("Steel", 0.04, 0.03, 0.786, 0.822, 0.017, seg=6)
    dp.cyl("Rust", -0.22, 0.24, 0.786, 0.829, 0.033, seg=12)                                # a dented can
    dp.cyl("Steel", 0.22, 0.22, 0.786, 0.8, 0.05, seg=12)                                   # an ashtray lid
    dp.export("poker_table")


def jukebox():
    """Comms console: a tall steel radio cabinet with a waveform scope still showing the last report, a bank of dials,
    a sloped desk of toggles and status lamps, a dented speaker grille, a handset hanging off its hook, an antenna
    stub on top."""
    dp.box("Rubber", -0.39, 0.39, -0.21, 0.29, 0.0, 0.06)
    dp.box("Paint", -0.4, 0.4, -0.22, 0.3, 0.04, 1.52, panel=True)
    dp.box("Paint", -0.4, 0.4, -0.27, 0.3, 1.5, 1.57, panel=True)                           # hood
    dp.box("Steel", -0.3, 0.3, -0.25, -0.2, 1.02, 1.44)
    framed("ScreenText", (0.0, -0.256, 1.23), 0.42, 0.3)
    for k in range(5):                                                                      # dials
        x = -0.24 + k * 0.12
        dp.cyl("Steel", x, 0.94, -0.245, -0.21, 0.032, seg=12, axis="Y")
        dp.box("Rubber", x - 0.004, x + 0.004, -0.252, -0.24, 0.94, 0.968, bev=0.0)
    C, a = (0.0, -0.2, 0.76), 0.35
    obox("Composite", C, (0.74, 0.18, 0.08), rx=a, panel=True)                               # the sloped desk
    for k in range(8):
        obox("Steel", on(C, a, -0.28 + k * 0.08, -0.03, 0.045), (0.02, 0.02, 0.03), rx=a - 0.3 * (k == 5), bev=0.0)
    for k in range(6):
        obox("Screen" if k % 3 else "Rubber", on(C, a, -0.25 + k * 0.1, 0.05, 0.04), (0.035, 0.02, 0.014), rx=a, bev=0.0)
    dp.box("Steel", -0.3, 0.3, -0.24, -0.21, 0.12, 0.6, rz=0.02)                            # speaker grille
    for k in range(10):
        if k == 6:
            continue
        z = 0.14 + k * 0.045
        dp.box("Rubber", -0.28, 0.28, -0.247, -0.235, z, z + 0.02, bev=0.0)
    dp.box("Rust", -0.29, -0.1, -0.245, -0.236, 0.38, 0.44, rx=0.1, bev=0.0)               # the dent
    dp.box("Steel", 0.32, 0.37, -0.25, -0.21, 0.6, 0.64)                                    # hook
    dp.rod("Rubber", (0.345, -0.245, 0.61), (0.34, -0.255, 0.44), 0.007, seg=6)             # cord
    dp.box("Composite", 0.31, 0.37, -0.275, -0.235, 0.25, 0.45)                             # handset
    dp.box("Composite", 0.305, 0.375, -0.28, -0.23, 0.245, 0.29)
    dp.rod("Steel", (0.28, 0.15, 1.56), (0.3, 0.17, 1.6), 0.014)
    dp.cable((-0.38, 0.2, 1.3), (-0.36, 0.27, 0.0), sag=0.1, r=0.01)
    dp.export("jukebox")


def pinball():
    """Diagnostic simulator cabinet: a sloped instrument deck on bolted legs — a keypad and knobs at the front, the rest
    under cracked dark glass between steel rails — a tall rear unit with a CRT still showing the last report, its
    service hatch gone and a cable bundle spilling out of the hole."""
    t = math.atan2(0.1, 1.3)
    for x in (-0.31, 0.31):
        for y in (-0.6, 0.5):
            dp.box("Paint", x - 0.035, x + 0.035, y - 0.035, y + 0.035, 0.02, 0.64)
            dp.box("Rubber", x - 0.05, x + 0.05, y - 0.05, y + 0.05, 0.0, 0.03)
    dp.box("Paint", -0.36, 0.36, -0.7, 0.6, 0.6, 0.92)
    C = (0.0, -0.05, 0.92)
    obox("Paint", C, (0.73, 1.31, 0.1), rx=t, panel=True)
    obox("Composite", on(C, t, 0, -0.5, 0.055), (0.66, 0.24, 0.03), rx=t)                   # control strip
    for i in range(4):
        for j in range(3):
            if (i, j) in ((1, 1), (3, 0)):
                continue
            obox("Rubber", on(C, t, -0.26 + i * 0.055, -0.58 + j * 0.05, 0.072), (0.04, 0.035, 0.02), rx=t, bev=0.0)
    for x in (0.08, 0.2):
        p = on(C, t, x, -0.5, 0.07)
        dp.cyl("Steel", p[0], p[1], p[2] - 0.005, p[2] + 0.03, 0.03, seg=12)
    obox("Glass", on(C, t, 0.26, -0.47, 0.07), (0.07, 0.1, 0.012), rx=t)
    obox("Screen", on(C, t, 0.29, -0.58, 0.072), (0.02, 0.02, 0.014), rx=t, bev=0.0)
    obox("Glass", on(C, t, 0, 0.05, 0.048), (0.62, 0.9, 0.016), rx=t)                       # the cracked glass
    for sx in (-1, 1):
        obox("Steel", on(C, t, sx * 0.33, -0.05, 0.06), (0.05, 1.25, 0.04), rx=t)
    for a_, b_ in (((-0.1, -0.2), (0.12, 0.1)), ((0.12, 0.1), (0.26, 0.3)), ((0.12, 0.1), (-0.05, 0.4)),
                   ((0.12, 0.1), (0.3, -0.1))):
        dp.rod("Rubber", on(C, t, a_[0], a_[1], 0.0585), on(C, t, b_[0], b_[1], 0.0585), 0.003, seg=4)
    obox("Steel", on(C, t, 0, -0.62, 0.06), (0.72, 0.07, 0.03), rx=t)                      # lockdown bar
    dp.box("Paint", -0.37, 0.37, 0.44, 0.7, 0.96, 1.84, panel=True)                         # rear unit
    dp.box("Paint", -0.375, 0.375, 0.36, 0.695, 1.82, 1.9)
    framed("ScreenText", (0.0, 0.432, 1.5), 0.54, 0.42)
    for sx in (-1, 1):
        dp.box("Steel", sx * 0.18 - 0.1, sx * 0.18 + 0.1, 0.43, 0.45, 1.06, 1.2)
        for k in range(4):
            z = 1.08 + k * 0.03
            dp.box("Rubber", sx * 0.18 - 0.09, sx * 0.18 + 0.09, 0.424, 0.436, z, z + 0.014, bev=0.0)
    dp.box("Rubber", -0.14, 0.14, -0.705, -0.69, 0.64, 0.84, bev=0.0)                       # the hatch is gone
    dp.box("Rust", -0.16, 0.16, -0.705, -0.695, 0.62, 0.64, bev=0.0)
    dp.cable((-0.08, -0.7, 0.7), (0.02, -0.55, 0.0), sag=0.08, r=0.012)
    dp.cable((0.0, -0.7, 0.72), (0.18, -0.6, 0.0), sag=0.1, r=0.009)
    dp.cable((0.06, -0.7, 0.76), (-0.2, -0.45, 0.0), sag=0.05, r=0.007)
    dp.box("Screen", 0.22, 0.25, -0.712, -0.695, 0.86, 0.875, bev=0.0)
    dp.export("pinball")


def gaming_corner():
    """Briefing console: a low steel equipment cabinet (one door hanging off a hinge, a tape deck and cassettes in the
    open bay), a wide monitor on a bracket showing snow, a speaker bar and a joystick controller on top."""
    dp.box("Paint", -0.79, 0.79, -0.27, 0.29, 0.04, 0.35, panel=True)
    dp.box("Steel", -0.8, 0.8, -0.285, 0.3, 0.34, 0.375, panel=True)
    for x in (-0.74, 0.74):
        for y in (-0.22, 0.22):
            dp.box("Rubber", x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0.0, 0.05)
    dp.box("Paint", -0.78, -0.29, -0.29, -0.26, 0.07, 0.32, panel=True)
    dp.box("Steel", -0.4, -0.34, -0.3, -0.28, 0.22, 0.25)
    dp.box("Paint", 0.3, 0.77, -0.293, -0.263, 0.05, 0.3, ry=0.07, panel=True)               # the hanging door
    dp.box("Rubber", -0.27, 0.27, -0.275, -0.25, 0.07, 0.32, bev=0.0)                       # open bay
    dp.box("Composite", -0.2, 0.18, -0.285, -0.0, 0.1, 0.18)                                # tape deck
    dp.box("Rubber", -0.15, 0.05, -0.29, -0.28, 0.13, 0.16, bev=0.0)
    dp.box("Screen", 0.1, 0.13, -0.291, -0.28, 0.15, 0.162, bev=0.0)
    for k in range(4):
        dp.box("Composite", -0.22 + 0.01 * k, -0.06 + 0.01 * k, -0.24, -0.14, 0.18 + k * 0.025, 0.205 + k * 0.025, rz=0.1 * k)
    dp.box("Paint", -0.08, 0.08, 0.13, 0.25, 0.36, 0.44)                                    # pedestal
    dp.box("Composite", -0.64, 0.64, 0.1, 0.27, 0.42, 1.1, panel=True)                      # the monitor
    framed("ScreenStatic", (0.0, 0.095, 0.76), 1.12, 0.56, frame=0.03)
    dp.box("Composite", -0.45, 0.45, -0.2, -0.1, 0.365, 0.43)                               # speaker bar
    for k in range(9):
        x = -0.4 + k * 0.1
        dp.box("Rubber", x, x + 0.06, -0.206, -0.19, 0.38, 0.415, bev=0.0)
    dp.box("Composite", 0.5, 0.68, -0.24, -0.1, 0.365, 0.41)                                # joystick
    dp.rod("Steel", (0.59, -0.17, 0.405), (0.61, -0.18, 0.52), 0.012)
    dp.sphere("Rubber", 0.61, -0.18, 0.525, 0.025, seg=10)
    dp.cable((0.5, 0.27, 0.6), (0.7, 0.28, 0.0), sag=0.1)
    dp.cable((0.55, -0.1, 0.39), (0.62, 0.0, 0.45), sag=0.05, r=0.005)
    dp.export("gaming_corner")


# --------------------------------------------------------------------------- lab

def fume_hood():
    """Containment hood: a steel base cabinet (its middle door missing), a dark work top with a sample rack and a
    broken flask, the hood with the glass sash jammed half open and cracked (a corner gone), a dented exhaust duct,
    a hose down the side and a status panel with its last report."""
    dp.box("Paint", -0.74, 0.74, -0.35, 0.39, 0.0, 0.9, panel=True)
    for x in (-0.48, 0.48):
        dp.box("Paint", x - 0.235, x + 0.235, -0.37, -0.34, 0.1, 0.82)
        dp.box("Steel", x - 0.08, x + 0.08, -0.385, -0.365, 0.72, 0.745)
    dp.box("Rubber", -0.235, 0.235, -0.36, -0.33, 0.1, 0.82, bev=0.0)                       # the missing door
    dp.cable((-0.1, -0.34, 0.8), (0.1, -0.33, 0.0), sag=-0.05, r=0.012)
    dp.box("Steel", -0.75, 0.75, -0.4, 0.4, 0.89, 0.93)                                     # work top
    for sx in (-1, 1):
        dp.box("Paint", sx * 0.68 - 0.06, sx * 0.68 + 0.06, -0.36, 0.38, 0.92, 1.79)
    dp.box("Paint", -0.64, 0.64, 0.32, 0.39, 0.92, 1.79)                                     # back wall
    for z in (1.1, 1.4, 1.65):
        dp.box("Rubber", -0.5, 0.5, 0.31, 0.33, z, z + 0.03, bev=0.0)                        # baffle slots
    dp.box("Paint", -0.75, 0.75, -0.4, 0.4, 1.77, 2.07, panel=True)                         # top housing
    dp.box("Composite", 0.22, 0.62, -0.405, -0.39, 1.83, 2.01)
    framed("ScreenText", (0.42, -0.402, 1.92), 0.3, 0.12, frame=0.02, depth=0.015)
    for k in range(4):
        z = 1.84 + k * 0.05
        dp.box("Rubber", -0.6, -0.2, -0.41, -0.385, z, z + 0.025, bev=0.0)
    dp.box("Steel", -0.62, 0.62, -0.37, -0.33, 1.33, 1.38)                                  # the sash
    for sx in (-1, 1):
        dp.box("Steel", sx * 0.6 - 0.02, sx * 0.6 + 0.02, -0.37, -0.33, 1.37, 1.79)
    dp.rod("Steel", (-0.45, -0.38, 1.355), (0.45, -0.38, 1.355), 0.014)
    for sx in (-1, 1):
        dp.rod("Steel", (sx * 0.45, -0.38, 1.355), (sx * 0.45, -0.36, 1.355), 0.009)
    dp.box("Glass", -0.59, 0.25, -0.355, -0.347, 1.37, 1.79)
    dp.box("Glass", 0.24, 0.59, -0.355, -0.347, 1.55, 1.79)                                 # lower right corner gone
    for a_, b_ in (((-0.2, 1.6), (0.1, 1.45)), ((0.1, 1.45), (0.24, 1.52)), ((-0.2, 1.6), (-0.45, 1.75)),
                   ((-0.2, 1.6), (-0.3, 1.38))):
        dp.rod("Rubber", (a_[0], -0.358, a_[1]), (b_[0], -0.358, b_[1]), 0.003, seg=4)
    obox("Glass", (0.42, -0.12, 0.932), (0.12, 0.08, 0.01), rz=0.5)                        # the shard on the top
    dp.rod("Paint", (0.0, 0.08, 2.05), (0.03, 0.1, 2.18), 0.16, seg=16)                     # exhaust duct
    dp.cyl("Rubber", 0.0, 0.08, 2.06, 2.1, 0.175, seg=16)
    dp.rod("Rubber", (0.735, 0.3, 1.75), (0.735, 0.25, 0.92), 0.015)                        # hose
    dp.box("Steel", -0.5, -0.2, 0.05, 0.25, 0.925, 0.96)                                    # sample rack
    for k in range(4):
        dp.cyl("Composite", -0.46 + k * 0.08, 0.15, 0.93, 1.05 - 0.02 * (k % 2), 0.018, seg=8)
    dp.cyl("Glass", -0.05, 0.12, 0.925, 1.05, 0.045, seg=12)
    dp.rod("Glass", (0.15, 0.05, 0.968), (0.33, 0.1, 0.962), 0.045, seg=12)                 # the broken flask
    dp.export("fume_hood")


def printer_3d():
    """Fabricator unit: a steel stand with feedstock canisters, an enclosed frame behind a cracked glass door with a
    failed half-print fused to the bed, the print head jammed, a canister on a side holder feeding a hose into the
    roof, a dead control panel."""
    for x in (-0.31, 0.31):
        for y in (-0.31, 0.31):
            dp.box("Paint", x - 0.0225, x + 0.0225, y - 0.0225, y + 0.0225, 0.0, 0.78)
    dp.box("Steel", -0.35, 0.35, -0.35, 0.35, 0.74, 0.79, panel=True)
    dp.box("Steel", -0.33, 0.33, -0.33, 0.33, 0.13, 0.16)
    dp.cyl("Composite", -0.15, -0.05, 0.155, 0.4, 0.09, seg=16)
    dp.cyl("Rust", 0.15, 0.1, 0.155, 0.35, 0.08, seg=16)
    dp.rod("Composite", (0.05, -0.22, 0.2), (0.25, -0.15, 0.2), 0.045, seg=12)
    dp.box("Paint", -0.29, 0.29, -0.29, 0.29, 0.78, 0.88, panel=True)                        # enclosure
    for x in (-0.27, 0.27):
        for y in (-0.27, 0.27):
            dp.box("Paint", x - 0.02, x + 0.02, y - 0.02, y + 0.02, 0.87, 1.55)
    dp.box("Paint", -0.3, 0.3, -0.3, 0.3, 1.54, 1.6, panel=True)
    dp.box("Steel", -0.26, 0.26, 0.255, 0.285, 0.87, 1.55)
    dp.box("Steel", -0.285, -0.255, -0.26, 0.26, 0.87, 1.55)
    dp.box("Glass", 0.258, 0.268, -0.26, 0.26, 0.87, 1.55)
    dp.box("Glass", -0.26, 0.26, -0.272, -0.262, 0.87, 1.55)                                # the door
    dp.box("Steel", 0.2, 0.23, -0.29, -0.265, 1.1, 1.3)
    for a_, b_ in (((-0.05, 1.3), (0.15, 1.45)), ((-0.05, 1.3), (-0.2, 1.1)), ((-0.05, 1.3), (-0.22, 1.42)),
                   ((0.15, 1.45), (0.24, 1.4))):
        dp.rod("Rubber", (a_[0], -0.274, a_[1]), (b_[0], -0.274, b_[1]), 0.003, seg=4)
    for x in (-0.12, 0.12):
        dp.rod("Steel", (x, 0.225, 0.87), (x, 0.225, 1.55), 0.008, seg=6)
    dp.box("Steel", -0.22, 0.22, -0.22, 0.225, 1.07, 1.09)                                  # the bed
    dp.sphere("Composite", 0.0, -0.03, 1.11, 0.07, seg=12, sz=0.6)                          # the failed print
    dp.sphere("Composite", 0.07, 0.02, 1.1, 0.04, seg=10, sz=0.7)
    r = random.Random(41)
    for k in range(5):
        a = r.random() * 2 * math.pi
        dp.rod("Composite", (0.0, -0.03, 1.1), (0.15 * math.cos(a), -0.03 + 0.15 * math.sin(a), 1.093), 0.004, seg=4)
    dp.rod("Steel", (-0.26, 0.02, 1.5), (0.26, 0.02, 1.5), 0.012)                           # gantry, head jammed
    dp.box("Composite", -0.02, 0.06, -0.03, 0.05, 1.44, 1.52, rz=0.15)
    dp.cyl("Steel", 0.02, 0.01, 1.4, 1.445, 0.014, seg=8, r2=0.004)
    dp.box("Composite", -0.22, -0.02, -0.31, -0.28, 0.79, 0.86)                             # control panel
    dp.box("Glass", -0.18, -0.06, -0.315, -0.3, 0.805, 0.845, bev=0.0)
    dp.box("Screen", -0.045, -0.03, -0.315, -0.3, 0.83, 0.845, bev=0.0)
    dp.cyl("Steel", 0.2, 1.3, 0.285, 0.36, 0.012, seg=8, axis="X")                         # side canister
    dp.cyl("Composite", 0.2, 1.3, 0.3, 0.365, 0.09, seg=16, axis="X")
    dp.cable((0.33, 0.2, 1.38), (0.2, 0.1, 1.58), sag=0.06, r=0.006)
    dp.export("printer_3d")


def robot_table():
    """Maintenance bench: a heavy steel table with parts cases and a drum on the shelf; on it a dismantled service synth
    lying on its back — chest plates opened with cables spilling, a faint core, a cracked visor, one arm on, the other
    in pieces beside it with its hand — a tool case, a data pad with its last report and a clamp lamp over it all."""
    dp.box("Steel", -0.9, 0.9, -0.45, 0.45, 0.85, 0.9, panel=True)
    dp.box("Paint", -0.82, 0.82, -0.37, 0.37, 0.77, 0.86)
    for x in (-0.83, 0.83):
        for y in (-0.38, 0.38):
            dp.box("Paint", x - 0.035, x + 0.035, y - 0.035, y + 0.035, 0.02, 0.86)
            dp.box("Rubber", x - 0.05, x + 0.05, y - 0.05, y + 0.05, 0.0, 0.03)
    dp.box("Steel", -0.84, 0.84, -0.39, 0.39, 0.13, 0.16)
    dp.box("Composite", -0.6, -0.2, -0.3, 0.2, 0.155, 0.35)
    dp.cyl("Rust", 0.4, 0.0, 0.155, 0.5, 0.15, seg=12)
    # the synth: torso, hips without legs, neck and head
    dp.box("Composite", -0.45, 0.05, -0.16, 0.16, 0.885, 1.07)
    dp.box("Rubber", -0.33, -0.05, -0.1, 0.1, 1.06, 1.075, bev=0.0)
    dp.cyl("Screen", -0.19, 0.0, 1.055, 1.085, 0.035, seg=12)
    for x in (-0.3, -0.2, -0.09):
        dp.rod("Steel", (x, -0.095, 1.075), (x, 0.095, 1.075), 0.008, seg=6)
    obox("Composite", (-0.225, -0.32, 0.899), (0.25, 0.16, 0.02), rz=0.3)                 # the chest plate
    dp.cable((-0.25, 0.08, 1.07), (-0.1, 0.3, 0.9), sag=-0.04, r=0.008, n=5)
    dp.cable((-0.15, 0.06, 1.07), (0.05, 0.25, 0.9), sag=-0.03, r=0.006, n=5)
    dp.box("Paint", -0.58, -0.43, -0.14, 0.14, 0.89, 1.03)
    for sy in (-1, 1):
        dp.box("Rubber", -0.6, -0.57, sy * 0.08 - 0.035, sy * 0.08 + 0.035, 0.925, 0.995, bev=0.0)
    dp.cable((-0.59, 0.08, 0.96), (-0.88, 0.12, 0.55), sag=0.1, r=0.009, n=4)
    dp.cable((-0.59, -0.08, 0.95), (-0.86, -0.15, 0.62), sag=0.08, r=0.007, n=4)
    dp.cyl("Steel", 0.0, 0.98, 0.04, 0.11, 0.04, seg=10, axis="X")
    dp.box("Composite", 0.1, 0.3, -0.1, 0.1, 0.89, 1.06)
    dp.box("Glass", 0.14, 0.27, -0.075, 0.075, 1.05, 1.07, bev=0.0)
    dp.rod("Rubber", (0.16, -0.05, 1.071), (0.24, 0.06, 1.071), 0.003, seg=4)
    # the attached arm, the loose one in pieces
    dp.sphere("Paint", 0.0, -0.2, 0.98, 0.05, seg=8)
    dp.rod("Paint", (0.0, -0.22, 0.95), (-0.22, -0.26, 0.94), 0.04, seg=10)
    dp.rod("Composite", (-0.22, -0.26, 0.94), (-0.44, -0.28, 0.935), 0.033, seg=10)
    dp.box("Paint", -0.52, -0.43, -0.32, -0.25, 0.895, 0.95)
    dp.rod("Paint", (-0.3, 0.3, 0.935), (-0.08, 0.27, 0.935), 0.04, seg=10)
    dp.cable((-0.08, 0.27, 0.935), (0.0, 0.3, 0.93), sag=0.02, r=0.006, n=4)
    dp.rod("Composite", (0.0, 0.3, 0.928), (0.22, 0.34, 0.928), 0.033, seg=10)
    dp.box("Paint", 0.22, 0.3, 0.31, 0.38, 0.895, 0.94)
    for k in range(3):
        dp.rod("Steel", (0.29, 0.32 + k * 0.025, 0.905), (0.36, 0.32 + k * 0.03, 0.905), 0.008, seg=6)
    # tools, a data pad, the lamp
    dp.box("Composite", 0.45, 0.77, -0.32, -0.14, 0.895, 1.0)
    dp.rod("Steel", (0.54, -0.23, 1.0), (0.68, -0.23, 1.0), 0.012)
    obox("Steel", (0.2, -0.33, 0.898), (0.2, 0.03, 0.016), rz=0.5)
    framed("ScreenText", (0.45, 0.21, 0.918), 0.14, 0.1, rx=-math.pi / 2, frame=0.02, depth=0.03, surf="Composite")
    dp.box("Steel", 0.7, 0.8, 0.34, 0.44, 0.895, 0.95)
    dp.rod("Paint", (0.75, 0.39, 0.94), (0.72, 0.3, 1.15), 0.012)
    dp.rod("Paint", (0.72, 0.3, 1.15), (0.45, 0.06, 1.16), 0.012)
    dp.cyl("Composite", 0.43, 0.04, 1.08, 1.17, 0.1, seg=14, r2=0.035)
    dp.sphere("Screen", 0.43, 0.04, 1.085, 0.03, seg=8)
    dp.export("robot_table")


def scope_cart():
    """Crash cart: a steel trolley on casters with a drawer block (one drawer sagging open), a vitals CRT on top still
    showing the last report with its leads hanging down, a defibrillator case on the bottom shelf, a push handle."""
    for x in (-0.27, 0.27):
        for y in (-0.215, 0.215):
            dp.box("Paint", x - 0.015, x + 0.015, y - 0.015, y + 0.015, 0.105, 0.965)
    for x in (-0.25, 0.25):
        for y in (-0.19, 0.19):
            dp.cyl("Rubber", x, 0.04, y - 0.012, y + 0.012, 0.04, seg=12, axis="Y")
            dp.box("Steel", x - 0.02, x + 0.02, y - 0.02, y + 0.02, 0.05, 0.11)
    dp.box("Steel", -0.29, 0.29, -0.23, 0.23, 0.1, 0.13)
    dp.box("Composite", -0.24, 0.1, -0.18, 0.18, 0.125, 0.28)                  # defibrillator
    for y in (-0.08, 0.06):
        dp.box("Composite", -0.2, -0.06, y - 0.05, y + 0.05, 0.275, 0.31)
    dp.box("Screen", 0.03, 0.06, -0.185, -0.17, 0.24, 0.255, bev=0.0)
    dp.box("Paint", -0.265, 0.265, -0.2, 0.2, 0.42, 0.95, panel=True)                       # drawer block
    for k, (z0, z1) in enumerate(((0.44, 0.6), (0.62, 0.76), (0.78, 0.92))):
        sag = 0.08 if k == 1 else 0.0
        out = 0.015 if k == 1 else 0.0
        dp.box("Paint", -0.245, 0.245, -0.215 - out, -0.19 - out, z0, z1, rx=sag)
        dp.box("Steel", -0.08, 0.08, -0.235 - out, -0.205 - out, z1 - 0.04 - out, z1 - 0.02 - out)
    dp.box("Rust", 0.265 - 0.03, 0.27, -0.12, 0.05, 0.45, 0.6, bev=0.0)
    dp.box("Steel", -0.3, 0.3, -0.24, 0.24, 0.94, 0.97)                                      # top tray
    for y in (-0.235, 0.235):
        dp.box("Paint", -0.295, 0.295, y - 0.007, y + 0.007, 0.96, 0.99)
    crt(0.0, -0.15, 0.97, w=0.26, h=0.18, depth=0.3, face=-1, kind="ScreenText", stand=False)
    for x, end in ((0.12, (0.18, -0.235, 0.5)), (0.08, (0.05, -0.23, 0.62))):
        dp.cable((x, -0.165, 0.99), end, sag=0.04, r=0.004)
        dp.box("Composite", end[0] - 0.02, end[0] + 0.02, end[1] - 0.005, end[1] + 0.005, end[2] - 0.03, end[2] + 0.005)
    for x in (-0.22, 0.22):
        dp.rod("Steel", (x, 0.215, 0.96), (x, 0.215, 1.07), 0.01)
    dp.rod("Steel", (-0.25, 0.215, 1.065), (0.25, 0.215, 1.065), 0.014)
    dp.export("scope_cart")


def component_shelf():
    """Spare-parts rack: a bolted steel rack of composite parts bins with stencilled tags, some bins gone, one tipping
    off the front with its parts spilled, the third shelf sagging, a cable draped across the front."""
    for x in (-0.585, 0.585):
        for y in (-0.205, 0.205):
            dp.box("Paint", x - 0.015, x + 0.015, y - 0.015, y + 0.015, 0.0, 1.8)
    shelves = (0.08, 0.42, 0.76, 1.1, 1.44, 1.77)
    r = random.Random(77)
    for k, z in enumerate(shelves):
        tilt = 0.03 if k == 2 else 0.0
        dp.box("Steel", -0.6, 0.6, -0.21, 0.21, z, z + 0.03, ry=tilt)
        if k == 5:
            continue
        if k < 2:
            xs, w, d, h = [-0.435 + i * 0.29 for i in range(4)], 0.27, 0.36, 0.22
        else:
            xs, w, d, h = [-0.475 + i * 0.19 for i in range(6)], 0.175, 0.3, 0.16
        for i, x in enumerate(xs):
            if r.random() < 0.15:
                continue                                                                    # a bin gone
            z0 = z + 0.02 - x * math.sin(tilt)
            tip = 0.45 if (k, i) == (3, 4) else 0.0
            yc = -0.19 + d / 2 + (0.02 if tip else 0.0)
            obox("Composite" if (k + i) % 4 else "Rust", (x, yc, z0 + h / 2 + (0.03 if tip else 0.0)), (w, d, h),
                 rx=tip, ry=tilt, bev=0.004)
            if not tip:
                dp.box("Paint", x - w * 0.3, x + w * 0.3, -0.2, -0.18, z0 + 0.02, z0 + 0.06, ry=tilt, bev=0.0)
    for k in range(4):                                                                       # the spilled parts
        dp.box("Steel", 0.2 + k * 0.05, 0.23 + k * 0.05, -0.2 + k * 0.02, -0.17 + k * 0.02, 1.125, 1.145, rz=k * 0.6, bev=0.0)
    dp.cable((-0.57, -0.215, 1.47), (0.3, -0.215, 1.47), sag=0.2, r=0.008)
    dp.export("component_shelf")


def card_catalog():
    """Specimen drawer cabinet: a steel case of shallow sample drawers with label frames and pulls on a welded stand;
    one drawer gone (a dark slot, the drawer dropped on the floor under the case), one sagging, a rust streak."""
    dp.box("Paint", -0.4, 0.4, -0.23, 0.23, 1.265, 1.3)
    dp.box("Paint", -0.38, 0.38, -0.2, 0.21, 0.37, 1.28, panel=True)
    for x in (-0.34, 0.34):
        for y in (-0.16, 0.17):
            dp.box("Paint", x - 0.02, x + 0.02, y - 0.02, y + 0.02, 0.01, 0.39)
            dp.box("Rubber", x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0.0, 0.02)
    for y in (-0.16, 0.17):
        dp.box("Paint", -0.34, 0.34, y - 0.012, y + 0.012, 0.1, 0.13)
    cols, rows, gap = 4, 6, 0.012
    w = (0.74 - (cols - 1) * gap) / cols
    h = (0.86 - (rows - 1) * gap) / rows
    for i in range(cols):
        x = -0.37 + w / 2 + i * (w + gap)
        for j in range(rows):
            z = 0.395 + h / 2 + j * (h + gap)
            if (i, j) == (3, 1):
                dp.box("Rubber", x - w / 2, x + w / 2, -0.205, -0.185, z - h / 2, z + h / 2, bev=0.0)
                continue
            sag = 0.07 if (i, j) == (1, 4) else 0.0
            dp.box("Paint", x - w / 2, x + w / 2, -0.215, -0.18, z - h / 2, z + h / 2, rx=sag, bev=0.003)
            dp.box("Composite", x - 0.035, x + 0.035, -0.222, -0.205, z + 0.02, z + 0.045, bev=0.0)
            dp.box("Steel", x - 0.03, x + 0.03, -0.228, -0.205, z - 0.035 - sag * 0.1, z - 0.02 - sag * 0.1, bev=0.0)
    # the dropped drawer under the case, its cards spilled beside it
    dp.box("Paint", -0.15, 0.03, -0.12, 0.12, 0.0, 0.1, rz=0.25)
    for k in range(5):
        dp.box("Composite", -0.12 + k * 0.025, -0.06 + k * 0.025, -0.02 + k * 0.02, 0.04 + k * 0.02, 0.093 + 0.003 * k, 0.098 + 0.003 * k,
               rz=0.3 * k, bev=0.0)
    for k in range(4):
        dp.box("Composite", 0.06 + k * 0.03, 0.14 + k * 0.03, -0.08, 0.0, 0.001 * k, 0.004 + 0.002 * k, rz=0.7 * k, bev=0.0)
    dp.box("Rust", 0.1, 0.15, -0.204, -0.196, 0.37, 0.7, bev=0.0)
    dp.export("card_catalog")


def book_cart():
    """Sample trolley: a two-tier steel cart on casters with a push bar, composite sample cases and canisters on the
    shelves, one case fallen open, a toppled canister, a clipboard hanging off the bar."""
    for x in (-0.35, 0.35):
        for y in (-0.17, 0.17):
            dp.box("Paint", x - 0.02, x + 0.02, y - 0.02, y + 0.02, 0.08, 0.92)
    for x in (-0.33, 0.33):
        for y in (-0.15, 0.15):
            dp.cyl("Rubber", y, 0.04, x - 0.012, x + 0.012, 0.04, seg=12, axis="X")
            dp.box("Steel", x - 0.02, x + 0.02, y - 0.02, y + 0.02, 0.05, 0.1)
    for z in (0.08, 0.52, 0.86):
        dp.box("Steel", -0.37, 0.37, -0.19, 0.19, z, z + 0.03, panel=True)
        for y in (-0.185, 0.185):
            dp.box("Paint", -0.37, 0.37, y - 0.008, y + 0.008, z + 0.02, z + 0.06)
    dp.box("Composite", -0.33, -0.05, -0.15, 0.15, 0.105, 0.26, panel=True)                 # bottom: cases
    dp.box("Composite", 0.0, 0.3, -0.16, 0.12, 0.105, 0.2)
    dp.box("Composite", 0.03, 0.31, -0.17, 0.1, 0.205, 0.225, rz=0.25)                      # its lid, knocked askew
    dp.box("Rust", 0.06, 0.12, -0.05, 0.03, 0.195, 0.215, bev=0.0)
    for k in range(3):                                                                       # middle: canisters
        dp.cyl("Composite", -0.27 + k * 0.12, 0.05, 0.53, 0.75 - 0.03 * k, 0.05, seg=12)
    dp.rod("Rust", (0.1, -0.1, 0.585), (0.3, -0.05, 0.585), 0.05, seg=12)
    sheets(-0.15, 0.0, 0.89, 3, 17, spread=0.1)                                             # top: papers and a case
    dp.box("Rust", 0.05, 0.3, -0.15, 0.1, 0.885, 0.97, panel=True)
    for y in (-0.17, 0.17):
        dp.rod("Steel", (0.35, y, 0.9), (0.35, y, 0.98), 0.012)
    dp.rod("Steel", (0.35, -0.18, 0.98), (0.35, 0.18, 0.98), 0.015)
    dp.box("Composite", 0.355, 0.368, -0.1, 0.1, 0.72, 0.97)                                # clipboard on the bar
    dp.box("Steel", 0.35, 0.375, -0.03, 0.03, 0.95, 0.99)
    dp.export("book_cart")


def study_carrel():
    """Monitoring cubicle: steel side panels and a work top, a back panel with a pinned schematic and torn notes, a
    small CRT showing snow, a lamp housing under the shelf with a dim strip, a keyboard and a phone off the hook."""
    for sx in (-1, 1):
        x0, x1 = sorted((sx * 0.47, sx * 0.5))
        dp.box("Paint", x0, x1, -0.35, 0.35, 0.0, 0.78, panel=True)
        dp.box("Paint", x0, x1, -0.1, 0.35, 0.76, 1.3, panel=True)
    dp.box("Steel", -0.48, 0.48, -0.34, 0.34, 0.72, 0.76)
    dp.box("Paint", -0.48, 0.48, 0.31, 0.35, 0.3, 1.29, panel=True)
    dp.box("Steel", -0.48, 0.48, 0.12, 0.33, 1.155, 1.18)
    dp.box("Paint", -0.3, 0.3, 0.14, 0.2, 1.12, 1.16)
    dp.box("Screen", -0.28, 0.28, 0.145, 0.185, 1.112, 1.124, bev=0.0)
    dp.box("Composite", -0.42, 0.42, 0.296, 0.315, 0.8, 1.1)                                # pinboard
    r = random.Random(29)
    for k in range(5):
        x = -0.35 + k * 0.17
        hh = 0.1 + r.random() * 0.08
        dp.box("Composite", x - 0.06, x + 0.06, 0.29, 0.3, 0.95 - hh / 2, 0.95 + hh / 2, ry=(r.random() - 0.5) * 0.4, bev=0.0)
    dp.box("Rust", -0.12, 0.12, 0.288, 0.298, 0.82, 0.88, bev=0.0)
    crt(-0.05, 0.0, 0.755, w=0.3, h=0.22, depth=0.28, face=-1, kind="ScreenStatic")
    dp.box("Composite", -0.32, 0.08, -0.29, -0.15, 0.755, 0.78, rx=-0.06)                   # keyboard
    for k in range(4):
        dp.box("Rubber", -0.3, 0.06, -0.275 + k * 0.032, -0.25 + k * 0.032, 0.77, 0.79, bev=0.0)
    dp.box("Composite", 0.22, 0.38, -0.05, 0.1, 0.755, 0.81)                                # phone, off the hook
    dp.box("Composite", 0.1, 0.3, -0.27, -0.21, 0.755, 0.79, rz=0.4)
    dp.cable((0.25, -0.05, 0.78), (0.2, -0.2, 0.78), sag=0.02, r=0.004)
    sheets(0.3, -0.18, 0.76, 2, 7, spread=0.08)
    dp.cable((-0.2, 0.28, 0.85), (-0.35, 0.3, 0.0), sag=0.1, r=0.006)
    dp.export("study_carrel")


def lectern():
    """Terminal pedestal: a bolted floor plate, a square steel column with a conduit, a tilted control slab with a small
    display showing the last report, a keypad with a key missing and a clipboard of yellowed sheets."""
    dp.box("Steel", -0.25, 0.25, -0.25, 0.25, 0.0, 0.04, panel=True)
    for sx in (-1, 1):
        for sy in (-1, 1):
            dp.cyl("Steel", sx * 0.2, sy * 0.2, 0.03, 0.055, 0.018, seg=6)
    dp.box("Paint", -0.08, 0.08, -0.08, 0.08, 0.03, 0.96, panel=True)
    dp.rod("Steel", (0.0, 0.088, 0.03), (0.0, 0.088, 0.95), 0.018)
    for z in (0.3, 0.7):
        dp.box("Steel", -0.03, 0.03, 0.07, 0.115, z, z + 0.03)
    dp.box("Paint", -0.2, 0.2, -0.16, 0.16, 0.93, 1.02, panel=True)
    C, a = (0.0, 0.0, 1.08), 0.35
    obox("Paint", on(C, a, 0, 0.02, -0.07), (0.36, 0.3, 0.11), rx=a)
    obox("Composite", C, (0.5, 0.44, 0.05), rx=a, panel=True)
    framed("ScreenText", on(C, a, 0, 0.08, 0.029), 0.3, 0.12, rx=a - math.pi / 2, frame=0.02, depth=0.02)
    for i in range(3):
        for j in range(3):
            if (i, j) == (1, 1):
                continue
            obox("Rubber", on(C, a, 0.1 + i * 0.04, -0.15 + j * 0.035, 0.024), (0.03, 0.028, 0.02), rx=a, bev=0.0)
    obox("Composite", on(C, a, -0.13, -0.12, 0.022), (0.16, 0.13, 0.02), rx=a, bev=0.0)    # clipboard
    obox("Composite", on(C, a, -0.13, -0.125, 0.0335), (0.14, 0.11, 0.006), rx=a, rz=0.08, bev=0.0)
    obox("Steel", on(C, a, -0.13, -0.06, 0.036), (0.06, 0.02, 0.012), rx=a, bev=0.0)
    dp.cable((0.1, 0.15, 1.12), (0.15, 0.2, 0.04), sag=0.05, r=0.007)
    dp.export("lectern")


def wingback_chair():
    """Crew acceleration seat: a high-backed steel shell with cracked vinyl padding and head wings (one ripped and
    hanging), armrests, a four-point harness hanging loose, bolted to a floor plate on a single pedestal."""
    dp.box("Steel", -0.34, 0.34, -0.36, 0.34, 0.0, 0.04, panel=True)
    for sx in (-1, 1):
        for sy in (-1, 1):
            dp.cyl("Steel", sx * 0.29, sy * 0.3, 0.03, 0.055, 0.018, seg=6)
    dp.box("Paint", -0.18, 0.18, -0.18, 0.18, 0.03, 0.3, panel=True)
    dp.box("Paint", -0.32, 0.32, -0.38, 0.28, 0.28, 0.36, panel=True)
    dp.box("Vinyl", -0.29, 0.29, -0.39, 0.22, 0.35, 0.45, panel=True)
    obox("Paint", (0.0, 0.29, 0.71), (0.64, 0.09, 0.74), rx=-0.15, panel=True)             # back shell
    obox("Vinyl", (0.0, 0.21, 0.72), (0.52, 0.09, 0.6), rx=-0.15, panel=True)
    obox("Rubber", (0.08, 0.162, 0.8), (0.14, 0.01, 0.02), rx=-0.15, rz=0.3, bev=0.0)       # a tear
    obox("Paint", (-0.27, 0.22, 0.93), (0.07, 0.22, 0.24), rx=-0.15)                       # head wings
    obox("Paint", (0.29, 0.2, 0.86), (0.07, 0.22, 0.24), rx=-0.15, ry=0.6)
    for sx in (-1, 1):
        dp.box("Paint", sx * 0.33 - 0.02, sx * 0.33 + 0.02, -0.25, -0.21, 0.34, 0.47)
        dp.box("Paint", sx * 0.33 - 0.035, sx * 0.33 + 0.035, -0.32, 0.2, 0.46, 0.52)
        dp.box("Vinyl", sx * 0.33 - 0.03, sx * 0.33 + 0.03, -0.3, 0.15, 0.51, 0.545)
    for sx in (-1, 1):                                                                       # the harness
        dp.rod("Rubber", (sx * 0.12, 0.24, 1.0), (sx * 0.1, 0.12, 0.82), 0.012, seg=4)
        dp.rod("Rubber", (sx * 0.1, 0.12, 0.82), (sx * 0.06, -0.05, 0.6), 0.012, seg=4)
        dp.rod("Rubber", (sx * 0.06, -0.05, 0.6), (0.0, -0.2, 0.465), 0.012, seg=4)
    dp.rod("Rubber", (-0.29, 0.0, 0.45), (0.0, -0.2, 0.46), 0.012, seg=4)
    dp.rod("Rubber", (0.29, -0.05, 0.45), (0.36, -0.1, 0.1), 0.012, seg=4)                  # the loose belt
    dp.box("Steel", -0.04, 0.04, -0.24, -0.17, 0.44, 0.475)
    dp.export("wingback_chair")


# --------------------------------------------------------------------------- studio

def guitar_stand():
    """Gas bottle in a tripod cradle: a dented steel cylinder with a hazard band leaning back in a stand, its valve, a
    regulator with a gauge and a hose looping down to the floor."""
    lean = math.radians(12)
    base = Vector((0.0, -0.1, 0.07))
    ax = Vector((0.0, math.sin(lean), math.cos(lean)))
    top = base + ax * 0.84
    dp.rod("Paint", base, top, 0.09, seg=16)
    dp.sphere("Paint", top.x, top.y, top.z, 0.09, seg=16)
    dp.rod("Rust", base + ax * 0.5, base + ax * 0.6, 0.094, seg=16)
    p = top + ax * 0.14
    dp.rod("Steel", top + ax * 0.05, p, 0.025, seg=10)
    dp.cyl("Rubber", p.x, p.y, p.z - 0.01, p.z + 0.015, 0.04, seg=12)
    dp.box("Steel", -0.03, 0.03, p.y - 0.1, p.y, p.z - 0.07, p.z - 0.02)
    dp.cyl("Steel", 0.0, p.z - 0.045, p.y - 0.12, p.y - 0.09, 0.04, seg=14, axis="Y")
    dp.cyl("Glass", 0.0, p.z - 0.045, p.y - 0.124, p.y - 0.115, 0.033, seg=14, axis="Y")
    dp.cable((0.02, p.y - 0.06, p.z - 0.07), (0.15, -0.15, 0.02), sag=0.15, r=0.01)
    dp.cyl("Steel", 0.15, -0.15, 0.0, 0.03, 0.016, seg=8)
    hub = (0.0, 0.06, 0.16)
    dp.sphere("Steel", *hub, 0.025, seg=10)
    for f in ((-0.2, -0.17, 0.012), (0.2, -0.17, 0.012), (0.0, 0.18, 0.012)):
        dp.rod("Paint", hub, f, 0.011)
        dp.cyl("Rubber", f[0], f[1], 0.0, 0.024, 0.02, seg=10)
    for sx in (-1, 1):
        dp.rod("Paint", hub, (sx * 0.08, -0.12, 0.07), 0.01)
        dp.box("Rubber", sx * 0.08 - 0.03, sx * 0.08 + 0.03, -0.15, -0.09, 0.05, 0.09)
    dp.rod("Paint", hub, (0.0, 0.15, 0.72), 0.011)
    dp.box("Steel", -0.1, 0.1, 0.115, 0.16, 0.7, 0.74)
    dp.export("guitar_stand")


def synth_stand():
    """Patch-bay console on a scissor stand: a long steel panel of toggles and jacks with patch cords looping between
    them, a raised rear strip with knobs and a dim status lamp, one leg of the stand bent, a cable bundle to the floor."""
    for sign, y in ((1, 0.13), (-1, 0.095)):
        x0, x1 = -0.32 * sign, 0.32 * sign
        for yy in (-y, y):
            if sign < 0 and yy < 0:                                                          # the bent leg
                dp.rod("Paint", (x0, yy, 0.03), (0.04, yy, 0.4), 0.014, seg=8)
                dp.rod("Paint", (0.04, yy, 0.4), (x1, yy, 0.83), 0.014, seg=8)
            else:
                dp.rod("Paint", (x0, yy, 0.03), (x1, yy, 0.83), 0.014, seg=8)
        dp.cyl("Paint", x0, 0.022, -0.22, 0.22, 0.018, seg=10, axis="Y")
        for sy in (-1, 1):
            dp.cyl("Rubber", x0, 0.022, sy * 0.225 - 0.015, sy * 0.225 + 0.015, 0.022, seg=10, axis="Y")
        dp.cyl("Paint", x1, 0.83, -0.18, 0.18, 0.016, seg=10, axis="Y")
        for sy in (-1, 1):
            dp.box("Rubber", x1 - 0.02, x1 + 0.02, sy * 0.12 - 0.03, sy * 0.12 + 0.03, 0.835, 0.86)
    dp.cyl("Steel", 0.0, 0.43, -0.16, 0.16, 0.025, seg=10, axis="Y")
    dp.box("Paint", -0.6, 0.6, -0.18, 0.18, 0.85, 0.92, panel=True)                          # the console
    for sx in (-1, 1):
        dp.box("Steel", sx * 0.625 - 0.025, sx * 0.625 + 0.025, -0.19, 0.19, 0.84, 0.94)
    Cr, a = (0.0, 0.115, 0.9275), 0.25
    obox("Composite", Cr, (1.2, 0.15, 0.055), rx=a)
    for k in range(6):
        p = on(Cr, a, -0.5 + k * 0.09, -0.01, 0.03)
        dp.box("Steel", p[0] - 0.012, p[0] + 0.012, p[1] - 0.012, p[1] + 0.012, p[2] - 0.01, p[2] + 0.014, rz=0.4, bev=0.0)
    obox("Screen", on(Cr, a, 0.5, -0.005, 0.0275), (0.08, 0.04, 0.012), rx=a, bev=0.0)
    obox("Rubber", on(Cr, a, 0.38, -0.005, 0.0275), (0.04, 0.04, 0.012), rx=a, bev=0.0)
    jacks = []
    for row, yr in enumerate((-0.13, -0.06)):
        for k in range(12):
            x = -0.55 + k * 0.095
            if row == 0:
                dp.box("Steel", x - 0.006, x + 0.006, yr - 0.006, yr + 0.006, 0.91, 0.94, rx=0.3 * (k % 3 == 1), bev=0.0)
            else:
                dp.box("Rubber", x - 0.011, x + 0.011, yr - 0.011, yr + 0.011, 0.91, 0.928, bev=0.0)
                jacks.append((x, yr, 0.926))
    for i, j in ((0, 3), (2, 7), (4, 5), (6, 11), (8, 10)):
        dp.cable(jacks[i], jacks[j], sag=-0.025 - 0.003 * i, r=0.004, n=4)
    dp.box("Rust", -0.6, -0.3, -0.186, -0.176, 0.86, 0.88, bev=0.0)
    dp.cable((0.4, 0.17, 0.86), (0.5, 0.235, 0.0), sag=0.08, r=0.012)
    dp.export("synth_stand")


def amp_cabinet():
    """Portable power cell: a steel case with rubber corner guards, a vent grille with a slat missing, two gauges, jacks
    and a switch on the control strip, a status lamp where our pilot light is, a carry handle, a cable trailing off."""
    dp.box("Paint", -0.33, 0.33, -0.18, 0.19, 0.03, 0.67, panel=True)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x0, x1 = sorted((sx * 0.305, sx * 0.355))
            y0, y1 = (-0.2, -0.15) if sy < 0 else (0.155, 0.205)
            for z0, z1 in ((0.0, 0.08), (0.62, 0.69)):
                dp.box("Rubber", x0, x1, y0, y1, z0, z1)
    dp.box("Steel", -0.27, 0.27, -0.19, -0.17, 0.1, 0.46)
    for k in range(9):
        if k == 5:
            continue
        z = 0.12 + k * 0.037
        dp.box("Rubber", -0.26, 0.26, -0.2, -0.182, z, z + 0.014, bev=0.0)
    dp.box("Composite", -0.3, 0.3, -0.195, -0.17, 0.5, 0.63)
    for x in (-0.2, -0.07):
        dp.cyl("Steel", x, 0.565, -0.2, -0.185, 0.045, seg=14, axis="Y")
        dp.cyl("Glass", x, 0.565, -0.2025, -0.195, 0.036, seg=14, axis="Y")
    for x in (0.06, 0.12):
        dp.cyl("Rubber", x, 0.545, -0.2, -0.185, 0.012, seg=8, axis="Y")
    dp.box("Steel", 0.16, 0.18, -0.205, -0.19, 0.58, 0.61, rx=0.4, bev=0.0)
    dp.box("Screen", 0.23, 0.25, -0.2, -0.188, 0.555, 0.575, bev=0.0)
    for x in (-0.1, 0.1):
        dp.rod("Steel", (x, 0.0, 0.66), (x, 0.0, 0.7), 0.012)
    dp.rod("Steel", (-0.11, 0.0, 0.7), (0.11, 0.0, 0.7), 0.012)
    dp.cable((0.06, -0.195, 0.545), (0.25, -0.195, 0.0), sag=0.05, r=0.008)
    dp.box("Rust", -0.33, -0.15, -0.186, -0.176, 0.03, 0.09, bev=0.0)
    dp.export("amp_cabinet")


def studio_couch():
    """Crew rest bench: a bolted steel bench frame with three cracked vinyl pads (the middle one torn and sagging), a
    back rail on posts with two back pads (the middle one fallen under the bench), straps hanging, a footlocker."""
    for x in (-0.88, -0.3, 0.3, 0.88):
        for y in (-0.36, 0.36):
            dp.box("Paint", x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0.03, 0.27)
            dp.box("Rubber", x - 0.05, x + 0.05, y - 0.05, y + 0.05, 0.0, 0.04)
    dp.box("Paint", -0.95, 0.95, -0.42, 0.42, 0.26, 0.33)
    for sx in (-1, 1):
        dp.box("Paint", sx * 0.96 - 0.04, sx * 0.96 + 0.04, -0.44, 0.44, 0.0, 0.62)
    for i, x in enumerate((-0.62, 0.0, 0.62)):
        if i == 1:
            dp.box("Vinyl", x - 0.29, x + 0.29, -0.41, 0.28, 0.28, 0.38, rx=0.12, panel=True)
            dp.sphere("Composite", x + 0.08, -0.05, 0.38, 0.09, seg=10, sz=0.4)               # the foam showing
            dp.box("Vinyl", x - 0.1, x + 0.05, -0.43, -0.39, 0.15, 0.33, rx=0.2)             # a flap hanging
        else:
            dp.box("Vinyl", x - 0.3, x + 0.3, -0.41, 0.28, 0.32, 0.43, panel=True)
    for x in (-0.6, 0.6):
        dp.box("Steel", x - 0.03, x + 0.03, 0.36, 0.42, 0.3, 0.84)
    dp.box("Paint", -0.92, 0.92, 0.37, 0.43, 0.78, 0.84)
    for x in (-0.62, 0.62):
        dp.box("Vinyl", x - 0.3, x + 0.3, 0.28, 0.38, 0.45, 0.8, rx=-0.1)
    dp.box("Steel", -0.3, 0.3, 0.36, 0.38, 0.42, 0.79)                                       # the bare plate
    dp.box("Vinyl", -0.25, 0.33, -0.3, 0.05, 0.0, 0.09, rz=0.2)                              # ... its pad on the floor
    dp.box("Paint", 0.42, 0.85, -0.3, 0.25, 0.0, 0.22)                           # footlocker
    dp.box("Paint", 0.42, 0.85, -0.31, 0.26, 0.21, 0.24, rx=0.06)
    for x in (0.52, 0.75):
        dp.box("Steel", x - 0.03, x + 0.03, -0.32, -0.29, 0.15, 0.2)
    for x in (-0.75, 0.45):
        dp.box("Rubber", x, x + 0.05, 0.33, 0.39, 0.5, 0.82)
    dp.box("Rust", -0.95, -0.7, -0.425, -0.415, 0.26, 0.3, bev=0.0)
    dp.export("studio_couch")


def vocal_booth():
    """Comms booth: a panelled steel cabin on a plinth, its door jammed half open with a cracked wire-glass window,
    acoustic foam torn off the walls in patches (some lying on the floor), a wall handset hanging by its cord, a dead
    ON AIR lamp above the door and a vent box on the roof."""
    dp.box("Steel", -0.59, 0.59, -0.56, 0.585, 0.0, 0.06)
    dp.box("Paint", -0.56, 0.56, 0.52, 0.595, 0.04, 2.1, panel=True)
    for sx in (-1, 1):
        x0, x1 = sorted((sx * 0.52, sx * 0.595))
        dp.box("Paint", x0, x1, -0.57, 0.59, 0.04, 2.1, panel=True)
    dp.box("Paint", -0.6, 0.6, -0.575, 0.6, 2.04, 2.12)
    dp.box("Paint", -0.55, -0.47, -0.578, -0.5, 0.045, 2.09)
    dp.box("Paint", 0.23, 0.55, -0.578, -0.5, 0.045, 2.09)
    dp.box("Paint", -0.48, 0.24, -0.578, -0.5, 1.99, 2.09)
    dp.box("Rust", 0.3, 0.36, -0.575, -0.565, 0.5, 1.4, bev=0.0)
    # the door, built closed and swung in about its hinge
    door = [dp.box("Paint", -0.48, 0.235, -0.545, -0.51, 0.07, 1.2),
            dp.box("Paint", -0.48, 0.235, -0.545, -0.51, 1.7, 1.98),
            dp.box("Paint", -0.48, -0.3, -0.545, -0.51, 1.19, 1.71),
            dp.box("Paint", 0.06, 0.235, -0.545, -0.51, 1.19, 1.71),
            dp.box("Glass", -0.31, 0.07, -0.532, -0.522, 1.19, 1.71),
            dp.box("Steel", 0.12, 0.2, -0.57, -0.535, 0.98, 1.01),
            dp.box("Steel", 0.12, 0.2, -0.52, -0.49, 0.98, 1.01)]
    for x in (-0.18, -0.06):
        door.append(dp.rod("Steel", (x, -0.534, 1.2), (x, -0.534, 1.7), 0.002, seg=4))
    for z in (1.33, 1.46, 1.59):
        door.append(dp.rod("Steel", (-0.3, -0.534, z), (0.06, -0.534, z), 0.002, seg=4))
    for a_, b_ in (((-0.1, 1.4), (0.05, 1.6)), ((-0.1, 1.4), (-0.28, 1.5)), ((-0.1, 1.4), (-0.05, 1.21))):
        door.append(dp.rod("Rubber", (a_[0], -0.535, a_[1]), (b_[0], -0.535, b_[1]), 0.003, seg=4))
    turn(door, 0.45, -0.47, -0.53)
    for z in (0.3, 1.0, 1.7):
        dp.cyl("Steel", -0.475, -0.53, z, z + 0.12, 0.018, seg=8)
    # foam: patches on the back and side walls, a few torn off and on the floor
    r = random.Random(61)
    for i in range(3):
        for j in range(4):
            if r.random() < 0.3:
                continue
            x, z = -0.34 + i * 0.34, 0.5 + j * 0.42
            dp.box("Rubber", x - 0.15, x + 0.15, 0.48, 0.53, z - 0.15, z + 0.15, rx=0.25 * (i == 2 and j == 1))
    for sx in (-1, 1):
        for j in range(3):
            if (sx, j) == (1, 1):
                continue
            z = 0.7 + j * 0.42
            x0, x1 = sorted((sx * 0.47, sx * 0.53))
            dp.box("Rubber", x0, x1, 0.0, 0.3, z - 0.15, z + 0.15)
    dp.box("Rubber", -0.3, 0.0, 0.1, 0.4, 0.05, 0.1, rz=0.4)
    dp.box("Rubber", 0.1, 0.4, -0.1, 0.2, 0.05, 0.11, rz=-0.3)
    dp.box("Composite", 0.44, 0.53, -0.25, -0.15, 1.2, 1.45)                                # wall handset
    dp.rod("Rubber", (0.46, -0.2, 1.21), (0.4, -0.18, 0.72), 0.007, seg=6)
    dp.box("Composite", 0.37, 0.43, -0.21, -0.15, 0.55, 0.73)
    dp.box("Composite", -0.25, 0.0, -0.6, -0.55, 2.0, 2.1)                                  # ON AIR lamp
    dp.box("Glass", -0.23, -0.02, -0.607, -0.59, 2.02, 2.08, bev=0.0)
    dp.box("Paint", 0.0, 0.45, 0.0, 0.3, 2.1, 2.2)                                          # roof vent
    for k in range(4):
        x = 0.04 + k * 0.1
        dp.box("Rubber", x, x + 0.06, -0.01, 0.01, 2.12, 2.18, bev=0.0)
    dp.export("vocal_booth")


PROPS = [air_hockey, amp_cabinet, book_cart, card_catalog, component_shelf, fume_hood, gaming_corner, guitar_stand,
         jukebox, lectern, pinball, poker_table, printer_3d, robot_table, scope_cart, studio_couch, study_carrel,
         synth_stand, vocal_booth, wingback_chair]

if __name__ == "__main__":
    dp.clear()
    for make in PROPS:
        make()
    print("DONE")
