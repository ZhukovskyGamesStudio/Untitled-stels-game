using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Keeps the rooms' reflection probes (<see cref="RoomProbe"/>, rules — <see cref="RoomProbes"/>) up to date: one
    /// picture at a time, near the camera, when a room's lamps changed; and their strength with the lamps every tick.
    /// Every peer, rendering only; ticked by <see cref="LightBudget"/>.
    /// </summary>
    public sealed class RoomProbeField
    {
        /// <summary>How strong the style shows the rooms' reflections (StyleGrade.Probes; 0 = none, the probes are off).</summary>
        public static float Strength { get; set; }

        readonly List<RoomProbes.State> m_States = new();
        readonly Dictionary<int, float> m_Lamps = new();
        RoomProbe m_Taking;

        static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        public void Tick(float now, Vector3 camera)
        {
            var all = RoomProbe.All;
            if (Strength <= 0f)
            {
                foreach (var p in all) if (p.Probe.enabled) p.Probe.enabled = false;
                return;
            }
            m_Lamps.Clear();
            foreach (var lamp in RoomLight.All)
            {
                if (lamp == null || lamp.Room < 0) continue;
                m_Lamps.TryGetValue(lamp.Room, out float sum);
                m_Lamps[lamp.Room] = sum + Luma(lamp.Output);
            }
            m_States.Clear();
            foreach (var p in all)
            {
                m_Lamps.TryGetValue(p.Room, out float lamps);
                bool taken = p.LampsThen >= 0f;
                if (taken)
                {
                    float intensity = Strength * RoomProbes.Share(lamps, p.LampsThen);
                    if (Mathf.Abs(p.Probe.intensity - intensity) > 0.01f) p.Probe.intensity = intensity;
                    if (!p.Probe.enabled) p.Probe.enabled = true;
                }
                m_States.Add(new RoomProbes.State
                {
                    Distance = Mathf.Sqrt(p.Box.SqrDistance(camera)),
                    Taken = taken,
                    Stale = taken && RoomProbes.Stale(lamps, p.LampsThen),
                    Age = now - p.TakenAt,
                    Dark = lamps <= 0.001f,
                });
            }
            if (m_Taking != null && m_Taking.Busy) return;
            m_Taking = null;
            int next = RoomProbes.Next(m_States);
            if (next < 0) return;
            m_Taking = all[next];
            m_Lamps.TryGetValue(m_Taking.Room, out float burning);
            m_Taking.Take(burning, now);
        }
    }
}
