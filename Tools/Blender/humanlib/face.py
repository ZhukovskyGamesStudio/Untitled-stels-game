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


def _slit(q, f: Face, flat=False):
    """The almond between the lids: the lens between an upper and a lower arc (pointed corners, the upper lid more
    arched), bored straight through the lids; its outer corner a little higher. flat: the almond alone, as seen from
    the front (metres outside its outline, whatever the depth)."""
    e = eye_centre(f)
    d = q - np.array((e[0], e[1], e[2] - 0.0017))
    c, s = math.cos(f.eye_tilt), math.sin(f.eye_tilt)
    x, z = d[..., 0] * c + d[..., 2] * s, d[..., 2] * c - d[..., 0] * s
    w, hu, hl = 0.0150 * f.eye_size, 0.0050 * f.eye_size * f.eye_open, 0.0040 * f.eye_size * f.eye_open
    ru, rl = (w * w + hu * hu) / (2 * hu), (w * w + hl * hl) / (2 * hl)
    lens = np.maximum(np.hypot(x, z - (hu - ru)) - ru, np.hypot(x, z + (hl - rl)) - rl)
    if flat:
        return lens
    # bored only in front of the ball and of a floor 6 mm before its centre: the almond is wider than the ball, and a bore
    # straight through left black holes in the corners — now the corners are shallow pockets (the caruncle is painted in)
    ball = (EYE_R - 0.0004) - np.linalg.norm(q - e, axis=-1)
    floor = q[..., 1] - (e[1] - 0.006)
    return np.maximum(np.maximum(lens, ball), np.maximum(floor, -0.025 - (q[..., 1] - e[1])))


def lips(q, f: Face):
    """The two lips (their own volumes: the map of the face colours them)."""
    z = -0.040 - 0.022 * f.face_len
    # the upper lip's Cupid's bow: two peaks either side of a dip over the middle (the whole lip follows it, so the line
    # of the mouth dips a little in the middle too, under the lip's tubercle)
    ax = np.abs(q[..., 0])
    bow = 0.0011 * np.exp(-((ax - 0.0055 * f.mouth_w) / 0.0040) ** 2) - 0.0007 * np.exp(-(ax / 0.0028) ** 2)
    qu = np.array(q, float)
    qu[..., 2] = q[..., 2] - bow * np.clip((q[..., 2] - (z + 0.0036)) / (0.0044 * f.lips), 0, 1)   # (the top edge only: the line of the mouth stays shut)
    up = ell(qu, (0, -0.0900, z + 0.0036), (0.0235 * f.mouth_w, 0.0105, 0.0044 * f.lips))
    lo = ell(q, (0, -0.0885, z - 0.0045), (0.0190 * f.mouth_w, 0.0108, 0.0056 * f.lips))
    return np.minimum(up, lo)


def mouth_z(f: Face):
    return -0.040 - 0.022 * f.face_len


