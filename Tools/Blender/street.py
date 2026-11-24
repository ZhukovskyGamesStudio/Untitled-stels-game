"""The city round the house (CityPlan, scenery past the yard's invisible wall): street furniture, parked cars, trees and
the facade kit the planner hangs on the buildings' textured boxes.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/street.py -- Assets/_Game/Art/Models [--only a,b]
      (every file lands in <root>/City/city_<name>.fbx; the game's CityPropKind.X is city_<x in snake case>)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <root>/City/city_*.fbx --lineup 6 --no-check

The level builder does not instantiate these: it merges every placed copy into the city's one mesh (LevelBuilder.BuildCity),
so an object's NAME is the CitySlot its triangles are drawn with (Metal, Stone, Glass, Tyre, Wood, Bark, Leaves…) and two
names are filled in per copy by the planner: Livery (a car's paint, an awning's cloth, a building's trim) and Pane (a
window's glass: lit, dim or dark). Texture coordinates are recomputed in the world, so a piece may be stretched.

Contract (metres, Blender: front -Y = Unity +Z, z up):
  * street props and cars stand on the ground, origin on it in the middle of the footprint;
  * facade pieces hang on a wall: the wall's plane is y = 0, they stand out to -y; windows have their origin at the middle
    of the opening's bottom edge (the texture's window: CityPlan.WindowOf times the storey ST), bays (shop front,
    entrance, curtain wall) at the middle of the bay's bottom edge, a bay is ST wide;
  * profiles (cornice, band, coping, kerb, awning) run from x = -0.5 to 0.5 and are stretched along x by the planner
    (their end caps are square: only the long edges are bevelled); pilaster and drainpipe run z 0..1, stretched along z.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import garage  # noqa: E402  (the shop's sedan; imports generate_models, which reads the output root after --)
import meshfix  # noqa: E402
import shapes as s  # noqa: E402
from rooms2_work import prism, revolve, rod  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = ARGS[0] if ARGS and not ARGS[0].startswith("--") else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")

ST = 4.1  # a storey and a bay of the facade textures: CityPlan.Storey = LevelMetrics.FloorHeight
WIN_A = (0.18, 0.30, 0.82, 0.82)  # CityPlan.WindowOf (x0, y0, x1, y1 of the window in its storey square)
WIN_B = (0.25, 0.28, 0.75, 0.80)
WIN_C = (0.04, 0.12, 0.96, 0.94)


# --------------------------------------------------------------------------- helpers

class Parts:
    """Shapes collected by role (a CitySlot name), merged into one object per role at the end."""

    def __init__(self):
        self.roles = {}

    def add(self, role, *bms):
        lst = self.roles.setdefault(role, [])
        for b in bms:
            if isinstance(b, (list, tuple)):
                lst.extend(b)
            else:
                lst.append(b)

    def finish(self, sharp=35.0):
        for role, bms in self.roles.items():
            if bms:
                s.part(role, bms, sharp_angle=sharp)


class CarParts(Parts):
    """The shop's sedan speaks Body / Accent / Trim / Eye: on the street they are paint, rubber, glass and chrome lamps."""
    MAP = {"Body": "Livery", "Accent": "Tyre", "Trim": "Glass", "Eye": "Chrome"}

    def add(self, role, *bms):
        super().add(self.MAP.get(role, role), *bms)


def export(name, floor=True):
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=floor, name=name)
    path = os.path.join(ROOT, "City", "city_" + name + ".fbx")
    s.export_fbx(path, objs)
    s.clear_scene()
    return path


def tri_ngons(bm):
    """Concave caps (an architrave's U, a cornice's profile) are triangulated here, not by the importer."""
    faces = [f for f in bm.faces if len(f.verts) > 4]
    if faces:
        bmesh.ops.triangulate(bm, faces=faces, quad_method="BEAUTY", ngon_method="BEAUTY")
    return bm


def _bevel_along(bm, axis, r, seg):
    i = "XYZ".index(axis)
    edges = []
    for e in bm.edges:
        if not e.is_manifold:
            continue
        d = e.verts[1].co - e.verts[0].co
        if d.length < 1e-6 or abs(d.normalized()[i]) < 0.99:
            continue
        if e.calc_face_angle(0) > math.radians(25):
            edges.append(e)
    if edges and r > 0:
        bmesh.ops.bevel(bm, geom=edges, offset=r, offset_type="OFFSET", segments=seg, profile=0.5, affect="EDGES",
                        clamp_overlap=True)


def ext_x(pts_yz, x0=-0.5, x1=0.5, bevel=0.0, seg=1):
    """A profile (y, z) extruded along x; only the long edges are bevelled (the planner stretches it along x)."""
    bm = prism(pts_yz, "X", x0, x1)
    _bevel_along(bm, "X", bevel, seg)
    return tri_ngons(bm)


def ext_z(pts_xy, z0=0.0, z1=1.0, bevel=0.0, seg=1):
    """A profile (x, y) extruded up z; only the upright edges are bevelled (stretched along z)."""
    bm = prism(pts_xy, "Z", z0, z1)
    _bevel_along(bm, "Z", bevel, seg)
    return tri_ngons(bm)


def plate(pts_xz, y0, y1, bevel=0.0, seg=1):
    """An outline (x, z) in the wall's plane standing out from y0 to y1 (frames, hoods), every edge bevelled."""
    bm = prism(pts_xz, "Y", min(y0, y1), max(y0, y1))
    if bevel:
        s.bevel_edges(bm, bevel, seg, angle=30)
    return tri_ngons(bm)


def box(x0, x1, y0, y1, z0, z1, r=0.01, seg=1):
    """An axis-aligned box from corner to corner with rounded edges."""
    size = (abs(x1 - x0), abs(y1 - y0), abs(z1 - z0))
    return s.rbox(size, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), r=r, seg=seg)


def frame_ring(w, h, bar, y0, y1, z0=0.0, r=0.006):
    """A rectangular frame w x h (outside) of bars `bar` wide: two full-height stiles and two rails between them."""
    return [box(-w / 2, -w / 2 + bar, y0, y1, z0, z0 + h, r), box(w / 2 - bar, w / 2, y0, y1, z0, z0 + h, r),
            box(-w / 2 + bar - 0.004, w / 2 - bar + 0.004, y0 + 0.002, y1 - 0.002, z0, z0 + bar, r),
            box(-w / 2 + bar - 0.004, w / 2 - bar + 0.004, y0 + 0.002, y1 - 0.002, z0 + h - bar, z0 + h, r)]


def noisy(bm, amp, seed):
    """Pushes vertices along their normals by a smooth hash of their position (a crown of leaves, not a ball)."""
    bm.normal_update()
    for v in bm.verts:
        p = v.co
        k = math.sin(p.x * 3.1 + seed) * math.cos(p.y * 2.7 - seed * 0.7) + math.sin(p.z * 3.7 + seed * 1.3)
        k += 0.5 * math.sin((p.x + p.y) * 7.3 + seed * 2.1)
        v.co += v.normal * amp * k
    return bm


def foliage(balls, resolution, seed, amp, target_tris):
    """A leaf mass from overlapping balls (shapes.foliage)."""
    return s.foliage(balls, resolution, seed, amp, target_tris)


def ico(radius, loc, subdiv=2):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    return s.place(bm, loc)


# =========================================================================== street furniture

