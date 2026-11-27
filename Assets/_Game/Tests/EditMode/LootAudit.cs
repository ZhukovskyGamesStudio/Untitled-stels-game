using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// A report on the loot of the first houses against their quota, kind by kind, with the game's own settings: how much
    /// a house holds, how thin it lies (per room, per 100 m²), what a thing is worth by the cell it takes in the backpack,
    /// how many points a full backpack and belt bag carry — and so how many trips to the car a quota is. Writes
    /// Logs/loot_audit.txt. AUDITKINDS=Hospital,Office picks the kinds, AUDITPLAYERS the team (1).
    /// </summary>
    [Explicit("writes a report: Logs/loot_audit.txt")]
    public class LootAudit
    {
        static readonly HouseKind[] Kinds =
        {
            HouseKind.Hospital, HouseKind.Abandoned, HouseKind.Office, HouseKind.Business, HouseKind.Garage, HouseKind.Apartments,
        };

        const int Seeds = 6, Houses = 2;

        [Test, Timeout(900000)]
        public void WriteReport()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Data/Config/Level.asset");
            var runConfig = AssetDatabase.LoadAssetAtPath<RunConfig>("Assets/_Game/Data/Config/Run.asset");
            var run = runConfig != null ? runConfig.Rules : new Stealth.Game.RunSettings();
            int players = int.TryParse(System.Environment.GetEnvironmentVariable("AUDITPLAYERS"), out int p) ? Mathf.Max(1, p) : 1;
            var only = System.Environment.GetEnvironmentVariable("AUDITKINDS");
            // the game's own colours, points and loot table (as GameConfig hands them to the catalogue)
            var game = Resources.Load<GameConfig>("GameConfig");
            if (game != null && game.Items != null) ItemCatalog.Configure(game.Items.Colors, game.Items.PointsPerCell);
            ItemCatalog.ConfigureLoot(config.Loot.Table);
            int pack = 0;
            for (int y = 0; y < InventoryLayout.Height; y++)
                for (int x = 0; x < InventoryLayout.Width; x++)
                    if (InventoryLayout.IsCell(x, y)) pack++;
            int belt = PlayerInventory.BeltSize * PlayerInventory.BeltSize;
            var sb = new StringBuilder();
            sb.AppendLine($"backpack {pack} cells, belt bag {belt} cells, {ItemCatalog.PointsPerCell} points a grey cell; team of {players}; " +
                          $"quotas {string.Join(", ", Enumerable.Range(0, 5).Select(h => Stealth.Game.RunState.QuotaFor(h, 0, run, players)))}; loot guaranteed × {run.LootPerQuota}");
            sb.AppendLine($"loot settings: BaseItems {config.Loot.BaseItems}, ItemsPerHouse {config.Loot.ItemsPerHouse}, one more per {config.Loot.RoomsPerExtraItem} hundred m2 of rooms, " +
                          $"in storage {config.Loot.InStorageChance:0.00}, on the floor {config.Loot.FloorShare:0.00}, items per old item {ItemCatalog.ItemsPerOldItem():0.00}");
            foreach (var kind in Kinds)
            {
                if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(kind.ToString())) continue;
                var specs = new List<TestHouses.Spec>();
                for (int h = 0; h < Houses; h++)
                    for (int s = 0; s < Seeds; s++)
                        specs.Add(new TestHouses.Spec(HouseKinds.SeedOf(kind, h, 2000 + 53 * s), h, config.Building, config.Security, players, config.Features));
                var all = TestHouses.Many(specs);
                for (int h = 0; h < Houses; h++)
                {
                    var m = new SortedDictionary<string, List<float>>(System.StringComparer.Ordinal);
                    void Add(string k, float v) { if (!m.TryGetValue(k, out var list)) m[k] = list = new(); list.Add(v); }
                    var things = new Dictionary<string, (int count, int value, int cells)>();
                    foreach (var l in all.Where(l => l.LevelIndex == h)) Measure(l, Add, things, config, run, pack + belt);
                    sb.AppendLine($"=== {kind}, house {h} ({Seeds} seeds)");
                    foreach (var (k, v) in m) sb.AppendLine($"  {k,-58} avg {v.Average(),8:0.00}  min {v.Min(),8:0.00}  max {v.Max(),8:0.00}");
                    int n = things.Values.Sum(t => t.count);
                    sb.AppendLine("  things: " + string.Join(", ", things.OrderByDescending(t => t.Value.count).Take(14)
                        .Select(t => $"{t.Key} {100f * t.Value.count / n:0}% ({(float)t.Value.value / t.Value.count:0} pts, {(float)t.Value.cells / t.Value.count:0.#} cells)")));
                }
            }
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Logs", "loot_audit.txt"), sb.ToString());
        }

        static void Measure(LevelLayout l, System.Action<string, float> add, Dictionary<string, (int count, int value, int cells)> things,
            LevelConfig config, Stealth.Game.RunSettings run, int carry)
        {
            var rooms = l.Plan.Rooms;
            float area = rooms.Sum(r => r.Area);
            int quota = Stealth.Game.RunState.QuotaFor(l.LevelIndex, 0, run, l.Players);
            float want = quota * run.LootPerQuota;
            var plain = LootPlanner.Plan(l, new System.Random(l.Seed), config.Loot);
            var loot = LootPlanner.Plan(l, new System.Random(l.Seed), config.Loot, want);
            int value = LootPlanner.ValueOf(loot), cells = loot.Sum(t => ItemCatalog.Cells(t.Shape));
            add("01 rooms", rooms.Count);
            add("02 floors", l.Floors);
            add("03 area, m2", area);
            add("04 quota", quota);
            add("05 loot: things", loot.Count);
            add("06 loot: points", value);
            add("07 loot: points before the guarantee", LootPlanner.ValueOf(plain));
            add("08 loot: points per quota", value / (float)quota);
            add("09 points a thing", value / (float)Mathf.Max(1, loot.Count));
            add("10 cells a thing", cells / (float)Mathf.Max(1, loot.Count));
            add("11 points a cell of the backpack", value / (float)Mathf.Max(1, cells));
            // what a full backpack and belt bag carry: at the loot's own average, and of the richest things by the cell
            float perCell = value / (float)Mathf.Max(1, cells);
            add("12 a full backpack + belt, points (average loot)", carry * perCell);
            int left = carry, best = 0;
            foreach (var t in loot.OrderByDescending(t => ItemCatalog.Value(t.Shape, t.Color) / (float)ItemCatalog.Cells(t.Shape)))
            {
                int c = ItemCatalog.Cells(t.Shape);
                if (c > left) continue;
                left -= c;
                best += ItemCatalog.Value(t.Shape, t.Color);
            }
            add("13 a full backpack + belt, points (the richest things)", best);
            add("14 trips to carry the quota (average loot)", quota / Mathf.Max(1f, carry * perCell));
            add("15 share of the house's things a quota takes", quota / (float)Mathf.Max(1, value));
            // how thin it lies
            var with = new HashSet<int>();
            int behind = 0, hidden = 0;
            var regions = LootPlanner.KeyRegions(l);
            foreach (var t in loot)
            {
                var room = l.Plan.RoomAt(t.Position + Vector3.up * 0.3f);
                if (room == null) continue;
                with.Add(room.Id);
                if (regions.Any(r => r.Contains(room.Id))) behind += ItemCatalog.Value(t.Shape, t.Color);
                if (room.Id == l.HiddenRoom) hidden += ItemCatalog.Value(t.Shape, t.Color);
            }
            add("16 share of rooms with a thing", with.Count / (float)rooms.Count);
            add("17 rooms to search for the quota (all things found)", rooms.Count * quota / (float)Mathf.Max(1, value));
            add("18 points per room", value / (float)rooms.Count);
            add("19 points per 100 m2", 100f * value / area);
            add("20 m2 of floor per thing", area / Mathf.Max(1, loot.Count));
            add("21 share of points behind key doors", behind / (float)Mathf.Max(1, value));
            add("22 share of points in the hidden room", hidden / (float)Mathf.Max(1, value));
            foreach (var t in loot)
            {
                string name = ItemCatalog.ShortName(t.Shape);
                things.TryGetValue(name, out var was);
                things[name] = (was.count + 1, was.value + ItemCatalog.Value(t.Shape, t.Color), was.cells + ItemCatalog.Cells(t.Shape));
            }
        }
    }
}
