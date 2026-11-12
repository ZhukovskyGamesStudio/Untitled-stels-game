"""S11 Noir — «Нуар-детектив»: 1940s streamline moderne, low-poly with hard edges (Lofi family, PS1-like).

Every robot is a piece of the 1940s city at night: a private eye in a trench coat and fedora, a chrome teardrop
streamliner with a tail fin, a hood-ornament greyhound, a fedora scuttling on eight legs, a cathedral radio with
ribbon microphones, a streamlined delivery van, a canister vacuum cleaner on chrome runners, a bullet-nosed
locomotive, a Hollywood searchlight behind venetian slats, a chrome teardrop drone with fan cowls, a finned bullet
on a tripod, a slatted ceiling fixture. Language: teardrops, speed-line ribs (Accent), tail fins, fedora brims,
bakelite slabs, venetian slats; few segments, flat shading.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import shapes as s
import props
from lang import Lang, align
import designs as D

SEG = 10   # round things: low-poly


class Parts(D.Parts):
    """D.Parts that also takes a bare bmesh or nested lists of them."""

    def add(self, role, bms):
        flat = []
        stack = [bms]
        while stack:
            b = stack.pop()
            if isinstance(b, (list, tuple)):
                stack.extend(reversed(b))
            elif b is not None:
                flat.append(b)
        return super().add(role, flat)


def lo(rings, axis="Z", loc=(0, 0, 0), seg=SEG):
    return s.loft(rings, seg=seg, axis=axis, loc=loc)


def la(profile, axis="Z", loc=(0, 0, 0), seg=SEG):
    return s.lathe(profile, seg=seg, axis=axis, loc=loc)


def prism(pts, y0, y1, bevel=.012):
    """A slab: the polygon pts [(x, z), ...] in the XZ plane extruded from y0 to y1, edges chamfered."""
    bm = bmesh.new()
    a = [bm.verts.new((x, y0, z)) for x, z in pts]
    b = [bm.verts.new((x, y1, z)) for x, z in pts]
    n = len(pts)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], b[i], b[j], a[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel:
        s.bevel_edges(bm, bevel, seg=1, angle=25)
    return bm


def arch(w, z0, z1, n=8):
    """Outline of a cathedral arch (flat sides, round top) of width w from z0 to z1."""
    r = w / 2
    zc = z1 - r
    pts = [(-r, z0), (r, z0)]
    for k in range(n + 1):
        a = k / n * math.pi
        pts.append((math.cos(a) * r, zc + math.sin(a) * r))
    return pts


class Style(Lang):
    """Noir detective, built from voxels (in the spirit of Shadows of Doubt): the shapes below, rebuilt from 4 cm cubes."""
    name = "S11_Noir"
    voxel = 0.04
    seg = SEG
    soft = 0.0
    edge = 0.012
    flat = True
    sharp = 30.0
    hull_kind = "rounded"
    eye_kind = "slit"
    limb_kind = "tube"

    # ------------------------------------------------------------------ streamline vocabulary

    def tear(self, length, r, loc, axis="-Y", k=.32, squash=1.0):
        """A teardrop along axis: a round nose at the front, the widest point at k of the length, a long point behind."""
        L = length
        rings = [(0, 0, 0), (L * .05, r * .55, r * .55 * squash), (L * k * .5, r * .9, r * .9 * squash), (L * k, r, r * squash),
                 (L * .6, r * .78, r * .78 * squash), (L * .85, r * .38, r * .38 * squash), (L, 0, 0)]
        return [lo(rings, axis=axis, loc=loc)]

    def tear_between(self, nose, tail, r, k=.3, squash=1.0):
        """A teardrop from its round nose to its pointed tail (any direction); squash flattens it along world z."""
        nose, tail = Vector(nose), Vector(tail)
        L = (tail - nose).length
        rings = [(-L / 2, 0, 0), (-L / 2 + L * .05, r * .55, r * .55), (-L / 2 + L * k * .5, r * .9, r * .9), (-L / 2 + L * k, r, r),
                 (-L / 2 + L * .6, r * .78, r * .78), (-L / 2 + L * .85, r * .38, r * .38), (L / 2, 0, 0)]
        bm = lo(rings, seg=12)
        if squash != 1.0:
            bmesh.ops.scale(bm, vec=(1, squash, 1), verts=bm.verts)
        return [align(bm, nose, tail)]

    def fedora(self, loc, r=.13, h=.15, brim=2.0):
        """(felt, band): a fedora — a pinched crown with a dent, a wide brim snapped down at the front."""
        x, y, z = loc
        crown = lo([(0, r, r * 1.08, 2.2), (h * .6, r * .96, r * 1.02, 2.2), (h * .9, r * .8, r * .82, 2.2), (h, r * .42, r * .6, 2.0)],
                   loc=loc)
        brim_r = r * brim
        b = s.lathe([(r * .9, -.008), (brim_r, -.012), (brim_r * 1.02, .006), (brim_r * .96, .014), (r * .9, .012)], seg=14, loc=(0, 0, 0))
        for v in b.verts:  # snap the brim down at the front, up at the back
            t = max(0.0, (Vector((v.co.x, v.co.y, 0)).length - r) / (brim_r - r))
            v.co.z += (-.06 if v.co.y < 0 else .03) * t * abs(v.co.y) / max(1e-6, Vector((v.co.x, v.co.y, 0)).length)
        s.place(b, (x, y, z))
        band = lo([(.008, r * 1.02, r * 1.1, 2.2), (.04, r * 1.0, r * 1.08, 2.2)], loc=loc)
        return [crown, b], [band]

    def fins(self, base, along, up, n=3, length=.4, gap=.05, t=.025, h=.03):
        """Speed-line ribs: n parallel bars from base along `along`, stacked along `up`."""
        base, along, up = Vector(base), Vector(along).normalized(), Vector(up).normalized()
        out = []
        for k in range(n):
            p = base + up * gap * k
            L = length * (1 - .18 * k)
            out.append(align(s.rbox((h, t, L), r=.006, seg=1), p, p + along * L))
        return out

    def tailfin(self, root0, root1, tip, t=.03):
        """A swept fin: a flat triangle-ish slab through root0, root1 (on the body) and the tip."""
        a, b, c = Vector(root0), Vector(root1), Vector(tip)
        bm = bmesh.new()
        n = (b - a).cross(c - a).normalized() * t / 2
        vs = [bm.verts.new(p + n) for p in (a, b, c)] + [bm.verts.new(p - n) for p in (a, b, c)]
        bm.faces.new(vs[:3])
        bm.faces.new(list(reversed(vs[3:])))
        for i in range(3):
            j = (i + 1) % 3
            bm.faces.new((vs[i], vs[j], vs[j + 3], vs[i + 3]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return [bm]

    def slit(self, w, loc, h=.03, rot=(0, 0, 0)):
        return [s.rbox((w, .03, h), loc, r=.006, seg=1, rot=rot)]

    def wheel(self, center, radius, width):
        """A whitewall wheel: a fat low-poly tyre, a white ring and a chrome hubcap dome (symmetric)."""
        c = Vector(center)
        out = [s.lathe([(radius * .55, -width / 2), (radius * .9, -width / 2), (radius, -width * .3), (radius, width * .3),
                        (radius * .9, width / 2), (radius * .55, width / 2)], seg=12, axis="X", loc=c)]
        out.append(s.rcyl(radius * .6, width * 1.04, c, axis="X", seg=12, r=0))
        out.append(s.lathe([(0, -width * .62), (radius * .35, -width * .55), (radius * .4, -width * .45), (radius * .4, width * .45),
                            (radius * .35, width * .55), (0, width * .62)], seg=10, axis="X", loc=c))
        return out

    def track(self, size, loc):
        """A crawler track along Y (Lang.track stands the belt upright): a stadium belt and road wheels."""
        w, d, h = size
        x, y, z = loc
        r = h / 2
        out = [s.loft([(-w / 2, r, d / 2, 6.0), (w / 2, r, d / 2, 6.0)], seg=16, axis="X", loc=loc)]
        for k in range(4):
            out.append(s.rcyl(r * .62, w * 1.12, (x, y - d / 2 + r + (d - 2 * r) * k / 3, z), axis="X", seg=8, r=0))
        return out

    # ------------------------------------------------------------------ furniture vocabulary: streamline moderne
    leg_kind = "round"
    edge_kind = "soft"
    cushion_puff = .5
    handle_kind = "bar"
    foliage_kind = "spiky"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """Boards are racetracks — fully rounded ends — with a chrome band round the edge; panels have radiused corners."""
        w, d, h = size
        x, y, z = loc
        if h <= .08 and min(w, d) > .3 and not any(rot):
            r = min(w, d) * .45
            out = [s.rbox((w, d, h), loc, r=r, seg=2, vertical_only=True)]
            out.append(s.rbox((w + .012, d + .012, h * .35), (x, y, z), r=r + .006, seg=2, vertical_only=True))
            return out
        return [s.rbox(size, loc, r=min(min(size) * .45, .04), seg=1, rot=rot)]

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A chrome tube with a flared foot."""
        x, y, z = top
        fz = max(0.0, z - length)
        return [s.rcyl(thick * .4, length - .01, (x + splay[0], y + splay[1], fz + .01 + (length - .01) / 2), seg=8, r=0),
                s.rcyl(thick * .7, .02, (x + splay[0], y + splay[1], fz + .01), seg=8, r=.004, radius2=thick * .45)]

    def carcass(self, size, loc, kick=0.06):
        """A bakelite cabinet with radiused corners, three chrome speed lines down each side and a recessed plinth."""
        w, d, h = size
        x, y, z = loc
        out = [s.rbox((w, d, h - kick), (x, y, z + kick + (h - kick) / 2), r=min(.08, w * .2, d * .2), seg=2, vertical_only=True)]
        for k in range(3):
            zz = z + kick + (h - kick) * (.3 + k * .06)
            out.append(s.rbox((w + .012, d * .7, .018), (x, y + d * .1, zz), r=.006, seg=1))
        if kick > 0:
            out.append(s.rbox((w - .05, d - .06, kick + .01), (x, y + .01, z + kick / 2 + .005), r=.01, seg=1))
        return out

    def door(self, size, loc, face="-Y"):
        """A flush panel with three chrome speed lines across its lower part."""
        w, h = size
        x, y, z = loc
        out = [s.rbox((w - .012, .022, h - .012), (x, y, z), r=.008, seg=1)]
        for k in range(3):
            out.append(s.rbox((w * (.7 - k * .15), .014, .012), (x - w * .1 + w * k * .075, y - .013, z - h * .3 + k * .03), r=.003, seg=1))
        return out

    def handle(self, loc, length=0.12, vertical=True):
        """A long chrome bar on two standoffs."""
        x, y, z = loc
        L = max(length, .16)
        size = (.02, .02, L) if vertical else (L, .02, .02)
        out = [s.rbox(size, (x, y - .025, z), r=.008, seg=1)]
        for sx in (-1, 1):
            out.append(s.rbox((.014, .04, .014), (x + (0 if vertical else sx * L * .4), y - .006, z + (sx * L * .4 if vertical else 0)), r=.004, seg=1))
        return out

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """A low-poly channel-tufted cushion."""
        w, d, h = size
        bm = s.loft([(-h / 2, w / 2, d / 2, 3.0), (0, w / 2 * 1.03, d / 2 * 1.03, 3.0), (h / 2, w / 2, d / 2, 3.0)], seg=12,
                    round_caps=min(h, w, d) * .3)
        return [s.place(bm, loc, rot)]

    def screen(self, size, loc, face="-Y"):
        """A rounded CRT: a radiused bakelite bezel and a slightly domed picture."""
        w, h = size
        x, y, z = loc
        acc = [s.rbox((w + .04, .06, h + .04), (x, y + .01, z), r=min(w, h) * .18, seg=2, vertical_only=False)]
        glow = [s.rbox((w - .04, .02, h - .04), (x, y - .022, z), r=min(w, h) * .14, seg=1)]
        return acc, glow

    def shade(self, r_bottom, r_top, height, loc):
        """A stepped chrome bowl shade, a shell open below (pole to pole)."""
        h = height
        prof = [(0, h + .004), (r_top, h), (r_top, h * .6), (r_bottom * .9, h * .55), (r_bottom, .01), (r_bottom - .008, .006),
                (r_bottom * .9 - .008, h * .55), (r_top - .008, h * .6), (0, h - .008)]
        return [s.lathe(prof, seg=10, loc=loc)]

    def pot(self, r, h, loc):
        """A stepped chrome-banded planter."""
        x, y, z = loc
        out = [s.lathe([(0, 0), (r * .8, 0), (r * .8, h * .15), (r * .9, h * .15), (r * .9, h * .5), (r, h * .5), (r, h), (0, h)], seg=10, loc=loc)]
        out.append(s.rcyl(r * .92, .03, (x, y, z + h * .35), seg=10, r=0))
        return out

    def foliage(self, centre, r, n=5, seed=1):
        """Sansevieria: tall pointed blades fanning upward."""
        cx, cy, cz = centre
        out = []
        for i in range(max(5, n + 2)):
            a = (i * 2.399 + seed) % math.tau
            base = Vector((cx + math.cos(a) * r * .15, cy + math.sin(a) * r * .15, cz - r * .6))
            tip = base + Vector((math.cos(a) * r * .5, math.sin(a) * r * .5, r * (1.6 + .4 * (i % 3) / 2)))
            n_ = (tip - base).length
            blade = s.loft([(-n_ / 2, .045, .01, 2.0), (-n_ * .1, .055, .012, 2.0), (n_ / 2, 0, 0)], seg=6)
            out.append(align(blade, base, tip))
        return out

    def signature(self, model, P):
        if model == "laser_emitter":  # a chrome teardrop cap and speed ribs on the post
            P.add("Body", self.tear(.2, .08, (0, 0, 1.56), axis="Z", k=.5))
            P.add("Body", self.fins((-.061, .04, .3), (0, 0, 1), (0, -1, 0), n=2, length=1.0, gap=.04, t=.012, h=.012))
        if model == "sensor_plate":  # venetian slats across the plate
            for k in range(-4, 5):
                P.add("Accent", [s.rbox((.9, .03, .006), (0, k * .11, .015), r=.002, seg=1)])
        if model == "attach_mic":  # a ribbon-mic grille cage
            P.add("Body", [s.rbox((.14, .03, .1), (0, 0, .32), r=.01, seg=1)])
        return P


