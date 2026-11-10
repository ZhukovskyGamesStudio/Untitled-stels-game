using System.Collections.Generic;
using System.IO;
using Stealth.Items;
using UnityEditor;
using Stealth.Level;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        static readonly Dictionary<string, Material> s_Mats = new();

        /// <summary>Emission of loot: above the bloom threshold, like every glowing material.</summary>
        const float LootGlow = 1.4f;

        static Material M(string name) =>
            s_Mats.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException($"Material {name} is not registered in BuildMaterials");

        /// <summary>Loads or creates Art/Materials/<paramref name="category"/>/<paramref name="name"/>.mat.</summary>
        static Material Mat(string category, string name, Color color, Color? emission = null, bool unlit = false, float smoothness = 0.25f)
        {
            var dir = $"{MaterialsDir}/{category}";
            var path = $"{dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            s_Mats[name] = mat;
            if (mat != null && !s_Force) return mat;

            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (mat == null)
            {
                EnsureFolder(dir);
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            if (!unlit) mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.SetColor("_EmissionColor", emission.Value);
                // Not "None": URP's material validation reads None as "emission is black" and switches _EMISSION off on import —
                // nothing ever glowed. Baked-emissive keeps the keyword (there is no lightmap baking, so it costs nothing).
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                mat.EnableKeyword("_EMISSION");
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.DisableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(mat);
            s_Mats[name] = mat;
            return mat;
        }

        static void BuildMaterials()
        {
            const string env = "Environment", props = "Props", robots = "Robots", player = "Player", fx = "FX";
            Mat(env, "Wall", new Color(0.78f, 0.77f, 0.72f));
            Mat(env, "Floor", new Color(0.30f, 0.32f, 0.35f), smoothness: 0.45f);
            Mat(env, "Ceiling", new Color(0.86f, 0.86f, 0.84f));
            Mat(env, "OutsideGround", new Color(0.24f, 0.29f, 0.22f));
            Mat(env, "DoorFrame", new Color(0.45f, 0.38f, 0.30f));
            Mat(env, "Door", new Color(0.55f, 0.68f, 0.75f), smoothness: 0.85f);
            // The glass shows the gas pressed against it (like the fog itself it is not lit by anything).
            Mat(env, "Window", LightMath.GasFog * 0.45f, unlit: true);
            Mat(env, "WindowFrame", new Color(0.88f, 0.88f, 0.86f), smoothness: 0.5f);
            Mat(env, "Blinds", new Color(0.82f, 0.8f, 0.72f), smoothness: 0.3f);
            Mat(env, "Hatch", new Color(0.32f, 0.32f, 0.33f), smoothness: 0.6f);
            Mat(env, "HatchRing", new Color(1f, 0.8f, 0.1f), new Color(2.4f, 1.8f, 0.15f));

            Mat(env, "LampHousing", new Color(0.82f, 0.83f, 0.85f), smoothness: 0.5f);
            Mat(env, "LampPanel", new Color(1f, 0.97f, 0.9f), new Color(2.2f, 2.1f, 1.9f));
            Mat(env, "ExitSign", new Color(0.1f, 0.75f, 0.3f), new Color(0.15f, 2.2f, 0.55f));
            Mat(env, "ExitSignMark", new Color(0.95f, 1f, 0.95f), new Color(1.3f, 1.4f, 1.3f));
            Mat(props, "LampShade", new Color(0.9f, 0.84f, 0.7f), new Color(1.3f, 1.05f, 0.6f), smoothness: 0.1f);
            Mat(props, "LampBulb", new Color(1f, 0.9f, 0.7f), new Color(3f, 2.5f, 1.6f));

            Mat(props, "PingPong", new Color(0.08f, 0.35f, 0.25f), smoothness: 0.4f);
            Mat(props, "BeanBag", new Color(0.85f, 0.42f, 0.15f), smoothness: 0.05f);
            Mat(props, "Arcade", new Color(0.18f, 0.12f, 0.35f), smoothness: 0.5f);
            Mat(props, "SafeMetal", new Color(0.22f, 0.24f, 0.27f), smoothness: 0.65f);
            Mat(props, "Keypad", new Color(0.2f, 0.9f, 0.4f), new Color(0.3f, 1.8f, 0.6f));

            Mat(props, "PropBody", new Color(0.62f, 0.60f, 0.56f));
            Mat(props, "PropAccent", new Color(0.22f, 0.23f, 0.26f));
            // Monitors are on: a real glow that blooms.
            Mat(props, "PropScreen", new Color(0.05f, 0.08f, 0.1f), new Color(0.3f, 0.85f, 1.5f), smoothness: 0.8f);
            Mat(props, "Plant", new Color(0.25f, 0.55f, 0.25f));
            Mat(props, "PlantPot", new Color(0.55f, 0.35f, 0.25f));
            Mat(props, "Rack", new Color(0.12f, 0.13f, 0.15f), smoothness: 0.5f);
            Mat(props, "RackLights", new Color(0.2f, 1f, 0.4f), new Color(0.2f, 1.6f, 0.5f));
            Mat(props, "Cardboard", new Color(0.62f, 0.47f, 0.3f));
            Mat(props, "Water", new Color(0.4f, 0.7f, 1f), smoothness: 0.9f);
            Mat(props, "Fabric", new Color(0.35f, 0.25f, 0.45f), smoothness: 0.05f);
            Mat(props, "Stall", new Color(0.55f, 0.65f, 0.6f), smoothness: 0.5f);
            Mat(props, "Wood", new Color(0.58f, 0.42f, 0.28f), smoothness: 0.35f);
            Mat(props, "Metal", new Color(0.55f, 0.57f, 0.6f), smoothness: 0.75f);
            Mat(props, "Folders", new Color(0.25f, 0.4f, 0.65f), smoothness: 0.2f);
            Mat(props, "Lockers", new Color(0.35f, 0.5f, 0.62f), smoothness: 0.55f);
            Mat(props, "Mirror", new Color(0.7f, 0.78f, 0.82f), smoothness: 0.98f);
            Mat(props, "Vending", new Color(0.75f, 0.12f, 0.12f), smoothness: 0.6f);
            Mat(props, "VendingGlass", new Color(0.6f, 0.85f, 1f), new Color(1.1f, 1.5f, 1.9f), smoothness: 0.9f); // lit display

            Mat(robots, "RobotBody", new Color(0.86f, 0.87f, 0.9f), smoothness: 0.6f);
            Mat(robots, "RobotAccent", new Color(0.18f, 0.19f, 0.22f), smoothness: 0.5f);
            Mat(robots, "ListenerBody", new Color(0.95f, 0.55f, 0.15f), smoothness: 0.5f);
            Mat(robots, "DroneBody", new Color(0.25f, 0.27f, 0.32f), smoothness: 0.7f);
            Mat(robots, "ThumperBody", new Color(0.55f, 0.5f, 0.2f), smoothness: 0.3f);
            Mat(robots, "HoundBody", new Color(0.45f, 0.2f, 0.2f), smoothness: 0.4f);
            Mat(robots, "SorterBody", new Color(0.9f, 0.9f, 0.85f), smoothness: 0.8f);
            Mat(robots, "WorkerBody", new Color(0.95f, 0.8f, 0.1f), smoothness: 0.4f);
            Mat(robots, "HunterBody", new Color(0.12f, 0.12f, 0.14f), smoothness: 0.85f);
            Mat(robots, "TurretBody", new Color(0.3f, 0.35f, 0.3f), smoothness: 0.6f);
            Mat(robots, "SpiderBody", new Color(0.15f, 0.12f, 0.12f), smoothness: 0.7f);
            Mat(robots, "VacuumBody", new Color(0.2f, 0.2f, 0.22f), smoothness: 0.8f);
            Mat(robots, "SweeperBody", new Color(0.3f, 0.62f, 0.78f), smoothness: 0.6f);
            Mat(robots, "SensorGlow", new Color(0.2f, 0.85f, 1f), new Color(0.4f, 1.7f, 2.4f));
            Mat(robots, "LaserPost", new Color(0.15f, 0.15f, 0.17f), smoothness: 0.6f);

            Mat(player, "PlayerBody", Color.white);
            Mat(player, "PlayerAccent", new Color(0.9f, 0.78f, 0.65f));
            Mat(player, "PlayerGlasses", new Color(0.05f, 0.05f, 0.06f), smoothness: 0.9f);

            Mat(fx, "Laser", new Color(1f, 0.1f, 0.05f), unlit: true);
            Mat(fx, "Radio", new Color(1f, 0.6f, 0.2f), unlit: true);
            Mat(fx, "Stomp", new Color(1f, 0.75f, 0.25f), unlit: true);

            for (int i = 0; i < ItemCatalog.Colors.Length; i++)
            {
                var c = ItemCatalog.Colors[i].Color;
                var glow = c * (LootGlow / Mathf.Max(0.01f, c.maxColorComponent)); // brightest channel above 1: it blooms
                var m = Mat("Items", $"Item_{i}", c, glow, smoothness: 0.55f); // loot glows: findable in dark rooms
                // Colours are tuned in ItemsConfig: keep the materials in sync even without a full rebuild.
                if (m.GetColor("_BaseColor") != c || m.GetColor("_EmissionColor") != glow)
                {
                    m.SetColor("_BaseColor", c);
                    m.SetColor("_EmissionColor", glow);
                    EditorUtility.SetDirty(m);
                }
            }
        }

        // ------------------------------------------------------------------ UI sprites

        static readonly Dictionary<string, Sprite> s_Sprites = new();

        static Sprite S(string name) =>
            s_Sprites.TryGetValue(name, out var s) ? s : throw new KeyNotFoundException($"Sprite {name} is not registered in BuildSprites");

        /// <summary>Procedural UI textures saved as PNG sprites (vignette, direction arc, soft disc).</summary>
        static void BuildSprites()
        {
            Sprite("Vignette", 128, 128, (u, v) =>
            {
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, d));
            });
            Sprite("Arc", 128, 32, (u, v) =>
            {
                float x = u * 2f - 1f;                  // -1..1 across
                float curve = 1f - x * x * 0.6f;         // bow towards the centre
                float band = Mathf.Clamp01(1f - Mathf.Abs(v - curve * 0.6f) * 4f);
                float fade = Mathf.Clamp01(1f - Mathf.Abs(x) * 1.1f);
                return band * fade;
            });
            Sprite("Disc", 64, 64, (u, v) =>
            {
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                return Mathf.Clamp01((1f - d) * 8f);
            });
            Sprite("Ring", 64, 64, (u, v) =>
            {
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                return Mathf.Clamp01((1f - Mathf.Abs(d - 0.85f) / 0.12f) * 2f);
            });
            // A map arrow pointing up (+v): a chevron with a notch at the back.
            Sprite("MapArrow", 64, 64, (u, v) =>
            {
                float x = Mathf.Abs(u - 0.5f) * 2f;   // 0 centre .. 1 edge
                float tip = 0.95f, back = 0.08f;
                float edge = tip - (tip - back) * x;      // outer edge of the triangle at this x
                float notch = back + 0.32f * (1f - x);    // the notch cut into the back
                float inside = Mathf.Min(edge - v, v - notch) * 40f;
                return Mathf.Clamp01(inside);
            });
        }

        static void Sprite(string name, int w, int h, System.Func<float, float, float> alpha)
        {
            var path = $"{SpritesDir}/{name}.png";
            if (s_Force || !File.Exists(path))
            {
                EnsureFolder(SpritesDir);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        tex.SetPixel(x, y, new Color(1, 1, 1, alpha(x / (w - 1f), y / (h - 1f))));
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            s_Sprites[name] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
