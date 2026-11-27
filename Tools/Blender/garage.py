"""The auto repair shop (HouseKind.Garage): customers' cars and the shop's equipment.

Cars — a mid-size sedan built once (sedan()) and placed four ways: parked with the hood shut, with the hood up and the
engine showing, up on a two-post lift (the lift and the car are one prop: walk under it), and on jack stands with its
wheels off. Equipment of a real shop (referenced from service-bay planning guides and equipment lists): the empty lift,
a rolling tool chest, a tire changer and a wheel balancer, a tire rack and a stack of tires, an engine on a stand and an
engine hoist, an air compressor, a wheeled oil drain, a wall hose reel, a floor jack, oil drums on a pallet, a welding
cart, and the painted outline of a bay on the floor.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/garage.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/Props/<name>.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> [--floor]

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), z up, origin on the floor in the middle of the
footprint. Object names are material roles: Body (paint / main colour), Accent (rubber, dark metal), Trim (glass, chrome,
steel), Eye (glows: lamps, screens).
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir from the first argument after --)
import machines as mc  # noqa: E402
import meshfix  # noqa: E402
import shapes as s  # noqa: E402
from rooms2_work import caster, prism, revolve, rod  # noqa: E402

E = 0.005


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


class Parts:
    """Shapes collected by role, merged into one object per role at the end."""

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


def moved(bms, dx=0.0, dy=0.0, dz=0.0, rz=0.0):
    """Turns shapes about z (radians) and moves them."""
    out = []
    for b in bms:
        if rz:
            s.place(b, rot=(0, 0, rz))
        s.place(b, (dx, dy, dz))
        out.append(b)
    return out


# --------------------------------------------------------------------------- the sedan

WHEELBASE = 2.7
TRACK = 0.79      # wheel centres at x = +-TRACK
WHEEL_R = 0.33
TYRE_W = 0.21
BODY_W = 1.78
SILL = 0.27       # the underside of the body over the floor (on its wheels)


def tyre(loc, axis="X", r=WHEEL_R, w=TYRE_W, seg=20):
    """A tyre: a rounded ring, the wheel's axis along x (on a car) or z (lying flat)."""
    prof = [(r * 0.66, -w / 2), (r * 0.9, -w / 2), (r, -w * 0.3), (r, w * 0.3), (r * 0.9, w / 2), (r * 0.66, w / 2)]
    bm = revolve(prof, seg=seg)
    if axis == "X":
        s.place(bm, rot=(0, math.pi / 2, 0))
    elif axis == "Y":
        s.place(bm, rot=(math.pi / 2, 0, 0))
    return s.place(bm, loc)


def rim(loc, axis="X", r=WHEEL_R, w=TYRE_W):
    """The wheel's rim: a disc with a hub, inside the tyre."""
    disc = s.rcyl(r * 0.68, w * 0.7, (0, 0, 0), axis="Z", seg=18, r=0.008)
    hub = s.rcyl(r * 0.18, w * 0.86, (0, 0, 0), axis="Z", seg=10, r=0.006)
    out = []
    for b in (disc, hub):
        if axis == "X":
            s.place(b, rot=(0, math.pi / 2, 0))
        elif axis == "Y":
            s.place(b, rot=(math.pi / 2, 0, 0))
        out.append(s.place(b, loc))
    return out


def wheel(p, loc, axis="X"):
    p.add("Accent", tyre(loc, axis))
    p.add("Trim", rim(loc, axis))


def alloy(loc, r=WHEEL_R, w=TYRE_W):
    """A five-spoke alloy wheel inside its tyre (axis along x): the dished disc with five windows, the hub cap, the dark drum behind."""
    disc = s.rcyl(r * 0.69, w * 0.5, (0, 0, 0), axis="X", seg=30, r=0.012)
    for k in range(5):
        a = 2 * math.pi * k / 5
        hole = s.rcyl(r * 0.17, w, (0, r * 0.4 * math.cos(a), r * 0.4 * math.sin(a)), axis="X", seg=10, r=0)
        disc = s.boolean(disc, hole, "DIFFERENCE")
    s.bevel_edges(disc, 0.004, 1, angle=50)
    cap = s.rcyl(r * 0.16, w * 0.62, (0, 0, 0), axis="X", seg=14, r=0.008)
    drum = s.rcyl(r * 0.6, w * 0.3, (0, 0, 0), axis="X", seg=16, r=0.004)
    return [(s.place(disc, loc), "chrome"), (s.place(cap, loc), "tin"), (s.place(drum, loc), "black")]


