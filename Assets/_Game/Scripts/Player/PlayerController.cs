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
    public partial class PlayerController : MonoBehaviour, IIntruder
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
        /// <summary>Owner: next to the hatch and looking right at it (its collider), so the hatch key climbs in.</summary>
        public bool AtHatch { get; private set; }
        /// <summary>Owner-owned.</summary>
        public bool Crouching { get; private set; }
        /// <summary>Owner-owned: camera pitch in degrees (updated ~10 times per second) so teammates see where you look.</summary>
        public sbyte LookPitch { get; private set; }
        /// <summary>Owner-owned: Steam name or empty.</summary>
        public string DisplayName { get; private set; } = "";
        public string Nick => DisplayName.Length > 0 ? DisplayName : Loc.F("player.nick", ClientId + 1);
        /// <summary>Who the player is across reconnects (<see cref="ISessionLauncher.LocalPlayerId"/>; the owner sets it, everyone sees it).</summary>
        public string Identity { get; private set; } = "";

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
        IPlayerLink m_Offline;
        /// <summary>Where the owner's requests go: the network twin, or (no twin: offline, tests) straight to this player's server side.</summary>
        IPlayerLink Link => m_Link ?? (m_Offline ??= new OfflinePlayerLink(this));
        int m_PlacedSeed;
        HealthState m_Health = HealthState.Full(0f);
        Vector3 m_ServerLastPos;

        /// <summary>Horizontal speed measured on the server from the replicated position (for vibration sensors).</summary>
        public float ServerSpeed { get; private set; }
        float m_PitchSyncTimer;
        Vector3 m_LastModelPos;
        float m_RemoteSpeed;

        // ------------------------------------------------------------------ IIntruder

        public Vector3 Position => transform.position;
        public bool IsCrouching => Crouching;
        public bool IsDown => !IsSpawned || Downed || InHatch || Invisible;

        /// <summary>How deep in the laughing gas this player is: 0 clear … 1 passing out (the owner's own, the others' replicated).</summary>
        public float GasShare { get; private set; }
        /// <summary>The share at which the laughing starts (the same settings on every peer).</summary>
        public float GasLaughShare => Gas.LaughAfter / Mathf.Max(0.01f, Gas.Limit);
        /// <summary>A copy of the owner's gas share (the network twin).</summary>
        public void ApplyGas(byte share) => GasShare = share / 255f;

        /// <summary>Server: cheats of the F4 panel — nothing hurts this player / robots do not perceive them.</summary>
        public bool Invulnerable { get; private set; }
        public bool Invisible { get; private set; }

        /// <summary>Owner: a cheat from the F4 panel (the server carries it out).</summary>
        public void RequestCheat(Cheat cheat, bool on)
        {
            // the owner runs its own gas and footsteps: it keeps the flags too (the server's copy is what robots use)
            if (cheat == Cheat.Invulnerable) Invulnerable = on;
            if (cheat == Cheat.Invisible) Invisible = on;
            Link.SendCheat(cheat, on);
        }

        public void CheatServer(Cheat cheat, bool on)
        {
            var gm = GameManager.Instance;
            switch (cheat)
            {
                case Cheat.Invulnerable: Invulnerable = on; break;
                case Cheat.Invisible: Invisible = on; break;
                case Cheat.KillRobots: gm?.KillAllRobotsServer(); break;
                case Cheat.FillQuota: gm?.FillQuotaServer(); break;
                default:
                    if (cheat >= Cheat.StartHouse) gm?.StartHouseServer((Stealth.Level.HouseKind)(cheat - Cheat.StartHouse));
                    break;
            }
        }
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
                Identity = Session.Launcher?.LocalPlayerId ?? "";
                Camera.enabled = true;
                Camera.clearFlags = CameraClearFlags.SolidColor; // the sky is drawn by the gas pass (GasView), never the engine's skybox
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
            if (IsServer && !IsOwner) GameManager.Instance?.PlayerLeftServer(this); // keep the backpack and the spot for a reconnect
            All.Remove(this);
            IsSpawned = false;
            m_Link = null;
            if (Local != this) return;
            Local = null;
            GasView.Hide();
            LocalChanged?.Invoke(false);
        }

        /// <summary>Client: health as decided by the server.</summary>
        public void ApplyServerState(int hp, bool downed, bool inHatch, int hatch = 0, bool flashlight = false)
        {
            if (downed && !Downed) m_DownedSince = Time.time;
            Hp = hp;
            Downed = downed;
            InHatch = inHatch;
            HatchIndex = hatch;
            FlashlightOn = flashlight;
        }

        /// <summary>Which hatch of the house the player sits in: 0 the entry (where the team comes up), 1 the exit (the way down).</summary>
        public int HatchIndex { get; private set; }

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
                UpdateFlashlightServer(Time.deltaTime);
            }
            UpdateFlashlightView();
            UpdateModelPose();
            if (!IsOwner) return;

            PlaceOnLevelIfNeeded();
            bool menu = GameMenu.IsOpen || CheatPanel.IsOpen;
            if (Keyboard.current == null) return;

            bool cracking = Cracking != null || UsingTerminal != null || AtConsole != null; // busy at a dial, a lock or a terminal: no walking
            CursorMode = !menu && !Downed && !cracking && // in the hatch too: the stash and the sell area are in reach
                          (Controls.Held(GameAction.Hands) || HoldCursor || DevFlags.ForceHandsMode);
            bool freeCursor = menu || CursorMode;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeCursor;

            // Turning a safe's dial: the player stays put (the mouse turns the dial). In the hatch: look around, no walking.
            var input = DevFlags.Autopilot ?? PlayerInputState.FromDevices(!freeCursor && !cracking);
            if (InHatch) input = new PlayerInputState { Look = input.Look };
            EnterHatchIfNeeded();
            Motor.InHatch = InHatch;
            UpdateLadder(input, Time.deltaTime);
            Motor.Seated = SeatIndex >= 0 || Climbing; // on the ladder: the view turns, the ladder moves the body
            Motor.Tick(input, !menu && !Downed && !cracking, Downed, Head, Camera, Time.deltaTime);
            IntoDuctIfJumping();
            UpdateSeat(menu);
            Crouching = Motor.IsCrouching;
            SyncLookPitch();

            UpdateNoise();
            UpdateGas();
            UpdateHand();
            AtHatch = !InHatch && Hatch.IsNear(transform.position) && (!UseLook || Aimed<Hatch>() != null);
            UpdateInteractions();
            UpdateCallouts();
            UpdateFog();
        }

        /// <summary>Dark haze indoors (it only takes the distance away, it does not light anything), the gas fog outside.</summary>
        void UpdateFog()
        {
            var layout = ActiveLevel.Layout;
            if (layout == null || Camera.transform.position.y < LevelLayout.Underground)
            {
                // The sewer (and the basement's tunnels): a damp indoor haze, the background is the haze too (never the default
                // sky colour) — the same on the drive in the sewer and in the basement's approach (Level.DrivePath).
                GasView.Hide();
                if (layout == null && SewerLocation.Active == null) return;
                var a = VisualStyles.World;
                RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, a.IndoorFogDensity * 1.4f, Time.deltaTime * 3f);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, a.IndoorFog, Time.deltaTime * 4f);
                Camera.backgroundColor = RenderSettings.fogColor;
                return;
            }
            // The engine's fog is only the dark haze, inside and out. The gas is drawn in the world by the gas pass
            // (GasView): none at the walls, full 15 m out (lose sight of the house and you are lost), the sky above it.
            // Visual only — the gas still hurts where it did (UpdateGas).
            var air = VisualStyles.World;
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, air.IndoorFogDensity, Time.deltaTime * 3f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, air.IndoorFog, Time.deltaTime * 2f);
            Camera.backgroundColor = Color.black; // the gas pass adds the sky where nothing was drawn
            GasView.Show(layout, air, layout.IsInside(Camera.transform.position));
        }

        bool m_PlacedInHatch;

        /// <summary>Owner: the server let us into the hatch — stand in its middle (and leave it where we are when climbing out).</summary>
        void EnterHatchIfNeeded()
        {
            if (InHatch == m_PlacedInHatch) return;
            m_PlacedInHatch = InHatch;
            var layout = ActiveLevel.Layout;
            if (!InHatch || layout == null) return;
            TeleportOwner(Hatch.PositionOf(HatchIndex) + Vector3.up * 0.05f, transform.rotation);
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
            // Lying, watching a teammate or in god mode, the gas has nothing to do with you (no overlay, no knockout).
            if (Downed || Invulnerable) { Gas.Reset(); GasShare = 0f; return; }
            var effect = Gas.Tick(outside, Time.deltaTime);
            GasShare = Gas.Share;
            if (effect == GasExposure.Effect.Laugh) EmitNoise(Gas.Settings.LaughNoise);
            else if (effect == GasExposure.Effect.Knockout)
            {
                Link.SendGasKnockout();
            }
        }

        void UpdateInteractions()
        {
            // Knocked down with HP left: Space gets you up — when the robot that hit you has gone.
            if (Downed && !GameMenu.IsOpen && CanGetUp && Controls.Pressed(GameAction.Jump))
            {
                Link.SendGetUp();
            }
            if (GameMenu.IsOpen) { ReviveProgress = 0; ReviveTarget = null; return; }
            if (Controls.Pressed(GameAction.Unstuck) && !CursorMode) RequestUnstuck();
            // A failed run is restarted by any player — usually everybody is down by then.
            var game = GameManager.Instance;
            if (game != null && Controls.Pressed(GameAction.Restart) && game.Phase == RunPhase.Failed && !CursorMode)
                game.RequestRestart();
            if (Downed) { ReviveProgress = 0; ReviveTarget = null; LeaveAllModes(); return; }
            bool holdE = Controls.Held(GameAction.Interact) || DevFlags.AutoInteract;
            if (UsingTerminal != null) { UpdateTerminal(holdE); return; }

            // Revive: hold E next to a downed teammate.
            ReviveTarget = null;
            foreach (var p in All)
            {
                if (p == this || !p.Downed) continue;
                var at = p.transform.position;
                if (View is { } view && !view.At(at, at + Vector3.up * 0.5f, 0.7f)) continue; // looking at them
                if ((at - transform.position).magnitude <= m_Interaction.ReviveDistance) { ReviveTarget = p; break; }
            }
            if (ReviveTarget != null && holdE)
            {
                ReviveProgress += Time.deltaTime;
                if (ReviveProgress >= m_Interaction.ReviveTime)
                {
                    Link.SendRevive(ReviveTarget);
                    ReviveProgress = 0;
                }
            }
            else ReviveProgress = 0;

            // Hack: hold E next to a device, or behind a robot that is not chasing you.
            // Walking away keeps the progress on that machine: coming back goes on from there.
            HackTarget = ReviveTarget == null ? FindHackTarget() : null;
            if (HackTarget != null && holdE && m_HackHeld.Hold(HackTarget, Time.deltaTime, m_Interaction.HackHoldTime))
            {
                Link.SendHack(HackTarget);
            }
            HackProgress = m_HackHeld.Of(HackTarget);

            // A locked door: hold E next to it to hack the lock (when nothing more urgent is in reach).
            DoorTarget = ReviveTarget == null && HackTarget == null ? AutoDoor.LockedNear(transform.position, AutoDoor.HackReach, View) ?? StorageDoorAtTerminal() : null;
            if (DoorTarget != null && holdE && m_DoorHeld.Hold(DoorTarget, Time.deltaTime, m_Interaction.DoorHackTime))
            {
                Link.SendUse(UseKind.DoorLock, DoorTarget.Index);
            }
            DoorProgress = m_DoorHeld.Of(DoorTarget);

            UpdateSafe();
            UpdateConsole();
            UpdateStorage();
            UpdateTerminal(holdE);
            UpdateUse(holdE);
            if (Controls.Pressed(GameAction.Gadget) && !CursorMode && m_Inventory != null && m_Inventory.Holds(Items.ItemCatalog.Stun))
            {
                Link.SendGadget();
            }
            // the storage robot: B calls it here, N sends it to the exit (once the exit room is known)
            if (!CursorMode && !Downed)
                foreach (var (action, command) in new[] { (GameAction.BotCall, (byte)0), (GameAction.BotExit, (byte)1) })
                    if (Controls.Pressed(action))
                    {
                        Link.SendBot(command);
                    }

            var gm = GameManager.Instance;
            if (gm == null || !Controls.Pressed(GameAction.Hatch) || gm.Phase != RunPhase.Playing) return;
            // The hatch: hide in it (the team leaves when everyone is in) or climb back out.
            if (InHatch) SetHatch(false);
            else if (AtHatch) SetHatch(true);
        }

        // ------------------------------------------------------------------ watching a teammate while knocked out

        /// <summary>Owner, knocked out: the teammate whose eyes the camera looks through (null = our own, lying on the floor).</summary>
        public PlayerController Watching { get; private set; }
        int m_WatchIndex;
        Quaternion m_WatchRotation;
        static readonly List<PlayerController> s_Mates = new();

        void LateUpdate()
        {
            if (!IsOwner) { HoldRemoteSeat(); return; }
            if (Camera == null) return;
            if (!ReferenceEquals(UsingTerminal, null) && UsingTerminal == null) StopTerminal(); // the house went away
            if (UsingTerminal != null) ShowTerminalView();
            else UpdateWatching();
        }

        /// <summary>
        /// Someone else in a seat of the car on its drive (moved by the clock here, Level.DrivePath): on their seat here, facing
        /// the road — their own peer's position comes late over the network and would trail behind the car.
        /// </summary>
        void HoldRemoteSeat()
        {
            var car = Vehicles.Car.Active;
            if (SeatIndex < 0 || car == null || !car.Scripted || SeatIndex >= car.Seats.Length || car.Seats[SeatIndex] == null) return;
            transform.SetPositionAndRotation(car.Seats[SeatIndex].position - Vector3.up * SeatDrop, car.transform.rotation);
        }

        // ------------------------------------------------------------------ server

        public void HitServer(Vector3 source)
        {
            if (!IsServer || Invulnerable || !m_Health.TryHit(Time.time)) return;
            PublishHealth();
            var away = transform.position - source;
            away.y = 0;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            Link.SendHitFeedback(away);
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
            Link.SendNoise(transform.position, radius);
        }

        /// <summary>Server: the owner made a noise.</summary>
        public void OnNoiseRequest(Vector3 position, float radius)
        {
            if (Downed || Invisible) return; // the invisibility cheat: robots ignore this player completely
            NoiseSystem.EmitServer(position, Mathf.Min(radius, 20f), ClientId);
        }

        /// <summary>Server: the owner passed out from the gas.</summary>
        public void OnGasKnockoutRequest()
        {
            if (Downed || Invulnerable) return;
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
            if (target is Enemies.Detector d && GameManager.Instance != null) GameManager.Instance.TeamHackServer(d);
            else target.HackServer(m_Interaction.HackDuration);
        }
    }
}
