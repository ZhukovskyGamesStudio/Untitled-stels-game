using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    public class ItemAndInventoryTests
    {
        [Test]
        public void TeamStashIsARectangularGrid()
        {
            var stash = new TeamStash();
            Assert.IsTrue(stash.CanPlace(0, 0, TeamStash.Width - 1, TeamStash.Height - 1));
            Assert.IsFalse(stash.CanPlace(0, 0, TeamStash.Width, 0), "right of the grid");
            Assert.IsFalse(stash.CanPlace(0, 0, 0, -1), "above the grid");
            int v = stash.Version;
            Assert.IsTrue(stash.Add(3, 4, 0, 0, 0)); // I piece, gold
            Assert.Greater(stash.Version, v);
            Assert.AreEqual(ItemCatalog.Value(3, 4), stash.TotalValue);
            Assert.IsFalse(stash.Add(0, 0, 0, 0, 0), "cell taken");
            Assert.AreEqual(0, stash.IndexAt(new Vector2Int(0, 0)));
            Assert.AreEqual(-1, stash.IndexAt(new Vector2Int(5, 3)));
        }

        [Test]
        public void TeamStashActsOnTheEntryARequestNames()
        {
            var stash = new TeamStash();
            stash.Add(0, 1, 2, 1, 0);
            stash.Add(0, 2, 5, 1, 0);
            var second = stash.Items[1];
            Assert.IsTrue(stash.Move(second, 6, 3, 0));
            Assert.AreEqual(6, stash.Items[1].X);
            Assert.IsFalse(stash.Take(second), "it moved: the old entry is gone");
            var stale = new InventoryEntry { Shape = 0, Color = 3, X = 2, Y = 1 };
            Assert.IsFalse(stash.Take(stale), "another colour at that place is not taken");
            Assert.IsTrue(stash.Take(stash.Items[0]));
            Assert.AreEqual(1, stash.Items.Count);
            stash.Clear();
            Assert.AreEqual(0, stash.Items.Count);
        }

        [Test]
        public void ValueGrowsWithSizeAndColor()
        {
            Assert.AreEqual(5, ItemCatalog.Value(0, 0)); // 1 cell, grey
            Assert.AreEqual(4 * 8 * 5, ItemCatalog.Value(3, 4)); // I piece, gold
            for (int c = 1; c < ItemCatalog.Colors.Length; c++)
                Assert.Greater(ItemCatalog.Value(0, c), ItemCatalog.Value(0, c - 1));
        }

        [Test]
        public void RotationKeepsCellCountAndNormalizes()
        {
            for (int s = 0; s < ItemCatalog.Shapes.Length; s++)
                for (int r = 0; r < 4; r++)
                {
                    var cells = ItemCatalog.RotatedCells(s, r);
                    Assert.AreEqual(ItemCatalog.Shapes[s].Cells.Length, cells.Length);
                    Assert.AreEqual(0, cells.Min(c => c.x));
                    Assert.AreEqual(0, cells.Min(c => c.y));
                    Assert.AreEqual(cells.Length, cells.Distinct().Count());
                }
        }

        [Test]
        public void FourRotationsReturnToStart()
        {
            for (int s = 0; s < ItemCatalog.Shapes.Length; s++)
                CollectionAssert.AreEquivalent(ItemCatalog.RotatedCells(s, 0), ItemCatalog.RotatedCells(s, 4));
        }

        [Test]
        public void IPieceRotatesToVertical()
        {
            var size = ItemCatalog.Size(ItemCatalog.RotatedCells(3, 1));
            Assert.AreEqual(new Vector2Int(1, 4), size);
        }

        [Test]
        public void FitsRespectsHolesAndOccupiedCells()
        {
            var mono = ItemCatalog.RotatedCells(0, 0);
            var occupied = new HashSet<Vector2Int>();
            Assert.IsTrue(InventoryLayout.Fits(mono, 0, 0, occupied));
            Assert.IsFalse(InventoryLayout.Fits(mono, 0, InventoryLayout.Height - 1, occupied), "bottom-left corner is a hole");
            Assert.IsFalse(InventoryLayout.Fits(mono, -1, 0, occupied));
            Assert.IsFalse(InventoryLayout.Fits(mono, InventoryLayout.Width, 0, occupied));
            occupied.Add(new Vector2Int(2, 1));
            Assert.IsFalse(InventoryLayout.Fits(ItemCatalog.RotatedCells(3, 0), 0, 1, occupied), "I piece crosses an occupied cell");
            Assert.IsTrue(InventoryLayout.Fits(ItemCatalog.RotatedCells(3, 0), 0, 0, occupied));
        }

        [Test]
        public void RandomColorFavoursRareColorsDeeper()
        {
            int Gold(int bonus)
            {
                var rng = new System.Random(1);
                int gold = 0;
                for (int i = 0; i < 5000; i++) if (ItemCatalog.RandomColor(rng, bonus) == 4) gold++;
                return gold;
            }
            Assert.Greater(Gold(10), Gold(0));
        }
    }
}
