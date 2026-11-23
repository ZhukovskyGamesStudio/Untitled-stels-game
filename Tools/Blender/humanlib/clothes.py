"""Clothes, hands and shoes as distance fields over the body (humanlib.body).

A garment is the body let out by the cloth's ease, hanging straight where cloth would (a trouser leg below the knee, a
hoodie below the chest), cut at its hems, hollow at the openings (a cuff, a hem: the wrist goes into the sleeve), with a
few deliberate folds where cloth really breaks — the crook of the elbow, behind the knee, the stack over the shoe, the
drag lines from the shoulder — instead of noise all over.
"""
import math
from dataclasses import replace

import numpy as np

from . import body as B
from .body import J, Build
from .sdf import box, cone, ell, gauss, plane, ridge, smax, smin, win


def _mirror(p):
    q = np.array(p, float)
    q[..., 0] = np.abs(q[..., 0])
    return q


def _open(d, depth_field, inner=0.0045):
    """Hollows a garment near an opening: `depth_field` > 0 is how far a point is inside the opening's zone (metres
    from the hem, counted only near it); what lies deeper than the cloth's thickness under the surface there is cut."""
    cavity = np.maximum(d + inner, -depth_field)
    return smax(d, -cavity, 0.0015)


def _along(a, b, t):
    return a + (b - a) * t


# --------------------------------------------------------------------------- trousers

def trousers(p, b: Build, ease=0.009, hem=0.075, waist=1.040, taper=0.0, cargo=False):
    """Trousers from the waistband to the shoe. taper: metres narrower at the hem (slim; negative: a wide leg);
    cargo: a bellows pocket on the outside of each thigh."""
    q = _mirror(p)
    z = q[..., 2]
    H, K, A = J["hip"], J["knee"], J["ankle"]
    seat = B.torso(q, b) - ease
    leg = B.leg(q, b) - ease
    drape = cone(q, _along(H, K, 0.55), (A[0], A[1] - 0.004, hem - 0.02), 0.070 * b.limbs, 0.068 - taper)   # hanging straight from the thigh
    d = smin(leg, drape, 0.03)
    d = smin(d, seat, 0.03)
    # folds: behind the knee, the break over the shoe, the pull lines from the crotch, the knee's bag in front
    f = 0.0
    for dz, w in ((0.030, 0.012), (-0.012, 0.013)):
        f += 0.0030 * ridge(q, K + (0.045, 0.055, dz), K + (-0.045, 0.055, dz - 0.004), w)
    for dz, amp in ((0.080, 0.0035), (0.038, 0.0045)):
        c = np.array((A[0], A[1] - 0.005, hem + dz))
        ring = gauss(z - c[2] - 0.012 * np.sin(3.0 * np.arctan2(q[..., 1] - c[1], q[..., 0] - c[0])), 0.013)
        f += amp * ring * win(z, hem, hem + 0.16, 0.02)
    for t0, t1 in ((0.05, 0.30), (0.12, 0.42), (0.20, 0.52)):
        a = _along(H, K, t0) + (-0.065, -0.050, 0.0)
        f += 0.0035 * ridge(q, a, _along(H, K, t1) + (0.075, -0.030, 0.0), 0.014)
    f += 0.003 * gauss(np.linalg.norm(q - (K + (0.0, -0.058, 0.0)), axis=-1), 0.05)
    f -= 0.0035 * gauss(q[..., 0], 0.007) * win(z, 0.80, 0.94, 0.02)                # the seam between the legs
    if cargo:
        pocket = win(z, 0.640, 0.790, 0.006) * win(q[..., 1], -0.050, 0.055, 0.006) * np.clip((q[..., 0] - H[0] - 0.02) / 0.02, 0, 1)
        flap = win(z, 0.755, 0.795, 0.004) * win(q[..., 1], -0.054, 0.059, 0.005) * np.clip((q[..., 0] - H[0] - 0.02) / 0.02, 0, 1)
        f += 0.011 * pocket + 0.004 * flap
    d = d - f
    d = d - 0.0035 * gauss(z - (waist - 0.022), 0.016)                            # the waistband
    d = smax(d, z - waist, 0.004)
    d = smax(d, hem - z, 0.003)                                                   # (closed under the hem: the shoe goes into it)
    return d


# --------------------------------------------------------------------------- tops

