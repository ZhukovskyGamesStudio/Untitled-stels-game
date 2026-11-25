using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Ceiling lamps that gather their light under themselves (<see cref="LightMath.PoolCookie"/>, the style's
    /// <see cref="StyleWorld.LampPool"/>): the look of a room with depth, for the eye only.
    /// </summary>
    public class LampPoolTests
    {
        [Test]
        public void APooledLampShinesMostlyDownAndNothingMoreGoesUp()
        {
            const float up = LightMath.SoftCeilingShare;
            Assert.AreEqual(1f, LightMath.PoolCookie(-1f, up), 1e-4f, "straight down: all of it");
            Assert.AreEqual(LightMath.PoolSide, LightMath.PoolCookie(-0.03f, up), 0.03f, "level with the lamp: its side share");
            float before = 2f;
            for (float y = -1f; y <= 1f; y += 0.05f)
            {
                float v = LightMath.PoolCookie(y, up);
                Assert.LessOrEqual(v, before + 1e-4f, $"at {y:0.00}: the light only falls off from straight down to straight up");
                Assert.LessOrEqual(v, LightMath.Cookie(y, up) + 1e-4f, $"at {y:0.00}: never more than the even lamp gives — nothing new reaches a ceiling or the room above");
                before = v;
            }
        }

        /// <summary>
        /// Robots read the even lamp (<see cref="LightSource.Brightness"/>); the player sees the pooled one. The two must
        /// stay close: over all the directions below the lamp the pooled light, boosted, is about what the game counts,
        /// and nowhere is it more than half as much again or less than half.
        /// </summary>
        [Test]
        public void WhatThePlayerSeesStaysNearWhatTheRobotsCount()
        {
            float sum = 0f;
            int n = 0;
            for (float down = 0.005f; down < 1f; down += 0.01f, n++)
            {
                float seen = LightMath.PoolCookie(-down, 0f) * LightMath.PoolBoost;
                sum += seen;
                Assert.That(seen, Is.InRange(0.5f, 1.5f), $"{down:0.00} of the way down: the look and the game too far apart");
            }
            Assert.That(sum / n, Is.InRange(0.85f, 1.1f), "in all, a pooled lamp gives the room what an even one does");
        }

        [Test]
        public void TheLampPrefabHasItsPooledCookieAndTheDefaultStyleUsesIt()
        {
            var lamp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Level/RoomLight.prefab").GetComponent<RoomLight>();
            Assert.IsNotNull(lamp.PoolCookie, "RoomLight has no pooled cookie (rebuild the prefab)");
            Assert.AreNotSame(lamp.SoftCookie, lamp.PoolCookie);
            var style = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.Default;
            Assert.Greater(style.World.LampPool, 0f, $"{style.name}: even lamps — every room lit flat, like a toy shop");
            Assert.AreEqual(1f, style.World.LightScale, 1e-3f, $"{style.name}: the lamps' boost is the pooled lamps' own, not every light's");
        }
    }
}
