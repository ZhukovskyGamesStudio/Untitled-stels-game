using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Items;
using Stealth.Vehicles;

namespace Stealth.Tests
{
    /// <summary>
    /// The garage's car upgrade bench: levels one at a time up to their top (3 levelled, 1 unique, the undesigned none),
    /// the run's recipes the same on every peer, the engine's fuel cut, the kennel's box, a recipe taken from the backpack.
    /// </summary>
    public class CarUpgradesTests
    {
        [Test]
        public void LevelsGoOneAtATimeToTheirTop()
        {
            var u = new CarUpgrades();
            Assert.AreEqual(3, CarUpgrades.MaxLevel(CarUpgrade.Engine));
            Assert.AreEqual(3, CarUpgrades.MaxLevel(CarUpgrade.Trunk));
            Assert.AreEqual(3, CarUpgrades.MaxLevel(CarUpgrade.Kennel));
            Assert.AreEqual(1, CarUpgrades.MaxLevel(CarUpgrade.Teleport));
            Assert.IsTrue(CarUpgrades.IsPlanned(CarUpgrade.Unique5) && CarUpgrades.IsPlanned(CarUpgrade.Unique6), "5 and 6 are not made yet");
            for (int i = 1; i <= 3; i++)
            {
                Assert.IsTrue(u.Raise(CarUpgrade.Engine));
                Assert.AreEqual(i, u.Level(CarUpgrade.Engine));
            }
            Assert.IsFalse(u.Raise(CarUpgrade.Engine), "level 3 is the top");
            Assert.IsTrue(u.Raise(CarUpgrade.Teleport));
            Assert.IsFalse(u.Raise(CarUpgrade.Teleport), "a unique one is bought once");
            Assert.IsFalse(u.Raise(CarUpgrade.Unique5), "not designed: cannot be bought");
            Assert.AreEqual(0, u.Level(CarUpgrade.Trunk), "the others untouched");

            var peer = new CarUpgrades();
            peer.Apply(u.Pack());
            for (int i = 0; i < CarUpgrades.Count; i++) Assert.AreEqual(u.Level((CarUpgrade)i), peer.Level((CarUpgrade)i), $"{(CarUpgrade)i} over the wire");
            peer.Apply(0xFFFFFFFF);
            Assert.AreEqual(3, peer.Level(CarUpgrade.Kennel), "clamped to the top");
            Assert.AreEqual(0, peer.Level(CarUpgrade.Unique6));
            u.Clear();
            Assert.AreEqual(0u, u.Pack(), "a new run");
        }

        [Test]
        public void TheEngineCutsTheFareAndTheKennelGrowsTheBox()
        {
            var s = new CarUpgradeSettings();
            Assert.AreEqual(1f, CarUpgrades.FuelFactor(0, s), 1e-5f);
            Assert.AreEqual(0.7f, CarUpgrades.FuelFactor(3, s), 1e-5f, "10 % a level");
            Assert.AreEqual(100, CarUpgrades.EngineCost(100, 0, s));
            Assert.AreEqual(90, CarUpgrades.EngineCost(100, 1, s));
            Assert.AreEqual(70, CarUpgrades.EngineCost(100, 3, s));
            Assert.AreEqual(5, CarUpgrades.EngineCost(5, 3, s), "never below the smallest fare");
            Assert.AreEqual(0, CarUpgrades.EngineCost(0, 3, s), "a free drive stays free");
            Assert.AreEqual(0, CarUpgrades.EngineCost(85, 2, s) % 5, "in fives like every fare");
            Assert.AreEqual(TeamStash.Height, CarUpgrades.StashRows(0, s));
            Assert.AreEqual(TeamStash.Height, CarUpgrades.StashRows(1, s), "level 1: it rides along, the same box");
            Assert.Less(CarUpgrades.StashRows(1, s), CarUpgrades.StashRows(2, s));
            Assert.Less(CarUpgrades.StashRows(2, s), CarUpgrades.StashRows(3, s));
            Assert.LessOrEqual(CarUpgrades.StashRows(3, s), TeamStash.MaxHeight, "the board is built for it");
            Assert.IsFalse(CarUpgrades.MuleRides(0));
            Assert.IsTrue(CarUpgrades.MuleRides(1));
        }

        [Test]
        public void TheRunsRecipesAreTheSameOnEveryPeerAndGrowWithTheLevel()
        {
            var a = new CarUpgradeBook(77, null, null);
            var b = new CarUpgradeBook(77, null, null);
            var other = new CarUpgradeBook(78, null, null);
            bool differs = false;
            foreach (CarUpgrade u in System.Enum.GetValues(typeof(CarUpgrade)))
            {
                for (int level = 1; level <= CarUpgrades.MaxLevel(u); level++)
                {
                    var r = a.Recipe(u, level);
                    Assert.IsNotNull(r, $"{u} {level}");
                    CollectionAssert.AreEqual(r, b.Recipe(u, level), "the same seed, the same recipe");
                    Assert.AreEqual(r.Length, r.Distinct().Count(), "each thing once");
                    Assert.LessOrEqual(r.Length, CarUpgrades.MostThings);
                    Assert.IsTrue(r.All(x => x < ItemCatalog.Shapes.Length), "real things");
                    if (!r.SequenceEqual(other.Recipe(u, level))) differs = true;
                }
                Assert.IsNull(a.Recipe(u, 0));
                Assert.IsNull(a.Recipe(u, CarUpgrades.MaxLevel(u) + 1), "no level past the top");
            }
            Assert.Less(a.Recipe(CarUpgrade.Engine, 1).Length, a.Recipe(CarUpgrade.Engine, 3).Length, "a higher level takes more");
            Assert.IsTrue(differs, "another run, other recipes");
        }

        [Test]
        public void ARecipeTakesEachThingOnceFromTheBackpack()
        {
            var take = new List<int>();
            var carried = new List<int> { 5, 9, -1, 5, 12 }; // (-1: the worn belt bag)
            Assert.IsTrue(CarUpgrades.Gather(carried, new byte[] { 5, 12 }, take));
            CollectionAssert.AreEqual(new[] { 0, 4 }, take);
            Assert.IsTrue(CarUpgrades.Gather(carried, new byte[] { 5, 5 }, take), "two of a kind: two things");
            CollectionAssert.AreEquivalent(new[] { 0, 3 }, take);
            Assert.IsFalse(CarUpgrades.Gather(carried, new byte[] { 5, 7 }, take), "one is missing");
            Assert.IsEmpty(take);
            Assert.IsFalse(CarUpgrades.Gather(carried, new byte[0], take), "no recipe");
        }

        [Test]
        public void TheStashBoxGrowsButNeverUnderAThing()
        {
            var stash = new TeamStash();
            Assert.AreEqual(TeamStash.Height, stash.Rows);
            stash.SetRows(6);
            Assert.AreEqual(6, stash.Rows);
            Assert.IsTrue(stash.Add(ItemCatalog.Book, 0, 0, 4, 0), "the new rows take things");
            stash.SetRows(4);
            Assert.AreEqual(4 + ItemCatalog.Size(ItemCatalog.RotatedCells(ItemCatalog.Book, 0)).y, stash.Rows, "a thing lies in the new rows: they stay");
            stash.Clear();
            stash.SetRows(4);
            Assert.AreEqual(4, stash.Rows);
            Assert.IsFalse(stash.CanPlace(ItemCatalog.Book, 0, 0, 4), "back to its own size");
        }
    }
}