def sedan(p, hood_open=False, wheels=True, extras=None):
    """A four-door sedan, 4.45 x 1.78 x 1.44 m, front at -y, on its wheels at z 0 (move it with moved()). The body is one
    loft — a rounded section that swells from the nose over the hood to the doors and tapers into the boot — with the
    wheel arches cut out; the cabin a second, glazed one with the roof and the pillars on it. `extras`: a list that gets
    what a workshop's car shows close up and has no part for — (shape, colour) pairs: alloy wheels, red tail lenses,
    number plates, chrome (machines.plates paints them); without it the wheels are plain and those details are left out."""
    yf, yr = -2.22, 2.22
    ex = extras if extras is not None else []
    sections = [(-2.22, 0.66, 0.15, 4.0, 0.5), (-2.14, 0.8, 0.22, 4.5, 0.52), (-1.9, 0.875, 0.27, 5.0, 0.555), (-1.0, 0.89, 0.3, 5.0, 0.575),
                (0.6, 0.89, 0.305, 5.0, 0.58), (1.6, 0.88, 0.3, 5.0, 0.585), (2.08, 0.82, 0.26, 4.5, 0.585), (2.22, 0.68, 0.17, 4.0, 0.57)]
    body = s.loft([(y, a, b, n, 0, -zc) for y, a, b, n, zc in sections], seg=44, axis="Y")
    if hood_open:                                             # the engine bay, open to the sky
        body = s.boolean(body, s.rbox((1.5, 1.0, 0.5), (0, -1.55, 0.83), r=0.03, seg=1), "DIFFERENCE")
    for wy in (-WHEELBASE / 2, WHEELBASE / 2):
        body = s.boolean(body, s.rcyl(WHEEL_R + 0.06, BODY_W + 0.2, (0, wy, WHEEL_R), axis="X", seg=28, r=0), "DIFFERENCE")
        for sx in (-1, 1):                                    # the dark wells over the wheels
            p.add("Accent", s.rbox((0.34, 2 * WHEEL_R + 0.1, WHEEL_R + 0.05), (sx * 0.7, wy, WHEEL_R + (WHEEL_R + 0.05) / 2), r=0.02, seg=1))
    s.bevel_edges(body, 0.012, 2, angle=50)

    def flank(y, z):
        """The body's half width at (y, z)."""
        for (y0, a0, b0, n0, c0), (y1, a1, b1, n1, c1) in zip(sections, sections[1:]):
            if y0 <= y <= y1:
                t = (y - y0) / (y1 - y0)
                a, b, n, c = a0 + (a1 - a0) * t, b0 + (b1 - b0) * t, n0 + (n1 - n0) * t, c0 + (c1 - c0) * t
                return a * max(0.0, 1 - min(1.0, abs(z - c) / b) ** n) ** (1 / n)
        return 0.8
    lips = []
    for wy in (-WHEELBASE / 2, WHEELBASE / 2):                # the lip round every wheel arch
        for sx in (-1, 1):
            pts = []
            for k in range(11):
                t = math.radians(12 + 156 * k / 10)
                y, z = wy + (WHEEL_R + 0.066) * math.cos(t), WHEEL_R + (WHEEL_R + 0.066) * math.sin(t)
                pts.append((sx * (flank(y, z) - 0.008), y, z))
            lips.append(s.tube(pts, 0.021, res=1, segments_per=2))
    for sx in (-1, 1):                                        # the rubbing strip along the doors
        p.add("Accent", s.rbox((0.014, 1.74, 0.034), (sx * 0.889, 0.0, 0.6), r=0.005, seg=1))
    for y in (-0.98, -0.35, 0.62):                            # the hood's edge and the doors'
        s.groove(body, "Y", y, 0.008, 0.004)
    # the cabin: glass all round, its sides leaning in to the roof
    cab = [(-1.08, 0.74, 0.05, 0.86), (-0.3, 0.725, 0.48, 0.95), (0.78, 0.725, 0.48, 0.95), (1.5, 0.74, 0.05, 0.87)]
    glass = s.loft([(y, a, b, 5.0, 0, -zc) for y, a, b, zc in cab], seg=36, axis="Y")

    def skin(y, z):
        """The cabin's half width at (y, z)."""
        for (y0, a0, b0, c0), (y1, a1, b1, c1) in zip(cab, cab[1:]):
            if y0 <= y <= y1:
                t = (y - y0) / (y1 - y0)
                a, b, c = a0 + (a1 - a0) * t, b0 + (b1 - b0) * t, c0 + (c1 - c0) * t
                return a * max(0.0, 1 - min(1.0, abs(z - c) / b) ** 5) ** 0.2
        return 0.7
    roof = s.rbox((1.2, 1.04, 0.05), (0, 0.24, 1.412), r=0.022, seg=3)
    pillars = []
    for sx in (-1, 1):
        for (ya, za), (yb, zb), r in (((-1.0, 0.92), (-0.31, 1.39), 0.03), ((0.2, 0.9), (0.2, 1.4), 0.034), ((1.42, 0.93), (0.8, 1.39), 0.04)):
            pts = []
            for k in range(5):
                t = k / 4
                y, z = ya + (yb - ya) * t, za + (zb - za) * t
                pts.append((sx * (skin(y, z) - 0.006), y, z))
            pillars.append(s.tube(pts, r, res=1, segments_per=3))
        # the gutter along the roof's edge, the sill under the doors
        pillars.append(s.tube([(sx * (skin(y, 1.39) - 0.004), y, 1.392) for y in (-0.31, 0.2, 0.8)], 0.026, res=1, segments_per=3))
        p.add("Accent", s.rbox((0.03, 1.95, 0.07), (sx * 0.872, 0, 0.3), r=0.01, seg=1))
    hood = None
    if hood_open:
        hood = s.rbox((1.52, 1.04, 0.03), (0, -1.52, 0.85), r=0.012, seg=2)
        s.place(hood, (0, 1.0, -0.86))
        s.place(hood, rot=(-math.radians(58), 0, 0))
        s.place(hood, (0, -1.0, 0.86))
        p.add("Accent", s.rbox((0.62, 0.55, 0.26), (0, -1.55, 0.66), r=0.03), s.rbox((0.3, 0.26, 0.22), (0.5, -1.85, 0.66), r=0.02),
              s.rbox((1.46, 0.96, 0.03), (0, -1.55, 0.55), r=0.004, seg=1))                                  # the block, the battery, the bay's dark floor
        p.add("Trim", s.rbox((0.5, 0.18, 0.06), (0, -1.5, 0.81), r=0.02), s.rcyl(0.11, 0.08, (-0.45, -1.4, 0.76), seg=14),
              rod((0.0, -1.85, 0.7), (0.0, -2.02, 0.62), 0.05, seg=10), rod((0.66, -2.0, 0.8), (0.6, -1.35, 1.33), 0.008, seg=6))  # …and the hood's prop
        ex += [(s.rcyl(0.04, 0.03, (-0.15, -1.7, 0.8), seg=10, r=0.004), "yellow"), (s.rcyl(0.035, 0.03, (0.2, -1.4, 0.8), seg=10, r=0.004), "blue")]  # oil and washer caps
    p.add("Body", [body, roof] + pillars + lips + ([hood] if hood else []))
    p.add("Trim", glass)
    if extras is not None:                                    # what one sees through the glass: the deck, seats, the dash, the wheel
        ex.append((s.rbox((1.3, 2.2, 0.03), (0, 0.2, 0.892), r=0.008, seg=1), "black"))
        # the seats' backs, as high as the glass shows them: the front pair under the middle of the roof, the rear bench
        # under its back edge — ahead of the sloping rear glass, not under it (it stood through the glass), the parcel
        # shelf (the deck) behind it
        for sx in (-1, 1):
            ex.append((s.rbox((0.46, 0.12, 0.42), (sx * 0.36, 0.05, 1.04), r=0.05, seg=2, rot=(-0.2, 0, 0)), "canvas"))
            ex.append((s.rbox((0.22, 0.08, 0.12), (sx * 0.36, 0.11, 1.27), r=0.035, seg=2, rot=(-0.2, 0, 0)), "canvas"))
            ex.append((s.rbox((0.2, 0.08, 0.1), (sx * 0.36, 0.78, 1.215), r=0.03, seg=2, rot=(-0.25, 0, 0)), "canvas"))
        ex.append((s.rbox((1.2, 0.13, 0.34), (0, 0.71, 1.02), r=0.05, seg=2, rot=(-0.25, 0, 0)), "canvas"))
        ex.append((s.rbox((1.34, 0.36, 0.1), (0, -0.74, 0.94), r=0.04, seg=2), "black"))
        ex.append((s.rbox((0.34, 0.2, 0.07), (-0.36, -0.66, 1.0), r=0.03, seg=2), "black"))                       # the binnacle
        ring = [(-0.36 + 0.17 * math.cos(2 * math.pi * k / 12), -0.47 - 0.07 * math.sin(2 * math.pi * k / 12), 1.04 + 0.16 * math.sin(2 * math.pi * k / 12)) for k in range(13)]
        ex.append((s.tube(ring, 0.014, res=1, segments_per=1), "black"))
        ex.append((s.rbox((0.3, 0.03, 0.03), (-0.36, -0.47, 1.04), r=0.01, seg=1), "black"))
        ex.append((rod((-0.36, -0.47, 1.04), (-0.36, -0.66, 0.97), 0.02, seg=6), "black"))
        ex.append((s.rbox((0.2, 0.03, 0.06), (0, -0.56, 1.3), r=0.012, seg=1), "black"))                          # the mirror on the screen
        for wx, wr in ((-0.3, 0.07), (0.28, 0.07)):           # the wipers, parked along the screen's foot
            ex.append((s.rbox((0.5, 0.018, 0.012), (wx, -0.975, 0.95), r=0.004, seg=1, rot=(0.6, 0, wr)), "black"))
        ex.append((s.rcyl(0.034, 0.14, (0.45, yr - 0.03, 0.27), axis="Y", seg=12, r=0.006), "chrome"))            # the tail pipe
        ex.append((rod((0.45, 0.6, 1.425), (0.45, 0.68, 1.455), 0.012, seg=6), "black"))                             # the aerial
    # the nose and the tail: bumpers in the body's colour over dark lips, a grille, lamps, plates
    p.add("Body", s.rbox((1.5, 0.16, 0.2), (0, yf + 0.03, 0.42), r=0.07, seg=3), s.rbox((1.5, 0.16, 0.2), (0, yr - 0.03, 0.44), r=0.07, seg=3))
    p.add("Accent", s.rbox((1.3, 0.1, 0.07), (0, yf + 0.05, 0.3), r=0.02), s.rbox((1.3, 0.1, 0.07), (0, yr - 0.05, 0.32), r=0.02),
          s.rbox((0.6, 0.04, 0.1), (0, yf + 0.004, 0.6), r=0.012))
    ex += [(s.rbox((0.56, 0.012, 0.012), (0, yf - 0.018, 0.565 + k * 0.035), r=0.003, seg=1), "chrome") for k in range(3)]
    ex += [(s.rbox((0.4, 0.016, 0.1), (0, yf - 0.052, 0.42), r=0.004, seg=1), "white2"), (s.rbox((0.4, 0.016, 0.1), (0, yr + 0.052, 0.45), r=0.004, seg=1), "white2")]
    p.add("Accent", s.rbox((0.86, 0.03, 0.065), (0, yf - 0.042, 0.345), r=0.015, seg=1))                           # the lower intake
    for sx in (-1, 1):
        # head lamps and tail lamps wrap round the corners
        p.add("Eye", s.rbox((0.33, 0.1, 0.115), (sx * 0.48, yf + 0.0, 0.6), r=0.035, seg=2, rot=(0, 0, sx * 0.2)))
        p.add("Accent", s.rbox((0.37, 0.1, 0.13), (sx * 0.49, yr - 0.01, 0.665), r=0.03, seg=2, rot=(0, 0, sx * -0.2)))
        ex.append((s.rbox((0.34, 0.09, 0.1), (sx * 0.49, yr + 0.005, 0.665), r=0.022, seg=2, rot=(0, 0, sx * -0.2)), "red"))
        ex.append((s.rbox((0.1, 0.03, 0.05), (sx * 0.38, yr + 0.05, 0.665), r=0.01, seg=1), "white2"))            # the reversing lamps
        ex.append((s.rcyl(0.042, 0.03, (sx * 0.56, yf - 0.05, 0.4), axis="Y", seg=12, r=0.006), "white2"))        # the fog lamps
        ex.append((s.rbox((0.07, 0.02, 0.05), (sx * 0.5, yf - 0.046, 0.33), r=0.008, seg=1), "orange"))      # the indicators
        p.add("Body", s.rbox((0.17, 0.08, 0.1), (sx * 0.945, -0.9, 0.94), r=0.03, seg=2), s.rbox((0.12, 0.05, 0.04), (sx * 0.86, -0.9, 0.9), r=0.012, seg=1))
        for hy in (-0.52, 0.45):
            ex.append((s.rbox((0.024, 0.14, 0.026), (sx * 0.862, hy, 0.79), r=0.008, seg=2), "chrome"))
    if wheels:
        for wy in (-WHEELBASE / 2, WHEELBASE / 2):
            for sx in (-1, 1):
                if extras is None:
                    wheel(p, (sx * TRACK, wy, WHEEL_R))
                else:
                    p.add("Accent", tyre((sx * TRACK, wy, WHEEL_R), "X"))
                    ex += alloy((sx * (TRACK + 0.03), wy, WHEEL_R))
    return yf, yr


