using NUnit.Framework;
using Stealth.Items;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The first real loot: the shovel — every loot is one, a T in the backpack with its picture, it falls in the world.</summary>
    public class ShovelTests
    {
        [Test]
        public void EveryLootIsAShovelAndTheStreamsDoNotShift()
        {
            ItemCatalog.ConfigureLoot(null); // the code's default table: shovels only (the configs add flashlights and batteries)
            var a = new System.Random(7);
            var b = new System.Random(7);
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(ItemCatalog.Shovel, ItemCatalog.LootShape(a));
                b.Next(ItemCatalog.LootShapes); // the old roll is still made
            }
            Assert.AreEqual(b.Next(), a.Next(), "the rest of the stream is where it was");
            Assert.IsTrue(ItemCatalog.IsLoot(ItemCatalog.Shovel), "sold, stashed, counted for the quota");
            Assert.IsFalse(ItemCatalog.IsTool(ItemCatalog.Shovel));
            Assert.Greater(ItemCatalog.Value(ItemCatalog.Shovel, 0), 0);
            Configs.Tuning.Apply(); // back to the configured loot table
        }

        [Test]
        public void TheShovelIsOneByFourInTheBackpack()
        {
            // a straight 1 x 4: the shovel stands on its handle in the backpack
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3) },
                ItemCatalog.Shapes[ItemCatalog.Shovel].Cells);
            Assert.AreEqual(new Vector2Int(1, 4), ItemCatalog.Size(ItemCatalog.Shapes[ItemCatalog.Shovel].Cells));
            foreach (var c in ItemCatalog.Shapes[ItemCatalog.Shovel].Cells)
                Assert.AreEqual('#', InventoryLayout.Rows[c.y][c.x + 1], "it fits the backpack in its base turn (from the second column)");
        }

        [Test]
        public void TheShovelHasAModelAPictureAndPhysics()
        {
            Assert.IsTrue(ItemCatalog.HasPhysics(ItemCatalog.Shovel), "it falls when dropped");
            Assert.IsFalse(ItemCatalog.HasPhysics(0), "the tetris pieces stay put");
            var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");
            Assert.IsNotNull(visuals.Models[ItemCatalog.Shovel], "its own model in the world");
            Assert.IsNotNull(visuals.Icons[ItemCatalog.Shovel], "its picture in the backpack");
            var sprite = visuals.Icons[ItemCatalog.Shovel];
            Assert.AreEqual(0.25f, sprite.rect.width / sprite.rect.height, 0.01f, "the picture covers the 1 x 4 cells");
            var model = visuals.Models[ItemCatalog.Shovel];
            var b = new Bounds();
            bool first = true;
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mb = f.sharedMesh.bounds;
                foreach (var c in new[] { mb.min, mb.max })
                {
                    var p = model.transform.InverseTransformPoint(f.transform.TransformPoint(c));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                }
            }
            var size = ItemCatalog.ModelSize(ItemCatalog.Shovel);
            Assert.AreEqual(size.x, b.size.x, 0.03f, "its colliders fit the model's length");
            Assert.AreEqual(size.z, b.size.z, 0.03f, "and its width");
            Assert.AreEqual(0f, b.center.x, 0.03f, "the model is centred on its length");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Items/WorldItem.prefab").GetComponent<WorldItem>();
            Assert.IsNotNull(prefab.Body, "a body to fall with");
            Assert.IsTrue(prefab.Body.isKinematic, "kinematic until a real item is spawned on the server");
            Assert.IsNotNull(prefab.Solid, "a solid box to land on");
            Assert.IsFalse(prefab.Solid.isTrigger);
            Assert.IsTrue(prefab.Collider.isTrigger, "the grab box stays a trigger");
        }

        [Test]
        public void AShovelStandsInALockerAndNeverHidesInADrawer()
        {
            Assert.IsFalse(ItemCatalog.FitsInDrawer(ItemCatalog.Shovel), "1.1 m does not lie in a 0.8 m drawer");
            var door = new Bounds(new Vector3(0f, 1.045f, -0.245f), new Vector3(0.47f, 1.97f, 0.02f));
            var (pos, rot) = StorageMath.LootPose(ItemCatalog.ModelSize(ItemCatalog.Shovel), door, Vector3.back, 0.46f, drawer: false);
            var along = rot * Vector3.right;
            Assert.Greater(Mathf.Abs(along.y), 0.99f, "on end");
            float top = pos.y + Mathf.Abs(along.y) * ItemCatalog.ModelSize(ItemCatalog.Shovel).x / 2f;
            Assert.Less(top, 2.05f - 0.38f, "under the hat shelf");
            Assert.Greater(pos.y - ItemCatalog.ModelSize(ItemCatalog.Shovel).x / 2f, door.min.y - 0.001f, "on the floor of the compartment");
            Assert.IsTrue(StorageMath.Fits(ItemCatalog.ModelSize(ItemCatalog.Shovel), door, Vector3.back, 0.46f, drawer: false), "it fits a locker");
            var wallCupboardDoor = new Bounds(new Vector3(0f, 1.785f, -0.15f), new Vector3(0.586f, 0.64f, 0.022f));
            Assert.IsFalse(StorageMath.Fits(ItemCatalog.ModelSize(ItemCatalog.Shovel), wallCupboardDoor, Vector3.back, 0.28f, drawer: false), "not a wall cupboard");
            Assert.IsTrue(StorageMath.Fits(ItemCatalog.ModelSize(ItemCatalog.Battery), wallCupboardDoor, Vector3.back, 0.28f, drawer: false), "batteries do");
        }
    }
}
