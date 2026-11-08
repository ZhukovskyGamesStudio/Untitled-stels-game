using UnityEngine;

namespace Stealth.Player
{
    /// <summary>Pure movement helpers (unit tested). All velocities are horizontal unless stated otherwise.</summary>
    public static class MovementMath
    {
        /// <summary>
        /// Moves the horizontal velocity towards <paramref name="wishDir"/> * <paramref name="wishSpeed"/>.
        /// Uses <paramref name="accel"/> while speeding up / turning and <paramref name="decel"/> when there is no input
        /// or when moving faster than wished (e.g. after releasing sprint).
        /// </summary>
        public static Vector3 Accelerate(Vector3 velocity, Vector3 wishDir, float wishSpeed, float accel, float decel, float dt)
        {
            velocity.y = 0f;
            var target = wishDir.sqrMagnitude > 1e-4f ? wishDir.normalized * wishSpeed : Vector3.zero;
            bool slowingDown = target.sqrMagnitude < 1e-4f || velocity.magnitude > wishSpeed + 0.01f;
            float rate = slowingDown ? decel : accel;
            return Vector3.MoveTowards(velocity, target, rate * dt);
        }

        /// <summary>Time needed to reach <paramref name="fraction"/> of the target speed from rest.</summary>
        public static float TimeToReach(float speed, float accel, float fraction = 0.9f) => speed * fraction / accel;

        /// <summary>Coyote time + input buffering: jump if we were grounded recently and the press is still queued.</summary>
        public static bool CanJump(float now, float lastGroundedTime, float jumpQueuedUntil, float coyoteTime) =>
            now - lastGroundedTime <= coyoteTime && now <= jumpQueuedUntil;

        /// <summary>Initial vertical speed for a jump of the given height.</summary>
        public static float JumpVelocity(float height, float gravity) => Mathf.Sqrt(2f * Mathf.Abs(gravity) * height);
    }
}
