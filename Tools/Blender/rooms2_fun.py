"""Furniture for the fun rooms, round 2: library (card catalogue, study carrel, lectern, wingback chair, book cart),
lab (fume hood, 3D printer, parts-bin shelf, robot work table, oscilloscope cart), studio (guitar on a stand, synth on
an X-stand, vocal booth, amp cabinet, leather couch) and game room (air hockey, poker table, jukebox, pinball,
gaming corner).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms2_fun.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--focus x,y,z,r] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y, z up, origin on the
floor in the middle of the footprint. Object names are material roles: Body, Accent, Trim, Eye (glows).
Forms come from shapes.py (plus the helpers of rooms_fun / rooms_paper / poi, imported, never changed) and are finished
by shapes.part; export() goes through meshfix (floor=True) like poi.export.
Attached pieces sink E = 5 mm into what carries them: no two faces share a plane (z-fighting), nothing floats.
"""
import math
import os
import random
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --)
import meshfix  # noqa: E402
import poi  # noqa: E402  (ring, sweep_rect, arch_path)
import rooms_fun as rf  # noqa: E402  (rod, link, prism, torus, tray, cut_box)
import rooms_paper as rp  # noqa: E402  (open_book)
import shapes as s  # noqa: E402

E = 0.005  # how deep an attached piece sinks into its carrier


