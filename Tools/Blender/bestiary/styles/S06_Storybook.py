"""S06 Storybook — «Книжка-чертёж»: art-deco clockwork automata from a picture book.

Language: lathed urns and fluted columns instead of boxes, stepped (ziggurat) discs, brass rims, round dial faces with a
monocle eye, wind-up keys, ball finials and sunburst fans. Symmetric, ornamental, tall and narrow.
Every robot is a toy from the same picture book: a nutcracker doorman, a skyscraper music box, a pom-pom poodle,
a wedding-cake crawler, a gramophone cart, a hotel luggage trolley, a dinner cloche, a steam roller, a sunburst
mirror, a flying brooch, a column with a blunderbuss, a ceiling medallion.
"""
import math

import bmesh

from mathutils import Matrix, Vector

import shapes as s
import props
from lang import Lang, align
import designs as D


def torus_x(R, r, loc, seg=20, seg_r=8, axis_z=False):
    """A ring about the X axis (finger rings, hoops seen from the side), or about Z with axis_z."""
    import bmesh
    bm = bmesh.new()
    if axis_z:
        rings = [[bm.verts.new(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b)))
                  for b in [j / seg_r * math.tau for j in range(seg_r)]] for a in [i / seg * math.tau for i in range(seg)]]
    else:
        rings = [[bm.verts.new((r * math.sin(b), (R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a)))
                  for b in [j / seg_r * math.tau for j in range(seg_r)]] for a in [i / seg * math.tau for i in range(seg)]]
    for i in range(seg):
        p, q = rings[i], rings[(i + 1) % seg]
        for j in range(seg_r):
            bm.faces.new((p[j], q[j], q[(j + 1) % seg_r], p[(j + 1) % seg_r]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return s.place(bm, loc)


class Style(Lang):
    name = "S06_Storybook"
    seg = 12
    soft = 0.15
    edge = 0.016
    hull_kind = "barrel"
    eye_kind = "porthole"
    limb_kind = "tube"

    def rseg(self):
        return 1

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        """Balls are the costliest form: few segments, more only on big ones."""
        return [s.ball(radius, loc, seg=16 if radius > .12 else 12 if radius > .06 else 8, scale=scale)]

    # ------------------------------------------------------------------ the picture-book vocabulary

    def steps(self, tiers, loc=(0, 0, 0), seg=None):
        """Stepped discs (a ziggurat lathe): tiers = [(radius, height), ...] bottom to top, each tier with a small chamfer."""
        prof, z = [(0, 0)], 0.0
        for r, h in tiers:
            c = min(0.008, h * 0.3, r * 0.1)
            prof += [(r - c, z), (r, z + c), (r, z + h - c), (r - c, z + h)]
            z += h
        prof.append((0, z))
        return [s.lathe(prof, seg=seg or self.seg, loc=loc)]

    def finial(self, loc, h=0.16, r=0.04):
        """A ball finial on a short turned neck."""
        x, y, z = loc
        out = [s.lathe([(0, 0), (r * 0.9, 0), (r * 0.5, h * 0.25), (r * 0.35, h * 0.55), (0, h * 0.6)], seg=16, loc=loc)]
        out.append(s.ball(r, (x, y, z + h * 0.6 + r * 0.8), seg=16))
        return out

    def key(self, loc, size=1.0, axis="Y"):
        """A wind-up key sticking out along +axis (Y = backwards): shaft, collar and two big round wings."""
        x, y, z = loc
        k = size
        if axis == "Y":
            out = [s.rcyl(.028 * k, .2 * k, (x, y + .1 * k, z), axis="Y", seg=12, r=.006)]
            out.append(s.rcyl(.05 * k, .03 * k, (x, y + .04 * k, z), axis="Y", seg=16, r=.006))
            for sx in (-1, 1):
                out.append(s.loft([(0, .1 * k, .085 * k, 2.0), (.035 * k, .1 * k, .085 * k, 2.0)], seg=14, axis="Y",
                                  loc=(x + sx * .09 * k, y + .19 * k, z)))
        else:  # up (Z)
            out = [s.rcyl(.028 * k, .2 * k, (x, y, z + .1 * k), seg=12, r=.006)]
            for sx in (-1, 1):
                out.append(s.loft([(0, .085 * k, .1 * k, 2.0), (.035 * k, .085 * k, .1 * k, 2.0)], seg=14, axis="Y",
                                  loc=(x + sx * .09 * k, y - .0175 * k, z + .19 * k)))
        return out

    def sunburst(self, c, r0, r1, n, a0=0.0, a1=math.pi, width=.05, depth=.03, plane="XZ"):
        """A fan of tapered rays from angle a0 to a1 in a plane through c (XZ: facing the front; XY: lying flat)."""
        c = Vector(c)
        out = []
        for k in range(n):
            a = a0 + (a1 - a0) * (k + 0.5) / n
            d = Vector((math.cos(a), 0, math.sin(a))) if plane == "XZ" else Vector((math.cos(a), math.sin(a), 0))
            L = r1 - r0
            ray = s.rbox((width, depth, L), r=min(width, depth) * 0.3, seg=1, taper=(0.35, 1.0)) if plane == "XZ" else \
                s.rbox((width, L, depth), r=min(width, depth) * 0.3, seg=1)
            if plane == "XZ":
                out.append(align(ray, c + d * r0, c + d * r1))
            else:
                ang = math.atan2(d.y, d.x) - math.pi / 2
                s.place(ray, rot=(0, 0, ang))
                out.append(s.place(ray, c + d * (r0 + L / 2)))
        return out

    def dial(self, r, loc, axis="-Y", depth=.03):
        """A round dial face plate with a brass rim (body, rim)."""
        loc = Vector(loc)
        a = {"-Y": Vector((0, -1, 0)), "Z": Vector((0, 0, 1)), "-Z": Vector((0, 0, -1))}[axis]
        face = s.rcyl(r, depth, loc, axis=axis.lstrip("-"), seg=self.seg, r=.006)
        rim = s.lathe([(r * .86, 0), (r * 1.06, 0), (r * 1.1, depth * .5), (r * 1.06, depth * 1.1), (r * .86, depth * 1.1)],
                      seg=self.seg, axis=axis, loc=loc + a * depth * 0.1)
        return [face], [rim]

    def monocle(self, r, loc, axis="-Y"):
        """(accent, glow): a monocle — a fat brass ring around a domed glowing lens."""
        loc = Vector(loc)
        a = {"-Y": Vector((0, -1, 0)), "-Z": Vector((0, 0, -1)), "Z": Vector((0, 0, 1))}[axis]
        ring = s.lathe([(r * .8, 0), (r * 1.3, 0), (r * 1.38, .02), (r * 1.3, .045), (r * .95, .045), (r * .8, .03)],
                       seg=self.seg, axis=axis, loc=loc - a * .01)
        lens = s.lathe([(r, 0), (r * .95, .015), (r * .6, .035), (0, .042)], seg=self.seg, axis=axis, loc=loc - a * .005)
        return [ring], [lens]

    def fluted(self, r, z0, z1, loc=(0, 0), n=10):
        """A fluted column: a core and n half-sunk ribs."""
        x, y = loc
        h = z1 - z0
        out = [s.rcyl(r * .9, h, (x, y, (z0 + z1) / 2), seg=self.seg, r=.004)]
        for k in range(n):
            a = (k + .5) / n * math.tau
            out.append(s.rbox((r * .32, r * .32, h * .96), (x + math.cos(a) * r * .86, y + math.sin(a) * r * .86, (z0 + z1) / 2),
                              r=r * .1, seg=1, rot=(0, 0, a)))
        return out

    def wheel(self, center, radius, width):
        """A carriage wheel: a rounded tyre ring, a hub and spokes on both faces (symmetric about its centre)."""
        c = Vector(center)
        out = [s.lathe([(radius * .78, -width / 2), (radius, -width * .3), (radius, width * .3), (radius * .78, width / 2),
                        (radius * .74, 0), (radius * .78, -width / 2)], seg=self.seg, axis="X", loc=c)]
        out.append(s.rcyl(radius * .8, width * .3, c, axis="X", seg=self.seg, r=0))         # web inside the rim
        out.append(s.rcyl(radius * .28, width * 1.1, c, axis="X", seg=10, r=.008))          # hub
        for k in range(6):
            a = (k + .5) / 6 * math.tau
            d = Vector((0, math.cos(a), math.sin(a)))
            out.append(align(s.rbox((width * .7, .022, radius * .6), r=.006, seg=1), c + d * radius * .2, c + d * radius * .8))
        return out

    # ------------------------------------------------------------------ furniture vocabulary: art deco
    leg_kind = "turned"
    edge_kind = "chamfer"
    cushion_puff = .7
    handle_kind = "knob"
    foliage_kind = "ball"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """Boards have a stepped edge (a narrower step on top); panels and blocks are chamfered."""
        w, d, h = size
        x, y, z = loc
        if h <= .08 and min(w, d) > .25 and not any(rot):
            return [s.rbox((w, d, h * .6), (x, y, z - h * .2), r=min(.01, h * .25), seg=1),
                    s.rbox((w - .03, d - .03, h * .5), (x, y, z + h * .25), r=min(.008, h * .2), seg=1)]
        return [s.rbox(size, loc, r=min(min(size) * .3, .012), seg=1, rot=rot)]

    def carcass(self, size, loc, kick=0.06):
        """A cabinet with a stepped cornice on top and a stepped plinth for a kick."""
        w, d, h = size
        x, y, z = loc
        out = [s.rbox((w, d, h - kick), (x, y, z + kick + (h - kick) / 2), r=.01, seg=1)]
        out.append(s.rbox((w + .02, d + .012, .035), (x, y, z + h - .02), r=.008, seg=1))
        out.append(s.rbox((w + .01, d + .006, .03), (x, y, z + h - .05), r=.006, seg=1))
        if kick > 0:
            out.append(s.rbox((w - .02, d - .03, kick + .01), (x, y + .01, z + kick / 2 + .005), r=.006, seg=1))
            out.append(s.rbox((w - .06, d - .06, kick * .5), (x, y + .01, z + kick * .25), r=.004, seg=1))
        return out

    def door(self, size, loc, face="-Y"):
        """A fielded panel: a raised centre on the door."""
        w, h = size
        x, y, z = loc
        return [s.rbox((w - .012, .022, h - .012), (x, y, z), r=.006, seg=1),
                s.rbox((max(.03, w - .09), .014, max(.03, h - .09)), (x, y - .013, z), r=.005, seg=1)]

    def handle(self, loc, length=0.12, vertical=True):
        """A round knob on a stepped rose."""
        x, y, z = loc
        return [s.rcyl(.028, .014, (x, y + .002, z), axis="Y", seg=12, r=.004), s.ball(.02, (x, y - .018, z), seg=10)]

    def screen(self, size, loc, face="-Y"):
        acc, glow = Lang.screen(self, size, loc, face)
        w, h = size
        x, y, z = loc
        acc.append(s.rbox((w + .04, .02, h + .04), (x, y + .016, z), r=.008, seg=1))
        return acc, glow

    def shade(self, r_bottom, r_top, height, loc):
        """A stepped deco shade: three tiers, a shell open at the bottom (pole to pole, no flat caps)."""
        h = height
        rm1, rm2 = r_bottom + (r_top - r_bottom) * .4, r_bottom + (r_top - r_bottom) * .75
        prof = [(0, h + .004), (r_top, h), (r_top, h * .7), (rm2, h * .66), (rm2, h * .38), (rm1, h * .34), (rm1, h * .05), (r_bottom, 0),
                (r_bottom - .008, .006), (rm1 - .008, h * .36), (rm2 - .008, h * .68), (r_top - .008, h - .008), (0, h - .008)]
        return [s.lathe(prof, seg=16, loc=loc)]

    def pot(self, r, h, loc):
        """An urn: a stepped foot, a swelling belly, a flared lip."""
        return [s.lathe([(0, 0), (r * .6, 0), (r * .6, h * .08), (r * .45, h * .14), (r * .8, h * .4), (r, h * .7), (r * .85, h * .86),
                         (r * 1.04, h * .94), (r * 1.04, h), (0, h)], seg=16, loc=loc)]

    def foliage(self, centre, r, n=5, seed=1):
        """Clipped topiary: one round ball."""
        return [s.ball(r * .95, centre, seg=14)]

    def signature(self, model, P):
        # brass finials crown the devices too
        if model == "attach_mic":
            P.add("Body", self.steps([(.05, .012), (.035, .012)], loc=(0, 0, -.002)))
        if model == "attach_seismic":  # a stepped brass cap on the probe
            P.add("Accent", self.steps([(.09, .02), (.065, .02), (.04, .02)], loc=(0, 0, .3)))
        if model == "attach_nose":  # brass rings round the snout
            for y, r in ((-.06, .085), (-.17, .066)):
                P.add("Accent", [s.loft([(y - .012, r, r * .95, 2.0), (y + .012, r, r * .95, 2.0)], seg=20, axis="Y")])
        return P


# =========================================================================== walkers

def guard(L):
    """Nutcracker doorman: an urn torso with brass bands and stepped epaulettes, a round dial face with a monocle,
    a tall shako with a plume finial, a big wind-up key in the back, column legs on round pedestal feet."""
    P = D.Parts()
    # urn torso (wider than deep), brass waist band, stepped skirt at the hips
    P.add("Body", L.loft([(.84, .2, .16, 2.2), (.97, .25, .19, 2.2), (1.1, .2, .155, 2.2), (1.34, .29, .2, 2.3),
                          (1.54, .35, .22, 2.4), (1.64, .3, .2, 2.3), (1.7, .14, .12, 2.0)], round_caps=.02))
    P.add("Accent", L.loft([(1.07, .225, .175, 2.2), (1.13, .225, .175, 2.2)]))
    P.add("Accent", L.loft([(1.4, .325, .222, 2.3), (1.45, .335, .226, 2.3)]))
    P.add("Accent", L.loft([(.8, .25, .2, 2.2), (.86, .27, .21, 2.2), (.9, .27, .21, 2.2), (.94, .24, .19, 2.2)]))
    # chest buttons: three brass studs down the front
    for z in (1.22, 1.32, 1.5):
        P.add("Accent", [s.ball(.03, (0, -.2 - (.012 if z > 1.4 else 0) + (z - 1.22) * -.05, z), seg=12)])
    for sx in (-1, 1):
        P.add("Accent", L.steps([(.13, .035), (.1, .035), (.065, .03)], loc=(sx * .33, 0, 1.57)))   # epaulette
        P.add("Accent", L.ball(.06, (sx * .37, 0, 1.52)))
        P.add("Body", L.rod((sx * .37, 0, 1.52), (sx * .4, -.03, 1.18), .055))
        P.add("Accent", L.cyl(.06, .05, (sx * .4, -.03, 1.17)))
        P.add("Body", L.rod((sx * .4, -.03, 1.17), (sx * .38, -.12, .9), .05))
        P.add("Accent", L.ball(.085, (sx * .38, -.13, .85)))                                   # white-glove ball
    # head: a drum with a dial face, the monocle on one eye, a tall shako with a visor and a plume finial
    P.add("Body", L.cyl(.06, .1, (0, 0, 1.7)))
    P.add("Body", L.lathe([(0, 1.7), (.15, 1.7), (.17, 1.74), (.17, 1.95), (.15, 1.98), (0, 1.98)]))
    face, rim = L.dial(.135, (0, -.155, 1.84))
    P.add("Body", face).add("Accent", rim)
    P.eye(L.monocle(.06, (.055, -.19, 1.86)))
    P.add("Eye", L.box((.05, .02, .02), (-.06, -.188, 1.86), r=.007))
    P.add("Accent", L.lathe([(0, 1.96), (.17, 1.96), (.18, 2.0), (.2, 2.2), (.21, 2.24), (0, 2.24)]))   # shako
    P.add("Body", L.loft([(2.04, .181, .181, 2.0), (2.08, .186, .186, 2.0)]))                          # shako band
    P.add("Accent", L.box((.26, .12, .02), (0, -.17, 1.975), r=.008, rot=(math.radians(-8), 0, 0)))     # visor
    P.add("Accent", L.finial((0, 0, 2.23), h=.14, r=.05))
    P.add("Accent", L.key((0, .17, 1.36), 1.25))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.075, (x, 0, .9)))
        P.add(leg, L.lathe([(.05, D.GUARD_KNEE), (.075, .55), (.085, .75), (.07, .9)], loc=(x, 0, 0)))
        P.add(leg, L.steps([(.075, .03), (.06, .03)], loc=(x, 0, D.GUARD_KNEE - .03)))          # knee disc
        P.add(leg, L.lathe([(.05, .08), (.06, .2), (.055, D.GUARD_KNEE)], loc=(x, 0, 0)))
        P.add(leg, L.steps([(.13, .04), (.1, .04), (.065, .04)], loc=(x, -.05, 0)))              # pedestal foot
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """Skyscraper music box: a stepped setback tower gliding on a round music-box drum, a sunburst crown fan behind a
    dial face with a monocle, long thin arms with tailor's shears."""
    P = D.Parts()
    P.add("Accent", L.steps([(.42, .05), (.36, .05), (.28, .06)], loc=(0, 0, .02)))                 # music-box base
    P.add("Eye", L.loft([(0, .4, .4, 2.0), (.025, .4, .4, 2.0)], loc=(0, 0, 0)))                   # glide ring
    P.add("Body", L.fluted(.13, .17, .95, n=8))                                                      # stem column
    # setback tiers of the tower: square-ish sections that step in
    P.add("Body", L.loft([(.9, .17, .13, 4), (1.05, .26, .17, 4), (1.4, .3, .19, 4)], round_caps=.0))
    P.add("Accent", L.loft([(1.38, .33, .21, 4), (1.44, .33, .21, 4)]))
    P.add("Body", L.loft([(1.42, .32, .2, 4), (1.75, .38, .22, 4), (1.8, .33, .2, 4)]))
    P.add("Accent", L.loft([(1.78, .35, .21, 4), (1.84, .27, .19, 4)]))
    P.add("Body", L.loft([(1.82, .21, .17, 4), (2.06, .2, .16, 4), (2.12, .14, .12, 4)]))
    P.add("Accent", L.loft([(2.1, .15, .13, 4), (2.16, .1, .09, 4), (2.24, .06, .06, 4)]))
    P.add("Accent", L.finial((0, 0, 2.22), h=.18, r=.04))
    # sunburst crown behind the head
    P.add("Accent", L.sunburst((0, .12, 1.86), .16, .5, 9, a0=math.radians(10), a1=math.radians(170), width=.07, depth=.04))
    P.add("Accent", [s.rcyl(.2, .05, (0, .12, 1.86), axis="Y", seg=24, r=.01)])
    face, rim = L.dial(.13, (0, -.17, 1.92))
    P.add("Body", face).add("Accent", rim)
    P.eye(L.monocle(.075, (0, -.205, 1.92)))
    for sx in (-1, 1):
        sh = (sx * .4, 0, 1.68)
        P.add("Accent", L.ball(.065, sh))
        P.add("Body", L.rod(sh, (sx * .5, -.08, 1.3), .04))
        P.add("Accent", L.cyl(.055, .05, (sx * .5, -.08, 1.29)))
        P.add("Body", L.rod((sx * .5, -.08, 1.29), (sx * .3, -.42, 1.08), .036))
        P.add("Accent", [torus_x(.06, .022, (sx * .3, -.42, 1.08))])                                  # finger ring
        a, b = Vector((sx * .3, -.42, 1.08)), Vector((-sx * .3, -1.0, .96))
        blade = s.loft([(-.375, .022, .05, 2.6), (-.075, .02, .07, 2.6), (.375, .0, .0)], seg=12, axis="Z")  # align() wants it centred
        P.add("Accent", [align(blade, a, a + (b - a).normalized() * .75)])                         # shear blade
    P.add("Accent", L.ball(.045, (0, -.71, 1.02)))                                                  # shear pivot
    return P


