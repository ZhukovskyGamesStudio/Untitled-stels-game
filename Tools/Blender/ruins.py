"""Ruined openings between rooms: the broken edge of a breach, a pile of rubble, the frame of an inner window without glass.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/ruins.py -- Assets/_Game/Art/Models [derelict]

With `derelict` it writes the derelict family's versions instead (Styles/Derelict/Level/<name>__derelict.fbx, the same single
Body part, swapped in by mesh name): a breach torn through a station's steel wall panels (petals of sheet metal bent out to
both faces, a buckled wall stud), a pile of fallen ceiling panels with a length of duct and cable, and a heavy bolted steel
inspection-window frame with one bent bar left of its grille.

Writes Level/breach_rim.fbx, Level/rubble.fbx and Level/inner_window_frame.fbx (prefabs Level/BreachFrame, HoleFrame,
InnerWindowFrame, AssetBuilder.Level). All decor: no colliders in the game, the wall pieces keep the shape.

- breach_rim: the broken edge of a wall along one side of a hole knocked through it — irregular chunks of concrete
  standing 2-9 cm into the gap and a little proud of both faces. 2.3 m tall (the game scales it to the opening; lying
  down it is the broken top). Origin at the foot of the cut line; +X into the gap, the wall's thickness along Y.
- rubble: a low pile of broken concrete (about 0.9 x 0.5 m, 0.3 m high), origin on the floor in its middle.
- inner_window_frame: a window between two rooms, its glass long gone: a frame on both faces round a 1.4 x 1.25 m
  opening whose sill is 0.9 m above the floor, a sill board and a stub of the broken mullion. Origin on the floor under
  the middle of the opening, along X.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODELS = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
DERELICT = "derelict" in argv[1:]
WALL = 0.2                       # LevelMetrics.WallThickness
RIM_H = 2.3                      # LevelMetrics.DoorHeight
WIN_W, WIN_SILL, WIN_H = 1.4, 0.9, 1.25   # clear width, sill height, opening height (LevelMetrics.InnerWindow*)


def deg(x, y, z):
    """Euler angles in degrees -> radians (shapes.place takes radians)."""
    return (math.radians(x), math.radians(y), math.radians(z))


def export(path, floor):
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=floor, name=os.path.basename(path))
    s.export_fbx(path, objs)
    print("exported", path)


def breach_rim():
    """Chunks stacked up the cut line, overlapping each other by a few centimetres (one piece of broken wall)."""
    rnd = random.Random(7)
    bms = []
    z = 0.0
    while z < RIM_H - 0.05:
        h = rnd.uniform(0.14, 0.32)
        h = min(h, RIM_H - z)
        out = rnd.uniform(0.02, 0.09)            # into the gap
        back = 0.06                               # into the wall (hidden in its block)
        deep = WALL + rnd.uniform(0.0, 0.05)      # a little proud of the faces, now one, now both
        y = rnd.uniform(-0.015, 0.015)
        bms.append(s.rbox((out + back, deep, h + 0.04), loc=((out - back) / 2, y, z + h / 2),
                          r=0.012, seg=1, rot=deg(rnd.uniform(-6, 6), rnd.uniform(-8, 8), rnd.uniform(-10, 10))))
        # a smaller broken lump on one face now and then
        if rnd.random() < 0.45:
            side = rnd.choice((-1, 1))
            bms.append(s.rbox((rnd.uniform(0.05, 0.1), 0.06, rnd.uniform(0.06, 0.12)),
                              loc=(rnd.uniform(-0.02, 0.04), side * (WALL / 2 + 0.005), z + h * rnd.uniform(0.2, 0.8)),
                              r=0.01, seg=1, rot=deg(rnd.uniform(-20, 20), rnd.uniform(-20, 20), rnd.uniform(-20, 20))))
        z += h
    s.part("Body", bms)


def rubble():
    """A pile grown chunk by chunk, each leaning on one already there: big ones low in the middle, small ones round them."""
    rnd = random.Random(11)
    bms = []
    placed = [(0.0, 0.0, 0.22)]
    bms.append(s.rbox((0.26, 0.2, 0.15), loc=(0, 0, 0.063), r=0.012, seg=1, rot=deg(4, -3, 20)))
    for i in range(15):
        px, py, ps = placed[rnd.randrange(len(placed))]
        a = rnd.uniform(0, 2 * math.pi)
        x = max(-0.42, min(0.42, px + math.cos(a) * ps * 0.45))
        y = max(-0.2, min(0.2, py + math.sin(a) * ps * 0.35))
        d = min(1.0, math.hypot(x / 0.45, y / 0.22))
        size = 0.22 * (1.0 - 0.5 * d) * rnd.uniform(0.7, 1.05)
        h = size * rnd.uniform(0.45, 0.75)
        lift = (1.0 - d) * 0.07
        bms.append(s.rbox((size, size * rnd.uniform(0.6, 1.0), h), loc=(x, y, h / 2 - 0.012 + lift),
                          r=0.012, seg=1, rot=deg(rnd.uniform(-15, 15), rnd.uniform(-15, 15), rnd.uniform(0, 180))))
        placed.append((x, y, size))
    s.part("Body", bms)


def inner_window_frame():
    """A frame on both faces, the sill board across the wall, a broken mullion stub hanging from the head."""
    bms = []
    bar = 0.07
    top = WIN_SILL + WIN_H
    for side in (-1, 1):
        y = side * (WALL / 2 + 0.012)
        # head and jambs: an L of wood standing out of the face, overlapping the opening's edge
        bms.append(s.rbox((WIN_W + 2 * bar, 0.035, bar), loc=(0, y, top + bar / 2 - 0.01), r=0.008, seg=1))
        for x in (-1, 1):
            bms.append(s.rbox((bar, 0.035, WIN_H + bar), loc=(x * (WIN_W / 2 + bar / 2 - 0.01), y, WIN_SILL + WIN_H / 2), r=0.008, seg=1))
    # the sill board, across the wall and a little over both faces
    bms.append(s.rbox((WIN_W + 0.1, WALL + 0.09, 0.04), loc=(0, 0, WIN_SILL + 0.01), r=0.01, seg=2))
    # a stub of the mullion that held the panes, snapped off
    bms.append(s.rbox((0.05, 0.035, 0.38), loc=(0.08, -(WALL / 2 + 0.012), top - 0.17), r=0.008, seg=1, rot=deg(0, 7, 0)))
    s.part("Body", bms)


# --------------------------------------------------------------------------- derelict family

def derelict_breach_rim():
    """Torn steel panels along the cut line: petals of sheet metal bent out to one face or the other, the buckled wall
    stud standing in the gap, a cut cable end (all Body: the game paints the rim with the wall's material)."""
    rnd = random.Random(23)
    bms = []
    # the wall stud at the edge of the gap, buckled half way up (the two halves overlap at the kink)
    bms.append(s.rbox((0.05, 0.1, 1.1), loc=(0.02, 0, 0.55), r=0.004, seg=1))
    bms.append(s.rbox((0.05, 0.1, RIM_H - 1.0), loc=(0.045, 0, 1.0 + (RIM_H - 1.0) / 2), r=0.004, seg=1, rot=deg(0, 4, 0)))
    z = 0.0
    side = 1
    while z < RIM_H - 0.04:
        h = min(rnd.uniform(0.22, 0.42), RIM_H - z)
        out = rnd.uniform(0.04, 0.14)                     # how far the sheet sticks into the gap
        bend = rnd.uniform(18, 48)                         # bent out of the wall's face
        side = -side if rnd.random() < 0.7 else side
        y = side * (WALL / 2 - 0.004)
        bms.append(s.rbox((out + 0.08, 0.012, h), loc=((out - 0.08) / 2 + 0.01, y + side * out * 0.25, z + h / 2),
                          r=0.003, seg=1, rot=deg(rnd.uniform(-5, 5), rnd.uniform(-6, 6), side * bend * 0.5)))
        if rnd.random() < 0.5:                             # a jagged tooth at the top of the petal
            bms.append(s.rbox((0.05, 0.012, 0.1), loc=(out * 0.6, y + side * out * 0.2, z + h - 0.03),
                              r=0.003, seg=1, rot=deg(0, rnd.uniform(-35, 35), side * bend * 0.5)))
        z += h * 0.9                                        # overlap the next petal
    # a cut cable hanging out of the wall's cavity, from inside the stud
    bms.append(s.tube([(0.02, 0.0, 1.58), (0.08, 0.01, 1.42), (0.11, -0.02, 1.25)], 0.009, res=1, segments_per=6))
    s.part("Body", bms)


def derelict_rubble():
    """Fallen ceiling panels lying on each other, a dented length of square duct over them and broken bits round it."""
    rnd = random.Random(31)
    bms = []
    for k in range(4):
        w, d = rnd.uniform(0.3, 0.4), rnd.uniform(0.22, 0.28)
        bms.append(s.rbox((w, d, 0.03), loc=(rnd.uniform(-0.07, 0.07), rnd.uniform(-0.03, 0.03), 0.015 + k * 0.02),
                          r=0.004, seg=1, rot=deg(rnd.uniform(-4, 4), rnd.uniform(-5, 5), rnd.uniform(0, 180))))
    bms.append(s.rbox((0.44, 0.13, 0.12), loc=(0.03, 0.01, 0.11), r=0.01, seg=1, rot=deg(0, 8, 28)))   # the duct
    for k in range(6):
        size = rnd.uniform(0.06, 0.1)
        a = k / 6 * 2 * math.pi + rnd.uniform(-0.3, 0.3)
        bms.append(s.rbox((size, size * 0.8, size * 0.6), loc=(math.cos(a) * 0.19, math.sin(a) * 0.1, size * 0.25),
                          r=0.008, seg=1, rot=deg(rnd.uniform(-15, 15), rnd.uniform(-15, 15), rnd.uniform(0, 180))))
    bms.append(s.tube([(-0.25, -0.08, 0.01), (-0.14, 0.0, 0.08), (0.04, 0.04, 0.15), (0.21, -0.06, 0.02)], 0.008, res=1, segments_per=6))
    s.part("Body", bms)


def derelict_inner_window_frame():
    """A heavy steel inspection-window frame on both faces (rounded corners, bolts), a sill plate through the wall, and one
    bent bar left of the grille that covered it."""
    bms = []
    bar = 0.09
    top = WIN_SILL + WIN_H
    for side in (-1, 1):
        y = side * (WALL / 2 + 0.018)
        bms.append(s.rbox((WIN_W + 2 * bar, 0.05, bar), loc=(0, y, top + bar / 2 - 0.015), r=0.018, seg=2))
        bms.append(s.rbox((WIN_W + 2 * bar, 0.05, bar), loc=(0, y, WIN_SILL - bar / 2 + 0.015), r=0.018, seg=2))
        for x in (-1, 1):
            bms.append(s.rbox((bar, 0.05, WIN_H + 0.02), loc=(x * (WIN_W / 2 + bar / 2 - 0.015), y, WIN_SILL + WIN_H / 2), r=0.018, seg=2))
            for zz in (WIN_SILL + 0.03, top - 0.03):        # bolts at the corners, sunk into the frame
                bms.append(s.rcyl(0.014, 0.03, loc=(x * (WIN_W / 2 + bar / 2 - 0.015), y + side * 0.022, zz), axis="Y", seg=8, r=0.003))
    bms.append(s.rbox((WIN_W + 0.04, WALL + 0.1, 0.035), loc=(0, 0, WIN_SILL + 0.005), r=0.008, seg=1))   # the sill plate
    # the grille's last bar: welded into the head, bent away at the bottom
    bms.append(s.tube([(0.35, -(WALL / 2 + 0.03), top + 0.02), (0.37, -(WALL / 2 + 0.05), top - 0.5), (0.31, -(WALL / 2 + 0.14), top - 0.85)],
                      0.016, res=1, segments_per=8))
    s.part("Body", bms)


def main():
    if DERELICT:
        out = os.path.join(MODELS, "Styles", "Derelict", "Level")
        os.makedirs(out, exist_ok=True)
        for name, build, floor in (("breach_rim", derelict_breach_rim, True), ("rubble", derelict_rubble, True),
                                   ("inner_window_frame", derelict_inner_window_frame, False)):
            s.clear_scene()
            build()
            export(os.path.join(out, name + "__derelict.fbx"), floor)
        return
    out = os.path.join(MODELS, "Level")
    os.makedirs(out, exist_ok=True)
    for name, build, floor in (("breach_rim", breach_rim, True), ("rubble", rubble, True), ("inner_window_frame", inner_window_frame, False)):
        s.clear_scene()
        build()
        export(os.path.join(out, name + ".fbx"), floor)


main()
