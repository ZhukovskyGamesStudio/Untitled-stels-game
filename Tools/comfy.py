#!/usr/bin/env python3
"""Generate images with the local ComfyUI (Z-Image Turbo) over its HTTP API.

    python Tools/comfy.py "a brass key icon, flat, thick outline" --out Logs/comfy/key.png
    python Tools/comfy.py "..." --count 4 --seed 10          # four seeds: key_10.png .. key_13.png
    python Tools/comfy.py "..." --cutout --size 256x256      # transparent PNG (needs BiRefNet), scaled down
    python Tools/comfy.py --start "..."                      # launch ComfyUI first if it is not running
    python Tools/comfy.py --check                            # is the server up, are the models there

Needs ComfyUI running (run_nvidia_gpu.bat, or --start). Stdlib only.
Env: COMFY_URL (default http://127.0.0.1:8188), COMFY_ROOT (default D:/AI/ComfyUI_windows_portable).
"""
import argparse
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

URL = os.environ.get("COMFY_URL", "http://127.0.0.1:8188").rstrip("/")
ROOT = os.environ.get("COMFY_ROOT", "D:/AI/ComfyUI_windows_portable")

UNET = "z_image_turbo_int8_convrot.safetensors"
CLIP = "qwen_3_4b_fp8_mixed.safetensors"
VAE = "ae.safetensors"
BIREFNET = "birefnet.safetensors"
BIREFNET_URL = "https://huggingface.co/Comfy-Org/BiRefNet/resolve/main/background_removal/birefnet.safetensors"

# Appended with --cutout: the background remover works best on one object on a flat plain field.
ISOLATED = ("single object isolated on a plain flat pure white background, centered, whole object in frame, "
            "nothing under it, no ground, no shadow, no scenery, no text")


def request(path, data=None, timeout=30):
    body = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(URL + path, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        raw = r.read()
    return raw if path.startswith("/view") else json.loads(raw)


def alive():
    try:
        request("/system_stats", timeout=3)
        return True
    except (urllib.error.URLError, OSError):
        return False


def start_server(wait=180):
    if alive():
        return
    python = os.path.join(ROOT, "python_embeded", "python.exe")
    main = os.path.join(ROOT, "ComfyUI", "main.py")
    if not os.path.exists(python):
        sys.exit(f"ComfyUI not found in {ROOT} (set COMFY_ROOT)")
    log = open(os.path.join(ROOT, "comfy_api_server.log"), "w", encoding="utf-8")
    flags = 0x08000000 | 0x00000200 if os.name == "nt" else 0  # CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP
    subprocess.Popen([python, "-s", main, "--windows-standalone-build", "--disable-auto-launch"],
                     cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, creationflags=flags)
    print(f"starting ComfyUI (log: {log.name}) ...", flush=True)
    deadline = time.time() + wait
    while time.time() < deadline:
        if alive():
            print("ComfyUI is up", flush=True)
            return
        time.sleep(2)
    sys.exit(f"ComfyUI did not come up in {wait} s, see {log.name}")


def models(kind):
    try:
        info = request(f"/object_info/{kind}")
    except urllib.error.HTTPError:
        return []
    first = next(iter(info[kind]["input"]["required"].values()))
    return first[0] if isinstance(first[0], list) else first[1].get("options", [])


def graph(prompt, seed, width, height, steps, shift, size, cutout):
    g = {
        "unet": {"class_type": "UNETLoader", "inputs": {"unet_name": UNET, "weight_dtype": "default"}},
        "clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": CLIP, "type": "lumina2", "device": "default"}},
        "vae": {"class_type": "VAELoader", "inputs": {"vae_name": VAE}},
        "model": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["unet", 0], "shift": shift}},
        "pos": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["clip", 0], "text": prompt}},
        "neg": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["pos", 0]}},
        "latent": {"class_type": "EmptySD3LatentImage", "inputs": {"width": width, "height": height, "batch_size": 1}},
        "sample": {"class_type": "KSampler", "inputs": {
            "model": ["model", 0], "positive": ["pos", 0], "negative": ["neg", 0], "latent_image": ["latent", 0],
            "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "res_multistep", "scheduler": "simple",
            "denoise": 1.0}},
        "decode": {"class_type": "VAEDecode", "inputs": {"samples": ["sample", 0], "vae": ["vae", 0]}},
    }
    image = ["decode", 0]
    if size:
        # Scale before cutting out: scaling an RGBA image would bleed the white field into the edges.
        g["scale"] = {"class_type": "ImageScale", "inputs": {
            "image": image, "upscale_method": "area", "width": size[0], "height": size[1], "crop": "center"}}
        image = ["scale", 0]
    if cutout:
        g["bgmodel"] = {"class_type": "LoadBackgroundRemovalModel", "inputs": {"bg_removal_name": BIREFNET}}
        g["fg"] = {"class_type": "RemoveBackground", "inputs": {"bg_removal_model": ["bgmodel", 0], "image": image}}
        # JoinImageWithAlpha takes a ComfyUI mask (1 = transparent), the remover gives 1 = foreground.
        g["inv"] = {"class_type": "InvertMask", "inputs": {"mask": ["fg", 0]}}
        g["rgba"] = {"class_type": "JoinImageWithAlpha", "inputs": {"image": image, "alpha": ["inv", 0]}}
        image = ["rgba", 0]
    g["out"] = {"class_type": "PreviewImage", "inputs": {"images": image}}
    return g


