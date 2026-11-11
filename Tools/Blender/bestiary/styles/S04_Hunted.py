"""S04 Hunted — «Туман и твид»: Victorian country-estate automata, tweed and brass, elegant and eerie.

Each robot is a figure from the estate in the fog: a lanky gamekeeper in a tailcoat and bowler, a caped stalker in a
top hat with a lantern, a foxhound with long ears and a sickle tail, a stag trophy crawling on the wall, a pram with an
ear-shaped hood and ear trumpets, a butler's tea trolley with a steamer trunk, a clockwork tortoise, a traction
engine with a canopy, a giant monocle on a cane, a flying duck decoy, a shotgun on shooting sticks, a pocket watch on
the ceiling. Language: tall and thin, stilt limbs, bowler domes, monocles with chains, canes with crooks, brass rims.
Body = tweed, Accent = brass.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import shapes as s
import props
from lang import Lang, align
import designs as D


def torus(R, r, loc=(0, 0, 0), seg=24, seg_r=10, axis="Z", arc=1.0, a0=0.0):
    """A ring of major radius R and tube radius r about the axis through loc; arc < 1 gives a capped arc from a0."""
    bm = bmesh.new()
    rings = []
    n = seg if arc >= 1.0 else seg + 1
    for i in range(n):
        a = a0 + i / seg * math.tau * arc
        c, sn = math.cos(a), math.sin(a)
        rings.append([bm.verts.new(((R + r * math.cos(b)) * c, (R + r * math.cos(b)) * sn, r * math.sin(b)))
                      for b in [j / seg_r * math.tau for j in range(seg_r)]])
    pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if arc >= 1.0 else [])
    for a, b in pairs:
        for j in range(seg_r):
            bm.faces.new((a[j], b[j], b[(j + 1) % seg_r], a[(j + 1) % seg_r]))
    if arc < 1.0:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


AX = {"-Y": Vector((0, -1, 0)), "Y": Vector((0, 1, 0)), "X": Vector((1, 0, 0)), "-X": Vector((-1, 0, 0)),
      "Z": Vector((0, 0, 1)), "-Z": Vector((0, 0, -1))}


class Style(Lang):
    name = "S04_Hunted"
    seg = 14
    soft = 0.05
    edge = 0.014
    hull_kind = "rounded"
    eye_kind = "lens"
    limb_kind = "stilt"
    limb_scale = 0.85

    def rseg(self):
        return 2 if self.edge > .02 else 1

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        """Balls are the costliest form: few segments, more only on big ones."""
        return [s.ball(radius, loc, seg=16 if radius > .12 else 12 if radius > .06 else 8, scale=scale)]

    # ------------------------------------------------------------------ the estate vocabulary

    def bowler(self, loc, r=.13, h=.13):
        """A bowler hat: a domed crown, a band and a brim curled up at the sides. (crown, band)"""
        x, y, z = loc
        crown = [s.lathe([(0, 0), (r, 0), (r * 1.02, h * .35), (r * .92, h * .8), (r * .62, h), (0, h * 1.04)], seg=self.seg, loc=loc)]
        brim = s.lathe([(r * .9, -.004), (r * 1.45, -.006), (r * 1.6, .012), (r * 1.62, .03), (r * 1.52, .018), (r * 1.4, .008),
                        (r * .9, .008)], seg=self.seg, loc=loc)
        s.place(brim, scale=None)
        for v in brim.verts:  # curl the brim up at the sides, down at front and back
            ang = math.atan2(v.co.y - y, v.co.x - x)
            d = (Vector((v.co.x - x, v.co.y - y, 0))).length
            v.co.z += max(0, d - r) * .35 * (math.cos(ang) ** 2 - .4)
        crown.append(brim)
        band = [s.lathe([(r * 1.0, .012), (r * 1.035, .014), (r * 1.035, .045), (r * 1.0, .048)], seg=self.seg, loc=loc)]
        return crown, band

    def tophat(self, loc, r=.11, h=.32):
        x, y, z = loc
        crown = [s.lathe([(0, 0), (r, 0), (r * .96, h * .5), (r * 1.06, h), (0, h)], seg=self.seg, loc=loc)]
        crown.append(s.lathe([(r * .9, -.005), (r * 1.85, -.008), (r * 1.95, .015), (r * 1.85, .025), (r * .9, .01)], seg=self.seg, loc=loc))
        band = [s.lathe([(r * .99, .015), (r * 1.01, .015), (r * 1.0, .07), (r * .98, .07)], seg=self.seg, loc=loc)]
        return crown, band

    def monocle(self, r, loc, axis="-Y", chain=True):
        """(brass, glow): a monocle — a fine thick brass ring, a domed lens and a chain hanging to the side."""
        loc = Vector(loc)
        a = AX[axis]
        ring = [s.lathe([(r * .82, 0), (r * 1.22, 0), (r * 1.28, .018), (r * 1.22, .036), (r * .9, .036)], seg=self.seg, axis=axis,
                        loc=loc - a * .012)]
        lens = [s.lathe([(r * .95, 0), (r * .9, .012), (r * .5, .026), (0, .03)], seg=self.seg, axis=axis, loc=loc + a * .014)]
        if chain:
            side = Vector((1, 0, 0)) if abs(a.x) < .5 else Vector((0, 1, 0))
            p0 = loc + side * r * 1.15 - Vector((0, 0, r * .3))
            ring.append(s.tube([tuple(p0), tuple(p0 + side * .03 - Vector((0, 0, .1)) + a * .01), tuple(p0 + side * .06 - Vector((0, 0, .15)) - a * .03)],
                               .007, res=1))
        return ring, lens

    def cane(self, top, bottom, r=.018, crook=.06, crook_dir=(0, -1, 0)):
        """A walking cane from bottom (ferrule) up to top, with a crook handle bending towards crook_dir. (wood, brass)"""
        t, b = Vector(top), Vector(bottom)
        d = Vector(crook_dir).normalized()
        wood = [align(s.rcyl(r, (t - b).length, seg=10, r=r * .3), b, t)]
        pts = [tuple(t - Vector((0, 0, .01)))]
        for k in range(1, 8):
            ang = k / 7 * math.pi
            pts.append(tuple(t + d * crook * (1 - math.cos(ang)) + Vector((0, 0, crook * math.sin(ang)))))
        wood.append(s.tube(pts, r, res=2))
        brass = [align(s.rcyl(r * 1.25, .06, seg=10, r=.004), b - Vector((0, 0, .005)), b + Vector((0, 0, .055)))]
        brass.append(align(s.rcyl(r * 1.3, .03, seg=10, r=.004), t - Vector((0, 0, .03)), t))
        return wood, brass

    def watch(self, r, loc, axis="-Y"):
        """A pocket watch facing along axis: (case, face, glow) — a domed case, a dial, brass crown and bow ring at +z
        (or +y for a face looking down)."""
        loc = Vector(loc)
        a = AX[axis]
        up = Vector((0, 0, 1)) if abs(a.z) < .5 else Vector((0, -1, 0))
        case = [s.lathe([(0, -.03), (r * .8, -.03), (r, -.005), (r * 1.02, .02), (r * .96, .04), (0, .04)], seg=self.seg, axis=axis, loc=loc)]
        case.append(s.rcyl(r * .12, r * .3, loc + up * r * 1.08, axis="Z" if abs(up.z) > .5 else "Y", seg=12, r=.004))
        case.append(torus(r * .3, r * .065, loc + up * r * 1.45, seg=16, seg_r=6, axis="Y" if abs(up.z) > .5 else "Z"))
        face = [s.lathe([(0, .036), (r * .9, .036), (r * .9, .044), (0, .046)], seg=self.seg, axis=axis, loc=loc)]
        side = a.cross(up)
        for k in range(12):
            t = k / 12 * math.tau
            p = loc + a * .047 + (up * math.cos(t) + side * math.sin(t)) * r * .76
            face.append(s.ball(r * (.06 if k % 3 == 0 else .035), p, seg=6))
        hands = [align(s.rbox((r * .07, r * .07, r * .6), r=.004, seg=1), loc + a * .05, loc + a * .05 + up * r * .62),
                 align(s.rbox((r * .08, r * .08, r * .45), r=.004, seg=1), loc + a * .052, loc + a * .052 + (side * .8 - up * .3).normalized() * r * .45)]
        glow = [s.lathe([(0, .05), (r * .1, .05), (r * .09, .06), (0, .065)], seg=12, axis=axis, loc=loc)]
        return case, face + hands, glow

    def wheel(self, center, radius, width):
        """A thin pram / carriage wheel: a slim rubber tyre, a brass hub and fine spokes (symmetric about its centre)."""
        c = Vector(center)
        big = radius > .15
        out = [torus(radius - .025, .028 if big else .022, c, seg=16 if big else 12, seg_r=6, axis="X")]
        out.append(s.lathe([(radius * .25, -width / 2), (radius * .3, -width * .3), (radius * .3, width * .3), (radius * .25, width / 2)],
                           seg=10, axis="X", loc=c))
        out.append(s.rcyl(radius * .18, width * 1.05, c, axis="X", seg=10, r=.006))
        n = 10 if big else 6
        for k in range(n):
            a = (k + .5) / n * math.tau
            d = Vector((0, math.cos(a), math.sin(a)))
            out.append(align(s.rcyl(.011, radius * .72, seg=4, r=0), c + d * radius * .2, c + d * (radius - .02)))
        return out

    def track(self, size, loc):
        """A crawler track along Y (w, d, h): a stadium belt and road wheels (Lang.track stands the belt upright)."""
        w, d, h = size
        x, y, z = loc
        r = h / 2
        out = [s.loft([(-w / 2, r, d / 2, 6.0), (w / 2, r, d / 2, 6.0)], seg=self.seg + 8, axis="X", loc=loc)]
        for k in range(4):
            out.append(s.rcyl(r * .62, w * 1.12, (x, y - d / 2 + r + (d - 2 * r) * k / 3, z), axis="X", seg=16, r=.01))
        return out

    # ------------------------------------------------------------------ furniture vocabulary: Victorian estate
    leg_kind = "cabriole"
    edge_kind = "eased"
    cushion_puff = .6
    handle_kind = "bail"
    foliage_kind = "blob"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """Boards get a moulded edge: a lip under the top; panels are eased planks."""
        w, d, h = size
        x, y, z = loc
        out = [s.rbox(size, loc, r=min(min(size) * .3, .008), seg=1, rot=rot)]
        if h <= .08 and min(w, d) > .3 and not any(rot):
            out.append(s.rbox((w + .016, d + .016, h * .4), (x, y, z + h * .25), r=min(.008, h * .18), seg=1))
        return out

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A cabriole leg: the knee swells out, the ankle tucks in, a ball-and-claw foot on the floor."""
        x, y, z = top
        out_dir = Vector((x, y, 0))
        out_dir = out_dir.normalized() if out_dir.length > 1e-3 else Vector((0, -1, 0))
        fz = max(0.0, z - length)
        k = thick * .6
        pts = [(x, y, z), tuple(Vector((x, y, z - length * .25)) + out_dir * k), tuple(Vector((x, y, fz + length * .2)) - out_dir * k * .4),
               tuple(Vector((x, y, fz + thick * .9)) + out_dir * k * .5)]
        out = [s.tube(pts, thick * .5, res=1, segments_per=2, radii=[1.1, 1.0, .6, .7])]
        c = Vector((x, y, fz)) + out_dir * k * .5
        out.append(s.lathe([(0, 0), (thick * .5, 0), (thick * .55, thick * .5), (thick * .3, thick * 1.0), (0, thick * 1.05)], seg=8, loc=c))
        for a in (-.7, 0, .7):
            d = Matrix.Rotation(a, 3, "Z") @ out_dir
            out.append(align(s.rcyl(thick * .16, thick * .7, seg=5, r=0), c + Vector((0, 0, thick * .5)), c + d * thick * .65 + Vector((0, 0, thick * .1))))
        return out

    def carcass(self, size, loc, kick=0.06):
        """A cabinet on four short cabriole feet with a moulded cornice."""
        w, d, h = size
        x, y, z = loc
        kick = max(kick, .06)
        out = [s.rbox((w, d, h - kick), (x, y, z + kick + (h - kick) / 2), r=.008, seg=1)]
        out.append(s.rbox((w + .02, d + .014, .04), (x, y, z + h - .02), r=.008, seg=1))
        out.append(s.rbox((w + .01, d + .008, .03), (x, y, z + kick + .015), r=.006, seg=1))
        for sx in (-1, 1):
            for sy in (-1, 1):
                out += self.leg((x + sx * (w / 2 - .05), y + sy * (d / 2 - .05), z + kick + .01), kick + .01, .06)
        return out

    def door(self, size, loc, face="-Y"):
        """A raised and fielded panel framed by a bead."""
        w, h = size
        x, y, z = loc
        out = [s.rbox((w - .012, .022, h - .012), (x, y, z), r=.005, seg=1)]
        out.append(s.rbox((max(.03, w - .1), .016, max(.03, h - .1)), (x, y - .01, z), r=.008, seg=1))
        return out

    def handle(self, loc, length=0.12, vertical=True):
        """A brass bail pull: an oval backplate and a hanging half ring."""
        x, y, z = loc
        return [s.place(s.rcyl(.03, .012, (0, 0, 0), axis="Y", seg=10, r=.003), (x, y + .004, z), scale=(1.6, 1, 1)),
                torus(.035, .008, (x, y - .01, z - .005), seg=10, seg_r=5, axis="Y", arc=.5, a0=math.pi)]

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """Buttoned leather: a plump block with deep buttons on its biggest face."""
        w, d, h = size
        x, y, z = loc
        p = self.cushion_puff
        bm = s.loft([(-h / 2, w / 2, d / 2, 4.0 - 1.5 * p), (0, w / 2 * 1.035, d / 2 * 1.035, 4.0 - 1.5 * p), (h / 2, w / 2, d / 2, 4.0 - 1.5 * p)],
                    seg=16, round_caps=min(h, w, d) * .35)
        out = [s.place(bm, loc, rot)]
        if any(rot) or max(w, d, h) < .25:
            return out
        if h <= min(w, d):   # lying: buttons on the top
            for i in (-1, 0, 1):
                for j in (-.5, .5):
                    out.append(s.ball(.018, (x + i * w * .3, y + j * d * .45, z + h / 2 + .002), seg=6))
        else:                # standing: buttons on the face
            for i in (-1, 0, 1):
                for j in (-.5, .5):
                    out.append(s.ball(.018, (x + i * w * .3, y - d / 2 - .002, z + j * h * .45), seg=6))
        return out

    def screen(self, size, loc, face="-Y"):
        """A screen in a heavy gilt picture frame."""
        acc, glow = Lang.screen(self, size, loc, face)
        w, h = size
        x, y, z = loc
        acc.append(s.rbox((w + .09, .03, h + .09), (x, y + .012, z), r=.01, seg=1))
        acc.append(s.rbox((w + .05, .02, h + .05), (x, y - .002, z), r=.006, seg=1))
        return acc, glow

    def shade(self, r_bottom, r_top, height, loc):
        """A fringed bell shade: a shell open below (pole to pole) with a scalloped fringe ring."""
        h = height
        x, y, z = loc
        prof = [(0, h + .004), (r_top, h), (r_top * 1.05, h * .8), (r_bottom * .9, h * .25), (r_bottom, .015), (r_bottom - .008, .008),
                (r_bottom * .9 - .008, h * .25), (r_top * 1.05 - .008, h * .8 - .004), (0, h - .008)]
        out = [s.lathe(prof, seg=16, loc=loc)]
        out.append(s.lathe([(r_bottom - .004, -.03), (r_bottom + .006, -.03), (r_bottom + .004, .02), (r_bottom - .006, .02)], seg=16, loc=loc))
        return out

    def pot(self, r, h, loc):
        """A jardiniere: a bellied bowl on three claw feet."""
        x, y, z = loc
        out = [s.lathe([(0, h * .1), (r * .7, h * .1), (r, h * .5), (r * .9, h * .85), (r * 1.05, h), (0, h)], seg=16, loc=loc)]
        for k in range(3):
            a = k / 3 * math.tau
            out.append(s.ball(r * .22, (x + math.cos(a) * r * .6, y + math.sin(a) * r * .6, z + r * .23), seg=8))
        return out

    def signature(self, model, P):
        if model == "laser_emitter":  # a brass-capped cane post with a crook
            wood, brass = self.cane((0, 0, 1.6), (0, 0, 1.5), r=.045, crook=.08, crook_dir=(0, 1, 0))
            P.add("Body", wood + brass)
        if model == "attach_mic":  # an ear trumpet bell
            P.add("Accent", [s.lathe([(.03, .2), (.05, .23), (.08, .26), (.085, .27), (.06, .265), (.03, .25)], seg=16)])
        if model == "attach_seismic":  # a cane's crook on the probe
            P.add("Accent", [s.tube([(0, 0, .3), (0, .0, .36), (0, -.05, .38), (0, -.08, .35)], .018, res=1)])
        if model == "attach_nose":  # a brass muzzle ring
            P.add("Accent", [s.loft([(.07, .1, .096, 2.0), (.12, .096, .092, 2.0)], seg=20, axis="-Y")])
        if model == "sensor_plate":  # brass corner rosettes
            for x in (-1, 1):
                for y in (-1, 1):
                    P.add("Accent", [s.rcyl(.06, .012, (x * .49, y * .49, .014), seg=16, r=.003)])
        return P


