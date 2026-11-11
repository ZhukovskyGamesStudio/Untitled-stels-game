using System.Linq;
using Stealth.Items;
using Stealth.Level;
using Stealth.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The sewer between houses (Prefabs/Level/Sewer), assembled from the models of Tools/Blender/sewer.py. Unity
    /// coordinates: the ladder and the dead end at x = 0, the brick tunnel to x = −12 with the cart, the hall with the
    /// cistern to x = −22, then three tunnels to the next houses (left z = −4.2, middle, right z = +4.2, seen from the
    /// hall), the side ones bending outwards. Built after the UI (it carries the route panels).
    /// </summary>
    public static partial class AssetBuilder
    {
        const string SewerMaterials = MaterialsDir + "/Sewer";
        static readonly float[] BranchZ = { -4.2f, 0f, 4.2f };
        static readonly float[] BranchYaw = { -18f, 0f, 18f };
        const float HallEnd = 22f;

        static GameObject BuildSewerPrefab(LevelArt art, RouteSignView sign)
        {
            BuildSewerMaterials();
            var display = Prefab(PrefabsDir + "/Items/ItemDisplay.prefab", () =>
            {
                var go = new GameObject("ItemDisplay");
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                return go;
            });
            var sewer = Prefab($"{LevelPrefabs}/Sewer.prefab", () => BuildSewer(sign, display.GetComponent<MeshRenderer>()));
            if (art.Sewer != sewer)
            {
                art.Sewer = sewer;
                EditorUtility.SetDirty(art);
            }
            return sewer;
        }

        static Material SewerMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{SewerMaterials}/{name}.mat");

        /// <summary>Wet brick, stone ribs, worn concrete, rusty metal, planks (CC0 scans) — kept out of the style variants.</summary>
        static void BuildSewerMaterials()
        {
            ConfigureExternalTextures();
            void Pbr(string name, string id, Color tint, float smooth, float tiling)
            {
                var m = LitAsset($"{SewerMaterials}/{name}.mat");
                SetPbr(m, $"{ExternalTextures}/{id}", tint, smooth, tiling);
                EditorUtility.SetDirty(m);
            }
            void Plain(string name, Color color, float smooth, float metal)
            {
                var m = LitAsset($"{SewerMaterials}/{name}.mat");
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                EditorUtility.SetDirty(m);
            }
            Pbr("SewerBrick", "large_red_bricks", new Color(0.62f, 0.52f, 0.46f), 0.55f, 0.5f);
            Pbr("SewerStone", "concrete_wall_006", new Color(0.62f, 0.6f, 0.56f), 0.4f, 0.5f);
            Pbr("SewerConcrete", "concrete_floor_worn_001", new Color(0.7f, 0.69f, 0.64f), 0.5f, 0.5f);
            Pbr("SewerRust", "green_metal_rust", new Color(0.85f, 0.8f, 0.75f), 0.45f, 1f);
            Pbr("SewerWood", "dark_planks", new Color(0.95f, 0.85f, 0.75f), 0.35f, 1f);
            Plain("SewerWater", new Color(0.06f, 0.12f, 0.1f), 0.96f, 0.35f);
            Plain("SewerMetal", new Color(0.22f, 0.23f, 0.24f), 0.62f, 0.7f);
            Plain("SewerDark", new Color(0.01f, 0.01f, 0.012f), 0f, 0f);
            Mat("Sewer", "SewerGlow", new Color(1f, 0.82f, 0.55f), new Color(3.2f, 2.4f, 1.3f));
            AssetDatabase.SaveAssets();
        }

        /// <summary>The material of a part by its name (see sewer.py).</summary>
        static Material SewerPart(string n)
        {
            if (n.StartsWith("Brick")) return SewerMat("SewerBrick");
            if (n.StartsWith("Stone") || n.StartsWith("Frame") || n.StartsWith("Curbs") || n.StartsWith("BasinRim")) return SewerMat("SewerStone");
            if (n.StartsWith("Concrete") || n.StartsWith("BasinBed") || n.StartsWith("TrenchBed")) return SewerMat("SewerConcrete");
            if (n.StartsWith("Water")) return SewerMat("SewerWater");
            if (n.StartsWith("Rust") || n.StartsWith("Cage")) return SewerMat("SewerRust");
            if (n.StartsWith("Wood")) return SewerMat("SewerWood");
            if (n.StartsWith("Dark") || n.StartsWith("Gutter")) return SewerMat("SewerDark");
            if (n.StartsWith("Glow")) return SewerMat("SewerGlow");
            return SewerMat("SewerMetal");
        }

        /// <summary>A sewer model under <paramref name="parent"/>: materials by part, solid parts get mesh colliders.</summary>
        static GameObject SewerModel(string file, Transform parent, Vector3 pos, float yaw = 0f, bool solid = true, string name = null)
        {
            var m = Model(file);
            m.name = name ?? file;
            m.transform.SetParent(parent, false);
            m.transform.localPosition = pos;
            m.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var r in m.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                r.sharedMaterial = SewerPart(n);
                bool water = n.StartsWith("Water"), glow = n.StartsWith("Glow");
                r.shadowCastingMode = water || glow ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
                if (solid && !water && !glow) r.gameObject.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
            }
            return m;
        }

        static Light SewerLamp(Transform parent, Vector3 pos, float wallYaw, bool shadows, float intensity = 5.5f, float range = 11f)
        {
            SewerModel("sewer_lamp", parent, pos, wallYaw, false);
            // shine away from the wall (the lamp's back faces it) and a bit down
            var away = Quaternion.Euler(0f, wallYaw, 0f) * Vector3.right;
            var light = Lamp(parent, "Light", pos + away * 0.25f, Quaternion.LookRotation(away + Vector3.down * 0.7f), LightType.Spot,
                new Color(1f, 0.78f, 0.5f), intensity, range, shadows ? LightShadows.Soft : LightShadows.None);
            light.spotAngle = 150f;
            light.innerSpotAngle = 80f;
            return light;
        }

        static GameObject BuildSewer(RouteSignView signPrefab, MeshRenderer itemDisplay)
        {
            var root = new GameObject("Sewer");
            var loc = root.AddComponent<SewerLocation>();
            root.AddComponent<SewerRoute>();
            var t = root.transform;

            // The vaults: tunnel from the ladder, the hall.
            SewerModel("sewer_tunnel", t, Vector3.zero);
            SewerModel("sewer_hall", t, Vector3.zero);

            // The way down: a ladder from the manhole in the vault, by the dead end.
            SewerModel("sewer_ladder", t, new Vector3(-0.35f, 0f, 2.3f));
            SewerModel("sewer_manhole", t, new Vector3(-0.35f, 3.22f, 2.3f), 0f, false);
            loc.Spawns = new[]
            {
                Child(t, "Spawn0", new Vector3(-1.3f, 0.05f, 2.3f)).transform,
                Child(t, "Spawn1", new Vector3(-1.3f, 0.05f, -2.3f)).transform,
                Child(t, "Spawn2", new Vector3(-2.4f, 0.05f, 2.0f)).transform,
                Child(t, "Spawn3", new Vector3(-2.4f, 0.05f, -2.0f)).transform,
            };
            foreach (var s in loc.Spawns) s.localRotation = Quaternion.Euler(0f, -90f, 0f); // facing the hall

            // The team's cart on the left walkway, handles towards the ladder; the stash board hangs over it.
            var cart = SewerModel("sewer_cart", t, new Vector3(-4.6f, 0f, -2.35f), 0f, false);
            var cartBox = cart.AddComponent<BoxCollider>();
            cartBox.center = new Vector3(0f, 0.55f, 0f);
            cartBox.size = new Vector3(2.0f, 1.1f, 1.3f);
            var bed = Child(cart.transform, "Bed", new Vector3(0.88f, 0.62f, 0.44f));
            bed.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var stash = root.AddComponent<StashCart>();
            stash.Bed = bed.transform;
            stash.ItemPrefab = itemDisplay;
            stash.Visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>(DataDir + "/ItemVisuals.asset");
            loc.TradeSpot = Child(t, "TradeSpot", new Vector3(-4.6f, 0f, -2.35f)).transform;

            // Life along the tunnel: pipes on both walls, a valve, barrels, crates.
            // (a pipe model runs 4 m towards −X at yaw 0 with its wall at −Z; turned around for the other wall)
            for (int i = 0; i < 3; i++)
            {
                float x0 = -0.4f - 4f * i;
                SewerModel("sewer_pipe", t, new Vector3(x0 - 4f, 2.75f, 3.1f), 180f, false);
                SewerModel("sewer_pipe", t, new Vector3(x0, 2.55f, -3.1f), 0f, false);
                SewerModel("sewer_pipe", t, new Vector3(x0, 0.55f, -3.15f), 0f, false);
            }
            SewerModel("sewer_valve", t, new Vector3(-6.8f, 1.2f, -3.05f), -90f, false);
            foreach (var p in new[] { new Vector3(-8.1f, 0f, -2.7f), new Vector3(-8.75f, 0f, -2.45f), new Vector3(-10.6f, 0f, 2.75f) })
            {
                var b = SewerModel("sewer_barrel", t, p, p.x * 37f, false);
                var c = b.AddComponent<CapsuleCollider>();
                c.center = new Vector3(0f, 0.44f, 0f);
                c.radius = 0.3f;
                c.height = 0.88f;
            }
            foreach (var (p, yaw) in new[] { (new Vector3(-2.9f, 0f, 2.75f), 8f), (new Vector3(-3.55f, 0f, 2.8f), -12f), (new Vector3(-3.2f, 0.6f, 2.78f), 25f) })
            {
                var c = SewerModel("sewer_crate", t, p, yaw, false).AddComponent<BoxCollider>();
                c.center = new Vector3(0f, 0.3f, 0f);
                c.size = new Vector3(0.62f, 0.6f, 0.62f);
            }

            // Lamps: alternating walls along the tunnel, over the openings in the hall, one hanging over the cistern.
            SewerLamp(t, new Vector3(-2.0f, 2.0f, -3.15f), -90f, true);
            SewerLamp(t, new Vector3(-5.2f, 2.0f, 3.15f), 90f, false);
            SewerLamp(t, new Vector3(-8.4f, 2.0f, -3.15f), -90f, true);
            SewerLamp(t, new Vector3(-11.2f, 2.0f, 3.15f), 90f, false, 4.5f);
            SewerLamp(t, new Vector3(-HallEnd + 0.45f, 3.0f, -2.1f), 0f, false, 4.5f, 10f);
            SewerLamp(t, new Vector3(-HallEnd + 0.45f, 3.0f, 2.1f), 0f, false, 4.5f, 10f);
            SewerLamp(t, new Vector3(-15.0f, 2.3f, -6.35f), -90f, false, 4f, 10f);
            SewerLamp(t, new Vector3(-15.0f, 2.3f, 6.35f), 90f, false, 4f, 10f);
            var hanging = Lamp(t, "HallLight", new Vector3(-16.6f, 4.2f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot,
                new Color(1f, 0.8f, 0.55f), 8f, 12f, LightShadows.Soft);
            hanging.spotAngle = 140f;
            hanging.innerSpotAngle = 70f;
            Cube(t, "HallLampShade", new Vector3(-16.6f, 4.3f, 0f), new Vector3(0.4f, 0.12f, 0.4f), SewerMat("SewerGlow"), false)
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cube(t, "HallLampCable", new Vector3(-16.6f, 4.75f, 0f), new Vector3(0.02f, 0.9f, 0.02f), SewerMat("SewerMetal"), false);

            // The three ways on: a tunnel each (side ones bending away), a grate when it leads nowhere, a panel over it.
            loc.Branches = new SewerLocation.Branch[3];
            for (int i = 0; i < 3; i++)
            {
                var mouth = Child(t, $"Branch{i}", new Vector3(-HallEnd, 0f, BranchZ[i])).transform;
                mouth.localRotation = Quaternion.Euler(0f, 180f + BranchYaw[i], 0f); // +X leads into the tunnel
                var tunnelRoot = Child(t, $"BranchTunnel{i}", new Vector3(-HallEnd, 0f, BranchZ[i])).transform;
                tunnelRoot.localRotation = Quaternion.Euler(0f, BranchYaw[i], 0f);
                SewerModel("sewer_branch", tunnelRoot, Vector3.zero);
                var grate = SewerModel("sewer_grate", tunnelRoot, new Vector3(-0.15f, 0f, 0f), 0f, false, "Grate");
                var bars = grate.AddComponent<BoxCollider>();
                bars.center = new Vector3(0f, 1.45f, 0f);
                bars.size = new Vector3(0.1f, 2.9f, 2.6f);
                var lamp = Lamp(tunnelRoot, "TunnelLight", new Vector3(-5f, 2.5f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot,
                    new Color(0.65f, 0.85f, 1f), 3.5f, 8f);
                lamp.spotAngle = 150f;
                var s = Instance<RouteSignView>(signPrefab.gameObject, t, $"Sign{i}");
                s.transform.localPosition = new Vector3(-HallEnd + 0.7f, 3.55f, BranchZ[i]);
                s.transform.localRotation = Quaternion.Euler(-15f, -90f, 0f); // read from the hall, leaning towards the reader
                loc.Branches[i] = new SewerLocation.Branch { Mouth = mouth, Grate = grate, Sign = s, Lamp = lamp.gameObject };
            }

            // The flood that ends a run without the quota: under the floors, rises over everything.
            var flood = Cube(t, "FloodWater", new Vector3(-17f, -0.5f, 0f), new Vector3(36f, 0.05f, 18f), SewerMat("SewerWater"), false);
            flood.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            loc.Water = flood.transform;
            return root;
        }
    }
}