def _sleeve_folds(q, b, wrist_t=1.0):
    S = np.array((J["shoulder"][0] * b.shoulders, J["shoulder"][1], J["shoulder"][2]))
    E, W = J["elbow"], J["wrist"]
    across = np.array((0.0, 1.0, 0.0))
    f = 0.0
    # the crook of the elbow: three creases across the inside (the front) of the joint
    inner = np.array((-0.030, -0.032, 0.020))
    for t, w in ((-0.035, 0.010), (0.0, 0.011), (0.036, 0.010)):
        c = E + (S - E) / np.linalg.norm(S - E) * t + inner * 0.9
        f += 0.0045 * ridge(q, c + (0.02, 0.0, -0.028), c + (-0.020, 0.0, 0.028), w)
    # two drag lines from the shoulder's point down to the armpit, one spiral down the forearm
    f += 0.0030 * ridge(q, _along(S, E, 0.18) + (0.030, -0.045, 0.0), _along(S, E, 0.50) + (-0.045, -0.02, 0.0), 0.013)
    f += 0.0030 * ridge(q, _along(S, E, 0.20) + (0.030, 0.048, 0.0), _along(S, E, 0.55) + (-0.045, 0.02, 0.0), 0.013)
    f += 0.0030 * ridge(q, _along(E, W, 0.15) + (0.03, 0.03, 0.02), _along(E, W, 0.75 * wrist_t) + (-0.02, -0.03, 0.0), 0.012)
    return f


def _body_folds(q, amount=1.0):
    """The torso's few folds: bunched above the hem at the sides, pulled from the armpits, the spine's furrow."""
    x, y, z = q[..., 0], q[..., 1], q[..., 2]
    f = 0.0
    for zz, amp in ((1.075, 0.0030), (1.035, 0.0035)):
        f += amp * gauss(z - zz - 0.10 * (x - 0.12), 0.011) * win(x, 0.105, 0.3, 0.018)       # at the waist, towards the sides
    f += 0.0008 * ridge(q, (0.165, -0.040, 1.340), (0.105, -0.080, 1.250), 0.018)           # from the armpit towards the chest (faint: a deep one outlined a breast)
    f += 0.0035 * ridge(q, (0.165, 0.060, 1.330), (0.085, 0.100, 1.215), 0.018)             # … and across the back
    f -= 0.0035 * gauss(x, 0.016) * np.clip(y * 12, 0, 1) * win(z, 1.08, 1.40, 0.05)        # the spine
    return f * amount


