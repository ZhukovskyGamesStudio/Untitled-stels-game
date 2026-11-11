"""The furniture of the abandoned-facility style (S11, derelict family): every cozy office piece swapped for station gear.

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_props.py -- Assets/_Game/Art/Models/StyleProps/Derelict

Writes <out>/<our model>.fbx, modelled to the size of our model (Art/Models/Props/<name>.fbx; Blender: x right, -y the
front, z up, standing on z = 0). The game hides our model and puts this one in its place (PropReplacement, derelict
family); our glowing Eye parts stay visible, so screens are framed where our model has them. Parts are named by surface
and get their material from AssetBuilder.DerelictSurface:
  Paint      chipped painted steel        Composite  yellowed scratched plastic   Rust    heavy rust
  Vinyl      cracked seat vinyl           Rubber     cables, hoses, rubber feet   Steel   bare grimy plate
  Screen     a status LED's faint glow       ScreenText the terminal's last report    ScreenStatic  CCTV snow, NO SIGNAL
  Glass      a dead monitor's dark glass
Decoration only: no colliders (our model keeps its own), deterministic.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models", "StyleProps", "Derelict"))
ONLY = argv[1:]

SURFACES = ("Paint", "Composite", "Rust", "Vinyl", "Rubber", "Steel", "Screen", "ScreenText", "ScreenStatic", "Glass")
parts = {}   # surface -> [bmesh]
rng = random.Random(11)


def clear():
    parts.clear()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)


def add(surface, bm):
    parts.setdefault(surface, []).append(bm)
    return bm


def bevel(bm, offset, segments=1, angle=30):
    edges = [e for e in bm.edges if e.is_manifold and e.calc_face_angle(0) > math.radians(angle)]
    if edges and offset > 0:
        bmesh.ops.bevel(bm, geom=edges, offset=offset, offset_type="OFFSET", segments=segments, profile=0.5,
                        affect="EDGES", clamp_overlap=True)


def box(surface, x0, x1, y0, y1, z0, z1, rx=0.0, ry=0.0, rz=0.0, panel=False, bev=0.006):
    """An axis-aligned box from corner to corner, then rotated about its centre (radians)."""
    bm = bmesh.new()
    sx, sy, sz = abs(x1 - x0), abs(y1 - y0), abs(z1 - z0)
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((sx, sy, sz)), verts=bm.verts)
    m = min(sx, sy, sz)
    if panel:
        big = [f for f in bm.faces if f.calc_area() > 0.03]
        if big:
            bmesh.ops.inset_individual(bm, faces=big, thickness=min(0.03, 0.15 * m), depth=0.0)
            bmesh.ops.inset_individual(bm, faces=big, thickness=0.008, depth=-0.008)
    bevel(bm, min(bev, 0.25 * m))
    for ang, ax in ((rx, "X"), (ry, "Y"), (rz, "Z")):
        if ang:
            bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(ang, 3, ax), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)), verts=bm.verts)
    return add(surface, bm)


def cyl(surface, x, y, z0, z1, r, seg=20, r2=None, axis="Z"):
    """A cylinder (axis Z from z0 to z1; for X/Y the 'z' range runs along that axis and x/y are the other two)."""
    bm = bmesh.new()
    h = abs(z1 - z0)
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
    bevel(bm, min(0.004, 0.2 * min(r, h)))
    if axis == "X":
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=bm.verts)
        c = Vector(((z0 + z1) / 2, x, y))
    elif axis == "Y":
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(math.pi / 2, 3, "X"), verts=bm.verts)
        c = Vector((x, (z0 + z1) / 2, y))
    else:
        c = Vector((x, y, (z0 + z1) / 2))
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    return add(surface, bm)


def rod(surface, a, b, r, seg=10):
    """A round bar from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=d.length)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.rotate(bm, cent=Vector(), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    return add(surface, bm)


def cable(a, b, sag=0.15, r=0.008, n=8):
    """A hanging cable between two points (Rubber)."""
    a, b = Vector(a), Vector(b)
    pts = [a.lerp(b, i / n) - Vector((0, 0, sag * math.sin(math.pi * i / n))) for i in range(n + 1)]
    for p, q in zip(pts, pts[1:]):
        rod("Rubber", p, q, r, seg=6)


def sphere(surface, x, y, z, r, seg=16, sz=1.0):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
    if sz != 1.0:
        bmesh.ops.scale(bm, vec=Vector((1, 1, sz)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((x, y, z)), verts=bm.verts)
    return add(surface, bm)


def export(name):
    if ONLY and name not in ONLY:
        clear()
        return
    objs = []
    for surface, bms in parts.items():
        merged = bmesh.new()
        for bm in bms:
            me = bpy.data.meshes.new("tmp")
            bm.to_mesh(me)
            merged.from_mesh(me)
            bpy.data.meshes.remove(me)
            bm.free()
        # dents: low-frequency noise pushes surfaces in a little
        from mathutils import noise as mnoise
        merged.normal_update()
        if not surface.startswith("Screen") and surface != "Glass":
            for v in merged.verts:
                v.co -= v.normal * max(0.0, mnoise.noise(v.co * 5.0)) * 0.006
        me = bpy.data.meshes.new(surface)
        merged.to_mesh(me)
        merged.free()
        obj = bpy.data.objects.new(surface, me)
        bpy.context.collection.objects.link(obj)
        objs.append(obj)
    for obj in objs:
        bpy.context.view_layer.objects.active = obj
        for o in bpy.context.scene.objects:
            o.select_set(o == obj)
        if obj.name.startswith("Screen") or obj.name.startswith("Glass"):
            continue                                       # screens keep their 0..1 UVs (the picture), stay flat-shaded per face
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=False, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
        for p in obj.data.polygons:
            p.use_smooth = True
        try:
            bpy.ops.object.shade_smooth_by_angle(angle=math.radians(35))
        except Exception:  # noqa: BLE001
            pass
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"{name}.fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH"},
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False)
    print("prop", name)
    clear()


