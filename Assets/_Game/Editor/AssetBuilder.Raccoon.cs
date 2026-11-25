using System.Collections.Generic;
using System.Linq;
using Stealth.Game;
using Stealth.Items;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The storage mule as a raccoon with a backpack (Tools/Blender/raccoon.py): the rigged raccoon and its takes
    /// (raccoon.fbx, Generic), its baked fur (Art/Textures/Raccoon), its animator (Data/Raccoon/Raccoon.controller), and the
    /// backpacks (raccoon_pack.fbx) hung on its bone "pack": one per level of the backpack upgrade, their charms that swing
    /// (<see cref="Dangle"/>), and each level's load — the stash wrapped round the pack (<see cref="RaccoonPack"/>, <see cref="PackWrap"/>).
    /// </summary>
    public static partial class AssetBuilder
    {
        const string RaccoonModelsDir = ModelsDir + "/Raccoon";
        const string RaccoonFbx = RaccoonModelsDir + "/raccoon.fbx";
        const string RaccoonPackFbx = RaccoonModelsDir + "/raccoon_pack.fbx";
        const string RaccoonTextures = Root + "/Art/Textures/Raccoon";
        const string RaccoonDataDir = Root + "/Data/Raccoon";
        const string RaccoonControllerPath = RaccoonDataDir + "/Raccoon.controller";

        /// <summary>Stamped on the raccoon's import: change it with the import settings below.</summary>
        const string RaccoonImportVersion = "raccoon: generic, takes by action name, Idle/Walk/Run/Climb/Curl/Sit loop";

        static readonly string[] RaccoonLoops = { "Idle", "Walk", "Run", "Climb", "Curl", "Sit" };
        static readonly string[] RaccoonTakes = { "Idle", "Walk", "Run", "Climb", "VentUp", "VentDown", "Pet", "Curl", "Sit" };

        /// <summary>The raccoon's file: Generic rig with its takes; the pack's: a plain model; the maps: normal map, linear mask, the plaid tiling.</summary>
        static void ConfigureRaccoonImport()
        {
            if (AssetImporter.GetAtPath(RaccoonFbx) is ModelImporter imp)
            {
                bool dirty = imp.animationType != ModelImporterAnimationType.Generic || !imp.importAnimation || imp.userData != RaccoonImportVersion
                             || imp.materialImportMode != ModelImporterMaterialImportMode.None || !imp.bakeAxisConversion
                             || imp.importNormals != ModelImporterNormals.Import || imp.clipAnimations.Length != imp.defaultClipAnimations.Length
                             // a take re-timed in Blender: the clips keep their old frame ranges until set anew
                             || imp.clipAnimations.Any(c => imp.defaultClipAnimations.FirstOrDefault(d => d.takeName == c.takeName) is not { } d
                                                            || !Mathf.Approximately(d.lastFrame, c.lastFrame) || !Mathf.Approximately(d.firstFrame, c.firstFrame));
                if (dirty)
                {
                    imp.animationType = ModelImporterAnimationType.Generic;
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    imp.importAnimation = true;
                    imp.materialImportMode = ModelImporterMaterialImportMode.None;
                    imp.bakeAxisConversion = true;
                    imp.importNormals = ModelImporterNormals.Import;              // (the normal map was baked against these)
                    imp.importTangents = ModelImporterTangents.CalculateMikk;
                    imp.skinWeights = ModelImporterSkinWeights.Standard;
                    imp.userData = RaccoonImportVersion;
                    imp.SaveAndReimport();
                    // every take by its action name (the exporter writes "Rig|Walk"); the gaits and the climb loop
                    imp.clipAnimations = imp.defaultClipAnimations.Select(c =>
                    {
                        c.name = c.takeName.Contains('|') ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                        c.loopTime = RaccoonLoops.Contains(c.name);
                        return c;
                    }).ToArray();
                    imp.SaveAndReimport();
                }
            }
            if (AssetImporter.GetAtPath(RaccoonPackFbx) is ModelImporter pack
                && (pack.materialImportMode != ModelImporterMaterialImportMode.None || pack.importAnimation || pack.animationType != ModelImporterAnimationType.None))
            {
                pack.materialImportMode = ModelImporterMaterialImportMode.None;
                pack.importAnimation = false;
                pack.animationType = ModelImporterAnimationType.None;
                pack.SaveAndReimport();
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D raccoon_", new[] { RaccoonTextures }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var t = (TextureImporter)AssetImporter.GetAtPath(path);
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                var type = file.EndsWith("_normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool srgb = file.EndsWith("_base") || file.EndsWith("_plaid");
                var wrap = file.EndsWith("_plaid") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (t.textureType == type && t.sRGBTexture == srgb && t.wrapMode == wrap && t.mipmapEnabled && t.filterMode == FilterMode.Trilinear) continue;
                t.textureType = type;
                t.sRGBTexture = srgb;
                t.wrapMode = wrap;
                t.mipmapEnabled = true;
                t.filterMode = FilterMode.Trilinear;
                t.anisoLevel = 4;
                t.SaveAndReimport();
            }
        }

        /// <summary>The fur: the baked albedo, normal map, occlusion and per-texel smoothness (URP Lit, like the people's).</summary>
        static Material RaccoonFur()
        {
            var m = LitAsset($"{RaccoonDataDir}/RaccoonFur.mat");
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", Tex($"{RaccoonTextures}/raccoon_base.png"));
            var bump = Tex($"{RaccoonTextures}/raccoon_normal.png");
            m.SetTexture("_BumpMap", bump);
            if (bump != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            var mask = Tex($"{RaccoonTextures}/raccoon_mask.png");
            m.SetTexture("_MetallicGlossMap", mask);
            m.SetTexture("_OcclusionMap", mask);
            m.SetFloat("_OcclusionStrength", 0.8f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Metallic", 0f);
            if (mask != null) { m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP"); }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The backpack's materials by its parts' names (&lt;Material&gt;_&lt;Group&gt;).</summary>
        static Dictionary<string, Material> RaccoonPackMaterials()
        {
            Material Make(string name, Color colour, float smooth, float metal = 0f, string texture = null, float tiling = 1f)
            {
                var m = LitAsset($"{RaccoonDataDir}/RaccoonPack{name}.mat");
                m.SetColor("_BaseColor", colour);
                var tex = texture != null ? Tex($"{RaccoonTextures}/{texture}") : null;
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", Vector2.one * tiling);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                EditorUtility.SetDirty(m);
                return m;
            }
            return new Dictionary<string, Material>
            {
                ["Canvas"] = Make("Canvas", Color.white, 0.18f, 0f, "raccoon_plaid.png", 8f),       // (UVs in metres: a check every 12.5 cm)
                ["Strap"] = Make("Strap", new Color(0.30f, 0.33f, 0.19f), 0.15f),
                ["Metal"] = Make("Metal", new Color(0.66f, 0.66f, 0.68f), 0.65f, 0.85f),
                ["Red"] = Make("Red", new Color(0.80f, 0.14f, 0.09f), 0.55f, 0.2f),
                ["Blue"] = Make("Blue", new Color(0.14f, 0.40f, 0.85f), 0.55f, 0.2f),
                ["Mug"] = Make("Mug", new Color(0.93f, 0.93f, 0.91f), 0.75f),
                ["Brass"] = Make("Brass", new Color(0.78f, 0.62f, 0.28f), 0.6f, 0.85f),
                ["Radio"] = Make("Radio", new Color(0.35f, 0.44f, 0.22f), 0.35f),
                ["Roll"] = Make("Roll", new Color(0.58f, 0.18f, 0.13f), 0.12f),
                ["Sack"] = Make("Sack", new Color(0.66f, 0.57f, 0.42f), 0.1f),
                ["Rope"] = Make("Rope", new Color(0.72f, 0.6f, 0.38f), 0.12f),
            };
        }

        /// <summary>
        /// The raccoon's one controller: Move (Idle → Walk → Run by Speed, quickened by Stride past the run), Climb (by
        /// ClimbSpeed: backwards on the way down), VentUp, VentDown, Pet, Curl (asleep in the trunk), Sit (waiting where it was called).
        /// No transitions: StorageBotAnimator cross-fades.
        /// </summary>
        static AnimatorController RaccoonController()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(RaccoonFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n);
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(RaccoonControllerPath);
            if (existing != null && !s_Force && existing.animationClips.All(c => c != null) && RaccoonTakes.All(t => existing.animationClips.Any(c => c.name == t)))
                return existing;
            EnsureFolder(RaccoonDataDir);
            AssetDatabase.DeleteAsset(RaccoonControllerPath);           // generated, never tuned by hand
            var c = AnimatorController.CreateAnimatorControllerAtPath(RaccoonControllerPath);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter(new AnimatorControllerParameter { name = "Stride", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            c.AddParameter(new AnimatorControllerParameter { name = "ClimbSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = c.layers[0].stateMachine;
            var move = c.CreateBlendTreeInController("Move", out var tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle"), 0f);
            tree.AddChild(Clip("Walk"), StorageBotRules.WalkTakeSpeed);
            tree.AddChild(Clip("Run"), StorageBotRules.RunTakeSpeed);
            move.speedParameterActive = true;
            move.speedParameter = "Stride";
            var climb = sm.AddState("Climb");
            climb.motion = Clip("Climb");
            climb.speedParameterActive = true;
            climb.speedParameter = "ClimbSpeed";
            foreach (var n in new[] { "VentUp", "VentDown", "Pet", "Curl", "Sit" }) sm.AddState(n).motion = Clip(n);
            sm.defaultState = move;
            EditorUtility.SetDirty(c);
            return c;
        }

        /// <summary>
        /// The raccoon with its backpacks, at rest, facing +Z, its paws on y = 0: the animator on the model, the packs on the
        /// bone "pack", each level hidden with its charms on <see cref="Dangle"/> clips and its load (the stash wraps round it),
        /// a hidden piece per stash cell, and <see cref="RaccoonPack"/> on the model wiring them.
        /// </summary>
        static GameObject RaccoonModel(out Animator animator, out RaccoonPack pack)
        {
            ConfigureRaccoonImport();
            var model = Model("raccoon");
            var fur = RaccoonFur();
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterial = fur;
                r.updateWhenOffscreen = false;
            }
            if (!model.TryGetComponent(out animator)) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = RaccoonController();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            // the file comes in posed (a frame of some take): the first frame of Idle stands it as at rest — its back and the
            // bone "pack" exactly so (only the head turns a little) — before the pack is hung on that bone
            var idle = AssetDatabase.LoadAllAssetsAtPath(RaccoonFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle");
            if (idle != null) idle.SampleAnimation(model, 0f);

            var bone = Find(model, "pack");
            var backpack = Model("raccoon_pack");
            backpack.name = "Backpack";
            backpack.transform.SetParent(model.transform, false);       // (the pack was modelled in the raccoon's rest space)
            backpack.transform.SetParent(bone != null ? bone : model.transform, true);
            var mats = RaccoonPackMaterials();
            var parts = backpack.GetComponentsInChildren<MeshRenderer>(true).ToList();
            foreach (var r in parts)
            {
                var kind = r.name.Split('_')[0];
                if (mats.TryGetValue(kind, out var m)) r.sharedMaterial = m;
                if (kind is "Metal" or "Brass" or "Red" or "Blue" or "Mug") r.shadowCastingMode = ShadowCastingMode.Off;   // (charms: no speckled shadows)
            }
            foreach (var col in backpack.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

            // a holder per level (parts named <Material>_L<level>[Group]), hidden: RaccoonPack shows the upgrade's one
            int levelCount = Stealth.Vehicles.CarUpgrades.MaxLevelled + 1;
            var levels = new GameObject[levelCount];
            var loads = new Transform[levelCount];
            var boxes = new Vector3[levelCount];
            for (int lv = 0; lv < levelCount; lv++)
            {
                var holder = new GameObject($"Level{lv}").transform;
                holder.SetParent(backpack.transform, false);
                var mine = parts.Where(r => r.name.Contains('_') && r.name.Substring(r.name.IndexOf('_') + 1).StartsWith($"L{lv}")).ToList();
                foreach (var r in mine) r.transform.SetParent(holder, true);
                foreach (var g in new[] { "DangleA", "DangleB", "DangleC" })
                {
                    var group = mine.Where(r => r.name.EndsWith($"_L{lv}{g}")).ToArray();
                    if (group.Length == 0) continue;
                    var clip = group.FirstOrDefault(r => r.name.StartsWith("Red") || r.name.StartsWith("Blue") || r.name.StartsWith("Rope") || r.name.StartsWith("Brass")) ?? group[0];
                    var all = group[0].bounds;
                    foreach (var r in group) all.Encapsulate(r.bounds);
                    var top = new Vector3(clip.bounds.center.x, all.max.y - 0.006f, clip.bounds.center.z);
                    var pivot = new GameObject(g).transform;
                    pivot.SetParent(holder, false);
                    pivot.position = top;
                    foreach (var r in group) r.transform.SetParent(pivot, true);
                    var dangle = pivot.gameObject.AddComponent<Dangle>();
                    dangle.Length = Mathf.Max(0.03f, Vector3.Distance(top, all.center));
                    dangle.RestDown = holder.InverseTransformDirection(Vector3.down);
                }
                // the load: the middle of the level's main body's top (Sack_L0 / Canvas_L<n>), +Z to the head, +Y up; the box
                // the stash wraps round in the load's space (PackWrap.Box: width, height, length)
                var main = mine.FirstOrDefault(r => r.name == $"Sack_L{lv}") ?? mine.FirstOrDefault(r => r.name == $"Canvas_L{lv}");
                var b = main != null ? main.bounds : new Bounds(bone != null ? bone.position + Vector3.up * 0.12f : Vector3.up * 0.6f, new Vector3(0.3f, 0.25f, 0.3f));
                var load = new GameObject("Load").transform;
                load.SetParent(holder, false);
                load.SetPositionAndRotation(new Vector3(b.center.x, b.max.y, b.center.z), Quaternion.LookRotation(Vector3.back)); // (the file's raccoon faces -Z)
                loads[lv] = load;
                boxes[lv] = new Vector3(b.size.x, b.size.y, b.size.z);
                holder.gameObject.SetActive(false);
                levels[lv] = holder.gameObject;
            }
            var pieces = new List<MeshFilter>();
            for (int i = 0; i < TeamStash.MaxWidth * TeamStash.MaxHeight; i++)
            {
                var piece = new GameObject($"Piece{i}");
                piece.transform.SetParent(loads[0], false);
                pieces.Add(piece.AddComponent<MeshFilter>());
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.enabled = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            // a rigged file faces -Z (like the people's): turned round to face +Z, the way the agent walks
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            pack = model.AddComponent<RaccoonPack>();
            pack.Levels = levels;
            pack.Loads = loads;
            pack.Boxes = boxes;
            pack.Pieces = pieces.ToArray();
            pack.Visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>(DataDir + "/ItemVisuals.asset");
            return model;
        }
    }
}
