"""A quick look at a rigged human's head alone (under a minute instead of the ten of a whole figure): the painted
sculpt of the head, the eyes, the hair and the beard — from the front, three quarters, the side, the back and above.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/human_head.py -- <out.png> <who> [bare] [fine]

`bare` leaves the hair off (the skull and the face alone), `fine` sculpts at the full resolution of the game's build.
"""
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0])
WHO = argv[1]
BARE, FINE = "bare" in argv, "fine" in argv
sys.argv = sys.argv[:1]                         # (human.py reads the arguments when it is imported)
import human  # noqa: E402
from human import H, HEAD_C, HEAD_S  # noqa: E402
from humanlib import body, build, face, sdf, view  # noqa: E402
from humanlib.body import Build  # noqa: E402
from humanlib.sdf import smax, smin  # noqa: E402

bpy.ops.wm.read_factory_settings(use_empty=True)
human.K = 1.0 if FINE else 1.5
person, b = face.PEOPLE[WHO], human.BUILDS[WHO]
f = person.face
skin = np.array(human.SKIN[WHO])
head_f = lambda p: face.head_with_ears(H(p), f) * HEAD_S


def skin_f(p):
    d = smin(head_f(p), body.neck(human.mx(p), Build(neck=b.neck, soft=b.soft)), 0.014)
    return smax(d, 1.405 - p[..., 2], 0.004)


objs = []
hd = human.sculpt("head", skin_f, (0, -0.15, 1.39), (0.113, 0.14, 1.82), 0.0013, mirror=True)
rgb, gloss = human.paint_head(H(sdf.points_of(hd)), person)
build.paint(hd, np.clip(rgb * skin[None, :], 0, 1), gloss)
objs.append(hd)
e = face.eye_centre(f)
for s, sd in ((1, "L"), (-1, "R")):
    c = HEAD_C + np.array((s * e[0], e[1], e[2])) * HEAD_S
    eye = human.eyeball(f"eye.{sd}", c, 48, 24)
    build.paint(eye, *human.paint_eye((sdf.points_of(eye) - c) / HEAD_S, person))
    objs.append(eye)
if not BARE:
    hair = human.sculpt("hair", lambda p: face.hair(H(p), f, person.hair) * HEAD_S, (-0.125, -0.15, 1.49), (0.125, 0.17, 1.85), 0.0016)
    build.paint(hair, *human.paint_hair(H(sdf.points_of(hair)), person, 1.0))
    objs.append(hair)
    if person.beard:
        bd = human.sculpt("beard", lambda p: face.beard(H(p), f) * HEAD_S, (0, -0.15, 1.50), (0.113, 0.07, 1.70), 0.0013, mirror=True)
        build.paint(bd, *human.paint_beard(H(sdf.points_of(bd)), person))
        objs.append(bd)

c = (0, -0.01, 1.665)
shots = [("front", (0, -1, 0), c, 0.34), ("3/4", (0.65, -0.75, 0.05), c, 0.34), ("side", (1, 0, 0), c, 0.34), ("back 3/4", (0.6, 0.8, 0.1), c, 0.34),
         ("top", (0, -0.25, 1), c, 0.34), ("eye", (0.15, -1, 0.0), (0.034, -0.08, 1.668), 0.09), ("mouth", (0.3, -1, -0.05), (0.0, -0.09, 1.60), 0.11)]
base = os.path.splitext(OUT)[0]
view.sheet(base + ".png", objs, shots=shots, width=400, height=440, color="VERTEX")
for o in objs:
    o.color = (0.75, 0.75, 0.76, 1)
view.sheet(base + "_clay.png", objs, shots=shots[:4], width=400, height=440)
print("DONE")
