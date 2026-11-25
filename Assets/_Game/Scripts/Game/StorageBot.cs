using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Vehicles;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Game
{
    /// <summary>
    /// The team's storage mule (prefab Vehicles/StorageBot, a network prefab; its twin StorageBotNet replicates
    /// <see cref="Mode"/> and <see cref="Motion"/>, a server NetworkTransform its place): a raccoon with a backpack carrying the team's stash
    /// (<see cref="GameManager.Stash"/>: the board shows next to it). Until called it sleeps curled up in the car's trunk;
    /// called, it hops out and runs to the player on the floor — through the ducts only when they are clearly the shorter
    /// way (<see cref="StorageBotRules.Way"/>), and only through grates whose lids are off: up into the open grate nearest
    /// it, along the duct, down through the open grate nearest the player if the room is safe, else it waits up there (the
    /// stash passed up through the grate); sent to the exit it leaves by the exit hatch with its load; on an alarm it runs
    /// back and curls up in the trunk again. A robot or a camera that sees it in a room
    /// raises the alarm. Its way is a list of legs (walk, climb up, crawl, climb down, leave) run one after another: up into
    /// a grate it leaps after a crouch, out of one it drops after hanging (<see cref="StorageBotRules.LeapLift"/>,
    /// <see cref="StorageBotRules.DropLower"/>), the basement shaft's ladder it climbs on the navmesh link. A player can pet
    /// it (<see cref="StorageBotPet"/>): every peer sees the reaction.
    /// </summary>
    public class StorageBot : MonoBehaviour
    {
        /// <summary>The house's storage robot, or null.</summary>
        public static StorageBot Active { get; private set; }

        public BotMode Mode { get; private set; }
        /// <summary>What its body is doing (the take every peer plays).</summary>
        public BotMotion Motion { get; private set; }
        /// <summary>Every peer: bumped on each pat (the animator plays the reaction).</summary>
        public int Pets { get; private set; }

        NavMeshAgent m_Agent;
        readonly List<BotLeg> m_Legs = new();
        int m_RouteAt;
        Vector3 m_ClimbFrom;
        float m_LegUntil, m_Seen, m_NextLook, m_AlarmCooldown, m_LeapAt = -1f, m_PettedUntil;
        IStorageBotLink m_Link;
        bool m_InDuct, m_InTrunk, m_Called, m_Sitting, m_OnRungs;
        Vector3[] m_LadderWay;
        int m_LadderAt;
        Vector3 m_LadderFrom, m_LadderFacing;
        Vector2Int m_Cell;
        int m_DuctFloor;
        BotMode m_After;

        void Awake() => m_Agent = GetComponent<NavMeshAgent>();
        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        /// <summary>Peers: the server's mode and motion (its place comes with the transform).</summary>
        public void ApplyReplicated(BotMode mode, BotMotion motion)
        {
            Mode = mode;
            Motion = motion;
        }

        /// <summary>The network twin, once spawned (null offline: the pat is shown here at once).</summary>
        public void Bind(IStorageBotLink link) => m_Link = link;

        /// <summary>Server: a player pats it (<see cref="StorageBotPet"/>, the reach checked by the use): every peer plays the reaction; it stands still meanwhile.</summary>
        public void PetServer()
        {
            if (!StorageBotRules.CanPet(Mode, Motion)) return;
            m_PettedUntil = Time.time + StorageBotRules.PetTime;
            if (m_Agent != null && m_Agent.enabled && m_Agent.isOnNavMesh) m_Agent.isStopped = true;
            if (m_Link != null) m_Link.BroadcastPet();
            else ReceivePet();
        }

        /// <summary>Every peer: it was petted.</summary>
        public void ReceivePet() => Pets++;

        /// <summary>Up in the ducts right now (no robot can see it there).</summary>
        public bool InDuct => m_InDuct;

        /// <summary>Asleep in the car's trunk (until it is called).</summary>
        public bool InTrunk => m_InTrunk;

        /// <summary>Server: start where the team arrives, waiting — curled up in the car's trunk, or on the basement floor when there is no car.</summary>
        public void BeginServer(bool inTrunk)
        {
            m_InTrunk = inTrunk;
            if (m_Agent != null && !inTrunk)
            {
                m_Agent.enabled = true;
                if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas)) m_Agent.Warp(hit.position);
            }
            Mode = BotMode.Waiting;
            Motion = inTrunk ? BotMotion.Curled : BotMotion.Floor;
            m_Called = m_Sitting = false;
        }

        /// <summary>Where it sleeps in the car's trunk (by the tailgate, on the bed's floor), or null without a car.</summary>
        public static Vector3? TrunkSpot()
        {
            var car = Car.Active;
            return car != null && car.Bed != null ? car.Bed.TransformPoint(new Vector3(0.45f, 0.02f, 0.4f)) : null;
        }

        /// <summary>The floor behind the tailgate it hops down to (and up from), on the navmesh, or null.</summary>
        static Vector3? Tailgate()
        {
            var car = Car.Active;
            if (car == null || car.Bed == null) return null;
            var p = car.Bed.position - car.Bed.forward * StorageBotRules.TailgateStep;
            return NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas) ? hit.position : null;
        }

        /// <summary>Where its way starts: on the floor behind the tailgate when it sleeps in the trunk (it hops out first).</summary>
        Vector3 Here => m_InTrunk && Tailgate() is Vector3 t ? t : transform.position;

        // ------------------------------------------------------------------ commands (server)

        public enum Answer { Coming, NoWay, Busy }

        /// <summary>Server: a player called it: through the ducts to the grate nearest them, down if the room is safe.</summary>
        public Answer CallServer(Vector3 caller, LevelLayout l)
        {
            if (!StorageBotRules.CanCall(Mode) || l == null) return Answer.Busy;
            // sat right by the caller already: it stays sat (no standing up for a step)
            var near = caller - transform.position;
            if (m_Sitting && m_Legs.Count == 0 && new Vector2(near.x, near.z).magnitude <= StorageBotRules.Arrive && Mathf.Abs(near.y) < 1f) return Answer.Coming;
            // on foot whenever the floor leads there and is not much longer than the ducts
            var ducts = DuctsToCaller(caller, l);
            switch (StorageBotRules.Way(StorageBotRules.SameRoom(l, Here, caller), FloorWay(caller), ducts != null ? StorageBotRules.Length(Here, ducts) : -1f))
            {
                case StorageBotRules.Route.Walk:
                    Begin(new List<BotLeg> { new() { Kind = BotLegKind.Walk, To = caller, Room = -2 } }, BotMode.Coming, BotMode.Waiting);
                    return Answer.Coming;
                case StorageBotRules.Route.Ducts:
                    StartClimb(l);
                    Begin(ducts, BotMode.Coming, BotMode.Waiting);
                    return Answer.Coming;
                default:
                    return Answer.NoWay;
            }
        }

        /// <summary>The legs through the ducts to the grate nearest a caller, down (if the room is safe) and over to them; null: no such way.</summary>
        List<BotLeg> DuctsToCaller(Vector3 caller, LevelLayout l)
        {
            int floor = LevelLayout.FloorOf(caller.y + 0.1f);
            if (floor < 0 || floor >= l.Vents.Count) return null;
            var net = l.Vents[floor];
            if (StorageBotRules.NearestGrate(net, caller, i => GrateOpen(l, floor, i)) is not DuctGrate to) return null;
            var legs = new List<BotLeg>();
            if (!ToDucts(legs, l, floor, out var cell)) return null;
            var crawl = StorageBotRules.Crawl(net, cell, to.Cell);
            if (crawl == null) return null;
            legs.Add(new BotLeg { Kind = BotLegKind.Crawl, Route = crawl });
            legs.Add(new BotLeg { Kind = BotLegKind.ClimbDown, To = to.Position, CheckSafe = true, Room = to.Room });
            legs.Add(new BotLeg { Kind = BotLegKind.Walk, To = caller, Room = -2 }); // −2: stop next to them
            return legs;
        }

        /// <summary>The legs through the ducts to the exit room's grate, down and into the exit hatch; null: no such way.</summary>
        List<BotLeg> DuctsToExit(LevelLayout l)
        {
            if (l.Vents.Count == 0) return null;
            var net = l.Vents[0];
            int exitRoom = l.ExitRoom;
            int i = net.Grates.FindIndex(g => g.Room == exitRoom);
            if (i < 0 || !GrateOpen(l, 0, i)) return null;
            var to = net.Grates[i];
            var legs = new List<BotLeg>();
            if (!ToDucts(legs, l, 0, out var cell)) return null;
            var crawl = StorageBotRules.Crawl(net, cell, to.Cell);
            if (crawl == null) return null;
            legs.Add(new BotLeg { Kind = BotLegKind.Crawl, Route = crawl });
            legs.Add(new BotLeg { Kind = BotLegKind.ClimbDown, To = to.Position, Room = to.Room });
            legs.Add(new BotLeg { Kind = BotLegKind.LeaveByHatch, To = l.ExitHatchPosition });
            return legs;
        }

        /// <summary>Setting off for the ducts from the basement: it pushes the shaft's lid open on its way up.</summary>
        void StartClimb(LevelLayout l)
        {
            if (!m_InDuct && InBasement(l)) GameManager.Instance?.OpenShaftServer();
        }

        /// <summary>A grate of a storey it can get through: never shut, or its lid taken off with a screwdriver (for everyone).</summary>
        static bool GrateOpen(LevelLayout l, int floor, int i)
        {
            var g = l.Vents[floor].Grates[i];
            return !g.Closed || World.IsOpened(OpenKeys.Vent(StorageBotRules.GrateIndex(l.Vents, floor, i)));
        }

        /// <summary>Server: sent to the exit — through the ducts to the exit room, down, into the exit hatch.</summary>
        public Answer SendToExitServer(LevelLayout l)
        {
            if (!StorageBotRules.CanCall(Mode) || l == null) return Answer.Busy;
            // on foot whenever the floor leads there and is not much longer than the ducts
            var ducts = DuctsToExit(l);
            switch (StorageBotRules.Way(StorageBotRules.SameRoom(l, Here, l.ExitHatchPosition), FloorWay(l.ExitHatchPosition),
                        ducts != null ? StorageBotRules.Length(Here, ducts) : -1f))
            {
                case StorageBotRules.Route.Walk:
                    Begin(new List<BotLeg> { new() { Kind = BotLegKind.LeaveByHatch, To = l.ExitHatchPosition } }, BotMode.ToGrate, BotMode.Gone);
                    return Answer.Coming;
                case StorageBotRules.Route.Ducts:
                    StartClimb(l);
                    Begin(ducts, BotMode.ToGrate, BotMode.Gone);
                    return Answer.Coming;
                default:
                    return Answer.NoWay;
            }
        }

        /// <summary>
        /// Server, an alarm: back to the car's trunk to curl up in (the basement floor without a car) — on foot when the
        /// floor is the shorter way, else through the ducts and down by the lobby, then the shaft.
        /// </summary>
        public void HideServer(LevelLayout l)
        {
            if (l == null || m_InTrunk || Mode is BotMode.Gone or BotMode.ToExit or BotMode.ToGrate or BotMode.Hiding) return;
            var tailgate = Tailgate();
            var home = tailgate ?? Basement.StashSpot(l.Cellar);
            if (tailgate == null && InBasement(l)) return;
            var legs = new List<BotLeg>();
            var net = l.Vents.Count > 0 ? l.Vents[0] : null;
            if (net != null && StorageBotRules.NearestGrate(net, l.HatchPosition, i => GrateOpen(l, 0, i)) is DuctGrate grate && ToDucts(legs, l, 0, out var cell))
            {
                var crawl = StorageBotRules.Crawl(net, cell, grate.Cell);
                if (crawl != null)
                {
                    legs.Add(new BotLeg { Kind = BotLegKind.Crawl, Route = crawl });
                    legs.Add(new BotLeg { Kind = BotLegKind.ClimbDown, To = grate.Position, Room = grate.Room });
                }
                else legs.Clear();
            }
            legs.Add(new BotLeg { Kind = BotLegKind.Walk, To = home, Room = -1 });
            float floor = FloorWay(home);
            if (floor >= 0f && (m_InDuct ? false : floor <= StorageBotRules.Length(Here, legs) * StorageBotRules.DuctDetour))
            {
                legs.Clear();
                legs.Add(new BotLeg { Kind = BotLegKind.Walk, To = home, Room = -1 });
            }
            if (tailgate != null && TrunkSpot() is Vector3 trunk) legs.Add(new BotLeg { Kind = BotLegKind.HopIn, To = trunk });
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
            int here = InBasement(l) ? 0 : LevelLayout.FloorOf(Here.y + 0.1f);
            if (here != floor || floor >= l.Vents.Count) return false;
            var near = InBasement(l) ? l.HatchPosition : Here; // (from the basement: up the shaft first)
            if (StorageBotRules.NearestGrate(l.Vents[floor], near, i => GrateOpen(l, floor, i)) is not DuctGrate up) return false;
            legs.Add(new BotLeg { Kind = BotLegKind.Walk, To = new Vector3(up.Position.x, LevelLayout.FloorY(floor), up.Position.z), Room = -1 });
            legs.Add(new BotLeg { Kind = BotLegKind.ClimbUp, To = up.Position, Room = up.Room });
            cell = up.Cell;
            return true;
        }

        bool InBasement(LevelLayout l) => Basement.Contains(l.Cellar, Here);

        static NavMeshPath s_Path; // made on first use: Unity forbids it in a static initializer

        /// <summary>The length of the whole walkable way to <paramref name="to"/>, metres; −1: up in the ducts, off the navmesh or no way.</summary>
        float FloorWay(Vector3 to)
        {
            if (m_InDuct) return -1f;
            if (!NavMesh.SamplePosition(Here, out var from, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var hit, 1.5f, NavMesh.AllAreas)) return -1f;
            s_Path ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, hit.position, NavMesh.AllAreas, s_Path) || s_Path.status != NavMeshPathStatus.PathComplete) return -1f;
            float length = 0f;
            var corners = s_Path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        void Begin(List<BotLeg> legs, BotMode mode, BotMode after)
        {
            m_Legs.Clear();
            // asleep in the trunk: it hops down behind the tailgate first
            if (m_InTrunk && Tailgate() is Vector3 tailgate) m_Legs.Add(new BotLeg { Kind = BotLegKind.HopOut, To = tailgate });
            m_Legs.AddRange(legs);
            m_After = after;
            Mode = mode;
            m_Called = mode == BotMode.Coming; // (a call: it sits down where it comes to)
            m_Sitting = false;
            StartLeg();
        }

        // ------------------------------------------------------------------ running the legs (server)

        void StartLeg()
        {
            if (m_Legs.Count == 0) { Finish(); return; }
            var leg = m_Legs[0];
            m_RouteAt = 0;
            m_LeapAt = -1f;
            m_ClimbFrom = transform.position;
            m_LegUntil = Time.time + 60f;
            m_LadderWay = null;
            m_OnRungs = false;
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
                case BotLegKind.HopOut:
                case BotLegKind.HopIn:
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
            m_Sitting = StorageBotRules.SitsDown(m_Called, !m_InDuct && !m_InTrunk, m_Legs.Count > 0); // come to the caller: it sits and waits
            m_Called = false;
            if (m_Agent.enabled && m_Agent.isOnNavMesh) { m_Agent.isStopped = true; m_Agent.ResetPath(); }
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
            Motion = MotionNow();
            if (m_Legs.Count == 0)
            {
                // asleep in the trunk: it rides along wherever the car goes
                if (m_InTrunk && TrunkSpot() is Vector3 trunk && Car.Active != null && Car.Active.Bed != null)
                    transform.SetPositionAndRotation(trunk, Quaternion.LookRotation(Vector3.ProjectOnPlane(-Car.Active.Bed.forward, Vector3.up).normalized + Vector3.forward * 1e-4f));
                return;
            }
            var leg = m_Legs[0];
            bool walking = leg.Kind is BotLegKind.Walk or BotLegKind.LeaveByHatch;
            if (walking && Time.time < m_PettedUntil)
            {
                // being petted: it stays (a call meanwhile waits until the reaction is over)
                if (m_Agent.enabled && m_Agent.isOnNavMesh) m_Agent.isStopped = true;
                m_LegUntil += Time.deltaTime;
                return;
            }
            if (walking && m_PettedUntil > 0f)
            {
                m_PettedUntil = 0f;
                if (m_Agent.enabled && m_Agent.isOnNavMesh) m_Agent.isStopped = false;
            }
            float step = StorageBotRules.DuctSpeed * Time.deltaTime;
            switch (leg.Kind)
            {
                case BotLegKind.Walk:
                case BotLegKind.LeaveByHatch:
                {
                    if (m_Agent.isOnOffMeshLink) { ClimbLadder(); break; }
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
                    // under the grate first, then a crouch and a leap up into the duct
                    var top = new Vector3(leg.To.x, StorageBotRules.DuctFloor(LevelLayout.FloorOf(leg.To.y - 1f)), leg.To.z);
                    if (m_LeapAt < 0f)
                    {
                        var under = new Vector3(top.x, transform.position.y, top.z);
                        Face(under - transform.position);
                        transform.position = Vector3.MoveTowards(transform.position, under, step);
                        if ((transform.position - under).sqrMagnitude > 1e-4f) break;
                        m_LeapAt = Time.time;
                        m_ClimbFrom = transform.position;
                    }
                    float lift = StorageBotRules.LeapLift(Time.time - m_LeapAt);
                    transform.position = new Vector3(top.x, Mathf.Lerp(m_ClimbFrom.y, top.y, lift), top.z);
                    if (lift < 1f) break;
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
                case BotLegKind.HopOut:
                case BotLegKind.HopIn:
                {
                    // down from the trunk to the floor behind the tailgate (a drop after a moment), or up into it (a leap after a crouch)
                    bool up = leg.Kind == BotLegKind.HopIn;
                    if (m_LeapAt < 0f)
                    {
                        m_LeapAt = Time.time;
                        m_ClimbFrom = transform.position;
                        Face(up ? leg.To - transform.position : transform.position - leg.To);
                    }
                    float t = Time.time - m_LeapAt;
                    float share = up ? StorageBotRules.LeapLift(t) : StorageBotRules.DropLower(t);
                    float along = up ? Mathf.Clamp01((t - StorageBotRules.LeapCrouch) / StorageBotRules.LeapTime) : Mathf.Clamp01((t - StorageBotRules.DropHang) / StorageBotRules.DropTime);
                    var flat = Vector3.Lerp(m_ClimbFrom, leg.To, along);
                    transform.position = new Vector3(flat.x, Mathf.Lerp(m_ClimbFrom.y, leg.To.y, share), flat.z);
                    if (along < 1f || share < 1f) break;
                    m_InTrunk = up;
                    if (!up) OnFloor();
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
                    if (m_LeapAt < 0f)
                    {
                        // over the grate's hole first (along the duct), then it hangs and drops
                        var over = new Vector3(floor.x, transform.position.y, floor.z);
                        transform.position = Vector3.MoveTowards(transform.position, over, step);
                        if ((transform.position - over).sqrMagnitude > 1e-4f) break;
                        m_LeapAt = Time.time;
                        m_ClimbFrom = transform.position;
                    }
                    float lower = StorageBotRules.DropLower(Time.time - m_LeapAt);
                    var at = new Vector3(floor.x, Mathf.Lerp(m_ClimbFrom.y, floor.y, lower), floor.z);
                    transform.position = at;
                    if (lower < 1f) break;
                    m_InDuct = false;
                    if (NavMesh.SamplePosition(at, out var land, 1.5f, NavMesh.AllAreas)) transform.position = land.position;
                    NextLeg();
                    break;
                }
            }
        }

        /// <summary>
        /// Server, on the basement shaft's navmesh link (its ends are the walkway at the ladder's foot and the lobby's floor
        /// beside the hole): across to the ladder, up or down its rungs with its paws on them, facing them, at its own
        /// pace (the agent would rush it at its walking speed), then off to the link's end — a hop over the hole at the
        /// top (<see cref="StorageBotRules.LadderWay"/>).
        /// </summary>
        void ClimbLadder()
        {
            var link = m_Agent.currentOffMeshLinkData;
            var end = link.endPos + Vector3.up * m_Agent.baseOffset;
            if (m_LadderWay == null)
            {
                var ladder = NearestLadder(link.startPos, link.endPos);
                m_LadderFrom = transform.position;
                m_LadderAt = 0;
                m_LadderFacing = ladder != null ? ladder.Facing : end - m_LadderFrom;
                m_LadderWay = ladder != null ? StorageBotRules.LadderWay(m_LadderFrom, end, ladder.Rungs, ladder.Facing) : new[] { end };
            }
            m_LegUntil += Time.deltaTime;
            var to = m_LadderWay[m_LadderAt];
            bool climb = m_LadderWay.Length == 3 && m_LadderAt == 1;
            m_OnRungs = climb;
            if (climb)
            {
                Face(m_LadderFacing);
                transform.position = Vector3.MoveTowards(transform.position, to, StorageBotRules.LadderSpeed * Time.deltaTime);
            }
            else
            {
                // across the walkway to the foot, or a hop over the hole at the top: flat at a walk, the height eased along
                var flatFrom = new Vector3(m_LadderFrom.x, 0f, m_LadderFrom.z);
                var flatTo = new Vector3(to.x, 0f, to.z);
                float length = Vector3.Distance(flatFrom, flatTo);
                var flatAt = Vector3.MoveTowards(new Vector3(transform.position.x, 0f, transform.position.z), flatTo, StorageBotRules.LadderStepSpeed * Time.deltaTime);
                float u = length > 1e-3f ? Vector3.Distance(flatFrom, flatAt) / length : 1f;
                bool top = Mathf.Max(m_LadderFrom.y, to.y) >= Mathf.Max(link.startPos.y, link.endPos.y) - 0.2f; // (along the top: over the hole)
                float y = Mathf.Lerp(m_LadderFrom.y, to.y, u) + (top ? StorageBotRules.HopArc(u, length) : 0f);
                Face(flatTo - flatFrom);
                transform.position = new Vector3(flatAt.x, y, flatAt.z);
                if (u >= 1f) transform.position = to;
            }
            if ((transform.position - to).sqrMagnitude > 1e-4f) return;
            m_LadderFrom = to;
            if (++m_LadderAt < m_LadderWay.Length) return;
            m_LadderWay = null;
            m_OnRungs = false;
            m_Agent.CompleteOffMeshLink();
        }

        /// <summary>The shaft's ladder a navmesh link climbs (its rungs nearest the link's ends), or null.</summary>
        static ShaftLadder NearestLadder(Vector3 a, Vector3 b)
        {
            ShaftLadder best = null;
            float d = 4f; // (further off: not this link's ladder)
            foreach (var l in ShaftLadder.All)
            {
                if (l == null) continue;
                var r = l.Rungs;
                float dd = Mathf.Min(new Vector2(a.x - r.x, a.z - r.z).magnitude, new Vector2(b.x - r.x, b.z - r.z).magnitude);
                if (dd < d) { d = dd; best = l; }
            }
            return best;
        }

        /// <summary>Server: what its body is doing now, from the leg it is on.</summary>
        BotMotion MotionNow()
        {
            if (m_Legs.Count == 0) return m_InTrunk ? BotMotion.Curled : m_InDuct ? BotMotion.Duct : m_Sitting ? BotMotion.Sit : BotMotion.Floor;
            switch (m_Legs[0].Kind)
            {
                case BotLegKind.HopOut: return BotMotion.VentDown;
                case BotLegKind.HopIn: return BotMotion.VentUp;
                case BotLegKind.ClimbUp: return m_LeapAt >= 0f ? BotMotion.VentUp : BotMotion.Floor;
                case BotLegKind.ClimbDown: return m_LeapAt >= 0f ? BotMotion.VentDown : BotMotion.Duct;
                case BotLegKind.Crawl: return BotMotion.Duct;
                default: return m_Agent.enabled && m_Agent.isOnOffMeshLink && m_OnRungs ? BotMotion.Ladder : BotMotion.Floor;
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
            if (m_InDuct || m_InTrunk || Mode == BotMode.Gone || Basement.Contains(l.Cellar, transform.position)) { m_Seen = 0f; return; }
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
