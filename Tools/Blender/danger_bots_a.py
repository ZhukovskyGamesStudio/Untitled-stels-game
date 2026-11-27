"""New dangerous robots, set A: warden (вахтёр), repairman (ремонтник), smoker (дымогенератор), relay (ретранслятор),
nurse (медсестра). Base models only (no per-style bestiary variants yet), built on shapes.py like guard_mk2.py.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/danger_bots_a.py -- <models_root> [names]
      names: robot_warden robot_repairman robot_smoker robot_relay robot_nurse (none = all)
Writes <models_root>/Robots/<name>.fbx — <models_root> is Assets/_Game/Art/Models for the game (AssetBuilder.Model finds
the file by name anywhere under it), a scratch folder while iterating.

Concepts (ComfyUI, Logs/comfy/danger/): warden_8892793 (peaked cap, barrel body, padlock on one arm, keys at the belt),
repairman_2315331 (toolbox with a handle and a wrench arm; here on four legs), smoker_10997097 (round boiler with a gauge,
chimneys, a hose), relay_1081948 (weighted dome base, mast, horn speakers round a hub, dish on top), nurse_16106153
(white skirted column, tapered torso, round head, nurse cap with a cross, syringe).

Part contract (AssetBuilder.Paint / RobotAnimator / ProceduralLegs): Body* body colour, Accent* accent colour, Eye*
glowing parts; AccentWheel{n} spin about X round their bounds' centre; AccentLeg{n}Upper / AccentLeg{n}Lower are a thigh
(hip to knee) and a shin (knee to foot) that ProceduralLegs plants. Blender x right, -y front (Unity +Z), z up; the pivot
is on the floor under the centre.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for p in (HERE, os.path.join(HERE, "bestiary"), os.path.join(HERE, "creatures")):
    if p not in sys.path:
        sys.path.insert(0, p)

import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

import meshfix  # noqa: E402
import shapes as s  # noqa: E402
from common import Creature  # noqa: E402
from lang import align  # noqa: E402

L = Creature()
V = Vector
MOVING = ("AccentWheel", "AccentLeg", "AccentHead", "AccentRotor")


# --------------------------------------------------------------------------- helpers

def torus(R, r, loc=(0, 0, 0), axis="Z", seg=24, rseg=8, arc=None):
    """A ring (closed tube) of radius R and thickness r round `axis` through loc; arc = (a0, a1) radians keeps only
    that part of the ring (capped)."""
    bm = bmesh.new()
    full = arc is None
    a0, a1 = (0.0, math.tau) if full else arc
    n = seg if full else max(2, int(seg * (a1 - a0) / math.tau)) + 1
    rings = []
    for i in range(n):
        a = a0 + (a1 - a0) * i / (seg if full else n - 1)
        c, sn = math.cos(a), math.sin(a)
        ring = []
        for j in range(rseg):
            b = math.tau * j / rseg
            rr = R + r * math.cos(b)
            ring.append(bm.verts.new((rr * c, rr * sn, r * math.sin(b))))
        rings.append(ring)
    pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if full else [])
    for lo, up in pairs:
        for j in range(rseg):
            bm.faces.new((lo[j], lo[(j + 1) % rseg], up[(j + 1) % rseg], up[j]))
    if not full:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


def rod(p0, p1, r, seg=12, bevel=None):
    """A chamfered cylinder from p0 to p1."""
    a, b = V(p0), V(p1)
    bm = s.rcyl(r, (b - a).length, seg=seg, r=bevel if bevel is not None else r * 0.3)
    return align(bm, a, b)


def limb(points, radii, seg=12):
    """A rounded arm / leg segment chain (round sections)."""
    return L.chain(points, radii, n=2.0, seg=seg)


def point(bm, p0, d):
    """Turn a shape built up +Z from z = 0 to face d, its base at p0 (lathes)."""
    q = V((0, 0, 1)).rotation_difference(V(d).normalized())
    bmesh.ops.rotate(bm, cent=V((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    return s.place(bm, V(p0))


def hoop(z0, z1, a, b, out=0.013, n=2.2, y=0.0, seg=32):
    """A band round a lofted body (a, b its half-sizes there): stands out by `out`, chamfered edges, four rings."""
    c = min(0.006, (z1 - z0) * 0.3)
    return s.loft([(z0, a + out - c, b + out - c, n, 0, y), (z0 + c, a + out, b + out, n, 0, y),
                   (z1 - c, a + out, b + out, n, 0, y), (z1, a + out - c, b + out - c, n, 0, y)], seg=seg)


def joint(r, loc, seg=12):
    return [s.ball(r, loc, seg=seg)]


def key(loc, length=0.12, rot=(0, 0, 0), k=1.0):
    """A big cartoon key hanging down from loc (its bow's ring): bow ring, shaft, bit teeth; k scales its thickness."""
    bow = torus(0.026 * k, 0.009 * k, (0, 0, -0.026 * k), axis="Y", seg=10, rseg=4)
    shaft = s.rbox((0.017 * k, 0.012 * k, length), (0, 0, -0.05 * k - length / 2), r=0.004, seg=1)
    bit = s.rbox((0.034 * k, 0.011 * k, 0.04 * k), (0.022 * k, 0, -0.045 * k - length + 0.024 * k), r=0.004, seg=1)
    out = [bow, shaft, bit]
    for bm in out:
        s.place(bm, rot=rot)
        s.place(bm, loc)
    return out


def wheels(centres, radius, width):
    """{name: [bm]} — one AccentWheel{n} per centre (they spin about X round their bounds' centre)."""
    out = {}
    for i, c in enumerate(centres):
        c = V(c)
        tyre = s.lathe([(radius * 0.6, -width / 2), (radius * 0.88, -width / 2), (radius, -width * 0.28),
                        (radius, width * 0.28), (radius * 0.88, width / 2), (radius * 0.6, width / 2)], seg=18, axis="X", loc=c)
        hub = s.rcyl(radius * 0.64, width * 0.84, c, axis="X", seg=12, r=0.005, bevel_seg=1)
        cap = s.rcyl(radius * 0.3, width * 1.06, c, axis="X", seg=8, r=0.004, bevel_seg=1)
        out[f"AccentWheel{i}"] = [tyre, hub, cap]
    return out


# --------------------------------------------------------------------------- warden

def warden():
    """Вахтёр, 1.53 m: a stocky barrel on a wheeled skirt, two accent hoops and a wide belt, a big boxy face under a
    doorman's peaked cap with one camera eye in its shade; the left fist holds a great key ring out in front with a
    padlock and two keys on it, a bunch of keys hangs at the right hip under the right hand. He shuts and locks doors.
    Eye lens centre (0, 1.20, fwd 0.30) in Unity axes."""
    P = {}
    body, acc = [], []
    # skirt with the wheels tucked under it, a rubber bumper round it
    body += [s.loft([(0.1, 0.33, 0.31, 2.4), (0.16, 0.35, 0.33, 2.4), (0.24, 0.33, 0.31, 2.4), (0.3, 0.27, 0.25, 2.4)],
                    seg=32, round_caps=0.02)]
    acc.append(hoop(0.135, 0.185, 0.35, 0.33, out=0.012, n=2.4))
    # barrel torso: belly out, shoulders in, a slight forward lean
    torso = s.loft([(0.26, 0.27, 0.24, 2.2, 0, 0.0), (0.4, 0.315, 0.28, 2.2, 0, -0.005), (0.58, 0.335, 0.3, 2.2, 0, -0.01),
                    (0.78, 0.32, 0.285, 2.2, 0, -0.015), (0.93, 0.28, 0.25, 2.3, 0, -0.02), (1.01, 0.2, 0.18, 2.3, 0, -0.02)],
                   seg=32, round_caps=0.045)
    body.append(torso)
    acc.append(hoop(0.55, 0.64, 0.335, 0.3, y=-0.01))                                          # belt
    body.append(s.rbox((0.15, 0.05, 0.12), (0, -0.322, 0.595), r=0.018, seg=2))                 # buckle
    acc.append(s.rbox((0.08, 0.03, 0.055), (0, -0.345, 0.595), r=0.01, seg=1))
    acc.append(s.rbox((0.11, 0.04, 0.08), (-0.14, -0.282, 0.78), r=0.014, seg=2, rot=(0.12, 0, 0)))  # chest badge
    # head: a big soft box sunk into the barrel's top (no neck)
    hy = -0.04
    head = s.loft([(0.96, 0.19, 0.16, 3.2, 0, hy), (1.02, 0.215, 0.185, 3.4, 0, hy), (1.24, 0.22, 0.19, 3.4, 0, hy),
                   (1.36, 0.21, 0.18, 3.2, 0, hy)], seg=32, round_caps=0.035)
    body.append(head)
    for sx in (-1, 1):                                                                         # ear cans
        acc.append(s.rcyl(0.06, 0.05, (sx * 0.22, hy, 1.15), axis="X", seg=16, r=0.01))
    # the cap: a tall flared crown wider than the head, a band, a big peak tipped down over the eye, a badge
    cy = hy - 0.01
    acc.append(s.loft([(1.32, 0.225, 0.2, 2.6, 0, cy), (1.38, 0.235, 0.21, 2.6, 0, cy), (1.48, 0.285, 0.265, 2.3, 0, cy),
                       (1.535, 0.28, 0.26, 2.3, 0, cy)], seg=32, round_caps=0.016))
    body.append(s.loft([(1.325, 0.232, 0.207, 2.6, 0, cy), (1.375, 0.24, 0.215, 2.6, 0, cy)], seg=32, round_caps=0.006))
    peak = s.loft([(0, 0.235, 0.34, 2.2), (0.014, 0.24, 0.345, 2.2), (0.03, 0.225, 0.33, 2.2)], seg=32, round_caps=0.008)
    peak = s.boolean(peak, s.rbox((0.6, 0.4, 0.2), (0, 0.2, 0.0), r=0), "DIFFERENCE")             # the front half
    s.bevel_edges(peak, 0.004, 1, angle=50)
    s.place(peak, (0, cy - 0.02, 1.325), rot=(0.25, 0, 0))
    body.append(peak)                                                                          # the visor: dark, not cap-coloured
    body.append(s.rcyl(0.05, 0.03, (0, cy - 0.255, 1.44), axis="Y", seg=16, r=0.008, rot=(0.2, 0, 0)))   # cap badge
    # the camera eye in the peak's shade
    a, g = L.eye(0.068, (0, -0.255, 1.2), depth=0.07)
    acc += a
    P["Eye"] = g
    # arms: shoulder balls, the left forearm raised forward holding the key ring out, the right down on the keys
    for sx in (-1, 1):
        acc += joint(0.088, (sx * 0.31, -0.02, 0.88))
    acc += limb([(-0.33, -0.02, 0.87), (-0.37, -0.08, 0.68)], [0.07, 0.062])
    acc += joint(0.066, (-0.37, -0.08, 0.68))
    acc += limb([(-0.37, -0.08, 0.68), (-0.31, -0.27, 0.8)], [0.062, 0.056])
    body += [s.ball(0.075, (-0.3, -0.31, 0.82), seg=16, scale=(1.0, 1.1, 0.95))]                  # fist
    acc += limb([(0.33, -0.02, 0.87), (0.37, -0.05, 0.67)], [0.07, 0.062])
    acc += joint(0.066, (0.37, -0.05, 0.67))
    acc += limb([(0.37, -0.05, 0.67), (0.34, -0.15, 0.52)], [0.062, 0.056])
    body += [s.ball(0.07, (0.33, -0.18, 0.48), seg=16, scale=(1, 1, 1.1))]
    # the great key ring hanging from the fist (facing front), a big padlock on it, two keys
    R = 0.12
    ring_c = V((-0.3, -0.34, 0.82 - 0.05 - R))
    acc.append(torus(R, 0.02, ring_c, axis="Y", seg=28, rseg=8))
    lock_c = ring_c + V((0.0, 0, -R - 0.15))
    acc.append(torus(0.07, 0.022, lock_c + V((0, 0, 0.07)), axis="Y", seg=14, rseg=8, arc=(0.0, math.pi)))  # shackle
    for sx in (-1, 1):
        acc.append(rod(lock_c + V((sx * 0.07, 0, 0.075)), lock_c + V((sx * 0.07, 0, -0.01)), 0.022, seg=8))
    body.append(s.rbox((0.25, 0.11, 0.2), lock_c + V((0, 0, -0.08)), r=0.035, seg=2))
    acc.append(s.rcyl(0.03, 0.02, lock_c + V((0, -0.055, -0.07)), axis="Y", seg=12, r=0.004, bevel_seg=1))    # keyhole
    acc.append(s.rbox((0.02, 0.02, 0.05), lock_c + V((0, -0.058, -0.11)), r=0.005, seg=1))
    for ang in (-0.8, 0.8):
        acc += key(ring_c + V((R * math.sin(ang), 0, -R * math.cos(ang))), 0.16, rot=(0, -ang * 0.35, 0), k=1.4)
    # bunch of keys at the right hip
    hip = V((0.27, -0.28, 0.52))
    acc.append(torus(0.055, 0.014, hip, axis="Y", seg=18, rseg=6))
    for k, ang in enumerate((-0.5, -0.15, 0.2, 0.55)):
        acc += key(hip + V((0.055 * math.sin(ang), -0.006 * k, -0.055 * math.cos(ang))), 0.13 + 0.02 * (k % 2),
                   rot=(0, -ang, 0), k=1.3)
    P["Body"] = body
    P["Accent"] = acc
    P.update(wheels([(-0.22, -0.17, 0.085), (0.22, -0.17, 0.085), (-0.22, 0.17, 0.085), (0.22, 0.17, 0.085)], 0.085, 0.07))
    return P, {}


# --------------------------------------------------------------------------- repairman

def repairman():
    """Ремонтник, 0.9 m: a toolbox crouching low between four crab legs (knees above the lid), its carry handle on top
    and a hammer stuck in the lid at the back, a small camera head on a stalk at the front left, and a thick tool arm
    on the front right raising a big orange open-ended wrench (with a screwdriver bit under it) above everything.
    Legs AccentLeg0..3 Upper (hip to knee) / Lower (knee to ankle to foot)."""
    P = {}
    body, acc, eye = [], [], []
    z0, z1 = 0.15, 0.39                                                                        # the box's bottom and top
    box = s.rbox((0.5, 0.36, z1 - z0), (0, 0, (z0 + z1) / 2), r=0.035, seg=3)
    s.groove(box, "Z", z1 - 0.06, 0.014, 0.007)                                                # the lid's seam
    body.append(box)
    acc.append(s.rbox((0.52, 0.38, 0.06), (0, 0, z1 + 0.01), r=0.03, seg=3, taper=(0.97, 0.95)))   # lid lip
    for sx in (-1, 1):
        acc.append(s.rbox((0.07, 0.035, 0.07), (sx * 0.13, -0.183, z1 - 0.055), r=0.012, seg=2))   # latches
        body.append(s.rbox((0.03, 0.02, 0.03), (sx * 0.13, -0.2, z1 - 0.065), r=0.006, seg=1))
    body.append(s.rbox((0.3, 0.03, 0.08), (0.05, -0.18, z0 + 0.08), r=0.012, seg=2))           # drawer and its pull
    acc.append(s.rbox((0.12, 0.03, 0.022), (0.05, -0.198, z0 + 0.08), r=0.008, seg=1))
    # the carry handle
    ht = z1 + 0.04
    for sx in (-1, 1):
        acc.append(s.rbox((0.04, 0.05, 0.06), (sx * 0.12, 0.05, ht), r=0.012, seg=2))
    acc.append(s.tube([(-0.12, 0.05, ht + 0.005), (-0.11, 0.05, ht + 0.095), (0.11, 0.05, ht + 0.095),
                       (0.12, 0.05, ht + 0.005)], 0.02, res=2, smooth=True, segments_per=6))
    body.append(s.rcyl(0.026, 0.14, (0, 0.05, ht + 0.095), axis="X", seg=14, r=0.008))         # grip sleeve
    # a hammer stuck in the lid at the back
    body.append(rod((-0.17, 0.13, z1 - 0.02), (-0.19, 0.15, z1 + 0.2), 0.02, seg=10))
    acc.append(s.rbox((0.15, 0.06, 0.06), (-0.19, 0.15, z1 + 0.22), r=0.015, seg=2, rot=(0, 0.15, 0)))
    # camera head on a stalk, front left
    st0, st1 = V((-0.15, -0.1, z1 + 0.02)), V((-0.15, -0.14, z1 + 0.16))
    body.append(rod(st0, st1, 0.022, seg=12))
    acc.append(s.rbox((0.12, 0.1, 0.09), st1 + V((0, -0.01, 0.03)), r=0.025, seg=2))
    a, g = L.eye(0.034, st1 + V((0, -0.075, 0.03)), depth=0.045)
    acc += a
    eye += g
    # the tool arm: a shoulder drum on the lid, thick upper arm up, forearm forward, the big wrench raised
    sh, el, wr = V((0.15, -0.06, z1 + 0.05)), V((0.17, -0.1, 0.72)), V((0.17, -0.27, 0.78))
    acc.append(s.rcyl(0.075, 0.09, sh, axis="X", seg=20, r=0.015))
    body += limb([sh, el], [0.055, 0.048])
    acc += joint(0.058, el)
    body += limb([el, wr], [0.046, 0.04])
    acc += joint(0.048, wr)
    d = V((0, -0.35, 1)).normalized()                                                          # the wrench points up-forward
    shaft_end = wr + d * 0.12
    body.append(align(s.rbox((0.05, 0.03, 0.14), r=0.01, seg=1), wr, shaft_end))
    wrench = s.rcyl(0.085, 0.035, (0, 0, 0), axis="Y", seg=20, r=0.008)
    wrench = s.boolean(wrench, s.rbox((0.09, 0.1, 0.08), (0, 0, 0.065), r=0), "DIFFERENCE")       # open jaw up
    s.bevel_edges(wrench, 0.005, 1, angle=50)
    bmesh.ops.rotate(wrench, cent=V((0, 0, 0)), matrix=V((0, 0, 1)).rotation_difference(d).to_matrix(), verts=wrench.verts)
    s.place(wrench, shaft_end + d * 0.05)
    acc.append(wrench)
    acc.append(rod(wr + V((0, 0, -0.02)), wr + V((0, -0.05, -0.15)), 0.013, seg=8))           # screwdriver bit below
    acc.append(rod(wr + V((0, 0, -0.02)), wr + V((0, -0.017, -0.07)), 0.022, seg=10))
    # legs: hips at the box's side midline, knees raised above the lid, ankles out, round feet
    n = 0
    for sy in (-1, 1):            # front, back
        for sx in (-1, 1):
            hip = V((sx * 0.25, sy * 0.11, 0.27))
            knee = V((sx * 0.3, sy * 0.16, 0.5))
            ankle = V((sx * 0.32, sy * 0.19, 0.22))
            foot = V((sx * 0.29, sy * 0.2, 0.035))
            up = joint(0.058, hip) + limb([hip, knee], [0.05, 0.044]) + joint(0.054, knee)
            low = limb([knee, ankle, foot + V((0, 0, 0.03))], [0.044, 0.038, 0.028]) + joint(0.044, ankle)
            low += [s.lathe([(0.0, 0.0), (0.05, 0.0), (0.054, 0.012), (0.044, 0.034), (0.026, 0.052), (0.0, 0.057)],
                            seg=16, loc=(foot.x, foot.y, 0.0))]
            P[f"AccentLeg{n}Upper"] = up
            P[f"AccentLeg{n}Lower"] = low
            n += 1
    P["Body"], P["Accent"], P["Eye"] = body, acc, eye
    return P, {}


# --------------------------------------------------------------------------- smoker

def smoker():
    """Дымогенератор, 1.1 m: a round boiler on a wheeled chassis, accent hoops, three flared chimney nozzles on the
    dome, a pressure gauge on the front, a compressor box with a grille on the back, hoses from it to the nozzles and a
    valve wheel. No eyes: it senses nothing."""
    P = {}
    body, acc = [], []
    body.append(s.rbox((0.56, 0.5, 0.08), (0, 0, 0.17), r=0.02, seg=2))                          # chassis
    acc.append(s.loft([(0.13, 0.31, 0.3, 2.6), (0.2, 0.34, 0.33, 2.6), (0.24, 0.3, 0.29, 2.6)], seg=32, round_caps=0.015))
    boiler = s.lathe([(0.2, 0.2), (0.3, 0.24), (0.35, 0.32), (0.37, 0.45), (0.37, 0.65), (0.35, 0.76), (0.29, 0.85),
                      (0.19, 0.91), (0.0, 0.935)], seg=32)
    body.append(boiler)
    for z in (0.38, 0.72):
        rr = 0.37 if z < 0.7 else 0.355
        acc.append(torus(rr, 0.022, (0, 0, z), seg=36, rseg=8))
    # chimneys: a tall one in the middle-back, two shorter splayed ones
    def chimney(base, top, r):
        b, t = V(base), V(top)
        d = (t - b).normalized()
        flare = point(s.lathe([(r * 0.9, 0), (r * 1.55, 0.05), (r * 1.7, 0.075), (r * 1.6, 0.09), (r * 1.1, 0.085),
                               (0.0, 0.07)], seg=16), t - d * 0.015, d)
        return [rod(b, t, r, seg=16)], [flare]
    for base, top, r in (((-0.14, -0.02, 0.83), (-0.22, -0.02, 0.97), 0.04), ((0.14, -0.02, 0.83), (0.22, -0.02, 0.97), 0.04)):
        b_, a_ = chimney(base, top, r)
        body += b_
        acc += a_
    # the main stack: tall, out of the back of the dome, bent forward at the top like a smoking pipe
    pts = [(0, 0.12, 0.8), (0, 0.13, 1.0), (0, 0.1, 1.12), (0, 0.02, 1.17)]
    body.append(s.tube(pts, 0.062, res=2, segments_per=8))
    acc.append(s.rcyl(0.08, 0.05, (0, 0.123, 0.9), seg=20, r=0.01))                               # its collar
    end, dirn = V(pts[-1]), (V(pts[-1]) - V(pts[-2])).normalized()
    acc.append(point(s.lathe([(0.055, 0), (0.09, 0.05), (0.1, 0.08), (0.092, 0.095), (0.065, 0.088), (0.0, 0.075)],
                             seg=20), end - dirn * 0.03, dirn))
    # the pressure gauge on the front left (off centre: it must not read as a face)
    gauge = []
    g = V((0.0, -0.38, 0.58))
    ga = [s.lathe([(0.15, 0.0), (0.16, 0.012), (0.16, 0.045), (0.14, 0.056), (0.0, 0.056)], seg=32, axis="-Y",
                  loc=g + V((0, 0.05, 0)))]
    gb = [s.rcyl(0.128, 0.02, g + V((0, -0.006, 0)), axis="Y", seg=32, r=0.003)]                 # dial face
    ga.append(align(s.rbox((0.018, 0.01, 0.11), r=0.004, seg=1), g + V((0, -0.02, -0.01)),
                    g + V((0.07, -0.02, 0.075))))                                                # needle
    ga.append(s.rcyl(0.018, 0.024, g + V((0, -0.02, 0)), axis="Y", seg=10, r=0.004))
    gb.append(rod(g + V((0, 0.03, 0.15)), g + V((0, 0.06, 0.19)), 0.026, seg=10))              # its stem
    for bm in ga + gb:
        s.place(bm, rot=(0, 0, math.radians(-42)))
    acc += ga
    body += gb
    # compressor box on the back with a grille, hoses to the nozzles
    body.append(s.rbox((0.34, 0.13, 0.3), (0, 0.38, 0.47), r=0.03, seg=3))
    acc += s.grille((0, 0.445, 0.47), 0.24, 0.2, n=4, proud=0.012, axis="Y")
    acc.append(s.rbox((0.38, 0.07, 0.04), (0, 0.38, 0.635), r=0.012, seg=2))
    for sx in (-1, 1):
        body.append(s.tube([(sx * 0.13, 0.41, 0.56), (sx * 0.25, 0.35, 0.76), (sx * 0.24, 0.17, 0.93),
                            (sx * 0.18, 0.0, 0.92)], 0.04, res=2, segments_per=8))
    # valve wheel on the front right shoulder
    vc = V((0.2, -0.22, 0.9))
    body.append(rod(vc + V((-0.05, 0.05, -0.1)), vc, 0.03, seg=10))
    acc.append(torus(0.07, 0.013, vc, axis="Z", seg=20, rseg=6))
    acc += [rod(vc + V((-0.07, 0, 0)), vc + V((0.07, 0, 0)), 0.009, seg=6),
            rod(vc + V((0, -0.07, 0)), vc + V((0, 0.07, 0)), 0.009, seg=6)]
    P["Body"], P["Accent"] = body, acc
    P.update(wheels([(-0.27, -0.2, 0.1), (0.27, -0.2, 0.1), (-0.27, 0.2, 0.1), (0.27, 0.2, 0.1)], 0.1, 0.07))
    return P, {}


# --------------------------------------------------------------------------- relay

def relay():
    """Ретранслятор, 1.8 m, static: a domed weighted base with stacked weight plates, a mast with collars, a speaker hub
    with a front speaker and two horn loudspeakers to the sides-back, a small dish and a whip antenna on top, cables
    down the mast to a junction box. Eye: speaker rims, the membranes and the indicator lamps."""
    P = {}
    body, acc, eye = [], [], []
    body.append(s.lathe([(0.34, 0.0), (0.345, 0.02), (0.34, 0.04), (0.0, 0.041)], seg=36))      # foot ring
    acc.append(s.lathe([(0.31, 0.035), (0.3, 0.1), (0.26, 0.15), (0.17, 0.19), (0.07, 0.205), (0.0, 0.207)], seg=36))
    for z, r in ((0.2, 0.15), (0.245, 0.13)):                                                   # weight plates
        body.append(s.rcyl(r, 0.05, (0, 0, z), seg=28, r=0.01))
    for k in range(4):                                                                         # base bolts
        a = math.tau * (k + 0.5) / 4
        body.append(s.rcyl(0.022, 0.03, (0.3 * math.cos(a), 0.3 * math.sin(a), 0.045), seg=10, r=0.005))
    # the mast with collars
    body.append(s.rcyl(0.065, 1.2, (0, 0, 0.86), seg=18, r=0.008))
    for z in (0.28, 1.12):
        acc.append(s.rcyl(0.088, 0.05, (0, 0, z), seg=20, r=0.01))
    # the amplifier cabinet clamped round the mast at mid height, its lamps and a grille in front
    body.append(s.rbox((0.26, 0.2, 0.34), (0, -0.02, 0.68), r=0.03, seg=3, taper=(0.92, 0.92)))
    acc.append(s.rbox((0.28, 0.22, 0.04), (0, -0.02, 0.86), r=0.012, seg=2))
    acc.append(s.rbox((0.28, 0.22, 0.04), (0, -0.02, 0.5), r=0.012, seg=2))
    acc += s.grille((0, -0.122, 0.64), 0.18, 0.16, n=4, proud=0.012, axis="-Y")
    eye += [s.ball(0.02, (x, -0.118, 0.78), seg=10) for x in (-0.06, 0.0, 0.06)]
    # the hub: a squat drum
    hub_z = 1.33
    body.append(s.lathe([(0.0, hub_z - 0.16), (0.1, hub_z - 0.155), (0.16, hub_z - 0.11), (0.175, hub_z - 0.03),
                         (0.175, hub_z + 0.04), (0.15, hub_z + 0.11), (0.08, hub_z + 0.15), (0.0, hub_z + 0.155)], seg=32))
    acc.append(torus(0.172, 0.014, (0, 0, hub_z + 0.005), seg=32, rseg=8))
    # front speaker in a square bezel
    acc.append(s.rbox((0.17, 0.06, 0.17), (0, -0.15, hub_z), r=0.025, seg=2))
    body.append(s.lathe([(0.065, 0.0), (0.06, 0.02), (0.03, 0.035), (0.0, 0.037)], seg=24, axis="-Y",
                        loc=(0, -0.18, hub_z)))                                                  # membrane cone
    eye.append(torus(0.062, 0.009, (0, -0.183, hub_z), axis="Y", seg=28, rseg=6))
    eye.append(s.ball(0.022, (0, -0.205, hub_z), seg=12))
    # two horns to the sides, turned a little back, and tipped down
    def horn(direction, tilt):
        d = V(direction).normalized()
        d = (d + V((0, 0, -tilt))).normalized()
        base = V((0, 0, hub_z)) + V((d.x, d.y, 0)).normalized() * 0.14
        out_b, out_a, out_e = [], [], []
        out_a.append(align(s.rcyl(0.055, 0.1, seg=18, r=0.01), base - d * 0.02, base + d * 0.08))        # driver can
        prof = [(0.0, 0.0), (0.045, 0.0), (0.05, 0.03), (0.07, 0.12), (0.11, 0.2), (0.16, 0.25), (0.165, 0.26),
                (0.15, 0.26), (0.06, 0.2), (0.0, 0.19)]
        out_b.append(point(s.lathe(prof, seg=28), base + d * 0.06, d))
        mouth = base + d * (0.06 + 0.26 * 0.995)
        rim = torus(0.158, 0.018, (0, 0, 0), seg=28, rseg=6)
        q = V((0, 0, 1)).rotation_difference(d)
        bmesh.ops.rotate(rim, cent=V((0, 0, 0)), matrix=q.to_matrix(), verts=rim.verts)
        s.place(rim, mouth)
        out_e.append(rim)
        out_e.append(point(s.lathe([(0.05, 0.0), (0.03, 0.02), (0.0, 0.025)], seg=16), base + d * 0.245, d))                                                       # glowing membrane
        # clamp to the hub
        out_a.append(align(s.rcyl(0.065, 0.03, seg=18, r=0.008), base - d * 0.01, base + d * 0.02))
        return out_b, out_a, out_e
    for dirv, tilt in (((-1, 0.2, 0), 0.12), ((1, 0.2, 0), 0.12), ((0, 1, 0), -0.25)):
        b, a_, e = horn(dirv, tilt)
        body += b
        acc += a_
        eye += e
    # the top: a short post, the dish on a tilt bracket, a whip antenna with a lamp
    top = hub_z + 0.15
    body.append(s.rcyl(0.03, 0.12, (0, 0, top + 0.05), seg=14, r=0.005))
    acc.append(s.rcyl(0.045, 0.03, (0, 0, top + 0.1), seg=16, r=0.006))
    dish_c = V((0, 0.0, top + 0.17))
    dn = V((0, -0.3, 1)).normalized()
    dish = s.lathe([(0.0, 0.0), (0.1, 0.012), (0.2, 0.055), (0.22, 0.08), (0.2, 0.084), (0.1, 0.034),
                    (0.0, 0.022)], seg=32)
    point(dish, dish_c - dn * 0.026, dn)
    acc.append(dish)
    body.append(rod(V((0, 0, top + 0.1)), dish_c - dn * 0.02, 0.018, seg=10))
    body.append(rod(dish_c, dish_c + dn * 0.17, 0.012, seg=8))                                  # feed arm
    eye.append(s.ball(0.03, dish_c + dn * 0.2, seg=10))                                     # lamp on the feed
    # cables from the horns and the hub down the mast into the junction box
    for sx in (-1, 1):
        acc.append(s.tube([(sx * 0.15, -0.02, hub_z - 0.06), (sx * 0.13, -0.03, hub_z - 0.22), (sx * 0.08, -0.04, 1.02),
                           (sx * 0.08, -0.04, 0.84)], 0.018, res=1, segments_per=6))
    P["Body"], P["Accent"], P["Eye"] = body, acc, eye
    return P, {}


# --------------------------------------------------------------------------- nurse

def nurse():
    """Медсестра, 1.77 m with the cap (hospital): a slim white skirt flaring onto a round base with hidden caster wheels,
    a belt at the waist, broad shoulders with a bib, a big egg head leaning forward with a dark face plate and one camera
    eye, a tall folded nurse cap with an accent cross; the right hand holds a big syringe high, the left ends in a clamp."""
    P = {}
    body, acc = [], []
    # base disc and skirt
    body.append(s.lathe([(0.0, 0.045), (0.27, 0.045), (0.295, 0.07), (0.29, 0.11), (0.26, 0.13), (0.0, 0.131)], seg=36))
    acc.append(torus(0.285, 0.018, (0, 0, 0.085), seg=36, rseg=8))
    skirt = s.loft([(0.11, 0.285, 0.265, 2.2), (0.15, 0.268, 0.25, 2.2), (0.24, 0.246, 0.229, 2.2), (0.36, 0.226, 0.21, 2.2),
                    (0.5, 0.2, 0.185, 2.2), (0.72, 0.172, 0.157, 2.2), (0.9, 0.158, 0.142, 2.2), (0.99, 0.15, 0.13, 2.2)],
                    seg=32)
    body.append(skirt)
    acc.append(hoop(0.2, 0.245, 0.252, 0.234, out=0.012))                                       # hem stripe
    acc.append(hoop(0.93, 0.99, 0.155, 0.136, out=0.014))                                       # belt at the waist
    # torso: broad shoulders over a narrow waist, a bib on the front
    torso = s.loft([(0.95, 0.14, 0.12, 2.4, 0, -0.01), (1.06, 0.18, 0.135, 2.6, 0, -0.015), (1.22, 0.225, 0.15, 2.8, 0, -0.02),
                    (1.32, 0.22, 0.14, 2.8, 0, -0.02), (1.38, 0.12, 0.09, 2.6, 0, -0.02)], seg=32, round_caps=0.03)
    body.append(torso)
    bib = s.loft([(1.0, 0.1, 0.03, 3.0), (1.16, 0.13, 0.03, 3.0), (1.26, 0.14, 0.03, 3.0)], seg=24, round_caps=0.012)
    s.place(bib, (0, -0.15, 0))
    s.taper(bib, "Z", 1.0, 1.0)
    body.append(bib)
    acc.append(s.rbox((0.026, 0.02, 0.07), (0.0, -0.183, 1.17), r=0.006, seg=1))                 # a small cross on the bib
    acc.append(s.rbox((0.07, 0.02, 0.026), (0.0, -0.183, 1.17), r=0.006, seg=1))
    # collar and neck
    acc.append(s.lathe([(0.11, 1.34), (0.125, 1.36), (0.09, 1.395), (0.0, 1.397)], seg=24))
    acc.append(s.lathe([(0.04, 1.38), (0.034, 1.42), (0.04, 1.46), (0.0, 1.461)], seg=14))
    # head: a big egg leaning forward, the face plate, the eye, ear discs
    hy = -0.06
    head = s.loft([(1.43, 0.0, 0.0, 2, 0, hy), (1.44, 0.08, 0.085, 2.2, 0, hy), (1.49, 0.14, 0.145, 2.2, 0, hy - 0.005),
                   (1.57, 0.15, 0.155, 2.2, 0, hy - 0.005), (1.64, 0.132, 0.137, 2.2, 0, hy), (1.69, 0.07, 0.075, 2.2, 0, hy + 0.005),
                   (1.705, 0.0, 0.0, 2, 0, hy + 0.005)], seg=32)
    body.append(head)
    face = s.loft([(0, 0.115, 0.085, 2.4), (0.02, 0.112, 0.082, 2.4)], seg=28, round_caps=0.008, axis="-Y")
    s.place(face, (0, -0.2, 1.55))
    acc.append(face)                                                                           # dark face plate
    a, g = L.eye(0.045, (0, -0.24, 1.53), depth=0.05)
    acc += a
    P["Eye"] = g
    for sx in (-1, 1):
        acc.append(s.rcyl(0.045, 0.03, (sx * 0.148, hy, 1.56), axis="X", seg=18, r=0.008))
    # nurse cap: a tall folded front band (no flat top), a soft crown on the back of the head, a big accent cross
    body.append(s.rbox((0.28, 0.035, 0.13), (0, -0.11, 1.715), r=0.012, seg=2, taper=(1.25, 1.0), rot=(-0.35, 0, 0)))
    body.append(s.ball(0.125, (0, -0.02, 1.665), seg=20, scale=(1.0, 1.0, 0.55)))
    acc.append(s.rbox((0.04, 0.02, 0.11), (0, -0.133, 1.722), r=0.007, seg=1, rot=(-0.35, 0, 0)))  # the cross
    acc.append(s.rbox((0.11, 0.02, 0.04), (0, -0.133, 1.722), r=0.007, seg=1, rot=(-0.35, 0, 0)))
    # arms: shoulder balls
    for sx in (-1, 1):
        acc += joint(0.058, (sx * 0.235, -0.02, 1.3))
    # right: elbow out, forearm up, a big syringe held high
    sh, el, wr = V((0.25, -0.02, 1.29)), V((0.3, -0.06, 1.08)), V((0.29, -0.2, 1.26))
    body += limb([sh, el], [0.048, 0.042])
    acc += joint(0.046, el)
    body += limb([el, wr], [0.04, 0.036])
    body += [s.ball(0.052, wr, seg=14)]
    sd = V((0.1, -0.25, 1)).normalized()
    s0 = wr - sd * 0.15
    acc.append(align(s.rcyl(0.044, 0.025, seg=14, r=0.006), s0 - sd * 0.012, s0 + sd * 0.012))    # plunger thumb pad
    acc.append(rod(s0, s0 + sd * 0.13, 0.017, seg=8))                                          # plunger rod
    acc.append(align(s.rbox((0.19, 0.055, 0.02), r=0.007, seg=1), s0 + sd * 0.12, s0 + sd * 0.14))     # finger flange
    body.append(align(s.rcyl(0.058, 0.38, seg=20, r=0.01), s0 + sd * 0.13, s0 + sd * 0.51))     # barrel
    for k in range(3):                                                                         # graduation rings
        c = s0 + sd * (0.25 + 0.07 * k)
        acc.append(align(s.rcyl(0.062, 0.014, seg=20, r=0.004, bevel_seg=1), c - sd * 0.007, c + sd * 0.007))
    acc.append(align(s.rcyl(0.038, 0.05, seg=14, r=0.006, radius2=0.018), s0 + sd * 0.505, s0 + sd * 0.555))  # hub
    acc.append(rod(s0 + sd * 0.545, s0 + sd * 0.71, 0.012, seg=6))                             # needle
    # left: forearm forward-down, a chunky clamp of two curved jaws on a hinge
    sh, el, wr = V((-0.25, -0.02, 1.29)), V((-0.29, -0.04, 1.06)), V((-0.27, -0.22, 0.98))
    body += limb([sh, el], [0.048, 0.042])
    acc += joint(0.046, el)
    body += limb([el, wr], [0.04, 0.036])
    acc.append(s.rcyl(0.048, 0.06, wr, axis="X", seg=16, r=0.012))
    d = (wr - el).normalized()
    for sz in (-1, 1):
        acc += L.chain([wr + V((0, 0, sz * 0.02)), wr + d * 0.1 + V((0, 0, sz * 0.065)), wr + d * 0.2 + V((0, 0, sz * 0.014))],
                       [0.032, 0.026, 0.016], n=2.6, flat=0.6, seg=10)
    P["Body"], P["Accent"] = body, acc
    P.update(wheels([(0.0, -0.17, 0.05), (-0.15, 0.085, 0.05), (0.15, 0.085, 0.05)], 0.05, 0.04))
    return P, {}


DESIGNS = {"robot_warden": warden, "robot_repairman": repairman, "robot_smoker": smoker, "robot_relay": relay,
           "robot_nurse": nurse}


def build(name):
    s.clear_scene()
    P, split = DESIGNS[name]()
    objs = []
    for role, bms in P.items():
        bms = [b for b in bms if b is not None]
        if os.environ.get("TRIS"):   # where the triangles go: the heaviest pieces of each role
            heavy = sorted(((sum(len(f.verts) - 2 for f in b.faces), k) for k, b in enumerate(bms)), reverse=True)[:8]
            print(f"  TRIS {role}: " + "  ".join(f"#{k} {n} @{tuple(round(c, 2) for c in next(iter(bms[k].verts)).co)}" for n, k in heavy))
        if not bms:
            continue
        obj = s.part(role, bms, sharp_angle=L.sharp)
        objs += s.split_at(obj, split[role]) if role in split else [obj]
    for o in objs:
        o.data.name = o.name
    meshfix.fix_objects(objs, floor=False, name=name, pinned=lambda n: n.startswith(MOVING))
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs}
    print(f"MODEL {name}: {sum(tris.values())} tris  " + "  ".join(f"{k} {v}" for k, v in sorted(tris.items())))
    return objs


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
    names = argv[1:] or list(DESIGNS)
    for name in names:
        objs = build(name)
        s.export_fbx(os.path.join(root, "Robots", name + ".fbx"), objs)
    print("DANGER A DONE")


if __name__ == "__main__":
    main()
