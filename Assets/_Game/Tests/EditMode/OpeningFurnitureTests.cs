using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Furniture that opens (cupboards, chests of drawers, fridges, lockers): the strip its doors and drawers swing into is
    /// the room's own floor — it never faces a wall — and no other furniture stands in it; two such pieces never open into
    /// each other. Chairs and bins do not count.
    /// </summary>
    public class OpeningFurnitureTests
    {
        static string Describe(LevelLayout l, (int prop, int other) c)
        {
            var p = l.Props[c.prop];
            string what = c.other < 0 ? "faces a wall" : $"blocked by {l.Props[c.other].Kind} at {l.Props[c.other].Position} yaw {l.Props[c.other].RotationY:0}";
            return $"{p.Kind} at {p.Position} yaw {p.RotationY:0}: {what} (room {l.Plan.RoomAt(p.Position + Vector3.up * 0.1f)?.Type})";
        }

        static void Check(IEnumerable<LevelLayout> houses)
        {
            var report = new List<string>();
            int opening = 0;
            foreach (var l in houses)
            {
                opening += l.Props.Count(p => Furnisher.Opens.Contains(p.Kind));
                foreach (var c in Furnisher.OpeningClashes(l)) report.Add($"seed {l.Seed} house {l.LevelIndex}: {Describe(l, c)}");
            }
            Assert.Greater(opening, 20, "the sample has furniture that opens");
            Assert.IsEmpty(report, $"{report.Count} of {opening}:\n" + string.Join("\n", report.Take(40)));
        }

        [Test]
        public void WhatOpensHasRoomToOpen() => Check(TestHouses.Sample);

        [Test, Category("Slow")]
        public void WhatOpensHasRoomToOpenInEveryKindOfHouse()
        {
            var houses = new List<LevelLayout>();
            foreach (HouseKind kind in System.Enum.GetValues(typeof(HouseKind)))
                for (int level = 1; level <= 5; level += 2)
                    houses.Add(TestHouses.Get(HouseKinds.SeedOf(kind, 11 + level, level), level));
            Check(houses);
        }

        [Test]
        public void TheStripIsInFrontOfThePieceAndNeighboursAlongAWallDoNotClash()
        {
            // a cabinet at the origin facing +Z: its strip lies in front of it, as wide as it is
            var foot = Furnisher.FootAt(PropKind.Cabinet, Vector3.zero, 0f);
            var swing = Furnisher.SwingAt(PropKind.Cabinet, Vector3.zero, 0f);
            Assert.Greater(swing.yMin, foot.yMax - 0.001f);
            Assert.AreEqual(Furnisher.OpenReach(PropKind.Cabinet), swing.yMax - foot.yMax, 0.02f);
            Assert.LessOrEqual(swing.width, foot.width);
            // turned to face +X, the strip is at +X
            var turned = Furnisher.SwingAt(PropKind.Cabinet, Vector3.zero, 90f);
            Assert.Greater(turned.xMin, 0f);
            // the next cabinet along the same wall: neither stands in the other's strip, the strips do not cross
            var next = new Vector3(foot.width, 0f, 0f);
            Assert.IsFalse(Furnisher.FootAt(PropKind.Cabinet, next, 0f).Overlaps(swing));
            Assert.IsFalse(Furnisher.SwingAt(PropKind.Cabinet, next, 0f).Overlaps(swing));
        }

        [Test]
        public void ChairsAndBinsMayStandInFrontButFurnitureMayNot()
        {
            Assert.IsFalse(Furnisher.BlocksOpening(PropKind.ExecutiveDesk, PropKind.Chair), "the boss's own chair");
            Assert.IsFalse(Furnisher.BlocksOpening(PropKind.Cabinet, PropKind.TrashBin));
            Assert.IsTrue(Furnisher.BlocksOpening(PropKind.Cabinet, PropKind.Desk));
            Assert.IsTrue(Furnisher.BlocksOpening(PropKind.Dresser, PropKind.DoubleBed));
            Assert.IsFalse(Furnisher.BlocksOpening(PropKind.WallCabinet, PropKind.KitchenCounter), "the cupboards over the counter open over it");
            Assert.IsTrue(Furnisher.BlocksOpening(PropKind.WallCabinet, PropKind.Fridge), "a tall fridge is in a wall cupboard's way");
            Assert.IsFalse(Furnisher.BlocksOpening(PropKind.KitchenCounter, PropKind.WallCabinet));
        }

        [Test]
        public void TheListOfWhatOpensMatchesThePrefabs()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
            {
                var prefab = art.Prop(kind);
                if (prefab == null) continue;
                bool opens = prefab.GetComponentInChildren<Stealth.Items.Storage>(true) != null;
                Assert.AreEqual(opens, Furnisher.Opens.Contains(kind), $"{kind}: Items.Storage on its prefab and Furnisher.Opens disagree");
            }
        }
    }
}
