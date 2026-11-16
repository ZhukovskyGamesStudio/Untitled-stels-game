"""Work-floor furniture, round 2: the lobby (info kiosk, speed-gate turnstile, round bench around a planter, logo totem,
queue barrier), the open space (standing desk, planter divider, mobile whiteboard, file pedestal, acoustic sofa pod),
the office (executive desk, filing cabinet, credenza, guest chair, wall clock) and the meeting room (conference table,
projector screen, flipchart, AV credenza, meeting chair).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms2_work.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--focus x,y,z,r] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y, z up, origin on the
floor in the middle of the footprint. Wall-hung pieces (clock, projector screen) keep their real height and a back face
flush with the footprint's back. Object names are material roles: Body, Accent, Trim, Eye (glows).
Forms come from shapes.py and are finished by shapes.part; export() goes through meshfix (floor=True) like poi.export.
"""
import math
import os
import sys

import bmesh
from mathutils import Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --)
import meshfix  # noqa: E402
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

def orient(bm, p0, p1):
    """A shape built along +Z from z = 0 turned to run from p0 towards p1."""
    d = Vector(p1) - Vector(p0)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(p0), verts=bm.verts)
    return bm


def rod(p0, p1, r, seg=10, bevel=0.0):
    """Straight round bar between two points (legs, struts, stems)."""
    length = (Vector(p1) - Vector(p0)).length
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=length)
    if bevel:
        s.bevel_edges(bm, min(bevel, 0.4 * r), 1, angle=50)
    bmesh.ops.translate(bm, vec=Vector((0, 0, length / 2)), verts=bm.verts)
    return orient(bm, p0, p1)


