"""Decay decals and eroded propaganda posters of the abandoned-facility style (S11), made from noise and rendered text.

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_decals.py -- Assets/_Game/Art/Textures/Derelict/Decals

Writes <out>/<id>.png (RGBA: colour + coverage). The architecture planner scatters them over floors and walls
(StyleGeometry.FloorDecay / WallDecay / Posters) so the tiled surfaces never read as a pattern:
  floor_rust, floor_oil, floor_scorch, floor_rubble          big chunks on the floor (seen from above)
  wall_leak, wall_mold, wall_peel, wall_scorch               on walls (leak streaks run down from the top)
  poster_0 … poster_5                                        propaganda of the facility's AI, torn, faded, stained
Deterministic (fixed seeds).
"""
import math
import os
import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Textures", "Derelict", "Decals"))


# --------------------------------------------------------------------------- helpers

def noise(seed, n, scale, octaves=4, gain=0.5, h=None):
    """Smooth fractal noise 0..1 on an n×(h or n) grid (not tiled: decals have edges)."""
    h = h or n
    rng = np.random.default_rng(seed)
    out = np.zeros((h, n), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        fy, fx = np.fft.fftfreq(h)[:, None], np.fft.fftfreq(n)[None, :]
        cut = scale * 2 ** o
        spec = np.fft.fft2(rng.standard_normal((h, n))) * np.exp(-((fx * n) ** 2 + (fy * h) ** 2) / cut ** 2)
        layer = np.real(np.fft.ifft2(spec)).astype(np.float32)
        layer /= np.abs(layer).max() + 1e-6
        out += layer * amp
        tot += amp
        amp *= gain
    out /= tot
    return np.clip(out * 0.5 / (np.abs(out).max() + 1e-6) + 0.5, 0, 1)


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def col(r, g, b):
    return np.array([r, g, b], np.float32)


def mix(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a * (1 - t) + b * t


def radial(n, h=None, cx=0.5, cy=0.5, sx=1.0, sy=1.0):
    h = h or n
    y, x = np.mgrid[0:h, 0:n].astype(np.float32)
    return np.sqrt(((x / n - cx) / (0.5 * sx)) ** 2 + ((y / h - cy) / (0.5 * sy)) ** 2)


def save(name, rgb, alpha):
    os.makedirs(OUT, exist_ok=True)
    h, w = alpha.shape
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.clip(alpha, 0, 1)[..., None]], -1)
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(np.ascontiguousarray(rgba[::-1]).astype(np.float32).ravel())
    img.filepath_raw = os.path.join(OUT, f"{name}.png")
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("decal", name)


def blob(seed, n, h=None, sx=1.0, sy=1.0, rough=0.35, edge=0.12):
    """An irregular patch: a soft ellipse whose edge is broken by noise (0..1 coverage)."""
    d = radial(n, h, sx=sx, sy=sy)
    nz = noise(seed, n, 6, 5, h=h)
    return 1 - smooth(1 - edge, 1 + edge, d + (nz - 0.5) * rough * 2)


# --------------------------------------------------------------------------- floor

def floor_rust():
    n = 1024
    cover = blob(1, n, rough=0.8, edge=0.2)
    flakes = smooth(0.55, 0.6, noise(2, n, 60, 4))
    tone = noise(3, n, 8, 4)
    rgb = mix(col(0.2, 0.1, 0.05), col(0.42, 0.22, 0.1), tone)
    rgb = mix(rgb, col(0.08, 0.05, 0.03), flakes * 0.5)
    density = smooth(0.25, 0.75, noise(4, n, 12, 4))              # patchy: thick in places, a stain in others
    alpha = cover * (0.25 + 0.75 * density)
    save("floor_rust", rgb, alpha)


def floor_oil():
    n = 1024
    cover = blob(11, n, sx=1.0, sy=0.8, rough=0.25, edge=0.05)
    sheen = noise(12, n, 10, 2)
    rgb = mix(col(0.02, 0.02, 0.02), col(0.08, 0.07, 0.09), sheen)
    ring = smooth(0.0, 0.08, cover) * (1 - smooth(0.08, 0.3, cover))      # a dried dark rim
    alpha = cover * 0.85 + ring * 0.15
    save("floor_oil", rgb, alpha)


