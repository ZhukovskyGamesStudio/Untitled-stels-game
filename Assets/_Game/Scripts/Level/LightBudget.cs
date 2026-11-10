using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Keeps the shadow atlas from overflowing (on the LevelRoot prefab, every peer, rendering only). Every lamp casts
    /// shadows — that is what keeps light inside its room — but the atlas holds only so many shadow maps; when URP has to
    /// squeeze in more it lowers their resolution, and with dozens of lamps in view the player has crashed right then.
    /// So only the lamps nearest to the camera that fit the atlas are drawn; the rest are hidden (culling mask 0) until the
    /// camera comes close. Gameplay is untouched: robots read the light through <see cref="LightSource"/>, not the renderer.
    /// </summary>
    public class LightBudget : MonoBehaviour
    {
        /// <summary>Shadow atlas (4096²) in units of one 1024² map; a bit is kept spare.</summary>
        public const float Budget = 13f;

        struct Entry
        {
            public Light Light;
            public LightShadows Shadows;
            public int Mask;
            public float Cost;
        }

        readonly List<Entry> m_Lights = new();
        readonly List<(float distance, int index)> m_Order = new();
        float m_NextScan, m_NextSort;

        /// <summary>
        /// Shadow atlas cost of a light (pure): a point light renders six maps; room-sized lights use medium maps (512²),
        /// small lamps and status lights low ones (256²).
        /// </summary>
        public static float Cost(LightType type, float range)
        {
            float map = range >= 4f ? 0.25f : 0.0625f;
            return type == LightType.Point ? 6f * map : map;
        }

        /// <summary>Which lights to draw, nearest first, while they fit the budget (pure).</summary>
        public static void Choose(IReadOnlyList<float> costsNearestFirst, float budget, bool[] into)
        {
            float used = 0f;
            for (int i = 0; i < costsNearestFirst.Count; i++)
            {
                into[i] = used + costsNearestFirst[i] <= budget;
                if (into[i]) used += costsNearestFirst[i];
            }
        }

        static readonly List<float> s_Costs = new();
        bool[] m_On = new bool[0];

        /// <summary>Right after the level is built: the very first frame must not render every lamp with shadows.</summary>
        public void ApplyNow()
        {
            m_NextScan = 0f;
            m_NextSort = 0f;
            Update();
        }

        void Update()
        {
            if (Time.time >= m_NextScan) Scan();
            if (Time.time < m_NextSort) return;
            m_NextSort = Time.time + 0.2f;
            var cam = PlayerController.Local != null && PlayerController.Local.Camera != null ? PlayerController.Local.Camera : Camera.main;
            if (cam == null) return;
            var eye = cam.transform.position;
            m_Order.Clear();
            for (int i = 0; i < m_Lights.Count; i++)
                if (m_Lights[i].Light != null) m_Order.Add(((m_Lights[i].Light.transform.position - eye).sqrMagnitude, i));
            m_Order.Sort((a, b) => a.distance.CompareTo(b.distance));
            s_Costs.Clear();
            foreach (var (_, i) in m_Order) s_Costs.Add(m_Lights[i].Cost);
            if (m_On.Length < s_Costs.Count) m_On = new bool[s_Costs.Count * 2];
            Choose(s_Costs, Budget, m_On);
            for (int k = 0; k < m_Order.Count; k++)
            {
                var e = m_Lights[m_Order[k].index];
                bool on = m_On[k];
                var shadows = on ? e.Shadows : LightShadows.None;
                if (e.Light.shadows != shadows) e.Light.shadows = shadows;
                int mask = on ? e.Mask : 0;
                if (e.Light.cullingMask != mask) e.Light.cullingMask = mask;
            }
        }

        /// <summary>Picks up lights that appeared (robots spawn, doors, lamps) — the ones that cast shadows.</summary>
        void Scan()
        {
            m_NextScan = Time.time + 1f;
            var known = new HashSet<Light>();
            for (int i = m_Lights.Count - 1; i >= 0; i--)
                if (m_Lights[i].Light == null) m_Lights.RemoveAt(i);
                else known.Add(m_Lights[i].Light);
            foreach (var l in FindObjectsByType<Light>())
            {
                if (l.type == LightType.Directional || l.shadows == LightShadows.None || known.Contains(l)) continue;
                m_Lights.Add(new Entry { Light = l, Shadows = l.shadows, Mask = l.cullingMask, Cost = Cost(l.type, l.range) });
            }
        }
    }
}
