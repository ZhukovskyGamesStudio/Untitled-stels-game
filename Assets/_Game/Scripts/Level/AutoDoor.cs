using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a door sounds like: a pneumatic slide, a latch and a creak, a heavy steel one.</summary>
    public enum DoorSound : byte { Slide, Swing, Heavy }

    /// <summary>
    /// A door in a doorway: sliding leaves (<see cref="PanelA"/>/<see cref="PanelB"/>) or leaves on hinges (<see cref="Swing"/>,
    /// <see cref="SwingB"/>; <see cref="DoorRules"/> decides the kind, the way it opens and the hinges' jamb). Opens when a player or a robot is close, closes behind them.
    /// A door-and-a-half opens its narrow leaf only for robots (<see cref="SecondForRobots"/>: they need the width); a both-ways
    /// door swings away from whoever comes (<see cref="BothWays"/>). A closed door blocks
    /// vision (robots, cameras) but is ignored by the navmesh, so robots path through and open it on arrival.
    /// Every peer runs it locally from replicated positions; the server's state is what sensors use.
    /// A locked door (<see cref="LevelLayout.LockedDoors"/>) opens only for robots until a player hacks its lock
    /// (hold E; the server keeps the list of unlocked doors, <see cref="World"/>). Its lamp is red, then green.
    /// A key door (<see cref="LevelLayout.KeyDoors"/>) cannot be hacked: it opens with a key block (<see cref="KeyLock"/>),
    /// its lamp is amber. The storage room's door (<see cref="LevelLayout.StorageDoor"/>) is locked unless: the door terminal
    /// unlocked it (for good); a worker is at the door or in the room (it lets them through — a player may slip in behind
    /// one — and locks again once no worker is left inside); a player inside held E at it (<see cref="World"/>: until
    /// no player is in the room or at the door any more).
    /// </summary>
    public class AutoDoor : MonoBehaviour, IReachTarget
    {
        public const float OpenRadius = 2.3f;
        public const float SlideTime = 0.35f;
        /// <summary>Seconds the vault's door takes to swing open.</summary>
        public const float VaultSwingTime = 2.6f;
        /// <summary>Seconds the vault door's wheel spins (drawing the bolts) before the door moves, and how many turns.</summary>
        public const float VaultSpinTime = 1.8f, VaultWheelTurns = 1.5f;

        public static readonly List<AutoDoor> All = new();

        public Transform PanelA, PanelB;
        [Tooltip("A door on a hinge instead of sliding panels (the vault's round door, the street doors): turns by SwingAngle when open.")]
        public Transform Swing;
        [Tooltip("The second leaf of a double swing door: turns the other way (by −SwingAngle).")]
        public Transform SwingB;
        [Tooltip("A door-and-a-half: the narrow second leaf opens only for robots (players pass the main leaf; robots need the whole width).")]
        public bool SecondForRobots;
        [Tooltip("The leaves swing either way, away from whoever comes (the hospital's corridor doors); hinged in the wall's middle.")]
        public bool BothWays;
        [Tooltip("The second leaf's own blocker (a door-and-a-half: it opens apart from the main leaf), or empty.")]
        public BoxCollider BlockerB;
        [Tooltip("What the door sounds like when it opens and shuts.")]
        public DoorSound Sound = DoorSound.Slide;
        [Tooltip("A flat's entry door: its number on the plate on the landing's face (set by the level builder), or empty.")]
        public UnityEngine.UI.Text Number;
        public float SwingAngle = 90f;
        [Tooltip("Seconds a swing door takes to open (the vault's: VaultSwingTime).")]
        public float SwingTime = VaultSwingTime;
        [Tooltip("The vault door's wheel (pivot on the disc's axis, local Z): spins before the door swings.")]
        public Transform Wheel;
        [Tooltip("Padlocks on both faces, hung on the panels' seam: shown while a hackable lock holds the door.")]
        public Transform[] Padlocks;
        [Tooltip("Parts that are the wall itself (the corners round the vault's round door): dressed in the style's wall finish by StyleDecor.")]
        public MeshRenderer[] WallParts;
        public BoxCollider Blocker;
        public float Width = LevelMetrics.DoorWidth;
        [Tooltip("Lock lamps on both sides above the door (shown only on locked doors).")]
        public Renderer[] LockLamps;

        /// <summary>Hold E this close to the door to hack its lock.</summary>
        public const float HackReach = 1.9f;

        /// <summary>The vault's door a player has opened (set by the game): unlocking alone does not move it.</summary>

        /// <summary>
        /// The vault door's motion (pure): <paramref name="spin"/> 0..1 of the wheel's turning, then <paramref name="swing"/>
        /// 0..1 of the door's — the door waits for the wheel, and closes (never, in play) only after it has.
        /// </summary>
        public static void VaultStep(bool opened, float dt, ref float spin, ref float swing)
        {
            if (opened)
            {
                spin = Mathf.MoveTowards(spin, 1f, dt / VaultSpinTime);
                if (spin >= 1f) swing = Mathf.MoveTowards(swing, 1f, dt / VaultSwingTime);
            }
            else
            {
                swing = Mathf.MoveTowards(swing, 0f, dt / VaultSwingTime);
                if (swing <= 0f) spin = Mathf.MoveTowards(spin, 0f, dt / VaultSpinTime);
            }
        }

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
        public bool TerminalOpen => HasLock && World.IsOpened(OpenKeys.Door(Index));

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
        public bool Locked => HasLock && (Storage ? StorageLocked(World.IsOpened(OpenKeys.Door(Index)), World.IsOpened(OpenKeys.Escape(Index)), WorkerThere) : !World.IsOpened(OpenKeys.Door(Index)));
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
            => ReachSearch.Nearest(All, player, player, reach, aim);

        // from outside the storage room's door: its terminal
        bool IReachTarget.CanReach(Vector3 player) => Locked && !NeedsKey && (!Storage || IsInside(transform.position, Inward, player));
        (Vector3, Vector3, float) IReachTarget.AimSpan => (transform.position + Vector3.up * 0.2f, transform.position + Vector3.up * 2.1f, 0.6f);
        Vector3 IReachTarget.ReachPoint => transform.position + Vector3.up * ReachSearch.Middle;
        float IReachTarget.MaxReach => float.PositiveInfinity;
        Transform IReachTarget.ReachRoot => transform;
        Vector3? IReachTarget.HandsTo => null; // the door is the wall

        OpenTween m_Open; // 0 closed .. 1 open
        OpenTween m_OpenB; // a door-and-a-half's narrow leaf
        float m_Spin; // the vault's wheel: 0 .. 1 turned
        float m_Way = 1f; // a both-ways door: +1 opens to +Z, −1 to −Z
        bool m_Mirrored;
        public bool IsOpen => m_Open.Value > 0.5f;
        /// <summary>The vault's door a player has opened (unlocking alone does not move it).</summary>
        public bool SwungOpen => Vault && World.IsOpened(OpenKeys.Stage(JobStage.VaultOpen));
        public float OpenAmount => m_Open.Value;

        /// <summary>
        /// Hangs the leaves on the other jamb (<see cref="DoorSpec.HingeRight"/>): the hinges, the leaves under them, the
        /// blockers and the padlocks mirror along X. The builder calls it once, right after placing the door.
        /// </summary>
        public void HingeRight()
        {
            if (m_Mirrored) return;
            m_Mirrored = true;
            foreach (var t in new[] { Swing, SwingB })
            {
                if (t == null) continue;
                var p = t.localPosition;
                t.localPosition = new Vector3(-p.x, p.y, p.z);
                var s = t.localScale;
                t.localScale = new Vector3(-s.x, s.y, s.z);
            }
            foreach (var b in new[] { Blocker, BlockerB })
                if (b != null) b.center = new Vector3(-b.center.x, b.center.y, b.center.z);
            if (Padlocks != null)
                foreach (var p in Padlocks)
                    if (p != null) p.localPosition = new Vector3(-p.localPosition.x, p.localPosition.y, p.localPosition.z);
            if (Number != null)
            {
                // the number rides on the mirrored leaf: mirrored back, it reads the right way round
                var s = Number.transform.localScale;
                Number.transform.localScale = new Vector3(-s.x, s.y, s.z);
            }
        }

        /// <summary>
        /// Turns the hinged leaves: <paramref name="main"/> and <paramref name="second"/> 0 shut .. 1 open, a both-ways door to
        /// <paramref name="way"/> (+1: +Z). A leaf hung on the other jamb (mirrored) turns the other way to open to the same side.
        /// </summary>
        public void ShowSwing(float main, float second, float way = 1f)
        {
            float sign = (m_Mirrored ? -1f : 1f) * (BothWays ? way : 1f);
            Swing.localRotation = Quaternion.Euler(0f, sign * SwingAngle * Mathf.SmoothStep(0f, 1f, main), 0f);
            if (SwingB != null) SwingB.localRotation = Quaternion.Euler(0f, -sign * SwingAngle * Mathf.SmoothStep(0f, 1f, second), 0f);
        }

        /// <summary>Which way a both-ways door swings (pure): away from the agent that opens it (+1: to +Z).</summary>
        public static float AwayFrom(Vector3 door, Vector3 forward, Vector3 agent) => Vector3.Dot(agent - door, forward) > 0f ? -1f : 1f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Pure rule: open if any agent stands within the radius on the same floor.</summary>
        public static bool ShouldOpen(Vector3 door, IEnumerable<Vector3> agents, float radius = OpenRadius)
        {
            foreach (var a in agents)
                if (Near(door, a, radius)) return true;
            return false;
        }

        static bool Near(Vector3 door, Vector3 a, float radius = OpenRadius)
        {
            if (Mathf.Abs(a.y - door.y) > 1.6f) return false;
            var d = a - door;
            d.y = 0;
            return d.sqrMagnitude <= radius * radius;
        }

        static readonly List<Vector3> s_Agents = new();

        void Update()
        {
            s_Agents.Clear();
            bool locked = Locked;
            if (!locked) foreach (var p in PlayerController.All) s_Agents.Add(p.transform.position); // a locked door ignores players
            int players = s_Agents.Count;
            foreach (var r in RobotController.Robots) s_Agents.Add(r.transform.position);
            ShowLock(locked);
            var here = transform.position;
            bool open = ShouldOpen(here, s_Agents);
            bool openB = open;
            if (SecondForRobots)
            {
                // the narrow leaf: for a robot near (players fit through the main leaf)
                openB = false;
                for (int k = players; k < s_Agents.Count && !openB; k++) openB = Near(here, s_Agents[k]);
            }
            if (BothWays && open && m_Open.Value < 0.02f && s_Agents.Count > 0)
            {
                // from shut: away from the nearest agent
                var nearest = s_Agents[0];
                foreach (var a in s_Agents) if ((a - here).sqrMagnitude < (nearest - here).sqrMagnitude) nearest = a;
                m_Way = AwayFrom(here, transform.forward, nearest);
            }
            if (Swing != null && Vault) open = !locked && SwungOpen; // unlocked by the panels, opened by a player
            float dt = Time.deltaTime, was = m_Open.Value;
            OpenMove move;
            if (Swing != null && Vault)
            {
                // the vault door: heavy and slow; its wheel turns first, then it swings in and stays open
                move = m_Open.Step(open, 0f, 1f); // the word only (a house built with it open: snapped open, wheel turned)
                if (m_Open.Value >= 1f) m_Spin = 1f;
                VaultStep(open, dt, ref m_Spin, ref m_Open.Value);
            }
            else move = m_Open.Step(open, dt, Swing != null ? Mathf.Max(0.05f, SwingTime) : SlideTime);
            // a slide sounds as it starts either way; a hinged door's latch as it meets the frame
            if (move == OpenMove.StartedOpening || (move == OpenMove.StartedShutting && Sound == DoorSound.Slide)) DoorSounds.Play(Sound, transform.position + Vector3.up * 1.2f, move == OpenMove.StartedOpening);
            else if (Sound != DoorSound.Slide && was > 0f && m_Open.Value <= 0f) DoorSounds.Play(Sound, transform.position + Vector3.up * 1.2f, false);
            if (Swing != null)
            {
                m_OpenB.Step(openB, dt, Mathf.Max(0.05f, SwingTime));
                if (Wheel != null) Wheel.localRotation = Quaternion.Euler(0f, 0f, -VaultWheelTurns * 360f * Mathf.SmoothStep(0f, 1f, m_Spin));
                ShowSwing(m_Open.Value, SecondForRobots ? m_OpenB.Value : m_Open.Value, m_Way);
                Blocker.enabled = m_Open.Value < 0.15f;
                if (BlockerB != null) BlockerB.enabled = m_OpenB.Value < 0.15f;
                return;
            }
            float slide = Width / 4f * m_Open.Value;
            PanelA.localPosition = new Vector3(-Width / 4f - slide, PanelA.localPosition.y, 0);
            PanelB.localPosition = new Vector3(Width / 4f + slide, PanelB.localPosition.y, 0);
            if (Padlocks != null)
                foreach (var p in Padlocks)
                    if (p != null) p.localPosition = new Vector3(-slide, p.localPosition.y, p.localPosition.z); // rides on panel A's edge
            // Solid only while (almost) closed: never trap anyone in the panels.
            Blocker.enabled = m_Open.Value < 0.15f;
        }

        void ShowLock(bool locked)
        {
            int state = !HasLock ? 0 : locked ? 1 : 2;
            if (state == m_ShownLock) return;
            m_ShownLock = state;
            if (Padlocks != null)
            {
                bool padlock = locked && !NeedsKey; // a key door has its key panels, the vault its lamps
                foreach (var p in Padlocks)
                    if (p != null && p.gameObject.activeSelf != padlock) p.gameObject.SetActive(padlock);
            }
            if (LockLamps == null) return;
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
