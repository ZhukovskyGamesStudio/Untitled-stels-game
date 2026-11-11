"""S03 Heist — «Ограбление»: modern private security tech (Realistic family: PBR).

Matte tactical armour over believable machines: angular chamfered plates with panel seams, visor slits instead of
round eyes, strobes, accessory rails, clean industrial design. Every robot is security equipment a contractor would
sell: an operator with a baton, an interceptor behind a ballistic shield, a K9 unit, a breaching crawler, a SIGINT
rover with phased-array panels, a cash courier, an armoured breaching vehicle, a biometric gate pillar.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import shapes as s
from lang import Lang, align
import designs as D

V = Vector


# =========================================================================== helpers (local to this style)

def basis(axis):
    if isinstance(axis, str):
        axis = {"X": (1, 0, 0), "-X": (-1, 0, 0), "Y": (0, 1, 0), "-Y": (0, -1, 0), "Z": (0, 0, 1), "-Z": (0, 0, -1)}[axis]
    a = V(axis).normalized()
    right = V((1, 0, 0)) if abs(a.x) < 0.9 else V((0, 1, 0))
    up = a.cross(right).normalized()
    right = up.cross(a).normalized()
    return a, right, up


def bar(p0, p1, w, t, normal, r=0.006, seg=1):
    """A flat bar from p0 to p1: width w, thickness t along `normal`."""
    p0, p1 = V(p0), V(p1)
    z = (p1 - p0)
    L = z.length
    z.normalize()
    y = V(normal)
    y = (y - z * y.dot(z)).normalized()
    x = y.cross(z)
    bm = s.rbox((w, t, L), r=r, seg=seg)
    m = Matrix((x, y, z)).transposed().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(p0 + p1) / 2, verts=bm.verts)
    return bm


def _to_axis(bm, a, loc):
    q = V((0, 0, 1)).rotation_difference(V(a).normalized())
    bmesh.ops.rotate(bm, cent=V((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=V(loc), verts=bm.verts)
    return bm


def lathe_between(profile, p0, p1, seg=24):
    p0, p1 = V(p0), V(p1)
    L = (p1 - p0).length
    bm = s.lathe([(r, t * L) for r, t in profile], seg=seg)
    return _to_axis(bm, (p1 - p0).normalized(), p0)


def grooved(bm, axis, values, width=0.01, depth=0.005):
    for v in values:
        s.groove(bm, axis, v, width, depth)
    return [bm]


def strobe(loc, r=0.03, axis="Z"):
    """(accent base, glow dome): a security strobe."""
    a, _, _ = basis(axis)
    loc = V(loc)
    base = _to_axis(s.rcyl(r * 1.3, r * 0.7, (0, 0, r * 0.35), seg=16, r=r * 0.15, bevel_seg=1), a, loc)
    glow = _to_axis(s.lathe([(r, 0), (r, r * 0.6), (r * 0.7, r * 1.1), (0, r * 1.3)], seg=16), a, loc + a * (r * 0.6))
    return [base], [glow]


def visor(L, loc, w, h, depth=0.05, wrap=0.0):
    """(accent frame, glow slit): a visor slit set in a chamfered frame on a -y face; wrap bends the ends back."""
    x, y, z = loc
    acc, glow = [], []
    mid = w * (0.6 if wrap else 1.0)
    acc += L.box((mid + 0.04, depth, h + 0.04), (x, y + depth * 0.3, z), r=0.012)
    glow += L.box((mid, 0.02, h), (x, y - 0.006, z), r=0.006)
    if wrap:
        side = (w - mid) / 2
        for sx in (-1, 1):
            cx = x + sx * (mid / 2 + side / 2 * math.cos(wrap))
            cy = y + side / 2 * math.sin(wrap)
            acc += L.box((side + 0.03, depth, h + 0.04), (cx, cy + depth * 0.3, z), rot=(0, 0, -sx * wrap), r=0.012)
            glow += L.box((side, 0.02, h), (cx, cy - 0.006, z), rot=(0, 0, -sx * wrap), r=0.006)
    return acc, glow


def hose(points, r=0.02):
    return s.tube([tuple(p) for p in points], r, res=2, segments_per=8)


# =========================================================================== the language

class Style(Lang):
    name = "S03_Heist"
    seg = 24
    edge = 0.022
    soft = 0.0
    sharp = 32.0
    hull_kind = "rounded"
    limb_kind = "tube"
    eye_kind = "visor"

    def rseg(self):
        return 1

    def loft(self, rings, axis="Z", loc=(0, 0, 0), round_caps=0.0, seg=None):
        return [s.loft(rings, seg=seg or 20, axis=axis, loc=loc, round_caps=round_caps)]

    def track(self, size, loc):
        """The shared crawler track with four rollers instead of six (triangle budget)."""
        w, d, h = size
        x, y, z = loc
        r = h / 2
        out = [s.loft([(-w / 2, d / 2, r, 6.0), (w / 2, d / 2, r, 6.0)], seg=24, axis="X", loc=loc)]
        for k in range(4):
            yy = y - d / 2 + r + (d - 2 * r) * k / 3
            out.append(s.rcyl(r * 0.62, w * 1.1, (x, yy, z), axis="X", seg=12, r=0.008, bevel_seg=1))
        return out


    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        return [s.ball(radius, loc, seg=16 if radius > .05 else 12, scale=scale)]

    def wheel(self, center, radius, width):
        c = V(center)
        tyre = s.lathe([(radius * 0.62, -width / 2), (radius * 0.92, -width / 2), (radius, -width * 0.32), (radius, width * 0.32),
                        (radius * 0.92, width / 2), (radius * 0.62, width / 2)], seg=24, axis="X", loc=c)
        hub = s.rcyl(radius * 0.66, width * 0.86, c, axis="X", seg=24, r=0.008, bevel_seg=1)
        cap = s.rcyl(radius * 0.32, width * 1.06, c, axis="X", seg=6, r=0.006, bevel_seg=1)
        return [tyre, hub, cap]

    # ------------------------------------------------------------------ furniture: corporate security fit-out
    leg_kind = "square"
    edge_kind = "chamfer"
    cushion_puff = 0.25
    handle_kind = "recess"
    foliage_kind = "blob"

    def carcass(self, size, loc, kick=0.06):
        """A cabinet with a shadow-gap plinth and a reveal line under the top."""
        w, d, h = size
        x, y, z = loc
        body = s.rbox((w, d, h - kick), (x, y, z + kick + (h - kick) / 2), r=min(min(w, d) * 0.3, 0.012), seg=1)
        if h - kick > 0.3:
            s.groove(body, "Z", z + h - 0.07, 0.01, 0.004)
        out = [body]
        if kick > 0:
            out += self.slab((w - 0.05, d - 0.07, kick + 0.01), (x, y + 0.02, z + (kick + 0.01) / 2))
        return out

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Square legs tied by an apron frame under the top."""
        out = []
        xs, ys = half_w - inset, half_d - inset
        for sx in (-1, 1):
            for sy in (-1, 1):
                out += self.leg((sx * xs, sy * ys, top_z), length, thick)
        for sy in (-1, 1):
            out += self.slab((xs * 2, .03, .08), (0, sy * ys, top_z - .04))
        for sx in (-1, 1):
            out += self.slab((.03, ys * 2, .08), (sx * xs, 0, top_z - .04))
        return out

    def handle(self, loc, length=0.12, vertical=True):
        """A flush finger pull with a chamfered lip."""
        x, y, z = loc
        size = (0.022, 0.03, length) if vertical else (length, 0.03, 0.022)
        return [s.rbox(size, (x, y + 0.006, z), r=0.006, seg=1)]

    def screen(self, size, loc, face="-Y"):
        """A thin-bezel display."""
        w, h = size
        x, y, z = loc
        sign = -1 if face == "-Y" else 1
        acc = [s.rbox((w, 0.025, h), (x, y, z), r=0.006, seg=1)]
        glow = [s.rbox((w - 0.025, 0.01, h - 0.025), (x, y + sign * 0.012, z), r=0.003, seg=1)]
        return acc, glow

    def shade(self, r_bottom, r_top, height, loc):
        """A straight drum shade with a lip."""
        r = (r_bottom + r_top) / 2
        return [s.lathe([(r, 0), (r + 0.004, 0.008), (r + 0.004, height), (r - 0.01, height), (r - 0.012, 0.01), (0, 0.01)],
                        seg=24, loc=loc)]

    def pot(self, r, h, loc):
        """A square tapered planter."""
        x, y, z = loc
        return [s.rbox((r * 1.7, r * 1.7, h), (x, y, z + h / 2), r=0.012, seg=1, taper=(1.12, 1.12))]

    def signature(self, model, P):
        if model == "laser_emitter":
            P.add("Body", self.box((.18, .18, .06), (0, 0, .03)) + self.box((.15, .15, .05), (0, 0, 1.6)))
            P.add("Body", self.box((.13, .02, 1.3), (0, .065, .85)))
        if model == "sensor_lidar":
            P.add("Body", self.box((.3, .3, .03), (0, 0, .015)))
        if model == "sensor_pir":
            P.add("Body", self.box((.16, .1, .02), (0, -.02, .075), rot=(math.radians(-10), 0, 0)))
        return P


