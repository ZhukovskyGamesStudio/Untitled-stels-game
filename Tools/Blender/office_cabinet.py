"""The office drawer cabinet (PropKind.Cabinet), 0.9 x 0.5 x 1.19: folded sheet steel — an open-fronted carcass with a lipped top, three
drawers that slide out on runners, each with a pressed front, a recessed cup pull and a framed label.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/office_cabinet.py -- Assets/_Game/Art/Models

Parts: Body (the carcass), BodyDrawer0..2 (a tray with its front: the game moves them, Items.Storage), AccentDrawer0..2 (pulls and
label frames: they go with their drawer), PrintDrawer0..2 (the labels). One model for every style.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import machines as mc  # noqa: E402
import shapes as s  # noqa: E402

H, P = 1.19, 0.37                                  # height, drawer pitch


def build():
    s.clear_scene()
    body = [s.rbox((0.022, 0.485, H - 0.02), (x * 0.439, 0.0, (H - 0.02) / 2), r=0.004, seg=2) for x in (-1, 1)]
    body += [s.rbox((0.9, 0.5, 0.026), (0, 0, H - 0.013), r=0.007, seg=3),                         # the top, its rolled edge
             s.rbox((0.86, 0.46, 0.06), (0, 0.012, 0.03), r=0.004, seg=1),                         # the plinth, set back
             s.rbox((0.87, 0.018, H - 0.03), (0, 0.238, (H - 0.03) / 2 + 0.01), r=0.003, seg=1)]
    body += [s.rbox((0.86, 0.46, 0.018), (0, 0.008, 0.06 + k * P), r=0.003, seg=1) for k in range(1, 3)]
    body += [s.rbox((0.012, 0.02, H - 0.1), (x * 0.424, -0.23, H / 2), r=0.003, seg=1) for x in (-1, 1)]   # the jambs
    s.part("Body", body)
    for k in range(3):
        z0 = 0.07 + k * P
        h = P - 0.03
        mid = z0 + h / 2
        front = s.rbox((0.862, 0.024, h), (0, -0.24, mid), r=0.006, seg=3)
        s.inset_faces(front, s.facing((0, -1, 0), 0.1), thickness=0.028, depth=-0.005)
        front = s.boolean(front, s.rbox((0.17, 0.03, 0.05), (0, -0.246, mid + 0.075), r=0.008, seg=2))      # the cup of the pull
        s.part(f"BodyDrawer{k}", [front,
                                  s.rbox((0.8, 0.4, 0.015), (0, -0.03, z0 + 0.02), r=0.002, seg=1),
                                  *[s.rbox((0.015, 0.4, 0.19), (x * 0.4, -0.03, z0 + 0.105), r=0.002, seg=1) for x in (-1, 1)],
                                  s.rbox((0.8, 0.015, 0.19), (0, 0.17, z0 + 0.105), r=0.002, seg=1),
                                  s.rbox((0.19, 0.012, 0.07), (0, -0.226, mid + 0.075), r=0.002, seg=1)])
        acc = [s.rbox((0.19, 0.03, 0.018), (0, -0.254, mid + 0.104), r=0.007, seg=3),                 # the lip the fingers go behind
               s.rbox((0.19, 0.008, 0.01), (0, -0.249, mid + 0.047), r=0.003, seg=1)]
        acc += [s.rbox((0.01, 0.01, 0.066), (x * 0.091, -0.249, mid + 0.075), r=0.003, seg=1) for x in (-1, 1)]
        lz = mid - 0.03
        acc += [s.rbox((0.11, 0.008, 0.007), (0, -0.249, lz + t * 0.026), r=0.002, seg=1) for t in (-1, 1)]
        acc += [s.rbox((0.007, 0.008, 0.06), (t * 0.0545, -0.249, lz), r=0.002, seg=1) for t in (-1, 1)]
        s.part(f"AccentDrawer{k}", acc)
        mc.plates(f"PrintDrawer{k}", [mc.Plate("boxlabel" if k % 2 else "boxlabel2", (0, -0.2485, lz), 0.1, 0.048, thick=0.005, rest="white")])
    mc.export("cabinet")


if __name__ == "__main__":
    build()
