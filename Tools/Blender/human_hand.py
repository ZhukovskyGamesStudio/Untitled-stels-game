"""A quick look at the rigged humans' hand alone (seconds, not the ten minutes of a whole figure): the sculpt, the game
mesh, and the game mesh with the fingers closed by the skeleton — each from six sides in the hand's own frame.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/human_hand.py -- <out.png> [female]

Writes <out>_sculpt.png, <out>_game.png, <out>_half.png, <out>_fist.png, <out>_pinch.png (rows of: the back, the palm,
the thumb's edge, the little finger's edge, from the fingertips, three quarters on the palm).
"""
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0])
FEMALE = "female" in argv
sys.argv = sys.argv[:1]                         # (human.py reads the arguments when it is imported)
import human  # noqa: E402
from humanlib import anim, body, build, clothes, sdf, view  # noqa: E402
from humanlib.body import J  # noqa: E402
from humanlib.build import Piece  # noqa: E402

bpy.ops.wm.read_factory_settings(use_empty=True)
W, D, N, T = J["wrist"], body.HAND_D, body.HAND_N, body.HAND_T
hand = human.sculpt("hand.L", lambda p: clothes.hand(p, FEMALE), W + (-0.10, -0.12, -0.20), W + (0.14, 0.12, 0.08), 0.0012)
build.paint(hand, *human.paint_hand(sdf.points_of(hand), build.normals_of(hand)))


def keep(pts):
    q = clothes.hand_frame(pts)
    return np.clip(np.maximum((q[:, 0] - 0.075) / 0.02, (q[:, 1] - 0.030) / 0.012), 0, 1) * 0.75


piece = Piece("hand.L", "AccentHead", "skin", hand, 1500, [], keep=keep)
build.lowpoly(piece)
build.clean(piece.lo)
build.orient_like(piece.lo, piece.hi)
print("HAND tris", sdf.tri_count(piece.lo))
errors = build.audit(piece.lo, "hand")

rig = anim.skeleton()
for name in anim.bone_segments():
    piece.lo.vertex_groups.new(name=name)
build.assign(piece.lo, human.hand_weights(sdf.points_of(piece.lo), "L"))
piece.lo.parent = rig
piece.lo.modifiers.new("Rig", "ARMATURE").object = rig

c = W + D * 0.075
shots = [("back", tuple(-N), tuple(c), 0.24), ("palm", tuple(N), tuple(c), 0.24), ("thumb edge", tuple(T), tuple(c), 0.24),
         ("little edge", tuple(-T), tuple(c), 0.24), ("tips", tuple(D), tuple(c), 0.24), ("3/4", tuple(N + 0.7 * T + 0.5 * D), tuple(c), 0.24)]
base = os.path.splitext(OUT)[0]
hand.color = (0.9, 0.72, 0.6, 1)
piece.lo.color = (0.9, 0.72, 0.6, 1)
view.sheet(base + "_sculpt.png", [hand], shots=shots, width=380, height=380, color="VERTEX")
hand.hide_render = True
view.sheet(base + "_game.png", [piece.lo], shots=shots, width=380, height=380)
P = anim.Poser(rig)
for label, fingers, thumb in (("half", 40, 14), ("fist", 78, 30)):
    P.clear()
    P.bend("fingers.L", fwd=fingers)
    P.bend("thumb.L", fwd=thumb)
    P.apply()
    bpy.context.view_layer.update()
    view.sheet(f"{base}_{label}.png", [piece.lo], shots=shots, width=380, height=380)
print("DONE", errors, "errors")
