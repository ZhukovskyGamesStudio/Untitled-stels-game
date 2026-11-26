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
            var steps = Steps();
            while (steps.MoveNext()) { }
        }

        /// <summary>The work in steps: a reflection probe is taken up by the engine only on its next tick (the batch run waits one, the menu does not).</summary>
        static System.Collections.IEnumerator Steps()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var style = config.Default;
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "surfaces");
            Directory.CreateDirectory(dir);
            string tag = System.Environment.GetEnvironmentVariable("TAG");
            // SKIP=1 / 2: the texture size setting (half, a quarter), to see what it costs the picture (the editor saves
            // the limit into ProjectSettings/QualitySettings.asset: revert that file after a run with SKIP)
            int skip = (int)Env("SKIP", 0f);
            QualitySettings.globalTextureMipmapLimit = skip;
            Stealth.Level.ShapeLibrary.Skip = skip;
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
            static Material Kit(string kit)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Game/Data/Styles/Kit/{kit}.mat");
                if (System.Environment.GetEnvironmentVariable("PARALLAX") != "0") return m;
                m = new Material(m);
                m.DisableKeyword("_PARALLAXMAP");
                return m;
            }
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
            // WALL=Real_Brick: the walls in another finish; PARALLAX=0: its relief without depth, to set the two side by side
            string wallKit = System.Environment.GetEnvironmentVariable("WALL") ?? "Real_BeigeWall";
            Face(new Vector3(0f, 1.375f, -2.94f), Vector3.forward, 7f, 2.75f, wallKit, beige);
            Face(new Vector3(0f, 1.375f, 2.94f), Vector3.back, 7f, 2.75f, wallKit, beige);
            Face(new Vector3(-3.44f, 1.375f, 0f), Vector3.right, 6f, 2.75f, wallKit, beige);
            Face(new Vector3(3.44f, 1.375f, 0f), Vector3.left, 6f, 2.75f, wallKit, beige);
            Face(new Vector3(0f, 2.75f, 0f), Vector3.down, 7f, 6f, "Real_CeilingTiles", new Color(0.48f, 0.43f, 0.4f));
            var wall = Lit(new Color(0.55f, 0.53f, 0.49f), 0.15f);
            Box(new Vector3(0f, -0.05f, 0f), new Vector3(7f, 0.1f, 6f), AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Data/Styles/Kit/Real_Laminate.mat"));
            Box(new Vector3(0f, 3.05f, 0f), new Vector3(7f, 0.1f, 6f), Lit(new Color(0.7f, 0.7f, 0.68f), 0.1f));
            Box(new Vector3(0f, 1.5f, -3f), new Vector3(7f, 3f, 0.1f), wall);
            Box(new Vector3(0f, 1.5f, 3f), new Vector3(7f, 3f, 0.1f), wall);
            Box(new Vector3(-3.5f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), wall);
            Box(new Vector3(3.5f, 1.5f, 0f), new Vector3(0.1f, 3f, 6f), wall);
            var warm = new Color(1f, 0.95f, 0.88f);
            // LAMPS=0.15: the room dim (a dim room of the game: what is left when the lamps are not what one sees by)
            float dim = float.TryParse(System.Environment.GetEnvironmentVariable("LAMPS"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lampScale) ? lampScale : 1f;
            // REAL=1: the game's own ceiling lamps (two of the prefab, as a room this size has) with the style's look of them —
            // POOL / SCALE / GLOW override the style's LampPool / LightScale / CeilingGlow; without it, four plain spots
            bool real = System.Environment.GetEnvironmentVariable("REAL") == "1";
            if (real)
            {
                LightSource.CeilingGlow = Env("GLOW", style.World.CeilingGlow);
                LightSource.LampPool = Env("POOL", style.World.LampPool);
                LightSource.GlowScale = style.World.LampGlow >= 0f ? style.World.LampGlow : 1f;
                // (no LateUpdate here: the lamp's share for the eye — a pooled lamp's boost — is applied by hand)
                float scale = Env("SCALE", style.World.LightScale) * (LightSource.LampPool > 0f ? Mathf.Lerp(1f, LightMath.PoolBoost, Mathf.Clamp01(LightSource.LampPool)) : 1f);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Level/RoomLight.prefab");
                foreach (float x in new[] { -1.7f, 1.7f })
                {
                    var lamp = Object.Instantiate(prefab, new Vector3(x, 2.75f, 0.1f), Quaternion.identity);
                    lamp.GetComponent<RoomLight>().Init(warm, LightMath.RoomIntensity * dim * scale, false, 0f);
                }
            }
            else
            foreach (float x in new[] { -1.6f, 1.6f })
                foreach (float z in new[] { -1.2f, 1.4f })
                {
                    Box(new Vector3(x, 2.73f, z), new Vector3(1.2f, 0.04f, 0.3f), Lit(Color.white, 0.1f, warm * (2.5f * dim)));
                    var l = new GameObject("Lamp").AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 160f;
                    l.innerSpotAngle = 60f;
                    l.range = 9f;
                    l.intensity = 8f * dim;
                    l.color = warm;
                    l.shadows = LightShadows.Soft;
                    l.transform.SetPositionAndRotation(new Vector3(x, 2.65f, z), Quaternion.Euler(90f, 0f, 0f));
                }
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            // the style's own grade (TONE=aces|neutral, EXPOSURE, CONTRAST, SATURATION override it; without REAL the tool's
            // old neutral curve with nothing else, as its earlier pictures were made)
            var grade = style.Grade;
            string tone = System.Environment.GetEnvironmentVariable("TONE") ?? (real && grade.Tonemap == StyleTonemap.Aces ? "aces" : "neutral");
            profile.Add<Tonemapping>(true).mode.Override(tone == "aces" ? TonemappingMode.ACES : TonemappingMode.Neutral);
            var adjust = profile.Add<ColorAdjustments>(true);
            adjust.postExposure.Override(Env("EXPOSURE", grade.Exposure));
            adjust.contrast.Override(Env("CONTRAST", real ? grade.Contrast : 0f));
            adjust.saturation.Override(Env("SATURATION", real ? grade.Saturation : 0f));
            if (real)
            {
                var toning = profile.Add<SplitToning>(true);
                toning.shadows.Override(grade.SplitShadows);
                toning.highlights.Override(grade.SplitHighlights);
                toning.balance.Override(grade.SplitBalance);
                profile.Add<WhiteBalance>(true).temperature.Override(grade.Temperature);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = style.World.Ambient * Env("AMBIENT", 1f);
            }
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(style.Grade.BloomIntensity);
            bloom.threshold.Override(style.Grade.BloomThreshold);
            VisualStyles.Reflect(profile, style.Grade.Reflections);
            // BOUNCE=0: without the bounced light, to set the two side by side
            VisualStyles.Bounce(float.TryParse(System.Environment.GetEnvironmentVariable("BOUNCE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float bounce) ? bounce : style.Grade.Bounce, style.Grade.BounceReach);
            // the room's fill, as a house would give it: one room of the plan under these four lamps (FILL=0: without)
            RoomFillField.Strength = float.TryParse(System.Environment.GetEnvironmentVariable("FILL"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fill) ? fill : style.Grade.RoomFill;
            // CONTACT=0: without the contact shadows, to set the two side by side
            ContactShadows.Strength = float.TryParse(System.Environment.GetEnvironmentVariable("CONTACT"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float contact) ? contact : style.Grade.Contact;
            ContactShadows.Show(Object.FindObjectsByType<Light>().Where(l => l.name == "Lamp").Select(l => (l.transform.position, 1f)).ToList(), Vector3.zero);
            var plan = new HousePlan(1, 14, 12);
            for (int cz = 0; cz < 12; cz++) for (int cx = 0; cx < 14; cx++) plan.Set(0, cx, cz, CellKind.Room, 0);
            plan.Rooms.Add(new PlanRoom { Id = 0, Area = 42f });
            bool lampsOff = System.Environment.GetEnvironmentVariable("DARK") == "1";
            // (a room this size has two ceiling lamps in the game)
            using var field = RoomFillField.Show(plan, new[] { RoomFill.Level(lampsOff ? Color.black : warm.linear * (2f * LightMath.RoomIntensity * dim), 42f, 1f) }, new Vector2(-3.5f, -3f));
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
                yield break;
            }

            if (System.Environment.GetEnvironmentVariable("DEVICES") == "1") // the devices with no eye and no ear: armed, tripped, hacked — side by side
            {
                foreach (var (r, kit, _) in faces) r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Game/Data/Styles/Kit/{kit}.mat");
                var idleOf = typeof(Enemies.SecurityDevice).GetProperty("IdleColor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                foreach (var (name, path, y, flip) in new[]
                {
                    ("plate", "Sensors/SensorPlate", 0f, false), ("lidar", "Sensors/SensorLidar", 0f, false),
                    ("vacuum", "Robots/RobotVacuum", 1.6f, false), ("spider", "Robots/RobotSpider", 0f, false),
                })
                {
                    var made = new List<GameObject>();
                    for (int state = 0; state < 3; state++)
                    {
                        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/{path}.prefab"));
                        SurfaceSheet.Dress(go, config, style, null, true);
                        go.transform.position = new Vector3(-1.6f + state * 1.6f, y, -0.6f);
                        var d = go.GetComponent<Enemies.Detector>();
                        var idle = (Color)idleOf.GetValue(d);
                        var colour = state == 0 ? idle : state == 1 ? Enemies.SecurityPalette.Alarm : Enemies.SecurityPalette.Hacked;
                        d.Status.Apply(colour, state == 0, d.GlowEyes, d.GlowEars);
                        if (!d.GlowEyes && !d.GlowEars && d.StateLine > 0f) d.Status.Line(colour, state == 2 ? 0f : d.StateLine);
                        foreach (var l in go.GetComponentsInChildren<LineRenderer>(true)) l.enabled = false;
                        made.Add(go);
                    }
                    foreach (var (view, at, look) in new[]
                    {
                        ("far", new Vector3(0f, 1.6f, 2.7f), new Vector3(0f, y + 0.2f, -0.6f)),
                        ("near", new Vector3(-1.6f, y + 1.1f, 0.7f), new Vector3(-0.8f, y + 0.1f, -0.6f)),
                    })
                    {
                        cam.transform.position = at;
                        cam.transform.LookAt(look);
                        cam.fieldOfView = 50f;
                        for (int i = 0; i < 4; i++) cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(dir, $"device_{name}_{view}{(string.IsNullOrEmpty(tag) ? "" : "_" + tag)}.png"), tex.EncodeToPNG());
                    }
                    foreach (var go in made) Object.DestroyImmediate(go);
                }
                Debug.Log("[Surfaces] the devices' pictures in Logs/surfaces");
                yield break;
            }

            // PROBE=1: the room's own reflections, as a house gives them (RoomProbe) — a cube picture of the room from
            // its middle, box-projected onto its walls
            ReflectionProbe roomProbe = null;
            if (System.Environment.GetEnvironmentVariable("PROBE") == "1")
            {
                QualitySettings.realtimeReflectionProbes = true;
                var pgo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Level/RoomProbe.prefab"));
                roomProbe = pgo.GetComponent<ReflectionProbe>();
                pgo.transform.position = new Vector3(0f, 1.5f, 0f);
                roomProbe.center = Vector3.zero;
                roomProbe.size = new Vector3(7f, 3f, 6f);
                yield return null;
            }

            // VIEWS=cooler,cabinet: only these frames; HOUSE=office: only this house, dressed
            string[] only = System.Environment.GetEnvironmentVariable("VIEWS")?.Split(',');
            string onlyHouse = System.Environment.GetEnvironmentVariable("HOUSE");
            bool split = System.Environment.GetEnvironmentVariable("SPLIT") == "1";
            foreach (var (house, kind) in new[] { ("office", HouseKind.Office), ("facility", HouseKind.Abandoned) })
                foreach (bool after in new[] { false, true })
                {
                    if (onlyHouse != null && (house != onlyHouse || !after)) continue;
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
                        // SHAPES=0: without the models' own baked maps (ShapeMaps), to set the two side by side
                        if (System.Environment.GetEnvironmentVariable("SHAPES") != "0")
                            ShapeMaps.Tag(go, config.Shapes);
                        go.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
                        made.Add(go);
                    }
                    Put("FilingCabinet", -1.9f, -2.5f, 0f);
                    // the view "drawer": its drawers pulled out, as a player leaves them searching
                    if (only != null && only.Contains("drawer"))
                        foreach (var tr in made[made.Count - 1].GetComponentsInChildren<Transform>(true))
                            if (tr.name.StartsWith("BodyDrawer") && tr.parent != null && !tr.parent.name.StartsWith("BodyDrawer")) tr.position += tr.root.forward * 0.4f;
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
                        r.sharedMaterial = after ? Kit(kit) : Lit(flat, 0.35f);
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
                    if (roomProbe != null)
                    {
                        int id = roomProbe.RenderProbe();
                        for (int t = 0; t < 200 && !roomProbe.IsFinishedRendering(id); t++) yield return null;
                        Debug.Log($"[Surfaces] probe rendered: {roomProbe.IsFinishedRendering(id)}, texture {roomProbe.texture}");
                        yield return null;
                    }
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
                        ("cooler", new Vector3(1.05f, 1.25f, 1.8f), new Vector3(1.4f, 1.0f, 2.6f), 40f),
                        ("cabinet", new Vector3(-1.45f, 1.3f, -1.5f), new Vector3(-1.9f, 0.7f, -2.4f), 50f),
                        ("drawer", new Vector3(-1.8f, 1.95f, -1.55f), new Vector3(-1.9f, 0.95f, -2.25f), 60f),
                    })
                    {
                        if (only != null && !only.Contains(view)) continue;
                        cam.transform.position = at;
                        cam.transform.LookAt(look);
                        cam.fieldOfView = fov;
                        bool flash = view is "flash" or "drawer" || Env("FLASH", 0f) > 0f; // the lamps off, the player's flashlight on (FLASH=1: in every view)
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
                            beam.shadowBias = Env("BIAS", 0.03f);          // BIAS / NBIAS: to try other shadow biases
                            beam.shadowNormalBias = Env("NBIAS", 0.2f);
                            var data = beam.gameObject.AddComponent<UniversalAdditionalLightData>();
                            var so = new SerializedObject(data);
                            so.FindProperty("m_UsePipelineSettings").boolValue = false;
                            so.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = (int)Env("TIER", after ? 2 : 1); // (TIER=1: the game's flashlight)
                            so.ApplyModifiedPropertiesWithoutUndo();
                            beam.transform.SetPositionAndRotation(cam.transform.TransformPoint(0.15f, -0.19f, 0.48f), cam.transform.rotation);
                        }
                        // SPLIT=1: the same frame with and without the room's reflections and the contact shadows, to tell whose a flaw is
                        float contactWas = ContactShadows.Strength, probeWas = roomProbe != null ? roomProbe.intensity : 0f;
                        foreach (var (part, probes, contacts) in split
                            ? new[] { ("", true, true), ("_noprobe", false, true), ("_nocontact", true, false), ("_neither", false, false) }
                            : new[] { ("", true, true) })
                        {
                            if (roomProbe != null)
                            {
                                roomProbe.intensity = probes ? probeWas : 0f;
                                yield return null; // (the engine takes a probe's change up on its next tick)
                            }
                            ContactShadows.Strength = contacts ? contactWas : 0f;
                            // AA=none|smaa|taa, FXAA=0, BACK=metres (the same view from further away), FRAMES=n (frames before the
                            // picture: the temporal filter needs its history), STEP=metres (the camera walks sideways this much a
                            // frame), PAIR=1 (the frame before the last one too: what changes between two frames)
                            string aa = System.Environment.GetEnvironmentVariable("AA") ?? "none";
                            var camData = cam.GetUniversalAdditionalCameraData();
                            camData.antialiasing = aa == "taa" ? AntialiasingMode.TemporalAntiAliasing : aa == "smaa" ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
                            camData.antialiasingQuality = AntialiasingQuality.High;
                            if (aa == "taa")
                            {
                                var tuning = new Stealth.Player.SmoothingSettings();
                                camData.taaSettings.quality = (TemporalAAQuality)tuning.Quality;
                                camData.taaSettings.baseBlendFactor = tuning.History;
                                camData.taaSettings.varianceClampScale = tuning.Clamp;
                                camData.taaSettings.contrastAdaptiveSharpening = Env("SHARPEN", tuning.Sharpen);
                            }
                            Shader.SetGlobalFloat("_FxaaOff", System.Environment.GetEnvironmentVariable("FXAA") == "0" ? 1f : 0f);
                            cam.transform.position -= cam.transform.forward * Env("BACK", 0f);
                            int frames = (int)Env("FRAMES", 4f);
                            float step = Env("STEP", 0f);
                            bool pair = System.Environment.GetEnvironmentVariable("PAIR") == "1";
                            var taaField = typeof(UniversalAdditionalCameraData).GetField("m_TaaSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            int stalled = 0, counterWas = Time.frameCount;
                            for (int i = 0; i < frames; i++)
                            {
                                if (aa == "taa" || step != 0f)
                                {
                                    // (the filter steps its shake and its "frame before" by the engine's frame counter, which stands
                                    // still between the editor's ticks: a player-loop update is asked for, and where the counter still
                                    // did not move, the shake is stepped by hand)
                                    int counter = Time.frameCount;
                                    EditorApplication.QueuePlayerLoopUpdate();
                                    yield return null;
                                    if (Time.frameCount == counter) stalled++;
                                    object boxed = taaField.GetValue(camData);
                                    boxed.GetType().GetField("jitterFrameCountOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(boxed, stalled);
                                    taaField.SetValue(camData, boxed);
                                }
                                if (i > 0) cam.transform.position += cam.transform.right * step;
                                cam.Render();
                                if (pair && i == frames - 2)
                                {
                                    RenderTexture.active = rt;
                                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                                    tex.Apply();
                                    RenderTexture.active = null;
                                    File.WriteAllBytes(Path.Combine(dir, $"room_{house}_{view}_{(after ? "after" : "before")}{part}{(string.IsNullOrEmpty(tag) ? "" : "_" + tag)}_prev.png"), tex.EncodeToPNG());
                                }
                            }
                            if (frames > 4) Debug.Log($"[Surfaces] {view} {aa}: the frame counter moved {Time.frameCount - counterWas} times in {frames} frames");
                            RenderTexture.active = rt;
                            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                            tex.Apply();
                            RenderTexture.active = null;
                            File.WriteAllBytes(Path.Combine(dir, $"room_{house}_{view}_{(after ? "after" : "before")}{part}{(string.IsNullOrEmpty(tag) ? "" : "_" + tag)}.png"), tex.EncodeToPNG());
                        }
                        if (roomProbe != null) roomProbe.intensity = probeWas;
                        ContactShadows.Strength = contactWas;
                        if (beam != null) Object.DestroyImmediate(beam.gameObject);
                        foreach (var l in lamps) l.enabled = true;
                    }
                    foreach (var go in made) Object.DestroyImmediate(go);
                }
            Debug.Log("[Surfaces] the room's pictures in Logs/surfaces");
        }

        static float Env(string name, float fallback) =>
            float.TryParse(System.Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

        /// <summary>For the command line (with graphics).</summary>
        public static void Batch()
        {
            var steps = Steps(); // (run without -quit: it leaves by itself)
            EditorApplication.update += () =>
            {
                try { if (!steps.MoveNext()) EditorApplication.Exit(0); }
                catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
            };
        }
    }
}