def hound(L):
    """Pom-pom poodle: a slim body with a big ball mane on the chest, pom-poms on the hips, the topknot, the tail and
    every ankle, long ears, a narrow snout ending in the glowing nose, a wind-up key in the back."""
    P = D.Parts()
    P.add("Body", L.loft([(.45, .1, .1, 2.0), (.1, .13, .14, 2.0), (-.25, .1, .11, 2.0)], axis="-Y", loc=(0, 0, .74),
                         round_caps=.05))                                                          # waist
    P.add("Body", L.ball(.2, (0, -.3, .84), scale=(1, 1.1, 1.05)))                                 # chest mane
    P.add("Body", L.ball(.16, (0, .34, .78)))                                                      # rump pompom
    P.add("Accent", L.loft([(.68, .13, .13, 2.0), (.74, .14, .14, 2.0)], loc=(0, -.5, .35), axis="Z"))  # collar ring
    P.add("Body", L.rod((0, -.48, 1.0), (0, -.58, 1.08), .07))                                     # neck
    P.add("Body", L.ball(.15, (0, -.6, 1.14)))                                                     # head
    P.add("Body", L.ball(.11, (0, -.58, 1.31)))                                                    # topknot
    P.add("Body", L.loft([(0, .075, .08, 2.2), (.3, .05, .05, 2.2), (.34, .04, .04, 2.0)], axis="-Y",
                         loc=(0, -.68, 1.08), round_caps=.02))                                     # snout
    P.add("Eye", L.ball(.04, (0, -1.03, 1.06)))                                                    # nose
    for sx in (-1, 1):
        P.add("Body", L.loft([(0, .045, .05, 2.0), (.22, .065, .07, 2.0), (.26, .0, .0)], axis="-Z",
                             loc=(sx * .13, -.6, 1.16)))                                           # long ear
        P.add("Eye", L.box((.035, .02, .02), (sx * .05, -.735, 1.15), r=.007))
        P.add("Body", L.ball(.12, (sx * .1, .3, .7)))                                              # hip pompoms
    P.add("Body", L.rod((0, .45, .85), (0, .62, 1.12), .025))                                      # tail
    P.add("Body", L.ball(.08, (0, .64, 1.16)))
    P.add("Body", L.key((0, .02, .86), .9, axis="Z"))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        if y < 0:
            P.add("Body", L.ball(.085, (x * .85, y, .7)))                                           # shoulder socket
        P.add(leg, L.ball(.06, (x, y, D.HOUND_HIP)))
        P.add(leg, L.lathe([(.035, D.HOUND_KNEE), (.045, .45), (.05, D.HOUND_HIP)], loc=(x, y, 0)))
        P.add(leg, L.cyl(.05, .04, (x, y, D.HOUND_KNEE)))
        P.add(leg, L.lathe([(.03, .06), (.035, D.HOUND_KNEE)], loc=(x, y, 0)))
        P.add(leg, L.ball(.075, (x, y, .1)))                                                       # ankle pompom
        P.add(leg, L.steps([(.06, .03), (.04, .03)], loc=(x, y, 0)))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """Wedding-cake crawler: a stepped three-tier dome with a finial, a dial face in front with two monocles,
    eight thin brass legs."""
    P = D.Parts()
    P.add("Body", L.steps([(.25, .11), (.2, .09), (.145, .08), (.09, .06)], loc=(0, .02, .06)))
    P.add("Body", L.lathe([(0, 0), (.22, 0), (.25, .06)], loc=(0, .02, .0)))
    P.add("Body", L.finial((0, .02, .39), h=.14, r=.04))
    face, rim = L.dial(.1, (0, -.205, .17))
    P.add("Body", face + rim)
    for sx in (-1, 1):
        acc, glow = L.monocle(.032, (sx * .045, -.235, .18))
        P.add("Body", acc).add("Eye", glow)
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .025) + L.ball(.038, knee))
        P.add(f"AccentLeg{i}Lower", L.rod(knee + (foot - knee) * .02, foot + Vector((0, 0, .03)), .018)
              + L.steps([(.035, .02), (.022, .02)], loc=foot - Vector((0, 0, .025))))
    return P


# =========================================================================== wheeled

def listener(L):
    """Gramophone cart: a deco cabinet on four carriage wheels, a fluted column and two flaring gramophone horns
    turned to the sides, their throats glowing."""
    P = D.Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    P.add("Body", L.loft([(.22, .3, .44, 3.0), (.5, .32, .46, 3.0), (.56, .28, .42, 3.0), (.62, .22, .34, 3.0)]))  # cabinet
    P.add("Accent", L.loft([(.47, .335, .475, 3.0), (.52, .335, .475, 3.0)]))
    for x, y in ((-.43, -.3), (.43, -.3), (-.43, .3), (.43, .3)):
        P.add("Accent", L.rod((x * .6, y, .22), (x * 1.02, y, .22), .03))                         # axles
    P.add("Accent", L.steps([(.18, .05), (.13, .05)], loc=(0, .05, .6)))
    P.add("Body", L.fluted(.08, .68, 1.05, loc=(0, .05), n=8))
    P.add("Accent", L.steps([(.12, .04), (.15, .04)], loc=(0, .05, 1.04)))
    P.add("Accent", L.rod((-.14, .05, 1.17), (.14, .05, 1.17), .05))                              # horn yoke
    for sx in (-1, 1):
        ax = "X" if sx > 0 else "-X"
        P.add("Body", L.lathe([(0, 0), (.045, 0), (.05, .1), (.08, .2), (.15, .27), (.24, .3), (.26, .315), (.22, .32),
                               (.13, .29), (.06, .25), (0, .24)], axis=ax, loc=(sx * .1, .05, 1.17)))
        P.add("Eye", L.lathe([(0, 0), (.06, 0), (0, .03)], axis=ax, loc=(sx * .33, .05, 1.17)))  # glowing throat
    acc, glow = L.monocle(.07, (0, -.475, .4))
    P.add("Body", acc).add("Eye", glow)
    P.add("Body", L.finial((0, .05, 1.22), h=.12, r=.035))
    return P


def worker(L):
    """Clockwork bellhop trolley: a brass-rimmed cabinet deck, a drum snout with two monocle lamps, a fluted column for
    a mast on a stepped plinth, a clock dial under its stepped capital and a wind-up key in its back, a ball finial
    lamp on top; the tray rests on a brass bracket."""
    def deck(L, P):
        P.add("Body", L.loft([(.14, .25, .37, 3.0), (.28, .26, .38, 3.0), (.32, .24, .36, 3.0)]))
        P.add("Accent", L.loft([(.26, .35, .4, 3.0), (.31, .35, .4, 3.0)]))                          # brass rim
        for x, y, _ in D.WORKER_WHEELS:
            P.add("Accent", L.rod((x * .6, y, .12), (x * 1.02, y, .12), .028))
        for sy in (-1, 1):
            P.add("Accent", L.rod((-.3, sy * .39, .22), (.3, sy * .39, .22), .03))                   # brass bumper bars
            for sx in (-1, 1):
                P.add("Accent", L.ball(.04, (sx * .3, sy * .39, .22)))

    def pod(L, P):
        P.add("Body", L.hull((.46, .32, .3), (0, -.22, .47)))
        for sx in (-1, 1):
            acc, glow = L.monocle(.035, (sx * .15, -.345, .4))
            P.add("Accent", acc).add("Eye", glow)
        D.worker_shoulders(L, P, inner=.19)

    def mast(L, P):
        P.add("Accent", L.steps([(.2, .04), (.17, .04)], loc=(0, .22, .3)))                          # plinth
        P.add("Body", L.fluted(.12, .38, 1.12, loc=(0, .22), n=10))
        P.add("Accent", L.steps([(.15, .03), (.18, .04), (.2, .05)], loc=(0, .22, 1.12)))           # capital
        face, rim = L.dial(.08, (0, .095, .98))
        P.add("Body", face).add("Accent", rim)
        P.add("Accent", L.key((0, .33, .78), .8))

    def beacon(L, P):
        f = L.finial((0, .25, 1.24), h=.12, r=.045)
        P.add("Accent", f[:1]).add("Eye", f[1:])

    def plaque(L, P):
        # an enamel shop sign hung on the front bumper bar: a stepped brass frame with an arched crest, the warehouse
        # pictogram glowing on it — the bellhop's «luggage» badge
        def frame(w, h, d):
            out = [s.rbox((w + .04, d * .7, h + .04), (0, d * .4, 0), r=.008, seg=1),
                   s.rbox((w + .02, d * .9, h + .02), (0, d * .1, 0), r=.006, seg=1)]
            out.append(s.loft([(-d * .5, .07, .035, 2.0), (d * .9, .07, .035, 2.0)], seg=12, axis="Y",
                              loc=(0, 0, h / 2 + .008)))                                     # the arched crest
            out += [s.ball(.016, (sx * (w / 2 + .01), -d * .2, h / 2 + .01), seg=8) for sx in (-1, 1)]   # rivets
            return out
        D.worker_plaque(L, P, loc=(0, -.418, .245), w=.24, h=.15, tilt=30, plate="Body", frame=frame,
                        frame_role="Accent")

    return D.worker(L, deck=deck, pod=pod, mast=mast, beacon=beacon, extra=plaque)


def sweeper(L):
    """Dinner cloche: a wide, low polished cover on a stepped serving rim that hides the wheels, a ring-handle finial,
    a monocle peeping from the front, two big sunburst brushes sticking out at the front corners."""
    P = D.Parts()
    D.wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18), (0, -.3)):
        P.add("Accent", L.box((.08, .07, .14), (x, y, .2), r=.015))
        P.add("Accent", L.rod((x - .045, y, .08), (x + .045, y, .08), .02))
    P.add("Accent", L.steps([(.42, .05), (.4, .04)], loc=(0, 0, .2)))                              # serving rim
    P.add("Body", L.lathe([(.38, .28), (.38, .33), (.36, .44), (.3, .54), (.2, .61), (.08, .645), (0, .65)]))  # cloche
    P.add("Accent", L.loft([(.3, .385, .385, 2.0), (.34, .385, .385, 2.0)]))
    P.add("Accent", L.lathe([(.04, .64), (.05, .67), (.03, .69)]))
    P.add("Accent", L.lathe([(.035, -.03), (.09, -.025), (.09, .025), (.035, .03)], axis="Y", loc=(0, 0, .76)))  # ring handle
    P.add("Accent", L.rod((0, 0, .63), (0, 0, .68), .03))
    P.add("Accent", L.cyl(.06, .08, (0, 0, .24)))
    P.add("AccentLidar", L.steps([(.12, .035), (.1, .035)], loc=(0, 0, .185)) + L.box((.05, .05, .04), (0, -.12, .22), r=.01))
    acc, glow = L.monocle(.07, (.0, -.38, .4))
    P.add("Accent", acc).add("Eye", glow)
    for sx in (-1, 1):
        c = (sx * .36, -.3, .012)
        P.add("Accent", L.sunburst(c, .03, .2, 9, a0=0, a1=math.tau, width=.045, depth=.018, plane="XY"))
        P.add("Accent", L.steps([(.06, .02), (.04, .03)], loc=(c[0], c[1], .005)))
        P.add("Accent", L.rod((c[0], c[1], .03), (sx * .3, -.24, .24), .02))
    return P


def thumper(L):
    """Wind-up drum: a tall tin drum with brass hoops and a dial face rides on two stepped ziggurat treads, a huge
    wind-up key in its back; two fluted piston columns stamp the pads, a little lamp hangs between them."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .55
        P.add("Accent", [s.loft([(-.13, .18, .68, 6.0), (.13, .18, .68, 6.0)], seg=16, axis="X", loc=(x, 0, .18))])  # tread
        P.add("Accent", L.box((.22, 1.1, .14), (x, 0, .4), r=.025))
        P.add("Accent", L.box((.16, .8, .1), (x, 0, .51), r=.02))
        for y in (-.45, -.15, .15, .45):
            P.add("Accent", [s.rcyl(.11, .28, (x, y, .18), axis="X", seg=10, r=0)])
    P.add("Body", L.box((.9, .9, .16), (0, 0, .46), r=.04))                                          # deck
    P.add("Body", L.lathe([(0, .5), (.36, .5), (.38, .54), (.38, 1.1), (.36, 1.14), (0, 1.14)], loc=(0, .1, 0)))  # drum
    for z in (.56, 1.08):
        P.add("Accent", L.loft([(z - .03, .39, .39, 2.0), (z + .03, .39, .39, 2.0)], loc=(0, .1, 0)))
    for k in range(10):                                                                             # zig-zag drum cords
        a = k / 10 * math.tau
        p0 = Vector((math.cos(a) * .385, .1 + math.sin(a) * .385, .6))
        a1 = a + math.tau / 20
        p1 = Vector((math.cos(a1) * .385, .1 + math.sin(a1) * .385, 1.04))
        P.add("Accent", [align(s.rcyl(.014, (p1 - p0).length, seg=6, r=0), p0, p1)])
    P.add("Accent", L.steps([(.3, .05), (.2, .05)], loc=(0, .1, 1.13)))
    P.add("Accent", L.finial((0, .1, 1.22), h=.16, r=.05))
    face, rim = L.dial(.2, (0, -.28, .85))
    P.add("Body", face).add("Accent", rim)
    P.add("Accent", L.key((0, .47, .85), 2.2))
    P.add("Body", L.rod((-.36, -.2, .75), (.36, -.2, .75), .05))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", L.rod((x, -.2, .75), (x, -.85, .75), .05))
        P.add("Accent", L.steps([(.1, .04), (.08, .04)], loc=(x, -.85, .68)))
        P.add("Accent", L.fluted(.065, .1, .7, loc=(x, -.85), n=6))
        P.add("Accent", L.steps([(.17, .04), (.12, .04)], loc=(x, -.85, .04)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
    P.add("Body", L.rod((-.3, -.85, .36), (.3, -.85, .36), .03))
    acc, glow = L.monocle(.06, (0, -.9, .32))
    P.add("Accent", L.ball(.07, (0, -.85, .33))).add("Accent", acc).add("Eye", glow)
    return P


def sorter(L):
    """Sunburst mirror: a fluted column on a stepped base, a round mirror frame with the colour lens, a wide sunburst
    of brass rays around it and a finial on top."""
    P = D.Parts()
    P.add("Accent", L.steps([(.3, .04), (.25, .04)]))
    P.add("Body", L.steps([(.18, .05), (.13, .05)], loc=(0, 0, .08)))
    P.add("Body", L.fluted(.08, .17, .95, n=10))
    P.add("Body", L.steps([(.12, .04), (.16, .04)], loc=(0, 0, .94)))
    P.add("Body", L.lathe([(.1, 0), (.24, 0), (.26, .03), (.26, .1), (.22, .13), (0, .13)], axis="-Y", loc=(0, .1, 1.25)))
    P.add("Accent", L.lathe([(.2, 0), (.27, 0), (.29, .02), (.27, .045), (.2, .045)], axis="-Y", loc=(0, -.03, 1.25)))
    P.add("Eye", L.lathe([(.205, 0), (.2, .02), (.12, .045), (0, .055)], axis="-Y", loc=(0, -.035, 1.25)))
    P.add("Accent", L.sunburst((0, .03, 1.25), .24, .46, 16, a0=0, a1=math.tau, width=.06, depth=.04))
    P.add("Accent", L.finial((0, .03, 1.5), h=.14, r=.04))
    P.add("Body", L.rod((0, .03, .99), (0, .05, 1.14), .05))
    return P


def drone(L):
    """Flying sunburst brooch: an eight-pointed star — four long points reaching the brass-rimmed pinwheels, four short
    ones between — round a stepped jewel, a pendant camera drop under the front."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", [s.lathe([(0, -.06), (.16, -.06), (.19, -.035), (.19, .0), (.13, .035), (.07, .06), (0, .07)], seg=8)])
    P.add("Accent", [s.lathe([(.17, -.025), (.205, -.02), (.205, .005), (.17, .01)], seg=8)])
    P.add("Accent", L.finial((0, 0, .055), h=.06, r=.03))
    for k in range(8):
        a = math.pi / 4 + k / 8 * math.tau
        long = k % 2 == 0
        n = .5 if long else .3
        pt = s.loft([(0, .075 if long else .06, .025, 2.0), (n * .5, .045, .02, 2.0), (n, .0, .0)], seg=8, axis="X")
        s.place(pt, rot=(0, 0, a))
        P.add("Body" if long else "Accent", [s.place(pt, (math.cos(a) * .12, math.sin(a) * .12, -.005))])
    for i, (x, y) in enumerate(rot):
        P.add("Body", L.cyl(.035, .07, (x, y, .02)))
        P.add("Accent", L.lathe([(.19, -.012), (.205, -.008), (.205, .008), (.19, .012), (.18, 0)], loc=(x, y, .06)))  # rim
        for k in range(2):
            a = k * math.pi + math.atan2(y, x) + math.pi / 2
            d = Vector((math.cos(a), math.sin(a), 0))
            P.add("Body", [align(s.rbox((.016, .016, .07), r=.004, seg=1), Vector((x, y, .015)) + d * .19, Vector((x, y, .065)) + d * .19)])
            P.add("Body", [align(s.rbox((.016, .016, .19), r=.004, seg=1), Vector((x, y, .015)), Vector((x, y, .015)) + d * .19)])
        c = Vector((x, y, .065))
        blades = [s.rcyl(.03, .05, c, seg=12, r=.006)]
        for k in range(4):
            a = k / 4 * math.tau
            b = s.loft([(0, .025, .006), (.07, .055, .008), (.16, .03, .005)], seg=10, axis="X")
            s.place(b, rot=(math.radians(20), 0, a))
            blades.append(s.place(b, c))
        P.add(f"AccentRotor{i}", blades)
    P.add("Body", L.rod((0, -.12, -.04), (0, -.22, -.08), .03))
    acc, glow = L.monocle(.05, (0, -.25, -.1))
    P.add("Accent", L.ball(.06, (0, -.235, -.1))).add("Accent", acc).add("Eye", glow)
    return P


