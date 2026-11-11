"""S10 Grid — «Сеть»: sleek dark machines of a digital world.

Language: long aerodynamic shells (lofts stretched along the direction of travel), light seams (Eye) running along
edges and around waists, wrap-around visor bands instead of round lenses, swept fins, and hover plates where a
machine has no legs. Every robot carries one glowing line that reads its status from any side.
Our motifs: the Y-seam on the chest, the halo band on the head, the hover plate with a glow rim — no discs to throw.
"""
import math

import bmesh
from mathutils import Euler, Vector

import shapes as s
from lang import Lang, align
import designs as D


class Style(Lang):
    name = "S10_Grid"
    seg = 24
    soft = 0.1
    edge = 0.016
    hull_kind = "rounded"
    eye_kind = "visor"
    wheel_kind = "solid"
    limb_kind = "tube"

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        # joints and knobs: a sphere costs ~1k triangles at the language's 32 segments
        return [s.ball(radius, loc, seg=16 if radius > .05 else 12, scale=scale)]

    # ------------------------------------------------------------------ furniture: dark slabs with glowing edges
    edge_kind = "chamfer"
    cushion_puff = 0.15
    handle_kind = "recess"
    foliage_kind = "spiky"
    _slabs = None

    def slab(self, size, loc, rot=(0, 0, 0)):
        """A dark chamfered board; big boards are remembered so the signature can light their front top edge."""
        if self._slabs is None:
            self._slabs = []
        if not any(rot):
            self._slabs.append((size, loc))
        w, d, h = size
        # boards overlap their neighbours by a few mm (stacked boards in the shared designs touch face to face or
        # leave hairline gaps)
        return [s.rbox((w, d, h + .012), loc, r=min(min(size) * .3, .014), seg=1, rot=rot)]

    def carcass(self, size, loc, kick=0.06):
        """A dark cabinet floating on a recessed plinth; the plinth's front is a light line (underglow)."""
        w, d, h = size
        x, y, z = loc
        # the body is a wedge: its front top edge is cut away in one wide chamfer, lit along its middle
        c = min(.1, d * .22, (h - kick) * .22)
        z0 = z + kick - .006
        prof = [(y - d / 2, z0), (y + d / 2, z0), (y + d / 2, z + h), (y - d / 2 + c, z + h), (y - d / 2, z + h - c)]
        out = [extrude_x(prof, x - w / 2, x + w / 2)]
        if self._slabs is None:
            self._slabs = []
        self._slabs.append(("edge", (w, d, h), (x, y, z), c))
        if kick > 0:
            out += [s.rbox((w - .06, d - .08, kick + .012), (x, y + .02, z + (kick + .012) / 2), r=.006, seg=1)]
            if self._slabs is not None:
                self._slabs.append(("kick", (w - .1, d - .08, kick), (x, y + .02, z + kick / 2)))
        return out

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """Upholstery as crisp chamfered pads, lit along their front top edge like every board."""
        w, d, h = size
        if not any(rot) and self._slabs is not None:
            self._slabs.append(((w, d, h), loc))
        elif not any(rot):
            self._slabs = [((w, d, h), loc)]
        return [s.rbox(size, loc, r=min(min(size) * .3, .03), seg=2, rot=rot, taper=(.97, .97))]

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A fin: a thin deep blade narrowing to the floor."""
        x, y, z = top
        foot = (x + splay[0], y + splay[1], max(0.0, z - length))
        bm = s.rbox((thick * .5, thick * 1.5, length + .01), r=.006, seg=1, taper=(.7, .55))
        return [align(bm, top, foot)]

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Two swept waterfall fins instead of four legs."""
        out = []
        hd = half_d - inset + .02
        for sx in (-1, 1):
            out += fin_panel(sx * (half_w - inset), -hd, hd, top_z - length, top_z + .015, t=max(.03, thick * .8), sweep=min(.15, hd * .35))
        return out

    def pot(self, r, h, loc):
        return [s.loft([(0, r * .75, r * .75, 3.4), (h * .9, r, r, 3.4), (h, r * 1.04, r * 1.04, 3.4)], seg=24, loc=loc)]

    def signature(self, model, P):
        slabs, self._slabs = self._slabs or [], None
        # sensor modules get a thin light seam so they belong to the same machines
        if model == "attach_seismic":
            P.add("Eye", band(.2, .02, .02, h=.03, grow=.008, seg=16))
        elif model == "attach_nose":
            P.add("Eye", band(.12, .072, .067, axis="-Y", h=.03, grow=.006, seg=24))
        elif not model.startswith(("robot_", "sensor_", "attach_", "security_", "laser_")) and model not in OWN_LIGHT:
            # furniture: the front top edge of every big board is a light line (the game's Eye, or trim if the prop has
            # no Eye role)
            for rec in slabs:
                if rec[0] == "edge":
                    (w, d, h), (x, y, z), c = rec[1], rec[2], rec[3]
                    P.add("Eye", [s.rbox((w - .03, .026, .02), (x, y - d / 2 + c / 2 - .004, z + h - c / 2 - .004), r=.005, seg=1,
                                         rot=(math.radians(45), 0, 0))])
                    continue
                if rec[0] == "kick":
                    (w, d, h), (x, y, z) = rec[1], rec[2]
                    P.add("Eye", [s.rbox((w, .02, .02), (x, y - d / 2 - .004, z + .005), r=.005, seg=1)])
                    continue
                (w, d, h), (x, y, z) = rec
                if w >= .3 and d >= .1 and h >= .025:
                    P.add("Eye", [s.rbox((w - .04, .024, .024), (x, y - d / 2 + .006, z + h / 2 + .006 - .012), r=.005, seg=1)])
        return P


# =========================================================================== helpers


def lo(rings, axis="Z", loc=(0, 0, 0), caps=0.0, seg=24):
    return [s.loft(rings, seg=seg, axis=axis, loc=loc, round_caps=caps)]


def limb(p0, p1, rings, seg=16):
    """A loft whose ring z are fractions -0.5..0.5 of the length, laid from p0 to p1."""
    n = (Vector(p1) - Vector(p0)).length
    rr = [(r[0] * n,) + tuple(r[1:]) for r in rings]
    return [align(s.loft(rr, seg=seg), p0, p1)]


def line(points, r=0.022):
    """A light seam: a tube half sunk into the surface (>= 4 cm across, or it vanishes at play distance)."""
    return [s.tube(points, r, res=1, smooth=True)]


