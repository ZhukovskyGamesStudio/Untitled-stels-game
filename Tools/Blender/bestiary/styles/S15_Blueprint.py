"""S15 Blueprint — «Синька»: drafting automata walking out of technical drawings.

Language: everything is drawn with a ruler and a compass — open frames of square beams, flat plates cut as set
squares and protractors, rulers for limbs, compass legs on needle points, circles as wheels and joints (pivot discs),
crosshair lenses (Eye), and dimension lines with arrowheads standing over the machines like annotations.
Few round forms, and those are true circles; no lofted organic shells.
"""
import math

import bmesh
from mathutils import Euler, Vector

import shapes as s
from lang import Lang, align
import designs as D

TAU = math.tau
BEAM = .06


class Style(Lang):
    name = "S15_Blueprint"
    seg = 16
    soft = 0.0
    edge = 0.008
    hull_kind = "frame"
    limb_kind = "square"
    eye_kind = "lens"
    wheel_kind = "spoke"

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        # joints and knobs: a sphere costs ~1k triangles at the language's 32 segments
        return [s.ball(radius, loc, seg=16 if radius > .05 else 12, scale=scale)]

    # ------------------------------------------------------------------ furniture: drafting frames with thin panels
    edge_kind = "sharp"
    handle_kind = "bar"

    def slab(self, size, loc, rot=(0, 0, 0)):
        """A board drawn with a ruler: an outline of square bars with a thin panel inside it (thin boards), or a box
        outlined by its twelve edges round a slightly sunk solid (thick blocks)."""
        out = drawn_box(size)
        if any(rot):
            for bm in out:
                s.place(bm, rot=rot)
        for bm in out:
            s.place(bm, loc)
        return out

    def carcass(self, size, loc, kick=0.06):
        """An outlined box standing on four short square feet instead of a plinth."""
        w, d, h = size
        x, y, z = loc
        out = self.slab((w, d, h - kick + .01), (x, y, z + kick + (h - kick) / 2 - .005))
        # the side faces are cross-braced like a drawing of a crate: an X of bars on each side
        z0, z1 = z + kick + .04, z + h - .04
        for sx in (-1, 1):
            xx = x + sx * (w / 2 - .006)
            if d > .25 and h - kick > .3:
                out += beam((xx, y - d / 2 + .04, z0), (xx, y + d / 2 - .04, z1), w=.022, d=.022, ext=False)
                out += beam((xx, y + d / 2 - .04, z0), (xx, y - d / 2 + .04, z1), w=.02, d=.02, ext=False)
        if kick > 0:
            b = min(.05, w * .15, d * .15)
            for sx in (-1, 1):
                for sy in (-1, 1):
                    out += [s.rbox((b, b, kick + .02), (x + sx * (w / 2 - b * .7), y + sy * (d / 2 - b * .7), z + (kick + .02) / 2), r=.003, seg=1)]
        return out

    def door(self, size, loc, face="-Y"):
        w, h = size
        x, y, z = loc
        return self.slab((w - 0.03, 0.022, h - 0.03), (x, y, z))

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A square beam with a small foot plate."""
        x, y, z = top
        foot = (x + splay[0], y + splay[1], max(0.0, z - length))
        return beam((x, y, z + .025), (foot[0], foot[1], foot[2] + .01), w=thick * .8, ext=False) + \
            [s.rbox((thick * 1.5, thick * 1.5, .012), (foot[0], foot[1], foot[2] + .006), r=.002, seg=1)]

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        """Four square legs, each end pair cross-braced like a trestle."""
        out = []
        xs, ys = half_w - inset, half_d - inset
        for sx in (-1, 1):
            for sy in (-1, 1):
                out += self.leg((sx * xs, sy * ys, top_z), length, thick)
            z0, z1 = top_z - length + .08, top_z - .06
            out += beam((sx * xs, -ys, z0), (sx * xs, ys, z1), w=thick * .45, ext=False) + \
                beam((sx * xs, ys, z0), (sx * xs, -ys, z1), w=thick * .4, ext=False)
        return out

    def cushion(self, size, loc, rot=(0, 0, 0)):
        return self.slab(size, loc, rot)

    def shade(self, r_bottom, r_top, height, loc):
        """A cone drawn as a square frustum with a circle round its rim."""
        x, y, z = loc
        return [s.rcyl(r_bottom * .95, height, (x, y, z + height / 2), seg=4, r=0, radius2=r_top, rot=(0, 0, math.pi / 4))] + \
            ring(r_bottom * .85, r_bottom, .02, (x, y, z + .01), seg=24)

    def pot(self, r, h, loc):
        x, y, z = loc
        return drawn_box((r * 1.6, r * 1.6, h), taper=(1.2, 1.2), at=(x, y, z + h / 2))


    def signature(self, model, P):
        if model == "attach_eye":    # the camera module gets a crosshair
            crosshair(P, (0, -.11, 0), .05)
        elif model == "attach_seismic":
            P.add("Accent", arrow((0, 0, -.005), (0, 0, .06), .045))
        elif model == "attach_nose":
            P.add("Accent", beam((-.07, -.12, .05), (.07, -.12, .05), w=.025) + beam((0, -.12, .04), (0, -.12, .07), w=.02))
        return P


# =========================================================================== helpers
def drawn_box(size, taper=(1.0, 1.0), at=(0, 0, 0)):
    """The ruler's box: thin in one direction -> an outline of bars round a thin panel; thick -> twelve edge bars
    round a sunk solid. Bar widths differ by direction so no two faces share a plane."""
    w, d, h = size
    m = min(size)
    out = []
    if m < .08:
        axis = list(size).index(m)
        others = [i for i in range(3) if i != axis]
        u, v = others
        U, V = size[u], size[v]
        bar = min(.035, U * .2, V * .2)
        if bar < .012 or (U < .1 or V < .1):
            return [s.rbox(size, at, r=min(.003, m * .3), seg=1)]
        def box(du, dv, cu, cv, k, shift=0.0):
            sz = [0, 0, 0]
            c = [0, 0, 0]
            sz[axis] = m * k
            sz[u], sz[v] = du, dv
            c[u], c[v] = cu, cv
            c[axis] = shift
            return s.rbox(tuple(sz), tuple(c), r=0, seg=1)
        for sv in (-1, 1):
            out.append(box(U, bar, 0, sv * (V / 2 - bar / 2), 1.0))
        for su in (-1, 1):
            out.append(box(bar * .9, V - bar * 1.2, su * (U / 2 - bar / 2), 0, .92))
        # the panel is flush (1 mm under) with the face things rest on: the top of a board, the front of a door
        flush = {2: 1, 1: -1}.get(axis, 0) * (.15 * m - .001)
        out.append(box(U - bar * 1.4, V - bar * 1.4, 0, 0, .7, flush))
    else:
        bar = min(.04, m * .2)
        for sx in (-1, 1):
            for sy in (-1, 1):
                out.append(s.rbox((bar, bar, h), (sx * (w / 2 - bar / 2), sy * (d / 2 - bar / 2), 0), r=0, seg=1))
            for sz in (-1, 1):
                out.append(s.rbox((bar * .9, d - bar * .2, bar * .9), (sx * (w / 2 - bar / 2), 0, sz * (h / 2 - bar / 2)), r=0, seg=1))
        for sy in (-1, 1):
            for sz in (-1, 1):
                out.append(s.rbox((w - bar * .2, bar * .8, bar * .8), (0, sy * (d / 2 - bar / 2), sz * (h / 2 - bar / 2)), r=0, seg=1))
        out.append(s.rbox((w - bar * .2, d - bar * .2, h - bar * .2), (0, 0, 0), r=0, seg=1))
    if taper != (1.0, 1.0):
        for bm in out:
            for vtx in bm.verts:
                k = .5 + vtx.co.z / h
                vtx.co.x *= 1 + (taper[0] - 1) * k
                vtx.co.y *= 1 + (taper[1] - 1) * k
    for bm in out:
        s.place(bm, at)
    return out



def beam(p0, p1, w=BEAM, d=None, ext=True):
    """A square (or w x d) bar from p0 to p1, overrunning each end by half its width so corners close."""
    p0, p1 = Vector(p0), Vector(p1)
    n = (p1 - p0).length + (w if ext else 0)
    return [align(s.rbox((w, d or w, n), r=min(.006, w * .2), seg=1), p0, p1)]


def taper_beam(p0, p1, w0, w1):
    n = (Vector(p1) - Vector(p0)).length
    bm = s.rbox((w0, w0, n), r=.005, seg=1, taper=(w1 / w0, w1 / w0))
    return [align(bm, p0, p1)]


def revolve(profile, seg=32, axis="Z", loc=(0, 0, 0)):
    """Solid of revolution from a CLOSED (r, z) profile: true circles drawn with a compass (rings, washers)."""
    bm = bmesh.new()
    rows = [[bm.verts.new((r * math.cos(k / seg * TAU), r * math.sin(k / seg * TAU), z)) for k in range(seg)] for r, z in profile]
    for i in range(len(rows)):
        a, b = rows[i], rows[(i + 1) % len(rows)]
        for j in range(seg):
            bm.faces.new((a[j], a[(j + 1) % seg], b[(j + 1) % seg], b[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    s.place(bm, rot=s._axis_rot(axis))
    return s.place(bm, loc)


def ring(r0, r1, t, loc, axis="Z", seg=40):
    """A flat circle of width r1 - r0 and thickness t (along axis), centred at loc."""
    return [revolve([(r0, -t / 2), (r1, -t / 2), (r1, t / 2), (r0, t / 2)], seg, axis, loc)]


def disc(r, t, loc, axis="Z", seg=32):
    return [s.rcyl(r, t, loc, axis=axis, seg=seg, r=min(.006, t * .3), bevel_seg=1)]


def poly_plate(pts, t, off=0.0):
    """A flat plate cut along a polygon of (x, z) points with thickness t along y (centred on y = off): set squares,
    protractors, rulers."""
    bm = bmesh.new()
    lo_ = [bm.verts.new((u, off - t / 2, v)) for u, v in pts]
    hi_ = [bm.verts.new((u, off + t / 2, v)) for u, v in pts]
    n = len(pts)
    bm.faces.new(lo_)
    bm.faces.new(list(reversed(hi_)))
    for i in range(n):
        bm.faces.new((lo_[i], lo_[(i + 1) % n], hi_[(i + 1) % n], hi_[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def half_disc(c, r, t, plane="XZ", seg=16, up=True):
    """A protractor: a half disc with its straight edge through c."""
    cu, cv = c
    sgn = 1 if up else -1
    pts = [(cu + r * math.cos(k / seg * math.pi), cv + sgn * r * math.sin(k / seg * math.pi)) for k in range(seg + 1)]
    return pts


def crosshair(P, loc, r, axis="-Y", role="Eye", t=.02):
    """A crosshair lens: a circle and two crossing hairs, glowing."""
    c = Vector(loc)
    P.add(role, ring(r - .028, r, t, c, axis=axis, seg=32))
    if axis.endswith("Y"):
        P.add(role, beam(c - Vector((r - .01, 0, 0)), c + Vector((r - .01, 0, 0)), w=.022, d=t, ext=False)
              + beam(c - Vector((0, 0, r - .01)), c + Vector((0, 0, r - .01)), w=.022, d=t, ext=False))
    elif axis.endswith("Z"):
        P.add(role, beam(c - Vector((r - .01, 0, 0)), c + Vector((r - .01, 0, 0)), w=.022, d=t, ext=False)
              + beam(c - Vector((0, r - .01, 0)), c + Vector((0, r - .01, 0)), w=.022, d=t, ext=False))
    else:
        P.add(role, beam(c - Vector((0, r - .01, 0)), c + Vector((0, r - .01, 0)), w=t, d=.022, ext=False)
              + beam(c - Vector((0, 0, r - .01)), c + Vector((0, 0, r - .01)), w=t, d=.022, ext=False))
    return P


def arrow(tip, back, w=.05):
    """An arrowhead (four-sided cone) pointing from back to tip."""
    tip, back = Vector(tip), Vector(back)
    bm = s.rcyl(w, (tip - back).length, seg=4, r=0, radius2=0.0)
    return [align(bm, back, tip)]


def dim_line(P, p0, p1, role="Eye", w=.022, head=.08, ticks=None):
    """A dimension line: a thin rule with arrowheads at both ends (and optional extension lines down to the part)."""
    p0, p1 = Vector(p0), Vector(p1)
    d = (p1 - p0).normalized()
    P.add(role, beam(p0 + d * head * .8, p1 - d * head * .8, w=w, ext=False))
    P.add(role, arrow(p0, p0 + d * head, .035) + arrow(p1, p1 - d * head, .035))
    for a, b in ticks or []:
        P.add("Body", beam(a, b, w=.022))
    return P


def frame_box(P, size, loc, w=BEAM, role="Body"):
    sx, sy, sz = size
    x, y, z = loc
    for i in (-1, 1):
        for j in (-1, 1):
            P.add(role, beam((x + i * sx / 2, y + j * sy / 2, z - sz / 2), (x + i * sx / 2, y + j * sy / 2, z + sz / 2), w))
            P.add(role, beam((x - sx / 2, y + i * sy / 2, z + j * sz / 2), (x + sx / 2, y + i * sy / 2, z + j * sz / 2), w))
            P.add(role, beam((x + i * sx / 2, y - sy / 2, z + j * sz / 2), (x + i * sx / 2, y + sy / 2, z + j * sz / 2), w))
    return P


def circle_wheel(c, r, w):
    """A wheel drawn with a compass: a rim circle, two crossed spokes and a hub (symmetric about its centre)."""
    c = Vector(c)
    out = ring(r * .78, r, w, c, axis="X", seg=32)
    for a in (math.pi / 4, -math.pi / 4):
        dv = Vector((0, math.cos(a), math.sin(a))) * r * .82
        out += beam(c - dv, c + dv, w=w * .55, ext=False)
    out += disc(r * .22, w * 1.15, c, axis="X", seg=16)
    return out


# =========================================================================== walkers

def guard(L):
    """The draftsman: a set-square torso (a triangle plate with a round hole) in a beam frame, a drum head with a
    crosshair, ruler arms on pivot discs, and compass legs that stand on needle points."""
    P = D.Parts()
    top, apex = 1.66, 1.0
    plate = poly_plate([(-.38, top), (.38, top), (0, apex)], .04)
    hole = s.rcyl(.1, .2, (0, 0, 1.44), axis="Y", seg=24, r=0)
    P.add("Accent", [s.boolean(plate, hole)])
    for a, b in (((-.4, top), (.4, top)), ((.4, top), (0, apex)), ((0, apex), (-.4, top))):
        P.add("Body", beam((a[0], 0, a[1]), (b[0], 0, b[1]), w=.05, d=.08))
    P.add("Body", beam((-.26, 0, .95), (.26, 0, .95), w=.05, d=.07))
    P.add("Accent", disc(.09, .1, (0, 0, .97), axis="Y"))
    # head: a drum with a crosshair face, a dimension line over it
    P.add("Body", beam((0, 0, top), (0, 0, 1.73), w=.05))
    P.add("Body", ring(.12, .17, .16, (0, -.07, 1.86), axis="Y"))
    crosshair(P, (0, -.16, 1.86), .12)
    dim_line(P, (-.24, -.07, 2.12), (.24, -.07, 2.12), ticks=[((-.17, -.07, 1.86), (-.24, -.07, 2.15)), ((.17, -.07, 1.86), (.24, -.07, 2.15))])
    for sx in (-1, 1):
        sh, el, wr = (sx * .43, 0, 1.62), (sx * .46, -.02, 1.2), (sx * .46, -.1, .86)
        P.add("Accent", disc(.075, .06, sh, axis="X") + disc(.06, .06, el, axis="X"))
        P.add("Body", beam(sh, el, w=.035, d=.08) + beam(el, wr, w=.035, d=.08))
        P.add("Accent", [s.place(poly_plate([(-.07, 0), (.07, 0), (0, -.14)], .035), (sx * .46, -.1, .86))])
    for sx, leg in ((-1, "AccentLeg0"), (1, "AccentLeg1")):
        x = sx * D.GUARD_HIP_X
        foot = (x * 1.2, -.02, 0)
        P.add(leg, disc(.09, .08, (x, 0, D.GUARD_HIP_Z), axis="X"))
        P.add(leg, taper_beam((x, 0, .9), (x * 1.05, 0, D.GUARD_KNEE), .085, .06))
        P.add(leg, disc(.07, .1, (x * 1.05, 0, D.GUARD_KNEE), axis="X"))
        P.add(leg, taper_beam((x * 1.05, 0, D.GUARD_KNEE), (foot[0], foot[1], .12), .06, .045))
        P.add(leg, ring(.0, .05, .04, (foot[0], foot[1], .13)))
        P.add(leg, arrow(foot, (foot[0], foot[1], .14), .03))
    P.split = {"AccentLeg0": D.GUARD_KNEE, "AccentLeg1": D.GUARD_KNEE}
    return P


def hound(L):
    """A truss dog: a triangular bridge truss for a body, a protractor head with a ruler snout, folding-rule legs."""
    P = D.Parts()
    ys = [.42, .21, 0, -.21, -.42]
    for sx in (-1, 1):
        P.add("Body", beam((sx * .13, .44, .66), (sx * .13, -.44, .66)))
    P.add("Body", beam((0, .44, .98), (0, -.44, .98)))
    for k, y in enumerate(ys):
        for sx in (-1, 1):
            if k < len(ys) - 1:
                a, b = ((sx * .13, y, .66), (0, ys[k + 1], .98)) if k % 2 == 0 else ((0, y, .98), (sx * .13, ys[k + 1], .66))
                P.add("Body", beam(a, b, w=.045))
        if k in (0, len(ys) - 1):
            P.add("Body", beam((-.13, y, .66), (.13, y, .66), w=.04) + beam((-.13, y, .66), (0, y, .98), w=.04) + beam((.13, y, .66), (0, y, .98), w=.04))
    P.add("Body", beam((0, -.44, .98), (0, -.6, 1.02), w=.05))
    pro = poly_plate(half_disc((0, 0), .2, .04), .04)
    for v in pro.verts:
        v.co.x, v.co.y = v.co.y, v.co.x
    s.place(pro, (0, -.66, .94))
    P.add("Body", [pro])
    for sx in (-1, 1):
        crosshair(P, (sx * .03, -.66, 1.0), .07, axis="X" if sx > 0 else "-X")
    P.add("Body", beam((0, -.7, .95), (0, -1.08, .9), w=.035, d=.07))
    P.add("Eye", beam((0, -1.08, .92), (0, -1.09, .87), w=.05, d=.05))
    dim_line(P, (0, .5, 1.1), (0, .82, 1.1), ticks=[((0, .44, .98), (0, .5, 1.14))])
    for i, (x, y) in enumerate(D.HOUND_LEGS):
        leg = f"AccentLeg{i}"
        P.add(leg, disc(.075, .05, (x, y, D.HOUND_HIP), axis="X"))
        P.add(leg, beam((x, y, D.HOUND_HIP), (x, y, D.HOUND_KNEE), w=.035, d=.07))
        P.add(leg, disc(.05, .06, (x, y, D.HOUND_KNEE), axis="X"))
        P.add(leg, beam((x, y, D.HOUND_KNEE), (x, y, .04), w=.035, d=.06))
        P.add(leg, beam((x, y - .03, .02), (x, y + .03, .02), w=.08, d=.04, ext=False))
    P.split = {f"AccentLeg{i}": D.HOUND_KNEE for i in range(4)}
    return P


def spider(L):
    """A compass circle twice the size of the body it carries: a solid disc plate in a thick ring, a cross of beams
    and a hub; a crosshair eye in front; beam legs ending in arrowheads."""
    P = D.Parts()
    c = (0, .03, .17)
    P.add("Body", ring(.24, .31, .07, c, seg=48))
    P.add("Accent", ring(.0, .245, .03, (0, .03, .16), seg=48))
    P.add("Body", beam((-.28, .03, .2), (.28, .03, .2)) + beam((0, -.25, .2), (0, .31, .2)))
    P.add("Accent", disc(.08, .16, (0, .03, .2)))
    crosshair(P, (0, -.29, .19), .07)
    for i in range(8):
        hip, knee, foot, d = D.spider_leg(i)
        P.add(f"AccentLeg{i}Upper", disc(.04, .06, hip) + beam(hip, knee, w=.045) + disc(.045, .06, knee))
        tipb = knee + (foot - knee) * .78
        P.add(f"AccentLeg{i}Lower", beam(knee, tipb, w=.04) + arrow(foot + Vector((0, 0, -.02)), tipb - (foot - knee) * .02, .035))
    return P


# =========================================================================== wheeled and tracked

def listener(L):
    """A beam-frame cart on compass wheels; a T-square mast carries two protractor ears with glowing pivots."""
    P = D.Parts()
    W = [(-.43, -.3, .22), (.43, -.3, .22), (-.43, .3, .22), (.43, .3, .22)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", circle_wheel(c, .22, .12))
        P.add("Body", beam((c[0] * .78, c[1], .22), (c[0] * 1.0, c[1], .22), w=.04))
    frame_box(P, (.68, .9, .2), (0, 0, .34))
    P.add("Body", [s.rbox((.7, .92, .03), (0, 0, .45), r=.006, seg=1)])
    P.add("Body", beam((0, .1, .45), (0, .1, 1.24), w=.06))
    P.add("Body", beam((-.32, .1, 1.2), (.32, .1, 1.2), w=.05, d=.08))
    for sx in (-1, 1):
        cx = sx * .32
        P.add("Accent", [poly_plate(half_disc((cx, 1.2), .26, .03), .035, off=.1)])
        P.add("Body", beam((cx, .1, 1.2), (cx + sx * .2, .1, 1.36), w=.03, d=.05))
        crosshair(P, (cx, .074, 1.26), .06)
    P.add("Body", [s.rbox((.3, .04, .14), (0, -.46, .36), r=.006, seg=1)])
    crosshair(P, (0, -.49, .36), .06)
    return P


def worker(L):
    """An open box-frame cart; a lift mast of two beams carries the crate (drawn with its edges) over the nose."""
    P = D.Parts()
    W = [(.31, .25, .12), (-.31, .25, .12), (.31, -.25, .12), (-.31, -.25, .12)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", circle_wheel(c, .12, .1))
        P.add("Body", beam((c[0] * .78, c[1], .12), (c[0] * 1.0, c[1], .12), w=.04))
    frame_box(P, (.5, .66, .2), (0, .03, .26))
    P.add("Body", [s.rbox((.52, .68, .03), (0, .03, .36), r=.006, seg=1)])
    # cab: an upright triangle frame at the back
    for sx in (-1, 1):
        P.add("Body", beam((sx * .2, .36, .36), (sx * .2, .36, .9)) + beam((sx * .2, .36, .9), (sx * .2, .0, .36)))
    P.add("Body", beam((-.2, .36, .9), (.2, .36, .9)))
    for sx in (-1, 1):
        P.add("Body", beam((sx * .23, -.33, .2), (sx * .23, -.33, 1.42)))
    P.add("Body", beam((-.23, -.33, 1.42), (.23, -.33, 1.42)))
    P.add("Accent", [s.rbox((.5, .44, .04), (0, -.15, .93), r=.006, seg=1)])
    P.add("Accent", [s.rbox((.42, .38, .34), (0, -.15, 1.13), r=.008, seg=1)])
    frame_box(P, (.44, .4, .36), (0, -.15, 1.13), w=.035)
    P.add("Body", [s.rbox((.3, .04, .14), (0, -.345, .45), r=.006, seg=1)])
    crosshair(P, (0, -.375, .45), .06)
    return P


def sweeper(L):
    """A compass drawing its own circle: a low ring on three small wheels, a cross of beams, a short compass handle
    in the middle; a ruler bar sweeps in front, the lidar disc turns under the cross."""
    P = D.Parts()
    W = [(-.28, .18, .08), (.28, .18, .08), (0, -.3, .08)]
    for i, c in enumerate(W):
        P.add(f"AccentWheel{i}", circle_wheel(c, .08, .06))
        x, y, _ = c
        P.add("Accent", beam((x - .055, y, .08), (x + .055, y, .08), w=.025))
        for sx in (-1, 1):
            P.add("Accent", beam((x + sx * .05, y, .08), (x + sx * .05, y, .21), w=.03))
    P.add("Body", ring(.34, .41, .06, (0, 0, .21), seg=48))
    for a in range(4):
        ang = a * math.pi / 2 + math.pi / 4
        P.add("Body", beam((math.cos(ang) * .37, math.sin(ang) * .37, .21), (math.cos(ang) * .06, math.sin(ang) * .06, .3), w=.04))
    P.add("Body", beam((-.28, .18, .21), (.28, .18, .21), w=.04) + beam((0, -.3, .21), (0, -.37, .21), w=.04) + beam((-.07, -.3, .21), (.07, -.3, .21), w=.04))
    P.add("Accent", disc(.07, .14, (0, 0, .31)))
    P.add("Body", beam((0, 0, .36), (0, 0, .52), w=.05))
    P.add("Accent", [s.rbox((.09, .09, .1), (0, 0, .55), r=.01, seg=1)])
    P.add("AccentLidar", disc(.12, .07, (0, 0, .22)) + L.box((.06, .06, .04), (0, -.12, .22)))
    P.add("Accent", beam((-.36, -.44, .03), (.36, -.44, .03), w=.05, d=.08))
    for sx in (-1, 1):
        P.add("Accent", beam((sx * .3, -.44, .03), (sx * .27, -.27, .2), w=.03))
    crosshair(P, (0, -.43, .3), .07)
    P.add("Body", beam((0, -.39, .21), (0, -.42, .26), w=.035))
    return P


def thumper(L):
    """An A-frame trestle on two beam-and-circle tracks; two plumb bobs hang from the front and stamp the floor."""
    P = D.Parts()
    for sx in (-1, 1):
        x = sx * .55
        for z in (.05, .5):
            P.add("Accent", beam((x, .66, z), (x, -.66, z), w=.06))
        for y in (-.66, .66):
            P.add("Accent", beam((x, y, .05), (x, y, .5), w=.06))
        for y in (-.44, 0, .44):
            P.add("Accent", ring(.14, .2, .1, (x, y, .21), axis="X", seg=24) + disc(.05, .12, (x, y, .21), axis="X", seg=12)
                  + beam((x, y - .16, .21), (x, y + .16, .21), w=.04, ext=False) + beam((x, y, .05), (x, y, .37), w=.04, ext=False))
    for sx in (-1, 1):
        x = sx * .4
        P.add("Body", beam((x, .55, .5), (x, -.5, .5), w=.06) + beam((x, .55, .5), (x, 0, 1.3), w=.06) + beam((x, -.5, .5), (x, 0, 1.3), w=.06))
        P.add("Body", beam((sx * .4, .2, .5), (sx * .55, .2, .5), w=.06) + beam((sx * .4, -.2, .5), (sx * .55, -.2, .5), w=.06))
        P.add("Accent", [s.place(poly_plate([(.5, .5), (-.45, .5), (0, 1.2)], .04), rot=(0, 0, math.pi / 2), loc=(x, 0, 0))])
    P.add("Body", beam((-.4, 0, 1.3), (.4, 0, 1.3), w=.06) + beam((-.4, -.5, .5), (.4, -.5, .5), w=.06) + beam((-.4, .55, .5), (.4, .55, .5), w=.06))
    for sx in (-1, 1):
        x = sx * .3
        P.add("Body", beam((sx * .4, -.3, .82), (x, -.85, .82), w=.05))
        P.add("Accent", disc(.05, .08, (x, -.85, .82)))
        P.add("Body", beam((x, -.85, .8), (x, -.85, .38), w=.025))
        P.add("Accent", [s.lathe([(0, .03), (.12, .2), (.12, .32), (.06, .38), (0, .4)], seg=24, loc=(x, -.85, 0))])
        P.add("Eye", ring(.0, .18, .04, (x, -.85, .02)))
    P.add("Body", beam((-.4, -.3, .82), (.4, -.3, .82), w=.05))
    crosshair(P, (0, -.33, .9), .08)
    return P


# =========================================================================== tall

def sorter(L):
    """A measuring rod on a square foot carries a big compass circle around the colour lens, crossed by hairs, with
    a dimension line over it."""
    P = D.Parts()
    P.add("Accent", [s.rbox((.56, .56, .06), (0, 0, .03), r=.008, seg=1)])
    frame_box(P, (.5, .5, .04), (0, 0, .08), w=.04)
    P.add("Body", [s.rbox((.1, .045, 1.0), (0, 0, .55), r=.006, seg=1)])
    for k in range(9):
        P.add("Accent", [s.rbox((.05 if k % 2 else .08, .05, .02), (-.05, 0, .14 + k * .1), r=.004, seg=1)])
    P.add("Body", ring(.21, .27, .07, (0, -.03, 1.25), axis="Y", seg=48))
    P.add("Eye", disc(.215, .03, (0, -.04, 1.25), axis="Y", seg=40))
    P.add("Body", beam((-.23, -.065, 1.25), (.23, -.065, 1.25), w=.022, d=.025, ext=False)
          + beam((0, -.065, 1.02), (0, -.065, 1.48), w=.022, d=.025, ext=False))
    dim_line(P, (-.27, -.03, 1.62), (.27, -.03, 1.62), ticks=[((-.27, -.03, 1.25), (-.27, -.03, 1.66)), ((.27, -.03, 1.25), (.27, -.03, 1.66))])
    return P


def hunter(L):
    """The compass: a 2.3 m drafting compass glides on its needle while its pencil leg scribes a glowing circle on
    the floor around it; the hinge carries the crosshair eye, an arc bar holds the spread."""
    P = D.Parts()
    hinge = Vector((0, 0, 1.95))
    needle, pencil = Vector((.22, .05, 0)), Vector((-.45, -.22, 0))
    R = (pencil - needle).length
    P.add("Eye", ring(R - .045, R + .005, .02, (needle.x, needle.y, .01), seg=64))
    P.add("Body", taper_beam(hinge, needle + Vector((0, 0, .16)), .11, .06))
    P.add("Accent", arrow(needle, needle + Vector((0, 0, .17)), .035))
    P.add("Body", taper_beam(hinge, pencil + Vector((0, 0, .25)), .11, .06))
    P.add("Accent", taper_beam(pencil + Vector((0, 0, .3)), pencil + Vector((0, 0, .08)), .07, .07))
    P.add("Accent", arrow(pencil, pencil + Vector((0, 0, .09)), .045))
    a, b = hinge.lerp(needle, .45), hinge.lerp(pencil, .45)
    mid = (a + b) / 2 + Vector((0, 0, -.12))
    P.add("Accent", [s.tube([tuple(a), tuple(mid), tuple(b)], .022, res=1)])
    P.add("Accent", disc(.06, .06, tuple((a + b) / 2 + Vector((0, 0, -.09))), axis="Y"))
    P.add("Accent", disc(.17, .12, tuple(hinge), axis="Y", seg=40))
    P.add("Body", beam(hinge, hinge + Vector((0, 0, .32)), w=.07))
    P.add("Accent", [s.rbox((.12, .12, .14), tuple(hinge + Vector((0, 0, .3))), r=.01, seg=1)])
    crosshair(P, (0, -.08, 1.95), .12)
    P.add("Body", beam((0, -.06, 1.95), (0, -.3, 1.9), w=.04))
    crosshair(P, (0, -.3, 1.9), .06)
    P.add("Body", beam((-.15, 0, 1.7), (.15, 0, 1.7), w=.05))
    for sx in (-1, 1):  # ruler blades held out like arms
        sh = hinge + Vector((sx * .15, 0, -.25))
        P.add("Accent", disc(.06, .06, tuple(sh), axis="X"))
        P.add("Body", beam(sh, sh + Vector((sx * .25, -.15, -.45)), w=.035, d=.09))
        P.add("Accent", beam(sh + Vector((sx * .25, -.15, -.45)), sh + Vector((sx * .27, -.65, -.6)), w=.03, d=.1))
    return P


# =========================================================================== devices

def drone(L):
    """A square plate in a beam frame with a big compass circle on it; diagonal braces run out to the four rotors
    inside their own circles; a crosshair hangs under the nose."""
    P = D.Parts()
    rot = [(.42, .42), (-.42, .42), (.42, -.42), (-.42, -.42)]
    for a, b in (((-.17, -.17), (.17, -.17)), ((.17, -.17), (.17, .17)), ((.17, .17), (-.17, .17)), ((-.17, .17), (-.17, -.17))):
        P.add("Body", beam((a[0], a[1], 0), (b[0], b[1], 0), w=.055))
    P.add("Accent", [s.rbox((.32, .32, .03), (0, 0, .0), r=.006, seg=1)])
    P.add("Eye", ring(.1, .14, .02, (0, 0, .022), seg=40))
    for i, (x, y) in enumerate(rot):
        P.add("Body", beam((x * .4, y * .4, 0), (x, y, .02), w=.05))
        P.add("Body", beam((x * .4, y * .4, 0), (x, y * .62, .02), w=.035) + beam((x * .4, y * .4, 0), (x * .62, y, .02), w=.035))
        P.add("Body", ring(.18, .22, .035, (x, y, .03), seg=32))
        P.add("Body", disc(.03, .05, (x, y, .025), seg=12))
        P.add(f"AccentRotor{i}", L.rotor((x, y, .065), .32))
    P.add("Body", beam((0, -.16, 0), (0, -.2, -.08), w=.045))
    crosshair(P, (0, -.22, -.09), .06)
    return P


def turret(L):
    """Two set squares crossed at right angles stand as a solid plinth, a glowing circle round their waist and
    glowing feet at their corners; the head is a T-square on a pivot disc, its long blade the barrel (it turns)."""
    P = D.Parts()
    for k in range(2):
        tri = poly_plate([(-.36, 0), (.36, 0), (0, 1.02)], .05)
        P.add("Accent" if k == 0 else "Body", [s.place(tri, rot=(0, 0, k * math.pi / 2))])
        for sx in (-1, 1):
            p = (sx * .36, 0, .015) if k == 0 else (0, sx * .36, .015)
            P.add("Eye", disc(.055, .03, p, seg=16))
    P.add("Body", beam((0, 0, .9), (0, 0, 1.1), w=.08))
    P.add("Eye", ring(.13, .18, .035, (0, 0, .5), seg=40))
    P.add("Body", ring(.0, .135, .03, (0, 0, .5), seg=40))
    P.add("AccentHead", disc(.11, .06, (0, 0, 1.13)))
    P.add("AccentHead", [s.rbox((.12, .78, .05), (0, -.17, 1.18), r=.006, seg=1)])
    P.add("AccentHead", [s.rbox((.42, .07, .1), (0, .22, 1.2), r=.006, seg=1)])
    P.add("AccentHead", ring(.035, .065, .04, (0, -.57, 1.27), axis="Y", seg=24))
    P.add("AccentHead", beam((0, -.57, 1.18), (0, -.57, 1.21), w=.05))
    for k in range(7):
        P.add("AccentHead", [s.rbox((.15 if k % 2 else .18, .02, .02), (0, -.5 + k * .1, 1.205), r=.004, seg=1)])
    return P


def vacuum(L):
    """A full circle drawn with a compass, ticked round its rim like a dial, with a radial ruler arm across it; the
    room face (-z) carries a crosshair."""
    P = D.Parts()
    P.add("Body", ring(.29, .36, .1, (0, 0, .04), seg=48))
    P.add("Accent", ring(.0, .3, .04, (0, 0, .05), seg=48))
    P.add("Body", [s.rbox((.06, .4, .05), (0, -.17, .09), r=.006, seg=1)])
    P.add("Body", disc(.05, .08, (0, 0, .09)))
    for k in range(16):
        a = k / 16 * TAU
        P.add("Accent", beam((math.cos(a) * .33, math.sin(a) * .33, -.012), (math.cos(a) * (.37 if k % 4 else .41), math.sin(a) * (.37 if k % 4 else .41), -.012),
                             w=.035, d=.03))
    crosshair(P, (0, 0, .02), .14, axis="-Z", t=.03)
    P.add("Eye", disc(.03, .05, (0, 0, .005), seg=12))
    return P


def lidar(L):
    """A square post on a square foot with a glowing circle; the square drum turned 45 degrees spins on top."""
    P = D.Parts()
    P.add("Body", [s.rbox((.3, .3, .04), (0, 0, .02), r=.006, seg=1)])
    P.add("Body", beam((0, 0, .02), (0, 0, .39), w=.07))
    P.add("Eye", ring(.03, .09, .025, (0, 0, .34)))
    P.add("AccentHead", [s.rbox((.15, .15, .13), (0, 0, .455), r=.008, seg=1, rot=(0, 0, math.pi / 4))])
    P.add("AccentHead", beam((0, -.08, .45), (0, -.11, .45), w=.05))
    return P


def security_camera(L):
    """A box frame camera on a square arm with a crosshair lens."""
    P = D.Parts()
    P.add("Body", [s.rbox((.16, .06, .22), (0, .27, 0), r=.006, seg=1)])
    P.add("Accent", beam((0, .26, 0), (0, .1, -.02), w=.04))
    P.add("Accent", [s.rbox((.145, .27, .135), (0, -.04, -.02), r=.006, seg=1)])
    frame_box(P, (.15, .28, .14), (0, -.04, -.02), w=.025, role="Body")
    P.add("Accent", [s.rcyl(.06, .06, (0, -.2, -.02), axis="Y", seg=16, r=0, radius2=.04)])
    crosshair(P, (0, -.235, -.02), .05)
    return P


def laser_emitter(L):
    """A square post marked like a ruler, the emitter strip glowing on its face."""
    P = D.Parts()
    P.add("Body", [s.rbox((.12, .12, 1.6), (0, 0, .8), r=.006, seg=1)])
    for k in range(8):
        P.add("Accent", [s.rbox((.13, .04 if k % 2 else .07, .02), (0, .03 if k % 2 else .015, .2 + k * .18), r=.004, seg=1)])
    P.add("Eye", L.box((.05, .04, 1.4), (0, -.065, .85), r=.006))
    return P


def plate(L):
    """A floor plate drawn as a square with a crosshair, all lines glowing."""
    P = D.Parts()
    S, h = 1.1, .012
    P.add("Body", L.box((S - .07, S - .07, h), (0, 0, h / 2), r=.003))
    e = h + .004
    b = .035
    P.add("Eye", L.box((S, b, e), (0, S / 2 - b / 2, e / 2), r=.003) + L.box((S, b, e), (0, -S / 2 + b / 2, e / 2), r=.003)
          + L.box((b, S - 2 * b, e), (S / 2 - b / 2, 0, e / 2), r=.003) + L.box((b, S - 2 * b, e), (-S / 2 + b / 2, 0, e / 2), r=.003))
    P.add("Eye", ring(.25, .285, e, (0, 0, e / 2), seg=40))
    P.add("Accent", L.box((S - .2, .03, e - .002), (0, 0, (e - .002) / 2), r=.003) + L.box((.03, S - .2, e - .002), (0, 0, (e - .002) / 2), r=.003))
    return P


def pir(L):
    """A square wall plate with a cone looking out, a crosshair on its face."""
    P = D.Parts()
    P.add("Body", [s.rbox((.16, .06, .16), (0, .04, 0), r=.006, seg=1)])
    frame_box(P, (.16, .02, .16), (0, .0, 0), w=.025, role="Accent")
    P.add("Body", [s.rcyl(.06, .05, (0, -.01, 0), axis="Y", seg=16, r=0, radius2=.045)])
    crosshair(P, (0, -.04, 0), .05)
    return P


def attach_mic(L):
    """A square stalk carrying a circle with a crosshair."""
    P = D.Parts()
    P.add("Accent", [s.rbox((.09, .09, .02), (0, 0, .01), r=.004, seg=1)] + beam((0, 0, .01), (0, 0, .27), w=.03))
    P.add("Body", ring(.045, .07, .04, (0, 0, .32), axis="Y", seg=20))
    crosshair(P, (0, -.025, .32), .045)
    return P


# =========================================================================== furniture and level pieces

def chair(L):
    """A drafting stool: a circle seat on four splayed square legs with a ring footrest, a small drawn backrest on a
    beam."""
    P = D.Parts()
    P.add("Body", ring(.18, .23, .04, (0, 0, .5), seg=32) + disc(.19, .025, (0, 0, .505), seg=32))
    P.add("Body", beam((0, .2, .5), (0, .26, .78), w=.03) + L.slab((.42, .04, .3), (0, .27, .915), rot=(math.radians(-8), 0, 0)))
    for k in range(4):
        a = k * math.pi / 2
        P.add("Accent", beam((math.cos(a) * .14, math.sin(a) * .14, .49), (math.cos(a) * .27, math.sin(a) * .27, .02), w=.035)
              + [s.rbox((.06, .06, .016), (math.cos(a) * .27, math.sin(a) * .27, .008), r=.002, seg=1)])
    P.add("Accent", ring(.19, .23, .03, (0, 0, .22), seg=32))
    return P


def armchair(L):
    """A cube chair drawn with a ruler: open beam frames for arms, a drawn seat and back."""
    P = D.Parts()
    for x in (-.37, .37):
        frame_box(P, (.1, .72, .6), (x, 0, .32), w=.04, role="Accent")
    P.add("Body", L.slab((.62, .66, .12), (0, -.02, .4)) + L.slab((.62, .1, .5), (0, .29, .64)))
    return P


def sofa(L):
    """A long bench drawn as open beam frames: arms, a drawn seat and back, a stretcher under it."""
    P = D.Parts()
    for x in (-1.0, 1.0):
        frame_box(P, (.18, .8, .6), (x, 0, .32), w=.045, role="Accent")
    P.add("Body", L.slab((1.84, .72, .14), (0, -.03, .38)) + L.slab((1.84, .14, .48), (0, .3, .64)))
    P.add("Body", beam((-.92, -.32, .12), (.92, -.32, .12), w=.045) + beam((-.92, .32, .12), (.92, .32, .12), w=.045))
    for x in (-.45, .45):
        P.add("Body", beam((x, -.32, .12), (x, .32, .12), w=.04) + beam((x, -.32, .12), (x, -.32, .32), w=.04))
    return P


def bean_bag(L):
    """A drawn globe: three great circles round a sphere's outline, a flat disc on the floor."""
    P = D.Parts()
    c = (0, 0, .345)
    P.add("Body", ring(.41, .45, .05, c, axis="Z", seg=40))
    for ax in ("X", "Y"):
        P.add("Body", [s.place(ring(.41, .45, .05, (0, 0, 0), axis=ax, seg=40)[0], c, scale=(1, 1, .766))])
    P.add("Body", disc(.3, .03, (0, 0, .015), seg=32) + disc(.12, .03, (0, 0, .675), seg=24))
    P.add("Body", [s.rbox((.6, .6, .35), (0, 0, .2), r=0, seg=1, taper=(1.1, 1.1))])
    return P