def turret(L):
    """Blunderbuss on a column: a stepped plinth, a fluted column with a glowing ring under its capital, and a round
    drum head with a bell-mouthed barrel and a little finial."""
    P = D.Parts()
    P.add("Body", L.steps([(.3, .06), (.24, .06), (.18, .06)]))
    P.add("Body", L.fluted(.1, .17, .98, n=10))
    P.add("Body", L.steps([(.13, .04), (.16, .05)], loc=(0, 0, .97)))
    P.add("Eye", L.loft([(1.02, .14, .14, 2.0), (1.04, .14, .14, 2.0)]))
    P.add("Eye", [torus_x(.245, .014, (0, 0, .118), seg=32, seg_r=6, axis_z=True)])
    P.add("Body", L.cyl(.06, .12, (0, 0, 1.08)))
    P.add("AccentHead", L.lathe([(0, 1.13), (.15, 1.13), (.17, 1.17), (.17, 1.33), (.14, 1.37), (0, 1.37)]))
    P.add("AccentHead", L.loft([(1.24, .18, .18, 2.0), (1.28, .18, .18, 2.0)]))
    P.add("AccentHead", L.lathe([(0, 0), (.06, 0), (.055, .2), (.06, .3), (.1, .38), (.13, .42), (.11, .43), (.06, .4), (0, .39)],
                                axis="-Y", loc=(0, -.14, 1.24)))
    P.add("AccentHead", L.finial((0, 0, 1.36), h=.06, r=.03))
    P.add("AccentHead", L.box((.08, .3, .06), (0, .15, 1.24), r=.02))
    return P


def vacuum(L):
    """Ceiling clock: a stepped plaster medallion edged with a sunburst of short rays sticking out past the rim; the room
    side (-z) is a clock dial with hour studs and two thick hands round a glowing pin."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, .1), (.3, .1), (.33, .08), (.33, .03), (.31, .0), (0, .0)], seg=L.seg)])
    P.add("Accent", L.sunburst((0, 0, .05), .28, .45, 20, a0=0, a1=math.tau, width=.06, depth=.04, plane="XY"))
    P.add("Accent", [s.lathe([(0, -.012), (.29, -.012), (.3, .0), (.3, .01), (0, .01)], seg=L.seg)])          # brass rim plate
    P.add("Body", [s.lathe([(0, -.03), (.25, -.03), (.26, -.015), (0, -.01)], seg=L.seg)])                   # dial
    for k in range(12):
        a = k / 12 * math.tau
        big = k % 3 == 0
        P.add("Accent", [s.rbox((.025 if big else .016, .07 if big else .045, .02), (math.cos(a) * .2, math.sin(a) * .2, -.035),
                                r=.004, seg=1, rot=(0, 0, a + math.pi / 2))])
    P.add("Accent", [align(s.rbox((.035, .018, .17), r=.006, seg=1), Vector((0, 0, -.04)), Vector((.02, -.17, -.04)))])
    P.add("Accent", [align(s.rbox((.03, .018, .12), r=.006, seg=1), Vector((0, 0, -.045)), Vector((.12, .03, -.045)))])
    P.add("Eye", [s.lathe([(0, -.06), (.035, -.055), (.04, -.04), (0, -.03)], seg=16)])
    P.add("Eye", L.box((.12, .03, .012), (0, -.27, -.012), r=.005))
    return P


def pir(L):
    """A round plaster rosette on the wall with a stepped brass eye."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.08, 0), (.08, .02), (.06, .04), (0, .045)], seg=L.seg, axis="-Y", loc=(0, .08, 0))])
    P.add("Body", L.sunburst((0, .05, 0), .03, .09, 8, a0=0, a1=math.tau, width=.025, depth=.03))
    P.add("Eye", [s.lathe([(.05, 0), (.048, .02), (.03, .04), (0, .046)], seg=12, axis="-Y", loc=(0, .035, 0))])
    return P


def lidar(L):
    P = D.Parts()
    P.add("Body", L.steps([(.18, .04), (.14, .04), (.1, .04)]))
    P.add("Body", L.fluted(.05, .12, .4, n=6))
    P.add("Eye", L.loft([(.38, .075, .075, 2.0), (.4, .075, .075, 2.0)]))
    P.add("AccentHead", L.steps([(.08, .02), (.1, .08), (.07, .02)], loc=(0, 0, .395)))
    P.add("AccentHead", L.finial((0, 0, .51), h=.04, r=.018))
    P.add("AccentHead", L.box((.06, .05, .05), (0, -.1, .455), r=.012))
    return P


def security_camera(L):
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.11, 0), (.11, .02), (.08, .04), (0, .04)], axis="-Y", loc=(0, .3, 0)))
    P.add("Accent", L.rod((0, .27, 0), (0, .1, -.02), .025))
    P.add("Accent", L.lathe([(0, 0), (.08, 0), (.09, .04), (.09, .26), (.1, .3), (0, .3)], axis="-Y", loc=(0, .1, -.02)))
    acc, glow = L.monocle(.05, (0, -.205, -.02))
    P.add("Accent", acc).add("Eye", glow)
    P.add("Accent", L.finial((0, -.05, .07), h=.06, r=.02))
    return P


def laser_emitter(L):
    P = D.Parts()
    P.add("Body", L.steps([(.08, .05), (.065, .05)]))
    P.add("Body", L.box((.1, .1, 1.45), (0, 0, .82), r=.02))
    P.add("Body", L.steps([(.065, .03), (.08, .03)], loc=(0, 0, 1.52)))
    P.add("Body", L.finial((0, 0, 1.58), h=.08, r=.03))
    P.add("Eye", L.box((.05, .04, 1.4), (0, -.055, .85), r=.012))
    return P


def plate(L):
    P = D.plate(L)
    P.add("Accent", L.sunburst((0, 0, .013), .06, .3, 8, a0=0, a1=math.tau, width=.05, depth=.008, plane="XY"))
    return P


def attach_eye(L):
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.1, 0), (.11, .03), (.11, .12), (.09, .14), (0, .14)], axis="-Y", loc=(0, .07, 0)))
    acc, glow = L.monocle(.055, (0, -.07, 0))
    P.add("Accent", acc).add("Eye", glow)
    return P


# =========================================================================== furniture and level pieces
# A picture-book flat furnished in art deco: everything stands on stepped ziggurat plinths, legs are turned balusters,
# tops are stepped, backs are fans, round things are urns and drums, and a sunburst or a finial crowns the big pieces.

def zig(L, w, d, h, loc, n=3, k=.04):
    """A ziggurat block: n tiers on the floor at loc (centre of the footprint), each tier k narrower on every side."""
    x, y, z = loc
    out = []
    for i in range(n):
        th = h / n
        out.append(s.rbox((w - 2 * k * i, d - 2 * k * i, th + (.01 if i < n - 1 else 0)), (x, y, z + th * i + th / 2 + (.005 if i < n - 1 else 0)),
                          r=.008, seg=1))
    return out


def half_disc(r, depth, base, seg=20):
    """An upright half disc (flat side down on base = the middle of its bottom edge), depth along Y."""
    bm = bmesh.new()
    x0, y0, z0 = base
    front, back = [], []
    for i in range(seg + 1):
        a = math.pi * i / seg
        x, z = x0 + r * math.cos(a), z0 + r * math.sin(a)
        front.append(bm.verts.new((x, y0 - depth / 2, z)))
        back.append(bm.verts.new((x, y0 + depth / 2, z)))
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    for i in range(seg):
        bm.faces.new((front[i], back[i], back[i + 1], front[i + 1]))
    bm.faces.new((front[seg], back[seg], back[0], front[0]))  # the flat bottom
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def fan(L, c, r, n=7, width=.08, depth=.03, a0=0.0, a1=math.pi):
    """A shell fan: a half disc of tapered rays round a hub (a chair back, a crest)."""
    return L.sunburst(c, r * .15, r, n, a0=a0, a1=a1, width=width, depth=depth) + [s.rcyl(r * .2, depth * 1.2, c, axis="Y", seg=12, r=.004)]


def p_desk(L):
    """Deco desk: a stepped top on two ziggurat drawer towers, a sunburst on the modesty panel, a round-cornered monitor."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    for x in (-.6, .6):
        P.add("Accent", zig(L, .38, .74, .72, (x, 0, 0), n=3, k=.025))
        for z, y in ((.36, -.347), (.6, -.322)):
            P.add("Accent", L.handle((x, y, z)))
    P.add("Accent", L.slab((.84, .025, .4), (0, .33, .52)) + L.sunburst((0, .31, .32), .04, .2, 7, width=.05, depth=.02))
    acc, glow = L.screen((.56, .34), (0, .17, .98), "-Y")
    P.add("Accent", acc + L.steps([(.12, .02), (.06, .03)], loc=(0, .2, .765)) + L.fluted(.03, .8, .84, loc=(0, .2), n=5))
    P.add("Eye", glow)
    return P


def p_terminal(L):
    """Deco console: the stepped desk with three screens fanned on arched frames, a keyboard tray, a drum computer."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", zig(L, .38, .74, .72, (-.6, 0, 0), n=3, k=.025) + zig(L, .38, .74, .72, (.6, 0, 0), n=3, k=.025))
    P.add("Accent", L.slab((.84, .025, .4), (0, .33, .52)))
    P.add("AccentConsole", L.slab((.52, .17, .025), (0, -.1, .778)))
    for x, y, a in ((-.33, .14, .35), (0, .19, 0), (.33, .14, -.35)):
        acc, glow = L.screen((.29, .22), (x, y, .99), "-Y")
        for bm in acc + glow:
            bmesh.ops.rotate(bm, cent=Vector((x, y, .99)), matrix=Matrix.Rotation(a, 3, "Z"), verts=bm.verts)
        P.add("Eye", glow).add("AccentConsole", acc + L.steps([(.07, .02), (.04, .1)], loc=(x, y + .03, .765)))
    return P


def p_chair(L):
    """Fan-back side chair: a round stepped seat on turned legs, two turned back posts holding a shell fan."""
    P = D.Parts()
    P.add("Body", L.steps([(.29, .04), (.27, .04)], loc=(0, 0, .42)))
    P.add("Body", fan(L, (0, .24, .72), .3, n=7, width=.09, depth=.04, a0=math.radians(5), a1=math.radians(175)))
    P.add("Body", L.slab((.56, .05, .1), (0, .24, .7)))
    for sx in (-1, 1):
        P.add("Accent", L.leg((sx * .15, .22, .74), .28, .045))
        for sy in (-1, 1):
            P.add("Accent", L.leg((sx * .18, sy * .18, .43), .43, .05))
    P.add("Accent", L.finial((0, .24, .99), h=.08, r=.022))
    return P


def p_server_rack(L):
    """Skyscraper cabinet: a tall case with setback tiers on top, fluted corner pilasters, glowing rows in an arched
    frame, a sunburst in the arch."""
    P = D.Parts()
    P.add("Body", zig(L, .7, 1.0, .08, (0, 0, 0), n=2, k=.01))
    P.add("Body", L.slab((.66, .96, 1.62), (0, 0, .89)))
    P.add("Body", zig(L, .66, .96, .3, (0, 0, 1.7), n=3, k=.06))
    for sx in (-1, 1):
        P.add("Body", L.fluted(.04, .1, 1.7, loc=(sx * .3, -.47), n=6))
    P.add("Body", [s.rcyl(.24, .03, (0, -.48, 1.5), axis="Y", seg=16, r=.004)])
    P.add("Body", L.sunburst((0, -.5, 1.5), .05, .22, 9, a0=0, a1=math.pi, width=.04, depth=.02))
    for i in range(9):
        P.add("Eye", L.slab((.48, .02, .05), (0, -.486, .25 + i * .14)))
    return P


def p_cabinet(L):
    """Ziggurat cabinet: three stepped drawers that narrow upwards, round knobs, a fan crest on the top tier."""
    P = D.Parts()
    P.add("Body", zig(L, .9, .5, .08, (0, 0, 0), n=2, k=.015))
    for i, (w, h) in enumerate(((.9, .48), (.82, .42), (.72, .32))):
        z0 = .08 + sum(hh for _, hh in ((.9, .48), (.82, .42), (.72, .32))[:i])
        P.add("Body", L.slab((w, .48, h + .01), (0, 0, z0 + h / 2)))
        P.add("Body", L.door((w - .06, h - .08), (0, -.24, z0 + h / 2)))
        P.add("Accent", L.handle((0, -.252, z0 + h / 2)))
    P.add("Accent", fan(L, (0, 0, 1.3), .1, n=5, width=.04, depth=.03))
    return P


def p_meeting_table(L):
    """A long stepped-edge table on two ziggurat pedestals joined by a fluted stretcher."""
    P = D.Parts()
    P.add("Body", L.slab((3.2, 1.3, .06), (0, 0, .75)))
    for x in (-1.0, 1.0):
        P.add("Accent", zig(L, .5, .7, .2, (x, 0, 0), n=2, k=.05) + L.fluted(.09, .19, .73, loc=(x, 0), n=8))
    P.add("Accent", L.rod((-1.0, 0, .2), (1.0, 0, .2), .03))
    return P


def p_plant(L):
    """Topiary: three clipped balls on a stem in a deco urn, a ball finial on top."""
    P = D.Parts()
    P.add("Accent", L.pot(.22, .42, (0, 0, 0)))
    P.add("Body", L.rod((0, 0, .38), (0, 0, 1.3), .025))
    for z, r in ((.72, .33), (1.08, .2), (1.31, .09)):
        P.add("Body", [s.ball(r, (0, 0, z), seg=14)])
    return P


def p_kitchen_counter(L):
    """Deco counter: stepped plinth and top, fielded doors with knobs, a radio-shaped microwave with an arched window."""
    P = D.Parts()
    P.add("Body", L.carcass((2.4, .65, .88), (0, 0, 0)))
    for i in range(4):
        x = -.9 + i * .6
        P.add("Body", L.door((.56, .66), (x, -.33, .48)))
        P.add("Accent", L.handle((x + .2, -.338, .7)))
    P.add("Accent", L.slab((2.44, .7, .05), (0, 0, .905)))
    P.add("Body", [s.rbox((.5, .42, .14), (.7, .02, 1.0), r=.03, seg=1)] + [s.rcyl(.25, .42, (.7, .02, 1.0), axis="Y", seg=16, r=.02)])
    P.add("Eye", [s.rcyl(.13, .02, (.7, -.2, 1.03), axis="Y", seg=16, r=.004)])
    return P


def p_shelf(L):
    """Etagere: four fluted columns with ball finials, stepped shelves, hat boxes and parcels."""
    P = D.Parts()
    for x in (-.94, .94):
        for y in (-.25, .25):
            P.add("Body", L.leg((x, y, 1.93), 1.93, .07) + L.finial((x, y, 1.91), h=.07, r=.025))
    for h in (.08, .48, 1.06, 1.64):
        P.add("Body", L.slab((2.0, .58, .04), (0, 0, h)))
    for x, h, r in ((-.6, .5, .17), (.45, .5, .15), (.3, 1.08, .2), (-.55, 1.66, .16)):
        P.add("Accent", [s.rcyl(r, .26, (x, 0, h + .13), seg=10, r=0), s.rcyl(r + .01, .04, (x, 0, h + .25), seg=10, r=0)])
    return P


