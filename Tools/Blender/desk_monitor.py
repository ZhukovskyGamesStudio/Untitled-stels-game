"""A desk computer's screen, keyboard and mouse: what stands on a scanned desk that has none of its own (the realistic family's
metal desk, AssetBuilder.Families; the screen of our own desk was left hanging over it in the air).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/desk_monitor.py -- Assets/_Game/Art/Models
      (lands in <root>/Props/desk_monitor.fbx)

Origin on the desk's top under the screen's foot; the screen faces Blender -Y (the side one sits at). Parts: Body (the
casing, foot, keyboard, mouse), EyeScreen (the picture: UV 0..1 over it).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

W, H = 0.56, 0.34        # the casing; the picture is 1.6 cm smaller on each side (the chin 2.6 cm)
MID = 0.31               # the casing's middle over the desk


def build():
    s.clear_scene()
    bms = [s.rbox((W, 0.03, H), (0, 0, MID), r=0.008, seg=2),                                   # the casing
           s.rbox((W * 0.6, 0.03, H * 0.55), (0, 0.022, MID), r=0.012, seg=2, taper=(0.92, 0.92)),   # its back
           s.rbox((0.06, 0.022, 0.26), (0, 0.045, 0.14), r=0.006, seg=2),                        # the neck
           s.rbox((0.23, 0.18, 0.014), (0, 0.03, 0.006), r=0.006, seg=2),                        # the foot
           s.rbox((0.44, 0.135, 0.018), (-0.03, -0.24, 0.008), r=0.005, seg=2),                  # the keyboard
           s.rbox((0.062, 0.105, 0.03), (0.28, -0.235, 0.011), r=0.011, seg=3)]                   # the mouse
    for row in range(5):                                                                          # its keys, in rows
        for k in range(14):
            bms.append(s.rbox((0.0245, 0.0195, 0.01), (-0.03 - 0.19 + 0.0292 * k, -0.288 + 0.0238 * row, 0.0185), r=0.002, seg=1))
    s.part("Body", bms)
    picture = s.part("EyeScreen", [s.rbox((W - 0.03, 0.004, H - 0.042), (0, -0.0145, MID + 0.005), r=0.0005, seg=1)])
    uv = picture.data.uv_layers.active.data
    for loop in picture.data.loops:
        v = picture.data.vertices[loop.vertex_index].co
        uv[loop.index].uv = (v.x / (W - 0.03) + 0.5, (v.z - MID - 0.005) / (H - 0.042) + 0.5)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
    out = os.path.join(root, "Props")
    os.makedirs(out, exist_ok=True)
    build()
    import bpy
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    path = os.path.join(out, "desk_monitor.fbx")
    meshfix.fix_objects(objs, floor=True, name="desk_monitor")
    s.export_fbx(path, objs)
    print("exported", path)


main()
