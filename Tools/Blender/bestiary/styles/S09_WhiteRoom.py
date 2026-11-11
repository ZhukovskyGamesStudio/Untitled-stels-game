"""S09 WhiteRoom — «Белая комната»: crystal sentinels.

Language: every form is a cut gem — few-sided prisms, bipyramids and spikes with flat facets (flat shading, low
segment counts), no visible machinery: no wheels' hubs, no bolts, no pipes. Joints are small floating-looking
octahedra overlapping the limbs. The glow (Eye) lives inside the cracks: thin fins of light that split a crystal
along its ridge, equator rings, a lit core at the heart of a cluster. Tall, sharp, minimal, abstract.
"""
import math

import bmesh
from mathutils import Euler, Vector

import shapes as s
from lang import Lang, align
import designs as D

TAU = math.tau


class Style(Lang):
    name = "S09_WhiteRoom"
    seg = 8
    soft = 0.0
    flat = True
    sharp = 20.0
    hull_kind = "shard"
    eye_kind = "lens"
    wheel_kind = "facet"
    limb_kind = "tube"

    # ------------------------------------------------------------------ furniture: cut crystal blocks
    edge_kind = "chamfer"
    cushion_puff = 0.0
    foliage_kind = "facet"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """A board cut like glass: wide flat chamfers (overlapping its neighbours by a few mm)."""
        w, d, h = size
        return [s.rbox((w, d, h + .012), loc, r=min(min(size) * .3, .03), seg=1, rot=rot)]

    def carcass(self, size, loc, kick=0.06):
        """A cabinet as a crystal block narrowing to the top, on a darker sunk plinth."""
        w, d, h = size
        x, y, z = loc
        r = min(.04, min(w, d) * .15)
        if h >= 1.2:   # tall cabinets end in a cut crystal roof
            cap = min(.16, h * .1)
            out = [s.rbox((w, d, h - kick - cap + .01), (x, y, z + kick + (h - kick - cap) / 2 - .005), r=r, seg=1, taper=(.96, 1.0)),
                   s.rbox((w * .96, d, cap + .01), (x, y, z + h - cap / 2 - .005), r=r * .5, seg=1, taper=(.55, .35))]
        else:
            out = [s.rbox((w, d, h - kick + .01), (x, y, z + kick + (h - kick) / 2 - .005), r=r, seg=1, taper=(.93, 1.0))]
        if kick > 0:
            out += [s.rbox((w - .08, d - .1, kick + .01), (x, y + .02, z + (kick + .01) / 2), r=0, seg=1)]
        return out

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A crystal prism narrowing to the floor."""
        x, y, z = top
        foot = (x + splay[0], y + splay[1], max(0.0, z - length))
        return along(top, foot, [(0, thick * .7, thick * .7), (.15, thick * .8, thick * .8), (1, thick * .3, thick * .3)], n=4)

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Two crystal V trestles: at each end two shards spring from one point on the floor to the front and back
        corners of the top."""
        out = []
        for sx in (-1, 1):
            x = sx * (half_w - inset)
            foot = (x, 0, .025)
            for sy in (-1, 1):
                out += along(foot, (x, sy * (half_d - inset), top_z), [(0, thick * .4, thick * .4), (.8, thick * .8, thick * .8),
                                                                        (1, thick * .7, thick * .7)], n=4)
            out += gem([(0, thick * 1.4, thick * 1.4), (.03, thick * 1.2, thick * 1.2)], n=4, phase=math.pi / 4, loc=(x, 0, 0))
        return out

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """Upholstery as a faceted pillow: one wide chamfer all round."""
        return [s.rbox(size, loc, r=min(size) * .3, seg=1, rot=rot)]

    def foliage(self, centre, r, n=5, seed=1):
        """A crystal cluster: spikes springing up and out from one point."""
        cx, cy, cz = centre
        out = []
        for i in range(n + 2):
            a = (i * 2.399 + seed) % math.tau
            tilt = .35 + .25 * (i % 3) / 2
            ln = r * (1.0 + .3 * ((i * 5 + seed) % 3) / 2)
            tip = (cx + math.cos(a) * ln * math.sin(tilt) * 1.6, cy + math.sin(a) * ln * math.sin(tilt) * 1.6, cz + ln * math.cos(tilt))
            out += spike((cx, cy, cz - r * .3), tip, r * .22, n=4, mid=.6)
        return out

    def signature(self, model, P):
        if model == "attach_mic":   # the microphone becomes a crystal on a stalk
            P.add("Eye", gem([(.27, 0, 0), (.32, .05, .05), (.4, 0, 0)], n=4))
        elif model == "attach_seismic":
            P.add("Accent", octa((0, 0, .18), .035))
        elif model == "attach_eye":
            P.add("Accent", spike((-.06, -.06, .06), (-.1, -.02, .16), .025, n=3) + spike((.06, -.06, .06), (.1, -.02, .16), .025, n=3))
        return P


# =========================================================================== helpers


def gem(rings, n=6, phase=None, axis="Z", loc=(0, 0, 0), rot=(0, 0, 0)):
    """A cut crystal: n-gon sections (z, a, b[, x, y]) skinned with flat facets; a == 0 is a point.
    Default phase puts a vertex at the front (-y), so ridges face the camera."""
    if phase is None:
        phase = -math.pi / 2
    bm = bmesh.new()
    loops = []
    for r in rings:
        z, a, b = r[:3]
        cx, cy = (r[3], r[4]) if len(r) > 4 else (0.0, 0.0)
        if a < 1e-6:
            loops.append([bm.verts.new((cx, cy, z))])
        else:
            loops.append([bm.verts.new((cx + a * math.cos(phase + k / n * TAU), cy + b * math.sin(phase + k / n * TAU), z))
                          for k in range(n)])
    for lo_, up in zip(loops, loops[1:]):
        if len(lo_) == 1 and len(up) == 1:
            continue
        if len(lo_) == 1:
            for j in range(n):
                bm.faces.new((lo_[0], up[j], up[(j + 1) % n]))
        elif len(up) == 1:
            for j in range(n):
                bm.faces.new((lo_[j], lo_[(j + 1) % n], up[0]))
        else:
            for j in range(n):
                bm.faces.new((lo_[j], lo_[(j + 1) % n], up[(j + 1) % n], up[j]))
    if len(loops[0]) > 1:
        bm.faces.new(list(reversed(loops[0])))
    if len(loops[-1]) > 1:
        bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.place(bm, rot=s._axis_rot(axis))
    return [s.place(bm, loc, rot)]


