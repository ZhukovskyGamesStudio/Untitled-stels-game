"""The humans' takes on the bare skeleton, no meshes (seconds): where the soles are in every take, when a foot is down and
which way it moves then — a planted foot goes back under the body, never forward, and no sole is under the floor.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/human_takes.py -- [Take ...] [frames]

A line per take: the lowest sole over the take (cm over the floor), the same after the game plants the feet by the ankle
alone (what HumanRig did before it looked at the ball of the foot), and for each foot the frames it is down in, how many of
them it moves back / forward in and its mean speed (metres a second: the speed the game plays the take for —
HumanMath.WalkTakeSpeed, RunTakeSpeed, CrouchTakeSpeed). `frames` also prints heel, ball, tip and ankle frame by frame.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from humanlib import anim

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

bpy.ops.wm.read_factory_settings(use_empty=True)
rig = anim.skeleton()
acts = anim.animate(rig)
only = [a for a in args if a != "frames"]
verbose = "frames" in args
sc = bpy.context.scene
fps = anim.FPS
rest = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in rig.data.bones}
ANKLE = rest["foot.L"][0].z


def M(b):
    return rig.matrix_world @ rig.pose.bones[b].matrix @ rig.data.bones[b].matrix_local.inverted()


def soles(sd):
    """Heel, ball and tip of the sole in the world: rigid with the foot (heel, ball) and the toe (tip)."""
    a, ball = rest[f"foot.{sd}"]
    tip = rest[f"toe.{sd}"][1]
    mf, mt = M(f"foot.{sd}"), M(f"toe.{sd}")
    return (mf @ Vector((a.x, a.y + 0.06, 0.0)), mf @ Vector((ball.x, ball.y, 0.0)), mt @ Vector((tip.x, tip.y, 0.0)))


for act in acts:
    name = act.name
    if only and name not in only:
        continue
    rig.animation_data.action = act
    if getattr(act, "slots", None):
        rig.animation_data.action_slot = act.slots[0]
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    rows = []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        r = {"f": f}
        for sd in "LR":
            h, b, t = soles(sd)
            r[sd] = (min(h.z, b.z, t.z), h.z, b.z, t.z, (M(f"foot.{sd}") @ rest[f"foot.{sd}"][0]).copy(), b.copy())
        rows.append(r)
    low = [min(r["L"][0], r["R"][0]) for r in rows]
    # the game's PlantFeet: the lower ankle goes to the standing ankle's height (within -0.15 .. +0.08)
    planted = []
    for r in rows:
        shift = max(-0.15, min(0.08, ANKLE - min(r["L"][4].z, r["R"][4].z)))
        planted.append(min(r["L"][0], r["R"][0]) + shift)
    # a foot is down while its sole is within 1.2 cm of the take's lowest point at that frame's ground (0)
    out = []
    for sd in "LR":
        down = [r[sd][0] < 0.012 for r in rows]
        vy = [(rows[i][sd][5].y - rows[i - 1][sd][5].y) * fps for i in range(1, len(rows)) if down[i] and down[i - 1]]
        spans, start = [], None
        for i, d in enumerate(down + [False]):
            if d and start is None:
                start = i
            if not d and start is not None:
                spans.append(f"{rows[start]['f']}-{rows[i - 1]['f']}")
                start = None
        back = sum(1 for v in vy if v > 0.05)
        fwd = sum(1 for v in vy if v < -0.05)
        mean = sum(vy) / len(vy) if vy else 0.0
        out.append(f"{sd} down {','.join(spans) or '-'} moving back {back} fwd {fwd} mean {mean:+.2f} m/s")
    print(f"TAKE {name:10s} {f0}-{f1} | lowest sole {min(low) * 100:+5.1f}..{max(low) * 100:+5.1f} cm | in game (planted) {min(planted) * 100:+5.1f}..{max(planted) * 100:+5.1f} cm | {out[0]} | {out[1]}")
    if verbose:
        for r in rows[::max(1, len(rows) // 16)]:
            print(f"   f{r['f']:3d} " + " | ".join(f"{sd} heel {r[sd][1] * 100:+5.1f} ball {r[sd][2] * 100:+5.1f} tip {r[sd][3] * 100:+5.1f} ankle y{r[sd][4].y * 100:+6.1f} z{r[sd][4].z * 100:5.1f}" for sd in "LR"))