# =========================================================================== walkers

def guard(L):
    """The gamekeeper: a lanky gentleman in a tailcoat with long tails, a waistcoat with a watch chain, a gaunt head
    leaning forward under a bowler, a monocle on a chain, a cane, stilt legs in spats and pointed shoes."""
    P = D.Parts()
    P.add("Body", L.loft([(.84, .2, .14, 2.4), (.95, .2, .15, 2.4), (1.04, .18, .14, 2.4)], round_caps=.02))       # trousers top
    P.add("Body", L.loft([(1.0, .19, .145, 2.6), (1.2, .21, .15, 2.6, 0, -.01), (1.42, .26, .16, 2.6, 0, -.01), (1.56, .28, .16, 2.6),
                          (1.63, .2, .13, 2.4), (1.68, .09, .08, 2.0)], round_caps=.02))                         # tailcoat
    P.add("Body", L.loft([(1.0, .02, .03, 2.0, 0, -.13), (1.06, .1, .06, 2.4, 0, -.1), (1.22, .15, .07, 2.4, 0, -.095),
                          (1.42, .19, .07, 2.4, 0, -.1), (1.46, .12, .05, 2.4, 0, -.11)]))                   # waistcoat
    for z in (1.12, 1.22, 1.32):
        P.add("Accent", [s.ball(.022, (0, -.168, z), seg=10)])
    P.add("Accent", [s.tube([(-.1, -.165, 1.18), (0, -.18, 1.13), (.1, -.165, 1.18)], .012, res=1)])             # watch chain
    for sx in (-1, 1):                                                                                     # coat tails
        P.add("Body", L.loft([(.56, .045, .016, 2.2, sx * .07, .2), (.8, .06, .022, 2.2, sx * .08, .18), (1.05, .08, .03, 2.2, sx * .09, .13)]))
    P.add("Body", L.cyl(.08, .07, (0, -.02, 1.66)))                                                          # collar
    P.add("Body", L.rod((0, -.02, 1.66), (0, -.07, 1.76), .035))                                             # neck
    P.add("Body", L.loft([(1.72, .0, .0), (1.74, .07, .08, 2.0, 0, -.08), (1.82, .095, .11, 2.0, 0, -.08), (1.92, .09, .1, 2.0, 0, -.07),
                          (1.96, .0, .0, 2.0, 0, -.07)]))                                                    # gaunt head
    acc, glow = L.monocle(.04, (.045, -.195, 1.85))
    P.add("Accent", acc).add("Eye", glow)
    P.add("Eye", L.box((.03, .02, .015), (-.045, -.185, 1.85), r=.006))
    P.add("Body", [s.loft([(0, .0, .0), (.02, .06, .015, 2.0), (.05, .05, .012, 2.0), (.06, 0, 0)], seg=12, axis="-Y",
                          loc=(0, -.17, 1.79))])                                                             # moustache
    crown, band = L.bowler((0, -.07, 1.92), r=.125, h=.13)
    P.add("Body", crown).add("Accent", band)
    for sx in (-1, 1):
        sh = (sx * .27, 0, 1.56)
        P.add("Body", L.rod(sh, (sx * .31, .02, 1.2), .045) + L.rod((sx * .31, .02, 1.2), (sx * .3, -.06, .9), .04))
        P.add("Accent", L.ball(.04, (sx * .31, .02, 1.2)))
        P.add("Body", L.ball(.05, (sx * .3, -.07, .87)))                                                     # glove
    wood, brass = L.cane((.3, -.1, .9), (.3, -.12, .14), r=.017, crook=.05, crook_dir=(-1, 0, 0))
    P.add("Accent", brass + wood)
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.065, (x, 0, .9)))
        P.add(leg, L.loft([(D.GUARD_KNEE, .055, .055, 2.0), (.7, .07, .07, 2.0), (.92, .075, .075, 2.0)], loc=(x, 0, 0)))
        P.add(leg, L.ball(.05, (x, 0, D.GUARD_KNEE)))
        P.add(leg, L.loft([(.08, .045, .045, 2.0), (D.GUARD_KNEE + .02, .052, .052, 2.0)], loc=(x, 0, 0)))
        P.add(leg, L.loft([(.03, .05, .06, 2.0), (.14, .045, .05, 2.0), (.18, .035, .04, 2.0)], loc=(x, -.01, 0)))   # spat
        P.add(leg, [s.loft([(0, .0, .0, 2.0, 0, .01), (.04, .045, .025, 2.2, 0, .02), (.2, .055, .035, 2.4, 0, .035),
                            (.32, .04, .03, 2.4, 0, .03), (.33, 0, 0, 2.0, 0, .025)], seg=16, axis="-Y", loc=(x, .07, .0))])  # pointed shoe
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """The caped stalker: a floor-length Inverness coat with a shoulder cape glides through the fog, a gaunt head
    under a tall top hat with a monocle, one long arm holding up a glowing lantern, the other a furled umbrella."""
    P = D.Parts()
    P.add("Body", L.loft([(.02, .4, .34, 2.2), (.25, .37, .31, 2.2), (.8, .29, .24, 2.2), (1.2, .25, .2, 2.2), (1.45, .25, .19, 2.2)]))
    P.add("Body", L.loft([(1.12, .4, .32, 2.2, 0, -.01), (1.3, .43, .34, 2.2, 0, -.01), (1.55, .37, .29, 2.2), (1.72, .27, .22, 2.2),
                          (1.8, .14, .12, 2.0)]))                                                           # shoulder cape
    P.add("Accent", L.loft([(.0, .41, .35, 2.2), (.04, .41, .35, 2.2)]))                                      # hem
    for z in (.5, .85, 1.1):
        b = .34 + (z - .02) / .23 * -.03 if z < .25 else .31 + (z - .25) / .55 * -.07 if z < .8 else .24 + (z - .8) / .4 * -.04
        P.add("Accent", [s.ball(.025, (0, -b - .008, z), seg=10)])
    P.add("Body", L.rod((0, -.03, 1.78), (0, -.08, 1.86), .045))
    P.add("Body", L.loft([(1.8, .0, .0), (1.82, .08, .09, 2.0, 0, -.1), (1.9, .105, .12, 2.0, 0, -.1), (2.0, .1, .11, 2.0, 0, -.1),
                          (2.05, .0, .0, 2.0, 0, -.1)]))                                                    # gaunt head
    acc, glow = L.monocle(.045, (.04, -.23, 1.92))
    P.add("Accent", acc).add("Eye", glow)
    P.add("Eye", L.box((.035, .02, .015), (-.05, -.215, 1.92), r=.006))
    crown, band = L.tophat((0, -.1, 2.0), r=.105, h=.34)
    P.add("Body", crown).add("Accent", band)
    # right arm: the lantern held out in front
    P.add("Body", L.rod((.3, -.12, 1.4), (.38, -.35, 1.25), .045) + L.rod((.38, -.35, 1.25), (.32, -.55, 1.3), .04))
    P.add("Body", L.ball(.05, (.32, -.57, 1.3)))
    P.add("Accent", L.rod((.32, -.57, 1.27), (.32, -.57, 1.15), .014))
    P.add("Accent", L.lathe([(0, .9), (.1, .9), (.11, .94), (.075, .96), (.075, 1.1), (.1, 1.11), (.06, 1.17), (0, 1.18)], loc=(.32, -.57, 0)))
    P.add("Eye", L.lathe([(0, .95), (.085, .95), (.085, 1.11), (0, 1.11)], loc=(.32, -.57, 0)))
    # left arm: a furled umbrella planted like a cane
    P.add("Body", L.rod((-.3, -.12, 1.4), (-.38, -.3, 1.15), .045) + L.rod((-.38, -.3, 1.15), (-.36, -.42, .95), .04))
    P.add("Body", L.ball(.05, (-.36, -.44, .93)))
    wood, brass = L.cane((-.36, -.45, .98), (-.36, -.45, .12), r=.016, crook=.05, crook_dir=(1, 0, 0))
    P.add("Accent", brass + wood)
    P.add("Body", [s.loft([(.2, .0, .0), (.3, .045, .045, 2.0), (.62, .03, .03, 2.0), (.72, .018, .018, 2.0)], seg=8, loc=(-.36, -.45, 0))])
    return P


