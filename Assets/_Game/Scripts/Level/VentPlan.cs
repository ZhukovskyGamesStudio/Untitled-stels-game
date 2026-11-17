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
        public static void Plan(LevelLayout l, float coverage)
        {
            l.Vents.Clear();
            var rng = new System.Random(LevelRandom.Mix(l.Seed, 0x7E47, 0));
            for (int f = 0; f < l.Floors; f++) l.Vents.Add(PlanFloor(l, f, coverage, rng));
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

        static VentNet PlanFloor(LevelLayout l, int f, float coverage, System.Random rng)
        {
            var net = new VentNet { Floor = f };
            var plan = l.Plan;
            int nx = plan.NX / 2, nz = plan.NZ / 2;
            var allowed = new bool[nx, nz];
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                    allowed[x, z] = Allowed(l, f, new Vector2Int(x, z));

            // a grate per room: a cell well inside the room (all round it the same room), nearest its middle
            var grates = new Dictionary<int, Vector2Int>();
            var rooms = new List<PlanRoom>();
            foreach (var r in plan.Rooms)
            {
                if (r.Floor != f || r.Levels != 1 || r.Area < 6f || r.Id == l.VaultRoom || r.Id == l.StorageRoom) continue;
                Vector2Int? best = null;
                float bestD = float.MaxValue;
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
                            if (d < bestD) { bestD = d; best = c; }
                        }
                if (best is Vector2Int g) { grates[r.Id] = g; rooms.Add(r); }
            }
            if (rooms.Count == 0) return net;

            // the tree: from the arrival room (the lobby; upstairs the biggest corridor or room), the exit room first
            int root = f == 0 ? Arrival(l, grates, rooms) : Biggest(rooms, plan);
            var connected = new HashSet<int> { root };
            net.Cells.Add(grates[root]);
            int exit = f == 0 ? plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f)?.Id ?? -1 : -1;
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
            foreach (int id in connected)
                net.Grates.Add(new DuctGrate { Room = id, Cell = grates[id], Position = CellCenter(grates[id], LevelLayout.FloorY(f) + LevelMetrics.WallHeight) });
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
