using Stealth.Enemies;
using Stealth.Game;
using Stealth.Player;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Stealth.Net
{
    /// <summary>
    /// Network twin of <see cref="PlayerController"/>. Health is written by the server, posture, look pitch and name
    /// by the owner; everyone else applies them. Carries the owner's requests (noise, revive, hack, gas) to the server
    /// and the hit feedback back to the owner. The transform itself is synced by the owner's NetworkTransform.
    /// </summary>
    public class PlayerNet : NetworkBehaviour, IPlayerLink, INetSync
    {
        readonly NetworkVariable<int> m_Hp = new(HealthState.MaxHp);
        readonly NetworkVariable<bool> m_Downed = new();
        readonly NetworkVariable<bool> m_InHatch = new();
        readonly NetworkVariable<bool> m_Crouching = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<sbyte> m_LookPitch = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<FixedString32Bytes> m_Name = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        PlayerController m_Player;
        NetworkTransform m_Transform;
        string m_PushedName;

        void Awake()
        {
            m_Player = GetComponent<PlayerController>();
            m_Transform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            m_Player.Bind(this, OwnerClientId, IsOwner, IsServer);
            m_Hp.OnValueChanged += (_, _) => Pull();
            m_Downed.OnValueChanged += (_, _) => Pull();
            m_InHatch.OnValueChanged += (_, _) => Pull();
            m_Crouching.OnValueChanged += (_, _) => Pull();
            m_LookPitch.OnValueChanged += (_, _) => Pull();
            m_Name.OnValueChanged += (_, _) => Pull();
            PushState();
            Pull();
        }

        public override void OnNetworkDespawn() => m_Player.Unbind();

        void LateUpdate()
        {
            if (IsSpawned) PushState();
        }

        public void PushState()
        {
            if (IsServer)
            {
                NetSync.Set(m_Hp, m_Player.Hp);
                NetSync.Set(m_Downed, m_Player.Downed);
                NetSync.Set(m_InHatch, m_Player.InHatch);
            }
            if (!IsOwner) return;
            NetSync.Set(m_Crouching, m_Player.Crouching);
            NetSync.Set(m_LookPitch, m_Player.LookPitch);
            if (m_PushedName == m_Player.DisplayName) return;
            m_PushedName = m_Player.DisplayName;
            m_Name.Value = new FixedString32Bytes(m_PushedName);
        }

        void Pull()
        {
            if (!IsServer) m_Player.ApplyServerState(m_Hp.Value, m_Downed.Value, m_InHatch.Value);
            if (!IsOwner) m_Player.ApplyOwnerState(m_Crouching.Value, m_LookPitch.Value, m_Name.Value.ToString());
        }

        // ------------------------------------------------------------------ IPlayerLink

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (m_Transform != null && m_Transform.IsSpawned) m_Transform.Teleport(position, rotation, transform.localScale);
        }

        public void SendNoise(Vector3 position, float radius) => NoiseRpc(position, radius);
        public void SendGasKnockout() => GasKnockoutRpc();
        public void SendRevive(PlayerController target) => ReviveRpc(target.GetComponent<NetworkObject>());
        public void SendGetUp() => GetUpRpc();

        [Rpc(SendTo.Server)]
        void GetUpRpc() => m_Player.OnGetUpRequest();
        public void SendHack(Detector target) => HackRpc(target.GetComponent<NetworkObject>());
        public void SendRemoteHack(Detector target, int terminal) => RemoteHackRpc(target.GetComponent<NetworkObject>(), terminal);

        [Rpc(SendTo.Server)]
        void RemoteHackRpc(NetworkObjectReference target, int terminal)
        {
            if (target.TryGet(out var obj)) GameManager.Instance?.RemoteHackServer(m_Player, terminal, obj.GetComponent<Detector>());
        }
        public void SendHitFeedback(Vector3 awayFromSource) => HitFeedbackRpc(awayFromSource);
        public void SendPing(Vector3 position) => PingRpc(position);
        public void SendHatch(bool hide) => HatchRpc(hide);
        public void SendUnlockDoor(int door) => UnlockDoorRpc(door);
        public void SendOpenSafe(int safe) => OpenSafeRpc(safe);
        public void SendCheat(Cheat cheat, bool on) => CheatRpc((byte)cheat, on);

        [Rpc(SendTo.Server)]
        void CheatRpc(byte cheat, bool on) => m_Player.CheatServer((Cheat)cheat, on);

        [Rpc(SendTo.Server)]
        void HatchRpc(bool hide) => m_Player.OnHatchRequest(hide);

        [Rpc(SendTo.Server)]
        void UnlockDoorRpc(int door) => GameManager.Instance?.UnlockDoorServer(m_Player, door);

        [Rpc(SendTo.Server)]
        void OpenSafeRpc(int safe) => GameManager.Instance?.OpenSafeServer(m_Player, safe);
        public void SendMark(Detector target) => MarkRpc(target.GetComponent<NetworkObject>());

        [Rpc(SendTo.Server)]
        void PingRpc(Vector3 position) => m_Player.OnPingRequest(position);

        [Rpc(SendTo.Server)]
        void MarkRpc(NetworkObjectReference target)
        {
            if (target.TryGet(out var obj)) m_Player.OnMarkRequest(obj.GetComponent<Detector>());
        }

        [Rpc(SendTo.Server)]
        void NoiseRpc(Vector3 position, float radius) => m_Player.OnNoiseRequest(position, radius);

        [Rpc(SendTo.Server)]
        void GasKnockoutRpc() => m_Player.OnGasKnockoutRequest();

        [Rpc(SendTo.Server)]
        void ReviveRpc(NetworkObjectReference target)
        {
            if (target.TryGet(out var obj)) m_Player.OnReviveRequest(obj.GetComponent<PlayerController>());
        }

        [Rpc(SendTo.Server)]
        void HackRpc(NetworkObjectReference target)
        {
            if (target.TryGet(out var obj)) m_Player.OnHackRequest(obj.GetComponent<IHackable>());
        }

        [Rpc(SendTo.Owner)]
        void HitFeedbackRpc(Vector3 awayFromSource) => m_Player.OnHitFeedback(awayFromSource);
    }
}
