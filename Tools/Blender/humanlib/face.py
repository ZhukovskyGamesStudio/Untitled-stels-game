"""Heads: a skull, a jaw, cheekbones, a nose, lips and lids blended into one field, by the measurements of a real
head — and a `Face` of proportions that makes one person out of it (four of them: PEOPLE).

The head's own frame: metres from its centre, x to its left, -y the face, z up. Landmarks of the neutral head:
crown z +0.125, chin z -0.105; the brow y -0.097, the eyes z +0.008 (their centres 12 mm behind the brow), the nose's
tip y -0.117 z -0.033, the mouth z -0.062, the ears x 0.075. Everything is symmetric (|x|).
"""
import math
from dataclasses import dataclass, field

import numpy as np

from .sdf import cone, ell, gauss, rot_x, rot_y, rot_z, smax, smin, win


@dataclass
class Face:
    """One person's proportions (1.0 / 0.0 = the neutral head)."""
    skull_w: float = 1.0        # the breadth of the skull
    face_w: float = 1.0         # the breadth across the cheekbones
    face_len: float = 1.0       # the length of the lower face (nose to chin)
    jaw_w: float = 1.0          # the breadth between the jaw's corners
    jaw_sq: float = 0.5         # 0 a pointed oval … 1 a square jaw
    chin_w: float = 1.0
    chin_fwd: float = 0.0       # metres the chin juts
    cheekbone: float = 1.0      # how much the cheekbones stand out
    cheek: float = 1.0          # the fullness of the cheeks
    brow: float = 1.0           # the brow ridge
    forehead: float = 0.0       # metres the forehead bulges
    eye_gap: float = 1.0
    eye_size: float = 1.0
    eye_open: float = 1.0
    eye_tilt: float = 0.09      # radians the outer corner is higher
    eye_deep: float = 0.0       # metres the eyes sit deeper
    nose_len: float = 1.0
    nose_w: float = 1.0
    nose_out: float = 1.0       # how far the tip stands out
    nose_hump: float = 0.0      # metres of hump on the bridge
    nose_up: float = 0.0        # metres the tip is turned up
    mouth_w: float = 1.0
    lips: float = 1.0           # their fullness
    smile: float = 0.0          # metres the corners of the mouth are lifted
    folds: float = 0.3          # the creases from the nose to the mouth's corners (age)
    ear: float = 1.0
    neck: float = 1.0
    female: bool = False


# --------------------------------------------------------------------------- the head

EYE_R = 0.0122


def eye_centre(f: Face):
    return np.array((0.0315 * f.eye_gap, -0.0665 + f.eye_deep, 0.008))


def _slit(q, f: Face):
    """The almond between the lids: the lens between an upper and a lower arc (pointed corners, the upper lid more
    arched), bored straight through the lids; its outer corner a little higher."""
    e = eye_centre(f)
    d = q - np.array((e[0], e[1], e[2] - 0.0006))
    c, s = math.cos(f.eye_tilt), math.sin(f.eye_tilt)
    x, z = d[..., 0] * c + d[..., 2] * s, d[..., 2] * c - d[..., 0] * s
    w, hu, hl = 0.0156 * f.eye_size, 0.0062 * f.eye_size * f.eye_open, 0.0040 * f.eye_size * f.eye_open
    ru, rl = (w * w + hu * hu) / (2 * hu), (w * w + hl * hl) / (2 * hl)
    lens = np.maximum(np.hypot(x, z - (hu - ru)) - ru, np.hypot(x, z + (hl - rl)) - rl)
    return np.maximum(lens, np.abs(d[..., 1] + 0.012) - 0.013)


def lips(q, f: Face):
    """The two lips (their own volumes: the map of the face colours them)."""
    z = -0.040 - 0.022 * f.face_len
    up = ell(q, (0, -0.0905, z + 0.0044), (0.0235 * f.mouth_w, 0.0090, 0.0042 * f.lips))
    lo = ell(q, (0, -0.0890, z - 0.0054), (0.0190 * f.mouth_w, 0.0095, 0.0054 * f.lips))
    return np.minimum(up, lo)


def mouth_z(f: Face):
    return -0.040 - 0.022 * f.face_len