def hound(L):
    """Foxhound: a deep narrow chest and a tucked belly, a long neck and head, long hanging ears, a sickle tail held
    high, slender legs with brass hocks."""
    P = D.Parts()
    P.add("Body", L.loft([(-.55, .0, .0), (-.5, .09, .14), (-.32, .13, .2, 2.0, 0, .05), (-.05, .12, .16, 2.0, 0, .02), (.22, .1, .1, 2.0, 0, -.04),
                          (.42, .1, .11, 2.0, 0, -.03), (.52, .0, .0)], axis="Y", loc=(0, 0, .84)))
    P.add("Body", L.ball(.13, (0, .32, .79), scale=(1.15, 1.1, .9)))                                       # haunches
    P.add("Body", L.rod((0, -.45, .92), (0, -.62, 1.08), .07))
    P.add("Body", L.loft([(0, .075, .085, 2.0), (.1, .08, .09, 2.0), (.22, .055, .06, 2.0), (.36, .035, .04, 2.0), (.38, .0, .0)], axis="-Y",
                         loc=(0, -.6, 1.02)))                                                               # long head
    P.add("Eye", L.ball(.03, (0, -.975, 1.02)))
    for sx in (-1, 1):
        P.add("Eye", L.box((.025, .02, .015), (sx * .055, -.72, 1.07), r=.006))
        ear = s.loft([(-.16, .03, .06, 2.0), (-.02, .04, .095, 2.0), (.1, .035, .085, 2.0), (.16, 0, 0)], seg=12)
        P.add("Body", [align(ear, Vector((sx * .08, -.64, 1.12)), Vector((sx * .3, -.66, .9)))])          # ear splayed out
    P.add("Body", [s.tube([(0, .46, .9), (0, .58, 1.0), (0, .6, 1.15), (0, .54, 1.24)], .028, res=2, radii=[1, .8, .6, .35])])  # sickle tail
    P.add("Body", [s.loft([(-.035, .1, .1, 2.0), (.035, .1, .1, 2.0)], seg=20, axis="-Y", loc=(0, -.55, 1.0))])   # collar
    P.add("Body", [s.rcyl(.045, .015, (0, -.6, .87), axis="Y", seg=12, r=.004), s.rcyl(.012, .05, (0, -.6, .92), seg=6, r=0)])  # brass tag
    P.add("Body", L.ball(.2, (0, -.4, .8), scale=(1.05, 1.0, 1.15)))                                     # deep chest
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        P.add("Body", L.ball(.085, (x * .85, y, .7 if y < 0 else .72)))
        leg = f"AccentLeg{i}"
        front = y < 0
        P.add(leg, L.ball(.055, (x, y, D.HOUND_HIP)))
        P.add(leg, L.loft([(D.HOUND_KNEE, .035, .04, 2.0), (.48, .05, .065, 2.0, 0, 0 if front else .015), (D.HOUND_HIP + .04, .055, .065, 2.0)],
                          loc=(x, y, 0)))
        P.add(leg, L.ball(.04, (x, y, D.HOUND_KNEE)))
        P.add(leg, L.loft([(.04, .025, .025, 2.0), (D.HOUND_KNEE + .02, .03, .03, 2.0)], loc=(x, y, 0)))
        P.add(leg, [s.loft([(0, .035, .05, 2.4), (.04, .03, .045, 2.4), (.05, 0, 0)], seg=12, loc=(x, y - .02, 0))])
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """Stag trophy: an oval shield plaque scuttles over the walls, a stag's head mounted on it with glowing eyes and
    great branching antlers rising away from the wall."""
    P = D.Parts()
    P.add("Body", [s.loft([(0, .19, .28, 2.0), (.04, .2, .29, 2.0), (.07, .17, .26, 2.0), (.1, .15, .23, 2.0)], seg=L.seg, loc=(0, .05, .0))])
    P.add("Body", L.lathe([(0, .08), (.12, .08), (.12, .14), (.09, .2), (0, .21)], loc=(0, .02, 0)))
    P.add("Body", L.loft([(0, .07, .07, 2.0), (.1, .075, .08, 2.0, 0, .02), (.2, .05, .05, 2.0, 0, .02), (.24, .0, .0, 2.0, 0, .02)], axis="-Y",
                         loc=(0, -.1, .18)))                                                                # stag head
    for sx in (-1, 1):
        P.add("Eye", L.ball(.022, (sx * .055, -.24, .22)))
        P.add("Body", [s.loft([(0, .015, .03, 2.0), (.07, .02, .04, 2.0), (.1, 0, 0)], seg=8, axis="X" if sx > 0 else "-X",
                              loc=(sx * .05, -.15, .25))])                                                 # ear
        base = Vector((sx * .04, -.13, .24))
        main = [base, base + Vector((sx * .08, .02, .14)), base + Vector((sx * .2, .06, .26)), base + Vector((sx * .26, .12, .38))]
        P.add("Body", [s.tube([tuple(p) for p in main], .02, res=1, radii=[1.2, 1, .8, .5])])
        for k, (f, up) in enumerate(((.35, .12), (.6, .14))):
            p = main[1].lerp(main[2], f) if k == 0 else main[2].lerp(main[3], f)
            P.add("Body", [s.tube([tuple(p), tuple(p + Vector((-sx * .02, -.04, up)))], .014, res=1, radii=[1, .5])])
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .025) + L.ball(.033, knee))
        P.add(f"AccentLeg{i}Lower", L.rod(knee + (foot - knee) * .02, foot + Vector((0, 0, .03)), .016)
              + [s.loft([(0, .0, .0), (.015, .025, .025, 2.0), (.05, .015, .015, 2.0)], seg=8, loc=foot - Vector((0, 0, .02)))])
    return P


