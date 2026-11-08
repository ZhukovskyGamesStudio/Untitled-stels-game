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

        string m_Banner;
        float m_BannerUntil;
        float m_HitFlash;
        bool m_ShowHelp = true;
        float m_HelpUntil = -1f;
        GameManager m_Subscribed;
        readonly List<Prompt> m_Prompts = new();

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
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                m_ShowHelp = !m_ShowHelp;
                m_HelpUntil = float.MaxValue;
            }
            if (m_HelpUntil < 0f && PlayerController.Local != null) m_HelpUntil = Time.time + HelpDuration;
            if (m_HelpUntil > 0f && Time.time > m_HelpUntil) m_ShowHelp = false;

            CurrentPrompt = gm != null && PlayerController.Local != null ? BuildPrompt(gm, PlayerController.Local) : default;
        }

        void ShowBanner(string text)
        {
            m_Banner = text;
            m_BannerUntil = Time.time + 5f;
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

            DrawOverlays(gm, me);
            DrawStamina(me);
            DrawQuota(gm);
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
            if (m_HitFlash > 0.6f) UiDraw.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0f, 0f, 0.5f * (m_HitFlash - 0.6f)));
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
                UiDraw.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.5f, 0.9f, 0.3f, g * 0.35f));
                if (me.Laughing)
                {
                    var jitter = new Vector2(Mathf.Sin(Time.time * 37f), Mathf.Cos(Time.time * 29f)) * 6f * g;
                    UiDraw.Label(new Rect(Screen.width / 2f - 200 + jitter.x, Screen.height * 0.3f + jitter.y, 400, 50),
                        "ХА-ХА-ХА-ХА", 30, TextAnchor.MiddleCenter, new Color(0.8f, 1f, 0.6f, 0.9f), true);
                }
                UiDraw.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.3f + 44, 400, 24),
                    $"Весёлый газ: {Mathf.Max(0, PlayerController.GasLimit - me.GasTime):0} c до отключки", 15, TextAnchor.MiddleCenter, Color.white);
            }
            if (me.Downed.Value)
            {
                UiDraw.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.25f, 0f, 0f, 0.55f));
                UiDraw.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 60),
                    "Ты выведен из строя\nСоюзник может поднять тебя (удерживать E рядом)", 22, TextAnchor.MiddleCenter, Color.white, true);
            }
            if (gm.AlarmActive && Mathf.Repeat(Time.time, 0.6f) < 0.4f)
                UiDraw.Label(new Rect(0, 56, Screen.width, 40), "ТРЕВОГА", 28, TextAnchor.MiddleCenter, new Color(1f, 0.2f, 0.15f), true);
        }

        static void DrawStamina(PlayerController me)
        {
            var stamina = me.Motor.Stamina;
            if (stamina.Value >= 0.999f || me.Downed.Value) return;
            float w = 120f;
            var r = new Rect(Screen.width / 2f - w / 2f, Screen.height / 2f + 34f, w, 6f);
            UiDraw.Rect(r, new Color(0, 0, 0, 0.45f));
            UiDraw.Rect(new Rect(r.x, r.y, w * stamina.Value, r.height),
                stamina.Exhausted ? new Color(1f, 0.35f, 0.25f, 0.9f) : new Color(1f, 1f, 1f, 0.75f));
        }

        static void DrawQuota(GameManager gm)
        {
            var panel = new Rect(16, 16, 300, 92);
            UiDraw.Rect(panel, new Color(0, 0, 0, 0.55f));
            UiDraw.Label(new Rect(28, 22, 280, 22), $"Дом №{gm.Level.Value.Index + 1}", 15, TextAnchor.MiddleLeft, Color.white, true);
            UiDraw.Label(new Rect(28, 44, 280, 20), $"Квота: {gm.Collected.Value} / {gm.Quota.Value}", 14, TextAnchor.MiddleLeft, Color.white);
            UiDraw.Bar(new Rect(28, 68, 276, 14), gm.Quota.Value > 0 ? gm.Collected.Value / (float)gm.Quota.Value : 0f,
                gm.Collected.Value >= gm.Quota.Value ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.8f, 0.2f));
            UiDraw.Label(new Rect(28, 84, 280, 20), $"За забег: {gm.RunTotal.Value}", 12, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.7f));
        }

        static void DrawHealth(PlayerController me)
        {
            var hp = new Rect(Screen.width - 196, 16, 180, 40);
            UiDraw.Rect(hp, new Color(0, 0, 0, 0.55f));
            UiDraw.Label(new Rect(hp.x + 12, hp.y, 60, 40), "ХП", 15, TextAnchor.MiddleLeft, Color.white, true);
            for (int i = 0; i < PlayerController.MaxHp; i++)
                UiDraw.Rect(new Rect(hp.x + 56 + i * 54, hp.y + 10, 46, 20), i < me.Hp.Value ? new Color(0.9f, 0.2f, 0.2f) : new Color(1, 1, 1, 0.15f));
        }

        static void DrawTeam(PlayerController me)
        {
            float ty = 64;
            foreach (var p in PlayerController.All)
            {
                if (p == me) continue;
                string state = p.Downed.Value ? "<color=#ff5050>выведен из строя</color>" : $"ХП {p.Hp.Value}";
                UiDraw.Label(new Rect(Screen.width - 260, ty, 244, 20), $"Игрок {p.OwnerClientId}: {state}", 13, TextAnchor.MiddleRight, Color.white);
                ty += 20;
            }
        }

        /// <summary>A small pill under the crosshair: [key] text, optional hold progress.</summary>
        static void DrawPrompt(Prompt p)
        {
            if (!p.IsValid) return;
            float textW = Mathf.Min(560f, p.Text.Length * 7.6f + 16f);
            float keyW = string.IsNullOrEmpty(p.Key) ? 0f : Mathf.Max(26f, p.Key.Length * 10f + 12f);
            float w = textW + keyW + (keyW > 0 ? 8f : 0f) + 16f;
            var r = new Rect(Screen.width / 2f - w / 2f, Screen.height / 2f + 52f, w, 30f);
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
            if (Time.time >= m_BannerUntil || string.IsNullOrEmpty(m_Banner)) return;
            var r = new Rect(Screen.width / 2f - 340, Screen.height * 0.18f, 680, 56);
            UiDraw.Rect(r, new Color(0, 0, 0, 0.6f));
            UiDraw.Label(r, m_Banner, 18, TextAnchor.MiddleCenter, Color.white, true);
        }

        static void DrawRunOver(GameManager gm)
        {
            if (gm.Phase.Value != RunPhase.Failed) return;
            var r = new Rect(Screen.width / 2f - 260, Screen.height * 0.38f, 520, 120);
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
            UiDraw.Label(new Rect(16, Screen.height - 76, 560, 64), help, 12, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.7f));
        }
    }
}
