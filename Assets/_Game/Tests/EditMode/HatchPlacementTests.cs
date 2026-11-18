using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The hatches are where a building meets the sewer, as in a real one: the entry in the plant room off the lobby at the
    /// outer wall (the drain's way out to the street), the exit in a far room with drains — always in a corner by the
    /// drain stack, never in the middle of a room.
    /// </summary>
    public class HatchPlacementTests
    {
        /// <summary>The distance from a point to the nearest wall line of its floor (railings are no walls).</summary>
        static float NearestWall(LevelLayout l, Vector3 p)
        {
            int f = LevelLayout.FloorOf(p.y + 0.1f);
            var q = new Vector2(p.x, p.z);
            float best = float.MaxValue;
            foreach (var w in l.Plan.Walls)
                if (w.Floor == f && !w.Railing) best = Mathf.Min(best, Stealth.Enemies.SpiderWebs.DistanceToSegment(q, w.A, w.B));
            return best;
        }

        [Test]
        public void TheTeamComesUpInThePlantRoomOffTheLobby()
        {
            foreach (var l in TestHouses.Sample)
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                var room = l.Plan.RoomAt(l.HatchPosition + Vector3.up * 0.3f);
                Assert.IsNotNull(room, name);
                Assert.AreEqual(RoomType.Utility, room.Type, $"{name}: the entry hatch is in the plant room");
                Assert.AreEqual(1, l.Rooms.Count(r => r.Type == RoomType.Utility), $"{name}: one plant room");
                Assert.IsTrue(l.Doorways.Any(d => (d.RoomA == room.Id && d.RoomB == l.LobbyRoom) || (d.RoomB == room.Id && d.RoomA == l.LobbyRoom)),
                    $"{name}: a door into the lobby");
                Assert.IsTrue(RoomRules.OutsideOf(l.Plan, room) > 0f, $"{name}: at the outer wall, where the drain leaves the building");
                Assert.IsFalse(l.Doorways.Any(d => d.Exterior && (d.RoomA == room.Id || d.RoomB == room.Id)), $"{name}: no street door of its own");
                Assert.Less(NearestWall(l, l.HatchPosition), LevelGenerator.HatchFromWall + 0.3f, $"{name}: by a wall");
                // the team comes up from the basement under it (its shaft's lid is where the entry hatch was)
                Assert.IsTrue(l.HasBasement, name);
                foreach (var p in l.PlayerSpawns) Assert.IsTrue(Basement.Contains(l.Cellar, p), $"{name}: the team waits in the basement");
            }
        }

        [Test]
        public void TheExitIsInACornerOfAFarRoomWithDrains()
        {
            int wet = 0;
            foreach (var l in TestHouses.Sample)
            {
                string name = $"{l.Seed}/{l.LevelIndex}";
                var room = l.Plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f);
                Assert.IsNotNull(room, name);
                Assert.Less(NearestWall(l, l.ExitHatchPosition), LevelGenerator.HatchFromWall + 0.3f, $"{name}: the exit hatch stands by a wall, not in the middle of the {room.Type}");
                if (room.Type is RoomType.Restroom or RoomType.Kitchen or RoomType.LockerRoom or RoomType.Workshop) wet++;
                if (l.Kind == HouseKind.Garage)
                {
                    // the workshop's oil separator chamber when the crawl ducts reach it (VentPlan: the double-height hall has no
                    // void over a ceiling of its own, mostly not), else another room the ducts reach
                    if (room.Type == RoomType.Workshop)
                        Assert.Greater(l.ExitHatchPosition.z, room.Bounds.center.y, $"{name}: at the back, away from the bays");
                }
            }
            Assert.Greater(wet, 0, "drains are where the exits are");
        }
    }
}
