"""S07 Logbook — «Бортовой журнал»: 19th-century ship and diving-bell automata, rendered 1-bit dithered.

Only silhouette and large shading survive the 1-bit filter, so every robot is ONE bold nautical object: a hard-hat
diver, a walking anchor with a bathysphere head, a submarine on legs, a diving bell crab, a deck cart of ventilator
cowls, a davit crane, a sea mine, a steam-hammer barge, a lighthouse, a lifebuoy drone, a harpoon gun on a capstan,
a porthole in the ceiling. Language: riveted bands (Accent hoops), portholes with fat brass rings, heavy lead boots,
ropes, flared ventilator mouths; big round forms, no small boxes.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import shapes as s
import props
from lang import Lang, align
import designs as D


def torus(R, r, loc=(0, 0, 0), seg=24, seg_r=10, axis="Z", arc=1.0, a0=0.0):
    """A ring (lifebuoy, hoop): major radius R, tube radius r, about the axis through loc. arc < 1 makes an open arc
    (capped)."""
    bm = bmesh.new()
    rings = []
    n = seg if arc >= 1.0 else seg + 1
    for i in range(n):
        a = a0 + i / seg * math.tau * arc
        c, sn = math.cos(a), math.sin(a)
        ring = []
        for j in range(seg_r):
            b = j / seg_r * math.tau
            rr = R + r * math.cos(b)
            ring.append(bm.verts.new((rr * c, rr * sn, r * math.sin(b))))
        rings.append(ring)
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


class Style(Lang):
    name = "S07_Logbook"
    seg = 12
    soft = 0.1
    edge = 0.02
    hull_kind = "barrel"
    eye_kind = "porthole"
    limb_kind = "tube"
    limb_scale = 1.15

    def rseg(self):
        return 1

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        """Balls are the costliest form: few segments, more only on big ones."""
        return [s.ball(radius, loc, seg=16 if radius > .12 else 12 if radius > .06 else 8, scale=scale)]

    # ------------------------------------------------------------------ nautical vocabulary

    def porthole(self, r, loc, axis="-Y", bolts=6):
        """(accent, glow): a porthole — a fat brass ring with bolt heads and a glass disc."""
        loc = Vector(loc)
        a = {"-Y": Vector((0, -1, 0)), "Y": Vector((0, 1, 0)), "X": Vector((1, 0, 0)), "-X": Vector((-1, 0, 0)),
             "-Z": Vector((0, 0, -1)), "Z": Vector((0, 0, 1))}[axis]
        ring = [s.lathe([(r * .9, 0), (r * 1.45, 0), (r * 1.5, .025), (r * 1.4, .05), (r * 1.05, .05), (r * .92, .035)],
                        seg=self.seg, axis=axis, loc=loc - a * .02)]
        side = Vector((1, 0, 0)) if abs(a.x) < .5 else Vector((0, 1, 0))
        up = a.cross(side)
        for k in range(bolts):
            t = (k + .5) / bolts * math.tau
            p = loc + a * .02 + (side * math.cos(t) + up * math.sin(t)) * r * 1.22
            ring.append(s.ball(max(.008, r * .22), p, seg=8))
        glass = [s.lathe([(r * .95, 0), (r * .9, .012), (r * .5, .025), (0, .028)], seg=self.seg, axis=axis, loc=loc - a * .005)]
        return ring, glass

    def hoop(self, a, b, z, loc=(0, 0, 0), t=.03, axis="Z"):
        """A riveted band (hoop) around an elliptic section of half-axes a, b at z."""
        return [s.loft([(z - t, a, b, 2.0), (z + t, a, b, 2.0)], seg=self.seg, axis=axis, loc=loc)]

    def cowl(self, base, top_z, mouth_dir, r=.08, mouth=.18):
        """A ship ventilator: a vertical pipe from base up to top_z, an elbow, a flared bell mouth facing mouth_dir.
        Returns (pipe+mouth, mouth centre)."""
        b = Vector(base)
        d = Vector(mouth_dir).normalized()
        elbow = Vector((b.x, b.y, top_z))
        tip = elbow + d * mouth * 1.1 + Vector((0, 0, mouth * .55))
        out = [s.tube([tuple(b), (b.x, b.y, top_z - mouth * .5), tuple(elbow + d * mouth * .4 + Vector((0, 0, mouth * .3))), tuple(tip)],
                      r, res=3)]
        # the bell mouth: a lathe flaring along d
        bell = s.lathe([(0, -.02), (r * 1.05, -.02), (r * 1.25, mouth * .3), (mouth, mouth * .65), (mouth * 1.08, mouth * .72),
                        (mouth * .95, mouth * .74), (r * 1.2, mouth * .5), (0, mouth * .45)], seg=self.seg)
        q = Vector((0, 0, 1)).rotation_difference(d)
        bmesh.ops.rotate(bell, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bell.verts)
        bmesh.ops.translate(bell, vec=tip - d * .03, verts=bell.verts)
        out.append(bell)
        return out, tip + d * mouth * .45

    def rope(self, points, r=.018):
        return [s.tube(points, r, res=1, segments_per=3 if len(points) < 8 else 2)]

    def wheel(self, center, radius, width):
        """A heavy cast wheel: a rounded rim with a raised hub boss (symmetric about its centre)."""
        c = Vector(center)
        out = [s.lathe([(0, -width / 2), (radius * .85, -width / 2), (radius, -width * .3), (radius, width * .3), (radius * .85, width / 2),
                        (0, width / 2)], seg=self.seg + 2, axis="X", loc=c)]
        out.append(s.rcyl(radius * .35, width * 1.25, c, axis="X", seg=8, r=.006))
        return out

    def boot(self, x, y=-.06):
        """A lead diving boot: a thick sole and a rounded toe cap with a strap."""
        out = [s.rbox((.2, .36, .07), (x, y - .03, .035), r=.025, seg=2)]
        out.append(s.loft([(0, .09, .14, 3.0, 0, 0), (.1, .085, .12, 3.0, 0, .01), (.17, .07, .09, 2.6, 0, .03)],
                          seg=self.seg, loc=(x, y - .04, .05)))
        out.append(s.rbox((.21, .05, .05), (x, y - .1, .11), r=.015, seg=2))
        return out

    def propeller(self, center, span, blades=3):
        """A ship's screw lying flat: broad twisted blades round a hub (spins about Z)."""
        c = Vector(center)
        out = [s.rcyl(span * .1, span * .2, c, seg=12, r=.006)]
        for k in range(blades):
            b = s.loft([(0, span * .06, .008), (span * .2, span * .12, .01), (span * .38, span * .1, .008), (span * .47, span * .035, .006)],
                       seg=10, axis="X")
            s.place(b, rot=(math.radians(28), 0, k / blades * math.tau))
            out.append(s.place(b, c))
        return out

    def track(self, size, loc):
        """A crawler track lying along Y (w, d, h): a stadium belt, road wheels showing on the outer face.
        (Lang.track lofts along X with the ring's a/b swapped, so its belt stands upright: built here instead.)"""
        w, d, h = size
        x, y, z = loc
        r = h / 2
        out = [s.loft([(-w / 2, r, d / 2, 6.0), (w / 2, r, d / 2, 6.0)], seg=self.seg + 8, axis="X", loc=loc)]
        for k in range(4):
            yy = y - d / 2 + r + (d - 2 * r) * k / 3
            out.append(s.rcyl(r * .62, w * 1.12, (x, yy, z), axis="X", seg=10, r=0))
        return out

    # ------------------------------------------------------------------ furniture vocabulary: ship's furniture
    leg_kind = "square"
    edge_kind = "eased"
    cushion_puff = .35
    handle_kind = "ring"
    foliage_kind = "spiky"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """Boards: a thick plank with a fiddle rail round the top (nothing slides off at sea); panels: eased planks."""
        w, d, h = size
        x, y, z = loc
        out = [s.rbox(size, loc, r=min(min(size) * .3, .01), seg=1, rot=rot)]
        if h <= .08 and min(w, d) > .35 and not any(rot):
            t, rh = .022, .02
            for sy in (-1, 1):
                out.append(s.rbox((w, t, rh + .006), (x, y + sy * (d / 2 - t / 2), z + h / 2 + rh / 2 - .003), r=.006, seg=1))
            for sx in (-1, 1):
                out.append(s.rbox((t, d - 2 * t + .004, rh + .006), (x + sx * (w / 2 - t / 2), y, z + h / 2 + rh / 2 - .003), r=.006, seg=1))
        return out

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A stout square post with a brass shoe."""
        x, y, z = top
        foot = (x + splay[0], y + splay[1], max(0.0, z - length))
        out = [align(s.rbox((thick * 1.15, thick * 1.15, length - .03), r=.006, seg=1), (x, y, z), (foot[0], foot[1], foot[2] + .03))]
        out.append(s.rbox((thick * 1.4, thick * 1.4, .05), (foot[0], foot[1], foot[2] + .025), r=.006, seg=1))
        return out

    def carcass(self, size, loc, kick=0.06):
        """A sea-chest body: a plank case with two iron bands round it and a heavy plinth."""
        w, d, h = size
        x, y, z = loc
        out = [s.rbox((w, d, h - kick), (x, y, z + kick + (h - kick) / 2), r=.012, seg=1)]
        for f in (.18, .82):
            zz = z + kick + (h - kick) * f
            out.append(s.rbox((w + .012, d + .012, .04), (x, y, zz), r=.006, seg=1))
        if kick > 0:
            out.append(s.rbox((w - .01, d - .02, kick + .01), (x, y + .005, z + kick / 2 + .005), r=.006, seg=1))
        return out

    def door(self, size, loc, face="-Y"):
        """A plank door; a big one gets a porthole in its upper part."""
        w, h = size
        x, y, z = loc
        out = [s.rbox((w - .012, .022, h - .012), (x, y, z), r=.006, seg=1)]
        if w > .25 and h > .45:
            r = min(w * .28, .09)
            out.append(torus(r, .016, (x, y - .012, z + h * .22), seg=16, seg_r=6, axis="Y"))
            out.append(s.rcyl(r, .012, (x, y - .008, z + h * .22), axis="Y", seg=16, r=0))
        return out

    def handle(self, loc, length=0.12, vertical=True):
        """A ring pull on a round rose."""
        x, y, z = loc
        return [s.rcyl(.025, .016, (x, y + .004, z), axis="Y", seg=10, r=.004), torus(.03, .008, (x, y - .008, z - .03), seg=12, seg_r=5, axis="Y")]

    def screen(self, size, loc, face="-Y"):
        """A screen in a thick riveted brass frame."""
        acc, glow = Lang.screen(self, size, loc, face)
        w, h = size
        x, y, z = loc
        acc.append(s.rbox((w + .06, .03, h + .06), (x, y + .01, z), r=.025, seg=1))
        for sx in (-1, 1):
            for sz in (-1, 1):
                acc.append(s.ball(.012, (x + sx * (w / 2 + .015), y - .008, z + sz * (h / 2 + .015)), seg=6))
        return acc, glow

    def shade(self, r_bottom, r_top, height, loc):
        """A ship's lamp bell: a domed shell open at the bottom with a rolled rim (pole to pole, no flat caps)."""
        h = height
        prof = [(0, h + .004), (r_top * .7, h), (r_top, h * .8), (r_bottom * .85, h * .3), (r_bottom + .006, .01), (r_bottom, 0),
                (r_bottom - .008, .008), (r_bottom * .85 - .008, h * .3), (r_top - .008, h * .8 - .006), (0, h - .008)]
        return [s.lathe(prof, seg=16, loc=loc)]

    def pot(self, r, h, loc):
        """A half barrel with iron hoops."""
        x, y, z = loc
        out = [s.lathe([(0, 0), (r * .85, 0), (r, h * .5), (r * .92, h), (0, h)], seg=16, loc=loc)]
        for f in (.2, .8):
            rr = r * (.85 + .3 * f if f < .5 else 1 - .16 * (f - .5)) + .006
            out.append(s.rcyl(rr, .03, (x, y, z + h * f), seg=16, r=0))
        return out

    def signature(self, model, P):
        if model == "laser_emitter":  # rope lashings round the mast
            for z in (.35, 1.3):
                P.add("Body", [torus(.075, .018, (0, 0, z), seg=16, seg_r=6), torus(.075, .018, (0, 0, z + .035), seg=16, seg_r=6)])
        if model == "attach_mic":  # a speaking-tube bell under the ball, rope lashing on the stalk
            P.add("Accent", [s.lathe([(.02, .2), (.035, .23), (.07, .26), (.075, .27), (.05, .27)], seg=16)])
            P.add("Accent", [torus(.022, .01, (0, 0, .11), seg=8, seg_r=4)])
        if model == "attach_seismic":  # a lead sounding weight
            P.add("Accent", [s.lathe([(0, .14), (.05, .14), (.06, .2), (.045, .26), (0, .27)], seg=16)])
        if model == "sensor_plate":  # a riveted deck hatch
            for k in range(6):
                t = (k + .5) / 6
                for x, y in ((-.47 + .94 * t, -.47), (-.47 + .94 * t, .47), (-.47, -.47 + .94 * t), (.47, -.47 + .94 * t)):
                    P.add("Accent", [s.rcyl(.018, .012, (x, y, .014), seg=6, r=0)])
        return P


