using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The classes, the starting items and the junk: fixed rarities, the coffee, hard landings, the fuel finder, the start
    /// screen's row, the house's clock and the titles of the drive.
    /// </summary>
    public class GearTests
    {
        [Test]
        public void EveryThingHasItsOwnFixedRarity()
        {
            Assert.AreEqual(4, ItemCatalog.Rarity(ItemCatalog.FirstJunk + 11), "the necklace is gold");
            Assert.AreEqual(0, ItemCatalog.Rarity(ItemCatalog.FirstJunk + 8), "the apple core is grey");
            for (int shape = ItemCatalog.FirstJunk; shape <= ItemCatalog.LastJunk; shape++)
            {
                Assert.IsTrue(ItemCatalog.IsLoot(shape) && ItemCatalog.IsJunk(shape) && !ItemCatalog.IsTool(shape), ItemCatalog.Shapes[shape].Id);
                Assert.GreaterOrEqual(ItemCatalog.Rarity(shape), 0, ItemCatalog.Shapes[shape].Id + " has no rarity");
                // whatever colour was rolled, it spawns with its own and is worth its own
                for (int rolled = 0; rolled < ItemCatalog.Colors.Length; rolled++)
                {
                    Assert.AreEqual(ItemCatalog.Rarity(shape), ItemCatalog.FreshColor(shape, rolled));
                    Assert.AreEqual(ItemCatalog.Value(shape, ItemCatalog.Rarity(shape)), ItemCatalog.Value(shape, rolled));
                }
            }
            Assert.AreEqual(ItemCatalog.FullCharge, ItemCatalog.FreshColor(ItemCatalog.Flashlight, 2), "a flashlight still comes charged");
            Assert.AreEqual(3, ItemCatalog.FreshColor(ItemCatalog.Diamond, 3), "a diamond keeps the workbench's colour");
        }

        [Test]
        public void TheJunkTakesDifferentShapes()
        {
            var shapes = new HashSet<string>();
            for (int shape = ItemCatalog.FirstJunk; shape <= ItemCatalog.LastJunk; shape++)
                shapes.Add(string.Join(";", ItemCatalog.Shapes[shape].Cells.Select(c => c.ToString())));
            Assert.GreaterOrEqual(shapes.Count, 5);
        }

        [Test]
        public void ClassToolsAndStartingItemsAreToolsInReach()
        {
            Assert.AreEqual(4, ItemCatalog.ClassTools.Length);
            Assert.AreEqual(5, ItemCatalog.StartItems.Length);
            foreach (var t in ItemCatalog.ClassTools) Assert.IsTrue(ItemCatalog.Holdable(t) && ItemCatalog.IsTool(t), ItemCatalog.Shapes[t].Id);
            foreach (var t in ItemCatalog.StartItems) Assert.IsTrue(ItemCatalog.IsTool(t) || t == ItemCatalog.Flashlight, ItemCatalog.Shapes[t].Id);
            Assert.IsTrue(ItemCatalog.Consumable(ItemCatalog.Medkit) && ItemCatalog.Consumable(ItemCatalog.Coffee));
            Assert.IsFalse(ItemCatalog.Holdable(ItemCatalog.Boots), "the boots are worn (carried), not held");
        }

        [Test]
        public void TheStartingItemsAndClassToolsLieInTheHousesToo()
        {
            var table = Resources.Load<Configs.GameConfig>(Configs.GameConfig.ResourcePath).Level.Loot.Table.Select(r => r.Item).ToList();
            foreach (var t in ItemCatalog.ClassTools.Concat(ItemCatalog.StartItems))
                Assert.Contains(ItemCatalog.Shapes[t].Id, table, "not found in houses");
            for (int shape = ItemCatalog.FirstJunk; shape <= ItemCatalog.LastJunk; shape++)
                Assert.Contains(ItemCatalog.Shapes[shape].Id, table);
        }

        [Test]
        public void AHardLandingSlowsAndAHigherOneHurtsButNotInTheBoots()
        {
            var s = new GadgetSettings();
            Assert.AreEqual((1f, false), GearRules.Landing(2.5f, false, s), "a jump off a desk");
            var (slow, hurt) = GearRules.Landing(3.5f, false, s);
            Assert.Less(slow, 1f);
            Assert.IsFalse(hurt);
            var (slower, hurts) = GearRules.Landing(7f, false, s);
            Assert.Less(slower, slow, "higher: slower");
            Assert.IsTrue(hurts);
            Assert.AreEqual((1f, false), GearRules.Landing(20f, true, s), "the boots take any fall");
            Assert.AreEqual(1f, GearRules.SlowNow(0.5f, s.FallSlowSeconds, s.FallSlowSeconds), 1e-4f, "worn off after the slow");
            Assert.AreEqual(3f, GearRules.FallHeight(Mathf.Sqrt(2f * 9.81f * 3f)), 1e-3f);
        }

        [Test]
        public void TheFinderBlinksFasterNearLoot()
        {
            var s = new GadgetSettings();
            float near = GearRules.FinderPeriod(1f, s), mid = GearRules.FinderPeriod(12f, s), far = GearRules.FinderPeriod(100f, s);
            Assert.Less(near, mid);
            Assert.Less(mid, far);
            Assert.Greater(GearRules.FinderPeriod(float.PositiveInfinity, s), far, "nothing left: it barely blinks");
            Assert.Less(GearRules.FinderReading(10f, 1f, s), GearRules.FinderReading(10f, 0f, s), "ahead reads nearer than aside");
            Assert.Less(GearRules.FinderReading(10f, 0f, s), GearRules.FinderReading(10f, -1f, s), "aside nearer than behind");
            Assert.AreEqual(10f, GearRules.FinderReading(10f, 1f, s), 1e-4f, "straight ahead: just the distance");
            Assert.AreEqual(10f * s.FinderBehind, GearRules.FinderReading(10f, -1f, s), 1e-3f);
            Assert.IsTrue(GearRules.FinderLit(0.01f, 1f));
            Assert.IsFalse(GearRules.FinderLit(0.5f, 1f));
        }

        [Test]
        public void ASnifferStopsNowAndThen()
        {
            Assert.IsFalse(RobotBrain.Sniffing(1f, 3f, 1f), "walks first");
            Assert.IsTrue(RobotBrain.Sniffing(3.5f, 3f, 1f), "then sniffs");
            Assert.IsFalse(RobotBrain.Sniffing(4.5f, 3f, 1f), "walks again");
            Assert.IsTrue(RobotBrain.Sniffing(7.2f, 3f, 1f), "and sniffs again");
            Assert.IsFalse(RobotBrain.Sniffing(100f, 0f, 1f), "0: never stops");
        }

        [Test]
        public void TheTeamStandsInARowFacingTheCamera()
        {
            var car = new Vector3(10f, 0f, 5f);
            var rot = Quaternion.Euler(0f, 90f, 0f);
            var (cam, look) = PickingRules.Camera(car, rot);
            var spots = Enumerable.Range(0, 4).Select(i => PickingRules.Spot(car, rot, i, 4)).ToArray();
            for (int i = 0; i < 4; i++)
            {
                var toCam = cam - spots[i].at;
                toCam.y = 0f;
                Assert.Less(Vector3.Angle(Quaternion.Euler(0f, spots[i].yaw, 0f) * Vector3.forward, toCam), 35f, "faces the camera");
                Assert.Greater(Vector3.Dot(look * Vector3.forward, spots[i].at - cam), 0f, "in front of the camera");
                if (i > 0) Assert.AreEqual(PickingRules.Gap, Vector3.Distance(spots[i].at, spots[i - 1].at), 1e-3f);
            }
            // left to right as the camera sees them
            var right = look * Vector3.right;
            Assert.Greater(Vector3.Dot(spots[3].at - spots[0].at, right), 0f);
        }

        [Test]
        public void ASingleFigureStandsBigInTheMiddleWithRoomForItsColumnUnderIt()
        {
            var car = new Vector3(-3f, 0f, 8f);
            var rot = Quaternion.Euler(0f, 30f, 0f);
            var (cam, look) = PickingRules.Camera(car, rot);
            var (at, _) = PickingRules.Spot(car, rot, 0, 1);
            Vector2 View(Vector3 p)
            {
                var l = Quaternion.Inverse(look) * (p - cam);
                float t = Mathf.Tan(PickingRules.Fov * 0.5f * Mathf.Deg2Rad);
                return new Vector2(0.5f + l.x / (l.z * 2f * t * 16f / 9f), 0.5f + l.y / (l.z * 2f * t));
            }
            var feet = View(at);
            var head = View(at + Vector3.up * 1.8f);
            Assert.AreEqual(0.5f, feet.x, 1e-3f, "alone: in the middle");
            Assert.Greater(head.y - feet.y, 0.45f, "big on the screen");
            Assert.Less(head.y, 0.95f, "the head is in the frame");
            Assert.Greater(feet.y, 0.34f, "the column of the class and the item fits under the feet, above the ready button");
            // four: the outer ones are in the frame too
            for (int i = 0; i < 4; i++)
            {
                float x = View(PickingRules.Spot(car, rot, i, 4).at).x;
                Assert.IsTrue(x > 0.12f && x < 0.88f, $"figure {i} at {x}");
            }
        }

        [Test]
        public void EachClassDressesThePlayerAsItsOwnDepartment()
        {
            var depts = Enumerable.Range(0, ItemCatalog.ClassTools.Length).Select(Player.PlayerLooks.DepartmentOfClass).Distinct().Count();
            Assert.AreEqual(ItemCatalog.ClassTools.Length, depts, "four classes, four looks");
            Assert.AreEqual(Player.Department.Backend, Player.PlayerLooks.DepartmentOfClass(1), "the hacker with a keyboard: hoodie and headphones");
            // the clothes follow the class, the team colour and the face stay the slot's
            Assert.IsTrue(Player.PlayerLooks.PartColor("Body", 2, Player.Department.Backend, out var body));
            Assert.AreEqual(Player.PlayerLooks.DepartmentColors[(int)Player.Department.Backend], body);
            Assert.IsTrue(Player.PlayerLooks.PartColor("Accent", 2, Player.Department.Backend, out var team));
            Assert.AreEqual(Player.PlayerLooks.TeamColor(2), team);
            // before anyone picks, the team comes dressed differently
            Assert.AreEqual(4, Enumerable.Range(0, 4).Select(s => Player.PlayerLooks.DepartmentOfClass(Player.PlayerLooks.DefaultClass(s))).Distinct().Count());
        }

        [Test]
        public void TheClockCountsUpAndShowsWhenTheNextRobotComes()
        {
            Assert.AreEqual("0:00", HouseClockRules.Format(0f));
            Assert.AreEqual("2:05", HouseClockRules.Format(125.9f));
            Assert.AreEqual(180f, HouseClockRules.NextAt(0f, 180f, 120f, 60f), "at the start: the 3rd minute");
            Assert.AreEqual(180f, HouseClockRules.NextAt(120f, 180f, 120f, 60f), "two minutes in: still the 3rd minute");
            Assert.AreEqual(300f, HouseClockRules.NextAt(200f, 180f, 120f, 60f));
            Assert.AreEqual(360f, HouseClockRules.NextAt(300f, 180f, 120f, 60f));
            Assert.AreEqual(300f, HouseClockRules.PreviousAt(330f, 180f, 120f, 60f));
            Assert.AreEqual(0f, HouseClockRules.Heat(0));
            Assert.AreEqual(1f, HouseClockRules.Heat(20), "capped: it never gets redder than this");
            var calm = HouseClockRules.ColorOf(0f);
            var hot = HouseClockRules.ColorOf(1f);
            Assert.Greater(hot.r, calm.r);
            Assert.Less(hot.b, calm.b);
            Assert.Less(hot.r, 0.95f, "a red that does not hurt the eyes");
        }

        [Test]
        public void EveryoneGetsTheTitleTheyStandOutIn()
        {
            var s = new HouseStats();
            s.Add(1, StatKind.Fuel, 200);
            s.Add(2, StatKind.Fuel, 20);
            s.Add(2, StatKind.Downs, 3);
            s.Add(3, StatKind.Talk, 90);
            s.Add(3, StatKind.Fuel, 10);
            var titles = s.Titles(new ulong[] { 1, 2, 3, 4 });
            Assert.AreEqual(StatKind.Fuel, titles[0].Kind);
            Assert.AreEqual(StatKind.Downs, titles[1].Kind, "fuel is taken: what they stand out in next");
            Assert.AreEqual(StatKind.Talk, titles[2].Kind);
            Assert.AreEqual(StatKind.None, titles[3].Kind, "did nothing: the shadow");
            Assert.AreEqual(200, titles[0].Fuel);

            var alone = new HouseStats();
            alone.Add(7, StatKind.Distance, 900);
            alone.Add(7, StatKind.Revives, 0.5f);
            Assert.AreEqual(StatKind.Distance, alone.Titles(new ulong[] { 7 })[0].Kind, "alone: the most remarkable for one house");
        }

        [Test]
        public void TheTitlesTravelAsOneString()
        {
            var titles = new List<HouseTitle> { new() { Player = 5, Kind = StatKind.Falls, Value = 2, Fuel = 30, Downs = 1, Revives = 0, Metres = 412 } };
            var packed = HouseStats.Pack(3, titles);
            var back = new List<HouseTitle>();
            Assert.IsTrue(HouseStats.Unpack(packed, out int house, back));
            Assert.AreEqual(3, house);
            Assert.AreEqual(1, back.Count);
            Assert.AreEqual(5UL, back[0].Player);
            Assert.AreEqual(StatKind.Falls, back[0].Kind);
            Assert.AreEqual(412, back[0].Metres);
        }
    }
}
