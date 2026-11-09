using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Office sliding door. Opens when a player or a robot is close, closes behind them. A closed door blocks
    /// vision (robots, cameras) but is ignored by the navmesh, so robots path through and open it on arrival.
    /// Every peer runs it locally from replicated positions; the server's state is what sensors use.
    /// </summary>
    public class AutoDoor : MonoBehaviour
    {
        public const float OpenRadius = 2.3f;
        public const float SlideTime = 0.35f;

        public static readonly List<AutoDoor> All = new();

        public Transform PanelA, PanelB;
        public BoxCollider Blocker;
        public float Width = LevelMetrics.DoorWidth;

        float m_Open; // 0 closed .. 1 open
        public bool IsOpen => m_Open > 0.5f;
        public float OpenAmount => m_Open;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Pure rule: open if any agent stands within the radius on the same floor.</summary>
        public static bool ShouldOpen(Vector3 door, IEnumerable<Vector3> agents, float radius = OpenRadius)
        {
            foreach (var a in agents)
            {
                if (Mathf.Abs(a.y - door.y) > 1.6f) continue;
                var d = a - door;
                d.y = 0;
                if (d.sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        static readonly List<Vector3> s_Agents = new();

        void Update()
        {
            s_Agents.Clear();
            foreach (var p in PlayerController.All) s_Agents.Add(p.transform.position);
            foreach (var r in RobotController.Robots) s_Agents.Add(r.transform.position);
            bool open = ShouldOpen(transform.position, s_Agents);
            m_Open = Mathf.MoveTowards(m_Open, open ? 1f : 0f, Time.deltaTime / SlideTime);
            float slide = Width / 4f * m_Open;
            PanelA.localPosition = new Vector3(-Width / 4f - slide, PanelA.localPosition.y, 0);
            PanelB.localPosition = new Vector3(Width / 4f + slide, PanelB.localPosition.y, 0);
            // Solid only while (almost) closed: never trap anyone in the panels.
            Blocker.enabled = m_Open < 0.15f;
        }
    }
}
