using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Player
{
    /// <summary>
    /// First person controller. Movement is owner-authoritative, health and "downed" state are server-authoritative.
    /// Rules live in pure classes (<see cref="PlayerMotor"/>, <see cref="HealthState"/>, <see cref="GasExposure"/>,
    /// <see cref="FootstepNoise"/>); this component wires them to input and the world. Security sees it as an <see cref="IIntruder"/>.
    ///
    /// No networking here: the twin component (PlayerNet, assembly Stealth.Net) calls <see cref="Bind"/> on spawn,
    /// replicates the state below (<see cref="ApplyServerState"/>, <see cref="ApplyOwnerState"/>) and carries the
    /// messages of <see cref="IPlayerLink"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IIntruder
    {
        public static PlayerController Local { get; private set; }
        public static readonly List<PlayerController> All = new();
        /// <summary>The local player appeared (true) or left (false): the menu camera reacts.</summary>
        public static event System.Action<bool> LocalChanged;

        public const float StandHeight = 1.8f;
        public const float CrouchHeight = 1.1f;

        [Header("Refs")]
        public Transform Head;
        public Camera Camera;
        public AudioListener Listener;
        public Transform Model;
        public Transform HeadPivot;
        public Renderer[] BodyRenderers;

        public PlayerMotor Motor;

        /// <summary>Tuning from PlayerConfig (applied in Awake; defaults until then).</summary>
        FootstepNoise m_Footsteps = new();
        InteractionSettings m_Interaction = new();
        HealthSettings m_HealthSettings;

        // ------------------------------------------------------------------ replicated state

        /// <summary>Server-owned.</summary>
        public int Hp { get; private set; } = HealthState.MaxHp;
        /// <summary>Server-owned.</summary>
        public bool Downed { get; private set; }
        /// <summary>Owner-owned.</summary>
        public bool Crouching { get; private set; }
        /// <summary>Owner-owned: camera pitch in degrees (updated ~10 times per second) so teammates see where you look.</summary>
        public sbyte LookPitch { get; private set; }
        /// <summary>Owner-owned: Steam name or empty.</summary>
        public string DisplayName { get; private set; } = "";
        public string Nick => DisplayName.Length > 0 ? DisplayName : $"Игрок {ClientId + 1}";

        public ulong ClientId { get; private set; }
        /// <summary>This peer controls the player (input, camera).</summary>
        public bool IsOwner { get; private set; }
        /// <summary>This peer is the server (health, requests).</summary>
        public bool IsServer { get; private set; } = true;
        public bool IsSpawned { get; private set; }

        public bool CursorMode { get; private set; }
        /// <summary>Set by the inventory while an item is dragged out of the world: the camera stays put, the cursor is free.</summary>
        public static bool HoldCursor { get; set; }
        public GasExposure Gas { get; private set; } = new();
        public float ReviveProgress { get; private set; }
        public PlayerController ReviveTarget { get; private set; }
        /// <summary>Robot or device that can be hacked right now (hold E).</summary>
        public Detector HackTarget { get; private set; }
        public float HackProgress { get; private set; }
        /// <summary>0..1 of holding E over a downed teammate.</summary>
        public float ReviveShare => ReviveProgress / m_Interaction.ReviveTime;
        /// <summary>0..1 of holding E at a robot / device.</summary>
        public float HackShare => HackProgress / m_Interaction.HackHoldTime;
        public float HackReach => m_Interaction.HackReach;
        public float Height => Crouching ? CrouchHeight : StandHeight;
        public bool IsSprinting => Motor != null && Motor.IsSprinting;

        /// <summary>The department of this player (by slot): an IT person from backend, QA, sysadmins or design.</summary>
        public Department Department => PlayerLooks.DepartmentOf(PlayerLooks.SlotOf(ClientId));

        CharacterController m_Controller;
        IPlayerLink m_Link;
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

        public Vector3 Position => transform.position;
        public bool IsCrouching => Crouching;
        public bool IsDown => !IsSpawned || Downed;
        /// <summary>Lying with no HP: only a teammate can lift you (lying with HP left you get up yourself).</summary>
        public bool KnockedOut => Downed && Hp == 0;
        /// <summary>Owner: lying with HP left long enough to get up (Space).</summary>
        public bool CanGetUp => Downed && Hp > 0 && Time.time >= m_DownedSince + m_HealthSettings.GetUpDelay;
        float m_DownedSince;
        public float GroundSpeed => ServerSpeed;
        public bool Carries(int color) => m_Inventory != null && m_Inventory.Carries(color);

        PlayerInventory m_Inventory;

        public static PlayerController Find(ulong clientId)
        {
            foreach (var p in All) if (p.ClientId == clientId) return p;
            return null;
        }

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_Inventory = GetComponent<PlayerInventory>();
            if (Motor == null) Motor = GetComponent<PlayerMotor>();
            var tuning = Tuning.Player;
            Motor.Configure(tuning.Movement, tuning.Stamina);
            m_Footsteps = new FootstepNoise(tuning.Noise);
            Gas = new GasExposure(tuning.Gas);
            m_Interaction = tuning.Interaction;
            m_HealthSettings = tuning.Health;
            Motor.Landed += OnLanded;
        }

        void OnLanded(float impactSpeed)
        {
            float radius = FootstepNoise.LandingRadius(impactSpeed);
            if (IsOwner && IsSpawned && radius > 0f) EmitNoise(radius);
        }

        // ------------------------------------------------------------------ binding (called by the network twin)

        public void Bind(IPlayerLink link, ulong clientId, bool isOwner, bool isServer)
        {
            m_Link = link;
            ClientId = clientId;
            IsOwner = isOwner;
            IsServer = isServer;
            IsSpawned = true;
            All.Add(this);
            ApplyColor();
            name = $"Player_{clientId}";
            VisualStyles.Dress(gameObject);
            if (IsOwner)
            {
                Local = this;
                GameSettings.EnsureLoaded();
                var nick = Session.Launcher?.LocalPlayerName ?? "";
                DisplayName = nick.Length > 28 ? nick.Substring(0, 28) : nick;
                Camera.enabled = true;
                Camera.clearFlags = CameraClearFlags.SolidColor; // no sky: outside is a wall of gas
                Listener.enabled = true;
                foreach (var r in BodyRenderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                LocalChanged?.Invoke(true);
            }
            else
            {
                Camera.enabled = false;
                Listener.enabled = false;
                // CharacterController stays enabled: it is the collider robots raycast against.
            }
        }

        public void Unbind()
        {
            All.Remove(this);
            IsSpawned = false;
            m_Link = null;
            if (Local != this) return;
            Local = null;
            LocalChanged?.Invoke(false);
        }

        /// <summary>Client: health as decided by the server.</summary>
        public void ApplyServerState(int hp, bool downed)
        {
            if (downed && !Downed) m_DownedSince = Time.time;
            Hp = hp;
            Downed = downed;
        }

        /// <summary>Other peers: what the owner reports.</summary>
        public void ApplyOwnerState(bool crouching, sbyte lookPitch, string displayName)
        {
            Crouching = crouching;
            LookPitch = lookPitch;
            DisplayName = displayName ?? "";
        }

        /// <summary>Team colour on the small things only, the department's clothes, skin, dark details (PlayerLooks).</summary>
        void ApplyColor()
        {
            int slot = PlayerLooks.SlotOf(ClientId);
            if (Model != null)
            {
                VisualStyles.SetVariant(Model.gameObject, slot); // the department's model in the current style
                PlayerLooks.Paint(Model, slot);
            }
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

            CursorMode = !menu && !Downed && (kb.tabKey.isPressed || HoldCursor || DevFlags.ForceHandsMode);
            bool freeCursor = menu || CursorMode;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeCursor;

            var input = DevFlags.Autopilot ?? PlayerInputState.FromDevices(!freeCursor);
            Motor.Tick(input, !menu && !Downed, Downed, Head, Camera, Time.deltaTime);
            Crouching = Motor.IsCrouching;
            SyncLookPitch();

            UpdateNoise();
            UpdateGas();
            UpdateInteractions(kb);
            UpdateFog();
        }

        /// <summary>Dark haze indoors (it only takes the distance away, it does not light anything), the gas fog outside.</summary>
        void UpdateFog()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null) return;
            bool outside = !layout.IsInside(transform.position);
            // Outside the gas is a wall: 1.5 m away nothing is seen — lose sight of the house and you are lost.
            var air = VisualStyles.World; // the gas density is gameplay; a style changes only the colours and the indoor haze
            float target = outside ? LightMath.GasFogDensity : air.IndoorFogDensity;
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, target, Time.deltaTime * 3f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, outside ? air.GasFog : air.IndoorFog, Time.deltaTime * 2f);
            Camera.backgroundColor = RenderSettings.fogColor;
        }

        void UpdateModelPose()
        {
            if (Model == null) return;
            float y = Downed ? 0.35f : (Crouching ? 0.65f : 1f);
            Model.localScale = new Vector3(1f, Mathf.MoveTowards(Model.localScale.y, y, Time.deltaTime * 4f), 1f);
            if (IsOwner || Time.deltaTime <= 0f) return;

            // Remote players: tilt the head to the synced pitch and bob a little while walking.
            if (HeadPivot != null)
                HeadPivot.localRotation = Quaternion.Slerp(HeadPivot.localRotation,
                    Quaternion.Euler(Mathf.Clamp(LookPitch, -60, 60), 0, 0), 1f - Mathf.Exp(-12f * Time.deltaTime));
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
            if (Mathf.Abs(pitch - LookPitch) >= 2) LookPitch = pitch;
        }

        void PlaceOnLevelIfNeeded()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null || layout.Seed == m_PlacedSeed || layout.PlayerSpawns.Count == 0) return;
            m_PlacedSeed = layout.Seed;
            var spawn = layout.PlayerSpawns[(int)(ClientId % (ulong)layout.PlayerSpawns.Count)];
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
            m_Link?.Teleport(position, rotation);
            // A teleport is not running: motion and seismic sensors must not feel it (host side; remote jumps are filtered by speed).
            m_ServerLastPos = position;
            ServerSpeed = 0f;
        }

        void UpdateNoise()
        {
            float radius = m_Footsteps.Tick(Motor.HorizontalSpeed, Motor.Grounded && !Downed, Crouching, IsSprinting, Time.deltaTime);
            if (radius > 0f) EmitNoise(radius);
        }

        void UpdateGas()
        {
            var layout = ActiveLevel.Layout;
            bool outside = layout != null && !layout.IsInside(transform.position);
            var effect = Gas.Tick(outside, Time.deltaTime);
            if (Downed) return;
            if (effect == GasExposure.Effect.Laugh) EmitNoise(Gas.Settings.LaughNoise);
            else if (effect == GasExposure.Effect.Knockout)
            {
                if (m_Link != null) m_Link.SendGasKnockout();
                else OnGasKnockoutRequest();
            }
        }

        void UpdateInteractions(Keyboard kb)
        {
            // Knocked down with HP left: Space gets you up — when the robot that hit you has gone.
            if (Downed && !GameMenu.IsOpen && CanGetUp && kb.spaceKey.wasPressedThisFrame)
            {
                if (m_Link != null) m_Link.SendGetUp();
                else OnGetUpRequest();
            }
            if (GameMenu.IsOpen) { ReviveProgress = 0; ReviveTarget = null; return; }
            // A failed run is restarted by any player — usually everybody is down by then.
            var game = GameManager.Instance;
            if (game != null && kb.rKey.wasPressedThisFrame && game.Phase == RunPhase.Failed && !CursorMode)
                game.RequestRestart();
            if (Downed) { ReviveProgress = 0; ReviveTarget = null; return; }

            // Revive: hold E next to a downed teammate.
            ReviveTarget = null;
            foreach (var p in All)
            {
                if (p == this || !p.Downed) continue;
                if ((p.transform.position - transform.position).magnitude <= m_Interaction.ReviveDistance) { ReviveTarget = p; break; }
            }
            bool holdE = kb.eKey.isPressed || DevFlags.AutoInteract;
            if (ReviveTarget != null && holdE)
            {
                ReviveProgress += Time.deltaTime;
                if (ReviveProgress >= m_Interaction.ReviveTime)
                {
                    if (m_Link != null) m_Link.SendRevive(ReviveTarget);
                    else OnReviveRequest(ReviveTarget);
                    ReviveProgress = 0;
                }
            }
            else ReviveProgress = 0;

            // Hack: hold E next to a device, or behind a robot that is not chasing you.
            HackTarget = ReviveTarget == null ? FindHackTarget() : null;
            if (HackTarget != null && holdE)
            {
                HackProgress += Time.deltaTime;
                if (HackProgress >= m_Interaction.HackHoldTime)
                {
                    if (m_Link != null) m_Link.SendHack(HackTarget);
                    else OnHackRequest(HackTarget);
                    HackProgress = 0;
                }
            }
            else HackProgress = 0;

            var gm = GameManager.Instance;
            if (gm == null) return;
            if (kb.fKey.wasPressedThisFrame && Hatch.IsNear(transform.position) && gm.Phase == RunPhase.Playing)
                gm.RequestFinishLevel();
        }

        /// <summary>The nearest robot / device that can be hacked from where we stand.</summary>
        Detector FindHackTarget()
        {
            Detector best = null;
            float bestDist = float.MaxValue;
            foreach (var d in Detector.All)
            {
                if (!d.CanBeHackedFrom(transform.position, HackReach)) continue;
                float dist = (d.transform.position - transform.position).sqrMagnitude;
                if (dist < bestDist) { best = d; bestDist = dist; }
            }
            return best;
        }

        // ------------------------------------------------------------------ server

        public void HitServer(Vector3 source)
        {
            if (!IsServer || !m_Health.TryHit(Time.time)) return;
            PublishHealth();
            var away = transform.position - source;
            away.y = 0;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            if (m_Link != null) m_Link.SendHitFeedback(away);
            else OnHitFeedback(away);
        }

        public void ReviveServer()
        {
            m_Health.Revive(Time.time);
            PublishHealth();
        }

        public void ResetForLevelServer()
        {
            m_Health = HealthState.Full(Time.time, m_HealthSettings);
            PublishHealth();
        }

        void PublishHealth()
        {
            if (m_Health.Downed && !Downed) m_DownedSince = Time.time;
            Hp = m_Health.Hp;
            Downed = m_Health.Downed;
        }

        /// <summary>Server: the owner wants to get up (lying with HP left).</summary>
        public void OnGetUpRequest()
        {
            if (m_Health.GetUp(Time.time)) PublishHealth();
        }

        // ------------------------------------------------------------------ messages (delivered by the network twin)

        /// <summary>Owner: the server says we were hit.</summary>
        public void OnHitFeedback(Vector3 awayFromSource)
        {
            Audio.Sfx.Ui(Audio.Sfx.Bank?.Hurt, 1f, Audio.Sfx.Group.Robots);
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
            if (m_Link != null) m_Link.SendNoise(transform.position, radius);
            else OnNoiseRequest(transform.position, radius);
        }

        /// <summary>Server: the owner made a noise.</summary>
        public void OnNoiseRequest(Vector3 position, float radius)
        {
            if (Downed) return;
            NoiseSystem.EmitServer(position, Mathf.Min(radius, 20f), ClientId);
        }

        /// <summary>Server: the owner passed out from the gas.</summary>
        public void OnGasKnockoutRequest()
        {
            if (Downed) return;
            m_Health.Knockout(Time.time);
            PublishHealth();
        }

        /// <summary>Server: the owner finished holding E over a downed teammate.</summary>
        public void OnReviveRequest(PlayerController other)
        {
            if (Downed || other == null || !other.Downed) return;
            if ((other.transform.position - transform.position).magnitude > m_Interaction.ReviveDistance + 1f) return;
            other.ReviveServer();
        }

        /// <summary>Server: the owner finished holding E at a robot / device.</summary>
        public void OnHackRequest(IHackable target)
        {
            if (Downed || target == null || !target.CanBeHackedFrom(transform.position, HackReach + 0.5f)) return;
            target.HackServer(m_Interaction.HackDuration);
        }
    }
}
