using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The house as a place to sneak through: most rooms with loot have a second way in (a crawl way counts), and the key of
    /// a locked door lies deeper in the house than the door, so the team goes past it, further in, and back.
    /// </summary>
    public class StealthLayoutTests
    {
        static IEnumerable<LevelLayout> Houses()
        {
            for (int seed = 1; seed <= 12; seed++)
                for (int house = 1; house < 6; house += 2)
                    yield return LevelGenerator.Generate(seed * 104729 + house, house, null, null, 2, new FeatureSettings());
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void MostLootRoomsHaveASecondWayIn()
        {
            int rooms = 0, two = 0;
            foreach (var l in Houses())
            {
                // the hospital's patient rooms and theatres, the shop's paint booth: one door each by their plan
                if (HouseKinds.OwnPlan(l.Kind)) continue;
                var plan = l.Plan;
                var loot = new bool[plan.Rooms.Count];
                foreach (var p in l.ItemSpots) { var r = plan.RoomAt(p + Vector3.up * 0.3f); if (r != null) loot[r.Id] = true; }
                foreach (var r in plan.Rooms)
                {
                    if (!loot[r.Id] || r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) continue;
                    int ways = 0;
                    foreach (var o in plan.Openings)
                    {
                        var w = plan.Walls[o.Wall];
                        if ((w.RoomA == r.Id || w.RoomB == r.Id) && o.Kind != EdgeType.Wall) ways++;
                    }
                    rooms++;
                    if (ways >= 2) two++;
                }
            }
            // it was two in three before crawl ways went to the rooms with one way in (BuildingSettings.DeadEndSneakBonus)
            Assert.That(two, Is.GreaterThan(rooms * 3 / 4), $"loot rooms with two ways in: {two} of {rooms}");
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void KeysLieDeeperThanTheirDoors()
        {
            int keys = 0, deeper = 0;
            foreach (var l in Houses())
                foreach (var k in l.KeyDoors)
                {
                    var keyRoom = l.Plan.RoomAt(k.Key + Vector3.up * 0.3f);
                    var at = l.AutoDoors[k.Door].position;
                    int near = -1;
                    foreach (var d in l.Doorways)
                        if ((d.Position - at).sqrMagnitude < 0.1f) near = k.Rooms.Contains(d.RoomA) ? d.RoomB : d.RoomA;
                    if (keyRoom == null || near < 0) continue;
                    keys++;
                    if (l.RoomSteps[keyRoom.Id] > l.RoomSteps[near]) deeper++;
                }
            Assert.That(keys, Is.GreaterThan(10), "key doors to look at");
            Assert.That(deeper, Is.GreaterThan(keys * 4 / 5), $"keys deeper than their door: {deeper} of {keys}");
        }
    }
}