# =========================================================================== walkers

def guard(L):
    """The private eye: a belted trench coat with padded shoulders, an upturned collar hiding a chrome face, one
    glowing slit under a snap-brim fedora, gloved fists, trousers with a crease and wingtip shoes."""
    P = Parts()
    P.add("Body", lo([(.6, .33, .26, 2.6), (.8, .29, .22, 2.6), (1.0, .23, .17, 2.6), (1.06, .22, .165, 2.6), (1.28, .27, .18, 2.8),
                      (1.48, .34, .2, 3.2), (1.58, .35, .2, 3.2), (1.64, .2, .14, 2.4)]))                    # trench coat
    P.add("Accent", lo([(.98, .235, .18, 2.6), (1.05, .235, .18, 2.6)]))                                      # belt
    P.add("Accent", [s.rbox((.08, .03, .07), (0, -.185, 1.015), r=.008, seg=1)])
    P.add("Body", [s.rbox((.3, .04, .3), (0, -.165, 1.36), r=.01, seg=1, rot=(math.radians(-6), 0, 0), taper=(1.5, 1))])  # lapels
    for z in (.8, 1.15, 1.25):
        y = -.19 if z > 1 else -.235
        P.add("Accent", [s.ball(.025, (.08, y, z), seg=6), s.ball(.025, (-.08, y, z), seg=6)])
    P.add("Body", lo([(1.6, .17, .14, 2.2), (1.72, .19, .16, 2.2), (1.84, .2, .17, 2.2)], loc=(0, -.02, 0)))   # upturned collar
    P.add("Body", s.mirror_x(lo([(0, .08, .1, 2.0), (.1, .1, .12, 2.0), (.16, .0, .0)], loc=(.0, -.04, 1.7)), keep=False))
    P.add("Body", [lo([(1.7, .0, .0), (1.72, .1, .11, 2.0, 0, -.05), (1.84, .12, .13, 2.0, 0, -.05), (1.94, .1, .11, 2.0, 0, -.05),
                       (1.97, .0, .0, 2.0, 0, -.05)])])                                                     # chrome head
    P.add("Eye", L.slit(.17, (0, -.175, 1.86), h=.03))
    felt, band = L.fedora((0, -.05, 1.93), r=.13, h=.15, brim=2.0)
    P.add("Body", felt).add("Accent", band)
    for sx in (-1, 1):
        P.add("Body", [lo([(0, .1, .09, 2.2), (.25, .085, .085, 2.2), (.58, .07, .075, 2.2)], axis="-Z", loc=(sx * .36, -.01, 1.56))])  # sleeve
        P.add("Accent", [s.rbox((.12, .14, .13), (sx * .37, -.03, .93), r=.03, seg=1)])                   # gloved fist
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, [s.ball(.08, (x, 0, .9), seg=8)])
        P.add(leg, [lo([(D.GUARD_KNEE - .02, .075, .075, 2.0), (.92, .09, .09, 2.0)], loc=(x, 0, 0), seg=8)])
        P.add(leg, [lo([(.06, .075, .08, 2.0), (D.GUARD_KNEE + .02, .075, .075, 2.0)], loc=(x, 0, 0), seg=8)])
        P.add(leg, [lo([(0, .0, .0), (.02, .06, .03, 2.0), (.12, .07, .05, 2.4), (.28, .065, .05, 2.4), (.32, .0, .0)], axis="-Y",
                       loc=(x, .08, .04), seg=8)])                                                           # wingtip shoe
        P.add(leg, [s.rbox((.15, .3, .05), (x, -.06, .025), r=.012, seg=1)])
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """The streamliner: a huge chrome teardrop torso leaning forward like the cowl of a speed train, gliding on a low
    teardrop sled, a fedora brim pulled down over the wrap-around visor, a long swept tail fin, speed ribs along the
    flanks, long arms ending in teardrop blades."""
    P = Parts()
    P.add("Body", L.tear(1.15, .22, (0, -.5, .2), axis="Y", k=.3, squash=.55))                             # glide sled
    P.add("Eye", [s.rbox((.3, .8, .03), (0, .02, .1), r=.006, seg=1, taper=(.6, 1))])                     # hover glow
    P.add("Body", [lo([(.2, .1, .3, 2.0, 0, .05), (.7, .08, .24, 2.0, 0, .12), (1.3, .12, .3, 2.0, 0, .2)], loc=(0, .05, 0))])  # keel
    nose, tail = Vector((0, -.38, 1.9)), Vector((0, .62, 1.0))
    P.add("Body", L.tear_between(nose, tail, .32, k=.28))                                                 # torso cowl
    P.add("Eye", [s.rbox((.4, .06, .055), (0, -.33, 1.86), r=.01, seg=1)])
    P.add("Accent", [s.rbox((.48, .06, .12), (0, -.31, 1.86), r=.015, seg=1, taper=(.9, 1))])
    d = (tail - nose).normalized()
    for sx in (-1, 1):
        for k in range(3):
            p0 = nose + d * (.2 + k * .04) + Vector((sx * (.3 - k * .02), 0, -.08 - k * .07))
            P.add("Accent", [align(s.rbox((.035, .03, .55 - k * .1), r=.006, seg=1), p0, p0 + d * (.55 - k * .1))])
    P.add("Body", L.tailfin((0, -.05, 2.0), (0, .45, 1.3), (0, .85, 2.0), t=.05))
    felt, band = L.fedora((0, 0, 0), r=.17, h=.2, brim=2.3)
    for bm in felt + band:
        s.place(bm, (0, -.22, 2.02), rot=(math.radians(-14), 0, 0))
    P.add("Body", felt).add("Accent", band)
    for sx in (-1, 1):
        sh = Vector((sx * .34, -.05, 1.62))
        P.add("Accent", [s.ball(.08, sh, seg=8)])
        P.add("Body", [align(s.rcyl(.05, .45, seg=8, r=0), sh, Vector((sx * .46, -.15, 1.25)))])
        P.add("Accent", [s.ball(.06, (sx * .46, -.15, 1.25), seg=8)])
        P.add("Body", [align(s.rcyl(.045, .45, seg=8, r=0), Vector((sx * .46, -.15, 1.25)), Vector((sx * .42, -.48, 1.0)))])
        P.add("Accent", L.tear(.6, .075, (sx * .42, -.42, 1.0), axis="-Y", k=.25, squash=.4))              # blade
    return P


