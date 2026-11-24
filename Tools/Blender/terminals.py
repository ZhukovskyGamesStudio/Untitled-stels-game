"""The security terminals (SecurityTerminal): the robots' control console (hacking robots from afar, the cameras) and the
storage door's access station — two looks that must never be mistaken for each other.

Run:  blender.exe -b --factory-startup --python-exit-code 1 --python Tools/Blender/terminals.py -- Assets/_Game/Art/Models
      (writes Props/terminal_console.fbx and Props/terminal_access.fbx)
Look: blender.exe -b --factory-startup --python Tools/Blender/review.py -- sheet.png <model.fbx> --floor

Conventions (generate_models): metres, front faces Blender -Y (Unity +Z: the operator's side), z up, origin on the floor
in the middle of the footprint (a desk's: 1.6 x 0.8 — both replace a desk of the plan). Parts: Body (the furniture),
Accent (keys, stands, trims), Eye (the buttons and lamps: SecurityTerminal tints them by the terminal's kind), EyeScreen
(the monitors' screens, each UV-mapped 0..1 on its own: SecurityTerminal shows the kind's picture on them).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rooms2_special as rs  # noqa: E402  (export(), torus())
import keyboard  # noqa: E402
import shapes as s  # noqa: E402

DESK_TOP = 0.76


def screen_uvs(obj, frames):
    """Each screen's own 0..1 UVs: a vertex takes the frame (centre, right, up, width, height) of the nearest screen."""
    from mathutils import Vector
    me = obj.data
    uv = me.uv_layers.active.data
    for loop in me.loops:
        p = obj.matrix_world @ me.vertices[loop.vertex_index].co
        c, r, u, w, h = min(frames, key=lambda f: (p - Vector(f[0])).length)
        d = p - Vector(c)
        uv[loop.index].uv = (d.dot(Vector(r)) / w + 0.5, d.dot(Vector(u)) / h + 0.5)


def _monitor(cx, cy, yaw, body, accent, eye, w=0.54, h=0.33, frames=None):
    """A flat monitor on a stand, its screen to -Y turned by yaw (degrees, about z) round its own stand."""
    a = math.radians(yaw)
    fx, fy = math.sin(a), -math.cos(a)              # the screen's facing in plan
    z = DESK_TOP + 0.13 + h / 2
    rot = (math.radians(-6), 0, a)                  # tipped back a little
    accent.append(s.rbox((0.22, 0.16, 0.012), (cx, cy, DESK_TOP + 0.006), r=0.006, seg=1, rot=(0, 0, a)))   # foot
    accent.append(s.rcyl(0.018, 0.13, (cx, cy, DESK_TOP + 0.075), seg=10, r=0.004))                           # neck
    body.append(s.rbox((w, 0.035, h), (cx, cy, z), r=0.008, seg=2, rot=rot))                                    # the case
    # the screen: 4 mm proud of the case's front, inside its bezel
    centre = (cx + fx * 0.0195, cy + fy * 0.0195, z)
    eye.append(s.rbox((w - 0.04, 0.004, h - 0.04), centre, r=0.002, seg=1, rot=rot))
    if frames is not None:
        frames.append((centre, (-fy, fx, 0.0), (0.0, 0.0, 1.0), w - 0.04, h - 0.04))  # right: the viewer's, looking at it


