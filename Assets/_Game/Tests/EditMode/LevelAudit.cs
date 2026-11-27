using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Items;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// A report on the generated houses as levels, kind by kind, with the game's own settings (Data/Config/Level): how big,
    /// how many ways round, how many rooms the way from the entry to the exit cannot do without, how long the straight
    /// corridors are, how much one type of room repeats and how alike two houses of a kind are. Writes Logs/level_audit.txt.
    /// </summary>
    [Explicit("writes a report: Logs/level_audit.txt")]
    public class LevelAudit
    {
        static readonly HouseKind[] Kinds =
        {
            HouseKind.Apartments, HouseKind.Garage, HouseKind.Hospital, HouseKind.Abandoned,
            HouseKind.Office, HouseKind.Business, HouseKind.DataCenter, HouseKind.Warehouse, HouseKind.Headquarters,
        };

        const int Seeds = 8, Houses = 5;
        Stealth.Game.RunSettings m_Run;

        [Test, Timeout(900000)]
        public void WriteReport()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Data/Config/Level.asset");
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var run = new Stealth.Game.RunSettings();
            m_Run = run;
            var sb = new StringBuilder();
            var only = System.Environment.GetEnvironmentVariable("AUDITKINDS");
            foreach (var kind in Kinds)
            {
                if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(kind.ToString())) continue;
                var specs = new List<TestHouses.Spec>();
                for (int h = 0; h < Houses; h++)
                    for (int s = 0; s < Seeds; s++)
                        specs.Add(new TestHouses.Spec(HouseKinds.SeedOf(kind, h, 1000 + 37 * s), h, config.Building, config.Security, 2, config.Features));
                var all = TestHouses.Many(specs);
                var m = new SortedDictionary<string, List<float>>(System.StringComparer.Ordinal);
                void Add(string k, float v) { if (!m.TryGetValue(k, out var list)) m[k] = list = new(); list.Add(v); }
                var types = new Dictionary<RoomType, int>();
                var masses = new HashSet<string>();
                foreach (var l in all) { Measure(l, Add, types, config); masses.Add(l.Mass); }
                for (int h = 0; h < Houses; h++)
                    for (int a = 0; a < Seeds; a++)
                        for (int b = a + 1; b < Seeds; b++)
                            Add("14 alike: same type at same place, two houses of a level", Alike(all[h * Seeds + a].Plan, all[h * Seeds + b].Plan));
                // the biggest of them once more, on one thread: how long a house takes
                var big = all.OrderByDescending(l => l.FloorArea).First();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                LevelGenerator.Generate(big.Seed, big.LevelIndex, config.Building, config.Security, 2, config.Features);
                Add("18 seconds to generate the biggest", watch.ElapsedMilliseconds / 1000f);
                watch.Restart();
                ActiveLevel.Load(big, art);
                Add("18 seconds to build it", watch.ElapsedMilliseconds / 1000f);
                watch.Restart();
                var surface = ActiveLevel.Root.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
                if (surface != null) surface.BuildNavMesh();
                Add("18 seconds to bake its navmesh", watch.ElapsedMilliseconds / 1000f);
                Add("18 renderers in it", ActiveLevel.Root.GetComponentsInChildren<Renderer>().Length);
                ActiveLevel.Clear();
                UnityEngine.AI.NavMesh.RemoveAllNavMeshData();
                sb.AppendLine($"=== {kind}: {all.Count} houses, masses: {string.Join(", ", masses.Where(x => x != ""))}; laid out: " +
                              string.Join(", ", all.GroupBy(l => l.Scheme).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
                foreach (var (k, v) in m) sb.AppendLine($"  {k,-62} avg {v.Average(),7:0.00}  min {v.Min(),7:0.00}  max {v.Max(),7:0.00}");
                int n = types.Values.Sum();
                sb.AppendLine("  types: " + string.Join(", ", types.OrderByDescending(t => t.Value).Take(9).Select(t => $"{t.Key} {100f * t.Value / n:0}%")));
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Logs", "level_audit.txt"), sb.ToString());
        }

        void Measure(LevelLayout l, System.Action<string, float> add, Dictionary<RoomType, int> types, LevelConfig config)
        {
            var plan = l.Plan;
            var rooms = plan.Rooms;
            add("01 rooms", rooms.Count);
            add("02 floors", plan.Floors);
            add("03 area m2", rooms.Sum(r => r.Area));
            add("04 distinct room types", rooms.Select(r => r.Type).Distinct().Count());
            add("05 share of the commonest type", rooms.GroupBy(r => r.Type).Max(g => g.Count()) / (float)rooms.Count);
            add("06 share of plain rectangles", rooms.Count(r => r.Shape == RoomShape.Rect && r.Shaped.Count == 0) / (float)rooms.Count);
            foreach (var r in rooms) { types.TryGetValue(r.Type, out int c); types[r.Type] = c + 1; }

            var plain = LootPlanner.Plan(l, new System.Random(l.Seed), config.Loot);
            float want = Stealth.Game.RunState.QuotaFor(l.LevelIndex, 0, m_Run, l.Players) * m_Run.LootPerQuota;
            var loot = LootPlanner.Plan(l, new System.Random(l.Seed), config.Loot, want);
            add("16 loot value before the guarantee", LootPlanner.ValueOf(plain));
            add("16 houses short of twice the quota", LootPlanner.ValueOf(loot) < want ? 1f : 0f);
            float per1000 = 1000f / rooms.Sum(r => r.Area);
            add("16 items", loot.Count);
            add("16 loot value", loot.Sum(t => ItemCatalog.Value(t.Shape, t.Color)));
            add($"16 loot value, house {l.LevelIndex}", loot.Sum(t => ItemCatalog.Value(t.Shape, t.Color)));
            add("16 loot value per 1000 m2", per1000 * loot.Sum(t => ItemCatalog.Value(t.Shape, t.Color)));
            add($"17 robots, house {l.LevelIndex}", l.RobotSpawns.Count(r => !RobotRoster.IsDevice(r.Kind)));
            add("17 robots per 1000 m2", per1000 * l.RobotSpawns.Count(r => !RobotRoster.IsDevice(r.Kind)));
            add("17 traps per 1000 m2 (devices, cameras, lasers)", per1000 * (l.RobotSpawns.Count(r => RobotRoster.IsDevice(r.Kind)) + l.CameraSpots.Count + l.LaserSpots.Count));
            foreach (var (name, pass) in new[] { ("robots", Passage.Robots), ("players", Passage.Players) })
            {
                var degree = new int[rooms.Count];
                var pairs = new HashSet<(int, int)>();
                void Pair(int a, int b) { if (a >= 0 && b >= 0 && a < rooms.Count && b < rooms.Count && a != b) pairs.Add((Mathf.Min(a, b), Mathf.Max(a, b))); }
                foreach (var o in plan.Openings)
                    if (RoomGraph.Passes(pass, o.Kind)) Pair(plan.Walls[o.Wall].RoomA, plan.Walls[o.Wall].RoomB);
                foreach (var s in l.Stairs) Pair(s.Room, s.RoomAbove);
                foreach (var (a, b) in pairs) { degree[a]++; degree[b]++; }
                add($"07 dead ends, {name}", degree.Count(d => d <= 1) / (float)rooms.Count);
                add($"08 loops per 10 rooms, {name}", 10f * (pairs.Count - rooms.Count + 1) / rooms.Count);
                if (l.EntryRoom < 0 || l.ExitRoom < 0) continue;
                var g = new RoomGraph(plan, l.Stairs, pass);
                add($"09 steps entry to exit, {name}", g.Steps(l.EntryRoom)[l.ExitRoom]);
                int choke = 0;
                for (int r = 0; r < rooms.Count; r++)
                    if (r != l.EntryRoom && r != l.ExitRoom && g.Steps(l.EntryRoom, -1, new HashSet<int> { r })[l.ExitRoom] < 0) choke++;
                add($"10 rooms no way to the exit avoids, {name}", choke);
            }
            add("11 longest corridor, m", rooms.Where(r => r.Type == RoomType.Corridor).Select(r => Mathf.Max(r.Bounds.width, r.Bounds.height)).DefaultIfEmpty(0f).Max());
            add("12 median room m2", rooms.Select(r => r.Area).OrderBy(x => x).ElementAt(rooms.Count / 2));
            // what tells one kind's layout from another's (LayoutSignature)
            add("19 layout: corridor share of the floor", LayoutSignature.CorridorShare(l));
            add("19 layout: islands a floor", LayoutSignature.Islands(l));
            add("19 layout: biggest room, m2", LayoutSignature.BiggestRoom(l));
            add("19 layout: longest straight corridor, m", LayoutSignature.LongestStraight(l));
            add("19 layout: widest place in the corridors, m", LayoutSignature.WidestCorridor(l));
            add("19 layout: houses with a court or an atrium", LayoutSignature.Court(l) > 0f ? 1f : 0f);
            add("19 layout: mirrored about the entrance", LayoutSignature.Symmetry(l));
            add("17 plans drawn before one held", l.PlanAttempt + 1);
            add("12 share of area in rooms over 60 m2", rooms.Where(r => r.Area > 60f && r.Type != RoomType.Corridor).Sum(r => r.Area) / rooms.Sum(r => r.Area));
            int same = 0, cells = 0;
            for (int f = 1; f < plan.Floors; f++)
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                    {
                        if (plan.KindAt(f, x, z) != CellKind.Room || plan.KindAt(f - 1, x, z) != CellKind.Room) continue;
                        cells++;
                        if (rooms[plan.IdAt(f, x, z)].Type == rooms[plan.IdAt(f - 1, x, z)].Type) same++;
                    }
            if (cells > 0) add("13 alike: same type one floor below", same / (float)cells);
            add("15 props per room", l.Props.Count / (float)rooms.Count);
            var counts = new int[rooms.Count];
            foreach (var p in l.Props) { var r = plan.RoomAt(p.Position + Vector3.up * 0.3f); if (r != null) counts[r.Id]++; }
            add("15 rooms with under 3 pieces (no corridors)", rooms.Count(r => r.Type != RoomType.Corridor && counts[r.Id] < 3) / (float)rooms.Count);
        }

        static float Alike(HousePlan a, HousePlan b)
        {
            int same = 0, union = 0;
            for (int f = 0; f < Mathf.Max(a.Floors, b.Floors); f++)
                for (int x = 0; x < Mathf.Max(a.NX, b.NX); x++)
                    for (int z = 0; z < Mathf.Max(a.NZ, b.NZ); z++)
                    {
                        bool ra = a.KindAt(f, x, z) == CellKind.Room, rb = b.KindAt(f, x, z) == CellKind.Room;
                        if (!ra && !rb) continue;
                        union++;
                        if (ra && rb && a.Rooms[a.IdAt(f, x, z)].Type == b.Rooms[b.IdAt(f, x, z)].Type) same++;
                    }
            return union == 0 ? 0f : same / (float)union;
        }
    }
}