def head(p, f: Face):
    """The head without ears and hair: negative inside."""
    q = np.array(p, float)
    q[..., 0] = np.abs(q[..., 0])
    L = f.face_len
    chin_z = -0.040 - 0.052 * L                                                             # the chin's centre
    d = ell(q, (0, 0.014, 0.040), (0.0775 * f.skull_w, 0.092, 0.086))                        # the brain case
    d = smin(d, ell(q, (0, -0.036 - f.forehead, 0.046), (0.062 * f.skull_w, 0.056, 0.060)), 0.03)   # the forehead
    d = smin(d, ell(q, (0, -0.028, -0.022), (0.0605 * f.face_w, 0.062, 0.058)), 0.03)        # the mask of the face
    d = smin(d, ell(q, (0, -0.058, -0.030 - 0.026 * L), (0.031, 0.034, 0.028)), 0.022)       # the muzzle: both jaws' teeth
    gon = np.array((0.049 * f.jaw_w, 0.006, -0.030 - 0.036 * L))                             # the jaw's corner
    chin = np.array((0.016 * f.chin_w, -0.070 - f.chin_fwd, chin_z))
    mid = gon + (chin - gon) * 0.5 + np.array((0.006 * f.jaw_sq, 0.0, -0.004 * f.jaw_sq))     # a square jaw bows out and down
    d = smin(d, cone(q, gon, mid, 0.0125, 0.0125), 0.03)
    d = smin(d, cone(q, mid, chin, 0.0125, 0.0135), 0.03)
    d = smin(d, cone(q, gon, (0.063 * f.face_w, 0.012, -0.016), 0.012, 0.011), 0.022)          # the jaw's branch up to the ear
    d = smin(d, ell(q, (0, -0.078 - f.chin_fwd, chin_z - 0.001), (0.021 * f.chin_w, 0.016, 0.0155)), 0.016)   # the chin
    d = smin(d, ell(q, (0, -0.020, chin_z + 0.022), (0.034 * f.jaw_w, 0.048, 0.020)), 0.025)   # the floor of the mouth
    d = smin(d, ell(q, (0.050 * f.face_w, -0.058, -0.015), (0.019, 0.021, 0.013 * f.cheekbone)), 0.026)   # cheekbones
    d = smin(d, cone(q, (0.058 * f.face_w, -0.046, -0.013), (0.071 * f.face_w, -0.004, -0.011), 0.0075, 0.0065), 0.02)   # … their arch to the ear
    d = smin(d, ell(q, (0.037, -0.061, -0.026 - 0.018 * L), (0.0185 * f.cheek, 0.0165 * f.cheek, 0.024)), 0.03)  # cheeks
    d = smin(d, cone(q, (0.0, -0.0915, 0.0255), (0.050, -0.077, 0.0285), 0.0066 * f.brow, 0.0048 * f.brow), 0.022)  # the brow ridge
    d = smax(d, -ell(q, (0.089 * f.skull_w, -0.046, 0.030), (0.014, 0.032, 0.032)), 0.03)        # temples
    e = eye_centre(f)
    d = smax(d, -ell(q, (e[0] + 0.001, -0.094 + f.eye_deep, e[2] - 0.002), (0.0200, 0.0135, 0.0125)), 0.018)   # eye sockets
    # the nose: the bridge from between the eyes down to the tip, the tip, the wings, the nostrils
    tip = np.array((0.0, -0.1025 - 0.0115 * f.nose_out, -0.008 - 0.0235 * f.nose_len + f.nose_up))
    root = np.array((0.0, -0.0865, 0.012))
    d = smin(d, cone(q, root, tip, 0.0052, 0.0060), 0.010)
    d = smin(d, ell(q, (0, tip[1] + 0.022, (root[2] + 2 * tip[2]) / 3), (0.0085 * f.nose_w, 0.010, 0.018 * f.nose_len)), 0.010)   # its sides sloping to the cheeks
    if f.nose_hump:
        d = smin(d, ell(q, root + (tip - root) * 0.42 + np.array((0, -f.nose_hump, 0)), (0.0058, 0.0058, 0.010)), 0.008)
    d = smin(d, ell(q, tip + np.array((0, 0.0025, 0.0005)), (0.0064 * f.nose_w, 0.0062, 0.0056)), 0.007)
    wing = np.array((0.0112 * f.nose_w, tip[1] + 0.0140, tip[2] - 0.0042 - f.nose_up * 0.6))
    d = smin(d, ell(q, wing, (0.0058 * f.nose_w, 0.0088, 0.0060)), 0.009)
    d = smin(d, cone(q, tip + np.array((0, 0.004, -0.004)), (0.0, tip[1] + 0.017, tip[2] - 0.0085), 0.0034, 0.0038), 0.004)   # the pillar between the nostrils
    d = smin(d, ell(q, (0, tip[1] + 0.0150, tip[2] - 0.0052), (0.0115 * f.nose_w, 0.0095, 0.0055)), 0.006)       # the nose's base
    d = smax(d, -ell(q, (0.0060 * f.nose_w, tip[1] + 0.0135, tip[2] - 0.0112 - f.nose_up * 0.5), (0.0028, 0.0046, 0.0030)), 0.002)   # nostrils: they open downwards
    # the mouth: its mound, the lips, the line between them, the dip under the nose and the one over the chin
    mz = mouth_z(f)
    d = smin(d, ell(q, (0, -0.069, mz - 0.001), (0.032 * f.mouth_w, 0.022, 0.022)), 0.014)
    d = smin(d, lips(q, f), 0.004)
    corner = np.array((0.0245 * f.mouth_w, -0.0800, mz - 0.0005 + f.smile))
    d = smax(d, -cone(q, (0.0, -0.1005, mz - 0.0002), corner, 0.0010, 0.0009), 0.0018)        # the line of the mouth
    d = smax(d, -cone(q, (0, tip[1] + 0.0165, tip[2] - 0.0115), (0, -0.0995, mz + 0.0090), 0.0012, 0.0016), 0.003)   # the philtrum
    d = smax(d, -ell(q, (0, -0.0950, mz - 0.0165), (0.017 * f.mouth_w, 0.0070, 0.0050)), 0.008)   # the dip over the chin
    if f.folds:
        a = wing + np.array((0.004, 0.004, 0.002))
        d = smax(d, -cone(q, a, corner + np.array((0.009, 0.003, 0.006)), 0.0010 * f.folds, 0.0005 * f.folds), 0.008)   # nose-to-mouth creases
    # the eyes: lids over the ball, their crease, the slit
    d = smin(d, ell(q, e, (EYE_R + 0.0024,) * 3), 0.009)
    d = smax(d, -cone(q, e + np.array((-0.011, -0.0135, 0.0100)), e + np.array((0.0125, -0.0100, 0.0088)), 0.0008), 0.0025)   # the upper lid's crease
    d = smax(d, -_slit(q, f), 0.0012)
    return d