# =========================================================================== walkers

def guard(L):
    """The operator: believable proportions in a plate carrier with pouches, a full helmet with a wrap-around visor
    slit and a strobe, angular pauldrons, forearm rails, a telescopic baton held low in the right hand."""
    P = D.Parts()
    P.add("Body", L.loft([(.84, .19, .14, 4), (.96, .21, .15, 4), (1.04, .19, .14, 4)], round_caps=.02))     # pelvis
    P.add("Body", grooved(L.loft([(1.0, .16, .12, 3.4), (1.12, .17, .13, 3.4), (1.24, .2, .14, 3.6)])[0], "Z", (1.08, 1.16)))
    P.add("Body", L.loft([(1.2, .22, .15, 4), (1.38, .3, .18, 5), (1.53, .33, .19, 5, 0, -.01), (1.66, .25, .16, 4)],
                         round_caps=.03))                                                                  # chest
    P.add("Accent", L.box((.38, .07, .34), (0, -.19, 1.43), rot=(math.radians(-6), 0, 0)))               # front plate
    P.add("Accent", L.box((.38, .07, .36), (0, .19, 1.43), rot=(math.radians(6), 0, 0)))                 # back plate
    for sx in (-1, 1):
        P.add("Accent", L.box((.05, .3, .14), (sx * .25, 0, 1.33)))                                      # cummerbund
    for x in (-.11, 0, .11):
        P.add("Body", L.box((.09, .07, .11), (x, -.24, 1.32)))                                           # pouches
    P.add("Body", L.box((.28, .12, .34), (0, .27, 1.42)))                                               # radio pack
    P.add("Body", L.box((.03, .03, .34), (.1, .3, 1.72), r=.008))                                       # antenna
    P.add("Body", L.box((.34, .05, .03), (0, -.22, 1.56), r=.006))                                      # chest rail
    for sx in (-1, 1):
        x = sx
        P.add("Accent", L.box((.2, .26, .1), (x * .37, 0, 1.61), rot=(0, x * math.radians(24), 0)))     # pauldron
        P.add("Body", L.ball(.08, (x * .37, 0, 1.55)))
        P.add("Body", L.loft([(1.22, .055, .06, 2.4, x * .41, -.02), (1.4, .07, .075, 2.6, x * .395, -.01),
                              (1.56, .07, .075, 2.6, x * .38, 0)]))                                      # upper arm
        P.add("Body", L.ball(.06, (x * .41, -.02, 1.2)))
        P.add("Body", L.loft([(.86, .05, .055, 2.6, x * .42, -.07), (1.02, .075, .08, 3.2, x * .42, -.05),
                              (1.2, .06, .065, 2.6, x * .41, -.02)]))                                   # forearm
        P.add("Accent", L.box((.1, .16, .18), (x * .43, -.05, 1.0), rot=(math.radians(-14), 0, 0)))     # bracer
        P.add("Body", L.box((.03, .14, .03), (x * .43, -.06, 1.1), rot=(math.radians(-14), 0, 0), r=.006))  # rail
        P.add("Body", L.box((.1, .12, .13), (x * .42, -.09, .8)))                                       # glove
    P.add("Accent", [lathe_between([(0, 0), (.026, 0), (.026, .3), (.02, .32), (.02, 1), (0, 1)], (.42, -.06, .82), (.42, -.56, .5), seg=12)])  # baton
    P.add("Eye", [lathe_between([(0, 0), (.026, 0), (.026, 1), (0, 1)], (.42, -.55, .51), (.42, -.59, .485), seg=12)])
    # helmet
    P.add("Body", L.cyl(.065, .12, (0, 0, 1.7)))
    P.add("Accent", L.loft([(1.71, .14, .15, 3.4), (1.84, .175, .185, 3.6, 0, -.01), (1.97, .16, .17, 3.4),
                            (2.03, .12, .13, 3.0)], round_caps=.04))
    P.add("Accent", L.box((.34, .06, .045), (0, -.17, 1.95), rot=(math.radians(18), 0, 0)))             # brow
    a, g = visor(L, (0, -.195, 1.85), .32, .04, wrap=math.radians(35))
    P.add("Body", a).add("Eye", g)
    for sx in (-1, 1):
        P.add("Body", L.box((.04, .13, .09), (sx * .175, 0, 1.82)))                                      # ear cups
    a, g = strobe((0, .07, 2.02), .03)
    P.add("Body", a).add("Eye", g)
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.09, (x, 0, .9)))
        P.add(leg, L.loft([(.47, .075, .085, 3.0, x, 0), (.68, .1, .11, 3.4, x, -.005), (.92, .1, .11, 3.2, x, 0)],
                          round_caps=.02))                                                               # thigh
        P.add(leg, L.cyl(.068, .17, (x, 0, D.GUARD_KNEE), axis="X"))
        P.add(leg, L.box((.14, .08, .16), (x, -.07, .47), rot=(math.radians(-8), 0, 0)))                 # knee pad
        P.add(leg, L.loft([(.1, .06, .07, 3.0, x, .0), (.28, .08, .1, 3.4, x, .02), (.45, .07, .08, 3.0, x, 0)],
                          round_caps=.02))                                                               # shin
        P.add(leg, L.box((.14, .1, .26), (x, -.05, .28)))                                                # shin guard
        P.add(leg, L.box((.15, .3, .1), (x, -.06, .05), taper=(.9, .8)))                                 # boot
        P.add(leg, L.box((.14, .08, .06), (x, -.19, .03)))                                               # toe cap
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """The interceptor: a tall armoured operator gliding on a thruster skirt, a full-height ballistic shield with a
    view slit on the left arm, a stun lance in the right, a long helmet with a visor."""
    P = D.Parts()
    P.add("Accent", L.loft([(.08, .3, .32, 6), (.24, .38, .4, 6), (.46, .2, .2, 5)], round_caps=.02))      # thruster skirt
    P.add("Eye", L.loft([(.04, .24, .26, 6), (.1, .26, .28, 6)]))
    P.add("Body", grooved(L.loft([(.4, .12, .1, 3.0), (.9, .15, .12, 3.4, 0, .02), (1.3, .2, .14, 3.6, 0, .02)])[0], "Z", (.7, 1.0)))
    P.add("Body", L.loft([(1.25, .22, .15, 4, 0, .02), (1.5, .32, .2, 5, 0, -.01), (1.78, .38, .22, 5, 0, -.03),
                          (1.94, .27, .18, 4, 0, -.02)], round_caps=.04))                                  # chest
    P.add("Accent", L.box((.42, .07, .34), (0, -.22, 1.6), rot=(math.radians(-8), 0, 0)))
    for sx in (-1, 1):
        P.add("Accent", L.box((.24, .32, .1), (sx * .42, -.02, 1.88), rot=(0, sx * math.radians(25), 0)))
        P.add("Body", L.ball(.08, (sx * .42, -.02, 1.8)))
    # left arm holds the shield
    P.add("Body", L.rod((-.43, -.02, 1.79), (-.47, -.18, 1.45), .06) + L.ball(.06, (-.47, -.18, 1.45)))
    P.add("Body", L.rod((-.47, -.18, 1.45), (-.3, -.46, 1.3), .055))
    P.add("Accent", L.box((.58, .06, 1.3), (-.2, -.56, 1.08), rot=(0, 0, math.radians(14))))           # shield
    P.add("Body", L.box((.5, .03, .06), (-.2, -.6, 1.6), rot=(0, 0, math.radians(14)), r=.008))
    P.add("Eye", L.box((.3, .02, .045), (-.21, -.605, 1.5), rot=(0, 0, math.radians(14)), r=.008))      # view slit
    P.add("Body", L.box((.12, .1, .14), (-.28, -.5, 1.3)))
    # right arm: the stun lance
    P.add("Body", L.rod((.43, -.02, 1.79), (.5, -.1, 1.42), .06) + L.ball(.06, (.5, -.1, 1.42)))
    P.add("Body", L.rod((.5, -.1, 1.42), (.46, -.38, 1.15), .055))
    P.add("Accent", L.box((.13, .15, .14), (.46, -.4, 1.13)))
    P.add("Accent", [lathe_between([(0, 0), (.03, 0), (.03, .8), (.022, .9), (0, 1)], (.46, -.25, 1.2), (.44, -1.0, .78), seg=12)])
    for sx in (-1, 1):
        P.add("Eye", L.box((.015, .12, .015), (.44 + sx * .016, -.97, .795), rot=(math.radians(-29), 0, 0), r=.004))
    # head
    P.add("Body", L.cyl(.065, .14, (0, -.06, 1.97)))
    P.add("Accent", L.loft([(0, .13, .12, 3.4), (.2, .15, .12, 3.6), (.36, .1, .08, 3.0)], axis="-Y", loc=(0, .1, 2.04),
                           round_caps=.04))
    a, g = visor(L, (0, -.24, 2.0), .22, .03, depth=.05, wrap=math.radians(30))
    P.add("Body", a).add("Eye", g)
    a, g = strobe((0, .02, 2.15), .025)
    P.add("Body", a).add("Eye", g)
    return P


