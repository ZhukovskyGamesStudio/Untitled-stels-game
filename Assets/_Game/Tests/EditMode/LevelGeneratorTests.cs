using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class LevelGeneratorTests
    {
        static readonly int[] Seeds = { 1, 7, 42, 1337, 99991, 123456789 };

        [Test]
        public void SomeDesksBecomeSecurityTerminals()
        {
            foreach (var seed in Seeds)
            {
                var l = LevelGenerator.Generate(seed, 2);
                var again = LevelGenerator.Generate(seed, 2);
                CollectionAssert.AreEqual(l.Terminals, again.Terminals, "the same on every peer");
                if (!l.Props.Any(p => p.Kind is PropKind.Desk or PropKind.Terminal)) continue; // a house without desks
                Assert.IsTrue(l.Terminals.Exists(t => t.kind == TerminalKind.Hacking), $"seed {seed}: a hacking terminal");
                foreach (var (prop, _) in l.Terminals)
                {
                    Assert.AreEqual(PropKind.Terminal, l.Props[prop].Kind);
                    Assert.Greater((l.Props[prop].Position - l.HatchPosition).magnitude, 7.5f, "not in the lobby");
                }
            }
        }

        [Test]
        public void SameSeedGivesSameLayout()
        {
            foreach (var seed in Seeds)
            {
                var a = LevelGenerator.Generate(seed, 2);
                var b = LevelGenerator.Generate(seed, 2);
                Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
                Assert.AreEqual(a.Plan.Walls.Count, b.Plan.Walls.Count);
                Assert.AreEqual(a.Props.Count, b.Props.Count);
                Assert.AreEqual(a.ItemSpots.Count, b.ItemSpots.Count);
                Assert.AreEqual(a.HatchPosition, b.HatchPosition);
                for (int i = 0; i < a.Props.Count; i++) Assert.AreEqual(a.Props[i].Position, b.Props[i].Position);
            }
        }

        [Test]
        public void EveryRoomIsReachable()
        {
            foreach (var seed in Seeds)
                for (int level = 0; level < 5; level++)
                {
                    var l = LevelGenerator.Generate(seed, level);
                    var reached = Reachable(l);
                    var lost = l.Rooms.Where(r => !reached.Contains(r.Id)).Select(r => $"{r.Type} {r.Area:0} m² floor {r.Floor}").ToList();
                    Assert.IsEmpty(lost, $"seed {seed} level {level}: rooms out of reach");
                }
        }

        [Test]
        public void SecurityIsPlacedAndGrowsWithLevel()
        {
            var easy = LevelGenerator.Generate(5, 0);
            var hard = LevelGenerator.Generate(5, 4);
            Assert.Greater(easy.RobotSpawns.Count, 0);
            Assert.Greater(hard.RobotSpawns.Count, easy.RobotSpawns.Count);
            Assert.GreaterOrEqual(hard.CameraSpots.Count, easy.CameraSpots.Count);
        }

        [Test]
        public void SpawnsAreInsideTheBuilding()
        {
            foreach (var seed in Seeds)
            {
                var l = LevelGenerator.Generate(seed, 3);
                foreach (var p in l.PlayerSpawns) Assert.IsNotNull(l.RoomAt(p), $"spawn {p}");
                foreach (var p in l.ItemSpots) Assert.IsTrue(l.IsInside(p), $"item {p}");
                foreach (var r in l.RobotSpawns) Assert.IsTrue(l.IsInside(r.Position), $"robot {r.Kind} {r.Position}");
                foreach (var p in l.PatrolPoints) Assert.IsNotNull(l.RoomAt(p), $"patrol point {p}");
            }
        }

        [Test]
        public void FloorsAreConnectedByStairsInTheirRoomsCorners()
        {
            foreach (var seed in Seeds)
                for (int level = 0; level < 6; level++)
                {
                    var l = LevelGenerator.Generate(seed, level);
                    Assert.GreaterOrEqual(l.Floors, 2);
                    Assert.AreEqual(l.Floors - 1, l.Stairs.Count, $"seed {seed} level {level}");
                    foreach (var s in l.Stairs)
                    {
                        Assert.AreNotEqual(l.LobbyRoom, s.Room, "not in the lobby");
                        Assert.GreaterOrEqual(s.Room, 0);
                        Assert.GreaterOrEqual(s.RoomAbove, 0);
                        // the whole square is the room's floor below and the room's floor above (the hole)
                        var a = PlanGenerator.StairArea(s, -0.1f);
                        for (float x = a.xMin; x <= a.xMax; x += 0.5f)
                            for (float z = a.yMin; z <= a.yMax; z += 0.5f)
                            {
                                Assert.AreEqual(s.Room, l.Plan.SpaceAt(s.Floor, new Vector2(x, z)), $"seed {seed}: stair {s.Floor} at {x},{z}");
                                Assert.AreEqual(s.RoomAbove, l.Plan.SpaceAt(s.Floor + 1, new Vector2(x, z)), $"seed {seed}: arrival {s.Floor + 1} at {x},{z}");
                            }
                    }
                }
        }

        [Test]
        public void NothingIsPlacedOnTheStairs()
        {
            foreach (var seed in Seeds)
            {
                var l = LevelGenerator.Generate(seed, 5);
                foreach (var s in l.Stairs)
                {
                    var area = PlanGenerator.StairArea(s, 0.2f);
                    bool OnStair(Vector3 p)
                    {
                        int f = LevelLayout.FloorOf(p.y);
                        return (f == s.Floor || f == s.Floor + 1) && area.Contains(new Vector2(p.x, p.z));
                    }
                    foreach (var p in l.Props) Assert.IsFalse(OnStair(p.Position), $"seed {seed}: {p.Kind} on stairs at {p.Position}");
                    foreach (var p in l.ItemSpots) Assert.IsFalse(OnStair(p), $"seed {seed}: item on stairs at {p}");
                }
            }
        }

        [Test]
        public void RobotsAreSpreadOverAllFloors()
        {
            var l = LevelGenerator.Generate(3, 3);
            for (int f = 0; f < l.Floors; f++)
                Assert.IsTrue(l.RobotSpawns.Exists(r => r.Floor == f), $"floor {f} has a robot");
        }

        /// <summary>Flood fill over doors/open passages inside floors and stairs between floors, from the lobby.</summary>
        static HashSet<int> Reachable(LevelLayout l)
        {
            var seen = new HashSet<int> { l.LobbyRoom };
            var queue = new Queue<int>();
            queue.Enqueue(l.LobbyRoom);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                foreach (int n in l.Neighbours(c))
                    if (seen.Add(n)) queue.Enqueue(n);
            }
            return seen;
        }

        [Test]
        public void HighVentsCanBeClimbedTo()
        {
            // the climb in numbers: floor → low crate → high crate are jumps, the sill a step, the duct fits a crouched player
            const float jump = 0.9f, step = 0.35f, crouched = Stealth.Player.PlayerController.CrouchHeight;
            Assert.LessOrEqual(LevelMetrics.CrateLow, jump - 0.1f);
            Assert.LessOrEqual(LevelMetrics.CrateHigh - LevelMetrics.CrateLow, jump - 0.1f);
            Assert.LessOrEqual(LevelMetrics.HighVentSill - LevelMetrics.CrateHigh, step - 0.03f, "a step up from the crate into the duct");
            Assert.GreaterOrEqual(LevelMetrics.VentHeight, crouched + step, "room to step in crouched");
            Assert.LessOrEqual(LevelMetrics.HighVentSill + LevelMetrics.VentHeight, LevelMetrics.WallHeight - 0.2f, "under the ceiling");

            int high = 0, low = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var l = LevelGenerator.Generate(seed, 2);
                foreach (var o in l.Plan.Openings)
                {
                    if (!o.High) { if (o.Kind == EdgeType.Vent) low++; continue; }
                    Assert.AreEqual(EdgeType.Vent, o.Kind, "only vents go up");
                    Assert.IsTrue(LevelGenerator.CratesFit(l, o), $"seed {seed}: the crates under the vent fit");
                    high++;
                }
                Assert.AreEqual(l.Plan.Openings.Count(o => o.High), l.Doorways.Count(d => d.High), "doorways carry it");
            }
            Assert.Greater(high, 3, "some vents are up under the ceiling");
            Assert.Greater(low, 3, "and some stay low");
        }
    }
}
