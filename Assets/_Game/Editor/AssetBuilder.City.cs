using System.Collections.Generic;
using System.IO;
using Stealth.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string CityTexturesDir = Root + "/Art/Textures/City";
        const string CityMaterialsDir = MaterialsDir + "/City";

        /// <summary>
        /// The city round the yard (<see cref="CityPlan"/>): its materials by <see cref="CitySlot"/> (textures from
        /// Tools/Art/generate_city.py; rebuilt every time, they are generated), the object its merged mesh is drawn with
        /// and a street lamp's light.
        /// </summary>
        static void BuildCityArt(LevelArt art)
        {
            EnsureFolder(CityMaterialsDir);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CityTexturesDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                bool glow = path.EndsWith("_glow.png");
                if (imp.wrapMode == TextureWrapMode.Repeat && imp.anisoLevel == 4 && imp.sRGBTexture == !glow) continue;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.anisoLevel = 4;
                imp.mipmapEnabled = true;
                imp.sRGBTexture = !glow;
                imp.SaveAndReimport();
            }
            Texture2D T(string n) => n == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{CityTexturesDir}/{n}.png");
            Material Mat(string name, Color color, string tex = null, float smooth = 0.25f, float metal = 0f, string glowTex = null, Color? glow = null)
            {
                var path = $"{CityMaterialsDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(m, path);
                }
                m.SetColor("_BaseColor", color);
                m.SetTexture("_BaseMap", T(tex));
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                if (glow.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", glow.Value);
                    m.SetTexture("_EmissionMap", T(glowTex));
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
                EditorUtility.SetDirty(m);
                return m;
            }
            var windows = new Color(1.5f, 1.2f, 0.75f); // lit windows bloom in the night
            var mats = new Material[System.Enum.GetValues(typeof(CitySlot)).Length];
            mats[(int)CitySlot.Road] = Mat("CityRoad", new Color(0.3f, 0.3f, 0.32f), "asphalt", 0.3f);
            mats[(int)CitySlot.Sidewalk] = Mat("CitySidewalk", new Color(0.62f, 0.62f, 0.6f), "paving", 0.2f);
            mats[(int)CitySlot.Paint] = Mat("CityPaint", new Color(0.92f, 0.92f, 0.88f), null, 0.35f);
            mats[(int)CitySlot.Roof] = Mat("CityRoof", new Color(0.2f, 0.2f, 0.22f), null, 0.15f);
            mats[(int)CitySlot.FacadeA] = Mat("CityFacadeA", Color.white, "facade_a", 0.3f, 0f, "facade_a_glow", windows);
            mats[(int)CitySlot.FacadeB] = Mat("CityFacadeB", Color.white, "facade_b", 0.2f, 0f, "facade_b_glow", windows);
            mats[(int)CitySlot.FacadeC] = Mat("CityFacadeC", Color.white, "facade_c", 0.7f, 0.2f, "facade_c_glow", windows);
            mats[(int)CitySlot.Metal] = Mat("CityMetal", new Color(0.26f, 0.28f, 0.3f), null, 0.5f, 0.5f);
            mats[(int)CitySlot.LampGlow] = Mat("CityLampGlow", new Color(1f, 0.86f, 0.62f), null, 0.5f, 0f, null, new Color(3f, 2.4f, 1.5f));
            mats[(int)CitySlot.Leaves] = Mat("CityLeaves", new Color(0.16f, 0.3f, 0.14f), null, 0.1f);
            mats[(int)CitySlot.Bark] = Mat("CityBark", new Color(0.25f, 0.17f, 0.1f), null, 0.1f);
            // parked cars
            mats[(int)CitySlot.CarRed] = Mat("CityCarRed", new Color(0.52f, 0.1f, 0.08f), null, 0.75f, 0.3f);
            mats[(int)CitySlot.CarBlue] = Mat("CityCarBlue", new Color(0.12f, 0.2f, 0.38f), null, 0.75f, 0.3f);
            mats[(int)CitySlot.CarSilver] = Mat("CityCarSilver", new Color(0.62f, 0.64f, 0.66f), null, 0.75f, 0.4f);
            mats[(int)CitySlot.Glass] = Mat("CityGlass", new Color(0.08f, 0.1f, 0.13f), null, 0.9f, 0.2f);
            mats[(int)CitySlot.Tyre] = Mat("CityTyre", new Color(0.05f, 0.05f, 0.05f), null, 0.2f);
            // the buildings' stone trims and plinths, the cars' tail lamps (and the traffic lights' red), the tree pits
            mats[(int)CitySlot.Stone] = Mat("CityStone", new Color(0.78f, 0.75f, 0.68f), "paving", 0.2f);
            mats[(int)CitySlot.Plinth] = Mat("CityPlinth", new Color(0.36f, 0.35f, 0.34f), "paving", 0.25f);
            mats[(int)CitySlot.TailLight] = Mat("CityTailLight", new Color(1f, 0.12f, 0.08f), null, 0.5f, 0f, null, new Color(2.6f, 0.25f, 0.15f));
            mats[(int)CitySlot.Soil] = Mat("CitySoil", new Color(0.16f, 0.12f, 0.09f), "asphalt", 0.05f);
            // the models' own (Tools/Blender/street.py): windows lit warm, a television's blue, dark; more car paints;
            // the benches' slats, concrete, chrome, white window frames
            mats[(int)CitySlot.WindowLit] = Mat("CityWindowLit", new Color(0.95f, 0.82f, 0.55f), null, 0.85f, 0f, null, windows);
            mats[(int)CitySlot.WindowDim] = Mat("CityWindowDim", new Color(0.35f, 0.45f, 0.65f), null, 0.85f, 0f, null, new Color(0.45f, 0.7f, 1.3f));
            mats[(int)CitySlot.WindowDark] = Mat("CityWindowDark", new Color(0.07f, 0.09f, 0.12f), null, 0.92f, 0.3f);
            mats[(int)CitySlot.CarWhite] = Mat("CityCarWhite", new Color(0.82f, 0.82f, 0.8f), null, 0.75f, 0.2f);
            mats[(int)CitySlot.CarBlack] = Mat("CityCarBlack", new Color(0.06f, 0.06f, 0.07f), null, 0.8f, 0.3f);
            mats[(int)CitySlot.CarGreen] = Mat("CityCarGreen", new Color(0.12f, 0.28f, 0.18f), null, 0.75f, 0.3f);
            mats[(int)CitySlot.Wood] = Mat("CityWood", new Color(0.42f, 0.27f, 0.15f), null, 0.3f);
            mats[(int)CitySlot.Concrete] = Mat("CityConcrete", new Color(0.55f, 0.55f, 0.53f), "paving", 0.15f);
            mats[(int)CitySlot.Chrome] = Mat("CityChrome", new Color(0.75f, 0.76f, 0.78f), null, 0.85f, 0.9f);
            mats[(int)CitySlot.Frame] = Mat("CityFrame", new Color(0.86f, 0.86f, 0.84f), null, 0.4f);
            // resolved per copy (CityProp.Paint / Pane) before merging: never drawn as themselves, kept filled as a fallback
            mats[(int)CitySlot.Livery] = mats[(int)CitySlot.CarWhite];
            mats[(int)CitySlot.Pane] = mats[(int)CitySlot.WindowDark];

            var piece = Prefab($"{LevelPrefabs}/City.prefab", () =>
            {
                // Ignore Raycast like the ground outside: out of the navmesh and of every sight check
                var go = new GameObject("City") { layer = LevelMetrics.CeilingLayer };
                go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                return go;
            });
            var lamp = Prefab($"{LevelPrefabs}/CityLamp.prefab", () =>
            {
                // a street lamp's light, straight down from its head (the pole and the head are in the city's mesh); it stays
                // out on the street, over 8 m from the house, so it needs no shadows
                var go = new GameObject("CityLamp");
                var l = Lamp(go.transform, "Light", Vector3.zero, Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(1f, 0.8f, 0.55f), 15f, 11f, LightShadows.None);
                l.spotAngle = 70f; // its pool (r 4 m) never reaches a wall: the light has no shadows and must not shine into the house
                l.innerSpotAngle = 40f;
                Object.DestroyImmediate(l.GetComponent<LightSource>()); // scenery: robots and the stealth meter never see it
                return go;
            });
            // the house's services (UtilityPlan)
            var util = new Material[System.Enum.GetValues(typeof(UtilitySlot)).Length];
            util[(int)UtilitySlot.Duct] = Mat("UtilDuct", new Color(0.7f, 0.72f, 0.74f), null, 0.55f, 0.6f);
            util[(int)UtilitySlot.Grille] = Mat("UtilGrille", new Color(0.18f, 0.19f, 0.2f), null, 0.3f, 0.4f);
            util[(int)UtilitySlot.Pipe] = Mat("UtilPipe", new Color(0.82f, 0.82f, 0.8f), null, 0.45f);
            util[(int)UtilitySlot.Copper] = Mat("UtilCopper", new Color(0.72f, 0.42f, 0.24f), null, 0.6f, 0.7f);
            util[(int)UtilitySlot.Conduit] = Mat("UtilConduit", new Color(0.55f, 0.56f, 0.57f), null, 0.3f);
            util[(int)UtilitySlot.Plate] = Mat("UtilPlate", new Color(0.93f, 0.93f, 0.91f), null, 0.4f);
            util[(int)UtilitySlot.Dark] = Mat("UtilDark", new Color(0.05f, 0.05f, 0.05f), null, 0.2f);
            util[(int)UtilitySlot.Panel] = Mat("UtilPanel", new Color(0.62f, 0.64f, 0.62f), null, 0.4f, 0.3f);
            util[(int)UtilitySlot.Warning] = Mat("UtilWarning", new Color(0.95f, 0.75f, 0.1f), null, 0.3f);
            var led = Mat("UtilLed", new Color(0.35f, 1f, 0.55f), null, 0.5f, 0f, null, new Color(0.5f, 2.4f, 1f)); // a light switch's lamp
            BuildFittings(art, util, led); // the house's own fittings (switches, grates, the shaft) use the services' materials
            Link(art, ref art.CityPiece, piece);
            Link(art, ref art.CityLamp, lamp);
            LinkAll(art, ref art.CityMaterials, mats);
            art.CityMeshes = BuildCityMeshes();
            EditorUtility.SetDirty(art);
            LinkAll(art, ref art.UtilityMaterials, util);
        }

        /// <summary>
        /// The city's models (Art/Models/City, Tools/Blender/street.py) as the level builder merges them: each
        /// <see cref="CityPropKind"/>'s parts, the <see cref="CitySlot"/> named by each part (Livery / Pane are filled in per
        /// copy) and the part's place in the model. A model not exported yet is left out (its copies are not drawn).
        /// </summary>
        static CityMesh[] BuildCityMeshes()
        {
            var list = new List<CityMesh>();
            foreach (CityPropKind kind in System.Enum.GetValues(typeof(CityPropKind)))
            {
                string path;
                try { path = ModelPath(CityPlan.ModelFile(kind)); }
                catch (FileNotFoundException)
                {
                    Debug.LogWarning($"[Builder] {CityPlan.ModelFile(kind)}.fbx is missing: run Tools/Blender/street.py");
                    continue;
                }
                var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var meshes = new List<Mesh>();
                var slots = new List<CitySlot>();
                var locals = new List<Matrix4x4>();
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    meshes.Add(mf.sharedMesh);
                    // a model of one part comes in as that part at the root, named after the file: then its mesh has the name
                    slots.Add(CitySlotOf(path, mf.gameObject.name, mf.sharedMesh.name));
                    locals.Add(model.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix);
                }
                Object.DestroyImmediate(model);
                list.Add(new CityMesh { Kind = kind, Meshes = meshes.ToArray(), Slots = slots.ToArray(), Locals = locals.ToArray() });
            }
            return list.ToArray();
        }

        /// <summary>A part's slot from its object's or mesh's name ("Stone", "Livery.001", "Glass 1"); unknown names are drawn as metal.</summary>
        static CitySlot CitySlotOf(string path, params string[] names)
        {
            foreach (var full in names)
            {
                var name = full ?? "";
                int cut = name.IndexOfAny(new[] { '.', ' ', '_' });
                if (cut > 0) name = name.Substring(0, cut);
                if (System.Enum.TryParse<CitySlot>(name, false, out var slot) && System.Enum.IsDefined(typeof(CitySlot), slot)) return slot;
            }
            Debug.LogWarning($"[Builder] {path}: part {string.Join(" / ", names)} is not a CitySlot, drawn as Metal");
            return CitySlot.Metal;
        }
    }
}
