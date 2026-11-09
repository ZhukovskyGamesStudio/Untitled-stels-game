using UnityEngine;

namespace Stealth.Player
{
    /// <summary>Tuning of sprint stamina (PlayerConfig).</summary>
    [System.Serializable]
    public class StaminaSettings
    {
        [Tooltip("Seconds of sprinting from full to empty.")]
        public float SprintDuration = 6f;
        [Tooltip("Seconds to refill from empty.")]
        public float RegenDuration = 4f;
        [Tooltip("Pause after sprinting before it starts to refill.")]
        public float RegenDelay = 1.2f;
        [Tooltip("When drained to zero you cannot sprint until it refills to this share.")]
        public float RecoverThreshold = 0.3f;
        [Tooltip("Share of the bar a jump costs.")]
        public float JumpCost = 0.08f;
    }

    /// <summary>Sprint stamina. Draining to zero exhausts the player until it recovers to <see cref="StaminaSettings.RecoverThreshold"/>.</summary>
    public class Stamina
    {
        readonly StaminaSettings m_S;

        public Stamina(StaminaSettings settings = null) => m_S = settings ?? new StaminaSettings();

        float SprintDuration => m_S.SprintDuration;
        float RegenDuration => m_S.RegenDuration;
        float RegenDelay => m_S.RegenDelay;
        float RecoverThreshold => m_S.RecoverThreshold;
        float JumpCost => m_S.JumpCost;

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
