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
        static IEnumerable<LevelLayout> Houses(int count, int level = 3)
        {
            for (int i = 0; i < count; i++) yield return LevelGenerator.Generate(1000 + i * 7919, level + i % 3);
        }

        [Test]
        public void HousesAreVeryDifferent()
        {
            var masses = new HashSet<string>();
            var shapes = new HashSet<RoomShape>();
            int atriums = 0, shaped = 0, threeStorey = 0;
            var areas = new List<float>();
            var roomCounts = new HashSet<int>();
            foreach (var l in Houses(40))
            {
                masses.Add(l.Mass);
                roomCounts.Add(l.Rooms.Count);
                foreach (var r in l.Rooms)
                {
                    shapes.Add(r.Shape);
                    areas.Add(r.Area);
                    if (r.Levels > 1) atriums++;
                    if (r.Levels > 2) threeStorey++;
                    if (r.Shaped.Count > 0) shaped++;
                }
            }
            Assert.GreaterOrEqual(masses.Count, 5, "bars, L, T, U, courtyards, H: " + string.Join(", ", masses));
            foreach (var s in new[] { RoomShape.Rect, RoomShape.L, RoomShape.Corridor, RoomShape.Hall })
                Assert.Contains(s, shapes.ToList(), $"room shapes: {string.Join(", ", shapes)}");
            Assert.IsTrue(shapes.Contains(RoomShape.Rotunda) || shapes.Contains(RoomShape.Octagon), "a rotunda or an octagon somewhere");
            Assert.Greater(atriums, 5, "rooms two storeys high");
            Assert.Greater(threeStorey, 0, "a three-storey atrium");
            Assert.Greater(shaped, 20, "cut and rounded corners");
            Assert.Less(areas.Min(), 14f, "small rooms");
            Assert.Greater(areas.Max(), 100f, "big halls");
            Assert.Greater(roomCounts.Count, 15, "houses differ in room count");
            float mean = areas.Average();
            float sd = Mathf.Sqrt(areas.Select(a => (a - mean) * (a - mean)).Average());
            Assert.Greater(sd / mean, 0.6f, "rooms differ in size a lot");
        }

        [Test]
        public void HouseKindsChangeTheBuilding()
        {
            float Share(HouseKind kind, RoomType type, out float meanArea)
            {
                float typed = 0f, total = 0f;
                int rooms = 0, found = 0;
                for (int seed = 1; found < 4 && seed < 400; seed++)
                {
                    if (HouseKinds.Of(seed, 3) != kind) continue;
                    found++;
                    var l = LevelGenerator.Generate(seed, 3);
                    Assert.AreEqual(kind, l.Kind);
                    foreach (var r in l.Rooms)
                    {
                        if (r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) continue;
                        total += r.Area;
                        rooms++;
                        if (r.Type == type) typed += r.Area;
                    }
                }
                meanArea = total / Mathf.Max(1, rooms);
                return typed / Mathf.Max(1f, total);
            }
            float officeServers = Share(HouseKind.Office, RoomType.Server, out float officeRoom);
            Assert.Greater(Share(HouseKind.DataCenter, RoomType.Server, out _), officeServers * 1.8f, "a data centre is mostly server rooms");
            Assert.Greater(Share(HouseKind.Warehouse, RoomType.Storage, out float warehouseRoom), Share(HouseKind.Office, RoomType.Storage, out _) * 2f, "a warehouse stores");
            Assert.Greater(warehouseRoom, officeRoom * 1.2f, "a warehouse has big halls");
            Assert.AreEqual(HouseKind.Office, HouseKinds.Of(12345, 0), "the first house is an office");
        }

        [Test]
        public void WallsNeverOverlapAndOpeningsFitTheirWalls()
        {
            foreach (var l in Houses(12))
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
            foreach (var l in Houses(12))
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
            foreach (var l in Houses(30))
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
            Assert.Greater(seen, 3);
            // and a normal room has its slab
            var house = LevelGenerator.Generate(77, 3);
            var plain = house.Rooms.First(r => r.Levels == 1 && r.Floor == 0 && house.Plan.SpaceAt(1, new Vector2(r.Center.x, r.Center.z)) >= 0
                                               && house.Plan.KindAt(1, HousePlan.CellOf(new Vector2(r.Center.x, r.Center.z)).x, HousePlan.CellOf(new Vector2(r.Center.x, r.Center.z)).y) == CellKind.Room);
            Assert.IsFalse(house.OpenBetweenFloors(plain.Center + Vector3.up, plain.Center + Vector3.up * 4.6f));
        }

        [Test]
        public void SightStopsAtWallsAndPassesThroughDoorways()
        {
            foreach (var l in Houses(6))
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
