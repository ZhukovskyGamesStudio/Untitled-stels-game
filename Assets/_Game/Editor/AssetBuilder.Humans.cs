using System.Linq;
using Stealth.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The rigged humans shown instead of the player model (Tools/Blender/human.py → Art/Models/Humans): import settings
    /// (generic rig, looping takes), the Animator controller (move blend Idle/Walk/Run, crouch blend, lying when downed,
    /// the seat, the ladder, the terminal, work with the hands), each person's materials — URP Lit with the maps baked
    /// from the sculpt (albedo, a tangent-space normal map, occlusion and smoothness in one mask) — and the prefab with
    /// <see cref="HumanRig"/>.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string HumanFbx = ModelsDir + "/Humans/human_scientist.fbx";
        const string HumansDataDir = StylesDir + "/Humans";
        const string HumanControllerPath = HumansDataDir + "/HumanRig.controller";
        const string HumanPrefabPath = PrefabsDir + "/Player/HumanRig_Scientist.prefab";

        /// <summary>Builds the rigged human (null when the model has not been exported yet).</summary>
        static GameObject BuildHumanRig() => BuildHumanRig(HumanFbx, HumanPrefabPath, "HumanRig_Scientist");

        /// <summary>The players' rigged humans, one per department (human.py <c>-- … backend</c> etc.), in PlayerLooks.Department order.</summary>
        static GameObject[] BuildDepartmentHumans()
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (var dept in new[] { "backend", "qa", "sysadmin", "designer" })
            {
                string name = "HumanRig_" + char.ToUpperInvariant(dept[0]) + dept.Substring(1);
                var rig = BuildHumanRig($"{ModelsDir}/Humans/human_{dept}.fbx", $"{PrefabsDir}/Player/{name}.prefab", name);
                if (rig == null) return new GameObject[0]; // all four or none: the player models stay
                list.Add(rig);
            }
            return list.ToArray();
        }

        static GameObject BuildHumanRig(string fbx, string prefabPath, string rootName)
        {
            string HumanFbx = fbx, HumanPrefabPath = prefabPath;
            if (!(AssetImporter.GetAtPath(HumanFbx) is ModelImporter imp)) { Debug.LogWarning($"[Builder] {HumanFbx} missing: run Tools/Blender/human.py"); return null; }
            ConfigureHumanImport(imp);
            EnsureFolder(HumansDataDir);
            var clips = AssetDatabase.LoadAllAssetsAtPath(HumanFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n);
            var controller = BuildHumanController(Clip("Idle"), Clip("Walk"), Clip("Run"), Clip("CrouchIdle"), Clip("CrouchWalk"), Clip("Down"), Clip("Sit"),
                Clip("Climb"), Clip("Terminal"), Clip("Work"));

            // the person's own maps (human_<who>_skin_* / _outfit_*): the skin and the eyes on one atlas, everything else on the other
            string who = System.IO.Path.GetFileNameWithoutExtension(fbx).Substring("human_".Length);
            ConfigureHumanTextures();
            var skin = HumanMaps($"Human_{who}_Skin", who, "skin");
            var outfit = HumanMaps($"Human_{who}_Outfit", who, "outfit");
            var eyes = HumanMaps($"Human_{who}_Eyes", who, "skin", normal: false);
            var mats = new System.Collections.Generic.Dictionary<string, Material>
            {
                ["BodyClothes"] = outfit, ["AccentHead"] = skin, ["AccentBand"] = outfit, ["EyeDetails"] = outfit, ["Sclera"] = eyes, ["Outfit"] = outfit,
            };
            return Prefab(HumanPrefabPath, () =>
            {
                // the model under a root: the import turns Blender's front (-Y) to -Z, the root faces +Z like the player
                var go = new GameObject(rootName);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HumanFbx));
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Model";
                model.transform.SetParent(go.transform, false);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                // every figure is sculpted on the same skeleton (they share the takes): a person's height is the model's scale
                model.transform.localScale = Vector3.one * HumanHeight(who);
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.sharedMaterial = mats.TryGetValue(r.gameObject.name, out var m) ? m : PartMaterial(mats, r.gameObject.name);
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.updateWhenOffscreen = false;
                }
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                if (!model.TryGetComponent<Animator>(out var animator)) animator = model.AddComponent<Animator>(); // (?? misses Unity's fake null)
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                var rig = go.AddComponent<HumanRig>();
                rig.Animator = animator;
                rig.HeadBone = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "head");
                return go;
            });
        }

        /// <summary>How tall a person is, as a share of the sculpt's 1.80 m (the lanky developer, the short tester, the stocky sysadmin, the designer).</summary>
        static float HumanHeight(string who) => who switch { "backend" => 1.015f, "qa" => 0.93f, "sysadmin" => 0.985f, "designer" => 0.955f, _ => 1f };

        /// <summary>A part of a human by its name's role (AccentHead the skin, Sclera the eyes, everything else the outfit's atlas).</summary>
        static Material PartMaterial(System.Collections.Generic.Dictionary<string, Material> mats, string part) =>
            part.StartsWith("AccentHead") ? mats["AccentHead"] : part.StartsWith("Sclera") ? mats["Sclera"] : mats["Outfit"];

        const string HumanTextures = Root + "/Art/Textures/Humans";

        /// <summary>The baked maps' import: normal maps as normal maps, masks linear, everything trilinear with mips.</summary>
        static void ConfigureHumanTextures()
        {
            if (!AssetDatabase.IsValidFolder(HumanTextures)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D human_", new[] { HumanTextures }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                var type = file.EndsWith("_normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool srgb = file.EndsWith("_base");
                if (imp.textureType == type && imp.sRGBTexture == srgb && imp.filterMode == FilterMode.Trilinear && imp.mipmapEnabled
                    && imp.wrapMode == TextureWrapMode.Clamp && imp.anisoLevel == 4) continue;
                imp.textureType = type;
                imp.sRGBTexture = srgb;
                imp.filterMode = FilterMode.Trilinear;
                imp.mipmapEnabled = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
            }
        }

        /// <summary>
        /// A person's material: white (PlayerLooks tints the part) times the baked albedo, lit by the baked normal map, with
        /// the baked occlusion and per-texel smoothness (skin, cloth, leather and glass differ in the mask, not in materials).
        /// </summary>
        static Material HumanMaps(string name, string who, string atlas, bool normal = true)
        {
            var m = LitAsset($"{HumansDataDir}/{name}.mat");
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", Tex($"{HumanTextures}/human_{who}_{atlas}_base.png"));
            var bump = normal ? Tex($"{HumanTextures}/human_{who}_{atlas}_normal.png") : null;
            m.SetTexture("_BumpMap", bump);
            m.SetFloat("_BumpScale", 1f);
            if (bump != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            var mask = Tex($"{HumanTextures}/human_{who}_{atlas}_mask.png");
            m.SetTexture("_MetallicGlossMap", mask);
            m.SetTexture("_OcclusionMap", mask);
            m.SetFloat("_OcclusionStrength", 0.8f);
            m.SetFloat("_Smoothness", 1f);                    // (the mask's alpha is the smoothness itself)
            m.SetFloat("_Metallic", 0f);
            if (mask != null) { m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP"); }
            else { m.DisableKeyword("_METALLICSPECGLOSSMAP"); m.DisableKeyword("_OCCLUSIONMAP"); m.SetFloat("_Smoothness", 0.3f); }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void ConfigureHumanImport(ModelImporter imp)
        {
            bool dirty = imp.animationType != ModelImporterAnimationType.Generic || imp.materialImportMode != ModelImporterMaterialImportMode.None ||
                         !imp.bakeAxisConversion || !imp.importAnimation || imp.clipAnimations.Length == 0 || imp.clipAnimations.Any(c => !c.loopTime)
                         || imp.clipAnimations.Length != imp.defaultClipAnimations.Length // a take was added in Blender (Sit, Climb…)
                         || imp.importNormals != ModelImporterNormals.Import || imp.importTangents != ModelImporterTangents.CalculateMikk
                         || imp.maxBonesPerVertex != 4;
            if (!dirty) return;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.bakeAxisConversion = true;
            imp.importAnimation = true;
            // the normal maps were baked against these normals and MikkTSpace tangents (Blender's bake uses the same)
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.maxBonesPerVertex = 4;
            imp.skinWeights = ModelImporterSkinWeights.Standard;
            imp.SaveAndReimport();
            // every take loops and is called by its action name (the exporter writes "Rig|Walk")
            imp.clipAnimations = imp.defaultClipAnimations.Select(c =>
            {
                c.name = c.takeName.Contains('|') ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                c.loopTime = true;
                return c;
            }).ToArray();
            imp.SaveAndReimport();
        }

        /// <summary>
        /// Speed (m/s) blends Idle → Walk → Run; Crouch blends crouched idle → crouch walk; Down lies on the floor; Sit sits in
        /// the car; Pose (<see cref="BodyPose"/>) plays Climb (by ClimbSpeed: backwards on the way down, still when hanging),
        /// Terminal or Work.
        /// </summary>
        static AnimatorController BuildHumanController(AnimationClip idle, AnimationClip walk, AnimationClip run,
                                                       AnimationClip crouchIdle, AnimationClip crouchWalk, AnimationClip down, AnimationClip sit,
                                                       AnimationClip climb, AnimationClip terminal, AnimationClip work)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanControllerPath);
            // the prefab references it: keep its GUID — unless it is older than the takes it should play (no Sit, no Pose yet)
            // or its clips are gone (the takes were exported anew from another model)
            if (existing != null && !s_Force && (sit == null || existing.parameters.Any(p => p.name == "Sit"))
                && (climb == null || existing.parameters.Any(p => p.name == "Pose")) && existing.animationClips.All(c => c != null)) return existing;
            AssetDatabase.DeleteAsset(HumanControllerPath);      // generated, never tuned by hand
            var c = AnimatorController.CreateAnimatorControllerAtPath(HumanControllerPath);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            c.AddParameter("Down", AnimatorControllerParameterType.Bool);
            c.AddParameter("Sit", AnimatorControllerParameterType.Bool);
            c.AddParameter("Pose", AnimatorControllerParameterType.Int);
            c.AddParameter("ClimbSpeed", AnimatorControllerParameterType.Float);
            var sm = c.layers[0].stateMachine;

            var move = c.CreateBlendTreeInController("Move", out var moveTree, 0);
            moveTree.blendParameter = "Speed";
            moveTree.useAutomaticThresholds = false;
            moveTree.AddChild(idle, 0f);
            moveTree.AddChild(walk, 1.5f);
            moveTree.AddChild(run, 4.5f);
            var crouch = c.CreateBlendTreeInController("Crouch", out var crouchTree, 0);
            crouchTree.blendParameter = "Speed";
            crouchTree.useAutomaticThresholds = false;
            crouchTree.AddChild(crouchIdle, 0f);
            crouchTree.AddChild(crouchWalk, 1.3f);
            var lying = sm.AddState("Down");
            lying.motion = down;
            sm.defaultState = move;

            void To(AnimatorState from, AnimatorState to, string param, bool value, float duration)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = false;
                t.duration = duration;
                t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
            }
            To(move, crouch, "Crouch", true, 0.2f);
            To(crouch, move, "Crouch", false, 0.2f);
            var fall = sm.AddAnyStateTransition(lying);
            fall.hasExitTime = false;
            fall.duration = 0.25f;
            fall.canTransitionToSelf = false;
            fall.AddCondition(AnimatorConditionMode.If, 0f, "Down");
            To(lying, move, "Down", false, 0.4f);
            if (sit != null)
            {
                var seated = sm.AddState("Sit");
                seated.motion = sit;
                var sitDown = sm.AddAnyStateTransition(seated);
                sitDown.hasExitTime = false;
                sitDown.duration = 0.3f;
                sitDown.canTransitionToSelf = false;
                sitDown.AddCondition(AnimatorConditionMode.If, 0f, "Sit");
                sitDown.AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");
                To(seated, move, "Sit", false, 0.3f);
            }
            // busy with something (BodyPose): from anywhere but lying and the seat, back to moving when it is over
            void Busy(string name, AnimationClip clip, BodyPose pose, float blend, bool byClimbSpeed = false)
            {
                if (clip == null) return;
                var state = sm.AddState(name);
                state.motion = clip;
                if (byClimbSpeed)
                {
                    state.speedParameterActive = true;
                    state.speedParameter = "ClimbSpeed";
                }
                var into = sm.AddAnyStateTransition(state);
                into.hasExitTime = false;
                into.duration = blend;
                into.canTransitionToSelf = false;
                into.AddCondition(AnimatorConditionMode.Equals, (int)pose, "Pose");
                into.AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");
                into.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sit");
                var back = state.AddTransition(move);
                back.hasExitTime = false;
                back.duration = blend;
                back.AddCondition(AnimatorConditionMode.NotEqual, (int)pose, "Pose");
            }
            Busy("Climb", climb, BodyPose.Ladder, 0.2f, byClimbSpeed: true);
            Busy("Terminal", terminal, BodyPose.Terminal, 0.3f);
            Busy("Work", work, BodyPose.Work, 0.22f);
            EditorUtility.SetDirty(c);
            return c;
        }
    }
}
