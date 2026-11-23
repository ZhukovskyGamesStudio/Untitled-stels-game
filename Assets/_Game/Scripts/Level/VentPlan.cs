using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>A grate in a room's ceiling, the way into (and out of) a storey's duct network.</summary>
    public struct DuctGrate
    {
        public int Room;
        /// <summary>The duct cell over the room (<see cref="VentPlan.Res"/>-metre grid).</summary>
        public Vector2Int Cell;
        /// <summary>The middle of the grate, on the room's ceiling.</summary>
        public Vector3 Position;
        /// <summary>Its lid is screwed shut: only a screwdriver in hand takes it off (<see cref="VentGrate"/>).</summary>
        public bool Closed;
        /// <summary>
        /// The way up to it (−1: none, only down): a crate stack right under it and a desk beside the stack on this side
        /// (0 +x, 1 −x, 2 +z, 3 −z) — floor → desk → crates → a crouched jump into the duct (<see cref="VentPlan.ClimbSide"/>).
        /// </summary>
        public int ClimbDir;
    }

    /// <summary>The crawl ducts in one storey's ceiling void (between its ceiling and the floor above).</summary>
    public class VentNet
    {
        /// <summary>The storey under the void.</summary>
        public int Floor;
        public readonly HashSet<Vector2Int> Cells = new();
        public readonly List<DuctGrate> Grates = new();

        /// <summary>A duct cell at this grate cell?</summary>
        public bool IsGrate(Vector2Int c)
        {
            foreach (var g in Grates) if (g.Cell == c) return true;
            return false;
        }
    }

    /// <summary>
    /// The duct network (pure, own stream): every storey has a crawl void over its ceiling (<see cref="LevelMetrics.VoidHeight"/>);
    /// ducts are tunnels through it on a 1 m grid, a tree that starts over the arrival room (the lobby) and reaches the exit
    /// room first — the team always has a hidden way from where it comes in to where it leaves —, then the nearest rooms
    /// until <see cref="BuildingSettings.VentCoverage"/> of them have a grate in their ceiling. The tunnels prefer to run
    /// over corridors; they never cross a stair hole or an atrium, and keep out of the vault and the storage room (their
    /// door is their only way in). Only players crawl there: a robot does not fit (1.15 m of headroom).
    /// </summary>
    public static class VentPlan
    {
        /// <summary>A duct cell: two plan cells a side.</summary>
        public const float Res = 2 * HousePlan.Res;

        public static Vector3 CellCenter(Vector2Int c, float y) => new((c.x + 0.5f) * Res, y, (c.y + 0.5f) * Res);
        public static Vector2Int CellOf(Vector3 p) => new(Mathf.FloorToInt(p.x / Res), Mathf.FloorToInt(p.z / Res));

        /// <summary>Plans the ducts of every storey into <see cref="LevelLayout.Vents"/>.</summary>
        public static void Plan(LevelLayout l, float coverage, float closedChance = 0f, float climbChance = 0f)
        {
            l.Vents.Clear();
            var rng = new System.Random(LevelRandom.Mix(l.Seed, Salt.Vents, 0));
            for (int f = 0; f < l.Floors; f++) l.Vents.Add(PlanFloor(l, f, coverage, rng, climbChance));
            // some lids are screwed shut (own stream): never the one over the room the team arrives in
            var lids = new System.Random(LevelRandom.Mix(l.Seed, Salt.VentLids, 0));
            foreach (var net in l.Vents)
                for (int i = 0; i < net.Grates.Count; i++)
                {
                    var g = net.Grates[i];
                    bool arrival = net.Floor == 0 && i == 0; // the tree's root: the lobby's grate (or the one nearest it)
                    g.Closed = !arrival && lids.NextDouble() < closedChance;
                    if (arrival) g.ClimbDir = -1; // never a way up in the room the team arrives in
                    net.Grates[i] = g;
                }
        }

        /// <summary>
        /// The duct cells of the ground floor's void a duct from <paramref name="room"/> can get to (a double-height hall or a
        /// stair hole may cut the void in parts): the exit room must be in the lobby's part.
        /// </summary>
        public static HashSet<Vector2Int> VoidReach(LevelLayout l, int room)
        {
            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            int nx = l.Plan.NX / 2, nz = l.Plan.NZ / 2;
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    var c = new Vector2Int(x, z);
                    if (l.Plan.SpaceAt(0, new Vector2((x + 0.5f) * Res, (z + 0.5f) * Res)) == room && Allowed(l, 0, c) && seen.Add(c)) queue.Enqueue(c);
                }
            if (seen.Count == 0)
            {
                // an atrium lobby has no ceiling: the ducts start from the rooms around it (Arrival)
                var around = l.Plan.Rooms[room].Bounds;
                around.min -= Vector2.one * 3f;
                around.max += Vector2.one * 3f;
                for (int x = 0; x < nx; x++)
                    for (int z = 0; z < nz; z++)
                    {
                        var c = new Vector2Int(x, z);
                        if (around.Contains(new Vector2((x + 0.5f) * Res, (z + 0.5f) * Res)) && Allowed(l, 0, c) && seen.Add(c)) queue.Enqueue(c);
                    }
            }
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || seen.Contains(n) || !Allowed(l, 0, n)) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// <summary>A duct can come down into the room: one storey high, and some cell of it has the void over it.</summary>
        public static bool CanReach(LevelLayout l, PlanRoom r, HashSet<Vector2Int> reach)
        {
            if (r.Levels != 1) return false;
            var b = r.Bounds;
            for (int x = Mathf.FloorToInt(b.xMin / Res); x * Res < b.xMax; x++)
                for (int z = Mathf.FloorToInt(b.yMin / Res); z * Res < b.yMax; z++)
                {
                    var c = new Vector2Int(x, z);
                    if (!reach.Contains(c)) continue;
                    bool own = true;
                    for (int px = x * 2; px <= x * 2 + 1 && own; px++)
                        for (int pz = z * 2; pz <= z * 2 + 1 && own; pz++)
                            own = l.Plan.SpaceAt(r.Floor, HousePlan.CellCenter(px, pz)) == r.Id;
                    if (own) return true;
                }
            return false;
        }

        /// <summary>A duct may run in this cell of floor <paramref name="f"/>'s void: over rooms or pockets, under a slab or the roof, no stair hole.</summary>
        public static bool Allowed(LevelLayout l, int f, Vector2Int c)
        {
            var plan = l.Plan;
            int top = l.Floors - 1;
            for (int dx = 0; dx < 2; dx++)
                for (int dz = 0; dz < 2; dz++)
                {
                    int x = c.x * 2 + dx, z = c.y * 2 + dz;
                    if (x < 0 || z < 0 || x >= plan.NX || z >= plan.NZ) return false;
                    var k = plan.KindAt(f, x, z);
                    if (k is not (CellKind.Room or CellKind.Pocket)) return false;
                    if (f < top)
                    {
                        var above = plan.KindAt(f + 1, x, z);
                        if (above is not (CellKind.Room or CellKind.Pocket)) return false; // an atrium, a setback: no slab over it
                    }
                    var p = HousePlan.CellCenter(x, z);
                    foreach (var s in l.Stairs)
                        if ((s.Floor == f || s.Floor + 1 == f) && PlanGenerator.StairArea(s, 0.3f).Contains(p)) return false;
                    int room = plan.SpaceAt(f, p);
                    if (room >= 0 && plan.Rooms[room].Levels != 1) return false; // (over the vault or the storage room it may pass: no grate there)
                }
            return true;
        }

        /// <summary>The side of a climb's desk (<see cref="DuctGrate.ClimbDir"/>) as a direction.</summary>
        public static Vector3 ClimbSide(int dir) => dir switch { 0 => Vector3.right, 1 => Vector3.left, 2 => Vector3.forward, _ => Vector3.back };

        /// <summary>The crate stack under the grate and the desk beside it: half their depth, the gap between them.</summary>
        public const float StackHalf = 0.39f, DeskHalf = 0.4f;

        /// <summary>Half a ceiling lamp's fixture (two panels, 1.24 × 1.28 m) and the gap a grate keeps from it.</summary>
        public const float LampHalf = 0.65f, LampGap = 0.15f;

        /// <summary>How far the nearest lamp hangs from the middle of cell <paramref name="c"/> (the larger of the two distances along the axes).</summary>
        public static float LampDistance(Vector2Int c, List<Vector2> lamps)
        {
            var m = new Vector2((c.x + 0.5f) * Res, (c.y + 0.5f) * Res);
            float best = float.MaxValue;
            foreach (var p in lamps) best = Mathf.Min(best, Mathf.Max(Mathf.Abs(p.x - m.x), Mathf.Abs(p.y - m.y)));
            return best;
        }

        /// <summary>The grate in cell <paramref name="c"/> would cut into (or touch) a ceiling lamp hanging at one of <paramref name="lamps"/>.</summary>
        /// <summary>
        /// Which way a ceiling grate is turned (pure; degrees about the vertical, a quarter turn at a time): its lid hangs
        /// open from the hinge edge (the prefab's −Z, turned by this) — on the side furthest from every doorway of its
        /// storey. A grate sits just inside its room's way in: hinged on the door's side, the open lid hung across the door.
        /// </summary>
        public static float LidYaw(Vector3 grate, IReadOnlyList<Doorway> doorways)
        {
            float best = -1f, yaw = 0f;
            for (int k = 0; k < 4; k++)
            {
                var edge = grate + Quaternion.Euler(0f, k * 90f, 0f) * Vector3.back * (Res / 2f);
                float near = float.MaxValue;
                if (doorways != null)
                    foreach (var d in doorways)
                    {
                        if (Mathf.Abs(d.Position.y - (grate.y - LevelMetrics.WallHeight)) > 1f) continue; // another storey's
                        near = Mathf.Min(near, new Vector2(edge.x - d.Position.x, edge.z - d.Position.z).magnitude);
                    }
                if (near > best + 0.05f) { best = near; yaw = k * 90f; }
            }
            return yaw;
        }

        public static bool ClashesLamp(Vector2Int c, List<Vector2> lamps)
        {
            var m = new Vector2((c.x + 0.5f) * Res, (c.y + 0.5f) * Res);
            float reach = Res / 2f + LampHalf + LampGap;
            foreach (var p in lamps)
                if (Mathf.Abs(p.x - m.x) < reach && Mathf.Abs(p.y - m.y) < reach) return true;
            return false;
        }

        /// <summary>
        /// A climb fits under a grate at <paramref name="c"/> with its desk on side <paramref name="dir"/>: the stack, the
        /// desk and an aisle in front of the desk all on the room's floor, away from its doors.
        /// </summary>
        /// <summary>
        /// Rooms that never get a way up: the walkways (corridors, halls, the lobby), the exit's room (its hatch), the robots'
        /// rooms (control, charging: their consoles and docks), storage, the workshop and its booth, the hostage's room.
        /// </summary>
        static bool NoClimb(LevelLayout l, PlanRoom r)
        {
            if (r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby or RoomType.ControlRoom or RoomType.ChargingStation
                or RoomType.Storage or RoomType.Workshop or RoomType.PaintBooth) return true;
            if (l.ExitRoom == r.Id) return true;
            foreach (var p in l.Props)
                if (p.Kind is PropKind.Cage or PropKind.Hatch or PropKind.ShaftHatch && l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == r.Id) return true;
            return false;
        }

        static bool ClimbFits(LevelLayout l, int f, PlanRoom r, Vector2 c, int dir)
        {
            var s = ClimbSide(dir);
            var side = new Vector2(s.x, s.z);
            var across = new Vector2(-side.y, side.x);
            for (float a = -0.75f; a <= 2.6f; a += 0.25f)
                for (float b = -1.0f; b <= 1.0f; b += 0.25f)
                {
                    var q = c + side * a + across * b;
                    if (l.Plan.SpaceAt(f, q) != r.Id || l.NearStair(f, new Vector3(q.x, LevelLayout.FloorY(f), q.y), 0.8f)) return false;
                }
            // clear of the room's middle (its patrol stop is kept free) and of the facade (the crates are tall: not before a window)
            var mid = new Vector2(r.Center.x, r.Center.z);
            for (float a = -0.5f; a <= 1.8f; a += 0.45f)
            {
                var q = c + side * a;
                if ((q - mid).magnitude < 1.3f) return false;
                foreach (var o in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                    if (l.Plan.SpaceAt(f, q + o * 1.2f) < 0 && !l.Plan.IsInside(q + o * 1.2f)) return false;
            }
            foreach (var d in l.Doorways)
            {
                if (d.Floor != f || (d.RoomA != r.Id && d.RoomB != r.Id)) continue;
                var dp = new Vector2(d.Position.x, d.Position.z);
                for (float a = 0f; a <= 2.2f; a += 0.55f)
                    if ((c + side * a - dp).magnitude < 1.9f) return false; // clear of the way in
            }
            return true;
        }

        static VentNet PlanFloor(LevelLayout l, int f, float coverage, System.Random rng, float climbChance = 0f)
        {
            var climbRng = new System.Random(LevelRandom.Mix(l.Seed, Salt.VentClimbs, f));
            var climbs = new Dictionary<int, int>();
            var net = new VentNet { Floor = f };
            var plan = l.Plan;
            int nx = plan.NX / 2, nz = plan.NZ / 2;
            var allowed = new bool[nx, nz];
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                    allowed[x, z] = Allowed(l, f, new Vector2Int(x, z));

            // the ceiling lamps of this storey: a grate never cuts into one (lamps hang where LevelBuilder.LightSpots says)
            var lamps = new List<Vector2>();
            foreach (var r in plan.Rooms)
                if (r.Floor + r.Levels - 1 == f) lamps.AddRange(LevelBuilder.LightSpots(l, r));
            bool Lit(Vector2Int c) => ClashesLamp(c, lamps);

            // a grate per room: a cell well inside the room (all round it the same room), nearest its middle
            var grates = new Dictionary<int, Vector2Int>();
            var rooms = new List<PlanRoom>();
            foreach (var r in plan.Rooms)
            {
                if (r.Floor != f || r.Levels != 1 || r.Area < 6f || r.Id == l.VaultRoom || r.Id == l.StorageRoom) continue;
                Vector2Int? best = null;
                float bestD = float.MaxValue;
                // some rooms get a way up: a crate stack under the grate, a desk beside it (never the lobby: the team's room)
                bool climb = climbRng.NextDouble() < climbChance && r.Id != l.LobbyRoom && r.Area >= 12f && !NoClimb(l, r);
                if (climb)
                {
                    float far = -1f;
                    for (int x = 0; x < nx; x++)
                        for (int z = 0; z < nz; z++)
                        {
                            var c = new Vector2Int(x, z);
                            if (!allowed[x, z] || !Inside(plan, f, r.Id, c)) continue;
                            var m = new Vector2((x + 0.5f) * Res, (z + 0.5f) * Res);
                            for (int dir = 0; dir < 4; dir++)
                            {
                                if (!ClimbFits(l, f, r, m, dir)) continue;
                                float score = (float)climbRng.NextDouble() - (m - new Vector2(r.Center.x, r.Center.z)).magnitude * 0.05f;
                                if (score > far && !Lit(c)) { far = score; best = c; climbs[r.Id] = dir; }
                            }
                        }
                }
                // just inside its main way in (a walkway anyway: the room's own furniture keeps its middle)
                var centre = new Vector2(r.Center.x, r.Center.z);
                foreach (var d in l.Doorways)
                {
                    if (d.Kind is not (EdgeType.Door or EdgeType.Open or EdgeType.Breach) || (d.RoomA != r.Id && d.RoomB != r.Id)) continue;
                    var across = d.Rotation * Vector3.forward;
                    var inward = Vector3.Dot(r.Center - d.Position, across) >= 0f ? across : -across;
                    var p = d.Position + inward.normalized * 1.1f; // (the doorway keeps its floor free anyway)
                    centre = new Vector2(p.x, p.z);
                    break;
                }
                // well inside first; a narrow room (the lobby, the exit's room must have one) takes any cell of its own
                for (int pass = 0; pass < 2 && best == null; pass++)
                    for (int x = 0; x < nx; x++)
                        for (int z = 0; z < nz; z++)
                        {
                            if (!allowed[x, z]) continue;
                            var c = new Vector2Int(x, z);
                            if (pass == 0 ? !Inside(plan, f, r.Id, c) : !Own(plan, f, r.Id, c)) continue;
                            var m = new Vector2((x + 0.5f) * Res, (z + 0.5f) * Res);
                            float d = (m - centre).sqrMagnitude + (float)rng.NextDouble() * 0.5f;
                            if (Lit(c)) // clear of the lamps (a narrow room may have no such cell: then the lamp moves aside, LevelBuilder.LightSpots)
                            {
                                if (pass == 0) continue;
                                d += 1000f - 100f * LampDistance(c, lamps); // as far from the lamp as it gets: room for the lamp to move aside
                            }
                            if (d < bestD) { bestD = d; best = c; }
                        }
                if (best is Vector2Int g) { grates[r.Id] = g; rooms.Add(r); }
            }
            if (rooms.Count == 0) return net;

            // the tree: from the arrival room (the lobby; upstairs the biggest corridor or room), the exit room first
            int root = f == 0 ? Arrival(l, grates, rooms) : Biggest(rooms, plan);
            var connected = new HashSet<int> { root };
            net.Cells.Add(grates[root]);
            int exit = f == 0 ? l.ExitRoom : -1;
            int want = Mathf.Max(1, Mathf.CeilToInt(rooms.Count * coverage));
            while (connected.Count < want || (exit >= 0 && grates.ContainsKey(exit) && !connected.Contains(exit)))
            {
                var dist = Distances(l, f, net.Cells, allowed, out var from);
                int pick = -1;
                float pickD = float.MaxValue;
                foreach (var r in rooms)
                {
                    if (connected.Contains(r.Id)) continue;
                    float d = dist.TryGetValue(grates[r.Id], out var v) ? v : float.MaxValue;
                    if (r.Id == exit && d < float.MaxValue) { pick = r.Id; break; } // the way out first
                    if (d < pickD) { pickD = d; pick = r.Id; }
                }
                if (pick < 0 || !dist.ContainsKey(grates[pick])) break; // the rest is cut off (by stairs, an atrium)
                for (var c = grates[pick]; !net.Cells.Contains(c); c = from[c]) net.Cells.Add(c);
                connected.Add(pick);
                if (connected.Count >= rooms.Count) break;
            }
            // the root first: the arrival's grate is index 0
            connected.Remove(root);
            net.Grates.Add(new DuctGrate { Room = root, Cell = grates[root], Position = CellCenter(grates[root], LevelLayout.FloorY(f) + LevelMetrics.WallHeight), ClimbDir = -1 });
            foreach (int id in connected)
                net.Grates.Add(new DuctGrate { Room = id, Cell = grates[id], Position = CellCenter(grates[id], LevelLayout.FloorY(f) + LevelMetrics.WallHeight),
                    ClimbDir = climbs.TryGetValue(id, out var dir) ? dir : -1 });
            return net;
        }

        /// <summary>
        /// Where the team arrives: the lobby — or, when it has no ceiling of its own (an atrium lobby), the room with a grate
        /// nearest to it, a step away.
        /// </summary>
        public static int Arrival(LevelLayout l, Dictionary<int, Vector2Int> grates, List<PlanRoom> rooms)
        {
            if (grates.ContainsKey(l.LobbyRoom)) return l.LobbyRoom;
            var lobby = l.Lobby.Center;
            PlanRoom best = rooms[0];
            foreach (var r in rooms)
                if ((r.Center - lobby).sqrMagnitude < (best.Center - lobby).sqrMagnitude) best = r;
            return best.Id;
        }

        static int Biggest(List<PlanRoom> rooms, HousePlan plan)
        {
            PlanRoom best = rooms[0];
            foreach (var r in rooms)
                if ((r.Type == RoomType.Corridor) != (best.Type == RoomType.Corridor) ? r.Type == RoomType.Corridor : r.Area > best.Area) best = r;
            return best.Id;
        }

        /// <summary>The cell and a ring of half a cell round it all lie in this room (a grate away from its walls).</summary>
        static bool Inside(HousePlan plan, int f, int room, Vector2Int c)
        {
            for (int x = c.x * 2 - 1; x <= c.x * 2 + 2; x++)
                for (int z = c.y * 2 - 1; z <= c.y * 2 + 2; z++)
                    if (x < 0 || z < 0 || x >= plan.NX || z >= plan.NZ || plan.SpaceAt(f, HousePlan.CellCenter(x, z)) != room) return false;
            return true;
        }

        /// <summary>All four plan cells of the duct cell lie in this room.</summary>
        static bool Own(HousePlan plan, int f, int room, Vector2Int c)
        {
            for (int x = c.x * 2; x <= c.x * 2 + 1; x++)
                for (int z = c.y * 2; z <= c.y * 2 + 1; z++)
                    if (plan.SpaceAt(f, HousePlan.CellCenter(x, z)) != room) return false;
            return true;
        }

        /// <summary>Walking distances in the void from the network (multi-source Dijkstra; over corridors cheaper).</summary>
        static Dictionary<Vector2Int, float> Distances(LevelLayout l, int f, HashSet<Vector2Int> sources, bool[,] allowed, out Dictionary<Vector2Int, Vector2Int> from)
        {
            var dist = new Dictionary<Vector2Int, float>();
            from = new Dictionary<Vector2Int, Vector2Int>();
            var open = new SortedSet<(float d, int x, int z)>();
            foreach (var s in sources) { dist[s] = 0f; open.Add((0f, s.x, s.y)); }
            int nx = allowed.GetLength(0), nz = allowed.GetLength(1);
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (open.Count > 0)
            {
                var top = open.Min;
                open.Remove(top);
                var c = new Vector2Int(top.x, top.z);
                if (top.d > dist[c]) continue;
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || !allowed[n.x, n.y]) continue;
                    int room = l.Plan.SpaceAt(f, new Vector2((n.x + 0.5f) * Res, (n.y + 0.5f) * Res));
                    bool corridor = room >= 0 && l.Plan.Rooms[room].Type is RoomType.Corridor or RoomType.Hall;
                    float nd = top.d + (corridor ? 1f : 1.6f);
                    if (dist.TryGetValue(n, out var old) && old <= nd) continue;
                    if (dist.ContainsKey(n)) open.Remove((old, n.x, n.y));
                    dist[n] = nd;
                    from[n] = c;
                    open.Add((nd, n.x, n.y));
                }
            }
            return dist;
        }

        /// <summary>A path through the ducts between two grates of one storey (pure: for tests and the storage robot), or null.</summary>
        public static List<Vector2Int> Path(VentNet net, Vector2Int a, Vector2Int b)
        {
            if (!net.Cells.Contains(a) || !net.Cells.Contains(b)) return null;
            var from = new Dictionary<Vector2Int, Vector2Int> { [a] = a };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(a);
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == b) break;
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (!net.Cells.Contains(n) || from.ContainsKey(n)) continue;
                    from[n] = c;
                    queue.Enqueue(n);
                }
            }
            if (!from.ContainsKey(b)) return null;
            var path = new List<Vector2Int>();
            for (var c = b; c != a; c = from[c]) path.Add(c);
            path.Add(a);
            path.Reverse();
            return path;
        }
    }
}