# =========================================================================== walkers

def guard(L):
    """Hard-hat diver: a huge round helmet with a porthole face on a broad riveted corselet, a puffy canvas suit,
    chest and back weights, an air hose, big mitts and lead boots."""
    P = D.Parts()
    P.add("Body", L.loft([(.84, .22, .17, 2.4), (1.0, .26, .2, 2.4), (1.2, .27, .21, 2.4), (1.42, .31, .22, 2.4),
                          (1.56, .3, .21, 2.2)], round_caps=.03))                                  # canvas suit
    P.add("Accent", L.hoop(.265, .205, 1.0))                                                       # belt
    P.add("Accent", L.loft([(1.5, .36, .27, 2.2), (1.56, .44, .3, 2.2), (1.62, .42, .29, 2.2), (1.68, .3, .23, 2.0),
                            (1.71, .2, .18, 2.0)]))                                               # corselet
    for k in range(10):
        t = (k + .5) / 10 * math.tau
        P.add("Accent", [s.ball(.02, (math.cos(t) * .43, math.sin(t) * .295, 1.6), seg=8)])
    P.add("Accent", L.box((.3, .06, .2), (0, -.235, 1.3), r=.03))                                 # chest weight
    P.add("Accent", L.box((.3, .06, .22), (0, .235, 1.3), r=.03))
    P.add("Body", L.ball(.265, (0, .02, 1.88)))                                                   # helmet
    P.add("Accent", L.hoop(.24, .24, 1.72))
    acc, glow = L.porthole(.1, (0, -.235, 1.86), bolts=6)
    P.add("Accent", acc).add("Eye", glow)
    for sx in (-1, 1):
        acc, glass = L.porthole(.065, (sx * .25, .02, 1.89), axis="X" if sx > 0 else "-X", bolts=4)
        P.add("Accent", acc).add("Body", glass)
    P.add("Accent", L.cyl(.06, .1, (0, .02, 2.15)))                                                # top valve
    P.add("Accent", L.rope([(.08, .2, 1.95), (.2, .34, 1.8), (.24, .3, 1.4), (.2, .24, 1.2)], r=.03))  # air hose
    for sx in (-1, 1):
        sh = (sx * .38, 0, 1.5)
        P.add("Body", L.loft([(0, .1, .1, 2.0), (.18, .095, .095, 2.0), (.36, .085, .085, 2.0)], axis="-Z",
                             loc=(sx * .4, 0, 1.52), round_caps=.03))                             # sleeve
        P.add("Accent", L.hoop(.095, .095, 1.13, loc=(sx * .4, 0, 0)))
        P.add("Body", L.loft([(.86, .08, .085, 2.0), (1.0, .09, .095, 2.0), (1.14, .085, .085, 2.0)], loc=(sx * .41, -.04, 0),
                             round_caps=.02))                                                       # forearm
        P.add("Accent", L.ball(.1, (sx * .42, -.06, .8), scale=(.9, 1.1, 1.1)))                    # mitt
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.1, (x, 0, .9)))
        P.add(leg, L.loft([(D.GUARD_KNEE - .02, .1, .1, 2.0), (.68, .125, .125, 2.0), (.92, .12, .12, 2.0)], loc=(x, 0, 0)))
        P.add(leg, L.hoop(.112, .112, D.GUARD_KNEE + .01, loc=(x, 0, 0), t=.035))
        P.add(leg, L.loft([(.13, .09, .095, 2.0), (.3, .105, .11, 2.0), (D.GUARD_KNEE + .02, .1, .1, 2.0)], loc=(x, 0, 0)))
        P.add(leg, L.boot(x))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """The anchor: a giant ship's anchor walks upright — the curved flukes skate on the floor, the shank is the spine,
    the stock is the shoulders, a bathysphere is the head; long arms end in grappling hooks, a rope coils up the shank."""
    P = D.Parts()
    # crown and flukes: an arc of a thick bar touching the floor in the middle, palms at the ends
    arc = [(math.sin(t) * .55, 0, .08 + (1 - math.cos(t)) * .55) for t in [math.radians(a) for a in range(-80, 81, 10)]]
    P.add("Body", [s.tube(arc, .07, res=2, segments_per=2)])
    for sx in (-1, 1):
        t = math.radians(80)
        tip = Vector((sx * math.sin(t) * .55, 0, .08 + (1 - math.cos(t)) * .55))
        P.add("Accent", [s.loft([(0, .0, .0), (.1, .13, .04, 2.2), (.26, .1, .03, 2.0), (.3, 0, 0)], seg=16, axis="Z",
                                loc=tip - Vector((0, 0, .12)))])                                   # fluke palm
    P.add("Body", L.ball(.1, (0, 0, .1)))
    P.add("Eye", [torus(.16, .02, (0, 0, .06), seg=24, seg_r=6)])
    P.add("Body", L.loft([(.1, .09, .08, 2.6), (1.2, .12, .1, 2.6), (1.7, .14, .11, 2.6)]))         # shank
    P.add("Accent", L.rope([(math.cos(t) * .14, math.sin(t) * .12, .3 + t * .085) for t in [k * .7 for k in range(17)]], r=.028))
    P.add("Body", L.loft([(1.55, .22, .16, 2.4), (1.7, .3, .2, 2.4), (1.8, .26, .19, 2.4)]))          # chest
    P.add("Accent", L.loft([(0, .06, .07, 2.0), (.5, .085, .09, 2.0), (1.0, .06, .07, 2.0)], axis="X",
                           loc=(-.5, 0, 1.76)))                                                    # the stock
    for sx in (-1, 1):
        P.add("Accent", L.ball(.1, (sx * .53, 0, 1.76)))
        P.add("Body", L.rod((sx * .53, 0, 1.76), (sx * .56, -.12, 1.25), .055))
        P.add("Accent", L.hoop(.07, .07, 1.25, loc=(sx * .56, -.12, 0)))
        P.add("Body", L.rod((sx * .56, -.12, 1.25), (sx * .42, -.45, .95), .05))
        hook = Vector((sx * .42, -.45, .92))
        P.add("Accent", L.ball(.07, hook))
        for k in range(3):
            a = k / 3 * math.tau + .3
            d = Vector((math.cos(a) * .7, -.3, math.sin(a) * .7))
            pts = [tuple(hook), tuple(hook + d * .14 + Vector((0, -.04, -.04))), tuple(hook + d * .2 + Vector((0, -.12, -.1)))]
            P.add("Accent", [s.tube(pts, .024, res=1, segments_per=3)])
    # bathysphere head with the anchor ring as a halo behind
    P.add("Body", L.ball(.2, (0, -.05, 1.98)))
    P.add("Accent", L.hoop(.205, .205, 1.98, loc=(0, -.05, 0), t=.025))
    acc, glow = L.porthole(.08, (0, -.235, 1.94), bolts=6)
    P.add("Accent", acc).add("Eye", glow)
    P.add("Accent", [torus(.2, .04, (0, .2, 2.22), seg=24, seg_r=8, axis="Y")])
    P.add("Accent", L.cyl(.05, .12, (0, .14, 2.02), axis="Y"))
    return P