def m_lamp():
    """A street lamp, 6 m: a cast base, a tapering pole, a swan-neck arm reaching 1.6 m over the street (-y), a flat
    luminaire with its glowing lens underneath (the light, Level/CityLamp, hangs at (0, -1.6, 5.75))."""
    p = Parts()
    p.add("Metal", s.lathe([(0.2, 0.0), (0.2, 0.05), (0.16, 0.09), (0.15, 0.55), (0.12, 0.62), (0.1, 0.7), (0.0, 0.7)], seg=20))
    p.add("Metal", s.lathe([(0.17, 0.12), (0.17, 0.16), (0.0, 0.16)], seg=20))
    p.add("Metal", s.lathe([(0.095, 0.6), (0.085, 1.0), (0.06, 5.2), (0.0, 5.2)], seg=14))
    p.add("Metal", s.lathe([(0.075, 3.0), (0.075, 3.08), (0.0, 3.08)], seg=14))  # a collar a third of the way up
    # the arm: up out of the pole, then a swan neck out over the street
    p.add("Metal", s.tube([(0, 0, 5.0), (0, 0, 5.7), (0, -0.35, 6.05), (0, -1.0, 6.08), (0, -1.45, 5.98)], 0.045, res=2,
                          segments_per=6))
    head = s.loft([(0.0, 0.0, 0.0, 2), (0.03, 0.15, 0.36, 3), (0.1, 0.16, 0.38, 3.2), (0.16, 0.12, 0.3, 3), (0.2, 0.0, 0.0, 2)],
                  seg=24)
    s.place(head, (0, -1.62, 5.78))
    p.add("Metal", head)
    p.add("LampGlow", s.loft([(0.0, 0.12, 0.31, 3), (0.035, 0.13, 0.33, 3), (0.04, 0.0, 0.0, 2)], seg=24, loc=(0, -1.62, 5.765)))
    p.add("Metal", s.rbox((0.12, 0.03, 0.2), (0, -0.165, 0.35), r=0.01))  # the base's service door
    p.finish()
    return export("lamp")


def tree(seed, height, crown, blobs, lean=(0.0, 0.0)):
    """A street tree in its pit: a trunk that forks into a few limbs, a crown of lumpy leaf masses on them, the pit's
    soil and its cast-iron grate flush with the paving."""
    p = Parts()
    import random
    rnd = random.Random(seed)
    lx, ly = lean
    top = Vector((lx, ly, height))
    p.add("Bark", s.tube([(0, 0, -0.09), (lx * 0.3, ly * 0.3, height * 0.45), tuple(top)], 0.13, res=2, segments_per=4,
                         radii=[1.25, 0.95, 0.7]))
    p.add("Bark", s.lathe([(0.2, -0.02), (0.15, 0.08), (0.0, 0.2)], seg=12))  # the root flare
    ends = []
    for k in range(blobs):
        a = 2 * math.pi * k / blobs + rnd.uniform(-0.4, 0.4)
        rr = crown * rnd.uniform(0.35, 0.6)
        z = height + crown * rnd.uniform(0.25, 0.75)
        end = Vector((top.x + math.cos(a) * rr, top.y + math.sin(a) * rr, z))
        ends.append(end)
        mid = top.lerp(end, 0.5) + Vector((0, 0, 0.15))
        p.add("Bark", s.tube([tuple(top - Vector((0, 0, 0.25))), tuple(mid), tuple(end)], 0.07, res=1, segments_per=4,
                             radii=[1.0, 0.75, 0.5]))
    # the crown: a cluster of leaf masses round every limb's end and a big one over the top, melted into one cloud
    balls = [((top.x, top.y, height + crown * 0.8), crown * 0.62)]
    for k, end in enumerate(ends):
        r = crown * rnd.uniform(0.34, 0.46)
        balls.append((tuple(end + Vector((0, 0, r * 0.2))), r))
        for j in range(3):                                   # smaller lumps round it, outwards and up: a ragged edge
            a = rnd.uniform(0, 2 * math.pi)
            out = (end - top).normalized() if (end - top).length > 1e-3 else Vector((0, 0, 1))
            d = (out + Vector((math.cos(a), math.sin(a), rnd.uniform(-0.2, 0.7))) * 0.8).normalized()
            balls.append((tuple(end + d * r * rnd.uniform(0.6, 0.9)), r * rnd.uniform(0.45, 0.65)))
    p.add("Leaves", foliage(balls, crown * 0.08, seed, crown * 0.03, 2600))
    # the pit: soil a little under the grate, the grate's frame and its radial bars
    p.add("Soil", box(-0.58, 0.58, -0.58, 0.58, -0.05, 0.006, r=0.0))
    p.add("Metal", [box(-0.62, 0.62, -0.62, -0.54, 0.0, 0.02, r=0.004), box(-0.62, 0.62, 0.54, 0.62, 0.0, 0.02, r=0.004),
                    box(-0.62, -0.54, -0.54, 0.54, 0.0, 0.02, r=0.004), box(0.54, 0.62, -0.54, 0.54, 0.0, 0.02, r=0.004)])
    ring = revolve([(0.22, 0.0), (0.27, 0.0), (0.27, 0.018), (0.22, 0.018)], seg=20)
    p.add("Metal", ring)
    for k in range(8):
        a = k * math.pi / 4
        bar = s.rbox((0.32, 0.035, 0.016), (0, 0, 0.009), r=0.003)
        s.place(bar, rot=(0, 0, a))
        s.place(bar, (math.cos(a) * 0.41, math.sin(a) * 0.41, 0))
        p.add("Metal", bar)
    p.finish(sharp=89)


def m_tree_a():
    tree(11, 2.7, 1.45, 6, (0.08, -0.05))
    return export("tree_a")


def m_tree_b():
    tree(23, 3.1, 1.15, 5, (-0.1, 0.06))  # a narrower, taller one
    return export("tree_b")


def m_tree_c():
    tree(37, 2.5, 1.6, 7, (0.05, 0.1))   # a wide old one
    return export("tree_c")


def m_bench():
    """A park bench 1.8 m along x facing the street (-y): two cast-iron sides with arms, five seat slats, three back slats."""
    p = Parts()
    side = [(0.24, 0.0), (0.18, 0.0), (0.12, 0.38), (-0.2, 0.38), (-0.26, 0.0), (-0.32, 0.0), (-0.26, 0.42), (-0.24, 0.45),
            (-0.22, 0.66), (-0.08, 0.66), (-0.06, 0.62), (0.13, 0.44), (0.22, 0.88), (0.27, 0.88)]
    for x in (-0.78, 0.78):
        leg = prism(side, "X", x - 0.025, x + 0.025)
        s.bevel_edges(leg, 0.008, 1, angle=30)
        p.add("Metal", tri_ngons(leg))
    for k in range(5):
        y = -0.21 + k * 0.095
        p.add("Wood", box(-0.9, 0.9, y - 0.04, y + 0.04, 0.4, 0.445, r=0.012))
    for k in range(3):
        z = 0.52 + k * 0.13
        y = 0.135 + (z - 0.44) * 0.2
        bm = box(-0.9, 0.9, y - 0.016, y + 0.016, z - 0.045, z + 0.045, r=0.01)
        p.add("Wood", bm)
    p.add("Metal", rod((-0.78, -0.26, 0.2), (0.78, -0.26, 0.2), 0.015, seg=8))  # the stretcher
    p.finish()
    return export("bench")