def hound(L):
    """Hood-ornament greyhound: a long lean chrome body, a teardrop head stretched forward, ears swept back as fins,
    speed-line ribs down the flanks, a swept tail fin, thin legs."""
    P = Parts()
    P.add("Body", [lo([(-.6, .0, .0), (-.55, .1, .12), (-.35, .14, .19), (-.05, .12, .14), (.25, .09, .1), (.5, .07, .08), (.6, .0, .0)],
                      axis="Y", loc=(0, 0, .84), seg=12)])
    P.add("Body", [align(s.rcyl(.065, .3, seg=8, r=0), Vector((0, -.45, .92)), Vector((0, -.62, 1.04)))])
    P.add("Body", L.tear(.46, .085, (0, -.56, 1.0), axis="-Y", k=.35, squash=1.05))
    P.add("Eye", L.slit(.1, (0, -.67, 1.05), h=.02))
    P.add("Eye", [s.ball(.03, (0, -1.01, 1.0), seg=8)])
    for sx in (-1, 1):
        P.add("Body", L.tailfin((sx * .05, -.64, 1.06), (sx * .05, -.54, 1.06), (sx * .11, -.4, 1.1), t=.025))   # swept ear
        for k, z in enumerate((.82, .9)):
            P.add("Body", [s.rbox((.03, .5 - k * .1, .025), (sx * (.13 - k * .01), -.08, z), r=.006, seg=1)])
    P.add("Body", L.tailfin((0, .3, .9), (0, .5, .86), (0, .72, 1.12), t=.03))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        P.add("Body", [s.ball(.085, (x * .8, y, .72), seg=8)])
        leg = f"AccentLeg{i}"
        P.add(leg, [s.ball(.055, (x, y, D.HOUND_HIP), seg=8)])
        P.add(leg, [lo([(D.HOUND_KNEE, .035, .04, 2.0), (.5, .05, .065, 2.0), (D.HOUND_HIP + .04, .05, .06, 2.0)], loc=(x, y, 0), seg=8)])
        P.add(leg, L.tear(.34, .085, (x, y, D.HOUND_HIP + .06), axis="-Z", k=.3, squash=1.0))              # thigh fairing
        P.add(leg, [s.ball(.04, (x, y, D.HOUND_KNEE), seg=8)])
        P.add(leg, [lo([(.04, .025, .025, 2.0), (D.HOUND_KNEE + .02, .03, .03, 2.0)], loc=(x, y, 0), seg=6)])
        P.add(leg, [s.rbox((.06, .12, .04), (x, y - .03, .02), r=.01, seg=1)])
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """The fedora: a felt hat scuttles on eight thin chrome legs coming out from under its brim, two slits glowing
    in the shadow under the front of the brim."""
    P = Parts()
    felt, band = L.fedora((0, .04, .12), r=.16, h=.26, brim=1.9)
    P.add("Body", felt + band)
    P.add("Body", [lo([(.0, .1, .1, 2.0), (.12, .12, .12, 2.0)], loc=(0, 0, .02))])
    P.add("Body", [lo([(0, .09, .05, 2.0), (.1, .09, .05, 2.0)], axis="-Y", loc=(0, -.16, .13))])           # shadowed face
    for sx in (-1, 1):
        P.add("Eye", L.slit(.05, (sx * .045, -.265, .135), h=.022))
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", [align(s.rcyl(.022, (knee - hip).length + .02, seg=6, r=0), hip, knee), s.ball(.032, knee, seg=8)])
        P.add(f"AccentLeg{i}Lower", [align(s.rcyl(.016, (foot - knee).length, seg=6, r=0), knee, foot + Vector((0, 0, .02))),
                                     s.rcyl(.02, .04, foot, seg=6, r=0, radius2=.008)])
    return P


# =========================================================================== wheeled

def listener(L):
    """Cathedral radio: an arched bakelite cabinet with a slatted speaker grille and a glowing half-moon dial, on four
    whitewall wheels, two pill-shaped ribbon microphones on a chrome yoke as its ears."""
    P = Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    for y in (-.3, .3):
        P.add("Body", [s.rbox((.98, .1, .1), (0, y, .25), r=.02, seg=1)])
    P.add("Body", [s.rbox((.7, .86, .12), (0, 0, .32), r=.03, seg=1)])                                         # chassis
    P.add("Body", [prism(arch(.62, .36, 1.08), -.22, .22, bevel=.025)])                                       # cabinet
    P.add("Accent", [prism(arch(.68, .36, 1.12), -.17, .17, bevel=.02)])                                      # wood frame inlay
    for k in range(-3, 4):                                                                                     # grille slats
        x = k * .07
        top = .62 + math.sqrt(max(0, .24 ** 2 - x ** 2)) * 1.4
        P.add("Accent", [s.rbox((.03, .04, top - .62), (x, -.23, .62 + (top - .62) / 2), r=.006, seg=1)])
    P.add("Eye", [prism([(math.cos(a) * .14, .45 + math.sin(a) * .1) for a in [k / 8 * math.pi for k in range(9)]], -.245, -.215, bevel=.004)])
    for sx in (-1, 1):
        P.add("Body", [s.rcyl(.035, .03, (sx * .16, -.235, .45), axis="Y", seg=8, r=0)])
    P.add("Body", [align(s.rcyl(.025, .9, seg=6, r=0), Vector((-.45, .05, 1.1)), Vector((.45, .05, 1.1)))])   # yoke
    for sx in (-1, 1):
        P.add("Body", [lo([(0, .0, .0), (.04, .07, .05, 2.0), (.25, .075, .055, 2.0), (.29, .0, .0)], loc=(sx * .45, .05, .99))])  # ribbon mic
        for z in (1.06, 1.14):
            P.add("Body", [lo([(z - .012, .078, .058, 2.0), (z + .012, .078, .058, 2.0)], loc=(sx * .45, .05, 0))])
    P.add("Body", [align(s.rcyl(.03, .2, seg=6, r=0), Vector((0, .05, 1.0)), Vector((0, .05, 1.15)))])
    return P


def worker(L):
    """Streamlined delivery van: a teardrop body on fender skirts, chrome speed ribs on the flanks, a chrome grille and
    one round headlight, the crate strapped on a roof rack."""
    P = Parts()
    D.wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    for y in (-.25, .25):
        P.add("Body", [s.rbox((.74, .1, .12), (0, y, .17), r=.015, seg=1)])
    P.add("Body", [s.rbox((.46, .8, .1), (0, 0, .24), r=.015, seg=1)])                                         # chassis
    P.add("Body", [lo([(-.46, .0, .0), (-.42, .18, .2), (-.3, .25, .3), (0, .27, .34), (.3, .26, .3), (.44, .17, .2), (.5, .0, .0)],
                      axis="Y", loc=(0, 0, .55), seg=12)])                                                  # teardrop van
    for sx in (-1, 1):
        P.add("Body", [lo([(-.44, .0, .0), (-.36, .06, .1), (0, .07, .13), (.36, .06, .1), (.44, .0, .0)], axis="Y",
                          loc=(sx * .29, 0, .27), seg=8)])                                                  # fender skirt
        for k, z in enumerate((.58, .64, .7)):
            xs = .27 * math.sqrt(max(0, 1 - ((z - .55) / .34) ** 2)) - .004
            P.add("Accent", [s.rbox((.025, .5 - k * .08, .022), (sx * xs, .1, z), r=.006, seg=1)])
    for k in range(-3, 4):                                                                                     # grille
        P.add("Accent", [s.rbox((.025, .04, .16 - abs(k) * .02), (k * .045, -.43 + abs(k) * .012, .32), r=.005, seg=1)])
    P.add("Body", [lo([(0, .0, .0), (.02, .075, .075, 2.0), (.1, .065, .065, 2.0)], axis="Y", loc=(0, -.46, .5))])
    P.add("Eye", [s.rcyl(.06, .02, (0, -.465, .5), axis="Y", seg=10, r=0)])
    for sx in (-1, 1):                                                                                         # roof rack
        P.add("Accent", [s.rbox((.03, .6, .03), (sx * .2, -.1, .9), r=.006, seg=1)])
        for y in (-.36, .16):
            P.add("Accent", [s.rbox((.03, .03, .26), (sx * .2, y, .78), r=.006, seg=1)])
    P.add("Accent", [s.rbox((.48, .4, .34), (0, -.15, 1.13), r=.02, seg=1)])                                    # crate
    for z in (1.03, 1.23):
        P.add("Accent", [s.rbox((.5, .42, .03), (0, -.15, z), r=.006, seg=1)])
    P.add("Accent", [s.rbox((.5, .44, .08), (0, -.15, .94), r=.008, seg=1)])
    return P


