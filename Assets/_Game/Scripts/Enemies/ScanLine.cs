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

        /// <summary>Rays per half of a laser fan (<see cref="FanRay"/>).</summary>
        public const int FanRays = 28;

        /// <summary>
        /// Ray <paramref name="i"/> of <paramref name="count"/> of half a laser fan, in the fan's plane (x out along the scan,
        /// y up, the apex at the origin): uncut, the rays meet the floor <paramref name="span"/> below the apex evenly from
        /// right under it to <paramref name="reach"/> out. The unit direction; <paramref name="length"/> = to the floor.
        /// </summary>
        public static Vector2 FanRay(int i, int count, float reach, float span, out float length)
        {
            var to = new Vector2(reach * i / Mathf.Max(1, count - 1), -Mathf.Max(0.01f, span));
            length = to.magnitude;
            return to / length;
        }

        /// <summary>
        /// Does a player's capsule touch a vertical laser fan whose halves are outlined by where their rays stopped
        /// (<paramref name="halfA"/> along +<paramref name="dir"/>, <paramref name="halfB"/> along −dir; points in the fan's
        /// plane from the apex, x out, y up — see <see cref="FanRay"/>)? Each half is the polygon apex → its points: walls,
        /// furniture and the floor cut it where the light stops, so nothing behind them is lit or caught. The light is
        /// widest low down (no jumping over it).
        /// </summary>
        public static bool FanTouches(Vector3 apex, Vector3 dir, Vector2[] halfA, Vector2[] halfB, Vector3 feet, float height, float radius = 0.35f)
        {
            var d = new Vector3(feet.x - apex.x, 0f, feet.z - apex.z);
            float along = Vector3.Dot(d, dir);
            if ((d - dir * along).magnitude > radius) return false;
            var half = along >= 0f ? halfA : halfB;
            if (half == null || half.Length < 2) return false;
            // the body's side nearest the apex, from the feet to the head: the fan narrows upwards and stops at what it hits
            float x = Mathf.Max(0f, Mathf.Abs(along) - radius);
            float low = feet.y - apex.y, high = low + height;
            for (float y = low + 0.05f; y <= high + 0.001f; y += 0.15f)
                if (Inside(half, new Vector2(x, Mathf.Min(y, high)))) return true;
            return false;
        }

        /// <summary>The point lies in the polygon apex (origin) → <paramref name="outline"/> (even-odd rule).</summary>
        static bool Inside(Vector2[] outline, Vector2 p)
        {
            bool inside = false;
            int n = outline.Length + 1;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var a = i == 0 ? Vector2.zero : outline[i - 1];
                var b = j == 0 ? Vector2.zero : outline[j - 1];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Feet above the beam (jumping over a knee-high line).</summary>
        public static bool Above(float feetY, float beamY) => feetY > beamY + 0.05f;
    }
}
