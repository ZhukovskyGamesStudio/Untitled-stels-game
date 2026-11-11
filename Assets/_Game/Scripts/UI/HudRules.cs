using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;

namespace Stealth.UI
{
    /// <summary>Everything the prompt under the crosshair depends on, gathered by the HUD each frame.</summary>
    public struct PromptContext
    {
        public bool Downed, MenuOpen, CursorMode;
        public bool CanRevive;
        public float ReviveProgress;
        /// <summary>Name of the robot / device that can be hacked, null if none.</summary>
        public string HackTarget;
        public float HackProgress;
        /// <summary>How long a hack lasts (shown in the prompt).</summary>
        public float HackSeconds;
        public bool BlockedByCeiling;
        public bool Playing, NearHatch;
        /// <summary>At the stash board (the hatch of a house or the board in the sewer).</summary>
        public bool NearTrade;
        public bool InHatch, InSewer;
        /// <summary>A locked door in reach and how far its hack is (0..1).</summary>
        public bool LockedDoor;
        public float DoorProgress;
        /// <summary>A closed safe in reach / turning a safe's dial now.</summary>
        public bool SafeNear, Cracking;
        public int CarriedItems, Collected, Quota;
        /// <summary>Robot / device in the crosshair: "name: senses", null if none.</summary>
        public string LookedAt;
        /// <summary>World item under the crosshair, null if none.</summary>
        public string HoverItem;
    }

    /// <summary>
    /// Which single hint to show under the crosshair (pure, unit tested). Higher priority wins:
    /// out of the hatch 90 · revive 100 · cannot stand 80 · hack 70 · door 68 · safe 66 · deposit 60 · hatch 55 · take item 50 ·
    /// robot info 45 · route map in the sewer 30.
    /// </summary>
    public static class PromptRules
    {
        static readonly List<Prompt> s_Prompts = new();

        /// <summary>What a player lying on the floor reads.</summary>
        public static string DownedText(int hp, bool canGetUp) =>
            hp <= 0 ? $"Без сознания\nСоюзник может поднять тебя ({Controls.Label(GameAction.Interact)} рядом)"
            : canGetUp ? $"Ты на полу\nЛежачих роботы не трогают — дождись, пока уйдут\n{Controls.Label(GameAction.Jump)} — встать"
            : "Сбит с ног!";

        public static Prompt Choose(in PromptContext c)
        {
            s_Prompts.Clear();
            if (c.Downed || c.MenuOpen || c.Cracking) return default;
            string hatch = Controls.Label(GameAction.Hatch);
            if (c.InHatch) return new Prompt(90, hatch, "Вылезти из люка");
            string use = Controls.Label(GameAction.Interact);
            if (c.CanRevive) s_Prompts.Add(new Prompt(100, use, "Удерживай — поднять союзника", c.ReviveProgress));
            if (c.HackTarget != null) s_Prompts.Add(new Prompt(70, use, $"Удерживай — взломать: {c.HackTarget} ({c.HackSeconds:0} с)", c.HackProgress));
            if (c.BlockedByCeiling) s_Prompts.Add(new Prompt(80, "", "Не встать: над головой препятствие"));
            if (c.LockedDoor) s_Prompts.Add(new Prompt(68, use, "Удерживай — взломать замок двери", c.DoorProgress));
            if (c.SafeNear) s_Prompts.Add(new Prompt(66, use, "Вскрыть сейф: подобрать код на слух"));
            // With the cursor out the board speaks for itself.
            if (c.NearTrade && c.CarriedItems > 0 && !c.CursorMode)
                s_Prompts.Add(new Prompt(60, Controls.Label(GameAction.Hands), "Разложить добычу: на склад или в «Продать»"));
            if (c.Playing && c.NearHatch)
                s_Prompts.Add(new Prompt(55, hatch, c.Collected >= c.Quota
                    ? "Спрятаться в люк — уйти в канализацию"
                    : $"Спрятаться в люк ({c.Collected}/{c.Quota}: квоту можно доплатить внизу)"));
            if (c.InSewer) s_Prompts.Add(new Prompt(30, "", "Выбери путь: зайди в один из туннелей"));
            if (c.LookedAt != null) s_Prompts.Add(new Prompt(45, "", c.LookedAt));
            if (c.HoverItem != null && !c.CursorMode) s_Prompts.Add(new Prompt(50, "ЛКМ", $"Взять: {c.HoverItem} (зажми и тащи в рюкзак)"));
            return UiMath.Choose(s_Prompts);
        }
    }

    /// <summary>Banners shown one after another (pure, unit tested): at most four waiting, no immediate repeats.</summary>
    public sealed class BannerQueue
    {
        public const float Duration = 3.5f;
        public const int Capacity = 4;

        readonly Queue<string> m_Queue = new();
        float m_Start = float.NegativeInfinity;

        public string Current { get; private set; }
        public int Waiting => m_Queue.Count;

        public void Push(string text)
        {
            if (string.IsNullOrEmpty(text) || m_Queue.Count >= Capacity || text == Current || m_Queue.Contains(text)) return;
            m_Queue.Enqueue(text);
        }

        public void Update(float now)
        {
            if (now - m_Start < Duration) return;
            if (m_Queue.Count == 0) { Current = null; return; }
            Current = m_Queue.Dequeue();
            m_Start = now;
        }

        public float Alpha(float now) => Current == null ? 0f : UiMath.MessageAlpha(now - m_Start, Duration);
        public float Age(float now) => now - m_Start;
    }

    /// <summary>"+N" next to the quota after a deposit; deposits close together add up (pure, unit tested).</summary>
    public sealed class GainTracker
    {
        public const float MergeWindow = 1.5f;
        public const float ShowTime = 2f;

        int m_Last = -1;

        public int Amount { get; private set; }
        public float Time { get; private set; } = float.NegativeInfinity;

        public void Observe(int collected, float now)
        {
            if (m_Last >= 0 && collected > m_Last)
            {
                Amount = (now - Time < MergeWindow ? Amount : 0) + (collected - m_Last);
                Time = now;
            }
            m_Last = collected;
        }

        public bool Visible(float now) => now - Time < ShowTime;
    }

    /// <summary>Hit flash and direction of the last hit (pure).</summary>
    public sealed class HitFeedback
    {
        public const float ArcTime = 1.6f;

        public float Flash { get; private set; }
        public Vector3 Direction { get; private set; }
        public float Time { get; private set; } = float.NegativeInfinity;

        public void Hit(Vector3 awayFromSource, float now)
        {
            Flash = 1f;
            Direction = -awayFromSource;
            Time = now;
        }

        public void Update(float dt) => Flash = Mathf.MoveTowards(Flash, 0f, dt * 1.5f);

        public float ArcAlpha(float now) => Mathf.Clamp01(ArcTime - (now - Time));
    }
}