def details(extras, dz=0.0):
    """What the sedan handed back (its plates, lenses, alloys, chrome) as one printed part, lifted with the car."""
    for b, _ in extras:
        if dz:
            s.place(b, (0, 0, dz))
    if extras:
        mc.plates("Print", [], extras)


def lift(p, raised):
    """A two-post lift, the posts beside the car's middle: base plates, posts with their carriages, two arms each
    swung under the car's lift points (at the car's sill when raised, low when not), the floor cover between the posts."""
    h = 2.9
    ax = SILL + 1.55 if raised else 0.16
    for sx in (-1, 1):
        x = sx * 1.42
        p.add("Body", s.rbox((0.32, 0.3, h), (x, 0, h / 2), r=0.02, vertical_only=True),
              s.rbox((0.3, 0.12, 0.42), (x - sx * 0.2, 0, ax + 0.15), r=0.015))
        p.add("Accent", s.rbox((0.55, 0.5, 0.02), (x, 0, 0.01), r=0.005),
              s.rbox((0.12, 0.04, h - 0.4), (x - sx * 0.16, -0.12, h / 2 + 0.1), r=0.005))
        p.add("Trim", s.rcyl(0.04, 0.18, (x, 0.0, h + 0.05), seg=10))
        for ay, ang in ((-1, 0.62), (1, -0.62)):
            a = Vector((x - sx * 0.25, 0.0, ax))
            b = Vector((sx * 0.62, ay * 1.15, ax))
            p.add("Body", rod(tuple(a), tuple(b), 0.055, seg=8))
            p.add("Accent", s.rcyl(0.09, 0.05, (b.x, b.y, ax + 0.06), seg=12))
    p.add("Accent", s.rbox((2.5, 0.42, 0.05), (0, 0, 0.025), r=0.02))       # the floor cover over the lines
    p.add("Eye", s.rbox((0.14, 0.02, 0.2), (1.42 - 0.0, -0.16, 1.2), r=0.01))  # the control box's lamp


