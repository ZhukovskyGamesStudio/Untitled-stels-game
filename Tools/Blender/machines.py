"""Machines one can tell at a glance: an arcade game, a snack machine and what it sells, the electrical panel on a corridor wall.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/machines.py -- Assets/_Game/Art/Models [names]

Front to Blender -Y (Unity +Z), origin on the floor in the middle of the footprint (the panel: on the wall, the middle of its
bottom edge). Parts by what paints them (AssetBuilder.MachinePaint): Body, Accent, Print… (machines.png), Glow… (shines
through machine_glow.png), Lit… (machines.png in a lit case), Pane… (glass). Where each picture lies: machine_atlas.py.

- arcade_cabinet  0.72 x 0.86 x 1.76: side panels with the game's art, a screen set back and leaning, a lit marquee that
                  overhangs it, a control panel with a stick and buttons, a coin door
- vending_window  0.9 x 0.83 x 1.9: a case behind glass, five shelves of five spirals, price tags, a keypad, note and coin
                  slots, a flap; SLOTS below is where AssetBuilder puts the goods
- vend_<kind>_<k> a spiral's goods (three behind each other): bag, bar, can, bottle
- electric_panel  0.62 x 0.9 x 0.17
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import machine_atlas as at  # noqa: E402
import meshfix  # noqa: E402
import printed  # noqa: E402
import rooms_fun as rf  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))


def uv(cell, u, v, table=at.PRINT):
    """The atlas point of (u right, v up, 0..1) of a cell."""
    x0, y0, x1, y1 = table[cell]
    e = at.EDGE
    return ((x0 + e + min(max(u, 0.0), 1.0) * (x1 - x0 - 2 * e)) / at.SIZE, 1 - (y1 - e - min(max(v, 0.0), 1.0) * (y1 - y0 - 2 * e)) / at.SIZE)


def plain(name):
    return uv(name, 0.5, 0.5)


def export(name, floor=True):
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=floor, snap=None, name=name)
    path = os.path.join(ROOT, "Props", name + ".fbx")
    s.export_fbx(path, objs)
    print("exported", name)


class Plate:
    """A flat plate (a printed or glowing sheet) standing at `at`, facing -Y, leant back by `lean`: its UV rule."""

    def __init__(self, cell, centre, w, h, lean=0.0, table=at.PRINT, thick=0.006, rest="black"):
        self.cell, self.c, self.w, self.h, self.table, self.rest = cell, Vector(centre), w, h, table, rest
        self.right = Vector((1, 0, 0))
        self.up = Vector((0, math.sin(lean), math.cos(lean)))
        self.n = Vector((0, -math.cos(lean), math.sin(lean)))
        self.bm = s.rbox((w, thick, h), centre, r=min(0.002, thick * 0.3), seg=1, rot=(-lean, 0, 0))

    def owns(self, c):
        d = c - self.c
        return abs(d.dot(self.right)) < self.w / 2 + 0.002 and abs(d.dot(self.up)) < self.h / 2 + 0.002 and abs(d.dot(self.n)) < 0.012

    def at(self, n, co):
        if n.dot(self.n) < 0.7:
            return plain(self.rest) if self.table is at.PRINT else uv(self.cell, 0.02, 0.02, self.table)
        d = co - self.c
        return uv(self.cell, d.dot(self.right) / self.w + 0.5, d.dot(self.up) / self.h + 0.5, self.table)


def plates(name, list_, extra=(), colour="black"):
    """One object of plates and of other shapes: extra is (bmesh, rule(n, c, co) or a swatch's name) pairs."""
    import bpy
    obj = None
    parts = [(p.bm, lambda n, c, co, p=p: p.at(n, co)) for p in list_] + [(b, r) for b, r in extra]
    made = []
    for i, (bm, rule) in enumerate(parts):
        o = s.part(f"{name}_{i}", [bm])
        printed.paint(o, (lambda n, c, co, r=rule: plain(r)) if isinstance(rule, str) else rule)
        made.append(o)
    for o in bpy.context.scene.objects:
        o.select_set(o in made)
    bpy.context.view_layer.objects.active = made[0]
    for o in made:                                             # the weighted normals first, then one object
        bpy.context.view_layer.objects.active = o
        for m in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.context.view_layer.objects.active = made[0]
    bpy.ops.object.join()
    obj = made[0]
    obj.name = obj.data.name = name
    return obj


# ------------------------------------------------------------------------------------------------- the arcade game

def arcade_cabinet():
    # the side's outline (y, z), clockwise from the toe: the front, the control panel's shelf, the screen's lean, the hood
    side = [(-0.30, 0.0), (-0.30, 0.80), (-0.46, 0.90), (-0.46, 0.965), (-0.22, 1.045), (-0.085, 1.50), (-0.33, 1.555), (-0.31, 1.745), (0.28, 1.76), (0.40, 1.60), (0.40, 0.0)]
    inner = [(-0.275, 0.06), (-0.275, 0.80), (-0.435, 0.905), (-0.435, 0.945), (-0.19, 1.03), (-0.045, 1.50), (-0.30, 1.565), (-0.285, 1.73), (0.27, 1.74), (0.385, 1.59), (0.385, 0.06)]
    s.part("Body", [rf.prism(inner, "X", -0.335, 0.335, bevel=0.004), s.rbox((0.62, 0.6, 0.08), (0, 0.06, 0.04), r=0.006, seg=1)])
    sides = []
    for x in (-1, 1):
        bm = rf.prism(side, "X", x * 0.34 - 0.012, x * 0.34 + 0.012, bevel=0.003)

        def art(n, c, co, x=x):
            if n.x * x < 0.8:
                return plain("violet")
            t = (co.y + 0.46) / 0.86
            return uv("arcade_side", t if x > 0 else 1 - t, co.z / 1.76)
        sides.append((bm, art))
    # the T-moulding: a bright edge round both sides
    trims = []
    for x in (-1, 1):
        ring = side + [side[0]]
        pts = [Vector((x * 0.354, y, z)) for y, z in ring[1:-1]]
        trims.append((s.tube([tuple(p) for p in pts], 0.006, smooth=False, segments_per=1), "cap"))
    lean = math.atan2(0.135, 0.455)                            # of the screen
    panel_lean = math.pi / 2 - math.atan2(0.085, 0.245)          # of the control panel (nearly flat)
    up = Vector((0, math.sin(panel_lean), math.cos(panel_lean)))
    n = Vector((0, -math.cos(panel_lean), math.sin(panel_lean)))
    mid = Vector((0, -0.3125, 0.9875)) + n * 0.002
    print_ = [Plate("arcade_panel", tuple(mid), 0.64, 0.232, panel_lean), Plate("arcade_front", (0, -0.277, 0.58), 0.6, 0.36), Plate("coin", (0, -0.277, 0.24), 0.26, 0.26),
              Plate("bezel", (0, -0.2005, 1.085), 0.62, 0.07, lean)]

    def on_panel(x, t, h):
        return mid + Vector((x, 0, 0)) + up * t + n * h
    extra = list(sides) + trims
    for x, colour in ((0.055, "cap"), (0.135, "yellow"), (0.215, "blue")):
        for h, r in ((0.004, 0.019), (0.012, 0.014)):
            b = s.rcyl(r, 0.012, (0, 0, 0), axis="Z", seg=18, r=0.003)
            s.place(b, tuple(on_panel(x, 0.015, h)), (math.pi / 2 - panel_lean, 0, 0))
            extra.append((b, colour if r < 0.018 else "black"))
    for x in (-0.06, 0.0):
        b = s.rcyl(0.011, 0.01, (0, 0, 0), axis="Z", seg=12, r=0.002, bevel_seg=1)
        s.place(b, tuple(on_panel(x + 0.15, 0.085, 0.004)), (math.pi / 2 - panel_lean, 0, 0))
        extra.append((b, "white"))
    extra.append((s.ball(0.021, tuple(on_panel(-0.145, 0.015, 0.085)), seg=18), "cap"))
    plates("Print", print_, extra)
    accent = []
    b = s.rcyl(0.032, 0.008, (0, 0, 0), axis="Z", seg=20, r=0.003)
    accent.append(s.place(b, tuple(on_panel(-0.145, 0.015, 0.003)), (math.pi / 2 - panel_lean, 0, 0)))
    accent.append(rf.rod(tuple(on_panel(-0.145, 0.015, 0.0)), tuple(on_panel(-0.145, 0.015, 0.08)), 0.0065, seg=10))
    # the bezel round the screen, the frames of the coin door and of the marquee, the speaker's slots, the feet
    sc = Vector((0, -0.118, 1.29))
    sup, sn = Vector((0, math.sin(lean), math.cos(lean))), Vector((0, -math.cos(lean), math.sin(lean)))
    for x in (-1, 1):
        accent.append(s.rbox((0.075, 0.03, 0.43), tuple(sc + Vector((x * 0.295, 0, 0)) + sn * 0.012), r=0.006, seg=2, rot=(-lean, 0, 0)))
    for t in (-1, 1):
        accent.append(s.rbox((0.66, 0.03, 0.05), tuple(sc + sup * (t * 0.205) + sn * 0.012), r=0.006, seg=2, rot=(-lean, 0, 0)))
    accent += [s.rbox((0.66, 0.02, 0.016), (0, -0.318, z), r=0.004, seg=1) for z in (1.565, 1.735)]
    accent += [s.rbox((0.016, 0.02, 0.18), (x * 0.322, -0.316, 1.65), r=0.004, seg=1) for x in (-1, 1)]
    accent += [s.rbox((0.30, 0.016, 0.014), (0, -0.284, 0.24 + t * 0.14), r=0.003, seg=1) for t in (-1, 1)]
    accent += [s.rbox((0.014, 0.016, 0.29), (t * 0.143, -0.284, 0.24), r=0.003, seg=1) for t in (-1, 1)]
    accent += [s.rcyl(0.03, 0.03, (x, y, 0.013), seg=12, r=0.004, bevel_seg=1) for x in (-0.28, 0.28) for y in (-0.2, 0.32)]
    for k in range(8):                                         # under the hood
        y = -0.11 - 0.022 * (k // 2)
        accent.append(s.rbox((0.16, 0.008, 0.012), (-0.16 if k % 2 else 0.16, y, 1.5 + (-0.045 - y) * 0.255), r=0.002, seg=1))
    s.part("Accent", accent)
    plates("Glow", [Plate("screen", tuple(sc + sn * 0.004), 0.53, 0.375, lean, at.GLOW), Plate("marquee", (0, -0.312, 1.65), 0.63, 0.155, -0.1, at.GLOW)],
           [(s.rbox((0.012, 0.006, 0.03), (x, -0.29, 0.275), r=0.001, seg=1), lambda n, c, co: uv("coinslot", 0.5, 0.5, at.GLOW)) for x in (-0.04, 0.04)])
    export("arcade_cabinet")


# ------------------------------------------------------------------------------------------------- the snack machine

WIN = (-0.41, 0.205, 0.47, 1.70)                               # the window: x, z
BACK = 0.12                                                    # the case's back
SHELVES = (0.475, 0.735, 0.985, 1.225, 1.455)
COLUMNS = [WIN[0] + 0.0615 + 0.123 * k for k in range(5)]
FRONT = -0.335                                                 # of the shelves


def vending_window():
    x0, x1, z0, z1 = WIN
    shell = s.rbox((0.9, 0.8, 1.86), (0, 0, 0.97), r=0.014, seg=3)
    shell = s.boolean(shell, s.rbox((x1 - x0, 0.6, z1 - z0), ((x0 + x1) / 2, BACK - 0.3, (z0 + z1) / 2), r=0.004, seg=1))
    shell = s.boolean(shell, s.rbox((0.56, 0.1, 0.17), ((x0 + x1) / 2, -0.385, 0.24), r=0.004, seg=1))       # the pickup's mouth
    s.bevel_edges(shell, 0.003, 1, angle=60)
    s.part("Body", [shell])
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    accent = [s.rbox((0.045, 0.03, z1 - z0 + 0.07), (x, -0.408, cz), r=0.008, seg=2) for x in (x0 - 0.005, x1 + 0.005)]
    accent += [s.rbox((x1 - x0 + 0.05, 0.03, 0.045), (cx, -0.408, z), r=0.008, seg=2) for z in (z0 - 0.005, z1 + 0.005)]
    accent += [s.rbox((0.2, 0.022, 0.95), (0.33, -0.403, 1.2), r=0.008, seg=2)]                              # the column
    accent += [s.rbox((0.6, 0.03, 0.03), (cx, -0.4, z), r=0.006, seg=1) for z in (0.145, 0.335)] + [s.rbox((0.03, 0.03, 0.2), (cx + t * 0.29, -0.4, 0.24), r=0.006, seg=1) for t in (-1, 1)]
    flap = s.rbox((0.55, 0.012, 0.15), (cx, -0.372, 0.25), r=0.003, seg=1, rot=(math.radians(8), 0, 0))
    accent += [flap, s.rbox((0.2, 0.03, 0.02), (cx, -0.376, 0.2), r=0.005, seg=1)]
    accent += [s.rcyl(0.03, 0.045, (x, y, 0.02), seg=12, r=0.004, bevel_seg=1) for x in (-0.38, 0.38) for y in (-0.32, 0.32)]
    accent += [s.rbox((0.014, 0.02, 0.3), (x1 + 0.03, -0.42, 1.0), r=0.005, seg=2)]                          # the door's handle
    accent += s.bolts([(0.415, -0.402, z) for z in (0.5, 1.0, 1.5)], 0.011, 0.008)                           # its locks
    # the front turns of the spirals
    coils = []
    for z in SHELVES:
        for x in COLUMNS:
            coils.append(s.place(rf.torus(0.046, 0.0022, (0, 0, 0), seg=14, rseg=4), (x, FRONT + 0.018, z + 0.062), (math.pi / 2, 0, 0.12)))
    s.part("Accent", accent)
    s.part("AccentCoils", coils)
    lining = [(s.rbox((x1 - x0 - 0.002, 0.006, z1 - z0), (cx, BACK - 0.004, cz), r=0), "white")]
    lining += [(s.rbox((0.006, BACK + 0.385, z1 - z0), (x, (BACK - 0.385) / 2, cz), r=0), "white") for x in (x0 + 0.004, x1 - 0.004)]
    lining += [(s.rbox((x1 - x0 - 0.002, BACK + 0.385, 0.006), (cx, (BACK - 0.385) / 2, z), r=0), "white") for z in (z0 + 0.004, z1 - 0.004)]
    for k, z in enumerate(SHELVES):
        lining.append((s.rbox((x1 - x0 - 0.012, BACK - FRONT - 0.01, 0.01), (cx, (BACK + FRONT) / 2, z - 0.004), r=0.002, seg=1), "grey"))
        for x in COLUMNS[:-1]:
            lining.append((s.rbox((0.003, BACK - FRONT - 0.02, 0.05), (x + 0.0615, (BACK + FRONT) / 2, z + 0.024), r=0), "chrome"))
    tags = [Plate(f"tag{(k * 5 + j) % 8}", (x, FRONT - 0.004, z - 0.016), 0.052, 0.026, thick=0.004, rest="white") for k, z in enumerate(SHELVES) for j, x in enumerate(COLUMNS)]
    rails = [(s.rbox((x1 - x0 - 0.012, 0.006, 0.034), (cx, FRONT, z - 0.016), r=0.002, seg=1), "chrome") for z in SHELVES]
    plates("Lit", tags, lining + rails)
    s.part("Pane", [s.rbox((x1 - x0 + 0.004, 0.006, z1 - z0 + 0.004), (cx, -0.398, cz), r=0)])
    plates("Print", [Plate("keypad", (0.33, -0.416, 1.245), 0.13, 0.163), Plate("slots", (0.33, -0.416, 1.03), 0.13, 0.163)],
           [(s.rbox((0.09, 0.012, 0.05), (0.33, -0.41, 0.83), r=0.004, seg=1), "chrome")])
    plates("Glow", [Plate("header", (0, -0.4, 1.80), 0.84, 0.135, 0, at.GLOW), Plate("display", (0.33, -0.415, 1.395), 0.13, 0.07, 0, at.GLOW)],
           [(s.rbox((x1 - x0 - 0.06, 0.02, 0.012), (cx, -0.36, z1 - 0.012), r=0.003, seg=1), lambda n, c, co: uv("lit", 0.5, 0.5, at.GLOW))])
    export("vending_window")


def front(cell, cx, z0, w, h, rest, way=-1.0):
    """A rule: a box's face towards the viewer (way -1: Blender -Y) shows a cell over (cx ± w/2, z0 .. z0 + h), the rest a plain colour."""
    def rule(n, c, co):
        if n.y * way < 0.7:
            return plain(rest)
        return uv(cell, (co.x - cx) * (-way) / w + 0.5, (co.z - z0) / h)
    return rule


def can(cell, r, h, loc, lid="chrome"):
    """A tin with a label round it and a pressed lid, standing at loc: (bmesh, rule) pairs."""
    x, y, z = loc
    body = s.rcyl(r, h, (x, y, z + h / 2), seg=18, r=0.004, bevel_seg=1)
    top = s.rcyl(r * 0.86, 0.008, (x, y, z + h + 0.001), seg=18, r=0.002, bevel_seg=1)

    def rule(n, c, co):
        if abs(n.z) > 0.5:
            return plain("tin")
        return uv(cell, 0.5 + math.atan2(co.x - x, -(co.y - y) + 1e-9) / math.pi, (co.z - z) / h)
    return [(body, rule), (top, lid)]


def wrapped(cell, r, z0, z1):
    """A rule: a label round a can or a bottle, its picture across the front half."""
    def rule(n, c, co):
        ang = math.atan2(co.x, -co.y + 1e-9)
        return uv(cell, 0.5 + ang / math.pi, (co.z - z0) / (z1 - z0))
    return rule


def vend(kind, k):
    step = {"bag": 0.085, "bar": 0.06, "can": 0.075, "bottle": 0.078}[kind]
    extra = []
    for q in range(3):
        y = 0.03 + q * step                                    # the front one a little behind the shelf's edge (Unity puts the origin there)
        if kind == "bag":
            b = s.loft([(0.0, 0.048, 0.003, 4), (0.012, 0.049, 0.010, 4), (0.05, 0.047, 0.030, 2.6), (0.10, 0.047, 0.031, 2.6), (0.145, 0.049, 0.011, 4), (0.158, 0.048, 0.003, 4)], seg=20)

            def rule(n, c, co, y=y):
                return uv(f"bag{k}", (co.x if n.y < 0 else -co.x) / 0.1 + 0.5, co.z / 0.158) if abs(n.y) > 0.15 else plain("foil")
            s.place(b, (0, y, 0.002), (math.radians(-4), 0, 0))
            extra.append((b, rule))
        elif kind == "bar":
            b = s.rbox((0.098, 0.016, 0.042), (0, y, 0.05), r=0.005, seg=2, rot=(math.radians(-12), 0, 0))

            def rule(n, c, co):
                return uv(f"bar{k}", co.x / 0.098 + 0.5, (co.z - 0.029) / 0.042) if n.y < -0.4 else plain("foil")
            extra.append((b, rule))
        elif kind == "can":
            r, h = 0.030, 0.115
            b = s.lathe([(0, 0.004), (0.022, 0.0), (0.026, 0.004), (r, 0.012), (r, h - 0.012), (0.026, h - 0.002), (0.0245, h), (0.023, h - 0.006), (0, h - 0.006)], seg=18, loc=(0, 0, 0))
            lab = wrapped(f"can{k}", r, 0.012, h - 0.012)

            def rule(n, c, co, lab=lab):
                return lab(n, c, co) if 0.011 < c.z < h - 0.011 and abs(n.z) < 0.5 else plain("chrome")
            o = (b, rule)
            extra.append((o, y))
        else:
            r, h = 0.031, 0.215
            b = s.lathe([(0, 0.004), (0.024, 0.0), (r, 0.01), (r, 0.052), (0.0295, 0.06), (0.0295, 0.125), (r, 0.133), (0.029, 0.155), (0.014, 0.185), (0.0125, 0.196), (0.0125, 0.2)], seg=18)
            cap = s.lathe([(0, 0.196), (0.0155, 0.196), (0.0155, 0.212), (0.0135, h), (0, h)], seg=14)
            lab = wrapped(f"bottle{k}", r, 0.06, 0.125)
            drink = ("cola", "lime", "clear", "orange", "cola", "clear", "lime", "orange")[k]

            def rule(n, c, co, lab=lab, drink=drink):
                return lab(n, c, co) if 0.0595 < c.z < 0.1255 and abs(n.z) < 0.5 else plain(drink if c.z < 0.17 else "clear")
            extra.append(((b, rule), y))
            extra.append(((cap, "cap"), y))
    flat = []
    for e in extra:
        if isinstance(e[0], tuple):                            # painted round its own axis, then moved back
            (b, rule), y = e
            s.place(b, (0, y, 0))
            flat.append((b, rule if isinstance(rule, str) else (lambda n, c, co, r=rule, y=y: r(n, c - Vector((0, y, 0)), co - Vector((0, y, 0))))))
        else:
            flat.append(e)
    plates("Lit", [], flat)
    export(f"vend_{kind}_{k}", floor=False)


# ------------------------------------------------------------------------------------------------- the electrical panel

def electric_panel():
    W, H, D = 0.62, 0.9, 0.155
    box = s.rbox((W, D, H), (0, -D / 2, H / 2), r=0.012, seg=3)
    door = s.rbox((W - 0.03, 0.022, H - 0.03), (0, -D - 0.007, H / 2), r=0.008, seg=3)
    s.inset_faces(door, s.facing((0, -1, 0), 0.2), thickness=0.035, depth=0.004)
    body = [box, door]
    body += [s.rbox((0.05, 0.03, 0.06), (x, -0.012, z), r=0.004, seg=1) for x in (-W / 2 - 0.02, W / 2 + 0.02) for z in (0.08, H - 0.08)]   # its ears
    for k in range(5):                                          # louvres in the door
        body.append(s.rbox((0.3, 0.012, 0.012), (0, -D - 0.02, 0.12 + k * 0.028), r=0.004, seg=2, rot=(math.radians(28), 0, 0)))
    s.part("Body", body)
    acc = [s.rcyl(0.011, 0.07, (-W / 2 + 0.012, -D - 0.014, z), seg=12, r=0.003, bevel_seg=1) for z in (0.12, 0.45, 0.78)]                    # hinges
    acc += [s.rbox((0.045, 0.014, 0.13), (W / 2 - 0.06, -D - 0.022, 0.5), r=0.006, seg=2), s.rbox((0.026, 0.026, 0.1), (W / 2 - 0.06, -D - 0.036, 0.505), r=0.008, seg=2),
            s.rcyl(0.011, 0.012, (W / 2 - 0.06, -D - 0.026, 0.41), axis="Y", seg=14, r=0.002, bevel_seg=1)]                                     # the handle, its lock
    acc += s.bolts([(x, -0.03, z) for x in (-W / 2 - 0.026, W / 2 + 0.026) for z in (0.08, H - 0.08)], 0.007, 0.01)
    acc += [s.rcyl(0.04, 0.05, (0, -0.06, H + 0.015), seg=16, r=0.006), s.rcyl(0.026, 0.03, (-0.16, -0.06, H + 0.008), seg=14, r=0.005)]        # the conduits' hubs
    s.part("Accent", acc)
    plates("Print", [Plate("danger", (-0.1, -D - 0.0235, 0.7), 0.15, 0.15, thick=0.004, rest="yellow"), Plate("circuits", (0.12, -D - 0.0235, 0.7), 0.13, 0.13, thick=0.003, rest="white")])
    export("electric_panel", floor=False)


ALL = {"arcade_cabinet": arcade_cabinet, "vending_window": vending_window, "electric_panel": electric_panel}
for _kind in ("bag", "bar", "can", "bottle"):
    for _k in range(8):
        ALL[f"vend_{_kind}_{_k}"] = lambda kind=_kind, k=_k: vend(kind, k)

if __name__ == "__main__":
    for name, f in ALL.items():
        if len(argv) < 2 or name in argv[1:] or any(name.startswith(a) for a in argv[1:] if a.endswith("_")):
            s.clear_scene()
            f()
