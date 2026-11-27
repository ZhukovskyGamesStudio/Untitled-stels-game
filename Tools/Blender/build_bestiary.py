"""Builds the robots, devices and sensor modules of every visual style from bestiary/ (designs × style languages).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/build_bestiary.py -- <models_root>
          [--styles S06_Storybook,S16_Derelict] [--only robot_drone,robot_guard] [--set robots|props|all] [--base]

Writes <models_root>/Styles/PerStyle/<style>/<model>__<style>.fbx (the asset builder maps them onto the original models
by mesh name for that style only: AssetBuilder.BuildStyleModels). --base also writes the neutral language as style
"Base" (for comparison; the game does not load it). Every model is checked against the original's contract (part
names, wheel/rotor centres, leg joints) — a broken contract is an error, the exit code is 1.

A style is bestiary/styles/<style>.py with `class Style(Lang)` and optionally DESIGNS = {model: function(L)} that
replace the shared designs for that style. The danger-zone robots (designs.DANGER) have no shared design (None): they are
built only for a style that designs them. A part whose original has its origin off the world's (the mimic's lid on
its hinge) gets that origin.
"""
import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "bestiary"))

import math  # noqa: E402

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import meshfix  # noqa: E402
import voxel  # noqa: E402
import shapes as s  # noqa: E402
import designs  # noqa: E402
import props  # noqa: E402
from lang import Lang  # noqa: E402

REPO_MODELS = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Game", "Art", "Models"))
STYLES = ["S06_Storybook", "S16_Derelict"]   # (the game keeps two styles; the others' designs are in the repository's history)


LEVEL = ("hatch", "window", "ceiling_lamp", "exit_sign")

# Furniture that opens in the game (drawers and doors are separate moving parts, Items.Storage): every style shows its
# model family's version, which has those parts; the designs here are single closed bodies and are not built.
OPENING = ("cabinet", "lockers", "fridge", "kitchen_counter", "tv_stand")
# Pieces a style leaves to its model family: the bookshelf holds the printed books (AssetBuilder.Variants), which stand on
# the family's shelves only (the style's own carried a row of blank blue box files).
LEFT = {"S16_Derelict": ("bookshelf",)}


def category(name):
    if name.startswith("robot_") or name.startswith("attach_") or name == "worker_lift":
        return "Robots"
    if name.startswith("sensor_") or name in ("security_camera", "laser_emitter"):
        return "Sensors"
    return "Level" if name in LEVEL else "Props"


_contracts = {}
_origins = {}   # model -> {part: the original's object origin} where it is not the world origin (the mimic's lid hinge)


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
            t = o.matrix_world.to_translation()
            if t.length > 1e-4:
                _origins.setdefault(name, {})[o.name.split(".")[0]] = tuple(t)
            ws = [o.matrix_world @ v.co for v in o.data.vertices]
            lo, hi = tuple(min(w[i] for w in ws) for i in range(3)), tuple(max(w[i] for w in ws) for i in range(3))
            key = o.name.split(".")[0]  # Body and Body.001 are one part: their bounds add up (toilet_stall)
            if key in out:
                lo = tuple(min(a, b) for a, b in zip(out[key][0], lo))
                hi = tuple(max(a, b) for a, b in zip(out[key][1], hi))
            out[key] = (lo, hi)
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
    if name == "worker_lift":
        errors += check_lift(have, want, report)
    if name == "robot_worker":
        errors += check_sweep(objs, report)
    errors += check_anchors(name, objs, want, report)
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


# The worker's lift (CargoMotion): the booms swing about the hinge, the tray stays level; its bottom is TrayHalf under
# the pin and must stay there (the tray lands on the floor with 1 cm to spare).
LIFT_HINGE, LIFT_ARM = Vector((0, -0.15, 0.562)), 0.843
LIFT_FLOOR_ANGLE = 129.6   # CargoMotion.ArmDown


def check_lift(have, want, report):
    errors = 0
    tray, arm = have.get("BodyLiftTray"), have.get("AccentLiftArm")
    if tray is not None and "BodyLiftTray" in want:
        lo, hi = bounds(tray)
        if abs(lo[2] - want["BodyLiftTray"][0][2]) > 0.005:
            report(f"  CONTRACT worker_lift.BodyLiftTray: bottom at {lo[2]:.3f}, CargoMotion wants {want['BodyLiftTray'][0][2]:.3f}")
            errors += 1
        if max(abs(hi[0]), abs(lo[0])) > 0.43:
            report(f"  CONTRACT worker_lift.BodyLiftTray: {max(abs(hi[0]), abs(lo[0])):.2f} wide of the centre (the booms are at 0.4)")
            errors += 1
    if arm is not None and "AccentLiftArm" in want:
        lo, hi = bounds(arm)
        wl, wh = want["AccentLiftArm"]
        off = max(max(abs(a - b) for a, b in zip(lo, wl)), max(abs(a - b) for a, b in zip(hi, wh)))
        if off > 0.04:
            report(f"  CONTRACT worker_lift.AccentLiftArm: bounds off the original's by {off * 100:.0f} cm")
            errors += 1
    return errors


