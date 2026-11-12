"""The players — IT people of four departments — modelled for every visual style.

Run:  blender.exe -b --factory-startup --python Tools/Blender/players.py -- Assets/_Game/Art/Models [S16_Derelict ...]

Writes Art/Models/Styles/Players/player__<StyleAsset>__<department>.fbx (11 styles × 4 departments). Every model keeps
the parts of the original player so the game swaps them in by name and the colours fall into place (PlayerLooks):
  Body        clothes, limbs, things held in the hands  — the department's colour
  Accent      the TEAM colour, kept small: the backpack, an armband, and the lanyard (QA) or the scarf (designer)
  AccentHead  the face (skin)                           — turns with the look direction (head pivot at the neck, 1.3 m)
  EyeGlasses  dark details on the head: hair, glasses, headphones, beret, visor — turns with the head too
Departments (PlayerLooks.Department): 0 backend developer (hoodie, big headphones, a laptop), 1 QA engineer (lanyard
badge, clipboard, big glasses), 2 sysadmin (vest with pockets, coil of cable, head torch), 3 designer (scarf, beret,
tablet and stylus). The build of the body is the style's: office proportions, toy, chunky, lanky, hazmat suit, diving
suit, crystal… The look direction of a teammate reads from the face side (-Y in Blender, +Z in Unity).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import figure as G  # noqa: E402  (primitives and the per-family finishing)
from figure import box, cyl, sph, capsule, limb, merge  # noqa: E402

import bpy  # noqa: E402
import voxel  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models")
OUT = os.path.join(ROOT, "Styles", "Players")
DEPARTMENTS = ["backend", "qa", "sysadmin", "designer"]


class Plan:
    """The build of a body: where the hips, chest, shoulders and head are, how thick the limbs."""

    def __init__(self, hip=0.9, leg_r=0.075, torso=(0.44, 0.26), top=1.42, shoulder_x=0.27, arm_r=0.06, arm_len=0.62,
                 head_r=0.13, head_z=1.6, hand_r=0.055, foot=(0.11, 0.27, 0.08), seg=12, square=False, head="sphere",
                 helmet=None, taper=1.0, head_scale=1.1, hat=None, mask=False, visor=False):
        self.hip, self.leg_r, self.torso, self.top = hip, leg_r, torso, top
        self.shoulder_x, self.arm_r, self.arm_len = shoulder_x, max(arm_r, leg_r * 0.65), arm_len
        # a game character reads by its head: a little bigger than life (a real adult keeps head_scale=1)
        self.head_r, self.head_z, self.hand_r = head_r * head_scale, head_z, max(hand_r, self.arm_r * 1.25)
        self.foot = (foot[0] * 1.15, foot[1] * 1.15, foot[2] * 1.1)  # shoes, not slippers
        self.seg, self.square, self.head, self.helmet, self.taper = seg, square, head, helmet, taper
        self.hat, self.mask, self.visor = hat, mask, visor  # style headwear: fedora / balaclava / visor band

    @property
    def chest_y(self):  # the front of the chest
        return -self.torso[1] / 2

    def hand(self, s):
        return (s * (self.shoulder_x + 0.04), -0.06, self.top - 0.06 - self.arm_len)


def body(p):
    """Torso, arms, neck: the Body part. A chest wider than the waist, hips with some mass, shoulder caps that read
    from above, arms bent a little at the elbow — not a board with rods."""
    w, d = p.torso
    h = p.top - p.hip
    chest_h, waist_h = h * 0.56, h * 0.5
    parts = [box(w, d * 1.04, chest_h, z=p.top - chest_h / 2, taper=p.taper * 1.06),           # chest, widening up
             box(w * 0.82, d * 0.9, waist_h, z=p.hip + waist_h / 2 + 0.02),                       # waist
             box(w * 0.94, d * 0.98, 0.17, z=p.hip + 0.04)]                                       # hips
    for s in (-1, 1):
        sh = (s * p.shoulder_x, 0, p.top - 0.06)
        hx, hy, hz = p.hand(s)
        elbow = (s * (p.shoulder_x + 0.03), 0.03, (sh[2] + hz) / 2 + 0.02)
        parts += [limb(sh, elbow, p.arm_r * 1.1, seg=p.seg, square=p.square),                     # sleeve, wider at the top
                  limb(elbow, (hx, hy, hz), p.arm_r * 0.9, seg=p.seg, square=p.square)]
    parts.append(cyl(0.065 if not p.helmet else 0.1, 0.14, z=p.top + 0.04, seg=p.seg))
    return parts


def legs(p):
    """Thigh, knee, shin, shoe with a toe cap: the knee forward a little, so the leg is not a post."""
    out = []
    ankle_z = p.foot[2] * 0.8
    knee_z = (p.hip + ankle_z) / 2
    for s in (-1, 1):
        x = s * 0.11
        knee = (x, -0.025, knee_z)
        out += [limb((x, 0, p.hip + 0.03), knee, p.leg_r * 1.12, seg=p.seg, square=p.square),
                limb(knee, (x, -0.005, ankle_z), p.leg_r * 0.9, seg=p.seg, square=p.square),
                box(p.foot[0], p.foot[1], p.foot[2], x=x, y=-0.05, z=p.foot[2] / 2),               # shoe
                box(p.foot[0] * 0.96, p.foot[1] * 0.3, p.foot[2] * 0.7, x=x, y=-0.05 - p.foot[1] * 0.38,
                    z=p.foot[2] * 0.35)]                                                           # toe of the shoe
    return out


def hands(p):
    """Mitten hands (a palm and a thumb), not balls: a ball reads as a robot joint."""
    out = []
    for s in (-1, 1):
        hx, hy, hz = p.hand(s)
        r = p.hand_r
        out += [box(r * 1.1, r * 1.6, r * 2.0, x=hx, y=hy, z=hz - r * 0.3),
                box(r * 0.55, r * 0.6, r * 0.9, x=hx - s * r * 0.55, y=hy - r * 0.45, z=hz - r * 0.1)]
    return out


def face(p):
    if p.head == "box":
        return [box(p.head_r * 1.7, p.head_r * 1.8, p.head_r * 2.1, z=p.head_z)]
    return [sph(p.head_r, z=p.head_z, seg=max(12, p.seg), sz=1.12, ico=p.head == "ico")]


def backpack(p, big=1.0):
    """The team colour's main carrier: wide and deep enough to read from above and behind at 20 m."""
    w, d = p.torso
    big = max(big, 0.9)
    h = (p.top - p.hip) * 0.72 * big
    return [box(w * 0.86 * big, 0.2 * big, h, y=d / 2 + 0.09 * big, z=p.top - h / 2 - 0.03),
            box(w * 0.7 * big, 0.06, h * 0.45, y=d / 2 + 0.2 * big, z=p.top - h * 0.62 - 0.03)]       # front pocket


