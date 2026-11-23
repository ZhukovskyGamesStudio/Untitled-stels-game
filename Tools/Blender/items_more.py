"""More pickup items as they lie in the world: a walkie-talkie, a keyboard, a rugged tablet, a first-aid box, a jar of
coffee, the fuel finder, a pair of boots, a tin of tomatoes, a folded chessboard, a magazine, pills, toothpaste, duct
tape, matches, a toilet roll, an apple core, soap, work gloves, a necklace, a mop, a teddy bear, rolled-up socks, an
SSD drive, a game controller, a gaming keyboard, a fuel canister and a roll of bandages; the upgraded tools (a power
screwdriver, a mechanical keyboard with a radio, a field tablet), a double espresso, spring boots, a gas mask, a
sledgehammer, a floor motion sensor, a chess-piece detector, the crafting parts (a blueprint, a circuit board, wires,
a chip) and collectible Staunton chess pieces (each exported twice: item_<piece>_w_model and _b_model).

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


# --------------------------------------------------------------------------- more loot

def model_item_ssd_model():
    """A 2.5-inch SSD drive lying flat, 0.10 x 0.07 x 0.007: the anodised case with one corner cut and the connector's
    notch in its -X end (Body), a label off to one side and a bold stripe across (Trim), the gold contacts in the notch
    and four case screws (Accent)."""
    L, W, H = 0.1, 0.07, 0.007
    case = s.rbox((L, W, H), (0, 0, H / 2), r=0.0018, seg=2)
    case = s.boolean(case, s.rbox((0.009, 0.05, 0.0042), (-L / 2 + 0.002, -0.004, 0.0029), r=0.0, seg=1))        # the notch
    case = s.boolean(case, s.rbox((0.022, 0.022, 0.02), (L / 2, W / 2, H / 2), r=0.0, seg=1, rot=(0, 0, math.pi / 4)))
    s.part("Body", [case])
    s.part("Trim", [s.rbox((0.044, 0.034, 0.0014), (0.02, -0.01, H - 0.0003), r=0.0004, seg=1),
                    s.rbox((0.009, W - 0.006, 0.0014), (-0.022, 0, H - 0.0003), r=0.0004, seg=1)])
    acc = [s.rbox((0.006, 0.046, 0.0016), (-L / 2 + 0.0058, -0.004, 0.0029), r=0.0003, seg=1)]                    # the contacts
    acc += [s.rcyl(0.0028, 0.0016, (x, y, H - 0.0003), seg=10, r=0.0004, bevel_seg=1)
            for x, y in ((-0.0435, 0.0285), (-0.0435, -0.0285), (0.0435, -0.0285), (0.031, 0.0285))]
    s.part("Accent", acc)
    return export("item_ssd_model")


def model_item_gamepad_model():
    """A game controller lying face up, 0.16 x 0.10 x 0.045, its grips toward -Y: one shell - a thick middle running
    out into the two grips (Body), two low thumbsticks, the d-pad, the shoulder triggers and the middle pad (Accent),
    four face buttons (Trim), the light bar on the back edge (Eye)."""
    pts = [(-0.064, -0.05, 0.02), (-0.058, -0.022, 0.022), (-0.047, 0.012, 0.024), (0.0, 0.016, 0.024),
           (0.047, 0.012, 0.024), (0.058, -0.022, 0.022), (0.064, -0.05, 0.02)]
    shell = s.skin(pts, [0.013, 0.018, 0.024, 0.021, 0.024, 0.018, 0.013], smooth_levels=2)
    s.place(shell, scale=(1.0, 1.0, 0.8))                                       # flatter than it is wide
    s.part("Body", [shell])
    acc = []
    for x in (-0.021, 0.021):                                                   # the thumbsticks, low
        acc.append(s.rcyl(0.007, 0.009, (x, -0.003, 0.0365), seg=14, r=0.001, bevel_seg=1))
        acc.append(s.ball(0.0095, (x, -0.003, 0.0425), seg=14, scale=(1.0, 1.0, 0.4)))
    a, b = 0.004, 0.0115
    plus = [(a, b), (-a, b), (-a, a), (-b, a), (-b, -a), (-a, -a), (-a, -b), (a, -b), (a, -a), (b, -a), (b, a), (a, a)]
    acc.append(s.place(prism(plus, 0.031, 0.0405, r=0.0008), (-0.048, 0.014, 0)))
    acc += [s.rbox((0.03, 0.014, 0.014), (sx * 0.045, 0.032, 0.0225), r=0.005, seg=2) for sx in (-1, 1)]    # triggers
    acc.append(s.rbox((0.03, 0.018, 0.004), (0, 0.017, 0.0345), r=0.001, seg=1))                            # the middle pad
    s.part("Accent", acc)
    s.part("Trim", [s.rcyl(0.0045, 0.009, (0.048 + dx, 0.014 + dy, 0.0365), seg=12, r=0.0012, bevel_seg=1)
                    for dx, dy in ((0.01, 0), (-0.01, 0), (0, 0.01), (0, -0.01))])
    s.part("Eye", [s.rbox((0.036, 0.008, 0.007), (0, 0.033, 0.021), r=0.002, seg=1)])
    return export("item_gamepad_model")


def model_item_gamekeyboard_model():
    """A compact gaming keyboard without a numpad lying flat, 0.36 x 0.13: a low case (Body), a glowing plate under the
    keys and a glowing line round the case (Eye), dark keycaps (Accent) with the pale gamer's keys - Esc, W A S D, the
    arrows (Trim). The space bar is at -Y (the front)."""
    total = 18.25 * KEY_U
    xs = -total / 2
    case = s.rbox((total + 0.014, 0.128, 0.017), (0, 0, 0.0105), r=0.005, seg=2)        # z 0.002 .. 0.019
    feet = [s.rbox((0.04, 0.012, 0.004), (x, 0.052, 0.002), r=0.0015, seg=1) for x in (-0.13, 0.13)]
    s.part("Body", [case, *feet])
    s.part("Eye", [s.rbox((total + 0.004, 0.118, 0.0024), (0, 0, 0.0196), r=0.0008, seg=1),       # under the keys
                   s.rbox((total + 0.0164, 0.1304, 0.003), (0, 0, 0.0075), r=0.0012, seg=1)])       # round the case
    z0 = 0.0202
    y_of = lambda r: -0.0505 + r * KEY_U  # noqa: E731  (row 0 = space row at the front)
    dark, pale = [], []

    def row(r, widths, x_off=0.0, light=()):
        x = xs + x_off * KEY_U
        n = 0
        for w in widths:
            if w > 0:
                (pale if n in light else dark).append(_keycap(x + w * KEY_U / 2, y_of(r), w, 1.0, z0))
                n += 1
            x += abs(w) * KEY_U

    row(0, [1.25, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25])
    row(1, [2.25] + [1] * 10 + [2.75])
    row(2, [1.75] + [1] * 11 + [2.25], light=(1, 2, 3))            # A S D
    row(3, [1.5] + [1] * 12 + [1.5], light=(2,))                   # W
    row(4, [1] * 13 + [2])
    f_row = 5.3
    row(f_row, [1, -1, 1, 1, 1, 1, -0.5, 1, 1, 1, 1, -0.5, 1, 1, 1, 1], light=(0,))   # Esc
    nav = 15.25
    row(f_row, [1, 1, 1], nav)
    row(4, [1, 1, 1], nav)
    row(3, [1, 1, 1], nav)
    row(1, [-1, 1], nav, light=(0,))                               # the arrows
    row(0, [1, 1, 1], nav, light=(0, 1, 2))
    s.part("Accent", dark)
    s.part("Trim", pale)
    return export("item_gamekeyboard_model")


def model_item_canister_model():
    """A steel fuel canister standing, 0.29 x 0.15 x 0.47, taller than wide: the pressed body with its welded seam, a
    thick cross stamped in both faces, the top with its shoulder sloping down to the spout's side, three carrying bars
    and the spout's neck (Body), the screw cap with its lever (Accent), a hazard diamond on the front face (Trim)."""
    W, D, H = 0.29, 0.15, 0.36
    tank = s.rbox((W, D, H), (0, 0, H / 2), r=0.022, seg=3)
    s.groove(tank, "Y", 0.0, width=0.006, depth=0.003)                                   # the welded seam
    tilt = 0.55
    body = [tank,
            s.rbox((0.19, D - 0.012, 0.07), (-0.045, 0, H + 0.018), r=0.02, seg=3),                        # under the bars
            s.rbox((0.15, D - 0.012, 0.07), (0.075, 0, H - 0.004), r=0.02, seg=3, rot=(0, tilt, 0))]       # the shoulder
    for sy in (-1, 1):                                                                   # the stamped crosses
        for a in (0.82, -0.82):
            body.append(s.rbox((0.3, 0.012, 0.03), (0, sy * (D / 2 - 0.002), 0.18), r=0.004, seg=1, rot=(0, a, 0)))
    top = H + 0.085
    for y in (-0.04, 0.0, 0.04):                                                         # three bars: two carry it together
        body.append(s.tube([(-0.125, y, H + 0.03), (-0.122, y, top - 0.012), (-0.105, y, top), (-0.0, y, top),
                            (0.017, y, top - 0.012), (0.02, y, H + 0.03)], 0.0085, res=1, smooth=True, segments_per=5))
    ax = (math.sin(tilt), 0.0, math.cos(tilt))
    neck = (0.092, 0.0, H + 0.035)
    body.append(s.rcyl(0.021, 0.06, neck, seg=18, r=0.002, bevel_seg=1, rot=(0, tilt, 0)))
    s.part("Body", body)
    cap = tuple(neck[k] + ax[k] * 0.034 for k in range(3))
    lug = tuple(neck[k] + ax[k] * 0.048 for k in range(3))
    s.part("Accent", [s.rcyl(0.027, 0.022, cap, seg=18, r=0.004, bevel_seg=2, rot=(0, tilt, 0)),
                      s.rbox((0.012, 0.06, 0.008), lug, r=0.003, seg=1, rot=(0, tilt, 0))])     # the cap's lever
    s.part("Trim", [s.rbox((0.085, 0.006, 0.085), (0, -(D / 2 + 0.004), 0.18), r=0.0015, seg=1, rot=(0, math.pi / 4, 0))])
    return export("item_canister_model")


