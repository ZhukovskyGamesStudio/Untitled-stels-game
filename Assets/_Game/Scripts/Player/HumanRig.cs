using UnityEngine;

namespace Stealth.Player
{
    /// <summary>What the body is busy with besides walking (replicated by the owner; the rigged human plays a take for it).</summary>
    public enum BodyPose : byte
    {
        None,
        /// <summary>On a ladder: the Climb take, played by the climbing speed (backwards on the way down).</summary>
        Ladder,
        /// <summary>At a terminal's keyboard, a safe's dial, a vault console: typing.</summary>
        Terminal,
        /// <summary>Hands on a lock, a grille, a robot, a teammate: holding E with the progress growing.</summary>
        Work,
    }

    /// <summary>
    /// A rigged human that a visual style can put in place of the player model (<see cref="Configs.StyleGeometry.PlayerRig"/>,
    /// built from Tools/Blender/human.py). Purely visual: <see cref="PlayerController"/> feeds it the speed, crouch, downed
    /// state, what the body is busy with and the look pitch every frame; it drives the Animator (Idle/Walk/Run, crouch,
    /// lying, the seat, the ladder, the terminal, work with the hands) and tilts the head after the animation. Its parts
    /// are named like the player model's (Body…, Accent…, Eye…) so <see cref="PlayerLooks.Paint"/> colours them per player.
    /// </summary>
    public class HumanRig : MonoBehaviour
    {
        public Animator Animator;
        [Tooltip("The head bone: tilted by the look pitch after the animation")]
        public Transform HeadBone;
        [Tooltip("How much of the look pitch the head follows")]
        [Range(0, 1)] public float HeadFollow = 0.7f;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int DownId = Animator.StringToHash("Down");
        static readonly int SitId = Animator.StringToHash("Sit");
        static readonly int PoseId = Animator.StringToHash("Pose");
        static readonly int ClimbId = Animator.StringToHash("ClimbSpeed");

        float m_Pitch;

        /// <summary>The state of the player this frame. <paramref name="climb"/>: metres a second up the ladder (negative: down).</summary>
        public void Pose(float speed, bool crouching, bool downed, float pitch, bool seated = false, BodyPose pose = BodyPose.None, float climb = 0f)
        {
            pose = HumanMath.Shown(pose, downed, seated);
            m_Pitch = HumanMath.HeadPitch(pitch, HumanMath.HeadShare(HeadFollow, pose), downed);
            if (Animator == null || !Animator.isActiveAndEnabled) return;
            // in a seat the body rides with the car: its speed is the car's, not a walk
            Animator.SetFloat(SpeedId, HumanMath.WalkSpeed(speed, seated || pose != BodyPose.None), 0.1f, Time.deltaTime);
            Animator.SetBool(CrouchId, crouching && !downed && !seated && pose == BodyPose.None);
            Animator.SetBool(DownId, downed);
            Animator.SetBool(SitId, seated && !downed);
            Animator.SetInteger(PoseId, (int)pose);
            Animator.SetFloat(ClimbId, HumanMath.ClimbRate(climb), 0.08f, Time.deltaTime);
        }

        void LateUpdate()
        {
            if (HeadBone == null || Mathf.Approximately(m_Pitch, 0f)) return;
            HeadBone.rotation = Quaternion.AngleAxis(m_Pitch, transform.right) * HeadBone.rotation;
        }
    }

    /// <summary>Rules of the rigged human (pure, unit tested).</summary>
    public static class HumanMath
    {
        /// <summary>The Climb take is one step of each hand: this many metres of ladder, at this many metres a second.</summary>
        public const float ClimbCycle = 0.6f, ClimbTakeSpeed = 0.5f;

        /// <summary>How far the head tilts for a look pitch (degrees, + looks down): a share of it, clamped; none when lying.</summary>
        public static float HeadPitch(float lookPitch, float follow, bool downed) =>
            downed ? 0f : Mathf.Clamp(lookPitch * follow, -45f, 45f);

        /// <summary>The speed the walk blend gets: none in a seat (riding in the car is not walking) or busy with the hands.</summary>
        public static float WalkSpeed(float speed, bool seated) => seated ? 0f : speed;

        /// <summary>What the body is busy with: a ladder first, then a terminal, then the hands at work.</summary>
        public static BodyPose PoseOf(bool climbing, bool atTerminal, bool working) =>
            climbing ? BodyPose.Ladder : atTerminal ? BodyPose.Terminal : working ? BodyPose.Work : BodyPose.None;

        /// <summary>The pose the rig shows: lying or seated, the body is busy with nothing else.</summary>
        public static BodyPose Shown(BodyPose pose, bool downed, bool seated) => downed || seated ? BodyPose.None : pose;

        /// <summary>
        /// How fast the Climb take plays for a climbing speed (metres a second up, negative down): its own speed at
        /// <see cref="ClimbTakeSpeed"/>, backwards on the way down, still when the player hangs on the ladder.
        /// </summary>
        public static float ClimbRate(float metresPerSecond) =>
            Mathf.Abs(metresPerSecond) < 0.03f ? 0f : Mathf.Clamp(metresPerSecond / ClimbTakeSpeed, -3f, 3f);

        /// <summary>
        /// How much of the look pitch the head follows in a pose: on a ladder the eyes go where the player looks; at a
        /// terminal and at work the take moves the head (the player's camera is on the terminal's screen, not in the head).
        /// </summary>
        public static float HeadShare(float follow, BodyPose pose) => pose is BodyPose.Terminal or BodyPose.Work ? 0f : follow;

        /// <summary>The progress of a hold is growing: the hands are at work (it is kept when E is let go, so its being there is not work).</summary>
        public static bool Growing(float now, float before) => now > before + 1e-5f;
    }
}