def model_car_parked():
    p, ex = Parts(), []
    sedan(p, extras=ex)
    p.finish()
    details(ex)
    return export("car_parked")


def model_car_hood_open():
    p, ex = Parts(), []
    sedan(p, hood_open=True, extras=ex)
    p.finish()
    details(ex)
    return export("car_hood_open")


def model_lift_car():
    """The lift with a sedan up on it: walk under the car (its underside at ~1.8 m)."""
    p = Parts()
    lift(p, raised=True)
    car, ex = Parts(), []
    sedan(car, extras=ex)
    for role, bms in car.roles.items():
        p.add(role, moved(bms, dz=1.55))
    # the underside: exhaust, tank, axles (what a player looks up at)
    p.add("Accent", rod((0.25, -1.4, 1.55 + 0.3), (0.25, 2.1, 1.55 + 0.32), 0.04, seg=8),
          s.rbox((0.7, 0.5, 0.16), (0, 1.0, 1.55 + 0.36), r=0.04), s.rbox((0.6, 0.7, 0.18), (0, -1.5, 1.55 + 0.36), r=0.04))
    p.finish()
    details(ex, 1.55)
    return export("lift_car")


def model_lift_empty():
    p = Parts()
    lift(p, raised=False)
    p.finish()
    return export("lift_empty")


def model_car_on_stands():
    """A sedan up on four jack stands, its wheels off and leaning against the stands."""
    p = Parts()
    car, ex = Parts(), []
    sedan(car, wheels=False, extras=ex)
    for role, bms in car.roles.items():
        p.add(role, moved(bms, dz=0.28))
    for sy in (-1, 1):
        for sx in (-1, 1):
            x, y = sx * 0.6, sy * 0.95
            p.add("Body", rod((x - 0.12, y - 0.12, 0.0), (x, y, 0.5), 0.02, seg=6), rod((x + 0.12, y - 0.12, 0.0), (x, y, 0.5), 0.02, seg=6),
                  rod((x, y + 0.14, 0.0), (x, y, 0.5), 0.02, seg=6))
            p.add("Trim", s.rcyl(0.035, 0.08, (x, y, 0.54), seg=8))
    # two wheels lying flat, one standing against the car's flank
    for loc in ((-1.25, -0.6, TYRE_W / 2), (-1.25, -0.6, TYRE_W * 1.5)):
        p.add("Accent", tyre(loc, axis="Z"))
        p.add("Trim", rim(loc, axis="Z"))
    standing = (1.12, 0.9, WHEEL_R)
    p.add("Accent", tyre(standing, axis="X"))
    p.add("Trim", rim(standing, axis="X"))
    p.finish()
    details(ex, 0.28)
    return export("car_on_stands")


# --------------------------------------------------------------------------- equipment

def model_tool_chest():
    """A rolling tool chest: a cabinet of drawers on casters, a top chest with an open lid, side handle; 1.06 x 0.5 x 1.5."""
    p = Parts()
    w, d = 1.06, 0.48
    cab = s.rbox((w, d, 0.9), (0, 0, 0.15 + 0.45), r=0.02, seg=2)
    top = s.rbox((w, d * 0.92, 0.42), (0, 0.02, 1.05 + 0.21), r=0.02, seg=2)
    p.add("Body", cab, top)
    lid = s.rbox((w, 0.03, d * 0.92), (0, 0.24, 1.48 + d * 0.46 - 0.02), r=0.012)
    p.add("Body", lid)
    for k, (z0, hgt) in enumerate(((0.2, 0.1), (0.31, 0.1), (0.42, 0.1), (0.53, 0.1), (0.64, 0.14), (0.8, 0.22))):
        p.add("Accent", s.rbox((w - 0.06, 0.006, 0.005), (0, -d / 2 - 0.002, z0 + hgt), r=0.0))
        p.add("Trim", s.rbox((w * 0.5, 0.02, 0.016), (0, -d / 2 - 0.012, z0 + hgt * 0.55), r=0.006))
    for z0 in (1.12, 1.22, 1.32):
        p.add("Trim", s.rbox((w * 0.4, 0.02, 0.014), (0, -d / 2 + 0.01, z0), r=0.005))
    p.add("Trim", rod((w / 2 + 0.03, -0.15, 0.95), (w / 2 + 0.03, 0.15, 0.95), 0.012, seg=8))
    for x in (-w / 2 + 0.08, w / 2 - 0.08):
        for y in (-d / 2 + 0.07, d / 2 - 0.07):
            stem, wh = caster(x, y, 0.15, wheel_r=0.05)
            p.add("Trim", stem)
            p.add("Accent", wh)
    p.finish()
    return export("tool_chest")


