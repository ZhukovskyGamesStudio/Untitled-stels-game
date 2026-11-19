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
        public void TheCityIsTheSameFromTheSameSeed([Values(0, 3)] int front)
        {
            var a = CityPlan.Make(House, front, 3, 42);
            var b = CityPlan.Make(House, front, 3, 42);
            Assert.AreEqual(a.Props.Count, b.Props.Count);
            for (int i = 0; i < a.Props.Count; i += 7)
            {
                Assert.AreEqual(a.Props[i].Kind, b.Props[i].Kind);
                Assert.AreEqual(a.Props[i].Position, b.Props[i].Position);
                Assert.AreEqual(a.Props[i].Pane, b.Props[i].Pane);
            }
        }

        [Test]
        public void ModelsStandOutsideTheHouse([Values(0, 1, 2, 3)] int front, [Values(7, 42)] int seed)
        {
            var city = CityPlan.Make(House, front, 3, seed);
            Assert.Greater(city.Props.Count, 200, "a street full of things");
            foreach (var p in city.Props)
            {
                var spot = new Rect(p.Position.x - 0.01f, p.Position.z - 0.01f, 0.02f, 0.02f);
                // a tree's crown reaches out 2 m over the pavement: it must not reach the house's windows
                float keep = p.Kind is CityPropKind.TreeA or CityPropKind.TreeB or CityPropKind.TreeC ? 2.2f : p.Kind == CityPropKind.Kerb ? 0f : 0.3f;
                Assert.GreaterOrEqual(CityPlan.Gap(spot, House), keep - 0.02f, $"front {front}, seed {seed}: {p.Kind} at {p.Position} is in or against the house");
            }
        }

        [Test]
        public void StreetFurnitureStandsOnThePavementAndCarsByTheKerb([Values(0, 1, 2, 3)] int front)
        {
            var city = CityPlan.Make(House, front, 3, 7);
            var ground = city.Ground.Select(Flat).ToList();
            bool OnGround(Vector3 p) => ground.Any(g => g.Contains(new Vector2(p.x, p.z)));
            var along = front % 2 == 0 ? Vector3.right : Vector3.forward;
            int cars = 0;
            foreach (var p in city.Props)
            {
                if (CityPlan.OnPavement(p.Kind))
                {
                    Assert.AreEqual(CityPlan.WalkTop, p.Position.y, 1e-4f, $"{p.Kind} at {p.Position}: on the pavement's top");
                    Assert.IsTrue(OnGround(p.Position), $"front {front}: {p.Kind} at {p.Position} stands in the road");
                }
                else if (CityPlan.IsCar(p.Kind))
                {
                    cars++;
                    Assert.AreEqual(CityPlan.RoadTop, p.Position.y, 1e-4f, $"{p.Kind} at {p.Position}: on the road");
                    Assert.IsFalse(OnGround(p.Position), $"front {front}: a car parked on the pavement at {p.Position}");
                    float kerb = ground.Min(g => CityPlan.Gap(new Rect(p.Position.x, p.Position.z, 0f, 0f), g));
                    Assert.That(kerb, Is.InRange(1.0f, 1.2f), $"front {front}: a car at {p.Position} is {kerb} m from the kerb");
                    // it lies along the street: its front looks along the house's front
                    Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(p.Forward, along)), 1e-3f, $"{p.Kind} at {p.Position} stands across the street");
                }
            }
            Assert.Greater(cars, 8, "cars along both kerbs");
        }

        [Test]
        public void WhatPlayersCanReachIsSolid([Values(0, 1)] int front)
        {
            // inside the yard every lamp, tree, bench, bin, car… has a solid round its spot
            var city = CityPlan.Make(House, front, 3, 7);
            var reach = new Rect(House.xMin - CityPlan.Yard, House.yMin - CityPlan.Yard, House.width + 2f * CityPlan.Yard, House.height + 2f * CityPlan.Yard);
            int seen = 0;
            foreach (var p in city.Props)
            {
                if (!(CityPlan.OnPavement(p.Kind) || CityPlan.IsCar(p.Kind)) || !reach.Contains(new Vector2(p.Position.x, p.Position.z))) continue;
                seen++;
                Assert.IsTrue(city.Solids.Any(s => Flat(s).Contains(new Vector2(p.Position.x, p.Position.z))), $"front {front}: {p.Kind} at {p.Position} has no collider");
            }
            Assert.Greater(seen, 5);
        }

        [Test]
        public void TheFacadeKitHangsOnTheBuildingsAndKeepsToItsBudget([Values(0, 2)] int front, [Values(7, 42)] int seed)
        {
            var city = CityPlan.Make(House, front, 3, seed);
            var buildings = city.Boxes.Where(IsBuilding).Select(b => b.Bounds).ToList();
            var walls = new[] { CityPropKind.WindowA, CityPropKind.WindowB, CityPropKind.CurtainBay, CityPropKind.Shopfront, CityPropKind.Entrance };
            var kit = city.Props.Where(p => walls.Contains(p.Kind)).ToList();
            Assert.Greater(kit.Count, 40, "windows, shop fronts and entrances on the buildings near the house");
            Assert.Less(kit.Count, 1100, "the windows' budget (each ~750 triangles in one merged mesh)");
            Assert.IsTrue(kit.Any(p => p.Pane == CitySlot.WindowLit) && kit.Any(p => p.Pane == CitySlot.WindowDark), "some lit, some dark");
            foreach (var p in kit)
                Assert.IsTrue(buildings.Any(b => OnFace(b, p.Position, p.Forward)), $"front {front}, seed {seed}: {p.Kind} at {p.Position} hangs on no building's wall");
            Assert.IsTrue(city.Props.Any(p => p.Kind == CityPropKind.Cornice) && city.Props.Any(p => p.Kind == CityPropKind.Coping), "the roofs' edges");
        }

        /// <summary>On the plane of a building's side (or the plinth's 8 cm in front of it), within its length, its front out of the wall.</summary>
        static bool OnFace(Bounds b, Vector3 p, Vector3 forward)
        {
            if (p.y < b.min.y - 0.01f || p.y > b.max.y) return false;
            for (int axis = 0; axis < 3; axis += 2)
            {
                float f = forward[axis];
                if (Mathf.Abs(f) < 0.99f) continue;
                float plane = f > 0 ? b.max[axis] : b.min[axis];
                float off = (p[axis] - plane) * Mathf.Sign(f);
                int other = 2 - axis;
                return off > -0.01f && off < 0.1f && p[other] > b.min[other] && p[other] < b.max[other];
            }
            return false;
        }

        [Test]
        public void ModelFilesAreSnakeCase()
        {
            Assert.AreEqual("city_curtain_bay", CityPlan.ModelFile(CityPropKind.CurtainBay));
            Assert.AreEqual("city_window_a", CityPlan.ModelFile(CityPropKind.WindowA));
            Assert.AreEqual("city_roof_ac", CityPlan.ModelFile(CityPropKind.RoofAc));
        }

        [Test]
        public void LiveryAndPaneComeFromTheCopy()
        {
            var p = new CityProp(CityPropKind.Sedan, Vector3.zero, 0f, Vector3.one, CitySlot.CarBlue, CitySlot.WindowLit);
            Assert.AreEqual(CitySlot.CarBlue, CityPlan.Resolve(CitySlot.Livery, p));
            Assert.AreEqual(CitySlot.WindowLit, CityPlan.Resolve(CitySlot.Pane, p));
            Assert.AreEqual(CitySlot.Tyre, CityPlan.Resolve(CitySlot.Tyre, p));
        }

        [Test]
        public void ModelsArePosedStretchedAndStillFaceOut()
        {
            // one triangle facing +z (clockwise seen from +z)
            var v = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f) };
            var n = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = Enumerable.Range(0, System.Enum.GetValues(typeof(CitySlot)).Length).Select(_ => new List<int>()).ToArray();
            var pose = new CityProp(CityPropKind.Band, new Vector3(5f, 2f, 0f), 90f, new Vector3(3f, 1f, 1f)).Pose; // stretched 3x, turned to face +x
            CityPlan.AddModel(v, n, new[] { 0, 1, 2 }, pose, CitySlot.Stone, verts, normals, uvs, tris);
            Assert.That(Vector3.Distance(Vector3.right, normals[0]), Is.LessThan(1e-4f), "the normal turns with the model");
            Assert.That(Vector3.Distance(new Vector3(5f, 2f, -3f), verts[1]), Is.LessThan(1e-4f), "stretched along its x, which now runs along -z");
            var t = tris[(int)CitySlot.Stone];
            Assert.Greater(Vector3.Dot(Vector3.Cross(verts[t[1]] - verts[t[0]], verts[t[2]] - verts[t[0]]), normals[0]), 0f, "still wound to face out");
            Assert.That(Vector2.Distance(new Vector2(-3f, 2f), uvs[1]), Is.LessThan(1e-4f), "texture in world metres (z, y on a face looking along x)");
            // a mirroring pose turns the triangle round
            CityPlan.AddModel(v, n, new[] { 0, 1, 2 }, Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)), CitySlot.Metal, verts, normals, uvs, tris);
            var m = tris[(int)CitySlot.Metal];
            Assert.Greater(Vector3.Dot(Vector3.Cross(verts[m[1]] - verts[m[0]], verts[m[2]] - verts[m[0]]), normals[m[0]]), 0f, "mirrored, still facing out");
        }

        [Test]
        public void AFloatingBoxHasABottom()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = Enumerable.Range(0, System.Enum.GetValues(typeof(CitySlot)).Length).Select(_ => new List<int>()).ToArray();
            CityPlan.AddFaces(new CityBox(new Vector3(0f, 2f, 0f), new Vector3(1f, 2.2f, 1f), CitySlot.Metal, CitySlot.Metal), verts, normals, uvs, tris);
            var t = tris[(int)CitySlot.Metal];
            Assert.AreEqual(6 * 6, t.Count, "four sides, a top and a bottom: seen from under it, it is not hollow");
            for (int i = 0; i < t.Count; i += 3)
                Assert.Greater(Vector3.Dot(Vector3.Cross(verts[t[i + 1]] - verts[t[i]], verts[t[i + 2]] - verts[t[i]]), normals[t[i]]), 0f);
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
