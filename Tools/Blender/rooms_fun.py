"""Furniture for the fun rooms: recording studio (mixing desk, mic stand, acoustic foam, drum kit), game room (pool
table, foosball, dart board cabinet, bar counter, bar stool) and the electronics lab (lab bench, small-parts cabinet,
industrial robot arm).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/rooms_fun.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--focus x,y,z,r] [--shading]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back is at Blender +Y, z up, origin on the
floor in the middle of the footprint. Wall-hung pieces (acoustic panel, dart board) keep their real height and a back
face flush with the footprint's back. Object names are material roles: Body, Accent, Trim, Eye (glows).
Forms come from shapes.py and are finished by shapes.part; export() goes through meshfix (floor=True) like poi.export.
Attached pieces sink E = 5 mm into what carries them: no two faces share a plane (z-fighting), nothing floats.
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --)
import meshfix  # noqa: E402
import poi  # noqa: E402  (ring)
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


def rod(p0, p1, r, seg=8, bevel=0.0):
    """Straight round bar between two points (stand legs, rods, spokes)."""
    L = (Vector(p1) - Vector(p0)).length
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=L)
    if bevel:
        s.bevel_edges(bm, min(bevel, 0.4 * r), 1, angle=50)
    bmesh.ops.translate(bm, vec=Vector((0, 0, L / 2)), verts=bm.verts)
    return orient(bm, p0, p1)


def link(p0, p1, a0, b0, a1, b1, n=3.0, seg=20, caps=0.02):
    """Lofted beam from p0 to p1: half-sizes (a across X, b across the other side) change from one end to the other."""
    L = (Vector(p1) - Vector(p0)).length
    bm = s.loft([(0, a0, b0, n), (L * 0.5, (a0 + a1) / 2 * 1.04, (b0 + b1) / 2 * 1.04, n), (L, a1, b1, n)],
                seg=seg, round_caps=caps)
    return orient(bm, p0, p1)


def prism(pts, axis, lo, hi, bevel=0.0):
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
        s.bevel_edges(bm, bevel, 1, angle=30)
    return bm


def torus(R, r, loc, seg=24, rseg=8):
    """Ring of round section around Z (foot ring, hoop)."""
    bm = bmesh.new()
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        c = Vector((math.cos(a), math.sin(a), 0))
        rings.append([bm.verts.new(c * (R + r * math.cos(2 * math.pi * j / rseg))
                                   + Vector((0, 0, r * math.sin(2 * math.pi * j / rseg)))) for j in range(rseg)])
    for i in range(seg):
        A, B = rings[i], rings[(i + 1) % seg]
        for j in range(rseg):
            bm.faces.new((A[j], B[j], B[(j + 1) % rseg], A[(j + 1) % rseg]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return s.place(bm, loc)


def gem(radius, loc, sub=2):
    """Cheap sphere (icosphere): pool balls, beads."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=radius)
    return s.place(bm, loc)


def tray(size, loc, wall=0.008):
    """Open box (parts tray, bin): a rounded box with its inside cut out from the top."""
    outer = s.rbox(size, loc, r=0.004, seg=1)
    cut = s.rbox((size[0] - 2 * wall, size[1] - 2 * wall, size[2]), (loc[0], loc[1], loc[2] + wall), r=0.003, seg=1)
    return s.boolean(outer, cut, "DIFFERENCE")


def merge(bms):
    """One bmesh holding all the given ones (a multi-island boolean cutter)."""
    import bpy
    out = bmesh.new()
    for b in bms:
        me = bpy.data.meshes.new("_m")
        b.to_mesh(me)
        b.free()
        out.from_mesh(me)
        bpy.data.meshes.remove(me)
    return out


def cut_box(outer, holes, bevel=0.004):
    """outer minus each hole bmesh in turn (exact boolean), cut edges chamfered."""
    bm = outer
    for h in holes:
        bm = s.boolean(bm, h, "DIFFERENCE")
    if bevel:
        s.bevel_edges(bm, bevel, 1, angle=50)
    return bm


# --------------------------------------------------------------------------- recording studio

SLOPE = math.atan2(0.08, 0.43)  # console face: from (y -0.33, z 0.78) up to (y 0.10, z 0.86)


def on_slope(x, y, h):
    """Centre of a piece of height h standing on the console face at (x, y), sunk E into it."""
    z = 0.78 + (y + 0.33) * math.tan(SLOPE)
    n = Vector((0, -math.sin(SLOPE), math.cos(SLOPE)))
    return Vector((x, y, z)) + n * (h / 2 - E)