# The danger-zone robots' points the game depends on beyond part names, wheels and legs (designs.py, the DANGER
# section): ("near", point, roles, tol) - geometry of those roles passes within tol of the point; ("clear", lo, hi) -
# nothing inside that box; ("floor", z, tol) - the lowest point of the model; ("bounds", roles, tol) - the union bounds
# of those roles match the original's.
def _danger_anchors():
    A = {name: [("near", eye, ("Eye",), .06)] for name, eye in designs.DANGER_EYES.items()}
    A["robot_banshee"] = [("near", (sx * .08, -.18, 1.8), ("Eye",), .06) for sx in (-1, 1)] + [("floor", 0.0, .02)]
    A["robot_gunner"].append(("near", designs.GUNNER_MUZZLE, ("Accent", "Body"), .04))
    A["robot_gunner"].append(("clear", (-.62, -1.6, .98), (-.38, -1.02, 1.22)))          # the line of fire
    A["robot_driller"] += [("bounds", ("AccentDrill",), .04)]
    A["robot_mimic"] += [("bounds", ("Body", "AccentLid"), .03)]
    A["robot_maniac"].append(("clear",) + designs.MANIAC_CLEAR)
    A["robot_relay"] = [("floor", 0.0, .02)]
    return A


ANCHORS = _danger_anchors()


def check_anchors(name, objs, want, report):
    checks = ANCHORS.get(name)
    if not checks:
        return 0
    bpy.context.view_layer.update()

    def tree(o):
        mw = o.matrix_world
        return BVHTree.FromPolygons([mw @ v.co for v in o.data.vertices], [tuple(p.vertices) for p in o.data.polygons])
    errors = 0
    for c in checks:
        kind = c[0]
        if kind == "near":
            _, p, roles, tol = c
            best = min((tree(o).find_nearest(Vector(p))[3] for o in objs if o.name in roles), default=None)
            if best is None or best > tol:
                report(f"  CONTRACT {name}: no {'/'.join(roles)} within {tol * 100:.0f} cm of {p}"
                       + ("" if best is None else f" (nearest {best * 100:.0f} cm)"))
                errors += 1
        elif kind == "clear":
            lo, hi = Vector(c[1]), Vector(c[2])
            bm = s.rbox(tuple(hi - lo), tuple((lo + hi) / 2), r=0.0, seg=1)
            box = BVHTree.FromBMesh(bm)
            bm.free()
            hits = [o.name for o in objs if tree(o).overlap(box)
                    or any(all(lo[k] < (o.matrix_world @ v.co)[k] < hi[k] for k in range(3)) for v in o.data.vertices)]
            if hits:
                report(f"  CONTRACT {name}: {hits} inside the box {tuple(lo)}..{tuple(hi)} that must stay clear")
                errors += 1
        elif kind == "floor":
            _, z, tol = c
            low = min(bounds(o)[0][2] for o in objs)
            if abs(low - z) > tol:
                report(f"  CONTRACT {name}: the lowest point at z {low:.3f}, the original's at {z:.3f}")
                errors += 1
        elif kind == "bounds":
            _, roles, tol = c
            have = [bounds(o) for o in objs if o.name in roles]
            orig = [want[r] for r in roles if r in want]
            if not have or not orig:
                continue
            lo1 = [min(b[0][k] for b in have) for k in range(3)]
            hi1 = [max(b[1][k] for b in have) for k in range(3)]
            lo0 = [min(b[0][k] for b in orig) for k in range(3)]
            hi0 = [max(b[1][k] for b in orig) for k in range(3)]
            off = max(max(abs(a - b) for a, b in zip(lo0, lo1)), max(abs(a - b) for a, b in zip(hi0, hi1)))
            if off > tol:
                report(f"  CONTRACT {name}: {'+'.join(roles)} bounds {[round(v, 2) for v in lo1]}..{[round(v, 2) for v in hi1]}, "
                       f"the original's {[round(v, 2) for v in lo0]}..{[round(v, 2) for v in hi0]}")
                errors += 1
    return errors


_lift = []


def lift_meshes():
    """The original lift's parts as (name, world vertices, polygons), imported once."""
    if _lift:
        return _lift
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(REPO_MODELS, "Robots", "worker_lift.fbx"))
    bpy.context.view_layer.update()
    for o in set(bpy.data.objects) - before:
        if o.type == "MESH":
            name = o.name.split(".")[0]
            verts = [o.matrix_world @ v.co for v in o.data.vertices]
            polys = [tuple(p.vertices) for p in o.data.polygons]
            if name.startswith("AccentLiftArm"):  # the axles go into the shoulder drums on purpose
                polys = [q for q in polys if all(math.hypot(verts[i].y - LIFT_HINGE.y, verts[i].z - LIFT_HINGE.z) > 0.09 for i in q)]
            _lift.append((name, verts, polys))
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    return _lift


