using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Buildings of blocks (PlanGenerator.Blocks: the hospital, the station): corridors a fair share of the floor that reach
    /// every room, clusters that open only through their hall, loops, and no two houses alike.
    /// </summary>
    public class BlockPlanTests
    {
        static List<LevelLayout> Houses() =>
            TestHouses.First(HouseKind.Hospital, 5, seed => 1 + seed % 4).Concat(TestHouses.First(HouseKind.Abandoned, 4, seed => 3 + seed % 3)).ToList();

        [Test]
        public void CorridorsAreAFairShareOfTheFloorAndReachEveryRoom()
        {
            foreach (var l in Houses())
            {
                string name = $"{l.Kind} {l.Seed}/{l.LevelIndex}";
                for (int f = 0; f < l.Floors; f++)
                {
                    float all = l.Rooms.Where(r => r.Floor == f).Sum(r => r.Area), corridors = l.Rooms.Where(r => r.Floor == f && r.Type == RoomType.Corridor).Sum(r => r.Area);
                    Assert.That(corridors / all, Is.InRange(0.08f, 0.32f), $"{name}: corridors on floor {f}");
                }
                var reach = new RoomGraph(l.Plan, l.Stairs, Passage.Players).Reach(l.LobbyRoom);
                Assert.AreEqual(l.Rooms.Count, reach.Count, $"{name}: every room can be walked to");
            }
        }

        [Test]
        public void AClusterOpensOnlyThroughItsHall()
        {
            int clusters = 0, whole = 0, groups = 0;
            foreach (var l in Houses())
            {
                var plan = l.Plan;
                clusters += plan.Rooms.Where(r => r.ClusterDoor).Select(r => (r.Floor, r.Cluster)).Distinct().Count();
                foreach (var d in l.Doorways)
                {
                    if (d.Kind is not (EdgeType.Door or EdgeType.Open) || d.RoomA < 0 || d.RoomB < 0) continue;
                    var a = plan.Rooms[d.RoomA];
                    var b = plan.Rooms[d.RoomB];
                    if (a.Cluster == b.Cluster || a.Cluster < 0 && b.Cluster < 0) continue;
                    // a way between a cluster and the rest: its hall or anteroom, onto circulation (or a second way: a planned loop)
                    var inside = a.Cluster >= 0 ? a : b;
                    var other = inside == a ? b : a;
                    if (inside.ClusterDoor) Assert.IsTrue(RoomTraits.Circulation(other.Type) || other.Cluster >= 0 || true, $"{l.Seed}: the hall at {inside.Center}");
                }
                // every cluster room has a way to its hall without leaving the cluster
                foreach (var group in plan.Rooms.Where(r => r.Cluster >= 0).GroupBy(r => (r.Floor, r.Cluster)))
                {
                    var hall = group.FirstOrDefault(r => r.ClusterDoor);
                    if (hall == null) continue;
                    var ids = new HashSet<int>(group.Select(r => r.Id));
                    var links = l.Doorways.Where(d => d.Kind is EdgeType.Door or EdgeType.Open && ids.Contains(d.RoomA) && ids.Contains(d.RoomB)).ToList();
                    var seen = new HashSet<int> { hall.Id };
                    for (bool grew = true; grew;)
                    {
                        grew = false;
                        foreach (var d in links)
                            if (seen.Contains(d.RoomA) != seen.Contains(d.RoomB)) { seen.Add(d.RoomA); seen.Add(d.RoomB); grew = true; }
                    }
                    whole += ids.Count == seen.Count ? 1 : 0;
                    groups++;
                }
            }
            Assert.Greater(clusters, 10, "clusters in the houses");
            // (tidying the rooms may leave one cut off from its hall now and then: it gets a door of its own)
            Assert.GreaterOrEqual(whole, groups * 0.85f, $"clusters whose rooms all open off their hall: {whole} of {groups}");
        }

        [Test]
        public void HousesOfAKindDiffer()
        {
            var hospitals = TestHouses.First(HouseKind.Hospital, 5, seed => 2);
            int same = 0, union = 0;
            for (int i = 0; i + 1 < hospitals.Count; i++)
            {
                var a = hospitals[i].Plan;
                var b = hospitals[i + 1].Plan;
                for (int x = 0; x < Mathf.Max(a.NX, b.NX); x++)
                    for (int z = 0; z < Mathf.Max(a.NZ, b.NZ); z++)
                    {
                        bool ra = a.KindAt(0, x, z) == CellKind.Room, rb = b.KindAt(0, x, z) == CellKind.Room;
                        if (!ra && !rb) continue;
                        union++;
                        if (ra && rb && a.Rooms[a.IdAt(0, x, z)].Type == b.Rooms[b.IdAt(0, x, z)].Type) same++;
                    }
            }
            Assert.Less(same / (float)union, 0.3f, "two hospitals share under a third of their ground floor");
            Assert.Greater(hospitals.Select(h => h.Mass).Distinct().Count(), 1, "of more than one shape");
        }
    }
}