def hound(L):
    """The K9 unit: a lean angular dog in an armoured vest with a whip antenna, a long sensor muzzle with intake
    slits, blade ears, digitigrade legs."""
    P = D.Parts()
    P.add("Body", L.loft([(.42, .14, .15, 4, 0, .02), (.12, .19, .2, 4.5, 0, .04), (-.18, .18, .19, 4.5, 0, .02),
                          (-.42, .14, .14, 4, 0, .0)], axis="-Y", loc=(0, 0, .82), round_caps=.05))
    P.add("Accent", L.box((.34, .56, .1), (0, -.04, 1.0), r=.02))                                        # vest top
    for sx in (-1, 1):
        P.add("Accent", L.box((.05, .5, .2), (sx * .18, -.06, .88), rot=(0, sx * math.radians(-10), 0), r=.015))
    P.add("Body", L.box((.03, .03, .3), (.1, .18, 1.18), r=.008))                                        # whip antenna
    a, g = strobe((-.08, .14, 1.05), .022)
    P.add("Body", a).add("Eye", g)
    P.add("Body", L.loft([(0, .08, .09, 3), (.24, .07, .08, 3)], axis="-Y", loc=(0, -.36, .9)))           # neck
    P.add("Body", L.loft([(0, .11, .12, 4), (.18, .12, .11, 4.5), (.48, .065, .055, 4)], axis="-Y", loc=(0, -.56, 1.0),
                         round_caps=.03))                                                                 # head + muzzle
    P.add("Accent", L.box((.2, .26, .05), (0, -.74, 1.11), rot=(math.radians(-8), 0, 0)))
    for sx in (-1, 1):
        P.add("Eye", L.box((.06, .04, .025), (sx * .1, -.78, 1.04), rot=(0, 0, sx * .5), r=.006))
        P.add("Accent", [bar((sx * .07, -.6, 1.08), (sx * .1, -.56, 1.27), .07, .025, (sx, -.3, 0))])  # blade ears
    for k in range(3):
        P.add("Eye", L.box((.06, .02, .012), (0, -1.04, .97 + k * .022), r=.004))                      # intake slits
    P.add("Body", L.loft([(0, .04, .03, 3), (.26, .02, .02, 3)], axis="Y", loc=(0, .4, .9)))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        back = y > 0
        P.add(leg, L.ball(.075, (x, y, .63)))
        P.add(leg, L.loft([(D.HOUND_KNEE, .045, .05, 3.4, x, y + (.04 if back else -.02)), (.48, .07, .08, 3.6, x, y + (.02 if back else 0)),
                           (.66, .075, .08, 3.4, x, y)], round_caps=.02))
        P.add(leg, L.cyl(.045, .1, (x, y + (.04 if back else -.02), D.HOUND_KNEE), axis="X"))
        P.add(leg, L.loft([(.05, .03, .035, 3, x, y), (.18, .035, .04, 3, x, y + (.0 if back else -.02)),
                           (D.HOUND_KNEE + .01, .04, .045, 3, x, y + (.04 if back else -.02))]))
        P.add(leg, L.box((.08, .13, .05), (x, y - .03, .025), taper=(.85, .8)))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """The breach crawler: a flat hexagonal armoured chassis like a door charge, a lens bar across its nose, a strobe
    on the back, flat blade legs with high knees."""
    P = D.Parts()
    P.add("Body", L.loft([(.1, .2, .26, 2.0), (.2, .24, .3, 2.0), (.3, .18, .24, 2.0)], seg=6, loc=(0, .04, 0)))
    P.add("Accent", L.loft([(.29, .17, .22, 2.0), (.33, .14, .18, 2.0)], seg=6, loc=(0, .04, 0)))         # top plate
    P.add("Body", grooved(L.box((.26, .12, .12), (0, -.23, .2))[0], "X", (-.06, .06)))                 # sensor nose
    P.add("Accent", L.box((.24, .04, .06), (0, -.3, .2), r=.01))
    for k, dx in enumerate((-.07, 0, .07)):
        P.add("Eye", L.lens(.022 if dx else .028, (dx, -.315, .2)))
    a, g = strobe((0, .12, .32), .025)
    P.add("Body", a).add("Eye", g)
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        side = V((d.y, -d.x, 0))
        P.add(f"AccentLeg{i}Upper", [bar(hip, knee + (knee - hip).normalized() * .02, .06, .025, side)] + L.ball(.035, knee))
        P.add(f"AccentLeg{i}Lower", [bar(knee, foot + V((0, 0, .02)), .045, .022, side)]
              + L.lathe([(0, 0), (.025, .01), (.02, .04), (0, .05)], loc=foot - V((0, 0, .02))))
    return P