def p_boxes(L):
    """A stack of striped hat boxes with stepped lids."""
    P = D.Parts()
    for (x, y, z, r, h) in ((0, 0, 0, .29, .48), (.05, -.04, .48, .24, .4), (.55, .1, 0, .21, .38)):
        P.add("Accent", [s.rcyl(r, h - .04, (x, y, z + (h - .04) / 2), seg=16, r=.008),
                         s.rcyl(r + .01, .05, (x, y, z + h - .03), seg=16, r=.008), s.rcyl(r * .5, .03, (x, y, z + h), seg=12, r=.006)])
        # the stripes: two raised bands round the box below its lid (one part, as the original: the cardboard's colour)
        P.add("Accent", [s.rcyl(r + .004, .03, (x, y, z + bz), seg=16, r=.004) for bz in (.1, (h - .04) * .62)])
    return P


def p_water_cooler(L):
    """A fluted column fountain on a stepped base, the bottle on a stepped capital."""
    P = D.Parts()
    P.add("Body", L.steps([(.17, .05), (.14, .05)]) + L.fluted(.11, .1, .92, n=8) + L.steps([(.13, .04), (.16, .05)], loc=(0, 0, .91)))
    P.add("Body", L.rod((0, -.1, .8), (0, -.16, .78), .015))
    P.add("Eye", L.lathe(props.COOLER_BOTTLE, loc=(0, 0, .99)))
    return P


def p_whiteboard(L):
    """A chalkboard with a low arched top in a frame on two fluted posts with finials."""
    P = D.Parts()
    arch = s.rcyl(.85, .03, (0, 0, 0), axis="Y", seg=24, r=0)
    s.place(arch, (0, 0, 1.72), scale=None)
    for v in arch.verts:
        v.co.z = 1.72 + (v.co.z - 1.72) * .3
    P.add("Body", L.slab((1.7, .03, .76), (0, 0, 1.36)) + [arch])
    for x in (-.89, .89):
        P.add("Accent", L.fluted(.035, .04, 1.9, loc=(x, 0), n=5) + L.finial((x, 0, 1.88), h=.07, r=.028))
        P.add("Accent", L.slab((.12, .07, .04), (x, 0, .02)))
    P.add("Accent", L.slab((1.78, .06, .04), (0, 0, .98)))
    return P


def p_sofa(L):
    """Deco sofa: round bolster arms, a seat on a stepped plinth, a back with a three-lobed scalloped top."""
    P = D.Parts()
    P.add("Body", zig(L, 1.84, .76, .2, (0, 0, 0), n=2, k=.03))
    P.add("Body", L.cushion((.88, .66, .16), (-.45, -.04, .28)) + L.cushion((.88, .66, .16), (.45, -.04, .28)))
    P.add("Body", L.cushion((1.8, .2, .46), (0, .32, .52)))
    for x in (-.55, 0, .55):
        P.add("Body", [s.rcyl(.22, .2, (x, .32, .66), axis="Y", seg=16, r=.03)])
    for x in (-1.0, 1.0):
        P.add("Accent", [s.rcyl(.19, .82, (x * .9, 0, .45), axis="Y", seg=16, r=.05)])
    return P


def p_toilet_stall(L):
    """Cubicle with stepped crests on the walls and fluted posts at the front corners."""
    P = props.toilet_stall(L)
    for x in (-.5, .5):
        P.add("Body", L.fluted(.035, .0, 1.94, loc=(x, -.72), n=5))
        P.add("Body", zig(L, .06, 1.5, .06, (x, 0, 1.94), n=2, k=.005))
    P.add("Accent", L.sunburst((0, .72, 1.7), .05, .3, 9, a0=0, a1=math.pi, width=.06, depth=.02))
    return P


def p_partition(L):
    """A three-leaf folding screen with arched tops and sunbursts, hinged on fluted posts."""
    P = D.Parts()
    for x in (-.53, 0, .53):
        P.add("Body", [s.rbox((.5, .03, .9), (x, 0, .5), r=.006, seg=1), s.rcyl(.25, .03, (x, 0, .95), axis="Y", seg=16, r=0)])
        P.add("Accent", L.sunburst((x, -.016, .95), .04, .22, 7, width=.04, depth=.01))
    for x in (-.79, -.265, .265, .79):
        P.add("Accent", L.rod((x, 0, .02), (x, 0, 1.2), .022) + L.finial((x, 0, 1.19), h=.05, r=.02))
    return P


def p_bookshelf(L):
    """Bookcase: fluted sides, an arched crest with a sunburst, rows of books."""
    P = D.Parts()
    for x in (-.47, .47):
        P.add("Body", L.fluted(.045, .0, 1.6, loc=(x, 0), n=4) + L.finial((x, 0, 1.58), h=.1, r=.035))
    for h in (.03, .52, 1.04, 1.52):
        P.add("Body", L.slab((.98, .34, .04), (0, 0, h)))
    # the crest: a half disc standing on the top, its sunburst fanning upwards — above the books, not over them
    P.add("Body", L.slab((.92, .02, 1.5), (0, .16, .78)) + [half_disc(.42, .26, (0, .02, 1.54))])
    P.add("Accent", L.sunburst((0, -.12, 1.56), .05, .37, 9, a0=0, a1=math.pi, width=.05, depth=.02))
    for i, h in enumerate((.05, .54, 1.06)):
        x = -.42
        for k in range(6 + i % 2):
            w = .07 + .03 * ((k + i) % 3)
            hh = .34 - .05 * ((k * 7 + i) % 3)
            P.add("Accent", L.slab((w, .24, hh), (x + w / 2, .02, h + hh / 2)))
            x += w + .012
    return P


def p_fridge(L):
    """Deco icebox: rounded shoulders, a big chrome lever, a sunburst badge, on a stepped plinth."""
    P = D.Parts()
    P.add("Body", zig(L, .7, .7, .06, (0, 0, 0), n=2, k=.02))
    P.add("Body", [s.rbox((.7, .7, 1.75), (0, 0, .925), r=.12, seg=3)])
    P.add("Body", L.door((.64, .44), (0, -.355, 1.45)) + L.door((.64, 1.0), (0, -.355, .68)))
    P.add("Accent", [s.rbox((.06, .05, .3), (.24, -.39, 1.0), r=.02, seg=1), s.rbox((.05, .05, .16), (.24, -.39, 1.45), r=.02, seg=1)])
    # the badge sits on the upper door, clear of the seam and of the lever
    P.add("Accent", L.sunburst((-.05, -.381, 1.45), .025, .085, 9, a0=0, a1=math.tau, width=.022, depth=.012))
    return P


def p_coffee_machine(L):
    """Samovar urn: a stepped base, a fat urn with a tap and a domed lid with a finial, a glowing gauge."""
    P = D.Parts()
    P.add("Accent", L.steps([(.16, .03), (.12, .03)]))
    P.add("Body", L.lathe([(0, .06), (.08, .06), (.14, .15), (.15, .26), (.12, .36), (.13, .38), (0, .38)]))
    P.add("Accent", L.lathe([(.12, .38), (.13, .38), (.1, .44), (0, .46)]) + L.finial((0, 0, .45), h=.03, r=.02))
    P.add("Accent", L.rod((0, -.12, .17), (0, -.22, .16), .02) + L.rod((0, -.22, .17), (0, -.22, .12), .015))
    P.add("Accent", [s.tube([(0, .12, .32), (0, .19, .27), (0, .13, .14)], .015, res=1, segments_per=3)])
    P.add("Eye", [s.rcyl(.04, .02, (0, -.15, .3), axis="Y", seg=12, r=.004)])
    return P


def p_round_table(L):
    """Round table: a stepped top on a fluted column and a stepped round foot."""
    P = D.Parts()
    P.add("Body", L.steps([(.48, .025), (.5, .025)], loc=(0, 0, .71)))
    P.add("Accent", L.steps([(.27, .03), (.2, .03), (.12, .03)]) + L.fluted(.06, .09, .72, n=8))
    return P


def p_armchair(L):
    """Tub chair: a drum seat on a stepped plinth, the back three stepped curved rolls round the rear half, a sunburst
    on the front of the drum."""
    P = D.Parts()
    P.add("Body", L.steps([(.37, .04), (.35, .36)], loc=(0, -.02, 0)))
    P.add("Body", L.cushion((.6, .56, .12), (0, -.04, .44)))
    for k, (z, r, rr) in enumerate(((.44, .33, .065), (.535, .33, .065), (.63, .325, .062), (.725, .32, .06), (.815, .315, .058))):
        pts = [(math.cos(a) * (r + .05), -.02 + math.sin(a) * r, z) for a in [math.radians(d) for d in range(-10, 191, 25)]]
        P.add("Accent", [s.tube(pts, rr, res=1, segments_per=3)])
    P.add("Body", L.sunburst((0, -.375, .2), .02, .16, 7, width=.05, depth=.02))
    return P


def p_coffee_table(L):
    """A two-tier ziggurat low table."""
    P = D.Parts()
    P.add("Body", L.slab((1.0, .55, .04), (0, 0, .4)))
    P.add("Accent", zig(L, .8, .4, .38, (0, 0, 0), n=3, k=.06))
    return P


def p_tv_stand(L):
    """Radio-cabinet TV: a stepped cabinet, the screen in an arched frame with a sunburst crest."""
    P = D.Parts()
    P.add("Body", L.carcass((1.4, .4, .5), (0, 0, 0), kick=.04))
    acc, glow = L.screen((1.2, .7), (0, .06, .98), "-Y")
    P.add("Accent", acc + [s.rcyl(.4, .04, (0, .07, 1.06), axis="Y", seg=20, r=0)] + L.steps([(.14, .04), (.08, .1)], loc=(0, .06, .5)))
    P.add("Accent", L.sunburst((0, .04, 1.33), .03, .12, 7, width=.035, depth=.015))
    P.add("Eye", glow)
    return P


def p_reception_desk(L):
    """Deco front counter: a stepped band front with fluted pilasters and a big sunburst in the middle."""
    P = props.reception_desk(L)
    for x in (-1.15, -.4, .4, 1.15):
        P.add("Body", L.fluted(.04, .02, 1.08, loc=(x, -.4), n=5))
    P.add("Accent", L.sunburst((0, -.41, .55), .05, .4, 11, a0=0, a1=math.pi, width=.07, depth=.02))
    return P


def p_trash_bin(L):
    """A fluted urn bin with a stepped rim."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.13, 0), (.15, .04), (.12, .08), (.17, .38), (.17, .4), (0, .4)]))
    P.add("Accent", L.steps([(.19, .02), (.17, .02)], loc=(0, 0, .4)))
    return P


def p_coat_rack(L):
    """Hat stand: a turned pole on a stepped foot, ball-tipped hooks, a finial, two coats."""
    P = D.Parts()
    P.add("Accent", L.steps([(.24, .03), (.17, .03), (.1, .03)]) + L.leg((0, 0, 1.66), 1.58, .07) + L.finial((0, 0, 1.64), h=.1, r=.04))
    for a in (0.0, 2.1, 4.2):
        P.add("Accent", L.rod((0, 0, 1.58), (math.cos(a) * .2, math.sin(a) * .2, 1.66), .014) + [s.ball(.025, (math.cos(a) * .21, math.sin(a) * .21, 1.67), seg=8)])
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.3)) + L.cushion((.3, .12, .55), (-.08, .1, 1.32), rot=(0, 0, 2.1)))
    return P


def p_lockers(L):
    """Three tall lockers with arched tops and sunburst vents, round knobs, on a stepped plinth."""
    P = D.Parts()
    P.add("Body", zig(L, .9, .5, .08, (0, 0, 0), n=2, k=.015) + L.slab((.9, .48, 1.62), (0, 0, .89)))
    for x in (-.3, 0, .3):
        P.add("Body", [s.rcyl(.15, .46, (x, 0, 1.73), axis="Y", seg=14, r=.01)])
        P.add("Body", L.door((.27, 1.5), (x, -.245, .9)))
        P.add("Accent", L.handle((x + .09, -.26, 1.0)) + L.sunburst((x, -.25, 1.48), .02, .1, 5, width=.025, depth=.012))
    return P


def p_pallet(L):
    """A stepped wooden stage carrying a pile of toy drums with stepped rims."""
    P = D.Parts()
    P.add("Accent", zig(L, 1.2, 1.0, .14, (0, 0, 0), n=2, k=.04))
    for x, y, z, r, h in ((-.3, -.24, .14, .22, .38), (.3, -.24, .14, .22, .38), (-.3, .24, .14, .22, .38), (.3, .22, .14, .24, .4),
                          (-.28, 0, .52, .24, .4)):
        P.add("Body", [s.rcyl(r, h, (x, y, z + h / 2), seg=16, r=.01)] + [s.rcyl(r + .015, .04, (x, y, z + h - .03 - k * (h - .08)), seg=16, r=.008)
                                                                     for k in (0, 1)])
    return P


def p_wash_basin(L):
    """Deco vanity: shell basins on a stepped counter, the mirror an arch with a sunburst frame."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .5, .06), (0, 0, .85)) + L.carcass((1.6, .45, .82), (0, .02, 0)) + L.slab((1.6, .03, .34), (0, .24, 1.02)))
    for x in (-.4, .4):
        P.add("Accent", L.lathe([(0, 0), (.12, 0), (.17, .07), (.17, .09), (0, .09)], loc=(x, -.02, .82)))
        P.add("Accent", L.rod((x, .17, .88), (x, .17, 1.0), .02) + L.rod((x, .17, 1.0), (x, .08, .99), .015))
    mirror = s.rcyl(.6, .02, (0, 0, 0), axis="Y", seg=24, r=0)
    for v in mirror.verts:
        v.co.z *= .45
    s.place(mirror, (0, .235, 1.66))
    P.add("Eye", L.slab((1.2, .02, .5), (0, .235, 1.41)) + [mirror])
    for x in (-.66, .66):
        P.add("Accent", L.fluted(.035, 1.08, 1.86, loc=(x, .23), n=5) + L.finial((x, .23, 1.84), h=.06, r=.025))
    return P


def p_vending_machine(L):
    """Automat: a cabinet with a stepped ziggurat crown, an arched glowing window, a sunburst over it, a coin plate."""
    P = D.Parts()
    P.add("Body", zig(L, .9, .8, .06, (0, 0, 0), n=2, k=.015) + L.slab((.9, .8, 1.66), (0, 0, .89)))
    P.add("Body", zig(L, .9, .8, .18, (0, 0, 1.72), n=3, k=.08))
    P.add("Eye", L.slab((.56, .02, .8), (-.1, -.405, 1.0)) + [s.rcyl(.28, .02, (-.1, -.405, 1.4), axis="Y", seg=16, r=0)])
    P.add("Accent", L.sunburst((-.1, -.41, 1.4), .29, .36, 11, a0=0, a1=math.pi, width=.04, depth=.02))
    P.add("Accent", L.slab((.6, .03, .15), (-.1, -.41, .3)) + L.slab((.14, .03, .5), (.33, -.41, 1.1)))
    return P


def p_ping_pong_table(L):
    """Table-tennis table on four turned legs with a stepped apron."""
    P = D.Parts()
    P.add("Body", L.slab((2.74, 1.52, .04), (0, 0, .74)))
    P.add("Body", L.slab((2.5, 1.3, .06), (0, 0, .7)))
    P.add("Accent", L.four_legs(.72, 1.25, .7, .72, .13, inset=.2))
    P.add("Eye", L.slab((.02, 1.72, .15), (0, 0, .82)))
    return P


def p_bean_bag(L):
    """A stepped round pouf: three tufted tiers."""
    P = D.Parts()
    for z, r, h in ((0, .45, .3), (.28, .4, .24), (.5, .3, .19)):
        P.add("Body", [s.loft([(0, r * .95, r * .95, 2.0), (h * .5, r, r, 2.0), (h, r * .9, r * .9, 2.0)], seg=20, loc=(0, 0, z), round_caps=.04)])
    return P


def p_arcade_machine(L):
    """Picture-book arcade: an arched cabinet like a radio, a sunburst over the screen, a coin door with a dial."""
    P = D.Parts()
    P.add("Body", zig(L, .72, .8, .06, (0, 0, 0), n=2, k=.015) + L.slab((.7, .78, 1.42), (0, 0, .76)))
    P.add("Body", [s.rcyl(.35, .78, (0, 0, 1.46), axis="Y", seg=20, r=.02)])
    P.add("Eye", L.slab((.48, .02, .38), (0, -.4, 1.28), rot=(math.radians(-6), 0, 0)))
    P.add("Accent", L.sunburst((0, -.4, 1.5), .06, .3, 9, a0=math.radians(15), a1=math.radians(165), width=.045, depth=.02))
    P.add("Accent", L.slab((.7, .16, .05), (0, -.46, .98), rot=(math.radians(-10), 0, 0)))
    for x in (-.12, .12):
        P.add("Accent", [s.ball(.03, (x, -.5, 1.03), seg=8)])
    P.add("Accent", L.door((.3, .34), (0, -.4, .4)))
    acc, glow = L.monocle(.04, (0, -.43, .45))
    P.add("Accent", acc).add("Eye", glow)
    return P


