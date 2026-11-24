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

namespace Stealth.EditorTools
{
    /// <summary>
    /// Pictures of what things are made of, without the game: rows of furniture as the house's look dresses them (models of
    /// the style, its surfaces) beside the models' own materials, and the items → Logs/surfaces/&lt;row&gt;_&lt;before|after&gt;.png.
    /// Menu Stealth → Surface Pictures, or in batch with graphics: <c>-executeMethod Stealth.EditorTools.SurfaceSheet.Batch</c>.
    /// </summary>
    public static class SurfaceSheet
    {
        static readonly (string name, HouseKind house, string[] props)[] Rows =
        {
            ("office_desks", HouseKind.Office, new[] { "Desk", "Chair", "StandingDesk", "ExecutiveDesk", "GuestChair", "MeetingTable", "OperatorChair" }),
            ("office_storage", HouseKind.Office, new[] { "Cabinet", "FilingCabinet", "FilePedestal", "Credenza", "TallBookcase", "MagazineRack", "SideTable", "ReadingTable" }),
            ("office_machines", HouseKind.Office, new[] { "Printer", "Copier", "WaterCooler", "VendingMachine", "RecyclingBins", "Whiteboard", "InfoKiosk" }),
            ("office_kitchen", HouseKind.Office, new[] { "KitchenCounter", "Fridge", "WallCabinet", "DiningTable", "Sofa", "SectionalSofa", "PlanterLarge" }),
            ("office_wet", HouseKind.Office, new[] { "MirrorSink", "Urinal", "ToiletStall", "Lockers", "TowelDispenser", "JanitorCart" }),
            ("facility_office", HouseKind.Abandoned, new[] { "Desk", "Chair", "Cabinet", "FilingCabinet", "TallBookcase", "Sofa", "OperatorChair" }),
            ("facility_lab", HouseKind.Abandoned, new[] { "LabBench", "FumeHood", "Lockers", "Fridge", "MirrorSink", "ToiletStall", "VendingMachine", "PlanterLarge" }),
        };

        static readonly int[] Things =
        {
            ItemCatalog.Stun, ItemCatalog.Book, ItemCatalog.Radio, ItemCatalog.Radio + 1, ItemCatalog.Radio + 2, ItemCatalog.Radio + 3, ItemCatalog.Radio + 4, ItemCatalog.Radio + 5,
            ItemCatalog.Radio + 6, ItemCatalog.Radio + 8, ItemCatalog.Radio + 9, ItemCatalog.Flashlight, ItemCatalog.Cans, ItemCatalog.BeltBag, ItemCatalog.Key,
        };

