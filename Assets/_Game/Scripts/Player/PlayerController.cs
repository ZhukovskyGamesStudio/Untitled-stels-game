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
        /// <summary>The body's capsule radius, metres: as slim as the person drawn — one stands right up against furniture and walls.</summary>
        public const float BodyRadius = 0.25f;
        public const float CrouchHeight = 1.1f;

        [Header("Refs")]
        public Transform Head;
        public Camera Camera;
        public AudioListener Listener;
        [Tooltip("Plays this player's voice chat from the mouth (others hear it; the owner does not).")]
        public Stealth.Audio.VoicePlayback Voice;
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
        /// <summary>
        /// Owner-owned: the head's turn off the body in degrees, + to the right (updated with the pitch): the body held on the
        /// ladder or in a seat (<see cref="PlayerMotor.HeadYaw"/>), so teammates see the head turn where the player looks.
        /// </summary>
        public sbyte LookYaw { get; private set; }
        /// <summary>The head's turn as shown here: the owner's own, a remote player's eased to what came over the network.</summary>
        public float ShownLookYaw => IsOwner && Motor != null ? Motor.HeadYaw : m_ShownYaw;
        float m_ShownYaw;
        /// <summary>
        /// Owner-owned: the owner's camera as it is, in the body's own space (its place off the player's position, its turn
        /// off the player's rotation: head height, bob, landing dip, shake, the look; a terminal's picture too) and its field
        /// of view — a knocked-out teammate watching this player sees exactly through these eyes.
        /// </summary>
        public Vector3 ViewOffset { get; private set; } = new(0f, 1.6f, 0f);
        public Quaternion ViewTurn { get; private set; } = Quaternion.identity;
        public float ViewFov { get; private set; } = 70f;
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
        public float ReviveShare => ReviveProgress / FreeTimeOf(ReviveTarget);
        /// <summary>0..1 of holding E at a robot / device.</summary>
        public float HackShare => HackProgress / HackHoldTime;
        /// <summary>Seconds of E the target in reach takes: a hack, or the upgraded screwdriver taking a device apart.</summary>
        float HackHoldTime => Unscrewing ? Configs.Tuning.Items.Crafted.UnscrewTime : m_Interaction.HackHoldTime;
        /// <summary>Owner: E at <see cref="HackTarget"/> (a robot or a device) takes it apart with the upgraded screwdriver (not a hack).</summary>
        public bool Unscrewing { get; private set; }
        public float HackReach => m_Interaction.HackReach;
        public float Height => Crouching ? CrouchHeight : StandHeight;
        public bool IsSprinting => Motor != null && Motor.IsSprinting;

        /// <summary>The department of this player (by slot): an IT person from backend, QA, sysadmins or design.</summary>
        public Department Department => PlayerLooks.DepartmentOfClass(LoadoutClass); // the class dresses the player

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
        public void RequestCheat(Cheat cheat, bool on) => RequestCheat(cheat, on ? 1 : 0);

        /// <summary>Owner: a cheat from the F4 panel with its argument (<see cref="CheatArgs"/>).</summary>
        public void RequestCheat(Cheat cheat, int arg)
        {
            // the owner runs its own gas and footsteps: it keeps the flags too (the server's copy is what robots use)
            if (cheat == Cheat.Invulnerable) Invulnerable = arg != 0;
            if (cheat == Cheat.Invisible) Invisible = arg != 0;
            Link.SendCheat(cheat, arg);
        }

        public void CheatServer(Cheat cheat, bool on) => CheatServer(cheat, on ? 1 : 0);

        public void CheatServer(Cheat cheat, int arg)
        {
            var gm = GameManager.Instance;
            bool on = arg != 0;
            switch (cheat)
            {
                case Cheat.Invulnerable: Invulnerable = on; break;
                case Cheat.Invisible: Invisible = on; break;
                case Cheat.KillRobots: gm?.KillAllRobotsServer(); break;
                case Cheat.FillQuota: gm?.FillQuotaServer(); break;
                case Cheat.SpawnItem:
                {
                    var (shape, color) = CheatArgs.ItemOf(arg);
                    gm?.CheatItemServer(this, shape, color);
                    break;
                }
                case Cheat.SetUpgrade:
                {
                    var (upgrade, level) = CheatArgs.UpgradeOf(arg);
                    gm?.CheatUpgradeServer(upgrade, level);
                    break;
                }
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
            m_CaptiveSettings = tuning.Captive;
            m_Ailments = tuning.Ailments;
            Motor.Landed += OnLanded;
        }

        void OnLanded(float impactSpeed)
        {
            float radius = FootstepNoise.LandingRadius(impactSpeed);
            if (IsOwner && IsSpawned && radius > 0f) EmitNoise(radius);
            HardLanding(impactSpeed);
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
            if (isServer) LoadoutClass = PlayerLooks.DefaultClass(PlayerLooks.SlotOf(clientId)); // until picked on the start screen
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
        public void ApplyOwnerState(bool crouching, sbyte lookPitch, string displayName, BodyPose pose = BodyPose.None, sbyte lookYaw = 0)
        {
            Crouching = crouching;
            LookPitch = lookPitch;
            LookYaw = lookYaw;
            DisplayName = displayName ?? "";
            Pose = pose;
        }

        /// <summary>Other peers: the owner's camera (<see cref="ViewOffset"/>, <see cref="ViewTurn"/>, <see cref="ViewFov"/>).</summary>
        public void ApplyView(Vector3 offset, Quaternion turn, float fov)
        {
            ViewOffset = offset;
            ViewTurn = turn;
            ViewFov = fov;
        }

        /// <summary>Owner, after the view is placed for the frame: the camera in the body's space, for the teammates watching.</summary>
        void PublishView()
        {
            var cam = Camera.transform;
            var body = transform.rotation;
            var inverse = Quaternion.Inverse(body);
            ViewOffset = inverse * (cam.position - transform.position);
            ViewTurn = inverse * cam.rotation;
            ViewFov = Camera.fieldOfView;
        }

        /// <summary>Team colour on the small things only, the department's clothes, skin, dark details (PlayerLooks).</summary>
        void ApplyColor()
        {
            int slot = PlayerLooks.SlotOf(ClientId);
            m_LookDepartment = Department;
            if (Model != null)
            {
                VisualStyles.SetVariant(Model.gameObject, (int)m_LookDepartment); // the department's model in the current style
                PlayerLooks.Paint(Model, slot, m_LookDepartment);
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
                UpdateAilmentsServer();
            }
            UpdateFlashlightView();
            UpdateModelPose();
            ShowHeld();
            if (!IsOwner) return;

            PlaceOnLevelIfNeeded();
            bool menu = GameMenu.IsOpen || CheatPanel.IsOpen;
            if (Keyboard.current == null) return;

            bool cracking = Cracking != null || UsingTerminal != null || AtConsole != null; // busy at a dial, a lock or a terminal: no walking
            UpdateBench(menu);
            UpdateUpgrades(menu);
            if (AtUpgrades) cracking = true; // at the car upgrade bench: the mouse picks, nobody walks
            CursorMode = !menu && !Downed && !cracking && // in the hatch too: the stash and the sell area are in reach
                          (Controls.Held(GameAction.Hands) || HoldCursor || DevFlags.ForceHandsMode || AtBench || AtShelf >= 0);
            // the run's start screen: the team stands in a row, the mouse picks a class and an item, nobody walks
            bool pickingNow = GameManager.Instance != null && GameManager.Instance.Picking;
            if (pickingNow) cracking = true;
            bool freeCursor = menu || CursorMode || pickingNow || AtUpgrades;
            Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freeCursor;

            // Turning a safe's dial: the player stays put (the mouse turns the dial). In the hatch: look around, no walking.
            var input = DevFlags.Autopilot ?? PlayerInputState.FromDevices(!freeCursor && !cracking);
            if (InHatch) input = new PlayerInputState { Look = input.Look };
            input = AilInput(input);
            EnterHatchIfNeeded();
            Motor.InHatch = InHatch;
            UpdateLadder(input, Time.deltaTime);
            Motor.Seated = SeatIndex >= 0 || Climbing; // on the ladder: the view turns, the ladder moves the body
            HoldLook();
            UpdateGearMotion();
            UpdateCrafted();
            // carried by a robot or held in a place: the body stays there (the view still turns)
            if (HoldCaptive()) input = new PlayerInputState { Look = input.Look };
            else
            Motor.Tick(input, !menu && !Downed && !cracking, Downed, Head, Camera, Time.deltaTime);
            IntoDuctIfJumping();
            UpdateSeat(menu);
            // seated, the body is put on its seat again now that the player is (UpdateModelPose ran before the seat moved
            // the player with the car: the body, and the view held to it, trailed by a frame's drive — the car shook)
            if (SeatIndex >= 0) FollowRig();
            Crouching = Motor.IsCrouching;
            SyncLookPitch();

            UpdateNoise();
            UpdateGas();
            UpdateHand();
            UpdateInteractions();
            WorkSounds();
            UpdateCallouts();
            UpdateFog();
        }

        /// <summary>Dark haze indoors (it only takes the distance away, it does not light anything), the gas fog outside.</summary>
        /// <summary>Underground: the haze's least density, and how much of the style's ambient light is left.</summary>
        const float UndergroundFog = 0.085f, UndergroundAmbient = 0.15f;
        static readonly int StyDarkId = Shader.PropertyToID("_StyDark");
        bool m_AmbientDimmed;
        static readonly Color SmokeColor = new(0.32f, 0.33f, 0.35f);

        /// <summary>
        /// The air of where the camera is, at once (not eased in): after a house is built under a camera in the sewer the
        /// game's atmosphere and the style were put back on — easing back from the street's haze and the style's full
        /// ambient read as the style changing for a second.
        /// </summary>
        public void SnapAir() => UpdateFog(true);

        void UpdateFog(bool snap = false)
        {
            float Toward(float from, float to, float rate) => snap ? to : Mathf.MoveTowards(from, to, Time.deltaTime * rate);
            Color Blend(Color from, Color to, float rate) => snap ? to : Color.Lerp(from, to, Time.deltaTime * rate);
            var layout = ActiveLevel.Layout;
            if (layout == null || Camera.transform.position.y < LevelLayout.Underground)
            {
                // The sewer (and the basement's tunnels): a damp indoor haze, the background is the haze too (never the default
                // sky colour) — the same on the drive in the sewer and in the basement's approach (Level.DrivePath).
                GasView.Hide();
                if (layout == null && SewerLocation.Active == null) return;
                var a = VisualStyles.World;
                // a thick black haze: the far tunnel sinks into black whatever the style's indoor haze looks like; the style's
                // own ambient (the picture's, not gameplay's) all but gone, and the ink lines fade where nothing is lit
                RenderSettings.fogDensity = Toward(RenderSettings.fogDensity, Mathf.Max(UndergroundFog, a.IndoorFogDensity * 3f), 3f);
                RenderSettings.fogColor = Blend(RenderSettings.fogColor, a.IndoorFog * 0.1f, 4f);
                RenderSettings.ambientLight = Blend(RenderSettings.ambientLight, VisualStyles.StyleAmbient * UndergroundAmbient, 4f);
                VisualStyles.Underground = Toward(VisualStyles.Underground, 1f, 1.2f); // the tunnels' lamps as the style shows them there
                Shader.SetGlobalFloat(StyDarkId, 1f);
                m_AmbientDimmed = true;
                Camera.backgroundColor = RenderSettings.fogColor;
                return;
            }
            if (m_AmbientDimmed)
            {
                m_AmbientDimmed = false;
                RenderSettings.ambientLight = VisualStyles.StyleAmbient;
                Shader.SetGlobalFloat(StyDarkId, 0f);
            }
            // The engine's fog is only the dark haze, inside and out. The gas is drawn in the world by the gas pass
            // (GasView): none at the walls, full 15 m out (lose sight of the house and you are lost), the sky above it.
            // Visual only — the gas still hurts where it did (UpdateGas).
            var air = VisualStyles.World;
            VisualStyles.Underground = Toward(VisualStyles.Underground, 0f, 1.2f);
            // in a smoke machine's cloud the air is thick and grey: a few steps of sight
            RenderSettings.fogDensity = Toward(RenderSettings.fogDensity, InSmoke ? Mathf.Max(air.IndoorFogDensity, m_Ailments.SmokeDensity) : air.IndoorFogDensity, InSmoke ? 1.2f : 3f);
            RenderSettings.fogColor = Blend(RenderSettings.fogColor, InSmoke ? SmokeColor : air.IndoorFog, 2f);
            Camera.backgroundColor = Color.black; // the gas pass adds the sky where nothing was drawn
            GasView.Show(layout, air, layout.IsInside(Camera.transform.position));
        }

        bool m_PlacedInHatch;

        float m_HatchGlideUntil;

        /// <summary>
        /// Owner: the server let us into the hatch — slide down into its middle (no jump: the body moves there over a moment,
        /// and the model sinks, <see cref="HatchDepth"/>); climbing out leaves us where we are.
        /// </summary>
        void EnterHatchIfNeeded()
        {
            if (InHatch != m_PlacedInHatch)
            {
                m_PlacedInHatch = InHatch;
                m_HatchGlideUntil = InHatch ? Time.time + 1.5f : 0f;
            }
            if (!InHatch || ActiveLevel.Layout == null || Time.time > m_HatchGlideUntil) return;
            var target = Hatch.PositionOf(HatchIndex) + Vector3.up * 0.05f;
            var away = target - transform.position;
            away.y = 0f;
            if (away.magnitude < 0.02f) { m_HatchGlideUntil = 0f; return; }
            m_Controller.Move(Vector3.ClampMagnitude(away, HatchGlideSpeed * Time.deltaTime));
        }

        /// <summary>How fast a body slides into a hatch's middle, metres a second.</summary>
        const float HatchGlideSpeed = 2.5f;

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
            // (an exterminator's cloud is the same gas, indoors)
            outside |= Hazards.At(HazardKind.Gas, transform.position, Session.ServerTime);
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
                if (p == this || !p.CanBeFreed) continue;
                var at = p.transform.position;
                if (View is { } view && !view.At(at, at + Vector3.up * 0.5f, 0.7f)) continue; // looking at them
                if ((at - transform.position).magnitude <= m_Interaction.ReviveDistance) { ReviveTarget = p; break; }
            }
            if (ReviveTarget != null && holdE)
            {
                ReviveProgress += WorkDt;
                if (ReviveProgress >= FreeTimeOf(ReviveTarget))
                {
                    Link.SendRevive(ReviveTarget);
                    ReviveProgress = 0;
                }
            }
            else ReviveProgress = 0;

            // Hack: hold E next to a device, or behind a robot that is not chasing you.
            // Walking away keeps the progress on that machine: coming back goes on from there.
            HackTarget = ReviveTarget == null ? FindHackTarget() : null;
            // the upgraded screwdriver (once per house): a robot or a device is taken apart instead of hacked — off for good,
            // no hack slot, its innards fall out (a hack is the keyboard's: two slots, three with the upgraded one)
            Unscrewing = HackTarget != null && CanUnscrew(m_Inventory);
            // (a hack needs the keyboard in the hand; taking a device apart needs only the screwdriver)
            if (HackTarget != null && !HackNeedsKeyboard && holdE && m_HackHeld.Hold(HackTarget, WorkDt, HackHoldTime))
            {
                if (Unscrewing) Link.SendGear(GearAction.Unscrew, HackTarget, HackTarget.Position);
                else Link.SendHack(HackTarget);
            }
            HackProgress = m_HackHeld.Of(HackTarget);

            // A locked door: hold E next to it to hack the lock (when nothing more urgent is in reach).
            DoorTarget = ReviveTarget == null && HackTarget == null ? AutoDoor.LockedNear(transform.position, AutoDoor.HackReach, View) ?? StorageDoorAtTerminal() : null;
            if (DoorTarget != null && holdE && m_DoorHeld.Hold(DoorTarget, WorkDt, m_Interaction.DoorHackTime))
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
            // (the exit is a shaft with a ladder like the entry's: nothing to sit in; out of a hatch if ever put in one)
            if (InHatch) SetHatch(false);
        }

        // ------------------------------------------------------------------ watching a teammate while knocked out

        /// <summary>Owner, knocked out: the teammate whose eyes the camera looks through (null = our own, lying on the floor).</summary>
        public PlayerController Watching { get; private set; }
        int m_WatchIndex;
        Vector3 m_WatchOffset;
        Quaternion m_WatchTurn = Quaternion.identity;
        /// <summary>The local player looks through this one's eyes now (knocked out, watching): their head is out of the way.</summary>
        bool WatchedHere => !IsOwner && Local != null && Local.Watching == this;
        static readonly List<PlayerController> s_Mates = new();

        void LateUpdate()
        {
            ShowCalling();
            if (!IsOwner) { HoldRemoteSeat(); return; }
            if (Camera == null) return;
            if (!ReferenceEquals(UsingTerminal, null) && UsingTerminal == null) StopTerminal(); // the house went away
            bool picking = GameManager.Instance != null && GameManager.Instance.Picking;
            if (UsingTerminal != null) ShowTerminalView();
            else if (picking) ShowPickingView();
            else
            {
                if (m_PickingView) { m_PickingView = false; Camera.transform.localPosition = Vector3.zero; Camera.transform.localRotation = Quaternion.identity; }
                UpdateWatching();
            }
            // through a teammate's eyes the body lying on the floor is seen like anyone's; on the start screen the team stands in a row
            ShowOwnBody(Watching != null || picking);
            if (Watching == null) PublishView(); // (watching, the camera is someone else's: the last own view stays)
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
            // …and the body on its seat again now that the player is: it was put there in Update, off where the network's
            // transform had the player (behind the car) or the frame before — a frame's drive off the seat; while the next
            // house is built under the drive the frames are long, and the teammate shook about the car by metres
            FollowRig();
        }

        // ------------------------------------------------------------------ server

        public void HitServer(Vector3 source, DownCause cause = DownCause.Servant)
        {
            if (!IsServer || Invulnerable) return;
            bool wasDown = Downed;
            if (!m_Health.TryHit(Time.time)) return;
            DownedBy = cause;
            HeldSince = Time.time;
            PublishHealth();
            if (Downed && !wasDown) GameManager.Instance?.NoteServer(this, StatKind.Downs);
            var away = transform.position - source;
            away.y = 0;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            Link.SendHitFeedback(away);
        }

        public void ReviveServer() => FreeServer();

        /// <summary>Lying with nobody holding them (a carrier may lift them).</summary>
        public bool CanBeCarried => IsSpawned && KnockedOut && Captive == CaptiveKind.None && !InHatch;

        public void ResetForLevelServer()
        {
            m_Health = HealthState.Full(Time.time, m_HealthSettings);
            ClearCaptivityServer();
            ClearAilmentsServer();
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
        /// <summary>This player's own voice was sent with this noise radius: the HUD's speaker shows it like a step (the server makes the robots' noise, VoiceHub).</summary>
        public void HearOwnVoice(float radius)
        {
            if (radius <= 0f) return;
            if (Time.time - LastNoiseTime > 0.3f || radius >= LastNoiseRadius) LastNoiseRadius = radius;
            LastNoiseTime = Time.time;
        }

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
            if (radius >= 6f) GameManager.Instance?.NoteServer(this, StatKind.Noise); // (a loud one: a run, a landing, not every step)
        }

        /// <summary>Server: out at once, whatever the health (a sedative).</summary>
        public void KnockOutServer(DownCause cause)
        {
            if (!IsServer || Downed || Invulnerable) return;
            m_Health.Knockout(Time.time);
            DownedBy = cause;
            HeldSince = Time.time;
            PublishHealth();
            GameManager.Instance?.NoteServer(this, StatKind.Downs);
        }

        /// <summary>Server: the owner passed out from the gas.</summary>
        public void OnGasKnockoutRequest()
        {
            if (Downed || Invulnerable) return;
            m_Health.Knockout(Time.time);
            DownedBy = DownCause.Self;
            HeldSince = Time.time;
            PublishHealth();
        }

        /// <summary>Server: the owner finished holding E over a downed teammate.</summary>
        public void OnReviveRequest(PlayerController other)
        {
            if (Downed || other == null || !other.CanBeFreed) return; // (not off a robot's back: stop the robot first)
            if ((other.transform.position - transform.position).magnitude > m_Interaction.ReviveDistance + 1f) return;
            other.FreeServer();
            GameManager.Instance?.NoteServer(this, StatKind.Revives);
        }

        /// <summary>Server: the owner finished holding E at a robot / device.</summary>
        public void OnHackRequest(IHackable target)
        {
            if (Downed || target == null || !target.CanBeHackedFrom(transform.position, HackReach + 0.5f) || !target.ReachableFrom(Hands)) return;
            // every robot and sensor needs the keyboard in the hand (terminals hack for anyone); without it, a robot that
            // carries something is only searched: it drops it
            if (target is Enemies.Detector && !CanHackByHand)
            {
                if (target is Enemies.RobotController robot && robot.Cargo >= 0) robot.SearchServer();
                return;
            }
            if (target is Enemies.Detector d && GameManager.Instance != null) { GameManager.Instance.TeamHackServer(d); GameManager.Instance.NoteHackServer(this); }
            else target.HackServer(m_Interaction.HackDuration);
        }
    }
}