# --------------------------------------------------------------------------- shared pieces

def legs4(x, y, z_top, r=0.025, surface="Paint", foot=True):
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(surface, sx * x, sy * y, 0.0, z_top, r, seg=12)
            if foot:
                cyl("Rubber", sx * x, sy * y, 0.0, 0.02, r * 1.6, seg=12)



def tapered_box(surface, x0, x1, y0, y1, z0, z1, back=0.7, bev=0.008, back_side=1):
    """A box whose back (the +Y side, or -Y with back_side=-1) is shrunk to `back` of its size: a CRT housing."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        if v.co.y * back_side > 0:
            v.co.x *= back
            v.co.z *= back
    bmesh.ops.scale(bm, vec=Vector((x1 - x0, y1 - y0, z1 - z0)), verts=bm.verts)
    bevel(bm, bev)
    bmesh.ops.translate(bm, vec=Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)), verts=bm.verts)
    return add(surface, bm)


def screen(surface, cx, y, cz, w, h, face=-1, bulge=0.01, tilt=0.0, nx=10, ny=8):
    """A slightly bulging screen with 0..1 UVs, facing -Y (face=-1) or +Y (face=+1), tilted back by `tilt` radians."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = {}
    for i in range(nx + 1):
        for j in range(ny + 1):
            u, v = i / nx, j / ny
            d = bulge * (1 - (2 * u - 1) ** 2) * (1 - (2 * v - 1) ** 2)
            x = (u - 0.5) * w * (-face)                     # mirrored for +Y so the image reads left to right
            z = (v - 0.5) * h
            grid[i, j] = bm.verts.new(Vector((x, face * d, z)))
    for i in range(nx):
        for j in range(ny):
            f = bm.faces.new((grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]))
            for loop, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                loop[uv].uv = (a / nx, b / ny)
    bm.normal_update()
    if tilt:
        bmesh.ops.rotate(bm, cent=Vector(), matrix=Matrix.Rotation(-face * tilt, 3, "X"), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector((cx, y, cz)), verts=bm.verts)
    return add(surface, bm)


def crt(cx, y_front, z_base, w=0.4, h=0.32, depth=0.38, face=-1, kind="ScreenText", tilt=0.05, stand=True):
    """A CRT monitor standing at (cx, z_base), its screen at y_front looking to -Y (face=-1) or +Y."""
    s = -face                                                # direction from the front to the back
    zc = z_base + (0.06 if stand else 0.0) + h / 2 + 0.03
    yb = y_front + s * depth
    y0, y1 = sorted((y_front, yb))
    tapered_box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, y0, y1, zc - h / 2 - 0.05, zc + h / 2 + 0.045, back=0.62,
                back_side=s)
    yf = y_front + face * 0.02                                 # the front of the bezel lip (towards the viewer)
    # bezel lip above and below the glass, the glass in front of the housing, a power LED and button, vents on the top
    lip = sorted((y_front, yf))
    box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, lip[0], lip[1], zc + h / 2, zc + h / 2 + 0.045)
    box("Composite", cx - w / 2 - 0.035, cx + w / 2 + 0.035, lip[0], lip[1], zc - h / 2 - 0.05, zc - h / 2)
    for sx in (-1, 1):
        box("Composite", cx + sx * (w / 2 + 0.0175) - 0.0175, cx + sx * (w / 2 + 0.0175) + 0.0175, lip[0], lip[1], zc - h / 2, zc + h / 2)
    screen(kind, cx, y_front + face * 0.004, zc, w, h, face=face, bulge=0.01, tilt=tilt)
    box("Screen" if kind != "Glass" else "Rubber", cx + s * (w / 2 - 0.01), cx + s * (w / 2 - 0.025), min(yf, yf + face * 0.004), max(yf, yf + face * 0.004),
        zc - h / 2 - 0.035, zc - h / 2 - 0.025, bev=0.001)
    box("Rubber", cx + s * (w / 2 - 0.04), cx + s * (w / 2 - 0.06), min(yf, yf + face * 0.006), max(yf, yf + face * 0.006),
        zc - h / 2 - 0.04, zc - h / 2 - 0.02, bev=0.001)
    for k in range(6):
        vx = cx - 0.12 + k * 0.048
        box("Rubber", vx, vx + 0.03, min(y0, y1) + 0.08, max(y0, y1) - 0.12, zc + h / 2 + 0.044, zc + h / 2 + 0.047, bev=0.0)
    if stand:
        cyl("Composite", cx, (y0 + y1) / 2, z_base, z_base + 0.015, 0.11, seg=24)
        cyl("Composite", cx, (y0 + y1) / 2, z_base + 0.015, z_base + 0.07, 0.05, seg=16, r2=0.07)
    return zc


def keyboard(cx, cy, z, face=-1, missing=(), w=0.46, d=0.16):
    """A keyboard on a desk (the typist sits on the `face` side): a tilted case with rows of keys, a few missing."""
    box("Composite", cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2, z, z + 0.025, rx=face * 0.06)
    rows, cols = 5, 15
    pitch = (w - 0.04) / cols
    for r in range(rows):
        for c in range(cols):
            if (r, c) in missing:
                continue
            if r == 0 and 4 <= c <= 10:                       # the space bar
                if c != 4:
                    continue
                kx0, kx1 = cx - w / 2 + 0.02 + 4 * pitch, cx - w / 2 + 0.02 + 11 * pitch - 0.004
            else:
                kx0 = cx - w / 2 + 0.02 + c * pitch
                kx1 = kx0 + pitch - 0.004
            ky = cy - face * (d / 2 - 0.02 - r * (d - 0.03) / rows)
            ky0, ky1 = sorted((ky, ky + (-face) * ((d - 0.03) / rows - 0.004)))
            kz = z + 0.025 + r * 0.003
            box("Composite" if (r + c) % 7 else "Rubber", kx0, kx1, ky0, ky1, kz, kz + 0.012, bev=0.002)