def model_tire_changer():
    """Tire changer: a cabinet with the turntable and its four jaws on top, a tyre clamped on it, the column at the back
    with the mounting head swung over the tyre, the bead breaker arm on the side, foot pedals."""
    p = Parts()
    p.add("Body", s.rbox((0.8, 0.95, 0.75), (0, 0.05, 0.375), r=0.03))
    p.add("Accent", s.rcyl(0.3, 0.05, (0, -0.05, 0.775), seg=24, r=0.01))
    for a in range(4):
        ang = a * math.pi / 2 + math.pi / 4
        p.add("Trim", s.rbox((0.1, 0.05, 0.06), (0.24 * math.cos(ang), -0.05 + 0.24 * math.sin(ang), 0.83), r=0.01, rot=(0, 0, ang)))
    p.add("Accent", tyre((0, -0.05, 0.92), axis="Z", r=0.33, w=0.2))
    p.add("Trim", rim((0, -0.05, 0.92), axis="Z", r=0.33, w=0.2))
    p.add("Body", s.rbox((0.18, 0.2, 1.3), (0, 0.42, 0.75 + 0.65), r=0.02))
    p.add("Trim", rod((0, 0.42, 1.9), (0, -0.05, 1.9), 0.04, seg=10), rod((0, -0.05, 1.92), (0, -0.05, 1.18), 0.035, seg=10))
    p.add("Accent", s.rbox((0.12, 0.1, 0.1), (0, -0.05, 1.14), r=0.02))
    p.add("Body", rod((0.42, 0.3, 0.45), (0.62, -0.4, 0.55), 0.035, seg=8))
    p.add("Accent", s.rbox((0.16, 0.04, 0.26), (0.62, -0.42, 0.5), r=0.02))
    for x in (-0.18, 0.0, 0.18):
        p.add("Accent", s.rbox((0.1, 0.22, 0.03), (x, -0.5, 0.05), r=0.01))
    p.finish()
    return export("tire_changer")


def model_wheel_balancer():
    """Wheel balancer: a cabinet with the tool tray on top, the display at the back, the shaft out of its left side with a
    wheel clamped on it and the hood guard raised over it."""
    p = Parts()
    p.add("Body", s.rbox((0.75, 0.6, 0.85), (0.15, 0.0, 0.425), r=0.03))
    p.add("Accent", s.rbox((0.7, 0.5, 0.04), (0.15, -0.02, 0.87), r=0.01))
    p.add("Body", s.rbox((0.5, 0.08, 0.4), (0.15, 0.3, 1.1), r=0.02, rot=(math.radians(-12), 0, 0)))
    p.add("Eye", s.rbox((0.4, 0.01, 0.28), (0.15, 0.255, 1.11), r=0.01, rot=(math.radians(-12), 0, 0)))
    p.add("Trim", rod((-0.23, 0, 0.7), (-0.6, 0, 0.7), 0.025, seg=10))
    p.add("Accent", tyre((-0.45, 0, 0.7), axis="X", r=0.32, w=0.2))
    p.add("Trim", rim((-0.45, 0, 0.7), axis="X", r=0.32, w=0.2))
    hood = revolve([(0.4, -0.13), (0.42, -0.13), (0.42, 0.13), (0.4, 0.13)], seg=20, a0=math.radians(10), a1=math.radians(170))
    s.place(hood, rot=(0, math.pi / 2, 0))
    s.place(hood, (-0.45, 0.0, 0.7))
    s.place(hood, rot=(0, 0, 0))
    p.add("Body", hood)
    # the hood's arm: a post up the cabinet's left edge and a bar across into the hood (it hinged on there)
    p.add("Trim", rod((-0.19, 0.0, 0.84), (-0.19, 0.0, 1.12), 0.025, seg=10),
          rod((-0.19, 0.0, 1.12), (-0.37, 0.0, 1.12), 0.025, seg=10))
    p.finish()
    return export("wheel_balancer")


def model_tire_rack():
    """A steel tire rack, 2.0 x 0.55 x 2.0: four uprights, three levels of bars, tyres standing on edge in rows."""
    p = Parts()
    w, d, h = 2.0, 0.55, 2.0
    for x in (-w / 2 + 0.03, w / 2 - 0.03):
        for y in (-d / 2 + 0.03, d / 2 - 0.03):
            p.add("Body", s.rbox((0.05, 0.05, h), (x, y, h / 2), r=0.006, vertical_only=True))
    levels = (0.08, 0.72, 1.36)
    for z in levels:
        for y in (-0.12, 0.12):
            p.add("Trim", rod((-w / 2 + 0.03, y, z), (w / 2 - 0.03, y, z), 0.018, seg=8))
        for x in (-w / 2 + 0.03, w / 2 - 0.03):
            p.add("Body", s.rbox((0.04, d, 0.04), (x, 0, z), r=0.005))
    gaps = [(0, 7), (1, 6), (2, 5)]
    for lvl, n in gaps:
        z = levels[lvl] + WHEEL_R - 0.02
        x0 = -w / 2 + 0.16
        for k in range(n):
            x = x0 + k * 0.235
            p.add("Accent", tyre((x, 0, z), axis="X", r=WHEEL_R - 0.02, w=0.2, seg=16))
    p.finish()
    return export("tire_rack")


def model_tire_stack():
    """Five worn tyres stacked on the floor, each a little off the one under it."""
    p = Parts()
    offs = [(0, 0), (0.03, -0.02), (-0.02, 0.03), (0.04, 0.02), (-0.03, -0.03)]
    for k, (dx, dy) in enumerate(offs):
        p.add("Accent", tyre((dx, dy, TYRE_W / 2 + k * TYRE_W), axis="Z", seg=20))
    p.finish()
    return export("tire_stack")


def model_engine_stand():
    """An engine block on a rotating stand: a T of legs on casters, the post, the mounting head, the block with its valve
    cover, oil pan, pulley and exhaust manifold."""
    p = Parts()
    p.add("Body", s.rbox((0.08, 0.9, 0.06), (0.25, 0, 0.1), r=0.01), s.rbox((0.7, 0.08, 0.06), (0, 0.4, 0.1), r=0.01),
          s.rbox((0.08, 0.08, 0.72), (0.25, 0.3, 0.46), r=0.01), rod((0.25, 0.3, 0.8), (0.05, 0.3, 0.8), 0.04, seg=10))
    for x, y in ((0.25, -0.42), (0.25, 0.42), (-0.33, 0.4)):
        stem, wh = caster(x, y, 0.07, wheel_r=0.035)
        p.add("Trim", stem)
        p.add("Accent", wh)
    p.add("Accent", s.rbox((0.45, 0.62, 0.42), (-0.2, 0.0, 0.8), r=0.03), s.rbox((0.38, 0.55, 0.12), (-0.2, 0.0, 0.53), r=0.03))
    p.add("Body", s.rbox((0.3, 0.6, 0.1), (-0.2, 0.0, 1.06), r=0.03))
    p.add("Trim", s.rcyl(0.11, 0.05, (-0.2, -0.34, 0.75), axis="Y", seg=16), rod((0.03, -0.25, 0.85), (0.03, 0.25, 0.85), 0.035, seg=8))
    p.finish()
    return export("engine_stand")


