"""Night sky of the abandoned-facility style (S16): the world outside the house, seen above the gas layer.

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_sky.py -- Assets/_Game/Art/Textures/Sky

Writes <out>/derelict_night.png, a 4096 x 2048 latitude-longitude panorama (top row = zenith). Unity imports it as a
cubemap (AssetBuilder: TextureImporterShape.TextureCube, Cylindrical) and the gas pass (Art/Shaders/GasFog.shader)
draws it where nothing was rendered: overcast toxic clouds lit from below by a dead city, a dim moon behind them, a
skyline of ruined towers and stacks on the horizon with a few lamps still burning. Made from noise, deterministic.
"""
import math
import os
import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Textures", "Sky"))
W, H = 4096, 2048


def tiled_noise(seed, n, scale, octaves=5, gain=0.55):
    """Smooth fractal noise 0..1 on an n×n grid that tiles (sampled with wrap-around)."""
    rng = np.random.default_rng(seed)
    out = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    f = np.fft.fftfreq(n) * n
    r2 = f[:, None] ** 2 + f[None, :] ** 2
    for o in range(octaves):
        cut = scale * 2 ** o
        spec = np.fft.fft2(rng.standard_normal((n, n))) * np.exp(-r2 / cut ** 2)
        layer = np.real(np.fft.ifft2(spec)).astype(np.float32)
        layer /= np.abs(layer).max() + 1e-6
        out += layer * amp
        tot += amp
        amp *= gain
    out /= tot
    return (out - out.min()) / (out.max() - out.min() + 1e-6)