def along(p0, p1, rings_frac, n=4, phase=None):
    """A gem whose ring z are fractions 0..1 of the length, laid from p0 to p1 (radii in metres)."""
    L = (Vector(p1) - Vector(p0)).length
    rr = [(-L / 2 + r[0] * L,) + tuple(r[1:]) for r in rings_frac]
    return [align(gem(rr, n, phase)[0], p0, p1)]


def spike(p0, p1, w, n=4, mid=.75, w2=None):
    """A crystal shard: base at p0, widest at `mid`, point at p1."""
    return along(p0, p1, [(0, w, w), (mid, w2 or w * 1.1, w2 or w * 1.1), (1, 0, 0)], n)


def bipyr(p0, p1, w, mid=.4, n=6, d=None):
    return along(p0, p1, [(0, 0, 0), (mid, w, d or w), (1, 0, 0)], n)


def prism(p0, p1, w0, w1, n=4):
    return along(p0, p1, [(0, w0, w0), (1, w1, w1)], n)


def octa(c, r, rz=None):
    """A joint: a small octahedron."""
    x, y, z = c
    rz = rz or r * 1.2
    return gem([(z - rz, 0, 0), (z, r, r), (z + rz, 0, 0)], n=4, phase=0.0, loc=(x, y, 0))


def fin(rings, n=6, thick=.022, grow=1.05, phase=None, axis="Z", loc=(0, 0, 0)):
    """A crack of light: a thin slab in the x = 0 plane with the outline of the crystal, standing a little proud of
    its front and back ridges with a flat face (a knife edge would vanish when seen head-on)."""
    k = math.sqrt(2)
    rr = [(r[0], thick * k if r[1] > 1e-6 else 0, r[2] * grow * k) + tuple(r[3:]) for r in rings]
    return gem(rr, 4, math.pi / 4, axis, loc)


def ring(z, a, b, n=6, h=.03, grow=1.06, phase=None, loc=(0, 0, 0)):
    """An equator of light around a crystal at height z."""
    return gem([(z - h / 2, a * grow, b * grow), (z + h / 2, a * grow, b * grow)], n, phase, loc=loc)


def slab(size, loc, taper=(1, 1), rot=(0, 0, 0)):
    return [s.rbox(size, loc, r=0.0, seg=1, taper=taper, rot=rot)]


# =========================================================================== walkers

def guard(L):
    """The sentinel: an inverted hexagonal shard for a torso cracked with light down its front ridge, a diamond head
    with a glowing equator, one great shard rising off the left shoulder (it reads from above), long blade shards
    for forearms reaching past the hands, faceted legs on wedge feet."""
    P = D.Parts()
    torso = [(.96, 0, 0), (1.2, .2, .14), (1.56, .4, .22), (1.7, .26, .15), (1.76, 0, 0)]
    P.add("Body", gem(torso, n=6))
    P.add("Eye", fin([(1.1, .1, .1), (1.56, .4, .22), (1.7, .26, .15)], n=6, grow=1.06))
    P.add("Accent", gem([(.82, .12, .1), (.92, .23, .14), (1.04, .1, .08)], n=6))
    P.add("Accent", prism((0, 0, 1.7), (0, 0, 1.8), .07, .06, n=4))
    head = [(1.73, 0, 0), (1.86, .14, .22), (2.04, 0, 0)]
    P.add("Accent", gem(head, n=4))
    P.add("Eye", ring(1.86, .14, .22, n=4, h=.04, grow=1.04))
    P.add("Accent", spike((-.28, .02, 1.5), (-.62, .12, 2.15), .13, n=4, mid=.55))
    P.add("Accent", spike((.3, 0, 1.52), (.55, .05, 1.8), .08, n=4))
    for sx in (-1, 1):
        sh, el, wr = (sx * .44, 0, 1.55), (sx * .5, -.02, 1.2), (sx * .5, -.16, .62)
        P.add("Accent", octa(sh, .07) + octa(el, .06))
        P.add("Body", prism(sh, el, .06, .05, n=4))
        P.add("Body", along(el, wr, [(0, .05, .05), (.3, .1, .13), (1, 0, 0)], n=4))
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, octa((x, 0, D.GUARD_HIP_Z), .08))
        P.add(leg, along((x, 0, .47), (x, 0, .9), [(0, .055, .055), (.55, .11, .12), (1, .07, .07)], n=4))
        P.add(leg, octa((x, 0, D.GUARD_KNEE), .07))
        P.add(leg, along((x, 0, .07), (x, 0, .44), [(0, .045, .045), (.7, .09, .11), (1, .055, .055)], n=4))
        P.add(leg, slab((.15, .32, .09), (x, -.06, .045), taper=(.55, .45)))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hound(L):
    """One long hexagonal prism for a body with a crack of light along its spine, a long needle snout, a single
    swept crest instead of ears, a shard tail."""
    P = D.Parts()
    body = [(-.52, .05, .05), (-.4, .15, .15), (.32, .18, .19), (.46, .12, .13), (.5, 0, 0)]
    P.add("Body", gem(body, n=6, phase=0.0, axis="-Y", loc=(0, 0, .83)))
    P.add("Eye", gem([(-.4, .03, .21), (.32, .03, .27), (.44, .03, .18)], n=4, phase=math.pi / 4, axis="-Y", loc=(0, 0, .83)))
    P.add("Body", prism((0, -.4, .9), (0, -.58, 1.0), .08, .07, n=4))
    head = [(0, .11, .1), (.12, .12, .1), (.6, .015, .015), (.63, 0, 0)]
    P.add("Body", gem(head, n=4, phase=0.0, axis="-Y", loc=(0, -.52, 1.0)))
    for sx in (-1, 1):
        P.add("Eye", gem([(.1, .0, .0), (.17, .035, .035), (.26, 0, 0)], n=4, phase=0.0, axis="-Y", loc=(sx * .1, -.52, 1.02)))
    P.add("Body", spike((0, -.6, 1.05), (0, -.3, 1.32), .06, n=3, mid=.4, w2=.09))
    P.add("Eye", octa((0, -1.15, 1.0), .03))
    P.add("Body", spike((0, .44, .9), (0, .82, 1.15), .05, n=3))
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        P.add("Body", octa((x * .85, y, .72), .11))
        leg = f"AccentLeg{i}"
        P.add(leg, octa((x, y, D.HOUND_HIP), .07))
        P.add(leg, along((x, y, .32), (x, y, .64), [(0, .04, .04), (.55, .07, .09), (1, .06, .06)], n=4))
        P.add(leg, octa((x, y, D.HOUND_KNEE), .05))
        P.add(leg, along((x, y, .04), (x, y, .31), [(0, .03, .03), (1, .045, .045)], n=4))
        P.add(leg, slab((.08, .16, .05), (x, y - .03, .025), taper=(.5, .4)))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """A geode: a low hexagonal pyramid with a lit crack ring, a cluster of small lit crystals in front, and eight
    needle legs."""
    P = D.Parts()
    P.add("Body", gem([(.06, .16, .2), (.15, .24, .3), (.24, .14, .18), (.32, 0, 0)], n=6, loc=(0, .05, 0)))
    P.add("Eye", ring(.15, .24, .3, n=6, h=.025, grow=1.04, loc=(0, .05, 0)))
    for dx, dz, r in ((-.035, .035, .035), (.035, .035, .035), (0, .0, .045)):
        P.add("Eye", gem([(-r, 0, 0), (0, r, r * .7), (r * 1.3, 0, 0)], n=4, phase=0.0, axis="-Y", loc=(dx, -.18, .17 + dz)))
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", octa(hip, .035) + along(hip, knee, [(0, .025, .025), (.6, .04, .045), (1, .02, .02)], n=4)
              + octa(knee, .035))
        P.add(f"AccentLeg{i}Lower", along(knee, foot + Vector((0, 0, -.02)), [(0, .028, .028), (.3, .032, .032), (1, 0, 0)], n=4))
    return P


