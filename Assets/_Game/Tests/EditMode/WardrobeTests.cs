using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The rigged humans' wardrobe: which slot a part is, what a slot does to wear an outfit, items fit their wearer.</summary>
    public class WardrobeTests
    {
        static readonly Dictionary<string, string> Slots = new()
        {
            ["hoodie"] = "top", ["shirt"] = "top", ["jeans"] = "bottom", ["sneakers"] = "feet", ["messy"] = "hair", ["bob"] = "hair",
            ["headphones"] = "head", ["glasses"] = "eyes", ["beard"] = "beard",
        };

        static string SlotOf(string item) => Slots.TryGetValue(item, out var s) ? s : null;

        [Test]
        public void APartOfTheOwnOutfitIsNamedByItsSlot()
        {
            Assert.AreEqual("top", WardrobeRules.SlotOfPart("BodyClothes.top"));
            Assert.AreEqual("hair", WardrobeRules.SlotOfPart("Hair.hair"));
            Assert.IsNull(WardrobeRules.SlotOfPart("AccentHead"), "the body is no slot");
            Assert.IsNull(WardrobeRules.SlotOfPart("hand.L"), "a dot is not a slot by itself");
            Assert.AreEqual("BodyClothes", WardrobeRules.PartOf("BodyClothes.top"));
            Assert.AreEqual("AccentHead", WardrobeRules.PartOf("AccentHead"));
        }

        [Test]
        public void AnOutfitSwapsKeepsAndBaresSlots()
        {
            var own = new Dictionary<string, string> { ["top"] = "hoodie", ["bottom"] = "jeans", ["feet"] = "sneakers", ["hair"] = "messy", ["head"] = "headphones" };
            var plan = WardrobeRules.Plan(own, new[] { "hoodie", "jeans", "sneakers", "bob", "glasses", "unknown" }, SlotOf);
            Assert.AreEqual((WearAction.Own, "hoodie"), plan["top"]);
            Assert.AreEqual((WearAction.Swap, "bob"), plan["hair"]);
            Assert.AreEqual((WearAction.Swap, "glasses"), plan["eyes"]);
            Assert.AreEqual(WearAction.Bare, plan["head"].Item1, "a slot the outfit does not name is bare (the headphones come off)");
            Assert.AreEqual(WearAction.Bare, plan["beard"].Item1);
            Assert.AreEqual(WardrobeRules.Slots.Length, plan.Count, "every slot has a decision, an unknown item none");
        }

        [Test]
        public void TheLastItemNamedForASlotWins()
        {
            var plan = WardrobeRules.Plan(new Dictionary<string, string>(), new[] { "messy", "bob" }, SlotOf);
            Assert.AreEqual((WearAction.Swap, "bob"), plan["hair"]);
        }

        [Test]
        public void EveryItemOfTheWardrobeIsMadeForEveryPersonAndKnowsItsBones()
        {
            var styles = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            if (styles.Wardrobe == null || styles.Wardrobe.Length == 0) Assert.Ignore("the wardrobe is not built (human.py item=all, then the assets)");
            var people = new[] { "backend", "qa", "sysadmin", "designer" };
            foreach (var piece in styles.Wardrobe)
            {
                Assert.IsNotNull(piece, "a missing item prefab");
                Assert.Contains(piece.Who, people, $"{piece.name}: made for nobody");
                Assert.Contains(piece.Slot, WardrobeRules.Slots, $"{piece.Item}: no slot");
                Assert.IsNotEmpty(piece.Parts, $"{piece.Who}/{piece.Item}: no meshes");
                foreach (var part in piece.Parts)
                {
                    Assert.IsNotNull(part.Renderer.sharedMesh, $"{piece.Who}/{piece.Item}: a part without a mesh");
                    Assert.AreEqual(part.Renderer.sharedMesh.bindposes.Length, part.Bones.Length, $"{piece.Who}/{piece.Item}: every bind pose names its bone");
                    Assert.IsTrue(part.Bones.All(b => !string.IsNullOrEmpty(b)), $"{piece.Who}/{piece.Item}: a bone without a name");
                }
            }
            // what one person can wear the others can too: an item of one is made for all four (their own come in their models)
            var humans = styles.PlayerHumans.Select(h => h.GetComponent<HumanWardrobe>()).ToArray();
            var all = styles.Wardrobe.Select(p => p.Item).Concat(humans.SelectMany(h => h.Own.Select(w => w.Item))).Where(i => i != null).Distinct().ToArray();
            foreach (var h in humans)
                foreach (var item in all)
                    Assert.IsTrue(h.Own.Any(w => w.Item == item) || styles.Wardrobe.Any(p => p.Who == h.Who && p.Item == item), $"{h.Who} cannot wear {item}");
        }

        [Test]
        public void AnItemWornOnAPersonFollowsTheirSkeleton()
        {
            var styles = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var item = styles.Wardrobe?.FirstOrDefault(p => p != null && p.Who == "qa" && p.Slot == "hair");
            if (item == null) Assert.Ignore("the wardrobe is not built");
            var go = Object.Instantiate(styles.PlayerHumans[(int)Department.Qa]);
            try
            {
                var wardrobe = go.GetComponent<HumanWardrobe>();
                string own = wardrobe.ItemIn("hair");
                wardrobe.Wear(item);
                Assert.AreEqual(item.Item, wardrobe.ItemIn("hair"));
                var worn = go.GetComponentsInChildren<WearPiece>().Single();
                var skeleton = wardrobe.Model.GetComponentsInChildren<Transform>().ToArray();
                foreach (var part in worn.Parts)
                    Assert.IsTrue(part.Renderer.bones.All(b => b != null && skeleton.Contains(b)), "every bone of the item is a bone of the wearer");
                Assert.IsTrue(wardrobe.Own.First(w => w.Slot == "hair").Renderers.All(r => !r.gameObject.activeSelf), "the own hair is hidden under the new one");
                wardrobe.WearOwn("hair");
                Assert.AreEqual(own, wardrobe.ItemIn("hair"));
                Assert.IsEmpty(go.GetComponentsInChildren<WearPiece>(), "the swapped item is gone");
                wardrobe.TakeOff("head");
                Assert.IsNull(wardrobe.ItemIn("head"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
