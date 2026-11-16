"""The security terminals (SecurityTerminal): the robots' control console (hacking robots from afar, the cameras) and the
storage door's access station — two looks that must never be mistaken for each other.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/terminals.py -- Assets/_Game/Art/Models
      (writes Props/terminal_console.fbx and Props/terminal_access.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z: the operator's side), z up, origin on the floor
in the middle of the footprint (a desk's: 1.6 x 0.8 — both replace a desk of the plan). Parts: Body (the furniture),
Accent (keys, stands, trims), Eye (every screen and lamp in ONE part: SecurityTerminal tints it by the terminal's kind).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rooms2_special as rs  # noqa: E402  (export(), torus())
import shapes as s  # noqa: E402

DESK_TOP = 0.76


def _monitor(cx, cy, yaw, body, accent, eye, w=0.54, h=0.33):
    """A flat monitor on a stand, its screen to -Y turned by yaw (degrees, about z) round its own stand."""
    a = math.radians(yaw)
    fx, fy = math.sin(a), -math.cos(a)              # the screen's facing in plan
    z = DESK_TOP + 0.13 + h / 2
    rot = (math.radians(-6), 0, a)                  # tipped back a little
    accent.append(s.rbox((0.22, 0.16, 0.012), (cx, cy, DESK_TOP + 0.006), r=0.006, seg=1, rot=(0, 0, a)))   # foot
    accent.append(s.rcyl(0.018, 0.13, (cx, cy, DESK_TOP + 0.075), seg=10, r=0.004))                           # neck
    body.append(s.rbox((w, 0.035, h), (cx, cy, z), r=0.008, seg=2, rot=rot))                                    # the case
    # the screen: 4 mm proud of the case's front, inside its bezel
    eye.append(s.rbox((w - 0.04, 0.004, h - 0.04), (cx + fx * 0.0195, cy + fy * 0.0195, z), r=0.002, seg=1, rot=rot))


def model_terminal_console():
    """The robots' control console: a steel desk on two pedestals, three monitors in an arc at its back, a keyboard, a
    console panel of chunky buttons and a joystick to the right, a cable trunk at the back; screens in one Eye part."""
    body, accent, eye = [], [], []
    body.append(s.rbox((1.6, 0.8, 0.04), (0, 0, DESK_TOP - 0.02), r=0.01, seg=2))                  # top
    for x in (-0.58, 0.58):
        body.append(s.rbox((0.42, 0.72, DESK_TOP - 0.06), (x, 0.02, (DESK_TOP - 0.04) / 2), r=0.012, seg=2))   # pedestals
        accent.append(s.rbox((0.36, 0.01, 0.012), (x, -0.345, 0.5), r=0.003, seg=1))                     # drawer pulls
        accent.append(s.rbox((0.36, 0.01, 0.012), (x, -0.345, 0.24), r=0.003, seg=1))
    body.append(s.rbox((0.74, 0.02, 0.4), (0, 0.36, 0.42), r=0.004, seg=1))                         # modesty panel
    for x, y, yaw in ((-0.5, 0.18, -22), (0.0, 0.24, 0), (0.5, 0.18, 22)):
        _monitor(x, y, yaw, body, accent, eye)
    accent.append(s.rbox((0.46, 0.15, 0.022), (-0.08, -0.2, DESK_TOP + 0.011), r=0.006, seg=2))     # keyboard
    for k in range(10):
        for j in range(3):
            accent.append(s.rbox((0.032, 0.03, 0.008), (-0.27 + k * 0.042, -0.24 + j * 0.04, DESK_TOP + 0.025), r=0.002, seg=1))
    # the console panel: a sloped box with buttons and a joystick
    body.append(s.rbox((0.3, 0.22, 0.06), (0.5, -0.2, DESK_TOP + 0.03), r=0.01, seg=2, rot=(math.radians(12), 0, 0)))
    for k in range(3):
        for j in range(2):
            eye.append(s.rcyl(0.018, 0.012, (0.42 + k * 0.06, -0.24 + j * 0.06, DESK_TOP + 0.066 + j * 0.012), seg=10, r=0.003))
    accent.append(s.rcyl(0.025, 0.02, (0.6, -0.17, DESK_TOP + 0.075), seg=12, r=0.005))
    accent.append(s.rcyl(0.008, 0.07, (0.6, -0.17, DESK_TOP + 0.11), seg=8, r=0.002))
    accent.append(s.ball(0.02, (0.6, -0.17, DESK_TOP + 0.15), seg=10))
    accent.append(s.rbox((1.2, 0.06, 0.06), (0, 0.37, DESK_TOP - 0.09), r=0.01, seg=1))           # cable trunk under the top
    s.part("Body", body)
    s.part("Accent", accent)
    s.part("Eye", eye)
    return rs.export("terminal_console")


def model_terminal_access():
    """The storage door's access station: a steel kiosk on a broad foot — a column, an angled head with a screen, a
    keypad and a card reader, a striped warning collar and a beacon on top; screen and beacon in one Eye part."""
    body, accent, eye = [], [], []
    body.append(s.rbox((0.62, 0.5, 0.06), (0, 0.05, 0.03), r=0.02, seg=2))                         # foot
    body.append(s.rbox((0.3, 0.24, 1.0), (0, 0.08, 0.56), r=0.03, seg=3, taper=(0.85, 0.85)))      # column
    accent.append(s.rbox((0.32, 0.26, 0.06), (0, 0.08, 0.2), r=0.01, seg=1))                       # collar (the stripes are its material)
    tilt = (math.radians(-35), 0, 0)
    head_z = 1.12
    body.append(s.rbox((0.5, 0.12, 0.4), (0, 0.0, head_z), r=0.03, seg=3, rot=tilt))               # the head
    # its face looks up and to the operator: a thing at (dx, dz) on the face (dz up its slope), d thick, stands proud of it
    th = math.radians(-35)

    def face(dz, w, h, d, dx=0.0):
        y, z = -0.06 - d / 2, dz                                     # in the head's own frame (front face at y = -0.06)
        yy, zz = y * math.cos(th) - z * math.sin(th), y * math.sin(th) + z * math.cos(th)
        return s.rbox((w, d, h), (dx, yy, head_z + zz), r=0.004, seg=1, rot=tilt)
    eye.append(face(0.07, 0.38, 0.17, 0.008))                                                       # the screen
    for k in range(3):
        for j in range(3):
            accent.append(face(-0.07 - j * 0.035, 0.03, 0.025, 0.012, dx=-0.09 + k * 0.04))         # keypad
    accent.append(face(-0.1, 0.12, 0.06, 0.02, dx=0.14))                                            # card reader
    eye.append(face(-0.075, 0.07, 0.006, 0.024, dx=0.14))                                           # its slot light
    accent.append(s.rcyl(0.05, 0.05, (0, 0.1, head_z + 0.24), seg=16, r=0.008))                    # beacon base
    eye.append(s.ball(0.055, (0, 0.1, head_z + 0.3), seg=16, scale=(1, 1, 0.9)))                   # beacon
    s.part("Body", body)
    s.part("Accent", accent)
    s.part("Eye", eye)
    return rs.export("terminal_access")


MODELS = {"terminal_console": model_terminal_console, "terminal_access": model_terminal_access}

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = set(args[args.index("--only") + 1].split(",")) if "--only" in args else None
    s.clear_scene()
    for name, fn in MODELS.items():
        if only and name not in only:
            continue
        print("exported", fn())
    print("DONE")
