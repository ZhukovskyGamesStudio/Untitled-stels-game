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
        NetworkList<int> m_Opened;
        NetworkList<NetVote> m_Votes;
        int m_PushedOpened = -1, m_PushedVotes = -1;
        readonly List<KeyValuePair<ulong, int>> m_VoteBuffer = new();
        NetworkList<NetInventoryEntry> m_Stash;
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
            PullOpened();
            m_Opened.OnListChanged += _ => PullOpened();
            PullVotes();
            m_Votes.OnListChanged += _ => PullVotes();
            PullStash();
            m_Stash.OnListChanged += _ => PullStash();
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
            if (m_PushedOpened != m_Game.OpenedVersion)
            {
                m_PushedOpened = m_Game.OpenedVersion;
                m_Opened.Clear();
                foreach (var k in m_Game.Opened) m_Opened.Add(k);
            }
            if (m_PushedVotes != m_Game.Votes.Version)
            {
                m_PushedVotes = m_Game.Votes.Version;
                m_Votes.Clear();
                foreach (var v in m_Game.Votes.All) m_Votes.Add(new NetVote { Client = v.Key, Lane = (byte)v.Value });
            }
            PushExplored();
            if (m_PushedStash != m_Game.Stash.Version)
            {
                m_PushedStash = m_Game.Stash.Version;
                m_Stash.Clear();
                foreach (var e in m_Game.Stash.Items) m_Stash.Add(NetInventoryEntry.From(e));
            }
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
        }

        void Pull()
        {
            var l = m_Level.Value;
            m_Game.ApplyReplicated(new LevelInfo { Seed = l.Seed, Index = l.Index, Players = l.Players, Lane = l.Lane, RunSeed = l.RunSeed },
                m_Quota.Value, m_Collected.Value, m_RunTotal.Value, m_Phase.Value, m_AlarmUntil.Value, m_LeaveAt.Value, m_GoAt.Value, m_Chosen.Value);
            PullExplored(); // the house may have just been built
        }

        // ------------------------------------------------------------------ IGameLink

        public void RequestVote(int lane) => VoteRpc((byte)Mathf.Clamp(lane, 0, 255));
        public void RequestRestart() => RestartRpc();
        public void BroadcastBanner(string text) => BannerRpc(text);
        public void BroadcastPing(ulong owner, PingKind kind, Vector3 position, ulong target) => PingRpc(owner, kind, position, target);

        [Rpc(SendTo.Everyone)]
        void PingRpc(ulong owner, PingKind kind, Vector3 position, ulong target) => m_Game.ReceivePing(owner, kind, position, target);

        [Rpc(SendTo.Server)]
        void VoteRpc(byte lane, RpcParams rpcParams = default) => m_Game.VoteServer(rpcParams.Receive.SenderClientId, lane);

        [Rpc(SendTo.Server)]
        void RestartRpc() => m_Game.RestartRunServer();

        [Rpc(SendTo.Everyone)]
        void BannerRpc(string text) => m_Game.ShowBanner(text);
    }
}
