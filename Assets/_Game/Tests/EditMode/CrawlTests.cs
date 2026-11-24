using System.Collections.Generic;
using NUnit.Framework;
using Stealth.Level;
using Stealth.Player;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The player's real capsule (the prefab's controller) through every opening he is meant to fit, with its real frame in it.</summary>
    public class CrawlTests
    {
        readonly List<GameObject> m_Objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Objects) Object.DestroyImmediate(go);
            m_Objects.Clear();
        }

        void Box(Vector3 min, Vector3 max)
        {
            var go = new GameObject("Box");
            go.transform.position = (min + max) / 2f;
            go.AddComponent<BoxCollider>().size = max - min;
            m_Objects.Add(go);
        }

        /// <summary>A wall across Z = 0 with an opening in it over a floor; its frame is the real prefab.</summary>
        void Wall(float clear, float sill, float top, string frame)
        {
            float t = LevelMetrics.WallThickness / 2f, h = LevelMetrics.WallHeight, y = 500f;
            Box(new Vector3(-4f, y - 0.5f, -4f), new Vector3(4f, y, 4f));
            Box(new Vector3(-4f, y, -t), new Vector3(-clear / 2f, y + h, t));
            Box(new Vector3(clear / 2f, y, -t), new Vector3(4f, y + h, t));
            Box(new Vector3(-clear / 2f, y + top, -t), new Vector3(clear / 2f, y + h, t));
            if (sill > 0f) Box(new Vector3(-clear / 2f, y, -t), new Vector3(clear / 2f, y + sill, t));
            if (frame == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Level/{frame}.prefab");
            Assert.IsNotNull(prefab, frame);
            var go = Object.Instantiate(prefab, new Vector3(0f, y, 0f), Quaternion.identity);
            m_Objects.Add(go);
            if (go.TryGetComponent<VentGrille>(out var grille)) grille.Mounted.SetActive(false); // the grille is off
        }

        /// <summary>Walks the capsule towards +Z from before the wall; how far it got.</summary>
        float Walk(bool crouched, float from, float x)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab").GetComponent<CharacterController>();
            var go = new GameObject("Capsule");
            var cc = go.AddComponent<CharacterController>();
            cc.radius = player.radius;
            cc.skinWidth = player.skinWidth;
            cc.height = crouched ? PlayerController.CrouchHeight : PlayerController.StandHeight;
            cc.center = new Vector3(0f, cc.height / 2f, 0f);
            go.transform.position = new Vector3(x, 500f + from, -1.2f);
            Physics.SyncTransforms();
            for (int i = 0; i < 120; i++)
            {
                var step = new Vector3(0f, -0.03f, 0.03f);
                cc.stepOffset = PlayerMotor.StepFor(cc, step, player.stepOffset);
                cc.Move(step);
            }
            float z = go.transform.position.z;
            Object.DestroyImmediate(go); // (out of the next one's way)
            return z;
        }

        [Test]
        public void ACrouchedPlayerCrawlsThroughAHoleInAWall()
        {
            Wall(LevelMetrics.ClearWidth(LevelMetrics.HoleWidth), 0f, LevelMetrics.HoleHeight, "HoleFrame");
            Assert.Less(Walk(crouched: false, 0.05f, 0f), 0f, "standing he does not fit");
            foreach (float x in new[] { 0f, 0.12f, -0.12f })
                Assert.Greater(Walk(crouched: true, 0.05f, x), 1f, $"crouched, {x:0.00} m off its middle");
        }

        [Test]
        public void APlayerWalksThroughABreachAndASqueeze()
        {
            Wall(LevelMetrics.ClearWidth(LevelMetrics.BreachWidth), 0f, LevelMetrics.DoorHeight, "BreachFrame");
            Assert.Greater(Walk(crouched: false, 0.05f, 0.3f), 1f);
            TearDown();
            Wall(LevelMetrics.ClearWidth(LevelMetrics.SqueezeWidth), 0f, LevelMetrics.DoorHeight, "SqueezeFrame");
            Assert.Greater(Walk(crouched: false, 0.05f, 0.1f), 1f);
        }

        [Test]
        public void ACrouchedPlayerPassesAnInnerWindowAndAVent()
        {
            // from a jump (onto the sill's height), crouched
            Wall(LevelMetrics.ClearWidth(LevelMetrics.InnerWindowWidth), LevelMetrics.InnerWindowSill, LevelMetrics.InnerWindowSill + LevelMetrics.InnerWindowHeight, "InnerWindowFrame");
            Box(new Vector3(-1f, 500f, -2f), new Vector3(1f, 500f + LevelMetrics.InnerWindowSill, -LevelMetrics.WallThickness / 2f));
            Assert.Greater(Walk(crouched: true, LevelMetrics.InnerWindowSill + 0.05f, 0f), 1f, "over the sill");
            TearDown();
            Wall(LevelMetrics.ClearWidth(LevelMetrics.VentWidth), LevelMetrics.VentSill, LevelMetrics.VentSill + LevelMetrics.VentHeight, "VentFrame");
            Assert.Greater(Walk(crouched: true, 0.05f, 0f), 1f, "a step up into the duct");
        }

        [Test]
        public void AStepIsStillClimbedCrouched()
        {
            Wall(2f, 0f, LevelMetrics.DoorHeight, null);
            Box(new Vector3(-1f, 500f, 0.3f), new Vector3(1f, 500.29f, 2f));
            Assert.Greater(Walk(crouched: true, 0.05f, 0f), 1f, "a stair's step");
        }
    }
}