def head(p, f: Face, creases=True):
    """The head without ears and hair: negative inside. (creases=False: without the creases that follow its surface — the
    surface they are laid on.)"""
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
    d = smin(d, cone(q, root, tip, 0.0040, 0.0056), 0.013)
    d = smin(d, ell(q, (0, tip[1] + 0.025, (root[2] + 2 * tip[2]) / 3), (0.0125 * f.nose_w, 0.011, 0.020 * f.nose_len)), 0.015)   # its sides sloping to the cheeks
    if f.nose_hump:
        d = smin(d, ell(q, root + (tip - root) * 0.42 + np.array((0, -f.nose_hump, 0)), (0.0058, 0.0058, 0.010)), 0.008)
    d = smin(d, ell(q, tip + np.array((0, 0.0025, 0.0005)), (0.0064 * f.nose_w, 0.0062, 0.0056)), 0.007)
    wing = np.array((0.0112 * f.nose_w, tip[1] + 0.0140, tip[2] - 0.0042 - f.nose_up * 0.6))
    d = smin(d, ell(q, wing, (0.0058 * f.nose_w, 0.0088, 0.0060)), 0.009)
    d = smin(d, cone(q, tip + np.array((0, 0.004, -0.004)), (0.0, tip[1] + 0.017, tip[2] - 0.0085), 0.0034, 0.0038), 0.004)   # the pillar between the nostrils
    d = smin(d, ell(q, (0, tip[1] + 0.0150, tip[2] - 0.0052), (0.0115 * f.nose_w, 0.0095, 0.0055)), 0.006)       # the nose's base
    d = smax(d, -ell(q, (0.0060 * f.nose_w, tip[1] + 0.0135, tip[2] - 0.0100 - f.nose_up * 0.5), (0.0028, 0.0046, 0.0026)), 0.002)   # nostrils: they open downwards
    # (reaching 3.5 mm under the nose's base, they bored into the upper lip below it and left crumbs of skin there)
    # the mouth: its mound, the lips, the line between them, the dip under the nose and the one over the chin
    mz = mouth_z(f)
    d = smin(d, ell(q, (0, -0.069, mz - 0.001), (0.032 * f.mouth_w, 0.022, 0.022)), 0.014)
    d = smin(d, lips(q, f), 0.004)
    corner = np.array((0.0245 * f.mouth_w, -0.0800, mz - 0.0005 + f.smile))
    if creases:                                                                             # the line of the mouth, laid in the valley between
        d = smax(d, -_groove(q, _mouth_path(f), MOUTH_R), 0.0015)                           # the lips (a straight line missed the middle and
                                                                                            # bit pits in beside it)
    if creases:                                                                             # the philtrum, on the skin (a straight capsule
        d = smax(d, -_groove(q, _philtrum_path(f), PHILTRUM_R), 0.003)                      # dug a crumbling pit under the nose)
    d = smax(d, -ell(q, (0, -0.0950, mz - 0.0165), (0.017 * f.mouth_w, 0.0070, 0.0050)), 0.008)   # the dip over the chin
    if f.folds and creases:
        # nose-to-mouth creases: a soft valley that follows the cheek's surface from the nose's wing to beside the mouth's
        # corner, deepest near the nose and fading to nothing at its end (a straight capsule left the skin in its middle
        # and dived 6 mm into the mouth's mound at its end: a dark pit by each corner)
        d = smax(d, -_groove(q, _fold_path(f)), 0.003)
    # the eyes: lids over the ball, their crease, the slit
    d = smin(d, ell(q, e, (EYE_R + 0.0024,) * 3), 0.009)
    if creases:                                                                             # the upper lid's crease, on the lid (a straight
        d = smax(d, -_groove(q, _crease_path(f)), 0.0025)                                   # capsule cut a crater where the face fell away)
    d = smax(d, -_slit(q, f), 0.0012)
    return d


GROOVE_R = 0.0024
_GROOVES = {}


MOUTH_R = 0.0012


def _groove(q, pts, r=GROOVE_R):
    """A crease along a path of capsule axes held off the skin (see _on_skin): the capsules' union, to carve out."""
    out = None
    for a, b in zip(pts[:-1], pts[1:]):
        seg = cone(q, a, b, r)
        out = seg if out is None else np.minimum(out, seg)
    return out


def _on_skin(f: Face, name, a, b, depth, r=GROOVE_R):
    """The axis of a crease that follows the skin: points on the line a→b brought onto the surface of the head without
    its creases straight along the line of sight (the line keeps its course), then held off it so a capsule of GROOVE_R
    bites exactly depth(t) into the skin (Newton steps along the field's gradient: the skin curves). A straight capsule
    left the skin in one place and dived into it in another — a crease that vanished midway and ended in a pit."""
    key = (name, repr(f))
    if key in _GROOVES:
        return _GROOVES[key]
    t = np.linspace(0, 1, 9)[:, None]
    pts = np.asarray(a, float) + (np.asarray(b, float) - np.asarray(a, float)) * t
    skin = lambda x: head(x, f, creases=False)
    eps = 1e-4
    for _ in range(8):
        v = skin(pts)
        dy = (skin(pts + np.array((0, eps, 0))) - v) / eps
        pts[:, 1] -= v / np.where(np.abs(dy) < 0.2, -0.2, dy)
    v = skin(pts)
    g = np.stack([(skin(pts + np.eye(3)[i] * eps) - v) / eps for i in range(3)], -1)
    g /= np.linalg.norm(g, axis=-1, keepdims=True)
    hold = r - depth(t[:, 0])
    out = pts + g * hold[:, None]
    for _ in range(6):
        out = out + g * (hold - skin(out))[:, None]
    _GROOVES[key] = out
    return out


