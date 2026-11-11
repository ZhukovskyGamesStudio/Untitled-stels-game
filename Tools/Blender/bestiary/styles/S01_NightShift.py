"""S01 NightShift — «Ночная смена»: night-shift industrial contractor horror (Lofi family: PS1-like low poly).

Utilitarian work machines nobody maintained: hunched, heavy forearms, caged headlamps, hazard chevrons, exposed
pistons and hoses, bolted sheet metal. Language: 8-sided rounds, single chamfers, hard edges; every robot is a piece
of site equipment first (scissor lift, pile driver, floor scrubber, clamp truck, searchlight dolly, oil-drum gun).
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
    """(forward, right, up) for a direction vector or axis name."""
    if isinstance(axis, str):
        axis = {"X": (1, 0, 0), "-X": (-1, 0, 0), "Y": (0, 1, 0), "-Y": (0, -1, 0), "Z": (0, 0, 1), "-Z": (0, 0, -1)}[axis]
    a = V(axis).normalized()
    right = V((1, 0, 0)) if abs(a.x) < 0.9 else V((0, 1, 0))
    up = a.cross(right).normalized()
    right = up.cross(a).normalized()
    return a, right, up


def bar(p0, p1, w, t, normal, r=0.004):
    """A flat bar from p0 to p1: width w, thickness t along `normal`."""
    p0, p1 = V(p0), V(p1)
    z = (p1 - p0)
    L = z.length
    z.normalize()
    y = V(normal)
    y = (y - z * y.dot(z)).normalized()
    x = y.cross(z)
    bm = s.rbox((w, t, L), r=r, seg=1)
    m = Matrix((x, y, z)).transposed().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(p0 + p1) / 2, verts=bm.verts)
    return bm


def stripes(center, w, h, normal, width=0.05, proud=0.014, sink=0.004):
    """Diagonal hazard stripes on a flat face (centre on the face, the face looks along `normal`)."""
    n, right, up = basis(normal)
    c = V(center) + n * (proud / 2 - sink)
    a, b = w / 2 - width * 0.45, h / 2 - width * 0.45
    out = []
    dt = width * 2 * math.sqrt(2)
    t = -(a + b)
    while t <= a + b:
        v0, v1 = max(-b, -a - t), min(b, a - t)
        if v1 - v0 > width * 0.9:
            p0 = c + right * (v0 + t) + up * v0
            p1 = c + right * (v1 + t) + up * v1
            out.append(bar(p0, p1, width, proud, n))
        t += dt
    return out


def cage_lamp(L, loc, r, axis="-Y", bars=2, rb=0.012, depth=None):
    """(accent, glow): a caged work lamp — bezel, lens, wire bars arching over the glass."""
    a, right, up = basis(axis)
    loc = V(loc)
    depth = depth or r * 0.8
    acc = [s.lathe([(r * 1.2, 0), (r * 1.3, depth * 0.2), (r * 1.3, depth * 0.8), (r * 1.12, depth), (0, depth)],
                   seg=8, axis="Z")]
    _to_axis(acc[0], a, loc)
    glow = [s.lathe([(r, 0), (r * 0.85, r * 0.3), (r * 0.45, r * 0.45), (0, r * 0.5)], seg=8, axis="Z")]
    _to_axis(glow[0], a, loc + a * (depth - 0.006))
    R = r * 1.22
    front = loc + a * (depth + r * 0.78)
    for k in range(bars):
        off = (k - (bars - 1) / 2) * r * 0.9
        h = math.sqrt(max(1e-6, R * R - off * off))
        base = loc + a * depth * 0.5 + right * off
        acc.append(s.tube([base + up * h, front + right * off, base - up * h], rb, res=1, segments_per=4))
    acc.append(s.tube([loc + a * depth * 0.5 - right * R, front, loc + a * depth * 0.5 + right * R], rb, res=1, segments_per=4))
    return acc, glow


def _to_axis(bm, a, loc):
    """A shape built along +Z from z = 0: turn it to look along a, move its base to loc."""
    q = V((0, 0, 1)).rotation_difference(a)
    bmesh.ops.rotate(bm, cent=V((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    return bm


def lathe_between(profile, p0, p1, seg=8):
    """A lathe (radius, t) with t in 0..1 laid from p0 to p1."""
    p0, p1 = V(p0), V(p1)
    L = (p1 - p0).length
    bm = s.lathe([(r, t * L) for r, t in profile], seg=seg)
    return _to_axis(bm, (p1 - p0).normalized(), p0)


def corrugated(p0, p1, r, n=6, seg=8):
    prof = [(r * 0.8, 0)]
    for k in range(n):
        prof += [(r, (k + 0.3) / n), (r * 0.8, (k + 0.8) / n)]
    prof += [(r * 0.8, 1.0), (0, 1.0)]
    prof = [(0, 0)] + prof
    return lathe_between(prof, p0, p1, seg)


def hose(points, r=0.022):
    return s.tube([tuple(p) for p in points], r, res=1, segments_per=6)


# =========================================================================== the language

class Style(Lang):
    name = "S01_NightShift"
    seg = 8
    edge = 0.012
    soft = 0.0
    sharp = 30.0
    hull_kind = "rounded"
    limb_kind = "piston"
    eye_kind = "lens"

    def rseg(self):
        return 1

    def rings(self, n=None):
        return self.seg if n is None else max(6, min(n, self.seg + 2))

    def loft(self, rings, axis="Z", loc=(0, 0, 0), round_caps=0.0, seg=None):
        return [s.loft(rings, seg=min(seg or 8, 10), axis=axis, loc=loc, round_caps=round_caps)]

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        return [s.ball(radius, loc, seg=8, scale=scale)]

    def wheel(self, center, radius, width):
        c = V(center)
        out = [s.rcyl(radius, width, c, axis="X", seg=10, r=min(0.02, radius * 0.15), bevel_seg=1)]
        out.append(s.rcyl(radius * 0.52, width * 1.14, c, axis="X", seg=8, r=0.008, bevel_seg=1))
        return out

    def chevrons(self, center, w, h, normal, width=0.05):
        return stripes(center, w, h, normal, width)

    # ------------------------------------------------------------------ furniture: site-office steel
    leg_kind = "square"
    edge_kind = "chamfer"
    cushion_puff = 0.0
    handle_kind = "bar"
    foliage_kind = "facet"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """Bent sheet steel: one hard chamfer."""
        return [s.rbox(size, loc, r=min(min(size) * 0.3, 0.012), seg=1, rot=rot)]

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """Square steel tube welded to a foot pad."""
        x, y, z = top
        fx, fy, fz = x + splay[0], y + splay[1], max(0.0, z - length)
        out = [align(s.rbox((thick, thick, length + 0.01), r=0.004, seg=1), top, (fx, fy, fz + 0.012))]
        out.append(s.rbox((thick * 1.7, thick * 1.7, 0.014), (fx, fy, fz + 0.007), r=0.003, seg=1))
        return out

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """A worn vinyl pad: an eight-sided slab of foam."""
        w, d, h = size
        bm = s.loft([(-h / 2, w / 2 * .96, d / 2 * .96, 4.0), (-h / 2 + h * .2, w / 2, d / 2, 4.0), (h / 2 - h * .2, w / 2, d / 2, 4.0),
                     (h / 2, w / 2 * .96, d / 2 * .96, 4.0)], seg=8)
        return [s.place(bm, loc, rot)]

    def carcass(self, size, loc, kick=0.06):
        """A steel cabinet: a body with a folded top lip and a toe kick."""
        w, d, h = size
        x, y, z = loc
        out = self.slab((w, d, h - kick - .02), (x, y, z + kick + (h - kick - .02) / 2))
        out += self.slab((w + .02, d + .02, .035), (x, y, z + h - .0175))
        if kick > 0:
            out += self.slab((w - 0.04, d - 0.06, kick + 0.01), (x, y + 0.02, z + (kick + 0.01) / 2))
        return out

    def handle(self, loc, length=0.12, vertical=True):
        """A square steel pull on two standoffs that reach back into the door."""
        x, y, z = loc
        size = (0.02, 0.02, length) if vertical else (length, 0.02, 0.02)
        out = [s.rbox(size, (x, y - 0.03, z), r=0.004, seg=1)]
        for sx in (-1, 1):
            dx, dz = (0, sx * length * 0.4) if vertical else (sx * length * 0.4, 0)
            out.append(s.rbox((0.016, 0.04, 0.016), (x + dx, y - 0.012, z + dz), r=0.003, seg=1))
        return out

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Steel legs welded into a frame: stretchers along the sides and the back."""
        out = []
        xs, ys = half_w - inset, half_d - inset
        for sx in (-1, 1):
            for sy in (-1, 1):
                out += self.leg((sx * xs, sy * ys, top_z), length, thick)
        zs = top_z - length * 0.6
        for sx in (-1, 1):
            out += self.slab((thick * .7, ys * 2, .04), (sx * xs, 0, zs))
        out += self.slab((xs * 2, thick * .7, .04), (0, ys, zs))
        return out

    def rod(self, p0, p1, radius, kind=None):
        if (kind or self.limb_kind) == "piston":   # the sleeve reaches back into whatever it is mounted on
            a, b = V(p0), V(p1)
            if (b - a).length > 1e-6:
                p0 = tuple(a - (b - a).normalized() * radius * 0.5)
        return Lang.rod(self, p0, p1, radius, kind)

    def screen(self, size, loc, face="-Y"):
        """A deep tube-monitor bezel."""
        w, h = size
        x, y, z = loc
        sign = -1 if face == "-Y" else 1
        acc = self.slab((w, 0.06, h), (x, y - sign * 0.012, z))
        glow = [s.rbox((w - 0.05, 0.01, h - 0.05), (x, y + sign * 0.017, z), r=0.004, seg=1)]
        return acc, glow

    def shade(self, r_bottom, r_top, height, loc):
        """A caged work-lamp shade: an eight-sided reflector with two guard wires across the mouth."""
        x, y, z = loc
        out = [s.lathe([(r_bottom, 0), (r_bottom + 0.006, 0.01), (r_top + 0.006, height), (r_top - 0.01, height),
                        (r_bottom - 0.012, 0.01), (0, 0.01)], seg=8, loc=loc)]
        for a in (math.pi / 8, math.pi / 8 + math.pi / 2):
            c, sn = math.cos(a) * r_bottom, math.sin(a) * r_bottom
            out.append(bar((x - c, y - sn, z + .004), (x + c, y + sn, z + .004), .012, .012, (0, 0, 1), r=.003))
        return out

    def pot(self, r, h, loc):
        """A paint bucket with a wire handle."""
        x, y, z = loc
        out = [s.lathe([(0, 0), (r * 0.9, 0), (r, h * 0.95), (r * 1.05, h * 0.96), (r * 1.05, h), (0, h)], seg=8, loc=loc)]
        out.append(s.tube([(x - r * 1.02, y, z + h * .85), (x, y, z + h * 1.3), (x + r * 1.02, y, z + h * .85)], .008, res=1, segments_per=4))
        return out

    def signature(self, model, P):
        if model == "laser_emitter":
            P.add("Body", self.box((.2, .2, .08), (0, 0, .04)) + self.box((.16, .16, .05), (0, 0, 1.6)))
        return P