def top(p, b: Build, ease=0.010, sleeve_ease=None, hem=0.99, straight=0.0, neck_z=1.462, neck_r=0.066, cuff_t=0.985,
        folds=1.0, sleeves=True, rib=0.0, v_neck=0.0, skirt=False):
    """A shirt, a jumper or a hoodie's body. hem: where it ends (z); straight: 0 follows the waist … 1 hangs straight
    from the chest; cuff_t: where the sleeve ends along the forearm (1 = the wrist, 0.45 = rolled to the forearm);
    rib: metres of ribbed band at the hem and the cuffs; v_neck: metres the neckline dips in front."""
    q = _mirror(p)
    x, y, z = q[..., 0], q[..., 1], q[..., 2]
    sleeve_ease = ease if sleeve_ease is None else sleeve_ease
    S = np.array((J["shoulder"][0] * b.shoulders, J["shoulder"][1], J["shoulder"][2]))
    E, W = J["elbow"], J["wrist"]
    # cloth does not cling: it spans from the chest's fullest line to the next (no muscle shows through a shirt), and a
    # sleeve is a tube round the arm, not a skin over the biceps
    under = replace(b, soft=1.0)
    d = B.torso(q, under)
    d = smin(d, ell(q, (0, -0.004 - 0.5 * b.bust, 1.285), (0.146 * b.chest, 0.100 * b.chest + 0.012 + 0.9 * b.bust, 0.16)), 0.045)
    d = d - ease
    if straight:
        column = ell(q, (0, 0.004 - 0.4 * b.belly, 1.13), ((0.150 * b.chest + ease) , (0.108 * b.chest + ease + 0.4 * b.belly), 0.30))
        d = smin(d, column + (1 - straight) * 0.02, 0.04)
    if skirt:                                                                    # a coat: a skirt hanging in a few flutes down to the hem
        tube = ell(q, (0, 0.006, hem + 0.02), (0.150 * b.hips + ease + 0.035, 0.105 * b.hips + ease + 0.03, 1.10 - hem))
        flutes = 0.006 * np.sin(5.0 * np.arctan2(y, x) + 2.0 * z) * np.clip((0.95 - z) / 0.3, 0, 1)
        d = smin(d, tube - flutes, 0.05)
    d = d - _body_folds(q, folds)
    if sleeves:
        end = _along(E, W, cuff_t)
        g = b.limbs
        tube = smin(cone(q, S, E, 0.050 * g, 0.044 * g), cone(q, E, W, 0.044 * g, 0.031 * g), 0.02)
        arm = smin(B.arm(q, under), tube, 0.02) - sleeve_ease
        arm = arm - _sleeve_folds(q, b, cuff_t) * folds
        axis = (W - E) / np.linalg.norm(W - E)
        along = (q - end) @ axis                                              # > 0 past the cuff
        if rib:
            arm = arm + 0.004 * gauss(-along - rib, 0.006) - 0.002 * win(-along, 0.0, rib - 0.008, 0.004)      # the ribbed cuff: gathered above it
        arm = smax(arm, along, 0.003)                                         # (closed at the cuff: the wrist goes into it)
        d = smin(d, arm, 0.02)
    else:
        hole = cone(q, S + (0.0, 0.0, -0.02), S + (0.25, 0.0, -0.02), 0.085, 0.085)     # armholes of a vest
        d = smax(d, -hole, 0.006)
    if rib:
        d = d + 0.005 * gauss(z - (hem + rib), 0.007) - 0.002 * win(z, hem, hem + rib - 0.008, 0.004)           # the ribbed hem
    d = smax(d, hem - z, 0.004)
    # the neckline: a hole round the neck, dipping in front
    neck_hole = cone(q, (0, 0.022, neck_z - 0.10), (0, 0.020 - 0.5 * v_neck, neck_z + 0.2), neck_r, neck_r)
    if v_neck:
        neck_hole = smin(neck_hole, cone(q, (0, -0.06, neck_z - 0.01), (0, -0.115, neck_z - v_neck), 0.03, 0.006), 0.02)
    d = smax(d, -smax(neck_hole, neck_z - 0.03 - v_neck - z, 0.01), 0.004)
    d = smax(d, z - (neck_z + 0.012), 0.004)
    return d


def hood(p):
    """A hood lying on the upper back, with the rim of its opening."""
    d = ell(p, (0, 0.100, 1.425), (0.118, 0.058, 0.100))
    d = smin(d, ell(p, (0, 0.074, 1.498), (0.098, 0.056, 0.042)), 0.03)                   # its rim rolled round the back of the neck
    d = smin(d, ell(np.abs(p) * (1, 0, 0) + p * (0, 1, 1), (0.070, 0.020, 1.500), (0.036, 0.040, 0.030)), 0.03)   # … and forward over the shoulders
    d = d - 0.005 * ridge(p, (-0.08, 0.150, 1.47), (0.08, 0.150, 1.47), 0.016) - 0.004 * ridge(p, (-0.07, 0.150, 1.40), (0.07, 0.150, 1.40), 0.016)
    return d


def collar(p, height=0.030, spread=0.045):
    """A shirt's collar: a stand round the neck and two leaves folded down over it, open in front."""
    q = _mirror(p)
    ring_c = np.array((0, 0.022, 1.488))
    r = np.hypot(q[..., 0], (q[..., 1] - ring_c[1]) * 1.04)
    band = np.maximum(np.abs(r - 0.064) - 0.0065, np.abs(q[..., 2] - (ring_c[2] - 0.011)) - (height * 0.5 + 0.011))   # down into the shirt's neck: no slot behind
    leaf = np.maximum(np.abs(r - 0.072) - 0.0055, np.abs(q[..., 2] - (ring_c[2] - 0.004 - 0.016 * np.clip(-q[..., 1] / 0.06, 0, 1))) - height * 0.62)
    d = smin(band, leaf, 0.006)
    half = 0.012 + spread * np.clip((1.49 - q[..., 2]) / 0.05, 0, 1)                    # the opening in front: a V widening downwards
    gap = np.maximum(np.maximum(q[..., 0] - half, np.abs(q[..., 1] + 0.075) - 0.04), np.abs(q[..., 2] - 1.47) - 0.06)
    return smax(d, -gap, 0.004)