def model_mixing_desk():
    """Studio desk 1.6 x 0.8, top at 0.75: two pedestals with rack gear, a slanted console (12 channel strips of a
    fader and two knobs), a meter bridge, a screen behind it and two monitor speakers on stands at the ends."""
    top = s.rbox((1.6, 0.8, 0.045), (0, 0, 0.7275), r=0.012, seg=2)
    peds = [s.rbox((0.36, 0.74, 0.71), (x * 0.6, 0, 0.355), r=0.012, seg=2, vertical_only=False) for x in (-1, 1)]
    for p in peds:
        s.groove(p, "Z", 0.07, 0.008, 0.004)
    modesty = s.rbox((0.9, 0.03, 0.42), (0, 0.33, 0.5), r=0.008, seg=1)
    s.part("Body", [top, *peds, modesty])

    acc, eye = [], []
    console = prism([(-0.33, 0.745), (-0.34, 0.765), (-0.33, 0.78), (0.1, 0.86), (0.1, 0.745)], "X", -0.5, 0.5,
                    bevel=0.008)
    bridge = s.rbox((0.98, 0.1, 0.2), (0, 0.14, 0.845), r=0.012, seg=2)
    acc += [console, bridge]
    for k in range(12):
        x = -0.4125 + k * 0.075
        fy = -0.27 + 0.045 * math.sin(k * 1.7)  # fader positions differ from strip to strip
        acc.append(s.rbox((0.006, 0.14, 0.006), on_slope(x, -0.23, 0.006), r=0, rot=(SLOPE, 0, 0)))
        acc.append(s.rbox((0.032, 0.018, 0.024), on_slope(x, fy, 0.024), r=0, rot=(SLOPE, 0, 0)))
        for y in (-0.11, -0.04):
            acc.append(s.rcyl(0.011, 0.02, on_slope(x, y, 0.02), seg=6, r=0, rot=(SLOPE, 0, 0)))
        eye.append(s.rbox((0.014, 0.008, 0.06 + 0.02 * math.sin(k * 2.3)), (x, 0.0885, 0.905), r=0))
    # monitor behind the bridge
    acc += [s.rbox((0.22, 0.14, 0.016), (0, 0.32, 0.754), r=0.005, seg=1),
            s.rbox((0.045, 0.03, 0.14), (0, 0.33, 0.82), r=0.008, seg=1),
            s.rbox((0.56, 0.032, 0.3), (0, 0.31, 1.0), r=0.01, seg=2)]
    eye.append(s.rbox((0.52, 0.008, 0.26), (0, 0.2925, 1.0), r=0.002, seg=1))
    # outboard rack gear in the pedestals
    for xs in (-1, 1):
        for z in (0.58, 0.43, 0.28):
            acc.append(s.rbox((0.3, 0.02, 0.12), (xs * 0.6, -0.37, z), r=0.004, seg=1))
            eye += [s.rbox((0.014, 0.008, 0.014), (xs * 0.6 + dx, -0.38, z + 0.02), r=0) for dx in (-0.11, -0.08)]
    # monitor speakers on desk stands, turned to the listener
    for xs in (-1, 1):
        spk = [s.rbox((0.13, 0.13, 0.012), (0, 0, 0.752), r=0.004, seg=1),
               s.rcyl(0.016, 0.11, (0, 0, 0.8), seg=8, r=0),
               s.rbox((0.2, 0.24, 0.3), (0, 0, 1.0), r=0.02, seg=2),
               poi.ring(0.075, 0.058, 0.012, (0, -0.122, 0.95), axis="Y", seg=16, r=0),
               s.lathe([(0.06, 0.0), (0.04, 0.008), (0.016, 0.014), (0.0, 0.02)], seg=12,
                       axis="-Y", loc=(0, -0.118, 0.95)),
               s.lathe([(0.03, 0.0), (0.03, 0.006), (0.018, 0.01), (0.0, 0.014)], seg=12,
                       axis="-Y", loc=(0, -0.118, 1.08))]
        for b in spk:
            s.place(b, (xs * 0.64, 0.17, 0), (0, 0, -xs * 0.35))
        acc += spk
    s.part("Accent", acc)
    s.part("Eye", eye)
    return export("mixing_desk")


def model_mic_stand():
    """Boom microphone stand 1.6 high: heavy round base, telescopic pole with a clutch, boom with a counterweight,
    an upright studio mic in a clip and a round pop filter on a gooseneck in front of it."""
    J = Vector((0, 0, 1.33))  # boom joint
    back, front = J + Vector((0, 0.17, -0.136)), J + Vector((0, -0.15, 0.12))
    body = [s.lathe([(0.0, 0.0), (0.235, 0.0), (0.235, 0.012), (0.222, 0.026), (0.07, 0.04), (0.035, 0.055),
                     (0.0, 0.056)], seg=32),
            s.rcyl(0.015, 0.8, (0, 0, 0.44), seg=12, r=0.002),
            s.rcyl(0.022, 0.05, (0, 0, 0.84), seg=12, r=0.004),
            s.rcyl(0.011, 0.5, (0, 0, 1.08), seg=10, r=0),
            s.rcyl(0.024, 0.06, J, axis="X", seg=12, r=0.004),
            s.rcyl(0.018, 0.02, J + Vector((0.038, 0, 0)), axis="X", seg=10, r=0.004),
            rod(back, front, 0.009, seg=10),
            rod(back + (back - J).normalized() * -0.01, back + (back - J).normalized() * 0.07, 0.03, seg=12,
                bevel=0.006),
            s.rbox((0.03, 0.05, 0.04), front + Vector((0, -0.012, 0.012)), r=0.006, seg=1),
            s.lathe([(0.0, 1.405), (0.018, 1.405), (0.026, 1.42), (0.026, 1.52), (0.0, 1.52)], seg=16,
                    loc=(0, -0.172, 0)),
            s.tube([(0, -0.06, 1.378), (0.03, -0.14, 1.392), (0.03, -0.215, 1.42), (0, -0.245, 1.449)], 0.005,
                   segments_per=6)]
    s.part("Body", body)
    grille = s.lathe([(0.0, 1.512), (0.028, 1.512), (0.03, 1.53), (0.031, 1.555), (0.028, 1.58), (0.018, 1.596),
                      (0.0, 1.601)], seg=16, loc=(0, -0.172, 0))
    s.groove(grille, "Z", 1.555, 0.004, 0.002)
    s.part("Accent", [grille])
    pop = [poi.ring(0.07, 0.061, 0.012, (0, -0.242, 1.515), axis="Y", seg=24, r=0.002),
           s.rcyl(0.064, 0.003, (0, -0.242, 1.515), axis="Y", seg=24, r=0)]
    s.part("Trim", pop)
    return export("mic_stand")