# =========================================================================== wheeled and tracked

def listener(L):
    """A hexagonal plinth on four faceted wheels; on it a crystal pillar opens two fans of shards — the ears — with
    a lit gem at each fan's root."""
    P = D.Parts()
    W = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .22, .12))
        P.add("Body", prism((c[0] * .3, c[1], .22), (c[0] * 1.0, c[1], .22), .05, .05, n=4))
    P.add("Body", gem([(.24, .3, .48), (.42, .36, .54), (.54, .26, .4)], n=6, phase=0.0))
    P.add("Body", gem([(.5, .14, .14), (.95, .12, .12), (1.1, 0, 0)], n=4))
    P.add("Eye", ring(.5, .3, .45, n=6, h=.025, grow=1.0, phase=0.0))
    for sx in (-1, 1):
        root = Vector((sx * .1, 0, .82))
        for k, (ang, ln) in enumerate(((20, .5), (45, .58), (70, .45))):
            a = math.radians(ang)
            tip = root + Vector((sx * math.sin(a) * ln, .0, math.cos(a) * ln * .9 + .05))
            P.add("Body", spike(root, tip, .045, n=4, mid=.6, w2=.075))
        P.add("Eye", octa(tuple(root + Vector((sx * .05, 0, .02))), .07))
    P.add("Eye", gem([(.0, .1, .03), (.03, .1, .03)], n=4, phase=0.0, axis="-Y", loc=(0, -.455, .42)))
    return P


def worker(L):
    """One low hexagonal slab on faceted wheels; two shard prongs rise from its nose and hold a cut geode for a
    crate, split by a crack of light; a small lit gem looks ahead."""
    P = D.Parts()
    W = [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .12, .1))
        P.add("Body", prism((c[0] * .4, c[1], .13), (c[0] * 1.0, c[1], .12), .04, .04, n=4))
    P.add("Body", gem([(.13, .3, .38), (.22, .34, .43), (.36, .3, .38), (.42, .18, .24)], n=6, phase=0.0, loc=(0, .03, 0)))
    P.add("Body", spike((-.12, .32, .38), (-.2, .45, .78), .07, n=4) + spike((.1, .3, .38), (.16, .42, .66), .06, n=4))
    for sx in (-1, 1):
        P.add("Body", along((sx * .17, -.2, .38), (sx * .21, -.18, 1.08), [(0, .04, .04), (.7, .05, .05), (1, 0, 0)], n=4))
    P.add("Accent", gem([(.93, .15, .14), (.98, .24, .22), (1.27, .24, .22), (1.36, .1, .09)], n=6, phase=0.0, loc=(0, -.15, 0)))
    P.add("Eye", ring(1.12, .24, .22, n=6, h=.035, grow=1.04, phase=0.0, loc=(0, -.15, 0)))
    P.add("Eye", gem([(-.03, 0, 0), (0, .07, .05), (.08, 0, 0)], n=4, phase=0.0, axis="-Y", loc=(0, -.38, .44)))
    P.add("Body", prism((0, -.25, .36), (0, -.36, .44), .05, .05, n=4))
    return P


def sweeper(L):
    """A low hexagonal crystal plate with a faceted top and a lit equator, spinning a hexagonal lidar ring under it."""
    P = D.Parts()
    W = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", L.wheel(c, .08, .06))
        x, y, _ = c
        P.add("Accent", prism((x - .06, y, .08), (x + .06, y, .08), .025, .025, n=4))
        for sx in (-1, 1):
            P.add("Accent", slab((.025, .07, .12), (x + sx * .045, y, .13)))
    P.add("Accent", gem([(.14, .3, .3), (.19, .3, .3)], n=6, phase=0.0))
    P.add("AccentLidar", gem([(.18, .33, .33), (.262, .33, .33)], n=6, phase=0.0) + slab((.1, .06, .05), (0, -.29, .22)))
    P.add("Body", gem([(.257, .38, .38), (.33, .44, .44), (.42, .3, .3), (.5, 0, 0)], n=6, phase=0.0))
    P.add("Eye", ring(.33, .44, .44, n=6, h=.022, grow=1.02, phase=0.0))
    P.add("Eye", gem([(-.03, 0, 0), (0, .05, .04), (.06, 0, 0)], n=4, phase=0.0, axis="-Y", loc=(0, -.36, .36)))
    for sx in (-1, 1):
        c = (sx * .27, -.25, .012)
        P.add("Accent", gem([(.0, .12, .12), (.024, .1, .1)], n=6, loc=(c[0], c[1], 0)) + prism((c[0], c[1], .02), (sx * .05, -.3, .15), .025, .02))
    return P