# =========================================================================== wheeled

def listener(L):
    """The perambulator: a boat-shaped pram tub on four tall spoked wheels with C-springs, a folding hood open to the
    front like a great ear with a glowing receiver inside, two curled ear trumpets and a push handle."""
    P = D.Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    for y in (-.3, .3):
        P.add("Body", L.rod((-.5, y, .22), (.5, y, .22), .02))                                               # axles
        for sx in (-1, 1):
            P.add("Body", [s.tube([(sx * .3, y, .22), (sx * .34, y * .9, .33), (sx * .17, y * .75, .45)], .02, res=1, segments_per=3)])  # C-spring
    P.add("Body", L.loft([(.38, .22, .34, 3.0), (.46, .28, .42, 3.4), (.7, .31, .45, 3.6), (.76, .3, .44, 3.4)]))   # tub
    P.add("Body", L.loft([(.74, .315, .455, 3.5), (.79, .315, .455, 3.5)]))                                  # rim
    # the hood: a shell of revolution about Y, open to the front, its lower half hidden in the tub
    P.add("Body", L.lathe([(0, .3), (.18, .27), (.26, .16), (.28, .02), (.32, .0), (.32, .1), (.29, .22), (.2, .32), (0, .37)],
                          axis="Y", loc=(0, .02, .78)))
    for k in range(3):                                                                                      # hood bows
        P.add("Body", [torus(.31 + k * .005, .014, (0, .1 + k * .08, .78), seg=14, seg_r=5, axis="Y", arc=.5)])
    P.add("Eye", L.ball(.065, (0, .27, .84)))
    for sx in (-1, 1):                                                                                      # ear trumpets
        P.add("Body", [s.tube([(sx * .2, .25, .92), (sx * .3, .25, 1.05), (sx * .33, .15, 1.15), (sx * .36, .05, 1.17)], .025, res=1, segments_per=4,
                              radii=[.8, 1, 1.2, 1.6])])
        P.add("Body", L.lathe([(.035, 0), (.05, .03), (.09, .07), (.12, .09), (.11, .1), (.08, .085), (.04, .06), (0, .055)],
                              axis="X" if sx > 0 else "-X", loc=(sx * .36, .05, 1.17)))
    P.add("Body", [s.tube([(-.24, .4, .72), (-.25, .55, 1.0), (-.2, .6, 1.08), (.2, .6, 1.08), (.25, .55, 1.0), (.24, .4, .72)], .02, res=1, segments_per=3)])  # handle
    acc, glow = L.monocle(.05, (0, -.45, .58), chain=False)
    P.add("Body", acc).add("Eye", glow)
    return P


def worker(L):
    """The butler's tea trolley: two railed tiers on turned brass posts and four little wheels, a steamer trunk with
    straps on the top tier, a bowler-hatted head on a tall neck behind it, a carriage lamp at the front."""
    P = D.Parts()
    D.wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    for x, y in ((.31, .25), (-.31, .25), (.31, -.25), (-.31, -.25)):
        P.add("Accent", L.rod((x * .75, y, .12), (x * 1.1, y, .12), .02))
        P.add("Accent", L.lathe([(.025, .12), (.035, .3), (.025, .5), (.035, .7), (.025, .94)], loc=(x * .75, y, 0)))   # turned posts
    P.add("Body", L.box((.54, .62, .04), (0, 0, .32), r=.012))                                               # lower shelf
    P.add("Body", L.box((.56, .64, .04), (0, 0, .93), r=.012))                                               # tray
    P.add("Accent", [s.tube([(-.28, -.33, .95), (-.28, .33, .95), (.28, .33, .95), (.28, -.33, .95), (-.28, -.33, .95)], .012, smooth=False)])
    # the steamer trunk (crate): a domed lid, leather straps, brass corners
    P.add("Accent", L.box((.48, .4, .24), (0, -.15, 1.07), r=.02))
    P.add("Accent", L.loft([(0, .2, .12, 3.0), (.44, .2, .12, 3.0)], axis="X", loc=(-.22, -.15, 1.19)))
    for x in (-.12, .12):
        P.add("Accent", L.box((.05, .42, .3), (x, -.15, 1.13), r=.012))
    # the butler: a post neck, a gaunt head, a bowler, a monocle
    P.add("Body", L.lathe([(.04, .95), (.05, 1.2), (.035, 1.42)], loc=(0, .24, 0)))
    P.add("Body", L.loft([(1.4, .0, .0), (1.42, .07, .08, 2.0), (1.5, .09, .1, 2.0), (1.6, .08, .09, 2.0), (1.63, .0, .0)], loc=(0, .22, 0)))
    crown, band = L.bowler((0, .22, 1.6), r=.095, h=.1)
    P.add("Body", crown).add("Accent", band)
    acc, glow = L.monocle(.032, (.035, .115, 1.52), chain=False)
    P.add("Accent", acc).add("Eye", glow)
    for sx in (-1, 1):
        P.add("Body", L.rod((sx * .06, .24, 1.3), (sx * .2, .1, 1.24), .025) + L.ball(.035, (sx * .2, .08, 1.22)))
    P.add("Accent", L.rod((0, -.31, .34), (0, -.36, .45), .012))
    P.add("Accent", L.lathe([(0, .38), (.06, .38), (.065, .42), (.05, .43), (.05, .5), (.065, .51), (.03, .55), (0, .56)], loc=(0, -.38, 0)))
    P.add("Eye", L.lathe([(0, .42), (.055, .42), (.055, .5), (0, .5)], loc=(0, -.38, 0)))
    return P


