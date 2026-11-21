"""More pickup items as they lie in the world: a walkie-talkie, a keyboard, a rugged tablet, a first-aid box, a jar of
coffee, the fuel finder, a pair of boots, a tin of tomatoes, a folded chessboard, a magazine, pills, toothpaste, duct
tape, matches, a toilet roll, an apple core, soap, work gloves, a necklace, a mop, a teddy bear and rolled-up socks.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/items_more.py -- Assets/_Game/Art/Models [--only a,b]
      (--only takes model names: item_radio_model,item_mop_model; files land in <root>/Items/item_<id>_model.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor [--shading]

Same conventions as poi.py's items (item_book_model, item_cans_model…): real-life size in metres, the long side along
Blender X, resting on z = 0, centred in XY (export() settles every model there), Blender -Y = Unity +Z, the Unity import
mirrors Blender X. Object names are material roles: Body, Accent*, Trim*, Eye* (glows). The tablet's EyeScreen has its
own 0..1 UVs over the screen face (u along +X, v along +Y, seen from above) so a picture can be put on it.
Grid pieces of these items: generate_models.SHAPES; backpack pictures: item_icons.ITEMS.
"""
import math
import os
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir, category())
import meshfix  # noqa: E402
import poi  # noqa: E402  (ring(), sweep_rect(), _tin())
import shapes as s  # noqa: E402


def bpy_objects():
    import bpy
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def _settle(objs):
    """Moves the whole model so its footprint is centred on the origin and its lowest point is on z = 0."""
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    shift = Matrix.Translation((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z))
    for o in objs:
        o.data.transform(shift)
        o.data.update()


