"""The land around a house (prefab Level/OutsideTerrain, placed by LevelBuilder centred on the house): visual only.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/terrain.py -- Assets/_Game/Art/Models

Writes Level/outside_terrain.fbx: one mesh, Body, a disc 650 m in radius made of rings (fine near the middle, coarse
far out). Flat for FLAT metres round the middle, level with the yard (the house and its yard stand on the ground block
there); then rolling mounds and heaps of rubble a few metres high; far out a ring of long low ridges that rise
smoothly — the horizon is the ridges sinking into the gas, not the edge of a block. UVs in metres (top projection).
Deterministic (fixed phases).
"""
import math
import os
import random
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODELS = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))
RADIUS = 650.0
FLAT = 80.0
BASE = -0.02  # the top of the ground block under the yard (LevelBuilder: centre −0.07, 0.1 thick)
RINGS, SEGS = 110, 192

rng = random.Random(7)
WAVES = [(rng.uniform(0, math.tau), rng.uniform(0.012, 0.05), rng.uniform(0, math.tau), rng.uniform(0.6, 1.0)) for _ in range(9)]
# heaps of rubble scattered between the yard and the ridges: (x, z, radius, height)
HEAPS = []
while len(HEAPS) < 160:
    a, r = rng.uniform(0, math.tau), rng.uniform(FLAT + 15.0, 330.0)
    HEAPS.append((r * math.cos(a), r * math.sin(a), rng.uniform(4.0, 14.0), rng.uniform(0.8, 4.5)))
RIDGE = [(k, rng.uniform(0.3, 1.0) / k ** 0.7, rng.uniform(0, math.tau)) for k in range(2, 11)]


def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def height(x, z):
    r = math.hypot(x, z)
    if r <= FLAT:
        return BASE
    theta = math.atan2(z, x)
    # mounds: long soft waves in random directions, a few metres high
    mounds = sum(a * math.sin((x * math.cos(d) + z * math.sin(d)) * f + p) for d, f, p, a in WAVES) / len(WAVES)
    mounds = (mounds + 0.35) * 5.0
    # rubble: heaps here and there
    bumps = 0.0
    for hx, hz, hr, hh in HEAPS:
        d2 = ((x - hx) ** 2 + (z - hz) ** 2) / (hr * hr)
        if d2 < 4.0:
            bumps = max(bumps, hh * math.exp(-d2 * 1.6))
    near = smoothstep(FLAT, FLAT + 70.0, r) * (max(0.0, mounds) + bumps)
    # far ridges: a smooth ring, higher and lower round the horizon
    ridge = 16.0 + 9.0 * sum(a * math.sin(k * theta + p) for k, a, p in RIDGE)
    far = smoothstep(230.0, 480.0, r) * max(4.0, ridge)
    return BASE + near * (1.0 - 0.6 * smoothstep(300.0, 450.0, r)) + far


def build():
    s.clear_scene()
    verts, faces, uvs = [(0.0, 0.0, BASE)], [], []
    radii = [RADIUS * (i / RINGS) ** 1.7 for i in range(1, RINGS + 1)]
    for r in radii:
        for k in range(SEGS):
            a = math.tau * k / SEGS
            x, z = r * math.cos(a), r * math.sin(a)
            verts.append((x, z, height(x, z)))       # Blender: x, y (= Unity z), z up
    for k in range(SEGS):
        faces.append((0, 1 + k, 1 + (k + 1) % SEGS))
    for i in range(RINGS - 1):
        a0, a1 = 1 + i * SEGS, 1 + (i + 1) * SEGS
        for k in range(SEGS):
            k1 = (k + 1) % SEGS
            faces.append((a0 + k, a1 + k, a1 + k1, a0 + k1))
    mesh = bpy.data.meshes.new("Body")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    for p in mesh.polygons:                          # every face looks up
        if p.normal.z < 0:
            p.flip()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        v = verts[loop.vertex_index]
        uv.data[loop.index].uv = (v[0], v[1])        # metres
    for p in mesh.polygons:
        p.use_smooth = True
    obj = bpy.data.objects.new("Body", mesh)
    bpy.context.collection.objects.link(obj)
    path = os.path.join(MODELS, "Level", "outside_terrain.fbx")
    s.export_fbx(path, [obj])
    print("exported", path, len(verts), "verts")


build()
