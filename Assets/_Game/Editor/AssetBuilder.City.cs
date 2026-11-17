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
            // a light switch beside a doorway (LightSwitch): a plate on the wall, a rocker that tilts, a small green lamp in it
            // that glows while the room is dark; +Z out of the wall, the pivot in the plate's middle on the wall's face
            var led = Mat("UtilLed", new Color(0.35f, 1f, 0.55f), null, 0.5f, 0f, null, new Color(0.5f, 2.4f, 1f));
            var lightSwitch = Prefab($"{LevelPrefabs}/LightSwitch.prefab", () =>
            {
                var go = new GameObject("LightSwitch");
                Cube(go.transform, "Plate", new Vector3(0f, 0f, 0.006f), new Vector3(0.085f, 0.125f, 0.012f), util[(int)UtilitySlot.Plate], collider: false);
                var rocker = new GameObject("Rocker").transform;
                rocker.SetParent(go.transform, false);
                rocker.localPosition = new Vector3(0f, 0f, 0.012f);
                Cube(rocker, "Key", new Vector3(0f, 0f, 0.005f), new Vector3(0.036f, 0.064f, 0.01f), util[(int)UtilitySlot.Plate], collider: false);
                var glow = Cube(rocker, "Led", new Vector3(0f, -0.022f, 0.0105f), new Vector3(0.008f, 0.008f, 0.002f), led, collider: false);
                var s = go.AddComponent<LightSwitch>();
                s.Rocker = rocker;
                s.Led = glow.GetComponent<MeshRenderer>();
                return go;
            });
            if (art.LightSwitch != lightSwitch) { art.LightSwitch = lightSwitch; EditorUtility.SetDirty(art); }
            // a grate in a room's ceiling (VentGrate, the way into the crawl ducts): a frame in the ceiling board and a grille
            // on a hinge along one edge that swings down while a player is near; the pivot in the frame's middle on the ceiling
            var ventGrate = Prefab($"{LevelPrefabs}/VentGrate.prefab", () =>
            {
                var go = new GameObject("VentGrate");
                float size = VentPlan.Res, t = LevelMetrics.CeilingBoard;
                foreach (var (c, sz) in new[]
                         {
                             (new Vector3(0f, t / 2f, -size / 2f + 0.03f), new Vector3(size, t, 0.06f)), (new Vector3(0f, t / 2f, size / 2f - 0.03f), new Vector3(size, t, 0.06f)),
                             (new Vector3(-size / 2f + 0.03f, t / 2f, 0f), new Vector3(0.06f, t, size)), (new Vector3(size / 2f - 0.03f, t / 2f, 0f), new Vector3(0.06f, t, size)),
                         })
                    Cube(go.transform, "Frame", c, sz, util[(int)UtilitySlot.Panel], collider: false);
                var lid = new GameObject("Lid").transform;
                lid.SetParent(go.transform, false);
                lid.localPosition = new Vector3(0f, 0.01f, -size / 2f + 0.06f);
                for (int i = 0; i < 9; i++)
                    Cube(lid, "Slat", new Vector3(0f, 0f, 0.05f + i * 0.1f), new Vector3(size - 0.14f, 0.02f, 0.035f), util[(int)UtilitySlot.Dark], collider: false);
                Cube(lid, "Rail", new Vector3(-size / 2f + 0.1f, 0f, (size - 0.12f) / 2f), new Vector3(0.03f, 0.025f, size - 0.12f), util[(int)UtilitySlot.Panel], collider: false);
                Cube(lid, "Rail", new Vector3(size / 2f - 0.1f, 0f, (size - 0.12f) / 2f), new Vector3(0.03f, 0.025f, size - 0.12f), util[(int)UtilitySlot.Panel], collider: false);
                var floor = go.AddComponent<BoxCollider>(); // walked on in the duct; from below the grate is climbed with E
                floor.center = new Vector3(0f, t / 2f, 0f);
                floor.size = new Vector3(size - 0.04f, t, size - 0.04f);
                go.AddComponent<KeepUnmerged>(); // the grille swings open
                var g = go.AddComponent<VentGrate>();
                g.Lid = lid;
                return go;
            });
            if (art.VentGrate != ventGrate) { art.VentGrate = ventGrate; EditorUtility.SetDirty(art); }
            // the lid over the shaft to the basement (ShaftHatch): a steel frame in the floor, a ribbed lid on a hinge at −Z
            // with a yellow rim, and the ladder's top under it (ShaftLadder); the pivot on the floor in the shaft's middle
            var shaftHatch = Prefab($"{LevelPrefabs}/ShaftHatch.prefab", () =>
            {
                var go = new GameObject("ShaftHatch");
                float half = Basement.ShaftHalf + 0.1f;
                foreach (var (c, sz) in new[]
                         {
                             (new Vector3(0f, 0.01f, -half), new Vector3(2 * half + 0.12f, 0.03f, 0.12f)), (new Vector3(0f, 0.01f, half), new Vector3(2 * half + 0.12f, 0.03f, 0.12f)),
                             (new Vector3(-half, 0.01f, 0f), new Vector3(0.12f, 0.03f, 2 * half)), (new Vector3(half, 0.01f, 0f), new Vector3(0.12f, 0.03f, 2 * half)),
                         })
                    Cube(go.transform, "Frame", c, sz, util[(int)UtilitySlot.Warning], collider: false);
                var lid = new GameObject("Lid").transform;
                lid.SetParent(go.transform, false);
                lid.localPosition = new Vector3(0f, 0.03f, -half);
                var plate = Cube(lid, "Plate", new Vector3(0f, 0.02f, half), new Vector3(2 * half - 0.04f, 0.05f, 2 * half - 0.04f), util[(int)UtilitySlot.Panel], collider: true);
                for (int i = 0; i < 5; i++)
                    Cube(lid, "Rib", new Vector3(0f, 0.055f, 0.2f + i * (2 * half - 0.4f) / 4f), new Vector3(2 * half - 0.2f, 0.02f, 0.04f), util[(int)UtilitySlot.Dark], collider: false);
                Cube(lid, "Handle", new Vector3(0f, 0.08f, 2 * half - 0.12f), new Vector3(0.3f, 0.04f, 0.04f), util[(int)UtilitySlot.Warning], collider: false);
                go.AddComponent<KeepUnmerged>(); // the lid moves: never baked into the furniture's merged mesh (it would stay shut there)
                var h = go.AddComponent<ShaftHatch>();
                h.Lid = lid;
                h.LidCollider = plate.GetComponent<Collider>();
                var top = new GameObject("LadderTop");
                top.transform.SetParent(go.transform, false);
                top.AddComponent<ShaftLadder>().Top = true;
                return go;
            });
            var shaftLadder = Prefab($"{LevelPrefabs}/ShaftLadder.prefab", () =>
            {
                var go = new GameObject("ShaftLadder");
                go.AddComponent<ShaftLadder>();
                return go;
            });
            if (art.ShaftHatch != shaftHatch || art.ShaftLadder != shaftLadder) { art.ShaftHatch = shaftHatch; art.ShaftLadder = shaftLadder; EditorUtility.SetDirty(art); }
            if (art.CityPiece == piece && art.CityLamp == lamp && art.CityMaterials != null && art.CityMaterials.Length == mats.Length
                && System.Linq.Enumerable.SequenceEqual(art.CityMaterials, mats) && art.UtilityMaterials != null
                && System.Linq.Enumerable.SequenceEqual(art.UtilityMaterials, util)) return;
            art.CityPiece = piece;
            art.CityLamp = lamp;
            art.CityMaterials = mats;
            art.UtilityMaterials = util;
            EditorUtility.SetDirty(art);
        }
    }
}
