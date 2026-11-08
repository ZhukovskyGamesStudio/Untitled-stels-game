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
        public Transform HeadPivot;
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
        /// <summary>Camera pitch in degrees (owner writes ~10 times per second) so teammates see where you look.</summary>
        public readonly NetworkVariable<sbyte> LookPitch = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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
        float m_PitchSyncTimer;
        Vector3 m_LastModelPos;
        float m_RemoteSpeed;
        float m_RemoteBobPhase;

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
            EmitNoise(Mathf.Lerp(4f, 14f, Mathf.InverseLerp(3.5f, 10f, impactSpeed)));
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            ApplyColor();
            if (IsOwner)
            {
                Local = this;
                GameSettings.EnsureLoaded();
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

            CursorMode = !menu && !Downed.Value && (kb.tabKey.isPressed || InventoryUI.ForceHandsModeForTest);
            bool freeCursor = menu || CursorMode;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeCursor;

            var input = Autopilot ?? PlayerInputState.FromDevices(!freeCursor);
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
            var layout = LevelGenerator.Current;
            if (layout == null || layout.Seed == m_PlacedSeed || layout.PlayerSpawns.Count == 0) return;
            m_PlacedSeed = layout.Seed;
            var spawn = layout.PlayerSpawns[(int)(OwnerClientId % (ulong)layout.PlayerSpawns.Count)];
            var toHatch = layout.HatchPosition - spawn;
            toHatch.y = 0;
            TeleportOwner(spawn + Vector3.up * 0.1f, Quaternion.LookRotation(toHatch.sqrMagnitude > 0.01f ? toHatch : Vector3.forward));
            Motor.SetPitch(15f);
            Motor.Stamina.Refill();
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
            EmitNoise(radius);
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
                    EmitNoise(LaughNoise);
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

        public void TakeHitServer(Vector3 source)
        {
            if (!IsServer || Downed.Value || Time.time < m_InvulnerableUntil) return;
            m_InvulnerableUntil = Time.time + 1.5f; // a moment to run after the first hit
            Hp.Value = Mathf.Max(0, Hp.Value - 1);
            if (Hp.Value == 0) Downed.Value = true;
            var away = transform.position - source;
            away.y = 0;
            HitFeedbackRpc(away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward);
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
