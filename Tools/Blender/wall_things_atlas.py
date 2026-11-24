"""What hangs on walls (Tools/Blender/wall_things.py, Tools/Art/generate_wall_things.py, Scripts/Level/WallThings.cs — the same
order in all three): its name, its size on the wall in metres (width, height), how it is made and its cell in wall_things.png.

The atlas is 4 x 4 cells; the coordinates here are in machine_atlas.SIZE (1024) units, as machines.uv reads them. A picture
fills the middle of its cell at its own proportions (rect())."""

# name, width, height, make: "wood" / "black" / "metal" frame with a mat, "thin" (a clip frame), "plate" (a sign screwed on),
# "paper" (hangs from a nail), None (no picture: a thing of its own)
THINGS = [
    ("landscape_a", 0.70, 0.50, "wood"), ("landscape_b", 0.90, 0.60, "black"), ("abstract_a", 0.50, 0.70, "black"), ("abstract_b", 0.60, 0.60, "metal"),
    ("photo_a", 0.40, 0.30, "black"), ("photo_b", 0.30, 0.40, "wood"), ("certificate", 0.30, 0.42, "wood"), ("calendar", 0.30, 0.45, "paper"),
    ("evac_plan", 0.42, 0.30, "thin"), ("first_aid", 0.30, 0.30, "plate"), ("tin_sign", 0.45, 0.30, "plate"), ("health", 0.42, 0.60, "thin"),
    ("family", 0.24, 0.30, "wood"), ("botanical", 0.30, 0.42, "metal"), ("no_smoking", 0.20, 0.20, "plate"),
    ("thermostat", 0.085, 0.125, None), ("vent_grille", 0.40, 0.20, None),
]
CELL = 256


def rect(index):
    """The picture's rectangle (x0, y0, x1, y1) in its cell, top-left origin, 1024 units."""
    _, w, h, _ = THINGS[index]
    cx, cy = CELL * (index % 4) + CELL / 2, CELL * (index // 4) + CELL / 2
    k = (CELL - 8) / max(w, h)
    return (cx - w * k / 2, cy - h * k / 2, cx + w * k / 2, cy + h * k / 2)


CELLS = {name: rect(i) for i, (name, _, _, make) in enumerate(THINGS) if make}