def vest(p, b: Build, ease=0.013, hem=1.013):
    """A sleeveless utility vest over the shirt: armholes, a deep V, a zip's two edges. (Its neckline and hem end at
    other heights than the shirt's and the belt's: two caps in one plane would flicker.)"""
    d = top(p, b, ease=ease, hem=hem, straight=0.45, neck_z=1.470, neck_r=0.076, folds=0.4, sleeves=False, v_neck=0.16)
    q = _mirror(p)
    front = np.clip(-q[..., 1] * 20, 0, 1)
    d = d - 0.003 * gauss(q[..., 0] - 0.012, 0.006) * front * win(q[..., 2], hem, 1.30, 0.01)   # the zip's edge
    for z0, w, h in ((1.275, 0.040, 0.040), (1.100, 0.048, 0.050)):                        # bellows pockets: chest and hip
        d = d - 0.008 * win(q[..., 0], 0.088 - w, 0.088 + w, 0.005) * win(q[..., 2], z0 - h, z0 + h, 0.005) * front
        d = d - 0.003 * win(q[..., 0], 0.088 - w - 0.003, 0.088 + w + 0.003, 0.004) * win(q[..., 2], z0 + h * 0.4, z0 + h + 0.004, 0.004) * front
    return d


def scarf(p):
    """A scarf wound twice round the neck, one end hanging down the chest."""
    c = np.array((0, 0.020, 1.487))
    r = np.hypot(p[..., 0], p[..., 1] - c[1])
    a = np.arctan2(p[..., 1] - c[1], p[..., 0])
    lower = np.hypot(r - 0.074, p[..., 2] - (c[2] - 0.010 + 0.006 * np.sin(a))) - 0.026
    upper = np.hypot(r - 0.068, p[..., 2] - (c[2] + 0.026 + 0.006 * np.sin(a + 1.3))) - 0.022
    d = smin(lower, upper, 0.008)
    return d                                                                         # (its hanging end lies on the garment: human.py; the knit is painted)


# --------------------------------------------------------------------------- hands

# a finger: its name, the knuckle in the hand's frame (metres along the hand, towards the thumb), how it fans out from
# the middle finger (radians, towards the thumb), its length, its radius, how much it is curled at rest
FINGERS = (("index", 0.091, 0.029, 0.085, 0.072, 0.0095, 0.50), ("middle", 0.095, 0.009, 0.015, 0.080, 0.0098, 0.60),
           ("ring", 0.091, -0.011, -0.065, 0.074, 0.0093, 0.72), ("little", 0.083, -0.029, -0.165, 0.058, 0.0083, 0.85))
PHALANGES = (0.44, 0.30, 0.26)
# the thumb: the joint at the wrist's side, the knuckle, the last joint, the tip (hand's frame) and the radii there
THUMB = ((0.020, 0.030, 0.006), (0.052, 0.051, 0.015), (0.082, 0.060, 0.026), (0.105, 0.063, 0.036))
THUMB_R = (0.0128, 0.0108, 0.0096, 0.0080)


def hand_chains_local():
    """Every finger as four points in the hand's frame (along, to the thumb, out of the palm): knuckle, two joints, tip.
    {"thumb" | "index" | "middle" | "ring" | "little": [4 points]} — the skeleton's finger bones lie on them."""
    out = {"thumb": [np.array(q, float) for q in THUMB]}
    for name, a, t, fan, ln, _, c in FINGERS:
        pts = [np.array((a, t, -0.001))]
        bend = 0.20 * c
        for share in PHALANGES:
            flat = math.cos(bend) * ln * share
            pts.append(pts[-1] + np.array((math.cos(fan) * flat, math.sin(fan) * flat, math.sin(bend) * ln * share)))
            bend += 0.55 * c
        out[name] = pts
    return out


def hand_chains():
    """The left hand's fingers in the world (the rest pose): {finger: [4 points]}."""
    W, D, N, T = J["wrist"], B.HAND_D, B.HAND_N, B.HAND_T
    return {k: [W + q[0] * D + q[1] * T + q[2] * N for q in pts] for k, pts in hand_chains_local().items()}


def hand_frame(p):
    """Points of the world in the left hand's frame: along the hand, to the thumb, out of the palm."""
    W, D, N, T = J["wrist"], B.HAND_D, B.HAND_N, B.HAND_T
    return np.stack([(p - W) @ D, (p - W) @ T, (p - W) @ N], -1)


