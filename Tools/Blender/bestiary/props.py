"""Furniture and level pieces designed against the furniture vocabulary (furniture.Furniture, mixed into lang.Lang).

Contract (the game swaps meshes by name; colliders, lights and placement stay the original's, generate_models.py):
  * exactly the original's roles (Body, Accent, Eye, AccentConsole for the terminal);
  * the same footprint and height (±3 cm; the build checks ±6 cm): rooms are laid out with the original sizes, the
    colliders are the original meshes, lamps keep their bulb where the original's light is;
  * stands on the floor at z = 0 (ceiling lamp hangs from z = 0 down, window and exit sign are centred), -y = the
    side that faces the room / the user (screens, doors, seat fronts); things against a wall have their back at +y.
"""
import math

import shapes as s
from designs import Parts


def desk(L):
    """Office desk 1.6 × 0.8, top at 0.765, a modesty panel at the back, a monitor (Eye) on a stand."""
    P = Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", L.four_legs(.72, .8, .4, .72, .05))
    P.add("Accent", L.slab((1.47, .025, .38), (0, .33, .5)))                        # modesty panel into the legs
    acc, glow = L.screen((.56, .34), (0, .17, .98), "-Y")
    P.add("Accent", acc + L.slab((.06, .05, .2), (0, .2, .84)) + L.slab((.24, .16, .02), (0, .2, .775)))
    P.add("Eye", glow)
    P.add("Accent", L.slab((.44, .15, .02), (0, -.12, .775)))                       # keyboard
    return P


def terminal(L):
    """Security console on the desk footprint: three screens, a keyboard (AccentConsole), a computer under the top."""
    P = Parts()
    P.add("Body", L.slab((1.6, .8, .05), (0, 0, .74)))
    P.add("Accent", L.four_legs(.72, .8, .4, .72, .05) + L.slab((1.47, .025, .38), (0, .33, .5)))
    P.add("Accent", L.carcass((.22, .45, .42), (.45, .05, 0), kick=.02))
    P.add("AccentConsole", L.slab((.52, .17, .025), (0, -.1, .778)))
    for x, y, w in ((-.32, .17, .29), (0, .19, .3), (.32, .17, .29)):
        acc, glow = L.screen((w, .23), (x, y, 1.0), "-Y")
        P.add("Eye", glow)
        P.add("AccentConsole", L.slab((.05, .05, .14), (x, y + .03, .83)) + acc)
    return P


def chair(L):
    """Office chair: seat, back, a gas column on a five-star base."""
    P = Parts()
    P.add("Body", L.cushion((.5, .48, .08), (0, 0, .48)) + L.cushion((.46, .08, .52), (0, .23, .8), rot=(math.radians(-8), 0, 0)))
    P.add("Accent", L.rod((0, .2, .5), (0, .24, .62), .02))
    P.add("Accent", L.rod((0, 0, .06), (0, 0, .46), .03))
    for k in range(5):
        a = k / 5 * math.tau + math.pi / 2
        P.add("Accent", L.rod((0, 0, .07), (math.cos(a) * .27, math.sin(a) * .27, .045), .022))
        P.add("Accent", L.ball(.025, (math.cos(a) * .27, math.sin(a) * .27, .025)))
    return P


def server_rack(L):
    """Server cabinet 0.7 × 1.0 × 2.0: a frame, a perforated door, rows of blinking units (Eye)."""
    P = Parts()
    P.add("Body", L.carcass((.7, 1.0, 2.0), (0, 0, 0), kick=.05))
    for i in range(11):
        P.add("Eye", L.slab((.56, .02, .045), (0, -.505, .22 + i * .16)))
    P.add("Body", L.slab((.66, .02, 1.88), (0, -.495, 1.0)))
    return P


def cabinet(L):
    """Filing cabinet 0.9 × 0.5 × 1.4 with three drawers."""
    P = Parts()
    P.add("Body", L.carcass((.9, .5, 1.4), (0, 0, 0)))
    for i in range(3):
        z = .3 + i * .42
        P.add("Body", L.door((.84, .38), (0, -.255, z)))
        P.add("Accent", L.handle((0, -.27, z + .1), .26, vertical=False))
    return P