def sweeper(L):
    """Clockwork tortoise: a high-domed shell with ring seams and a brass rim, a wrinkled neck stretched forward and a
    small head with a monocle, stubby feet, a pair of whisker brushes on the floor."""
    P = D.Parts()
    D.wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18), (0, -.3)):
        P.add("Accent", L.box((.04, .06, .24), (x - .045, y, .22), r=.01) + L.box((.04, .06, .24), (x + .045, y, .22), r=.01))
        P.add("Accent", L.rod((x - .06, y, .08), (x + .06, y, .08), .015))
    shell = s.loft([(.28, .4, .44, 2.3), (.36, .42, .46, 2.3), (.5, .37, .41, 2.2), (.6, .27, .3, 2.0), (.66, .14, .16, 2.0), (.68, .0, .0)],
                   seg=12)
    for axis, v in (("Z", .44), ("Z", .56), ("X", -.13), ("X", .13), ("Y", -.17), ("Y", .15)):
        s.groove(shell, axis, v, .016, .012)
    P.add("Body", [shell])
    P.add("Accent", L.loft([(.26, .43, .47, 2.3), (.3, .44, .48, 2.3), (.34, .42, .46, 2.3)]))               # marginal rim
    P.add("Accent", L.cyl(.07, .14, (0, 0, .3)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    P.add("Body", [s.tube([(0, -.35, .36), (0, -.48, .42), (0, -.55, .5)], .07, res=2)])                    # neck
    P.add("Body", L.loft([(0, .09, .085, 2.0), (.1, .085, .08, 2.0), (.17, .04, .04, 2.0), (.19, .0, .0)], axis="-Y", loc=(0, -.52, .54)))  # head
    acc, glow = L.monocle(.045, (.04, -.64, .56), chain=False)
    P.add("Accent", acc).add("Eye", glow)
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", [s.loft([(.03, .08, .08, 2.0), (.07, .085, .085, 2.0), (.32, .075, .075, 2.0)], seg=10, loc=(sx * .33, sy * .24, 0))])  # elephant feet
        c = (sx * .28, -.33, .02)
        for k in range(4):
            a = math.radians(-60 + k * 40) if sx > 0 else math.radians(240 - k * 40)
            P.add("Accent", [align(s.rbox((.012, .012, .14), r=.004, seg=1), Vector(c), Vector(c) + Vector((math.cos(a), math.sin(a), 0)) * .14)])
        P.add("Accent", L.rod((c[0], c[1], .02), (sx * .22, -.28, .32), .015))
    return P


def thumper(L):
    """Traction engine: a riveted boiler on crawler tracks, a tall chimney with a flared crown at the front, a canopy
    roof on brass posts over the footplate, a spoked flywheel, two brass-knobbed stamps hammering the pads."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.24, 1.36, .5), (sx * .56, 0, .26)))
    P.add("Body", L.box((1.0, 1.2, .2), (0, 0, .5), r=.04))                                                   # frame over the tracks
    P.add("Body", L.loft([(-.62, .3, .3, 2.0), (-.55, .32, .32, 2.0), (.3, .32, .32, 2.0), (.4, .36, .36, 2.0)], axis="Y", loc=(0, 0, .88)))  # boiler
    for y in (-.4, -.05, .25):
        P.add("Accent", L.loft([(-.02, .328, .328, 2.0), (.02, .328, .328, 2.0)], axis="Y", loc=(0, y, .88)))
    P.add("Body", L.box((.7, .4, .62), (0, .45, .95), r=.03))                                                 # firebox / cab
    P.add("Accent", L.lathe([(0, 0), (.24, 0), (.25, .02), (.2, .04), (0, .045)], axis="-Y", loc=(0, -.6, .88)))   # smokebox door
    P.add("Body", L.lathe([(0, 1.1), (.09, 1.1), (.08, 1.6), (.13, 1.72), (.15, 1.78), (.11, 1.8), (0, 1.8)], loc=(0, -.42, 0)))  # chimney
    P.add("Accent", L.loft([(1.69, .125, .125, 2.0), (1.73, .14, .14, 2.0)], loc=(0, -.42, 0)))
    for sx in (-1, 1):
        for y in (.0, .6):
            P.add("Accent", L.rod((sx * .32, y, .9 if y < .3 else 1.15), (sx * .32, y, 1.68), .022))
    P.add("Body", L.loft([(0, .4, .04, 2.0, 0, -.02), (.66, .4, .04, 2.0, 0, -.02)], axis="Y", loc=(0, -.03, 1.64)))   # canopy roof
    P.add("Body", [s.loft([(-.38, .03, .02, 2.0), (.0, .03, .06, 2.0), (.38, .03, .02, 2.0)], seg=8, axis="X", loc=(0, .3, 1.66))])
    # flywheel on the right side
    c = Vector((.4, .2, 1.05))
    P.add("Accent", [torus(.24, .03, c, seg=28, seg_r=8, axis="X")])
    for k in range(6):
        a = k / 6 * math.tau
        P.add("Accent", [align(s.rcyl(.015, .22, seg=6, r=0), c, c + Vector((0, math.cos(a), math.sin(a))) * .23)])
    P.add("Accent", L.cyl(.05, .1, c, axis="X"))
    P.add("Body", L.rod((.2, .2, 1.0), (.42, .2, 1.05), .045))
    # stamps
    P.add("Body", L.rod((-.36, -.62, .7), (.36, -.62, .7), .05))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", L.rod((x, -.62, .7), (x, -.85, .7), .045))
        P.add("Accent", L.rod((x, -.85, .78), (x, -.85, .1), .035) + L.ball(.07, (x, -.85, .82)))
        P.add("Accent", L.lathe([(0, .03), (.15, .03), (.12, .08), (.04, .11), (0, .11)], loc=(x, -.85, 0)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
    P.add("Accent", L.rod((0, -.6, .6), (0, -.72, .36), .012))
    P.add("Accent", L.lathe([(0, .25), (.06, .25), (.065, .29), (.05, .3), (.05, .37), (.065, .38), (.03, .42), (0, .43)], loc=(0, -.78, 0)))
    P.add("Eye", L.lathe([(0, .29), (.055, .29), (.055, .37), (0, .37)], loc=(0, -.78, 0)))
    return P


# =========================================================================== tall

def sorter(L):
    """A giant monocle on a cane: a claw-footed brass stand, a tall cane whose crook holds a huge monocle frame with
    the colour lens, its chain looping back down to the cane."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.29, .03), (.22, .07), (0, .08)]))
    for k in range(3):
        a = k / 3 * math.tau + math.pi / 2
        d = Vector((math.cos(a), math.sin(a), 0))
        P.add("Accent", [s.tube([tuple(d * .05 + Vector((0, 0, .3))), tuple(d * .18 + Vector((0, 0, .2))), tuple(d * .27 + Vector((0, 0, .06)))],
                                .022, res=2)])
        P.add("Accent", [s.ball(.035, d * .27 + Vector((0, 0, .06)), seg=12)])
    wood, brass = L.cane((0, .08, 1.48), (0, .08, .06), r=.03, crook=.11, crook_dir=(0, -1, 0))
    P.add("Body", wood).add("Accent", brass)
    for z in (.35, .7):
        P.add("Accent", L.cyl(.04, .03, (0, .08, z)))
    P.add("Accent", L.rod((0, -.14, 1.48), (0, -.14, 1.5), .03))
    P.add("Accent", L.lathe([(.2, 0), (.27, 0), (.29, .025), (.27, .05), (.2, .05)], axis="-Y", loc=(0, -.08, 1.25)))   # monocle frame
    P.add("Eye", L.lathe([(.205, 0), (.2, .02), (.12, .045), (0, .055)], axis="-Y", loc=(0, -.09, 1.25)))
    P.add("Body", L.lathe([(0, 0), (.2, 0), (.2, .02), (0, .02)], axis="-Y", loc=(0, -.06, 1.25)))
    P.add("Accent", L.rod((0, -.12, 1.52), (0, -.12, 1.47), .02))
    P.add("Accent", [s.tube([(.26, -.11, 1.2), (.3, -.05, 1.0), (.22, .02, .82), (.03, .08, .78)], .012, res=1)])   # chain
    return P


def drone(L):
    """Flying duck decoy: a carved duck body with its neck stretched out in flight, short swept wings, brass arms to
    four rotors in brass guard rings, a lens slung under the breast."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", L.loft([(-.34, .0, .0, 2.0, 0, .05), (-.28, .05, .03, 2.0, 0, .04), (-.12, .13, .07, 2.0, 0, .01), (.08, .15, .085, 2.0),
                          (.2, .1, .07, 2.0, 0, .01), (.24, .0, .0, 2.0, 0, .02)], axis="-Y"))                    # body, tail at +y
    P.add("Body", L.rod((0, -.18, .02), (0, -.3, .05), .045))
    P.add("Body", L.ball(.055, (0, -.32, .06), scale=(1, 1.2, 1)))
    P.add("Accent", [s.loft([(0, .025, .012, 2.0), (.08, .02, .006, 2.0), (.09, 0, 0)], seg=10, axis="-Y", loc=(0, -.37, .05))])   # bill
    P.add("Eye", L.box((.12, .015, .015), (0, -.34, .08), r=.005))
    for sx in (-1, 1):
        P.add("Body", [s.loft([(0, .05, .012, 2.0), (.15, .045, .01, 2.0), (.24, .02, .006, 2.0)], seg=10, axis="X" if sx > 0 else "-X",
                              loc=(sx * .1, .02, .03))])                                                 # wing
    for i, (x, y) in enumerate(rot):
        d = Vector((x, y, 0)).normalized()
        P.add("Accent", [align(s.rcyl(.016, .5, seg=8, r=0), d * .08 + Vector((0, 0, .01)), Vector((x, y, .02)) - d * .03)])
        P.add("Accent", L.cyl(.03, .06, (x, y, .03)))
        P.add("Body", [torus(.19, .012, (x, y, .065), seg=24, seg_r=6)])
        P.add("Accent", [align(s.rcyl(.008, .2, seg=6, r=0), Vector((x, y, .03)), Vector((x, y, .065)) + d * .19)])
        c = Vector((x, y, .065))
        blades = [s.rcyl(.025, .04, c, seg=10, r=.005)]
        for k in range(4):
            b = s.loft([(0, .02, .005), (.08, .035, .006), (.16, .02, .004)], seg=8, axis="X")
            s.place(b, rot=(math.radians(15), 0, k / 4 * math.tau + .3))
            blades.append(s.place(b, c))
        P.add(f"AccentRotor{i}", blades)
    P.add("Accent", L.rod((0, -.17, -.06), (0, -.24, -.08), .015))
    acc, glow = L.monocle(.04, (0, -.25, -.1), chain=False)
    P.add("Accent", L.ball(.045, (0, -.23, -.1))).add("Accent", acc).add("Eye", glow)
    return P


def turret(L):
    """Shotgun on shooting sticks: three walking canes splay into a tripod under a brass swivel, the head is a
    double-barrelled fowling piece with a long brass telescope like a monocle on a stalk."""
    P = D.Parts()
    for k in range(3):
        a = .5 + k * math.tau / 3
        foot = Vector((math.cos(a) * .34, math.sin(a) * .34, .0))
        wood, brass = L.cane((math.cos(a) * .07, math.sin(a) * .07, 1.02), tuple(foot), r=.048, crook=.0001)
        P.add("Body", wood[:1]).add("Body", brass)
        P.add("Eye", [s.ball(.022, foot + Vector((0, 0, .08)), seg=10)])
    P.add("Body", L.lathe([(0, .95), (.1, .95), (.11, 1.0), (.08, 1.04), (.06, 1.1), (0, 1.1)]))
    P.add("Eye", L.loft([(1.02, .095, .095, 2.0), (1.045, .095, .095, 2.0)]))
    P.add("AccentHead", L.lathe([(0, 1.09), (.07, 1.09), (.08, 1.13), (.06, 1.17), (0, 1.17)]))
    P.add("AccentHead", L.box((.16, .22, .12), (0, 0, 1.22), r=.03))                                         # action
    for sx in (-1, 1):
        P.add("AccentHead", L.cyl(.036, .5, (sx * .037, -.33, 1.25), axis="Y"))                              # barrels
    P.add("AccentHead", L.loft([(0, .06, .05, 2.4), (.08, .06, .065, 2.4, 0, -.02), (.14, .065, .085, 2.4, 0, -.06)], axis="Y",
                               loc=(0, .07, 1.22)))                                                         # stock
    P.add("AccentHead", L.rod((0, -.02, 1.23), (0, -.02, 1.36), .022))
    P.add("AccentHead", L.lathe([(0, 0), (.03, 0), (.025, .3), (.045, .34), (.045, .38), (0, .38)], axis="-Y", loc=(0, .12, 1.36)))  # telescope
    return P


def vacuum(L):
    """Pocket watch on the ceiling: a gold hunter case, the dial with its hours and hands looking down into the room
    (-z), the crown and bow ring at the front."""
    P = D.Parts()
    case, face, glow = L.watch(.34, (0, 0, .07), axis="-Z")
    P.add("Body", case[:1]).add("Accent", case[1:] + face).add("Eye", glow)
    P.add("Accent", L.loft([(.06, .355, .355, 2.0), (.1, .345, .345, 2.0)]))
    return P


# =========================================================================== devices

def security_camera(L):
    """A brass spyglass camera on a bracket, a monocle lens."""
    P = D.Parts()
    P.add("Body", L.box((.16, .06, .22), (0, .27, 0), r=.02))
    P.add("Accent", L.rod((0, .26, 0), (0, .08, -.02), .025))
    P.add("Accent", L.lathe([(0, 0), (.06, 0), (.065, .1), (.07, .14), (.08, .26), (.085, .3), (0, .3)], axis="-Y", loc=(0, .1, -.02)))
    acc, glow = L.monocle(.05, (0, -.205, -.02), chain=True)
    P.add("Accent", acc).add("Eye", glow)
    return P


def pir(L):
    """A brass-framed watch glass on a wall plate."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.08, 0), (.08, .02), (.07, .05), (0, .055)], axis="-Y", loc=(0, .08, 0)))
    acc, glow = L.monocle(.045, (0, .035, 0), chain=True)
    P.add("Body", acc).add("Eye", glow)
    return P


def lidar(L):
    """A brass mantel-clock drum on a turned post."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.18, 0), (.17, .03), (.06, .08), (.05, .2), (.07, .3), (.05, .36), (.09, .39), (0, .39)]))
    P.add("Eye", L.loft([(.38, .08, .08, 2.0), (.4, .08, .08, 2.0)]))
    P.add("AccentHead", L.lathe([(0, .395), (.08, .395), (.1, .42), (.1, .48), (.07, .51), (0, .52)]))
    acc, glow = L.monocle(.025, (0, -.1, .455), chain=False)
    P.add("AccentHead", acc + glow)
    return P


def attach_eye(L):
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.09, 0), (.1, .05), (.1, .12), (0, .13)], axis="-Y", loc=(0, .07, 0)))
    acc, glow = L.monocle(.055, (0, -.065, 0), chain=True)
    P.add("Accent", acc).add("Eye", glow)
    return P


# =========================================================================== furniture and level pieces
# A country estate after dark: claw-footed mahogany, buttoned leather, gilt frames, brass bail pulls, antlers and gun
# cabinets. (The realistic family swaps a dozen props for CC0 scans: desk, chair, cabinet, plant, bookshelf, shelf,
# boxes, sofa, armchair, coffee/round/meeting tables, trash bin, desk lamp — those keep the shared designs.)

def antlers(c, span=.4, rise=.35, tines=3, r=.022):
    """A pair of stag antlers rising from c, spreading sideways (x)."""
    c = Vector(c)
    out = []
    for sx in (-1, 1):
        main = [c, c + Vector((sx * span * .3, 0, rise * .3)), c + Vector((sx * span * .75, -.02, rise * .65)), c + Vector((sx * span, .02, rise))]
        out.append(s.tube([tuple(p) for p in main], r, res=1, segments_per=3, radii=[1.3, 1, .8, .45]))
        for k in range(tines):
            p = main[1].lerp(main[2], k / max(1, tines - 1)) if k < tines - 1 else main[2].lerp(main[3], .4)
            out.append(s.tube([tuple(p), tuple(p + Vector((-sx * .03, -.03, rise * .28)))], r * .75, res=1, segments_per=2, radii=[1, .4]))
    return out


def gilt_frame(w, h, loc, t=.05):
    """A picture frame (four mitred bars) standing in the XZ plane at loc."""
    x, y, z = loc
    return [s.rbox((w, .04, t), (x, y, z + h / 2 - t / 2), r=.01, seg=1), s.rbox((w, .04, t), (x, y, z - h / 2 + t / 2), r=.01, seg=1),
            s.rbox((t, .04, h - t * 2 + .01), (x - w / 2 + t / 2, y, z), r=.01, seg=1), s.rbox((t, .04, h - t * 2 + .01), (x + w / 2 - t / 2, y, z), r=.01, seg=1)]


def p_terminal(L):
    """Roll-top desk: a claw-legged desk with a curved roll-top hood at the back framing three gilt-framed screens and a
    brass telegraph key."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", L.four_legs(.72, .8, .4, .72, .06) + [s.rbox((1.5, .7, .1), (0, 0, .67), r=.01, seg=1)])
    hood = s.loft([(-.78, .34, .18, 2.0, 0, 0), (.78, .34, .18, 2.0, 0, 0)], seg=20, axis="X")
    P.add("AccentConsole", [s.place(hood, (0, .22, .77))])
    P.add("AccentConsole", L.slab((.4, .17, .025), (0, -.1, .775)) + [s.rcyl(.02, .04, (0, -.1, .8), seg=8, r=.004)])
    for x in (-.45, 0, .45):
        acc, glow = L.screen((.27, .2), (x, .03, .98), "-Y")
        P.add("Eye", glow).add("AccentConsole", acc)
    return P


def p_server_rack(L):
    """Longcase clock: a plinth, a narrow trunk with a glowing pendulum window, a hood with an arched pediment and a big
    dial."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, 1.0, .35), (0, 0, 0), kick=.08))
    P.add("Body", [s.rbox((.52, .96, 1.15), (0, 0, .92), r=.01, seg=1), s.rbox((.68, 1.0, .5), (0, 0, 1.69), r=.01, seg=1)])
    P.add("Body", [s.place(s.loft([(-.34, .14, .5, 2.0), (.34, .14, .5, 2.0)], seg=16, axis="X"), (0, 0, 1.94 - .14))])
    case, face, glow = L.watch(.19, (0, -.47, 1.62), axis="-Y")
    P.add("Body", case + face).add("Eye", glow)
    P.add("Eye", L.slab((.22, .02, .8), (0, -.484, .95)))
    P.add("Body", [s.rbox((.3, .03, .88), (0, -.48, .95), r=.01, seg=1)])
    for x in (-.2, .2):
        P.add("Body", [s.ball(.035, (x, 0, 1.95), seg=8)])
    return P


def p_kitchen_counter(L):
    """Victorian dresser base and range: a claw-footed panelled base with bail pulls, a moulded marble top, a black
    iron range box with a glowing grate."""
    P = D.Parts()
    P.add("Body", L.carcass((2.4, .65, .88), (0, 0, 0)))
    for i in range(4):
        x = -.9 + i * .6
        P.add("Body", L.door((.56, .66), (x, -.33, .5)))
        P.add("Accent", L.handle((x, -.345, .76)))
    P.add("Accent", L.slab((2.44, .7, .05), (0, 0, .905)))
    P.add("Body", [s.rbox((.5, .42, .3), (.7, .02, 1.07), r=.015, seg=1), s.rcyl(.04, .1, (.86, .1, 1.2), seg=10, r=.006)])
    P.add("Eye", [s.rbox((.3, .02, .14), (.7, -.2, 1.05), r=.008, seg=1)])
    P.add("Accent", [s.rbox((.012, .03, .14), (.62 + k * .04, -.205, 1.05), r=.003, seg=1) for k in range(5)])
    return P


def p_water_cooler(L):
    """A stoneware water filter: a glazed crock with a brass tap on a claw-footed tripod stand, a glass globe on top."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.16, .03, (0, 0, .5), seg=14, r=.006)] + sum([L.leg((math.cos(a) * .1, math.sin(a) * .1, .5), .5, .05)
                                                                    for a in (.5, .5 + math.tau / 3, .5 + 2 * math.tau / 3)], []))
    P.add("Body", L.lathe([(0, .5), (.13, .5), (.16, .6), (.16, .85), (.13, .97), (.1, 1.0), (0, 1.0)], seg=16))
    P.add("Body", L.rod((0, -.14, .66), (0, -.19, .64), .015))
    P.add("Eye", L.lathe([(0, .99), (.06, .99), (.12, 1.08), (.15, 1.2), (.12, 1.34), (.05, 1.42), (0, 1.45)], seg=16))
    return P


