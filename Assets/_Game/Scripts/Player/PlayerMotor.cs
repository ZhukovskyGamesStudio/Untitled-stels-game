using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Player
{
    /// <summary>Raw input of one frame. Filled from the keyboard/mouse or by the test autopilot.</summary>
    public struct PlayerInputState
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool Sprint;
        public bool CrouchHeld;
        public bool Jump; // pressed this frame

        public static PlayerInputState FromDevices(bool allowLook)
        {
            var s = new PlayerInputState();
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return s;
            if (kb.wKey.isPressed) s.Move.y += 1;
            if (kb.sKey.isPressed) s.Move.y -= 1;
            if (kb.dKey.isPressed) s.Move.x += 1;
            if (kb.aKey.isPressed) s.Move.x -= 1;
            s.Sprint = kb.leftShiftKey.isPressed;
            s.CrouchHeld = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
            s.Jump = kb.spaceKey.wasPressedThisFrame;
            if (allowLook && mouse != null) s.Look = mouse.delta.ReadValue();
            return s;
        }
    }

    /// <summary>Owner-side first person motor: acceleration based movement, crouch and camera.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Speed (m/s)")]
        public float WalkSpeed = 3.6f;
        public float SprintSpeed = 6.2f;
        public float CrouchSpeed = 1.8f;

        [Header("Acceleration (m/s²)")]
        public float GroundAccel = 45f;
        public float GroundDecel = 60f;
        public float AirAccel = 8f;

        [Header("Jump")]
        public float JumpHeight = 0.9f;
        [Tooltip("Jump still works this long after walking off an edge.")]
        public float CoyoteTime = 0.12f;
        [Tooltip("A jump pressed this long before landing is executed on landing.")]
        public float JumpBuffer = 0.15f;

        [Header("Look")]
        public float MouseSensitivity = 0.1f;

        public Vector3 Velocity => m_Velocity;
        public float HorizontalSpeed => new Vector3(m_Velocity.x, 0, m_Velocity.z).magnitude;
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool Grounded => m_Controller.isGrounded;
        public float Pitch => m_Pitch;

        /// <summary>Raised on landing with the downward speed at impact (m/s).</summary>
        public event System.Action<float> Landed;
        public event System.Action Jumped;

        CharacterController m_Controller;
        Vector3 m_Velocity;
        float m_Pitch;
        float m_HeadHeight = 1.6f;
        float m_LastGroundedTime = -10f;
        float m_JumpQueuedUntil = -10f;
        bool m_WasGrounded = true;
        float m_FallSpeed;

        void Awake() => m_Controller = GetComponent<CharacterController>();

        public void SetPitch(float pitch) => m_Pitch = pitch;

        public void ResetMotion() => m_Velocity = Vector3.zero;

        /// <summary>Simulates one frame. <paramref name="canAct"/> = false while downed or in a menu.</summary>
        public void Tick(PlayerInputState input, bool canAct, bool downed, Transform head, float dt)
        {
            if (!canAct) input = default;

            transform.Rotate(0f, input.Look.x * MouseSensitivity, 0f);
            m_Pitch = Mathf.Clamp(m_Pitch - input.Look.y * MouseSensitivity, -85f, 85f);

            bool crouch = input.CrouchHeld && !downed;
            if (!crouch && IsCrouching && !CanStandUp()) crouch = true;
            IsCrouching = crouch;
            IsSprinting = !crouch && input.Sprint && input.Move.y > 0.1f;

            float speed = crouch ? CrouchSpeed : IsSprinting ? SprintSpeed : WalkSpeed;
            var wish = transform.forward * input.Move.y + transform.right * input.Move.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool grounded = m_Controller.isGrounded;
            var horizontal = MovementMath.Accelerate(m_Velocity, wish, speed * Mathf.Min(1f, input.Move.magnitude),
                grounded ? GroundAccel : AirAccel, grounded ? GroundDecel : AirAccel * 0.5f, dt);
            float now = Time.time;
            if (grounded) m_LastGroundedTime = now;
            if (input.Jump) m_JumpQueuedUntil = now + JumpBuffer;
            bool canJump = !crouch && !downed && canAct && MovementMath.CanJump(now, m_LastGroundedTime, m_JumpQueuedUntil, CoyoteTime);
            float vertical;
            if (canJump)
            {
                vertical = MovementMath.JumpVelocity(JumpHeight, Physics.gravity.y);
                m_JumpQueuedUntil = -10f;
                m_LastGroundedTime = -10f;
                Jumped?.Invoke();
            }
            else vertical = grounded && m_Velocity.y <= 0f ? -2f : m_Velocity.y + Physics.gravity.y * dt;
            m_Velocity = new Vector3(horizontal.x, vertical, horizontal.z);
            var flags = m_Controller.Move(m_Velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && m_Velocity.y > 0f) m_Velocity.y = 0f;

            bool nowGrounded = m_Controller.isGrounded;
            if (!nowGrounded) m_FallSpeed = Mathf.Max(0f, -m_Velocity.y);
            if (nowGrounded && !m_WasGrounded) Landed?.Invoke(m_FallSpeed);
            m_WasGrounded = nowGrounded;
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Lose the speed that went into the wall so that we do not "stick" when sliding along it.
                var actual = m_Controller.velocity;
                m_Velocity.x = actual.x;
                m_Velocity.z = actual.z;
            }

            // Crouch height & camera.
            float targetHeight = crouch ? PlayerController.CrouchHeight : PlayerController.StandHeight;
            m_Controller.height = Mathf.MoveTowards(m_Controller.height, targetHeight, dt * 5f);
            m_Controller.center = new Vector3(0, m_Controller.height / 2f, 0);
            float targetHead = downed ? 0.35f : (crouch ? 0.95f : 1.6f);
            m_HeadHeight = Mathf.MoveTowards(m_HeadHeight, targetHead, dt * 4f);
            head.localPosition = new Vector3(0, m_HeadHeight, 0);
            head.localRotation = Quaternion.Euler(m_Pitch, 0, downed ? 25f : 0f);
        }

        bool CanStandUp() =>
            !Physics.SphereCast(transform.position + Vector3.up * 0.5f, m_Controller.radius * 0.9f, Vector3.up, out _,
                PlayerController.StandHeight - 0.5f, ~(1 << 2), QueryTriggerInteraction.Ignore);
    }
}
