using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A hatch of the house (two of them): the entry the team comes up through from the car (its stash holds what came up
    /// with them) and the exit the team leaves by (its stash goes down with them). Hiding in the exit's hatch (everyone)
    /// takes the team down into the sewer.
    /// </summary>
    public class Hatch : MonoBehaviour
    {
        public const float UseDistance = 1.6f; // standing at its rim, not across the room

        [Tooltip("The lid on its hinge: it swings open while someone hides inside.")]
        public Transform Lid;

        float m_Open;

        void Update()
        {
            if (Lid == null) return;
            bool someone = false;
            var layout = ActiveLevel.Layout;
            int mine = layout != null && (transform.position - layout.ExitHatchPosition).sqrMagnitude < 1f ? 1 : 0;
            foreach (var p in Stealth.Player.PlayerController.All) someone |= p.InHatch && p.HatchIndex == mine;
            m_Open = Mathf.MoveTowards(m_Open, someone ? 1f : 0f, Time.deltaTime * 2.5f);
            Lid.localRotation = Quaternion.Euler(0f, 0f, -Mathf.SmoothStep(0f, 105f, m_Open));
        }

        /// <summary>Feet this far above or below the hatch are on another floor (a floor is 3.5 m).</summary>
        public const float SameFloor = 1.5f;

        public static bool IsNear(Vector3 position) => IndexNear(position) >= 0;

        /// <summary>0 near the entry hatch, 1 near the exit, −1 near neither.</summary>
        public static int IndexNear(Vector3 position)
        {
            var layout = ActiveLevel.Layout;
            if (layout == null) return -1;
            if (IsNear(position, layout.ExitHatchPosition)) return 1;
            return IsNear(position, layout.HatchPosition) ? 0 : -1;
        }

        public static Vector3 PositionOf(int hatch)
        {
            var layout = ActiveLevel.Layout;
            return layout == null ? Vector3.zero : hatch == 1 ? layout.ExitHatchPosition : layout.HatchPosition;
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
