"""The skeleton and the takes of the rigged human (in place: the game moves the body, the takes move the limbs).

The rest pose is the A-pose of humanlib.body. A pose is written in the figure's own axes — forward / outward / twist
in degrees for every bone, whatever way the bone lies — and the takes are built from a few shared pieces: a stance
(how the legs stand and the pelvis hangs), arms hanging, breathing, a gait.
"""
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

from .body import J, side

FPS = 30


def _bones():
    b = [("hips", J["hips"], J["spine"], None), ("spine", J["spine"], J["chest"], "hips"), ("chest", J["chest"], J["neck"] - (0, 0.005, 0.03), "spine"),
         ("neck", J["neck"], J["head"], "chest"), ("head", J["head"], J["crown"], "neck")]
    from . import body as B
    thumb0 = J["wrist"] + B.HAND_D * 0.030 + B.HAND_T * 0.030
    thumb1 = J["wrist"] + B.HAND_D * 0.100 + B.HAND_T * 0.062 + B.HAND_N * 0.03
    for s, sd in ((1, "L"), (-1, "R")):
        m = lambda p: side(p, s)
        b += [(f"shoulder.{sd}", m(J["clavicle"]), m(J["shoulder"]), "chest"),
              (f"upper_arm.{sd}", m(J["shoulder"]), m(J["elbow"]), f"shoulder.{sd}"),
              (f"forearm.{sd}", m(J["elbow"]), m(J["wrist"]), f"upper_arm.{sd}"),
              (f"hand.{sd}", m(J["wrist"]), m(J["knuckle"]), f"forearm.{sd}"),
              (f"fingers.{sd}", m(J["knuckle"]), m(J["fingertip"]), f"hand.{sd}"),
              (f"thumb.{sd}", m(thumb0), m(thumb1), f"hand.{sd}"),
              (f"thigh.{sd}", m(J["hip"]), m(J["knee"]), "hips"),
              (f"shin.{sd}", m(J["knee"]), m(J["ankle"]), f"thigh.{sd}"),
              (f"foot.{sd}", m(J["ankle"]), m(J["ball"]), f"shin.{sd}"),
              (f"toe.{sd}", m(J["ball"]), m(J["toe"]), f"foot.{sd}")]
    return b


BONES = _bones()


def skeleton():
    arm = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.scene.objects:
        o.select_set(o == rig)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, h, t, parent in BONES:
        eb = arm.edit_bones.new(name)
        eb.head, eb.tail = Vector(h), Vector(t)
        eb.roll = 0.0
        if parent:
            eb.parent = arm.edit_bones[parent]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return rig


def bone_segments():
    return {name: (Vector(h), Vector(t)) for name, h, t, _ in BONES}


# --------------------------------------------------------------------------- posing

