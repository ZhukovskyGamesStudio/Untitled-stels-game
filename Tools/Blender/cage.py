"""The hostage's cage, 2 x 2 x 2.2 m (prefab Props/Cage; HouseFeatures.CageSize), modelled — it was raw cubes:

- cage_base: a steel floor plate with a rolled edge and rivets, four round corner posts with caps and feet, a top
  frame with cross bars, round bars on the back and both sides held by a band at mid height (Body = steel, Accent =
  the dark top frame and bands);
- cage_door: the front's bars with their bands and a padlock on a hasp (Body steel, EyeLock = the lock's lamp: red
  while locked);
- cage_hostage: the tied-up person sitting inside against the back bars (rooms2_special.hostage, as in the storybook cage).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/cage.py -- Assets/_Game/Art/Models

The door faces -Y (Unity +Z); the import mirrors X, so the lock drawn at -X lands at Unity +X (where the builder had it).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import rooms2_special as rs  # noqa: E402  (the tied-up hostage, shared with the storybook cage)
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
HALF, H = 1.0, 2.2
STEP = 0.18
R_BAR = 0.015


def export(name):
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=True, name=name)
    s.export_fbx(os.path.join(ROOT, "Props", name + ".fbx"), objs)
    print("exported", name)
    s.clear_scene()


def bar_positions():
    n = int((2 * HALF - 0.2) // STEP)
    return [-HALF + 0.1 + i * (2 * HALF - 0.2) / n for i in range(n + 1)]


def bar(x, y):
    return s.rcyl(R_BAR, H - 0.1, (x, y, 0.05 + (H - 0.1) / 2), seg=10, r=0.004)


def band(axis_len, center, along_x):
    size = (axis_len, 0.04, 0.05) if along_x else (0.04, axis_len, 0.05)
    return s.rbox(size, center, r=0.008, seg=1)


def base():
    e = HALF - 0.04
    body = [s.rbox((2 * HALF, 2 * HALF, 0.06), (0, 0, 0.03), r=0.02, seg=2)]
    for x in (-1, 1):
        for y in (-1, 1):
            body.append(s.rcyl(0.045, H, (x * e, y * e, H / 2), seg=16, r=0.006))           # corner posts
            body.append(s.rcyl(0.06, 0.04, (x * e, y * e, 0.08), seg=16, r=0.008))          # their feet
            body.append(s.rcyl(0.055, 0.03, (x * e, y * e, H + 0.04), seg=16, r=0.01))      # caps
    for t in bar_positions():
        body.append(bar(t, e))                       # back (+Y: Unity -Z)
        body.append(bar(-e, t))                      # sides
        body.append(bar(e, t))
    rivets = [s.rcyl(0.012, 0.006, (x, y, 0.063), seg=8, r=0.002) for x in (-0.8, -0.27, 0.27, 0.8) for y in (-0.8, -0.27, 0.27, 0.8)]
    s.part("Body", body + rivets)
    accent = [band(2 * HALF - 0.04, (0, e, H), True), band(2 * HALF - 0.04, (0, -e, H), True),
              band(2 * HALF - 0.04, (-e, 0, H), False), band(2 * HALF - 0.04, (e, 0, H), False)]
    for k in (-0.5, 0.0, 0.5):                       # cross bars over the top
        accent.append(s.rcyl(0.012, 2 * HALF - 0.1, (k, 0, H + 0.005), axis="Y", seg=8, r=0.003))
    for z in (0.12, 1.1):                            # bands holding the bars, at the foot and mid height
        accent += [band(2 * HALF - 0.1, (0, e, z), True), band(2 * HALF - 0.1, (-e, 0, z), False), band(2 * HALF - 0.1, (e, 0, z), False)]
    s.part("Accent", accent)
    export("cage_base")


def door():
    e = HALF - 0.04
    parts = [bar(t, -e) for t in bar_positions()]
    for z in (0.12, 1.1, H - 0.02):
        parts.append(band(2 * HALF - 0.1, (0, -e, z), True))
    lx = -(HALF - 0.35)                              # (Unity +X once imported)
    parts.append(s.rbox((0.2, 0.025, 0.06), (lx, -e - 0.03, 1.18), r=0.006, seg=1))                    # the hasp
    s.part("Body", parts)
    lock = [s.rbox((0.12, 0.05, 0.12), (lx, -e - 0.06, 1.08), r=0.02, seg=2)]
    shackle = s.tube([(lx - 0.035, -e - 0.06, 1.13), (lx - 0.035, -e - 0.06, 1.19), (lx, -e - 0.06, 1.22),
                      (lx + 0.035, -e - 0.06, 1.19), (lx + 0.035, -e - 0.06, 1.13)], 0.008, res=2, segments_per=4)
    s.part("EyeLock", lock + [shackle])
    export("cage_door")


def hostage():
    """cage_hostage: the tied-up hostage sitting on the floor plate (0.06 m) against the back bars."""
    rs.hostage(0.06, hip_y=0.6, back_y=0.73)
    export("cage_hostage")


if __name__ == "__main__":
    s.clear_scene()
    base()
    door()
    hostage()
    print("DONE")