def model_acoustic_panel():
    """Wall of acoustic foam 1.2 x 1.2 (y 0.9..2.1), 8 cm deep: a 3 x 3 grid of wedge foam tiles in a thin frame,
    the wedges of neighbouring tiles turned 90 degrees (checkerboard), the back flush with the wall."""
    outer = s.rbox((1.2, 0.06, 1.2), (0, 0.01, 1.5), r=0.008, seg=2)
    hole = s.rbox((1.12, 0.2, 1.12), (0, 0.01, 1.5), r=0)
    frame = cut_box(outer, [hole], bevel=0.003)
    board = s.rbox((1.13, 0.01, 1.13), (0, 0.034, 1.5), r=0.002, seg=1)
    s.part("Accent", [frame, board])

    foam = []
    pitch, tile, w, n = 0.373, 0.366, 0.072, 5
    for i in range(3):
        for j in range(3):
            xc, zc = (i - 1) * pitch, 1.5 + (j - 1) * pitch
            foam.append(s.rbox((tile, 0.022, tile), (xc, 0.02, zc), r=0.004, seg=1))
            half = (n * w) / 2
            for k in range(n):
                c = -half + w / 2 + k * w
                if (i + j) % 2 == 0:  # ridges run across (along X)
                    prof = [(0.012, -w / 2 + 0.0008), (-0.031, -0.009), (-0.04, -0.003), (-0.04, 0.003), (-0.031, 0.009),
                            (0.012, w / 2 - 0.0008)]
                    foam.append(prism([(y, zc + c + v) for y, v in prof], "X", xc - 0.18, xc + 0.18))
                else:  # ridges run up (along Z)
                    prof = [(-w / 2 + 0.0008, 0.012), (-0.009, -0.031), (-0.003, -0.04), (0.003, -0.04), (0.009, -0.031),
                            (w / 2 - 0.0008, 0.012)]
                    foam.append(prism([(xc + c + u, y) for u, y in prof], "Z", zc - 0.18, zc + 0.18))
    s.part("Body", foam)
    return export("acoustic_panel")


def drum(center, r, depth, tilt=0.0, hoops=(1,), seg=16, n_lugs=4):
    """Drum shell (axis Z, tilted about X towards +Y) and its metal hoops: (shell bm, [hoop bms])."""
    shell = s.rcyl(r, depth, (0, 0, 0), seg=seg, r=0.004, bevel_seg=1)
    rings = [poi.ring(r + 0.01, r - 0.008, 0.022, (0, 0, side * (depth / 2 - 0.004)), axis="Z", seg=seg, r=0)
             for side in hoops]
    lugs = [s.rbox((0.014, 0.02, depth * 0.45), (math.cos(a) * r, math.sin(a) * r, 0), r=0,
                   rot=(0, 0, a + math.pi / 2)) for a in [2 * math.pi * k / n_lugs + 0.3 for k in range(n_lugs)]]
    for b in [shell, *rings, *lugs]:
        s.place(b, center, (tilt, 0, 0))
    return shell, rings + lugs


def tripod(x, y, z_top, spread, z_hub, r=0.008, start=0.0):
    """Stand post from the floor hub up to z_top and three legs from the hub out to the floor."""
    parts = [rod((x, y, z_hub - 0.02), (x, y, z_top), 0.011, seg=6)]
    for k in range(3):
        a = start + 2 * math.pi * k / 3
        fx, fy = x + spread * math.cos(a), y + spread * math.sin(a)
        parts += [rod((x, y, z_hub), (fx, fy, 0.008), r, seg=5), foot(fx, fy)]
    return parts


def foot(x, y):
    """Rubber foot of a stand leg: a flat disc on the floor (all legs end in one, so they all share z = 0)."""
    return s.rcyl(0.014, 0.012, (x, y, 0.006), seg=5, r=0)


def cymbal(center, r, tilt=(0.0, 0.0), flip=False):
    z = 0.0
    prof = [(0.0, z - 0.004), (0.04, z - 0.006), (r, z - 0.026), (r, z - 0.021), (0.04, z + 0.002),
            (0.03, z + 0.012), (0.0, z + 0.014)]
    bm = s.lathe(prof, seg=16)
    if flip:
        s.place(bm, rot=(math.pi, 0, 0))
    return s.place(bm, center, (tilt[0], tilt[1], 0))


