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
            if (m_Link != null) m_Link.SendCheat(cheat, on);
            else CheatServer(cheat, on);
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

        // ------------------------------------------------------------------ the hand: flashlight

        [Tooltip("The flashlight's beam (off unless a charged flashlight in the hand is switched on).")]
        public Light Flashlight;
        [Tooltip("The hand holding the flashlight, seen in the first-person view while a flashlight is in the hand slot (owner only).")]
        public GameObject ViewFlashlight;
        [Tooltip("The shelter map held open in front of the face while it is in the hand slot (every peer sees it: a thing in the world, not UI).")]
        public GameObject HeldMap;
        /// <summary>The flashlight in the hand shines (server-decided, replicated).</summary>
        public bool FlashlightOn { get; private set; }
        float m_Drain;

        /// <summary>Owner: the hand key — use what the hand holds.</summary>
        void UpdateHand()
        {
            if (!Controls.Pressed(GameAction.UseHand) || CursorMode || Downed) return;
            if (m_Link != null) m_Link.SendUseHand();
            else UseHandServer();
        }

        /// <summary>
        /// Server: the hand key. A flashlight switches on (if it has charge) or off; a battery goes into the first flashlight
        /// in the backpack that is not full.
        /// </summary>
        public void UseHandServer()
        {
            var inv = GetComponent<PlayerInventory>();
            int hand = inv.HandIndex();
            if (Downed || hand < 0) return;
            var e = inv.Items[hand];
            if (e.Shape == Items.ItemCatalog.Flashlight)
            {
                FlashlightOn = !FlashlightOn && e.Color > 0;
                return;
            }
            if (e.Shape == Items.ItemCatalog.Battery)
                for (int i = 0; i < inv.Items.Count; i++)
                    if (inv.CanRecharge(i)) { inv.RechargeServer(hand, i); return; }
        }

        /// <summary>Server: the flashlight drains while it shines; it goes out when empty, put away or when its holder falls.</summary>
        void UpdateFlashlightServer(float dt)
        {
            if (!FlashlightOn) { m_Drain = 0f; return; }
            var inv = GetComponent<PlayerInventory>();
            int hand = inv.HandIndex();
            if (Downed || InHatch || hand < 0 || inv.Items[hand].Shape != Items.ItemCatalog.Flashlight || inv.Items[hand].Color == 0)
            {
                FlashlightOn = false;
                return;
            }
            float seconds = Mathf.Max(1f, Configs.Tuning.Items.Gadgets.FlashlightSeconds);
            m_Drain += dt * Items.ItemCatalog.FullCharge / seconds;
            if (m_Drain < 1f) return;
            int used = Mathf.FloorToInt(m_Drain);
            m_Drain -= used;
            int charge = Mathf.Max(0, inv.Items[hand].Color - used);
            inv.SetChargeServer(hand, (byte)charge);
            if (charge == 0) FlashlightOn = false;
        }

        /// <summary>Every peer: the beam where the holder looks.</summary>
        void UpdateFlashlightView()
        {
            if (HeldMap != null)
            {
                var inv = GetComponent<PlayerInventory>();
                int hand = inv != null ? inv.HandIndex() : -1;
                bool show = hand >= 0 && inv.Items[hand].Shape == Items.ItemCatalog.ShelterMap && !Downed && !InHatch && UsingTerminal == null;
                if (HeldMap.activeSelf != show) HeldMap.SetActive(show);
                // a remote player's head does not pitch: the map follows their look up and down
                if (show && !IsOwner) HeldMap.transform.localRotation = (m_HeldMapRot ??= HeldMap.transform.localRotation) * Quaternion.Euler(LookPitch * 0.6f, 0f, 0f);
            }
            if (ViewFlashlight != null)
            {
                var inv = GetComponent<PlayerInventory>();
                int hand = inv != null ? inv.HandIndex() : -1;
                bool show = IsOwner && hand >= 0 && inv.Items[hand].Shape == Items.ItemCatalog.Flashlight && !Downed && !InHatch && UsingTerminal == null;
                if (ViewFlashlight.activeSelf != show) ViewFlashlight.SetActive(show);
            }
            if (Flashlight == null) return;
            bool on = FlashlightOn && !Downed && !InHatch;
            if (Flashlight.enabled != on) Flashlight.enabled = on;
            if (on && !IsOwner) Flashlight.transform.localRotation = Quaternion.Euler(LookPitch, 0f, 0f); // the head of a remote player does not pitch
            if (!on) return;
            // close to a surface the lamp dims (no white blown-out spot): the nearest thing in front that is not this player
            if (m_FlashBase < 0f)
            {
                // the intensity as built, without the style's visual scale (LightSource writes that over the light)
                var source = Flashlight.GetComponent<LightSource>();
                m_FlashBase = source != null ? source.RawIntensity : Flashlight.intensity;
            }
            var t = Flashlight.transform;
            float d = FlashNear;
            int n = Physics.RaycastNonAlloc(t.position, t.forward, s_FlashHits, FlashNear, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (s_FlashHits[i].distance < d && !s_FlashHits[i].collider.transform.IsChildOf(transform)) d = s_FlashHits[i].distance;
            Flashlight.intensity = m_FlashBase * PlayerRules.FlashlightNear(d, FlashNear);
        }

        Quaternion? m_HeldMapRot;              // the held map's pose as built (a remote holder's pitch is added to it)
        const float FlashNear = 1.6f;          // closer than this the flashlight dims (metres)
        float m_FlashBase = -1f;               // its intensity as built
        static readonly RaycastHit[] s_FlashHits = new RaycastHit[8];

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
            Motor.Seated = SeatIndex >= 0;
            Motor.Tick(input, !menu && !Downed && !cracking, Downed, Head, Camera, Time.deltaTime);
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
            if (layout == null)
            {
                // The sewer: a damp indoor haze, the background is the haze too (never the default sky colour).
                GasView.Hide();
                if (SewerLocation.Active == null) return;
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

        /// <summary>How deep the model sits while hiding: only the head looks out of the hatch.</summary>
        public const float HatchDepth = 1.5f;
        float m_ModelSink;

        GameObject m_RigPrefab;
        HumanRig m_Rig;

        /// <summary>What other players see of this one: the style's rigged human if it has one, otherwise the model.</summary>
        public Transform Visual => m_Rig != null ? m_Rig.transform : Model;

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

        Transform m_LegL, m_LegR;
        Quaternion m_LegRestL, m_LegRestR;
        float m_StridePhase, m_LegSwing;

        /// <summary>
        /// The legs (parts BodyLegL / BodyLegR hanging from the hip) swing in step with the walk: one step per
        /// <see cref="StepLength"/> metres, wider when running, still when standing, crouched lower, none in the hatch or lying.
        /// </summary>
        void SwingLegs(float speed)
        {
            if (m_LegL == null && Model != null)
                foreach (var t in Model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "BodyLegL") { m_LegL = t; m_LegRestL = t.localRotation; }
                    else if (t.name == "BodyLegR") { m_LegR = t; m_LegRestR = t.localRotation; }
                }
            if (m_LegL == null || m_LegR == null) return;
            if (SeatIndex >= 0)
            {
                // sitting: thighs forward
                m_LegL.localRotation = Quaternion.Slerp(m_LegL.localRotation, m_LegRestL * Quaternion.Euler(-80f, 0f, 0f), 1f - Mathf.Exp(-10f * Time.deltaTime));
                m_LegR.localRotation = Quaternion.Slerp(m_LegR.localRotation, m_LegRestR * Quaternion.Euler(-80f, 0f, 0f), 1f - Mathf.Exp(-10f * Time.deltaTime));
                m_LegSwing = 0f;
                return;
            }
            bool still = Downed || InHatch || speed < 0.3f;
            float amplitude = still ? 0f : PlayerRules.LegSwing(speed, Crouching);
            m_LegSwing = Mathf.MoveTowards(m_LegSwing, amplitude, Time.deltaTime * 160f);
            if (!still) m_StridePhase += speed / StepLength * Mathf.PI * Time.deltaTime;
            else if (m_LegSwing <= 0.01f) m_StridePhase = 0f; // the next walk starts from the rest pose
            float angle = Mathf.Sin(m_StridePhase) * m_LegSwing;
            m_LegL.localRotation = m_LegRestL * Quaternion.Euler(angle, 0f, 0f);
            m_LegR.localRotation = m_LegRestR * Quaternion.Euler(-angle, 0f, 0f);
        }

        /// <summary>Metres per step of the walking animation (the footstep sounds use the same length).</summary>
        const float StepLength = 1.25f;

        void UpdateModelPose()
        {
            UpdateRig();
            if (Model == null) return;
            m_ModelSink = Mathf.MoveTowards(m_ModelSink, InHatch ? HatchDepth : 0f, Time.deltaTime * 4f);
            if (IsOwner) Model.localPosition = Vector3.down * m_ModelSink;
            float y = Downed ? 0.35f : (Crouching ? 0.65f : 1f);
            Model.localScale = new Vector3(1f, Mathf.MoveTowards(Model.localScale.y, y, Time.deltaTime * 4f), 1f);
            if (IsOwner) SwingLegs(Motor.HorizontalSpeed);
            if (IsOwner || Time.deltaTime <= 0f) { FollowRig(); return; }

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
            // the bob follows the steps: up at mid-stride, down when a foot lands (two per leg cycle)
            Model.localPosition = new Vector3(0, Mathf.Abs(Mathf.Cos(m_StridePhase)) * 0.035f * walk - m_ModelSink, 0);
            Model.localRotation = Quaternion.Euler(Mathf.Clamp(m_RemoteSpeed * 0.6f, 0f, 4f), 0, Mathf.Sin(m_StridePhase) * 1.5f * walk);
            SwingLegs(m_RemoteSpeed);
            FollowRig();
        }

        /// <summary>The style's rigged human sinks into the hatch with the model (it is a sibling of the model, not its child).</summary>
        void FollowRig()
        {
            // in the car every peer puts the model right on its seat (the synced position lags behind the moving car)
            var car = Vehicles.Car.Active;
            int seat = SeatIndex;
            if (seat >= 0 && car.Seats[seat] != null && Model != null)
            {
                Model.SetPositionAndRotation(car.Seats[seat].position - Vector3.up * ModelUnderSeat, car.transform.rotation);
                Model.localScale = Vector3.one;
            }
            if (m_Rig != null && Model != null) m_Rig.transform.localPosition = Model.localPosition;
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

        Vector3? m_ServerPlace;
        float m_ServerPlaceYaw;

        /// <summary>Peers: the owner's identity arrived; the server gives a player who came back their things.</summary>
        public void ApplyIdentity(string identity)
        {
            if (string.IsNullOrEmpty(identity) || identity == Identity) return;
            Identity = identity;
            if (IsServer) GameManager.Instance?.PlayerJoinedServer(this);
        }

        /// <summary>Server: tell the owner where to stand (a reconnect).</summary>
        public void PlaceServer(Vector3 position, float yaw)
        {
            if (m_Link != null) m_Link.PlaceOwner(position, yaw);
            else PlaceFromServer(position, yaw);
        }

        /// <summary>
        /// Owner: whether this player already stands where the current location put them (the sewer's spawn, the house's
        /// hatch, or the server's spot after a reconnect). Until then the screen stays black (<see cref="UI.LoadingRules"/>).
        /// </summary>
        public bool PlacedHere
        {
            get
            {
                if (m_ServerPlace != null) return false;
                var sewer = SewerLocation.Active;
                if (sewer != null) return m_PlacedSewer == sewer;
                var layout = ActiveLevel.Layout;
                return layout != null && (m_PlacedSeed == layout.Seed || layout.PlayerSpawns.Count == 0);
            }
        }

        /// <summary>Owner: the server's spot wins over the usual spawn, once the house or the sewer is built here.</summary>
        public void PlaceFromServer(Vector3 position, float yaw)
        {
            m_ServerPlace = position;
            m_ServerPlaceYaw = yaw;
        }

        void PlaceOnLevelIfNeeded()
        {
            var sewer = SewerLocation.Active;
            if (m_ServerPlace is Vector3 place && (sewer != null || ActiveLevel.Layout != null))
            {
                m_ServerPlace = null;
                m_PlacedSewer = sewer;
                if (ActiveLevel.Layout != null) m_PlacedSeed = ActiveLevel.Layout.Seed;
                m_PlacedInHatch = InHatch;
                TeleportOwner(place, Quaternion.Euler(0f, m_ServerPlaceYaw, 0f));
                Motor.SetPitch(0f);
                return;
            }
            if (sewer != null && sewer != m_PlacedSewer)
            {
                m_PlacedSewer = sewer;
                int slot = PlayerLooks.SlotOf(ClientId);
                TeleportOwner(sewer.SpawnFor(slot), sewer.SpawnRotation(slot));
                Motor.SetPitch(0f);
                Cracking = null;
                CrackingConsole = null;
                LeaveConsole();
                return;
            }
            var layout = ActiveLevel.Layout;
            if (layout == null || layout.Seed == m_PlacedSeed || layout.PlayerSpawns.Count == 0) return;
            m_PlacedSeed = layout.Seed;
            var spawn = layout.PlayerSpawns[(int)(ClientId % (ulong)layout.PlayerSpawns.Count)];
            var toHatch = layout.HatchPosition - spawn;
            toHatch.y = 0;
            TeleportOwner(spawn + Vector3.up * 0.1f, Quaternion.LookRotation(toHatch.sqrMagnitude > 0.01f ? -toHatch : Vector3.forward));
            m_PlacedInHatch = false; // the team starts in the entry hatch: sit down into it again
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
                gm?.ShowBanner(Loc.F("unstuck.wait", Mathf.CeilToInt(wait)));
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
                gm?.ShowBanner(Loc.T("unstuck.none"));
                return;
            }
            m_UnstuckAt = Time.time;
            Cracking = null;
            CrackingSafe = null;
            CrackingConsole = null;
            LeaveConsole();
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
            // Lying, watching a teammate or in god mode, the gas has nothing to do with you (no overlay, no knockout).
            if (Downed || Invulnerable) { Gas.Reset(); GasShare = 0f; return; }
            var effect = Gas.Tick(outside, Time.deltaTime);
            GasShare = Gas.Share;
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
                var at = p.transform.position;
                if (View is { } view && !view.At(at, at + Vector3.up * 0.5f, 0.7f)) continue; // looking at them
                if ((at - transform.position).magnitude <= m_Interaction.ReviveDistance) { ReviveTarget = p; break; }
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
            DoorTarget = ReviveTarget == null && HackTarget == null ? AutoDoor.LockedNear(transform.position, AutoDoor.HackReach, View) ?? StorageDoorAtTerminal() : null;
            if (DoorTarget != null && holdE && m_DoorHeld.Hold(DoorTarget, Time.deltaTime, m_Interaction.DoorHackTime))
            {
                if (m_Link != null) m_Link.SendUnlockDoor(DoorTarget.Index);
                else GameManager.Instance?.UnlockDoorServer(this, DoorTarget.Index);
            }
            DoorProgress = m_DoorHeld.Of(DoorTarget);

            UpdateSafe();
            UpdateConsole();
            UpdateStorage();
            UpdateTerminal(holdE);
            UpdateUse(holdE);
            if (Controls.Pressed(GameAction.Gadget) && !CursorMode && m_Inventory != null && m_Inventory.Holds(Items.ItemCatalog.Stun))
            {
                if (m_Link != null) m_Link.SendGadget();
                else GameManager.Instance?.GadgetServer(this);
            }

            var gm = GameManager.Instance;
            if (gm == null || !Controls.Pressed(GameAction.Hatch) || gm.Phase != RunPhase.Playing) return;
            // The hatch: hide in it (the team leaves when everyone is in) or climb back out.
            if (InHatch) SetHatch(false);
            else if (AtHatch) SetHatch(true);
        }

        // ------------------------------------------------------------------ in the car

        /// <summary>The seat of the car this player sits on (0 the driver's … 3), −1 = on foot (from the car's replicated seats).</summary>
        public int SeatIndex => Vehicles.Car.Active != null ? Vehicles.Car.Active.SeatOf(ClientId) : -1;
        public bool Driving => SeatIndex == 0;

        /// <summary>Feet this far under the seat's cushion: a sitting player's eyes are ~0.8 m above it.</summary>
        const float SeatDrop = 0.72f;
        /// <summary>
        /// The model's feet this far under the cushion when sitting: its hips (the legs' pivot, 0.92 m over the feet,
        /// <c>generate_models.LEG_PIVOT</c>) rest on the seat, so the head stays under the cab's roof (1.85 m).
        /// </summary>
        const float ModelUnderSeat = 0.85f;
        bool m_WasSeated;
        int m_LastSeat;

        /// <summary>
        /// Owner: sitting in the car the player is carried by it (looking around freely, turning with it); E gets out on the
        /// car's side. The server decides the seats (<see cref="Vehicles.Car.SitServer"/>).
        /// </summary>
        void UpdateSeat(bool menu)
        {
            var car = Vehicles.Car.Active;
            int seat = SeatIndex;
            bool seated = seat >= 0 && car.Seats[seat] != null;
            if (seated != m_WasSeated)
            {
                m_WasSeated = seated;
                // getting out beside the car — but not on arrival (the drive scene is up): the house puts us in its hatch
                if (!seated && car != null && DriveScene.Active == null) TeleportOwner(car.ExitPoint(m_LastSeat), transform.rotation);
                if (seated) { transform.rotation = car.transform.rotation; Motor.SetPitch(6f); } // sitting down: eyes on the road
                m_Controller.enabled = !seated;
                Audio.Sfx.At(Audio.Sfx.Bank?.Land, transform.position, 0.6f, Audio.Sfx.Group.Footsteps);
            }
            if (!seated) return;
            m_LastSeat = seat;
            transform.position = car.Seats[seat].position - Vector3.up * SeatDrop;
            transform.Rotate(0f, car.YawDelta, 0f);
            if (!menu && !CursorMode && Controls.Pressed(GameAction.Interact))
            {
                // this press is spent: on the host the seat is freed at once, and the same E must not sit us down again
                // on the seat we are still standing on (UpdateUse runs later this frame)
                m_UseWaitRelease = true;
                if (m_Link != null) m_Link.SendUse(UseKind.CarSeat, seat);
                else GameManager.Instance?.UseServer(this, UseKind.CarSeat, seat);
            }
        }

        /// <summary>Something in reach to use with E (a crate, the med table, a seat of the car…), or null.</summary>
        public Interactable UseTarget { get; private set; }
        /// <summary>The hint for <see cref="UseTarget"/>, whether E does something now, and the hold progress (0..1).</summary>
        public string UsePrompt { get; private set; }
        public bool UseAvailable { get; private set; }
        public float UseProgress { get; private set; }
        readonly HoldProgress<Interactable> m_UseHeld = new();
        bool m_UseWaitRelease;

        /// <summary>Owner: press (or hold) E at a crate, the med table, the workbench, a key lock, a lever, the car.</summary>
        // ------------------------------------------------------------------ the vault's lock panels

        /// <summary>The vault's lock panel whose switch or sequence lock is on the screen, or null (the dial: <see cref="CrackingConsole"/>).</summary>
        public VaultConsole AtConsole { get; private set; }
        /// <summary>The switch lock on the screen (<see cref="VaultGame.Switches"/>), or null.</summary>
        public Items.SwitchPuzzle Switches { get; private set; }
        /// <summary>The sequence lock on the screen (<see cref="VaultGame.Sequence"/>), or null.</summary>
        public Items.SequenceLock Sequence { get; private set; }
        /// <summary>When a lock last clanked on a wrong press (the screen flashes red).</summary>
        public float ConsoleWrongTime { get; private set; } = -10f;
        /// <summary>Busy at any lock of the vault (no walking, no reticle).</summary>
        public bool AtVaultLock => AtConsole != null || CrackingConsole != null;
        readonly Dictionary<VaultConsole, Items.SafeLock> m_ConsoleDials = new();
        readonly Dictionary<VaultConsole, Items.SwitchPuzzle> m_ConsoleSwitches = new();

        int ConsoleSeed(VaultConsole c) => LevelRandom.Mix(ActiveLevel.Layout != null ? ActiveLevel.Layout.Seed : 0, 0x0C05, c.Index);

        /// <summary>Owner: E at a lock panel puts its minigame on the screen (a dial or the switches keep their state while away).</summary>
        void StartConsole(VaultConsole c)
        {
            if (c == null || c.Solved) return;
            switch (c.Game)
            {
                case VaultGame.Dial:
                    if (!m_ConsoleDials.TryGetValue(c, out var dial))
                    {
                        var safes = Tuning.Items.Objectives.VaultDial(Tuning.Items.Safes);
                        dial = new Items.SafeLock(ConsoleSeed(c), safes);
                        dial.SetDial(new System.Random(ConsoleSeed(c) ^ 0x5A).Next(safes.DialSize));
                        m_ConsoleDials[c] = dial;
                    }
                    Cracking = dial;
                    CrackingConsole = c;
                    m_CrackStartFrame = Time.frameCount;
                    LastTry = Items.SafeLock.Result.Nothing;
                    break;
                case VaultGame.Switches:
                    if (!m_ConsoleSwitches.TryGetValue(c, out var switches)) m_ConsoleSwitches[c] = switches = new Items.SwitchPuzzle(ConsoleSeed(c), Tuning.Items.Objectives);
                    Switches = switches;
                    AtConsole = c;
                    break;
                default:
                    Sequence = new Items.SequenceLock(ConsoleSeed(c), Time.time, Tuning.Items.Objectives); // it plays again from the start
                    AtConsole = c;
                    break;
            }
        }

        void LeaveConsole()
        {
            AtConsole = null;
            Switches = null;
            Sequence = null;
        }

        /// <summary>Owner: the switch or sequence lock on the screen — number keys press its switches, Space / back leaves.</summary>
        void UpdateConsole()
        {
            var c = AtConsole;
            if (c == null) return;
            bool away = c.Solved || (c.transform.position - transform.position).magnitude > Items.SafeBox.Reach + 0.8f;
            if (away || Controls.Pressed(GameAction.Jump) || Controls.Pressed(GameAction.Back)) { LeaveConsole(); return; }
            var at = c.transform.position;
            int keys = Switches != null ? Switches.Count : Sequence != null ? Sequence.Lamps : 0;
            for (int i = 0; i < keys; i++)
            {
                if (!Controls.DigitPressed(i + 1)) continue;
                if (Switches != null)
                {
                    Audio.Sfx.At(Audio.Sfx.Bank?.Click, at, 0.7f, Audio.Sfx.Group.Items, 0.9f + 0.05f * i);
                    if (Switches.Press(i)) { SolveConsole(c); LeaveConsole(); return; }
                }
                else if (Sequence != null)
                {
                    switch (Sequence.Press(i, Time.time))
                    {
                        case Items.SequenceLock.Result.Wrong:
                            Audio.Sfx.At(Audio.Sfx.Bank?.Drop, at, 1f, Audio.Sfx.Group.Items, 0.6f);
                            EmitNoise(Tuning.Items.Objectives.WrongNoise);
                            ConsoleWrongTime = Time.time;
                            break;
                        case Items.SequenceLock.Result.Step:
                            Audio.Sfx.At(Audio.Sfx.Bank?.Click, at, 0.8f, Audio.Sfx.Group.Items, 0.9f + 0.08f * i);
                            break;
                        case Items.SequenceLock.Result.Opened:
                            SolveConsole(c);
                            LeaveConsole();
                            return;
                    }
                }
            }
        }

        /// <summary>Owner: a lock panel is solved — the server marks it (and opens the vault with the third).</summary>
        void SolveConsole(VaultConsole c)
        {
            if (c == null) return;
            Audio.Sfx.At(Audio.Sfx.Bank?.Stash, c.transform.position, 1f, Audio.Sfx.Group.Items);
            if (m_Link != null) m_Link.SendUse(UseKind.VaultConsole, c.Index);
            else GameManager.Instance?.UseServer(this, UseKind.VaultConsole, c.Index);
        }

        void UpdateUse(bool holdE)
        {
            bool busy = ReviveTarget != null || HackTarget != null || DoorTarget != null || SafeTarget != null || TerminalTarget != null
                        || Cracking != null || AtConsole != null || CursorMode || SeatIndex >= 0;
            UseTarget = busy ? null : Interactable.Near(this, Hands, View);
            bool available = false;
            UsePrompt = UseTarget != null ? UseTarget.Prompt(this, out available) : null;
            UseAvailable = UseTarget != null && available;
            if (!holdE) m_UseWaitRelease = false;
            if (UseTarget == null || !UseAvailable || m_UseWaitRelease) { UseProgress = 0f; return; }
            float hold = UseTarget.HoldTime;
            bool done = hold <= 0f ? Controls.Pressed(GameAction.Interact) : holdE && m_UseHeld.Hold(UseTarget, Time.deltaTime, hold);
            UseProgress = hold > 0f ? m_UseHeld.Of(UseTarget) / hold : 0f; // the ring is a share of the hold, not seconds
            if (!done) return;
            m_UseWaitRelease = true; // one use per press
            if (UseTarget is VaultConsole console) { StartConsole(console); return; } // its minigame, here; the server hears when solved
            if (m_Link != null) m_Link.SendUse(UseTarget.Kind, UseTarget.Index);
            else GameManager.Instance?.UseServer(this, UseTarget.Kind, UseTarget.Index);
        }

        /// <summary>Server: the med table raised this player's health.</summary>
        public void HealServer(int hp)
        {
            m_Health.Heal(hp);
            PublishHealth();
        }

        /// <summary>Owner: hide in the hatch or climb out (also a test hook).</summary>
        public void SetHatch(bool hide)
        {
            if (m_Link != null) m_Link.SendHatch(hide);
            else OnHatchRequest(hide);
        }

        /// <summary>The drawer cabinet / lockers looked at and which part (E opens or closes it), or null.</summary>
        public Items.Storage StorageTarget { get; private set; }
        public int StoragePart { get; private set; } = -1;

        /// <summary>Owner: look right at a drawer or a locker door and press E to open or close it (the server decides).</summary>
        void UpdateStorage()
        {
            StorageTarget = null;
            StoragePart = -1;
            if (ReviveTarget != null || HackTarget != null || DoorTarget != null || SafeTarget != null || Cracking != null || Camera == null) return;
            var storage = LookTarget.At<Items.Storage>(Camera.transform, transform, m_Interaction.LookReach, m_Interaction.LookRadius, out var hit);
            if (storage == null || storage.Index < 0) return;
            StorageTarget = storage;
            StoragePart = storage.PartOf(hit.collider, hit.point);
            if (StoragePart < 0 || CursorMode || !Controls.Pressed(GameAction.Interact)) return;
            if (m_Link != null) m_Link.SendToggleStorage(storage.Index, StoragePart);
            else GameManager.Instance?.ToggleStorageServer(this, storage.Index, StoragePart);
        }

        /// <summary>Locked door in reach (hold E to hack), or null.</summary>
        public AutoDoor DoorTarget { get; private set; }

        /// <summary>At the door terminal, looking at it: the storage room's door, while it is locked (hold E opens it from here).</summary>
        AutoDoor StorageDoorAtTerminal()
        {
            var t = SecurityTerminal.Near(transform.position, Hands, SecurityTerminal.Reach, View);
            if (t == null || t.Kind != TerminalKind.Doors) return null;
            var d = AutoDoor.StorageDoor;
            return d != null && !d.TerminalOpen ? d : null; // even while a worker holds it open: unlocking it is for good
        }
        public float DoorProgress { get; private set; }
        public float DoorShare => DoorProgress / m_Interaction.DoorHackTime;

        // ------------------------------------------------------------------ safes

        /// <summary>Owner: the dial being turned (null when not cracking a safe).</summary>
        public Items.SafeLock Cracking { get; private set; }
        public Items.SafeBox CrackingSafe { get; private set; }
        /// <summary>The vault's lock panel whose dial is being turned (the dial lock is the safe's minigame), or null.</summary>
        public VaultConsole CrackingConsole { get; private set; }
        /// <summary>Owner: a closed safe in reach (press E to start), null if none.</summary>
        public Items.SafeBox SafeTarget { get; private set; }
        /// <summary>Owner: the last try on the dial and when (for the minigame's feedback).</summary>
        public Items.SafeLock.Result LastTry { get; private set; }
        public float LastTryTime { get; private set; } = -10f;
        int m_CrackStartFrame;
        /// <summary>
        /// The dials this player has worked on in this house: walking away from a safe keeps the digits already found
        /// (two found, a walk, the third next time). Safes are new objects in every house, so stale keys just drop out.
        /// </summary>
        readonly Dictionary<Items.SafeBox, Items.SafeLock> m_Dials = new();

        /// <summary>The dial of this safe as it was left, or a new one (its code from the safe's seed).</summary>
        Items.SafeLock DialOf(Items.SafeBox safe)
        {
            if (m_Dials.TryGetValue(safe, out var dial)) return dial;
            var stale = new List<Items.SafeBox>();
            foreach (var k in m_Dials.Keys) if (k == null) stale.Add(k);
            foreach (var k in stale) m_Dials.Remove(k);
            var settings = Tuning.Items.Safes;
            dial = new Items.SafeLock(safe.Seed, settings);
            dial.SetDial(new System.Random(safe.Seed ^ Time.frameCount).Next(settings.DialSize));
            m_Dials[safe] = dial;
            return dial;
        }

        /// <summary>Test hook: start cracking this safe as if E was pressed next to it.</summary>
        public void StartCrackForTest(Items.SafeBox safe)
        {
            if (safe == null) { Cracking = null; CrackingSafe = null; return; }
            Cracking = DialOf(safe);
            CrackingSafe = safe;
            m_CrackStartFrame = Time.frameCount;
        }

        void UpdateSafe()
        {
            SafeTarget = ReviveTarget == null && HackTarget == null && DoorTarget == null ? Items.SafeBox.ClosedNear(transform.position, Hands, Items.SafeBox.Reach, View) : null;
            if (Cracking == null)
            {
                if (SafeTarget == null || !Controls.Pressed(GameAction.Interact) || CursorMode) return;
                Cracking = DialOf(SafeTarget); // the digits found before are still found
                CrackingSafe = SafeTarget;
                m_CrackStartFrame = Time.frameCount;
                LastTry = Items.SafeLock.Result.Nothing;
                return;
            }
            var safe = CrackingSafe;
            var host = safe != null ? safe.transform : CrackingConsole != null ? CrackingConsole.transform : null;
            bool away = host == null || (safe != null && safe.Opened) || (CrackingConsole != null && CrackingConsole.Solved)
                        || (host.position - transform.position).magnitude > Items.SafeBox.Reach + 0.6f;
            if (away || Controls.Pressed(GameAction.Jump) || Controls.Pressed(GameAction.Back))
            {
                Cracking = null;
                CrackingSafe = null;
                CrackingConsole = null;
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                int ticks = Cracking.Turn(mouse.delta.ReadValue().x);
                if (ticks > 0)
                    Audio.Sfx.At(Audio.Sfx.Bank?.Click, host.position + Vector3.up, 0.15f + 0.85f * Cracking.Closeness(), Audio.Sfx.Group.Items,
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
            var host = safe != null ? safe.transform : CrackingConsole != null ? CrackingConsole.transform : null;
            if (Cracking == null || host == null) return;
            LastTry = Cracking.Try();
            LastTryTime = Time.time;
            switch (LastTry)
            {
                case Items.SafeLock.Result.Wrong:
                    // A loud clank: robots with ears may come.
                    Audio.Sfx.At(Audio.Sfx.Bank?.Drop, host.position + Vector3.up, 1f, Audio.Sfx.Group.Items, 0.6f);
                    EmitNoise(Tuning.Items.Safes.WrongNoise);
                    break;
                case Items.SafeLock.Result.Digit:
                    Audio.Sfx.At(Audio.Sfx.Bank?.Pickup, host.position + Vector3.up, 0.8f, Audio.Sfx.Group.Items, 1.3f);
                    break;
                case Items.SafeLock.Result.Opened:
                    Audio.Sfx.At(Audio.Sfx.Bank?.Stash, host.position + Vector3.up, 1f, Audio.Sfx.Group.Items);
                    if (safe != null)
                    {
                        if (m_Link != null) m_Link.SendOpenSafe(safe.Index);
                        else GameManager.Instance?.OpenSafeServer(this, safe.Index);
                    }
                    else SolveConsole(CrackingConsole);
                    Cracking = null;
                    CrackingSafe = null;
                    CrackingConsole = null;
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
            var eye = Camera.transform.position;
            var forward = Camera.transform.forward;
            if (UsingTerminal != null && TerminalViewPose(out var at, out var rotation))
            {
                eye = at;
                forward = rotation * Vector3.forward;
            }
            var view = (position: eye, forward: forward);
            if (mark)
            {
                var target = MarkTarget(view.position, view.forward, Team.MarkRange, UsingTerminal != null ? TerminalView : null);
                if (target != null)
                {
                    if (m_Link != null) m_Link.SendMark(target);
                    else OnMarkRequest(target);
                    return;
                }
                // not a robot: a thing (loot, a crate, a panel…) — marked for the team in yellow-green
                if (MarkedThings.Target(view.position, view.forward, Team.MarkRange, out var point) != null)
                {
                    if (m_Link != null) m_Link.SendMarkThing(point);
                    else OnMarkThingRequest(point);
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
        public static Detector MarkTarget(Vector3 eye, Vector3 forward, float range, Detector except = null)
        {
            if (Physics.Raycast(eye, forward, out var direct, range, Perception.SightMask, QueryTriggerInteraction.Ignore))
            {
                var hit = direct.collider.GetComponentInParent<Detector>();
                if (hit != null && !hit.IsHacked && hit != except) return hit;
            }
            Detector best = null;
            float bestAngle = 5f;
            foreach (var d in Detector.All)
            {
                var to = d.Position - eye;
                float dist = to.magnitude;
                if (dist > range || dist < 0.01f || d.IsHacked || d == except) continue;
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
            if (Downed || gm == null || gm.Phase != RunPhase.Playing) return;
            int hatch = Hatch.IndexNear(transform.position);
            if (hatch < 0) return;
            InHatch = true;
            HatchIndex = hatch;
        }

        /// <summary>Server: the team left the house (or a new one starts).</summary>
        public void LeaveHatchServer() => InHatch = false;

        /// <summary>Server: a house starts with the team sitting in its entry hatch.</summary>
        public void EnterHatchServer(int hatch)
        {
            InHatch = true;
            HatchIndex = hatch;
        }

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

        /// <summary>Server: the terminal this player sits at (-1 none): cameras do not notice them, marks come from the screen.</summary>
        public int AtTerminal { get; private set; } = -1;
        SecurityCamera m_Steered;

        public void TerminalServer(int terminal, Detector steered, float yaw)
        {
            AtTerminal = terminal;
            var camera = terminal >= 0 ? steered as SecurityCamera : null;
            if (m_Steered != null && m_Steered != camera) m_Steered.ReleaseServer(this);
            m_Steered = camera;
            if (camera != null) camera.SteerServer(this, yaw);
        }

        SecurityCamera m_SteerCamera;
        float m_SteerYaw, m_TerminalSent;

        /// <summary>Owner at a camera terminal: the mouse and A/D turn the camera on the screen; its yaw goes to the server.</summary>
        void SteerTerminalCamera(Detector view)
        {
            var camera = view as SecurityCamera;
            if (camera != m_SteerCamera)
            {
                m_SteerCamera = camera;
                m_SteerYaw = camera != null ? camera.CurrentYaw : 0f;
            }
            if (camera != null)
            {
                float keys = (Controls.Held(GameAction.Right) ? 1f : 0f) - (Controls.Held(GameAction.Left) ? 1f : 0f);
                var mouse = Mouse.current;
                float mouseX = mouse != null ? mouse.delta.ReadValue().x * GameSettings.Sensitivity : 0f;
                m_SteerYaw = Mathf.Clamp(m_SteerYaw + keys * 70f * Time.deltaTime + mouseX, -SecurityCamera.SteerLimit, SecurityCamera.SteerLimit);
                camera.PreviewAim(m_SteerYaw);
            }
            SendTerminalState(UsingTerminal != null ? UsingTerminal.Index : -1, camera, m_SteerYaw, false);
        }

        void SendTerminalState(int terminal, Detector steered, float yaw, bool now)
        {
            if (!now && Time.time < m_TerminalSent) return;
            m_TerminalSent = Time.time + 0.1f;
            if (m_Link != null) m_Link.SendTerminal(terminal, steered, yaw);
            else TerminalServer(terminal, steered, yaw);
        }
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
                TerminalTarget = ReviveTarget == null && HackTarget == null && DoorTarget == null && SafeTarget == null && Cracking == null && StorageTarget == null
                    ? SecurityTerminal.Near(transform.position, Hands, SecurityTerminal.Reach, View) : null;
                if (TerminalTarget != null && TerminalTarget.Kind == TerminalKind.Doors) TerminalTarget = null; // nothing to sit at: hold E to open the door
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
            if (m_TerminalTargets.Count == 0) { TerminalView = null; SteerTerminalCamera(null); return; }
            var mouse = Mouse.current;
            // cameras: A/D and the mouse turn the camera, the mouse buttons and the wheel switch it; robots: A/D switch too
            bool steer = t.Kind == TerminalKind.Cameras;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if ((!steer && Controls.Pressed(GameAction.Right)) || (mouse != null && mouse.leftButton.wasPressedThisFrame) || wheel < 0f) m_TerminalIndex++;
            if ((!steer && Controls.Pressed(GameAction.Left)) || (mouse != null && mouse.rightButton.wasPressedThisFrame) || wheel > 0f) m_TerminalIndex--;
            int n = m_TerminalTargets.Count;
            m_TerminalIndex = (m_TerminalIndex % n + n) % n;
            var view = m_TerminalTargets[m_TerminalIndex];
            if (view != TerminalView) m_TerminalHold = 0f;
            TerminalView = view;
            SteerTerminalCamera(steer ? view : null);
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
            m_SteerCamera = null;
            SendTerminalState(terminal.Index, null, 0f, true);
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
            m_SteerCamera = null;
            SendTerminalState(-1, null, 0f, true);
            m_TerminalHold = 0f;
            if (Camera == null) return;
            Camera.transform.localPosition = Vector3.zero;
            Camera.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Owner at a terminal: the camera looks through the security camera on the screen, or at the robot from a clear side.</summary>
        void ShowTerminalView()
        {
            if (TerminalViewPose(out var at, out var rotation)) Camera.transform.SetPositionAndRotation(at, rotation);
        }

        /// <summary>Where the terminal's picture is seen from (also where pings and marks come from while sitting at it).</summary>
        bool TerminalViewPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            var d = TerminalView;
            if (d == null) return false;
            if (d is SecurityCamera cam && cam.Pivot != null)
            {
                // just in front of the lens: the camera's own housing and glow stay behind the view
                position = d.Position + cam.Pivot.forward * 0.4f;
                rotation = cam.Pivot.rotation;
                return true;
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
            position = best;
            rotation = Quaternion.LookRotation(target - best);
            return true;
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
            if (gm == null || (AtTerminal < 0 && !WithinCallRange(position, Team.PingRange + 5f))) return;
            gm.CalloutServer(ClientId, PingKind.Here, position, null);
        }

        /// <summary>Server: the owner marked a thing (every peer finds it at this point and lights it through walls).</summary>
        public void OnMarkThingRequest(Vector3 point)
        {
            var gm = GameManager.Instance;
            if (gm == null || (AtTerminal < 0 && !WithinCallRange(point, Team.MarkRange + 5f))) return;
            gm.CalloutServer(ClientId, PingKind.Thing, point, null);
        }

        /// <summary>Server: the owner marked a robot / device.</summary>
        public void OnMarkRequest(Detector target)
        {
            var gm = GameManager.Instance;
            if (gm == null || target == null || target.IsHacked) return;
            // at a terminal the player looks through the house's cameras: the mark comes from there, not from their eyes
            if (AtTerminal < 0 && !WithinCallRange(target.Position, Team.MarkRange + 5f)) return;
            gm.CalloutServer(ClientId, PingKind.Enemy, target.Position, target);
        }

        /// <summary>
        /// Where the player looks: E works on what is under the crosshair, not on whatever is nearest (null for the
        /// autopilot that holds E, <see cref="DevFlags.AutoInteract"/>: it uses whatever is in reach).
        /// </summary>
        public Aim? View => DevFlags.AutoInteract ? null
            : Camera != null ? new Aim(Camera.transform.position, Camera.transform.forward) : new Aim(Hands, transform.forward);

        /// <summary>Where the player reaches from (chest height): hacking and cracking need a clear line from here.</summary>
        public Vector3 Hands => transform.position + Vector3.up * (Height * 0.7f);

        /// <summary>
        /// Interactive objects are used by looking right at them (<see cref="LookTarget"/>); the autopilot (tests) and a
        /// player without a camera still use the old "anything within reach".
        /// </summary>
        bool UseLook => Camera != null && !DevFlags.AutoInteract;

        /// <summary>The <typeparamref name="T"/> under the crosshair within <see cref="InteractionSettings.LookReach"/>, or null.</summary>
        T Aimed<T>() where T : Component => LookTarget.At<T>(Camera.transform, transform, m_Interaction.LookReach, m_Interaction.LookRadius);

        /// <summary>The robot / device to hack: the one looked at (or, for the autopilot, the nearest in reach).</summary>
        Detector FindHackTarget()
        {
            if (UseLook)
            {
                var looked = Aimed<Detector>();
                if (looked != null) return looked.CanBeHackedFrom(transform.position, HackReach) && looked.ReachableFrom(Hands) ? looked : null;
                // devices without a body to hit (cameras, sensors, plates): the one closest to the crosshair, if close enough
                Detector bestLook = null;
                float bestAngle = m_Interaction.LookAngle;
                var eye = Camera.transform;
                foreach (var d in Detector.All)
                {
                    if (!d.CanBeHackedFrom(transform.position, HackReach) || !d.ReachableFrom(Hands)) continue;
                    float angle = Vector3.Angle(eye.forward, d.Position - eye.position);
                    if (angle < bestAngle) { bestLook = d; bestAngle = angle; }
                }
                return bestLook;
            }
            Detector best = null;
            float bestDist = float.MaxValue;
            foreach (var d in Detector.All)
            {
                if (!d.CanBeHackedFrom(transform.position, HackReach) || (View.HasValue && !View.Value.At(d.transform.position + Vector3.up * 0.1f, d.Position, 0.45f)) ||
                    !d.ReachableFrom(Hands)) continue;
                float dist = (d.transform.position - transform.position).sqrMagnitude;
                if (dist < bestDist) { best = d; bestDist = dist; }
            }
            return best;
        }

        // ------------------------------------------------------------------ server

        public void HitServer(Vector3 source)
        {
            if (!IsServer || Invulnerable || !m_Health.TryHit(Time.time)) return;
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