def p_whiteboard(L):
    """A hunting map in a gilt frame on two turned posts, a stag's antlers mounted on top."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .02, .86), (0, 0, 1.33)))
    P.add("Accent", gilt_frame(1.7, .96, (0, 0, 1.33), t=.06))
    for x in (-.88, .88):
        P.add("Accent", [s.rbox((.06, .06, 1.8), (x, 0, .9), r=.015, seg=1)] + L.leg((x, 0, .2), .2, .07))
    P.add("Accent", [s.rbox((1.82, .05, .05), (0, 0, .9), r=.01, seg=1)])
    P.add("Body", [s.place(s.rbox((.16, .04, .12), (0, 0, 0), r=.03, seg=1), (0, .0, 1.85))] + antlers((0, 0, 1.85), span=.5, rise=.14, r=.02))
    return P


def p_toilet_stall(L):
    """Panelled water closet: mahogany walls with raised panels, a high cistern with a pull chain on the back wall."""
    P = D.Parts()
    for x in (-.5, .5):
        P.add("Body", [s.rbox((.04, 1.5, 1.88), (x, 0, 1.06), r=.006, seg=1)])
        for y in (-.4, .3):
            P.add("Body", [s.rbox((.06, .55, .7), (x, y, .6 + .0), r=.01, seg=1), s.rbox((.06, .55, .6), (x, y, 1.45), r=.01, seg=1)])
        for y in (-.7, 0.0):
            P.add("Body", [s.rbox((.05, .05, .14), (x, y, .07), r=.01, seg=1)])
    P.add("Body", [s.rbox((1.04, .04, 1.88), (0, .75, 1.06), r=.006, seg=1)])
    P.add("Accent", L.lathe([(0, 0), (.15, 0), (.2, .3), (.22, .4), (0, .42)], loc=(0, .4, 0)))
    P.add("Accent", [s.rbox((.5, .18, .22), (0, .64, 1.8), r=.02, seg=1), s.rcyl(.015, 1.2, (0, .7, 1.1), seg=8, r=0),
                     s.rcyl(.015, .6, (.18, .62, 1.45), seg=6, r=0), s.ball(.03, (.18, .62, 1.14), seg=8)])
    return P


def p_partition(L):
    """A three-leaf screen of buttoned tweed panels with arched tops, framed in mahogany, on claw feet."""
    P = D.Parts()
    for x in (-.53, 0, .53):
        P.add("Body", L.cushion((.44, .03, .9), (x, 0, .64)) + [s.place(s.rcyl(.22, .03, (0, 0, 0), axis="Y", seg=14, r=0), (x, 0, 1.06), scale=(1, 1, .7))])
        P.add("Accent", [s.rbox((.5, .05, .05), (x, 0, .19), r=.01, seg=1)])
    for x in (-.79, -.265, .265, .79):
        P.add("Accent", [s.rbox((.045, .05, 1.06), (x, 0, .7), r=.012, seg=1)] + [s.ball(.03, (x, 0, 1.24), seg=8)] + L.leg((x, 0, .18), .18, .05))
    return P


def p_printer(L):
    """A Victorian typewriter on a claw-legged stand: a tall basket of type bars, a carriage and a paper roller."""
    P = D.Parts()
    P.add("Body", [s.rbox((.6, .55, .05), (0, 0, .72), r=.01, seg=1)] + L.four_legs(.7, .3, .275, .7, .05, inset=.05))
    P.add("Body", [s.rbox((.5, .4, .14), (0, .02, .8), r=.03, seg=1)])
    P.add("Body", [s.place(s.loft([(0, .22, .08, 2.0, 0, .0), (.22, .16, .06, 2.0)], seg=16, axis="Z"), (0, .02, .86))])
    P.add("Accent", [s.rcyl(.06, .6, (0, .1, 1.0), axis="X", seg=14, r=.01), s.rbox((.42, .04, .12), (0, .14, 1.0), r=.01, seg=1)])
    for k in range(4):
        P.add("Accent", [s.rbox((.44 - k * .04, .05, .03), (0, -.19 + k * .04, .8 + k * .02), r=.006, seg=1)])
    P.add("Accent", L.slab((.36, .3, .03), (-.05, -.38, .74), rot=(math.radians(6), 0, 0)))
    P.add("Eye", [s.rcyl(.04, .02, (.2, -.18, .84), axis="Y", seg=10, r=.004)])
    return P


def p_fridge(L):
    """Oak icebox: a claw-footed cabinet with two panelled doors and big brass hinge latches."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .7, 1.8), (0, 0, 0), kick=.1))
    P.add("Body", L.door((.66, .5), (0, -.355, 1.48)) + L.door((.66, 1.02), (0, -.355, .7)))
    for z in (1.44, .95):
        P.add("Accent", [s.rbox((.08, .04, .14), (.26, -.375, z), r=.015, seg=1), s.rbox((.14, .03, .04), (.2, -.385, z + .03), r=.01, seg=1)])
    for z in (1.66, 1.3, 1.05, .35):
        P.add("Accent", [s.rbox((.1, .03, .04), (-.3, -.37, z), r=.008, seg=1)])
    return P


