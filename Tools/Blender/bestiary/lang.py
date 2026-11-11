"""The shape language of a visual style: how it builds a hull, a limb, a lens, a wheel, a joint.

Every robot in designs.py is written once against this vocabulary; a style (bestiary/styles/sNN_*.py) subclasses Lang
and changes the language — faceted crystal, soft toy, boiler plate, open drafting frame — and, where the language is not
enough, replaces a whole design (its own guard, its own hunter) or adds its signature pieces (`signature`).

All functions return lists of bmeshes in world coordinates (metres; Blender x right, -y front, z up), so a design can
collect them into parts: {"Body": [...], "Accent": [...], "Eye": [...], "AccentWheel0": [...], ...}.
Rules (see .claude/skills/blender-modeling): detail under 3 cm is invisible from the game camera — the language must
show in the big forms; pieces overlap 1-3 cm instead of touching face to face.
"""
import math

import bmesh
from mathutils import Matrix, Vector

import shapes as s
from furniture import Furniture


def _v(p):
    return Vector(p)


def align(bm, p0, p1):
    """Move a shape built along +Z from z = -L/2..L/2 so it spans p0 -> p1."""
    a, b = _v(p0), _v(p1)
    d = b - a
    if d.length < 1e-9:
        return s.place(bm, a)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=q.to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    return bm