# =========================================================================== wheeled and tracked

def listener(L):
    """The SIGINT rover: an armoured six-sided body on four tyres, a telescopic mast carrying two tilted phased-array
    panels (lit seams) and a long shotgun microphone aimed forward."""
    P = D.Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    P.add("Body", L.loft([(.26, .3, .5, 5), (.44, .33, .52, 6), (.6, .26, .42, 5, 0, .04)], round_caps=.03))
    for x, y in ((-.43, -.3), (.43, -.3), (-.43, .3), (.43, .3)):
        P.add("Body", L.cyl(.06, .2, (x * .78, y, .22), axis="X"))
        P.add("Accent", L.box((.16, .5, .05), (x * .92, y, .48), rot=(0, (1 if x > 0 else -1) * math.radians(-14), 0)))
    a, g = visor(L, (0, -.5, .44), .36, .03, depth=.05)
    P.add("Accent", a).add("Eye", g)
    P.add("Body", L.cyl(.08, .3, (0, .1, .72)))                                                        # mast sleeve
    P.add("Accent", L.cyl(.05, .4, (0, .1, 1.0)))
    P.add("Body", L.box((.76, .08, .06), (0, .1, 1.17)))                                               # yoke
    for sx in (-1, 1):
        c = V((sx * .36, .1, 1.18))
        rot = (0, sx * math.radians(-20), 0)
        P.add("Body", L.box((.05, .42, .5), tuple(c), rot=rot))
        for k in (-1, 0, 1):
            P.add("Eye", L.box((.02, .015, .44), (c.x + sx * .025, c.y + k * .12, c.z), rot=rot, r=.004))
    P.add("Accent", L.box((.1, .1, .1), (0, .02, 1.2)))
    P.add("Accent", [lathe_between([(0, 0), (.035, 0), (.035, .9), (.045, .92), (.045, 1), (0, 1)], (0, .0, 1.22), (0, -.62, 1.08), seg=12)])
    P.add("Eye", L.cyl(.03, .02, (0, -.625, 1.08), axis="Y", rot=(math.radians(13), 0, 0)))
    return P


def worker(L):
    """The cash courier: a low chamfered base, a rear tower with a strobe, a lift carriage holding the armoured
    case on two forks, a lit sensor slit on the bumper."""
    P = D.Parts()
    D.wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    P.add("Body", L.box((.56, .76, .2), (0, 0, .27)))
    for x, y in ((.31, .25), (-.31, .25), (.31, -.25), (-.31, -.25)):
        P.add("Body", L.cyl(.04, .14, (x * .8, y, .12), axis="X"))
    P.add("Accent", L.box((.6, .1, .14), (0, -.38, .3)))
    a, g = visor(L, (0, -.43, .32), .4, .03, depth=.04)
    P.add("Body", a).add("Eye", g)
    P.add("Body", grooved(L.box((.4, .22, .92), (0, .24, .8), taper=(.9, .9))[0], "Z", (.6, 1.0)))       # tower
    a, g = strobe((0, .24, 1.26), .035)
    P.add("Body", a).add("Eye", g)
    for sx in (-1, 1):
        P.add("Body", L.box((.05, .05, .86), (sx * .16, .11, .78)))                                     # lift rails
        P.add("Accent", L.box((.06, .5, .04), (sx * .14, -.12, .93)))                                   # forks
    P.add("Body", L.box((.38, .06, .2), (0, .1, .98)))                                                 # carriage
    P.add("Accent", L.box((.48, .44, .38), (0, -.15, 1.14)))                                           # the case
    P.add("Accent", L.box((.5, .46, .04), (0, -.15, 1.08), r=.012))
    P.add("Body", L.box((.18, .04, .03), (0, -.38, 1.26), r=.008) + L.box((.04, .04, .04), (-.08, -.38, 1.24), r=.008)
          + L.box((.04, .04, .04), (.08, -.38, 1.24), r=.008))                                         # handle
    return P


