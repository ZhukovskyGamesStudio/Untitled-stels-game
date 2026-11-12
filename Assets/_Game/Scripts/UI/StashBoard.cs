using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The team stash shown in the world above the hatch (prefab UI/StashBoard, a world-space canvas): a grid of the
    /// shared stash and a "sell" area next to it. A dumb view: <see cref="InventoryUI"/> places it, draws the items and
    /// handles the drag-and-drop onto it.
    /// </summary>
    public class StashBoard : MonoBehaviour
    {
        public static StashBoard Instance { get; private set; }

        /// <summary>Height of the board's centre above the hatch, metres.</summary>
        public const float Height = 1.6f;

        public Canvas Canvas;
        public RectTransform Root;
        [Tooltip("Cells of the stash grid (pivot top-left, InventoryUI.Cell units per cell).")]
        public RectTransform Grid;
        public Image[] Cells;
        public RectTransform ItemLayer;
        public RectTransform HighlightLayer;
        public RectTransform SellZone;
        public Image SellBackground;
        public Text ValueLabel;
        [Tooltip("The \"put everything in\" button under the grid (clicked with the cursor out, or its key).")]
        public RectTransform StashAllZone;
        public Image StashAllBackground;
        public Text StashAllLabel;

        public bool Visible => Canvas.enabled;

        void Awake()
        {
            Instance = this;
            Canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(bool on)
        {
            if (Canvas.enabled != on) Canvas.enabled = on;
        }

        /// <summary>Hangs above the hatch, turned towards the viewer (yaw only).</summary>
        public void Place(Vector3 hatch, Vector3 viewer)
        {
            var pos = hatch + Vector3.up * Height;
            var dir = pos - viewer;
            dir.y = 0f;
            Root.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward));
        }

        /// <summary>Metres in front of the eyes while sitting in the hatch.</summary>
        public const float HatchDistance = 1.9f;

        /// <summary>
        /// From inside the hatch the eyes are at the floor and the board above the hatch is right overhead: it floats
        /// in front of the view instead, a little above the floor, tilted to face the eyes.
        /// </summary>
        public void PlaceInFront(Transform eyes)
        {
            var flat = eyes.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = eyes.up;
            flat.y = 0f;
            flat.Normalize();
            var pos = eyes.position + flat * HatchDistance + Vector3.up * 0.75f; // above the backpack at the bottom of the screen
            Root.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - eyes.position));
        }
    }
}