def p_safe(L):
    """Vault: a stepped-plinth safe with a round stepped door, a spoked wheel and a dial with a monocle lamp."""
    P = D.Parts()
    P.add("Body", zig(L, .8, .7, .06, (0, 0, 0), n=2, k=.015) + L.slab((.8, .68, .94), (0, 0, .53)))
    P.add("Body", L.lathe([(0, 0), (.34, 0), (.36, .015), (.3, .03), (.3, .045), (0, .05)], axis="-Y", loc=(0, -.335, .52)))
    c = Vector((0, -.4, .52))
    ring = torus_x(.16, .016, (0, 0, 0), seg=20, seg_r=6)
    bmesh.ops.rotate(ring, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.pi / 2, 3, "Z"), verts=ring.verts)
    P.add("Accent", [s.rcyl(.05, .04, c, axis="Y", seg=12, r=.008), s.place(ring, (0, -.42, .52))])
    for k in range(6):
        a = k / 6 * math.tau
        P.add("Accent", [align(s.rcyl(.012, .16, seg=6, r=0), c + Vector((0, -.02, 0)), c + Vector((math.cos(a) * .16, -.02, math.sin(a) * .16)))])
    acc, glow = L.monocle(.03, (.28, -.345, .85))
    P.add("Accent", acc).add("Eye", glow)
    return P


def p_floor_lamp(L):
    """Fluted column lamp: a stepped foot, a fluted shaft, a stepped shade round the bulb, a finial."""
    P = D.Parts()
    P.add("Body", L.steps([(.18, .03), (.12, .03), (.07, .03)]) + L.fluted(.025, .09, 1.3, n=5) + L.rod((0, 0, 1.28), (0, 0, 1.55), .012))
    P.add("Accent", L.shade(.2, .12, .25, (0, 0, 1.3)) + L.finial((0, 0, 1.55), h=.08, r=.025))
    P.add("Eye", [s.ball(.05, (0, 0, 1.36), seg=12)])
    return P


def p_desk_lamp(L):
    """A deco torch lamp: a stepped foot, a fluted stem, a stepped shade."""
    P = D.Parts()
    P.add("Body", L.steps([(.075, .015), (.05, .015)]) + L.rod((0, 0, .03), (0, .02, .22), .012) + L.rod((0, .02, .22), (0, -.16, .36), .012))
    P.add("Accent", L.shade(.075, .03, .1, (0, -.16, .26)) + L.rod((0, -.16, .36), (0, -.16, .28), .008))
    P.add("Eye", [s.ball(.03, (0, -.16, .27), seg=10)])
    return P


def p_ceiling_lamp(L):
    """A stepped ceiling panel with a sunburst grille over the glowing diffuser."""
    P = D.Parts()
    P.add("Body", L.slab((1.24, .66, .03), (0, 0, -.015)) + L.slab((1.12, .54, .03), (0, 0, -.035)))
    P.add("Eye", L.slab((1.04, .46, .02), (0, 0, -.055)))
    P.add("Accent", L.sunburst((0, 0, -.068), .06, .3, 12, a0=0, a1=math.tau, width=.035, depth=.02, plane="XY"))
    return P


def p_exit_sign(L):
    P = props.exit_sign(L)
    P.add("Body", [s.rbox((.4, .2, .03), (0, 0, .07), r=.008, seg=1)])
    return P


def p_hatch(L):
    """Manhole with a stepped rim and a sunburst lid."""
    P = D.Parts()
    P.add("Body", L.steps([(.78, .03), (.7, .03)]))
    P.add("Accent", L.sunburst((0, 0, .06), .1, .62, 16, a0=0, a1=math.tau, width=.07, depth=.03, plane="XY"))
    P.add("Eye", [s.lathe([(.76, 0), (.9, 0), (.9, .02), (.76, .02)], seg=32)])
    return P


def p_printer(L):
    """A deco copier like a cash register: a stepped cabinet, a round-topped head with a glowing dial window, a paper
    tray like a fan."""
    P = D.Parts()
    P.add("Body", L.carcass((.6, .6, .8), (0, 0, 0), kick=.06))
    P.add("Body", zig(L, .56, .52, .08, (0, .02, .8), n=2, k=.03) + [s.rcyl(.2, .44, (0, .02, .86), axis="X", seg=16, r=.02)])
    P.add("Accent", fan(L, (-.06, -.31, .6), .16, n=7, width=.05, depth=.03) + L.door((.5, .3), (0, -.31, .32)))
    P.add("Accent", L.slab((.36, .3, .03), (-.05, -.37, .79), rot=(math.radians(8), 0, 0)))
    P.add("Eye", [s.rcyl(.06, .02, (.12, -.172, .9), axis="Y", seg=12, r=.004)])
    return P


PROP_DESIGNS = {"desk": p_desk, "terminal": p_terminal, "chair": p_chair, "server_rack": p_server_rack, "cabinet": p_cabinet, "meeting_table": p_meeting_table, "plant": p_plant, "kitchen_counter": p_kitchen_counter, "shelf": p_shelf, "boxes": p_boxes, "water_cooler": p_water_cooler, "whiteboard": p_whiteboard, "sofa": p_sofa, "toilet_stall": p_toilet_stall, "partition": p_partition, "bookshelf": p_bookshelf, "fridge": p_fridge, "coffee_machine": p_coffee_machine, "round_table": p_round_table, "armchair": p_armchair, "coffee_table": p_coffee_table, "tv_stand": p_tv_stand, "reception_desk": p_reception_desk, "trash_bin": p_trash_bin, "coat_rack": p_coat_rack, "lockers": p_lockers, "pallet": p_pallet, "wash_basin": p_wash_basin, "vending_machine": p_vending_machine, "ping_pong_table": p_ping_pong_table, "bean_bag": p_bean_bag, "arcade_machine": p_arcade_machine, "safe": p_safe, "floor_lamp": p_floor_lamp, "desk_lamp": p_desk_lamp, "ceiling_lamp": p_ceiling_lamp, "exit_sign": p_exit_sign, "hatch": p_hatch, "printer": p_printer}



# =========================================================================== the danger-zone robots
# Concepts: Logs/comfy/danger_s06/<name>_<seed>.png (ComfyUI, Z-Image). Contract numbers: designs.py, DANGER section.

def orient(bm, base, d):
    """Turn a shape built up +Z from z = 0 to point along d, its z = 0 at base."""
    d = Vector(d).normalized()
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Vector((0, 0, 1)).rotation_difference(d).to_matrix(), verts=bm.verts)
    return s.place(bm, Vector(base))


def ring(R, r, loc, normal=(0, -1, 0), seg=20, seg_r=6):
    """A closed hoop of radius R, tube r, round the axis `normal` through loc."""
    bm = torus_x(R, r, (0, 0, 0), seg=seg, seg_r=seg_r, axis_z=True)
    return orient(bm, loc, normal)


def horn(base, d, length, bell, seg=12, throat=.55):
    """(accent, eye): a gramophone horn from base along d — a narrow throat flaring to a lipped bell, hollow down to a
    cone; its glowing disc sits deep in the throat (at `throat` of the length)."""
    t = .028
    prof = [(t, 0), (t * 1.05, length * .3), (t * 1.5 + bell * .1, length * .58), (bell * .62, length * .82),
            (bell, length - .012), (bell - .002, length - .002), (bell - .014, length - .004),
            (bell * .55, length * .82 - .008), (t * 1.2, length * .5), (0, length * .48)]
    acc = s.lathe(prof, seg=seg)
    eye = s.lathe([(t * 2.2, 0), (t * 2.0, .008), (0, .014)], seg=10, loc=(0, 0, length * throat))
    return orient(acc, base, d), orient(eye, base, d)


def flare(base, d, length, r0, r1, seg=12):
    """A trumpet nozzle: a short pipe flaring to a rim, from base along d."""
    return orient(s.lathe([(r0, 0), (r0, length * .4), (r1 * .7, length * .85), (r1, length), (r1 * .8, length * 1.02),
                           (r0 * .7, length * .7), (0, length * .6)], seg=seg), base, d)


def caster_wheels(P, centres, radius, width):
    """Plain little casters (AccentWheel0..) for wheels hidden under a skirt: a rounded disc and a hub."""
    for i, c in enumerate(centres):
        P.add(f"AccentWheel{i}", [s.rcyl(radius, width, c, axis="X", seg=10, r=.008, bevel_seg=1),
                                  s.rcyl(radius * .4, width * 1.15, c, axis="X", seg=6, r=0)])


def cart_wheels(P, centres, radius, width):
    """Light carriage wheels (AccentWheel0..): a rounded tyre, a web, a hub and four spokes, symmetric about the
    centre (the style's own wheel is 550 triangles; these hide under skirts and carriages)."""
    for i, c in enumerate(centres):
        c = Vector(c)
        out = [s.lathe([(radius * .76, -width / 2), (radius, -width * .28), (radius, width * .28), (radius * .76, width / 2),
                        (radius * .72, 0), (radius * .76, -width / 2)], seg=10, axis="X", loc=c)]
        out.append(s.rcyl(radius * .78, width * .3, c, axis="X", seg=10, r=0))
        out.append(s.rcyl(radius * .3, width * 1.1, c, axis="X", seg=8, r=.004, bevel_seg=1))
        for k in range(4):
            a = (k + .5) / 4 * math.tau
            d = Vector((0, math.cos(a), math.sin(a)))
            out.append(align(s.rbox((width * .7, .02, radius * .5), r=0, seg=1), c + d * radius * .25, c + d * radius * .75))
        P.add(f"AccentWheel{i}", out)


def fin(loc, h=.06, r=.025):
    """A small ball finial (the style's finial carries a 16-segment ball)."""
    x, y, z = loc
    return [s.lathe([(0, 0), (r * .9, 0), (r * .5, h * .25), (r * .35, h * .55), (0, h * .6)], seg=10, loc=loc),
            s.ball(r, (x, y, z + h * .6 + r * .8), seg=8)]


def big_key(top, length, k=1.0, bit=1):
    """A storybook key hanging down from `top` (its bow ring): bow, collar, shaft, a stepped bit (bit = +-1: to which
    side, along x). Returns bmeshes."""
    top = Vector(top)
    out = [ring(.045 * k, .012 * k, top - Vector((0, 0, .045 * k)), seg=10, seg_r=4)]
    out.append(s.rcyl(.02 * k, .03 * k, top - Vector((0, 0, .1 * k)), seg=8, r=.004, bevel_seg=1))
    out.append(s.rcyl(.011 * k, length, top - Vector((0, 0, .1 * k + length / 2)), seg=6, r=0))
    end = top - Vector((0, 0, .1 * k + length))
    out.append(s.rbox((.045 * k, .014 * k, .028 * k), end + Vector((bit * .03 * k, 0, .022 * k)), r=.004, seg=1))
    out.append(s.rbox((.03 * k, .014 * k, .022 * k), end + Vector((bit * .03 * k, 0, .058 * k)), r=.004, seg=1))
    return out


def warden(L):
    """Вахтёр: the padlock doorman. His body is a great padlock — a rounded brass-banded slab with a glowing keyhole on
    its belly and the shackle arching up behind his head —, a drum head with a monocle and a doorman's moustache
    under a peaked cap, a sceptre key in one fist, a key ring in the other, a wind-up key in the back; he rolls on
    four little carriage wheels under a stepped skirt. Concept: danger_s06/warden_13739441."""
    P = D.Parts()
    r, w = D.WARDEN_WHEEL
    caster_wheels(P, D.WARDEN_WHEELS, r, w)
    for y in (-.17, .17):
        P.add("Accent", L.rod((-.27, y, .085), (.27, y, .085), .02))                                  # axles
    P.add("Accent", L.loft([(.09, .35, .31, 3.0), (.15, .36, .32, 3.0)]))                             # skirt rim
    P.add("Body", L.loft([(.14, .34, .3, 3.0), (.22, .33, .29, 3.0), (.27, .3, .26, 3.0)]))           # skirt
    # the padlock body
    P.add("Body", L.loft([(.25, .29, .16, 2.6), (.31, .33, .19, 2.6), (.79, .33, .19, 2.6), (.86, .28, .15, 2.6)],
                         round_caps=.03))
    for z in (.36, .74):
        P.add("Accent", L.loft([(z - .02, .343, .202, 2.6), (z + .02, .343, .202, 2.6)]))            # brass bands
    face, rim = L.dial(.15, (0, -.185, .56))
    P.add("Accent", face + rim)                                                                        # escutcheon
    P.add("Eye", [s.rcyl(.036, .016, (0, -.222, .6), axis="Y", seg=12, r=.004),
                  s.rbox((.036, .016, .085), (0, -.222, .535), r=.004, seg=1, taper=(1.6, 1.0))])   # glowing keyhole
    # the shackle: a fat brass arch from the lock's shoulders up behind the head
    R, cy, cz = .26, .13, .98
    pts = [(-R, cy, .8)] + [(-R * math.cos(a), cy, cz + R * math.sin(a)) for a in [k / 10 * math.pi for k in range(11)]] \
        + [(R, cy, .8)]
    P.add("Accent", [s.tube(pts, .045, res=1, smooth=False)])
    # head: a drum with a dial face, a monocle, a moustache, a doorman's peaked cap with a key badge
    P.add("Body", L.cyl(.08, .08, (0, -.04, .89)))
    P.add("Body", L.lathe([(0, .9), (.15, .9), (.17, .93), (.17, 1.22), (.15, 1.25), (0, 1.25)], loc=(0, -.05, 0)))
    face, rim = L.dial(.13, (0, -.215, 1.12))
    P.add("Body", face).add("Accent", rim)
    P.eye(L.monocle(.055, (.04, -.25, 1.2)))
    P.add("Eye", L.box((.05, .02, .02), (-.065, -.247, 1.2), r=.007))
    for sx in (-1, 1):
        P.add("Accent", [s.rbox((.1, .03, .035), (sx * .05, -.25, 1.12), r=.012, seg=1, rot=(0, sx * .35, 0))])  # moustache
    P.add("Accent", L.lathe([(0, 1.24), (.17, 1.24), (.18, 1.27), (.22, 1.43), (.23, 1.46), (0, 1.46)], loc=(0, -.04, 0)))
    P.add("Body", L.loft([(1.26, .185, .185, 2.0), (1.31, .192, .192, 2.0)], loc=(0, -.04, 0)))      # cap band
    P.add("Body", L.box((.32, .14, .022), (0, -.23, 1.255), r=.008, rot=(math.radians(-12), 0, 0)))   # peak
    P.add("Accent", fin((0, -.04, 1.45), h=.05, r=.025))
    P.add("Body", big_key((0, -.235, 1.4), .04, k=.8))                                                  # cap badge
    # arms: brass shoulder balls, tube arms, white-glove balls
    for sx in (-1, 1):
        P.add("Accent", L.ball(.07, (sx * .35, 0, .78)))
        P.add("Body", L.rod((sx * .35, 0, .78), (sx * .41, -.04, .56), .045))
        P.add("Accent", L.ball(.05, (sx * .41, -.04, .55)))
    P.add("Body", L.rod((-.41, -.04, .55), (-.37, -.22, .64), .04))
    P.add("Body", L.ball(.07, (-.37, -.25, .65)))                                                       # glove
    P.add("Accent", [s.rcyl(.016, .55, (-.37, -.27, .7), seg=8, r=.004)])                              # the sceptre key
    P.add("Accent", [ring(.06, .016, (-.37, -.27, 1.03), seg=16)])
    P.add("Accent", [s.rbox((.07, .02, .05), (-.33, -.27, .46), r=.006, seg=1),
                     s.rbox((.05, .02, .03), (-.33, -.27, .51), r=.006, seg=1)])
    P.add("Body", L.rod((.41, -.04, .55), (.37, -.22, .64), .04))
    P.add("Body", L.ball(.07, (.37, -.25, .65)))
    c = Vector((.37, -.27, .52))
    P.add("Accent", [ring(.1, .015, c, seg=20)])                                                       # key ring
    for k, a in enumerate((-.7, 0, .7)):
        top = c + Vector((math.sin(a) * .1, -.005 * k, -math.cos(a) * .1))
        P.add("Accent", big_key(top, .09 + .02 * (k % 2), k=.9, bit=1 if a >= 0 else -1))
    P.add("Accent", L.key((0, .18, .56), 1.0))
    return P