def m_bin():
    """A litter bin: a ribbed steel drum on a short pedestal, a domed lid with a slot."""
    p = Parts()
    drum = s.lathe([(0.0, 0.18), (0.22, 0.18), (0.235, 0.22), (0.235, 0.86), (0.25, 0.9), (0.25, 0.93), (0.0, 0.93)], seg=24)
    for z in (0.34, 0.5, 0.66, 0.8):
        s.groove(drum, "Z", z, 0.012, 0.007)
    p.add("Metal", drum)
    p.add("Metal", s.lathe([(0.0, 0.0), (0.16, 0.0), (0.16, 0.03), (0.07, 0.06), (0.06, 0.2), (0.0, 0.2)], seg=16))
    p.add("Tyre", s.lathe([(0.0, 0.92), (0.24, 0.92), (0.24, 0.96), (0.2, 1.02), (0.1, 1.05), (0.0, 1.06)], seg=24))
    p.add("Metal", s.rbox((0.2, 0.06, 0.05), (0, -0.2, 0.985), r=0.012))  # the slot's hood
    p.finish()
    return export("bin")


def m_hydrant():
    """A red fire hydrant: a flanged foot, the barrel, the bonnet and its nut, two side outlets and the pumper outlet
    to the street (-y), their caps."""
    p = Parts()
    p.add("CarRed", s.lathe([(0.0, 0.0), (0.17, 0.0), (0.17, 0.05), (0.13, 0.07), (0.12, 0.5), (0.14, 0.52), (0.14, 0.56),
                             (0.12, 0.58), (0.09, 0.67), (0.0, 0.7)], seg=20))
    p.add("Chrome", s.rcyl(0.035, 0.07, (0, 0, 0.72), seg=5, r=0.004, bevel_seg=1))
    for sx in (-1, 1):
        p.add("CarRed", s.rcyl(0.05, 0.12, (sx * 0.16, 0, 0.4), axis="X", seg=14, r=0.008))
        p.add("Chrome", s.rcyl(0.055, 0.03, (sx * 0.225, 0, 0.4), axis="X", seg=6, r=0.004))
    p.add("CarRed", s.rcyl(0.07, 0.12, (0, -0.16, 0.36), axis="Y", seg=16, r=0.01))
    p.add("Chrome", s.rcyl(0.075, 0.04, (0, -0.235, 0.36), axis="Y", seg=6, r=0.005))
    p.finish()
    return export("hydrant")


def m_bollard():
    """A cast-iron bollard 0.95 m: a base ring, a fluted waist, two bands and a domed cap."""
    p = Parts()
    p.add("Metal", s.lathe([(0.0, 0.0), (0.12, 0.0), (0.12, 0.05), (0.09, 0.09), (0.08, 0.3), (0.095, 0.32), (0.095, 0.36),
                            (0.075, 0.38), (0.07, 0.76), (0.09, 0.78), (0.09, 0.82), (0.07, 0.86), (0.05, 0.93),
                            (0.0, 0.95)], seg=16))
    p.finish()
    return export("bollard")


def m_traffic_light():
    """Traffic lights 3.4 m: a pole on a base, the signal head facing the street (-y) — a housing with three lenses
    under visors (red lit) and a back plate, a push-button box for walkers on the pole."""
    p = Parts()
    p.add("Metal", s.lathe([(0.0, 0.0), (0.14, 0.0), (0.14, 0.04), (0.1, 0.08), (0.065, 0.3), (0.055, 3.4), (0.0, 3.42)], seg=14))
    p.add("Tyre", s.rbox((0.34, 0.24, 0.92), (0, -0.16, 2.78), r=0.04, seg=2))
    p.add("Tyre", s.rbox((0.5, 0.03, 1.06), (0, -0.03, 2.78), r=0.02))       # the back plate
    p.add("Metal", s.rbox((0.08, 0.2, 0.08), (0, -0.04, 3.1), r=0.01), s.rbox((0.08, 0.2, 0.08), (0, -0.04, 2.45), r=0.01))
    for k, z in enumerate((3.06, 2.78, 2.5)):
        p.add("TailLight" if k == 0 else "Glass", s.rcyl(0.1, 0.03, (0, -0.28, z), axis="Y", seg=18, r=0.008))
        visor = revolve([(0.11, -0.12), (0.125, -0.12), (0.125, 0.0), (0.11, 0.0)], seg=16, a0=0.0, a1=math.pi)
        s.place(visor, rot=(math.pi / 2, 0, 0))
        s.place(visor, (0, -0.29, z))
        p.add("Tyre", visor)
    p.add("Metal", s.rbox((0.12, 0.08, 0.2), (0, -0.09, 1.15), r=0.02))
    p.add("Chrome", s.rcyl(0.03, 0.02, (0, -0.135, 1.17), axis="Y", seg=12, r=0.004))
    p.finish()
    return export("traffic_light")


def m_bus_stop():
    """A bus shelter 3.6 x 1.4 m, open to the street (-y): four posts, a curved roof, glass back and sides, a lit
    advertising panel at one end, a perch bench."""
    p = Parts()
    w, d, h = 3.6, 1.4, 2.5
    for x in (-w / 2 + 0.05, w / 2 - 0.05):
        for y in (-d / 2 + 0.05, d / 2 - 0.05):
            p.add("Metal", s.rbox((0.08, 0.08, h), (x, y, h / 2), r=0.02, vertical_only=True))
    roof = ext_x([(-d / 2 - 0.15, h), (-d / 2 - 0.15, h + 0.06), (0.0, h + 0.14), (d / 2 + 0.1, h + 0.08), (d / 2 + 0.1, h),
                  (0.0, h + 0.07)], -w / 2 - 0.1, w / 2 + 0.1, 0.01)
    p.add("Metal", roof)
    p.add("Glass", box(-w / 2 + 0.1, w / 2 - 0.1, d / 2 - 0.07, d / 2 - 0.03, 0.15, h - 0.05, r=0.004))
    p.add("Glass", box(-w / 2 + 0.03, -w / 2 + 0.07, -d / 2 + 0.6, d / 2 - 0.1, 0.15, h - 0.05, r=0.004))
    # the ad panel: a lit box across the far end
    p.add("Metal", box(w / 2 - 0.13, w / 2 + 0.03, -d / 2 + 0.15, d / 2 - 0.1, 0.1, h - 0.1, r=0.015))
    p.add("LampGlow", box(w / 2 - 0.145, w / 2 - 0.12, -d / 2 + 0.22, d / 2 - 0.17, 0.25, h - 0.25, r=0.004))
    p.add("Metal", box(-w / 2 + 0.2, w / 2 - 0.25, 0.25, 0.6, 0.6, 0.65, r=0.015))   # the perch
    for x in (-1.0, 0.9):
        p.add("Metal", box(x - 0.03, x + 0.03, 0.4, 0.66, 0.0, 0.61, r=0.008))
    p.finish()
    return export("bus_stop")


def m_manhole():
    """A cast-iron manhole cover flush with the asphalt: a rim, concentric ribs."""
    p = Parts()
    p.add("Metal", s.lathe([(0.0, 0.0), (0.36, 0.0), (0.36, 0.012), (0.33, 0.016), (0.0, 0.016)], seg=28))
    for r in (0.12, 0.2, 0.28):
        p.add("Tyre", revolve([(r, 0.012), (r + 0.02, 0.012), (r + 0.02, 0.022), (r, 0.022)], seg=28))
    p.finish()
    return export("manhole", floor=False)


