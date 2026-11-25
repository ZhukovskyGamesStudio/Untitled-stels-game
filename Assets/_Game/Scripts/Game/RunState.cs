using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>In a house · the run is over · in the sewer between houses (sell, pay the quota, vote for the next house).</summary>
    public enum RunPhase : byte { Playing, Failed, Sewer }

    /// <summary>Which house the team is in: every peer generates the same building from it.</summary>
    public struct LevelInfo : IEquatable<LevelInfo>
    {
        public int Seed;
        public int Index;
        /// <summary>Team size the house was generated for: every peer must build the same house.</summary>
        public int Players;
        /// <summary>Row of the house in its column of the route (<see cref="RunMap"/>).</summary>
        public int Lane;
        /// <summary>The seed of the whole run: every peer draws the same route from it.</summary>
        public int RunSeed;
        public bool Equals(LevelInfo other) =>
            Seed == other.Seed && Index == other.Index && Players == other.Players && Lane == other.Lane && RunSeed == other.RunSeed;
    }

    /// <summary>Tuning of a run (RunConfig): quota, penalties, alarms, hunters.</summary>
    [Serializable]
    public class RunSettings
    {
        [Header("Quota")]
        [Tooltip("Share of all loot value in a house that the quota asks for (first house).")]
        public float QuotaShare = 0.35f;
        [Tooltip("The share grows by this much with every house.")]
        public float QuotaShareGrowth = 0.04f;
        [Tooltip("The share never goes above this.")]
        public float MaxQuotaShare = 0.85f;
        [Tooltip("Share multiplier by team size (1, 2, 3, 4… players; the last repeats). The bigger house of a bigger team already holds more loot, so this stays mild.")]
        public float[] QuotaByPlayers = { 1f, 1.1f, 1.18f, 1.25f };
        [Tooltip("Quota increase per teammate left down at the end of the previous house.")]
        public float DeathPenalty = 0.15f;
        [Tooltip("What the drive costs by the way the team takes, as a share of the house's quota: every tunnel out of the sewer gets one of these (shuffled by the run's seed), so the ways on differ in fuel.")]
        public float[] RouteFuel = { 0.7f, 1f, 1.35f };

        [Header("Leaving a house")]
        [Tooltip("Seconds the car must stand in a tunnel with the whole team in it before it goes (the countdown on the HUD).")]
        public float VoteDelay = 3f;
        [Tooltip("Reinforcements: seconds from the house's start to the first extra robot coming in, then to the second, then between every one after (forever).")]
        public float ReinforceFirst = 180f, ReinforceSecond = 120f, ReinforceEvery = 60f;
        [Tooltip("The shortest drive to the next house, seconds, before the car changes into the basement's approach (GameManager.Arrival, Level.DrivePath): the house is built meanwhile, a slow build makes it longer.")]
        public float DriveTime = 6.5f;
        [Tooltip("Seconds of warning before the run ends when the team cannot get the fuel for any way on.")]
        public float StrandedGrace = 12f;

        [Header("Alarms")]
        [Tooltip("Seconds an alarm lasts (red lights, robots come running).")]
        public float AlarmDuration = 6f;
        [Tooltip("Alarms closer together than this count once towards the hunter.")]
        public float AlarmCountSpacing = 4f;
        [Tooltip("Sensors are off for this long after a house starts.")]
        public float GracePeriod = 8f;

        [Header("Hunter")]
        [Tooltip("The hunter at all: off = it never comes and there is no countdown on the screen (a quiet test level).")]
        public bool Hunter = true;
        [Tooltip("Alarms that bring the hunter in the first house.")]
        public int HunterAlarmsFirstHouse = 3;
        [Tooltip("Alarms that bring the hunter in later houses.")]
        public int HunterAlarmsLater = 2;
        [Tooltip("Seconds in a house after which the hunter comes anyway (first house).")]
        public float HunterTime = 360f;
        [Tooltip("The time limit shrinks by this much per house…")]
        public float HunterTimeStepPerHouse = 20f;
        [Tooltip("…but never below this.")]
        public float HunterMinTime = 180f;
        [Tooltip("One more hunter is allowed every this many houses.")]
        public int HousesPerExtraHunter = 3;
    }

    /// <summary>
    /// Rules of a run (pure, unit tested): the fuel of the car, what the drive on costs (the "quota" of a house), death
    /// penalties, alarms and hunters, how a level ends. The tank (<see cref="Collected"/>) keeps its fuel for the whole
    /// run: loot is fed into the engine, every drive to the next house burns <see cref="Quota"/> of it.
    /// <see cref="GameManager"/> owns one on the server and replicates the public state.
    /// </summary>
    public sealed class RunState
    {
        readonly RunSettings m_S;

        public RunState(RunSettings settings = null) => m_S = settings ?? new RunSettings();

        public RunSettings Settings => m_S;

        public int LevelIndex { get; private set; }
        /// <summary>Fuel the drive from this house to the next one burns (the house's quota).</summary>
        public int Quota { get; private set; }
        /// <summary>Fuel in the car's tank (kept between houses).</summary>
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
        public static int QuotaFor(int totalValue, int levelIndex, int penalties, RunSettings s = null, int players = 1)
        {
            s ??= new RunSettings();
            var byTeam = s.QuotaByPlayers;
            float team = byTeam == null || byTeam.Length == 0 ? 1f : byTeam[Mathf.Clamp(players - 1, 0, byTeam.Length - 1)];
            float share = Mathf.Min(s.MaxQuotaShare, (s.QuotaShare + s.QuotaShareGrowth * levelIndex) * team);
            return Mathf.Max(10, Mathf.RoundToInt(totalValue * share * (1f + s.DeathPenalty * penalties) / 5f) * 5);
        }

        /// <summary>The fuel a drive costs by a way whose share of the quota is <paramref name="share"/> (none while the quota is none: the first drive).</summary>
        public static int RouteCost(int quota, float share) => quota <= 0 ? 0 : Mathf.Max(5, Mathf.RoundToInt(quota * share / 5f) * 5);

        /// <summary>
        /// The share of the quota the drive to the house in row <paramref name="lane"/> of column <paramref name="depth"/>
        /// costs: one of <paramref name="shares"/>, shuffled per column by the run's seed — the ways out of one sewer all
        /// differ (as long as there are as many shares as ways).
        /// </summary>
        public static float RouteShare(int runSeed, int depth, int lane, float[] shares)
        {
            if (shares == null || shares.Length == 0) return 1f;
            var order = new int[shares.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            var rng = new System.Random(Stealth.Level.LevelRandom.Mix(runSeed, Stealth.Level.Salt.RouteFuel, depth));
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return shares[order[((lane % order.Length) + order.Length) % order.Length]];
        }

        /// <summary>Does a hunter come now? After enough alarms, or when the team lingers too long.</summary>
        public static bool HunterDue(int alarms, float elapsed, int levelIndex, int huntersSoFar, RunSettings s = null)
        {
            var (seconds, alarmsLeft) = HunterForecast(alarms, elapsed, levelIndex, huntersSoFar, s);
            return alarmsLeft == 0 || seconds == 0f;
        }

        /// <summary>
        /// When the next hunter comes: seconds left until it comes anyway and alarms left that would bring it sooner
        /// (both -1 when no more hunters come to this house). Shown to the players, so it is never a surprise.
        /// </summary>
        public static (float seconds, int alarms) HunterForecast(int alarms, float elapsed, int levelIndex, int huntersSoFar, RunSettings s = null)
        {
            s ??= new RunSettings();
            if (!s.Hunter || huntersSoFar >= 1 + levelIndex / Mathf.Max(1, s.HousesPerExtraHunter)) return (-1f, -1);
            int alarmLimit = (levelIndex == 0 ? s.HunterAlarmsFirstHouse : s.HunterAlarmsLater) * (huntersSoFar + 1);
            float timeLimit = Mathf.Max(s.HunterMinTime, s.HunterTime - levelIndex * s.HunterTimeStepPerHouse) * (huntersSoFar + 1);
            return (Mathf.Max(0f, timeLimit - elapsed), Mathf.Max(0, alarmLimit - alarms));
        }

        /// <summary>This house's forecast of the next hunter (see <see cref="HunterForecast(int,float,int,int,RunSettings)"/>).</summary>
        public (float seconds, int alarms) HunterForecast(float elapsed) =>
            Phase == RunPhase.Playing ? HunterForecast(Alarms, elapsed, LevelIndex, Hunters, m_S) : (-1f, -1);

        /// <summary>A new run begins in the sewer, before the first house: an empty tank, the first drive is free.</summary>
        public void BeginRun()
        {
            LevelIndex = RunMap.StartDepth;
            LootValue = 0;
            Quota = 0;
            Collected = 0;
            Penalties = 0;
            Phase = RunPhase.Sewer;
            AlarmUntil = 0;
            Alarms = 0;
            Hunters = 0;
        }

        /// <summary>A new house begins with this much loot in it.</summary>
        public void BeginLevel(int index, int lootValue, int players = 1)
        {
            LevelIndex = index;
            LootValue = lootValue;
            Players = Mathf.Max(1, players);
            Quota = QuotaFor(lootValue, index, Penalties, m_S, Players);
            Phase = RunPhase.Playing; // the tank keeps what is in it
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

        /// <summary>Loot value of the current house (the quota is a share of it).</summary>
        public int LootValue { get; private set; }
        /// <summary>Players in the team now: the quota follows them (friends join a house that is already running).</summary>
        public int Players { get; private set; } = 1;

        /// <summary>The team grew or shrank during a house: its quota follows. Returns true if it changed.</summary>
        public bool SetPlayers(int players)
        {
            players = Mathf.Max(1, players);
            if (players == Players || Phase != RunPhase.Playing) return false;
            Players = players;
            Quota = QuotaFor(LootValue, LevelIndex, Penalties, m_S, Players);
            return true;
        }

        public bool AlarmActive(double now) => now < AlarmUntil;

        /// <summary>An alarm goes off. Alarms within a few seconds of each other count once (towards the hunter).</summary>
        public void RaiseAlarm(double now)
        {
            if (now - m_LastAlarmCounted > m_S.AlarmCountSpacing)
            {
                Alarms++;
                m_LastAlarmCounted = now;
            }
            AlarmUntil = now + m_S.AlarmDuration;
        }

        /// <summary>End the current alarm at once (the red lights go off).</summary>
        public void CalmDown() => AlarmUntil = 0;

        public bool ShouldSendHunter(float elapsed) => Phase == RunPhase.Playing && HunterDue(Alarms, elapsed, LevelIndex, Hunters, m_S);

        public void HunterSent() => Hunters++;

        public enum Exit { Ignored, Failed, NextLevel }

        /// <summary>
        /// The car drives off from under the house, leaving <paramref name="playersDown"/> knocked-out teammates behind (each
        /// one raises the next quota): the house is left.
        /// </summary>
        public bool EnterSewer(int playersDown)
        {
            if (Phase != RunPhase.Playing) return false;
            Penalties = playersDown;
            Phase = RunPhase.Sewer;
            AlarmUntil = 0;
            return true;
        }

        /// <summary>The team drives on to the next house: the drive burns <paramref name="cost"/> of the fuel (the way's, <see cref="RouteCost"/>; not enough — it does not go).</summary>
        public Exit Advance(int cost)
        {
            if (Phase != RunPhase.Sewer || Collected < cost) return Exit.Ignored;
            Collected -= cost;
            return Exit.NextLevel;
        }

        /// <summary>Not enough fuel for any way on and nothing left to feed the engine (in the house above either): the run ends in the sewer.</summary>
        public bool Strand()
        {
            if (Phase is not (RunPhase.Sewer or RunPhase.Playing)) return false;
            Phase = RunPhase.Failed;
            return true;
        }

        /// <summary>Can the team still get the fuel: what is in the tank plus everything it could still feed into the engine.</summary>
        public static bool CanStillGo(int fuel, int cost, int burnable) => fuel + burnable >= cost;

        /// <summary>Can the team trade (sell, use the stash) now: in a house at the hatch, or in the sewer.</summary>
        public bool CanTrade => Phase == RunPhase.Playing || Phase == RunPhase.Sewer;

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
            Collected = 0;
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