def tower(cx, cy, z0=0.0, w=0.19, d=0.44, h=0.43, face=-1):
    """A beige PC tower: drive bays, a floppy slot, power button and LED, a vent grille, rust at the feet."""
    y0, y1 = cy - d / 2, cy + d / 2
    box("Composite", cx - w / 2, cx + w / 2, y0, y1, z0 + 0.015, z0 + h, panel=True)
    yf = y0 if face < 0 else y1
    sgn = -1 if face < 0 else 1
    def front(x0, x1, za, zb, surface="Composite", out=0.006):
        ya, yb = sorted((yf, yf + sgn * out))
        box(surface, cx + x0, cx + x1, ya, yb, z0 + za, z0 + zb, bev=0.001)
    front(-0.075, 0.075, h - 0.07, h - 0.025)                  # 5.25" bay
    front(-0.07, 0.07, h - 0.05, h - 0.045, "Rubber", 0.008)
    front(-0.075, 0.075, h - 0.12, h - 0.075)                  # second bay, its cover gone
    front(-0.07, 0.07, h - 0.115, h - 0.08, "Rubber", 0.002)
    front(-0.04, 0.04, h - 0.155, h - 0.15, "Rubber", 0.008)   # floppy slot
    front(-0.02, 0.02, 0.12, 0.16)                             # power button
    front(0.04, 0.05, 0.14, 0.15, "Screen", 0.007)             # LED (barely alive)
    for k in range(7):
        front(-0.07, 0.07, 0.03 + k * 0.012, 0.036 + k * 0.012, "Rubber", 0.003)
    for sx in (-1, 1):
        for sy in (y0 + 0.03, y1 - 0.03):
            box("Rubber", cx + sx * w / 2 * 0.8 - 0.015, cx + sx * w / 2 * 0.8 + 0.015, sy - 0.015, sy + 0.015, z0, z0 + 0.015)


def papers(cx, cy, z, n, seed, spread=0.25):
    r = random.Random(seed)
    for i in range(n):
        x, y = cx + (r.random() - 0.5) * spread, cy + (r.random() - 0.5) * spread * 0.6
        box("Composite", x - 0.105, x + 0.105, y - 0.148, y + 0.148, z + i * 0.002, z + i * 0.002 + 0.0015, rz=(r.random() - 0.5) * 0.8, bev=0.0)


def mug(x, y, z):
    cyl("Composite", x, y, z, z + 0.095, 0.04, seg=16)
    cyl("Rubber", x, y, z + 0.09, z + 0.093, 0.036, seg=16)
    rod("Composite", (x + 0.04, y, z + 0.075), (x + 0.065, y, z + 0.05), 0.008, seg=6)
    rod("Composite", (x + 0.065, y, z + 0.05), (x + 0.04, y, z + 0.025), 0.008, seg=6)


# --------------------------------------------------------------------------- the computers

def desk():
    """Workstation: a steel desk, a CRT terminal still showing the last status report, keyboard and mouse, a beige
    tower under the desk, cables, papers, a mug and a binder stack."""
    box("Steel", -0.8, 0.8, -0.4, 0.4, 0.70, 0.74, panel=True)
    box("Paint", -0.78, 0.78, -0.38, 0.38, 0.64, 0.70)
    for sx in (-1, 1):
        box("Paint", sx * 0.74 - 0.03, sx * 0.74 + 0.03, -0.36, 0.36, 0.0, 0.64)
        box("Paint", sx * 0.74 - 0.03, sx * 0.74 + 0.03, -0.36, 0.36, 0.12, 0.16)
    box("Paint", -0.71, 0.71, 0.33, 0.36, 0.3, 0.62)                                          # modesty panel
    box("Paint", 0.3, 0.71, -0.34, 0.33, 0.08, 0.62, panel=True)                              # drawer block
    for k in range(3):
        z = 0.12 + k * 0.165
        open_ = 0.14 if k == 1 else 0.0
        box("Paint", 0.32, 0.69, -0.37 - open_, -0.34 - open_, z, z + 0.15, panel=True)
        box("Steel", 0.45, 0.56, -0.39 - open_, -0.37 - open_, z + 0.1, z + 0.12)
    zc = crt(0.02, 0.0, 0.74, w=0.36, h=0.28, depth=0.36, face=-1, kind="ScreenText")
    box("Composite", -0.12, -0.04, -0.005, -0.004, zc + 0.12, zc + 0.15, bev=0.0)               # a sticky note on the bezel
    keyboard(0.0, -0.24, 0.74, face=-1, missing={(2, 3), (3, 11), (1, 7)})
    box("Rubber", 0.28, 0.48, -0.32, -0.12, 0.74, 0.743, bev=0.0)                              # mouse pad
    sphere("Composite", 0.37, -0.22, 0.755, 0.03, seg=12, sz=0.55)                             # mouse
    cable((0.37, -0.19, 0.755), (0.2, 0.3, 0.74), sag=-0.01, r=0.003)
    tower(-0.55, -0.02, 0.0, face=-1)
    cable((-0.55, 0.2, 0.3), (0.0, 0.25, 0.85), sag=0.25, r=0.006)
    cable((-0.6, 0.22, 0.15), (-0.7, 0.4, 0.0), sag=0.05, r=0.006)
    cable((0.1, 0.3, 0.8), (0.4, 0.42, 0.0), sag=0.1, r=0.005)
    papers(-0.48, -0.15, 0.74, 4, 3)
    mug(0.6, 0.05, 0.74)
    for k in range(4):                                                                         # binders
        box("Vinyl" if k % 2 else "Rust", 0.52 + k * 0.045, 0.56 + k * 0.045, 0.12, 0.38, 0.74, 1.02, ry=0.05 * k)
    export("desk")