def run(g, timeout=600):
    try:
        pid = request("/prompt", {"prompt": g, "client_id": uuid.uuid4().hex})["prompt_id"]
    except urllib.error.HTTPError as e:
        sys.exit(f"ComfyUI rejected the graph: {e.read().decode(errors='replace')}")
    deadline = time.time() + timeout
    while time.time() < deadline:
        hist = request(f"/history/{pid}")
        if pid in hist:
            entry = hist[pid]
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                msgs = [m for m in status.get("messages", []) if m[0] == "execution_error"]
                sys.exit("ComfyUI error: " + json.dumps(msgs[-1][1] if msgs else status, ensure_ascii=False)[:2000])
            img = entry["outputs"]["out"]["images"][0]
            q = urllib.parse.urlencode({"filename": img["filename"], "subfolder": img["subfolder"], "type": img["type"]})
            return request(f"/view?{q}", timeout=60)
        time.sleep(0.4)
    sys.exit(f"no result in {timeout} s")


def slug(text):
    s = re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")
    return s[:40].rstrip("_") or "image"


def targets(out, prompt, seeds):
    if out and out.lower().endswith(".png"):
        base, folder = os.path.splitext(os.path.basename(out))[0], os.path.dirname(out)
        if len(seeds) == 1:
            return [out]
    else:
        base, folder = slug(prompt), out or os.path.join("Logs", "comfy")
    return [os.path.join(folder, f"{base}_{s}.png") for s in seeds]


def check():
    if not alive():
        print(f"ComfyUI is not running at {URL} (run_nvidia_gpu.bat or --start)")
        return 1
    ok = True
    for kind, name in (("UNETLoader", UNET), ("CLIPLoader", CLIP), ("VAELoader", VAE),
                       ("LoadBackgroundRemovalModel", BIREFNET)):
        have = name in models(kind)
        ok &= have or kind == "LoadBackgroundRemovalModel"
        print(f"{'ok     ' if have else 'MISSING'} {kind}: {name}")
    if BIREFNET not in models("LoadBackgroundRemovalModel"):
        print(f"  --cutout needs {BIREFNET} in {ROOT}/ComfyUI/models/background_removal ({BIREFNET_URL})")
    return 0 if ok else 1


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("prompt", nargs="?", help="what to draw (English works best)")
    p.add_argument("--out", help="file.png, or a folder (default Logs/comfy); several seeds add _<seed>")
    p.add_argument("--seed", type=int, default=None, help="first seed (default: random)")
    p.add_argument("--count", type=int, default=1, help="how many seeds, one image each")
    p.add_argument("--width", type=int, default=1024)
    p.add_argument("--height", type=int, default=1024)
    p.add_argument("--steps", type=int, default=8, help="Z-Image Turbo is made for 8")
    p.add_argument("--shift", type=float, default=3.0)
    p.add_argument("--size", help="scale the result to WxH (e.g. 256x256), center crop")
    p.add_argument("--cutout", action="store_true", help="transparent background (BiRefNet); adds an 'isolated object' tail to the prompt")
    p.add_argument("--raw", action="store_true", help="with --cutout: do not add the 'isolated object' tail")
    p.add_argument("--start", action="store_true", help="launch ComfyUI if it is not running")
    p.add_argument("--check", action="store_true", help="check the server and the models, then exit")
    a = p.parse_args()

    if a.start:
        start_server()
    if a.check:
        sys.exit(check())
    if not a.prompt:
        p.error("prompt is required")
    if not alive():
        sys.exit(f"ComfyUI is not running at {URL}: start run_nvidia_gpu.bat or pass --start")
    if a.cutout and BIREFNET not in models("LoadBackgroundRemovalModel"):
        sys.exit(f"--cutout needs {BIREFNET} in {ROOT}/ComfyUI/models/background_removal\n  {BIREFNET_URL}")

    size = None
    if a.size:
        w, _, h = a.size.lower().partition("x")
        size = (int(w), int(h or w))
    prompt = a.prompt if not a.cutout or a.raw else f"{a.prompt}, {ISOLATED}"
    first = a.seed if a.seed is not None else int.from_bytes(os.urandom(3), "little")
    seeds = [first + i for i in range(a.count)]

    for seed, path in zip(seeds, targets(a.out, a.prompt, seeds)):
        t = time.time()
        png = run(graph(prompt, seed, a.width, a.height, a.steps, a.shift, size, a.cutout))
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        with open(path, "wb") as f:
            f.write(png)
        print(f"{path}  seed={seed}  {time.time() - t:.1f}s", flush=True)


if __name__ == "__main__":
    main()
