"""Self-made PBR surfaces of the abandoned-facility style (S11): tileable 1024 px maps made from periodic noise.

Run:  blender.exe -b --factory-startup --python Tools/Blender/derelict_textures.py -- Assets/_Game/Art/Textures/Derelict

Writes <out>/<id>/albedo.jpg, normal.jpg (OpenGL, +Y up) and mask.png (R metallic, G occlusion, A smoothness), the same
layout as the CC0 scans (AssetBuilder.SetPbr). Everything tiles: noise is filtered in frequency space and every
pattern repeats on the texture edge. Deterministic (fixed seeds).
"""
import os
import sys

import bpy
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Textures", "Derelict"))
N = 1024
YY, XX = np.mgrid[0:N, 0:N].astype(np.float32) / N  # 0..1, row 0 = top


# --------------------------------------------------------------------------- noise and helpers

def noise(seed, scale, octaves=1, gain=0.5):
    """Tileable fractal noise in 0..1: white noise low-passed in frequency space (periodic by construction)."""
    rng = np.random.default_rng(seed)
    f = np.fft.fftfreq(N)
    fr = np.sqrt(f[None, :] ** 2 + f[:, None] ** 2) * N
    out = np.zeros((N, N), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cut = scale * (2 ** o)
        spec = np.fft.fft2(rng.standard_normal((N, N)))
        spec *= np.exp(-(fr / cut) ** 2)
        layer = np.real(np.fft.ifft2(spec)).astype(np.float32)
        layer /= (np.abs(layer).max() + 1e-6)
        out += layer * amp
        total += amp
        amp *= gain
    out /= total
    return np.clip(out * 0.5 / max(1e-6, np.abs(out).max()) + 0.5, 0, 1)


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def blur(img, r):
    """Periodic gaussian blur (radius in pixels)."""
    f = np.fft.fftfreq(N)
    g = np.exp(-(f[None, :] ** 2 + f[:, None] ** 2) * (2 * np.pi * r) ** 2 / 2)
    return np.real(np.fft.ifft2(np.fft.fft2(img) * g)).astype(np.float32)


def streaks(seed, density=0.5, length=40.0):
    """Vertical grime runs: noise stretched downwards (tileable)."""
    rng = np.random.default_rng(seed)
    f = np.fft.fftfreq(N)
    spec = np.fft.fft2(rng.standard_normal((N, N)))
    spec *= np.exp(-(f[None, :] * N / 90.0) ** 2 - (f[:, None] * N / (N / length)) ** 2)
    s = np.real(np.fft.ifft2(spec)).astype(np.float32)
    s = (s - s.min()) / (s.max() - s.min() + 1e-6)
    return smooth(1 - density, 1.0, s)


def grid_edges(cols, rows, width):
    """Distance-based seams of a cols×rows panel grid: 1 on the seam, 0 inside the panels."""
    gx = np.abs(((XX * cols) % 1.0) - 0.5) * 2  # 1 at panel edge
    gy = np.abs(((YY * rows) % 1.0) - 0.5) * 2
    ex = smooth(1 - width * cols, 1.0, gx)
    ey = smooth(1 - width * rows, 1.0, gy)
    return np.maximum(ex, ey), gx, gy


def rivets(cols, rows, inset, radius, per_side=4):
    """Round rivet heads along the panel borders (height 0..1)."""
    h = np.zeros((N, N), np.float32)
    px, py = (XX * cols) % 1.0, (YY * rows) % 1.0
    for i in range(per_side):
        t = (i + 0.5) / per_side
        for cx, cy in ((t, inset), (t, 1 - inset), (inset, t), (1 - inset, t)):
            d = np.sqrt(((px - cx) * N / cols) ** 2 + ((py - cy) * N / rows) ** 2)
            h = np.maximum(h, np.clip(1 - (d / radius) ** 2, 0, 1) ** 0.5)
    return h


def normal_from(height, strength):
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5 * strength
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5 * strength
    # row 0 is the top of the image: +Y (up) is towards smaller rows
    nx, ny, nz = -dx, dy, np.ones_like(height)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / l * 0.5 + 0.5, ny / l * 0.5 + 0.5, nz / l * 0.5 + 0.5], -1)