def helmet(p):
    """Suits: a helmet in the Body part (it does not need to turn), the face behind a dark visor (EyeGlasses)."""
    r = p.head_r * 1.55
    if p.helmet == "hazmat":
        shell = [sph(r, z=p.head_z, seg=16, sy=1.05, sz=1.05), cyl(r * 0.95, 0.06, z=p.head_z - r * 0.75, seg=16)]
        visor = [box(r * 1.4, 0.04, r * 0.8, y=-r * 0.86, z=p.head_z + 0.02)]
    else:  # diving helmet: a round glass porthole and a ring of bolts
        shell = [sph(r, z=p.head_z, seg=20), cyl(r * 1.08, 0.08, z=p.head_z - r * 0.82, seg=20)]
        shell += [cyl(r * 0.62, 0.05, y=-r * 0.9, z=p.head_z, axis="Y", seg=20)]
        visor = [cyl(r * 0.5, 0.03, y=-r * 0.95, z=p.head_z, axis="Y", seg=20)]
    return shell, visor


# --------------------------------------------------------------------------- departments

def department(p, dept):
    """(Accent, EyeGlasses, extra Body) of a department on a body plan."""
    w, d = p.torso
    h = p.top - p.hip
    head_top = p.head_z + p.head_r * 1.1
    side = p.head_r * 1.02
    accent, dark, extra = [], [], []
    # the team armband on the left upper arm
    sx, _, sz = -p.shoulder_x - 0.01, 0, p.top - 0.2
    accent += [cyl(p.arm_r * 1.35, 0.07, x=sx, z=sz, seg=max(8, p.seg))]
    if dept == 0:  # backend developer: hoodie with a hood and a pocket, over-ear headphones, a laptop under the arm
        extra += [box(w + 0.05, d + 0.05, h * 0.92, z=p.hip + h * 0.5, taper=p.taper),
                  sph(p.head_r * 1.05, y=d * 0.45, z=p.top + 0.05, seg=12, sy=0.7, sz=0.6),          # hood on the back
                  box(w * 0.6, 0.03, h * 0.25, y=-(d / 2 + 0.03), z=p.hip + h * 0.3),                # pocket
                  limb((-0.04, -d / 2 - 0.03, p.top - 0.02), (-0.05, -d / 2 - 0.035, p.top - 0.2), 0.008),
                  limb((0.04, -d / 2 - 0.03, p.top - 0.02), (0.05, -d / 2 - 0.035, p.top - 0.2), 0.008)]   # drawstrings
        accent += backpack(p, 0.8)
        if not p.helmet and not p.visor:
            dark += [cyl(p.head_r * 0.45, 0.07, x=-side, z=p.head_z, axis="X", seg=14), cyl(p.head_r * 0.45, 0.07, x=side, z=p.head_z, axis="X", seg=14),
                     limb((-side, 0, p.head_z + 0.04), (0, 0, head_top + 0.02), 0.018), limb((side, 0, p.head_z + 0.04), (0, 0, head_top + 0.02), 0.018),
                     sph(p.head_r * 1.02, y=0.02, z=p.head_z + p.head_r * 0.25, seg=12, sy=0.95, sz=0.75)]   # hair
        hx, hy, hz = p.hand(-1)
        extra += [box(0.05, 0.34, 0.24, x=hx - 0.02, y=hy, z=hz + 0.2)]                                 # laptop
    elif dept == 1:  # QA: a lanyard with a badge, a clipboard, big square glasses
        accent += [limb((-w * 0.25, -d / 2 - 0.01, p.top), (0, -d / 2 - 0.02, p.top - h * 0.45), 0.012),
                   limb((w * 0.25, -d / 2 - 0.01, p.top), (0, -d / 2 - 0.02, p.top - h * 0.45), 0.012),
                   box(0.09, 0.015, 0.12, y=-d / 2 - 0.025, z=p.top - h * 0.52)]                       # badge
        extra += [box(w + 0.03, d + 0.03, 0.08, z=p.top - 0.02)]                                      # shirt collar
        accent += backpack(p, 0.7)
        hx, hy, hz = p.hand(1)
        extra += [box(0.22, 0.03, 0.3, x=hx, y=hy - 0.08, z=hz + 0.14, rx=-0.4), box(0.08, 0.035, 0.03, x=hx, y=hy - 0.12, z=hz + 0.29)]
        if not p.helmet:
            dark += [box(p.head_r * 0.7, 0.03, p.head_r * 0.45, x=-p.head_r * 0.42, y=-p.head_r * 0.98, z=p.head_z + 0.01),
                     box(p.head_r * 0.7, 0.03, p.head_r * 0.45, x=p.head_r * 0.42, y=-p.head_r * 0.98, z=p.head_z + 0.01),
                     sph(p.head_r * 1.03, y=0.03, z=p.head_z + p.head_r * 0.3, seg=12, sy=0.95, sz=0.7)]   # hair
    elif dept == 2:  # sysadmin: a vest with pockets, a coil of cable over the shoulder, a head torch
        extra += [box(w + 0.06, d + 0.06, h * 0.75, z=p.hip + h * 0.55, taper=p.taper),
                  box(0.12, 0.04, 0.12, x=-w * 0.25, y=-(d / 2 + 0.05), z=p.hip + h * 0.4),
                  box(0.12, 0.04, 0.12, x=w * 0.25, y=-(d / 2 + 0.05), z=p.hip + h * 0.4),
                  box(0.1, 0.04, 0.1, x=-w * 0.25, y=-(d / 2 + 0.05), z=p.hip + h * 0.75)]
        accent += backpack(p, 1.05)
        coil_c = (w * 0.15, 0, p.top - h * 0.25)
        for k in range(12):  # a ring of cable diagonally over the chest
            a0, a1 = 2 * math.pi * k / 12, 2 * math.pi * (k + 1) / 12
            r = max(w, d) * 0.62
            pa = (coil_c[0] + r * math.cos(a0) * 0.75, coil_c[1] + r * math.sin(a0) * 0.62, coil_c[2] + r * math.cos(a0) * 0.6)
            pb = (coil_c[0] + r * math.cos(a1) * 0.75, coil_c[1] + r * math.sin(a1) * 0.62, coil_c[2] + r * math.cos(a1) * 0.6)
            extra.append(limb(pa, pb, 0.022))
        if not p.helmet:
            dark += [cyl(p.head_r * 1.04, 0.035, z=p.head_z + p.head_r * 0.35, seg=16),                  # torch strap
                     cyl(0.035, 0.05, y=-p.head_r * 1.05, z=p.head_z + p.head_r * 0.35, axis="Y", seg=10),
                     box(p.head_r * 1.2, p.head_r * 0.6, p.head_r * 0.5, y=-p.head_r * 0.55, z=p.head_z - p.head_r * 0.75)]  # beard
    else:  # designer: a long scarf, a beret, a tablet with a stylus
        accent += [cyl(max(w, d) * 0.42, 0.08, z=p.top + 0.02, seg=14),
                   box(0.09, 0.03, h * 0.55, x=w * 0.18, y=-d / 2 - 0.04, z=p.top - h * 0.28)]          # scarf end
        accent += backpack(p, 0.6)
        hx, hy, hz = p.hand(-1)
        extra += [box(0.26, 0.02, 0.19, x=hx + 0.08, y=hy - 0.1, z=hz + 0.16, rx=-0.5), limb((hx + 0.25, hy - 0.08, hz), (hx + 0.27, hy - 0.1, hz + 0.14), 0.011)]  # stylus: one 2 cm grid cell thick
        if not p.helmet:
            if not p.hat:
                dark += [cyl(p.head_r * 1.15, 0.06, x=0.03, y=0.02, z=head_top - 0.02, seg=16, rx=0.15)]       # beret
            dark += [cyl(p.head_r * 0.28, 0.02, x=-p.head_r * 0.38, y=-p.head_r * 0.97, z=p.head_z + 0.01, axis="Y", seg=14),
                     cyl(p.head_r * 0.28, 0.02, x=p.head_r * 0.38, y=-p.head_r * 0.97, z=p.head_z + 0.01, axis="Y", seg=14)]
    if not dark and not p.helmet:  # the part must exist; a suit's visor fills it (a stray cube floated inside the head)
        dark = [box(0.02, 0.02, 0.02, z=p.head_z)]
    return accent, dark, extra


