using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Which rooms an eye sees into (RoomSight): what the rooms' furniture is drawn by. It may see too much, never too
    /// little — a room a straight line reaches is always seen — and it must leave most of a big house out.
    /// </summary>
    public class RoomSightTests
    {
        static List<LevelLayout> Houses() =>
            TestHouses.First(HouseKind.Hospital, 2, seed => 1 + seed % 4).Concat(TestHouses.Offices(2)).ToList();

        static Vector3 EyeIn(PlanRoom r) => r.Center + Vector3.up * 1.6f;

        [Test]
        public void ARoomAStraightLineReachesIsSeenAndMostOfTheHouseIsNot()
        {
            foreach (var l in Houses())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                var plan = l.Plan;
                var sight = new RoomSight(l);
                var seen = new bool[sight.Rooms];
                var rooms = plan.Rooms.Where(r => r.Area >= 6f).ToList();
                var rng = new System.Random(l.Seed);
                float share = 0f;
                int looks = 0;
                for (int k = 0; k < 40; k++)
                {
                    var a = rooms[rng.Next(rooms.Count)];
                    Assert.IsTrue(sight.See(EyeIn(a), seen), $"{name}: an eye in room {a.Id} is inside");
                    Assert.IsTrue(seen[a.Id], $"{name}: the eye's own room {a.Id}");
                    var from = new Vector2(a.Center.x, a.Center.z);
                    foreach (var b in rooms)
                    {
                        if (b.Floor != a.Floor || b == a) continue;
                        if (plan.CanSee(a.Floor, from, new Vector2(b.Center.x, b.Center.z)))
                            Assert.IsTrue(seen[b.Id], $"{name}: room {b.Id} ({b.Type}) is in plain sight of room {a.Id} ({a.Type})");
                    }
                    foreach (var d in l.Doorways)
                        if (d.RoomA == a.Id && d.RoomB >= 0 && d.Floor == a.Floor && (new Vector2(d.Position.x, d.Position.z) - from).magnitude < RoomSight.NearDoor - 0.1f)
                            Assert.IsTrue(seen[d.RoomB], $"{name}: the room behind a doorway by the eye in room {a.Id}");
                    share += seen.Count(s => s) / (float)seen.Length;
                    looks++;
                }
                Assert.Less(share / looks, 0.4f, $"{name}: the share of the house an eye sees into, on average");
            }
        }

        /// <summary>Shut doors hide the rooms behind them — but never the eye's own room, and a door with a slot only from afar.</summary>
        [Test]
        public void ShutDoorsHideTheRoomsBehindThem()
        {
            foreach (var l in Houses())
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                var plan = l.Plan;
                var sight = new RoomSight(l);
                bool[] open = new bool[sight.Rooms], shut = new bool[sight.Rooms], slot = new bool[sight.Rooms];
                var blind = new byte[sight.Openings];
                var slots = new byte[sight.Openings];
                for (int i = 0; i < blind.Length; i++)
                    if (plan.Openings[i].Kind == EdgeType.Door) { blind[i] = RoomSight.Shut; slots[i] = RoomSight.ShutSlot; }
                var rooms = plan.Rooms.Where(r => r.Area >= 6f).ToList();
                var rng = new System.Random(l.Seed + 1);
                float shareOpen = 0f, shareShut = 0f;
                for (int k = 0; k < 30; k++)
                {
                    var a = rooms[rng.Next(rooms.Count)];
                    sight.See(EyeIn(a), open);
                    sight.See(EyeIn(a), shut, blind);
                    sight.See(EyeIn(a), slot, slots);
                    Assert.IsTrue(shut[a.Id], $"{name}: the eye's own room {a.Id} behind shut doors");
                    for (int i = 0; i < open.Length; i++)
                    {
                        if (shut[i]) Assert.IsTrue(slot[i], $"{name}: room {i} seen past blind doors is seen past doors with a slot");
                        if (slot[i]) Assert.IsTrue(open[i], $"{name}: room {i} seen past shut doors is seen past open ones");
                    }
                    shareOpen += open.Count(s => s);
                    shareShut += shut.Count(s => s);
                }
                // (an office of open plans and open passages has fewer doors to shut than a hospital)
                Assert.Less(shareShut, shareOpen * 0.9f, $"{name}: rooms seen with every door shut ({shareShut}) against every door open ({shareOpen})");
            }
        }

        [Test]
        public void AStairShowsTheStoreyItLeadsTo()
        {
            foreach (var l in Houses())
            {
                var sight = new RoomSight(l);
                var seen = new bool[sight.Rooms];
                foreach (var s in l.Stairs)
                {
                    if (s.Room < 0 || s.RoomAbove < 0) continue;
                    sight.See(EyeIn(l.Plan.Rooms[s.Room]), seen);
                    Assert.IsTrue(seen[s.RoomAbove], $"{l.Seed}/{l.LevelIndex}: from the foot of a stair in room {s.Room}, the room it arrives in");
                    sight.See(EyeIn(l.Plan.Rooms[s.RoomAbove]), seen);
                    Assert.IsTrue(seen[s.Room], $"{l.Seed}/{l.LevelIndex}: from the head of a stair in room {s.RoomAbove}, the room it starts in");
                }
            }
        }

        [Test]
        public void FromTheStreetEverythingShows()
        {
            foreach (var l in Houses())
            {
                var sight = new RoomSight(l);
                Assert.IsFalse(sight.See(new Vector3(-6f, 1.6f, -6f), new bool[sight.Rooms]), $"{l.Seed}/{l.LevelIndex}: an eye on the street");
            }
        }
    }
}