def meeting_table(L):
    """Meeting table 3.2 × 1.3, two pedestal legs."""
    P = Parts()
    P.add("Body", L.slab((3.2, 1.3, .06), (0, 0, .75)))
    for x in (-1.1, 1.1):
        P.add("Accent", L.slab((.26, .26, .72), (x, 0, .36)) + L.slab((.5, .6, .03), (x, 0, .015)))
    return P


def plant(L):
    """Office plant 1.4 m: a pot and a bush."""
    P = Parts()
    P.add("Accent", L.pot(.21, .45, (0, 0, 0)))
    P.add("Body", L.rod((0, 0, .4), (0, 0, .75), .03))                              # stem into the bush
    P.add("Body", L.rod((0, 0, .75), (.04, .02, 1.18), .025))                         # stem up into the top bush
    P.add("Body", L.foliage((0, 0, .78), .3, n=6) + L.foliage((.05, .03, 1.24), .2, n=3, seed=4))
    return P


def kitchen_counter(L):
    """Kitchen counter 2.4 × 0.7 × 0.93 with cupboard doors and a microwave (Eye) on top."""
    P = Parts()
    P.add("Body", L.carcass((2.4, .65, .88), (0, 0, 0)))
    for i in range(4):
        x = -.9 + i * .6
        P.add("Body", L.door((.58, .7), (x, -.33, .46)))
        P.add("Accent", L.handle((x + .22, -.345, .7), .1))
    P.add("Accent", L.slab((2.44, .7, .05), (0, 0, .905)))
    P.add("Eye", L.slab((.5, .42, .3), (.7, .02, 1.08)))
    return P


def shelf(L):
    """Storage rack 2.0 × 0.6 × 2.0: uprights, four shelves, boxes (Accent)."""
    P = Parts()
    for x in (-.97, .97):
        for y in (-.27, .27):
            P.add("Body", L.leg((x, y, 2.0), 2.0, .05))
    for h in (.06, .45, 1.05, 1.65):
        P.add("Body", L.slab((2.0, .6, .04), (0, 0, h)))
    P.add("Accent", L.slab((.45, .4, .3), (-.6, 0, .615)) + L.slab((.35, .35, .25), (.5, .05, .59))
          + L.slab((.5, .45, .35), (.3, 0, 1.24)) + L.slab((.4, .4, .3), (-.55, 0, 1.815)))       # 5 mm into the shelves
    return P


def boxes(L):
    """A stack of cardboard boxes."""
    P = Parts()
    P.add("Accent", L.slab((.6, .6, .5), (0, 0, .25)) + L.slab((.5, .5, .4), (.05, -.05, .7))
          + L.slab((.45, .45, .4), (.55, .1, .2)))
    return P


def water_cooler(L):
    """Water cooler: a cabinet with taps and a big bottle (Eye)."""
    P = Parts()
    P.add("Body", L.carcass((.34, .34, 1.0), (0, 0, 0), kick=.03))
    P.add("Eye", L.lathe([(0, 0), (.13, 0), (.15, .05), (.15, .35), (.1, .43), (.05, .45), (0, .45)], loc=(0, 0, .99)))
    return P


def whiteboard(L):
    """Whiteboard 1.8 × 1.0 on a stand with two legs."""
    P = Parts()
    P.add("Body", L.slab((1.8, .04, 1.0), (0, 0, 1.5)))
    P.add("Accent", L.slab((1.86, .06, .05), (0, 0, 1.0)) + L.slab((1.86, .06, .05), (0, 0, 2.0)))
    for x in (-.85, .85):
        P.add("Accent", L.leg((x, 0, 2.0), 2.0, .05) + L.slab((.07, .08, .04), (x, 0, .02)))
    return P


def sofa(L):
    """Sofa 2.2 × 0.86 × 0.9: a base, two seat cushions, a back, two arms."""
    P = Parts()
    P.add("Body", L.cushion((1.8, .7, .2), (0, -.03, .22)))
    P.add("Body", L.cushion((.88, .68, .16), (-.45, -.05, .38)) + L.cushion((.88, .68, .16), (.45, -.05, .38)))
    P.add("Body", L.cushion((1.8, .22, .5), (0, .3, .62), rot=(math.radians(-6), 0, 0)))
    for x in (-1.0, 1.0):
        P.add("Accent", L.cushion((.2, .84, .62), (x, 0, .33)))
    return P


