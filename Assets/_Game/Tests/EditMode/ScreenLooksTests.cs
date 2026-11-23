using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using Stealth.Player;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Looks decided by small pure rules: the furniture's screens (off, static, cracked), a ceiling grate's hinge side,
    /// the pieces that keep their front clear, what of his own body a player sees, and the styles the game keeps.
    /// </summary>
    public class ScreenLooksTests
    {
        [Test]
        public void MostScreensAreOffSomeShowStaticSomeAreCracked()
        {
            var counts = new int[3];
            for (int i = 0; i < 4000; i++) counts[(int)ScreenLooks.Pick(LevelRandom.Mix(12345, Salt.Screens, i))]++;
            Assert.That(counts[(int)ScreenLook.Off], Is.InRange(1900, 2500), "about 55 % off");
            Assert.That(counts[(int)ScreenLook.Static], Is.InRange(800, 1200), "about 25 % static");
            Assert.That(counts[(int)ScreenLook.Cracked], Is.InRange(600, 1000), "about 20 % cracked");
            Assert.AreEqual(ScreenLooks.Pick(77), ScreenLooks.Pick(77), "the same on every peer");
            Assert.IsTrue(System.Enum.IsDefined(typeof(ScreenLook), ScreenLooks.Pick(-12345)), "any seed, a negative one too");
            // the static jumps about the texture, staying on it
            var seen = new HashSet<Vector2>();
            for (int k = 0; k < 50; k++)
            {
                var at = ScreenLooks.Jump(k);
                Assert.That(at.x, Is.InRange(0f, 1f));
                Assert.That(at.y, Is.InRange(0f, 1f));
                seen.Add(at);
            }
            Assert.Greater(seen.Count, 45, "a new place every step");
        }

        [Test]
        public void TheHouseHasMaterialsForEveryScreenLook()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            Assert.IsNotNull(art);
            Assert.AreEqual(4, art.Screens.Length, "the models' screen, then off, static, cracked");
            foreach (var m in art.Screens) Assert.IsNotNull(m);
            Assert.AreEqual(4, art.Screens.Distinct().Count());
            Assert.IsFalse(art.Screens[1 + (int)ScreenLook.Off].IsKeywordEnabled("_EMISSION"), "a screen switched off does not glow");
            Assert.IsNotNull(art.Screens[1 + (int)ScreenLook.Static].GetTexture("_EmissionMap"), "static glows through its noise");
            Assert.IsNotNull(art.Screens[1 + (int)ScreenLook.Cracked].GetTexture("_BaseMap"), "the cracks");
        }

        [Test]
        public void ACeilingGratesLidHangsAwayFromTheDoorway()
        {
            float ceiling = LevelMetrics.WallHeight;
            var grate = new Vector3(5.5f, ceiling, 3.5f);
            Doorway Door(float x, float z, float y = 0f) => new() { Kind = EdgeType.Door, Position = new Vector3(x, y, z), Rotation = Quaternion.identity, Width = 1.6f };
            // the doorway behind the grate (−Z): as built the lid would hang across it; it is turned to hang on the far side
            float yaw = VentPlan.LidYaw(grate, new[] { Door(5.5f, 2.5f) });
            var hinge = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            Assert.Greater(hinge.z, 0.9f, "the hinge on the side away from the door");
            // a doorway to the +X: the hinge to −X
            yaw = VentPlan.LidYaw(grate, new[] { Door(6.6f, 3.5f) });
            Assert.Less((Quaternion.Euler(0f, yaw, 0f) * Vector3.back).x, -0.9f);
            // a doorway on another storey says nothing; no doorways: as built
            Assert.AreEqual(0f, VentPlan.LidYaw(grate, new[] { Door(5.5f, 2.5f, LevelMetrics.FloorHeight) }));
            Assert.AreEqual(0f, VentPlan.LidYaw(grate, new Doorway[0]));
        }

        [Test]
        public void WhatIsLookedAtFromItsFrontKeepsItClear()
        {
            Assert.IsTrue(Furnisher.NeedsFront(PropKind.LogoTotem), "a totem's screen is not put against the side of a cupboard");
            Assert.IsTrue(Furnisher.NeedsFront(PropKind.Cabinet), "nor a chest's drawers");
            Assert.IsFalse(Furnisher.NeedsFront(PropKind.Plant));
            foreach (var k in Furnisher.Faced)
            {
                Assert.IsFalse(Furnisher.Opens.Contains(k), $"{k}: either it opens or it is looked at");
                Assert.Greater(Furnisher.OpenReach(k), Furnisher.OpenReach(PropKind.Cabinet), $"{k}: seen from a step back");
            }
        }

        [Test]
        public void ThePlayerLooksOutOfHisOwnBody()
        {
            // what is on the head is not seen from inside it (it still casts its shadow); the rest of the body is
            foreach (var n in new[] { "Hair.hair", "Outfit.hair", "Hair.beard", "BodyClothes.head", "EyeDetails.eyes", "Sclera" })
                Assert.IsTrue(HumanMath.OnTheHead(n), n);
            foreach (var n in new[] { "BodyClothes.top", "Outfit.bottom", "Outfit.feet", "AccentHead", "AccentBand.top", "" })
                Assert.IsFalse(HumanMath.OnTheHead(n), n);
            // the body hangs from the eyes: moved so that its head (leant forward by a take) is at them
            var eyes = new Vector3(2f, 1.6f, 3f);
            var head = new Vector3(2f, 1.42f, 3.25f);
            var shift = HumanMath.EyeShift(eyes, head, Vector3.forward, 1f);
            var at = head + shift + Vector3.up * HumanMath.EyesUp + Vector3.forward * HumanMath.EyesAhead;
            Assert.Less((at - eyes).magnitude, 1e-4f, "the eyes of the body are at the camera");
            Assert.Less(shift.z, 0f, "a body leaning forward is moved back under the eyes");
            Assert.LessOrEqual(HumanMath.EyeShift(eyes, eyes + Vector3.forward * 5f, Vector3.forward, 1f).magnitude, HumanMath.EyeReach + 1e-4f, "never far");
            // his own flashlight leaves his own body out
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab").GetComponent<PlayerController>();
            Assert.AreEqual("OwnBody", LayerMask.LayerToName(PlayerController.OwnBodyLayer));
            Assert.AreEqual(0, player.Flashlight.cullingMask & (1 << PlayerController.OwnBodyLayer), "no shadows of the owner's body in his own beam");
            Assert.IsNotNull(player.EyeGlow);
            Assert.AreEqual(LightShadows.None, player.EyeGlow.shadows);
            Assert.IsNull(player.EyeGlow.GetComponent<LightSource>(), "robots do not see by it");
        }

        [Test]
        public void TheGameKeepsTwoStyles()
        {
            var config = AssetDatabase.LoadAssetAtPath<StyleConfig>("Assets/_Game/Data/Styles/Styles.asset");
            Assert.IsNotNull(config);
            CollectionAssert.AreEquivalent(new[] { "S06_Storybook", "S16_Derelict" }, config.Styles.Select(s => s.name).ToArray());
            Assert.AreEqual("S16_Derelict", config.Default.name);
            // a dark room is dark: the house-wide fill is small, the lamps light their own rooms
            var quiet = config.Styles.First(s => s.name == "S16_Derelict");
            Assert.Less(quiet.World.Ambient.maxColorComponent, 0.13f, "a room with its lamps off needs a flashlight");
            Assert.Greater(quiet.World.CeilingGlow, 0f, "a lit room's own lamps light its ceiling");
        }
    }
}
