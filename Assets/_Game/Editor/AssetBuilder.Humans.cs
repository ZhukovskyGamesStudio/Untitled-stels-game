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
    /// (a humanoid Avatar described from the common skeleton in a T-pose — <see cref="HumanSkeleton"/> —, looping in-place
    /// takes), one Animator controller for everybody playing one shared set of humanoid takes in layers with avatar masks
    /// (the legs, the upper body with the hands' IK, the head and face, the right hand's grip), each person's materials — URP Lit with the maps baked
    /// from the sculpt (albedo, a tangent-space normal map, occlusion and smoothness in one mask) — and the prefab with
    /// <see cref="HumanRig"/>.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string HumanFbx = ModelsDir + "/Humans/human_scientist.fbx";
        const string HumansDataDir = StylesDir + "/Humans";
        const string HumanControllerPath = HumansDataDir + "/HumanRig.controller";
        const string HumanPrefabPath = PrefabsDir + "/Player/HumanRig_Scientist.prefab";
        /// <summary>The file whose takes every person plays (humanoid clips retarget onto every Avatar).</summary>
        const string SharedTakes = ModelsDir + "/Humans/human_backend.fbx";
        const string HumanGripPath = HumansDataDir + "/HumanGrip.anim", HumanCupPath = HumansDataDir + "/HumanCup.anim";

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
            // one set of humanoid takes for everybody (retargeted onto each person's Avatar): the shared file's
            if (AssetImporter.GetAtPath(SharedTakes) is ModelImporter shared && shared != imp) ConfigureHumanImport(shared);
            var clips = AssetDatabase.LoadAllAssetsAtPath(AssetImporter.GetAtPath(SharedTakes) != null ? SharedTakes : HumanFbx)
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
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
                    // ("<part>.<slot>": a renderer of the person's own outfit — the part says the material)
                    string part = WardrobeRules.PartOf(r.gameObject.name);
                    r.sharedMaterial = part == "Hair" ? HairCardMaterial($"Human_{who}_Hair", HairColourOf(who)) : mats.TryGetValue(part, out var m) ? m : PartMaterial(mats, part);
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
                var bones = model.GetComponentsInChildren<Transform>(true);
                rig.HeadBone = bones.FirstOrDefault(t => t.name == "head");
                rig.Hips = bones.FirstOrDefault(t => t.name == "hips");
                rig.FootL = bones.FirstOrDefault(t => t.name == "foot.L");
                rig.FootR = bones.FirstOrDefault(t => t.name == "foot.R");
                rig.ToeL = bones.FirstOrDefault(t => t.name == "toe.L");
                rig.ToeR = bones.FirstOrDefault(t => t.name == "toe.R");
                rig.HandR = bones.FirstOrDefault(t => t.name == "hand.R");
                rig.FingersR = bones.FirstOrDefault(t => t.name == "middle.1.R");
                rig.Chest = bones.FirstOrDefault(t => t.name == "chest");
                // the IK callbacks come to the Animator's own object: a relay hands them to the rig
                model.AddComponent<HumanIK>().Rig = rig;
                // the wardrobe: the person's own outfit, slot by slot (its renderers are "<part>.<slot>"; the item names from human.py's sidecar)
                var wardrobe = go.AddComponent<HumanWardrobe>();
                wardrobe.Who = who;
                wardrobe.Model = model.transform;
                var ownItems = SidecarOf(fbx).Items;
                wardrobe.Own = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Select(r => (slot: WardrobeRules.SlotOfPart(r.gameObject.name), r)).Where(x => x.slot != null)
                    .GroupBy(x => x.slot)
                    .Select(g => new HumanWardrobe.Worn { Slot = g.Key, Item = ownItems.TryGetValue(g.Key, out var it) ? it : null, Renderers = g.Select(x => (Renderer)x.r).ToArray() })
                    .ToArray();
                // the ankle over the sole: the Idle take stands on the floor (HumanRigTests), its lower foot bone's height
                var idle = controller.animationClips.FirstOrDefault(c => c.name == "Idle");
                if (idle != null && rig.FootL != null && rig.FootR != null)
                {
                    idle.SampleAnimation(model, 0f);
                    rig.AnkleHeight = Mathf.Min(rig.FootL.position.y, rig.FootR.position.y) - go.transform.position.y;
                    if (rig.ToeL != null && rig.ToeR != null)
                        rig.BallHeight = Mathf.Min(rig.ToeL.position.y, rig.ToeR.position.y) - go.transform.position.y;
                    // the chest when standing, in the model's own size: what the hand's carrying point is given for
                    if (rig.Chest != null)
                        rig.ChestStanding = go.transform.InverseTransformPoint(rig.Chest.position) / Mathf.Max(1e-4f, model.transform.lossyScale.y);
                }
                AddOwnView(rig, who);
                return go;
            });
        }

        /// <summary>What human.py writes beside a person's or an item's file (human_&lt;who&gt;.json, human_&lt;who&gt;__&lt;item&gt;.json).</summary>
        sealed class HumanSidecar
        {
            public string Slot, Item;
            public System.Collections.Generic.Dictionary<string, string> Items = new();
            public Color HairColour = new(0.1f, 0.08f, 0.06f);
        }

        /// <summary>The sidecar of a human's file (json: "slot", "item", "items" {slot: item}, "hair_colour" [r, g, b] linear); empty when missing.</summary>
        static HumanSidecar SidecarOf(string fbx)
        {
            var car = new HumanSidecar();
            string path = System.IO.Path.ChangeExtension(fbx, ".json");
            if (!System.IO.File.Exists(path)) return car;
            string json = System.IO.File.ReadAllText(path);
            const System.Text.RegularExpressions.RegexOptions S = System.Text.RegularExpressions.RegexOptions.Singleline;
            string Str(string key)
            {
                var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
            car.Slot = Str("slot");
            car.Item = Str("item");
            var items = System.Text.RegularExpressions.Regex.Match(json, "\"items\"\\s*:\\s*\\{([^}]*)\\}", S);
            if (items.Success)
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(items.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\""))
                    car.Items[m.Groups[1].Value] = m.Groups[2].Value;
            var hair = System.Text.RegularExpressions.Regex.Match(json, "\"hair_colour\"\\s*:\\s*\\[([^\\]]*)\\]", S);
            if (hair.Success)
            {
                var v = hair.Groups[1].Value.Split(',').Select(x => float.Parse(x.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                if (v.Length >= 3) car.HairColour = new Color(v[0], v[1], v[2]).gamma;      // (linear in the file, the material's colour is sRGB)
            }
            return car;
        }

        static Color HairColourOf(string who) => SidecarOf($"{ModelsDir}/Humans/human_{who}.fbx").HairColour;

        const string WearDir = PrefabsDir + "/Player/Wear";

        /// <summary>
        /// Every item made for every person (human.py <c>item=…</c> → Art/Models/Humans/human_&lt;who&gt;__&lt;item&gt;.fbx): a
        /// prefab each (Prefabs/Player/Wear/&lt;who&gt;/&lt;item&gt;) — its skinned meshes with their bones' names
        /// (<see cref="WearPiece"/>), bound to the wearer's skeleton when worn — and its own baked maps.
        /// </summary>
        static WearPiece[] BuildWardrobe()
        {
            var list = new System.Collections.Generic.List<WearPiece>();
            string dir = ModelsDir + "/Humans";
            if (!AssetDatabase.IsValidFolder(dir)) return list.ToArray();
            ConfigureHumanTextures();
            foreach (var guid in AssetDatabase.FindAssets("human_ t:Model", new[] { dir }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                string fbx = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(fbx);
                int cut = file.IndexOf("__", System.StringComparison.Ordinal);
                if (cut < 0) continue;
                string who = file.Substring("human_".Length, cut - "human_".Length), item = file.Substring(cut + 2);
                var car = SidecarOf(fbx);
                if (AssetImporter.GetAtPath(fbx) is ModelImporter imp) ConfigureWearImport(imp);
                var mat = HumanMaps($"Human_{who}__{item}", $"{who}__{item}", "outfit");
                var hairMat = HairCardMaterial($"Human_{who}__{item}_Hair", HairColourOf(who));
                var piece = Prefab($"{WearDir}/{who}/{item}.prefab", () =>
                {
                    var go = new GameObject(item);
                    var wear = go.AddComponent<WearPiece>();
                    wear.Slot = car.Slot ?? "";
                    wear.Item = item;
                    wear.Who = who;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                    var parts = new System.Collections.Generic.List<WearPiece.Part>();
                    foreach (var src in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var child = new GameObject(src.gameObject.name);
                        child.transform.SetParent(go.transform, false);
                        var r = child.AddComponent<SkinnedMeshRenderer>();
                        r.sharedMesh = src.sharedMesh;
                        r.sharedMaterial = WardrobeRules.PartOf(src.gameObject.name) == "Hair" ? hairMat : mat;
                        r.localBounds = src.localBounds;
                        r.shadowCastingMode = ShadowCastingMode.On;
                        r.updateWhenOffscreen = false;
                        parts.Add(new WearPiece.Part { Renderer = r, Bones = src.bones.Select(b => b != null ? b.name : "").ToArray(), Root = src.rootBone != null ? src.rootBone.name : "hips" });
                    }
                    wear.Parts = parts.ToArray();
                    return go;
                });
                if (piece != null && piece.TryGetComponent<WearPiece>(out var w)) list.Add(w);
            }
            return list.ToArray();
        }

        /// <summary>An item's file: the skeleton and its meshes, no takes, no Avatar (it is worn on a person's skeleton).</summary>
        static void ConfigureWearImport(ModelImporter imp)
        {
            if (imp.animationType == ModelImporterAnimationType.Generic && !imp.importAnimation && imp.materialImportMode == ModelImporterMaterialImportMode.None
                && imp.importNormals == ModelImporterNormals.Import && imp.importTangents == ModelImporterTangents.CalculateMikk && imp.bakeAxisConversion) return;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.bakeAxisConversion = true;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.skinWeights = ModelImporterSkinWeights.Standard;     // (four bones a vertex)
            imp.SaveAndReimport();
        }

        /// <summary>The outfits of the departments as the persons wear them out of the box (a starting point to edit in Styles.asset).</summary>
        static Stealth.Configs.HumanOutfit[] DefaultOutfits() => new[] { "backend", "qa", "sysadmin", "designer" }
            .Select(who => new Stealth.Configs.HumanOutfit { Items = SidecarOf($"{ModelsDir}/Humans/human_{who}.fbx").Items.Values.ToArray() }).ToArray();

        /// <summary>
        /// Hair cards: strips of the strand texture (Art/Textures/Humans/hair_strands.png, white strands — the colour is the
        /// person's hair) cut out by alpha (coverage kept in the mips: strands do not melt away in the distance), seen from
        /// both sides, matte with a little sheen.
        /// </summary>
        static Material HairCardMaterial(string name, Color colour)
        {
            var m = LitAsset($"{HumansDataDir}/{name}.mat");
            m.SetColor("_BaseColor", colour);
            m.SetTexture("_BaseMap", Tex(HumanTextures + "/hair_strands.png"));
            m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", 0.45f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_Metallic", 0f);
            m.SetTexture("_BumpMap", null);
            m.DisableKeyword("_NORMALMAP");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
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
                if (file == "hair_strands")
                {
                    // the strand texture: coverage kept in the mips (a box filter thins strands to nothing in the distance), alpha as transparency
                    if (imp.mipMapsPreserveCoverage && imp.alphaIsTransparency && imp.wrapMode == TextureWrapMode.Clamp && imp.sRGBTexture) continue;
                    imp.textureType = TextureImporterType.Default;
                    imp.sRGBTexture = true;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = true;
                    imp.mipMapsPreserveCoverage = true;
                    imp.alphaTestReferenceValue = 0.45f;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.filterMode = FilterMode.Trilinear;
                    imp.anisoLevel = 4;
                    imp.SaveAndReimport();
                    continue;
                }
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

        /// <summary>
        /// The way the Avatar is described (<see cref="HumanDescriptionOf"/>), stamped on the import: a model imported with
        /// an older description is imported anew. Change it with the description.
        /// </summary>
        const string HumanAvatarVersion = "humanoid: T-pose from the bind pose, facing +Z, palms down; twist on the keyed bone; fifteen finger bones a hand, straight in the T-pose, the palm level by the knuckles";

        internal static void ConfigureHumanImport(ModelImporter imp, bool force = false)
        {
            bool dirty = force || imp.animationType != ModelImporterAnimationType.Human || imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                         || imp.humanDescription.human == null || imp.humanDescription.human.Length != HumanSkeleton.Map.Length
                         || imp.userData != HumanAvatarVersion
                         || imp.materialImportMode != ModelImporterMaterialImportMode.None ||
                         !imp.bakeAxisConversion || !imp.importAnimation || imp.clipAnimations.Length == 0 || imp.clipAnimations.Any(c => !c.loopTime || !c.lockRootRotation)
                         || imp.clipAnimations.Length != imp.defaultClipAnimations.Length // a take was added in Blender (Sit, Climb…)
                         || imp.importNormals != ModelImporterNormals.Import || imp.importTangents != ModelImporterTangents.CalculateMikk
                         || imp.maxBonesPerVertex != 4 || !imp.isReadable;
            if (!dirty) return;
            // the Avatar: our bones mapped onto the humanoid ones, described in a T-pose worked out from the model itself
            var description = HumanDescriptionOf(imp.assetPath);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            if (description is HumanDescription d) imp.humanDescription = d;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.bakeAxisConversion = true;
            imp.importAnimation = true;
            // the normal maps were baked against these normals and MikkTSpace tangents (Blender's bake uses the same)
            imp.importNormals = ModelImporterNormals.Import;
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.maxBonesPerVertex = 4;
            imp.isReadable = true; // the own view is cut out of these meshes (AssetBuilder.OwnView)
            imp.skinWeights = ModelImporterSkinWeights.Standard;
            imp.userData = HumanAvatarVersion;
            imp.SaveAndReimport();
            // every take loops, is called by its action name (the exporter writes "Rig|Walk") and stays in place: the player
            // controller moves the body, the takes keep the root where it was (height and turn included: lying, sitting)
            imp.clipAnimations = imp.defaultClipAnimations.Select(c =>
            {
                c.name = c.takeName.Contains('|') ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                c.loopTime = true;
                c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                c.heightFromFeet = false;
                return c;
            }).ToArray();
            imp.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(imp.assetPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) Debug.LogError($"[Builder] {imp.assetPath}: the humanoid Avatar is not valid");
            else Debug.Log($"[Builder] {imp.assetPath}: humanoid Avatar ok");
        }

        /// <summary>
        /// The humanoid description of a rigged human: our bones mapped (<see cref="HumanSkeleton.Map"/>) and every transform
        /// of the model in a T-pose — the arms turned straight out, the spine up, the legs down (<see cref="HumanSkeleton.TPose"/>)
        /// — from its rest pose (an A-pose). Null when the model is not there or lacks a bone.
        /// </summary>
        static HumanDescription? HumanDescriptionOf(string fbx)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (asset == null) return null;
            var go = Object.Instantiate(asset);
            go.name = asset.name;
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                var all = go.GetComponentsInChildren<Transform>(true);
                Transform Bone(string n) => all.FirstOrDefault(t => t.name == n);
                if (HumanSkeleton.Map.Any(m => Bone(m.bone) == null)) { Debug.LogError($"[Builder] {fbx}: a bone of the common skeleton is missing"); return null; }
                // the file comes in posed (a frame of a take, not the rest pose): the T-pose is worked out from the pose the
                // meshes were bound in — the real A-pose
                int unbound = ToBindPose(go);
                if (unbound > 0) Debug.LogWarning($"[Builder] {fbx}: {unbound} bones of the common skeleton are bound to no mesh (their rest pose is unknown)");
                var chest = Bone("chest");
                // Unity works out every joint's axes from this pose taking the figure to face +Z: ours faces -Z in the file
                // (the prefab turns the model round), which bent the knees and elbows the wrong way round — the armature is
                // turned to face +Z in the description
                var armature = Bone("hips").parent;
                if (armature != null && armature != go.transform && Facing(all) < 0f)
                {
                    var turnRound = Quaternion.Euler(0f, 180f, 0f);
                    armature.SetPositionAndRotation(turnRound * armature.position, turnRound * armature.rotation);
                }
                foreach (var (bone, child, aim) in HumanSkeleton.TPose)
                {
                    Transform b = Bone(bone), c = child != null ? Bone(child) : null;
                    // straight out to its side: along X exactly (the shoulder's side of the chest)
                    var outward = new Vector3(Mathf.Sign(Bone(bone.EndsWith(".L") ? "upper_arm.L" : "upper_arm.R").position.x - chest.position.x), 0f, 0f);
                    // (a bone with no child — a finger's last — lies along its own Y, as Blender writes bones)
                    var turn = HumanSkeleton.Turn(c != null ? c.position - b.position : b.up, HumanSkeleton.Direction(aim, outward));
                    b.rotation = turn * b.rotation;
                }
                // … and the palms face down, the thumbs ahead: the whole arm is rolled about its length until the thumb's side
                // of the hand looks forward (from the A-pose it nearly does; a palm looking ahead is a quarter turn of twist in
                // every take, which Unity then spreads over the upper arm and the forearm)
                foreach (var sd in new[] { "L", "R" })
                {
                    Transform up = Bone("upper_arm." + sd), hand = Bone("hand." + sd);
                    var axis = (hand.position - up.position).normalized;
                    // (the thumb's side by the row of knuckles, little finger to index: it lies in the palm's plane exactly, and
                    // the fingers' one-axis joints then curl about the axis Unity gives them)
                    var thumbSide = Vector3.ProjectOnPlane(Bone("index.1." + sd).position - Bone("little.1." + sd).position, axis);
                    float roll = Vector3.SignedAngle(thumbSide, Vector3.forward, axis);
                    up.rotation = Quaternion.AngleAxis(roll, axis) * up.rotation;
                }
                return new HumanDescription
                {
                    human = HumanSkeleton.Map.Select(m => new HumanBone
                    {
                        boneName = m.bone,
                        humanName = HumanTrait.BoneName[(int)m.human],
                        limit = new HumanLimit { useDefaultValues = true },
                    }).ToArray(),
                    skeleton = all.Select(t => new SkeletonBone
                    {
                        name = t.name,
                        position = t.localPosition,
                        rotation = t.localRotation,
                        scale = t.localScale,
                    }).ToArray(),
                    // a limb's twist stays on the bone that was keyed (no twist bones to spread it over): measured by the audit
                    upperArmTwist = HumanSkeleton.ArmTwist, lowerArmTwist = HumanSkeleton.ArmTwist,
                    upperLegTwist = HumanSkeleton.LegTwist, lowerLegTwist = HumanSkeleton.LegTwist,
                    armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false,
                };
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Puts the bones of a model where its skinned meshes were bound (the rest pose), whatever pose the import left it
        /// in. Returns how many bones of the common skeleton no mesh is bound to.
        /// </summary>
        internal static int ToBindPose(GameObject go)
        {
            var bound = new System.Collections.Generic.Dictionary<Transform, Matrix4x4>();
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                var poses = r.sharedMesh.bindposes;
                var bones = r.bones;
                for (int i = 0; i < bones.Length && i < poses.Length; i++)
                    if (bones[i] != null && !bound.ContainsKey(bones[i])) bound[bones[i]] = r.transform.localToWorldMatrix * poses[i].inverse;
            }
            // (parents come first in the hierarchy's order)
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (bound.TryGetValue(t, out var m)) t.SetPositionAndRotation(m.GetColumn(3), m.rotation);
            return HumanSkeleton.Map.Count(m => bound.Keys.All(t => t.name != m.bone));
        }

        /// <summary>Which way the figure faces along Z (+1 / -1): its toes are ahead of its ankles.</summary>
        static float Facing(Transform[] bones)
        {
            Transform foot = bones.FirstOrDefault(t => t.name == "foot.L"), toe = bones.FirstOrDefault(t => t.name == "toe.L");
            return foot == null || toe == null ? 1f : Mathf.Sign(toe.position.z - foot.position.z);
        }

        /// <summary>
        /// The one controller of every rigged human, in four layers over the humanoid Avatar (<see cref="HumanLayers"/>):
        /// <b>Legs</b> (the base: root, legs, feet), <b>UpperBody</b> (spine, arms, hands; its IK pass carries the held thing in
        /// the right hand), <b>Head</b> (the head and the face; the look turns it after the animation) — the three play the
        /// same states: Speed (m/s) blends Idle → Walk → Run, Crouch blends crouched idle → crouch walk, Down lies on the
        /// floor, Sit sits in the car, Pose (<see cref="BodyPose"/>) plays Climb (by ClimbSpeed: backwards on the way down,
        /// still when hanging), Terminal or Work — and <b>RightHand</b> (the right fingers) closes them on what the hand holds
        /// (its weight is set while holding).
        /// </summary>
        static AnimatorController BuildHumanController(AnimationClip idle, AnimationClip walk, AnimationClip run,
                                                       AnimationClip crouchIdle, AnimationClip crouchWalk, AnimationClip down, AnimationClip sit,
                                                       AnimationClip climb, AnimationClip terminal, AnimationClip work)
        {
            // (rewritten every time: their muscles are tuned in HumanSkeleton.Grip / Cup)
            var grip = HandClip(HumanGripPath, "Grip", HumanSkeleton.Grip);
            var cup = HandClip(HumanCupPath, "Cup", HumanSkeleton.Cup);
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(HumanControllerPath);
            // the prefab references it: keep its GUID — unless it is older than its layers or takes (no Pose, not layered, other
            // clips) or its clips are gone (the takes were exported anew)
            if (existing != null && !s_Force && existing.layers.Length == HumanLayers.Count && !existing.layers[HumanLayers.Head].iKPass
                && existing.parameters.Any(p => p.name == "Pose") && existing.parameters.Any(p => p.name == "Cup")
                && existing.parameters.Any(p => p.name == "Stride")
                && existing.animationClips.All(c => c != null) && (idle == null || existing.animationClips.Contains(idle))) return existing;
            AssetDatabase.DeleteAsset(HumanControllerPath);      // generated, never tuned by hand
            var c = AnimatorController.CreateAnimatorControllerAtPath(HumanControllerPath);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            c.AddParameter("Down", AnimatorControllerParameterType.Bool);
            c.AddParameter("Sit", AnimatorControllerParameterType.Bool);
            c.AddParameter("Pose", AnimatorControllerParameterType.Int);
            c.AddParameter("ClimbSpeed", AnimatorControllerParameterType.Float);
            c.AddParameter("Cup", AnimatorControllerParameterType.Float);
            // (the walking states play at it: 1 until HumanRig sets it)
            c.AddParameter(new AnimatorControllerParameter { name = "Stride", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            // the layers and their masks: the base is the first layer the controller was created with
            var layers = c.layers;
            layers[0].name = "Legs";
            layers[0].avatarMask = HumanMask("HumanLegs", AvatarMaskBodyPart.Root, AvatarMaskBodyPart.LeftLeg, AvatarMaskBodyPart.RightLeg,
                AvatarMaskBodyPart.LeftFootIK, AvatarMaskBodyPart.RightFootIK);
            c.layers = layers;
            void Layer(string name, AvatarMask mask, bool ik, float weight)
            {
                c.AddLayer(name);
                var all = c.layers;
                var l = all[all.Length - 1];
                l.avatarMask = mask;
                l.iKPass = ik;
                l.defaultWeight = weight;
                l.blendingMode = AnimatorLayerBlendingMode.Override;
                all[all.Length - 1] = l;
                c.layers = all;
            }
            Layer("UpperBody", HumanMask("HumanUpperBody", AvatarMaskBodyPart.Body, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK), true, 1f);
            Layer("Head", HumanMask("HumanHead", AvatarMaskBodyPart.Head), false, 1f);
            Layer("RightHand", HumanMask("HumanRightHand", AvatarMaskBodyPart.RightFingers), false, 0f);
            for (int i = 0; i < HumanLayers.RightHand; i++)
                BodyStates(c, i, idle, walk, run, crouchIdle, crouchWalk, down, sit, climb, terminal, work);
            var hand = c.layers[HumanLayers.RightHand].stateMachine;
            // one state: the fist round a handle blending into the palm cupped under a compact thing (Cup 0 … 1)
            var held = hand.AddState("Grip");
            var tree = new BlendTree { name = "Grip", blendType = BlendTreeType.Simple1D, blendParameter = "Cup", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, c);
            tree.AddChild(grip, 0f);
            tree.AddChild(cup, 1f);
            held.motion = tree;
            hand.defaultState = held;
            EditorUtility.SetDirty(c);
            return c;
        }

        /// <summary>The same states in a layer of the controller (the legs, the upper body and the head each play them).</summary>
        static void BodyStates(AnimatorController c, int layer, AnimationClip idle, AnimationClip walk, AnimationClip run,
                               AnimationClip crouchIdle, AnimationClip crouchWalk, AnimationClip down, AnimationClip sit,
                               AnimationClip climb, AnimationClip terminal, AnimationClip work)
        {
            var sm = c.layers[layer].stateMachine;
            var move = c.CreateBlendTreeInController("Move", out var moveTree, layer);
            moveTree.blendParameter = "Speed";
            moveTree.useAutomaticThresholds = false;
            // each take where its feet step at the body's speed (HumanMath: the takes' speeds); faster than the fastest,
            // the steps quicken (Stride)
            moveTree.AddChild(idle, 0f);
            moveTree.AddChild(walk, HumanMath.WalkTakeSpeed);
            moveTree.AddChild(run, HumanMath.RunTakeSpeed);
            move.speedParameterActive = true;
            move.speedParameter = "Stride";
            var crouch = c.CreateBlendTreeInController("Crouch", out var crouchTree, layer);
            crouchTree.blendParameter = "Speed";
            crouchTree.useAutomaticThresholds = false;
            crouchTree.AddChild(crouchIdle, 0f);
            crouchTree.AddChild(crouchWalk, HumanMath.CrouchTakeSpeed);
            crouch.speedParameterActive = true;
            crouch.speedParameter = "Stride";
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
        }

        /// <summary>A humanoid avatar mask with just these body parts (an asset next to the controller, rewritten each time).</summary>
        static AvatarMask HumanMask(string name, params AvatarMaskBodyPart[] parts)
        {
            string path = $"{HumansDataDir}/{name}.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, System.Array.IndexOf(parts, part) >= 0);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>
        /// The right hand closed round a handle, as a humanoid clip of finger muscles (our hand has one finger bone, mapped as
        /// the index: it curls all four; the thumb comes over them) — the RightHand layer plays it while the hand holds something.
        /// </summary>
        static AnimationClip HandClip(string path, string name, (string muscle, float value)[] muscles)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            foreach (var (muscle, value) in muscles)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), AnimationCurve.Constant(0f, 1f, value));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }
    }
}
