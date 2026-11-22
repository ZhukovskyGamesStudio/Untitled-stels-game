using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// The players' common humanoid skeleton (pure, unit tested): which bone of the rigged humans (Tools/Blender/humanlib
    /// anim.py — all five people share it) is which bone of Unity's humanoid Avatar, and the T-pose the Avatar is described
    /// in (the models rest in an A-pose). With it every person plays the same humanoid takes, an Animator layer can take
    /// just the legs, the upper body, the head or the fingers (AvatarMask), and the hands and the head follow IK goals.
    /// </summary>
    public static class HumanSkeleton
    {
        /// <summary>The fingers of a hand, as the bones are named: <c>&lt;finger&gt;.&lt;1..3&gt;.&lt;L|R&gt;</c>, 1 from the knuckle.</summary>
        public static readonly string[] Fingers = { "thumb", "index", "middle", "ring", "little" };

        /// <summary>Our bone → the humanoid bone: the body, then every finger's three bones (<see cref="FingerMap"/>).</summary>
        public static readonly (string bone, HumanBodyBones human)[] Map = Body().Concat(FingerMap()).ToArray();

        /// <summary>The fingers' bones → the humanoid ones (the enum runs thumb … little, proximal … distal, left then right).</summary>
        static IEnumerable<(string bone, HumanBodyBones human)> FingerMap()
        {
            for (int side = 0; side < 2; side++)
            for (int f = 0; f < Fingers.Length; f++)
            for (int i = 0; i < 3; i++)
                yield return ($"{Fingers[f]}.{i + 1}.{(side == 0 ? "L" : "R")}",
                    (side == 0 ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal) + f * 3 + i);
        }

        static (string bone, HumanBodyBones human)[] Body() => new[]
        {
            ("hips", HumanBodyBones.Hips), ("spine", HumanBodyBones.Spine), ("chest", HumanBodyBones.Chest),
            ("neck", HumanBodyBones.Neck), ("head", HumanBodyBones.Head),
            ("shoulder.L", HumanBodyBones.LeftShoulder), ("upper_arm.L", HumanBodyBones.LeftUpperArm),
            ("forearm.L", HumanBodyBones.LeftLowerArm), ("hand.L", HumanBodyBones.LeftHand),
            ("shoulder.R", HumanBodyBones.RightShoulder), ("upper_arm.R", HumanBodyBones.RightUpperArm),
            ("forearm.R", HumanBodyBones.RightLowerArm), ("hand.R", HumanBodyBones.RightHand),
            ("thigh.L", HumanBodyBones.LeftUpperLeg), ("shin.L", HumanBodyBones.LeftLowerLeg),
            ("foot.L", HumanBodyBones.LeftFoot), ("toe.L", HumanBodyBones.LeftToes),
            ("thigh.R", HumanBodyBones.RightUpperLeg), ("shin.R", HumanBodyBones.RightLowerLeg),
            ("foot.R", HumanBodyBones.RightFoot), ("toe.R", HumanBodyBones.RightToes),
        };

        /// <summary>The humanoid bones an Avatar cannot do without.</summary>
        public static readonly HumanBodyBones[] Required =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        /// <summary>
        /// The T-pose, bone by bone (parents first): which way the bone from <c>bone</c> to <c>child</c> points — up the
        /// spine, the arms straight out to their side (<see cref="Out"/>: the side of the shoulder from the chest), the legs
        /// straight down, the four fingers straight out with the hand (a finger's last bone has no child: null — it lies
        /// along its own axis). The feet, toes and thumbs keep their own direction.
        /// </summary>
        public static readonly (string bone, string child, Aim aim)[] TPose = BodyTPose().Concat(FingerTPose()).ToArray();

        static IEnumerable<(string bone, string child, Aim aim)> FingerTPose()
        {
            foreach (var side in new[] { "L", "R" })
            foreach (var finger in Fingers.Skip(1))
            for (int i = 1; i <= 3; i++)
                yield return ($"{finger}.{i}.{side}", i < 3 ? $"{finger}.{i + 1}.{side}" : null, Aim.Out);
        }

        static (string bone, string child, Aim aim)[] BodyTPose() => new[]
        {
            ("hips", "spine", Aim.Up), ("spine", "chest", Aim.Up), ("chest", "neck", Aim.Up), ("neck", "head", Aim.Up),
            ("shoulder.L", "upper_arm.L", Aim.Out), ("upper_arm.L", "forearm.L", Aim.Out), ("forearm.L", "hand.L", Aim.Out), ("hand.L", "middle.1.L", Aim.Out),
            ("shoulder.R", "upper_arm.R", Aim.Out), ("upper_arm.R", "forearm.R", Aim.Out), ("forearm.R", "hand.R", Aim.Out), ("hand.R", "middle.1.R", Aim.Out),
            ("thigh.L", "shin.L", Aim.Down), ("shin.L", "foot.L", Aim.Down),
            ("thigh.R", "shin.R", Aim.Down), ("shin.R", "foot.R", Aim.Down),
        };

        public enum Aim { Up, Down, Out }

        /// <summary>
        /// How the Avatar spreads a limb's twist between the bone and its child (HumanDescription.upperArmTwist…): our limbs
        /// have no twist bones, so the twist stays where the take keyed it — these values play the takes back bone for bone
        /// (measured: Stealth → Human Rig Audit).
        /// </summary>
        public const float ArmTwist = 1f, LegTwist = 0f;

        /// <summary>The world direction a bone points in the T-pose: <paramref name="outward"/> is its side, flat (arms).</summary>
        public static Vector3 Direction(Aim aim, Vector3 outward) => aim switch
        {
            Aim.Up => Vector3.up,
            Aim.Down => Vector3.down,
            _ => Flat(outward),
        };

        /// <summary>The turn that makes a bone pointing <paramref name="now"/> point <paramref name="wanted"/> (the shortest one).</summary>
        public static Quaternion Turn(Vector3 now, Vector3 wanted) =>
            now.sqrMagnitude < 1e-10f || wanted.sqrMagnitude < 1e-10f ? Quaternion.identity : Quaternion.FromToRotation(now, wanted);

        /// <summary>Flat and of unit length (the arms' "outward" is horizontal).</summary>
        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-10f ? v.normalized : Vector3.right;
        }

        /// <summary>
        /// Where a player carries the thing in the hand, in the body's space (x right, y up, z forward, metres for a body
        /// 1.8 m tall): the right hand raised before the chest, the forearm ahead (the elbow lower, by the side) — where the
        /// thing is seen.
        /// </summary>
        public static readonly Vector3 Carry = new(0.19f, 1.2f, 0.4f), CarryElbow = new(0.3f, 0.95f, -0.02f);

        /// <summary>
        /// The right hand's IK turn when carrying, in the body's space: a closed fist standing before the chest, the thing
        /// upright in it (picked from a grid of turns on the pose pictures: Stealth → Human Pose Pictures).
        /// </summary>
        public static readonly Quaternion CarryHand = Quaternion.Euler(0f, 0f, 180f) * Quaternion.Euler(0f, 90f, 0f);

        /// <summary>
        /// The right hand's grip as humanoid finger muscles (curve names of a humanoid clip; −1 bent .. 1 stretched): the
        /// four fingers closed round a handle, each joint curled, the little finger tightest; the thumb comes in over them.
        /// </summary>
        public static (string muscle, float value)[] Grip => s_Grip ??= GripMuscles().ToArray();
        static (string muscle, float value)[] s_Grip;

        /// <summary>How far each finger's three joints close in the grip (index … little), and the thumb's.</summary>
        static readonly float[] GripCurl = { -0.62f, -0.72f, -0.5f }, GripThumb = { -0.25f, -0.45f, -0.4f };

        static IEnumerable<(string muscle, float value)> GripMuscles() => HandMuscles(GripCurl, 0.05f, -0.2f, GripThumb, -0.5f);

        /// <summary>
        /// The right hand cupped under a compact thing (a jar, a box, a tablet lying on it): the palm open, the fingers a
        /// little curled round its far edge, the thumb out at its side.
        /// </summary>
        public static (string muscle, float value)[] Cup => s_Cup ??= HandMuscles(CupCurl, 0.03f, 0.15f, CupThumb, 0.3f).ToArray();
        static (string muscle, float value)[] s_Cup;
        static readonly float[] CupCurl = { 0.35f, 0.1f, 0.0f }, CupThumb = { 0.3f, 0.2f, 0.1f };

        static IEnumerable<(string muscle, float value)> HandMuscles(float[] curl, float tighter, float spread, float[] thumb, float thumbSpread)
        {
            string[] names = { "Index", "Middle", "Ring", "Little" };
            for (int f = 0; f < names.Length; f++)
            {
                for (int i = 0; i < 3; i++) yield return ($"RightHand.{names[f]}.{i + 1} Stretched", curl[i] - tighter * f);
                yield return ($"RightHand.{names[f]}.Spread", spread);
            }
            for (int i = 0; i < 3; i++) yield return ($"RightHand.Thumb.{i + 1} Stretched", thumb[i]);
            yield return ("RightHand.Thumb.Spread", thumbSpread);
        }
    }

    /// <summary>The layers of the rigged humans' Animator controller (AssetBuilder.Humans builds it), by index.</summary>
    public static class HumanLayers
    {
        /// <summary>The base: root, legs and feet.</summary>
        public const int Legs = 0;
        /// <summary>Spine, arms and hands; its IK pass puts the right hand on the carried thing.</summary>
        public const int UpperBody = 1;
        /// <summary>The head and the face (its takes; the look turns the head after the animation: HumanRig).</summary>
        public const int Head = 2;
        /// <summary>The right fingers: the grip, weighted while the hand holds something.</summary>
        public const int RightHand = 3;
        public const int Count = 4;
    }
}
