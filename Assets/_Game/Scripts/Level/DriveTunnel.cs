using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The endless tunnel of the drive to the next house (prefab Level/Sewer/DriveTunnel; <see cref="DrivePath"/>): whole
    /// kit segments along its +X, segment k at x = 6k, the pattern's variant and a lamp on alternate walls — the same in
    /// the sewer (after a branch's reducer) and in the basement's approach, so the car can change from one to the other
    /// unseen. Only the stretch round the car is there (<see cref="Follow"/>: ahead and behind past what the haze lets
    /// anyone see), between <see cref="From"/> and <see cref="To"/> (segment numbers). Scenery: no colliders.
    /// </summary>
    public class DriveTunnel : MonoBehaviour
    {
        [Tooltip("The pattern's two segments (whole kit segments).")]
        public GameObject[] Segments;
        [Tooltip("A wall lamp (the sewer's).")]
        public GameObject Lamp;

        /// <summary>Metres of tunnel kept ahead of and behind the car (past the haze).</summary>
        public const float Ahead = 66f, Behind = 60f;
        /// <summary>The lamps' colour (the basement's warm light).</summary>
        public static readonly Color LampColor = new(1f, 0.8f, 0.55f);
        /// <summary>…and their brightness (dim: the tunnel between them dark).</summary>
        public const float LampIntensity = 2.4f;

        /// <summary>The segments it may have: k from <see cref="From"/> to <see cref="To"/>.</summary>
        public int From = int.MinValue / 2, To = int.MaxValue / 2;
        /// <summary>Shown only while the tunnel is empty (the basement's cap at the end of its short back stretch).</summary>
        [System.NonSerialized] public GameObject HideWhileFollowing;

        readonly Dictionary<int, GameObject> m_Cells = new();

        /// <summary>Keep the tunnel round local x (the car's, in this tunnel's frame).</summary>
        public void Follow(float x)
        {
            int lo = Mathf.Max(From, Mathf.FloorToInt((x - Behind) / SewerPlan.SegmentLength));
            int hi = Mathf.Min(To, Mathf.FloorToInt((x + Ahead) / SewerPlan.SegmentLength));
            var gone = new List<int>();
            foreach (var k in m_Cells.Keys) if (k < lo - 2 || k > hi + 2) gone.Add(k);
            foreach (int k in gone) { Destroy(m_Cells[k]); m_Cells.Remove(k); }
            for (int k = lo; k <= hi; k++)
                if (!m_Cells.ContainsKey(k)) m_Cells[k] = Cell(k);
            if (HideWhileFollowing != null && HideWhileFollowing.activeSelf) HideWhileFollowing.SetActive(false);
        }

        /// <summary>Everything gone (the car has arrived): what it hid is back.</summary>
        public void Clear()
        {
            foreach (var go in m_Cells.Values) Destroy(go);
            m_Cells.Clear();
            if (HideWhileFollowing != null) HideWhileFollowing.SetActive(true);
        }

        public int Count => m_Cells.Count;

        GameObject Cell(int k)
        {
            float x = k * SewerPlan.SegmentLength;
            var cell = Instantiate(Segments[DrivePath.VariantOf(k) % Segments.Length], transform);
            cell.name = $"Tunnel{k}";
            cell.transform.SetLocalPositionAndRotation(new Vector3(x, 0f, 0f), Quaternion.identity);
            foreach (var c in cell.GetComponentsInChildren<Collider>()) c.enabled = false;
            if (Lamp != null)
            {
                int side = DrivePath.LampSideOf(k);
                var lamp = Instantiate(Lamp, cell.transform);
                lamp.transform.SetLocalPositionAndRotation(new Vector3(SewerPlan.SegmentLength / 2f, 2.3f, side * 3.55f), Quaternion.Euler(0f, side > 0 ? 90f : -90f, 0f));
                foreach (var light in lamp.GetComponentsInChildren<Light>()) { light.color = LampColor; light.intensity = LampIntensity; }
            }
            VisualStyles.Dress(cell);
            return cell;
        }

        // the level (or the sewer) it belongs to is going: what it hid goes with it — showing it again here was
        // "GameObjects can not be made active when they are being destroyed"
        void OnDestroy()
        {
            foreach (var go in m_Cells.Values) if (go != null) Destroy(go);
            m_Cells.Clear();
        }
    }
}
