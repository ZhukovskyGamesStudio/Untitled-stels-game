using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Rotating laser line geometry shared by the ceiling vacuum and the floor lidar (pure, unit tested).</summary>
    public static class ScanLine
    {
        /// <summary>Direction of a line rotating at <paramref name="degPerSec"/> at time <paramref name="t"/>.</summary>
        public static Vector3 Direction(float t, float degPerSec, float phase = 0f)
        {
            float a = (t * degPerSec + phase) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }

        /// <summary>
        /// Does a player's capsule (feet at <paramref name="player"/>, radius r) cut a line through
        /// <paramref name="center"/> along <paramref name="dir"/>? <paramref name="twoSided"/> = the line extends both ways.
        /// </summary>
        public static bool Crosses(Vector3 center, Vector3 dir, float halfLength, bool twoSided, Vector3 player, float radius = 0.35f)
        {
            var d = new Vector3(player.x - center.x, 0, player.z - center.z);
            float along = Vector3.Dot(d, dir);
            if (along > halfLength || (twoSided ? along < -halfLength : along < 0f)) return false;
            var perp = d - dir * along;
            return perp.magnitude <= radius;
        }

        /// <summary>Feet above the beam (jumping over a knee-high line).</summary>
        public static bool Above(float feetY, float beamY) => feetY > beamY + 0.05f;
    }
}
