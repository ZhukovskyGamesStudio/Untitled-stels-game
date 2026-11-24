"""A computer keyboard and a mouse, for every desk: real rows and key sizes, the legends printed on the caps (Art/Textures/
Covers/keyboard.png, drawn from the same layout: machine_atlas.py). A part of its own, named Keys…, painted KeyCaps.

    import keyboard
    keyboard.add("Keys", (x, y, desk top), yaw=0, light=False, mouse=0.3)     # its middle; the sitter at -Y

Run alone to look at it:  blender.exe -b --factory-startup --python Tools/Blender/keyboard.py -- <out.fbx>
"""
import math
import os
import sys

import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import machine_atlas as at  # noqa: E402
import printed  # noqa: E402
import shapes as s  # noqa: E402

U = 0.0188                                        # a key's width, metres
W, D = at.KEYS_W * U, at.KEYS_H * U               # the field of keys
BORDER = 0.007
TILT = math.radians(4.5)


def _swatch(name):
    x, y = at.KEY_SWATCH[name]
    return x / at.SIZE, 1 - y / at.SIZE


def _cap(x, y, w, h, z):
    """A key cap: a low pyramid without its tip; x, y in metres from the field's middle (y up the board)."""
    bm = bmesh.new()
    g, top = 0.0006, 0.0022
    lo = [(x + g, y + g), (x + w - g, y + g), (x + w - g, y + h - g), (x + g, y + h - g)]
    hi = [(x + top, y + top * 1.3), (x + w - top, y + top * 1.3), (x + w - top, y + h - top * 0.5), (x + top, y + h - top * 0.5)]
    a = [bm.verts.new((px, py, z)) for px, py in lo]
    b = [bm.verts.new((px, py, z + 0.0065)) for px, py in hi]
    bm.faces.new(b)
    for k in range(4):
        bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
    return bm


def add(name, loc=(0, 0, 0), yaw=0.0, light=False, mouse=0.31, only_keys=False):
    """The keyboard (and the mouse `mouse` metres to its right; 0: none) as an object with its own UVs."""
    kind = "light" if light else "black"
    ox, oy = at.KEYBOARDS[kind]
    bms = [s.rbox((W + 2 * BORDER, D + 2 * BORDER, 0.016), (0, 0, 0.008), r=0.004, seg=2)]
    for _, x, y, w, h in at.keys():
        bms.append(_cap(-W / 2 + x * U, D / 2 - (y + h) * U, w * U, h * U, 0.0145))
    # it lies on its feet: the back edge higher
    for b in bms:
        s.place(b, rot=(TILT, 0, 0))
        s.place(b, (0, 0, math.sin(TILT) * D / 2))
    n_keys = len(bms)
    if not only_keys:
        bms += [s.rbox((0.05, 0.012, 0.008), (sx * W * 0.38, D / 2 - 0.004, 0.004), r=0.002, seg=1) for sx in (-1, 1)]
    if mouse:
        body = s.loft([(0.0, 0.029, 0.054, 2.6), (0.012, 0.031, 0.056, 2.5), (0.026, 0.027, 0.047, 2.3, 0, 0.004), (0.034, 0.016, 0.03, 2.2, 0, 0.008), (0.0365, 0.0, 0.0, 2, 0, 0.01)], seg=24)
        s.groove(body, "X", 0.0, 0.0012, 0.0014)
        s.place(body, (mouse, -0.012, 0))
        wheel = s.rcyl(0.0085, 0.006, (mouse, -0.045, 0.0265), axis="X", seg=12, r=0.001, bevel_seg=1)
        cable = s.tube([(mouse, 0.04, 0.012), (mouse + 0.004, 0.075, 0.005), (mouse - 0.025, 0.12, 0.004), (mouse - 0.02, 0.17, 0.004)], 0.0022, segments_per=4)
        bms += [body, wheel, cable]
    obj = s.part(name, bms, flat=False)
    inv = Matrix.Translation(Vector((0, 0, -math.sin(TILT) * D / 2)))
    inv = Matrix.Rotation(-TILT, 4, "X") @ inv

    def rule(n, c, co):
        p, q = inv @ co, inv @ c
        if abs(q.x) <= W / 2 + BORDER + 0.002 and abs(q.y) <= D / 2 + BORDER + 0.002 and q.z > 0.0146 and c.x < W / 2 + 0.01:
            u = (p.x + W / 2) / U * at.KEY_PX
            v = (D / 2 - p.y) / U * at.KEY_PX
            return (ox + min(max(u, 0.5), at.KEYS_W * at.KEY_PX - 0.5)) / at.SIZE, 1 - (oy + min(max(v, 0.5), at.KEYS_H * at.KEY_PX - 0.5)) / at.SIZE
        if mouse and c.x > mouse - 0.06:
            if (c - Vector((mouse, -0.045, 0.0265))).length < 0.011 and abs(n.z) < 1.1 and c.z > 0.0265:
                return _swatch("wheel")
            if c.y > 0.052:
                return _swatch("cable")
        return _swatch(kind)
    printed.paint(obj, rule)
    obj.data.transform(Matrix.Translation(Vector(loc)) @ Matrix.Rotation(yaw, 4, "Z"))
    return obj


if __name__ == "__main__":
    out = sys.argv[sys.argv.index("--") + 1]
    s.clear_scene()
    add("Keys")
    s.export_fbx(out)
