"""The hospital location: a ward (bed head unit, privacy curtain), the nurse station, the corridors (gurney, medication
cart, linen cart, beam seating, vital-signs monitor, hand sanitizer), the operating room (operating table, twin
surgical lights, anaesthesia workstation, back table with a Mayo stand, scrub sink) and imaging (X-ray room, CT scanner).
Shapes follow real equipment: headwall units with gas outlets, ceiling-hung cubicle tracks, crash-style carts,
pedestal OR tables with arm boards, LED surgical lights on spring arms, column tube stands with a wall bucky.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/hospital.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--floor]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), z up, origin on the floor in the middle of the
footprint. Wall pieces (bed_headwall, scrub_sink, hand_sanitizer) keep their real height, the back face flush with the
footprint's back (Blender +y). Ceiling pieces (bed_curtain, surgical_lights, the X-ray column) reach z 3.02: 2 cm into
the 3.0 m ceiling, so no face shares the ceiling's plane. Object names are material roles: Body (main colour), Accent
(rubber, dark plastic, second colour), Trim (chrome, steel, glass), Eye (glows: screens, lamp faces).
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --)
import keyboard  # noqa: E402
import meshfix  # noqa: E402
import shapes as s  # noqa: E402
from rooms2_work import flat_ring, prism, revolve, rounded_rect, rod  # noqa: E402

E = 0.005
CEIL = 3.02   # ceiling-mounted parts end 2 cm into the 3.0 m ceiling


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

class Parts:
    """Shapes collected by role, merged into one object per role at the end (as in garage.py)."""

    def __init__(self):
        self.roles = {"Body": [], "Accent": [], "Trim": [], "Eye": []}

    def add(self, role, *bms):
        for b in bms:
            if isinstance(b, (list, tuple)):
                self.roles[role].extend(b)
            else:
                self.roles[role].append(b)

    def finish(self):
        for role, bms in self.roles.items():
            if bms:
                s.part(role, bms)


def moved(bms, dx=0.0, dy=0.0, dz=0.0):
    """Moves shapes."""
    for b in bms:
        s.place(b, (dx, dy, dz))
    return bms


def _all(p):
    return [b for bms in p.roles.values() for b in bms]


def recentre(p, x=True, y=True):
    """Moves every shape so the footprint is centred on the origin (x and/or y)."""
    bms = _all(p)
    xs = [v.co.x for b in bms for v in b.verts]
    ys = [v.co.y for b in bms for v in b.verts]
    d = Vector((-(min(xs) + max(xs)) / 2 if x else 0.0, -(min(ys) + max(ys)) / 2 if y else 0.0, 0.0))
    for b in bms:
        bmesh.ops.translate(b, vec=d, verts=b.verts)
    return d


def hcaster(p, x, y, z_top, r=0.05, w=0.03):
    """A hospital swivel caster: the wheel on the floor with its hub, a fork of two plates, the swivel housing and a stem
    up into the furniture at z_top. Wheel Accent, metal Trim."""
    p.add("Accent", s.rcyl(r, w, (x, y, r), axis="X", seg=16, r=min(0.008, r * 0.2), bevel_seg=1))
    p.add("Trim", s.rcyl(r * 0.35, w + 0.012, (x, y, r), axis="X", seg=10, r=0.002, bevel_seg=1))
    z0, z1 = r * 0.5, min(2 * r + 0.025, z_top - 0.015)   # the swivel housing shows under the furniture
    for sx in (-1, 1):
        p.add("Trim", s.rbox((0.008, r * 1.1, z1 - z0), (x + sx * (w / 2 + 0.004), y, (z0 + z1) / 2), r=0.003, seg=1))
    p.add("Trim", s.rcyl(w / 2 + 0.012, 0.022, (x, y, z1 + 0.006), seg=12, r=0.004, bevel_seg=1))
    if z_top > z1 + 0.02:
        p.add("Trim", s.rcyl(0.01, z_top - z1 + 0.005, (x, y, (z1 + z_top) / 2 + 0.005), seg=8, r=0.002, bevel_seg=1))


def polyline(pts):
    """[(Vector, cumulative length)] of a 2D path."""
    out, acc = [], 0.0
    prev = None
    for q in pts:
        v = Vector((q[0], q[1]))
        if prev is not None:
            acc += (v - prev).length
        out.append((v, acc))
        prev = v
    return out


def path_at(path, t):
    """Point and unit tangent at length t along a polyline from polyline()."""
    for (a, ta), (b, tb) in zip(path, path[1:]):
        if t <= tb or (b, tb) == path[-1]:
            f = (t - ta) / max(1e-9, tb - ta)
            d = (b - a).normalized()
            return a.lerp(b, max(0.0, min(1.0, f))), d
    return path[-1][0], (path[-1][0] - path[-2][0]).normalized()


def l_track(x_side, y_head, y_foot, x_end, rc=0.18, n=8):
    """Plan path of an L-shaped cubicle track: along the bed at x_side from the head wall to the foot, round a corner of
    radius rc, across the foot to x_end."""
    pts = [(x_side, y_head), (x_side, y_foot + rc)]
    cx, cy = x_side - rc, y_foot + rc
    for k in range(1, n):
        a = -math.pi / 2 * k / n
        pts.append((cx + rc * math.cos(a), cy + rc * math.sin(a)))
    pts += [(x_side - rc, y_foot), (x_end, y_foot)]
    return pts


def pleated(path, t0, t1, z0, z1, amp0, amp1, period, thick, per=10, rows=5):
    """A hanging curtain: a closed sheet thick wide following path from length t0 to t1, from z0 (hem) to z1, waved
    sideways into vertical pleats; the pleat depth goes from amp0 at the hem to amp1 at the top."""
    cols = max(2, int(round((t1 - t0) / period * per)) + 1)
    bm = bmesh.new()
    fr, bk = [], []
    for i in range(cols):
        t = t0 + (t1 - t0) * i / (cols - 1)
        c, d = path_at(path, t)
        nrm = Vector((d.y, -d.x))
        u = (t - t0) / period
        wave = math.sin(2 * math.pi * u) * (0.75 + 0.25 * math.sin(2 * math.pi * u / 3.7 + 0.5))  # uneven folds
        cf, cb = [], []
        for j in range(rows):
            f = j / (rows - 1)
            z = z0 + (z1 - z0) * f
            q = c + nrm * (wave * (amp0 + (amp1 - amp0) * f))
            a = q - nrm * thick / 2
            b = q + nrm * thick / 2
            cf.append(bm.verts.new((a.x, a.y, z)))
            cb.append(bm.verts.new((b.x, b.y, z)))
        fr.append(cf)
        bk.append(cb)
    for i in range(cols - 1):
        for j in range(rows - 1):
            bm.faces.new((fr[i][j], fr[i + 1][j], fr[i + 1][j + 1], fr[i][j + 1]))
            bm.faces.new((bk[i][j + 1], bk[i + 1][j + 1], bk[i + 1][j], bk[i][j]))
        bm.faces.new((fr[i][0], bk[i][0], bk[i + 1][0], fr[i + 1][0]))
        top = rows - 1
        bm.faces.new((fr[i + 1][top], bk[i + 1][top], bk[i][top], fr[i][top]))
    for i in (0, cols - 1):
        for j in range(rows - 1):
            bm.faces.new((fr[i][j], fr[i][j + 1], bk[i][j + 1], bk[i][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def thick_line(pts, width, y0, y1):
    """A flat strip (waveform trace) through (x, z) points, width wide, from y0 to y1 (a prism of the offset outline)."""
    left, right = [], []
    for k, (x, z) in enumerate(pts):
        a = Vector(pts[max(0, k - 1)])
        b = Vector(pts[min(len(pts) - 1, k + 1)])
        d = (b - a).normalized()
        n = Vector((-d.y, d.x)) * width / 2
        left.append((x + n.x, z + n.y))
        right.append((x - n.x, z - n.y))
    return prism(left + right[::-1], "Y", y0, y1)


def shell_profile(center, t):
    """Closed (u, v) outline of a shell t thick along a centre polyline (a moulded seat seen from the side)."""
    a, b = [], []
    for k, q in enumerate(center):
        p0 = Vector(center[max(0, k - 1)])
        p1 = Vector(center[min(len(center) - 1, k + 1)])
        d = (p1 - p0).normalized()
        n = Vector((-d.y, d.x)) * t / 2
        a.append((q[0] + n.x, q[1] + n.y))
        b.append((q[0] - n.x, q[1] - n.y))
    return a + b[::-1]


def annulus(a, b, n, r_in, layers, seg=48):
    """A thick ring along y (a gantry): a superellipse outline (half sizes a, b, exponent n) round a circular bore r_in;
    layers = [(y, outline scale), ...] front to back. Built directly as quads, no boolean."""
    outline = s._ring(a, b, n, seg)
    bm = bmesh.new()
    loops = [[bm.verts.new((q.x * k, y, q.y * k)) for q in outline] for y, k in layers]
    inner = [[bm.verts.new((q.normalized().x * r_in, y, q.normalized().y * r_in)) for q in outline]
             for y in (layers[0][0], layers[-1][0])]
    for lo, up in zip(loops, loops[1:]):
        for j in range(seg):
            bm.faces.new((lo[j], lo[(j + 1) % seg], up[(j + 1) % seg], up[j]))
    for j in range(seg):
        k = (j + 1) % seg
        bm.faces.new((loops[0][j], inner[0][j], inner[0][k], loops[0][k]))
        bm.faces.new((loops[-1][k], inner[1][k], inner[1][j], loops[-1][j]))
        bm.faces.new((inner[0][k], inner[0][j], inner[1][j], inner[1][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def hollow_box(size, loc, wall, r=0.008):
    """An open-topped box (basket, tray): a rounded box with its inside cut out from the top."""
    outer = s.rbox(size, loc, r=r, seg=2)
    inner = s.rbox((size[0] - 2 * wall, size[1] - 2 * wall, size[2]), (loc[0], loc[1], loc[2] + wall), r=r * 0.6, seg=1)
    bm = s.boolean(outer, inner, "DIFFERENCE")
    s.bevel_edges(bm, min(0.002, wall * 0.3), 1, angle=50)
    return bm


# --------------------------------------------------------------------------- ward

def model_bed_headwall():
    """Bed head unit on the wall at 1.35..1.6: an aluminium rail 1.4 m with end caps and two seams, three gas outlets
    (round collars with chrome valves) and an oxygen flowmeter, a row of sockets, a night-light strip under the rail,
    an articulated reading lamp over the pillow and a nurse-call handset hanging on its cord. The back is flush."""
    p = Parts()
    zc, yb = 1.475, 0.05
    rail = s.rbox((1.4, 0.1, 0.25), (0, 0, zc), r=0.02, seg=2)
    s.groove(rail, "Z", 1.405, 0.008, 0.004)
    s.groove(rail, "Z", 1.545, 0.008, 0.004)
    p.add("Body", rail)
    for sx in (-1, 1):
        p.add("Accent", s.rbox((0.05, 0.11, 0.27), (sx * 0.705, 0.0, zc), r=0.015, seg=2))
    yf = -yb
    for k, x in enumerate((-0.56, -0.44, -0.32)):
        p.add("Accent", s.rcyl(0.032, 0.022, (x, yf - 0.009, 1.475), axis="Y", seg=16, r=0.004, bevel_seg=1))
        p.add("Trim", s.rcyl(0.014, 0.03, (x, yf - 0.022, 1.475), axis="Y", seg=10, r=0.003, bevel_seg=1))
    # the oxygen flowmeter: a chrome stem out of the first outlet, a clear tube with its knob
    p.add("Trim", rod((-0.56, yf - 0.03, 1.475), (-0.56, yf - 0.075, 1.475), 0.01, seg=8),
          s.rcyl(0.022, 0.13, (-0.56, yf - 0.085, 1.43), seg=12, r=0.004, bevel_seg=1))
    p.add("Accent", s.rcyl(0.016, 0.03, (-0.56, yf - 0.085, 1.505), seg=12, r=0.004, bevel_seg=1),
          s.rcyl(0.012, 0.02, (-0.56, yf - 0.085, 1.355), seg=10, r=0.003, bevel_seg=1))
    # sockets: white face plates with dark slots
    for x in (0.02, 0.13, 0.24, 0.35):
        p.add("Trim", s.rbox((0.085, 0.012, 0.085), (x, yf - 0.004, 1.475), r=0.008, seg=1))
        p.add("Accent", s.rbox((0.04, 0.006, 0.012), (x, yf - 0.011, 1.49), r=0.002, seg=1),
              s.rbox((0.012, 0.006, 0.012), (x, yf - 0.011, 1.455), r=0.002, seg=1))
    # night light under the rail
    p.add("Eye", s.rbox((0.6, 0.03, 0.01), (0.1, -0.01, 1.351), r=0.003, seg=1))
    # reading lamp: knuckle on top of the rail, an arm out over the bed, a flat head facing down
    p.add("Trim", s.rcyl(0.022, 0.05, (0.52, -0.01, 1.615), seg=12, r=0.004, bevel_seg=1),
          rod((0.52, -0.01, 1.63), (0.5, -0.26, 1.76), 0.012, seg=8))
    head = s.rbox((0.22, 0.12, 0.05), (0.5, -0.3, 1.76), r=0.018, seg=2, rot=(math.radians(-8), 0, 0))
    p.add("Body", head)
    p.add("Eye", s.rbox((0.18, 0.085, 0.01), (0.5, -0.302, 1.733), r=0.004, seg=1, rot=(math.radians(-8), 0, 0)))
    # nurse call: cord socket, a cord down to the handset with its red call button
    p.add("Accent", s.rcyl(0.018, 0.02, (-0.12, yf - 0.008, 1.42), axis="Y", seg=12, r=0.003, bevel_seg=1),
          s.tube([(-0.12, yf - 0.012, 1.42), (-0.1, yf - 0.07, 1.3), (-0.14, yf - 0.06, 1.1), (-0.15, yf - 0.045, 0.99)],
                 0.0045, smooth=True),
          s.rbox((0.055, 0.032, 0.17), (-0.15, yf - 0.045, 0.905), r=0.014, seg=2, taper=(0.85, 0.9)))
    p.add("Eye", s.rcyl(0.012, 0.008, (-0.15, yf - 0.062, 0.93), axis="Y", seg=10, r=0.002, bevel_seg=1))
    recentre(p)
    p.finish()
    return export("bed_headwall")


def model_bed_curtain():
    """Privacy curtain round a bed: an L-shaped cubicle track (2.3 m along the bed on +x, round the corner, 1.0 m across
    the foot) hanging from the ceiling on two drop rods with ceiling plates; the curtain half drawn — from the head
    wall along the bed and a little way round the corner — pleated, a mesh band at the top, the hem 0.35 m over the
    floor. Origin: the middle of the bed area; the open side is -x."""
    p = Parts()
    zt = 2.57
    plan = l_track(0.6, 1.16, -1.15, -0.4)
    path = polyline(plan)
    total = path[-1][1]
    p.add("Trim", s.tube([(x, y, zt) for x, y in plan], 0.014, smooth=False, res=1))
    for (x, y) in (plan[0], plan[-1]):
        p.add("Trim", s.rcyl(0.02, 0.04, (x, y, zt), seg=10, r=0.004, bevel_seg=1))
    for t in (0.75, total - 0.45):
        c, _ = path_at(path, t)
        p.add("Trim", s.rcyl(0.007, CEIL - zt, (c.x, c.y, (CEIL + zt) / 2), seg=8, r=0.002, bevel_seg=1),
              s.rcyl(0.04, 0.025, (c.x, c.y, CEIL - 0.0125), seg=14, r=0.005, bevel_seg=1))
    t1 = 2.75   # how far the curtain is drawn along the track
    amp, period = 0.035, 0.16
    p.add("Body", pleated(path, 0.01, t1, 0.35, 2.125, amp * 1.25, amp * 0.8, period, 0.012, rows=2))
    p.add("Trim", pleated(path, 0.013, t1 - 0.003, 2.12, zt + 0.004, amp * 0.8, amp * 0.3, period, 0.008, rows=2))
    p.finish()
    return export("bed_curtain")


def model_nurse_station():
    """Nurse station 3.0 x 1.1: the raised front counter with a transaction ledge at 1.1 m over a recessed plinth, a
    glowing accent stripe and a sign plate on its face; behind it the work surface at 0.76 on end panels and a drawer
    pedestal, two monitors and keyboards facing the staff (+y), a phone and a row of binders."""
    p = Parts()
    front = s.rbox((3.0, 0.3, 1.04), (0, -0.35, 0.56), r=0.02, seg=2, vertical_only=True)
    s.groove(front, "X", -0.5, 0.008, 0.004)
    s.groove(front, "X", 0.5, 0.008, 0.004)
    p.add("Body", front)
    p.add("Accent", s.rbox((2.96, 0.27, 0.07), (0, -0.33, 0.035), r=0.01, seg=1),
          s.rbox((3.06, 0.4, 0.035), (0, -0.37, 1.09), r=0.012, seg=2),
          s.rbox((3.0, 0.72, 0.035), (0, 0.14, 0.743), r=0.01, seg=2))
    p.add("Eye", s.rbox((2.94, 0.012, 0.035), (0, -0.503, 0.8), r=0.004, seg=1))
    p.add("Trim", s.rbox((0.62, 0.012, 0.15), (0, -0.505, 0.97), r=0.01, seg=2))
    p.add("Accent", s.rbox((0.5, 0.006, 0.06), (0, -0.512, 0.97), r=0.003, seg=1))
    # end panels and a drawer pedestal under the work surface
    for sx in (-1, 1):
        p.add("Body", s.rbox((0.04, 0.72, 0.73), (sx * 1.48, 0.14, 0.365), r=0.01, seg=2))
    ped = s.rbox((0.42, 0.55, 0.735), (-0.95, 0.2, 0.3675), r=0.012, seg=2)
    p.add("Body", ped)
    for z in (0.2, 0.42, 0.58):
        p.add("Trim", s.rbox((0.16, 0.02, 0.016), (-0.95, 0.483, z), r=0.006, seg=1))
    # monitors facing the staff, keyboards and mice
    for x in (-0.45, 0.55):
        p.add("Accent", s.rbox((0.22, 0.17, 0.012), (x, 0.04, 0.765), r=0.005, seg=1),
              s.rbox((0.045, 0.03, 0.3), (x, 0.025, 0.91), r=0.008, seg=1),
              s.rbox((0.56, 0.035, 0.34), (x, 0.045, 1.12), r=0.01, seg=2),
              s.rbox((0.3, 0.03, 0.2), (x, 0.02, 1.11), r=0.012, seg=2))
        p.add("Eye", s.rbox((0.52, 0.006, 0.3), (x, 0.0625, 1.125), r=0.003, seg=1))
    # the phone
    p.add("Accent", s.rbox((0.19, 0.21, 0.05), (1.1, 0.2, 0.775), r=0.015, seg=2, taper=(1.0, 0.8)),
          s.rbox((0.055, 0.22, 0.045), (1.03, 0.2, 0.815), r=0.018, seg=2))
    p.add("Trim", s.rbox((0.08, 0.06, 0.006), (1.14, 0.17, 0.801), r=0.002, seg=1))
    # binders against the back of the counter
    for k in range(6):
        role = "Body" if k % 2 else "Accent"
        p.add(role, s.rbox((0.055, 0.27, 0.31), (-1.35 + k * 0.06, -0.06, 0.76 + 0.155), r=0.006, seg=1))
    d = recentre(p)
    p.finish()
    for k, x in enumerate((-0.45, 0.55)):                                    # the sitter at +Y
        keyboard.add(f"Keys{k}", (x + d.x, 0.3 + d.y, 0.7605 + d.z), yaw=math.pi, light=True)
    return export("nurse_station")


def model_gurney():
    """Patient stretcher 2.0 x 0.7: four big casters under a base cover, two bellows columns lifting the frame, the
    mattress with the backrest raised and a pillow, side rails half up, push handles at the head (+y) and an IV pole
    with a bag at the head corner (-x)."""
    p = Parts()
    for sx in (-1, 1):
        for sy in (-1, 1):
            hcaster(p, sx * 0.24, sy * 0.8, 0.205, r=0.07, w=0.04)
    p.add("Body", s.rbox((0.6, 1.8, 0.1), (0, 0, 0.25), r=0.03, seg=2))
    for y in (-0.45, 0.45):
        prof = [(0.07, 0.29)]
        for k in range(8):
            prof.append((0.075 if k % 2 == 0 else 0.062, 0.31 + k * 0.03))
        prof.append((0.06, 0.58))
        p.add("Accent", s.lathe([(0.0, 0.29)] + prof + [(0.0, 0.58)], seg=16, loc=(0, y, 0)))
    frame = s.rbox((0.66, 1.98, 0.07), (0, 0, 0.6), r=0.02, seg=2)
    p.add("Body", frame)
    for sx in (-1, 1):
        p.add("Trim", rod((sx * 0.335, -0.99, 0.6), (sx * 0.335, 0.99, 0.6), 0.016, seg=8))
    # mattress: the flat leg part, the raised backrest on its hinge, a pillow
    p.add("Accent", s.rbox((0.62, 1.3, 0.1), (0, -0.33, 0.68), r=0.04, seg=3))
    th = math.radians(28)
    d = Vector((0, math.cos(th), math.sin(th)))
    n = Vector((0, -math.sin(th), math.cos(th)))
    hinge = Vector((0, 0.3, 0.64))
    back = s.rbox((0.62, 0.7, 0.1), tuple(hinge + d * 0.34 + n * 0.04), r=0.04, seg=3, rot=(th, 0, 0))
    p.add("Accent", back)
    p.add("Body", s.rbox((0.6, 0.66, 0.03), tuple(hinge + d * 0.33 - n * 0.02), r=0.01, seg=1, rot=(th, 0, 0)),
          rod((0, 0.4, 0.62), tuple(hinge + d * 0.55 - n * 0.03), 0.018, seg=8))
    p.add("Body", s.rbox((0.46, 0.26, 0.1), tuple(hinge + d * 0.54 + n * 0.12), r=0.045, seg=3, rot=(th, 0, 0)))
    # side rails half up: two posts, a top bar and a mid bar each side
    for sx in (-1, 1):
        x = sx * 0.35
        for y in (-0.25, 0.25):
            p.add("Trim", rod((x, y, 0.6), (x, y, 0.95), 0.014, seg=8))
        p.add("Trim", rod((x, -0.42, 0.95), (x, 0.42, 0.95), 0.016, seg=8), rod((x, -0.4, 0.8), (x, 0.4, 0.8), 0.011, seg=8))
        for y in (-0.42, 0.42):
            p.add("Trim", s.ball(0.02, (x, y, 0.95), seg=10))
    # push handles at the head end
    for sx in (-1, 1):
        p.add("Trim", rod((sx * 0.18, 0.97, 0.6), (sx * 0.18, 1.04, 0.92), 0.015, seg=8))
    p.add("Trim", rod((-0.18, 1.04, 0.92), (0.18, 1.04, 0.92), 0.016, seg=8))
    p.add("Accent", s.rcyl(0.022, 0.24, (0, 1.04, 0.92), axis="X", seg=12, r=0.006, bevel_seg=1))
    # IV pole with a hook cross and a bag
    x, y = -0.3, 0.96
    p.add("Accent", s.rcyl(0.025, 0.06, (x, y, 0.62), seg=12, r=0.005, bevel_seg=1))
    p.add("Trim", rod((x, y, 0.6), (x, y, 1.95), 0.012, seg=8), rod((x - 0.14, y, 1.93), (x + 0.14, y, 1.93), 0.008, seg=6))
    p.add("Trim", s.rbox((0.11, 0.035, 0.19), (x - 0.12, y, 1.78), r=0.017, seg=2), rod((x - 0.12, y, 1.88), (x - 0.12, y, 1.93), 0.004, seg=6),
          s.rcyl(0.012, 0.05, (x - 0.12, y, 1.665), seg=10, r=0.003, bevel_seg=1))
    p.add("Accent", s.tube([(x - 0.12, y, 1.645), (x - 0.15, y - 0.08, 1.2), (x - 0.02, y - 0.2, 0.9), (x + 0.05, y - 0.35, 0.83)],
                           0.003, smooth=True))
    recentre(p)
    p.finish()
    return export("gurney")


def model_medication_cart():
    """Medication / crash cart 0.8 x 0.55 x 1.05: a cabinet of six drawers with long bar pulls and label clips, a
    worktop with a chrome gallery rail, corner bumpers, a push handle, a sharps box on its bracket at the side, a lock,
    four casters."""
    p = Parts()
    for sx in (-1, 1):
        for sy in (-1, 1):
            hcaster(p, sx * 0.29, sy * 0.19, 0.14, r=0.05)
    p.add("Accent", s.rbox((0.76, 0.53, 0.04), (0, 0, 0.15), r=0.015, seg=2))
    cab = s.rbox((0.7, 0.48, 0.82), (0, 0, 0.58), r=0.02, seg=2, vertical_only=True)
    p.add("Body", cab)
    p.add("Accent", s.rbox((0.8, 0.56, 0.05), (0, 0, 0.99), r=0.018, seg=2))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add("Accent", s.rbox((0.05, 0.05, 0.78), (sx * 0.35, sy * 0.24, 0.58), r=0.018, seg=2, vertical_only=True))
    z = 0.19
    for hgt in (0.17, 0.15, 0.12, 0.12, 0.1, 0.1):
        zc = z + hgt / 2
        p.add("Body", s.rbox((0.6, 0.03, hgt - 0.008), (0, -0.25, zc), r=0.008, seg=1))
        p.add("Accent", s.rbox((0.42, 0.024, 0.022), (0, -0.272, zc + hgt * 0.15), r=0.008, seg=1))
        p.add("Trim", s.rbox((0.08, 0.006, 0.03), (-0.2, -0.267, zc - hgt * 0.15), r=0.002, seg=1))
        z += hgt
    p.add("Trim", s.rcyl(0.014, 0.01, (0.24, -0.248, 0.92), axis="Y", seg=10, r=0.002, bevel_seg=1))
    # the gallery rail on the worktop: back and sides
    yr, xr, zr = 0.24, 0.36, 1.045
    for x, y in ((-xr, -yr), (-xr, yr), (xr, yr), (xr, -yr)):
        p.add("Trim", rod((x, y, 1.01), (x, y, zr), 0.009, seg=8))
    for a, b in (((-xr, -yr), (-xr, yr)), ((-xr, yr), (xr, yr)), ((xr, yr), (xr, -yr))):
        p.add("Trim", rod((a[0], a[1], zr), (b[0], b[1], zr), 0.009, seg=8))
    # push handle on the right side, sharps box on the left
    for y in (-0.15, 0.15):
        p.add("Trim", rod((0.34, y, 0.92), (0.45, y, 0.92), 0.012, seg=8))
    p.add("Trim", rod((0.45, -0.16, 0.92), (0.45, 0.16, 0.92), 0.014, seg=8))
    p.add("Trim", s.rbox((0.04, 0.2, 0.03), (-0.37, 0.0, 0.66), r=0.006, seg=1))
    p.add("Accent", s.rbox((0.12, 0.24, 0.26), (-0.42, 0.0, 0.8), r=0.015, seg=2),
          s.rbox((0.13, 0.25, 0.04), (-0.42, 0.0, 0.94), r=0.012, seg=2))
    p.add("Trim", s.rbox((0.06, 0.08, 0.012), (-0.43, 0.04, 0.962), r=0.004, seg=1))
    recentre(p)
    p.finish()
    return export("medication_cart")


def model_linen_cart():
    """Linen hamper 0.7 x 0.55 x 1.0: a chrome frame on four casters, a fabric bag hanging in it with its hem folded
    over the top hoop, a dark lid, a foot pedal at the front and a push handle at the back."""
    p = Parts()
    xs, ys = 0.3, 0.22
    for sx in (-1, 1):
        for sy in (-1, 1):
            hcaster(p, sx * xs, sy * ys, 0.13, r=0.045)
            p.add("Trim", rod((sx * xs, sy * ys, 0.12), (sx * xs, sy * ys, 0.93), 0.012, seg=8))
    for z in (0.14, 0.92):
        for (a, b) in (((-xs, -ys), (xs, -ys)), ((xs, -ys), (xs, ys)), ((xs, ys), (-xs, ys)), ((-xs, ys), (-xs, -ys))):
            p.add("Trim", rod((a[0], a[1], z), (b[0], b[1], z), 0.012, seg=8))
    bag = s.loft([(0.22, 0.2, 0.14, 3), (0.32, 0.27, 0.2, 3), (0.55, 0.305, 0.225, 3.2), (0.8, 0.29, 0.21, 3.4),
                  (0.92, 0.29, 0.21, 3.6)], seg=32, round_caps=0.04)
    s.groove(bag, "Z", 0.45, 0.012, 0.006)
    s.groove(bag, "Z", 0.68, 0.012, 0.006)
    p.add("Body", bag, s.loft([(0.86, 0.315, 0.235, 3.6), (0.945, 0.315, 0.235, 3.6)], seg=32, round_caps=0.01))
    p.add("Accent", s.rbox((0.68, 0.52, 0.03), (0, 0, 0.955), r=0.012, seg=2))
    p.add("Trim", s.rbox((0.16, 0.03, 0.02), (0, -0.2, 0.978), r=0.008, seg=1))
    p.add("Accent", s.rbox((0.16, 0.08, 0.025), (0, -0.28, 0.1), r=0.008, seg=1))
    p.add("Trim", rod((0, -0.25, 0.1), (0, -0.22, 0.14), 0.008, seg=6))
    for sx in (-1, 1):
        p.add("Trim", rod((sx * 0.25, ys, 0.88), (sx * 0.25, ys + 0.08, 1.0), 0.012, seg=8))
    p.add("Trim", rod((-0.25, ys + 0.08, 1.0), (0.25, ys + 0.08, 1.0), 0.013, seg=8))
    recentre(p)
    p.finish()
    return export("linen_cart")


# --------------------------------------------------------------------------- operating room

def model_surgical_table():
    """Operating table: a base plate with locking feet, a lofted pedestal column with a collar, a segmented padded top
    2.0 x 0.55 at 0.85 (head, back, seat, legs) on a frame with chrome side rails, two arm boards out to the sides at the
    shoulders, a control handset hooked on the +x rail with its cord to the column. Head at +y."""
    p = Parts()
    base = s.rbox((0.56, 0.95, 0.1), (0, -0.12, 0.05), r=0.03, seg=2, taper=(0.92, 0.95))
    p.add("Body", base)
    for y in (-0.52, 0.28):
        p.add("Accent", s.rbox((0.5, 0.08, 0.03), (0, y, 0.015), r=0.01, seg=1))
    col = s.loft([(0.09, 0.15, 0.2, 3), (0.5, 0.11, 0.15, 3), (0.66, 0.12, 0.16, 3)], seg=28)
    p.add("Body", moved([col], dy=-0.05))
    p.add("Accent", s.loft([(0.36, 0.125, 0.165, 3), (0.42, 0.125, 0.165, 3)], seg=28, loc=(0, -0.05, 0)))
    p.add("Body", s.rbox((0.4, 0.7, 0.09), (0, -0.05, 0.69), r=0.02, seg=2))
    p.add("Body", s.rbox((0.5, 1.68, 0.04), (0, -0.15, 0.75), r=0.012, seg=2))
    sections = ((-0.99, -0.37), (-0.36, 0.08), (0.09, 0.69))
    for y0, y1 in sections:
        p.add("Accent", s.rbox((0.55, y1 - y0, 0.08), (0, (y0 + y1) / 2, 0.81), r=0.03, seg=3))
    p.add("Body", s.rbox((0.3, 0.16, 0.04), (0, 0.72, 0.765), r=0.01, seg=1))
    p.add("Accent", s.rbox((0.3, 0.28, 0.07), (0, 0.86, 0.815), r=0.03, seg=3))
    for sx in (-1, 1):
        p.add("Trim", s.rbox((0.012, 1.55, 0.03), (sx * 0.282, -0.15, 0.755), r=0.004, seg=1))
        for y in (-0.85, -0.1, 0.55):
            p.add("Trim", s.rbox((0.03, 0.03, 0.03), (sx * 0.265, y, 0.755), r=0.005, seg=1))
    # arm boards at the shoulders
    for sx in (-1, 1):
        p.add("Trim", s.rbox((0.06, 0.08, 0.05), (sx * 0.31, 0.45, 0.76), r=0.008, seg=1),
              s.rbox((0.55, 0.06, 0.02), (sx * 0.58, 0.45, 0.78), r=0.006, seg=1))
        p.add("Accent", s.rbox((0.58, 0.16, 0.05), (sx * 0.6, 0.45, 0.815), r=0.022, seg=2))
    # handset hooked on the +x rail
    p.add("Trim", s.rbox((0.012, 0.03, 0.05), (0.296, -0.25, 0.74), r=0.003, seg=1))
    p.add("Accent", s.rbox((0.03, 0.075, 0.17), (0.31, -0.25, 0.64), r=0.012, seg=2, taper=(0.9, 0.85)),
          s.tube([(0.31, -0.25, 0.556), (0.33, -0.2, 0.4), (0.2, -0.1, 0.3), (0.12, -0.05, 0.32)], 0.005, smooth=True))
    p.add("Eye", s.rbox((0.006, 0.05, 0.05), (0.326, -0.25, 0.68), r=0.002, seg=1))
    recentre(p)
    p.finish()
    return export("surgical_table")


def lamp_head(p, loc, tilt, az):
    """A round LED surgical light head 0.6 m: a white dome, a chrome bezel, the glowing face down, a ring of
    dividers and the sterile handle in the middle; tilted by `tilt` about the axis at azimuth az."""
    parts = {"Body": [s.lathe([(0.0, 0.0), (0.3, 0.0), (0.3, 0.035), (0.27, 0.1), (0.15, 0.15), (0.0, 0.16)], seg=32)],
             "Trim": [revolve(rounded_rect(0.255, 0.305, -0.012, 0.02, 0.006, n=1), seg=32),
                      revolve(rounded_rect(0.13, 0.145, -0.01, 0.005, 0.003, n=1), seg=24),
                      s.rcyl(0.035, 0.05, (0, 0, 0.175), seg=12, r=0.006, bevel_seg=1)],
             "Eye": [s.rcyl(0.258, 0.012, (0, 0, -0.002), seg=32, r=0.002, bevel_seg=1)],
             "Accent": [s.rcyl(0.028, 0.12, (0, 0, -0.06), seg=12, r=0.008, bevel_seg=1),
                        s.rcyl(0.045, 0.012, (0, 0, -0.006), seg=14, r=0.003, bevel_seg=1)]}
    for role, bms in parts.items():
        for b in bms:
            s.place(b, rot=(tilt, 0, 0))
            s.place(b, rot=(0, 0, az))
            s.place(b, loc)
            p.add(role, b)


def model_surgical_lights():
    """Twin ceiling surgical lights: a canopy on the ceiling, the central axis with two hub joints, two articulated arms
    (a horizontal arm to an elbow joint, a spring arm sloping down to a drop stem) and two round lamp heads 0.6 m facing
    down at 2.0 and 2.1 m, each slightly tilted. Nothing below 1.86 m. Origin under the ceiling mount."""
    p = Parts()
    p.add("Body", s.lathe([(0.0, 2.93), (0.2, 2.93), (0.23, 2.97), (0.23, CEIL), (0.0, CEIL)], seg=32),
          s.rcyl(0.06, 0.4, (0, 0, 2.75), seg=16, r=0.01, bevel_seg=1))
    p.add("Accent", s.rcyl(0.085, 0.07, (0, 0, 2.86), seg=20, r=0.012, bevel_seg=2),
          s.rcyl(0.085, 0.07, (0, 0, 2.72), seg=20, r=0.012, bevel_seg=2))
    for hub_z, az, lamp_z, tilt in ((2.86, math.radians(205), 2.0, math.radians(12)), (2.72, math.radians(20), 2.1, math.radians(-15))):
        da = Vector((math.cos(az - 0.6), math.sin(az - 0.6), 0))
        elbow = da * 0.55 + Vector((0, 0, hub_z))
        lamp = Vector((math.cos(az) * 0.78, math.sin(az) * 0.78, lamp_z))
        drop = Vector((lamp.x, lamp.y, hub_z - 0.32))
        p.add("Body", rod(tuple(da * 0.05 + Vector((0, 0, hub_z))), tuple(elbow), 0.04, seg=12),
              rod(tuple(elbow - Vector((0, 0, 0.02))), tuple(drop), 0.035, seg=12))
        p.add("Accent", s.rcyl(0.06, 0.08, tuple(elbow), seg=16, r=0.01, bevel_seg=1),
              s.rcyl(0.05, 0.07, tuple(drop), seg=14, r=0.01, bevel_seg=1))
        p.add("Body", rod(tuple(drop), (lamp.x, lamp.y, lamp_z + 0.2), 0.025, seg=10))
        p.add("Accent", s.ball(0.04, (lamp.x, lamp.y, lamp_z + 0.2), seg=12))
        lamp_head(p, tuple(lamp), tilt, az + math.pi / 2)
    p.finish()
    return export("surgical_lights")


def model_anesthesia_machine():
    """Anaesthesia workstation 0.8 x 0.7 x 1.4 (monitor to 1.8): a drawer cabinet on casters, a pull-out writing shelf,
    the upper section with a flowmeter bank, two vaporizers and an absorber canister, the breathing bag on its arm and
    corrugated hoses looping to a Y-piece, two gas cylinders strapped at the back, side handles and a monitor on top."""
    p = Parts()
    for sx in (-1, 1):
        for sy in (-1, 1):
            hcaster(p, sx * 0.33, sy * 0.25, 0.12, r=0.05)
    p.add("Body", s.rbox((0.78, 0.62, 0.72), (0, 0.0, 0.48), r=0.025, seg=2))
    p.add("Accent", s.rbox((0.8, 0.64, 0.04), (0, 0.0, 0.13), r=0.015, seg=1))
    for z, hgt in ((0.27, 0.2), (0.49, 0.2), (0.69, 0.16)):
        p.add("Body", s.rbox((0.7, 0.03, hgt - 0.012), (0, -0.31, z), r=0.008, seg=1))
        p.add("Accent", s.rbox((0.3, 0.024, 0.02), (0, -0.33, z + hgt * 0.25), r=0.008, seg=1))
    p.add("Accent", s.rbox((0.82, 0.36, 0.035), (0, -0.16, 0.855), r=0.012, seg=2))
    up = s.rbox((0.72, 0.42, 0.56), (0, 0.08, 1.14), r=0.025, seg=2)
    p.add("Body", up)
    # flowmeters: a frame, three glass tubes, knobs
    p.add("Accent", s.rbox((0.24, 0.03, 0.32), (-0.16, -0.135, 1.17), r=0.01, seg=2))
    for k, x in enumerate((-0.23, -0.16, -0.09)):
        p.add("Trim", s.rcyl(0.014, 0.24, (x, -0.155, 1.19), seg=10, r=0.003, bevel_seg=1))
        p.add("Eye", s.ball(0.009, (x, -0.163, 1.1 + 0.05 * k), seg=8))
        p.add("Body", s.rcyl(0.018, 0.025, (x, -0.16, 1.04), axis="Y", seg=12, r=0.004, bevel_seg=1))
    # vaporizers on their bar
    p.add("Trim", s.rbox((0.3, 0.03, 0.03), (0.17, -0.135, 1.31), r=0.006, seg=1))
    for x in (0.1, 0.24):
        p.add("Accent", s.rbox((0.11, 0.13, 0.2), (x, -0.2, 1.2), r=0.018, seg=2))
        p.add("Trim", s.rcyl(0.035, 0.02, (x, -0.22, 1.306), seg=14, r=0.004, bevel_seg=1))
    # absorber canister on the left front, hoses to a Y-piece, the breathing bag on its arm
    p.add("Trim", s.rbox((0.06, 0.1, 0.04), (-0.38, -0.1, 0.92), r=0.008, seg=1),
          s.lathe([(0.0, 0.94), (0.08, 0.94), (0.085, 0.96), (0.085, 1.1), (0.08, 1.12), (0.0, 1.12)], seg=20, loc=(-0.43, -0.1, 0)))
    p.add("Accent", s.rcyl(0.09, 0.03, (-0.43, -0.1, 1.135), seg=20, r=0.008, bevel_seg=1))
    for x0, bend in ((-0.4, -0.05), (-0.46, 0.05)):
        pts = [(x0, -0.16, 1.11), (x0 + bend, -0.42, 1.12), (-0.25 + bend, -0.5, 0.98), (-0.2, -0.43, 0.92)]
        p.add("Accent", s.tube(pts, 0.017, smooth=True, segments_per=10))
    p.add("Trim", s.rcyl(0.025, 0.06, (-0.2, -0.43, 0.91), seg=10, r=0.005, bevel_seg=1))
    p.add("Trim", rod((-0.36, 0.0, 1.3), (-0.55, -0.05, 1.3), 0.012, seg=8))
    p.add("Accent", s.loft([(1.29, 0.0, 0.0), (1.27, 0.022, 0.022), (1.22, 0.02, 0.02), (1.13, 0.085, 0.07),
                            (1.03, 0.095, 0.08), (0.95, 0.06, 0.055), (0.92, 0.0, 0.0)], seg=16, loc=(-0.55, -0.05, 0)))
    # gas cylinders at the back with straps
    for x in (-0.18, 0.18):
        p.add("Accent", s.lathe([(0.0, 0.17), (0.07, 0.17), (0.075, 0.2), (0.075, 0.74), (0.05, 0.8), (0.02, 0.82),
                                 (0.0, 0.82)], seg=16, loc=(x, 0.37, 0)))
        p.add("Trim", s.rcyl(0.022, 0.06, (x, 0.37, 0.85), seg=10, r=0.004, bevel_seg=1),
              s.rbox((0.17, 0.05, 0.03), (x, 0.36, 0.6), r=0.006, seg=1), s.rbox((0.17, 0.08, 0.02), (x, 0.35, 0.165), r=0.006, seg=1))
    for sx in (-1, 1):
        for z in (0.95, 1.35):
            p.add("Trim", rod((sx * 0.36, -0.12, z), (sx * 0.41, -0.12, z), 0.012, seg=8))
        p.add("Trim", rod((sx * 0.41, -0.12, 0.95), (sx * 0.41, -0.12, 1.35), 0.014, seg=8))
    # the monitor on a short post
    p.add("Trim", s.rcyl(0.03, 0.1, (0, 0.12, 1.45), seg=12, r=0.006, bevel_seg=1))
    mon = s.rbox((0.48, 0.07, 0.34), (0, 0.1, 1.64), r=0.02, seg=2, rot=(math.radians(-8), 0, 0))
    p.add("Body", mon)
    p.add("Eye", s.rbox((0.42, 0.01, 0.27), (0, 0.065, 1.645), r=0.005, seg=1, rot=(math.radians(-8), 0, 0)))
    recentre(p)
    p.finish()
    return export("anesthesia_machine")


def model_instrument_table():
    """The back table 1.2 x 0.6 at 0.9 with a green drape over it (top and a skirt), instruments on it — a tray of
    clamps, two round basins, a stack of towels — and a lower shelf; beside it (+x) a Mayo stand: a U base on casters,
    the post, a draped tray at 1.1 cantilevered towards -y."""
    p = Parts()
    xt = -0.35
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = xt + sx * 0.55, sy * 0.25
            hcaster(p, x, y, 0.11, r=0.04)
            p.add("Body", rod((x, y, 0.1), (x, y, 0.875), 0.016, seg=10))
    p.add("Body", s.rbox((1.2, 0.6, 0.03), (xt, 0, 0.883), r=0.008, seg=2), s.rbox((1.14, 0.54, 0.02), (xt, 0, 0.3), r=0.006, seg=1))
    outer = s.rbox((1.24, 0.64, 0.19), (xt, 0, 0.904 - 0.095), r=0.006, seg=1)
    inner = s.rbox((1.224, 0.624, 0.19), (xt, 0, 0.896 - 0.095 - 0.004), r=0.0, seg=1)
    drape = s.boolean(outer, inner, "DIFFERENCE")
    p.add("Accent", drape)
    zt = 0.904
    p.add("Trim", hollow_box((0.42, 0.26, 0.04), (xt - 0.3, -0.05, zt + 0.018), 0.006))
    for k in range(6):
        x = xt - 0.47 + k * 0.034
        p.add("Trim", rod((x, -0.14, zt + 0.011), (x + 0.01, 0.05, zt + 0.011), 0.004, seg=6),
              s.rbox((0.018, 0.012, 0.006), (x, -0.145, zt + 0.011), r=0.002, seg=1))
    for x, y, rr in ((xt + 0.12, 0.1, 0.11), (xt + 0.4, -0.15, 0.08)):
        bowl = s.lathe([(0.0, zt - 0.002), (rr * 0.6, zt - 0.002), (rr, zt + 0.05), (rr + 0.006, zt + 0.056),
                        (0.0, zt + 0.056)], seg=24, loc=(x, y, 0))
        inner = s.lathe([(0.0, zt + 0.006), (rr * 0.55, zt + 0.006), (rr - 0.005, zt + 0.05), (rr - 0.004, zt + 0.08),
                         (0.0, zt + 0.08)], seg=24, loc=(x, y, 0))
        p.add("Trim", s.boolean(bowl, inner, "DIFFERENCE"))
    for k in range(3):
        p.add("Accent", s.rbox((0.3, 0.22, 0.028), (xt + 0.33 + 0.005 * k, 0.15, zt + 0.012 + k * 0.026), r=0.01, seg=2))
    # Mayo stand
    xm = 0.65
    for y in (-0.2, 0.2):
        p.add("Body", rod((xm - 0.22, y, 0.1), (xm + 0.22, y, 0.1), 0.015, seg=8))
        for x in (xm - 0.2, xm + 0.2):
            hcaster(p, x, y, 0.1, r=0.035, w=0.025)
    p.add("Body", rod((xm + 0.22, -0.2, 0.1), (xm + 0.22, 0.2, 0.1), 0.015, seg=8))
    p.add("Body", rod((xm + 0.22, 0.0, 0.09), (xm + 0.22, 0.0, 1.06), 0.02, seg=10))
    p.add("Accent", s.rcyl(0.03, 0.05, (xm + 0.22, 0.0, 0.7), seg=12, r=0.006, bevel_seg=1))
    p.add("Body", s.rbox((0.04, 0.42, 0.03), (xm + 0.22, -0.05, 1.07), r=0.006, seg=1))
    p.add("Trim", hollow_box((0.5, 0.36, 0.025), (xm, -0.05, 1.09), 0.006))
    p.add("Accent", s.rbox((0.52, 0.38, 0.006), (xm, -0.05, 1.103), r=0.002, seg=1))
    for k in range(4):
        x = xm - 0.12 + k * 0.06
        p.add("Trim", rod((x, -0.18, 1.11), (x + 0.015, 0.06, 1.11), 0.005, seg=6))
    recentre(p)
    p.finish()
    return export("instrument_table")


def model_scrub_sink():
    """Surgical scrub trough 1.5 m against the wall: a stainless basin with its rim at 0.95 and a deck at the back, a
    knee panel and side panels down to the floor with a dark kick strip and knee-operated valve pads, a splashback to
    1.4, two tall gooseneck taps and a soap dispenser in the middle. The back is flush (Blender +y)."""
    p = Parts()
    yb = 0.275
    basin = s.rbox((1.5, 0.55, 0.32), (0, 0, 0.79), r=0.03, seg=2)
    cut = s.rbox((1.42, 0.4, 0.3), (0, -0.04, 0.835), r=0.06, seg=3)
    basin = s.boolean(basin, cut, "DIFFERENCE")
    s.bevel_edges(basin, 0.004, 1, angle=50)
    p.add("Body", basin)
    p.add("Body", s.rbox((1.5, 0.03, 0.48), (0, yb - 0.013, 1.16), r=0.01, seg=2))
    p.add("Body", s.rbox((1.44, 0.03, 0.66), (0, -0.19, 0.33), r=0.01, seg=2))
    for sx in (-1, 1):
        p.add("Body", s.rbox((0.03, 0.48, 0.66), (sx * 0.735, 0.035, 0.33), r=0.01, seg=2))
        p.add("Accent", s.rbox((0.14, 0.02, 0.1), (sx * 0.4, -0.21, 0.52), r=0.01, seg=2))
    p.add("Accent", s.rbox((1.5, 0.05, 0.08), (0, -0.19, 0.04), r=0.01, seg=1))
    p.add("Trim", s.rcyl(0.04, 0.01, (-0.5, -0.04, 0.688), seg=16, r=0.002, bevel_seg=1),
          s.rcyl(0.04, 0.01, (0.5, -0.04, 0.688), seg=16, r=0.002, bevel_seg=1))
    for x in (-0.4, 0.4):
        p.add("Trim", s.rcyl(0.025, 0.05, (x, 0.2, 0.965), seg=12, r=0.005, bevel_seg=1),
              s.tube([(x, 0.2, 0.97), (x, 0.2, 1.25), (x, 0.17, 1.33), (x, 0.06, 1.33), (x, 0.0, 1.27), (x, -0.01, 1.2)],
                     0.012, smooth=True, segments_per=8),
              s.rcyl(0.016, 0.04, (x, -0.01, 1.19), seg=10, r=0.004, bevel_seg=1))
    p.add("Body", s.rbox((0.11, 0.09, 0.2), (0, yb - 0.07, 1.2), r=0.02, seg=2))
    p.add("Accent", s.rbox((0.08, 0.02, 0.07), (0, yb - 0.117, 1.15), r=0.008, seg=1),
          s.rcyl(0.008, 0.03, (0, yb - 0.09, 1.09), seg=8, r=0.002, bevel_seg=1))
    p.add("Trim", s.rbox((0.025, 0.006, 0.06), (0, yb - 0.115, 1.25), r=0.002, seg=1))
    recentre(p)
    p.finish()
    return export("scrub_sink")


# --------------------------------------------------------------------------- imaging

def model_xray_machine():
    """X-ray room: a radiography table 2.2 x 0.8 (carbon top on a frame with chrome rails) on a pedestal; beside it (+x)
    a floor-to-ceiling column tube stand on floor and ceiling rails, its carriage carrying an arm over the table with
    the tube head and the collimator (light window glowing); at the head end (+y) a wall bucky stand with its detector
    panel facing the table."""
    p = Parts()
    p.add("Accent", s.rbox((0.62, 1.0, 0.04), (0, -0.1, 0.02), r=0.01, seg=1))
    ped = s.loft([(0.03, 0.27, 0.48, 4), (0.5, 0.22, 0.42, 4), (0.62, 0.24, 0.44, 4)], seg=28, loc=(0, -0.1, 0))
    p.add("Body", ped)
    p.add("Body", s.rbox((0.74, 1.6, 0.1), (0, 0, 0.66), r=0.02, seg=2))
    p.add("Accent", s.rbox((0.8, 2.2, 0.05), (0, 0, 0.735), r=0.02, seg=2))
    for sx in (-1, 1):
        for y in (-0.8, 0.0, 0.8):
            p.add("Trim", s.rbox((0.05, 0.04, 0.04), (sx * 0.39, y, 0.68), r=0.006, seg=1))
        p.add("Trim", rod((sx * 0.42, -0.9, 0.68), (sx * 0.42, 0.9, 0.68), 0.012, seg=8))
    xc = 0.8
    p.add("Accent", s.rbox((0.22, 2.4, 0.025), (xc, 0, 0.0125), r=0.006, seg=1),
          s.rbox((0.22, 2.4, 0.04), (xc, 0, CEIL - 0.02), r=0.008, seg=1))
    p.add("Body", s.rbox((0.34, 0.4, 0.12), (xc, 0.1, 0.07), r=0.02, seg=2),
          s.rbox((0.18, 0.18, CEIL - 0.04), (xc, 0.1, (CEIL - 0.04) / 2 + 0.02), r=0.02, seg=2, vertical_only=True),
          s.rbox((0.3, 0.32, 0.14), (xc, 0.1, CEIL - 0.1), r=0.02, seg=2))
    p.add("Body", s.rbox((0.28, 0.27, 0.42), (xc, 0.1, 1.95), r=0.025, seg=2))
    p.add("Accent", s.rbox((0.2, 0.01, 0.12), (xc, -0.04, 2.02), r=0.004, seg=1))
    p.add("Eye", s.rbox((0.16, 0.006, 0.08), (xc, -0.046, 2.02), r=0.003, seg=1))
    p.add("Body", s.rbox((0.72, 0.16, 0.14), (0.4, 0.1, 1.98), r=0.03, seg=2))
    tube = s.loft([(-0.27, 0.12, 0.12), (-0.24, 0.14, 0.14), (0.24, 0.14, 0.14), (0.27, 0.12, 0.12)], seg=24, axis="Y",
                  loc=(0.03, 0.1, 1.86))
    p.add("Body", tube)
    for y in (-0.15, 0.35):
        p.add("Trim", s.rcyl(0.145, 0.03, (0.03, y, 1.86), axis="Y", seg=24, r=0.006, bevel_seg=1))
    coll = s.rbox((0.28, 0.26, 0.2), (0.03, 0.1, 1.64), r=0.02, seg=2, taper=(0.8, 0.8))
    p.add("Accent", coll)
    p.add("Eye", s.rbox((0.17, 0.17, 0.01), (0.03, 0.1, 1.539), r=0.004, seg=1))
    for sx in (-1, 1):
        p.add("Trim", rod((0.03 + sx * 0.14, 0.0, 1.6), (0.03 + sx * 0.14, 0.2, 1.6), 0.012, seg=8))
    # wall bucky stand at the head end
    yw = 1.62
    p.add("Accent", s.rbox((0.55, 0.42, 0.03), (0, yw, 0.015), r=0.008, seg=1))
    p.add("Body", s.rbox((0.2, 0.15, 2.2), (0, yw + 0.08, 1.11), r=0.02, seg=2, vertical_only=True))
    p.add("Body", s.rbox((0.62, 0.12, 0.62), (0, yw - 0.05, 1.35), r=0.03, seg=2))
    p.add("Accent", s.rbox((0.5, 0.012, 0.5), (0, yw - 0.11, 1.35), r=0.006, seg=1))
    p.add("Eye", s.rbox((0.3, 0.004, 0.006), (0, yw - 0.117, 1.35), r=0.001, seg=1),
          s.rbox((0.006, 0.004, 0.3), (0, yw - 0.117, 1.35), r=0.001, seg=1))
    for sx in (-1, 1):
        p.add("Trim", rod((sx * 0.31, yw - 0.05, 1.15), (sx * 0.36, yw - 0.05, 1.15), 0.012, seg=8),
              rod((sx * 0.36, yw - 0.05, 1.15), (sx * 0.36, yw - 0.05, 1.55), 0.012, seg=8),
              rod((sx * 0.31, yw - 0.05, 1.55), (sx * 0.36, yw - 0.05, 1.55), 0.012, seg=8))
    recentre(p)
    p.finish()
    return export("xray_machine")


def model_ct_scanner():
    """CT scanner: the gantry — a rounded white ring 1.9 m with a 0.75 m bore, 0.8 m deep, a seam round its front cover,
    a glowing status ring round the bore and two control panels — on its plinth; in front of it (-y) the patient couch
    2.4 m long on a lofted pedestal, its tabletop running into the bore."""
    p = Parts()
    yc, zc = 0.9, 1.0
    gantry = annulus(0.95, 0.95, 4.0, 0.375, [(-0.4, 0.94), (-0.36, 1.0), (0.34, 1.0), (0.4, 0.94)], seg=48)
    s.groove(gantry, "Y", -0.26, 0.012, 0.006)
    s.bevel_edges(gantry, 0.015, 2, angle=40)
    p.add("Body", s.place(gantry, (0, yc, zc)))
    p.add("Accent", s.rbox((1.1, 0.7, 0.14), (0, yc, 0.07), r=0.02, seg=2))
    liner = revolve(rounded_rect(0.37, 0.385, -0.395, 0.395, 0.005, n=1), seg=48)
    s.place(liner, rot=(math.pi / 2, 0, 0))
    p.add("Trim", s.place(liner, (0, yc, zc)))
    p.add("Eye", flat_ring(0.47, 0.44, 0.012, (0, yc - 0.402, zc), seg=48))
    for sx in (-1, 1):
        p.add("Accent", s.rbox((0.2, 0.02, 0.13), (sx * 0.62, yc - 0.405, 1.32), r=0.008, seg=1))
        p.add("Eye", s.rbox((0.12, 0.006, 0.05), (sx * 0.62, yc - 0.416, 1.34), r=0.002, seg=1))
    # couch
    p.add("Accent", s.rbox((0.62, 1.3, 0.05), (0, -0.55, 0.025), r=0.01, seg=1))
    ped = s.loft([(0.04, 0.3, 0.62, 4), (0.4, 0.22, 0.52, 4), (0.76, 0.24, 0.56, 4)], seg=28, loc=(0, -0.55, 0))
    p.add("Body", ped)
    p.add("Body", s.rbox((0.48, 2.4, 0.06), (0, -0.3, 0.78), r=0.02, seg=2))
    p.add("Accent", s.rbox((0.42, 2.3, 0.045), (0, -0.33, 0.825), r=0.02, seg=2))
    p.add("Trim", s.rbox((0.012, 1.5, 0.02), (0.245, -0.55, 0.77), r=0.004, seg=1),
          s.rbox((0.012, 1.5, 0.02), (-0.245, -0.55, 0.77), r=0.004, seg=1))
    recentre(p)
    p.finish()
    return export("ct_scanner")


# --------------------------------------------------------------------------- waiting and corridors

def model_waiting_seats():
    """Beam seating 2.4 m: four moulded seats on brackets on one steel beam, two T-legs with glides, armrests on posts
    between the seats and at the ends."""
    p = Parts()
    p.add("Trim", s.rbox((2.44, 0.08, 0.06), (0, 0.04, 0.32), r=0.012, seg=2))
    for x in (-0.85, 0.85):
        p.add("Trim", s.rbox((0.06, 0.06, 0.32), (x, 0.04, 0.17), r=0.01, seg=2),
              s.rbox((0.06, 0.6, 0.03), (x, 0.04, 0.03), r=0.01, seg=2))
        for y in (-0.24, 0.32):
            p.add("Accent", s.rbox((0.07, 0.05, 0.02), (x, y, 0.01), r=0.006, seg=1))
    center = [(-0.24, 0.40), (-0.23, 0.435), (-0.16, 0.455), (0.0, 0.45), (0.15, 0.44), (0.21, 0.47), (0.24, 0.6),
              (0.26, 0.78), (0.27, 0.84)]
    prof = shell_profile(center, 0.03)
    for x in (-0.9, -0.3, 0.3, 0.9):
        p.add("Body", prism(prof, "X", x - 0.25, x + 0.25, bevel=0.012, seg=2))
        p.add("Trim", s.rbox((0.08, 0.34, 0.1), (x, 0.0, 0.39), r=0.01, seg=1))
        p.add("Trim", rod((x, 0.08, 0.34), (x, 0.24, 0.58), 0.014, seg=8))
    for x in (-1.2, -0.6, 0.0, 0.6, 1.2):
        p.add("Trim", rod((x, 0.06, 0.33), (x, 0.06, 0.63), 0.012, seg=8))
        p.add("Accent", s.rbox((0.05, 0.4, 0.04), (x, -0.03, 0.64), r=0.015, seg=2))
    recentre(p)
    p.finish()
    return export("waiting_seats")


def model_vitals_monitor():
    """Vital-signs monitor on a rolling stand: a five-star base on casters, a chrome pole to 1.3, a wire basket on a
    clamp, the monitor head with a carry handle, an alarm light on top, a dark screen with glowing waveform traces and
    number blocks, a row of keys; a sensor cable coiled to the basket."""
    p = Parts()
    p.add("Body", s.rcyl(0.06, 0.08, (0, 0, 0.12), seg=16, r=0.012, bevel_seg=1))
    for k in range(5):
        a = 2 * math.pi * k / 5 + math.pi / 2
        c = Vector((math.cos(a), math.sin(a), 0))
        p.add("Body", s.rbox((0.29, 0.05, 0.035), tuple(c * 0.16 + Vector((0, 0, 0.115))), r=0.01, seg=2, rot=(0, 0, a)))
        hcaster(p, c.x * 0.29, c.y * 0.29, 0.1, r=0.035, w=0.025)
    p.add("Trim", rod((0, 0, 0.15), (0, 0, 1.31), 0.017, seg=12))
    p.add("Accent", s.rcyl(0.028, 0.05, (0, 0, 0.75), seg=12, r=0.006, bevel_seg=1))
    p.add("Trim", s.rbox((0.04, 0.06, 0.03), (0, 0.04, 0.75), r=0.006, seg=1),
          hollow_box((0.26, 0.15, 0.1), (0, 0.14, 0.73), 0.006))
    # head on a tilt bracket
    p.add("Accent", s.rbox((0.1, 0.08, 0.06), (0, 0.0, 1.33), r=0.012, seg=2))
    head = s.rbox((0.34, 0.12, 0.27), (0, 0.0, 1.49), r=0.025, seg=3)
    p.add("Body", head, s.rbox((0.26, 0.08, 0.2), (0, 0.07, 1.48), r=0.03, seg=2))
    p.add("Body", s.tube([(-0.11, 0.0, 1.62), (-0.08, 0.0, 1.67), (0.08, 0.0, 1.67), (0.11, 0.0, 1.62)], 0.012, smooth=True))
    p.add("Eye", s.rbox((0.06, 0.04, 0.02), (0.12, 0.0, 1.628), r=0.008, seg=1))
    yf = -0.06
    p.add("Accent", s.rbox((0.25, 0.01, 0.19), (-0.03, yf - 0.002, 1.5), r=0.006, seg=1))
    ys0, ys1 = yf - 0.0085, yf - 0.0045
    for k, z in enumerate((1.56, 1.5, 1.44)):
        pts = []
        for i in range(25):
            x = -0.145 + 0.16 * i / 24
            ph = (i % 8) / 8
            if k == 0:   # ECG: a flat line with spikes
                dz = {2: 0.01, 3: 0.028, 4: -0.012}.get(i % 8, 0.0)
            elif k == 1:
                dz = 0.012 * math.sin(2 * math.pi * ph)
            else:
                dz = 0.01 * (1 - abs(2 * ph - 1)) - 0.005
            pts.append((x, z + dz))
        p.add("Eye", thick_line(pts, 0.004, ys0, ys1))
        p.add("Eye", s.rbox((0.045, 0.004, 0.035), (0.055, (ys0 + ys1) / 2, z), r=0.002, seg=1))
    for z in (1.56, 1.51, 1.46, 1.41):
        p.add("Accent", s.rbox((0.03, 0.012, 0.025), (0.14, yf - 0.002, z), r=0.004, seg=1))
    p.add("Accent", s.tube([(0.17, 0.02, 1.42), (0.2, 0.05, 1.2), (0.12, 0.1, 0.95), (0.06, 0.13, 0.78)], 0.004, smooth=True))
    recentre(p)
    p.finish()
    return export("vitals_monitor")


def model_hand_sanitizer():
    """Wall hand-rub dispenser 0.12 x 0.1 x 0.25 centred at 1.2 m: a white rounded body, a level window, the dark push
    paddle and the nozzle under it. The back is flush (Blender +y)."""
    p = Parts()
    yb = 0.05
    body = s.loft([(1.075, 0.05, 0.04, 3), (1.1, 0.06, 0.05, 3.5), (1.3, 0.06, 0.05, 3.5), (1.325, 0.055, 0.045, 3)],
                  seg=28, round_caps=0.012, loc=(0, 0, 0))
    p.add("Body", body)
    p.add("Accent", s.rbox((0.12, 0.012, 0.22), (0, yb - 0.004, 1.2), r=0.005, seg=1))
    p.add("Accent", s.rbox((0.085, 0.018, 0.09), (0, -0.054, 1.15), r=0.01, seg=2, rot=(math.radians(-6), 0, 0)),
          s.rcyl(0.008, 0.025, (0, -0.025, 1.07), seg=10, r=0.002, bevel_seg=1))
    p.add("Trim", s.rbox((0.025, 0.008, 0.07), (0, -0.05, 1.255), r=0.003, seg=1))
    recentre(p)
    p.finish()
    return export("hand_sanitizer")


def _turned(bms, loc, angle):
    """Shapes built round the origin, turned about z and moved to loc."""
    for b in bms:
        s.place(b, loc, (0, 0, angle))
    return bms


def model_clinic_bench():
    """Clinical lab bench 1.8 x 0.75, the worktop at 0.9: a stack of four drawers on the left, a double-door cupboard
    on the right and a knee space between them under a thick dark resin top with a raised lip; along the back a
    service strip with sockets and, on two stout posts, two full-width reagent shelves (jars, a wash bottle, a
    canister, boxes of gloves and tips). On the top a binocular microscope turned to show its profile (left) and a
    low benchtop centrifuge (right), a rack of sample tubes between them. The front of the top stays clear in the
    middle (loot lies there). The back is flush (Blender +y)."""
    p = Parts()
    top = 0.9
    # the worktop: thick, a lip at the back and the sides
    p.add("Accent", s.rbox((1.8, 0.75, 0.04), (0, 0, top - 0.02), r=0.008, seg=2))
    p.add("Accent", s.rbox((1.8, 0.03, 0.07), (0, 0.36, top + 0.03), r=0.006, seg=1))
    for sx in (-1, 1):
        p.add("Accent", s.rbox((0.025, 0.4, 0.035), (sx * 0.8875, 0.175, top + 0.012), r=0.005, seg=1))
    # the units: drawers on the left, doors on the right; a recessed toe-kick under each
    for sx in (-1, 1):
        p.add("Body", s.rbox((0.56, 0.66, 0.76), (sx * 0.61, 0.025, 0.11 + 0.38), r=0.012, seg=2))
        p.add("Accent", s.rbox((0.52, 0.54, 0.12), (sx * 0.61, 0.07, 0.06), r=0.006, seg=1))
    for k in range(4):                                             # four drawer fronts, a bar handle on each
        z = 0.2 + k * 0.176
        p.add("Body", s.rbox((0.52, 0.02, 0.16), (-0.61, -0.312, z + 0.07), r=0.006, seg=1))
        p.add("Trim", s.rbox((0.26, 0.022, 0.016), (-0.61, -0.33, z + 0.115), r=0.005, seg=1))
    for sd in (-1, 1):                                             # two doors, their handles at the meeting edges
        p.add("Body", s.rbox((0.255, 0.02, 0.69), (0.61 + sd * 0.1325, -0.312, 0.505), r=0.006, seg=1))
        p.add("Trim", s.rbox((0.016, 0.022, 0.22), (0.61 + sd * 0.03, -0.33, 0.66), r=0.005, seg=1))
    p.add("Body", s.rbox((0.72, 0.02, 0.3), (0, 0.33, 0.72), r=0.006, seg=1))   # a rail between the units, high at the back
    # the service strip and the shelves on their posts
    p.add("Body", s.rbox((1.72, 0.05, 0.13), (0, 0.345, top + 0.13), r=0.008, seg=1))
    for x in (-0.55, -0.25, 0.25, 0.55):
        p.add("Accent", s.rbox((0.07, 0.012, 0.07), (x, 0.318, top + 0.13), r=0.004, seg=1))
    for sx in (-1, 1):
        p.add("Body", s.rbox((0.05, 0.06, 0.74), (sx * 0.86, 0.34, top + 0.37), r=0.008, seg=1))
    shelves = (1.3, 1.52)
    for z in shelves:
        p.add("Body", s.rbox((1.76, 0.22, 0.028), (0, 0.262, z), r=0.007, seg=1))
        p.add("Trim", s.rbox((1.7, 0.008, 0.03), (0, 0.156, z + 0.026), r=0.003, seg=1))

    def jar(x, z, rad, h, role="Trim"):
        p.add(role, s.lathe([(0, 0), (rad, 0), (rad, h * 0.74), (rad * 0.6, h * 0.86), (rad * 0.6, h * 0.94), (0, h * 0.94)], seg=16, loc=(x, 0.27, z)))
        p.add("Accent", s.rcyl(rad * 0.68, h * 0.1, (x, 0.27, z + h * 0.95), seg=14, r=0.003, bevel_seg=1))

    def box(x, z, w, h, d=0.14):
        p.add("Body", s.rbox((w, d, h), (x, 0.27, z + h / 2), r=0.006, seg=1))
        p.add("Accent", s.rbox((w * 0.5, 0.006, h * 0.4), (x, 0.27 - d / 2 - 0.001, z + h / 2), r=0.002, seg=1))

    z0, z1 = shelves[0] + 0.012, shelves[1] + 0.012
    jar(-0.7, z0, 0.065, 0.17, "Accent"); jar(-0.52, z0, 0.045, 0.15); box(-0.28, z0, 0.24, 0.09); jar(-0.04, z0, 0.04, 0.13)
    jar(0.1, z0, 0.04, 0.16); box(0.36, z0, 0.2, 0.13); jar(0.62, z0, 0.065, 0.17, "Accent"); jar(0.77, z0, 0.035, 0.12)
    box(-0.62, z1, 0.26, 0.1); jar(-0.36, z1, 0.05, 0.18); jar(-0.2, z1, 0.05, 0.14, "Accent"); box(0.12, z1, 0.3, 0.08, 0.16)
    jar(0.72, z1, 0.06, 0.17)
    # a wash bottle: its bent spout
    wx = 0.5
    p.add("Body", s.lathe([(0, 0), (0.04, 0), (0.04, 0.11), (0.018, 0.14), (0.018, 0.155), (0, 0.155)], seg=14, loc=(wx, 0.27, z1)))
    p.add("Trim", s.tube([(wx, 0.27, z1 + 0.15), (wx, 0.27, z1 + 0.185), (wx - 0.02, 0.27, z1 + 0.195), (wx - 0.07, 0.27, z1 + 0.17)], 0.005, smooth=True, segments_per=6))
    # the rack of sample tubes
    p.add("Body", s.rbox((0.3, 0.1, 0.07), (0.02, 0.22, top + 0.033), r=0.008, seg=1))
    for i in range(7):
        x = -0.1 + i * 0.04
        p.add("Trim", s.rcyl(0.011, 0.1, (x, 0.22, top + 0.085), seg=8, r=0.003, bevel_seg=1))
        p.add("Accent", s.rcyl(0.013, 0.016, (x, 0.22, top + 0.141), seg=8, r=0.003, bevel_seg=1))
    # the microscope (its front along -y here, then turned): a heavy foot, a C-arm, the stage with the light under it,
    # the nosepiece with three objectives, a binocular head leaning to the user, a focus knob each side
    m = []
    roles = []

    def mic(role, *bms):
        for b in bms:
            m.append(b); roles.append(role)

    mic("Accent", s.rbox((0.2, 0.27, 0.045), (0, 0, 0.022), r=0.014, seg=2))
    mic("Body", s.loft([(0.0, 0.055, 0.06, 3, 0, 0.085), (0.1, 0.05, 0.05, 3, 0, 0.1), (0.2, 0.05, 0.05, 3, 0, 0.095),
                        (0.27, 0.055, 0.07, 3, 0, 0.05), (0.3, 0.055, 0.075, 3, 0, 0.02)], seg=16, round_caps=0.012, loc=(0, 0, 0.04)))
    mic("Accent", s.rbox((0.17, 0.17, 0.016), (0, -0.02, 0.165), r=0.004, seg=1))
    mic("Trim", s.rcyl(0.035, 0.03, (0, -0.02, 0.06), seg=14, r=0.004, bevel_seg=1))
    mic("Trim", s.rcyl(0.038, 0.03, (0, -0.02, 0.29), seg=14, r=0.004, bevel_seg=1))
    for a in (-1, 0, 1):
        mic("Trim", s.rcyl(0.012, 0.06 - abs(a) * 0.012, (a * 0.024, -0.02 - (1 - abs(a)) * 0.014, 0.245 + abs(a) * 0.006), seg=10, r=0.002, bevel_seg=1))
    mic("Body", s.rbox((0.13, 0.12, 0.075), (0, -0.005, 0.345), r=0.02, seg=2, rot=(math.radians(-20), 0, 0)))
    lean = math.radians(-40)
    for sx in (-1, 1):
        mic("Body", s.rcyl(0.02, 0.1, (sx * 0.034, -0.085, 0.395), seg=12, r=0.003, bevel_seg=1, rot=(lean, 0, 0)))
        mic("Accent", s.rcyl(0.025, 0.03, (sx * 0.034, -0.122, 0.44), seg=12, r=0.005, bevel_seg=1, rot=(lean, 0, 0)))
        mic("Accent", s.rcyl(0.03, 0.035, (sx * 0.07, 0.085, 0.12), axis="X", seg=14, r=0.005, bevel_seg=1))
    _turned(m, (-0.63, 0.14, top), math.radians(-50))
    for role, b in zip(roles, m):
        p.add(role, b)
    # the centrifuge: low and wide, a flat lid with a round port, the hinge at the back, a sloped fascia with its display
    cx, cy = 0.66, 0.17
    p.add("Body", s.rbox((0.42, 0.36, 0.2), (cx, cy, top + 0.1), r=0.03, seg=2))
    p.add("Accent", s.rbox((0.4, 0.3, 0.04), (cx, cy + 0.025, top + 0.21), r=0.014, seg=2))
    p.add("Trim", s.rcyl(0.07, 0.014, (cx, cy + 0.03, top + 0.233), seg=20, r=0.003, bevel_seg=1))
    p.add("Body", s.rbox((0.3, 0.05, 0.05), (cx, cy + 0.165, top + 0.2), r=0.015, seg=2))
    p.add("Accent", s.rbox((0.4, 0.05, 0.09), (cx, cy - 0.165, top + 0.135), r=0.008, seg=1, rot=(math.radians(25), 0, 0)))
    p.add("Eye", s.rbox((0.15, 0.012, 0.05), (cx - 0.09, cy - 0.185, top + 0.142), r=0.004, seg=1, rot=(math.radians(25), 0, 0)))
    p.add("Trim", s.rcyl(0.02, 0.02, (cx + 0.1, cy - 0.19, top + 0.14), axis="Y", seg=12, r=0.004, bevel_seg=1, rot=(math.radians(25), 0, 0)))
    p.finish()
    return export("clinic_bench")


def model_med_fridge():
    """Medical refrigerator 0.7 x 0.68 x 2.0 on four castor feet: a white cabinet, a tall control header with a big
    temperature readout, a lock and an alarm beacon on top; a door with a broad frame round its window, a long bar
    handle on standoffs; behind the glass four shelves of boxes and vial trays. Roles here: Trim is the glass, Accent
    the dark metal and plastic, Eye the readout."""
    p = Parts()
    w, d, h = 0.7, 0.66, 1.95
    z0 = 0.09
    case = s.rbox((w, d - 0.04, h - z0), (0, 0.02, z0 + (h - z0) / 2), r=0.02, seg=2)
    hole = s.rbox((w - 0.1, d - 0.1, 1.42), (0, -0.05, 0.93), r=0.01, seg=1)
    case = s.boolean(case, hole, "DIFFERENCE")
    s.bevel_edges(case, 0.004, 1, angle=50)
    p.add("Body", case)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.add("Accent", s.rcyl(0.035, 0.1, (sx * 0.27, 0.02 + sy * 0.23, 0.05), seg=12, r=0.006, bevel_seg=1))
    p.add("Accent", s.grille((0, -0.293, 0.155), 0.5, 0.07, n=5, axis="-Y"))
    # the header: readout, lock, two keys; the beacon on top
    zh = h - 0.15
    p.add("Accent", s.rbox((w - 0.06, 0.016, 0.22), (0, -0.292, zh), r=0.01, seg=1))
    p.add("Eye", s.rbox((0.26, 0.01, 0.1), (-0.13, -0.302, zh), r=0.006, seg=1))
    p.add("Body", s.rcyl(0.02, 0.014, (0.14, -0.3, zh + 0.03), axis="Y", seg=12, r=0.003, bevel_seg=1),
          s.rbox((0.05, 0.012, 0.025), (0.23, -0.3, zh + 0.03), r=0.004, seg=1),
          s.rbox((0.05, 0.012, 0.025), (0.23, -0.3, zh - 0.03), r=0.004, seg=1),
          s.rbox((0.05, 0.012, 0.025), (0.14, -0.3, zh - 0.03), r=0.004, seg=1))
    p.add("Accent", s.rcyl(0.04, 0.02, (0.2, 0.05, h + 0.008), seg=14, r=0.004, bevel_seg=1))
    p.add("Eye", s.lathe([(0, 0), (0.032, 0), (0.032, 0.03), (0.02, 0.05), (0, 0.055)], seg=14, loc=(0.2, 0.05, h + 0.015)))
    # shelves and what stands on them
    for k, z in enumerate((0.36, 0.7, 1.04, 1.36)):
        p.add("Accent", s.rbox((w - 0.11, d - 0.16, 0.012), (0, -0.01, z), r=0.003, seg=1))
        for i in range(3):
            x = -0.19 + i * 0.19
            kind = (i + k) % 3
            if kind == 0:
                p.add("Body", s.rbox((0.15, 0.2, 0.11), (x, -0.05, z + 0.062), r=0.008, seg=1))
                p.add("Accent", s.rbox((0.152, 0.03, 0.03), (x, -0.138, z + 0.07), r=0.004, seg=1))
            elif kind == 1:
                p.add("Accent", s.rbox((0.16, 0.24, 0.035), (x, -0.05, z + 0.024), r=0.006, seg=1))
                for j in range(3):
                    for mm in range(4):
                        p.add("Body", s.rcyl(0.014, 0.07, (x - 0.045 + j * 0.045, -0.135 + mm * 0.055, z + 0.07), seg=8, r=0.003, bevel_seg=1))
            else:
                p.add("Body", s.rbox((0.13, 0.13, 0.19), (x, -0.01, z + 0.102), r=0.01, seg=1))
                p.add("Body", s.rbox((0.13, 0.1, 0.1), (x, -0.14, z + 0.057), r=0.008, seg=1))
    # the door: a broad frame round the window, the glass, the handle on its standoffs
    yd = -0.313
    door = s.rbox((w - 0.02, 0.045, 1.5), (0, yd, 0.93), r=0.012, seg=2)
    window = s.rbox((w - 0.18, 0.2, 1.32), (0, yd, 0.93), r=0.045, seg=3, vertical_only=False)
    door = s.boolean(door, window, "DIFFERENCE")
    s.bevel_edges(door, 0.004, 1, angle=50)
    p.add("Body", door)
    p.add("Trim", s.rbox((w - 0.14, 0.012, 1.36), (0, yd, 0.93), r=0.002, seg=1))
    hx = w / 2 - 0.05
    p.add("Accent", s.rbox((0.026, 0.026, 0.62), (hx, yd - 0.06, 1.0), r=0.008, seg=2),
          s.rbox((0.024, 0.06, 0.03), (hx, yd - 0.04, 1.27), r=0.005, seg=1),
          s.rbox((0.024, 0.06, 0.03), (hx, yd - 0.04, 0.73), r=0.005, seg=1))
    recentre(p)
    p.finish()
    return export("med_fridge")


def model_med_workstation():
    """A hospital's computer: a workstation on wheels, 0.6 x 0.68 x 1.5 — not an office desk. A chunky moulded base with
    the battery in it (a charge strip glows on its nose) on four big hooded castors, a broad two-stage column with a
    collar, one deep moulded head (two locked medication drawers in its face, the keyboard on a tray pulled out of it, a
    work mat on top) with a push rail wrapped round its back, a monitor with a thick bezel on a stout arm (a patient's
    chart: a header, rows, a pane, bars — Eye), a wire basket with a sharps bin on the column's front, a hand scanner
    in its cradle and a pump bottle on the top. The nurse stands at -Y."""
    p = Parts()
    # the base: one moulded wedge, the battery's hump at its back, big castors under hoods
    p.add("Body", s.rbox((0.5, 0.56, 0.1), (0, 0.02, 0.15), r=0.04, seg=3, taper=(0.8, 0.86)))
    p.add("Body", s.rbox((0.32, 0.2, 0.1), (0, 0.17, 0.235), r=0.03, seg=3, taper=(0.86, 0.8)))
    p.add("Eye", s.rbox((0.16, 0.022, 0.008), (0, -0.19, 0.2), r=0.003, seg=1))
    p.add("Accent", s.rbox((0.42, 0.012, 0.03), (0, -0.262, 0.125), r=0.005, seg=1))            # the kick strip
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * 0.21, 0.02 + sy * 0.23
            hcaster(p, x, y, 0.1, r=0.055, w=0.04)
            p.add("Body", s.rbox((0.1, 0.15, 0.05), (x, y, 0.125), r=0.022, seg=2))
    # the column: a broad outer stage, the collar, the inner stage up into the head
    p.add("Body", s.rbox((0.17, 0.12, 0.5), (0, 0.08, 0.47), r=0.04, seg=3, vertical_only=True))
    p.add("Accent", s.rbox((0.19, 0.14, 0.045), (0, 0.08, 0.73), r=0.04, seg=2))
    p.add("Trim", s.rbox((0.125, 0.085, 0.22), (0, 0.08, 0.84), r=0.03, seg=2, vertical_only=True))
    p.add("Accent", s.rbox((0.014, 0.06, 0.02), (0.1, 0.05, 0.735), r=0.005, seg=1))             # the height lever
    # the head: one deep moulded unit
    top = 1.06
    p.add("Body", s.rbox((0.58, 0.46, 0.15), (0, 0.04, top - 0.075), r=0.04, seg=3, taper=(0.97, 0.97)))
    p.add("Accent", s.rbox((0.44, 0.3, 0.008), (0, 0.02, top + 0.002), r=0.012, seg=1))          # the work mat
    yf = -0.19                                                                                    # its face
    for k in range(2):
        z = top - 0.04 - k * 0.05
        p.add("Body", s.rbox((0.44, 0.012, 0.042), (0, yf - 0.004, z), r=0.005, seg=1))
        p.add("Accent", s.rbox((0.14, 0.016, 0.012), (-0.07, yf - 0.014, z + 0.006), r=0.004, seg=1))
        p.add("Trim", s.rcyl(0.008, 0.012, (0.17, yf - 0.012, z), axis="Y", seg=10, r=0.002, bevel_seg=1))
    # the keyboard's tray out of the head's foot, a lip at its front
    tz = top - 0.132
    p.add("Body", s.rbox((0.5, 0.26, 0.028), (0, -0.26, tz), r=0.012, seg=2))
    p.add("Body", s.rbox((0.5, 0.016, 0.02), (0, -0.382, tz + 0.016), r=0.006, seg=1))
    keyboard.add("Keys", (0, -0.28, tz + 0.014), light=True, mouse=0.0)
    # the push rail round the back, on four posts
    rz = top + 0.07
    p.add("Trim", s.tube([(-0.31, -0.08, rz), (-0.31, 0.2, rz), (-0.24, 0.3, rz), (0.24, 0.3, rz), (0.31, 0.2, rz), (0.31, -0.08, rz)], 0.015, smooth=False))
    for x, y in ((-0.3, -0.06), (0.3, -0.06), (-0.22, 0.27), (0.22, 0.27)):
        p.add("Trim", rod((x * 0.95, y * 0.93, top - 0.03), (x * 1.02, y * 1.05, rz), 0.011, seg=8))
    # the monitor: a stout post from the head's back, a knuckle, a thick bezel round an inset screen, tipped back
    p.add("Trim", s.rbox((0.085, 0.06, 0.24), (0, 0.2, top + 0.11), r=0.02, seg=2, vertical_only=True))
    p.add("Accent", s.rcyl(0.04, 0.1, (0, 0.2, top + 0.24), axis="X", seg=16, r=0.008, bevel_seg=1))
    p.add("Trim", s.rbox((0.07, 0.12, 0.05), (0, 0.16, top + 0.27), r=0.012, seg=2, rot=(math.radians(18), 0, 0)))
    mz, my, tilt = top + 0.3, 0.1, math.radians(-8)

    def on_head(x, ly, lz, w, d, h, r=0.002):
        """A box in the monitor's own frame: ly from its middle towards its back, lz up its tipped face."""
        yy, zz = ly * math.cos(tilt) - lz * math.sin(tilt), ly * math.sin(tilt) + lz * math.cos(tilt)
        return s.rbox((w, d, h), (x, my + yy, mz + zz), r=r, seg=1, rot=(tilt, 0, 0))
    mw, mh = 0.42, 0.27
    p.add("Accent", s.rbox((mw, 0.04, mh), (0, my, mz), r=0.014, seg=3, rot=(tilt, 0, 0)))
    p.add("Accent", on_head(0, 0.03, -0.01, 0.2, 0.03, 0.14, r=0.012))                            # the hump at its back
    for sz in (-1, 1):
        p.add("Body", on_head(0, -0.024, sz * (mh / 2 - 0.011), mw, 0.014, 0.024, r=0.005))       # the bezel: light, as the cart
    for sx in (-1, 1):
        p.add("Body", on_head(sx * (mw / 2 - 0.011), -0.024, 0, 0.024, 0.014, mh, r=0.005))

    def lit(x, lz, w, h):
        return on_head(x, -0.0215, lz, w, 0.004, h, r=0.0015)
    p.add("Eye", lit(0, 0.092, 0.35, 0.026))                                                      # the header: the patient's name
    for k in range(4):                                                                             # the chart's rows, a tick box each
        p.add("Eye", lit(-0.06, 0.05 - k * 0.032, 0.2, 0.012), lit(-0.172, 0.05 - k * 0.032, 0.013, 0.013))
    p.add("Eye", lit(0.118, 0.035, 0.11, 0.06))                                                   # a picture's pane
    for k, h in enumerate((0.016, 0.03, 0.022, 0.038, 0.026)):                                    # a small chart of bars under it
        p.add("Eye", lit(0.072 + k * 0.023, -0.07 + h / 2, 0.013, h))
    p.add("Eye", lit(-0.085, -0.095, 0.18, 0.018))                                                # the bar of buttons
    # the wire basket on the column's front, the sharps bin in it
    p.add("Trim", s.rbox((0.06, 0.05, 0.04), (0, 0.0, 0.56), r=0.008, seg=1))
    p.add("Trim", hollow_box((0.36, 0.2, 0.16), (0, -0.09, 0.56), 0.007))
    p.add("Accent", s.rbox((0.15, 0.14, 0.2), (-0.08, -0.09, 0.6), r=0.02, seg=2, taper=(0.88, 0.88)))
    p.add("Body", s.rbox((0.14, 0.13, 0.025), (-0.08, -0.09, 0.712), r=0.01, seg=1))
    # on the top: the hand scanner in its cradle, the pump bottle
    p.add("Trim", s.rbox((0.07, 0.07, 0.03), (0.21, 0.13, top + 0.015), r=0.01, seg=1))
    p.add("Accent", s.rbox((0.036, 0.046, 0.11), (0.21, 0.13, top + 0.07), r=0.012, seg=2))
    p.add("Accent", s.rbox((0.06, 0.1, 0.05), (0.21, 0.1, top + 0.14), r=0.016, seg=2, rot=(math.radians(-25), 0, 0)))
    p.add("Eye", s.rbox((0.04, 0.006, 0.022), (0.21, 0.05, top + 0.118), r=0.003, seg=1, rot=(math.radians(-25), 0, 0)))
    p.add("Body", s.rcyl(0.036, 0.13, (-0.21, 0.14, top + 0.065), seg=14, r=0.01, bevel_seg=2))
    p.add("Accent", s.rcyl(0.014, 0.04, (-0.21, 0.14, top + 0.148), seg=10, r=0.003, bevel_seg=1))
    p.add("Accent", s.rbox((0.016, 0.06, 0.012), (-0.21, 0.115, top + 0.17), r=0.004, seg=1))
    p.finish()
    return export("med_workstation")


MODELS = {
    "bed_headwall": model_bed_headwall,
    "bed_curtain": model_bed_curtain,
    "nurse_station": model_nurse_station,
    "gurney": model_gurney,
    "medication_cart": model_medication_cart,
    "linen_cart": model_linen_cart,
    "surgical_table": model_surgical_table,
    "surgical_lights": model_surgical_lights,
    "anesthesia_machine": model_anesthesia_machine,
    "instrument_table": model_instrument_table,
    "scrub_sink": model_scrub_sink,
    "xray_machine": model_xray_machine,
    "ct_scanner": model_ct_scanner,
    "waiting_seats": model_waiting_seats,
    "vitals_monitor": model_vitals_monitor,
    "hand_sanitizer": model_hand_sanitizer,
    "clinic_bench": model_clinic_bench,
    "med_fridge": model_med_fridge,
    "med_workstation": model_med_workstation,
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