# =========================================================================== walkers

def guard(L):
    """The night watchman: a hunched labour mech — a heavy chest leaning over a sunk head with a caged headlamp,
    a battery hump above the head, gorilla forearms with hazard plates and claw hands, hoses, piston shins."""
    P = D.Parts()
    P.add("Body", L.box((.44, .28, .2), (0, .01, .97)))                                              # pelvis
    P.add("Body", L.rod((0, .04, .98), (0, .04, 1.3), .075, kind="piston"))                          # bare spine
    P.add("Body", L.loft([(1.16, .2, .15, 4, 0, .05), (1.36, .36, .23, 5, 0, .0), (1.6, .44, .27, 5, 0, -.03),
                          (1.78, .4, .22, 5, 0, .03), (1.9, .27, .16, 4, 0, .08)]))                   # hunched chest
    P.add("Accent", L.box((.36, .06, .26), (0, -.27, 1.42), rot=(math.radians(-12), 0, 0)))          # chest plate
    P.add("Body", stripes((0, -.3, 1.42), .34, .24, (0, -.97, .21)))
    P.add("Body", L.box((.52, .28, .72), (0, .27, 1.71), taper=(.92, .9)))                            # battery hump
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.045, .3, (sx * .15, .33, 2.06)))                                      # exhaust stacks
        P.add("Accent", L.cyl(.06, .04, (sx * .15, .33, 2.21)))
    # head sunk in front of, and below, the shoulders
    P.add("Accent", L.box((.32, .26, .22), (0, -.12, 1.76), taper=(.9, .9)))
    P.add("Accent", L.box((.38, .1, .05), (0, -.21, 1.88), rot=(math.radians(14), 0, 0)))             # brow
    P.eye(cage_lamp(L, (0, -.24, 1.75), .09))
    for sx in (-1, 1):
        x = sx
        P.add("Accent", L.box((.3, .36, .26), (x * .45, .0, 1.84), rot=(0, x * math.radians(12), 0)))   # shoulder block
        P.add("Body", L.rod((x * .46, 0, 1.68), (x * .52, -.04, 1.26), .07, kind="piston"))           # upper arm
        P.add("Accent", L.ball(.085, (x * .52, -.04, 1.25)))                                          # elbow
        P.add("Body", L.loft([(.76, .1, .11, 4, x * .54, -.16), (.86, .13, .14, 4, x * .54, -.14),
                              (1.1, .15, .16, 4, x * .54, -.08), (1.28, .09, .1, 4, x * .52, -.04)]))  # forearm
        P.add("Accent", L.box((.06, .26, .34), (x * .68, -.1, 1.0)))                                  # hazard plate
        P.add("Body", stripes((x * .71, -.1, 1.0), .26, .34, (x, 0, 0), width=.045))
        P.add("Body", L.box((.22, .24, .14), (x * .54, -.17, .72)))                                  # claw hand
        for dx in (-.07, .07):
            P.add("Accent", L.rod((x * .54 + dx, -.2, .72), (x * .54 + dx * 1.3, -.27, .56), .025, kind="square"))
        P.add("Body", [hose([(x * .2, .36, 1.6), (x * .42, .34, 1.38), (x * .56, .06, 1.16)], .024)])
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        P.add(leg, L.ball(.1, (x, 0, .9)))
        P.add(leg, L.loft([(.46, .08, .09, 4, x, .01), (.7, .11, .12, 4, x, 0), (.92, .1, .11, 4, x, 0)]))  # thigh
        P.add(leg, L.cyl(.08, .21, (x, 0, D.GUARD_KNEE), axis="X"))                                 # knee
        P.add(leg, L.box((.17, .08, .17), (x, -.09, .48)))                                          # knee guard
        P.add(leg, L.box((.15, .15, .2), (x, .02, .32)))                                            # shin block
        P.add(leg, L.rod((x, .02, .44), (x, .0, .1), .055, kind="piston"))
        P.add(leg, L.box((.21, .36, .12), (x, -.07, .06), taper=(.85, .72)))                         # boot
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hunter(L):
    """The excavator: a cab on a hover sled, a boom neck carrying a bucket head with the headlamp, two excavator
    booms for arms ending in grab hooks — a reaper built from site machinery."""
    P = D.Parts()
    P.add("Body", L.loft([(.06, .32, .38, 5, 0, .05), (.2, .42, .5, 5, 0, .05), (.36, .34, .4, 5, 0, .05)]))   # sled
    P.add("Accent", L.loft([(.15, .44, .52, 5, 0, .05), (.25, .44, .52, 5, 0, .05)]))
    P.add("Accent", L.box((.5, .05, .12), (0, -.44, .2)))
    P.add("Body", stripes((0, -.465, .2), .48, .1, (0, -1, 0), width=.04))
    for x, y in ((-.22, -.2), (.22, -.2), (-.22, .3), (.22, .3)):
        P.add("Accent", L.cyl(.08, .08, (x, y, .04), radius2=.06))                                 # thrusters
        P.add("Eye", L.cyl(.05, .02, (x, y, .0)))
    P.add("Body", L.box((.22, .22, 1.0), (0, .14, .8)))                                             # mast
    P.add("Body", L.rod((0, -.25, .3), (0, -.08, 1.25), .06, kind="piston"))                       # front ram
    P.add("Body", L.loft([(1.18, .22, .18, 4, 0, .1), (1.5, .34, .24, 5, 0, .02), (1.8, .38, .24, 5, 0, -.03),
                          (1.94, .28, .2, 4, 0, -.01)]))                                            # cab
    P.add("Accent", L.box((.34, .06, .2), (0, -.27, 1.6), rot=(math.radians(-8), 0, 0)))
    P.add("Body", stripes((0, -.3, 1.6), .32, .18, (0, -.99, .14), width=.04))
    # boom neck and the bucket head
    P.add("Accent", [hose([(0, .12, 1.85), (0, .1, 2.3), (0, -.14, 2.1)], .07)])
    P.add("Accent", L.box((.36, .36, .26), (0, -.16, 2.02), taper=(.9, .85)))
    for k in range(4):
        P.add("Body", L.rod((-.12 + k * .08, -.31, 1.93), (-.12 + k * .08, -.38, 1.8), .025, kind="square"))  # teeth
    P.eye(cage_lamp(L, (0, -.3, 2.04), .085))
    for sx in (-1, 1):
        x = sx
        P.add("Accent", L.box((.26, .3, .22), (x * .42, 0, 1.84)))                                  # shoulder
        P.add("Body", L.rod((x * .45, .03, 1.82), (x * .58, .14, 1.38), .08, kind="square"))         # boom
        P.add("Body", L.rod((x * .52, -.08, 1.8), (x * .6, .1, 1.4), .03, kind="piston"))
        P.add("Accent", L.ball(.08, (x * .58, .14, 1.36)))
        P.add("Body", L.rod((x * .58, .14, 1.36), (x * .5, -.46, 1.0), .07, kind="square"))          # stick
        P.add("Body", L.rod((x * .6, .1, 1.38), (x * .53, -.38, 1.07), .028, kind="piston"))
        P.add("Accent", L.box((.18, .2, .18), (x * .5, -.48, .98)))                                 # wrist
        P.add("Accent", L.box((.28, .26, .24), (x * .5, -.56, .82), rot=(math.radians(30), 0, 0), taper=(1.15, 1.2)))  # bucket
        for dx in (-.09, 0, .09):
            P.add("Body", L.rod((x * .5 + dx, -.62, .74), (x * .5 + dx, -.72, .62), .024, kind="square"))  # bucket teeth
    return P