def m_planter():
    """A concrete planter 1.2 x 0.6 with a trimmed shrub in it."""
    p = Parts()
    p.add("Concrete", s.rbox((1.2, 0.6, 0.5), (0, 0, 0.25), r=0.03, seg=2, taper=(1.06, 1.1)))
    p.add("Soil", box(-0.56, 0.56, -0.26, 0.26, 0.4, 0.505, r=0.0))
    balls = []
    for k, x in enumerate((-0.36, -0.12, 0.12, 0.36)):        # a trimmed shrub: a row of lumps melted into one hedge
        r = 0.26 + 0.04 * (k % 2)
        balls.append(((x, 0.02 * (k % 3 - 1), 0.62 + 0.04 * (k % 2)), r))   # sunk into the soil: it grows out of it
        balls.append(((x * 0.9, -0.12 + 0.24 * (k % 2), 0.78), r * 0.6))
    p.add("Leaves", foliage(balls, 0.04, 5, 0.01, 1400))
    p.finish(sharp=60)
    return export("planter")


# =========================================================================== parked cars

def taillights(p, yr, z=0.75, x=0.62):
    """Red lenses over the sedan's dark tail lamp blocks (not lit: the car is parked)."""
    for sx in (-1, 1):
        p.add("CarRed", s.rbox((0.36, 0.1, 0.12), (sx * x, yr - 0.05, z), r=0.02))


def hubcaps(p, wheels, x):
    for wy in wheels:
        for sx in (-1, 1):
            p.add("Chrome", s.rcyl(0.17, 0.03, (sx * (x + 0.055), wy, garage.WHEEL_R), axis="X", seg=16, r=0.01))


def m_sedan():
    """The repair shop's sedan (garage.sedan) parked on the street: its paint is the copy's livery."""
    p = CarParts()
    yf, yr = garage.sedan(p)
    for sx in (-1, 1):                                      # red lenses over the wrapped tail lamps
        p.add("CarRed", s.rbox((0.34, 0.09, 0.1), (sx * 0.49, yr + 0.005, 0.665), r=0.022, seg=2, rot=(0, 0, sx * -0.2)))
    hubcaps(p, (-garage.WHEELBASE / 2, garage.WHEELBASE / 2), garage.TRACK)
    p.finish()
    return export("sedan")


def hatch_body(p, length, roof, cab0, cab1, high=False):
    """A hatchback / small van body after garage.sedan: a side profile extruded across, wheel arches cut, a glass cabin
    to the roof, bumpers, lamps, mirrors, wheels."""
    W = 1.74
    yf, yr = -length / 2, length / 2
    base = garage.SILL
    wb = length * 0.62
    lower = [(yf + 0.08, base + 0.03), (yf - 0.02, 0.45), (yf + 0.03, 0.7), (yf + 0.22, 0.82), (cab0, 0.88),
             (yr - 0.08, 0.92), (yr, 0.72), (yr + 0.01, 0.42), (yr - 0.12, base + 0.02), (wb / 2 + 0.42, base),
             (-wb / 2 - 0.42, base)]
    body = prism(lower, "X", -W / 2, W / 2)
    s.bevel_edges(body, 0.05, 2, angle=30)
    for wy in (-wb / 2, wb / 2):
        body = s.boolean(body, s.rcyl(garage.WHEEL_R + 0.06, W + 0.2, (0, wy, garage.WHEEL_R), axis="X", seg=24, r=0), "DIFFERENCE")
    for gy in (cab0 + 0.05, (cab0 + cab1) / 2 + 0.1):
        s.groove(body, "Y", gy, 0.008, 0.004)
    cabin = [(cab0, 0.87), (cab0 + (0.45 if not high else 0.25), roof), (cab1 - 0.06, roof), (cab1, roof - 0.05), (yr - 0.04, 0.9)]
    glass = prism(cabin, "X", -0.72, 0.72)
    s.bevel_edges(glass, 0.03, 2, angle=30)
    for v in glass.verts:
        if v.co.z > 0.95:
            v.co.x *= 1.0 - 0.1 * (v.co.z - 0.95) / max(0.1, roof - 0.95)
    roof_panel = s.rbox((1.3, cab1 - cab0 - 0.55, 0.05), (0, (cab0 + cab1) / 2 + 0.2, roof - 0.005), r=0.02)
    pillars = []
    for sx in (-1, 1):
        pillars.append(rod((sx * 0.72, cab0 + 0.02, 0.88), (sx * 0.66, cab0 + 0.45, roof - 0.02), 0.03, seg=8))
        pillars.append(rod((sx * 0.72, (cab0 + cab1) / 2 + 0.1, 0.9), (sx * 0.67, (cab0 + cab1) / 2 + 0.1, roof - 0.01), 0.035, seg=8))
        pillars.append(rod((sx * 0.72, yr - 0.1, 0.92), (sx * 0.67, cab1 - 0.08, roof - 0.03), 0.05, seg=8))
    p.add("Body", [body, roof_panel] + pillars)
    p.add("Trim", glass)
    p.add("Accent", s.rbox((1.62, 0.12, 0.16), (0, yf + 0.02, 0.4), r=0.05), s.rbox((1.62, 0.12, 0.16), (0, yr - 0.02, 0.42), r=0.05),
          s.rbox((0.8, 0.04, 0.12), (0, yf + 0.05, 0.6), r=0.02))
    for sx in (-1, 1):
        p.add("Accent", s.rbox((0.22, 0.09, 0.09), (sx * 0.8, cab0 + 0.14, 0.93), r=0.025))
        p.add("Eye", s.rbox((0.3, 0.06, 0.12), (sx * 0.58, yf + 0.15, 0.7), r=0.03))
        p.add("Accent", s.rbox((0.12, 0.05, 0.3), (sx * 0.72, yr - 0.03, 0.72), r=0.02))
        p.add("CarRed", s.rbox((0.14, 0.05, 0.32), (sx * 0.72, yr - 0.05, 0.72), r=0.02))
    for wy in (-wb / 2, wb / 2):
        for sx in (-1, 1):
            garage.wheel(p, (sx * 0.77, wy, garage.WHEEL_R))
    hubcaps(p, (-wb / 2, wb / 2), 0.77)


def m_hatchback():
    """A compact hatchback 3.9 m: a short bonnet, the roof running back to an upright tailgate."""
    p = CarParts()
    hatch_body(p, 3.9, 1.48, -0.85, 1.6)
    p.finish()
    return export("hatchback")


def m_van():
    """A small delivery van 4.7 m: a short nose, a tall roof the length of the body, glass only in the cab."""
    p = CarParts()
    hatch_body(p, 4.7, 1.95, -1.25, 2.2, high=True)
    # the cargo box hides the rear glass: panels over the cabin's back two thirds
    p.add("Body", s.rbox((1.62, 2.7, 1.1), (0, 0.95, 1.44), r=0.05, seg=2))
    p.finish()
    return export("van")


# =========================================================================== the facade kit

def sill(w, depth=0.24, h=0.14, corbels=True):
    out = [box(-w / 2, w / 2, 0.03, -depth, -h, 0.0, r=0.02, seg=1)]
    s.groove(out[0], "Y", -depth + 0.04, 0.008, 0.006)  # the drip under the nose
    if corbels:
        for x in (-w / 2 + 0.1, w / 2 - 0.1):
            out.append(box(x - 0.06, x + 0.06, 0.03, -depth + 0.07, -h - 0.17, -h + 0.01, r=0.015))
    return out


