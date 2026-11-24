using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stealth.Configs;
using Stealth.Items;
using Stealth.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Stealth.EditorTools
{
    /// <summary>
    /// A furnished room under the game's own light and grade (the style's ambient, tone curve and exposure, ceiling panels),
    /// without the game: what a house's surfaces look like where they are seen → Logs/surfaces/room_*.png. Menu Stealth →
    /// Surface Room Pictures, or in batch with graphics: <c>-executeMethod Stealth.EditorTools.SurfaceRoom.Batch</c>
    /// (TAG=name puts a tag on the files, to set two states side by side).
    /// </summary>
    public static class SurfaceRoom
    {
        [MenuItem("Stealth/Surface Room Pictures")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var style = config.Default;
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "surfaces");
            Directory.CreateDirectory(dir);
            string tag = System.Environment.GetEnvironmentVariable("TAG");
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = style.World.Ambient;
            RenderSettings.reflectionIntensity = 0f; // as in the game
            static Material Lit(Color c, float smooth, Color? glow = null)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                if (glow.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", glow.Value);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                return m;
            }
            static void Box(Vector3 at, Vector3 size, Material m)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.transform.position = at;
                g.transform.localScale = size;
                g.GetComponent<Renderer>().sharedMaterial = m;
            }
            // the walls and the ceiling of an office: quads with UVs in metres, as the game's finishes have them
            var faces = new List<(MeshRenderer r, string kit, Color flat)>();
            void Face(Vector3 at, Vector3 normal, float fw, float fh, string kit, Color flat)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                g.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-normal));
                g.transform.localScale = new Vector3(fw, fh, 1f);
                var mesh = Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);
                mesh.uv = mesh.uv.Select(q => new Vector2(q.x * fw, q.y * fh)).ToArray();
                g.GetComponent<MeshFilter>().sharedMesh = mesh;
                faces.Add((g.GetComponent<MeshRenderer>(), kit, flat));
            }
            var beige = new Color(0.62f, 0.55f, 0.47f);
            Face(new Vector3(0f, 1.375f, -2.94f), Vector3.forward, 7f, 2.75f, "Real_BeigeWall", beige);
            Face(new Vector3(0f, 1.375f, 2.94f), Vector3.back, 7f, 2.75f, "Real_BeigeWall", beige);
            Face(new Vector3(-3.44f, 1.375f, 0f), Vector3.right, 6f, 2.75f, "Real_BeigeWall", beige);
            Face(new Vector3(3.44f, 1.375f, 0f), Vector3.left, 6f, 2.75f, "Real_BeigeWall", beige);
            Face(new Vector3(0f, 2.75f, 0f), Vector3.down, 7f, 6f, "Real_CeilingTiles", new Color(0.48f, 0.43f, 0.4f));
            var wall = Lit(new Color(0.55f, 0.53f, 0.49f), 0.15f);
            Box(new Vector3(0f, -0.05f, 0f), new Vector3(7f, 0.1f, 6f), AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Styles/Kit/Real_Laminate.mat"));
            Box(new Vector3(0f, 3.05f, 0f), new Vector3(7f, 0.1f, 6f), Lit(new Color(0.7f, 0.7f, 0.68f), 0.1f));
            Box(new Vector3(0f, 1.5f, -3f), new Vector3(7f, 3f, 0.1f), wall);
            Box(new Vector3(0f, 1.5f, 3f), new Vector3(7f, 3f, 0.1f), wall);
            Box(new Vector3(-3.5f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), wall);
            Box(new Vector3(3.5f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), wall);
            var warm = new Color(1f, 0.95f, 0.88f);
            foreach (float x in new[] { -1.6f, 1.6f })
                foreach (float z in new[] { -1.2f, 1.4f })
                {
                    Box(new Vector3(x, 2.73f, z), new Vector3(1.2f, 0.04f, 0.3f), Lit(Color.white, 0.1f, warm * 2.5f));
                    var l = new GameObject("Lamp").AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 160f;
                    l.innerSpotAngle = 60f;
                    l.range = 9f;
                    l.intensity = 8f;
                    l.color = warm;
                    l.shadows = LightShadows.Soft;
                    l.transform.SetPositionAndRotation(new Vector3(x, 2.65f, z), Quaternion.Euler(90f, 0f, 0f));
                }
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            profile.Add<ColorAdjustments>(true).postExposure.Override(style.Grade.Exposure);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(style.Grade.BloomIntensity);
            bloom.threshold.Override(style.Grade.BloomThreshold);
            VisualStyles.Reflect(profile, style.Grade.Reflections);
            // BOUNCE=0: without the bounced light, to set the two side by side
            VisualStyles.Bounce(float.TryParse(System.Environment.GetEnvironmentVariable("BOUNCE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float bounce) ? bounce : style.Grade.Bounce, style.Grade.BounceReach);
            var volume = new GameObject("Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);

            if (System.Environment.GetEnvironmentVariable("DARK") == "1") // the lamps off, no fill: the picture must be black, reflections or not
            {
                foreach (var l in Object.FindObjectsByType<Light>()) l.enabled = false;
                RenderSettings.ambientLight = Color.black;
                foreach (var r in Object.FindObjectsByType<MeshRenderer>())
                    if (r.sharedMaterial.IsKeywordEnabled("_EMISSION")) r.sharedMaterial.DisableKeyword("_EMISSION");
                foreach (var p in new[] { "VendingMachine", "MirrorSink", "Lockers" })
                {
                    var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{p}.prefab"), new Vector3(p.Length - 11f, 0f, -2.2f), Quaternion.identity);
                    foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
                    SurfaceSheet.Dress(go, config, style, config.LookOf(HouseKind.Office), true);
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                        if (r.name.StartsWith("Eye") || r.sharedMaterial.IsKeywordEnabled("_EMISSION")) r.enabled = false; // (what glows of itself: a machine's lit front)
                }
                cam.transform.position = new Vector3(0f, 1.6f, 2f);
                cam.transform.LookAt(new Vector3(0f, 1f, -2f));
                for (int i = 0; i < 6; i++) cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                RenderTexture.active = null;
                Debug.Log($"[Surfaces] dark room: brightest pixel {tex.GetPixels().Max(c => c.maxColorComponent):0.000}");
                return;
            }

            foreach (var (house, kind) in new[] { ("office", HouseKind.Office), ("facility", HouseKind.Abandoned) })
                foreach (bool after in new[] { false, true })
                {
                    var made = new List<GameObject>();
                    void Put(string p, float x, float z, float yaw)
                    {
                        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{p}.prefab"));
                        foreach (var v in go.GetComponentsInChildren<PropVariants>(true))
                        {
                            v.Dress(7);
                            foreach (var r in v.Own) if (r != null) r.enabled = false;
                        }
                        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
                        SurfaceSheet.Dress(go, config, style, config.LookOf(kind), after);
                        go.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
                        made.Add(go);
                    }
                    Put("FilingCabinet", -1.9f, -2.5f, 0f);
                    Put("Credenza", -0.4f, -2.5f, 0f);
                    Put("MirrorSink", 1.1f, -2.5f, 0f);
                    Put("VendingMachine", 2.4f, -2.4f, 0f);
                    Put("ExecutiveDesk", -0.9f, -0.9f, 180f);
                    Put("GuestChair", 0.6f, -1.2f, 160f);
                    Put("StandingDesk", 2.3f, -0.6f, -60f);
                    Put("Lockers", -2.9f, -0.6f, 90f);
                    Put("SectionalSofa", -1.9f, 1.6f, 120f);
                    Put("TrashBin", 0.2f, -2.4f, 0f);
                    Put("Cabinet", 2.4f, 2.5f, 180f);
                    Put("WaterCooler", 1.4f, 2.6f, 180f);
                    Put("SynthStand", -0.4f, 2.3f, 180f);
                    Put("NoticeBoard", 0.3f, -2.91f, 0f);
                    foreach (var (r, kit, flat) in faces) // (before: the old paint was one flat colour, like this)
                        r.sharedMaterial = after ? AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Game/Data/Styles/Kit/{kit}.mat") : Lit(flat, 0.35f);
                    var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");
                    void Item(int shape, float x, float y, float z, float yaw)
                    {
                        var go = Object.Instantiate(visuals.Models[shape]);
                        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
                        if (after) SurfaceSheet.Dress(go, config, style, null, true);
                        go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
                        made.Add(go);
                    }
                    Item(ItemCatalog.Stun, -1.3f, 0.77f, -0.85f, 20f);
                    Item(ItemCatalog.Radio + 3, -0.75f, 0.77f, -0.85f, -20f);
                    Item(ItemCatalog.Flashlight, -0.35f, 0.77f, -0.75f, 40f);
                    Item(ItemCatalog.Cans, -0.4f, 0.77f, -1f, 10f);
                    foreach (var (view, at, look, fov) in new[]
                    {
                        ("room", new Vector3(1.4f, 1.6f, 2.5f), new Vector3(-0.1f, 0.8f, -1.6f), 60f),
                        ("desk", new Vector3(-0.35f, 1.5f, 0.2f), new Vector3(-0.85f, 0.75f, -0.95f), 50f),
                        ("items", new Vector3(-0.85f, 1.2f, -0.35f), new Vector3(-0.9f, 0.8f, -0.9f), 50f),
                        ("lockers", new Vector3(-1.3f, 1.5f, 0.4f), new Vector3(-2.9f, 0.95f, -0.5f), 60f),
                        ("wall", new Vector3(1.0f, 1.5f, -0.9f), new Vector3(1.5f, 1.0f, -2.6f), 55f),
                        ("corner", new Vector3(0.4f, 1.6f, 0.6f), new Vector3(2.6f, 1.2f, 2.9f), 65f),
                        ("board", new Vector3(0.5f, 1.6f, -1.6f), new Vector3(0.3f, 1.5f, -2.9f), 55f),
                        ("stand", new Vector3(0.3f, 1.5f, 1.2f), new Vector3(-0.4f, 0.75f, 2.4f), 50f),
                        ("flash", new Vector3(1.2f, 1.6f, 0.9f), new Vector3(2.2f, 0.9f, 2.6f), 60f),
                    })
                    {
                        cam.transform.position = at;
                        cam.transform.LookAt(look);
                        cam.fieldOfView = fov;
                        bool flash = view == "flash"; // the lamps off, the player's flashlight on
                        var lamps = Object.FindObjectsByType<Light>().Where(l => l.name == "Lamp").ToList();
                        foreach (var l in lamps) l.enabled = !flash;
                        Light beam = null;
                        if (flash)
                        {
                            beam = new GameObject("Beam").AddComponent<Light>();
                            beam.type = LightType.Spot;
                            beam.spotAngle = 55f;
                            beam.innerSpotAngle = 16f;
                            beam.range = 18f;
                            beam.intensity = 8f;
                            beam.color = new Color(1f, 0.93f, 0.8f);
                            beam.shadows = LightShadows.Soft;
                            beam.shadowBias = 0.03f;
                            beam.shadowNormalBias = 0.2f;
                            var data = beam.gameObject.AddComponent<UniversalAdditionalLightData>();
                            var so = new SerializedObject(data);
                            so.FindProperty("m_UsePipelineSettings").boolValue = false;
                            so.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = after ? 2 : 1;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            beam.transform.SetPositionAndRotation(cam.transform.TransformPoint(0.15f, -0.19f, 0.48f), cam.transform.rotation);
                        }
                        for (int i = 0; i < 4; i++) cam.Render();
                        if (beam != null) Object.DestroyImmediate(beam.gameObject);
                        foreach (var l in lamps) l.enabled = true;
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(dir, $"room_{house}_{view}_{(after ? "after" : "before")}{(string.IsNullOrEmpty(tag) ? "" : "_" + tag)}.png"), tex.EncodeToPNG());
                    }
                    foreach (var go in made) Object.DestroyImmediate(go);
                }
            Debug.Log("[Surfaces] the room's pictures in Logs/surfaces");
        }

        /// <summary>For the command line (with graphics).</summary>
        public static void Batch()
        {
            try { Render(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