def check_sweep(objs, report):
    """The worker's body must leave the lift's path clear: the tray and the booms swing from rest down to the floor."""
    bpy.context.view_layer.update()
    body = []
    for o in objs:
        mw = o.matrix_world
        body.append((o.name, BVHTree.FromPolygons([mw @ v.co for v in o.data.vertices], [tuple(p.vertices) for p in o.data.polygons])))
    rest = LIFT_HINGE + Vector((0, 0, LIFT_ARM))
    hits = {}
    for k in range(0, 66):
        ang = min(k * 2.0, LIFT_FLOOR_ANGLE)
        r = math.radians(ang)
        pin = LIFT_HINGE + Vector((0, -math.sin(r), math.cos(r))) * LIFT_ARM
        rot = Matrix.Translation(LIFT_HINGE) @ Matrix.Rotation(r, 4, "X") @ Matrix.Translation(-LIFT_HINGE)
        for name, verts, polys in lift_meshes():
            moved = [rot @ v for v in verts] if name.startswith("AccentLiftArm") else [v + (pin - rest) for v in verts]
            tree = BVHTree.FromPolygons(moved, polys)
            for bn, bt in body:
                if tree.overlap(bt):
                    hits.setdefault((name, bn), []).append(ang)
    for (ln, bn), angs in hits.items():
        report(f"  CONTRACT robot_worker: the lift's {ln} hits {bn} at {min(angs):.0f}..{max(angs):.0f} degrees of its swing")
    return len(hits)


def load_style(style):
    if style == "Base":
        return Lang(), {}
    mod = importlib.import_module("styles." + style)
    return mod.Style(), getattr(mod, "DESIGNS", {})


MOVING = ("AccentWheel", "AccentRotor", "AccentLidar", "AccentHead", "AccentLeg", "AccentLid", "AccentDrill")


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


# Voxel styles: furniture whose parts rest on each other (boxes on shelves, a dial on a door) keeps them touching on one
# world grid (voxel.voxelize world=True); others came out better on a grid per part (compared by the model audit).
WORLD_GRID = {"safe", "shelf", "coat_rack", "reception_desk"}


def build(name, L, design):
    s.clear_scene()
    P = design(L)
    if name != "worker_lift":   # the lift has only its two moving parts: no signature pieces
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
    if getattr(L, "voxel", 0.0) > 0.0:
        # a voxel style: every part rebuilt from cubes — but the lift's tray, 3 cm thick, must keep its exact bottom
        voxel.voxelize([o for o in objs if not (name == "worker_lift" and o.name == "BodyLiftTray")], L.voxel,
                       world=name in WORLD_GRID)
    objs += fillers(name, objs)
    taller(name, objs)
    for o in objs:   # the game swaps meshes into the original's objects: a part whose origin is not the world's keeps it
        origin = _origins.get(name, {}).get(o.name)
        if origin is not None:
            o.data.transform(Matrix.Translation(-Vector(origin)))
            o.matrix_world = Matrix.Translation(Vector(origin)) @ o.matrix_world
    return objs


# The office desks are taller than they were drawn (generate_models.DESK_TOP_NEW): every style's design is raised the
# same way — what is under the old top stretches, what stands on it lifts.
TALLER = {"desk": 0.765, "terminal": 0.765, "reception_desk": 0.76}
DESK_TOP_NEW = 0.918


def taller(name, objs):
    if name not in TALLER:
        return
    top = TALLER[name]
    k = DESK_TOP_NEW / top
    bpy.context.view_layer.update()
    for o in objs:
        mw, inv = o.matrix_world, o.matrix_world.inverted()
        for v in o.data.vertices:
            w = mw @ v.co
            w.z = w.z * k if w.z <= top + 1e-4 else w.z + top * (k - 1)
            v.co = inv @ w
    # (the Eye part is no longer scaled up to the original's screen: it grew alone, out of its bezel, and took the style's
    # light lines along the edges with it — a metre longer, floating off the furniture)


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
            if (only and name not in only) or name in OPENING or name in LEFT.get(style, ()):
                continue
            design = own.get(name, design)
            if design is None:   # a danger-zone robot this style has no design for: the game shows the base model
                continue
            objs = build(name, L, design)
            errors += check_contract(name, objs, print)
            path = os.path.join(root, "Styles", "PerStyle", style, f"{name}__{style}.fbx")
            s.export_fbx(path, objs)
    print(f"BESTIARY DONE errors={errors}")
    if errors:
        sys.exit(1)


if __name__ == "__main__":
    main()
