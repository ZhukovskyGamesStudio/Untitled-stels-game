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

        [Tooltip("The lid on its hinge: it swings open while someone hides inside.")]
        public Transform Lid;

        float m_Open;

        void Update()
        {
            if (Lid == null) return;
            bool someone = false;
            foreach (var p in Stealth.Player.PlayerController.All) someone |= p.InHatch;
            m_Open = Mathf.MoveTowards(m_Open, someone ? 1f : 0f, Time.deltaTime * 2.5f);
            Lid.localRotation = Quaternion.Euler(0f, 0f, -Mathf.SmoothStep(0f, 105f, m_Open));
        }

        /// <summary>Feet this far above or below the hatch are on another floor (a floor is 3.5 m).</summary>
        public const float SameFloor = 1.5f;

        public static bool IsNear(Vector3 position)
        {
            var layout = ActiveLevel.Layout;
            return layout != null && IsNear(position, layout.HatchPosition);
        }

        /// <summary>Close to the hatch and on its floor — not right above it on the floor upstairs (pure).</summary>
        public static bool IsNear(Vector3 position, Vector3 hatch)
        {
            var d = position - hatch;
            if (Mathf.Abs(d.y) > SameFloor) return false;
            d.y = 0;
            return d.magnitude <= UseDistance;
        }
    }
}