def revolve(profile, seg=32, axis="Z", loc=(0, 0, 0)):
    """Solid of revolution from a CLOSED (r, z) profile — rings, tori, washers (lathe would cap them into discs)."""
    bm = bmesh.new()
    rows = []
    for r, z in profile:
        rows.append([bm.verts.new((r * math.cos(k / seg * math.tau), r * math.sin(k / seg * math.tau), z)) for k in range(seg)])
    for i in range(len(rows)):
        a, b = rows[i], rows[(i + 1) % len(rows)]
        for j in range(seg):
            bm.faces.new((a[j], a[(j + 1) % seg], b[(j + 1) % seg], b[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


def torus(R, r, loc=(0, 0, 0), axis="Z", seg=32, rseg=8, zs=1.0):
    prof = [(R + r * math.cos(k / rseg * math.tau), r * zs * math.sin(k / rseg * math.tau)) for k in range(rseg)]
    return [revolve(prof, seg, axis, loc)]


def washer(r0, r1, z0, z1, loc=(0, 0, 0), axis="Z", seg=32):
    return [revolve([(r0, z0), (r1, z0), (r1, z1), (r0, z1)], seg, axis, loc)]


def band(z, a, b, n=2.4, x=0.0, y=0.0, h=0.035, grow=0.012, axis="Z", loc=(0, 0, 0), seg=32):
    """A glowing band around a loft at ring position z (the ring's a, b, n, offset): wrap visor, waist seam."""
    return [s.loft([(z - h / 2, a + grow, b + grow, n, x, y), (z + h / 2, a + grow, b + grow, n, x, y)],
                   seg=seg, axis=axis, loc=loc)]


def xf(p, loc=(0, 0, 0), rot=(0, 0, 0)):
    return tuple(Euler(rot, "XYZ").to_matrix() @ Vector(p) + Vector(loc))


def placed(bms, loc, rot):
    return [s.place(b, loc, rot) for b in bms]


# =========================================================================== walkers

def guard(L):
    """The sentinel: one tapered shell from hips to shoulders (V torso, pinched waist) with a glowing Y seam and a
    waist band, a bullet helmet ringed by a tall wrap-around visor, teardrop pauldrons with fins trailing back,
    blade forearms with a light edge, slim legs on speed-skate boots."""
    P = D.Parts()
    torso = [(.83, .19, .12, 2.6), (.93, .23, .14, 2.8), (1.05, .17, .12, 2.4), (1.18, .2, .14, 2.6, 0, -.005),
             (1.34, .29, .17, 2.8, 0, -.015), (1.52, .40, .19, 3.0, 0, -.02), (1.64, .37, .17, 2.6, 0, -.01), (1.71, .22, .12, 2.2)]
    P.add("Body", lo(torso, caps=.03))
    for sx in (-1, 1):
        P.add("Eye", line([(sx * .31, -.165, 1.6), (sx * .15, -.186, 1.45), (0, -.19, 1.33)]))
    P.add("Eye", line([(0, -.19, 1.34), (0, -.17, 1.22), (0, -.13, 1.1)]))
    P.add("Eye", band(1.05, .17, .12, h=.055, grow=.008))
    pd = s.loft([(0, .08, .14, 2.4), (.12, .1, .16, 2.6, -.01, .01), (.24, .06, .12, 2.4, .02, .05), (.3, 0, 0, 2, .03, .08)],
                seg=24, axis="X", loc=(.27, 0, 1.62))
    P.add("Accent", s.mirror_x(pd))
    for sx in (-1, 1):  # fins trailing back from the shoulders
        fin = s.loft([(0, .025, .13, 2.2), (.32, .02, .1, 2.2, 0, .03), (.55, 0, 0, 2.0, 0, .08)], seg=12)
        P.add("Accent", [s.place(fin, (sx * .33, .06, 1.64), (math.radians(-72), sx * math.radians(14), 0))])
    for sx in (-1, 1):
        sh, el, wr = (sx * .45, 0, 1.53), (sx * .5, -.01, 1.18), (sx * .48, -.1, .84)
        P.add("Accent", L.ball(.065, sh) + L.ball(.06, el))
        P.add("Body", limb(sh, el, [(-.5, .05, .05, 2.2), (0, .06, .062, 2.4), (.5, .045, .045, 2.2)], seg=16))
        P.add("Body", limb(el, wr, [(-.55, .06, .06, 2.4), (-.2, .085, .1, 2.8), (.35, .075, .085, 2.6), (.55, .05, .055, 2.2)]))
        P.add("Eye", line([(sx * .565, -.02, 1.12), (sx * .565, -.06, .98), (sx * .535, -.09, .86)], r=.018))
        P.add("Accent", [s.place(s.ball(.07, seg=16, scale=(.8, 1, 1.25)), (sx * .48, -.12, .77))])
    P.add("Accent", L.cyl(.065, .14, (0, 0, 1.71)))
    helm = [(1.74, .1, .13, 2.2, 0, -.01), (1.79, .145, .2, 2.4, 0, -.02), (1.9, .15, .21, 2.4, 0, 0),
            (1.98, .11, .16, 2.2, 0, .03), (2.04, 0, 0, 2.0, 0, .05)]
    P.add("Body", lo(helm, caps=.02))
    P.add("Eye", band(1.85, .148, .205, x=0, y=-.011, h=.065, grow=.01))
    P.add("Accent", [s.rbox((.035, .3, .09), (0, .08, 2.0), r=.012, seg=2, rot=(math.radians(-18), 0, 0))])
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.085, (x, 0, D.GUARD_HIP_Z)))
        P.add(leg, lo([(D.GUARD_KNEE + .01, .06, .07, 2.4, x, 0), (.7, .085, .1, 2.6, x, -.01), (.9, .09, .1, 2.4, x, 0)]))
        P.add(leg, L.cyl(.07, .17, (x, 0, D.GUARD_KNEE), axis="X"))
        P.add(leg, lo([(.05, .08, .12, 2.6, x, .0), (.17, .085, .125, 2.8, x, .01), (.32, .062, .085, 2.6, x, .015), (D.GUARD_KNEE - .01, .065, .075, 2.4, x, 0)]))
        P.add(leg, lo([(0, .055, .045, 2.4), (.12, .07, .05, 2.6, 0, .005), (.28, .045, .03, 2.2, 0, -.01), (.33, 0, 0, 2.0, 0, -.02)],
                      axis="-Y", loc=(x, .08, .045), seg=20))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hound(L):
    """The tracker: a low dart-shaped fuselage with no neck: it runs out into a long sensor nose ringed by a glowing
    sniffer band; engine-like nacelles over the four blade legs, a tall swept fin at the back, light lines down its
    flanks. From above: an arrow on four blades."""
    P = D.Parts()
    body = [(-.58, .03, .03, 2.0, 0, .04), (-.42, .13, .1, 2.4, 0, .03), (-.1, .19, .14, 2.6), (.25, .17, .14, 2.6),
            (.55, .1, .085, 2.4, 0, -.02), (.85, .05, .045, 2.2, 0, -.06), (1.08, .012, .012, 2.0, 0, -.09)]
    P.add("Body", lo(body, axis="-Y", loc=(0, 0, .86), seg=24))
    P.add("Eye", band(.68, .078, .066, n=2.3, y=-.04, axis="-Y", loc=(0, 0, .86), h=.06, grow=.006, seg=24))
    P.add("Eye", band(.85, .05, .045, n=2.2, y=-.06, axis="-Y", loc=(0, 0, .86), h=.035, grow=.006, seg=16))
    for sx in (-1, 1):
        P.add("Eye", line([(sx * .185, .3, .86), (sx * .19, -.1, .86), (sx * .155, -.4, .85), (sx * .09, -.7, .82)], r=.02))
    fin = s.loft([(0, .025, .2, 2.2), (.22, .02, .15, 2.2, 0, .1), (.38, 0, 0, 2.0, 0, .24)], seg=12)
    P.add("Body", [s.place(fin, (0, .2, .97), (math.radians(-10), 0, 0))])
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        P.add("Body", lo([(-.13, .05, .05, 2.4), (0, .075, .085, 2.6), (.13, .05, .05, 2.4)], axis="-Y", loc=(x, y, .74), seg=16))
        leg = f"AccentLeg{i}"
        P.add(leg, L.ball(.07, (x, y, D.HOUND_HIP)))
        P.add(leg, lo([(D.HOUND_KNEE, .03, .045, 2.4, x, y), (.47, .035, .085, 2.4, x, y + .02), (D.HOUND_HIP + .02, .04, .07, 2.4, x, y)], seg=16))
        P.add(leg, L.cyl(.05, .1, (x, y, D.HOUND_KNEE), axis="X"))
        P.add(leg, lo([(.03, .02, .03, 2.2, x, y - .01), (.2, .028, .05, 2.4, x, y + .01), (D.HOUND_KNEE + .01, .03, .045, 2.4, x, y)], seg=16))
        P.add(leg, lo([(0, .035, .022, 2.4), (.1, .04, .025, 2.4), (.17, 0, 0, 2.0, 0, -.01)], axis="-Y", loc=(x, y + .05, .025), seg=16))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """The halo crawler: a flat ring hull with a glowing inner rim around a domed core, a light slit on the core's
    face; the eight legs spring out from under the ring."""
    P = D.Parts()
    c = (0, -.02, .19)
    P.add("Body", [revolve([(.13, -.04), (.28, -.035), (.3, -.01), (.29, .02), (.24, .04), (.13, .035)], seg=40, loc=c)])
    P.add("Eye", washer(.115, .14, .03, .05, loc=c, seg=40))
    P.add("Eye", washer(.29, .305, -.015, .005, loc=c, seg=40))
    P.add("Body", L.lathe([(0, -.07), (.12, -.07), (.135, -.02), (.12, .05), (.07, .09), (0, .1)], seg=28, loc=c))
    P.add("Eye", lo([(-.02, .07, .018, 2.4), (.02, .07, .018, 2.4)], axis="-Y", loc=(0, -.13, .24), seg=16))
    for k in range(4):
        a = math.radians(45 + 90 * k)
        P.add("Body", [s.rbox((.2, .03, .03), (math.cos(a) * .2, -.02 + math.sin(a) * .2, .175), r=.008, seg=1, rot=(0, 0, a))])
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.ball(.035, hip) +
              limb(hip, knee, [(-.5, .025, .03, 2.2), (0, .03, .05, 2.4), (.5, .022, .03, 2.2)], seg=12) + L.ball(.034, knee))
        P.add(f"AccentLeg{i}Lower", limb(knee + (foot - knee) * .03, foot + Vector((0, 0, -.02)),
                                         [(-.5, .024, .028, 2.2), (.2, .02, .03, 2.2), (.5, .004, .004, 2.0)], seg=12))
    return P