def hound(L):
    """Submarine hound: a riveted cigar hull, a conning tower with a periscope on its back, cross tail fins and a
    screw at the stern, a porthole nose at the bow, four sturdy legs with lead paws."""
    P = D.Parts()
    P.add("Body", L.loft([(.6, .0, .0), (.5, .1, .1, 2.0), (.3, .17, .18, 2.0), (-.3, .2, .21, 2.0), (-.75, .16, .17, 2.0),
                          (-.95, .1, .11, 2.0), (-1.02, .0, .0)], axis="Y", loc=(0, 0, .82)))
    for y in (-.6, -.15, .3):
        P.add("Accent", L.hoop(.205 if y < .2 else .19, .215 if y < .2 else .2, -y, loc=(0, 0, .82), axis="-Y", t=.022))
    P.add("Accent", L.loft([(.98, .07, .2, 3.0, 0, -.1), (1.2, .06, .17, 3.0, 0, -.12), (1.24, .05, .15, 3.0, 0, -.12)],
                           round_caps=.02))                                                          # conning tower
    P.add("Accent", L.rod((0, -.2, 1.2), (0, -.2, 1.42), .025) + L.rod((0, -.2, 1.42), (0, -.32, 1.42), .025))  # periscope
    P.add("Eye", L.box((.05, .02, .04), (0, -.34, 1.42), r=.01))
    acc, glow = L.porthole(.055, (0, -1.0, .83), bolts=6)
    P.add("Body", acc).add("Eye", glow)
    for sx in (-1, 1):
        acc, glass = L.porthole(.045, (sx * .2, -.35, .88), axis="X" if sx > 0 else "-X", bolts=4)
        P.add("Body", acc + glass)
    for rot in (0, math.pi / 2):                                                                   # cross tail fins
        P.add("Accent", L.box((.44, .14, .03), (0, .52, .82), r=.012, rot=(0, rot, 0)))
    P.add("Body", L.cyl(.04, .1, (0, .63, .82), axis="Y"))
    for k in range(3):
        a = k / 3 * math.tau
        b = s.loft([(0, .035, .008), (.1, .05, .01), (.14, .02, .006)], seg=8, axis="X")
        s.place(b, rot=(math.radians(25), 0, 0))
        s.place(b, rot=(0, a, 0))
        P.add("Accent", [s.place(b, (0, .66, .82))])
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        P.add("Body", L.ball(.09, (x, y, .66)))
        leg = f"AccentLeg{i}"
        P.add(leg, L.ball(.07, (x, y, D.HOUND_HIP)))
        P.add(leg, L.loft([(D.HOUND_KNEE, .06, .06, 2.0), (.5, .075, .075, 2.0), (D.HOUND_HIP + .03, .065, .065, 2.0)], loc=(x, y, 0)))
        P.add(leg, L.hoop(.068, .068, D.HOUND_KNEE + .01, loc=(x, y, 0), t=.025))
        P.add(leg, L.loft([(.05, .05, .05, 2.0), (D.HOUND_KNEE + .02, .055, .055, 2.0)], loc=(x, y, 0)))
        P.add(leg, [s.rbox((.12, .17, .07), (x, y - .03, .035), r=.025, seg=2)])                # lead paw
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """Diving-bell crab: a squat riveted bell with a ring of portholes (the front two glow), a lifting shackle on
    top, eight thick legs with weighted feet."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, .0), (.26, .0), (.27, .03), (.25, .14), (.2, .26), (.12, .33), (0, .35)], loc=(0, .03, 0)))
    P.add("Body", L.hoop(.27, .27, .04, loc=(0, .03, 0), t=.025))
    P.add("Body", [torus(.06, .022, (0, .03, .39), seg=16, seg_r=8, axis="Y")])
    for k in range(6):
        a = -math.pi / 2 + (k - 0.5) / 6 * math.tau
        p = (math.cos(a) * .25, .03 + math.sin(a) * .25, .13)
        if k in (0, 1):
            acc, glow = L.porthole(.04, (p[0] * .98, p[1] - .02, .14), bolts=4)
            P.add("Body", acc).add("Eye", glow)
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .03) + L.ball(.045, knee))
        P.add(f"AccentLeg{i}Lower", L.rod(knee + (foot - knee) * .02, foot + Vector((0, 0, .04)), .024)
              + [s.lathe([(0, 0), (.045, 0), (.05, .03), (.03, .06), (0, .065)], seg=12, loc=foot - Vector((0, 0, .02)))])
    return P


# =========================================================================== wheeled

def listener(L):
    """Ventilator cart: a riveted deck hull on four heavy wheels and two tall ship's ventilator cowls whose bell
    mouths turn to the sides, glowing inside."""
    P = D.Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    P.add("Body", L.loft([(0, .0, .0), (.04, .2, .3, 2.6, 0, 0), (.25, .3, .2, 2.6), (.75, .3, .2, 2.6), (.98, .26, .17, 2.6)],
                         axis="-Y", loc=(0, .5, .44)))                                             # hull, bow forward
    P.add("Accent", L.box((.66, .9, .06), (0, 0, .58), r=.025))                                   # deck rim
    for x, y in ((-.43, -.3), (.43, -.3), (-.43, .3), (.43, .3)):
        P.add("Accent", L.rod((x * .55, y, .22), (x * 1.12, y, .22), .04))
    for y in (-.3, .3):
        P.add("Body", L.box((.8, .12, .12), (0, y, .26), r=.03))                                    # axle beams
    for sx in (-1, 1):
        pipe, mouth = L.cowl((sx * .16, .1, .58), 1.0, (sx, -.15, 0), r=.07, mouth=.2)
        P.add("Body", pipe)
        P.add("Eye", [s.ball(.07, mouth - Vector((sx * .08, 0, 0)), seg=12)])
    acc, glow = L.porthole(.07, (0, -.5, .44), bolts=6)
    P.add("Accent", acc).add("Eye", glow)
    return P


def worker(L):
    """Davit crane: a squat deck cart with a little funnel, a curved davit arm from the stern swinging over the bow,
    the crate hanging from it on ropes."""
    P = D.Parts()
    D.wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    P.add("Body", L.loft([(0, .0, .0), (.05, .22, .12, 2.6), (.15, .3, .17, 2.6), (.62, .3, .18, 2.6), (.78, .26, .15, 2.6)],
                         axis="-Y", loc=(0, .39, .36)))
    P.add("Accent", L.box((.64, .76, .05), (0, 0, .53), r=.02))
    for x, y in ((.31, .25), (-.31, .25), (.31, -.25), (-.31, -.25)):
        P.add("Accent", L.rod((x * .6, y, .12), (x * 1.12, y, .12), .03))
    for y in (-.25, .25):
        P.add("Body", L.box((.56, .1, .1), (0, y, .16), r=.025))                                   # axle beams
    P.add("Body", L.lathe([(0, .5), (.12, .5), (.11, .86), (.14, .9), (.14, .96), (0, .96)], loc=(-.15, .24, 0)))  # funnel
    P.add("Accent", L.hoop(.115, .115, .8, loc=(-.15, .24, 0), t=.03))
    # the davit: a thick curved arm from the stern post over the bow
    arm = [(.14, .26, .5), (.14, .26, 1.3), (.14, .16, 1.6), (.12, -.05, 1.68), (.06, -.15, 1.64)]
    P.add("Body", [s.tube(arm, .075, res=3)])
    P.add("Body", L.lathe([(0, .5), (.13, .5), (.1, .62), (0, .63)], loc=(.14, .26, 0)))
    P.add("Accent", L.ball(.08, (.03, -.15, 1.6)))                                                 # pulley block
    for sx in (-1, 1):
        P.add("Accent", L.rope([(.03, -.15, 1.54), (sx * .17, -.15, 1.31)], r=.02))
    P.add("Accent", L.box((.46, .42, .36), (0, -.15, 1.15), r=.03))                                 # crate
    for z in (1.02, 1.28):
        P.add("Accent", L.box((.48, .44, .04), (0, -.15, z), r=.01))
    acc, glow = L.porthole(.045, (0, -.38, .44), bolts=6)
    P.add("Accent", acc).add("Eye", glow)
    return P


def sweeper(L):
    """Sea mine: a riveted sphere with contact horns all round its top, an equator band, a porthole eye, rolling on
    three small wheels; side brushes like seaweed mops."""
    P = D.Parts()
    D.wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18), (0, -.3)):
        P.add("Accent", L.box((.08, .07, .22), (x, y, .23), r=.015))
        P.add("Accent", L.rod((x - .045, y, .08), (x + .045, y, .08), .02))
    P.add("Accent", [torus(.3, .04, (0, 0, .33), seg=28, seg_r=8)])                                 # base ring
    for x, y in ((-.28, .18), (.28, .18), (0, -.3)):
        P.add("Accent", L.rod((x * .9, y * .9, .3), (x * .9, y * .9, .36), .04))
    P.add("Body", L.ball(.32, (0, 0, .58)))
    P.add("Accent", L.hoop(.325, .325, .58, t=.03))
    for k in range(8):
        a = k / 8 * math.tau
        for el, n in ((math.radians(30), 1),):
            d = Vector((math.cos(a) * math.cos(el), math.sin(a) * math.cos(el), math.sin(el)))
            if d.y < -.6:
                continue
            base = Vector((0, 0, .58)) + d * .3
            P.add("Accent", [align(s.rcyl(.03, .12, seg=10, r=.006, radius2=.022), base, base + d * .12)])
            P.add("Accent", [s.ball(.035, base + d * .13, seg=10)])
    P.add("Accent", [align(s.rcyl(.04, .12, seg=10, r=.006, radius2=.025), Vector((0, 0, .86)), Vector((0, 0, .98)))])
    P.add("Accent", [s.ball(.045, (0, 0, .99), seg=10)])
    P.add("Accent", L.cyl(.07, .1, (0, 0, .3)))
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    acc, glow = L.porthole(.075, (0, -.31, .52), bolts=6)
    P.add("Accent", acc).add("Eye", glow)
    for sx in (-1, 1):
        c = (sx * .3, -.22, .03)
        P.add("Accent", L.ball(.1, c, scale=(1, 1, .35)))
        P.add("Accent", L.rod((c[0], c[1], .03), (sx * .27, -.16, .36), .018))
    return P


def thumper(L):
    """Steam-hammer barge: a boat hull with a raked bow on crawler tracks, a tall smoking funnel with bands, portholes
    down the sides and two steam hammers on a gantry pounding the pads into the floor."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.24, 1.36, .5), (sx * .56, 0, .26)))
    P.add("Body", L.box((1.0, 1.1, .14), (0, 0, .5), r=.03))                                          # frame over the tracks
    P.add("Body", L.loft([(.5, .3, .5, 2.2, 0, .05), (.62, .42, .56, 2.4, 0, .04), (1.0, .48, .6, 2.8, 0, .02)]))  # hull
    P.add("Body", L.loft([(0, .46, .24, 2.4, 0, .02), (.12, .36, .26, 2.2, 0, .04), (.28, .17, .28, 2.0, 0, .08),
                          (.4, .0, .0, 2.0, 0, .2)], axis="-Y", loc=(0, -.5, .78)))                  # raked bow
    P.add("Accent", L.box((.84, 1.1, .05), (0, .02, .98), r=.02))                                    # deck
    P.add("Body", L.box((.5, .4, .3), (0, .25, 1.15), r=.04))                                        # deck house
    P.add("Body", L.lathe([(0, 1.25), (.15, 1.25), (.14, 1.62), (.17, 1.66), (.17, 1.72), (0, 1.72)], loc=(0, .3, 0)))  # funnel
    for z in (1.45, 1.6):
        P.add("Accent", L.hoop(.155, .155, z, loc=(0, .3, 0), t=.03))
    for sx in (-1, 1):
        for y in (-.2, .25):
            acc, glow = L.porthole(.055, (sx * .44, y, .78), axis="X" if sx > 0 else "-X", bolts=0)
            P.add("Accent", acc + glow)
    P.add("Body", L.rod((0, -.5, .6), (0, -.72, .46), .045))
    acc, glow = L.porthole(.07, (0, -.76, .44), bolts=6)
    P.add("Accent", acc).add("Eye", glow)
    # gantry with two steam hammers
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", L.rod((x, -.42, .95), (x, -.85, 1.0), .05))
        P.add("Body", L.lathe([(0, .62), (.07, .62), (.08, .66), (.08, .98), (.06, 1.02), (0, 1.02)], loc=(x, -.85, 0)))  # cylinder
        P.add("Accent", L.hoop(.082, .082, .8, loc=(x, -.85, 0), t=.02))
        P.add("Accent", L.rod((x, -.85, .64), (x, -.85, .1), .04))
        P.add("Accent", L.lathe([(0, .04), (.15, .04), (.13, .1), (.05, .12), (0, .12)], loc=(x, -.85, 0)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
    P.add("Body", L.rod((-.32, -.85, 1.0), (.32, -.85, 1.0), .05))
    return P


# =========================================================================== tall

def sorter(L):
    """Lighthouse: a striped tapered tower on a rock base, a railed gallery, the lantern room with the big colour lens
    looking out front, a domed cap with a vent ball."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.29, .04), (.24, .08), (0, .08)]))
    P.add("Body", L.lathe([(0, .07), (.19, .07), (.17, .5), (.14, 1.0), (0, 1.0)]))
    for z0, z1 in ((.22, .38), (.6, .76)):
        k0, k1 = .19 - (z0 - .07) / .93 * .05, .19 - (z1 - .07) / .93 * .05
        P.add("Accent", L.lathe([(0, z0), (k0 + .01, z0), (k1 + .01, z1), (0, z1)]))
    P.add("Accent", L.lathe([(0, .98), (.24, .98), (.25, 1.0), (.25, 1.04), (0, 1.04)]))                # gallery
    for k in range(10):
        a = k / 10 * math.tau
        P.add("Accent", L.rod((math.cos(a) * .235, math.sin(a) * .235, 1.03), (math.cos(a) * .235, math.sin(a) * .235, 1.12), .012))
    P.add("Accent", [torus(.235, .014, (0, 0, 1.12), seg=24, seg_r=6)])
    P.add("Body", L.lathe([(0, 1.03), (.19, 1.03), (.19, 1.42), (0, 1.42)]))                          # lantern room
    for k in range(8):
        a = (k + .5) / 8 * math.tau
        P.add("Accent", L.box((.025, .025, .38), (math.cos(a) * .19, math.sin(a) * .19, 1.225), r=.006))
    P.add("Body", L.lathe([(0, 1.4), (.22, 1.4), (.21, 1.44), (.12, 1.54), (0, 1.58)]))                # dome
    P.add("Body", L.ball(.04, (0, 0, 1.6)))
    P.add("Accent", L.lathe([(0, 0), (.2, 0), (.21, .03), (.18, .05), (0, .05)], axis="-Y", loc=(0, -.16, 1.24)))
    P.add("Eye", L.lathe([(.18, 0), (.17, .02), (.1, .04), (0, .045)], axis="-Y", loc=(0, -.205, 1.24)))
    return P


def drone(L):
    """Lifebuoy drone: a striped life ring flies flat, four spars to ship's screws, a porthole camera slung under the
    front of the ring and a rope looped round the outside."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", [torus(.25, .09, (0, 0, 0), seg=32, seg_r=12)])
    for k in range(4):                                                                              # red-white bands
        a = k / 4 * math.tau
        P.add("Accent", [torus(.25, .1, (0, 0, 0), seg=4, seg_r=12, arc=.1)])
        s.place(P["Accent"][-1], rot=(0, 0, a + math.pi / 4 - .38))
    for i, (x, y) in enumerate(rot):
        d = Vector((x, y, 0)).normalized()
        P.add("Body", [align(s.rbox((.06, .06, .3), r=.012, seg=1), d * .3, Vector((x, y, .02)) - d * .02)])
        P.add("Body", L.cyl(.04, .06, (x, y, .03)))
        P.add(f"AccentRotor{i}", L.propeller((x, y, .065), .35, 4))
    P.add("Body", L.rod((0, -.25, -.05), (0, -.26, -.08), .04))
    acc, glow = L.porthole(.045, (0, -.3, -.1), bolts=4)
    P.add("Accent", L.ball(.07, (0, -.26, -.1))).add("Accent", acc).add("Eye", glow)
    return P


def turret(L):
    """Deck cannon on a capstan: a waisted capstan drum on a riveted deck plate with a glowing band, and a fat
    muzzle-swelled cannon behind a curved shield, a barbed harpoon sticking out of its mouth."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.34, 0), (.34, .05), (.3, .07), (0, .07)]))
    P.add("Body", L.lathe([(0, .06), (.25, .06), (.22, .15), (.16, .4), (.15, .7), (.18, .95), (.23, 1.05), (.23, 1.12), (0, 1.12)]))
    for z in (.12, 1.07):
        P.add("Accent", L.hoop(.245, .245, z, t=.03))
    P.add("Eye", L.loft([(.5, .163, .163, 2.0), (.56, .163, .163, 2.0)]))
    for k in range(6):
        a = k / 6 * math.tau
        P.add("Body", L.box((.05, .05, .5), (math.cos(a) * .155, math.sin(a) * .155, .58), r=.012))     # whelps
    P.add("AccentHead", L.lathe([(0, 1.11), (.14, 1.11), (.15, 1.15), (.12, 1.2), (0, 1.2)]))
    P.add("AccentHead", L.box((.2, .26, .14), (0, .02, 1.25), r=.03))                                 # carriage
    P.add("AccentHead", L.lathe([(0, 0), (.1, 0), (.11, .04), (.09, .08), (.075, .4), (.09, .48), (.1, .52), (.07, .54), (0, .54)],
                                axis="-Y", loc=(0, .2, 1.3)))                                         # cannon
    P.add("AccentHead", L.ball(.06, (0, .22, 1.3)))
    P.add("AccentHead", [s.loft([(0, .025, .025, 2.0), (.08, .065, .022, 2.0), (.2, .0, .0)], seg=8, axis="-Y", loc=(0, -.3, 1.3))])  # harpoon
    for sx in (-1, 1):
        P.add("AccentHead", [align(s.rbox((.03, .03, .1), r=.008, seg=1), Vector((sx * .015, -.45, 1.3)), Vector((sx * .075, -.39, 1.3)))])
        P.add("AccentHead", L.cyl(.05, .04, (sx * .105, .02, 1.3), axis="X"))                         # trunnions
    P.add("AccentHead", [s.rbox((.36, .045, .26), (0, -.1, 1.31), r=.02, seg=2, taper=(.7, 1.0), rot=(math.radians(-12), 0, 0))])  # shield
    return P


def vacuum(L):
    """Ceiling helm-porthole: a heavy brass porthole flange with bolt heads (the room side is -z), a dark deadlight glass
    and a glowing lens, ringed by a ship's wheel whose turned handles stick out past the rim."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, .1), (.32, .1), (.34, .08), (.34, .02), (.32, 0), (0, 0)], seg=L.seg)])
    P.add("Accent", [s.lathe([(.2, .01), (.31, .01), (.33, -.01), (.31, -.035), (.22, -.035), (.2, -.02)], seg=L.seg)])
    for k in range(8):
        a = (k + .5) / 8 * math.tau
        P.add("Accent", [s.ball(.025, (math.cos(a) * .27, math.sin(a) * .27, -.035), seg=8)])
    P.add("Body", [s.lathe([(0, -.025), (.21, -.025), (.2, 0), (0, 0)], seg=L.seg)])
    P.add("Eye", [s.lathe([(0, -.05), (.07, -.045), (.08, -.03), (0, -.02)], seg=L.seg)])
    P.add("Accent", [torus(.36, .025, (0, 0, .05), seg=40, seg_r=8)])                                  # wheel rim
    for k in range(8):
        a = k / 8 * math.tau
        d = Vector((math.cos(a), math.sin(a), 0))
        P.add("Accent", [s.lathe([(.022, -.07), (.03, -.03), (.022, 0), (.03, .04), (.018, .08), (0, .09)], seg=10, axis="X")])
        s.place(P["Accent"][-1], rot=(0, 0, a))
        s.place(P["Accent"][-1], d * .37 + Vector((0, 0, .05)))
    P.add("Eye", L.box((.1, .03, .014), (0, -.22, -.03), r=.005))
    return P


def lidar(L):
    """Binnacle: a turned pedestal with two Kelvin spheres, the compass hood on top turns."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.18, 0), (.17, .04), (.08, .1), (.07, .36), (.11, .4), (0, .4)]))
    for sx in (-1, 1):
        P.add("Body", L.rod((0, 0, .34), (sx * .15, 0, .34), .02) + L.ball(.05, (sx * .17, 0, .34)))
    P.add("Eye", L.loft([(.38, .085, .085, 2.0), (.4, .085, .085, 2.0)]))
    P.add("AccentHead", L.lathe([(0, .395), (.09, .395), (.1, .45), (.07, .5), (0, .515)]))
    acc, glow = L.porthole(.025, (0, -.095, .455), bolts=0)
    P.add("AccentHead", acc + glow)
    return P


def security_camera(L):
    """A diving-helmet camera: a riveted brass ball on a bracket, a porthole lens."""
    P = D.Parts()
    P.add("Body", L.box((.16, .06, .22), (0, .27, 0), r=.02))
    P.add("Accent", L.rod((0, .26, 0), (0, .08, -.02), .03))
    P.add("Accent", L.ball(.13, (0, -.06, -.02)))
    P.add("Accent", L.hoop(.135, .135, -.02, loc=(0, -.06, 0), t=.02))
    acc, glow = L.porthole(.05, (0, -.2, -.02), bolts=4)
    P.add("Accent", acc).add("Eye", glow)
    return P


def pir(L):
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.08, 0), (.08, .05), (0, .07)], axis="-Y", loc=(0, .08, 0)))
    acc, glow = L.porthole(.04, (0, .02, 0), bolts=4)
    P.add("Body", acc).add("Eye", glow)
    return P


def attach_eye(L):
    P = D.Parts()
    P.add("Accent", L.ball(.1, (0, .0, 0)))
    acc, glow = L.porthole(.05, (0, -.105, 0), bolts=0)
    P.add("Accent", acc).add("Eye", glow)
    return P


def attach_nose(L):
    """A snorkel: a pipe with a bell mouth turned down and forward."""
    P = D.Parts()
    P.add("Accent", L.loft([(0, .08, .08, 2.0), (.15, .06, .06, 2.0), (.2, .07, .07, 2.0), (.24, .09, .09, 2.0), (.26, .085, .085, 2.0)],
                           axis="-Y"))
    P.add("Eye", L.box((.025, .02, .02), (-.02, -.265, .005), r=.005) + L.box((.025, .02, .02), (.02, -.265, .005), r=.005)
          + L.box((.06, .02, .012), (0, -.265, -.025), r=.004))
    return P


# =========================================================================== furniture and level pieces
# A ship's interior: sea chests, barrels and casks, plank furniture with fiddle rails, capstans, portholes, ropes,
# riveted iron bands, lanterns. Bold forms for the 1-bit filter.

def barrel(r, h, loc, axis="Z", hoops=(.15, .85)):
    """A cask: bulging staves and iron hoops, along axis (Z standing, X / Y lying)."""
    x, y, z = loc
    prof = [(0, -h / 2), (r * .82, -h / 2), (r * .95, -h * .25), (r, 0), (r * .95, h * .25), (r * .82, h / 2), (0, h / 2)]
    out = [s.lathe(prof, seg=14, axis=axis, loc=loc)]
    for f in hoops:
        t = f - .5
        rr = r * (1 - .72 * t * t) + .006
        c = Vector(loc) + {"Z": Vector((0, 0, 1)), "X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0))}[axis] * h * t
        out.append(s.rcyl(rr, .03, c, axis=axis, seg=14, r=0))
    return out


def chest(L, w, d, h, loc, lid=True):
    """A sea chest: a plank box, a rounded lid, two iron straps over it, rope handles at the ends."""
    x, y, z = loc
    hb = h * (.72 if lid else 1.0)
    out = [s.rbox((w, d, hb), (x, y, z + hb / 2), r=.015, seg=1)]
    if lid:
        lh = h - hb
        out.append(s.place(s.loft([(-w / 2, lh * .98, d / 2, 3.0), (w / 2, lh * .98, d / 2, 3.0)], seg=16, axis="X"), (x, y, z + hb)))
    for sx in (-.3, .3):
        out.append(s.rbox((.05, d + .014, hb - .02), (x + sx * w, y, z + hb / 2 + .01), r=.006, seg=1))
    for sx in (-1, 1):
        out.append(torus(.05, .012, (x + sx * (w / 2 + .004), y, z + hb * .62), seg=10, seg_r=5, axis="X", arc=.5, a0=math.pi))
    return out


def rope_coil(R, r, loc, turns=3, dz=None):
    x, y, z = loc
    dz = dz or r * 1.7
    return [torus(R - k * r * .3, r, (x, y, z + r + k * dz), seg=20, seg_r=6) for k in range(turns)]


def p_desk(L):
    """Captain's chart table: a planked top with a fiddle rail on a sea-chest pedestal and a post, a chart and a
    monitor in a riveted brass frame."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", chest(L, .5, .7, .72, (.5, 0, 0), lid=False) + L.leg((-.68, -.28, .72), .72, .07) + L.leg((-.68, .28, .72), .72, .07))
    P.add("Accent", L.slab((.6, .4, .008), (-.25, -.08, .766)))
    acc, glow = L.screen((.5, .32), (0, .17, .98), "-Y")
    P.add("Accent", acc + L.slab((.06, .05, .2), (0, .2, .84)) + L.slab((.24, .16, .02), (0, .2, .775)))
    P.add("Eye", glow)
    return P


