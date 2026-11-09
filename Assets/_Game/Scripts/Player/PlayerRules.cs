using UnityEngine;

namespace Stealth.Player
{
    /// <summary>Tuning of player health (PlayerConfig). The number of hit points is fixed by the HUD (two pips).</summary>
    [System.Serializable]
    public class HealthSettings
    {
        [Tooltip("Invulnerability after a hit: a moment to run.")]
        public float HitGrace = 1.5f;
        [Tooltip("Invulnerability after being revived.")]
        public float ReviveGrace = 2f;
        [Tooltip("Invulnerability when a house starts.")]
        public float LevelStartGrace = 3f;
    }

    /// <summary>
    /// Server-side health rules (pure, unit tested): 2 HP, the second hit downs you, a short invulnerability
    /// after every hit so that one robot cannot finish you in the same second.
    /// </summary>
    public struct HealthState
    {
        public const int MaxHp = 2;

        public int Hp;
        public bool Downed;
        public float InvulnerableUntil;
        HealthSettings m_S;

        HealthSettings S => m_S ??= new HealthSettings();

        public static HealthState Full(float now, HealthSettings settings = null)
        {
            var h = new HealthState { Hp = MaxHp, m_S = settings };
            h.InvulnerableUntil = now + h.S.LevelStartGrace;
            return h;
        }

        /// <summary>Apply one hit. Returns false if it was ignored (downed or invulnerable).</summary>
        public bool TryHit(float now)
        {
            if (Downed || now < InvulnerableUntil) return false;
            InvulnerableUntil = now + S.HitGrace;
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
            InvulnerableUntil = now + S.ReviveGrace;
        }
    }

    /// <summary>Tuning of the laughing gas outside (PlayerConfig).</summary>
    [System.Serializable]
    public class GasSettings
    {
        [Tooltip("Seconds outside until you pass out.")]
        public float Limit = 25f;
        [Tooltip("Seconds outside until you start laughing (noise).")]
        public float LaughAfter = 8f;
        public float LaughInterval = 1.2f;
        [Tooltip("How much faster the gas wears off inside than it builds up outside.")]
        public float RecoveryRate = 3f;
        [Tooltip("Noise radius of a laugh, meters.")]
        public float LaughNoise = 9f;
    }

    /// <summary>
    /// Laughing gas outside the building (pure, unit tested): after a while you start laughing (noise every
    /// <see cref="GasSettings.LaughInterval"/> s), at <see cref="GasSettings.Limit"/> you pass out. Inside, it wears off faster.
    /// </summary>
    public sealed class GasExposure
    {
        readonly GasSettings m_S;

        public GasExposure(GasSettings settings = null) => m_S = settings ?? new GasSettings();

        public GasSettings Settings => m_S;
        public float Limit => m_S.Limit;
        public float LaughAfter => m_S.LaughAfter;
        public float LaughInterval => m_S.LaughInterval;
        float RecoveryRate => m_S.RecoveryRate;

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

    /// <summary>Tuning of footstep noise (PlayerConfig): how far robots with microphones hear you.</summary>
    [System.Serializable]
    public class NoiseSettings
    {
        public float WalkRadius = 6f;
        public float SprintRadius = 13f;
        public float CrouchRadius = 2f;
    }

    /// <summary>Tuning of holding E: reviving teammates and hacking robots (PlayerConfig).</summary>
    [System.Serializable]
    public class InteractionSettings
    {
        public float ReviveTime = 2f;
        public float ReviveDistance = 2.2f;
        [Tooltip("Seconds to hold E to hack.")]
        public float HackHoldTime = 2.5f;
        [Tooltip("Seconds a hacked robot / device stays off.")]
        public float HackDuration = 20f;
        [Tooltip("How close you must be to hack, meters.")]
        public float HackReach = 1.9f;
    }

    /// <summary>
    /// Footstep noise (pure, unit tested): how often a step is heard and how far, by gait.
    /// Crouching is nearly silent, sprinting is loud. Standing still or in the air makes no steps.
    /// </summary>
    public sealed class FootstepNoise
    {
        readonly NoiseSettings m_S;

        public FootstepNoise(NoiseSettings settings = null) => m_S = settings ?? new NoiseSettings();

        public float WalkRadius => m_S.WalkRadius;
        public float SprintRadius => m_S.SprintRadius;
        public float CrouchRadius => m_S.CrouchRadius;

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