def model_drum_kit():
    """Compact drum kit 1.6 x 1.3, 1.2 high: bass drum facing +Z (Blender -Y) with two rack toms on it, snare,
    floor tom, hi-hat, crash cymbal on a boom stand, bass pedal and a stool behind (-Z)."""
    shells, metal, cymbals = [], [], []
    sh, hw = drum((0, -0.35, 0.272), 0.26, 0.36, tilt=math.pi / 2, hoops=(1, -1), seg=20)  # axis along Y
    shells.append(sh)
    metal += hw
    for x, z, r in ((-0.14, 0.67, 0.11), (0.15, 0.69, 0.12)):
        sh, hw = drum((x, -0.29, z), r, 0.16, tilt=-0.4, hoops=(1,), seg=16, n_lugs=0)
        shells.append(sh)
        metal += hw
        metal.append(rod((0, -0.32, 0.5), (x * 0.8, -0.3, z - 0.05), 0.01, seg=6))
    sh, hw = drum((-0.3, 0.06, 0.6), 0.17, 0.13, tilt=-0.12, hoops=(1,), seg=16, n_lugs=0)
    shells.append(sh)
    metal += hw + tripod(-0.3, 0.06, 0.54, 0.2, 0.18, start=0.5)
    sh, hw = drum((0.42, 0.13, 0.47), 0.19, 0.33, hoops=(1,), seg=16)
    shells.append(sh)
    metal += hw
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.4
        fx, fy = 0.42 + 0.26 * math.cos(a), 0.13 + 0.26 * math.sin(a)
        metal += [rod((0.42 + 0.18 * math.cos(a), 0.13 + 0.18 * math.sin(a), 0.55), (fx, fy, 0.008), 0.008, seg=6),
                  foot(fx, fy)]
    # bass drum spurs and pedal
    for xs in (-1, 1):
        metal += [rod((xs * 0.2, -0.47, 0.12), (xs * 0.32, -0.625, 0.008), 0.009, seg=6), foot(xs * 0.32, -0.625)]
    metal += [s.rbox((0.09, 0.26, 0.02), (0, -0.03, 0.01), r=0.004, seg=1),
              s.rbox((0.12, 0.03, 0.19), (0, -0.155, 0.11), r=0.006, seg=1),
              rod((0, -0.15, 0.16), (0, -0.19, 0.3), 0.006, seg=6),
              s.ball(0.025, (0, -0.19, 0.3), seg=8)]
    # hi-hat
    metal += tripod(-0.62, -0.06, 0.92, 0.2, 0.2, start=0.2)
    metal.append(s.rbox((0.08, 0.22, 0.02), (-0.62, 0.1, 0.01), r=0.004, seg=1))
    metal.append(rod((-0.62, 0.0, 0.015), (-0.62, -0.06, 0.2), 0.006, seg=6))  # pedal pull rod
    cymbals += [cymbal((-0.62, -0.06, 0.86), 0.17), cymbal((-0.62, -0.06, 0.888), 0.17, flip=True)]
    # crash on a boom stand
    metal += tripod(0.55, -0.3, 0.98, 0.22, 0.22, start=1.0)
    metal.append(rod((0.55, -0.3, 0.97), (0.6, -0.4, 1.13), 0.008, seg=6))
    metal.append(s.rcyl(0.016, 0.04, (0.55, -0.3, 0.975), seg=8, r=0.003))
    cymbals.append(cymbal((0.6, -0.4, 1.145), 0.2, tilt=(-0.3, 0.15)))
    # stool behind
    shells.append(s.lathe([(0.0, 0.48), (0.15, 0.48), (0.165, 0.5), (0.16, 0.535), (0.12, 0.55), (0.0, 0.553)],
                          seg=12, loc=(0, 0.47, 0)))
    metal += tripod(0, 0.47, 0.49, 0.18, 0.12, start=math.pi / 2)
    s.part("Body", shells)
    s.part("Accent", metal)
    s.part("Trim", cymbals)
    return export("drum_kit")


# --------------------------------------------------------------------------- game room

def model_pool_table():
    """Pool table 2.5 x 1.4, felt at 0.8: chunky wooden rails with six pockets and sights, green bed and cushions,
    deep apron on four thick turned legs, a few balls and a cue lying on the cloth."""
    pockets = [(x, y) for x in (-1.13, 1.13) for y in (-0.58, 0.58)] + [(0, -0.6), (0, 0.6)]

    def cutters(r=0.064):
        return merge([s.rcyl(r, 0.4, (x, y, 0.8), seg=16, r=0) for x, y in pockets])

    rails = s.rbox((2.5, 1.4, 0.13), (0, 0, 0.785), r=0.03, seg=2)
    rails = cut_box(rails, [s.rbox((2.26, 1.16, 0.4), (0, 0, 0.8), r=0), cutters()], bevel=0.004)
    apron = s.rbox((2.32, 1.22, 0.185), (0, 0, 0.6525), r=0.015, seg=2)
    s.groove(apron, "Z", 0.6, 0.01, 0.005)
    legs = []
    for x in (-0.98, 0.98):
        for y in (-0.48, 0.48):
            leg = s.rbox((0.16, 0.16, 0.5), (x, y, 0.315), r=0.02, seg=1)
            s.taper(leg, "Z", 0.78, 1.0, center=(x, y))
            legs += [leg, s.rbox((0.2, 0.2, 0.075), (x, y, 0.0375), r=0.015, seg=1),
                     s.rbox((0.19, 0.19, 0.04), (x, y, 0.55), r=0.01, seg=1)]
    # cue lying on the cloth (butt bottom and tip bottom both on the felt)
    p0, p1 = Vector((-0.95, -0.28, 0.8 + 0.015 - 0.003)), Vector((0.45, 0.18, 0.8 + 0.006 - 0.003))
    L = (p1 - p0).length
    cue = orient(s.lathe([(0.0, 0.0), (0.015, 0.0), (0.015, 0.35), (0.012, 0.5), (0.007, L - 0.02),
                          (0.006, L - 0.004), (0.0, L)], seg=10), p0, p1)
    s.part("Body", [rails, apron, *legs, cue])

    felt = cut_box(s.rbox((2.28, 1.18, 0.06), (0, 0, 0.77), r=0.004, seg=1), [cutters(0.062)], bevel=0.003)
    cush = []
    for ys in (-1, 1):
        prof = [(0.59, 0.796), (0.545, 0.81), (0.54, 0.822), (0.548, 0.832), (0.59, 0.84)]
        for x0, x1 in ((-1.055, -0.08), (0.08, 1.055)):
            cush.append(prism([(ys * y, z) for y, z in prof], "X", x0, x1))
    for xs in (-1, 1):
        prof = [(1.14, 0.796), (1.095, 0.81), (1.09, 0.822), (1.098, 0.832), (1.14, 0.84)]
        cush.append(prism([(xs * x, z) for x, z in prof], "Y", -0.505, 0.505))
    s.part("Accent", [felt, *cush])

    balls = [gem(0.028, (0.55, 0.0, 0.824)),
             gem(0.028, (0.6, -0.03, 0.824)), gem(0.028, (0.6, 0.03, 0.824)),
             gem(0.028, (0.65, -0.06, 0.824)), gem(0.028, (0.65, 0.0, 0.824)), gem(0.028, (0.65, 0.06, 0.824)),
             gem(0.028, (-0.55, 0.12, 0.824)), gem(0.028, (0.1, -0.35, 0.824))]
    sights = [s.rcyl(0.009, 0.006, (x * xs, ys * 0.64, 0.85), seg=6, r=0) for x in (0.28, 0.565, 0.85)
              for xs in (-1, 1) for ys in (-1, 1)]
    s.part("Trim", balls + sights)
    return export("pool_table")


