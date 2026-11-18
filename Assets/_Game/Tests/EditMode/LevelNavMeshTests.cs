using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Tests
{
    /// <summary>Builds real level geometry in the editor, bakes the navmesh and checks paths between floors.</summary>
    [TestFixture(99991, 4)] // a hospital
    [TestFixture(7, 1)]     // an auto repair shop: the hall, its cars and lifts, the gallery
    [TestFixture(12, 2)]    // an apartment block: the flats off the landings, the stacked stairwell
    public class LevelNavMeshTests
    {
        // one shared house, built and baked once for the fixture: every test only asks the navmesh
        readonly int Seed, Level;
        LevelLayout m_Layout;

        public LevelNavMeshTests(int seed, int level)
        {
            Seed = seed;
            Level = level;
        }

        [OneTimeSetUp]
        public void Build()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            Assert.IsNotNull(art, "run Stealth/Build Prototype Assets first");
            m_Layout = TestHouses.Get(Seed, Level);
            ActiveLevel.Load(m_Layout, art).GetComponent<NavMeshSurface>().BuildNavMesh(); // configured in the LevelRoot prefab
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            NavMesh.RemoveAllNavMeshData();
            ActiveLevel.Clear();
        }

        static bool Reach(Vector3 from, Vector3 to, int areaMask)
        {
            if (!NavMesh.SamplePosition(from, out var a, 2f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(to, out var b, 2f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(a.position, b.position, areaMask, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>The room's floor clear of furniture, nearest to <paramref name="p"/> first.</summary>
        System.Collections.Generic.IEnumerable<Vector3> OpenFloor(PlanRoom r, Vector3 p)
        {
            var cells = new System.Collections.Generic.List<Vector3>();
            for (float x = r.Bounds.xMin + 0.5f; x < r.Bounds.xMax - 0.4f; x += 0.5f)
                for (float z = r.Bounds.yMin + 0.5f; z < r.Bounds.yMax - 0.4f; z += 0.5f)
                {
                    var q = new Vector3(x, p.y, z);
                    if (m_Layout.Plan.SpaceAt(r.Floor, new Vector2(x, z)) == r.Id && Stealth.Items.LootPlanner.ClearOfFurniture(m_Layout, q, r.Floor, 0.3f)) cells.Add(q);
                }
            return cells.OrderBy(q => (q - p).sqrMagnitude);
        }

        [Test]
        public void EveryFloorIsReachableFromTheHatchByStairs()
        {
            int seed = Seed;
            foreach (var r in m_Layout.Rooms)
                if (r.Id != m_Layout.HiddenRoom) // robots never get into the hidden room
                {
                    // the exit hatch stands in the middle of its room: walk up to it
                    var target = (r.Center - m_Layout.ExitHatchPosition).sqrMagnitude < 0.1f ? r.Center + new Vector3(1.6f, 0f, 0f) : r.Center;
                    // a piece standing on the middle (the hostage's cage, a table): any open floor of the room will do
                    // (a strip walled in behind such a piece is a pocket nobody needs, not a room out of reach)
                    if (!Stealth.Items.LootPlanner.ClearOfFurniture(m_Layout, target, r.Floor, 0.3f))
                        foreach (var q in OpenFloor(r, target))
                            if (Reach(m_Layout.HatchPosition, q, NavMesh.AllAreas)) { target = q; break; }
                    Assert.IsTrue(Reach(m_Layout.HatchPosition, target, NavMesh.AllAreas), $"seed {seed}: {r.Type} ({r.Shape}, {r.Area:0} m²) on floor {r.Floor} at {r.Center}" +
                        $" (exit hatch {(r.Center - m_Layout.ExitHatchPosition).magnitude:0.0} m away, hidden {m_Layout.HiddenRoom}, key rooms {string.Join(",", m_Layout.KeyDoors.SelectMany(k => k.Rooms))}, room {r.Id}, POI {m_Layout.PointOfInterest})");
                }
        }

        [Test]
        public void WithoutStairsAreaRobotsStayOnTheirFloor()
        {
            int noStairs = NavMesh.AllAreas & ~(1 << LevelMetrics.StairsArea);
            var upstairs = LevelLayout.StairEnds(m_Layout.Stairs[0]).top;
            Assert.IsTrue(Reach(m_Layout.HatchPosition, upstairs, NavMesh.AllAreas));
            Assert.IsFalse(Reach(m_Layout.HatchPosition, upstairs, noStairs), "wheels cannot climb stairs");
        }

        [Test]
        public void RobotsCannotUseVents()
        {
            int seed = Seed;
            int checkedVents = 0;
            foreach (var d in m_Layout.Doorways)
                    {
                        var edge = d.Kind;
                        if (edge is not (EdgeType.Vent or EdgeType.Squeeze or EdgeType.Hole or EdgeType.InnerWindow)) continue; // crawl vents, slots, holes, inner windows: players only
                        // Points right on both sides of the opening.
                        var across = d.Rotation * Vector3.forward;
                        var a = d.Position - across * 0.8f;
                        var b = d.Position + across * 0.8f;
                        var at = d.Position;
                        int f = d.Floor;
                        if (!NavMesh.SamplePosition(a, out var ha, 0.6f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b, out var hb, 0.6f, NavMesh.AllAreas)) continue;
                        var path = new NavMeshPath();
                        NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
                        float len = 0f;
                        for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                        Assert.IsTrue(path.status != NavMeshPathStatus.PathComplete || len > 3f, $"seed {seed}: robots get through the {edge} at {at} floor {f}");
                        checkedVents++;
                    }
            if (m_Layout.Kind != HouseKind.Apartments) Assert.Greater(checkedVents, 0, "the level has vents"); // (a block of flats has none)
        }

        [Test]
        public void ItemSpotsAreOnTheNavMeshOrFurniture()
        {
            int seed = Seed;
            foreach (var spot in m_Layout.ItemSpots)
            {
                // Spots on desks / shelves are above the floor: there must be walkable floor of the same storey nearby.
                int floor = LevelLayout.FloorOf(spot.y);
                var atFloor = new Vector3(spot.x, LevelLayout.FloorY(floor), spot.z);
                Assert.IsTrue(NavMesh.SamplePosition(atFloor, out var hit, 1.5f, NavMesh.AllAreas), $"seed {seed}: item at {spot} unreachable");
                Assert.AreEqual(floor, LevelLayout.FloorOf(hit.position.y), $"item at {spot} is reachable from its own floor");
            }
        }
    }
}
