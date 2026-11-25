"""The hanger of a sewer route sign (SewerLocation: one per tunnel, a little inside it): a road sign's board hung on two
galvanised rods from the tunnel's vault (concept: Logs/comfy/upgrades/sign_601.png).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/road_sign.py -- Assets/_Game/Art/Models
      (lands in <root>/Props/route_sign_stand.fbx — the old standing sign's file, so the prefab keeps its GUID)

Not settled like the items: the model's origin is the floor (the road) under the middle of the board. The board's face —
where the route panel lies (a world-space canvas, RouteSignView, 2.45 x 0.45 m, its middle FACE_Y up: SewerLocation.SignSpot)
— is at Blender y = 0 and faces Blender +Y, which the import turns into Unity −Z: the side a canvas is read from.
Everything else is behind it (Blender −Y = Unity +Z). The rods end in the branch tunnel's vault (sewer.py KIT_BRANCH:
half width 2.3, walls 2.6, rise 1.2) with a ceiling plate each. Parts: Body (galvanised rods, rails, clamps, plates,
turnbuckles), Accent (the board's painted back and edges).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (out_dir, category())
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

FACE_Y = 2.75          # the canvas's middle over the road (SewerLocation.SignSpot.y)
BOARD_W, BOARD_H, BOARD_T = 2.56, 0.56, 0.03
POST_X, POST_R = 0.95, 0.018
VAULT_HALF, VAULT_WALL, VAULT_RISE = 2.3, 2.6, 1.2


def vault_at(x):
    """The branch tunnel's vault over the road at x across (elliptic arch over the walls)."""
    return VAULT_WALL + VAULT_RISE * math.sqrt(max(0.0, 1.0 - (x / VAULT_HALF) ** 2))


def build():
    s.clear_scene()
    # the board just behind the canvas (Unity +Z = Blender −Y): the panel's face is at y = 0
    board = s.rbox((BOARD_W, BOARD_T, BOARD_H), (0, -0.006 - BOARD_T / 2, FACE_Y), r=0.012, seg=2)
    rim = s.rbox((BOARD_W + 0.02, 0.012, BOARD_H + 0.02), (0, -0.03, FACE_Y), r=0.008, seg=2)  # a folded edge round the back
    s.part("Accent", [board, rim])
    low = FACE_Y - 0.17 - 0.03
    post_y = -0.075
    body = []
    for x in (-POST_X, POST_X):
        top = vault_at(x) + 0.02                                                                         # into the brick
        body.append(s.rcyl(POST_R, top - low, (x, post_y, (top + low) / 2), seg=16, r=0.003))
        body.append(s.rcyl(POST_R + 0.004, 0.012, (x, post_y, low - 0.004), seg=16, r=0.003))           # the rod's end cap
        body.append(s.rbox((0.14, 0.14, 0.014), (x, post_y, top - 0.03), r=0.004, seg=1,
                           rot=(0, math.atan(VAULT_RISE * x / (VAULT_HALF ** 2 * math.sqrt(max(1e-4, 1 - (x / VAULT_HALF) ** 2)))), 0)))   # ceiling plate, along the vault
        tb = FACE_Y + BOARD_H / 2 + 0.22                                                                 # a turnbuckle half way up
        body.append(s.rbox((0.03, 0.03, 0.12), (x, post_y, tb), r=0.008, seg=1))
        for z in (FACE_Y - 0.17, FACE_Y + 0.17):
            body.append(s.rbox((0.09, 0.05, 0.035), (x, post_y + 0.025, z), r=0.006, seg=1))           # the clamp
    for z in (FACE_Y - 0.17, FACE_Y + 0.17):
        body.append(s.rbox((BOARD_W - 0.18, 0.02, 0.045), (0, -0.046, z), r=0.004, seg=1))             # back rail
    s.part("Body", body)
    import bpy
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=False, name="route_sign_stand")  # (it hangs: nothing stands on the floor)
    path = os.path.join(g.out_dir, g.category("route_sign_stand"), "route_sign_stand.fbx")
    s.export_fbx(path, objs)
    print("wrote", path)


if __name__ == "__main__":
    build()
    print("DONE")
