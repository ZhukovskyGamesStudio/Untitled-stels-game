"""Prototype of the new floor layouts (1 m cells): a mass of wings, a network of lanes with jogs, crossings and loops,
blocks between the lanes treated as single rooms, clusters, rows, back-to-back rows or open plan. Pictures + numbers."""
import random, sys, os, collections, colorsys
from PIL import Image, ImageDraw

OUT = -2; FREE = -1
D4 = [(1, 0), (-1, 0), (0, 1), (0, -1)]

# ------------------------------------------------------------------ masses: lists of rects (x, z, w, d)
def m_figure8(r, s):
    w, d, k = int(64*s), int(46*s), r.randint(15, 18)
    mid = int(w*r.uniform(.42, .58)) - k//2
    return [(0, 0, w, k), (0, d-k, w, k), (0, 0, k, d), (w-k, 0, k, d), (mid, 0, k-2, d)]
def m_pinwheel(r, s):
    c, a, k = int(22*s), int(24*s), r.randint(18, 22)
    return [(a, a, c, c), (a-r.randint(2, 6), 0, k, a+2), (a+c-2, a-r.randint(2, 6), a, k), (a+c-k+r.randint(2, 6), a+c-2, k, a), (0, a+c-k+r.randint(2, 6), a+2, k)]
def m_stagger(r, s):
    res, x, z = [], 0, 0
    for i in range(3):
        w, d = int(r.randint(26, 34)*s), int(r.randint(22, 30)*s)
        res.append((x, z, w, d)); x += int(w*r.uniform(.62, .85)); z += int(d*r.uniform(.45, .75)) * (1 if i % 2 == 0 else r.choice([1, -1]))
    mz = min(q[1] for q in res)
    return [(a, b-mz, c, d) for a, b, c, d in res]
def m_comb(r, s):
    L, k = int(64*s), r.randint(19, 24)
    res = [(0, 0, L, k)]; n = r.randint(2, 3); x = r.randint(0, 5)
    for i in range(n):
        w = r.randint(16, 20); res.append((x, k-2, w, int(r.randint(16, 26)*s))); x += w + r.randint(9, 14)
        if x + 15 > L: break
    if r.random() < .6: res.append((r.randint(18, 34), -int(13*s), r.randint(16, 24), int(13*s)+2))
    mz = min(q[1] for q in res)
    return [(a, b-mz, c, d) for a, b, c, d in res]
def m_deep(r, s):
    w, d = int(r.randint(46, 54)*s), int(r.randint(38, 44)*s)
    cw, cd = r.randint(8, 12), r.randint(8, 11); cx, cz = int(w*r.uniform(.3, .6)), int(d*r.uniform(.3, .5))
    return [(0, 0, w, cz), (0, cz+cd, w, d-cz-cd), (0, cz, cx, cd), (cx+cw, cz, w-cx-cw, cd)]
def m_cross(r, s):
    a, b, k = int(60*s), int(48*s), r.randint(20, 25)
    z, x = int(b*r.uniform(.3, .55)), int(a*r.uniform(.28, .5))
    return [(0, z, a, k), (x, 0, k, b), (x-r.randint(4, 8), z-r.randint(2, 6), k+12, k+8)]
def m_campus(r, s):
    res = [(16, 14, int(26*s), int(22*s))]
    cx, cz, w, d = res[0]
    res.append((cx-r.randint(14, 18), cz+r.randint(-6, 4), 19, r.randint(14, 18)))
    res.append((cx+w-2, cz+r.randint(4, 9), r.randint(18, 22), r.randint(15, 20)))
    res.append((cx+r.randint(2, 8), cz+d-2, r.randint(16, 22), r.randint(14, 18)))
    res.append((cx+r.randint(-4, 5), cz-r.randint(11, 14), r.randint(17, 22), 16))
    mx, mz = min(q[0] for q in res), min(q[1] for q in res)
    return [(a-mx, b-mz, c, d) for a, b, c, d in res]
def m_z(r, s):
    k = r.randint(20, 24); L = int(34*s); mid = int(30*s)
    return [(0, 0, L, k), (L-k, 0, k, mid+k), (L-k, mid, L, k), (L-k-r.randint(6, 10), k+r.randint(2, 5), k+14, r.randint(14, 18))]

MASS = dict(figure8=m_figure8, pinwheel=m_pinwheel, stagger=m_stagger, comb=m_comb, deep=m_deep, cross=m_cross, campus=m_campus, z=m_z)

