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
            m_Root = LevelGenerator.Build(m_Layout, art);
            var surface = m_Root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << LevelGenerator.CeilingLayer);
            surface.BuildNavMesh();
        }

        [TearDown]
        public void Cleanup()
        {
            NavMesh.RemoveAllNavMeshData();
            LevelGenerator.Clear();
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
            for (int f = 0; f < m_Layout.Floors; f++)
                for (int x = 0; x < m_Layout.Width; x++)
                    for (int y = 0; y < m_Layout.Height; y++)
                    {
                        var target = m_Layout.CellCenter(f, x, y);
                        Assert.IsTrue(Reach(m_Layout.HatchPosition, target, NavMesh.AllAreas), $"seed {seed}: room {x},{y} on floor {f}");
                    }
        }

        [Test]
        public void WithoutStairsAreaRobotsStayOnTheirFloor([Values(7, 99991)] int seed)
        {
            Build(seed, 2);
            int noStairs = NavMesh.AllAreas & ~(1 << LevelGenerator.StairsArea);
            var upstairs = m_Layout.CellCenter(1, m_Layout.Width / 2, m_Layout.Height / 2);
            Assert.IsTrue(Reach(m_Layout.HatchPosition, upstairs, NavMesh.AllAreas));
            Assert.IsFalse(Reach(m_Layout.HatchPosition, upstairs, noStairs), "wheels cannot climb stairs");
        }

        [Test]
        public void ItemSpotsAreOnTheNavMeshOrFurniture([Values(5, 123456789)] int seed)
        {
            Build(seed, 4);
            foreach (var spot in m_Layout.ItemSpots)
            {
                // Spots on desks are above the navmesh; there must be walkable floor within reach (1.6 m).
                Assert.IsTrue(NavMesh.SamplePosition(spot, out var hit, 1.6f, NavMesh.AllAreas), $"seed {seed}: item at {spot} unreachable");
                Assert.AreEqual(LevelLayout.FloorOf(spot.y), LevelLayout.FloorOf(hit.position.y), $"item at {spot} is reachable from the same floor");
            }
        }
    }
}