def plant(L):
    """A tree drawn with a compass: a square stem, three circle canopies crossing at angles, a square pot."""
    P = D.Parts()
    P.add("Accent", drawn_box((.34, .34, .44), taper=(1.15, 1.15), at=(0, 0, .22)))
    P.add("Body", beam((0, 0, .42), (0, 0, 1.36), w=.04))
    for k, (z, r) in enumerate(((.82, .3), (1.05, .25), (1.18, .2))):
        a = k * math.pi / 3
        cz = z
        bm = ring(r - .05, r, .03, (0, 0, 0), axis="Y", seg=32)[0]
        s.place(bm, (0, 0, cz), (0, 0, a))
        P.add("Body", [bm])
        pan = disc(r - .045, .01, (0, 0, 0), axis="Y", seg=32)[0]
        s.place(pan, (0, 0, cz), (0, 0, a))
        P.add("Body", [pan])
    P.add("Body", beam((-.33, 0, .82), (.33, 0, .82), w=.025) + beam((0, -.33, .82), (0, .33, .82), w=.025))
    return P


def round_table(L):
    """A circle drawn on four splayed square legs with a ring stretcher."""
    P = D.Parts()
    P.add("Body", ring(.44, .5, .04, (0, 0, .74), seg=40) + disc(.45, .02, (0, 0, .748), seg=40))
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        P.add("Accent", beam((math.cos(a) * .3, math.sin(a) * .3, .73), (math.cos(a) * .4, math.sin(a) * .4, .01), w=.045))
    P.add("Accent", ring(.32, .37, .03, (0, 0, .25), seg=32))
    return P