class Poser:
    """Poses the rig in the figure's axes: x to its left, -y its front, z up (degrees)."""

    def __init__(self, rig):
        self.rig = rig
        self.rest = {b.name: b.matrix_local.to_3x3() for b in rig.data.bones}
        self.q = {}
        self.loc = {}

    def clear(self):
        self.q = {name: Quaternion() for name in self.rest}
        self.loc = {}

    def turn(self, bone, axis, degrees):
        """A rotation about one of the figure's axes ((1,0,0) pitches, (0,1,0) rolls, (0,0,1) turns to its left)."""
        self.q[bone] = self.q[bone] @ Quaternion(Vector(axis), math.radians(degrees))

    def _dir(self, bone):
        b = self.rig.data.bones[bone]
        return (b.tail_local - b.head_local).normalized()

    def _towards(self, bone, where, fallback):
        """The axis about which a positive turn swings the bone's end towards `where` (whatever way the bone lies)."""
        d = self._dir(bone)
        a = d.cross(Vector(where))
        return a.normalized() if a.length > 0.25 else Vector(fallback)

    def bend(self, bone, fwd=0.0, out=0.0, twist=0.0):
        """A limb swung forward (its end towards the figure's front; a hand, fingers and a thumb: towards the palm),
        outward (away from the body) and twisted about its own length (outward for a positive angle). Degrees."""
        s = -1.0 if bone.endswith(".R") else 1.0
        d = self._dir(bone)
        if bone.split(".")[0] in ("hand", "fingers", "thumb"):
            from . import body as B
            palm = Vector((B.HAND_N[0] * s, B.HAND_N[1], B.HAND_N[2]))
            front = palm
        else:
            front = Vector((0, -1, 0))
        q = Quaternion()
        if out:
            q = q @ Quaternion(self._towards(bone, (s, 0, 0), (0, -s, 0)), math.radians(out))
        if fwd:
            q = q @ Quaternion(self._towards(bone, front, (-1, 0, 0)), math.radians(fwd))
        if twist:
            q = q @ Quaternion(d, math.radians(-twist * s))
        self.q[bone] = self.q[bone] @ q

    def lean(self, bone, fwd=0.0, side_=0.0, turn=0.0):
        """A bone of the spine: bowed forward, to the figure's left, turned to its left. Degrees."""
        q = Quaternion()
        if turn:
            q = q @ Quaternion(Vector((0, 0, 1)), math.radians(turn))
        if side_:
            q = q @ Quaternion(self._towards(bone, (1, 0, 0), (0, 1, 0)), math.radians(side_))
        if fwd:
            q = q @ Quaternion(self._towards(bone, (0, -1, 0), (1, 0, 0)), math.radians(fwd))
        self.q[bone] = self.q[bone] @ q

    def reach(self, sd, fwd=0.0, spread=0.0, twist=0.0):
        """The upper arm of a side ("L" / "R") by where it points, not by how far it is from the rest pose: hanging
        straight down, raised `fwd` degrees to the front (90 level, 180 overhead), then swung `spread` degrees away from
        the body, twisted about itself (outward for a positive angle)."""
        from .body import ARM_OUT
        s = -1.0 if sd == "R" else 1.0
        bone = f"upper_arm.{sd}"
        down = Quaternion(Vector((0, 1, 0)), ARM_OUT * s)                 # (the rest pose holds the arm ARM_OUT off the body)
        q = Quaternion(Vector((0, 0, 1)), math.radians(spread) * s) @ Quaternion(Vector((1, 0, 0)), math.radians(-fwd)) @ down
        if twist:
            q = q @ Quaternion(self._dir(bone), math.radians(-twist * s))
        self.q[bone] = self.q[bone] @ q

    def move(self, dx=0.0, dy=0.0, dz=0.0):
        """The pelvis moved in the figure's axes (metres)."""
        self.loc["hips"] = Vector(self.loc.get("hips", (0, 0, 0))) + Vector((dx, dy, dz))

    def apply(self):
        for pb in self.rig.pose.bones:
            r = self.rest[pb.name]
            q = self.q.get(pb.name, Quaternion())
            pb.rotation_quaternion = (r.inverted() @ q.to_matrix() @ r).to_quaternion()
            pb.location = (r.inverted() @ Vector(self.loc.get(pb.name, (0, 0, 0)))) if pb.name in self.loc else (0, 0, 0)


def _ease(t):
    return t * t * (3 - 2 * t)