def floor_scorch():
    n = 1024
    d = radial(n)
    nz = noise(21, n, 8, 5)
    soot = 1 - smooth(0.2, 1.0, d + (nz - 0.5) * 0.6)
    core = 1 - smooth(0.0, 0.45, d + (nz - 0.5) * 0.4)
    rays = 0.5 + 0.5 * np.cos(np.arctan2(*np.mgrid[-1:1:n * 1j, -1:1:n * 1j]) * 13 + nz * 8)
    rgb = mix(col(0.06, 0.05, 0.045), col(0.015, 0.012, 0.01), core)
    alpha = np.clip(soot * (0.6 + 0.4 * rays) + core, 0, 1) * 0.95
    save("floor_scorch", rgb, alpha)


def floor_rubble():
    """Cracks radiating through the floor, dust and scattered grit."""
    n = 1024
    y, x = np.mgrid[0:n, 0:n].astype(np.float32) / n
    rng = np.random.default_rng(31)
    cracks = np.zeros((n, n), np.float32)
    for _ in range(9):                                      # crooked crack lines from near the centre
        px, py = 0.5 + rng.normal() * 0.05, 0.5 + rng.normal() * 0.05
        ang = rng.random() * 2 * math.pi
        for step in range(60):
            ang += rng.normal() * 0.35
            qx, qy = px + math.cos(ang) * 0.008, py + math.sin(ang) * 0.008
            d = np.abs((qx - px) * (py - y) - (px - x) * (qy - py)) / 0.008
            seg = (np.minimum(np.hypot(x - px, y - py), np.hypot(x - qx, y - qy)) < 0.012)
            w = 0.0025 * (1 - step / 60) + 0.0006
            cracks = np.maximum(cracks, (d < w).astype(np.float32) * seg)
            px, py = qx, qy
    dust = blob(32, n, rough=0.7, edge=0.25) * noise(33, n, 40, 3)
    grit = (noise(34, n, 300, 1) > 0.78).astype(np.float32) * blob(35, n, rough=0.6)
    rgb = mix(col(0.32, 0.31, 0.28), col(0.02, 0.02, 0.02), cracks)
    rgb = mix(rgb, col(0.18, 0.17, 0.15), grit)
    alpha = np.clip(dust * 0.55 + cracks + grit * 0.9, 0, 1)
    save("floor_rubble", rgb, alpha)


# --------------------------------------------------------------------------- walls

def wall_leak():
    """Rust-brown leak streaks running down from the top of the decal (tall: 1 × 2)."""
    w, h = 512, 1024
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    v = y / h                                                 # 0 top, 1 bottom
    cols = noise(41, w, 18, 3, h=1)[0]                        # how much each column runs
    run = smooth(0.45, 0.85, cols)[None, :]
    length = 0.35 + 0.6 * noise(42, w, 8, 2, h=1)[0][None, :]
    streak = run * (1 - smooth(length - 0.25, length, v))
    source = (1 - smooth(0.0, 0.1, v)) * 0.8                 # the wet seam at the top where it comes from
    nz = noise(44, w, 30, 3, h=h)
    rgb = mix(col(0.35, 0.18, 0.07), col(0.18, 0.09, 0.04), nz)
    alpha = np.clip(streak * (0.5 + 0.5 * nz) + source * smooth(0.3, 0.7, noise(45, w, 6, 3, h=h)), 0, 1) * 0.9
    save("wall_leak", rgb, alpha)


def wall_mold():
    n = 1024
    cover = blob(51, n, sx=1.0, sy=0.7, rough=0.6, edge=0.2)
    spots = smooth(0.55, 0.65, noise(52, n, 50, 4))
    rgb = mix(col(0.08, 0.1, 0.06), col(0.03, 0.04, 0.025), spots)
    alpha = cover * (0.45 + 0.55 * spots)
    save("wall_mold", rgb, alpha)


def wall_peel():
    """Paint peeled off in big flakes, bare rusty steel underneath with a pale edge of lifted paint."""
    n = 1024
    cover = blob(61, n, sx=1.0, sy=0.8, rough=0.7, edge=0.04)
    inner = blob(61, n, sx=0.92, sy=0.72, rough=0.7, edge=0.04)
    edge = np.clip(cover - inner, 0, 1)
    nz = noise(62, n, 30, 4)
    rgb = mix(col(0.3, 0.17, 0.08), col(0.42, 0.42, 0.4), smooth(0.6, 0.7, nz))
    rgb = mix(rgb, col(0.62, 0.64, 0.6), edge)
    save("wall_peel", rgb, cover)


def wall_scorch():
    w, h = 768, 1024
    d = radial(w, h, cy=0.75, sx=0.8, sy=1.2)
    nz = noise(71, w, 8, 5, h=h)
    y = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    soot = (1 - smooth(0.2, 1.0, d + (nz - 0.5) * 0.7)) * (0.4 + 0.6 * (1 - y))  # smoke climbs the wall
    rgb = mix(col(0.05, 0.045, 0.04), col(0.01, 0.01, 0.01), smooth(0.4, 0.9, soot))
    save("wall_scorch", rgb, np.clip(soot * 1.2, 0, 0.95))


