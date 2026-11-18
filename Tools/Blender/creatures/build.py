"""Builds the creature robots: the base models of every robot and security device (except the spider), the same in
every visual style. Lore: the house AI learned from its own output and from human films and books, so its machines
grew into animals and things from the deep — but they keep the original models' part contract, so the game drives
them as before (bestiary/STYLES.md, checked here with build_bestiary.check_contract).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/creatures/build.py -- <models_root>
          [--only robot_guard,robot_drone] [--contract <dir with Robots/ and Sensors/ of the original models>]

Writes <models_root>/Robots/<name>.fbx and <models_root>/Sensors/<name>.fbx (the game's file names; <models_root> is
Assets/_Game/Art/Models for the game, a scratch folder while iterating). --contract defaults to the game's models —
pass a copy of the old models when the game's files are already the creatures.

A design is a function design(L) -> designs.Parts in one of the modules below (walkers, rollers, devices); L is the
creature language (common.Creature).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
BLENDER = os.path.dirname(HERE)
for p in (HERE, BLENDER, os.path.join(BLENDER, "bestiary")):
    if p not in sys.path:
        sys.path.insert(0, p)

import bpy  # noqa: E402

import build_bestiary as bb  # noqa: E402
import meshfix  # noqa: E402
import shapes as s  # noqa: E402
import importlib  # noqa: E402

import common  # noqa: E402

DESIGNS = {}
for _mod in ("walkers", "rollers", "devices"):
    try:
        DESIGNS.update(getattr(importlib.import_module(_mod), "DESIGNS", {}))
    except Exception as e:  # one broken module must not stop building the others
        print(f"CREATURES MODULE {_mod} failed to load: {e!r}")

# new models with no original to compare with
NEW = {"robot_storage"}

# the original's object origins that are not the world origin (poi.py builds the warden with pivots on its parts)
ORIGINS = {
    "robot_warden": {"AccentHead": (0, 0, 1.3), "AccentWheel0": (-.24, -.2, .085), "AccentWheel1": (.24, -.2, .085),
                     "AccentWheel2": (-.24, .2, .085), "AccentWheel3": (.24, .2, .085)},
}


def set_origin(obj, origin):
    from mathutils import Matrix, Vector
    o = Vector(origin)
    obj.data.transform(Matrix.Translation(-o))
    obj.matrix_world = Matrix.Translation(o) @ obj.matrix_world


def build(name, L):
    s.clear_scene()
    P = DESIGNS[name](L)
    if name not in NEW:
        P = bb.fit_roles(name, P)
    objs = []
    for role, bms in P.items():
        bms = [b for b in bms if b is not None]
        if not bms:
            continue
        obj = s.part(role, bms, sharp_angle=L.sharp)
        if role in P.split:
            objs += s.split_at(obj, P.split[role])
        else:
            objs.append(obj)
    for o in objs:
        o.data.name = o.name
    meshfix.fix_objects(objs, floor=False, name=name, pinned=lambda n: n.startswith(bb.MOVING + ("AccentLift", "BodyLift")))
    for part, origin in ORIGINS.get(name, {}).items():
        for o in objs:
            if o.name == part:
                set_origin(o, origin)
    return objs


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = argv[0] if argv and not argv[0].startswith("--") else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
    opt = {argv[i]: argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith("--") else "" for i in range(len(argv))
           if argv[i].startswith("--")}
    only = opt["--only"].split(",") if opt.get("--only") else list(DESIGNS)
    if opt.get("--contract"):
        bb.REPO_MODELS = os.path.abspath(opt["--contract"])
    contract_root = bb.REPO_MODELS
    L = common.Creature()
    errors = 0
    # the lift first: the worker's sweep check swings the lift written here
    for name in sorted(only, key=lambda n: n != "worker_lift"):
        objs = build(name, L)
        if name not in NEW:
            if name == "robot_worker" and os.path.exists(os.path.join(root, "Robots", "worker_lift.fbx")):
                bb._lift.clear()
                bb.REPO_MODELS = root
                bb.lift_meshes()
                bb.REPO_MODELS = contract_root
            n = bb.check_contract(name, objs, print)
            errors += n
        tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs}
        print(f"MODEL {name}: {sum(tris.values())} tris  " + "  ".join(f"{k} {v}" for k, v in sorted(tris.items())))
        s.export_fbx(os.path.join(root, bb.category(name) if name not in NEW else "Robots", name + ".fbx"), objs)
    print(f"CREATURES DONE errors={errors}")
    if errors:
        sys.exit(1)


if __name__ == "__main__":
    main()