def hound(L):
    """The compressor dog: a gas tank for a body on four piston legs, a corrugated hose neck to a welded box skull
    with a round intake for a nose, an exhaust stack for a tail."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.14, 0), (.2, .07), (.2, .73), (.14, .8), (0, .8)], seg=8, axis="Y", loc=(0, -.42, .84))])
    for y in (-.24, .2):
        P.add("Accent", [s.lathe([(.205, 0), (.215, .01), (.215, .05), (.205, .06), (0, .06), (0, 0)], seg=8, axis="Y",
                                 loc=(0, y, .84))])
    P.add("Body", L.box((.38, .86, .1), (0, 0, .67)))                                               # saddle frame
    P.add("Accent", L.box((.2, .3, .06), (0, .05, 1.05)))                                           # valve plate
    P.add("Accent", L.cyl(.035, .1, (0, .05, 1.1)) + L.cyl(.07, .02, (0, .05, 1.15)))               # valve wheel
    P.add("Body", [corrugated((0, -.36, .9), (0, -.66, 1.0), .075)])                                # hose neck
    P.add("Accent", L.box((.24, .28, .2), (0, -.73, 1.0), taper=(.92, .9)))                         # skull
    P.add("Accent", L.box((.16, .26, .13), (0, -.93, .93)))                                         # snout
    P.add("Body", L.cyl(.065, .06, (0, -1.06, .93), axis="Y"))
    P.add("Eye", L.cyl(.045, .02, (0, -1.09, .93), axis="Y"))                                       # intake glow
    for sx in (-1, 1):
        P.add("Eye", L.box((.06, .03, .03), (sx * .07, -.87, 1.0), r=.006))
        P.add("Accent", L.box((.04, .1, .1), (sx * .11, -.66, 1.12), rot=(math.radians(-25), 0, sx * .2)))  # ear flaps
    P.add("Accent", L.cyl(.05, .3, (0, .4, 1.06)) + L.cyl(.065, .04, (0, .4, 1.22)))                 # exhaust tail
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add(leg, L.ball(.075, (x, y, .62)))
        P.add(leg, L.box((.11, .12, .3), (x, y, .45), taper=(.8, .8)))
        P.add(leg, L.ball(.055, (x, y, D.HOUND_KNEE)))
        P.add(leg, L.rod((x, y, D.HOUND_KNEE), (x, y, .05), .042, kind="piston"))
        P.add(leg, L.box((.11, .17, .06), (x, y - .03, .03), taper=(.8, .7)))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """The bottle crawler: a welding gas bottle for an abdomen, a chamfered box thorax with a caged lamp, square
    angle-iron legs with knee blocks."""
    P = D.Parts()
    P.add("Body", L.box((.3, .3, .16), (0, -.08, .2)))
    P.add("Body", [s.lathe([(0, 0), (.1, 0), (.15, .05), (.15, .3), (.1, .38), (.05, .4), (0, .4)], seg=8, axis="Y",
                           loc=(0, .04, .22))])
    P.add("Accent", L.cyl(.035, .08, (0, .48, .22), axis="Y") + L.cyl(.07, .02, (0, .52, .22), axis="Y"))  # valve
    P.add("Accent", [s.lathe([(.155, 0), (.16, .01), (.16, .05), (.155, .06), (0, .06), (0, 0)], seg=8, axis="Y", loc=(0, .2, .22))])
    P.add("Accent", L.box((.22, .2, .04), (0, -.1, .29)))
    P.eye(cage_lamp(L, (0, -.22, .2), .06))
    for sx in (-1, 1):
        P.add("Eye", L.box((.04, .03, .03), (sx * .11, -.235, .25), r=.006))
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", L.rod(hip, knee, .026, kind="square") + L.box((.07, .07, .07), knee))
        P.add(f"AccentLeg{i}Lower", L.rod(knee, foot, .02, kind="square")
              + L.box((.06, .06, .04), foot - V((0, 0, .0))))
    return P


# =========================================================================== wheeled and tracked

def listener(L):
    """The scissor-lift ears: a flatbed on four lugged wheels, a scissor lift raising an amplifier box with two big
    siren horns turned sideways (the receivers glow in their mouths), a caged lamp on the front bumper."""
    P = D.Parts()
    D.wheels(P, L, [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)], .22, .12)
    P.add("Body", L.box((.6, 1.04, .2), (0, 0, .42)))
    for x, y in ((-.43, -.3), (.43, -.3), (-.43, .3), (.43, .3)):
        P.add("Body", L.rod((x * .5, y, .22), (x * .98, y, .22), .05, kind="square"))
        P.add("Body", L.box((.12, .14, .16), (x * .64, y, .3)))
        P.add("Accent", L.box((.25, .5, .05), (x * .97, y, .49), rot=(0, x / abs(x) * math.radians(-12), 0)))  # fenders
    P.add("Accent", L.box((.66, .08, .12), (0, -.54, .4)))                                         # bumper
    P.add("Body", stripes((0, -.58, .4), .62, .1, (0, -1, 0), width=.04))
    P.eye(cage_lamp(L, (0, -.57, .5), .06))
    # scissor lift
    for sx in (-1, 1):
        x = sx * .2
        P.add("Body", [bar((x, -.24, .52), (x, .24, .96), .05, .04, (1, 0, 0))])
        P.add("Body", [bar((x + sx * .045, .24, .52), (x + sx * .045, -.24, .96), .05, .04, (1, 0, 0))])
        P.add("Accent", L.cyl(.035, .1, (x + sx * .02, 0, .74), axis="X"))
    P.add("Body", L.box((.5, .62, .06), (0, 0, .98)))                                               # platform
    P.add("Accent", L.box((.32, .3, .22), (0, .04, 1.11)))                                         # amplifier
    for sx in (-1, 1):
        p0, p1 = V((sx * .14, .04, 1.13)), V((sx * .52, -.04, 1.16))
        P.add("Body", [lathe_between([(0, 0), (.06, 0), (.06, .3), (.1, .6), (.17, .85), (.23, 1.0), (0, 1.0)], p0, p1, seg=8)])
        P.add("Eye", [lathe_between([(0, 0), (.16, 0), (.16, .5), (0, .5)], p1 - (p1 - p0).normalized() * .01,
                                    p1 + (p1 - p0).normalized() * .015, seg=8)])
    P.add("Accent", L.cyl(.02, .3, (0, .15, 1.36)))                                                 # whip antenna
    return P


def worker(L):
    """The clamp truck: a low tug with a roll cage and an amber beacon, two hydraulic clamp arms squeezing the crate
    from the sides, twin caged headlamps on the crossbar."""
    P = D.Parts()
    D.wheels(P, L, [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)], .12, .1)
    P.add("Body", L.box((.54, .78, .2), (0, .0, .26)))
    for x, y in ((.31, .25), (-.31, .25), (.31, -.25), (-.31, -.25)):
        P.add("Body", L.rod((x * .5, y, .12), (x * .96, y, .12), .035, kind="square"))
    P.add("Body", L.box((.46, .3, .26), (0, .22, .48)))                                             # engine block
    # roll cage over the engine
    for sx in (-1, 1):
        for y in (.08, .36):
            P.add("Accent", L.rod((sx * .22, y, .5), (sx * .2, y, .98), .022, kind="square"))
        P.add("Accent", L.rod((sx * .2, .07, .98), (sx * .2, .37, .98), .022, kind="square"))
    P.add("Accent", L.rod((-.21, .36, .98), (.21, .36, .98), .022, kind="square"))
    P.add("Accent", L.rod((-.21, .08, .98), (.21, .08, .98), .022, kind="square"))
    P.add("Body", L.cyl(.04, .14, (0, .3, 1.04)))
    P.eye(cage_lamp(L, (0, .3, 1.1), .045, axis="Z"))
    P.add("Body", L.cyl(.035, .36, (-.16, .3, .74)) + L.cyl(.045, .04, (-.16, .3, .93)))           # exhaust
    # clamp frame
    P.add("Body", L.box((.6, .06, .08), (0, -.36, .5)))                                            # crossbar
    for sx in (-1, 1):
        x = sx * .29
        P.add("Body", L.box((.06, .07, 1.1), (x, -.36, .8)))                                        # uprights
        P.add("Body", L.rod((x, -.36, 1.15), (x, -.1, .38), .028, kind="piston"))
        P.add("Accent", L.box((.05, .46, .36), (sx * .25, -.15, 1.15)))                             # clamp paddles
        P.add("Body", L.rod((x, -.36, 1.15), (sx * .26, -.36, 1.15), .03))
        P.eye(cage_lamp(L, (sx * .14, -.39, .5), .045))
    P.add("Accent", L.box((.46, .44, .4), (0, -.15, 1.15)))                                        # crate
    for z in (1.04, 1.26):
        P.add("Body", L.box((.47, .45, .035), (0, -.15, z), r=.004))                                # straps
    P.add("Body", L.box((.58, .06, .1), (0, -.42, .26)))
    P.add("Accent", stripes((0, -.45, .26), .56, .09, (0, -1, 0), width=.035))
    return P


def sweeper(L):
    """The floor scrubber: a wedge hood on three wheels, a recovery tank lying across the back, a curved squeegee
    blade dragging behind, a brush roller in front, a caged lamp, the lidar spinning under the belly."""
    P = D.Parts()
    D.wheels(P, L, [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)], .08, .06)
    for x, y in ((-.28, .18), (.28, .18)):
        P.add("Body", L.rod((x * .6, y, .08), (x, y, .08), .025))
        P.add("Body", L.box((.08, .1, .2), (x * .6, y, .2)))
    P.add("Body", L.box((.14, .1, .14), (0, -.3, .24)))                                              # front fork
    P.add("Body", L.box((.03, .06, .14), (-.05, -.3, .12)) + L.box((.03, .06, .14), (.05, -.3, .12)))
    P.add("Body", L.box((.68, .74, .36), (0, .0, .48), taper=(.92, .7)))                            # wedge hood
    P.add("Accent", L.box((.72, .78, .06), (0, 0, .32)))                                           # bumper skirt
    P.add("Body", stripes((0, -.39, .32), .68, .05, (0, -1, 0), width=.03))
    P.add("Accent", L.cyl(.11, .5, (0, .17, .72), axis="X"))                                       # recovery tank
    P.add("Body", L.box((.06, .06, .1), (-.2, .17, .63)) + L.box((.06, .06, .1), (.2, .17, .63)))
    P.add("Body", L.cyl(.05, .1, (0, .0, .25)))                                                     # lidar spindle
    P.add("AccentLidar", L.cyl(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    # squeegee behind, brush roller in front
    arc = [(.42 * math.cos(math.radians(a)), .08 + .3 * math.sin(math.radians(a)), .04) for a in range(10, 171, 20)]
    for p0, p1 in zip(arc, arc[1:]):
        P.add("Accent", [bar(p0, p1, .025, .07, (0, 0, 1), r=.004)])
    P.add("Body", L.box((.7, .06, .05), (0, .36, .07)))
    P.add("Body", L.rod((-.3, .3, .3), (-.3, .36, .08), .02) + L.rod((.3, .3, .3), (.3, .36, .08), .02))
    P.add("Accent", L.cyl(.055, .6, (0, -.43, .06), axis="X"))
    for sx in (-1, 1):
        P.add("Body", L.box((.04, .12, .28), (sx * .31, -.4, .17)))
    P.add("Body", [hose([(.2, .2, .7), (.38, .36, .5), (.3, .36, .08)], .022)])
    P.eye(cage_lamp(L, (0, -.3, .52), .07))
    P.add("Body", L.cyl(.03, .2, (.22, -.1, .72)) + L.cyl(.04, .03, (.22, -.1, .83)))               # vent stack
    return P


def thumper(L):
    """The pile driver: a tracked engine deck with a tall leader mast in front, a striped drop hammer between the
    beams and two hydraulic rams pressing the sensor pads into the floor."""
    P = D.Parts()
    for sx in (-1, 1):
        P.add("Accent", L.track((.24, 1.36, .54), (sx * .56, 0, .28)))
    P.add("Body", L.box((.94, 1.08, .36), (0, .06, .7), taper=(.94, .9)))                           # engine deck
    P.add("Body", L.box((.56, .5, .34), (0, .3, 1.02), taper=(.88, .85)))                           # engine
    for sx in (-1, 1):
        P.add("Accent", L.cyl(.05, .4, (sx * .18, .42, 1.3)))
    P.add("Body", L.box((.82, .3, .12), (0, -.55, .58)))                                            # front sill
    # leader mast
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", L.box((.09, .09, 1.6), (x, -.66, 1.08)))
        P.add("Body", L.rod((x, -.62, 1.84), (x * .9, .25, 1.18), .035, kind="square"))            # back stays
        P.add("Accent", L.rod((x, -.85, .62), (x, -.85, .1), .05, kind="piston"))                  # ram
        P.add("Body", L.box((.12, .3, .08), (x, -.75, .64)))
        P.add("Accent", L.cyl(.14, .06, (x, -.85, .07)))
        P.add("Eye", L.cyl(.18, .04, (x, -.85, .02)))
    for z in (1.86, 1.0):
        P.add("Body", L.box((.7, .1, .08), (0, -.66, z)))
    P.add("Accent", L.box((.52, .2, .34), (0, -.68, 1.42)))                                         # drop hammer
    P.add("Body", stripes((0, -.785, 1.42), .48, .32, (0, -1, 0), width=.05))
    P.add("Body", L.cyl(.025, .3, (0, -.68, 1.72)))
    P.eye(cage_lamp(L, (0, -.69, .6), .06))
    return P


def sorter(L):
    """The searchlight dolly: an oil drum counterweight, a lattice pole, a yoke holding a big caged searchlight
    drum whose glass is the colour lens."""
    P = D.Parts()
    P.add("Accent", L.lathe([(0, 0), (.3, 0), (.3, .04), (.25, .08), (0, .08)]))                    # base disc
    P.add("Body", L.cyl(.22, .34, (0, .02, .25)))                                                   # oil drum
    for z in (.15, .35):
        P.add("Accent", L.cyl(.23, .03, (0, .02, z)))
    for k in range(3):
        a = k / 3 * math.tau + math.pi / 2
        P.add("Body", L.rod((math.cos(a) * .12, .02 + math.sin(a) * .12, .4), (math.cos(a) * .04, .02 + math.sin(a) * .04, 1.0),
                            .018, kind="square"))
    P.add("Body", L.cyl(.06, .08, (0, .02, 1.0)))
    P.add("Accent", L.box((.6, .08, .06), (0, .03, 1.03)))                                          # yoke
    for sx in (-1, 1):
        P.add("Accent", L.box((.05, .08, .3), (sx * .28, .03, 1.17)))
        P.add("Body", L.cyl(.04, .06, (sx * .25, .03, 1.25), axis="X"))
    P.add("Body", [s.lathe([(0, 0), (.2, 0), (.23, .05), (.23, .3), (.24, .36), (0, .36)], seg=10, axis="-Y", loc=(0, .2, 1.25))])
    P.add("Accent", [s.lathe([(.25, 0), (.25, .04), (0, .04)], seg=10, axis="-Y", loc=(0, -.15, 1.25))])
    P.add("Eye", [s.lathe([(.21, 0), (.17, .03), (0, .04)], seg=10, axis="-Y", loc=(0, -.18, 1.25))])
    for dx in (-.1, .1):
        P.add("Accent", [hose([(dx, -.17, 1.49), (dx, -.26, 1.25), (dx, -.17, 1.01)], .013)])
    P.add("Accent", [hose([(-.24, -.17, 1.25), (0, -.26, 1.25), (.24, -.17, 1.25)], .013)])
    P.add("Accent", L.box((.44, .2, .03), (0, -.1, 1.5), rot=(math.radians(8), 0, 0)))             # sun hood
    P.add("Body", [hose([(0, .15, 1.15), (.1, .2, .7), (.18, .1, .35)], .02)])
    return P


# =========================================================================== devices

def drone(L):
    """The work-light drone: a square-tube cross frame, a striped battery box, octagonal rotor guards, a caged
    work lamp slung under the nose as its camera."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    P.add("Body", L.box((.32, .4, .18), (0, 0, 0)))                                                 # battery box
    P.add("Accent", L.box((.26, .32, .03), (0, .02, .095)))
    P.add("Body", stripes((0, .02, .11), .24, .3, (0, 0, 1), width=.035))
    for sx in (-1, 1):
        P.add("Accent", L.rod((sx * .06, -.12, .1), (sx * .06, .14, .1), .016, kind="square"))     # carry rails
    for i, (x, y) in enumerate(rot):
        P.add("Body", L.rod((x * .2, y * .2, 0), (x, y, 0), .025, kind="square"))
        P.add("Body", L.cyl(.045, .08, (x, y, .01)))                                                # motor
        R = .2
        pts = [(x + math.cos(k / 8 * math.tau + math.pi / 8) * R, y + math.sin(k / 8 * math.tau + math.pi / 8) * R, .06) for k in range(8)]
        for k in range(8):
            P.add("Accent", [bar(pts[k], pts[(k + 1) % 8], .012, .03, (0, 0, 1), r=.003)])         # guard ring
        u = V((x, y, 0)).normalized()
        post = V((x, y, 0)) - u * R * math.cos(math.pi / 8)
        P.add("Accent", L.box((.03, .03, .09), (post.x, post.y, .03)))                               # guard post
        P.add(f"AccentRotor{i}", L.cyl(.03, .04, (x, y, .065)) +
              [bar((x - .16, y, .07), (x + .16, y, .06), .045, .01, (0, 0, 1), r=.002)])
    P.add("Body", L.box((.06, .08, .1), (0, -.2, -.07)))
    P.eye(cage_lamp(L, (0, -.23, -.09), .065, axis=(0, -.7, -.7)))
    return P