def p_coffee_machine(L):
    """A silver tea urn: a footed urn with ear handles, a spigot, a domed lid with a finial knob."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.1, 0), (.12, .02), (.05, .06), (.05, .1), (0, .1)], seg=16) + [s.rbox((.34, .45, .012), (0, -.025, .006), r=.004, seg=1)])
    P.add("Body", L.lathe([(0, .09), (.08, .09), (.14, .2), (.15, .3), (.12, .38), (0, .38)], seg=16))
    P.add("Accent", L.lathe([(.12, .37), (.125, .37), (.09, .44), (.03, .46), (.04, .48), (0, .5)], seg=16))
    for sx in (-1, 1):
        P.add("Accent", [torus(.03, .009, (sx * .16, 0, .3), seg=10, seg_r=5, axis="Y")])
    P.add("Accent", L.rod((0, -.13, .18), (0, -.22, .17), .015) + L.rod((0, -.22, .18), (0, -.22, .13), .012))
    P.add("Eye", [s.rcyl(.03, .02, (0, -.14, .3), axis="Y", seg=10, r=.004)])
    return P


def p_tv_stand(L):
    """A claw-legged console table under a screen in a heavy gilt frame like a family portrait."""
    P = D.Parts()
    P.add("Body", L.slab((1.4, .4, .05), (0, 0, .48)) + L.four_legs(.46, .7, .2, .46, .06, inset=.06) + [s.rbox((1.3, .3, .12), (0, 0, .4), r=.01, seg=1)])
    acc, glow = L.screen((1.2, .7), (0, .06, 1.05), "-Y")
    P.add("Accent", acc + L.slab((.3, .2, .04), (0, .08, .52)) + L.slab((.06, .05, .2), (0, .09, .6)))
    P.add("Eye", glow)
    return P


def p_reception_desk(L):
    """Hotel lobby counter: a mahogany front of raised panels between pilasters, a moulded top, a brass bell."""
    P = props.reception_desk(L)
    for x in (-1.15, -.6, 0, .6, 1.15):
        P.add("Body", [s.rbox((.08, .06, 1.08), (x, -.41, .55), r=.012, seg=1)])
    for x in (-.88, -.3, .3, .88):
        P.add("Body", [s.rbox((.44, .04, .72), (x, -.41, .55), r=.012, seg=1)])
    return P


def p_coat_rack(L):
    """Hall stand: a turned pole on three splayed claw feet, crowned by a stag's antlers as hooks, two coats hanging."""
    P = D.Parts()
    P.add("Accent", L.lathe([(.02, .24), (.035, .3), (.025, 1.0), (.035, 1.55), (.03, 1.62)], seg=12) + [s.rcyl(.06, .08, (0, 0, .28), seg=10, r=.01)])
    for k in range(3):
        a = k / 3 * math.tau + .3
        d = Vector((math.cos(a), math.sin(a), 0))
        P.add("Accent", [align(s.rcyl(.02, .2, seg=8, r=0), Vector((0, 0, .28)), d * .17 + Vector((0, 0, .24)))] + L.leg(tuple(d * .17 + Vector((0, 0, .25))), .25, .07))
    P.add("Accent", [s.rbox((.14, .04, .1), (0, 0, 1.6), r=.025, seg=1)] + antlers((0, 0, 1.6), span=.22, rise=.18, r=.018))
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.28)) + L.cushion((.3, .12, .55), (-.08, .1, 1.3), rot=(0, 0, 2.1)))
    P.add("Body", [s.tube([(.06, 0, 1.58), (.1, 0, 1.6)], .012, res=1)])
    return P


def p_lockers(L):
    """A gun cabinet: three glazed doors with rifles standing behind the glass, a pediment, claw feet."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .5, 1.8), (0, 0, 0), kick=.08) + [s.place(s.loft([(-.45, .1, .25, 2.0), (.45, .1, .25, 2.0)], seg=12, axis="X"), (0, 0, 1.8))])
    for x in (-.3, 0, .3):
        P.add("Body", gilt_frame(.27, 1.5, (x, -.26, .98), t=.04))
        P.add("Accent", [s.rbox((.2, .01, 1.42), (x, -.24, .98), r=.003, seg=1)])
        for dx in (-.05, .05):
            P.add("Accent", [s.rcyl(.012, 1.1, (x + dx, -.235, 1.0), seg=6, r=0), s.rbox((.04, .03, .28), (x + dx, -.235, .45), r=.012, seg=1)])
        P.add("Accent", L.handle((x + .1, -.285, 1.0)))
    return P


def p_pallet(L):
    """Steamer trunks with leather straps and brass corners stacked on a low skid."""
    P = D.Parts()
    P.add("Accent", L.slab((1.2, 1.0, .04), (0, 0, .13)))
    for y in (-.45, 0, .45):
        P.add("Accent", [s.rbox((1.2, .1, .11), (0, y, .055), r=.008, seg=1)])
    for x, y, z, w, d, h in ((-.3, 0, .15, .56, .95, .38), (.3, 0, .15, .56, .95, .38), (-.28, 0, .53, .54, .9, .38)):
        P.add("Body", [s.rbox((w, d, h), (x, y, z + h / 2), r=.03, seg=1)])
        for yy in (-.25, .25):
            P.add("Accent", [s.rbox((w + .014, .05, h + .014), (x, y + yy, z + h / 2), r=.01, seg=1)])
    return P


def p_wash_basin(L):
    """Marble washstand on claw legs with porcelain bowls, a splashback, an oval gilt mirror."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .5, .06), (0, 0, .85)) + L.four_legs(.83, .8, .25, .83, .07, inset=.07) + L.slab((1.6, .03, .34), (0, .24, 1.02)))
    P.add("Body", [s.rbox((1.5, .4, .1), (0, 0, .76), r=.01, seg=1)])
    for x in (-.4, .4):
        P.add("Accent", L.lathe([(0, 0), (.12, 0), (.17, .07), (.17, .09), (0, .09)], loc=(x, -.02, .84)))
        P.add("Accent", L.rod((x, .17, .88), (x, .17, 1.0), .02) + L.rod((x, .17, 1.0), (x, .08, .99), .015))
    mirror = s.rcyl(.4, .02, (0, 0, 0), axis="Y", seg=24, r=0)
    s.place(mirror, (0, .235, 1.53), scale=(1.6, 1, 1))
    P.add("Eye", [mirror])
    ring = torus(.42, .03, (0, 0, 0), seg=28, seg_r=6, axis="Y")
    s.place(ring, (0, .225, 1.53), scale=(1.6, 1, .95))
    P.add("Accent", [ring])
    return P


