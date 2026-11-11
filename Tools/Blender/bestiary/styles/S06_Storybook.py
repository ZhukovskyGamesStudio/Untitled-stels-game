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
    """Hotel luggage trolley: a low cabinet on four wheels, a fluted post with a dial-faced driver at the back,
    the crate on a brass deck under a tall arched hoop with a finial."""
    P = D.Parts()
    D.wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    P.add("Body", L.loft([(.15, .24, .36, 3.0), (.36, .26, .38, 3.0), (.42, .22, .34, 3.0)]))
    P.add("Accent", L.loft([(.33, .275, .395, 3.0), (.37, .275, .395, 3.0)]))
    for x, y in ((.31, .25), (-.31, .25), (.31, -.25), (-.31, -.25)):
        P.add("Accent", L.rod((x * .6, y, .12), (x * 1.02, y, .12), .028))
    P.add("Body", L.fluted(.07, .4, .96, loc=(0, .2), n=6))
    P.add("Accent", L.steps([(.3, .025), (.26, .025)], loc=(0, -.15, .9)))                          # deck
    P.add("Body", L.rod((0, .2, .7), (0, -.1, .91), .04))
    P.add("Accent", L.box((.44, .38, .3), (0, -.15, 1.1), r=.025))                                 # crate
    P.add("Accent", L.box((.46, .05, .32), (0, -.15, 1.1), r=.012))
    # the arched hoop over the crate
    pts = [(math.cos(a) * .28, -.15, 1.0 + math.sin(a) * .5) for a in [k / 10 * math.pi for k in range(11)]]
    P.add("Accent", [s.tube(pts, .024, res=1, segments_per=2)])
    P.add("Accent", L.finial((0, -.15, 1.49), h=.13, r=.04))
    # the driver: a dial face on the post
    P.add("Body", L.lathe([(0, .94), (.13, .94), (.14, .98), (.14, 1.2), (.12, 1.24), (0, 1.24)], loc=(0, .24, 0)))
    face, rim = L.dial(.1, (0, .1, 1.11))
    P.add("Body", face).add("Accent", rim)
    P.add("Eye", L.box((.1, .02, .022), (0, .08, 1.12), r=.008))
    P.add("Accent", L.key((0, .37, 1.11), .8))
    acc, glow = L.monocle(.04, (0, -.375, .33))
    P.add("Accent", acc).add("Eye", glow)
    return P


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
    return P


def p_water_cooler(L):
    """A fluted column fountain on a stepped base, the bottle on a stepped capital."""
    P = D.Parts()
    P.add("Body", L.steps([(.17, .05), (.14, .05)]) + L.fluted(.11, .1, .92, n=8) + L.steps([(.13, .04), (.16, .05)], loc=(0, 0, .91)))
    P.add("Body", L.rod((0, -.1, .8), (0, -.16, .78), .015))
    P.add("Eye", L.lathe([(0, 0), (.13, 0), (.15, .05), (.15, .3), (.08, .4), (.04, .45), (0, .45)], loc=(0, 0, .99)))
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
    P.add("Body", L.slab((.92, .02, 1.5), (0, .16, .78)) + [s.rcyl(.45, .3, (0, .01, 1.55), axis="Y", seg=20, r=.01)])
    P.add("Accent", L.sunburst((0, -.15, 1.55), .05, .4, 9, a0=0, a1=math.pi, width=.05, depth=.02))
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
    P.add("Accent", L.sunburst((0, -.37, 1.25), .03, .1, 9, a0=0, a1=math.tau, width=.025, depth=.015))
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


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_pir": pir, "security_camera": security_camera, "laser_emitter": laser_emitter, "sensor_plate": plate,
    "attach_eye": attach_eye,
}
DESIGNS.update(PROP_DESIGNS)