def turret(L):
    """The oil-drum nailer: a drum on the floor with an amber collar, a pipe column, a rivet gun head with a coil
    magazine and a hazard-striped flank (the head turns, the collar and drum lamps stay still)."""
    P = D.Parts()
    P.add("Body", L.cyl(.27, .62, (0, 0, .31)))
    for z in (.12, .5):
        P.add("Body", L.cyl(.28, .035, (0, 0, z)))
    for k in range(3):
        a = k / 3 * math.tau + math.pi / 2
        P.add("Body", L.box((.08, .3, .04), (math.cos(a) * .3, math.sin(a) * .3, .02), rot=(0, 0, a + math.pi / 2)))
    P.add("Body", L.cyl(.07, .5, (0, 0, .85)))
    P.add("Body", L.cyl(.12, .06, (0, 0, 1.1)))
    P.add("Eye", L.cyl(.125, .04, (0, 0, 1.06)))
    for k in range(3):
        a = k / 3 * math.tau - math.pi / 2
        P.add("Eye", L.box((.07, .03, .05), (math.cos(a) * .27, math.sin(a) * .27, .55), rot=(0, 0, a + math.pi / 2)))
    P.add("Body", [hose([(.12, .1, .6), (.25, .22, .8), (.07, .05, 1.0)], .02)])
    P.add("AccentHead", L.cyl(.1, .08, (0, 0, 1.17)))
    P.add("AccentHead", L.box((.24, .56, .2), (0, -.08, 1.3), taper=(.9, 1)))
    P.add("AccentHead", L.box((.12, .14, .1), (0, .1, 1.44)))
    P.add("AccentHead", L.cyl(.05, .24, (0, -.46, 1.28), axis="Y") + L.cyl(.065, .05, (0, -.58, 1.28), axis="Y"))
    P.add("AccentHead", L.cyl(.1, .1, (.16, -.02, 1.3), axis="X"))                                  # coil magazine
    P.add("AccentHead", stripes((-.115, -.08, 1.3), .5, .16, (-1, 0, 0), width=.04))
    return P