        [MenuItem("Stealth/Surface Pictures")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var style = config.Default;
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "surfaces");
            Directory.CreateDirectory(dir);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.31f);
            void Light(Vector3 at, float intensity, Color color)
            {
                var l = new GameObject("Lamp").AddComponent<Light>();
                l.type = LightType.Spot;
                l.spotAngle = 130f;
                l.range = 14f;
                l.intensity = intensity;
                l.color = color;
                l.shadows = LightShadows.Soft;
                l.transform.SetPositionAndRotation(at, Quaternion.Euler(60f, 180f, 0f));
            }
            Light(new Vector3(-3f, 3.2f, 1.9f), 24f, new Color(1f, 0.95f, 0.88f));
            Light(new Vector3(3f, 3.2f, 1.9f), 24f, new Color(1f, 0.95f, 0.88f));
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.13f);
            const int w = 1800, h = 560;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 3f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.6f, -1.2f);
            wall.transform.localScale = new Vector3(20f, 3.2f, 0.1f);
            var paint = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            paint.SetColor("_BaseColor", new Color(0.5f, 0.49f, 0.46f));
            floor.GetComponent<Renderer>().sharedMaterial = wall.GetComponent<Renderer>().sharedMaterial = paint;

            void Shoot(string file)
            {
                cam.Render();
                cam.Render(); // (the first time textures may not have arrived)
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, file + ".png"), tex.EncodeToPNG());
            }

            foreach (var (name, house, props) in Rows)
                foreach (bool after in new[] { false, true })
                {
                    var made = new List<GameObject>();
                    float x = 0f;
                    foreach (var p in props)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{p}.prefab");
                        if (prefab == null) { Debug.LogWarning($"[Surfaces] no {p}"); continue; }
                        var go = Object.Instantiate(prefab);
                        foreach (var v in go.GetComponentsInChildren<PropVariants>(true))
                        {
                            v.Dress(7);
                            foreach (var r in v.Own) if (r != null) r.enabled = false;
                        }
                        Dress(go, config, style, config.LookOf(house), after);
                        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
                        var b = Bounds(go);
                        go.transform.position = new Vector3(x - b.min.x, p == "WallCabinet" || p == "TowelDispenser" ? 1f - Mathf.Min(1f, b.min.y) : 0f, -1.1f - b.min.z);
                        x += b.size.x + 0.18f;
                        made.Add(go);
                    }
                    foreach (var go in made) go.transform.position -= Vector3.right * x / 2f;
                    cam.fieldOfView = 24f;
                    float back = Mathf.Max(5.5f, -1f + x / 2f / (Mathf.Tan(12f * Mathf.Deg2Rad) * w / h));
                    cam.transform.position = new Vector3(0f, 1.5f, back);
                    cam.transform.LookAt(new Vector3(0f, 0.85f, -1f));
                    Shoot($"{name}_{(after ? "after" : "before")}");
                    if (after) // two close looks, as near as a player stands
                        for (int k = 0; k < 2; k++)
                        {
                            var b = Bounds(made[k == 0 ? 0 : made.Count / 2]);
                            cam.fieldOfView = 46f;
                            cam.transform.position = new Vector3(b.center.x + 0.5f, Mathf.Max(1.3f, b.max.y * 0.85f), b.max.z + 0.95f);
                            cam.transform.LookAt(new Vector3(b.center.x, b.center.y + b.extents.y * 0.35f, b.max.z - 0.1f));
                            Shoot($"close_{name}_{k}");
                        }
                    foreach (var go in made) Object.DestroyImmediate(go);
                }

            var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");
            foreach (bool after in new[] { false, true })
            {
                var made = new List<GameObject>();
                float x = 0f;
                foreach (int shape in Things)
                {
                    if (visuals.Models[shape] == null) continue;
                    var go = Object.Instantiate(visuals.Models[shape]);
                    if (after) Dress(go, config, style, null, true);
                    foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
                    go.transform.rotation = Quaternion.Euler(0f, shape == ItemCatalog.Book || shape == ItemCatalog.Radio + 9 ? 90f : 20f, 0f);
                    var b = Bounds(go);
                    go.transform.position = new Vector3(x - b.min.x, -b.min.y, 0f);
                    x += b.size.x + 0.05f;
                    made.Add(go);
                }
                foreach (var go in made) go.transform.position -= Vector3.right * x / 2f;
                cam.fieldOfView = 20f;
                float back = x / 2f / (Mathf.Tan(10f * Mathf.Deg2Rad) * w / h) * 1.03f;
                cam.transform.position = new Vector3(0f, back * 0.62f, back * 0.8f);
                cam.transform.LookAt(new Vector3(0f, 0.06f, 0f));
                Shoot($"items_{(after ? "after" : "before")}");
                if (after)
                    for (int k = 0; k < 3; k++)
                    {
                        cam.fieldOfView = 14f;
                        float at = x * (0.17f + 0.33f * k) - x / 2f;
                        cam.transform.position = new Vector3(at, 1.1f, 1.5f);
                        cam.transform.LookAt(new Vector3(at, 0.06f, 0f));
                        Shoot($"close_items_{k}");
                    }
                foreach (var go in made) Object.DestroyImmediate(go);
            }
            Debug.Log("[Surfaces] pictures in Logs/surfaces");
        }

        /// <summary>The models and materials of a prop in a house, as VisualStyles.DressRenderer puts them on (without the game).</summary>
        public static void Dress(GameObject go, StyleConfig config, VisualStyle style, HouseLook look, bool surfaces)
        {
            var family = style.Geometry.Family;
            var house = look != null ? look.FurnitureFor(family) : ModelFamily.None;
            var f = house != ModelFamily.None ? house : family;
            var meshes = config.Meshes.FirstOrDefault(m => m.Family == f);
            var own = config.StyleModels.Where(m => m.Style == style && m.Variant == -1).ToArray();
            var mats = config.Materials.FirstOrDefault(m => m.Family == f);
            var set = surfaces && look != null ? look.SurfacesFor(family) : null;
            bool opens = go.GetComponentInChildren<Storage>(true) != null;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var replace = opens ? null : config.Props.FirstOrDefault(p => p.Family == f && p.Key == mesh);
                foreach (var m in own)
                {
                    int i = house == ModelFamily.None ? System.Array.IndexOf(m.From, mesh) : -1;
                    if (i >= 0) mf.sharedMesh = m.To[i];
                }
                if (mf.sharedMesh == mesh && meshes != null)
                {
                    int i = System.Array.IndexOf(meshes.From, mesh);
                    if (i >= 0 && meshes.To[i] != null) mf.sharedMesh = meshes.To[i];
                }
                r.sharedMaterials = r.sharedMaterials.Select(m =>
                {
                    if (m == null) return m;
                    if (set != null && set.TryGet(mesh, m, out var s)) return s;
                    int i = mats != null ? System.Array.IndexOf(mats.From, m) : -1;
                    var to = i >= 0 ? mats.To[i] : m;
                    var wear = config.Wear.FirstOrDefault(v => v.From == to);
                    return wear != null ? wear.To[Mathf.Min(1, wear.To.Length - 1)] : to;
                }).ToArray();
                if (replace == null) continue;
                foreach (var o in mf.transform.parent.GetComponentsInChildren<MeshRenderer>(true))
                    if (replace.HideGlow || !o.name.StartsWith("Eye")) o.enabled = false;
                var made = Object.Instantiate(replace.Prefab, mf.transform.parent);
                made.transform.localPosition = replace.Position;
                made.transform.localRotation = Quaternion.identity;
                made.transform.localScale = replace.Scale;
            }
        }

        static Bounds Bounds(GameObject go)
        {
            Bounds b = default;
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>For the command line (with graphics).</summary>
        public static void Batch()
        {
            try { Render(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