def player_figure(x, y):
    """Little foosball man hung on a rod at z 0.865: feet, legs block, chest, head."""
    return s.loft([(0.776, 0.011, 0.018, 2.5), (0.81, 0.013, 0.012, 2.5), (0.872, 0.025, 0.014, 3),
                   (0.888, 0.01, 0.01, 2), (0.9, 0.014, 0.014, 2), (0.918, 0.0, 0.0)], seg=6, loc=(x, y, 0))


def model_foosball():
    """Foosball table: wooden box 1.4 x 0.52 on four legs with stretchers, eight rods across (along Z) with their
    teams of little players, wooden handles on one side per rod (alternating), bumpers and score rails."""
    box = s.rbox((1.4, 0.52, 0.22), (0, 0, 0.82), r=0.015, seg=2)
    box = cut_box(box, [s.rbox((1.32, 0.44, 0.4), (0, 0, 0.96), r=0.01, seg=1),
                        s.rbox((1.6, 0.16, 0.06), (0, 0, 0.795), r=0)], bevel=0.004)
    legs = []
    for x in (-0.6, 0.6):
        for y in (-0.19, 0.19):
            leg = s.rbox((0.08, 0.08, 0.72), (x, y, 0.36 + 0.005), r=0.012, seg=1)
            s.taper(leg, "Z", 0.7, 1.0, center=(x, y))
            legs += [leg, s.rcyl(0.028, 0.02, (x, y, 0.01), seg=8, r=0)]
        legs.append(s.rbox((0.05, 0.36, 0.06), (x, 0, 0.17), r=0.008, seg=1))
    legs.append(s.rbox((1.16, 0.05, 0.06), (0, 0, 0.17), r=0.008, seg=1))

    rods, handles, men = [], [], []
    layout = [(-0.6, 1, -1), (-0.43, 2, -1), (-0.26, 3, 1), (-0.09, 3, -1),
              (0.09, 3, 1), (0.26, 3, -1), (0.43, 2, 1), (0.6, 1, 1)]  # x, players, handle side (-1 = front)
    for x, n, side in layout:
        rods.append(rod((x, -0.33, 0.865), (x, 0.33, 0.865), 0.007, seg=8))
        rods.append(s.rcyl(0.014, 0.025, (x, -side * 0.3, 0.865), axis="Y", seg=8, r=0))  # bumper
        grip = s.lathe([(0.0, 0.0), (0.013, 0.0), (0.018, 0.03), (0.016, 0.08), (0.0, 0.087)], seg=8)
        handles.append(orient(grip, (x, side * 0.288, 0.865), (x, side * 0.4, 0.865)))
        ys = [0.0] if n == 1 else ([-0.1, 0.1] if n == 2 else [-0.14, 0.0, 0.14])
        men += [player_figure(x, y) for y in ys]
    # score rails with beads on the end walls
    for xs in (-1, 1):
        x = xs * 0.68
        rods += [rod((x, -0.16, 0.95), (x, 0.16, 0.95), 0.004, seg=6),
                 s.rbox((0.02, 0.012, 0.03), (x, -0.165, 0.94), r=0.003, seg=1),
                 s.rbox((0.02, 0.012, 0.03), (x, 0.165, 0.94), r=0.003, seg=1)]
        men += [gem(0.009, (x, -0.13 + k * 0.022 + (0.1 if k > 2 else 0), 0.95), sub=1) for k in range(6)]
    s.part("Body", [box, *legs, *handles])
    s.part("Accent", rods)
    s.part("Trim", men)
    return export("foosball")


def arc_piece(r0, r1, a0, a1, y0, y1, cz, n=3):
    """Annular sector in the XZ plane around (0, cz), from y0 to y1 (dart board ring segments)."""
    pts = [(r1 * math.cos(a0 + (a1 - a0) * k / n), cz + r1 * math.sin(a0 + (a1 - a0) * k / n)) for k in range(n + 1)]
    pts += [(r0 * math.cos(a1 - (a1 - a0) * k / n), cz + r0 * math.sin(a1 - (a1 - a0) * k / n)) for k in range(n + 1)]
    return prism(pts, "Y", y0, y1)


