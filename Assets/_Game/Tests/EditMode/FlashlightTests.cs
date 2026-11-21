using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The hand slot, the flashlight with its charge, batteries, the test level's loot and lights.</summary>
    public class FlashlightTests
    {
        GameObject m_Player;

        [TearDown]
        public void Cleanup()
        {
            Tuning.Apply(); // back to the configured loot table
            if (m_Player == null) return;
            m_Player.GetComponent<PlayerController>().Unbind();
            Object.DestroyImmediate(m_Player);
        }

        [TestCase("WallCabinet", 2)]
        [TestCase("MedicineCabinet", 2)]
        [TestCase("PartsCabinet", 2)]
        [TestCase("MailShelf", 3)]
        public void TheThemedRoomsCupboardsOpenLikeLockers(string prefab, int doors)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{prefab}.prefab");
            Assert.IsNotNull(go.GetComponent<Storage>(), $"{prefab} opens");
            var parts = go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("BodyDoor")).ToArray();
            Assert.AreEqual(doors, parts.Length);
            foreach (var p in parts) Assert.IsNotNull(p.GetComponent<Collider>(), $"{prefab} {p.name}: can be looked at");
        }

        [Test]
        public void EveryItemThatIsNotATetrisPieceHasAPictureOfItsCells()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");
            for (int s = ItemCatalog.LootShapes; s < ItemCatalog.Shapes.Length; s++)
            {
                var icon = visuals.Icons[s];
                Assert.IsNotNull(icon, $"{ItemCatalog.Shapes[s].Id}: a picture in the backpack");
                var size = ItemCatalog.Size(ItemCatalog.Shapes[s].Cells);
                Assert.AreEqual(size.x / (float)size.y, icon.rect.width / icon.rect.height, 0.01f, $"{ItemCatalog.Shapes[s].Id}: its cells' shape");
            }
        }

        [Test]
        public void TheLootTableKeepsTheHousesValueBudget()
        {
            // the old loot: the eight tetris pieces, drawn evenly — that is the budget the quota is a share of
            ItemCatalog.ConfigureLoot(null);
            float oldMean = 0f;
            for (int i = 0; i < ItemCatalog.LootShapes; i++) oldMean += ItemCatalog.MeanValueCells(i);
            oldMean /= ItemCatalog.LootShapes;
            Assert.AreEqual(oldMean / ItemCatalog.MeanValueCells(ItemCatalog.Shovel), ItemCatalog.ItemsPerOldItem(), 1e-4f, "shovels only: fewer, bigger");
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Data/Config/Level.asset");
            ItemCatalog.ConfigureLoot(level.Loot.Table);
            float k = ItemCatalog.ItemsPerOldItem();
            float now = 0f, w = 0f;
            foreach (var t in ItemCatalog.LootTable) { now += t.weight * ItemCatalog.MeanValueCells(t.shape); w += t.weight; }
            Assert.AreEqual(oldMean, k * now / w, 1e-3f, "items × their mean value = the old budget");
            Assert.Greater(ItemCatalog.MeanValueCells(ItemCatalog.Battery), 1f, "batteries carry a value colour like loot");
            Assert.IsTrue(ItemCatalog.LootTable.All(t => t.shape >= ItemCatalog.LootShapes), "no tetris pieces in the table");
        }

        [Test]
        public void TheViewShowsAHandHoldingTheFlashlight()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab").GetComponent<PlayerController>();
            Assert.IsNotNull(player.ViewFlashlight, "the hand in the first-person view");
            Assert.IsFalse(player.ViewFlashlight.activeSelf, "hidden until a flashlight is in the hand");
            Assert.AreEqual(player.Head, player.ViewFlashlight.transform.parent, "it moves with the view");
        }

        PlayerInventory Inventory()
        {
            m_Player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab"));
            return m_Player.GetComponent<PlayerInventory>();
        }

        [Test]
        public void TheHandHoldsOneFlashlightOrBatteryOutsideTheGrid()
        {
            var inv = Inventory();
            Assert.IsTrue(inv.CanPlace(ItemCatalog.Flashlight, 0, PlayerInventory.HandX, 0), "a flashlight goes in the hand");
            Assert.IsFalse(inv.CanPlace(ItemCatalog.Shovel, 0, PlayerInventory.HandX, 0), "a shovel does not");
            inv.ApplyReplicated(new[] { new InventoryEntry { Shape = ItemCatalog.Flashlight, Color = 50, X = PlayerInventory.HandX } });
            Assert.AreEqual(0, inv.HandIndex());
            Assert.IsFalse(inv.CanPlace(ItemCatalog.Battery, 0, PlayerInventory.HandX, 0), "one item in the hand");
            Assert.IsEmpty(inv.Occupied(), "the hand takes no backpack cells");
            Assert.AreEqual(-1, inv.IndexAt(Vector2Int.zero));
            Assert.IsTrue(inv.CanPlace(ItemCatalog.Shovel, 0, 1, 0), "the backpack is still free");
        }

        [Test]
        public void ABatteryRechargesAFlashlightThatIsNotFull()
        {
            var inv = Inventory();
            inv.ApplyReplicated(new[]
            {
                new InventoryEntry { Shape = ItemCatalog.Flashlight, Color = 12, X = 0, Y = 0 },
                new InventoryEntry { Shape = ItemCatalog.Battery, X = 2, Y = 0 },
                new InventoryEntry { Shape = ItemCatalog.Battery, X = 3, Y = 0 },
            });
            Assert.IsTrue(inv.CanRecharge(0));
            inv.RechargeServer(1, 0);
            Assert.AreEqual(2, inv.Items.Count, "one battery used up");
            Assert.AreEqual(ItemCatalog.FullCharge, inv.Items[0].Color, "full again");
            Assert.IsFalse(inv.CanRecharge(0), "a full flashlight takes no battery");
            inv.RechargeServer(1, 0);
            Assert.AreEqual(2, inv.Items.Count, "the second battery stays");
            inv.SetChargeServer(0, 3);
            Assert.AreEqual(3, inv.Items[0].Color, "it drains");
        }

        [Test]
        public void FlashlightsComeChargedAndAreToolsWithPhysics()
        {
            Assert.AreEqual(ItemCatalog.FullCharge, ItemCatalog.FreshColor(ItemCatalog.Flashlight, 2));
            Assert.AreEqual(ItemCatalog.Rarity(ItemCatalog.Shovel), ItemCatalog.FreshColor(ItemCatalog.Shovel, 2), "a shovel has its own rarity, whatever was rolled");
            Assert.IsFalse(ItemCatalog.IsTool(ItemCatalog.Flashlight) || ItemCatalog.IsTool(ItemCatalog.Battery), "they burn into fuel at the end");
            Assert.Greater(ItemCatalog.Value(ItemCatalog.Flashlight, ItemCatalog.FullCharge), ItemCatalog.Value(ItemCatalog.Battery, 0));
            Assert.Greater(ItemCatalog.Value(ItemCatalog.Battery, 0), 0);
            Assert.IsTrue(ItemCatalog.HasPhysics(ItemCatalog.Flashlight) && ItemCatalog.HasPhysics(ItemCatalog.Battery));
            Assert.AreEqual(new Vector2Int(1, 3), ItemCatalog.Size(ItemCatalog.Shapes[ItemCatalog.Flashlight].Cells));
            Assert.AreEqual(new Vector2Int(1, 1), ItemCatalog.Size(ItemCatalog.Shapes[ItemCatalog.Battery].Cells));
            var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");
            foreach (var s in new[] { ItemCatalog.Flashlight, ItemCatalog.Battery })
            {
                Assert.IsNotNull(visuals.Models[s], $"{ItemCatalog.Shapes[s].Id}: its model");
                Assert.IsNotNull(visuals.Icons[s], $"{ItemCatalog.Shapes[s].Id}: its picture");
            }
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab").GetComponent<PlayerController>();
            Assert.IsNotNull(player.Flashlight, "the beam");
            Assert.AreEqual(LightType.Spot, player.Flashlight.type);
            Assert.IsFalse(player.Flashlight.enabled, "off until switched on");
            Assert.IsNotNull(player.Flashlight.GetComponent<LightSource>(), "robots see a player in its light");
        }

        [Test]
        public void TheTestLevelsLootIsShovelsFlashlightsAndBatteries()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Data/Config/RoomsTest/Level.asset");
            ItemCatalog.ConfigureLoot(level.Loot.Table);
            var rng = new System.Random(3);
            var seen = Enumerable.Range(0, 300).Select(_ => ItemCatalog.LootShape(rng)).Distinct().OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(new[] { ItemCatalog.Shovel, ItemCatalog.Flashlight, ItemCatalog.Battery }, seen);
            ItemCatalog.ConfigureLoot(null);
            Assert.AreEqual(ItemCatalog.Shovel, ItemCatalog.LootShape(rng), "the game's default: shovels only");
            Assert.IsFalse(level.Features.Gadgets, "no lure speakers or stun cubes in the test level");
            Assert.AreEqual(0, level.Features.KeyDoors, "no key doors (no keys lying around)");
        }

        [Test]
        public void EveryTestRoomHasALightNormalDimOrFlickering()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>("Assets/_Game/Data/Config/RoomsTest/Level.asset");
            Assert.IsTrue(level.Building.DarkRoomsDim);
            Assert.AreEqual(0.25f, level.Building.FlickerChance, 1e-4f);
            int dim = 0, flicker = 0, rooms = 0;
            for (int seed = 1; seed <= 16; seed++)
            {
                var l = LevelGenerator.Generate(seed * 7907, 0, level.Building, level.Security, 1);
                Assert.IsTrue(l.DarkRoomsDim);
                foreach (var r in l.Rooms)
                {
                    rooms++;
                    Assert.Greater(LevelBuilder.LightSpots(l, r).Count, 0, $"seed {seed}: {r.Type} has a lamp");
                    var (_, intensity) = RoomLighting.For(r.Type, r.Dark, l.DimShare);
                    Assert.Greater(intensity, 0f, $"seed {seed}: {r.Type} is lit");
                    if (r.Dark) dim++;
                    if (r.Flicker) flicker++;
                }
                Assert.Greater(BuildingSettings.CountJoiningCorridors(l), 0, $"seed {seed}: a corridor between the rooms");
            }
            Assert.Greater(dim, 0, "some dim rooms");
            Assert.Greater(flicker, 0, "some flickering lamps");
        }

        [Test]
        public void DesksAreTallerButNotWider()
        {
            var desk = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Models/Props/desk.fbx").OfType<Mesh>().ToArray();
            float top = desk.Max(m => m.bounds.max.y);
            Assert.Greater(LevelMetrics.DeskTop, 0.9f, "the desk's top is higher than an ordinary 0.75 m");
            float width = desk.Max(m => m.bounds.size.x);
            Assert.AreEqual(1.6f, width, 0.06f, "the same width as before");
            Assert.Greater(top, LevelMetrics.DeskTop - 0.05f);
        }
    }
}
