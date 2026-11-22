"""The body under the clothes: joints (the skeleton's contract), a build, and the anatomy as a distance field.

Blender: x to the figure's left, -y its front, z up; metres. The rest pose is an A-pose (arms 38 degrees off the
body): an arm does not touch the ribs, so the shoulder, the armpit and a sleeve are sculpted and skinned cleanly and
bend well both down to the hips and up to a ladder's rung.
"""
import math
from dataclasses import dataclass

import numpy as np

from . import sdf
from .sdf import cone, ell, smin


def _v(*a):
    return np.array(a, float)


# --------------------------------------------------------------------------- joints

ARM_OUT = math.radians(38)
_ARM = _v(math.sin(ARM_OUT), 0.0, -math.cos(ARM_OUT))                  # down the upper arm
_FORE = _v(0.600, -0.130, -0.789) / np.linalg.norm(_v(0.600, -0.130, -0.789))   # the forearm hangs a little forward

J = {
    "hips": _v(0, 0.005, 0.955), "spine": _v(0, 0.005, 1.07), "chest": _v(0, 0.01, 1.24), "neck": _v(0, 0.025, 1.475),
    "head": _v(0, 0.012, 1.565), "crown": _v(0, 0.0, 1.79),
    "clavicle": _v(0.03, 0.02, 1.450), "shoulder": _v(0.165, 0.02, 1.440),
    "hip": _v(0.092, 0.005, 0.925), "knee": _v(0.098, -0.01, 0.50), "ankle": _v(0.10, 0.02, 0.085),
    "ball": _v(0.105, -0.105, 0.02), "toe": _v(0.108, -0.185, 0.02),
}
J["elbow"] = J["shoulder"] + _ARM * 0.285 + _v(0, 0.012, 0)
J["wrist"] = J["elbow"] + _FORE * 0.255
J["knuckle"] = J["wrist"] + _FORE * 0.095
J["fingertip"] = J["knuckle"] + _FORE * 0.09

# the hand's frame: along the fingers, the palm's normal (towards the thigh), the thumb's side (to the front)
HAND_D = _FORE
HAND_N = _v(-1.0, 0.0, 0.0) - HAND_D * (_v(-1.0, 0.0, 0.0) @ HAND_D)    # the palm faces the body (-x for the left hand)
HAND_N /= np.linalg.norm(HAND_N)
HAND_T = np.cross(HAND_N, HAND_D)                                       # the thumb's side: the front
if HAND_T[1] > 0:
    HAND_T = -HAND_T


def side(p, s):
    """A joint of the left side on side s (+1 left, -1 right)."""
    return _v(p[0] * s, p[1], p[2])


@dataclass
class Build:
    """How one person differs from the next: breadths and girths as multipliers (the joints stay where they are, so
    every figure plays the same takes)."""
    shoulders: float = 1.0      # the breadth of the shoulder girdle and the deltoids
    chest: float = 1.0          # the rib cage
    waist: float = 1.0          # the waist's girth
    belly: float = 0.0          # metres of belly in front
    hips: float = 1.0           # pelvis and seat
    limbs: float = 1.0          # the girth of arms and legs
    neck: float = 1.0
    bust: float = 0.0           # metres of bust (a woman's chest)
    soft: float = 0.0           # 0 lean and defined … 1 soft: muscles melt into each other


# --------------------------------------------------------------------------- anatomy

def _along(a, b, t, off=(0, 0, 0)):
    return a + (b - a) * t + _v(*off)


def _limb_rot(a, b):
    """Columns: two axes across a limb and the axis along it (for ellipsoids lying along the limb)."""
    z = (b - a) / np.linalg.norm(b - a)
    x = np.cross(_v(0, 1, 0), z)
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    return np.stack([x, y, z], 1)


def torso(p, b: Build):
    """Pelvis to the base of the neck, with the shoulder girdle. p is mirrored (x >= 0)."""
    k = 0.035 + 0.02 * b.soft
    d = ell(p, (0, 0.012, 0.965), (0.150 * b.hips, 0.098 * b.hips, 0.108))                    # the pelvis
    d = smin(d, ell(p, (0.074, 0.052, 0.930), (0.076 * b.hips, 0.070 * b.hips, 0.088)), k + 0.01)     # the seat
    d = smin(d, ell(p, (0, -0.004 - 0.6 * b.belly, 1.09), (0.138 * b.waist, 0.092 * b.waist + 0.5 * b.belly, 0.130)), k + 0.015)   # the abdomen
    d = smin(d, ell(p, (0, 0.002, 1.275), (0.150 * b.chest, 0.106 * b.chest, 0.165)), k)        # the rib cage
    d = smin(d, ell(p, (0.076, -0.056 * b.chest, 1.318), (0.080, 0.024, 0.054)), 0.05)           # the chest muscles
    if b.bust > 0:
        d = smin(d, ell(p, (0.068, -0.060 * b.chest - 0.5 * b.bust, 1.292), (0.058, 0.022 + b.bust, 0.058)), 0.06)
    d = smin(d, ell(p, (0.085, 0.070, 1.335), (0.070, 0.024, 0.090)), 0.05)                    # the shoulder blades
    d = smin(d, ell(p, (0.128 * b.chest, 0.030, 1.235), (0.034, 0.058, 0.110)), 0.05)           # the lats
    sh = _v(J["shoulder"][0] * b.shoulders, J["shoulder"][1], J["shoulder"][2])
    d = smin(d, cone(p, (0.0, 0.034, 1.516), (sh[0] - 0.03, 0.026, 1.446), 0.040, 0.028), 0.05)  # the trapezius down to the shoulder
    d = smin(d, cone(p, (0.02, -0.012, 1.462), (sh[0] - 0.035, 0.002, 1.454), 0.018, 0.018), 0.03)  # the collar bone
    return d