def sample(tex, u, v):
    """Bilinear sample of a tiled square texture at (u, v) in texels (any range)."""
    n = tex.shape[0]
    x0 = np.floor(u).astype(np.int64)
    y0 = np.floor(v).astype(np.int64)
    fx, fy = u - x0, v - y0
    x0 %= n
    y0 %= n
    x1, y1 = (x0 + 1) % n, (y0 + 1) % n
    a = tex[y0, x0] * (1 - fx) + tex[y0, x1] * fx
    b = tex[y1, x0] * (1 - fx) + tex[y1, x1] * fx
    return a * (1 - fy) + b * fy


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def main():
    # directions of the pixels: rows bottom (−90°) to top (+90°), columns a full turn of azimuth
    el = (np.arange(H, dtype=np.float32) + 0.5) / H * math.pi - math.pi / 2
    az = (np.arange(W, dtype=np.float32) + 0.5) / W * 2 * math.pi
    EL, AZ = np.meshgrid(el, az, indexing="ij")
    up = np.sin(EL)
    cosel = np.cos(EL)
    dx, dz = cosel * np.sin(AZ), cosel * np.cos(AZ)
    deg = np.degrees(EL)

    # --- the sky: near-black zenith, a sick green-grey glow of the dead city low on the horizon
    zen = np.array([0.006, 0.008, 0.013], np.float32)
    hor = np.array([0.075, 0.085, 0.07], np.float32)
    t = np.clip(up, 0, 1) ** 0.45
    col = hor[None, None, :] * (1 - t[..., None]) + zen[None, None, :] * t[..., None]
    # the city glow is stronger in two directions (the old districts)
    glow = (0.6 + 0.4 * np.cos(AZ - 0.9) ** 2 + 0.3 * np.exp(-((AZ - 4.2) ** 2) / 0.3)) * np.exp(-np.clip(deg, 0, 90) / 9.0)
    col += np.array([0.06, 0.045, 0.025], np.float32) * glow[..., None]

    # --- the moon: high, pale, almost lost behind the clouds
    moon_az, moon_el = math.radians(215.0), math.radians(38.0)
    m = np.array([math.cos(moon_el) * math.sin(moon_az), math.sin(moon_el), math.cos(moon_el) * math.cos(moon_az)], np.float32)
    cosang = dx * m[0] + up * m[1] + dz * m[2]
    ang = np.degrees(np.arccos(np.clip(cosang, -1, 1)))
    disc = smooth(1.1, 0.9, ang)
    halo = np.exp(-ang / 7.0) * 0.12 + np.exp(-ang / 30.0) * 0.04

    # --- the clouds: a layer over the city, its texture laid out on a plane above (perspective to the horizon)
    n = 1024
    base = tiled_noise(11, n, 5.0, octaves=6)
    detail = tiled_noise(12, n, 22.0, octaves=4)
    above = np.clip(up, 0.02, 1)
    cu, cv = dx / above * 260.0, dz / above * 260.0
    dens = sample(base, cu, cv) * 0.75 + sample(detail, cu * 1.7, cv * 1.7) * 0.25
    cover = smooth(0.38, 0.72, dens) * smooth(-0.5, 4.0, deg)  # thinning to the horizon haze
    # under-lit by the city (warm-green low), silver where the moon is behind them
    lit_city = np.array([0.05, 0.055, 0.04], np.float32) * (0.35 + glow[..., None] * 0.9)
    lit_moon = np.array([0.11, 0.12, 0.13], np.float32) * (np.exp(-ang / 18.0)[..., None]) * (1 - smooth(0.6, 0.9, dens))[..., None]
    cloud = lit_city + lit_moon + np.array([0.012, 0.014, 0.016], np.float32)
    sky = col + (np.array([0.75, 0.78, 0.8], np.float32) * disc[..., None] + np.array([0.6, 0.65, 0.7], np.float32) * halo[..., None]) * (1 - cover[..., None] * 0.92)
    col = sky * (1 - cover[..., None]) + cloud * cover[..., None]

    # --- a few cold stars in the gaps
    rng = np.random.default_rng(7)
    stars = rng.random((H, W)) > 0.9993
    gaps = (cover < 0.15) & (deg > 18)
    col[stars & gaps] += rng.uniform(0.05, 0.22, (int((stars & gaps).sum()), 1)).astype(np.float32)

    # --- the skyline: three bands of ruined towers, the far ones washed out by the haze
    for band, (count, hmin, hmax, shade, haze, lamps) in enumerate([
            (180, 0.6, 4.5, 0.035, 0.75, 0.0025),
            (120, 1.0, 8.0, 0.018, 0.45, 0.004),
            (60, 1.5, 13.0, 0.008, 0.2, 0.006)]):
        r = np.random.default_rng(100 + band)
        prof = np.full(W, -1.0, np.float32)
        owner = np.full(W, -1, np.int64)
        for i in range(count):
            c = r.uniform(0, W)
            wdt = r.uniform(6, 60) * (1 + band * 0.6)
            h = r.uniform(hmin, hmax) * (r.random() ** 1.6 * 0.8 + 0.2)
            cols = (np.arange(int(c - wdt / 2), int(c + wdt / 2)) % W)
            kind = r.random()
            if kind < 0.15:    # a stack: thin and tall
                cols = (np.arange(int(c - 2 - band), int(c + 2 + band)) % W)
                h *= 1.6
            hh = np.full(len(cols), h, np.float32)
            if 0.15 <= kind < 0.35:  # broken top: jagged
                hh -= np.abs(np.sin(np.arange(len(cols)) * r.uniform(0.2, 0.7))) * h * 0.35
            if 0.35 <= kind < 0.45:  # stepped
                hh[: len(cols) // 3] *= 0.7
            upd = hh > prof[cols]
            prof[cols[upd]] = hh[upd]
            owner[cols[upd]] = i
        # antennas and the last red warning lights
        mask = (deg[:, :] >= -0.5) & (deg <= prof[None, :])
        dark = np.array([shade, shade * 1.05, shade * 1.1], np.float32)
        hz = (haze * (col * 0.0 + hor[None, None, :] * 0.8))
        bandcol = dark[None, None, :] * (1 - haze) + hz
        col = np.where(mask[..., None], bandcol, col)
        # windows: a grid on each tower, very few lit
        wy = (deg * (60 + band * 30)).astype(np.int64)
        wx = (np.arange(W)[None, :] // (3 + band)).astype(np.int64)
        hsh = (wy * 73856093 ^ wx * 19349663 ^ (owner[None, :] + 7) * 83492791) & 0xFFFFFF
        lit = mask & (deg < prof[None, :] - 0.15) & ((hsh / 0xFFFFFF) < lamps * 6) & ((wy + wx) % 2 == 0)
        warm = (hsh % 3) != 0
        wc = np.where(warm[..., None], np.array([0.55, 0.38, 0.16], np.float32), np.array([0.2, 0.45, 0.45], np.float32))
        col = np.where(lit[..., None], wc * (0.25 + 0.5 * (1 - haze)), col)
        for i in r.choice(W, 10 + band * 6, replace=False):
            if prof[i] < 3.0:
                continue
            top = int((prof[i] + 90) / 180 * H)
            col[top: top + 2, i: i + 2] = np.array([0.9, 0.08, 0.05], np.float32) * (1 - haze * 0.7)

    # --- below the horizon: the gas lying over the ground (dark, the gas pass covers it anyway)
    below = deg < -0.5
    col[below] = (hor * 0.35)[None, :] * np.exp(np.clip(deg[below], -90, 0) / 6.0)[:, None] + np.array([0.004, 0.005, 0.004], np.float32)

    # dither against banding, then to 8-bit sRGB
    col += (np.random.default_rng(3).random(col.shape).astype(np.float32) - 0.5) / 255.0 * 0.6
    col = np.clip(col, 0, 1)
    srgb = np.where(col <= 0.0031308, col * 12.92, 1.055 * np.power(col, 1 / 2.4) - 0.055)

    os.makedirs(OUT, exist_ok=True)
    img = bpy.data.images.new("derelict_night", W, H, alpha=False)
    rgba = np.concatenate([srgb, np.ones((H, W, 1), np.float32)], axis=2)  # Blender rows are bottom-up, as computed
    img.colorspace_settings.name = "Non-Color"  # the values are already sRGB-encoded (set before the pixels: it resets them)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    path = os.path.join(OUT, "derelict_night.png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("wrote", path)


main()
