"""Downloads the CC0 assets of the realistic visual styles from Poly Haven (polyhaven.com, all assets CC0).

Run:  python Tools/Art/fetch_cc0.py

1. Raw downloads go to Tools/Art/cache/ (ignored by git): textures (1k jpg: diffuse, OpenGL normal, ARM) and models
   (1k glTF with their textures).
2. Textures are converted for URP Lit into Assets/_Game/Art/External/Textures/<id>/:
     albedo.jpg   the diffuse map
     normal.jpg   OpenGL normal map (what Unity expects)
     mask.png     R = metallic, G = occlusion, A = smoothness (1 - roughness)   (URP Lit _MetallicGlossMap + _OcclusionMap)
3. Models are converted by Tools/Blender/import_cc0.py (run separately) into Assets/_Game/Art/External/Models.
4. CREDITS.md lists every asset with its source page.
"""
import json
import os
import sys
import urllib.request

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CACHE = os.path.join(ROOT, "Tools", "Art", "cache")
OUT_TEX = os.path.join(ROOT, "Assets", "_Game", "Art", "External", "Textures")
CREDITS = os.path.join(ROOT, "Assets", "_Game", "Art", "External", "CREDITS.md")
UA = {"User-Agent": "stealth-prototype-asset-fetch"}

# Surfaces of the styles (floors, walls, ceilings, trims, furniture materials).
TEXTURES = [
    "marble_01", "laminate_floor_02", "plastered_wall_04", "beige_wall_001", "ceiling_interior", "dark_wood",
    "fabric_pattern_07", "brown_leather", "metal_plate", "painted_metal_shutter", "green_metal_rust", "corrugated_iron_02",
    "concrete_floor_worn_001", "old_wooden_floor_01", "peeling_painted_wall", "large_red_bricks", "dark_planks",
    "decrepit_wallpaper", "dark_paneled_wood", "checkered_pavement_tiles", "herringbone_parquet", "concrete_wall_006",
    "hangar_concrete_floor", "concrete_floor_01", "plank_flooring", "raw_plank_wall", "dirty_carpet", "blue_metal_plate",
    "kitchen_wood", "plastered_wall",
]

# Furniture of the realistic styles: Poly Haven model -> the model of ours it replaces.
MODELS = {
    "metal_office_desk": "desk",
    "modern_arm_chair_01": "chair",
    "drawer_cabinet": "cabinet",
    "potted_plant_02": "plant",
    "wooden_bookshelf_worn": "bookshelf",
    "steel_frame_shelves_01": "shelf",
    "cardboard_box_01": "boxes",
    "sofa_02": "sofa",
    "ArmChair_01": "armchair",
    "modern_coffee_table_01": "coffee_table",
    "round_wooden_table_01": "round_table",
    "dining_table": "meeting_table",
    "metal_trash_can": "trash_bin",
    "desk_lamp_arm_01": "desk_lamp",
    "mounted_fluorescent_lights": "ceiling_lamp",
}


def get(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=120) as r, open(path, "wb") as f:
        f.write(r.read())
    return path


def files(asset):
    req = urllib.request.Request(f"https://api.polyhaven.com/files/{asset}", headers=UA)
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))