def sweeper(L):
    """Canister vacuum cleaner: a chrome torpedo canister lying lengthwise on two sled runners, a fin-handle on top,
    a ribbed hose curling down to a wide floor nozzle in front, a round headlamp on its nose."""
    P = Parts()
    D.wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18), (0, -.3)):
        P.add("Accent", [s.rbox((.035, .05, .14), (x - .045, y, .14), r=.006, seg=1), s.rbox((.035, .05, .14), (x + .045, y, .14), r=.006, seg=1)])
        P.add("Accent", [align(s.rcyl(.015, .12, seg=6, r=0), Vector((x - .06, y, .08)), Vector((x + .06, y, .08)))])
    for sx in (-1, 1):
        P.add("Accent", [lo([(-.42, .015, .015, 2.0, 0, -.04), (-.36, .02, .02, 2.0), (.36, .02, .02, 2.0), (.4, .015, .015, 2.0, 0, -.03)],
                            axis="Y", loc=(sx * .26, 0, .2), seg=6)])                                      # runners
        for y in (-.25, .2):
            P.add("Accent", [align(s.rcyl(.018, .2, seg=6, r=0), Vector((sx * .26, y, .2)), Vector((sx * .15, y, .38)))])
    P.add("Accent", [s.rbox((.56, .12, .06), (0, -.3, .22), r=.012, seg=1), s.rbox((.56, .12, .06), (0, .18, .22), r=.012, seg=1)])
    P.add("Body", [lo([(-.4, .0, .0), (-.37, .13, .13), (-.28, .2, .2), (0, .22, .22), (.2, .2, .2), (.36, .12, .12), (.42, .0, .0)],
                      axis="Y", loc=(0, 0, .5), seg=12)])                                                   # canister
    for y in (-.2, .12):
        P.add("Accent", [lo([(y - .015, .225, .225, 2.0), (y + .015, .225, .225, 2.0)], axis="Y", loc=(0, 0, .5), seg=12)])
    P.add("Accent", L.tailfin((0, -.15, .7), (0, .3, .66), (0, .2, .88), t=.04))
    P.add("Accent", [s.rbox((.06, .2, .05), (0, .02, .76), r=.012, seg=1)])
    P.add("Accent", [s.tube([(.12, -.3, .55), (.3, -.38, .5), (.32, -.5, .3), (.2, -.52, .1), (.1, -.5, .05)], .04, res=1)])   # hose
    P.add("Accent", [s.rbox((.42, .1, .05), (0, -.52, .03), r=.012, seg=1, taper=(.9, .8))])
    for sx in (-1, 1):                                                                                     # hubcap brushes
        P.add("Accent", [la([(0, .0), (.14, .0), (.15, .02), (.1, .04), (0, .05)], loc=(sx * .34, -.32, .0), seg=12)])
        for k in range(6):
            a = k / 6 * math.tau
            P.add("Accent", [s.place(s.rbox((.12, .02, .02), r=.004, seg=1), (sx * .34 + math.cos(a) * .1, -.32 + math.sin(a) * .1, .015),
                                     rot=(0, 0, a))])
        P.add("Accent", [align(s.rcyl(.02, .3, seg=6, r=0), Vector((sx * .34, -.32, .04)), Vector((sx * .26, -.2, .22)))])
    P.add("Eye", [s.rcyl(.07, .03, (0, -.385, .52), axis="Y", seg=10, r=0)])
    P.add("Accent", [s.rcyl(.07, .16, (0, 0, .32), seg=10, r=0)])
    P.add("AccentLidar", [s.rcyl(.12, .07, (0, 0, .22), seg=10, r=0), s.rbox((.06, .06, .04), (0, -.12, .22), r=.006, seg=1)])
    return P


def thumper(L):
    """Streamlined locomotive: a bullet-nosed hull with skirts down over the tracks, chrome speed lines running back
    from the nose, a cyclops headlamp on the pilot, two pistons hammering the pads."""
    P = Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.24, 1.36, .5), (sx * .56, 0, .26)))
    P.add("Body", [s.rbox((1.0, 1.2, .14), (0, 0, .5), r=.02, seg=1)])
    P.add("Body", [lo([(-.66, .4, .38, 3.0), (-.3, .46, .42, 3.2), (.3, .44, .42, 3.0), (.5, .36, .38, 2.6), (.7, .2, .26, 2.2),
                       (.78, .0, .0, 2.0, 0, -.05)], axis="-Y", loc=(0, 0, .86), seg=14)])                    # bullet hull
    for sx in (-1, 1):
        P.add("Body", [s.rbox((.06, 1.3, .42), (sx * .55, 0, .58), r=.02, seg=1, taper=(1, .95))])         # skirts
        for k, z in enumerate((.86, .96, 1.06)):
            P.add("Accent", [s.rbox((.035, .9 - k * .15, .03), (sx * (.455 - k * .03), -.02 - k * .06, z), r=.006, seg=1)])
    P.add("Accent", [lo([(-.48, .06, .2, 2.4), (.4, .06, .2, 2.4)], axis="Y", loc=(0, .1, 1.2), seg=8)])      # roof fin
    P.add("Body", [s.rbox((.5, .3, .3), (0, -.62, .38), r=.04, seg=1, taper=(.6, .7))])                       # pilot
    P.add("Eye", [s.rcyl(.07, .03, (0, -.79, .3), axis="Y", seg=10, r=0)])
    P.add("Accent", [s.rcyl(.09, .06, (0, -.76, .3), axis="Y", seg=10, r=0)])
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", [align(s.rcyl(.05, .4, seg=8, r=0), Vector((x, -.5, .75)), Vector((x, -.85, .78)))])
        P.add("Body", [s.rcyl(.08, .26, (x, -.85, .68), seg=8, r=.01)])
        P.add("Accent", [s.rcyl(.04, .6, (x, -.85, .32), seg=8, r=0)])
        P.add("Accent", [s.rcyl(.15, .06, (x, -.85, .06), seg=10, r=.01, radius2=.11)])
        P.add("Eye", [s.rcyl(.18, .04, (x, -.85, .02), seg=12, r=0)])
    return P


# =========================================================================== tall

def sorter(L):
    """Hollywood searchlight: a teardrop pedestal, a chrome yoke, a finned drum with the big colour lens behind
    venetian slats."""
    P = Parts()
    P.add("Accent", la([(0, 0), (.3, 0), (.3, .04), (.24, .08), (0, .08)]))
    P.add("Body", [lo([(.06, .2, .2, 2.0), (.3, .12, .12, 2.0), (.7, .07, .07, 2.0), (.92, .06, .06, 2.0)])])
    P.add("Body", [s.rbox((.1, .1, .1), (0, .05, .95), r=.01, seg=1)])
    P.add("Accent", [s.tube([(-.33, .05, 1.25), (-.33, .05, 1.0), (.33, .05, 1.0), (.33, .05, 1.25)], .03, smooth=False, res=1)])  # yoke
    P.add("Body", la([(0, 0), (.24, 0), (.27, .05), (.27, .28), (.22, .34), (0, .34)], axis="-Y", loc=(0, .22, 1.25)))           # drum
    for y in (.0, .08, .16):
        P.add("Body", [lo([(-.015, .29, .29, 2.0), (.015, .29, .29, 2.0)], axis="-Y", loc=(0, y, 1.25), seg=SEG)])
    P.add("Accent", la([(.22, 0), (.29, 0), (.29, .04), (.22, .04)], axis="-Y", loc=(0, -.12, 1.25)))
    P.add("Eye", la([(.23, 0), (.22, .02), (.12, .04), (0, .045)], axis="-Y", loc=(0, -.12, 1.25)))
    for k in range(-2, 3):
        z = 1.25 + k * .09
        w = 2 * math.sqrt(max(0, .25 ** 2 - (z - 1.25) ** 2))
        P.add("Accent", [s.rbox((w, .015, .035), (0, -.175, z), r=.004, seg=1, rot=(math.radians(35), 0, 0))])
    P.add("Accent", [s.rbox((.035, .02, .5), (0, -.17, 1.25), r=.004, seg=1)])
    return P


def drone(L):
    """Flying teardrop wing: a flat chrome wing with a blunt round front and a pointed tail, a teardrop canopy and a tail
    fin on top, speed ribs, four fans set in round cowls on the wing, a ball camera under the nose."""
    P = Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    wing = lo([(-.62, .0, .0), (-.58, .3, .02), (-.48, .5, .03), (-.3, .6, .035), (0, .6, .04), (.3, .6, .035), (.48, .5, .03),
               (.6, .26, .02), (.7, .0, .0)], axis="Y", seg=14)
    P.add("Body", [wing])
    P.add("Body", L.tear(.62, .13, (0, -.32, .02), axis="Y", k=.3, squash=.55))                         # canopy
    P.add("Accent", L.tailfin((0, .2, .04), (0, .62, .01), (0, .58, .12), t=.025))
    for sx in (-1, 1):
        for k in range(3):
            P.add("Accent", [s.rbox((.025, .5 - k * .08, .02), (sx * (.16 + k * .05), .02, .035), r=.005, seg=1)])
    for i, (x, y) in enumerate(rot):
        P.add("Body", [la([(.18, .0), (.205, .0), (.21, .03), (.205, .06), (.185, .06)], loc=(x, y, .03), seg=12)])   # fan cowl
        P.add("Body", [s.rcyl(.04, .05, (x, y, .03), seg=8, r=0)])
        c = Vector((x, y, .065))
        blades = [s.rcyl(.03, .05, c, seg=8, r=0)]
        for k in range(4):
            b = s.loft([(0, .02, .005), (.08, .04, .006), (.16, .025, .004)], seg=6, axis="X")
            s.place(b, rot=(math.radians(18), 0, k / 4 * math.tau))
            blades.append(s.place(b, c))
        P.add(f"AccentRotor{i}", blades)
    P.add("Body", [align(s.rcyl(.025, .1, seg=6, r=0), Vector((0, -.25, -.02)), Vector((0, -.25, -.08)))])
    P.add("Accent", [s.ball(.055, (0, -.25, -.1), seg=8)])
    P.add("Eye", [s.rcyl(.035, .02, (0, -.3, -.1), axis="Y", seg=10, r=0)])
    return P