def thumper(L):
    """A massive cluster of three crystal blocks on two hexagonal prism treads; two crystal pillars stamp lit
    hexagonal pads into the floor."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", gem([(-.7, .1, .2), (-.55, .12, .29), (.55, .12, .29), (.7, .1, .2)], n=6, phase=0.0, axis="-Y",
                            loc=(sx * .55, 0, .252)))
        P.add("Body", prism((sx * .2, .3, .4), (sx * .6, .3, .4), .06, .06, n=4) + prism((sx * .2, -.25, .4), (sx * .6, -.25, .4), .06, .06, n=4))
    P.add("Body", gem([(.38, .44, .48), (.75, .5, .55), (1.0, .34, .4), (1.18, 0, 0)], n=6, phase=0.0, loc=(0, .08, 0)))
    P.add("Body", spike((-.15, .25, .85), (-.5, .55, 1.4), .15, n=5))
    P.add("Body", spike((.15, .3, .85), (.45, .6, 1.25), .13, n=5))
    P.add("Body", spike((0, .1, .95), (0, .3, 1.55), .16, n=5))
    P.add("Eye", ring(.75, .5, .55, n=6, h=.03, grow=1.02, phase=0.0, loc=(0, .08, 0)))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Accent", prism((x * .8, -.35, .65), (x, -.85, .6), .06, .05, n=4) + octa((x, -.85, .6), .08))
        P.add("Body", along((x, -.85, .6), (x, -.85, .025), [(0, .06, .06), (.7, .09, .09), (1, .12, .12)], n=6))
        P.add("Eye", gem([(.0, .18, .18), (.04, .18, .18)], n=6, phase=0.0, loc=(x, -.85, 0)))
    P.add("Eye", gem([(-.02, .12, .03), (.02, .12, .03)], n=4, phase=0.0, axis="-Y", loc=(0, -.39, .55)))
    return P


# =========================================================================== tall

def sorter(L):
    """An obelisk on a hexagonal plinth, carrying a big brilliant-cut colour gem on its face."""
    P = D.Parts()
    P.add("Accent", gem([(0, .3, .3), (.06, .3, .3), (.1, .22, .22)], n=6, phase=0.0))
    P.add("Body", gem([(.08, .13, .13), (1.3, .08, .08), (1.5, .07, .07), (1.66, 0, 0)], n=4, phase=math.pi / 4))
    P.add("Accent", gem([(0, .25, .25), (.04, .27, .27), (.08, .22, .22)], n=8, phase=0.0, axis="-Y", loc=(0, -.04, 1.25)))
    P.add("Eye", gem([(0, .21, .21), (.035, .19, .19), (.08, .1, .1), (.09, 0, 0)], n=8, phase=0.0, axis="-Y", loc=(0, -.115, 1.25)))
    return P


def hunter(L):
    """The spike: a 2.3 m crystal spindle balanced on its point over a lit hexagon, a crown of shards on its
    shoulders and two long blade shards held forward; a crack of light down its front."""
    P = D.Parts()
    body = [(.02, 0, 0), (1.0, .2, .17), (1.7, .36, .3), (2.05, .24, .2), (2.32, 0, 0)]
    P.add("Body", gem(body, n=5))
    P.add("Eye", fin([(.4, .07, .06), (1.0, .2, .17), (1.7, .36, .3), (2.05, .24, .2)], n=5, grow=1.04))
    P.add("Eye", gem([(.0, .3, .3), (.04, .28, .28)], n=6, phase=0.0))
    P.add("Eye", octa((0, -.3, 1.92), .06))
    for sx in (-1, 1):
        P.add("Accent", spike((sx * .25, .05, 1.8), (sx * .62, .15, 2.3), .08, n=4))
        P.add("Accent", spike((sx * .15, .2, 1.85), (sx * .3, .5, 2.25), .06, n=4))
        sh, el, tip = (sx * .38, 0, 1.68), (sx * .55, .02, 1.28), (sx * .45, -.85, .95)
        P.add("Accent", octa(sh, .08) + octa(el, .06))
        P.add("Body", prism(sh, el, .055, .045, n=4))
        P.add("Accent", along(el, tip, [(0, .035, .05), (.25, .04, .1), (1, 0, 0)], n=4))
    return P


# =========================================================================== devices

def drone(L):
    """One large flat diamond shard whose points reach into the four hexagonal ducts, a crack of light along it, a
    lit gem under the nose."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", gem([(-.09, 0, 0), (0, .52, .52), (.07, 0, 0)], n=4, phase=math.pi / 4))
    P.add("Eye", along((0, -.34, 0), (0, .34, 0), [(0, 0, 0), (.5, .035, .085), (1, 0, 0)], n=4)
          + along((-.34, 0, 0), (.34, 0, 0), [(0, 0, 0), (.5, .035, .085), (1, 0, 0)], n=4))
    for i, (x, y) in enumerate(rot):
        for k in range(6):
            a0, a1 = k / 6 * TAU, (k + 1) / 6 * TAU
            P.add("Body", prism((x + .2 * math.cos(a0), y + .2 * math.sin(a0), .04), (x + .2 * math.cos(a1), y + .2 * math.sin(a1), .04),
                                .025, .025, n=4))
        P.add("Body", prism((x, y, -.01), (x, y, .05), .03, .03, n=6))
        P.add("Body", prism((x * .7, y * .7, 0), (x, y, .02), .03, .025, n=4))
        P.add(f"AccentRotor{i}", L.rotor((x, y, .065), .32))
    P.add("Eye", gem([(-.09, 0, 0), (-.04, .05, .05), (.01, 0, 0)], n=4, phase=0.0, loc=(0, -.2, -.03)))
    P.add("Body", prism((0, -.13, -.03), (0, -.2, -.06), .03, .02, n=4))
    return P


def turret(L):
    """A cluster of three crystal pillars with a lit crack ring; the head is a long bipyramid lance (it turns)."""
    P = D.Parts()
    for k in range(3):
        a = .5 + k * TAU / 3
        base = (math.cos(a) * .2, math.sin(a) * .2, 0)
        P.add("Body", along(base, (math.cos(a) * .05, math.sin(a) * .05, 1.12), [(0, .1, .1), (.7, .07, .07), (1, .04, .04)], n=4))
        P.add("Eye", octa((math.cos(a) * .27, math.sin(a) * .27, .05), .05))
        P.add("Body", prism(base, (math.cos(a) * .27, math.sin(a) * .27, .02), .05, .04, n=4))
    P.add("Eye", gem([(1.0, .13, .13), (1.04, .13, .13)], n=6, phase=0.0))
    P.add("Body", gem([(.96, .1, .1), (1.1, .12, .12), (1.14, .08, .08)], n=6, phase=0.0))
    P.add("AccentHead", octa((0, 0, 1.22), .1, .1))
    P.add("AccentHead", gem([(-.3, 0, 0), (-.05, .12, .13), (.15, .1, .1), (.62, 0, 0)], n=6, phase=0.0, axis="-Y", loc=(0, 0, 1.3)))
    for sx in (-1, 1):
        P.add("AccentHead", spike((sx * .08, .0, 1.32), (sx * .3, .2, 1.42), .04, n=3))
    return P


def vacuum(L):
    """A flat hexagonal crystal; its room face (-z) rises into a faceted pyramid split by two cracks of light."""
    P = D.Parts()
    P.add("Body", gem([(0, .3, .3), (.04, .36, .36), (.08, .36, .36), (.11, .25, .25)], n=6, phase=0.0))
    P.add("Accent", gem([(-.05, 0, 0), (.0, .3, .3), (.01, .3, .3)], n=6, phase=math.pi / 6))
    for a in (0.0, math.pi / 3, -math.pi / 3):
        P.add("Eye", [s.place(gem([(-.055, 0, 0), (0, .022, .32), (.005, .022, .32)], n=4, phase=math.pi / 4)[0], rot=(0, 0, a))])
    return P


