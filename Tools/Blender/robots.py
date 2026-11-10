"""Robot and security-device placeholder models. Run after generate_models.py helpers are loaded:

  blender.exe -b --factory-startup --python Tools/Blender/robots.py -- <output_dir>

Naming (see PrototypeBuilder.Paint): Body*, Accent*, Eye* (sensor parts glow and show the robot state).
Named sub-objects that the game animates: AccentRotor*, AccentLeg*, AccentHead, AccentWheel*.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_models as g  # noqa: E402  (re-uses helpers; generate_models runs its own export on import)

box, cylinder, sphere, merge, export, clear_scene = g.box, g.cylinder, g.sphere, g.merge, g.export, g.clear_scene


def drone():
    """Quadcopter, ~0.9 m across. Hovers ~2 m high, looks down-forward with a camera eye."""
    merge("Body", [box((0.5, 0.5, 0.18), (0, 0, 0)), box((0.9, 0.06, 0.04), (0, 0, 0.02)), box((0.06, 0.9, 0.04), (0, 0, 0.02))])
    for i, (x, y) in enumerate([(0.42, 0.42), (-0.42, 0.42), (0.42, -0.42), (-0.42, -0.42)]):
        merge(f"AccentRotor{i}", [box((0.36, 0.05, 0.015), (x, y, 0.1)), box((0.05, 0.36, 0.015), (x, y, 0.1)), cylinder(0.03, 0.08, (x, y, 0.06))])
    merge("Eye", [sphere(0.1, (0, -0.24, -0.08), segments=12)])
    export("robot_drone")


def spider():
    """Wall crawler: low round body, eight legs, cluster of eyes. Local up = away from the surface."""
    merge("Body", [sphere(0.28, (0, 0.05, 0.2), segments=12), sphere(0.18, (0, -0.28, 0.18), segments=10)])
    for i in range(8):
        side = 1 if i < 4 else -1
        k = i % 4
        y = 0.25 - k * 0.17
        merge(f"AccentLeg{i}", [box((0.55, 0.05, 0.05), (side * 0.38, y, 0.25)), box((0.05, 0.05, 0.3), (side * 0.65, y, 0.1))])
    merge("Eye", [sphere(0.05, (dx, -0.43, 0.22 + dz), segments=8) for dx, dz in [(-0.06, 0.03), (0.06, 0.03), (-0.1, -0.03), (0.1, -0.03)]])
    export("robot_spider")


def vacuum():
    """Robot vacuum that rides on the ceiling (game flips it upside down). Scanner lens on the 'bottom'."""
    merge("Body", [cylinder(0.36, 0.1, (0, 0, 0.05), segments=24)])
    merge("Accent", [cylinder(0.3, 0.02, (0, 0, 0.11), segments=24), box((0.5, 0.06, 0.03), (0, -0.2, -0.01))])
    merge("Eye", [cylinder(0.07, 0.05, (0, 0, -0.02), segments=12), box((0.2, 0.04, 0.03), (0, -0.33, 0.06))])
    export("robot_vacuum")


def turret():
    """Sentry turret on a tripod; the head (with barrel and eye) turns."""
    merge("Body", [box((0.06, 0.06, 1.1), (0.25 * math.cos(a), 0.25 * math.sin(a), 0.5)) for a in (0.5, 2.6, 4.7)] +
          [cylinder(0.14, 0.2, (0, 0, 1.05), segments=12)])
    merge("AccentHead", [box((0.36, 0.42, 0.26), (0, 0, 1.3)), box((0.08, 0.45, 0.08), (0.1, -0.35, 1.3)), box((0.08, 0.45, 0.08), (-0.1, -0.35, 1.3))])
    merge("Eye", [cylinder(0.06, 0.04, (0, -0.22, 1.38), axis="Y")])
    export("robot_turret")


def thumper():
    """Heavy tracked robot with seismic sensor plates touching the floor in front."""
    merge("Body", [box((1.0, 1.2, 0.6), (0, 0, 0.75)), box((0.6, 0.6, 0.3), (0, 0.15, 1.2))])
    merge("Accent", [box((0.25, 1.4, 0.45), (-0.62, 0, 0.23)), box((0.25, 1.4, 0.45), (0.62, 0, 0.23))])
    merge("Eye", [cylinder(0.18, 0.04, (-0.3, -0.85, 0.02), segments=12), cylinder(0.18, 0.04, (0.3, -0.85, 0.02), segments=12),
                  box((0.05, 0.25, 0.05), (-0.3, -0.72, 0.15)), box((0.05, 0.25, 0.05), (0.3, -0.72, 0.15))])
    export("robot_thumper")


def hound():
    """Quadruped with a long sniffing snout."""
    merge("Body", [box((0.4, 1.0, 0.35), (0, 0, 0.75)), box((0.3, 0.35, 0.3), (0, -0.6, 0.95))])
    for i, (x, y) in enumerate([(0.15, -0.35), (-0.15, -0.35), (0.15, 0.35), (-0.15, 0.35)]):
        merge(f"AccentLeg{i}", [box((0.09, 0.09, 0.6), (x, y, 0.3))])
    merge("Eye", [cylinder(0.08, 0.4, (0, -0.95, 0.9), axis="Y", radius2=0.03)])
    export("robot_hound")


def sorter():
    """Slim colour-sensing robot with one big coloured lens."""
    merge("Body", [box((0.4, 0.35, 1.0), (0, 0, 0.9)), box((0.12, 0.12, 0.45), (0, 0, 0.22))])
    merge("Accent", [cylinder(0.3, 0.08, (0, 0, 0.04), segments=16)])
    merge("Eye", [cylinder(0.22, 0.06, (0, -0.2, 1.25), axis="Y", segments=20)])
    export("robot_sorter")


def worker():
    """Small wheeled worker carrying a crate — it snitches but cannot fight."""
    merge("Body", [box((0.6, 0.7, 0.35), (0, 0, 0.35)), box((0.08, 0.08, 0.5), (-0.25, -0.2, 0.75)), box((0.08, 0.08, 0.5), (0.25, -0.2, 0.75))])
    merge("Accent", [box((0.55, 0.45, 0.4), (0, -0.15, 1.15))])
    for i, (x, y) in enumerate([(0.32, 0.25), (-0.32, 0.25), (0.32, -0.25), (-0.32, -0.25)]):
        merge(f"AccentWheel{i}", [cylinder(0.12, 0.06, (x, y, 0.12), axis="X")])
    merge("Eye", [box((0.3, 0.04, 0.08), (0, -0.36, 0.45))])
    export("robot_worker")


def hunter():
    """Tall angular hunter (2.3 m), shows up after alarms."""
    merge("Body", [box((0.55, 0.4, 0.9), (0, 0, 1.55)), box((0.16, 0.16, 1.1), (-0.2, 0.05, 0.55)), box((0.16, 0.16, 1.1), (0.2, 0.05, 0.55))])
    merge("Accent", [box((0.12, 0.6, 0.12), (-0.45, -0.2, 1.6)), box((0.12, 0.6, 0.12), (0.45, -0.2, 1.6)),
                     box((0.7, 0.08, 0.08), (0, 0.1, 2.05)), box((0.08, 0.08, 0.35), (0, 0.15, 2.2))])
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
    """Pressure plate: a barely visible square in the floor with a thin glowing border."""
    merge("Body", [box((0.9, 0.9, 0.02), (0, 0, 0.01))])
    merge("Eye", [box((0.9, 0.04, 0.025), (0, 0.43, 0.012)), box((0.9, 0.04, 0.025), (0, -0.43, 0.012)),
                  box((0.04, 0.9, 0.025), (0.43, 0, 0.012)), box((0.04, 0.9, 0.025), (-0.43, 0, 0.012))])
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
    merge("Accent", [cylinder(0.08, 0.03, (0, 0, 0.3), segments=12), cylinder(0.02, 0.2, (0, 0, 0.18), segments=8)])
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
