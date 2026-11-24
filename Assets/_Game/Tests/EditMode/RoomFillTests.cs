using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The soft light a room's lamps leave in it: its own room only, through an open passage a little, through a wall never.</summary>
    public class RoomFillTests
    {
        /// <summary>Two rooms 4 x 4 m side by side along x (cells 0..7 and 8..15), a wall between them.</summary>
        static HousePlan TwoRooms(bool passage)
        {
            var plan = new HousePlan(1, 16, 8);
            for (int z = 0; z < 8; z++)
                for (int x = 0; x < 16; x++) plan.Set(0, x, z, CellKind.Room, x < 8 ? 0 : 1);
            plan.Rooms.Add(new PlanRoom { Id = 0, Area = 16f });
            plan.Rooms.Add(new PlanRoom { Id = 1, Area = 16f });
            plan.DeriveWalls();
            if (passage)
            {
                int wall = plan.Walls.FindIndex(w => w.Separates(0, 1));
                Assert.GreaterOrEqual(wall, 0);
                plan.Openings.Add(new PlanOpening { Wall = wall, Kind = EdgeType.Open, At = plan.Walls[wall].Length / 2f, Width = 2f });
            }
            return plan;
        }

        static readonly Color Lit = new(0.3f, 0.28f, 0.25f, 1f);

        [Test]
        public void ARoomsLevelComesFromItsLampsAndItsSize()
        {
            var lamp = new Color(3.6f, 3.4f, 3f);
            var small = RoomFill.Level(lamp, 9f, 1f);
            var big = RoomFill.Level(lamp, 100f, 1f);
            Assert.Greater(small.r, big.r * 3f, "a hall under one lamp is dimmer than a closet");
            Assert.That(RoomFill.Level(lamp, 16f, 1f).r, Is.InRange(0.03f, 0.4f), "a soft fill, no second lamp");
            Assert.AreEqual(0f, RoomFill.Level(Color.black, 16f, 1f).maxColorComponent, "no lamp burning, no fill");
            Assert.AreEqual(0f, RoomFill.Level(lamp, 16f, 0f).maxColorComponent, "a style without it");
            Assert.AreEqual(RoomFill.Level(lamp, 16f, 1f).r * 2f, RoomFill.Level(lamp * 2f, 16f, 1f).r, 1e-4f);
        }

        [Test]
        public void ACellKeepsItsLevelAndItsSides()
        {
            foreach (var v in new[] { Vector3.zero, new Vector3(0.05f, 0.1f, 0.2f), new Vector3(1.9f, 0.6f, 0.01f) })
            {
                uint cell = RoomFill.Pack(v, RoomFill.BlockX | RoomFill.OpenUp);
                var back = RoomFill.Unpack(cell);
                Assert.Less((back - v).magnitude, 0.02f, v.ToString());
                Assert.AreEqual(RoomFill.BlockX | RoomFill.OpenUp, cell & (RoomFill.BlockX | RoomFill.BlockZ | RoomFill.OpenUp));
            }
        }

        [Test]
        public void NothingPassesThroughAWall()
        {
            var plan = TwoRooms(false);
            var grid = new RoomFill.Grid(plan);
            var cells = new uint[grid.Count];
            grid.Bake(new[] { Lit, Color.black }, cells);
            float y = 1.2f;
            // everywhere in the dark room: its floor, the wall of the lit room seen from here, things standing against it
            for (float x = 4.16f; x < 8f; x += 0.07f)
                for (float z = 0.1f; z < 4f; z += 0.19f)
                {
                    Assert.AreEqual(0f, grid.Sample(cells, new Vector3(x, 0f, z), Vector3.up).magnitude, 1e-6f, $"the floor at {x:0.00}, {z:0.00}");
                    Assert.AreEqual(0f, grid.Sample(cells, new Vector3(x + 0.15f, y, z), Vector3.left).magnitude, 1e-6f, $"facing the wall at {x:0.00}, {z:0.00}");
                }
            Assert.AreEqual(0f, grid.Sample(cells, new Vector3(4f + LevelMetrics.WallThickness / 2f, y, 2f), Vector3.right).magnitude, 1e-6f, "the wall in the dark room");
            // and the lit room is lit to its walls
            for (float x = 0.1f; x < 3.85f; x += 0.11f)
                Assert.AreEqual(Lit.r, grid.Sample(cells, new Vector3(x, 0f, 2f), Vector3.up).x, 0.01f, $"the lit floor at {x:0.00}");
            Assert.AreEqual(Lit.r, grid.Sample(cells, new Vector3(4f - LevelMetrics.WallThickness / 2f, y, 2f), Vector3.left).x, 0.01f, "the wall in the lit room");
            // outside the house, over the roof, in the slab over the ceiling (the ducts): nothing
            Assert.AreEqual(0f, grid.Sample(cells, new Vector3(-0.2f, y, 2f), Vector3.left).magnitude);
            Assert.AreEqual(0f, grid.Sample(cells, new Vector3(2f, LevelMetrics.FloorHeight + 0.5f, 2f), Vector3.up).magnitude);
            Assert.AreEqual(0f, grid.Sample(cells, new Vector3(2f, LevelMetrics.WallHeight + 0.6f, 2f), Vector3.up).magnitude, "in the duct over a lit room");
        }

        [Test]
        public void AnOpenPassageLetsALittleThroughAndItFades()
        {
            var plan = TwoRooms(true);
            var grid = new RoomFill.Grid(plan);
            var cells = new uint[grid.Count];
            grid.Bake(new[] { Lit, Color.black }, cells);
            float last = Lit.r + 0.01f;
            for (float x = 3.2f; x < 7.5f; x += 0.1f)
            {
                float here = grid.Sample(cells, new Vector3(x, 0f, 2f), Vector3.up).x;
                Assert.LessOrEqual(here, last + 1e-4f, $"at {x:0.0}: it only fades away from the lit room");
                Assert.Less(last - here, Lit.r * 0.2f, $"at {x:0.0}: no edge on the floor");
                last = here;
            }
            Assert.Greater(grid.Sample(cells, new Vector3(4.4f, 0f, 2f), Vector3.up).x, Lit.r * 0.15f, "just past the passage");
            Assert.Less(grid.Sample(cells, new Vector3(7.5f, 0f, 2f), Vector3.up).x, Lit.r * 0.1f, "the far side of the dark room");
            // beside the passage the wall still stands
            Assert.Less(grid.Sample(cells, new Vector3(4.3f, 0f, 0.2f), Vector3.up).x, Lit.r * 0.12f, "behind the wall beside the passage");
        }

        [Test]
        public void ADoorKeepsItIn()
        {
            var plan = TwoRooms(false);
            plan.Openings.Add(new PlanOpening { Wall = plan.Walls.FindIndex(w => w.Separates(0, 1)), Kind = EdgeType.Door, At = 2f, Width = LevelMetrics.DoorWidth });
            var grid = new RoomFill.Grid(plan);
            var cells = new uint[grid.Count];
            grid.Bake(new[] { Lit, Color.black }, cells);
            Assert.AreEqual(0f, grid.Sample(cells, new Vector3(4.4f, 0f, 2f), Vector3.up).magnitude, 1e-6f);
        }

        [Test]
        public void InAHouseEveryCellOfADarkShutRoomIsBlack()
        {
            foreach (var (seed, house) in new[] { (7, 3), (42, 5), (99991, 4) })
            {
                var l = TestHouses.Get(seed, house);
                var plan = l.Plan;
                var grid = new RoomFill.Grid(plan, RoomFillField.Holes(l));
                var cells = new uint[grid.Count];
                // every other room lit
                var levels = plan.Rooms.Select(r => r.Id % 2 == 0 ? Lit : Color.black).ToArray();
                grid.Bake(levels, cells);
                // a dark room no open passage, hole or railing leads into
                var open = new System.Collections.Generic.HashSet<int>();
                foreach (var o in plan.Openings)
                    if (o.Kind is EdgeType.Open or EdgeType.Breach) { open.Add(plan.Walls[o.Wall].RoomA); open.Add(plan.Walls[o.Wall].RoomB); }
                foreach (var w in plan.Walls) if (w.Railing) { open.Add(w.RoomA); open.Add(w.RoomB); }
                int checkedRooms = 0;
                foreach (var r in plan.Rooms)
                {
                    if (r.Id % 2 == 0 || open.Contains(r.Id) || r.Levels > 1) continue;
                    checkedRooms++;
                    for (int z = 0; z < plan.NZ; z++)
                        for (int x = 0; x < plan.NX; x++)
                            if (plan.KindAt(r.Floor, x, z) == CellKind.Room && plan.IdAt(r.Floor, x, z) == r.Id)
                                Assert.AreEqual(0f, RoomFill.Unpack(cells[(r.Floor * plan.NZ + z) * plan.NX + x]).magnitude, 1e-6f, $"seed {seed}, room {r.Id} ({r.Type}), cell {x}, {z}");
                }
                Assert.Greater(checkedRooms, 2, $"seed {seed}: no shut dark rooms to check");
                // a lit room is lit in its middle
                var lit = plan.Rooms.First(r => r.Id % 2 == 0 && r.Levels == 1 && r.Area > 9f);
                Assert.Greater(grid.Sample(cells, lit.Center + Vector3.up * 0.01f, Vector3.up).x, Lit.r * 0.5f, $"seed {seed}: room {lit.Id}");
            }
        }
    }
}