def model_dart_board():
    """Dartboard in an open wall cabinet: board centre at 1.73, the two doors swung open flat to the sides with
    chalk score panels inside, three darts in the board. Back flush with the wall at Blender +Y 0.04."""
    zc = 1.73
    cab = cut_box(s.rbox((0.32, 0.074, 0.38), (0, 0.003, zc), r=0.008, seg=2),
                  [s.rbox((0.28, 0.1, 0.34), (0, -0.025, zc), r=0.004, seg=1)], bevel=0.003)
    doors = [s.rbox((0.145, 0.024, 0.38), (xs * 0.2275, -0.028, zc), r=0.006, seg=2) for xs in (-1, 1)]
    s.part("Body", [cab, *doors])

    trim = [s.rcyl(0.135, 0.03, (0, 0.015, zc), axis="Y", seg=40, r=0.004)]
    trim += [s.rbox((0.1, 0.006, 0.3), (xs * 0.23, -0.04, zc), r=0.002, seg=1) for xs in (-1, 1)]
    trim += [s.rcyl(0.006, 0.05, (xs * 0.158, -0.037, zc + dz), seg=8, r=0) for xs in (-1, 1) for dz in (-0.12, 0.12)]
    s.part("Trim", trim)

    acc = []
    y0, y1 = -0.0025, 0.002
    st = 2 * math.pi / 20
    for k in range(20):
        a0 = math.pi / 2 - st / 2 + k * st + 0.004
        a1 = a0 + st - 0.008
        if k % 2:
            acc += [arc_piece(0.0125, 0.0635, a0, a1, y0, y1, zc), arc_piece(0.0725, 0.1065, a0, a1, y0, y1, zc)]
        else:
            acc += [arc_piece(0.064, 0.072, a0, a1, y0, y1, zc, n=2), arc_piece(0.107, 0.115, a0, a1, y0, y1, zc, n=2)]
    acc.append(s.rcyl(0.005, 0.005, (0, -0.001, zc), axis="Y", seg=12, r=0))
    # darts: barrel and flights sticking out of the board
    for x, z in ((0.03, zc + 0.05), (-0.05, zc - 0.02), (0.015, zc - 0.07)):
        acc.append(rod((x, 0.003, z), (x - 0.003, -0.05, z + 0.008), 0.004, seg=6))
        acc += [s.rbox((0.022, 0.022, 0.002), (x - 0.003, -0.045, z + 0.007), r=0),
                s.rbox((0.002, 0.022, 0.022), (x - 0.003, -0.045, z + 0.007), r=0)]
    s.part("Accent", acc)
    return export("dart_board")


def model_bar_counter():
    """Small bar counter 2.0 x 0.6, top at 1.05: panelled front and ends, a recessed plinth, a dark top overhanging
    the front, a brass foot rail on three brackets curving back into the panel at both ends."""
    carcass = s.rbox((1.94, 0.48, 0.91), (0, 0.05, 0.555), r=0.01, seg=2)
    plinth = s.rbox((1.88, 0.42, 0.105), (0, 0.07, 0.0525), r=0.006, seg=1)
    body = [carcass, plinth, s.rbox((1.96, 0.05, 0.05), (0, -0.2, 0.985), r=0.012, seg=2)]
    for x in (-0.72, -0.24, 0.24, 0.72):
        body += [s.rbox((0.42, 0.03, 0.64), (x, -0.2, 0.56), r=0.01, seg=2),
                 s.rbox((0.32, 0.02, 0.52), (x, -0.215, 0.56), r=0.012, seg=2, taper=(1.0, 1.0))]
    for xs in (-1, 1):
        body += [s.rbox((0.03, 0.36, 0.64), (xs * 0.975, 0.06, 0.56), r=0.01, seg=2),
                 s.rbox((0.02, 0.27, 0.52), (xs * 0.99, 0.06, 0.56), r=0.01, seg=2)]
    body += [s.rbox((1.95, 0.025, 0.06), (0, -0.2, 0.17), r=0.008, seg=1)]
    s.part("Body", body)

    top = s.rbox((2.0, 0.6, 0.05), (0, 0, 1.025), r=0.015, seg=3)
    s.part("Trim", [top])

    pts = [(-0.95, -0.185)]
    rr, cy = 0.06, -0.21
    for k in range(7):  # left bend
        a = math.pi + (math.pi / 2) * k / 6
        pts.append((-0.89 + rr * math.cos(a), cy + rr * math.sin(a)))
    for k in range(7):  # right bend
        a = 1.5 * math.pi + (math.pi / 2) * k / 6
        pts.append((0.89 + rr * math.cos(a), cy + rr * math.sin(a)))
    pts.append((0.95, -0.185))
    rail = s.tube([(x, y, 0.2) for x, y in pts], 0.024, smooth=False, res=2)
    acc = [rail]
    for x in (-0.48, 0.0, 0.48):
        acc += [rod((x, -0.27, 0.2), (x, -0.215, 0.2), 0.011, seg=8),
                s.rcyl(0.026, 0.012, (x, -0.228, 0.2), axis="Y", seg=12, r=0.003)]
    s.part("Accent", acc)
    return export("bar_counter")


def model_bar_stool():
    """Round bar stool: domed round base, column with a gas-lift collar, foot ring on three spokes, padded seat
    at 0.75 on a seat pan."""
    body = [s.lathe([(0.0, 0.0), (0.2, 0.0), (0.2, 0.008), (0.19, 0.018), (0.06, 0.036), (0.04, 0.05),
                     (0.0, 0.05)], seg=32),
            s.rcyl(0.026, 0.64, (0, 0, 0.36), seg=16, r=0),
            s.rcyl(0.036, 0.1, (0, 0, 0.62), seg=16, r=0.006),
            s.rcyl(0.035, 0.03, (0, 0, 0.3), seg=16, r=0.005),
            torus(0.16, 0.011, (0, 0, 0.3), seg=28, rseg=8),
            s.lathe([(0.0, 0.665), (0.05, 0.665), (0.15, 0.682), (0.165, 0.692), (0.0, 0.694)], seg=28)]
    for k in range(3):
        a = 2 * math.pi * k / 3 + math.pi / 2
        body.append(rod((0.02 * math.cos(a), 0.02 * math.sin(a), 0.3),
                        (0.163 * math.cos(a), 0.163 * math.sin(a), 0.3), 0.008, seg=8))
    s.part("Body", body)
    seat = s.lathe([(0.0, 0.688), (0.17, 0.688), (0.183, 0.696), (0.19, 0.716), (0.184, 0.738), (0.16, 0.75),
                    (0.08, 0.754), (0.0, 0.755)], seg=32)
    s.groove(seat, "Z", 0.716, 0.004, 0.002)
    s.part("Accent", [seat])
    return export("bar_stool")


