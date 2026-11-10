using System.Collections.Generic;
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
            Assert.AreEqual(layout.AutoDoors.Count + layout.ExteriorDoors.Count, root.GetComponentsInChildren<AutoDoor>().Length, "inner sliding doors + a door in every exit");
            Assert.AreEqual(layout.Floors * layout.Width * layout.Height, root.GetComponentsInChildren<RoomLight>().Length, "ceiling lights in every room");
            Assert.Greater(root.GetComponentsInChildren<LightSource>().Length, layout.Floors * layout.Width * layout.Height, "plus exit signs and lamps");
            Assert.IsNotNull(root.GetComponent<Unity.AI.Navigation.NavMeshSurface>(), "the root carries the navmesh surface");
            int vents = 0;
            foreach (var e in layout.EdgesX) if (e == EdgeType.Vent) vents++;
            foreach (var e in layout.EdgesZ) if (e == EdgeType.Vent) vents++;
            Assert.AreEqual(vents, CountNamed(root, "VentFrame"));
            ActiveLevel.Clear();
            Assert.IsNull(ActiveLevel.Layout);
        }

        static readonly HashSet<string> Floors = new() { "Floor", "Slab", "Roof", "OutsideGround", "Fence" };

        /// <summary>
        /// Furniture must not stick into walls, door frames, stairs or other furniture. Every model part is checked as its
        /// own oriented box (the mesh bounds of that part), with 2 cm of tolerance for things that only touch. Facade windows
        /// (blinds and sills stick into the room) count as well.
        /// </summary>
        [Test]
        public void FurnitureDoesNotClip([Values(3, 21, 77, 140, 555)] int seed)
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var layout = LevelGenerator.Generate(seed, 4);
            var root = ActiveLevel.Load(layout, art);
            var scratch = new GameObject("ClipProbe");
            try
            {
                var parts = new List<(string owner, BoxCollider box)>();
                var solids = new List<(string owner, Collider col)>();
                int index = 0;
                foreach (Transform child in root.transform)
                {
                    index++;
                    string owner = $"{child.name}#{index}@{child.position}";
                    if (child.name == "Window" || System.Enum.TryParse<PropKind>(child.name, out _))
                    {
                        foreach (var mf in child.GetComponentsInChildren<MeshFilter>())
                        {
                            var probe = new GameObject("Probe");
                            probe.transform.SetParent(scratch.transform, false);
                            probe.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                            probe.transform.localScale = mf.transform.lossyScale;
                            var box = probe.AddComponent<BoxCollider>();
                            var b = mf.sharedMesh.bounds;
                            box.center = b.center;
                            box.size = Vector3.Max(b.size - Vector3.one * 0.04f, Vector3.one * 0.001f);
                            parts.Add((owner, box));
                        }
                    }
                    else if (!Floors.Contains(child.name))
                        foreach (var col in child.GetComponentsInChildren<Collider>())
                            if (col.enabled && !col.isTrigger) solids.Add((owner, col));
                }
                Physics.SyncTransforms();

                var hits = new List<string>();
                bool Overlap(Collider a, Collider b) => Physics.ComputePenetration(
                    a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out _);
                for (int i = 0; i < parts.Count; i++)
                {
                    var a = parts[i];
                    var ab = a.box.bounds;
                    // Windows sit inside the walls on purpose; they only must not run into furniture.
                    if (!a.owner.StartsWith("Window"))
                        foreach (var s in solids)
                            if (ab.Intersects(s.col.bounds) && Overlap(a.box, s.col)) hits.Add($"{a.owner} x {s.owner}");
                    for (int j = i + 1; j < parts.Count; j++)
                    {
                        var b = parts[j];
                        if (b.owner == a.owner || !ab.Intersects(b.box.bounds)) continue;
                        if (Overlap(a.box, b.box)) hits.Add($"{a.owner} x {b.owner}");
                    }
                }
                var unique = new List<string>(new HashSet<string>(hits));
                Assert.IsEmpty(unique, $"{unique.Count} clipping pairs, e.g.\n" + string.Join("\n", unique.GetRange(0, Mathf.Min(25, unique.Count))));
            }
            finally { Object.DestroyImmediate(scratch); }
        }

        /// <summary>Windows sit in the outer walls with their blinds (local +Z) facing into the building.</summary>
        [Test]
        public void WindowsFaceIntoTheBuilding()
        {
            var spots = LevelBuilder.WindowSpots(3, 4, 1.7f);
            var centre = new Vector3(15f, 1.7f, 20f);
            Assert.AreEqual(2 * 2 * (3 + 4), spots.Count);
            foreach (var (pos, yaw) in spots)
            {
                var inward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var toCentre = (centre - pos);
                Assert.Greater(Vector3.Dot(inward, toCentre), 0f, $"window at {pos}");
                bool onWall = Mathf.Approximately(pos.x, 0f) || Mathf.Approximately(pos.x, 30f) || Mathf.Approximately(pos.z, 0f) || Mathf.Approximately(pos.z, 40f);
                Assert.IsTrue(onWall, $"window at {pos} is on an outer wall");
            }
        }

        static int CountNamed(GameObject root, string prefix)
        {
            int n = 0;
            foreach (Transform t in root.transform) if (t.name.StartsWith(prefix)) n++;
            return n;
        }
    }
}