def sweeper(L):
    """The commercial floor robot: a tall chamfered body with a dark skirt, a lit visor across the front, a sensor
    mast with a strobe, side brushes on short arms, the lidar under the belly."""
    P = D.Parts()
    D.wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18)):
        P.add("Body", L.cyl(.035, .12, (x * .8, y, .08), axis="X"))
        P.add("Body", L.box((.08, .1, .24), (x * .66, y, .2)))
    P.add("Body", L.box((.14, .1, .14), (0, -.3, .24)))
    P.add("Body", L.box((.03, .06, .14), (-.05, -.3, .12)) + L.box((.03, .06, .14), (.05, -.3, .12)))
    P.add("Body", grooved(L.loft([(.32, .36, .36, 5), (.6, .35, .35, 5), (.74, .28, .28, 4.5)], round_caps=.05)[0], "Z", (.5,)))
    P.add("Accent", L.loft([(.3, .38, .38, 5), (.4, .385, .385, 5)]))
    ring = [(.37 * math.cos(k / 12 * math.tau), .37 * math.sin(k / 12 * math.tau), .22) for k in range(13)]
    for p0, p1 in zip(ring, ring[1:]):
        P.add("Body", [bar(V(p0) + (V(p0) - V(p1)) * .06, V(p1) + (V(p1) - V(p0)) * .06, .17, .03,
                           V((p0[0] + p1[0], p0[1] + p1[1], 0)), r=.006)])                          # dark under-skirt
    a, g = visor(L, (0, -.36, .6), .4, .04, depth=.05, wrap=math.radians(30))
    P.add("Accent", a).add("Eye", g)
    P.add("Body", L.cyl(.04, .2, (.16, .12, .84)) + L.box((.1, .08, .06), (.16, .12, .95)))
    a, g = strobe((.16, .12, .98), .022)
    P.add("Body", a).add("Eye", g)
    P.add("Body", L.cyl(.05, .1, (0, 0, .25)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    for sx in (-1, 1):
        c = (sx * .27, -.27, .02)
        P.add("Body", L.cyl(.022, .3, (c[0], c[1], .17)))
        P.add("Accent", L.cyl(.11, .02, c, seg=12))
        for k in range(4):
            a = k / 4 * math.tau
            P.add("Accent", L.box((.015, .12, .01), (c[0] + math.cos(a) * .07, c[1] + math.sin(a) * .07, .012),
                                  rot=(0, 0, a + math.pi / 2), r=.003))
    return P


def thumper(L):
    """The breaching vehicle: an armoured hull with a sloped glacis on tracks hidden behind side skirts, a sensor
    mast, two hydraulic ram arms pressing the pads in front."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.22, 1.36, .5), (sx * .56, 0, .26)))
        P.add("Body", L.box((.06, 1.3, .32), (sx * .69, 0, .42), rot=(0, sx * math.radians(-6), 0)))  # side skirt
    P.add("Body", grooved(L.loft([(.42, .5, .6, 6), (.8, .5, .6, 6), (1.02, .4, .48, 5, 0, .08)], round_caps=.03)[0], "Z", (.7,)))
    P.add("Body", L.box((.94, .4, .3), (0, -.5, .6), rot=(math.radians(35), 0, 0)))                     # glacis
    P.add("Accent", L.box((.6, .5, .2), (0, .1, 1.1), taper=(.85, .8)))                                 # cupola
    a, g = visor(L, (0, -.15, 1.12), .4, .04, depth=.05)
    P.add("Accent", a).add("Eye", g)
    P.add("Body", L.cyl(.03, .4, (.2, .3, 1.36)))
    a, g = strobe((.2, .3, 1.56), .028)
    P.add("Body", a).add("Eye", g)
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", L.box((.12, .3, .14), (x, -.62, .62)))
        P.add("Body", L.rod((x, -.62, .62), (x, -.85, .4), .055))
        P.add("Accent", L.rod((x, -.85, .42), (x, -.85, .1), .045, kind="piston"))
        P.add("Accent", L.box((.3, .3, .06), (x, -.85, .07)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
    P.add("Body", L.rod((-.3, -.7, .55), (.3, -.7, .55), .04))
    return P


def sorter(L):
    """The biometric gate pillar: an offset base plate, a slim faceted column, a tilted scanner ring around the big
    colour lens, a visor strip down the column."""
    P = D.Parts()
    P.add("Accent", L.box((.56, .5, .06), (0, .04, .03)))
    P.add("Body", grooved(L.loft([(.05, .16, .13, 6), (.6, .12, .1, 6), (1.04, .1, .09, 6)])[0], "Z", (.4, .8)))
    P.add("Eye", L.box((.03, .02, .6), (0, -.1, .55), r=.006))
    P.add("Body", L.box((.24, .2, .16), (0, .02, 1.04)))
    c = V((0, -.06, 1.25))
    tilt = math.radians(10)
    ring = s.lathe([(.2, 0), (.27, 0), (.29, .03), (.29, .12), (.27, .15), (.2, .15)], seg=32)
    s.place(ring, rot=(math.pi / 2 + tilt, 0, 0))
    bmesh.ops.translate(ring, vec=c + V((0, .06, 0)), verts=ring.verts)
    P.add("Accent", [ring])
    back = s.lathe([(0, 0), (.22, 0), (.24, .03), (.24, .08), (0, .08)], seg=32)
    s.place(back, rot=(math.pi / 2 + tilt, 0, 0))
    bmesh.ops.translate(back, vec=c + V((0, .14, -.01)), verts=back.verts)
    P.add("Body", [back])
    lens = s.lathe([(.21, 0), (.2, .02), (.12, .04), (0, .05)], seg=32)
    s.place(lens, rot=(math.pi / 2 + tilt, 0, 0))
    bmesh.ops.translate(lens, vec=c + V((0, -.085, .0)), verts=lens.verts)
    P.add("Eye", [lens])
    return P


# =========================================================================== devices

def drone(L):
    """The tactical quad: a faceted shell with a lit visor, chamfered arms to motor pods, a gimbal camera ball under
    the nose, strobes on the rear arms."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", L.loft([(.26, 0, 0), (.2, .1, .06, 4), (0, .16, .085, 5), (-.18, .13, .07, 4.5), (-.26, .06, .04, 3)],
                         axis="Y"))
    P.add("Accent", L.box((.16, .26, .04), (0, .03, .07)))
    a, g = visor(L, (0, -.215, .02), .16, .022, depth=.04)
    P.add("Accent", a).add("Eye", g)
    for i, (x, y) in enumerate(rot):
        P.add("Body", [bar((x * .2, y * .2, 0), (x, y, .0), .06, .03, (0, 0, 1), r=.01)])
        P.add("Body", L.cyl(.05, .09, (x, y, .0), seg=16))
        P.add(f"AccentRotor{i}", L.cyl(.025, .04, (x, y, .065), seg=12) +
              [bar((x - .17, y, .065), (x + .17, y, .065), .04, .008, (0, 0, 1), r=.002)])
        if y > 0:
            a, g = strobe((x * .6, y * .6, .014), .016)
            P.add("Accent", a).add("Eye", g)
    P.add("Accent", L.cyl(.035, .06, (0, -.2, -.07)))
    P.add("Accent", L.ball(.065, (0, -.24, -.12)))
    P.add("Eye", L.lens(.035, (0, -.295, -.12)))
    return P


def turret(L):
    """The remote weapon station on a bollard: a chamfered floor plinth with lit corner markers, a telescopic square
    mast with a cable conduit and a lit status ring, a slab-sided head with a sensor box, an ammo box and a shrouded
    barrel (the head turns, the ring and the markers do not)."""
    P = D.Parts()
    P.add("Body", grooved(L.box((.5, .5, .26), (0, 0, .13), taper=(.8, .8))[0], "Z", (.08,)))
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        P.add("Eye", L.box((.06, .03, .03), (math.cos(a) * .3, math.sin(a) * .3, .02), rot=(0, 0, a + math.pi / 2), r=.006))
        P.add("Body", L.box((.1, .05, .04), (math.cos(a) * .3, math.sin(a) * .3, .02), rot=(0, 0, a + math.pi / 2)))
    P.add("Body", L.box((.18, .18, .5), (0, 0, .5)))
    P.add("Accent", L.box((.14, .14, .4), (0, 0, .88)))
    P.add("Body", L.box((.05, .04, .7), (0, .1, .6), r=.01))                                            # conduit
    P.add("Body", L.loft([(.98, .13, .13, 6), (1.06, .12, .12, 6)]))
    P.add("Eye", L.loft([(1.02, .135, .135, 6), (1.06, .135, .135, 6)]))
    P.add("AccentHead", L.cyl(.1, .14, (0, 0, 1.12)))
    P.add("AccentHead", grooved(L.box((.34, .5, .24), (0, -.02, 1.3), taper=(.85, .9))[0], "Y", (-.1, .08)))
    P.add("AccentHead", L.box((.14, .2, .14), (.23, -.04, 1.3)))
    P.add("AccentHead", L.box((.14, .22, .16), (-.23, .02, 1.28)))
    P.add("AccentHead", L.cyl(.06, .3, (0, -.4, 1.29), axis="Y") + L.cyl(.03, .22, (0, -.6, 1.29), axis="Y"))
    P.add("AccentHead", L.box((.04, .34, .025), (0, -.06, 1.425), r=.006))
    return P


def vacuum(L):
    """A hexagonal ceiling unit: chamfered hex shell, a dark rim, a lit visor and a sensor dome facing the room."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.33, 0), (.36, .03), (.36, .07), (.32, .1), (0, .1)], seg=6)])
    P.add("Accent", L.loft([(.02, .37, .37, 2), (.07, .37, .37, 2)], seg=6))
    P.add("Accent", [s.lathe([(0, 0), (.24, 0), (.26, .015), (0, .022)], seg=6, loc=(0, 0, .1))])
    P.add("Accent", L.cyl(.09, .03, (0, 0, -.005)))
    P.add("Eye", L.lathe([(.07, 0), (.06, .02), (0, .035)], axis="-Z", loc=(0, 0, -.015)))
    P.add("Eye", L.box((.22, .04, .02), (0, -.25, -.002), r=.008))
    return P


def security_camera(L):
    """A bullet camera with a sun shield, a lit IR ring around the lens and a strobe."""
    P = D.security_camera(L)
    a, g = strobe((.05, .22, .1), .016)
    P.add("Accent", a).add("Eye", g)
    return P


def attach_eye(L):
    """The camera-eye module as a visor pod: a chamfered housing, a lit slit, a brow and a strobe."""
    P = D.Parts()
    P.add("Accent", L.box((.2, .12, .1), (0, -.02, 0)))
    P.add("Eye", L.box((.16, .02, .03), (0, -.085, 0), r=.006))
    P.add("Accent", L.box((.22, .06, .03), (0, -.06, .06), r=.008))
    a, g = strobe((.06, .01, .05), .016)
    P.add("Accent", a).add("Eye", g)
    return P


def attach_mic(L):
    """The shared microphone module at a lower segment count."""
    P = D.Parts()
    P.add("Accent", [s.rcyl(.045, .02, (0, 0, .01), seg=12, r=.004, bevel_seg=1)] + L.rod((0, 0, .01), (0, 0, .27), .014))
    P.add("Eye", [s.ball(.06, (0, 0, .32), seg=12)])
    P.add("Body", [s.rcyl(.066, .012, (0, 0, .32 + d), seg=12, r=.003, bevel_seg=1) for d in (-.03, 0, .03)])
    return P


# =========================================================================== furniture and level pieces

def arcade_machine(L):
    """A modern lounge cabinet: angled side fins with a lit edge, a slanted screen, a flush control deck."""
    P = D.Parts()
    P.add("Body", L.carcass((.64, .78, 1.75), (0, .02, 0), kick=.05))
    for sx in (-1, 1):
        P.add("Accent", L.box((.03, .82, 1.78), (sx * .335, .0, .89), taper=(1, .55)))                 # side fins
        P.add("Eye", L.box((.012, .02, 1.2), (sx * .352, -.2, .98), rot=(math.radians(-14), 0, 0), r=.004))
    P.add("Accent", L.slab((.66, .4, .05), (0, -.38, .95), rot=(math.radians(-12), 0, 0)))
    P.add("Eye", L.slab((.56, .03, .44), (0, -.38, 1.3), rot=(math.radians(-15), 0, 0)))
    P.add("Accent", L.slab((.66, .25, .18), (0, -.1, 1.71)))
    for dx in (-.15, 0, .15):
        P.add("Body", L.cyl(.025, .03, (dx, -.42, 1.0)))
    return P


def vending_machine(L):
    """A glass-front vending unit: the lit window, a touch panel with a screen, a pickup slot, a logo band."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .8, 1.9), (0, 0, 0), kick=.05))
    P.add("Eye", L.slab((.6, .02, 1.1), (-.1, -.42, 1.15)))
    P.add("Accent", L.slab((.66, .03, 1.16), (-.1, -.4, 1.15)))
    P.add("Accent", L.slab((.16, .03, .6), (.33, -.41, 1.15)))
    P.add("Eye", L.slab((.11, .02, .14), (.33, -.425, 1.33)))
    P.add("Accent", L.slab((.6, .03, .15), (-.1, -.41, .3)))
    P.add("Body", L.box((.5, .03, .06), (-.1, -.425, .3)))
    P.add("Accent", L.slab((.9, .03, .12), (0, -.405, 1.82)))
    return P


def safe(L):
    """A vault safe: a recessed door with a frame, a keypad, a three-spoke wheel, hinge bars, a status LED."""
    P = D.Parts()
    P.add("Body", L.carcass((.8, .7, 1.0), (0, 0, 0), kick=.04))
    P.add("Body", grooved(L.slab((.66, .03, .82), (0, -.355, .52))[0], "X", (-.25,)))
    P.add("Accent", L.slab((.12, .03, .16), (-.22, -.375, .82)))
    P.add("Eye", L.slab((.05, .02, .05), (-.22, -.38, .66)))
    c = V((.1, -.39, .5))
    P.add("Accent", L.cyl(.06, .05, tuple(c), axis="Y"))
    for k in range(3):
        a = k / 3 * math.tau + math.pi / 2
        tip = c + V((math.cos(a) * .14, -.02, math.sin(a) * .14))
        P.add("Accent", [lathe_between([(0, 0), (.016, 0), (.016, .9), (.026, .92), (.026, 1), (0, 1)], tuple(c), tuple(tip), seg=10)])
    for z in (.25, .8):
        P.add("Accent", L.slab((.04, .05, .16), (.39, -.33, z)))
    return P


def server_rack(L):
    """A glass-door rack: a chamfered frame, lit vertical cable-guide strips, rows of status units."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, 1.0, 2.0), (0, 0, 0), kick=.05))
    P.add("Body", L.slab((.66, .02, 1.88), (0, -.495, 1.0)))
    for x in (-.3, .3):
        P.add("Eye", L.slab((.02, .02, 1.7), (x, -.51, 1.0)))
    for i in range(11):
        P.add("Eye", L.slab((.4, .02, .03), (0, -.505, .22 + i * .16)))
    P.add("Body", L.slab((.1, .03, .2), (.24, -.515, 1.0)))
    return P


def reception_desk(L):
    """The security desk: a faceted front of two angled panels, a chamfered counter, a lit kick strip, a monitor."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Body", L.slab((1.22, .08, 1.08), (sx * .6, -.36, .55), rot=(0, 0, sx * math.radians(4))))
    P.add("Body", L.slab((.08, .76, 1.1), (-1.16, 0, .55)) + L.slab((.08, .76, 1.1), (1.16, 0, .55)))
    P.add("Accent", L.slab((2.5, .3, .04), (0, -.33, 1.12)) + L.slab((2.3, .7, .04), (0, .02, .74)))
    acc, glow = L.screen((.5, .32), (.4, .15, .96), "-Y")
    P.add("Eye", glow + acc + L.slab((.06, .05, .2), (.4, .19, .83)))
    for sx in (-1, 1):
        P.add("Eye", L.slab((1.1, .02, .025), (sx * .6, -.405, .11), rot=(0, 0, sx * math.radians(4))))
    return P