def ear(p):
    """An ear in its own frame: x out of the head, y to the back, z up."""
    d = ell(p, (0.004, 0.002, 0.0), (0.0062, 0.0160, 0.0300))                             # the shell
    d = smin(d, ell(p, (0.003, -0.006, -0.0255), (0.0048, 0.0078, 0.0088)), 0.006)        # the lobe
    d = smin(d, ell(p, (-0.004, -0.008, -0.002), (0.009, 0.010, 0.020)), 0.006)           # where it grows from the head
    d = smax(d, -ell(p, (0.0100, -0.003, -0.004), (0.0058, 0.0078, 0.0105)), 0.004)       # the bowl
    d = smax(d, -ell(p, (0.0105, 0.004, 0.013), (0.0048, 0.0068, 0.0098)), 0.004)         # the hollow under the rim
    d = smin(d, ell(p, (0.0060, -0.0140, -0.006), (0.0038, 0.0038, 0.0048)), 0.003)       # the tragus
    return d


EAR_AT = np.array((0.0735, 0.012, -0.002))
EAR_ROT = np.array(rot_z(math.radians(-18)) @ rot_x(math.radians(-8)))


def head_with_ears(p, f: Face):
    p = np.asarray(p, float)
    q = p.copy()
    q[..., 0] = np.abs(q[..., 0])
    local = ((q - EAR_AT * np.array((f.skull_w, 1, 1))) @ EAR_ROT) / (0.93 * f.ear)
    return smin(head(p, f), ear(local) * 0.93 * f.ear, 0.0035)


# --------------------------------------------------------------------------- hair

def _scalp(p, f, thick):
    """The head let out by `thick` metres: hair lying on it."""
    return head(p, f) - thick