def hand(p, female=False, curl=1.0):
    """The left hand hanging loose: the wrist, a palm broad at the knuckles and narrow at the wrist with the two pads
    of its heel, four fingers of their own lengths fanning from an arc of knuckles, half curled, each with its joints,
    and the thumb turned in over the palm. A woman's hand is finer (the joints are where a man's are: one skeleton)."""
    lq = hand_frame(p)
    g = 0.88 if female else 1.0                                           # the girth of fingers and palm

    def at(a, t, n):
        return np.array((a, t, n), float)
    # the palm: a slab that narrows to the wrist
    wq = lq.copy()
    wq[..., 1] = lq[..., 1] / (np.clip(0.74 + 0.26 * lq[..., 0] / 0.085, 0.74, 1.02) * (0.94 if female else 1.0))
    d = box(wq, at(0.050, 0.000, 0.000), (0.036, 0.030, 0.0105 * g), 0.0104)
    d = smin(d, ell(lq, at(0.050, 0.000, -0.004), (0.046, 0.034 * g, 0.0105 * g)), 0.012)                       # the back of the hand: a little domed
    d = smin(d, cone(lq, at(-0.034, 0.0, 0.0), at(0.008, 0.0, 0.0), 0.0255 * g, 0.0245 * g, flat=(2, 0.70)), 0.014)   # the wrist
    d = smin(d, ell(lq, at(0.030, 0.024, 0.009), (0.029, 0.017, 0.0125 * g)), 0.010)                            # the ball of the thumb
    d = smin(d, ell(lq, at(0.034, -0.027, 0.007), (0.031, 0.011, 0.0105 * g)), 0.010)                           # the heel of the hand
    d = smin(d, ell(lq, at(0.081, 0.000, 0.005), (0.013, 0.033, 0.0062 * g)), 0.012)                            # the pads under the fingers
    chains = hand_chains_local()
    fingers = None
    for name, _, _, _, _, r, _ in FINGERS:
        pts = chains[name]
        r = r * g
        radii = (r, r * 0.94, r * 0.87, r * 0.76)
        f = None
        for i in range(3):
            seg = cone(lq, pts[i], pts[i + 1], radii[i], radii[i + 1])
            f = seg if f is None else smin(f, seg, 0.004)
        for i in (1, 2):
            f = smin(f, ell(lq, pts[i] + at(0, 0, -0.0006), (radii[i] * 0.93,) * 3), 0.004)                       # the joints: a hint of knuckle on the back (fuller, they read as beads on a string)
        f = smin(f, ell(lq, pts[0] + at(-0.002, 0.0, -0.0045), (0.0115, 0.0098 * g, 0.0080)), 0.005)             # the knuckle
        fingers = f if fingers is None else np.minimum(fingers, f)       # (fingers do not melt into each other)
    d = smin(d, fingers, 0.0045)
    th = chains["thumb"]
    t_ = None
    for i in range(3):
        seg = cone(lq, th[i], th[i + 1], THUMB_R[i] * g, THUMB_R[i + 1] * g)
        t_ = seg if t_ is None else smin(t_, seg, 0.003)
    t_ = smin(t_, ell(lq, th[2] + at(0, 0.001, 0), (THUMB_R[2] * g * 1.04,) * 3), 0.003)
    d = smin(d, t_, 0.007)
    d = smin(d, ell(lq, at(0.058, 0.033, 0.007), (0.018, 0.012, 0.0055)), 0.008)                                 # the web between thumb and palm
    return d


def forearm_skin(p, b: Build, hand_female=False, to=0.42):
    """The hand with the forearm up towards the elbow (rolled sleeves show it)."""
    q = np.array(p, float)
    d = hand(q, hand_female)
    E, W = J["elbow"], J["wrist"]
    arm = B.arm(q, b)
    axis = (W - E) / np.linalg.norm(W - E)
    arm = smax(arm, -((q - _along(E, W, to)) @ axis), 0.003)
    return smin(d, arm, 0.012)


# --------------------------------------------------------------------------- shoes