def casement(p, w, h, transom=None, mullions=(0.0,), rails=()):
    """The window itself in the reveal: a white frame, glazing bars, the glass a little behind them."""
    p.add("Frame", frame_ring(w - 0.006, h - 0.006, 0.07, 0.0, -0.05, 0.003))
    bars = []
    if transom is not None:
        bars.append(box(-w / 2 + 0.06, w / 2 - 0.06, 0.0, -0.042, transom - 0.03, transom + 0.03, r=0.0))
    top = transom if transom is not None else h
    for x in mullions:
        bars.append(box(x - 0.028, x + 0.028, 0.0, -0.042, 0.06, top - 0.01, r=0.0))
    for z in rails:
        bars.append(box(-w / 2 + 0.06, w / 2 - 0.06, 0.0, -0.036, z - 0.018, z + 0.018, r=0.0))
    p.add("Frame", bars)
    p.add("Pane", box(-w / 2 + 0.02, w / 2 - 0.02, -0.012, -0.022, 0.02, h - 0.02, r=0.0))


def m_window_a():
    """A window of the plain stucco front (facade_a): an architrave round the sides and the head standing 10 cm off the
    wall (the glass sits 8 cm back in it), a keystone, a moulded sill on two corbels, a casement with a fanlight."""
    W, H = (WIN_A[2] - WIN_A[0]) * ST, (WIN_A[3] - WIN_A[1]) * ST
    p = Parts()
    j, t = 0.16, 0.22
    outline = [(-W / 2 - j, -0.02), (-W / 2, -0.02), (-W / 2, H), (W / 2, H), (W / 2, -0.02), (W / 2 + j, -0.02),
               (W / 2 + j, H + t), (-W / 2 - j, H + t)]
    p.add("Livery", plate(outline, 0.03, -0.1, 0.012, 1))
    p.add("Livery", plate([(-0.12, H - 0.04), (0.12, H - 0.04), (0.17, H + t + 0.06), (-0.17, H + t + 0.06)], 0.02, -0.14, 0.01, 1))
    p.add("Livery", box(-W / 2 - j - 0.05, W / 2 + j + 0.05, 0.03, -0.14, H + t, H + t + 0.06, r=0.012))  # the head's cap
    p.add("Livery", sill(W + 2 * j + 0.16))
    casement(p, W, H, transom=0.72 * H)
    p.finish()
    return export("window_a", floor=False)


def m_window_b():
    """A window of the brick front (facade_b): stone jambs, a segmental stone head with a keystone, a stone sill, a
    six-pane sash."""
    W, H = (WIN_B[2] - WIN_B[0]) * ST, (WIN_B[3] - WIN_B[1]) * ST
    p = Parts()
    j = 0.19
    for sx in (-1, 1):
        x0, x1 = sorted((sx * W / 2, sx * (W / 2 + j)))
        p.add("Livery", box(x0, x1, 0.03, -0.06, -0.02, H + 0.02, r=0.01))
    arc = []
    for k in range(9):
        x = -W / 2 - j - 0.06 + (W + 2 * j + 0.12) * k / 8
        u = x / (W / 2 + j + 0.06)
        arc.append((x, H + 0.2 + 0.16 * (1 - u * u)))
    p.add("Livery", plate([(-W / 2 - j - 0.06, H), (W / 2 + j + 0.06, H)] + arc[::-1], 0.03, -0.08, 0.01, 1))
    p.add("Livery", plate([(-0.11, H - 0.03), (0.11, H - 0.03), (0.15, H + 0.44), (-0.15, H + 0.44)], 0.02, -0.12, 0.01, 1))
    p.add("Livery", sill(W + 2 * j + 0.1, depth=0.2, h=0.16, corbels=False))
    casement(p, W, H, mullions=(-W / 6, W / 6), rails=(H * 0.5,))
    p.finish()
    return export("window_b", floor=False)


def m_curtain_bay():
    """A bay of the glass curtain wall (facade_c), ST x ST: half a mullion at each edge (neighbours complete them), a
    middle mullion, the opaque spandrel bands at the floor lines and a pane between them."""
    x0, z0, x1, z1 = WIN_C
    p = Parts()
    hw = ST / 2
    for sx in (-1, 1):
        a, b = sorted((sx * hw, sx * (hw - 0.07)))
        p.add("Metal", box(a, b, 0.02, -0.16, 0.0, ST, r=0.0))
        p.add("Metal", box(a - sx * 0.004, b - sx * 0.004, -0.16, -0.2, 0.0, ST, r=0.0))  # the cap's nose
    p.add("Metal", box(-0.04, 0.04, 0.0, -0.12, z0 * ST, z1 * ST, r=0.008))
    p.add("Metal", box(-hw + 0.06, hw - 0.06, 0.02, -0.1, z0 * ST - 0.05, z0 * ST + 0.03, r=0.008))   # transoms
    p.add("Metal", box(-hw + 0.06, hw - 0.06, 0.02, -0.1, z1 * ST - 0.03, z1 * ST + 0.05, r=0.008))
    p.add("Tyre", box(-hw + 0.06, hw - 0.06, 0.02, -0.035, 0.0, z0 * ST - 0.04, r=0.0))              # spandrels
    p.add("Tyre", box(-hw + 0.06, hw - 0.06, 0.02, -0.035, z1 * ST + 0.04, ST, r=0.0))
    p.add("Pane", box(-hw + 0.06, hw - 0.06, -0.02, -0.03, z0 * ST, z1 * ST, r=0.0))
    p.finish()
    return export("curtain_bay", floor=False)


def shop_shell(p):
    """What a shop bay and an entrance bay share: half pilasters at the edges, the fascia (livery) under a little
    cornice, a stone stall plate at the foot."""
    hw = ST / 2
    for sx in (-1, 1):
        a, b = sorted((sx * hw, sx * (hw - 0.22)))
        p.add("Stone", box(a, b, 0.03, -0.12, 0.0, 3.78, r=0.0))
        p.add("Stone", box(a, b, 0.03, -0.16, 0.0, 0.3, r=0.0))               # its base
    p.add("Livery", box(-hw + 0.21, hw - 0.21, 0.03, -0.1, 3.02, 3.58, r=0.02, seg=2))
    p.add("Stone", ext_x([(0.03, 3.56), (-0.12, 3.56), (-0.17, 3.62), (-0.2, 3.7), (-0.2, 3.76), (0.03, 3.76)], -hw, hw, 0.008))


def m_shopfront():
    """A shop's bay of the ground floor, ST wide: a stall riser, a big glazed window in an aluminium frame with a
    transom bar, the fascia over it for the sign."""
    p = Parts()
    hw = ST / 2
    shop_shell(p)
    p.add("Concrete", box(-hw + 0.2, hw - 0.2, 0.03, -0.07, 0.0, 0.55, r=0.015))
    w = ST - 0.44
    p.add("Metal", frame_ring(w, 2.47, 0.07, 0.0, -0.07, 0.55))
    p.add("Metal", box(-w / 2 + 0.06, w / 2 - 0.06, 0.0, -0.06, 2.42, 2.48, r=0.005))
    p.add("Metal", box(-0.03, 0.03, 0.0, -0.06, 0.6, 2.43, r=0.005))
    p.add("Pane", box(-w / 2 + 0.03, w / 2 - 0.03, -0.025, -0.035, 0.58, 2.99, r=0.0))
    p.finish()
    return export("shopfront", floor=False)


