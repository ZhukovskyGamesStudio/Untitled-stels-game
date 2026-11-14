using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>How the team's pickup drives (RunConfig → Car).</summary>
    [System.Serializable]
    public class CarSettings
    {
        [Header("Speed")]
        [Tooltip("Top speed forward, m/s.")]
        public float MaxSpeed = 10f;
        [Tooltip("Top speed in reverse, m/s.")]
        public float MaxReverse = 4f;
        [Tooltip("Acceleration with the pedal down, m/s².")]
        public float Acceleration = 5.5f;
        [Tooltip("Braking (pedal the other way or Space), m/s².")]
        public float Braking = 14f;
        [Tooltip("Slowing down by itself with no pedal, m/s².")]
        public float Coasting = 2.2f;

        [Header("Steering")]
        [Tooltip("Front wheels turn at most this far at low speed, degrees.")]
        public float MaxSteer = 34f;
        [Tooltip("…and this far at top speed (steadier when fast).")]
        public float MaxSteerAtSpeed = 14f;
        [Tooltip("How fast the wheels turn to the wanted angle, degrees per second.")]
        public float SteerSpeed = 140f;
        [Tooltip("Distance between the axles, m (with the steering angle it gives the turning circle).")]
        public float Wheelbase = 3.0f;

        [Header("Feel")]
        [Tooltip("Bounce of the body on the grate, metres (the side view of the drive shows it).")]
        public float Bounce = 0.025f;
        [Tooltip("Pitch of the body when speeding up / braking, degrees per m/s².")]
        public float PitchPerAccel = 0.35f;
        [Tooltip("Roll of the body in a turn, degrees at full lock and top speed.")]
        public float Roll = 3.5f;

        [Header("Layout")]
        [Tooltip("Steering wheel on the left (driver's seat on the left, passenger on the right). Off — a right-hand car.")]
        public bool LeftHandDrive = true;
    }

    /// <summary>What the driver does this frame.</summary>
    public struct CarInput
    {
        /// <summary>-1 back … 1 forward.</summary>
        public float Throttle;
        /// <summary>-1 left … 1 right.</summary>
        public float Steer;
        public bool Brake;
    }

    /// <summary>
    /// The pickup's driving (pure, unit tested): a bicycle model on the ground plane — speed along the body, front wheels
    /// that turn towards the wanted angle (less at speed), a turning rate of speed / wheelbase · tan(steer), pedal the other
    /// way to brake first and then reverse. The component moves the body by <see cref="Step"/>'s result and stops it at walls.
    /// </summary>
    public sealed class CarPhysics
    {
        readonly CarSettings m_S;

        public CarPhysics(CarSettings settings = null) => m_S = settings ?? new CarSettings();

        public CarSettings Settings => m_S;
        /// <summary>Speed along the body, m/s (negative: reversing).</summary>
        public float Speed { get; set; }
        /// <summary>Angle of the front wheels, degrees (+ = right).</summary>
        public float SteerAngle { get; private set; }
        /// <summary>Change of speed this step, m/s² (for the body's pitch).</summary>
        public float Accel { get; private set; }

        /// <summary>How far the wheels may turn at this speed.</summary>
        public float SteerLimit(float speed) =>
            Mathf.Lerp(m_S.MaxSteer, m_S.MaxSteerAtSpeed, Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.1f, m_S.MaxSpeed)));

        /// <summary>One step: returns how far the car moves along its forward (metres) and how much it turns (degrees).</summary>
        public (float forward, float yaw) Step(CarInput input, float dt)
        {
            float before = Speed;
            float throttle = Mathf.Clamp(input.Throttle, -1f, 1f);
            if (input.Brake) Speed = Mathf.MoveTowards(Speed, 0f, m_S.Braking * dt);
            else if (Mathf.Abs(throttle) < 0.01f) Speed = Mathf.MoveTowards(Speed, 0f, m_S.Coasting * dt);
            else if (Speed * throttle < -0.05f) Speed = Mathf.MoveTowards(Speed, 0f, m_S.Braking * dt); // the other way: brake first
            else
            {
                float top = throttle > 0f ? m_S.MaxSpeed : m_S.MaxReverse;
                Speed = Mathf.MoveTowards(Speed, throttle * top, m_S.Acceleration * Mathf.Abs(throttle) * dt);
            }
            Accel = dt > 0f ? (Speed - before) / dt : 0f;

            float limit = SteerLimit(Speed);
            SteerAngle = Mathf.MoveTowards(SteerAngle, Mathf.Clamp(input.Steer, -1f, 1f) * limit, m_S.SteerSpeed * dt);
            float distance = Speed * dt;
            float yaw = distance / Mathf.Max(0.5f, m_S.Wheelbase) * Mathf.Tan(SteerAngle * Mathf.Deg2Rad) * Mathf.Rad2Deg;
            return (distance, yaw);
        }

        /// <summary>The car hit something: it stops with a little knock back.</summary>
        public void Bump() => Speed = -Speed * 0.15f;

        /// <summary>The smallest turning circle (radius, metres) at walking pace.</summary>
        public float TurningRadius => m_S.Wheelbase / Mathf.Tan(m_S.MaxSteer * Mathf.Deg2Rad);
    }
}