def _fold_path(f: Face):
    """The nose-to-mouth crease: from the nose's wing to beside the mouth's corner, deepest near the nose, nothing at
    the end."""
    tip = np.array((0.0, -0.1025 - 0.0115 * f.nose_out, -0.008 - 0.0235 * f.nose_len + f.nose_up))
    wing = np.array((0.0112 * f.nose_w, tip[1] + 0.0140, tip[2] - 0.0042 - f.nose_up * 0.6))
    mz = mouth_z(f)
    depth = lambda t: 0.0011 * f.folds * np.sin(np.pi * np.clip(t * 1.1 + 0.08, 0, 1)) ** 0.8 - 0.0003 * t ** 3
    return _on_skin(f, "fold", wing + np.array((0.0045, 0.0, 0.0015)), (0.0245 * f.mouth_w + 0.0085, -0.080, mz - 0.0015 + f.smile), depth)


def _mouth_path(f: Face):
    """The line of the mouth: from the middle to the corner, a millimetre deep, a little deeper into the corner."""
    mz = mouth_z(f)
    depth = lambda t: 0.0007 + 0.0002 * t * t
    return _on_skin(f, "mouth", (0.0, -0.13, mz - 0.0002), (0.0238 * f.mouth_w, -0.13, mz - 0.0005 + f.smile), depth, MOUTH_R)


PHILTRUM_R = 0.0030


def _philtrum_path(f: Face):
    """The philtrum: the shallow trough from under the nose down to the bow of the upper lip, fading at both ends."""
    tip = np.array((0.0, -0.1025 - 0.0115 * f.nose_out, -0.008 - 0.0235 * f.nose_len + f.nose_up))
    mz = mouth_z(f)
    depth = lambda t: 0.0006 * np.sin(np.pi * np.clip(t * 1.15 + 0.05, 0, 1)) ** 0.6 - 0.0002
    return _on_skin(f, "philtrum", (0.0, -0.13, tip[2] - 0.0125), (0.0, -0.13, mz + 0.0075), depth, PHILTRUM_R)


