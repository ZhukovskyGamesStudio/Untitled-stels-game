using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The street the house stands on: one building of a block, neighbours close by, nothing inside it, lamps that keep out of it.</summary>
    public class CityPlanTests
    {
        static readonly Rect House = new(0f, 0f, 34f, 22f);

        static Rect Flat(Bounds b) => new(b.min.x, b.min.z, b.size.x, b.size.z);

        /// <summary>The gap between two rectangles (0 when they touch or overlap).</summary>
        static float Gap(Rect a, Rect b) =>
            new Vector2(Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax)), Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax))).magnitude;

        static bool IsBuilding(CityBox b) => b.Sides is CitySlot.FacadeA or CitySlot.FacadeB or CitySlot.FacadeC;

        [Test]
        public void NothingStandsInsideTheHouse([Values(0, 1, 2, 3)] int front, [Values(7, 42)] int seed)
        {
            var inside = new Rect(House.xMin + 0.01f, House.yMin + 0.01f, House.width - 0.02f, House.height - 0.02f);
            foreach (var b in CityPlan.Make(House, front, 3, seed).Boxes)
            {
                if (b.Center.y + b.Size.y / 2f <= CityPlan.RoadTop + 0.001f) continue; // the asphalt under it all, below the floor slab
                Assert.IsFalse(Flat(b.Bounds).Overlaps(inside), $"front {front}, seed {seed}: {b.Sides} at {b.Center} is inside the house");
            }
        }

        [Test]
        public void TheHouseIsOneBuildingOfAStreet([Values(0, 1, 2, 3)] int front)
        {
            var city = CityPlan.Make(House, front, 3, 7);
            var buildings = city.Boxes.Where(IsBuilding).Select(b => Flat(b.Bounds)).ToList();
            Assert.Greater(buildings.Count, 30, "a city full of buildings");
            // neighbours a few metres away on both sides along the street, the street's far side a road's width away
            var near = buildings.Where(r => Gap(r, House) < 7f).ToList();
            Assert.GreaterOrEqual(near.Count, 2, $"front {front}: neighbours close to the house");
            Assert.IsTrue(buildings.Any(r => Gap(r, House) > 14f && Gap(r, House) < 17f), $"front {front}: the row across the street");
            Assert.IsTrue(near.All(r => Gap(r, House) >= 2.9f), $"front {front}: an alley at least 3 m wide to the neighbours");
            for (int i = 0; i < buildings.Count; i++)
                for (int j = i + 1; j < buildings.Count; j++)
                    Assert.IsFalse(buildings[i].Overlaps(buildings[j]), $"front {front}: {buildings[i]} and {buildings[j]} overlap");
            // the pavement comes right up to the house's walls, level with its floor
            Assert.IsTrue(city.Ground.Any(g => Gap(Flat(g), House) < 0.01f && Mathf.Abs(g.max.y - CityPlan.WalkTop) < 1e-4f), "pavement at the walls");
            Assert.AreEqual(city.Boxes.Count, CityPlan.Make(House, front, 3, 7).Boxes.Count, "the same city from the same seed");
        }

        [Test]
        public void StreetLampsNeverShineIntoTheHouse([Values(0, 1, 2, 3)] int front)
        {
            var city = CityPlan.Make(House, front, 3, 7);
            Assert.Greater(city.Lamps.Count, 8);
            foreach (var l in city.Lamps)
            {
                // the lamp's light (no shadows: Level/CityLamp, a 70° cone straight down) must not reach a wall of the house
                float pool = (l.y - CityPlan.WalkTop) * Mathf.Tan(35f * Mathf.Deg2Rad);
                Assert.Greater(Gap(new Rect(l.x - pool, l.z - pool, 2f * pool, 2f * pool), House), 0.2f, $"front {front}: the lamp at {l} lights the house");
            }
        }

        [Test]
        public void TheShaftGoesThroughTheAsphalt()
        {
            // the asphalt lies under the whole block, the house too: the shaft to the basement is cut out of it (from the lobby
            // its top was a black plate over the shaft)
            var city = CityPlan.Make(House, 0, 3, 7);
            var hole = new Rect(4.25f, 3.25f, 1.5f, 1.5f);
            float before = city.Boxes.Where(b => b.Center.y < 0f).Sum(b => b.Size.x * b.Size.z);
            CityPlan.CutHole(city, hole);
            foreach (var b in city.Boxes)
                Assert.IsFalse(new Rect(b.Center.x - b.Size.x / 2f, b.Center.z - b.Size.z / 2f, b.Size.x, b.Size.z).Overlaps(new Rect(hole.x + 0.01f, hole.y + 0.01f, 1.48f, 1.48f)), "a box over the shaft");
            float after = city.Boxes.Where(b => b.Center.y < 0f).Sum(b => b.Size.x * b.Size.z);
            Assert.AreEqual(before - 1.5f * 1.5f, after, 0.5f, "only the shaft's hole is cut (out of the asphalt under the house)");
        }

        [Test]
        public void TheFrontIsTheSideNearestTheLobby()
        {
            Assert.AreEqual(0, CityPlan.SideNearest(House, new Vector2(17f, 3f)));
            Assert.AreEqual(1, CityPlan.SideNearest(House, new Vector2(31f, 11f)));
            Assert.AreEqual(2, CityPlan.SideNearest(House, new Vector2(17f, 20f)));
            Assert.AreEqual(3, CityPlan.SideNearest(House, new Vector2(2f, 11f)));
        }

        [Test]
        public void FacesAreWoundOutwards()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = Enumerable.Range(0, System.Enum.GetValues(typeof(CitySlot)).Length).Select(_ => new List<int>()).ToArray();
            CityPlan.AddFaces(new CityBox(Vector3.zero, new Vector3(2f, 3f, 4f), CitySlot.FacadeA, CitySlot.Roof), verts, normals, uvs, tris);
            Assert.AreEqual(4 * 6, tris[(int)CitySlot.FacadeA].Count, "four sides");
            Assert.AreEqual(6, tris[(int)CitySlot.Roof].Count, "a top, no bottom");
            foreach (var t in tris)
                for (int i = 0; i < t.Count; i += 3)
                {
                    Vector3 a = verts[t[i]], b = verts[t[i + 1]], c = verts[t[i + 2]];
                    // Unity draws triangles clockwise as seen from the front: their cross product points out, along the normal
                    Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), normals[t[i]]), 0f, $"triangle {i / 3} faces the wrong way");
                }
        }
    }
}
