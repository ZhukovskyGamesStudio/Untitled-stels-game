using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    public class LevelGeneratorTests
    {
        [Test]
        public void SomeDesksBecomeSecurityTerminals()
        {
            foreach (var l in TestHouses.Sample)
            {
                int seed = l.Seed;
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
            // one house generated again (the shared one is the first): every peer builds the same house from the seed
            {
                var a = TestHouses.Get(42, 2);
                var b = LevelGenerator.Generate(42, 2);
                Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
                Assert.AreEqual(a.Plan.Walls.Count, b.Plan.Walls.Count);
                Assert.AreEqual(a.Props.Count, b.Props.Count);
                Assert.AreEqual(a.ItemSpots.Count, b.ItemSpots.Count);
                Assert.AreEqual(a.HatchPosition, b.HatchPosition);
                for (int i = 0; i < a.Props.Count; i++) Assert.AreEqual(a.Props[i].Position, b.Props[i].Position);
                CollectionAssert.AreEqual(a.Terminals, b.Terminals, "the terminals too");
            }
        }

        [Test]
        public void EveryRoomIsReachable()
        {
            foreach (var l in TestHouses.Sample)
                {
                    int seed = l.Seed, level = l.LevelIndex;
                    var reached = Reachable(l);
                    // the hidden room: only players get in (through its illusion walls)
                    if (l.HiddenRoom >= 0)
                        Assert.IsTrue(HouseFeatures.Reach(l.Plan, l.Stairs, l.LobbyRoom, true).Contains(l.HiddenRoom), $"seed {seed} level {level}: the hidden room is reached by players");
                    var lost = l.Rooms.Where(r => r.Id != l.HiddenRoom && !reached.Contains(r.Id)).Select(r => $"{r.Type} {r.Area:0} m² floor {r.Floor}").ToList();
                    Assert.IsEmpty(lost, $"seed {seed} level {level}: rooms out of reach");
                }
        }

        [Test]
        public void SecurityIsPlacedAndGrowsWithLevel()
        {
            var easy = TestHouses.Get(7, 1);
            var hard = TestHouses.Get(7, 4);
            Assert.Greater(easy.RobotSpawns.Count, 0);
            Assert.Greater(hard.RobotSpawns.Count, easy.RobotSpawns.Count);
            Assert.GreaterOrEqual(hard.CameraSpots.Count, easy.CameraSpots.Count);
        }

        [Test]
        public void SpawnsAreInsideTheBuilding()
        {
            foreach (var l in TestHouses.Sample)
            {
                foreach (var p in l.PlayerSpawns) Assert.IsNotNull(l.RoomAt(p), $"spawn {p}");
                foreach (var p in l.ItemSpots) Assert.IsTrue(l.IsInside(p), $"item {p}");
                foreach (var r in l.RobotSpawns) Assert.IsTrue(l.IsInside(r.Position), $"robot {r.Kind} {r.Position}");
                foreach (var p in l.PatrolPoints) Assert.IsNotNull(l.RoomAt(p), $"patrol point {p}");
            }
        }

        [Test]
        public void FloorsAreConnectedByStairsInTheirRoomsCorners()
        {
            foreach (var l in TestHouses.Sample)
                {
                    int seed = l.Seed, level = l.LevelIndex;
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
            foreach (var l in TestHouses.Sample)
            {
                int seed = l.Seed;
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
            var l = TestHouses.Get(1337, 3);
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
            // the climb in numbers: floor → desk is a jump, desk → sill a crouched step, the duct fits a crouched player
            const float jump = 0.9f, step = 0.35f, crouched = Stealth.Player.PlayerController.CrouchHeight;
            Assert.LessOrEqual(LevelMetrics.DeskTop, jump - 0.1f);
            Assert.LessOrEqual(LevelMetrics.HighVentSill - LevelMetrics.DeskTop, step - 0.03f, "a step up from the desk into the duct");
            Assert.GreaterOrEqual(LevelMetrics.VentHeight, crouched + step, "room to step in crouched");
            Assert.LessOrEqual(LevelMetrics.HighVentSill + LevelMetrics.VentHeight, LevelMetrics.WallHeight - 0.2f, "under the ceiling");

            int high = 0, low = 0;
            foreach (var l in TestHouses.Sample)
            {
                int seed = l.Seed;
                foreach (var o in l.Plan.Openings)
                {
                    if (!o.High) { if (o.Kind == EdgeType.Vent) low++; continue; }
                    Assert.AreEqual(EdgeType.Vent, o.Kind, "only vents go up");
                    Assert.IsTrue(LevelGenerator.DesksFit(l, o), $"seed {seed}: the desks under the vent fit");
                    // a desk on each side, back to the wall under the hole
                    var w = l.Plan.Walls[o.Wall];
                    var mid = w.At(o.At);
                    int desks = l.Props.Count(p => p.Kind is PropKind.Desk or PropKind.Terminal &&
                        (new Vector2(p.Position.x, p.Position.z) - mid).magnitude < 0.75f && LevelLayout.FloorOf(p.Position.y) == w.Floor);
                    Assert.AreEqual(2, desks, $"seed {seed}: desks under the high vent at {mid}");
                    high++;
                }
                Assert.AreEqual(l.Plan.Openings.Count(o => o.High), l.Doorways.Count(d => d.High), "doorways carry it");
            }
            Assert.Greater(high, 0, "some vents are up under the ceiling");
            Assert.Greater(low, 0, "and some stay low");
        }

        [Test]
        public void EveryHouseHasTheExitHatchFarFromTheEntry()
        {
            var problems = new List<string>();
            // the shared houses, and one for a team of four (a bigger house)
            foreach (var l in TestHouses.Sample.Append(TestHouses.Get(42, 2, 4)))
                    {
                        int seed = l.Seed, level = l.LevelIndex, players = l.Players;
                        var hatches = l.Props.Where(p => p.Kind == PropKind.Hatch).ToList();
                        string at = $"seed {seed} house {level} players {players}";
                        if (hatches.Count != 2) problems.Add($"{at}: {hatches.Count} hatches");
                        if (!hatches.Any(h => (h.Position - l.ExitHatchPosition).sqrMagnitude < 0.01f)) problems.Add($"{at}: no hatch at the exit");
                        if ((l.ExitHatchPosition - l.HatchPosition).magnitude < 6f) problems.Add($"{at}: exit {(l.ExitHatchPosition - l.HatchPosition).magnitude:0.0} m from the entry");
                        var room = l.RoomAt(l.ExitHatchPosition);
                        if (room == null || room.Floor != 0) problems.Add($"{at}: the exit hatch at {l.ExitHatchPosition} is not in a ground floor room");
                    }
            Assert.IsEmpty(problems, string.Join("; ", problems));
        }
    }
}
