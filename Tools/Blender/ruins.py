"""Ruined openings between rooms: the broken edge of a breach, a pile of rubble, the frame of an inner window without glass.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/ruins.py -- Assets/_Game/Art/Models [derelict]

With `derelict` it writes the derelict family's inspection-window frame instead (Styles/Derelict/Level/inner_window_frame__derelict.fbx,
the same single Body part, swapped in by mesh name).

Writes Level/breach_0..2.fbx, Level/hole_0..2.fbx and Level/inner_window_frame.fbx (prefabs Level/BreachFrame, HoleFrame,
InnerWindowFrame, AssetBuilder.Level). All decor: no colliders in the game, the wall pieces keep the shape.

- breach_k, hole_k (opening()): a hole knocked through a plastered brick wall 0.3 m thick, each in three variants the house
  picks by its seed — toothed brickwork standing out of both jambs, the head hanging in steps, the mortar bed, heaps of grit,
  fallen bricks, mortar and plaster shards on both faces, the cut ends of a conduit. Parts Brick / Mortar / Plaster / Metal;
  the plaster skin round the hole on each face is the game's (AssetBuilder.RuinSkin: each room's own wall finish).
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


# --------------------------------------------------------------------------- a hole knocked through a brick wall

TRUE_WALL = 0.3                  # LevelMetrics.WallThickness: these are modelled at the wall's real thickness
BRICK = (0.25, 0.12, 0.065)      # a brick: along the wall, across it, its height
COURSE = 0.075                   # a brick and its bed of mortar


def snapped(bm, at, normal):
    """Breaks the shape off at a plane: what lies beyond it (along normal) is gone, the break is a face."""
    import bmesh
    from mathutils import Vector
    cut = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=Vector(at), plane_no=Vector(normal).normalized(), clear_outer=True)
    edges = [g for g in cut["geom_cut"] if isinstance(g, bmesh.types.BMEdge)]
    if edges:
        bmesh.ops.edgeloop_fill(bm, edges=edges)
    return bm


def brick(rnd, x0, x1, y, z, way=1, lean=0.0):
    """A brick lying along the wall from x0 (inside the wall) to its broken end at x1 (way = which way the gap is)."""
    length = abs(x1 - x0)
    bm = s.rbox((length + 0.05, BRICK[1], BRICK[2]), (0, 0, 0), r=0.0, seg=1)
    # its end broken off askew, sometimes in two facets
    snapped(bm, (length / 2, 0, 0), (1, rnd.uniform(-0.55, 0.55), rnd.uniform(-0.5, 0.5)))
    if rnd.random() < 0.4:
        snapped(bm, (length / 2 - 0.012, rnd.choice((-1, 1)) * 0.03, 0), (1, rnd.choice((-1, 1)) * rnd.uniform(0.8, 1.6), rnd.uniform(-0.3, 0.3)))
    if way < 0:
        s.place(bm, rot=(0, 0, math.pi))
    return s.place(bm, ((x0 + x1) / 2 - way * 0.025, y, z), (rnd.uniform(-0.02, 0.02) + lean, rnd.uniform(-0.02, 0.02), rnd.uniform(-0.03, 0.03)))


def loose_brick(rnd, loc, rot, half=False):
    """A fallen brick, whole or a broken half."""
    bm = s.rbox(BRICK, (0, 0, 0), r=0.006, seg=1)
    if half:
        snapped(bm, (rnd.uniform(-0.04, 0.05), 0, 0), (1, rnd.uniform(-0.6, 0.6), rnd.uniform(-0.5, 0.5)))
    return s.place(bm, loc, rot)


def chunk(rnd, loc, size):
    """A lump of mortar: a low-poly ball knocked into facets."""
    bm = s.ball(1.0, (0, 0, 0), seg=6, scale=(size, size * rnd.uniform(0.6, 1.0), size * rnd.uniform(0.45, 0.8)))
    for _ in range(3):
        n = (rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.2, 1))
        snapped(bm, tuple(k * size * 0.55 for k in n), n)
    return s.place(bm, loc, (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), rnd.uniform(0, 3.1)))


def flake(rnd, loc):
    """A shard of plaster lying on the floor."""
    w, d = rnd.uniform(0.05, 0.13), rnd.uniform(0.04, 0.09)
    bm = s.rbox((w, d, 0.014), (0, 0, 0), r=0.0, seg=1)
    for _ in range(2):
        a = rnd.uniform(0, 2 * math.pi)
        snapped(bm, (math.cos(a) * w * 0.3, math.sin(a) * d * 0.3, 0), (math.cos(a), math.sin(a), 0))
    return s.place(bm, loc, (rnd.uniform(-0.12, 0.12), rnd.uniform(-0.12, 0.12), rnd.uniform(0, 3.1)))


def mound(rnd, cx, cy, a, b, height, n=9):
    """A heap of grit and dust: a low dome with a rough back, flat on the floor. Returns (shape, height at (x, y))."""
    import bmesh

    def h(x, y):
        q = ((x - cx) / a) ** 2 + ((y - cy) / b) ** 2
        return height * max(0.0, 1 - q) ** 1.5

    bm = bmesh.new()
    rows = []
    p1, p2, p3 = rnd.uniform(0, 6.28), rnd.uniform(0, 6.28), rnd.uniform(0, 6.28)

    def ragged(u, v):
        """The outline is no ellipse: it bulges and runs out in tongues."""
        t = math.atan2(v, u)
        return 1 + 0.22 * math.sin(2 * t + p1) + 0.16 * math.sin(3 * t + p2) + 0.1 * math.sin(7 * t + p3)
    for i in range(n + 1):
        row = []
        for j in range(n + 1):
            u, v = i / n * 2 - 1, j / n * 2 - 1
            r = max(abs(u), abs(v))
            k = (1 / math.hypot(u, v) * r) if r > 1e-6 else 0.0          # the square grid squeezed into the ellipse
            f = ragged(u, v) if r > 1e-6 else 1.0
            x, y = cx + u * k * a * f, cy + v * k * b * f
            edge = i in (0, n) or j in (0, n)
            z = 0.0 if edge else h(cx + u * k * a, cy + v * k * b) * rnd.uniform(0.7, 1.2) + rnd.uniform(0.0, 0.01)
            row.append(bm.verts.new((x, y, z)))
        rows.append(row)
    for i in range(n):
        for j in range(n):
            bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]))
    rim = [rows[0][j] for j in range(n)] + [rows[i][n] for i in range(n)] + [rows[n][j] for j in range(n, 0, -1)] + [rows[i][0] for i in range(n, 0, -1)]
    bm.faces.new(rim)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, h


def opening(width, height, seed, tooth, steps, conduit):
    """A hole knocked through a plastered brick wall, the clear opening width x height (the game's wall pieces keep that
    shape): the two wythes of brick standing toothed out of both jambs — every course ends where it broke, now a
    stretcher's stub, now a snapped half —, the head hanging in steps from the corners (`steps` courses), the bed of
    mortar behind them, heaps of grit with fallen bricks, lumps of mortar and shards of plaster against the jambs on
    both faces, and (`conduit`) the cut ends of a conduit that ran in the wall. Parts: Brick, Mortar, Plaster, Metal.
    The plaster skin on the two faces is not here: the game lays it on in each room's own wall finish. Along x, the
    wall's thickness along y, origin on the floor in the middle."""
    rnd = random.Random(seed)
    T = TRUE_WALL
    bricks, mortar, plaster, metal = [], [], [], []
    wythes = (-(BRICK[1] / 2 + 0.005), BRICK[1] / 2 + 0.005)
    courses = int(height / COURSE)
    # the jambs: toothing, the odd courses half a brick further out
    for way, x_edge in ((1, -width / 2), (-1, width / 2)):
        mortar.append(s.rbox((0.03, T - 0.03, height + 0.02), (x_edge - way * 0.003, 0, height / 2), r=0.0, seg=1))
        for k in range(courses):
            z = k * COURSE + BRICK[2] / 2 + 0.005
            for y in wythes:
                if rnd.random() < 0.07:
                    continue                                    # this one came out whole
                out = rnd.uniform(0.02, tooth * 0.5) if k % 2 == 0 else rnd.uniform(tooth * 0.45, tooth)
                if rnd.random() < 0.18:
                    out = rnd.uniform(0.012, 0.03)              # snapped at the wall's face
                bricks.append(brick(rnd, x_edge - way * 0.03, x_edge + way * out, y, z, way))
                if rnd.random() < 0.3:                           # mortar squeezed out of the bed above it
                    mortar.append(chunk(rnd, (x_edge + way * out * rnd.uniform(0.2, 0.7), y, z + BRICK[2] / 2 + 0.004), rnd.uniform(0.012, 0.022)))
    # the head: bricks left hanging, stepping down towards the corners
    mortar.append(s.rbox((width + 0.04, T - 0.03, 0.03), (0, 0, height + 0.003), r=0.0, seg=1))
    for row in range(steps):
        z = height - row * COURSE - BRICK[2] / 2 - 0.005
        reach = (0.16 + 0.12 * (steps - 1 - row)) * rnd.uniform(0.8, 1.15)       # how far from each corner this course still hangs
        x = -width / 2 + (0.0 if row % 2 else -0.12)
        while x < width / 2:
            x1 = x + BRICK[0] + 0.01
            mid = (x + x1) / 2
            near = min(mid + width / 2, width / 2 - mid)
            if near < reach or (row == 0 and rnd.random() < 0.45):
                for y in wythes:
                    if rnd.random() < 0.15:
                        continue
                    bm = s.rbox((BRICK[0], BRICK[1], BRICK[2]), (0, 0, 0), r=0.0, seg=1)
                    if rnd.random() < 0.5:                       # its underside broken away
                        snapped(bm, (0, 0, -BRICK[2] * rnd.uniform(0.0, 0.3)), (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), -1))
                    lo, hi = max(x, -width / 2 - 0.03), min(x1 - 0.01, width / 2 + 0.03)
                    if hi - lo > 0.06:
                        s.place(bm, scale=((hi - lo) / BRICK[0], 1, 1))
                        bricks.append(s.place(bm, ((lo + hi) / 2, y + rnd.uniform(-0.004, 0.004), z - rnd.uniform(0.001, 0.005)), (rnd.uniform(-0.03, 0.03), rnd.uniform(-0.04, 0.04), rnd.uniform(-0.03, 0.03))))
            x = x1
    # what fell: a heap against each jamb on each face, thinning out along the wall; grit across the threshold
    grit, _ = mound(rnd, 0.0, 0.0, width / 2 + 0.05, T / 2 + 0.12, 0.022, n=7)
    mortar.append(grit)
    for face, x_side in ((-1, -1), (1, 1), (-1, 1), (1, -1)):
        big = face * x_side > 0
        a, b, top = (0.5, 0.36, 0.2) if big else (0.3, 0.24, 0.1)
        if height < 1.6:
            a, b, top = a * 0.8, b * 0.8, top * 0.75
        cx = x_side * (width / 2 + 0.05 - a * 0.35)
        cy = face * (T / 2 + b * 0.75)
        heap, h = mound(rnd, cx, cy, a, b, top)
        mortar.append(heap)
        for _ in range(9 if big else 4):
            q, t = math.sqrt(rnd.random()) * 0.8, rnd.uniform(0, 2 * math.pi)
            x, y = cx + math.cos(t) * q * a, cy + math.sin(t) * q * b
            if abs(y) < T / 2 + 0.07:
                y = face * (T / 2 + 0.07 + rnd.uniform(0, 0.05))
            flat = rnd.random() < 0.6
            rot = (rnd.uniform(-0.25, 0.25), rnd.uniform(-0.3, 0.3), rnd.uniform(0, 3.1)) if flat else (rnd.uniform(0.5, 1.1), rnd.uniform(-0.3, 0.3), rnd.uniform(0, 3.1))
            bricks.append(loose_brick(rnd, (x, y, h(x, y) + (0.022 if flat else 0.04)), rot, half=rnd.random() < 0.5))
        for _ in range(8 if big else 4):
            q, t = math.sqrt(rnd.random()), rnd.uniform(0, 2 * math.pi)
            x, y = cx + math.cos(t) * q * a * 1.05, cy + math.sin(t) * q * b * 1.05
            mortar.append(chunk(rnd, (x, y, h(x, y) + 0.012), rnd.uniform(0.02, 0.045)))
        for _ in range(7 if big else 4):                       # plaster shards, flung furthest
            q, t = 0.5 + rnd.random() * 0.8, rnd.uniform(0, 2 * math.pi)
            x, y = cx + math.cos(t) * q * a, cy + math.sin(t) * q * b
            if abs(y) > T / 2 + 0.03:
                plaster.append(flake(rnd, (x, y, max(0.008, h(x, y) + 0.006))))
    if conduit:
        # a steel conduit ran across here inside the wall: both cut ends bent out of the jambs, wires hanging from one
        z = height * rnd.uniform(0.62, 0.8)
        for way, x_edge in ((1, -width / 2), (-1, width / 2)):
            end = (x_edge + way * rnd.uniform(0.16, 0.26), rnd.uniform(-0.05, 0.05), z - rnd.uniform(0.03, 0.1))
            metal.append(s.tube([(x_edge - way * 0.04, 0, z), (x_edge + way * 0.07, 0, z), end], 0.011, res=1, segments_per=3))
            if way > 0:
                for dy in (-0.006, 0.006):
                    metal.append(s.tube([end, (end[0] + 0.03, end[1] + dy * 4, end[2] - 0.12), (end[0] + 0.02 + dy * 6, end[1] + dy * 8, end[2] - 0.3)], 0.0035, res=1, segments_per=3))
    s.part("Brick", bricks)
    s.part("Mortar", mortar)
    if plaster:
        s.part("Plaster", plaster)
    if metal:
        s.part("Metal", metal)


OPENINGS = [("breach", 1.6, 2.3, 0.12, 4), ("hole", 0.9, 1.25, 0.085, 2)]   # name, clear width (LevelMetrics.ClearWidth), height, how far the teeth stand, head steps
VARIANTS = 3


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
        s.clear_scene()
        derelict_inner_window_frame()
        export(os.path.join(out, "inner_window_frame__derelict.fbx"), False)
        return
    out = os.path.join(MODELS, "Level")
    os.makedirs(out, exist_ok=True)
    for name, width, height, tooth, steps in OPENINGS:
        for k in range(VARIANTS):
            s.clear_scene()
            opening(width, height, 31 + 17 * k + (0 if name == "breach" else 100), tooth, steps, conduit=k != 1)
            export(os.path.join(out, f"{name}_{k}.fbx"), True)
    s.clear_scene()
    inner_window_frame()
    export(os.path.join(out, "inner_window_frame.fbx"), False)


main()