# --------------------------------------------------------------------------- style headwear and costume

def fedora(p):
    """Noir: a fedora with a wide brim (reads from above) — dark — and a trench coat with a raised collar (Body) and a
    belt in the team colour."""
    w, d = p.torso
    top = p.head_z + p.head_r * 0.82
    dark = [cyl(p.head_r * 1.65, 0.025, z=top, seg=max(16, p.seg)),                                   # brim
            cyl(p.head_r * 0.98, 0.15, z=top + 0.07, seg=max(16, p.seg), r2=p.head_r * 0.86),          # crown
            box(p.head_r * 0.5, p.head_r * 1.6, 0.03, z=top + 0.15)]                                  # the crease
    coat = [cyl(w * 0.66, 0.62, z=p.hip - 0.12, seg=max(8, p.seg), r2=w * 0.52),                  # skirt flaring to the knees
            box(w * 0.32, 0.05, 0.16, x=-w * 0.2, y=-d * 0.42, z=p.top + 0.04, rx=-0.35, rz=0.3),     # collar flaps
            box(w * 0.32, 0.05, 0.16, x=w * 0.2, y=-d * 0.42, z=p.top + 0.04, rx=-0.35, rz=-0.3),
            box(w * 0.6, 0.05, 0.16, y=d * 0.42, z=p.top + 0.05, rx=0.3)]
    belt = [box(w * 0.86, d * 0.96, 0.06, z=p.hip + 0.12)]
    return dark, coat, belt


