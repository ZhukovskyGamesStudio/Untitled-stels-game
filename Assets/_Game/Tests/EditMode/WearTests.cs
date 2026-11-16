using NUnit.Framework;
using Stealth.Configs;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Rust lands on parts of an object, never more than the object's own level.</summary>
    public class WearTests
    {
        [Test]
        public void AFlashlightAgainstASurfaceDoesNotBurnIt()
        {
            const float near = 1.6f;
            Assert.AreEqual(1f, Stealth.Player.PlayerRules.FlashlightNear(3f, near), "far: full light");
            Assert.AreEqual(1f, Stealth.Player.PlayerRules.FlashlightNear(near, near), 1e-5f);
            // the light on the surface (intensity / d²) stays at most what it is at the near distance
            foreach (float d in new[] { 0.3f, 0.5f, 0.8f, 1.2f })
            {
                float onSurface = Stealth.Player.PlayerRules.FlashlightNear(d, near) / (d * d);
                Assert.LessOrEqual(onSurface, 1f / (near * near) + 1e-4f, $"at {d} m");
            }
        }

        [Test]
        public void PartsRustUpToTheirObjectAndDiffer()
        {
            string[] parts = { "Body", "BodyDrawer0", "BodyDrawer1", "BodyDrawer2", "Accent", "BodyDoor0", "BodyDoor1" };
            int mixed = 0, levels = 0;
            for (int i = 0; i < 300; i++)
            {
                var at = new Vector3(i * 1.37f, 0f, i * 0.73f);
                int most = WearMath.Pick(at, 5);
                var seen = new System.Collections.Generic.HashSet<int>();
                foreach (var p in parts)
                {
                    int k = WearMath.PartLevel(at, p, 5);
                    Assert.That(k, Is.InRange(0, most), "a part rusts no more than its object");
                    Assert.AreEqual(k, WearMath.PartLevel(at, p, 5), "the same on every peer");
                    seen.Add(k);
                }
                if (most > 0 && seen.Count > 1) mixed++;
                if (most > 0) levels++;
            }
            Assert.Greater(mixed, levels / 2, "most rusty objects have parts of different rust");
        }
    }
}