def repairman(L):
    """Ремонтник: the tinker's chest — a stepped sewing-box toolchest crouching between four thin brass legs whose
    ball knees rise above the lid, a carry handle, a small drum head with a monocle on a turned stalk at the front
    left, a tool arm raising a great open spanner on the right, a wind-up key behind. Concept:
    danger_s06/repairman_16189479."""
    P = D.Parts()
    P.add("Body", L.box((.5, .36, .2), (0, 0, .25)))
    P.add("Accent", L.box((.53, .39, .035), (0, 0, .365)))                                            # lid, stepped
    P.add("Body", L.box((.46, .32, .04), (0, 0, .4)))
    P.add("Accent", L.box((.34, .22, .025), (0, 0, .43)))
    P.add("Accent", L.box((.53, .39, .03), (0, 0, .16)))                                              # plinth band
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Accent", L.box((.045, .045, .2), (sx * .24, sy * .17, .26), r=.01))                 # corner straps
    face, rim = L.dial(.06, (.1, -.18, .26))
    P.add("Body", face).add("Accent", rim)                                                               # a little gauge
    P.add("Accent", L.box((.12, .03, .022), (-.08, -.185, .23), r=.008))                               # drawer pull
    # carry handle
    P.add("Accent", [s.tube([(-.12, .06, .43), (-.12, .06, .52), (.12, .06, .52), (.12, .06, .43)], .016, res=2,
                            smooth=True, segments_per=5)])
    P.add("Body", L.cyl(.024, .14, (0, .06, .52), axis="X"))
    # camera head on a turned stalk at the front left
    P.add("Body", L.lathe([(.035, 0), (.025, .04), (.02, .12), (.03, .14)], loc=(-.15, -.1, .435)))
    P.add("Body", L.lathe([(0, 0), (.055, 0), (.06, .02), (.06, .08), (.05, .1), (0, .1)], axis="-Y", loc=(-.15, -.1, .6)))
    P.eye(L.monocle(.032, (-.15, -.205, .59)))
    P.add("Accent", fin((-.15, -.15, .655), h=.05, r=.018))
    # tool arm: a drum shoulder on the lid, the upper arm up, the forearm forward, a great open spanner raised
    sh, el, wr = Vector((.15, -.06, .47)), Vector((.17, -.1, .72)), Vector((.17, -.27, .8))
    P.add("Accent", L.cyl(.06, .08, tuple(sh), axis="X"))
    P.add("Body", L.rod(sh, el, .04))
    P.add("Accent", L.ball(.045, tuple(el)))
    P.add("Body", L.rod(el, wr, .035))
    P.add("Accent", L.ball(.04, tuple(wr)))
    d = Vector((0, -.35, 1)).normalized()
    P.add("Accent", [align(s.rbox((.045, .025, .16), r=.008, seg=1), wr, wr + d * .16)])
    jaw = s.rcyl(.08, .03, (0, 0, 0), axis="Y", seg=16, r=.006)
    jaw = s.boolean(jaw, s.rbox((.085, .1, .08), (0, 0, .06), r=0), "DIFFERENCE")
    bmesh.ops.rotate(jaw, cent=Vector((0, 0, 0)), matrix=Vector((0, 0, 1)).rotation_difference(d).to_matrix(), verts=jaw.verts)
    P.add("Accent", [s.place(jaw, wr + d * .2)])
    P.add("Accent", [ring(.035, .011, wr - d * .05 + Vector((0, 0, -.02)), normal=(0, 1, 0), seg=12, seg_r=5)])  # ring end
    P.add("Accent", L.key((0, .18, .26), .7))
    for i in range(4):
        hip, knee, ankle, foot = D.repairman_leg(i)
        P.add("Body", L.ball(.05, tuple(hip)))                                                          # hip socket
        out = Vector((hip.x, hip.y, 0)).normalized()
        mid = hip.lerp(knee, .5) + out * .05 + Vector((0, 0, .04))
        P.add(f"AccentLeg{i}Upper", L.ball(.04, tuple(hip)) + [s.tube([tuple(hip), tuple(mid), tuple(knee)], .026, res=1,
                                                                      segments_per=3)] + L.ball(.045, tuple(knee)))
        bend = knee.lerp(foot, .35) + out * .09                                   # crab legs: bowed out like a pincer
        tip = foot + Vector((0, 0, .05))
        P.add(f"AccentLeg{i}Lower", [s.tube([tuple(knee), tuple(bend), tuple(tip)], .024, res=1, segments_per=3,
                                            radii=[1.0, .9, .6])] + L.ball(.034, tuple(bend))
              + L.steps([(.045, .02), (.03, .02), (.016, .02)], loc=tuple(foot)))
    return P


def smoker(L):
    """Дымогенератор: the samovar wagon — a fat brass-hooped samovar urn on a carriage, a fluted chimney with a trumpet
    crown and two splayed spouts that puff the smoke, side handles, a tap and a pressure gauge off to one side (it has
    no eyes: it senses nothing), a wind-up key behind. Concept: danger_s06/smoker_2885735."""
    P = D.Parts()
    r, w = D.SMOKER_WHEEL
    cart_wheels(P, D.SMOKER_WHEELS, r, w)
    for y in (-.2, .2):
        P.add("Accent", L.rod((-.32, y, .1), (.32, y, .1), .022))
    P.add("Body", L.box((.6, .5, .06), (0, 0, .2)))
    P.add("Accent", L.box((.64, .54, .025), (0, 0, .17)))
    P.add("Accent", L.steps([(.25, .03), (.21, .03)], loc=(0, 0, .22)))
    P.add("Body", L.lathe([(0, .26), (.17, .26), (.15, .3), (.18, .34), (.3, .44), (.34, .56), (.33, .7), (.28, .8),
                           (.2, .86), (.18, .9), (0, .9)]))
    for z, rr in ((.5, .335), (.72, .335)):                                    # hoops, as round as the urn (12 sides)
        P.add("Accent", [s.lathe([(0, z - .022), (rr, z - .022), (rr + .006, z), (rr, z + .022), (0, z + .022)], seg=12)])
    P.add("Accent", L.lathe([(0, .88), (.2, .88), (.21, .91), (.16, .95), (0, .96)]))                 # lid
    P.add("Body", L.fluted(.05, .92, 1.14, loc=(0, .04), n=6))                                         # chimney
    P.add("Accent", [s.lathe([(.05, 1.12), (.09, 1.19), (.11, 1.24), (.1, 1.27), (.07, 1.255), (0, 1.25)], seg=12,
                             loc=(0, .04, 0))])                                                       # trumpet crown
    for sx in (-1, 1):
        a, b = Vector((sx * .12, -.02, .86)), Vector((sx * .22, -.02, 1.0))
        P.add("Body", L.rod(a, b, .032))
        P.add("Accent", [flare(b - (b - a).normalized() * .02, b - a, .08, .033, .065)])
        P.add("Accent", [s.tube([(sx * .3, 0, .68), (sx * .4, 0, .66), (sx * .4, 0, .54), (sx * .32, 0, .52)], .018,
                                res=1, smooth=True, segments_per=4)])                                 # side handles
    P.add("Accent", L.rod((0, -.3, .42), (0, -.4, .37), .022))                                        # the tap
    P.add("Accent", L.ball(.03, (0, -.3, .45)))
    P.add("Accent", [s.rbox((.06, .02, .018), (0, -.3, .48), r=.006, seg=1)])
    face, rim = L.dial(.075, (.17, -.27, .68))
    P.add("Body", face).add("Accent", rim)
    P.add("Accent", [align(s.rbox((.012, .008, .06), r=.003, seg=1), Vector((.17, -.31, .68)), Vector((.2, -.31, .72)))])
    P.add("Accent", L.key((0, .3, .6), 1.0))
    return P


def relay(L):
    """Ретранслятор: the bandstand clock tower — a stepped ziggurat foot, a fluted column with a clock cabinet and three
    lamps, a capital carrying a drum with four gramophone horns glowing in their throats, a sunburst crown and a lamp
    on a whip. Static. Concept: danger_s06/relay_10650448."""
    P = D.Parts()
    P.add("Body", L.steps([(.34, .05), (.28, .05), (.2, .06)]))
    P.add("Accent", L.loft([(.035, .348, .348, 2.0), (.055, .348, .348, 2.0)]))
    P.add("Body", L.fluted(.07, .21, 1.12, n=8))
    P.add("Accent", L.steps([(.11, .03), (.09, .02)], loc=(0, 0, .19)))
    P.add("Body", L.loft([(.55, .14, .12, 3.2), (.85, .14, .12, 3.2)], round_caps=.015))              # clock cabinet
    P.add("Accent", L.steps([(.16, .025)], loc=(0, 0, .54)) + L.steps([(.16, .025)], loc=(0, 0, .845)))
    face, rim = L.dial(.085, (0, -.12, .74))
    P.add("Body", face).add("Accent", rim)
    for x in (-.05, 0, .05):
        P.add("Eye", L.ball(.017, (x, -.122, .6)))
    P.add("Accent", L.steps([(.1, .03), (.14, .04), (.17, .04)], loc=(0, 0, 1.12)))
    hub = 1.34
    P.add("Body", L.lathe([(0, 1.23), (.15, 1.23), (.16, 1.26), (.16, 1.42), (.14, 1.45), (0, 1.45)]))
    P.add("Accent", L.loft([(hub - .015, .168, .168, 2.0), (hub + .015, .168, .168, 2.0)]))
    for d in ((0, -1, -.12), (-1, .15, -.05), (1, .15, -.05), (0, 1, .08)):
        dv = Vector(d).normalized()
        base = Vector((0, 0, hub)) + Vector((dv.x, dv.y, 0)).normalized() * .13
        a, e = horn(base, dv, .3, .15)
        P.add("Accent", [a]).add("Eye", [e])
        P.add("Accent", [orient(s.rcyl(.04, .05, (0, 0, .025), seg=12, r=.006), base - dv * .02, dv)])   # driver can
    P.add("Accent", fin((0, 0, 1.44), h=.16, r=.04))
    P.add("Accent", L.sunburst((0, .02, 1.56), .06, .24, 9, a0=math.radians(5), a1=math.radians(175), width=.045, depth=.025))
    P.add("Body", [s.rcyl(.008, .2, (0, 0, 1.7), seg=6, r=.002)])
    P.add("Eye", L.ball(.03, (0, 0, 1.82)))
    return P


def nurse(L):
    """Медсестра: the music-box nurse — a bell skirt of stepped cake tiers with brass rims and a cross on the apron,
    gliding on three casters under a stepped disc, a slim bodice with a fob watch, a drum head with a monocle and red
    lips under a folded cap with a cross, a great brass syringe held high, a little hand bell in the other hand.
    Concept: danger_s06/nurse_4304637."""
    P = D.Parts()
    r, w = D.NURSE_WHEEL
    caster_wheels(P, D.NURSE_WHEELS, r, w)
    for x, y, z in D.NURSE_WHEELS:
        for sx in (-1, 1):
            P.add("Accent", [s.rbox((.008, .05, .075), (x + sx * .028, y, .08), r=.002, seg=1)])       # caster forks
    P.add("Body", L.steps([(.3, .035), (.27, .03)], loc=(0, 0, .09)))
    P.add("Accent", L.loft([(.1, .306, .306, 2.0), (.122, .306, .306, 2.0)]))
    P.add("Body", L.lathe([(0, .15), (.27, .15), (.27, .2), (.24, .22), (.235, .38), (.21, .4), (.2, .56), (.18, .58),
                           (.172, .74), (.155, .76), (.15, .96), (0, .96)]))
    for z, rr in ((.2, .275), (.39, .24), (.57, .205), (.75, .176)):
        P.add("Accent", L.loft([(z - .01, rr, rr, 2.0), (z + .012, rr, rr, 2.0)]))                   # tier rims
    P.add("Accent", [s.rbox((.03, .02, .11), (0, -.195, .48), r=.006, seg=1, rot=(math.radians(8), 0, 0)),
                     s.rbox((.1, .02, .03), (0, -.195, .48), r=.006, seg=1, rot=(math.radians(8), 0, 0))])  # apron cross
    P.add("Accent", L.loft([(.94, .158, .158, 2.0), (.99, .16, .16, 2.0)]))                          # waist band
    P.add("Body", L.loft([(.97, .15, .12, 2.2), (1.1, .18, .13, 2.4), (1.24, .21, .14, 2.6), (1.32, .2, .13, 2.6),
                          (1.37, .12, .09, 2.4)], round_caps=.03))
    face, rim = L.dial(.04, (.08, -.14, 1.2))
    P.add("Body", face).add("Accent", rim)                                                               # fob watch
    P.add("Accent", L.steps([(.1, .025), (.065, .02)], loc=(0, 0, 1.35)))
    P.add("Body", L.cyl(.04, .06, (0, -.02, 1.41)))
    P.add("Body", L.lathe([(0, 1.42), (.13, 1.42), (.15, 1.45), (.15, 1.62), (.13, 1.65), (0, 1.65)], loc=(0, -.06, 0)))
    face, rim = L.dial(.115, (0, -.205, 1.54))
    P.add("Body", face).add("Accent", rim)
    P.eye(L.monocle(.042, (.045, -.24, 1.55)))
    P.add("Eye", L.box((.04, .02, .016), (-.05, -.237, 1.55), r=.006))
    P.add("Accent", [s.rbox((.05, .02, .016), (0, -.24, 1.48), r=.007, seg=1)])                       # lips
    P.add("Body", [s.rbox((.27, .05, .11), (0, -.1, 1.7), r=.012, seg=2, taper=(1.2, 1.0), rot=(-.32, 0, 0))])  # cap
    P.add("Body", L.ball(.12, (0, -.02, 1.64), scale=(1, 1, .55)))
    P.add("Accent", [s.rbox((.035, .02, .09), (0, -.13, 1.71), r=.006, seg=1, rot=(-.32, 0, 0)),
                     s.rbox((.09, .02, .035), (0, -.13, 1.71), r=.006, seg=1, rot=(-.32, 0, 0))])
    for sx in (-1, 1):
        P.add("Accent", L.ball(.055, (sx * .22, -.02, 1.3)))
    sh, el, wr = Vector((.23, -.02, 1.29)), Vector((.29, -.06, 1.08)), Vector((.28, -.2, 1.25))
    P.add("Body", L.rod(sh, el, .04)).add("Accent", L.ball(.04, tuple(el))).add("Body", L.rod(el, wr, .034))
    P.add("Body", L.ball(.05, tuple(wr)))
    sd = Vector((.1, -.25, 1)).normalized()
    s0 = wr - sd * .15
    P.add("Accent", [ring(.03, .009, s0 - sd * .02, normal=sd, seg=12, seg_r=5)])                    # thumb ring
    P.add("Accent", [align(s.rcyl(.014, .14, seg=8, r=.003), s0 - sd * .01, s0 + sd * .13)])
    P.add("Accent", [align(s.rbox((.17, .05, .02), r=.007, seg=1), s0 + sd * .12, s0 + sd * .14)])   # finger rest
    P.add("Body", [align(s.rcyl(.055, .36, seg=14, r=.01), s0 + sd * .14, s0 + sd * .5)])            # barrel
    for k in range(3):
        c = s0 + sd * (.2 + .1 * k)
        P.add("Accent", [align(s.rcyl(.06, .02, seg=14, r=.004), c - sd * .01, c + sd * .01)])
    P.add("Accent", [align(s.rcyl(.04, .05, seg=12, r=.006, radius2=.018), s0 + sd * .5, s0 + sd * .55)])
    P.add("Accent", [align(s.rcyl(.009, .16, seg=6, r=.002), s0 + sd * .54, s0 + sd * .7)])           # needle
    sh, el, wr = Vector((-.23, -.02, 1.29)), Vector((-.28, -.05, 1.07)), Vector((-.27, -.2, 1.0))
    P.add("Body", L.rod(sh, el, .04)).add("Accent", L.ball(.04, tuple(el))).add("Body", L.rod(el, wr, .034))
    P.add("Body", L.ball(.05, tuple(wr)))
    P.add("Accent", L.lathe([(0, 0), (.055, 0), (.05, .02), (.035, .07), (.012, .1), (.015, .13), (0, .14)],
                            loc=(-.27, -.22, .84)))                                                    # hand bell
    return P


def mimic(L):
    """Мимик: the searchable crate's double, and it must pass for one: the crate's planks, ribs, grips and lid stay
    (Body + AccentLid within 0.8 x 0.6 x 0.6); the picture book shows only in the brass — corner brackets with stepped
    caps that are stilt legs with ball knees, little stepped fangs in the seam under the lid, and a monocle peeping
    out of it. Concept: danger_s06/mimic_9115705 (the brackets)."""
    P = D.Parts()
    t = .03
    body = [s.rbox((.78, .58, .05), (0, 0, .025), r=.02, seg=2)]
    body += [s.rbox((t, .58, .5), (x * (.39 - t / 2), 0, .25), r=.012, seg=2) for x in (-1, 1)]
    body.append(s.rbox((.78 - 2 * t, t, .5), (0, .29 - t / 2, .25), r=.012, seg=2))
    body.append(s.rbox((.78 - 2 * t + .01, t, .48), (0, -.29 + t / 2, .24), r=.012, seg=2))
    body.append(s.rbox((.7, .5, .03), (0, 0, .41), r=.01, seg=1))
    for z in (.06, .44):
        body += [s.rbox((.02, .6, .045), (x * .39, 0, z), r=.008, seg=1) for x in (-1, 1)]
        body += [s.rbox((.8, .02, .045), (0, y * .29, z), r=.008, seg=1) for y in (-1, 1)]
    P.add("Body", body)
    acc = []
    for x in (-1, 1):
        acc.append(s.rbox((.03, .22, .035), (x * .405, 0, .3), r=.012, seg=2))
        acc += [s.rbox((.04, .03, .05), (x * .395, y * .1, .3), r=.008, seg=1) for y in (-1, 1)]
    for x in (-.22, .22):
        acc.append(s.rbox((.08, .025, .07), (x, -.3, .4), r=.008, seg=2))
        acc.append(s.rbox((.05, .022, .085), (x, -.307, .462), r=.008, seg=1, taper=(.55, 1.0)))       # fang
    acc += [s.rbox((.12, .02, .06), (x, .3, .465), r=.006, seg=1) for x in (-.25, .25)]
    for x in (-.31, -.14, -.07, .07, .14, .31):
        acc.append(s.rcyl(.013, .032, (x, -.279, .482), seg=6, r=0, radius2=.003))                    # lower teeth
    P.add("Accent", acc)
    lid = s.rbox((.8, .6, .1), (0, 0, .548), r=.025, seg=3)
    s.inset_faces(lid, s.facing((0, 0, 1), min_area=.1, tol=.95), thickness=.04, depth=-.012)
    pieces = [lid]
    pieces += [s.rcyl(.016, .1, (x, .3, .5), axis="X", seg=12, r=.003) for x in (-.25, .25)]
    pieces += [s.rbox((.1, .016, .07), (x, .303, .54), r=.005, seg=1) for x in (-.25, .25)]
    pieces += [s.rbox((.06, .03, .04), (x, -.305, .53), r=.008, seg=1) for x in (-.22, .22)]
    for x in (-.265, -.175, -.105, .105, .175, .265):
        pieces.append(s.rcyl(.003, .03, (x, -.281, .49), seg=6, r=0, radius2=.013))                    # upper teeth
    pieces += [s.rbox((.33, .014, .03), (x * .215, -.293, .505), r=.005, seg=1) for x in (-1, 1)]
    P.add("AccentLid", pieces)
    P.add("BodySocket", [s.rcyl(.026, .03, (0, -.27, .489), axis="Y", seg=12, r=.004)])
    P.add("Accent", [ring(.022, .006, (0, -.29, .489), seg=14, seg_r=4)])                              # monocle rim
    P.add("Eye", [s.lathe([(.017, 0), (.017, .004), (.011, .012), (0, .014)], seg=12, axis="-Y", loc=(0, -.28, .489))])
    for i, (cx, cy) in enumerate(D.MIMIC_CORNERS):
        sx, sy = (1 if cx > 0 else -1), (1 if cy > 0 else -1)
        c = Vector((cx, cy, 0))
        out = Vector((sx, sy, 0)).normalized()
        hip = c + out * -.004 + Vector((0, 0, .462))
        knee = c + out * .009 + Vector((0, 0, D.MIMIC_KNEE))
        foot = c + Vector((0, 0, .03))
        leg = f"AccentLeg{i}"
        P.add(leg, [align(s.rbox((.07, .07, (knee - hip).length), r=.012, seg=1), hip, knee),
                    align(s.rbox((.074, .074, (foot - knee).length), r=.012, seg=1), knee, foot)])
        P.add(leg, [s.rbox((.086, .086, .03), (hip.x, hip.y, .45), r=.01, seg=1),
                    s.rbox((.07, .07, .025), (hip.x, hip.y, .47), r=.008, seg=1)])                     # stepped top cap
        P.add(leg, [s.rbox((.096, .096, .07), (c.x, c.y, .035), r=.014, seg=1),
                    s.rbox((.08, .08, .03), (c.x, c.y, .08), r=.01, seg=1)])                           # stepped corner foot
        P.add(leg, L.ball(.034, tuple(knee + out * .015)))                                             # ball knee
    P.split = {f"AccentLeg{i}": D.MIMIC_KNEE for i in range(4)}
    return P