def toilet_stall(L):
    """Toilet cubicle 1.0 × 1.5 × 2.0: raised walls on feet, the bowl (Accent) inside."""
    P = Parts()
    for x in (-.5, .5):
        P.add("Body", L.slab((.04, 1.5, 1.88), (x, 0, 1.06)))
    P.add("Body", L.slab((1.04, .04, 1.88), (0, .75, 1.06)))
    for x in (-.5, .5):
        for y in (-.7, 0.0):
            P.add("Body", L.slab((.05, .05, .14), (x, y, .07)))
    P.add("Accent", L.lathe([(0, 0), (.15, 0), (.2, .3), (.22, .4), (0, .42)], loc=(0, .4, 0)))
    P.add("Accent", L.slab((.36, .14, .36), (0, .66, .55)))
    return P


def partition(L):
    """Privacy screen 1.6 × 0.06 × 1.25: fabric panel in a frame."""
    P = Parts()
    P.add("Body", L.cushion((1.52, .035, 1.16), (0, 0, .64)))
    P.add("Accent", L.slab((1.6, .055, .04), (0, 0, 1.24)) + L.slab((.04, .055, 1.22), (-.79, 0, .62))
          + L.slab((.04, .055, 1.22), (.79, 0, .62)) + L.slab((1.6, .055, .04), (0, 0, .04)))
    return P


def bookshelf(L):
    """Bookcase 1.0 × 0.35 × 2.0 with rows of folders."""
    P = Parts()
    for x in (-.48, .48):
        P.add("Body", L.slab((.04, .35, 2.0), (x, 0, 1.0)))
    for h in (.03, .5, 1.0, 1.5, 1.98):
        P.add("Body", L.slab((1.0, .35, .03), (0, 0, h)))
    P.add("Body", L.slab((.96, .02, 1.96), (0, .165, 1.0)))
    for i, h in enumerate((.045, .515, 1.015, 1.515)):
        x = -.43
        for k in range(6 + i % 2):
            w = .07 + .03 * ((k + i) % 3)
            hh = .32 - .04 * ((k * 7 + i) % 3)
            P.add("Accent", L.slab((w, .26, hh), (x + w / 2, .02, h + hh / 2)))
            x += w + .012
    return P


def printer(L):
    """Office copier 0.6 × 0.8 × 1.07 with a paper tray and a glowing panel."""
    P = Parts()
    P.add("Body", L.carcass((.6, .6, .95), (0, 0, 0), kick=.04) + L.slab((.56, .52, .12), (0, .02, 1.0)))
    P.add("Accent", L.slab((.36, .3, .03), (-.1, -.38, .82), rot=(math.radians(8), 0, 0)) + L.door((.5, .3), (0, -.31, .3)))
    P.add("Eye", L.slab((.16, .03, .08), (.18, -.29, .97)))
    return P


def fridge(L):
    """Fridge 0.7 × 0.75 × 1.8, two doors with handles."""
    P = Parts()
    P.add("Body", L.carcass((.7, .7, 1.8), (0, 0, 0), kick=.04))
    P.add("Body", L.door((.68, .5), (0, -.355, 1.5)) + L.door((.68, 1.2), (0, -.355, .64)))
    P.add("Accent", L.handle((.28, -.37, 1.4), .4) + L.handle((.28, -.37, .95), .3))
    return P


def coffee_machine(L):
    """Coffee machine on a counter, 0.35 × 0.44 × 0.5."""
    P = Parts()
    P.add("Body", L.slab((.34, .38, .5), (0, 0, .25)))
    P.add("Accent", L.slab((.22, .15, .04), (0, -.17, .06)) + L.cyl(.035, .08, (0, -.15, .16)) + L.slab((.26, .1, .06), (0, -.15, .23)))
    P.add("Eye", L.slab((.13, .02, .06), (0, -.2, .4)))
    return P


