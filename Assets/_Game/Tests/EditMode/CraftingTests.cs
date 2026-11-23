using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The car's workbench: the run's recipes, «combine», blueprints, and what the made things do.</summary>
    public class CraftingTests
    {
        static List<InventoryEntry> Bench(params byte[] shapes)
        {
            var list = new List<InventoryEntry>();
            var placed = new List<(byte x, byte y, byte rot)>();
            Assert.IsTrue(BenchRules.Pack(shapes, CraftBook.BenchSize, CraftBook.BenchSize, placed));
            for (int i = 0; i < shapes.Length; i++)
                list.Add(new InventoryEntry { Shape = shapes[i], Color = ItemCatalog.FreshColor(shapes[i], 0), X = placed[i].x, Y = placed[i].y, Rot = placed[i].rot });
            return list;
        }

        [Test]
        public void EveryRunHasARecipeForEachThingAndTheyDiffer()
        {
            var book = new CraftBook(7, null);
            Assert.AreEqual(CraftBook.Results.Length, book.Recipes.Count);
            var keys = new HashSet<string>();
            for (int i = 0; i < book.Recipes.Count; i++)
            {
                var r = book.Recipes[i];
                Assert.AreEqual(CraftBook.Results[i], r.Result);
                Assert.GreaterOrEqual(r.Ingredients.Length, 2, "two things at least");
                Assert.IsTrue(keys.Add(string.Join(",", r.Ingredients)), "no two recipes take the same things");
                Assert.AreEqual(r.Ingredients.Length, r.Ingredients.Distinct().Count(), "a thing goes in once");
                Assert.IsTrue(BenchRules.Pack(r.Ingredients, CraftBook.BenchSize, CraftBook.BenchSize, null), "it all fits the bench");
                byte base_ = CraftBook.BaseOf(r.Result);
                if (base_ != byte.MaxValue) Assert.Contains(base_, r.Ingredients, "the upgrade is always made from its base");
                foreach (var s in r.Ingredients) Assert.IsFalse(ItemCatalog.IsCrafted(s), "nothing made goes into a recipe");
            }
        }

        [Test]
        public void TheRecipesAreTheSameForASeedAndChangeWithIt()
        {
            string Of(int seed) => string.Join("|", new CraftBook(seed, null).Recipes.Select(r => string.Join(",", r.Ingredients)));
            Assert.AreEqual(Of(42), Of(42));
            int differ = Enumerable.Range(1, 8).Count(s => Of(s) != Of(42));
            Assert.Greater(differ, 6, "every run its own recipes");
        }

        [Test]
        public void AMatchingBenchMakesItsThingAndTheOrderDoesNotMatter()
        {
            var book = new CraftBook(3, null);
            var r = book.Recipes[0];
            var bench = Bench(r.Ingredients.Reverse().ToArray());
            var o = BenchRules.Combine(bench, book, new System.Random(1), 1f);
            Assert.AreEqual(0, o.Recipe);
            Assert.AreEqual(-1, o.Broken, "a recipe breaks nothing");
            // one thing more or less is no recipe
            var more = Bench(r.Ingredients.Concat(new[] { ItemCatalog.Wires }).ToArray());
            Assert.AreEqual(-1, BenchRules.Combine(more, book, new System.Random(1), 0f).Recipe);
        }

        [Test]
        public void AFailedCombineBreaksOneThingAboutHalfTheTime()
        {
            var book = new CraftBook(3, null);
            var bench = Bench(ItemCatalog.Wires, ItemCatalog.Chip);
            int broke = 0;
            for (int i = 0; i < 400; i++)
            {
                var o = BenchRules.Combine(bench, book, new System.Random(i), 0.5f);
                Assert.AreEqual(-1, o.Recipe);
                if (o.Broken >= 0) { broke++; Assert.Less(o.Broken, bench.Count); }
            }
            Assert.That(broke, Is.InRange(150, 250));
            Assert.IsFalse(BenchRules.CanCombine(1), "one thing is nothing to combine");
        }

        [Test]
        public void TheResultShowsOnlyOnceTheTeamHasMadeIt()
        {
            var book = new CraftBook(5, null);
            var bench = Bench(book.Recipes[2].Ingredients);
            Assert.AreEqual(-1, BenchRules.KnownResult(bench, book, 0), "result unknown");
            Assert.AreEqual(2, BenchRules.KnownResult(bench, book, 1 << 2));
            Assert.AreEqual(-1, BenchRules.KnownResult(Bench(ItemCatalog.Chip, ItemCatalog.Wires), book, ~0), "no recipe: nothing to show");
        }

        [Test]
        public void ABlueprintIsWorthMoreTheMoreItTakes()
        {
            var book = new CraftBook(11, null);
            for (int i = 0; i < book.Recipes.Count; i++)
                Assert.AreEqual(Mathf.Clamp(book.Size(i) - 1, 1, 3), book.BlueprintRarity(i));
            CraftBook.Use(11, null);
            int small = Enumerable.Range(0, book.Recipes.Count).OrderBy(book.Size).First();
            int big = Enumerable.Range(0, book.Recipes.Count).OrderBy(book.Size).Last();
            if (book.Size(small) < book.Size(big))
                Assert.Less(ItemCatalog.Value(ItemCatalog.Blueprint, small), ItemCatalog.Value(ItemCatalog.Blueprint, big));
            Assert.AreEqual((byte)4, ItemCatalog.FreshColor(ItemCatalog.Blueprint, 4), "its colour byte is its recipe");
            Assert.IsTrue(ItemCatalog.IsLoot(ItemCatalog.Blueprint), "a blueprint burns into fuel");
        }

        [Test]
        public void TheMadeThingsWorkAsWhatTheyAreMadeFrom()
        {
            Assert.IsTrue(ItemCatalog.Works(ItemCatalog.ProScrewdriver, ItemCatalog.Screwdriver));
            Assert.IsTrue(ItemCatalog.Works(ItemCatalog.ProKeyboard, ItemCatalog.Keyboard));
            Assert.IsTrue(ItemCatalog.Works(ItemCatalog.ProTablet, ItemCatalog.Tablet));
            Assert.IsTrue(ItemCatalog.Works(ItemCatalog.Espresso, ItemCatalog.Coffee));
            Assert.IsTrue(ItemCatalog.Works(ItemCatalog.JumpBoots, ItemCatalog.Boots));
            Assert.IsFalse(ItemCatalog.Works(ItemCatalog.Screwdriver, ItemCatalog.ProScrewdriver), "not the other way round");
            for (int s = ItemCatalog.FirstCrafted; s <= ItemCatalog.LastCrafted; s++)
            {
                Assert.IsTrue(ItemCatalog.IsTool(s), ItemCatalog.Shapes[s].Id + ": a tool (not fed to the hopper with the loot)");
                Assert.GreaterOrEqual(ItemCatalog.Rarity(s), 1, ItemCatalog.Shapes[s].Id + ": the rarity its blueprint shows");
                Assert.Greater(ItemCatalog.Value(s, 0), ItemCatalog.Cells(s) * ItemCatalog.PointsPerCell, "burns for its rarity, not at the grey rate");
            }
        }

        [Test]
        public void TheChessPiecesAreRarerAndDearerTheHigherInChess()
        {
            for (int k = 1; k < 6; k++)
            {
                Assert.GreaterOrEqual(ItemCatalog.Value(ItemCatalog.ChessPiece(k, false), 0), ItemCatalog.Value(ItemCatalog.ChessPiece(k - 1, false), 0));
                Assert.AreEqual(ItemCatalog.Value(ItemCatalog.ChessPiece(k, false), 0), ItemCatalog.Value(ItemCatalog.ChessPiece(k, true), 0), "black as white");
            }
            var s = new CraftingSettings();
            var rng = new System.Random(3);
            var counts = new int[6];
            int black = 0;
            for (int i = 0; i < 2000; i++)
            {
                byte p = CraftedRules.ChessPiece(rng, s.ChessWeights);
                Assert.IsTrue(ItemCatalog.IsChess(p));
                counts[ItemCatalog.ChessKind(p)]++;
                if ((p - ItemCatalog.FirstChess) % 2 == 1) black++;
            }
            Assert.Greater(counts[0], counts[3]);
            Assert.Greater(counts[3], counts[5]);
            Assert.That(black, Is.InRange(850, 1150));
        }

        [Test]
        public void TheCraftedRulesGiveWhatTheThingsPromise()
        {
            var s = new CraftedSettings();
            Assert.AreEqual(2, CraftedRules.HackSlots(2, false, s));
            Assert.AreEqual(3, CraftedRules.HackSlots(2, true, s), "the upgraded keyboard: three hacks instead of two");
            Assert.AreEqual(15f, new GasSettings().Limit, "the gas takes you in 15 s now");
            Assert.AreEqual(60f, CraftedRules.GasLimit(15f, true, s), "with a gas mask: a minute");
            Assert.AreEqual(15f, CraftedRules.GasLimit(15f, false, s));
            // the jump boots reach a balcony of the second floor: 4.1 m up, its parapet 0.75 m over that
            Assert.Greater(CraftedRules.JumpHeight(1.05f, true, s), LevelMetrics.FloorHeight + 0.75f);
            Assert.AreEqual(1.05f, CraftedRules.JumpHeight(1.05f, false, s));
            Assert.IsTrue(CraftedRules.Rams(true, true, 0.5f, s));
            Assert.IsFalse(CraftedRules.Rams(false, true, 0.5f, s), "walking into it is no ram");
            Assert.IsFalse(CraftedRules.Rams(true, false, 0.5f, s), "plain coffee is no ram");
            Assert.AreEqual(10f, s.RamStun);
            // once per house: the mark travels with the screwdriver
            Assert.IsFalse(CraftedRules.IsUsedIn(0, 3));
            Assert.IsTrue(CraftedRules.IsUsedIn(CraftedRules.UsedIn(3), 3));
            Assert.IsFalse(CraftedRules.IsUsedIn(CraftedRules.UsedIn(3), 4), "a new house: usable again");
            for (int seed = -3; seed < 600; seed += 7) Assert.AreNotEqual(0, CraftedRules.UsedIn(seed), "0 is never used");
            Assert.IsFalse(CraftedRules.IsUsedIn(ItemCatalog.FreshColor(ItemCatalog.ProScrewdriver, 0), 1), "a made screwdriver is not used yet, in any house");
            for (int c = ItemCatalog.FirstCrafted; c <= ItemCatalog.LastCrafted; c++) Assert.AreEqual(0, ItemCatalog.FreshColor(c, 3));
            var drops = CraftedRules.UnscrewDrops(new System.Random(1), s);
            Assert.That(drops.Count, Is.InRange(s.UnscrewDrops.x, s.UnscrewDrops.y));
            Assert.IsTrue(drops.All(d => d is ItemCatalog.Circuit or ItemCatalog.Wires or ItemCatalog.Chip));
        }

        [Test]
        public void TheUpgradedFinderIsAFinderMadeFromOne()
        {
            Assert.IsTrue(ItemCatalog.IsCrafted(ItemCatalog.ProFinder));
            Assert.IsTrue(ItemCatalog.Works(ItemCatalog.ProFinder, ItemCatalog.Finder));
            Assert.IsTrue(ItemCatalog.Holdable(ItemCatalog.ProFinder));
            var book = new CraftBook(7, null);
            var r = book.Recipes[System.Array.IndexOf(CraftBook.Results, ItemCatalog.ProFinder)];
            Assert.AreEqual(ItemCatalog.ProFinder, r.Result);
            CollectionAssert.Contains(r.Ingredients, ItemCatalog.Finder, "always made from a fuel finder");
        }

        [Test]
        public void TheUpgradedFindersRingPointsTheWayToTheLoot()
        {
            var s = new CraftedSettings();
            var toLoot = new Vector3(3f, 0.5f, 3f);
            float dist = toLoot.magnitude;
            Assert.AreEqual(1f, CraftedRules.DialGlow(new Vector3(1f, 0.2f, 1f), toLoot, dist, s), 1e-4f, "the lamp that points at it: full");
            Assert.AreEqual(0f, CraftedRules.DialGlow(new Vector3(-1f, 0f, -1f), toLoot, dist, s), "the opposite lamp: dark");
            Assert.AreEqual(0f, CraftedRules.DialGlow(new Vector3(1f, 0f, 1f), toLoot, s.FinderRange + 1f, s), "out of range: dark");
            Assert.AreEqual(0f, CraftedRules.DialGlow(new Vector3(1f, 0f, 1f), toLoot, float.PositiveInfinity, s), "no loot: dark");
            // a lamp 22.5° off (the next of sixteen): dark when the loot is near, lit when it is far — only the general way
            var next = Quaternion.Euler(0f, 22.5f, 0f) * new Vector3(1f, 0f, 1f);
            Assert.AreEqual(0f, CraftedRules.DialGlow(next, toLoot, 1f, s), "near: one lamp");
            Assert.Greater(CraftedRules.DialGlow(next, toLoot, s.FinderRange * 0.9f, s), 0.5f, "far: its neighbours too");
        }

        [Test]
        public void TheRobotSensorSeesMovingRobotsAheadOfIt()
        {
            var s = new CraftedSettings();
            Assert.IsTrue(CraftedRules.SensorSees(new Vector3(0f, 0.3f, 5f), Vector3.forward, 1f, s));
            Assert.IsFalse(CraftedRules.SensorSees(new Vector3(0f, 0.3f, 5f), Vector3.forward, 0.1f, s), "standing still");
            Assert.IsFalse(CraftedRules.SensorSees(new Vector3(0f, 0.3f, -5f), Vector3.forward, 1f, s), "behind it");
            Assert.IsFalse(CraftedRules.SensorSees(new Vector3(0f, 0f, s.SensorRange + 1f), Vector3.forward, 1f, s), "too far");
        }

        [Test]
        public void TheBenchIsAFourByFourGrid()
        {
            var bench = new TeamStash(CraftBook.BenchSize, CraftBook.BenchSize);
            Assert.IsTrue(bench.CanPlace(ItemCatalog.Wires, 0, 3, 3));
            Assert.IsFalse(bench.CanPlace(ItemCatalog.Wires, 0, 4, 0));
            Assert.IsFalse(bench.CanPlace(ItemCatalog.Hammer, 0, 0, 1), "a hammer stands four cells tall");
            Assert.IsTrue(bench.CanPlace(ItemCatalog.Hammer, 0, 0, 0));
        }

        [Test]
        public void AHoleIsCutRoundAndItsKeyRoundTrips()
        {
            var parts = FloorHoles.Around(new Rect(0, 0, 10, 6), new Rect(4, 2, 1, 1));
            Assert.AreEqual(4, parts.Count);
            Assert.AreEqual(60f - 1f, parts.Sum(r => r.width * r.height), 1e-3f, "everything but the hole");
            foreach (var a in parts) Assert.IsFalse(a.Overlaps(new Rect(4.01f, 2.01f, 0.98f, 0.98f)));
            Assert.AreEqual(1, FloorHoles.Around(new Rect(0, 0, 2, 2), new Rect(5, 5, 1, 1)).Count, "apart: untouched");
            int key = OpenKeys.Hole(3, 517, 42);
            Assert.AreEqual(OpenKind.Hole, OpenKeys.KindOf(key));
            Assert.AreEqual((3, 517, 42), OpenKeys.HoleOf(key));
            Assert.AreEqual(1, FloorHoles.FloorAt(LevelMetrics.FloorHeight + 0.02f));
            Assert.AreEqual(-1, FloorHoles.FloorAt(LevelMetrics.FloorHeight + 1.5f));
        }

        [Test]
        public void TheHammerOnlyBreaksAnOpenBitOfAnUpperFloor()
        {
            var l = LevelGenerator.Generate(7, 4);
            Assert.IsFalse(FloorHoles.CanBreak(l, 0, new RectInt(10, 10, 2, 2)), "not the ground floor");
            int found = 0;
            for (int f = 1; f < l.Floors && found == 0; f++)
                foreach (var room in l.Plan.Rooms)
                {
                    if (room.Floor != f) continue;
                    var c = HousePlan.CellOf(new Vector2(room.Center.x, room.Center.z));
                    var cells = new RectInt(c.x - 1, c.y - 1, 2, 2);
                    if (!FloorHoles.CanBreak(l, f, cells)) continue;
                    found++;
                    // never through a wall: every cell over one room, above and below
                    int a = l.Plan.IdAt(f, cells.x, cells.y), b = l.Plan.IdAt(f - 1, cells.x, cells.y);
                    for (int z = cells.yMin; z < cells.yMax; z++)
                        for (int x = cells.xMin; x < cells.xMax; x++)
                        {
                            Assert.AreEqual(a, l.Plan.IdAt(f, x, z));
                            Assert.AreEqual(b, l.Plan.IdAt(f - 1, x, z));
                        }
                    Assert.IsFalse(FloorHoles.CanBreak(l, f, cells, new[] { (f, cells) }), "not twice in one place");
                    break;
                }
            Assert.Greater(found, 0, "somewhere in an upper floor a hole may be knocked");
            // never a way round a lock: not into the vault, the hidden room or a room behind a key door
            for (int f = 1; f < l.Floors; f++)
                for (int z = 0; z < l.Plan.NZ; z += 2)
                    for (int x = 0; x < l.Plan.NX; x += 2)
                    {
                        var cells = new RectInt(x, z, 2, 2);
                        if (!FloorHoles.CanBreak(l, f, cells)) continue;
                        int below = l.Plan.IdAt(f - 1, x, z);
                        Assert.AreNotEqual(l.VaultRoom, below);
                        Assert.AreNotEqual(l.HiddenRoom, below);
                        Assert.IsFalse(l.KeyDoors.Any(k => k.Rooms.Contains(below)));
                    }
        }

        [Test]
        public void TheExtrasLieOnFreeSpots()
        {
            var l = LevelGenerator.Generate(7, 3);
            var loot = LootPlanner.Plan(l, new System.Random(1));
            var spots = LootPlanner.Extras(l, new System.Random(2), 4, loot, 0.5f);
            Assert.AreEqual(4, spots.Count);
            foreach (var p in spots) Assert.IsFalse(loot.Exists(t => (t.Position - p).sqrMagnitude < 0.25f), "not on a loot's spot");
        }
    }
}