def balaclava(p):
    """Heist: a balaclava over the head (dark) with an eye slit (skin), a duffel bag in the team colour on the back."""
    w, d = p.torso
    dark = [sph(p.head_r * 1.06, z=p.head_z, seg=max(12, p.seg), sz=1.12)]
    slit = [box(p.head_r * 1.15, p.head_r * 0.5, p.head_r * 0.32, y=-p.head_r * 0.8, z=p.head_z + p.head_r * 0.12)]
    bag = [cyl(0.13, w * 0.72, y=d / 2 + 0.17, z=p.top - 0.14, axis="X", seg=max(10, p.seg)),
           limb((-w * 0.3, -d / 2 - 0.01, p.top - 0.02), (w * 0.35, -d / 2 - 0.02, p.hip + 0.05), 0.022)]  # strap
    vest = [box(w + 0.05, d + 0.06, (p.top - p.hip) * 0.6, z=p.top - (p.top - p.hip) * 0.33, taper=p.taper)]
    vest += [box(0.1, 0.05, 0.11, x=x, y=-(d / 2 + 0.06), z=p.hip + (p.top - p.hip) * 0.55) for x in (-w * 0.25, 0, w * 0.25)]
    return dark, slit, bag, vest


def visor_band(p):
    """Grid: a VR visor wrapping the front of the head (dark)."""
    return [box(p.head_r * 2.3, p.head_r * 1.3, p.head_r * 0.7, y=-p.head_r * 0.3, z=p.head_z + p.head_r * 0.12),
            box(p.head_r * 2.05, p.head_r * 2.05, 0.03, z=p.head_z + p.head_r * 0.12)]               # strap all round


