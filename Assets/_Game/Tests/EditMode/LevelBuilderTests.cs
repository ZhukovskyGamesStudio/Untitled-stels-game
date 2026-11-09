using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Geometry built from prefabs: stair placement, lights, lamps, the built level itself.</summary>
    public class LevelBuilderTests
    {
        [TearDown]
        public void Cleanup() => ActiveLevel.Clear();

        static readonly Vector2Int[] Corners = { new(1, 1), new(-1, -1), new(1, -1), new(-1, 1) };

        [Test]
        public void StairPrefabPlacementMatchesTheCornerFrame([ValueSource(nameof(Corners))] Vector2Int corner)
        {
            var stair = new Stair { Floor = 1, Cell = new Vector2Int(2, 1), Corner = corner };
            var (mirrored, yaw) = StairGeometry.Placement(corner);
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var box in StairGeometry.Boxes())
            {
                // Where the prefab puts the box (prefab local: mirrored variant flips x) ...
                var local = box.Center;
                if (mirrored) local.x = -local.x;
                var placed = StairGeometry.Origin(stair) + rotation * local;
                // ... must be where the corner frame says it is.
                var expected = StairGeometry.ToWorld(stair, box.Center);
                Assert.That(Vector3.Distance(placed, expected), Is.LessThan(1e-3f), $"{box.Name} for corner {corner}");
            }
        }

        [Test]
        public void StairsFitIntoTheirCornerAndClimbOneFloor()
        {
            float top = 0f;
            foreach (var box in StairGeometry.Boxes())
            {
                var min = box.Center - box.Size / 2f;
                var max = box.Center + box.Size / 2f;
                Assert.GreaterOrEqual(min.x, 0f, box.Name);
                Assert.GreaterOrEqual(min.z, 0f, box.Name);
                Assert.LessOrEqual(max.x, LevelMetrics.StairSize + 0.1f, box.Name);
                Assert.LessOrEqual(max.z, LevelMetrics.StairSize + 0.1f, box.Name);
                if (box.Name.StartsWith("Step")) top = Mathf.Max(top, max.y);
            }
            Assert.AreEqual(LevelMetrics.FloorHeight, top, 1e-3f, "the last step reaches the next floor");
        }

        [Test]
        public void DarkRoomsAreDimServerRoomsAreBlue()
        {
            var (_, dark) = RoomLighting.For(RoomType.Office, true);
            var (_, lit) = RoomLighting.For(RoomType.Office, false);
            Assert.Less(dark, lit * 0.25f);
            var (server, _) = RoomLighting.For(RoomType.Server, false);
            Assert.Greater(server.b, server.r);
        }

        [Test]
        public void StreetLampsSurroundTheBuilding()
        {
            var spots = LevelBuilder.StreetLampSpots(30f, 40f);
            Assert.Greater(spots.Count, 8);
            foreach (var p in spots)
            {
                bool outside = p.x < 0f || p.x > 30f || p.z < 0f || p.z > 40f;
                Assert.IsTrue(outside, $"lamp at {p} is outside the walls");
            }
        }

        [Test]
        public void BuiltLevelIsMadeOfPrefabInstances()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var layout = LevelGenerator.Generate(77, 3);
            var root = ActiveLevel.Load(layout, art);
            Assert.AreSame(layout, ActiveLevel.Layout);
            Assert.AreEqual(layout.Stairs.Count, CountNamed(root, "Stairs_"), "one staircase prefab per stair");
            Assert.AreEqual(layout.AutoDoors.Count, root.GetComponentsInChildren<AutoDoor>().Length);
            Assert.AreEqual(layout.Floors * layout.Width * layout.Height, root.GetComponentsInChildren<RoomLight>().Length, "a light per room");
            Assert.IsNotNull(root.GetComponent<Unity.AI.Navigation.NavMeshSurface>(), "the root carries the navmesh surface");
            int vents = 0;
            foreach (var e in layout.EdgesX) if (e == EdgeType.Vent) vents++;
            foreach (var e in layout.EdgesZ) if (e == EdgeType.Vent) vents++;
            Assert.AreEqual(vents, CountNamed(root, "VentFrame"));
            ActiveLevel.Clear();
            Assert.IsNull(ActiveLevel.Layout);
        }

        static int CountNamed(GameObject root, string prefix)
        {
            int n = 0;
            foreach (Transform t in root.transform) if (t.name.StartsWith(prefix)) n++;
            return n;
        }
    }
}
