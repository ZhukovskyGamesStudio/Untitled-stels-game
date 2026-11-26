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

    /// <summary>A kind of house the route offers only from a house on.</summary>
    [Serializable]
    public struct LateHouse
    {
        public Stealth.Level.HouseKind Kind;
        public int FromHouse;
        public LateHouse(Stealth.Level.HouseKind kind, int fromHouse) { Kind = kind; FromHouse = fromHouse; }

        /// <summary>The kind may come as house <paramref name="house"/> (0-based).</summary>
        public static bool Allowed(LateHouse[] late, Stealth.Level.HouseKind kind, int house)
        {
            if (late != null) foreach (var l in late) if (l.Kind == kind && house < l.FromHouse) return false;
            return true;
        }
    }

    /// <summary>Tuning of a run (RunConfig): quota, penalties, alarms, reinforcements.</summary>
    [Serializable]
    public class RunSettings
    {
        [Header("Quota")]
        [Tooltip("The fuel the drive on from each house asks for (the first house first): fixed numbers, whatever lies in the house.")]
        public int[] Quotas = { 240, 320, 420, 540, 680 };
        [Tooltip("Every house after the last of those asks for this much more than the one before it.")]
        public int QuotaGrowth = 160;
        [Tooltip("Every player after the first adds this share of the quota (0.5: two players × 1.5, four × 2.5).")]
        public float QuotaPerExtraPlayer = 0.5f;
        [Tooltip("A house always holds loot worth at least this many times its quota.")]
        public float LootPerQuota = 2f;
        [Tooltip("Quota increase per teammate left down at the end of the previous house.")]
        public float DeathPenalty = 0.15f;
        [Tooltip("What the drive costs by the way the team takes, as a share of the house's quota: every tunnel out of the sewer gets one of these (shuffled by the run's seed), so the ways on differ in fuel.")]
        public float[] RouteFuel = { 0.7f, 1f, 1.35f };

        [Header("Leaving a house")]
        [Tooltip("Seconds the car must stand in a tunnel with the whole team in it before it goes (the countdown on the HUD).")]
        public float VoteDelay = 3f;
        [Tooltip("Reinforcements: seconds from the house's start to the first extra robot coming in, then to the second, then between every one after.")]
        public float ReinforceFirst = 180f, ReinforceSecond = 120f, ReinforceEvery = 60f;
        [Tooltip("Those times are for a house of this floor area, m²: a bigger one is slower by as much (up to ReinforceSlowest times), never faster.")]
        public float ReinforceArea = 2500f;
        public float ReinforceSlowest = 3f;
        [Tooltip("No more extra robots than this in one house.")]
        public int ReinforceMax = 8;

        [Header("Houses that come only later in a run")]
        [Tooltip("From which house on (0 = the first) the route offers these kinds; the rest from the start.")]
        public LateHouse[] LateHouses = { new(Stealth.Level.HouseKind.Abandoned, 3) };
        [Tooltip("The shortest drive to the next house, seconds, before the car changes into the basement's approach (GameManager.Arrival, Level.DrivePath): the house is built meanwhile, a slow build makes it longer.")]
        public float DriveTime = 6.5f;
        [Tooltip("Seconds of warning before the run ends when the team cannot get the fuel for any way on.")]
        public float StrandedGrace = 12f;

        /// <summary>The reinforcements of a house of this floor area.</summary>
        public ReinforcementClock ReinforcementsFor(float area)
        {
            float slower = ReinforceArea > 0f ? Mathf.Clamp(area / ReinforceArea, 1f, Mathf.Max(1f, ReinforceSlowest)) : 1f;
            return new ReinforcementClock(ReinforceFirst * slower, ReinforceSecond * slower, ReinforceEvery * slower, ReinforceMax);
        }

        [Header("Alarms")]
        [Tooltip("Seconds an alarm lasts (red lights, robots come running).")]
        public float AlarmDuration = 6f;
        [Tooltip("Alarms closer together than this count once.")]
        public float AlarmCountSpacing = 4f;
        [Tooltip("Sensors are off for this long after a house starts.")]
        public float GracePeriod = 8f;
    }

    /// <summary>
    /// Rules of a run (pure, unit tested): the fuel of the car, what the drive on costs (the "quota" of a house), death
    /// penalties, alarms, how a level ends. The tank (<see cref="Collected"/>) keeps its fuel for the whole
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
        /// <summary>Players down at the end of the previous house: each one raises this house's quota.</summary>
        public int Penalties { get; private set; }

        double m_LastAlarmCounted = double.NegativeInfinity;

        /// <summary>The quota of a house (0-based): its fixed number, more for every player after the first and for every teammate left down in the house before.</summary>
        public static int QuotaFor(int house, int penalties, RunSettings s = null, int players = 1)
        {
            s ??= new RunSettings();
            int n = s.Quotas?.Length ?? 0, last = n > 0 ? s.Quotas[n - 1] : 0;
            house = Mathf.Max(0, house);
            int quota = house < n ? s.Quotas[house] : last + s.QuotaGrowth * (house - n + 1);
            float team = 1f + s.QuotaPerExtraPlayer * Mathf.Max(0, players - 1);
            return Mathf.Max(10, Mathf.RoundToInt(quota * team * (1f + s.DeathPenalty * Mathf.Max(0, penalties)) / 5f) * 5);
        }

        /// <summary>The least the loot of a house is worth: its quota so many times over (<see cref="RunSettings.LootPerQuota"/>).</summary>
        public int LootFor(int house, int players = 1) => Mathf.CeilToInt(QuotaFor(house, Penalties, m_S, players) * m_S.LootPerQuota);

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

        /// <summary>A new run begins in the sewer, before the first house: an empty tank, the first drive is free.</summary>
        public void BeginRun()
        {
            LevelIndex = RunMap.StartDepth;
            Quota = 0;
            Collected = 0;
            Penalties = 0;
            Phase = RunPhase.Sewer;
            AlarmUntil = 0;
            Alarms = 0;
        }

        /// <summary>A new house begins.</summary>
        public void BeginLevel(int index, int players = 1)
        {
            LevelIndex = index;
            Players = Mathf.Max(1, players);
            Quota = QuotaFor(index, Penalties, m_S, Players);
            Phase = RunPhase.Playing; // the tank keeps what is in it
            AlarmUntil = 0;
            Alarms = 0;
            m_LastAlarmCounted = double.NegativeInfinity;
        }

        public void Deposit(int value)
        {
            Collected += value;
            RunTotal += value;
        }

        public bool QuotaMet => Collected >= Quota;
        /// <summary>Players in the team now: the quota follows them.</summary>
        public int Players { get; private set; } = 1;

        /// <summary>The team grew or shrank during a house: its quota follows. Returns true if it changed.</summary>
        public bool SetPlayers(int players)
        {
            players = Mathf.Max(1, players);
            if (players == Players || Phase != RunPhase.Playing) return false;
            Players = players;
            Quota = QuotaFor(LevelIndex, Penalties, m_S, Players);
            return true;
        }

        public bool AlarmActive(double now) => now < AlarmUntil;

        /// <summary>An alarm goes off. Alarms within a few seconds of each other count once.</summary>
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
