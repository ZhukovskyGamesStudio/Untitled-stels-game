"""The leaves and the hardware of the hinged doors (prefabs Level/WoodDoor, WoodDoubleDoor, NarrowDoor, ShopDoor,
FlatDoor, EntranceDoor, ClinicDoor, ClinicSwingDoor; AssetBuilder.BuildHingedDoor, DoorRules).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/doors.py -- Assets/_Game/Art/Models
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <root>/Level/wood_leaf.fbx --floor

Writes <root>/Level/<name>.fbx. Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), z up.
A leaf is 0.8 x 2.24 m (the builder stretches it to its span and height: the forms are laid out to stretch — stiles and
rails keep their look between 0.45 and 1.0 m), origin on the floor in its middle, both faces alike and symmetric about
its middle, so a leaf hung on either jamb (the builder mirrors it) looks right; no handle on it — the handle, the
hinges, the closer and the intercom are pieces of their own the builder puts on the leaf's free and hinged edges.
Object names are material roles: Body (the leaf's paint), Accent (steel: kick plates, bezels, grilles), Eye (glass).

  wood_leaf          the thin wooden interior door, 42 mm: a frame with two fielded panels (upper tall, lower short)
  wood_leaf_glass    the same frame, the upper half glazed in six panes behind glazing bars (double doors to big rooms)
  steel_leaf         a plain steel door, 60 mm: pressed edges, a louvre grille low, a wired-glass vision slot
  entrance_leaf      the apartment block's street door, 90 mm: a thick steel slab with two stamped panels and a rib
  clinic_leaf        the hospital's laminate door, 50 mm: rounded edges, a tall vision slot, steel kick plates
  clinic_swing_leaf  the hospital's swing door: a round porthole in a steel bezel, a steel kick plate, a push plate
  flat_side_leaf     the narrow leaf beside a flat's entry door: padded dermantin like flat_leaf, no lock, no number
  door_lever         a lever handle on its rose (one face; the lever points along +X of Unity... the builder checks)
  door_pull          a steel D pull bar (steel and street doors)
  door_hinge         a barrel hinge: a knuckle and two short leaves
  door_closer        an overhead door closer: a rounded body and its arm folded along it
  intercom           an intercom panel: a steel plate, a keypad, a speaker grille and a small display
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODELS = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
ONLY = set(argv[1].split(",")) if len(argv) > 1 and argv[1] else None

W, H = 0.8, 2.24        # the leaf's nominal width and height


def export(name, floor=True):
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=floor, name=name)
    s.export_fbx(os.path.join(MODELS, "Level", name + ".fbx"), objs)
    s.clear_scene()


def face_frame(t, proud, holes, r=0.004):
    """The raised frame of both faces of a leaf of core thickness t: two plates proud of the core, with the holes (x0, x1,
    z0, z1) cut through them (where the panels or the glass are), their edges chamfered."""
    out = []
    for side in (-1, 1):
        y = side * (t / 2 + proud / 2 - 0.0015)
        plate = s.rbox((W, proud + 0.003, H), (0, y, H / 2), r=0.003, seg=1)
        for x0, x1, z0, z1 in holes:
            cutter = s.rbox((x1 - x0, proud * 4, z1 - z0), ((x0 + x1) / 2, y, (z0 + z1) / 2), r=0.0, seg=1)
            plate = s.boolean(plate, cutter)
        s.bevel_edges(plate, 0.0025, seg=1, angle=50)
        out.append(plate)
    return out


def fielded(x0, x1, z0, z1, t, rise, chamfer):
    """A fielded panel filling a hole of the frame: a flat board a hair out of the core, and on it a raised field inset
    from the frame (`rise` out of the core) with a wide chamfer all round — the frame, the margin and the field step."""
    board = s.rbox((x1 - x0 + 0.01, t + 0.003, z1 - z0 + 0.01), ((x0 + x1) / 2, 0, (z0 + z1) / 2), r=0.001, seg=1)
    field = s.rbox((x1 - x0 - 0.06, t + 2 * rise, z1 - z0 - 0.06), ((x0 + x1) / 2, 0, (z0 + z1) / 2), r=chamfer, seg=1)
    return [board, field]


def panes(x0, x1, z0, z1, t, nx, nz, bar=0.022):
    """Glass across a hole (Eye) and glazing bars over both faces of it (Body)."""
    glass = s.rbox((x1 - x0 + 0.012, 0.006, z1 - z0 + 0.012), ((x0 + x1) / 2, 0, (z0 + z1) / 2), r=0.001, seg=1)
    bars = []
    for side in (-1, 1):
        y = side * 0.007
        for i in range(1, nx):
            x = x0 + (x1 - x0) * i / nx
            bars.append(s.rbox((bar, 0.012, z1 - z0 + 0.01), (x, y, (z0 + z1) / 2), r=0.003, seg=1))
        for j in range(1, nz):
            z = z0 + (z1 - z0) * j / nz
            # the cross bars a hair less deep than the long ones: no two faces in one plane where they cross
            bars.append(s.rbox((x1 - x0 + 0.01, 0.0112, bar), ((x0 + x1) / 2, y, z), r=0.003, seg=1))
        # the glazing bead round the hole
        bead = 0.014
        bars += [s.rbox((x1 - x0 + 0.01, 0.0124, bead), ((x0 + x1) / 2, side * 0.008, z), r=0.004, seg=1) for z in (z0 + bead / 2, z1 - bead / 2)]
        bars += [s.rbox((bead, 0.0128, z1 - z0 - 2 * bead + 0.004), (x, side * 0.008, (z0 + z1) / 2), r=0.004, seg=1)
                 for x in (x0 + bead / 2, x1 - bead / 2)]
    return glass, bars


# --------------------------------------------------------------------------- wooden leaves

def model_wood_leaf(glass=False):
    t, proud = 0.026, 0.008         # core and the frame's rise on each face: 42 mm in all
    stile, top, lock, bottom = 0.115, 0.115, 0.17, 0.2
    lz0, lz1 = bottom, 0.86              # the lower panel
    uz0, uz1 = lz1 + lock, H - top       # the upper panel (or glass)
    holes = [(-W / 2 + stile, W / 2 - stile, lz0, lz1), (-W / 2 + stile, W / 2 - stile, uz0, uz1)]
    core = [s.rbox((W, t, H), (0, 0, H / 2), r=0.004, seg=1)]
    body = core + face_frame(t, proud, holes)
    body += fielded(-W / 2 + stile, W / 2 - stile, lz0, lz1, t, 0.0055, 0.016)
    eye = []
    if glass:
        g, bars = panes(-W / 2 + stile, W / 2 - stile, uz0, uz1, t, 2, 3)
        eye.append(g)
        body += bars
        # the core is cut where the glass is
        cut = s.rbox((W - 2 * stile, t * 3, uz1 - uz0), (0, 0, (uz0 + uz1) / 2), r=0.0, seg=1)
        body[0] = s.boolean(body[0], cut)
    else:
        body += fielded(-W / 2 + stile, W / 2 - stile, uz0, uz1, t, 0.0055, 0.016)
    s.part("Body", body)
    if eye:
        s.part("Eye", eye)
    export("wood_leaf_glass" if glass else "wood_leaf")


# --------------------------------------------------------------------------- steel leaves

def model_steel_leaf():
    t = 0.05
    body = [s.rbox((W, t, H), (0, 0, H / 2), r=0.006, seg=2)]
    acc = []
    eye = []
    for side in (-1, 1):
        y = side * (t / 2)
        # the pressed edge: a raised rim all round each face
        rim = s.rbox((W - 0.02, 0.012, H - 0.02), (0, y, H / 2), r=0.004, seg=1)
        rim = s.boolean(rim, s.rbox((W - 0.13, 0.05, H - 0.13), (0, y, H / 2), r=0.0, seg=1))
        s.bevel_edges(rim, 0.003, seg=1, angle=50)
        body.append(rim)
    # the louvre low down, through the leaf: a frame and slats on both faces (Accent)
    louvre = s.rbox((0.44, t * 3, 0.3), (0, 0, 0.36), r=0.0, seg=1)
    body[0] = s.boolean(body[0], louvre)
    for side in (-1, 1):
        acc += s.grille((0, side * (t / 2 + 0.002), 0.36), 0.46, 0.32, n=6, bar=0.018, proud=0.012, axis="-Y" if side < 0 else "Y")
    acc.append(s.rbox((0.43, t - 0.004, 0.29), (0, 0, 0.36), r=0.002, seg=1))   # the back of the louvre: a dark plate inside
    # the wired-glass vision slot, a steel bezel round it on both faces
    slot = s.rbox((0.18, t * 3, 0.42), (0, 0, 1.55), r=0.0, seg=1)
    body[0] = s.boolean(body[0], slot)
    eye.append(s.rbox((0.19, 0.008, 0.43), (0, 0, 1.55), r=0.001, seg=1))
    for side in (-1, 1):
        bez = s.rbox((0.25, 0.014, 0.49), (0, side * (t / 2 + 0.004), 1.55), r=0.005, seg=1)
        bez = s.boolean(bez, s.rbox((0.18, 0.06, 0.42), (0, side * (t / 2 + 0.004), 1.55), r=0.0, seg=1))
        s.bevel_edges(bez, 0.003, seg=1, angle=50)
        acc.append(bez)
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Eye", eye)
    export("steel_leaf")


def model_entrance_leaf():
    t = 0.075
    body = [s.rbox((W, t, H), (0, 0, H / 2), r=0.008, seg=2)]
    for side in (-1, 1):
        y = side * (t / 2)
        # two stamped panels: a raised plate with a deep groove round each panel (the plate minus two rings)
        plate = s.rbox((W - 0.05, 0.016, H - 0.05), (0, y, H / 2), r=0.006, seg=2)
        for z0, z1 in ((0.12, 1.0), (1.18, H - 0.12)):
            ring = s.rbox((W - 0.16, 0.03, z1 - z0), (0, y + side * 0.008, (z0 + z1) / 2), r=0.0, seg=1)
            inner = s.rbox((W - 0.22, 0.04, z1 - z0 - 0.06), (0, y + side * 0.008, (z0 + z1) / 2), r=0.0, seg=1)
            ring = s.boolean(ring, inner)
            plate = s.boolean(plate, ring)
        s.bevel_edges(plate, 0.003, seg=1, angle=50)
        body.append(plate)
        # the rib between the panels, a heavy horizontal bar
        body.append(s.rbox((W - 0.1, 0.02, 0.09), (0, y + side * 0.006, 1.09), r=0.008, seg=2))
    # a kick plate at the bottom of both faces (Accent)
    acc = [s.rbox((W - 0.08, t + 0.03, 0.16), (0, 0, 0.1), r=0.006, seg=2)]
    s.part("Body", body)
    s.part("Accent", acc)
    export("entrance_leaf")


# --------------------------------------------------------------------------- the hospital's leaves

def model_clinic_leaf():
    t = 0.046
    body = [s.rbox((W, t, H), (0, 0, H / 2), r=0.012, seg=3)]
    slot = s.rbox((0.16, t * 3, 0.62), (0, 0, 1.5), r=0.0, seg=1)
    body[0] = s.boolean(body[0], slot)
    eye = [s.rbox((0.17, 0.008, 0.63), (0, 0, 1.5), r=0.001, seg=1)]
    acc = []
    for side in (-1, 1):
        bez = s.rbox((0.2, 0.01, 0.66), (0, side * (t / 2 + 0.002), 1.5), r=0.004, seg=1)
        bez = s.boolean(bez, s.rbox((0.16, 0.05, 0.62), (0, side * (t / 2 + 0.002), 1.5), r=0.0, seg=1))
        s.bevel_edges(bez, 0.002, seg=1, angle=50)
        acc.append(bez)
    # stainless kick plates on both faces, and a band at bed height (wards: the beds bump the door)
    acc.append(s.rbox((W - 0.04, t + 0.006, 0.3), (0, 0, 0.18), r=0.003, seg=1))
    acc.append(s.rbox((W - 0.04, t + 0.004, 0.07), (0, 0, 0.72), r=0.003, seg=1))
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Eye", eye)
    export("clinic_leaf")


def model_clinic_swing_leaf():
    t = 0.046
    body = [s.rbox((W, t, H), (0, 0, H / 2), r=0.012, seg=3)]
    zc, rad = 1.55, 0.17
    hole = s.rcyl(rad, t * 3, (0, 0, zc), axis="Y", seg=32, r=0.0, rot=(0, math.pi / 32, 0))
    body[0] = s.boolean(body[0], hole)
    eye = [s.rcyl(rad + 0.006, 0.008, (0, 0, zc), axis="Y", seg=32, r=0.001, bevel_seg=1)]
    acc = []
    for side in (-1, 1):
        bez = s.rcyl(rad + 0.035, 0.012, (0, side * (t / 2 + 0.003), zc), axis="Y", seg=32, r=0.004, bevel_seg=1)
        bez = s.boolean(bez, s.rcyl(rad, 0.06, (0, side * (t / 2 + 0.003), zc), axis="Y", seg=32, r=0.0, rot=(0, math.pi / 32, 0)))
        s.bevel_edges(bez, 0.002, seg=1, angle=50)
        acc.append(bez)
    acc.append(s.rbox((W - 0.04, t + 0.006, 0.36), (0, 0, 0.2), r=0.003, seg=1))   # the kick plate (trolleys push it open)
    acc.append(s.rbox((W - 0.04, t + 0.004, 0.08), (0, 0, 0.9), r=0.003, seg=1))   # the push band
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Eye", eye)
    export("clinic_swing_leaf")


# --------------------------------------------------------------------------- the flat's narrow leaf

def model_flat_side_leaf():
    import apartments as ap
    T, bar, proud = 0.03, 0.05, 0.016
    steel = [s.rbox((W, T, H), (0, 0, H / 2), r=0.004, seg=1)]
    pad, studs = [], []
    for side in (-1, 1):
        y = side * (T / 2 + proud / 2 - 0.001)
        steel += [s.rbox((W, proud, bar), (0, y, z), r=0.006, seg=2) for z in (bar / 2, H - bar / 2)]
        steel += [s.rbox((bar, proud, H - 2 * bar + 0.01), (x * (W / 2 - bar / 2), y, H / 2), r=0.006, seg=2) for x in (-1, 1)]
        q, pts = ap.quilted(-W / 2 + bar - 0.006, W / 2 - bar + 0.006, bar - 0.006, H - bar + 0.006,
                            T / 2 - 0.002, T / 2 + 0.003, T / 2 + 0.0145, 4, 16)
        st = [s.lathe([(0.0, -0.003), (0.0085, -0.003), (0.0065, 0.0025), (0.0, 0.0045)], seg=7, axis="Y", loc=p) for p in pts]
        if side < 0:
            q = ap.flip_y(q)
            st = [ap.flip_y(b) for b in st]
        pad.append(q)
        studs += st
    s.part("BodyFrame", steel)
    s.part("Body", pad)
    s.part("TrimStuds", studs)
    export("flat_side_leaf")


# --------------------------------------------------------------------------- hardware

def model_door_lever():
    """A lever handle on a round rose, standing out of the leaf's -Y face (Unity +Z), the lever along +X; origin on the
    spindle at the leaf's face. One face: the builder mirrors a copy onto the other."""
    acc = [s.rcyl(0.026, 0.012, (0, -0.006, 0), axis="Y", seg=20, r=0.004, bevel_seg=1)]
    acc.append(s.rcyl(0.009, 0.05, (0, -0.03, 0), axis="Y", seg=12, r=0.002, bevel_seg=1))
    acc.append(s.tube([(0, -0.05, 0), (0.025, -0.058, 0), (0.13, -0.056, -0.004)], 0.0095, res=2, segments_per=5))
    acc.append(s.ball(0.0105, (0.13, -0.056, -0.004), seg=12))
    s.part("Accent", acc)
    export("door_lever", floor=False)