def lidar(L):
    """A crystal post on a hexagonal plinth; the hexagonal drum on top turns."""
    P = D.Parts()
    P.add("Body", gem([(0, .17, .17), (.03, .17, .17), (.07, .1, .1)], n=6, phase=0.0))
    P.add("Body", prism((0, 0, .05), (0, 0, .4), .06, .045, n=4))
    P.add("Eye", ring(.35, .055, .055, n=4, h=.03, grow=1.15))
    P.add("AccentHead", gem([(.385, .08, .08), (.41, .1, .1), (.48, .1, .1), (.52, .07, .07)], n=6, phase=0.0))
    P.add("AccentHead", gem([(-.02, 0, 0), (0, .04, .03), (.04, 0, 0)], n=4, phase=0.0, axis="-Y", loc=(0, -.09, .45)))
    return P


def security_camera(L):
    """A crystal wedge on a shard arm, a lit gem lens at its point."""
    P = D.Parts()
    P.add("Body", slab((.15, .06, .22), (0, .27, 0)))
    P.add("Accent", prism((0, .27, 0), (0, .08, -.02), .03, .025, n=4))
    P.add("Accent", gem([(-.18, .07, .06), (0, .08, .08), (.12, .06, .06), (.18, .03, .03)], n=4, phase=0.0, axis="-Y", loc=(0, .02, -.02)))
    P.add("Eye", gem([(0, .04, .04), (.04, 0, 0)], n=4, phase=0.0, axis="-Y", loc=(0, -.155, -.02)))
    return P


def laser_emitter(L):
    """A square crystal post with a lit crack down its front and a pyramid tip."""
    P = D.Parts()
    P.add("Body", gem([(0, .085, .085), (1.5, .075, .075), (1.62, 0, 0)], n=4, phase=math.pi / 4))
    P.add("Eye", slab((.04, .04, 1.4), (0, -.055, .85)))
    return P


def plate(L):
    """A crystal floor tile with two lit cracks running corner to corner."""
    P = D.Parts()
    S, h = 1.1, .012
    P.add("Body", slab((S - .06, S - .06, h), (0, 0, h / 2), taper=(.98, .98)))
    P.add("Accent", slab((S, S, h * .6), (0, 0, h * .3)))
    e = h + .004
    for a in (math.pi / 4, -math.pi / 4):
        P.add("Eye", slab((S * 1.3, .035, e), (0, 0, e / 2), rot=(0, 0, a)))
    return P


def pir(L):
    """A hexagonal crystal plate on the wall with a cut gem looking out."""
    P = D.Parts()
    P.add("Body", gem([(0, .09, .09), (.06, .09, .09)], n=6, phase=0.0, axis="Y", loc=(0, .02, 0)))
    P.add("Eye", gem([(0, .065, .065), (.035, .05, .05), (.07, 0, 0)], n=6, phase=0.0, axis="-Y", loc=(0, .03, 0)))
    return P


# =========================================================================== furniture and level pieces

def extrude_x(pts, x0, x1):
    """A solid from a (y, z) profile extruded along x from x0 to x1."""
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


def crack(points, w=.03):
    """A lit crack: a chain of thin diamond prisms through the points."""
    out = []
    for a, b in zip(points, points[1:]):
        out += along(a, b, [(0, w * .4, w * .4), (.5, w * .6, w * .6), (1, w * .4, w * .4)], n=4)
    return out


def plant(L):
    """A crystal cluster growing out of a hexagonal pot."""
    P = D.Parts()
    P.add("Accent", gem([(0, .17, .17), (.38, .21, .21), (.42, .22, .22), (.44, .18, .18)], n=6, phase=0.0))
    P.add("Body", spike((0, 0, .38), (0, 0, 1.4), .08, n=5, mid=.45))
    for k in range(8):
        a = k * math.pi / 4 + .2
        tip = (math.cos(a) * .3, math.sin(a) * .32, .95 + .12 * (k % 3))
        P.add("Body", spike((math.cos(a) * .04, math.sin(a) * .04, .4), tip, .05 + .01 * (k % 2), n=4, mid=.55))
    return P


def floor_lamp(L):
    """A crystal needle on a hexagonal plinth; a crown of shards opens round a lit gem at the top."""
    P = D.Parts()
    P.add("Body", gem([(0, .18, .18), (.03, .18, .18), (.05, .12, .12)], n=6, phase=0.0))
    P.add("Body", along((0, 0, .04), (0, 0, 1.36), [(0, .045, .045), (.6, .03, .03), (1, .035, .035)], n=4))
    P.add("Eye", gem([(1.33, 0, 0), (1.36, .14, .14), (1.39, .12, .12), (1.45, 0, 0)], n=6, phase=0.0))
    for k in range(6):
        a = k * math.tau / 6
        P.add("Accent", spike((math.cos(a) * .09, math.sin(a) * .09, 1.3), (math.cos(a) * .185, math.sin(a) * .185, 1.65), .025, n=3, mid=.6))
    P.add("Accent", gem([(1.28, .04, .04), (1.33, .11, .11), (1.35, .1, .1)], n=6, phase=0.0))
    return P


def desk_lamp(L):
    """A shard arm on a hexagonal foot; an inverted crystal pyramid over the lit gem."""
    P = D.Parts()
    P.add("Body", gem([(0, .075, .075), (.025, .07, .07), (.035, .05, .05)], n=6, phase=0.0))
    P.add("Body", along((0, .02, .02), (0, .02, .3), [(0, .018, .018), (1, .012, .012)], n=4)
          + along((0, .02, .29), (0, -.13, .345), [(0, .012, .012), (1, .012, .012)], n=4))
    P.add("Accent", gem([(.27, .03, .03), (.33, .075, .075), (.37, 0, 0)], n=6, phase=0.0, loc=(0, -.16, 0)))
    P.add("Eye", octa((0, -.16, .27), .03))
    return P


def vending_machine(L):
    """A crystal monolith narrowing upwards and cut to a ridge; the window is a lit facet split by a crack."""
    P = D.Parts()
    P.add("Body", [s.rbox((.9, .8, 1.76), (0, 0, .88), r=.04, seg=1, taper=(.92, .9))])
    P.add("Body", gem([(1.75, .4, .35), (1.9, .2, .05)], n=4, phase=math.pi / 4))
    P.add("Eye", [s.place(s.rbox((.56, .03, 1.08), r=.012, seg=1), (-.1, -.385, 1.12), (math.radians(-1.6), 0, 0))])
    P.add("Eye", crack([(.3, -.37, .3), (.36, -.37, .8), (.28, -.36, 1.3), (.34, -.36, 1.7)]))
    P.add("Accent", [s.rbox((.6, .04, .15), (-.1, -.4, .27), r=.015, seg=1), s.rbox((.12, .04, .3), (.32, -.385, 1.0), r=.015, seg=1)])
    return P


