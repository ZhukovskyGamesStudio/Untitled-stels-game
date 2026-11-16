"""Robot and security-device placeholder models. Run after generate_models.py helpers are loaded:

  blender.exe -b --factory-startup --python Tools/Blender/robots.py -- <output_dir>

Naming (see PrototypeBuilder.Paint): Body*, Accent*, Eye* (sensor parts glow and show the robot state).
Named sub-objects that the game animates: AccentRotor*, AccentLeg*, AccentHead, AccentWheel*.
"""
import math
import os
import sys

import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (re-uses helpers; generate_models runs its own export on import)

box, cylinder, sphere, merge, export, clear_scene = g.box, g.cylinder, g.sphere, g.merge, g.export, g.clear_scene


def drone():
    """Quadcopter, ~0.9 m across. Hovers ~2 m high, looks down-forward with a camera eye."""
    rotors = [(0.42, 0.42), (-0.42, 0.42), (0.42, -0.42), (-0.42, -0.42)]
    merge("Body", [box((0.5, 0.5, 0.18), (0, 0, 0))] + [beam((0, 0, 0.02), (x, y, 0.02), 0.05) for x, y in rotors])
    for i, (x, y) in enumerate(rotors):
        merge(f"AccentRotor{i}", [box((0.36, 0.05, 0.015), (x, y, 0.1)), box((0.05, 0.36, 0.015), (x, y, 0.1)), cylinder(0.03, 0.08, (x, y, 0.06))])
    merge("Accent", [box((0.3, 0.3, 0.04), (0, 0, 0.1))])                            # top plate (accent colour)
    merge("Eye", [sphere(0.1, (0, -0.24, -0.08), segments=12)])
    export("robot_drone")