def _face_cut(q, f, fringe=0.0, temple=0.0):
    """What a hairline leaves bare: the face from the forehead's hairline down, forward of the ears."""
    return ell(q, (0, -0.139, -0.040 + fringe), (0.094 - temple, 0.126, 0.128))


def _strands(p, amount=1.0):
    """Combed locks: ridges fanning out from the whorl at the back of the crown (0..1, 1 on a lock's crest)."""
    w = np.array((0.02, 0.045, 0.118))
    v = p - w
    e1 = np.array((1.0, 0.0, 0.0))
    e2 = np.array((0.0, -0.92, 0.39))
    phi = np.arctan2(v @ e2, v @ e1)
    r = np.linalg.norm(v, axis=-1)
    locks = np.abs(np.sin(7.0 * phi + 9.0 * r + 0.8 * np.sin(31.0 * r + 2.0 * phi)))
    fine = 0.5 + 0.5 * np.sin(23.0 * phi + 40.0 * r)
    return (0.75 * locks ** 0.7 + 0.25 * fine) * amount


def hair(p, f: Face, style: str):
    """Hair as a volume over the skull, thinning to nothing at the hairline, in combed locks. Styles: crop (short, a
    side parting), messy (longer on top, pushed forward), bob (to the jaw, a fringe), bun (pulled back to a knot at
    the nape), buzz (a few millimetres)."""
    p = np.asarray(p, float)
    q = p.copy()
    q[..., 0] = np.abs(q[..., 0])
    y, z = p[..., 1], p[..., 2]
    ear_hole = ell(q, (0.080 * f.skull_w, 0.014, -0.004), (0.019, 0.0180, 0.0280))
    nape = lambda low: -(z + 0.44 * (y + 0.02) + low) / 1.09                        # everything under the nape's line
    locks = _strands(p)

    def lying(thick, cut, low, groove=0.0016):
        """Hair of a thickness lying on the scalp inside a hairline: tapering to it, grooved between the locks."""
        edge = np.clip(np.minimum(cut, -nape(low)) / 0.016, 0.0, 1.0) ** 0.7        # 0 at the hairline … 1 well inside it
        d = head(p, f) - (0.0012 + thick * edge) + groove * (1.0 - locks) * edge
        d = smax(d, -(cut + 0.005 * (locks - 0.55)), 0.0015)                        # (the hairline breaks into locks)
        d = smax(d, nape(low) - 0.004 * (locks - 0.55), 0.003)
        return d

    if style == "buzz":
        return smax(lying(0.0030, _face_cut(q, f, 0.004, -0.002), 0.030, 0.0004), -ear_hole, 0.002)
    if style in ("crop", "messy"):
        top = 0.013 if style == "crop" else 0.019
        t = 0.005 + top * np.clip((z - 0.03) / 0.07, 0, 1)                          # short at the sides, longer on top
        d = lying(t, _face_cut(q, f, 0.0 if style == "crop" else 0.004), 0.022)
        if style == "crop":
            d = smin(d, ell(p, (0.018, -0.060, 0.094), (0.048, 0.022, 0.016)) + 0.0016 * (1 - locks), 0.014)   # the fringe swept to one side
            d = smax(d, -cone(p, (-0.034, -0.085, 0.108), (-0.040, 0.020, 0.126), 0.0024), 0.005)              # the parting
        else:
            d = smin(d, ell(p, (0.004, -0.052, 0.100), (0.056, 0.036, 0.020)) + 0.0022 * (1 - locks), 0.018)    # a mop pushed forward
            d = smin(d, ell(p, (-0.028, -0.078, 0.082), (0.026, 0.016, 0.015)) + 0.0018 * (1 - locks), 0.012)   # … falling on the forehead
        return smax(d, -ear_hole, 0.003)
    if style == "bob":
        t = 0.007 + 0.007 * np.clip((z - 0.0) / 0.08, 0, 1)
        d = head(p, f) - t + 0.0018 * (1 - locks)
        d = smin(d, ell(q, (0.060 * f.skull_w, 0.024, -0.040), (0.022, 0.056, 0.058)) + 0.0020 * (1 - locks), 0.022)   # the sides falling to the jaw
        d = smin(d, ell(p, (0, 0.066, -0.040), (0.064, 0.032, 0.058)) + 0.0020 * (1 - locks), 0.024)                   # the back
        d = smin(d, ell(p, (0.0, -0.064, 0.084), (0.058, 0.020, 0.018)) + 0.0016 * (1 - locks), 0.012)                 # the fringe
        d = smax(d, -ell(q, (0, -0.150, -0.050), (0.080, 0.134, 0.124)), 0.003)                 # the face looks out of it
        d = smax(d, -(z + 0.080 + 0.008 * (locks - 0.5)), 0.004)                               # cut level at the jaw, in locks
        return d
    if style == "bun":
        t = 0.007 + 0.004 * np.clip((z - 0.02) / 0.07, 0, 1)
        d = lying(t, _face_cut(q, f, 0.006, -0.010), 0.036, 0.0014)
        d = smin(d, ell(p, (0, 0.100, 0.010), (0.034, 0.030, 0.032)) + 0.0016 * (1 - locks), 0.014)   # the knot
        return smax(d, -ear_hole, 0.003)
    raise ValueError(style)


