"""v2: blocks first (a staggered partition of the mass), corridors then along the edges between blocks: the sparsest network
that touches every block, with turns, plus a few loops. Never along the facade."""
import random, sys, os, collections, heapq
import layouts_base as A
from layouts_base import FREE, OUT, D4, MASS

KINDS = {
 "hospital": dict(mass=["figure8", "comb", "deep", "pinwheel", "campus", "cross", "stagger"], block=(8, 15), w=dict(cluster=4, rows=3, back=4, single=.6, open=.5, pinwheel=3), main=3, room=(4, 6), loops=4, hall=.3),
 "station":  dict(mass=["pinwheel", "campus", "z", "stagger", "cross", "deep"], block=(10, 18), w=dict(cluster=3, rows=1, back=1, single=3.5, open=1, pinwheel=3), main=3, room=(4, 8), loops=3, hall=.35),
 "office":   dict(mass=["deep", "figure8", "stagger", "cross", "comb", "z", "campus"], block=(8, 16), w=dict(cluster=3, rows=2, back=2, single=1, open=4, pinwheel=2), main=3, room=(3, 6), loops=4, hall=.35),
 "garage":   dict(mass=["stagger", "z", "campus", "deep", "comb"], block=(13, 24), w=dict(cluster=2, rows=2, back=1, single=6, open=1, pinwheel=1), main=3, room=(4, 7), loops=2, hall=.2),
}
A.KINDS.update(KINDS)