def trash_bin(L):
    """A square bin drawn as a tapered frame."""
    P = D.Parts()
    P.add("Body", drawn_box((.3, .3, .42), taper=(1.2, 1.2), at=(0, 0, .21)))
    P.add("Accent", [s.rbox((.38, .38, .03), (0, 0, .43), r=.003, seg=1)])
    return P


def water_cooler(L):
    """A square beam stand with a drawn front, carrying the bottle in a cage of four bars."""
    P = D.Parts()
    P.add("Body", drawn_box((.34, .34, .98), at=(0, 0, .49)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", beam((sx * .15, sy * .15, .97), (sx * .15, sy * .15, 1.42), w=.025))
    P.add("Body", [s.rbox((.33, .33, .025), (0, 0, 1.43), r=.002, seg=1)])
    P.add("Eye", L.lathe([(0, 0), (.12, 0), (.13, .05), (.13, .33), (.08, .4), (0, .41)], seg=16, loc=(0, 0, .972)))
    return P


def coffee_machine(L):
    """A drawn box with a cone funnel spout and a crosshair for a display."""
    P = D.Parts()
    P.add("Body", drawn_box((.34, .38, .5), at=(0, 0, .25)))
    P.add("Accent", [s.rcyl(.07, .1, (0, -.15, .26), seg=4, r=0, radius2=.02, rot=(math.pi, 0, math.pi / 4))]
          + [s.rbox((.22, .15, .03), (0, -.15, .015), r=.002, seg=1)])
    crosshair(P, (0, -.2, .4), .045)
    return P


def desk_lamp(L):
    """An architect's lamp: a square foot, two parallel arms to the elbow, a cone shade over the bulb."""
    P = D.Parts()
    P.add("Body", [s.rbox((.15, .15, .02), (0, 0, .01), r=.002, seg=1)])
    for dx in (-.02, .02):
        P.add("Body", beam((dx, .03, .02), (dx, .04, .26), w=.014) + beam((dx, .04, .26), (dx, -.12, .33), w=.014))
    P.add("Body", disc(.02, .06, (0, .04, .26), axis="X", seg=12))
    P.add("Accent", [s.rcyl(.075, .1, (0, -.16, .31), seg=4, r=0, radius2=.025, rot=(0, 0, math.pi / 4))])
    P.add("Eye", [s.ball(.03, (0, -.16, .27), seg=10)])
    return P


def floor_lamp(L):
    """A survey tripod of square beams carrying a pyramid shade over a glowing disc."""
    P = D.Parts()
    for k in range(3):
        a = math.pi / 2 + k * math.tau / 3
        P.add("Body", beam((math.cos(a) * .03, math.sin(a) * .03, 1.3), (math.cos(a) * .17, math.sin(a) * .17, .01), w=.025)
              + [s.rbox((.04, .04, .012), (math.cos(a) * .17, math.sin(a) * .17, .006), r=.002, seg=1)])
    P.add("Body", [s.rbox((.06, .06, .06), (0, 0, 1.33), r=.003, seg=1)])
    P.add("Accent", [s.rcyl(.27, .28, (0, 0, 1.505), seg=4, r=0, radius2=.13, rot=(0, 0, math.pi / 4))])
    P.add("Eye", disc(.14, .02, (0, 0, 1.36), seg=24))
    return P


def coat_rack(L):
    """A tripod of square beams with three hook arms and two drawn coats."""
    P = D.Parts()
    for k in range(3):
        a = k * math.tau / 3
        P.add("Accent", beam((0, 0, 1.78), (math.cos(a) * .22, math.sin(a) * .22, .01), w=.03))
        P.add("Accent", beam((0, 0, 1.68), (math.cos(a) * .2, math.sin(a) * .2, 1.74), w=.02))
    P.add("Body", L.slab((.34, .1, .6), (.12, 0, 1.36)) + L.slab((.3, .1, .55), (-.08, .1, 1.38), rot=(0, 0, 2.1)))
    return P


def safe(L):
    """A drawn box whose door carries a protractor dial and a crosshair status light."""
    P = D.Parts()
    P.add("Body", L.carcass((.8, .7, 1.0), (0, 0, 0), kick=.04))
    P.add("Body", L.door((.66, .82), (0, -.36, .52)))
    P.add("Accent", [poly_plate(half_disc((0, .52), .16, .03), .03, off=-.385)] + beam((-.16, -.385, .52), (.16, -.385, .52), w=.03))
    P.add("Accent", beam((0, -.395, .52), (.1, -.395, .64), w=.02))
    crosshair(P, (-.24, -.38, .82), .045)
    return P


def vending_machine(L):
    """An open frame cabinet: a drawn body, the window a glowing grid of compartments, a ruler keypad."""
    P = D.Parts()
    P.add("Body", L.carcass((.9, .8, 1.9), (0, 0, 0), kick=.05))
    P.add("Eye", [s.rbox((.6, .02, 1.1), (-.1, -.405, 1.15), r=.002, seg=1)])
    for k in range(1, 5):
        P.add("Accent", beam((-.4, -.42, .6 + k * .22), (.2, -.42, .6 + k * .22), w=.02))
    for x in (-.25, -.1, .05):
        P.add("Accent", beam((x, -.42, .6), (x, -.42, 1.7), w=.02))
    P.add("Accent", L.slab((.14, .03, .5), (.33, -.41, 1.2)) + L.slab((.6, .03, .15), (-.1, -.41, .25)))
    return P


def server_rack(L):
    """An open rack of square beams with no sides: the units are drawn plates with glowing ticks, and the rack is
    see-through."""
    P = D.Parts()
    for sx in (-1, 1):
        for sy in (-1, 1):
            P.add("Body", beam((sx * .32, sy * .47, 0), (sx * .32, sy * .47, 1.99), w=.055))
        for z in (.03, 1.97):
            P.add("Body", beam((sx * .32, -.47, z), (sx * .32, .47, z), w=.05))
    for sy in (-1, 1):
        for z in (.03, 1.97):
            P.add("Body", beam((-.32, sy * .47, z), (.32, sy * .47, z), w=.045))
    for i in range(11):
        z = .2 + i * .16
        P.add("Body", [s.rbox((.62, .9, .08), (0, 0, z), r=.002, seg=1)])
        P.add("Eye", [s.rbox((.5, .02, .03), (0, -.455, z), r=.002, seg=1)])
    return P


def reception_desk(L):
    """A drafting table for a counter: a drawn front panel and, behind it, a big slanted drawing board."""
    P = D.Parts()
    P.add("Body", L.slab((2.4, .08, 1.1), (0, -.36, .55)) + L.slab((.08, .76, 1.1), (-1.16, 0, .55)) + L.slab((.08, .76, 1.1), (1.16, 0, .55)))
    P.add("Accent", [s.rbox((2.36, .7, .03), (0, .02, .75), r=.002, seg=1)])
    board = drawn_box((2.3, .5, .03))
    for bm in board:
        s.place(bm, (0, -.2, 1.0), (math.radians(30), 0, 0))
    P.add("Accent", board)
    for x in (-1.1, 1.1):
        P.add("Accent", beam((x, -.0, .76), (x, -.1, 1.04), w=.03))
    acc, glow = L.screen((.5, .32), (.4, .2, .96), "-Y")
    P.add("Eye", glow + acc + L.slab((.06, .05, .2), (.4, .23, .83)))
    return P


def meeting_table(L):
    """A long drawn top on two A-frame trestles joined by a stretcher."""
    P = D.Parts()
    P.add("Body", [s.rbox((3.2, 1.3, .03), (0, 0, .765), r=.002, seg=1)])
    frame_box(P, (3.16, 1.26, .03), (0, 0, .75), w=.04)
    for x in (-1.1, 1.1):
        P.add("Accent", beam((x, -.5, .74), (x, 0, .02), w=.05) + beam((x, .5, .74), (x, 0, .02), w=.05)
              + beam((x, -.55, .74), (x, .55, .74), w=.05) + [s.rbox((.12, .5, .03), (x, 0, .015), r=.002, seg=1)])
    P.add("Accent", beam((-1.1, 0, .05), (1.1, 0, .05), w=.04))
    return P


def ceiling_lamp(L):
    """A drawn frame hanging from the ceiling with a glowing panel crossed by a grid of bars."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.24, .66, .04), (0, 0, -.02), r=.002, seg=1)])
    P.add("Eye", [s.rbox((1.12, .56, .03), (0, 0, -.055), r=.002, seg=1)])
    for x in (-.37, 0, .37):
        P.add("Accent", beam((x, -.3, -.068), (x, .3, -.068), w=.025))
    P.add("Accent", beam((-.6, -.3, -.068), (.6, -.3, -.068), w=.025) + beam((-.6, .3, -.068), (.6, .3, -.068), w=.025))
    return P


def hatch(L):
    """A circle drawn on the floor with a crosshair of grips; a glowing ring round it."""
    P = D.Parts()
    P.add("Body", ring(.6, .75, .06, (0, 0, .03), seg=48) + disc(.61, .03, (0, 0, .03), seg=48))
    P.add("Accent", beam((-.7, 0, .06), (.7, 0, .06), w=.06) + beam((0, -.7, .06), (0, .7, .06), w=.06))
    P.add("Eye", ring(.74, .9, .02, (0, 0, .01), seg=48))
    return P


def whiteboard(L):
    """A drawing board on an easel of square beams braced with diagonals."""
    P = D.Parts()
    P.add("Body", L.slab((1.8, .04, 1.0), (0, 0, 1.5)))
    for x in (-.85, .85):
        P.add("Accent", beam((x, 0, 1.98), (x, 0, .02), w=.045) + [s.rbox((.12, .08, .02), (x, 0, .01), r=.002, seg=1)])
        P.add("Accent", beam((x, 0, .3), (x * .3, 0, 1.0), w=.03))
    P.add("Accent", beam((-.85, 0, .3), (.85, 0, .3), w=.035) + beam((-.9, 0, 1.0), (.9, 0, 1.0), w=.05))
    return P


def arcade_machine(L):
    """An arcade cabinet drawn by its side profile: two profile frames of beams, panels between, a crosshair screen."""
    P = D.Parts()
    prof = [(.38, .02), (.38, 1.78), (-.12, 1.78), (-.2, 1.62), (-.3, 1.12), (-.52, 1.0), (-.48, .9), (-.3, .84), (-.22, .02)]
    for sx in (-1, 1):
        for a, b in zip(prof, prof[1:] + prof[:1]):
            P.add("Body", beam((sx * .33, a[0], a[1]), (sx * .33, b[0], b[1]), w=.045))
    P.add("Body", [s.rbox((.62, .58, 1.74), (0, .08, .9), r=0, seg=1)])
    P.add("Accent", [s.rbox((.62, .3, .03), (0, -.38, .96), r=.002, seg=1, rot=(math.radians(-12), 0, 0))])
    P.add("Eye", [s.rbox((.5, .02, .44), (0, -.25, 1.37), r=.002, seg=1, rot=(math.radians(-12), 0, 0))])
    crosshair(P, (0, -.27, 1.37), .12, role="Accent")
    return P


def bookshelf(L):
    """A bookcase drawn as two ladder frames with drawn shelves and no back; the books are rulers and set squares."""
    P = D.Parts()
    for x in (-.47, .47):
        for y in (-.15, .15):
            P.add("Body", beam((x, y, 0), (x, y, 2.0), w=.05))
        P.add("Body", beam((x, -.15, .1), (x, .15, 1.9), w=.025))
    for h in (.03, .5, 1.0, 1.5, 1.97):
        P.add("Body", L.slab((1.0, .35, .04), (0, 0, h)))
    for i, h in enumerate((.05, .52, 1.02, 1.52)):
        x = -.4
        for k in range(5 + i % 2):
            w = .05 + .02 * ((k + i) % 3)
            hh = .3 - .05 * ((k * 7 + i) % 3)
            P.add("Accent", [s.rbox((w, .24, hh), (x + w / 2, .0, h + hh / 2 - .005), r=.002, seg=1)])
            x += w + .03
        P.add("Accent", [poly_plate([(.15, h - .005), (.42, h - .005), (.15, h + .27)], .03)])
    return P


def boxes(L):
    """Crates drawn as cubes with their diagonals."""
    P = D.Parts()
    for size, c in (((.6, .6, .5), (0, 0, .25)), ((.5, .5, .41), (.05, -.05, .69)), ((.45, .45, .4), (.55, .1, .2))):
        P.add("Accent", drawn_box(size, at=c))
        w, d, h = size
        x, y, z = c
        P.add("Accent", beam((x - w / 2 + .03, y - d / 2 - .002, z - h / 2 + .03), (x + w / 2 - .03, y - d / 2 - .002, z + h / 2 - .03), w=.022, ext=False)
              + beam((x - w / 2 - .002, y - d / 2 + .03, z - h / 2 + .03), (x - w / 2 - .002, y + d / 2 - .03, z + h / 2 - .03), w=.022, ext=False))
    return P


def partition(L):
    """A screen drawn as a braced frame: a thin panel in a frame of bars with both diagonals."""
    P = D.Parts()
    P.add("Body", [s.rbox((1.52, .012, 1.16), (0, 0, .64), r=.002, seg=1)])
    P.add("Accent", beam((-.8, 0, 1.24), (.8, 0, 1.24), w=.04, d=.055) + beam((-.8, 0, .04), (.8, 0, .04), w=.04, d=.055)
          + beam((-.79, 0, .02), (-.79, 0, 1.25), w=.04, d=.05) + beam((.79, 0, .02), (.79, 0, 1.25), w=.04, d=.05)
          + beam((-.77, 0, .07), (.77, 0, 1.21), w=.025, d=.03, ext=False) + beam((.77, 0, .07), (-.77, 0, 1.21), w=.022, d=.028, ext=False))
    return P


def pallet(L):
    """A drawn pallet (bars and slats) carrying crates drawn with their diagonals."""
    P = D.Parts()
    for y in (-.45, 0, .45):
        P.add("Accent", [s.rbox((1.2, .1, .1), (0, y, .05), r=.002, seg=1)])
    for x in (-.5, -.25, 0, .25, .5):
        P.add("Accent", [s.rbox((.12, 1.0, .05), (x, 0, .12), r=.002, seg=1)])
    for x, y, z, sz in [(x, y, .34, (.55, .45, .4)) for x in (-.3, .3) for y in (-.24, .24)] + [(-.3, y, .735, (.53, .43, .4)) for y in (-.24, .24)]:
        P.add("Body", drawn_box(sz, at=(x, y, z)))
        P.add("Body", beam((x - sz[0] / 2 + .03, y - sz[1] / 2 - .002, z - sz[2] / 2 + .03), (x + sz[0] / 2 - .03, y - sz[1] / 2 - .002, z + sz[2] / 2 - .03),
                           w=.022, ext=False))
    return P


DESIGNS = {
    "robot_guard": guard, "robot_hunter": hunter, "robot_hound": hound, "robot_spider": spider,
    "robot_listener": listener, "robot_worker": worker, "robot_sweeper": sweeper, "robot_thumper": thumper,
    "robot_sorter": sorter, "robot_drone": drone, "robot_turret": turret, "robot_vacuum": vacuum,
    "sensor_lidar": lidar, "sensor_plate": plate, "security_camera": security_camera, "laser_emitter": laser_emitter,
    "sensor_pir": pir, "attach_mic": attach_mic,
    "chair": chair,
    "armchair": armchair,
    "sofa": sofa,
    "bean_bag": bean_bag,
    "plant": plant,
    "round_table": round_table,
    "trash_bin": trash_bin,
    "water_cooler": water_cooler,
    "coffee_machine": coffee_machine,
    "desk_lamp": desk_lamp,
    "floor_lamp": floor_lamp,
    "coat_rack": coat_rack,
    "safe": safe,
    "vending_machine": vending_machine,
    "server_rack": server_rack,
    "reception_desk": reception_desk,
    "meeting_table": meeting_table,
    "ceiling_lamp": ceiling_lamp,
    "hatch": hatch,
    "whiteboard": whiteboard,
    "arcade_machine": arcade_machine,
    "bookshelf": bookshelf,
    "boxes": boxes,
    "partition": partition,
    "pallet": pallet,
}