# --------------------------------------------------------------------------- the styles

PLANS = {
    "S00_Base": ("clean", Plan(head="box")),
    "S01_NightShift": ("lofi", Plan(torso=(0.52, 0.34), leg_r=0.095, arm_r=0.08, hand_r=0.075, seg=6, helmet="hazmat", foot=(0.14, 0.3, 0.1))),
    "S02_Scrapyard": ("toy", Plan(hip=0.55, leg_r=0.1, torso=(0.5, 0.4), top=1.18, shoulder_x=0.28, arm_r=0.085, arm_len=0.42, head_r=0.25,
                                 head_z=1.45, hand_r=0.08, foot=(0.16, 0.24, 0.1), seg=20)),
    "S03_Heist": ("realistic", Plan(seg=20, mask=True)),
    "S04_Hunted": ("realistic", Plan(hip=1.0, leg_r=0.055, torso=(0.38, 0.22), top=1.48, shoulder_x=0.23, arm_r=0.045, arm_len=0.66,
                                    head_r=0.12, head_z=1.64, hand_r=0.045, seg=16)),
    "S05_Toon": ("lowpoly", Plan(torso=(0.56, 0.34), leg_r=0.1, arm_r=0.085, arm_len=0.56, hand_r=0.1, head_r=0.15, head="ico",
                                 foot=(0.16, 0.32, 0.12), seg=6, taper=1.18)),
    "S06_Storybook": ("clean", Plan(hip=0.95, leg_r=0.06, torso=(0.36, 0.22), top=1.45, shoulder_x=0.22, arm_r=0.045, head_r=0.12,
                                    head_z=1.62, seg=20, taper=1.12)),
    "S07_Logbook": ("clean", Plan(torso=(0.46, 0.3), seg=16, taper=0.95)),
    "S08_Footage": ("toy", Plan(torso=(0.5, 0.34), leg_r=0.1, arm_r=0.085, hand_r=0.08, seg=20, helmet="diver", foot=(0.15, 0.3, 0.12))),
    "S09_WhiteRoom": ("crystal", Plan(square=True, head="ico", seg=6)),
    "S10_Grid": ("clean", Plan(torso=(0.42, 0.24), leg_r=0.065, arm_r=0.05, seg=20, taper=1.1, visor=True)),
    # blocky noir figures, a sleek lab jumpsuit, chunky comic heroes, a heat suit, ruler-drawn blueprint people
    "S11_Noir": ("lofi", Plan(torso=(0.46, 0.28), leg_r=0.07, arm_r=0.06, seg=6, square=True, taper=1.12, hat="fedora")),
    "S12_TestChamber": ("clean", Plan(torso=(0.4, 0.24), leg_r=0.06, arm_r=0.045, seg=24, taper=0.95)),
    "S13_Comic": ("lowpoly", Plan(torso=(0.5, 0.3), leg_r=0.085, arm_r=0.075, arm_len=0.6, hand_r=0.08, head_r=0.15, seg=8, taper=1.2,
                                  foot=(0.15, 0.3, 0.1))),
    "S14_Thermal": ("realistic", Plan(seg=16, helmet="hazmat")),
    "S15_Blueprint": ("clean", Plan(torso=(0.4, 0.22), leg_r=0.05, arm_r=0.04, seg=8, square=True, head="box")),
    # real adult proportions (1.75 m, head 1/7.5 of the height); the stand-in until the rigged human replaces it
    "S16_Derelict": ("realistic", Plan(hip=0.92, leg_r=0.068, torso=(0.4, 0.23), top=1.44, shoulder_x=0.23, arm_r=0.048, arm_len=0.64,
                                       head_r=0.108, head_z=1.63, hand_r=0.045, foot=(0.1, 0.27, 0.07), seg=24, taper=1.12,
                                       head_scale=1.0)),
}