def model_door_pull():
    """A D pull bar: a vertical round bar 0.36 m on two stand-offs out of the -Y face; origin at its middle on the face."""
    acc = [s.rcyl(0.014, 0.4, (0, -0.07, 0), axis="Z", seg=16, r=0.006, bevel_seg=2)]
    for z in (-0.15, 0.15):
        acc.append(s.rcyl(0.012, 0.072, (0, -0.035, z), axis="Y", seg=14, r=0.003, bevel_seg=1))
        acc.append(s.rcyl(0.024, 0.008, (0, -0.004, z), axis="Y", seg=16, r=0.002, bevel_seg=1))
    s.part("Accent", acc)
    export("door_pull", floor=False)


def model_door_hinge():
    """A barrel hinge: the knuckle along Z (origin on its axis, at its middle) and two short leaf plates either side."""
    acc = [s.rcyl(0.009, 0.12, (0, 0, 0), axis="Z", seg=12, r=0.002, bevel_seg=1)]
    acc.append(s.rcyl(0.006, 0.02, (0, 0, 0.065), axis="Z", seg=10, r=0.002, bevel_seg=1))
    acc.append(s.rcyl(0.006, 0.02, (0, 0, -0.065), axis="Z", seg=10, r=0.002, bevel_seg=1))
    s.part("Accent", acc)
    export("door_hinge", floor=False)


