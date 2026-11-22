using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The rigged humans shown instead of the player model: real-size people of four departments that can move.</summary>
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
        public void ASeatedBodyDoesNotWalk()
        {
            Assert.AreEqual(0f, HumanMath.WalkSpeed(9f, true), "riding in the car is not walking");
            Assert.AreEqual(3f, HumanMath.WalkSpeed(3f, false));
        }

        [Test]
        public void TheFeetStandOnTheFloorNotOverIt()
        {
            // the capsule floats ~9 cm over the floor: the body goes down so the lower ankle is its height over the floor
            Assert.AreEqual(-0.09f, HumanMath.PlantShift(0.17f, 0f, 0.08f), 1e-4f);
            Assert.AreEqual(0.02f, HumanMath.PlantShift(0.06f, 0f, 0.08f), 1e-4f, "sunk a little: up");
            Assert.AreEqual(-0.15f, HumanMath.PlantShift(2f, 0f, 0.08f), 1e-4f, "high in the air (falling): no dragging down");
            Assert.IsTrue(HumanMath.Planted(false, false, BodyPose.None));
            Assert.IsTrue(HumanMath.Planted(false, false, BodyPose.Work));
            Assert.IsFalse(HumanMath.Planted(false, true, BodyPose.None), "seated");
            Assert.IsFalse(HumanMath.Planted(true, false, BodyPose.None), "lying");
            Assert.IsFalse(HumanMath.Planted(false, false, BodyPose.Ladder), "on a ladder");
        }

        [Test]
        public void TheBodyIsBusyWithOneThingAtATime()
        {
            Assert.AreEqual(BodyPose.Ladder, HumanMath.PoseOf(true, true, true), "on a ladder nothing else shows");
            Assert.AreEqual(BodyPose.Terminal, HumanMath.PoseOf(false, true, true));
            Assert.AreEqual(BodyPose.Work, HumanMath.PoseOf(false, false, true));
            Assert.AreEqual(BodyPose.None, HumanMath.PoseOf(false, false, false));
            Assert.AreEqual(BodyPose.None, HumanMath.Shown(BodyPose.Work, downed: true, seated: false), "knocked out, the hands are not at work");
            Assert.AreEqual(BodyPose.None, HumanMath.Shown(BodyPose.Terminal, downed: false, seated: true));
            Assert.AreEqual(BodyPose.Ladder, HumanMath.Shown(BodyPose.Ladder, false, false));
            // a hold's progress is kept when E is let go: only its growing is work
            Assert.IsTrue(HumanMath.Growing(0.31f, 0.3f));
            Assert.IsFalse(HumanMath.Growing(0.3f, 0.3f));
            Assert.IsFalse(HumanMath.Growing(0f, 0.3f));
        }

        [Test]
        public void TheClimbTakeFollowsTheLadder()
        {
            Assert.AreEqual(1f, HumanMath.ClimbRate(HumanMath.ClimbTakeSpeed), 1e-4f, "its own speed up the ladder");
            Assert.Less(HumanMath.ClimbRate(-1f), 0f, "backwards on the way down");
            Assert.AreEqual(0f, HumanMath.ClimbRate(0.01f), "hanging on the ladder: still");
            Assert.AreEqual(3f, HumanMath.ClimbRate(50f), "a teleport does not spin the arms");
            Assert.AreEqual(0f, HumanMath.HeadShare(0.7f, BodyPose.Terminal), "at a terminal the take moves the head");
            Assert.AreEqual(0.7f, HumanMath.HeadShare(0.7f, BodyPose.Ladder), "on a ladder the head looks where the player does");
            Assert.AreEqual(0f, HumanMath.WalkSpeed(3f, true));
        }

        [Test]
        public void EveryStyleShowsTheDepartments()
        {
            var styles = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            Assert.AreEqual(4, styles.PlayerHumans.Length, "one rigged human per department");
            foreach (var style in styles.Styles.Where(s => s != null))
                Assert.IsNull(style.Geometry.PlayerRig, $"{style.name} has a human of its own: its players would not show their departments");
            Assert.AreEqual(ModelFamily.Realistic, styles.Styles.First(s => s != null && s.name == "S16_Derelict").Geometry.Family, "S16 is a realistic style now (the derelict look is the abandoned station's)");
        }

        [Test]
        public void DepartmentHumansWork([Values(0, 1, 2, 3)] int department)
        {
            var prefab = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans[department];
            Assert.IsNotNull(prefab, "no rigged human");

            var rig = prefab.GetComponent<HumanRig>();
            Assert.IsNotNull(rig);
            Assert.IsNotNull(rig.HeadBone, "no head bone");
            Assert.IsNotNull(rig.Animator);
            var controller = rig.Animator.runtimeAnimatorController;
            Assert.IsNotNull(controller, "no animator controller");
            var clips = controller.animationClips.Select(c => c.name).ToArray();
            foreach (var n in new[] { "Idle", "Walk", "Run", "CrouchIdle", "CrouchWalk", "Down", "Sit", "Climb", "Terminal", "Work" })
                Assert.Contains(n, clips, $"the controller has no {n} clip");
            Assert.IsFalse(rig.Animator.applyRootMotion, "the player controller moves the body, not the animation");

            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), "the human is decoration: no colliders");
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            // (EyeDetails — headphones, glasses, a belt — is worn by some and not by others)
            foreach (var part in new[] { "BodyClothes", "AccentHead", "AccentBand", "Sclera", "Outfit" })
                Assert.IsTrue(renderers.Any(r => r.name == part), $"no part {part} (PlayerLooks paints by part name)");
            // lit per pixel: every part has the person's baked maps — albedo, a normal map (not the eyes), occlusion and smoothness
            int triangles = 0;
            foreach (var r in renderers)
            {
                var m = r.sharedMaterial;
                Assert.IsNotNull(m, $"{r.name}: no material");
                Assert.IsNotNull(m.GetTexture("_BaseMap"), $"{r.name}: no baked albedo");
                Assert.IsNotNull(m.GetTexture("_MetallicGlossMap"), $"{r.name}: no baked mask (occlusion, smoothness)");
                if (r.name != "Sclera") Assert.IsNotNull(m.GetTexture("_BumpMap"), $"{r.name}: no baked normal map");
                Assert.IsTrue(r.sharedMesh.uv.Length == r.sharedMesh.vertexCount, $"{r.name}: no UVs");
                Assert.IsTrue(r.sharedMesh.tangents.Length == r.sharedMesh.vertexCount, $"{r.name}: no tangents (the normal map needs them)");
                Assert.LessOrEqual(r.quality == SkinQuality.Auto ? 4 : (int)r.quality, 4);
                triangles += r.sharedMesh.triangles.Length / 3;
            }
            Assert.That(triangles, Is.InRange(9000, 19000), "the triangle budget of a player");
            // the four are different people: of different heights, and the head's mesh is not shared between departments
            var heights = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans.Select(h => h.GetComponentInChildren<Animator>(true).transform.localScale.y).ToArray();
            Assert.Greater(heights.Max() - heights.Min(), 0.05f, "the tallest and the shortest differ by a few centimetres at least");
            var heads = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans
                .Select(h => h.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "AccentHead").sharedMesh).ToArray();
            Assert.AreEqual(heads.Length, heads.Distinct().Count(), "each department has a face of its own");
            // the bones the takes and the head tilt need
            var bones = prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (var bone in new[] { "hips", "spine", "chest", "neck", "head", "upper_arm.L", "forearm.R", "hand.L", "fingers.L", "thumb.R", "thigh.L", "shin.R", "foot.L", "toe.R" })
                Assert.Contains(bone, bones, $"no bone {bone}");
            var planted = prefab.GetComponent<HumanRig>();
            Assert.IsTrue(planted.Hips != null && planted.FootL != null && planted.FootR != null, "the bones that plant the feet");
            Assert.That(planted.AnkleHeight, Is.InRange(0.03f, 0.16f), "the ankle's height over the sole");

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
