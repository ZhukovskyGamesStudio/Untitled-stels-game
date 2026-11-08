using System.Collections.Generic;
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
    /// health and "downed" state are server-authoritative.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : NetworkBehaviour
    {
        public static PlayerController Local { get; private set; }
        public static readonly List<PlayerController> All = new();

        public const int MaxHp = 2;
        public const float StandHeight = 1.8f;
        public const float CrouchHeight = 1.1f;
        public const float GasLimit = 25f;
        public const float ReviveTime = 2f;
        public const float ReviveDistance = 2.2f;

        [Header("Refs")]
        public Transform Head;
        public Camera Camera;
        public AudioListener Listener;
        public Transform Model;
        public Renderer[] BodyRenderers;

        public PlayerMotor Motor;

        /// <summary>Test hook: when set, replaces keyboard/mouse input of the local player.</summary>
        public static PlayerInputState? Autopilot;

        [Header("Noise radius (m)")]
        public float WalkNoise = 6f;
        public float SprintNoise = 13f;
        public float CrouchNoise = 2f;
        public float LaughNoise = 9f;

        public readonly NetworkVariable<int> Hp = new(MaxHp);
        public readonly NetworkVariable<bool> Downed = new();
        public readonly NetworkVariable<bool> Crouching = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public bool CursorMode { get; private set; }
        public float GasTime { get; private set; }
        public bool Laughing => GasTime > 8f;
        public float ReviveProgress { get; private set; }
        public PlayerController ReviveTarget { get; private set; }
        public float Height => Crouching.Value ? CrouchHeight : StandHeight;
        public bool IsSprinting => Motor != null && Motor.IsSprinting;

        static readonly Color[] Palette =
        {
            new(0.95f, 0.55f, 0.2f), new(0.3f, 0.7f, 0.95f), new(0.6f, 0.9f, 0.3f), new(0.9f, 0.4f, 0.8f),
        };

        CharacterController m_Controller;
        NetworkTransform m_NetTransform;
        int m_PlacedSeed;
        float m_StepTimer;
        float m_LaughTimer;
        float m_InvulnerableUntil;

        public static PlayerController Find(ulong clientId)
        {
            foreach (var p in All) if (p.OwnerClientId == clientId) return p;
            return null;
        }

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_NetTransform = GetComponent<NetworkTransform>();
            if (Motor == null) Motor = GetComponent<PlayerMotor>();
            Motor.Landed += OnLanded;
        }

        void OnLanded(float impactSpeed)
        {
            if (!IsOwner || !IsSpawned || impactSpeed < 3.5f) return;
            // A normal jump lands at ~4 m/s: audible nearby. Falling from a floor above is loud.
            NoiseRpc(transform.position, Mathf.Lerp(4f, 14f, Mathf.InverseLerp(3.5f, 10f, impactSpeed)));
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            ApplyColor();
            if (IsOwner)
            {
                Local = this;
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
            UpdateModelPose();
            if (!IsOwner) return;

            PlaceOnLevelIfNeeded();
            bool menu = GameMenu.IsOpen;
            var kb = Keyboard.current;
            if (kb == null) return;

            CursorMode = !menu && !Downed.Value && kb.tabKey.isPressed;
            bool freeCursor = menu || CursorMode;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeCursor;

            var input = Autopilot ?? PlayerInputState.FromDevices(!freeCursor);
            Motor.Tick(input, !menu && !Downed.Value, Downed.Value, Head, Time.deltaTime);
            if (Motor.IsCrouching != Crouching.Value) Crouching.Value = Motor.IsCrouching;

            UpdateNoise();
            UpdateGas();
            UpdateInteractions(kb);
            UpdateFog();
        }

        /// <summary>Light haze indoors, an impenetrable gas fog outside.</summary>
        void UpdateFog()
        {
            var layout = LevelGenerator.Current;
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
        }

        void PlaceOnLevelIfNeeded()
        {
            var layout = LevelGenerator.Current;
            if (layout == null || layout.Seed == m_PlacedSeed || layout.PlayerSpawns.Count == 0) return;
            m_PlacedSeed = layout.Seed;
            var spawn = layout.PlayerSpawns[(int)(OwnerClientId % (ulong)layout.PlayerSpawns.Count)];
            var toHatch = layout.HatchPosition - spawn;
            toHatch.y = 0;
            TeleportOwner(spawn + Vector3.up * 0.1f, Quaternion.LookRotation(toHatch.sqrMagnitude > 0.01f ? toHatch : Vector3.forward));
            Motor.SetPitch(15f);
            GasTime = 0;
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
            if (Motor.HorizontalSpeed < 0.6f || !Motor.Grounded || Downed.Value) { m_StepTimer = 0; return; }
            float interval = Crouching.Value ? 0.6f : IsSprinting ? 0.32f : 0.45f;
            m_StepTimer += Time.deltaTime;
            if (m_StepTimer < interval) return;
            m_StepTimer = 0;
            float radius = Crouching.Value ? CrouchNoise : IsSprinting ? SprintNoise : WalkNoise;
            NoiseRpc(transform.position, radius);
        }

        void UpdateGas()
        {
            var layout = LevelGenerator.Current;
            bool outside = layout != null && !layout.IsInside(transform.position);
            GasTime = outside ? GasTime + Time.deltaTime : Mathf.Max(0f, GasTime - Time.deltaTime * 3f);

            if (outside && Laughing && !Downed.Value)
            {
                m_LaughTimer -= Time.deltaTime;
                if (m_LaughTimer <= 0f)
                {
                    m_LaughTimer = 1.2f;
                    NoiseRpc(transform.position, LaughNoise);
                }
            }
            if (GasTime >= GasLimit && !Downed.Value) GasKnockoutRpc();
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
            if (ReviveTarget != null && kb.eKey.isPressed)
            {
                ReviveProgress += Time.deltaTime;
                if (ReviveProgress >= ReviveTime)
                {
                    ReviveRpc(ReviveTarget.NetworkObject);
                    ReviveProgress = 0;
                }
            }
            else ReviveProgress = 0;

            var gm = GameManager.Instance;
            if (gm == null) return;
            if (kb.fKey.wasPressedThisFrame && Hatch.IsNear(transform.position) && gm.Phase.Value == RunPhase.Playing)
                gm.FinishLevelRpc();
            if (kb.rKey.wasPressedThisFrame && gm.Phase.Value == RunPhase.Failed && !CursorMode)
                gm.RestartRunRpc();
        }

        // ------------------------------------------------------------------ server

        public void TakeHitServer()
        {
            if (!IsServer || Downed.Value || Time.time < m_InvulnerableUntil) return;
            m_InvulnerableUntil = Time.time + 1.5f; // a moment to run after the first hit
            Hp.Value = Mathf.Max(0, Hp.Value - 1);
            if (Hp.Value == 0) Downed.Value = true;
            HitFeedbackRpc();
        }

        public void ReviveServer()
        {
            Downed.Value = false;
            Hp.Value = 1;
            m_InvulnerableUntil = Time.time + 2f;
        }

        public void ResetForLevelServer()
        {
            Downed.Value = false;
            Hp.Value = MaxHp;
            m_InvulnerableUntil = Time.time + 3f;
        }

        [Rpc(SendTo.Owner)]
        void HitFeedbackRpc()
        {
            if (GameHUD.Instance != null) GameHUD.Instance.FlashHit();
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
            Hp.Value = 0;
            Downed.Value = true;
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