def round_table(L):
    """Round table r 0.5 on a pedestal."""
    P = Parts()
    P.add("Body", L.cyl(.5, .04, (0, 0, .74)))
    P.add("Accent", L.cyl(.045, .72, (0, 0, .36)) + L.cyl(.27, .03, (0, 0, .015)))
    return P


def armchair(L):
    """Armchair 0.88 × 0.76 × 0.86."""
    P = Parts()
    P.add("Body", L.cushion((.6, .66, .4), (0, -.02, .22)) + L.cushion((.6, .62, .14), (0, -.04, .46))
          + L.cushion((.6, .2, .46), (0, .28, .64), rot=(math.radians(-8), 0, 0)))
    for x in (-.37, .37):
        P.add("Accent", L.cushion((.14, .74, .6), (x, 0, .3)))
    return P


def coffee_table(L):
    """Coffee table 1.0 × 0.55 × 0.42 with a lower shelf."""
    P = Parts()
    P.add("Body", L.slab((1.0, .55, .04), (0, 0, .4)))
    P.add("Accent", L.four_legs(.38, .5, .275, .38, .04, inset=.04) + L.slab((.9, .45, .02), (0, 0, .12)))
    return P


def tv_stand(L):
    """Low cabinet 1.4 × 0.4 with a big screen."""
    P = Parts()
    P.add("Body", L.carcass((1.4, .4, .5), (0, 0, 0), kick=.04))
    acc, glow = L.screen((1.36, .8), (0, .06, 1.05), "-Y")
    P.add("Accent", acc + L.slab((.3, .2, .04), (0, .08, .52)) + L.slab((.06, .05, .15), (0, .09, .6)))
    P.add("Eye", glow)
    return P


def reception_desk(L):
    """Front counter 2.4 × 0.8: a high front panel to the visitor (-y), a work top behind, a monitor."""
    P = Parts()
    P.add("Body", L.slab((2.4, .08, 1.1), (0, -.36, .55)) + L.slab((.08, .76, 1.1), (-1.16, 0, .55))
          + L.slab((.08, .76, 1.1), (1.16, 0, .55)))
    P.add("Accent", L.slab((2.5, .3, .04), (0, -.33, 1.12)) + L.slab((2.3, .7, .04), (0, .02, .74)))
    acc, glow = L.screen((.5, .32), (.4, .15, .96), "-Y")
    P.add("Eye", glow + acc + L.slab((.06, .05, .2), (.4, .19, .83)))
    return P


def trash_bin(L):
    """Bin, r 0.19, 0.45 tall, with a rim."""
    P = Parts()
    P.add("Body", L.lathe([(0, 0), (.16, 0), (.19, .42), (.17, .42), (.15, .03), (0, .03)]))
    P.add("Accent", L.lathe([(.165, 0), (.2, 0), (.2, .03), (.165, .03)], loc=(0, 0, .42)))
    return P


def coat_rack(L):
    """Coat stand 1.8 m with hooks and two coats (Body)."""
    P = Parts()
    P.add("Accent", L.rod((0, 0, .02), (0, 0, 1.78), .022, kind="tube") + L.cyl(.24, .03, (0, 0, .015)))
    for a in (0.0, 2.1, 4.2):
        P.add("Accent", L.rod((0, 0, 1.66), (math.cos(a) * .2, math.sin(a) * .2, 1.74), .012))
    P.add("Body", L.cushion((.34, .12, .6), (.12, 0, 1.36)) + L.cushion((.3, .12, .55), (-.08, .1, 1.38), rot=(0, 0, 2.1)))
    return P


def lockers(L):
    """Three staff lockers 0.9 × 0.5 × 1.9 with vents and handles."""
    P = Parts()
    P.add("Body", L.carcass((.9, .5, 1.9), (0, 0, 0), kick=.06))
    for x in (-.3, 0.0, .3):
        P.add("Body", L.door((.29, 1.8), (x, -.255, .98)))
        P.add("Accent", L.handle((x + .1, -.27, 1.0), .12))
        for k in range(3):
            P.add("Accent", L.slab((.18, .015, .02), (x, -.27, 1.75 - k * .045)))
    return P