def vacuum(L):
    """The extractor: an octagonal fan housing on the ceiling, a striped rim, a fan grille and a caged lamp
    looking down into the room (-z)."""
    P = D.Parts()
    P.add("Body", [s.lathe([(0, 0), (.34, 0), (.36, .02), (.36, .07), (.33, .1), (0, .1)], seg=8)])
    P.add("Accent", [s.lathe([(.37, .02), (.37, .07), (0, .07), (0, .02)], seg=8)])
    P.add("Accent", [s.lathe([(0, 0), (.24, 0), (.26, .015), (0, .022)], seg=8, loc=(0, 0, .1))])
    for k in range(4):
        a = k / 4 * math.pi
        P.add("Accent", [bar((math.cos(a) * -.3, math.sin(a) * -.3, -.008), (math.cos(a) * .3, math.sin(a) * .3, -.008),
                             .03, .025, (0, 0, -1), r=.004)])
    P.add("Accent", L.cyl(.1, .03, (0, 0, -.01)))
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        P.add("Body", L.box((.1, .05, .06), (math.cos(a) * .27, math.sin(a) * .27, .12), rot=(0, 0, a)))  # ceiling brackets
    P.eye(cage_lamp(L, (0, 0, -.015), .04, axis="-Z", depth=.025))
    P.add("Eye", L.box((.2, .04, .02), (0, -.25, -.002), r=.006))
    return P


def security_camera(L):
    """A caged site camera: a bolted wall plate, a square arm, a chamfered housing with a hazard-striped top and a
    wire cage over the lens."""
    P = D.Parts()
    P.add("Body", L.box((.16, .06, .22), (0, .27, 0)))
    P.add("Accent", L.rod((0, .26, 0), (0, .08, -.02), .025, kind="square"))
    P.add("Accent", L.box((.17, .3, .15), (0, -.04, -.02)))
    P.add("Body", stripes((0, -.04, .055), .15, .28, (0, 0, 1), width=.03))
    P.add("Accent", L.box((.2, .1, .02), (0, -.17, .06), r=.005))
    a, g = cage_lamp(L, (0, -.19, -.02), .05, depth=.03)
    P.add("Accent", a).add("Eye", g)
    return P


def plate(L):
    """A pressure plate with a hazard-striped border."""
    P = D.plate(L)
    for k in (-1, 1):
        P.add("Accent", stripes((0, k * .46, .012), .9, .08, (0, 0, 1), width=.04, proud=.008, sink=.003))
        P.add("Accent", stripes((k * .46, 0, .012), .08, .84, (0, 0, 1), width=.04, proud=.008, sink=.003))
    return P


def lidar(L):
    """A lidar on a bolted octagon foot with a caged drum."""
    P = D.lidar(L)
    P.add("Body", [s.lathe([(0, 0), (.2, 0), (.2, .02), (0, .02)], seg=8)])
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        P.add("Body", L.box((.03, .03, .03), (math.cos(a) * .16, math.sin(a) * .16, .025)))
    return P


def pir(L):
    """A motion sensor behind a wire cage, a sun hood over it."""
    P = D.Parts()
    P.add("Body", L.box((.16, .06, .16), (0, .04, 0)))
    P.add("Eye", [s.lathe([(.055, 0), (.05, .025), (.03, .05), (0, .058)], seg=8, axis="-Y", loc=(0, .01, 0))])
    for dx in (-.03, .03):
        P.add("Body", [hose([(dx, .01, .07), (dx, -.07, 0), (dx, .01, -.07)], .008)])
    P.add("Body", L.box((.16, .08, .02), (0, -.02, .075)))
    return P


# =========================================================================== furniture and level pieces

def arcade_machine(L):
    """A break-room arcade bolted together from steel sheet: a wire guard over the slanted screen, a striped kick
    plate, a padlocked coin box."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .78, 1.75), (0, .02, 0), kick=.05))
    P.add("Accent", L.slab((.72, .4, .06), (0, -.38, .95), rot=(math.radians(-12), 0, 0)))            # control deck
    P.add("Accent", L.slab((.72, .25, .2), (0, -.1, 1.7)))                                              # marquee
    P.add("Eye", L.slab((.56, .03, .44), (0, -.38, 1.3), rot=(math.radians(-15), 0, 0)))
    for dx in (-.2, -.07, .07, .2):
        P.add("Accent", [bar((dx, -.42, 1.08), (dx, -.31, 1.53), .02, .02, (0, -1, .26), r=.003)])   # screen guard
    P.add("Accent", L.cyl(.012, .1, (-.15, -.44, 1.04)) + L.ball(.03, (-.15, -.44, 1.1)))             # stick
    P.add("Accent", L.slab((.2, .04, .2), (0, -.38, .55)))                                              # coin box
    P.add("Body", L.box((.05, .03, .06), (0, -.405, .5)))
    P.add("Accent", stripes((0, -.376, .2), .66, .26, (0, -1, 0), width=.04))
    return P


def vending_machine(L):
    """A heavy steel vending box: the lit window behind a welded cage, a striped kick plate, a padlocked flap."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .8, 1.9), (0, 0, 0), kick=.05))
    P.add("Eye", L.slab((.6, .02, 1.1), (-.1, -.405, 1.15)))
    for k in range(6):
        x = -.37 + k * .108
        P.add("Accent", L.box((.025, .025, 1.18), (x, -.42, 1.15)))
    for z in (.62, 1.15, 1.68):
        P.add("Accent", L.box((.62, .025, .025), (-.1, -.43, z)))
    P.add("Accent", L.slab((.14, .03, .5), (.33, -.41, 1.2)) + L.slab((.6, .03, .15), (-.1, -.41, .3)))
    P.add("Body", L.box((.06, .03, .08), (-.1, -.43, .3)))
    P.add("Accent", stripes((0, -.4, .14), .86, .14, (0, -1, 0), width=.04))
    return P