def prism(pts, axis, lo, hi, bevel=0.0, seg=1):
    """Polygon (pts in the two other axes, in X-Y-Z order) extruded along axis from lo to hi."""
    bm = bmesh.new()

    def co(t, u, v):
        return {"X": (t, u, v), "Y": (u, t, v), "Z": (u, v, t)}[axis]

    a = [bm.verts.new(co(lo, u, v)) for u, v in pts]
    b = [bm.verts.new(co(hi, u, v)) for u, v in pts]
    n = len(pts)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bm.faces.new(a)
    bm.faces.new(b[::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel:
        s.bevel_edges(bm, bevel, seg, angle=30)
    return bm


def rounded_rect(r0, r1, z0, z1, rad, n=2):
    """Closed (r, z) outline of a rounded rectangle, counter-clockwise — a profile for revolve()."""
    rad = min(rad, 0.49 * (r1 - r0), 0.49 * (z1 - z0))
    pts = []
    for cx, cz, a0 in ((r1 - rad, z0 + rad, -90), (r1 - rad, z1 - rad, 0), (r0 + rad, z1 - rad, 90),
                       (r0 + rad, z0 + rad, 180)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((cx + rad * math.cos(a), cz + rad * math.sin(a)))
    return pts


def revolve(profile, seg=32, a0=0.0, a1=2 * math.pi, loc=(0, 0, 0)):
    """Revolve a closed (r, z) outline around Z: a ring or torus (lathe() can only make solids reaching the axis).
    A partial sweep a0..a1 is closed with flat end caps (ring segments)."""
    full = abs((a1 - a0) - 2 * math.pi) < 1e-6
    bm = bmesh.new()
    steps = seg if full else seg + 1
    rings = []
    for i in range(steps):
        a = a0 + (a1 - a0) * i / seg
        rings.append([bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for r, z in profile])
    m = len(profile)
    for i in range(seg):
        A = rings[i]
        B = rings[(i + 1) % steps]
        for j in range(m):
            bm.faces.new((A[j], A[(j + 1) % m], B[(j + 1) % m], B[j]))
    if not full:
        bm.faces.new(rings[0])
        bm.faces.new(rings[-1][::-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return s.place(bm, loc)


def flat_ring(r_out, r_in, depth, loc, seg=32, rad=0.003):
    """A flat ring (logo, LED ring) facing -Y, centred at loc."""
    bm = revolve(rounded_rect(r_in, r_out, -depth / 2, depth / 2, rad, n=1), seg=seg)
    s.place(bm, rot=(math.pi / 2, 0, 0))
    return s.place(bm, loc)


def frame(size, loc, border, r=0.006, seg=2, axis_open="Y"):
    """A rectangular frame (board frame, screen border): a rounded box with a through-hole, border wide."""
    outer = s.rbox(size, loc, r=r, seg=seg)
    hole = list(size)
    for k, c in enumerate("XYZ"):
        hole[k] = size[k] + 0.1 if c == axis_open else size[k] - 2 * border
    cut = s.rbox(tuple(hole), loc, r=0)
    bm = s.boolean(outer, cut, "DIFFERENCE")
    s.bevel_edges(bm, min(r, border * 0.3), 1, angle=50)
    return bm


def shaker(bm, min_area, thickness=0.04, depth=-0.006):
    """Recessed panel on the front face of a door or drawer."""
    return s.inset_faces(bm, s.facing((0, -1, 0), min_area=min_area, tol=0.95), thickness=thickness, depth=depth)


def bar_handle(x, z, y_face, length, axis="X", r=0.008, stand=0.018):
    """A D-shaped bar pull: a round bar on two stand-offs, sunk 3 mm into the face at y_face (front = -y)."""
    y_bar = y_face - stand
    out = []
    if axis == "Z":
        out.append(s.rcyl(r, length, (x, y_bar, z), axis="Z", seg=10, r=r * 0.4, bevel_seg=1))
        for dz in (-1, 1):
            out.append(s.rcyl(r * 0.75, stand + 0.003, (x, y_face - stand / 2 + 0.0015, z + dz * (length / 2 - 0.02)),
                              axis="Y", seg=8, r=0.002, bevel_seg=1))
    else:
        out.append(s.rcyl(r, length, (x, y_bar, z), axis="X", seg=10, r=r * 0.4, bevel_seg=1))
        for dx in (-1, 1):
            out.append(s.rcyl(r * 0.75, stand + 0.003, (x + dx * (length / 2 - 0.02), y_face - stand / 2 + 0.0015, z),
                              axis="Y", seg=8, r=0.002, bevel_seg=1))
    return out


def blob(loc, radii, sub=2, amp=0.18, freq=2.2, seed=0.0):
    """A lumpy leafy mass (shrub, tree crown): an icosphere pushed in and out by noise, scaled to radii."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    off = Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    for v in bm.verts:
        n = v.co.normalized()
        v.co = n * (1.0 + amp * noise.noise(n * freq + off))
    return s.place(bm, loc, scale=radii)


def leaf(base, length, width, lean, azim, thick=0.006, seg=8):
    """A sword leaf (snake plant): a flat pointed loft standing up from base, leaning out by lean towards azim."""
    bm = s.loft([(0, width * 0.5, thick, 2), (length * 0.25, width, thick, 2), (length * 0.7, width * 0.8, thick * 0.8, 2),
                 (length, 0, 0)], seg=seg)
    return s.place(bm, base, rot=(lean, 0, azim))


def caster(x, y, z_top, wheel_r=0.024, axis="X"):
    """A swivel caster: a stem into the furniture above, a fork block and a wheel touching the floor."""
    wz = wheel_r
    stem = s.rcyl(0.009, z_top - wz + 0.01, (x, y, (z_top + wz) / 2 + 0.005), seg=8, r=0.002, bevel_seg=1)
    fork = s.rbox((0.034, 0.034, 0.03), (x, y, wz + 0.012), r=0.006, seg=1)
    wheel = s.rcyl(wheel_r, 0.022, (x, y, wz), axis=axis, seg=14, r=0.005, bevel_seg=1)
    return [stem, fork], [wheel]


# --------------------------------------------------------------------------- lobby

def model_info_kiosk():
    """Touch-screen info kiosk: a floor plate, a slim lofted pillar and a wedge head whose front leans back 28° with a
    dark bezel and the glowing screen; a ticket slot under the head."""
    plate = s.rbox((0.6, 0.5, 0.03), (0, 0, 0.015), r=0.012, seg=2)
    pillar = s.loft([(0.02, 0.17, 0.12, 3.5), (0.5, 0.15, 0.1, 3.5), (1.0, 0.15, 0.1, 3.5), (1.16, 0.17, 0.12, 3.5)],
                    seg=28)
    head = prism([(-0.15, 1.1), (0.14, 1.1), (0.15, 1.5), (0.05, 1.5)], "X", -0.29, 0.29, bevel=0.014, seg=2)
    s.groove(head, "X", -0.2, 0.006, 0.003)
    s.groove(head, "X", 0.2, 0.006, 0.003)
    s.part("Body", [pillar, head])

    # the slanted face runs from (y -0.15, z 1.1) to (0.05, 1.5): direction d, outward normal n
    d = Vector((0, 0.2, 0.4)).normalized()
    n = Vector((0, -d.z, d.y))
    tilt = -math.atan2(d.y, d.z)
    mid = Vector((0, -0.05, 1.3))
    bezel = s.rbox((0.54, 0.012, 0.4), tuple(mid + n * 0.004), r=0.008, seg=2, rot=(tilt, 0, 0))
    slot = s.rbox((0.2, 0.05, 0.03), (0, -0.115, 1.0), r=0.008, seg=2)
    s.part("Accent", [plate, bezel, slot])
    screen = s.rbox((0.48, 0.006, 0.34), tuple(mid + n * 0.0115), r=0.004, seg=1, rot=(tilt, 0, 0))
    s.part("Eye", [screen])
    return export("info_kiosk")


def model_security_turnstile():
    """Speed-gate cabinet 1.2 m: a pill-shaped body with a toe seam, a dark top cap, a glowing guide line all around
    under the cap, a card reader on top at the entry end, and two glass flaps closing the lanes on both sides."""
    body = s.loft([(0.0, 0.57, 0.112, 6), (0.93, 0.57, 0.112, 6)], seg=40)
    s.groove(body, "Z", 0.07, 0.008, 0.004)
    s.part("Body", [body])
    cap = s.loft([(0.925, 0.6, 0.12, 6), (1.0, 0.6, 0.12, 6)], seg=40, round_caps=0.018)
    s.part("Accent", [cap])
    flaps = []
    for sy in (-1, 1):
        pts = [(sy * 0.09, 0.5), (sy * 0.24, 0.5), (sy * 0.3, 0.58), (sy * 0.3, 0.82), (sy * 0.22, 0.9),
               (sy * 0.09, 0.9)]
        flaps.append(prism(pts, "X", -0.007, 0.007, bevel=0.003))
    s.part("Trim", flaps)
    line = s.loft([(0.912, 0.59, 0.122, 6), (0.93, 0.59, 0.122, 6)], seg=40)
    reader = s.rbox((0.13, 0.13, 0.012), (-0.42, 0, 1.003), r=0.004, seg=1)
    arrow = s.rbox((0.06, 0.06, 0.01), (0.42, 0, 1.002), r=0.003, seg=1, rot=(0, 0, math.pi / 4))
    s.part("Eye", [line, reader, arrow])
    return export("security_turnstile")


def model_round_bench_planter():
    """Island seat: a dark plinth ring with a toe kick, six upholstered cushion segments around a tall round planter,
    a small tree (trunk, branches, a lumpy crown) and low shrubs on the soil."""
    plinth = revolve(rounded_rect(0.52, 0.85, 0.0, 0.07, 0.01, n=1), seg=40)
    shell = revolve(rounded_rect(0.5, 0.88, 0.06, 0.31, 0.015, n=2), seg=40)
    planter = s.lathe([(0.0, 0.01), (0.51, 0.01), (0.505, 0.84), (0.53, 0.845), (0.535, 0.9), (0.47, 0.9),
                       (0.47, 0.88), (0.0, 0.88)], seg=40)
    soil = s.rcyl(0.475, 0.03, (0, 0, 0.875), seg=32, r=0.005)
    trunk = s.tube([(0, 0, 0.86), (0.02, 0.01, 1.15), (-0.01, 0.0, 1.45)], 0.035, segments_per=6, radii=[1.0, 0.75, 0.5])
    br = [s.tube([(0.015, 0.008, 1.2), (0.12, 0.06, 1.38)], 0.016, segments_per=3, radii=[1.0, 0.6]),
          s.tube([(0.0, 0.0, 1.3), (-0.12, 0.05, 1.45)], 0.015, segments_per=3, radii=[1.0, 0.6])]
    s.part("Trim", [plinth, shell, planter, soil, trunk, *br])

    cushions = []
    gap = math.radians(1.2)
    for k in range(6):
        a0 = k * math.pi / 3 + gap
        a1 = (k + 1) * math.pi / 3 - gap
        cushions.append(revolve(rounded_rect(0.505, 0.9, 0.3, 0.46, 0.035, n=2), seg=10, a0=a0, a1=a1))
    s.part("Body", cushions)

    leaves = [blob((0.0, 0.0, 1.62), (0.3, 0.28, 0.24), sub=3, seed=1),
              blob((0.17, 0.07, 1.5), (0.2, 0.18, 0.16), sub=3, seed=2),
              blob((-0.16, 0.09, 1.54), (0.2, 0.18, 0.16), sub=3, seed=3),
              blob((0.04, -0.17, 1.52), (0.2, 0.18, 0.15), sub=3, seed=4),
              blob((-0.02, 0.02, 1.79), (0.17, 0.16, 0.13), sub=3, seed=5)]
    for k in range(7):
        a = 2 * math.pi * k / 7 + 0.3
        leaves.append(blob((0.32 * math.cos(a), 0.32 * math.sin(a), 0.93), (0.11, 0.11, 0.08), sub=2, amp=0.25,
                           seed=10 + k))
    s.part("Accent", leaves)
    return export("round_bench_planter")


def model_logo_totem():
    """Company sign: a tall slab on a dark plinth, on both faces a dark panel with the glowing logo (a ring around a
    diamond) and the company name as two light bars."""
    slab = s.rbox((0.7, 0.16, 1.9), (0, 0, 1.03), r=0.02, seg=2)
    s.groove(slab, "Z", 0.32, 0.008, 0.004)
    s.part("Body", [slab])
    plinth = s.rbox((0.8, 0.4, 0.09), (0, 0, 0.045), r=0.012, seg=2)
    acc = [plinth]
    eye = []
    for sy in (-1, 1):
        acc.append(s.rbox((0.58, 0.02, 0.78), (0, sy * 0.08, 1.42), r=0.008, seg=2))
        y = sy * 0.091
        eye.append(flat_ring(0.17, 0.125, 0.012, (0, y, 1.58), seg=36))
        eye.append(s.rbox((0.12, 0.012, 0.12), (0, y, 1.58), r=0.004, seg=1, rot=(0, math.pi / 4, 0)))
        eye.append(s.rbox((0.44, 0.012, 0.05), (0, y, 1.25), r=0.004, seg=1))
        eye.append(s.rbox((0.3, 0.012, 0.026), (0, y, 1.17), r=0.004, seg=1))
    s.part("Accent", acc)
    s.part("Eye", eye)
    return export("logo_totem")


def model_queue_barrier():
    """Two chrome stanchions on domed round bases, belt-cassette heads, a red belt stretched between them."""
    body, base = [], []
    for x in (-0.65, 0.65):
        base.append(s.lathe([(0.0, 0.0), (0.15, 0.0), (0.15, 0.008), (0.14, 0.02), (0.05, 0.04), (0.034, 0.05),
                             (0.0, 0.05)], seg=32, loc=(x, 0, 0)))
        body.append(s.rcyl(0.028, 0.88, (x, 0, 0.48), seg=16, r=0.002, bevel_seg=1))
        body.append(s.lathe([(0.0, 0.89), (0.04, 0.89), (0.043, 0.91), (0.043, 0.965), (0.036, 0.99), (0.0, 1.0)],
                            seg=20, loc=(x, 0, 0)))
        body.append(s.rcyl(0.034, 0.03, (x, 0, 0.06), seg=16, r=0.006, bevel_seg=1))
    s.part("Body", body)
    s.part("Accent", base)
    belt = s.rbox((1.3, 0.006, 0.05), (0, 0, 0.94), r=0.002, seg=1)
    clip = s.rbox((0.04, 0.016, 0.056), (0.6, 0, 0.94), r=0.004, seg=1)
    s.part("Trim", [belt, clip])
    return export("queue_barrier")


# --------------------------------------------------------------------------- open space

def model_standing_desk():
    """Sit-stand desk at standing height: T-feet, two telescoping columns, a frame under a 1.4 x 0.7 top, a control
    paddle under the front edge, a monitor on a stand at the back, keyboard and mouse at the front."""
    s.part("Body", [s.rbox((1.4, 0.7, 0.03), (0, 0, 1.085), r=0.008, seg=2)])
    acc = []
    for x in (-0.6, 0.6):
        acc.append(s.rbox((0.07, 0.66, 0.035), (x, 0, 0.0175), r=0.01, seg=2))
        acc.append(s.rbox((0.09, 0.065, 0.6), (x, 0.05, 0.33), r=0.008, seg=2))
        acc.append(s.rbox((0.075, 0.05, 0.47), (x, 0.05, 0.835), r=0.006, seg=2))
        acc.append(s.rbox((0.06, 0.6, 0.03), (x, 0, 1.058), r=0.006, seg=1))
    acc.append(s.rbox((1.14, 0.06, 0.05), (0, 0.05, 1.045), r=0.008, seg=1))
    acc.append(s.rbox((0.11, 0.05, 0.022), (0.5, -0.345, 1.062), r=0.006, seg=1))
    # monitor: base plate, neck, back bulge, shell
    acc.append(s.rbox((0.22, 0.17, 0.012), (0, 0.17, 1.104), r=0.005, seg=1))
    acc.append(s.rbox((0.05, 0.03, 0.24), (0, 0.205, 1.215), r=0.008, seg=1))
    acc.append(s.rbox((0.3, 0.04, 0.2), (0, 0.185, 1.32), r=0.012, seg=2))
    acc.append(s.rbox((0.6, 0.04, 0.36), (0, 0.16, 1.32), r=0.008, seg=2))
    kb = s.rbox((0.42, 0.13, 0.02), (0, -0.13, 1.107), r=0.006, seg=1, taper=(0.99, 0.94))
    s.groove(kb, "X", 0.13, 0.004, 0.002)
    acc += [kb, s.rbox((0.06, 0.1, 0.03), (0.3, -0.13, 1.108), r=0.014, seg=2)]
    s.part("Accent", acc)
    s.part("Eye", [s.rbox((0.57, 0.006, 0.32), (0, 0.139, 1.325), r=0.003, seg=1)])
    return export("standing_desk")


def model_planter_divider():
    """Long planter box on a recessed plinth with a dark rim, three clumps of tall snake-plant leaves and low shrubs
    between them — a green divider between desk rows."""
    box = s.rbox((1.58, 0.34, 0.56), (0, 0, 0.32), r=0.012, seg=2)
    s.groove(box, "Z", 0.12, 0.008, 0.004)
    s.part("Body", [box])
    plinth = s.rbox((1.52, 0.28, 0.06), (0, 0, 0.03), r=0.006, seg=1)
    rim = frame((1.6, 0.36, 0.04), (0, 0, 0.615), 0.03, r=0.008, seg=2, axis_open="Z")
    soil = s.rbox((1.55, 0.31, 0.02), (0, 0, 0.605), r=0.004, seg=1)
    s.part("Trim", [plinth, rim, soil])
    leaves = []
    for cx, base_len, turn in ((-0.52, 0.44, 0.0), (0.0, 0.48, 0.5), (0.52, 0.42, 1.1)):
        for k in range(9):
            a = turn + 2 * math.pi * k / 9
            lean = math.radians(6 + 11 * ((k * 5) % 9) / 8)
            length = base_len * (0.7 + 0.3 * ((k * 7) % 9) / 8)
            r = 0.03 if k % 2 else 0.012
            leaves.append(leaf((cx + r * math.cos(a), r * math.sin(a) * 0.6, 0.59), length, 0.03, lean, a + math.pi / 2,
                                   seg=10))
    for k, cx in enumerate((-0.74, -0.26, 0.26, 0.74)):
        rx = 0.065 if abs(cx) > 0.5 else 0.1
        leaves.append(blob((cx, 0.0, 0.66), (rx, 0.12, 0.09), sub=2, amp=0.25, seed=20 + k))
    s.part("Accent", leaves)
    return export("planter_divider")


def model_mobile_whiteboard():
    """Double-sided whiteboard on a wheeled frame: two uprights on foot bars with four casters, a low stretcher, the
    board pivoting between the uprights in an aluminium frame, a marker tray in front."""
    body, rubber = [], []
    for x in (-0.68, 0.68):
        body.append(s.rbox((0.04, 0.04, 1.85), (x, 0, 0.97), r=0.01, seg=2))
        body.append(s.rbox((0.05, 0.56, 0.04), (x, 0, 0.07), r=0.01, seg=2))
        body.append(s.rcyl(0.026, 0.02, (x, 0, 1.895), seg=12, r=0.006, bevel_seg=1))
        for y in (-0.25, 0.25):
            st, wh = caster(x, y, 0.06, wheel_r=0.026)
            body += st
            rubber += wh
    body.append(s.rbox((1.35, 0.03, 0.03), (0, 0, 0.35), r=0.008, seg=1))
    s.part("Body", body + rubber)
    acc = [frame((1.24, 0.032, 0.94), (0, 0, 1.3), 0.03, r=0.008, seg=2)]
    for sx in (-1, 1):
        acc.append(s.rcyl(0.018, 0.06, (sx * 0.64, 0, 1.3), axis="X", seg=12, r=0.004, bevel_seg=1))
    acc.append(s.rbox((0.6, 0.06, 0.018), (0, -0.04, 0.84), r=0.004, seg=1))
    acc.append(s.rbox((0.6, 0.008, 0.035), (0, -0.068, 0.855), r=0.003, seg=1))
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((1.19, 0.02, 0.89), (0, 0, 1.3), r=0.003, seg=1)])
    return export("mobile_whiteboard")


def model_file_pedestal():
    """Under-desk mobile pedestal: a carcass on four casters, a pencil drawer, a box drawer and a file drawer, each
    with a bar pull, a lock at the top right."""
    body = [s.rbox((0.4, 0.535, 0.565), (0, 0.0125, 0.3325), r=0.01, seg=2)]
    acc = []
    fronts = ((0.605, 0.495), (0.485, 0.335), (0.325, 0.06))
    for top, bot in fronts:
        d = s.rbox((0.39, 0.02, top - bot), (0, -0.265, (top + bot) / 2), r=0.006, seg=2)
        body.append(d)
        acc += bar_handle(0, top - 0.035, -0.275, 0.16, r=0.006, stand=0.012)
    acc.append(s.rcyl(0.011, 0.016, (0.15, -0.278, 0.575), axis="Y", seg=12, r=0.003, bevel_seg=1))
    for x in (-0.15, 0.15):
        for y in (-0.2, 0.2):
            st, wh = caster(x, y, 0.055, wheel_r=0.022, axis="X")
            acc += st + wh
    s.part("Body", body)
    s.part("Accent", acc)
    return export("file_pedestal")


def model_sofa_pod():
    """High-back acoustic sofa booth: a shell (back panel and two wings with a raked front edge, quilted upper back)
    on a dark recessed plinth, two seat cushions and two leaning back cushions."""
    shell = []
    for sx in (-1, 1):
        pts = [(-0.45, 0.04), (0.45, 0.04), (0.45, 1.4), (0.08, 1.4), (-0.45, 0.98)]
        x0, x1 = (0.8, 0.9) if sx > 0 else (-0.9, -0.8)
        shell.append(prism(pts, "X", x0, x1, bevel=0.018, seg=2))
    back = s.rbox((1.62, 0.14, 1.32), (0, 0.38, 0.72), r=0.02, seg=2)
    for z in (1.12, 1.25):
        s.groove(back, "Z", z, 0.01, 0.006)
    shell.append(back)
    shell.append(s.rbox((1.62, 0.75, 0.29), (0, -0.065, 0.205), r=0.015, seg=2))
    s.part("Trim", shell)
    s.part("Accent", [s.rbox((1.7, 0.8, 0.05), (0, 0, 0.025), r=0.008, seg=1)])
    cushions = []
    for x in (-0.4, 0.4):
        seat = s.rbox((0.79, 0.68, 0.14), (x, -0.1, 0.41), r=0.045, seg=3)
        cushions.append(seat)
        cushions.append(s.rbox((0.79, 0.14, 0.6), (x, 0.25, 0.8), r=0.05, seg=3, rot=(math.radians(-8), 0, 0)))
    s.part("Body", cushions)
    return export("sofa_pod")


# --------------------------------------------------------------------------- office

def model_executive_desk():
    """Executive desk 1.8 x 0.9: two drawer pedestals on dark plinths, a modesty panel at the back, a thick top with a
    seam and a leather inlay, bar pulls, and a banker's-style desk lamp at the back left."""
    body = [s.rbox((1.8, 0.9, 0.045), (0, 0, 0.7375), r=0.012, seg=2)]
    s.groove(body[0], "Z", 0.735, 0.006, 0.003)
    acc = []
    for sx in (-1, 1):
        x = sx * 0.66
        body.append(s.rbox((0.46, 0.8, 0.68), (x, 0, 0.385), r=0.01, seg=2))
        acc.append(s.rbox((0.42, 0.74, 0.05), (x, 0, 0.025), r=0.006, seg=1))
        for top, bot in ((0.7, 0.55), (0.54, 0.39), (0.38, 0.06)):
            d = s.rbox((0.43, 0.02, top - bot - 0.01), (x, -0.405, (top + bot) / 2), r=0.006, seg=2)
            shaker(d, 0.02, thickness=0.025, depth=-0.004)
            body.append(d)
            acc += bar_handle(x, top - 0.045, -0.415, 0.16, r=0.007, stand=0.016)
    body.append(s.rbox((0.88, 0.03, 0.45), (0, 0.33, 0.49), r=0.006, seg=1))
    s.part("Body", body)
    s.part("Trim", [s.rbox((1.0, 0.52, 0.006), (0, -0.02, 0.761), r=0.003, seg=1)])
    lx, ly = -0.64, 0.28
    acc.append(s.lathe([(0.0, 0.755), (0.075, 0.755), (0.075, 0.765), (0.06, 0.78), (0.0, 0.785)], seg=20,
                       loc=(lx, ly, 0)))
    acc.append(s.rcyl(0.008, 0.25, (lx, ly, 0.9), seg=10, r=0.002, bevel_seg=1))
    acc.append(s.rbox((0.3, 0.11, 0.07), (lx, ly - 0.03, 1.04), r=0.032, seg=3))
    s.part("Accent", acc)
    s.part("Eye", [s.rbox((0.26, 0.08, 0.012), (lx, ly - 0.03, 1.007), r=0.004, seg=1)])
    return export("executive_desk")


def model_filing_cabinet():
    """Four-drawer steel filing cabinet: a carcass on a recessed plinth, four drawer fronts with bar pulls and label
    holders, a lock in the top drawer."""
    body = [s.rbox((0.46, 0.555, 1.27), (0, 0.0225, 0.665), r=0.01, seg=2)]
    acc = [s.rbox((0.42, 0.5, 0.04), (0, 0.02, 0.02), r=0.006, seg=1)]
    for k in range(4):
        bot = 0.05 + k * 0.31
        top = bot + 0.296
        d = s.rbox((0.43, 0.02, top - bot), (0, -0.265, (top + bot) / 2), r=0.007, seg=2)
        body.append(d)
        acc += bar_handle(0, top - 0.06, -0.275, 0.18, r=0.008, stand=0.016)
        acc.append(frame((0.1, 0.008, 0.04), (0, -0.276, top - 0.11), 0.008, r=0.002, seg=1))
    acc.append(s.rcyl(0.012, 0.016, (0.16, -0.278, 1.25), axis="Y", seg=12, r=0.003, bevel_seg=1))
    s.part("Body", body)
    s.part("Accent", acc)
    return export("filing_cabinet")


def model_credenza():
    """Low sideboard 1.6 m on four tapered legs: a carcass with four shaker doors and vertical pulls, a wooden top."""
    body = [s.rbox((1.58, 0.44, 0.6), (0, 0.01, 0.42), r=0.01, seg=2)]
    acc = []
    for k in range(4):
        x = -0.594 + k * 0.396
        d = s.rbox((0.388, 0.02, 0.56), (x, -0.218, 0.42), r=0.006, seg=2)
        shaker(d, 0.05, thickness=0.035)
        body.append(d)
        px = x + (0.15 if k % 2 == 0 else -0.15)
        acc += bar_handle(px, 0.55, -0.228, 0.12, axis="Z", r=0.006, stand=0.014)
    for x in (-0.72, 0.72):
        for y in (-0.15, 0.17):
            acc.append(s.rcyl(0.016, 0.135, (x, y, 0.0675), seg=12, r=0.003, bevel_seg=1, radius2=0.022))
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((1.6, 0.46, 0.032), (0, 0, 0.734), r=0.008, seg=2)])
    return export("credenza")


def model_guest_chair():
    """Upholstered visitor chair: four tapered wooden legs, a seat rail, a padded seat, a reclined padded back and
    padded arm pads on wooden arm posts."""
    acc = []
    for x in (-0.24, 0.24):
        for y in (-0.23, 0.23):
            acc.append(s.rcyl(0.014, 0.38, (x, y, 0.19), seg=12, r=0.003, bevel_seg=1, radius2=0.02))
    acc.append(s.rbox((0.54, 0.52, 0.06), (0, 0, 0.38), r=0.01, seg=2))
    for sx in (-1, 1):
        x = sx * 0.265
        acc.append(s.rbox((0.04, 0.04, 0.24), (x, -0.18, 0.52), r=0.01, seg=2))
        acc.append(s.rbox((0.04, 0.04, 0.24), (x, 0.17, 0.52), r=0.01, seg=2))
        acc.append(s.rbox((0.05, 0.44, 0.04), (x, -0.005, 0.395), r=0.01, seg=2))
    s.part("Accent", acc)
    body = [s.rbox((0.5, 0.5, 0.1), (0, -0.01, 0.445), r=0.035, seg=3)]
    body.append(s.rbox((0.5, 0.08, 0.42), (0, 0.225, 0.69), r=0.035, seg=3, rot=(math.radians(-8), 0, 0)))
    for sx in (-1, 1):
        body.append(s.rbox((0.07, 0.46, 0.05), (sx * 0.265, -0.01, 0.645), r=0.022, seg=3))
    s.part("Body", body)
    return export("guest_chair")


def model_wall_clock():
    """Round wall clock hung at 2.0..2.35: a dark case with a recessed white dial, twelve hour marks, two hands at
    ten past ten and a centre cap. The back is flush with the wall (Blender +y 0.03)."""
    cz = 2.175
    case = s.rcyl(0.175, 0.056, (0, 0.002, cz), axis="Y", seg=48, r=0.01, bevel_seg=2)
    case = s.boolean(case, s.rcyl(0.152, 0.03, (0, -0.03, cz), axis="Y", seg=48, r=0), "DIFFERENCE")
    s.bevel_edges(case, 0.002, 1, angle=50)
    acc = [case]
    for k in range(12):
        a = 2 * math.pi * k / 12
        long = 0.03 if k % 3 == 0 else 0.016
        rr = 0.13 - long / 2
        acc.append(s.rbox((0.008 if k % 3 == 0 else 0.005, 0.004, long), (rr * math.sin(a), -0.0185, cz + rr * math.cos(a)),
                          r=0.0015, seg=1, rot=(0, a, 0)))
    for length, width, ang, y in ((0.085, 0.01, math.radians(-60), -0.0205), (0.12, 0.007, math.radians(60), -0.0235)):
        hand = s.rbox((width, 0.003, length + 0.02), (0, y, length / 2 - 0.01), r=0.001, seg=1)
        s.place(hand, (0, 0, cz), (0, ang, 0))
        acc.append(hand)
    acc.append(s.rcyl(0.009, 0.01, (0, -0.022, cz), axis="Y", seg=12, r=0.002, bevel_seg=1))
    s.part("Accent", acc)
    s.part("Body", [s.rcyl(0.154, 0.012, (0, -0.012, cz), axis="Y", seg=48, r=0.001, bevel_seg=1)])
    return export("wall_clock")


# --------------------------------------------------------------------------- meeting room

def boat_outline(half_len, w_mid, w_end, end_len, n=16, n_end=8):
    """Plan outline of a boat-shaped table top, counter-clockwise: widest in the middle, elliptic ends."""
    x_end = half_len - end_len
    top = []
    for k in range(n + 1):
        x = -x_end + 2 * x_end * k / n
        top.append((x, w_mid - (w_mid - w_end) * (x / x_end) ** 2))
    right = [(x_end + end_len * math.sin(math.pi * k / n_end), w_end * math.cos(math.pi * k / n_end))
             for k in range(1, n_end)]
    return _ccw(top, right)


def _ccw(top, right):
    """top edge (x rising, y > 0) + right cap -> a closed counter-clockwise loop."""
    loop = [(x, -y) for x, y in top]                    # bottom edge, x rising (y < 0)
    loop += list(reversed(right))                        # right cap from bottom up
    loop += list(reversed(top))                          # top edge, x falling
    loop += [(-x, y) for x, y in right]                  # left cap from top down
    return loop


def model_conference_table():
    """Boat-shaped meeting table 3.6 x 1.2, top at 0.75: a thick top with elliptic ends, two tapered panel legs on
    foot plates joined by a beam, a flush cable box in the middle with a cable spine down to the floor."""
    top = prism(boat_outline(1.8, 0.6, 0.42, 0.2), "Z", 0.71, 0.75, bevel=0.01, seg=2)
    top = s.boolean(top, s.rbox((0.36, 0.16, 0.03), (0, 0, 0.75), r=0), "DIFFERENCE")
    s.part("Body", [top])
    acc = []
    for x in (-1.05, 1.05):
        acc.append(s.rbox((0.1, 0.6, 0.69), (x, 0, 0.365), r=0.012, seg=2, taper=(1.0, 1.35)))
        acc.append(s.rbox((0.18, 0.84, 0.03), (x, 0, 0.015), r=0.008, seg=2))
    acc.append(s.rbox((2.0, 0.08, 0.08), (0, 0, 0.67), r=0.01, seg=1))
    box = s.rbox((0.355, 0.155, 0.12), (0, 0, 0.684), r=0.004, seg=1)
    lid = s.rbox((0.34, 0.14, 0.012), (0, 0, 0.744), r=0.003, seg=1)
    s.groove(lid, "X", 0.0, 0.004, 0.002)
    spine = [s.rbox((0.07 if k % 2 else 0.06, 0.05 if k % 2 else 0.045, 0.085), (0, 0, 0.0425 + 0.075 * k), r=0.012, seg=1)
             for k in range(9)]
    acc += [box, lid, *spine]
    s.part("Accent", acc)
    return export("conference_table")


def model_projector_screen():
    """Wall-hung pull-down projector screen: a rounded case with end caps and wall brackets at 2.48..2.6, the screen
    (white with a black border) down to a weighted bottom bar at 0.93 and a pull loop to 0.9."""
    case = s.rbox((1.96, 0.13, 0.12), (0, 0.01, 2.54), r=0.05, seg=4)
    s.part("Body", [case])
    acc = [s.rbox((0.04, 0.14, 0.13), (sx * 0.98, 0.01, 2.54), r=0.015, seg=2) for sx in (-1, 1)]
    acc += [s.rbox((0.08, 0.03, 0.1), (sx * 0.8, 0.065, 2.54), r=0.006, seg=1) for sx in (-1, 1)]
    acc.append(frame((1.84, 0.01, 1.6), (0, 0.0, 1.73), 0.04, r=0.002, seg=1))
    acc.append(s.rbox((1.9, 0.026, 0.03), (0, 0.0, 0.94), r=0.008, seg=2))
    acc.append(s.tube([(-0.03, 0.0, 0.93), (0.0, 0.0, 0.904), (0.03, 0.0, 0.93)], 0.004, segments_per=4))
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((1.78, 0.006, 1.54), (0, 0.0, 1.73), r=0.002, seg=1)])
    return export("projector_screen")


def model_flipchart():
    """Tripod flipchart easel: two front legs and a back leg hinged at the top, a board leaning back 9° with a
    paper pad, a clamp bar on top and a marker tray at the bottom."""
    k = 0.15 / 0.96  # board rake: y grows by k per metre of height
    tilt = -math.atan(k)
    n = Vector((0, -math.cos(math.atan(k)), math.sin(math.atan(k))))
    bc = Vector((0, -0.17 + (1.26 - 0.78) * k, 1.26))
    board = s.rbox((0.68, 0.025, 0.97), tuple(bc), r=0.008, seg=2, rot=(tilt, 0, 0))
    s.part("Body", [board])
    pad_c = bc + n * 0.016 + Vector((0, -0.02 * k, -0.02))
    s.part("Trim", [s.rbox((0.62, 0.012, 0.86), tuple(pad_c), r=0.003, seg=1, rot=(tilt, 0, 0))])
    acc = []
    for sx in (-1, 1):
        acc.append(rod((sx * 0.315, -0.265, 0.0), (sx * 0.315, 0.0, 1.73), 0.012, seg=10, bevel=0.003))
    acc.append(rod((0.0, 0.015, 1.72), (0.0, 0.285, 0.0), 0.012, seg=10, bevel=0.003))
    top_c = bc + Vector((0, 0.47 * k, 0.47)) + n * 0.02
    acc.append(s.rbox((0.66, 0.035, 0.045), tuple(top_c), r=0.01, seg=2, rot=(tilt, 0, 0)))
    acc.append(s.rbox((0.7, 0.05, 0.05), (0, 0.012, 1.75), r=0.012, seg=2))
    tray_c = bc + Vector((0, -0.47 * k, -0.47)) + n * 0.03
    acc.append(s.rbox((0.68, 0.07, 0.025), tuple(tray_c), r=0.006, seg=1))
    acc.append(s.rbox((0.68, 0.008, 0.04), tuple(tray_c + Vector((0, -0.032, 0.015))), r=0.003, seg=1))
    s.part("Accent", acc)
    return export("flipchart")


def model_av_credenza():
    """Low AV cabinet: a carcass on a dark plinth with two doors and an open centre bay holding two rack units with
    status LEDs, a wooden top, a round conference speakerphone with a light ring on top."""
    carcass = s.rbox((1.58, 0.47, 0.66), (0, 0.005, 0.4), r=0.01, seg=2)
    carcass = s.boolean(carcass, s.rbox((0.52, 0.44, 0.48), (0, -0.2, 0.42), r=0), "DIFFERENCE")
    s.bevel_edges(carcass, 0.004, 1, angle=50)
    body = [carcass]
    acc = [s.rbox((1.5, 0.4, 0.08), (0, 0, 0.04), r=0.006, seg=1)]
    for sx in (-1, 1):
        d = s.rbox((0.5, 0.02, 0.6), (sx * 0.52, -0.235, 0.41), r=0.006, seg=2)
        shaker(d, 0.05, thickness=0.035)
        body.append(d)
        acc += bar_handle(sx * 0.3, 0.41, -0.245, 0.14, axis="Z", r=0.006, stand=0.014)
    eye = []
    for z, h, w in ((0.212, 0.09, 0.46), (0.292, 0.08, 0.44)):
        acc.append(s.rbox((w, 0.22, h), (0, -0.09, z), r=0.006, seg=1))
        for k in range(3):
            eye.append(s.rbox((0.012, 0.006, 0.012), (0.14 + k * 0.025, -0.2015, z), r=0.002, seg=1))
    # speakerphone
    px, py = 0.4, 0.0
    acc.append(s.lathe([(0.0, 0.745), (0.115, 0.745), (0.126, 0.752), (0.122, 0.772), (0.085, 0.792), (0.04, 0.798),
                        (0.0, 0.799)], seg=32, loc=(px, py, 0)))
    eye.append(s.place(revolve(rounded_rect(0.088, 0.104, 0.776, 0.79, 0.003, n=1), seg=32), (px, py, 0)))
    eye.append(s.rcyl(0.022, 0.006, (px, py, 0.799), seg=16, r=0.002, bevel_seg=1))
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Trim", [s.rbox((1.6, 0.48, 0.03), (0, 0.005, 0.735), r=0.008, seg=2)])
    s.part("Eye", eye)
    return export("av_credenza")


def model_meeting_chair():
    """High-back office chair: a five-star base on casters, a gas column with a shroud, a seat mechanism, a contoured
    seat, a curved high back on a spine and T-arms."""
    acc = [s.rcyl(0.045, 0.06, (0, 0, 0.1), seg=16, r=0.008, bevel_seg=1)]
    rubber = []
    for k in range(5):
        a = -math.pi / 2 + 2 * math.pi * k / 5
        c, sn = math.cos(a), math.sin(a)
        acc.append(s.rbox((0.23, 0.04, 0.03), (0.13 * c, 0.13 * sn, 0.095), r=0.01, seg=2, rot=(0, 0, a),
                          taper=(1.0, 0.8)))
        st, wh = caster(0.245 * c, 0.245 * sn, 0.085, wheel_r=0.026, axis="X")
        s.place(wh[0], (-0.245 * c, -0.245 * sn, 0))
        s.place(wh[0], (0.245 * c, 0.245 * sn, 0), (0, 0, a + math.pi / 2))
        acc += st
        rubber += wh
    acc.append(s.rcyl(0.026, 0.3, (0, 0, 0.26), seg=16, r=0.003, bevel_seg=1))
    acc.append(s.rcyl(0.034, 0.16, (0, 0, 0.19), seg=16, r=0.006, bevel_seg=1, radius2=0.03))
    acc.append(s.rbox((0.22, 0.24, 0.05), (0, 0.0, 0.405), r=0.012, seg=2))
    acc.append(s.tube([(0, 0.1, 0.415), (0, 0.22, 0.44), (0, 0.268, 0.55), (0, 0.262, 0.76)], 0.02, segments_per=6))
    for sx in (-1, 1):
        acc.append(s.rbox((0.14, 0.04, 0.022), (sx * 0.17, 0.0, 0.42), r=0.008, seg=1))
        acc.append(s.rbox((0.026, 0.05, 0.25), (sx * 0.245, 0.0, 0.53), r=0.01, seg=2))
        acc.append(s.rbox((0.06, 0.26, 0.03), (sx * 0.252, -0.01, 0.665), r=0.013, seg=2))
    s.part("Accent", acc)
    s.part("Trim", rubber)
    seat = s.loft([(0.42, 0.23, 0.22, 4, 0, -0.01), (0.47, 0.25, 0.245, 4, 0, -0.01), (0.505, 0.24, 0.235, 4, 0, -0.01)],
                  seg=32, round_caps=0.02)
    back = s.loft([(0.56, 0.19, 0.03, 4, 0, 0.248), (0.7, 0.21, 0.04, 4, 0, 0.232), (0.9, 0.23, 0.035, 4, 0, 0.242),
                   (1.05, 0.21, 0.03, 4, 0, 0.262)], seg=32, round_caps=0.025)
    s.groove(back, "Z", 0.8, 0.006, 0.003)
    s.part("Body", [seat, back])
    return export("meeting_chair")


MODELS = {
    "info_kiosk": model_info_kiosk,
    "security_turnstile": model_security_turnstile,
    "round_bench_planter": model_round_bench_planter,
    "logo_totem": model_logo_totem,
    "queue_barrier": model_queue_barrier,
    "standing_desk": model_standing_desk,
    "planter_divider": model_planter_divider,
    "mobile_whiteboard": model_mobile_whiteboard,
    "file_pedestal": model_file_pedestal,
    "sofa_pod": model_sofa_pod,
    "executive_desk": model_executive_desk,
    "filing_cabinet": model_filing_cabinet,
    "credenza": model_credenza,
    "guest_chair": model_guest_chair,
    "wall_clock": model_wall_clock,
    "conference_table": model_conference_table,
    "projector_screen": model_projector_screen,
    "flipchart": model_flipchart,
    "av_credenza": model_av_credenza,
    "meeting_chair": model_meeting_chair,
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
