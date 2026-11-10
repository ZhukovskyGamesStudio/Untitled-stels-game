using System;
using System.Collections.Generic;
using Stealth.Game;
using Stealth.Player;
using Unity.Netcode;

namespace Stealth.Net
{
    /// <summary>Wire format of <see cref="LevelInfo"/>.</summary>
    public struct NetLevelInfo : INetworkSerializeByMemcpy, IEquatable<NetLevelInfo>
    {
        public int Seed;
        public int Index;
        public bool Equals(NetLevelInfo o) => Seed == o.Seed && Index == o.Index;
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
        NetworkList<NetInventoryEntry> m_Stash;
        int m_PushedStash = -1;
        readonly List<InventoryEntry> m_Buffer = new();
        GameManager m_Game;

        void Awake()
        {
            m_Game = GetComponent<GameManager>();
            m_Stash = new NetworkList<NetInventoryEntry>();
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
            PullStash();
            m_Stash.OnListChanged += _ => PullStash();
        }

        public override void OnNetworkDespawn() => m_Game.Unbind();

        void LateUpdate()
        {
            if (IsSpawned && IsServer) PushState();
        }

        public void PushState()
        {
            NetSync.Set(m_Level, new NetLevelInfo { Seed = m_Game.Level.Seed, Index = m_Game.Level.Index });
            NetSync.Set(m_Quota, m_Game.Quota);
            NetSync.Set(m_Collected, m_Game.Collected);
            NetSync.Set(m_RunTotal, m_Game.RunTotal);
            NetSync.Set(m_Phase, m_Game.Phase);
            NetSync.Set(m_AlarmUntil, m_Game.AlarmUntil);
            if (m_PushedStash != m_Game.Stash.Version)
            {
                m_PushedStash = m_Game.Stash.Version;
                m_Stash.Clear();
                foreach (var e in m_Game.Stash.Items) m_Stash.Add(NetInventoryEntry.From(e));
            }
        }

        void PullStash()
        {
            m_Buffer.Clear();
            foreach (var e in m_Stash) m_Buffer.Add(e.ToEntry());
            m_Game.Stash.ApplyReplicated(m_Buffer);
        }

        void Pull() => m_Game.ApplyReplicated(new LevelInfo { Seed = m_Level.Value.Seed, Index = m_Level.Value.Index },
            m_Quota.Value, m_Collected.Value, m_RunTotal.Value, m_Phase.Value, m_AlarmUntil.Value);

        // ------------------------------------------------------------------ IGameLink

        public void RequestFinishLevel() => FinishLevelRpc();
        public void RequestRestart() => RestartRpc();
        public void BroadcastBanner(string text) => BannerRpc(text);

        [Rpc(SendTo.Server)]
        void FinishLevelRpc(RpcParams rpcParams = default) => m_Game.FinishLevelServer(rpcParams.Receive.SenderClientId);

        [Rpc(SendTo.Server)]
        void RestartRpc() => m_Game.RestartRunServer();

        [Rpc(SendTo.Everyone)]
        void BannerRpc(string text) => m_Game.ShowBanner(text);
    }
}
