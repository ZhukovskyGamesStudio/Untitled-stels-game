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
        public void MirroringFlipsAPieceAndKeepsItsTurn()
        {
            int tetL = 6;
            var plain = ItemCatalog.RotatedCells(tetL, 0);
            var mirrored = ItemCatalog.RotatedCells(tetL, ItemCatalog.Mirrored(0));
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(0, 0) }, mirrored);
            CollectionAssert.AreNotEquivalent(plain, mirrored);
            int rot = ItemCatalog.Turned(ItemCatalog.Mirrored(1));
            Assert.IsTrue(ItemCatalog.IsMirrored(rot), "turning keeps the mirror");
            Assert.AreEqual(2, rot & 3);
            Assert.AreEqual(1, ItemCatalog.Turned(0));
            Assert.AreEqual(0, ItemCatalog.Turned(3), "four turns come back");
            Assert.AreEqual(0, ItemCatalog.Mirrored(ItemCatalog.Mirrored(0)));
            var stash = new TeamStash();
            Assert.IsTrue(stash.Add((byte)tetL, 0, 0, 0, (byte)ItemCatalog.Mirrored(0)));
            Assert.AreEqual(0, stash.IndexAt(new Vector2Int(1, 2)), "the mirrored L takes the mirrored cells");
            Assert.AreEqual(-1, stash.IndexAt(new Vector2Int(0, 2)));
        }

        /// <summary>
        /// Every item in the sewer cart covers exactly the cells it takes in the stash grid: the real item mesh, placed by
        /// <see cref="StashCart.Place"/>, has all its vertices over those cells and some over each of them.
        /// </summary>
        [Test]
        public void CartShowsEveryItemOnItsStashCells()
        {
            var visuals = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");
            Assert.IsNotNull(visuals, "ItemVisuals asset");
            const float c = StashCart.Cell;
            for (int shape = 0; shape < ItemCatalog.Shapes.Length; shape++)
            {
                var verts = visuals.Meshes[shape].vertices;
                for (int rot = 0; rot < 8; rot++)
                {
                    var e = new InventoryEntry { Shape = (byte)shape, X = 2, Y = 1, Rot = (byte)rot };
                    var (pos, q, scale) = StashCart.Place(e);
                    var m = Matrix4x4.TRS(pos, q, scale);
                    var cells = ItemCatalog.RotatedCells(shape, rot).Select(v => new Vector2Int(e.X + v.x, e.Y + v.y)).ToList();
                    var hit = new HashSet<Vector2Int>();
                    foreach (var v in verts)
                    {
                        var w = m.MultiplyPoint3x4(v);
                        // grid x along +X, rows along −Z from the far edge
                        var cell = new Vector2Int(Mathf.FloorToInt(w.x / c), Mathf.FloorToInt(TeamStash.Height - w.z / c));
                        Assert.IsTrue(cells.Contains(cell), $"{ItemCatalog.Shapes[shape].Id} rot {rot}: a vertex at {w} lies over cell {cell}, not over the item's cells");
                        hit.Add(cell);
                    }
                    Assert.AreEqual(cells.Count, hit.Count, $"{ItemCatalog.Shapes[shape].Id} rot {rot}: some cells of the item stay empty");
                }
            }
        }

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
            {
                int rot = 0;
                for (int i = 0; i < 4; i++) rot = ItemCatalog.Turned(rot);
                Assert.AreEqual(0, rot);
                CollectionAssert.AreEquivalent(ItemCatalog.RotatedCells(s, 0), ItemCatalog.RotatedCells(s, 3 + 1 & 3));
            }
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
        public void StashAllPutsWhatFitsAndKeepsTheRest()
        {
            var stash = new TeamStash();
            var items = new List<InventoryEntry>();
            // More single cells than the stash holds: the extra ones stay in the backpack.
            for (int i = 0; i < TeamStash.Width * TeamStash.Height + 3; i++) items.Add(new InventoryEntry { Shape = 0, Color = 1, X = (byte)(i % 8), Y = (byte)(i / 8) });
            int added = stash.AddAll(items, out var left);
            Assert.AreEqual(TeamStash.Width * TeamStash.Height, added);
            Assert.AreEqual(3, left.Count);
            Assert.IsFalse(stash.FindSpot(0, out _, out _, out _), "the stash is full");

            // A big piece goes in before the small ones fill the gaps.
            var fresh = new TeamStash();
            var mixed = new List<InventoryEntry> { new() { Shape = 0 }, new() { Shape = 3 }, new() { Shape = 0 } };
            Assert.AreEqual(3, fresh.AddAll(mixed, out var rest));
            Assert.AreEqual(0, rest.Count);
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
