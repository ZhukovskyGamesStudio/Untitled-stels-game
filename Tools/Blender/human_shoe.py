"""A quick look at the rigged humans' shoes alone (seconds): the painted sculpt of each kind — sneaker, boot, flat —
from the outside, the inside, the front, the back, above and three quarters.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/human_shoe.py -- <out.png>

Writes <out>_<kind>.png.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0])
sys.argv = sys.argv[:1]                         # (human.py reads the arguments when it is imported)
import human  # noqa: E402
from humanlib import build, clothes, sdf, view  # noqa: E402

bpy.ops.wm.read_factory_settings(use_empty=True)
c = (0.105, -0.055, 0.07)
shots = [("outside", (1, 0, 0.1), c, 0.34), ("inside", (-1, 0, 0.1), c, 0.34), ("front", (0, -1, 0.15), c, 0.34), ("back", (0, 1, 0.15), c, 0.34),
         ("top", (0, 0, 1), c, 0.34), ("3/4", (0.7, -0.8, 0.5), c, 0.34)]
base = os.path.splitext(OUT)[0]
for who, kind in (("backend", "sneaker"), ("sysadmin", "boot"), ("qa", "flat")):
    fit = human.OUTFITS[who]
    shoe = human.sculpt("shoe." + kind, lambda p: clothes.shoe(p, kind), (0.03, -0.21, -0.004), (0.175, 0.12, 0.22), 0.0018)
    build.paint(shoe, *human.paint_shoe(sdf.points_of(shoe), fit))
    view.sheet(f"{base}_{kind}.png", [shoe], shots=shots, width=380, height=300, color="VERTEX")
    shoe.hide_render = True
print("DONE")