# =========================================================================== wheeled and tracked

def grid_wheel(c, r, w):
    """A solid wheel with a recessed hub face (no glow: wheels turn, the Eye does not)."""
    prof = [(0, -w / 2), (r * .7, -w / 2), (r * .78, -w * .42), (r * .95, -w / 2), (r, -w * .3), (r, w * .3), (r * .95, w / 2),
            (r * .78, w * .42), (r * .7, w / 2), (0, w / 2)]
    return [s.lathe(prof, seg=32, axis="X", loc=c)]


def listener(L):
    """A low hover deck on four wheels with two tall leaf-ears whose front edges glow."""
    P = D.Parts()
    W = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", grid_wheel(c, .22, .12))
        P.add("Body", L.rod((c[0] * .55, c[1], .22), (c[0] * 1.02, c[1], .22), .045))
    P.add("Body", lo([(-.5, .22, .08, 2.4, 0, .02), (-.35, .3, .12, 3.0), (.3, .3, .12, 3.0), (.52, .18, .07, 2.4, 0, -.02)],
                     axis="-Y", loc=(0, 0, .36), caps=.03))
    deck = s.rbox((1.06, 1.0, .07), (0, 0, .5), r=.03, seg=2, taper=(.94, .94))
    P.add("Body", [deck])
    P.add("Eye", [s.rbox((1.08, 1.02, .022), (0, 0, .49), r=.01, seg=1)])
    for sx in (-1, 1):
        rot = (0, sx * math.radians(8), 0)
        base = (sx * .37, .1, .52)
        leaf = s.loft([(0, .045, .12, 2.4), (.3, .05, .22, 2.6), (.6, .04, .18, 2.4, 0, .02), (.88, 0, 0, 2.0, 0, .08)], seg=24)
        P.add("Body", [s.place(leaf, base, rot)])
        edge = [xf(p, base, rot) for p in ((0, -.12, .04), (0, -.215, .3), (0, -.16, .62), (0, -.04, .82))]
        P.add("Eye", line(edge, r=.016))
    P.add("Body", lo([(-.15, .05, .04, 2.2), (.15, .1, .06, 2.4)], axis="-Y", loc=(0, -.35, .58), seg=20))
    P.add("Eye", band(.08, .08, .052, axis="-Y", loc=(0, -.35, .58), h=.04, grow=.008, seg=20))
    return P