def p_terminal(L):
    """Bridge console: a slanted riveted desk with three porthole screens, a binnacle drum, an engine telegraph lever."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", chest(L, .5, .7, .72, (-.52, 0, 0), lid=False) + chest(L, .5, .7, .72, (.52, 0, 0), lid=False))
    P.add("AccentConsole", L.slab((.52, .17, .025), (0, -.1, .778)))
    for x in (-.34, 0, .34):
        ring, glass = L.porthole(.1, (x, .14, 1.0), bolts=4)
        P.add("Eye", glass).add("AccentConsole", ring + [s.rbox((.06, .06, .2), (x, .15, .86), r=.01, seg=1)])
    return P


def p_chair(L):
    """Captain's barrel chair: a cask-shaped seat with a curved half-barrel back on a swivel post and four feet."""
    P = D.Parts()
    P.add("Body", barrel(.26, .16, (0, 0, .46)))
    pts = [(math.cos(a) * .26, math.sin(a) * .26, z) for z in (0,) for a in [math.radians(d) for d in range(0, 181, 20)]]
    for z in (.56, .68, .8, .92):
        P.add("Body", [s.tube([(px, py, z) for px, py, _ in pts], .07, res=1, segments_per=2)])
    P.add("Body", [s.rbox((.5, .04, .08), (0, .26, 1.0), r=.01, seg=1)])
    P.add("Accent", L.rod((0, 0, .06), (0, 0, .4), .04))
    for k in range(4):
        a = k / 4 * math.tau
        P.add("Accent", L.rod((0, 0, .07), (math.cos(a) * .28, math.sin(a) * .28, .04), .025) + [s.ball(.03, (math.cos(a) * .28, math.sin(a) * .28, .03), seg=8)])
    return P


