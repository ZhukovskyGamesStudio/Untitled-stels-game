using NUnit.Framework;
using Stealth.Level;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Tests
{
    /// <summary>Builds real level geometry in the editor, bakes the navmesh and checks paths between floors.</summary>
    public class LevelNavMeshTests
    {
        GameObject m_Root;
        LevelLayout m_Layout;

        void Build(int seed, int level)
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            Assert.IsNotNull(art, "run Stealth/Build Prototype Assets first");
            m_Layout = LevelGenerator.Generate(seed, level);
            m_Root = ActiveLevel.Load(m_Layout, art);
            m_Root.GetComponent<NavMeshSurface>().BuildNavMesh(); // configured in the LevelRoot prefab
        }

        [TearDown]
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

        [Test]
        public void EveryFloorIsReachableFromTheHatchByStairs([Values(1, 42, 1337)] int seed)
        {
            Build(seed, 3);
            foreach (var r in m_Layout.Rooms)
                Assert.IsTrue(Reach(m_Layout.HatchPosition, r.Center, NavMesh.AllAreas), $"seed {seed}: {r.Type} ({r.Shape}, {r.Area:0} m²) on floor {r.Floor} at {r.Center}");
        }

        [Test]
        public void WithoutStairsAreaRobotsStayOnTheirFloor([Values(7, 99991)] int seed)
        {
            Build(seed, 2);
            int noStairs = NavMesh.AllAreas & ~(1 << LevelMetrics.StairsArea);
            var upstairs = LevelLayout.StairEnds(m_Layout.Stairs[0]).top;
            Assert.IsTrue(Reach(m_Layout.HatchPosition, upstairs, NavMesh.AllAreas));
            Assert.IsFalse(Reach(m_Layout.HatchPosition, upstairs, noStairs), "wheels cannot climb stairs");
        }

        [Test]
        public void RobotsCannotUseVents([Values(1, 2, 3)] int seed)
        {
            Build(seed, 4);
            int checkedVents = 0;
            foreach (var d in m_Layout.Doorways)
                    {
                        var edge = d.Kind;
                        if (edge != EdgeType.Vent && edge != EdgeType.Squeeze) continue; // crawl vents and narrow slots are for players only
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
            Assert.Greater(checkedVents, 0, "the level has vents");
        }

        [Test]
        public void ItemSpotsAreOnTheNavMeshOrFurniture([Values(5, 123456789)] int seed)
        {
            Build(seed, 4);
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