def water_cooler(L):
    """A modern dispenser: a chamfered column with a recessed niche and a sealed cylindrical tank on top."""
    P = D.Parts()
    P.add("Body", L.carcass((.34, .34, 1.0), (0, 0, 0), kick=.03))
    P.add("Body", L.slab((.24, .05, .26), (0, -.165, .62)) + L.slab((.2, .06, .03), (0, -.17, .5)))
    P.add("Body", L.cyl(.12, .05, (0, 0, 1.0)))
    P.add("Eye", [s.lathe([(0, 0), (.14, 0), (.15, .02), (.15, .41), (.13, .45), (0, .45)], seg=24, loc=(0, 0, .99))])
    P.add("Body", [s.lathe([(.145, 0), (.158, 0), (.158, .03), (.145, .03)], seg=24, loc=(0, 0, 1.2))])
    return P


def coffee_machine(L):
    """An espresso machine: a chamfered body, a group head with a portafilter, a grid drip tray, a display."""
    P = D.Parts()
    P.add("Body", L.slab((.34, .38, .5), (0, 0, .25)))
    P.add("Accent", L.slab((.24, .14, .03), (0, -.17, .075)))
    for dx in (-.06, 0, .06):
        P.add("Body", L.box((.012, .12, .01), (dx, -.17, .093)))
    P.add("Accent", L.cyl(.045, .05, (0, -.17, .27)) + L.slab((.03, .08, .025), (0, -.2, .245)))
    P.add("Accent", L.slab((.3, .06, .04), (0, -.19, .31)))
    P.add("Eye", L.slab((.12, .02, .05), (0, -.2, .4)))
    return P