def turret(L):
    """Finned bullet on a pylon: a single streamlined chrome pylon with three tail fins at its foot and a glowing collar
    under the top, the head a chrome bullet pod with three fins and a barrel from its nose."""
    P = Parts()
    P.add("Body", la([(0, 0), (.3, 0), (.3, .04), (.26, .07), (0, .07)], seg=12))
    P.add("Body", la([(0, .06), (.17, .06), (.13, .3), (.08, .7), (.07, .88), (.11, .98), (.12, 1.05), (.09, 1.1), (0, 1.1)], seg=10))
    for k in range(3):
        a = math.pi / 2 + k * math.tau / 3
        d = Vector((math.cos(a), math.sin(a), 0))
        P.add("Body", L.tailfin(d * .1 + Vector((0, 0, .5)), d * .15 + Vector((0, 0, .06)), d * .36 + Vector((0, 0, .06)), t=.035))
    P.add("Eye", [lo([(.99, .125, .125, 2.0), (1.02, .125, .125, 2.0)])])
    P.add("Eye", [lo([(.06, .305, .305, 2.0), (.075, .305, .305, 2.0)], seg=12)])
    P.add("AccentHead", la([(0, 1.1), (.08, 1.1), (.08, 1.21), (0, 1.21)]))
    P.add("AccentHead", L.tear(.52, .13, (0, -.3, 1.3), axis="Y", k=.3))
    for k in range(3):
        a = math.pi / 2 + k * math.tau / 3
        d = Vector((math.cos(a), 0, math.sin(a)))
        P.add("AccentHead", L.tailfin(Vector((0, .02, 1.3)) + d * .08, Vector((0, .2, 1.3)) + d * .03, Vector((0, .21, 1.3)) + d * .15, t=.025))
    P.add("AccentHead", [s.rcyl(.03, .3, (0, -.43, 1.3), axis="Y", seg=8, r=0)])
    for sx in (-1, 1):
        P.add("AccentHead", [s.rbox((.03, .3, .025), (sx * .12, -.08, 1.33), r=.005, seg=1)])
    return P


def vacuum(L):
    """Slatted ceiling fixture: a stepped chrome dish (the room side is -z) with a round opening crossed by venetian
    slats, the light glowing between them."""
    P = Parts()
    P.add("Body", [la([(0, .1), (.34, .1), (.36, .08), (.36, .03), (.34, .0), (0, .0)], seg=14)])
    P.add("Accent", [la([(.22, .005), (.33, .005), (.33, -.02), (.3, -.03), (.22, -.03)], seg=14)])
    P.add("Eye", [la([(0, -.005), (.22, -.005), (.22, .002), (0, .002)], seg=14)])
    for k in range(-4, 5):
        y = k * .045
        w = 2 * math.sqrt(max(0, .23 ** 2 - y ** 2))
        P.add("Accent", [s.rbox((w, .02, .035), (0, y, -.025), r=.004, seg=1, rot=(math.radians(30), 0, 0))])
    for k in range(4):                                                                                     # fan blades
        a = math.pi / 4 + k * math.tau / 4
        b = s.rbox((.24, .13, .014), (.0, 0, 0), r=.004, seg=1, taper=(1, 1))
        s.place(b, rot=(math.radians(12), 0, 0))
        s.place(b, (.43, 0, .04), rot=(0, 0, 0))
        s.place(b, rot=(0, 0, a))
        P.add("Body", [b])
        P.add("Body", [s.place(s.rbox((.12, .05, .02), r=.004, seg=1), (math.cos(a) * .3, math.sin(a) * .3, .045), rot=(0, 0, a))])
    return P


# =========================================================================== devices

def security_camera(L):
    """A chrome teardrop camera on a bracket, speed ribs, a slit hood."""
    P = Parts()
    P.add("Body", [s.rbox((.16, .06, .22), (0, .27, 0), r=.015, seg=1)])
    P.add("Accent", [align(s.rcyl(.025, .2, seg=6, r=0), Vector((0, .26, 0)), Vector((0, .08, -.02)))])
    P.add("Accent", L.tear(.44, .09, (0, -.2, -.02), axis="Y", k=.3))
    P.add("Accent", L.tailfin((0, .05, .05), (0, .2, .02), (0, .2, .1), t=.02))
    P.add("Eye", [s.rcyl(.05, .02, (0, -.205, -.02), axis="Y", seg=10, r=0)])
    return P


def lidar(L):
    P = Parts()
    P.add("Body", la([(0, 0), (.18, 0), (.16, .04), (.06, .12), (.045, .3), (.06, .39), (0, .39)]))
    P.add("Eye", [lo([(.38, .08, .08, 2.0), (.4, .08, .08, 2.0)])])
    P.add("AccentHead", la([(0, .395), (.08, .395), (.1, .42), (.1, .47), (.06, .52), (0, .52)]))
    P.add("AccentHead", [s.rbox((.12, .05, .025), (0, -.09, .45), r=.005, seg=1)])
    return P


def pir(L):
    """A bakelite wall box with venetian slats over the fresnel eye."""
    P = Parts()
    P.add("Body", [s.rbox((.16, .06, .16), (0, .04, 0), r=.015, seg=1)])
    P.add("Eye", [la([(.06, 0), (.055, .025), (.03, .05), (0, .055)], axis="-Y", loc=(0, .01, 0), seg=8)])
    for k in (-1, 0, 1):
        P.add("Body", [s.rbox((.15, .015, .02), (0, -.045, k * .04), r=.003, seg=1, rot=(math.radians(30), 0, 0))])
    return P


# =========================================================================== furniture and level pieces
# A 1940s detective agency: racetrack desks with waterfall ends, bakelite cabinets with radiused corners and chrome
# speed lines, tubular chrome cantilever chairs, venetian blinds, rounded CRTs, torchieres, sansevieria.

def track_slab(w, d, h, loc, band=True):
    """A racetrack board (fully rounded ends) with a chrome band round its edge."""
    x, y, z = loc
    r = min(w, d) * .48
    out = [s.rbox((w, d, h), loc, r=r, seg=2, vertical_only=True)]
    if band:
        out.append(s.rbox((w + .012, d + .012, h * .35), loc, r=r + .006, seg=2, vertical_only=True))
    return out


def waterfall(w, d, h, loc, side=1):
    """A waterfall end: a slab that rounds over the top edge and runs down to the floor (side = which way it faces)."""
    x, y, z = loc
    r = min(.12, w * .6)
    out = [s.rbox((w, d, h), (x, y, z + h / 2), r=r, seg=3)]
    return out


def speed_lines(x0, x1, y, zs, t=.016):
    """Horizontal chrome strips from x0 to x1 on a face at y, at heights zs."""
    return [s.rbox((abs(x1 - x0), t, t), ((x0 + x1) / 2, y, z), r=.004, seg=1) for z in zs]


def slats(w, h, loc, n=8, tilt=30, t=.014):
    """Venetian blind slats across a w x h opening in the XZ plane at loc (facing -y)."""
    x, y, z = loc
    return [s.rbox((w, t, h / n * .7), (x, y, z - h / 2 + h / n * (k + .5)), r=.003, seg=1, rot=(math.radians(tilt), 0, 0)) for k in range(n)]


def p_desk(L):
    """Streamline desk: a racetrack top, a waterfall end on one side, a drawer pedestal with speed lines on the other,
    a rounded CRT."""
    P = Parts()
    P.add("Body", track_slab(1.6, .8, .05, (0, 0, .74)))
    P.add("Accent", waterfall(.06, .7, .73, (-.66, 0, 0)))
    P.add("Accent", [s.rbox((.42, .7, .72), (.5, 0, .36), r=.08, seg=2, vertical_only=True)] + speed_lines(.3, .7, -.355, (.25, .3, .35)))
    acc, glow = L.screen((.5, .32), (0, .17, .98), "-Y")
    P.add("Accent", acc + [s.rbox((.06, .05, .2), (0, .2, .84), r=.02, seg=1), s.rbox((.24, .16, .02), (0, .2, .775), r=.06, seg=1)])
    P.add("Eye", glow)
    return P


def p_terminal(L):
    """Radio-room console: a racetrack top between two waterfall ends, three rounded CRTs, a bakelite keyboard."""
    P = Parts()
    P.add("Body", track_slab(1.6, .8, .05, (0, 0, .74)))
    P.add("Accent", waterfall(.08, .74, .73, (-.68, 0, 0)) + waterfall(.08, .74, .73, (.68, 0, 0)) + [s.rbox((1.3, .03, .4), (0, .33, .52), r=.01, seg=1)])
    P.add("AccentConsole", [s.rbox((.52, .17, .03), (0, -.1, .778), r=.02, seg=1)])
    for x, y in ((-.33, .14), (0, .18), (.33, .14)):
        acc, glow = L.screen((.27, .21), (x, y, .99), "-Y")
        P.add("Eye", glow).add("AccentConsole", acc + [s.rbox((.06, .06, .14), (x, y + .03, .83), r=.02, seg=1)])
    return P