def worker(L):
    """A wedge cart whose swan-neck arm lifts a data-cube crate (light seams on its edges) over its nose."""
    P = D.Parts()
    W = [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", grid_wheel(c, .12, .1))
        P.add("Body", L.rod((c[0] * .4, c[1], .13), (c[0] * 1.0, c[1], .12), .04))
    P.add("Body", lo([(-.42, .16, .08, 2.6, 0, .05), (-.25, .23, .14, 3.0), (.3, .23, .13, 3.0), (.44, .14, .07, 2.4, 0, -.03)],
                     axis="-Y", loc=(0, .02, .28), caps=.03))
    P.add("Body", [s.tube([(0, .3, .38), (0, .32, .7), (0, .2, .92), (0, .02, .96)], .055, res=2)])
    P.add("Accent", [s.rbox((.48, .44, .04), (0, -.15, .945), r=.012, seg=2)])
    P.add("Accent", [s.rbox((.44, .42, .36), (0, -.15, 1.145), r=.03, seg=2)])
    P.add("Eye", [s.rbox((.46, .44, .03), (0, -.15, 1.15), r=.01, seg=1)])
    for x in (-.2, .2):
        P.add("Eye", [s.rbox((.03, .44, .34), (x, -.15, 1.145), r=.01, seg=1)])
    P.add("Body", lo([(0, .1, .06, 2.4), (.12, .12, .07, 2.6), (.2, .07, .045, 2.2)], axis="-Y", loc=(0, -.24, .48), seg=24))
    P.add("Eye", band(.15, .1, .06, axis="-Y", loc=(0, -.24, .48), h=.04, grow=.008, seg=24))
    P.add("Eye", band(.38, .23, .13, axis="-Y", loc=(0, .02, .29), h=.03, grow=.006, n=3.0))
    return P


def sweeper(L):
    """A manta: a wide flat lens-shaped shell with a glowing rim riding on its own spinning lidar ring, a dark base
    on three wheels, two mandible brushes in front."""
    P = D.Parts()
    W = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", grid_wheel(c, .08, .06))
        x, y, _ = c
        P.add("Accent", L.rod((x - .06, y, .08), (x + .06, y, .08), .02))
        for sx in (-1, 1):
            P.add("Accent", [s.rbox((.025, .08, .12), (x + sx * .045, y, .125), r=.01, seg=1)])
    P.add("Accent", L.lathe([(0, .14), (.3, .14), (.34, .16), (.34, .17), (.3, .19), (0, .19)], seg=40))
    P.add("AccentLidar", L.lathe([(0, .18), (.3, .18), (.33, .2), (.33, .24), (.3, .262), (0, .262)], seg=40)
          + L.box((.1, .06, .05), (0, -.33, .22)))
    P.add("Body", lo([(.257, .36, .34, 2.6), (.3, .45, .43, 2.8), (.36, .38, .36, 2.6), (.44, .2, .18, 2.2), (.48, 0, 0, 2.0)], seg=40))
    P.add("Eye", band(.3, .45, .43, n=2.8, h=.025, grow=.006, seg=40))
    for sx in (-1, 1):
        c = (sx * .27, -.25, .015)
        P.add("Accent", L.cyl(.12, .02, c) + L.rod((c[0], c[1], .03), (sx * .04, -.3, .14), .022))
        for k in range(3):
            a = k / 3 * math.pi
            P.add("Accent", [s.rbox((.24, .025, .016), (c[0], c[1], .03), r=.005, seg=1, rot=(0, 0, a))])
    P.add("Eye", lo([(-.03, .1, .02, 2.2), (.03, .1, .02, 2.2)], axis="-Y", loc=(0, -.31, .36), seg=16))
    return P


def thumper(L):
    """A long low wedge, like the nose of a racing machine, over two pontoons hidden under side fairings; a thick
    glowing chevron on its back points forward; two hydraulic stamps press glowing pads into the floor."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .55
        P.add("Accent", lo([(-.7, .1, .16, 2.6, 0, .03), (-.55, .12, .27, 4.0), (.5, .12, .27, 4.0), (.66, .1, .15, 2.6, 0, .04)],
                           axis="-Y", loc=(x, 0, .28), caps=.03, seg=12))
        P.add("Body", lo([(-.74, .12, .07, 2.4, 0, -.01), (-.6, .16, .13, 2.8), (.52, .16, .13, 2.8), (.74, .1, .05, 2.4, 0, -.06)],
                         axis="-Y", loc=(x, 0, .47), caps=.03, seg=18))
        P.add("Eye", line([(sx * .7, .5, .47), (sx * .7, 0, .47), (sx * .7, -.5, .47)], r=.025))
    P.add("Body", lo([(-.7, .38, .2, 3.2, 0, .1), (-.3, .44, .26, 3.4, 0, .08), (.2, .4, .2, 3.0, 0, .0), (.55, .26, .1, 2.6, 0, -.08),
                      (.75, .08, .04, 2.2, 0, -.12)], axis="-Y", loc=(0, .05, .6), caps=.04, seg=18))
    for sx in (-1, 1):
        P.add("Eye", line([(sx * .32, .3, .9), (sx * .16, .02, .835), (0, -.25, .735)], r=.032))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", L.rod((x * .8, -.3, .6), (x, -.82, .6), .06))
        P.add("Accent", L.ball(.08, (x, -.84, .6)))
        P.add("Accent", L.rod((x, -.84, .6), (x, -.85, .1), .055, kind="piston"))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
        P.add("Accent", L.cyl(.13, .06, (x, -.85, .07)))
    P.add("Accent", L.rod((-.3, -.84, .32), (.3, -.84, .32), .04))
    P.add("Eye", lo([(-.02, .16, .03, 2.4), (.02, .16, .03, 2.4)], axis="-Y", loc=(0, -.86, .32), seg=20))
    return P


# =========================================================================== tall

def sorter(L):
    """A blade column carrying a big lens inside a halo ring held by four spokes."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.3, .03), (.22, .08), (0, .09)]))
    P.add("Eye", washer(.28, .305, .005, .025))
    P.add("Body", lo([(.07, .13, .09, 1.6), (.6, .075, .05, 1.6), (1.05, .06, .045, 1.6)], seg=24))
    P.add("Body", L.lathe([(0, 0), (.21, 0), (.24, .04), (.24, .13), (.2, .17), (0, .17)], axis="-Y", loc=(0, .1, 1.25)))
    P.add("Eye", L.lathe([(.2, 0), (.2, .01), (.12, .03), (0, .04)], axis="-Y", loc=(0, -.068, 1.25)))
    P.add("Accent", torus(.32, .03, (0, .0, 1.25), axis="-Y", seg=40, rseg=8))
    for k in range(4):
        a = math.radians(45 + 90 * k)
        P.add("Accent", L.rod((math.cos(a) * .2, 0, 1.25 + math.sin(a) * .2), (math.cos(a) * .32, 0, 1.25 + math.sin(a) * .32), .02))
        P.add("Eye", L.ball(.03, (math.cos(a) * .32, -.025, 1.25 + math.sin(a) * .32)))
    P.add("Eye", line([(0, -.06, .15), (0, -.045, .6), (0, -.04, 1.04)], r=.013))
    return P


def hunter(L):
    """The wraith: an inverted spearhead body balanced on a hover plate with a glowing rim, a needle head with a
    visor, two swept back-fins and long blade arms with light edges."""
    P = D.Parts()
    P.add("Body", lo([(.14, 0, 0, 2.0), (.4, .06, .05, 2.2), (.9, .14, .1, 2.4, 0, .01), (1.35, .24, .16, 2.6),
                      (1.7, .36, .2, 2.8, 0, -.02), (1.84, .3, .17, 2.4, 0, -.02), (1.93, .15, .11, 2.2)], caps=.03))
    P.add("Eye", band(1.35, .24, .16, n=2.6, h=.03, grow=.006))
    P.add("Eye", line([(0, -.205, 1.72), (0, -.17, 1.4), (0, -.11, .95), (0, -.06, .5)], r=.014))
    P.add("Accent", L.lathe([(0, .06), (.26, .06), (.33, .1), (.3, .16), (.12, .2), (0, .21)]))
    P.add("Eye", torus(.32, .025, (0, 0, .1), seg=40, rseg=8))
    P.add("Eye", L.lathe([(0, .0), (.08, .065), (0, .065)]))
    head = [(0, .09, .08, 2.4), (.2, .11, .09, 2.6), (.36, .07, .055, 2.2, 0, -.01), (.46, 0, 0, 2.0, 0, -.02)]
    P.add("Body", L.cyl(.07, .1, (0, -.02, 1.95)))
    P.add("Body", lo(head, axis="-Y", loc=(0, .08, 2.0), seg=24))
    P.add("Eye", band(.3, .085, .065, y=-.01, axis="-Y", loc=(0, .08, 2.0), h=.05, grow=.008, seg=24))
    for sx in (-1, 1):
        sh, el, tip = (sx * .38, 0, 1.73), (sx * .56, .08, 1.3), (sx * .44, -.82, 1.0)
        P.add("Accent", L.ball(.075, sh) + L.ball(.06, el))
        P.add("Body", limb(sh, el, [(-.5, .06, .06, 2.2), (.5, .045, .045, 2.2)], seg=16))
        P.add("Accent", limb(el, tip, [(-.5, .03, .07, 2.2), (-.25, .035, .1, 2.4), (.3, .025, .06, 2.2), (.5, .006, .01, 2.0)], seg=16))
        d = (Vector(tip) - Vector(el)).normalized()
        n = d.cross(Vector((1, 0, 0))).normalized()
        if n.z > 0:
            n = -n
        P.add("Eye", line([tuple(Vector(el).lerp(Vector(tip), t) + n * (.075 * (1 - t) + .01)) for t in (.1, .5, .9)], r=.012))
        fin = s.loft([(0, .02, .14, 2.2), (.25, .018, .11, 2.2, 0, .05), (.5, 0, 0, 2.0, 0, .14)], seg=12)
        P.add("Accent", [s.place(fin, (sx * .2, .16, 1.75), (math.radians(-35), sx * math.radians(25), 0))])
    return P


# =========================================================================== devices

def drone(L):
    """A four-pointed star wing (concave sides, points running out into the ducts), a raised spine with a glow
    band, the rotors in duct rings with glowing rims at the star's points."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    star = s.loft([(-.025, .5, .5, .9), (0, .55, .55, .9), (.025, .48, .48, .9)], seg=64)
    P.add("Body", [s.place(star, rot=(0, 0, math.pi / 4))])
    P.add("Eye", [s.place(s.loft([(-.008, .565, .565, .9), (.008, .565, .565, .9)], seg=64), rot=(0, 0, math.pi / 4))])
    P.add("Body", lo([(-.27, .14, .03, 2.2, 0, .01), (-.1, .16, .07, 2.4), (.12, .09, .06, 2.4), (.27, .02, .02, 2.0, 0, -.01)],
                     axis="-Y", loc=(0, 0, .02), seg=24))
    P.add("Eye", band(-.1, .16, .07, axis="-Y", loc=(0, 0, .02), h=.04, grow=.006, seg=24))
    for i, (x, y) in enumerate(rot):
        P.add("Body", torus(.2, .03, (x, y, .04), seg=20, rseg=6, zs=1.4))
        P.add("Eye", torus(.2, .018, (x, y, .08), seg=20, rseg=4))
        P.add("Body", L.cyl(.03, .045, (x, y, .025)))
        P.add(f"AccentRotor{i}", L.rotor((x, y, .065), .32))
    P.add("Accent", L.ball(.05, (0, -.17, -.06)))
    P.add("Accent", lo([(0, .05, .04, 2.4), (.08, .045, .035, 2.4)], axis="-Y", loc=(0, -.17, -.09), seg=16))
    P.add("Eye", lo([(0, .035, .025, 2.2), (.02, .035, .025, 2.2)], axis="-Y", loc=(0, -.245, -.09), seg=16))
    return P


def turret(L):
    """A hover plate with a glowing rim; a swept blade pylon (deep, narrow, leaning back like a fin) carries the
    glowing collar; the head is a long bullet pod with one barrel and two canted tail fins (it turns)."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.34, 0), (.36, .03), (.3, .08), (.12, .11), (0, .12)], seg=40))
    P.add("Eye", washer(.33, .37, .01, .035, seg=40))
    P.add("Body", lo([(.08, .1, .2, 2.2, 0, .02), (.35, .06, .17, 2.2, 0, .1), (.7, .05, .12, 2.2, 0, .1), (.98, .07, .08, 2.4, 0, .02),
                      (1.1, .1, .1, 2.4)], seg=28))
    P.add("Eye", line([(0, -.2, .12), (0, -.09, .45), (0, -.025, .8), (0, -.075, 1.0)], r=.02))
    P.add("Eye", band(1.06, .1, .1, n=2.4, h=.04, grow=.008))
    P.add("AccentHead", L.cyl(.09, .1, (0, 0, 1.14)))
    P.add("AccentHead", lo([(-.3, .12, .07, 2.4), (-.08, .2, .12, 2.6), (.18, .17, .1, 2.6), (.34, .08, .06, 2.2)],
                           axis="-Y", loc=(0, .05, 1.29), caps=.02, seg=28))
    P.add("AccentHead", L.cyl(.035, .38, (0, -.45, 1.28), axis="Y") + L.cyl(.05, .07, (0, -.62, 1.28), axis="Y"))
    for sx in (-1, 1):
        fin = s.loft([(0, .022, .14, 2.2), (.08, .018, .1, 2.2, 0, .06), (.14, 0, 0, 2.0, 0, .14)], seg=12)
        P.add("AccentHead", [s.place(fin, (sx * .08, .2, 1.32), (0, sx * math.radians(35), 0))])
    return P


def vacuum(L):
    """A lens disc whose room face (-z) carries a wide recessed light ring and a keel blade across it."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, -.03), (.3, -.03), (.36, .0), (.36, .05), (.3, .1), (0, .11)], seg=48))
    P.add("Accent", L.lathe([(0, 0), (.22, 0), (.25, .015), (0, .022)], loc=(0, 0, .1)))
    P.add("Accent", washer(.19, .29, -.042, -.028, seg=48))
    P.add("Eye", washer(.205, .275, -.05, -.038, seg=48))
    P.add("Body", [s.rbox((.24, .07, .04), (sx * .22, 0, -.035), r=.015, seg=2) for sx in (-1, 1)])
    P.add("Eye", L.lathe([(.07, 0), (.06, .015), (0, .022)], axis="-Z", loc=(0, 0, -.025)))
    return P


def lidar(L):
    """A slim flared post with a light band; the head is a short bullet drum with a slit."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.17, 0), (.17, .025), (.07, .08), (.04, .2), (.045, .395), (0, .395)], seg=32))
    P.add("Eye", band(.36, .045, .045, n=2.0, h=.03, grow=.01, seg=24))
    P.add("AccentHead", L.lathe([(0, .385), (.08, .385), (.1, .41), (.1, .48), (.07, .52), (0, .52)], seg=32))
    P.add("AccentHead", L.box((.08, .06, .04), (0, -.085, .45)))
    return P


def plate(L):
    """A floor plate with a glowing grid inlaid (a 3 x 3 light grid on dark glass)."""
    P = D.Parts()
    S, h = 1.1, .012
    P.add("Body", L.box((S - .02, S - .02, h), (0, 0, h / 2), r=.004))
    e = h + .004
    for k in (-1, 0, 1):
        P.add("Eye", L.box((S - .02, .03, e), (0, k * .36, e / 2), r=.003) + L.box((.03, S - .02, e), (k * .36, 0, e / 2), r=.003))
    for x in (-1, 1):
        P.add("Accent", L.box((.04, S, e + .002), (x * (S / 2 - .02), 0, (e + .002) / 2), r=.004)
              + L.box((S - .08, .04, e + .002), (0, x * (S / 2 - .02), (e + .002) / 2), r=.004))
    return P


def security_camera(L):
    """A bullet camera on a swept arm, a wrap visor instead of a round lens."""
    P = D.Parts()
    P.add("Body", L.box((.15, .06, .22), (0, .27, 0)))
    P.add("Accent", limb((0, .26, .02), (0, .1, -.02), [(-.5, .03, .03, 2.2), (.5, .022, .022, 2.2)], seg=12))
    P.add("Accent", lo([(-.17, .06, .05, 2.4), (0, .08, .07, 2.6), (.16, .07, .06, 2.4), (.22, .04, .035, 2.2)],
                       axis="-Y", loc=(0, .02, -.02), seg=24))
    P.add("Eye", band(.15, .07, .06, axis="-Y", loc=(0, .02, -.02), h=.04, grow=.008, seg=24))
    P.add("Eye", lo([(0, .035, .03, 2.2), (.02, .03, .025, 2.2)], axis="-Y", loc=(0, -.195, -.02), seg=16))
    return P


def laser_emitter(L):
    """A slim post with chamfered edges, a light strip and a swept cap."""
    P = D.Parts()
    P.add("Body", [s.rbox((.12, .12, 1.56), (0, 0, .78), r=.03, seg=2)])
    P.add("Body", [s.rbox((.14, .16, .06), (0, .01, 1.57), r=.02, seg=2, taper=(.7, .6))])
    P.add("Eye", L.box((.05, .04, 1.4), (0, -.062, .85), r=.008))
    P.add("Accent", [s.rbox((.16, .16, .05), (0, 0, .025), r=.015, seg=2)])
    return P


def pir(L):
    """A half bullet on a wall plate with a light band round its waist."""
    P = D.Parts()
    P.add("Body", [s.rbox((.16, .06, .16), (0, .04, 0), r=.02, seg=2)])
    P.add("Body", lo([(0, .07, .07, 2.2), (.035, .068, .068, 2.2), (.065, .04, .04, 2.2), (.075, 0, 0, 2.0)], axis="-Y", loc=(0, .015, 0), seg=24))
    P.add("Eye", band(.03, .068, .068, n=2.2, axis="-Y", loc=(0, .015, 0), h=.025, grow=.006, seg=24))
    P.add("Eye", L.ball(.022, (0, -.058, 0)))
    return P


def attach_mic(L):
    """A bullet on a thin stalk with a light band."""
    P = D.Parts()
    P.add("Accent", L.cyl(.045, .02, (0, 0, .01), seg=16) + limb((0, 0, .01), (0, 0, .27), [(-.5, .016, .016, 2.2), (.5, .012, .012, 2.2)], seg=10))
    P.add("Body", lo([(.25, .03, .03, 2.2), (.3, .065, .065, 2.4), (.37, .055, .055, 2.4), (.39, 0, 0, 2.0)], seg=16))
    P.add("Eye", band(.33, .062, .062, h=.03, grow=.006, seg=16))
    return P


# =========================================================================== furniture and level pieces

def extrude_x(pts, x0, x1):
    """A solid from a (y, z) profile extruded along x from x0 to x1 (side profiles of cabinets)."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in pts]
    b = [bm.verts.new((x1, y, z)) for y, z in pts]
    n = len(pts)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def vending_machine(L):
    """A dark monolith with a full-height light window, glowing edges up both front corners and a visor of light
    across its brow."""
    P = D.Parts()
    P.add("Body", [s.rbox((.9, .78, 1.84), (0, .01, .92), r=.03, seg=2)])
    P.add("Body", [s.rbox((.86, .74, .08), (0, .01, 1.86), r=.025, seg=2, taper=(.9, .85))])
    P.add("Eye", [s.rbox((.6, .02, 1.12), (-.1, -.39, 1.15), r=.01, seg=1)])
    for x in (-.425, .425):
        P.add("Eye", [s.rbox((.022, .022, 1.78), (x, -.375, .93), r=.006, seg=1)])
    P.add("Eye", [s.rbox((.82, .022, .04), (0, -.39, 1.79), r=.008, seg=1)])
    P.add("Accent", [s.rbox((.6, .03, .16), (-.1, -.39, .27), r=.012, seg=1), s.rbox((.14, .03, .5), (.32, -.39, 1.2), r=.012, seg=1)])
    return P


def arcade_machine(L):
    """A swept side profile like a racing fairing: the cabinet leans its marquee forward over a slanted screen, a
    control deck juts out; light lines trace the front profile on both sides."""
    P = D.Parts()
    prof = [(.38, 0), (.38, 1.8), (-.12, 1.8), (-.2, 1.64), (-.32, 1.12), (-.52, 1.0), (-.5, .9), (-.3, .84), (-.22, 0)]
    P.add("Body", [extrude_x(prof, -.33, .33)])
    for sx in (-1, 1):
        P.add("Accent", [extrude_x([(y + .005, z) for y, z in prof], *sorted((sx * .325, sx * .352)))])
        P.add("Eye", line([(sx * .345, -.205, 1.64), (sx * .345, -.325, 1.12), (sx * .345, -.525, 1.0)], r=.014))
    P.add("Eye", [s.rbox((.52, .025, .44), (0, -.27, 1.37), r=.01, seg=1, rot=(math.radians(-13), 0, 0))])
    P.add("Accent", [s.rbox((.6, .3, .04), (0, -.4, .965), r=.01, seg=1, rot=(math.radians(-10), 0, 0))])
    P.add("Eye", [s.rbox((.56, .03, .1), (0, -.15, 1.7), r=.01, seg=1)])
    return P


def server_rack(L):
    """A dark tower whose door is a field of light: three vertical light columns crossed by unit ticks."""
    P = D.Parts()
    P.add("Body", [s.rbox((.7, .96, 1.94), (0, .02, .97), r=.03, seg=2)])
    P.add("Body", [s.rbox((.66, .03, 1.86), (0, -.47, .99), r=.012, seg=1)])
    for x in (-.2, 0, .2):
        P.add("Eye", [s.rbox((.03, .02, 1.64), (x, -.49, 1.0), r=.006, seg=1)])
    for i in range(11):
        P.add("Eye", [s.rbox((.56, .015, .018), (0, -.49, .2 + i * .16), r=.004, seg=1)])
    P.add("Body", [s.rbox((.68, .9, .06), (0, .02, 1.96), r=.02, seg=1, taper=(.9, .9))])
    return P


def plant(L):
    """A data fern: a square tapered pot, a stem and flat fronds spiralling up and out."""
    P = D.Parts()
    P.add("Accent", L.pot(.21, .42, (0, 0, 0)))
    P.add("Body", limb((0, 0, .38), (0, 0, 1.4), [(-.5, .025, .025, 2.2), (.5, .006, .006, 2.0)], seg=10))
    fronds = [(0, .5, .33, .12), (90, .55, .35, .12), (180, .6, .33, .12), (270, .65, .35, .12), (45, .8, .27, .16),
              (135, .85, .27, .16), (225, .9, .27, .16), (315, .95, .27, .16), (20, 1.1, .19, .2), (200, 1.15, .19, .2)]
    for ang, z, ln, rise in fronds:
        a = math.radians(ang)
        tip = (math.cos(a) * ln, math.sin(a) * ln, z + rise)
        P.add("Body", limb((math.cos(a) * .01, math.sin(a) * .01, z), tip,
                           [(-.5, .012, .015, 2.0), (-.1, .06, .012, 2.2), (.5, .004, .004, 2.0)], seg=8))
    return P


def floor_lamp(L):
    """A swept fin pylon on a dark plate; at the top a halo shade over a light disc."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, 0), (.18, 0), (.19, .015), (.16, .03), (0, .035)], seg=32))
    P.add("Body", lo([(.03, .02, .08, 2.2, 0, .02), (.6, .015, .05, 2.2, 0, .05), (1.2, .015, .04, 2.2, 0, .02), (1.36, .02, .03, 2.2)], seg=12))
    P.add("Accent", L.lathe([(.06, 1.3), (.19, 1.37), (.2, 1.39), (.15, 1.6), (.12, 1.65), (.1, 1.64), (.13, 1.58), (.175, 1.39),
                             (0, 1.38)], seg=32))
    P.add("Eye", L.lathe([(0, 1.35), (.14, 1.35), (.14, 1.37), (0, 1.37)], seg=32))
    return P


def desk_lamp(L):
    """A thin arc rising from a chamfered base, ending in a bullet head over the bulb."""
    P = D.Parts()
    P.add("Body", [s.rbox((.15, .15, .025), (0, 0, .0125), r=.008, seg=1)])
    P.add("Body", [s.tube([(0, .03, .02), (0, .04, .25), (0, -.03, .35), (0, -.12, .33)], .012, res=1)])
    P.add("Accent", lo([(-.06, .05, .045, 2.4), (.02, .07, .055, 2.6), (.07, .05, .04, 2.2)], axis="-Y", loc=(0, -.15, .31), seg=20))
    P.add("Eye", L.lathe([(0, .25), (.03, .255), (.03, .285), (0, .29)], seg=12, loc=(0, -.16, 0)))
    return P


def reception_desk(L):
    """A counter of three canted dark panels with a light band running across them; a work top behind and a
    monitor."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.4, .08, 1.11), (0, -.38, .555), r=.014, seg=1)])
    for sx in (-1, 1):
        P.add("Body", [s.rbox((.56, .08, 1.11), (sx * .94, -.33, .555), r=.014, seg=1, rot=(0, 0, sx * math.radians(12)))])
        P.add("Body", [s.rbox((.08, .62, 1.11), (sx * 1.18, .06, .555), r=.014, seg=1)])
        P.add("Eye", [s.rbox((.54, .02, .035), (sx * .94, -.375, .9), r=.006, seg=1, rot=(0, 0, sx * math.radians(12)))])
    P.add("Eye", [s.rbox((1.38, .02, .035), (0, -.425, .9), r=.006, seg=1)])
    P.add("Accent", [s.rbox((2.46, .3, .04), (0, -.33, 1.12), r=.012, seg=1), s.rbox((2.3, .66, .04), (0, .05, .74), r=.012, seg=1)])
    acc, glow = L.screen((.5, .32), (.4, .17, .96), "-Y")
    P.add("Eye", glow + acc + L.slab((.06, .05, .2), (.4, .21, .83)))
    return P


def safe(L):
    """A dark block whose round door is ringed with light; a spoked dial in its middle."""
    P = D.Parts()
    P.add("Body", [s.rbox((.8, .7, .96), (0, 0, .5), r=.03, seg=2)])
    P.add("Body", L.lathe([(.32, 0), (.33, .02), (.3, .05), (0, .05)], seg=40, axis="-Y", loc=(0, -.34, .5)))
    P.add("Eye", torus(.31, .012, (0, -.385, .5), axis="-Y", seg=40, rseg=6))
    P.add("Accent", L.cyl(.08, .04, (0, -.4, .5), axis="Y"))
    for k in range(3):
        a = k * math.tau / 3
        P.add("Accent", [s.rbox((.025, .03, .16), (math.cos(a) * .1, -.4, .5 + math.sin(a) * .1), r=.008, seg=1, rot=(0, a + math.pi / 2, 0))])
    P.add("Eye", [s.rbox((.06, .02, .06), (-.24, -.355, .82), r=.008, seg=1)])
    return P


def water_cooler(L):
    """A chamfered dark column, the bottle a faceted capsule."""
    P = D.Parts()
    P.add("Body", [s.rbox((.34, .34, .98), (0, 0, .49), r=.025, seg=2)])
    P.add("Body", [s.rbox((.16, .06, .1), (0, -.18, .62), r=.012, seg=1)])
    P.add("Eye", lo([(.97, .1, .1, 2.0), (1.0, .14, .14, 2.0), (1.32, .15, .15, 2.0), (1.42, .08, .08, 2.0), (1.445, .04, .04, 2.0)], seg=8))
    return P


OWN_LIGHT = {"vending_machine", "arcade_machine", "server_rack", "plant", "floor_lamp", "desk_lamp", "reception_desk",
             "safe", "water_cooler", "ceiling_lamp", "exit_sign", "hatch", "terminal", "trash_bin", "round_table", "bean_bag",
             "chair", "sofa", "armchair", "whiteboard", "coat_rack", "coffee_machine"}


def trash_bin(L):
    """A tapered square bin with chamfered corners and a light rim."""
    P = D.Parts()
    P.add("Body", lo([(0, .15, .15, 3.4), (.4, .185, .185, 3.4), (.42, .19, .19, 3.4)], seg=24))
    P.add("Accent", [revolve([(.17, .405), (.195, .405), (.195, .445), (.17, .445)], seg=24)])
    return P


def round_table(L):
    """A chamfered disc with a lit rim on a cross of two fins."""
    P = D.Parts()
    P.add("Body", L.lathe([(0, .71), (.47, .71), (.5, .735), (.49, .76), (0, .76)], seg=40))
    P.add("Accent", washer(.475, .502, .718, .74, seg=40))
    for a in (0, math.pi / 2):
        fin = s.rbox((.6, .04, .7), (0, 0, .36), r=.012, seg=1, taper=(.25, 1.0))
        P.add("Accent", [s.place(fin, rot=(0, 0, a))])
    return P


def bean_bag(L):
    """A lounge pod: a low chamfered pad rising into a swept backrest."""
    P = D.Parts()
    P.add("Body", lo([(0, .4, .4, 3.0), (.2, .45, .45, 3.0), (.32, .42, .42, 3.0)], seg=32, caps=.05))
    back = s.loft([(0, .38, .09, 3.0), (.34, .34, .075, 3.0), (.42, .28, .055, 3.0)], seg=24, round_caps=.04)
    P.add("Body", [s.place(back, (0, .26, .27), (math.radians(-16), 0, 0))])
    return P


def extrude_y(pts, y0, y1):
    """A solid from an (x, z) outline extruded along y."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in pts]
    b = [bm.verts.new((x, y1, z)) for x, z in pts]
    n = len(pts)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def fin_panel(x, y0, y1, z0, z1, t=.04, sweep=.08):
    """A waterfall fin: a dark panel in the y-z plane, narrower at the floor (swept in at the bottom)."""
    return [extrude_x([(y0 + sweep, z0), (y1 - sweep, z0), (y1, z1), (y0, z1)], x - t / 2, x + t / 2)]


def chair(L):
    """A swept shell chair: seat and back one continuous blade on a single fin pedestal and a dark foot plate."""
    P = D.Parts()
    shell = [(-.26, .44), (.2, .44), (.24, .5), (.3, 1.06), (.25, 1.07), (.18, .56), (-.22, .52), (-.27, .48)]
    P.add("Body", [extrude_x(shell, -.28, .28)])
    P.add("Accent", fin_panel(0, -.18, .18, .02, .46, t=.05, sweep=.1) + [s.rbox((.5, .5, .02), (0, 0, .01), r=.01, seg=1)])
    return P


def sofa(L):
    """A long low lounge: a continuous swept back over a floating seat slab, both on a dark plinth."""
    P = D.Parts()
    prof = [(-.42, .22), (.2, .22), (.3, .3), (.43, .88), (.33, .88), (.22, .5), (-.4, .46), (-.43, .4)]
    P.add("Body", [extrude_x(prof, -.98, .98)])
    for x in (-1.0, 1.0):
        P.add("Accent", [s.rbox((.2, .84, .62), (x, 0, .32), r=.015, seg=1, taper=(1, .8))])
    P.add("Accent", [s.rbox((1.9, .6, .2), (0, .0, .11), r=.012, seg=1)])
    return P


def armchair(L):
    """A swept lounge chair: one blade from front lip to back top, slab arms, a dark plinth."""
    P = D.Parts()
    prof = [(-.36, .22), (.16, .22), (.26, .3), (.38, .86), (.28, .86), (.18, .5), (-.34, .46), (-.37, .4)]
    P.add("Body", [extrude_x(prof, -.3, .3)])
    for x in (-.37, .37):
        P.add("Accent", [s.rbox((.14, .74, .6), (x, 0, .31), r=.015, seg=1, taper=(1, .8))])
    P.add("Accent", [s.rbox((.62, .56, .2), (0, 0, .11), r=.012, seg=1)])
    return P


def whiteboard(L):
    """A floating screen panel held by one swept fin on a dark foot."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.8, .04, 1.0), (0, 0, 1.5), r=.012, seg=1)])
    P.add("Accent", [extrude_y([(-.25, .03), (.25, .03), (.08, 1.1), (-.08, 1.1)], -.025, .025)]
          + [s.rbox((.9, .08, .03), (0, 0, .015), r=.01, seg=1)])
    for z in (1.0, 2.0):
        P.add("Accent", [s.rbox((1.84, .06, .02), (0, 0, z), r=.006, seg=1)])
    return P


def coat_rack(L):
    """A fin tree: a tall swept blade on a dark plate, three blade hooks, two crisp coats."""
    P = D.Parts()
    P.add("Accent", [s.rbox((.48, .48, .02), (0, 0, .01), r=.01, seg=1)] + lo([(.01, .02, .09, 2.2), (1.2, .015, .05, 2.2), (1.8, .012, .03, 2.2)], seg=12))
    for a in (0.0, 2.1, 4.2):
        P.add("Accent", limb((0, 0, 1.64), (math.cos(a) * .22, math.sin(a) * .22, 1.76), [(-.5, .012, .03, 2.0), (.5, .006, .012, 2.0)], seg=8))
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.36)) + L.cushion((.3, .12, .55), (-.08, .1, 1.38), rot=(0, 0, 2.1)))
    return P


def ceiling_lamp(L):
    """A stadium-shaped ring of light hanging under a thin dark plate."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.24, .66, .03), (0, 0, -.015), r=.01, seg=1)])
    P.add("Accent", [s.loft([(-.06, .6, .3, 2.8), (-.025, .6, .3, 2.8)], seg=48)])
    P.add("Eye", [s.loft([(-.082, .62, .32, 2.8), (-.055, .62, .32, 2.8)], seg=48)])
    P.add("Accent", [s.loft([(-.085, .5, .2, 2.8), (-.04, .5, .2, 2.8)], seg=48)])
    return P


def hatch(L):
    """A dark hexagonal lid with blade grips, a wide light ring round it."""
    P = D.Parts()
    P.add("Body", lo([(0, .8, .8, 2.0), (.05, .78, .78, 2.0), (.065, .72, .72, 2.0)], seg=6))
    for y in (-.25, 0, .25):
        P.add("Accent", [s.rbox((1.0, .06, .03), (0, y, .07), r=.01, seg=1)])
    P.add("Eye", washer(.8, .9, 0, .02, seg=48))
    return P


def coffee_machine(L):
    """A sleek bullet leaning back on a dark plate: a blade spout and a light visor for a display."""
    P = D.Parts()
    P.add("Body", lo([(0, .16, .17, 3.0), (.3, .15, .16, 2.8, 0, .02), (.48, .12, .12, 2.4, 0, .04), (.5, 0, 0, 2.0, 0, .05)], seg=24))
    P.add("Accent", [s.rbox((.24, .16, .03), (0, -.17, .015), r=.01, seg=1), s.rbox((.04, .12, .03), (0, -.18, .2), r=.008, seg=1)])
    P.add("Eye", band(.4, .13, .14, n=2.6, y=.03, h=.04, grow=.008, seg=24))
    return P


def meeting_table(L):
    """A long dark top lit along its edge on two swept waterfall fins."""
    P = D.Parts()
    P.add("Body", L.slab((3.2, 1.3, .06), (0, 0, .75)))
    for x in (-1.1, 1.1):
        P.add("Accent", fin_panel(x, -.55, .55, 0, .73, t=.06, sweep=.25))
    return P


def printer(L):
    """A copier as a wedge with a slanted scanner lid; the panel lights on the lid's front."""
    P = D.Parts()
    P.add("Body", L.carcass((.6, .6, .95), (0, 0, 0), kick=.04))
    P.add("Body", [extrude_x([(-.24, .94), (.28, .94), (.28, 1.07), (-.12, 1.07), (-.24, 1.0)], -.28, .28)])
    P.add("Accent", L.slab((.36, .3, .03), (-.1, -.38, .82), rot=(math.radians(8), 0, 0)) + L.door((.5, .3), (0, -.31, .3)))
    P.add("Eye", [s.rbox((.16, .03, .06), (.15, -.235, 1.0), r=.008, seg=1, rot=(math.radians(-27), 0, 0))])
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_plate": plate, "security_camera": security_camera, "laser_emitter": laser_emitter,
    "sensor_pir": pir, "attach_mic": attach_mic,
    "vending_machine": vending_machine,
    "arcade_machine": arcade_machine,
    "server_rack": server_rack,
    "plant": plant,
    "floor_lamp": floor_lamp,
    "desk_lamp": desk_lamp,
    "reception_desk": reception_desk,
    "safe": safe,
    "water_cooler": water_cooler,
    "trash_bin": trash_bin,
    "round_table": round_table,
    "bean_bag": bean_bag,
    "chair": chair,
    "sofa": sofa,
    "armchair": armchair,
    "whiteboard": whiteboard,
    "coat_rack": coat_rack,
    "ceiling_lamp": ceiling_lamp,
    "hatch": hatch,
    "coffee_machine": coffee_machine,
    "meeting_table": meeting_table,
    "printer": printer,
}
