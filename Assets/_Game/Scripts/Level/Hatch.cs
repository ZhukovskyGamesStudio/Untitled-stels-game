using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The sewer hatch the team climbed in through. Items dragged onto it are handed in for the quota;
    /// standing next to it a player can finish the level.
    /// </summary>
    public class Hatch : MonoBehaviour
    {
        public const float UseDistance = 3.2f;

        public static bool IsNear(Vector3 position)
        {
            var layout = LevelGenerator.Current;
            if (layout == null) return false;
            var d = position - layout.HatchPosition;
            d.y = 0;
            return d.magnitude <= UseDistance;
        }
    }
}
