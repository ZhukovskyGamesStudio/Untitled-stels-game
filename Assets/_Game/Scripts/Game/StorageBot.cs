using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Game
{
    /// <summary>
    /// The team's storage robot (prefab Vehicles/StorageBot, a network prefab; its twin StorageBotNet replicates
    /// <see cref="Mode"/>, a server NetworkTransform its place): a hermit crab in a crate carrying the team's stash
    /// (<see cref="GameManager.Stash"/>: the board shows and the fuel burns next to it). It lives in the ducts
    /// (<see cref="StorageBotRules"/>): called, it crawls to the grate nearest the player and comes down if the room is safe,
    /// else waits up there (the stash passed up through the grate); sent to the exit it crawls to the exit room and leaves
    /// by the exit hatch with its load; on an alarm it hides in the basement. A robot or a camera that sees it in a room
    /// raises the alarm. Its way is a list of legs (walk, climb up, crawl, climb down, leave) run one after another.
    /// </summary>
    public class StorageBot : MonoBehaviour
    {
        /// <summary>The house's storage robot, or null.</summary>
        public static StorageBot Active { get; private set; }

        public BotMode Mode { get; private set; }

        NavMeshAgent m_Agent;
        readonly List<BotLeg> m_Legs = new();
        int m_RouteAt;
        Vector3 m_ClimbFrom;
        float m_LegUntil, m_Seen, m_NextLook, m_AlarmCooldown;
        bool m_InDuct;
        Vector2Int m_Cell;
        int m_DuctFloor;
        BotMode m_After;

        void Awake() => m_Agent = GetComponent<NavMeshAgent>();
        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        /// <summary>Peers: the server's mode (its place comes with the transform).</summary>
        public void ApplyReplicated(BotMode mode) => Mode = mode;

        /// <summary>Up in the ducts right now (no robot can see it there).</summary>
        public bool InDuct => m_InDuct;

        /// <summary>Server: start where the team arrives, waiting.</summary>
        public void BeginServer()
        {
            if (m_Agent != null)
            {
                m_Agent.enabled = true;
                if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) m_Agent.Warp(hit.position);
            }
            Mode = BotMode.Waiting;
        }

        // ------------------------------------------------------------------ commands (server)

        public enum Answer { Coming, NoWay, Busy }

        /// <summary>Server: a player called it: through the ducts to the grate nearest them, down if the room is safe.</summary>
        public Answer CallServer(Vector3 caller, LevelLayout l)
        {
            if (!StorageBotRules.CanCall(Mode) || l == null) return Answer.Busy;
            // on its feet with a way there on the floor: it simply walks over (the ducts only when there is no other way)
            if (WalksTo(caller))
            {
                Begin(new List<BotLeg> { new() { Kind = BotLegKind.Walk, To = caller, Room = -2 } }, BotMode.Coming, BotMode.Waiting);
                return Answer.Coming;
            }
            int floor = LevelLayout.FloorOf(caller.y + 0.1f);
            if (floor >= l.Vents.Count) return Answer.NoWay;
            var net = l.Vents[floor];
            var target = StorageBotRules.NearestGrate(net, caller);
            if (target is not DuctGrate to) return Answer.NoWay;
            var legs = new List<BotLeg>();
            if (!ToDucts(legs, l, floor, out var cell)) return Answer.NoWay;
            var crawl = StorageBotRules.Crawl(net, cell, to.Cell);
            if (crawl == null) return Answer.NoWay;
            legs.Add(new BotLeg { Kind = BotLegKind.Crawl, Route = crawl });
            legs.Add(new BotLeg { Kind = BotLegKind.ClimbDown, To = to.Position, CheckSafe = true, Room = to.Room });
            legs.Add(new BotLeg { Kind = BotLegKind.Walk, To = caller, Room = -2 }); // −2: stop next to them
            Begin(legs, BotMode.Coming, BotMode.Waiting);
            return Answer.Coming;
        }

        /// <summary>Server: sent to the exit — through the ducts to the exit room, down, into the exit hatch.</summary>
        public Answer SendToExitServer(LevelLayout l)
        {
            if (!StorageBotRules.CanCall(Mode) || l == null) return Answer.Busy;
            if (WalksTo(l.ExitHatchPosition))
            {
                Begin(new List<BotLeg> { new() { Kind = BotLegKind.LeaveByHatch, To = l.ExitHatchPosition } }, BotMode.ToGrate, BotMode.Gone);
                return Answer.Coming;
            }
            if (l.Vents.Count == 0) return Answer.Busy;
            var net = l.Vents[0];
            int exitRoom = l.ExitRoom;
            int i = net.Grates.FindIndex(g => g.Room == exitRoom);
            if (i < 0) return Answer.NoWay;
            var to = net.Grates[i];
            var legs = new List<BotLeg>();
            if (!ToDucts(legs, l, 0, out var cell)) return Answer.NoWay;
            var crawl = StorageBotRules.Crawl(net, cell, to.Cell);
            if (crawl == null) return Answer.NoWay;
            legs.Add(new BotLeg { Kind = BotLegKind.Crawl, Route = crawl });
            legs.Add(new BotLeg { Kind = BotLegKind.ClimbDown, To = to.Position, Room = to.Room });
            legs.Add(new BotLeg { Kind = BotLegKind.LeaveByHatch, To = l.ExitHatchPosition });
            Begin(legs, BotMode.ToGrate, BotMode.Gone);
            return Answer.Coming;
        }

        /// <summary>Server, an alarm: back to the basement it came from (through the ducts, down by the lobby, the shaft).</summary>
        public void HideServer(LevelLayout l)
        {
            if (l == null || Mode is BotMode.Gone or BotMode.ToExit or BotMode.ToGrate || InBasement(l)) return;
            if (Mode == BotMode.Hiding) return;
            var net = l.Vents.Count > 0 ? l.Vents[0] : null;
            var legs = new List<BotLeg>();
            if (net != null && StorageBotRules.NearestGrate(net, l.HatchPosition) is DuctGrate home && ToDucts(legs, l, 0, out var cell))
            {
                var crawl = StorageBotRules.Crawl(net, cell, home.Cell);
                if (crawl != null)
                {
                    legs.Add(new BotLeg { Kind = BotLegKind.Crawl, Route = crawl });
                    legs.Add(new BotLeg { Kind = BotLegKind.ClimbDown, To = home.Position, Room = home.Room });
                }
                else legs.Clear();
            }
            legs.Add(new BotLeg { Kind = BotLegKind.Walk, To = Basement.StashSpot(l.Cellar), Room = -1 });
            GameManager.Instance?.OpenShaftServer(); // it pushes the lid open on its way down
            Begin(legs, BotMode.Hiding, BotMode.Waiting);
        }

        /// <summary>
        /// The legs that take it into the ducts of <paramref name="floor"/> (none when it is there already): from the
        /// basement up the shaft to the ground floor's grate nearest the lobby, from a room to its storey's nearest grate.
        /// </summary>
        bool ToDucts(List<BotLeg> legs, LevelLayout l, int floor, out Vector2Int cell)
        {
            cell = default;
            if (m_InDuct)
            {
                cell = m_Cell;
                return m_DuctFloor == floor;
            }
            int here = InBasement(l) ? 0 : LevelLayout.FloorOf(transform.position.y + 0.1f);
            if (here != floor || floor >= l.Vents.Count) return false;
            var near = InBasement(l) ? l.HatchPosition : transform.position;
            if (InBasement(l)) GameManager.Instance?.OpenShaftServer(); // up the shaft: it pushes the lid open
            if (StorageBotRules.NearestGrate(l.Vents[floor], near) is not DuctGrate up) return false;
            legs.Add(new BotLeg { Kind = BotLegKind.Walk, To = new Vector3(up.Position.x, LevelLayout.FloorY(floor), up.Position.z), Room = -1 });
            legs.Add(new BotLeg { Kind = BotLegKind.ClimbUp, To = up.Position, Room = up.Room });
            cell = up.Cell;
            return true;
        }

        bool InBasement(LevelLayout l) => Basement.Contains(l.Cellar, transform.position);

        static NavMeshPath s_Path; // made on first use: Unity forbids it in a static initializer

        /// <summary>Out of the ducts, on the navmesh, with a whole walkable way to <paramref name="to"/> that is not too long.</summary>
        bool WalksTo(Vector3 to)
        {
            if (m_InDuct || m_Agent == null) return false;
            OnFloor();
            if (!m_Agent.isOnNavMesh || !NavMesh.SamplePosition(to, out var hit, 1.5f, NavMesh.AllAreas)) return false;
            s_Path ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, s_Path) || s_Path.status != NavMeshPathStatus.PathComplete) return false;
            float length = 0f;
            var corners = s_Path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length <= StorageBotRules.WalkFurthest;
        }

        void Begin(List<BotLeg> legs, BotMode mode, BotMode after)
        {
            m_Legs.Clear();
            m_Legs.AddRange(legs);
            m_After = after;
            Mode = mode;
            StartLeg();
        }

        // ------------------------------------------------------------------ running the legs (server)

        void StartLeg()
        {
            if (m_Legs.Count == 0) { Finish(); return; }
            var leg = m_Legs[0];
            m_RouteAt = 0;
            m_ClimbFrom = transform.position;
            m_LegUntil = Time.time + 60f;
            switch (leg.Kind)
            {
                case BotLegKind.Walk:
                case BotLegKind.LeaveByHatch:
                    OnFloor();
                    if (!m_Agent.isOnNavMesh) { m_LegUntil = Time.time; break; } // nowhere to walk from: the leg ends at once
                    m_Agent.isStopped = false;
                    var to = leg.To;
                    if (NavMesh.SamplePosition(to, out var hit, 1.5f, NavMesh.AllAreas)) to = hit.position;
                    m_Agent.SetDestination(to);
                    break;
                case BotLegKind.ClimbUp:
                case BotLegKind.ClimbDown:
                case BotLegKind.Crawl:
                    if (m_Agent.enabled) m_Agent.enabled = false;
                    break;
            }
        }

        void NextLeg()
        {
            if (m_Legs.Count > 0) m_Legs.RemoveAt(0);
            StartLeg();
        }

        void Finish()
        {
            if (m_After == BotMode.Gone)
            {
                Mode = BotMode.Gone;
                GameManager.Instance?.StorageBotLeftServer(this);
                return;
            }
            Mode = m_InDuct ? BotMode.WaitingAbove : m_After;
            if (m_Agent.enabled) { m_Agent.isStopped = true; m_Agent.ResetPath(); }
        }

        void OnFloor()
        {
            if (!m_Agent.enabled) m_Agent.enabled = true;
            if (m_Agent.isOnNavMesh) return;
            // off the navmesh (out of a duct, set down by the network): onto the nearest walkable spot, or SetDestination throws
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) m_Agent.Warp(hit.position);
            else m_Agent.Warp(transform.position);
        }

        void Update()
        {
            if (!Session.IsServer || m_Agent == null) return;
            WatchedServer();
            if (m_Legs.Count == 0) return;
            var leg = m_Legs[0];
            float step = StorageBotRules.DuctSpeed * Time.deltaTime;
            switch (leg.Kind)
            {
                case BotLegKind.Walk:
                case BotLegKind.LeaveByHatch:
                {
                    float within = leg.Room == -2 ? StorageBotRules.Arrive : leg.Kind == BotLegKind.LeaveByHatch ? 1.0f : 0.6f;
                    var d = leg.To - transform.position;
                    d.y = 0f;
                    bool there = !m_Agent.pathPending && (d.magnitude <= within || (!m_Agent.hasPath && m_Agent.remainingDistance <= within));
                    if (there || Time.time > m_LegUntil)
                    {
                        m_Agent.isStopped = true;
                        if (leg.Kind == BotLegKind.LeaveByHatch) { m_Legs.Clear(); m_After = BotMode.Gone; Finish(); }
                        else NextLeg();
                    }
                    break;
                }
                case BotLegKind.ClimbUp:
                {
                    var top = new Vector3(leg.To.x, StorageBotRules.DuctFloor(LevelLayout.FloorOf(leg.To.y - 1f)), leg.To.z);
                    var at = Vector3.MoveTowards(transform.position, new Vector3(top.x, transform.position.y, top.z), step);
                    at = Vector3.MoveTowards(at, top, step);
                    transform.position = at;
                    if ((at - top).sqrMagnitude > 1e-4f) break;
                    m_InDuct = true;
                    m_DuctFloor = LevelLayout.FloorOf(leg.To.y - 1f);
                    m_Cell = VentPlan.CellOf(leg.To);
                    NextLeg();
                    break;
                }
                case BotLegKind.Crawl:
                {
                    var at = StorageBotRules.Advance(leg.Route, ref m_RouteAt, transform.position, step);
                    Face(at - transform.position);
                    transform.position = at;
                    if (m_RouteAt < leg.Route.Count) break;
                    m_Cell = VentPlan.CellOf(at);
                    NextLeg();
                    break;
                }
                case BotLegKind.ClimbDown:
                {
                    if (leg.CheckSafe && m_RouteAt == 0)
                    {
                        if (!RoomSafe(leg.Room))
                        {
                            // a watcher in the room under it: it stays up in the duct and says so (the stash passed up through the grate)
                            m_Legs.Clear();
                            Mode = BotMode.WaitingAbove;
                            GameManager.Instance?.StorageBotWaitsServer();
                            break;
                        }
                        m_RouteAt = 1;
                    }
                    var floor = new Vector3(leg.To.x, LevelLayout.FloorY(LevelLayout.FloorOf(leg.To.y - 1f)), leg.To.z);
                    if (NavMesh.SamplePosition(floor, out var hit, 1.5f, NavMesh.AllAreas)) floor = new Vector3(leg.To.x, hit.position.y, leg.To.z);
                    var at = Vector3.MoveTowards(transform.position, floor, step);
                    transform.position = at;
                    if ((at - floor).sqrMagnitude > 1e-4f) break;
                    m_InDuct = false;
                    if (NavMesh.SamplePosition(at, out var land, 1.5f, NavMesh.AllAreas)) transform.position = land.position;
                    NextLeg();
                    break;
                }
            }
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
        }

        // ------------------------------------------------------------------ watched (server)

        /// <summary>No awake detector in the room (a robot, a camera, a sensor) and no alarm.</summary>
        bool RoomSafe(int room)
        {
            var l = ActiveLevel.Layout;
            if (l == null) return true;
            var watchers = new List<int>();
            foreach (var d in Detector.All)
            {
                if (d == null || d.IsHacked) continue;
                var p = d.transform.position;
                var r = l.Plan.RoomAt(new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y + 0.1f)) + 0.3f, p.z));
                if (r != null) watchers.Add(r.Id);
            }
            return StorageBotRules.Safe(room, watchers, GameManager.Instance != null && GameManager.Instance.AlarmActive);
        }

        /// <summary>Out in a room (not in a duct, not in the basement): a robot with eyes or a camera that sees it raises the alarm.</summary>
        void WatchedServer()
        {
            var l = ActiveLevel.Layout;
            if (Time.time < m_NextLook || l == null) return;
            m_NextLook = Time.time + 0.25f;
            if (m_InDuct || Mode == BotMode.Gone || InBasement(l)) { m_Seen = 0f; return; }
            var body = transform.position + Vector3.up * 0.35f;
            bool seen = false;
            foreach (var d in Detector.All)
            {
                if (d == null || d.IsHacked) continue;
                bool eyes = d is RobotController rc ? rc.Senses.HasEyes : d is SecurityCamera;
                if (!eyes) continue;
                var eye = d.Position;
                bool clear = !Physics.Linecast(eye, body, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (StorageBotRules.Sees(eye, d.transform.forward, body, clear)) { seen = true; break; }
            }
            m_Seen = seen ? m_Seen + 0.25f : Mathf.Max(0f, m_Seen - 0.25f);
            if (m_Seen < StorageBotRules.SeenFor || Time.time < m_AlarmCooldown) return;
            m_Seen = 0f;
            m_AlarmCooldown = Time.time + 10f;
            GameManager.Instance?.RaiseAlarmServer(transform.position, 30f);
        }
    }
}
