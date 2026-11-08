using System.Collections.Generic;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.UI
{
    /// <summary>
    /// Placeholder HUD. Principle: show only what matters right now. One contextual prompt under the crosshair,
    /// overlays only while something is happening (damage, gas, alarm), static panels kept small.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        public const float HelpDuration = 30f;

        public const float BannerDuration = 3.5f;
        readonly Queue<string> m_BannerQueue = new();
        string m_Banner;
        float m_BannerStart = -10f;
        float m_HitFlash;
        bool m_ShowHelp = true;
        float m_HelpUntil = -1f;
        GameManager m_Subscribed;
        readonly List<Prompt> m_Prompts = new();
        int m_LastCollected = -1;
        int m_GainAmount;
        float m_GainTime = -10f;

        public Vector3 LastHitDirection { get; private set; }
        public float LastHitTime { get; private set; } = -10f;
        public Prompt CurrentPrompt { get; private set; }

        void Awake() => Instance = this;

        public void FlashHit(Vector3 awayFromSource)
        {
            m_HitFlash = 1f;
            LastHitDirection = -awayFromSource;
            LastHitTime = Time.time;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != m_Subscribed)
            {
                if (m_Subscribed != null) m_Subscribed.OnBanner -= ShowBanner;
                if (gm != null) gm.OnBanner += ShowBanner;
                m_Subscribed = gm;
            }
            m_HitFlash = Mathf.MoveTowards(m_HitFlash, 0f, Time.deltaTime * 1.5f);
            UpdateBanner();
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                m_ShowHelp = !m_ShowHelp;
                m_HelpUntil = float.MaxValue;
            }
            if (m_HelpUntil < 0f && PlayerController.Local != null) m_HelpUntil = Time.time + HelpDuration;
            if (m_HelpUntil > 0f && Time.time > m_HelpUntil) m_ShowHelp = false;

            if (gm != null)
            {
                int c = gm.Collected.Value;
                if (m_LastCollected >= 0 && c > m_LastCollected)
                {
                    m_GainAmount = (Time.time - m_GainTime < 1.5f ? m_GainAmount : 0) + (c - m_LastCollected);
                    m_GainTime = Time.time;
                }
                m_LastCollected = c;
            }

            CurrentPrompt = gm != null && PlayerController.Local != null ? BuildPrompt(gm, PlayerController.Local) : default;
        }

        public void ShowBanner(string text)
        {
            if (m_BannerQueue.Count < 4 && text != m_Banner) m_BannerQueue.Enqueue(text);
        }

        void UpdateBanner()
        {
            if (Time.time - m_BannerStart < BannerDuration) return;
            if (m_BannerQueue.Count == 0) { m_Banner = null; return; }
            m_Banner = m_BannerQueue.Dequeue();
            m_BannerStart = Time.time;
        }

        Prompt BuildPrompt(GameManager gm, PlayerController me)
        {
            m_Prompts.Clear();
            if (me.Downed.Value || GameMenu.IsOpen) return default;
            if (me.ReviveTarget != null)
                m_Prompts.Add(new Prompt(100, "E", "Удерживай — поднять союзника", me.ReviveProgress / PlayerController.ReviveTime));
            if (me.Motor.BlockedByCeiling)
                m_Prompts.Add(new Prompt(80, "", "Не встать: над головой препятствие"));
            if (gm.Phase.Value == RunPhase.Playing && Hatch.IsNear(me.transform.position))
            {
                var inv = me.GetComponent<PlayerInventory>();
                if (inv.Items.Count > 0)
                    m_Prompts.Add(new Prompt(60, "Tab", "Перетащи предметы на люк, чтобы сдать"));
                else if (gm.Collected.Value >= gm.Quota.Value)
                    m_Prompts.Add(new Prompt(55, "F", "Следующий дом"));
                else
                    m_Prompts.Add(new Prompt(40, "F", $"Уйти, не набрав квоту ({gm.Collected.Value}/{gm.Quota.Value})"));
            }
            var hover = InventoryUI.Local != null ? InventoryUI.Local.Hover : null;
            if (hover != null && !me.CursorMode)
                m_Prompts.Add(new Prompt(50, "Tab", $"Взять: {ItemCatalog.Describe(hover.Shape.Value, hover.Color.Value)}"));
            return UiMath.Choose(m_Prompts);
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            var me = PlayerController.Local;
            if (gm == null || me == null) return;
            GUI.depth = 10;
            UiDraw.Begin();

            DrawOverlays(gm, me);
            DrawDetection(me);
            DrawStamina(me);
            DrawNoise(me);
            DrawQuota(gm);
            DrawHatchMarker(gm, me);
            DrawHealth(me);
            DrawTeam(me);
            DrawPrompt(CurrentPrompt);
            DrawBanner();
            DrawRunOver(gm);
            DrawHelp();
        }

        // ------------------------------------------------------------------ sections

        void DrawOverlays(GameManager gm, PlayerController me)
        {
            // Last hit point left: a light red edge, slow pulse. A fresh hit: short flash + an arc towards the attacker.
            if (me.Hp.Value == 1 && !me.Downed.Value) UiDraw.Vignette(new Color(0.8f, 0f, 0f, 0.32f + 0.1f * Mathf.Sin(Time.time * 2.5f)));
            if (m_HitFlash > 0.6f) UiDraw.Rect(new Rect(0, 0, UiDraw.W, UiDraw.H), new Color(1f, 0f, 0f, 0.5f * (m_HitFlash - 0.6f)));
            float sinceHit = Time.time - LastHitTime;
            if (sinceHit < 1.6f && me.Camera != null)
            {
                var cam = me.Camera.transform;
                float angle = UiMath.ScreenAngle(cam.position, cam.forward, cam.position + LastHitDirection);
                UiDraw.DirectionArc(angle, 120f, new Color(1f, 0.15f, 0.1f, Mathf.Clamp01(1.6f - sinceHit)));
            }
            if (me.GasTime > 0f)
            {
                float g = me.GasTime / PlayerController.GasLimit;
                UiDraw.Vignette(new Color(0.4f, 0.95f, 0.3f, Mathf.Clamp01(g * 1.4f)));
                UiDraw.Rect(new Rect(0, 0, UiDraw.W, UiDraw.H), new Color(0.5f, 0.9f, 0.3f, g * 0.35f));
                if (me.Laughing)
                {
                    var jitter = new Vector2(Mathf.Sin(Time.time * 37f), Mathf.Cos(Time.time * 29f)) * 6f * g;
                    UiDraw.Label(new Rect(UiDraw.W / 2f - 200 + jitter.x, UiDraw.H * 0.3f + jitter.y, 400, 50),
                        "ХА-ХА-ХА-ХА", 30, TextAnchor.MiddleCenter, new Color(0.8f, 1f, 0.6f, 0.9f), true);
                }
                UiDraw.Label(new Rect(UiDraw.W / 2f - 200, UiDraw.H * 0.3f + 44, 400, 24),
                    $"Весёлый газ: {Mathf.Max(0, PlayerController.GasLimit - me.GasTime):0} c до отключки", 15, TextAnchor.MiddleCenter, Color.white);
            }
            if (me.Downed.Value)
            {
                UiDraw.Rect(new Rect(0, 0, UiDraw.W, UiDraw.H), new Color(0.25f, 0f, 0f, 0.55f));
                if (gm.Phase.Value == RunPhase.Playing) UiDraw.Label(new Rect(0, UiDraw.H * 0.35f, UiDraw.W, 60),
                    "Ты выведен из строя\nСоюзник может поднять тебя (удерживать E рядом)", 22, TextAnchor.MiddleCenter, Color.white, true);
            }
            if (gm.AlarmActive)
            {
                // Thin pulsing frame + a small label with the time left: clear, but does not cover the view.
                float left = (float)(gm.AlarmUntil.Value - gm.NetworkManager.ServerTime.Time);
                float pulse = 0.35f + 0.25f * Mathf.Sin(Time.time * 7f);
                var red = new Color(1f, 0.15f, 0.1f, pulse);
                UiDraw.Rect(new Rect(0, 0, UiDraw.W, 4), red);
                UiDraw.Rect(new Rect(0, UiDraw.H - 4, UiDraw.W, 4), red);
                UiDraw.Rect(new Rect(0, 0, 4, UiDraw.H), red);
                UiDraw.Rect(new Rect(UiDraw.W - 4, 0, 4, UiDraw.H), red);
                var tag = new Rect(UiDraw.W / 2f - 70, 14, 140, 24);
                UiDraw.Rect(tag, new Color(0.5f, 0f, 0f, 0.6f));
                UiDraw.Label(tag, $"ТРЕВОГА · {Mathf.Max(0, left):0} c", 14, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.8f), true);
            }
        }

        /// <summary>Arcs around the crosshair towards every sensor that is noticing you.</summary>
        static void DrawDetection(PlayerController me)
        {
            if (me.Camera == null || me.Downed.Value) return;
            var cam = me.Camera.transform;
            foreach (var d in Stealth.Enemies.DetectorAwareness.All)
            {
                float a = d.AwarenessOf(me.OwnerClientId);
                if (a < 0.03f) continue;
                float angle = UiMath.ScreenAngle(cam.position, cam.forward, d.Position);
                bool detected = a >= 0.99f;
                var color = detected
                    ? new Color(1f, 0.12f, 0.08f, 0.75f + 0.25f * Mathf.Sin(Time.time * 12f))
                    : Color.Lerp(new Color(1f, 0.95f, 0.4f, 0.35f), new Color(1f, 0.55f, 0.1f, 0.95f), a);
                UiDraw.DirectionArc(angle, 70f + (1f - a) * 25f, color);
            }
        }

        /// <summary>How loud you are right now: three short bars above the inventory, fading out when silent.</summary>
        static void DrawNoise(PlayerController me)
        {
            float age = Time.time - me.LastNoiseTime;
            if (age > 0.9f || me.Downed.Value) return;
            int bars = UiMath.NoiseBars(me.LastNoiseRadius);
            float alpha = Mathf.Clamp01(1f - age / 0.9f);
            var labels = new[] { "", "тихо", "слышно", "громко" };
            var colors = new[] { Color.white, new Color(0.6f, 0.9f, 1f), new Color(1f, 0.85f, 0.35f), new Color(1f, 0.35f, 0.25f) };
            float x0 = UiDraw.W / 2f - 30f, y0 = UiDraw.H - 150f;
            for (int i = 0; i < 3; i++)
            {
                var c = i < bars ? colors[bars] : new Color(1, 1, 1, 0.25f);
                c.a *= alpha;
                float h = 6f + i * 5f;
                UiDraw.Rect(new Rect(x0 + i * 9f, y0 + 16f - h, 6f, h), c);
            }
            var lc = colors[bars];
            lc.a = alpha * 0.9f;
            UiDraw.Label(new Rect(x0 + 32f, y0, 120f, 18f), labels[bars], 12, TextAnchor.MiddleLeft, lc);
        }

        static void DrawStamina(PlayerController me)
        {
            var stamina = me.Motor.Stamina;
            if (stamina.Value >= 0.999f || me.Downed.Value) return;
            float w = 120f;
            var r = new Rect(UiDraw.W / 2f - w / 2f, UiDraw.H / 2f + 34f, w, 6f);
            UiDraw.Rect(r, new Color(0, 0, 0, 0.45f));
            UiDraw.Rect(new Rect(r.x, r.y, w * stamina.Value, r.height),
                stamina.Exhausted ? new Color(1f, 0.35f, 0.25f, 0.9f) : new Color(1f, 1f, 1f, 0.75f));
        }

        void DrawQuota(GameManager gm)
        {
            bool met = gm.Collected.Value >= gm.Quota.Value;
            var panel = new Rect(16, 16, 232, 50);
            UiDraw.Rect(panel, new Color(0, 0, 0, 0.45f));
            UiDraw.Label(new Rect(26, 18, 120, 22), $"Дом {gm.Level.Value.Index + 1}", 14, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.75f), true);
            UiDraw.Label(new Rect(26, 18, 212, 22), $"{gm.Collected.Value} / {gm.Quota.Value}", 14, TextAnchor.MiddleRight,
                met ? new Color(0.45f, 1f, 0.5f) : Color.white, true);
            UiDraw.Bar(new Rect(26, 44, 212, 10), gm.Quota.Value > 0 ? gm.Collected.Value / (float)gm.Quota.Value : 0f,
                met ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.8f, 0.2f));
            float age = Time.time - m_GainTime;
            if (age < 2f)
                UiDraw.Label(new Rect(panel.xMax + 8, 26 - age * 8f, 120, 26), $"+{m_GainAmount}", 18, TextAnchor.MiddleLeft,
                    new Color(0.45f, 1f, 0.5f, Mathf.Clamp01(2f - age)), true);
            if (met && gm.Phase.Value == RunPhase.Playing)
                UiDraw.Label(new Rect(26, panel.yMax + 2, 400, 20), "Квота набрана — к люку, F: следующий дом", 12, TextAnchor.MiddleLeft, new Color(0.6f, 1f, 0.65f, 0.9f));
        }

        /// <summary>Once the quota is met: a small diamond over the hatch (clamped to the screen edge) with distance.</summary>
        static void DrawHatchMarker(GameManager gm, PlayerController me)
        {
            var layout = LevelGenerator.Current;
            if (layout == null || me.Camera == null || gm.Phase.Value != RunPhase.Playing) return;
            if (gm.Collected.Value < gm.Quota.Value || Hatch.IsNear(me.transform.position)) return;
            var target = layout.HatchPosition + Vector3.up * 0.4f;
            var vp = me.Camera.WorldToViewportPoint(target);
            var pos = UiMath.EdgeMarker(vp, UiDraw.W, UiDraw.H, 48f, out bool clamped);
            float dist = Vector3.Distance(me.transform.position, layout.HatchPosition);
            var c = new Color(0.45f, 1f, 0.5f, clamped ? 0.9f : 0.75f);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, pos);
            UiDraw.Rect(new Rect(pos.x - 7, pos.y - 7, 14, 14), c);
            GUI.matrix = m;
            UiDraw.Label(new Rect(pos.x - 60, pos.y + 10, 120, 18), $"люк · {dist:0} м", 12, TextAnchor.MiddleCenter, c);
        }

        static void DrawHealth(PlayerController me)
        {
            var hp = new Rect(UiDraw.W - 196, 16, 180, 40);
            UiDraw.Rect(hp, new Color(0, 0, 0, 0.55f));
            UiDraw.Label(new Rect(hp.x + 12, hp.y, 60, 40), "ХП", 15, TextAnchor.MiddleLeft, Color.white, true);
            for (int i = 0; i < PlayerController.MaxHp; i++)
                UiDraw.Rect(new Rect(hp.x + 56 + i * 54, hp.y + 10, 46, 20), i < me.Hp.Value ? new Color(0.9f, 0.2f, 0.2f) : new Color(1, 1, 1, 0.15f));
        }

        /// <summary>
        /// Teammates: a compact list under the health panel; names over heads only when close and visible;
        /// a downed teammate gets a red marker through walls so the team can come and help.
        /// </summary>
        static void DrawTeam(PlayerController me)
        {
            float ty = 62;
            var cam = me.Camera;
            foreach (var p in PlayerController.All)
            {
                if (p == me) continue;
                var c = p.Downed.Value ? new Color(1f, 0.35f, 0.3f) : new Color(1, 1, 1, 0.8f);
                string state = p.Downed.Value ? "упал" : new string('■', Mathf.Max(0, p.Hp.Value));
                UiDraw.Label(new Rect(UiDraw.W - 260, ty, 244, 18), $"{p.Nick}  {state}", 12, TextAnchor.MiddleRight, c);
                ty += 18;
                if (cam == null) continue;

                var head = p.transform.position + Vector3.up * (p.Downed.Value ? 0.6f : p.Height + 0.25f);
                var vp = cam.WorldToViewportPoint(head);
                float dist = Vector3.Distance(cam.transform.position, head);
                if (p.Downed.Value)
                {
                    var pos = UiMath.EdgeMarker(vp, UiDraw.W, UiDraw.H, 48f, out _);
                    float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 6f);
                    UiDraw.Rect(new Rect(pos.x - 3, pos.y - 10, 6, 20), new Color(1f, 0.25f, 0.2f, pulse));
                    UiDraw.Rect(new Rect(pos.x - 10, pos.y - 3, 20, 6), new Color(1f, 0.25f, 0.2f, pulse));
                    UiDraw.Label(new Rect(pos.x - 80, pos.y + 12, 160, 18), $"{p.Nick} · {dist:0} м", 12, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.45f));
                }
                else if (vp.z > 0f && dist < 14f && !Physics.Linecast(cam.transform.position, head, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    float a = Mathf.Clamp01((14f - dist) / 4f) * 0.8f;
                    var pos = new Vector2(vp.x * UiDraw.W, (1f - vp.y) * UiDraw.H);
                    UiDraw.Label(new Rect(pos.x - 80, pos.y - 18, 160, 18), p.Nick, 12, TextAnchor.MiddleCenter, new Color(1, 1, 1, a));
                }
            }
        }

        /// <summary>A small pill under the crosshair: [key] text, optional hold progress.</summary>
        static void DrawPrompt(Prompt p)
        {
            if (!p.IsValid) return;
            float textW = Mathf.Min(560f, p.Text.Length * 7.6f + 16f);
            float keyW = string.IsNullOrEmpty(p.Key) ? 0f : Mathf.Max(26f, p.Key.Length * 10f + 12f);
            float w = textW + keyW + (keyW > 0 ? 8f : 0f) + 16f;
            var r = new Rect(UiDraw.W / 2f - w / 2f, UiDraw.H / 2f + 52f, w, 30f);
            UiDraw.Rect(r, new Color(0f, 0f, 0f, 0.5f));
            float x = r.x + 8f;
            if (keyW > 0f)
            {
                var k = new Rect(x, r.y + 4f, keyW, 22f);
                UiDraw.Rect(k, new Color(1f, 1f, 1f, 0.9f));
                UiDraw.Label(k, p.Key, 13, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f), true);
                x += keyW + 8f;
            }
            UiDraw.Label(new Rect(x, r.y, textW, r.height), p.Text, 15, TextAnchor.MiddleLeft, Color.white);
            if (p.Progress >= 0f) UiDraw.Bar(new Rect(r.x, r.yMax + 2f, r.width, 6f), p.Progress, Color.white);
        }

        void DrawBanner()
        {
            if (string.IsNullOrEmpty(m_Banner)) return;
            float a = UiMath.MessageAlpha(Time.time - m_BannerStart, BannerDuration);
            if (a <= 0f) return;
            var r = new Rect(UiDraw.W / 2f - 320, UiDraw.H * 0.16f - (1f - a) * 10f, 640, 40);
            UiDraw.Rect(r, new Color(0, 0, 0, 0.5f * a));
            UiDraw.Label(r, m_Banner, 16, TextAnchor.MiddleCenter, new Color(1, 1, 1, a), true);
        }

        static void DrawRunOver(GameManager gm)
        {
            if (gm.Phase.Value != RunPhase.Failed) return;
            var r = new Rect(UiDraw.W / 2f - 260, UiDraw.H * 0.38f, 520, 120);
            UiDraw.Rect(r, new Color(0, 0, 0, 0.8f));
            UiDraw.Label(new Rect(r.x, r.y + 14, r.width, 40), "Забег окончен", 26, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.35f), true);
            UiDraw.Label(new Rect(r.x, r.y + 56, r.width, 24), $"Сдано за забег: {gm.RunTotal.Value} очк.", 16, TextAnchor.MiddleCenter, Color.white);
            UiDraw.Label(new Rect(r.x, r.y + 84, r.width, 24), "R — начать новый забег", 15, TextAnchor.MiddleCenter, Color.white);
        }

        void DrawHelp()
        {
            if (!m_ShowHelp) return;
            const string help = "WASD — ходить · Shift — бег · Пробел — прыжок · Ctrl/C — присесть\n" +
                                "Tab (держать) — руки: тащи предметы мышкой, R — повернуть\n" +
                                "E — поднять союзника · F у люка — следующий дом · Esc — меню · H — эта справка";
            UiDraw.Label(new Rect(16, UiDraw.H - 76, 560, 64), help, 12, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.7f));
        }
    }
}
