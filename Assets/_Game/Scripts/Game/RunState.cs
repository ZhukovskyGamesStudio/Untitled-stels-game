using System;
using UnityEngine;

namespace Stealth.Game
{
    public enum RunPhase : byte { Playing, Failed }

    /// <summary>Which house the team is in: every peer generates the same building from it.</summary>
    public struct LevelInfo : IEquatable<LevelInfo>
    {
        public int Seed;
        public int Index;
        public bool Equals(LevelInfo other) => Seed == other.Seed && Index == other.Index;
    }

    /// <summary>
    /// Rules of a run (pure, unit tested): quota per house, deposits, death penalties, alarms and hunters,
    /// how a level ends. <see cref="GameManager"/> owns one on the server and replicates the public state.
    /// </summary>
    public sealed class RunState
    {
        public const double AlarmCountSpacing = 4.0;

        public float QuotaShare = 0.35f;
        public float QuotaShareGrowth = 0.04f;
        public float DeathPenalty = 0.15f;
        public float AlarmDuration = 6f;

        public int LevelIndex { get; private set; }
        public int Quota { get; private set; }
        public int Collected { get; private set; }
        public int RunTotal { get; private set; }
        public RunPhase Phase { get; private set; }
        public double AlarmUntil { get; private set; }
        public int Alarms { get; private set; }
        public int Hunters { get; private set; }
        /// <summary>Players down at the end of the previous house: each one raises this house's quota.</summary>
        public int Penalties { get; private set; }

        double m_LastAlarmCounted = double.NegativeInfinity;

        /// <summary>Quota for a level: a share of the loot value that grows with depth, plus death penalties.</summary>
        public static int QuotaFor(int totalValue, int levelIndex, int penalties, float share = 0.35f, float growth = 0.04f, float penalty = 0.15f)
        {
            float s = Mathf.Min(0.85f, share + growth * levelIndex);
            return Mathf.Max(10, Mathf.RoundToInt(totalValue * s * (1f + penalty * penalties) / 5f) * 5);
        }

        /// <summary>Does a hunter come now? After enough alarms, or when the team lingers too long.</summary>
        public static bool HunterDue(int alarms, float elapsed, int levelIndex, int huntersSoFar)
        {
            if (huntersSoFar >= 1 + levelIndex / 3) return false;
            int alarmLimit = (levelIndex == 0 ? 3 : 2) * (huntersSoFar + 1);
            float timeLimit = Mathf.Max(180f, 360f - levelIndex * 20f) * (huntersSoFar + 1);
            return alarms >= alarmLimit || elapsed >= timeLimit;
        }

        /// <summary>A new house begins with this much loot in it.</summary>
        public void BeginLevel(int index, int lootValue)
        {
            LevelIndex = index;
            Quota = QuotaFor(lootValue, index, Penalties, QuotaShare, QuotaShareGrowth, DeathPenalty);
            Collected = 0;
            Phase = RunPhase.Playing;
            AlarmUntil = 0;
            Alarms = 0;
            Hunters = 0;
            m_LastAlarmCounted = double.NegativeInfinity;
        }

        public void Deposit(int value)
        {
            Collected += value;
            RunTotal += value;
        }

        public bool QuotaMet => Collected >= Quota;

        public bool AlarmActive(double now) => now < AlarmUntil;

        /// <summary>An alarm goes off. Alarms within a few seconds of each other count once (towards the hunter).</summary>
        public void RaiseAlarm(double now)
        {
            if (now - m_LastAlarmCounted > AlarmCountSpacing)
            {
                Alarms++;
                m_LastAlarmCounted = now;
            }
            AlarmUntil = now + AlarmDuration;
        }

        /// <summary>End the current alarm at once (the red lights go off).</summary>
        public void CalmDown() => AlarmUntil = 0;

        public bool ShouldSendHunter(float elapsed) => Phase == RunPhase.Playing && HunterDue(Alarms, elapsed, LevelIndex, Hunters);

        public void HunterSent() => Hunters++;

        public enum Exit { Ignored, Failed, NextLevel }

        /// <summary>The team leaves through the hatch with <paramref name="playersDown"/> teammates down.</summary>
        public Exit Leave(int playersDown)
        {
            if (Phase != RunPhase.Playing) return Exit.Ignored;
            if (!QuotaMet)
            {
                Phase = RunPhase.Failed;
                return Exit.Failed;
            }
            Penalties = playersDown;
            return Exit.NextLevel;
        }

        /// <summary>Everybody is down: the run is over.</summary>
        public bool Wipe()
        {
            if (Phase != RunPhase.Playing) return false;
            Phase = RunPhase.Failed;
            return true;
        }

        /// <summary>A new run after a failed one. Returns false if the run is still going.</summary>
        public bool Restart()
        {
            if (Phase != RunPhase.Failed) return false;
            Penalties = 0;
            RunTotal = 0;
            return true;
        }

        /// <summary>Client: values replicated from the server.</summary>
        public void ApplyReplicated(int index, int quota, int collected, int runTotal, RunPhase phase, double alarmUntil)
        {
            LevelIndex = index;
            Quota = quota;
            Collected = collected;
            RunTotal = runTotal;
            Phase = phase;
            AlarmUntil = alarmUntil;
        }
    }
}
