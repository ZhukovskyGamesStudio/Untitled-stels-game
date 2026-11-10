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
    }
}
