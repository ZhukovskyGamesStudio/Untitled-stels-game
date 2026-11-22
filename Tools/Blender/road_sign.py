"""The stand of a sewer route sign (SewerLocation: one per tunnel mouth): a road sign's board on two galvanised posts.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/road_sign.py -- Assets/_Game/Art/Models
      (lands in <root>/Props/route_sign_stand.fbx)

Not settled like the items: the model's origin is the floor under the middle of the board. The board's face — where the
route panel lies (a world-space canvas, RouteSignView, 2.45 x 0.45 m, its middle 1.75 m up) — is at Blender y = 0 and
faces Blender +Y, which the import turns into Unity −Z: the side a canvas is read from. Everything else is behind it
(Blender −Y = Unity +Z). Parts: Body (galvanised posts, rails, clamps, base plates), Accent (the board's painted back and
edges).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir, category())
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

FACE_Y = 1.75          # the canvas's middle over the floor
BOARD_W, BOARD_H, BOARD_T = 2.56, 0.56, 0.03
POST_X, POST_R = 0.95, 0.032


def build():
    s.clear_scene()
    # the board just behind the canvas (Unity +Z = Blender −Y): the panel's face is at y = 0
    board = s.rbox((BOARD_W, BOARD_T, BOARD_H), (0, -0.006 - BOARD_T / 2, FACE_Y), r=0.012, seg=2)
    rim = s.rbox((BOARD_W + 0.02, 0.012, BOARD_H + 0.02), (0, -0.03, FACE_Y), r=0.008, seg=2)  # a folded edge round the back
    s.part("Accent", [board, rim])
    top = FACE_Y + BOARD_H / 2 + 0.03
    post_y = -0.075
    body = []
    for x in (-POST_X, POST_X):
        body.append(s.rcyl(POST_R, top, (x, post_y, top / 2), seg=20, r=0.004))
        body.append(s.rcyl(POST_R + 0.006, 0.012, (x, post_y, top + 0.004), seg=20, r=0.003))        # the cap
        body.append(s.rbox((0.2, 0.2, 0.012), (x, post_y, 0.006), r=0.004, seg=1))                     # base plate
        for z in (FACE_Y - 0.17, FACE_Y + 0.17):
            body.append(s.rbox((0.09, 0.05, 0.035), (x, post_y + 0.025, z), r=0.006, seg=1))           # the clamp
    for z in (FACE_Y - 0.17, FACE_Y + 0.17):
        body.append(s.rbox((BOARD_W - 0.18, 0.02, 0.045), (0, -0.046, z), r=0.004, seg=1))             # back rail
    s.part("Body", body)
    import bpy
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name="route_sign_stand")
    path = os.path.join(g.out_dir, g.category("route_sign_stand"), "route_sign_stand.fbx")
    s.export_fbx(path, objs)
    print("wrote", path)


if __name__ == "__main__":
    build()
    print("DONE")