def safe(L):
    """A site strongbox on skids: a thick door with a spoked wheel, hinge blocks and steel corner guards."""
    P = D.Parts()
    P.add("Body", L.slab((.8, .7, .88), (0, 0, .55)))
    for x in (-.28, .28):
        P.add("Body", L.slab((.12, .7, .12), (x, 0, .06)))                                             # skids
    P.add("Body", L.slab((.66, .03, .74), (0, -.36, .55)))
    for z in (.3, .8):
        P.add("Accent", L.box((.06, .06, .14), (-.36, -.37, z)))                                       # hinges
    for x in (-.39, .39):
        P.add("Accent", L.box((.04, .04, .86), (x, -.34, .55)))                                        # corner guards
    c = V((.08, -.39, .55))
    P.add("Accent", L.cyl(.05, .05, tuple(c), axis="Y"))
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        tip = c + V((math.cos(a) * .15, -.01, math.sin(a) * .15))
        P.add("Accent", L.rod(tuple(c), tuple(tip), .015, kind="square") + L.ball(.022, tuple(tip)))
    P.add("Eye", L.slab((.06, .03, .06), (-.22, -.385, .66)))
    return P


def plant(L):
    """A plant someone keeps in a paint bucket, tied to a stake."""
    P = D.Parts()
    P.add("Accent", L.pot(.21, .43, (0, 0, 0)))
    P.add("Body", L.rod((0, 0, .4), (0, 0, .75), .03, kind="square"))
    P.add("Body", L.foliage((0, 0, .78), .3, n=6) + L.foliage((.06, .03, 1.22), .2, n=3, seed=4))
    P.add("Body", L.rod((-.06, .05, .38), (-.08, .06, 1.3), .012, kind="square"))
    return P


def floor_lamp(L):
    """A tripod site light: square-tube tripod, a mast, a caged flood reflector looking down."""
    P = D.Parts()
    for k in range(3):
        a = k / 3 * math.tau + math.pi / 2
        P.add("Body", [bar((0, 0, .72), (math.cos(a) * .17, math.sin(a) * .17, .01), .03, .03, (0, 0, 1), r=.004)])
        P.add("Body", L.box((.05, .05, .02), (math.cos(a) * .17, math.sin(a) * .17, .01)))
    P.add("Body", L.box((.06, .06, .14), (0, 0, .7)))
    P.add("Body", L.rod((0, 0, .7), (0, 0, 1.46), .018, kind="square"))
    P.add("Accent", L.shade(.2, .1, .26, (0, 0, 1.38)))
    P.add("Accent", [s.rcyl(.115, .025, (0, 0, 1.635), seg=8, r=.004, bevel_seg=1)])
    P.add("Eye", [s.rcyl(.14, .02, (0, 0, 1.36), seg=8, r=0)])
    return P


def desk_lamp(L):
    """A clamp work lamp: a steel block base, a square arm with a spring, a caged shade."""
    P = D.Parts()
    P.add("Body", L.slab((.15, .15, .035), (0, 0, .018)))
    P.add("Body", L.rod((0, .02, .03), (0, .04, .23), .012, kind="square") + L.rod((0, .04, .23), (0, -.12, .32), .012, kind="square"))
    P.add("Body", L.box((.035, .035, .035), (0, .04, .23)))
    P.add("Body", [hose([(.02, .025, .06), (.025, .035, .14), (.02, .03, .2)], .008)])
    P.add("Accent", L.shade(.072, .03, .1, (0, -.16, .26)))
    P.add("Eye", [s.ball(.03, (0, -.16, .27), seg=8)])
    return P


def ceiling_lamp(L):
    """A caged fluorescent fitting: a steel tray, two bare tubes, wire guards under them."""
    P = D.Parts()
    P.add("Body", L.slab((1.24, .64, .05), (0, 0, -.025)))
    for y in (-.13, .13):
        P.add("Eye", L.cyl(.022, 1.1, (0, y, -.06), axis="X"))
        P.add("Accent", L.box((.04, .1, .04), (-.57, y, -.055)) + L.box((.04, .1, .04), (.57, y, -.055)))
    for y in (-.29, -.13, .13, .29):
        P.add("Accent", L.box((1.18, .016, .016), (0, y, -.074)))
    for x in (-.4, 0, .4):
        P.add("Accent", L.box((.016, .6, .016), (x, 0, -.074)))
    for x in (-.6, .6):
        P.add("Accent", L.box((.03, .62, .04), (x, 0, -.06)))
    return P


def server_rack(L):
    """A steel cage rack: angle uprights, a wire-mesh door of bars, blinking units behind."""
    P = D.Parts()
    P.add("Body", L.slab((.7, 1.0, .06), (0, 0, .03)) + L.slab((.7, 1.0, .06), (0, 0, 1.97)))
    for x in (-.32, .32):
        for y in (-.47, .47):
            P.add("Body", L.box((.06, .06, 1.9), (x, y, 1.0)))
    P.add("Body", L.slab((.66, .03, 1.88), (0, .48, 1.0)) + L.slab((.03, .94, 1.88), (-.335, 0, 1.0)) + L.slab((.03, .94, 1.88), (.335, 0, 1.0)))
    for i in range(11):
        z = .22 + i * .16
        P.add("Body", L.slab((.66, .8, .11), (0, .02, z)))
        P.add("Eye", L.slab((.5, .02, .04), (0, -.385, z)))
    for x in (-.24, -.12, 0, .12, .24):
        P.add("Body", L.box((.02, .02, 1.86), (x, -.5, 1.0)))
    for z in (.1, .7, 1.3, 1.9):
        P.add("Body", L.box((.66, .025, .025), (0, -.5, z)))
    return P


def water_cooler(L):
    """A cooler with a jerrycan bottle held in a wire cage, a striped drip tray."""
    P = D.Parts()
    P.add("Body", L.carcass((.34, .34, 1.0), (0, 0, 0), kick=.03))
    P.add("Body", L.slab((.2, .05, .04), (0, -.17, .55)) + L.box((.03, .04, .06), (-.05, -.18, .62)) + L.box((.03, .04, .06), (.05, -.18, .62)))
    P.add("Eye", [s.lathe([(0, 0), (.13, 0), (.15, .05), (.15, .35), (.1, .43), (.05, .45), (0, .45)], seg=8, loc=(0, 0, .99))])
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        P.add("Body", L.box((.018, .018, .38), (math.cos(a) * .155, math.sin(a) * .155, 1.17)))
    P.add("Body", [s.lathe([(.14, 0), (.165, 0), (.165, .02), (.14, .02)], seg=8, loc=(0, 0, 1.33))])
    return P


def coffee_machine(L):
    """A dented eight-sided steel urn on a block base: a tap, a striped drip tray, a lit gauge glass."""
    P = D.Parts()
    P.add("Body", L.slab((.32, .36, .06), (0, 0, .03)))
    P.add("Body", [s.lathe([(0, .06), (.14, .06), (.15, .1), (.15, .42), (.12, .47), (0, .5)], seg=8)])
    P.add("Body", L.box((.04, .04, .04), (0, 0, .49)))
    P.add("Accent", L.slab((.2, .12, .03), (0, -.17, .08)) + L.box((.04, .06, .04), (0, -.17, .2)) + L.cyl(.02, .06, (0, -.18, .16)))
    P.add("Accent", L.box((.06, .03, .1), (0, -.15, .3)))
    P.add("Eye", L.slab((.05, .02, .1), (0, -.15, .37)))
    return P


def shelf(L):
    """A warehouse rack of square tube with X-bracing at the back; the boxes rest on the shelves."""
    P = D.Parts()
    for x in (-.97, .97):
        for y in (-.27, .27):
            P.add("Body", L.leg((x, y, 2.0), 2.0, .05))
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", L.slab((2.0, .6, .04), (0, 0, h)))
    for a, b in (((-.95, .28, .1), (.95, .28, 1.62)), ((.95, .28, .1), (-.95, .28, 1.62))):
        P.add("Body", [bar(a, b, .035, .02, (0, -1, 0), r=.003)])
    P.add("Accent", L.slab((.45, .4, .3), (-.6, 0, .618)) + L.slab((.35, .35, .25), (.5, .05, .593))
          + L.slab((.5, .45, .35), (.3, 0, 1.243)) + L.slab((.4, .4, .3), (-.55, 0, 1.818)))
    return P


