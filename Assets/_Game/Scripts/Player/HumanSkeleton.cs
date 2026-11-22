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
        /// <summary>Our bone → the humanoid bone. The hand has one finger bone (the whole fingers) and the thumb.</summary>
        public static readonly (string bone, HumanBodyBones human)[] Map =
        {
            ("hips", HumanBodyBones.Hips), ("spine", HumanBodyBones.Spine), ("chest", HumanBodyBones.Chest),
            ("neck", HumanBodyBones.Neck), ("head", HumanBodyBones.Head),
            ("shoulder.L", HumanBodyBones.LeftShoulder), ("upper_arm.L", HumanBodyBones.LeftUpperArm),
            ("forearm.L", HumanBodyBones.LeftLowerArm), ("hand.L", HumanBodyBones.LeftHand),
            ("fingers.L", HumanBodyBones.LeftIndexProximal), ("thumb.L", HumanBodyBones.LeftThumbProximal),
            ("shoulder.R", HumanBodyBones.RightShoulder), ("upper_arm.R", HumanBodyBones.RightUpperArm),
            ("forearm.R", HumanBodyBones.RightLowerArm), ("hand.R", HumanBodyBones.RightHand),
            ("fingers.R", HumanBodyBones.RightIndexProximal), ("thumb.R", HumanBodyBones.RightThumbProximal),
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
        /// straight down. The feet, toes and thumbs keep their own direction.
        /// </summary>
        public static readonly (string bone, string child, Aim aim)[] TPose =
        {
            ("hips", "spine", Aim.Up), ("spine", "chest", Aim.Up), ("chest", "neck", Aim.Up), ("neck", "head", Aim.Up),
            ("shoulder.L", "upper_arm.L", Aim.Out), ("upper_arm.L", "forearm.L", Aim.Out), ("forearm.L", "hand.L", Aim.Out), ("hand.L", "fingers.L", Aim.Out),
            ("shoulder.R", "upper_arm.R", Aim.Out), ("upper_arm.R", "forearm.R", Aim.Out), ("forearm.R", "hand.R", Aim.Out), ("hand.R", "fingers.R", Aim.Out),
            ("thigh.L", "shin.L", Aim.Down), ("shin.L", "foot.L", Aim.Down),
            ("thigh.R", "shin.R", Aim.Down), ("shin.R", "foot.R", Aim.Down),
        };

        public enum Aim { Up, Down, Out }

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
        /// 1.8 m tall): the right hand before the chest, a little out to the side.
        /// </summary>
        public static readonly Vector3 Carry = new(0.17f, 1.08f, 0.3f), CarryElbow = new(0.4f, 1.15f, -0.05f);

        /// <summary>
        /// The right hand's grip as humanoid finger muscles (curve names of a humanoid clip; −1 bent .. 1 stretched): the one
        /// finger bone curls (it is mapped as the index), the thumb comes in over it.
        /// </summary>
        public static readonly (string muscle, float value)[] Grip =
        {
            ("RightHand.Index.1 Stretched", -0.9f), ("RightHand.Index.Spread", 0f),
            ("RightHand.Thumb.1 Stretched", -0.35f), ("RightHand.Thumb.Spread", -0.4f),
        };
    }

    /// <summary>The layers of the rigged humans' Animator controller (AssetBuilder.Humans builds it), by index.</summary>
    public static class HumanLayers
    {
        /// <summary>The base: root, legs and feet.</summary>
        public const int Legs = 0;
        /// <summary>Spine, arms and hands; its IK pass puts the right hand on the carried thing.</summary>
        public const int UpperBody = 1;
        /// <summary>The head and the face; its IK pass turns the head where the player looks.</summary>
        public const int Head = 2;
        /// <summary>The right fingers: the grip, weighted while the hand holds something.</summary>
        public const int RightHand = 3;
        public const int Count = 4;
    }
}
