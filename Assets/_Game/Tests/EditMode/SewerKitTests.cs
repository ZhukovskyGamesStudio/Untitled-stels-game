using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The sewer's kit pieces lie where <see cref="SewerLocation"/> expects them: each one runs from its origin along +X
    /// (the FBX import turns Blender +X into Unity −X, the builder turns the pieces back); the hall's tiles fit their cells
    /// and the sides' frames (<see cref="SewerHall"/>). Once they were mirrored: the
    /// hall overlapped the tunnel, the three tunnels ran back through it and their zones hung in the void behind the hall.
    /// </summary>
    public class SewerKitTests
    {
        const string Kit = "Assets/_Game/Prefabs/Level/Sewer/";

        /// <summary>The bounds of every mesh of a prefab, in the prefab root's space.</summary>
        static Bounds MeshBounds(GameObject root, System.Func<MeshFilter, bool> which = null)
        {
            var toRoot = root.transform.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || (which != null && !which(f))) continue;
                var m = toRoot * f.transform.localToWorldMatrix;
                var mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            Assert.IsTrue(any, $"{root.name} has meshes");
            return b;
        }

        static GameObject Load(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".prefab");
            Assert.IsNotNull(go, name);
            return go;
        }

        [Test]
        public void SegmentsRunAlongPlusX()
        {
            for (int i = 0; i < SewerPlan.Variants; i++)
            {
                var b = MeshBounds(Load($"Segment{i}"));
                Assert.AreEqual(0f, b.min.x, 0.15f, $"Segment{i} starts at its origin");
                Assert.AreEqual(SewerPlan.SegmentLength, b.max.x, 0.15f, $"Segment{i} ends where the next one starts");
            }
        }

        [Test]
        public void TheHallsTilesFitTheirCellsAndSides()
        {
            const float half = SewerHall.Cell / 2f;
            var floor = MeshBounds(Load("HallFloor"));
            Assert.AreEqual(-half, floor.min.x, 0.1f, "a floor tile covers its cell");
            Assert.AreEqual(half, floor.max.x, 0.1f);
            Assert.AreEqual(-half, floor.min.z, 0.1f);
            Assert.AreEqual(half, floor.max.z, 0.1f);
            Assert.AreEqual(0f, floor.max.y, 0.05f, "the road's grate on top");
            for (int h = 0; h < SewerHall.Heights; h++)
            {
                var vault = MeshBounds(Load($"HallVault{h}"));
                Assert.AreEqual(-half, vault.min.x, 0.1f, $"vault {h} covers its cell");
                Assert.AreEqual(half, vault.max.z, 0.1f);
                Assert.AreEqual(SewerHall.Springing[h], vault.min.y, 0.05f, $"vault {h} springs from the capitals");
                var column = MeshBounds(Load($"HallColumn{h}"));
                Assert.AreEqual(SewerHall.Springing[h], column.max.y, 0.05f, $"column {h} carries the vaults");
                Assert.LessOrEqual(column.max.x, 0.5f, "a column round a corner of the cells");
                foreach (var name in new[] { "HallWall", "HallMouth", "HallEntry" })
                {
                    var wall = MeshBounds(Load($"{name}{h}"));
                    Assert.AreEqual(0f, wall.max.x, 0.05f, $"{name}{h}: its outer face at the side's frame");
                    Assert.GreaterOrEqual(wall.min.x, -SewerHall.Wall - 0.2f, $"{name}{h}: in the wall, not in the hall");
                    Assert.AreEqual(-half, wall.min.z, 0.1f, $"{name}{h}: along the whole side");
                    Assert.AreEqual(half, wall.max.z, 0.1f);
                    Assert.Greater(wall.max.y, SewerHall.Springing[h] + 1f, $"{name}{h}: up into the vault");
                }
            }
            var branch = Load("Branch");
            var tunnel = MeshBounds(branch, f => f.transform.IsChildOf(branch.transform.Find("Tunnel")));
            Assert.GreaterOrEqual(tunnel.min.x, -0.2f, "a way leads away from the hall, not back through it");
            Assert.Greater(tunnel.max.x, SewerLocation.StopAt, "the tunnel goes past its stop wall");
            var zone = MeshBounds(branch, f => f.transform.IsChildOf(branch.transform.Find("WhenOpen")));
            Assert.Less(zone.max.x, tunnel.max.x, "the vote zone lies inside the tunnel");
            var plug = MeshBounds(Load("Plug"));
            Assert.Less(plug.max.x, 0.05f, "the plug sits in the wall, not in the tunnel");
        }

        [Test]
        public void TheClosedEndStandsBehindTheFirstSegment()
        {
            var cap = MeshBounds(Load("Cap"));
            Assert.AreEqual(-0.5f, cap.min.x, 0.15f, "its brick wall is behind x = 0, not inside the first segment");
            Assert.Less(cap.max.x, 1.5f, "only the outlet pipe reaches into the tunnel");
        }

        [Test]
        public void TheGatesHazardLampsFaceTheCar()
        {
            var gate = Load("Gate");
            var glow = MeshBounds(gate, f => f.name.StartsWith("Glow"));
            Assert.Less(glow.center.x, 0f, "the lamps hang on the car's side, like the hazard lights");
            var spots = gate.GetComponent<SewerGate>().HazardLights;
            Assert.IsTrue(spots.All(l => gate.transform.InverseTransformPoint(l.transform.position).x < 0f));
        }
    }
}