def model_terminal_console():
    """The robots' control console: a steel desk on two pedestals, three monitors in an arc at its back, a keyboard, a
    console panel of chunky buttons and a joystick to the right, a cable trunk at the back; screens in one Eye part."""
    body, accent, eye, screens, frames = [], [], [], [], []
    body.append(s.rbox((1.6, 0.8, 0.04), (0, 0, DESK_TOP - 0.02), r=0.01, seg=2))                  # top
    for x in (-0.58, 0.58):
        body.append(s.rbox((0.42, 0.72, DESK_TOP - 0.06), (x, 0.02, (DESK_TOP - 0.04) / 2), r=0.012, seg=2))   # pedestals
        accent.append(s.rbox((0.36, 0.01, 0.012), (x, -0.345, 0.5), r=0.003, seg=1))                     # drawer pulls
        accent.append(s.rbox((0.36, 0.01, 0.012), (x, -0.345, 0.24), r=0.003, seg=1))
    body.append(s.rbox((0.74, 0.02, 0.4), (0, 0.36, 0.42), r=0.004, seg=1))                         # modesty panel
    for x, y, yaw in ((-0.5, 0.18, -22), (0.0, 0.24, 0), (0.5, 0.18, 22)):
        _monitor(x, y, yaw, body, accent, screens, frames=frames)
    keyboard.add("Keys", (-0.1, -0.2, DESK_TOP - 0.0005), mouse=0.0)
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
    screen_uvs(s.part("EyeScreen", screens), frames)
    return rs.export("terminal_console")


def model_terminal_access():
    """The storage door's access station: a steel kiosk on a broad foot — a column, a broad angled head that is nearly
    all screen (it shows what the station is for: the storeroom's door, locked or open — Tools/Art/generate_screens.py;
    the screen has the picture's own 16:10, so nothing is stretched), a card reader under it, a striped warning collar
    and a beacon on top; the reader's light and the beacon in one Eye part (red locked, green open)."""
    body, accent, eye = [], [], []
    body.append(s.rbox((0.62, 0.5, 0.06), (0, 0.05, 0.03), r=0.02, seg=2))                         # foot
    body.append(s.rbox((0.3, 0.24, 1.0), (0, 0.08, 0.56), r=0.03, seg=3, taper=(0.85, 0.85)))      # column
    accent.append(s.rbox((0.32, 0.26, 0.06), (0, 0.08, 0.2), r=0.01, seg=1))                       # collar (the stripes are its material)
    tilt = (math.radians(-35), 0, 0)
    head_z = 1.14
    body.append(s.rbox((0.58, 0.12, 0.48), (0, 0.0, head_z), r=0.03, seg=3, rot=tilt))             # the head
    # its face looks up and to the operator: a thing at (dx, dz) on the face (dz up its slope), d thick, stands proud of it
    th = math.radians(-35)

    def face(dz, w, h, d, dx=0.0, sink=0.0):
        y, z = -0.06 - d / 2 + sink, dz                              # in the head's own frame (front face at y = -0.06)
        yy, zz = y * math.cos(th) - z * math.sin(th), y * math.sin(th) + z * math.cos(th)
        return s.rbox((w, d, h), (dx, yy, head_z + zz), r=0.004, seg=1, rot=tilt)
    sw, sh, sz = 0.496, 0.31, 0.05                                                                  # the screen: 16:10
    accent.append(face(sz, sw + 0.03, sh + 0.03, 0.012, sink=0.006))                                # its bezel (sunk: no face shares a plane)
    screen = face(sz, sw, sh, 0.012, sink=0.002)
    y0, z0 = -0.06 - 0.004, sz
    frame = ((0.0, y0 * math.cos(th) - z0 * math.sin(th), head_z + y0 * math.sin(th) + z0 * math.cos(th)),
             (1.0, 0.0, 0.0), (0.0, -math.sin(th), math.cos(th)), sw, sh)
    accent.append(face(-0.175, 0.2, 0.05, 0.024, sink=0.004))                                       # card reader, under the screen
    eye.append(face(-0.175, 0.13, 0.008, 0.03, sink=0.003))                                         # its slot light
    accent.append(s.rcyl(0.055, 0.05, (0, 0.1, head_z + 0.27), seg=16, r=0.008))                   # beacon base
    eye.append(s.ball(0.065, (0, 0.1, head_z + 0.335), seg=16, scale=(1, 1, 0.9)))                 # beacon
    s.part("Body", body)
    s.part("Accent", accent)
    s.part("Eye", eye)
    screen_uvs(s.part("EyeScreen", [screen]), [frame])
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
