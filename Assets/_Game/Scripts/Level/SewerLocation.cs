using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The sewer between two houses (prefab Level/Sewer): a tunnel with a ladder up to the hatch the team came down, the
    /// team's stash board where the rest of the loot is sold and the quota paid, and the tunnels to the next houses.
    /// Every peer builds it locally when the run enters the sewer; nothing in it is networked. If the team goes on without
    /// the quota, the water rises (<see cref="Flood"/>).
    /// </summary>
    public class SewerLocation : MonoBehaviour
    {
        public static SewerLocation Active { get; private set; }

        [Tooltip("Where the players stand when they come down (one per slot).")]
        public Transform[] Spawns;
        [Tooltip("The stash board and the sell area hang here.")]
        public Transform TradeSpot;
        [Tooltip("The water surface: it rises when the run ends here.")]
        public Transform Water;
        [Tooltip("Tunnel mouths to the next houses (decor; the route is chosen on the map).")]
        public Transform[] Tunnels;

        float m_WaterBase;
        float m_FloodStart = -1f;

        void Awake()
        {
            Active = this;
            if (Water != null) m_WaterBase = Water.localPosition.y;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        public Vector3 SpawnFor(int slot) =>
            Spawns != null && Spawns.Length > 0 ? Spawns[Mathf.Abs(slot) % Spawns.Length].position : transform.position + Vector3.up;

        /// <summary>The run ends here: the water rises over the next seconds.</summary>
        public void Flood()
        {
            if (m_FloodStart < 0f) m_FloodStart = Time.time;
        }

        void Update()
        {
            if (Water == null || m_FloodStart < 0f) return;
            float t = Mathf.Clamp01((Time.time - m_FloodStart) / 6f);
            var p = Water.localPosition;
            Water.localPosition = new Vector3(p.x, m_WaterBase + Mathf.SmoothStep(0f, 2.2f, t), p.z);
        }
    }
}