def m_entrance():
    """The building's way in, ST wide: a pair of glazed doors with side lights and a fanlight, a step, a flat steel
    canopy on two hangers."""
    p = Parts()
    hw = ST / 2
    shop_shell(p)
    w = ST - 0.44
    p.add("Metal", frame_ring(w, 2.98, 0.08, 0.0, -0.08, 0.0))
    for x in (-0.95, 0.95):
        p.add("Metal", box(x - 0.04, x + 0.04, 0.0, -0.08, 0.05, 2.95, r=0.005))
    p.add("Metal", box(-w / 2 + 0.06, w / 2 - 0.06, 0.0, -0.075, 2.38, 2.46, r=0.005))
    for x in (-0.48, 0.48):  # the two door leaves' frames
        p.add("Metal", [s.place(b, (x, 0, 0)) for b in frame_ring(0.9, 2.36, 0.07, -0.03, -0.1, 0.02)])
        p.add("Chrome", rod((x - math.copysign(0.34, x), -0.13, 0.85), (x - math.copysign(0.34, x), -0.13, 1.45), 0.015, seg=8))
    p.add("Metal", box(-0.005, 0.005, -0.03, -0.1, 0.02, 2.38, r=0.0))
    p.add("Pane", box(-w / 2 + 0.03, w / 2 - 0.03, -0.04, -0.05, 0.03, 2.97, r=0.0))
    p.add("Concrete", box(-1.5, 1.5, 0.03, -0.5, -0.04, 0.13, r=0.02, seg=2))
    p.add("Metal", box(-1.6, 1.6, 0.03, -1.25, 3.0, 3.1, r=0.02, seg=2))
    p.add("Metal", box(-1.6, 1.6, -1.18, -1.25, 2.92, 3.1, r=0.01))
    for x in (-1.4, 1.4):
        p.add("Metal", rod((x, 0.0, 3.85), (x, -1.12, 3.08), 0.018, seg=8))
    p.finish()
    return export("entrance", floor=False)


def m_cornice():
    """The main cornice under the parapet, 0.5 m tall and 0.42 out: a fascia, a bed moulding, the corona, a cyma."""
    p = Parts()
    p.add("Livery", ext_x([(0.03, 0.0), (-0.05, 0.0), (-0.05, 0.1), (-0.1, 0.12), (-0.13, 0.17), (-0.3, 0.24), (-0.38, 0.25),
                           (-0.4, 0.28), (-0.4, 0.37), (-0.42, 0.39), (-0.42, 0.43), (-0.36, 0.46), (-0.3, 0.5),
                           (0.03, 0.5)], bevel=0.008))
    p.finish(sharp=40)
    return export("cornice", floor=False)


def m_band():
    """A string course between storeys: 0.22 tall, 0.14 out, chamfered top to shed the rain."""
    p = Parts()
    p.add("Livery", ext_x([(0.03, 0.0), (-0.11, 0.0), (-0.14, 0.03), (-0.14, 0.15), (-0.1, 0.22), (0.03, 0.22)], bevel=0.008))
    p.finish()
    return export("band", floor=False)


def m_coping():
    """The parapet's coping: a cap over the wall top (its top at z = 0 is the parapet's), overhanging the street side
    with a drip, sloping back to the roof."""
    p = Parts()
    p.add("Livery", ext_x([(-0.07, -0.05), (0.0, -0.05), (0.0, -0.02), (0.35, -0.02), (0.35, 0.05), (0.12, 0.09),
                           (-0.07, 0.065)], bevel=0.008))
    p.finish()
    return export("coping", floor=False)


def m_kerb():
    """A granite kerb stone along a pavement's edge: y = 0 is the edge, the road at -y; its top 8 mm over the paving,
    a rounded nose, its face 1 cm proud of the slab's side."""
    p = Parts()
    p.add("Concrete", ext_x([(-0.01, -0.2), (0.18, -0.2), (0.18, 0.008), (0.035, 0.008), (0.005, -0.004), (-0.01, -0.03)],
                            bevel=0.006))
    p.finish()
    return export("kerb", floor=False)


def m_pilaster():
    """A flat pilaster between the bays of the plain front, z 0..1 (stretched up the wall)."""
    p = Parts()
    p.add("Livery", ext_z([(-0.18, 0.03), (0.18, 0.03), (0.18, -0.06), (0.15, -0.1), (-0.15, -0.1), (-0.18, -0.06)], bevel=0.01))
    p.finish()
    return export("pilaster", floor=False)


def m_quoin():
    """A corner stone of the brick front, 0.5 x 0.42, standing 5 cm off the wall (short ones: scaled to 0.32)."""
    p = Parts()
    p.add("Livery", box(-0.25, 0.25, 0.03, -0.05, 0.0, 0.42, r=0.018, seg=1))
    p.finish()
    return export("quoin", floor=False)


def m_drainpipe():
    """A metre of drainpipe 11 cm off the wall (stretched up)."""
    p = Parts()
    p.add("Metal", s.rcyl(0.05, 1.0, (0, -0.11, 0.5), seg=12, r=0.0))
    p.finish()
    return export("drainpipe", floor=False)


def m_drain_head():
    """The hopper under the gutter at the drainpipe's top: a box widening upward, its outlet down into the pipe."""
    p = Parts()
    p.add("Metal", s.loft([(0.0, 0.06, 0.06, 2), (0.1, 0.07, 0.07, 2), (0.16, 0.13, 0.1, 3), (0.34, 0.16, 0.11, 3.5),
                           (0.36, 0.17, 0.12, 3.5)], seg=20, loc=(0, -0.11, 0.0)))
    p.add("Metal", box(-0.2, 0.2, 0.02, -0.02, 0.06, 0.34, r=0.008))
    p.finish()
    return export("drain_head", floor=False)


def m_pipe_clip():
    """A bracket holding the drainpipe to the wall."""
    p = Parts()
    p.add("Metal", revolve([(0.052, -0.025), (0.064, -0.025), (0.064, 0.025), (0.052, 0.025)], seg=12, loc=(0, -0.11, 0)))
    p.add("Metal", box(-0.015, 0.015, 0.02, -0.06, -0.02, 0.02, r=0.004))
    p.finish()
    return export("pipe_clip", floor=False)


def m_fire_landing():
    """A fire escape's landing in front of a window, 3 x 1.1 m: a ribbed deck, a railing, two struts to the wall."""
    p = Parts()
    deck = box(-1.5, 1.5, 0.02, -1.1, 0.0, 0.05, r=0.008)
    for x in (-1.2, -0.9, -0.6, -0.3, 0.0, 0.3, 0.6, 0.9, 1.2):
        s.groove(deck, "X", x, 0.01, 0.008)
    p.add("Metal", deck)
    for x in (-1.47, -0.5, 0.5, 1.47):
        p.add("Metal", box(x - 0.02, x + 0.02, -1.05, -1.09, 0.04, 1.02, r=0.005))
    for y in (-0.05, -0.55):
        for x in (-1.47, 1.47):
            p.add("Metal", box(x - 0.02, x + 0.02, y + 0.02, y - 0.02, 0.04, 1.02, r=0.005))
    p.add("Metal", rod((-1.49, -1.07, 1.0), (1.49, -1.07, 1.0), 0.022, seg=8), rod((-1.49, -1.07, 0.52), (1.49, -1.07, 0.52), 0.012, seg=6))
    for x in (-1.47, 1.47):
        p.add("Metal", rod((x, 0.01, 1.0), (x, -1.07, 1.0), 0.022, seg=8), rod((x, 0.01, 0.52), (x, -1.07, 0.52), 0.012, seg=6))
        p.add("Metal", rod((x * 0.9, 0.01, -0.75), (x * 0.9, -1.0, 0.02), 0.025, seg=6))
    p.add("Metal", box(-1.5, 1.5, -1.07, -1.1, 0.04, 0.16, r=0.004))  # the toe board
    p.finish()
    return export("fire_landing", floor=False)