def pallet(L):
    """A pallet of crates under steel strapping."""
    P = D.Parts()
    P.add("Accent", L.slab((1.2, 1.0, .04), (0, 0, .12)))
    for y in (-.45, 0, .45):
        P.add("Accent", L.slab((1.2, .1, .105), (0, y, .0525)))
    P.add("Body", sum([L.slab((.55, .45, .4), (x, y, .337)) for x in (-.3, .3) for y in (-.24, .24)], [])
          + sum([L.slab((.52, .42, .4), (-.3, y, .732)) for y in (-.24, .24)], []))
    for x in (-.3, .3):
        P.add("Accent", L.box((.04, 1.0, .02), (x, 0, .545)))
    return P


def armchair(L):
    """A machine seat on a square-tube frame: vinyl pads, tube armrests, floor runners."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .41
        P.add("Accent", L.box((.05, .05, .6), (x, -.32, .3)) + L.box((.05, .05, .6), (x, .32, .3)))
        P.add("Accent", L.box((.05, .72, .05), (x, 0, .58)) + L.box((.05, .72, .05), (x, 0, .03)))
        P.add("Accent", L.box((.05, .05, .44), (sx * .25, .35, .58)))
    P.add("Accent", L.box((.84, .05, .05), (0, -.32, .36)) + L.box((.84, .05, .05), (0, .32, .36)))
    P.add("Body", L.cushion((.74, .64, .12), (0, -.02, .44)))
    P.add("Body", L.cushion((.66, .12, .48), (0, .31, .62), rot=(math.radians(-10), 0, 0)))
    return P


def sofa(L):
    """A vehicle bench seat bolted to a square-tube frame with tube arms."""
    P = D.Parts()
    for x in (-.95, 0, .95):
        for y in (-.33, .33):
            P.add("Accent", L.box((.05, .05, .4), (x, y, .2)))
    for y in (-.33, .33):
        P.add("Accent", L.box((2.0, .05, .05), (0, y, .37)))
    for sx in (-1, 1):
        x = sx * 1.06
        P.add("Accent", L.box((.05, .05, .62), (x, -.36, .31)) + L.box((.05, .05, .62), (x, .36, .31)))
        P.add("Accent", L.box((.05, .8, .05), (x, 0, .6)) + L.box((.18, .05, .05), (sx * .99, -.36, .2)) + L.box((.18, .05, .05), (sx * .99, .36, .2)))
        P.add("Accent", L.box((.05, .05, .5), (sx * .6, .37, .6)))
    P.add("Body", L.cushion((1.94, .72, .14), (0, -.04, .46)))
    P.add("Body", L.cushion((1.94, .14, .42), (0, .35, .67), rot=(math.radians(-8), 0, 0)))
    return P


def bean_bag(L):
    """Two old tyres stacked with a lumpy tarp cushion thrown on top."""
    P = D.Parts()
    for z, r in ((0, .45), (.21, .43)):
        P.add("Body", [s.lathe([(.22, 0), (r - .05, 0), (r, .06), (r, .16), (r - .05, .22), (.22, .22)], seg=10, loc=(0, 0, z))])
    P.add("Body", [s.loft([(.42, .36, .34, 2.4), (.52, .38, .37, 2.4, .02, 0), (.64, .26, .25, 2.2, -.02, .02), (.69, .1, .1, 2.0)], seg=8)])
    return P


def bookshelf(L):
    """Steel shelving: angle uprights, a wire back, binders and a hard hat."""
    P = D.Parts()
    for x in (-.47, .47):
        for y in (-.15, .15):
            P.add("Body", L.box((.05, .05, 2.0), (x, y, 1.0)))
    for h in (.04, .5, 1.0, 1.5, 1.97):
        P.add("Body", L.slab((1.0, .36, .03), (0, 0, h)))
    for z in (.27, .75, 1.25, 1.73):
        P.add("Body", L.box((.92, .015, .015), (0, .16, z)))
    for x in (-.3, 0, .3):
        P.add("Body", L.box((.015, .015, 1.9), (x, .16, 1.0)))
    for i, h in enumerate((.055, .515, 1.015)):
        x = -.42
        for k in range(5):
            w = .1 + .03 * ((k + i) % 2)
            P.add("Accent", L.slab((w, .26, .3), (x + w / 2, .0, h + .15)))
            x += w + .02
    P.add("Accent", [s.lathe([(.14, 0), (.15, .01), (.12, .08), (.06, .13), (0, .14)], seg=8, loc=(.1, 0, 1.515))])
    return P


def boxes(L):
    """Crates with batten frames, corner posts and stencil plates."""
    P = D.Parts()
    for (w, d, h), (x, y, z) in (((.6, .6, .5), (0, 0, .25)), ((.5, .5, .4), (.05, -.05, .7)), ((.45, .45, .4), (.55, .1, .2))):
        P.add("Accent", L.slab((w - .04, d - .04, h), (x, y, z)))
        for dz in (-1, 1):
            P.add("Accent", L.slab((w, d, .05), (x, y, z + dz * (h / 2 - .025))))
        for sx in (-1, 1):
            for sy in (-1, 1):
                P.add("Accent", L.box((.045, .045, h - .08), (x + sx * (w / 2 - .022), y + sy * (d / 2 - .022), z)))
        P.add("Accent", L.box((w * .4, .015, h * .25), (x, y - d / 2 + .012, z)))
    return P


def cabinet(L):
    """A rolling steel tool chest: five drawers with full-width bar pulls, a padlock hasp, casters."""
    P = D.Parts()
    P.add("Body", L.slab((.9, .5, 1.28), (0, 0, .76)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Accent", L.box((.05, .06, .1), (sx * .38, sy * .18, .07)) + L.cyl(.04, .03, (sx * .38, sy * .18, .045), axis="X"))
    for i in range(5):
        z = .26 + i * .22
        P.add("Body", L.door((.84, .2), (0, -.255, z)))
        P.add("Accent", L.handle((0, -.27, z + .05), .6, vertical=False))
    P.add("Accent", L.box((.05, .03, .1), (.3, -.26, 1.3)))
    P.add("Accent", L.box((.03, .05, .3), (.46, 0, 1.1)) + L.box((.03, .05, .3), (-.46, 0, 1.1)))
    return P


def coffee_table(L):
    """A cable reel on its side with a plank on top."""
    P = D.Parts()
    for x in (-.42, .42):
        P.add("Accent", L.cyl(.19, .05, (x, 0, .19), axis="X", seg=10))
    P.add("Accent", L.cyl(.09, .8, (0, 0, .19), axis="X"))
    P.add("Accent", L.cyl(.15, .7, (0, 0, .19), axis="X"))
    P.add("Body", L.slab((1.0, .56, .045), (0, 0, .397)))
    return P


def desk(L):
    """A board on two trestles, a deep tube monitor, a keyboard."""
    P = D.Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    for x in (-.6, .6):
        P.add("Accent", L.box((.08, .7, .06), (x, 0, .685)))
        for y in (-1, 1):
            for dx in (-1, 1):
                P.add("Accent", [bar((x, y * .3, .66), (x + dx * .12, y * .34, .0), .04, .04, (1, 0, 0), r=.004)])
    acc, glow = L.screen((.56, .34), (0, .17, .98), "-Y")
    P.add("Accent", acc + L.slab((.3, .26, .1), (0, .2, .81)))
    P.add("Eye", glow)
    P.add("Accent", L.slab((.44, .15, .02), (0, -.12, .775)))
    return P


def lockers(L):
    """Steel lockers: one door ajar, padlocks on the others, vent slots, stencil plates."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .5, 1.9), (0, 0, 0), kick=.06))
    for k, x in enumerate((-.3, 0.0, .3)):
        door = L.door((.29, 1.8), (x, -.255, .98))
        if k == 1:
            for bm in door:
                bmesh.ops.rotate(bm, cent=(x - .145, -.255, .98), matrix=Matrix.Rotation(math.radians(-9), 3, "Z"), verts=bm.verts)
            P.add("Body", door)
            continue
        P.add("Body", door)
        P.add("Accent", L.handle((x + .1, -.27, 1.0), .12))
        P.add("Accent", L.box((.05, .04, .06), (x + .1, -.285, .9)) + [s.tube([(x + .085, -.285, .925), (x + .1, -.285, .96), (x + .115, -.285, .925)], .006, res=1)])
        for j in range(3):
            P.add("Accent", L.slab((.18, .015, .02), (x, -.27, 1.75 - j * .045)))
        P.add("Accent", L.slab((.1, .015, .06), (x, -.27, 1.55)))
    return P


