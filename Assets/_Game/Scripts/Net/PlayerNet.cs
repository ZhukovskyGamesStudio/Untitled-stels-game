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
        readonly NetworkVariable<byte> m_Hatch = new();
        readonly NetworkVariable<bool> m_Flashlight = new();
        readonly NetworkVariable<double> m_Coffee = new();
        readonly NetworkVariable<ushort> m_Loadout = new();
        /// <summary>The double espresso's end (server time) and the gear's flags (PlayerController.GearFlags: the hammer's tired arms).</summary>
        readonly NetworkVariable<double> m_Ram = new();
        readonly NetworkVariable<byte> m_GearFlags = new();
        /// <summary>Knocked out and held (PlayerController.Captive): where, by which robot, at which spot.</summary>
        readonly NetworkVariable<byte> m_Captive = new();
        readonly NetworkVariable<ulong> m_Captor = new();
        readonly NetworkVariable<Vector3> m_CaptiveSpot = new();
        /// <summary>Ailments (PlayerController.Ailments): a net, a stun's end, the muddled head and its end (server time).</summary>
        readonly NetworkVariable<bool> m_Netted = new();
        readonly NetworkVariable<double> m_StunUntil = new();
        readonly NetworkVariable<byte> m_Muddle = new();
        readonly NetworkVariable<double> m_MuddleUntil = new();
        readonly NetworkVariable<bool> m_Crouching = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<sbyte> m_LookPitch = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>The head's turn off the held body (the ladder, a seat), degrees (PlayerController.LookYaw).</summary>
        readonly NetworkVariable<sbyte> m_LookYaw = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<byte> m_Gas = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>What the body is busy with (PlayerController.Pose: a ladder, a terminal, work with the hands) — the rigged human's take.</summary>
        readonly NetworkVariable<byte> m_Pose = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<FixedString32Bytes> m_Name = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<FixedString64Bytes> m_Identity = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        PlayerController m_Player;
        NetworkTransform m_Transform;
        string m_PushedName, m_PushedIdentity;

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
            m_Hatch.OnValueChanged += (_, _) => Pull();
            m_Flashlight.OnValueChanged += (_, _) => Pull();
            m_Coffee.OnValueChanged += (_, _) => Pull();
            m_Loadout.OnValueChanged += (_, _) => Pull();
            m_Ram.OnValueChanged += (_, _) => Pull();
            m_GearFlags.OnValueChanged += (_, _) => Pull();
            m_Captive.OnValueChanged += (_, _) => Pull();
            m_Captor.OnValueChanged += (_, _) => Pull();
            m_CaptiveSpot.OnValueChanged += (_, _) => Pull();
            m_Netted.OnValueChanged += (_, _) => Pull();
            m_StunUntil.OnValueChanged += (_, _) => Pull();
            m_Muddle.OnValueChanged += (_, _) => Pull();
            m_MuddleUntil.OnValueChanged += (_, _) => Pull();
            m_Crouching.OnValueChanged += (_, _) => Pull();
            m_LookPitch.OnValueChanged += (_, _) => Pull();
            m_LookYaw.OnValueChanged += (_, _) => Pull();
            m_Pose.OnValueChanged += (_, _) => Pull();
            m_Name.OnValueChanged += (_, _) => Pull();
            m_Identity.OnValueChanged += (_, now) => m_Player.ApplyIdentity(now.ToString());
            m_Gas.OnValueChanged += (_, now) => { if (!IsOwner) m_Player.ApplyGas(now); };
            PushState();
            if (!IsOwner) m_Player.ApplyIdentity(m_Identity.Value.ToString());
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
                NetSync.Set(m_Hatch, (byte)m_Player.HatchIndex);
                NetSync.Set(m_Flashlight, m_Player.FlashlightOn);
                NetSync.Set(m_Coffee, m_Player.CoffeeUntil);
                NetSync.Set(m_Loadout, m_Player.LoadoutPacked);
                NetSync.Set(m_Ram, m_Player.RamUntil);
                NetSync.Set(m_GearFlags, m_Player.GearFlags);
                NetSync.Set(m_Captive, (byte)m_Player.Captive);
                NetSync.Set(m_Captor, m_Player.CaptorId);
                NetSync.Set(m_CaptiveSpot, m_Player.CaptiveSpot);
                NetSync.Set(m_Netted, m_Player.Netted);
                NetSync.Set(m_StunUntil, m_Player.StunnedUntil);
                NetSync.Set(m_Muddle, (byte)m_Player.Muddled);
                NetSync.Set(m_MuddleUntil, m_Player.MuddledUntil);
            }
            if (!IsOwner) return;
            NetSync.Set(m_Crouching, m_Player.Crouching);
            NetSync.Set(m_LookPitch, m_Player.LookPitch);
            NetSync.Set(m_LookYaw, m_Player.LookYaw);
            NetSync.Set(m_Pose, (byte)m_Player.Pose);
            NetSync.Set(m_Gas, (byte)Mathf.RoundToInt(Mathf.Clamp01(m_Player.GasShare) * 255f));
            if (m_PushedIdentity != m_Player.Identity)
            {
                m_PushedIdentity = m_Player.Identity;
                m_Identity.Value = new FixedString64Bytes(m_PushedIdentity ?? "");
            }
            if (m_PushedName == m_Player.DisplayName) return;
            m_PushedName = m_Player.DisplayName;
            m_Name.Value = new FixedString32Bytes(m_PushedName);
        }

        void Pull()
        {
            if (!IsServer)
            {
                m_Player.ApplyServerState(m_Hp.Value, m_Downed.Value, m_InHatch.Value, m_Hatch.Value, m_Flashlight.Value);
                m_Player.ApplyGear(m_Coffee.Value, m_Loadout.Value, m_Ram.Value, m_GearFlags.Value);
                m_Player.ApplyCaptivity((CaptiveKind)m_Captive.Value, m_Captor.Value, m_CaptiveSpot.Value);
                m_Player.ApplyAilments(m_Netted.Value, m_StunUntil.Value, (Muddle)m_Muddle.Value, m_MuddleUntil.Value);
            }
            if (!IsOwner) m_Player.ApplyOwnerState(m_Crouching.Value, m_LookPitch.Value, m_Name.Value.ToString(), (BodyPose)m_Pose.Value, m_LookYaw.Value);
        }

        // ------------------------------------------------------------------ IPlayerLink

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (m_Transform != null && m_Transform.IsSpawned) m_Transform.Teleport(position, rotation, transform.localScale);
        }

        public void PlaceOwner(Vector3 position, float yaw) => PlaceRpc(position, yaw);

        [Rpc(SendTo.Owner)]
        void PlaceRpc(Vector3 position, float yaw) => m_Player.PlaceFromServer(position, yaw);

        public void SendNoise(Vector3 position, float radius) => NoiseRpc(position, radius);
        public void SendGasKnockout() => GasKnockoutRpc();
        public void SendRevive(PlayerController target) => ReviveRpc(target.GetComponent<NetworkObject>());
        public void SendGetUp() => GetUpRpc();

        [Rpc(SendTo.Server)]
        void GetUpRpc() => m_Player.OnGetUpRequest();

        public void SendStruggle() => StruggleRpc();

        [Rpc(SendTo.Server)]
        void StruggleRpc() => m_Player.OnStruggleRequest();
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
        public void SendUseHand() => UseHandRpc();
        public void SendFallHurt() => FallHurtRpc();
        public void SendLoadout(byte cls, byte item, bool ready) => LoadoutRpc(cls, item, ready);

        [Rpc(SendTo.Server)]
        void FallHurtRpc() => m_Player.OnFallHurtRequest();

        public void SendGear(GearAction action, Detector target, Vector3 point) =>
            GearRpc((byte)action, target != null ? target.GetComponent<NetworkObject>() : default, target != null, point);

        [Rpc(SendTo.Server)]
        void GearRpc(byte action, NetworkObjectReference target, bool hasTarget, Vector3 point) =>
            m_Player.OnGearRequest((GearAction)action, hasTarget && target.TryGet(out var obj) ? obj.GetComponent<Detector>() : null, point);

        [Rpc(SendTo.Server)]
        void LoadoutRpc(byte cls, byte item, bool ready) => m_Player.OnLoadoutRequest(cls, item, ready);

        [Rpc(SendTo.Server)]
        void UseHandRpc() => m_Player.UseHandServer();
        public void SendCheat(Cheat cheat, int arg) => CheatRpc((byte)cheat, arg);
        public void SendUse(Stealth.Level.UseKind kind, int index) => UseRpc((byte)kind, index);
        public void SendGadget() => GadgetRpc();
        public void SendBot(byte command) => BotRpc(command);

        [Rpc(SendTo.Server)]
        void BotRpc(byte command) => GameManager.Instance?.BotCommandServer(m_Player, command);

        [Rpc(SendTo.Server)]
        void UseRpc(byte kind, int index) => GameManager.Instance?.UseServer(m_Player, (Stealth.Level.UseKind)kind, index);

        [Rpc(SendTo.Server)]
        void GadgetRpc() => GameManager.Instance?.GadgetServer(m_Player);
        public void SendTerminal(int terminal, Detector steered, float yaw) =>
            TerminalRpc(terminal, steered != null ? steered.GetComponent<NetworkObject>() : default, steered != null, yaw);

        [Rpc(SendTo.Server)]
        void TerminalRpc(int terminal, NetworkObjectReference steered, bool hasSteered, float yaw) =>
            m_Player.TerminalServer(terminal, hasSteered && steered.TryGet(out var obj) ? obj.GetComponent<Detector>() : null, yaw);

        [Rpc(SendTo.Server)]
        void CheatRpc(byte cheat, int arg) => m_Player.CheatServer((Cheat)cheat, arg);

        [Rpc(SendTo.Server)]
        void HatchRpc(bool hide) => m_Player.OnHatchRequest(hide);

        public void SendMark(Detector target) => MarkRpc(target.GetComponent<NetworkObject>());
        public void SendMarkThing(Vector3 point) => MarkThingRpc(point);

        [Rpc(SendTo.Server)]
        void MarkThingRpc(Vector3 point) => m_Player.OnMarkThingRequest(point);

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
