"""The vault's three lock consoles on the walls (prefab Props/VaultConsole: one of its three faces shows, by the panel's
game), modelled — they were raw cubes, the "round" dial two squares turned 45 degrees:

- vault_console_dial: a steel safe-door panel with a recessed face, a graduated ring, a fluted combination knob with
  a pointer and a T-handle beside it;
- vault_console_switches: an electrical box with a hazard stripe along its foot, four heavy toggle switches in their
  guards and a pilot lamp over each;
- vault_console_sequence: a white panel with a dark bezel and a 3 x 3 keypad of rounded lamp buttons.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/vault_console.py -- Assets/_Game/Art/Models

Each faces -Y (Unity +Z: out of the wall), its back on y = 0, centred on its own middle (x, z); parts Body (the casing:
the style's door colour), Accent (steel trim), Trim (dark rubber/plastic), Stripe (the hazard band), and lit parts
EyeA / EyeB / EyeC (the builder gives them the lock-lamp, keypad and bulb glows). Written to Props/.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshfix  # noqa: E402
import shapes as s  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "Assets", "_Game", "Art", "Models"))


def export(name):
    import bpy
    bpy.context.view_layer.update()
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    meshfix.fix_objects(objs, floor=False, name=name)       # (hung on a wall, centred: nothing rests on the floor)
    s.export_fbx(os.path.join(ROOT, "Props", name + ".fbx"), objs)
    print("exported", name)
    s.clear_scene()


def ring(r0, r1, depth, y, z=0.0, seg=40):
    """A flat ring facing -Y (its front at y)."""
    outer = s.rcyl(r1, depth, (0, y + depth / 2, z), axis="Y", seg=seg, r=0.002)
    inner = s.rcyl(r0, depth + 0.02, (0, y + depth / 2, z), axis="Y", seg=seg, r=0.0, rot=(0, math.pi / seg, 0))
    return s.boolean(outer, inner, "DIFFERENCE")


def dial():
    w, h, d = 0.5, 0.6, 0.08
    body = s.rbox((w, d, h), (0, -d / 2, 0), r=0.012, seg=2)
    s.inset_faces(body, s.facing((0, -1, 0), min_area=0.05), thickness=0.035, depth=-0.012)
    s.part("Body", [body])
    zc = -0.02
    s.part("Accent", [ring(0.15, 0.185, 0.008, -d - 0.004, zc),                          # the graduated ring's plate
                      s.rcyl(0.035, 0.03, (0.19, -d - 0.012, zc + 0.08), axis="Y", seg=14, r=0.004),
                      s.rcyl(0.035, 0.03, (0.19, -d - 0.012, zc - 0.08), axis="Y", seg=14, r=0.004)])
    knob = s.lathe([(0.0, 0.0), (0.1, 0.0), (0.1, 0.012), (0.092, 0.03), (0.08, 0.04), (0.0, 0.042)], seg=36, axis="Y",
                   loc=(0, -d - 0.006, zc))
    for v in knob.verts:                                                                 # flutes round its rim
        a = math.atan2(v.co.z - zc, v.co.x)
        rr = math.hypot(v.co.x, v.co.z - zc)
        if rr > 0.07:
            k = 1.0 - 0.06 * max(0.0, math.cos(a * 18))
            v.co.x *= k
            v.co.z = zc + (v.co.z - zc) * k
    s.part("Trim", [knob])
    ticks = []
    for k in range(24):                                                                  # ticks on the ring, every 15 degrees
        a = k * math.tau / 24
        long = k % 2 == 0
        t = s.rbox((0.006, 0.004, 0.026 if long else 0.014), (0, 0, 0), r=0.0, seg=1)
        s.place(t, rot=(0, -a, 0))
        rr = 0.168 if long else 0.172
        s.place(t, (math.sin(a) * rr, -d - 0.013, zc + math.cos(a) * rr))
        ticks.append(t)
    s.part("EyeB", ticks + [s.rbox((0.014, 0.006, 0.05), (0, -d - 0.016, zc + 0.12), r=0.003, seg=1)])  # the pointer above
    # the T-handle beside the dial, on its two bosses
    s.part("Stripe", [s.rcyl(0.014, 0.2, (0.19, -d - 0.04, zc), axis="Z", seg=12, r=0.004),
                      s.rcyl(0.012, 0.035, (0.19, -d - 0.025, zc + 0.08), axis="Y", seg=10, r=0.002),
                      s.rcyl(0.012, 0.035, (0.19, -d - 0.025, zc - 0.08), axis="Y", seg=10, r=0.002)])
    export("vault_console_dial")


def switches():
    w, h, d = 0.56, 0.5, 0.09
    body = s.rbox((w, d, h), (0, -d / 2, 0), r=0.014, seg=2)
    s.part("Body", [body])
    s.part("Stripe", [s.rbox((w - 0.01, 0.006, 0.05), (0, -d - 0.002, -h / 2 + 0.045), r=0.004, seg=1)])
    sockets, bats, lamps, guards = [], [], [], []
    for k in range(4):
        x = -0.195 + k * 0.13
        sockets.append(s.rbox((0.08, 0.02, 0.15), (x, -d - 0.008, -0.03), r=0.008, seg=2))
        for gx in (-0.032, 0.032):                                                       # the guard's two cheeks
            guards.append(s.rbox((0.008, 0.045, 0.1), (x + gx, -d - 0.03, -0.02), r=0.003, seg=1))
        bat = s.rcyl(0.011, 0.1, (0, 0, 0.05), axis="Z", seg=12, r=0.005, radius2=0.015)
        s.place(bat, rot=(math.radians(-28), 0, 0))
        s.place(bat, (x, -d - 0.02, -0.03))
        bats.append(bat)
        lamps.append(s.ball(0.022, (x, -d - 0.004, 0.13), seg=16, scale=(1.0, 0.7, 1.0)))   # a dome out of its bezel
        guards.append(ring(0.021, 0.03, 0.01, -d - 0.006, 0.13, seg=24))
    s.part("Accent", sockets + guards)
    s.part("Trim", bats)
    s.part("EyeB", lamps)
    export("vault_console_switches")


def sequence():
    w, h, d = 0.46, 0.56, 0.07
    s.part("Body", [s.rbox((w, d, h), (0, -d / 2, 0), r=0.014, seg=2)])
    s.part("Trim", [s.rbox((0.37, 0.012, 0.37), (0, -d - 0.004, -0.02), r=0.012, seg=2)])
    groups = {"EyeA": [], "EyeB": [], "EyeC": []}
    for row in range(3):
        for col in range(3):
            b = s.rbox((0.082, 0.024, 0.082), ((col - 1) * 0.11, -d - 0.016, -0.02 + (1 - row) * 0.11), r=0.016, seg=3)
            groups[("EyeA", "EyeB", "EyeC")[(row + col) % 3]].append(b)
    for role, bms in groups.items():
        s.part(role, bms)
    export("vault_console_sequence")


def panel():
    """vault_panel: the board over the vault's door (VaultPanel): a steel plate with bevelled edges and four screws, three
    round lamps in chrome bezels — one per console, lit as each is solved (separate parts EyeLamp0..2, coloured in play)."""
    s.part("Body", [s.rbox((0.74, 0.02, 0.2), (0, -0.01, 0), r=0.006, seg=2)]
           + [s.rcyl(0.008, 0.006, (x, -0.022, z), axis="Y", seg=8, r=0.002) for x in (-0.34, 0.34) for z in (-0.07, 0.07)])
    s.part("Accent", [s.place(ring(0.045, 0.058, 0.012, -0.032, 0.0, seg=28), ((k - 1) * 0.22, 0, 0)) for k in range(3)])
    for k in range(3):
        s.part(f"EyeLamp{k}", [s.ball(0.046, ((k - 1) * 0.22, -0.026, 0.0), seg=16, scale=(1.0, 0.55, 1.0))])
    export("vault_panel")


if __name__ == "__main__":
    s.clear_scene()
    dial()
    switches()
    sequence()
    panel()
    print("DONE")
