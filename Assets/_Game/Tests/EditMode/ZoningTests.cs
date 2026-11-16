using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Zoning: work rooms on the facade, service rooms inside, toilets by the stairs, themed rooms capped.</summary>
    public class ZoningTests
    {
        [Test]
        public void ATypeScoresBestInItsZone()
        {
            var rules = RoomRules.Defaults();
            Assert.Greater(Zoning.Score(RoomType.Office, true, false, rules), Zoning.Score(RoomType.Office, false, false, rules), "an office by the windows");
            Assert.Greater(Zoning.Score(RoomType.Server, false, false, rules), Zoning.Score(RoomType.Server, true, false, rules), "a server room inside");
            Assert.Greater(Zoning.Score(RoomType.Restroom, false, true, rules), Zoning.Score(RoomType.Restroom, false, false, rules), "toilets by the core");
            Assert.AreEqual(0, Zoning.Score(RoomType.Lab, true, false, rules), "a lab goes anywhere");
            Assert.IsTrue(RoomRules.Capped(RoomRules.For(RoomType.Kitchen, rules), 3, 1), "one kitchen a floor");
            Assert.IsFalse(RoomRules.Capped(RoomRules.For(RoomType.Kitchen, rules), 3, 0));
            Assert.IsTrue(RoomRules.Capped(RoomRules.For(RoomType.Gym, rules), 1, 0), "one gym a house");
            Assert.IsFalse(RoomRules.Capped(RoomRules.For(RoomType.Office, rules), 99, 99), "offices: no cap");
        }

        [Test]
        public void AHouseKindThatFavoursATypeLiftsItsCap()
        {
            var hospital = HouseKinds.Apply(HouseKind.Hospital, new BuildingSettings());
            Assert.AreEqual(0, RoomRules.For(RoomType.Infirmary, hospital.RoomTypeRules).PerHouse, "a hospital has its wards");
            Assert.AreEqual(1, RoomRules.For(RoomType.Infirmary, new BuildingSettings().RoomTypeRules).PerHouse, "the settings it came from keep theirs");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HousesAreZonedAndCapped(bool classic)
        {
            int work = 0, workDark = 0, service = 0, serviceIn = 0, wc = 0, wcCore = 0;
            for (int seed = 1; seed <= 8; seed++)
                for (int house = 0; house < 5; house += 2)
                {
                    var l = LevelGenerator.Generate(seed * 7727 + house, house, null, null, 2, null, classic);
                    var plan = l.Plan;
                    var rules = HouseKinds.Apply(l.Kind, new BuildingSettings()).RoomTypeRules;
                    var inHouse = new Dictionary<RoomType, int>();
                    var onFloor = new Dictionary<(int, RoomType), int>();
                    foreach (var r in plan.Rooms)
                    {
                        if (r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) continue;
                        inHouse[r.Type] = (inHouse.TryGetValue(r.Type, out var h) ? h : 0) + 1;
                        onFloor[(r.Floor, r.Type)] = (onFloor.TryGetValue((r.Floor, r.Type), out var n) ? n : 0) + 1;
                        float outside = plan.Walls.Where(w => w.Exterior && (w.RoomA == r.Id || w.RoomB == r.Id)).Sum(w => w.Length);
                        var zone = RoomRules.For(r.Type, rules).Zone;
                        if (zone == RoomZone.Facade) { work++; if (outside < Zoning.FacadeWall) workDark++; }
                        if (zone == RoomZone.Interior) { service++; if (outside < Zoning.FacadeWall) serviceIn++; }
                        if (r.Type != RoomType.Restroom) continue;
                        wc++;
                        var c = new Vector2(r.Center.x, r.Center.z);
                        bool core = l.Stairs.Any(s => (s.Floor == r.Floor || s.Floor + 1 == r.Floor) && (s.Origin - c).magnitude <= Zoning.CoreReach)
                            || plan.Walls.Any(w => (w.RoomA == r.Id || w.RoomB == r.Id) && w.RoomA >= 0 && w.RoomB >= 0
                                                   && plan.Rooms[w.RoomA == r.Id ? w.RoomB : w.RoomA].Type == RoomType.Hall);
                        if (core) wcCore++;
                    }
                    string at = $"seed {seed} house {house} ({l.Kind})";
                    foreach (var kv in inHouse)
                    {
                        var rule = RoomRules.For(kv.Key, rules);
                        if (rule.PerHouse > 0) Assert.That(kv.Value, Is.LessThanOrEqualTo(rule.PerHouse), $"{at}: {kv.Key} a house");
                    }
                    foreach (var kv in onFloor)
                    {
                        var rule = RoomRules.For(kv.Key.Item2, rules);
                        if (rule.PerFloor > 0) Assert.That(kv.Value, Is.LessThanOrEqualTo(rule.PerFloor), $"{at}: {kv.Key.Item2} on floor {kv.Key.Item1}");
                    }
                }
            Assert.That(workDark, Is.LessThan(work / 4), $"work rooms without windows: {workDark} of {work}");
            Assert.That(serviceIn, Is.GreaterThan(service * 3 / 4), $"service rooms inside: {serviceIn} of {service}");
            Assert.That(wcCore, Is.GreaterThan(wc * 3 / 5), $"toilets by the stairs: {wcCore} of {wc}");
        }
    }
}
