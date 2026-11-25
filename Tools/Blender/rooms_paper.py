"""Furniture for the paper rooms of an office: library (bookcase, reading table, globe), copy room (copier, paper
stack, shredder), mail room (pigeonhole cabinet) and the control room (video wall, operator console).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms_paper.py -- Assets/_Game/Art/Models [--only a,b]
      (every model lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), z up, origin on the floor in the middle of
the footprint; the back is at Blender +Y (Unity z min, against the wall). Object names are material roles:
Body, Accent, Trim, Eye (glows). Built with shapes.py and exported like poi.export (meshfix with floor, smooth shading
kept). Random detail (book sizes, letters in cubbies) comes from a fixed seed per model, so the export is stable.
"""
import math
import os
import random
import sys

import bmesh
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir, category())
import keyboard  # noqa: E402
import meshfix  # noqa: E402
import printed  # noqa: E402
import shapes as s  # noqa: E402


def export(filename):
    """poi.export: meshfix (pieces resting on the floor count as supported), then the FBX into <root>/Props/."""
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=filename)
    path = os.path.join(g.out_dir, g.category(filename), filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def box(size, loc, r=0.0, seg=1, rot=(0, 0, 0)):
    """Plain or lightly rounded box (books, letters: many small pieces, so no bevel by default)."""
    return s.rbox(size, loc, r=r, seg=seg, rot=rot)


def place_on(bms, loc=(0, 0, 0), rot=(0, 0, 0)):
    for b in bms:
        s.place(b, loc, rot)
    return bms


def prism(profile, x0, x1, r=0.01, seg=2):
    """A solid of the (y, z) profile polygon extruded from x0 to x1, edges rounded (desk turrets, wedges)."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in profile]
    b = [bm.verts.new((x1, y, z)) for y, z in profile]
    n = len(profile)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bm.faces.new(a[::-1])
    bm.faces.new(b)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.bevel_edges(bm, r, seg, angle=30)
    return bm


def molding(rings, back, seg=28, n=12):
    """A wall-flush molding wrapped round front and sides: rings (z, half_width, front_y); the back stays at `back`."""
    return s.loft([(z, a, (back - f) / 2, n, 0.0, (back + f) / 2) for z, a, f in rings], seg=seg)


def ring(r_out, r_in, depth, loc=(0, 0, 0), axis="Z", seg=32, r=0.003):
    """Flat ring (as poi.ring): a washer along an axis, edges chamfered."""
    bm = bmesh.new()
    loops = []
    for rad, z in ((r_out, -depth / 2), (r_out, depth / 2), (r_in, depth / 2), (r_in, -depth / 2)):
        loops.append([bm.verts.new((rad * math.cos(2 * math.pi * k / seg), rad * math.sin(2 * math.pi * k / seg), z))
                      for k in range(seg)])
    for a, b in zip(loops, loops[1:] + loops[:1]):
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.bevel_edges(bm, min(r, 0.3 * min(depth, r_out - r_in)), 1, angle=50)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


def lean_book(w, d, h, angle, z, y0, x_contact, side):
    """A book leaning on its neighbour: side +1 — it stands right of x_contact, its top falls to -x (and mirrored).
    Its low corner sinks 2 mm into the shelf, its top edge 2 mm into the neighbour."""
    bm = box((w, d, h), (0, 0, 0), rot=(0, -side * angle, 0))
    zs = [v.co.z for v in bm.verts]
    xs = [v.co.x for v in bm.verts]
    dx = (x_contact - 0.002 - min(xs)) if side > 0 else (x_contact + 0.002 - max(xs))
    return s.place(bm, (dx, y0 + d / 2, z - 0.002 - min(zs)))


def book_stack(rng, cx, z, y0, n, out_a, out_t, trim_p=0.3):
    """Books lying flat, each a little turned, each sunk 1.5 mm into the one below. Returns the top."""
    top = z - 0.002
    for _ in range(n):
        w, d, h = rng.uniform(0.19, 0.25), rng.uniform(0.15, 0.2), rng.uniform(0.028, 0.045)
        bm = box((w, d, h), (0, 0, 0), rot=(0, 0, rng.uniform(-0.07, 0.07)))
        s.place(bm, (cx + rng.uniform(-0.01, 0.01), y0 + 0.11, top + h / 2))
        (out_t if rng.random() < trim_p else out_a).append(bm)
        top += h - 0.0015
    return top + 0.0015


def book_row(rng, x0, x1, z, y0, max_h, out_a, out_t, trim_p=0.22):
    """Upright books from x0 to at most x1 standing on z (2 mm sunk), spines near y0. Returns (last book's right edge,
    its height)."""
    x, last_h, edge = x0, max_h * 0.8, x0
    while True:
        w = rng.uniform(0.022, 0.048)
        if x + w > x1:
            break
        h = max_h * rng.uniform(0.62, 0.97)
        d = rng.uniform(0.17, 0.23)
        yf = y0 + rng.uniform(0.0, 0.012)
        bm = box((w, d, h + 0.002), (x + w / 2, yf + d / 2, z - 0.002 + (h + 0.002) / 2))
        (out_t if rng.random() < trim_p else out_a).append(bm)
        edge = x + w
        x = edge + rng.uniform(0.0015, 0.004)
        last_h = h
    return edge, last_h


# --------------------------------------------------------------------------- library

CASE_SHELVES = (0.15, 0.56, 0.97, 1.38, 1.79)  # shelf tops; the books stand on them


def model_tall_bookcase():
    """Library bookcase 1.0 x 0.35 x 2.2: a wall-flush carcass on a molded plinth with a stepped cornice, five
    shelves packed with books of varying heights, a few leaning, a few lying in stacks. Books: Accent (Folders) with
    some contrasting ones (Trim)."""
    back = 0.175
    sides = [s.rbox((0.026, 0.33, 2.0), (x * 0.467, 0.01, 1.09), r=0.006, seg=2) for x in (-1, 1)]
    back_panel = s.rbox((0.93, 0.012, 1.96), (0, 0.164, 1.1), r=0.003, seg=1)
    shelves = [s.rbox((0.926, 0.31, 0.026), (0, 0.005, t - 0.013), r=0.005, seg=1) for t in CASE_SHELVES]
    top_board = s.rbox((0.926, 0.31, 0.03), (0, 0.005, 2.07), r=0.005, seg=1)
    plinth = molding([(0.0, 0.5, -0.175), (0.075, 0.5, -0.175), (0.09, 0.49, -0.165), (0.128, 0.484, -0.16)], back)
    cornice = molding([(2.07, 0.481, -0.153), (2.1, 0.484, -0.158), (2.112, 0.491, -0.166), (2.15, 0.491, -0.166),
                       (2.162, 0.5, -0.175), (2.2, 0.5, -0.175)], back)
    s.groove(cornice, "Z", 2.18, 0.008, 0.003)
    s.part("Body", [*sides, back_panel, *shelves, top_board, plinth, cornice])

    rng = random.Random(4101)
    acc, trim = [], []
    x0, x1, y0 = -0.452, 0.452, -0.142
    for k, z in enumerate(CASE_SHELVES):
        clear = (CASE_SHELVES[k + 1] - 0.026 if k + 1 < len(CASE_SHELVES) else 2.055) - z
        max_h = min(0.33, clear - 0.03)
        if k == 0:    # tall folios, the last one leaning
            right, h = book_row(rng, x0, x1 - 0.13, z, y0, max_h, acc, trim)
            acc.append(lean_book(0.034, 0.22, h * 0.92, math.radians(16), z, y0, right, 1))
        elif k == 1:  # a stack at the left, then a row
            book_stack(rng, x0 + 0.135, z, y0, 5, acc, trim)
            book_row(rng, x0 + 0.29, x1, z, y0, max_h, acc, trim)
        elif k == 2:  # a row with a gap in the middle: one book fallen against the left half
            right, h = book_row(rng, x0, -0.05, z, y0, max_h, acc, trim)
            trim.append(lean_book(0.03, 0.2, h * 0.9, math.radians(22), z, y0, right, 1))
            book_row(rng, 0.1, x1, z, y0, max_h, acc, trim)
        elif k == 3:  # row, a stack at the right end
            book_row(rng, x0, x1 - 0.29, z, y0, max_h, acc, trim)
            book_stack(rng, x1 - 0.135, z, y0, 4, acc, trim)
        else:         # top: the row leans from the right end into a gap at the left
            book_row(rng, x0 + 0.1, x1, z, y0, max_h, acc, trim)
            acc.append(lean_book(0.03, 0.2, max_h * 0.75, math.radians(14), z, y0, x0 + 0.1, -1))
    s.part("Accent", acc)
    s.part("Trim", trim)
    return export("tall_bookcase")


def turned_leg(x, y, z_top):
    """Table leg: a square block under the apron and a turned shaft (bead, vase, ring, tapered foot)."""
    block = s.rbox((0.08, 0.08, 0.2), (x, y, z_top - 0.1), r=0.008, seg=2)
    shaft = s.lathe([(0.026, 0.0), (0.032, 0.015), (0.022, 0.06), (0.02, 0.14), (0.04, 0.32), (0.024, 0.4),
                     (0.032, 0.43), (0.022, 0.46), (0.034, 0.5),
                     (0.034, z_top - 0.19)], seg=8, loc=(x, y, 0))
    return [block, shaft]


def bankers_lamp(x, y, z):
    """Banker's lamp standing on z: brass base and stem with a harp bend, a green glass shade along x.
    Returns (brass parts, glass shade)."""
    base = s.lathe([(0.07, 0.0), (0.072, 0.008), (0.062, 0.02), (0.035, 0.03), (0.014, 0.036), (0.012, 0.04)],
                   seg=12, loc=(x, y, z - 0.002))
    stem = s.rcyl(0.008, 0.22, (x, y + 0.03, z + 0.14), seg=8, r=0.002, bevel_seg=1)
    arm = s.tube([(x, y + 0.03, z + 0.24), (x, y + 0.02, z + 0.28), (x, y - 0.01, z + 0.29)], 0.008, res=1,
                 segments_per=3)
    zs = z + 0.3
    shade = s.rbox((0.27, 0.13, 0.075), (x, y - 0.015, zs), r=0.012, seg=2, taper=(0.96, 0.45))
    caps = [s.rbox((0.012, 0.136, 0.08), (x + d * 0.133, y - 0.015, zs - 0.001), r=0.004, seg=1, taper=(1, 0.45))
            for d in (-1, 1)]
    chain = s.tube([(x + 0.06, y - 0.03, zs - 0.03), (x + 0.06, y - 0.035, zs - 0.09)], 0.003, res=1,
                   smooth=False, segments_per=1)
    bead = s.rcyl(0.007, 0.014, (x + 0.06, y - 0.035, zs - 0.095), seg=6, r=0.002, bevel_seg=1)
    return [base, stem, arm, *caps, chain, bead], [shade]


def open_book(loc, yaw, w=0.34, d=0.24):
    """An open book lying on a table: cover, two page blocks rising to the outer edges, a spine ridge."""
    a = math.radians(5)
    cover = box((w, d, 0.008), (0, 0, 0.004), r=0.002)
    pages = [box((w / 2 - 0.012, d - 0.016, 0.016), (side * (w / 4 - 0.002), 0, 0.012), r=0.002,
                 rot=(0, -side * a, 0)) for side in (-1, 1)]
    spine = box((0.012, d - 0.02, 0.01), (0, 0, 0.011), rot=(0, math.radians(45), 0))
    return place_on([cover, *pages, spine], loc, (0, 0, yaw))


OPEN_BOOKS = (((-0.55, -0.24, 0.758), math.radians(4)), ((0.45, 0.25, 0.758), math.radians(176)))


def model_reading_table():
    """Library reading table 2.0 x 0.9, top at 0.76 with a molded edge, on four turned legs with an H-stretcher;
    two green banker's lamps on the centre line (shades glow), two open books and a closed stack."""
    top = s.rbox((2.0, 0.9, 0.045), (0, 0, 0.7375), r=0.012, seg=2)
    s.groove(top, "Z", 0.732, 0.008, 0.003)
    apron = s.rbox((1.76, 0.68, 0.1), (0, 0, 0.67), r=0.008, seg=1)
    legs = []
    for x in (-1, 1):
        for y in (-1, 1):
            legs += turned_leg(x * 0.86, y * 0.33, 0.72)
    stretchers = [s.rcyl(0.018, 0.68, (x * 0.86, 0, 0.12), axis="Y", seg=8, r=0.003, bevel_seg=1) for x in (-1, 1)]
    stretchers.append(s.rcyl(0.02, 1.72, (0, 0, 0.12), axis="X", seg=8, r=0.003, bevel_seg=1))
    s.part("Body", [top, apron, *legs, *stretchers])

    brass, glass = [], []
    for x in (-0.5, 0.5):
        b, gl = bankers_lamp(x, 0.0, 0.76)
        brass += b
        glass += gl
    s.part("Accent", brass)
    s.part("Eye", glass)

    books = [b for loc, yaw in OPEN_BOOKS for b in open_book(loc, yaw)]
    rng = random.Random(4102)
    stack = []
    book_stack(rng, 0.0, 0.76, -0.2, 3, stack, stack)
    opened = [Matrix.Translation(loc) @ Matrix.Rotation(yaw, 4, "Z") for loc, yaw in OPEN_BOOKS]

    def print_on(n, c, co):                      # the open books' pages, the stack's cloth fronts, paper edges
        if c.z > 0.758 and abs(c.x) > 0.2:
            back = opened[0 if c.x < 0 else 1].inverted()
            p = back @ co
            if p.z > 0.0085 and n.z > 0.8:
                return printed.cell_uv(printed.PAGE_L if p.x < 0 else printed.PAGE_R, abs(p.x) / 0.17 if p.x > 0 else 1 - abs(p.x) / 0.17, p.y / 0.224 + 0.5)
            return printed.cell_uv(printed.PAPER if p.z > 0.0085 else printed.CLOTH, p.x * 2 + 0.5, p.y * 2 + 0.5)
        if n.z > 0.8:
            return printed.cell_uv(printed.BOOK, 0.5 - (co.y + 0.09) / 0.2, 0.5 + co.x / 0.24)
        return printed.cell_uv(printed.CLOTH if n.y < -0.6 else printed.PAPER, co.x + 0.5, co.y + 0.5)
    printed.paint(s.part("Trim", books + stack), print_on)
    return export("reading_table")


def model_globe():
    """Floor globe: a 0.5 m ball on a tilted axis in a brass meridian ring, the ring sitting in a wooden horizon ring
    carried by four curved legs with a cross stretcher and a centre post. Height 1.0."""
    zc, tilt = 0.72, math.radians(23.5)
    horizon = ring(0.3, 0.262, 0.04, (0, 0, zc), seg=32, r=0.006)
    legs, feet = [], []
    for k in range(4):
        a = math.radians(45 + 90 * k)
        c, sn = math.cos(a), math.sin(a)
        pts = [(r * c, r * sn, z) for r, z in ((0.24, 0.03), (0.205, 0.25), (0.235, 0.5), (0.283, zc - 0.005))]
        legs.append(s.tube(pts, 0.027, res=1, radii=[0.7, 1.0, 0.75, 0.95], segments_per=3))
        feet.append(s.rcyl(0.032, 0.035, (0.24 * c, 0.24 * sn, 0.0175), seg=8, r=0.006, bevel_seg=1))
    stretch = [s.rcyl(0.018, 0.44, (0, 0, 0.16), axis="X", seg=8, r=0.003, bevel_seg=1, rot=(0, 0, math.radians(a)))
               for a in (45, -45)]
    hub = s.lathe([(0.04, 0.13), (0.045, 0.15), (0.04, 0.18), (0.022, 0.2)], seg=12)
    post = s.lathe([(0.022, 0.19), (0.016, 0.3), (0.02, 0.38), (0.03, 0.42), (0.034, 0.46)], seg=10)
    s.part("Body", [horizon, *legs, *feet, *stretch, hub, post])

    meridian = ring(0.28, 0.262, 0.016, (0, 0, zc), axis="Y", seg=32, r=0.003)
    d = (math.sin(tilt), 0, math.cos(tilt))
    pins = [s.rcyl(0.007, 0.05, (sg * 0.262 * d[0], 0, zc + sg * 0.262 * d[2]), seg=8, r=0.002, rot=(0, tilt, 0))
            for sg in (-1, 1)]
    knob = s.ball(0.014, (0.29 * d[0], 0, zc + 0.29 * d[2]), seg=8)
    s.part("Accent", [meridian, *pins, knob])

    ball = s.ball(0.25, (0, 0, 0), seg=24)
    s.place(ball, (0, 0, zc), (0, tilt, 0))
    globe = s.part("Trim", [ball])
    north = Vector((math.sin(tilt), 0, math.cos(tilt)))
    east0 = Vector((math.cos(tilt), 0, -math.sin(tilt)))
    side = north.cross(east0)

    def lonlat(p):
        d = (p - Vector((0, 0, zc))).normalized()
        return math.atan2(d.dot(side), d.dot(east0)) / (2 * math.pi) + 0.5, math.asin(max(-1.0, min(1.0, d.dot(north)))) / math.pi + 0.5

    def rule(n, c, co):                                                  # the map (Art/Textures/Covers/globe.png), whole round it
        u, v = lonlat(co)
        uc, _ = lonlat(c)
        if u - uc > 0.5:
            u -= 1.0
        elif uc - u > 0.5:
            u += 1.0
        return u, v
    printed.paint(globe, rule)
    return export("globe")


# --------------------------------------------------------------------------- copy room

def model_copier():
    """Office multifunction copier 0.96 x 0.66 x 1.15: printer cabinet with four paper drawers on a dark plinth,
    an open output bay with a tray, the scanner bed with the dark document feeder lid on top, a tilted touch panel
    at the front and a finisher tower on the right with two output trays sticking out of its side."""
    body = s.rbox((0.68, 0.6, 0.72), (-0.14, 0.03, 0.44), r=0.018, seg=2)
    drawers = [s.rbox((0.64, 0.02, 0.152), (-0.14, -0.271, z), r=0.006, seg=1) for z in (0.175, 0.335, 0.495, 0.655)]
    column = s.rbox((0.68, 0.18, 0.14), (-0.14, 0.24, 0.85), r=0.01, seg=1)
    left_post = s.rbox((0.06, 0.6, 0.13), (-0.45, 0.03, 0.85), r=0.01, seg=1)
    scanner = s.rbox((0.68, 0.6, 0.1), (-0.14, 0.03, 0.95), r=0.014, seg=2)
    s.groove(scanner, "Z", 0.93, 0.008, 0.003)
    finisher = s.rbox((0.24, 0.54, 0.9), (0.305, 0.04, 0.48), r=0.02, seg=2)
    s.groove(finisher, "Z", 0.45, 0.008, 0.003)
    paper = s.rbox((0.3, 0.22, 0.012), (-0.2, -0.04, 0.802), r=0.002, seg=1, rot=(0, math.radians(-6), 0))
    out_paper = s.rbox((0.07, 0.22, 0.008), (0.43, 0.05, 0.783), r=0.002, seg=1, rot=(0, math.radians(-14), 0))
    vent = s.grille((-0.48, 0.1, 0.45), 0.24, 0.14, n=4, proud=0.01, axis="-X")
    s.part("Body", [body, *drawers, column, left_post, scanner, finisher, paper, out_paper, *vent])

    plinth = s.rbox((0.92, 0.56, 0.1), (-0.02, 0.04, 0.05), r=0.012, seg=1)
    handles = [s.rbox((0.22, 0.016, 0.022), (-0.14, -0.283, z + 0.05), r=0.006, seg=1)
               for z in (0.175, 0.335, 0.495, 0.655)]
    bay_tray = s.rbox((0.4, 0.3, 0.012), (-0.17, -0.02, 0.795), r=0.003, seg=1, rot=(0, math.radians(-6), 0))
    lid = s.rbox((0.66, 0.56, 0.05), (-0.14, 0.04, 1.02), r=0.012, seg=2)
    feeder = s.rbox((0.22, 0.52, 0.12), (-0.36, 0.045, 1.07), r=0.04, seg=3)
    feed_tray = s.rbox((0.34, 0.3, 0.02), (-0.08, 0.04, 1.055), r=0.005, seg=1, rot=(0, math.radians(-3), 0))
    trays = []
    for z in (0.58, 0.77):
        trays.append(s.rbox((0.11, 0.36, 0.014), (0.42, 0.04, z), r=0.004, seg=1, rot=(0, math.radians(-14), 0)))
        trays.append(s.rbox((0.012, 0.3, 0.04), (0.468, 0.04, z + 0.025), r=0.004, seg=1))
    panel_rot = (math.radians(28), 0, 0)
    panel = s.rbox((0.3, 0.11, 0.035), (0.02, -0.28, 0.985), r=0.01, seg=2, rot=panel_rot)
    neck = s.rbox((0.12, 0.06, 0.05), (0.02, -0.245, 0.965), r=0.008, seg=1)
    s.part("Accent", [plinth, *handles, bay_tray, lid, feeder, feed_tray, *trays, panel, neck])

    n = (0, -math.sin(panel_rot[0]), math.cos(panel_rot[0]))
    c = (0.02 + n[0] * 0.0175, -0.28 + n[1] * 0.0175, 0.985 + n[2] * 0.0175)
    screen = s.rbox((0.2, 0.08, 0.008), c, r=0.002, seg=1, rot=panel_rot)
    s.part("Eye", [screen])
    import shapebake
    shapebake.detail("copier", [], waves=0.0)                   # (its baked maps: rounded edges, occlusion)
    return export("copier")


def paper_box(cx, cy, z, yaw, size=(0.385, 0.285, 0.21), lid=True):
    """A copier-paper case standing on z (sunk 2 mm): the lid seam near its top (if it has a lid)."""
    w, d, h = size
    bm = s.rbox((w, d, h), (0, 0, 0), r=0.007, seg=1)
    if lid:
        s.groove(bm, "Z", h / 2 - 0.04, 0.006, 0.0025)
    return s.place(bm, (cx, cy, z - 0.002 + h / 2), (0, 0, yaw))


def label(cx, cy, z, yaw, face_y, size=(0.16, 0.09)):
    """A white label on the front (-y side at local face_y) of a case rotated by yaw about (cx, cy)."""
    bm = s.rbox((size[0], 0.004, size[1]), (0, face_y - 0.001, 0), r=0.001, seg=1)
    return s.place(bm, (cx, cy, z), (0, 0, yaw))


def model_paper_stack():
    """A low stack on a wooden pallet 0.8 x 0.6: two layers of paper cases, an opened case full of white reams on
    the second, two cases and a few loose reams on top. Height 0.8."""
    pallet = [s.rbox((0.8, 0.1, 0.02), (0, y, 0.11), r=0.004, seg=1) for y in (-0.25, -0.125, 0.0, 0.125, 0.25)]
    pallet += [s.rbox((0.1, 0.1, 0.082), (x, y, 0.06), r=0.005, seg=1) for x in (-0.35, 0, 0.35) for y in (-0.25, 0, 0.25)]
    pallet += [s.rbox((0.1, 0.6, 0.022), (x, 0, 0.011), r=0.004, seg=1) for x in (-0.35, 0, 0.35)]
    s.part("Trim", pallet)

    rng = random.Random(4105)
    cases, reams, labels = [], [], []
    deck, h = 0.12, 0.21
    for x in (-0.198, 0.198):
        for y in (-0.145, 0.145):
            yaw = rng.uniform(-0.025, 0.025)
            cases.append(paper_box(x, y, deck, yaw))
            if y < 0:
                labels.append(label(x, y, deck + 0.08, yaw, -0.1425))
    z2 = deck + h - 0.003
    # second layer: three cases and one open tray of reams (front right)
    for x, y in ((-0.198, -0.145), (-0.198, 0.145), (0.198, 0.145)):
        yaw = rng.uniform(-0.04, 0.04)
        cases.append(paper_box(x, y, z2, yaw))
        if y < 0:
            labels.append(label(x, y, z2 + 0.08, yaw, -0.1425))
    tray = paper_box(0.198, -0.145, z2, 0.0, size=(0.385, 0.285, 0.15), lid=False)
    tray = s.boolean(tray, s.rbox((0.365, 0.265, 0.2), (0.198, -0.145, z2 + 0.01 + 0.1), r=0.0), "DIFFERENCE")
    s.bevel_edges(tray, 0.002, 1, angle=50)
    cases.append(tray)
    for k in range(4):
        reams.append(s.rbox((0.355, 0.255, 0.05), (0.198 + rng.uniform(-0.003, 0.003), -0.145, z2 + 0.034 + k * 0.0485),
                            r=0.004, seg=1))
    z3 = z2 + h - 0.003
    for x, y, yaw in ((-0.2, 0.14, 0.06), (0.17, 0.15, -0.05)):
        cases.append(paper_box(x, y, z3, yaw))
    # loose reams: three on the front left case, one on top of the right back case
    top = z3 - 0.002
    for k in range(3):
        reams.append(s.rbox((0.297, 0.21, 0.05), (-0.19 + rng.uniform(-0.02, 0.02), -0.15, top + 0.025),
                            r=0.004, seg=1, rot=(0, 0, rng.uniform(-0.25, 0.25))))
        top += 0.0485
    reams.append(s.rbox((0.297, 0.21, 0.05), (0.17, 0.15, z3 + h - 0.002 + 0.023), r=0.004, seg=1, rot=(0, 0, 0.4)))
    s.part("Body", cases)
    s.part("Accent", reams + labels)
    return export("paper_stack")


def model_shredder():
    """Office shredder 0.46 x 0.36 x 0.7: a tapered dark bin with a recessed pull-out door and a metal handle on
    four casters, a rounded head with a brushed metal slot plate on top, a rocker switch and a green LED."""
    bin_ = s.rbox((0.42, 0.32, 0.56), (0, 0, 0.32), r=0.025, seg=2, taper=(1.03, 1.03))
    s.inset_faces(bin_, s.facing((0, -1, 0), min_area=0.1, tol=0.95), thickness=0.03, depth=-0.006)
    head = s.rbox((0.46, 0.36, 0.11), (0, 0, 0.645), r=0.03, seg=3)
    s.groove(head, "Z", 0.615, 0.008, 0.003)
    head = s.boolean(head, s.rbox((0.3, 0.012, 0.08), (0, 0.0, 0.7), r=0.0), "DIFFERENCE")
    s.part("Body", [bin_, head])

    plate = s.rbox((0.34, 0.07, 0.012), (0, 0, 0.702), r=0.004, seg=1)
    plate = s.boolean(plate, s.rbox((0.3, 0.012, 0.08), (0, 0.0, 0.7), r=0.0), "DIFFERENCE")
    s.bevel_edges(plate, 0.0015, 1, angle=50)
    handle = s.rbox((0.18, 0.035, 0.03), (0, -0.168, 0.5), r=0.01, seg=2)
    rocker = s.rbox((0.04, 0.02, 0.026), (0.1, -0.181, 0.645), r=0.006, seg=1)
    casters = []
    for x in (-0.16, 0.16):
        for y in (-0.11, 0.11):
            casters.append(s.rcyl(0.022, 0.018, (x, y, 0.021), axis="X", seg=10, r=0.004, bevel_seg=1))
            casters.append(s.rbox((0.03, 0.03, 0.03), (x, y, 0.035), r=0.006, seg=1))
    s.part("Accent", [plate, handle, rocker, *casters])

    led = s.rcyl(0.008, 0.01, (0.16, -0.18, 0.645), axis="-Y", seg=8, r=0.002, bevel_seg=1)
    s.part("Eye", [led])
    return export("shredder")


# --------------------------------------------------------------------------- mail room

MAIL_COLS, MAIL_ROWS = 6, 5
MAIL_Z0, MAIL_H, MAIL_W = 0.85, 0.95, 1.16
MAIL_WALL, MAIL_DIV = 0.02, 0.015


def cubby_floors():
    """(x centre, floor z, width, height) of every cubby, rows bottom to top."""
    cw = (MAIL_W - 2 * MAIL_WALL - (MAIL_COLS - 1) * MAIL_DIV) / MAIL_COLS
    ch = (MAIL_H - 2 * MAIL_WALL - (MAIL_ROWS - 1) * MAIL_DIV) / MAIL_ROWS
    out = []
    for r in range(MAIL_ROWS):
        for c in range(MAIL_COLS):
            x = -MAIL_W / 2 + MAIL_WALL + cw / 2 + c * (cw + MAIL_DIV)
            z = MAIL_Z0 + MAIL_WALL + r * (ch + MAIL_DIV)
            out.append((x, z, cw, ch))
    return out


def model_mail_shelf():
    """Mail-room cabinet 1.2 x 0.4 x 1.8: a base cupboard with three doors and a counter top, on it a 6 x 5 grid
    of open pigeonholes with bundles of letters (Accent) and parcels (Trim) in some of them."""
    # the base cupboard: open-fronted (sides, dividers, bottom, the back) behind three swinging doors
    base = [s.rbox((0.02, 0.37, 0.76), (x, 0.015, 0.45), r=0.004, seg=1) for x in (-0.58, -0.196, 0.196, 0.58)]
    base += [s.rbox((1.18, 0.37, 0.02), (0, 0.015, 0.08), r=0.004, seg=1),
             s.rbox((1.14, 0.02, 0.74), (0, 0.19, 0.45), r=0.003, seg=1)]
    kick = s.rbox((1.14, 0.34, 0.08), (0, 0.02, 0.04), r=0.006, seg=1)
    counter = s.rbox((1.2, 0.4, 0.035), (0, 0.0, 0.8375), r=0.008, seg=2)
    doors = [s.rbox((0.382, 0.02, 0.68), (x, -0.175, 0.455), r=0.006, seg=1) for x in (-0.392, 0.0, 0.392)]
    for d in doors:
        s.inset_faces(d, s.facing((0, -1, 0), min_area=0.1, tol=0.95), thickness=0.04, depth=-0.004)
    knobs = [s.rcyl(0.012, 0.03, (x, -0.195, 0.72), axis="-Y", seg=10, r=0.004) for x in (-0.225, 0.17, 0.225)]
    # the grid from boards: uprights reach the front (-0.13), shelves stop 3 mm short, so no two fronts share a plane
    cubbies = cubby_floors()
    cw, ch = cubbies[0][2], cubbies[0][3]
    inner = MAIL_W - 2 * MAIL_WALL + 0.012
    zb, zt = MAIL_Z0 + MAIL_WALL / 2, MAIL_Z0 + MAIL_H - MAIL_WALL / 2
    grid = [s.rbox((MAIL_WALL, 0.32, MAIL_H), (x * (MAIL_W - MAIL_WALL) / 2, 0.03, MAIL_Z0 + MAIL_H / 2), r=0.004, seg=1)
            for x in (-1, 1)]
    grid.append(s.rbox((inner, 0.015, MAIL_H - 0.01), (0, 0.1795, MAIL_Z0 + MAIL_H / 2), r=0.002, seg=1))
    for z in [zb, zt] + [cubbies[r * MAIL_COLS][1] - MAIL_DIV / 2 for r in range(1, MAIL_ROWS)]:
        th = MAIL_WALL if z in (zb, zt) else MAIL_DIV
        grid.append(s.rbox((inner, 0.312, th), (0, 0.029, z), r=0.003, seg=1))
    for c in range(MAIL_COLS - 1):
        x = cubbies[c][0] + cw / 2 + MAIL_DIV / 2
        grid.append(s.rbox((MAIL_DIV, 0.315, MAIL_H - 2 * MAIL_WALL + 0.012), (x, 0.0275, MAIL_Z0 + MAIL_H / 2),
                           r=0.003, seg=1))
    cap = s.rbox((1.2, 0.345, 0.03), (0, 0.0275, MAIL_Z0 + MAIL_H - 0.005), r=0.008, seg=2)
    s.part("Body", [*base, kick, counter, *grid, cap])
    for i, (d, k) in enumerate(zip(doors, (knobs[0], knobs[1], knobs[2]))):
        s.part(f"BodyDoor{i}", [d, k])

    rng = random.Random(4106)
    letters, parcels = [], []
    for x, z, cw, ch in cubby_floors():
        roll = rng.random()
        if roll < 0.42:  # a bundle of envelopes, the top ones sticking out a little
            top = z - 0.0015
            for _ in range(rng.randint(2, 5)):
                w, d, t = cw * rng.uniform(0.7, 0.88), rng.uniform(0.17, 0.24), rng.uniform(0.004, 0.012)
                yf = -0.13 - rng.uniform(-0.01, 0.03)
                bm = box((w, d, t), (x + rng.uniform(-0.012, 0.012), yf + d / 2, top + t / 2),
                         rot=(0, 0, rng.uniform(-0.08, 0.08)))
                letters.append(bm)
                top += t - 0.001
        elif roll < 0.62:  # a parcel
            w, d, t = cw * rng.uniform(0.6, 0.85), rng.uniform(0.14, 0.24), ch * rng.uniform(0.45, 0.8)
            parcels.append(s.rbox((w, d, t), (x, -0.1 + d / 2, z - 0.002 + t / 2), r=0.004, seg=1,
                                  rot=(0, 0, rng.uniform(-0.05, 0.05))))
        elif roll < 0.72:  # letters standing, leaning on the side wall
            for k in range(rng.randint(2, 3)):
                bm = box((0.006, 0.2, ch * 0.75), (0, 0, 0), rot=(0, -math.radians(10 + 4 * k), 0))
                zs = [v.co.z for v in bm.verts]
                xs = [v.co.x for v in bm.verts]
                s.place(bm, (x - cw / 2 - 0.002 - min(xs) + k * 0.012, -0.03, z - 0.0015 - min(zs)))
                letters.append(bm)
    s.part("Accent", letters)
    s.part("Trim", parcels)
    return export("mail_shelf")


# --------------------------------------------------------------------------- control room

def model_monitor_wall():
    """Video wall 2.4 wide: 3 x 2 large screens with thin bezels from 1.0 to 2.4 m, hung on two rails across two
    posts that stand on long feet; screens face +z (Blender -y)."""
    body, screens = [], []
    for zc in (1.35, 2.05):
        for xc in (-0.8, 0.0, 0.8):
            bezel = s.rbox((0.795, 0.06, 0.695), (xc, 0.0, zc), r=0.008, seg=2)
            s.inset_faces(bezel, s.facing((0, -1, 0), min_area=0.2, tol=0.95), thickness=0.012, depth=-0.006)
            body.append(bezel)
            screens.append(s.rbox((0.74, 0.008, 0.64), (xc, -0.023, zc), r=0.002, seg=1))
    rails = [s.rbox((2.0, 0.04, 0.08), (0, 0.048, z), r=0.008, seg=1) for z in (1.35, 2.05)]
    posts = [s.rbox((0.08, 0.06, 2.3), (x, 0.09, 1.18), r=0.012, seg=2) for x in (-0.8, 0.8)]
    feet = [s.rbox((0.1, 0.3, 0.05), (x, 0.0, 0.03), r=0.012, seg=2) for x in (-0.8, 0.8)]
    pads = [s.rcyl(0.022, 0.016, (x, y, 0.008), seg=10, r=0.003) for x in (-0.8, 0.8) for y in (-0.12, 0.12)]
    for f in feet:
        s.place(f, (0, 0, 0.007))
    cross = s.rbox((1.56, 0.05, 0.06), (0, 0.09, 0.3), r=0.008, seg=1)
    tray = s.rbox((1.62, 0.06, 0.05), (0, 0.06, 0.92), r=0.006, seg=1)
    cables = [s.tube([(x, 0.035, 1.05), (x + 0.02, 0.06, 0.98), (x + 0.03, 0.06, 0.94)], 0.008, res=1,
                     segments_per=4) for x in (-0.6, 0.15)]
    s.part("Body", [*body, *rails, *posts, *feet, *pads, cross, tray, *cables])
    s.part("Eye", screens)
    return export("monitor_wall")


SLOPE = ((0.03, 0.78), (0.37, 0.94))  # front-low and back-high (y, z) of the control strip


def on_slope(t, x, size, lift=0.0):
    """(loc, rot) for a box of `size` sitting on the slanted control strip at fraction t, sunk 3 mm into it."""
    (y0, z0), (y1, z1) = SLOPE
    a = math.atan2(z1 - z0, y1 - y0)
    ny, nz = -math.sin(a), math.cos(a)
    off = size[2] / 2 - 0.003 + lift
    return (x, y0 + (y1 - y0) * t + ny * off, z0 + (z1 - z0) * t + nz * off), (a, 0, 0)


def monitor(cx, cy, yaw):
    """A monitor on an arm, built round its own centre then turned by yaw: (dark parts, screen)."""
    zc = 1.1
    shell = s.rbox((0.56, 0.04, 0.33), (0, 0, 0), r=0.01, seg=1)
    s.inset_faces(shell, s.facing((0, -1, 0), min_area=0.1, tol=0.95), thickness=0.012, depth=-0.004)
    back = s.rbox((0.3, 0.03, 0.18), (0, 0.03, 0), r=0.01, seg=1)
    arm = s.rbox((0.04, 0.08, 0.04), (0, 0.075, -0.02), r=0.01, seg=1)
    pole = s.rcyl(0.016, 0.2, (0, 0.1, -0.12), seg=8, r=0.003, bevel_seg=1)
    tilt = (math.radians(-6), 0, 0)
    for bm in (shell, back):
        s.place(bm, rot=tilt)
    dark = place_on([shell, back, arm, pole], (cx, cy, zc), (0, 0, yaw))
    screen = s.rbox((0.5, 0.004, 0.27), (0, -0.0165, 0), r=0.0015, seg=1)
    s.place(screen, rot=tilt)
    return dark, s.place(screen, (cx, cy, zc), (0, 0, yaw))


def model_console_desk():
    """Control-room operator console 2.4 x 0.9: a work top at 0.76 for the operator at the front, behind it a
    slanted control strip with button rows and lights, three monitors on arms (the outer ones turned in), two
    keyboards and a telephone; drawer pedestals at both ends and a modesty panel."""
    (y0, z0), (y1, z1) = SLOPE
    work = s.rbox((2.4, 0.5, 0.04), (0, -0.2, 0.74), r=0.016, seg=2)
    turret = prism([(0.02, 0.72), (0.45, 0.72), (0.45, 0.94), (y1, z1), (y0, z0)], -1.19, 1.19, r=0.012, seg=2)
    modesty = s.rbox((2.2, 0.03, 0.72), (0, 0.42, 0.37), r=0.006, seg=1)
    peds = []
    for x in (-1, 1):
        p = s.rbox((0.34, 0.8, 0.71), (x * 1.0, 0.03, 0.37), r=0.012, seg=2)
        for z in (0.27, 0.5):
            s.groove(p, "Z", z, 0.008, 0.003)
        peds.append(p)
        peds.append(s.rbox((0.3, 0.76, 0.03), (x * 1.0, 0.03, 0.015), r=0.006, seg=1))
    s.part("Body", [work, turret, modesty, *peds])

    dark, screens = [], []
    for cx, cy, yaw in ((-0.6, 0.32, math.radians(18)), (0.0, 0.33, 0.0), (0.6, 0.32, math.radians(-18))):
        d, sc = monitor(cx, cy, yaw)
        dark += d
        screens.append(sc)
    for k, x in enumerate((-0.35, 0.35)):
        keyboard.add(f"Keys{k}", (x, -0.2, 0.757), mouse=0.3)
    sub_panels = []
    for x in (-0.7, 0.0, 0.7):
        loc, rot = on_slope(0.45, x, (0.5, 0.26, 0.012))
        sub_panels.append(s.rbox((0.5, 0.26, 0.012), loc, r=0.004, seg=1, rot=rot))
    phone = s.rbox((0.2, 0.2, 0.07), (0.98, -0.16, 0.79), r=0.018, seg=1, taper=(0.9, 0.7))
    handset = [s.rbox((0.16, 0.045, 0.03), (0.98, -0.12, 0.835), r=0.01, seg=1)]
    handset += [s.rbox((0.055, 0.06, 0.045), (0.98 + d * 0.075, -0.12, 0.84), r=0.014, seg=1) for d in (-1, 1)]
    cord = s.tube([(1.055, -0.11, 0.83), (1.1, -0.1, 0.79), (1.07, -0.06, 0.765), (1.05, -0.04, 0.79)], 0.005,
                  res=1, segments_per=4)
    s.part("Accent", [*dark, *sub_panels, phone, *handset, cord])

    lights = []
    for x0 in (-0.7, 0.0, 0.7):
        for row, t in enumerate((0.3, 0.45, 0.6)):
            for k in range(4):
                size = (0.05, 0.03, 0.012) if row else (0.034, 0.02, 0.012)
                loc, rot = on_slope(t, x0 - 0.165 + k * 0.11, size, lift=0.009)
                lights.append(s.rbox(size, loc, r=0.0, rot=rot))
    loc, rot = on_slope(0.06, 0.0, (1.9, 0.02, 0.008))
    lights.append(s.rbox((1.9, 0.02, 0.008), loc, r=0.002, seg=1, rot=rot))
    lights.append(s.rbox((0.07, 0.04, 0.006), (0.98, -0.185, 0.826), r=0.002, seg=1))
    s.part("Eye", screens + lights)
    # the work top at the office desks' height (0.918, generate_models.DESK_TOP_NEW): under it stretches, on it lifts
    top, new = 0.76, 0.918
    import bpy
    bpy.context.view_layer.update()
    for obj in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        mw = obj.matrix_world
        inv = mw.inverted()
        for v in obj.data.vertices:
            w = mw @ v.co
            w.z = w.z * new / top if w.z <= top + 1e-4 else w.z + (new - top)
            v.co = inv @ w
    return export("console_desk")


MODELS = {
    "tall_bookcase": model_tall_bookcase,
    "reading_table": model_reading_table,
    "globe": model_globe,
    "copier": model_copier,
    "paper_stack": model_paper_stack,
    "mail_shelf": model_mail_shelf,
    "shredder": model_shredder,
    "monitor_wall": model_monitor_wall,
    "console_desk": model_console_desk,
}


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    if "--only" in args:
        only = set(args[args.index("--only") + 1].split(","))
    s.clear_scene()
    for name, fn in MODELS.items():
        if only is None or name in only:
            fn()
    print("DONE")
