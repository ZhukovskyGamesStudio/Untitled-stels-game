using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.Net;
using Stealth.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Player
{
    /// <summary>
    /// First person controller. Movement is owner-authoritative (NetworkTransform in Owner mode),
    /// health and "downed" state are server-authoritative. Rules live in pure classes
    /// (<see cref="PlayerMotor"/>, <see cref="HealthState"/>, <see cref="GasExposure"/>, <see cref="FootstepNoise"/>);
    /// this component wires them to input, networking and the world. Security sees it as an <see cref="IIntruder"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour, IIntruder
    {
        public static PlayerController Local { get; private set; }
        public static readonly List<PlayerController> All = new();

        public const float StandHeight = 1.8f;
        public const float CrouchHeight = 1.1f;
        public const float ReviveTime = 2f;
        public const float ReviveDistance = 2.2f;

        [Header("Refs")]
        public Transform Head;
        public Camera Camera;
        public AudioListener Listener;
        public Transform Model;
        public Transform HeadPivot;
        public Renderer[] BodyRenderers;

        public PlayerMotor Motor;

        [Header("Noise radius (m)")]
        public FootstepNoise Footsteps = new();
        public float LaughNoise = 9f;

        public readonly NetworkVariable<int> Hp = new(HealthState.MaxHp);
        public readonly NetworkVariable<bool> Downed = new();
        public readonly NetworkVariable<bool> Crouching = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        /// <summary>Camera pitch in degrees (owner writes ~10 times per second) so teammates see where you look.</summary>
        public readonly NetworkVariable<Unity.Collections.FixedString32Bytes> DisplayName =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public string Nick => DisplayName.Value.Length > 0 ? DisplayName.Value.ToString() : $"Игрок {OwnerClientId + 1}";
        public readonly NetworkVariable<sbyte> LookPitch = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool CursorMode { get; private set; }
        public GasExposure Gas { get; } = new();
        public float ReviveProgress { get; private set; }
        public PlayerController ReviveTarget { get; private set; }
        /// <summary>Robot or device that can be hacked right now (hold E).</summary>
        public Detector HackTarget { get; private set; }
        public float HackProgress { get; private set; }
        public float Height => Crouching.Value ? CrouchHeight : StandHeight;
        public bool IsSprinting => Motor != null && Motor.IsSprinting;

        static readonly Color[] Palette =
        {
            new(0.95f, 0.55f, 0.2f), new(0.3f, 0.7f, 0.95f), new(0.6f, 0.9f, 0.3f), new(0.9f, 0.4f, 0.8f),
        };

        CharacterController m_Controller;
        NetworkTransform m_NetTransform;
        int m_PlacedSeed;
        HealthState m_Health = HealthState.Full(0f);
        Vector3 m_ServerLastPos;

        /// <summary>Horizontal speed measured on the server from the replicated position (for vibration sensors).</summary>
        public float ServerSpeed { get; private set; }
        float m_PitchSyncTimer;
        Vector3 m_LastModelPos;
        float m_RemoteSpeed;
        float m_RemoteBobPhase;

        // ------------------------------------------------------------------ IIntruder

        public ulong ClientId => OwnerClientId;
        public Vector3 Position => transform.position;
        public bool IsCrouching => Crouching.Value;
        public bool IsDown => !IsSpawned || Downed.Value;
        public float GroundSpeed => ServerSpeed;
        public bool Carries(int color) => m_Inventory != null && m_Inventory.Carries(color);

        PlayerInventory m_Inventory;

        public static PlayerController Find(ulong clientId)
        {
            foreach (var p in All) if (p.OwnerClientId == clientId) return p;
            return null;
        }

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_NetTransform = GetComponent<NetworkTransform>();
            m_Inventory = GetComponent<PlayerInventory>();
            if (Motor == null) Motor = GetComponent<PlayerMotor>();
            Motor.Landed += OnLanded;
        }

        void OnLanded(float impactSpeed)
        {
            float radius = FootstepNoise.LandingRadius(impactSpeed);
            if (IsOwner && IsSpawned && radius > 0f) EmitNoise(radius);
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            ApplyColor();
            if (IsOwner)
            {
                Local = this;
                GameSettings.EnsureLoaded();
                var nick = SteamManager.Initialized ? SteamManager.PersonaName : $"Игрок {OwnerClientId + 1}";
                if (nick.Length > 28) nick = nick.Substring(0, 28);
                DisplayName.Value = new Unity.Collections.FixedString32Bytes(nick);
                Camera.enabled = true;
                Camera.clearFlags = CameraClearFlags.SolidColor; // no sky: outside is a wall of gas
                Listener.enabled = true;
                if (GameNetwork.Instance != null) GameNetwork.Instance.ShowMenuCamera(false);
                foreach (var r in BodyRenderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            else
            {
                Camera.enabled = false;
                Listener.enabled = false;
                // CharacterController stays enabled: it is the collider robots raycast against.
            }
            name = $"Player_{OwnerClientId}";
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this)
            {
                Local = null;
                if (GameNetwork.Instance != null) GameNetwork.Instance.ShowMenuCamera(true);
            }
        }

        void ApplyColor()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", Palette[(int)(OwnerClientId % (ulong)Palette.Length)]);
            foreach (var r in BodyRenderers) r.SetPropertyBlock(block);
        }

        void Update()
        {
            if (IsServer && Time.deltaTime > 0f)
            {
                var d = transform.position - m_ServerLastPos;
                m_ServerLastPos = transform.position;
                d.y = 0;
                float s = d.magnitude / Time.deltaTime;
                ServerSpeed = s > 20f ? 0f : Mathf.Lerp(ServerSpeed, s, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            UpdateModelPose();
            if (!IsOwner) return;

            PlaceOnLevelIfNeeded();
            bool menu = GameMenu.IsOpen;
            var kb = Keyboard.current;
            if (kb == null) return;

            CursorMode = !menu && !Downed.Value && (kb.tabKey.isPressed || DevFlags.ForceHandsMode);
            bool freeCursor = menu || CursorMode;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeCursor;

            var input = DevFlags.Autopilot ?? PlayerInputState.FromDevices(!freeCursor);
            Motor.Tick(input, !menu && !Downed.Value, Downed.Value, Head, Camera, Time.deltaTime);
            if (Motor.IsCrouching != Crouching.Value) Crouching.Value = Motor.IsCrouching;
            SyncLookPitch();

            UpdateNoise();
            UpdateGas();
            UpdateInteractions(kb);
            UpdateFog();
        }

        /// <summary>Light haze indoors, an impenetrable gas fog outside.</summary>
        void UpdateFog()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null) return;
            bool outside = !layout.IsInside(transform.position);
            float target = outside ? 0.22f : 0.03f;
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, target, Time.deltaTime * 0.25f);
            Camera.backgroundColor = RenderSettings.fogColor;
        }

        void UpdateModelPose()
        {
            if (Model == null) return;
            float y = Downed.Value ? 0.35f : (Crouching.Value ? 0.65f : 1f);
            Model.localScale = new Vector3(1f, Mathf.MoveTowards(Model.localScale.y, y, Time.deltaTime * 4f), 1f);
            if (IsOwner || Time.deltaTime <= 0f) return;

            // Remote players: tilt the head to the synced pitch and bob a little while walking.
            if (HeadPivot != null)
                HeadPivot.localRotation = Quaternion.Slerp(HeadPivot.localRotation,
                    Quaternion.Euler(Mathf.Clamp(LookPitch.Value, -60, 60), 0, 0), 1f - Mathf.Exp(-12f * Time.deltaTime));
            var pos = transform.position;
            var delta = pos - m_LastModelPos;
            m_LastModelPos = pos;
            delta.y = 0;
            float speed = delta.magnitude / Time.deltaTime;
            if (speed > 20f) speed = 0f; // teleport
            m_RemoteSpeed = Mathf.Lerp(m_RemoteSpeed, speed, 1f - Mathf.Exp(-10f * Time.deltaTime));
            float walk = Mathf.Clamp01(m_RemoteSpeed / 3.6f);
            m_RemoteBobPhase += m_RemoteSpeed * 0.75f * Mathf.PI * Time.deltaTime;
            Model.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(m_RemoteBobPhase)) * 0.05f * walk, 0);
            Model.localRotation = Quaternion.Euler(Mathf.Clamp(m_RemoteSpeed * 1.2f, 0f, 8f), 0, Mathf.Sin(m_RemoteBobPhase) * 2f * walk);
        }

        void SyncLookPitch()
        {
            m_PitchSyncTimer -= Time.deltaTime;
            if (m_PitchSyncTimer > 0f) return;
            m_PitchSyncTimer = 0.1f;
            var pitch = (sbyte)Mathf.Clamp(Mathf.RoundToInt(Motor.Pitch), -88, 88);
            if (Mathf.Abs(pitch - LookPitch.Value) >= 2) LookPitch.Value = pitch;
        }

        void PlaceOnLevelIfNeeded()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null || layout.Seed == m_PlacedSeed || layout.PlayerSpawns.Count == 0) return;
            m_PlacedSeed = layout.Seed;
            var spawn = layout.PlayerSpawns[(int)(OwnerClientId % (ulong)layout.PlayerSpawns.Count)];
            var toHatch = layout.HatchPosition - spawn;
            toHatch.y = 0;
            TeleportOwner(spawn + Vector3.up * 0.1f, Quaternion.LookRotation(toHatch.sqrMagnitude > 0.01f ? toHatch : Vector3.forward));
            Motor.SetPitch(15f);
            Motor.Stamina.Refill();
            Gas.Reset();
        }

        public void TeleportOwner(Vector3 position, Quaternion rotation)
        {
            m_Controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            m_Controller.enabled = true;
            if (Motor != null) Motor.ResetMotion();
            if (m_NetTransform != null && m_NetTransform.IsSpawned) m_NetTransform.Teleport(position, rotation, transform.localScale);
        }

        void UpdateNoise()
        {
            float radius = Footsteps.Tick(Motor.HorizontalSpeed, Motor.Grounded && !Downed.Value, Crouching.Value, IsSprinting, Time.deltaTime);
            if (radius > 0f) EmitNoise(radius);
        }

        void UpdateGas()
        {
            var layout = ActiveLevel.Layout;
            bool outside = layout != null && !layout.IsInside(transform.position);
            var effect = Gas.Tick(outside, Time.deltaTime);
            if (Downed.Value) return;
            if (effect == GasExposure.Effect.Laugh) EmitNoise(LaughNoise);
            else if (effect == GasExposure.Effect.Knockout) GasKnockoutRpc();
        }

        void UpdateInteractions(Keyboard kb)
        {
            if (GameMenu.IsOpen || Downed.Value) { ReviveProgress = 0; ReviveTarget = null; return; }

            // Revive: hold E next to a downed teammate.
            ReviveTarget = null;
            foreach (var p in All)
            {
                if (p == this || !p.Downed.Value) continue;
                if ((p.transform.position - transform.position).magnitude <= ReviveDistance) { ReviveTarget = p; break; }
            }
            bool holdE = kb.eKey.isPressed || DevFlags.AutoInteract;
            if (ReviveTarget != null && holdE)
            {
                ReviveProgress += Time.deltaTime;
                if (ReviveProgress >= ReviveTime)
                {
                    ReviveRpc(ReviveTarget.NetworkObject);
                    ReviveProgress = 0;
                }
            }
            else ReviveProgress = 0;

            // Hack: hold E next to a device, or behind a robot that is not chasing you.
            HackTarget = ReviveTarget == null ? FindHackTarget() : null;
            if (HackTarget != null && holdE)
            {
                HackProgress += Time.deltaTime;
                if (HackProgress >= Hacking.HoldTime)
                {
                    HackRpc(HackTarget.NetworkObject);
                    HackProgress = 0;
                }
            }
            else HackProgress = 0;

            var gm = GameManager.Instance;
            if (gm == null) return;
            if (kb.fKey.wasPressedThisFrame && Hatch.IsNear(transform.position) && gm.Phase.Value == RunPhase.Playing)
                gm.FinishLevelRpc();
            if (kb.rKey.wasPressedThisFrame && gm.Phase.Value == RunPhase.Failed && !CursorMode)
                gm.RestartRunRpc();
        }

        /// <summary>The nearest robot / device that can be hacked from where we stand.</summary>
        Detector FindHackTarget()
        {
            Detector best = null;
            float bestDist = float.MaxValue;
            foreach (var d in Detector.All)
            {
                if (!d.CanBeHackedFrom(transform.position)) continue;
                float dist = (d.transform.position - transform.position).sqrMagnitude;
                if (dist < bestDist) { best = d; bestDist = dist; }
            }
            return best;
        }

        [Rpc(SendTo.Server)]
        void HackRpc(NetworkObjectReference target)
        {
            if (Downed.Value || !target.TryGet(out var obj)) return;
            var hackable = obj.GetComponent<IHackable>();
            if (hackable != null && hackable.CanBeHackedFrom(transform.position)) hackable.HackServer(Hacking.Duration);
        }

        // ------------------------------------------------------------------ server

        public void HitServer(Vector3 source)
        {
            if (!IsServer || !m_Health.TryHit(Time.time)) return;
            PublishHealth();
            var away = transform.position - source;
            away.y = 0;
            HitFeedbackRpc(away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward);
        }

        public void ReviveServer()
        {
            m_Health.Revive(Time.time);
            PublishHealth();
        }

        public void ResetForLevelServer()
        {
            m_Health = HealthState.Full(Time.time);
            PublishHealth();
        }

        void PublishHealth()
        {
            Hp.Value = m_Health.Hp;
            Downed.Value = m_Health.Downed;
        }

        [Rpc(SendTo.Owner)]
        void HitFeedbackRpc(Vector3 awayFromSource)
        {
            Motor.AddImpulse(awayFromSource * 6f + Vector3.up * 2.5f);
            Motor.AddTrauma(0.7f);
            if (GameHUD.Instance != null) GameHUD.Instance.FlashHit(awayFromSource);
        }

        /// <summary>Last noise this player made (owner side, for the HUD meter).</summary>
        public float LastNoiseRadius { get; private set; }
        public float LastNoiseTime { get; private set; } = -10f;

        void EmitNoise(float radius)
        {
            LastNoiseRadius = radius;
            LastNoiseTime = Time.time;
            NoiseRpc(transform.position, radius);
        }

        [Rpc(SendTo.Server)]
        void NoiseRpc(Vector3 position, float radius)
        {
            if (Downed.Value) return;
            NoiseSystem.EmitServer(position, Mathf.Min(radius, 20f), OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        void GasKnockoutRpc()
        {
            if (Downed.Value) return;
            m_Health.Knockout();
            PublishHealth();
        }

        [Rpc(SendTo.Server)]
        void ReviveRpc(NetworkObjectReference target)
        {
            if (Downed.Value || !target.TryGet(out var obj)) return;
            var other = obj.GetComponent<PlayerController>();
            if (other == null || !other.Downed.Value) return;
            if ((other.transform.position - transform.position).magnitude > ReviveDistance + 1f) return;
            other.ReviveServer();
        }
    }
}
