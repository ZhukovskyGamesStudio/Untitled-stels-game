"""Downloads the scanned CC0 textures the surfaces are made of (Tools/Art/generate_surfaces.py) into Tools/Art/cache/surfaces
(ignored by git): <id>/color.jpg, normal.jpg (OpenGL), rough.jpg and what else the set has (metal.jpg, ao.jpg).

Run:  python -I Tools/Art/fetch_surfaces.py

Sources: ambientCG (1K-JPG zips) and Poly Haven (1k jpg: diffuse, OpenGL normal, ARM). Both are CC0: free, commercial use
included, no attribution required (listed in Art/Textures/Surfaces/CREDITS.md all the same).
"""
import json
import os
import urllib.request
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
CACHE = os.path.join(ROOT, "Tools", "Art", "cache", "surfaces")
CREDITS = os.path.join(ROOT, "Assets", "_Game", "Art", "Textures", "Surfaces", "CREDITS.md")
UA = {"User-Agent": "game-texture-fetch/1.0"}

AMBIENTCG = ["Metal027", "Metal009", "Plastic013A", "Plastic018B", "Leather026", "Porcelain001", "Cardboard004", "Paper001", "Fabric034",
             "Fingerprints002", "Smear006", "SurfaceImperfections003", "SurfaceImperfections013", "Scratches003", "Leaking001"]
POLYHAVEN = ["oak_veneer_01", "american_walnut_veneer", "plywood", "wood_table_worn", "wool_boucle", "rough_linen", "cotton_jersey", "denim_fabric"]

PARTS = {"_Color": "color", "_NormalGL": "normal", "_Roughness": "rough", "_Metalness": "metal", "_AmbientOcclusion": "ao", "_Opacity": "opacity"}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180) as r:
        return r.read()


def ambientcg(name):
    folder = os.path.join(CACHE, name)
    if os.path.exists(os.path.join(folder, ".done")):
        return
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".zip")
    with open(path, "wb") as f:
        f.write(get(f"https://ambientcg.com/get?file={name}_1K-JPG.zip"))
    with zipfile.ZipFile(path) as z:
        for n in z.namelist():                           # only the named maps, under our own names: nothing else is unpacked
            base = os.path.splitext(os.path.basename(n))[0]
            for tail, ours in PARTS.items():
                if base.endswith(tail) and n.lower().endswith(".jpg"):
                    with open(os.path.join(folder, ours + ".jpg"), "wb") as f:
                        f.write(z.read(n))
    os.remove(path)
    open(os.path.join(folder, ".done"), "w").close()
    print(name, sorted(os.listdir(folder)))


def polyhaven(name):
    folder = os.path.join(CACHE, name)
    if os.path.exists(os.path.join(folder, ".done")):
        return
    os.makedirs(folder, exist_ok=True)
    files = json.loads(get(f"https://api.polyhaven.com/files/{name}").decode("utf-8"))
    for key, ours in (("Diffuse", "color"), ("nor_gl", "normal"), ("arm", "arm")):
        if key in files:
            with open(os.path.join(folder, ours + ".jpg"), "wb") as f:
                f.write(get(files[key]["1k"]["jpg"]["url"]))
    open(os.path.join(folder, ".done"), "w").close()
    print(name, sorted(os.listdir(folder)))


if __name__ == "__main__":
    for a in AMBIENTCG:
        ambientcg(a)
    for p in POLYHAVEN:
        polyhaven(p)
    os.makedirs(os.path.dirname(CREDITS), exist_ok=True)
    with open(CREDITS, "w", encoding="utf-8", newline="\n") as c:
        c.write("# Scanned sources of the surfaces\n\nAll CC0 (public domain: free, commercial use included, no attribution required — listed for "
                "reference). The other surfaces are made from scratch by Tools/Art/generate_surfaces.py.\n\n| Set | Page |\n| --- | --- |\n")
        for a in AMBIENTCG:
            c.write(f"| {a} | https://ambientcg.com/a/{a} |\n")
        for p in POLYHAVEN:
            c.write(f"| {p} | https://polyhaven.com/a/{p} |\n")
    print("DONE")