def animate(rig):
    """All takes as actions (and NLA strips, so the exporter writes a take per action). Returns the actions."""
    P = Poser(rig)
    rig.animation_data_create()
    two_pi = 2 * math.pi

    def key(name, frames, pose):
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        rig.animation_data.action = act
        for f in range(frames + 1):
            P.clear()
            pose(f / frames)
            P.apply()
            for pb in rig.pose.bones:
                pb.keyframe_insert("rotation_quaternion", frame=f + 1)
                pb.keyframe_insert("location", frame=f + 1)
        return act

    # ---- shared pieces

    def arms(fwd=2.0, spread=6.0, elbow=14.0, sway=0.0, hand=14.0):
        """Arms hanging at the sides (a few degrees off the body: over the hips), elbows a little bent, fingers loose."""
        for s, sg in (("L", 1.0), ("R", -1.0)):
            P.reach(s, fwd=fwd + sway * sg, spread=spread, twist=-8)
            P.bend(f"forearm.{s}", fwd=elbow)
            P.bend(f"hand.{s}", fwd=4)
            P.bend(f"fingers.{s}", fwd=hand)
            P.bend(f"thumb.{s}", fwd=6)

    def breathe(t, amount=1.0, rate=1.0):
        b = math.sin(t * two_pi * rate)
        P.lean("chest", fwd=-0.9 * b * amount)
        P.lean("spine", fwd=0.4 * b * amount)
        for s in ("L", "R"):
            P.bend(f"shoulder.{s}", out=1.2 * b * amount)
        return b

    def legs(thigh, knee, foot=None, out=0.0, toe=0.0, which="LR"):
        for s in which:
            P.bend(f"thigh.{s}", fwd=thigh, out=out)
            P.bend(f"shin.{s}", fwd=-knee)
            P.bend(f"foot.{s}", fwd=(knee - thigh) if foot is None else foot)   # by default the sole stays level
            if toe:
                P.bend(f"toe.{s}", fwd=toe)

    # ---- standing

    def idle(t):
        """Standing at ease: the weight drifts from one leg to the other and back, the chest breathes twice, the head
        glances aside once — slow enough to loop unnoticed (8 s)."""
        shift = math.sin(t * two_pi)                       # + on the left leg
        b = breathe(t, 1.0, 3.0)
        P.move(dx=0.014 * shift, dz=-0.004 * abs(shift))
        P.lean("hips", side_=-1.6 * shift, turn=1.0 * shift)
        P.lean("spine", side_=1.2 * shift)
        P.lean("chest", side_=0.8 * shift, turn=-1.2 * shift)
        glance = math.sin(_ease(max(0.0, min(1.0, (t - 0.55) / 0.3))) * math.pi)
        P.lean("neck", fwd=1.5 - 0.6 * b)
        P.lean("head", turn=14 * glance - 2.0 * shift, fwd=2 - 0.8 * b, side_=-0.8 * shift)
        for s, sg in (("L", 1.0), ("R", -1.0)):
            w = max(0.0, -shift * sg)                      # this leg is the free one
            P.bend(f"thigh.{s}", fwd=2 + 5 * w, out=1.5 + 1.6 * shift * sg)
            P.bend(f"shin.{s}", fwd=-(3 + 9 * w))
            P.bend(f"foot.{s}", fwd=1 + 4 * w, out=5)
        arms(fwd=2, spread=6, elbow=16 + 1.5 * b, sway=1.5 * shift, hand=16)

    def gait(t, stride, knee, arm, elbow, lean, bob, sway, crouch=0.0, toe_off=18.0, twist=7.0, fist=16.0):
        """One cycle of both legs: the heel strikes ahead, the body rides over the planted leg, the foot rolls off its
        toes, the knee folds through the swing; the pelvis drops on the passing side and turns with the stride, the
        chest and the arms counter it."""
        ph = t * two_pi
        P.move(dz=-crouch - bob * (0.5 - 0.5 * math.cos(2 * ph)), dx=sway * math.sin(ph))
        P.lean("hips", fwd=lean * 0.25, turn=twist * math.sin(ph), side_=3.0 * math.sin(ph + math.pi / 2))
        P.lean("spine", fwd=lean * 0.4, turn=-0.9 * twist * math.sin(ph), side_=-1.5 * math.sin(ph + math.pi / 2))
        P.lean("chest", fwd=lean * 0.35, turn=-1.3 * twist * math.sin(ph), side_=-2.0 * math.sin(ph + math.pi / 2))
        P.lean("neck", fwd=-lean * 0.3)
        P.lean("head", fwd=-lean * 0.45, turn=4 * math.sin(ph))
        for s, sg in (("L", 1.0), ("R", -1.0)):
            p = ph + (0 if sg > 0 else math.pi)
            swing = math.sin(p)                            # + the leg is ahead
            lift = max(0.0, math.sin(p - math.pi * 0.5 + 0.9))      # in the air: from toe-off, through the pass, to before the strike
            push = max(0.0, -math.sin(p + 0.35)) ** 2      # the planted leg behind the body: pushing off
            P.bend(f"thigh.{s}", fwd=stride * swing + 6 + knee * 0.28 * lift)
            P.bend(f"shin.{s}", fwd=-(5 + knee * lift + 8 * push))
            P.bend(f"foot.{s}", fwd=-0.25 * stride * swing + 10 * lift - toe_off * push + 4)
            P.bend(f"toe.{s}", fwd=toe_off * 1.4 * push)
            a = -swing                                     # the arm swings against its leg
            P.reach(s, fwd=arm * a + 3, spread=6 + 2 * max(0.0, -a), twist=-8)
            P.bend(f"forearm.{s}", fwd=elbow + 0.5 * arm * max(0.0, a))
            P.bend(f"hand.{s}", fwd=4)
            P.bend(f"fingers.{s}", fwd=fist)
            P.bend(f"thumb.{s}", fwd=fist * 0.4)
            P.bend(f"shoulder.{s}", fwd=3.0 * a)

    def walk(t):
        gait(t, stride=24, knee=54, arm=26, elbow=16, lean=4, bob=0.022, sway=0.012, twist=8)

    def run(t):
        gait(t, stride=42, knee=112, arm=44, elbow=84, lean=19, bob=0.05, sway=0.006, toe_off=30, twist=11, fist=62)

    # ---- crouching

    def crouched(t, breathe_amount=1.0):
        """Low and coiled: the back bowed over the knees, the head up, watching."""
        P.move(dz=-0.36, dy=-0.04)
        P.lean("hips", fwd=30)
        P.lean("spine", fwd=16)
        P.lean("chest", fwd=10)
        P.lean("neck", fwd=-22)
        P.lean("head", fwd=-26)
        breathe(t, breathe_amount, 2.0)

    def crouch_idle(t):
        """Sneaking, stopped: one foot ahead flat on the floor, the other back on its ball, the weight between them;
        the left hand low in front for balance, the right drawn back by the hip."""
        crouched(t)
        sh = math.sin(t * two_pi)
        P.move(dx=0.008 * sh)
        P.lean("hips", turn=-8)
        P.lean("chest", turn=10 + 2 * sh)
        P.bend("thigh.L", fwd=96, out=10)                 # the leading leg
        P.bend("shin.L", fwd=-104)
        P.bend("foot.L", fwd=12, out=6)
        P.bend("thigh.R", fwd=44, out=8)                  # the trailing leg, on the ball of the foot
        P.bend("shin.R", fwd=-112)
        P.bend("foot.R", fwd=46)
        P.bend("toe.R", fwd=42)
        P.reach("L", fwd=52, spread=10, twist=-10)
        P.bend("forearm.L", fwd=38)
        P.bend("fingers.L", fwd=22)
        P.reach("R", fwd=8, spread=14, twist=-14)
        P.bend("forearm.R", fwd=84)
        P.bend("fingers.R", fwd=34)

    def crouch_walk(t):
        """Sneaking: long low steps, heel to toe, the body level; the arms out a little for balance, countering the legs."""
        crouched(t, 0.5)
        ph = t * two_pi
        P.move(dz=-0.016 * (0.5 - 0.5 * math.cos(2 * ph)), dx=0.016 * math.sin(ph))
        P.lean("hips", turn=9 * math.sin(ph), side_=3 * math.sin(ph + math.pi / 2))
        P.lean("chest", turn=-10 * math.sin(ph))
        for s, sg in (("L", 1.0), ("R", -1.0)):
            p = ph + (0 if sg > 0 else math.pi)
            swing = math.sin(p)                            # + the leg is ahead
            lift = max(0.0, math.sin(p - math.pi * 0.5 + 0.9))
            push = max(0.0, -math.sin(p + 0.35)) ** 2
            P.bend(f"thigh.{s}", fwd=70 + 28 * swing + 10 * lift, out=9)
            P.bend(f"shin.{s}", fwd=-(96 + 30 * lift - 14 * swing + 8 * push))
            P.bend(f"foot.{s}", fwd=26 - 12 * swing + 12 * lift + 14 * push)
            P.bend(f"toe.{s}", fwd=26 * push)
            a = -swing
            P.reach(s, fwd=30 + 20 * a, spread=14, twist=-12)
            P.bend(f"forearm.{s}", fwd=58 + 12 * max(0.0, a))
            P.bend(f"fingers.{s}", fwd=26)

    # ---- down, seated

    def down(t):
        """Lying on the back, knocked out: limbs loose, the chest barely breathing."""
        b = math.sin(t * two_pi)
        P.turn("hips", (1, 0, 0), -88)                     # the whole body tipped onto its back (the head to the rear)
        P.move(dz=-0.82, dy=0.05)
        P.lean("chest", fwd=-1.0 * b)
        P.lean("head", turn=24, fwd=-6)
        # (the body lies on its back: "forward" is up off the floor now, so the limbs are laid out sideways and back)
        P.reach("L", fwd=-6, spread=48, twist=40)
        P.bend("forearm.L", fwd=16)
        P.reach("R", fwd=-4, spread=22, twist=30)
        P.bend("forearm.R", fwd=30)
        for s, sg in (("L", 1.0), ("R", -1.0)):
            P.bend(f"fingers.{s}", fwd=34)
            P.bend(f"thigh.{s}", fwd=4 if sg > 0 else 14, out=12 if sg > 0 else 18)
            P.bend(f"shin.{s}", fwd=-(8 if sg > 0 else 30))
            P.bend(f"foot.{s}", fwd=-26, out=16)

    def sit(t):
        """In the car's seat: thighs level, shins down, a little reclined, hands forward on the wheel."""
        b = breathe(t, 0.8)
        P.move(dz=-0.02)
        P.lean("hips", fwd=-10)
        P.lean("spine", fwd=6)
        P.lean("neck", fwd=4)
        P.lean("head", fwd=2 - 0.5 * b)
        legs(thigh=86, knee=82, foot=-4, out=5)
        for s in ("L", "R"):
            P.reach(s, fwd=30 + b, spread=10, twist=-18)
            P.bend(f"forearm.{s}", fwd=66)
            P.bend(f"hand.{s}", fwd=-10)
            P.bend(f"fingers.{s}", fwd=52)
            P.bend(f"thumb.{s}", fwd=20)

    # ---- the new takes: a ladder, a terminal, work with the hands

    def climb(t):
        """Up a ladder, hand over hand: the right hand and the left foot reach up together, then the others; the body
        hangs close to the rungs and rises and sinks a little with every step. (The game plays it by the climbing
        speed: backwards on the way down, held still when the player stops.)"""
        ph = t * two_pi
        P.move(dy=-0.06, dz=0.02 * math.sin(2 * ph))
        P.lean("hips", fwd=4, turn=6 * math.sin(ph), side_=4 * math.sin(ph))
        P.lean("spine", fwd=3, side_=-3 * math.sin(ph))
        P.lean("chest", fwd=2, turn=-5 * math.sin(ph))
        P.lean("neck", fwd=-12)
        P.lean("head", fwd=-16)
        for s, sg in (("L", 1.0), ("R", -1.0)):
            leg = 0.5 + 0.5 * math.sin(ph + (0 if sg > 0 else math.pi))            # 1 this foot is on the higher rung
            arm_up = 0.5 + 0.5 * math.sin(ph + (math.pi if sg > 0 else 0))          # the opposite hand goes with it
            P.bend(f"thigh.{s}", fwd=14 + 62 * leg, out=3)
            P.bend(f"shin.{s}", fwd=-(18 + 74 * leg))
            P.bend(f"foot.{s}", fwd=6 + 8 * leg)
            # the arm reaches out and up to its rung and pulls down past the chest: never back beside the head
            P.reach(s, fwd=66 + 74 * arm_up, spread=3, twist=-6)
            P.bend(f"forearm.{s}", fwd=52 - 40 * arm_up)
            P.bend(f"hand.{s}", fwd=-14)
            grip = 1.0 - 0.6 * max(0.0, math.sin(ph + (math.pi if sg > 0 else 0) + 1.2))   # the hand opens while it travels
            P.bend(f"fingers.{s}", fwd=70 * grip)
            P.bend(f"thumb.{s}", fwd=28 * grip)

    def terminal(t):
        """At a terminal's keyboard: standing bent over it, forearms level, the hands typing in bursts — fingers drumming,
        wrists hopping from key to key — the eyes going from the keys up to the screen and back."""
        b = breathe(t, 0.6, 2.0)
        P.move(dz=-0.03, dy=0.02)
        P.lean("hips", fwd=10)
        P.lean("spine", fwd=8)
        P.lean("chest", fwd=6)
        look = math.sin(_ease(0.5 + 0.5 * math.sin(t * two_pi * 2)) * math.pi * 0.5)       # 0 at the keys … 1 at the screen
        P.lean("neck", fwd=12 - 14 * look)
        P.lean("head", fwd=12 - 20 * look, turn=3 * math.sin(t * two_pi))
        legs(thigh=10, knee=14, out=3)
        for s, sg in (("L", 1.0), ("R", -1.0)):
            tap = math.sin(t * two_pi * 12 + (0 if sg > 0 else 1.9))                         # the fingers
            hop = math.sin(t * two_pi * 3 + (0 if sg > 0 else 2.4))                          # the hand moves along the keys
            burst = 0.35 + 0.65 * (0.5 + 0.5 * math.sin(t * two_pi * 2 + (0.6 if sg > 0 else 2.2)))
            P.reach(s, fwd=26 + 2 * hop, spread=10 + 3 * hop, twist=-28)
            P.bend(f"forearm.{s}", fwd=70 + 2.5 * hop * burst)
            P.bend(f"hand.{s}", fwd=-14 + 5 * tap * burst)
            P.bend(f"fingers.{s}", fwd=30 + 16 * tap * burst)
            P.bend(f"thumb.{s}", fwd=10 + 6 * math.sin(t * two_pi * 6 + sg))

    def work(t):
        """Hands on a lock, a panel, a robot's hatch: leaning in, one hand steadying, the other turning and prying."""
        b = breathe(t, 0.6, 2.0)
        ph = t * two_pi
        P.move(dz=-0.05, dy=-0.01)
        P.lean("hips", fwd=14)
        P.lean("spine", fwd=10, turn=3 * math.sin(ph))
        P.lean("chest", fwd=8, turn=-4)
        P.lean("neck", fwd=12)
        P.lean("head", fwd=9 + 2 * math.sin(2 * ph), turn=4)
        legs(thigh=14, knee=22, out=4)
        # the left hand holds the thing still
        P.reach("L", fwd=50, spread=4, twist=-24)
        P.bend("forearm.L", fwd=52 + 1.5 * math.sin(2 * ph))
        P.bend("hand.L", fwd=-8)
        P.bend("fingers.L", fwd=48)
        P.bend("thumb.L", fwd=18)
        # the right hand works: small turns of the wrist, a push now and then
        turn = math.sin(ph * 4)
        push = max(0.0, math.sin(ph * 2 - 0.6)) ** 2
        P.reach("R", fwd=56 + 6 * push, spread=2, twist=-22)
        P.bend("forearm.R", fwd=46 - 8 * push + 2 * turn, twist=22 * turn)
        P.bend("hand.R", fwd=-6 + 8 * turn)
        P.bend("fingers.R", fwd=58)
        P.bend("thumb.R", fwd=22)

    acts = [key("Idle", 240, idle), key("Walk", 32, walk), key("Run", 22, run), key("CrouchIdle", 90, crouch_idle),
            key("CrouchWalk", 40, crouch_walk), key("Down", 120, down), key("Sit", 90, sit),
            key("Climb", 36, climb), key("Terminal", 120, terminal), key("Work", 90, work)]
    rig.animation_data.action = None
    P.clear()
    P.apply()
    for a in acts:
        tr = rig.animation_data.nla_tracks.new()
        tr.name = a.name
        tr.strips.new(a.name, 1, a)
        tr.mute = True
    return acts