def _ribbon(xs, zs, half_w, t):
    """A strip of cloth along X through (x, z) points, half_w to each side in Y, t thick."""
    bm = bmesh.new()
    rings = [[bm.verts.new((x, -half_w, z)), bm.verts.new((x, half_w, z)), bm.verts.new((x, half_w, z + t)),
              bm.verts.new((x, -half_w, z + t))] for x, z in zip(xs, zs)]
    for q, r in zip(rings, rings[1:]):
        for k in range(4):
            bm.faces.new((q[k], q[(k + 1) % 4], r[(k + 1) % 4], r[k]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def model_item_bandage_model():
    """A roll of gauze bandage lying on its side, 0.07 wide and 0.045 across (its axis along Y), its loose end run off
    from under it along +X as a ribbon as wide as the roll with one soft wave: the wound roll with the turns showing on
    its ends and the ribbon (Body), two red stripes round the roll (Accent)."""
    R, L = 0.0225, 0.07
    turns = []
    r, up = 0.0085, True
    while r < R - 0.003:                                           # the turns of the gauze, seen on the roll's ends
        turns.append((r, 0.0012 if up else 0.0))
        r += 0.0018
        up = not up
    prof = [(0.0, 0.006), (0.0055, 0.006), (0.0065, 0.0)] + turns + [(R - 0.0015, 0.0), (R, 0.002), (R, L - 0.002), (R - 0.0015, L)]
    prof += [(r, L - z) for r, z in reversed(turns)] + [(0.0065, L), (0.0055, L - 0.006), (0.0, L - 0.006)]
    roll = s.lathe(prof, seg=32, axis="Y", loc=(0, -L / 2, R))
    n = 18
    xs = [-0.006 + 0.094 * k / (n - 1) for k in range(n)]
    zs = [0.0005 + 0.006 * math.sin(math.pi * min(1.0, max(0.0, (x - 0.038) / 0.036))) ** 2 for x in xs]
    s.part("Body", [roll, _ribbon(xs, zs, L / 2 - 0.003, 0.0016)])
    s.part("Accent", [s.rcyl(R + 0.0007, 0.007, (0, y, R), axis="Y", seg=32, r=0.0004, bevel_seg=1) for y in (-0.012, 0.012)])
    return export("item_bandage_model")


# --------------------------------------------------------------------------- upgraded tools, gadgets, parts

def _flute(bm, x0, x1, zc, n, depth, phase=0.0):
    """Presses n soft flutes into a round part along X between x0 and x1 (the axis at y = 0, z = zc)."""
    for v in bm.verts:
        if x0 < v.co.x < x1:
            t = (v.co.x - x0) / (x1 - x0)
            ang = math.atan2(v.co.z - zc, v.co.y)
            k = 1 - depth * max(0.0, math.cos(n * ang + phase)) ** 1.5 * math.sin(math.pi * t) ** 0.5
            v.co.y *= k
            v.co.z = zc + (v.co.z - zc) * k
    return bm


def model_item_proscrewdriver_model():
    """A cordless power screwdriver lying along X, 0.25 m, the bit at +X: the orange housing swelling over the motor
    and narrowing to the nose (Body), the black battery pack at the butt and the ribbed rubber grip with the
    forward/reverse rocker (Accent), the brass ratchet collar with its knurling and a brass band (Trim), the steel hex
    chuck and a Phillips bit (TrimSteel), the LED ring round the nose (EyeLed)."""
    R = 0.0245
    zc = R
    x0 = -0.125
    housing = s.lathe([(0.0, 0.042), (0.0215, 0.0425), (0.0222, 0.046), (0.0212, 0.05), (0.0212, 0.118),
                       (0.0228, 0.13), (0.0226, 0.142), (0.0198, 0.155), (0.0178, 0.165), (0.0172, 0.168),
                       (0.0, 0.1682)], seg=32, axis="X", loc=(x0, 0, zc))
    s.part("Body", [housing])
    battery = s.lathe([(0.0, 0.0), (0.015, 0.0004), (0.021, 0.0035), (0.0245, 0.011), (0.0245, 0.04), (0.0232, 0.046),
                       (0.0, 0.0462)], seg=32, axis="X", loc=(x0, 0, zc))
    s.groove(battery, "X", x0 + 0.03, width=0.002, depth=0.001)               # the pack's release seam
    grip = s.rcyl(0.0226, 0.062, (x0 + 0.086, 0, zc), axis="X", seg=32, r=0.004, bevel_seg=2)
    _flute(grip, x0 + 0.057, x0 + 0.115, zc, 8, 0.07)
    rocker = s.rbox((0.022, 0.011, 0.009), (x0 + 0.135, 0, zc + 0.0215), r=0.003, seg=2, rot=(0, math.radians(-8), 0))
    s.part("Accent", [battery, grip, rocker])
    collar = s.lathe([(0.0, 0.0), (0.0185, 0.0), (0.0198, 0.0015), (0.0198, 0.0145), (0.0175, 0.017), (0.0, 0.017)],
                     seg=32, axis="X", loc=(x0 + 0.166, 0, zc))
    _flute(collar, x0 + 0.166, x0 + 0.183, zc, 16, 0.06)
    band = s.rcyl(0.0249, 0.006, (x0 + 0.0445, 0, zc), axis="X", seg=32, r=0.0012, bevel_seg=1)
    s.part("Trim", [collar, band])
    chuck = s.rcyl(0.0085, 0.03, (x0 + 0.196, 0, zc), axis="X", seg=6, r=0.0012, bevel_seg=1,
                   rot=(math.pi / 6, 0, 0))
    shank = s.rcyl(0.0042, 0.03, (x0 + 0.222, 0, zc), axis="X", seg=6, r=0.0005, bevel_seg=1)
    tip = s.loft([(0.0, 0.0042, 0.0042, 2.0), (0.009, 0.0034, 0.0034, 2.0), (0.0145, 0.0011, 0.0011, 2.0)],
                 seg=8, axis="X", loc=(x0 + 0.2355, 0, zc))
    for k in range(4):                                                        # the Phillips cross
        a = k * math.pi / 2 + math.pi / 4
        for v in tip.verts:
            ang = math.atan2(v.co.z - zc, v.co.y)
            if abs(math.atan2(math.sin(ang - a), math.cos(ang - a))) < 0.3 and v.co.x > x0 + 0.24:
                f = 0.55
                v.co.y *= f
                v.co.z = zc + (v.co.z - zc) * f
    s.part("TrimSteel", [chuck, shank, tip])
    s.part("EyeLed", [poi.ring(0.0186, 0.0128, 0.003, (x0 + 0.1675, 0, zc), axis="X", seg=24, r=0.0006)])
    return export("item_proscrewdriver_model")


def model_item_prokeyboard_model():
    """A compact mechanical keyboard lying flat, 0.38 m with its radio: a 60 % case on a raised back (Body), tall
    keycaps with a glowing plate under them and a light strip round the case (Eye), the space bar at -Y; a radio
    dongle plugged into its +X end with a stubby folding antenna lying along +X (Accent with the keycaps), the
    dongle's ring LED (EyeLed)."""
    total = 15 * KEY_U
    xs = -total / 2
    case = s.rbox((total + 0.014, 0.105, 0.02), (0, 0, 0.012), r=0.006, seg=2, taper=(1.0, 0.97))   # z 0.002 .. 0.022
    s.groove(case, "Z", 0.01, width=0.0016, depth=0.0008)
    back = s.rbox((total + 0.01, 0.018, 0.024), (0, 0.044, 0.014), r=0.006, seg=2)
    feet = [s.rbox((0.04, 0.012, 0.004), (x, 0.04, 0.002), r=0.0015, seg=1) for x in (-0.1, 0.1)]
    s.part("Body", [case, back, *feet])
    s.part("Eye", [s.rbox((total + 0.002, 0.0965, 0.0024), (0, -0.002, 0.0222), r=0.0008, seg=1),
                   s.rbox((total + 0.0164, 0.1074, 0.003), (0, 0, 0.0055), r=0.0012, seg=1)])
    z0 = 0.0228
    y_of = lambda r: -0.0405 + r * KEY_U  # noqa: E731
    keys = []

    def row(r, widths):
        x = xs
        for w in widths:
            if w > 0:
                keys.append(_keycap(x + w * KEY_U / 2, y_of(r), w, 1.0, z0))
            x += abs(w) * KEY_U

    row(0, [1.25, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25])
    row(1, [2.25] + [1] * 10 + [2.75])
    row(2, [1.75] + [1] * 11 + [2.25])
    row(3, [1.5] + [1] * 12 + [1.5])
    row(4, [1] * 13 + [2])
    xe = total / 2 + 0.007
    dongle = s.rbox((0.03, 0.024, 0.013), (xe + 0.012, 0.02, 0.0115), r=0.004, seg=2)
    hinge = s.rcyl(0.0068, 0.016, (xe + 0.03, 0.02, 0.0115), axis="Y", seg=14, r=0.0018)
    antenna = s.rcyl(0.0055, 0.062, (xe + 0.062, 0.02, 0.0115), axis="X", seg=12, r=0.002, radius2=0.0042)
    tip = s.ball(0.0052, (xe + 0.093, 0.02, 0.0115), seg=12)
    s.part("Accent", keys + [dongle, hinge, antenna, tip])
    s.part("EyeLed", [s.rcyl(0.0032, 0.003, (xe + 0.01, 0.02, 0.0182), seg=12, r=0.0008)])
    return export("item_prokeyboard_model")


def model_item_protablet_model():
    """A field-upgraded rugged tablet lying face up, 0.30 x 0.21: the case inside a continuous rubber bumper with
    thick corners and grip ridges (Body), the bezel, a sensor pod clamped to its -X side with a lens, and a whip
    antenna hinged at the +X +Y corner, folded back along the +Y edge (Accent), the pod's lens (EyeSensor), the screen face (EyeScreen: its own 0..1
    UVs, u along +X, v along +Y, like the plain tablet)."""
    dz = 0.0015
    case = s.rbox((0.25, 0.175, 0.02), (0, 0, 0.01 + dz), r=0.008, seg=3)
    s.groove(case, "Z", 0.008 + dz, width=0.0016, depth=0.0008)
    bumper_ring = []
    for sy in (-1, 1):                                                       # the long sides of the bumper frame
        bumper_ring.append(s.rbox((0.21, 0.016, 0.024), (0, sy * 0.0905, 0.012), r=0.006, seg=2))
    for sx in (-1, 1):
        bumper_ring.append(s.rbox((0.016, 0.15, 0.024), (sx * 0.1305, 0, 0.012), r=0.006, seg=2))
    corners = [s.rbox((0.054, 0.054, 0.0275), (sx * 0.12, sy * 0.0835, 0.01375), r=0.012, seg=3)
               for sx in (-1, 1) for sy in (-1, 1)]
    ridges = [s.rbox((0.006, 0.006, 0.026), (x, sy * 0.0985, 0.012), r=0.0022, seg=1)
              for x in (-0.06, -0.045, -0.03, 0.03, 0.045, 0.06) for sy in (-1, 1)]
    keys = [s.rbox((0.016, 0.006, 0.006), (x, 0.0985, 0.02), r=0.0022, seg=1) for x in (-0.012, 0.008)]
    s.part("Body", [case, *bumper_ring, *corners, *ridges, *keys])
    bezel = s.rbox((0.226, 0.152, 0.0045), (0, 0, 0.02 + dz), r=0.0018, seg=2)
    button = s.rcyl(0.005, 0.0016, (0.104, 0, 0.0224 + dz), seg=14, r=0.0005)
    pod = s.rbox((0.034, 0.06, 0.03), (-0.148, 0.025, 0.015), r=0.008, seg=2)              # sensor pod on the -X side
    clamp = s.rbox((0.03, 0.07, 0.008), (-0.132, 0.025, 0.028), r=0.003, seg=1)             # its clamp over the bumper
    lens_ring = poi.ring(0.0115, 0.0075, 0.005, (-0.165, 0.025, 0.015), axis="X", seg=20, r=0.0012)
    hinge = s.rcyl(0.0085, 0.02, (0.128, 0.1, 0.017), seg=16, r=0.002)                        # folded along +Y
    whip = s.rcyl(0.0052, 0.085, (0.128 - 0.0425, 0.1065, 0.017), axis="X", seg=10, r=0.0015, radius2=0.0062)
    whip_tip = s.ball(0.0046, (0.128 - 0.087, 0.1065, 0.017), seg=10)
    s.part("Accent", [bezel, button, pod, clamp, lens_ring, hinge, whip, whip_tip])
    s.part("EyeSensor", [s.lathe([(0.0085, 0.0), (0.008, 0.003), (0.005, 0.0055), (0.0, 0.0065)], seg=16, axis="-X",
                                 loc=(-0.163, 0.025, 0.015))])
    screen = s.part("EyeScreen", [s.rbox((0.19, 0.13, 0.0015), (0, 0, 0.0226 + dz), r=0.0005, seg=1)])
    unit_uvs(screen)
    return export("item_protablet_model")


def model_item_espresso_model():
    """A double espresso to go lying on its side along X, 0.105 m, the lid at +X (as the coffee jar lies): a small
    insulated paper cup (Body), its card sleeve (Trim) with two bold stripes round it - the 'double' (TrimBand), the
    snap-on lid with the sip spout (Accent)."""
    zc = 0.039
    cup = s.lathe([(0.0, 0.0), (0.028, 0.0), (0.0302, 0.002), (0.0315, 0.006), (0.0362, 0.088), (0.0372, 0.092),
                   (0.0, 0.0922)], seg=32, axis="X", loc=(-0.05, 0, zc))
    s.part("Body", [cup])
    # the sleeve follows the cup's taper: r 0.0322 .. 0.0352 between 0.018 and 0.075 from the base
    sleeve = s.lathe([(0.0, 0.018), (0.0333, 0.018), (0.0337, 0.0195), (0.0365, 0.0735), (0.0362, 0.075),
                      (0.0, 0.075)], seg=32, axis="X", loc=(-0.05, 0, zc))
    s.part("Trim", [sleeve])
    sleeve_r = lambda z: 0.0337 + 0.0519 * (z - 0.0195) + 0.0008  # noqa: E731  (just proud of the sleeve)
    bands = [s.lathe([(0.0, z), (sleeve_r(z), z), (sleeve_r(z + 0.006), z + 0.006), (0.0, z + 0.006)],
                     seg=32, axis="X", loc=(-0.05, 0, zc)) for z in (0.034, 0.046)]
    s.part("TrimBand", bands)
    lid = s.lathe([(0.0, 0.0), (0.0385, 0.0), (0.0392, 0.0025), (0.0388, 0.006), (0.034, 0.0075), (0.033, 0.0105),
                   (0.0, 0.0108)], seg=32, axis="X", loc=(-0.05 + 0.0895, 0, zc))
    spout = s.rbox((0.006, 0.014, 0.008), (-0.05 + 0.1025, 0, zc + 0.024), r=0.0025, seg=2)
    s.part("Accent", [lid, spout])
    return export("item_espresso_model")


def _helix(cx, cy, r, z0, z1, turns, per_turn=10, phase=0.0):
    n = int(turns * per_turn) + 1
    return [(cx + r * math.cos(phase + 2 * math.pi * turns * k / (n - 1)),
             cy + r * math.sin(phase + 2 * math.pi * turns * k / (n - 1)),
             z0 + (z1 - z0) * k / (n - 1)) for k in range(n)]


def model_item_jumpboots_model():
    """A pair of spring boots standing side by side, toes at +X, 0.31 x 0.26 x 0.29: the boots' leather uppers (Body)
    lifted on their lugged soles (Accent) over two coil springs each with a piston inside (TrimSpring, steel) onto a
    thick rubber foot plate (Accent), laces (Trim)."""
    lift = 0.062
    parts = {"Body": [], "Accent": [], "Trim": [], "TrimSpring": []}
    for side, y, yaw, dx in ((1, -0.064, math.radians(4), 0.0), (-1, 0.064, math.radians(-6), 0.014)):
        upper, accent, trim = _boot(side)
        for b in upper + accent + trim:
            s.place(b, (0, 0, lift))
        foot = xloft([(-0.13, 0.011, 0.048, 5.0, 0.011), (-0.03, 0.011, 0.056, 5.0, 0.011),
                      (0.06, 0.011, 0.057, 5.0, 0.011), (0.15, 0.011, 0.044, 5.0, 0.011)], seg=16, round_caps=0.009)
        tread = [s.rbox((0.012, 0.09, 0.006), (x, 0, 0.003), r=0.002, seg=1) for x in (-0.1, -0.06, 0.04, 0.08, 0.12)]
        springs = []
        for x in (-0.075, 0.075):
            springs.append(s.tube(_helix(x, 0, 0.029, 0.016, lift + 0.006, 3.5, 7), 0.0048, res=1, smooth=False))
            springs.append(s.rcyl(0.011, lift - 0.006, (x, 0, (lift + 0.016) / 2), seg=12, r=0.002, bevel_seg=1))  # piston
            springs.append(s.rcyl(0.0175, 0.008, (x, 0, lift + 0.002), seg=12, r=0.002, bevel_seg=1))           # top cup
            springs.append(s.rcyl(0.0175, 0.008, (x, 0, 0.02), seg=12, r=0.002, bevel_seg=1))                   # bottom cup
        for key, bms in (("Body", upper), ("Accent", accent + [foot] + tread), ("Trim", trim), ("TrimSpring", springs)):
            parts[key] += moved(bms, (dx, y, 0), (0, 0, yaw))
    for key, bms in parts.items():
        s.part(key, bms)
    return export("item_jumpboots_model")


def _interp(pts, x):
    for (x0, v0), (x1, v1) in zip(pts, pts[1:]):
        if x <= x1:
            t = max(0.0, (x - x0) / (x1 - x0))
            return v0 + (v1 - v0) * (0.5 - 0.5 * math.cos(math.pi * t))
    return pts[-1][1]


GASMASK_H = [(-0.115, 0.03), (-0.085, 0.07), (-0.055, 0.086), (-0.02, 0.074), (0.02, 0.066), (0.06, 0.06),
             (0.095, 0.042), (0.115, 0.024)]     # the face piece's height along X (chin -> forehead)
GASMASK_W = [(-0.115, 0.05), (-0.07, 0.075), (-0.02, 0.09), (0.04, 0.1), (0.09, 0.09), (0.115, 0.07)]   # half width
GASMASK_N = 2.3


def _mask_z(x, y):
    hz, hy = _interp(GASMASK_H, x), _interp(GASMASK_W, x)
    q = max(0.0, 1 - abs(y / hy) ** GASMASK_N)
    return hz * q ** (1 / GASMASK_N)


def _mask_frame(x, y):
    """The point on the face piece above (x, y) and the Euler rotation taking +Z onto its outward normal."""
    d = 0.002
    z = _mask_z(x, y)
    n = Vector((-(_mask_z(x + d, y) - _mask_z(x - d, y)) / (2 * d), -(_mask_z(x, y + d) - _mask_z(x, y - d)) / (2 * d),
                1.0)).normalized()
    return Vector((x, y, z)), Vector((0, 0, 1)).rotation_difference(n).to_euler(), n


def model_item_gasmask_model():
    """A gas mask lying face up, chin at -X, forehead at +X, 0.25 x 0.24 x 0.10: the rubber face piece rising to the
    snout over the mouth, with a flat lip round its edge (Body), two big round eye lenses (Accent, tinted glass) in
    pressed rims, the speech diaphragm on the snout and the filter's threaded port (Trim), the round filter canister
    screwed on at the left cheek, pointing out to the side (AccentFilter), the head harness straps lying out round it
    (BodyStrap: rubber like the face)."""
    xs = [-0.115 + 0.23 * k / 16 for k in range(17)]
    face = xloft([(x, _interp(GASMASK_H, x), _interp(GASMASK_W, x), GASMASK_N, 0.0) for x in xs], seg=28,
                 round_caps=0.01)
    lip = xloft([(x, 0.012, _interp(GASMASK_W, x) + 0.008, GASMASK_N, 0.0) for x in xs], seg=28, round_caps=0.008)
    for bm, floor in ((face, -0.003), (lip, -0.0015)):                 # flat backs: it lies on its edge
        for v in bm.verts:
            v.co.z = max(v.co.z, floor)
    s.part("Body", [face, lip])
    lenses, trim = [], []
    for sy in (-1, 1):
        p, rot, n = _mask_frame(0.045, sy * 0.046)
        trim.append(s.place(poi.ring(0.031, 0.0225, 0.012, seg=28, r=0.002), p + n * 0.001, rot))
        lenses.append(s.place(s.rcyl(0.0235, 0.008, seg=28, r=0.0015), p + n * 0.0005, rot))
    s.part("Accent", lenses)
    p, rot, n = _mask_frame(-0.06, 0.0)                                  # the speech diaphragm on the snout
    trim.append(s.place(s.lathe([(0.025, -0.006), (0.025, 0.008), (0.021, 0.012), (0.0, 0.0125)], seg=24), p, rot))
    trim += [s.place(s.rbox((0.003, 0.03, 0.003), (-0.008 + k * 0.0055, 0, 0.0125), r=0.0008, seg=1), p, rot)
             for k in range(4)]
    p, rot, n = _mask_frame(-0.05, -0.068)                               # the filter port on the left cheek
    out = Vector((-0.25, -1.0, 0.25)).normalized()
    out_rot = Vector((0, 0, 1)).rotation_difference(out).to_euler()
    trim.append(s.place(s.rcyl(0.021, 0.034, seg=24, r=0.003), p + out * 0.004, out_rot))
    s.part("Trim", trim)
    canister = s.lathe([(0.0, 0.0), (0.04, 0.0), (0.043, 0.003), (0.043, 0.036), (0.04, 0.04), (0.0, 0.04)], seg=32)
    for zz in (0.009, 0.015, 0.021, 0.027, 0.033):
        s.groove(canister, "Z", zz, width=0.0018, depth=0.0009)
    s.place(canister, p + out * 0.016, out_rot)
    holes = s.place(s.rcyl(0.013, 0.004, seg=16, r=0.001), p + out * 0.057, out_rot)
    s.part("AccentFilter", [canister, holes])
    straps = []
    for sy in (-1, 1):
        straps.append(s.tube([(0.085, sy * 0.088, 0.01), (0.105, sy * 0.1, 0.004), (0.13, sy * 0.07, 0.003),
                              (0.14, 0.0, 0.003)], 0.004, res=1, segments_per=4))
        straps.append(s.tube([(-0.065, sy * 0.082, 0.01), (-0.085, sy * 0.1, 0.003), (-0.115, sy * 0.095, 0.003)],
                             0.004, res=1, segments_per=4))
    s.part("BodyStrap", straps)
    return export("item_gasmask_model")


def model_item_hammer_model():
    """A sledgehammer lying on its side, 0.9 m, the head at +X: an oval hickory handle swelling to a knob at the butt
    and thickening into the head's eye (Body), the forged steel head with chamfered striking faces (Accent), the black
    rubber grip at the butt and the wedge in the eye (Trim)."""
    hh = 0.046                                                           # the head's half height: the axis height
    handle = xloft([(-0.45, 0.019, 0.016, 2.2, hh), (-0.44, 0.022, 0.018, 2.2, hh), (-0.425, 0.019, 0.015, 2.2, hh),
                    (-0.2, 0.017, 0.0135, 2.2, hh), (0.15, 0.0175, 0.0135, 2.2, hh), (0.3, 0.021, 0.015, 2.2, hh),
                    (0.36, 0.024, 0.016, 2.2, hh), (0.4, 0.024, 0.016, 2.2, hh)], seg=20, round_caps=0.006)
    s.part("Body", [handle])
    head = s.loft([(-0.09, 0.037, 0.037, 7.0), (-0.083, 0.044, 0.044, 7.0), (-0.055, 0.046, 0.046, 7.0),
                   (-0.025, 0.042, 0.044, 6.0), (0.025, 0.042, 0.044, 6.0), (0.055, 0.046, 0.046, 7.0),
                   (0.083, 0.044, 0.044, 7.0), (0.09, 0.037, 0.037, 7.0)], seg=24, axis="Y", loc=(0.38, 0, hh))
    s.groove(head, "Y", 0.07, width=0.004, depth=0.0018)
    s.groove(head, "Y", -0.07, width=0.004, depth=0.0018)
    s.part("Accent", [head])
    grip = xloft([(-0.428, 0.0205, 0.0165, 2.2, hh), (-0.3, 0.0188, 0.015, 2.2, hh)], seg=20, round_caps=0.004)
    for x in (-0.4, -0.38, -0.36, -0.34, -0.32):
        s.groove(grip, "X", x, width=0.0035, depth=0.0012)
    wedge = s.rbox((0.034, 0.006, 0.006), (0.38, 0, 2 * hh - 0.0015), r=0.0015, seg=1)
    s.part("Trim", [grip, wedge])
    return export("item_hammer_model")


def model_item_robosensor_model():
    """A portable motion sensor for robots standing on the floor, 0.18 m tall: a squat weighted base on three short
    splayed legs, a short neck and a wide head housing (Body), the faceted PIR dome on top (Accent, milky plastic),
    the legs' rubber feet and a bright collar under the dome (Trim), the status LED on the head (EyeLed)."""
    up = 0.014
    base = s.lathe([(0.0, 0.028 + up), (0.052, 0.028 + up), (0.058, 0.033 + up), (0.06, 0.045 + up),
                    (0.056, 0.058 + up), (0.045, 0.064 + up), (0.03, 0.068 + up), (0.0, 0.068 + up)], seg=32)
    s.groove(base, "Z", 0.047 + up, width=0.0025, depth=0.0012)
    neck = s.rcyl(0.015, 0.026, (0, 0, 0.076 + up), seg=20, r=0.003)
    head = s.lathe([(0.0, 0.084 + up), (0.03, 0.084 + up), (0.042, 0.089 + up), (0.047, 0.099 + up),
                    (0.047, 0.115 + up), (0.0, 0.115 + up)], seg=32)
    s.groove(head, "Z", 0.104 + up, width=0.002, depth=0.001)
    legs = []
    for k in range(3):
        a = 2 * math.pi * k / 3 + math.pi / 2
        c, sn = math.cos(a), math.sin(a)
        legs.append(s.tube([(0.03 * c, 0.03 * sn, 0.034 + up), (0.06 * c, 0.06 * sn, 0.026), (0.074 * c, 0.074 * sn, 0.012)],
                           0.0065, res=1, segments_per=3))
    s.part("Body", [base, neck, head, *legs])
    feet = []
    for k in range(3):
        a = 2 * math.pi * k / 3 + math.pi / 2
        feet.append(s.rcyl(0.012, 0.012, (0.075 * math.cos(a), 0.075 * math.sin(a), 0.006), seg=14, r=0.003))
    collar = s.rcyl(0.0465, 0.006, (0, 0, 0.115 + up), seg=32, r=0.0015)
    s.part("Trim", [*feet, collar])
    dome = s.lathe([(0.042, 0.114 + up), (0.042, 0.122 + up), (0.039, 0.136 + up), (0.031, 0.149 + up),
                    (0.018, 0.159 + up), (0.0, 0.1625 + up)], seg=12)
    s.part("Accent", [dome])
    s.part("EyeLed", [s.rcyl(0.0045, 0.004, (0, -0.0465, 0.1075 + up), axis="Y", seg=12, r=0.001)])
    return export("item_robosensor_model")


KNIGHT_SIDE = [  # the knight's head in profile (forward u, up v), muzzle at +u
    (-0.018, 0.028), (-0.0205, 0.048), (-0.0195, 0.064), (-0.016, 0.079), (-0.0105, 0.091), (-0.0075, 0.1),
    (-0.004, 0.109), (0.0005, 0.1045), (0.005, 0.0985), (0.0115, 0.095), (0.0195, 0.089), (0.0265, 0.0805),
    (0.0315, 0.0725), (0.0335, 0.066), (0.0305, 0.0605), (0.0235, 0.0615), (0.0205, 0.0565), (0.0125, 0.0525),
    (0.0115, 0.046), (0.016, 0.038), (0.0185, 0.028)]


def model_item_chessdetector_model():
    """A handheld chess-piece detector lying on its side (the pistol's flank up), 0.24 m, the scan dish at +X: a
    pistol body with a rubber grip falling to -Y (Body), the trigger, the grip's battery cap and the scanner dish's
    collar (Accent), a coil ring in the dish, a gold knight emblem on the flank and the dish's rim (Trim), the small
    screen at the back of the barrel and a LED (Eye)."""
    zc = 0.03
    barrel = xloft([(-0.1, 0.017, 0.024, 3.0, zc), (-0.07, 0.02, 0.027, 3.2, zc), (0.0, 0.02, 0.028, 3.4, zc),
                    (0.05, 0.018, 0.024, 3.2, zc), (0.075, 0.016, 0.019, 3.0, zc)], seg=24, round_caps=0.008)
    s.groove(barrel, "Z", zc, width=0.0016, depth=0.0008)                    # clamshell seam
    grip = s.loft([(0.0, 0.016, 0.022, 3.0), (0.04, 0.017, 0.02, 3.0), (0.075, 0.019, 0.021, 3.0)], seg=20,
                  round_caps=0.007)
    s.place(grip, rot=(math.pi / 2, 0, 0))                                   # along -Y
    s.place(grip, rot=(0, 0, math.radians(-14)))
    s.place(grip, (-0.06, -0.01, zc))
    guard = s.tube([(-0.035, -0.022, zc), (-0.02, -0.05, zc), (0.0, -0.05, zc), (0.01, -0.024, zc)], 0.0045,
                   res=1, segments_per=4)
    s.part("Body", [barrel, grip, guard])
    trigger = s.rbox((0.009, 0.022, 0.012), (-0.024, -0.034, zc), r=0.003, seg=2, rot=(0, 0, math.radians(-10)))
    cap = s.place(s.rbox((0.036, 0.008, 0.034), r=0.004, seg=2), (-0.06 - 0.0775 * math.sin(math.radians(14)),
                                                                  -0.01 - 0.0775 * math.cos(math.radians(14)), zc),
                  (0, 0, math.radians(-14)))
    collar = s.rcyl(0.021, 0.018, (0.08, 0, zc), axis="X", seg=24, r=0.003, radius2=0.027)
    dish = s.lathe([(0.0, 0.0), (0.03, 0.0), (0.03, 0.006), (0.028, 0.007), (0.012, 0.004), (0.0, 0.0035)], seg=32,
                   axis="X", loc=(0.088, 0, zc))
    s.part("Accent", [trigger, cap, collar, dish])
    coil = poi.ring(0.022, 0.016, 0.004, (0.094, 0, zc), axis="X", seg=28, r=0.0008)
    rim = poi.ring(0.0305, 0.0272, 0.004, (0.095, 0, zc), axis="X", seg=32, r=0.0008)
    knight = prism([(u * 0.5, (v - 0.069) * 0.5) for u, v in KNIGHT_SIDE], 0.0, 0.0018, r=0.0005)
    bmesh.ops.triangulate(knight, faces=[f for f in knight.faces if len(f.verts) > 4], ngon_method="EAR_CLIP")
    s.place(knight, (0.01, 0.0, zc + 0.0188))
    s.part("Trim", [coil, rim, knight])
    screen = s.rbox((0.026, 0.018, 0.002), (-0.082, 0.0, zc + 0.0168), r=0.0006, seg=1)
    led = s.rcyl(0.0024, 0.002, (-0.05, 0.012, zc + 0.0196), seg=10, r=0.0006)
    s.part("Eye", [screen, led])
    return export("item_chessdetector_model")


def model_item_blueprint_model():
    """A technical blueprint sheet lying flat, 0.30 x 0.20, two opposite corners curling up (Body, blue paper), a
    border, a title block and a faint floor plan in thin white lines on its flat middle (Accent)."""
    L, W, T = 0.3, 0.2, 0.0007
    sheet = s.rbox((L, W, T), (0, 0, T / 2), r=0.0, seg=1)
    slice_grid(sheet, "X", [-0.15 + k * 0.015 for k in range(1, 20)])
    slice_grid(sheet, "Y", [-0.1 + k * 0.0125 for k in range(1, 16)])
    for v in sheet.verts:
        for sx in (-1, 1):
            t = 1 + (sx * v.co.x - 0.15) / 0.07 + (sx * v.co.y - 0.1) / 0.05   # 0 on the curl's line, 1 at the corner
            if t > 0 and sx * v.co.y > 0:
                v.co.z += 0.02 * t ** 2
    s.part("Body", [sheet], sharp_angle=80.0)
    lw = 0.0012
    z = T
    lines = []

    def seg(x0, y0, x1, y1, w=lw):
        i = len(lines) % 7                                   # touching lines never share a plane (z-fighting):
        h, w = 0.0006 + 0.0002 * i, 0.001 + 0.0002 * i       # each a little higher and wider than its neighbour
        lines.append(s.rbox((abs(x1 - x0) + w, abs(y1 - y0) + w, h), ((x0 + x1) / 2, (y0 + y1) / 2, z), r=0.0))

    # border (inside the flat part: the curled corners are at +X+Y and -X-Y)
    x0, x1, y0, y1 = -0.125, 0.125, -0.075, 0.08
    seg(x0, y1, x1 - 0.04, y1)
    seg(x0 + 0.04, y0, x1, y0)
    seg(x0, y0 + 0.035, x0, y1)
    seg(x1, y0, x1, y1 - 0.035)
    # title block at the lower right
    seg(0.06, y0, 0.06, -0.035)
    seg(0.06, -0.035, x1, -0.035)
    seg(0.07, -0.05, 0.115, -0.05, 0.0008)
    # a floor plan: an outer wall, inner walls with door gaps
    seg(-0.1, 0.07, 0.04, 0.07)
    seg(-0.1, -0.05, -0.1, 0.07)
    seg(-0.1, -0.05, 0.04, -0.05)
    seg(0.04, -0.05, 0.04, 0.07)
    seg(-0.03, 0.07, -0.03, 0.03)
    seg(-0.03, 0.005, -0.03, -0.05)
    seg(-0.1, 0.0, -0.06, 0.0)
    seg(-0.045, 0.0, -0.03, 0.0)
    seg(-0.03, 0.02, 0.0, 0.02)
    seg(0.018, 0.02, 0.04, 0.02)
    s.part("Accent", lines, flat=True)
    return export("item_blueprint_model")


def model_item_circuit_model():
    """A green circuit board lying flat, 0.14 x 0.07: the board with its mounting holes (Body), black chips and a pin
    header (Accent), aluminium capacitors and a crystal (Trim), gold edge contacts along -X (TrimGold), a red LED
    (EyeLed)."""
    L, W, T = 0.14, 0.07, 0.0016
    board = s.rbox((L, W, T), (0, 0, T / 2), r=0.0006, seg=1)
    for sx in (-1, 1):
        for sy in (-1, 1):
            board = s.boolean(board, s.rcyl(0.0018, 0.01, (sx * 0.064, sy * 0.029, 0.0), seg=12, r=0.0))
    s.part("Body", [board])
    acc = [s.rbox((0.024, 0.024, 0.003), (0.008, 0.004, T + 0.0012), r=0.0006, seg=1),           # the processor
           s.rbox((0.018, 0.009, 0.0024), (-0.028, 0.018, T + 0.001), r=0.0005, seg=1),
           s.rbox((0.018, 0.009, 0.0024), (-0.028, -0.008, T + 0.001), r=0.0005, seg=1),
           s.rbox((0.011, 0.007, 0.002), (0.044, -0.022, T + 0.0008), r=0.0004, seg=1),
           s.rbox((0.04, 0.0055, 0.0085), (0.03, 0.0305, T + 0.0038), r=0.0006, seg=1)]              # pin header
    for yc in (0.018, -0.008):                                              # the two small chips' legs
        for k in range(6):
            for sy in (-1, 1):
                acc.append(s.rbox((0.0011, 0.0026, 0.0012), (-0.028 - 0.0055 + k * 0.0022, yc + sy * 0.0052,
                                                             T + 0.0002), r=0.0))
    s.part("Accent", acc)
    trim = []
    for x, y, r, h in ((0.05, 0.012, 0.004, 0.009), (0.058, 0.002, 0.003, 0.007), (-0.052, -0.022, 0.0035, 0.008),
                       (0.03, -0.018, 0.0028, 0.006)):
        trim.append(s.lathe([(0.0, 0.0), (r, 0.0), (r, h - 0.0008), (r - 0.0006, h), (0.0, h)], seg=14,
                            loc=(x, y, T - 0.0003)))
    trim.append(s.rbox((0.009, 0.0035, 0.0032), (-0.006, -0.022, T + 0.0013), r=0.0012, seg=1))   # crystal
    s.part("Trim", trim)
    s.part("TrimGold", [s.rbox((0.0065, 0.0028, 0.0008), (-L / 2 + 0.0045, -0.026 + k * 0.0042, T - 0.0001), r=0.0)
                        for k in range(13)])
    s.part("EyeLed", [s.rbox((0.003, 0.002, 0.0016), (0.058, -0.026, T + 0.0006), r=0.0004, seg=1)])
    return export("item_circuit_model")


def model_item_wires_model():
    """A tangled bundle of wires lying on the floor, about 0.09 m across: three insulated wires in loose loops - red
    (Body), blue (Accent), yellow (AccentYellow) - their stripped copper ends (TrimCopper), a black cable tie
    holding the bundle in the middle (Trim)."""
    wires = {"Body": [], "Accent": [], "AccentYellow": []}
    ends = []
    r = 0.0026
    for k, key in enumerate(("Body", "Accent", "AccentYellow")):
        pts = []
        n = 15
        ph = k * 2.1
        for j in range(n):
            t = j / (n - 1) * 2 * math.pi * 1.6 + ph
            rad = 0.026 + 0.01 * math.sin(2.3 * t + k)
            pts.append((rad * math.cos(t) * 1.2, rad * math.sin(t), 0.008 + 0.006 * math.sin(3 * t + k) + 0.002 * k))
        pts[n // 2] = (0.0, 0.0, 0.009 + 0.0035 * (k - 1))                 # all pass through the tie
        wires[key].append(s.tube(pts, r, res=1, segments_per=4))
        for p, q in ((pts[0], pts[1]), (pts[-1], pts[-2])):
            d = (Vector(p) - Vector(q)).normalized()
            ends.append(s.tube([tuple(Vector(p) - d * 0.002), tuple(Vector(p) + d * 0.008)], 0.0012, res=1,
                               smooth=False))
    for key, bms in wires.items():
        s.part(key, bms)
    s.part("TrimCopper", ends)
    tie = poi.ring(0.0105, 0.0078, 0.005, (0.0, 0.0, 0.009), axis="X", seg=16, r=0.0008)
    head = s.rbox((0.006, 0.007, 0.006), (0.0, 0.0, 0.0205), r=0.0012, seg=1)
    s.part("Trim", [tie, head])
    return export("item_wires_model")


def model_item_chip_model():
    """A single processor chip lying flat, 0.04 x 0.04: the black epoxy package (Body) on gull-wing gold pins along
    all four sides (Trim), the silver heat spreader on top with a pin-1 dot (Accent)."""
    P, H = 0.03, 0.004
    pkg = s.rbox((P, P, H), (0, 0, 0.001 + H / 2), r=0.0006, seg=1)
    s.part("Body", [pkg])
    pins = []
    n = 7
    for k in range(n):
        c = -P / 2 + 0.003 + k * (P - 0.006) / (n - 1)
        for side in range(4):
            prof = s.tube([(P / 2 - 0.001, 0, 0.0025), (P / 2 + 0.0018, 0, 0.0022), (P / 2 + 0.0026, 0, 0.0005),
                           (P / 2 + 0.0048, 0, 0.0003)], 0.0004, res=1, smooth=False)
            s.place(prof, (0, 0, 0))
            s.place(prof, rot=(0, 0, side * math.pi / 2))
            off = Vector((0, c, 0)) if side % 2 == 0 else Vector((c, 0, 0))
            s.place(prof, tuple(off))
            pins.append(prof)
    s.part("Trim", pins)
    s.part("Accent", [s.rbox((0.02, 0.02, 0.0009), (0, 0, 0.001 + H + 0.0002), r=0.0004, seg=1),
                      s.rcyl(0.0012, 0.0006, (-0.011, -0.011, 0.001 + H), seg=10, r=0.0002)])
    return export("item_chip_model")


# --------------------------------------------------------------------------- collectible chess pieces (Staunton)

def _chess_base(r, h):
    """The turned foot every piece stands on: a bevelled plinth, a bead and a cove, up to height h (r: its radius)."""
    k = r / 0.026
    return [(0.0, 0.0), (r, 0.0), (r * 1.015, h * 0.12), (r * 0.87, h * 0.24), (r * 0.77, h * 0.33),
            (r * 0.8, h * 0.44), (r * 0.78, h * 0.5), (r * 0.62, h * 0.6)], k


def _felt(r):
    return s.rcyl(r * 0.93, 0.0026, (0, 0, 0.0011), seg=28, r=0.0006, bevel_seg=1)


def _chess_piece(kind):
    """bmesh lists (body, felt) of one piece, scaled to its height (pawn 0.08 … king 0.14 m)."""
    body = []
    if kind == "pawn":
        prof, _ = _chess_base(0.022, 0.03)
        prof += [(0.0105, 0.021), (0.0085, 0.032), (0.0072, 0.044), (0.0112, 0.0465), (0.0118, 0.049),
                 (0.0075, 0.051), (0.0, 0.0512)]
        body = [s.lathe(prof, seg=24), s.ball(0.0135, (0, 0, 0.0635), seg=20)]
        height, r = 0.08, 0.022
    elif kind == "rook":
        prof, _ = _chess_base(0.026, 0.034)
        prof += [(0.0145, 0.024), (0.0125, 0.06), (0.0158, 0.0645), (0.0168, 0.068), (0.0158, 0.07), (0.0178, 0.072),
                 (0.0182, 0.097), (0.0, 0.097)]
        tower = s.lathe(prof, seg=24)
        tower = s.boolean(tower, s.rcyl(0.0124, 0.02, (0, 0, 0.1), seg=24, r=0.0))
        for a in (0.0, math.pi / 2):
            tower = s.boolean(tower, s.rbox((0.05, 0.0062, 0.014), (0, 0, 0.1), r=0.0, rot=(0, 0, a + math.pi / 4)))
        s.bevel_edges(tower, 0.0007, 1, angle=50)
        body = [tower]
        height, r = 0.1, 0.026
    elif kind == "bishop":
        prof, _ = _chess_base(0.025, 0.034)
        prof += [(0.0135, 0.026), (0.0098, 0.045), (0.0078, 0.06), (0.0122, 0.063), (0.0128, 0.0655), (0.0082, 0.0675),
                 (0.0092, 0.07), (0.0122, 0.0765), (0.0128, 0.084), (0.0108, 0.0925), (0.0066, 0.0995),
                 (0.0022, 0.1025), (0.0, 0.103)]
        mitre = s.lathe(prof, seg=24)
        mitre = s.boolean(mitre, s.rbox((0.03, 0.04, 0.0028), (0.012, 0, 0.0905), r=0.0, rot=(0, math.radians(-42), 0)))
        s.bevel_edges(mitre, 0.0005, 1, angle=50)
        body = [mitre, s.ball(0.0036, (0, 0, 0.1055), seg=12)]
        height, r = 0.11, 0.025
    elif kind == "queen":
        prof, _ = _chess_base(0.028, 0.036)
        prof += [(0.0145, 0.028), (0.0108, 0.05), (0.0086, 0.074), (0.0142, 0.0775), (0.0148, 0.0805), (0.0098, 0.083),
                 (0.0102, 0.087), (0.0138, 0.1), (0.016, 0.1095), (0.0148, 0.1115), (0.009, 0.1125), (0.0068, 0.1145),
                 (0.0, 0.1155)]
        crown = [s.lathe(prof, seg=24)]
        crown += [s.ball(0.0029, (0.0148 * math.cos(2 * math.pi * k / 8), 0.0148 * math.sin(2 * math.pi * k / 8), 0.1115),
                         seg=10) for k in range(8)]
        crown.append(s.ball(0.0058, (0, 0, 0.1205), seg=14))
        body = crown
        height, r = 0.13, 0.028
    elif kind == "king":
        prof, _ = _chess_base(0.029, 0.037)
        prof += [(0.015, 0.029), (0.0112, 0.052), (0.009, 0.078), (0.0148, 0.0815), (0.0154, 0.0845), (0.0102, 0.087),
                 (0.0106, 0.091), (0.0142, 0.104), (0.0158, 0.112), (0.0128, 0.1145), (0.0062, 0.1165), (0.0062, 0.119),
                 (0.0045, 0.1205), (0.0, 0.1207)]
        body = [s.lathe(prof, seg=24),
                s.rbox((0.0058, 0.0052, 0.024), (0, 0, 0.13), r=0.0012, seg=1),
                s.rbox((0.0158, 0.0052, 0.0058), (0, 0, 0.1335), r=0.0012, seg=1)]
        height, r = 0.14, 0.029
    else:  # knight
        prof, _ = _chess_base(0.026, 0.034)
        prof += [(0.0155, 0.026), (0.0152, 0.03), (0.0178, 0.0325), (0.0168, 0.035), (0.0, 0.035)]
        base = s.lathe(prof, seg=24)
        head = prism(KNIGHT_SIDE, -0.0135, 0.0135, r=0.0055, seg=3)
        bmesh.ops.triangulate(head, faces=[f for f in head.faces if len(f.verts) > 4], ngon_method="EAR_CLIP")
        s.place(head, rot=(math.pi / 2, 0, 0))                    # outline (u, v) in XY -> XZ, thickness along Y
        for v in head.verts:                                       # the muzzle narrower than the jowls, the ears thin
            k = 1 - 0.38 * max(0.0, (v.co.x - 0.006) / 0.028) - 0.35 * max(0.0, (v.co.z - 0.096) / 0.013)
            v.co.y *= max(0.45, k)
        mane = s.tube([(-0.0178, 0.0, 0.042), (-0.0182, 0.0, 0.062), (-0.0138, 0.0, 0.082), (-0.0078, 0.0, 0.095)],
                      0.0052, res=1, segments_per=4)
        eyes = [s.ball(0.0024, (0.0115, sy * 0.0105, 0.084), seg=10) for sy in (-1, 1)]
        nostrils = [s.ball(0.0018, (0.0295, sy * 0.0062, 0.0695), seg=8) for sy in (-1, 1)]
        body = [base, head, mane, *eyes, *nostrils]
        height, r = 0.105, 0.026
    felt = [_felt(r)]
    top = max(v.co.z for b in body for v in b.verts)
    k = height / top
    for b in body + felt:
        s.place(b, scale=(k, k, k))
    for b in body:                                                 # the felt is let into the plinth's underside
        s.place(b, (0, 0, 0.0008 * k))
    return body, felt


def _chess_model(kind, colour):
    body, felt = _chess_piece(kind)
    s.part("Body", body)
    s.part("Accent", felt)
    return export(f"item_{kind}_{colour}_model")


for _kind in ("pawn", "knight", "bishop", "rook", "queen", "king"):
    for _colour in ("w", "b"):
        globals()[f"model_item_{_kind}_{_colour}_model"] = (lambda k=_kind, c=_colour: _chess_model(k, c))


NEW_MODELS = {
    "item_proscrewdriver_model": model_item_proscrewdriver_model,
    "item_prokeyboard_model": model_item_prokeyboard_model,
    "item_protablet_model": model_item_protablet_model,
    "item_espresso_model": model_item_espresso_model,
    "item_jumpboots_model": model_item_jumpboots_model,
    "item_gasmask_model": model_item_gasmask_model,
    "item_hammer_model": model_item_hammer_model,
    "item_robosensor_model": model_item_robosensor_model,
    "item_chessdetector_model": model_item_chessdetector_model,
    "item_blueprint_model": model_item_blueprint_model,
    "item_circuit_model": model_item_circuit_model,
    "item_wires_model": model_item_wires_model,
    "item_chip_model": model_item_chip_model,
}
for _kind in ("pawn", "knight", "bishop", "rook", "queen", "king"):
    for _colour in ("w", "b"):
        NEW_MODELS[f"item_{_kind}_{_colour}_model"] = globals()[f"model_item_{_kind}_{_colour}_model"]


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
    "item_ssd_model": model_item_ssd_model,
    "item_gamepad_model": model_item_gamepad_model,
    "item_gamekeyboard_model": model_item_gamekeyboard_model,
    "item_canister_model": model_item_canister_model,
    "item_bandage_model": model_item_bandage_model,
}
MODELS.update(NEW_MODELS)


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
