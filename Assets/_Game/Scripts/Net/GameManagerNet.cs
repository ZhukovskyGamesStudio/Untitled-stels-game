using System;
using System.Collections.Generic;
using Stealth.Game;
using Stealth.Player;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Net
{
    /// <summary>Wire format of <see cref="LevelInfo"/>.</summary>
    public struct NetLevelInfo : INetworkSerializeByMemcpy, IEquatable<NetLevelInfo>
    {
        public int Seed;
        public int Index;
        public byte Players;
        public byte Lane;
        public int RunSeed;
        public bool Equals(NetLevelInfo o) => Seed == o.Seed && Index == o.Index && Players == o.Players && Lane == o.Lane && RunSeed == o.RunSeed;
    }

    /// <summary>Wire format of a vote for the next house.</summary>
    public struct NetVote : INetworkSerializeByMemcpy, IEquatable<NetVote>
    {
        public ulong Client;
        public byte Lane;
        public bool Equals(NetVote o) => Client == o.Client && Lane == o.Lane;
    }

    /// <summary>The lift's state in one variable (its parts never arrive one without the others).</summary>
    public struct NetLift : INetworkSerializeByMemcpy, IEquatable<NetLift>
    {
        public sbyte From, To;
        public double Start;
        public bool Equals(NetLift o) => From == o.From && To == o.To && Start.Equals(o.Start);
        public static NetLift Of(Stealth.Level.LiftState s) => new() { From = (sbyte)s.From, To = (sbyte)s.To, Start = s.Start };
        public Stealth.Level.LiftState State => new() { From = From, To = To, Start = Start };
    }

    /// <summary>
    /// Network twin of <see cref="GameManager"/>: replicates the current house (seed → every peer builds the level)
    /// and the run state (quota, deposits, phase, alarm), carries "next house" / "new run" requests to the server
    /// and banners to everyone.
    /// </summary>
    public class GameManagerNet : NetworkBehaviour, IGameLink, INetSync
    {
        readonly NetworkVariable<NetLevelInfo> m_Level = new();
        readonly NetworkVariable<int> m_Quota = new();
        readonly NetworkVariable<int> m_Collected = new();
        readonly NetworkVariable<int> m_RunTotal = new();
        readonly NetworkVariable<RunPhase> m_Phase = new();
        readonly NetworkVariable<double> m_AlarmUntil = new();
        readonly NetworkVariable<double> m_LeaveAt = new();
        readonly NetworkVariable<double> m_GoAt = new();
        readonly NetworkVariable<sbyte> m_Chosen = new(-1);
        readonly NetworkVariable<sbyte> m_Style = new(-1);
        NetworkList<int> m_Opened;
        NetworkList<NetVote> m_Votes;
        int m_PushedOpened = -1, m_PushedVotes = -1;
        readonly List<KeyValuePair<ulong, int>> m_VoteBuffer = new();
        NetworkList<NetInventoryEntry> m_Stash, m_ExitStash, m_Bench;
        /// <summary>The car's workbench: the recipes made this run (bits) and the last press of «combine».</summary>
        readonly NetworkVariable<int> m_Known = new(), m_LastCombine = new();
        int m_PushedBench = -1;
        NetworkList<ulong> m_Cargo, m_Shred;
        readonly NetworkVariable<double> m_ShredStart = new();
        int m_PushedShred = -1;
        readonly NetworkVariable<double> m_DriveUntil = new(), m_ArriveAt = new(), m_StunnedUntil = new(), m_DepartAt = new();
        readonly NetworkVariable<Vector4> m_DriveFrom = new();
        readonly NetworkVariable<double> m_HouseClock = new();
        readonly NetworkVariable<bool> m_Picking = new();
        readonly NetworkVariable<uint> m_Upgrades = new();
        readonly NetworkVariable<byte> m_TeleportCharge = new();
        readonly NetworkVariable<sbyte> m_Cassette = new(-1);
        readonly NetworkVariable<sbyte> m_DriveBranch = new(-1);
        readonly NetworkVariable<NetLift> m_Lift = new(NetLift.Of(Stealth.Level.LiftState.ParkedAt(0)));
        int m_PushedExitStash = -1, m_PushedCargo = -1;
        /// <summary>What the team has seen of the house: [0] = the house seed, then the bits of <see cref="Stealth.Level.ExplorationMap"/>.</summary>
        NetworkList<ulong> m_Explored;
        int m_PushedExplored = -1;
        float m_NextExploredPush;
        int m_PushedStash = -1;
        readonly List<InventoryEntry> m_Buffer = new();
        GameManager m_Game;

        void Awake()
        {
            m_Game = GetComponent<GameManager>();
            m_Stash = new NetworkList<NetInventoryEntry>();
            m_ExitStash = new NetworkList<NetInventoryEntry>();
            m_Bench = new NetworkList<NetInventoryEntry>();
            m_Cargo = new NetworkList<ulong>();
            m_Shred = new NetworkList<ulong>();
            m_Explored = new NetworkList<ulong>();
            m_Opened = new NetworkList<int>();
            m_Votes = new NetworkList<NetVote>();
        }

        public override void OnNetworkSpawn()
        {
            m_Game.Bind(this, IsServer);
            if (IsServer) { PushState(); return; }
            Pull();
            m_Level.OnValueChanged += (_, _) => Pull();
            m_Quota.OnValueChanged += (_, _) => Pull();
            m_Collected.OnValueChanged += (_, _) => Pull();
            m_RunTotal.OnValueChanged += (_, _) => Pull();
            m_Phase.OnValueChanged += (_, _) => Pull();
            m_AlarmUntil.OnValueChanged += (_, _) => Pull();
            m_LeaveAt.OnValueChanged += (_, _) => Pull();
            m_GoAt.OnValueChanged += (_, _) => Pull();
            m_Chosen.OnValueChanged += (_, _) => Pull();
            m_Style.OnValueChanged += (_, v) => m_Game.ApplyStyle(v);
            m_Game.ApplyStyle(m_Style.Value); // a late joiner sees what the team sees
            PullOpened();
            m_Opened.OnListChanged += _ => PullOpened();
            PullVotes();
            m_Votes.OnListChanged += _ => PullVotes();
            PullStash();
            m_Stash.OnListChanged += _ => PullStash();
            m_ExitStash.OnListChanged += _ => PullStash();
            m_Bench.OnListChanged += _ => PullStash();
            m_Known.OnValueChanged += (_, _) => m_Game.ApplyCrafting(m_Known.Value, m_LastCombine.Value);
            m_LastCombine.OnValueChanged += (_, _) => m_Game.ApplyCrafting(m_Known.Value, m_LastCombine.Value);
            m_Game.ApplyCrafting(m_Known.Value, m_LastCombine.Value);
            m_Cargo.OnListChanged += _ => PullCargo();
            PullCargo();
            m_Shred.OnListChanged += _ => PullShred();
            m_ShredStart.OnValueChanged += (_, _) => PullShred();
            PullShred();
            // (the drive's start before its times: a peer never moves the car from a stale start)
            m_DepartAt.OnValueChanged += (_, v) => m_Game.DepartAt = v;
            m_Game.DepartAt = m_DepartAt.Value;
            m_DriveFrom.OnValueChanged += (_, v) => m_Game.DriveFrom = v;
            m_Game.DriveFrom = m_DriveFrom.Value;
            m_DriveBranch.OnValueChanged += (_, v) => m_Game.DriveBranch = v;
            m_Game.DriveBranch = m_DriveBranch.Value;
            m_DriveUntil.OnValueChanged += (_, v) => m_Game.DriveUntil = v;
            m_Game.DriveUntil = m_DriveUntil.Value;
            m_ArriveAt.OnValueChanged += (_, v) => m_Game.ArriveAt = v;
            m_Game.ArriveAt = m_ArriveAt.Value;
            m_Lift.OnValueChanged += (_, v) => m_Game.Lift = v.State;
            m_Game.Lift = m_Lift.Value.State;
            m_StunnedUntil.OnValueChanged += (_, v) => m_Game.ApplyStun(v);
            m_HouseClock.OnValueChanged += (_, v) => m_Game.ApplyHouseClock(v);
            m_Game.ApplyHouseClock(m_HouseClock.Value);
            m_Picking.OnValueChanged += (_, v) => m_Game.ApplyPicking(v);
            m_Game.ApplyPicking(m_Picking.Value);
            m_Upgrades.OnValueChanged += (_, v) => m_Game.ApplyUpgrades(v);
            m_Game.ApplyUpgrades(m_Upgrades.Value);
            m_TeleportCharge.OnValueChanged += (_, v) => m_Game.ApplyTeleportCharge(v);
            m_Game.ApplyTeleportCharge(m_TeleportCharge.Value);
            m_Cassette.OnValueChanged += (_, v) => m_Game.ApplyCassette(v);
            m_Game.ApplyCassette(m_Cassette.Value);
            m_Explored.OnListChanged += _ => PullExplored();
        }

        public override void OnNetworkDespawn() => m_Game.Unbind();

        void LateUpdate()
        {
            if (IsSpawned && IsServer) PushState();
        }

        public void PushState()
        {
            NetSync.Set(m_Level, new NetLevelInfo
            {
                Seed = m_Game.Level.Seed, Index = m_Game.Level.Index, Players = (byte)Mathf.Clamp(m_Game.Level.Players, 0, 255),
                Lane = (byte)Mathf.Clamp(m_Game.Level.Lane, 0, 255), RunSeed = m_Game.Level.RunSeed,
            });
            NetSync.Set(m_Quota, m_Game.Quota);
            NetSync.Set(m_Collected, m_Game.Collected);
            NetSync.Set(m_RunTotal, m_Game.RunTotal);
            NetSync.Set(m_Phase, m_Game.Phase);
            NetSync.Set(m_AlarmUntil, m_Game.AlarmUntil);
            NetSync.Set(m_LeaveAt, m_Game.LeaveAt);
            NetSync.Set(m_GoAt, m_Game.GoAt);
            NetSync.Set(m_Chosen, (sbyte)Mathf.Clamp(m_Game.ChosenLane, -1, 127));
            NetSync.Set(m_Style, (sbyte)Mathf.Clamp(m_Game.StyleIndex, -1, 127));
            NetSync.SetList(m_Opened, ref m_PushedOpened, m_Game.OpenedVersion, m_Game.Opened, k => k);
            NetSync.SetList(m_Votes, ref m_PushedVotes, m_Game.Votes.Version, m_Game.Votes.All, v => new NetVote { Client = v.Key, Lane = (byte)v.Value });
            PushExplored();
            NetSync.SetList(m_Stash, ref m_PushedStash, m_Game.Stash.Version, m_Game.Stash.Items, NetInventoryEntry.From);
            NetSync.SetList(m_ExitStash, ref m_PushedExitStash, m_Game.ExitStash.Version, m_Game.ExitStash.Items, NetInventoryEntry.From);
            NetSync.SetList(m_Bench, ref m_PushedBench, m_Game.Bench.Version, m_Game.Bench.Items, NetInventoryEntry.From);
            NetSync.Set(m_Known, m_Game.KnownRecipes);
            NetSync.Set(m_LastCombine, m_Game.LastCombine);
            NetSync.SetList(m_Cargo, ref m_PushedCargo, m_Game.Cargo.Version, m_Game.Cargo.Items, c => c.Pack());
            double shredBase = m_Game.Shredder.Started; // the starts go as offsets from the heap's earliest
            NetSync.Set(m_ShredStart, shredBase);
            NetSync.SetList(m_Shred, ref m_PushedShred, m_Game.Shredder.Version, m_Game.Shredder.Pile, p => Stealth.Vehicles.Shredder.Pack(p, shredBase));
            NetSync.Set(m_DepartAt, m_Game.DepartAt);
            NetSync.Set(m_DriveFrom, m_Game.DriveFrom);
            NetSync.Set(m_DriveBranch, (sbyte)Mathf.Clamp(m_Game.DriveBranch, -1, 127));
            NetSync.Set(m_DriveUntil, m_Game.DriveUntil);
            NetSync.Set(m_ArriveAt, m_Game.ArriveAt);
            NetSync.Set(m_StunnedUntil, m_Game.StunnedUntil);
            NetSync.Set(m_Lift, NetLift.Of(m_Game.Lift));
            NetSync.Set(m_HouseClock, m_Game.HouseClockAt);
            NetSync.Set(m_Picking, m_Game.Picking);
            NetSync.Set(m_Upgrades, m_Game.Upgrades.Pack());
            NetSync.Set(m_TeleportCharge, (byte)m_Game.TeleportChargePercent);
            NetSync.Set(m_Cassette, (sbyte)m_Game.Cassette);
        }

        /// <summary>Sends only the changed chunks, a few times a second; a new house resends the whole list.</summary>
        void PushExplored()
        {
            var map = m_Game.Exploration;
            if (map == null || map.Version == m_PushedExplored || Time.unscaledTime < m_NextExploredPush) return;
            m_PushedExplored = map.Version;
            m_NextExploredPush = Time.unscaledTime + 0.25f;
            ulong seed = (uint)map.Seed;
            if (m_Explored.Count != map.ChunkCount + 1 || m_Explored[0] != seed)
            {
                m_Explored.Clear();
                m_Explored.Add(seed);
                for (int i = 0; i < map.ChunkCount; i++) m_Explored.Add(map.Chunk(i));
                return;
            }
            for (int i = 0; i < map.ChunkCount; i++)
                if (m_Explored[i + 1] != map.Chunk(i)) m_Explored[i + 1] = map.Chunk(i);
        }

        void PullExplored()
        {
            var map = m_Game.Exploration;
            if (map == null || m_Explored.Count != map.ChunkCount + 1 || m_Explored[0] != (uint)map.Seed) return;
            for (int i = 0; i < map.ChunkCount; i++) map.Merge(i, m_Explored[i + 1]);
        }

        void PullOpened()
        {
            var keys = new List<int>();
            foreach (var k in m_Opened) keys.Add(k);
            m_Game.ApplyOpened(keys);
        }

        void PullVotes()
        {
            m_VoteBuffer.Clear();
            foreach (var v in m_Votes) m_VoteBuffer.Add(new KeyValuePair<ulong, int>(v.Client, v.Lane));
            m_Game.Votes.ApplyReplicated(m_VoteBuffer);
        }

        void PullStash()
        {
            m_Buffer.Clear();
            foreach (var e in m_Stash) m_Buffer.Add(e.ToEntry());
            m_Game.Stash.ApplyReplicated(m_Buffer);
            m_Buffer.Clear();
            foreach (var e in m_ExitStash) m_Buffer.Add(e.ToEntry());
            m_Game.ExitStash.ApplyReplicated(m_Buffer);
            m_Buffer.Clear();
            foreach (var e in m_Bench) m_Buffer.Add(e.ToEntry());
            m_Game.Bench.ApplyReplicated(m_Buffer);
        }

        readonly List<ulong> m_CargoBuffer = new();

        void PullShred()
        {
            m_CargoBuffer.Clear();
            foreach (var c in m_Shred) m_CargoBuffer.Add(c);
            m_Game.Shredder.ApplyReplicated(m_CargoBuffer, m_ShredStart.Value);
        }

        void PullCargo()
        {
            m_CargoBuffer.Clear();
            foreach (var c in m_Cargo) m_CargoBuffer.Add(c);
            m_Game.Cargo.ApplyReplicated(m_CargoBuffer);
        }

        void Pull()
        {
            var l = m_Level.Value;
            m_Game.ApplyReplicated(new LevelInfo { Seed = l.Seed, Index = l.Index, Players = l.Players, Lane = l.Lane, RunSeed = l.RunSeed },
                m_Quota.Value, m_Collected.Value, m_RunTotal.Value, m_Phase.Value, m_AlarmUntil.Value, m_LeaveAt.Value, m_GoAt.Value, m_Chosen.Value);
            PullExplored(); // the house may have just been built
        }

        // ------------------------------------------------------------------ IGameLink


        public void RequestRestart() => RestartRpc();
        public void RequestStyle(int index) => StyleRpc(index);

        [Rpc(SendTo.Server)]
        void StyleRpc(int index) => m_Game.SetStyleServer(index);
        public void BroadcastBanner(string text) => BannerRpc(text);
        public void BroadcastHouseStats(string packed) => HouseStatsRpc(packed);

        public void BroadcastSound(Stealth.Audio.GameSound sound, Vector3 at) => SoundRpc((byte)sound, at);

        [Rpc(SendTo.Everyone)]
        void SoundRpc(byte sound, Vector3 at) => m_Game.ReceiveSound((Stealth.Audio.GameSound)sound, at);

        [Rpc(SendTo.Everyone)]
        void HouseStatsRpc(string packed) => m_Game.ReceiveHouseStats(packed);
        public void BroadcastPing(ulong owner, PingKind kind, Vector3 position, ulong target) => PingRpc(owner, kind, position, target);

        [Rpc(SendTo.Everyone)]
        void PingRpc(ulong owner, PingKind kind, Vector3 position, ulong target) => m_Game.ReceivePing(owner, kind, position, target);



        [Rpc(SendTo.Server)]
        void RestartRpc() => m_Game.RestartRunServer();

        [Rpc(SendTo.Everyone)]
        void BannerRpc(string text) => m_Game.ShowBanner(text);
    }
}
