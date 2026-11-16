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
    /// A key door (<see cref="LevelLayout.KeyDoors"/>) cannot be hacked: it opens with a key block (<see cref="KeyLock"/>),
    /// its lamp is amber. The storage room's door (<see cref="LevelLayout.StorageDoor"/>) is locked unless: the door terminal
    /// unlocked it (for good); a worker is at the door or in the room (it lets them through — a player may slip in behind
    /// one — and locks again once no worker is left inside); a player inside held E at it (<see cref="IsEscaping"/>: until
    /// no player is in the room or at the door any more).
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
        /// <summary>The storage door held open from inside for now (set by the game: index in the layout's door list).</summary>
        public static System.Func<int, bool> IsEscaping = _ => false;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color LockedColor = new(1f, 0.12f, 0.08f), OpenColor = new(0.2f, 1f, 0.35f), KeyColor = new(1f, 0.62f, 0.05f);

        /// <summary>Index in <see cref="LevelLayout.AutoDoors"/> (-1: an outer door).</summary>
        public int Index { get; private set; } = -1;
        public bool HasLock { get; private set; }
        /// <summary>Opens only with a key block (not hacked).</summary>
        public bool NeedsKey { get; private set; }
        /// <summary>The vault's door: opened by its three lock panels (not hacked, no key).</summary>
        public bool Vault { get; private set; }
        /// <summary>The storage room's door: opened from the door terminal, or from inside.</summary>
        public bool Storage { get; private set; }
        /// <summary>The storage room's door: the way into the room (flat, unit).</summary>
        public Vector3 Inward { get; private set; }
        /// <summary>The storage room's door: the room's bounds on the plan (x, z).</summary>
        public Rect Room { get; private set; }

        /// <summary>The storage room's door in this house, or null.</summary>
        public static AutoDoor StorageDoor
        {
            get
            {
                foreach (var d in All) if (d.Storage) return d;
                return null;
            }
        }

        /// <summary>A point on the room's side of the storage door (pure).</summary>
        public static bool IsInside(Vector3 door, Vector3 inward, Vector3 p)
        {
            var to = p - door;
            to.y = 0f;
            return Vector3.Dot(to, inward) > 0.15f;
        }
        /// <summary>A point in the storage room behind this door (pure): its side of the door, within the room's bounds, its floor.</summary>
        public static bool InRoom(Vector3 door, Vector3 inward, Rect room, Vector3 p) =>
            Mathf.Abs(p.y - door.y) <= 1.6f && IsInside(door, inward, p) && room.Contains(new Vector2(p.x, p.z));

        /// <summary>A point at the door (pure): close, on its floor.</summary>
        public static bool AtDoor(Vector3 door, Vector3 p, float radius = OpenRadius)
        {
            var d = p - door;
            d.y = 0f;
            return Mathf.Abs(p.y - door.y) <= 1.6f && d.sqrMagnitude <= radius * radius;
        }

        /// <summary>The storage door's rule (pure): locked unless its terminal unlocked it, a player opened it from inside,
        /// or a worker is at the door or in the room.</summary>
        public static bool StorageLocked(bool terminal, bool escaping, bool worker) => !terminal && !escaping && !worker;

        /// <summary>The door terminal unlocked it for good (a lock hacked open).</summary>
        public bool TerminalOpen => HasLock && IsUnlocked(Index);

        /// <summary>The storage door: a working worker (one that moves loot, not hacked) at the door or in the room.</summary>
        public bool WorkerThere
        {
            get
            {
                if (!Storage) return false;
                var door = transform.position;
                foreach (var r in RobotController.Robots)
                {
                    if (r == null || r.IsHacked || !r.Traits.MovesLoot) continue;
                    var p = r.transform.position;
                    if (AtDoor(door, p) || InRoom(door, Inward, Room, p)) return true;
                }
                return false;
            }
        }

        /// <summary>Locked: players cannot pass (a lock not hacked yet; the storage door by <see cref="StorageLocked"/>).</summary>
        public bool Locked => HasLock && (Storage ? StorageLocked(IsUnlocked(Index), IsEscaping(Index), WorkerThere) : !IsUnlocked(Index));
        MaterialPropertyBlock m_Block;
        int m_ShownLock = -1;

        public void Configure(int index, bool locked, bool keyed = false, bool vault = false, bool storage = false, Vector3 inward = default, Rect room = default)
        {
            Room = room;
            Index = index;
            HasLock = locked || keyed || vault || storage;
            NeedsKey = keyed || vault; // neither can be hacked
            Vault = vault;
            Storage = storage;
            Inward = inward;
            m_ShownLock = -1;
        }

        /// <summary>The door a player can hack from here (locked, close, same floor), or null.</summary>
        public static AutoDoor LockedNear(Vector3 player, float reach = HackReach, Aim? aim = null)
        {
            AutoDoor best = null;
            float bestD = reach;
            foreach (var d in All)
            {
                if (!d.Locked || d.NeedsKey || Mathf.Abs(player.y - d.transform.position.y) > 1.6f) continue;
                if (d.Storage && !IsInside(d.transform.position, d.Inward, player)) continue; // from outside: the door terminal
                var p = d.transform.position;
                if (aim.HasValue && !aim.Value.At(p + Vector3.up * 0.2f, p + Vector3.up * 2.1f, 0.6f)) continue; // looked at
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
                Audio.Sfx.At(Audio.Sfx.Bank?.DoorSlide, transform.position + Vector3.up * 1.2f, Configs.Tuning.Audio.Mix.Doors, Audio.Sfx.Group.Items, open ? 1f : 0.92f);
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
            var c = locked ? (NeedsKey ? KeyColor : LockedColor) : OpenColor;
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
