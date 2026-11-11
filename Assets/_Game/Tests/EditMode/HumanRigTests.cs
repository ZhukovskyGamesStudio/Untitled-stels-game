using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The rigged human a style shows instead of the player model (S11): a real-size person that can move.</summary>
    public class HumanRigTests
    {
        [Test]
        public void HeadFollowsThePitchWithinLimits()
        {
            Assert.AreEqual(14f, HumanMath.HeadPitch(20f, 0.7f, false), 1e-4f);
            Assert.AreEqual(45f, HumanMath.HeadPitch(88f, 0.7f, false), 1e-4f);
            Assert.AreEqual(-45f, HumanMath.HeadPitch(-88f, 0.7f, false), 1e-4f);
            Assert.AreEqual(0f, HumanMath.HeadPitch(60f, 0.7f, true), 1e-4f, "a downed player's head lies still");
        }

        [Test]
        public void DerelictStyleHasAWorkingHuman()
        {
            var styles = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var style = styles.Styles.FirstOrDefault(s => s != null && s.name == "S16_Derelict");
            Assert.IsNotNull(style, "no S11 style");
            Assert.AreEqual(ModelFamily.Derelict, style.Geometry.Family);
            var prefab = style.Geometry.PlayerRig;
            Assert.IsNotNull(prefab, "S11 has no rigged human");

            var rig = prefab.GetComponent<HumanRig>();
            Assert.IsNotNull(rig);
            Assert.IsNotNull(rig.HeadBone, "no head bone");
            Assert.IsNotNull(rig.Animator);
            var controller = rig.Animator.runtimeAnimatorController;
            Assert.IsNotNull(controller, "no animator controller");
            var clips = controller.animationClips.Select(c => c.name).ToArray();
            foreach (var n in new[] { "Idle", "Walk", "Run", "CrouchIdle", "CrouchWalk", "Down" })
                Assert.Contains(n, clips, $"the controller has no {n} clip");
            Assert.IsFalse(rig.Animator.applyRootMotion, "the player controller moves the body, not the animation");

            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), "the human is decoration: no colliders");
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var part in new[] { "BodyClothes", "Coat", "AccentHead", "AccentBand", "EyeDetails" })
                Assert.IsTrue(renderers.Any(r => r.name == part), $"no part {part} (PlayerLooks paints by part name)");

            // a real adult, standing on the floor, facing +Z
            var go = Object.Instantiate(prefab);
            try
            {
                // standing: the pose of the Idle clip (the model's own default pose is whatever the exporter left)
                controller.animationClips.First(c => c.name == "Idle").SampleAnimation(go.GetComponent<HumanRig>().Animator.gameObject, 0f);
                // the posed geometry in world space (skinned bounds are not updated in edit mode)
                Bounds Baked(SkinnedMeshRenderer r)
                {
                    var m = new Mesh();
                    r.BakeMesh(m, true);
                    var pts = m.vertices.Select(v => r.transform.TransformPoint(v)).ToArray();
                    Object.DestroyImmediate(m);
                    var bb = new Bounds(pts[0], Vector3.zero);
                    foreach (var p in pts) bb.Encapsulate(p);
                    return bb;
                }
                var parts = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                var b = parts.Select(Baked).Aggregate((x, y) => { x.Encapsulate(y); return x; });
                Assert.That(b.size.y, Is.InRange(1.6f, 1.9f), "height");
                Assert.That(b.min.y, Is.InRange(-0.05f, 0.05f), "feet on the floor");
                Assert.That(b.size.x, Is.LessThan(0.9f), "lying on its side?");
                var head = go.GetComponentsInChildren<Transform>().First(t => t.name == "head");
                var eyes = Baked(parts.First(r => r.name == "Sclera"));
                Assert.Greater(eyes.center.z, head.position.z, "the face looks forward (+Z)");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