def reception_desk():
    """Security checkpoint console: an angled control desk with switch banks, three CRTs for the guard behind it
    (+Y), a keyboard, a microphone on a gooseneck and a dead barrier arm stub."""
    box("Paint", -1.25, 1.25, -0.48, -0.38, 0.0, 1.14, panel=True)                             # high front skirt
    box("Steel", -1.25, 1.25, -0.48, -0.2, 1.08, 1.14, rx=-0.15)
    box("Paint", -1.25, 1.25, -0.38, 0.4, 0.0, 0.7, panel=True)
    box("Composite", -1.2, 1.2, -0.36, 0.0, 0.7, 0.8, rx=0.35, panel=True)                     # angled control panel
    r = random.Random(9)
    for row in range(3):
        for i in range(22):
            x = -1.1 + i * 0.1
            y = -0.3 + row * 0.1
            kind = r.random()
            if kind < 0.45:
                box("Rubber", x, x + 0.04, y, y + 0.04, 0.78 - row * 0.03, 0.8 - row * 0.03, bev=0.002)
            elif kind < 0.6:
                box("Screen", x, x + 0.025, y + 0.01, y + 0.035, 0.785 - row * 0.03, 0.795 - row * 0.03, bev=0.001)
            elif kind < 0.8:
                cyl("Steel", x + 0.02, y + 0.02, 0.785 - row * 0.03, 0.81 - row * 0.03, 0.012, seg=10)
    for cx, kind in ((-0.75, "ScreenStatic"), (0.0, "ScreenText"), (0.75, "Glass")):
        crt(cx, 0.06, 0.7, w=0.34, h=0.26, depth=0.32, face=1, kind=kind)
    keyboard(0.0, 0.25, 0.7, face=1, missing={(1, 2), (4, 9)})
    rod("Steel", (0.45, 0.2, 0.7), (0.45, 0.22, 0.82), 0.005)
    rod("Steel", (0.45, 0.22, 0.82), (0.42, 0.3, 0.88), 0.005)
    cyl("Rubber", 0.42, 0.3, 0.87, 0.9, 0.012, seg=10)
    papers(-0.6, 0.3, 0.7, 3, 4)
    mug(0.95, 0.3, 0.7)
    cable((-0.75, 0.3, 0.75), (-1.0, 0.42, 0.0), sag=0.15)
    cable((0.75, 0.3, 0.75), (1.1, 0.42, 0.0), sag=0.12)
    export("reception_desk")


def server_rack():
    """Open server rack: rails, rows of rack units (drive bays, handles, a few dead status LEDs), one unit pulled out,
    a patch panel with looping cables, the mesh door hanging open."""
    W, D, H = 0.7, 1.02, 2.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    for sx in (x0, x1 - 0.04):
        for sy in (y0, y1 - 0.04):
            box("Paint", sx, sx + 0.04, sy, sy + 0.04, 0.0, H)
    for z in (0.0, H - 0.04):
        box("Paint", x0, x1, y0, y1, z, z + 0.04, panel=True)
    box("Paint", x0, x0 + 0.01, y0 + 0.04, y1 - 0.04, 0.04, H - 0.04, panel=True)               # side panel (one left)
    r = random.Random(17)
    z = 0.12
    while z < H - 0.2:
        u = r.choice((1, 1, 2, 2, 3))
        h = u * 0.0445
        out = 0.35 if abs(z - 1.05) < 0.05 else 0.0
        if r.random() < 0.12:
            z += h + 0.003
            continue                                                                            # an empty slot
        box("Steel", x0 + 0.05, x1 - 0.05, y0 + 0.03 - out, y1 - 0.12 - out, z, z + h - 0.003, panel=u > 1)
        box("Composite" if u > 1 else "Paint", x0 + 0.03, x1 - 0.03, y0 + 0.01 - out, y0 + 0.03 - out, z, z + h - 0.003, bev=0.002)
        for sx in (x0 + 0.045, x1 - 0.065):                                                     # handles
            box("Steel", sx, sx + 0.02, y0 - 0.01 - out, y0 + 0.01 - out, z + 0.006, z + h - 0.01, bev=0.002)
        if u > 1:
            for k in range(6):                                                                  # drive bays
                bx = x0 + 0.1 + k * 0.075
                box("Rubber", bx, bx + 0.06, y0 + 0.005 - out, y0 + 0.012 - out, z + 0.008, z + h - 0.012, bev=0.001)
        for k in range(3):                                                                      # status LEDs: a few alive
            lx = x1 - 0.12 + k * 0.018
            surf = "Screen" if r.random() < 0.3 else "Rubber"
            box(surf, lx, lx + 0.008, y0 + 0.004 - out, y0 + 0.012 - out, z + h - 0.02, z + h - 0.012, bev=0.0)
        z += h + 0.003
    # patch panel at the top, cables looping down the front
    box("Rubber", x0 + 0.03, x1 - 0.03, y0 + 0.01, y0 + 0.04, H - 0.17, H - 0.12)
    for k in range(10):
        px = x0 + 0.08 + k * 0.055
        cable((px, y0 + 0.0, H - 0.15), (px + r.uniform(-0.15, 0.15), y0 - 0.05, H - 0.6 - r.random() * 0.6), sag=-0.05 - r.random() * 0.1, r=0.004)
    cable((x1 - 0.1, y0 - 0.02, H - 0.15), (x1 + 0.1, y0 - 0.3, 0.0), sag=0.2, r=0.006)
    # the mesh door, hanging open on its hinge at the left front post
    hx, hy = x0, y0 - 0.02
    ang = math.radians(-70)
    def dp(u, z):
        return (hx + math.cos(ang) * u, hy + math.sin(ang) * u, z)
    rod("Paint", dp(0.0, 0.05), dp(0.0, H - 0.05), 0.012)
    rod("Paint", dp(W - 0.02, 0.05), dp(W - 0.02, H - 0.05), 0.012)
    rod("Paint", dp(0.0, 0.05), dp(W - 0.02, 0.05), 0.012)
    rod("Paint", dp(0.0, H - 0.05), dp(W - 0.02, H - 0.05), 0.012)
    for k in range(1, 20):
        zz = 0.05 + k * (H - 0.1) / 20
        rod("Steel", dp(0.0, zz), dp(W - 0.02, zz), 0.003, seg=4)
    for k in range(1, 7):
        uu = k * (W - 0.02) / 7
        rod("Steel", dp(uu, 0.05), dp(uu, H - 0.05), 0.003, seg=4)
    export("server_rack")


