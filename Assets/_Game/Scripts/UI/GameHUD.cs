using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.UI
{
    /// <summary>Placeholder HUD: quota, health, gas, alarm, hints and banners.</summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        string m_Banner;
        float m_BannerUntil;
        float m_HitFlash;
        bool m_ShowHelp = true;
        GameManager m_Subscribed;

        void Awake() => Instance = this;

        public void FlashHit() => m_HitFlash = 1f;

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
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) m_ShowHelp = !m_ShowHelp;
        }

        void ShowBanner(string text)
        {
            m_Banner = text;
            m_BannerUntil = Time.time + 5f;
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            var me = PlayerController.Local;
            if (gm == null || me == null) return;
            GUI.depth = 10;

            // Overlays first.
            if (me.Hp.Value == 1 && !me.Downed.Value) UiDraw.Vignette(new Color(0.85f, 0f, 0f, 0.65f + 0.15f * Mathf.Sin(Time.time * 4f)));
            if (m_HitFlash > 0f) UiDraw.Rect(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0f, 0f, 0.35f * m_HitFlash));
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

            // Stamina: only visible while not full.
            var stamina = me.Motor.Stamina;
            if (stamina.Value < 0.999f && !me.Downed.Value)
            {
                float w = 120f;
                var r = new Rect(Screen.width / 2f - w / 2f, Screen.height / 2f + 34f, w, 6f);
                UiDraw.Rect(r, new Color(0, 0, 0, 0.45f));
                UiDraw.Rect(new Rect(r.x, r.y, w * stamina.Value, r.height),
                    stamina.Exhausted ? new Color(1f, 0.35f, 0.25f, 0.9f) : new Color(1f, 1f, 1f, 0.75f));
            }

            // Quota.
            var panel = new Rect(16, 16, 300, 92);
            UiDraw.Rect(panel, new Color(0, 0, 0, 0.55f));
            UiDraw.Label(new Rect(28, 22, 280, 22), $"Дом №{gm.Level.Value.Index + 1}", 15, TextAnchor.MiddleLeft, Color.white, true);
            UiDraw.Label(new Rect(28, 44, 280, 20), $"Квота: {gm.Collected.Value} / {gm.Quota.Value}", 14, TextAnchor.MiddleLeft, Color.white);
            UiDraw.Bar(new Rect(28, 68, 276, 14), gm.Quota.Value > 0 ? gm.Collected.Value / (float)gm.Quota.Value : 0f,
                gm.Collected.Value >= gm.Quota.Value ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.8f, 0.2f));
            UiDraw.Label(new Rect(28, 84, 280, 20), $"За забег: {gm.RunTotal.Value}", 12, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.7f));

            // Health.
            var hp = new Rect(Screen.width - 196, 16, 180, 40);
            UiDraw.Rect(hp, new Color(0, 0, 0, 0.55f));
            UiDraw.Label(new Rect(hp.x + 12, hp.y, 60, 40), "ХП", 15, TextAnchor.MiddleLeft, Color.white, true);
            for (int i = 0; i < PlayerController.MaxHp; i++)
                UiDraw.Rect(new Rect(hp.x + 56 + i * 54, hp.y + 10, 46, 20), i < me.Hp.Value ? new Color(0.9f, 0.2f, 0.2f) : new Color(1, 1, 1, 0.15f));

            // Team.
            float ty = 64;
            foreach (var p in PlayerController.All)
            {
                if (p == me) continue;
                string state = p.Downed.Value ? "<color=#ff5050>выведен из строя</color>" : $"ХП {p.Hp.Value}";
                UiDraw.Label(new Rect(Screen.width - 260, ty, 244, 20), $"Игрок {p.OwnerClientId}: {state}", 13, TextAnchor.MiddleRight, Color.white);
                ty += 20;
            }

            // Hints.
            string hint = null;
            if (me.ReviveTarget != null)
            {
                hint = "Удерживай E — поднять союзника";
                UiDraw.Bar(new Rect(Screen.width / 2f - 100, Screen.height * 0.56f + 30, 200, 12), me.ReviveProgress / PlayerController.ReviveTime, Color.white);
            }
            else if (Hatch.IsNear(me.transform.position) && gm.Phase.Value == RunPhase.Playing)
            {
                hint = gm.Collected.Value >= gm.Quota.Value
                    ? "Люк: перетащи предметы на люк, чтобы сдать · F — следующий дом"
                    : "Люк: перетащи предметы на люк, чтобы сдать · F — завершить (квота не набрана!)";
            }
            if (hint != null) UiDraw.Label(new Rect(Screen.width / 2f - 400, Screen.height * 0.56f, 800, 28), hint, 15, TextAnchor.MiddleCenter, Color.white, true);

            if (Time.time < m_BannerUntil && !string.IsNullOrEmpty(m_Banner))
            {
                var r = new Rect(Screen.width / 2f - 340, Screen.height * 0.18f, 680, 56);
                UiDraw.Rect(r, new Color(0, 0, 0, 0.6f));
                UiDraw.Label(r, m_Banner, 18, TextAnchor.MiddleCenter, Color.white, true);
            }

            if (gm.Phase.Value == RunPhase.Failed)
            {
                var r = new Rect(Screen.width / 2f - 260, Screen.height * 0.38f, 520, 120);
                UiDraw.Rect(r, new Color(0, 0, 0, 0.8f));
                UiDraw.Label(new Rect(r.x, r.y + 14, r.width, 40), "Забег окончен", 26, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.35f), true);
                UiDraw.Label(new Rect(r.x, r.y + 56, r.width, 24), $"Сдано за забег: {gm.RunTotal.Value} очк.", 16, TextAnchor.MiddleCenter, Color.white);
                UiDraw.Label(new Rect(r.x, r.y + 84, r.width, 24), "R — начать новый забег", 15, TextAnchor.MiddleCenter, Color.white);
            }

            if (m_ShowHelp)
            {
                const string help = "WASD — ходить · Shift — бег (громко) · Ctrl/C — присесть (тихо)\n" +
                                    "Tab (держать) — курсор: тащи предметы мышкой, R — повернуть\n" +
                                    "E (держать) — поднять союзника · F у люка — следующий дом · Esc — меню · H — скрыть подсказку";
                UiDraw.Label(new Rect(16, Screen.height - 76, 560, 64), help, 12, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.75f));
            }
        }
    }
}