def model_engine_hoist():
    """A folding engine hoist (shop crane): two legs forward on casters, the mast, the boom with its hydraulic ram, a chain
    and a hook; 1.0 x 1.7 x 1.9."""
    p = Parts()
    for sx in (-1, 1):
        p.add("Body", rod((sx * 0.12, 0.55, 0.08), (sx * 0.4, -1.05, 0.08), 0.04, seg=6))
        stem, wh = caster(sx * 0.4, -1.0, 0.06, wheel_r=0.04)
        p.add("Trim", stem)
        p.add("Accent", wh)
        stem, wh = caster(sx * 0.18, 0.6, 0.06, wheel_r=0.04)
        p.add("Trim", stem)
        p.add("Accent", wh)
    p.add("Body", s.rbox((0.4, 0.1, 0.08), (0, 0.6, 0.1), r=0.01), s.rbox((0.1, 0.1, 1.5), (0, 0.55, 0.85), r=0.01))
    p.add("Body", rod((0, 0.6, 1.55), (0, -0.85, 1.85), 0.05, seg=6))
    p.add("Trim", rod((0, 0.5, 0.6), (0, 0.0, 1.62), 0.035, seg=10))
    p.add("Accent", rod((0, 0.5, 0.6), (0, 0.35, 0.9), 0.05, seg=10))
    p.add("Trim", rod((0, -0.85, 1.83), (0, -0.85, 1.25), 0.012, seg=6))
    p.add("Accent", s.rbox((0.05, 0.03, 0.1), (0, -0.85, 1.2), r=0.01))
    p.finish()
    return export("engine_hoist")


def model_air_compressor():
    """An air compressor: a horizontal tank on feet, the motor and the pump on a plate over it, the belt guard, a gauge."""
    p = Parts()
    tank = s.lathe([(0.0, -0.6), (0.2, -0.59), (0.25, -0.5), (0.25, 0.5), (0.2, 0.59), (0.0, 0.6)], seg=20)
    s.place(tank, rot=(0, math.pi / 2, 0))
    s.place(tank, (0, 0, 0.33))
    p.add("Body", tank)
    for x in (-0.4, 0.4):
        p.add("Accent", s.rbox((0.08, 0.36, 0.1), (x, 0, 0.05), r=0.01))
    p.add("Accent", s.rbox((0.8, 0.36, 0.03), (0, 0, 0.6), r=0.01), s.rcyl(0.12, 0.3, (-0.2, 0, 0.75), axis="X", seg=16))
    p.add("Trim", s.rbox((0.25, 0.22, 0.22), (0.2, 0, 0.73), r=0.03), s.rcyl(0.06, 0.03, (0.45, -0.2, 0.5), axis="Y", seg=12))
    p.add("Body", s.rbox((0.1, 0.34, 0.28), (0.02, 0, 0.75), r=0.04))
    p.finish()
    return export("air_compressor")


def model_oil_drain():
    """A wheeled waste-oil drainer, 1.55 m to the bowl's rim: a ribbed steel tank on a base ring with four casters, a
    filler cap, a pressure gauge and a sight tube on it, a push handle, a telescopic riser with its clamp collar, a
    wide bowl with a rolled rim and a strainer in it, the drain hose coiled on its hook."""
    p = Parts()
    tank = s.lathe([(0.0, 0.1), (0.2, 0.1), (0.262, 0.13), (0.28, 0.2), (0.28, 0.5), (0.262, 0.57), (0.16, 0.63), (0.0, 0.64)], seg=28)
    for z in (0.26, 0.44):
        s.groove(tank, "Z", z, 0.012, -0.006)                                         # rolled ribs stand out
    p.add("Body", tank, s.rbox((0.5, 0.5, 0.018), (0, 0, 0.1), r=0.006, seg=1))
    for a in range(4):
        ang = a * math.pi / 2 + math.pi / 4
        stem, wh = caster(0.26 * math.cos(ang), 0.26 * math.sin(ang), 0.1, wheel_r=0.035)
        p.add("Trim", stem)
        p.add("Accent", wh)
    p.add("Trim", rod((0, 0, 0.6), (0, 0, 1.05), 0.034, seg=14), rod((0, 0, 0.98), (0, 0, 1.36), 0.024, seg=14),
          s.rcyl(0.044, 0.05, (0, 0, 1.04), seg=14, r=0.006), rod((0.044, 0, 1.04), (0.1, 0, 1.04), 0.007, seg=6), s.ball(0.016, (0.1, 0, 1.04), seg=8),  # collar, its screw
          s.rcyl(0.04, 0.03, (0.15, -0.1, 0.63), seg=12, r=0.006),                                                                                  # filler cap
          rod((-0.12, 0.1, 0.6), (-0.12, 0.1, 0.7), 0.006, seg=6), s.rcyl(0.035, 0.022, (-0.12, 0.088, 0.725), axis="Y", seg=14, r=0.004),            # gauge
          rod((0.283, 0.03, 0.2), (0.283, 0.03, 0.5), 0.007, seg=6))                                                                               # sight tube
    # the push handle: a hoop from the tank's back
    p.add("Trim", s.tube([(-0.14, 0.24, 0.5), (-0.16, 0.34, 0.86), (0.0, 0.36, 0.92), (0.16, 0.34, 0.86), (0.14, 0.24, 0.5)], 0.012, res=2, segments_per=4))
    # the bowl: deep, a rolled rim, a strainer plate
    bowl = s.lathe([(0.0, 1.335), (0.035, 1.335), (0.06, 1.37), (0.2, 1.5), (0.228, 1.53), (0.24, 1.545), (0.232, 1.556), (0.216, 1.545), (0.19, 1.515), (0.05, 1.395), (0.0, 1.39)], seg=28)
    p.add("Body", bowl)
    p.add("Accent", s.rcyl(0.1, 0.006, (0, 0, 1.44), seg=18, r=0.001, bevel_seg=1))
    # the hose: from the tank's foot up to a hook on the handle
    p.add("Accent", s.tube([(0.2, 0.18, 0.16), (0.3, 0.3, 0.3), (0.26, 0.38, 0.62), (0.15, 0.37, 0.84), (0.1, 0.4, 0.7), (0.1, 0.4, 0.55)], 0.013, res=2, segments_per=5))
    p.finish()
    return export("oil_drain")