def export(filename):
    """meshfix (furniture stands on the floor), then the FBX into <root>/Props/."""
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=filename)
    path = os.path.join(g.out_dir, "Props", filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def caster(x, y, z_top, r=0.035, out_body=None):
    """A swivel caster under a carrier whose bottom is z_top: plate (sunk E into it), fork, wheel on the floor.
    Returns [plate, fork, wheel]."""
    plate = s.rbox((0.05, 0.05, 0.01), (x, y, z_top - 0.005 + E), r=0)
    h = z_top - 0.01 + 2 * E - r
    fork = s.rbox((0.036, 0.03, h), (x, y + 0.008, r + h / 2), r=0.004, seg=1)
    wheel = s.rcyl(r, 0.024, (x, y + 0.012, r), axis="X", seg=10, r=0.005, bevel_seg=1)
    return [plate, fork, wheel]


def book_line(rng, x0, x1, z, y_spine, depth, max_h, out, facing=-1, gap=(0.0015, 0.004)):
    """Upright books from x0 to at most x1 on a shelf top z (sunk 2 mm), spines at y_spine facing -y (facing -1) or
    +y (facing +1)."""
    x = x0
    while True:
        w = rng.uniform(0.022, 0.045)
        if x + w > x1:
            break
        h = max_h * rng.uniform(0.66, 0.98)
        d = depth * rng.uniform(0.85, 1.0)
        yc = y_spine - facing * d / 2
        out.append(s.rbox((w, d, h + 0.002), (x + w / 2, yc, z - 0.002 + (h + 0.002) / 2), r=0))
        x += w + rng.uniform(*gap)


def arc_band(cx, cy, r0, r1, a0, a1, z0, z1, n=12):
    """A flat curved strip (an annulus sector from angle a0 to a1) standing from z0 to z1: painted arcs, rims."""
    bm = bmesh.new()
    rings = []
    for k in range(n + 1):
        a = a0 + (a1 - a0) * k / n
        c, sn = math.cos(a), math.sin(a)
        rings.append([bm.verts.new((cx + r * c, cy + r * sn, z)) for r, z in ((r0, z0), (r1, z0), (r1, z1), (r0, z1))])
    for A, B in zip(rings, rings[1:]):
        for j in range(4):
            bm.faces.new((A[j], B[j], B[(j + 1) % 4], A[(j + 1) % 4]))
    bm.faces.new(rings[0])
    bm.faces.new(rings[-1][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def window_cut(w, h, depth, loc, r):
    """Cutter for a window through a wall facing -y: w wide, h tall, corners rounded in the wall plane."""
    return s.rbox((w, h, depth), loc, r=r, seg=3, vertical_only=True, rot=(math.pi / 2, 0, 0))


# --------------------------------------------------------------------------- library

def model_card_catalog():
    """Library card catalogue 0.8 x 0.46 x 1.3: a cabinet of 4 x 7 small drawers (brass label frames and cup pulls)
    under an overhanging top, on a stand of tapered legs with an apron."""
    body = [s.rbox((0.8, 0.46, 0.03), (0, 0, 1.285), r=0.008, seg=2),
            s.rbox((0.76, 0.42, 0.9), (0, 0.01, 0.825), r=0.01, seg=2),          # case 0.375..1.275, front -0.2
            s.rbox((0.74, 0.39, 0.085), (0, 0.015, 0.3425), r=0.006, seg=1)]     # apron 0.30..0.385
    body += [s.rbox((0.036, 0.036, 0.31), (x, y, 0.155), r=0.006, seg=1, taper=(1.4, 1.4))
             for x in (-0.33, 0.33) for y in (-0.14, 0.17)]
    acc = []
    cols, rows, gap = 4, 7, 0.008
    w = (0.74 - (cols - 1) * gap) / cols
    h = (0.86 - (rows - 1) * gap) / rows
    yf = -0.2 - 0.009 + E                     # drawer centre: its back sunk E into the case
    for i in range(cols):
        x = -0.37 + w / 2 + i * (w + gap)
        for j in range(rows):
            z = 0.395 + h / 2 + j * (h + gap)
            body.append(s.rbox((w, 0.018, h), (x, yf, z), r=0.004, seg=1))
            face = yf - 0.009
            acc.append(s.rbox((0.062, 0.006, 0.028), (x, face - 0.001, z + 0.024), r=0))
            acc.append(s.rbox((0.05, 0.016, 0.014), (x, face - 0.006, z - 0.02), r=0))
    s.part("Body", body)
    s.part("Accent", acc)
    return export("card_catalog")


def model_study_carrel():
    """Study carrel 1.0 x 0.7: a desk top at 0.76 between two panel ends (floor to 1.3, front edge stepping back
    above the desk), a back panel with a pinboard and a shelf, a task light under the shelf, a power strip."""
    profile = [(-0.35, 0.0), (0.35, 0.0), (0.35, 1.3), (0.02, 1.3), (-0.3, 0.96), (-0.35, 0.9)]
    body = [rf.prism(profile, "X", 0.47, 0.5, bevel=0.008), rf.prism(profile, "X", -0.5, -0.47, bevel=0.008),
            s.rbox((0.95, 0.03, 0.54), (0, 0.33, 1.02), r=0.006, seg=1),          # back panel 0.75..1.29
            s.rbox((0.95, 0.02, 0.45), (0, 0.32, 0.5), r=0.004, seg=1)]           # modesty panel
    s.part("Body", body)
    trim = [s.rbox((0.95, 0.685, 0.03), (0, -0.0025, 0.745), r=0.008, seg=2),    # desk top 0.73..0.76
            s.rbox((0.95, 0.2, 0.025), (0, 0.22, 1.17), r=0.005, seg=1)]          # shelf
    s.part("Trim", trim)
    acc = [s.rbox((0.8, 0.012, 0.28), (0, 0.311, 0.95), r=0.003, seg=1),          # pinboard on the back panel
           s.rbox((0.6, 0.06, 0.03), (0, 0.17, 1.1475), r=0.008, seg=2),          # light housing under the shelf
           s.rbox((0.3, 0.05, 0.03), (0.22, 0.27, 0.772), r=0.006, seg=1)]        # power strip on the desk
    acc += [s.rbox((0.022, 0.006, 0.014), (0.13 + k * 0.05, 0.244, 0.782), r=0) for k in range(4)]
    s.part("Accent", acc)
    s.part("Eye", [s.rbox((0.56, 0.04, 0.01), (0, 0.165, 1.1325), r=0.002, seg=1)])
    return export("study_carrel")


def model_lectern():
    """Reading lectern 0.5 x 0.5 x 1.2: a stepped square base, a tapered column with a capital, a wedge carrying the
    sloped reading desk (20°, a book ledge along the front) and an open book on it."""
    a = math.radians(20)
    R = Matrix.Rotation(a, 3, "X")
    C = Vector((0, 0, 1.112))

    def on_board(p):
        return tuple(C + R @ Vector(p))

    def z_under(y):
        return C.z - 0.0175 / math.cos(a) + math.tan(a) * y

    body = [s.rbox((0.5, 0.5, 0.06), (0, 0, 0.03), r=0.012, seg=2),
            s.rbox((0.36, 0.36, 0.06), (0, 0, 0.085), r=0.01, seg=2),
            s.loft([(0.1, 0.085, 0.085, 4), (0.5, 0.075, 0.075, 4), (0.94, 0.065, 0.065, 4)], seg=16),
            s.rbox((0.22, 0.22, 0.04), (0, 0, 0.95), r=0.008, seg=2),
            rf.prism([(-0.17, 0.955), (0.17, 0.955), (0.17, z_under(0.17) + 0.006), (-0.17, z_under(-0.17) + 0.006)],
                     "X", -0.22, 0.22, bevel=0.008),
            s.rbox((0.5, 0.42, 0.035), tuple(C), r=0.008, seg=2, rot=(a, 0, 0)),
            s.rbox((0.5, 0.025, 0.035), on_board((0, -0.198, 0.0175 + 0.0175 - 0.008)), r=0.006, seg=1,
                   rot=(a, 0, 0))]
    s.part("Body", body)
    cover, p1, p2, spine = rp.open_book((0, 0, 0), 0, w=0.36, d=0.26)
    for b in (cover, p1, p2, spine):
        s.place(b, on_board((0, 0.02, 0.0175 - 0.002)), (a, 0, 0))
    s.part("Accent", [cover, spine])
    s.part("Trim", [p1, p2])
    return export("lectern")


def model_wingback_chair():
    """Wingback reading armchair 0.8 x 0.8 x 1.1: a reclined lofted back with a padded inner cushion, wings sweeping
    forward from the back down to the rolled arms, a thick seat cushion, four short tapered wooden legs."""
    fab = [s.rbox((0.56, 0.66, 0.2), (0, -0.02, 0.27), r=0.02, seg=2),                       # seat frame 0.17..0.37
           s.rbox((0.54, 0.6, 0.12), (0, -0.08, 0.42), r=0.045, seg=3),                      # seat cushion
           s.loft([(0.36, 0.31, 0.085, 4, 0, 0.26), (0.7, 0.31, 0.08, 4, 0, 0.29), (0.98, 0.3, 0.075, 4, 0, 0.315),
                   (1.08, 0.27, 0.07, 4, 0, 0.325)], seg=24, round_caps=0.04),               # back
           s.loft([(0.47, 0.25, 0.06, 3, 0, 0.2), (0.75, 0.25, 0.065, 3, 0, 0.215), (0.97, 0.235, 0.05, 3, 0, 0.255)],
                  seg=20, round_caps=0.04)]                                                  # inner back cushion
    wing = [(0.33, 0.6), (0.05, 0.62), (-0.08, 0.72), (-0.1, 0.85), (-0.06, 0.97), (0.05, 1.04), (0.2, 1.07),
            (0.33, 1.06)]
    for sx in (-1, 1):
        fab.append(s.rbox((0.12, 0.72, 0.44), (sx * 0.325, -0.03, 0.38), r=0.03, seg=2))     # arm panel
        fab.append(s.rcyl(0.07, 0.74, (sx * 0.325, -0.035, 0.6), axis="Y", seg=16, r=0.03))  # rolled arm
        lo, hi = (0.27, 0.36) if sx > 0 else (-0.36, -0.27)
        fab.append(rf.prism(wing, "X", lo, hi, bevel=0.02))
    s.part("Body", fab)
    legs = [s.rbox((0.04, 0.04, 0.19), (x, y, 0.095), r=0.006, seg=1, taper=(1.3, 1.3))
            for x in (-0.3, 0.3) for y in (-0.3, 0.28)]
    s.part("Accent", legs)
    return export("wingback_chair")


def model_book_cart():
    """Library book trolley 0.8 x 0.4 x 1.0: two end panels with a hand-grip slot, three shelves with lips and a
    centre divider, books back to back on each shelf (spines out on both sides), four swivel casters."""
    body = []
    for xs in (-1, 1):
        p = s.rbox((0.03, 0.4, 0.91), (xs * 0.37, 0, 0.545), r=0.008, seg=2)
        body.append(rf.cut_box(p, [s.rbox((0.1, 0.18, 0.045), (xs * 0.37, 0, 0.92), r=0.02, seg=2)], bevel=0.003))
    shelves = (0.13, 0.46, 0.79)
    for z in shelves:
        top = z + 0.011
        body.append(s.rbox((0.725, 0.38, 0.022), (0, 0, z), r=0.004, seg=1))
        body.append(s.rbox((0.725, 0.012, 0.16), (0, 0, top + 0.08 - 0.003), r=0.003, seg=1))
        body += [s.rbox((0.725, 0.012, 0.035), (0, sy * 0.184, top + 0.0175 - 0.003), r=0.003, seg=1)
                 for sy in (-1, 1)]
    acc = []
    for x in (-0.3, 0.3):
        for y in (-0.14, 0.14):
            acc += caster(x, y, 0.119)
    rng = random.Random(5207)
    books = []
    for k, z in enumerate(shelves):
        top = z + 0.011
        max_h = 0.25 if k < 2 else 0.19
        if k < 2:
            book_line(rng, -0.35, 0.35, top, -0.172, 0.16, max_h, books, facing=-1)
            book_line(rng, -0.35, 0.35, top, 0.172, 0.16, max_h, books, facing=1)
        else:  # top shelf half full: a gap on each side
            book_line(rng, -0.35, 0.08, top, -0.172, 0.16, max_h, books, facing=-1)
            book_line(rng, -0.1, 0.35, top, 0.172, 0.16, max_h, books, facing=1)
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Trim", books)
    return export("book_cart")


# --------------------------------------------------------------------------- lab

def model_fume_hood():
    """Lab fume hood 1.5 x 0.8 x 2.2: a base cabinet with three doors, a dark worktop, the hood (side walls, back
    wall with a baffle, a top housing with a grille), a glass sash raised half way with a bar handle, an airfoil
    sill, a round exhaust duct on top and two glass flasks inside."""
    body = [s.rbox((1.44, 0.68, 0.1), (0, 0.05, 0.05), r=0.008, seg=1),
            s.rbox((1.48, 0.75, 0.805), (0, 0.025, 0.4925), r=0.012, seg=2),       # cabinet 0.09..0.895
            s.rbox((1.3, 0.06, 0.86), (0, 0.365, 1.355), r=0.006, seg=1),          # back wall
            s.rbox((1.5, 0.8, 0.3), (0, 0, 1.92), r=0.014, seg=2)]                 # top housing 1.77..2.07
    body += [s.rbox((0.12, 0.74, 0.86), (sx * 0.685, 0.03, 1.355), r=0.012, seg=2) for sx in (-1, 1)]
    doors = [s.rbox((0.47, 0.02, 0.72), (x, -0.355, 0.495), r=0.006, seg=2) for x in (-0.48, 0, 0.48)]
    body += doors
    s.part("Body", body)

    baffle = s.rbox((1.2, 0.02, 0.7), (0, 0.33, 1.33), r=0.004, seg=1)
    for z in (1.2, 1.45):
        s.groove(baffle, "Z", z, 0.03, 0.008)
    acc = [s.rbox((1.5, 0.8, 0.04), (0, 0, 0.91), r=0.008, seg=2), baffle,
           s.rbox((1.26, 0.07, 0.018), (0, -0.36, 0.94), r=0.006, seg=1, rot=(math.radians(-8), 0, 0))]
    s.part("Accent", acc)

    trim = [s.rbox((0.16, 0.02, 0.02), (x, -0.372, 0.8), r=0.006, seg=1) for x in (-0.48, 0, 0.48)]
    trim += [s.rbox((1.28, 0.03, 0.06), (0, -0.35, 1.35), r=0.008, seg=2)]                    # sash bottom rail
    trim += [s.rbox((0.04, 0.03, 0.47), (sx * 0.62, -0.35, 1.585), r=0.006, seg=1) for sx in (-1, 1)]
    trim.append(s.rcyl(0.014, 1.0, (0, -0.395, 1.34), axis="X", seg=10, r=0.004, bevel_seg=1))
    trim += [s.rcyl(0.009, 0.04, (sx * 0.45, -0.375, 1.34), axis="Y", seg=8, r=0.002, bevel_seg=1) for sx in (-1, 1)]
    trim += s.grille((0.42, -0.4, 1.92), 0.4, 0.14, n=4, axis="-Y")
    trim.append(s.rcyl(0.18, 0.03, (0, 0.08, 2.08), seg=20, r=0.006, bevel_seg=1))
    trim.append(s.rcyl(0.16, 0.14, (0, 0.08, 2.135), seg=20, r=0.01, bevel_seg=1))
    s.part("Trim", trim)

    glass = [s.rbox((1.2, 0.008, 0.46), (0, -0.35, 1.6), r=0.003, seg=1),
             s.lathe([(0.0, 0.926), (0.075, 0.926), (0.08, 0.935), (0.024, 1.08), (0.02, 1.13), (0.026, 1.135),
                      (0.0, 1.14)], seg=16, loc=(0.28, 0.08, 0)),                                # Erlenmeyer flask
             s.lathe([(0.0, 0.926), (0.04, 0.926), (0.045, 0.935), (0.045, 1.12), (0.05, 1.13), (0.0, 1.132)],
                     seg=14, loc=(-0.32, 0.12, 0))]                                               # beaker
    s.part("Eye", glass)
    return export("fume_hood")


def model_printer_3d():
    """3D printer on a stand 0.7 x 0.7 x 1.6: a stand with a shelf of filament spools, a cube-frame printer (base with
    a display, four posts, top rails, a gantry with the print head, Z rods and lead screws carrying the bed — a dark
    build plate on its arms — with a print on it), a spool on a side holder feeding a filament tube to the head."""
    body = [s.rbox((0.7, 0.7, 0.04), (0, 0, 0.76), r=0.01, seg=2),
            s.rbox((0.66, 0.66, 0.025), (0, 0, 0.16), r=0.006, seg=1)]
    body += [s.rbox((0.045, 0.045, 0.745), (x, y, 0.3725), r=0.006, seg=1) for x in (-0.31, 0.31) for y in (-0.31, 0.31)]
    s.part("Body", body)

    acc = [s.rbox((0.56, 0.56, 0.1), (0, 0, 0.825), r=0.012, seg=2)]                              # base 0.775..0.875
    acc += [s.rbox((0.04, 0.04, 0.7), (x, y, 1.22), r=0.005, seg=1) for x in (-0.26, 0.26) for y in (-0.26, 0.26)]
    acc += [s.rbox((0.55, 0.03, 0.03), (0, y, 1.55), r=0.004, seg=1) for y in (-0.26, 0.26)]
    acc += [s.rbox((0.03, 0.55, 0.03), (x, 0, 1.54), r=0.004, seg=1) for x in (-0.26, 0.26)]
    acc += [s.rbox((0.04, 0.06, 0.05), (x, 0.02, 1.52), r=0.005, seg=1) for x in (-0.245, 0.245)]  # gantry ends
    acc.append(s.rbox((0.47, 0.025, 0.025), (0, 0.02, 1.51), r=0.004, seg=1))                     # gantry beam
    acc.append(s.rbox((0.07, 0.06, 0.08), (0.05, 0.0, 1.49), r=0.008, seg=2))                     # print head
    acc.append(s.rcyl(0.012, 0.03, (0.05, 0.0, 1.44), seg=10, r=0.002, radius2=0.004, bevel_seg=1))
    acc += [s.rcyl(0.006, 0.68, (x, 0.25, 1.21), seg=8, r=0.0) for x in (-0.12, 0.12)]            # Z rods
    acc.append(s.rbox((0.4, 0.03, 0.06), (0, 0.24, 1.09), r=0.006, seg=1))                        # Z carriage
    acc += [s.rbox((0.045, 0.44, 0.03), (x, 0.03, 1.085), r=0.004, seg=1) for x in (-0.18, 0.18)]  # bed arms
    acc.append(s.rbox((0.4, 0.045, 0.03), (0, -0.17, 1.085), r=0.004, seg=1))                     # …tied at the front
    acc.append(s.rbox((0.46, 0.46, 0.014), (0, 0, 1.101), r=0.004, seg=1))                        # the bed: a dark textured build plate
    acc += [s.rcyl(0.011, 0.66, (x, 0.25, 1.2), seg=10, r=0.0) for x in (-0.19, 0.19)]            # lead screws
    acc += [s.rbox((0.05, 0.05, 0.05), (x, 0.25, 0.9), r=0.006, seg=1) for x in (-0.19, 0.19)]    # their motors on the base
    acc.append(s.rcyl(0.018, 0.02, (0.03, -0.285, 0.825), axis="Y", seg=12, r=0.004, bevel_seg=1))
    acc.append(s.rcyl(0.012, 0.11, (0.31, 0.26, 1.3), axis="X", seg=10, r=0.003, bevel_seg=1))   # spool holder
    acc.append(s.tube([(0.3, 0.22, 1.39), (0.25, 0.15, 1.585), (0.12, 0.05, 1.6), (0.05, 0.0, 1.525)], 0.005,
                      res=1, segments_per=6))
    s.part("Accent", acc)

    spool = [(0.1, -0.035), (0.1, -0.027), (0.08, -0.025), (0.08, 0.025), (0.1, 0.027), (0.1, 0.035)]
    trim = [s.lathe([(r, z + 0.035) for r, z in spool], seg=24, loc=(x, y, 0.1725 - 0.002))
            for x, y in ((-0.15, -0.02), (0.15, 0.06))]
    trim.append(s.lathe(spool, seg=24, axis="X", loc=(0.32, 0.26, 1.3)))
    trim.append(s.lathe([(0.0, 1.104), (0.05, 1.104), (0.06, 1.15), (0.045, 1.2), (0.035, 1.22), (0.0, 1.222)],
                        seg=16, loc=(-0.05, -0.03, 0)))
    s.part("Trim", trim)
    # only the display on the base glows (the bed was in this part too: a sheet of blue light in mid-air)
    s.part("Eye", [s.rbox((0.1, 0.01, 0.05), (-0.12, -0.28, 0.825), r=0.002, seg=1)])
    return export("printer_3d")


def parts_bin(x, w, z0, yf, d, h):
    """An open-fronted stacking bin (hopper): a tall back, a low front with a sloped mouth, hollowed from the top."""
    pts = [(yf, z0), (yf + d, z0), (yf + d, z0 + h), (yf + 0.25 * d, z0 + h), (yf, z0 + 0.55 * h)]
    bm = rf.prism(pts, "X", x - w / 2, x + w / 2, bevel=0.003)
    cut = s.rbox((w - 0.016, d - 0.022, h), (x, yf + d / 2 + 0.003, z0 + 0.04 + h / 2), r=0)
    return rf.cut_box(bm, [cut], bevel=0)


def model_component_shelf():
    """Parts-bin shelf 1.2 x 0.44 x 1.8: a steel shelving unit (four angle posts, six shelves) with rows of red and
    blue hopper bins: four big ones on the two lowest shelves, eight small ones on the next three."""
    body = [s.rbox((0.03, 0.03, 1.8), (x, y, 0.9), r=0.004, seg=1) for x in (-0.585, 0.585) for y in (-0.205, 0.205)]
    shelves = (0.08, 0.42, 0.76, 1.1, 1.44, 1.775)
    body += [s.rbox((1.17, 0.41, 0.03), (0, 0, z), r=0.005, seg=1) for z in shelves]
    import machines as mc
    goods, labels = [], []
    colours = ("binred", "binblue", "binyellow", "binblue", "bingreen", "binred", "bingrey")
    fill = ("chrome", "brass", "black", "copper", "tin", "white2", "rust", "orange")
    n = 0
    for row, z in enumerate(shelves[:5]):
        z0 = z + 0.015 - 0.002
        if row < 2:
            xs, w, d, h = [-0.435 + k * 0.29 for k in range(4)], 0.27, 0.38, 0.24
        else:
            xs, w, d, h = [-0.475 + k * 0.19 for k in range(6)], 0.175, 0.3, 0.17
        yf = -0.195
        for k, x in enumerate(xs):                                       # a shelf of one colour here, mixed there; every bin its parts and its label
            goods.append((parts_bin(x, w, z0, yf, d, h), colours[row] if row % 2 else colours[(row * 3 + k) % 7]))
            if (row * 5 + k * 3) % 7:
                goods.append((s.rbox((w - 0.03, d * 0.62, h * 0.26), (x, yf + d * 0.62, z0 + 0.04 + h * 0.13), r=0.008, seg=1), fill[n % 8]))
            labels.append(mc.Plate(f"binlabel{n % 8}", (x, yf - 0.002, z0 + 0.25 * h), w * 0.62, min(0.05, 0.3 * h), thick=0.004, rest="white2"))
            n += 1
    s.part("Body", body)
    mc.plates("Print", labels, goods)
    return export("component_shelf")


def model_robot_table():
    """Robot work table 1.8 x 0.9, top at 0.9: a heavy table with an apron and a low shelf; on it a half-built robot
    lying on its back (torso with an open chest and a glowing core, head with a visor, one arm on, legs missing with
    loose cables), the other arm lying beside it, a toolbox, a screwdriver and a clamped work lamp over it all."""
    body = [s.rbox((1.8, 0.9, 0.05), (0, 0, 0.875), r=0.01, seg=2),
            s.rbox((1.62, 0.72, 0.08), (0, 0, 0.815), r=0.006, seg=1),
            s.rbox((1.66, 0.76, 0.025), (0, 0, 0.15), r=0.006, seg=1)]
    body += [s.rbox((0.07, 0.07, 0.855), (x, y, 0.4275), r=0.01, seg=1) for x in (-0.83, 0.83) for y in (-0.38, 0.38)]
    s.part("Body", body)

    top = 0.9
    x0 = -0.45   # hips; the torso runs to the shoulders at x0 + 0.5
    torso = s.loft([(0, 0.08, 0.13, 3), (0.12, 0.09, 0.15, 3), (0.32, 0.11, 0.18, 3), (0.5, 0.1, 0.17, 3)],
                   seg=14, round_caps=0.03)
    s.place(torso, (x0, 0, top + 0.11 - E), (0, math.pi / 2, 0))
    torso = rf.cut_box(torso, [s.rbox((0.2, 0.16, 0.1), (x0 + 0.3, 0, top + 0.215 + 0.02), r=0.02, seg=2)],
                       bevel=0.004)
    head = s.rbox((0.2, 0.2, 0.17), (0.22, 0, top + 0.085 - E), r=0.05, seg=2)
    hatch = s.rbox((0.22, 0.18, 0.016), (x0 + 0.3, -0.32, top + 0.008 - 0.002), r=0.006, seg=1, rot=(0, 0, 0.3))
    toolbox = s.rbox((0.32, 0.16, 0.12), (0.6, -0.22, top + 0.06 - 0.002), r=0.01, seg=2)
    s.part("Trim", [torso, head, hatch, toolbox])

    acc = [s.rcyl(0.045, 0.1, (0.085, 0, 1.0), axis="X", seg=10, r=0.006, bevel_seg=1),                     # neck
           s.rbox((0.08, 0.26, 0.14), (x0 - 0.015, 0, 0.97), r=0.015, seg=1)]                   # hip block
    acc += [s.rcyl(0.04, 0.06, (x0 - 0.07, sy * 0.08, 0.97), axis="X", seg=10, r=0.006, bevel_seg=1) for sy in (-1, 1)]
    for sy, end in ((1, (-0.7, 0.16, 0.906)),):
        acc.append(s.tube([(x0 - 0.08, sy * 0.08, 0.97), (x0 - 0.16, sy * 0.1, 0.94), (x0 - 0.22, sy * 0.06, 0.91),
                           end], 0.009, res=1, segments_per=2))
    # chest cavity: a core and wires
    acc.append(s.rbox((0.17, 0.13, 0.03), (x0 + 0.3, 0, top + 0.19), r=0.004, seg=1))
    acc += [s.tube([(x0 + 0.24, dy, top + 0.2), (x0 + 0.3, dy + 0.04, top + 0.225), (x0 + 0.37, dy, top + 0.2)],
                   0.006, res=1, segments_per=2) for dy in (-0.045, 0.03)]
    # attached arm along the body, the loose one beside it
    acc += [s.ball(0.05, (0.0, -0.2, 0.985), seg=8),
            rf.rod((0.0, -0.22, 0.94), (-0.22, -0.25, 0.94), 0.042, seg=10, bevel=0.01),
            rf.rod((-0.22, -0.25, 0.936), (-0.44, -0.27, 0.936), 0.034, seg=10, bevel=0.008),
            s.rbox((0.08, 0.07, 0.05), (-0.47, -0.28, top + 0.025 - 0.003), r=0.015, seg=1)]
    acc += [s.ball(0.048, (-0.3, 0.3, 0.945), seg=8),
            rf.rod((-0.3, 0.3, 0.94), (-0.08, 0.27, 0.94), 0.042, seg=10, bevel=0.01),
            rf.rod((-0.08, 0.27, 0.936), (0.14, 0.325, 0.936), 0.034, seg=10, bevel=0.008),
            s.rbox((0.08, 0.07, 0.05), (0.17, 0.335, top + 0.025 - 0.003), r=0.015, seg=1, rot=(0, 0, 0.25)),
            s.tube([(-0.33, 0.31, 0.94), (-0.4, 0.33, 0.92), (-0.46, 0.3, 0.906), (-0.55, 0.32, 0.906)], 0.007,
                   res=1, segments_per=2)]
    # toolbox handle, screwdriver
    acc += [s.rbox((0.2, 0.025, 0.022), (0.6, -0.22, top + 0.142), r=0.008, seg=1)]
    acc += [s.rbox((0.02, 0.02, 0.04), (0.6 + sx * 0.09, -0.22, top + 0.128), r=0.004, seg=1) for sx in (-1, 1)]
    acc += [rf.rod((0.3, 0.02, 0.908), (0.44, 0.07, 0.908), 0.005, seg=8),
            s.rcyl(0.014, 0.09, (0.26, 0.005, 0.913), axis="X", seg=10, r=0.004, rot=(0, 0, 0.34))]
    # clamp lamp at the back right corner
    acc += [s.rbox((0.08, 0.08, 0.05), (0.75, 0.38, top + 0.02), r=0.008, seg=1),
            rf.rod((0.75, 0.38, 0.93), (0.72, 0.3, 1.16), 0.012, seg=8),
            s.rcyl(0.022, 0.05, (0.72, 0.3, 1.165), axis="X", seg=8, r=0.004, bevel_seg=1),
            rf.rod((0.72, 0.3, 1.165), (0.45, 0.06, 1.175), 0.011, seg=8),
            s.rcyl(0.02, 0.045, (0.45, 0.06, 1.175), axis="X", seg=8, r=0.004, bevel_seg=1),
            s.lathe([(0.0, 1.195), (0.03, 1.195), (0.04, 1.18), (0.1, 1.09), (0.1, 1.08), (0.0, 1.08)], seg=10,
                    loc=(0.43, 0.04, 0))]
    s.part("Accent", acc)
    s.part("Eye", [s.rcyl(0.04, 0.03, (x0 + 0.3, 0, top + 0.21), seg=10, r=0.004, bevel_seg=1),
                   s.rbox((0.06, 0.15, 0.012), (0.24, 0, top + 0.17 - 0.004), r=0.004, seg=1),
                   s.rcyl(0.088, 0.01, (0.43, 0.04, 1.079), seg=12, r=0)])
    return export("robot_table")


def model_scope_cart():
    """Instrument trolley 0.6 x 0.5 x 1.2: four posts on casters, a shelf with a function generator, a top tray with a
    push handle at the back and an oscilloscope (screen, knobs, probe sockets with hanging cables)."""
    body = [s.rbox((0.03, 0.03, 0.85), (x, y, 0.545), r=0.004, seg=1) for x in (-0.27, 0.27) for y in (-0.215, 0.215)]
    body += [s.rbox((0.58, 0.46, 0.025), (0, 0, 0.16), r=0.006, seg=1),
             rf.tray((0.6, 0.48, 0.05), (0, 0, 0.965), wall=0.012)]
    body += [rf.rod((x, 0.233, 0.98), (x, 0.233, 1.085), 0.01, seg=10) for x in (-0.22, 0.22)]
    body.append(s.rcyl(0.013, 0.47, (0, 0.233, 1.08), axis="X", seg=12, r=0.004, bevel_seg=1))
    s.part("Body", body)

    acc = [s.rbox((0.42, 0.28, 0.2), (0, -0.02, 1.05), r=0.015, seg=2),              # scope 0.95..1.15
           s.rbox((0.36, 0.1, 0.16), (0, 0.15, 1.04), r=0.015, seg=2),               # its vented back
           s.rbox((0.25, 0.03, 0.025), (0, -0.02, 1.185), r=0.008, seg=1),
           s.rbox((0.24, 0.008, 0.15), (-0.07, -0.162, 1.06), r=0.003, seg=1),       # screen bezel
           s.rbox((0.36, 0.26, 0.12), (0, -0.03, 0.231), r=0.01, seg=2)]             # function generator
    acc += [s.rbox((0.02, 0.03, 0.04), (sx * 0.11, -0.02, 1.165), r=0.004, seg=1) for sx in (-1, 1)]
    s.part("Accent", acc)

    trim = [s.rcyl(0.014, 0.02, (x, -0.168, z), axis="Y", seg=8, r=0.003, bevel_seg=1)
            for x in (0.07, 0.12, 0.17) for z in (1.1, 1.045)]
    trim += [s.rcyl(0.009, 0.016, (x, -0.166, 0.985), axis="Y", seg=8, r=0.002, bevel_seg=1) for x in (0.08, 0.15)]
    trim += [s.tube([(x, -0.168, 0.985), (x - 0.01, -0.215, 0.975), (x - 0.02, -0.236, 0.9), (x - 0.03, -0.232, 0.78)],
                    0.004, res=1, segments_per=5) for x in (0.08, 0.15)]
    trim += [s.rcyl(0.012, 0.02, (0.13 + k * 0.05, -0.165, 0.215), axis="Y", seg=8, r=0.003, bevel_seg=1)
             for k in range(3)]
    trim += [s.rcyl(0.018, 0.12, (sx * 0.15, 0.233, 1.08), axis="X", seg=10, r=0.004, bevel_seg=1) for sx in (-1, 1)]
    for x in (-0.27, 0.27):
        for y in (-0.215, 0.215):
            trim += caster(x, y, 0.12)
    s.part("Trim", trim)
    s.part("Eye", [s.rbox((0.21, 0.006, 0.12), (-0.07, -0.164, 1.06), r=0.002, seg=1),
                   s.rbox((0.1, 0.008, 0.035), (-0.08, -0.161, 0.25), r=0.002, seg=1)])
    return export("scope_cart")


# --------------------------------------------------------------------------- studio

GUITAR_BODY = [(0, -0.23), (0.09, -0.22), (0.145, -0.18), (0.163, -0.12), (0.158, -0.06), (0.135, -0.01),
               (0.13, 0.04), (0.145, 0.09), (0.15, 0.14), (0.135, 0.19), (0.11, 0.215), (0.085, 0.2), (0.07, 0.15),
               (0.055, 0.11), (0.03, 0.1), (-0.03, 0.1), (-0.055, 0.12), (-0.075, 0.18), (-0.1, 0.235),
               (-0.125, 0.25), (-0.15, 0.22), (-0.155, 0.16), (-0.14, 0.09), (-0.13, 0.03), (-0.14, -0.02),
               (-0.16, -0.08), (-0.163, -0.13), (-0.145, -0.18), (-0.09, -0.22)]
GUARD = [(0.02, -0.17), (0.1, -0.15), (0.12, -0.08), (0.1, 0.0), (0.06, 0.09), (0.035, 0.095), (-0.04, 0.095),
         (-0.08, 0.15), (-0.11, 0.19), (-0.125, 0.15), (-0.115, 0.07), (-0.1, -0.02), (-0.07, -0.1)]


def model_guitar_stand():
    """Electric guitar on a floor stand 0.5 x 0.4 x 1.05: a double-cutaway solid body with a pickguard, pickups,
    bridge and knobs, a bolt-on neck and headstock with tuners, leaning back 15° into a tripod stand (padded cradle,
    a post up to a neck yoke behind the neck)."""
    lean = math.radians(-15)
    body = [rf.prism(GUITAR_BODY, "Y", -0.0225, 0.0225, bevel=0.008)]
    trim = [s.rbox((0.055, 0.025, 0.47), (0, -0.01, 0.325), r=0.006, seg=1, taper=(0.78, 1.0)),
            rf.prism([(-0.024, 0.55), (0.026, 0.55), (0.036, 0.6), (0.03, 0.68), (0.045, 0.73), (0.02, 0.75),
                      (-0.03, 0.7), (-0.032, 0.6)], "Y", -0.013, 0.003, bevel=0.003)]
    acc = [rf.prism(GUARD, "Y", -0.0265, -0.0215, bevel=0.0015)]
    acc += [s.rbox((0.07, 0.008, 0.018), (0.0, -0.028, z), r=0.002, seg=1) for z in (-0.06, 0.0, 0.06)]
    acc += [s.rbox((0.09, 0.01, 0.03), (0.0, -0.026, -0.13), r=0.003, seg=1),
            s.rbox((0.012, 0.012, 0.012), (0.0, -0.026, -0.13), r=0)]
    acc += [s.rcyl(0.011, 0.012, (0.075 + k * 0.012, -0.03, -0.09 - k * 0.04), axis="Y", seg=10, r=0.002,
                   bevel_seg=1) for k in range(3)]
    acc += [s.rcyl(0.005, 0.03, (0.041, -0.005, 0.585 + k * 0.026), axis="X", seg=6, r=0.0) for k in range(6)]
    for b in body + trim + acc:
        s.place(b, (0, -0.07, 0.322), (lean, 0, 0))

    # stand: hub, three legs with rubber feet, a cradle with pads, a post up to the yoke
    hub = (0, 0.0, 0.18)
    stand = [s.ball(0.025, hub, seg=12)]
    feet = [(-0.2, -0.17, 0.012), (0.2, -0.17, 0.012), (0, 0.18, 0.012)]
    for f in feet:
        stand.append(rf.rod(hub, f, 0.011, seg=10))
        stand.append(s.rcyl(0.02, 0.024, f, seg=12, r=0.005))
    for sx in (-1, 1):
        stand.append(rf.rod(hub, (sx * 0.08, -0.13, 0.08), 0.01, seg=10))
        stand.append(s.rbox((0.05, 0.06, 0.03), (sx * 0.08, -0.13, 0.085), r=0.01, seg=2))
        stand.append(s.rbox((0.04, 0.015, 0.05), (sx * 0.08, -0.167, 0.105), r=0.006, seg=1))
    stand.append(rf.rod(hub, (0, 0.095, 0.775), 0.011, seg=10))
    stand.append(s.rbox((0.1, 0.03, 0.025), (0, 0.095, 0.78), r=0.008, seg=1))
    stand += [s.rbox((0.015, 0.06, 0.06), (sx * 0.04, 0.075, 0.805), r=0.005, seg=1) for sx in (-1, 1)]
    s.part("Body", body)
    s.part("Trim", trim)
    s.part("Accent", acc + stand)
    return export("guitar_stand")


WHITE = (0, 2, 4, 5, 7, 9, 11)  # semitones of the white keys in an octave


def model_synth_stand():
    """Synth keyboard on an X-stand 1.3 x 0.5 x 0.95: a 73-key synth (red case, wooden-look end cheeks, white and
    black keys, pitch and mod wheels, a rear panel with knobs, buttons and a screen) on a double-tube X-stand with
    rubber-capped foot bars and padded arms."""
    acc = []
    # stand: two crossing frames, each two tubes, foot bars and arms along y
    for sign, y in ((1, 0.13), (-1, 0.095)):
        x0, x1 = -0.32 * sign, 0.32 * sign
        for yy in (-y, y):
            acc.append(rf.rod((x0, yy, 0.03), (x1, yy, 0.83), 0.014, seg=8))
        acc.append(s.rcyl(0.018, 0.46, (x0, 0, 0.022), axis="Y", seg=10, r=0.004, bevel_seg=1))
        acc += [s.rcyl(0.022, 0.03, (x0, sy * 0.235, 0.022), axis="Y", seg=10, r=0.006, bevel_seg=1) for sy in (-1, 1)]
        acc.append(s.rcyl(0.016, 0.36, (x1, 0, 0.83), axis="Y", seg=10, r=0.004, bevel_seg=1))
        acc += [s.rbox((0.04, 0.06, 0.02), (x1, sy * 0.12, 0.848), r=0.006, seg=1) for sy in (-1, 1)]
    acc.append(s.rcyl(0.025, 0.32, (0, 0, 0.43), axis="Y", seg=10, r=0.006, bevel_seg=1))

    case = s.rbox((1.25, 0.36, 0.07), (0, 0.0, 0.888), r=0.012, seg=2)                 # 0.853..0.923
    rear = s.rbox((1.25, 0.13, 0.035), (0, 0.11, 0.93), r=0.01, seg=2)                 # 0.9125..0.9475
    s.part("Body", [case, rear])
    acc += [s.rbox((0.03, 0.37, 0.1), (sx * 0.635, 0.0, 0.9), r=0.01, seg=1) for sx in (-1, 1)]
    trim = []
    kw, x_left, yk, zk = 0.0235, -0.47, -0.085, 0.923
    for i in range(43):                                                                  # 73 keys: to the panel's end
        x = x_left + kw * (i + 0.5)
        trim.append(s.rbox((kw - 0.0015, 0.15, 0.02), (x, yk, zk + 0.01 - 0.004), r=0))
    for octave in range(7):
        for k, semi in enumerate(WHITE):
            if semi in (4, 11):
                continue  # no black key after E and B
            i = octave * 7 + k
            if i >= 42:
                break
            x = x_left + kw * (i + 1)
            acc.append(s.rbox((0.012, 0.09, 0.012), (x, yk + 0.03, zk + 0.02 + 0.006 - 0.003), r=0))
    for x in (-0.575, -0.525):
        acc.append(s.rcyl(0.032, 0.018, (x, -0.06, zk - 0.004), axis="X", seg=12, r=0.004, bevel_seg=1))
    acc += [s.rcyl(0.012, 0.018, (-0.5 + k * 0.09, 0.1, 0.9525), seg=8, r=0.003, bevel_seg=1) for k in range(6)]
    acc += [s.rbox((0.025, 0.02, 0.008), (0.25 + k * 0.035, 0.08, 0.9485), r=0) for k in range(6)]
    s.part("Accent", acc)
    s.part("Trim", trim)
    s.part("Eye", [s.rbox((0.12, 0.05, 0.008), (0.5, 0.11, 0.948), r=0.002, seg=1)])
    return export("synth_stand")


def model_vocal_booth():
    """Isolation booth 1.2 x 1.2 x 2.2: a panelled hollow shell on a plinth, a door with a rounded window (glass),
    hinges and a lever handle, an ON AIR light above it, a silencer box on the roof, foam inside and a microphone on
    a stand you can see through the window."""
    shell = s.rbox((1.2, 1.17, 2.065), (0, 0.015, 1.0875), r=0.02, seg=2)              # 0.055..2.12, y -0.57..0.6
    for v in (-0.2, 0.2):
        s.groove(shell, "X", v, 0.012, 0.005)
        s.groove(shell, "Y", v + 0.015, 0.012, 0.005)
    hollow = s.rbox((1.04, 1.01, 1.9), (0, 0.015, 1.0875), r=0.01, seg=1)
    wx, wz = -0.12, 1.45
    shell = rf.cut_box(shell, [hollow, window_cut(0.36, 0.5, 0.4, (wx, -0.55, wz), 0.06)], bevel=0.004)
    plinth = s.rbox((1.16, 1.12, 0.06), (0, 0.02, 0.03), r=0.008, seg=1)
    s.part("Body", [shell, plinth])

    door = s.rbox((0.72, 0.035, 1.94), (wx, -0.5875 + E / 2, 1.03), r=0.012, seg=2)
    door = rf.cut_box(door, [window_cut(0.36, 0.5, 0.2, (wx, -0.6, wz), 0.06)], bevel=0.004)
    acc = [door]
    acc += [s.rcyl(0.014, 0.12, (wx - 0.36, -0.59, z), seg=8, r=0.003, bevel_seg=1) for z in (0.35, 1.0, 1.7)]
    acc += [s.rcyl(0.025, 0.02, (0.16, -0.605, 1.0), axis="Y", seg=12, r=0.004, bevel_seg=1),
            s.rbox((0.13, 0.022, 0.022), (0.105, -0.618, 1.0), r=0.008, seg=1)]
    acc += [s.rbox((0.26, 0.035, 0.08), (wx, -0.585, 2.06), r=0.008, seg=1),
            s.rbox((0.5, 0.3, 0.09), (0.2, 0.15, 2.155), r=0.015, seg=2)]
    # mic stand inside, in line with the window
    inside = 0.1375
    acc += [s.rcyl(0.12, 0.02, (wx, 0.05, inside + 0.01 - 0.002), seg=16, r=0.005, bevel_seg=1),
            s.rcyl(0.01, 1.42, (wx, 0.05, inside + 0.72), seg=10, r=0.0),
            s.lathe([(0.0, 1.5), (0.022, 1.5), (0.025, 1.56), (0.03, 1.63), (0.025, 1.67), (0.0, 1.68)], seg=10,
                    loc=(wx, 0.05, 0)),
            poi.ring(0.07, 0.06, 0.008, (wx, -0.06, 1.6), axis="Y", seg=16),
            rf.rod((wx, 0.04, 1.54), (wx, -0.055, 1.54), 0.005, seg=6)]
    s.part("Accent", acc)

    trim = []
    for k in range(3):
        for j in range(3):
            trim.append(s.rbox((0.3, 0.05, 0.3), (-0.34 + k * 0.34, 0.52 - 0.025 + E, 0.75 + j * 0.34), r=0.03, seg=1))
    trim += [s.rbox((0.05, 0.3, 0.3), (sx * (0.52 - 0.025 + E), 0.1, 1.3), r=0.03, seg=1) for sx in (-1, 1)]
    s.part("Trim", trim)
    s.part("Eye", [s.rbox((0.4, 0.008, 0.54), (wx, -0.57, wz), r=0.004, seg=1),
                   s.rbox((0.2, 0.008, 0.05), (wx, -0.6045, 2.06), r=0.002, seg=1)])
    return export("vocal_booth")


def model_amp_cabinet():
    """Guitar combo amp 0.7 x 0.4 x 0.7: a tolex cabinet with metal corner caps, a recessed grille cloth with a logo
    plate, a chrome control strip (knobs, input jack, a red jewel pilot light), a strap handle, rubber feet."""
    cab = s.rbox((0.7, 0.38, 0.66), (0, 0.01, 0.342), r=0.015, seg=2)                 # 0.012..0.672, front -0.18
    cab = rf.cut_box(cab, [s.rbox((0.6, 0.06, 0.4), (0, -0.19, 0.27), r=0.012, seg=2, vertical_only=True)],
                     bevel=0.004)
    body = [cab]
    body += [s.rcyl(0.025, 0.016, (x, y, 0.008), seg=12, r=0.004) for x in (-0.29, 0.29) for y in (-0.13, 0.15)]
    body += [s.rcyl(0.017, 0.026, (-0.19 + k * 0.07, -0.19, 0.565), axis="Y", seg=12, r=0.004, bevel_seg=1)
             for k in range(6)]
    s.part("Body", body)
    acc = [s.rbox((0.6, 0.012, 0.09), (0, -0.182, 0.565), r=0.004, seg=1),
           s.rbox((0.14, 0.008, 0.035), (-0.18, -0.172, 0.43), r=0.003, seg=1),
           s.rcyl(0.011, 0.02, (-0.265, -0.19, 0.565), axis="Y", seg=10, r=0.003, bevel_seg=1),
           s.rbox((0.2, 0.04, 0.022), (0, 0.01, 0.69), r=0.008, seg=2)]
    acc += [s.rbox((0.05, 0.05, 0.025), (sx * 0.12, 0.01, 0.68), r=0.008, seg=1) for sx in (-1, 1)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            for z in (0.012 + 0.0225, 0.672 - 0.0225):
                acc.append(s.rbox((0.055, 0.055, 0.055), (sx * 0.3275, 0.01 + sy * 0.1675, z), r=0.014, seg=2))
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((0.59, 0.02, 0.39), (0, -0.165, 0.27), r=0.004, seg=1)])
    s.part("Eye", [s.ball(0.012, (0.24, -0.19, 0.565), seg=12)])
    return export("amp_cabinet")


def model_studio_couch():
    """Leather studio couch 2.0 x 0.9 x 0.85: a base, thick rounded arms and back, three seat and three back cushions
    (reclined 10°), four tapered metal legs."""
    body = [s.rbox((1.84, 0.82, 0.22), (0, 0.01, 0.2), r=0.03, seg=2),                 # base 0.09..0.31
            s.rbox((1.72, 0.2, 0.55), (0, 0.35, 0.58), r=0.06, seg=3)]                 # back 0.305..0.855
    body += [s.rbox((0.16, 0.9, 0.55), (sx * 0.92, 0, 0.365), r=0.06, seg=3) for sx in (-1, 1)]
    for x in (-0.575, 0, 0.575):
        cush = s.rbox((0.57, 0.66, 0.16), (x, -0.09, 0.38), r=0.05, seg=3)
        s.groove(cush, "Z", 0.38, 0.008, 0.004)
        body.append(cush)
        body.append(s.rbox((0.555, 0.16, 0.4), (x, 0.205, 0.64), r=0.06, seg=3, rot=(math.radians(-10), 0, 0)))
    s.part("Body", body)
    s.part("Accent", [s.rcyl(0.02, 0.1, (x, y, 0.05), seg=12, r=0.004, radius2=0.03, bevel_seg=1)
                      for x in (-0.85, 0.85) for y in (-0.33, 0.35)])
    return export("studio_couch")


# --------------------------------------------------------------------------- game room

def model_air_hockey():
    """Air-hockey table 2.0 x 1.1 x 0.85: a blue cabinet on four legs, black rails with goal slots at the ends, a
    white playfield with glowing markings (centre line, centre circle, goal creases), a score display on the front,
    two mallets and a puck."""
    body = [s.rbox((1.96, 1.06, 0.17), (0, 0, 0.705), r=0.015, seg=2),                 # apron 0.62..0.79
            s.rbox((0.6, 0.4, 0.15), (0, 0, 0.55), r=0.01, seg=1)]                     # blower housing
    s.part("Body", body)
    rails = s.boolean(s.rbox((2.0, 1.1, 0.07), (0, 0, 0.815), r=0.015, seg=2),
                      s.rbox((1.84, 0.94, 0.2), (0, 0, 0.815), r=0.03, seg=2, vertical_only=True), "DIFFERENCE")
    rails = rf.cut_box(rails, [s.rbox((0.2, 0.3, 0.06), (sx * 0.96, 0, 0.81), r=0.01, seg=1) for sx in (-1, 1)],
                       bevel=0.004)
    acc = [rails]
    acc += [s.rbox((0.1, 0.1, 0.63), (x, y, 0.315), r=0.012, seg=2) for x in (-0.85, 0.85) for y in (-0.4, 0.4)]
    acc += [s.rbox((0.14, 0.14, 0.03), (x, y, 0.015), r=0.008, seg=1) for x in (-0.85, 0.85) for y in (-0.4, 0.4)]
    acc.append(s.rbox((0.34, 0.03, 0.1), (0, -0.54, 0.705), r=0.008, seg=1))
    mallet = [(0.045, 0.798), (0.045, 0.812), (0.03, 0.82), (0.016, 0.826), (0.015, 0.85), (0.02, 0.858), (0.0, 0.864)]
    acc += [s.lathe(mallet, seg=16, loc=(x, y, 0)) for x, y in ((-0.62, 0.1), (0.6, -0.12))]
    acc.append(s.rcyl(0.035, 0.008, (0.2, -0.1, 0.802), seg=16, r=0.002))
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((1.86, 0.96, 0.02), (0, 0, 0.79), r=0.004, seg=1)])
    eye = [s.rbox((0.012, 0.92, 0.004), (0, 0, 0.8), r=0),
           poi.ring(0.16, 0.148, 0.004, (0, 0, 0.8), seg=32, r=0.0)]
    eye += [arc_band(-0.92, 0, 0.208, 0.22, -math.pi / 2, math.pi / 2, 0.798, 0.802, n=12),
            arc_band(0.92, 0, 0.208, 0.22, math.pi / 2, 3 * math.pi / 2, 0.798, 0.802, n=12)]
    eye += [s.rbox((0.12, 0.008, 0.05), (sx * 0.08, -0.557, 0.705), r=0.002, seg=1) for sx in (-1, 1)]
    s.part("Eye", eye)
    return export("air_hockey")