def m_fire_ladder():
    """The fire escape's ladder from a landing up to the next one (a storey, ST)."""
    p = Parts()
    for x in (0.78, 1.22):
        p.add("Metal", box(x - 0.02, x + 0.02, -0.3, -0.36, 0.03, ST + 0.05, r=0.005))
    for k in range(int(ST / 0.3)):
        z = 0.3 + k * 0.3
        p.add("Metal", rod((0.77, -0.33, z), (1.23, -0.33, z), 0.012, seg=6))
    p.finish()
    return export("fire_ladder", floor=False)


def m_balcony():
    """A balcony of the plain front, 3 x 1 m: a concrete slab with a drip, a railing of square bars."""
    p = Parts()
    slab = box(-1.5, 1.5, 0.03, -1.0, 0.0, 0.16, r=0.02, seg=2)
    s.groove(slab, "Z", 0.04, 0.008, 0.006)
    p.add("Concrete", slab)
    p.add("Metal", box(-1.49, 1.49, -0.92, -0.98, 1.0, 1.05, r=0.012))
    for x in (-1.46, 1.46):
        p.add("Metal", box(x - 0.025, x + 0.025, 0.02, -0.98, 1.0, 1.05, r=0.012))
    bars = []
    k = 0
    while -1.42 + k * 0.12 <= 1.43:
        x = -1.42 + k * 0.12
        bars.append(box(x - 0.012, x + 0.012, -0.94, -0.964, 0.14, 1.01, r=0.0))
        k += 1
    for y in (-0.2, -0.45, -0.7):
        for x in (-1.46, 1.46):
            bars.append(box(x - 0.012, x + 0.012, y - 0.012, y + 0.012, 0.14, 1.01, r=0.0))
    p.add("Metal", bars)
    p.finish()
    return export("balcony", floor=False)


def m_juliet():
    """A French balcony: a railing across the bottom of a brick front's window, on two brackets."""
    W = (WIN_B[2] - WIN_B[0]) * ST + 0.3
    p = Parts()
    p.add("Metal", box(-W / 2, W / 2, -0.2, -0.25, 0.86, 0.9, r=0.01), box(-W / 2, W / 2, -0.2, -0.25, 0.0, 0.04, r=0.008))
    k = 0
    bars = []
    while -W / 2 + 0.06 + k * 0.11 < W / 2 - 0.04:
        x = -W / 2 + 0.06 + k * 0.11
        bars.append(rod((x, -0.225, 0.02), (x, -0.225, 0.88), 0.008, seg=6))
        k += 1
    p.add("Metal", bars)
    for x in (-W / 2 + 0.05, W / 2 - 0.05):
        p.add("Metal", box(x - 0.015, x + 0.015, 0.02, -0.24, 0.0, 0.04, r=0.005), box(x - 0.015, x + 0.015, 0.02, -0.24, 0.84, 0.88, r=0.005))
    p.finish()
    return export("juliet", floor=False)


def m_wall_ac():
    """An air conditioner hung outside a window: a white box with a round fan grille, on two brackets, its pipes
    running into the wall."""
    p = Parts()
    p.add("Frame", box(-0.4, 0.4, -0.04, -0.36, 0.0, 0.55, r=0.025, seg=1))
    p.add("Tyre", s.rcyl(0.19, 0.02, (-0.1, -0.36, 0.28), axis="Y", seg=14, r=0.004, bevel_seg=1))
    for r in (0.1, 0.19):
        ring = revolve([(r - 0.008, -0.01), (r, -0.01), (r, 0.01), (r - 0.008, 0.01)], seg=14)
        s.place(ring, rot=(math.pi / 2, 0, 0))
        p.add("Metal", s.place(ring, (-0.1, -0.372, 0.28)))
    p.add("Metal", s.rbox((0.03, 0.02, 0.4), (-0.1, -0.375, 0.28), r=0.004), s.rbox((0.4, 0.02, 0.03), (-0.1, -0.375, 0.28), r=0.004))
    p.add("Metal", s.grille((0.24, -0.36, 0.3), 0.18, 0.36, n=4, axis="-Y"))
    for x in (-0.3, 0.3):
        p.add("Metal", box(x - 0.02, x + 0.02, 0.02, -0.38, -0.05, 0.0, r=0.004))
        p.add("Metal", rod((x, 0.01, -0.35), (x, -0.36, -0.03), 0.012, seg=6))
    p.add("Chrome", s.tube([(0.35, -0.2, 0.1), (0.45, -0.15, 0.05), (0.45, 0.02, 0.05)], 0.012, segments_per=4))
    p.finish()
    return export("wall_ac", floor=False)


def m_dish():
    """A satellite dish on a wall bracket: the dish turned up to the sky and to the street, its feed arm and LNB."""
    p = Parts()
    pts = [(0.005 + 0.3 * k / 6, 0.6 * (0.3 * k / 6) ** 2) for k in range(7)]
    outline = pts + [(r + 0.0, z + 0.015) for r, z in reversed(pts)]
    dish = revolve(outline, seg=24)
    s.place(dish, rot=(math.radians(-70), 0, 0))
    s.place(dish, (0, -0.42, 0.05))
    p.add("Frame", dish)
    p.add("Metal", box(-0.06, 0.06, 0.02, -0.02, -0.12, 0.12, r=0.006), rod((0, 0.0, 0.0), (0, -0.4, 0.04), 0.02, seg=8))
    p.add("Metal", rod((0, -0.44, -0.08), (0, -0.66, 0.16), 0.01, seg=6))
    p.add("Frame", s.rbox((0.05, 0.08, 0.05), (0, -0.67, 0.17), r=0.01))
    p.finish()
    return export("dish", floor=False)


def m_awning():
    """A shop awning, a metre along x (stretched to the shop): the cloth sloping down from the wall to a valance, a
    steel bar along its front edge."""
    p = Parts()
    p.add("Livery", ext_x([(0.03, 0.62), (-1.2, 0.16), (-1.2, -0.12), (-1.17, -0.12), (-1.17, 0.12), (0.03, 0.57)], bevel=0.0))
    p.add("Metal", ext_x([(-1.2, 0.13), (-1.15, 0.13), (-1.15, 0.18), (-1.2, 0.18)], bevel=0.0))
    p.finish()
    return export("awning", floor=False)


def m_sign():
    """A lit shop sign on the fascia: a shallow box with a glowing face."""
    p = Parts()
    p.add("Metal", box(-1.4, 1.4, 0.0, -0.08, 0.0, 0.42, r=0.015, seg=2))
    p.add("LampGlow", box(-1.34, 1.34, -0.07, -0.09, 0.05, 0.37, r=0.004))
    p.finish()
    return export("sign", floor=False)


# --------------------------------------------------------------------------- on the roofs

