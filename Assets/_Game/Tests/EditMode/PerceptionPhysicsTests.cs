using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Eyes and ears against real colliders: how much of a body furniture hides, how walls muffle sound.</summary>
    public class PerceptionPhysicsTests
    {
        readonly List<GameObject> m_Objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Objects) Object.DestroyImmediate(go);
            m_Objects.Clear();
        }

        GameObject Box(Vector3 center, Vector3 size, SoundMaterial? material = null)
        {
            var go = new GameObject("Box");
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
            if (material.HasValue) go.AddComponent<SoundBlocker>().Material = material.Value;
            m_Objects.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        static readonly Vector3 Eye = new(0, 1.6f, 0);
        static FakeIntruder PlayerAt(float z) => new() { Position = new Vector3(0, 0, z), Height = 1.8f };

        [Test]
        public void InTheOpenTheWholeBodyIsVisible()
        {
            Assert.AreEqual(1f, Perception.Exposure(Eye, PlayerAt(5f)), 1e-4f);
        }

        [Test]
        public void ADeskHidesTheLegsAWallHidesEverything()
        {
            Box(new Vector3(0, 0.4f, 4.3f), new Vector3(2f, 0.8f, 0.8f)); // a desk right in front of the player
            float behindDesk = Perception.Exposure(Eye, PlayerAt(5f));
            Assert.That(behindDesk, Is.InRange(0.3f, 0.8f), "head and chest stick out above the desk");

            Box(new Vector3(0, 1.5f, 3f), new Vector3(4f, 3f, 0.2f));
            Assert.AreEqual(0f, Perception.Exposure(Eye, PlayerAt(5f)), 1e-4f);
        }

        [Test]
        public void SoundLosesMoreThroughAWallThanThroughFurniture()
        {
            var settings = new HearingSettings();
            var from = new Vector3(0, 1f, 0);
            var to = new Vector3(0, 1f, 6f);
            Assert.AreEqual(1f, Perception.SoundTransmission(from, to, settings), 1e-4f);

            Box(new Vector3(0, 0.4f, 2f), new Vector3(1f, 1.5f, 0.6f)); // a cabinet, no marker: furniture
            Assert.AreEqual(settings.Furniture, Perception.SoundTransmission(from, to, settings), 1e-4f);

            Box(new Vector3(0, 1.5f, 4f), new Vector3(4f, 3f, 0.2f), SoundMaterial.Wall);
            Assert.AreEqual(settings.Furniture * settings.Wall, Perception.SoundTransmission(from, to, settings), 1e-4f);
        }
    }
}
