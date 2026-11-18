using System.Linq;
using NUnit.Framework;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The book, canned food, screwdriver and belt bag: where they lie (canned food in kitchens, the screwdriver in tech
    /// rooms, books mostly in libraries), the belt bag's 4 x 4 belt, the vent lids a screwdriver takes off.
    /// </summary>
    public class NewItemsTests
    {
        [TearDown]
        public void RestoreLoot() => ItemCatalog.ConfigureLoot(Configs.Tuning.Level.Loot.Table);

        [Test]
        public void ItemsLieInTheRoomsTheyBelongTo()
        {
            ItemCatalog.ConfigureLoot(new[]
            {
                new ItemCatalog.LootWeight { Item = "shovel", Weight = 5 },
                new ItemCatalog.LootWeight { Item = "book", Weight = 2, Favoured = new[] { RoomType.Library }, FavouredBoost = 5 },
                new ItemCatalog.LootWeight { Item = "cans", Weight = 3, Only = new[] { RoomType.Kitchen } },
                new ItemCatalog.LootWeight { Item = "screwdriver", Weight = 3, Only = new[] { RoomType.Server, RoomType.Lab } },
            });
            var rng = new System.Random(5);
            int Count(RoomType? room, byte shape) => Enumerable.Range(0, 2000).Count(_ => ItemCatalog.LootShape(rng, room) == shape);
            Assert.AreEqual(0, Count(RoomType.Office, ItemCatalog.Cans), "canned food only in kitchens");
            Assert.Greater(Count(RoomType.Kitchen, ItemCatalog.Cans), 300);
            Assert.AreEqual(0, Count(RoomType.Kitchen, ItemCatalog.Screwdriver), "the screwdriver only in tech rooms");
            Assert.Greater(Count(RoomType.Server, ItemCatalog.Screwdriver), 300);
            Assert.AreEqual(0, Count(null, ItemCatalog.Cans), "a crate or a safe: no room's own things");
            Assert.Greater(Count(RoomType.Library, ItemCatalog.Book), 2 * Count(RoomType.Office, ItemCatalog.Book), "books mostly in a library");
        }

        [Test]
        public void TheShapesAreTheirSizes()
        {
            Assert.AreEqual(new Vector2Int(2, 3), ItemCatalog.Size(ItemCatalog.Shapes[ItemCatalog.Book].Cells));
            Assert.AreEqual(new Vector2Int(2, 2), ItemCatalog.Size(ItemCatalog.Shapes[ItemCatalog.Cans].Cells));
            Assert.AreEqual(new Vector2Int(1, 3), ItemCatalog.Size(ItemCatalog.Shapes[ItemCatalog.Screwdriver].Cells));
            Assert.IsTrue(ItemCatalog.Holdable(ItemCatalog.Screwdriver), "the screwdriver is used from the hand");
            Assert.IsTrue(ItemCatalog.IsTool(ItemCatalog.Screwdriver));
            Assert.IsTrue(ItemCatalog.IsLoot(ItemCatalog.Book) && ItemCatalog.IsLoot(ItemCatalog.Cans));
        }

        static PlayerInventory Inventory(params InventoryEntry[] items)
        {
            var inv = new GameObject("inv").AddComponent<PlayerInventory>();
            inv.ApplyReplicated(items);
            return inv;
        }

        [Test]
        public void ABeltBagIsPutOnAndBringsAFourByFourBelt()
        {
            var inv = Inventory();
            Assert.IsFalse(inv.CanPlace(ItemCatalog.Shapes.Length > 0 ? 0 : 0, 0, PlayerInventory.BeltX0, 0), "no belt without the bag");
            Assert.IsTrue(inv.FindSpot(ItemCatalog.BeltBag, out var bx, out _, out _));
            Assert.AreEqual(PlayerInventory.WornX, bx, "the bag is worn, not packed");
            Object.DestroyImmediate(inv.gameObject);

            inv = Inventory(new InventoryEntry { Shape = ItemCatalog.BeltBag, X = PlayerInventory.WornX });
            Assert.IsTrue(inv.HasBelt);
            Assert.IsTrue(inv.CanPlace(ItemCatalog.Cans, 0, PlayerInventory.BeltX0 + 2, 2), "a 2 x 2 in the belt's corner");
            Assert.IsFalse(inv.CanPlace(ItemCatalog.Cans, 0, PlayerInventory.BeltX0 + 3, 0), "over the belt's edge");
            Assert.IsFalse(inv.CanPlace(ItemCatalog.Book, 0, PlayerInventory.BeltX0, 2), "too tall for what is left");
            Assert.IsFalse(inv.CanPlace(ItemCatalog.BeltBag, 0, PlayerInventory.WornX, 0), "one bag worn at a time");
            Object.DestroyImmediate(inv.gameObject);

            // with things in the belt the bag stays on
            inv = Inventory(new InventoryEntry { Shape = ItemCatalog.BeltBag, X = PlayerInventory.WornX },
                            new InventoryEntry { Shape = ItemCatalog.Cans, X = PlayerInventory.BeltX0, Y = 0 });
            Assert.IsFalse(inv.CanPlace(ItemCatalog.BeltBag, 0, 0, 0, inv.WornIndex()), "the bag comes off only empty");
            Assert.IsFalse(inv.CanPlace(ItemCatalog.Cans, 0, PlayerInventory.BeltX0 + 1, 1), "the cans are there");
            Object.DestroyImmediate(inv.gameObject);
        }

        [Test]
        public void WhenTheBackpackIsFullTheBeltTakesTheRest()
        {
            var full = Enumerable.Range(0, InventoryLayout.Width).SelectMany(x => Enumerable.Range(0, InventoryLayout.Height).Select(y => (x, y)))
                .Where(c => InventoryLayout.IsCell(c.x, c.y)).Select(c => new InventoryEntry { Shape = 0, X = (byte)c.x, Y = (byte)c.y }).ToList();
            var inv = Inventory(full.ToArray());
            Assert.IsFalse(inv.FindSpot(ItemCatalog.Cans, out _, out _, out _), "no room without a belt");
            Object.DestroyImmediate(inv.gameObject);
            full.Add(new InventoryEntry { Shape = ItemCatalog.BeltBag, X = PlayerInventory.WornX });
            inv = Inventory(full.ToArray());
            Assert.IsTrue(inv.FindSpot(ItemCatalog.Cans, out var x, out _, out _));
            Assert.GreaterOrEqual(x, PlayerInventory.BeltX0, "into the belt");
            Object.DestroyImmediate(inv.gameObject);
        }

        [Test]
        public void SomeVentLidsAreScrewedShutButNeverTheArrivals()
        {
            int closed = 0, all = 0;
            foreach (var l in TestHouses.Sample)
            {
                Assert.IsFalse(l.Vents[0].Grates[0].Closed, $"seed {l.Seed}: the arrival's grate is open");
                foreach (var net in l.Vents) foreach (var g in net.Grates) { all++; if (g.Closed) closed++; }
            }
            Assert.That(closed, Is.InRange(all / 5, all * 4 / 5), $"{closed} of {all} grates shut");
        }
    }
}