def floor_lamp(L):
    """A linear floor lamp: a square plinth, a square stem, a drum shade."""
    P = D.Parts()
    P.add("Body", L.slab((.3, .3, .03), (0, 0, .015)) + L.box((.03, .03, 1.38), (0, 0, .71)))
    P.add("Accent", L.shade(.2, .2, .27, (0, 0, 1.38)))
    P.add("Eye", [s.rcyl(.14, .02, (0, 0, 1.36), seg=24, r=.003, bevel_seg=1)])
    return P


def ceiling_lamp(L):
    """A recessed linear LED panel: a chamfered housing, a trim frame, three lit bars."""
    P = D.Parts()
    P.add("Body", L.slab((1.24, .66, .05), (0, 0, -.025)))
    P.add("Accent", L.slab((1.24, .05, .03), (0, -.31, -.06)) + L.slab((1.24, .05, .03), (0, .31, -.06))
          + L.slab((.05, .58, .03), (-.6, 0, -.06)) + L.slab((.05, .58, .03), (.6, 0, -.06)))
    for y in (-.18, 0, .18):
        P.add("Eye", L.slab((1.12, .08, .03), (0, y, -.065)))
    return P


def lockers(L):
    """Evidence lockers: flush doors with keypads, vent slots and a number plate."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .5, 1.9), (0, 0, 0), kick=.06))
    for x in (-.3, 0.0, .3):
        P.add("Body", L.door((.29, 1.8), (x, -.255, .98)))
        P.add("Accent", L.slab((.07, .03, .1), (x + .08, -.27, 1.05)))
        P.add("Accent", L.handle((x + .08, -.27, .9), .14))
        for k in range(3):
            P.add("Accent", L.slab((.18, .015, .02), (x, -.267, 1.75 - k * .045)))
        P.add("Accent", L.slab((.08, .015, .05), (x - .06, -.267, 1.55)))
    return P


def plant(L):
    """A planter with a single stem through both bushes (swapped for a scan in the realistic family)."""
    P = D.Parts()
    P.add("Accent", L.pot(.21, .45, (0, 0, 0)))
    P.add("Body", L.rod((0, 0, .4), (.05, .02, 1.2), .03))
    P.add("Body", L.foliage((0, 0, .78), .3, n=6) + L.foliage((.06, .03, 1.2), .2, n=3, seed=4))
    return P


def shelf(L):
    """A storage rack whose boxes rest on the shelves (swapped for a scan in the realistic family)."""
    P = D.Parts()
    for x in (-.97, .97):
        for y in (-.27, .27):
            P.add("Body", L.leg((x, y, 2.0), 2.0, .05))
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", L.slab((2.0, .6, .04), (0, 0, h)))
    P.add("Accent", L.slab((.45, .4, .3), (-.6, 0, .618)) + L.slab((.35, .35, .25), (.5, .05, .593))
          + L.slab((.5, .45, .35), (.3, 0, 1.243)) + L.slab((.4, .4, .3), (-.55, 0, 1.818)))
    return P


def pallet(L):
    """A pallet of shrink-wrapped cartons with corner boards."""
    P = D.Parts()
    P.add("Accent", L.slab((1.2, 1.0, .04), (0, 0, .12)))
    for y in (-.45, 0, .45):
        P.add("Accent", L.slab((1.2, .1, .105), (0, y, .0525)))
    P.add("Body", sum([L.slab((.55, .45, .4), (x, y, .337)) for x in (-.3, .3) for y in (-.24, .24)], [])
          + sum([L.slab((.52, .42, .4), (-.3, y, .732)) for y in (-.24, .24)], []))
    for x in (-.565, .565):
        for y in (-.455, .455):
            P.add("Accent", L.box((.05, .05, .4), (x, y, .337)))
    return P


def bean_bag(L):
    """A lounge cube chair: a chamfered seat block, a high back block, seams."""
    P = D.Parts()
    P.add("Body", grooved(L.slab((.88, .66, .42), (0, -.11, .21))[0], "Z", (.3,)))
    P.add("Body", grooved(L.slab((.88, .26, .69), (0, .31, .345))[0], "Z", (.3,)))
    return P


def partition(L):
    """A frosted glass screen in a slim aluminium frame on blade feet, a frosted band across it."""
    P = D.Parts()
    P.add("Body", L.slab((1.54, .02, 1.16), (0, 0, .66)))
    P.add("Accent", L.slab((1.6, .035, .03), (0, 0, 1.245)) + L.slab((1.6, .035, .03), (0, 0, .075)))
    for x in (-.785, .785):
        P.add("Accent", L.slab((.03, .035, 1.2), (x, 0, .66)))
        P.add("Accent", L.slab((.3, .05, .02), (x * .8, 0, .01)) + L.slab((.03, .03, .08), (x * .8, 0, .05)))
    P.add("Accent", L.slab((1.54, .028, .07), (0, 0, .95)))
    return P


def kitchen_counter(L):
    """A handleless kitchen run: grooved flush fronts, a stone top, an undermount sink with a tall tap, the microwave."""
    P = D.Parts()
    P.add("Body", L.carcass((2.4, .65, .88), (0, 0, 0)))
    for i in range(4):
        x = -.9 + i * .6
        P.add("Body", L.door((.58, .68), (x, -.33, .45)))
    P.add("Body", grooved(L.slab((2.36, .02, .06), (0, -.33, .82))[0], "X", (-.6, 0, .6)))
    P.add("Accent", L.slab((2.44, .7, .05), (0, 0, .905)))
    P.add("Body", L.slab((.5, .38, .012), (-.6, -.02, .932)))
    P.add("Accent", [lathe_between([(0, 0), (.02, 0), (.016, .2), (.016, 1), (0, 1)], (-.6, .24, .93), (-.6, .24, 1.18), seg=12)]
          + [lathe_between([(0, 0), (.014, 0), (.014, 1), (0, 1)], (-.6, .24, 1.17), (-.6, .06, 1.12), seg=12)])
    P.add("Eye", L.slab((.5, .42, .3), (.7, .02, 1.08)))
    return P


def tv_stand(L):
    """A floating credenza on a recessed plinth, a thin screen on a slim post."""
    P = D.Parts()
    P.add("Body", grooved(L.slab((1.4, .4, .36), (0, 0, .32))[0], "X", (-.35, 0, .35)))
    P.add("Body", L.slab((1.1, .26, .16), (0, .04, .08)))
    acc, glow = L.screen((1.36, .78), (0, .06, 1.05), "-Y")
    P.add("Accent", acc + L.slab((.06, .04, .28), (0, .08, .6)) + L.slab((.34, .2, .02), (0, .08, .51)))
    P.add("Eye", glow)
    return P


def toilet_stall(L):
    """Floor-to-ceiling cubicle panels with a slim header rail and a wall-hung bowl."""
    P = D.Parts()
    for x in (-.51, .51):
        P.add("Body", L.slab((.03, 1.5, 1.99), (x, 0, .995)))
    P.add("Body", L.slab((1.05, .03, 1.99), (0, .76, .995)))
    P.add("Body", L.slab((1.05, .05, .08), (0, -.72, 1.95)))
    P.add("Accent", L.slab((.4, .5, .3), (0, .5, .45)) + L.slab((.2, .04, .14), (0, .74, .95)))
    return P


def whiteboard(L):
    """A glass board on a blade easel with a marker shelf."""
    P = D.Parts()
    P.add("Body", L.slab((1.8, .02, 1.0), (0, 0, 1.5)))
    for x in (-.86, .86):
        P.add("Accent", L.slab((.03, .06, 2.0), (x, -.005, 1.0)) + L.slab((.06, .08, .02), (x, 0, .01)))
    P.add("Accent", L.slab((1.7, .06, .03), (0, -.015, 1.0)))
    return P


def printer(L):
    """A multifunction printer: a drawer base, a scanner body with a lid, an output recess, a touch panel on an arm."""
    P = D.Parts()
    c = L.carcass((.6, .6, .7), (0, 0, 0), kick=.04)
    P.add("Body", grooved(c[0], "Z", (.3, .5)) + c[1:])
    P.add("Body", L.slab((.6, .56, .32), (0, .02, .85)) + L.slab((.58, .5, .04), (0, .04, 1.025)))
    P.add("Accent", L.slab((.4, .3, .03), (-.05, -.15, .73)) + L.slab((.42, .02, .2), (-.05, -.29, .3)))
    P.add("Accent", [bar((.16, -.24, .9), (.16, -.38, .95), .04, .03, (0, 0, 1), r=.006)] + L.slab((.2, .14, .02), (.16, -.4, .945), rot=(math.radians(30), 0, 0)))
    P.add("Eye", L.slab((.16, .1, .01), (.16, -.405, .958), rot=(math.radians(30), 0, 0)))
    return P


def fridge(L):
    """French doors over a freezer drawer, long bar handles, a dispenser recess."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .7, 1.8), (0, 0, 0), kick=.04))
    for x in (-.175, .175):
        P.add("Body", L.door((.35, 1.1), (x, -.355, 1.22)))
    P.add("Body", L.door((.7, .58), (0, -.355, .36)))
    for x in (-.03, .03):
        P.add("Accent", L.slab((.02, .03, .9), (x, -.385, 1.22)) + L.slab((.02, .03, .02), (x, -.375, .8)) + L.slab((.02, .03, .02), (x, -.375, 1.64)))
    P.add("Accent", L.slab((.5, .03, .02), (0, -.385, .58)) + L.slab((.02, .03, .02), (-.22, -.375, .58)) + L.slab((.02, .03, .02), (.22, -.375, .58)))
    P.add("Accent", L.slab((.16, .02, .24), (-.2, -.37, 1.2)))
    return P


