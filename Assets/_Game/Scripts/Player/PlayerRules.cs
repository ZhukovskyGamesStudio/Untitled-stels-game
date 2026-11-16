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
        [Tooltip("Knocked down with HP left: seconds on the floor before you can get up yourself.")]
        public float GetUpDelay = 1.2f;
        [Tooltip("Invulnerability after getting up.")]
        public float GetUpGrace = 1f;
    }

    /// <summary>
    /// Server-side health rules (pure, unit tested): 2 HP. Every hit knocks you down — robots leave a lying player alone.
    /// With HP left you get up yourself (after <see cref="HealthSettings.GetUpDelay"/>); at 0 HP a teammate must lift you.
    /// A short invulnerability after every hit so that one robot cannot finish you in the same second.
    /// </summary>
    public struct HealthState
    {
        public const int MaxHp = 2;

        public int Hp;
        /// <summary>Lying on the floor (after any hit).</summary>
        public bool Downed;
        public float InvulnerableUntil;
        public float DownedAt;
        HealthSettings m_S;

        HealthSettings S => m_S ??= new HealthSettings();

        public static HealthState Full(float now, HealthSettings settings = null)
        {
            var h = new HealthState { Hp = MaxHp, m_S = settings };
            h.InvulnerableUntil = now + h.S.LevelStartGrace;
            return h;
        }

        /// <summary>Knocked out: lying with no HP, only a teammate can help.</summary>
        public bool KnockedOut => Downed && Hp == 0;

        /// <summary>Apply one hit: lose a point and fall. Returns false if it was ignored (already down or invulnerable).</summary>
        public bool TryHit(float now)
        {
            if (Downed || now < InvulnerableUntil) return false;
            InvulnerableUntil = now + S.HitGrace;
            Hp = Mathf.Max(0, Hp - 1);
            Downed = true;
            DownedAt = now;
            return true;
        }

        /// <summary>Can get up by yourself: down, with HP left, after a moment on the floor.</summary>
        public bool CanGetUp(float now) => Downed && Hp > 0 && now >= DownedAt + S.GetUpDelay;

        public bool GetUp(float now)
        {
            if (!CanGetUp(now)) return false;
            Downed = false;
            InvulnerableUntil = now + S.GetUpGrace;
            return true;
        }

        /// <summary>Knocked out at once (laughing gas).</summary>
        public void Knockout(float now = 0f)
        {
            Hp = 0;
            Downed = true;
            DownedAt = now;
        }

        /// <summary>The med table: up to <paramref name="hp"/> points (more than the usual maximum); not while lying.</summary>
        public void Heal(int hp)
        {
            if (!Downed) Hp = Mathf.Max(Hp, hp);
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
        [Tooltip("How far from the eyes an object can be used: you must look right at it (doors, safes, terminals, " +
                 "drawers and lockers, robots and devices to hack), metres.")]
        public float LookReach = 2.2f;
        [Tooltip("How thick the look ray is: how precisely you must aim at the object, metres.")]
        public float LookRadius = 0.06f;
        [Tooltip("Devices without a body to hit (cameras, motion sensors, plates): how far off the crosshair they may be, degrees.")]
        public float LookAngle = 10f;
        public float ReviveTime = 2f;
        public float ReviveDistance = 2.2f;
        [Tooltip("Seconds to hold E to hack.")]
        public float HackHoldTime = 1.75f;
        [Tooltip("Seconds a hacked robot / device stays off; 0 = for good (until a hack over the limit wakes it, MaxHacked).")]
        public float HackDuration = 0f;
        [Tooltip("Robots and devices the team can keep switched off at once: hacking one more wakes the first.")]
        public int MaxHacked = 2;
        [Tooltip("How close you must be to hack, meters.")]
        public float HackReach = 1.9f;
        [Tooltip("Seconds to hold E to hack the lock of a door.")]
        public float DoorHackTime = 3f;
        [Tooltip("Seconds between two uses of \"I'm stuck\" (it moves you to the nearest free spot).")]
        public float UnstuckCooldown = 8f;
        [Tooltip("Hacks a hacking terminal can make per house.")]
        public int TerminalHacks = 3;
        [Tooltip("Seconds to hold E at a hacking terminal to hack the robot on its screen.")]
        public float TerminalHackTime = 0.5f;
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

    /// <summary>Small rules of the player's body (pure, unit tested).</summary>
    public static class PlayerRules
    {
        /// <summary>How far (degrees) a leg swings forward and back at this speed: wider when running, short crouched.</summary>
        public static float LegSwing(float speed, bool crouching) =>
            (crouching ? 0.6f : 1f) * Mathf.Lerp(14f, 34f, Mathf.InverseLerp(1.5f, 6f, speed));

        /// <summary>
        /// Share of a flashlight's intensity with a surface this close in front of it: light falls off with the square of
        /// the distance, so held against a drawer the spot burned white and bloomed over the screen; closer than
        /// <paramref name="near"/> the lamp dims by the same square, the spot stays as bright as at that distance.
        /// </summary>
        public static float FlashlightNear(float distance, float near) =>
            near <= 0f ? 1f : Mathf.Clamp(distance * distance / (near * near), 0.01f, 1f);
    }
}