def server_rack(L):
    """A crystal slab narrowing upwards with a lit crack zig-zagging down its face."""
    P = D.Parts()
    P.add("Body", [s.rbox((.7, 1.0, 1.96), (0, 0, .98), r=.04, seg=1, taper=(.9, .95))])
    pts = [(-.18, 1.85), (.12, 1.45), (-.1, 1.0), (.15, .55), (-.08, .17)]
    P.add("Eye", crack([(x, -(.5 - .025 * z / 1.96) - .004, z) for x, z in pts], w=.05))
    for z in (.4, .8, 1.2, 1.6):
        P.add("Eye", [s.rbox((.12, .03, .03), (.22, -(.5 - .025 * z / 1.96) - .004, z), r=0, seg=1)])
    return P


def water_cooler(L):
    """A hexagonal crystal column carrying a cut gem for a bottle."""
    P = D.Parts()
    P.add("Body", gem([(0, .17, .17), (.95, .15, .15), (.99, .12, .12)], n=6, phase=0.0))
    P.add("Body", gem([(-.04, .06, .03), (.04, .06, .03)], n=4, phase=math.pi / 4, axis="-Y", loc=(0, -.13, .62)))
    P.add("Eye", gem([(.97, .08, .08), (1.05, .15, .15), (1.32, .15, .15), (1.445, 0, 0)], n=6, phase=0.0))
    return P


def safe(L):
    """A cut crystal cube; the door is an octagon framed by lit seams, an octahedron for a dial."""
    P = D.Parts()
    P.add("Body", [s.rbox((.8, .7, .96), (0, .0, .49), r=.06, seg=1)])
    P.add("Body", gem([(0, .3, .3), (.04, .27, .27)], n=8, phase=math.pi / 8, axis="-Y", loc=(0, -.34, .5)))
    pts = [(math.cos(math.pi / 8 + k * math.tau / 8) * .325, math.sin(math.pi / 8 + k * math.tau / 8) * .325) for k in range(9)]
    P.add("Eye", crack([(x, -.355, .5 + z) for x, z in pts], w=.03))
    P.add("Accent", octa((0, -.39, .5), .07, .07))
    P.add("Eye", octa((-.25, -.36, .82), .035))
    return P


def bean_bag(L):
    """A geode pouf: a squat faceted boulder."""
    P = D.Parts()
    P.add("Body", gem([(0, .38, .38), (.22, .45, .45), (.52, .37, .37), (.69, .14, .14)], n=7, phase=0.3))
    return P


def chair(L):
    """A crystal throne: a faceted seat on an hourglass prism, a tall shard for a back."""
    P = D.Parts()
    P.add("Body", [s.rbox((.5, .48, .09), (0, -.02, .47), r=.03, seg=1)])
    P.add("Body", gem([(.5, .2, .035), (.75, .22, .04), (1.068, 0, 0)], n=4, phase=0.0, loc=(0, .22, 0), rot=(0, 0, 0)))
    P.add("Accent", gem([(.008, .28, .28), (.04, .27, .27), (.08, .08, .08), (.25, .045, .045), (.44, .09, .09)], n=6, phase=0.0))
    return P


def round_table(L):
    """A hexagonal gem top on an hourglass crystal pedestal."""
    P = D.Parts()
    P.add("Body", gem([(.71, .44, .51), (.735, .5, .577), (.76, .46, .53)], n=6, phase=0.0))
    P.add("Accent", gem([(0, .26, .26), (.03, .26, .26), (.36, .05, .05), (.72, .2, .2)], n=6, phase=0.0))
    return P


def trash_bin(L):
    """A hexagonal crystal cup with a cut rim."""
    P = D.Parts()
    P.add("Body", gem([(0, .15, .15), (.41, .19, .19)], n=6, phase=0.0))
    P.add("Accent", gem([(.4, .2, .2), (.445, .19, .19)], n=6, phase=0.0))
    return P


def coat_rack(L):
    """A shard tree: a crystal trunk on a hexagonal foot, three shards for hooks, two faceted coats."""
    P = D.Parts()
    P.add("Accent", gem([(0, .24, .24), (.03, .22, .22)], n=6, phase=0.0))
    P.add("Accent", along((0, 0, .02), (0, 0, 1.8), [(0, .04, .04), (.85, .025, .025), (1, 0, 0)], n=4))
    for a in (0.0, 2.1, 4.2):
        P.add("Accent", spike((0, 0, 1.6), (math.cos(a) * .22, math.sin(a) * .22, 1.76), .02, n=3))
    P.add("Body", L.cushion((.32, .12, .6), (.12, 0, 1.36)) + L.cushion((.3, .12, .55), (-.08, .1, 1.38), rot=(0, 0, 2.1)))
    return P


def coffee_machine(L):
    """A crystal block narrowing up, a prism spout and a lit gem display."""
    P = D.Parts()
    P.add("Body", [s.rbox((.34, .4, .5), (0, -.02, .25), r=.03, seg=1, taper=(.85, .8))])
    P.add("Accent", along((0, -.18, .3), (0, -.2, .14), [(0, .035, .035), (1, .015, .015)], n=4)
          + [s.rbox((.22, .12, .03), (0, -.17, .015), r=0, seg=1)])
    P.add("Eye", gem([(0, .05, .03), (.03, 0, 0)], n=4, phase=0.0, axis="-Y", loc=(0, -.185, .4)))
    return P


