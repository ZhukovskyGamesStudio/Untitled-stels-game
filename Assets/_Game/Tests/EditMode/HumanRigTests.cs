using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Player;
using UnityEngine;
using UnityEngine.Rendering;

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
        public void TheCommonSkeletonMakesAHumanoid()
        {
            var mapped = HumanSkeleton.Map.Select(m => m.human).ToArray();
            foreach (var bone in HumanSkeleton.Required) Assert.Contains(bone, mapped, $"{bone} is required by a humanoid Avatar");
            Assert.AreEqual(mapped.Length, mapped.Distinct().Count(), "every humanoid bone once");
            Assert.AreEqual(HumanSkeleton.Map.Length, HumanSkeleton.Map.Select(m => m.bone).Distinct().Count(), "every bone of ours once");
            // the T-pose: arms straight out to their side, flat; the spine up; the legs down
            Assert.AreEqual(Vector3.up, HumanSkeleton.Direction(HumanSkeleton.Aim.Up, Vector3.zero));
            Assert.AreEqual(Vector3.down, HumanSkeleton.Direction(HumanSkeleton.Aim.Down, Vector3.zero));
            var outward = HumanSkeleton.Direction(HumanSkeleton.Aim.Out, new Vector3(0.2f, 0.1f, 0.01f));
            Assert.AreEqual(0f, outward.y, 1e-5f, "flat");
            Assert.Greater(outward.x, 0.99f);
            var arm = new Vector3(0.3f, -0.5f, 0.05f); // an A-pose arm, down and out
            Assert.Less(Vector3.Angle(HumanSkeleton.Turn(arm, outward) * arm, outward), 0.01f, "turned straight out");
            Assert.AreEqual(Quaternion.identity, HumanSkeleton.Turn(Vector3.zero, outward));
            // every bone the T-pose turns is ours
            var bones = HumanSkeleton.Map.Select(m => m.bone).ToArray();
            foreach (var (bone, child, _) in HumanSkeleton.TPose)
            {
                Assert.Contains(bone, bones);
                if (child != null) Assert.Contains(child, bones); // (a finger's last bone has no child)
            }
        }

        [Test]
        public void TheHandCarriesWhatItHoldsWhenItIsFree()
        {
            Assert.IsTrue(HumanMath.Carries(true, false, false, BodyPose.None));
            Assert.IsFalse(HumanMath.Carries(false, false, false, BodyPose.None), "nothing in the hand");
            Assert.IsFalse(HumanMath.Carries(true, true, false, BodyPose.None), "lying");
            Assert.IsFalse(HumanMath.Carries(true, false, true, BodyPose.None), "seated (the hands on the wheel or the lap)");
            Assert.IsFalse(HumanMath.Carries(true, false, false, BodyPose.Work), "the hands at work");
            Assert.IsFalse(HumanMath.Carries(true, false, false, BodyPose.Ladder), "on the rungs");
            var grip = HumanMath.Grip(Vector3.zero, new Vector3(0.1f, 0f, 0f), Vector3.up, 1f);
            Assert.That(grip.x, Is.InRange(0.06f, 0.1f), "by the knuckles: in the closed fingers, not at the wrist");
            Assert.That(grip.y, Is.InRange(-0.01f, 0.04f), "at the palm, not floating over it");
            Assert.Greater(HumanSkeleton.Carry.z, 0.15f, "before the chest");
            Assert.Greater(HumanSkeleton.Carry.x, 0f, "the right hand");
            Assert.IsTrue(HumanSkeleton.Grip.Any(g => g.muscle.StartsWith("RightHand.Index") && g.value < -0.5f), "the fingers curl");
            foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                Assert.AreEqual(3, HumanSkeleton.Grip.Count(g => g.muscle.StartsWith($"RightHand.{finger}.") && g.muscle.EndsWith("Stretched")), $"every joint of the {finger}");
            Assert.Less(HumanSkeleton.Grip.Where(g => g.muscle.Contains("Index")).Min(g => g.value), HumanSkeleton.Cup.Where(g => g.muscle.Contains("Index")).Min(g => g.value), "a cupped palm is more open than a fist");
            var cup = HumanMath.Grip(Vector3.zero, new Vector3(0.1f, 0f, 0f), Vector3.up, 1f, true);
            Assert.Less(cup.x, grip.x, "a compact thing lies over the middle of the palm, not at the knuckles");
            Assert.AreEqual(15 * 2, HumanSkeleton.Map.Count(m => m.human >= HumanBodyBones.LeftThumbProximal && m.human <= HumanBodyBones.RightLittleDistal), "three bones for every finger of both hands");
        }

        [Test]
        public void TheFeetStandOnTheFloorNotOverIt()
        {
            // the capsule floats ~9 cm over the floor: the body goes down so the lower ankle is its height over the floor
            Assert.AreEqual(-0.09f, HumanMath.PlantShift(0.17f, 0f, 0.08f), 1e-4f);
            Assert.AreEqual(0.02f, HumanMath.PlantShift(0.06f, 0f, 0.08f), 1e-4f, "sunk a little: up");
            Assert.AreEqual(-0.15f, HumanMath.PlantShift(2f, 0f, 0.08f), 1e-4f, "high in the air (falling): no dragging down");
            // a sole is where the foot stands: under the ankle when flat, under the ball when the heel is up
            Assert.AreEqual(0f, HumanMath.Sole(0.085f, 0.02f, 0.085f, 0.02f), 1e-5f, "flat on the floor");
            Assert.AreEqual(0f, HumanMath.Sole(0.16f, 0.02f, 0.085f, 0.02f), 1e-5f, "on its ball, the heel up: still on the floor");
            Assert.AreEqual(-0.03f, HumanMath.Sole(0.10f, -0.01f, 0.085f, 0.02f), 1e-5f, "the toes under the floor");
            Assert.AreEqual(0.015f, HumanMath.Sole(0.10f, float.PositiveInfinity, 0.085f, 0.02f), 1e-5f, "a rig without toes: by the ankle");
            // up at once, down slowly: a run's flight does not pull the body to the floor
            float up = HumanMath.PlantFollow(0f, 0.05f, 0.05f), down = HumanMath.PlantFollow(0f, -0.05f, 0.05f);
            Assert.Greater(up, 0.035f, "out of the floor within a frame or two");
            Assert.Greater(down, -0.012f, "a twentieth of a second in the air: the body hardly follows");
            Assert.AreEqual(-0.05f, HumanMath.PlantFollow(0f, -0.05f, 5f), 1e-3f, "standing: it settles onto the floor");
            Assert.AreEqual(0f, HumanMath.PlantFollow(0f, 0.05f, 0f), 1e-6f);
        }

        [Test]
        public void TheHandCarriesBeforeTheChestWhereverItIs()
        {
            var standing = new Vector3(0f, 1.25f, 0.02f);
            Assert.AreEqual(HumanSkeleton.Carry, HumanSkeleton.CarryAt(HumanSkeleton.Carry, standing, standing), "standing: where it was");
            // crouched and bowed: the chest 40 cm lower and 25 cm ahead — the hand goes with it, not over the head
            var crouched = standing + new Vector3(0f, -0.4f, 0.25f);
            var hand = HumanSkeleton.CarryAt(HumanSkeleton.Carry, crouched, standing);
            Assert.AreEqual(HumanSkeleton.Carry.y - 0.4f - 0.25f * HumanSkeleton.CarryBowDrop, hand.y, 1e-5f, "under the bowed head");
            Assert.AreEqual(HumanSkeleton.Carry.z + 0.25f, hand.z, 1e-5f);
            Assert.Less(hand.y, crouched.y + 0.1f, "no higher than the chest");
            var elbow = HumanSkeleton.CarryAt(HumanSkeleton.CarryElbow, crouched, standing);
            Assert.Less(((HumanSkeleton.Carry - HumanSkeleton.CarryElbow) - (hand - elbow)).magnitude, 1e-5f, "the arm keeps its shape");
        }

        [Test]
        public void TheStepsQuickenAboveTheTakesSpeed()
        {
            Assert.AreEqual(1f, HumanMath.StrideRate(0f, false));
            Assert.AreEqual(1f, HumanMath.StrideRate(HumanMath.WalkTakeSpeed, false), "below the run's speed the blend slows the steps");
            Assert.AreEqual(1f, HumanMath.StrideRate(HumanMath.RunTakeSpeed, false), 1e-5f);
            Assert.AreEqual(1.5f, HumanMath.StrideRate(HumanMath.RunTakeSpeed * 1.5f, false), 1e-4f, "a sprint: quicker steps, no sliding");
            Assert.AreEqual(2f, HumanMath.StrideRate(50f, false), "not a blur");
            Assert.AreEqual(1.6f, HumanMath.StrideRate(HumanMath.CrouchTakeSpeed * 1.6f, true), 1e-4f, "crouched: by the crouched walk's speed");
            Assert.Less(HumanMath.WalkTakeSpeed, HumanMath.RunTakeSpeed);
            Assert.Less(HumanMath.CrouchTakeSpeed, HumanMath.WalkTakeSpeed);
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

        /// <summary>
        /// The clips the game plays are the takes that were authored: every bone of every person, at a few moments of every
        /// take, is where anim.py put it. (A humanoid Avatar described in a wrong T-pose — worked out from the pose the file
        /// comes in instead of the bind pose, facing -Z, palms ahead — bent the knees sideways and twisted thighs and arms a
        /// quarter turn.) The reference is written by Stealth → Human Rig Audit; run it after exporting the takes anew.
        /// </summary>
        [Test]
        public void TheGamePlaysTheTakesAsAuthored([Values("backend", "qa", "sysadmin", "designer")] string who)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string file = System.IO.Path.Combine(Application.dataPath, "_Game/Tests/EditMode/HumanTakesReference.csv");
            Assert.IsTrue(System.IO.File.Exists(file), "no reference: run Tools/human_audit.sh");
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Art/Models/Humans/human_{who}.fbx");
            Assert.IsNotNull(asset);
            var clips = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans[0].GetComponent<HumanRig>().Animator.runtimeAnimatorController.animationClips;
            var go = Object.Instantiate(asset);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var bones = go.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                string sampled = null;
                int checkedBones = 0;
                float worstCm = 0f, worstDeg = 0f;
                string worst = "";
                foreach (var line in System.IO.File.ReadLines(file))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    var f = line.Split(',');
                    string key = f[0] + "@" + f[1];
                    if (key != sampled)
                    {
                        var clip = clips.FirstOrDefault(c => c.name == f[0]);
                        Assert.IsNotNull(clip, $"the controller has no {f[0]} clip");
                        clip.SampleAnimation(go, float.Parse(f[1], inv) * clip.length);
                        sampled = key;
                    }
                    Assert.IsTrue(bones.TryGetValue(f[2], out var bone), $"no bone {f[2]}");
                    var at = new Vector3(float.Parse(f[3], inv), float.Parse(f[4], inv), float.Parse(f[5], inv));
                    var turn = new Quaternion(float.Parse(f[6], inv), float.Parse(f[7], inv), float.Parse(f[8], inv), float.Parse(f[9], inv));
                    float cm = Vector3.Distance(at, go.transform.InverseTransformPoint(bone.position)) * 100f;
                    float deg = Quaternion.Angle(turn, Quaternion.Inverse(go.transform.rotation) * bone.rotation);
                    // (a thumb's last joints bend about one axis in a humanoid, a little off the one the takes use: its tip
                    // stays in place — the position is held — and turns a few degrees more about itself)
                    if (f[2].StartsWith("thumb.")) deg *= 20f / 32f;
                    if (cm > worstCm || deg > worstDeg) worst = $"{key} {f[2]}: {cm:0.0} cm, {deg:0} deg";
                    worstCm = Mathf.Max(worstCm, cm);
                    worstDeg = Mathf.Max(worstDeg, deg);
                    checkedBones++;
                }
                Assert.Greater(checkedBones, 500, "the reference is empty");
                Assert.Less(worstCm, 2.5f, $"a bone is not where the take puts it ({worst})");
                Assert.Less(worstDeg, 20f, $"a bone is twisted against the take ({worst})");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
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
            // a humanoid on the common skeleton, the takes shared by everybody, a layer each for the legs, the upper body, the
            // head and the right hand's grip
            Assert.IsNotNull(rig.Animator.avatar, "no Avatar");
            Assert.IsTrue(rig.Animator.avatar.isValid && rig.Animator.avatar.isHuman, "not a valid humanoid Avatar");
            var layered = (UnityEditor.Animations.AnimatorController)controller;
            Assert.AreEqual(HumanLayers.Count, layered.layers.Length);
            Assert.AreEqual("Legs", layered.layers[HumanLayers.Legs].name);
            Assert.AreEqual("UpperBody", layered.layers[HumanLayers.UpperBody].name);
            Assert.AreEqual("Head", layered.layers[HumanLayers.Head].name);
            Assert.AreEqual("RightHand", layered.layers[HumanLayers.RightHand].name);
            Assert.IsTrue(layered.layers[HumanLayers.UpperBody].iKPass, "IK for the hands");
            Assert.IsFalse(layered.layers[HumanLayers.Head].iKPass, "no look-at IK: on this Avatar it bowed every head (the look turns the head after the animation)");
            Assert.IsTrue(layered.layers[HumanLayers.Head].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head));
            Assert.IsFalse(layered.layers[HumanLayers.Head].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg));
            Assert.IsTrue(layered.layers[HumanLayers.Legs].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg));
            Assert.IsFalse(layered.layers[HumanLayers.Legs].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head));
            Assert.IsTrue(layered.layers[HumanLayers.RightHand].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers));
            Assert.IsTrue(controller.animationClips.Any(c => c.name == "HumanGrip"), "the grip of the right hand");
            Assert.IsTrue(controller.animationClips.Any(c => c.name == "HumanCup"), "the palm cupped under a compact thing");
            Assert.IsTrue(layered.parameters.Any(p => p.name == "Cup"), "fist … cupped palm");
            Assert.IsTrue(controller.animationClips.Where(c => c.name != "HumanGrip" && c.name != "HumanCup").All(c => UnityEditor.AssetDatabase.GetAssetPath(c).EndsWith("human_backend.fbx")),
                "one set of takes for everybody");
            Assert.IsNotNull(rig.Animator.GetComponent<HumanIK>(), "the IK relay beside the Animator");
            Assert.IsTrue(rig.HandR != null && rig.FingersR != null, "the right hand's bones (the held thing lies there)");

            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), "the human is decoration: no colliders");
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            // (EyeDetails — headphones, glasses, a belt — is worn by some and not by others)
            // (a part of the person's own outfit is "<part>.<slot>": the wardrobe swaps it by its slot)
            foreach (var part in new[] { "BodyClothes", "AccentHead", "AccentBand", "Sclera", "Outfit" })
                Assert.IsTrue(renderers.Any(r => WardrobeRules.PartOf(r.name) == part), $"no part {part} (PlayerLooks paints by part name)");
            var wardrobe = prefab.GetComponent<HumanWardrobe>();
            Assert.IsNotNull(wardrobe, "the wardrobe on the rig");
            foreach (var slot in new[] { "top", "bottom", "feet", "hair" })
                Assert.IsTrue(wardrobe.Own.Any(w => w.Slot == slot && w.Renderers.Length > 0 && !string.IsNullOrEmpty(w.Item)), $"the person's own {slot} is not in the wardrobe");
            // lit per pixel: every part has the person's baked maps — albedo, a normal map (not the eyes), occlusion and smoothness
            int triangles = 0;
            foreach (var r in renderers)
            {
                var m = r.sharedMaterial;
                Assert.IsNotNull(m, $"{r.name}: no material");
                triangles += r.sharedMesh.triangles.Length / 3;
                if (WardrobeRules.PartOf(r.name) == "Hair")
                {
                    // hair cards: the strand texture cut out by alpha, both sides
                    Assert.IsTrue(m.IsKeywordEnabled("_ALPHATEST_ON"), $"{r.name}: the strands are not cut out");
                    Assert.AreEqual((float)CullMode.Off, m.GetFloat("_Cull"), $"{r.name}: a card is seen from both sides");
                    Assert.IsNotNull(m.GetTexture("_BaseMap"), $"{r.name}: no strand texture");
                    continue;
                }
                Assert.IsNotNull(m.GetTexture("_BaseMap"), $"{r.name}: no baked albedo");
                Assert.IsNotNull(m.GetTexture("_MetallicGlossMap"), $"{r.name}: no baked mask (occlusion, smoothness)");
                if (r.name != "Sclera") Assert.IsNotNull(m.GetTexture("_BumpMap"), $"{r.name}: no baked normal map");
                Assert.IsTrue(r.sharedMesh.uv.Length == r.sharedMesh.vertexCount, $"{r.name}: no UVs");
                Assert.IsTrue(r.sharedMesh.tangents.Length == r.sharedMesh.vertexCount, $"{r.name}: no tangents (the normal map needs them)");
                Assert.LessOrEqual(r.quality == SkinQuality.Auto ? 4 : (int)r.quality, 4);
            }
            // (~21.5 thousand and the hair cards: the head 6500 with the eyes, nose and lips held, each hand 3000 — fingers were
            // octagons, a lip six segments —, a hairstyle's cards up to ~9 thousand)
            Assert.That(triangles, Is.InRange(9000, 34000), "the triangle budget of a player");
            // the four are different people: of different heights, and the head's mesh is not shared between departments
            var heights = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans.Select(h => h.GetComponentInChildren<Animator>(true).transform.localScale.y).ToArray();
            Assert.Greater(heights.Max() - heights.Min(), 0.05f, "the tallest and the shortest differ by a few centimetres at least");
            var heads = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans
                .Select(h => h.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "AccentHead").sharedMesh).ToArray();
            Assert.AreEqual(heads.Length, heads.Distinct().Count(), "each department has a face of its own");
            // the bones the takes and the head tilt need
            var bones = prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (var bone in new[] { "hips", "spine", "chest", "neck", "head", "upper_arm.L", "forearm.R", "hand.L", "index.1.L", "middle.3.R", "little.2.L", "thumb.1.R", "thigh.L", "shin.R", "foot.L", "toe.R" })
                Assert.Contains(bone, bones, $"no bone {bone}");
            var planted = prefab.GetComponent<HumanRig>();
            Assert.IsTrue(planted.Hips != null && planted.FootL != null && planted.FootR != null, "the bones that plant the feet");
            Assert.That(planted.AnkleHeight, Is.InRange(0.03f, 0.16f), "the ankle's height over the sole");
            Assert.IsTrue(planted.ToeL != null && planted.ToeR != null, "the balls of the feet: a heel up does not sink the toes");
            Assert.IsNotNull(planted.Chest, "the chest the hand carries a thing before");
            Assert.That(planted.ChestStanding.y, Is.InRange(1.05f, 1.45f), "the standing chest's height, in the model's own size");
            Assert.Less(Mathf.Abs(planted.ChestStanding.x), 0.03f);
            Assert.Greater(HumanSkeleton.Carry.y, planted.ChestStanding.y - 0.25f, "carried about the chest's height when standing");
            Assert.That(planted.BallHeight, Is.InRange(0.005f, 0.05f), "the ball's height over the sole");
            Assert.Less(planted.BallHeight, planted.AnkleHeight);
            var walking = planted.Animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            Assert.IsTrue(walking.parameters.Any(p => p.name == "Stride" && Mathf.Approximately(p.defaultFloat, 1f)), "the steps' rate, 1 until the rig sets it");

            // a real adult, standing on the floor, facing +Z
            var go = Object.Instantiate(prefab);
            try
            {
                // standing: the Animator itself plays the Idle state through the humanoid Avatar (the retargeting is what is
                // checked: a wrong T-pose in the Avatar's description twists the arms out or up)
                var animator = go.GetComponent<HumanRig>().Animator;
                animator.Rebind();
                animator.Update(0f);
                animator.Update(0.5f);
                Transform B(string n) => go.GetComponentsInChildren<Transform>().First(t => t.name == n);
                foreach (var side in new[] { "L", "R" })
                {
                    Assert.Less(B("hand." + side).position.y, B("upper_arm." + side).position.y - 0.35f, $"{side}: the arm hangs down in the idle");
                    Assert.Less(Mathf.Abs(B("hand." + side).position.x - B("hips").position.x), 0.5f, $"{side}: the hand by the thigh, not out to the side");
                }
                Assert.That(B("hips").position.y, Is.InRange(0.8f, 1.15f), "the hips at a standing person's height");
                Assert.Greater(B("head").position.y, B("chest").position.y + 0.2f, "the head over the chest");
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
