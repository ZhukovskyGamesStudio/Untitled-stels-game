using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Robots share sightings over the radio (pure rules, unit tested). The visual is <see cref="RadioRipple"/>.</summary>
    public static class RobotRadio
    {
        public const float Range = 15f;

        /// <summary>Indices of robots that hear a call from <paramref name="sender"/>: same floor, within range, not the sender.</summary>
        public static List<int> Recipients(Vector3 sender, IReadOnlyList<Vector3> robots, float range = Range)
        {
            var result = new List<int>();
            for (int i = 0; i < robots.Count; i++)
            {
                var d = robots[i] - sender;
                if (d.sqrMagnitude < 0.01f) continue;
                // Same storey: hovering drones are ~2 m up, the next floor is 3.5 m up.
                if (Mathf.Abs(d.y) > 2.6f) continue;
                d.y = 0;
                if (d.magnitude <= range) result.Add(i);
            }
            return result;
        }
    }
}
