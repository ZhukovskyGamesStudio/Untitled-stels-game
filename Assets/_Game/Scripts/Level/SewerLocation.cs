using System;
using Stealth.Game;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The sewer between two houses (prefab Level/Sewer): a vaulted brick tunnel from the ladder under the hatch, the team's
    /// cart with the stash, a hall with a cistern and three tunnels branching off to the next houses (<see cref="Branches"/>,
    /// left, middle, right). The ways on are chosen by walking into a tunnel; tunnels that lead nowhere this time are
    /// barred. Every peer builds it locally; nothing in it is networked. If the team goes on without the quota, the water
    /// rises (<see cref="Flood"/>).
    /// </summary>
    public class SewerLocation : MonoBehaviour
    {
        public static SewerLocation Active { get; private set; }

        [Serializable]
        public class Branch
        {
            [Tooltip("The mouth of the tunnel: +X leads into it.")]
            public Transform Mouth;
            [Tooltip("Bars that close the tunnel when it leads nowhere.")]
            public GameObject Grate;
            [Tooltip("The panel over the mouth: where it leads.")]
            public Stealth.UI.RouteSignView Sign;
            [Tooltip("A lamp inside, lit only when the tunnel is open.")]
            public GameObject Lamp;
        }

        [Tooltip("Where the players stand when they come down (one per slot).")]
        public Transform[] Spawns;
        [Tooltip("The cart: the stash board hangs over it, its bed shows the stash.")]
        public Transform TradeSpot;
        [Tooltip("The flood water: it rises when the run ends here.")]
        public Transform Water;
        [Tooltip("Left, middle, right.")]
        public Branch[] Branches = new Branch[SewerBranches.Count];

        /// <summary>A player inside a tunnel this far past its mouth (and up to this deep) votes for it.</summary>
        public const float VoteFrom = 0.6f, VoteTo = 11.5f, VoteHalfWidth = 1.4f;

        float m_WaterBase;
        float m_FloodStart = -1f;
        int m_Ways = -1;

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

        public Quaternion SpawnRotation(int slot) =>
            Spawns != null && Spawns.Length > 0 ? Spawns[Mathf.Abs(slot) % Spawns.Length].rotation : transform.rotation;

        /// <summary>The tunnel (0 left, 1 middle, 2 right) a point is in, or −1.</summary>
        public int BranchAt(Vector3 p)
        {
            for (int i = 0; i < Branches.Length; i++)
            {
                var m = Branches[i].Mouth;
                if (m == null) continue;
                var local = m.InverseTransformPoint(p);
                if (local.x >= VoteFrom && local.x <= VoteTo && Mathf.Abs(local.z) <= VoteHalfWidth && Mathf.Abs(local.y) < 2.5f) return i;
            }
            return -1;
        }

        /// <summary>Open as many tunnels as there are ways on (1–3); the others get their bars.</summary>
        public void ShowWays(int ways)
        {
            if (ways == m_Ways) return;
            m_Ways = ways;
            for (int i = 0; i < Branches.Length; i++)
            {
                bool open = SewerBranches.WayOf(i, ways) >= 0;
                var b = Branches[i];
                if (b.Grate != null) b.Grate.SetActive(!open);
                if (b.Sign != null) b.Sign.gameObject.SetActive(open);
                if (b.Lamp != null) b.Lamp.SetActive(open);
            }
        }

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
            Water.localPosition = new Vector3(p.x, m_WaterBase + Mathf.SmoothStep(0f, 2.4f, t), p.z);
        }
    }
}