def model_door_closer():
    """An overhead closer: a rounded body 0.26 m along X on the -Y face (origin at its back-middle) and its arm folded along it."""
    acc = [s.rbox((0.26, 0.05, 0.06), (0, -0.025, 0), r=0.016, seg=3)]
    acc.append(s.rcyl(0.012, 0.03, (0.09, -0.06, 0.0), axis="Y", seg=12, r=0.003, bevel_seg=1))
    acc.append(s.rbox((0.24, 0.016, 0.022), (-0.02, -0.072, 0.0), r=0.006, seg=2))
    s.part("Accent", acc)
    export("door_closer", floor=False)


def model_intercom():
    """An intercom panel 0.14 x 0.3 on the -Y face (origin at its back-middle): a steel plate, a grid of keys (Accent),
    a speaker grille and a small display (Eye)."""
    body = [s.rbox((0.14, 0.02, 0.3), (0, -0.01, 0), r=0.008, seg=2)]
    acc = []
    for i in range(3):
        for j in range(4):
            acc.append(s.rbox((0.026, 0.01, 0.02), (-0.034 + i * 0.034, -0.02, -0.11 + j * 0.03), r=0.003, seg=1))
    for k in range(5):
        acc.append(s.rbox((0.08, 0.008, 0.006), (0, -0.02, 0.05 + k * 0.014), r=0.002, seg=1))
    eye = [s.rbox((0.09, 0.006, 0.03), (0, -0.021, 0.0), r=0.002, seg=1)]
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Eye", eye)
    export("intercom", floor=False)


