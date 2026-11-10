using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stealth.Configs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Model families of the visual styles: the CC0 textures and models (Tools/Art/fetch_cc0.py, Tools/Blender/import_cc0.py),
    /// the family versions of every material of the world, and the furniture the realistic family swaps for scanned models.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string ExternalDir = Root + "/Art/External";
        const string ExternalTextures = ExternalDir + "/Textures";
        const string ExternalModels = ExternalDir + "/Models";
        const string PixelTextures = ExternalDir + "/Pixel";
        const string FamilyMaterialsDir = StylesDir + "/Families";
        /// <summary>Self-made surfaces of the derelict family (Tools/Blender/derelict_textures.py), same layout as the scans.</summary>
        const string DerelictTextures = Root + "/Art/Textures/Derelict";
        /// <summary>Decay decals and posters of the derelict style (Tools/Blender/derelict_decals.py): RGBA, colour + coverage.</summary>
        const string DerelictDecals = DerelictTextures + "/Decals";
        /// <summary>Self-made furniture of the derelict family (Tools/Blender/derelict_props.py): Art/Models/StyleProps/Derelict/&lt;our model&gt;.fbx.</summary>
        const string DerelictPropsDir = ModelsDir + "/StyleProps/Derelict";

        /// <summary>Scanned CC0 model → our model it replaces in the realistic family (see Tools/Art/fetch_cc0.py).</summary>
        static readonly Dictionary<string, string> RealisticProps = new()
        {
            ["metal_office_desk"] = "desk", ["modern_arm_chair_01"] = "chair", ["drawer_cabinet"] = "cabinet",
            ["potted_plant_02"] = "plant", ["wooden_bookshelf_worn"] = "bookshelf", ["steel_frame_shelves_01"] = "shelf",
            ["cardboard_box_01"] = "boxes", ["sofa_02"] = "sofa", ["ArmChair_01"] = "armchair",
            ["modern_coffee_table_01"] = "coffee_table", ["round_wooden_table_01"] = "round_table", ["dining_table"] = "meeting_table",
            ["metal_trash_can"] = "trash_bin", ["desk_lamp_arm_01"] = "desk_lamp",
        };

        // ------------------------------------------------------------------ textures

        /// <summary>Import settings of the downloaded textures: normal maps, linear masks, point-filtered pixel art.</summary>
        static void ConfigureExternalTextures()
        {
            var roots = new[] { ExternalDir, DerelictTextures }.Where(AssetDatabase.IsValidFolder).ToArray();
            if (roots.Length == 0) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", roots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                var file = Path.GetFileNameWithoutExtension(path);
                bool pixel = path.StartsWith(PixelTextures);
                var type = file == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool srgb = file != "mask" && file != "normal";
                var filter = pixel ? FilterMode.Point : FilterMode.Trilinear;
                bool decal = path.StartsWith(DerelictDecals);            // single images with edges: never repeat
                var wrap = decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                if (imp.textureType == type && imp.sRGBTexture == srgb && imp.filterMode == filter && imp.wrapMode == wrap &&
                    imp.mipmapEnabled == !pixel && imp.maxTextureSize == 1024 && imp.alphaIsTransparency == decal) continue;
                imp.textureType = type;
                imp.sRGBTexture = srgb;
                imp.filterMode = filter;
                imp.wrapMode = wrap;
                imp.alphaIsTransparency = decal;
                imp.mipmapEnabled = !pixel;
                imp.anisoLevel = pixel ? 0 : 4;
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
            }
        }

        static Texture2D Tex(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        /// <summary>URP Lit with albedo, normal and the packed mask (metal R, occlusion G, smoothness A) of a texture folder.</summary>
        static void SetPbr(Material m, string folder, Color tint, float smoothness = 0.9f, float tiling = 1f)
        {
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", Tex($"{folder}/albedo.jpg"));
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            var normal = Tex($"{folder}/normal.jpg");
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            var mask = Tex($"{folder}/mask.png");
            m.SetTexture("_MetallicGlossMap", mask);
            m.SetTexture("_OcclusionMap", mask);
            m.SetFloat("_OcclusionStrength", 1f);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (mask != null) { m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP"); }
            else { m.DisableKeyword("_METALLICSPECGLOSSMAP"); m.DisableKeyword("_OCCLUSIONMAP"); }
        }

        /// <summary>
        /// A decal: URP Lit with the decal's colour and coverage. Stains blend softly (transparent, no depth write); posters
        /// are cut out where the paper is torn (alpha clip) so they stay opaque and sorted like the wall.
        /// </summary>
        static Material DecalMaterial(string name, string texture, float smoothness, bool clip)
        {
            var m = LitAsset($"{KitMaterialsDir}/{name}.mat");
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", Tex(texture));
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_ReceiveShadows", 1f);
            if (clip)
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
                m.SetFloat("_ZWrite", 1f);
            }
            else
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_AlphaClip", 0f);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetPixel(Material m, string id, Color tint, float smoothness = 0.08f)
        {
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", Tex($"{PixelTextures}/{id}.png"));
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
        }

        static Material LitAsset(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        /// <summary>Architecture and finish materials made of the scanned and the pixel textures.</summary>
        static void BuildExternalKitMaterials()
        {
            ConfigureExternalTextures();
            void Pbr(string name, string id, Color tint, float smooth = 0.9f, float tiling = 1f)
            {
                var m = LitAsset($"{KitMaterialsDir}/{name}.mat");
                SetPbr(m, $"{ExternalTextures}/{id}", tint, smooth, tiling);
                EditorUtility.SetDirty(m);
                s_Kit[name] = m;
            }
            void Px(string name, string id, Color tint)
            {
                var m = LitAsset($"{KitMaterialsDir}/{name}.mat");
                SetPixel(m, id, tint);
                EditorUtility.SetDirty(m);
                s_Kit[name] = m;
            }
            // realistic office / bank (PAYDAY)
            Pbr("Real_Marble", "marble_01", Color.white);
            Pbr("Real_Plaster", "plastered_wall_04", new Color(1f, 0.97f, 0.92f));
            Pbr("Real_CeilingTiles", "ceiling_interior", Color.white);
            Pbr("Real_DarkWood", "dark_wood", Color.white);
            Pbr("Real_Carpet", "dirty_carpet", new Color(0.75f, 0.8f, 0.9f));
            Pbr("Real_Laminate", "laminate_floor_02", Color.white);
            Pbr("Real_BeigeWall", "beige_wall_001", Color.white);
            Pbr("Real_MetalPlate", "metal_plate", Color.white);
            // realistic old English house (Sir, You Are Being Hunted)
            Pbr("Real_OldFloor", "old_wooden_floor_01", Color.white);
            Pbr("Real_Peeling", "peeling_painted_wall", Color.white);
            Pbr("Real_Brick", "large_red_bricks", Color.white);
            Pbr("Real_DarkPlanks", "dark_planks", Color.white);
            Pbr("Real_Plaster2", "plastered_wall", new Color(0.92f, 0.9f, 0.8f));
            // the mansion of R.E.P.O.
            Pbr("Real_Wallpaper", "decrepit_wallpaper", Color.white);
            Pbr("Real_PaneledWood", "dark_paneled_wood", Color.white);
            Pbr("Real_CheckerTiles", "checkered_pavement_tiles", Color.white);
            // ship planks (Obra Dinn), parquet (Blue Prince), concrete (Content Warning)
            Pbr("Real_PlankFloor", "plank_flooring", Color.white);
            Pbr("Real_RawPlanks", "raw_plank_wall", Color.white);
            Pbr("Real_Herringbone", "herringbone_parquet", new Color(0.95f, 0.9f, 0.85f));
            Pbr("Real_HangarConcrete", "hangar_concrete_floor", Color.white);
            Pbr("Real_ConcreteWall", "concrete_wall_006", Color.white);
            Pbr("Real_ConcreteClean", "concrete_floor_01", new Color(0.98f, 0.99f, 1f));
            // bare grimy plate of the derelict props (Steel parts)
            Pbr("Real_GrimePlate", "metal_plate", new Color(0.45f, 0.5f, 0.5f), 0.85f);
            // the derelict family's own surfaces (self-made, Tools/Blender/derelict_textures.py)
            void Dr(string name, string id, Color tint, float smooth = 1f)
            {
                var m = LitAsset($"{KitMaterialsDir}/{name}.mat");
                SetPbr(m, $"{DerelictTextures}/{id}", tint, smooth);
                EditorUtility.SetDirty(m);
                s_Kit[name] = m;
            }
            Dr("Dr_SteelPanels", "dr_steel_panels", Color.white);
            Dr("Dr_FloorPlate", "dr_floor_plate", Color.white);
            Dr("Dr_CeilingVents", "dr_ceiling_vents", Color.white);
            Dr("Dr_PaintedMetal", "dr_painted_metal", Color.white);
            Dr("Dr_Rust", "dr_rust", Color.white);
            Dr("Dr_Composite", "dr_composite", Color.white);
            Dr("Dr_Vinyl", "dr_vinyl", Color.white);
            var screen = LitAsset($"{KitMaterialsDir}/Dr_Screen.mat");          // dead terminals: a faint sick-green glow
            screen.SetColor("_BaseColor", new Color(0.02f, 0.04f, 0.035f));
            screen.SetFloat("_Smoothness", 0.85f);
            screen.EnableKeyword("_EMISSION");
            screen.SetColor("_EmissionColor", new Color(0.25f, 1f, 0.75f) * 1.2f);
            screen.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(screen);
            s_Kit["Dr_Screen"] = screen;
            // decay decals (soft transparent stains) and posters (torn paper: alpha clipped)
            foreach (var id in new[] { "floor_rust", "floor_oil", "floor_scorch", "floor_rubble", "wall_leak", "wall_mold", "wall_peel", "wall_scorch" })
                s_Kit[$"Dc_{id}"] = DecalMaterial($"Dc_{id}", $"{DerelictDecals}/{id}.png", id == "floor_oil" ? 0.85f : 0.2f, clip: false);
            for (int i = 0; i < 6; i++)
                s_Kit[$"Dc_poster_{i}"] = DecalMaterial($"Dc_poster_{i}", $"{DerelictDecals}/poster_{i}.png", 0.15f, clip: true);
            // computer screens: the terminal's last report and CCTV snow, glowing through their image; dead dark glass
            Material ScreenImage(string name, string image, float glow)
            {
                var m = LitAsset($"{KitMaterialsDir}/{name}.mat");
                m.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.03f));
                m.SetTexture("_BaseMap", Tex($"{DerelictDecals}/{image}.png"));
                m.SetFloat("_Smoothness", 0.9f);
                m.SetFloat("_Metallic", 0f);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", Tex($"{DerelictDecals}/{image}.png"));
                m.SetColor("_EmissionColor", Color.white * glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                EditorUtility.SetDirty(m);
                s_Kit[name] = m;
                return m;
            }
            ScreenImage("Dr_ScreenText", "screen_terminal", 1.5f);
            ScreenImage("Dr_ScreenStatic", "screen_static", 1.1f);
            var glass = LitAsset($"{KitMaterialsDir}/Dr_Glass.mat");
            glass.SetColor("_BaseColor", new Color(0.015f, 0.018f, 0.02f));
            glass.SetFloat("_Smoothness", 0.92f);
            EditorUtility.SetDirty(glass);
            s_Kit["Dr_Glass"] = glass;
            var rubber = LitAsset($"{KitMaterialsDir}/Dr_Rubber.mat");          // cables, hoses, rubber feet
            rubber.SetColor("_BaseColor", new Color(0.035f, 0.035f, 0.035f));
            rubber.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(rubber);
            s_Kit["Dr_Rubber"] = rubber;
            // PS1 / Lethal Company: tiny pixel textures, tinted
            Px("Px_Plate", "metal_plate", new Color(0.75f, 0.72f, 0.65f));
            Px("Px_Corrugated", "corrugated_iron_02", new Color(0.62f, 0.7f, 0.64f));
            Px("Px_Rust", "green_metal_rust", new Color(0.85f, 0.65f, 0.45f));
            Px("Px_Concrete", "concrete_wall_006", new Color(0.7f, 0.7f, 0.68f));
            Px("Px_Pipe", "painted_metal_shutter", new Color(0.45f, 0.62f, 0.5f));
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ family materials

        /// <summary>
        /// Every lit, non-glowing material of the world in every family: matte flat colours (low-poly), glossy saturated
        /// plastic (toy), tiny pixel textures (lo-fi), matte (clean), scanned PBR surfaces (realistic). Walls, floors and
        /// ceilings (stretched cubes) only change their finish — the style finishes cover them.
        /// </summary>
        static MaterialVariants[] BuildMaterialVariants()
        {
            var originals = new[] { "Environment", "Props", "Robots", "Player" }
                .SelectMany(MaterialsIn)
                .Where(m => m.shader != null && m.shader.name == "Universal Render Pipeline/Lit" && !m.IsKeywordEnabled("_EMISSION"))
                .ToArray();
            var sets = new List<MaterialVariants>();
            foreach (ModelFamily f in System.Enum.GetValues(typeof(ModelFamily)))
            {
                if (f == ModelFamily.None) continue;
                var to = new List<Material>();
                foreach (var o in originals)
                {
                    var m = LitAsset($"{FamilyMaterialsDir}/{f}/{o.name}.mat");
                    m.CopyPropertiesFromMaterial(o);
                    m.shaderKeywords = o.shaderKeywords;
                    FamilyLook(m, o, f);
                    EditorUtility.SetDirty(m);
                    to.Add(m);
                }
                sets.Add(new MaterialVariants { Family = f, From = originals, To = to.ToArray() });
            }
            AssetDatabase.SaveAssets();
            return sets.ToArray();
        }

        static Color Saturate(Color c, float k, float bright = 1f)
        {
            float grey = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            var o = Color.LerpUnclamped(new Color(grey, grey, grey), c, k) * bright;
            o.a = c.a;
            return o;
        }

        /// <summary>What a material becomes in a family, judged by its name (Wood, Fabric, Metal, …Body).</summary>
        static void FamilyLook(Material m, Material o, ModelFamily f)
        {
            string n = o.name;
            bool structure = n is "Wall" or "Floor" or "Ceiling" or "OutsideGround" or "Blinds" or "WindowFrame";
            bool wood = n.Contains("Wood") || n == "DoorFrame" || n == "PingPong";
            bool fabric = n.Contains("Fabric") || n.Contains("BeanBag");
            bool player = n.StartsWith("Player"); // team colours come on top as a property block: keep them clean
            bool metal = n.Contains("Metal") || n.Contains("Rack") || n.Contains("Stall") || n.Contains("Housing") ||
                         n.Contains("Hatch") || n.Contains("LaserPost") || n.Contains("Accent") || n.Contains("Keypad");
            bool painted = n.EndsWith("Body") || n.Contains("Vending") || n.Contains("Arcade") || n.Contains("Locker") || n == "Door";
            bool pot = n.Contains("Pot");
            var c = o.GetColor("_BaseColor");
            switch (f)
            {
                case ModelFamily.Lowpoly:
                    m.SetColor("_BaseColor", Saturate(c, 1.2f, 1.05f));
                    m.SetFloat("_Smoothness", 0.02f);
                    m.SetFloat("_Metallic", 0f);
                    break;
                case ModelFamily.Toy:
                    m.SetColor("_BaseColor", Saturate(c, 1.35f, 1.08f));
                    m.SetFloat("_Smoothness", 0.8f);
                    m.SetFloat("_Metallic", 0f);
                    break;
                case ModelFamily.Clean:
                    m.SetFloat("_Smoothness", 0.12f);
                    m.SetFloat("_Metallic", 0f);
                    break;
                case ModelFamily.Lofi:
                    if (structure || player) break;
                    string px = wood ? "dark_wood" : fabric ? "dirty_carpet" : pot ? "concrete_floor_worn_001" : metal ? "metal_plate"
                              : painted ? "green_metal_rust" : "painted_metal_shutter";
                    SetPixel(m, px, c * 1.35f);
                    break;
                case ModelFamily.Realistic:
                    if (structure) break;
                    if (player) { m.SetFloat("_Smoothness", 0.35f); break; }
                    if (wood) SetPbr(m, $"{ExternalTextures}/dark_wood", Color.white);
                    else if (fabric) SetPbr(m, $"{ExternalTextures}/dirty_carpet", Saturate(c, 1f, 1.5f));
                    else if (pot) SetPbr(m, $"{ExternalTextures}/concrete_floor_01", c * 1.3f);
                    else if (metal) SetPbr(m, $"{ExternalTextures}/metal_plate", c * 1.6f);
                    else if (painted) SetPbr(m, $"{ExternalTextures}/painted_metal_shutter", c * 1.5f);
                    break;
                case ModelFamily.Derelict:
                    // everything is industrial now: wood becomes painted steel, fabric cracked vinyl, plastic yellowed composite
                    if (structure) break;
                    if (player) { m.SetFloat("_Smoothness", 0.3f); break; }
                    if (fabric) SetPbr(m, $"{DerelictTextures}/dr_vinyl", Saturate(c, 0.4f, 1.6f), 1f, 2f);
                    else if (pot) SetPbr(m, $"{DerelictTextures}/dr_composite", Color.white, 1f, 2f);
                    else if (metal) SetPbr(m, $"{DerelictTextures}/dr_painted_metal", Saturate(c, 0.3f, 1.8f), 1f, 1.5f);
                    else if (wood) SetPbr(m, $"{DerelictTextures}/dr_painted_metal", Color.white, 1f, 1.5f);
                    else if (painted) SetPbr(m, $"{DerelictTextures}/dr_painted_metal", Saturate(c, 0.35f, 1.7f), 1f, 0.6f); // doors: one patch pattern, not a grid
                    else SetPbr(m, $"{DerelictTextures}/dr_composite", Saturate(c, 0.3f, 1.5f), 1f, 2f);
                    break;
            }
        }

        // ------------------------------------------------------------------ scanned furniture

        /// <summary>
        /// Prefabs of the scanned CC0 furniture (Art/External/Models, materials from their texture folders) and where each
        /// goes: stretched into the bounds of our model (never larger, at most 25 % out of proportion), standing on its floor.
        /// </summary>
        static PropReplacement[] BuildPropReplacements()
        {
            var list = new List<PropReplacement>();
            list.AddRange(BuildDerelictProps());
            if (!AssetDatabase.IsValidFolder(ExternalModels)) return list.ToArray();
            EnsureFolder($"{KitPrefabsDir}/Scanned");
            foreach (var (id, ours) in RealisticProps)
            {
                var fbx = $"{ExternalModels}/{id}.fbx";
                var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (imp == null) continue;
                if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.SaveAndReimport(); }
                var prefab = Prefab($"{KitPrefabsDir}/Scanned/{id}.prefab", () =>
                {
                    var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                    go.name = id;
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++)
                        {
                            var folder = $"{ExternalModels}/{id}/{id}_{i}";
                            var m = LitAsset($"{FamilyMaterialsDir}/Scanned/{id}_{i}.mat");
                            SetPbr(m, AssetDatabase.IsValidFolder(folder) ? folder : $"{ExternalModels}/{id}/{id}_0", Color.white);
                            EditorUtility.SetDirty(m);
                            mats[i] = m;
                        }
                        r.sharedMaterials = mats;
                        r.shadowCastingMode = ShadowCastingMode.On;
                    }
                    foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    return go;
                });
                string ourPath;
                try { ourPath = ModelPath(ours); }
                catch (FileNotFoundException) { continue; }
                var ourMeshes = AssetDatabase.LoadAllAssetsAtPath(ourPath).OfType<Mesh>().ToArray();
                var key = ourMeshes.FirstOrDefault(m => m.name == "Body") ?? ourMeshes.FirstOrDefault();
                if (key == null) continue;
                var ob = BoundsOf(AssetDatabase.LoadAssetAtPath<GameObject>(ourPath), skipGlow: true);
                var nb = BoundsOf(prefab);
                var s = new Vector3(ob.size.x / nb.size.x, ob.size.y / nb.size.y, ob.size.z / nb.size.z);
                float mean = Mathf.Pow(s.x * s.y * s.z, 1f / 3f);
                s = new Vector3(Mathf.Min(s.x, mean * 1.25f), Mathf.Min(s.y, mean * 1.25f), Mathf.Min(s.z, mean * 1.25f));
                var pos = new Vector3(ob.center.x - nb.center.x * s.x, ob.min.y - nb.min.y * s.y, ob.center.z - nb.center.z * s.z);
                list.Add(new PropReplacement { Family = ModelFamily.Realistic, Key = key, Prefab = prefab, Position = pos, Scale = s });
            }
            AssetDatabase.SaveAssets();
            return list.ToArray();
        }

        /// <summary>
        /// The derelict family's furniture (self-made, Tools/Blender/derelict_props.py): one FBX per our model, its parts named
        /// by surface (Paint, Composite, Rust, Vinyl, Rubber, Screen, Steel). Fitted into our model's bounds like the scans.
        /// </summary>
        static List<PropReplacement> BuildDerelictProps()
        {
            var list = new List<PropReplacement>();
            if (!AssetDatabase.IsValidFolder(DerelictPropsDir)) return list;
            EnsureFolder($"{KitPrefabsDir}/Derelict/Props");
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { DerelictPropsDir }))
            {
                var fbx = AssetDatabase.GUIDToAssetPath(guid);
                var ours = Path.GetFileNameWithoutExtension(fbx);
                var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (imp == null) continue;
                if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.SaveAndReimport(); }
                var prefab = Prefab($"{KitPrefabsDir}/Derelict/Props/{ours}.prefab", () =>
                {
                    var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                    go.name = $"{ours}_derelict";
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                    {
                        r.sharedMaterial = DerelictSurface(r.gameObject.name);
                        r.shadowCastingMode = ShadowCastingMode.On;
                    }
                    foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    return go;
                });
                string ourPath;
                try { ourPath = ModelPath(ours); }
                catch (FileNotFoundException) { continue; }
                var ourMeshes = AssetDatabase.LoadAllAssetsAtPath(ourPath).OfType<Mesh>().ToArray();
                var key = ourMeshes.FirstOrDefault(m => m.name == "Body") ?? ourMeshes.FirstOrDefault();
                if (key == null) continue;
                var ob = BoundsOf(AssetDatabase.LoadAssetAtPath<GameObject>(ourPath), skipGlow: true);
                var nb = BoundsOf(prefab);
                // modelled to our model's size: only a small correction, never noticeably larger than the original
                var s = new Vector3(Mathf.Min(1.05f, ob.size.x / nb.size.x), Mathf.Min(1.05f, ob.size.y / nb.size.y), Mathf.Min(1.05f, ob.size.z / nb.size.z));
                var pos = new Vector3(ob.center.x - nb.center.x * s.x, ob.min.y - nb.min.y * s.y, ob.center.z - nb.center.z * s.z);
                bool ownScreens = prefab.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("Screen") || r.name.StartsWith("Glass"));
                list.Add(new PropReplacement { Family = ModelFamily.Derelict, Key = key, Prefab = prefab, Position = pos, Scale = s, HideGlow = ownScreens });
            }
            AssetDatabase.SaveAssets();
            return list;
        }

        /// <summary>The material of a derelict prop part by its name prefix.</summary>
        static Material DerelictSurface(string part)
        {
            if (part.StartsWith("ScreenText")) return K("Dr_ScreenText");
            if (part.StartsWith("ScreenStatic")) return K("Dr_ScreenStatic");
            if (part.StartsWith("Glass")) return K("Dr_Glass");
            if (part.StartsWith("Screen")) return K("Dr_Screen");
            if (part.StartsWith("Composite")) return K("Dr_Composite");
            if (part.StartsWith("Rust")) return K("Dr_Rust");
            if (part.StartsWith("Vinyl")) return K("Dr_Vinyl");
            if (part.StartsWith("Rubber")) return K("Dr_Rubber");
            if (part.StartsWith("Steel")) return K("Real_GrimePlate");
            return K("Dr_PaintedMetal");
        }

        /// <summary>Bounds of all renderers of a model asset in its own space (without the glowing Eye parts if asked).</summary>
        static Bounds BoundsOf(GameObject asset, bool skipGlow = false)
        {
            var go = (GameObject)Object.Instantiate(asset);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            Bounds b = default;
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (skipGlow && r.gameObject.name.StartsWith("Eye")) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            Object.DestroyImmediate(go);
            return b;
        }
    }
}
