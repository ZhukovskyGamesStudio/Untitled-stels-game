"""The team's storage mule as a raccoon with a backpack (concept: Logs/comfy/raccoon/03_karabiny.png, raccoon_v3_refs.jpg).

Two models, so the backpack can grow with the kennel upgrades and its gear can swing on its own:

  raccoon.fbx       armature "Rig" + one skinned mesh "Body" (sculpted with humanlib.sdf, baked into
                    raccoon_{base,normal,mask}.png); the harness straps are painted and raised on the fur, so they bend with
                    it. Takes (in place: the game moves the body): Idle, Walk, Run, Climb (the basement shaft's ladder),
                    VentUp (the leap up into a ceiling grate), VentDown (the drop out of one), Pet (the reaction to a pat),
                    Curl (asleep in the car's trunk). The bone "pack" (on the spine, not deforming) carries the backpack in the game.
  raccoon_pack.fbx  the backpacks in the raccoon's rest-pose space, one per level of the backpack upgrade (the game shows
                    one), parts named <Material>_L<level>[Group]: L0 a small soft rucksack, L1 the plaid pack, L2 a hiking pack with
                    side pouches and a bedroll, L3 a frame pack in an aluminium cage (concepts: Logs/comfy/upgrades/pack*.png);
                    groups DangleA / DangleB / DangleC (a carabiner with a mug / with keys, a lantern, a tin can: the game
                    swings each from its highest point). The stash wraps round each level's main body — Sack_L0 /
                    Canvas_L<n>: its sides, top and back (the builder's "Load" per level, RaccoonPack, PackWrap).

    blender -b --factory-startup --python-exit-code 1 --python Tools/Blender/raccoon.py -- [out=<dir>] [tex=<dir>]
            [preview=<png>] [draft] [only=body|pack]

Defaults: out = Assets/_Game/Art/Models/Raccoon, tex = Assets/_Game/Art/Textures/Raccoon (experiment in the scratchpad).
Blender: -Y is the raccoon's front, Z up, metres; the paws stand on z = 0.
"""
import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from humanlib import build, sdf, view  # noqa: E402
import shapes as S  # noqa: E402

