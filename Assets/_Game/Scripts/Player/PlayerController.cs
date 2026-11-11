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
        /// <summary>Server-owned: hiding in the hatch (robots do not see you; when the whole team is in, it leaves the house).</summary>
        public bool InHatch { get; private set; }
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
        readonly HoldProgress<Detector> m_HackHeld = new();
        readonly HoldProgress<AutoDoor> m_DoorHeld = new();
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
        public bool IsDown => !IsSpawned || Downed || InHatch;
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
                GameSettings.EnsureLoaded(); // and the controls
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
        public void ApplyServerState(int hp, bool downed, bool inHatch)
        {
            if (downed && !Downed) m_DownedSince = Time.time;
            Hp = hp;
            Downed = downed;
            InHatch = inHatch;
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
            if (Keyboard.current == null) return;

            bool cracking = Cracking != null || UsingTerminal != null; // busy at a dial or a terminal: no walking
            CursorMode = !menu && !Downed && !InHatch && !cracking && (Controls.Held(GameAction.Hands) || HoldCursor || DevFlags.ForceHandsMode);
            bool freeCursor = menu || CursorMode;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeCursor;

            // Turning a safe's dial: the player stays put (the mouse turns the dial). In the hatch: look around, no walking.
            var input = DevFlags.Autopilot ?? PlayerInputState.FromDevices(!freeCursor && !cracking);
            if (InHatch) input = new PlayerInputState { Look = input.Look };
            EnterHatchIfNeeded();
            Motor.InHatch = InHatch;
            Motor.Tick(input, !menu && !Downed && !cracking, Downed, Head, Camera, Time.deltaTime);
            Crouching = Motor.IsCrouching;
            SyncLookPitch();

            UpdateNoise();
            UpdateGas();
            UpdateInteractions();
            UpdateCallouts();
            UpdateFog();
        }

        /// <summary>Dark haze indoors (it only takes the distance away, it does not light anything), the gas fog outside.</summary>
        void UpdateFog()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null)
            {
                // The sewer: a damp indoor haze, the background is the haze too (never the default sky colour).
                if (SewerLocation.Active == null) return;
                var a = VisualStyles.World;
                RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, a.IndoorFogDensity * 1.4f, Time.deltaTime * 3f);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, a.IndoorFog, Time.deltaTime * 4f);
                Camera.backgroundColor = RenderSettings.fogColor;
                return;
            }
            bool outside = !layout.IsInside(transform.position);
            // Outside the gas is a wall: 1.5 m away nothing is seen — lose sight of the house and you are lost.
            var air = VisualStyles.World; // the gas density is gameplay; a style changes only the colours and the indoor haze
            float target = outside ? LightMath.GasFogDensity : air.IndoorFogDensity;
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, target, Time.deltaTime * 3f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, outside ? air.GasFog : air.IndoorFog, Time.deltaTime * 2f);
            Camera.backgroundColor = RenderSettings.fogColor;
        }

        bool m_PlacedInHatch;

        /// <summary>Owner: the server let us into the hatch — stand in its middle (and leave it where we are when climbing out).</summary>
        void EnterHatchIfNeeded()
        {
            if (InHatch == m_PlacedInHatch) return;
            m_PlacedInHatch = InHatch;
            var layout = ActiveLevel.Layout;
            if (!InHatch || layout == null) return;
            TeleportOwner(layout.HatchPosition + Vector3.up * 0.05f, transform.rotation);
        }

        /// <summary>How deep the model sits while hiding: only the head looks out of the hatch.</summary>
        public const float HatchDepth = 1.5f;
        float m_ModelSink;

        GameObject m_RigPrefab;
        HumanRig m_Rig;

        /// <summary>
        /// The style's rigged human (VisualStyles.PlayerRig) in place of the player model: swapped in and out when the style
        /// changes, painted for the slot, a shadow only for the owner (like the model), posed every frame. Purely visual.
        /// </summary>
        void UpdateRig()
        {
            var prefab = VisualStyles.PlayerRig;
            if (prefab != m_RigPrefab)
            {
                if (m_Rig != null) Destroy(m_Rig.gameObject);
                m_Rig = null;
                m_RigPrefab = prefab;
                if (Model != null)
                    foreach (var r in Model.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = prefab != null;
                if (prefab != null && Model != null)
                {
                    var go = Instantiate(prefab, Model.parent);
                    go.transform.SetLocalPositionAndRotation(Model.localPosition, Quaternion.identity);
                    m_Rig = go.GetComponent<HumanRig>();
                    PlayerLooks.Paint(go.transform, PlayerLooks.SlotOf(ClientId));
                    if (IsOwner)
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
            if (m_Rig == null) return;
            float speed = IsOwner ? Motor.HorizontalSpeed : m_RemoteSpeed;
            m_Rig.Pose(speed, Crouching, Downed, IsOwner ? Motor.Pitch : LookPitch);
        }

        void UpdateModelPose()
        {
            UpdateRig();
            if (Model == null) return;
            m_ModelSink = Mathf.MoveTowards(m_ModelSink, InHatch ? HatchDepth : 0f, Time.deltaTime * 4f);
            if (IsOwner) Model.localPosition = Vector3.down * m_ModelSink;
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
            Model.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(m_RemoteBobPhase)) * 0.05f * walk - m_ModelSink, 0);
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


        SewerLocation m_PlacedSewer;

        void PlaceOnLevelIfNeeded()
        {
            var sewer = SewerLocation.Active;
            if (sewer != null && sewer != m_PlacedSewer)
            {
                m_PlacedSewer = sewer;
                int slot = PlayerLooks.SlotOf(ClientId);
                TeleportOwner(sewer.SpawnFor(slot), sewer.SpawnRotation(slot));
                Motor.SetPitch(0f);
                Cracking = null;
                return;
            }
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

        float m_UnstuckAt = -100f;

        /// <summary>Owner: "I'm stuck" — move to the nearest spot where you stand free (a cooldown keeps it from being an escape).</summary>
        public void RequestUnstuck()
        {
            if (!IsOwner || !IsSpawned) return;
            var gm = GameManager.Instance;
            if (Downed || InHatch) return;
            float wait = m_UnstuckAt + m_Interaction.UnstuckCooldown - Time.time;
            if (wait > 0f)
            {
                gm?.ShowBanner($"«Я застрял» можно снова через {Mathf.CeilToInt(wait)} с");
                return;
            }
            var fallback = new List<Vector3>();
            var sewer = SewerLocation.Active;
            if (sewer != null)
                foreach (var s in sewer.Spawns) fallback.Add(s.position);
            else if (ActiveLevel.Layout is { } layout)
            {
                int floor = LevelLayout.FloorOf(transform.position.y);
                foreach (var p in layout.PatrolPoints)
                    if (LevelLayout.FloorOf(p.y) == floor) fallback.Add(p);
                fallback.AddRange(layout.PlayerSpawns);
            }
            if (!Unstuck.FindSpot(transform.position, m_Controller.radius, m_Controller.height, fallback, transform, out var spot))
            {
                gm?.ShowBanner("Свободного места рядом не нашлось");
                return;
            }
            m_UnstuckAt = Time.time;
            Cracking = null;
            CrackingSafe = null;
            TeleportOwner(spot, transform.rotation);
            Debug.Log($"[Player] Unstuck: moved to {spot}");
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

        void UpdateInteractions()
        {
            // Knocked down with HP left: Space gets you up — when the robot that hit you has gone.
            if (Downed && !GameMenu.IsOpen && CanGetUp && Controls.Pressed(GameAction.Jump))
            {
                if (m_Link != null) m_Link.SendGetUp();
                else OnGetUpRequest();
            }
            if (GameMenu.IsOpen) { ReviveProgress = 0; ReviveTarget = null; return; }
            if (Controls.Pressed(GameAction.Unstuck) && !CursorMode) RequestUnstuck();
            // A failed run is restarted by any player — usually everybody is down by then.
            var game = GameManager.Instance;
            if (game != null && Controls.Pressed(GameAction.Restart) && game.Phase == RunPhase.Failed && !CursorMode)
                game.RequestRestart();
            if (Downed) { ReviveProgress = 0; ReviveTarget = null; StopTerminal(); return; }
            bool holdE = Controls.Held(GameAction.Interact) || DevFlags.AutoInteract;
            if (UsingTerminal != null) { UpdateTerminal(holdE); return; }

            // Revive: hold E next to a downed teammate.
            ReviveTarget = null;
            foreach (var p in All)
            {
                if (p == this || !p.Downed) continue;
                if ((p.transform.position - transform.position).magnitude <= m_Interaction.ReviveDistance) { ReviveTarget = p; break; }
            }
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
            // Walking away keeps the progress on that machine: coming back goes on from there.
            HackTarget = ReviveTarget == null ? FindHackTarget() : null;
            if (HackTarget != null && holdE && m_HackHeld.Hold(HackTarget, Time.deltaTime, m_Interaction.HackHoldTime))
            {
                if (m_Link != null) m_Link.SendHack(HackTarget);
                else OnHackRequest(HackTarget);
            }
            HackProgress = m_HackHeld.Of(HackTarget);

            // A locked door: hold E next to it to hack the lock (when nothing more urgent is in reach).
            DoorTarget = ReviveTarget == null && HackTarget == null ? AutoDoor.LockedNear(transform.position) : null;
            if (DoorTarget != null && holdE && m_DoorHeld.Hold(DoorTarget, Time.deltaTime, m_Interaction.DoorHackTime))
            {
                if (m_Link != null) m_Link.SendUnlockDoor(DoorTarget.Index);
                else GameManager.Instance?.UnlockDoorServer(this, DoorTarget.Index);
            }
            DoorProgress = m_DoorHeld.Of(DoorTarget);

            UpdateSafe();
            UpdateTerminal(holdE);

            var gm = GameManager.Instance;
            if (gm == null || !Controls.Pressed(GameAction.Hatch) || gm.Phase != RunPhase.Playing) return;
            // The hatch: hide in it (the team leaves when everyone is in) or climb back out.
            if (InHatch) SetHatch(false);
            else if (Hatch.IsNear(transform.position)) SetHatch(true);
        }

        /// <summary>Owner: hide in the hatch or climb out (also a test hook).</summary>
        public void SetHatch(bool hide)
        {
            if (m_Link != null) m_Link.SendHatch(hide);
            else OnHatchRequest(hide);
        }

        /// <summary>Locked door in reach (hold E to hack), or null.</summary>
        public AutoDoor DoorTarget { get; private set; }
        public float DoorProgress { get; private set; }
        public float DoorShare => DoorProgress / m_Interaction.DoorHackTime;

        // ------------------------------------------------------------------ safes

        /// <summary>Owner: the dial being turned (null when not cracking a safe).</summary>
        public Items.SafeLock Cracking { get; private set; }
        public Items.SafeBox CrackingSafe { get; private set; }
        /// <summary>Owner: a closed safe in reach (press E to start), null if none.</summary>
        public Items.SafeBox SafeTarget { get; private set; }
        /// <summary>Owner: the last try on the dial and when (for the minigame's feedback).</summary>
        public Items.SafeLock.Result LastTry { get; private set; }
        public float LastTryTime { get; private set; } = -10f;
        int m_CrackStartFrame;

        /// <summary>Test hook: start cracking this safe as if E was pressed next to it.</summary>
        public void StartCrackForTest(Items.SafeBox safe)
        {
            if (safe == null) { Cracking = null; CrackingSafe = null; return; }
            Cracking = new Items.SafeLock(safe.Seed, Tuning.Items.Safes);
            CrackingSafe = safe;
            m_CrackStartFrame = Time.frameCount;
        }

        void UpdateSafe()
        {
            SafeTarget = ReviveTarget == null && HackTarget == null && DoorTarget == null ? Items.SafeBox.ClosedNear(transform.position, Hands) : null;
            if (Cracking == null)
            {
                if (SafeTarget == null || !Controls.Pressed(GameAction.Interact) || CursorMode) return;
                var settings = Tuning.Items.Safes;
                Cracking = new Items.SafeLock(SafeTarget.Seed, settings);
                Cracking.SetDial(new System.Random(SafeTarget.Seed ^ Time.frameCount).Next(settings.DialSize));
                CrackingSafe = SafeTarget;
                m_CrackStartFrame = Time.frameCount;
                LastTry = Items.SafeLock.Result.Nothing;
                return;
            }
            var safe = CrackingSafe;
            bool away = safe == null || safe.Opened || (safe.transform.position - transform.position).magnitude > Items.SafeBox.Reach + 0.6f;
            if (away || Controls.Pressed(GameAction.Jump) || Controls.Pressed(GameAction.Back))
            {
                Cracking = null;
                CrackingSafe = null;
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                int ticks = Cracking.Turn(mouse.delta.ReadValue().x);
                if (ticks > 0)
                    Audio.Sfx.At(Audio.Sfx.Bank?.Click, safe.transform.position + Vector3.up, 0.15f + 0.85f * Cracking.Closeness(), Audio.Sfx.Group.Items,
                        0.8f + 0.5f * Cracking.Closeness());
            }
            bool tryNow = Time.frameCount != m_CrackStartFrame &&
                          (Controls.Pressed(GameAction.Interact) || (mouse != null && mouse.leftButton.wasPressedThisFrame));
            if (tryNow) TryDial();
        }

        /// <summary>Owner: try the number under the pointer (LMB / E; also a test hook).</summary>
        public void TryDial()
        {
            var safe = CrackingSafe;
            if (Cracking == null || safe == null) return;
            LastTry = Cracking.Try();
            LastTryTime = Time.time;
            switch (LastTry)
            {
                case Items.SafeLock.Result.Wrong:
                    // A loud clank: robots with ears may come.
                    Audio.Sfx.At(Audio.Sfx.Bank?.Drop, safe.transform.position + Vector3.up, 1f, Audio.Sfx.Group.Items, 0.6f);
                    EmitNoise(Tuning.Items.Safes.WrongNoise);
                    break;
                case Items.SafeLock.Result.Digit:
                    Audio.Sfx.At(Audio.Sfx.Bank?.Pickup, safe.transform.position + Vector3.up, 0.8f, Audio.Sfx.Group.Items, 1.3f);
                    break;
                case Items.SafeLock.Result.Opened:
                    Audio.Sfx.At(Audio.Sfx.Bank?.Stash, safe.transform.position + Vector3.up, 1f, Audio.Sfx.Group.Items);
                    if (m_Link != null) m_Link.SendOpenSafe(safe.Index);
                    else GameManager.Instance?.OpenSafeServer(this, safe.Index);
                    Cracking = null;
                    CrackingSafe = null;
                    break;
            }
        }

        // ------------------------------------------------------------------ callouts: pings and marks

        TeamSettings Team => Tuning.Player.Team;

        /// <summary>
        /// Owner: the ping key calls "here!" where the crosshair points; the mark key marks the robot / device in the
        /// crosshair for the whole team (or pings the spot when there is none).
        /// </summary>
        void UpdateCallouts()
        {
            // Knocked out: still a teammate — pings and marks from the eyes of the one being watched.
            bool canCall = !Downed || Watching != null;
            if (GameMenu.IsOpen || !canCall || CursorMode || Camera == null || GameManager.Instance == null) return;
            bool ping = Controls.Pressed(GameAction.Ping), mark = Controls.Pressed(GameAction.Mark);
            if (ping || mark) Callout(mark);
        }

        /// <summary>Owner: ping where the crosshair points, or mark the robot in it (falls back to a ping). Also a test hook.</summary>
        public void Callout(bool mark)
        {
            if (Camera == null) return;
            var view = Camera.transform;
            if (mark)
            {
                var target = MarkTarget(view.position, view.forward, Team.MarkRange);
                if (target != null)
                {
                    if (m_Link != null) m_Link.SendMark(target);
                    else OnMarkRequest(target);
                    return;
                }
            }
            if (!Physics.Raycast(view.position, view.forward, out var hit, Team.PingRange, Perception.SightMask, QueryTriggerInteraction.Ignore)) return;
            var spot = hit.point + hit.normal * 0.05f;
            if (m_Link != null) m_Link.SendPing(spot);
            else OnPingRequest(spot);
        }

        /// <summary>
        /// The robot / device the player aims at: the crosshair ray hits its body, or it is within a few degrees of the
        /// view and in plain sight (small devices are hard to hit exactly).
        /// </summary>
        public static Detector MarkTarget(Vector3 eye, Vector3 forward, float range)
        {
            if (Physics.Raycast(eye, forward, out var direct, range, Perception.SightMask, QueryTriggerInteraction.Ignore))
            {
                var hit = direct.collider.GetComponentInParent<Detector>();
                if (hit != null && !hit.IsHacked) return hit;
            }
            Detector best = null;
            float bestAngle = 5f;
            foreach (var d in Detector.All)
            {
                var to = d.Position - eye;
                float dist = to.magnitude;
                if (dist > range || dist < 0.01f || d.IsHacked) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle >= bestAngle) continue;
                if (Physics.Raycast(eye, to / dist, out var block, dist - 0.6f, Perception.SightMask, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<Detector>() != d) continue;
                best = d;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>Server: the owner hides in the hatch or climbs out of it.</summary>
        public void OnHatchRequest(bool hide)
        {
            var gm = GameManager.Instance;
            if (!hide) { InHatch = false; return; }
            if (Downed || gm == null || gm.Phase != RunPhase.Playing || !Hatch.IsNear(transform.position)) return;
            InHatch = true;
        }

        /// <summary>Server: the team left the house (or a new one starts).</summary>
        public void LeaveHatchServer() => InHatch = false;

        /// <summary>Server: the owner pinged a spot.</summary>
        // ------------------------------------------------------------------ watching a teammate while knocked out

        /// <summary>Owner, knocked out: the teammate whose eyes the camera looks through (null = our own, lying on the floor).</summary>
        public PlayerController Watching { get; private set; }
        int m_WatchIndex;
        Quaternion m_WatchRotation;
        static readonly List<PlayerController> s_Mates = new();

        void LateUpdate()
        {
            if (!IsOwner || Camera == null) return;
            if (!ReferenceEquals(UsingTerminal, null) && UsingTerminal == null) StopTerminal(); // the house went away
            if (UsingTerminal != null) ShowTerminalView();
            else UpdateWatching();
        }

        // ------------------------------------------------------------------ security terminals

        /// <summary>Owner: the terminal being used (null = none) and the robot / camera on its screen.</summary>
        public SecurityTerminal UsingTerminal { get; private set; }
        public Detector TerminalView { get; private set; }
        public int TerminalViewNumber => m_TerminalIndex + 1;
        public int TerminalViewCount => m_TerminalTargets.Count;
        /// <summary>Owner: a terminal in reach (press E to use it), null if none.</summary>
        public SecurityTerminal TerminalTarget { get; private set; }
        /// <summary>0..1 of holding E at a hacking terminal.</summary>
        public float TerminalHackShare => m_TerminalHold / Mathf.Max(0.05f, m_Interaction.TerminalHackTime);

        readonly List<Detector> m_TerminalTargets = new();
        int m_TerminalIndex;
        float m_TerminalHold;
        bool m_TerminalWaitRelease;

        /// <summary>
        /// Owner: E at a terminal sits down at it. A/D (or the mouse buttons) flip through what it shows, Space or S stands up.
        /// At a hacking terminal holding E hacks the robot on the screen (a charge of the terminal is spent on the server).
        /// </summary>
        void UpdateTerminal(bool holdE)
        {
            if (UsingTerminal == null)
            {
                TerminalTarget = ReviveTarget == null && HackTarget == null && DoorTarget == null && SafeTarget == null && Cracking == null
                    ? SecurityTerminal.Near(transform.position, Hands) : null;
                if (TerminalTarget == null || CursorMode || !Controls.Pressed(GameAction.Interact)) return;
                StartTerminal(TerminalTarget);
                return;
            }
            var t = UsingTerminal;
            var to = t != null ? transform.position - t.transform.position : Vector3.positiveInfinity;
            to.y = 0;
            if (t == null || to.magnitude > SecurityTerminal.Reach + 0.6f || Controls.Pressed(GameAction.Jump) || Controls.Pressed(GameAction.Back))
            {
                StopTerminal();
                return;
            }
            SecurityTerminal.Targets(t.Kind, t.transform.position, m_TerminalTargets);
            if (m_TerminalTargets.Count == 0) { TerminalView = null; return; }
            var mouse = Mouse.current;
            if (Controls.Pressed(GameAction.Right) || (mouse != null && mouse.leftButton.wasPressedThisFrame)) m_TerminalIndex++;
            if (Controls.Pressed(GameAction.Left) || (mouse != null && mouse.rightButton.wasPressedThisFrame)) m_TerminalIndex--;
            int n = m_TerminalTargets.Count;
            m_TerminalIndex = (m_TerminalIndex % n + n) % n;
            var view = m_TerminalTargets[m_TerminalIndex];
            if (view != TerminalView) m_TerminalHold = 0f;
            TerminalView = view;
            if (!holdE) m_TerminalWaitRelease = false;
            bool hacking = t.Kind == TerminalKind.Hacking && t.Charges > 0 && holdE && !m_TerminalWaitRelease && !view.IsHacked;
            if (!hacking) { m_TerminalHold = 0f; return; }
            m_TerminalHold += Time.deltaTime;
            if (m_TerminalHold < m_Interaction.TerminalHackTime) return;
            m_TerminalHold = 0f;
            m_TerminalWaitRelease = true;
            if (m_Link != null) m_Link.SendRemoteHack(view, t.Index);
            else GameManager.Instance?.RemoteHackServer(this, t.Index, view);
        }

        /// <summary>Owner: sit down at a terminal (also a test hook: <paramref name="waitRelease"/> false = E already counts).</summary>
        public void StartTerminal(SecurityTerminal terminal, bool waitRelease = true)
        {
            UsingTerminal = terminal;
            TerminalView = null;
            m_TerminalIndex = 0;
            m_TerminalHold = 0f;
            m_TerminalWaitRelease = waitRelease; // the E that sat us down is not a hack
        }

        /// <summary>Test hook: the next thing on the terminal's screen (as if D was pressed).</summary>
        public void NextTerminalViewForTest() => m_TerminalIndex++;

        /// <summary>Owner: stand up from the terminal; the camera is ours again.</summary>
        public void StopTerminal()
        {
            if (ReferenceEquals(UsingTerminal, null)) return;
            UsingTerminal = null;
            TerminalView = null;
            m_TerminalHold = 0f;
            if (Camera == null) return;
            Camera.transform.localPosition = Vector3.zero;
            Camera.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Owner at a terminal: the camera looks through the security camera on the screen, or at the robot from a clear side.</summary>
        void ShowTerminalView()
        {
            var d = TerminalView;
            if (d == null) return;
            if (d is SecurityCamera cam && cam.Pivot != null)
            {
                // just in front of the lens: the camera's own housing and glow stay behind the view
                Camera.transform.SetPositionAndRotation(d.Position + cam.Pivot.forward * 0.4f, cam.Pivot.rotation);
                return;
            }
            // A robot: from behind (or the first side with no wall in between), a little above it and under the ceiling.
            var target = d.Position;
            float ceiling = LevelLayout.FloorY(LevelLayout.FloorOf(d.transform.position.y)) + LevelMetrics.WallHeight - 0.35f;
            var forward = Vector3.ProjectOnPlane(d.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            Vector3 At(Vector3 dir)
            {
                var at = target + dir.normalized * 2.2f + Vector3.up * 0.6f;
                at.y = Mathf.Min(at.y, ceiling);
                return at;
            }
            Vector3 best = At(-forward);
            foreach (var dir in new[] { -forward, -forward + right, -forward - right, right, -right, forward })
            {
                var at = At(dir);
                if (Physics.Linecast(target + Vector3.up * 0.1f, at, Perception.SightMask, QueryTriggerInteraction.Ignore)) continue;
                best = at;
                break;
            }
            Camera.transform.SetPositionAndRotation(best, Quaternion.LookRotation(target - best));
        }

        /// <summary>
        /// Knocked out with a teammate still standing: the camera rides with them (their position, yaw and the synced head
        /// pitch); the mouse buttons switch between teammates. Back on your feet, the camera is yours again.
        /// </summary>
        void UpdateWatching()
        {
            s_Mates.Clear();
            var gm = GameManager.Instance;
            if (KnockedOut && gm != null && gm.Phase == RunPhase.Playing)
                foreach (var p in All)
                    if (p != this && p.IsSpawned && !p.KnockedOut) s_Mates.Add(p);
            if (s_Mates.Count == 0)
            {
                if (Watching == null) return;
                Watching = null;
                Camera.transform.localPosition = Vector3.zero;
                Camera.transform.localRotation = Quaternion.identity;
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null && !GameMenu.IsOpen)
            {
                if (mouse.leftButton.wasPressedThisFrame) m_WatchIndex++;
                if (mouse.rightButton.wasPressedThisFrame) m_WatchIndex--;
            }
            var mate = s_Mates[(m_WatchIndex % s_Mates.Count + s_Mates.Count) % s_Mates.Count];
            float eye = mate.InHatch ? 0.25f : mate.Downed ? 0.35f : mate.Crouching ? 0.95f : 1.6f;
            var look = mate.transform.rotation * Quaternion.Euler(mate.LookPitch, 0f, 0f);
            m_WatchRotation = Watching == mate ? Quaternion.Slerp(m_WatchRotation, look, 1f - Mathf.Exp(-12f * Time.deltaTime)) : look;
            Watching = mate;
            // A little in front of their face: the camera does not look out from inside their head.
            var pos = mate.transform.position + Vector3.up * eye + m_WatchRotation * Vector3.forward * 0.18f;
            Camera.transform.SetPositionAndRotation(pos, m_WatchRotation);
        }

        /// <summary>
        /// Server: a callout is in range of this player — or, while knocked out, of a teammate still standing (it is
        /// made through their eyes).
        /// </summary>
        bool WithinCallRange(Vector3 position, float range)
        {
            if ((position - transform.position).magnitude <= range) return true;
            if (!KnockedOut) return false;
            foreach (var p in All)
                if (p != this && !p.KnockedOut && (position - p.transform.position).magnitude <= range) return true;
            return false;
        }

        public void OnPingRequest(Vector3 position)
        {
            var gm = GameManager.Instance;
            if (gm == null || !WithinCallRange(position, Team.PingRange + 5f)) return;
            gm.CalloutServer(ClientId, PingKind.Here, position, null);
        }

        /// <summary>Server: the owner marked a robot / device.</summary>
        public void OnMarkRequest(Detector target)
        {
            var gm = GameManager.Instance;
            if (gm == null || target == null || target.IsHacked) return;
            if (!WithinCallRange(target.Position, Team.MarkRange + 5f)) return;
            gm.CalloutServer(ClientId, PingKind.Enemy, target.Position, target);
        }

        /// <summary>Where the player reaches from (chest height): hacking and cracking need a clear line from here.</summary>
        public Vector3 Hands => transform.position + Vector3.up * (Height * 0.7f);

        /// <summary>The nearest robot / device that can be hacked from where we stand.</summary>
        Detector FindHackTarget()
        {
            Detector best = null;
            float bestDist = float.MaxValue;
            foreach (var d in Detector.All)
            {
                if (!d.CanBeHackedFrom(transform.position, HackReach) || !d.ReachableFrom(Hands)) continue;
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
            InHatch = false;
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
            if (Downed || target == null || !target.CanBeHackedFrom(transform.position, HackReach + 0.5f) || !target.ReachableFrom(Hands)) return;
            target.HackServer(m_Interaction.HackDuration);
        }
    }
}
