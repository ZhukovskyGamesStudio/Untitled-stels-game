using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// A rigged human that a visual style can put in place of the player model (<see cref="Configs.StyleGeometry.PlayerRig"/>,
    /// built from Tools/Blender/human.py). Purely visual: <see cref="PlayerController"/> feeds it the speed, crouch, downed
    /// state and look pitch every frame; it drives the Animator (Idle/Walk/Run, crouch, lying) and tilts the head after the
    /// animation. Its parts are named like the player model's (Body…, Accent…, Eye…) so <see cref="PlayerLooks.Paint"/>
    /// colours them per player.
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

        float m_Pitch;

        /// <summary>The state of the player this frame.</summary>
        public void Pose(float speed, bool crouching, bool downed, float pitch)
        {
            m_Pitch = HumanMath.HeadPitch(pitch, HeadFollow, downed);
            if (Animator == null || !Animator.isActiveAndEnabled) return;
            Animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);
            Animator.SetBool(CrouchId, crouching && !downed);
            Animator.SetBool(DownId, downed);
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
        /// <summary>How far the head tilts for a look pitch (degrees, + looks down): a share of it, clamped; none when lying.</summary>
        public static float HeadPitch(float lookPitch, float follow, bool downed) =>
            downed ? 0f : Mathf.Clamp(lookPitch * follow, -45f, 45f);
    }
}