MODELS_ALL = {
    "wood_leaf": lambda: model_wood_leaf(False),
    "wood_leaf_glass": lambda: model_wood_leaf(True),
    "steel_leaf": model_steel_leaf,
    "entrance_leaf": model_entrance_leaf,
    "clinic_leaf": model_clinic_leaf,
    "clinic_swing_leaf": model_clinic_swing_leaf,
    "flat_side_leaf": model_flat_side_leaf,
    "door_lever": model_door_lever,
    "door_pull": model_door_pull,
    "door_hinge": model_door_hinge,
    "door_closer": model_door_closer,
    "intercom": model_intercom,
}


# --------------------------------------------------------------------------- the apartment block's lift (LiftDoor, LiftCab)

def model_lift_panel():
    """A landing door panel of the lift, 0.6 x 2.28 m, 40 mm: brushed steel with a pressed frame on both faces and a fine
    horizontal groove at hand height; symmetric (the builder stretches it to half the opening)."""
    w, h, t = 0.6, 2.28, 0.03
    body = [s.rbox((w, t, h), (0, 0, h / 2), r=0.003, seg=1)]
    for side in (-1, 1):
        y = side * (t / 2)
        rim = s.rbox((w - 0.01, 0.01, h - 0.01), (0, y, h / 2), r=0.003, seg=1)
        rim = s.boolean(rim, s.rbox((w - 0.08, 0.04, h - 0.08), (0, y, h / 2), r=0.0, seg=1))
        s.bevel_edges(rim, 0.002, seg=1, angle=50)
        body.append(rim)
    acc = [s.rbox((w - 0.08, t + 0.004, 0.012), (0, 0, 1.05), r=0.002, seg=1)]
    s.part("Body", body)
    s.part("Accent", acc)
    export("lift_panel")


