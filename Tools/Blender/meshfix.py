"""Mesh fixes the model scripts apply before export, so the scenario-labs gate (vendor/scenario/bx_audit.deliverable)
passes without hand-placing every part to the millimetre.

fix_objects(objs, floor) — call it on all mesh objects of one model right before export:
  1. attach: a piece (a loose part of any object) that floats less than SNAP from the model moves until it overlaps it
     by 2 mm. Box-stacked models, grid snapping (lofi), tapering (lowpoly) and dents/tilts (derelict) leave parts
     hanging a few millimetres to centimetres off; physically they must touch. Bigger gaps are design errors and are
     only reported (fix them in the model function). A piece standing on the model's floor never moves (a foot
     lifted off the floor would make a robot walk on air): the rest of the model comes down to it instead. With floor=True (furniture, props, items) a piece resting on the
     floor counts as supported: a chair beside a desk is not a defect.
  0. drop_double_faces + outward normals: faces squashed onto each other (grid snapping) are deleted, thin plates turned
     inside out by dents get their normals back.
  2. separate_coplanar: two pieces with faces in one plane — overlapping and facing the same way (z-fighting: the
     engine cannot tell which is in front and the surface flickers) or pressed face to face (a double wall inside the
     mesh) — the smaller piece grows by GAP in every direction, so the faces part.
separate_coplanar(bm) does step 2 inside one bmesh (used when parts are merged).
"""
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

EPS = 1e-4     # faces closer than 0.1 mm count as one plane
GAP = 0.0015   # the smaller piece grows 1.5 mm: clear of depth-buffer precision at play distance
TOUCH = 0.004  # pieces closer than 4 mm touch (as review.check)
SNAP = 0.07    # pieces floating up to 7 cm get attached; more is a design error


# --------------------------------------------------------------------------- pieces

class Piece:
    def __init__(self, bm, verts):
        self.bm, self.verts = bm, verts
        self.refresh()

    def refresh(self):
        verts = self.verts
        faces = {f for v in verts for f in v.link_faces}
        idx = {v.index: k for k, v in enumerate(verts)}
        coords = [v.co.copy() for v in verts]
        polys = [[idx[v.index] for v in f.verts] for f in faces]
        self.coords = coords
        self.lo = Vector((min(c.x for c in coords), min(c.y for c in coords), min(c.z for c in coords)))
        self.hi = Vector((max(c.x for c in coords), max(c.y for c in coords), max(c.z for c in coords)))
        self.faces = [(f.calc_center_median(), f.normal.copy()) for f in faces if f.calc_area() > 1e-10]
        self.bvh = BVHTree.FromPolygons(coords, polys) if polys else None
        size = self.hi - self.lo
        self.volume = max(1e-12, size.x * size.y * size.z)

    def near(self, other, pad):
        return not any(self.lo[k] > other.hi[k] + pad or other.lo[k] > self.hi[k] + pad for k in range(3))

    def move(self, d):
        for v in self.verts:
            v.co += d
        self.refresh()

    def grow(self, gap):
        centre = (self.lo + self.hi) / 2
        size = self.hi - self.lo
        k = Vector(tuple(1.0 + 2 * gap / s if s > 1e-6 else 1.0 for s in size))
        for v in self.verts:
            d = v.co - centre
            v.co = centre + Vector((d.x * k.x, d.y * k.y, d.z * k.z))
        self.refresh()


def _pieces(bm):
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    seen = set()
    out = []
    for start in bm.verts:
        if start.index in seen:
            continue
        stack, comp = [start], []
        seen.add(start.index)
        while stack:
            v = stack.pop()
            comp.append(v)
            for e in v.link_edges:
                w = e.other_vert(v)
                if w.index not in seen:
                    seen.add(w.index)
                    stack.append(w)
        if any(v.link_faces for v in comp):
            out.append(Piece(bm, comp))
    return out