def p_chair(L):
    """Tubular chrome cantilever chair: one bent tube runs along the floor, up the front and back under a rounded
    bakelite seat and back."""
    P = Parts()
    P.add("Body", L.cushion((.56, .5, .07), (0, 0, .46)) + L.cushion((.54, .07, .44), (0, .22, .82), rot=(math.radians(-8), 0, 0)))
    for sx in (-1, 1):
        x = sx * .27
        P.add("Accent", [s.tube([(x, .28, .02), (x, -.22, .02), (x, -.28, .1), (x, -.24, .42), (x, .12, .44), (x, .2, .6), (x, .24, 1.05)],
                                .016, res=1, smooth=False)])
    return P


def p_server_rack(L):
    """Teletype cabinet: a tall bakelite case with radiused corners, venetian slats over rows of glowing tubes, chrome
    speed lines."""
    P = Parts()
    P.add("Body", L.carcass((.7, 1.0, 2.0), (0, 0, 0), kick=.05))
    for i in range(10):
        P.add("Eye", [s.rbox((.5, .02, .05), (0, -.495, .3 + i * .15), r=.006, seg=1)])
        P.add("Body", [s.rbox((.56, .025, .03), (0, -.505, .35 + i * .15), r=.004, seg=1, rot=(math.radians(25), 0, 0))])
    return P


def p_cabinet(L):
    """Filing cabinet with radiused corners, three drawers with long chrome bars and speed lines."""
    P = Parts()
    P.add("Body", L.carcass((.9, .5, 1.4), (0, 0, 0)))
    for i in range(3):
        z = .3 + i * .42
        P.add("Body", L.door((.84, .38), (0, -.255, z)))
        P.add("Accent", L.handle((0, -.268, z + .1), .4, vertical=False))
    return P


def p_meeting_table(L):
    """Conference table: a long racetrack top on two oval chrome-banded pedestals."""
    P = Parts()
    P.add("Body", track_slab(3.2, 1.3, .06, (0, 0, .75)))
    for x in (-1.0, 1.0):
        P.add("Accent", [s.rbox((.5, .3, .72), (x, 0, .36), r=.14, seg=2, vertical_only=True), s.rbox((.7, .5, .04), (x, 0, .02), r=.24, seg=2, vertical_only=True)]
              + speed_lines(x - .26, x + .26, -.155, (.3, .36, .42)))
    return P


def p_plant(L):
    """Sansevieria in a stepped chrome-banded planter."""
    P = Parts()
    P.add("Accent", L.pot(.24, .42, (0, 0, 0)))
    P.add("Body", L.foliage((0, 0, .7), .42, n=7, seed=2) + [s.rcyl(.2, .06, (0, 0, .41), seg=10, r=0)])
    return P


def p_kitchen_counter(L):
    """Diner counter: rounded ends, chrome speed lines along the front, a bakelite radio-oven with a slatted glowing window."""
    P = Parts()
    P.add("Body", [s.rbox((2.4, .65, .82), (0, 0, .47), r=.3, seg=2, vertical_only=True), s.rbox((2.3, .55, .07), (0, .02, .04), r=.25, seg=2, vertical_only=True)])
    P.add("Body", speed_lines(-1.0, 1.0, -.33, (.3, .36, .42)))
    for x in (-.6, 0, .6):
        P.add("Accent", L.handle((x, -.33, .7), .3, vertical=False))
    P.add("Accent", track_slab(2.44, .7, .05, (0, 0, .905)))
    P.add("Body", [s.rbox((.5, .42, .3), (.7, .02, 1.08), r=.1, seg=2)])
    P.add("Eye", [s.rbox((.3, .02, .16), (.68, -.195, 1.08), r=.04, seg=1)])
    P.add("Accent", slats(.3, .16, (.68, -.205, 1.08), n=4, tilt=20))
    return P


def p_shelf(L):
    """Streamline etagere: shelves with half-round ends on chrome tubes, suitcases and hat boxes on them."""
    P = Parts()
    for x in (-.7, .7):
        for y in (-.25, .25):
            P.add("Body", [s.rcyl(.02, 2.0, (x, y, 1.0), seg=8, r=0)])
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", track_slab(2.0, .6, .04, (0, 0, h), band=False))
    P.add("Accent", [s.rbox((.5, .3, .32), (-.4, 0, .63), r=.06, seg=2), s.rcyl(.16, .24, (.5, 0, .59), seg=10, r=.02),
                     s.rbox((.6, .32, .3), (.3, 0, 1.22), r=.06, seg=2), s.rcyl(.18, .22, (-.5, 0, 1.78), seg=10, r=.02)])
    return P


def p_boxes(L):
    """Streamline luggage: two suitcases and a hat box with chrome corners."""
    P = Parts()
    P.add("Accent", [s.rbox((.6, .6, .5), (0, 0, .25), r=.1, seg=2), s.rbox((.5, .5, .4), (.05, -.05, .7), r=.1, seg=2),
                     s.rcyl(.22, .4, (.55, .1, .2), seg=10, r=.03)] + speed_lines(-.3, .3, -.302, (.15, .2)) + speed_lines(-.2, .3, -.302, (.62,)))
    return P


def p_water_cooler(L):
    """Chrome fountain: a stepped streamline column with speed lines, a teardrop glass bottle on top."""
    P = Parts()
    P.add("Body", la([(0, 0), (.17, 0), (.17, .05), (.14, .08), (.12, .7), (.15, .9), (.15, 1.0), (0, 1.0)]))
    P.add("Body", [s.rcyl(.125, .02, (0, 0, z), seg=10, r=0) for z in (.4, .46, .52)] + [s.rbox((.06, .08, .04), (0, -.15, .9), r=.015, seg=1)])
    P.add("Eye", la([(0, .99), (.08, 1.0), (.15, 1.12), (.14, 1.26), (.08, 1.38), (.03, 1.44), (0, 1.45)]))
    return P


def p_whiteboard(L):
    """A racetrack chalkboard on a chrome tube stand with sled feet."""
    P = Parts()
    P.add("Body", [s.place(s.rbox((1.9, .95, .03), (0, 0, 0), r=.4, seg=2, vertical_only=True), (0, 0, 1.5), rot=(math.pi / 2, 0, 0))])
    for x in (-.86, .86):
        P.add("Accent", [s.tube([(x, -.035, .02), (x, .035, .02), (x, 0, .06), (x, 0, 1.98)], .018, res=1, smooth=False)])
    P.add("Accent", [s.rbox((1.72, .06, .04), (0, 0, 1.04), r=.015, seg=1)])
    return P


def p_sofa(L):
    """Streamline sofa: rounded waterfall arms, a channel-tufted back, a chrome base strip."""
    P = Parts()
    P.add("Body", L.cushion((1.8, .7, .2), (0, -.03, .22)) + L.cushion((.88, .68, .16), (-.45, -.05, .38)) + L.cushion((.88, .68, .16), (.45, -.05, .38)))
    P.add("Body", L.cushion((1.8, .22, .5), (0, .3, .62), rot=(math.radians(-6), 0, 0)))
    for x in (-.9, -.45, 0, .45, .9):
        P.add("Body", [s.rbox((.02, .04, .4), (x, .2, .64), r=.006, seg=1)])
    for sx in (-1, 1):
        P.add("Accent", [s.place(s.rbox((.62, .84, .2), (0, 0, 0), r=.09, seg=2), (sx * 1.0, 0, .31), rot=(0, math.pi / 2, 0))])
    P.add("Accent", [s.rbox((2.0, .72, .04), (0, 0, .03), r=.06, seg=1)])
    return P


def p_toilet_stall(L):
    """A cubicle with radiused top corners and chrome edge strips."""
    P = Parts()
    for x in (-.5, .5):
        P.add("Body", [s.place(s.rbox((1.5, 1.88, .04), (0, 0, 0), r=.3, seg=2, vertical_only=True), (x, 0, 1.06), rot=(math.pi / 2, 0, math.pi / 2))])
        P.add("Accent", [s.rbox((.06, .03, 1.6), (x, -.74, .95), r=.01, seg=1)])
        for y in (-.7, 0.0):
            P.add("Body", [s.rcyl(.025, .14, (x, y, .07), seg=8, r=0)])
    P.add("Body", [s.rbox((1.04, .04, 1.88), (0, .75, 1.06), r=.02, seg=1)])
    P.add("Accent", [la([(0, 0), (.15, 0), (.2, .3), (.22, .4), (0, .42)], loc=(0, .4, 0)), s.rbox((.36, .14, .36), (0, .66, .55), r=.06, seg=1)])
    return P


def p_partition(L):
    """A venetian blind screen: a chrome frame full of tilted slats on two sled feet."""
    P = Parts()
    P.add("Body", slats(1.5, 1.1, (0, 0, .67), n=16, tilt=35, t=.012))
    P.add("Accent", [s.rbox((1.6, .05, .04), (0, 0, 1.24), r=.015, seg=1), s.rbox((1.6, .05, .04), (0, 0, .1), r=.015, seg=1)])
    for x in (-.78, .78):
        P.add("Accent", [s.rbox((.04, .05, 1.2), (x, 0, .64), r=.015, seg=1), s.rbox((.06, .06, .05), (x, 0, .025), r=.02, seg=1)])
    P.add("Body", [s.rbox((.012, .02, 1.12), (x, -.004, .67), r=.003, seg=1) for x in (-.4, .4)])
    return P