# voxel styles: the figure rebuilt from cubes of this size (the same grid as the style's robots and furniture)
VOXEL = {"S11_Noir": 0.04}


def build(style, finish, p, dept):
    G.FINISH = finish
    acc, dark, extra = department(p, dept)
    b = body(p) + hands(p) + extra
    head = face(p)
    if p.hat == "fedora":
        hat, coat, belt = fedora(p)
        dark, b, acc = dark + hat, b + coat, acc + belt
    if p.mask:
        mask, slit, bag, vest = balaclava(p)
        dark, head, acc, b = dark + mask, head + slit, acc + bag, b + vest
    if p.visor:
        dark = dark + visor_band(p)
    if p.helmet:
        shell, visor = helmet(p)
        b += shell
        dark = visor + [x for x in dark]
    merge("Body", b + legs(p))
    merge("Accent", acc)
    merge("AccentHead", head)
    merge("EyeGlasses", dark)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    G.meshfix.fix_objects(list(bpy.context.selected_objects), name=f"player__{style}__{DEPARTMENTS[dept]}")
    if style in VOXEL:
        voxel.voxelize(list(bpy.context.selected_objects), VOXEL[style])
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"player__{style}__{DEPARTMENTS[dept]}.fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             object_types={"MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False)
    G.g.clear_scene()


if __name__ == "__main__":
    G.g.clear_scene()
    only = argv[1:]  # optional: style asset names to build (default: all)
    for style, (finish, plan) in PLANS.items():
        if only and style not in only:
            continue
        for dept in range(4):
            build(style, finish, plan, dept)
        print("players", style)
    print("DONE")
