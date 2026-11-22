using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The abandoned research station is a house of its own (a slab with a spine corridor: labs, servers and control below,
    /// quarters above) and the only house dressed derelict — in every style; the realistic facility style (S16) dresses
    /// every house in its own materials, each kind of room in its own finishes, and leaves the derelict look to the station.
    /// </summary>
    public class AbandonedTests
    {
        static readonly List<LevelLayout> s_Stations = new();

        static List<LevelLayout> Stations()
        {
            if (s_Stations.Count > 0) return s_Stations;
            for (int seed = 1; s_Stations.Count < 4 && seed < 2000; seed++)
            {
                int house = 1 + seed % 4;
                if (HouseKinds.Of(seed, house) != HouseKind.Abandoned) continue;
                s_Stations.Add(TestHouses.Get(seed, house));
            }
            return s_Stations;
        }

        [Test]
        public void SomeHousesAreAbandonedStations()
        {
            int n = Enumerable.Range(1, 1000).Count(seed => HouseKinds.Of(seed, 2) == HouseKind.Abandoned);
            Assert.That(n, Is.InRange(50, 160), "about one house in eleven");
            Assert.AreEqual(Location.Abandoned, HouseFeatures.LocationOf(HouseKind.Abandoned));
            Assert.AreEqual(4, Stations().Count);
        }

        [Test]
        public void TheStationIsASlabWithLabsBelowAndQuartersAbove()
        {
            foreach (var l in Stations())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                Assert.AreEqual("station", l.Mass, name);
                Assert.AreEqual(HouseKind.Abandoned, l.PlanKind, name);
                Assert.That(l.Floors, Is.InRange(2, 3), name);
                for (int f = 0; f < l.Floors; f++)
                {
                    var corridor = l.Rooms.Where(r => r.Floor == f && r.Type == RoomType.Corridor).OrderByDescending(r => r.Area).FirstOrDefault();
                    Assert.IsNotNull(corridor, $"{name}: a corridor on floor {f}");
                    Assert.Greater(corridor.Bounds.width, l.Plan.SizeX - 1f, $"{name}: the spine runs the building's length on floor {f}");
                }
                Assert.Greater(l.Rooms.Count(r => r.Type == RoomType.Lab), 1, $"{name}: labs");
                Assert.Greater(l.Rooms.Count(r => r.Type == RoomType.Server), 0, $"{name}: servers");
                Assert.Greater(l.Rooms.Count(r => r.Type == RoomType.Bedroom && r.Floor > 0), 0, $"{name}: the staff's quarters upstairs");
                Assert.Greater(l.Rooms.Count(r => r.Type == RoomType.Kitchen), 0, $"{name}: a canteen");
                Assert.AreEqual(-1, l.PointOfInterest, $"{name}: no piece of furniture to find");
            }
        }

        static StyleKit[] Kits() => Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.Kits.Where(k => k != null).ToArray();

        [Test]
        public void EveryStylesKitCarriesTheDerelictLook()
        {
            var kits = Kits();
            Assert.Greater(kits.Length, 0);
            foreach (var kit in kits)
            {
                Assert.IsNotNull(kit.DerelictWall, $"{kit.name}: steel panels");
                Assert.IsNotNull(kit.DerelictFloor, $"{kit.name}: floor plates");
                Assert.Greater(kit.FloorDecay.Length + kit.WallDecay.Length, 0, $"{kit.name}: decay");
            }
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void OnlyTheStationIsDressedDerelict()
        {
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var station = Stations()[0];
            var others = new[] { HouseKind.Office, HouseKind.Hospital, HouseKind.Garage, HouseKind.Apartments }
                .Select(k => { int seed = Enumerable.Range(1, 400).First(s => HouseKinds.Of(s, 2) == k); return TestHouses.Get(seed, 2); }).ToArray();
            foreach (var style in config.Styles.Where(s => s != null))
            {
                var kit = config.KitFor(style.Geometry.Family);
                if (kit == null) continue;
                var dressed = HouseRules.Of(station).Look(style.Geometry, kit, station);
                Assert.AreEqual(kit.DerelictWall, dressed.Wall, $"{style.name}: the station's walls are steel panels");
                Assert.Greater(dressed.DecayDensity, 0f, $"{style.name}: the station decays");
                foreach (var l in others)
                {
                    var g = HouseRules.Of(l).Look(style.Geometry, kit, l);
                    Assert.AreNotEqual(kit.DerelictWall, g.Wall, $"{style.name}, {l.Kind}: keeps its own walls");
                    Assert.AreEqual(0f, g.DecayDensity, $"{style.name}, {l.Kind}: no decay outside the station");
                    Assert.AreEqual(0f, g.PosterChance, $"{style.name}, {l.Kind}: no derelict posters outside the station");
                }
            }
        }

        [Test]
        public void TheQuietFacilityStyleGivesEveryKindOfRoomItsOwnFinish()
        {
            var style = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.Styles.First(s => s != null && s.name == "S16_Derelict");
            var g = style.Geometry;
            Assert.AreEqual(ModelFamily.Realistic, g.Family);
            Assert.AreEqual(0f, g.DecayDensity, "no decay of its own");
            foreach (var room in new[] { RoomType.Kitchen, RoomType.Restroom, RoomType.Office, RoomType.Library, RoomType.Gym, RoomType.Lab, RoomType.Storage })
                Assert.GreaterOrEqual(g.RoomFloorOf(room), 0, $"{room}: a floor of its own");
            foreach (var room in new[] { RoomType.Kitchen, RoomType.Restroom, RoomType.Library, RoomType.Gym, RoomType.Lab, RoomType.Storage })
                Assert.GreaterOrEqual(g.RoomLookOf(room), 0, $"{room}: walls of its own");
            Assert.AreNotEqual(g.RoomFloors[g.RoomFloorOf(RoomType.Kitchen)].Material, g.RoomFloors[g.RoomFloorOf(RoomType.Office)].Material, "a kitchen is not carpeted like an office");
        }
    }
}