def p_server_rack(L):
    """Engine-room cabinet: a riveted iron case with banded seams and a grid of glowing round gauges."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, 1.0, 2.0), (0, 0, 0), kick=.05))
    for i in range(5):
        for j in (-1, 1):
            ring, glass = L.porthole(.07, (j * .15, -.5, .4 + i * .3), bolts=0)
            P.add("Body", ring).add("Eye", glass)
    return P


def p_cabinet(L):
    """Three sea chests stacked, iron straps and rope handles, the top one with a rounded lid."""
    P = D.Parts()
    P.add("Body", chest(L, .88, .48, .46, (0, 0, 0), lid=False) + chest(L, .84, .46, .44, (0, 0, .46), lid=False)
          + chest(L, .8, .44, .48, (0, 0, .9), lid=True))
    for z in (.3, .74):
        P.add("Accent", L.handle((0, -.245, z)))
    return P


def p_meeting_table(L):
    """Mess table: a long plank top with a fiddle rail on two barrel pedestals joined by a beam."""
    P = D.Parts()
    P.add("Body", L.slab((3.2, 1.3, .06), (0, 0, .73)))
    for x in (-1.0, 1.0):
        P.add("Accent", barrel(.3, .7, (x, 0, .35)))
    P.add("Accent", [s.rbox((1.8, .1, .1), (0, 0, .35), r=.01, seg=1)])
    return P


def p_plant(L):
    """A palm in a half barrel: a curved trunk and a crown of long fronds."""
    P = D.Parts()
    P.add("Accent", L.pot(.24, .42, (0, 0, 0)))
    P.add("Body", [s.tube([(0, 0, .38), (.03, .02, .8), (.02, .0, 1.18)], .045, res=1, segments_per=4)])
    for k in range(8):
        a = k / 8 * math.tau
        d = Vector((math.cos(a), math.sin(a), 0))
        base = Vector((.02, .0, 1.18))
        P.add("Body", [s.tube([tuple(base), tuple(base + d * .17 + Vector((0, 0, .17))), tuple(base + d * .33 + Vector((0, 0, .0)))],
                              .03, res=1, segments_per=3, radii=[1.2, 1, .4])])
    return P


def p_kitchen_counter(L):
    """Galley: a plank counter with a fiddle rail, porthole cupboard doors, a stove with a glowing porthole door."""
    P = D.Parts()
    P.add("Body", L.carcass((2.4, .65, .88), (0, 0, 0)))
    for i in range(4):
        x = -.9 + i * .6
        P.add("Body", L.door((.56, .66), (x, -.33, .48)))
        P.add("Accent", L.handle((x + .2, -.343, .62)))
    P.add("Accent", L.slab((2.44, .7, .05), (0, 0, .9)))
    P.add("Body", [s.rbox((.48, .42, .32), (.7, .02, 1.08), r=.04, seg=1)])
    ring, glass = L.porthole(.09, (.7, -.2, 1.08), bolts=4)
    P.add("Body", ring).add("Eye", glass)
    return P


def p_shelf(L):
    """Cargo rack: plank shelves with fiddle rails on posts, barrels and lashed crates on them."""
    P = D.Parts()
    for x in (-.96, .96):
        for y in (-.26, .26):
            P.add("Body", [s.rbox((.07, .07, 2.0), (x, y, 1.0), r=.008, seg=1)])
    for h in (.06, .55, 1.15):
        P.add("Body", L.slab((2.0, .6, .04), (0, 0, h)))
    P.add("Body", L.slab((2.0, .6, .04), (0, 0, 1.7)))
    P.add("Accent", barrel(.2, .4, (-.6, 0, .28)) + barrel(.18, .5, (.5, 0, .25), axis="X") + barrel(.2, .4, (.3, 0, .76))
          + [s.rbox((.45, .4, .3), (-.5, 0, 1.31), r=.01, seg=1)])
    return P


def p_boxes(L):
    """A pile of casks: two standing, one lying on top."""
    P = D.Parts()
    P.add("Accent", barrel(.28, .5, (0, 0, .25)) + barrel(.21, .4, (.55, .1, .2)) + barrel(.22, .55, (.06, -.04, .7), axis="X"))
    return P


def p_water_cooler(L):
    """Scuttlebutt: a cask on a cradle with a brass tap and a glass demijohn in a rope net on top."""
    P = D.Parts()
    P.add("Body", [s.rbox((.3, .3, .2), (0, 0, .1), r=.01, seg=1)] + barrel(.16, .8, (0, 0, .6)))
    P.add("Body", L.rod((0, -.14, .5), (0, -.18, .48), .02))
    P.add("Eye", L.lathe([(0, 0), (.13, 0), (.15, .1), (.15, .28), (.08, .38), (.04, .45), (0, .45)], loc=(0, 0, .99)))
    P.add("Body", [torus(.152, .012, (0, 0, 1.18), seg=16, seg_r=5)])
    return P


def p_whiteboard(L):
    """A sea chart hung from a yard between two oar posts, lashed with rope."""
    P = D.Parts()
    P.add("Body", L.slab((1.7, .03, 1.0), (0, 0, 1.4)))
    P.add("Accent", [s.rbox((1.86, .06, .06), (0, 0, 1.96), r=.02, seg=1)] + [s.rcyl(.04, 1.76, (0, 0, z), axis="X", seg=10, r=.008) for z in (.9, 1.9)])
    for x in (-.88, .88):
        P.add("Accent", [s.rbox((.06, .06, 1.96), (x, 0, .98), r=.02, seg=1)] + [s.loft([(0, .07, .02, 2.0), (.3, .06, .02, 2.0), (.36, 0, 0)],
                                                                                          seg=10, loc=(x, 0, .0))])
        P.add("Accent", [s.tube([(x * .97, 0, 1.92), (x * .9, -.02, 1.88)], .012, res=1)])
    return P


def p_sofa(L):
    """Settle on a sea chest: a long chest for a seat with cushions, a planked back with a rope rail, chest ends."""
    P = D.Parts()
    P.add("Body", chest(L, 1.8, .7, .34, (0, -.03, 0), lid=False))
    P.add("Body", L.cushion((.86, .64, .14), (-.45, -.05, .4)) + L.cushion((.86, .64, .14), (.45, -.05, .4)))
    for k in range(5):
        P.add("Body", [s.rbox((1.8, .06, .12), (0, .36, .41 + k * .1), r=.01, seg=1)])
    P.add("Accent", [s.tube([(-.9, .34, .83), (0, .33, .82), (.9, .34, .83)], .025, res=1, segments_per=4)])
    for x in (-1.0, 1.0):
        P.add("Accent", chest(L, .2, .84, .6, (x, 0, 0), lid=False))
    return P


def p_toilet_stall(L):
    """Ship's head: plank walls with iron bands, a porthole in the back wall, a brass bowl."""
    P = props.toilet_stall(L)
    ring, glass = L.porthole(.12, (0, .72, 1.5), bolts=6)
    P.add("Accent", ring + glass)
    for x in (-.5, .5):
        P.add("Body", [s.rbox((.06, 1.5, .05), (x, 0, z), r=.01, seg=1) for z in (.5, 1.6)])
    return P