def model_lift_cab():
    """The lift's cab, inside 2.6 x 2.1 x 2.4 m, its open front (Blender -Y, Unity +Z) a 1.1 m doorway between two front
    returns and a header: steel walls with tall raised panels (Body), a kick strip and a handrail on the back wall
    (Accent), a dark floor plate (Floor), a lit ceiling panel (Eye). Origin on the cab's floor in its middle; symmetric —
    the floor buttons' panel is a piece of its own (lift_cop)."""
    W, D, H, T = 2.6, 2.1, 2.4, 0.05
    door = 1.1
    body = []
    body.append(s.rbox((W + 2 * T, T, H + 2 * T), (0, D / 2 + T / 2, H / 2), r=0.004, seg=1))                 # back
    for x in (-1, 1):
        body.append(s.rbox((T, D + 2 * T, H + 2 * T), (x * (W / 2 + T / 2), 0, H / 2), r=0.004, seg=1))      # sides
    ret = (W - door) / 2
    for x in (-1, 1):
        body.append(s.rbox((ret + 0.004, T, H + 2 * T), (x * (W / 2 - ret / 2 + 0.002), -D / 2 - T / 2, H / 2), r=0.004, seg=1))
    body.append(s.rbox((door + 0.02, T, H - 2.3 + T), (0, -D / 2 - T / 2, 2.3 + (H - 2.3 + T) / 2), r=0.004, seg=1))  # header
    body.append(s.rbox((W + 2 * T, D + 2 * T, T), (0, 0, H + T / 2), r=0.004, seg=1))                         # ceiling
    # tall raised panels on the inside of the back and side walls
    pz0, pz1 = 0.16, H - 0.12
    for k in range(3):
        x = -W / 2 + W * (k + 0.5) / 3
        body.append(s.rbox((W / 3 - 0.08, 0.012, pz1 - pz0), (x, D / 2 - 0.004, (pz0 + pz1) / 2), r=0.004, seg=1))
    for side in (-1, 1):
        for k in range(2):
            y = -D / 2 + D * (k + 0.5) / 2
            body.append(s.rbox((0.012, D / 2 - 0.08, pz1 - pz0), (side * (W / 2 - 0.004), y, (pz0 + pz1) / 2), r=0.004, seg=1))
    acc = []
    # the kick strip round the three walls, the handrail along the back on brackets
    acc.append(s.rbox((W - 0.004, 0.014, 0.1), (0, D / 2 - 0.005, 0.05), r=0.003, seg=1))
    for side in (-1, 1):
        acc.append(s.rbox((0.014, D - 0.004, 0.1), (side * (W / 2 - 0.005), 0, 0.05), r=0.003, seg=1))
    rail_y = D / 2 - 0.07
    acc.append(s.tube([(-W / 2 + 0.25, rail_y, 0.92), (W / 2 - 0.25, rail_y, 0.92)], 0.019, res=2, segments_per=2))
    for x in (-W / 2 + 0.3, 0.0, W / 2 - 0.3):
        acc.append(s.rcyl(0.011, 0.07, (x, D / 2 - 0.035, 0.92), axis="Y", seg=10, r=0.003, bevel_seg=1))
    # the door's sill strip at the front
    acc.append(s.rbox((door + 0.04, 0.09, 0.012), (0, -D / 2 - 0.01, 0.006), r=0.002, seg=1))
    floor = [s.rbox((W + 0.004, D + 0.004, 0.04), (0, 0, -0.02), r=0.003, seg=1)]
    eye = [s.rbox((1.5, 1.1, 0.02), (0, 0.05, H - 0.006), r=0.006, seg=1)]
    s.part("Body", body)
    s.part("Accent", acc)
    s.part("Floor", floor)
    s.part("Eye", eye)
    export("lift_cab")