def ceiling_lamp(L):
    """A crystal panel whose diffuser is a long faceted prism of light."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.24, .66, .045), (0, 0, -.0225), r=.015, seg=1)])
    P.add("Eye", gem([(-.082, .79, .28), (-.04, .82, .4)], n=4, phase=math.pi / 4))
    P.add("Accent", [s.rbox((1.2, .05, .03), (0, y, -.055), r=0, seg=1) for y in (-.3, .3)])
    return P


def hatch(L):
    """An octagonal crystal lid with shard grips, a lit octagon round it."""
    P = D.Parts()
    P.add("Body", gem([(0, .84, .84), (.05, .81, .81), (.07, .71, .71)], n=8, phase=math.pi / 8))
    for y in (-.25, 0, .25):
        P.add("Accent", along((-.5, y, .055), (.5, y, .055), [(0, .02, .015), (.5, .04, .022), (1, .02, .015)], n=4))
    P.add("Eye", gem([(0, .975, .975), (.02, .94, .94)], n=8, phase=math.pi / 8))
    return P


def arcade_machine(L):
    """An angular crystal cabinet: the profile cut in straight facets, a lit screen facet and a lit crack."""
    P = D.Parts()
    prof = [(.38, 0), (.38, 1.78), (-.05, 1.8), (-.22, 1.6), (-.3, 1.12), (-.52, .98), (-.42, .86), (-.24, .78), (-.18, 0)]
    P.add("Body", [extrude_x(prof, -.34, .34)])
    P.add("Eye", [s.rbox((.54, .025, .46), (0, -.265, 1.36), r=0, seg=1, rot=(math.radians(-14), 0, 0))])
    P.add("Accent", [s.rbox((.62, .2, .04), (0, -.42, .93), r=0, seg=1, rot=(math.radians(-25), 0, 0))])
    P.add("Eye", crack([(-.345, .3, .2), (-.345, .05, .7), (-.345, .2, 1.2), (-.345, .0, 1.7)]))
    return P


def extrude_y(pts, y0, y1):
    """A solid from an (x, z) outline extruded along y (panes, panels)."""
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


def sofa(L):
    """A faceted crystal bench with a comb of shards leaning back for a backrest, blocky cut arms."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.86, .7, .38), (0, -.05, .2), r=.1, seg=1)])
    for k in range(6):
        x = -.78 + k * .312
        P.add("Body", spike((x, .2, .3), (x * 1.02, .36, .885), .1, n=4, mid=.45))
    for x in (-1.0, 1.0):
        P.add("Accent", [s.rbox((.2, .84, .62), (x, 0, .31), r=.06, seg=1, taper=(.6, .85))])
    return P


def armchair(L):
    """A faceted crystal seat with two leaning shards for a back, blocky cut arms."""
    P = D.Parts()
    P.add("Body", [s.rbox((.62, .66, .42), (0, -.04, .22), r=.1, seg=1)])
    for x in (-.15, .15):
        P.add("Body", spike((x, .18, .35), (x * 1.2, .32, .865), .11, n=4, mid=.45))
    for x in (-.37, .37):
        P.add("Accent", [s.rbox((.14, .74, .58), (x, 0, .29), r=.05, seg=1, taper=(.6, .85))])
    return P


def bookshelf(L):
    """A case of crystal slabs leaning in at the top; the books are standing crystals with cut points."""
    P = D.Parts()
    for x in (-.48, .48):
        P.add("Body", [s.rbox((.04, .35, 2.0), (x, 0, 1.0), r=.012, seg=1)])
    for h in (.03, .5, 1.0, 1.5, 1.98):
        P.add("Body", [s.rbox((1.0, .35, .04), (0, 0, h), r=.012, seg=1)])
    P.add("Body", [s.rbox((.96, .02, 1.96), (0, .165, 1.0), r=0, seg=1)])
    for i, h in enumerate((.05, .52, 1.02, 1.52)):
        x = -.42
        for k in range(6 + i % 2):
            w = .07 + .03 * ((k + i) % 3)
            hh = .3 - .05 * ((k * 7 + i) % 3)
            P.add("Accent", [s.rbox((w, .24, hh), (x + w / 2, .02, h + hh / 2 - .01), r=w * .3, seg=1, taper=(.5, .7))])
            x += w + .012
    return P


def boxes(L):
    """Cut crystal cubes: every corner cut away."""
    P = D.Parts()
    P.add("Accent", [s.rbox((.6, .6, .5), (0, 0, .25), r=.12, seg=1), s.rbox((.5, .5, .41), (.05, -.05, .69), r=.1, seg=1),
                     s.rbox((.45, .45, .4), (.55, .1, .2), r=.1, seg=1)])
    return P


def partition(L):
    """A crystal pane with a broken, peaked top edge, standing in two cut feet."""
    P = D.Parts()
    pts = [(-.78, .05), (.78, .05), (.78, 1.0), (.45, 1.25), (.1, 1.1), (-.3, 1.26), (-.78, 1.05)]
    P.add("Body", [extrude_y(pts, -.018, .018)])
    for x in (-.65, .65):
        P.add("Accent", [s.rbox((.3, .062, .1), (x, 0, .05), r=.025, seg=1, taper=(.6, .8))])
    return P


def whiteboard(L):
    """A crystal pane with a peaked top on two shard legs."""
    P = D.Parts()
    pts = [(-.9, 1.02), (.9, 1.02), (.9, 1.85), (.5, 2.0), (-.2, 1.9), (-.9, 1.97)]
    P.add("Body", [extrude_y(pts, -.02, .02)])
    for x in (-.85, .85):
        P.add("Accent", along((x, 0, .02), (x, 0, 1.9), [(0, .035, .035), (.9, .025, .025), (1, 0, 0)], n=4)
              + [s.rbox((.16, .08, .04), (x, 0, .02), r=0, seg=1, taper=(.6, .6))])
    P.add("Accent", [s.rbox((1.86, .05, .05), (0, 0, 1.02), r=.015, seg=1)])
    return P


def meeting_table(L):
    """A long crystal top on two hourglass crystal pedestals."""
    P = D.Parts()
    P.add("Body", [s.rbox((3.2, 1.3, .07), (0, 0, .745), r=.03, seg=1)])
    for x in (-1.1, 1.1):
        P.add("Accent", gem([(0, .3, .35), (.03, .3, .35), (.36, .07, .08), (.72, .22, .26)], n=6, phase=0.0, loc=(x, 0, 0)))
    return P


def reception_desk(L):
    """A counter whose front is one crystal pane with a broken, peaked top edge; a monitor behind."""
    P = D.Parts()
    pts = [(-1.2, 0), (1.2, 0), (1.2, 1.04), (.6, 1.14), (0, 1.07), (-.7, 1.14), (-1.2, 1.04)]
    P.add("Body", [extrude_y(pts, -.44, -.36)])
    for x in (-1.16, 1.16):
        P.add("Body", [s.rbox((.08, .76, 1.08), (x, .0, .54), r=.02, seg=1, taper=(1, .9))])
    P.add("Accent", [s.rbox((2.3, .72, .05), (0, .02, .74), r=.015, seg=1), s.rbox((2.42, .26, .04), (0, -.33, 1.06), r=.012, seg=1)])
    acc, glow = L.screen((.5, .32), (.4, .17, .96), "-Y")
    P.add("Eye", glow + acc + L.slab((.06, .05, .2), (.4, .21, .83)))
    return P


