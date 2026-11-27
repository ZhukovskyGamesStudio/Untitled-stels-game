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
        public bool CrouchToggle; // pressed this frame

        public static PlayerInputState FromDevices(bool allowLook)
        {
            var s = new PlayerInputState();
            var mouse = Mouse.current;
            if (Controls.Held(GameAction.Forward)) s.Move.y += 1;
            if (Controls.Held(GameAction.Back)) s.Move.y -= 1;
            if (Controls.Held(GameAction.Right)) s.Move.x += 1;
            if (Controls.Held(GameAction.Left)) s.Move.x -= 1;
            s.Sprint = Controls.Held(GameAction.Sprint);
            // Ctrl = hold to crouch (or toggle, if chosen in the settings), C = always toggles.
            s.CrouchHeld = Controls.Held(GameAction.CrouchHold) && !GameSettings.CrouchToggle;
            s.CrouchToggle = Controls.Pressed(GameAction.CrouchToggle) || (GameSettings.CrouchToggle && Controls.Pressed(GameAction.CrouchHold));
            s.Jump = Controls.Pressed(GameAction.Jump);
            if (allowLook && mouse != null) s.Look = mouse.delta.ReadValue();
            return s;
        }
    }

    /// <summary>Tuning of the first person motor (PlayerConfig).</summary>
    [System.Serializable]
    public class MovementSettings
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
        public float JumpHeight = 1.05f;
        [Tooltip("Jump still works this long after walking off an edge.")]
        public float CoyoteTime = 0.12f;
        [Tooltip("A jump pressed this long before landing is executed on landing.")]
        public float JumpBuffer = 0.15f;

        [Header("Ground")]
        [Tooltip("When walking down steps or slopes, stick to the ground below if it is at most this far.")]
        public float GroundSnapDistance = 0.45f;

        [Header("Camera")]
        [Tooltip("Extra field of view while sprinting, degrees.")]
        public float SprintFovKick = 7f;
        [Tooltip("Head bob amplitude at walking speed, meters.")]
        public float BobAmplitude = 0.035f;
        [Tooltip("Steps per meter travelled (one step = half a bob cycle).")]
        public float StepsPerMeter = 0.75f;
    }

    /// <summary>Owner-side first person motor: acceleration based movement, crouch and camera. Tuning: <see cref="MovementSettings"/>.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        MovementSettings m_S = new();

        /// <summary>Applies the tuning (from PlayerConfig): movement and stamina.</summary>
        public void Configure(MovementSettings movement, StaminaSettings stamina)
        {
            m_S = movement ?? new MovementSettings();
            Stamina = new Stamina(stamina);
        }

        public float WalkSpeed => m_S.WalkSpeed;
        public float SprintSpeed => m_S.SprintSpeed;
        public float CrouchSpeed => m_S.CrouchSpeed;
        float GroundAccel => m_S.GroundAccel;
        float GroundDecel => m_S.GroundDecel;
        float AirAccel => m_S.AirAccel;
        float JumpHeight => JumpHeightOverride > 0f ? JumpHeightOverride : m_S.JumpHeight;

        /// <summary>A higher jump than the tuned one (0: the tuned one): the jump boots.</summary>
        public float JumpHeightOverride { get; set; }
        float CoyoteTime => m_S.CoyoteTime;
        float JumpBuffer => m_S.JumpBuffer;
        float GroundSnapDistance => m_S.GroundSnapDistance;
        float SprintFovKick => m_S.SprintFovKick;
        float BobAmplitude => m_S.BobAmplitude;
        float StepsPerMeter => m_S.StepsPerMeter;

        public Vector3 Velocity => m_Velocity;
        public float HorizontalSpeed => new Vector3(m_Velocity.x, 0, m_Velocity.z).magnitude;
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        /// <summary>The player wants to stand up but something is right above the head.</summary>
        public bool BlockedByCeiling { get; private set; }
        public bool Grounded => m_Controller.isGrounded;
        public float Pitch => m_Pitch;
        public Stamina Stamina { get; private set; } = new();

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
        bool m_CrouchToggled;
        float m_Trauma;
        Vector3 m_Impulse;
        float m_Fov = GameSettings.DefaultFov;
        float m_HeadVelocity;
        float m_BobPhase;
        float m_BobWeight;
        float m_Dip;
        float m_DipVelocity;

        void Awake() => m_Controller = GetComponent<CharacterController>();

        // (desk monitors and the like: a body goes through them, PassableForPlayers)
        void OnEnable() => Stealth.Level.PassableForPlayers.Register(m_Controller != null ? m_Controller : GetComponent<CharacterController>());
        void OnDisable() => Stealth.Level.PassableForPlayers.Unregister(m_Controller);

        public void SetPitch(float pitch) => m_Pitch = pitch;

        /// <summary>Knockback: applied as velocity, horizontal part decays through normal deceleration.</summary>
        public void AddImpulse(Vector3 impulse) => m_Impulse += impulse;

        public void AddTrauma(float amount) => m_Trauma = Mathf.Clamp01(m_Trauma + amount);

        /// <summary>Down in the hatch: the eyes sink to just above the floor (peeking out), the body stays put.</summary>
        public bool InHatch { get; set; }
        /// <summary>The walk's speed × this (the instant coffee, a hard landing: <see cref="PlayerController"/> sets it).</summary>
        public float SpeedScale { get; set; } = 1f;
        /// <summary>Degrees added to the field of view (running on coffee feels fast).</summary>
        public float FovBoost { get; set; }
        /// <summary>Sitting in the car: no walking, falling or crouching (the car carries the player).</summary>
        public bool Seated { get; set; }

        /// <summary>
        /// The body held facing <see cref="HoldYaw"/> (on the ladder: the rungs; in a seat: the road): the mouse turns the
        /// head up to <see cref="HoldHead"/> degrees to either side and the body a share of it up to <see cref="HoldBody"/>
        /// (<see cref="LookLimits"/>), never round the back. Let go, the body turns to where the head looked.
        /// </summary>
        public bool LookHeld { get; set; }
        public float HoldYaw { get; set; }
        public float HoldHead { get; set; } = 90f;
        public float HoldBody { get; set; }
        /// <summary>Held: the head's turn off the body, and the body's off <see cref="HoldYaw"/>, degrees (0 when free).</summary>
        public float HeadYaw { get; private set; }
        public float BodyYaw { get; private set; }

        float m_Look;          // held: the look's degrees off HoldYaw
        bool m_WasHeld;

        /// <summary>The held look straight at the anchor (sitting down: eyes on the road).</summary>
        public void ResetLook()
        {
            m_Look = 0f;
            HeadYaw = BodyYaw = 0f;
        }
        public const float HatchEyeHeight = 0.24f;

        /// <summary>Crouched at once (pulled up into a duct: no room to stand); it stays crouched until there is (BlockedByCeiling).</summary>
        public void ForceCrouch()
        {
            IsCrouching = true;
            m_Controller.height = PlayerController.CrouchHeight;
            m_Controller.center = new Vector3(0, m_Controller.height / 2f, 0);
        }

        public void ResetMotion()
        {
            m_Impulse = Vector3.zero;
            m_Trauma = 0f;
            m_Velocity = Vector3.zero;
            m_CrouchToggled = false;
        }

        /// <summary>Simulates one frame. <paramref name="canAct"/> = false while downed or in a menu.</summary>
        public void Tick(PlayerInputState input, bool canAct, bool downed, Transform head, Camera cam, float dt)
        {
            if (!canAct) input = default;
            if (Seated) input = new PlayerInputState { Look = input.Look }; // in the car: look around, the car moves us

            // Raw mouse delta, no smoothing: the camera reacts on the same frame.
            float sens = GameSettings.Sensitivity;
            float turn = input.Look.x * sens;
            if (LookHeld)
            {
                // the body faces its anchor (a share of the look at most), the head turns within its limit
                if (!m_WasHeld) m_Look = LookLimits.Take(transform.eulerAngles.y + HeadYaw, HoldYaw, HoldHead, HoldBody);
                m_Look = LookLimits.Turn(m_Look, turn, HoldHead, HoldBody);
                BodyYaw = LookLimits.Body(m_Look, HoldHead, HoldBody);
                HeadYaw = m_Look - BodyYaw;
                transform.rotation = Quaternion.Euler(0f, HoldYaw + BodyYaw, 0f);
            }
            else
            {
                if (m_WasHeld && HeadYaw != 0f) transform.Rotate(0f, HeadYaw, 0f); // let go: the body turns to where the head looked (the view stays)
                HeadYaw = BodyYaw = 0f;
                transform.Rotate(0f, turn, 0f);
            }
            m_WasHeld = LookHeld;
            m_Pitch = Mathf.Clamp(m_Pitch - input.Look.y * sens * (GameSettings.InvertY ? -1f : 1f), -88f, 88f);

            if (input.CrouchToggle) m_CrouchToggled = !m_CrouchToggled;
            if (input.Sprint && input.Move.y > 0.1f && m_CrouchToggled && CanStandUp()) m_CrouchToggled = false; // sprint stands up
            if (downed) m_CrouchToggled = false;
            bool crouch = (input.CrouchHeld || m_CrouchToggled) && !downed;
            BlockedByCeiling = !crouch && IsCrouching && !CanStandUp();
            if (BlockedByCeiling) crouch = true;
            IsCrouching = crouch;
            bool wantsSprint = !crouch && input.Sprint && input.Move.y > 0.1f;
            IsSprinting = wantsSprint && Stamina.CanSprint;
            Stamina.Tick(IsSprinting && HorizontalSpeed > 0.5f, dt);

            float speed = (crouch ? CrouchSpeed : IsSprinting ? SprintSpeed : WalkSpeed) * SpeedScale;
            var wish = transform.forward * input.Move.y + transform.right * input.Move.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool grounded = m_Controller.isGrounded;
            var horizontal = MovementMath.Accelerate(m_Velocity, wish, speed * Mathf.Min(1f, input.Move.magnitude),
                grounded ? GroundAccel : AirAccel, grounded ? GroundDecel : AirAccel * 0.5f, dt);
            float now = Time.time;
            if (grounded) m_LastGroundedTime = now;
            if (input.Jump) m_JumpQueuedUntil = now + JumpBuffer;
            bool canJump = !crouch && !downed && canAct && MovementMath.CanJump(now, m_LastGroundedTime, m_JumpQueuedUntil, CoyoteTime);
            if (m_Impulse.sqrMagnitude > 0f)
            {
                horizontal += new Vector3(m_Impulse.x, 0f, m_Impulse.z);
                if (m_Impulse.y > 0f) { m_Velocity.y = m_Impulse.y; grounded = false; }
                m_Impulse = Vector3.zero;
            }
            float vertical;
            bool jumpedNow = canJump;
            if (canJump)
            {
                vertical = MovementMath.JumpVelocity(JumpHeight, Physics.gravity.y);
                m_JumpQueuedUntil = -10f;
                m_LastGroundedTime = -10f;
                Stamina.SpendJump();
                Jumped?.Invoke();
            }
            else vertical = grounded && m_Velocity.y <= 0f ? -2f : m_Velocity.y + Physics.gravity.y * dt;
            m_Velocity = new Vector3(horizontal.x, vertical, horizontal.z);
            var step = m_Velocity * dt;
            // While grounded push down by at least a few centimeters per frame: at high frame rates a tiny push
            // stays inside the skin width and isGrounded starts flickering.
            if (grounded && !jumpedNow && m_Velocity.y <= 0f) step.y = Mathf.Min(step.y, -0.03f);
            if (Seated) { m_Velocity = Vector3.zero; step = Vector3.zero; }
            // standing in the lift's cab: carried up or down with it (the cab's floor moves under the controller)
            float carry = Seated ? 0f : Level.LiftCab.CarryAt(transform.position, dt);
            if (carry != 0f && m_Controller.enabled)
            {
                m_Controller.enabled = false;
                transform.position += Vector3.up * carry;
                m_Controller.enabled = true;
                Level.PassableForPlayers.Refresh(m_Controller);
                if (m_Velocity.y < 0f) m_Velocity.y = -2f;
            }
            if (m_StepOffset < 0f) m_StepOffset = m_Controller.stepOffset;
            m_Controller.stepOffset = StepFor(m_Controller, step, m_StepOffset);
            var flags = Seated || !m_Controller.enabled ? CollisionFlags.None : m_Controller.Move(step);
            if ((flags & CollisionFlags.Above) != 0 && m_Velocity.y > 0f) m_Velocity.y = 0f;

            bool nowGrounded = m_Controller.isGrounded;
            bool recentlyGrounded = grounded || now - m_LastGroundedTime < 0.15f;
            if (!nowGrounded && recentlyGrounded && !jumpedNow && m_Velocity.y <= 0.5f && TrySnapToGround())
            {
                nowGrounded = true;
                SnapCount++;
            }
            if (!nowGrounded) m_FallSpeed = Mathf.Max(0f, -m_Velocity.y);
            if (nowGrounded && !m_WasGrounded)
            {
                Landed?.Invoke(m_FallSpeed);
                m_DipVelocity -= Mathf.Min(m_FallSpeed, 12f) * 0.35f; // camera sinks a little on landing
            }
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
            float targetHead = InHatch ? HatchEyeHeight : downed ? 0.35f : (crouch ? 0.95f : 1.6f);
            m_HeadHeight = Mathf.SmoothDamp(m_HeadHeight, targetHead, ref m_HeadVelocity, 0.09f, Mathf.Infinity, dt);

            // Head bob follows the distance travelled, so it matches the footsteps at any speed.
            float speedNow = HorizontalSpeed;
            m_BobPhase += speedNow * StepsPerMeter * Mathf.PI * dt;
            float targetWeight = GameSettings.HeadBob && nowGrounded && !downed ? Mathf.Clamp01(speedNow / WalkSpeed) : 0f;
            m_BobWeight = Mathf.MoveTowards(m_BobWeight, targetWeight, dt * 6f);
            float amplitude = BobAmplitude * (crouch ? 0.5f : IsSprinting ? 1.6f : 1f) * m_BobWeight;
            var bob = MovementMath.HeadBob(m_BobPhase, amplitude);
            m_Dip = MovementMath.SpringStep(m_Dip, ref m_DipVelocity, 0f, 2.2f, 0.7f, dt);
            m_Dip = Mathf.Clamp(m_Dip, -0.25f, 0.1f);
            head.localPosition = new Vector3(bob.x, m_HeadHeight + bob.y + m_Dip, 0);
            m_Trauma = Mathf.Max(0f, m_Trauma - dt * 1.6f);
            var shake = MovementMath.Shake(m_Trauma, Time.time, 6f);
            head.localRotation = Quaternion.Euler(m_Pitch + shake.x, HeadYaw + shake.y, (downed ? 25f : 0f) + shake.z);

            // Field of view: a subtle widening while actually running fast.
            bool fast = IsSprinting && HorizontalSpeed > WalkSpeed + 0.5f;
            float targetFov = GameSettings.Fov + (fast ? SprintFovKick : 0f) + FovBoost;
            m_Fov = Mathf.Lerp(m_Fov, targetFov, 1f - Mathf.Exp(-8f * dt));
            if (cam != null) cam.fieldOfView = m_Fov;
        }

        float m_StepOffset = -1f; // the controller's own (the prefab's)
        /// <summary>
        /// How high the controller may step on this move. A CharacterController lifts itself by its step before it goes
        /// sideways, so it needs that much headroom over its own height: crouched (1.1 m + 0.35) it was stopped by the edge
        /// over a hole in a wall it fits into (1.25 m). With a ceiling that low over the body or just ahead, it does not step.
        /// </summary>
        public static float StepFor(CharacterController cc, Vector3 move, float step)
        {
            var dir = new Vector3(move.x, 0f, move.z);
            if (dir.sqrMagnitude < 1e-8f || step <= LowStep) return step;
            dir.Normalize();
            var side = new Vector3(dir.z, 0f, -dir.x) * (cc.radius + 0.05f);
            var middle = cc.transform.TransformPoint(cc.center);
            float up = cc.height / 2f + step + cc.skinWidth + 0.02f;
            for (int ahead = 0; ahead < 3; ahead++)
                for (int s = -1; s <= 1; s++)
                {
                    var from = middle + dir * (ahead * (cc.radius + 0.05f)) + side * s;
                    // (a ray that starts inside a wall does not see it: only what is over a free spot counts)
                    if (Level.PassableForPlayers.Raycast(from, Vector3.up, out var hit, up, ~(1 << 2))
                        && hit.normal.y < -0.5f && !hit.collider.transform.IsChildOf(cc.transform)) return LowStep;
                }
            return step;
        }

        /// <summary>The step left under a low ceiling, metres.</summary>
        const float LowStep = 0.03f;

        /// <summary>Pulls the controller down onto walkable ground right below (stairs, curbs, slopes).</summary>
        bool TrySnapToGround()
        {
            const float lift = 0.1f;
            var origin = transform.position + Vector3.up * lift;
            if (!Level.PassableForPlayers.Raycast(origin, Vector3.down, out var hit, GroundSnapDistance + lift, ~(1 << 2)))
                return false;
            if (Vector3.Angle(hit.normal, Vector3.up) > m_Controller.slopeLimit + 1f) return false;
            float drop = hit.distance - lift;
            if (drop <= 0.001f) return false;
            m_Controller.Move(Vector3.down * (drop + m_Controller.skinWidth));
            m_Velocity.y = -2f;
            return m_Controller.isGrounded;
        }

        public int SnapCount { get; private set; }

        bool CanStandUp() =>
            !Level.PassableForPlayers.SphereCast(transform.position + Vector3.up * 0.5f, m_Controller.radius * 0.9f, Vector3.up, out _,
                PlayerController.StandHeight - 0.5f, ~(1 << 2));
    }
}