def p_vending_machine(L):
    """Fortune-teller automaton: a mahogany cabinet with an arched glass front, a bowler-hatted figure with a monocle
    sitting inside, a coin plate."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .72, 1.75), (0, .04, 0), kick=.1) + [s.place(s.loft([(-.45, .15, .36, 2.0), (.45, .15, .36, 2.0)], seg=14, axis="X"), (0, .04, 1.75))])
    P.add("Eye", L.slab((.62, .02, .8), (-.05, -.325, 1.1)) + [s.place(s.rcyl(.31, .02, (0, 0, 0), axis="Y", seg=16, r=0), (-.05, -.325, 1.5), scale=(1, 1, .6))])
    P.add("Body", [s.ball(.085, (-.05, -.33, 1.25), seg=12), s.rbox((.28, .08, .22), (-.05, -.33, 1.06), r=.04, seg=1)])
    crown, band = L.bowler((-.05, -.34, 1.32), r=.06, h=.07)
    P.add("Body", crown + band)
    P.add("Accent", L.slab((.6, .03, .15), (-.1, -.33, .3)) + L.slab((.12, .03, .4), (.36, -.33, .7)))
    return P


def p_ping_pong_table(L):
    """A billiard-style table: a deep moulded apron on six thick cabriole legs."""
    P = D.Parts()
    P.add("Body", L.slab((2.74, 1.52, .04), (0, 0, .74)) + [s.rbox((2.6, 1.4, .16), (0, 0, .64), r=.02, seg=1)])
    for x in (-1.15, 0, 1.15):
        for y in (-.6, .6):
            P.add("Accent", L.leg((x, y, .58), .58, .12))
    P.add("Eye", L.slab((.02, 1.72, .15), (0, 0, .82)))
    return P


def p_bean_bag(L):
    """A buttoned chesterfield pouf: a round drum of deep-buttoned leather with a rolled rim."""
    P = D.Parts()
    P.add("Body", [s.loft([(.04, .42, .42, 2.0), (.3, .45, .45, 2.0), (.58, .44, .44, 2.0), (.65, .4, .4, 2.0)], seg=20, round_caps=.06)])
    P.add("Body", [torus(.41, .05, (0, 0, .63), seg=20, seg_r=6)])
    for k in range(8):
        a = k / 8 * math.tau
        P.add("Body", [s.ball(.022, (math.cos(a) * .25, math.sin(a) * .25, .655), seg=6), s.ball(.022, (math.cos(a) * .448, math.sin(a) * .448, .3), seg=6)])
    P.add("Body", sum([L.leg((math.cos(a) * .3, math.sin(a) * .3, .06), .06, .06) for a in (.4, 1.97, 3.54, 5.11)], []))
    return P


def p_arcade_machine(L):
    """Mutoscope: a cast-iron peep-show machine — a fluted pedestal on claw feet, a tall case with a viewing hood
    (the glowing screen inside) and a crank handle."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.3, 0), (.3, .06), (.14, .12), (.12, .7), (.16, .78), (0, .78)], seg=14))
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        P.add("Body", L.leg((math.cos(a) * .12, math.sin(a) * .12, .1), .1, .08))
    P.add("Body", [s.rbox((.6, .7, .9), (0, .04, 1.23), r=.04, seg=1), s.place(s.loft([(-.3, .12, .35, 2.0), (.3, .12, .35, 2.0)], seg=14, axis="X"), (0, .04, 1.68))])
    P.add("Accent", [s.loft([(0, .16, .1, 3.0), (.22, .2, .12, 3.0), (.24, .2, .12, 3.0)], seg=16, axis="-Y", loc=(0, -.3, 1.38))])
    P.add("Eye", [s.rbox((.3, .02, .16), (0, -.53, 1.38), r=.01, seg=1)])
    P.add("Accent", L.rod((.28, .05, 1.2), (.33, .05, 1.2), .02) + L.rod((.33, .05, 1.2), (.33, -.1, 1.1), .015) + [s.ball(.03, (.33, -.12, 1.1), seg=8)])
    P.add("Accent", [s.rbox((.06, .2, .2), (-.32, .05, 1.2), r=.02, seg=1)])
    P.add("Accent", gilt_frame(.5, .3, (0, -.31, .98), t=.04))
    return P


def p_safe(L):
    """A Victorian safe on claw feet: a gilt-lined door panel, a brass wheel and a crest, a glowing keyhole."""
    P = D.Parts()
    P.add("Body", L.carcass((.8, .7, 1.0), (0, 0, 0), kick=.1))
    P.add("Body", L.door((.66, .76), (0, -.355, .55)))
    P.add("Accent", gilt_frame(.56, .66, (0, -.37, .55), t=.03))
    c = Vector((0, -.385, .5))
    P.add("Accent", [torus(.1, .014, c, seg=16, seg_r=5, axis="Y"), s.rcyl(.03, .04, c, axis="Y", seg=10, r=.006)])
    for k in range(4):
        a = k / 4 * math.tau
        P.add("Accent", [align(s.rcyl(.01, .1, seg=6, r=0), c, c + Vector((math.cos(a) * .1, 0, math.sin(a) * .1)))])
    P.add("Accent", [s.place(s.rcyl(.07, .02, (0, 0, 0), axis="Y", seg=12, r=.004), (0, -.37, .78), scale=(1, 1, 1.2))])
    P.add("Eye", [s.rbox((.025, .02, .05), (.2, -.375, .5), r=.008, seg=1)])
    return P


def p_floor_lamp(L):
    """Gas-lamp standard: a fluted iron pole on claw feet, a frosted glass globe round the flame, a crown cap."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.15, .06, (0, 0, .16), seg=12, r=.01)] + sum([L.leg((math.cos(a) * .13, math.sin(a) * .13, .16), .16, .09)
                                                                     for a in (math.pi / 4, 3 * math.pi / 4, 5 * math.pi / 4, 7 * math.pi / 4)], []))
    P.add("Body", L.lathe([(.03, .16), (.025, 1.2), (.04, 1.24), (.05, 1.26)], seg=10))
    P.add("Accent", L.lathe([(.05, 1.24), (.08, 1.26), (.06, 1.28), (0, 1.28)], seg=12))
    P.add("Eye", L.lathe([(0, 1.27), (.08, 1.28), (.14, 1.36), (.13, 1.46), (.06, 1.52), (0, 1.53)], seg=16))
    P.add("Accent", L.lathe([(0, 1.5), (.09, 1.5), (.11, 1.54), (.05, 1.58), (.02, 1.62), (.03, 1.64), (0, 1.65)], seg=12))
    return P


def p_ceiling_lamp(L):
    """A flush gas plate: a moulded brass rim round a frosted glass bowl."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.24, .66, .04), (0, 0, -.02), r=.01, seg=1)])
    P.add("Accent", [s.place(s.loft([(0, .58, .3, 2.0), (.03, .58, .3, 2.0)], seg=28), (0, 0, -.065))])
    P.add("Eye", [s.place(s.loft([(0, .5, .24, 2.0), (.035, .52, .26, 2.0)], seg=28), (0, 0, -.075))])
    return P


def p_exit_sign(L):
    """An enamel plaque in a brass frame."""
    P = props.exit_sign(L)
    P.add("Accent", [s.rbox((.4, .02, .16), (0, sy * .155, 0), r=.008, seg=1) for sy in (-1, 1)])
    return P


def p_hatch(L):
    """A cast-iron coal-hole cover with a raised star pattern and a brass ring pull."""
    P = D.Parts()
    P.add("Body", L.cyl(.76, .06, (0, 0, .03)))
    for k in range(8):
        a = k / 8 * math.tau
        P.add("Accent", [s.place(s.rbox((.6, .05, .025), r=.006, seg=1), (math.cos(a) * .32, math.sin(a) * .32, .065), rot=(0, 0, a))])
    P.add("Accent", [torus(.6, .03, (0, 0, .065), seg=28, seg_r=6), torus(.08, .015, (0, 0, .07), seg=12, seg_r=5)])
    P.add("Eye", L.lathe([(.76, 0), (.9, 0), (.9, .02), (.76, .02)], seg=32))
    return P


PROP_DESIGNS = {"terminal": p_terminal, "server_rack": p_server_rack, "kitchen_counter": p_kitchen_counter, "water_cooler": p_water_cooler, "whiteboard": p_whiteboard, "toilet_stall": p_toilet_stall, "partition": p_partition, "printer": p_printer, "fridge": p_fridge, "coffee_machine": p_coffee_machine, "tv_stand": p_tv_stand, "reception_desk": p_reception_desk, "coat_rack": p_coat_rack, "lockers": p_lockers, "pallet": p_pallet, "wash_basin": p_wash_basin, "vending_machine": p_vending_machine, "ping_pong_table": p_ping_pong_table, "bean_bag": p_bean_bag, "arcade_machine": p_arcade_machine, "safe": p_safe, "floor_lamp": p_floor_lamp, "ceiling_lamp": p_ceiling_lamp, "exit_sign": p_exit_sign, "hatch": p_hatch}


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "security_camera": security_camera, "sensor_lidar": lidar, "sensor_pir": pir, "attach_eye": attach_eye,
}
DESIGNS.update(PROP_DESIGNS)