def shoe(p, kind="sneaker"):
    """The left shoe, built on a last: along the foot from the heel to the toe every cross-section has its own breadth
    and height (narrow heel, broad ball, a low rounded toe that lifts off the floor), the upper standing on a sole of
    the same outline a few millimetres wider. Kinds: sneaker (a thick sole, a padded collar, laces), boot (a shaft
    over the ankle, a tread), flat (a low city shoe)."""
    x0 = 0.103
    sole_h = {"sneaker": 0.026, "boot": 0.030, "flat": 0.016}[kind]
    top_z = {"sneaker": 0.125, "boot": 0.19, "flat": 0.085}[kind]
    low = 0.84 if kind == "flat" else 1.0
    q = np.array(p, float)
    x, y, z = q[..., 0], q[..., 1], q[..., 2]
    back, length = 0.085, 0.282                                  # the heel's back (y) and the shoe's length towards -y
    t = np.clip((back - y) / length, 0.0, 1.0)                   # 0 at the heel … 1 at the toe's tip
    w = np.interp(t, [0.0, 0.05, 0.30, 0.68, 0.90, 1.0], [0.012, 0.029, 0.036, 0.047, 0.040, 0.010])        # half the breadth
    h = np.interp(t, [0.0, 0.10, 0.30, 0.50, 0.70, 0.92, 1.0], [0.070, 0.105, 0.105, 0.062, 0.042, 0.030, 0.016]) * low
    xc = x0 + 0.005 * t                                          # the foot points a little outward
    spring = 0.011 * np.clip((t - 0.76) / 0.24, 0.0, 1.0) ** 2   # the toe lifts off the floor
    zz = z - sole_h - spring                                     # the height over the sole
    ends = np.maximum(y - back, (back - length) - y)             # past the heel or the toe
    r = np.sqrt(((x - xc) / w) ** 2 + (np.maximum(zz, 0.0) / h) ** 2)
    d = smax((r - 1.0) * np.minimum(w, h) * 0.9, ends, 0.030)
    d = smin(d, cone(q, (x0, 0.022, 0.07), (x0, 0.020, top_z), 0.042, 0.044), 0.02)             # the collar round the ankle
    d = smax(d, z - top_z, 0.006)
    if kind != "flat":
        d = d - 0.004 * gauss(z - (top_z - 0.012), 0.012) * win(y, -0.03, 0.2, 0.02)             # the padded rim
        d = d - 0.0040 * ridge(q, (x0 + 0.002, -0.040, 0.100), (x0 + 0.003, -0.122, 0.064), 0.017, taper=False)  # the tongue under the laces
        for s in (0.15, 0.38, 0.62, 0.86):
            c = np.array((x0 + 0.003, -0.042 - 0.082 * s, 0.103 - 0.038 * s))
            d = d - 0.0026 * ridge(q, c + (-0.023, 0, -0.004), c + (0.023, 0, -0.004), 0.004)    # the laces
        d = d + 0.0014 * gauss(t - 0.80, 0.007) * np.clip(zz / 0.006, 0, 1)                      # the toe cap's seam
        d = d - 0.0016 * win(t, -0.1, 0.17, 0.02) * win(zz, 0.004, 0.060, 0.008)                 # the heel's stiff counter
    else:
        d = d + 0.0012 * gauss(t - 0.78, 0.006) * np.clip(zz / 0.006, 0, 1)                      # a city shoe's toe seam
    # the sole: the same outline a little wider, lifting with the toe; a groove where the upper meets it
    out = smax(np.abs(x - xc) - (w + 0.0032), ends - 0.003, 0.030)
    sole = smax(out, np.maximum(spring - z, z - (sole_h + spring)), 0.004)
    d = smax(d, (sole_h * 0.6 + spring) - z, 0.003)
    d = smin(d, sole, 0.003)
    d = d + 0.0018 * gauss(zz - 0.002, 0.0025)
    if kind == "sneaker":
        d = d + 0.0010 * gauss(zz + 0.013, 0.002) * (t > 0.03)                                    # a line round the midsole
    if kind == "boot":
        d = d - 0.002 * (np.sin(y * 260) > 0.3) * win(z - spring, 0.0, sole_h * 0.7, 0.002)      # the tread's teeth
    if kind != "sneaker":
        d = smin(d, smax(smax(np.abs(x - xc) - (w + 0.0032), np.maximum(y - back - 0.003, (back - 0.062) - y), 0.012),
                         np.maximum(-z, z - (sole_h + 0.008)), 0.003), 0.003) if kind == "flat" else d   # a city shoe's heel
    d = smax(d, -z, 0.001)                                                                        # flat on the floor
    return d