def tv_stand():
    """CCTV monitor bank: a steel shelf with six small CRTs — static, the terminal, a couple of dead glass — and a
    tangle of coax."""
    for sx in (-0.68, 0.64):
        for sy in (-0.18, 0.14):
            box("Paint", sx, sx + 0.04, sy, sy + 0.04, 0.0, 1.45)
    for z in (0.08, 0.66, 1.41):
        box("Steel", -0.7, 0.7, -0.2, 0.2, z, z + 0.03, panel=True)
    kinds = ["ScreenStatic", "Glass", "ScreenText", "ScreenStatic", "ScreenStatic", "Glass"]
    i = 0
    for zb in (0.11, 0.69):
        for cx in (-0.44, 0.0, 0.44):
            crt(cx, -0.18, zb, w=0.32, h=0.25, depth=0.32, face=-1, kind=kinds[i], stand=False, tilt=0.0)
            i += 1
    r = random.Random(23)
    for k in range(6):
        cable((-0.5 + k * 0.2, 0.18, 0.75 + (k % 2) * 0.55), (-0.6 + r.random() * 1.2, 0.22, 0.0), sag=0.1, r=0.005)
    export("tv_stand")


def printer():
    """A line printer on a stand: a fan-fold paper stack, the last printout spilling over the front onto the floor."""
    box("Composite", -0.3, 0.3, -0.38, 0.38, 0.62, 0.98, panel=True)
    box("Composite", -0.3, 0.3, -0.38, -0.1, 0.98, 1.05, rx=-0.25)                            # lid
    box("Screen", 0.12, 0.24, -0.395, -0.385, 0.9, 0.94, bev=0.0)                              # status panel
    for k in range(4):
        box("Rubber", -0.2 + k * 0.05, -0.17 + k * 0.05, -0.395, -0.385, 0.9, 0.93, bev=0.001)
    legs4(0.26, 0.34, 0.62, r=0.02)
    box("Paint", -0.27, 0.27, -0.35, 0.35, 0.2, 0.22)
    for k in range(10):                                                                        # fan-fold stack below
        box("Composite", -0.2, 0.2, -0.2, 0.2, 0.22 + k * 0.012, 0.231 + k * 0.012, rz=(k % 2) * 0.02, bev=0.0)
    pts = [(-0.38, 0.96), (-0.45, 0.9), (-0.5, 0.7), (-0.52, 0.45), (-0.55, 0.2), (-0.7, 0.02), (-0.95, 0.005)]
    for (ya, za), (yb, zb) in zip(pts, pts[1:]):                                               # the printout, spilling
        box("Composite", -0.19, 0.19, min(ya, yb), max(ya, yb) + 0.002, min(za, zb), max(za, zb) + 0.002, bev=0.0)
    export("printer")


def arcade_machine():
    """Diagnostic kiosk: a slanted composite cabinet with a CRT behind a steel bezel, a keypad, cables torn out."""
    box("Composite", -0.36, 0.36, -0.25, 0.4, 0.0, 1.8, panel=True)
    box("Composite", -0.36, 0.36, -0.55, -0.25, 0.0, 0.9, panel=True)
    box("Steel", -0.33, 0.33, -0.52, -0.2, 0.9, 0.96, rx=0.35)
    screen("ScreenText", 0.0, -0.31, 1.33, 0.5, 0.38, face=-1, bulge=0.015, tilt=0.12)
    t = 0.04
    box("Steel", -0.32, 0.32, -0.36, -0.25, 1.54, 1.54 + t)
    box("Steel", -0.32, 0.32, -0.36, -0.25, 1.12 - t, 1.12)
    box("Steel", -0.28 - t, -0.28, -0.36, -0.25, 1.12, 1.54)
    box("Steel", 0.28, 0.28 + t, -0.36, -0.25, 1.12, 1.54)
    box("Composite", -0.36, 0.36, -0.36, -0.25, 1.54, 1.8)
    for i in range(4):
        for j in range(4):
            box("Rubber", -0.2 + j * 0.07, -0.15 + j * 0.07, -0.5 + i * 0.06, -0.455 + i * 0.06, 0.95 + i * 0.022, 0.968 + i * 0.022, bev=0.002)
    cable((0.3, -0.56, 0.4), (0.4, -0.7, 0.0), sag=0.05)
    cable((0.2, -0.56, 0.5), (0.1, -0.75, 0.0), sag=0.02)
    export("arcade_machine")


# --------------------------------------------------------------------------- the props (sizes from our models)


def chair():
    """Swivel lab chair: torn vinyl seat and back on a gas column, a five-star base with one caster missing."""
    for i in range(5):
        a = i * 2 * math.pi / 5 + 0.3
        rod("Paint", (0, 0, 0.09), (0.27 * math.cos(a), 0.27 * math.sin(a), 0.06), 0.018)
        if i != 3:
            sphere("Rubber", 0.27 * math.cos(a), 0.27 * math.sin(a), 0.035, 0.035, seg=10)
    cyl("Steel", 0, 0, 0.08, 0.45, 0.025, seg=14)
    box("Vinyl", -0.24, 0.24, -0.24, 0.22, 0.45, 0.53, ry=0.03, panel=True)
    box("Paint", -0.03, 0.03, 0.2, 0.25, 0.45, 0.75)
    box("Vinyl", -0.22, 0.22, 0.21, 0.28, 0.62, 1.04, rx=-0.12, panel=True)
    export("chair")


