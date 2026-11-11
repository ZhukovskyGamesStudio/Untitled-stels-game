"""Batch check of many models: one table instead of one sheet per model.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/audit_models.py -- <report.md>
          <folder or .fbx> [...] [--json report.json] [--loose]

Imports every FBX (folders are searched recursively), runs review.check — triangles, DETACHED pieces, holes and the
scenario-labs gate (z-fighting, inward faces, inside-out parts, duplicate faces…) — and writes a Markdown table, worst
models first, plus the gate messages of every failing model. --loose: the model is a set of separate props (a kit),
so floating parts are expected and not counted. Furniture, props, items and level pieces are checked with the floor
as a support (review.check floor=True); robots, sensors and characters without it.
Expected warnings of our pipeline (n-gons, no material) are left out of the table.
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import review  # noqa: E402

EXPECTED = ("n-gon", "no material")


def stands_on_floor(path):
    """Furniture, props, items and level pieces stand on the floor; robots, sensors and characters do not lean on it."""
    parts = set(os.path.normpath(path).replace("\\", "/").split("/"))
    return not parts & {"Robots", "Sensors", "Player", "Players", "Guards", "Humans"}


def files(args):
    out = []
    for a in args:
        if os.path.isdir(a):
            for root, _, names in os.walk(a):
                out += [os.path.join(root, n) for n in sorted(names) if n.lower().endswith(".fbx")]
        elif a.lower().endswith(".fbx"):
            out.append(a)
    return out


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    json_path, loose, rest = None, False, []
    i = 0
    while i < len(args):
        if args[i] == "--json":
            json_path = args[i + 1]
            i += 2
        elif args[i] == "--loose":
            loose = True
            i += 1
        else:
            rest.append(args[i])
            i += 1
    out, paths = rest[0], files(rest[1:])
    rows = []
    t0 = time.time()
    for k, path in enumerate(paths):
        review.clear()
        try:
            objs = review.import_model(path)
            r = review.check(objs, os.path.basename(path), verbose=False, floor=stands_on_floor(path))
        except Exception as e:  # noqa: BLE001 — one broken file must not stop the batch
            rows.append({"path": path, "crash": str(e)})
            continue
        errors = [e for e in r["gate"]["errors"] if not (loose and ("floating" in e or "fragment" in e))]
        warnings = [w for w in r["gate"]["warnings"] if not any(x in w for x in EXPECTED)]
        rows.append({
            "path": path, "tris": r["tris"], "size": r["size_m"], "detached": 0 if loose else len(r["detached"]),
            "errors": errors, "warnings": warnings,
            "open_edges": sum(p["open_edges"] for p in r["parts"].values()),
        })
        if k % 20 == 0:
            print(f"AUDIT {k + 1}/{len(paths)} {time.time() - t0:.0f}s", flush=True)
    rows.sort(key=lambda r: (-len(r.get("errors", [1])), -r.get("detached", 0), -len(r.get("warnings", []))))
    base = os.path.commonpath([os.path.dirname(r["path"]) for r in rows]) if rows else ""
    bad = [r for r in rows if r.get("crash") or r["errors"] or r["detached"]]
    lines = [f"# Model audit — {len(rows)} models, {len(bad)} with errors or detached pieces", "",
             f"Folder: `{base}`", "",
             "| model | tris | size m | detached | gate errors | warnings | open edges |",
             "| --- | ---: | --- | ---: | ---: | ---: | ---: |"]
    for r in rows:
        name = os.path.relpath(r["path"], base)
        if r.get("crash"):
            lines.append(f"| {name} | — | — | — | CRASH | — | — |")
            continue
        lines.append(f"| {name} | {r['tris']} | {r['size']} | {r['detached']} | {len(r['errors'])} | "
                     f"{len(r['warnings'])} | {r['open_edges']} |")
    lines += ["", "## Gate messages", ""]
    for r in rows:
        if r.get("crash"):
            lines.append(f"- **{os.path.relpath(r['path'], base)}**: crash — {r['crash']}")
        elif r["errors"] or r["warnings"]:
            lines.append(f"- **{os.path.relpath(r['path'], base)}**")
            lines += [f"  - ERROR {e}" for e in r["errors"]]
            lines += [f"  - warn {w}" for w in r["warnings"][:6]]
    with open(out, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    if json_path:
        with open(json_path, "w", encoding="utf-8") as f:
            json.dump(rows, f, indent=1)
    print(f"AUDIT DONE {len(rows)} models, {len(bad)} bad, {time.time() - t0:.0f}s -> {out}")


if __name__ == "__main__":
    main()
