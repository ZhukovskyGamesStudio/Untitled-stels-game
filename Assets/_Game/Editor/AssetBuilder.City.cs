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
            if (art.CityPiece == piece && art.CityLamp == lamp && art.CityMaterials != null && art.CityMaterials.Length == mats.Length
                && System.Linq.Enumerable.SequenceEqual(art.CityMaterials, mats)) return;
            art.CityPiece = piece;
            art.CityLamp = lamp;
            art.CityMaterials = mats;
            EditorUtility.SetDirty(art);
        }
    }
}