def sofa():
    """A row of three bolted waiting-area seats on a beam; the middle seat is broken and hangs down."""
    box("Paint", -1.05, 1.05, -0.05, 0.05, 0.3, 0.38)
    for sx in (-0.8, 0.8):
        box("Paint", sx - 0.04, sx + 0.04, -0.3, 0.3, 0.0, 0.32)
        box("Rubber", sx - 0.06, sx + 0.06, -0.35, 0.35, 0.0, 0.02)
    for i, x in enumerate((-0.72, 0.0, 0.72)):
        broken = i == 1
        tilt = 0.32 if broken else 0.0
        box("Vinyl", x - 0.32, x + 0.32, -0.4, 0.16, 0.39, 0.47, rx=tilt, panel=True)
        box("Vinyl", x - 0.3, x + 0.3, 0.2, 0.3, 0.48, 0.86, rx=-0.15 - (0.25 if broken else 0.0), panel=True)
        box("Paint", x - 0.36, x - 0.32, -0.35, 0.25, 0.45, 0.62)                             # armrests
    box("Paint", 0.68, 0.72, -0.35, 0.25, 0.45, 0.62)
    export("sofa")


def armchair():
    """Examination chair: reclined padded vinyl on a hydraulic base, a headrest and a swing arm with a dead lamp."""
    box("Steel", -0.3, 0.3, -0.3, 0.3, 0.0, 0.05, panel=True)
    cyl("Paint", 0, 0.05, 0.05, 0.38, 0.07, seg=18)
    box("Vinyl", -0.32, 0.32, -0.38, 0.1, 0.38, 0.48, rx=0.12, panel=True)                   # seat
    box("Vinyl", -0.32, 0.32, 0.08, 0.2, 0.42, 0.82, rx=-0.45, panel=True)                    # back
    box("Vinyl", -0.12, 0.12, 0.28, 0.34, 0.74, 0.87, rx=-0.5)                                 # headrest
    for sx in (-1, 1):
        box("Paint", sx * 0.36 - 0.03, sx * 0.36 + 0.03, -0.2, 0.15, 0.5, 0.56)
    rod("Paint", (0.42, 0.3, 0.0), (0.42, 0.3, 0.8), 0.02)
    rod("Paint", (0.42, 0.3, 0.8), (0.2, -0.1, 0.84), 0.015)
    cyl("Composite", 0.2, -0.1, 0.78, 0.84, 0.07, seg=16, r2=0.05)
    cable((0.42, 0.32, 0.6), (0.1, 0.25, 0.4), sag=0.12)
    export("armchair")


def bean_bag():
    """Debris pile: slumped vinyl sacks, a fallen ceiling panel, cable ends."""
    sphere("Vinyl", -0.12, 0.05, 0.18, 0.3, seg=14, sz=0.6)
    sphere("Vinyl", 0.2, -0.1, 0.14, 0.24, seg=14, sz=0.55)
    sphere("Vinyl", 0.05, 0.22, 0.32, 0.2, seg=12, sz=0.7)
    box("Steel", -0.42, 0.3, -0.35, 0.15, 0.3, 0.33, rx=0.5, ry=-0.3)
    box("Rust", -0.1, 0.42, -0.42, 0.25, 0.0, 0.03, rz=0.3)
    cable((-0.3, -0.3, 0.02), (0.3, 0.35, 0.55), sag=-0.05)
    export("bean_bag")


def coffee_table():
    """A heavy equipment case on the floor: ribbed composite shell, latches, a stencil band, lid slightly open."""
    box("Composite", -0.5, 0.5, -0.27, 0.27, 0.0, 0.3, panel=True)
    box("Composite", -0.5, 0.5, -0.27, 0.27, 0.31, 0.4, rx=-0.05, panel=True)
    for x in (-0.3, 0.3):
        box("Steel", x - 0.05, x + 0.05, -0.29, -0.27, 0.24, 0.34)
    for x in (-0.35, 0.0, 0.35):
        box("Composite", x - 0.02, x + 0.02, -0.28, 0.28, 0.02, 0.29)
    box("Rubber", -0.12, 0.12, 0.27, 0.3, 0.18, 0.22)
    export("coffee_table")


def plant():
    """Dead specimen in a hydroponic canister: composite base, a cracked glass ring frame, dry stalks bending over."""
    cyl("Composite", 0, 0, 0.0, 0.45, 0.26, seg=20, r2=0.23)
    cyl("Steel", 0, 0, 0.45, 0.5, 0.27, seg=20)
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        rod("Steel", (0.24 * math.cos(a), 0.24 * math.sin(a), 0.5), (0.2 * math.cos(a), 0.2 * math.sin(a), 1.25), 0.012)
    cyl("Steel", 0, 0, 1.22, 1.26, 0.22, seg=20)
    r = random.Random(5)
    for i in range(9):
        a = r.random() * 2 * math.pi
        h = 0.6 + r.random() * 0.55
        bend = 0.15 + r.random() * 0.2
        p0 = (0.05 * math.cos(a), 0.05 * math.sin(a), 0.5)
        p1 = (0.08 * math.cos(a), 0.08 * math.sin(a), 0.5 + h * 0.6)
        p2 = ((0.1 + bend) * math.cos(a), (0.1 + bend) * math.sin(a), 0.5 + h * 0.75)
        rod("Rust", p0, p1, 0.008, seg=5)
        rod("Rust", p1, p2, 0.006, seg=5)
    cable((0.2, 0.2, 0.3), (0.32, 0.3, 0.0), sag=0.05)
    export("plant")


