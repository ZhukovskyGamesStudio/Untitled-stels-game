"""Builds the robots, devices and sensor modules of every visual style from bestiary/ (designs × style languages).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/build_bestiary.py -- <models_root>
          [--styles S05_Toon,S11_Noir] [--only robot_drone,robot_guard] [--set robots|props|all] [--base]

Writes <models_root>/Styles/PerStyle/<style>/<model>__<style>.fbx (the asset builder maps them onto the original models
by mesh name for that style only: AssetBuilder.BuildStyleModels). --base also writes the neutral language as style
"Base" (for comparison; the game does not load it). Every model is checked against the original's contract (part
names, wheel/rotor centres, leg joints) — a broken contract is an error, the exit code is 1.

A style is bestiary/styles/<style>.py with `class Style(Lang)` and optionally DESIGNS = {model: function(L)} that
replace the shared designs for that style.
"""
import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "bestiary"))

import bpy  # noqa: E402

import meshfix  # noqa: E402
import shapes as s  # noqa: E402
import designs  # noqa: E402
import props  # noqa: E402
from lang import Lang  # noqa: E402

REPO_MODELS = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Game", "Art", "Models"))
STYLES = ["S01_NightShift", "S02_Scrapyard", "S03_Heist", "S04_Hunted", "S05_Toon", "S06_Storybook", "S07_Logbook",
          "S08_Footage", "S09_WhiteRoom", "S10_Grid", "S11_Noir", "S12_TestChamber", "S13_Comic", "S14_Thermal",
          "S15_Blueprint", "S16_Derelict"]


LEVEL = ("hatch", "window", "ceiling_lamp", "exit_sign")


def category(name):
    if name.startswith("robot_") or name.startswith("attach_"):
        return "Robots"
    if name.startswith("sensor_") or name in ("security_camera", "laser_emitter"):
        return "Sensors"
    return "Level" if name in LEVEL else "Props"


_contracts = {}


def contract(name):
    """Part name -> (min, max) bounds of the original model (imported once)."""
    if name in _contracts:
        return _contracts[name]
    path = os.path.join(REPO_MODELS, category(name), name + ".fbx")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    out = {}
    for o in set(bpy.data.objects) - before:
        if o.type == "MESH":
            ws = [o.matrix_world @ v.co for v in o.data.vertices]
            out[o.name.split(".")[0]] = (tuple(min(w[i] for w in ws) for i in range(3)), tuple(max(w[i] for w in ws) for i in range(3)))
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    _contracts[name] = out
    return out


def bounds(obj):
    ws = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return tuple(min(w[i] for w in ws) for i in range(3)), tuple(max(w[i] for w in ws) for i in range(3))


def check_contract(name, objs, report):
    """Part names equal to the original's; spinning parts centred where the original's were; legs reach the same
    hip and floor. Returns the number of errors."""
    want = contract(name)
    have = {o.name: o for o in objs}
    errors = 0
    missing, extra = set(want) - set(have), set(have) - set(want)
    if category(name) in ("Props", "Level") and objs:
        lo0 = [min(b[0][k] for b in want.values()) for k in range(3)]
        hi0 = [max(b[1][k] for b in want.values()) for k in range(3)]
        bb = [bounds(o) for o in objs]
        lo1 = [min(b[0][k] for b in bb) for k in range(3)]
        hi1 = [max(b[1][k] for b in bb) for k in range(3)]
        worst = max(max(abs(a - b) for a, b in zip(lo0, lo1)), max(abs(a - b) for a, b in zip(hi0, hi1)))
        if worst > 0.06:
            report(f"  CONTRACT {name}: bounds {[round(v, 2) for v in lo1]}..{[round(v, 2) for v in hi1]}, the original's "
                   f"{[round(v, 2) for v in lo0]}..{[round(v, 2) for v in hi0]} (off by {worst * 100:.0f} cm)")
            errors += 1
    if missing or extra:
        report(f"  CONTRACT {name}: missing {sorted(missing)} extra {sorted(extra)}")
        errors += 1
    for part, (lo, hi) in want.items():
        if part not in have:
            continue
        nlo, nhi = bounds(have[part])
        c0 = [(a + b) / 2 for a, b in zip(lo, hi)]
        c1 = [(a + b) / 2 for a, b in zip(nlo, nhi)]
        if part.startswith(("AccentWheel", "AccentRotor", "AccentLidar")):
            off = max(abs(a - b) for a, b in zip(c0, c1))
            if off > 0.025:
                report(f"  CONTRACT {name}.{part}: centre moved {off * 100:.1f} cm ({c0} -> {c1})")
                errors += 1
        if part.startswith("AccentLeg"):
            if part.endswith("Upper") and not -0.06 < nhi[2] - hi[2] < 0.12:
                report(f"  CONTRACT {name}.{part}: hip at {nhi[2]:.2f}, the original's at {hi[2]:.2f}")
                errors += 1
            if part.endswith("Lower") and abs(lo[2] - nlo[2]) > 0.03:
                report(f"  CONTRACT {name}.{part}: foot at {nlo[2]:.2f}, the original's at {lo[2]:.2f}")
                errors += 1
            for k in (0, 1):
                if abs(c0[k] - c1[k]) > 0.08:
                    report(f"  CONTRACT {name}.{part}: {'xy'[k]} {c1[k]:.2f}, the original's {c0[k]:.2f}")
                    errors += 1
    return errors