def m_roof_ac():
    """A rooftop condenser 1.3 x 0.9 x 0.9: a cabinet with louvred sides, two fans under grilles on top, on rails."""
    p = Parts()
    cab = box(-0.65, 0.65, -0.45, 0.45, 0.12, 0.98, r=0.025, seg=1)
    for z in (0.35, 0.55, 0.75):
        s.groove(cab, "Z", z, 0.01, 0.006)
    p.add("Frame", cab)
    for x in (-0.32, 0.32):
        p.add("Tyre", s.rcyl(0.25, 0.04, (x, 0, 0.98), seg=14, r=0.006, bevel_seg=1))
        p.add("Metal", revolve([(0.2, 0.0), (0.255, 0.0), (0.255, 0.03), (0.2, 0.03)], seg=14, loc=(x, 0, 0.98)))
        for a in (0.0, math.pi / 2):
            bar = s.rbox((0.5, 0.02, 0.02), (0, 0, 0), r=0.004)
            s.place(bar, rot=(0, 0, a))
            p.add("Metal", s.place(bar, (x, 0, 1.01)))
    for y in (-0.35, 0.35):
        p.add("Metal", box(-0.75, 0.75, y - 0.05, y + 0.05, 0.0, 0.13, r=0.008))
    p.finish()
    return export("roof_ac")


def m_roof_vent():
    """A vent stack with a mushroom cowl, 1.4 m."""
    p = Parts()
    p.add("Metal", s.lathe([(0.0, 0.0), (0.24, 0.0), (0.24, 0.05), (0.16, 0.08), (0.16, 1.15), (0.0, 1.15)], seg=18))
    p.add("Metal", s.lathe([(0.0, 1.22), (0.32, 1.22), (0.3, 1.27), (0.1, 1.4), (0.0, 1.41)], seg=18))
    for a in range(3):
        ang = a * 2 * math.pi / 3
        p.add("Metal", rod((math.cos(ang) * 0.14, math.sin(ang) * 0.14, 1.12), (math.cos(ang) * 0.26, math.sin(ang) * 0.26, 1.24), 0.01, seg=6))
    p.finish()
    return export("roof_vent")


def m_roof_mast():
    """An antenna mast 4 m on a footing with guy cables, crossed arms near the top."""
    p = Parts()
    p.add("Concrete", s.rbox((0.5, 0.5, 0.2), (0, 0, 0.1), r=0.02))
    p.add("Metal", s.rcyl(0.04, 4.0, (0, 0, 2.1), seg=10, r=0.005))
    for z, a in ((3.4, 0.0), (3.0, math.pi / 2)):
        arm = s.rbox((1.2, 0.03, 0.03), (0, 0, 0), r=0.006)
        s.place(arm, rot=(0, 0, a))
        p.add("Metal", s.place(arm, (0, 0, z)))
    for k in range(3):
        a = k * 2 * math.pi / 3 + 0.4
        p.add("Metal", rod((0, 0, 2.6), (math.cos(a) * 1.6, math.sin(a) * 1.6, 0.02), 0.006, seg=4))
        p.add("Metal", s.rbox((0.1, 0.1, 0.05), (math.cos(a) * 1.6, math.sin(a) * 1.6, 0.025), r=0.01))
    p.finish()
    return export("roof_mast")


def m_water_tank():
    """A wooden water tank on a steel stand: four legs with braces, a platform, the staved drum with hoops, a conical lid."""
    p = Parts()
    for x in (-0.95, 0.95):
        for y in (-0.95, 0.95):
            p.add("Metal", box(x - 0.07, x + 0.07, y - 0.07, y + 0.07, 0.0, 1.62, r=0.01))
    for z in (0.5,):
        for (a, b) in (((-0.95, -0.95), (0.95, -0.95)), ((-0.95, 0.95), (0.95, 0.95)), ((-0.95, -0.95), (-0.95, 0.95)), ((0.95, -0.95), (0.95, 0.95))):
            p.add("Metal", rod((a[0], a[1], z), (b[0], b[1], z + 1.0), 0.02, seg=6))
    p.add("Metal", box(-1.2, 1.2, -1.2, 1.2, 1.6, 1.7, r=0.01))
    drum = s.lathe([(0.0, 1.68), (1.12, 1.68), (1.18, 2.4), (1.12, 3.6), (0.0, 3.6)], seg=28)
    p.add("Wood", drum)
    for z in (1.95, 2.45, 2.95, 3.4):
        r = 1.12 + 0.06 * (1 - abs(z - 2.4) / 1.2) + 0.015
        p.add("Metal", revolve([(r - 0.03, z - 0.025), (r, z - 0.025), (r, z + 0.025), (r - 0.03, z + 0.025)], seg=28))
    p.add("Wood", s.lathe([(0.0, 3.58), (1.2, 3.58), (1.2, 3.64), (0.12, 4.15), (0.0, 4.17)], seg=28))
    p.add("Metal", s.rcyl(0.08, 0.12, (0, 0, 4.2), seg=10, r=0.01))
    p.finish()
    return export("water_tank")


def m_roof_hut():
    """A machine room on a roof, 3 x 3 x 2.5 (the planner scales it): rendered walls, a steel door, a vent, a slab
    roof overhanging with a drip."""
    p = Parts()
    p.add("Concrete", box(-1.5, 1.5, -1.5, 1.5, 0.0, 2.4, r=0.02, seg=2))
    p.add("Concrete", box(-1.62, 1.62, -1.62, 1.62, 2.38, 2.55, r=0.02, seg=2))
    p.add("Metal", box(-0.45, 0.45, -1.48, -1.53, 0.05, 2.05, r=0.01))
    p.add("Metal", box(-0.52, 0.52, -1.48, -1.54, 2.05, 2.12, r=0.008))
    p.add("Chrome", rod((0.34, -1.55, 1.0), (0.34, -1.55, 1.1), 0.012, seg=6))
    p.add("Metal", s.grille((0.0, 1.5, 1.6), 0.7, 0.5, n=5, axis="Y"))
    p.finish()
    return export("roof_hut")


MODELS = {
    "lamp": m_lamp, "tree_a": m_tree_a, "tree_b": m_tree_b, "tree_c": m_tree_c, "bench": m_bench, "bin": m_bin,
    "hydrant": m_hydrant, "bollard": m_bollard, "traffic_light": m_traffic_light, "bus_stop": m_bus_stop,
    "manhole": m_manhole, "planter": m_planter,
    "sedan": m_sedan, "hatchback": m_hatchback, "van": m_van,
    "window_a": m_window_a, "window_b": m_window_b, "curtain_bay": m_curtain_bay, "shopfront": m_shopfront,
    "entrance": m_entrance, "cornice": m_cornice, "band": m_band, "coping": m_coping, "kerb": m_kerb,
    "pilaster": m_pilaster, "quoin": m_quoin, "drainpipe": m_drainpipe, "drain_head": m_drain_head,
    "pipe_clip": m_pipe_clip, "fire_landing": m_fire_landing, "fire_ladder": m_fire_ladder, "balcony": m_balcony,
    "juliet": m_juliet, "wall_ac": m_wall_ac, "dish": m_dish, "awning": m_awning, "sign": m_sign,
    "roof_ac": m_roof_ac, "roof_vent": m_roof_vent, "roof_mast": m_roof_mast, "water_tank": m_water_tank,
    "roof_hut": m_roof_hut,
}


if __name__ == "__main__":
    only = None
    if "--only" in ARGS:
        only = set(ARGS[ARGS.index("--only") + 1].split(","))
    s.clear_scene()
    for name, fn in MODELS.items():
        if only is None or name in only:
            fn()
    print("DONE")