def p_bookshelf(L):
    """Bookcase with quarter-round open ends and speed lines, rows of files."""
    P = Parts()
    P.add("Body", [s.rbox((.04, .35, 2.0), (x, 0, 1.0), r=.012, seg=1) for x in (-.48, .48)])
    for h in (.03, .5, 1.0, 1.5, 1.98):
        P.add("Body", [s.rbox((1.0, .35, .03), (0, 0, h), r=.012, seg=1)])
    P.add("Body", [s.rbox((.96, .02, 1.96), (0, .165, 1.0), r=.004, seg=1)])
    for sx in (-1, 1):
        P.add("Body", [s.place(s.loft([(0, .15, .15, 2.0), (1.96, .15, .15, 2.0)], seg=10, loc=(0, 0, 0)), (sx * .48, .02, .02), scale=(1, 1, 1))])
    for i, h in enumerate((.045, .515, 1.015, 1.515)):
        x = -.43
        for k in range(6 + i % 2):
            w = .07 + .03 * ((k + i) % 3)
            hh = .32 - .04 * ((k * 7 + i) % 3)
            P.add("Accent", [s.rbox((w, .26, hh), (x + w / 2, .02, h + hh / 2), r=.01, seg=1)])
            x += w + .012
    return P


def p_printer(L):
    """Bakelite teletype: a rounded body, a paper roll on top, a glowing dial."""
    P = Parts()
    P.add("Body", [s.rbox((.6, .6, .95), (0, 0, .475), r=.12, seg=2, vertical_only=True), s.place(s.rcyl(.12, .5, (0, 0, 0), axis="X", seg=10, r=.01), (0, .05, .98))])
    P.add("Accent", [s.rbox((.36, .3, .03), (-.05, -.37, .82), r=.04, seg=1, rot=(math.radians(8), 0, 0))] + L.door((.5, .3), (0, -.31, .3)))
    P.add("Eye", [s.rcyl(.05, .02, (.18, -.305, .8), axis="Y", seg=10, r=0)])
    return P


def p_fridge(L):
    """1940s fridge: a rounded-top cabinet, a big chrome lever handle, chrome speed lines at the foot."""
    P = Parts()
    P.add("Body", [s.rbox((.7, .7, 1.76), (0, 0, .92), r=.16, seg=2), s.rbox((.6, .6, .06), (0, .02, .03), r=.05, seg=1)])
    P.add("Body", [s.rbox((.64, .03, 1.5), (0, -.34, .9), r=.08, seg=1)] + speed_lines(-.25, .25, -.36, (.25, .29, .33)))
    P.add("Accent", [s.rbox((.06, .06, .34), (.24, -.39, 1.25), r=.025, seg=1), s.rbox((.04, .06, .04), (.24, -.365, 1.1), r=.01, seg=1),
                     s.rbox((.04, .06, .04), (.24, -.365, 1.4), r=.01, seg=1)])
    return P


def p_coffee_machine(L):
    """Streamline espresso machine: a horizontal chrome teardrop boiler on a stepped base, two spouts, a dial."""
    P = Parts()
    P.add("Body", [s.rbox((.34, .4, .12), (0, -.02, .06), r=.05, seg=2, vertical_only=True)])
    P.add("Accent", L.tear(.44, .13, (0, -.24, .26), axis="Y", k=.35) + [s.rbox((.14, .2, .06), (0, .0, .15), r=.02, seg=1)])
    P.add("Accent", L.tailfin((0, -.05, .36), (0, .15, .34), (0, .13, .5), t=.02))
    for x in (-.06, .06):
        P.add("Accent", [s.rcyl(.015, .08, (x, -.2, .17), seg=6, r=0)])
    P.add("Eye", [s.rcyl(.035, .02, (0, -.245, .27), axis="Y", seg=10, r=0)])
    return P


def p_round_table(L):
    """Round table on a stepped chrome cone pedestal."""
    P = Parts()
    P.add("Body", [s.rcyl(.5, .04, (0, 0, .74), seg=16, r=.015), s.rcyl(.506, .014, (0, 0, .74), seg=16, r=0)])
    P.add("Accent", la([(0, 0), (.3, 0), (.3, .03), (.2, .05), (.2, .08), (.08, .2), (.05, .72), (0, .72)], seg=12))
    return P


def p_armchair(L):
    """Club chair: a channel-tufted seat and back between two rounded waterfall arms."""
    P = Parts()
    P.add("Body", L.cushion((.6, .66, .4), (0, -.02, .22)) + L.cushion((.6, .62, .14), (0, -.04, .46)) + L.cushion((.6, .2, .46), (0, .28, .64), rot=(math.radians(-8), 0, 0)))
    for sx in (-1, 1):
        P.add("Accent", [s.place(s.rbox((.66, .74, .14), (0, 0, 0), r=.07, seg=2), (sx * .37, 0, .33), rot=(0, math.pi / 2, 0))])
    return P


def p_coffee_table(L):
    """A racetrack low table on two chrome sled runners."""
    P = Parts()
    P.add("Body", track_slab(1.0, .55, .04, (0, 0, .4)))
    for x in (-.36, .36):
        P.add("Accent", [s.tube([(x, -.22, .38), (x, -.22, .04), (x, .22, .04), (x, .22, .38)], .016, res=1, smooth=False)])
    return P


def p_tv_stand(L):
    """A rounded bakelite console with speed lines, the big rounded CRT on top."""
    P = Parts()
    P.add("Body", L.carcass((1.4, .4, .5), (0, 0, 0), kick=.04))
    acc, glow = L.screen((1.28, .76), (0, .05, 1.04), "-Y")
    P.add("Accent", acc + [s.rbox((.3, .2, .05), (0, .07, .52), r=.06, seg=1), s.rbox((.06, .05, .16), (0, .08, .6), r=.02, seg=1)])
    P.add("Eye", glow)
    return P


def p_reception_desk(L):
    """Streamline counter: a front that curves round both ends, three chrome speed lines wrapping it."""
    P = Parts()
    P.add("Body", [s.rbox((2.5, .8, 1.1), (0, -.04, .55), r=.38, seg=2, vertical_only=True)])
    for k in range(3):
        P.add("Accent", [s.rbox((2.512, .812, .02), (0, -.04, .3 + k * .07), r=.386, seg=2, vertical_only=True)])
    P.add("Accent", track_slab(2.5, .3, .04, (0, -.33, 1.12)))
    acc, glow = L.screen((.46, .3), (.4, .1, 1.0), "-Y")
    P.add("Eye", glow + acc)
    return P


def p_trash_bin(L):
    """A stepped chrome bin with a domed lid."""
    P = Parts()
    P.add("Body", la([(0, 0), (.16, 0), (.16, .05), (.18, .05), (.18, .36), (0, .36)]))
    P.add("Accent", [la([(0, .35), (.19, .35), (.19, .39), (.12, .43), (0, .44)])] + [s.rcyl(.185, .02, (0, 0, z), seg=10, r=0) for z in (.15, .2)])
    return P


def p_coat_rack(L):
    """A chrome pole with teardrop hooks, a fedora on one hook and a trench coat on another."""
    P = Parts()
    P.add("Accent", [s.rcyl(.022, 1.76, (0, 0, .9), seg=8, r=0), la([(0, 0), (.24, 0), (.24, .03), (.1, .06), (0, .07)])])
    for a in (0.0, 2.1, 4.2):
        d = Vector((math.cos(a), math.sin(a), 0))
        P.add("Accent", L.tear_between(d * .2 + Vector((0, 0, 1.72)), Vector((0, 0, 1.66)), .025))
    felt, band = L.fedora((0, 0, 0), r=.1, h=.12, brim=1.9)
    for bm in felt + band:
        s.place(bm, (.2, 0, 1.73))
    P.add("Body", felt + band + L.cushion((.32, .12, .62), (-.08, .1, 1.34), rot=(0, 0, 2.1)))
    return P


def p_lockers(L):
    """Three rounded-top lockers with slatted vents and chrome bars."""
    P = Parts()
    P.add("Body", [s.rbox((.9, .5, .06), (0, .02, .03), r=.02, seg=1)])
    for x in (-.3, 0, .3):
        P.add("Body", [s.rbox((.3, .5, 1.84), (x, 0, .98), r=.14, seg=2, vertical_only=False)])
        P.add("Accent", L.handle((x + .09, -.26, 1.0), .2) + slats(.18, .15, (x, -.255, 1.65), n=4, tilt=25))
    return P


def p_pallet(L):
    """A pallet stacked with rounded crates banded in chrome."""
    P = Parts()
    P.add("Accent", L.slab((1.2, 1.0, .04), (0, 0, .13)) + [s.rbox((1.2, .1, .11), (0, y, .055), r=.02, seg=1) for y in (-.45, 0, .45)])
    for x, y, z in ((-.3, -.24, .15), (.3, -.24, .15), (-.3, .24, .15), (.3, .24, .15), (-.3, -.24, .55), (-.3, .24, .55)):
        P.add("Body", [s.rbox((.55, .45, .4), (x, y, z + .2), r=.08, seg=2), s.rbox((.565, .465, .03), (x, y, z + .2), r=.085, seg=1)])
    return P