def _crease_path(f: Face):
    """The upper lid's crease: over the lid from the inner corner to the outer, fading out at both ends."""
    e = eye_centre(f)
    depth = lambda t: 0.0007 * np.sin(np.pi * np.clip(t * 1.1 - 0.05, 0, 1)) ** 0.7 - 0.0002
    return _on_skin(f, "crease", e + np.array((-0.011, -0.03, 0.0100)), e + np.array((0.0125, -0.03, 0.0088)), depth)


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
    # many narrow locks, a little wavy and of uneven width (seven broad ones read as the segments of a pumpkin)
    locks = np.abs(np.sin(17.0 * phi + 12.0 * r + 1.4 * np.sin(29.0 * r + 3.0 * phi) + 0.9 * np.sin(5.0 * phi)))
    fine = 0.5 + 0.5 * np.sin(53.0 * phi + 64.0 * r + 2.0 * np.sin(11.0 * phi))
    v = 0.6 * locks ** 0.6 + 0.4 * fine
    return (1.0 - 0.55 * (1.0 - v)) * amount                  # (shallow: hair is a mass with a grain, not ropes)


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

    def lying(thick, cut, low, groove=0.0010):
        """Hair of a thickness lying on the scalp inside a hairline: tapering to it, grooved between the locks."""
        edge = np.clip(np.minimum(cut, -nape(low)) / 0.036, 0.0, 1.0)               # 0 at the hairline … 1 well inside it: thinning to it, no rim
        edge = edge * edge * (3 - 2 * edge)
        d = head(p, f) - (0.0012 + thick * edge) + groove * (1.0 - locks) * edge
        d = smax(d, -(cut + 0.0022 * (locks - 0.75)), 0.0015)                       # (the hairline breaks into locks, a little)
        d = smax(d, nape(low) - 0.004 * (locks - 0.55), 0.003)
        return d

    if style == "buzz":
        return smax(lying(0.0030, _face_cut(q, f, 0.004, -0.002), 0.030, 0.0004), -ear_hole, 0.002)
    if style in ("crop", "messy"):
        top = 0.013 if style == "crop" else 0.012
        up = np.clip((z - 0.005) / 0.105, 0, 1)
        t = 0.0035 + top * up * up * (3 - 2 * up)                                   # short at the sides and the back (tapered, not a bowl), longer on top
        d = lying(t, _face_cut(q, f, 0.0 if style == "crop" else 0.004), 0.022)
        if style == "crop":
            d = smin(d, ell(p, (0.018, -0.060, 0.094), (0.048, 0.022, 0.016)) + 0.0016 * (1 - locks), 0.014)   # the fringe swept to one side
            d = smax(d, -cone(p, (-0.034, -0.085, 0.108), (-0.040, 0.020, 0.126), 0.0024), 0.005)              # the parting
        else:
            # a mop pushed forward: tapering locks from the crown that fall over the hairline and end in points (a blob
            # of an ellipsoid over the forehead ended in a blunt brim: a helmet, a bowl cut, from every side)
            for a, b, r in (((0.010, -0.025, 0.118), (-0.024, -0.090, 0.074), 0.0105), ((0.026, -0.030, 0.114), (0.030, -0.088, 0.072), 0.0095),
                            ((-0.012, -0.018, 0.121), (-0.048, -0.076, 0.078), 0.0090), ((0.042, -0.016, 0.110), (0.060, -0.068, 0.070), 0.0080),
                            ((-0.032, -0.010, 0.114), (-0.064, -0.050, 0.076), 0.0075), ((0.0, -0.040, 0.118), (0.006, -0.094, 0.080), 0.0085)):
                d = smin(d, cone(p, a, b, r, 0.0012) + 0.0016 * (1 - locks), 0.010)
        return smax(d, -ear_hole, 0.003)
    if style == "bob":
        t = 0.007 + 0.007 * np.clip((z - 0.0) / 0.08, 0, 1)
        d = head(p, f) - t + 0.0018 * (1 - locks)
        d = smin(d, ell(q, (0.066 * f.skull_w, 0.026, -0.040), (0.018, 0.054, 0.058)) + 0.0020 * (1 - locks), 0.022)   # the sides falling to the jaw
        d = smin(d, ell(p, (0, 0.066, -0.040), (0.064, 0.032, 0.058)) + 0.0020 * (1 - locks), 0.024)                   # the back
        d = smin(d, ell(p, (0.0, -0.064, 0.092), (0.058, 0.020, 0.014)) + 0.0016 * (1 - locks), 0.012)                 # the fringe: over the forehead, clear of the brows
        d = smax(d, -ell(q, (0, -0.150, -0.043), (0.096, 0.136, 0.126)), 0.006)                 # the face looks out of it, cheekbones and all
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
    mz = mouth_z(f)
    mouth = ell(q, (0, -0.094, mz - 0.0008), (0.0200 * f.mouth_w, 0.014, 0.0052 * f.lips))   # the lips and the corners of the mouth: bare
    bare = smin(lips(q, f) - 0.0014, mouth, 0.002)                     # metres outside the bare patch round the lips
    by_lips = np.clip(bare / 0.011, 0.0, 1.0)                          # the beard thins to the lips (a thick rim round them read as a mask's window)
    skin = smin(skin, ell(q, (0, -0.0900, mz - 0.0165), (0.017 * f.mouth_w, 0.0075, 0.0055)), 0.006)   # (hair fills the dip under the lower lip)
    # at its outline (and round the lips) the beard sinks SINK under the skin, so its edge is where it meets the skin, and
    # the cuts that bound it lie inside the head: ending in a sheet 0.4 mm over the skin (finer than the sculpt's 1.3 mm
    # cells) it tore into flaps and stood as a step all round in the game
    near_lips = np.clip((bare - 0.0004) / 0.0030, 0.0, 1.0)            # (it grows right up to the lips: a wide bare ring read as a mask's window)
    d = skin - (-SINK + (length * full * (0.25 + 0.75 * by_lips) + SINK) * edge * near_lips) + 0.0005 * tufts * edge
    d = smax(d, -(inside + 0.004), 0.002)
    tin = moustache_zone(q, f)
    te = np.clip(tin / 0.0030, 0.0, 1.0)
    te = te * te * (3 - 2 * te)
    # (the moustache keeps most of its thickness down to the lip: thinned like the beard, only a thin bar of it rose
    # above the skin, apart from the beard)
    tash = skin - (-SINK * 0.5 + (length * 0.75 * (0.65 + 0.35 * by_lips) + SINK * 0.5) * te * near_lips) + 0.0004 * tufts * te
    tash = smax(tash, -(tin + 0.004), 0.002)
    d = smin(d, tash, 0.003)
    d = smax(d, -(bare + 0.001), 0.002)
    return d


SINK = 0.0015


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
                 "bob", (0.19, 0.085, 0.04), (0.20, 0.42, 0.30), brow_thick=0.75, lip_colour=0.75, freckles=0.6, eye_shadow=0.1),
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