ARGS = dict(a.split("=", 1) if "=" in a else (a, "1") for a in (sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = ARGS.get("out", os.path.join(ROOT, "Assets/_Game/Art/Models/Raccoon"))
TEX = ARGS.get("tex", os.path.join(ROOT, "Assets/_Game/Art/Textures/Raccoon"))
PREVIEW = ARGS.get("preview")
DRAFT = "draft" in ARGS
ONLY = ARGS.get("only")
FPS = 30

# --------------------------------------------------------------------------- the skeleton (rest pose: standing on all fours)

BONES = [
    ("root", (0, 0, 0), (0, 0, 0.1), None),
    ("pelvis", (0, 0.17, 0.32), (0, 0.02, 0.345), "root"),
    ("spine", (0, 0.02, 0.345), (0, -0.12, 0.345), "pelvis"),
    ("chest", (0, -0.12, 0.345), (0, -0.22, 0.37), "spine"),
    ("neck", (0, -0.22, 0.37), (0, -0.29, 0.42), "chest"),
    ("head", (0, -0.29, 0.42), (0, -0.52, 0.42), "neck"),
    ("pack", (0, -0.02, 0.50), (0, -0.02, 0.60), "spine"),
    ("tail1", (0, 0.25, 0.33), (0, 0.37, 0.31), "pelvis"),
    ("tail2", (0, 0.37, 0.31), (0, 0.49, 0.32), "tail1"),
    ("tail3", (0, 0.49, 0.32), (0, 0.59, 0.36), "tail2"),
    ("tail4", (0, 0.59, 0.36), (0, 0.67, 0.44), "tail3"),
]
for _s, _sd in ((1, "L"), (-1, "R")):
    BONES += [
        (f"ear.{_sd}", (0.095 * _s, -0.31, 0.53), (0.12 * _s, -0.31, 0.63), "head"),
        (f"upper_arm.{_sd}", (0.11 * _s, -0.15, 0.24), (0.115 * _s, -0.15, 0.12), "chest"),
        (f"forearm.{_sd}", (0.115 * _s, -0.15, 0.12), (0.115 * _s, -0.16, 0.045), f"upper_arm.{_sd}"),
        (f"paw.{_sd}", (0.115 * _s, -0.16, 0.045), (0.115 * _s, -0.22, 0.015), f"forearm.{_sd}"),
        (f"thigh.{_sd}", (0.115 * _s, 0.17, 0.25), (0.12 * _s, 0.11, 0.13), "pelvis"),
        (f"shin.{_sd}", (0.12 * _s, 0.11, 0.13), (0.12 * _s, 0.20, 0.06), f"thigh.{_sd}"),
        (f"foot.{_sd}", (0.12 * _s, 0.20, 0.06), (0.12 * _s, 0.12, 0.015), f"shin.{_sd}"),
    ]
DEFORM = [b[0] for b in BONES if b[0] not in ("root", "pack")]
# how far a bone reaches into the body (skin weights by distance in these radii)
RADIUS = {"pelvis": 0.16, "spine": 0.17, "chest": 0.15, "neck": 0.11, "head": 0.15, "ear": 0.045, "tail1": 0.08, "tail2": 0.09,
          "tail3": 0.09, "tail4": 0.07, "upper_arm": 0.07, "forearm": 0.05, "paw": 0.04, "thigh": 0.08, "shin": 0.055, "foot": 0.04}


def skeleton():
    arm = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.scene.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, h, t, parent in BONES:
        eb = arm.edit_bones.new(name)
        eb.head, eb.tail = Vector(h), Vector(t)
        eb.roll = 0.0
        eb.use_deform = name in DEFORM
        if parent:
            eb.parent = arm.edit_bones[parent]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return rig


# --------------------------------------------------------------------------- the sculpt

TAIL = [(0, 0.25, 0.33, 0.065), (0, 0.37, 0.31, 0.095), (0, 0.49, 0.32, 0.10), (0, 0.59, 0.36, 0.09), (0, 0.66, 0.44, 0.07)]
EYE_C = np.array((0.07, -0.445, 0.465))
EAR_C, EAR_R, EAR_ROT = (0.105, -0.31, 0.575), (0.065, 0.032, 0.07), sdf.rot_y(math.radians(-25))
BELLY_STRAP = 0.10                                              # the harness: a band round the belly (y) ...
CHEST_C, CHEST_N = np.array((0, -0.17, 0.33)), np.array((0, 1.0, -0.55)) / np.linalg.norm((0, 1.0, -0.55))   # ... and a slanted one round the chest


def strap_bands(p):
    """Distance-like value of the two harness straps (negative inside a strap's band)."""
    belly = np.abs(p[:, 1] - BELLY_STRAP) - 0.017
    chest = np.abs((p - CHEST_C) @ CHEST_N) - 0.017
    return np.minimum(belly, chest)


def body_field(p):
    q = p.copy()
    q[:, 0] = np.abs(q[:, 0])                                   # (paired parts: both sides from one)
    d = sdf.ell(p, (0, 0.0, 0.34), (0.19, 0.22, 0.17))
    d = sdf.smin(d, sdf.ell(p, (0, -0.13, 0.33), (0.17, 0.12, 0.165)), 0.06)          # chest
    d = sdf.smin(d, sdf.ell(p, (0, 0.15, 0.34), (0.18, 0.13, 0.18)), 0.06)            # high round rump
    d = sdf.smin(d, sdf.ell(p, (0, 0.02, 0.245), (0.17, 0.19, 0.13)), 0.06)           # low belly
    d = sdf.smin(d, sdf.cone(p, (0, -0.18, 0.36), (0, -0.29, 0.42), 0.13, 0.12), 0.06)  # (no neck to speak of)
    head = sdf.ell(p, (0, -0.33, 0.45), (0.165, 0.15, 0.14))
    head = sdf.smin(head, sdf.ell(q, (0.11, -0.36, 0.41), (0.08, 0.08, 0.07)), 0.05)       # fluffy cheek ruff
    head = sdf.smin(head, sdf.cone(p, (0, -0.42, 0.43), (0, -0.52, 0.41), 0.07, 0.035), 0.04)   # short blunt snout
    head = sdf.smin(head, sdf.ell(p, (0, -0.535, 0.415), (0.028, 0.022, 0.022)), 0.01)           # nose
    head = sdf.smin(head, sdf.ell(q, EYE_C, (0.032, 0.022, 0.032)), 0.006)                       # big eyes
    ear = sdf.ell(q, EAR_C, EAR_R, rot=EAR_ROT)
    head = sdf.smin(head, ear, 0.02)
    d = sdf.smin(d, head, 0.06)
    front = sdf.chain(q, [(0.11, -0.15, 0.24, 0.085), (0.115, -0.15, 0.12, 0.06), (0.115, -0.16, 0.045, 0.05)], 0.035)
    front = sdf.smin(front, sdf.ell(q, (0.115, -0.18, 0.025), (0.05, 0.062, 0.027)), 0.02)
    hind = sdf.chain(q, [(0.115, 0.17, 0.25, 0.10), (0.12, 0.11, 0.13, 0.065), (0.12, 0.20, 0.06, 0.048)], 0.035)
    hind = sdf.smin(hind, sdf.ell(q, (0.12, 0.165, 0.025), (0.05, 0.078, 0.027)), 0.02)
    d = sdf.smin(d, sdf.smin(front, hind, 0.0), 0.07)
    d = sdf.smin(d, sdf.chain(p, TAIL, 0.05), 0.04)
    # the Y-harness, a few millimetres proud of the fur
    d = np.minimum(d, sdf.smax(d - 0.005, strap_bands(p), 0.004))
    return sdf.smax(d, -p[:, 2], 0.004)                         # flat under the paws


def tail_share(pts):
    """0 at the tail's root … 1 at its tip, for points near the tail (its rings)."""
    best, share = np.full(len(pts), 9.0), np.zeros(len(pts))
    lens = [np.linalg.norm(np.subtract(b[:3], a[:3])) for a, b in zip(TAIL, TAIL[1:])]
    total, acc = sum(lens), 0.0
    for (a, b), ln in zip(zip(TAIL, TAIL[1:]), lens):
        dist, t = sdf.seg_dist(pts, a[:3], b[:3])
        better = dist < best
        best[better] = dist[better]
        share[better] = (acc + t[better] * ln) / total
        acc += ln
    return share, best


def paint_body(pts, nrm):
    x, y, z = pts[:, 0], pts[:, 1], pts[:, 2]
    ax = np.abs(x)
    n = build.noise(pts, 38.0, 1)
    grey = np.array((0.42, 0.415, 0.42))
    col = grey[None, :] * (0.85 + 0.3 * n[:, None])
    # darker along the back, lighter belly
    top = np.clip((z - 0.42) / 0.08, 0, 1) * (y > -0.22)
    col = col * (1 - 0.25 * top[:, None])
    belly = np.clip((0.2 - z) / 0.06, 0, 1) * (y > -0.22) * (y < 0.25)
    col = col * (1 - belly[:, None]) + np.array((0.68, 0.67, 0.65)) * belly[:, None]
    head = y < -0.24
    front = nrm[:, 1] < 0.3
    # white muzzle, cheek ruffs and brows; one bold black band over the eyes with a dark bridge down the nose
    muzzle = head & (y < -0.42) & (z < 0.445)
    cheek = head & (ax > 0.1) & (z < 0.44) & (y < -0.3)
    band = ((ax - 0.07) / 0.088) ** 2 + ((z - 0.465 + 0.12 * (ax - 0.07)) / 0.042) ** 2
    mask = head & front & (y < -0.38) & ((band < 1) | ((ax < 0.035) & (np.abs(z - 0.468) < 0.022)))
    brow = head & front & (y < -0.36) & ~mask & (band < 1.9) & (z > 0.47) & (ax < 0.15)
    col[muzzle | cheek | brow] = np.array((0.88, 0.87, 0.84)) * (0.95 + 0.08 * n[muzzle | cheek | brow, None])
    col[mask] = (0.045, 0.045, 0.05)
    bridge = head & front & (ax < 0.018) & (z > 0.43) & (z < 0.455) & (y < -0.43)
    col[bridge] = (0.22, 0.22, 0.225)
    nose = (y < -0.515) & (z > 0.395)
    col[nose] = (0.04, 0.035, 0.04)
    eyes = np.linalg.norm((np.c_[ax, y, z] - EYE_C) / np.array((0.034, 0.024, 0.034)), axis=1) < 1.05
    col[eyes] = (0.015, 0.015, 0.02)
    glint = np.linalg.norm(np.c_[ax, y, z] - (EYE_C + np.array((0.008, -0.02, 0.014))), axis=1) < 0.0105
    col[glint] = (0.96, 0.96, 0.96)
    # ears: grey with a white rim, dark inside
    q = np.c_[ax, y, z]
    ear = head & (z > 0.5) & (sdf.ell(q, EAR_C, EAR_R, rot=EAR_ROT) < 0.006)
    col[ear] = (0.30, 0.29, 0.30)
    rim = ear & (np.hypot((ax - 0.105 - 0.45 * (z - 0.575)) / 0.065, (z - 0.575) / 0.07) > 0.74)
    col[rim] = (0.86, 0.85, 0.83)
    inner = ear & ~rim & (nrm[:, 1] < -0.35)
    col[inner] = (0.15, 0.12, 0.12)
    # black only on the paws (below the wrists and hocks)
    paws = z < 0.062
    col[paws] = np.array((0.08, 0.08, 0.085)) * (0.9 + 0.2 * n[paws, None])
    # the tail: five crisp dark rings and a dark tip
    share, dist = tail_share(pts)
    tail = (dist < 0.11) & (y > 0.27)
    ring = ((share * 5.0 + 0.25) % 1.0) < 0.42
    col[tail & ring] = np.array((0.08, 0.08, 0.085)) * (0.9 + 0.2 * n[tail & ring, None])
    col[tail & ~ring] = np.array((0.72, 0.71, 0.68)) * (0.92 + 0.12 * n[tail & ~ring, None])
    col[tail & (share > 0.9)] = (0.07, 0.07, 0.075)
    # the harness (olive canvas) with a buckle where the chest strap crosses the breastbone
    strap = (strap_bands(pts) < 0.002) & (z > 0.1)
    col[strap] = np.array((0.27, 0.30, 0.17)) * (0.9 + 0.15 * n[strap, None])
    buckle = strap & (ax < 0.03) & (y < -0.2) & (z < 0.3)
    col[buckle] = (0.6, 0.6, 0.62)
    gloss = np.full(len(pts), 0.22)
    gloss[strap] = 0.15
    gloss[nose] = 0.55
    gloss[buckle] = 0.6
    gloss[eyes] = 0.9
    return np.clip(col, 0, 1), gloss


def keep_head(pts):
    """Triangles held on the face and the paws (what the eye reads), loose on the barrel of the body."""
    face = np.clip((-pts[:, 1] - 0.30) / 0.08, 0, 1)
    paws = np.clip((0.09 - pts[:, 2]) / 0.05, 0, 1)
    return np.maximum(face, 0.6 * paws)


def sculpt_body():
    h = 0.012 if DRAFT else 0.0055
    t = time.time()
    hi = sdf.mesh("raccoon_hi", body_field, (-0.26, -0.58, -0.01), (0.26, 0.76, 0.72), h, mirror=True)
    sdf.keep_biggest(hi)
    col, gloss = paint_body(sdf.points_of(hi), build.normals_of(hi))
    build.paint(hi, col, gloss)
    print(f"SCULPT body {sdf.tri_count(hi)} tris in {time.time() - t:.1f} s")
    return build.Piece("body", "Body", "body", hi, 2600 if DRAFT else 5200, DEFORM, keep=keep_head)


def assemble_body():
    piece = sculpt_body()
    build.lowpoly(piece)
    build.clean(piece.lo)
    build.orient_like(piece.lo, piece.hi)
    build.unwrap(piece)
    size = 512 if DRAFT else 1024
    build.pack([piece], size)
    t = time.time()
    build.bake([piece], size, TEX, "raccoon", ao_samples=16 if DRAFT else 48)
    print(f"BAKED body {time.time() - t:.1f} s")
    rig = skeleton()
    seg = {n: (np.array(h), np.array(tl)) for n, h, tl, _ in BONES}
    lo = piece.lo
    for name in DEFORM:
        lo.vertex_groups.new(name=name)
    pts = sdf.points_of(lo)
    w = build.weights(pts, {k: seg[k] for k in DEFORM}, DEFORM, RADIUS)
    build.assign(lo, w)
    lo.name = lo.data.name = "Body"
    lo.data.uv_layers.active.name = "UVMap"
    errors = build.audit(lo)
    lo.parent = rig
    mod = lo.modifiers.new("Rig", "ARMATURE")
    mod.object = rig
    piece.hi.hide_render = piece.hi.hide_viewport = True
    return rig, lo, errors


# --------------------------------------------------------------------------- posing and the takes

def rot(x=0.0, y=0.0, z=0.0):
    """A turn in the rest pose's axes, degrees: x pitches (a hanging limb swings back, the spine dips its front end),
    y rolls, z yaws (to the raccoon's left)."""
    return Euler((math.radians(x), math.radians(y), math.radians(z)), "XYZ").to_quaternion()


class Pose:
    def __init__(self, rig):
        self.rig = rig
        self.r, self.loc = {}, {}

    def turn(self, bone, x=0.0, y=0.0, z=0.0):
        self.r[bone] = rot(x, y, z) @ self.r.get(bone, Quaternion())

    def move(self, bone, d):
        self.loc[bone] = Vector(self.loc.get(bone, Vector())) + Vector(d)

    def pivot(self, x, about):
        """The whole body pitched x degrees about a point (the root's turn plus the shift that keeps the point still)."""
        q = rot(x=x)
        self.turn("root", x=x)
        p = Vector(about)
        self.move("root", p - q @ p)

    def apply(self):
        for pb in self.rig.pose.bones:
            ml = pb.bone.matrix_local.to_quaternion()
            pb.rotation_quaternion = ml.inverted() @ self.r.get(pb.name, Quaternion()) @ ml
            d = self.loc.get(pb.name)
            pb.location = (ml.inverted() @ Vector(d)) if d is not None else Vector()


TAU = 2 * math.pi
SIDES = (("L", 1.0), ("R", -1.0))


def smooth01(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def ramp(t, a, b):
    return smooth01((t - a) / max(b - a, 1e-6))


def tail_wave(P, amp, phase, rate=1.0, lift=0.0):
    for i in range(4):
        P.turn(f"tail{i + 1}", x=-lift * (1 if i == 0 else 0.3), z=amp * (0.6 + 0.25 * i) * math.sin(phase - 0.7 * i * rate))


def leg_cycle(P, side, which, s, amp, stance, lift, sign):
    """One leg at its phase s (0..1): planted from front to back over the stance share, then lifted forward."""
    if s < stance:
        a = -amp + 2 * amp * (s / stance)
        up = 0.0
    else:
        u = (s - stance) / (1 - stance)
        a = amp - 2 * amp * smooth01(u)
        up = math.sin(math.pi * u)
    if which == "front":
        P.turn(f"upper_arm.{side}", x=a - 8 * up)
        P.turn(f"forearm.{side}", x=lift * up)
        P.turn(f"paw.{side}", x=0.8 * lift * up)
    else:
        P.turn(f"thigh.{side}", x=a - 18 * up)
        P.turn(f"shin.{side}", x=lift * up)
        P.turn(f"foot.{side}", x=-0.4 * lift * up)


# the takes' speeds on the floor (m/s): the game plays a gait at the body's speed over this (StorageBotAnimator)
WALK_SPEED, RUN_SPEED, CLIMB_SPEED = 0.55, 2.4, 1.0      # (StorageBotRules.WalkTakeSpeed, RunTakeSpeed, ClimbTakeSpeed)


def take_idle(P, t):
    b = math.sin(t * TAU * 3)
    P.turn("chest", x=-1.2 * b)
    P.turn("spine", x=0.6 * b)
    P.move("root", (0, 0, 0.003 * b))
    look = math.sin(t * TAU) * 0.8 + 0.35 * math.sin(t * TAU * 2 + 1.0)
    P.turn("neck", z=14 * look, x=-3 + 3 * math.sin(t * TAU * 2))
    P.turn("head", z=10 * look, y=-6 * look, x=-4 * math.sin(t * TAU * 4) * ramp(t, 0.55, 0.6) * (1 - ramp(t, 0.7, 0.75)))  # (a sniff)
    for side, sg in SIDES:
        tw = math.exp(-((t - (0.3 if side == "L" else 0.78)) / 0.025) ** 2)
        P.turn(f"ear.{side}", y=-25 * tw * sg, x=10 * tw)
    tail_wave(P, 9, t * TAU * 2, lift=4)


def take_walk(P, t):
    offs = {("hind", "L"): 0.0, ("front", "L"): 0.25, ("hind", "R"): 0.5, ("front", "R"): 0.75}
    for (which, side), o in offs.items():
        leg_cycle(P, side, which, (t - o) % 1.0, 22, 0.62, 48 if which == "front" else 40, 1)
    sway = math.sin(t * TAU)
    P.turn("pelvis", y=3.5 * sway, z=3 * sway)
    P.turn("chest", y=-3 * sway, z=-2.5 * sway)
    P.move("root", (0, 0, 0.008 * math.cos(t * TAU * 2)))
    P.turn("neck", x=2 * math.cos(t * TAU * 2), z=2 * sway)
    P.turn("head", x=-2 * math.cos(t * TAU * 2))
    tail_wave(P, 10, t * TAU, lift=6)


def take_run(P, t):
    # a bound: the forelegs nearly together, then the hind legs; the back gathers and stretches
    offs = {("front", "L"): 0.0, ("front", "R"): 0.1, ("hind", "L"): 0.5, ("hind", "R"): 0.6}
    for (which, side), o in offs.items():
        leg_cycle(P, side, which, (t - o) % 1.0, 30, 0.42, 62 if which == "front" else 52, 1)
    flex = math.sin(t * TAU)
    P.turn("pelvis", x=-14 * flex)
    P.turn("spine", x=8 * flex)
    P.turn("chest", x=8 * flex)
    P.move("root", (0, 0, 0.035 * max(0.0, math.sin(t * TAU * 2 + 0.6)) - 0.01))
    P.turn("neck", x=-7 * flex)
    P.turn("head", x=5 * flex)
    for side, sg in SIDES:
        P.turn(f"ear.{side}", x=25, y=-10 * sg)
    tail_wave(P, 7, t * TAU, lift=-6 + 14 * flex)


def take_climb(P, t):
    """Up a vertical ladder: the body stood up against the rungs, a forepaw and the opposite hind paw reach together."""
    P.pivot(-75, (0, 0.17, 0.0))
    for side, sg in SIDES:
        ph = t * TAU + (0 if side == "L" else math.pi)
        reach = math.sin(ph)
        up = max(0.0, math.cos(ph))
        P.turn(f"upper_arm.{side}", x=-45 - 38 * reach, z=-6 * sg)
        P.turn(f"forearm.{side}", x=35 + 30 * up)
        P.turn(f"paw.{side}", x=25)
        P.turn(f"thigh.{side}", x=-35 + 38 * reach)
        P.turn(f"shin.{side}", x=25 + 45 * max(0.0, -math.cos(ph)))
        P.turn(f"foot.{side}", x=-35 + 20 * reach)
    sway = math.sin(t * TAU)
    P.turn("pelvis", y=5 * sway, z=4 * sway)
    P.turn("chest", y=-5 * sway)
    P.turn("neck", x=10)
    P.turn("head", x=16 + 3 * math.sin(t * TAU * 2))
    for i in range(4):
        P.turn(f"tail{i + 1}", x=22 if i == 0 else 10, z=12 * math.sin(t * TAU - 0.8 * i))


def take_vent_up(P, t):
    """A crouched spring up into a grate overhead (the game lifts the body after the crouch): forepaws first."""
    crouch = ramp(t, 0.0, 0.3) * (1 - ramp(t, 0.32, 0.42))
    leap = ramp(t, 0.34, 0.55)
    P.move("root", (0, 0, -0.07 * crouch))
    for side, sg in SIDES:
        P.turn(f"upper_arm.{side}", x=-25 * crouch - 105 * leap, z=-6 * sg * leap)
        P.turn(f"forearm.{side}", x=50 * crouch + 20 * leap)
        P.turn(f"paw.{side}", x=20 * crouch - 10 * leap)
        P.turn(f"thigh.{side}", x=-35 * crouch + 50 * leap)
        P.turn(f"shin.{side}", x=55 * crouch - 15 * leap)
        P.turn(f"foot.{side}", x=-25 * crouch + 45 * leap)
        P.turn(f"ear.{side}", x=30 * leap)
    P.pivot(-48 * leap, (0, 0.05, 0.3))
    P.turn("spine", x=-6 * leap)
    P.turn("neck", x=-15 * crouch - 6 * leap)
    P.turn("head", x=-10 * crouch - 8 * leap)
    kick = math.sin(t * TAU * 3) * ramp(t, 0.55, 0.7)
    P.turn("thigh.L", x=12 * kick)
    P.turn("thigh.R", x=-12 * kick)
    tail_wave(P, 10 * leap, t * TAU * 2, lift=10 * crouch + 20 * leap)


def take_vent_down(P, t):
    """Out of a grate overhead: nose down with the forepaws reaching for the floor, landing on them, standing up."""
    # (the game: it hangs 0.3 s, drops 0.45 s — 1.2 s of take, the paws touch the floor at 0.62 of it)
    dive = 1 - ramp(t, 0.5, 0.64)
    land = ramp(t, 0.6, 0.68) * (1 - ramp(t, 0.72, 1.0))
    P.pivot(38 * dive, (0, -0.15, 0.05))
    P.move("root", (0, 0, -0.06 * land))
    for side, sg in SIDES:
        P.turn(f"upper_arm.{side}", x=-30 * dive - 15 * land, z=-5 * sg * dive)
        P.turn(f"forearm.{side}", x=-5 * dive + 40 * land)
        P.turn(f"paw.{side}", x=-15 * dive + 15 * land)
        P.turn(f"thigh.{side}", x=25 * dive - 25 * land)
        P.turn(f"shin.{side}", x=10 * dive + 45 * land)
        P.turn(f"foot.{side}", x=15 * dive - 15 * land)
        P.turn(f"ear.{side}", x=20 * dive)
    P.turn("neck", x=-12 * dive + 6 * land)
    P.turn("head", x=-18 * dive)
    tail_wave(P, 8, t * TAU * 2, lift=18 * dive + 8 * land)


def take_pet(P, t):
    """A pat on the head: it settles back a little, tilts its head up into the hand and rubs it side to side with the
    ears laid back and the eyes squeezed (the head tilt), and wags its tail; the neck stays short."""
    sit = ramp(t, 0.05, 0.22) * (1 - ramp(t, 0.82, 1.0))
    lean = ramp(t, 0.12, 0.28) * (1 - ramp(t, 0.75, 0.92))
    P.pivot(-9 * sit, (0, -0.16, 0.0))
    for side, sg in SIDES:
        P.turn(f"thigh.{side}", x=-30 * sit)
        P.turn(f"shin.{side}", x=34 * sit)
        P.turn(f"upper_arm.{side}", x=7 * sit)
        P.turn(f"ear.{side}", x=45 * lean, y=20 * lean * sg)
    rub = math.sin(t * TAU * 3) * lean
    P.turn("neck", x=-6 * lean, z=8 * rub)
    P.turn("head", x=-16 * lean, y=18 * rub, z=10 * rub)
    P.turn("chest", y=-5 * rub, x=-3 * sit)
    P.turn("pelvis", y=4 * rub)
    P.move("root", (0, 0, 0.004 * math.sin(t * TAU * 6) * lean))
    wag = ramp(t, 0.1, 0.25) * (1 - ramp(t, 0.85, 1.0))
    tail_wave(P, 30 * wag, t * TAU * 4.5, rate=1.3, lift=14 * wag)


def take_curl(P, t):
    """Asleep in the car's trunk: lying on its belly, curled round to the left into a ball, the head on its paws, the tail
    wrapped round the front, breathing slowly."""
    b = math.sin(t * TAU * 2)
    P.move("root", (0, 0, -0.105 + 0.003 * b))
    for side, sg in SIDES:
        P.turn(f"thigh.{side}", x=-75)
        P.turn(f"shin.{side}", x=115)
        P.turn(f"foot.{side}", x=-30)
        P.turn(f"upper_arm.{side}", x=70)
        P.turn(f"forearm.{side}", x=-110)
        P.turn(f"paw.{side}", x=-20)
        P.turn(f"ear.{side}", x=22, y=8 * sg)
    P.turn("pelvis", z=8)
    P.turn("spine", z=34, x=-1.0 * b)
    P.turn("chest", z=34, x=1.5 * b)
    P.turn("neck", z=40, x=16)
    P.turn("head", z=34, x=12, y=-12)
    for i in range(4):
        P.turn(f"tail{i + 1}", x=14 if i == 0 else 3, z=-50 - 3 * math.sin(t * TAU + i))   # (a tail pointing back sweeps to the left with a negative turn)


def take_sit(P, t):
    """Sat down on its bottom where it was called, waiting for the next word: the front up on straight forelegs, the hind
    legs folded flat beside the belly, the tail along the floor bent round to one side; it breathes and twitches an ear.
    (The game turns its neck and head to the nearest player on top of this.) Measured: the forepaws, the hind feet and
    the rump all on z = 0."""
    b = math.sin(t * TAU * 2)
    P.pivot(-35, (0, 0.2, 0.16))                                 # the front up about the rump ...
    P.move("root", (0, 0, -0.17))                               # ... and the rump down onto the floor
    for side, sg in SIDES:
        P.turn(f"thigh.{side}", x=-55)
        P.turn(f"shin.{side}", x=80)
        P.turn(f"foot.{side}", x=5)
        P.turn(f"upper_arm.{side}", x=35)                        # (the forelegs straight down again)
        tw = math.exp(-((t - (0.4 if side == "L" else 0.85)) / 0.025) ** 2)
        P.turn(f"ear.{side}", y=-25 * tw * sg, x=10 * tw)
    P.turn("chest", x=-1.0 * b)
    P.turn("spine", x=0.5 * b)
    P.turn("neck", x=15)                                         # (the head level again, looking ahead)
    P.turn("head", x=15)
    P.turn("tail1", x=45, z=12)
    for i in (2, 3, 4):
        P.turn(f"tail{i}", z=18 + 3 * math.sin(t * TAU + i))


# name, frames, loops, take
TAKES = [("Idle", 120, True, take_idle), ("Walk", 24, True, take_walk), ("Run", 14, True, take_run),
         ("Climb", 24, True, take_climb), ("VentUp", 27, False, take_vent_up), ("VentDown", 36, False, take_vent_down),
         ("Pet", 66, False, take_pet), ("Curl", 90, True, take_curl), ("Sit", 90, True, take_sit)]


def animate(rig):
    rig.animation_data_create()
    acts = []
    for name, frames, loops, fn in TAKES:
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        rig.animation_data.action = act
        for f in range(frames + 1):
            P = Pose(rig)
            fn(P, (f / frames) if loops else min(1.0, f / frames))
            P.apply()
            for pb in rig.pose.bones:
                pb.keyframe_insert("rotation_quaternion", frame=f + 1)
                pb.keyframe_insert("location", frame=f + 1)
        acts.append(act)
    rig.animation_data.action = None
    for pb in rig.pose.bones:                                   # (the file's own pose: the rest pose)
        pb.rotation_quaternion = Quaternion()
        pb.location = Vector()
    return acts


# --------------------------------------------------------------------------- the backpacks (one per level of the upgrade)

BACK_Z = 0.49                                                    # the raccoon's back under the pack (the bone "pack" at z 0.50)
# each level's main body: centre and size (across, along the body, up); the stash wraps round Canvas_L<n>'s / Sack_L0's box
PACKS = {
    0: ((0.0, 0.0, BACK_Z + 0.105), (0.27, 0.25, 0.21)),        # a small soft rucksack (concept Logs/comfy/upgrades/pack0_100.png)
    1: ((0.0, -0.02, 0.615), (0.32, 0.32, 0.25)),               # the plaid pack (Logs/comfy/raccoon/03_karabiny.png)
    2: ((0.0, -0.02, BACK_Z + 0.2), (0.35, 0.34, 0.40)),        # a tall hiking pack with pouches and a bedroll (pack2_200.png)
    3: ((0.0, -0.02, BACK_Z + 0.225), (0.43, 0.41, 0.42)),      # a frame pack in an aluminium cage (pack3_301.png)
}
PACK_C, (PACK_W, PACK_D, PACK_H) = PACKS[1]


def backpack():
    """Every level's pack, all in the raccoon's rest space (the game hangs them on the bone "pack" and shows one).
    Parts are <Material>_L<level>[Group]: a group DangleA / DangleB / DangleC swings from its highest point."""
    return pack0() + pack1() + pack2() + pack3()


def harness(level, w, d):
    """The strip under a pack that sits on the back."""
    return S.rbox((w * 0.86, d * 0.9, 0.022), (0, PACKS[level][0][1], BACK_Z + 0.006), r=0.008, seg=1)


def dangles(level, rear, z, sx=0.095, k=1.5):
    """A red carabiner with a mug and a blue one with keys, clipped to loops on the rear face (+y) at height z."""
    objs = []
    for g, x, colour in (("DangleA", -sx, "Red"), ("DangleB", sx, "Blue")):
        hy = rear + 0.024
        objs.append(S.part(f"{colour}_L{level}{g}", [carabiner(x, hy, z - 0.012)]))
        objs.append(S.part(f"Metal_L{level}{g}", [S.tube([(x + 0.0256, hy, z - 0.055), (x + 0.0256, hy, z - 0.108)], 0.0055, res=1)]))
        if g == "DangleA":
            objs.append(S.part(f"Mug_L{level}{g}", mug(x + 0.0256, hy + 0.02, z - 0.155, k=k)))   # (under the hook)
        else:
            objs.append(S.part(f"Brass_L{level}{g}", keys(x + 0.0256, hy + 0.005, z - 0.14, k=k)))
    return objs


def pack0():
    """Level 0: a small soft canvas rucksack (concept Logs/comfy/upgrades/pack0_100.png) — a plump, rounded body a little
    narrower at the top, a plaid flap over the top falling down the rear face with a strap and buckle, a plaid pocket on
    the rear, a patch on the side, a rope loop with a tin can on a string."""
    (cx, cy, cz), (w, d, h) = PACKS[0]
    z0 = BACK_Z
    top, rear = z0 + h, cy + d / 2
    body = S.rbox((w - 0.02, d - 0.02, h), (cx, cy, z0 + h / 2), r=0.07, seg=4, taper=(0.86, 0.88))
    objs = [S.part("Sack_L0", [body])]
    # the flap: over the top and down the rear face, its edge rounded, one piece in two parts that overlap at the bend
    flap = [S.rbox((w * 0.8, d * 0.78, 0.024), (cx, cy + 0.012, top - 0.006), r=0.011, seg=2),
            S.rbox((w * 0.72, 0.024, 0.09), (cx, rear - 0.012, top - 0.05), r=0.011, seg=2, rot=(math.radians(-6), 0, 0))]
    pocket = [S.rbox((0.13, 0.03, 0.075), (cx, rear - 0.006, z0 + 0.06), r=0.014, seg=2)]
    patch = [S.rbox((0.008, 0.07, 0.06), (cx + (w - 0.02) / 2 - 0.012, cy - 0.02, z0 + 0.09), r=0.003, seg=1, rot=(math.radians(-8), 0, 0))]
    objs.append(S.part("Canvas_L0", flap + pocket + patch))
    straps = [harness(0, w, d), S.rbox((0.026, 0.012, 0.12), (cx, rear + 0.002, top - 0.06), r=0.004, seg=1),
              S.tube([(cx - 0.035, cy + 0.01, top + 0.004), (cx - 0.03, cy + 0.01, top + 0.04), (cx + 0.03, cy + 0.01, top + 0.04), (cx + 0.035, cy + 0.01, top + 0.004)], 0.009, res=3)]
    objs.append(S.part("Strap_L0", straps))
    objs.append(S.part("Metal_L0", [S.rbox((0.038, 0.012, 0.028), (cx, rear + 0.008, top - 0.085), r=0.004, seg=1)]))
    # a rope loop on the left side with a tin can on a string hanging from it (it swings: DangleA)
    sx = cx - (w - 0.02) / 2 + 0.006
    objs.append(S.part("Rope_L0", [S.tube([(sx, cy - 0.04, top - 0.03), (sx - 0.022, cy - 0.04, top - 0.05), (sx, cy - 0.04, top - 0.07)], 0.006, res=2)]))
    objs.append(S.part("Rope_L0DangleA", [S.tube([(sx - 0.02, cy - 0.04, top - 0.05), (sx - 0.022, cy - 0.04, top - 0.1)], 0.003, res=1)]))
    can = S.lathe([(0.0, 0.0), (0.024, 0.0), (0.026, 0.004), (0.026, 0.048), (0.024, 0.052), (0.0, 0.052)], seg=16, loc=(sx - 0.022, cy - 0.04, top - 0.15))
    objs.append(S.part("Mug_L0DangleA", [can]))
    return objs


def pack1():
    """Level 1: the plaid pack — the canvas body (a little wider at the bottom), its flap pocket on the rear face, the
    handle, the straps over the top, a walkie-talkie in a pouch on the left, a mug and keys on carabiners."""
    cx, cy, cz = PACK_C
    top, rear, bottom = cz + PACK_H / 2, cy + PACK_D / 2, cz - PACK_H / 2
    objs = []
    canvas = [S.rbox((PACK_W, PACK_D, PACK_H), PACK_C, r=0.05, seg=3, taper=(0.9, 0.94)),
              S.rbox((0.21, 0.06, 0.13), (cx, rear + 0.014, cz - 0.035), r=0.024, seg=2),
              S.rbox((0.22, 0.07, 0.045), (cx, rear + 0.018, cz + 0.04), r=0.018, seg=2)]
    objs.append(S.part("Canvas_L1", canvas))
    straps = [S.rbox((0.034, PACK_D + 0.012, 0.012), (sx, cy, top - 0.002), r=0.004, seg=1) for sx in (-0.095, 0.095)]
    straps += [S.rbox((0.034, 0.012, 0.19), (sx, rear + 0.002, cz + 0.02), r=0.004, seg=1) for sx in (-0.095, 0.095)]
    straps += [S.rbox((PACK_W * 0.86, PACK_D * 0.9, 0.022), (cx, cy, bottom + 0.004), r=0.008, seg=1)]
    straps += [S.tube([(-0.05, cy + 0.06, top - 0.005), (-0.045, cy + 0.06, top + 0.05), (0.045, cy + 0.06, top + 0.05), (0.05, cy + 0.06, top - 0.005)], 0.012, res=3)]
    rx = PACK_W / 2 + 0.003
    straps += [S.rbox((0.012, 0.058, 0.03), (rx + 0.022, cy - 0.125, cz - 0.01), r=0.004, seg=1)]
    objs.append(S.part("Strap_L1", straps))
    objs.append(S.part("Metal_L1", [S.rbox((0.03, 0.012, 0.024), (sx, rear + 0.044, cz - 0.035), r=0.003, seg=1) for sx in (-0.035, 0.035)]
                       + [S.rbox((0.046, 0.016, 0.03), (sx, rear + 0.008, cz + 0.085), r=0.004, seg=1) for sx in (-0.095, 0.095)]))
    objs.append(S.part("Radio_L1", [S.rbox((0.03, 0.05, 0.085), (rx + 0.006, cy - 0.125, cz + 0.0), r=0.01, seg=2),
                                     S.rcyl(0.006, 0.06, loc=(rx + 0.006, cy - 0.135, cz + 0.07), axis="Z", seg=10, r=0.002)]))
    objs += dangles(1, rear, cz + 0.085)
    return objs


def pack2():
    """Level 2: a hiking pack — a taller plaid body with a lid, two bulging side pouches with flaps, a rear pocket,
    compression straps and buckles, a red bedroll across the top at the head end, a walkie-talkie, mug and keys."""
    (cx, cy, cz), (w, d, h) = PACKS[2]
    top, rear = cz + h / 2, cy + d / 2
    canvas = [S.rbox((w, d, h), (cx, cy, cz), r=0.06, seg=3, taper=(0.92, 0.95)),
              S.rbox((w * 0.9, d * 0.88, 0.07), (cx, cy + 0.01, top + 0.012), r=0.03, seg=2),                     # the lid
              S.rbox((0.23, 0.07, 0.16), (cx, rear + 0.016, cz - 0.05), r=0.03, seg=2),                            # rear pocket
              S.rbox((0.24, 0.08, 0.05), (cx, rear + 0.02, cz + 0.035), r=0.02, seg=2)]                            # its flap
    for sx in (-1, 1):
        canvas.append(S.rbox((0.09, 0.24, 0.22), (sx * (w / 2 + 0.024), cy + 0.02, cz - 0.06), r=0.04, seg=2, taper=(0.85, 0.95)))  # side pouch
        canvas.append(S.rbox((0.094, 0.245, 0.055), (sx * (w / 2 + 0.024), cy + 0.02, cz + 0.055), r=0.022, seg=2))  # its flap
    objs = [S.part("Canvas_L2", canvas)]
    straps = [harness(2, w, d)]
    straps += [S.rbox((0.036, d * 0.9 + 0.012, 0.012), (sx, cy + 0.01, top + 0.048), r=0.004, seg=1) for sx in (-0.1, 0.1)]       # over the lid
    straps += [S.rbox((0.036, 0.012, 0.24), (sx, rear + 0.004, cz + 0.02), r=0.004, seg=1) for sx in (-0.1, 0.1)]                 # down the rear
    for sx in (-1, 1):
        straps += [S.rbox((0.012, d * 0.86, 0.03), (sx * (w / 2 - 0.004), cy, cz + z), r=0.004, seg=1) for z in (0.07, -0.13)]   # compression
    straps += [S.rcyl(0.084, 0.026, loc=(sx, cy - 0.09, top + 0.125), axis="X", seg=20, r=0.004) for sx in (-0.13, 0.13)]       # round the roll
    straps += [S.tube([(-0.05, cy + 0.08, top + 0.045), (-0.045, cy + 0.08, top + 0.09), (0.045, cy + 0.08, top + 0.09), (0.05, cy + 0.08, top + 0.045)], 0.012, res=3)]
    objs.append(S.part("Strap_L2", straps))
    metal = [S.rbox((0.048, 0.016, 0.032), (sx, rear + 0.012, cz + 0.09), r=0.004, seg=1) for sx in (-0.1, 0.1)]
    metal += [S.rbox((0.032, 0.012, 0.024), (sx, rear + 0.056, cz - 0.04), r=0.003, seg=1) for sx in (-0.04, 0.04)]
    metal += [S.rbox((0.04, 0.03, 0.03), (sx, rear - 0.006, cz + 0.12), r=0.004, seg=1) for sx in (-0.14, 0.14)]                   # the carabiners' loops
    objs.append(S.part("Metal_L2", metal))
    objs.append(S.part("Roll_L2", [S.rcyl(0.078, w + 0.1, loc=(cx, cy - 0.09, top + 0.125), axis="X", seg=24, r=0.022)]))
    rx = w / 2 + 0.072
    objs.append(S.part("Radio_L2", [S.rbox((0.03, 0.05, 0.085), (rx, cy - 0.06, cz + 0.08), r=0.01, seg=2),
                                     S.rcyl(0.006, 0.06, loc=(rx, cy - 0.07, cz + 0.15), axis="Z", seg=10, r=0.002)]))
    objs += dangles(2, rear - 0.006, cz + 0.12, sx=0.14)
    return objs


def pack3():
    """Level 3: a frame pack — a boxy plaid body in an aluminium cage (posts at the corners, a rail round the top and the
    bottom), two pockets a side, a bungee net over the rear face, a bedroll at the head end of the top, a radio with a
    tall antenna on the top corner, a lantern on the left side, mug and keys on carabiners."""
    (cx, cy, cz), (w, d, h) = PACKS[3]
    top, rear, bottom = cz + h / 2, cy + d / 2, cz - h / 2
    canvas = [S.rbox((w, d, h), (cx, cy, cz), r=0.05, seg=3, taper=(0.96, 0.97))]
    for sx in (-1, 1):
        for sy in (-1, 1):
            canvas.append(S.rbox((0.07, 0.16, 0.17), (sx * (w / 2 + 0.02), cy + sy * 0.095, cz - 0.07), r=0.03, seg=2))
            canvas.append(S.rbox((0.077, 0.165, 0.045), (sx * (w / 2 + 0.022), cy + sy * 0.095, cz + 0.02), r=0.018, seg=2))
    objs = [S.part("Canvas_L3", canvas)]
    # the cage: a rail round the bottom and the top, a post at every corner (tubes into one another)
    fw, fd = w / 2 + 0.006, d / 2 + 0.006           # (the tubes bite into the canvas: the cage holds it)
    rails = []
    for z in (bottom + 0.012, top + 0.012):
        loop = [(-fw, -fd, z), (fw, -fd, z), (fw, fd, z), (-fw, fd, z), (-fw, -fd, z)]
        rails.append(S.tube([(x, cy + y, zz) for x, y, zz in loop], 0.012, res=1, smooth=False))
    for sx in (-1, 1):
        for sy in (-1, 1):
            rails.append(S.tube([(sx * fw, cy + sy * fd, bottom), (sx * fw, cy + sy * fd, top + 0.03)], 0.013, res=1, smooth=False))
            rails.append(S.rcyl(0.018, 0.03, loc=(sx * fw, cy + sy * fd, top + 0.012), axis="Z", seg=12, r=0.004))    # corner caps
    objs.append(S.part("Metal_L3", rails))
    straps = [harness(3, w, d)]
    straps += [S.rcyl(0.07, 0.028, loc=(sx, cy - 0.12, top + 0.06), axis="X", seg=20, r=0.004) for sx in (-0.16, 0.16)]
    straps += [S.rbox((0.012, d * 0.9, 0.032), (sx * (w / 2 - 0.004), cy, cz + 0.12), r=0.004, seg=1) for sx in (-1, 1)]
    # the bungee net on the rear face: cords across and down, held at the cage
    net = []
    for k in (-1, 0, 1):
        net.append(S.tube([(k * w * 0.27, rear + 0.008, bottom + 0.05), (k * w * 0.27, rear + 0.008, top - 0.04)], 0.005, res=1, smooth=False))
        net.append(S.tube([(-w * 0.44, rear + 0.008, cz + k * h * 0.27), (w * 0.44, rear + 0.008, cz + k * h * 0.27)], 0.005, res=1, smooth=False))
    objs.append(S.part("Strap_L3", straps))
    objs.append(S.part("Red_L3", net))
    objs.append(S.part("Roll_L3", [S.rcyl(0.065, w + 0.1, loc=(cx, cy - 0.12, top + 0.06), axis="X", seg=24, r=0.018)]))
    # the radio on the top's rear right corner, its antenna up
    rx, ry = w / 2 - 0.06, rear - 0.07
    objs.append(S.part("Radio_L3", [S.rbox((0.07, 0.05, 0.11), (rx, ry, top + 0.05), r=0.012, seg=2),
                                     S.rcyl(0.008, 0.22, loc=(rx + 0.02, ry, top + 0.21), axis="Z", seg=8, r=0.003),
                                     S.rcyl(0.009, 0.012, loc=(rx + 0.02, ry, top + 0.32), axis="Z", seg=10, r=0.003)]))
    # DangleC: a lantern hanging from the cage's top rail on the left side
    lx, ly, lz = -fw - 0.002, cy + 0.02, top - 0.02
    lantern = [S.tube([(lx - 0.02, ly - 0.015, lz - 0.022), (lx - 0.004, ly, lz + 0.03), (lx - 0.02, ly + 0.015, lz - 0.022)], 0.004, res=2),
               S.rcyl(0.035, 0.012, loc=(lx - 0.02, ly, lz - 0.025), axis="Z", seg=14, r=0.004),
               S.rcyl(0.04, 0.014, loc=(lx - 0.02, ly, lz - 0.135), axis="Z", seg=14, r=0.004)]
    lantern += [S.tube([(lx - 0.02 + 0.03 * math.cos(a), ly + 0.03 * math.sin(a), lz - 0.03), (lx - 0.02 + 0.03 * math.cos(a), ly + 0.03 * math.sin(a), lz - 0.13)], 0.004, res=1, smooth=False)
                for a in (0.6, 2.2, 3.8, 5.4)]
    objs.append(S.part("Brass_L3DangleC", lantern))
    objs.append(S.part("Mug_L3DangleC", [S.rcyl(0.026, 0.09, loc=(lx - 0.02, ly, lz - 0.08), axis="Z", seg=14, r=0.008)]))
    objs += dangles(3, rear, cz + 0.16, sx=0.12)
    return objs


def strap():
    """raccoon_strap.fbx: the strap that holds a thing to the pack (RaccoonPack sizes it to each thing): three unit boxes
    the game scales — Strap_Top over the thing, Strap_SideL / Strap_SideR down its sides into the pack — and a buckle on
    the top (Metal_Buckle, its own size). All at the origin; nothing here is placed."""
    unit = lambda: S.rbox((1.0, 1.0, 1.0), (0, 0, 0), r=0.18, seg=1)
    buckle = [S.rbox((0.034, 0.03, 0.006), (0, 0, 0), r=0.002, seg=1),
              S.rbox((0.004, 0.032, 0.009), (0, 0, 0.002), r=0.0012, seg=1)]
    return [S.part("Strap_Top", [unit()]), S.part("Strap_SideL", [unit()]), S.part("Strap_SideR", [unit()]), S.part("Metal_Buckle", buckle)]


def carabiner(x, y, z):
    """A D-shaped climbing carabiner hanging in the x-z plane, its top at z."""
    pts = []
    for i in range(10):
        a = TAU * i / 10
        px = 0.032 * math.cos(a) * (1.0 if math.cos(a) < 0 else 0.8)
        pz = -0.05 + 0.05 * math.sin(a)
        pts.append((x + px, y, z + pz))
    pts.append(pts[0])
    return S.tube(pts, 0.0075, res=1)


def mug(x, y, z, k=1.0):
    body = S.lathe([(0.0, -0.024 * k), (0.026 * k, -0.024 * k), (0.028 * k, -0.018 * k), (0.028 * k, 0.024 * k), (0.024 * k, 0.024 * k),
                    (0.022 * k, -0.016 * k), (0.0, -0.016 * k)], seg=20, loc=(x, y, z))
    handle = S.tube([(x, y - 0.02 * k, z + 0.018 * k), (x, y - 0.042 * k, z + 0.016 * k), (x, y - 0.042 * k, z - 0.012 * k),
                     (x, y - 0.02 * k, z - 0.014 * k)], 0.005 * k, res=2)
    ring = S.tube([(x, y - 0.03 * k, z + 0.03 * k), (x, y - 0.03 * k, z + 0.05 * k + 0.02)], 0.003 * k, res=1)
    return [body, handle, ring]


def keys(x, y, z, k=1.0):
    ring = S.tube([(x + 0.012 * k * math.cos(TAU * i / 10), y, z + 0.02 * k + 0.012 * k * math.sin(TAU * i / 10)) for i in range(11)], 0.0022 * k, res=1)
    out = [ring, S.tube([(x, y, z + 0.032 * k), (x, y, z + 0.032 * k + 0.03)], 0.0025, res=1)]
    for j, a in enumerate((-22, 0, 25)):
        r = math.radians(a)
        cx, cz = x + 0.03 * k * math.sin(r), z - 0.005 * k - 0.03 * k * math.cos(r)
        out.append(S.rbox((0.012 * k, 0.004 * k, 0.04 * k), (cx, y, cz), r=0.0015, seg=1, rot=(0, -r, 0)))
        out.append(S.rcyl(0.009 * k, 0.0056 * k, loc=(x + 0.012 * k * math.sin(r), y + 0.0013 * k * (j - 1), z + 0.008 * k - 0.012 * k * math.cos(r)),
                          axis="Y", seg=12, r=0.0015))
    return out


# --------------------------------------------------------------------------- previews and export

def preview_body(rig, body, acts, path, extra=()):
    mat = bpy.data.materials.new("raccoon_preview")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(os.path.join(TEX, "raccoon_base.png"), check_existing=True)
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    nt.nodes.active = img
    body.data.materials.clear()
    body.data.materials.append(mat)
    base, ext = os.path.splitext(path)
    shots = [("front", (0, -1, 0.15), (0, -0.05, 0.35), 0.9), ("3/4", (0.7, -0.7, 0.25), (0, 0.05, 0.35), 1.1),
             ("side", (1, 0, 0.05), (0, 0.06, 0.36), 1.0), ("back", (0.2, 1, 0.3), (0, 0.1, 0.35), 0.9),
             ("top", (0.1, 0.2, 1), (0, 0.06, 0.3), 1.6)]
    view.sheet(base + "_body" + ext, [body, *extra], shots=shots, width=360, height=360, color="TEXTURE")
    view.sheet(base + "_face" + ext, [body], shots=[("face", (0, -1, 0.1), (0, -0.48, 0.5), 0.36), ("face 3/4", (0.65, -0.75, 0.1), (0, -0.48, 0.5), 0.36),
                                                   ("profile", (1, 0, 0), (0, -0.48, 0.5), 0.36)], width=360, height=360, color="TEXTURE")
    s = bpy.context.scene
    tiles = []
    for a in acts:
        rig.animation_data.action = a
        n = int(a.frame_range[1])
        for share in (0.0, 0.25, 0.5, 0.75):
            s.frame_set(1 + int((n - 1) * share))
            tmp = f"{base}_take_{a.name}_{int(share * 100)}{ext}"
            view.sheet(tmp, [body, *extra], shots=[("side", (1, -0.25, 0.15), (0, 0.06, 0.45), 1.5)], width=260, height=260, color="TEXTURE")
            tiles.append(tmp)
    rig.animation_data.action = None
    s.frame_set(1)
    rows = []
    for i in range(0, len(tiles), 8):
        imgs = [bpy.data.images.load(t) for t in tiles[i:i + 8]]
        px = [np.array(im.pixels[:], np.float32).reshape(im.size[1], im.size[0], 4) for im in imgs]
        while len(px) < 8:
            px.append(np.full_like(px[0], 0.2))
        rows.append(np.concatenate(px, 1))
        for im in imgs:
            bpy.data.images.remove(im)
    grid = np.concatenate(rows[::-1], 0)
    out = bpy.data.images.new("_takes", grid.shape[1], grid.shape[0], alpha=False)
    out.pixels.foreach_set(grid.ravel())
    out.filepath_raw = base + "_takes" + ext
    out.file_format = "PNG"
    out.save()
    for t in tiles:
        os.remove(t)
    body.data.materials.clear()


PACK_COLOURS = {"Canvas": (0.80, 0.62, 0.16), "Strap": (0.27, 0.30, 0.17), "Metal": (0.6, 0.6, 0.62), "Red": (0.75, 0.12, 0.08),
                "Blue": (0.12, 0.35, 0.8), "Mug": (0.92, 0.92, 0.9), "Brass": (0.72, 0.58, 0.25), "Radio": (0.33, 0.42, 0.2),
                "Roll": (0.55, 0.16, 0.12), "Sack": (0.70, 0.60, 0.44), "Rope": (0.74, 0.62, 0.40)}


def preview_together(rig, body, objs, acts, path):
    """The pack on the bone "pack" (as the game hangs it) over a few frames of every take."""
    for o in objs:
        mw = o.matrix_world.copy()
        o.parent = rig
        o.parent_type = "BONE"
        o.parent_bone = "pack"
        bpy.context.view_layer.update()
        o.matrix_world = mw
    preview_body(rig, body, acts, path, extra=objs)


def export_body(rig, body):
    os.makedirs(OUT, exist_ok=True)
    body.data.materials.clear()
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    body.select_set(True)
    path = os.path.join(OUT, "raccoon.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, object_types={"ARMATURE", "MESH"},
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False, add_leaf_bones=False,
                             use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    print("exported", path)


def write_plaid(path, size=128):
    """The canvas's mustard plaid (tiles every 12.5 cm on the pack: the builder sets the tiling)."""
    u = np.arange(size)[None, :] / size
    v = np.arange(size)[:, None] / size
    base = np.array((0.80, 0.60, 0.16))
    def band(t, at, w):
        return (np.abs(((t - at + 0.5) % 1.0) - 0.5) < w).astype(float)
    wide = np.maximum(band(u, 0.25, 0.07), band(v, 0.25, 0.07)) * 0.10 + band(u, 0.25, 0.07) * band(v, 0.25, 0.07) * 0.06
    thin = np.maximum(band(u, 0.75, 0.012), band(v, 0.75, 0.012))
    weave = 0.03 * (((np.arange(size)[None, :] + np.arange(size)[:, None]) % 4) < 2)
    rgb = base[None, None, :] * (1 - wide[..., None] - weave[..., None])
    rgb = rgb * (1 - thin[..., None]) + np.array((0.93, 0.80, 0.42))[None, None, :] * thin[..., None]
    img = bpy.data.images.new("plaid", size, size, alpha=False)
    px = np.concatenate([np.clip(rgb, 0, 1) ** 2.2, np.ones((size, size, 1))], -1)   # (sRGB file from linear pixels)
    img.pixels.foreach_set(px.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def main():
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    errors = 0
    rig = body = None
    if ONLY in (None, "body"):
        rig, body, errors = assemble_body()
        acts = animate(rig)
        export_body(rig, body)
    objs = []
    if ONLY in (None, "pack"):
        objs = backpack()
        for o in objs:
            o.color = (*PACK_COLOURS[o.name.split("_")[0]], 1)
        print("PACK " + ", ".join(f"{o.name} {sdf.tri_count(o)}" for o in objs) + f" | total {sum(sdf.tri_count(o) for o in objs)} tris")
        S.export_fbx(os.path.join(OUT, "raccoon_pack.fbx"), objs)
        S.export_fbx(os.path.join(OUT, "raccoon_strap.fbx"), strap())
        os.makedirs(TEX, exist_ok=True)
        write_plaid(os.path.join(TEX, "raccoon_plaid.png"))
        if PREVIEW:
            base, ext = os.path.splitext(PREVIEW)
            view.sheet(base + "_pack" + ext, objs, shots=[("rear 3/4", (0.6, 0.8, 0.45), PACK_C, 0.5), ("side", (1, 0, 0.1), PACK_C, 0.5),
                                                          ("top", (0.05, 0.1, 1), PACK_C, 0.5), ("front 3/4", (-0.6, -0.7, 0.4), PACK_C, 0.5)],
                       width=360, height=360)
    if PREVIEW and body is not None:
        if objs:
            for o in objs:                                      # (the sheets draw textures: a flat material of its colour)
                m = bpy.data.materials.new("prev_" + o.name)
                m.diffuse_color = o.color
                m.use_nodes = True
                next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = o.color
                o.data.materials.append(m)
            preview_together(rig, body, objs, acts, PREVIEW)
        else:
            preview_body(rig, body, acts, PREVIEW)
    print(f"DONE raccoon in {time.time() - t0:.1f} s" + (f" WITH {errors} TOPOLOGY ERRORS" if errors else ""))


if __name__ == "__main__":
    main()