def model_lift_button():
    """A round push button 36 mm on a bezel, out of the -Y face (origin at its back): the button (Body) and its lamp ring (Eye)."""
    acc = [s.rcyl(0.026, 0.006, (0, -0.003, 0), axis="Y", seg=20, r=0.0015, bevel_seg=1)]
    body = [s.rcyl(0.016, 0.01, (0, -0.009, 0), axis="Y", seg=18, r=0.003, bevel_seg=1)]
    eye = [s.rcyl(0.021, 0.004, (0, -0.0075, 0), axis="Y", seg=20, r=0.001, bevel_seg=1)]
    s.part("Accent", acc)
    s.part("Body", body)
    s.part("Eye", eye)
    export("lift_button", floor=False)


def model_lift_cop():
    """The cab's floor buttons' panel: a brushed steel plate 0.2 x 0.95 m on the wall (origin at its back middle, out of -Y)
    with a small display at its top (Eye); the buttons are pieces of their own (lift_button)."""
    acc = [s.rbox((0.2, 0.012, 0.95), (0, -0.006, 0), r=0.004, seg=2)]
    eye = [s.rbox((0.12, 0.004, 0.05), (0, -0.0135, 0.39), r=0.002, seg=1)]
    s.part("Accent", acc)
    s.part("Eye", eye)
    export("lift_cop", floor=False)


MODELS_ALL.update({
    "lift_panel": model_lift_panel,
    "lift_cab": model_lift_cab,
    "lift_button": model_lift_button,
    "lift_cop": model_lift_cop,
})

if __name__ == "__main__":
    s.clear_scene()
    for name, fn in MODELS_ALL.items():
        if ONLY is None or name in ONLY:
            fn()
