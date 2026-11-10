using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// HUD presenter (prefab UI/HUD). Principle: show only what matters right now — one contextual prompt under the
    /// crosshair, overlays only while something is happening (damage, gas, alarm), static panels kept small.
    /// Every element is pre-built in the prefab; this component only shows, hides, moves and recolours them.
    /// Decisions live in pure classes (<see cref="PromptRules"/>, <see cref="BannerQueue"/>, <see cref="GainTracker"/>, <see cref="UiMath"/>).
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        public const float HelpDuration = 30f;
        public const float IconRange = 25f;
        public const float LookRange = 18f;
        public const float LookDwell = 0.4f;

        [Header("Root")]
        public RectTransform Canvas;
        public GameObject Content;

        [Header("Overlays")]
        public Image LowHpVignette;
        public Image HitFlash;
        public Image GasVignette;
        public Image GasTint;
        public Text LaughText;
        public Text GasText;
        public Image DownedTint;
        public Text DownedText;
        public GameObject AlarmFrame;
        public Image[] AlarmEdges;
        public Text AlarmLabel;

        [Header("Around the crosshair")]
        public RectTransform ArcLayer;
        public ArcView ArcPrefab;
        public ArcView HitArc;
        public GameObject CrosshairDot;
        public GameObject CrosshairBrackets;
        public BarView Stamina;
        public PromptView Prompt;

        [Header("World markers")]
        public RectTransform IconLayer;
        [Tooltip("Suspicion bar + \"?\" / \"!\" over robots and devices.")]
        public SuspicionMarkerView SuspicionPrefab;
        public RectTransform MarkerLayer;
        public TeamMarkerView TeamMarkerPrefab;
        public RectTransform HatchMarker;
        public Image HatchDiamond;
        public Text HatchLabel;

        [Header("Panels")]
        [Tooltip("How much light is on you = how well robots see you.")]
        public BarView LightBar;
        public Text LightLabel;
        public CanvasGroup NoiseGroup;
        public Image[] NoiseBars;
        public Text NoiseLabel;
        public Text HouseLabel;
        public Text QuotaLabel;
        public BarView QuotaBar;
        public RectTransform GainRect;
        public Text GainLabel;
        public Text QuotaHint;
        public Image[] HpPips;
        public RectTransform TeamList;
        public Text TeamRowPrefab;
        public CanvasGroup Banner;
        public RectTransform BannerRect;
        public Text BannerLabel;
        public GameObject RunOver;
        public Text RunOverTotal;
        public GameObject Help;

        static readonly string[] NoiseNames = { "", "тихо", "слышно", "громко" };
        static readonly Color[] NoiseColors = { Color.white, new(0.6f, 0.9f, 1f), new(1f, 0.85f, 0.35f), new(1f, 0.35f, 0.25f) };
        static readonly Color QuotaGreen = new(0.45f, 1f, 0.5f);

        readonly BannerQueue m_Banners = new();
        readonly GainTracker m_Gain = new();
        readonly HitFeedback m_Hit = new();
        UiPool<ArcView> m_Arcs;
        UiPool<SuspicionMarkerView> m_Icons;
        UiPool<TeamMarkerView> m_Markers;
        UiPool<Text> m_TeamRows;
        GameManager m_Subscribed;
        bool m_ShowHelp = true;
        float m_HelpUntil = -1f;
        Detector m_LookCandidate;
        float m_LookSince;

        public Prompt CurrentPrompt { get; private set; }
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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void FlashHit(Vector3 awayFromSource) => m_Hit.Hit(awayFromSource, Time.time);

        public void ShowBanner(string text) => m_Banners.Push(text);

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
            Ui.Show(Content, active);
            if (!active) return;

            float now = Time.time;
            m_Hit.Update(Time.deltaTime);
            m_Banners.Update(now);
            m_Gain.Observe(gm.Collected, now);
            UpdateHelp(me);
            CurrentPrompt = PromptRules.Choose(BuildContext(gm, me));

            ShowOverlays(gm, me, now);
            ShowDetection(me, now);
            ShowRobotIcons(me);
            ShowCrosshair(me);
            ShowStamina(me);
            ShowNoise(me, now);
            ShowVisibility(me, now);
            ShowQuota(gm, me, now);
            ShowHatchMarker(gm, me);
            ShowHealth(me);
            ShowTeam(me, now);
            Prompt.Set(CurrentPrompt);
            ShowBannerLine(now);
            Ui.Show(RunOver, gm.Phase == RunPhase.Failed);
            if (gm.Phase == RunPhase.Failed) RunOverTotal.text = $"Сдано за забег: {gm.RunTotal} очк.";
            Ui.Show(Help, m_ShowHelp);
        }

        void UpdateHelp(PlayerController me)
        {
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                m_ShowHelp = !m_ShowHelp;
                m_HelpUntil = float.MaxValue;
            }
            if (m_HelpUntil < 0f) m_HelpUntil = Time.time + HelpDuration;
            if (Time.time > m_HelpUntil) m_ShowHelp = false;
        }

        PromptContext BuildContext(GameManager gm, PlayerController me)
        {
            var looked = LookedAtRobot(me);
            var hover = InventoryUI.Instance != null ? InventoryUI.Instance.Hover : null;
            return new PromptContext
            {
                Downed = me.Downed,
                MenuOpen = GameMenu.IsOpen,
                CursorMode = me.CursorMode,
                CanRevive = me.ReviveTarget != null,
                ReviveProgress = me.ReviveShare,
                HackTarget = me.HackTarget != null ? me.HackTarget.DisplayName : null,
                HackProgress = me.HackShare,
                HackSeconds = Configs.Tuning.Player.Interaction.HackDuration,
                BlockedByCeiling = me.Motor.BlockedByCeiling,
                Playing = gm.Phase == RunPhase.Playing,
                NearHatch = Hatch.IsNear(me.transform.position),
                CarriedItems = me.GetComponent<PlayerInventory>().Items.Count,
                Collected = gm.Collected,
                Quota = gm.Quota,
                LookedAt = looked != null ? $"{looked.DisplayName}: {looked.SensesDescription}" : null,
                HoverItem = hover != null ? ItemCatalog.Describe(hover.Shape, hover.Color) : null,
            };
        }

        /// <summary>The robot / device in the crosshair for at least <see cref="LookDwell"/> s (within 18 m, in plain sight).</summary>
        Detector LookedAtRobot(PlayerController me)
        {
            var cam = me.Camera;
            if (cam == null) return null;
            var origin = cam.transform.position;
            Detector best = null;
            // 1) The crosshair ray hits the robot's body.
            if (Physics.Raycast(origin, cam.transform.forward, out var direct, LookRange, Perception.SightMask, QueryTriggerInteraction.Ignore))
                best = direct.collider.GetComponentInParent<Detector>();
            // 2) Small devices without a body (PIR, plates): close enough to the centre of the view.
            float bestAngle = best != null ? -1f : 6f;
            foreach (var d in Detector.All)
            {
                var to = d.Position - origin;
                if (to.magnitude > LookRange) continue;
                float angle = Vector3.Angle(cam.transform.forward, to);
                if (angle >= bestAngle || !Visible(origin, d, to, 0.6f)) continue;
                best = d;
                bestAngle = angle;
            }
            if (best != m_LookCandidate) { m_LookCandidate = best; m_LookSince = Time.time; }
            return best != null && Time.time - m_LookSince > LookDwell ? best : null;
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

            // Laughing gas outside.
            var gas = me.Gas;
            bool gassed = gas.Time > 0f;
            Ui.Show(GasVignette, gassed);
            Ui.Show(GasTint, gassed);
            Ui.Show(GasText, gassed);
            Ui.Show(LaughText, gassed && gas.Laughing);
            if (gassed)
            {
                float g = gas.Share;
                GasVignette.color = new Color(0.4f, 0.95f, 0.3f, Mathf.Clamp01(g * 1.4f));
                GasTint.color = new Color(0.5f, 0.9f, 0.3f, g * 0.35f);
                GasText.text = $"Весёлый газ: {gas.SecondsLeft:0} c до отключки";
                if (gas.Laughing)
                    LaughText.rectTransform.anchoredPosition = new Vector2(0f, -216f) + new Vector2(Mathf.Sin(now * 37f), -Mathf.Cos(now * 29f)) * 6f * g;
            }

            Ui.Show(DownedTint, down);
            bool showDown = down && gm.Phase == RunPhase.Playing;
            Ui.Show(DownedText, showDown);
            if (showDown) DownedText.text = PromptRules.DownedText(me.Hp, me.CanGetUp);

            // Alarm: thin pulsing frame + a small label with the time left: clear, but does not cover the view.
            bool alarm = gm.AlarmActive;
            Ui.Show(AlarmFrame, alarm);
            if (!alarm) return;
            float left = (float)(gm.AlarmUntil - Session.ServerTime);
            var red = new Color(1f, 0.15f, 0.1f, 0.35f + 0.25f * Mathf.Sin(now * 7f));
            foreach (var e in AlarmEdges) e.color = red;
            AlarmLabel.text = $"ТРЕВОГА · {Mathf.Max(0, left):0} c";
        }

        /// <summary>Arcs around the crosshair towards every sensor that is noticing you.</summary>
        void ShowDetection(PlayerController me, float now)
        {
            m_Arcs.Begin();
            if (me.Camera != null && !me.Downed)
            {
                var cam = me.Camera.transform;
                foreach (var d in Detector.All)
                {
                    float a = d.AwarenessOf(me.ClientId);
                    if (a < 0.03f) continue;
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

        void ShowCrosshair(PlayerController me)
        {
            bool hands = me.CursorMode || me.Downed;
            bool hover = InventoryUI.Instance != null && InventoryUI.Instance.Hover != null;
            Ui.Show(CrosshairDot, !hands && !hover);
            Ui.Show(CrosshairBrackets, !hands && hover); // "this can be taken"
        }

        void ShowStamina(PlayerController me)
        {
            var stamina = me.Motor.Stamina;
            bool visible = stamina.Value < 0.999f && !me.Downed;
            Ui.Show(Stamina, visible);
            if (visible) Stamina.Set(stamina.Value, stamina.Exhausted ? new Color(1f, 0.35f, 0.25f, 0.9f) : new Color(1f, 1f, 1f, 0.75f));
        }

        float m_NextLight, m_Visibility;

        /// <summary>How lit you are (= how well eyes see you): a bar and a word, like a thief's light gem.</summary>
        void ShowVisibility(PlayerController me, float now)
        {
            bool visible = !me.Downed;
            Ui.Show(LightBar, visible);
            Ui.Show(LightLabel, visible);
            if (!visible) return;
            if (now >= m_NextLight)
            {
                m_NextLight = now + 0.1f;
                var s = Tuning.Perception.Illumination;
                float target = SensorMath.Visibility(SensorMath.LightFactor(Perception.IlluminationOf(me), s), s);
                m_Visibility = Mathf.Lerp(m_Visibility, target, 0.6f);
            }
            int level = UiMath.LightLevel(m_Visibility);
            var color = UiMath.LightColor(level);
            LightBar.Set(Mathf.Max(0.04f, m_Visibility), color);
            Ui.Set(LightLabel, UiMath.LightNames[level], color);
        }

        /// <summary>How loud you are right now: three short bars above the inventory, fading out when silent.</summary>
        void ShowNoise(PlayerController me, float now)
        {
            float age = now - me.LastNoiseTime;
            bool visible = age <= 0.9f && !me.Downed;
            Ui.Show(NoiseGroup, visible);
            if (!visible) return;
            int bars = UiMath.NoiseBars(me.LastNoiseRadius);
            NoiseGroup.alpha = Mathf.Clamp01(1f - age / 0.9f);
            for (int i = 0; i < NoiseBars.Length; i++)
                NoiseBars[i].color = i < bars ? NoiseColors[bars] : new Color(1, 1, 1, 0.25f);
            Ui.Set(NoiseLabel, NoiseNames[bars], NoiseColors[bars]);
        }

        void ShowQuota(GameManager gm, PlayerController me, float now)
        {
            int collected = gm.Collected, quota = gm.Quota;
            bool met = collected >= quota;
            HouseLabel.text = $"Дом {gm.Level.Index + 1} · этаж {LevelLayout.FloorOf(me.transform.position.y) + 1}";
            Ui.Set(QuotaLabel, $"{collected} / {quota}", met ? QuotaGreen : Color.white);
            QuotaBar.Set(quota > 0 ? collected / (float)quota : 0f, met ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.8f, 0.2f));
            bool gain = m_Gain.Visible(now);
            Ui.Show(GainLabel, gain);
            if (gain)
            {
                float age = now - m_Gain.Time;
                GainRect.anchoredPosition = new Vector2(240f, -23f + age * 8f);
                Ui.Set(GainLabel, $"+{m_Gain.Amount}", new Color(QuotaGreen.r, QuotaGreen.g, QuotaGreen.b, Mathf.Clamp01(2f - age)));
            }
            Ui.Show(QuotaHint, met && gm.Phase == RunPhase.Playing);
        }

        /// <summary>Once the quota is met: a small diamond over the hatch (clamped to the screen edge) with distance.</summary>
        void ShowHatchMarker(GameManager gm, PlayerController me)
        {
            var layout = ActiveLevel.Layout;
            bool visible = layout != null && me.Camera != null && gm.Phase == RunPhase.Playing &&
                           gm.Collected >= gm.Quota && !Hatch.IsNear(me.transform.position);
            Ui.Show(HatchMarker, visible);
            if (!visible) return;
            var size = Size;
            var vp = me.Camera.WorldToViewportPoint(layout.HatchPosition + Vector3.up * 0.4f);
            var gui = UiMath.EdgeMarker(vp, size.x, size.y, 48f, out bool clamped);
            HatchMarker.anchoredPosition = new Vector2(gui.x, size.y - gui.y);
            var c = new Color(0.45f, 1f, 0.5f, clamped ? 0.9f : 0.75f);
            HatchDiamond.color = c;
            Ui.Set(HatchLabel, $"люк · {Vector3.Distance(me.transform.position, layout.HatchPosition):0} м", c);
        }

        void ShowHealth(PlayerController me)
        {
            for (int i = 0; i < HpPips.Length; i++)
                HpPips[i].color = i < me.Hp ? new Color(0.9f, 0.2f, 0.2f) : new Color(1, 1, 1, 0.15f);
        }

        /// <summary>
        /// Teammates: a compact list under the health panel; names over heads only when close and visible;
        /// a downed teammate gets a red marker through walls so the team can come and help.
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
                string state = down ? "упал" : new string('■', Mathf.Max(0, p.Hp));
                Ui.Set(m_TeamRows.Next(), $"{p.Nick}  {state}", down ? new Color(1f, 0.35f, 0.3f) : new Color(1, 1, 1, 0.8f));
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
                    Ui.Set(m.Label, $"{p.Nick} · {dist:0} м", new Color(1f, 0.5f, 0.45f));
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
