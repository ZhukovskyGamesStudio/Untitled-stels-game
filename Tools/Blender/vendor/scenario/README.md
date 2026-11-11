# scenario-labs Blender toolkit (vendored)

Unchanged copies of `bx_audit.py` and `bx_review.py` from
[scenario-labs/skills](https://github.com/scenario-labs/skills), `skills/dcc/blender/scenario-blender-expert/scripts`,
commit `b96506d` (2026-11-08), MIT licence (see `LICENSE`). Tested by its authors headless on Blender 5.2.1.

Used by `Tools/Blender/review.py`:
- `bx_audit.deliverable(objs, profile="game")` — the gate before export: floating and hidden parts, z-fighting between
  parts, inside-out parts, faces pointing inward (Unity culls them: holes), unapplied scale, duplicate faces, no UVs.

Not edited here: to update, copy the files again from upstream and note the commit above.