def export(filename):
    """Settle the model, meshfix (floor as support), FBX into <root>/Items/; prints the bounds."""
    import bpy
    objs = bpy_objects()
    _settle(objs)
    bpy.context.view_layer.update()
    meshfix.fix_objects(objs, floor=True, name=filename)
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    size = [max(p[k] for p in pts) - min(p[k] for p in pts) for k in range(3)]
    print(f"BOUNDS {filename}: blender x {size[0]:.3f} y {size[1]:.3f} z {size[2]:.3f}  "
          f"(unity x {size[0]:.3f} y {size[2]:.3f} z {size[1]:.3f})  parts {sorted(o.name for o in objs)}")
    path = os.path.join(g.out_dir, g.category(filename), filename + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


# --------------------------------------------------------------------------- helpers

def rnd(k):
    """A fixed pseudo-random number in 0..1 for an integer k (the models are deterministic)."""
    return (math.sin(k * 12.9898 + 4.1414) * 43758.5453) % 1.0


def xloft(rings, seg=24, round_caps=0.0, cap=True):
    """s.loft along world X from (x, half height, half width, n, centre z, centre y) rings."""
    out = []
    for r in rings:
        x, hz, hy = r[0], r[1], r[2]
        n = r[3] if len(r) > 3 else 2.0
        zc = r[4] if len(r) > 4 else 0.0
        yc = r[5] if len(r) > 5 else 0.0
        out.append((x, hz, hy, n, -zc, yc))       # loft axis X: local x -> world -z
    return s.loft(out, seg=seg, axis="X", round_caps=round_caps, cap=cap)


def prism(outline, z0, z1, r=0.001, seg=1):
    """A flat plate from a 2D outline (x, y) between z0 and z1, its edges rounded."""
    bm = bmesh.new()
    lo = [bm.verts.new((x, y, z0)) for x, y in outline]
    hi = [bm.verts.new((x, y, z1)) for x, y in outline]
    n = len(outline)
    for k in range(n):
        bm.faces.new((lo[k], lo[(k + 1) % n], hi[(k + 1) % n], hi[k]))
    bm.faces.new(list(reversed(lo)))
    bm.faces.new(hi)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.bevel_edges(bm, r, seg, angle=40)
    return bm


def rect_tube(a, b, length, t, loc=(0, 0, 0), axis="Z", r=0.0004):
    """A hollow rectangular sleeve along an axis: outside a x b, walls t thick, open at both ends, edges rounded."""
    bm = bmesh.new()
    z0, z1 = -length / 2, length / 2

    def rect(w, h, z):
        return [bm.verts.new((sx * w / 2, sy * h / 2, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]

    oo, oi = rect(a, b, z0), rect(a - 2 * t, b - 2 * t, z0)
    eo, ei = rect(a, b, z1), rect(a - 2 * t, b - 2 * t, z1)
    for k in range(4):
        j = (k + 1) % 4
        bm.faces.new((oo[k], oo[j], eo[j], eo[k]))          # outside
        bm.faces.new((oi[j], oi[k], ei[k], ei[j]))          # inside
        bm.faces.new((oo[j], oo[k], oi[k], oi[j]))          # the ends
        bm.faces.new((eo[k], eo[j], ei[j], ei[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.bevel_edges(bm, min(r, 0.3 * t), 1, angle=40)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


def slice_grid(bm, axis, values):
    """Cuts the mesh at planes axis = value (more vertices to bend)."""
    i = "XYZ".index(axis)
    no = Vector((0, 0, 0))
    no[i] = 1.0
    for v in values:
        co = Vector((0, 0, 0))
        co[i] = v
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no)
    return bm


def unit_uvs(obj):
    """UVs 0..1 over the object's top view: u along +X, v along +Y (a picture on a screen lying face up)."""
    me = obj.data
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    uv = me.uv_layers.active.data
    for loop in me.loops:
        co = me.vertices[loop.vertex_index].co
        uv[loop.index].uv = ((co.x - x0) / (x1 - x0), (co.y - y0) / (y1 - y0))


def moved(bms, loc=(0, 0, 0), rot=(0, 0, 0)):
    for b in bms:
        s.place(b, loc, rot)
    return bms


# --------------------------------------------------------------------------- electronics

def model_item_radio_model():
    """A handheld walkie-talkie lying on its back, 0.21 m with the antenna: a dark case with a back-shell seam and a
    head block, a speaker grille, a small display and a push-to-talk key on the side (Accent with the rubber antenna
    and the channel knob at +X), a green LED (EyeLed)."""
    h = 0.036
    case = s.rbox((0.125, 0.062, h - 0.004), (-0.03, 0, 0.004 + (h - 0.004) / 2), r=0.009, seg=3)
    s.groove(case, "Z", 0.014, width=0.0018, depth=0.0009)                  # back shell seam
    head = s.rbox((0.014, 0.058, 0.03), (0.036, 0, 0.019), r=0.006, seg=2)   # the top block the antenna stands in
    clip = s.rbox((0.06, 0.03, 0.0065), (-0.035, 0, 0.00325), r=0.002, seg=1)  # belt clip under the back
    s.part("Body", [case, head, clip])
    accent = [s.rcyl(0.009, 0.012, (0.046, 0.016, 0.018), axis="X", seg=16, r=0.003),          # antenna collar
              s.rcyl(0.0066, 0.075, (0.085, 0.016, 0.018), axis="X", seg=14, r=0.002, radius2=0.0048),
              s.ball(0.0055, (0.1225, 0.016, 0.018), seg=12),                                   # antenna tip
              s.rcyl(0.0085, 0.016, (0.048, -0.016, 0.018), axis="X", seg=16, r=0.003),         # channel knob
              s.rcyl(0.0092, 0.004, (0.052, -0.016, 0.018), axis="X", seg=16, r=0.0012),        # its ribbed ring
              s.rbox((0.034, 0.03, 0.002), (-0.014, 0, h + 0.0004), r=0.0008, seg=1),           # display
              s.rbox((0.036, 0.007, 0.016), (-0.028, -0.0315, 0.019), r=0.0025, seg=2)]         # push-to-talk key
    accent += [s.rbox((0.0045, 0.04, 0.003), (-0.078 + k * 0.0075, 0, h + 0.0007), r=0.0012, seg=1) for k in range(6)]
    s.part("Accent", accent)
    s.part("EyeLed", [s.rcyl(0.0028, 0.003, (0.016, 0.019, h + 0.0008), seg=12, r=0.0008)])
    return export("item_radio_model")


KEY_U = 0.019            # keyboard key pitch


def _keycap(x, y, wu, hu, z0):
    """One keycap: a rounded-square foot and a smaller dished top (two loft rings)."""
    w = wu * KEY_U - 0.0034
    d = hu * KEY_U - 0.0034
    return s.loft([(z0, w / 2, d / 2, 6.0, x, y), (z0 + 0.008, w / 2 - 0.0026, d / 2 - 0.0032, 3.2, x, y + 0.0004)],
                  seg=8)


def model_item_keyboard_model():
    """A full-size computer keyboard lying flat, 0.46 x 0.16: a case with a raised back rim (Body), real key rows —
    function row, number row, three letter rows, the space row, the arrow and navigation block and the numpad
    (Accent), three lock LEDs above the numpad (EyeLed). The space bar is at -Y (the front)."""
    total = 23 * KEY_U
    xs = -total / 2
    case = s.rbox((total + 0.018, 0.158, 0.019), (0, 0.006, 0.0125), r=0.006, seg=2)   # 0.003 .. 0.022
    rim = s.rbox((total + 0.014, 0.024, 0.025), (0, 0.0725, 0.016), r=0.007, seg=2)     # the raised back edge
    feet = [s.rbox((0.04, 0.012, 0.0045), (x, 0.07, 0.00225), r=0.0015, seg=1) for x in (-0.18, 0.18)]
    s.part("Body", [case, rim, *feet])
    z0 = 0.0198
    y_of = lambda r: -0.052 + r * KEY_U  # noqa: E731  (row 0 = space row at the front)
    keys = []

    def row(r, widths, x_off=0.0):
        x = xs + x_off * KEY_U
        for w in widths:
            if w > 0:
                keys.append(_keycap(x + w * KEY_U / 2, y_of(r), w, 1.0, z0))
            x += abs(w) * KEY_U

    row(0, [1.25, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25])
    row(1, [2.25] + [1] * 10 + [2.75])
    row(2, [1.75] + [1] * 11 + [2.25])
    row(3, [1.5] + [1] * 12 + [1.5])
    row(4, [1] * 13 + [2])
    f_row = 5.3                                                   # the function row, a little gap behind
    row(f_row, [1, -1, 1, 1, 1, 1, -0.5, 1, 1, 1, 1, -0.5, 1, 1, 1, 1])
    # navigation block and arrows
    row(f_row, [1, 1, 1], 15.5)
    row(4, [1, 1, 1], 15.5)
    row(3, [1, 1, 1], 15.5)
    row(1, [-1, 1], 15.5)
    row(0, [1, 1, 1], 15.5)
    # numpad: + and Enter two rows tall
    np = 19.5
    row(4, [1, 1, 1, 1], np)
    row(3, [1, 1, 1], np)
    row(2, [1, 1, 1], np)
    row(1, [1, 1, 1], np)
    row(0, [2, 1], np)
    for r in (2.5, 0.5):
        keys.append(_keycap(xs + (np + 3.5) * KEY_U, y_of(r), 1.0, 2.0, z0))
    s.part("Accent", keys)
    leds = [s.rcyl(0.0016, 0.0024, (xs + (np + 0.5 + k * 1.2) * KEY_U, y_of(f_row), 0.0223), seg=10, r=0.0006)
            for k in range(3)]
    s.part("EyeLed", leds)
    return export("item_keyboard_model")


def model_item_tablet_model():
    """A rugged tablet lying face up, 0.26 x 0.185: a rubber case with thick corner bumpers and three side keys
    (Body), the bezel (Accent), the screen face (EyeScreen: its own 0..1 UVs, u along +X, v along +Y)."""
    dz = 0.0015                                               # the case floats on its corner bumpers
    case = s.rbox((0.25, 0.175, 0.02), (0, 0, 0.01 + dz), r=0.008, seg=3)
    s.groove(case, "Z", 0.008 + dz, width=0.0016, depth=0.0008)
    bumpers = [s.rbox((0.046, 0.046, 0.0255), (sx * 0.107, sy * 0.0745, 0.01275), r=0.01, seg=3)
               for sx in (-1, 1) for sy in (-1, 1)]
    keys = [s.rbox((0.016, 0.006, 0.006), (x, 0.0885, 0.011 + dz), r=0.0022, seg=1) for x in (-0.03, -0.01, 0.04)]
    handle = s.rbox((0.012, 0.11, 0.008), (-0.1275, 0, 0.0105 + dz), r=0.0035, seg=2)     # the grip strap end
    s.part("Body", [case, *bumpers, *keys, handle])
    bezel = s.rbox((0.226, 0.152, 0.0045), (0, 0, 0.02 + dz), r=0.0018, seg=2)
    button = s.rcyl(0.005, 0.0016, (0.104, 0, 0.0224 + dz), seg=14, r=0.0005)              # home key on the bezel
    camera = s.rcyl(0.0025, 0.0014, (-0.104, 0, 0.0223 + dz), seg=10, r=0.0004)
    s.part("Accent", [bezel, button, camera])
    screen = s.part("EyeScreen", [s.rbox((0.19, 0.13, 0.0015), (0, 0, 0.0226 + dz), r=0.0005, seg=1)])
    unit_uvs(screen)
    return export("item_tablet_model")


def model_item_finder_model():
    """The fuel finder, a handheld detector lying flat, 0.23 m: a casing that widens from a ribbed grip at -X to the
    head (Body), a dial bezel with a needle, a knob, the whip antenna at +X (Accent), the big lamp on the head that
    blinks (EyeLed, a dome in a chrome collar)."""
    body = xloft([(-0.09, 0.016, 0.022, 3.0, 0.016), (-0.045, 0.017, 0.024, 3.0, 0.017), (-0.012, 0.019, 0.033, 3.2, 0.019),
                  (0.045, 0.02, 0.038, 3.5, 0.02), (0.078, 0.019, 0.036, 3.5, 0.019)], seg=24, round_caps=0.008)
    for x in (-0.078, -0.067, -0.056):
        s.groove(body, "X", x, width=0.0026, depth=0.0013)                                  # grip ribs
    s.groove(body, "Z", 0.01, width=0.0016, depth=0.0008)                                   # shell seam
    s.part("Body", [body])
    top = 0.0395
    accent = [poi.ring(0.0165, 0.0125, 0.005, (0.012, 0, top + 0.0005), seg=20, r=0.0012),   # dial bezel
              s.rcyl(0.0128, 0.0022, (0.012, 0, top), seg=20, r=0.0006),                     # dial glass
              s.rbox((0.0115, 0.0014, 0.001), (0.0135, 0.0035, top + 0.0013), r=0.0003, seg=1,
                     rot=(0, 0, math.radians(35))),                                          # the needle
              s.rcyl(0.0075, 0.008, (-0.024, 0.013, 0.0385), seg=14, r=0.002),                # sensitivity knob
              s.rcyl(0.009, 0.012, (0.0835, 0.012, 0.02), axis="X", seg=14, r=0.003),          # antenna socket
              s.rcyl(0.0038, 0.058, (0.115, 0.012, 0.02), axis="X", seg=10, r=0.0012, radius2=0.0028),
              s.ball(0.0062, (0.145, 0.012, 0.02), seg=10),                                    # antenna tip
              poi.ring(0.0172, 0.0128, 0.005, (0.056, -0.004, 0.0405), seg=20, r=0.0012)]      # the lamp's collar
    s.part("Accent", accent)
    lamp = s.lathe([(0.0135, 0.0), (0.0134, 0.004), (0.0118, 0.0088), (0.0075, 0.0122), (0.0, 0.0136)], seg=20,
                   loc=(0.056, -0.004, 0.0375))
    s.part("EyeLed", [lamp])
    return export("item_finder_model")


# --------------------------------------------------------------------------- boxes, tins, jars

def model_item_medkit_model():
    """A first-aid box lying lid up, 0.30 x 0.20 x 0.10: a rounded case with the lid seam (Body), two latches on the
    front and a carry handle on the back side (Accent), the cross on the lid (Trim, one plate)."""
    L, W, H = 0.30, 0.2, 0.1
    case = s.rbox((L, W, H), (0, 0, H / 2), r=0.014, seg=3)
    s.groove(case, "Z", 0.068, width=0.003, depth=0.0015)
    s.part("Body", [case])
    accent = []
    for x in (-0.085, 0.085):
        accent += [s.rbox((0.03, 0.008, 0.034), (x, -W / 2 - 0.001, 0.07), r=0.0025, seg=2),        # latch plate
                   s.rcyl(0.0042, 0.034, (x, -W / 2 - 0.0042, 0.054), axis="X", seg=12, r=0.0012)]   # its hinge pin
    accent += [s.rbox((0.018, 0.016, 0.026), (x, W / 2 + 0.006, 0.075), r=0.004, seg=2) for x in (-0.065, 0.065)]
    accent.append(s.rbox((0.15, 0.014, 0.016), (0, W / 2 + 0.011, 0.075), r=0.0065, seg=2))          # the grip
    s.part("Accent", accent)
    a, b = 0.017, 0.052
    plus = [(a, b), (-a, b), (-a, a), (-b, a), (-b, -a), (-a, -a), (-a, -b), (a, -b), (a, -a), (b, -a), (b, a), (a, a)]
    s.part("Trim", [prism(plus, H - 0.001, H + 0.0016, r=0.0008)])
    return export("item_medkit_model")


def model_item_coffee_model():
    """A jar of instant coffee lying on its side along X, 0.135 m, the lid at +X: a squat glass jar with a shoulder
    and neck (Body), its paper label (Trim), the ribbed screw lid (Accent)."""
    zc = 0.0418
    jar = s.lathe([(0.0, 0.0), (0.035, 0.0), (0.0395, 0.0025), (0.041, 0.009), (0.041, 0.086), (0.0395, 0.095),
                   (0.0345, 0.1025), (0.031, 0.105), (0.031, 0.109), (0.0, 0.109)], seg=32, axis="X", loc=(-0.067, 0, zc))
    s.part("Body", [jar])
    label = s.rcyl(0.0416, 0.062, (-0.067 + 0.048, 0, zc), axis="X", seg=32, r=0.0005, bevel_seg=1)
    s.part("Trim", [label])
    lid = s.lathe([(0.0, 0.0), (0.0345, 0.0), (0.0358, 0.0015), (0.0358, 0.0205), (0.0345, 0.0222), (0.0, 0.0225)],
                  seg=32, axis="X", loc=(-0.067 + 0.1035, 0, zc))
    for k in range(4):
        s.groove(lid, "X", -0.067 + 0.108 + k * 0.0042, width=0.0012, depth=0.0006)
    s.part("Accent", [lid])
    return export("item_coffee_model")


def model_item_tomatoes_model():
    """A tin of canned tomatoes lying on its side along X, 0.11 m: steel shell with rolled rims and a ring pull (Accent),
    the paper label over nearly all its height (Body)."""
    steel, labels = poi._tin(0.11, 0.9)
    for b in steel + labels:
        s.place(b, (0, 0, -0.055))
        s.place(b, (0, 0, 0.0398), rot=(0, math.pi / 2, 0))
    s.part("Body", labels)
    s.part("Accent", steel)
    return export("item_tomatoes_model")


def model_item_pills_model():
    """Headache pills: the box lying flat, 0.10 x 0.05 x 0.022, its tuck flap seam (Body), and a blister sheet of ten
    pills lying beside it, a little turned (Accent: foil and domes)."""
    box = s.rbox((0.1, 0.05, 0.022), (0, 0.03, 0.011), r=0.002, seg=2)
    s.groove(box, "X", 0.043, width=0.0012, depth=0.0006)
    s.groove(box, "X", -0.043, width=0.0012, depth=0.0006)
    s.part("Body", [box])
    blister = [s.rbox((0.09, 0.042, 0.0012), (0, 0, 0.0006), r=0.0005, seg=1)]
    for k in range(5):
        for j in (-1, 1):
            blister.append(s.lathe([(0.0062, 0.0), (0.006, 0.0018), (0.0048, 0.0034), (0.0025, 0.0044), (0.0, 0.0047)],
                                   seg=12, loc=(-0.032 + k * 0.016, j * 0.0105, 0.0009)))
    moved(blister, (0.004, -0.03, 0), (0, 0, math.radians(-7)))
    s.part("Accent", blister)
    return export("item_pills_model")


def model_item_matches_model():
    """A large kitchen matchbox lying flat, 0.10 x 0.06 x 0.022, its drawer slid out at +X: the sleeve with striker
    strips on both sides (Body), the drawer and a layer of matches (Accent), their red heads (Trim)."""
    L, W, H = 0.1, 0.06, 0.022
    t = 0.0012
    sleeve = rect_tube(H, W, L, t, (0, 0, H / 2), axis="X", r=0.0005)
    strikers = [s.rbox((L - 0.012, 0.0012, H - 0.008), (0, sy * (W / 2 + 0.0002), H / 2), r=0.0004, seg=1)
                for sy in (-1, 1)]
    s.part("Body", [sleeve, *strikers])
    out = 0.03                                                    # how far the drawer is slid out
    x0, x1 = -L / 2 + 0.002 + out, L / 2 - 0.002 + out
    cx = (x0 + x1) / 2
    yi = W / 2 - 0.0016
    wall_h = H - 0.0034
    drawer = [rect_tube(x1 - x0, 2 * yi, wall_h, t, (cx, 0, 0.001 + wall_h / 2), r=0.0003),
              s.rbox((x1 - x0 - t, 2 * yi - t, t), (cx, 0, 0.0019), r=0.0003, seg=1)]                # its bottom
    sticks, heads = [], []
    for k in range(11):
        y = -yi + 0.0042 + k * 0.0047
        flip = 1 if k % 2 == 0 else -1                            # heads alternate ends, as they are packed
        lx = 0.082
        cx = (x0 + x1) / 2 + flip * 0.001
        sticks.append(s.rbox((lx, 0.0028, 0.0028), (cx, y, 0.0034), r=0.0004, seg=1))
        heads.append(s.ball(0.0024, (cx + flip * lx / 2, y, 0.0035), seg=8, scale=(1.5, 1.05, 1.05)))
    s.part("Accent", drawer + sticks)
    s.part("Trim", heads)
    return export("item_matches_model")


def model_item_chess_model():
    """A folded chessboard box lying flat, 0.30 x 0.15 x 0.046: two wooden leaves with the fold seam (Body), the light
    squares inlaid on the top leaf (Accent), two hinges and a clasp (Accent), a rook and a pawn standing on dark
    squares (Accent)."""
    L, W, H = 0.3, 0.15, 0.046
    board = s.rbox((L, W, H), (0, 0, H / 2), r=0.004, seg=2)
    s.groove(board, "Z", H / 2, width=0.002, depth=0.0012)
    s.part("Body", [board])
    sq = 0.034
    accent = []
    for i in range(8):
        for j in range(4):
            if (i + j) % 2 == 0:
                accent.append(s.rbox((sq - 0.0012, sq - 0.0012, 0.0012), (-3.5 * sq + i * sq, -1.5 * sq + j * sq, H + 0.0003),
                                          r=0.0))
    accent += [s.rcyl(0.004, 0.03, (x, W / 2 + 0.0015, H / 2), axis="X", seg=12, r=0.001) for x in (-0.09, 0.09)]
    accent.append(s.rbox((0.022, 0.006, 0.02), (0, -W / 2 - 0.0012, H / 2), r=0.0015, seg=1))
    rook = s.lathe([(0.0, 0.0), (0.0145, 0.0), (0.015, 0.004), (0.0118, 0.007), (0.0092, 0.012), (0.0082, 0.026),
                    (0.0112, 0.03), (0.0112, 0.038), (0.0078, 0.0385), (0.0078, 0.035), (0.0, 0.035)], seg=20,
                   loc=(3 * sq - 3.5 * sq + sq, -0.5 * sq, H - 0.0005))     # on the dark square (i=4, j=1)
    pawn = s.lathe([(0.0, 0.0), (0.0125, 0.0), (0.013, 0.0035), (0.0095, 0.006), (0.0065, 0.011), (0.0052, 0.019),
                    (0.0082, 0.0205), (0.005, 0.022), (0.0, 0.022)], seg=18, loc=(-1.5 * sq - 1 * sq, 0.5 * sq, H - 0.0005))
    pawn_head = s.ball(0.0078, (-2.5 * sq, 0.5 * sq, H - 0.0005 + 0.028), seg=14)
    s.part("Accent", accent + [rook, pawn, pawn_head])
    return export("item_chess_model")


def model_item_magazine_model():
    """A glossy magazine lying flat, 0.28 x 0.21, 7 mm thick (its top end at -X): the cover wrapped round a square
    spine at -Y, the front cover's free corner curling up (Body), the page block between the covers (Accent), the
    printed masthead, cover lines and barcode (Trim)."""
    L, W = 0.28, 0.21
    top = s.rbox((L, W, 0.0012), (0, 0, 0.0064), r=0.0005, seg=1)
    slice_grid(top, "X", [0.03 + k * 0.022 for k in range(6)])
    slice_grid(top, "Y", [0.0 + k * 0.021 for k in range(5)])
    for v in top.verts:                                       # the corner at +X +Y lifts in a soft curl
        fx = max(0.0, (v.co.x - 0.03) / (L / 2 - 0.03))
        fy = max(0.0, (v.co.y - 0.0) / (W / 2))
        v.co.z += 0.016 * (fx * fy) ** 1.6
    bottom = s.rbox((L, W, 0.0012), (0, 0, 0.0006), r=0.0005, seg=1)
    spine = s.rbox((L - 0.001, 0.006, 0.0064), (0, -W / 2 + 0.002, 0.0035), r=0.002, seg=2)
    s.part("Body", [top, bottom, spine])
    pages = s.rbox((L - 0.004, W - 0.005, 0.0048), (-0.001, 0.0015, 0.0032), r=0.0006, seg=1)   # into the spine
    s.part("Accent", [pages])
    # the cover's print: the masthead across the top end (-X), cover lines down the spine side, a barcode corner
    z = 0.0071
    trim = [s.rbox((0.034, 0.18, 0.0006), (-0.112, 0.0, z), r=0.0002, seg=1)]
    trim += [s.rbox((0.009, w, 0.0006), (x, -0.07 + w / 2, z), r=0.0002, seg=1)
             for x, w in ((-0.06, 0.07), (-0.04, 0.05), (-0.02, 0.06), (0.08, 0.055), (0.1, 0.04))]
    trim.append(s.rbox((0.02, 0.03, 0.0006), (0.11, -0.075, z), r=0.0002, seg=1))
    s.part("Trim", trim)
    return export("item_magazine_model")


def model_item_toothpaste_model():
    """A toothpaste tube lying flat along X, 0.20 m: the tube from its crimped flat end at -X to the round shoulder
    (Body), the ribbed screw cap at +X (Accent)."""
    tube = xloft([(-0.085, 0.002, 0.026, 4.0, 0.0024), (-0.07, 0.0065, 0.026, 3.4, 0.0068),
                  (-0.03, 0.0125, 0.023, 2.6, 0.0127), (0.02, 0.0155, 0.02, 2.2, 0.0157),
                  (0.058, 0.0165, 0.0178, 2.0, 0.0167), (0.07, 0.0148, 0.0158, 2.0, 0.0158),
                  (0.077, 0.0095, 0.0095, 2.0, 0.0132), (0.083, 0.0068, 0.0068, 2.0, 0.0124)], seg=28)
    crimp = s.rbox((0.017, 0.054, 0.0032), (-0.0895, 0, 0.0022), r=0.001, seg=1)
    for x in (-0.0945, -0.091, -0.0875):
        s.groove(crimp, "X", x, width=0.0008, depth=0.0004)
    s.part("Body", [tube, crimp])
    cap = s.rcyl(0.0122, 0.024, (0.092, 0, 0.0123), axis="X", seg=24, r=0.002)
    for k in range(5):
        s.groove(cap, "X", 0.084 + k * 0.0035, width=0.001, depth=0.0005)
    s.part("Accent", [cap])
    return export("item_toothpaste_model")


# --------------------------------------------------------------------------- rolls

def model_item_tape_model():
    """A roll of grey duct tape lying flat, 0.10 across, 0.048 wide: the tape wound on (Body) with its loose end lying
    out tangent on the floor, the cardboard core standing a hair proud of it (Accent)."""
    tape = poi.ring(0.05, 0.038, 0.048, (0, 0, 0.024), seg=40, r=0.0015)
    tail = poi.sweep_rect([(-0.0498, 0.012), (-0.0503, -0.01), (-0.0512, -0.03), (-0.0528, -0.046)], 0.0, 0.0474,
                          0.0012, r=0.0004)
    s.place(tail, rot=(0, -math.pi / 2, 0))                   # width X -> Z, the path's (y, z) -> world (y, -x)
    s.place(tail, (0, 0, 0.024))
    s.part("Body", [tape, tail])
    s.part("Accent", [poi.ring(0.0385, 0.0355, 0.0482, (0, 0, 0.0245), seg=40, r=0.0006)])
    return export("item_tape_model")


def model_item_toiletpaper_model():
    """One toilet roll lying on its side along X, 0.10 long, 0.11 across: the soft paper roll (Body) with its loose
    sheet hanging off the front down onto the floor, the cardboard core (Accent)."""
    zc = 0.055
    paper = poi.ring(0.055, 0.021, 0.1, (0, 0, zc), axis="X", seg=36, r=0.008)
    sheet = poi.sweep_rect([(-0.0538, 0.062), (-0.0549, 0.045), (-0.0562, 0.025), (-0.06, 0.009), (-0.07, 0.0018),
                            (-0.095, 0.0008), (-0.13, 0.0008)], 0.0, 0.096, 0.0015, r=0.0004)
    s.part("Body", [paper, sheet])
    s.part("Accent", [poi.ring(0.0215, 0.019, 0.101, (0, 0, zc), axis="X", seg=24, r=0.0006)])
    return export("item_toiletpaper_model")


def model_item_mop_model():
    """A string mop lying along X, 0.9 m: the handle with a hanging cap at -X and the head's clamp and collar (Body),
    the strands spilling out of the clamp and fanning over the floor at +X (Accent)."""
    r = 0.012
    handle = s.rcyl(r, 0.6, (-0.05, 0, r), axis="X", seg=16, r=0.003)                         # -0.35 .. 0.25
    cap = s.rcyl(0.0138, 0.03, (-0.347, 0, r), axis="X", seg=16, r=0.004)
    hole = poi.ring(0.009, 0.0045, 0.006, (-0.368, 0, r), axis="Y", seg=14, r=0.0015)         # the hanging loop
    collar = s.rcyl(0.017, 0.034, (0.255, 0, 0.017), axis="X", seg=18, r=0.004)
    clamp = s.rbox((0.046, 0.11, 0.036), (0.285, 0, 0.022), r=0.01, seg=2)
    s.part("Body", [handle, cap, hole, collar, clamp])
    strands = []
    n = 22
    for k in range(n):
        f = ((k * 7) % n + 0.5) / n * 2 - 1                      # -1 .. 1 across the clamp, layers interleaved
        upper = k % 2 == 1                                       # the upper layer flops over the lower one
        y0 = f * 0.045
        z0 = 0.03 if upper else 0.014
        length = 0.13 + 0.1 * rnd(k)
        spread = f * (0.06 + 0.05 * rnd(k + 40)) + (rnd(k + 80) - 0.5) * 0.05
        lift = 0.012 if upper else 0.0
        pts = [(0.282 + 0.008 * rnd(k + 20), y0, z0), (0.322, y0 * 1.2, z0 * 0.55 + 0.006 + lift * 0.5),
               (0.31 + length * 0.55, y0 + spread * 0.6, 0.009 + lift * 0.4), (0.3 + length, y0 + spread, 0.0085)]
        strands.append(s.tube(pts, 0.009, res=1, segments_per=2, radii=[1.0, 1.0, 1.0, 0.8]))
    s.part("Accent", strands, sharp_angle=60.0)
    return export("item_mop_model")


# --------------------------------------------------------------------------- soft things

BOOT = [  # (z, front x, back x, half width, n): the upper's horizontal sections, toe box flowing up into the shaft
    (0.026, 0.146, -0.126, 0.05, 2.8), (0.045, 0.152, -0.133, 0.055, 2.8), (0.06, 0.148, -0.135, 0.056, 2.7),
    (0.075, 0.136, -0.135, 0.056, 2.6), (0.09, 0.112, -0.134, 0.055, 2.5), (0.105, 0.078, -0.132, 0.053, 2.4),
    (0.12, 0.042, -0.13, 0.051, 2.4), (0.14, 0.008, -0.129, 0.049, 2.3), (0.165, -0.006, -0.128, 0.048, 2.3),
    (0.195, -0.008, -0.128, 0.049, 2.3), (0.208, -0.004, -0.132, 0.053, 2.3), (0.217, -0.008, -0.128, 0.049, 2.3),
    (0.214, -0.016, -0.12, 0.04, 2.3), (0.2, -0.022, -0.114, 0.033, 2.3)]   # padded collar folding into the opening


def _boot_front(z):
    for a, b in zip(BOOT, BOOT[1:10]):
        if a[0] <= z <= b[0]:
            return a[1] + (b[1] - a[1]) * (z - a[0]) / (b[0] - a[0])
    return BOOT[0][1]


def _boot(side):
    """One chunky boot standing on its sole, toe at +X (side = +1 / -1: right / left): one lofted upper from the
    rounded toe box up the instep into the padded shaft and its opening, on a thick lugged sole. Returns (upper,
    sole, laces) bmesh lists."""
    upper = s.loft([(z, (f - b) / 2, w, n, (f + b) / 2, 0.0) for z, f, b, w, n in BOOT], seg=24)
    s.groove(upper, "Z", 0.19, width=0.003, depth=0.0016)             # the collar's seam
    sole = xloft([(-0.138, 0.015, 0.05, 5.0, 0.015), (-0.1, 0.015, 0.058, 5.0, 0.015), (-0.03, 0.015, 0.06, 5.0, 0.015),
                  (0.04, 0.015, 0.061, 5.0, 0.015), (0.1, 0.015, 0.06, 5.0, 0.015), (0.158, 0.015, 0.046, 5.0, 0.015)],
                 seg=16, round_caps=0.013)
    for x in (-0.085, -0.03, 0.04, 0.1):
        s.groove(sole, "X", x, width=0.006, depth=0.0035)             # lugs
    pull = s.tube([(-0.12, 0, 0.18), (-0.136, 0, 0.198), (-0.132, 0, 0.228), (-0.117, 0, 0.214)], 0.0042,
                  res=1, segments_per=3)                              # the pull loop at the back
    laces = [s.rbox((0.006, 0.044, 0.006), (_boot_front(z) - 0.0016, 0, z), r=0.002, seg=1)
             for z in (0.1, 0.116, 0.133, 0.152, 0.172)]
    f = _boot_front(0.172)
    laces.append(s.tube([(f + 0.001, -0.012, 0.174), (f + 0.012, -0.024, 0.166), (f + 0.008, -0.032, 0.144)],
                        0.0022, res=1, segments_per=3))               # a loose lace end
    body, accent, trim = [upper], [sole, pull], laces
    if side < 0:
        for b in body + accent + trim:
            bmesh.ops.scale(b, vec=Vector((1, -1, 1)), verts=b.verts)
            bmesh.ops.reverse_faces(b, faces=b.faces)
    return body, accent, trim


def model_item_boots_model():
    """A pair of chunky shock-absorbing boots standing side by side, toes at +X, 0.30 x 0.25 x 0.22: leather uppers
    with a padded collar (Body), thick lugged soles and pull loops (Accent), laces (Trim)."""
    parts = {"Body": [], "Accent": [], "Trim": []}
    for side, y, yaw, dx in ((1, -0.062, math.radians(4), 0.0), (-1, 0.062, math.radians(-6), 0.014)):
        upper, accent, trim = _boot(side)
        for key, bms in (("Body", upper), ("Accent", accent), ("Trim", trim)):
            parts[key] += moved(bms, (dx, y, 0), (0, 0, yaw))
    for key, bms in parts.items():
        s.part(key, bms)
    return export("item_boots_model")


def _glove(hand):
    """A flat work glove lying palm down, fingers to +X, thumb toward hand * +Y: a padded palm, four flat fingers
    side by side, the thumb out at an angle; a flat gauntlet cuff. Returns (fabric, cuff)."""
    palm = xloft([(-0.07, 0.0105, 0.04, 2.6, 0.013, 0), (0.0, 0.0125, 0.046, 3.2, 0.0135, 0),
                  (0.032, 0.011, 0.045, 3.6, 0.012, 0)], seg=20)
    fabric = [palm]
    for y, length, spread in ((0.0335, 0.062, 0.003), (0.0112, 0.07, 0.001), (-0.0112, 0.066, -0.001),
                              (-0.0335, 0.052, -0.003)):
        y *= hand
        spread *= hand
        fabric.append(xloft([(0.02, 0.0082, 0.0114, 2.4, 0.009, y),
                             (0.02 + length * 0.55, 0.0078, 0.011, 2.3, 0.0083, y + spread * 0.55),
                             (0.02 + length, 0.007, 0.0098, 2.2, 0.0076, y + spread)], seg=10, round_caps=0.008))
    thumb = xloft([(0.0, 0.0088, 0.0125, 2.4, 0.0098, 0), (0.03, 0.0082, 0.0118, 2.3, 0.009, 0),
                   (0.055, 0.0074, 0.0104, 2.2, 0.0081, 0)], seg=10, round_caps=0.008)
    s.place(thumb, (-0.012, hand * 0.037, 0), rot=(0, 0, hand * math.radians(30)))
    fabric.append(thumb)
    cuff = xloft([(-0.129, 0.0095, 0.048, 3.0, 0.0125), (-0.126, 0.012, 0.052, 3.0, 0.0125),
                  (-0.09, 0.0118, 0.05, 3.0, 0.0122), (-0.052, 0.0118, 0.046, 3.0, 0.0122)], seg=20)
    s.groove(cuff, "X", -0.112, width=0.003, depth=0.0015)            # the hem
    s.groove(cuff, "X", -0.06, width=0.0024, depth=0.0012)            # where it is sewn on
    return fabric, [cuff]


def model_item_gloves_model():
    """A pair of work gloves lying flat side by side, fingers at +X, thumbs out, 0.25 x 0.23: the fabric hands
    (Body), the knitted cuffs (Accent). The right glove lies over the left one's edge."""
    body, accent = [], []
    for hand, y, z, yaw, dx in ((-1, -0.037, 0.0, math.radians(3), 0.0), (1, 0.037, 0.004, math.radians(-4), 0.012)):
        fabric, cuff = _glove(hand)
        body += moved(fabric, (dx, y, z), (0, 0, yaw))
        accent += moved(cuff, (dx, y, z), (0, 0, yaw))
    s.part("Body", body)
    s.part("Accent", accent)
    return export("item_gloves_model")


def model_item_socks_model():
    """A pair of clean socks rolled into each other, lying along X, 0.12 m: the soft cotton bundle with its turned-over
    cuff (Body), two stripes round the cuff and the coloured toe at +X (Accent)."""
    roll = xloft([(-0.058, 0.024, 0.036, 2.3, 0.025), (-0.02, 0.027, 0.041, 2.2, 0.028), (0.025, 0.026, 0.039, 2.2, 0.027),
                  (0.056, 0.021, 0.032, 2.4, 0.023)], seg=24, round_caps=0.017)
    s.groove(roll, "X", 0.028, width=0.0024, depth=0.0012)             # the toe seam showing through
    cuff = xloft([(-0.047, 0.0295, 0.0415, 2.3, 0.0275), (-0.026, 0.0305, 0.0435, 2.2, 0.0285),
                  (-0.004, 0.0305, 0.0438, 2.2, 0.0285)], seg=24, round_caps=0.003)
    s.part("Body", [roll, cuff])
    stripes = [xloft([(x - 0.003, 0.0313, 0.0446, 2.2, 0.0283), (x + 0.003, 0.0313, 0.0448, 2.2, 0.0285)], seg=24)
               for x in (-0.037, -0.023)]
    toe = s.ball(1.0, (0.0445, 0, 0.0232), seg=20, scale=(0.0135, 0.0329, 0.0219))      # the knitted-in toe colour
    s.part("Accent", stripes + [toe])
    return export("item_socks_model")


def model_item_soap_model():
    """A bar of soap lying flat, 0.09 x 0.06 x 0.032, softly domed (Body), a paper wrapper band round its middle
    (Accent)."""
    zc = 0.0166
    soap = xloft([(-0.045, 0.0138, 0.029, 3.2, zc), (0.0, 0.0155, 0.031, 3.0, zc), (0.045, 0.0138, 0.029, 3.2, zc)],
                 seg=28, round_caps=0.012)
    s.part("Body", [soap])
    band = xloft([(-0.017, 0.0163, 0.0318, 3.0, zc), (0.017, 0.0163, 0.0318, 3.0, zc)], seg=28)
    s.part("Accent", [band])
    return export("item_soap_model")


APPLE = [(0.0, -0.042), (0.008, -0.0415), (0.016, -0.039), (0.022, -0.0345), (0.0255, -0.028), (0.026, -0.022),
         (0.024, -0.0175), (0.018, -0.0145), (0.0135, -0.009), (0.0115, -0.002), (0.0118, 0.006), (0.0145, 0.0125),
         (0.02, 0.0165), (0.0265, 0.02), (0.0275, 0.026), (0.0265, 0.032), (0.022, 0.038), (0.014, 0.0425),
         (0.006, 0.0438), (0.0, 0.0425)]   # (radius, x) from the calyx end to the stem end, the stem sits in a dimple
SKIN_FROM = (-0.0178, 0.0195)               # the bitten middle lies between these x


def _apple_r(x):
    for (ra, xa), (rb, xb) in zip(APPLE, APPLE[1:]):
        if min(xa, xb) <= x <= max(xa, xb) and xa != xb:
            return ra + (rb - ra) * (x - xa) / (xb - xa)
    return 0.0


def _bitten(v_co, zc=0.0):
    """Scalloped bites round the waist (a factor on the radius at this vertex)."""
    x0, x1 = SKIN_FROM
    if not x0 < v_co.x < x1:
        return 1.0
    ang = math.atan2(v_co.z - zc, v_co.y)
    w = math.sin(math.pi * (v_co.x - x0) / (x1 - x0)) ** 0.7
    return 1 + w * (0.13 * math.sin(4 * ang + 1.3 + v_co.x * 90) + 0.06 * math.sin(7 * ang - 0.4))


def model_item_applecore_model():
    """An apple core lying on its side along X, 0.088 m: the bitten white flesh, a narrow scalloped waist between two
    round ends (Body), the red skin left on both ends (Accent), the stem in its dimple at +X, the dried calyx at -X and
    two seeds showing in the waist (Trim)."""
    zc = 0.0
    flesh = s.lathe(APPLE, seg=20, axis="X")
    for v in flesh.verts:
        k = _bitten(v.co, zc)
        v.co.y *= k
        v.co.z = zc + (v.co.z - zc) * k
    s.part("Body", [flesh], sharp_angle=50.0)
    x0, x1 = SKIN_FROM
    top = [(_apple_r(x1) + 0.0008, 0.0)] + [(r + 0.0008, x - x1) for r, x in APPLE if x > x1 + 1e-6 and r > 0]
    top.append((0.0, 0.0425 + 0.0008 - x1))
    bot = [(_apple_r(x0) + 0.0008, 0.0)] + [(r + 0.0008, x0 - x) for r, x in reversed(APPLE) if x < x0 - 1e-6 and r > 0]
    bot.append((0.0, x0 + 0.042 + 0.0008))
    skin = [s.lathe(top, seg=20, axis="X", loc=(x1, 0, zc)), s.lathe(bot, seg=20, axis="-X", loc=(x0, 0, zc))]
    s.part("Accent", skin, sharp_angle=50.0)
    stem = s.tube([(0.041, 0, zc), (0.05, 0.002, zc + 0.002), (0.058, 0.005, zc + 0.008)], 0.0018, res=1,
                  segments_per=3, radii=[1.0, 0.9, 0.8])
    calyx = s.rcyl(0.0055, 0.004, (-0.0418, 0, zc), axis="X", seg=5, r=0.001)
    seeds = [s.ball(0.0034, (x, 0.0106 * math.cos(a), 0.0106 * math.sin(a)), seg=8, scale=(1.7, 0.9, 0.9))
             for x, a in ((0.0, 0.5), (0.003, 2.4))]
    s.part("Trim", [stem, calyx, *seeds])
    return export("item_applecore_model")


def model_item_necklace_model():
    """A precious necklace laid out flat, 0.34 x 0.16: the gold chain in a teardrop loop with beaded links, the clasp
    at -X, the bail and the pendant's cup at +X (Body), the big cut gem in the pendant and two small stones on the
    chain (Accent)."""
    rc = 0.003
    loop = [(0.13, 0.0), (0.115, 0.022), (0.088, 0.048), (0.03, 0.07), (-0.05, 0.072), (-0.12, 0.052), (-0.158, 0.012),
            (-0.158, -0.012), (-0.12, -0.052), (-0.05, -0.072), (0.03, -0.07), (0.088, -0.048), (0.115, -0.022),
            (0.13, 0.0)]
    chain = s.tube([(x, y, rc) for x, y in loop], rc, res=1, segments_per=3)
    beads = []
    pts = [Vector((x, y)) for x, y in loop]
    total = sum((b - a).length for a, b in zip(pts, pts[1:]))
    for k in range(1, 17):                                    # beads along the chain (on the polyline)
        d = total * k / 17
        for a, b in zip(pts, pts[1:]):
            seg_len = (b - a).length
            if d <= seg_len:
                p = a.lerp(b, d / seg_len)
                beads.append(s.ball(0.0043, (p.x, p.y, 0.0043), seg=6, scale=(1, 1, 0.9)))
                break
            d -= seg_len
    clasp = s.rcyl(0.0055, 0.02, (-0.16, 0, 0.0055), axis="Y", seg=12, r=0.002)
    bail = poi.ring(0.008, 0.0048, 0.004, (0.136, 0, 0.008), axis="Y", seg=14, r=0.001)
    cup = s.lathe([(0.0, 0.0), (0.01, 0.0), (0.0155, 0.0035), (0.0168, 0.0085), (0.0148, 0.0093), (0.0, 0.0093)],
                  seg=24, loc=(0.158, 0, 0.0))
    s.part("Body", [chain, *beads, clasp, bail, cup])
    gem = s.lathe([(0.0, 0.003), (0.0142, 0.009), (0.0142, 0.0102), (0.0082, 0.0155), (0.0, 0.0155)], seg=10,
                  loc=(0.158, 0, 0.0))
    stones = [s.ball(0.0052, (0.1015, sgn * 0.035, 0.0052), seg=8, scale=(1, 1, 0.85)) for sgn in (-1, 1)]
    s.part("Accent", [gem, *stones], sharp_angle=20.0)
    return export("item_necklace_model")


def model_item_plush_model():
    """A plush teddy bear sitting, 0.20 wide, 0.27 tall, legs forward (-Y): a pear body, round head, arms resting on
    the legs (Body fur), the muzzle, the inner ears, the tummy and the foot pads (Accent), eyes and nose (Trim)."""
    body = [s.ball(0.07, (0, 0, 0.081), seg=16, scale=(1, 0.9, 1.15)),
            s.ball(0.058, (0, -0.005, 0.2), seg=16)]
    for sx in (-1, 1):
        body += [s.ball(0.021, (sx * 0.045, 0.0, 0.248), seg=12, scale=(1, 0.6, 1)),                       # ears
                 s.tube([(sx * 0.052, -0.004, 0.136), (sx * 0.074, -0.03, 0.095), (sx * 0.078, -0.052, 0.058)], 0.021,
                        res=3, segments_per=4, radii=[1.0, 1.0, 1.08]),                                        # arms
                 s.ball(0.023, (sx * 0.078, -0.054, 0.055), seg=12),                                         # paws
                 s.tube([(sx * 0.038, -0.015, 0.036), (sx * 0.05, -0.07, 0.03), (sx * 0.056, -0.11, 0.029)], 0.027,
                        res=3, segments_per=3),                                                                # legs
                 s.ball(0.032, (sx * 0.057, -0.118, 0.032), seg=14, scale=(0.95, 0.65, 1.0))]                # feet
    s.part("Body", body, sharp_angle=60.0)
    accent = [s.ball(0.027, (0, -0.056, 0.188), seg=14, scale=(1.15, 0.8, 0.85)),                    # muzzle
              s.ball(0.05, (0, -0.056, 0.082), seg=16, scale=(1.0, 0.35, 1.15))]                     # tummy
    for sx in (-1, 1):
        accent += [s.ball(0.0125, (sx * 0.045, -0.0095, 0.247), seg=10, scale=(1, 0.4, 1)),          # inner ears
                   s.ball(0.021, (sx * 0.057, -0.1385, 0.032), seg=12, scale=(1.0, 0.3, 1.15))]       # foot pads
    s.part("Accent", accent, sharp_angle=60.0)
    trim = [s.ball(0.0085, (0, -0.0765, 0.197), seg=10, scale=(1.3, 0.8, 0.9))]                      # nose
    trim += [s.ball(0.0072, (sx * 0.022, -0.0545, 0.215), seg=10) for sx in (-1, 1)]                 # eyes
    s.part("Trim", trim)
    return export("item_plush_model")


MODELS = {
    "item_radio_model": model_item_radio_model,
    "item_keyboard_model": model_item_keyboard_model,
    "item_tablet_model": model_item_tablet_model,
    "item_medkit_model": model_item_medkit_model,
    "item_coffee_model": model_item_coffee_model,
    "item_finder_model": model_item_finder_model,
    "item_boots_model": model_item_boots_model,
    "item_tomatoes_model": model_item_tomatoes_model,
    "item_chess_model": model_item_chess_model,
    "item_magazine_model": model_item_magazine_model,
    "item_pills_model": model_item_pills_model,
    "item_toothpaste_model": model_item_toothpaste_model,
    "item_tape_model": model_item_tape_model,
    "item_matches_model": model_item_matches_model,
    "item_toiletpaper_model": model_item_toiletpaper_model,
    "item_applecore_model": model_item_applecore_model,
    "item_soap_model": model_item_soap_model,
    "item_gloves_model": model_item_gloves_model,
    "item_necklace_model": model_item_necklace_model,
    "item_mop_model": model_item_mop_model,
    "item_plush_model": model_item_plush_model,
    "item_socks_model": model_item_socks_model,
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