def convert(diff, nor, arm, out_dir):
    """Diffuse, OpenGL normal and ARM (AO, roughness, metal) or a lone roughness map -> albedo, normal, URP mask."""
    os.makedirs(out_dir, exist_ok=True)
    Image.open(diff).convert("RGB").save(os.path.join(out_dir, "albedo.jpg"), quality=90)
    if nor:
        Image.open(nor).convert("RGB").save(os.path.join(out_dir, "normal.jpg"), quality=92)
    if arm:
        if "_rough" in os.path.basename(arm).lower():
            rough = Image.open(arm).convert("L")
            ao, metal = Image.new("L", rough.size, 255), Image.new("L", rough.size, 0)
        else:
            ao, rough, metal = Image.open(arm).convert("RGB").split()
        smooth = rough.point(lambda v: 255 - v)
        # The game has no reflections (night, black ambient: light must not leak), so full metal renders black —
        # keep a third of it: metal still reads as metal from its highlights.
        metal = metal.point(lambda v: v * 35 // 100)
        Image.merge("RGBA", (metal, ao, Image.new("L", rough.size, 0), smooth)).save(os.path.join(out_dir, "mask.png"))


def fetch_texture(asset):
    f = files(asset)
    cache = os.path.join(CACHE, "textures", asset)
    diff = get(f["Diffuse"]["1k"]["jpg"]["url"], os.path.join(cache, "diff.jpg"))
    nor = get(f["nor_gl"]["1k"]["jpg"]["url"], os.path.join(cache, "nor_gl.jpg"))
    arm = get(f["arm"]["1k"]["jpg"]["url"], os.path.join(cache, "arm.jpg")) if "arm" in f else None
    convert(diff, nor, arm, os.path.join(OUT_TEX, asset))


def fetch_model(asset):
    f = files(asset)
    g = f["gltf"]["1k"]["gltf"]
    cache = os.path.join(CACHE, "models", asset)
    get(g["url"], os.path.join(cache, os.path.basename(g["url"])))
    for rel, info in g["include"].items():
        get(info["url"], os.path.join(cache, rel))


def main():
    done = []
    for t in TEXTURES:
        try:
            fetch_texture(t)
            done.append(("texture", t))
            print("texture", t)
        except Exception as e:  # noqa: BLE001 — report and go on
            print("FAILED texture", t, e)
    for m in MODELS:
        try:
            fetch_model(m)
            done.append(("model", m))
            print("model", m)
        except Exception as e:  # noqa: BLE001
            print("FAILED model", m, e)
    os.makedirs(os.path.dirname(CREDITS), exist_ok=True)
    with open(CREDITS, "w", encoding="utf-8") as c:
        c.write("# External assets\n\nAll from [Poly Haven](https://polyhaven.com), licence [CC0](https://polyhaven.com/license) "
                "(public domain, no attribution required — listed for reference).\n\n| Kind | Asset | Page |\n| --- | --- | --- |\n")
        for kind, a in done:
            c.write(f"| {kind} | {a} | https://polyhaven.com/a/{a} |\n")
    with open(os.path.join(CACHE, "models.json"), "w", encoding="utf-8") as m:
        json.dump(MODELS, m, indent=1)
    print("DONE", len(done))


def convert_models():
    """After Tools/Blender/import_cc0.py: the textures of every model material, next to the FBX."""
    models = os.path.join(ROOT, "Assets", "_Game", "Art", "External", "Models")
    with open(os.path.join(models, "manifest.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    for model, info in manifest.items():
        for mat, maps in info["materials"].items():
            if "diff" in maps:
                convert(maps["diff"], maps.get("nor"), maps.get("arm"), os.path.join(models, model, mat))
        print("textures of", model)


PIXEL = ["dark_wood", "painted_metal_shutter", "dirty_carpet", "green_metal_rust", "metal_plate", "concrete_floor_worn_001",
         "corrugated_iron_02", "blue_metal_plate", "concrete_wall_006", "old_wooden_floor_01", "large_red_bricks", "kitchen_wood"]


def pixelate():
    """PS1-style copies: grey, 64 x 64, 8 tones — the material colour tints them (Art/External/Pixel/<id>.png)."""
    out = os.path.join(ROOT, "Assets", "_Game", "Art", "External", "Pixel")
    os.makedirs(out, exist_ok=True)
    for t in PIXEL:
        src = os.path.join(OUT_TEX, t, "albedo.jpg")
        if not os.path.exists(src):
            continue
        img = Image.open(src).convert("L").resize((64, 64), Image.BOX)
        lo, hi = img.getextrema()
        img = img.point(lambda v: int(round(((v - lo) / max(1, hi - lo)) * 7)) * 255 // 7 * 0.75 + 64)
        img.convert("RGB").save(os.path.join(out, t + ".png"))
        print("pixel", t)


if __name__ == "__main__":
    if "--pixel" in sys.argv:
        sys.exit(pixelate())
    sys.exit(convert_models() if "--models" in sys.argv else main())
