"""The two floor hatches of a house, modelled (they were built from raw cubes in the asset builder): the shaft's lid over
the way down to the basement (Level/ShaftHatch) and the exit hatch (Level/Hatch), in one look — a yellow steel angle
frame lying on the floor round the opening, countersunk bolts in its flange, and a lid of tread plate with stiffening
ribs, a recessed lifting bar at its free edge and hinge knuckles along the other.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/hatches.py -- Assets/_Game/Art/Models

Writes Props/hatch_frame_<kind>.fbx and Props/hatch_lid_<kind>.fbx for kind = shaft and exit (both over a shaft with a
ladder, opening 1.5 m: 3 x 3 plan cells, Basement.ShaftHalf). The frame: origin in the opening's middle on the floor; parts Frame (yellow),
Bolts (dark), and for the exit EyeLamp (four corner lamps, the hatch's colour). The lid:
origin on its hinge line, the plate running from it along -Y (Unity +Z) — the builder hangs it on a hinge transform;
parts Lid, Ribs, Handle, Knuckles. Both faces of the frame's lip and the lid's underside are closed (seen from the
basement's ladder looking up).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rooms2_special as rs  # noqa: E402  (export(): meshfix, the FBX into <root>/<category>/)
import shapes as s  # noqa: E402

KINDS = {
    # half the opening, the frame's flange width, the lid's overlap on the lip
    "shaft": (0.75, 0.12, 0.03),
    "exit": (0.75, 0.12, 0.03),   # the same shaft (3 x 3 plan cells) down to the passage in the sewer
}
FLANGE_T = 0.012          # the flange's thickness on the floor
LIP_H = 0.024             # the inner lip's top over the floor (the lid rests on it)
LID_T = 0.035             # the lid plate


def frame(kind):
    half, bar, lip = KINDS[kind]
    out = half + bar
    parts = []
    # the flange: four plates on the floor, mitred by overlapping only in the corners' own square (no shared faces)
    parts.append(s.rbox((2 * out, bar, FLANGE_T), (0, -(half + bar / 2), FLANGE_T / 2), r=0.004, seg=1))
    parts.append(s.rbox((2 * out, bar, FLANGE_T), (0, half + bar / 2, FLANGE_T / 2), r=0.004, seg=1))
    parts.append(s.rbox((bar, 2 * half - 0.002, FLANGE_T), (-(half + bar / 2), 0, FLANGE_T / 2), r=0.004, seg=1))
    parts.append(s.rbox((bar, 2 * half - 0.002, FLANGE_T), (half + bar / 2, 0, FLANGE_T / 2), r=0.004, seg=1))
    # the inner lip standing round the opening (the lid rests on it), a little proud of the flange
    t = 0.022
    parts.append(s.rbox((2 * half + 2 * t, t, LIP_H), (0, -(half + t / 2), LIP_H / 2), r=0.003, seg=1))
    parts.append(s.rbox((2 * half + 2 * t, t, LIP_H), (0, half + t / 2, LIP_H / 2), r=0.003, seg=1))
    parts.append(s.rbox((t, 2 * half - 0.002, LIP_H - 0.001), (-(half + t / 2), 0, (LIP_H - 0.001) / 2), r=0.003, seg=1))
    parts.append(s.rbox((t, 2 * half - 0.002, LIP_H - 0.001), (half + t / 2, 0, (LIP_H - 0.001) / 2), r=0.003, seg=1))
    s.part("Frame", parts)
    bolts = []
    n = 4 if kind == "shaft" else 3
    for k in range(n):
        u = -half + (k + 0.5) * (2 * half / n)
        for x, y in ((u, -(half + bar * 0.6)), (u, half + bar * 0.6), (-(half + bar * 0.6), u), (half + bar * 0.6, u)):
            bolts.append(s.rcyl(0.012, 0.004, (x, y, FLANGE_T + 0.001), seg=10, r=0.0015, bevel_seg=1))
    s.part("Bolts", bolts)
    if kind == "exit":
        lamps = []
        for x in (-1, 1):
            for y in (-1, 1):
                lamps.append(s.rbox((0.06, 0.06, 0.008), (x * (half + bar / 2), y * (half + bar / 2), FLANGE_T + 0.003), r=0.01, seg=2))
        s.part("EyeLamp", lamps)
    return rs.export(f"hatch_frame_{kind}")


def lid(kind):
    half, bar, lip = KINDS[kind]
    side = 2 * half + 2 * lip
    # the plate, its hinge edge on y = 0, running to -side
    s.part("Lid", [s.rbox((side, side, LID_T), (0, -side / 2, LID_T / 2), r=0.006, seg=2)])
    ribs = []
    for i in range(5):                                       # tread ribs across the plate (diagonal tread reads as noise)
        y = -0.18 - i * (side - 0.36) / 4
        ribs.append(s.rbox((side - 0.16, 0.035, 0.012), (0, y, LID_T + 0.005), r=0.004, seg=1))
    s.part("Ribs", ribs)
    # the lifting bar in a recess at the free edge (a pocket plate under it, the bar across on two posts)
    hy = -side + 0.1
    handle = [s.rbox((0.34, 0.07, 0.006), (0, hy, LID_T + 0.002), r=0.003, seg=1),
              s.rcyl(0.011, 0.26, (0, hy, LID_T + 0.03), axis="X", seg=12, r=0.003)]
    for x in (-0.12, 0.12):
        handle.append(s.rcyl(0.009, 0.03, (x, hy, LID_T + 0.016), seg=10, r=0.002))
    s.part("Handle", handle)
    knuckles = [s.rcyl(0.018, 0.14, (x, 0.0, LID_T / 2), axis="X", seg=14, r=0.003) for x in (-half + 0.18, half - 0.18)]
    s.part("Knuckles", knuckles)
    return rs.export(f"hatch_lid_{kind}")


if __name__ == "__main__":
    s.clear_scene()
    for k in KINDS:
        frame(k)
        lid(k)
    print("DONE")