def beam(p0, p1, t):
    """A square rod of thickness t from p0 to p1 in any direction (built from the current box, so families restyle it)."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    bm = box((d.length, t, t), (0, 0, 0))
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Vector((1, 0, 0)).rotation_difference(d.normalized()).to_matrix(),
                     verts=bm.verts)
    bmesh.ops.translate(bm, vec=(a + b) / 2, verts=bm.verts)
    return bm


def spider():
    """Wall crawler: a flat two-part body hugging the surface, eight legs fanned out around it with the knees raised
    and the feet on the surface, a cluster of eyes in front. Local up = away from the surface, forward = -Y.
    Every leg is a thigh (AccentLeg{n}Upper) and a shin (AccentLeg{n}Lower): ProceduralLegs plants the feet."""
    merge("Body", [sphere(0.14, (0, -0.15, 0.15), segments=12), sphere(0.2, (0, 0.17, 0.17), segments=12)])
    merge("Accent", [box((0.16, 0.24, 0.06), (0, 0.17, 0.36))])                      # back plate (accent colour)
    angles = (52, 18, -14, -46)  # degrees ahead of straight sideways, front to back
    for i in range(8):
        side = 1 if i < 4 else -1
        a = math.radians(angles[i % 4])
        d = Vector((side * math.cos(a), -math.sin(a), 0))
        centre = Vector((0, -0.02, 0))
        hip = centre + d * 0.1 + Vector((0, 0, 0.16))
        knee = centre + d * 0.36 + Vector((0, 0, 0.34))
        foot = centre + d * 0.62
        merge(f"AccentLeg{i}Upper", [beam(hip, knee, 0.045)])
        merge(f"AccentLeg{i}Lower", [beam(knee, foot + Vector((0, 0, 0.02)), 0.038), sphere(0.028, foot + Vector((0, 0, 0.02)), segments=8)])
    merge("Eye", [sphere(0.045, (dx, -0.29, 0.18 + dz), segments=8) for dx, dz in [(-0.05, 0.03), (0.05, 0.03), (-0.09, -0.02), (0.09, -0.02)]])
    export("robot_spider")


def vacuum():
    """Robot vacuum that rides on the ceiling (game flips it upside down). Scanner lens on the 'bottom'."""
    merge("Body", [cylinder(0.36, 0.1, (0, 0, 0.05), segments=24)])
    merge("Accent", [cylinder(0.3, 0.02, (0, 0, 0.11), segments=24), box((0.5, 0.06, 0.03), (0, -0.2, -0.01))])
    merge("Eye", [cylinder(0.07, 0.05, (0, 0, -0.02), segments=12), box((0.2, 0.04, 0.03), (0, -0.33, 0.06))])
    export("robot_vacuum")


def turret():
    """Sentry turret on a tripod; the head (with barrel and eye) turns."""
    merge("Body", [beam((0.3 * math.cos(a), 0.3 * math.sin(a), 0.0), (0.07 * math.cos(a), 0.07 * math.sin(a), 1.0), 0.06)
                   for a in (0.5, 2.6, 4.7)] + [cylinder(0.14, 0.24, (0, 0, 1.06), segments=12)])
    merge("AccentHead", [box((0.36, 0.42, 0.26), (0, 0, 1.3)), box((0.08, 0.45, 0.08), (0.1, -0.35, 1.3)), box((0.08, 0.45, 0.08), (-0.1, -0.35, 1.3))])
    merge("Eye", [cylinder(0.06, 0.04, (0, -0.22, 1.38), axis="Y")])
    export("robot_turret")


def thumper():
    """Heavy tracked robot with seismic sensor plates touching the floor in front."""
    merge("Body", [box((1.0, 1.2, 0.6), (0, 0, 0.75)), box((0.6, 0.6, 0.3), (0, 0.15, 1.2))])
    merge("Accent", [box((0.25, 1.4, 0.56), (-0.56, 0, 0.28)), box((0.25, 1.4, 0.56), (0.56, 0, 0.28))])  # tracks reach under the body
    merge("Eye", [cylinder(0.18, 0.04, (-0.3, -0.85, 0.02), segments=12), cylinder(0.18, 0.04, (0.3, -0.85, 0.02), segments=12),
                  beam((-0.3, -0.5, 0.5), (-0.3, -0.85, 0.03), 0.05), beam((0.3, -0.5, 0.5), (0.3, -0.85, 0.03), 0.05)])
    export("robot_thumper")


def hound():
    """Quadruped with a long sniffing snout."""
    merge("Body", [box((0.4, 1.0, 0.35), (0, 0, 0.75)), box((0.3, 0.35, 0.3), (0, -0.6, 0.95))])
    for i, (x, y) in enumerate([(0.15, -0.35), (-0.15, -0.35), (0.15, 0.35), (-0.15, 0.35)]):
        g.leg(f"AccentLeg{i}", [box((0.09, 0.09, 0.6), (x, y, 0.3))], 0.3)
    merge("Accent", [box((0.32, 0.12, 0.08), (0, -0.48, 0.93))])                      # collar (accent colour)
    merge("Eye", [cylinder(0.08, 0.4, (0, -0.95, 0.9), axis="Y", radius2=0.03)])
    export("robot_hound")


def sorter():
    """Slim colour-sensing robot with one big coloured lens."""
    merge("Body", [box((0.4, 0.35, 1.0), (0, 0, 0.9)), box((0.12, 0.12, 0.45), (0, 0, 0.22))])
    merge("Accent", [cylinder(0.3, 0.08, (0, 0, 0.04), segments=16)])
    merge("Eye", [cylinder(0.22, 0.06, (0, -0.2, 1.25), axis="Y", segments=20)])
    export("robot_sorter")


# The worker's lift (worker_lift.py, CargoMotion): two arms hinged on the sides of the sensor pod swing a level tray
# from its dock on top of the mast (at rest) forward and down to the floor in front. Everything of the body stays out
# of the tray's path: under the tray's resting place the body keeps below the arc its back edge sweeps
# (z < WORKER_SWEEP_Z0 + sqrt(WORKER_SWEEP_R² - (y - WORKER_SWEEP_Y)²)), and within x ±0.36 (the arms run at ±0.4).
WORKER_HINGE = (-0.15, 0.562)                       # (y, z) of the arms' hinge (CargoMotion.Hinge)
WORKER_SWEEP_Y, WORKER_SWEEP_Z0, WORKER_SWEEP_R = 0.05, 0.547, 0.843


def worker():
    """Warehouse runner, 1.4 m: a low drive deck on four small wheels with rubber bumpers front and back, a sensor pod
    in front (the camera module seats on its face at z 0.5; the lift's arms are hinged on its flanks) joined by a
    fairing to a stepped mast at the back (a wide battery block, a slimmer electronics tower) with a cable chain up its
    front, the tray's dock and cradle at its top and an amber beacon on its cap. The tray and the arms are
    worker_lift.py: at rest the tray sits on the dock against the cradle's pads, over the open space between the arms.
    Contract: wheels r 0.12, width 0.1 at (±0.31, ±0.25, 0.12); hinge (0, -0.15, 0.562)."""
    hy, hz = WORKER_HINGE
    # drive deck: a chassis between the wheels, a deck plate over them (the arms swing past outside it), bumpers
    body = [box((0.5, 0.74, 0.16), (0, 0, 0.2)),
            box((0.7, 0.8, 0.07), (0, 0, 0.285))]                                    # deck plate over the wheels
    for i, (x, y) in enumerate([(0.31, 0.25), (-0.31, 0.25), (0.31, -0.25), (-0.31, -0.25)]):
        merge(f"AccentWheel{i}", [cylinder(0.12, 0.1, (x, y, 0.12), axis="X", segments=20),
                                  cylinder(0.07, 0.11, (x, y, 0.12), axis="X", segments=12)])   # tyre + hub
    accent = [cylinder(0.035, 0.12, (x * 0.24, y, 0.12), axis="X", segments=10)
              for x, y in ((1, 0.25), (-1, 0.25), (1, -0.25), (-1, -0.25))]           # axles into the hubs
    accent += [box((0.7, 0.07, 0.11), (0, -0.41, 0.205)),                             # front bumper (rubber)
               box((0.7, 0.07, 0.11), (0, 0.41, 0.205))]                              # rear bumper
    # sensor pod in front: the face the camera module sits on, the arms' shoulders on its flanks
    body.append(box((0.48, 0.34, 0.32), (0, -0.23, 0.47)))                            # y -0.40..-0.06, z 0.31..0.63
    body.append(box((0.4, 0.28, 0.05), (0, -0.23, 0.645)))                            # its lid
    accent.append(box((0.42, 0.03, 0.2), (0, -0.405, 0.47)))                          # dark face plate
    eye = [box((0.08, 0.02, 0.035), (sx * 0.15, -0.425, 0.385)) for sx in (-1, 1)]     # headlights under the camera
    for sx in (-1, 1):
        accent.append(cylinder(0.085, 0.07, (sx * 0.26, hy, hz), axis="X", segments=20))   # shoulder drum (the arm's axle goes in)
        body.append(cylinder(0.05, 0.02, (sx * 0.3, hy, hz), axis="X", segments=16))      # its cap
    body.append(box((0.4, 0.2, 0.12), (0, 0.0, 0.37)))                               # fairing from the pod to the mast
    # the mast at the back (nothing swings behind the hinge, so it may be wider than the deck at its foot): a battery
    # block, a slimmer electronics tower, the dock and the cradle the tray rests against, a cable chain, a beacon
    body.append(box((0.6, 0.32, 0.48), (0, 0.23, 0.54)))                              # battery block, y 0.07..0.39
    body.append(box((0.44, 0.26, 0.54), (0, 0.2, 1.04)))                              # tower, y 0.07..0.33, up to 1.31
    accent.append(box((0.62, 0.34, 0.05), (0, 0.23, 0.78)))                           # band between them
    body.append(box((0.48, 0.3, 0.05), (0, 0.2, 1.315)))                              # cap (yellow: seen from above)
    accent.append(box((0.4, 0.2, 0.04), (0, 0.02, 1.335)))                            # dock under the tray's back (y -0.08..0.12)
    for sx in (-1, 1):
        accent.append(box((0.04, 0.16, 0.12), (sx * 0.17, 0.04, 1.27)))              # the dock's brackets
        accent.append(box((0.045, 0.045, 0.22), (sx * 0.2, 0.0875, 1.37)))            # cradle posts behind the tray
        accent.append(box((0.07, 0.03, 0.07), (sx * 0.2, 0.072, 1.45)))               # their pads (the backrest docks here)
    for k in range(7):                                                                # cable chain up the mast's front
        accent.append(box((0.12, 0.04, 0.1), (0, 0.06, 0.48 + k * 0.115)))
    for k in range(4):                                                                # hazard stripes on the battery's back
        a, b = Vector((-0.24 + k * 0.12, 0.395, 0.36)), Vector((-0.12 + k * 0.12, 0.395, 0.56))
        accent.append(beam(a, b, 0.045))
    accent.append(cylinder(0.075, 0.035, (0, 0.26, 1.355), segments=16))              # beacon base, at the back
    eye.append(cylinder(0.055, 0.075, (0, 0.26, 1.41), segments=16, radius2=0.045))   # the beacon (status colour)
    merge("Body", body)
    merge("Accent", accent)
    merge("Eye", eye)
    export("robot_worker")


def hunter():
    """Tall angular hunter (2.3 m), shows up after alarms."""
    merge("Body", [box((0.55, 0.4, 0.9), (0, 0, 1.55)), box((0.16, 0.16, 1.1), (-0.2, 0.05, 0.55)), box((0.16, 0.16, 1.1), (0.2, 0.05, 0.55))])
    merge("Accent", [box((0.12, 0.6, 0.12), (-0.33, -0.2, 1.6)), box((0.12, 0.6, 0.12), (0.33, -0.2, 1.6)),
                     box((0.7, 0.08, 0.08), (0, 0.1, 2.03)), box((0.08, 0.08, 0.35), (0, 0.15, 2.2))])
    merge("Eye", [box((0.38, 0.05, 0.08), (0, -0.21, 1.85)), sphere(0.06, (0, -0.2, 2.0), segments=8)])
    export("robot_hunter")


def scanner():
    """Floor lidar pole: the head spins a laser line at knee height."""
    merge("Body", [cylinder(0.18, 0.06, (0, 0, 0.03), segments=16), cylinder(0.05, 0.4, (0, 0, 0.23), segments=8)])
    merge("AccentHead", [cylinder(0.1, 0.12, (0, 0, 0.45), segments=12)])
    merge("Eye", [box((0.04, 0.06, 0.04), (0, -0.11, 0.45))])
    export("sensor_lidar")


def pir():
    """Wall-mounted passive infrared motion sensor (mounted on +Y side, looks to -Y)."""
    merge("Body", [box((0.16, 0.08, 0.16), (0, 0.04, 0))])
    merge("Eye", [sphere(0.07, (0, -0.02, 0), segments=10)])
    export("sensor_pir")


def plate():
    """Pressure plate in a doorway: a steel slab flush with the floor, a glowing border in the status colour and four
    corner studs. Visible to anyone who watches where they walk: jump over it or hack it."""
    s, h = 1.1, 0.012
    merge("Body", [box((s - 0.07, s - 0.07, h), (0, 0, h / 2))])
    k = s / 2 - 0.06
    merge("Accent", [box((0.07, 0.07, h + 0.006), (x * k, y * k, (h + 0.006) / 2)) for x in (-1, 1) for y in (-1, 1)])
    b, e = 0.035, h + 0.004
    merge("Eye", [box((s, b, e), (0, s / 2 - b / 2, e / 2)), box((s, b, e), (0, -s / 2 + b / 2, e / 2)),
                  box((b, s - 2 * b, e), (s / 2 - b / 2, 0, e / 2)), box((b, s - 2 * b, e), (-s / 2 + b / 2, 0, e / 2))])
    export("sensor_plate")


# --------------------------------------------------------------------------- sensor attachments
# Mounted on mobile robots by the asset builder according to their senses, so that a player can tell what a robot
# perceives by looking at it. Origin = mount point, front = -Y (Unity +Z). Glowing parts (Eye*) show the robot state.

def attach_eye():
    """Camera eye: a housing with a hood and a big glowing lens."""
    merge("Accent", [cylinder(0.1, 0.14, (0, 0.02, 0), axis="Y", segments=16), cylinder(0.115, 0.05, (0, -0.06, 0), axis="Y", segments=16, radius2=0.1)])
    merge("Eye", [cylinder(0.07, 0.03, (0, -0.07, 0), axis="Y", segments=16)])
    export("attach_eye")


def attach_mic():
    """Microphone on a stalk: thin rod, a grilled ball on top (glows)."""
    merge("Accent", [cylinder(0.015, 0.28, (0, 0, 0.14), segments=8), cylinder(0.05, 0.02, (0, 0, 0.01), segments=10)])
    merge("Eye", [sphere(0.065, (0, 0, 0.32), segments=12)])
    merge("Body", [cylinder(0.07, 0.015, (0, 0, 0.32 + d), segments=12) for d in (-0.03, 0.0, 0.03)])
    export("attach_mic")


def attach_seismic():
    """Ground probe: a plate with a spike down to the floor (origin at floor level)."""
    merge("Accent", [cylinder(0.08, 0.03, (0, 0, 0.3), segments=12), cylinder(0.02, 0.22, (0, 0, 0.19), segments=8)])
    merge("Eye", [cylinder(0.04, 0.1, (0, 0, 0.05), segments=10, radius2=0.002)])
    export("attach_seismic")


def attach_nose():
    """Sniffer snout pointing forward with glowing vents at the tip."""
    merge("Accent", [cylinder(0.09, 0.26, (0, -0.13, 0), axis="Y", segments=12, radius2=0.05)])
    merge("Eye", [box((0.03, 0.02, 0.02), (x, -0.265, 0)) for x in (-0.02, 0.02)] + [box((0.07, 0.02, 0.015), (0, -0.265, -0.03))])
    export("attach_nose")


if __name__ == "__main__":
    clear_scene()
    for fn in (drone, spider, vacuum, turret, thumper, hound, sorter, worker, hunter, scanner, pir, plate,
               attach_eye, attach_mic, attach_seismic, attach_nose):
        fn()
    print("ROBOTS DONE")