def p_partition(L):
    """A canvas sail bellied by the wind in a spar frame, rope grommets along the mast."""
    P = D.Parts()
    sail = s.loft([(-.74, .52, .012, 3.0, 0, 0), (0, .56, .012, 3.0, 0, -.018), (.74, .5, .012, 3.0, 0, 0)], seg=12, axis="X")
    P.add("Body", [s.place(sail, (0, 0, .64))])
    P.add("Accent", [s.rbox((.05, .05, 1.26), (-.78, 0, .63), r=.02, seg=1), s.rbox((1.6, .05, .05), (0, 0, 1.21), r=.02, seg=1),
                     s.rbox((1.6, .05, .05), (0, 0, .08), r=.02, seg=1), s.rbox((.05, .05, 1.2), (.78, 0, .64), r=.02, seg=1)])
    for k in range(6):
        P.add("Accent", [torus(.04, .008, (-.76, 0, .22 + k * .17), seg=10, seg_r=4, axis="Z")])
    return P


def p_bookshelf(L):
    """Captain's bookcase: planked case, shelves with brass fiddle bars across, a porthole in the top panel."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.0, .35, .04), (0, 0, 1.98), r=.01, seg=1)])
    for x in (-.48, .48):
        P.add("Body", [s.rbox((.04, .35, 2.0), (x, 0, 1.0), r=.01, seg=1)])
    for h in (.03, .5, 1.0, 1.5):
        P.add("Body", [s.rbox((1.0, .35, .03), (0, 0, h), r=.008, seg=1)])
    P.add("Body", [s.rbox((.96, .02, 1.96), (0, .165, 1.0), r=.004, seg=1), s.rbox((.96, .03, .42), (0, -.15, 1.76), r=.01, seg=1)])
    ring, glass = L.porthole(.12, (0, -.15, 1.76), bolts=6)
    P.add("Accent", ring + glass)
    for i, h in enumerate((.045, .515, 1.015)):
        x = -.43
        for k in range(6 + i % 2):
            w = .07 + .03 * ((k + i) % 3)
            hh = .32 - .04 * ((k * 7 + i) % 3)
            P.add("Accent", L.slab((w, .26, hh), (x + w / 2, .02, h + hh / 2)))
            x += w + .012
        P.add("Accent", [s.rbox((.94, .02, .02), (0, -.16, h + .2), r=.006, seg=1)])
    return P


def p_printer(L):
    """A brass ship's printer: a riveted case with a ticker-tape drum on top and a glowing dial."""
    P = D.Parts()
    P.add("Body", L.carcass((.6, .6, .9), (0, 0, 0), kick=.04))
    P.add("Body", [s.rcyl(.15, .5, (0, .02, .9), axis="X", seg=14, r=.01)])
    P.add("Accent", L.slab((.36, .3, .03), (-.05, -.37, .9), rot=(math.radians(8), 0, 0)) + L.door((.5, .3), (0, -.31, .32)))
    ring, glass = L.porthole(.045, (.16, -.31, .78), bolts=0)
    P.add("Accent", ring).add("Eye", glass)
    return P


