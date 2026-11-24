using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>What tells a player about a door — its lamps, the storage sign, the vault's plate, the exit sign — is in sight: out of the wall, under the ceiling.</summary>
    public class DoorSignTests
    {
        const float Face = LevelMetrics.WallThickness / 2f + ArchitecturePlanner.FinishLift;

        static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Level/{name}.prefab");

        static Bounds Of(Renderer r, Transform root)
        {
            var mesh = r.GetComponent<MeshFilter>().sharedMesh.bounds;
            var m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(mesh.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(m.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
            return b;
        }

        [Test]
        public void LockLampsStandOutOfTheWallOnBothSides()
        {
            int doors = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Level" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var door = prefab.GetComponent<AutoDoor>();
                if (door == null || door.LockLamps == null) continue;
                var over = door.LockLamps.Where(r => r.transform.parent == prefab.transform).Select(r => Of(r, prefab.transform)).ToList();
                if (over.Count == 0) continue;
                doors++;
                Assert.IsTrue(over.Any(b => b.max.z > Face + 0.05f) && over.Any(b => b.min.z < -Face - 0.05f), $"{prefab.name}: past the casing on both faces");
                foreach (var b in over)
                {
                    Assert.GreaterOrEqual(Mathf.Abs(b.center.z) + b.extents.z, Face + 0.05f, $"{prefab.name}: a lamp inside the wall");
                    Assert.LessOrEqual(b.max.y, LevelMetrics.WallHeight, $"{prefab.name}: a lamp over the ceiling");
                    Assert.GreaterOrEqual(b.min.y, LevelMetrics.DoorHeight, $"{prefab.name}: a lamp in the opening");
                }
            }
            Assert.Greater(doors, 5);
        }

        [Test]
        public void TheSignsAreUnderTheCeilingAndOverTheDoorsTrim()
        {
            foreach (var sign in new[] { LevelBuilder.StorageSign, LevelBuilder.VaultSign })
            {
                Assert.LessOrEqual(sign.Center.y + sign.HalfHeight, LevelMetrics.WallHeight, "under the ceiling");
                Assert.GreaterOrEqual(sign.Center.y - sign.HalfHeight, LevelMetrics.DoorHeight + 0.16f, "over the casing");
            }
            var storage = Prefab("StorageDoor");
            foreach (var r in storage.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Sign")))
                Assert.LessOrEqual(Of(r, storage.transform).max.y, LevelMetrics.WallHeight, r.name);
            // the vault's plate hangs at the sign's middle, clear of the round door's ring
            var plate = Prefab("VaultPanel");
            foreach (var r in plate.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = Of(r, plate.transform);
                Assert.LessOrEqual(Mathf.Max(b.max.y, -b.min.y), LevelBuilder.VaultSign.HalfHeight + 0.005f, r.name);
                Assert.LessOrEqual(Mathf.Max(b.max.x, -b.min.x), LevelBuilder.VaultSign.HalfWidth + 0.005f, r.name);
            }
            var vault = Prefab("VaultDoor");
            float ring = vault.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.GetComponent<MeshFilter>() != null && !r.name.StartsWith("Wall")).Max(r => Of(r, vault.transform).max.y);
            Assert.LessOrEqual(ring, LevelBuilder.VaultSign.Center.y - LevelBuilder.VaultSign.HalfHeight, "the round door's ring under the plate");
        }

        [Test]
        public void TheExitSignShowsOnBothFacesOfItsWall()
        {
            var sign = Prefab("ExitSign");
            var eye = sign.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.StartsWith("Eye"));
            var b = Of(eye, sign.transform);
            Assert.GreaterOrEqual(b.max.z, Face + 0.05f);
            Assert.LessOrEqual(b.min.z, -Face - 0.05f);
        }

        [Test]
        public void NothingOfTheServicesHangsBeforeASign()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            int signs = 0;
            foreach (var (seed, house) in new[] { (99991, 4), (7, 3), (42, 5), (12, 4) })
            {
                var l = TestHouses.Get(seed, house);
                ActiveLevel.Load(l, art);
                foreach (var s in ActiveLevel.Shell.Signs)
                {
                    signs++;
                    Assert.AreEqual(1f, s.Along.magnitude, 1e-3f);
                    var n = new Vector3(-s.Along.z, 0f, s.Along.x);
                    foreach (var part in UtilityPlan.Make(l, ActiveLevel.Shell))
                    {
                        if (part.Slot is UtilitySlot.Conduit) continue; // (the thin cables that run to the door itself)
                        var d = part.Center - s.Center;
                        float across = Mathf.Abs(Vector3.Dot(d, n)) - Mathf.Abs(Vector3.Dot(part.Size, new Vector3(Mathf.Abs(n.x), 0f, Mathf.Abs(n.z)))) / 2f;
                        float along = Mathf.Abs(Vector3.Dot(d, s.Along)) - Mathf.Abs(Vector3.Dot(part.Size, new Vector3(Mathf.Abs(s.Along.x), 0f, Mathf.Abs(s.Along.z)))) / 2f;
                        float up = Mathf.Abs(d.y) - part.Size.y / 2f;
                        Assert.IsFalse(across < 0.5f && along < s.HalfWidth && up < s.HalfHeight, $"seed {seed}: {part.Slot} at {part.Center} before the sign at {s.Center}");
                    }
                }
            }
            Assert.Greater(signs, 0);
            ActiveLevel.Clear();
        }
    }
}