KINDS = {  # block: how big a block between lanes may stay; weights of what a block becomes
 "hospital":   dict(mass=["figure8", "comb", "deep", "pinwheel", "campus", "cross"], block=(10, 17), w=dict(cluster=4, rows=3, back=3, single=1, open=.6, pinwheel=2), main=3, room=(4, 6)),
 "station":    dict(mass=["pinwheel", "campus", "z", "stagger", "cross"], block=(11, 19), w=dict(cluster=3, rows=1, back=1.5, single=3, open=1, pinwheel=2), main=3, room=(4, 8)),
 "office":     dict(mass=["deep", "figure8", "stagger", "cross", "comb", "z", "campus"], block=(11, 20), w=dict(cluster=3, rows=2, back=2, single=1, open=4, pinwheel=2), main=3, room=(3, 6)),
 "garage":     dict(mass=["stagger", "z", "campus", "deep"], block=(15, 26), w=dict(cluster=2, rows=1, back=1, single=6, open=1, pinwheel=1), main=4, room=(4, 7)),
}

class Plan:
    def __init__(s, kind, seed, mass=None):
        s.r = r = random.Random(seed); s.k = KINDS[kind]; s.kind = kind
        s.massname = mass or r.choice(s.k["mass"])
        s.wings = MASS[s.massname](r, r.uniform(.95, 1.15))
        s.W = max(x+w for x, z, w, d in s.wings); s.D = max(z+d for x, z, w, d in s.wings)
        s.g = [[OUT]*s.D for _ in range(s.W)]
        for x, z, w, d in s.wings:
            for i in range(x, x+w):
                for j in range(z, z+d): s.g[i][j] = FREE
        s.lane = set(); s.rooms = {}; s.style = {}; s.next = 0
        s.network(); s.join(); s.loops(); s.trim(); s.fill(); s.access()

    def inside(s, x, z): return 0 <= x < s.W and 0 <= z < s.D and s.g[x][z] != OUT
    def put(s, x, z):
        if s.inside(x, z): s.lane.add((x, z))
    def carve(s, x, z, w, d):
        for i in range(x, x+w):
            for j in range(z, z+d): s.put(i, j)

    # -------------------------------------------------------------- lanes
    def network(s):
        rects = s.decompose({(x, z) for x in range(s.W) for z in range(s.D) if s.g[x][z] != OUT}, big=True)
        for q in rects: s.split(q, 0)
    def split(s, q, depth):
        x, z, w, d = q; r = s.r
        lim = r.randint(*s.k["block"])
        long_x = w >= d
        L, S = (w, d) if long_x else (d, w)
        if L <= 8 or (S <= lim and L <= lim*1.5): return
        if S < 12:                                   # a thin wing: rooms on one side of a lane along its edge or a lane that swaps sides
            lw = 2
            if L < 14: return
            cut = r.randint(int(L*.35), int(L*.65)); a, b = r.choice([(0, S-lw), (S-lw, 0)])
            if long_x:
                s.carve(x, z+a, cut+lw, lw); s.carve(x+cut, z+b, w-cut, lw); s.carve(x+cut, z, lw, d)
            else:
                s.carve(x+a, z, lw, cut+lw); s.carve(x+b, z+cut, lw, d-cut); s.carve(x, z+cut, w, lw)
            return
        lw = s.k["main"] if depth == 0 else 2
        # a long piece: first a party line across it (no corridor): the lanes on its two sides miss each other — a jog
        if L > 22 and r.random() < .72:
            at = r.randint(int(L*.36), int(L*.64))
            a, b = ((x, z, at, d), (x+at, z, w-at, d)) if long_x else ((x, z, w, at), (x, z+at, w, d-at))
            o1 = s.lengthwise(a, long_x, lw); o2 = s.lengthwise(b, long_x, lw, avoid=o1)
            lo, hi = min(o1, o2), max(o1, o2)+lw
            if long_x: s.carve(x+at-lw//2-1, z+lo, 2, hi-lo)
            else: s.carve(x+lo, z+at-lw//2-1, hi-lo, 2)
            for h, o in ((a, o1), (b, o2)): s.sides(h, long_x, o, lw, depth)
            return
        along = long_x if r.random() < .6 or S <= lim else not long_x
        if along: o = s.lengthwise(q, long_x, lw); s.sides(q, long_x, o, lw, depth)
        else:                                        # a lane across: the piece falls into two, each with lanes of its own
            o = r.randint(int(L*.3), int(L*.7)-lw)
            if long_x:
                s.carve(x+o, z, lw, d); s.split((x, z, o, d), depth+1); s.split((x+o+lw, z, w-o-lw, d), depth+1)
            else:
                s.carve(x, z+o, w, lw); s.split((x, z, w, o), depth+1); s.split((x, z+o+lw, w, d-o-lw), depth+1)
    def lengthwise(s, q, long_x, lw, avoid=None):
        x, z, w, d = q; S = d if long_x else w
        for _ in range(12):
            o = s.r.choice([s.r.randint(5, max(5, S-5-lw)), s.r.randint(6, max(6, S-6-lw))])
            if avoid is None or abs(o-avoid) >= 4: break
        if avoid is not None and abs(o-avoid) < 4: o = 5 if avoid > S//2-lw//2 else max(5, S-5-lw)
        if long_x: s.carve(x, z+o, w, lw)
        else: s.carve(x+o, z, lw, d)
        return o
    def sides(s, q, long_x, o, lw, depth):
        x, z, w, d = q
        if long_x: s.split((x, z, w, o), depth+1); s.split((x, z+o+lw, w, d-o-lw), depth+1)
        else: s.split((x, z, o, d), depth+1); s.split((x+o+lw, z, w-o-lw, d), depth+1)

    def comps(s, cells):
        left = set(cells); out = []
        while left:
            q = [left.pop()]; c = set(q)
            while q:
                a = q.pop()
                for dx, dz in D4:
                    b = (a[0]+dx, a[1]+dz)
                    if b in left: left.discard(b); c.add(b); q.append(b)
            out.append(c)
        return out
    def route(s, src, dst, turn=3, lane=.4, free=1.0):
        import heapq
        h = [(0, i, a, None) for i, a in enumerate(sorted(src))]; heapq.heapify(h); best = {}; n = len(h); par = {}
        while h:
            c, _, a, d = heapq.heappop(h)
            if (a, d) in best: continue
            best[(a, d)] = c
            if a in dst:
                p = []; k = (a, d)
                while k: p.append(k[0]); k = par.get(k)
                return p
            for e in D4:
                b = (a[0]+e[0], a[1]+e[1])
                if not s.inside(*b) or not s.inside(b[0]+1, b[1]+1): continue
                n += 1; nc = c + (lane if b in s.lane else free) + (turn if d and e != d else 0)
                if (b, e) not in best:
                    if (b, e) not in par: par[(b, e)] = (a, d)
                    heapq.heappush(h, (nc, n, b, e))
        return None
    def join(s):
        for _ in range(40):
            cs = sorted(s.comps(s.lane), key=len, reverse=True)
            if len(cs) < 2: return
            small = cs[-1]
            if len(small) < 10: s.lane -= small; continue
            p = s.route(small, set().union(*cs[:-1]))
            if not p: s.lane -= small; continue
            for a in p: s.carve(a[0], a[1], 2, 2)
    def ends(s):
        """lane ends: cells of a lane whose 5x5 neighbourhood holds few lane cells"""
        e = []
        for (x, z) in sorted(s.lane):
            n = sum((x+i, z+j) in s.lane for i in range(-3, 4) for j in range(-3, 4))
            if n <= 8 * 1.6: e.append((x, z))
        return e
    def loops(s):
        done = 0
        for e in s.ends():
            if done >= 5: break
            near = {(e[0]+i, e[1]+j) for i in range(-5, 6) for j in range(-5, 6)}
            dist = s.walk({e})
            far = {c for c in s.lane if c not in near and dist.get(c, 0) > 34 and abs(c[0]-e[0])+abs(c[1]-e[1]) < 18}
            if not far: continue
            p = s.route({e}, far, turn=2, lane=1.2)
            if p and len(p) < 24:
                for a in p: s.carve(a[0], a[1], 2, 2)
                done += 1
    def trim(s):
        # lanes merged side by side into blobs are fine (halls); drop stubs shorter than 3
        pass
    def walk(s, src, cells=None):
        cells = cells or s.lane; d = {a: 0 for a in src}; q = collections.deque(src)
        while q:
            a = q.popleft()
            for dx, dz in D4:
                b = (a[0]+dx, a[1]+dz)
                if b in cells and b not in d: d[b] = d[a]+1; q.append(b)
        return d

    # -------------------------------------------------------------- blocks
    def decompose(s, cells, big=False):
        """cells as rectangles, the biggest first"""
        cells = set(cells); out = []
        while cells:
            best = None
            hist = {}
            xs = sorted({c[0] for c in cells}); zmin = min(c[1] for c in cells); zmax = max(c[1] for c in cells)
            H = [0]*(zmax-zmin+2)
            for x in range(min(xs), max(xs)+1):
                for z in range(zmin, zmax+1): H[z-zmin] = H[z-zmin]+1 if (x, z) in cells else 0
                st = []
                for i in range(len(H)):
                    h = H[i] if i < len(H)-1 else 0; start = i
                    while st and st[-1][1] >= h:
                        j, hh = st.pop(); ar = hh*(i-j); start = j
                        score = ar * (1 if min(hh, i-j) >= 4 else .3)
                        if ar and (best is None or score > best[0]): best = (score, x-hh+1, j+zmin, hh, i-j)
                    st.append((start, h))
            _, x, z, w, d = best
            out.append((x, z, w, d))
            for i in range(x, x+w):
                for j in range(z, z+d): cells.discard((i, j))
        return out
    def room(s, x, z, w, d, style, grp=None):
        if w <= 0 or d <= 0: return None
        i = s.next; s.next += 1
        s.rooms[i] = dict(cells=set((a, b) for a in range(x, x+w) for b in range(z, z+d)), style=style, grp=grp)
        return i
    def lanesides(s, q):
        x, z, w, d = q
        f = lambda cells: sum(c in s.lane for c in cells)
        return dict(W=f([(x-1, z+i) for i in range(d)]), E=f([(x+w, z+i) for i in range(d)]), S=f([(x+i, z-1) for i in range(w)]), N=f([(x+i, z+d) for i in range(w)]))
    def slices(s, q, along_x, style, grp=None):
        x, z, w, d = q; L = w if along_x else d; out = []; at = 0
        lo, hi = s.k["room"]
        while at < L:
            t = s.r.randint(lo, hi)
            if s.r.random() < .18: t = int(t*1.8)
            if L-at-t < 3: t = L-at
            out.append(s.room(x+at, z, t, d, style, grp) if along_x else s.room(x, z+at, w, t, style, grp)); at += t
        return out
    def fill(s):
        free = {(x, z) for x in range(s.W) for z in range(s.D) if s.g[x][z] == FREE and (x, z) not in s.lane}
        grp = 0
        for comp in s.comps(free):
            for q in s.decompose(comp):
                x, z, w, d = q; area = w*d; sd = s.lanesides(q); r = s.r
                on = [k for k in sd if sd[k] >= 2]
                m, M_ = min(w, d), max(w, d)
                if m < 4 or area < 30: s.room(x, z, w, d, "nook"); continue
                opt = dict(s.k["w"])
                if area > 170 and s.kind != "garage" and s.kind != "station": opt["single"] = .3
                if area < 60: opt = dict(single=1)
                elif m < 9: opt = dict(rows=3, single=opt["single"]*.7)
                if m < 9 or area < 110 or not on: opt.pop("cluster", 0)
                if m < 11 or not on: opt.pop("pinwheel", 0)
                if m < 11 or area < 130: opt.pop("open", 0)
                if not (("W" in on and "E" in on and w >= 9) or ("S" in on and "N" in on and d >= 9)): opt.pop("back", 0)
                if m > 10 and "rows" in opt and len(opt) > 1: opt.pop("rows")
                if not opt: opt = dict(single=1)
                t = r.choices(list(opt), list(opt.values()))[0]
                grp += 1
                getattr(s, "b_"+t)(q, on, sd, grp)
    def b_single(s, q, on, sd, grp): s.room(*q, "single")
    def b_rows(s, q, on, sd, grp):
        x, z, w, d = q; front = max(sd, key=lambda k: sd[k])
        if front in "SN": s.slices(q, True, "row")
        else: s.slices(q, False, "row")
    def b_back(s, q, on, sd, grp):
        x, z, w, d = q
        if "W" in on and "E" in on and w >= 9 and not ("S" in on and "N" in on and d >= 9 and s.r.random() < .5):
            c = s.r.randint(4, w-4); s.slices((x, z, c, d), False, "row"); s.slices((x+c, z, w-c, d), False, "row")
        else:
            c = s.r.randint(4, d-4); s.slices((x, z, w, c), True, "row"); s.slices((x, z+c, w, d-c), True, "row")
    def b_open(s, q, on, sd, grp):
        x, z, w, d = q; pods = s.r.randint(1, 3); cells = {(a, b) for a in range(x, x+w) for b in range(z, z+d)}
        for c in s.r.sample([(0, 0), (1, 0), (0, 1), (1, 1)], pods):
            pw, pd = s.r.randint(4, max(4, w//3)), s.r.randint(4, max(4, d//3))
            px, pz = x+(w-pw if c[0] else 0), z+(d-pd if c[1] else 0)
            i = s.room(px, pz, pw, pd, "pod", grp); cells -= s.rooms[i]["cells"]
        i = s.next; s.next += 1; s.rooms[i] = dict(cells=cells, style="open", grp=grp)
    def orient(s, q, side):
        """maps a block so that its lane side is at local z = 0: local (u, v, w, d) -> world rect"""
        x, z, w, d = q
        if side == "S": return w, d, lambda u, v, a, b: (x+u, z+v, a, b)
        if side == "N": return w, d, lambda u, v, a, b: (x+u, z+d-v-b, a, b)
        if side == "W": return d, w, lambda u, v, a, b: (x+v, z+u, b, a)
        return d, w, lambda u, v, a, b: (x+w-v-b, z+u, b, a)
    def lanespan(s, q, side):
        """local u of the cells on that side that touch the lane"""
        x, z, w, d = q
        if side in "SN": return [i for i in range(w) if (x+i, z-1 if side == "S" else z+d) in s.lane]
        return [i for i in range(d) if (x-1 if side == "W" else x+w, z+i) in s.lane]
    def b_cluster(s, q, on, sd, grp):
        side = max(on, key=lambda k: sd[k]); W, D, f = s.orient(q, side); r = s.r
        span = s.lanespan(q, side)
        ok = [u for u in span if u+1 in span and 3 <= u <= W-5] or [u for u in span if u+1 in span]
        if not ok or W < 9 or D < 8: return s.b_single(q, on, sd, grp)
        u = min(ok, key=lambda a: abs(a-W/2)+r.random()*4)
        u = max(3, min(W-5, u))
        hall = r.randint(max(4, D-6), D-4)            # the private hall goes in this deep; a room ends it
        s.slicesL(f, 0, u, hall, grp); s.slicesL(f, u+2, W-u-2, hall, grp)
        h = s.room(*f(u, 0, 2, hall), "hall", grp)
        # the hall widens into an anteroom at its end now and then
        if D-hall >= 4:
            n = 1 if W < 12 else 2
            if n == 1: s.room(*f(0, hall, W, D-hall), "clu", grp)
            else:
                c = max(4, min(W-4, u+1+r.randint(-2, 2)));
                c = u+1 if not (u-1 <= c <= u+2) else c
                s.room(*f(0, hall, c, D-hall), "clu", grp); s.room(*f(c, hall, W-c, D-hall), "clu", grp)
        return h
    def slicesL(s, f, u, w, D, grp):
        if w <= 0: return
        if w < 3: s.room(*f(u, 0, w, D), "nook", grp); return
        at = 0; lo, hi = s.k["room"]
        while at < D:
            t = s.r.randint(max(3, lo-1), hi)
            if D-at-t < 3: t = D-at
            s.room(*f(u, at, w, t), "clu", grp); at += t
    def b_pinwheel(s, q, on, sd, grp):
        side = max(on, key=lambda k: sd[k]); W, D, f = s.orient(q, side); r = s.r
        if W < 11 or D < 11: return s.b_cluster(q, on, sd, grp)
        hw, hd = r.randint(3, max(3, W-9)), r.randint(3, max(3, D-9)); hw = min(hw, 6); hd = min(hd, 6)
        x1 = r.randint(4, W-hw-4); z1 = r.randint(4, D-hd-4); x2, z2 = x1+hw, z1+hd
        # windmill of four rooms round the hall; the one on the lane's side gives the way in (an anteroom)
        s.room(*f(x1, z1, hw, hd), "hall", grp)
        s.room(*f(0, 0, x2, z1), "ante", grp); s.room(*f(x2, 0, W-x2, z2), "clu", grp)
        s.room(*f(x1, z2, W-x1, D-z2), "clu", grp); s.room(*f(0, z1, x1, D-z1), "clu", grp)
        # the big arms once more in two
        for i in list(s.rooms):
            R = s.rooms[i]
            if R["grp"] != grp or R["style"] != "clu": continue
            xs = [c[0] for c in R["cells"]]; zs = [c[1] for c in R["cells"]]; x, z, w, d = min(xs), min(zs), max(xs)-min(xs)+1, max(zs)-min(zs)+1
            if w*d < 52 or max(w, d) < 9: continue
            del s.rooms[i]
            if w >= d: c = w//2+r.randint(-1, 1); s.room(x, z, c, d, "clu", grp); s.room(x+c, z, w-c, d, "clu", grp)
            else: c = d//2+r.randint(-1, 1); s.room(x, z, w, c, "clu", grp); s.room(x, z+c, w, d-c, "clu", grp)

    # -------------------------------------------------------------- doors
    def access(s):
        s.idof = {}
        for i, R in s.rooms.items():
            for c in R["cells"]: s.idof[c] = i
        for c in s.lane: s.idof[c] = -10
        bord = collections.defaultdict(list)
        for c, i in s.idof.items():
            for dx, dz in ((1, 0), (0, 1)):
                b = (c[0]+dx, c[1]+dz); j = s.idof.get(b)
                if j is not None and j != i: bord[(min(i, j), max(i, j))].append((c, b))
        s.bord = bord; s.doors = []; deg = collections.Counter(); linked = {-10}
        def door(i, j):
            e = bord[(min(i, j), max(i, j))]
            if len(e) < 2: return False
            s.doors.append((i, j, s.r.choice(e[1:-1] or e))); deg[i] += 1; deg[j] += 1; return True
        nb = collections.defaultdict(set)
        for (i, j), e in bord.items():
            if len(e) >= 2: nb[i].add(j); nb[j].add(i)
        # clusters: their halls / anterooms / open rooms onto the lane, their rooms onto those; the rest onto the lane
        first = [i for i, R in s.rooms.items() if R["style"] in ("hall", "ante", "open")]
        for i in first:
            R = s.rooms[i]
            if -10 in nb[i] and not (R["style"] == "hall" and any(s.rooms[j]["style"] == "ante" for j in nb[i] if j >= 0 and s.rooms[j]["grp"] == R["grp"])): door(i, -10); linked.add(i)
        for rnd in range(6):
            for i, R in s.rooms.items():
                if i in linked: continue
                mates = [j for j in nb[i] if j >= 0 and j in linked and s.rooms[j]["grp"] is not None and s.rooms[j]["grp"] == R["grp"] and s.rooms[j]["style"] in ("hall", "ante", "open")]
                if mates: door(i, mates[0]); linked.add(i)
                elif -10 in nb[i] and (R["style"] not in ("clu", "hall") or rnd > 2): door(i, -10); linked.add(i)
        for rnd in range(8):
            for i in s.rooms:
                if i in linked: continue
                c = [j for j in nb[i] if j in linked]
                if c: j = max(c, key=lambda j: len(bord[(min(i, j), max(i, j))]) + (50 if j == -10 else 0)); door(i, j); linked.add(i)
        s.lost = [i for i in s.rooms if i not in linked]
        # second ways: the back of a cluster onto another lane, a room through to its neighbour
        s.extra = 0
        for i, R in list(s.rooms.items()):
            if R["style"] == "clu" and -10 in nb[i] and s.r.random() < .16 and door(i, -10): s.extra += 1
            elif s.r.random() < .05:
                c = [j for j in nb[i] if j >= 0 and s.rooms[j]["grp"] != R["grp"]]
                if c and door(i, s.r.choice(c)): s.extra += 1
        s.deg = deg

    # -------------------------------------------------------------- numbers and picture
    def stats(s):
        run = 0
        for x in range(s.W):
            n = 0
            for z in range(s.D+1):
                n = n+1 if (x, z) in s.lane else 0; run = max(run, n)
        for z in range(s.D):
            n = 0
            for x in range(s.W+1):
                n = n+1 if (x, z) in s.lane else 0; run = max(run, n)
        area = sum(1 for x in range(s.W) for z in range(s.D) if s.g[x][z] != OUT)
        dead = sum(1 for i in s.rooms if s.deg[i] <= 1)
        # loops of the lane network: holes of the lane region (complement components not touching the outside)
        comp = s.comps({(x, z) for x in range(-1, s.W+1) for z in range(-1, s.D+1)} - s.lane)
        holes = sum(1 for c in comp if not any(x < 0 or z < 0 or x >= s.W or z >= s.D or s.g[x][z] == OUT for x, z in c))
        st = collections.Counter(R["style"] for R in s.rooms.values())
        clu = len({R["grp"] for R in s.rooms.values() if R["style"] in ("hall",)})
        return dict(area=area, rooms=len(s.rooms), lane=round(100*len(s.lane)/area), run=run, loops=holes, clusters=clu, single=st["single"], open=st["open"], row=st["row"], lost=len(s.lost), deadpct=round(100*dead/max(1, len(s.rooms))))
    def draw(s, px=9, title=""):
        im = Image.new("RGB", (s.W*px+2*px, s.D*px+2*px+16), "white"); d = ImageDraw.Draw(im)
        col = {}
        def color(i):
            R = s.rooms[i]; t = R["style"]
            if t == "single": return (150, 185, 225)
            if t == "open": return (190, 222, 240)
            if t == "pod": return (135, 200, 190)
            if t == "row": return (196, 186, 232)
            if t == "nook": return (205, 205, 190)
            h = (R["grp"]*0.381) % 1; l = .88 if t == "hall" else .82 if t == "ante" else .7
            return tuple(int(255*v) for v in colorsys.hls_to_rgb(.02+.13*h, l, .75))
        off = px
        def R_(c, k): d.rectangle([off+c[0]*px, 16+off+c[1]*px, off+c[0]*px+px-1, 16+off+c[1]*px+px-1], fill=k)
        for i, R in s.rooms.items():
            k = color(i)
            for c in R["cells"]: R_(c, k)
        for c in s.lane: R_(c, (236, 235, 230))
        for x in range(-1, s.W):
            for z in range(-1, s.D):
                a = s.idof.get((x, z), -99)
                for dx, dz in ((1, 0), (0, 1)):
                    b = s.idof.get((x+dx, z+dz), -99)
                    if a == b: continue
                    w = 3 if -99 in (a, b) else 2
                    if dx: d.line([off+(x+1)*px, 16+off+z*px, off+(x+1)*px, 16+off+(z+1)*px], fill="black", width=w)
                    else: d.line([off+x*px, 16+off+(z+1)*px, off+(x+1)*px, 16+off+(z+1)*px], fill="black", width=w)
        for i, j, (a, b) in s.doors:
            if a[0] != b[0]: X = off+max(a[0], b[0])*px; d.line([X, 16+off+a[1]*px+1, X, 16+off+(a[1]+1)*px-1], fill=(20, 170, 60), width=4)
            else: Z = 16+off+max(a[1], b[1])*px; d.line([off+a[0]*px+1, Z, off+(a[0]+1)*px-1, Z], fill=(20, 170, 60), width=4)
        st = s.stats(); d.text((4, 2), f"{title} {s.massname}  {st['area']} m2, {st['rooms']} rooms, {st['clusters']} clusters, {st['single']} single, {st['open']} open, sight {st['run']} m, loops {st['loops']}, lost {st['lost']}", fill="black")
        return im

def sheet(kind, seeds, path, masses=None):
    ims = [Plan(kind, s, masses[i % len(masses)] if masses else None).draw(title=kind) for i, s in enumerate(seeds)]
    cols = 2; W = max(i.width for i in ims)+10; rows = [ims[k:k+cols] for k in range(0, len(ims), cols)]
    H = sum(max(i.height for i in r)+8 for r in rows)
    im = Image.new("RGB", (W*cols, H), "white"); y = 0
    for r in rows:
        for c, i in enumerate(r): im.paste(i, (c*W, y))
        y += max(i.height for i in r)+8
    im.save(path)

if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    for kind in KINDS:
        sheet(kind, range(1, 1+len(KINDS[kind]["mass"])), f"{out}/{kind}.png", KINDS[kind]["mass"])
    tot = collections.defaultdict(list)
    for kind in KINDS:
        for s in range(60):
            for k, v in Plan(kind, 100+s).stats().items(): tot[k].append(v)
        print(kind, {k: (round(sum(v)/len(v), 1), min(v), max(v)) for k, v in tot.items()}); tot.clear()
