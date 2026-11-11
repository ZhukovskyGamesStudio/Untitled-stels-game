"""The furniture vocabulary of a shape language (mixed into lang.Lang): slabs, legs, cushions, cabinets, doors,
handles, screens, shades, pots and foliage. A style changes the kinds (leg_kind = "turned", edge_kind = "soft", ...)
or overrides a method, and every prop in props.py follows.

Same conventions as lang.py: functions return lists of bmeshes in world coordinates, -y = the front of the prop.
"""
import math

import bmesh
from mathutils import Vector

import shapes as s


class Furniture:
    leg_kind = "square"     # square | round | tapered | turned | hairpin | block | sled
    edge_kind = "eased"     # eased (1 cm round) | soft (thick round) | sharp | chamfer
    cushion_puff = 0.5      # 0 flat foam .. 1 overstuffed
    handle_kind = "bar"     # bar | knob | recess | ring
    foliage_kind = "blob"   # blob | facet | spiky | ball

    # ------------------------------------------------------------------ boards and boxes

    def _edge(self, size):
        k = self.edge_kind
        m = min(size)
        if k == "sharp" or getattr(self, "hull_kind", "") in ("facet", "shard", "block"):
            return 0.0 if k == "sharp" or self.hull_kind != "block" else 0.003
        if k == "chamfer":
            return min(m * 0.3, 0.012)
        if k == "soft":
            return m * 0.45
        return min(m * 0.4, 0.01 + 0.01 * getattr(self, "soft", 0))

    def slab(self, size, loc, rot=(0, 0, 0)):
        """A board: table top, shelf, seat pan, panel."""
        r = self._edge(size)
        seg = 1 if self.edge_kind == "chamfer" or getattr(self, "flat", False) else 2
        return [s.rbox(size, loc, r=r, seg=seg, rot=rot)]

    def carcass(self, size, loc, kick=0.06):
        """A cabinet body standing on the floor (loc = centre of the footprint at the floor), with a toe kick."""
        w, d, h = size
        x, y, z = loc
        out = self.slab((w, d, h - kick), (x, y, z + kick + (h - kick) / 2))
        if kick > 0:  # from the floor up 1 cm into the body
            out += self.slab((w - 0.04, d - 0.06, kick + 0.01), (x, y + 0.02, z + kick / 2 + 0.005))
        return out

    def door(self, size, loc, face="-Y"):
        """A door or drawer front lying on a face: a panel 2 cm proud with a seam around it."""
        w, h = size
        x, y, z = loc
        return self.slab((w - 0.012, 0.022, h - 0.012), (x, y, z))

    def handle(self, loc, length=0.12, vertical=True):
        x, y, z = loc
        k = self.handle_kind
        if k == "knob":
            return [s.ball(0.022, (x, y - 0.02, z), seg=10)]
        if k == "ring":
            return [s.loft([(0, .025, .025, 2.0), (.008, .025, .025, 2.0)], seg=12, axis="-Y", loc=(x, y, z))]
        if k == "recess":
            return [s.rbox((0.012, 0.012, length) if vertical else (length, 0.012, 0.012), (x, y - 0.004, z), r=0.004)]
        size = (0.018, 0.018, length) if vertical else (length, 0.018, 0.018)
        return [s.rbox(size, (x, y - 0.03, z), r=0.007)] + \
               [s.rbox((0.014, 0.045, 0.014), (x + (0 if vertical else sx * length * 0.4), y - 0.012,
                                               z + (sx * length * 0.4 if vertical else 0)), r=0.004) for sx in (-1, 1)]

    # ------------------------------------------------------------------ legs

    def leg(self, top, length, thick=0.045, splay=(0.0, 0.0)):
        """A leg hanging from `top` down to the floor (splay = horizontal offset of the foot)."""
        x, y, z = top
        foot = (x + splay[0], y + splay[1], max(0.0, z - length))
        k = self.leg_kind
        if getattr(self, "hull_kind", "") in ("facet", "shard"):
            k = "block" if k not in ("hairpin",) else k
        if k == "round":
            return self.rod(top, foot, thick / 2, kind="tube")
        if k == "tapered":
            bm = s.rcyl(thick / 2, length, seg=12, r=thick * 0.1)
            s.taper(bm, "Z", 0.6, 1.0)
            from lang import align
            return [align(bm, top, foot)]
        if k == "turned":
            prof = [(thick * .35, 0), (thick * .55, .02), (thick * .4, .06), (thick * .3, length * .35), (thick * .6, length * .5),
                    (thick * .35, length * .62), (thick * .45, length * .9), (thick * .6, length * .97), (thick * .55, length), (0, length)]
            return [s.lathe(prof, seg=12, loc=foot)]
        if k == "hairpin":
            from lang import align
            out = []
            for sx in (-1, 1):
                out.append(align(s.rcyl(0.007, length, seg=8, r=0.002), (x + sx * thick * 0.6, y, z), (foot[0], foot[1], foot[2] + 0.01)))
            out.append(s.rbox((thick * 1.4, thick * 0.6, 0.012), (x, y, z - 0.006), r=0.003))
            return out
        if k == "sled":  # a bent tube runner — legs come in pairs, handled by the caller; alone it is a post
            return self.rod(top, foot, thick / 2, kind="tube")
        r = 0.0 if k == "block" else min(thick * 0.25, 0.008)
        from lang import align
        return [align(s.rbox((thick, thick, length), r=r, seg=2 if r else 1), top, foot)]

    def four_legs(self, top_z, half_w, half_d, length, thick=0.045, inset=0.06):
        out = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                out += self.leg((sx * (half_w - inset), sy * (half_d - inset), top_z), length, thick)
        return out

    # ------------------------------------------------------------------ soft and living things

    def cushion(self, size, loc, rot=(0, 0, 0)):
        """An upholstered block: a superellipse loft that bulges by cushion_puff."""
        w, d, h = size
        p = self.cushion_puff
        if getattr(self, "hull_kind", "") in ("facet", "shard", "block"):
            return [s.rbox(size, loc, r=0.0 if self.hull_kind != "block" else 0.004, seg=1, rot=rot)]
        n = 4.0 - 1.5 * p
        bulge = 1 + 0.06 * p
        bm = s.loft([(-h / 2, w / 2, d / 2, n), (0, w / 2 * bulge, d / 2 * bulge, n), (h / 2, w / 2, d / 2, n)],
                    seg=24, round_caps=min(h, w, d) * (0.25 + 0.2 * p))
        return [s.place(bm, loc, rot)]

    def screen(self, size, loc, face="-Y"):
        """(accent, glow): a flat screen — a bezel and the glowing picture on its front face."""
        w, h = size
        x, y, z = loc
        sign = -1 if face == "-Y" else 1
        acc = self.slab((w, 0.035, h), (x, y, z))
        glow = [s.rbox((w - 0.04, 0.01, h - 0.04), (x, y + sign * 0.017, z), r=0.004)]
        return acc, glow

    def shade(self, r_bottom, r_top, height, loc):
        """A lamp shade (open-looking cone with a thickness)."""
        x, y, z = loc
        if getattr(self, "hull_kind", "") in ("facet", "shard"):
            return [s.loft([(0, r_bottom, r_bottom, 2.0), (height, r_top, r_top, 2.0)], seg=6, loc=loc)]
        return [s.lathe([(r_bottom, 0), (r_bottom + 0.004, 0.01), (r_top + 0.004, height), (r_top - 0.01, height),
                         (r_bottom - 0.01, 0.01), (0, 0.01)], seg=24, loc=loc)]

    def pot(self, r, h, loc):
        x, y, z = loc
        if getattr(self, "hull_kind", "") in ("facet", "shard"):
            return [s.loft([(0, r * .8, r * .8, 2.0), (h, r, r, 2.0)], seg=6, loc=loc)]
        return [s.lathe([(0, 0), (r * 0.78, 0), (r * 0.82, 0.02), (r, h * 0.9), (r * 1.06, h * 0.92), (r * 1.06, h),
                         (0, h)], seg=20, loc=loc)]

    def foliage(self, centre, r, n=5, seed=1):
        """A bush of leaves: overlapping blobs (or facets / spikes by foliage_kind)."""
        cx, cy, cz = centre
        out = []
        k = self.foliage_kind
        for i in range(n):
            a = (i * 2.399 + seed) % math.tau
            rr = r * (0.55 + 0.25 * ((i * 7 + seed) % 3) / 2)
            d = r * 0.45 if i else 0
            c = (cx + math.cos(a) * d, cy + math.sin(a) * d, cz + (i % 3 - 1) * r * 0.25)
            if k == "facet" or getattr(self, "hull_kind", "") in ("facet", "shard"):
                bm = bmesh.new()
                bmesh.ops.create_icosphere(bm, subdivisions=1, radius=rr)
                out.append(s.place(bm, c, rot=(a, a * 0.5, 0)))
            elif k == "spiky":
                for j in range(5):
                    b = a + j * math.tau / 5
                    tip = (c[0] + math.cos(b) * rr, c[1] + math.sin(b) * rr, c[2] + rr * 0.9)
                    from lang import align
                    blade = s.loft([(-rr * .5, .03, .008, 2.0), (0, .04, .01, 2.0), (rr * .5, .005, .004, 2.0)], seg=6)
                    out.append(align(blade, c, tip))
            else:
                out.append(s.ball(rr, c, seg=14, scale=(1, 1, 0.85)))
        return out