def shelf(L):
    """A crystal rack: tapered prism uprights braced by a long diagonal crystal at each end, cut crystal crates."""
    P = D.Parts()
    for x in (-.97, .97):
        for y in (-.27, .27):
            P.add("Body", along((x, y, 0.0), (x, y, 2.0), [(0, .03, .03), (.5, .025, .025), (1, .02, .02)], n=4))
        P.add("Body", along((x, -.25, .08), (x, .25, 1.9), [(0, .02, .02), (.5, .03, .03), (1, .02, .02)], n=4))
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", [s.rbox((2.0, .6, .05), (0, 0, h), r=.015, seg=1)])
    P.add("Accent", [s.rbox((.45, .4, .3), (-.6, 0, .62), r=.08, seg=1), s.rbox((.35, .35, .25), (.5, .05, .59), r=.07, seg=1),
                     s.rbox((.5, .45, .35), (.3, 0, 1.245), r=.09, seg=1), s.rbox((.4, .4, .3), (-.55, 0, 1.82), r=.08, seg=1)])
    return P


def lockers(L):
    """Three tall hexagonal crystal prisms side by side, each with a cut roof and a handle on its front ridge."""
    P = D.Parts()
    P.add("Body", [s.rbox((.88, .1, 1.7), (0, .19, .87), r=0, seg=1)])
    for x in (-.3, 0, .3):
        P.add("Body", gem([(0, .15, .25), (1.74, .15, .25), (1.9, 0, 0)], n=6, loc=(x, -.02, 0)))
        P.add("Accent", along((x, -.28, .85), (x, -.28, 1.15), [(0, .015, .015), (.5, .025, .025), (1, .015, .015)], n=4))
    return P


def fridge(L):
    """One tall hexagonal crystal prism with a cut roof; a seam splits the doors, handles ride the front ridge."""
    P = D.Parts()
    P.add("Body", gem([(0, .35, .36), (1.62, .35, .36), (1.8, .12, .12)], n=6))
    P.add("Accent", gem([(1.24, .356, .366), (1.27, .356, .366)], n=6))
    P.add("Accent", along((0, -.375, 1.35), (0, -.375, 1.55), [(0, .015, .015), (.5, .025, .025), (1, .015, .015)], n=4)
          + along((0, -.375, .75), (0, -.375, 1.1), [(0, .015, .015), (.5, .025, .025), (1, .015, .015)], n=4))
    return P


def tv_stand(L):
    """A low crystal block; the screen is a pane with a peaked top on a prism stem."""
    P = D.Parts()
    P.add("Body", L.carcass((1.4, .4, .5), (0, 0, 0), kick=.04))
    pts = [(-.66, .68), (.66, .68), (.66, 1.36), (.25, 1.45), (-.2, 1.39), (-.66, 1.43)]
    P.add("Accent", [extrude_y(pts, .055, .09)] + along((0, .08, .5), (0, .08, .7), [(0, .05, .03), (1, .03, .02)], n=4))
    P.add("Eye", [extrude_y([(x * .96, z - .02 if z > 1 else z + .02) for x, z in pts], .035, .06)])
    return P


def toilet_stall(L):
    """Crystal walls with broken peaked tops raised on cut feet; a hexagonal gem for the bowl."""
    P = D.Parts()
    side = [(-.75, .12), (.75, .12), (.75, 1.86), (.3, 2.0), (-.2, 1.9), (-.75, 1.97)]
    for x in (-.5, .5):
        P.add("Body", [extrude_x(side, x - .02, x + .02)])
    P.add("Body", [extrude_y([(-.52, .12), (.52, .12), (.52, 1.9), (0, 2.0), (-.52, 1.92)], .73, .775)])
    for x in (-.5, .5):
        for y in (-.7, 0.0):
            P.add("Body", [s.rbox((.06, .06, .14), (x, y, .07), r=0, seg=1, taper=(.6, .6))])
    P.add("Accent", gem([(0, .15, .15), (.3, .2, .22), (.42, .2, .2)], n=6, phase=0.0, loc=(0, .4, 0)))
    P.add("Accent", [s.rbox((.36, .14, .36), (0, .66, .55), r=.04, seg=1)])
    return P


def wash_basin(L):
    """A crystal counter with hexagonal basins, prism taps and a mirror pane with a peaked top."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.6, .5, .06), (0, 0, .85), r=.02, seg=1)] + L.carcass((1.6, .45, .82), (0, .02, 0))
          + [s.rbox((1.6, .03, .4), (0, .235, 1.06), r=0, seg=1)])
    for x in (-.4, .4):
        P.add("Accent", gem([(.82, .12, .12), (.9, .19, .19), (.91, .17, .17)], n=6, phase=0.0, loc=(x, -.02, 0)))
        P.add("Accent", along((x, .17, .87), (x, .17, 1.0), [(0, .02, .02), (1, .015, .015)], n=4)
              + along((x, .17, 1.0), (x, .07, .98), [(0, .015, .015), (1, .008, .008)], n=4))
    P.add("Eye", [extrude_y([(-.75, 1.13), (.75, 1.13), (.75, 1.82), (.3, 1.928), (-.2, 1.86), (-.75, 1.9)], .225, .25)])
    return P


def pallet(L):
    """A crystal slab on three prism runners carrying cut crystal crates."""
    P = D.Parts()
    P.add("Accent", [s.rbox((1.2, 1.0, .05), (0, 0, .12), r=.015, seg=1)])
    for y in (-.45, 0, .45):
        P.add("Accent", gem([(-.6, .07, .07), (.6, .07, .07)], n=4, phase=math.pi / 4, axis="X", loc=(0, y, .05)))
    P.add("Body", [s.rbox((.55, .45, .4), (x, y, .34), r=.09, seg=1) for x in (-.3, .3) for y in (-.24, .24)]
          + [s.rbox((.53, .43, .4), (-.3, y, .735), r=.09, seg=1) for y in (-.24, .24)])
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_plate": plate, "security_camera": security_camera, "laser_emitter": laser_emitter,
    "sensor_pir": pir,
    "plant": plant,
    "floor_lamp": floor_lamp,
    "desk_lamp": desk_lamp,
    "vending_machine": vending_machine,
    "server_rack": server_rack,
    "water_cooler": water_cooler,
    "safe": safe,
    "bean_bag": bean_bag,
    "chair": chair,
    "round_table": round_table,
    "trash_bin": trash_bin,
    "coat_rack": coat_rack,
    "coffee_machine": coffee_machine,
    "ceiling_lamp": ceiling_lamp,
    "hatch": hatch,
    "arcade_machine": arcade_machine,
    "sofa": sofa,
    "armchair": armchair,
    "bookshelf": bookshelf,
    "boxes": boxes,
    "partition": partition,
    "whiteboard": whiteboard,
    "meeting_table": meeting_table,
    "reception_desk": reception_desk,
    "shelf": shelf,
    "lockers": lockers,
    "fridge": fridge,
    "tv_stand": tv_stand,
    "toilet_stall": toilet_stall,
    "wash_basin": wash_basin,
    "pallet": pallet,
}