def pallet(L):
    """Warehouse pallet 1.2 × 1.0 with a stack of boxes (Body)."""
    P = Parts()
    P.add("Accent", L.slab((1.2, 1.0, .05), (0, 0, .12)))                           # deck, 5 mm into the runners
    for y in (-.45, 0, .45):
        P.add("Accent", L.slab((1.2, .1, .1), (0, y, .05)))
    P.add("Body", sum([L.slab((.55, .45, .4), (x, y, .34)) for x in (-.3, .3) for y in (-.24, .24)], [])
          + sum([L.slab((.53, .43, .4), (-.3, y, .735)) for y in (-.24, .24)], []))       # the upper layer sits into the lower
    return P


def wash_basin(L):
    """Restroom counter 1.6 × 0.5 with two sinks and a mirror (Eye) on the wall behind (+y)."""
    P = Parts()
    P.add("Body", L.slab((1.6, .5, .06), (0, 0, .85)) + L.carcass((1.6, .45, .82), (0, .02, 0)) + L.slab((1.6, .03, .4), (0, .24, 1.06)))
    for x in (-.4, .4):
        P.add("Accent", L.lathe([(0, 0), (.12, 0), (.17, .07), (.17, .09), (0, .09)], loc=(x, -.02, .82)))
        P.add("Accent", L.rod((x, .17, .88), (x, .17, 1.0), .02) + L.rod((x, .17, 1.0), (x, .08, .99), .015))
    P.add("Eye", L.slab((1.5, .02, .8), (0, .235, 1.55)))
    return P


def vending_machine(L):
    """Vending machine 0.9 × 0.82 × 1.9 with a glowing window (Eye), a keypad and a flap."""
    P = Parts()
    P.add("Body", L.carcass((.9, .8, 1.9), (0, 0, 0), kick=.05))
    P.add("Accent", L.slab((.6, .03, .15), (-.1, -.41, .25)) + L.slab((.14, .03, .5), (.33, -.41, 1.2)))
    P.add("Eye", L.slab((.6, .02, 1.1), (-.1, -.405, 1.15)))
    return P


def ping_pong_table(L):
    """Table-tennis table 2.74 × 1.72, a net (Eye stripe)."""
    P = Parts()
    P.add("Body", L.slab((2.74, 1.52, .04), (0, 0, .74)))
    P.add("Accent", L.four_legs(.72, 1.25, .7, .72, .06, inset=.25))
    P.add("Eye", L.slab((.02, 1.72, .15), (0, 0, .83)))                              # net, its foot inside the top
    for y in (-.81, .81):
        P.add("Eye", L.slab((.04, .08, .2), (0, y, .8)))                               # net posts clamped to the edge
    return P


def bean_bag(L):
    """Bean bag ~0.9 across."""
    P = Parts()
    P.add("Body", [s.loft([(0, .42, .42, 2.0), (.22, .45, .45, 2.0), (.5, .36, .36, 2.0), (.69, .2, .2, 2.0)],
                          seg=24, round_caps=.08)])
    return P


def arcade_machine(L):
    """Arcade cabinet 0.72 × 0.95 × 1.81: a cabinet with a slanted screen (Eye), a control panel."""
    P = Parts()
    P.add("Body", L.carcass((.7, .8, 1.8), (0, .02, 0), kick=.05))
    P.add("Accent", L.slab((.72, .4, .06), (0, -.4, .95), rot=(math.radians(-12), 0, 0)) + L.slab((.72, .25, .2), (0, -.1, 1.7)))
    P.add("Eye", L.slab((.56, .03, .44), (0, -.39, 1.3), rot=(math.radians(-15), 0, 0)))
    return P


def safe(L):
    """Safe 0.8 × 0.76 × 1.0: a thick door, a dial and a handle (Accent), a status light (Eye)."""
    P = Parts()
    P.add("Body", L.carcass((.8, .7, 1.0), (0, 0, 0), kick=.04))
    P.add("Body", L.door((.66, .82), (0, -.36, .52)))
    P.add("Accent", L.cyl(.08, .04, (0, -.39, .62), axis="Y") + L.rod((-.12, -.384, .4), (.12, -.384, .4), .02))
    P.add("Eye", L.slab((.05, .02, .05), (.25, -.385, .88)))
    return P


