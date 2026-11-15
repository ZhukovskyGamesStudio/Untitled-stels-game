using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The perf scenario's look inside a frame (dev tooling, not used in the game): the main and render thread and the GPU
    /// from <see cref="FrameTimingManager"/>, and — in a development player (<c>DEV=1 Tools/dev.sh player</c>) — every
    /// profiler marker of the scripts and the engine's big systems, so the worst offenders can be listed by milliseconds.
    /// </summary>
    public sealed class PerfProfile : IDisposable
    {
        readonly List<(string name, ProfilerRecorder rec)> m_Markers = new();
        readonly Dictionary<string, double> m_Total = new();
        readonly FrameTiming[] m_Timing = new FrameTiming[1];
        double m_Cpu, m_Render, m_Gpu, m_Alloc;
        float m_Worst;
        int m_Frames, m_AllFrames, m_Hitches, m_Gc0;
        ProfilerRecorder m_GcAlloc;

        public PerfProfile()
        {
            m_GcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var h in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (d.Category != ProfilerCategory.Scripts && d.Category != ProfilerCategory.Render && d.Category != ProfilerCategory.Physics &&
                    d.Category != ProfilerCategory.Ai && d.Category != ProfilerCategory.Animation && d.Category != ProfilerCategory.Gui &&
                    d.Category != ProfilerCategory.Lighting) continue;
                if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                m_Markers.Add((d.Name, ProfilerRecorder.StartNew(d.Category, d.Name, 1)));
                if (m_Markers.Count >= 1500) break;
            }
        }

        public void Begin()
        {
            m_Cpu = m_Render = m_Gpu = m_Alloc = 0;
            m_Worst = 0f;
            m_Frames = m_Hitches = 0;
            m_Gc0 = GC.CollectionCount(0);
        }

        /// <summary>Once a frame.</summary>
        public void Sample()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0)
            {
                m_Cpu += m_Timing[0].cpuMainThreadFrameTime;
                m_Render += m_Timing[0].cpuRenderThreadFrameTime;
                m_Gpu += m_Timing[0].gpuFrameTime;
            }
            float dt = Time.unscaledDeltaTime * 1000f;
            if (m_Frames > 0) { m_Worst = Mathf.Max(m_Worst, dt); if (dt > 33f) m_Hitches++; }
            if (m_GcAlloc.Valid) m_Alloc += m_GcAlloc.LastValue;
            m_Frames++;
            m_AllFrames++;
            foreach (var (name, rec) in m_Markers)
            {
                if (!rec.Valid) continue;
                double ms = rec.LastValue * 1e-6;
                if (ms <= 0) continue;
                m_Total[name] = (m_Total.TryGetValue(name, out var t) ? t : 0) + ms;
            }
        }

        public string Summary() => m_Frames == 0 ? "" :
            $"main {m_Cpu / m_Frames:0.0} ms, render {m_Render / m_Frames:0.0} ms, gpu {m_Gpu / m_Frames:0.0} ms, worst frame {m_Worst:0} ms, " +
            $"{m_Hitches} hitches > 33 ms, GC {m_Alloc / m_Frames / 1024:0.0} KB/frame, {GC.CollectionCount(0) - m_Gc0} collections" +
            (m_Markers.Count == 0 ? " (no markers: not a development player)" : "");

        /// <summary>The markers that took the most, ms per frame over every sampled frame.</summary>
        public IEnumerable<string> Top(int n) => m_Total.OrderByDescending(kv => kv.Value).Take(n)
            .Select(kv => $"{kv.Value / Math.Max(1, m_AllFrames):0.00} ms  {kv.Key}");

        public void Dispose()
        {
            foreach (var (_, rec) in m_Markers) rec.Dispose();
            m_GcAlloc.Dispose();
            m_Markers.Clear();
        }
    }
}