def _touching(a, b, tol=TOUCH):
    if a.bvh is None or b.bvh is None or not a.near(b, tol):
        return False
    if a.bvh.overlap(b.bvh):
        return True
    for src, dst in ((a, b), (b, a)):  # both ways: the corners of a big slab can be far from a small part on it
        step = max(1, len(src.coords) // 300)
        if any(dst.bvh.find_nearest(c, tol)[0] is not None for c in src.coords[::step]):
            return True
    return False


def _distance(group, main):
    """Smallest distance from a group of pieces to the main group, with the closest point pair."""
    best = (1e9, None, None)
    for p in group:
        step = max(1, len(p.coords) // 200)
        for c in p.coords[::step]:
            for m in main:
                if m.bvh is None:
                    continue
                loc, _, _, d = m.bvh.find_nearest(c)
                if loc is not None and d < best[0]:
                    best = (d, c, loc)
    for m in main:  # and from the model to the group (a big slab hovering over a small post)
        step = max(1, len(m.coords) // 200)
        for c in m.coords[::step]:
            for p in group:
                if p.bvh is None:
                    continue
                loc, _, _, d = p.bvh.find_nearest(c)
                if loc is not None and d < best[0]:
                    best = (d, loc, c)  # (point on the group, point on the model): the group moves towards the model
    return best


# --------------------------------------------------------------------------- 1. attach floating pieces

def attach(pieces, floor=False, snap=SNAP, report=print, name=""):
    """Moves pieces that float less than `snap` onto the model. Returns the number moved."""
    if len(pieces) < 2:
        return 0
    floor_z = min(p.lo.z for p in pieces)
    if floor and -0.02 < floor_z < 0.0:
        floor_z = 0.0  # a plinth or a leg sunk a few mm into the floor must not lift the floor for everything else
    moved = 0
    for _ in range(12):
        parent = list(range(len(pieces) + 1))  # the last node is the floor

        def find(i):
            while parent[i] != i:
                parent[i] = parent[parent[i]]
                i = parent[i]
            return i

        for i in range(len(pieces)):
            for j in range(i + 1, len(pieces)):
                if find(i) != find(j) and _touching(pieces[i], pieces[j]):
                    parent[find(i)] = find(j)
        ground = len(pieces)
        if floor:
            for i, p in enumerate(pieces):
                if p.lo.z <= floor_z + TOUCH:
                    parent[find(i)] = find(ground)
        groups = {}
        for i in range(len(pieces)):
            groups.setdefault(find(i), []).append(i)
        if floor and find(ground) in groups:
            main_key = find(ground)
        else:
            main_key = max(groups, key=lambda k: sum(len(pieces[i].coords) for i in groups[k]))
        main = [pieces[i] for i in groups[main_key]]
        others = [[pieces[i] for i in g] for k, g in groups.items() if k != main_key]
        if not others:
            return moved
        changed = False
        def stuck(p):  # stands on the floor, or must not move (a wheel or rotor spins about its own centre)
            return p.lo.z <= floor_z + TOUCH or getattr(p, "pinned", False)

        main_on_floor = any(stuck(p) for p in main)
        for group in others:  # every group within reach moves this round; the next round may carry the rest
            d, src, dst = _distance(group, main)
            if src is None or d > snap:
                continue
            direction = (dst - src)
            shift = direction.normalized() * (d + 0.002) if d > 1e-6 else Vector((0, 0, 0))
            if any(stuck(p) for p in group):
                # a foot or a wheel on the floor stays put (lifted, the model would walk on air): the body comes down
                if main_on_floor or floor:
                    continue
                for p in main:
                    p.move(-shift)
                moved += len(main)
                changed = True
                break  # the main group moved: measure again
            for p in group:
                p.move(shift)
            moved += len(group)
            changed = True
        if not changed:
            for group in others:
                d = _distance(group, main)[0]
                c = (group[0].lo + group[0].hi) / 2
                report(f"   MESHFIX {name}: piece at {[round(x, 3) for x in c]} floats {d:.3f} m — fix it in the model")
            return moved
    return moved


# --------------------------------------------------------------------------- 2. coplanar faces

def _clash(a, b):
    """A face of one piece lies in a face of the other (same or opposite facing)."""
    for src, dst in ((a, b), (b, a)):
        if dst.bvh is None:
            continue
        for c, n in src.faces:
            for loc, hn, _, _ in dst.bvh.find_nearest_range(c, EPS):
                if hn is None or abs(n.dot(hn)) < 0.999:
                    continue
                off = c - loc
                if (off - off.dot(hn) * hn).length < EPS:
                    return True
    return False


def _separate(pieces, gap, passes=3):
    grown = 0
    for _ in range(passes):
        fix = set()
        for i in range(len(pieces)):
            a = pieces[i]
            for j in range(i + 1, len(pieces)):
                b = pieces[j]
                if a.near(b, EPS) and _clash(a, b):
                    fix.add(i if a.volume <= b.volume else j)
        if not fix:
            return grown
        for i in fix:
            pieces[i].grow(gap)
        grown += len(fix)
    return grown


def separate_coplanar(bm, gap=GAP):
    """Step 2 inside one bmesh. Returns the number of pieces grown."""
    return _separate(_pieces(bm), gap)


# --------------------------------------------------------------------------- 3. collapsed walls

def drop_debris(bm, size=0.002):
    """Deletes pieces smaller than `size` in every direction (a thin rod squashed to a point by grid snapping)."""
    doomed = [v for p in _pieces(bm) if max(p.hi[k] - p.lo[k] for k in range(3)) < size for v in p.verts]
    if doomed:
        bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    return len(doomed)


def drop_double_faces(bm):
    """Deletes pairs of faces on the same corner positions (a part squashed to zero thickness by grid snapping, or two
    parts pressed into one wall): both are an invisible double wall the gate rejects. Returns the number deleted."""
    seen = {}
    doomed = set()
    for f in bm.faces:
        key = tuple(sorted(tuple(round(c, 5) for c in v.co) for v in f.verts))
        if key in seen:
            doomed.add(f)
            doomed.add(seen[key])
        else:
            seen[key] = f
    if doomed:
        bmesh.ops.delete(bm, geom=list(doomed), context="FACES_ONLY")
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
    return len(doomed)


def _signed_volume(faces):
    vol = 0.0
    for f in faces:
        vs = [v.co for v in f.verts]
        for i in range(1, len(vs) - 1):
            vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
    return vol


def turn_outward(bm):
    """A closed piece whose faces enclose a negative volume is inside out (the engine would draw its back faces):
    reverse it. Returns the number of pieces turned."""
    turned = 0
    for p in _pieces(bm):
        faces = list({f for v in p.verts for f in v.link_faces})
        if any(not e.is_manifold for f in faces for e in f.edges):
            continue  # open pieces have no inside
        if _signed_volume(faces) < -1e-9:
            bmesh.ops.reverse_faces(bm, faces=faces)
            turned += 1
    return turned


# --------------------------------------------------------------------------- whole model

def fix_objects(objs, floor=False, snap=SNAP, gap=GAP, report=print, name="", pinned=None):
    """Attach floating pieces and separate coplanar faces across all mesh objects of one model (world space).
    pinned(object name) -> True keeps that object's pieces where they are (the model moves to them instead)."""
    objs = [o for o in objs if o.type == "MESH" and o.data.polygons]
    bms = []
    dropped = flipped = 0
    for o in objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.transform(o.matrix_world)
        dropped += drop_double_faces(bm) + drop_debris(bm)
        flipped += turn_outward(bm)
        bms.append(bm)
    pieces = []
    for o, bm in zip(objs, bms):
        for p in _pieces(bm):
            p.pinned = bool(pinned and pinned(o.name))
            pieces.append(p)
    moved = attach(pieces, floor, snap, report, name) if snap else 0
    grown = _separate(pieces, gap)
    for o, bm in zip(objs, bms):
        bm.transform(o.matrix_world.inverted())
        bm.normal_update()
        bm.to_mesh(o.data)
        bm.free()
    if moved or grown or dropped or flipped:
        report(f"   MESHFIX {name}: {moved} pieces attached, {grown} pieces grown apart, {dropped} double faces dropped, "
               f"{flipped} pieces turned outward")
    return moved, grown