def p_fridge(L):
    """Ice chest: a tall riveted icebox with rounded corners, iron bands and two heavy lever latches."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .7, 1.8), (0, 0, 0), kick=.04))
    P.add("Body", L.door((.66, .5), (0, -.355, 1.5)) + L.door((.66, 1.1), (0, -.355, .68)))
    for z in (1.42, .95):
        P.add("Accent", [s.rbox((.06, .06, .12), (.27, -.38, z), r=.015, seg=1), s.rbox((.16, .04, .04), (.2, -.39, z + .05), r=.012, seg=1)])
    return P


def p_coffee_machine(L):
    """A copper kettle on a little stove with a glowing fire door."""
    P = D.Parts()
    P.add("Body", [s.rbox((.32, .36, .2), (0, -.02, .1), r=.02, seg=1), s.rbox((.3, .04, .3), (0, .17, .15), r=.01, seg=1)])
    P.add("Accent", L.lathe([(0, .19), (.13, .19), (.15, .26), (.13, .36), (.06, .4), (.05, .44), (0, .45)]))
    P.add("Accent", [s.tube([(0, -.12, .27), (0, -.18, .33), (0, -.22, .38)], .018, res=1)])
    P.add("Accent", [s.tube([(-.08, 0, .4), (0, 0, .49), (.08, 0, .4)], .01, res=1)])
    ring, glass = L.porthole(.035, (0, -.2, .1), bolts=0)
    P.add("Body", ring).add("Eye", glass)
    return P


def p_round_table(L):
    """Capstan table: a waisted capstan drum with whelps under a round top."""
    P = D.Parts()
    P.add("Body", L.cyl(.5, .05, (0, 0, .735)))
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.28, .08), (.16, .3), (.15, .5), (.2, .65), (.22, .72), (0, .72)]))
    for k in range(6):
        a = k / 6 * math.tau
        P.add("Accent", [s.rbox((.05, .05, .4), (math.cos(a) * .16, math.sin(a) * .16, .42), r=.01, seg=1)])
    return P


def p_armchair(L):
    """Barrel chair: a cask cut open into a chair, the staves rising into the back and arms, a hoop round them."""
    P = D.Parts()
    P.add("Body", barrel(.42, .4, (0, 0, .2)))
    P.add("Body", L.cushion((.58, .54, .1), (0, -.02, .44)))
    for k in range(9):
        a = math.radians(-20 + k * 27.5)
        hgt = .3 + .26 * math.sin(math.radians(k * 22.5))
        P.add("Accent", [s.rbox((.13, .05, hgt), (math.cos(a) * .38, math.sin(a) * .35, .3 + hgt / 2), r=.012, seg=1, rot=(0, 0, a + math.pi / 2))])
    P.add("Accent", [torus(.38, .02, (0, 0, .5), seg=20, seg_r=5, arc=.62, a0=math.radians(-25))])
    return P


def p_coffee_table(L):
    """A sea chest on short feet as a low table."""
    P = D.Parts()
    P.add("Body", chest(L, 1.0, .55, .34, (0, 0, .08), lid=True))
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Accent", [s.rbox((.07, .07, .1), (sx * .44, sy * .22, .05), r=.01, seg=1)])
    return P


def p_tv_stand(L):
    """A sea chest under a screen in a heavy riveted frame."""
    P = D.Parts()
    P.add("Body", chest(L, 1.4, .4, .5, (0, 0, 0), lid=False))
    acc, glow = L.screen((1.3, .76), (0, .06, 1.04), "-Y")
    P.add("Accent", acc + L.slab((.3, .2, .04), (0, .08, .52)) + L.slab((.06, .05, .15), (0, .09, .6)))
    P.add("Eye", glow)
    return P


def p_reception_desk(L):
    """Bridge counter: a planked hull-curved front with three portholes, a brass rail on top."""
    P = D.Parts()
    front = s.loft([(-1.2, .55, .05, 4.0, 0, .03), (0, .55, .05, 4.0, 0, -.04), (1.2, .55, .05, 4.0, 0, .03)], seg=12, axis="X")
    P.add("Body", [s.place(front, (0, -.36, .55))] + L.slab((.08, .76, 1.1), (-1.16, 0, .55)) + L.slab((.08, .76, 1.1), (1.16, 0, .55)))
    for k in range(5):
        P.add("Body", [s.rbox((2.3, .03, .03), (0, -.4, .15 + k * .2), r=.006, seg=1)])
    for x in (-.7, 0, .7):
        ring, glass = L.porthole(.09, (x, -.4, .6), bolts=4)
        P.add("Accent", ring + glass)
    P.add("Accent", L.slab((2.5, .3, .04), (0, -.33, 1.12)) + L.slab((2.3, .7, .04), (0, .02, .74)))
    acc, glow = L.screen((.48, .3), (.4, .15, .96), "-Y")
    P.add("Eye", glow + acc + L.slab((.06, .05, .2), (.4, .19, .83)))
    return P


def p_trash_bin(L):
    """A wooden bucket with iron hoops and a rope handle."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.15, 0), (.18, .42), (.16, .42), (.13, .03), (0, .03)], seg=14))
    P.add("Accent", [s.rcyl(.162, .03, (0, 0, .1), seg=14, r=0), s.rcyl(.182, .03, (0, 0, .4), seg=14, r=0)])
    return P


def p_coat_rack(L):
    """A mast post with belaying pins, a coil of rope at its foot and two oilskins hanging."""
    P = D.Parts()
    P.add("Accent", [s.rbox((.07, .07, 1.78), (0, 0, .89), r=.025, seg=1), s.rcyl(.24, .03, (0, 0, .015), seg=16, r=.008)]
          + rope_coil(.2, .035, (0, 0, .02), turns=2))
    for a in (0.0, 2.1, 4.2):
        d = Vector((math.cos(a), math.sin(a), 0))
        P.add("Accent", [align(s.rcyl(.02, .3, seg=8, r=.004), d * .02 + Vector((0, 0, 1.68)), d * .22 + Vector((0, 0, 1.68)))])
    P.add("Body", L.cushion((.34, .12, .62), (.12, 0, 1.34)) + L.cushion((.3, .12, .55), (-.08, .1, 1.36), rot=(0, 0, 2.1)))
    return P


def p_lockers(L):
    """Three narrow crew lockers with porthole doors, iron bands and ring pulls."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .5, 1.9), (0, 0, 0), kick=.06))
    for x in (-.3, 0.0, .3):
        P.add("Body", L.door((.29, 1.76), (x, -.255, .98)))
        P.add("Accent", L.handle((x + .09, -.27, .95)))
    return P


def p_pallet(L):
    """Cargo: crates on a pallet under a rope net."""
    P = D.Parts()
    P.add("Accent", L.slab((1.2, 1.0, .04), (0, 0, .13)))
    for y in (-.45, 0, .45):
        P.add("Accent", [s.rbox((1.2, .1, .11), (0, y, .055), r=.008, seg=1)])
    for x, y, z in ((-.3, -.24, .15), (.3, -.24, .15), (-.3, .24, .15), (.3, .24, .15), (-.3, -.24, .55), (-.3, .24, .55)):
        P.add("Body", [s.rbox((.55, .45, .4), (x, y, z + .2), r=.012, seg=1)])
    for k in range(4):
        y = -.42 + k * .28
        P.add("Accent", [s.tube([(-.6, y, .16), (-.6, y, .94), (.0, y, .94), (.05, y, .56), (.6, y, .56), (.6, y, .16)], .015, res=1, smooth=False)])
    return P


def p_wash_basin(L):
    """Ship's washstand: a planked counter with brass bowls, two big porthole mirrors on the bulkhead."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .5, .06), (0, 0, .85)) + L.carcass((1.6, .45, .82), (0, .02, 0)) + L.slab((1.6, .03, .34), (0, .24, 1.02)))
    for x in (-.4, .4):
        P.add("Accent", L.lathe([(0, 0), (.12, 0), (.17, .07), (.17, .09), (0, .09)], loc=(x, -.02, .84)))
        P.add("Accent", L.rod((x, .17, .88), (x, .17, 1.0), .02) + L.rod((x, .17, 1.0), (x, .08, .99), .015))
        P.add("Accent", [torus(.33, .04, (x * .95, .21, 1.55), seg=24, seg_r=6, axis="Y")])
        P.add("Eye", [s.rcyl(.33, .02, (x * .95, .235, 1.55), axis="Y", seg=20, r=0)])
    P.add("Body", [s.rbox((1.5, .02, .8), (0, .25, 1.52), r=.006, seg=1)])
    return P