class Lang(Furniture):
    """The neutral language: machined security hardware — rounded boxes, chamfered cylinders, smooth lofts.
    Furniture adds the furniture vocabulary (slab, leg, cushion, carcass, door, handle, screen, shade, pot, foliage)."""

    name = "Base"
    seg = 24            # segments of round things
    edge = 0.018        # edge radius of a box side 0.2 m (scales with size)
    soft = 0.0          # 0 machined .. 1 toy-soft: bigger radii, rounder sections
    flat = False        # flat shading (faceted styles)
    sharp = 35.0        # smooth-by-angle limit
    grid = 0.0          # snap every vertex to this grid (PS1 wobble); 0 = off
    hull_kind = "rounded"   # rounded | soft | facet | block | barrel | frame | plated | shard
    limb_kind = "tube"      # tube | square | piston | stilt
    eye_kind = "lens"       # lens | visor | bulb | porthole | slit
    wheel_kind = "tyre"     # tyre | solid | spoke | facet
    head_scale = 1.0        # robots' heads / sensor housings relative to the neutral size
    limb_scale = 1.0        # thickness of limbs, struts and arms

    # ------------------------------------------------------------------ primitives in this language

    def r(self, size):
        """Edge radius for a form whose smallest side is `size`."""
        if self.hull_kind in ("facet", "shard", "block"):
            return 0.0 if self.hull_kind != "block" else min(0.004, size * 0.05)
        return min(size * 0.45, self.edge * (size / 0.2) ** 0.5 * (1 + 2.5 * self.soft))

    def rseg(self):
        return 1 if self.flat else (3 if self.soft > 0.4 else 2)

    def rings(self, n=None):
        """Segments of a round section: n asks for a finer (or coarser) round than usual, but never far beyond the
        style's own `seg` (a low-poly style asking for 32 gets 16)."""
        return max(6, min(n, self.seg + 8)) if n else self.seg

    def box(self, size, loc=(0, 0, 0), rot=(0, 0, 0), taper=(1.0, 1.0), r=None):
        r = self.r(min(size)) if r is None else r
        return [s.rbox(size, loc, r=r, seg=self.rseg(), rot=rot, taper=taper)]

    def cyl(self, radius, depth, loc=(0, 0, 0), axis="Z", radius2=None, rot=(0, 0, 0), seg=None):
        seg = seg or max(6, self.rings() if radius > 0.05 else self.rings() // 2)
        r = 0.0 if self.flat else self.r(min(radius * 2, depth)) * 0.6
        return [s.rcyl(radius, depth, loc, axis=axis, seg=seg, r=r, bevel_seg=self.rseg(), radius2=radius2, rot=rot)]

    def ball(self, radius, loc=(0, 0, 0), scale=(1, 1, 1)):
        if self.hull_kind in ("facet", "shard"):
            bm = bmesh.new()
            bmesh.ops.create_icosphere(bm, subdivisions=2, radius=radius)  # Blender 5.2: 1 = a bare icosahedron
            return [s.place(bm, loc, scale=scale)]
        seg = self.rings() if radius > 0.12 else max(8, self.rings() // 2) if radius > 0.04 else 8
        return [s.ball(radius, loc, seg=min(seg, 24), scale=scale)]

    def lathe(self, profile, axis="Z", loc=(0, 0, 0), seg=None):
        return [s.lathe(profile, seg=seg or self.rings(), axis=axis, loc=loc)]

    def loft(self, rings, axis="Z", loc=(0, 0, 0), round_caps=0.0, seg=None):
        n = seg or (8 if self.hull_kind in ("facet", "shard") else max(16, self.rings()))
        if self.hull_kind in ("facet", "shard"):
            round_caps = 0.0
            rings = [(r[0], r[1], r[2], 2.0) + tuple(r[4:6]) if len(r) > 4 else (r[0], r[1], r[2], 2.0) for r in rings]
        return [s.loft(rings, seg=n, axis=axis, loc=loc, round_caps=round_caps)]

    # ------------------------------------------------------------------ the big forms

    def hull(self, size, loc=(0, 0, 0), taper=(1.0, 1.0), kind=None, rot=(0, 0, 0)):
        """The main volume of a robot or device (w, d, h centred at loc): the strongest carrier of the style."""
        kind = kind or self.hull_kind
        w, d, h = size
        x, y, z = loc
        if kind == "soft":
            k = min(w, d, h)
            out = self.loft([(-h / 2, w / 2, d / 2, 2.6), (0, w / 2 * (1 + taper[0]) / 2 * 1.04, d / 2 * (1 + taper[1]) / 2 * 1.04, 2.6),
                             (h / 2, w / 2 * taper[0], d / 2 * taper[1], 2.6)], loc=loc, round_caps=k * 0.3)
        elif kind == "barrel":
            out = self.loft([(-h / 2, w / 2 * 0.92, d / 2 * 0.92, 2.0), (-h / 2 + h * 0.08, w / 2, d / 2, 2.0),
                             (h / 2 - h * 0.08, w / 2 * taper[0], d / 2 * taper[1], 2.0),
                             (h / 2, w / 2 * taper[0] * 0.92, d / 2 * taper[1] * 0.92, 2.0)], loc=loc, round_caps=min(w, d) * 0.04)
            for t in (0.18, 0.82):  # hoops
                zz = -h / 2 + h * t
                k = 1 + (taper[0] - 1) * t
                out += self.loft([(zz - 0.02, w / 2 * k + 0.012, d / 2 * k + 0.012, 2.0), (zz + 0.02, w / 2 * k + 0.012, d / 2 * k + 0.012, 2.0)], loc=loc)
        elif kind in ("facet", "shard"):
            n = 6 if kind == "shard" else 8
            out = [s.loft([(-h / 2, w / 2 * 0.8, d / 2 * 0.8, 2.0), (-h / 2 + h * 0.25, w / 2, d / 2, 2.0),
                           (h / 2 - h * 0.15, w / 2 * taper[0], d / 2 * taper[1], 2.0),
                           (h / 2, w / 2 * taper[0] * 0.7, d / 2 * taper[1] * 0.7, 2.0)], seg=n, loc=loc)]
        elif kind == "frame":
            out = self.frame(size, loc, bar=max(0.025, min(w, d, h) * 0.08))
            out += self.box((w * 0.8, d * 0.08, h * 0.8), (x, y - d * 0.05, z))
        elif kind == "plated":
            out = self.box(size, loc, taper=taper)
            out += self.box((w * 0.72, 0.04, h * 0.6), (x, y - d / 2 - 0.005, z + h * 0.05), r=0.012)
            out += self.box((w * 1.04, d * 1.04, h * 0.12), (x, y, z - h * 0.32), r=0.01)
        elif kind == "block":
            out = [s.rbox(size, loc, r=0.0, seg=1, taper=taper)]
        else:  # rounded
            out = self.box(size, loc, taper=taper)
        if any(rot):
            for bm in out:
                bmesh.ops.translate(bm, vec=-_v(loc), verts=bm.verts)
                s.place(bm, loc, rot)
        return out

    def frame(self, size, loc, bar=0.03):
        """An open box of square bars (drafting frame, cage)."""
        w, d, h = size
        x, y, z = loc
        out = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                out += self.box((bar, bar, h + bar), (x + sx * w / 2, y + sy * d / 2, z), r=bar * 0.15)
        for sz in (-1, 1):
            for sy in (-1, 1):
                out += self.box((w + bar, bar, bar), (x, y + sy * d / 2, z + sz * h / 2), r=bar * 0.15)
            for sx in (-1, 1):
                out += self.box((bar, d + bar, bar), (x + sx * w / 2, y, z + sz * h / 2), r=bar * 0.15)
        return out

    def rod(self, p0, p1, radius, kind=None):
        """A limb, strut or arm from p0 to p1."""
        kind = kind or self.limb_kind
        radius *= self.limb_scale
        L = (_v(p1) - _v(p0)).length
        if kind == "square" or self.hull_kind in ("block", "frame"):
            bm = s.rbox((radius * 2, radius * 2, L + radius * 0.6), r=self.r(radius * 2) * 0.5, seg=self.rseg())
            return [align(bm, p0, p1)]
        if kind == "piston":  # a thick sleeve over a thin rod
            a, b = _v(p0), _v(p1)
            mid = a.lerp(b, 0.55)
            sleeve = s.rcyl(radius * 1.25, (mid - a).length + radius, seg=self.rings(12), r=radius * 0.3)
            rod = s.rcyl(radius * 0.6, (b - mid).length + radius, seg=self.rings(10), r=radius * 0.2)
            return [align(sleeve, a, mid + (mid - a).normalized() * radius * 0.5),
                    align(rod, mid - (b - mid).normalized() * radius * 0.5, b)]
        seg = 6 if self.hull_kind in ("facet", "shard") else max(8, self.rings() // 2)
        bm = s.rcyl(radius, L + radius * 0.6, seg=seg, r=0.0 if self.flat else radius * 0.35, bevel_seg=self.rseg())
        if kind == "stilt":
            s.taper(bm, "Z", 1.15, 0.7)
        return [align(bm, p0, p1)]

    def joint(self, radius, loc):
        """A visible joint (robots only: people get sleeves)."""
        return self.ball(radius, loc)

    def plate(self, size, loc, rot=(0, 0, 0)):
        """An armour plate / panel laid on a form."""
        return self.box(size, loc, rot=rot, r=min(size) * 0.3)

    # ------------------------------------------------------------------ sensors and moving parts

    def lens(self, radius, loc, axis="-Y"):
        """Glowing glass (Eye): a dome sitting in front of a bezel."""
        if self.eye_kind == "slit":
            return self.box((radius * 2.6, 0.03, radius * 0.6), loc, r=0.01)
        if self.hull_kind in ("facet", "shard"):
            return [s.loft([(0, radius, radius, 2.0), (radius * 0.35, radius * 0.6, radius * 0.6, 2.0), (radius * 0.5, 0, 0)],
                           seg=6, axis=axis, loc=loc)]
        prof = [(radius, 0), (radius, radius * 0.08), (radius * 0.82, radius * 0.3), (radius * 0.5, radius * 0.44), (0, radius * 0.5)]
        return [s.lathe(prof, seg=self.rings(), axis=axis, loc=loc)]

    def bezel(self, radius, depth, loc, axis="-Y"):
        """The housing a lens sits in (Accent): a short barrel with a lip."""
        prof = [(radius * 1.25, 0), (radius * 1.32, depth * 0.15), (radius * 1.32, depth * 0.7), (radius * 1.12, depth),
                (0, depth)]
        if self.hull_kind == "block":
            return self.box((radius * 2.6, depth, radius * 2.6), _v(loc) + _axis_vec(axis) * depth / 2)
        return [s.lathe(prof, seg=self.rings() if not self.flat else 8, axis=axis, loc=loc)]

    def eye(self, radius, loc, axis="-Y", depth=0.08):
        """(accent, glow): a complete camera eye — bezel and lens. Styles change its kind (visor, bulb, porthole)."""
        loc = _v(loc)
        a = _axis_vec(axis)
        if self.eye_kind == "visor":
            w = radius * 3.2
            acc = self.box((w + 0.04, depth, radius * 1.3), loc - a * depth * 0.3)
            glow = self.box((w, 0.03, radius * 0.7), loc + a * depth * 0.25, r=0.01)
            return acc, glow
        if self.eye_kind == "bulb":
            acc = self.cyl(radius * 1.15, depth * 0.6, loc - a * depth * 0.2, axis=_axis_name(axis))
            glow = self.ball(radius, loc + a * radius * 0.3)
            return acc, glow
        if self.eye_kind == "porthole":
            acc = self.cyl(radius * 1.35, depth, loc - a * depth * 0.2, axis=_axis_name(axis))
            acc += [s.place(s.loft([(0, radius * 1.45, radius * 1.45, 2.0), (0.025, radius * 1.45, radius * 1.45, 2.0)],
                                   seg=self.rings(), axis=axis), loc + a * depth * 0.25)]
            acc += [s.ball(0.016, loc + a * depth * 0.3 + _ring_pt(axis, radius * 1.45, k / 6 * math.tau), seg=8) for k in range(6)]
            glow = self.cyl(radius, 0.02, loc + a * depth * 0.32, axis=_axis_name(axis))
            return acc, glow
        acc = self.bezel(radius, depth, loc - a * depth * 0.6, axis)
        glow = self.lens(radius, loc + a * depth * 0.35, axis)
        return acc, glow

    def wheel(self, center, radius, width):
        """A wheel turning about X (AccentWheel*): it spins about the centre of its bounds, so keep it symmetric."""
        c = _v(center)
        kind = self.wheel_kind
        if kind == "facet" or self.flat:
            return [s.rcyl(radius, width, c, axis="X", seg=8, r=0.0)] + \
                   [s.rcyl(radius * 0.55, width * 1.15, c, axis="X", seg=6, r=0.0)]
        if kind == "solid":
            prof = [(0, -width / 2), (radius * 0.85, -width / 2), (radius, -width * 0.25), (radius, width * 0.25),
                    (radius * 0.85, width / 2), (0, width / 2)]
            return [s.lathe(prof, seg=self.rings(), axis="X", loc=c)]
        tyre = s.lathe([(radius * 0.62, -width / 2), (radius * 0.9, -width / 2), (radius, -width * 0.3), (radius, width * 0.3),
                        (radius * 0.9, width / 2), (radius * 0.62, width / 2)], seg=self.rings(32), axis="X", loc=c)
        # lathe closes the ends at the axis only when a radius is 0: fill the rim with a hub disc
        hub = s.rcyl(radius * 0.66, width * 0.86, c, axis="X", seg=self.rings(), r=0.006)
        cap = s.rcyl(radius * 0.3, width * 1.08, c, axis="X", seg=self.rings(16), r=0.006)
        out = [tyre, hub, cap]
        if kind == "spoke":
            for k in range(5):
                a = k / 5 * math.tau
                out.append(align(s.rbox((0.02, 0.02, radius * 0.6), r=0.004),
                                 c + Vector((width * 0.46, math.cos(a) * radius * 0.2, math.sin(a) * radius * 0.2)),
                                 c + Vector((width * 0.46, math.cos(a) * radius * 0.64, math.sin(a) * radius * 0.64))))
        return out

    def rotor(self, center, span):
        """A propeller turning about Z (AccentRotor*): two blades and a hub."""
        c = _v(center)
        out = [s.rcyl(span * 0.08, span * 0.18, c, seg=self.rings(16), r=0.004)]
        for k in (0, 1):
            blade = s.loft([(0, span * 0.05, 0.008), (span * 0.25, span * 0.075, 0.009), (span * 0.48, span * 0.04, 0.006)],
                           seg=12 if not self.flat else 6, axis="X")
            s.place(blade, rot=(math.radians(12), 0, k * math.pi))
            out.append(s.place(blade, c + Vector((0, 0, span * 0.04))))
        return out

    def track(self, size, loc):
        """A crawler track (w, d, h): a stadium loop with treads and road wheels."""
        w, d, h = size
        x, y, z = loc
        r = h / 2
        # built along +Z and turned onto X: the ring's first half-axis ends up vertical, the second along y
        belt = s.loft([(-w / 2, r, d / 2, 6.0), (w / 2, r, d / 2, 6.0)], seg=self.rings(32), axis="X", loc=loc)
        out = [belt]
        n = 6
        for k in range(n):
            yy = y - d / 2 + r + (d - 2 * r) * k / (n - 1)
            out += self.cyl(r * 0.62, w * 1.1, (x, yy, z), axis="X", seg=self.rings(12))
        return out

    # ------------------------------------------------------------------ style hooks

    def signature(self, model, parts):
        """Add the style's signature pieces to a finished design (parts: role -> list of bmeshes). Default: none."""
        return parts

    def post(self, bm):
        """Last pass on every bmesh of a model (grid snapping for PS1 styles, ...)."""
        if self.grid > 0:
            g = self.grid
            for v in bm.verts:
                v.co = Vector(tuple(round(c / g) * g for c in v.co))
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=g * 0.1)
        return bm


def _axis_vec(axis):
    return {"X": Vector((1, 0, 0)), "-X": Vector((-1, 0, 0)), "Y": Vector((0, 1, 0)), "-Y": Vector((0, -1, 0)),
            "Z": Vector((0, 0, 1)), "-Z": Vector((0, 0, -1))}[axis]


def _axis_name(axis):
    return axis.lstrip("-")


def _ring_pt(axis, radius, a):
    """A point on a circle of `radius` around the given axis."""
    n = axis.lstrip("-")
    c, sn = math.cos(a) * radius, math.sin(a) * radius
    return {"X": Vector((0, c, sn)), "Y": Vector((c, 0, sn)), "Z": Vector((c, sn, 0))}[n]