def salesman(L):
    """Назойливый продавец: the carousel barker — a fluted carousel drum on a stepped skirt over the wheels, a round
    tray of wares held out (a cake, an urn, a bottle, a box), a drum head whose whole face is a glowing round screen
    with a grin, a gramophone megaphone at its side, a striped carousel canopy with a scalloped valance, a finial and a
    pennant. Concept: danger_s06/salesman_7664549."""
    P = D.Parts()
    r, w = D.SALESMAN_WHEEL
    cart_wheels(P, D.SALESMAN_WHEELS, r, w)
    for y in (-.2, .2):
        P.add("Accent", L.rod((-.3, y, .09), (.3, y, .09), .02))
    P.add("Body", L.steps([(.36, .06), (.33, .06), (.3, .05)], loc=(0, 0, .08)))
    P.add("Body", L.fluted(.27, .25, .98, n=10))
    P.add("Accent", L.steps([(.3, .03)], loc=(0, 0, .25)) + L.steps([(.33, .03), (.36, .03)], loc=(0, 0, .97)))
    P.add("Accent", L.loft([(.6, .285, .285, 2.0), (.64, .285, .285, 2.0)]))
    P.add("Accent", [s.rbox((.26, .03, .16), (0, -.285, .48), r=.012, seg=1)])                         # sale plaque
    P.add("Accent", L.sunburst((0, -.29, .58), .02, .1, 5, a0=math.radians(20), a1=math.radians(160), width=.03, depth=.015))
    for sx in (-1, 1):
        P.add("Accent", L.ball(.05, (sx * .3, -.02, .88)))
        P.add("Body", L.rod((sx * .3, -.02, .88), (sx * .32, -.2, .86), .035))
        P.add("Body", L.rod((sx * .32, -.2, .86), (sx * .25, -.36, .91), .032))
        P.add("Body", L.ball(.045, (sx * .25, -.38, .92)))
    P.add("Accent", L.lathe([(0, 0), (.2, 0), (.22, .01), (.22, .035), (.2, .035), (0, .02)], loc=(0, -.4, .9)))  # tray
    P.add("Accent", L.steps([(.07, .04), (.05, .035), (.03, .03)], loc=(-.08, -.42, .92)))           # cake
    P.add("Accent", L.lathe([(.03, 0), (.045, .04), (.035, .09), (.015, .1), (.02, .12), (0, .12)], loc=(.07, -.36, .92)))
    P.add("Accent", L.lathe([(.025, 0), (.025, .07), (.01, .1), (.01, .13), (0, .13)], loc=(.1, -.47, .92)))
    P.add("Accent", L.box((.08, .07, .06), (-.02, -.31, .955), rot=(0, 0, .3)))
    P.add("Body", L.lathe([(.1, .99), (.1, 1.06), (.08, 1.1), (.08, 1.17), (0, 1.17)]))
    tilt = math.radians(-16)
    hc = Vector((0, -.03, 1.33))

    def tl(bm):
        bmesh.ops.rotate(bm, cent=hc, matrix=Matrix.Rotation(tilt, 3, "X"), verts=bm.verts)
        return bm
    P.add("Body", [tl(s.lathe([(0, 0), (.18, 0), (.19, .02), (.19, .2), (.17, .22), (0, .22)], seg=16, axis="-Y",
                             loc=(0, .08, 1.33)))])
    P.add("Accent", [tl(ring(.172, .02, (0, -.15, 1.33), seg=16, seg_r=5))])                         # brass bezel
    P.add("Eye", [tl(s.rcyl(.155, .02, (0, -.15, 1.33), axis="Y", seg=16, r=.004))])                   # the screen face
    for sx in (-1, 1):
        P.add("Accent", [tl(s.rcyl(.028, .015, (sx * .06, -.165, 1.37), axis="Y", seg=10, r=.004))])
    grin = [(math.sin(a) * .09, -.167, 1.3 - math.cos(a) * .035) for a in [math.radians(-70 + 14 * k) for k in range(11)]]
    P.add("Accent", [tl(s.tube(grin, .013, res=1, smooth=False))])
    mp0, mp1 = Vector((-.33, -.05, 1.3)), Vector((-.5, -.26, 1.4))
    P.add("Body", L.rod((-.18, -.03, 1.3), mp0, .025))
    a, _ = horn(mp0, mp1 - mp0, .33, .13, throat=.3)
    P.add("Accent", [a])
    P.add("Body", L.rod((0, .05, 1.5), (0, .05, 1.56), .03))
    P.add("Body", L.lathe([(0, 1.52), (.33, 1.52), (.33, 1.545), (.18, 1.62), (.05, 1.68), (0, 1.69)]))  # canopy
    for k in range(8):
        a0 = k / 8 * math.tau
        p0 = Vector((math.cos(a0) * .3, math.sin(a0) * .3, 1.55))
        p1 = Vector((math.cos(a0) * .06, math.sin(a0) * .06, 1.675))
        P.add("Accent", [align(s.rbox((.09, .014, (p1 - p0).length), r=.004, seg=1, taper=(.25, 1.0)), p0, p1)])  # stripes
    for k in range(14):
        a0 = (k + .5) / 14 * math.tau
        P.add("Accent" if k % 2 else "Body", [s.rcyl(.042, .05, (math.cos(a0) * .315, math.sin(a0) * .315, 1.515),
                                                     seg=6, r=.01, bevel_seg=1)])                     # valance
    P.add("Accent", fin((0, 0, 1.67), h=.08, r=.025))
    P.add("Accent", [s.rcyl(.006, .12, (0, 0, 1.78), seg=6, r=.002)])
    P.add("Accent", [s.rbox((.13, .01, .07), (.068, 0, 1.8), r=.004, seg=1, taper=(.4, 1.0))])        # pennant
    return P


def gunner(L):
    """Охранник с пушкой: the toy-soldier nutcracker with an organ gun — a heavy urn chest with brass bands, buttons and a
    bandolier, big stepped epaulettes, a drum head with a monocle and a moustache under a tall shako; the right
    forearm is a barrel organ's gun: a stepped receiver, six brass pipes to the muzzle, a drum with a crank; a pack of
    organ pipes on the back. Column legs on pedestal feet. Concept: danger_s06/gunner_16772084."""
    P = D.Parts()
    P.add("Body", L.loft([(.86, .24, .18, 2.2), (.98, .28, .2, 2.2), (1.12, .24, .18, 2.2), (1.36, .36, .24, 2.3),
                          (1.54, .42, .26, 2.4), (1.64, .36, .24, 2.3), (1.7, .16, .13, 2.0)], round_caps=.02))
    for z, a, b in ((1.1, .245, .185), (1.42, .385, .254)):
        P.add("Accent", L.loft([(z - .025, a, b, 2.3), (z + .025, a + .005, b + .004, 2.3)]))
    P.add("Accent", L.loft([(.8, .28, .22, 2.2), (.87, .3, .23, 2.2), (.93, .28, .21, 2.2)]))         # hip skirt
    for z in (1.2, 1.3, 1.52):
        P.add("Accent", [s.ball(.032, (0, -.215 - (z - 1.2) * .2, z), seg=6)])
    bp = [Vector((.3, -.26, 1.6)), Vector((.05, -.3, 1.36)), Vector((-.2, -.22, 1.1))]
    P.add("Accent", [s.tube([tuple(p) for p in bp], .035, res=1)])                                     # bandolier
    for k in range(6):
        f = (k + .5) / 6
        p = bp[0].lerp(bp[1], f * 2) if f < .5 else bp[1].lerp(bp[2], f * 2 - 1)
        P.add("Body", [s.rcyl(.02, .08, (p.x, p.y - .035, p.z), seg=6, r=0, rot=(0, math.radians(48), 0))])
    for sx in (-1, 1):
        P.add("Accent", L.steps([(.17, .045), (.11, .05)], loc=(sx * .42, 0, 1.6)))
        P.add("Accent", L.ball(.09, (sx * .46, 0, 1.52)))
    x = .5
    P.add("Body", L.rod((x - .04, 0, 1.52), (x, -.02, 1.2), .065)).add("Accent", L.ball(.075, (x, -.02, 1.19)))
    P.add("Body", L.rod((x, -.02, 1.19), (x - .02, -.12, .92), .06)).add("Accent", L.ball(.095, (x - .02, -.13, .86)))
    P.add("Body", L.rod((-x + .04, 0, 1.52), (-x, 0, 1.17), .065)).add("Accent", L.ball(.085, (-x, 0, 1.15)))
    # the organ gun along -y on the axis (x -0.5, z 1.1), muzzle at y -0.99
    gx, gz = D.GUNNER_MUZZLE[0], D.GUNNER_MUZZLE[2]
    P.add("Body", [s.lathe([(0, 0), (.1, 0), (.12, .03), (.12, .3), (.1, .34), (.06, .38), (0, .38)], seg=14, axis="-Y",
                           loc=(gx, .08, gz))])                                                       # receiver
    for y in (-.06, -.22):
        P.add("Accent", [s.rcyl(.128, .03, (gx, y, gz), axis="Y", seg=14, r=.006)])
    for k in range(6):
        a = k / 6 * math.tau
        P.add("Accent", [s.rcyl(.026, .69, (gx + math.cos(a) * .058, -.645, gz + math.sin(a) * .058), axis="Y", seg=6, r=0)])
    P.add("Accent", [s.rcyl(.02, .64, (gx, -.67, gz), axis="Y", seg=8, r=.004)])
    for y in (-.5, -.86):
        P.add("Accent", [s.rcyl(.095, .03, (gx, y, gz), axis="Y", seg=14, r=.006)])                   # clamps
    P.add("Accent", [s.rcyl(.15, .1, (gx - .03, -.04, gz - .17), axis="X", seg=16, r=.012)])         # drum
    P.add("Accent", L.rod((gx - .08, -.04, gz - .17), (gx - .16, -.04, gz - .17), .015))
    P.add("Accent", L.rod((gx - .16, -.04, gz - .17), (gx - .16, -.04, gz - .28), .013) + L.ball(.025, (gx - .16, -.04, gz - .3)))  # crank
    # organ pipes on the back
    P.add("Body", L.box((.44, .22, .44), (0, .33, 1.38)))
    P.add("Accent", L.box((.48, .25, .03), (0, .33, 1.6)))
    for k, h in enumerate((.18, .26, .34, .26, .18)):
        xx = -.16 + k * .08
        P.add("Accent", [s.rcyl(.03, h, (xx, .33, 1.6 + h / 2), seg=8, r=.004, bevel_seg=1)])
        P.add("Body", [s.rbox((.03, .012, .02), (xx, .3, 1.66), r=.003, seg=1)])                      # pipe mouths
    # head
    P.add("Body", L.cyl(.07, .12, (0, -.03, 1.7)))
    P.add("Body", L.lathe([(0, 1.72), (.15, 1.72), (.17, 1.75), (.17, 1.94), (.15, 1.97), (0, 1.97)], loc=(0, -.08, 0)))
    face, rim = L.dial(.13, (0, -.25, 1.84))
    P.add("Body", face).add("Accent", rim)
    P.eye(L.monocle(.055, (.035, -.285, 1.86)))
    P.add("Eye", L.box((.05, .02, .02), (-.06, -.282, 1.86), r=.007))
    for sx in (-1, 1):
        P.add("Accent", [s.rbox((.1, .03, .035), (sx * .05, -.285, 1.78), r=.012, seg=1, rot=(0, sx * .35, 0))])
    P.add("Accent", L.lathe([(0, 1.95), (.17, 1.95), (.18, 1.99), (.2, 2.18), (.21, 2.2), (0, 2.2)], loc=(0, -.07, 0)))
    P.add("Body", L.loft([(2.02, .182, .182, 2.0), (2.06, .187, .187, 2.0)], loc=(0, -.07, 0)))
    P.add("Accent", L.box((.26, .12, .02), (0, -.25, 1.965), r=.008, rot=(math.radians(-8), 0, 0)))
    P.add("Accent", fin((0, -.07, 2.19), h=.1, r=.04))
    P.add("Accent", L.key((0, .45, 1.2), 1.0))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUNNER_HIP_X
        P.add(leg, L.ball(.095, (x, 0, D.GUNNER_HIP_Z)))
        P.add(leg, L.lathe([(.065, D.GUNNER_KNEE), (.09, .6), (.1, .78), (.085, D.GUNNER_HIP_Z)], loc=(x, 0, 0)))
        P.add(leg, L.steps([(.09, .035), (.07, .035)], loc=(x, 0, D.GUNNER_KNEE - .035)))
        P.add(leg, L.lathe([(.06, .08), (.075, .25), (.065, D.GUNNER_KNEE)], loc=(x, 0, 0)))
        P.add(leg, L.steps([(.15, .05), (.1, .05)], loc=(x, -.04, 0)))
        P.add(leg, L.box((.16, .2, .07), (x, -.13, .035), taper=(.85, .8)))
    P.split = {"AccentLeg0": D.GUNNER_KNEE, "AccentLeg1": D.GUNNER_KNEE}
    return P