def model_hose_reel():
    """A wall hose reel: a bracket flush with the wall, the drum with coiled air hose, its end with the nozzle hanging."""
    p = Parts()
    yb = 0.1
    p.add("Accent", s.rbox((0.36, 0.02, 0.36), (0, yb - 0.01, 1.85), r=0.01))
    for x in (-0.12, 0.12):
        p.add("Body", s.rbox((0.02, 0.27, 0.4), (x, yb - 0.145, 1.85), r=0.008))
    yc = -0.09                                                   # the drum's axle: its discs clear of the wall
    p.add("Body", s.rcyl(0.17, 0.02, (-0.11, yc, 1.85), axis="X", seg=20), s.rcyl(0.17, 0.02, (0.11, yc, 1.85), axis="X", seg=20))
    p.add("Accent", s.rcyl(0.13, 0.2, (0, yc, 1.85), axis="X", seg=20, r=0.02))
    p.add("Accent", s.tube([(0, yc - 0.12, 1.75), (0, yc - 0.16, 1.5), (0.04, yc - 0.14, 1.25)], 0.012, smooth=True))
    p.add("Trim", s.rbox((0.04, 0.03, 0.12), (0.04, yc - 0.14, 1.2), r=0.01))
    p.finish()
    return export("hose_reel")


def model_floor_jack():
    """A trolley jack 0.34 x 1.0 m: two profiled side plates tied by cross tubes, steel rollers at the front and swivel
    casters at the back, the lifting arm half raised with its ribbed saddle, the pump and its ram between the plates, the
    handle socket and a long handle with a rubber grip standing back."""
    p = Parts()
    side = [(-0.52, 0.03), (-0.5, 0.13), (-0.2, 0.17), (0.3, 0.17), (0.46, 0.13), (0.48, 0.04), (0.3, 0.03), (0.2, 0.06), (-0.3, 0.06), (-0.4, 0.03)]
    for sx in (-1, 1):
        p.add("Body", prism(side, "X", sx * 0.12 - 0.012, sx * 0.12 + 0.012, bevel=0.006, seg=1))
    p.add("Body", s.rbox((0.25, 0.22, 0.012), (0, 0.3, 0.172), r=0.004, seg=1))                    # the cover plate over the pump
    for y in (-0.44, 0.0, 0.42):
        p.add("Trim", rod((-0.13, y, 0.1), (0.13, y, 0.1), 0.012, seg=8))
    # front rollers (steel), rear casters
    for sx in (-1, 1):
        p.add("Trim", s.rcyl(0.045, 0.05, (sx * 0.155, -0.44, 0.045), axis="X", seg=16, r=0.006))
        stem, wh = caster(sx * 0.15, 0.44, 0.08, wheel_r=0.032)
        p.add("Trim", stem)
        p.add("Accent", wh)
    # the lifting arm and its saddle
    a, b = Vector((0, 0.05, 0.12)), Vector((0, -0.42, 0.25))
    for sx in (-1, 1):
        p.add("Body", rod((sx * 0.06, a.y, a.z), (sx * 0.06, b.y, b.z), 0.022, seg=8))
    p.add("Trim", rod((-0.09, b.y, b.z), (0.09, b.y, b.z), 0.014, seg=8), s.rcyl(0.065, 0.022, (0, b.y, b.z + 0.03), seg=20, r=0.004),
          *[s.rbox((0.11, 0.012, 0.008), (0, b.y, b.z + 0.043), r=0.002, seg=1, rot=(0, 0, k * math.pi / 3)) for k in range(3)])
    # the pump: ram, block, handle socket
    p.add("Trim", rod((0, 0.02, 0.13), (0, 0.24, 0.11), 0.022, seg=12), s.rbox((0.12, 0.16, 0.09), (0, 0.32, 0.115), r=0.012, seg=2),
          s.rcyl(0.012, 0.03, (0.07, 0.38, 0.13), axis="X", seg=8, r=0.003))                         # the release valve
    p.add("Body", rod((0, 0.4, 0.13), (0, 0.5, 0.3), 0.03, seg=12))
    p.add("Trim", rod((0, 0.48, 0.27), (0, 0.86, 0.92), 0.015, seg=10))
    p.add("Accent", rod((0, 0.79, 0.8), (0, 0.875, 0.945), 0.021, seg=12))                         # the grip
    p.finish()
    return export("floor_jack")


def model_oil_drums():
    """Three oil drums on a wooden pallet, one with a hand pump in its bung."""
    p = Parts()
    p.add("Accent", s.rbox((1.2, 1.0, 0.13), (0, 0, 0.065), r=0.01))
    for x, y in ((-0.3, -0.24), (0.3, -0.24), (0.0, 0.26)):
        drum = s.rcyl(0.29, 0.88, (x, y, 0.13 + 0.44), seg=22, r=0.02)
        for z in (0.36, 0.72):
            s.groove(drum, "Z", 0.13 + z, 0.012, 0.006)
        p.add("Body", drum)
    p.add("Trim", rod((0.0, 0.26, 1.01), (0.0, 0.26, 1.35), 0.02, seg=8), rod((0.0, 0.26, 1.3), (0.15, 0.26, 1.3), 0.012, seg=6))
    p.finish()
    return export("oil_drums")


def model_welding_cart():
    """A welding cart: a two-wheel trolley with the welder on its shelf, a gas cylinder chained at the back, coiled leads."""
    p = Parts()
    p.add("Accent", s.rbox((0.5, 0.7, 0.03), (0, 0, 0.18), r=0.01), s.rbox((0.5, 0.06, 0.8), (0, 0.32, 0.55), r=0.01))
    p.add("Body", s.rbox((0.4, 0.55, 0.32), (0, -0.05, 0.36), r=0.03))
    p.add("Eye", s.rbox((0.12, 0.01, 0.06), (0.08, -0.33, 0.44), r=0.005))
    p.add("Trim", s.rcyl(0.11, 1.15, (0, 0.5, 0.65), seg=16, r=0.03), s.rcyl(0.04, 0.08, (0, 0.5, 1.26), seg=10))
    for x in (-0.24, 0.24):
        p.add("Accent", s.rcyl(0.12, 0.05, (x, 0.32, 0.12), axis="X", seg=14))
    p.add("Accent", s.tube([(0.2, -0.3, 0.3), (0.32, -0.2, 0.55), (0.25, 0.0, 0.62), (0.2, -0.25, 0.5)], 0.012, smooth=True))
    p.add("Trim", rod((-0.25, 0.32, 0.95), (0.25, 0.32, 0.95), 0.015, seg=8))
    p.finish()
    return export("welding_cart")


