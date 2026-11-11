using System.Linq;
using Stealth.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The rigged human a style can show instead of the player model (Tools/Blender/human.py → Art/Models/Humans):
    /// import settings (generic rig, looping takes), the Animator controller (move blend Idle/Walk/Run, crouch blend,
    /// lying when downed), its materials and the prefab with <see cref="HumanRig"/>.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string HumanFbx = ModelsDir + "/Humans/human_scientist.fbx";
        const string HumansDataDir = StylesDir + "/Humans";
        const string HumanControllerPath = HumansDataDir + "/HumanRig.controller";
        const string HumanPrefabPath = PrefabsDir + "/Player/HumanRig_Scientist.prefab";

        static GameObject s_HumanRig;

        /// <summary>Builds the rigged human (null when the model has not been exported yet).</summary>
        static GameObject BuildHumanRig()
        {
            if (!(AssetImporter.GetAtPath(HumanFbx) is ModelImporter imp)) { Debug.LogWarning($"[Builder] {HumanFbx} missing: run Tools/Blender/human.py"); return null; }
            ConfigureHumanImport(imp);
            EnsureFolder(HumansDataDir);
            var clips = AssetDatabase.LoadAllAssetsAtPath(HumanFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n);
            var controller = BuildHumanController(Clip("Idle"), Clip("Walk"), Clip("Run"), Clip("CrouchIdle"), Clip("CrouchWalk"), Clip("Down"));

            var mats = new System.Collections.Generic.Dictionary<string, Material>
            {
                ["BodyClothes"] = HumanMat("Human_Clothes", Color.white, 0.18f),
                ["Coat"] = HumanCoat(),
                ["AccentHead"] = HumanSkin(),
                ["AccentBand"] = HumanMat("Human_Band", Color.white, 0.3f),
                ["EyeDetails"] = HumanMat("Human_Dark", Color.white, 0.3f),
                ["Sclera"] = HumanMat("Human_Sclera", new Color(0.78f, 0.76f, 0.7f), 0.7f),
            };
            return Prefab(HumanPrefabPath, () =>
            {
                // the model under a root: the import turns Blender's front (-Y) to -Z, the root faces +Z like the player
                var go = new GameObject("HumanRig_Scientist");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HumanFbx));
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Model";
                model.transform.SetParent(go.transform, false);
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.sharedMaterial = mats.TryGetValue(r.gameObject.name, out var m) ? m : mats["BodyClothes"];
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

        static void ConfigureHumanImport(ModelImporter imp)
        {
            bool dirty = imp.animationType != ModelImporterAnimationType.Generic || imp.materialImportMode != ModelImporterMaterialImportMode.None ||
                         !imp.bakeAxisConversion || !imp.importAnimation || imp.clipAnimations.Length == 0 || imp.clipAnimations.Any(c => !c.loopTime);
            if (!dirty) return;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.bakeAxisConversion = true;
            imp.importAnimation = true;
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

        /// <summary>Skin: the face map (stubble, lips, cheeks, sockets) multiplied by the player's skin tone (PlayerLooks).</summary>
        static Material HumanSkin()
        {
            var m = HumanMat("Human_Skin", Color.white, 0.38f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Humans/skin_face.png"));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material HumanMat(string name, Color color, float smoothness)
        {
            var m = LitAsset($"{HumansDataDir}/{name}.mat");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The lab coat: off-white, yellowed and grimy (the composite surface of the derelict family).</summary>
        static Material HumanCoat()
        {
            var m = LitAsset($"{HumansDataDir}/Human_Coat.mat");
            SetPbr(m, $"{DerelictTextures}/dr_composite", new Color(0.92f, 0.91f, 0.86f), 0.5f, 3f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Speed (m/s) blends Idle → Walk → Run; Crouch blends crouched idle → crouch walk; Down lies on the floor.</summary>
        static AnimatorController BuildHumanController(AnimationClip idle, AnimationClip walk, AnimationClip run,
                                                       AnimationClip crouchIdle, AnimationClip crouchWalk, AnimationClip down)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanControllerPath);
            if (existing != null && !s_Force) return existing; // the prefab references it: keep its GUID
            AssetDatabase.DeleteAsset(HumanControllerPath);      // generated, never tuned by hand
            var c = AnimatorController.CreateAnimatorControllerAtPath(HumanControllerPath);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            c.AddParameter("Down", AnimatorControllerParameterType.Bool);
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
            EditorUtility.SetDirty(c);
            return c;
        }
    }
}