def load_style(style):
    if style == "Base":
        return Lang(), {}
    mod = importlib.import_module("styles." + style)
    return mod.Style(), getattr(mod, "DESIGNS", {})


MOVING = ("AccentWheel", "AccentRotor", "AccentLidar", "AccentHead", "AccentLeg")


def fit_roles(name, P):
    """Pieces of a role the original model does not have go to the nearest role it has (Accent <-> Body): the game
    shows only the original's parts."""
    have = set(contract(name))
    for role in list(P):
        if role in have or role + "Upper" in have:
            continue
        to = next((r for r in ("Accent", "Body", "Eye") if r in have and r != role), None)
        if to:
            P.add(to, P.pop(role))
    return P


def build(name, L, design):
    s.clear_scene()
    P = design(L)
    P = L.signature(name, P) or P
    P = fit_roles(name, P)
    objs = []
    for role, bms in P.items():
        bms = [L.post(b) for b in bms if b is not None]
        if not bms:
            continue
        obj = s.part(role, bms, sharp_angle=L.sharp, flat=L.flat)
        if role in P.split:
            objs += s.split_at(obj, P.split[role])
        else:
            objs.append(obj)
    for o in objs:
        o.data.name = o.name
    meshfix.fix_objects(objs, floor=category(name) in ("Props", "Level"), name=f"{name}__{L.name}",
                        pinned=lambda n: n.startswith(MOVING))
    objs += fillers(name, objs)
    return objs


def fillers(name, objs):
    """A part of the original the design has no pieces for (an Accent added to the base model after the style was
    designed) would show the original's mesh in this style: give it a 2 mm speck hidden inside the body instead."""
    have = {o.name for o in objs}
    body = next((o for o in objs if o.name == "Body"), objs[0] if objs else None)
    out = []
    for role in contract(name):
        if role in have or role.startswith(MOVING) or body is None:
            continue
        lo, hi = bounds(body)
        c = [(a + b) / 2 for a, b in zip(lo, hi)]
        out.append(s.part(role, [s.rbox((0.002, 0.002, 0.002), tuple(c), r=0.0, seg=1)]))
        print(f"  FILLER {name}.{role}: no pieces in this style, a hidden speck stands in")
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    root = argv[0] if argv and not argv[0].startswith("--") else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
    opt = {argv[i]: argv[i + 1] if i + 1 < len(argv) and not argv[i + 1].startswith("--") else "" for i in range(len(argv)) if argv[i].startswith("--")}
    styles = opt["--styles"].split(",") if opt.get("--styles") else list(STYLES)
    if "--base" in opt:
        styles = ["Base"] + styles if "--styles" not in opt else styles
    only = set(opt["--only"].split(",")) if opt.get("--only") else None
    which = opt.get("--set") or "robots"
    catalogue = {}
    if which in ("robots", "all"):
        catalogue.update(designs.DESIGNS)
    if which in ("props", "all"):
        catalogue.update(props.PROPS)
    errors = 0
    for style in styles:
        L, own = load_style(style)
        L.name = style
        for name, design in catalogue.items():
            if only and name not in only:
                continue
            objs = build(name, L, own.get(name, design))
            errors += check_contract(name, objs, print)
            path = os.path.join(root, "Styles", "PerStyle", style, f"{name}__{style}.fbx")
            s.export_fbx(path, objs)
    print(f"BESTIARY DONE errors={errors}")
    if errors:
        sys.exit(1)


if __name__ == "__main__":
    main()
