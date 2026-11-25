using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Contact shadows of the picture (<see cref="ContactShadows"/>): which lamps the pass walks towards.</summary>
    public class ContactShadowTests
    {
        [Test]
        public void TheNearestBurningLampsAreChosen()
        {
            var lamps = new List<(Vector3, float)>();
            for (int i = 0; i < 20; i++) lamps.Add((new Vector3(i, 3f, 0f), i == 1 ? 0f : 1f + i)); // lamp 1 is out
            lamps.Add((new Vector3(0f, 3f, ContactShadows.Reach + 5f), 9f)); // too far
            var into = new Vector4[ContactShadows.Lamps];
            int n = ContactShadows.Choose(lamps, new Vector3(0f, 1.6f, 0f), into);
            Assert.AreEqual(ContactShadows.Lamps, n, "no more than the pass walks towards");
            Assert.AreEqual(0f, into[0].x, 1e-4f, "the nearest first");
            Assert.AreEqual(1f, into[0].w, 1e-4f, "its light rides along");
            for (int i = 0; i < n; i++)
            {
                Assert.AreNotEqual(1f, into[i].x, "a lamp that is out shades nothing");
                Assert.Less(Mathf.Abs(into[i].z), 1f, "a lamp out of reach is left out");
                if (i > 0) Assert.GreaterOrEqual(into[i].x, into[i - 1].x);
            }
            Assert.AreEqual(0, ContactShadows.Choose(new List<(Vector3, float)>(), Vector3.zero, into));
        }

        [Test]
        public void OnlyTheDefaultStyleDrawsThem()
        {
            var config = AssetDatabase.LoadAssetAtPath<StyleConfig>("Assets/_Game/Data/Styles/Styles.asset");
            foreach (var style in config.Styles)
                if (style.name == "S16_Derelict") Assert.That(style.Grade.Contact, Is.InRange(0.2f, 1f));
                else Assert.AreEqual(0f, style.Grade.Contact, $"{style.name}: a drawn style has its own shading");
        }
    }
}