def mix(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a * (1 - t) + b * t


def col(r, g, b):
    return np.array([r, g, b], np.float32)


def save(tex_id, albedo, height, metal, ao, smooth_map, strength=6.0):
    folder = os.path.join(OUT, tex_id)
    os.makedirs(folder, exist_ok=True)

    def write(name, rgba, fmt):
        img = bpy.data.images.new(f"{tex_id}_{name}", N, N, alpha=True)
        img.colorspace_settings.name = "Non-Color"
        img.pixels.foreach_set(np.ascontiguousarray(rgba[::-1]).astype(np.float32).ravel())  # Blender rows start at the bottom
        img.filepath_raw = os.path.join(folder, f"{name}.{'jpg' if fmt == 'JPEG' else 'png'}")
        img.file_format = fmt
        if fmt == "JPEG":
            bpy.context.scene.render.image_settings.quality = 92
        img.save()
        bpy.data.images.remove(img)

    ones = np.ones((N, N, 1), np.float32)
    write("albedo", np.concatenate([np.clip(albedo, 0, 1), ones], -1), "JPEG")
    write("normal", np.concatenate([normal_from(height, strength), ones], -1), "JPEG")
    mask = np.stack([np.clip(metal, 0, 1), np.clip(ao, 0, 1), np.zeros((N, N), np.float32), np.clip(smooth_map, 0, 1)], -1)
    write("mask", mask, "PNG")
    print("texture", tex_id)


# --------------------------------------------------------------------------- the surfaces

def rust_layer(seed):
    """Rust colour field and how much rust (0..1): a few large, sparse patches (the big chunks come as decals)."""
    n = noise(seed, 4, 4, 0.6)
    amount = smooth(0.66, 0.8, n)
    tone = noise(seed + 1, 60, 2)
    rust = mix(col(0.32, 0.16, 0.08), col(0.52, 0.28, 0.12), tone)
    return rust, amount


def steel_panels():
    """Walls: riveted steel panels painted dirty grey-green, paint worn at the seams, rust and grime runs."""
    seams, gx, gy = grid_edges(2, 2, 0.006)
    riv = rivets(2, 2, 0.04, 7.0, per_side=5)
    wear = noise(11, 40, 4)
    edge_wear = smooth(0.55, 1.0, np.maximum(gx, gy) + (wear - 0.5) * 0.35)
    paint = mix(col(0.30, 0.34, 0.32), col(0.36, 0.40, 0.37), noise(12, 8, 2))
    paint = mix(paint, col(0.22, 0.25, 0.24), noise(13, 3, 1) * 0.5)          # panel to panel variation
    bare = mix(col(0.38, 0.38, 0.37), col(0.26, 0.26, 0.26), noise(14, 90, 2))
    rust, rust_amt = rust_layer(15)
    rust_amt = np.maximum(rust_amt * 0.6, edge_wear * smooth(0.4, 0.7, wear))
    alb = mix(paint, bare, edge_wear * 0.7)
    alb = mix(alb, rust, rust_amt * 0.8)
    run = streaks(16, 0.45, 30)
    alb = mix(alb, col(0.08, 0.07, 0.06), run * 0.55)                          # grime runs
    alb = mix(alb, col(0.05, 0.05, 0.05), seams * 0.85)
    h = 1 - seams * 0.9 + riv * 0.7 - rust_amt * 0.08 + (noise(17, 120, 2) - 0.5) * 0.05
    metal = edge_wear * 0.8 * (1 - rust_amt)
    ao = 1 - blur(seams, 4) * 0.7 - run * 0.15
    sm = 0.32 + edge_wear * 0.3 - rust_amt * 0.25 - run * 0.15
    save("dr_steel_panels", alb, h, metal, ao, sm, 5.0)


def floor_plate():
    """Floors: diamond tread plate, scuffed bright where walked, dirt in the grooves, rust patches, two plate seams."""
    u, v = XX * 24, YY * 24
    a = (u + v) % 1.0
    b = (u - v) % 1.0
    row = np.floor(v).astype(np.int32) % 2
    d1 = np.abs(a - 0.5) * 2
    d2 = np.abs(b - 0.5) * 2
    tread = np.where(row == 0, smooth(0.75, 0.9, 1 - d1) * smooth(0.2, 0.5, d2), smooth(0.75, 0.9, 1 - d2) * smooth(0.2, 0.5, d1))
    seams, gx, gy = grid_edges(1, 2, 0.004)
    dirt = noise(21, 30, 4)
    wornpath = smooth(0.45, 0.8, noise(22, 3, 2))
    steel = mix(col(0.28, 0.29, 0.29), col(0.42, 0.42, 0.41), noise(23, 70, 2))
    alb = mix(steel, col(0.55, 0.55, 0.53), tread * wornpath * 0.6)
    rust, rust_amt = rust_layer(24)
    alb = mix(alb, rust, rust_amt * 0.4)
    dirt_amt = smooth(0.35, 0.7, dirt) * (1 - tread * 0.7)
    alb = mix(alb, col(0.1, 0.095, 0.085), dirt_amt * 0.4)
    alb = mix(alb, col(0.03, 0.03, 0.03), seams)
    h = tread * 0.6 - seams + (noise(25, 150, 1) - 0.5) * 0.04
    metal = (0.85 - dirt_amt * 0.6 - rust_amt * 0.7)
    ao = 1 - dirt_amt * 0.3 - blur(seams, 3) * 0.6
    sm = 0.35 + tread * wornpath * 0.3 - dirt_amt * 0.25 - rust_amt * 0.2
    save("dr_floor_plate", alb, h, metal, ao, sm, 4.0)


def ceiling_vents():
    """Ceilings: perforated acoustic panels in a frame, water stains, a few missing holes clogged with dirt."""
    seams, gx, gy = grid_edges(2, 2, 0.008)
    u, v = XX * 64, YY * 64
    hole = np.sqrt(((u % 1) - 0.5) ** 2 + ((v % 1) - 0.5) ** 2) < 0.22
    inner = (np.maximum(gx, gy) < 0.88)
    holes = (hole & inner).astype(np.float32)
    stain = smooth(0.6, 0.85, noise(31, 5, 3))
    ring = np.abs(noise(31, 5, 3) - 0.6) < 0.01
    base = mix(col(0.42, 0.43, 0.41), col(0.36, 0.37, 0.35), noise(32, 60, 2))
    alb = mix(base, col(0.3, 0.24, 0.15), stain * 0.65)
    alb = mix(alb, col(0.22, 0.16, 0.08), ring.astype(np.float32) * 0.7)
    alb = mix(alb, col(0.02, 0.02, 0.02), holes)
    alb = mix(alb, col(0.12, 0.12, 0.12), seams)
    h = 1 - holes * 0.5 - seams * 0.8
    save("dr_ceiling_vents", alb, h, np.zeros((N, N)), 1 - holes * 0.5 - blur(seams, 4) * 0.6, 0.18 - stain * 0.08, 4.0)


def painted_metal():
    """Furniture metal: chipped industrial paint (dark grey-green) over steel, edge rust, fingerprints of grime."""
    chips = smooth(0.62, 0.66, noise(41, 24, 5))
    rust, rust_amt = rust_layer(42)
    paint = mix(col(0.24, 0.28, 0.27), col(0.3, 0.33, 0.31), noise(43, 6, 2))
    steel = col(0.45, 0.45, 0.44)
    alb = mix(paint, steel, chips)
    alb = mix(alb, rust, rust_amt * 0.5)
    grime = smooth(0.5, 0.8, noise(44, 10, 3))
    alb = mix(alb, col(0.06, 0.06, 0.05), grime * 0.45)
    h = chips * -0.3 + rust_amt * 0.1 + (noise(45, 200, 1) - 0.5) * 0.03
    save("dr_painted_metal", alb, h, chips * 0.9 * (1 - rust_amt), 1 - grime * 0.25, 0.3 + chips * 0.3 - rust_amt * 0.2 - grime * 0.1, 3.0)


def rust():
    """Pipes and trims: heavy rust with flaking scales and a few remains of paint."""
    n = noise(51, 30, 5)
    flakes = smooth(0.58, 0.62, noise(52, 50, 4))
    alb = mix(col(0.26, 0.12, 0.06), col(0.5, 0.27, 0.11), n)
    alb = mix(alb, col(0.18, 0.2, 0.19), smooth(0.7, 0.75, noise(53, 8, 3)) * 0.8)
    alb = mix(alb, col(0.12, 0.06, 0.03), flakes * 0.5)
    h = n * 0.5 + flakes * 0.4
    save("dr_rust", alb, h, np.zeros((N, N)), 1 - flakes * 0.3, 0.12 + n * 0.08, 7.0)


def composite():
    """Consoles and casings: dirty off-white composite plastic, yellowed, scratched, grime in the corners."""
    scratches = np.zeros((N, N), np.float32)
    rng = np.random.default_rng(61)
    for _ in range(220):
        x0, y0 = rng.random(2)
        ang = rng.random() * np.pi
        ln = 0.02 + rng.random() * 0.1
        dx, dy = np.cos(ang), np.sin(ang)
        px = (XX - x0 + 0.5) % 1.0 - 0.5
        py = (YY - y0 + 0.5) % 1.0 - 0.5
        t = np.clip(px * dx + py * dy, 0, ln)
        d = np.sqrt((px - t * dx) ** 2 + (py - t * dy) ** 2) * N
        scratches = np.maximum(scratches, np.clip(1 - d / 1.2, 0, 1) * 0.8)
    base = mix(col(0.62, 0.62, 0.57), col(0.55, 0.52, 0.42), noise(62, 4, 2))    # yellowed
    grime = smooth(0.55, 0.85, noise(63, 20, 4))
    alb = mix(base, col(0.2, 0.19, 0.15), grime * 0.3)
    alb = mix(alb, col(0.75, 0.75, 0.72), scratches * 0.4)
    h = -scratches * 0.3 + (noise(64, 250, 1) - 0.5) * 0.02
    save("dr_composite", alb, h, np.zeros((N, N)), 1 - grime * 0.3, 0.42 - grime * 0.25 - scratches * 0.1, 3.0)


def vinyl():
    """Seats: cracked dark vinyl, worn to the fabric backing in places."""
    cells = noise(71, 60, 3)
    cracks = smooth(0.0, 0.02, np.abs(cells - 0.5))
    cracks = 1 - cracks
    worn = smooth(0.7, 0.76, noise(72, 14, 3))
    alb = mix(col(0.1, 0.11, 0.11), col(0.14, 0.15, 0.14), noise(73, 30, 2))
    alb = mix(alb, col(0.3, 0.27, 0.22), worn)
    alb = mix(alb, col(0.03, 0.03, 0.03), cracks * 0.6)
    h = -cracks * 0.4 - worn * 0.3
    save("dr_vinyl", alb, h, np.zeros((N, N)), 1 - cracks * 0.3, 0.45 - worn * 0.35 - cracks * 0.2, 3.0)


if __name__ == "__main__":
    for make in (steel_panels, floor_plate, ceiling_vents, painted_metal, rust, composite, vinyl):
        make()
    print("DONE")
