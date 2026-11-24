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

        /// <summary>Every mesh vertex of a prefab, set where <paramref name="place"/> puts its root.</summary>
        static Vector3[] Vertices(GameObject root, Matrix4x4 place)
        {
            var toRoot = root.transform.worldToLocalMatrix;
            return root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null)
                .SelectMany(f => { var m = place * toRoot * f.transform.localToWorldMatrix; return f.sharedMesh.vertices.Select(v => m.MultiplyPoint3x4(v)); })
                .ToArray();
        }

        /// <summary>Every mesh triangle of a prefab (its three corners), in the prefab root's space.</summary>
        static Vector3[][] Triangles(GameObject root)
        {
            var toRoot = root.transform.worldToLocalMatrix;
            return root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).SelectMany(f =>
            {
                var m = toRoot * f.transform.localToWorldMatrix;
                var v = f.sharedMesh.vertices.Select(p => m.MultiplyPoint3x4(p)).ToArray();
                var t = f.sharedMesh.triangles;
                return Enumerable.Range(0, t.Length / 3).Select(i => new[] { v[t[3 * i]], v[t[3 * i + 1]], v[t[3 * i + 2]] });
            }).ToArray();
        }

        /// <summary>Where a ray from the hall along +X at (y, z) first meets the triangles (+∞: nothing there).</summary>
        static float FirstAlongX(Vector3[][] tris, float y, float z)
        {
            float best = float.PositiveInfinity;
            foreach (var t in tris)
            {
                // barycentric in the (z, y) plane: the ray is a point there
                float d = (t[1].z - t[0].z) * (t[2].y - t[0].y) - (t[2].z - t[0].z) * (t[1].y - t[0].y);
                if (Mathf.Abs(d) < 1e-9f) continue; // edge-on to the ray
                float u = ((z - t[0].z) * (t[2].y - t[0].y) - (t[2].z - t[0].z) * (y - t[0].y)) / d;
                float w = ((t[1].z - t[0].z) * (y - t[0].y) - (z - t[0].z) * (t[1].y - t[0].y)) / d;
                if (u < 0f || w < 0f || u + w > 1f) continue;
                best = Mathf.Min(best, t[0].x + u * (t[1].x - t[0].x) + w * (t[2].x - t[0].x));
            }
            return best;
        }

        /// <summary>
        /// A tunnel's route sign stands clear of its mouth's wall where the game puts it (<see cref="SewerLocation.SignSpot"/>):
        /// the board, its posts and their base plates in front of the stone frame round the mouth and the plinth at the wall's
        /// foot. (At 0.75 m off the wall the board lay in the frame's face — it stands 15 cm proud of the brick — and flickered
        /// through it, and the base plates sat in the plinth.)
        /// </summary>
        [Test]
        public void TheRouteSignStandsClearOfTheMouthsWall()
        {
            var place = Matrix4x4.TRS(new Vector3(SewerLocation.SignSpot.x, 0f, SewerLocation.SignSpot.z), Quaternion.Euler(0f, 90f, 0f), Vector3.one);
            var stand = Vertices(Load("RouteSignStand"), place);
            Assert.Less(stand.Min(v => v.x), -SewerHall.Wall - 0.15f, "the board faces the hall, in front of the wall");
            float z0 = stand.Min(v => v.z) - 0.05f, z1 = stand.Max(v => v.z) + 0.05f, top = stand.Max(v => v.y) + 0.05f;
            for (int h = 0; h < 2; h++)
            {
                // the wall's faces beside the sign that look into the hall (from 2 m out to the wall's outer face)
                var tris = Triangles(Load($"HallMouth{h}")).Where(t => t.Max(p => p.z) > z0 && t.Min(p => p.z) < z1 && t.Min(p => p.y) < top && t.Min(p => p.x) > -2f).ToArray();
                float worst = float.PositiveInfinity;
                string where = "";
                foreach (var v in stand)
                {
                    float gap = FirstAlongX(tris, v.y, v.z) - v.x; // from this point of the stand to the wall behind it
                    if (gap < worst) { worst = gap; where = $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})"; }
                }
                Assert.Greater(worst, 0.01f, $"HallMouth{h}: the sign's stand at {where} is {worst * 100f:0.0} cm in front of the wall (in it, if negative)");
            }
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
        [Test]
        public void EveryWayOnIsMarkedByItsOwnLampsInTheHall()
        {
            var branch = Load("Branch").GetComponent<SewerBranch>();
            Assert.AreEqual(4, branch.MouthLights.Length, "two lamps: each its beam and its halo");
            foreach (var l in branch.MouthLights)
            {
                Assert.IsTrue(l.transform.IsChildOf(branch.WhenOpen.transform), "lit only while the way is open");
                var at = branch.transform.InverseTransformPoint(l.transform.position);
                Assert.Less(at.x, -0.6f, "on the hall's side of the wall the tunnel starts in");
                Assert.Greater(Mathf.Abs(at.z), 2.3f, "beside the mouth, not in it (4.6 m wide)");
                Assert.That(l.intensity, Is.InRange(1f, 4.5f), "dim");
                Assert.Less(l.color.r, l.color.b, "the tunnels' cold light, not the hall's warm one");
            }
            Assert.AreEqual(2, branch.MouthLights.Count(l => branch.transform.InverseTransformPoint(l.transform.position).z > 0f), "one lamp on each side");
            var door = Load("SegmentDoor").GetComponentsInChildren<Light>(true);
            Assert.AreEqual(1, door.Count(l => l.name == "DoorLight"), "a lamp beside the doorway to the exit's passage");
        }

        [Test]
        public void ASewerLampLightsTheWallItHangsOn()
        {
            var lamp = Load("Lamp").GetComponent<SewerLamp>();
            Assert.IsNotNull(lamp.Beam);
            Assert.IsNotNull(lamp.Halo);
            Assert.AreEqual(LightType.Point, lamp.Halo.type, "all round: the wall behind the lamp, the vault, the floor");
            Assert.LessOrEqual(lamp.Halo.range, SewerLamp.HaloReach);
            Assert.Less(2.3f + SewerLamp.HaloReach, Basement.Depth, "from a lamp's height it ends under the house's floor: it has no shadows");
            // set by whoever places it: the halo follows the beam
            var go = Object.Instantiate(lamp.gameObject);
            try
            {
                SewerLamp.Set(go, Color.cyan, 4f, 13f);
                var placed = go.GetComponent<SewerLamp>();
                Assert.AreEqual(4f, placed.Beam.intensity, 1e-4f);
                Assert.AreEqual(13f, placed.Beam.range, 1e-4f);
                Assert.AreEqual(4f * SewerLamp.HaloShare, placed.Halo.intensity, 1e-4f);
                Assert.AreEqual(SewerLamp.HaloReach, placed.Halo.range, 1e-4f, "a long beam: the halo stays short");
                Assert.AreEqual(Color.cyan, placed.Halo.color);
                SewerLamp.Set(go, Color.cyan, 2f, 7f);
                Assert.AreEqual(3.5f, placed.Halo.range, 1e-4f, "a short beam's halo is shorter still");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AShaftHasADimLampUnderItsLidShiningUp()
        {
            foreach (string name in new[] { "Alcove", "PassageEnd" })
            {
                var piece = Load(name);
                var lid = piece.GetComponentsInChildren<Light>(true).Where(l => l.name == "LidLight").ToArray();
                Assert.AreEqual(1, lid.Length, name);
                var at = piece.transform.InverseTransformPoint(lid[0].transform.position);
                Assert.That(Basement.Depth - at.y, Is.InRange(0.8f, 2f), $"{name}: just under the lid");
                Assert.Greater(piece.transform.InverseTransformDirection(lid[0].transform.forward).y, 0.7f, $"{name}: it shines up at the lid");
                Assert.Greater(lid[0].range, Basement.Depth - at.y, $"{name}: and reaches it");
                Assert.AreNotEqual(LightShadows.None, lid[0].shadows, $"{name}: the shaft's walls keep its light in");
                // the shaft is lit all its length: no stretch of its wall further than a fill's reach from a light
                var heights = piece.GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith("Shaft") || l.name == "LidLight")
                    .Select(l => piece.transform.InverseTransformPoint(l.transform.position).y).OrderBy(y => y).ToList();
                Assert.GreaterOrEqual(heights.Count, 3, name);
                for (int i = 1; i < heights.Count; i++)
                    Assert.LessOrEqual(heights[i] - heights[i - 1], 2f, $"{name}: a dark stretch between {heights[i - 1]:0.0} and {heights[i]:0.0} m");
                foreach (var fill in piece.GetComponentsInChildren<Light>(true).Where(l => l.name.StartsWith("ShaftFill")))
                {
                    var from = piece.transform.InverseTransformPoint(fill.transform.position);
                    Assert.AreEqual(LightShadows.None, fill.shadows, $"{name}: a fill costs no shadow map");
                    Assert.Less(from.y + fill.range, Basement.Depth, $"{name}: it ends inside the shaft, under the house's floor");
                }
                // its bulb is in the model: the glowing part reaches up to it
                var glow = MeshBounds(piece, f => f.name.StartsWith("Glow"));
                Assert.Greater(glow.max.y, at.y - 0.2f, $"{name}: the caged bulb is where the light is");
            }
        }
    }
}