def meeting_table(L):
    """A plywood sheet on two sawhorses."""
    P = D.Parts()
    P.add("Body", L.slab((3.2, 1.3, .04), (0, 0, .76)))
    for x in (-1.1, 1.1):
        P.add("Accent", L.box((.1, 1.1, .08), (x, 0, .7)))
        for y in (-1, 1):
            for dx in (-1, 1):
                P.add("Accent", [bar((x, y * .48, .68), (x + dx * .14, y * .5, 0), .05, .04, (0, 1, 0), r=.004)])
    return P


def round_table(L):
    """A cable reel stood on end: a big top flange, a drum wound with cable, a foot flange."""
    P = D.Parts()
    P.add("Body", [s.rcyl(.5, .04, (0, 0, .74), seg=10, r=.008, bevel_seg=1)])
    P.add("Accent", [s.rcyl(.42, .08, (0, 0, .04), seg=10, r=.008, bevel_seg=1)])
    P.add("Accent", L.cyl(.18, .66, (0, 0, .41)))
    for z in (.2, .34, .48, .62):
        P.add("Accent", L.cyl(.24, .1, (0, 0, z)))
    return P


def partition(L):
    """A construction fence panel: square-tube frame, wire mesh, block feet, a striped sign."""
    P = D.Parts()
    P.add("Accent", L.box((1.6, .04, .04), (0, 0, 1.24)) + L.box((1.6, .04, .04), (0, 0, .08)))
    for x in (-.78, .78):
        P.add("Accent", L.box((.04, .04, 1.2), (x, 0, .66)))
        P.add("Accent", L.box((.16, .055, .08), (x * .9, 0, .04)))
    for k in range(15):
        P.add("Body", L.box((.012, .012, 1.14), (-.7 + k * .1, 0, .66)))
    for z in (.3, .55, .8, 1.05):
        P.add("Body", L.box((1.54, .012, .012), (0, 0, z)))
    P.add("Accent", L.box((.36, .03, .22), (0, -.015, .8)))
    P.add("Body", stripes((0, -.03, .8), .34, .2, (0, -1, 0), width=.035))
    return P


def kitchen_counter(L):
    """A steel workbench: square legs, a lower shelf, two drawer boxes, a vice, the microwave on top."""
    P = D.Parts()
    P.add("Accent", L.slab((2.44, .7, .05), (0, 0, .905)))
    for x in (-1.17, 0, 1.17):
        for y in (-.3, .3):
            P.add("Body", L.box((.05, .05, .88), (x, y, .44)))
    P.add("Body", L.slab((2.36, .62, .03), (0, 0, .2)))
    for x in (-.65, .65):
        P.add("Body", L.slab((.6, .6, .36), (x, 0, .71)))
        for z in (.62, .78):
            P.add("Body", L.door((.56, .14), (x, -.305, z)))
            P.add("Accent", L.handle((x, -.32, z), .3, vertical=False))
    P.add("Accent", L.box((.14, .14, .1), (-1.0, -.27, .98)) + L.box((.04, .08, .04), (-1.0, -.33, .98)))
    P.add("Eye", L.slab((.5, .42, .3), (.7, .02, 1.08)))
    P.add("Body", L.slab((.54, .44, .04), (.7, .02, 1.25)))
    return P


def tv_stand(L):
    """An AV cart on casters carrying a two-by-two bank of tube monitors."""
    P = D.Parts()
    for z in (.12, .48):
        P.add("Body", L.slab((1.4, .4, .03), (0, 0, z)))
    for x in (-.67, .67):
        for y in (-.17, .17):
            P.add("Body", L.box((.04, .04, .48), (x, y, .27)) + L.cyl(.035, .03, (x, y, .035), axis="X"))
    for x in (-.34, .34):
        for z in (.73, 1.2):
            P.add("Accent", L.slab((.66, .2, .47), (x, .08, z)))
            P.add("Eye", L.slab((.54, .01, .32), (x, -.025, z)))
    return P


def toilet_stall(L):
    """A portable site toilet: ribbed walls, a roof, the door swung half open inwards."""
    P = D.Parts()
    for x in (-.5, .5):
        P.add("Body", L.slab((.04, 1.5, 1.9), (x, 0, .97)))
        for y in (-.5, -.1, .3, .65):
            P.add("Body", L.box((.03, .05, 1.8), (x * 1.05, y, .97)))
    P.add("Body", L.slab((1.04, .04, 1.9), (0, .75, .97)))
    P.add("Body", L.slab((1.08, 1.56, .08), (0, 0, 1.955)))
    P.add("Body", L.slab((1.04, 1.5, .05), (0, 0, .025)))
    door = L.slab((.9, .04, 1.84), (.0, -.73, .97))
    for bm in door:
        bmesh.ops.rotate(bm, cent=(-.45, -.73, .97), matrix=Matrix.Rotation(math.radians(30), 3, "Z"), verts=bm.verts)
    P.add("Body", door)
    P.add("Accent", L.slab((.6, .45, .42), (0, .5, .21)) + L.slab((.5, .3, .04), (0, .45, .43)))
    return P


def whiteboard(L):
    """A site notice board of corrugated sheet on square posts, a striped header, pinned sheets."""
    P = D.Parts()
    board = L.slab((1.8, .04, 1.0), (0, 0, 1.5))[0]
    for k in range(-8, 9):
        s.groove(board, "X", k * .1, .02, .006)
    P.add("Body", [board])
    for x in (-.92, .92):
        P.add("Accent", L.box((.05, .05, 2.0), (x, 0, 1.0)) + L.box((.08, .08, .02), (x, 0, .01)))
    P.add("Accent", L.slab((1.86, .05, .1), (0, 0, 1.95)))
    P.add("Body", stripes((0, -.025, 1.95), 1.84, .09, (0, -1, 0), width=.04))
    for x, z in ((-.5, 1.6), (-.1, 1.5), (.4, 1.62)):
        P.add("Accent", L.box((.21, .012, .29), (x, -.025, z)))
    return P


def printer(L):
    """A dot-matrix printer on a steel stand, tractor paper spilling into a basket."""
    P = D.Parts()
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", L.box((.04, .04, .7), (sx * .27, sy * .27, .35)))
    P.add("Body", L.slab((.6, .6, .03), (0, 0, .15)) + L.slab((.6, .6, .03), (0, 0, .69)))
    P.add("Body", L.slab((.56, .44, .22), (0, .02, .81)))
    P.add("Accent", L.slab((.4, .3, .15), (0, .05, .235)))
    pts = [(0, .1, .9), (0, -.05, 1.04), (0, -.22, .98), (0, -.36, .8), (0, -.44, .55), (0, -.46, .3)]
    for a, b in zip(pts, pts[1:]):
        P.add("Accent", [bar(a, b, .4, .012, (0, -1, .2), r=.002)])
    P.add("Accent", L.slab((.44, .12, .16), (0, -.42, .24)))
    P.add("Eye", L.slab((.16, .03, .06), (.18, -.21, .86)))
    return P


def fridge(L):
    """An industrial cold-store cabinet: a heavy door with a lever latch, hinge blocks, a vent grille, a striped kick."""
    P = D.Parts()
    P.add("Body", L.carcass((.7, .7, 1.8), (0, 0, 0), kick=.06))
    P.add("Body", L.slab((.66, .04, 1.5), (0, -.36, .86)))
    P.add("Accent", L.box((.05, .05, .5), (.25, -.39, 1.0)) + L.box((.08, .05, .1), (.25, -.39, 1.28)))
    P.add("Accent", L.box((.08, .05, .05), (.25, -.4, .78)))
    for z in (.3, 1.4):
        P.add("Accent", L.box((.06, .06, .14), (-.34, -.37, z)))
    for k in range(5):
        P.add("Accent", L.box((.5, .03, .02), (0, -.36, 1.66 + k * .03)))
    P.add("Accent", stripes((0, -.38, .2), .6, .12, (0, -1, 0), width=.035))
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "security_camera": security_camera, "sensor_pir": pir, "sensor_plate": plate, "sensor_lidar": lidar,
    "arcade_machine": arcade_machine,
    "vending_machine": vending_machine,
    "safe": safe,
    "plant": plant,
    "floor_lamp": floor_lamp,
    "desk_lamp": desk_lamp,
    "ceiling_lamp": ceiling_lamp,
    "server_rack": server_rack,
    "water_cooler": water_cooler,
    "coffee_machine": coffee_machine,
    "shelf": shelf,
    "pallet": pallet,
    "armchair": armchair,
    "sofa": sofa,
    "bean_bag": bean_bag,
    "bookshelf": bookshelf,
    "boxes": boxes,
    "cabinet": cabinet,
    "coffee_table": coffee_table,
    "desk": desk,
    "lockers": lockers,
    "meeting_table": meeting_table,
    "round_table": round_table,
    "partition": partition,
    "kitchen_counter": kitchen_counter,
    "tv_stand": tv_stand,
    "toilet_stall": toilet_stall,
    "whiteboard": whiteboard,
    "printer": printer,
    "fridge": fridge,
}