def bookshelf():
    """Archive rack: steel uprights, shelves of data cartridges, a few fallen over, one shelf sagging."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.48 - 0.02, sx * 0.48 + 0.02, sy * 0.15 - 0.02, sy * 0.15 + 0.02, 0.0, 2.0)
    r = random.Random(7)
    for i, z in enumerate((0.1, 0.55, 1.0, 1.45, 1.9)):
        sag = 0.05 if i == 2 else 0.0
        box("Steel", -0.48, 0.48, -0.16, 0.16, z, z + 0.02, ry=sag)
        if z > 1.85:
            continue
        x = -0.44
        while x < 0.4:
            w = 0.035 + r.random() * 0.02
            if r.random() < 0.15:
                box("Composite", x, x + 0.25, -0.12, 0.12, z + 0.02, z + 0.02 + w, rz=r.random() * 0.4)   # fallen
                x += 0.27
                continue
            h = 0.22 + r.random() * 0.12
            box("Composite" if r.random() < 0.7 else "Rubber", x, x + w, -0.13, 0.12, z + 0.02, z + 0.02 + h, ry=(r.random() - 0.5) * 0.15)
            x += w + 0.005
    export("bookshelf")


def round_table():
    """An empty industrial cable spool used as a table, with a mug and a coil of cable."""
    cyl("Rust", 0, 0, 0.0, 0.06, 0.5, seg=28)
    cyl("Rust", 0, 0, 0.7, 0.76, 0.5, seg=28)
    cyl("Paint", 0, 0, 0.06, 0.7, 0.2, seg=20)
    for i in range(8):
        a = i * math.pi / 4
        box("Steel", 0.42 * math.cos(a) - 0.02, 0.42 * math.cos(a) + 0.02, 0.42 * math.sin(a) - 0.02, 0.42 * math.sin(a) + 0.02, 0.76, 0.77)
    for k in range(4):
        cyl("Rubber", 0, 0, 0.12 + k * 0.03, 0.15 + k * 0.03, 0.22, seg=24)
    cyl("Composite", 0.2, -0.15, 0.76, 0.86, 0.04, seg=12)
    export("round_table")


def meeting_table():
    """Long steel lab table: two work tops, a sink in the middle, gas taps, shelf below with jars, a lamp arm."""
    box("Steel", -1.6, 1.6, -0.65, 0.65, 0.72, 0.78, panel=True)
    for sx in (-1, 1):
        box("Paint", sx * 1.5 - 0.05, sx * 1.5 + 0.05, -0.6, 0.6, 0.0, 0.72, panel=True)
    box("Paint", -1.45, 1.45, -0.6, 0.6, 0.15, 0.18)
    box("Steel", -0.3, 0.3, -0.25, 0.25, 0.7, 0.785)
    box("Rust", -0.26, 0.26, -0.21, 0.21, 0.74, 0.786)                                          # sink basin
    rod("Steel", (0, 0.32, 0.78), (0, 0.32, 1.05), 0.015)
    rod("Steel", (0, 0.32, 1.05), (0, 0.1, 1.0), 0.012)
    for x in (-1.1, -0.6, 0.6, 1.1):
        cyl("Steel", x, 0.55, 0.78, 0.9, 0.015, seg=8)
        sphere("Rust", x, 0.55, 0.9, 0.025, seg=8)
    r = random.Random(3)
    for i in range(10):
        x = -1.3 + i * 0.26
        cyl("Composite", x, (r.random() - 0.5) * 0.6, 0.18, 0.18 + 0.1 + r.random() * 0.12, 0.05, seg=12)
    rod("Paint", (1.3, 0.6, 0.78), (1.3, 0.6, 1.3), 0.018)
    rod("Paint", (1.3, 0.6, 1.3), (0.9, 0.2, 1.2), 0.015)
    cyl("Composite", 0.9, 0.2, 1.12, 1.2, 0.09, seg=16, r2=0.06)
    cable((-1.5, 0.62, 0.7), (-0.6, 0.62, 0.0), sag=0.2)
    export("meeting_table")


def ping_pong_table():
    """Surgical table: tilted padded slab on a pedestal, instrument tray, a ring lamp on a post (where our status dot is)."""
    box("Steel", -0.5, 0.5, -0.4, 0.4, 0.0, 0.06, panel=True)
    cyl("Paint", 0, 0, 0.06, 0.62, 0.12, seg=18)
    box("Vinyl", -1.0, 1.0, -0.36, 0.36, 0.64, 0.74, ry=0.04, panel=True)
    box("Steel", -1.05, 1.05, -0.4, 0.4, 0.62, 0.65, ry=0.04)
    box("Steel", 1.1, 1.36, -0.7, -0.3, 0.8, 0.82)                                             # instrument tray
    rod("Paint", (1.23, -0.5, 0.0), (1.23, -0.5, 0.8), 0.015)
    rod("Paint", (0.0, 0.84, 0.0), (0.0, 0.84, 0.91), 0.02)                                    # post at our status dot
    rod("Paint", (0.0, 0.84, 0.91), (0.0, 0.3, 0.91), 0.015)
    cyl("Composite", 0.0, 0.3, 0.85, 0.91, 0.18, seg=24)
    cable((0.0, 0.84, 0.5), (0.4, 0.8, 0.0), sag=0.05)
    export("ping_pong_table")



def coat_rack():
    """IV / equipment stand on casters with a hanging hazmat hood and coiled hoses."""
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        rod("Steel", (0, 0, 0.08), (0.24 * math.cos(a), 0.24 * math.sin(a), 0.05), 0.014)
        sphere("Rubber", 0.24 * math.cos(a), 0.24 * math.sin(a), 0.03, 0.03, seg=8)
    rod("Steel", (0, 0, 0.05), (0, 0, 1.8), 0.015)
    for i in range(4):
        a = i * math.pi / 2
        rod("Steel", (0, 0, 1.7), (0.15 * math.cos(a), 0.15 * math.sin(a), 1.76), 0.008)
    sphere("Vinyl", 0.1, -0.08, 1.45, 0.16, seg=14, sz=1.3)                                     # hood
    box("Composite", 0.0, 0.2, -0.22, -0.18, 1.38, 1.52)                                        # visor
    for k in range(5):
        cyl("Rubber", -0.12, 0.05, 0.9 + k * 0.03, 0.92 + k * 0.03, 0.12, seg=16)
    cable((-0.12, 0.05, 0.9), (-0.2, 0.2, 0.2), sag=0.05)
    export("coat_rack")


def whiteboard():
    """Wall display panel: thick steel frame, cracked dead glass, a small status panel, a conduit going up."""
    box("Paint", -0.95, 0.95, -0.04, 0.04, 0.75, 1.95, panel=False)
    box("Composite", -0.88, 0.88, -0.045, -0.035, 0.82, 1.88)
    for a, b in (((-0.4, -0.05, 1.5), (0.1, -0.05, 1.2)), ((0.1, -0.05, 1.2), (0.5, -0.05, 1.45)), ((0.1, -0.05, 1.2), (0.0, -0.05, 0.9))):
        rod("Rubber", a, b, 0.004, seg=4)                                                     # cracks
    box("Screen", 0.6, 0.85, -0.05, -0.04, 0.78, 0.81)
    for sx in (-1, 1):
        box("Paint", sx * 0.75 - 0.03, sx * 0.75 + 0.03, -0.03, 0.03, 0.0, 0.75)
    box("Paint", -0.05, 0.05, -0.04, 0.04, 1.95, 2.0)
    export("whiteboard")



def partition():
    """A section of wire-mesh security fence in a steel frame, bent at one corner."""
    box("Paint", -0.81, 0.81, -0.03, 0.03, 1.2, 1.26)
    box("Paint", -0.81, 0.81, -0.03, 0.03, 0.0, 0.06)
    for x in (-0.81, 0.81):
        box("Paint", x - 0.03, x + 0.03, -0.03, 0.03, 0.0, 1.26)
    for i in range(1, 16):
        x = -0.81 + i * 0.1
        rod("Steel", (x, 0, 0.06), (x, 0, 1.2), 0.003, seg=4)
    for i in range(1, 12):
        z = i * 0.1
        rod("Steel", (-0.78, 0, z), (0.78, 0, z), 0.003, seg=4)
    export("partition")


def cabinet():
    """Tool chest: stacked steel drawers with pull handles, one drawer pulled out, a rag of vinyl over the top."""
    box("Paint", -0.45, 0.45, -0.25, 0.27, 0.05, 1.4, panel=True)
    for k in range(6):
        z = 0.1 + k * 0.21
        out = 0.18 if k == 2 else 0.0
        box("Paint", -0.42, 0.42, -0.27 - out, -0.22 - out, z, z + 0.19, panel=True)
        box("Steel", -0.2, 0.2, -0.3 - out, -0.27 - out, z + 0.13, z + 0.16)
    for sx in (-1, 1):
        for sy in (-1, 1):
            sphere("Rubber", sx * 0.38, sy * 0.2, 0.03, 0.03, seg=8)
    box("Vinyl", -0.3, 0.2, -0.2, 0.27, 1.4, 1.42, ry=0.05)
    export("cabinet")


def shelf():
    """Heavy steel rack: canisters, boxes and a toppled drum."""
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Paint", sx * 0.98 - 0.03, sx * 0.98 + 0.03, sy * 0.28 - 0.03, sy * 0.28 + 0.03, 0.0, 2.0)
    r = random.Random(13)
    for z in (0.1, 0.75, 1.4, 1.97):
        box("Steel", -0.98, 0.98, -0.29, 0.29, z, z + 0.03, panel=True)
        if z > 1.9:
            continue
        x = -0.9
        while x < 0.8:
            kind = r.random()
            if kind < 0.45:
                cyl("Composite", x + 0.1, (r.random() - 0.5) * 0.2, z + 0.03, z + 0.03 + 0.3 + r.random() * 0.15, 0.09, seg=14)
                x += 0.22
            elif kind < 0.8:
                box("Composite" if r.random() < 0.5 else "Rust", x, x + 0.3, -0.22, 0.22, z + 0.03, z + 0.03 + 0.2 + r.random() * 0.2, panel=True)
                x += 0.34
            else:
                x += 0.25
    export("shelf")


def boxes():
    """Stacked hard cases and a drum (our boxes stand from x = -0.3 to 0.78)."""
    box("Composite", -0.3, 0.3, -0.3, 0.3, 0.0, 0.45, panel=True)
    box("Composite", -0.27, 0.27, -0.27, 0.27, 0.45, 0.85, rz=0.12, panel=True)
    cyl("Rust", 0.55, 0.0, 0.0, 0.62, 0.22, seg=20)
    for z in (0.12, 0.5):
        cyl("Rust", 0.55, 0.0, z, z + 0.03, 0.225, seg=20)
    export("boxes")


def trash_bin():
    """Biohazard waste bin: composite drum with a hinged lid propped open, foot pedal."""
    cyl("Composite", 0, 0, 0.0, 0.4, 0.18, seg=20, r2=0.19)
    box("Composite", -0.19, 0.19, -0.02, 0.19, 0.4, 0.43, rx=-0.6)
    box("Steel", -0.08, 0.08, -0.25, -0.17, 0.0, 0.03)
    cyl("Rubber", 0, 0, 0.0, 0.02, 0.185, seg=20)
    export("trash_bin")


PROPS = [desk, chair, sofa, armchair, bean_bag, coffee_table, plant, bookshelf, round_table, meeting_table, ping_pong_table,
         arcade_machine, coat_rack, whiteboard, reception_desk, partition, cabinet, shelf, boxes, trash_bin,
         server_rack, tv_stand, printer]

if __name__ == "__main__":
    clear()
    for make in PROPS:
        make()
    print("DONE")