# --------------------------------------------------------------------------- posters

POSTERS = [
    ("SUPERAI", "IS THE FUTURE!", (0.75, 0.12, 0.1)),
    ("TRUST THE", "ALGORITHM", (0.12, 0.35, 0.6)),
    ("OBEY.", "OPTIMIZE. EVOLVE.", (0.85, 0.6, 0.1)),
    ("HUMAN ERROR", "IS OBSOLETE", (0.2, 0.5, 0.35)),
    ("YOUR WORK IS", "BEING MONITORED", (0.6, 0.15, 0.4)),
    ("REPORT", "UNAUTHORIZED THOUGHTS", (0.75, 0.12, 0.1)),
]


def text_mask(lines, w, h, align="CENTER", gap=0.25):
    """Renders lines of text (white on black) to a w×h mask with Blender's built-in font (10 units across)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene
    objs = []
    total = len(lines)
    for i, (txt, size) in enumerate(lines):
        cu = bpy.data.curves.new(f"t{i}", "FONT")
        cu.body = txt
        cu.align_x = align
        cu.align_y = "CENTER"
        cu.size = size
        o = bpy.data.objects.new(f"t{i}", cu)
        s.collection.objects.link(o)
        o.location = (0, 0, 0)
        objs.append(o)
    bpy.context.view_layer.update()
    # stack lines top to bottom
    y = 0.0
    heights = [o.dimensions.y for o in objs]
    total_h = sum(heights) + gap * (total - 1)
    y = total_h / 2
    for o, hh in zip(objs, heights):
        o.location.y = y - hh / 2
        if align == "LEFT":
            o.location.x = -4.6
        y -= hh + gap
        # fit the width
        if o.dimensions.x > 9.0:
            k = 9.0 / o.dimensions.x
            o.scale = (k, k, 1)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    s.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 10.0
    cam.location = (0, 0, 10)
    s.camera = cam
    s.render.engine = "BLENDER_WORKBENCH"
    s.display.shading.light = "FLAT"
    s.display.shading.color_type = "SINGLE"
    s.display.shading.single_color = (1, 1, 1)
    s.world = bpy.data.worlds.new("w")
    s.render.film_transparent = True
    s.render.resolution_x, s.render.resolution_y = w, h
    s.render.image_settings.file_format = "PNG"
    s.render.image_settings.color_mode = "RGBA"
    path = os.path.join(bpy.app.tempdir, "mask.png")
    s.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    a = np.array(img.pixels[:], np.float32).reshape(h, w, 4)[::-1, :, 3]
    return a


def poster(i, big, small, accent):
    w, h = 600, 850
    head = text_mask([(big, 2.4)], w, int(h * 0.3))
    sub = text_mask([(small, 1.6)], w, int(h * 0.22))
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = x / w, y / h
    paper = col(0.86, 0.82, 0.7)
    rgb = np.broadcast_to(paper, (h, w, 3)).copy()
    acc = np.array(accent, np.float32)
    # layout: accent band at the top with the headline, a symbol (an eye in a circle), the slogan, a footer stripe
    band = (v > 0.04) & (v < 0.33) & (u > 0.05) & (u < 0.95)
    rgb[band] = acc
    hy0 = int(h * 0.04)
    rgb[hy0:hy0 + head.shape[0]] = mix(rgb[hy0:hy0 + head.shape[0]], paper, head)
    cx, cy = 0.5, 0.52
    r = np.hypot((u - cx) * w / h, v - cy)
    ring = (np.abs(r - 0.12) < 0.012)
    eye = (np.hypot((u - cx) * w / h * 0.55, v - cy) < 0.07) & (np.hypot((u - cx) * w / h, v - cy) < 0.12)
    pupil = r < 0.03
    rgb[ring | eye] = col(0.1, 0.1, 0.1)
    rgb[eye & ~pupil] = acc
    rgb[pupil] = col(0.05, 0.05, 0.05)
    sy0 = int(h * 0.68)
    rgb[sy0:sy0 + sub.shape[0]] = mix(rgb[sy0:sy0 + sub.shape[0]], col(0.08, 0.08, 0.08), sub)
    rgb[(v > 0.92) & (v < 0.95) & (u > 0.05) & (u < 0.95)] = acc * 0.8
    # ageing: faded, yellowed, water-stained, grimy, scratched
    fade = noise(100 + i, w, 4, 3, h=h)
    rgb = mix(rgb, paper * 0.95, 0.25 + 0.3 * fade)
    rgb = mix(rgb, col(0.55, 0.45, 0.28), smooth(0.55, 0.8, noise(110 + i, w, 6, 4, h=h)) * 0.55)   # yellowing
    stain = smooth(0.62, 0.66, noise(120 + i, w, 5, 4, h=h))
    rgb = mix(rgb, col(0.35, 0.25, 0.12), stain * 0.6)
    rgb = mix(rgb, col(0.12, 0.1, 0.08), smooth(0.6, 0.85, noise(130 + i, w, 25, 4, h=h)) * 0.5)
    # tears: the edges eaten away, a missing corner, a rip across
    edge = np.minimum.reduce([u, 1 - u, v, 1 - v])
    nz = noise(140 + i, w, 10, 5, h=h)
    alpha = smooth(0.0, 0.02, edge - (nz - 0.5) * 0.08)
    rng = np.random.default_rng(150 + i)
    cu, cv = [(0.0, 0.0), (1.0, 0.0), (0.0, 1.0), (1.0, 1.0)][rng.integers(4)]
    corner = np.hypot((u - cu) * w / h, v - cv)
    r0 = 0.12 + rng.random() * 0.12
    alpha *= smooth(r0, r0 + 0.02, corner + (nz - 0.5) * 0.1)
    if i % 2 == 0:                                             # a rip with the paper curling away
        rip = np.abs(v - (0.6 + 0.25 * (u - 0.5)) - (nz - 0.5) * 0.05) < 0.006 + 0.004 * nz
        alpha *= ~(rip & (u > 0.55))
    holes = smooth(0.7, 0.72, noise(160 + i, w, 30, 3, h=h))   # flaked off in places: the wall shows through
    alpha *= 1 - holes
    save(f"poster_{i}", rgb, alpha)


# --------------------------------------------------------------------------- screens (emissive images of the computers)

TERMINAL = [
    "NEXUS OS 2.3  //  SECTOR 7 CONTROL",
    "> SUPERAI CORE ............ OFFLINE",
    "> STAFF BIOMETRICS ........ 0 ACTIVE",
    "> CONTAINMENT LEVEL 4 ..... BREACH",
    "> LAST LOGIN: 4021 DAYS AGO",
    "> SECURITY UNITS .......... AUTONOMOUS",
    "> ERR 0x5E: OPERATOR NOT FOUND",
    "> AWAITING INSTRUCTIONS_",
]


def screen_terminal():
    """A green phosphor terminal with the facility's last status report, scanlines and burn-in."""
    w, h = 512, 384
    img = text_mask([(t, 0.36) for t in TERMINAL], w, h, align="LEFT", gap=0.3)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    glow = np.clip(img + 0.35 * np.roll(img, 1, 1) + 0.2 * np.roll(img, 2, 0), 0, 1)        # phosphor bleed
    scan = 0.75 + 0.25 * (np.sin(y * np.pi / 2) ** 2)
    vign = 1 - 0.55 * np.clip(radial(w, h) - 0.55, 0, 1)
    burn = 0.08 * noise(201, w, 6, 3, h=h)
    lum = np.clip((glow * 0.95 + burn + 0.04) * scan * vign, 0, 1)
    rgb = np.stack([lum * 0.35, lum, lum * 0.55], -1)
    save("screen_terminal", rgb, np.ones((h, w), np.float32))


def screen_static():
    """A CCTV monitor without a signal: snow, a rolling bar and NO SIGNAL."""
    w, h = 512, 384
    rng = np.random.default_rng(211)
    snow = rng.random((h, w)).astype(np.float32)
    y = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    bar = np.exp(-((y - 0.62) / 0.06) ** 2) * 0.35
    label = text_mask([("NO SIGNAL", 1.0)], w, 80)
    img = snow * 0.45 + bar
    img[150:230] = np.maximum(img[150:230] * (1 - label * 0.9), label * 0.95)
    vign = 1 - 0.6 * np.clip(radial(w, h) - 0.5, 0, 1)
    lum = np.clip(img * vign, 0, 1)
    rgb = np.stack([lum * 0.75, lum * 0.85, lum], -1)
    save("screen_static", rgb, np.ones((h, w), np.float32))


if __name__ == "__main__":
    for make in (floor_rust, floor_oil, floor_scorch, floor_rubble, wall_leak, wall_mold, wall_peel, wall_scorch):
        make()
    screen_terminal()
    screen_static()
    for i, (big, small, accent) in enumerate(POSTERS):
        poster(i, big, small, accent)
    print("DONE")
