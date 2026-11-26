using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The apartment block: one entrance, more storeys than other houses and a few flats on each — a hallway and a bathroom
    /// on the landing's side, the kitchen and the rooms by the windows; flats apart from each other, one door each onto the
    /// landing; the shelter map hidden in one flat above the ground floor.
    /// </summary>
    public class ApartmentTests
    {
        static readonly List<LevelLayout> s_Blocks = new();

        static List<LevelLayout> Blocks()
        {
            if (s_Blocks.Count > 0) return s_Blocks;
            s_Blocks.AddRange(TestHouses.First(HouseKind.Apartments, 4, seed => seed % 5));
            return s_Blocks;
        }

        static bool Passes(EdgeType k) => k is EdgeType.Door or EdgeType.Open or EdgeType.Breach;

        [Test]
        public void TheBlockHasManyFloorsAFewFlatsEachAndOneEntrance()
        {
            foreach (var l in Blocks())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                Assert.AreEqual("apartments", l.Mass, name);
                Assert.That(l.Floors, Is.InRange(5, 8), $"{name}: more storeys than an office");
                for (int f = 0; f < l.Floors; f++)
                {
                    int flats = l.Rooms.Where(r => r.Floor == f && r.Flat >= 0).Select(r => r.Flat).Distinct().Count();
                    Assert.That(flats, Is.InRange(f == 0 ? 1 : 2, 4), $"{name}: a few flats on floor {f}");
                    var landing = l.Rooms.Where(r => r.Floor == f && r.Type == RoomType.Corridor).ToList();
                    Assert.AreEqual(1, landing.Count, $"{name}: one landing on floor {f}");
                    Assert.Greater(landing[0].Bounds.width, l.Plan.SizeX - 1f, $"{name}: it runs the building's length");
                }
                // (the floors above have their balconies' doors: not ways in from the street)
                var outside = l.Plan.Openings.Where(o => l.Plan.Walls[o.Wall].Exterior && l.Plan.Walls[o.Wall].Floor == 0 && o.Kind != EdgeType.Wall).ToList();
                Assert.AreEqual(1, outside.Count, $"{name}: one way in from the street");
                foreach (var o in l.Plan.Openings.Where(o => l.Plan.Walls[o.Wall].Exterior && l.Plan.Walls[o.Wall].Floor > 0))
                    Assert.IsTrue(l.Plan.Balconies.Any(b => b.Floor == l.Plan.Walls[o.Wall].Floor && (b.Center - l.Plan.Walls[o.Wall].At(o.At)).magnitude < 0.05f),
                        $"{name}: a door in the facade above the ground is a balcony's");
                var w = l.Plan.Walls[outside[0].Wall];
                Assert.AreEqual(l.LobbyRoom, Mathf.Max(w.RoomA, w.RoomB), $"{name}: through the entrance hall");
                Assert.IsTrue(l.Rooms.Any(r => r.Type == RoomType.Utility && r.Floor == 0), $"{name}: the plant room with the drain");
            }
        }

        [Test]
        public void AFlatIsAHallwayABathroomAKitchenAndRooms()
        {
            foreach (var l in Blocks())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                foreach (var flat in l.Rooms.Where(r => r.Flat >= 0).GroupBy(r => r.Flat))
                {
                    // (the robots' charging room takes a room of a ground-floor flat: ChargingRooms, as the room it was)
                    var types = flat.Select(r => r.Id == l.ChargingRoom ? l.ChargingFrom : r.Type).ToList();
                    Assert.AreEqual(1, types.Count(t => t == RoomType.FlatHall), $"{name}: flat {flat.Key} has its hallway");
                    Assert.AreEqual(1, types.Count(t => t == RoomType.Bathroom), $"{name}: and its bathroom");
                    Assert.AreEqual(1, types.Count(t => t == RoomType.Living), $"{name}: and its living room");
                    Assert.That(types.Count, Is.InRange(3, 5), $"{name}: flat {flat.Key}: {string.Join(",", types)}");
                    Assert.AreEqual(1, flat.Select(r => r.Floor).Distinct().Count(), $"{name}: a flat is on one floor");
                    foreach (var r in flat)
                        Assert.IsTrue(l.Doorways.Any(d => (d.RoomA == r.Id || d.RoomB == r.Id) && d.Kind is EdgeType.Door or EdgeType.Open), $"{name}: a way into the {r.Type} of flat {flat.Key}");
                }
            }
        }

        [Test]
        public void FlatsOpenOnlyThroughTheirHallwayAndNeverIntoEachOther()
        {
            foreach (var l in Blocks())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                var entries = new Dictionary<int, int>();
                foreach (var o in l.Plan.Openings)
                {
                    var w = l.Plan.Walls[o.Wall];
                    if (w.RoomA < 0 || w.RoomB < 0 || !Passes(o.Kind)) continue;
                    var a = l.Plan.Rooms[w.RoomA];
                    var b = l.Plan.Rooms[w.RoomB];
                    if (a.Flat < 0 && b.Flat < 0) continue;
                    Assert.IsFalse(a.Flat >= 0 && b.Flat >= 0 && a.Flat != b.Flat, $"{name}: a walked way between flats {a.Flat} and {b.Flat}");
                    if (a.Flat >= 0 && b.Flat >= 0)
                    {
                        if (a.Type == RoomType.Bathroom || b.Type == RoomType.Bathroom)
                            Assert.IsTrue(a.Type == RoomType.FlatHall || b.Type == RoomType.FlatHall, $"{name}: a bathroom opens onto its hallway only");
                        continue;
                    }
                    var inside = a.Flat >= 0 ? a : b;
                    var other = a.Flat >= 0 ? b : a;
                    Assert.AreEqual(RoomType.FlatHall, inside.Type, $"{name}: only the hallway of flat {inside.Flat} opens out");
                    Assert.AreEqual(RoomType.Corridor, other.Type, $"{name}: onto the landing");
                    Assert.AreEqual(EdgeType.Door, o.Kind, $"{name}: through its door");
                    entries[inside.Flat] = entries.TryGetValue(inside.Flat, out int n) ? n + 1 : 1;
                }
                foreach (int flat in l.Rooms.Where(r => r.Flat >= 0).Select(r => r.Flat).Distinct())
                    Assert.AreEqual(1, entries.TryGetValue(flat, out int n) ? n : 0, $"{name}: flat {flat} has one door");
                Assert.IsTrue(l.AutoDoors.Where((_, i) => l.FlatEntry(i)).Any(), $"{name}: the flats' doors are known to the builder");
            }
        }

        [Test]
        public void TheStairsStandOneAboveTheOtherInOneStairwell()
        {
            foreach (var l in Blocks())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                Assert.AreEqual(l.Floors - 1, l.Stairs.Count, $"{name}: a flight pair from every floor but the top");
                var first = l.Stairs[0];
                foreach (var s in l.Stairs)
                {
                    Assert.IsTrue(s.Stacked, $"{name}: stacked flights");
                    Assert.AreEqual(first.Origin, s.Origin, $"{name}: floor {s.Floor} right above floor 0's");
                    Assert.AreEqual(first.Corner, s.Corner, name);
                    Assert.AreEqual(first.Flip, s.Flip, name);
                }
                // the slab keeps the floor's own landing: the hole is over the flights only, inside the staircase's square
                var hole = PlanGenerator.HoleOf(first);
                var square = PlanGenerator.StairArea(first, 0f);
                Assert.Less(hole.width * hole.height, square.width * square.height - 0.5f, $"{name}: the hole leaves the landing");
                Assert.IsTrue(square.Contains(hole.center), name);
                // thin flights: whoever climbs one has the storey's height over their head, less the flight above's thickness
                foreach (var b in StairGeometry.Boxes(true))
                    if (b.Name.StartsWith("Step") || b.Name == "Landing")
                        Assert.LessOrEqual(b.Size.y, StairGeometry.StackedThickness + 1e-4f, $"{b.Name} is thin");
            }
        }

        [Test]
        public void EveryFloorAboveTheGroundIsTheSameTypicalFloor()
        {
            foreach (var l in Blocks())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                string Floor(int f) => string.Join(";", l.Rooms.Where(r => r.Floor == f).OrderBy(r => r.Bounds.xMin).ThenBy(r => r.Bounds.yMin)
                    .Select(r => $"{r.Type}@{r.Bounds.xMin:0.0},{r.Bounds.yMin:0.0},{r.Bounds.width:0.0}x{r.Bounds.height:0.0}"));
                // (the vault's doorway is widened and moved along its wall afterwards: the one door that may differ)
                var vault = l.VaultDoorway >= 0 ? l.Doorways[l.VaultDoorway].Position : new Vector3(-99f, 0f, -99f);
                bool AtVault(PlanOpening o) { var p = l.Plan.Walls[o.Wall].At(o.At); return new Vector2(p.x - vault.x, p.y - vault.z).magnitude < 1.5f; }
                string Openings(int f) => string.Join(";", l.Plan.Openings.Where(o => l.Plan.Walls[o.Wall].Floor == f && !AtVault(o))
                    .Select(o => { var w = l.Plan.Walls[o.Wall]; var p = w.At(o.At); return $"{o.Kind}@{Mathf.Round(p.x)},{Mathf.Round(p.y)}"; }).OrderBy(x => x)); // (to a metre: the doors of a wall's run, not their exact spot)
                for (int f = 2; f < l.Floors; f++)
                {
                    Assert.AreEqual(Floor(1), Floor(f), $"{name}: floor {f} is floor 1's plan");
                    Assert.AreEqual(Openings(1), Openings(f), $"{name}: with the same doors and crawl ways");
                }
                // every bathroom of the ground floor's flats stands under the bathrooms above (one riser, one vent shaft)
                foreach (var bath in l.Rooms.Where(r => r.Type == RoomType.Bathroom && r.Floor == 0))
                    Assert.IsTrue(l.Rooms.Any(r => r.Type == RoomType.Bathroom && r.Floor == 1 && r.Bounds == bath.Bounds), $"{name}: the bathroom at {bath.Bounds} has one over it");
            }
        }

        [Test]
        public void TheShelterMapIsHiddenInAFlatAboveTheGround()
        {
            foreach (var l in Blocks())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                Assert.GreaterOrEqual(l.MapFlat, 0, $"{name}: the block has the map");
                var room = l.Plan.Rooms[l.MapRoom];
                Assert.AreEqual(l.MapFlat, room.Flat, $"{name}: in its flat");
                Assert.Greater(room.Floor, 0, $"{name}: above the ground floor");
                Assert.AreEqual(room, l.Plan.RoomAt(l.MapSpot + Vector3.up * 0.3f), $"{name}: its spot is in that room");
            }
            Assert.AreEqual(-1, TestHouses.Get(42, 2).MapFlat, "an office has no map");
        }

        [Test]
        public void TheFlatsAreFurnishedAsHomes()
        {
            Assert.AreEqual(HouseKind.Apartments, HouseKinds.Of(12, 2), "the fixture of the builder's and the navmesh's tests is a block");
            foreach (var l in Blocks())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                List<PropKind> In(PlanRoom r) => l.Props.Where(p => l.Plan.RoomAt(p.Position + Vector3.up * 0.3f) == r).Select(p => p.Kind).ToList();
                int Share(RoomType t, params PropKind[] kinds)
                {
                    var rooms = l.Rooms.Where(r => r.Type == t && r.Id != l.VaultRoom).ToList(); // (the vault is furnished as one)
                    return rooms.Count == 0 ? 100 : 100 * rooms.Count(r => In(r).Any(kinds.Contains)) / rooms.Count;
                }
                Assert.GreaterOrEqual(Share(RoomType.Bedroom, PropKind.DoubleBed), 80, $"{name}: beds in the bedrooms");
                Assert.GreaterOrEqual(Share(RoomType.Living, PropKind.Sofa), 80, $"{name}: sofas in the living rooms");
                Assert.GreaterOrEqual(Share(RoomType.Bathroom, PropKind.Bathtub, PropKind.HomeToilet), 80, $"{name}: a bath or a toilet in the bathrooms");
                // (a real block's 3 m hallway: with our wide doors a few of them are all doorways, no wall for a cupboard)
                Assert.GreaterOrEqual(Share(RoomType.FlatHall, PropKind.ShoeCabinet, PropKind.Wardrobe), 70, $"{name}: a cupboard in the hallways");
                Assert.IsTrue(l.Props.Any(p => p.Kind == PropKind.Mailboxes), $"{name}: the mailboxes in the entrance hall");
                // the map's flat has a cupboard to hide it in (a wardrobe, a chest of drawers or the hallway's shoe cabinet)
                var cupboards = l.Props.Where(p => p.Kind is PropKind.Wardrobe or PropKind.Dresser or PropKind.ShoeCabinet && HouseFeatures.FlatAt(l, p.Position) == l.MapFlat);
                Assert.IsTrue(cupboards.Any(), $"{name}: a cupboard in the map's flat");
            }
        }

        [Test]
        public void TheMapIsAHeldThreeByThreeThingThatNeverBurns()
        {
            byte map = ItemCatalog.ShelterMap;
            Assert.AreEqual("map", ItemCatalog.Shapes[map].Id);
            Assert.AreEqual(new Vector2Int(3, 3), ItemCatalog.Size(ItemCatalog.RotatedCells(map, 0)), "3 x 3 in the backpack");
            Assert.AreEqual(9, ItemCatalog.Cells(map));
            Assert.IsTrue(ItemCatalog.Holdable(map), "it goes into the hand");
            Assert.IsTrue(ItemCatalog.Unburnable(map) && ItemCatalog.IsTool(map) && !ItemCatalog.IsLoot(map), "never fuel, never cargo");
            Assert.AreEqual(0, ItemCatalog.Value(map, 0));
            Assert.IsTrue(ItemCatalog.FitsInDrawer(map), "folded, it fits a drawer");
        }
    }
}