def beard_zone(q, f: Face):
    """Where a short boxed beard grows, as metres inside its outline (negative outside), for points mirrored to x >= 0:
    under the cheek line (from the sideburn in front of the ear, sagging a little, to above the mouth's corner), in front
    of the jaw's corner, above the neckline under the jaw (rising towards the ear)."""
    y, z = q[..., 1], q[..., 2]
    mz = mouth_z(f)
    t = np.clip((-0.010 - y) / 0.075, 0.0, 1.0)                       # 0 at the sideburn … 1 at the mouth's corner
    cheek_line = 0.006 + (mz + 0.008 - 0.006) * t - 0.009 * np.sin(np.pi * t)        # (it starts in the sideburn: no bare gap to the hair)
    chin_z = -0.040 - 0.052 * f.face_len
    neckline = chin_z - 0.030 + 0.42 * np.clip(y + 0.070, 0.0, 0.2)   # under the chin, rising to the jaw's corner
    return np.minimum(np.minimum(cheek_line - z, 0.014 - y), z - neckline)


def moustache_zone(q, f: Face):
    """Metres inside the moustache's outline: a soft crescent between the nose's base and the upper lip, drooping to
    just past the mouth's corners."""
    mz = mouth_z(f)
    x, y, z = q[..., 0], q[..., 1], q[..., 2]
    half_w = 0.034 * f.mouth_w
    mid = mz + 0.0130 - 0.0050 * (x / half_w) ** 2                    # its middle line droops towards the corners
    half_h = 0.0078 * np.sqrt(np.clip(1.0 - (x / half_w) ** 2, 0.0, 1.0)) + 0.0006
    return np.minimum(np.minimum(half_h - np.abs(z - mid), half_w - x), -0.064 - y)


def beard(p, f: Face, length=0.0050):
    """A short boxed beard and a moustache: hair lying on the jaw, fullest on the chin and thinning to nothing along
    the cheek line and the neckline (no edge: the skin under it is painted with its shadow), the lips bare. Its
    surface is plain — the hairs are painted and lit by their own roughness, not sculpted in stripes."""
    p = np.asarray(p, float)
    q = p.copy()
    q[..., 0] = np.abs(q[..., 0])
    y = q[..., 1]
    skin = head(p, f)
    inside = beard_zone(q, f)
    edge = np.clip(inside / 0.012, 0.0, 1.0)
    edge = edge * edge * (3 - 2 * edge)
    full = 0.55 + 0.45 * np.clip((-0.045 - y) / 0.03, 0.0, 1.0)        # fuller on the chin than on the cheeks
    tufts = 0.5 + 0.5 * np.sin(q[..., 0] * 190 + 2.2 * np.sin(q[..., 2] * 130)) * np.sin(q[..., 2] * 170 + 1.7 * np.sin(y * 150))
    d = skin - (0.0004 + length * edge * full) + 0.0005 * tufts * edge
    d = smax(d, -(inside + 0.001), 0.0012)
    tin = moustache_zone(q, f)
    te = np.clip(tin / 0.0045, 0.0, 1.0)
    tash = skin - (0.0004 + length * 0.75 * te * te * (3 - 2 * te)) + 0.0004 * tufts * te
    tash = smax(tash, -(tin + 0.0008), 0.001)
    d = smin(d, tash, 0.003)
    mz = mouth_z(f)
    mouth = ell(q, (0, -0.094, mz - 0.0020), (0.0235 * f.mouth_w, 0.016, 0.0085 * f.lips))   # the lips and the corners of the mouth: bare
    d = smax(d, -smin(lips(q, f) - 0.0032, mouth, 0.003), 0.0025)
    return d


