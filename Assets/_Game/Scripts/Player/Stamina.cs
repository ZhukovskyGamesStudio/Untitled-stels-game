using UnityEngine;

namespace Stealth.Player
{
    /// <summary>Sprint stamina. Draining to zero exhausts the player until it recovers to <see cref="RecoverThreshold"/>.</summary>
    public class Stamina
    {
        public float SprintDuration = 6f;
        public float RegenDuration = 4f;
        public float RegenDelay = 1.2f;
        public float RecoverThreshold = 0.3f;
        public float JumpCost = 0.08f;

        public float Value { get; private set; } = 1f;
        public bool Exhausted { get; private set; }
        public bool CanSprint => !Exhausted && Value > 0f;

        float m_SinceUse = 10f;

        public void Tick(bool sprinting, float dt)
        {
            if (sprinting && CanSprint)
            {
                Value = Mathf.Max(0f, Value - dt / SprintDuration);
                m_SinceUse = 0f;
                if (Value <= 0f) Exhausted = true;
                return;
            }
            m_SinceUse += dt;
            if (m_SinceUse >= RegenDelay) Value = Mathf.Min(1f, Value + dt / RegenDuration);
            if (Exhausted && Value >= RecoverThreshold) Exhausted = false;
        }

        public void SpendJump()
        {
            Value = Mathf.Max(0f, Value - JumpCost);
            m_SinceUse = 0f;
        }

        public void Refill()
        {
            Value = 1f;
            Exhausted = false;
        }
    }
}