class Plan(A.Plan):
    def __init__(s, kind, seed, mass=None):
        s.r = r = random.Random(seed); s.k = KINDS[kind]; s.kind = kind
        s.massname = mass or r.choice(s.k["mass"])
        s.wings = MASS[s.massname](r, r.uniform(.95, 1.15))
        s.W = max(x+w for x, z, w, d in s.wings); s.D = max(z+d for x, z, w, d in s.wings)
        s.g = [[OUT]*s.D for _ in range(s.W)]
        for x, z, w, d in s.wings:
            for i in range(x, x+w):
                for j in range(z, z+d): s.g[i][j] = FREE
        s.lane = set(); s.rooms = {}; s.next = 0; s.halls = 0
        s.partition(); s.grow(); s.ring(); s.carve_all(); s.fill2(); s.access()

    # ---------------------------------------------------------------- blocks
    def partition(s):
        cells = {(x, z) for x in range(s.W) for z in range(s.D) if s.g[x][z] != OUT}
        s.blocks = []
        for q in s.decompose(cells, big=True): s.cutblock(q)
        s.bl = {}
        # slivers join the neighbour they share the most with
        for i, q in enumerate(s.blocks):
            x, z, w, d = q
            for a in range(x, x+w):
                for b in range(z, z+d): s.bl[(a, b)] = i
        for i, (x, z, w, d) in enumerate(s.blocks):
            if min(w, d) >= 5 and w*d >= 45: continue
            n = collections.Counter()
            own = [c for c in s.bl if s.bl[c] == i]
            for c in own:
                for dx, dz in D4:
                    j = s.bl.get((c[0]+dx, c[1]+dz))
                    if j is not None and j != i: n[j] += 1
            if n:
                j = n.most_common(1)[0][0]
                for c in own: s.bl[c] = j
    def cutblock(s, q):
        x, z, w, d = q; r = s.r; lo, hi = s.k["block"]
        lim = r.randint(lo, hi)
        if max(w, d) <= max(lim, 10) or (max(w, d) <= hi and r.random() < .45): s.blocks.append(q); return
        along = w >= d if r.random() < .85 or max(w, d) > 1.7*min(w, d) else w < d
        L = w if along else d
        if L < 2*7: s.blocks.append(q); return
        c = r.randint(max(7, int(L*.3)), min(L-7, int(L*.7)))
        if along: s.cutblock((x, z, c, d)); s.cutblock((x+c, z, w-c, d))
        else: s.cutblock((x, z, w, c)); s.cutblock((x, z+c, w, d-c))

    # ---------------------------------------------------------------- the edges between blocks, as a graph of grid points
    def edges(s):
        s.adj = collections.defaultdict(dict); s.cost = {}
        def depth(x, z):          # how far a grid point is from the facade (to 5)
            for k in range(1, 6):
                if not all((x+i, z+j) in s.bl for i in (-k, k-1) for j in (-k, k-1)) or not all((x+i, z+j) in s.bl for i, j in ((-k, 0), (k-1, 0), (0, -k), (0, k-1))): return k-1
            return 5
        dep = {(x, z): depth(x, z) for x in range(1, s.W) for z in range(1, s.D) if all((x-i, z-j) in s.bl for i in (0, 1) for j in (0, 1))}
        s.dep = dep
        for (x, z) in dep:
            for q, c1, c2 in (((x+1, z), (x, z-1), (x, z)), ((x, z+1), (x-1, z), (x, z))):
                if q not in dep: continue
                i, j = s.bl[c1], s.bl[c2]
                s.adj[(x, z)][q] = s.adj[q][(x, z)] = (i, j)
                k = 1.0 if i != j else 2.6
                m = min(dep[(x, z)], dep[q])
                if m < 4: k += (4-m)*1.1
                s.cost[((x, z), q)] = s.cost[(q, (x, z))] = k
        def paths(src): pass
    def paths(s, src, limit=1e9, skip=None, straight=15, avoid=None):
        best = {}; par = {}; h = []; n = 0
        for a in sorted(src): heapq.heappush(h, (0.0, n, a, None, 0)); n += 1
        while h:
            c, _, a, d, run = heapq.heappop(h)
            if (a, d) in best: continue
            if c > limit: break
            best[(a, d)] = c
            for b in s.adj[a]:
                e = (b[0]-a[0], b[1]-a[1])
                if d and e == (-d[0], -d[1]): continue
                if skip and (min(a, b), max(a, b)) in skip: continue
                nr = run+1 if e == d else 1
                if avoid and a in avoid and a not in src: continue
                if avoid and b not in avoid and c > 8 and any((b[0]+i, b[1]+j) in avoid for i, j in ((2, 0), (-2, 0), (0, 2), (0, -2), (3, 0), (-3, 0), (0, 3), (0, -3)) if (i or j) and (i*e[0]+j*e[1]) == 0): continue
                nc = c + (.3 if skip is None and (min(a, b), max(a, b)) in s.nete else s.cost[(a, b)]) + (1.6 if d and e != d else 0) + (2.5 if nr > straight else 0)
                if (b, e) not in best and ((b, e) not in par or par[(b, e)][1] > nc):
                    par[(b, e)] = ((a, d), nc); heapq.heappush(h, (nc, n, b, e, nr)); n += 1
        return best, par
    def back(s, par, k):
        p = []
        while k: p.append(k[0]); k = par[k][0] if k in par else None
        return p[::-1]
    def grow(s):
        s.edges()
        s.net = set(); s.nete = set(); s.hopeless = set(); s.unreached = 0
        ids = set(s.bl.values()); s.size = collections.Counter(s.bl.values())
        # it starts at the block of the entrance: on the front (z = 0 side), the nearest edge point to it
        front = sorted(s.bl, key=lambda c: (c[1], abs(c[0]-s.W*s.r.uniform(.2, .8))))[0]
        s.entry = front
        pts = [p for p in s.adj if s.dep[p] >= 4] or list(s.adj)
        start = min(pts, key=lambda p: abs(p[0]-front[0])+abs(p[1]-front[1]))
        s.net.add(start); touch = collections.Counter()
        for guard in range(200):
            left = [i for i in ids if touch[i] < 3 and s.size[i] >= 30 and i not in s.hopeless]
            if not left: break
            best, par = s.paths(s.net)
            pick = None
            for (a, d), c in best.items():
                if not d: continue
                b = (a[0]-d[0], a[1]-d[1])
                if (min(a, b), max(a, b)) in s.nete: continue
                pair = set(s.adj[a][b])
                gain = sum(s.size[i] for i in pair if i in left)
                if gain and s.dep[a] < 3: continue
                if not gain: continue
                score = (c+3)/gain**.5 * s.r.uniform(.8, 1.25)
                if pick is None or score < pick[0]: pick = (score, (a, d))
            if pick is None:
                s.unreached = len(left); break
            s.addpath(s.back(par, pick[1]), touch)
            # on a little: the block is met along at least 3 m
            a, d = pick[1]
            for _ in range(2):
                b = (a[0]+d[0], a[1]+d[1])
                if b in s.adj[a] and s.dep[b] >= 4: s.addpath([a, b], touch); a = b
                else: break
    def addpath(s, p, touch=None):
        for a, b in zip(p, p[1:]):
            e = (min(a, b), max(a, b))
            if e in s.nete: continue
            s.nete.add(e); s.net.add(a); s.net.add(b)
            if touch is not None:
                for i in set(s.adj[a][b]): touch[i] += 1
    def netdist(s, a):
        n = collections.defaultdict(list)
        for x, y in s.nete: n[x].append(y); n[y].append(x)
        d = {a: 0}; q = collections.deque([a])
        while q:
            x = q.popleft()
            for y in n[x]:
                if y not in d: d[y] = d[x]+1; q.append(y)
        return d
    def ring(s):
        """loops: new ways between points of the network that are far apart along it; a tree always has its far ends"""
        for _ in range(s.k["loops"]):
            n = collections.Counter()
            for x, y in s.nete: n[x] += 1; n[y] += 1
            ends = [p for p in s.net if n[p] == 1]; rest = sorted(s.net)
            s.r.shuffle(ends); s.r.shuffle(rest)
            cand = None
            for a in (ends + rest)[:9]:
                dist = s.netdist(a)
                near = {q for q in s.net if dist.get(q, 0) < 9}
                best, par = s.paths({a}, limit=62, skip=s.nete, avoid=s.net)
                for (y, d), c in best.items():
                    if y not in s.net or y in near or not d: continue
                    p = s.back(par, (y, d)); score = dist.get(y, 0) - 1.1*len(p) - .25*c
                    if score > 9 and (cand is None or score > cand[0]): cand = (score, p)
            if not cand: break
            s.addpath(cand[1])
    def carve_all(s):
        n = collections.defaultdict(set)
        for a, b in s.nete: n[a].add(b); n[b].add(a)
        # the main way: from the entrance to the farthest point, 3 m; the rest 2 m
        st = min(s.net, key=lambda p: abs(p[0]-s.entry[0])+abs(p[1]-s.entry[1]))
        dist = s.netdist(st); far = max(dist, key=dist.get); s.main = set(); p = far
        while p != st:
            s.main.add(p); p = min(n[p], key=lambda q: dist.get(q, 1e9))
        s.main.add(st); s.far = far; s.st = st; s.farlen = dist[far]
        for (x, z) in s.net:
            s.carve(x-1, z-1, 2, 2)
            if (x, z) in s.main and s.k["main"] == 3: s.carve(x-1, z-1, 3, 3) if s.inside(x+1, z) and s.inside(x, z+1) and s.inside(x+1, z+1) else None
        # a hall at a crossing now and then
        for (x, z) in sorted(s.net):
            if len(n[(x, z)]) >= 3 and s.r.random() < s.k["hall"] and all(s.inside(x+i, z+j) for i in (-4, 3) for j in (-4, 3)):
                w, d = s.r.choice([(5, 6), (6, 5), (6, 6), (4, 7), (7, 4)])
                s.carve(x-w//2, z-d//2, w, d); s.halls += 1
        # the entrance: from the facade to the network, a lobby
        x, z = s.entry
        ex = st
        for i in range(min(x, ex[0]), max(x, ex[0])+1):
            s.carve(i, ex[1]-1, 2, 2)
        s.lobby = set()
        # nothing of a lane thinner than 2 remains on its own
        for _ in range(2):
            for c in list(s.lane):
                if sum((c[0]+dx, c[1]+dz) in s.lane for dx, dz in D4) <= 1: s.lane.discard(c)
        cs = sorted(s.comps(s.lane), key=len, reverse=True)
        s.lane = cs[0] if cs else set()

    # ---------------------------------------------------------------- what the blocks become
    def fill2(s):
        grp = 0
        rest = collections.defaultdict(set)
        for c, i in s.bl.items():
            if c not in s.lane: rest[i].add(c)
        for i in sorted(rest):
            for comp in s.comps(rest[i]):
                if len(comp) < 8:
                    for c in comp: s.pending = getattr(s, "pending", []) + [c]
                    continue
                xs = [c[0] for c in comp]; zs = [c[1] for c in comp]
                q = (min(xs), min(zs), max(xs)-min(xs)+1, max(zs)-min(zs)+1)
                x, z, w, d = q; area = len(comp); r = s.r; m = min(w, d)
                on = s.contact(comp, q)
                sides = [k for k in on if on[k] >= 2]
                opt = dict(s.k["w"])
                if area < 64 or m < 4: opt = dict(single=1)
                elif m <= 8: opt = dict(rows=4, single=1 if area < 110 or s.kind in ("garage", "station") else .15)
                else:
                    opt.pop("rows")
                    if area < 100 or not sides: opt.pop("cluster")
                    if m < 11 or area < 130 or not sides: opt.pop("pinwheel")
                    if area < 120: opt.pop("open")
                    if not ("W" in sides and "E" in sides or "S" in sides and "N" in sides): opt.pop("back")
                    if area > 150 and s.kind not in ("garage", "station"): opt["single"] = .25
                before = set(s.rooms); grp += 1
                getattr(s, "b_"+r.choices(list(opt), list(opt.values()))[0])(q, sides, on, grp)
                # only the block's own cells; crumbs to a neighbour in the block
                mine = [k for k in s.rooms if k not in before]; left = set(comp)
                for k in mine:
                    s.rooms[k]["cells"] &= comp
                    cs = sorted(s.comps(s.rooms[k]["cells"]), key=len, reverse=True)
                    s.rooms[k]["cells"] = cs[0] if cs else set()
                for k in mine:
                    C = s.rooms[k]["cells"]
                    xs = [c[0] for c in C]; zs = [c[1] for c in C]
                    if len(C) < 6 or (max(xs)-min(xs) < 2 or max(zs)-min(zs) < 2) and s.rooms[k]["style"] != "hall": del s.rooms[k]
                    else: left -= C
                s.give(left, [k for k in mine if k in s.rooms])
        s.give(set(getattr(s, "pending", [])), list(s.rooms))
    def give(s, left, rooms):
        own = {c: k for k in rooms for c in s.rooms[k]["cells"]}
        left = set(left)
        for _ in range(60):
            if not left: return
            done = []
            for c in sorted(left):
                ks = [own[(c[0]+dx, c[1]+dz)] for dx, dz in D4 if (c[0]+dx, c[1]+dz) in own]
                ks = [k for k in ks if s.rooms[k]["style"] != "hall"] or ks
                if ks: done.append((c, max(set(ks), key=ks.count)))
            if not done: break
            for c, k in done: own[c] = k; s.rooms[k]["cells"].add(c); left.discard(c)
        for comp in s.comps(left):
            if len(comp) >= 9: i = s.next; s.next += 1; s.rooms[i] = dict(cells=set(comp), style="nook", grp=None)
    def contact(s, comp, q):
        x, z, w, d = q; on = dict(W=0, E=0, S=0, N=0)
        for (a, b) in comp:
            if (a-1, b) in s.lane and a < x+2: on["W"] += 1
            if (a+1, b) in s.lane and a >= x+w-2: on["E"] += 1
            if (a, b-1) in s.lane and b < z+2: on["S"] += 1
            if (a, b+1) in s.lane and b >= z+d-2: on["N"] += 1
        return on
    def lanespan(s, q, side):
        x, z, w, d = q; res = []
        for i in range(w if side in "SN" else d):
            p = {"S": [(x+i, z-1), (x+i, z)], "N": [(x+i, z+d), (x+i, z+d-1)], "W": [(x-1, z+i), (x, z+i)], "E": [(x+w, z+i), (x+w-1, z+i)]}[side]
            if any(c in s.lane for c in p): res.append(i)
        return res
    def draw(s, px=9, title=""):
        im = super().draw(px, title)
        from PIL import ImageDraw
        d = ImageDraw.Draw(im)
        for p, k in ((s.st, (200, 30, 30)), (s.far, (30, 90, 200))):
            d.ellipse([px+p[0]*px-6, 16+p[1]*px-6, px+p[0]*px+6, 16+p[1]*px+6], fill=k)
        return im
    def stats(s):
        st = super().stats(); st["walk"] = s.farlen; st["halls"] = s.halls; st["unreached"] = s.unreached; st["loops"] = len(s.nete)-len(s.net)+1; return st

A.Plan = Plan
def sh(kind, out): A.sheet(kind, range(1, 1+len(KINDS[kind]["mass"])), f"{out}/{kind}4.png", KINDS[kind]["mass"])
def one(a): return Plan(*a).stats()
if __name__ == "__main__":
    import multiprocessing as mp
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    with mp.Pool(10) as pool:
        for kind in KINDS:
            r = [pool.apply_async(sh, (kind, out))] if False else None
        jobs = [pool.apply_async(sh, (kind, out)) for kind in KINDS]
        for j in jobs: j.get()
        for kind in KINDS:
            tot = collections.defaultdict(list)
            for st in pool.map(one, [(kind, 100+s) for s in range(40)]):
                for k, v in st.items(): tot[k].append(v)
            print(kind, {k: (round(sum(v)/len(v), 1), min(v), max(v)) for k, v in tot.items()}, flush=True)
