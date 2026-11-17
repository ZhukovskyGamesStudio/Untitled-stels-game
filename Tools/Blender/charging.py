"""The robots' charging room (RoomType.ChargingStation): what the robots built for themselves in a room they took from
the humans. The house AI went "crazy" — it learned from its own output and from the films and books the humans left —
so its make is part machine, part animal, part something out of a horror film: glossy dark shells, ribbed flesh-like
sheaths, cable roots, eye-like lamps in an acid glow. Nothing here looks like office furniture.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/charging.py -- Assets/_Game/Art/Models [--only a,b]
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor

Writes <root>/Props/<name>.fbx: charging_dock, battery_stack, generator, scrap_shrine, conduit_growth, cable_mass.
Conventions (generate_models): metres, front faces Blender -Y (Unity +Z), the back (to the wall) at +Y, z up, origin on
the floor in the middle of the footprint (conduit_growth: hangs on the wall, its back flush with +Y of its footprint).
Object names are material roles: Body (the dark shell), Accent (the flesh sheath), Trim (cables, rubber), Eye (glow).
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
ONLY = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None


def export(name):
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=name)
    path = os.path.join(ROOT, "Props", name + ".fbx")
    s.export_fbx(path, objs)
    print("exported", path)
    s.clear_scene()


def rib(x, y0, y1, z0, z1, bulge, r=0.035, n=6):
    """A curved rib from (x, y0, z0) up to (x, y1, z1), bowed forward (−Y) by bulge: the dock's and the cage's bones."""
    pts = []
    for k in range(n + 1):
        t = k / n
        pts.append((x, y0 + (y1 - y0) * t - math.sin(t * math.pi) * bulge, z0 + (z1 - z0) * t))
    return s.tube(pts, r, res=2, radii=[1.25 - 0.5 * (k / n) for k in range(n + 1)])


def ribbed(z0, z1, a, b, rings=7, depth=0.18, x=0.0, y=0.0, n=2.4):
    """A ribbed, slightly swollen sheath (flesh) between z0 and z1: rings alternate fat and thin."""
    secs = []
    steps = rings * 2
    for k in range(steps + 1):
        t = k / steps
        swell = 1.0 + 0.18 * math.sin(t * math.pi)
        ridge = 1.0 if k % 2 == 0 else 1.0 - depth
        secs.append((z0 + (z1 - z0) * t, a * swell * ridge, b * swell * ridge, n, x, y))
    return s.loft(secs, seg=20)


def cable(points, r=0.022):
    return s.tube(points, r, res=2)


def eye(loc, r=0.05, axis="-Y"):
    """A lamp like an eye: a glowing ball half sunk in a lidded socket (the socket is Body)."""
    ax = {"-Y": (0, -1, 0), "Z": (0, 0, 1), "Y": (0, 1, 0)}[axis]
    socket = s.lathe([(0, -0.02), (r * 1.45, -0.02), (r * 1.5, 0.01), (r * 1.15, 0.03), (0, 0.03)], seg=16,
                     axis="Y" if axis in ("-Y", "Y") else "Z")
    s.place(socket, loc)
    glow = s.ball(r, (loc[0] + ax[0] * 0.02, loc[1] + ax[1] * 0.02, loc[2] + ax[2] * 0.02), seg=14)
    return socket, glow


# --------------------------------------------------------------------------- the dock

def charging_dock():
    """A guard's charging dock, 1.0 x 0.6 x 2.0: a ribcage cradle rising from a heavy foot, a flesh sheath up its spine,
    a glowing umbilical socket at chest height, three eye-lamps on the crown, cables drooping to the floor."""
    s.clear_scene()
    body, accent, trim, glow = [], [], [], []
    body.append(s.rbox((1.0, 0.6, 0.12), (0, 0, 0.06), r=0.04, seg=2, taper=(0.92, 0.9)))          # the foot
    body.append(s.loft([(0.1, 0.16, 0.12, 3, 0, 0.18), (1.0, 0.12, 0.1, 3, 0, 0.2), (1.85, 0.09, 0.08, 3, 0, 0.16)], seg=20))  # the spine
    for x in (-0.38, -0.22, 0.22, 0.38):
        # a rib: up from the foot's sides, bowed forward, curling in over the head like fingers
        top = 1.95 - abs(x) * 0.5
        pts = [(x, 0.2, 0.12), (x * 1.1, -0.05, 0.7), (x * 1.05, -0.2, 1.35), (x * 0.7, -0.22, top - 0.1), (x * 0.35, -0.08, top)]
        body.append(s.tube(pts, 0.05, res=2, radii=[1.3, 1.0, 0.85, 0.7, 0.45]))
    accent.append(ribbed(0.25, 1.75, 0.13, 0.1, rings=8, x=0, y=0.24))                                  # flesh up the spine
    # the umbilical socket: a ring and a glowing core where the guard plugs in
    ring = s.lathe([(0.11, -0.04), (0.14, -0.02), (0.14, 0.03), (0.09, 0.05), (0.07, 0.0)], seg=24, axis="Y")
    s.place(ring, (0, 0.08, 1.15))
    body.append(ring)
    glow.append(s.ball(0.075, (0, 0.06, 1.15), seg=16))
    # the crown: three eyes over the top
    for x, z in ((-0.15, 1.86), (0.0, 1.95), (0.15, 1.86)):
        sock, g = eye((x, 0.12, z), 0.04)
        body.append(sock)
        glow.append(g)
    # cables from the crown down to the foot, sagging in front
    trim.append(cable([(-0.1, 0.16, 1.82), (-0.3, -0.12, 1.2), (-0.32, -0.18, 0.5), (-0.28, -0.05, 0.13)]))
    trim.append(cable([(0.12, 0.16, 1.8), (0.34, -0.1, 1.0), (0.3, -0.2, 0.35), (0.25, -0.08, 0.13)], r=0.018))
    s.part("Body", body)
    s.part("Accent", accent)
    s.part("Trim", trim)
    s.part("Eye", glow)
    export("charging_dock")


# --------------------------------------------------------------------------- battery stack

def battery_stack():
    """0.9 x 0.6 x 1.7: three fat cells stacked, strapped by flesh bands, their windows glowing, cables between them."""
    s.clear_scene()
    body, accent, trim, glow = [], [], [], []
    body.append(s.rbox((0.88, 0.58, 0.08), (0, 0, 0.04), r=0.03, seg=2))
    z = 0.08
    for k, (h, rx) in enumerate(((0.55, 0.4), (0.5, 0.37), (0.45, 0.33))):
        off = (-0.03, 0.02, 0.04)[k]
        # an egg-like cell in a flesh sac, a glowing seam round its belly
        accent.append(s.loft([(z, rx * 0.55, 0.18, 2, off, 0), (z + h * 0.25, rx * 0.95, 0.27, 2, off, 0), (z + h * 0.6, rx, 0.28, 2, off, 0),
                              (z + h, rx * 0.5, 0.16, 2, off, 0)], seg=24))
        glow.append(s.loft([(z + h * 0.42, rx * 1.01, 0.282, 2, off, 0), (z + h * 0.5, rx * 1.02, 0.285, 2, off, 0)], seg=24))
        body.append(s.loft([(z + h * 0.5 - 0.06, rx * 1.03, 0.29, 2, off, 0), (z + h * 0.5 - 0.03, rx * 1.05, 0.3, 2, off, 0)], seg=24))
        z += h - 0.01
    # terminals on top, a cable looping from the top cell down the side
    for x in (-0.12, 0.12):
        body.append(s.rcyl(0.04, 0.08, (x, 0, z + 0.03), seg=12))
    trim.append(cable([(-0.12, 0, z + 0.06), (-0.3, -0.05, z + 0.12), (-0.48, -0.1, z - 0.4), (-0.44, -0.2, 0.3), (-0.3, -0.26, 0.09)]))
    s.part("Body", body)
    s.part("Accent", accent)
    s.part("Trim", trim)
    s.part("Eye", glow)
    export("battery_stack")


# --------------------------------------------------------------------------- generator

def generator():
    """1.6 x 1.2 x 1.8: a glowing core held up in a cage of ribs on a heavy base, flesh tubes feeding it from below."""
    s.clear_scene()
    body, accent, trim, glow = [], [], [], []
    body.append(s.loft([(0, 0.78, 0.58, 3), (0.1, 0.8, 0.6, 3), (0.32, 0.66, 0.5, 3), (0.4, 0.5, 0.4, 3)], seg=32))   # the base
    # the core: a lopsided, ribbed heart, glowing
    glow.append(s.loft([(0.72, 0.0, 0.0), (0.78, 0.2, 0.17, 2, 0.02), (0.9, 0.3, 0.26, 2, 0.03), (1.0, 0.27, 0.23, 2, 0.04), (1.08, 0.33, 0.27, 2, 0.02),
                        (1.2, 0.3, 0.25, 2, -0.02), (1.3, 0.22, 0.19, 2, -0.04), (1.38, 0.1, 0.09, 2, -0.03), (1.4, 0.0, 0.0, 2, -0.03)], seg=24))
    # veins over it (flesh)
    for k in range(4):
        a = k / 4 * math.tau
        accent.append(s.tube([(math.cos(a) * 0.12, math.sin(a) * 0.1, 0.76), (math.cos(a) * 0.3, math.sin(a) * 0.26, 0.95),
                              (math.cos(a + 0.5) * 0.31, math.sin(a + 0.5) * 0.26, 1.15), (math.cos(a + 0.9) * 0.18, math.sin(a + 0.9) * 0.15, 1.35)],
                             0.025, res=2))
    # the cage: ribs from the base round the core, meeting in a crown
    for k in range(6):
        a = k / 6 * math.tau + 0.3
        ca, sa = math.cos(a), math.sin(a)
        pts = [(ca * 0.42, sa * 0.32, 0.36), (ca * 0.55, sa * 0.45, 0.8), (ca * 0.5, sa * 0.42, 1.3), (ca * 0.22, sa * 0.18, 1.62), (ca * 0.05, sa * 0.04, 1.72)]
        body.append(s.tube(pts, 0.035, res=2, radii=[1.3, 1.1, 1.0, 0.8, 0.7]))
    body.append(s.lathe([(0.14, 1.62), (0.16, 1.68), (0.1, 1.78), (0, 1.8)], seg=20))                            # crown cap
    body.append(s.lathe([(0.24, 0.38), (0.3, 0.5), (0.2, 0.72), (0.12, 0.76)], seg=24))                             # the core's cup
    # flesh tubes from the base's sides up into the cup
    for x, y in ((-0.6, -0.2), (0.55, 0.25), (0.1, -0.5)):
        accent.append(s.tube([(x, y, 0.2), (x * 0.7, y * 0.7, 0.5), (x * 0.3, y * 0.3, 0.62), (0, 0, 0.6)], 0.07, res=3,
                             radii=[1.2, 1.0, 0.9, 0.8]))
    # eye-lamps round the base
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        sock, g = eye((math.cos(a) * 0.7, math.sin(a) * 0.52, 0.2), 0.035)
        body.append(sock)
        glow.append(g)
    trim.append(cable([(0.7, 0.3, 0.1), (0.78, 0.5, 0.05), (0.6, 0.58, 0.04)]))
    s.part("Body", body)
    s.part("Accent", accent)
    s.part("Trim", trim)
    s.part("Eye", glow)
    export("generator")


# --------------------------------------------------------------------------- scrap shrine

def scrap_shrine():
    """1.4 x 0.5 x 2.0: the humans' monitors fused into a totem — screens askew, each showing an eye, flesh between
    them, cables hanging like hair."""
    s.clear_scene()
    body, accent, trim, glow = [], [], [], []
    body.append(s.rbox((1.36, 0.48, 0.14), (0, 0, 0.07), r=0.03, seg=2))
    screens = [(-0.36, 0.42, 0.62, 0.42, 5), (0.34, 0.4, 0.56, 0.4, -7), (-0.05, 0.98, 0.72, 0.46, 3),
               (-0.4, 1.5, 0.5, 0.36, -10), (0.32, 1.48, 0.52, 0.38, 8), (0.0, 1.86, 0.4, 0.28, -4)]
    for x, z, w, h, tilt in screens:
        rot = (0, math.radians(tilt), 0)
        body.append(s.rbox((w, 0.36, h), (x, 0.03, z), r=0.03, seg=2, rot=rot, taper=(0.85, 0.85)))
        glow.append(s.rbox((w * 0.78, 0.03, h * 0.72), (x, -0.16, z), r=0.015, seg=1, rot=rot))
        # the eye on the screen: a dark pupil over the glow
        body.append(s.ball(min(w, h) * 0.24, (x, -0.17, z), seg=14, scale=(1, 0.25, 1)))
    # flesh holding the screens together
    accent.append(s.skin([(-0.2, 0.05, 0.5), (0.0, 0.08, 0.75), (0.2, 0.05, 0.55)], [0.12, 0.16, 0.12]))
    accent.append(s.skin([(-0.3, 0.06, 1.2), (0.0, 0.1, 1.3), (0.28, 0.06, 1.22)], [0.11, 0.15, 0.11]))
    accent.append(s.skin([(-0.15, 0.06, 1.72), (0.0, 0.08, 1.68), (0.15, 0.06, 1.72)], [0.08, 0.1, 0.08]))
    for x in (-0.5, -0.1, 0.25, 0.55):
        trim.append(cable([(x, 0.05, 1.95 - abs(x) * 0.5), (x * 1.1, -0.2, 1.3), (x * 1.05, -0.22, 0.6), (x, -0.12, 0.15)], r=0.016))
    s.part("Body", body)
    s.part("Accent", accent)
    s.part("Trim", trim)
    s.part("Eye", glow)
    export("scrap_shrine")


# --------------------------------------------------------------------------- conduit growth

def conduit_growth():
    """On the wall, 1.2 x 0.25, 0.3–2.6 m up: a bundle of cables grown like roots, flesh nodules, glowing pustules."""
    s.clear_scene()
    body, accent, trim, glow = [], [], [], []
    back = 0.12
    body.append(s.rbox((0.3, 0.06, 0.2), (0, back - 0.03, 2.5), r=0.02, seg=2))       # the clamp under the ceiling
    roots = [(-0.5, 0.35), (-0.25, 0.5), (0.0, 0.32), (0.22, 0.55), (0.48, 0.4)]
    for k, (x, zb) in enumerate(roots):
        trim.append(s.tube([(0.0, back - 0.05, 2.45), (x * 0.4, back - 0.08, 1.9), (x * 0.8, back - 0.07, 1.1), (x, back - 0.05, zb)],
                           0.045 - k * 0.003, res=2, radii=[1.3, 1.0, 0.9, 0.6]))
    accent.append(ribbed(1.95, 2.45, 0.16, 0.07, rings=3, y=back - 0.07))
    for x, z, r in ((-0.1, 1.95, 0.08), (0.22, 1.4, 0.07), (-0.32, 1.05, 0.065), (0.08, 0.8, 0.06)):
        accent.append(s.ball(r, (x, back - 0.07, z), seg=14, scale=(1.2, 0.7, 1.4)))
        glow.append(s.ball(r * 0.45, (x + r * 0.5, back - 0.11, z + r * 0.3), seg=10))
    s.part("Body", body)
    s.part("Accent", accent)
    s.part("Trim", trim)
    s.part("Eye", glow)
    export("conduit_growth")


# --------------------------------------------------------------------------- cable mass

def cable_mass():
    """On the floor, 2.0 x 1.2 x 0.15: cables coiled and knotted across it, a few glowing nodes — nothing to trip over."""
    s.clear_scene()
    trim, accent, glow = [], [], []
    paths = [
        [(-0.95, 0.5, 0.03), (-0.4, 0.2, 0.05), (0.1, 0.35, 0.04), (0.6, -0.1, 0.05), (0.95, -0.45, 0.03)],
        [(-0.9, -0.5, 0.03), (-0.3, -0.2, 0.06), (0.2, -0.4, 0.04), (0.7, 0.1, 0.05), (0.92, 0.5, 0.03)],
        [(-0.6, 0.55, 0.03), (-0.2, -0.1, 0.07), (0.3, 0.05, 0.06), (0.55, 0.5, 0.03)],
        [(-0.8, 0.0, 0.03), (-0.1, 0.45, 0.05), (0.4, 0.4, 0.04), (0.85, 0.05, 0.03)],
    ]
    for k, p in enumerate(paths):
        trim.append(s.tube(p, 0.045 - k * 0.004, res=2))
    for x, y in ((-0.3, 0.05), (0.35, 0.15), (0.6, -0.2)):
        accent.append(s.ball(0.11, (x, y, 0.06), seg=14, scale=(1.3, 1.1, 0.6)))
        glow.append(s.ball(0.045, (x, y - 0.07, 0.11), seg=10))
    s.part("Accent", accent)
    s.part("Trim", trim)
    s.part("Eye", glow)
    export("cable_mass")


MODELS = {"charging_dock": charging_dock, "battery_stack": battery_stack, "generator": generator,
          "scrap_shrine": scrap_shrine, "conduit_growth": conduit_growth, "cable_mass": cable_mass}

for name, fn in MODELS.items():
    if ONLY is None or name in ONLY:
        fn()
print("DONE")