# --------------------------------------------------------------------------- electronics lab

def model_lab_bench():
    """Electronics lab bench 1.8 x 0.75, top at 0.9: steel legs and stretchers, a low shelf, a riser at the back with
    a pegboard and two shelves (oscilloscope and bench power supply on the lower one), a soldering station with its
    iron in a holder, a magnifier lamp on a jointed arm, a circuit board and parts trays."""
    body = [s.rbox((1.8, 0.75, 0.04), (0, 0, 0.88), r=0.01, seg=2),
            s.rbox((1.72, 0.66, 0.025), (0, 0, 0.2), r=0.006, seg=1),
            s.rbox((1.76, 0.28, 0.025), (0, 0.225, 1.2025), r=0.006, seg=1),
            s.rbox((1.76, 0.2, 0.025), (0, 0.265, 1.4875), r=0.006, seg=1),
            s.rbox((1.72, 0.015, 0.6), (0, 0.36, 1.195), r=0.004, seg=1)]
    s.part("Body", body)

    trim = []
    for x in (-0.85, 0.85):
        for y in (-0.33, 0.33):
            trim.append(s.rbox((0.05, 0.05, 0.865), (x, y, 0.4325), r=0.008, seg=1))
        trim += [s.rbox((0.04, 0.66, 0.04), (x, 0, 0.12), r=0.006, seg=1),
                 s.rbox((0.04, 0.04, 0.61), (x * 1.012, 0.335, 1.1925), r=0.006, seg=1)]
    trim += [s.rbox((1.66, 0.04, 0.04), (0, y, 0.12), r=0.006, seg=1) for y in (-0.33, 0.33)]
    # scope knobs, psu knobs and binding posts
    trim += [s.rcyl(0.012, 0.016, (x, 0.078, z), axis="-Y", seg=8, r=0) for x in (-0.33, -0.29)
             for z in (1.26, 1.31, 1.36)]
    trim += [s.rcyl(0.014, 0.016, (x, 0.098, 1.25), axis="-Y", seg=8, r=0) for x in (-0.05, 0.0)]
    trim += [s.rcyl(0.008, 0.02, (x, 0.098, 1.3), axis="-Y", seg=6, r=0) for x in (0.04, 0.07, 0.1)]
    # soldering iron holder: base, slanted coil tube, sponge tray
    trim += [s.rbox((0.1, 0.14, 0.014), (0.55, -0.08, 0.903), r=0.004, seg=1),
             rod((0.55, -0.02, 0.905), (0.55, -0.12, 0.99), 0.014, seg=10)]
    # magnifier lamp arm: clamp on the back edge, post, two arms
    lamp_head = (0.58, -0.17, 1.08)
    trim += [s.rbox((0.05, 0.06, 0.07), (0.72, 0.29, 0.905), r=0.008, seg=1),
             rod((0.72, 0.29, 0.92), (0.72, 0.29, 1.1), 0.011, seg=8),
             rod((0.72, 0.29, 1.1), (0.7, 0.02, 1.15), 0.009, seg=8),
             rod((0.7, 0.02, 1.15), (lamp_head[0] + 0.058, lamp_head[1] + 0.058, lamp_head[2] + 0.008), 0.008, seg=8),
             s.ball(0.017, (0.72, 0.29, 1.1), seg=8), s.ball(0.016, (0.7, 0.02, 1.15), seg=8)]
    s.part("Trim", trim)

    acc = [s.rbox((0.4, 0.24, 0.2), (-0.45, 0.2, 1.31), r=0.015, seg=2),
           s.rbox((0.42, 0.04, 0.02), (-0.45, 0.31, 1.415), r=0.006, seg=1),
           s.rbox((0.24, 0.22, 0.14), (0.0, 0.21, 1.28), r=0.012, seg=2),
           s.rbox((1.0, 0.04, 0.05), (0.2, 0.34, 1.0), r=0.008, seg=1),  # power strip on the pegboard
           s.rbox((0.15, 0.16, 0.09), (0.35, -0.05, 0.94), r=0.012, seg=2, taper=(1.0, 0.85)),
           s.rcyl(0.015, 0.012, (0.38, -0.132, 0.91), axis="-Y", seg=8, r=0.002),
           rod((0.55, -0.17, 1.03), (0.55, -0.02, 0.92), 0.008, seg=8),  # the iron, resting in the coil
           s.tube([(0.35, 0.03, 0.93), (0.45, 0.05, 0.91), (0.53, 0.0, 0.92), (0.55, -0.03, 0.925)], 0.005,
                  segments_per=6),
           s.rbox((0.16, 0.11, 0.008), (-0.2, -0.1, 0.902), r=0.002, seg=1)]
    acc += [s.rbox((w, d, 0.01), (x, y, 0.908), r=0.001, seg=1)
            for x, y, w, d in ((-0.24, -0.11, 0.04, 0.03), (-0.17, -0.08, 0.03, 0.02), (-0.15, -0.13, 0.02, 0.02))]
    acc += [tray((0.13, 0.1, 0.04), (x, -0.25, 0.917), wall=0.008) for x in (-0.76, -0.62, -0.48)]
    acc.append(poi.ring(0.09, 0.07, 0.03, lamp_head, axis="Z", seg=20, r=0))
    s.part("Accent", acc)

    eye = [s.rbox((0.2, 0.01, 0.13), (-0.54, 0.0805, 1.315), r=0.003, seg=1),
           s.rbox((0.08, 0.008, 0.03), (-0.04, 0.1, 1.31), r=0.002, seg=1),
           s.rbox((0.06, 0.008, 0.025), (0.33, -0.128, 0.955), r=0.002, seg=1),
           s.rcyl(0.072, 0.008, lamp_head, seg=20, r=0)]
    s.part("Eye", eye)
    return export("lab_bench")


