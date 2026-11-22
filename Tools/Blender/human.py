"""The players' rigged humans: four people of four departments (and the station scientist of the old S16 look).

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/human.py --
          <out: Assets/_Game/Art/Models/Humans> [backend|qa|sysadmin|designer] [preview=<file.png>] [static=<file.fbx>] [draft]

Writes <out>/human_<who>.fbx — one armature ("Rig") with skinned meshes and the takes Idle, Walk, Run, CrouchIdle,
CrouchWalk, Down, Sit, Climb, Terminal, Work — and its maps in <out>/../../Textures/Humans/: human_<who>_skin_*.png
(head, hands, eyes) and human_<who>_outfit_*.png (everything else), each a _base (albedo), a _normal (tangent space)
and a _mask (G occlusion, A smoothness). `static=` also writes the meshes alone in the rest pose (what
Tools/Blender/review.py looks at: an FBX with takes comes in posed); `preview=` writes contact sheets of the sculpt,
the game mesh and every take; `draft` sculpts coarser (a quick look at shapes).

How a figure is made (Tools/Blender/humanlib):
  * the body is anatomy as a distance field (body.py): pelvis, rib cage, shoulder girdle, the muscles of the limbs —
    one build per person (broad, slim, soft), the joints the same for all, so every figure plays the same takes;
  * the head is a skull, a jaw, a nose, lips and lids by the measurements of a real head, and a `Face` of proportions
    makes a person of it (face.py): four different faces, hair as a volume in combed locks, a beard;
  * clothes are the body let out by the cloth's ease, hanging where cloth hangs, hollow at the cuffs, with a few
    deliberate folds (clothes.py); hands and shoes are fields too;
  * each piece is meshed dense (surface nets), painted on its vertices, cut to a triangle budget, unwrapped, and the
    dense sculpt is baked onto it: normals, colour, occlusion, smoothness (build.py);
  * the rest pose is an A-pose; the skin follows the bones by distance within each piece's own bones (anim.py holds
    the skeleton and the takes).

Parts (the game's renderers; PlayerLooks.PartColor tints by the name's start):
  BodyClothes  the department's garment (hoodie, shirt, vest, jumper)      — tinted the department's colour
  AccentHead   head, neck, hands                                           — tinted the player's skin tone
  AccentBand   armband, lanyard / scarf / cable                            — tinted the team colour
  EyeDetails   headphones, glasses, belt                                   — tinted dark
  Sclera       the eyes                                                    — its own colours
  Outfit       trousers, shoes, hair, beard, everything of its own colour  — its own colours
Blender: x the figure's left, -y its front, z up; Unity gets +Z forward (bake axis conversion on import).
"""
import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from humanlib import anim, body, build, cards, clothes, face, sdf, view  # noqa: E402
from humanlib.body import Build, J  # noqa: E402
from humanlib.build import Piece  # noqa: E402
from humanlib.sdf import cone, ell, gauss, smax, smin, win  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
WHO = next((a for a in argv if a in face.PEOPLE), "scientist")
DRAFT = "draft" in argv
PREVIEW = next((os.path.abspath(a[8:]) for a in argv if a.startswith("preview=")), None)
STATIC = next((os.path.abspath(a[7:]) for a in argv if a.startswith("static=")), None)
ITEM_ARG = next((a[5:] for a in argv if a.startswith("item=")), None)     # item=hoodie,bob | item=all: wardrobe items made for this person
_pos = [a for a in argv if a not in face.PEOPLE and a != "draft" and "=" not in a]
OUT = os.path.abspath(_pos[0] if _pos else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models", "Humans"))
TEXTURES = os.path.abspath(os.path.join(OUT, "..", "..", "Textures", "Humans"))
K = 1.6 if DRAFT else 1.0                       # the sculpt's cell size, times this

HEAD_C = np.array((0.0, -0.004, 1.660))         # the head's centre on the body
HEAD_S = 1.07                                   # the head on the body is this much bigger than face.py's (a real head is 1/7.5 of the height)


def H(p):
    """A point of the body in the head's own frame (face.py's metres)."""
    return (np.asarray(p, float) - HEAD_C) / HEAD_S

BUILDS = {
    "backend": Build(shoulders=0.97, chest=0.95, waist=0.93, limbs=0.92),
    "qa": Build(shoulders=0.88, chest=0.88, waist=0.80, hips=1.10, limbs=0.86, neck=0.84, bust=0.008, soft=0.6),
    "sysadmin": Build(shoulders=1.08, chest=1.10, waist=1.22, belly=0.045, hips=1.08, limbs=1.14, neck=1.12, soft=0.7),
    "designer": Build(shoulders=0.87, chest=0.86, waist=0.76, hips=1.05, limbs=0.80, neck=0.82, bust=0.006, soft=0.5),
    "scientist": Build(),
}
# what each wears: the top's cut, trousers and shoes (colours are linear RGB)
OUTFITS = {
    "backend": dict(top=dict(ease=0.015, sleeve_ease=0.012, hem=0.965, straight=0.75, neck_z=1.498, neck_r=0.072, rib=0.045), hood=True,
                    trousers=(0.10, 0.14, 0.24), taper=0.0, shoe="sneaker", shoe_colour=(0.78, 0.78, 0.76), sole=(0.85, 0.85, 0.83)),
    "qa": dict(top=dict(ease=0.007, sleeve_ease=0.008, hem=1.032, straight=0.15, neck_z=1.482, neck_r=0.062, folds=0.8), collar=True, tucked=True,
               trousers=(0.085, 0.085, 0.10), taper=0.014, shoe="flat", shoe_colour=(0.05, 0.045, 0.045), sole=(0.03, 0.03, 0.03)),
    "sysadmin": dict(top=dict(ease=0.008, sleeve_ease=0.010, hem=1.032, straight=0.3, neck_z=1.482, neck_r=0.066, cuff_t=0.46, rib=0.03), vest=True, collar=True,
                     tucked=True, shirt=(0.42, 0.47, 0.52), trousers=(0.17, 0.16, 0.12), taper=-0.006, cargo=True, trouser_ease=0.014, shoe="boot", shoe_colour=(0.16, 0.10, 0.06),
                     sole=(0.04, 0.035, 0.03)),
    "designer": dict(top=dict(ease=0.014, sleeve_ease=0.012, hem=0.985, straight=0.5, neck_z=1.486, neck_r=0.066, rib=0.035, folds=0.8),
                     trousers=(0.045, 0.045, 0.05), taper=-0.020, trouser_hem=0.125, shoe="sneaker", shoe_colour=(0.60, 0.16, 0.14), sole=(0.88, 0.87, 0.84)),
    "scientist": dict(top=dict(ease=0.008, sleeve_ease=0.009, hem=1.032, straight=0.2, neck_z=1.482, neck_r=0.064), collar=True, tucked=True, coat=True,
                      trousers=(0.12, 0.13, 0.15), taper=0.0, shoe="flat", shoe_colour=(0.07, 0.06, 0.05), sole=(0.03, 0.03, 0.03)),
}

TORSO = ["hips", "spine", "chest"]
RADIUS = {"hips": 0.17, "spine": 0.15, "chest": 0.17, "neck": 0.06, "head": 0.10, "shoulder": 0.085, "upper_arm": 0.055, "forearm": 0.045,
          "hand": 0.040, "thigh": 0.085, "shin": 0.06, "foot": 0.055, "toe": 0.04}


def _step(x, lo, hi):
    t = np.clip((x - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def hand_weights(points, sd):
    """Skin weights of a hand's points (side "L" / "R"): every finger follows its own three bones — a point belongs to
    the finger it is nearest to, its weight passes from bone to bone across each joint and from the hand to the finger
    across the knuckle — and the rest follows the hand and the forearm by distance. Fingers lie millimetres apart:
    weights by the distance to every bone would drag a finger after its neighbour."""
    p = np.array(points, float)
    if sd == "R":
        p[:, 0] *= -1
    chains = clothes.hand_chains()
    names = list(chains)
    n = len(p)
    near = np.full(n, 1e9)
    own = np.zeros(n, int)
    along = np.zeros((len(names), n, 3))                           # for every finger: metres past the head of each of its bones
    for k, name in enumerate(names):
        pts = chains[name]
        best = np.full(n, 1e9)
        for i in range(3):
            d, _ = sdf.seg_dist(p, pts[i], pts[i + 1])
            ab = pts[i + 1] - pts[i]
            along[k, :, i] = ((p - pts[i]) @ ab) / np.linalg.norm(ab)
            best = np.minimum(best, d)
        own = np.where(best < near, k, own)
        near = np.minimum(near, best)
    seg = {b: (np.array(a), np.array(c)) for b, (a, c) in anim.bone_segments().items()}
    out = build.weights(np.array(points, float), seg, [f"forearm.{sd}", f"hand.{sd}"], RADIUS)
    for k, name in enumerate(names):
        mine = own == k
        a = along[k]
        # into the finger across the knuckle (the thumb's first bone lies inside the palm: it takes the ball of the thumb,
        # as far as the thumb's own flesh reaches), then from bone to bone across each joint
        if name == "thumb":
            w_in = _step(a[:, 0], -0.004, 0.022) * mine * _step(0.030 - near, 0.0, 0.012)
        else:
            w_in = _step(a[:, 0], -0.007, 0.006) * mine
        w23 = _step(a[:, 1], -0.005, 0.005)
        w3 = _step(a[:, 2], -0.004, 0.004)
        for b in list(out):
            out[b] = out[b] * (1 - w_in)
        for i, w in enumerate((w_in * (1 - w23), w_in * w23 * (1 - w3), w_in * w23 * w3)):
            out[f"{name}.{i + 1}.{sd}"] = w
    return out


def both(*names):
    return [f"{n}.{s}" for n in names for s in ("L", "R")]


def mx(p):
    q = np.array(p, float)
    q[..., 0] = np.abs(q[..., 0])
    return q


def mirrored(obj, name):
    o = obj.copy()
    o.data = obj.data.copy()
    o.name = name
    bpy.context.collection.objects.link(o)
    pts = sdf.points_of(o)
    pts[:, 0] *= -1
    sdf.set_points(o, pts)
    o.data.flip_normals()
    return o


def sculpt(name, f, lo, hi, h, mirror=False):
    t = time.time()
    o = sdf.mesh(name, f, lo, hi, h * K, mirror=mirror)
    sdf.keep_biggest(o)
    print(f"SCULPT {name}: {len(o.data.vertices)} verts, {time.time() - t:.1f} s")
    return o


# --------------------------------------------------------------------------- the figure

class Who:
    """A person the pieces are made for: their build, face and colours (an item made for one fits their body only)."""
    def __init__(self, who):
        self.who, self.person, self.b = who, face.PEOPLE[who], BUILDS[who]
        self.f = self.person.face
        f = self.f
        self.head_f = lambda p: face.head_with_ears(H(p), f) * HEAD_S
        style = self.person.hair
        self.hair_f = lambda p: face.hair(H(p), f, style) * HEAD_S             # their own hair (what a head torch's band lies on)


# The wardrobe: every item a slot and the outfit (OUTFITS) whose cut and colours it has; made for any person's build.
# Things that lie on a garment come with it (the armband, a hood and its strings, a lanyard and badge, a scarf; a belt and
# a cable coil with the trousers). Hair, a beard and what sits on the head are made on the wearer's own head.
ITEMS = {
    "hoodie": ("top", "backend"), "shirt": ("top", "qa"), "vest": ("top", "sysadmin"), "jumper": ("top", "designer"),
    "jeans": ("bottom", "backend"), "slacks": ("bottom", "qa"), "cargo": ("bottom", "sysadmin"), "wide": ("bottom", "designer"),
    "sneakers": ("feet", "backend"), "flats": ("feet", "qa"), "boots": ("feet", "sysadmin"), "red_sneakers": ("feet", "designer"),
    "messy": ("hair", None), "bob": ("hair", None), "buzz": ("hair", None), "bun": ("hair", None),
    "beard": ("beard", None),
    "headphones": ("head", None), "torch": ("head", None), "beret": ("head", None),
    "glasses": ("eyes", None),
}
# what each person wears out of the box (the game's HumanOutfits config says the same; the game can swap any of it)
DEFAULTS = {
    "backend": ["hoodie", "jeans", "sneakers", "messy", "headphones"],
    "qa": ["shirt", "slacks", "flats", "bob", "glasses"],
    "sysadmin": ["vest", "cargo", "boots", "buzz", "beard", "torch"],
    "designer": ["jumper", "wide", "red_sneakers", "bun", "beret"],
}


def figure(who, items=None, body_too=True):
    """Sculpts and paints the pieces of a person: the body (head, hands with the forearms, eyes) and the items — their
    own outfit by default. [Piece], every item's pieces with its slot."""
    c = Who(who)
    items = DEFAULTS[who] if items is None else items
    pieces = body_pieces(c) if body_too else []
    for name in items:
        made = ITEM_MAKERS[name](c, items)
        for p in made:
            p.slot = ITEMS[name][0]
        pieces += made
    return pieces


def body_pieces(c):
    """The skin: the head with the neck, both hands with the forearms (bare under rolled sleeves, hidden in long ones),
    the eyes."""
    person, b, f = c.person, c.b, c.f
    pieces = []
    head_f = c.head_f

    def skin_f(p):
        d = smin(head_f(p), body.neck(mx(p), Build(neck=b.neck, soft=b.soft)), 0.014)
        d = smax(d, np.minimum(p[..., 1] - 0.050, 1.470 - p[..., 2]), 0.004)      # the neck's base stays inside the garment's back (it showed under a collar)
        return smax(d, 1.405 - p[..., 2], 0.004)
    hd = sculpt("head", skin_f, (0, -0.15, 1.39), (0.113, 0.14, 1.82), 0.0013, mirror=True)
    rgb, gloss = paint_head(H(sdf.points_of(hd)), person)
    build.paint(hd, rgb, gloss)

    def keep_face(pts):
        p = H(pts)
        front = np.clip((-p[:, 1] - 0.035) / 0.03, 0, 1) * np.clip((0.065 - p[:, 2]) / 0.02, 0, 1) * np.clip((p[:, 2] + 0.115) / 0.02, 0, 1)
        q = mx(p)
        fine = sum(np.exp(-(np.linalg.norm((q - c_) / r, axis=1)) ** 2) for c_, r in (
            ((0, -0.095, face.mouth_z(f)), (0.032, 0.022, 0.016)), ((0.0315, -0.08, 0.008), (0.022, 0.022, 0.014)), ((0, -0.105, -0.034), (0.022, 0.022, 0.018))))
        ears = np.clip((q[:, 0] - 0.068) / 0.004, 0, 1) * (np.abs(p[:, 2]) < 0.04)
        neck = 0.50 * np.clip((-0.095 - p[:, 2]) / 0.02, 0, 1)                    # a round neck, not a prism
        return np.maximum(np.maximum(0.45 * front + 0.5 * np.clip(fine, 0, 1), 0.35 * ears), neck)
    # (6500 triangles, the eyes, the nose and the lips held hardest: with 4400 a lip was six segments wide and the game
    # mesh cut under the nose's wings — no bake makes that smooth)
    # (fine: a second pass of the bake looks for the sculpt only 4 mm off the game mesh — from 12 mm the upper lip found the
    # nose's underside first and baked it in: knobs under the nose, a cracked lip, a shadow round the nose's wings)
    pieces.append(Piece("head", "AccentHead", "skin", hd, 6500, ["chest", "neck", "head"], keep=keep_face, texel=1.6, fine=0.004, angle=45.0))   # (at 66° the ear folded onto itself in UV)

    # the hands always with the forearms: any top may roll its sleeves up (a long sleeve hides the forearm; it is hollow at the cuff)
    W = J["wrist"]
    hand = sculpt("hand.L", lambda p: clothes.forearm_skin(p, b, f.female, 0.36), W + (-0.21, -0.12, -0.20), W + (0.14, 0.12, 0.26), 0.0012)
    hp = sdf.points_of(hand)
    build.paint(hand, *paint_hand(hp, build.normals_of(hand)))
    hand_r = mirrored(hand, "hand.R")

    def keep_fingers(pts):
        q = clothes.hand_frame(mx(pts))                 # triangles go to the fingers and the thumb, not to the palm's flat
        return np.clip(np.maximum((q[:, 0] - 0.075) / 0.02, (q[:, 1] - 0.030) / 0.012), 0, 1) * 0.9
    for o, sd in ((hand, "L"), (hand_r, "R")):
        # (1500 triangles left every finger an octagon in a close-up: the fingers take the extra)
        pieces.append(Piece(o.name, "AccentHead", "skin", o, 3000, [f"forearm.{sd}", f"hand.{sd}"], keep=keep_fingers, texel=1.3, fine=(0.0035, 0.0015), angle=40.0))
        # (from 3.5 mm off a finger's side the ray starts inside the next finger — the gap is 2–3 mm — and bakes that one:
        # light blotches between the fingers; the 1.5 mm pass finds the finger's own skin, the game mesh is within 1 mm of it)

    e = face.eye_centre(f)
    for s, sd in ((1, "L"), (-1, "R")):
        cen = HEAD_C + np.array((s * e[0], e[1], e[2])) * HEAD_S
        eye = eyeball(f"eye.{sd}", cen, 48, 24)
        build.paint(eye, *paint_eye((sdf.points_of(eye) - cen) / HEAD_S, person))
        p = Piece(eye.name, "Sclera", "skin", eye, 0, ["head"], texel=2.2)
        p.lo = eyeball(f"eye.{sd}_lo", cen, 14, 8)
        pieces.append(p)
    return pieces


# --------------------------------------------------------------------------- the items

# hair cards of a style: length, segments, width, depth of the deepest layer, coverage (cards over a spot), how near the
# skin they stop, how far their tips stand off (a buzz cut has none: no locks to show, the grain painted on its mass does)
CARDS = {
    "messy": (0.050, 6, 0.009, 0.0010, 2.8, 0.0012, 0.0042),
    "crop": (0.032, 4, 0.008, 0.0010, 2.8, 0.0012, 0.0030),
    "bob": (0.110, 8, 0.012, 0.0012, 2.8, 0.0012, 0.0025),
    "bun": (0.080, 7, 0.010, 0.0010, 2.8, 0.0012, 0.0015),
    "beard": (0.012, 3, 0.0022, 0.0006, 1.3, 0.0004, 0.0040),
}


def hair_cards(name, style, volume, skin, base, cut=None, seed=0):
    """The cards of a style over its sculpted mass (`base`, the roots are spread over it): a Piece of the part "Hair"
    with its own UVs on the strand texture (not baked)."""
    length, segments, width, depth, coverage, stop, tip = CARDS[style]
    rng = np.random.default_rng(1234 + seed)
    keep = lambda pts, nrm: (skin(pts) > stop * 0.5) & (np.ones(len(pts), bool) if cut is None else cut(pts) > 0.002)
    _, _, area = cards.roots_on(base, keep, 1, rng)
    count = int(min(1500, coverage * area / (width * length * 0.8)))
    roots, _, _ = cards.roots_on(base, keep, max(count, 1), rng)
    spine, normals, n, widths = cards.grow(volume, skin, roots, cards.flow_of(style, H), length, segments, width, depth, rng, stop=stop, cut=cut)
    obj = cards.mesh(name, spine, normals, n, widths, rng, tip_lift=tip, v_from=0.5 if style == "beard" else 0.0)
    p = Piece(name, "Hair", "outfit", obj, 0, ["head"])
    p.lo, p.own_uv = obj, True
    print(f"CARDS {name}: {len(obj.data.polygons)} triangles from {count} roots")
    return p


def item_hair(style):
    def make(c, worn):
        hair_f = lambda p: face.hair(H(p), c.f, style) * HEAD_S
        cut = None
        if "beret" in worn:
            loose = hair_f
            hair_f = lambda p: smax(loose(p), -(beret_solid(p) + 0.003), 0.003)     # no hair inside the beret: none to show through it
            cut = lambda p: beret_solid(p)
        # the sculpted mass a little in from the hair's surface, darker: the strands lie over it, no skin between them
        base_f = (lambda p: hair_f(p) + 0.0012) if style in CARDS else hair_f
        hair = sculpt("hair", base_f, (-0.125, -0.15, 1.49), (0.125, 0.17, 1.85), 0.0016)
        build.paint(hair, *paint_hair(H(sdf.points_of(hair)), c.person, 0.45 if style in CARDS else 1.0))
        pieces = [Piece("hair", "Outfit", "outfit", hair, {"bob": 1700, "buzz": 900}.get(style, 1400), ["head"], texel=1.2)]
        if style in CARDS:
            pieces.append(hair_cards("cards", style, hair_f, c.head_f, hair, cut))
        return pieces
    return make


def item_beard(c, worn):
    beard_f = lambda p: face.beard(H(p), c.f) * HEAD_S
    bd = sculpt("beard", lambda p: beard_f(p) + 0.0006, (0, -0.15, 1.50), (0.113, 0.07, 1.70), 0.0013, mirror=True)
    build.paint(bd, *paint_beard(H(sdf.points_of(bd)), c.person))
    return [Piece("beard", "Outfit", "outfit", bd, 1400, ["head"], texel=1.4, fine=0.004),   # (700: its outline a polygon of steps)
            hair_cards("beard_cards", "beard", beard_f, c.head_f, bd, seed=7)]


def trousers_of(c, o):
    waist = 1.040
    return (lambda p: clothes.trousers(p, c.b, ease=o.get("trouser_ease", 0.009), hem=o.get("trouser_hem", 0.075 if o["shoe"] != "boot" else 0.10),
                                       waist=waist, taper=o["taper"], cargo=o.get("cargo", False))), waist


def item_bottom(origin):
    def make(c, worn):
        o, b = OUTFITS[origin], c.b
        tr_f, waist = trousers_of(c, o)
        tr = sculpt("trousers", tr_f, (0, -0.22, 0.05), (0.30, 0.22, 1.08), 0.0032, mirror=True)
        build.paint(tr, *paint_trousers(sdf.points_of(tr), build.normals_of(tr), o["trousers"]))
        pieces = [Piece("trousers", "Outfit", "outfit", tr, 2000, ["hips", "spine"] + both("thigh", "shin"))]
        if o.get("tucked"):
            # a belt where the shirt goes into the trousers, with a buckle
            def belt_f(p):
                d = tr_f(p)
                band = smax(smax(d - 0.0045, -(d + 0.004), 0.001), np.abs(p[..., 2] - (waist - 0.022)) - 0.017, 0.002)
                return smin(band, sdf.box(p, (0, -0.118 - 0.9 * b.belly - (0.02 if b.waist > 1.1 else 0.0), waist - 0.022), (0.022, 0.012, 0.016), 0.003), 0.002)
            belt = sculpt("belt", belt_f, (-0.22, -0.24, waist - 0.06), (0.22, 0.2, waist + 0.02), 0.0018)
            bp = sdf.points_of(belt)
            buckle = (np.abs(bp[:, 0]) < 0.024) & (bp[:, 1] < -0.09) & (np.abs(bp[:, 2] - (waist - 0.022)) < 0.018)
            build.paint(belt, np.where(buckle[:, None], 6.0, 1.0) * np.ones((len(bp), 3)), np.where(buckle, 0.75, 0.4))
            pieces.append(Piece("belt", "EyeDetails", "outfit", belt, 420, ["hips", "spine"]))
        if origin == "sysadmin":
            def cable_f(p):                                                      # a coil of cable at the hip, on its strap
                cc = np.array((0.172 * b.hips + 0.012, 0.0, 0.955))
                r = np.hypot(p[..., 1] - cc[1], p[..., 2] - cc[2])
                d = None
                for k in range(3):
                    t = np.hypot(r - (0.072 - 0.004 * k), p[..., 0] - (cc[0] + 0.009 * k)) - 0.0065
                    d = t if d is None else smin(d, t, 0.002)
                return smin(d, cone(p, (cc[0] - 0.02, 0.0, cc[2] + 0.07), (cc[0] + 0.02, 0.0, cc[2] + 0.075), 0.007), 0.004)
            cb = sculpt("cable", cable_f, (0.12, -0.10, 0.86), (0.24, 0.10, 1.06), 0.0015)
            build.paint(cb, 1.0, 0.45)
            pieces.append(Piece("cable", "AccentBand", "outfit", cb, 420, ["hips", "thigh.L"]))
        return pieces
    return make


def item_feet(origin):
    def make(c, worn):
        o = OUTFITS[origin]
        shoe = sculpt("shoe.L", lambda p: clothes.shoe(p, o["shoe"]), (0.03, -0.21, -0.004), (0.175, 0.12, 0.22), 0.0018)
        build.paint(shoe, *paint_shoe(sdf.points_of(shoe), o))
        return [Piece(s.name, "Outfit", "outfit", s, 720, [f"shin.{sd}", f"foot.{sd}", f"toe.{sd}"]) for s, sd in ((shoe, "L"), (mirrored(shoe, "shoe.R"), "R"))]
    return make


def top_of(c, o, origin):
    """The garment of an outfit made for this body: [Piece] and the outermost garment's surface (what a lanyard, a
    scarf's tail and drawstrings lie on); the armband on the sleeve comes with it."""
    b = c.b
    top_f = lambda p: clothes.top(p, b, **o["top"])
    garment_f = top_f
    if o.get("collar"):
        top_f_plain = top_f
        top_f = lambda p: smin(top_f_plain(p), clothes.collar(p), 0.004)
        garment_f = top_f
    top = sculpt("top", top_f, (0, -0.24, 0.93), (0.62, 0.24, 1.56), 0.0032, mirror=True)
    arms = both("shoulder", "upper_arm", "forearm")
    pieces = []
    if o.get("vest"):
        # under a vest: a shirt of its own colour, and the vest is the department's garment
        build.paint(top, *paint_cloth(sdf.points_of(top), o["shirt"], 0.16, weave=0.03))
        pieces.append(Piece("shirt", "Outfit", "outfit", top, 2200, TORSO + ["neck"] + arms))
        garment_f = lambda p: clothes.vest(p, b)
        vest = sculpt("vest", garment_f, (0, -0.26, 0.97), (0.26, 0.24, 1.54), 0.0032, mirror=True)
        build.paint(vest, *paint_vest(sdf.points_of(vest)))
        pieces.append(Piece("vest", "BodyClothes", "outfit", vest, 1500, TORSO + both("shoulder")))
    else:
        build.paint(top, *paint_top(sdf.points_of(top), origin, o))
        pieces.append(Piece("top", "BodyClothes", "outfit", top, 3000, TORSO + ["neck"] + arms))
    if o.get("hood"):
        hood = sculpt("hood", clothes.hood, (-0.14, 0.0, 1.31), (0.14, 0.18, 1.56), 0.0032)
        build.paint(hood, 1.0, 0.14)
        pieces.append(Piece("hood", "BodyClothes", "outfit", hood, 420, ["chest", "neck", "spine"]))

    # the team colour on everyone's sleeve: an armband
    S, E = np.array((J["shoulder"][0] * b.shoulders, J["shoulder"][1], J["shoulder"][2])), J["elbow"]
    axis = (E - S) / np.linalg.norm(E - S)
    sleeve_f = top_f

    def band_f(p):
        along = (p - S) @ axis
        d = sleeve_f(p)
        near, _ = sdf.seg_dist(p, S, E)
        # (standing clear of the sleeve: the sleeve's game mesh is a few millimetres off its sculpt and showed through a band lying on it)
        return smax(smax(smax(d - 0.0080, -(d - 0.0008), 0.001), np.abs(along - 0.150) - 0.026, 0.002), near - 0.095, 0.002)
    band = sculpt("armband", band_f, S + (-0.09, -0.10, -0.26), S + (0.16, 0.10, 0.02), 0.0016)
    build.paint(band, 1.0, 0.22)
    pieces.append(Piece("armband", "AccentBand", "outfit", band, 300, TORSO + ["neck"] + arms))     # (the sleeve's bones: it moves as the sleeve does)
    return pieces, garment_f


def item_top(origin):
    def make(c, worn):
        o = OUTFITS[origin]
        pieces, surface = top_of(c, o, origin)
        if origin == "backend":
            def strings_f(p):                                                    # the hoodie's drawstrings
                d = np.minimum(*[np.hypot(p[..., 0] - np.interp(p[..., 2], [1.31, 1.467], [s * 0.034, s * 0.026]), 0) for s in (1, -1)])
                z = p[..., 2]
                return np.maximum(np.maximum(surface(p) - 0.0075, -(surface(p)) - 0.003), np.maximum(d - 0.0036, np.maximum(1.31 - z, z - 1.467)))
            st = sculpt("strings", strings_f, (-0.06, -0.2, 1.29), (0.06, 0.0, 1.48), 0.0012)
            build.paint(st, 0.86, 0.2)
            pieces.append(Piece("strings", "Outfit", "outfit", st, 160, ["chest", "spine"]))
        elif origin == "qa":
            def lanyard_f(p):                                                    # a lanyard round the neck to a badge on the chest
                q = mx(p)
                flat = np.hypot(q[..., 0] - np.interp(q[..., 2], [1.215, 1.30, 1.41, 1.474], [0.004, 0.026, 0.046, 0.058]), 0) - 0.0055
                z = q[..., 2]
                strap = np.maximum(shell(surface, p, 0.0008, 0.0022), np.maximum(flat, np.maximum(1.215 - z, z - 1.482)))
                return np.maximum(strap, p[..., 1] + 0.0)                        # only down the front
            ln = sculpt("lanyard", lanyard_f, (-0.08, -0.22, 1.20), (0.08, 0.01, 1.49), 0.0011)
            build.paint(ln, 1.0, 0.25)
            pieces.append(Piece("lanyard", "AccentBand", "outfit", ln, 260, ["chest", "spine", "neck"]))

            def badge_f(p):
                return np.maximum(shell(surface, p, 0.001, 0.004), sdf.box(p * (1, 0, 1), (0.0, 0.0, 1.175), (0.027, 1.0, 0.040), 0.004))
            bg = sculpt("badge", badge_f, (-0.04, -0.22, 1.12), (0.04, -0.03, 1.23), 0.0011)
            gp = sdf.points_of(bg)
            photo = (np.abs(gp[:, 0] + 0.006) < 0.012) & (np.abs(gp[:, 2] - 1.186) < 0.014)
            lines = (gp[:, 2] < 1.166) & (np.sin(gp[:, 2] * 900) > 0.2) & (np.abs(gp[:, 0]) < 0.02)
            col = np.where(photo[:, None], (0.25, 0.3, 0.38), np.where(lines[:, None], (0.3, 0.3, 0.32), (0.86, 0.86, 0.84)))
            build.paint(bg, col, 0.55)
            pieces.append(Piece("badge", "Outfit", "outfit", bg, 120, ["chest", "spine"]))
        elif origin == "designer":
            def scarf_f(p):
                tail = np.maximum(shell(surface, p, 0.0005, 0.011), sdf.box(p * (1, 0, 1), (0.050, 0.0, 1.340), (0.030, 1.0, 0.115), 0.008))
                tail = np.maximum(tail, p[..., 1])                                   # (down the front only)
                return smin(clothes.scarf(p), tail, 0.012)
            sc = sculpt("scarf", scarf_f, (-0.12, -0.20, 1.20), (0.12, 0.14, 1.54), 0.002)
            sp = sdf.points_of(sc)
            knit = 0.90 + 0.10 * np.sin(np.arctan2(sp[:, 1] - 0.02, sp[:, 0]) * 46 + sp[:, 2] * 260) * np.sin(sp[:, 2] * 420)
            build.paint(sc, np.repeat(knit[:, None], 3, 1), 0.1)
            pieces.append(Piece("scarf", "AccentBand", "outfit", sc, 800, ["chest", "neck", "spine"]))
        return pieces
    return make


def item_headphones(c, worn):
    # big over-ear headphones over the hair
    def phones_f(p):
        q = mx(H(p))
        ring = np.abs(np.hypot(q[..., 0] / 0.101, (q[..., 2] + 0.006) / 0.176) - 1.0) * 0.101      # over the hair: an ellipse
        band = np.maximum(np.maximum(ring - 0.0055, np.abs(q[..., 1] - 0.012) - 0.013), -q[..., 2] - 0.02)
        cup = ell(q, (0.086, 0.012, -0.006), (0.020, 0.040, 0.046))
        cushion = ell(q, (0.076, 0.012, -0.006), (0.012, 0.034, 0.040))
        return smin(smin(band, cup, 0.006), cushion, 0.003) * HEAD_S
    ph = sculpt("headphones", phones_f, (-0.135, -0.07, 1.57), (0.135, 0.09, 1.86), 0.0018)
    pp = mx(H(sdf.points_of(ph)))
    build.paint(ph, np.where((pp[:, 0] < 0.082)[:, None] & (pp[:, 2] < 0.05)[:, None], 0.55, 1.0) * np.ones((len(pp), 3)), np.where(pp[:, 0] > 0.09, 0.62, 0.3))
    return [Piece("headphones", "EyeDetails", "outfit", ph, 700, ["head"])]


def item_torch(c, worn):
    # a head torch on its band, lying on the wearer's own hair
    hair_f = c.hair_f

    def torch_f(p):
        q = H(p)
        band = np.maximum(np.abs(hair_f(p) - 0.0025) - 0.0022, (np.abs(q[..., 2] - 0.058) - 0.0105) * HEAD_S)
        lamp = sdf.box(q, (0, -0.103, 0.058), (0.022, 0.014, 0.016), 0.005)
        lens = cone(q, (0, -0.110, 0.058), (0, -0.124, 0.058), 0.012, 0.0125)
        return smin(smin(band, lamp * HEAD_S, 0.003), lens * HEAD_S, 0.002)
    tc = sculpt("torch", torch_f, (-0.12, -0.15, 1.69), (0.12, 0.14, 1.76), 0.0013)
    tp = H(sdf.points_of(tc))
    lens = (tp[:, 1] < -0.1215) & (np.hypot(tp[:, 0], tp[:, 2] - 0.058) < 0.0105)
    lampbox = (tp[:, 1] < -0.088) & (np.abs(tp[:, 0]) < 0.03)
    col = np.where(lens[:, None], (0.85, 0.88, 0.8), np.where(lampbox[:, None], (0.75, 0.42, 0.05), (0.06, 0.06, 0.065)))
    build.paint(tc, col, np.where(lens, 0.85, np.where(lampbox, 0.5, 0.2)))
    return [Piece("torch", "Outfit", "outfit", tc, 420, ["head"])]


def item_beret(c, worn):
    head_f = c.head_f

    def beret_f(p):
        return smax(beret_solid(p), -(head_f(p) + 0.001), 0.004)             # hollow where the head is (the hair under it is cut away: item_hair)
    br = sculpt("beret", beret_f, (-0.13, -0.13, 1.71), (0.15, 0.15, 1.86), 0.0018)
    build.paint(br, np.array((0.32, 0.035, 0.05)) * (0.9 + 0.2 * build.noise(sdf.points_of(br), 400)[:, None]), 0.12)
    return [Piece("beret", "Outfit", "outfit", br, 520, ["head"])]


def item_glasses(c, worn):
    f = c.f
    e = face.eye_centre(f)

    def glasses_f(p):                                                            # big round glasses
        q = mx(H(p))
        cc = np.array((e[0] + 0.001, e[1] - 0.0235, e[2] - 0.002))
        rim = np.hypot(np.hypot(q[..., 0] - cc[0], q[..., 2] - cc[2]) - 0.0205, q[..., 1] - cc[1]) - 0.0017
        bridge = cone(q, (0.0, cc[1], cc[2] + 0.006), (0.010, cc[1], cc[2] + 0.004), 0.0014)
        temple = cone(q, (cc[0] + 0.0205, cc[1], cc[2] + 0.004), (0.079 * f.skull_w, 0.012, cc[2] + 0.010), 0.0014)
        return np.minimum(np.minimum(rim, bridge), temple) * HEAD_S
    gl = sculpt("glasses", glasses_f, (-0.10, -0.125, 1.63), (0.10, 0.035, 1.71), 0.0006)
    build.paint(gl, 1.0, 0.7)
    return [Piece("glasses", "EyeDetails", "outfit", gl, 520, ["head"], texel=0.6)]


ITEM_MAKERS = {
    **{n: item_top(o) for n, (s, o) in ITEMS.items() if s == "top"},
    **{n: item_bottom(o) for n, (s, o) in ITEMS.items() if s == "bottom"},
    **{n: item_feet(o) for n, (s, o) in ITEMS.items() if s == "feet"},
    **{n: item_hair(n) for n, (s, o) in ITEMS.items() if s == "hair"},
    "beard": item_beard, "headphones": item_headphones, "torch": item_torch, "beret": item_beret, "glasses": item_glasses,
}


def beret_solid(p):
    """The designer's beret as a solid (the body's space): pulled down over one ear, its crown slouching to one side,
    big enough to sit over the hair."""
    q = (H(p) - np.array((0.016, 0.006, 0.100))) @ (sdf.rot_y(-0.30) @ sdf.rot_x(0.14))
    d = ell(q, (0.012, 0, 0.014), (0.112, 0.118, 0.030))                                # the crown
    d = smin(d, ell(q, (0.0, 0.002, -0.016), (0.091, 0.105, 0.030)), 0.016)             # down to the band round the head
    d = smin(d, cone(q, (0.012, 0, 0.041), (0.014, 0, 0.053), 0.0045, 0.0035), 0.004)   # the stalk
    return d * HEAD_S


def shell(surface, p, lift=0.0012, thick=0.0026):
    """A thin layer lying on a garment: between `lift` and `lift + thick` over its surface."""
    d = surface(p)
    return np.maximum(d - (lift + thick), -(d - lift) - 0.004)


def along_path(p, pts):
    """Distance to a polyline."""
    d = None
    for a, c in zip(pts, pts[1:]):
        x, _ = sdf.seg_dist(p, a, c)
        d = x if d is None else np.minimum(d, x)
    return d


def eyeball(name, c, seg, rings):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=face.EYE_R * HEAD_S)
    bmesh.ops.rotate(bm, cent=Vector(), matrix=sdf_rot_x90(), verts=bm.verts)     # the poles to the front and the back
    bmesh.ops.translate(bm, vec=Vector(c), verts=bm.verts)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    return o


def sdf_rot_x90():
    from mathutils import Matrix
    return Matrix.Rotation(math.pi / 2, 3, "X")


# --------------------------------------------------------------------------- painting

def paint_head(p, person):
    """The skin of the head as a multiplier of the player's skin tone: warm cheeks, nose and ears, the lips, shadows
    round the eyes, lash lines, eyebrows, stubble, freckles."""
    f = person.face
    q = mx(p)
    n = len(p)
    rgb = np.ones((n, 3)) * (0.965 + 0.035 * build.noise(p, 900)[:, None])
    near = lambda c, w: np.exp(-(np.linalg.norm(q - np.array(c), axis=1) / w) ** 2)
    warm = 0.9 * near((0.046, -0.068, -0.030), 0.028) + 0.8 * near((0, -0.118, -0.033), 0.016) + 0.8 * near((0.078, 0.014, -0.004), 0.028) + 0.4 * near((0, -0.086, -0.094), 0.02)
    rgb *= 1 - np.clip(warm, 0, 1)[:, None] * np.array([-0.02, 0.085, 0.10])
    lip = np.clip(1 - face.lips(q, f) / 0.0028, 0, 1) ** 0.7 * (p[:, 1] < -0.080)      # (the red of the lips, fading out past their edge)
    lip *= np.clip((0.0235 * f.mouth_w - q[:, 0]) / 0.007, 0, 1) ** 0.6                  # … and towards the corners (two red stripes ended bluntly)
    k = person.lip_colour
    rgb *= 1 - lip[:, None] * np.array([0.10 + 0.05 * k, 0.26 + 0.22 * k, 0.24 + 0.16 * k])
    e = face.eye_centre(f)
    socket = near((e[0], e[1] - 0.012, e[2] - 0.004), 0.021)
    rgb *= 1 - socket[:, None] * (0.12 + person.eye_shadow) * np.array([0.34, 0.40, 0.34])
    # the lash line: a dark rim just outside the almond, on the lids only (the almond is flat: measured by depth too, it
    # drew its outline again wherever the brow stood as far forward as the lids)
    slit = face._slit(q, f, flat=True)
    lids = (p[:, 1] < e[1] - 0.004) & (np.abs(q[:, 2] - e[2]) < 0.011) & (np.abs(q[:, 0] - e[0]) < 0.021) & (p[:, 1] > e[1] - 0.022)
    lash = np.clip(1 - np.abs(slit - 0.0008) / 0.0016, 0, 1) ** 0.6 * np.where(q[:, 2] > e[2] - 0.0017, 1.0 if f.female else 0.8, 0.35) * lids
    rgb *= 1 - lash[:, None]
    # the pockets in the corners of the almond: the pink caruncle at the inner one, a darker pink at the outer
    pocket = (slit < 0.0004) * (p[:, 1] > e[1] - 0.0080) * np.clip((np.abs(q[:, 0] - e[0]) - 0.0085) / 0.002, 0, 1)
    rgb *= 1 - pocket[:, None] * np.where(q[:, 0] < e[0], 1.0, 0.6)[:, None] * np.array([0.02, 0.30, 0.26])
    # eyebrows: an arc of hair on the brow ridge, thicker towards the nose
    t = np.clip((q[:, 0] - 0.008) / 0.040, 0, 1)
    arc = 0.0235 + 0.0050 * np.sin(np.pi * np.minimum(1.0, t * 1.25)) - 0.006 * t * t + (0.0025 if f.female else 0.0)
    half = (0.0066 - 0.0036 * t) * person.brow_thick
    # (no drawn hairs: the sculpt's vertices are 1.3 mm apart, and a finer stripe aliased into a checkerboard in the game —
    # a soft mass, denser at the lower edge and at the head of the brow, thinning out along its tail, reads as a brow)
    hairs = (0.82 + 0.18 * build.noise(p, 260, 5)) * (1.0 - 0.35 * t * t)
    edge = (q[:, 2] - arc) / half
    brow = np.clip(1.25 * (1 - np.abs(edge) ** 1.6) * (1.0 - 0.15 * np.clip(edge, 0, 1)), 0, 1) * hairs
    brow *= np.clip((q[:, 0] - 0.006) / 0.004, 0, 1) * np.clip((0.051 - q[:, 0]) / 0.006, 0, 1) * (p[:, 1] < -0.06)
    dark = np.array(person.brow_colour or person.hair_colour) * 0.55
    rgb = rgb * (1 - brow[:, None]) + np.clip(dark / np.array((0.8, 0.6, 0.5)), 0, 1)[None, :] * brow[:, None] * 0.32
    if person.stubble or person.beard:
        # the shadow of shaved hair (or of the beard's thin edge): where a beard would grow, fading out past its outline
        zone = np.maximum(face.beard_zone(q, f), face.moustache_zone(q, f))
        amount = np.clip((zone + 0.010) / 0.012, 0, 1) * (1 - lip) * (0.6 + 0.4 * build.speckle(p, 2200, 0.55))
        rgb *= 1 - amount[:, None] * max(person.stubble, 0.5 if person.beard else 0.0) * np.array([0.42, 0.38, 0.33])
    if person.freckles:
        zone = np.clip(near((0.030, -0.075, -0.018), 0.030) + near((0, -0.105, -0.015), 0.018), 0, 1)
        rgb *= 1 - (zone * build.speckle(p, 1500, 0.16, 3) * person.freckles)[:, None] * np.array([0.10, 0.22, 0.30])
    gloss = 0.36 + 0.16 * near((0, -0.105, -0.020), 0.03) + 0.10 * near((0, -0.085, 0.05), 0.04) + 0.10 * lip
    return rgb, gloss


def paint_hand(p, normals):
    W, D, N = J["wrist"], body.HAND_D, body.HAND_N
    along = (p - W) @ D
    back = np.clip(-(normals @ N), 0, 1)                         # the back of the hand faces away from the palm
    rgb = np.ones((len(p), 3)) * (0.965 + 0.035 * build.noise(p, 900)[:, None])
    knuckle = gauss(along - 0.094, 0.012) * back
    rgb *= 1 - knuckle[:, None] * np.array([0.0, 0.08, 0.09])
    palm = np.clip(normals @ N, 0, 1) * win(along, 0.0, 0.2, 0.02)
    rgb *= 1 - palm[:, None] * np.array([-0.02, 0.05, 0.06])       # the palm is pinker
    gloss = 0.34 + 0.08 * back
    # the joints' creases on the back of every finger, and a nail on its last bone
    for name, pts in clothes.hand_chains().items():
        for i in (1, 2):
            axis = (pts[i + 1] - pts[i - 1]) / np.linalg.norm(pts[i + 1] - pts[i - 1])
            crease = gauss((p - pts[i]) @ axis, 0.0018) * (np.linalg.norm(p - pts[i], axis=1) < 0.013) * back
            rgb *= 1 - 0.5 * crease[:, None] * np.array([0.10, 0.20, 0.22])
        a, b = pts[2], pts[3]
        axis = (b - a) / np.linalg.norm(b - a)
        t = (p - a) @ axis / np.linalg.norm(b - a)
        d, _ = sdf.seg_dist(p, a, b + axis * 0.004)
        up = np.cross(axis, np.cross(-N, axis))
        up /= np.linalg.norm(up)
        nail = (d < 0.012) * np.clip((normals @ up - 0.55) / 0.12, 0, 1) * win(t, 0.42, 1.25, 0.06)   # on the finger's back
        rgb = rgb * (1 - nail[:, None]) + nail[:, None] * np.array([1.03, 0.90, 0.88])
        gloss = gloss + 0.34 * nail
    return rgb, gloss


def paint_eye(p, person):
    """An eyeball looking forward (-y): the iris with its fibres and dark rim, the pupil, a white with red corners."""
    r = np.hypot(p[:, 0], p[:, 2])
    front = p[:, 1] < 0
    a = np.arctan2(p[:, 2], p[:, 0])
    iris_r, pupil_r = 0.0058, 0.0021
    white = np.array((0.86, 0.84, 0.81))[None, :] * (1 - 0.25 * np.clip((r - 0.008) / 0.004, 0, 1))[:, None] * np.array((1.0, 0.93, 0.92))[None, :] ** np.clip((r - 0.007) / 0.004, 0, 1)[:, None]
    fibres = 0.75 + 0.25 * np.sin(a * 36 + 3 * np.sin(a * 7))
    ring = np.clip((r - iris_r * 0.8) / (iris_r * 0.2), 0, 1)
    iris = np.array(person.iris)[None, :] * (fibres * (1.15 - 0.5 * ring) * (0.6 + 0.6 * np.clip((r - pupil_r) / (iris_r - pupil_r), 0, 1)))[:, None]
    rgb = np.where(((r < iris_r) & front)[:, None], iris, white)
    rgb = np.where(((r < pupil_r) & front)[:, None], 0.01, rgb)
    return rgb, np.where(front, 0.92, 0.6)


def paint_hair(p, person, shade):
    locks = face._strands(p)
    # the grain follows the locks only a little; lighter and darker locks come and go along their length (a lock-for-lock
    # stripe as dark as a groove read as the segments of a pumpkin from the crown down)
    streak = 0.86 + 0.13 * locks + 0.16 * (build.noise(p, 170, 2) - 0.5) + 0.10 * (build.noise(p * (4, 1, 1), 420) - 0.5)
    rgb = np.array(person.hair_colour)[None, :] * (streak * shade)[:, None]
    return rgb, 0.20 + 0.12 * locks


def paint_beard(p, person):
    """Short dense hairs: the hair's colour a shade darker, mottled finely, a little lighter and thinner where it fades
    into the cheek; matte."""
    q = mx(p)
    inside = np.maximum(face.beard_zone(q, person.face), face.moustache_zone(q, person.face))
    dense = np.clip(inside / 0.010, 0.25, 1.0)
    grain = 0.78 + 0.30 * build.speckle(p, 2600, 0.5, 5) + 0.12 * (build.noise(p, 700) - 0.5)
    rgb = np.array(person.hair_colour)[None, :] * (0.86 * grain * (0.75 + 0.25 * dense))[:, None]
    return rgb, 0.18 + 0.08 * build.noise(p, 900)


def paint_cloth(p, colour, gloss, weave=0.05, wear=None):
    """Cloth of one colour: mottled a little, darker in the weave's direction."""
    k = 1 - weave + 2 * weave * build.noise(p, 260)
    if wear is not None:
        k = k * (1 + wear)
    return np.array(colour)[None, :] * k[:, None], gloss


def paint_trousers(p, normals, colour):
    q = mx(p)
    H, Kn = J["hip"], J["knee"]
    front = np.clip(-normals[:, 1], 0, 1)
    wear = 0.22 * front * gauss(q[:, 2] - 0.72, 0.16) + 0.14 * np.clip(-normals[:, 1], 0, 1) * gauss(q[:, 2] - 0.5, 0.05)      # faded on the thighs and the knees
    seam = gauss(np.abs(normals[:, 0]) - 1.0, 0.18) * gauss(q[:, 1] - 0.012, 0.004) * (q[:, 2] < 0.98)                         # the side seam
    fly = gauss(q[:, 0] - 0.012, 0.0022) * front * win(q[:, 2], 0.90, 1.01, 0.01)
    pocket = gauss(np.hypot(q[:, 0] - 0.165, q[:, 2] - 1.02) - 0.085, 0.0022) * front * (q[:, 2] > 0.93) * (q[:, 0] < 0.165)
    stitch = np.clip(seam + fly + pocket, 0, 1)
    rgb, gloss = paint_cloth(p, colour, 0.14, 0.06, wear)
    rgb = rgb * (1 - 0.35 * stitch[:, None])
    band = q[:, 2] > 1.003
    rgb = np.where(band[:, None], rgb * 0.82, rgb)
    rgb *= (1 - 0.3 * gauss(q[:, 0], 0.0025) * win(q[:, 2], 0.80, 0.99, 0.01))[:, None]            # the seam between the legs
    return rgb, gloss


def paint_shoe(p, fit):
    """A shoe's colours by clothes.shoe's own measures: the sole (it lifts with the toe), the upper, the laces where
    the sculpt has them, the toe cap and the heel's counter a shade apart, a dark opening at the collar."""
    kind = fit["shoe"]
    sole_h = {"sneaker": 0.026, "boot": 0.030, "flat": 0.016}[kind]
    top_z = {"sneaker": 0.125, "boot": 0.19, "flat": 0.085}[kind]
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    t = np.clip((0.085 - y) / 0.282, 0, 1)
    spring = 0.011 * np.clip((t - 0.76) / 0.24, 0, 1) ** 2
    zz = z - sole_h - spring
    on_sole = zz < 0.002
    upper = np.array(fit["shoe_colour"])[None, :] * (0.92 + 0.16 * build.noise(p, 300))[:, None]
    sole = np.array(fit["sole"])[None, :] * (0.94 + 0.06 * build.noise(p, 500))[:, None]
    if kind == "sneaker":
        sole = sole * np.where(zz < -0.013, 0.72, 1.0)[:, None]                 # the outsole under the white midsole
    rgb = np.where(on_sole[:, None], sole, upper)
    if kind != "flat":
        lace = np.zeros(len(p), bool)
        for s in (0.15, 0.38, 0.62, 0.86):                                      # (where clothes.shoe raises them)
            cy, cz = -0.042 - 0.082 * s, 0.103 - 0.038 * s
            lace |= (np.abs(y - cy) < 0.0042) & (np.abs(x - 0.106) < 0.024) & (z > cz - 0.016)
        rgb = np.where(lace[:, None], np.clip(rgb * 0.3 + 0.6, 0, 1) if kind == "sneaker" else rgb * 0.45, rgb)
        cap = (t > 0.80) & ~on_sole
        rgb = np.where(cap[:, None], np.clip(rgb * (1.10 if kind == "sneaker" else 0.86), 0, 1), rgb)
        counter = (t < 0.17) & (zz > 0.004) & (zz < 0.060)
        rgb = np.where(counter[:, None], rgb * 0.82, rgb)
    rgb = rgb * (1 - 0.75 * np.clip((z - (top_z - 0.004)) / 0.004, 0, 1))[:, None]   # the opening (the leg goes into it)
    gloss = np.where(on_sole, 0.3, 0.5 if kind != "sneaker" else 0.28)
    return rgb, gloss


def paint_top(p, who, fit):
    """The department's garment: white (the game tints it), its seams, ribs and pocket a shade darker."""
    q = mx(p)
    k = np.ones(len(p))
    hem, rib = fit["top"].get("hem", 0.99), fit["top"].get("rib", 0.0)
    if rib:
        ribs = 0.86 + 0.08 * np.sin(np.arctan2(q[:, 1], q[:, 0]) * 150)
        k = np.where(q[:, 2] < hem + rib, ribs, k)
        W, E = J["wrist"], J["elbow"]
        axis = (W - E) / np.linalg.norm(W - E)
        cuff = ((q - W) @ axis > -rib) & (q[:, 0] > 0.3)
        k = np.where(cuff, 0.86 + 0.08 * np.sin((q @ np.cross(axis, (0, 1.0, 0))) * 900), k)
    if who == "backend":
        pocket = (np.abs(q[:, 2] - 1.07) < 0.062) & (q[:, 0] < 0.115 - 0.3 * np.abs(q[:, 2] - 1.07)) & (p[:, 1] < 0)
        edge = pocket & ~((np.abs(q[:, 2] - 1.07) < 0.057) & (q[:, 0] < 0.110 - 0.3 * np.abs(q[:, 2] - 1.07)))
        k = np.where(edge, 0.62, np.where(pocket, 0.96, k))
        seam = gauss(q[:, 0], 0.0016) * (p[:, 1] < 0) * (q[:, 2] > 1.14) * (q[:, 2] < 1.46)          # the zip's line
        k = k * (1 - 0.4 * seam)
    if who in ("qa", "scientist"):
        placket = (q[:, 0] < 0.014) & (p[:, 1] < 0) & (q[:, 2] < 1.46)
        k = np.where(placket & (q[:, 0] > 0.0115), 0.75, k)                                          # the placket's stitched edge
        button = (np.hypot(q[:, 0], (q[:, 2] - 1.06) % 0.085 - 0.0425) < 0.0055) & (p[:, 1] < 0) & (q[:, 2] < 1.44) & (q[:, 2] > 1.03)
        k = np.where(button, 0.55, k)
    if who == "designer":
        knit = 0.93 + 0.07 * np.sin(q[:, 2] * 700) * np.sin(np.arctan2(q[:, 1], q[:, 0] - 0.0) * 90)   # a chunky knit
        k = k * knit
    armhole = gauss(np.linalg.norm(q - np.array((J["shoulder"][0] - 0.012, 0.02, J["shoulder"][2] - 0.01)), axis=1) - 0.078, 0.0018)
    k = k * (1 - 0.25 * armhole)                                                                     # the sleeve's seam
    k = k * (0.97 + 0.06 * build.noise(p, 240))
    gloss = {"backend": 0.10, "designer": 0.08}.get(who, 0.2)
    return np.repeat(np.clip(k, 0, 1)[:, None], 3, 1), gloss


def paint_vest(p):
    q = mx(p)
    k = 0.97 + 0.06 * build.noise(p, 240)
    zipper = (q[:, 0] < 0.008) & (p[:, 1] < 0)
    k = np.where(zipper, 0.45, k)
    for z0, w, h in ((1.275, 0.042, 0.042), (1.095, 0.05, 0.05)):                                    # two chest pockets, two hip pockets
        dx, dz = np.abs(q[:, 0] - 0.088), np.abs(q[:, 2] - z0)
        box_ = (dx < w) & (dz < h) & (p[:, 1] < 0)
        edge = box_ & ~((dx < w - 0.004) & (dz < h - 0.004))
        flap = box_ & (q[:, 2] > z0 + h * 0.45)
        k = np.where(edge, 0.6, np.where(flap, 0.88, k))
    hems = gauss(np.abs(clothes_edge(q)), 0.004)
    return np.repeat(np.clip(k * (1 - 0.2 * hems), 0, 1)[:, None], 3, 1), 0.16


def clothes_edge(q):
    return q[:, 2] - 1.012


# --------------------------------------------------------------------------- assembling the game model

def assemble(pieces, stem, atlases=(("skin", 2048), ("outfit", 2048))):
    """Cuts every piece to its budget, unwraps and packs the atlases, bakes, skins and joins the parts. Returns the rig
    and the game's part objects."""
    t = time.time()
    for p in pieces:
        if p.lo is None:
            build.lowpoly(p)
            build.clean(p.lo)
            build.orient_like(p.lo, p.hi)
        if not p.own_uv:
            build.unwrap(p)
    print(f"LOWPOLY {sum(sdf.tri_count(p.lo) for p in pieces)} tris in {len(pieces)} pieces, {time.time() - t:.1f} s")
    for atlas, size in atlases:                                        # (the skin at 2048: the face is what a close-up looks at)
        group = [p for p in pieces if p.atlas == atlas and not p.own_uv]
        if not group:
            continue
        build.pack(group, size if not DRAFT else size // 2)
        t = time.time()
        build.bake(group, size if not DRAFT else size // 2, TEXTURES, f"human_{stem}_{atlas}", ao_samples=64 if not DRAFT else 16)   # (24 samples baked a grain of sand into every skin)
        print(f"BAKED {atlas}: {len(group)} pieces, {time.time() - t:.1f} s")
    rig = anim.skeleton()
    bones = anim.bone_segments()
    seg = {n: (np.array(a), np.array(b)) for n, (a, b) in bones.items()}
    parts = {}
    for p in pieces:
        for name in bones:
            p.lo.vertex_groups.new(name=name)
        pts = sdf.points_of(p.lo)
        build.assign(p.lo, hand_weights(pts, p.name[-1]) if p.name.startswith("hand.") else build.weights(pts, seg, p.bones, RADIUS))
        parts.setdefault(p.part if p.slot is None else f"{p.part}.{p.slot}", []).append(p.lo)     # (an item's: "<part>.<slot>", the wardrobe's handle)
    out = []
    errors = 0
    for part, objs in parts.items():
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        obj.name = obj.data.name = part
        obj.data.uv_layers.active.name = "UVMap"
        errors += build.audit(obj, open_ok=part.startswith("Hair"))
        obj.parent = rig
        mod = obj.modifiers.new("Rig", "ARMATURE")
        mod.object = rig
        out.append(obj)
    print(f"TOPO total: {sum(sdf.tri_count(o) for o in out)} tris, {errors} errors")
    return rig, out, errors


def hide_sculpts(pieces):
    for p in pieces:
        for h in (p.hi if isinstance(p.hi, (list, tuple)) else [p.hi]):
            if h is p.lo:
                continue                                                  # (hair cards: the game mesh is its own sculpt)
            h.hide_render = True
            h.hide_viewport = True


def preview_material(obj, who, tint):
    """Principled with the baked maps (what the game's Lit material does with them), tinted like PlayerLooks would."""
    atlas = "skin" if obj.name in ("AccentHead", "Sclera") else "outfit"
    mat = bpy.data.materials.new("preview_" + obj.name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")

    def tex(suffix, data):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(os.path.join(TEXTURES, f"human_{who}_{atlas}_{suffix}.png"), check_existing=True)
        if data:
            n.image.colorspace_settings.name = "Non-Color"
        return n
    base, nrm, mask = tex("base", False), tex("normal", True), tex("mask", True)
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    nt.links.new(base.outputs["Color"], mix.inputs[6])
    mix.inputs[7].default_value = (*tint, 1)
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(nrm.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    inv = nt.nodes.new("ShaderNodeInvert")
    nt.links.new(mask.outputs["Alpha"], inv.inputs["Color"])
    nt.links.new(inv.outputs["Color"], bsdf.inputs["Roughness"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    obj.color = (*tint, 1)


TINTS = {"backend": (0.22, 0.24, 0.30), "qa": (0.85, 0.87, 0.90), "sysadmin": (0.45, 0.43, 0.32), "designer": (0.62, 0.52, 0.46), "scientist": (0.8, 0.8, 0.78)}
SKIN = {"backend": (0.93, 0.76, 0.62), "qa": (0.96, 0.82, 0.70), "sysadmin": (0.78, 0.58, 0.44), "designer": (0.55, 0.38, 0.28), "scientist": (0.93, 0.76, 0.62)}
TEAM = {"backend": (0.95, 0.55, 0.2), "qa": (0.6, 0.9, 0.3), "sysadmin": (0.3, 0.7, 0.95), "designer": (0.9, 0.4, 0.8), "scientist": (0.95, 0.55, 0.2)}


def previews(rig, parts, acts, who, path):
    """Contact sheets: the lit game model (EEVEE, the baked maps), its face and hands, and every take."""
    s = bpy.context.scene
    for o in parts:
        tint = SKIN[who] if o.name == "AccentHead" else TINTS[who] if o.name.startswith("Body") else TEAM[who] if o.name.startswith("Accent") \
            else (0.06, 0.06, 0.07) if o.name.startswith("Eye") else (1, 1, 1)
        preview_material(o, who, tint)
    for name, loc, energy in (("key", (2.0, -3.0, 3.2), 230), ("fill", (-2.5, -2.0, 1.6), 60), ("rim", (0.5, 3.0, 2.6), 140)):
        light = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
        light.data.energy = energy
        light.data.size = 2.0
        light.location = loc
        light.rotation_euler = (Vector((0, 0, 1.2)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        s.collection.objects.link(light)
    base, ext = os.path.splitext(path)
    try:
        view.sheet(base + "_lit" + ext, parts, shots=view.FULL, engine="BLENDER_EEVEE")
        view.sheet(base + "_lit_face" + ext, parts, shots=view.FACE + view.BUST[:1], width=460, engine="BLENDER_EEVEE")
    except Exception as ex:                                                  # (no GPU in this session: the Workbench sheets remain)
        print("PREVIEW eevee failed:", ex)
    view.sheet(base + "_mesh" + ext, parts, shots=view.FULL, color="TEXTURE")
    view.sheet(base + "_face" + ext, parts, shots=view.FACE + view.HANDS, width=400, color="TEXTURE")
    tiles = []
    for a in acts:
        rig.animation_data.action = a
        n = int(a.frame_range[1])
        for share in (0.0, 0.25, 0.5):
            s.frame_set(1 + int((n - 1) * share))
            tmp = f"{base}_take_{a.name}_{int(share * 100)}{ext}"
            view.sheet(tmp, parts, shots=[("3/4", (0.75, -0.66, 0.15), (0, 0, 0.9), 2.0)], width=300, height=560, color="TEXTURE")
            tiles.append(tmp)
    rig.animation_data.action = None
    s.frame_set(1)
    rows = []
    for i in range(0, len(tiles), 6):
        imgs = [bpy.data.images.load(t) for t in tiles[i:i + 6]]
        px = [np.array(im.pixels[:], np.float32).reshape(im.size[1], im.size[0], 4) for im in imgs]
        rows.append(np.concatenate(px, 1))
        for im in imgs:
            bpy.data.images.remove(im)
    w = max(r.shape[1] for r in rows)
    sheet = np.concatenate([np.pad(r, ((0, 0), (0, w - r.shape[1]), (0, 0)), constant_values=0.2) for r in rows[::-1]], 0)
    out = bpy.data.images.new("_takes", sheet.shape[1], sheet.shape[0], alpha=False)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = base + "_takes" + ext
    out.file_format = "PNG"
    out.save()
    for t in tiles:
        os.remove(t)


def sidecar(path, **data):
    """What the game reads beside a file (AssetBuilder.SidecarOf): the slot and name of an item, a person's own items, the hair's colour."""
    import json
    with open(os.path.splitext(path)[0] + ".json", "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)


def export_item(rig, parts, who, item):
    """An item's file: the skeleton (no takes: it is worn on a person's) and its meshes, and its sidecar."""
    os.makedirs(OUT, exist_ok=True)
    for o in parts:
        o.data.materials.clear()
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for o in parts:
        o.select_set(True)
    path = os.path.join(OUT, f"human_{who}__{item}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, object_types={"ARMATURE", "MESH"}, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                             use_tspace=False, add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False)
    sidecar(path, who=who, item=item, slot=ITEMS[item][0])


def make_item(who, item):
    """Sculpts, bakes and exports one wardrobe item made for a person's build (its own maps: human_<who>__<item>_outfit_*)."""
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = anim.FPS
    pieces = figure(who, [item], body_too=False)
    size = 1024 if ITEMS[item][0] in ("top", "bottom") else 512
    rig, parts, errors = assemble(pieces, f"{who}__{item}", (("outfit", size),))
    hide_sculpts(pieces)
    export_item(rig, parts, who, item)
    print(f"ITEM {who} {item}: {time.time() - t0:.1f} s" + (f" WITH {errors} TOPOLOGY ERRORS" if errors else ""))
    return errors


def export(rig, parts, who):
    os.makedirs(OUT, exist_ok=True)
    for o in parts:
        o.data.materials.clear()
    if STATIC:
        bpy.ops.object.select_all(action="DESELECT")
        for o in parts:
            o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=STATIC, use_selection=True, axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                                 object_types={"MESH"}, use_mesh_modifiers=False, bake_anim=False, apply_scale_options="FBX_SCALE_ALL",
                                 mesh_smooth_type="FACE")
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    for o in parts:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"human_{who}.fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, object_types={"ARMATURE", "MESH"},
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False, add_leaf_bones=False,
                             use_armature_deform_only=True, bake_anim=True, bake_anim_use_all_actions=True,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    sidecar(os.path.join(OUT, f"human_{who}.fbx"), who=who, items={ITEMS[n][0]: n for n in DEFAULTS.get(who, [])},
            hair_colour=list(face.PEOPLE[who].hair_colour) if who in face.PEOPLE else [0.1, 0.08, 0.06])


if __name__ == "__main__" and ITEM_ARG:
    # wardrobe items for this person: item=all — every item that is not their own (theirs are in their model)
    names = [n for n in ITEMS if n not in DEFAULTS.get(WHO, [])] if ITEM_ARG == "all" else ITEM_ARG.split(",")
    bad = sum(make_item(WHO, n) for n in names)
    print(f"DONE {WHO} items {','.join(names)}" + (f" WITH {bad} TOPOLOGY ERRORS" if bad else ""))
elif __name__ == "__main__":
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = anim.FPS
    pieces = figure(WHO)
    print(f"SCULPTED {WHO} in {time.time() - t0:.1f} s")
    if PREVIEW:
        cols = {"AccentHead": SKIN[WHO], "BodyClothes": TINTS[WHO], "AccentBand": TEAM[WHO], "EyeDetails": (0.06, 0.06, 0.07), "Sclera": (0.9, 0.9, 0.88)}
        for p in pieces:
            p.hi.color = (*cols.get(p.part, (0.5, 0.5, 0.52)), 1)
        base, ext = os.path.splitext(PREVIEW)
        view.sheet(base + "_sculpt" + ext, [p.hi for p in pieces], shots=view.FULL)
        view.sheet(base + "_sculpt_face" + ext, [p.hi for p in pieces], shots=view.FACE + view.BUST[:1], width=460)
    rig, parts, errors = assemble(pieces, WHO)                     # (the person's maps: human_<who>_skin_*, human_<who>_outfit_*)
    hide_sculpts(pieces)
    acts = anim.animate(rig)
    if PREVIEW:
        previews(rig, parts, acts, WHO, PREVIEW)
    export(rig, parts, WHO)
    print(f"DONE {WHO} in {time.time() - t0:.1f} s" + (f" WITH {errors} TOPOLOGY ERRORS" if errors else ""))
