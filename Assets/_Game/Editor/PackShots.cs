using System.Collections.Generic;
using System.IO;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Pictures of Linus's backpack loaded with things, to see where they really lie (RaccoonPack, PackWrap): every level's
    /// pack with a few stash layouts — a full mix as "put everything in" packs it (every turn FindSpot tries), long things
    /// along and across the grid, single things in each corner and edge — each seen from four sides, a sheet per layout in
    /// Logs/packshots/. Needs a graphics device (no -nographics):
    ///
    ///     Unity -batchmode -projectPath . -executeMethod Stealth.EditorTools.PackShots.Run -quit -logFile Logs/packshots.log
    ///
    /// or the menu Stealth → Pack Shots in the editor.
    /// </summary>
    public static class PackShots
    {
        const string BotPrefab = "Assets/_Game/Prefabs/Vehicles/StorageBot.prefab";
        const int Tile = 360;

        [MenuItem("Stealth/Pack Shots")]
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefab);
            if (prefab == null) { Debug.LogError("[PackShots] no StorageBot prefab"); return; }
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "packshots");
            Directory.CreateDirectory(dir);
            var lightGo = new GameObject("PackShotsLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var fillGo = new GameObject("PackShotsFill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.6f;
            fillGo.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            var camGo = new GameObject("PackShotsCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.44f, 0.47f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(Tile, Tile, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            try
            {
                foreach (var (name, make) in Layouts())
                {
                    var rows = new List<Texture2D[]>();
                    for (int level = 0; level <= Vehicles.CarUpgrades.MaxLevelled; level++)
                    {
                        var size = Vehicles.CarUpgrades.StashSize(level, null);
                        var items = make(size.x, size.y);
                        var bot = Object.Instantiate(prefab);
                        bot.transform.position = Vector3.zero;
                        var pack = bot.GetComponentInChildren<RaccoonPack>(true);
                        pack.Show(level, size.x, size.y, items);
                        Report(name, level, pack, items);
                        var shots = new Texture2D[4];
                        var aim = new Vector3(0f, 0.65f, -0.05f);
                        var views = new[] { new Vector3(-1f, 0.7f, -1f), new Vector3(-1.4f, 0.25f, 0f), new Vector3(0.9f, 0.6f, -1.2f), new Vector3(0.05f, 2f, -0.4f) };
                        for (int v = 0; v < views.Length; v++)
                        {
                            float far = 1.6f + level * 0.35f;
                            cam.transform.position = aim + views[v].normalized * far * 1.6f;
                            cam.transform.LookAt(aim);
                            cam.Render();
                            RenderTexture.active = rt;
                            var tex = new Texture2D(Tile, Tile, TextureFormat.RGB24, false);
                            tex.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
                            tex.Apply();
                            shots[v] = tex;
                        }
                        RenderTexture.active = null;
                        Object.DestroyImmediate(bot);
                        rows.Add(shots);
                        Debug.Log($"[PackShots] {name} level {level}: {items.Count} things on a {size.x} x {size.y} grid");
                    }
                    var sheet = new Texture2D(Tile * 4, Tile * rows.Count, TextureFormat.RGB24, false);
                    for (int r = 0; r < rows.Count; r++)
                        for (int c = 0; c < 4; c++)
                            sheet.SetPixels(c * Tile, (rows.Count - 1 - r) * Tile, Tile, Tile, rows[r][c].GetPixels());
                    sheet.Apply();
                    File.WriteAllBytes(Path.Combine(dir, name + ".png"), sheet.EncodeToPNG());
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(fillGo);
                rt.Release();
            }
            Debug.Log("[PackShots] done: " + dir);
        }

        /// <summary>
        /// Each thing's place on the pack in the log: its face, where it lies, and how far its nearest point is from the pack's
        /// main body (above a couple of centimetres it floats; inside the body by more than its own thickness it sank in).
        /// </summary>
        static void Report(string layout, int level, RaccoonPack pack, List<InventoryEntry> items)
        {
            var body = pack.Levels[level].GetComponentsInChildren<MeshRenderer>(true);
            var main = System.Array.Find(body, r => r.name == $"Sack_L{level}" || r.name == $"Canvas_L{level}");
            if (main == null) return;
            var mb = main.bounds;
            for (int i = 0; i < items.Count && i < pack.Pieces.Length; i++)
            {
                var piece = pack.Pieces[i];
                var rs = piece.GetComponentsInChildren<Renderer>(false);
                var b = new Bounds(piece.transform.position, Vector3.zero);
                bool any = false;
                foreach (var r in rs) if (r.enabled) { if (!any) b = r.bounds; else b.Encapsulate(r.bounds); any = true; }
                float gap = Mathf.Sqrt(mb.SqrDistance(b.ClosestPoint(mb.center)));
                Debug.Log($"[PackShots] {layout} L{level} #{i} {ItemCatalog.Shapes[items[i].Shape].Id} at ({items[i].X},{items[i].Y}) rot {items[i].Rot}: " +
                          $"local {piece.transform.localPosition:F3} world {b.center:F2} size {b.size:F2} gap to body {gap:F3}{(gap > 0.03f ? "  FLOATS" : "")}");
            }
        }

        /// <summary>The stash layouts shot (by the grid's columns and rows).</summary>
        static IEnumerable<(string, System.Func<int, int, List<InventoryEntry>>)> Layouts()
        {
            // what "put everything in" makes of a mixed heap: the biggest first, each into the first spot of any turn
            yield return ("full_mix", (w, h) =>
            {
                var stash = new TeamStash(w, h);
                byte[] heap = { ItemCatalog.Shovel, ItemCatalog.Book, ItemCatalog.Canister, ItemCatalog.Flashlight, ItemCatalog.Cans, ItemCatalog.Screwdriver,
                    ItemCatalog.Gamepad, ItemCatalog.Battery, ItemCatalog.Coffee, ItemCatalog.Bandage, ItemCatalog.Ssd, ItemCatalog.Cassette,
                    ItemCatalog.Radio, ItemCatalog.Key, ItemCatalog.FirstChess, ItemCatalog.Medkit, ItemCatalog.Chip, ItemCatalog.Wires };
                var list = new List<InventoryEntry>();
                foreach (var shape in heap) list.Add(new InventoryEntry { Shape = shape, Color = 1 });
                stash.AddAll(list, out _);
                return new List<InventoryEntry>(stash.Items);
            });
            // long things along the grid (columns: round the pack) and across it (rows: along the body), and turned
            yield return ("long_things", (w, h) =>
            {
                var stash = new TeamStash(w, h);
                stash.Add(ItemCatalog.Shovel, 1, 0, 0, 1);                       // across the top rows, a quarter turned
                stash.Add(ItemCatalog.Flashlight, 1, (byte)(w - 1), 1, 0);      // upright in the last column
                stash.Add(ItemCatalog.Screwdriver, 1, 0, 1, 0);                 // upright in the first column
                stash.Add(ItemCatalog.Book, 1, (byte)(w / 2 - 1), 1, 0);        // a flat square in the middle
                stash.Add(ItemCatalog.Hammer, 1, 1, (byte)(h - 1), 1);          // across the last row (down the back)
                return new List<InventoryEntry>(stash.Items);
            });
            // one small thing in every corner and in the middle of every edge: where the faces meet
            yield return ("corners", (w, h) =>
            {
                var stash = new TeamStash(w, h);
                byte[] small = { ItemCatalog.Battery, ItemCatalog.Coffee, ItemCatalog.Bandage, ItemCatalog.Cassette, ItemCatalog.Ssd, ItemCatalog.Chip, ItemCatalog.Wires, ItemCatalog.Espresso };
                var spots = new[] { (0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1), (w / 2, 0), (w / 2, h - 1), (0, h / 2), (w - 1, h / 2) };
                for (int i = 0; i < spots.Length; i++) stash.Add(small[i], 1, (byte)spots[i].Item1, (byte)spots[i].Item2, (byte)(i % 4));
                return new List<InventoryEntry>(stash.Items);
            });
        }
    }
}