def model_parts_cabinet():
    """Small-parts cabinet 0.9 x 0.45, 1.4 high: a steel base cabinet with two doors and a 5 x 8 grid of little
    clear drawers with pulls on top of it."""
    body = [s.rbox((0.9, 0.45, 0.8), (0, 0, 0.4), r=0.01, seg=2),
            s.rbox((0.9, 0.42, 0.605), (0, 0.015, 1.0975), r=0.01, seg=2)]
    body += [s.rbox((0.43, 0.02, 0.7), (xs * 0.2175, -0.22, 0.43), r=0.006, seg=1) for xs in (-1, 1)]
    s.groove(body[0], "Z", 0.05, 0.01, 0.005)
    s.part("Body", body)

    acc, trim = [], []
    for c in range(5):
        x = -0.336 + c * 0.168
        for r in range(8):
            z = 0.838 + r * 0.07
            acc.append(s.rbox((0.156, 0.03, 0.061), (x, -0.2, z), r=0.006, seg=1))
            trim.append(s.rbox((0.05, 0.012, 0.012), (x, -0.217, z - 0.012), r=0))
    trim += [s.rbox((0.022, 0.025, 0.16), (xs * 0.035, -0.235, 0.55), r=0.006, seg=1) for xs in (-1, 1)]
    s.part("Accent", acc)
    s.part("Trim", trim)
    return export("parts_cabinet")


def model_robot_arm():
    """Six-axis industrial robot arm on a square pedestal, posed reaching forward and down: turret, shoulder
    housing, upper arm leaning back, elbow with the forearm motor behind it, forearm, wrist and a two-finger
    gripper; a status light on the pedestal."""
    S = Vector((0, 0.02, 0.86))     # shoulder axis (X)
    El = Vector((0, 0.1, 1.45))     # elbow axis (X)
    W = Vector((0, -0.34, 1.22))    # wrist axis (X)
    F = Vector((0, -0.38, 1.06))    # tool flange
    acc = [s.rbox((0.6, 0.6, 0.06), (0, 0, 0.03), r=0.012, seg=2),
           s.rbox((0.44, 0.44, 0.5), (0, 0, 0.3), r=0.02, seg=2),
           s.rbox((0.5, 0.5, 0.04), (0, 0, 0.55), r=0.01, seg=1),
           s.lathe([(0.0, 0.565), (0.2, 0.565), (0.2, 0.6), (0.185, 0.615), (0.17, 0.68), (0.0, 0.68)], seg=24),
           s.rcyl(0.115, 0.34, S, axis="X", seg=24, r=0.012, bevel_seg=1),
           s.rcyl(0.085, 0.24, El, axis="X", seg=20, r=0.01, bevel_seg=1),
           s.rcyl(0.065, 0.13, El + Vector((0, 0.15, 0.02)), axis="Y", seg=16, r=0.01, bevel_seg=1),
           s.rcyl(0.055, 0.16, W, axis="X", seg=16, r=0.008, bevel_seg=1),
           s.rcyl(0.045, 0.03, F, seg=16, r=0.004),
           s.rbox((0.12, 0.07, 0.06), F - Vector((0, 0, 0.04)), r=0.01, seg=1),
           s.rcyl(0.026, 0.03, (0.19, -0.19, 0.58), seg=12, r=0)]
    acc += s.bolts([(x, y, 0.06) for x in (-0.26, 0.26) for y in (-0.26, 0.26)], radius=0.016, depth=0.012,
                   axis="Z")
    s.groove(acc[1], "Z", 0.42, 0.01, 0.005)
    for xs in (-1, 1):
        acc.append(s.rbox((0.018, 0.035, 0.1), (xs * 0.035, F.y - 0.01, F.z - 0.11), r=0.005, seg=1))
    acc.append(s.tube([(0.0, 0.17, 0.66), (0.0, 0.16, 0.95), (0.0, 0.14, 1.25), (0.0, 0.2, 1.42)], 0.016,
                      segments_per=6))
    s.part("Accent", acc)

    body = [s.rbox((0.3, 0.32, 0.24), (0, 0.02, 0.77), r=0.06, seg=2),
            link(S, El, 0.09, 0.1, 0.075, 0.08, seg=20, caps=0.02),
            s.rbox((0.17, 0.3, 0.17), El + Vector((0, 0.03, 0.0)), r=0.05, seg=2),
            link(El, W, 0.07, 0.07, 0.055, 0.055, seg=16, caps=0.02),
            link(W, F + Vector((0, 0, 0.01)), 0.045, 0.045, 0.04, 0.04, seg=16, caps=0.01)]
    s.groove(body[0], "Z", 0.72, 0.01, 0.004)
    s.part("Body", body)
    s.part("Eye", [s.lathe([(0.02, 0.59), (0.021, 0.615), (0.015, 0.632), (0.0, 0.638)], seg=12,
                           loc=(0.19, -0.19, 0))])
    return export("robot_arm")


MODELS = {
    "mixing_desk": model_mixing_desk,
    "mic_stand": model_mic_stand,
    "acoustic_panel": model_acoustic_panel,
    "drum_kit": model_drum_kit,
    "pool_table": model_pool_table,
    "foosball": model_foosball,
    "dart_board": model_dart_board,
    "bar_counter": model_bar_counter,
    "bar_stool": model_bar_stool,
    "lab_bench": model_lab_bench,
    "parts_cabinet": model_parts_cabinet,
    "robot_arm": model_robot_arm,
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