def driller(L):
    """Бурильщик: the clockwork mole — a stepped ziggurat hull on two crawler treads, a great spinning-top drill of
    stepped tiers with a brass spiral, on a gearbox drum carried by two pistons, a small domed cab with a porthole,
    a fluted chimney with a stepped crown, a big wind-up key behind. Concept: danger_s06/driller_14560890."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .4
        P.add("BodyTracks", L.track((.22, 1.0, .42), (x, .1, .21)))
        for k in range(7):
            P.add("BodyTracks", [s.rbox((.21, .035, .03), (x, -.25 + k * .117, .415), r=.006, seg=1)])
    P.add("Body", L.box((.58, .9, .24), (0, .1, .36)))
    for wd, dp, z in ((.9, .92, .5), (.72, .74, .62), (.54, .56, .74)):
        P.add("Body", L.box((wd, dp, .12), (0, .14, z)))
        P.add("Accent", L.box((wd + .02, dp + .02, .025), (0, .14, z + .06)))                         # stepped trims
    P.add("Body", [s.lathe([(0, 0), (.29, 0), (.3, .02), (.3, .1), (.27, .13), (0, .13)], seg=16, axis="-Y",
                           loc=(0, -.26, D.DRILL_AXIS_Z))])                                           # gearbox
    P.add("Accent", [s.rcyl(.31, .04, (0, -.375, D.DRILL_AXIS_Z), axis="Y", seg=16, r=.008)])
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .28, -.06, .86), (sx * .26, -.32, .76), .05, kind="piston"))
        P.add("Accent", L.ball(.055, (sx * .28, -.06, .86)))
    # the drill: stepped tiers to a point, a two-start brass spiral
    y0, z0 = D.DRILL_BASE_Y, D.DRILL_AXIS_Z
    ln = D.DRILL_BASE_Y - D.DRILL_TIP_Y
    prof = [(.29, 0), (.295, .025), (.24, .1), (.22, .1), (.17, .2), (.15, .2), (.1, .31), (.085, .31), (.04, .43), (0, ln)]
    P.add("AccentDrill", [s.lathe(prof, seg=16, axis="-Y", loc=(0, y0, z0))])

    def cone_r(dd):
        for (r0, d0), (r1, d1) in zip(prof, prof[1:]):
            if d0 <= dd <= d1 and d1 > d0:
                return r0 + (r1 - r0) * (dd - d0) / (d1 - d0)
        return 0.0
    for start in (0, math.pi):
        pts, radii = [], []
        for k in range(17):
            f = .04 + k / 16 * .82
            a = start + f * 2.2 * math.tau
            rr = cone_r(f * ln) + .004
            pts.append((math.cos(a) * rr, y0 - f * ln, z0 + math.sin(a) * rr))
            radii.append(max(.3, 1 - f))
        P.add("AccentDrill", [s.tube(pts, .022, res=0, radii=radii, segments_per=2)])
    # the cab: a drum with a dome, the porthole eye, a finial
    P.add("Body", L.lathe([(0, .79), (.19, .79), (.19, 1.16), (.17, 1.22), (.11, 1.27), (0, 1.29)], loc=(0, -.02, 0)))
    P.add("Accent", L.loft([(1.02, .2, .2, 2.0), (1.06, .2, .2, 2.0)], loc=(0, -.02, 0)))
    P.eye(L.monocle(.05, (0, -.215, 1.15)))                                    # the porthole
    P.add("Accent", fin((0, -.02, 1.28), h=.06, r=.025))
    P.add("Body", L.fluted(.05, .8, 1.3, loc=(.22, .38), n=6))
    P.add("Accent", L.steps([(.07, .03), (.09, .04), (.06, .03)], loc=(.22, .38, 1.3)))
    P.add("Accent", L.key((0, .58, .62), 1.3))
    return P


def exterminator(L):
    """Дезинсектор: the perfumer — a carriage with a brass rail, an urn body, a gas-mask head with two goggle monocles
    and an atomiser snout, two tall perfume-bottle tanks with stoppers and brass bands on the back, two curved booms
    ending in trumpet nozzles with squeeze bulbs on their hoses, a third nozzle straight up with a lamp. Concept:
    danger_s06/exterminator_10767869."""
    P = D.Parts()
    r, w = D.EXTERMINATOR_WHEEL
    cart_wheels(P, D.EXTERMINATOR_WHEELS, r, w)
    for y in (-.24, .24):
        P.add("Accent", L.rod((-.42, y, .14), (.42, y, .14), .025))
    P.add("Body", L.box((.6, .74, .18), (0, 0, .28)))
    P.add("Accent", L.box((.64, .78, .03), (0, 0, .375)))
    for k in range(6):
        P.add("Accent", [s.rbox((.04, .02, .1), (-.22 + k * .09, -.38, .28), r=.006, seg=1, rot=(0, math.radians(35), 0))])
    P.add("Body", L.loft([(.38, .26, .22, 2.2, 0, -.05), (.6, .28, .23, 2.2, 0, -.05), (.84, .2, .17, 2.2, 0, -.05)],
                         round_caps=.03))
    P.add("Accent", L.loft([(.6, .29, .24, 2.2, 0, -.05), (.64, .285, .235, 2.2, 0, -.05)]))
    P.add("Body", L.ball(.18, (0, -.08, 1.02), scale=(1, 1, 1.05)))
    P.add("Accent", [s.lathe([(.13, 0), (.14, .02), (.12, .04), (0, .045)], seg=16, axis="-Y", loc=(0, -.2, 1.03))])  # mask
    for sx in (-1, 1):
        P.eye(L.monocle(.038, (sx * .052, -.25, 1.06)))
    P.add("Accent", [s.lathe([(.055, 0), (.06, .03), (.045, .08), (.05, .1), (0, .1)], seg=12, axis="-Y", loc=(0, -.2, .92))])
    for sx in (-1, 1):
        P.add("Accent", L.steps([(.05, .03), (.04, .03)], loc=(sx * .16, -.18, .9)))                  # filter caps
    for sx in (-1, 1):
        x = sx * .16
        P.add("Accent", L.lathe([(0, .3), (.1, .3), (.12, .34), (.12, 1.12), (.08, 1.2), (.04, 1.22), (.04, 1.27), (0, 1.27)],
                                loc=(x, .26, 0)))
        P.add("Accent", L.ball(.05, (x, .26, 1.32)))                                                    # stopper
        for z in (.55, 1.0):
            P.add("BodyBands", L.loft([(z - .03, .127, .127, 2.0), (z + .03, .127, .127, 2.0)], loc=(x, .26, 0)))
        P.add("Body", L.box((.08, .1, .5), (sx * .27, .22, .62)))
    P.add("Body", L.box((.56, .1, .08), (0, .2, .9)))
    for sx in (-1, 1):
        pts = [(sx * .22, .22, .92), (sx * .38, .18, 1.02), (sx * .44, .04, 1.2), (sx * .42, -.12, 1.24)]
        P.add("BodyBooms", [s.tube(pts, .03, res=1, segments_per=4)] + L.ball(.04, (sx * .22, .22, .92)))
        n0 = Vector((sx * .42, -.12, 1.24))
        P.add("BodyBooms", L.ball(.045, tuple(n0)))
        P.add("BodyBooms", [flare(n0, (sx * .45, -1, -.45), .16, .025, .065)])
        P.add("BodyBooms", [s.tube([(sx * .16, .26, 1.32), (sx * .2, .32, 1.34), (sx * .26, .34, 1.12), (sx * .23, .25, .95)],
                                   .014, res=1, segments_per=4)])
        P.add("BodyBooms", L.ball(.055, (sx * .262, .335, 1.15), scale=(1, 1, 1.3)))                  # squeeze bulb
    P.add("BodyBooms", [s.tube([(0, .22, .94), (0, .24, 1.15), (0, .24, 1.3)], .02, res=1)])
    P.add("BodyBooms", [flare((0, .24, 1.28), (0, 0, 1), .07, .02, .045)])
    P.add("Eye", L.ball(.02, (0, .24, 1.37)))
    return P


def banshee(L):
    """Банши: the wailing spinning top — a toy top for a hover skirt with its point on the pivot, ribbons hanging from
    the waist, a ribbed corset of rings, long thin arms with claw fingers, and for a head a bouquet of six gramophone
    horns glowing in their throats in front of a sunburst crown. Concept: danger_s06/banshee_8051524 (the top skirt,
    horn ears)."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.03, .005), (.07, .04), (.19, .12), (.27, .19), (.28, .23), (.22, .28), (.12, .32),
                             (0, .33)]))
    P.add("Body", L.loft([(.2, .282, .282, 2.0), (.235, .282, .282, 2.0)]))
    P.add("Eye", L.loft([(.115, .175, .175, 2.0), (.13, .182, .182, 2.0)]))
    P.add("Body", L.fluted(.045, .3, 1.1, n=6))
    for k in range(16):
        a = k / 16 * math.tau
        ln = .72 if k % 2 else .58
        top = Vector((math.cos(a) * .12, math.sin(a) * .1 + .01, 1.1))
        bot = Vector((math.cos(a) * .27, math.sin(a) * .24 + .01, 1.1 - ln))
        rb = s.rbox((.1, .012, ln), r=.004, seg=1, taper=(.55, 1.0))
        bmesh.ops.rotate(rb, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(-a + math.pi / 2, 3, "Z"), verts=rb.verts)
        P.add("Body", [align(rb, bot, top)])                                                            # ribbons
    for k in range(7):
        z = 1.08 + k * .06
        f = k / 6
        a = .08 + .08 * math.sin(f * math.pi * .7)
        P.add("Accent" if k % 2 else "Body", L.loft([(z, a, a * .75, 2.4, 0, -.01 * f),
                                                    (z + .055, a * 1.04, a * .78, 2.4, 0, -.01 * f)], seg=14))
    P.add("Body", L.loft([(1.44, .22, .075, 3.0), (1.5, .24, .085, 3.2), (1.55, .16, .07, 3.0)], round_caps=.02))
    for sx in (-1, 1):
        sh, el, wr = (sx * .225, 0, 1.49), (sx * .25, .04, 1.1), (sx * .245, -.04, .76)
        P.add("Accent", L.ball(.045, sh)).add("Body", L.rod(sh, el, .028)).add("Accent", L.ball(.035, el))
        P.add("Body", L.rod(el, wr, .022)).add("Accent", L.ball(.03, wr))
        for dx, dy in ((-.02, -.03), (0, -.035), (.02, -.025)):
            tip = (wr[0] + dx * .8, wr[1] + dy * 1.4, wr[2] - .19)
            P.add("Accent", [align(s.rcyl(.01, .19, seg=6, r=0, radius2=.003), Vector(wr), Vector(tip))])  # claws
    P.add("Body", L.lathe([(.04, 1.5), (.042, 1.56), (.034, 1.6), (.042, 1.64), (.034, 1.68), (.04, 1.74), (0, 1.74)]))
    P.add("Body", L.ball(.09, (0, 0, 1.81)))
    for sx in (-1, 1):
        for b, d, ln, bell in (((sx * .04, -.05, 1.8), (sx * .3, -1, .02), .25, .1),
                               ((sx * .07, 0, 1.79), (sx * 1, -.18, -.05), .22, .08),
                               ((sx * .035, -.01, 1.86), (sx * .55, -.45, .7), .19, .07)):
            a, e = horn(b, d, ln, bell)
            P.add("Accent", [a]).add("Eye", [e])
    P.add("Accent", L.sunburst((0, .07, 1.86), .1, .3, 9, a0=math.radians(10), a1=math.radians(170), width=.05, depth=.025))
    return P


def trough(x0, x1, cy, cz, r0, r1, a0, a1, n=8):
    """A thick curved sheet along x: the part of a hollow cylinder (axis x through (cy, cz)) between radii r0..r1 and
    angles a0..a1 (degrees, 270 = straight down)."""
    bm = bmesh.new()
    rows = []
    for rr in (r0, r1):
        row = []
        for k in range(n + 1):
            a = math.radians(a0 + (a1 - a0) * k / n)
            row.append([bm.verts.new((x, cy + rr * math.cos(a), cz + rr * math.sin(a))) for x in (x0, x1)])
        rows.append(row)
    inner, outer = rows
    for k in range(n):
        for a_, b_ in ((inner[k], inner[k + 1]), (outer[k + 1], outer[k])):
            bm.faces.new((a_[0], b_[0], b_[1], a_[1]))
        bm.faces.new((inner[k][0], outer[k][0], outer[k + 1][0], inner[k + 1][0]))
        bm.faces.new((inner[k + 1][1], outer[k + 1][1], outer[k][1], inner[k][1]))
    for k in (0, n):
        bm.faces.new((inner[k][0], inner[k][1], outer[k][1], outer[k][0]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def maniac(L):
    """Маньяк: the bogeyman — very tall and thin on stilt legs with long curled shoes, a hunched clock-case chest
    with a dial, a long narrow head thrust forward under a crooked stovepipe hat with one small monocle eye on the
    brow, long arms ending in a meat hook and a three-claw clamp, coat tails; on its back an empty brass birdcage
    cradle with a velvet lining, where a carried player lies across. Concept: danger_s06/maniac (the cage on the back)."""
    P = D.Parts()
    P.add("Body", L.loft([(.94, .14, .1, 2.6, 0, .02), (1.04, .19, .12, 2.8, 0, .02), (1.13, .15, .1, 2.6, 0, .01)],
                         round_caps=.03))
    P.add("Body", L.loft([(1.08, .13, .09, 2.6, 0, .03), (1.25, .11, .08, 2.6, 0, .02), (1.4, .2, .13, 3.2, 0, -.04),
                          (1.53, .26, .15, 3.4, 0, -.11), (1.62, .26, .14, 3.2, 0, -.16), (1.68, .2, .12, 3.0, 0, -.2),
                          (1.72, .1, .08, 2.4, 0, -.22)], round_caps=.02))
    P.add("Accent", L.loft([(1.36, .2, .13, 3.2, 0, -.03), (1.39, .205, .135, 3.2, 0, -.035)]))
    face, rim = L.dial(.075, (0, -.23, 1.45))
    P.add("Body", face).add("Accent", rim)
    for sx in (-1, 1):
        P.add("Body", [align(s.rbox((.09, .012, .32), r=.004, seg=1, taper=(.4, 1.0)), Vector((sx * .07, .14, .78)),
                             Vector((sx * .09, .11, 1.08)))])                                         # coat tails
    P.add("Body", L.rod((0, -.18, 1.62), (0, -.26, 1.7), .045))
    P.add("Body", L.loft([(-.06, 0, 0), (-.05, .05, .055, 2.4), (0, .07, .08, 2.6), (.07, .065, .075, 2.6),
                          (.12, .045, .05, 2.4), (.16, 0, 0)], axis="-Y", loc=(0, -.25, 1.72)))       # long head
    P.add("Accent", L.lathe([(0, 0), (.1, 0), (.11, .015), (.075, .03), (.08, .2), (.088, .22), (0, .22)],
                            loc=(0, -.25, 1.78)))                                                      # stovepipe hat
    P.add("Body", L.loft([(.035, .084, .084, 2.0), (.065, .086, .086, 2.0)], loc=(0, -.25, 1.78)))
    P.add("Eye", L.ball(.024, (0, -.355, 1.775)))
    P.add("Accent", [ring(.034, .009, (0, -.36, 1.778), normal=(0, -1, .55), seg=14, seg_r=5)])
    for sx in (-1, 1):
        sh, el, wr = Vector((sx * .26, -.16, 1.56)), Vector((sx * .32, -.1, 1.14)), Vector((sx * .29, -.24, .78))
        P.add("Accent", L.ball(.06, tuple(sh))).add("Body", L.rod(sh, el, .04)).add("Accent", L.ball(.045, tuple(el)))
        P.add("Body", L.rod(el, wr, .035)).add("Accent", L.cyl(.045, .06, tuple(wr)))
        if sx < 0:                                                                                     # meat hook
            st = wr + Vector((0, -.02, -.16))
            P.add("Accent", L.rod(wr, st, .018))
            hook = [st, st + Vector((0, -.05, -.07)), st + Vector((0, -.02, -.16)), st + Vector((0, .07, -.17)),
                    st + Vector((0, .1, -.1))]
            P.add("Accent", [s.tube([tuple(p) for p in hook], .022, res=2, radii=[1, 1, .9, .7, .3])])
        else:                                                                                          # claw clamp
            palm = wr + Vector((0, -.03, -.08))
            P.add("Accent", L.box((.08, .1, .1), tuple(palm)))
            for a in (-50, 70, 190):
                d = Vector((math.cos(math.radians(a)) * .5, math.sin(math.radians(a)) * .5 - .2, -1)).normalized()
                b = palm + Vector((math.cos(math.radians(a)) * .03, math.sin(math.radians(a)) * .03 - .01, -.04))
                P.add("Accent", [align(s.rcyl(.016, .22, seg=6, r=.003, radius2=.004), b, b + d * .22)])
    # the birdcage cradle across the back: U ribs, rails with ball finials, a velvet lining, struts to the shoulders
    cy, cz, cr, half = D.MANIAC_SEAT[1], 1.8, .16, .26
    for x in (-half, -half / 2, 0, half / 2, half):
        P.add("Accent", [s.tube([(x, cy + cr * math.cos(math.radians(a)), cz + cr * math.sin(math.radians(a)))
                                 for a in range(180, 361, 20)], .013, res=1, smooth=False)])
    for a in (180, 230, 310, 360):
        y, z = cy + cr * math.cos(math.radians(a)), cz + cr * math.sin(math.radians(a))
        P.add("Accent", L.rod((-half - .02, y, z), (half + .02, y, z), .012))
        if a in (180, 360):
            P.add("Accent", L.ball(.025, (-half - .03, y, z)) + L.ball(.025, (half + .03, y, z)))
    P.add("Body", [trough(-half + .02, half - .02, cy, cz, cr - .035, cr - .02, 200, 340, n=8)])       # velvet lining
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .12, -.1, 1.58), (sx * .14, .06, 1.66), .022))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.MANIAC_HIP_X
        P.add(leg, L.ball(.07, (x, .02, D.MANIAC_HIP_Z)))
        P.add(leg, L.lathe([(.04, D.MANIAC_KNEE), (.058, .7), (.062, .92), (.05, D.MANIAC_HIP_Z)], loc=(x, -.02, 0)))
        P.add(leg, L.steps([(.06, .025), (.045, .025)], loc=(x + sx * .01, -.04, D.MANIAC_KNEE - .03)))
        P.add(leg, L.lathe([(.028, .08), (.042, .3), (.038, D.MANIAC_KNEE)], loc=(x + sx * .02, 0, 0)))
        P.add(leg, L.loft([(-.06, .05, .045, 2.4), (.05, .055, .045, 2.4), (.2, .03, .03, 2.2), (.27, .012, .02, 2.0)],
                          axis="-Y", loc=(x + sx * .02, 0, .045)))                                    # long shoe
        P.add(leg, L.ball(.022, (x + sx * .02, -.28, .07)))                                            # curled toe
    P.split = {"AccentLeg0": D.MANIAC_KNEE, "AccentLeg1": D.MANIAC_KNEE}
    return P


DANGER_DESIGNS = {"robot_warden": warden, "robot_repairman": repairman, "robot_smoker": smoker, "robot_relay": relay,
                  "robot_nurse": nurse, "robot_mimic": mimic, "robot_salesman": salesman, "robot_gunner": gunner,
                  "robot_driller": driller, "robot_exterminator": exterminator, "robot_banshee": banshee,
                  "robot_maniac": maniac}


DESIGNS = {
    "robot_guard": guard, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_pir": pir, "security_camera": security_camera, "laser_emitter": laser_emitter, "sensor_plate": plate,
    "attach_eye": attach_eye,
}
DESIGNS.update(PROP_DESIGNS)
DESIGNS.update(DANGER_DESIGNS)
