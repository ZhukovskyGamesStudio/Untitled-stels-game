using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// IT people can hack the machines — for a while. Rules (pure, unit tested):
    /// devices are hacked up close; mobile robots from behind or the side and only while they are not chasing you.
    /// </summary>
    public static class Hacking
    {
        /// <summary>Default reach; the game uses PlayerConfig.Interaction (hold time, duration, reach).</summary>
        public const float Reach = 1.9f;
        /// <summary>A robot is hacked from further back than this angle off its nose (90 = exactly at its side).</summary>
        public const float BehindAngle = 95f;

        public static bool CanHackRobot(Vector3 robotPos, Vector3 robotForward, Vector3 player, bool chasing, float reach = Reach)
        {
            if (chasing) return false;
            var to = player - robotPos;
            to.y = 0;
            if (to.magnitude > reach) return false;
            var f = new Vector3(robotForward.x, 0, robotForward.z);
            return Vector3.Angle(f, to) > BehindAngle; // standing behind it or at its side
        }

        public static bool CanHackDevice(Vector3 devicePos, Vector3 player, float reach = Reach)
        {
            var d = player - devicePos;
            float vertical = Mathf.Abs(d.y);
            d.y = 0;
            return d.magnitude <= reach && vertical < 3.2f; // wall / ceiling devices are reachable from below
        }
    }

    /// <summary>
    /// Progress of holding E on several targets (pure): walking away keeps what was done, coming back goes on from there.
    /// A target is forgotten once finished or when told so (the hack landed, a new house).
    /// </summary>
    public sealed class HoldProgress<T> where T : class
    {
        readonly Dictionary<T, float> m_Done = new();

        public float Of(T target) => target != null && m_Done.TryGetValue(target, out var v) ? v : 0f;

        /// <summary>Holds on <paramref name="target"/> for <paramref name="dt"/>; returns true (and forgets it) once <paramref name="needed"/> is reached.</summary>
        public bool Hold(T target, float dt, float needed)
        {
            if (target == null) return false;
            float v = Of(target) + dt;
            if (v >= needed)
            {
                m_Done.Remove(target);
                return true;
            }
            m_Done[target] = v;
            return false;
        }

        public void Forget(T target)
        {
            if (target != null) m_Done.Remove(target);
        }

        public void Clear() => m_Done.Clear();

        public int Count => m_Done.Count;
    }
}
