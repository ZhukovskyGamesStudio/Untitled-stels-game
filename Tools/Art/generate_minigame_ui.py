"""Pictures of the minigame screens (the safe's dial, the vault's switch and sequence locks): gunmetal panels with screws,
a brass name plate, a graphite dial face with a chrome knurled bezel, a fluted knob and its gloss, a gold pointer,
recessed code windows, an oscilloscope screen, glass lamp lenses in chrome bezels, key caps and LEDs.

    python Tools/Art/generate_minigame_ui.py

Writes Assets/_Game/Art/Sprites/UI/Minigame/*.png (deterministic, full colour; the UI tints some of them: lens, LED,
key cap). AssetBuilder.UI BuildSafeCrack / BuildVaultLock use them; 9-slice borders are in AssetBuilder (MinigameBorders).
"""
import math
import os

import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Game", "Art", "Sprites", "UI", "Minigame")
SS = 3  # supersampling


def grid(w, h):
    """Pixel centres in -1..1 (y up), at supersampled resolution."""
    ys, xs = np.mgrid[0:h * SS, 0:w * SS].astype(np.float64)
    x = (xs + 0.5) / (w * SS) * 2 - 1
    y = 1 - (ys + 0.5) / (h * SS) * 2
    return x, y


def save(name, rgb, alpha, w, h):
    rgb = np.clip(rgb, 0, 1)
    alpha = np.clip(alpha, 0, 1)
    arr = np.dstack([rgb[..., 0], rgb[..., 1], rgb[..., 2], alpha])
    img = Image.fromarray((arr * 255 + 0.5).astype(np.uint8), "RGBA")
    # premultiply-ish downsample: resize colour weighted by alpha, so edges do not go dark
    pre = np.dstack([rgb * alpha[..., None], alpha])
    pimg = Image.fromarray((np.clip(pre, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").resize((w, h), Image.LANCZOS)
    p = np.asarray(pimg).astype(np.float64) / 255
    a = p[..., 3]
    c = np.where(a[..., None] > 1e-4, p[..., :3] / np.maximum(a[..., None], 1e-4), 0)
    out = np.dstack([np.clip(c, 0, 1), a])
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"))
    print("wrote", name, w, h)


def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def noise(shape, seed, blur=0.0):
    rng = np.random.default_rng(seed)
    n = rng.random(shape)
    if blur > 0:
        img = Image.fromarray((n * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(blur))
        n = np.asarray(img).astype(np.float64) / 255
    return n


def rrect_sdf(px, py, hw, hh, r):
    """Signed distance (pixels) to a rounded rectangle centred at 0 with half sizes hw, hh."""
    qx = np.abs(px) - (hw - r)
    qy = np.abs(py) - (hh - r)
    out = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    inside = np.minimum(np.maximum(qx, qy), 0)
    return out + inside - r


def mix(a, b, t):
    """Lerp of colours (shape (3,) or (h, w, 3)) or of scalar fields (shape (h, w))."""
    a, b, t = np.asarray(a, dtype=np.float64), np.asarray(b, dtype=np.float64), np.asarray(t, dtype=np.float64)
    colour = a.ndim in (1, 3) or b.ndim in (1, 3)
    if colour and t.ndim == 2:
        t = t[..., None]
    return a * (1 - t) + b * t


def col(hexs):
    hexs = hexs.lstrip("#")
    return np.array([int(hexs[i:i + 2], 16) / 255 for i in (0, 2, 4)])


def screw(rgb, alpha, px, py, cx, cy, r, slot_angle):
    d = np.hypot(px - cx, py - cy)
    disc = smooth(r + 0.8, r - 0.8, d)
    # domed head: lit from the top left
    nx, ny = (px - cx) / r, (py - cy) / r
    light = np.clip(0.55 - 0.45 * (nx * -0.7 + ny * -0.7) * -1, 0, 1)
    head = mix(col("#3a3f47"), col("#c9ced6"), np.clip(0.5 + 0.5 * (-nx * 0.7 + ny * -0.7) * -1, 0, 1) ** 1.4)
    rim = smooth(r - 2.2, r - 0.6, d)
    head = mix(head, col("#15181c"), rim * 0.8)
    # the slot
    ca, sa = math.cos(slot_angle), math.sin(slot_angle)
    along = (px - cx) * ca + (py - cy) * sa
    across = -(px - cx) * sa + (py - cy) * ca
    slot = smooth(1.6, 0.6, np.abs(across)) * smooth(r * 0.75, r * 0.6, np.abs(along))
    head = mix(head, col("#0b0d10"), slot)
    _ = light
    rgb[:] = mix(rgb, head, disc)
    alpha[:] = np.maximum(alpha, disc)


# --------------------------------------------------------------------------------------------------- panels

def panel():
    """A gunmetal panel for 9-slicing: bevelled edge, engraved groove inside, a screw in each corner (border 48)."""
    W = H = 160
    x, y = grid(W, H)
    px, py = x * W * SS / 2, y * H * SS / 2
    half = W * SS / 2
    r = 26 * SS
    d = rrect_sdf(px, py, half - 2 * SS, half - 2 * SS, r)
    alpha = smooth(0.8 * SS, -0.8 * SS, d)
    # body: a vertical gradient of gunmetal
    body = mix(col("#0b0d10"), col("#1d222a"), (y * 0.5 + 0.5) ** 1.3)
    rgb = np.broadcast_to(body, body.shape).copy()
    # outer bevel: bright on top-left, dark at the bottom right
    edge = smooth(-5 * SS, -1 * SS, d)
    lit = np.clip(0.5 + 0.5 * (y * 0.8 - x * 0.35), 0, 1)
    rgb = mix(rgb, mix(col("#05060a"), col("#6b7380"), lit), edge * 0.9)
    # the very rim: a thin dark line
    rgb = mix(rgb, col("#020304"), smooth(-1.4 * SS, 0, d))
    # engraved groove 12 px in: dark line with a highlight under it
    g = d + 13 * SS
    rgb = mix(rgb, col("#050608"), smooth(1.2 * SS, 0, np.abs(g)) * 0.9)
    rgb = mix(rgb, col("#3a414c"), smooth(1.0 * SS, 0, np.abs(g + 1.6 * SS)) * 0.6)
    # corner screws (inside the 48 px border)
    for sx, sy, a in ((-1, 1, 0.4), (1, 1, 1.1), (-1, -1, 2.2), (1, -1, 0.9)):
        screw(rgb, alpha, px, py, sx * (half - 24 * SS), sy * (half - 24 * SS), 6.5 * SS, a)
    save("mg_panel", rgb, alpha, W, H)


def plate():
    """A brushed brass name plate (9-slice, border 22 x 20) with a rivet at each end."""
    W, H = 256, 48
    x, y = grid(W, H)
    px, py = x * W * SS / 2, y * H * SS / 2
    d = rrect_sdf(px, py, W * SS / 2 - SS, H * SS / 2 - SS, 9 * SS)
    alpha = smooth(0.8 * SS, -0.8 * SS, d)
    brush = noise((1, W * SS), 3, 0) * 0.5 + noise((H * SS, W * SS), 4, 0.6) * 0.5
    brush = np.broadcast_to(brush, px.shape)
    base = mix(col("#7a5418"), col("#e8bf62"), np.clip(0.55 + 0.45 * y, 0, 1))
    rgb = base * (0.9 + 0.16 * brush[..., None])
    # bevel
    edge = smooth(-4 * SS, -0.5 * SS, d)
    rgb = mix(rgb, mix(col("#3d2805"), col("#fff0c0"), np.clip(0.5 + 0.6 * y, 0, 1)), edge * 0.8)
    rgb = mix(rgb, col("#2a1a02"), smooth(-1.2 * SS, 0, d))
    for sx in (-1, 1):
        cx = sx * (W * SS / 2 - 12 * SS)
        dd = np.hypot(px - cx, py)
        rv = smooth(4.4 * SS, 3.4 * SS, dd)
        shade = mix(col("#5a3c0c"), col("#fff3c8"), np.clip(0.5 + 0.5 * ((py) / (4 * SS) * 0.8 - (px - cx) / (4 * SS) * 0.4), 0, 1))
        rgb = mix(rgb, shade, rv)
    save("mg_plate", rgb, alpha, W, H)


def window():
    """A recessed glass window for a code digit (9-slice, border 20): inner shadow on top, a glint line at the bottom."""
    W, H = 96, 64
    x, y = grid(W, H)
    px, py = x * W * SS / 2, y * H * SS / 2
    d = rrect_sdf(px, py, W * SS / 2 - SS, H * SS / 2 - SS, 12 * SS)
    alpha = smooth(0.8 * SS, -0.8 * SS, d)
    rgb = mix(col("#020304"), col("#0d1114"), np.clip(0.5 - 0.5 * y, 0, 1))
    rgb = np.broadcast_to(rgb, px.shape + (3,)).copy()
    # inner shadow from the top
    rgb *= (1 - 0.6 * smooth(-10 * SS, 0, d) * np.clip(y + 0.2, 0, 1))[..., None]
    # outer rim: a lit lip at the bottom (the panel's light from above), a dark one on top
    lip = smooth(-2.5 * SS, -0.5 * SS, d)
    rgb = mix(rgb, mix(col("#4a525e"), col("#06070a"), np.clip(0.5 + 0.6 * y, 0, 1)), lip)
    # a glass reflection band across the top
    band = smooth(0.25, 0.4, y) * smooth(0.95, 0.75, y) * smooth(0, -3 * SS, d)
    rgb = rgb + band[..., None] * 0.07
    save("mg_window", rgb, alpha, W, H)


def scope():
    """An oscilloscope screen (not sliced; shown at 330 x 86): phosphor-dark glass, a grid, a vignette, a reflection."""
    W, H = 660, 172
    x, y = grid(W, H)
    px, py = x * W * SS / 2, y * H * SS / 2
    d = rrect_sdf(px, py, W * SS / 2 - SS, H * SS / 2 - SS, 18 * SS)
    alpha = smooth(0.8 * SS, -0.8 * SS, d)
    rgb = np.broadcast_to(col("#031008"), px.shape + (3,)).copy()
    # grid: 10 x 4 divisions, a brighter centre line
    u = (x * 0.5 + 0.5) * 10
    v = (y * 0.5 + 0.5) * 4
    lw = 0.02
    gx = smooth(lw * 2, 0, np.abs(u - np.round(u)) / 1.0 * 4 / 10)
    gy = smooth(lw * 2, 0, np.abs(v - np.round(v)) / 1.0 * 4 / 10 * 0.4)
    gridv = np.maximum(gx, gy) * 0.18 + smooth(0.012, 0, np.abs(y)) * 0.18
    # minor ticks on the centre line
    mu = (x * 0.5 + 0.5) * 50
    tick = smooth(0.08, 0, np.abs(mu - np.round(mu)) / 50 * 10) * smooth(0.05, 0.02, np.abs(y))
    gridv = gridv + tick * 0.2
    rgb = rgb + gridv[..., None] * col("#5cff9a")
    # vignette
    vig = 1 - 0.55 * np.clip((x ** 2 * 0.6 + y ** 2 * 0.9), 0, 1)
    rgb *= vig[..., None]
    # bezel lip and inner shadow
    rgb *= (1 - 0.65 * smooth(-12 * SS, 0, d))[..., None]
    lip = smooth(-3 * SS, -0.6 * SS, d)
    rgb = mix(rgb, mix(col("#58616e"), col("#050608"), np.clip(0.5 + 0.6 * y, 0, 1)), lip)
    # glass reflection: a soft diagonal sheen at the top left
    sheen = smooth(0.2, 0.7, y - x * 0.35) * smooth(1.1, 0.75, y - x * 0.35) * smooth(0, -6 * SS, d)
    rgb = rgb + sheen[..., None] * 0.06
    save("mg_scope", rgb, alpha, W, H)


# --------------------------------------------------------------------------------------------------- the dial

def dial_face():
    """The dial's face: a graphite disc with fine concentric brushing and two broad sheen lobes (a fixed light), a
    recessed band near the rim where the ticks sit, a raised centre."""
    N = 512
    x, y = grid(N, N)
    r = np.hypot(x, y)
    a = np.arctan2(y, x)
    alpha = smooth(1.0, 0.994, r)
    ring_noise = noise((1, 4096), 9, 0)[0]
    rings = ring_noise[np.clip((r * 4095).astype(int), 0, 4095)]
    sheen = np.abs(np.cos(a - math.radians(130))) ** 6 * 0.55 + np.abs(np.cos(a - math.radians(130))) ** 40 * 0.4
    base = mix(col("#15181d"), col("#3a404a"), np.clip(sheen * (0.4 + 0.6 * r), 0, 1))
    rgb = base * (0.9 + 0.18 * rings[..., None])
    # the tick band: darker, sunken (shadow at its top inner edge)
    band = smooth(0.76, 0.765, r) * smooth(0.985, 0.98, r)
    rgb = mix(rgb, rgb * 0.45, band)
    rgb = mix(rgb, col("#000000"), smooth(0.012, 0, np.abs(r - 0.765)) * 0.7)
    rgb = mix(rgb, col("#7c8592"), smooth(0.008, 0, np.abs(r - 0.755)) * 0.5)
    # the raised centre plateau edge
    rgb = mix(rgb, mix(col("#05060a"), col("#9aa3af"), np.clip(0.5 + 0.5 * (y * 0.7 - x * 0.7) / np.maximum(r, 1e-3), 0, 1)),
              smooth(0.01, 0, np.abs(r - 0.42)) * 0.6)
    save("mg_dial_face", rgb, alpha, N, N)


def dial_bezel():
    """The chrome bezel: a knurled ring with a studio-like reflection (bright top, dark band, a glint low), a thin inner lip."""
    N = 512
    x, y = grid(N, N)
    r = np.hypot(x, y)
    a = np.arctan2(y, x)
    inner, outer = 0.87, 0.995
    alpha = smooth(outer, outer - 0.006, r) * smooth(inner - 0.006, inner, r)
    t = np.clip((r - inner) / (outer - inner), 0, 1)
    # a curved profile: the normal tilts outwards across the ring
    tilt = (t - 0.5) * 2
    ny = np.sin(a) * tilt
    env = 0.5 + 0.5 * np.sin((ny * 1.4 + 0.15) * math.pi)       # bright sky, dark horizon, a glint from the floor
    env = env ** 1.6
    knurl = 0.82 + 0.18 * (np.sin(a * 120) > 0)
    band = smooth(0.25, 0.3, t) * smooth(0.75, 0.7, t)          # knurled middle, smooth lips
    k = mix(1.0, knurl, band)
    rgb = mix(col("#202329"), col("#f2f5fa"), env) * k[..., None]
    rgb = mix(rgb, col("#08090b"), smooth(0.03, 0, np.abs(t - 0.0)) * 0.9)
    rgb = mix(rgb, col("#0a0b0e"), smooth(0.04, 0, np.abs(t - 1.0)) * 0.6)
    save("mg_dial_bezel", rgb, alpha, N, N)


def knob():
    """The knob that turns with the dial: 20 flutes round the grip, a brushed cap the number is shown on (no light:
    the gloss is a separate fixed sprite)."""
    N = 256
    x, y = grid(N, N)
    r = np.hypot(x, y)
    a = np.arctan2(y, x)
    alpha = smooth(1.0, 0.985, r)
    flute = 0.5 + 0.5 * np.cos(a * 20)
    grip = smooth(0.66, 0.7, r)
    rgb = mix(col("#2a2e35"), col("#767e8a"), flute ** 0.7)
    rgb = mix(col("#454b55"), rgb, grip)
    rgb = mix(rgb, col("#0d0f12"), smooth(0.02, 0, np.abs(r - 0.68)) * 0.8)
    # cap: a brushed disc
    cap_rings = noise((1, 2048), 21, 0)[0][np.clip((r * 2047).astype(int), 0, 2047)]
    rgb = rgb * mix(1.0, 0.92 + 0.14 * cap_rings, 1 - grip)[..., None]
    rgb = mix(rgb, col("#08090b"), smooth(0.03, 0, np.abs(r - 0.985)))
    save("mg_knob", rgb, alpha, N, N)


def knob_gloss():
    """A fixed light over the knob (white with alpha): a soft crescent at the top left and a hot spot."""
    N = 256
    x, y = grid(N, N)
    r = np.hypot(x, y)
    inside = smooth(0.99, 0.97, r)
    lit = np.clip(-x * 0.6 + y * 0.8, 0, 1)
    crescent = lit ** 2.2 * smooth(0.55, 0.95, r) * 0.32
    spot = np.exp(-(((x + 0.3) ** 2 + (y - 0.36) ** 2) / 0.03)) * 0.3
    a = (crescent + spot) * inside
    rgb = np.ones(x.shape + (3,))
    save("mg_knob_gloss", rgb, a, N, N)


def pointer():
    """The gold index pointer over the bezel: a bevelled downward triangle."""
    N = 96
    x, y = grid(N, N)
    # a triangle tip down: |x| < (y + 0.75) * 0.65, y in -0.75..0.75
    w = (y + 0.8) * 0.6
    inside = np.minimum(w - np.abs(x), np.minimum(0.75 - y, y + 0.8))
    alpha = smooth(-0.015, 0.015, inside)
    face = np.clip(inside * 6, 0, 1)
    rgb = mix(col("#7a4c08"), col("#ffe9a6"), np.clip(0.4 + 0.6 * (y * 0.5 + 0.5) - x * 0.4, 0, 1))
    rgb = mix(col("#3a2200"), rgb, smooth(0, 0.08, inside))
    _ = face
    save("mg_pointer", rgb, alpha, N, N)


# --------------------------------------------------------------------------------------------------- lamps and keys

def lamp_lens():
    """A glass lamp lens (tinted by the UI): bright core, darker rim, a crisp highlight; white so the tint is the colour."""
    N = 128
    x, y = grid(N, N)
    r = np.hypot(x, y)
    alpha = smooth(1.0, 0.97, r)
    core = 1 - 0.55 * r ** 2
    facets = 0.985 + 0.015 * np.cos(np.hypot(x, y) * 70)          # faint fresnel rings
    rgb = np.dstack([core, core, core]) * facets[..., None]
    hl = np.exp(-(((x + 0.3) ** 2) / 0.03 + ((y - 0.42) ** 2) / 0.012))
    rgb = rgb + hl[..., None] * 0.9
    rgb = mix(rgb, np.array([0.35, 0.35, 0.35]), smooth(0.85, 1.0, r) * 0.6)
    save("mg_lens", rgb, alpha, N, N)


def lamp_bezel():
    """A chrome bezel round a lamp: a ring with the panel's light from above, a black hole in the middle."""
    N = 128
    x, y = grid(N, N)
    r = np.hypot(x, y)
    a = np.arctan2(y, x)
    alpha = smooth(1.0, 0.975, r) * smooth(0.7, 0.73, r)
    t = np.clip((r - 0.72) / 0.28, 0, 1)
    ny = np.sin(a) * (t - 0.5) * 2
    env = (0.5 + 0.5 * np.sin((ny * 1.3 + 0.2) * math.pi)) ** 1.5
    rgb = mix(col("#1a1d22"), col("#e9edf3"), env)
    rgb = mix(rgb, col("#050607"), smooth(0.06, 0, t) * 0.9)
    rgb = mix(rgb, col("#050607"), smooth(0.05, 0, 1 - t) * 0.6)
    save("mg_lamp_bezel", rgb, alpha, N, N)


def keycap():
    """A key cap (9-slice, border 20; tinted by the UI): a dished top face, darker sides, a shadow under it."""
    N = 64
    x, y = grid(N, N)
    px, py = x * N * SS / 2, y * N * SS / 2
    half = N * SS / 2
    d_out = rrect_sdf(px, py, half - SS, half - SS, 12 * SS)
    d_top = rrect_sdf(px, py - 3 * SS, half - 8 * SS, half - 9 * SS, 9 * SS)
    alpha = smooth(0.8 * SS, -0.8 * SS, d_out)
    side = mix(np.array([0.42, 0.42, 0.44]), np.array([0.62, 0.62, 0.64]), np.clip(0.5 + 0.5 * y, 0, 1))
    top = mix(np.array([0.86, 0.86, 0.88]), np.array([1.0, 1.0, 1.0]), np.clip(0.5 + 0.5 * y, 0, 1))
    t = smooth(1.5 * SS, -1.5 * SS, d_top)
    rgb = mix(side, top, t)
    rgb = mix(rgb, np.array([0.18, 0.18, 0.2]), smooth(-1.5 * SS, 0, d_out))
    save("mg_key", rgb, alpha, N, N)


def led():
    """A small LED dome (tinted by the UI)."""
    N = 48
    x, y = grid(N, N)
    r = np.hypot(x, y)
    alpha = smooth(1.0, 0.93, r)
    core = 1 - 0.5 * r ** 2
    rgb = np.dstack([core, core, core])
    hl = np.exp(-(((x + 0.28) ** 2 + (y - 0.35) ** 2) / 0.03))
    rgb = rgb + hl[..., None] * 0.8
    rgb = mix(rgb, np.array([0.15, 0.15, 0.15]), smooth(0.8, 1.0, r) * 0.7)
    save("mg_led", rgb, alpha, N, N)


if __name__ == "__main__":
    panel()
    plate()
    window()
    scope()
    dial_face()
    dial_bezel()
    knob()
    knob_gloss()
    pointer()
    lamp_lens()
    lamp_bezel()
    keycap()
    led()