def p_wash_basin(L):
    """A rounded vanity with chrome speed lines, round basins and a round stepped mirror."""
    P = Parts()
    P.add("Body", [s.rbox((1.6, .45, .82), (0, .02, .41), r=.2, seg=2, vertical_only=True)] + track_slab(1.6, .5, .06, (0, 0, .85)) + [s.rbox((1.6, .03, .34), (0, .24, 1.02), r=.01, seg=1)])
    P.add("Body", speed_lines(-.6, .6, -.205, (.3, .36, .42)))
    for x in (-.4, .4):
        P.add("Accent", la([(0, 0), (.12, 0), (.17, .07), (.17, .09), (0, .09)], loc=(x, -.02, .83)))
        P.add("Accent", [s.tube([(x, .17, .87), (x, .17, 1.0), (x, .08, .99)], .015, res=1, smooth=False)])
    P.add("Eye", [s.rcyl(.38, .02, (0, .235, 1.53), axis="Y", seg=16, r=0)])
    P.add("Accent", [s.lathe([(.38, 0), (.42, 0), (.42, .03), (.38, .03)], seg=16, axis="-Y", loc=(0, .25, 1.53))])
    return P


def p_vending_machine(L):
    """Cigarette machine: a rounded-top cabinet, a slatted glowing window, a row of chrome pull knobs, speed lines."""
    P = Parts()
    P.add("Body", [s.rbox((.9, .8, 1.84), (0, 0, .94), r=.2, seg=2), s.rbox((.8, .7, .06), (0, .02, .03), r=.06, seg=1)])
    P.add("Eye", [s.rbox((.6, .02, .7), (-.05, -.405, 1.25), r=.08, seg=1)])
    P.add("Accent", slats(.6, .7, (-.05, -.415, 1.25), n=7, tilt=20) + speed_lines(-.38, .38, -.405, (.45, .5, .55)))
    for k in range(5):
        P.add("Accent", [s.rbox((.06, .05, .05), (-.24 + k * .12, -.415, .76), r=.02, seg=1)])
    return P


def p_ping_pong_table(L):
    """Table-tennis table on four chrome sled runners."""
    P = Parts()
    P.add("Body", [s.rbox((2.74, 1.52, .04), (0, 0, .74), r=.03, seg=1)])
    for x in (-1.0, 1.0):
        for y in (-.5, .5):
            P.add("Accent", [s.tube([(x - .2, y, .72), (x - .2, y, .04), (x + .2, y, .04), (x + .2, y, .72)], .02, res=1, smooth=False)])
    P.add("Eye", [s.rbox((.02, 1.72, .15), (0, 0, .82), r=.004, seg=1)])
    return P


def p_bean_bag(L):
    """A streamline ottoman: a stepped drum with a rounded top and chrome bands."""
    P = Parts()
    P.add("Body", la([(0, 0), (.38, 0), (.38, .06), (.45, .08), (.45, .5), (.42, .62), (.3, .68), (0, .69)], seg=12))
    P.add("Body", [s.rcyl(.455, .02, (0, 0, z), seg=12, r=0) for z in (.25, .31)])
    return P


def p_arcade_machine(L):
    """One-armed bandit: a rounded-top cabinet with three reel windows (the screen), a chrome lever on the side, a payout
    tray, speed lines."""
    P = Parts()
    P.add("Body", [s.rbox((.68, .8, 1.8), (0, .02, .9), r=.22, seg=2), s.rbox((.6, .7, .05), (0, .02, .025), r=.05, seg=1)])
    P.add("Eye", [s.rbox((.5, .02, .3), (0, -.385, 1.32), r=.05, seg=1)])
    P.add("Accent", [s.rbox((.03, .03, .3), (x, -.395, 1.32), r=.008, seg=1) for x in (-.08, .08)] + speed_lines(-.3, .3, -.385, (.85, .9, .95)))
    P.add("Accent", [s.rbox((.5, .2, .1), (0, -.44, .6), r=.04, seg=1), s.rbox((.4, .14, .06), (0, -.49, .56), r=.02, seg=1)])
    P.add("Accent", [s.tube([(.34, .0, 1.1), (.36, .0, 1.15), (.36, .0, 1.5)], .018, res=1, smooth=False), s.ball(.045, (.36, 0, 1.53), seg=8)])
    return P


def p_safe(L):
    """A streamline safe: radiused corners, a big chrome dial and a spoked handle, speed lines, a glowing lamp."""
    P = Parts()
    P.add("Body", [s.rbox((.8, .7, .96), (0, 0, .5), r=.14, seg=2), s.rbox((.7, .6, .04), (0, .02, .02), r=.05, seg=1)])
    P.add("Body", [s.rbox((.64, .03, .76), (0, -.35, .52), r=.1, seg=1)] + speed_lines(-.25, .25, -.37, (.2, .25)))
    P.add("Accent", la([(0, 0), (.1, 0), (.11, .02), (.07, .05), (0, .06)], axis="-Y", loc=(0, -.36, .6)))
    for k in range(3):
        a = k / 3 * math.tau + math.pi / 2
        P.add("Accent", [align(s.rcyl(.012, .14, seg=6, r=0), Vector((0, -.38, .38)), Vector((math.cos(a) * .12, -.38, .38 + math.sin(a) * .12)))])
    P.add("Accent", [s.rcyl(.03, .04, (0, -.38, .38), axis="Y", seg=8, r=0)])
    P.add("Eye", [s.rcyl(.025, .02, (.24, -.37, .82), axis="Y", seg=8, r=0)])
    return P


def p_floor_lamp(L):
    """Torchiere: a stepped chrome foot, a slim pole, a stepped uplight bowl over a teardrop bulb."""
    P = Parts()
    P.add("Body", [la([(0, 0), (.2, 0), (.2, .03), (.12, .05), (.12, .07), (0, .08)]), s.rcyl(.016, 1.5, (0, 0, .8), seg=6, r=0)])
    P.add("Accent", la([(0, 1.5), (.08, 1.5), (.18, 1.58), (.2, 1.62), (.2, 1.65), (0, 1.65)], seg=12))
    P.add("Eye", L.tear(.22, .07, (0, 0, 1.3), axis="Z", k=.5))
    return P


def p_desk_lamp(L):
    """Gooseneck lamp: a stepped round foot, a curved chrome neck, a teardrop shade over the bulb."""
    P = Parts()
    P.add("Body", [la([(0, 0), (.075, 0), (.075, .015), (.04, .03), (0, .035)]), s.tube([(0, 0, .03), (0, .03, .2), (0, -.06, .33), (0, -.15, .35)], .01, res=1)])
    P.add("Accent", L.tear_between(Vector((0, -.12, .37)), Vector((0, -.24, .28)), .06) + L.shade(.075, .04, .06, (0, -.16, .26)))
    P.add("Eye", [s.ball(.03, (0, -.16, .27), seg=8)] + [s.rcyl(.006, .06, (0, -.16, .3), seg=4, r=0)])
    return P


def p_ceiling_lamp(L):
    """A rounded chrome fixture with venetian slats across the glowing diffuser."""
    P = Parts()
    P.add("Body", [s.rbox((1.24, .66, .04), (0, 0, -.02), r=.2, seg=2, vertical_only=True)])
    P.add("Eye", [s.rbox((1.12, .54, .02), (0, 0, -.045), r=.16, seg=1, vertical_only=True)])
    P.add("Accent", [s.rbox((.035, .54, .025), (x, 0, -.065), r=.006, seg=1, rot=(0, math.radians(30), 0)) for x in [(-.48 + k * .12) for k in range(9)]])
    return P


def p_exit_sign(L):
    P = props.exit_sign(L)
    P.add("Body", [s.rbox((.4, .2, .03), (0, 0, z), r=.012, seg=1) for z in (-.07, .07)])
    return P


def p_hatch(L):
    """A round hatch with concentric chrome rings and speed-line grips."""
    P = Parts()
    P.add("Body", [s.rcyl(.76, .06, (0, 0, .03), seg=16, r=.01), s.rbox((1.0, .1, .04), (0, 0, .02), r=.02, seg=1)])
    P.add("Accent", [s.lathe([(r0, .055), (r0 + .04, .055), (r0 + .04, .075), (r0, .075)], seg=16) for r0 in (.2, .45, .66)])
    P.add("Accent", speed_lines(-.35, .35, 0, (.075,)) + [s.rbox((.5, .04, .02), (0, y, .072), r=.006, seg=1) for y in (-.08, .08)])
    P.add("Eye", la([(.76, 0), (.9, 0), (.9, .02), (.76, .02)], seg=16))
    return P


PROP_DESIGNS = {"desk": p_desk, "terminal": p_terminal, "chair": p_chair, "server_rack": p_server_rack, "cabinet": p_cabinet, "meeting_table": p_meeting_table, "plant": p_plant, "kitchen_counter": p_kitchen_counter, "shelf": p_shelf, "boxes": p_boxes, "water_cooler": p_water_cooler, "whiteboard": p_whiteboard, "sofa": p_sofa, "toilet_stall": p_toilet_stall, "partition": p_partition, "bookshelf": p_bookshelf, "printer": p_printer, "fridge": p_fridge, "coffee_machine": p_coffee_machine, "round_table": p_round_table, "armchair": p_armchair, "coffee_table": p_coffee_table, "tv_stand": p_tv_stand, "reception_desk": p_reception_desk, "trash_bin": p_trash_bin, "coat_rack": p_coat_rack, "lockers": p_lockers, "pallet": p_pallet, "wash_basin": p_wash_basin, "vending_machine": p_vending_machine, "ping_pong_table": p_ping_pong_table, "bean_bag": p_bean_bag, "arcade_machine": p_arcade_machine, "safe": p_safe, "floor_lamp": p_floor_lamp, "desk_lamp": p_desk_lamp, "ceiling_lamp": p_ceiling_lamp, "exit_sign": p_exit_sign, "hatch": p_hatch}


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "security_camera": security_camera, "sensor_lidar": lidar, "sensor_pir": pir,
}
DESIGNS.update(PROP_DESIGNS)