# --------------------------------------------------------------------------- the four people

@dataclass
class Person:
    face: Face
    hair: str
    hair_colour: tuple
    iris: tuple
    beard: bool = False
    stubble: float = 0.0        # 0..1 the shadow on the jaw
    brow_colour: tuple = None
    brow_thick: float = 1.0
    lip_colour: float = 0.5     # 0 pale … 1 painted
    freckles: float = 0.0
    eye_shadow: float = 0.2     # the darkness round the eyes (tired)


PEOPLE = {
    # the backend developer: a lean long face, a strong nose with a hump, a mop of dark hair, two days of stubble
    "backend": Person(Face(face_w=0.95, face_len=1.07, jaw_w=0.93, jaw_sq=0.35, cheekbone=1.1, cheek=0.75, brow=1.1, eye_deep=0.001,
                           nose_len=1.08, nose_out=1.12, nose_hump=0.0022, nose_w=0.95, lips=0.85, mouth_w=0.98, folds=0.45),
                      "messy", (0.10, 0.075, 0.06), (0.30, 0.22, 0.13), stubble=0.8, brow_thick=1.15, eye_shadow=0.45),
    # the QA engineer: a round soft face, a short turned-up nose, big eyes, a chestnut bob with a fringe
    "qa": Person(Face(skull_w=0.98, face_w=0.98, face_len=0.93, jaw_w=0.84, jaw_sq=0.0, chin_w=0.74, cheekbone=0.85, cheek=1.2, brow=0.35,
                      forehead=0.002, eye_size=1.1, eye_open=1.12, eye_tilt=0.12, nose_len=0.90, nose_w=0.90, nose_out=0.86, nose_up=0.0022,
                      lips=1.15, mouth_w=0.95, smile=0.0012, folds=0.1, ear=0.94, neck=0.86, female=True),
                 "bob", (0.34, 0.17, 0.08), (0.20, 0.42, 0.30), brow_thick=0.75, lip_colour=0.75, freckles=0.6, eye_shadow=0.1),
    # the sysadmin: a broad square head, a heavy brow, a wide nose, cropped hair and a full beard
    "sysadmin": Person(Face(skull_w=1.04, face_w=1.06, face_len=1.0, jaw_w=1.13, jaw_sq=1.0, chin_w=1.2, cheekbone=0.95, cheek=1.15, brow=1.35,
                            eye_size=0.94, eye_open=0.9, eye_tilt=0.05, eye_deep=0.0015, nose_w=1.18, nose_len=0.98, nose_out=0.98,
                            lips=1.0, mouth_w=1.04, folds=0.6, ear=1.05, neck=1.12),
                       "buzz", (0.33, 0.20, 0.11), (0.25, 0.38, 0.52), beard=True, brow_thick=1.3, eye_shadow=0.3),
    # the designer: a heart-shaped face, high cheekbones, a pointed chin, full lips, black hair pulled back under the beret
    "designer": Person(Face(skull_w=0.97, face_w=0.97, face_len=0.95, jaw_w=0.80, jaw_sq=0.0, chin_w=0.66, chin_fwd=0.001, cheekbone=1.25, cheek=0.9,
                            brow=0.4, eye_size=1.04, eye_open=1.0, eye_tilt=0.17, nose_len=0.90, nose_w=0.80, nose_out=0.84, nose_up=0.0014,
                            lips=1.3, mouth_w=1.0, folds=0.0, ear=0.92, neck=0.84, female=True),
                       "bun", (0.045, 0.04, 0.045), (0.16, 0.11, 0.07), brow_thick=0.8, lip_colour=1.0, eye_shadow=0.15),
}
# the station scientist of the old S16 look: a plain neutral man
PEOPLE["scientist"] = Person(Face(), "crop", (0.16, 0.12, 0.09), (0.3, 0.3, 0.25), stubble=0.4)
