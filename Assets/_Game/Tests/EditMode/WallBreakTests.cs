using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The breaches a driller knocks in walls (<see cref="WallBreaks"/>), the repairman's climb and the zones on the map.</summary>
    public class WallBreakTests
    {
        static IEnumerable<(int wall, float at)> Spots(LevelLayout l)
        {
            for (int i = 0; i < l.Plan.Walls.Count; i++)
            {
                var w = l.Plan.Walls[i];
                for (float at = 1f; at < w.Length - 1f; at += 0.5f)
                    if (WallBreaks.CanBreak(l, i, WallBreaks.Snap(at))) yield return (i, WallBreaks.Snap(at));
            }
        }

        [Test]
        public void HousesHaveWallsToBreakAndNeverTheLockedOnes()
        {
            int houses = 0, with = 0;
            foreach (var l in TestHouses.Corpus(8))
            {
                houses++;
                var spots = Spots(l).ToList();
                if (spots.Count > 0) with++;
                foreach (var (wall, at) in spots)
                {
                    var w = l.Plan.Walls[wall];
                    Assert.IsFalse(w.Exterior, $"seed {l.Seed}: never an outer wall");
                    Assert.IsFalse(w.Railing);
                    Assert.AreNotEqual(l.VaultRoom, w.RoomA, $"seed {l.Seed}: not into the vault");
                    Assert.AreNotEqual(l.VaultRoom, w.RoomB, $"seed {l.Seed}: not into the vault");
                    Assert.IsTrue(l.HiddenRoom < 0 || (w.RoomA != l.HiddenRoom && w.RoomB != l.HiddenRoom), $"seed {l.Seed}: not into the hidden room");
                    Assert.IsTrue(l.StorageRoom < 0 || (w.RoomA != l.StorageRoom && w.RoomB != l.StorageRoom), $"seed {l.Seed}: not into the storage room");
                    Assert.IsTrue(l.Plan.FreeOn(wall, at, LevelMetrics.OpeningWidth(EdgeType.Breach), 0.6f), "clear of the wall's ends and openings");
                }
            }
            Assert.GreaterOrEqual(with, houses / 2, "most houses have a wall a driller can go through");
        }

        [Test]
        public void ASecondBreachKeepsItsDistance()
        {
            var l = TestHouses.Corpus(8).First(h => Spots(h).Any());
            var (wall, at) = Spots(l).First();
            Assert.IsFalse(WallBreaks.CanBreak(l, wall, at, new[] { (wall, at) }), "not on top of the first");
            Assert.IsFalse(WallBreaks.CanBreak(l, wall, at + 0.5f, new[] { (wall, at) }));
        }

        [Test]
        public void TheWallAheadIsTheFirstOneOnTheWay()
        {
            var l = TestHouses.Corpus(8).First(h => Spots(h).Any());
            var (wall, at) = Spots(l).First();
            var w = l.Plan.Walls[wall];
            var mid = w.At(at);
            var normal = new Vector2(-w.Dir.y, w.Dir.x);
            float y = LevelLayout.FloorY(w.Floor);
            var from = new Vector3(mid.x + normal.x * 1f, y, mid.y + normal.y * 1f);
            var to = new Vector3(mid.x - normal.x * 2f, y, mid.y - normal.y * 2f);
            Assert.IsTrue(WallBreaks.Ahead(l, from, to, 3f, out int found, out float where));
            Assert.AreEqual(wall, found);
            Assert.AreEqual(at, where, 0.11f);
            Assert.IsFalse(WallBreaks.Ahead(l, from, from + new Vector3(normal.x, 0f, normal.y) * 0.4f, 3f, out _, out _), "walking away from it: nothing on a way that short");
        }

        [Test]
        public void TheKeyHoldsTheWallAndTheSpot()
        {
            foreach (var (wall, at) in new[] { (0, 0f), (17, 3.4f), (4095, 61.3f), (8191, 102.3f) })
            {
                int key = OpenKeys.WallBreak(wall, at);
                Assert.AreEqual(OpenKind.Wall, OpenKeys.KindOf(key));
                var (w, a) = OpenKeys.WallBreakOf(key);
                Assert.AreEqual(wall, w);
                Assert.AreEqual(at, a, 0.001f);
            }
            Assert.AreEqual(2.3f, WallBreaks.Snap(2.34f), 0.001f);
        }

        [Test]
        public void AWallSpanIsSplitRoundTheGap()
        {
            var parts = WallBreaks.Around(0f, 10f, 4f, 5.2f);
            CollectionAssert.AreEqual(new[] { (0f, 4f), (5.2f, 10f) }, parts);
            CollectionAssert.AreEqual(new[] { (0f, 3f) }, WallBreaks.Around(0f, 3f, 4f, 5f), "a span beside the gap stays whole");
            CollectionAssert.AreEqual(new[] { (5f, 8f) }, WallBreaks.Around(4.5f, 8f, 4f, 5f), "a span that starts in the gap keeps what is past it");
            Assert.AreEqual(0, WallBreaks.Around(4.2f, 4.8f, 4f, 5f).Count, "a span inside the gap is gone");
        }

        [Test]
        public void ADrillerBreaksThroughOnlyWhenTheWayRoundIsLong()
        {
            Assert.IsFalse(RoleRules.WorthBreaking(6f, 8f, false), "the way round is short: it walks");
            Assert.IsTrue(RoleRules.WorthBreaking(6f, 30f, false));
            Assert.IsTrue(RoleRules.WorthBreaking(6f, 6f, noWay: true), "no way at all: through the wall");
            Assert.IsFalse(RoleRules.WorthBreaking(40f, 200f, false), "its prey is far: not this wall");
        }

        [Test]
        public void ARepairmanClimbsToHighWorkOnly()
        {
            Assert.IsFalse(RoleRules.Climbs(0.4f), "a grille by the floor");
            Assert.IsTrue(RoleRules.Climbs(2.6f), "a grate in the ceiling");
            Assert.Greater(RoleRules.WorkTime(true), RoleRules.WorkTime(false));
            Assert.AreEqual(0f, RoleRules.ClimbEase(0f));
            Assert.AreEqual(1f, RoleRules.ClimbEase(1f));
        }

        [Test]
        public void TheMapWashesSeenYellowAndRedRoomsInTheirColour()
        {
            var l = TestHouses.Corpus(8).First(h => h.Zones.Contains(DangerZone.Red) && h.Zones.Contains(DangerZone.Yellow));
            var map = new ExplorationMap(l);
            map.RevealAll();
            var rects = new List<MapRect>();
            bool yellow = false, red = false;
            for (int f = 0; f < l.Floors; f++)
            {
                MapSketch.Build(map, f, null, rects);
                yellow |= rects.Exists(r => r.Part == MapPart.ZoneYellow);
                red |= rects.Exists(r => r.Part == MapPart.ZoneRed);
                foreach (var r in rects)
                {
                    if (r.Part is not (MapPart.ZoneYellow or MapPart.ZoneRed)) continue;
                    var c = r.Area.center;
                    var room = l.Plan.RoomAt(new Vector3(c.x, LevelLayout.FloorY(f) + 0.3f, c.y));
                    if (room == null) continue; // (a rectangle merged round a corner: its middle may fall outside)
                    Assert.AreNotEqual(DangerZone.Green, l.ZoneOf(room.Id), "no wash over a green room");
                }
            }
            Assert.IsTrue(yellow && red, "both zones show");
            Assert.AreNotEqual(HudRules.ZoneColor(DangerZone.Green), HudRules.ZoneColor(DangerZone.Red));
        }
    }
}