def arm(p, b: Build, hand=True):
    """The left arm from the deltoid to the wrist (p is mirrored: x >= 0)."""
    S = _v(J["shoulder"][0] * b.shoulders, J["shoulder"][1], J["shoulder"][2])
    E, W = J["elbow"], J["wrist"]
    g = b.limbs
    k = 0.02 + 0.015 * b.soft
    up, fo = _limb_rot(S, E), _limb_rot(E, W)
    d = cone(p, S, E, 0.045 * g, 0.037 * g)
    d = smin(d, ell(p, _along(S, E, 0.10, (0.004, 0.0, 0.002)), (0.045 * b.shoulders * g, 0.050 * g, 0.068), up), 0.035)   # the deltoid
    d = smin(d, ell(p, _along(S, E, 0.52, (0.0, -0.016, 0.0)), (0.033 * g, 0.034 * g, 0.090), up), k + 0.01)       # the biceps
    d = smin(d, ell(p, _along(S, E, 0.50, (0.0, 0.020, 0.0)), (0.034 * g, 0.032 * g, 0.100), up), k + 0.01)        # the triceps
    d = smin(d, ell(p, E + _v(0.0, 0.010, 0.0), (0.040 * g, 0.043 * g, 0.045), up), k)                      # the elbow
    d = smin(d, cone(p, E, _along(E, W, 0.45), 0.043 * g, 0.037 * g), k)
    d = smin(d, ell(p, _along(E, W, 0.22, (0.006, -0.004, 0.0)), (0.046 * g, 0.042 * g, 0.085), fo), k)      # the forearm's belly
    d = smin(d, cone(p, _along(E, W, 0.45), W, 0.037 * g, 0.0265), k)
    return d


def leg(p, b: Build):
    """The left leg from the hip to the ankle (p is mirrored: x >= 0)."""
    H, K, A = J["hip"], J["knee"], J["ankle"]
    g = b.limbs
    k = 0.03 + 0.02 * b.soft
    th, sh = _limb_rot(H, K), _limb_rot(K, A)
    d = cone(p, H, K, 0.086 * g * (0.5 + 0.5 * b.hips), 0.054 * g)
    d = smin(d, ell(p, _along(H, K, 0.40, (0.004, -0.022, 0.0)), (0.066 * g, 0.060 * g, 0.185), th), k)     # the quadriceps
    d = smin(d, ell(p, _along(H, K, 0.45, (0.0, 0.030, 0.0)), (0.060 * g, 0.050 * g, 0.170), th), k)        # the hamstrings
    d = smin(d, ell(p, _along(H, K, 0.20, (-0.032, 0.004, 0.0)), (0.050 * g, 0.060 * g, 0.125), th), k)     # the adductors
    d = smin(d, ell(p, K + _v(0.0, -0.012, 0.004), (0.046 * g, 0.046 * g, 0.050)), 0.035)                   # the knee
    d = smin(d, cone(p, K, A, 0.051 * g, 0.037), 0.025)
    d = smin(d, ell(p, _along(K, A, 0.30, (0.004, 0.030, 0.0)), (0.050 * g, 0.050 * g, 0.115), sh), 0.03)    # the calf
    d = smin(d, ell(p, _along(K, A, 0.42, (0.0, -0.022, 0.0)), (0.030, 0.022, 0.150), sh), 0.02)            # the shin bone
    return d


def neck(p, b: Build):
    d = cone(p, (0, 0.032, 1.440), (0, 0.014, 1.610), 0.064 * b.neck, 0.050 * b.neck)       # wider at its base, narrower under the jaw
    d = smin(d, cone(p, (0.030, 0.000, 1.468), (0.048, 0.030, 1.565), 0.012, 0.014), 0.03)      # the muscle from the collar bone to behind the ear
    if b.soft < 0.4:
        d = smin(d, ell(p, (0, -0.030, 1.516), (0.011, 0.010, 0.016)), 0.025)                      # the larynx (a man's)
    return d


def body(p, b: Build):
    """The whole body without the head, the hands and the feet (symmetric)."""
    q = np.array(p, float)
    q[..., 0] = np.abs(q[..., 0])
    d = torso(q, b)
    d = smin(d, arm(q, b), 0.035)
    d = smin(d, leg(q, b), 0.045)
    d = smin(d, neck(q, b), 0.04)
    return d
