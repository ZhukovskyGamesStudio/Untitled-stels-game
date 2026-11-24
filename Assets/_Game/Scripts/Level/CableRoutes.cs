using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Cables that show what is wired to what (pure, from the plan; looks only — part of the services' mesh, no colliders):
    /// from each of the vault's three lock panels to the vault's door, and from the storage room's access terminal to its
    /// door. A cable rises from the device to just under the ceiling, runs along the ceiling close by the walls (through
    /// the doorways, over the doors) and drops to the door it opens: the player can follow it from a lock to what it locks.
    /// </summary>
    public static class CableRoutes
    {
        /// <summary>How far under the ceiling the cables run, their radius, and how far off the wall a drop hangs, metres.</summary>
        public const float UnderCeiling = 0.05f, Radius = 0.016f, OffWall = 0.025f;
        /// <summary>Where a cable meets a door: this high over the floor (over the door's head, on the wall above it).</summary>
        public const float DoorDrop = 2.4f;

        /// <summary>The cables of a house: the vault's (three) and the storage door's (one), as pipes of the services' mesh.</summary>
        public static void Lay(List<UtilityPart> parts, LevelLayout l, LevelShell shell = null)
        {
            if (l?.Plan == null) return;
            // the vault: every lock panel to the vault's doorway, on the panel's side of the wall
            if (l.VaultDoorway >= 0 && l.VaultDoorway < l.Doorways.Count)
            {
                var door = l.Doorways[l.VaultDoorway];
                foreach (var console in l.VaultConsoles)
                {
                    var facing = console.rotation * Vector3.forward;
                    var start = console.position + facing * OffWall;
                    Add(parts, l, start, Flat(facing), console.position.y + 0.35f, door, l.VaultRoom, shell);
                }
            }
            // the storage room: its access terminal (a stand) to its door
            if (l.StorageTerminal >= 0 && l.StorageTerminal < l.Props.Count && l.StorageDoorway >= 0 && l.StorageDoorway < l.Doorways.Count)
            {
                var t = l.Props[l.StorageTerminal];
                var facing = Quaternion.Euler(0f, t.RotationY, 0f) * Vector3.forward;
                Add(parts, l, t.Position - facing * 0.12f, Flat(facing), t.Position.y + 1.25f, l.Doorways[l.StorageDoorway], l.StorageRoom, shell);
            }
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z).normalized;

        /// <summary>One cable: up from <paramref name="start"/> (from <paramref name="riseFrom"/> high), along the ceiling to the doorway's outer side, down over the door.</summary>
        static void Add(List<UtilityPart> parts, LevelLayout l, Vector3 start, Vector2 facing, float riseFrom, Doorway door, int insideRoom, LevelShell shell)
        {
            int floor = LevelLayout.FloorOf(start.y + 0.1f);
            if (door.Floor != floor) return; // (on another storey: no cable between floors)
            float ceiling = LevelLayout.FloorY(floor) + LevelMetrics.WallHeight - UnderCeiling;
            // the doorway's side away from the locked room
            var across = door.Rotation * Vector3.forward;
            var a2 = new Vector2(across.x, across.z).normalized;
            var mid = new Vector2(door.Position.x, door.Position.z);
            int inside = l.Plan.IdAt(floor, HousePlan.CellOf(mid + a2 * 0.4f).x, HousePlan.CellOf(mid + a2 * 0.4f).y);
            if (inside == insideRoom) a2 = -a2;
            var doorFace = mid + a2 * (LevelMetrics.WallThickness / 2f + OffWall);
            // beside what is mounted over the doorway, never across it (nor across another's on the way)
            var along2 = new Vector2(-a2.y, a2.x);
            var keepOff = new List<Rect>();
            float sign = 0f;
            if (shell != null)
                foreach (var s in shell.Signs)
                {
                    if (s.Floor != floor) continue;
                    var c = new Vector2(s.Center.x, s.Center.z);
                    if ((c - mid).sqrMagnitude < 0.04f) sign = s.HalfWidth;
                    var half = new Vector2(Mathf.Abs(s.Along.x) * s.HalfWidth + Mathf.Abs(s.Along.z) * 0.6f, Mathf.Abs(s.Along.z) * s.HalfWidth + Mathf.Abs(s.Along.x) * 0.6f);
                    keepOff.Add(new Rect(c - half, 2f * half));
                }
            float near = Vector2.Dot(new Vector2(start.x, start.z) - mid, along2) >= 0f ? 1f : -1f;
            List<Vector2> route = null;
            foreach (float at in sign > 0f ? new[] { near * (sign + 0.4f), -near * (sign + 0.4f), near * (sign + 0.15f), -near * (sign + 0.15f), 0f } : new[] { 0f })
            {
                route = Route(l.Plan, floor, new Vector2(start.x, start.z) + facing * 0.3f, mid + along2 * at + a2 * 0.3f, keepOff);
                if (route == null) continue;
                doorFace += along2 * at;
                break;
            }
            if (route == null) return;
            var points = new List<Vector3> { new(start.x, riseFrom, start.z), new(start.x, ceiling, start.z) };
            foreach (var p in route) points.Add(new Vector3(p.x, ceiling, p.y));
            points.Add(new Vector3(doorFace.x, ceiling, doorFace.y));
            points.Add(new Vector3(doorFace.x, LevelLayout.FloorY(floor) + DoorDrop, doorFace.y));
            Pipes(parts, Square(points));
        }

        /// <summary>
        /// The path along the ceiling between two points of a floor: cell to cell (4 ways) through the rooms and their
        /// openings, keeping by the walls and turning seldom; the turning points (cell centres), or null when unreachable.
        /// </summary>
        public static List<Vector2> Route(HousePlan plan, int floor, Vector2 from, Vector2 to, List<Rect> keepOff = null)
        {
            var a = HousePlan.CellOf(from);
            var b = HousePlan.CellOf(to);
            if (!Open(plan, floor, a.x, a.y) || !Open(plan, floor, b.x, b.y)) return null;
            int nx = plan.NX, nz = plan.NZ;
            var cost = new float[nx * nz * 4];
            var prev = new int[nx * nz * 4];
            for (int i = 0; i < cost.Length; i++) { cost[i] = float.PositiveInfinity; prev[i] = -1; }
            var heap = new SortedSet<(float c, int node)>();
            for (int d = 0; d < 4; d++)
            {
                int n = (a.y * nx + a.x) * 4 + d;
                cost[n] = 0f;
                heap.Add((0f, n));
            }
            int goal = -1;
            var steps = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (heap.Count > 0)
            {
                var (c, node) = heap.Min;
                heap.Remove(heap.Min);
                if (c > cost[node]) continue;
                int cell = node / 4, dir = node % 4, x = cell % nx, z = cell / nx;
                if (x == b.x && z == b.y) { goal = node; break; }
                for (int d = 0; d < 4; d++)
                {
                    int x2 = x + steps[d].x, z2 = z + steps[d].y;
                    if (!Open(plan, floor, x2, z2) || !Passes(plan, floor, x, z, x2, z2)) continue;
                    float step = 1f + (ByWall(plan, floor, x2, z2) ? 0f : 1.2f) + (d != dir ? 2.5f : 0f);
                    int n2 = (z2 * nx + x2) * 4 + d;
                    if (keepOff != null)
                        foreach (var r in keepOff)
                            if (r.Contains(HousePlan.CellCenter(x2, z2))) { step += 30f; break; }
                    float c2 = c + step;
                    if (c2 >= cost[n2]) continue;
                    if (!float.IsInfinity(cost[n2])) heap.Remove((cost[n2], n2));
                    cost[n2] = c2;
                    prev[n2] = node;
                    heap.Add((c2, n2));
                }
            }
            if (goal < 0) return null;
            var cells = new List<Vector2Int>();
            for (int n = goal; n >= 0; n = prev[n])
            {
                var cell = new Vector2Int(n / 4 % nx, n / 4 / nx);
                if (cells.Count == 0 || cells[^1] != cell) cells.Add(cell);
            }
            cells.Reverse();
            // only the turning points
            var path = new List<Vector2>();
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0 && i < cells.Count - 1 && cells[i] - cells[i - 1] == cells[i + 1] - cells[i]) continue;
                path.Add(HousePlan.CellCenter(cells[i].x, cells[i].y));
            }
            return path;
        }

        static bool Open(HousePlan plan, int f, int x, int z) => plan.KindAt(f, x, z) == CellKind.Room;

        /// <summary>A step between two neighbouring cells crosses no solid wall (inside a room, or through an opening).</summary>
        static bool Passes(HousePlan plan, int f, int x, int z, int x2, int z2) =>
            plan.IdAt(f, x, z) == plan.IdAt(f, x2, z2) || plan.CanSee(f, HousePlan.CellCenter(x, z), HousePlan.CellCenter(x2, z2));

        /// <summary>A cell next to a wall (a neighbour of another room or of no floor).</summary>
        static bool ByWall(HousePlan plan, int f, int x, int z)
        {
            int id = plan.IdAt(f, x, z);
            return plan.IdAt(f, x + 1, z) != id || plan.IdAt(f, x - 1, z) != id || plan.IdAt(f, x, z + 1) != id || plan.IdAt(f, x, z - 1) != id
                   || plan.KindAt(f, x + 1, z) != CellKind.Room || plan.KindAt(f, x - 1, z) != CellKind.Room
                   || plan.KindAt(f, x, z + 1) != CellKind.Room || plan.KindAt(f, x, z - 1) != CellKind.Room;
        }

        /// <summary>The points made square: between two points that differ in more than one axis an elbow is put in.</summary>
        public static List<Vector3> Square(List<Vector3> points)
        {
            var o = new List<Vector3>();
            foreach (var p in points)
            {
                if (o.Count > 0)
                {
                    var q = o[^1];
                    int moved = (Mathf.Abs(p.x - q.x) > 1e-3f ? 1 : 0) + (Mathf.Abs(p.y - q.y) > 1e-3f ? 1 : 0) + (Mathf.Abs(p.z - q.z) > 1e-3f ? 1 : 0);
                    if (moved == 0) continue;
                    if (moved > 1)
                    {
                        // first along x, then along z (then up or down): every piece is straight along an axis
                        if (Mathf.Abs(p.x - q.x) > 1e-3f) o.Add(new Vector3(p.x, q.y, q.z));
                        if (Mathf.Abs(p.z - q.z) > 1e-3f && Mathf.Abs(p.y - q.y) > 1e-3f) o.Add(new Vector3(p.x, q.y, p.z));
                    }
                }
                o.Add(p);
            }
            return o;
        }

        /// <summary>A pipe per straight piece, each a radius longer at both ends (no gap at the bends).</summary>
        static void Pipes(List<UtilityPart> parts, List<Vector3> points)
        {
            for (int i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var d = b - a;
                if (d.sqrMagnitude < 1e-6f) continue;
                var e = d.normalized * Radius;
                parts.Add(UtilityPart.Pipe(a - e, b + e, Radius, UtilitySlot.Dark));
            }
        }
    }
}
