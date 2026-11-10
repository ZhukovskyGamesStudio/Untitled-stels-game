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
    /// A locked door (<see cref="LevelLayout.LockedDoors"/>) opens only for robots until a player hacks its lock
    /// (hold E; the server keeps the list of unlocked doors, <see cref="IsUnlocked"/>). Its lamp is red, then green.
    /// </summary>
    public class AutoDoor : MonoBehaviour
    {
        public const float OpenRadius = 2.3f;
        public const float SlideTime = 0.35f;

        public static readonly List<AutoDoor> All = new();

        public Transform PanelA, PanelB;
        public BoxCollider Blocker;
        public float Width = LevelMetrics.DoorWidth;
        [Tooltip("Lock lamps on both sides above the door (shown only on locked doors).")]
        public Renderer[] LockLamps;

        /// <summary>Hold E this close to the door to hack its lock.</summary>
        public const float HackReach = 1.9f;

        /// <summary>Which doors are unlocked now (set by the game: index in the layout's door list).</summary>
        public static System.Func<int, bool> IsUnlocked = _ => false;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color LockedColor = new(1f, 0.12f, 0.08f), OpenColor = new(0.2f, 1f, 0.35f);

        /// <summary>Index in <see cref="LevelLayout.AutoDoors"/> (-1: an outer door).</summary>
        public int Index { get; private set; } = -1;
        public bool HasLock { get; private set; }
        /// <summary>Locked and not hacked yet: players cannot pass.</summary>
        public bool Locked => HasLock && !IsUnlocked(Index);
        MaterialPropertyBlock m_Block;
        int m_ShownLock = -1;

        public void Configure(int index, bool locked)
        {
            Index = index;
            HasLock = locked;
            m_ShownLock = -1;
        }

        /// <summary>The door a player can hack from here (locked, close, same floor), or null.</summary>
        public static AutoDoor LockedNear(Vector3 player, float reach = HackReach)
        {
            AutoDoor best = null;
            float bestD = reach;
            foreach (var d in All)
            {
                if (!d.Locked || Mathf.Abs(player.y - d.transform.position.y) > 1.6f) continue;
                var to = player - d.transform.position;
                to.y = 0;
                if (to.magnitude <= bestD) { best = d; bestD = to.magnitude; }
            }
            return best;
        }

        float m_Open; // 0 closed .. 1 open
        bool m_Opening;
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
            bool locked = Locked;
            if (!locked) foreach (var p in PlayerController.All) s_Agents.Add(p.transform.position); // a locked door ignores players
            foreach (var r in RobotController.Robots) s_Agents.Add(r.transform.position);
            ShowLock(locked);
            bool open = ShouldOpen(transform.position, s_Agents);
            if (open != m_Opening)
            {
                m_Opening = open;
                Audio.Sfx.At(Audio.Sfx.Bank?.DoorSlide, transform.position + Vector3.up * 1.2f, 0.7f, Audio.Sfx.Group.Items, open ? 1f : 0.92f);
            }
            m_Open = Mathf.MoveTowards(m_Open, open ? 1f : 0f, Time.deltaTime / SlideTime);
            float slide = Width / 4f * m_Open;
            PanelA.localPosition = new Vector3(-Width / 4f - slide, PanelA.localPosition.y, 0);
            PanelB.localPosition = new Vector3(Width / 4f + slide, PanelB.localPosition.y, 0);
            // Solid only while (almost) closed: never trap anyone in the panels.
            Blocker.enabled = m_Open < 0.15f;
        }

        void ShowLock(bool locked)
        {
            int state = !HasLock ? 0 : locked ? 1 : 2;
            if (state == m_ShownLock || LockLamps == null) return;
            m_ShownLock = state;
            m_Block ??= new MaterialPropertyBlock();
            var c = locked ? LockedColor : OpenColor;
            foreach (var r in LockLamps)
            {
                if (r == null) continue;
                r.enabled = HasLock;
                r.GetPropertyBlock(m_Block);
                m_Block.SetColor(BaseColorId, c);
                m_Block.SetColor(EmissionId, c * 2.5f);
                r.SetPropertyBlock(m_Block);
            }
        }
    }
}