def p_vending_machine(L):
    """Chandlery locker: a riveted iron cabinet with a big porthole window full of goods, a coin plate."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .8, 1.9), (0, 0, 0), kick=.05))
    ring, glass = L.porthole(.22, (-.05, -.37, 1.2), bolts=8)
    P.add("Accent", ring).add("Eye", glass)
    P.add("Accent", L.slab((.6, .03, .15), (-.1, -.41, .28)) + L.slab((.14, .03, .5), (.33, -.41, .7)))
    return P


def p_ping_pong_table(L):
    """A deck table on cross-braced trestles."""
    P = D.Parts()
    P.add("Body", [s.rbox((2.74, 1.52, .05), (0, 0, .735), r=.01, seg=1)])
    for x in (-1.0, 1.0):
        for sy in (-1, 1):
            n = (Vector((.5, 0, .725))).length + .03
            P.add("Accent", [align(s.rbox((.07, .07, n), r=.01, seg=1), Vector((x - .25, sy * .55, .725)), Vector((x + .25, sy * .55, .0))),
                             align(s.rbox((.07, .07, n), r=.01, seg=1), Vector((x + .25, sy * .55, .725)), Vector((x - .25, sy * .55, .0)))])
        P.add("Accent", [s.rbox((.08, 1.2, .08), (x, 0, .37), r=.01, seg=1)])
    P.add("Eye", [s.rbox((.02, 1.72, .15), (0, 0, .83), r=.004, seg=1)])
    return P


def p_bean_bag(L):
    """A great coil of mooring rope."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.32, .3, (0, 0, .15), seg=16, r=.02)])
    for k, (R, z) in enumerate(((.37, .08), (.36, .22), (.33, .36), (.27, .49), (.18, .6))):
        P.add("Body", [torus(R, .075, (0, 0, z), seg=22, seg_r=6)])
    return P


def p_arcade_machine(L):
    """Diving-helmet arcade: a riveted cabinet under a huge brass helmet whose front porthole is the screen."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .78, 1.1), (0, 0, 0), kick=.05))
    P.add("Body", [s.ball(.35, (0, 0, 1.45), seg=18)] + [s.rcyl(.37, .1, (0, 0, 1.12), seg=18, r=.02)])
    ring, glass = L.porthole(.15, (0, -.33, 1.42), bolts=6)
    P.add("Accent", ring).add("Eye", glass)
    for sx in (-1, 1):
        r2, g2 = L.porthole(.07, (sx * .33, -.02, 1.46), axis="X" if sx > 0 else "-X", bolts=0)
        P.add("Accent", r2 + g2)
    P.add("Accent", L.slab((.7, .2, .06), (0, -.44, .98), rot=(math.radians(-10), 0, 0)))
    for x in (-.12, .12):
        P.add("Accent", [s.ball(.035, (x, -.47, 1.03), seg=8)])
    return P


def p_safe(L):
    """Strongbox: an iron-strapped chest with a domed lid, a big padlock and a glowing keyhole lamp."""
    P = D.Parts()
    P.add("Body", chest(L, .8, .68, 1.0, (0, 0, 0), lid=True))
    P.add("Accent", [s.rbox((.16, .06, .2), (0, -.36, .55), r=.03, seg=1), torus(.06, .016, (0, -.36, .66), seg=12, seg_r=5, axis="Y", arc=.5)])
    ring, glass = L.porthole(.03, (.22, -.345, .62), bolts=0)
    P.add("Accent", ring).add("Eye", glass)
    return P


def p_desk_lamp(L):
    """Gimballed hurricane lamp on a bulkhead bracket: a brass font, a glass chimney with the flame (Eye), a hood."""
    P = D.Parts()
    P.add("Body", [s.rbox((.12, .03, .2), (0, .06, .12), r=.01, seg=1)] + L.rod((0, .06, .14), (0, -.1, .14), .012)
          + [torus(.07, .01, (0, -.16, .2), seg=14, seg_r=5)])
    P.add("Body", L.rod((0, -.1, .14), (0, -.1, .2), .01))
    P.add("Accent", L.lathe([(0, .1), (.05, .1), (.06, .14), (.04, .2), (0, .21)], loc=(0, -.16, 0)))
    P.add("Eye", L.lathe([(0, .2), (.035, .21), (.045, .27), (.03, .33), (0, .335)], loc=(0, -.16, 0)))
    P.add("Accent", L.shade(.06, .025, .05, (0, -.16, .325)) + L.rod((0, -.16, .32), (0, -.16, .372), .008))
    return P


def p_floor_lamp(L):
    """A ship's lantern hung from a yard on a mast post: a caged glass round the flame."""
    P = D.Parts()
    P.add("Body", [s.rbox((.4, .4, .05), (0, 0, .025), r=.01, seg=1), s.rbox((.05, .05, 1.6), (.12, 0, .82), r=.015, seg=1),
                   s.rbox((.2, .04, .04), (.04, 0, 1.6), r=.01, seg=1)])
    P.add("Accent", L.rod((0, 0, 1.6), (0, 0, 1.52), .008) + L.shade(.12, .07, .1, (0, 0, 1.43)))
    P.add("Accent", L.lathe([(0, 1.24), (.1, 1.24), (.11, 1.27), (.06, 1.29), (0, 1.29)]))
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        P.add("Accent", L.rod((math.cos(a) * .09, math.sin(a) * .09, 1.27), (math.cos(a) * .1, math.sin(a) * .1, 1.44), .008))
    P.add("Eye", L.lathe([(0, 1.28), (.07, 1.3), (.08, 1.38), (.05, 1.44), (0, 1.45)]))
    return P


def p_ceiling_lamp(L):
    """Bulkhead light: a planked mount, an oval glass with three cage bars across."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.24, .66, .04), (0, 0, -.02), r=.01, seg=1)])
    P.add("Eye", [s.place(s.loft([(0, .52, .24, 2.0), (.03, .5, .22, 2.0)], seg=24), (0, 0, -.067))])
    P.add("Accent", [s.place(s.loft([(0, .56, .28, 2.0), (.04, .56, .28, 2.0)], seg=24), (0, 0, -.06))])
    for x in (-.25, 0, .25):
        P.add("Accent", [s.rbox((.03, .5, .02), (x, 0, -.07), r=.006, seg=1)])
    return P


def p_exit_sign(L):
    P = props.exit_sign(L)
    for sx in (-1, 1):
        for sz in (-1, 1):
            P.add("Body", [s.ball(.014, (sx * .17, 0, sz * .065), seg=6, scale=(1, 12, 1))])
    return P


def p_hatch(L):
    """Deck hatch: a heavy round lid with dog clamps round the rim, a hinge and a spoked hand wheel."""
    P = D.Parts()
    P.add("Body", L.cyl(.76, .06, (0, 0, .03)))
    for k in range(6):
        a = k / 6 * math.tau
        P.add("Accent", [s.rbox((.1, .14, .05), (math.cos(a) * .74, math.sin(a) * .74, .045), r=.01, seg=1, rot=(0, 0, a))])
    P.add("Accent", [torus(.3, .025, (0, 0, .085), seg=24, seg_r=6), s.rcyl(.06, .05, (0, 0, .07), seg=12, r=.01)])
    for k in range(4):
        a = k / 4 * math.tau
        P.add("Accent", [align(s.rcyl(.015, .3, seg=6, r=0), Vector((0, 0, .08)), Vector((math.cos(a) * .3, math.sin(a) * .3, .08)))])
    P.add("Eye", L.lathe([(.76, 0), (.9, 0), (.9, .02), (.76, .02)], seg=32))
    return P


PROP_DESIGNS = {"desk": p_desk, "terminal": p_terminal, "chair": p_chair, "server_rack": p_server_rack, "cabinet": p_cabinet, "meeting_table": p_meeting_table, "plant": p_plant, "kitchen_counter": p_kitchen_counter, "shelf": p_shelf, "boxes": p_boxes, "water_cooler": p_water_cooler, "whiteboard": p_whiteboard, "sofa": p_sofa, "toilet_stall": p_toilet_stall, "partition": p_partition, "bookshelf": p_bookshelf, "printer": p_printer, "fridge": p_fridge, "coffee_machine": p_coffee_machine, "round_table": p_round_table, "armchair": p_armchair, "coffee_table": p_coffee_table, "tv_stand": p_tv_stand, "reception_desk": p_reception_desk, "trash_bin": p_trash_bin, "coat_rack": p_coat_rack, "lockers": p_lockers, "pallet": p_pallet, "wash_basin": p_wash_basin, "vending_machine": p_vending_machine, "ping_pong_table": p_ping_pong_table, "bean_bag": p_bean_bag, "arcade_machine": p_arcade_machine, "safe": p_safe, "desk_lamp": p_desk_lamp, "floor_lamp": p_floor_lamp, "ceiling_lamp": p_ceiling_lamp, "exit_sign": p_exit_sign, "hatch": p_hatch}


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "security_camera": security_camera, "sensor_pir": pir, "attach_eye": attach_eye,
    "attach_nose": attach_nose,
}
DESIGNS.update(PROP_DESIGNS)
