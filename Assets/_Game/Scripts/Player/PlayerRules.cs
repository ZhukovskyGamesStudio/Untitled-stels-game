using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// Server-side health rules (pure, unit tested): 2 HP, the second hit downs you, a short invulnerability
    /// after every hit so that one robot cannot finish you in the same second.
    /// </summary>
    public struct HealthState
    {
        public const int MaxHp = 2;
        public const float HitGrace = 1.5f;
        public const float ReviveGrace = 2f;
        public const float LevelStartGrace = 3f;

        public int Hp;
        public bool Downed;
        public float InvulnerableUntil;

        public static HealthState Full(float now) => new() { Hp = MaxHp, InvulnerableUntil = now + LevelStartGrace };

        /// <summary>Apply one hit. Returns false if it was ignored (downed or invulnerable).</summary>
        public bool TryHit(float now)
        {
            if (Downed || now < InvulnerableUntil) return false;
            InvulnerableUntil = now + HitGrace;
            Hp = Mathf.Max(0, Hp - 1);
            if (Hp == 0) Downed = true;
            return true;
        }

        /// <summary>Knocked out at once (laughing gas).</summary>
        public void Knockout()
        {
            Hp = 0;
            Downed = true;
        }

        public void Revive(float now)
        {
            Downed = false;
            Hp = 1;
            InvulnerableUntil = now + ReviveGrace;
        }
    }

    /// <summary>
    /// Laughing gas outside the building (pure, unit tested): after a while you start laughing (noise every
    /// <see cref="LaughInterval"/> s), at <see cref="Limit"/> you pass out. Inside, the gas wears off three times faster.
    /// </summary>
    public sealed class GasExposure
    {
        public const float Limit = 25f;
        public const float LaughAfter = 8f;
        public const float LaughInterval = 1.2f;
        public const float RecoveryRate = 3f;

        public float Time { get; private set; }
        public bool Laughing => Time > LaughAfter;
        public float Share => Mathf.Clamp01(Time / Limit);
        public float SecondsLeft => Mathf.Max(0f, Limit - Time);

        float m_LaughTimer;

        public enum Effect { None, Laugh, Knockout }

        public Effect Tick(bool outside, float dt)
        {
            Time = outside ? Time + dt : Mathf.Max(0f, Time - dt * RecoveryRate);
            if (outside && Time >= Limit) return Effect.Knockout;
            if (!outside || !Laughing) return Effect.None;
            m_LaughTimer -= dt;
            if (m_LaughTimer > 0f) return Effect.None;
            m_LaughTimer = LaughInterval;
            return Effect.Laugh;
        }

        public void Reset()
        {
            Time = 0f;
            m_LaughTimer = 0f;
        }
    }

    /// <summary>
    /// Footstep noise (pure, unit tested): how often a step is heard and how far, by gait.
    /// Crouching is nearly silent, sprinting is loud. Standing still or in the air makes no steps.
    /// </summary>
    public sealed class FootstepNoise
    {
        public float WalkRadius = 6f;
        public float SprintRadius = 13f;
        public float CrouchRadius = 2f;

        public const float MinSpeed = 0.6f;

        float m_Timer;

        public static float Interval(bool crouching, bool sprinting) => crouching ? 0.6f : sprinting ? 0.32f : 0.45f;

        public float Radius(bool crouching, bool sprinting) => crouching ? CrouchRadius : sprinting ? SprintRadius : WalkRadius;

        /// <summary>Returns the radius of a step made this frame, or 0.</summary>
        public float Tick(float speed, bool grounded, bool crouching, bool sprinting, float dt)
        {
            if (speed < MinSpeed || !grounded)
            {
                m_Timer = 0f;
                return 0f;
            }
            m_Timer += dt;
            if (m_Timer < Interval(crouching, sprinting)) return 0f;
            m_Timer = 0f;
            return Radius(crouching, sprinting);
        }

        /// <summary>Landing noise: a normal jump (~4 m/s) is audible nearby, falling from a floor above is loud.</summary>
        public static float LandingRadius(float impactSpeed) =>
            impactSpeed < 3.5f ? 0f : Mathf.Lerp(4f, 14f, Mathf.InverseLerp(3.5f, 10f, impactSpeed));
    }
}