def model_poker_table():
    """Round poker table 1.3 x 0.8: green felt inside a padded black rail, an apron, a turned pedestal on a round
    foot, chip stacks in front of the seats, a pot in the middle and a few cards."""
    s.part("Body", [s.rcyl(0.56, 0.03, (0, 0, 0.765), seg=40, r=0.004, bevel_seg=1)])               # felt 0.75..0.78
    acc = [rf.torus(0.585, 0.064, (0, 0, 0.765), seg=40, rseg=8),
           s.rcyl(0.56, 0.08, (0, 0, 0.71), seg=32, r=0.01, bevel_seg=1),
           s.lathe([(0.33, 0.0), (0.33, 0.03), (0.28, 0.05), (0.1, 0.08), (0.08, 0.12), (0.075, 0.6),
                    (0.14, 0.64), (0.22, 0.675), (0.0, 0.676)], seg=24)]
    s.part("Accent", acc)
    rng = random.Random(1707)
    trim = []
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.3
        for j in range(2):
            h = 0.012 + 0.004 * rng.randint(1, 6)
            p = (0.42 * math.cos(a) + 0.045 * j * math.sin(a), 0.42 * math.sin(a) - 0.045 * j * math.cos(a))
            trim.append(s.rcyl(0.02, h, (p[0], p[1], 0.778 + h / 2), seg=10, r=0.0015, bevel_seg=1))
    trim += [s.rcyl(0.02, h, (x, y, 0.778 + h / 2), seg=10, r=0.0015, bevel_seg=1)
             for x, y, h in ((0.03, 0.05, 0.03), (-0.02, 0.0, 0.022), (0.06, -0.03, 0.016))]
    for k, (x, y, yaw) in enumerate(((-0.12, -0.1, 0.2), (-0.06, -0.12, -0.15), (0.1, 0.12, 0.6),
                                     (-0.2, 0.2, 1.1), (0.25, -0.18, -0.4))):
        trim.append(s.rbox((0.064, 0.089, 0.003), (x, y, 0.7795 + 0.001 * k), r=0, rot=(0, 0, yaw)))
    s.part("Trim", trim)
    return export("poker_table")