def sofa(L):
    """A low slab sofa: a recessed plinth, deep seat slabs, a slim high back panel, slab arms."""
    P = D.Parts()
    P.add("Accent", L.slab((1.9, .7, .1), (0, 0, .05)))
    P.add("Body", L.cushion((.98, .76, .2), (-.5, -.04, .2)) + L.cushion((.98, .76, .2), (.5, -.04, .2)))
    P.add("Body", L.slab((2.0, .14, .78), (0, .35, .5)))
    for x in (-1.06, 1.06):
        P.add("Accent", L.slab((.1, .86, .56), (x * .97, 0, .29)))
    return P


def coffee_table(L):
    """A glass slab on two blade legs."""
    P = D.Parts()
    P.add("Body", L.slab((1.0, .56, .02), (0, 0, .41)))
    for x in (-.38, .38):
        P.add("Accent", L.slab((.03, .48, .4), (x, 0, .2)))
    return P


def meeting_table(L):
    """A glass top on two blade frames joined by a blade spine."""
    P = D.Parts()
    P.add("Body", L.slab((3.2, 1.3, .03), (0, 0, .765)))
    for x in (-1.1, 1.1):
        P.add("Accent", L.slab((.04, 1.0, .75), (x, 0, .375)))
    P.add("Accent", L.slab((2.24, .04, .08), (0, 0, .7)))
    return P


def round_table(L):
    """A round glass top on a cross of blades."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.5, .02, (0, 0, .75), seg=32, r=.004, bevel_seg=1)])
    P.add("Accent", L.slab((.56, .03, .74), (0, 0, .37)) + L.slab((.03, .56, .74), (0, 0, .37)))
    return P


def desk(L):
    """A glass-topped desk on blade legs with a blade modesty panel and a thin monitor."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .025), (0, 0, .7575)))
    for x in (-.77, .77):
        P.add("Accent", L.slab((.03, .74, .745), (x, 0, .3725)))
    P.add("Accent", L.slab((1.52, .02, .32), (0, .32, .58)))
    acc, glow = L.screen((.56, .34), (0, .17, .98), "-Y")
    P.add("Accent", acc + L.slab((.04, .04, .2), (0, .19, .85)) + L.slab((.22, .14, .015), (0, .19, .7775)))
    P.add("Eye", glow)
    P.add("Accent", L.slab((.44, .15, .015), (0, -.12, .7775)))
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "security_camera": security_camera, "attach_eye": attach_eye, "attach_mic": attach_mic,
    "arcade_machine": arcade_machine,
    "vending_machine": vending_machine,
    "safe": safe,
    "server_rack": server_rack,
    "reception_desk": reception_desk,
    "water_cooler": water_cooler,
    "coffee_machine": coffee_machine,
    "floor_lamp": floor_lamp,
    "ceiling_lamp": ceiling_lamp,
    "lockers": lockers,
    "plant": plant,
    "shelf": shelf,
    "pallet": pallet,
    "bean_bag": bean_bag,
    "partition": partition,
    "kitchen_counter": kitchen_counter,
    "tv_stand": tv_stand,
    "toilet_stall": toilet_stall,
    "whiteboard": whiteboard,
    "printer": printer,
    "fridge": fridge,
    "sofa": sofa,
    "coffee_table": coffee_table,
    "meeting_table": meeting_table,
    "round_table": round_table,
    "desk": desk,
}
