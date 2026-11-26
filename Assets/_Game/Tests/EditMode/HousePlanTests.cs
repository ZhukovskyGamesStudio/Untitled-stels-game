using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The floor plans of houses: rooms of every size and shape, sound walls and openings, atriums.</summary>
    public class HousePlanTests
    {
        [Test]
        public void MassesOfEveryKindComeUp()
        {
            // the mass alone (no house): bars, L, T, U, courtyards, H at the sizes of small and big houses
            var kinds = new HashSet<string>();
            foreach (float area in new[] { 800f, 1500f, 2500f })
                for (int seed = 0; seed < 200; seed++)
                {
                    var wings = PlanGenerator.Mass(new System.Random(seed), area, out string kind);
                    kinds.Add(kind);
                    Assert.IsNotEmpty(wings);
                    foreach (var w in wings) Assert.IsTrue(w.xMin >= 0 && w.yMin >= 0 && w.width > 0 && w.height > 0, $"{kind}: wing {w}");
                }
            CollectionAssert.IsSubsetOf(new[] { "bar", "L", "T", "U", "courtyard", "H" }, kinds.ToList(), string.Join(", ", kinds));
        }

        [Test]
        public void HousesAreVeryDifferent()
        {
            var masses = new HashSet<string>();
            var shapes = new HashSet<RoomShape>();
            int atriums = 0, shaped = 0;
            var areas = new List<float>();
            var roomCounts = new HashSet<int>();
            // the office generator's variety: the sample's offices and a few more (a shop and a hospital have plans of their own)
            var houses = TestHouses.Sample.Where(h => !HouseKinds.OwnPlan(h.Kind)).ToList();
            for (int seed = 3; houses.Count < 7; seed++) if (!HouseKinds.OwnPlan(HouseKinds.Of(seed * 101, 2))) houses.Add(TestHouses.Get(seed * 101, 2));
            foreach (var l in houses)
            {
                masses.Add(l.Mass);
                roomCounts.Add(l.Rooms.Count);
                foreach (var r in l.Rooms)
                {
                    shapes.Add(r.Shape);
                    areas.Add(r.Area);
                    if (r.Levels > 1) atriums++;
                    if (r.Shaped.Count > 0) shaped++;
                }
            }
            Assert.GreaterOrEqual(masses.Count, 3, "bars, L, T, U, courtyards, H: " + string.Join(", ", masses));
            foreach (var s in new[] { RoomShape.Rect, RoomShape.L, RoomShape.Corridor, RoomShape.Hall })
                Assert.Contains(s, shapes.ToList(), $"room shapes: {string.Join(", ", shapes)}");
            Assert.IsTrue(shapes.Contains(RoomShape.Rotunda) || shapes.Contains(RoomShape.Octagon), "a rotunda or an octagon somewhere");
            Assert.Greater(atriums, 1, "rooms two storeys high");
            Assert.Greater(shaped, 5, "cut and rounded corners");
            Assert.Less(areas.Min(), 14f, "small rooms");
            Assert.Greater(areas.Max(), 100f, "big halls");
            Assert.Greater(roomCounts.Count, 4, "houses differ in room count");
            float mean = areas.Average();
            float sd = Mathf.Sqrt(areas.Select(a => (a - mean) * (a - mean)).Average());
            Assert.Greater(sd / mean, 0.6f, "rooms differ in size a lot");
        }

        [Test]
        public void HouseKindsChangeTheBuilding()
        {
            // the kind changes the programme of rooms and their size (a pure clone of the settings), and the house carries it
            var b = new BuildingSettings();
            float Share(BuildingSettings s, RoomType type)
            {
                float total = s.RoomWeights.Sum(w => (float)w.Weight);
                return s.RoomWeights.Where(w => w.Room == type).Sum(w => (float)w.Weight) / total;
            }
            var data = HouseKinds.Apply(HouseKind.DataCenter, b);
            var store = HouseKinds.Apply(HouseKind.Warehouse, b);
            Assert.Greater(Share(data, RoomType.Server), Share(b, RoomType.Server) * 1.8f, "a data centre is mostly server rooms");
            Assert.Greater(Share(store, RoomType.Storage), Share(b, RoomType.Storage) * 2f, "a warehouse stores");
            Assert.Greater(store.RoomSizeScale, b.RoomSizeScale * 1.2f, "a warehouse has big halls");
            Assert.AreSame(b, HouseKinds.Apply(HouseKind.Office, b), "an office is the plain settings");
            var first = Enumerable.Range(1, 60).Select(seed => HouseKinds.Of(seed, 0)).ToList();
            Assert.IsTrue(first.Contains(HouseKind.Garage) && first.Contains(HouseKind.Hospital), "the first house may be a shop or a hospital too");
            var kinds = new HashSet<HouseKind>();
            for (int seed = 1; seed < 400; seed++) kinds.Add(HouseKinds.Of(seed, 3));
            Assert.Greater(kinds.Count, 3, "houses of many kinds: " + string.Join(", ", kinds));
            foreach (var l in TestHouses.Sample) Assert.AreEqual(HouseKinds.Of(l.Seed, l.LevelIndex), l.Kind, $"seed {l.Seed}");
        }

        [Test]
        public void WallsNeverOverlapAndOpeningsFitTheirWalls()
        {
            foreach (var l in TestHouses.Sample)
            {
                var p = l.Plan;
                for (int i = 0; i < p.Walls.Count; i++)
                {
                    var a = p.Walls[i];
                    Assert.Greater(a.Length, 0.2f, $"seed {l.Seed}: a wall of {a.Length} m");
                    if (a.Shaped) continue;
                    for (int j = i + 1; j < p.Walls.Count; j++)
                    {
                        var b = p.Walls[j];
                        if (b.Floor != a.Floor || b.Shaped) continue;
                        // collinear axis-aligned walls must not share more than an end point
                        bool sameLineX = Mathf.Abs(a.A.x - a.B.x) < 1e-3f && Mathf.Abs(b.A.x - b.B.x) < 1e-3f && Mathf.Abs(a.A.x - b.A.x) < 1e-3f;
                        bool sameLineZ = Mathf.Abs(a.A.y - a.B.y) < 1e-3f && Mathf.Abs(b.A.y - b.B.y) < 1e-3f && Mathf.Abs(a.A.y - b.A.y) < 1e-3f;
                        if (!sameLineX && !sameLineZ) continue;
                        float a0 = sameLineX ? Mathf.Min(a.A.y, a.B.y) : Mathf.Min(a.A.x, a.B.x), a1 = sameLineX ? Mathf.Max(a.A.y, a.B.y) : Mathf.Max(a.A.x, a.B.x);
                        float b0 = sameLineX ? Mathf.Min(b.A.y, b.B.y) : Mathf.Min(b.A.x, b.B.x), b1 = sameLineX ? Mathf.Max(b.A.y, b.B.y) : Mathf.Max(b.A.x, b.B.x);
                        Assert.LessOrEqual(Mathf.Min(a1, b1) - Mathf.Max(a0, b0), 0.01f, $"seed {l.Seed}: walls {i} and {j} overlap");
                    }
                }
                foreach (var o in p.Openings)
                {
                    var w = p.Walls[o.Wall];
                    Assert.IsFalse(w.Railing || w.Shaped);
                    if (o.Kind == EdgeType.Open) continue;
                    Assert.GreaterOrEqual(o.At - o.Width / 2f, 0.2f, $"seed {l.Seed}: opening runs off the wall");
                    Assert.LessOrEqual(o.At + o.Width / 2f, w.Length - 0.2f, $"seed {l.Seed}: opening runs off the wall");
                }
            }
        }

        [Test]
        public void RoomsAreWholeAndUsable()
        {
            foreach (var l in TestHouses.Sample)
                foreach (var r in l.Rooms)
                {
                    Assert.Greater(r.Area, 5f, $"seed {l.Seed}: {r.Type} of {r.Area} m²");
                    Assert.AreEqual(r, l.RoomAt(r.Center), $"seed {l.Seed}: the centre of {r.Type} is on its floor");
                    Assert.GreaterOrEqual(Mathf.Min(r.Bounds.width, r.Bounds.height), 2.5f, $"seed {l.Seed}: {r.Type} too narrow");
                }
        }

        [Test]
        public void AtriumsHaveNoSlabAndGalleriesLookIntoThem()
        {
            int seen = 0;
            foreach (var l in TestHouses.Sample)
                foreach (var r in l.Rooms.Where(r => r.Levels > 1))
                {
                    seen++;
                    var c = new Vector2(r.Center.x, r.Center.z);
                    for (int lv = 1; lv < r.Levels; lv++)
                        Assert.AreEqual(r.Id, l.Plan.SpaceAt(r.Floor + lv, c), $"seed {l.Seed}: the void over {r.Type} on floor {r.Floor + lv}");
                    // from the middle of the atrium the floor above is open to the floor below
                    var low = r.Center + Vector3.up * 1.5f;
                    var high = r.Center + Vector3.up * (LevelLayout.FloorY(1) + 1.5f) + Vector3.right * 0.3f;
                    Assert.IsTrue(l.OpenBetweenFloors(low, high), $"seed {l.Seed}: no slab inside the atrium");
                }
            Assert.Greater(seen, 0, "the sample has atriums");
            // and a normal room has its slab
            var house = TestHouses.Get(1337, 3);
            var plain = house.Rooms.First(r => r.Levels == 1 && r.Floor == 0 && house.Plan.SpaceAt(1, new Vector2(r.Center.x, r.Center.z)) >= 0
                                               && house.Plan.KindAt(1, HousePlan.CellOf(new Vector2(r.Center.x, r.Center.z)).x, HousePlan.CellOf(new Vector2(r.Center.x, r.Center.z)).y) == CellKind.Room);
            Assert.IsFalse(house.OpenBetweenFloors(plain.Center + Vector3.up, plain.Center + Vector3.up * 4.6f));
        }

        [Test]
        public void SightStopsAtWallsAndPassesThroughDoorways()
        {
            foreach (var l in TestHouses.Sample)
            {
                var p = l.Plan;
                foreach (var d in l.Doorways.Where(d => d.Kind == EdgeType.Door && !d.Exterior).Take(10))
                {
                    var across = d.Rotation * Vector3.forward;
                    var a = new Vector2(d.Position.x - across.x * 0.6f, d.Position.z - across.z * 0.6f);
                    var b = new Vector2(d.Position.x + across.x * 0.6f, d.Position.z + across.z * 0.6f);
                    Assert.IsTrue(p.CanSee(d.Floor, a, b), $"seed {l.Seed}: through the doorway at {d.Position}");
                    // a step beside the doorway the wall stands (unless another opening is there)
                    var w = p.Walls[d.Wall];
                    var dp = new Vector2(d.Position.x, d.Position.z);
                    float t0 = Vector2.Dot(dp - w.A, w.Dir) + d.Width / 2f + 0.6f;
                    if (t0 > w.Length - 0.4f) continue;
                    bool open = p.OpeningsOf(d.Wall).Any(o => o.Kind == EdgeType.Open || Mathf.Abs(o.At - t0) < o.Width / 2f + 0.3f);
                    if (open) continue;
                    var shift = w.At(t0) - dp;
                    Assert.IsFalse(p.CanSee(d.Floor, a + shift, b + shift), $"seed {l.Seed}: through the wall beside the doorway at {d.Position}");
                }
            }
        }
    }
}