def model_jukebox():
    """Retro jukebox 0.8 x 0.6 x 1.6: an arched wooden cabinet on a chrome plinth, a glowing arch of light tubes down
    both sides, a chrome edge trim, an arched window over a record, a sloped selector panel with glowing buttons
    and a coin slot, a speaker grille of chrome bars."""
    arc = [(0.4 * math.cos(math.pi * k / 16), 1.2 + 0.4 * math.sin(math.pi * k / 16)) for k in range(17)]
    cab = rf.prism([(-0.4, 0.06), (0.4, 0.06)] + arc, "Y", -0.25, 0.3, bevel=0.012)
    win = [(0.27 * math.cos(math.pi * k / 16), 1.2 + 0.27 * math.sin(math.pi * k / 16)) for k in range(17)]
    win_cut = rf.prism([(-0.27, 0.92), (0.27, 0.92)] + win, "Y", -0.3, -0.19)
    grille_cut = s.rbox((0.5, 0.06, 0.36), (0, -0.25, 0.36), r=0.02, seg=2, vertical_only=True, rot=(0, 0, 0))
    cab = rf.cut_box(cab, [win_cut, grille_cut], bevel=0.004)
    s.part("Body", [cab])

    def arch(radius, y, width, thick, z_foot):
        bm = poi.sweep_rect(poi.arch_path(radius, z_foot, 1.2, seg=16), y, width, thick, r=0.008)
        return s.place(bm, rot=(0, 0, math.pi / 2))

    acc = [s.rbox((0.8, 0.56, 0.08), (0, 0.025, 0.04), r=0.01, seg=2),
           arch(0.385, -0.255, 0.025, 0.022, 0.08),
           rf.prism([(-0.245, 0.6), (-0.29, 0.62), (-0.29, 0.66), (-0.245, 0.8)], "X", -0.29, 0.29, bevel=0.008)]
    acc += [s.rbox((0.022, 0.02, 0.36), (x, -0.235, 0.36), r=0.006, seg=1) for x in (-0.18, -0.12, -0.06, 0, 0.06, 0.12, 0.18)]
    acc.append(s.rbox((0.012, 0.012, 0.045), (0.22, -0.292, 0.64), r=0))
    s.part("Accent", acc)
    glass = rf.prism([(-0.275, 0.915), (0.275, 0.915)] + [(0.275 * math.cos(math.pi * k / 16),
                      1.2 + 0.275 * math.sin(math.pi * k / 16)) for k in range(17)], "Y", -0.252, -0.244)
    s.part("Trim", [glass])
    slope = math.atan2(0.14, 0.045)
    n = Vector((0, -math.sin(slope), math.cos(slope)))
    buttons = []
    for k in range(6):
        t = 0.35 + 0.3 * (k // 3)
        p = Vector((-0.15 + (k % 3) * 0.075, -0.29 + 0.045 * t, 0.66 + 0.14 * t)) + n * 0.002
        buttons.append(s.rbox((0.05, 0.03, 0.008), tuple(p), r=0, rot=(slope, 0, 0)))
    import machine_atlas as at
    import machines as mc

    def tube(n, c, co):                                                  # the light tubes: amber at the foot, violet at the crown
        return mc.uv("tube", 0.5 + co.x * 1.1, 1 - min(1.0, max(0.0, (co.z - 0.1) / 1.5)), at.GLOW)
    cards = mc.Plate("cards", (0, -0.212, 0.975), 0.44, 0.1, math.radians(18), at.GLOW)
    glow = [(arch(0.345, -0.27, 0.05, 0.06, 0.1), tube)] + [(b, lambda n, c, co: mc.uv("button", 0.5, 0.5, at.GLOW)) for b in buttons]
    mc.plates("Glow", [cards], glow)
    disc = s.rcyl(0.16, 0.012, (0, -0.2, 1.2), axis="Y", seg=40, r=0.002, bevel_seg=1)
    hub = s.rcyl(0.02, 0.03, (0, -0.208, 1.2), axis="Y", seg=16, r=0.004, bevel_seg=1)

    def grooves(n, c, co):
        return mc.uv("record", co.x / 0.33 + 0.5, (co.z - 1.2) / 0.33 + 0.5) if n.y < -0.6 else mc.plain("black")
    mc.plates("Print", [], [(disc, grooves), (hub, "chrome"), (s.rbox((0.5, 0.01, 0.48), (0, -0.188, 1.16), r=0.002, seg=1), "red")])
    return export("jukebox")


def model_pinball():
    """Pinball machine 0.76 x 1.4 x 1.9: a red sloped cabinet on four metal legs with brackets and levelers, side
    rails and a lockdown bar around a sunk playfield (pop bumpers, flippers, lane guides), a coin door with glowing
    slots, flipper buttons and a plunger; the backbox at the back with a big screen and speaker grilles."""
    t = math.atan2(0.1, 1.3)

    def z_top(y):
        return 0.9 + (y + 0.7) * math.tan(t)

    cab = rf.prism([(-0.7, 0.6), (0.6, 0.6), (0.6, 1.0), (-0.7, 0.9)], "X", -0.36, 0.36, bevel=0.012)
    pf_y = -0.1
    cut = s.rbox((0.66, 1.04, 0.1), (0, pf_y, z_top(pf_y) - 0.03 / math.cos(t) + 0.05), r=0.01, seg=1, rot=(t, 0, 0))
    cab = rf.cut_box(cab, [cut], bevel=0.004)
    back = s.rbox((0.74, 0.26, 0.92), (0, 0.57, 1.44), r=0.015, seg=2)                 # backbox 0.98..1.9
    s.part("Body", [cab, back])

    nrm = Vector((0, -math.sin(t), math.cos(t)))

    def on_pf(x, y, h):
        return tuple(Vector((x, y, z_top(y) - 0.03 / math.cos(t))) + nrm * h)

    s.part("Trim", [s.rbox((0.65, 1.03, 0.02), on_pf(0, pf_y, 0.005), r=0.003, seg=1, rot=(t, 0, 0))])
    acc = []
    for x, y in ((-0.1, 0.1), (0.1, 0.1), (0.0, 0.25)):
        b = s.lathe([(0.04, 0.0), (0.04, 0.03), (0.046, 0.034), (0.046, 0.044), (0.0, 0.05)], seg=14)
        acc.append(s.place(b, on_pf(x, y, 0.013), (t, 0, 0)))
    for sx in (-1, 1):
        acc.append(s.rbox((0.1, 0.025, 0.02), on_pf(sx * 0.075, -0.48, 0.022), r=0.008, seg=1, rot=(t, 0, -sx * 0.45)))
        acc.append(s.rbox((0.02, 0.3, 0.025), on_pf(sx * 0.2, -0.3, 0.024), r=0.006, seg=1, rot=(t, 0, sx * 0.25)))
        acc.append(s.rbox((0.03, 1.28, 0.025), (sx * 0.34, -0.06, z_top(-0.06) + 0.008), r=0.008, seg=1, rot=(t, 0, 0)))
        acc.append(s.rcyl(0.015, 0.02, (sx * 0.365, -0.6, 0.84), axis="X", seg=12, r=0.004, bevel_seg=1))
    acc.append(s.rbox((0.72, 0.07, 0.03), (0, -0.665, z_top(-0.665) + 0.01), r=0.01, seg=2, rot=(t, 0, 0)))
    for x in (-0.31, 0.31):
        for y in (-0.6, 0.5):
            acc += [s.rbox((0.06, 0.06, 0.62), (x, y, 0.31), r=0.008, seg=1, taper=(1.25, 1.25)),
                    s.rbox((0.09, 0.09, 0.12), (x, y, 0.62), r=0.01, seg=2),
                    s.rcyl(0.025, 0.02, (x, y, 0.01), seg=12, r=0.004)]
    acc += [s.rbox((0.3, 0.014, 0.2), (0, -0.705, 0.74), r=0.006, seg=2),
            s.rcyl(0.008, 0.025, (0.27, -0.71, 0.84), axis="Y", seg=8, r=0.002, bevel_seg=1),
            s.rcyl(0.014, 0.008, (0.27, -0.724, 0.84), axis="Y", seg=12, r=0.003, bevel_seg=1),
            s.rbox((0.66, 0.012, 0.56), (0, 0.436, 1.52), r=0.006, seg=1)]
    acc += [s.rbox((0.18, 0.012, 0.1), (sx * 0.18, 0.438, 1.13), r=0.008, seg=1) for sx in (-1, 1)]
    s.part("Accent", acc)
    s.part("Eye", [s.rbox((0.6, 0.008, 0.5), (0, 0.432, 1.52), r=0.003, seg=1)] +
           [s.rbox((0.03, 0.006, 0.05), (sx * 0.06, -0.713, 0.77), r=0) for sx in (-1, 1)])
    return export("pinball")


def model_gaming_corner():
    """Gaming corner 1.6 x 0.6 x 1.1: a low TV stand (two doors, an open middle cubby with a console and game cases,
    short feet), a 55" TV on a pedestal, a soundbar in front of it and a gamepad on the top."""
    carcass = s.rbox((1.58, 0.57, 0.305), (0, 0.015, 0.1925), r=0.01, seg=2)           # 0.04..0.345
    carcass = rf.cut_box(carcass, [s.rbox((0.5, 0.5, 0.2), (0, -0.07, 0.19), r=0.008, seg=1)], bevel=0.004)
    body = [carcass, s.rbox((1.6, 0.59, 0.035), (0, 0.005, 0.3575), r=0.01, seg=2)]  # top 0.34..0.375
    body += [s.rbox((0.5, 0.02, 0.24), (sx * 0.53, -0.275, 0.19), r=0.006, seg=2) for sx in (-1, 1)]
    s.part("Body", body)
    acc = [s.rbox((0.12, 0.018, 0.016), (sx * 0.53, -0.29, 0.28), r=0.005, seg=1) for sx in (-1, 1)]
    acc += [s.rbox((0.05, 0.05, 0.045), (x, y, 0.0225), r=0.006, seg=1) for x in (-0.74, 0.74) for y in (-0.22, 0.22)]
    acc += [s.rbox((0.38, 0.26, 0.07), (-0.02, -0.03, 0.123), r=0.012, seg=2),
            s.rbox((0.9, 0.08, 0.06), (0, -0.13, 0.4), r=0.02, seg=2),
            s.rbox((0.36, 0.22, 0.015), (0, 0.1, 0.38), r=0.005, seg=1),
            s.rbox((0.07, 0.03, 0.1), (0, 0.135, 0.43), r=0.006, seg=1),
            s.rbox((1.24, 0.045, 0.7), (0, 0.13, 0.75), r=0.01, seg=2),
            s.rbox((0.8, 0.06, 0.4), (0, 0.17, 0.72), r=0.02, seg=2),
            s.rbox((0.15, 0.08, 0.03), (0.5, -0.17, 0.388), r=0.012, seg=2)]
    acc += [s.rbox((0.045, 0.07, 0.03), (0.5 + sx * 0.065, -0.185, 0.388), r=0.012, seg=2, rot=(0, 0, sx * 0.4))
            for sx in (-1, 1)]
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((0.014, 0.135, 0.17), (0.2 - k * 0.018, -0.02, 0.09 + 0.085 - 0.002), r=0.002, seg=1)
                    for k in range(5)])
    s.part("Eye", [s.rbox((1.2, 0.008, 0.66), (0, 0.106, 0.755), r=0.002, seg=1),
                   s.rbox((0.2, 0.006, 0.006), (-0.02, -0.161, 0.14), r=0)])
    return export("gaming_corner")


MODELS = {
    "card_catalog": model_card_catalog,
    "study_carrel": model_study_carrel,
    "lectern": model_lectern,
    "wingback_chair": model_wingback_chair,
    "book_cart": model_book_cart,
    "fume_hood": model_fume_hood,
    "printer_3d": model_printer_3d,
    "component_shelf": model_component_shelf,
    "robot_table": model_robot_table,
    "scope_cart": model_scope_cart,
    "guitar_stand": model_guitar_stand,
    "synth_stand": model_synth_stand,
    "vocal_booth": model_vocal_booth,
    "amp_cabinet": model_amp_cabinet,
    "studio_couch": model_studio_couch,
    "air_hockey": model_air_hockey,
    "poker_table": model_poker_table,
    "jukebox": model_jukebox,
    "pinball": model_pinball,
    "gaming_corner": model_gaming_corner,
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