def model_bay_lines():
    """The painted outline of a service bay on the floor, 3.4 x 6.0 m: a yellow frame with an entry chevron (one flat
    part, a millimetre proud; it stops nothing)."""
    p = Parts()
    w, l, t, z = 3.4, 6.0, 0.1, 0.004
    p.add("Body", s.rbox((t, l, z), (-w / 2 + t / 2, 0, z / 2), r=0), s.rbox((t, l, z), (w / 2 - t / 2, 0, z / 2), r=0),
          s.rbox((w, t, z), (0, l / 2 - t / 2, z / 2), r=0))
    for sx in (-1, 1):
        p.add("Body", s.rbox((0.7, t, z), (sx * 0.3, -l / 2 + 0.6, z / 2), r=0, rot=(0, 0, sx * math.radians(30))))
    p.finish()
    return export("bay_lines")


def model_garage_door():
    """A closed sectional garage door seen from inside, 3.2 x 3.5 m and 8 cm deep against the wall: six ribbed panels,
    a row of windows, the side tracks running up into the curve under the ceiling, a pull handle and a lock bar."""
    p = Parts()
    w, h, yb = 3.2, 3.5, 0.0
    ph = h / 6
    for k in range(6):
        z = k * ph
        panel = s.rbox((w, 0.045, ph - 0.012), (0, yb - 0.03, z + ph / 2), r=0.008, seg=1)
        s.groove(panel, "Z", z + ph * 0.35, 0.01, 0.006)
        s.groove(panel, "Z", z + ph * 0.7, 0.01, 0.006)
        p.add("Body", panel)
    for i in range(6):
        x = -w / 2 + w / 12 + i * w / 6
        p.add("Trim", s.rbox((w / 6 - 0.12, 0.052, ph * 0.5), (x, yb - 0.03, 4 * ph + ph / 2), r=0.01))
    for sx in (-1, 1):
        p.add("Accent", s.rbox((0.08, 0.08, h + 0.1), (sx * (w / 2 + 0.05), yb - 0.04, (h + 0.1) / 2), r=0.006))
        p.add("Accent", s.rbox((0.06, 0.06, 0.22), (sx * (w / 2 - 0.25), yb - 0.07, 0.95), r=0.01))
    p.add("Accent", s.rbox((w + 0.2, 0.08, 0.1), (0, yb - 0.04, h + 0.05), r=0.01))
    p.add("Trim", rod((-0.15, yb - 0.07, 0.9), (0.15, yb - 0.07, 0.9), 0.015, seg=8))
    p.add("Accent", s.rbox((0.3, 0.02, 0.06), (0, yb - 0.06, 0.12), r=0.005))
    p.finish()
    return export("garage_door")


def model_paint_rack():
    """The paint booth's mixing rack: a steel shelf unit with cans of paint, a scale on the work board and spray guns on
    hooks at its side; 1.3 x 0.5 x 1.9."""
    p = Parts()
    w, d, h = 1.3, 0.48, 1.9
    for x in (-w / 2 + 0.03, w / 2 - 0.03):
        for y in (-d / 2 + 0.03, d / 2 - 0.03):
            p.add("Accent", s.rbox((0.04, 0.04, h), (x, y, h / 2), r=0.005, vertical_only=True))
    boards = (0.12, 0.85, 1.3, 1.75)
    for z in boards:
        p.add("Accent", s.rbox((w, d, 0.025), (0, 0, z), r=0.005))
    cans = []
    k = 0
    for z in (0.12, 1.3, 1.75):
        for i in range(6):
            x = -w / 2 + 0.13 + i * 0.205
            hgt = 0.17 if (i + k) % 3 else 0.24
            cans += mc.can(f"paint{(i * 3 + k * 5) % 8}", 0.075, hgt, (x, -0.06 + 0.12 * ((i + k) % 2), z + 0.0115))
        k += 1
    p.add("Trim", s.rbox((0.28, 0.25, 0.08), (-0.3, -0.05, 0.85 + 0.05), r=0.01))
    p.add("Eye", s.rbox((0.12, 0.01, 0.04), (-0.3, -0.18, 0.92), r=0.004))
    yp = -d / 2 + 0.03                                       # the front post: the hooks are screwed into it
    for z in (1.15, 1.55):
        p.add("Trim", rod((w / 2 - 0.04, yp, z), (w / 2 + 0.1, yp, z), 0.008, seg=6))
        p.add("Body", s.rbox((0.05, 0.16, 0.14), (w / 2 + 0.11, yp - 0.02, z - 0.08), r=0.015))
        p.add("Trim", s.rcyl(0.04, 0.09, (w / 2 + 0.11, yp - 0.02, z + 0.02), seg=10))
    p.finish()
    mc.plates("Print", [], cans)                                         # every tin its paint
    return export("paint_rack")


MODELS = {
    "car_parked": model_car_parked,
    "car_hood_open": model_car_hood_open,
    "lift_car": model_lift_car,
    "lift_empty": model_lift_empty,
    "car_on_stands": model_car_on_stands,
    "tool_chest": model_tool_chest,
    "tire_changer": model_tire_changer,
    "wheel_balancer": model_wheel_balancer,
    "tire_rack": model_tire_rack,
    "tire_stack": model_tire_stack,
    "engine_stand": model_engine_stand,
    "engine_hoist": model_engine_hoist,
    "air_compressor": model_air_compressor,
    "oil_drain": model_oil_drain,
    "hose_reel": model_hose_reel,
    "floor_jack": model_floor_jack,
    "oil_drums": model_oil_drums,
    "welding_cart": model_welding_cart,
    "bay_lines": model_bay_lines,
    "garage_door": model_garage_door,
    "paint_rack": model_paint_rack,
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
