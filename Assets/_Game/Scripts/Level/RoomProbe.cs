using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A room's reflection probe (prefab Level/RoomProbe, one per room; rules — <see cref="RoomProbes"/>): the engine takes
    /// its cube picture when <see cref="RoomProbeField"/> asks, a face a frame.
    /// </summary>
    public class RoomProbe : MonoBehaviour
    {
        /// <summary>Every room probe standing in the world.</summary>
        public static readonly List<RoomProbe> All = new();

        public ReflectionProbe Probe;

        /// <summary>Its room's id in the plan.</summary>
        public int Room { get; private set; } = -1;
        /// <summary>Its box in the world.</summary>
        public Bounds Box { get; private set; }
        /// <summary>The room's lamps when the picture was taken (−1: no picture yet).</summary>
        public float LampsThen { get; private set; } = -1f;
        /// <summary>When the picture was taken.</summary>
        public float TakenAt { get; private set; }

        int m_Render = -1;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Stands it in its room (<see cref="RoomProbes.Box"/>).</summary>
        public void Setup(int room, Vector3 eye, Vector3 centre, Vector3 size)
        {
            Room = room;
            transform.position = eye;
            Probe.center = centre - eye;
            Probe.size = size;
            Box = new Bounds(centre, size);
            Probe.enabled = false; // (no picture: nothing to show yet)
        }

        /// <summary>Whether a picture is being taken now.</summary>
        public bool Busy => m_Render >= 0 && !Probe.IsFinishedRendering(m_Render);

        /// <summary>Starts a new picture, the lamps burning like this.</summary>
        public void Take(float lamps, float now)
        {
            Probe.enabled = true;
            m_Render = Probe.RenderProbe();
            LampsThen = lamps;
            TakenAt = now;
        }
    }
}