def desk_lamp(L):
    """Desk lamp 0.38 m: a base, a two-part arm, a shade over the bulb (Eye) in front."""
    P = Parts()
    P.add("Body", L.cyl(.075, .025, (0, 0, .0125)) + L.rod((0, 0, .02), (0, .03, .22), .012) + L.rod((0, .03, .22), (0, -.12, .32), .012))
    P.add("Accent", L.shade(.075, .03, .1, (0, -.16, .26)))
    P.add("Eye", [s.ball(.03, (0, -.16, .27), seg=10)])
    return P


def floor_lamp(L):
    """Floor lamp 1.65 m: a base, a pole, a shade (Accent) around the bulb (Eye)."""
    P = Parts()
    P.add("Body", L.cyl(.18, .03, (0, 0, .015)) + L.rod((0, 0, .03), (0, 0, 1.4), .015))
    P.add("Accent", L.shade(.2, .13, .26, (0, 0, 1.38)))
    P.add("Eye", [s.ball(.05, (0, 0, 1.45), seg=12)])
    return P


# --------------------------------------------------------------------------- level pieces

def ceiling_lamp(L):
    """Ceiling light 1.24 × 0.66, hanging from z = 0 down to −0.08: a housing and a diffuser (Eye)."""
    P = Parts()
    P.add("Body", L.slab((1.24, .66, .05), (0, 0, -.025)))
    P.add("Accent", L.slab((1.2, .04, .04), (0, -.31, -.06)) + L.slab((1.2, .04, .04), (0, .31, -.06)))
    P.add("Eye", L.slab((1.12, .56, .03), (0, 0, -.06)))
    return P


def exit_sign(L):
    """Exit sign above a door, 0.38 × 0.3 × 0.16: it sticks out of both faces of a 0.2 m wall, glowing faces on both
    sides (Eye), a running figure and an arrow (Accent) on them."""
    P = Parts()
    P.add("Body", L.slab((.38, .26, .16), (0, 0, 0)))
    P.add("Eye", L.slab((.34, .3, .12), (0, 0, 0)))
    P.add("Accent", L.slab((.05, .31, .07), (-.1, 0, 0)) + L.slab((.12, .31, .02), (.05, 0, 0)))
    return P


def hatch(L):
    """Manhole hatch r 0.9: a ring frame on the floor, the lid with grips, a glow ring (Eye)."""
    P = Parts()
    P.add("Body", L.cyl(.75, .06, (0, 0, .03)))
    for y in (-.25, 0, .25):
        P.add("Accent", L.slab((1.2, .08, .02), (0, y, .07)))
    P.add("Eye", L.cyl(.9, .02, (0, 0, .01)))
    return P


# window: the opening and the wall thickness are geometry of the building; it keeps the shared model (no design here)

PROPS = {
    "desk": desk, "terminal": terminal, "chair": chair, "server_rack": server_rack, "cabinet": cabinet,
    "meeting_table": meeting_table, "plant": plant, "kitchen_counter": kitchen_counter, "shelf": shelf, "boxes": boxes,
    "water_cooler": water_cooler, "whiteboard": whiteboard, "sofa": sofa, "toilet_stall": toilet_stall,
    "partition": partition, "bookshelf": bookshelf, "printer": printer, "fridge": fridge, "coffee_machine": coffee_machine,
    "round_table": round_table, "armchair": armchair, "coffee_table": coffee_table, "tv_stand": tv_stand,
    "reception_desk": reception_desk, "trash_bin": trash_bin, "coat_rack": coat_rack, "lockers": lockers,
    "pallet": pallet, "wash_basin": wash_basin, "vending_machine": vending_machine, "ping_pong_table": ping_pong_table,
    "bean_bag": bean_bag, "arcade_machine": arcade_machine, "safe": safe, "desk_lamp": desk_lamp,
    "floor_lamp": floor_lamp, "ceiling_lamp": ceiling_lamp, "exit_sign": exit_sign, "hatch": hatch,
}
