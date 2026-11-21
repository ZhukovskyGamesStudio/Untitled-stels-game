using System.Collections.Generic;
using System.Linq;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// HUD presenter (prefab UI/HUD). Principle: almost no text. The reticle changes its glyph when E would do something
    /// (<see cref="ReticleRules"/>), the keys of the moment sit quietly in the bottom right corner (<see cref="HintRules"/>),
    /// noise, light and health are icons in the bottom left; overlays only while something is happening (damage, gas, alarm).
    /// Every element is pre-built in the prefab; this component only shows, hides, moves and recolours them.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        public const float IconRange = 25f;
        /// <summary>Where the "+N" of fuel starts, next to the fuel bar.</summary>
        public const float GainX = 310f, GainY = -36f;
        /// <summary>How opaque the hint stack is: there, but not calling for attention.</summary>
        public const float HintAlpha = 0.6f;

        [Header("Root")]
        public RectTransform Canvas;
        public GameObject Content;
        [Tooltip("Fades the whole HUD out while the drive scene plays (the drive speaks for itself).")]
        public CanvasGroup ContentGroup;

        [Header("Overlays")]
        public Image LowHpVignette;
        public Image HitFlash;
        public Image GasVignette;
        public Image GasTint;
        public Text LaughText;
        [Tooltip("Seconds of laughing gas left before passing out.")]
        public Text GasText;
        public Image DownedTint;
        [Tooltip("Knocked out: whose eyes you look through (or that you are out).")]
        public Text DownedText;
        [Tooltip("Hiding in the hatch: the view goes dark.")]
        public Image HatchTint;
        [Tooltip("In the exit hatch: who is still missing, or the countdown.")]
        public Text HatchText;
        [Tooltip("In the sewer, once the car is in a tunnel: the countdown to the next house.")]
        public Text RouteCountdown;
        [Tooltip("Black over everything between the drive scene and the next house.")]
        public Image LoadingFade;
        public GameObject AlarmFrame;
        public Image[] AlarmEdges;
        public Text AlarmLabel;

        [Header("Reticle")]
        public RectTransform ArcLayer;
        public ArcView ArcPrefab;
        public ArcView HitArc;
        public Image ReticleGlyph;
        [Tooltip("The hold progress around the reticle (a radial fill).")]
        public Image ReticleRing;
        [Tooltip("One sprite per ReticleIcon, in its order (Dot first).")]
        public Sprite[] ReticleSprites;
        public BarView Stamina;

        [Header("Hints (bottom right)")]
        public RectTransform HintStack;
        public HintView HintPrefab;

        [Header("World markers")]
        public RectTransform IconLayer;
        [Tooltip("Suspicion bar + \"?\" / \"!\" over robots and devices.")]
        public SuspicionMarkerView SuspicionPrefab;
        public RectTransform MarkerLayer;
        public TeamMarkerView TeamMarkerPrefab;
        public RectTransform HatchMarker;
        public Image HatchDiamond;
        public Text HatchLabel;

        [Header("Status (bottom left)")]
        [Tooltip("How lit you are = how well eyes see you: a closed eye in the dark, wide open in full light.")]
        public Image Eye;
        public Sprite[] EyeSprites;
        [Tooltip("How loud you are: a speaker with 0–3 waves.")]
        public Image Speaker;
        public Sprite[] SpeakerSprites;
        public Image[] Hearts;
        public Sprite HeartFull, HeartEmpty;
        [Tooltip("The team's hack slots, bottom left: lit = a robot / device is switched off.")]
        public Image[] HackSlots;
        [Header("The house's clock")]
        [Tooltip("Its panel: tinted from calm blue to red as extra robots come.")]
        public Image ClockPanel;
        public Text ClockTime, ClockNext, ClockCount;
        [Tooltip("From the last extra robot to the next.")]
        public BarView ClockBar;
        [Header("The team comes up into the house")]
        public CanvasGroup EntryBanner;
        public Text EntryTitle, EntrySub;
        [Header("The drive: the last house's titles")]
        public GameObject StatsPanel;
        public Text StatsHeader;
        public StatsRowView[] StatsRows;
        [Tooltip("A free hack slot: the bolt.")]
        public Sprite HackFree;
        [Tooltip("A taken hack slot: the picture of what is switched off (indexed by RobotKind).")]
        public Sprite[] HackKindIcons;

        [Header("Panels")]
        public Text HouseLabel;
        public Text QuotaLabel;
        public BarView QuotaBar;
        public RectTransform GainRect;
        public Text GainLabel;
        [Tooltip("When the hunter comes (or that it is in the house), under the fuel bar.")]
        public Text HunterLabel;
        [Tooltip("The house's main job and how far along it is (under the hunter's line).")]
        public Text ObjectiveLabel;
        [Tooltip("Sitting at a security terminal: what is on the screen.")]
        public Text TerminalLabel;
        public RectTransform TeamList;
        public Text TeamRowPrefab;
        public CanvasGroup Banner;
        public RectTransform BannerRect;
        public Text BannerLabel;
        public GameObject RunOver;
        public Text RunOverTotal;
        public Text RunOverRestart;

        [Header("Map and callouts")]
        [Tooltip("The map terminal's map: the whole building, a floor at a time, north-up (the HUD has no map of its own).")]
        public MapView BigMap;
        [Tooltip("The tablet's minimap (corner, follows the player): what the team has seen, teammates, pings, marked robots.")]
        public MapView MiniMap;
        [Tooltip("Pings and marked robots on screen, through walls.")]
        public RectTransform CalloutLayer;
        public CalloutMarkerView CalloutPrefab;

        static readonly Color[] NoiseColors = { new(1f, 1f, 1f, 0.45f), new(0.6f, 0.9f, 1f), new(1f, 0.85f, 0.35f), new(1f, 0.35f, 0.25f) };
        static readonly Color QuotaGreen = new(0.45f, 1f, 0.5f);

        readonly BannerQueue m_Banners = new();
        readonly GainTracker m_Gain = new();
        readonly HitFeedback m_Hit = new();
        readonly List<Hint> m_Hints = new();
        UiPool<ArcView> m_Arcs;
        UiPool<SuspicionMarkerView> m_Icons;
        UiPool<TeamMarkerView> m_Markers;
        UiPool<Text> m_TeamRows;
        UiPool<CalloutMarkerView> m_Callouts;
        UiPool<HintView> m_HintRows;
        bool m_ForceMap;
        int m_KeysVersion = -1;
        GameManager m_Subscribed;

        /// <summary>The hint lines of this frame, joined (logs of the autopilot).</summary>
        public Prompt CurrentPrompt { get; private set; }
        public Reticle CurrentReticle { get; private set; }
        public IReadOnlyList<Hint> CurrentHints => m_Hints;
        public Vector3 LastHitDirection => m_Hit.Direction;
        public float LastHitTime => m_Hit.Time;

        Vector2 Size => Canvas.rect.size;

        void Awake()
        {
            Instance = this;
            m_Arcs = new UiPool<ArcView>(ArcPrefab, ArcLayer);
            m_Icons = new UiPool<SuspicionMarkerView>(SuspicionPrefab, IconLayer);
            m_Markers = new UiPool<TeamMarkerView>(TeamMarkerPrefab, MarkerLayer);
            m_TeamRows = new UiPool<Text>(TeamRowPrefab, TeamList);
            m_Callouts = new UiPool<CalloutMarkerView>(CalloutPrefab, CalloutLayer);
            m_HintRows = new UiPool<HintView>(HintPrefab, HintStack);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void FlashHit(Vector3 awayFromSource) => m_Hit.Hit(awayFromSource, Time.time);

        public void ShowBanner(string text) => m_Banners.Push(text);

        /// <summary>Seconds the style banner stays (a new F8 replaces it at once).</summary>
        const float StyleBannerTime = 1.6f;

        void ShowStyleBanner(string text) => m_Banners.Push(text, "style", Time.time, StyleBannerTime);

        void OnEnable() => GameManager.StyleAnnounced += ShowStyleBanner;
        void OnDisable() => GameManager.StyleAnnounced -= ShowStyleBanner;

        // ------------------------------------------------------------------ frame

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != m_Subscribed)
            {
                if (m_Subscribed != null) m_Subscribed.OnBanner -= ShowBanner;
                if (gm != null) gm.OnBanner += ShowBanner;
                m_Subscribed = gm;
            }
            var me = PlayerController.Local;
            bool active = gm != null && me != null;
            if (gm != null) ShowLoadingFade(gm, me); // black from the first frame of a session until the player stands in place
            else { m_Fade = 1f; m_LoadingSince = -1f; m_LoadedFor = int.MinValue; if (LoadingFade != null) Ui.Show(LoadingFade, false); }
            Ui.Show(Content, active);
            if (!active) return;

            float now = Time.time;
            m_Hit.Update(Time.deltaTime);
            m_Banners.Update(now);
            m_Gain.Observe(gm.Collected, now);
            bool driveScene = gm.DriveUntil > 0; // the drive on to the next house speaks for itself
            bool hideHud = driveScene || gm.Picking; // (the start screen has its own)
            if (ContentGroup != null) ContentGroup.alpha = Mathf.MoveTowards(ContentGroup.alpha, hideHud ? 0f : 1f, Time.deltaTime * 2.5f);
            var context = BuildContext(gm, me);
            CurrentReticle = driveScene ? Reticle.Hidden : ReticleRules.Choose(context);
            HintRules.Collect(context, m_Hints);
            if (driveScene) m_Hints.Clear();
            CurrentPrompt = m_Hints.Count > 0 ? new Prompt(1, "", string.Join(" · ", m_Hints)) : default;

            ShowOverlays(gm, me, now);
            ShowHatch(gm, me);
            ShowRouteCountdown(gm);
            ShowDetection(me, now);
            ShowRobotIcons(me);
            ShowReticle();
            ShowHints();
            ShowStamina(me);
            ShowNoise(me, now);
            ShowVisibility(me, now);
            ShowQuota(gm, now);
            ShowHatchMarker(gm, me);
            ShowHealth(me);
            ShowHackSlots(gm);
            ShowHouseClock(gm);
            ShowEntryBanner(gm);
            ShowHouseStats(gm);
            ShowTeam(me, now);
            ShowCallouts(gm, me, now);
            ShowMaps(gm, me);
            ShowBannerLine(now);
            Ui.Show(RunOver, gm.Phase == RunPhase.Failed);
            if (gm.Phase == RunPhase.Failed) RunOverTotal.text = Loc.F("hud.run_total", gm.RunTotal);
            NameTheKeys();
        }

        // ------------------------------------------------------------------ the house's clock, its first moment, its titles

        /// <summary>
        /// The house's clock, top middle: it counts up from when the team came up into the house, shows when the next
        /// extra robot comes (not a countdown: the clock's second it comes at) with a bar to it, and gets redder with every
        /// robot that has come (<see cref="HouseClockRules"/>).
        /// </summary>
        void ShowHouseClock(GameManager gm)
        {
            if (ClockPanel == null) return;
            bool on = gm.Phase == RunPhase.Playing && gm.HouseClockAt > 0;
            Ui.Show(ClockPanel, on);
            if (!on) return;
            var r = Tuning.Run.Rules;
            float t = gm.HouseClock;
            float next = HouseClockRules.NextAt(t, r.ReinforceFirst, r.ReinforceSecond, r.ReinforceEvery);
            float prev = HouseClockRules.PreviousAt(t, r.ReinforceFirst, r.ReinforceSecond, r.ReinforceEvery);
            int came = RobotRoster.ReinforcementsDue(t, r.ReinforceFirst, r.ReinforceSecond, r.ReinforceEvery);
            var color = HouseClockRules.ColorOf(HouseClockRules.Heat(came));
            color.a = 0.82f;
            if (ClockPanel.color != color) ClockPanel.color = color;
            string time = HouseClockRules.Format(t), at = HouseClockRules.Format(next);
            if (ClockTime.text != time) ClockTime.text = time;
            if (ClockNext.text != at) ClockNext.text = at;
            if (ClockBar != null) ClockBar.Set(Mathf.InverseLerp(prev, next, t), new Color(1f, 1f, 1f, 0.9f));
            if (ClockCount != null)
            {
                string n = came > 0 ? "+" + came : "";
                if (ClockCount.text != n) ClockCount.text = n;
            }
        }

        double m_ClockSeen = -1;
        float m_EntryAt = -100f;

        /// <summary>The moment the team first comes up into the house and its clock starts: a big notice across the screen for everyone.</summary>
        void ShowEntryBanner(GameManager gm)
        {
            if (EntryBanner == null) return;
            if (gm.HouseClockAt != m_ClockSeen)
            {
                // (a player who joins a house already going sees no notice: the clock has run a while)
                if (m_ClockSeen >= 0 && gm.HouseClockAt > 0 && gm.HouseClock < 5f)
                {
                    m_EntryAt = Time.unscaledTime;
                    var r = Tuning.Run.Rules;
                    EntrySub.text = Loc.F("hud.entry.sub", HouseClockRules.Format(r.ReinforceFirst));
                    Audio.Sfx.Ui(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.Ping : null, 1f);
                }
                m_ClockSeen = gm.HouseClockAt;
            }
            float age = Time.unscaledTime - m_EntryAt;
            const float hold = 3.2f, fade = 0.6f;
            float a = age < 0.25f ? age / 0.25f : age < hold ? 1f : 1f - (age - hold) / fade;
            a = Mathf.Clamp01(a);
            Ui.Show(EntryBanner, a > 0.001f);
            if (a <= 0.001f) return;
            EntryBanner.alpha = a;
            EntryTitle.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Exp(-age * 4f));
        }

        /// <summary>The drive to the next house: who did what in the last one, and everyone's title.</summary>
        void ShowHouseStats(GameManager gm)
        {
            if (StatsPanel == null) return;
            bool on = gm.DriveUntil > 0 && gm.LastTitles.Count > 0 && gm.LastTitlesAt >= 0f && Time.time - gm.LastTitlesAt < 240f;
            Ui.Show(StatsPanel, on);
            if (!on || StatsRows == null) return;
            Ui.Set(StatsHeader, Loc.F("stats.header", gm.LastTitlesHouse), Color.white);
            for (int i = 0; i < StatsRows.Length; i++)
            {
                var row = StatsRows[i];
                bool shown = i < gm.LastTitles.Count;
                Ui.Show(row, shown);
                if (!shown) continue;
                var t = gm.LastTitles[i];
                var who = PlayerController.Find(t.Player);
                var team = PlayerLooks.TeamColor(PlayerLooks.SlotOf(t.Player));
                Ui.Set(row.Name, who != null ? who.Nick : "—", team);
                Ui.Set(row.Title, Loc.T(HouseStats.TitleKey(t.Kind)), new Color(1f, 0.84f, 0.35f));
                Ui.Set(row.Did, t.Kind == StatKind.None ? Loc.T(HouseStats.DidKey(t.Kind)) : string.Format(Loc.Plural(HouseStats.DidKey(t.Kind), Mathf.RoundToInt(t.Value)), Mathf.RoundToInt(t.Value)), new Color(1f, 1f, 1f, 0.85f));
                Ui.Set(row.Numbers, Loc.F("stats.numbers", t.Fuel, t.Downs, t.Revives, t.Metres), new Color(1f, 1f, 1f, 0.6f));
                if (row.Bar != null) row.Bar.color = team;
            }
        }

        /// <summary>Texts that name keys follow the rebinds and the language.</summary>
        void NameTheKeys()
        {
            int version = Controls.Version * 1000 + Loc.Version;
            if (m_KeysVersion == version) return;
            m_KeysVersion = version;
            RunOverRestart.text = Loc.F("hud.restart", Controls.Label(GameAction.Restart));
        }

        float m_Fade = 1f, m_LoadingSince = -1f;
        int m_LoadedFor = int.MinValue; // the location (sewer instance / house seed) that already opened

        /// <summary>
        /// A location being set up (a new session, the sewer, a house started some other way) is black at once and opens
        /// only when the player stands in place (<see cref="LoadingRules"/>). The drive to the next house is never black:
        /// the house is built during the drive scene and its camera glides back into the player's eyes in the car
        /// (GameManager.Arrival) — the house counts as open once the player is in place there.
        /// </summary>
        void ShowLoadingFade(GameManager gm, PlayerController me)
        {
            if (LoadingFade == null) return;
            var sewer = SewerLocation.Active;
            if (sewer != null && sewer.UnderHouse) sewer = null; // (a house's own sewer is part of the house: nothing to load)
            int here = sewer != null ? sewer.GetHashCode() : ActiveLevel.Layout != null ? ActiveLevel.Layout.Seed : 0;
            bool loading = false;
            bool driving = gm.DriveUntil > 0; // the drive is the show
            if (driving)
            {
                if (me != null && me.PlacedHere) m_LoadedFor = here;
                m_LoadingSince = -1f;
            }
            else if (here != m_LoadedFor)
            {
                if (m_LoadingSince < 0f) m_LoadingSince = Time.unscaledTime;
                loading = LoadingRules.StillLoading(me != null, me != null && me.PlacedHere, sewer != null, Vehicles.Car.Active != null,
                    Time.unscaledTime - m_LoadingSince);
                if (!loading) { m_LoadedFor = here; m_LoadingSince = -1f; }
            }
            if (loading) m_Fade = 1f; // no fade in: the jump must never be seen
            m_Fade = Mathf.MoveTowards(m_Fade, loading ? 1f : 0f, Time.unscaledDeltaTime / 0.9f);
            Ui.Show(LoadingFade, m_Fade > 0.001f);
            if (m_Fade > 0.001f) LoadingFade.color = new Color(0f, 0f, 0f, m_Fade);
        }

        /// <summary>
        /// In a hatch the view goes dark and a short line says who is still missing (everyone must be in a hatch, either
        /// one), that somebody must sit in the exit's, or counts down to the sewer.
        /// </summary>
        void ShowHatch(GameManager gm, PlayerController me)
        {
            bool inside = gm.Phase == RunPhase.Playing && me.InHatch;
            Ui.Show(HatchTint, inside);
            Ui.Show(HatchText, inside);
            if (!inside) return;
            float left = gm.LeaveAt > 0 ? Mathf.Max(0f, (float)(gm.LeaveAt - Session.ServerTime)) : -1f;
            string text;
            if (left >= 0f) text = Mathf.CeilToInt(left).ToString();
            else
            {
                string missing = string.Join(", ", PlayerController.All.Where(p => !p.InHatch && !p.KnockedOut).Select(p => p.Nick));
                text = missing.Length > 0 ? Loc.F("hud.hatch.waiting", missing)
                    : !PlayerController.All.Any(p => p.InHatch && p.HatchIndex == 1) ? Loc.T("hud.hatch.need_exit") : "";
            }
            if (HatchText.text != text) HatchText.text = text;
        }

        /// <summary>The way is chosen: a big countdown in the middle of the screen until the team goes.</summary>
        void ShowRouteCountdown(GameManager gm)
        {
            if (RouteCountdown == null) return;
            bool on = gm.GoAt > 0; // (in the sewer between houses or under the house)
            Ui.Show(RouteCountdown, on);
            if (!on) return;
            float left = Mathf.Max(0f, (float)(gm.GoAt - Session.ServerTime));
            string text = Mathf.Max(1, Mathf.CeilToInt(left)).ToString();
            if (RouteCountdown.text != text) RouteCountdown.text = text;
            // each second starts big and settles
            float f = left - Mathf.Floor(left);
            RouteCountdown.rectTransform.localScale = Vector3.one * (1f + 0.15f * f);
        }

        // ------------------------------------------------------------------ map and callouts

        /// <summary>Test hook: show the whole-house map as if sitting at a map terminal (on the player's floor).</summary>
        public void SetBigMap(bool open) => m_ForceMap = open;

        /// <summary>Sitting at a map terminal (or the test hook): the whole building, the floor the player flips to.</summary>
        bool MapOpen(GameManager gm, PlayerController me)
        {
            if (GameMenu.IsOpen || gm.Exploration == null) return false;
            var t = me.UsingTerminal;
            return m_ForceMap || (t != null && t.Kind == TerminalKind.Map) || TabletOpen(gm, me);
        }

        /// <summary>The tablet in the hand, opened with the hand key: the big map of what the team has seen (not the whole house).</summary>
        static bool TabletOpen(GameManager gm, PlayerController me) =>
            me.TabletOpen && gm.Phase == RunPhase.Playing && me.UsingTerminal == null && !me.Downed && me.GetComponent<PlayerInventory>().Holds(ItemCatalog.Tablet);

        /// <summary>The tablet in the hand, closed: a minimap of what the team has seen in the corner.</summary>
        static bool TabletMini(GameManager gm, PlayerController me) =>
            !me.TabletOpen && gm.Phase == RunPhase.Playing && gm.Exploration != null && me.UsingTerminal == null && !me.Downed && !GameMenu.IsOpen
            && me.GetComponent<PlayerInventory>().Holds(ItemCatalog.Tablet);

        void ShowMaps(GameManager gm, PlayerController me)
        {
            bool mini = MiniMap != null && TabletMini(gm, me);
            if (MiniMap != null)
            {
                Ui.Show(MiniMap, mini);
                if (mini) MiniMap.Refresh(gm, me);
            }
            bool open = MapOpen(gm, me);
            Ui.Show(BigMap, open);
            if (!open) return;
            // the map terminal shows the whole house and everything in it; the tablet only what the team has seen
            BigMap.WholeHouse = !TabletOpen(gm, me) || m_ForceMap;
            var t = me.UsingTerminal;
            BigMap.ViewFloor = t != null && t.Kind == TerminalKind.Map ? me.TerminalFloor : -1;
            BigMap.Refresh(gm, me);
        }

        /// <summary>
        /// Teammates' pings and marked robots: a diamond with the distance, seen through walls, kept on the screen edge
        /// when out of view. Marked robots are also drawn through walls by the renderer.
        /// </summary>
        void ShowCallouts(GameManager gm, PlayerController me, float now)
        {
            m_Callouts.Begin();
            var cam = me.Camera;
            var size = Size;
            foreach (var d in Detector.All)
            {
                bool marked = d.IsMarked && !d.IsHacked;
                d.ShowThroughWalls(marked);
                if (!marked || cam == null) continue;
                var top = d.Position + Vector3.up * 0.7f;
                float left = d.MarkedUntil - Session.ServerTimeF;
                var red = new Color(1f, 0.25f, 0.18f, left < 2f ? 0.5f + 0.5f * Mathf.Sin(now * 14f) : 1f);
                Callout(cam, size, top, red, Loc.F("hud.metres", Vector3.Distance(me.transform.position, d.Position)), false, 0f);
            }
            // marked things: lit through walls (their layer) and a yellow-green marker over each
            s_ThingMarks.Clear();
            foreach (var p in gm.Pings.Active)
            {
                if (p.Kind != PingKind.Thing) continue;
                s_ThingMarks.Add(p.Position);
                if (cam == null) continue;
                float left = p.Until - now;
                var green = new Color(0.8f, 1f, 0.25f, left < 2f ? 0.5f + 0.5f * Mathf.Sin(now * 14f) : 1f);
                Callout(cam, size, p.Position + Vector3.up * 0.35f, green, Loc.F("hud.metres", Vector3.Distance(me.transform.position, p.Position)), p.Age(now) < 0.6f, p.Age(now));
            }
            MarkedThings.Show(s_ThingMarks);
            if (cam != null)
                foreach (var p in gm.Pings.Active)
                {
                    if (p.Kind is not (PingKind.Here or PingKind.Help)) continue;
                    var color = PlayerLooks.TeamColor(PlayerLooks.SlotOf(p.Owner));
                    color.a = Mathf.Clamp01(p.Until - now);
                    // a call for help pulses its ring the whole time it lasts (a ping only as it appears)
                    float age = p.Kind == PingKind.Help ? Mathf.Repeat(p.Age(now), 1.2f) : p.Age(now);
                    Callout(cam, size, p.Position, color, Loc.F("hud.metres", Vector3.Distance(me.transform.position, p.Position)), true, age);
                }
            m_Callouts.End();
        }

        static readonly List<Vector3> s_ThingMarks = new();

        void Callout(Camera cam, Vector2 size, Vector3 world, Color color, string label, bool ring, float age)
        {
            var vp = cam.WorldToViewportPoint(world);
            var gui = UiMath.EdgeMarker(vp, size.x, size.y, 40f, out bool clamped);
            var m = m_Callouts.Next();
            m.Rect.anchoredPosition = new Vector2(gui.x, size.y - gui.y);
            m.Diamond.color = color;
            // A ring that shrinks onto the spot when the ping appears: the eye finds it.
            bool showRing = ring && age < 0.6f;
            Ui.Show(m.Ring, showRing);
            if (showRing)
            {
                m.Ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(3f, 1f, age / 0.6f);
                m.Ring.color = new Color(color.r, color.g, color.b, color.a * (1f - age / 0.6f));
            }
            Ui.Set(m.Label, label, new Color(color.r, color.g, color.b, color.a * (clamped ? 0.8f : 1f)));
        }

        /// <summary>The hint of the hand key: switch the flashlight, or put the battery in a flashlight.</summary>
        static string HandUse(PlayerController me, PlayerInventory inventory)
        {
            int hand = inventory.HandIndex();
            if (hand < 0) return null;
            var e = inventory.Items[hand];
            if (e.Shape == ItemCatalog.Flashlight) return e.Color > 0 || me.FlashlightOn ? Loc.T(me.FlashlightOn ? "hint.flashlight.off" : "hint.flashlight.on") : null;
            if (e.Shape == ItemCatalog.Battery)
                for (int i = 0; i < inventory.Items.Count; i++) if (inventory.CanRecharge(i)) return Loc.T("hint.battery.use");
            if (e.Shape == ItemCatalog.Medkit) return me.Hp < HealthState.MaxHp ? Loc.T("hint.medkit") : null;
            if (e.Shape == ItemCatalog.Coffee) return Loc.T("hint.coffee");
            if (e.Shape == ItemCatalog.Tablet) return Loc.T("hint.tablet");
            return null;
        }

        PromptContext BuildContext(GameManager gm, PlayerController me)
        {
            var hover = InventoryUI.Instance != null ? InventoryUI.Instance.Hover : null;
            var inventory = me.GetComponent<PlayerInventory>();
            var terminal = me.UsingTerminal;
            var view = me.TerminalView;
            return new PromptContext
            {
                Downed = me.Downed,
                Hp = me.Hp,
                CanGetUp = me.CanGetUp,
                Watching = me.Watching != null,
                MenuOpen = GameMenu.IsOpen,
                CursorMode = me.CursorMode,
                CanRevive = me.ReviveTarget != null,
                ReviveProgress = me.ReviveShare,
                CanHack = me.HackTarget != null,
                SearchOnly = me.HackTarget is RobotController && !me.HoldsKeyboard,
                HackProgress = me.HackShare,
                Playing = gm.Phase == RunPhase.Playing,
                NearExit = Hatch.IndexNear(me.transform.position) == 1,
                NearTrade = gm.CanTradeAt(me.transform.position),
                InHatch = me.InHatch,
                LockedDoor = me.DoorTarget != null,
                DoorProgress = me.DoorShare,
                SafeNear = me.SafeTarget != null && me.Cracking == null,
                Storage = me.StorageTarget != null
                    ? Loc.T((me.StorageTarget.Drawers ? "hint.drawer." : "hint.locker.") + (me.StorageTarget.IsOpen(me.StoragePart) ? "close" : "open"))
                    : null,
                Cracking = me.Cracking != null || me.AtConsole != null,
                NearTerminal = me.TerminalTarget != null && terminal == null,
                TerminalUsable = me.TerminalTarget != null && !me.TerminalTarget.Spent,
                AtTerminal = terminal != null,
                SittingKind = terminal != null ? terminal.Kind : default,
                SittingCanHack = terminal != null && terminal.Charges > 0 && view != null && !view.IsHacked,
                CarriedItems = inventory.Items.Count,
                HasUse = me.UsePrompt != null,
                UseKind = me.UseTarget != null ? me.UseTarget.Kind : default,
                UseText = me.UsePrompt,
                UseAvailable = me.UseAvailable,
                UseProgress = me.UseProgress,
                Driving = me.SeatIndex == 0,
                Passenger = me.SeatIndex > 0,
                HasStun = inventory.Holds(ItemCatalog.Stun),
                HandUse = HandUse(me, inventory),
                HoverItem = hover != null,
                BigMap = MapOpen(gm, me),
            };
        }

        static bool Visible(Vector3 origin, Detector d, Vector3 to, float slack) =>
            !Physics.Raycast(origin, to.normalized, out var hit, to.magnitude - slack, Perception.SightMask, QueryTriggerInteraction.Ignore)
            || hit.collider.GetComponentInParent<Detector>() == d;

        // ------------------------------------------------------------------ sections

        void ShowOverlays(GameManager gm, PlayerController me, float now)
        {
            bool down = me.Downed;
            // Last hit point left: a light red edge, slow pulse.
            Ui.Show(LowHpVignette, me.Hp == 1 && !down);
            if (me.Hp == 1 && !down) LowHpVignette.color = new Color(0.8f, 0f, 0f, 0.32f + 0.1f * Mathf.Sin(now * 2.5f));
            // A fresh hit: short flash + an arc towards the attacker.
            Ui.Show(HitFlash, m_Hit.Flash > 0.6f);
            if (m_Hit.Flash > 0.6f) HitFlash.color = new Color(1f, 0f, 0f, 0.5f * (m_Hit.Flash - 0.6f));
            float arcAlpha = m_Hit.ArcAlpha(now);
            Ui.Show(HitArc, arcAlpha > 0f && me.Camera != null);
            if (arcAlpha > 0f && me.Camera != null)
            {
                var cam = me.Camera.transform;
                HitArc.Set(UiMath.ScreenAngle(cam.position, cam.forward, cam.position + m_Hit.Direction), 120f, new Color(1f, 0.15f, 0.1f, arcAlpha));
            }

            // Laughing gas outside: the green edge says what it is; only the seconds left are written.
            var gas = me.Gas;
            bool gassed = gas.Time > 0f && !me.Downed && me.Watching == null;
            Ui.Show(GasVignette, gassed);
            Ui.Show(GasTint, gassed);
            Ui.Show(GasText, gassed);
            Ui.Show(LaughText, gassed && gas.Laughing);
            if (gassed)
            {
                float g = gas.Share;
                GasVignette.color = new Color(0.4f, 0.95f, 0.3f, Mathf.Clamp01(g * 1.4f));
                GasTint.color = new Color(0.5f, 0.9f, 0.3f, g * 0.35f);
                string seconds = Mathf.CeilToInt(gas.SecondsLeft).ToString();
                if (GasText.text != seconds) GasText.text = seconds;
                if (gas.Laughing)
                {
                    if (LaughText.text != Loc.T("hud.laugh")) LaughText.text = Loc.T("hud.laugh");
                    LaughText.rectTransform.anchoredPosition = new Vector2(0f, -216f) + new Vector2(Mathf.Sin(now * 37f), -Mathf.Cos(now * 29f)) * 6f * g;
                }
            }

            Ui.Show(DownedTint, down);
            // Lying with health left needs no words (the hint names the key to get up); out cold — whose eyes these are.
            string downed = !down || gm.Phase != RunPhase.Playing || me.Hp > 0 ? null
                : me.Watching != null ? me.Watching.Nick : Loc.T("hud.out_cold");
            Ui.Show(DownedText, downed != null);
            if (downed != null && DownedText.text != downed) DownedText.text = downed;

            // Alarm: thin pulsing frame + a small label with the time left: clear, but does not cover the view.
            bool alarm = gm.AlarmActive;
            Ui.Show(AlarmFrame, alarm);
            if (!alarm) return;
            float left = (float)(gm.AlarmUntil - Session.ServerTime);
            var red = new Color(1f, 0.15f, 0.1f, 0.35f + 0.25f * Mathf.Sin(now * 7f));
            foreach (var e in AlarmEdges) e.color = red;
            AlarmLabel.text = Loc.F("hud.alarm", Mathf.Max(0, Mathf.CeilToInt(left)));
        }

        const float DetectionArcMin = 0.12f, DetectionArcRange = 15f;

        /// <summary>
        /// Arcs around the crosshair towards a robot or sensor near you that is noticing you (yellow while its meter fills, red
        /// once it has seen you): only those on your floor and close enough to matter — a seismic sensor two floors down
        /// feeling a step is no news, and an arc nobody can find reads as a bug.
        /// </summary>
        void ShowDetection(PlayerController me, float now)
        {
            m_Arcs.Begin();
            if (me.Camera != null && !me.Downed)
            {
                var cam = me.Camera.transform;
                var at = me.transform.position;
                foreach (var d in Detector.All)
                {
                    float a = d.AwarenessOf(me.ClientId);
                    if (a < DetectionArcMin) continue;
                    var to = d.Position - at;
                    if (Mathf.Abs(to.y) > 2.5f || new Vector2(to.x, to.z).magnitude > DetectionArcRange) continue;
                    bool detected = a >= 0.99f;
                    var color = detected
                        ? new Color(1f, 0.12f, 0.08f, 0.75f + 0.25f * Mathf.Sin(now * 12f))
                        : Color.Lerp(new Color(1f, 0.95f, 0.4f, 0.35f), new Color(1f, 0.55f, 0.1f, 0.95f), a);
                    m_Arcs.Next().Set(UiMath.ScreenAngle(cam.position, cam.forward, d.Position), 70f + (1f - a) * 25f, color);
                }
            }
            m_Arcs.End();
        }

        /// <summary>
        /// Over robots and devices you can see (within 25 m): a suspicion bar that fills while it notices you
        /// (red and blinking during the «Обнаружен нарушитель» moment) and a "?" / "!" for its state.
        /// </summary>
        void ShowRobotIcons(PlayerController me)
        {
            m_Icons.Begin();
            var cam = me.Camera;
            if (cam != null)
            {
                var camPos = cam.transform.position;
                var size = Size;
                float now = Time.time;
                foreach (var d in Detector.All)
                {
                    float mine = d.IsHacked ? 0f : d.AwarenessOf(me.ClientId);
                    bool bar = UiMath.ShowSuspicionBar(mine, d.IsChasing);
                    int icon = d.IsHacked ? 0 : UiMath.StateIcon(d.Awareness, d.IsInvestigating, d.IsChasing);
                    if (icon == 0 && !bar) continue;
                    var top = d.Position + Vector3.up * 0.6f;
                    var to = top - camPos;
                    float dist = to.magnitude;
                    if (dist > IconRange) continue;
                    var vp = cam.WorldToViewportPoint(top);
                    if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
                    if (!Visible(camPos, d, to, 0.8f)) continue;
                    var (text, color) = icon switch
                    {
                        0 => (null, Color.clear),
                        1 => ("?", new Color(1f, 0.9f, 0.3f)),
                        2 => ("?", new Color(1f, 0.55f, 0.15f)),
                        _ => ("!", new Color(1f, 0.15f, 0.1f)),
                    };
                    bool detected = mine >= 0.99f;
                    m_Icons.Next().Show(Ui.FromViewport(vp, size), Mathf.Lerp(1.15f, 0.7f, dist / IconRange), bar, mine,
                        UiMath.SuspicionColor(mine, detected, now), text, color);
                }
            }
            m_Icons.End();
        }

        /// <summary>The reticle: a dot, or the glyph of what E would do (dimmed when it can't now), a ring while holding.</summary>
        void ShowReticle()
        {
            var r = CurrentReticle;
            Ui.Show(ReticleGlyph, r.Visible);
            Ui.Show(ReticleRing, r.Visible && r.Progress > 0f);
            if (!r.Visible) return;
            bool dot = r.Icon == ReticleIcon.Dot;
            int i = (int)r.Icon;
            var sprite = i < ReticleSprites.Length ? ReticleSprites[i] : null;
            if (ReticleGlyph.sprite != sprite) ReticleGlyph.sprite = sprite;
            float size = dot ? 5f : 30f;
            if (!Mathf.Approximately(ReticleGlyph.rectTransform.sizeDelta.x, size)) ReticleGlyph.rectTransform.sizeDelta = new Vector2(size, size);
            var color = dot ? new Color(1f, 1f, 1f, 0.8f) : r.Available ? new Color(1f, 1f, 1f, 0.92f) : new Color(1f, 1f, 1f, 0.38f);
            if (ReticleGlyph.color != color) ReticleGlyph.color = color;
            if (r.Progress > 0f) ReticleRing.fillAmount = Mathf.Clamp01(r.Progress);
        }

        void ShowHints()
        {
            m_HintRows.Begin();
            foreach (var h in m_Hints) m_HintRows.Next().Set(h, HintAlpha);
            m_HintRows.End();
        }

        void ShowStamina(PlayerController me)
        {
            var stamina = me.Motor.Stamina;
            bool visible = stamina.Value < 0.999f && !me.Downed;
            Ui.Show(Stamina, visible);
            if (visible) Stamina.Set(stamina.Value, stamina.Exhausted ? new Color(1f, 0.35f, 0.25f, 0.9f) : new Color(1f, 1f, 1f, 0.75f));
        }

        float m_NextLight, m_Visibility;

        /// <summary>How lit you are (= how well eyes see you): an eye that opens as the light on you grows.</summary>
        void ShowVisibility(PlayerController me, float now)
        {
            bool visible = !me.Downed;
            Ui.Show(Eye, visible);
            if (!visible) return;
            if (now >= m_NextLight)
            {
                m_NextLight = now + 0.1f;
                var s = Tuning.Perception.Illumination;
                float target = SensorMath.Visibility(SensorMath.LightFactor(Perception.IlluminationOf(me), s), s);
                m_Visibility = Mathf.Lerp(m_Visibility, target, 0.6f);
            }
            int frame = Mathf.Clamp(Mathf.RoundToInt(m_Visibility * (EyeSprites.Length - 1)), 0, EyeSprites.Length - 1);
            if (Eye.sprite != EyeSprites[frame]) Eye.sprite = EyeSprites[frame];
            var color = Color.Lerp(new Color(0.75f, 0.8f, 0.9f, 0.6f), new Color(1f, 0.92f, 0.6f, 1f), m_Visibility);
            if (Eye.color != color) Eye.color = color;
        }

        /// <summary>How loud you are right now: a speaker with 0–3 waves, dim when silent.</summary>
        void ShowNoise(PlayerController me, float now)
        {
            bool visible = !me.Downed;
            Ui.Show(Speaker, visible);
            if (!visible) return;
            float age = now - me.LastNoiseTime;
            bool fresh = age <= 0.9f;
            int level = fresh ? UiMath.NoiseBars(me.LastNoiseRadius) : 0;
            if (Speaker.sprite != SpeakerSprites[level]) Speaker.sprite = SpeakerSprites[level];
            var color = NoiseColors[level];
            if (level > 0) color.a = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(1f - age / 0.9f));
            if (Speaker.color != color) Speaker.color = color;
        }

        void ShowQuota(GameManager gm, float now)
        {
            int collected = gm.Collected, quota = gm.FuelNeeded; // (the way the car stands in, else the cheapest)
            bool met = collected >= quota;
            if (gm.Level.Index < 0)
            {
                // the start of the run: the car comes with a full tank and the first drive costs nothing — no quota yet
                if (HouseLabel.text != Loc.T("hud.sewer")) HouseLabel.text = Loc.T("hud.sewer");
                Ui.Show(QuotaLabel, false);
                QuotaBar.Set(1f, new Color(0.3f, 0.9f, 0.4f));
                Ui.Show(GainLabel, false);
                ShowHunter(gm);
                ShowObjective(gm);
                ShowTerminal(PlayerController.Local);
                return;
            }
            Ui.Show(QuotaLabel, true);
            var layout = ActiveLevel.Layout;
            string house = InTheSewer(gm) || layout == null ? Loc.T("hud.sewer") : HouseKinds.Title(gm.Level.Seed, layout.Kind);
            if (HouseLabel.text != house) HouseLabel.text = house;
            Ui.Set(QuotaLabel, $"{collected} / {quota}", met ? QuotaGreen : Color.white);
            QuotaBar.Set(quota > 0 ? collected / (float)quota : 0f, met ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.8f, 0.2f));
            bool gain = m_Gain.Visible(now);
            Ui.Show(GainLabel, gain);
            if (gain)
            {
                float age = now - m_Gain.Time;
                GainRect.anchoredPosition = new Vector2(GainX, GainY + age * 8f);
                Ui.Set(GainLabel, $"+{m_Gain.Amount}", new Color(QuotaGreen.r, QuotaGreen.g, QuotaGreen.b, Mathf.Clamp01(2f - age)));
            }
            ShowHunter(gm);
            ShowObjective(gm);
            ShowTerminal(PlayerController.Local);
        }

        /// <summary>At a terminal: what is on the screen, which of how many, and the charges of a hacking terminal.</summary>
        void ShowTerminal(PlayerController me)
        {
            if (TerminalLabel == null) return;
            var t = me.UsingTerminal;
            Ui.Show(TerminalLabel, t != null && t.Kind != TerminalKind.Map); // the map has its own caption
            if (t == null || t.Kind == TerminalKind.Map) return;
            var shown = me.TerminalView;
            string text = me.TerminalViewCount == 0 ? "—"
                : $"{(shown != null ? shown.DisplayName : "")} · {me.TerminalViewNumber} / {me.TerminalViewCount}";
            if (t.Kind == TerminalKind.Hacking)
            {
                float hack = me.TerminalHackShare;
                text += shown != null && shown.IsHacked ? " · " + Loc.T("hud.terminal.hacked")
                    : hack > 0f ? $" · {Mathf.RoundToInt(hack * 100f)}%"
                    : " · " + Loc.F("hud.terminal.charges", t.Charges);
            }
            if (TerminalLabel.text != text) TerminalLabel.text = text;
        }

        /// <summary>
        /// The task list under the fuel: in a house find the exit, then the main job (gold once done); in the sewer open
        /// the way, fuel the car, choose a tunnel. Done tasks stay, ticked and dim (<see cref="ObjectiveRules.Tasks"/>).
        /// </summary>
        void ShowObjective(GameManager gm)
        {
            if (ObjectiveLabel == null) return;
            var l = ActiveLevel.Layout;
            bool sewer = InTheSewer(gm);
            if (!sewer && (gm.Phase != RunPhase.Playing || l == null)) { Ui.Show(ObjectiveLabel, false); return; }
            bool exitSeen = !sewer && gm.Exploration != null && MapSketch.AnyRevealed(gm.Exploration, 0,
                new Rect(l.ExitHatchPosition.x - 0.8f, l.ExitHatchPosition.z - 0.8f, 1.6f, 1.6f));
            var tasks = ObjectiveRules.Tasks(sewer, exitSeen, sewer ? default : gm.ObjectiveStage, gm.GateOpen, gm.QuotaMet);
            m_TaskText.Clear();
            foreach (var (line, done) in tasks)
            {
                if (m_TaskText.Length > 0) m_TaskText.Append('\n');
                m_TaskText.Append(done ? "<color=#8CE69Acc>✓ " : "<color=#ffffffcc>• ").Append(line).Append("</color>");
            }
            Ui.Show(ObjectiveLabel, tasks.Count > 0);
            string text = m_TaskText.ToString();
            if (ObjectiveLabel.text != text) ObjectiveLabel.text = text;
        }

        readonly System.Text.StringBuilder m_TaskText = new();

        /// <summary>
        /// The local player is in the sewer: between houses, or under the house past the basement's gate (the basement the
        /// team arrives in still counts as the house: its tasks, its name).
        /// </summary>
        static bool InTheSewer(GameManager gm)
        {
            if (gm.Phase == RunPhase.Sewer) return true;
            var me = PlayerController.Local;
            var l = ActiveLevel.Layout;
            if (me == null || l == null || SewerLocation.Active == null) return false;
            var at = me.transform.position;
            return at.y < LevelLayout.Underground && !Basement.Contains(l.Cellar, at + Vector3.up * 0.5f);
        }

        static readonly Color[] HunterColors = { new(1f, 1f, 1f, 0.7f), new(1f, 0.7f, 0.25f), new(1f, 0.3f, 0.25f) };

        void ShowHunter(GameManager gm)
        {
            if (HunterLabel == null) return;
            int inside = 0;
            foreach (var d in Detector.All)
                if (d.Kind == RobotKind.Hunter && !d.IsHacked) inside++;
            float seconds = gm.HunterAt > 0 ? (float)(gm.HunterAt - Session.ServerTime) : -1f;
            var (text, urgency) = gm.Phase == RunPhase.Playing ? HunterRules.Line(seconds, gm.HunterAlarmsLeft, inside) : (null, HunterRules.Urgency.Calm);
            Ui.Show(HunterLabel, text != null);
            if (text == null) return;
            var color = HunterColors[(int)urgency];
            if (urgency != HunterRules.Urgency.Calm) color.a = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f); // blinks when it matters
            Ui.Set(HunterLabel, text, color);
        }

        /// <summary>Once the fuel is enough: a small diamond over the exit hatch (clamped to the screen edge) with the distance.</summary>
        void ShowHatchMarker(GameManager gm, PlayerController me)
        {
            var layout = ActiveLevel.Layout;
            bool visible = layout != null && me.Camera != null && gm.Phase == RunPhase.Playing &&
                           gm.QuotaMet && !Hatch.IsNear(me.transform.position) && !me.InHatch;
            Ui.Show(HatchMarker, visible);
            if (!visible) return;
            var size = Size;
            var exit = layout.ExitHatchPosition;
            var vp = me.Camera.WorldToViewportPoint(exit + Vector3.up * 0.4f);
            var gui = UiMath.EdgeMarker(vp, size.x, size.y, 48f, out bool clamped);
            HatchMarker.anchoredPosition = new Vector2(gui.x, size.y - gui.y);
            var c = new Color(0.45f, 1f, 0.5f, clamped ? 0.9f : 0.75f);
            HatchDiamond.color = c;
            Ui.Set(HatchLabel, Loc.F("hud.metres", Vector3.Distance(me.transform.position, exit)), c);
        }

        readonly List<int> m_HackedKinds = new();

        /// <summary>The team's hack slots: free ones (blue bolts) on the left, then what is switched off, oldest first, grey (HackSlotRules).</summary>
        void ShowHackSlots(GameManager gm)
        {
            if (HackSlots == null) return;
            bool house = gm != null && gm.Phase == RunPhase.Playing;
            int max = house ? gm.MaxHacked : 0;
            m_HackedKinds.Clear();
            if (house)
                for (int i = 0; i < 8; i++)
                {
                    int k = gm.HackedKind(i);
                    if (k < 0) break;
                    m_HackedKinds.Add(k);
                }
            var layout = HackSlotRules.Layout(Mathf.Min(max, HackSlots.Length), m_HackedKinds);
            for (int i = 0; i < HackSlots.Length; i++)
            {
                bool shown = i < layout.Length;
                Ui.Show(HackSlots[i], shown);
                if (!shown) continue;
                int kind = layout[i];
                var sprite = kind < 0 || HackKindIcons == null || kind >= HackKindIcons.Length || HackKindIcons[kind] == null ? HackFree : HackKindIcons[kind];
                if (sprite != null && HackSlots[i].sprite != sprite) HackSlots[i].sprite = sprite;
                var color = kind < 0 ? new Color(0.35f, 0.75f, 1f, 1f) : new Color(0.62f, 0.64f, 0.68f, 0.85f);
                if (HackSlots[i].color != color) HackSlots[i].color = color;
            }
        }

        /// <summary>Hearts: two as a rule, the third (gold) only while the med table's extra point is there.</summary>
        void ShowHealth(PlayerController me)
        {
            for (int i = 0; i < Hearts.Length; i++)
            {
                bool shown = i < Mathf.Max(HealthState.MaxHp, me.Hp);
                Ui.Show(Hearts[i], shown);
                if (!shown) continue;
                bool full = i < me.Hp;
                var sprite = full ? HeartFull : HeartEmpty;
                if (Hearts[i].sprite != sprite) Hearts[i].sprite = sprite;
                var color = !full ? new Color(1f, 1f, 1f, 0.35f) : i >= HealthState.MaxHp ? new Color(1f, 0.8f, 0.2f) : new Color(0.95f, 0.25f, 0.25f);
                if (Hearts[i].color != color) Hearts[i].color = color;
            }
        }

        /// <summary>
        /// Teammates: a compact list under the minimap (health as squares, or a word: down / in the hatch); names over heads
        /// only when close and visible; a downed teammate gets a red marker through walls so the team can come and help.
        /// </summary>
        void ShowTeam(PlayerController me, float now)
        {
            m_TeamRows.Begin();
            m_Markers.Begin();
            var cam = me.Camera;
            var size = Size;
            foreach (var p in PlayerController.All)
            {
                if (p == me) continue;
                bool down = p.Downed;
                string state = down ? Loc.T("hud.team.down") : p.InHatch ? Loc.T("hud.team.hatch") : new string('■', Mathf.Max(0, p.Hp));
                Ui.Set(m_TeamRows.Next(), $"{p.Nick}  {state}", down ? new Color(1f, 0.35f, 0.3f) : p.InHatch ? new Color(0.6f, 1f, 0.65f, 0.85f) : new Color(1, 1, 1, 0.8f));
                if (cam == null) continue;

                var head = p.transform.position + Vector3.up * (down ? 0.6f : p.Height + 0.25f);
                var vp = cam.WorldToViewportPoint(head);
                float dist = Vector3.Distance(cam.transform.position, head);
                if (down)
                {
                    var gui = UiMath.EdgeMarker(vp, size.x, size.y, 48f, out _);
                    var m = m_Markers.Next();
                    m.Rect.anchoredPosition = new Vector2(gui.x, size.y - gui.y);
                    Ui.Show(m.Cross, true);
                    var red = new Color(1f, 0.25f, 0.2f, 0.7f + 0.3f * Mathf.Sin(now * 6f));
                    foreach (var bar in m.CrossBars) bar.color = red;
                    m.Label.rectTransform.anchoredPosition = new Vector2(0f, -21f);
                    Ui.Set(m.Label, $"{p.Nick} · {Loc.F("hud.metres", dist)}", new Color(1f, 0.5f, 0.45f));
                }
                else if (vp.z > 0f && dist < 14f && !Physics.Linecast(cam.transform.position, head, Perception.SightMask, QueryTriggerInteraction.Ignore))
                {
                    var m = m_Markers.Next();
                    m.Rect.anchoredPosition = Ui.FromViewport(vp, size);
                    Ui.Show(m.Cross, false);
                    m.Label.rectTransform.anchoredPosition = new Vector2(0f, 9f);
                    Ui.Set(m.Label, p.Nick, new Color(1, 1, 1, Mathf.Clamp01((14f - dist) / 4f) * 0.8f));
                }
            }
            m_TeamRows.End();
            m_Markers.End();
        }

        void ShowBannerLine(float now)
        {
            float a = m_Banners.Alpha(now);
            Ui.Show(Banner, a > 0f);
            if (a <= 0f) return;
            Banner.alpha = a;
            BannerRect.anchoredPosition = new Vector2(0f, -115f + (1f - a) * 10f);
            if (BannerLabel.text != m_Banners.Current) BannerLabel.text = m_Banners.Current;
        }
    }
}
