using System.IO;
using System.Linq;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Every piece of furniture on its own, as the office wears it (models of the style, surfaces), from a standing player's
    /// eyes, three quarters from the front → Logs/props/&lt;prefab&gt;.png: can one tell what it is? Menu Stealth → Prop Catalogue,
    /// or in batch with graphics: <c>-executeMethod Stealth.EditorTools.PropCatalogue.Batch</c> (ONLY=Safe,Desk: just these;
    /// SIZE=640: bigger).
    /// </summary>
    public static class PropCatalogue
    {
        [MenuItem("Stealth/Prop Catalogue")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var style = config.Default;
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "props");
            Directory.CreateDirectory(dir);
            var only = (System.Environment.GetEnvironmentVariable("ONLY") ?? "").Split(',').Where(s => s.Length > 0).ToArray();
            if (!int.TryParse(System.Environment.GetEnvironmentVariable("SIZE"), out int size)) size = 360;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.26f, 0.27f);
            RenderSettings.reflectionIntensity = 0f;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 4f;
            var paint = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            paint.SetColor("_BaseColor", new Color(0.42f, 0.41f, 0.39f));
            paint.SetFloat("_Smoothness", 0.1f);
            floor.GetComponent<Renderer>().sharedMaterial = paint;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.7f;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(48f, 205f, 0f);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.21f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            int n = 0;
            if (!System.Enum.TryParse<HouseKind>(System.Environment.GetEnvironmentVariable("HOUSE"), out var house)) house = HouseKind.Office;
            if (!int.TryParse(System.Environment.GetEnvironmentVariable("SEED"), out int seed)) seed = 77;
            string suffix = System.Environment.GetEnvironmentVariable("HOUSE") != null || System.Environment.GetEnvironmentVariable("SEED") != null ? $"_{house}_{seed}" : "";
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs/Props", "Assets/_Game/Prefabs/StyleKit/Things", "Assets/_Game/Prefabs/Items" }).Concat(AssetDatabase.FindAssets("ElectricPanel t:Prefab")))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (only.Length > 0 && !only.Contains(prefab.name)) continue;
                var go = Object.Instantiate(prefab);
                if (System.Enum.TryParse<WallThing>(prefab.name, out _)) go.transform.position = Vector3.up * 1.5f; // (what hangs on a wall: off the floor)
                foreach (var v in go.GetComponentsInChildren<PropVariants>(true))
                {
                    v.Dress(seed == 77 ? 7 : seed);
                    foreach (var r in v.Own) if (r != null) r.enabled = false;
                }
                foreach (var parts in go.GetComponentsInChildren<StyleParts>(true)) parts.Show(style.name);
                SurfaceSheet.Dress(go, config, style, config.LookOf(house), true);
                foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
                // its own picture as in a house (LevelBuilder.DressPictures): HOUSE=Garage, SEED=3 for another
                if (System.Enum.TryParse<PropKind>(prefab.name, out var mine) && PropPictures.TryPick(mine, house, seed, out var pick))
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (pick.Part != null ? !r.name.StartsWith(pick.Part) : r.sharedMaterial.name != "Mirror") continue;
                        r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Game/Art/Materials/Props/{new[] { "BoardSheet", "MirrorGlass", "ProjectorCloth" }[(int)pick.Kind]}.mat");
                        var mf = r.GetComponent<MeshFilter>();
                        mf.sharedMesh = ScreenFaces.Of(mf.sharedMesh, pick.Faces);
                    }
                // the displays as in a house (LevelBuilder.DressScreens)
                int picture = System.Enum.TryParse<PropKind>(prefab.name, out var kind) ? ScreenLooks.DisplayOf(kind) : -1;
                if (picture >= 0)
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                        if (r.sharedMaterial.name == "PropScreen")
                        {
                            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/Props/Displays.mat");
                            var mf = r.GetComponent<MeshFilter>();
                            mf.sharedMesh = ScreenFaces.Of(mf.sharedMesh, picture);
                        }
                var bounds = new Bounds();
                bool first = true;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                if (!first)
                {
                    float radius = Mathf.Max(0.15f, bounds.extents.magnitude);
                    var from = new Vector3(0.55f, 0.38f, 1f).normalized;
                    cam.transform.position = bounds.center + from * (radius / Mathf.Sin(13f * Mathf.Deg2Rad) * 0.92f);
                    cam.transform.LookAt(bounds.center);
                    for (int i = 0; i < 2; i++) cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, prefab.name + suffix + ".png"), tex.EncodeToPNG());
                    n++;
                }
                Object.DestroyImmediate(go);
            }
            Debug.Log($"[Props] {n} pictures in Logs/props");
        }

        /// <summary>For the command line (with graphics).</summary>
        public static void Batch()
        {
            try { Render(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
