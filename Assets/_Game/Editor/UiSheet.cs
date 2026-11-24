using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Stealth.Items;
using Stealth.Level;
using Stealth.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Pictures of the interface without running the game: the UI prefabs filled with sample states (a house in
    /// progress, a terminal, the big moments, the minigames, the start screen, the backpack, the menus, the world signs)
    /// over a game screenshot, at several screen shapes — Logs/ui/&lt;shot&gt;_&lt;w&gt;x&lt;h&gt;.png. Menu Stealth → UI Sheet, or
    /// batchmode (with graphics): <c>-executeMethod Stealth.EditorTools.UiSheet.RenderBatch</c>.
    /// </summary>
    public static class UiSheet
    {
        const string Out = "Logs/ui";
        const string Ui = "Assets/_Game/Prefabs/UI";

        static readonly Vector2Int[] Sizes = { new(1280, 720), new(1024, 768), new(2560, 1080), new(1920, 1080) };

        [MenuItem("Stealth/UI Sheet")]
        public static void Render()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var path = EditorSceneManager.GetActiveScene().path;
            RenderAll();
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
            EditorUtility.RevealInFinder(Out);
        }

        public static void RenderBatch()
        {
            int code = 0;
            try { RenderAll(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        static Camera s_Camera;
        static RawImage s_Back;
        static readonly Dictionary<string, Texture2D> s_Backs = new();

        static void RenderAll()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            s_Camera = new GameObject("SheetCamera").AddComponent<Camera>();
            s_Camera.clearFlags = CameraClearFlags.SolidColor;
            s_Camera.backgroundColor = new Color(0.09f, 0.09f, 0.1f);
            s_Camera.nearClipPlane = 0.05f;
            var back = new GameObject("Backdrop", typeof(RectTransform)).AddComponent<Canvas>();
            back.renderMode = RenderMode.ScreenSpaceCamera;
            back.worldCamera = s_Camera;
            back.planeDistance = 50f;
            back.sortingOrder = -100;
            s_Back = new GameObject("Shot", typeof(RectTransform)).AddComponent<RawImage>();
            s_Back.transform.SetParent(back.transform, false);
            var brt = s_Back.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = brt.offsetMax = Vector2.zero;

            Shot("hud_play", "Builds/shots/tour_5_5/tour_05_f0_Kitchen_33_54m.png", HudPlay);
            Shot("hud_light", "Builds/shots/tour_5_5/tour_01_f0_Restroom_9_19m.png", HudPlay);
            Shot("hud_terminal", "Builds/shots/tour_5_5/tour_02_f0_Meeting_21_16m.png", HudTerminal);
            Shot("hud_map", "Builds/shots/tour_5_5/tour_02_f0_Meeting_21_16m.png", HudMap);
            Shot("hud_moments", "Builds/shots/tour_5_5/tour_06_f1_Library_40_36m.png", HudMoments);
            Shot("hud_stats", "Builds/shots/tour_5_5/tour_06_f1_Library_40_36m.png", HudStats);
            Shot("safe", "Builds/shots/tour_5_5/tour_04_f0_Kitchen_27_16m.png", Safe);
            Shot("vault", "Builds/shots/tour_5_5/tour_04_f0_Kitchen_27_16m.png", Vault);
            Shot("start", "Builds/shots/tour_5_5/tour_03_f0_Restroom_24_20m.png", Start);
            Shot("upgrades", "Builds/shots/tour_5_5/tour_03_f0_Restroom_24_20m.png", Upgrades);
            Shot("inventory", "Builds/shots/tour_5_5/tour_05_f0_Kitchen_33_54m.png", Inventory);
            Shot("menu_main", null, MenuMain);
            Shot("menu_pause", "Builds/shots/tour_5_5/tour_02_f0_Meeting_21_16m.png", MenuPause);
            Shot("menu_settings", "Builds/shots/tour_5_5/tour_02_f0_Meeting_21_16m.png", MenuSettings);
            Shot("cheats", "Builds/shots/tour_5_5/tour_02_f0_Meeting_21_16m.png", Cheats);
            Shot("styles", "Builds/shots/tour_5_5/tour_02_f0_Meeting_21_16m.png", Styles);
            WorldShot();
            Debug.Log($"[UiSheet] pictures in {Path.GetFullPath(Out)}");
        }

        // ------------------------------------------------------------------ rendering

        static void Shot(string name, string background, Func<List<GameObject>> fill)
        {
            List<GameObject> made = null;
            try
            {
                s_Back.texture = Back(background);
                s_Back.enabled = s_Back.texture != null;
                made = fill();
                foreach (var go in made)
                    if (go.TryGetComponent<Canvas>(out var c) && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        c.renderMode = RenderMode.ScreenSpaceCamera;
                        c.worldCamera = s_Camera;
                        c.planeDistance = 10f - c.sortingOrder * 0.01f;
                    }
                foreach (var size in Sizes) Capture(name, size, made);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UiSheet] {name}: {e}");
            }
            finally
            {
                if (made != null) foreach (var go in made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void Capture(string name, Vector2Int size, List<GameObject> made)
        {
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            s_Camera.targetTexture = rt;
            s_Camera.aspect = size.x / (float)size.y;
            // CanvasScaler Expand by hand (it scales in the player loop): the tighter side fits the 1280 x 720 layout
            float scale = Mathf.Min(size.x / 1280f, size.y / 720f);
            foreach (var go in made)
                foreach (var canvas in go.GetComponentsInChildren<Canvas>(true))
                    if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace) canvas.scaleFactor = scale;
            for (int pass = 0; pass < 3; pass++)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var go in made)
                {
                    foreach (var r in go.GetComponentsInChildren<RectTransform>(false).Where(r => r.GetComponent<LayoutGroup>() != null).Reverse())
                        LayoutRebuilder.ForceRebuildLayoutImmediate(r);
                    foreach (var b in go.GetComponentsInChildren<UiBacking>(false)) b.Refit();
                }
            }
            Canvas.ForceUpdateCanvases();
            s_Camera.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{Out}/{name}_{size.x}x{size.y}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            s_Camera.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        static Texture2D Back(string path)
        {
            if (path == null || !File.Exists(path)) return null;
            if (s_Backs.TryGetValue(path, out var t)) return t;
            t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(path));
            return s_Backs[path] = t;
        }

        static GameObject Make(string prefab) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Ui}/{prefab}.prefab"));

        static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(target, args);

        static void On(Component c, bool on = true) { if (c != null) c.gameObject.SetActive(on); }
        static void On(GameObject g, bool on = true) { if (g != null) g.SetActive(on); }

        static T Add<T>(T prefab, Transform parent) where T : Component
        {
            var t = UnityEngine.Object.Instantiate(prefab, parent, false);
            t.gameObject.SetActive(true);
            return t;
        }

        static Sprite ItemIcon(byte shape)
        {
            var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>("Assets/_Game/Data/ItemVisuals.asset");
            return visuals != null && visuals.Icons != null && shape < visuals.Icons.Length ? visuals.Icons[shape] : null;
        }

        // ------------------------------------------------------------------ HUD

        static GameHUD Hud(out GameObject go)
        {
            go = Make("HUD");
            var hud = go.GetComponent<GameHUD>();
            On(hud.Content);
            On(hud.TabletScreen, false);
            On(hud.LoadingFade, false);
            On(hud.RunOver, false);
            foreach (var g in new Component[] { hud.LowHpVignette, hud.HitFlash, hud.GasVignette, hud.GasTint, hud.LaughText, hud.GasText, hud.DownedTint,
                         hud.DownedText, hud.HatchTint, hud.HatchText, hud.RouteCountdown, hud.TerminalLabel, hud.HatchMarker, hud.HitArc, hud.ReticleRing })
                On(g, false);
            On(hud.AlarmFrame, false);
            On(hud.Banner, false);
            On(hud.GainLabel, false);
            hud.ReticleGlyph.rectTransform.sizeDelta = new Vector2(5, 5);
            return hud;
        }

        static void Hint(GameHUD hud, string key, string key2, string text, bool dim = false)
        {
            var row = Add(hud.HintPrefab, hud.HintStack);
            row.First.Key.text = key;
            On(row.First.Cap);
            On(row.First.Mouse, false);
            On(row.Second, key2 != null);
            if (key2 != null) row.Second.Key.text = key2;
            row.Label.text = text;
            row.Group.alpha = dim ? GameHUD.HintAlpha * 0.55f : GameHUD.HintAlpha;
        }

        static List<GameObject> HudPlay()
        {
            var hud = Hud(out var go);
            hud.HouseLabel.text = "Больница «Мега-Лаб»";
            hud.QuotaLabel.text = "42 / 120";
            hud.QuotaBar.Set(0.35f, UiPalette.MarkOrange);
            On(hud.GainLabel);
            hud.GainLabel.text = "+20";
            On(hud.ClockPanel);
            hud.ClockPanel.color = UiPalette.HeatPaper(0.33f);
            hud.ClockTime.text = "4:17";
            hud.ClockNext.text = "6:00";
            hud.ClockCount.text = "+2";
            hud.ClockBar.Set(0.6f, UiPalette.Stamp.A(0.75f));
            On(hud.ObjectiveNote);
            foreach (var (line, done) in new[] { ("Найдите выход", true), ("<b>Найдите заложника</b>", false), ("Наберите топливо на поездку", false) })
            {
                var row = Add(hud.TaskRowPrefab, hud.TaskRows);
                On(row.Tick, done);
                row.Label.text = line;
                row.Label.color = done ? UiPalette.InkFaint : UiPalette.Pen;
            }
            On(hud.TeamList);
            foreach (var (nick, slot, hp, state) in new[] { ("Саша", 1, 2, ""), ("Лёша", 2, 1, ""), ("Петя", 3, 0, "лежит") })
            {
                var row = Add(hud.TeamRowPrefab, hud.TeamList);
                row.Name.text = nick;
                row.Name.color = UiPalette.InkOf(Stealth.Player.PlayerLooks.TeamColor(slot));
                On(row.State, state.Length > 0);
                row.State.text = state;
                row.State.color = UiPalette.Stamp;
                for (int i = 0; i < row.Hearts.Length; i++)
                {
                    On(row.Hearts[i], state.Length == 0 && i < 2);
                    row.Hearts[i].sprite = i < hp ? hud.HeartFull : hud.HeartEmpty;
                    row.Hearts[i].color = i < hp ? UiPalette.Danger : UiPalette.PaperShade;
                }
            }
            hud.Eye.sprite = hud.EyeSprites[3];
            hud.Eye.color = Color.Lerp(UiPalette.PaperShade, UiPalette.MarkYellow, 0.45f);
            hud.Speaker.sprite = hud.SpeakerSprites[2];
            hud.Speaker.color = UiPalette.MarkOrange;
            for (int i = 0; i < hud.Hearts.Length; i++)
            {
                On(hud.Hearts[i], i < 2);
                hud.Hearts[i].sprite = i < 1 ? hud.HeartFull : hud.HeartEmpty;
                hud.Hearts[i].color = i < 1 ? UiPalette.Danger : UiPalette.PaperShade;
            }
            for (int i = 0; i < hud.HackSlots.Length; i++)
            {
                On(hud.HackSlots[i], i < 3);
                bool taken = i == 2;
                hud.HackSlots[i].sprite = taken ? hud.HackKindIcons[0] : hud.HackFree;
                hud.HackSlots[i].color = taken ? UiPalette.PaperShade : UiPalette.MarkBlue;
            }
            Hint(hud, "Tab", null, "Рюкзак", true);
            Hint(hud, "Q", null, "Пометить");
            Hint(hud, "E", null, "Взломать");
            hud.ReticleGlyph.sprite = hud.ReticleSprites[4];
            hud.ReticleGlyph.rectTransform.sizeDelta = new Vector2(30, 30);
            On(hud.ReticleRing);
            hud.ReticleRing.fillAmount = 0.4f;
            On(hud.Banner);
            hud.Banner.alpha = 1f;
            hud.BannerLabel.text = "Сейф открыт: +60 топлива";
            On(hud.AlarmFrame);
            hud.AlarmLabel.text = "ТРЕВОГА · 12";
            foreach (var e in hud.AlarmEdges) e.color = UiPalette.Danger.A(0.5f);
            var marker = Add(hud.SuspicionPrefab, hud.IconLayer);
            marker.Show(new Vector2(820, 420), 1f, true, 0.6f, UiMath.SuspicionColor(0.6f, false, 0f), "?", UiPalette.Caution);
            var marker2 = Add(hud.SuspicionPrefab, hud.IconLayer);
            marker2.Show(new Vector2(470, 400), 0.9f, false, 0f, Color.clear, "!", UiPalette.Danger);
            var callout = Add(hud.CalloutPrefab, hud.CalloutLayer);
            callout.Rect.anchoredPosition = new Vector2(1000, 380);
            callout.Diamond.color = Stealth.Player.PlayerLooks.TeamColor(1);
            callout.Label.text = "14 м";
            callout.Label.color = Stealth.Player.PlayerLooks.TeamColor(1);
            var mate = Add(hud.TeamMarkerPrefab, hud.MarkerLayer);
            mate.Rect.anchoredPosition = new Vector2(330, 330);
            On(mate.Cross);
            mate.Label.text = "Петя · 9 м";
            mate.Label.color = UiPalette.Danger;
            On(hud.HatchMarker);
            hud.HatchMarker.anchoredPosition = new Vector2(1150, 300);
            hud.HatchLabel.text = "18 м";
            hud.Stamina.Set(0.6f, UiPalette.OnWorld.A(0.8f));
            On(hud.Stamina);
            var arc = Add(hud.ArcPrefab, hud.ArcLayer);
            arc.Set(40f, 80f, UiPalette.MarkOrange.A(0.8f));
            var inv = Make("Inventory");
            InventoryCompact(inv.GetComponent<InventoryUI>());
            return new List<GameObject> { go, inv };
        }

        static void InventoryCompact(InventoryUI ui)
        {
            ui.Panel.localScale = Vector3.one * InventoryUI.CompactScale;
            On(ui.BenchPanel, false);
            On(ui.BeltPanel, false);
            On(ui.Tooltip, false);
            ui.ValueLabel.text = "34 очк.";
            Items(ui, false);
        }

        static void Items(InventoryUI ui, bool hand)
        {
            void Item(RectTransform layer, Vector2Int at, Vector2Int cells, Color c, byte shape)
            {
                for (int x = 0; x < cells.x; x++)
                    for (int y = 0; y < cells.y; y++)
                        Add(ui.BlockPrefab, layer).Set(new Vector2((at.x + x) * InventoryUI.Cell + 2, -((at.y + y) * InventoryUI.Cell + 2)), InventoryUI.Cell - 4, c, 1f);
                var icon = ItemIcon(shape);
                if (icon == null) return;
                var img = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
                img.transform.SetParent(layer, false);
                img.sprite = icon;
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0, 1);
                img.rectTransform.sizeDelta = new Vector2(cells.x * InventoryUI.Cell - 2, cells.y * InventoryUI.Cell - 2);
                img.rectTransform.anchoredPosition = new Vector2((at.x + cells.x / 2f) * InventoryUI.Cell, -(at.y + cells.y / 2f) * InventoryUI.Cell);
            }
            Item(ui.ItemLayer, new Vector2Int(0, 0), new Vector2Int(2, 1), ItemCatalog.UiColor(ItemCatalog.Flashlight, 2), ItemCatalog.Flashlight);
            Item(ui.ItemLayer, new Vector2Int(3, 0), new Vector2Int(1, 2), ItemCatalog.UiColor(ItemCatalog.Battery, 0), ItemCatalog.Battery);
            Item(ui.ItemLayer, new Vector2Int(5, 1), new Vector2Int(2, 2), ItemCatalog.UiColor(ItemCatalog.Medkit, 0), ItemCatalog.Medkit);
            if (hand) Item(ui.HandItems, new Vector2Int(0, 0), new Vector2Int(1, 2), ItemCatalog.UiColor(ItemCatalog.Coffee, 0), ItemCatalog.Coffee);
        }

        static List<GameObject> HudTerminal()
        {
            var hud = Hud(out var go);
            hud.HouseLabel.text = "Офис «Ромашка»";
            hud.QuotaLabel.text = "120 / 120";
            hud.QuotaLabel.color = UiPalette.Good;
            hud.QuotaBar.Set(1f, UiPalette.MarkGreen);
            On(hud.HackList);
            hud.HackTitle.text = "Взлом · зарядов: 2";
            var names = new[] { ("Охранник", 0, false, 0f), ("Охранник 2", 0, true, 0f), ("Камера", 7, false, 0.55f), ("Паук", 5, false, 0f), ("Датчик движения", 9, false, 0f), ("Лазер", 10, false, 0f) };
            for (int i = 0; i < names.Length; i++)
            {
                var (n, kind, hacked, share) = names[i];
                var row = Add(hud.HackRowTemplate, hud.HackRows);
                row.Icon.sprite = hud.HackKindPictures[Mathf.Min(kind, hud.HackKindPictures.Length - 1)];
                row.Icon.color = hacked ? Color.white.A(0.4f) : Color.white;
                row.Name.text = n;
                row.Name.color = hacked ? UiPalette.InkFaint : UiPalette.Ink;
                row.State.text = hacked ? "выключен" : share > 0 ? "55%" : "";
                row.State.color = hacked ? UiPalette.InkFaint : UiPalette.Good;
                row.Back.color = i == 2 ? UiPalette.MarkYellow.A(0.75f) : Color.clear;
                row.Fill.rectTransform.anchorMax = new Vector2(share, 1f);
                On(row.Fill, share > 0f);
            }
            On(hud.TerminalLabel);
            hud.TerminalLabel.text = "Камера 2 · 2 / 5";
            Hint(hud, "W", "S", "Выбрать");
            Hint(hud, "E", null, "Взломать");
            return new List<GameObject> { go };
        }

        static List<GameObject> HudMap()
        {
            var hud = Hud(out var go);
            On(hud.BigMap);
            var map = hud.BigMap;
            Call(map, "Awake");
            var layout = LevelGenerator.Generate(7, 3);
            var full = new ExplorationMap(layout);
            full.RevealAll();
            var size = map.Viewport.rect.size;
            float scale = Mathf.Min(size.x / layout.SizeX, size.y / layout.SizeZ) * 0.92f;
            Call(map, "Draw", full, 0, scale);
            map.Content.anchoredPosition = -new Vector2(layout.SizeX, layout.SizeZ) / 2f * scale;
            map.Caption.text = "Этаж 1 из " + full.Floors;
            void Icon(Vector2 at, Color c, string label, int shape)
            {
                var icon = Add(map.IconPrefab, map.IconLayer);
                icon.Rect.anchoredPosition = at * scale;
                On(icon.Arrow, shape == 0);
                On(icon.Dot, shape == 1);
                On(icon.Diamond, shape == 2);
                On(icon.Glyph, shape == 3);
                (shape switch { 0 => icon.Arrow, 1 => icon.Dot, 2 => icon.Diamond, _ => icon.Glyph }).color = c;
                On(icon.Label, label != null);
                if (label != null) { icon.Label.text = label; icon.Label.color = UiPalette.Ink; }
            }
            Icon(new Vector2(layout.HatchPosition.x, layout.HatchPosition.z), UiPalette.MarkBlue, "вход", 2);
            Icon(new Vector2(layout.ExitHatchPosition.x, layout.ExitHatchPosition.z), UiPalette.MarkGreen, "выход", 2);
            Icon(new Vector2(layout.SizeX * 0.4f, layout.SizeZ * 0.5f), UiPalette.Ink, null, 0);
            Icon(new Vector2(layout.SizeX * 0.6f, layout.SizeZ * 0.3f), UiPalette.Stamp, null, 3);
            Icon(new Vector2(layout.SizeX * 0.3f, layout.SizeZ * 0.7f), UiPalette.MarkOrange, null, 1);
            Hint(hud, "A", "D", "Этаж");
            return new List<GameObject> { go };
        }

        static List<GameObject> HudMoments()
        {
            var hud = Hud(out var go);
            hud.HouseLabel.text = "Склад «Север»";
            hud.QuotaLabel.text = "0 / 80";
            hud.QuotaBar.Set(0f, UiPalette.MarkOrange);
            On(hud.EntryBanner);
            hud.EntryBanner.alpha = 1f;
            On(hud.RunOver);
            hud.RunOverTotal.text = "Итого за забег: 412";
            hud.RunOverRestart.text = "R — начать заново";
            On(hud.HatchText);
            hud.HatchText.text = "Ждём: Саша, Лёша";
            On(hud.HatchTint);
            return new List<GameObject> { go };
        }

        static List<GameObject> HudStats()
        {
            var hud = Hud(out var go);
            On(hud.Content, false);
            On(hud.StatsPanel);
            hud.StatsHeader.text = "Итоги: Больница «Мега-Лаб»";
            var rows = new[] { ("Саша", 1, "Кормилец команды", "принёс 140 топлива"), ("Лёша", 2, "Любитель полежать", "падал 3 раза"), ("Петя", 3, "Самый многословный", "говорил 4 минуты") };
            for (int i = 0; i < hud.StatsRows.Length; i++)
            {
                var row = hud.StatsRows[i];
                On(row, i < rows.Length);
                if (i >= rows.Length) continue;
                var (nick, slot, title, did) = rows[i];
                var team = Stealth.Player.PlayerLooks.TeamColor(slot);
                row.Name.text = nick;
                row.Name.color = UiPalette.InkOf(team);
                row.Bar.color = team;
                row.Title.text = title;
                row.Did.text = did;
                row.Numbers.text = "топливо 40 · падений 1 · поднял 2 · 312 м";
            }
            return new List<GameObject> { go };
        }

        static List<GameObject> Safe()
        {
            var hud = Hud(out var go);
            On(hud.Content);
            var view = go.GetComponentInChildren<SafeCrackView>(true);
            On(view.Root);
            Call(view, "Build", 60);
            view.Dial.localRotation = Quaternion.Euler(0, 0, 37f / 60f * 360f);
            view.Readout.text = "37";
            string[] digits = { "37", "--", "" };
            for (int i = 0; i < 3; i++)
            {
                view.SlotTexts[i].text = digits[i];
                view.SlotTexts[i].color = i == 0 ? UiPalette.Good : UiPalette.Pen;
                view.SlotGlows[i].color = i == 0 ? UiPalette.MarkGreen.A(0.6f) : i == 1 ? UiPalette.MarkYellow.A(0.55f) : Color.clear;
                view.SlotLeds[i].color = i == 0 ? UiPalette.MarkGreen : i == 1 ? UiPalette.MarkYellow : UiPalette.PaperShade;
            }
            On(view.Flash, false);
            On(view.FaceFlash, false);
            On(view.Ring, false);
            Call(view, "DrawScope", 0f, 0.7f);
            view.Hint.text = "Мышь — крутить · ЛКМ — попробовать · Пробел — уйти";
            return new List<GameObject> { go };
        }

        static List<GameObject> Vault()
        {
            var hud = Hud(out var go);
            var view = go.GetComponentInChildren<VaultLockView>(true);
            On(view.Root);
            view.Title.text = "Порядок ламп";
            int count = 6;
            for (int i = 0; i < view.Slots.Length; i++)
            {
                On(view.Slots[i], i < count);
                view.Slots[i].anchoredPosition = new Vector2((i - (count - 1) / 2f) * 86f, view.Slots[i].anchoredPosition.y);
                float g = i == 2 ? 1f : 0f;
                view.Lamps[i].color = Color.Lerp(UiPalette.PaperShade, UiPalette.MarkYellow, g);
                view.Halos[i].color = UiPalette.MarkYellow.A(0.55f * g);
            }
            for (int i = 0; i < view.Dots.Length; i++)
            {
                On(view.Dots[i], i < 5);
                On(view.DotGlows[i], i < 5);
                var at = new Vector2((i - 2f) * 26f, view.Dots[i].rectTransform.anchoredPosition.y);
                view.Dots[i].rectTransform.anchoredPosition = at;
                view.DotGlows[i].rectTransform.anchoredPosition = at;
                view.Dots[i].color = i < 2 ? UiPalette.MarkGreen : i == 2 ? UiPalette.MarkYellow : UiPalette.PaperShade;
                view.DotGlows[i].color = i < 2 ? UiPalette.MarkGreen.A(0.5f) : Color.clear;
            }
            On(view.Flash, false);
            view.Hint.text = "1–6 — нажать · Пробел — уйти";
            return new List<GameObject> { go };
        }

        static List<GameObject> Start()
        {
            var hud = Hud(out var go);
            On(hud.Content, false);
            var view = go.GetComponentInChildren<StartScreen>(true);
            On(view.Root);
            var players = new[] { ("Жора", 0, true, false), ("Саша", 1, false, true), ("Лёша", 2, false, false) };
            for (int i = 0; i < view.Slots.Length; i++)
            {
                var v = view.Slots[i];
                On(v, i < players.Length);
                if (i >= players.Length) continue;
                var (nick, slot, mine, ready) = players[i];
                v.Rect.anchoredPosition = new Vector2(400 + i * 240, 330);
                v.Name.text = nick;
                v.Name.color = UiPalette.InkOf(Stealth.Player.PlayerLooks.TeamColor(slot));
                v.ClassName.text = new[] { "Электрик", "Слесарь", "Связист" }[i];
                v.ItemName.text = new[] { "Фонарик", "Кофе", "Бинт" }[i];
                v.ClassIcon.sprite = ItemIcon(ItemCatalog.ClassTools[i % ItemCatalog.ClassTools.Length]);
                v.ItemIcon.sprite = ItemIcon(ItemCatalog.StartItems[i % ItemCatalog.StartItems.Length]);
                foreach (var b in new[] { v.ClassPrev, v.ClassNext, v.ItemPrev, v.ItemNext }) On(b, mine);
                v.ReadyMark.color = ready ? UiPalette.MarkGreen : UiPalette.PaperShade;
                v.ReadyLabel.text = ready ? "Готов" : "Выбирает";
                v.ReadyLabel.color = ready ? UiPalette.Good : UiPalette.InkFaint;
            }
            view.Waiting.text = "Готовы 1 из 3";
            return new List<GameObject> { go };
        }

        /// <summary>The car upgrade bench: the engine half way, the trunk at its top, the kennel short of things, the teleport, the planned two.</summary>
        static List<GameObject> Upgrades()
        {
            var hud = Hud(out var go);
            On(hud.Content, false);
            var view = go.GetComponentInChildren<CarUpgradeScreen>(true);
            On(view.Root);
            var book = new Vehicles.CarUpgradeBook(7, null, null);
            int[] levels = { 1, 3, 0, 0, 0, 0 };
            for (int i = 0; i < view.Cards.Length; i++)
            {
                var v = view.Cards[i];
                var u = (Vehicles.CarUpgrade)i;
                int max = Vehicles.CarUpgrades.MaxLevel(u), level = levels[i];
                bool planned = max == 0, top = !planned && level >= max;
                v.Name.text = Loc.T(Game.GameManager.UpgradeKey(u));
                v.What.text = Loc.F(Game.GameManager.UpgradeKey(u) + ".what", 10);
                v.Now.text = i switch { 0 => Loc.F("upgrade.engine.now", 10), 1 => Loc.F("upgrade.trunk.now", 3), 2 => Loc.T("upgrade.kennel.now.none"), 3 => Loc.T("upgrade.teleport.now.none"), _ => "" };
                for (int b = 0; b < v.Boxes.Length; b++) { On(v.Boxes[b], b < max); On(v.Ticks[b], b < level); }
                On(v.Planned, planned);
                On(v.Done, top);
                var recipe = planned || top ? null : book.Recipe(u, level + 1);
                On(v.Recipe, recipe != null);
                On(v.Raise, recipe != null);
                if (recipe == null) continue;
                bool all = i != 2;
                for (int k = 0; k < v.Things.Length; k++)
                {
                    On(v.Things[k], k < recipe.Length);
                    On(v.Marks[k], k < recipe.Length);
                    if (k >= recipe.Length) continue;
                    v.Things[k].sprite = ItemIcon(recipe[k]);
                    bool got = all || k == 0;
                    v.Marks[k].sprite = got ? view.Tick : view.Cross;
                    v.Marks[k].color = got ? UiPalette.Good : UiPalette.Stamp;
                }
                v.Raise.interactable = all;
                v.RaiseLabel.text = Loc.T(all ? "upgrade.raise" : "upgrade.missing");
            }
            return new List<GameObject> { go };
        }

        static List<GameObject> Inventory()
        {
            var inv = Make("Inventory");
            var ui = inv.GetComponent<InventoryUI>();
            ui.Panel.localScale = Vector3.one;
            On(ui.Header);
            ui.ValueLabel.text = "34 очк.";
            On(ui.BenchPanel);
            On(ui.BeltPanel);
            ui.HandLabel.text = "в руке";
            Items(ui, true);
            On(ui.Tooltip);
            ui.Tooltip.text = "Отвёртка · 5 очк.";
            ui.Tooltip.rectTransform.anchoredPosition = new Vector2(760, 330);
            ui.ResultLabel.text = "Результат неизвестен";
            return new List<GameObject> { inv };
        }

        // ------------------------------------------------------------------ menus and panels

        static List<GameObject> MenuMain()
        {
            var go = Make("Menu");
            var menu = go.GetComponent<GameMenu>();
            On(go.GetComponentInChildren<StudioSplash>(true), false);
            menu.SteamStatus.text = "Steam: Жора";
            menu.LocalCaption.text = "По локальной сети";
            menu.FriendsStatus.text = "Друзья в игре:";
            foreach (var (n, p) in new[] { ("Саша", "2/4"), ("Лёша", "1/4") })
            {
                var row = Add(menu.FriendRowPrefab, menu.FriendList);
                row.Label.text = $"{n} · {p}";
            }
            return new List<GameObject> { go };
        }

        static List<GameObject> MenuPause()
        {
            var go = Make("Menu");
            var menu = go.GetComponent<GameMenu>();
            On(go.GetComponentInChildren<StudioSplash>(true), false);
            On(menu.MainPage, false);
            On(menu.PausePage);
            menu.Role.text = "Хост · 3 игрока";
            On(menu.KeysPanel);
            try { Call(menu.KeysPanel, "Build"); } catch (Exception e) { Debug.LogWarning("[UiSheet] controls: " + e.Message); }
            menu.KeysPanel.Hint.text = "Нажмите на клавишу, чтобы переназначить";
            return new List<GameObject> { go };
        }

        static List<GameObject> MenuSettings()
        {
            var go = Make("Menu");
            var menu = go.GetComponent<GameMenu>();
            On(go.GetComponentInChildren<StudioSplash>(true), false);
            On(menu.MainPage, false);
            On(menu.SettingsPage);
            menu.SensitivityLabel.text = "Чувствительность мыши: 0.12";
            menu.FovLabel.text = "Поле зрения: 80°";
            menu.VolumeLabel.text = "Громкость: 80%";
            menu.MusicLabel.text = "Музыка: 50%";
            menu.Sensitivity.value = 0.12f;
            menu.Fov.value = 80;
            menu.Volume.value = 0.8f;
            menu.Music.value = 0.5f;
            menu.InvertY.isOn = false;
            menu.HeadBob.isOn = true;
            menu.Language.GetComponentInChildren<Text>().text = "Язык: русский";
            menu.VoiceMode.GetComponentInChildren<Text>().text = "Голос: по нажатию V";
            menu.VoiceDevice.GetComponentInChildren<Text>().text = "Микрофон: по умолчанию";
            menu.MicSensitivityLabel.text = "Чувствительность: 40%";
            menu.MicLevelLabel.text = "Уровень микрофона";
            menu.MicLevel.value = 0.55f;
            menu.VoiceVolumeLabel.text = "Громкость голосов: 100%";
            menu.VoiceVolume.value = 1f;
            return new List<GameObject> { go };
        }

        static List<GameObject> Cheats()
        {
            Hud(out var go);
            var cheats = go.GetComponentInChildren<CheatPanel>(true);
            On(cheats.Root);
            cheats.Invulnerable.GetComponentInChildren<Text>().text = "Неуязвимость: выкл";
            cheats.Invisible.GetComponentInChildren<Text>().text = "Невидимость: вкл";
            return new List<GameObject> { go };
        }

        static List<GameObject> Styles()
        {
            Hud(out var go);
            var styles = go.GetComponentInChildren<StylePanel>(true);
            On(styles.Root);
            int i = 0;
            foreach (var n in new[] { "Тихий комплекс", "Заброшенный", "Пиксели", "Комикс", "Нуар", "VHS", "Чертёж", "Акварель" })
            {
                var b = Add(styles.Template, styles.List);
                b.GetComponentInChildren<Text>().text = n;
                if (i++ == 0) b.image.color = styles.CurrentColor;
            }
            return new List<GameObject> { go };
        }

        // ------------------------------------------------------------------ the world's signs

        static void WorldShot()
        {
            GameObject board = null, sign = null;
            try
            {
                s_Back.enabled = false;
                board = Make("StashBoard");
                board.transform.position = new Vector3(-1.4f, 1.6f, 4f);
                sign = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Ui}/Elements/RouteSign.prefab"));
                sign.transform.position = new Vector3(1.6f, 1.8f, 4f);
                var view = sign.GetComponent<RouteSignView>();
                view.Title.text = "Больница «Мега-Лаб»";
                view.Body.text = "Заложник · 3 этажа · 6 роботов";
                view.Votes.text = "";
                view.Frame.color = UiPalette.MarkBlue;
                var b = board.GetComponent<StashBoard>();
                b.ValueLabel.text = "Склад: 86 очк.";
                b.StashAllLabel.text = "G — сложить всё";
                foreach (var c in new[] { board.GetComponent<Canvas>(), sign.GetComponent<Canvas>() }) c.worldCamera = s_Camera;
                s_Camera.transform.position = Vector3.zero;
                s_Camera.transform.rotation = Quaternion.identity;
                s_Camera.fieldOfView = 60f;
                s_Camera.cullingMask = ~0;
                Capture("world", new Vector2Int(1600, 900), new List<GameObject> { board, sign });
            }
            catch (Exception e)
            {
                Debug.LogError($"[UiSheet] world: {e}");
            }
            finally
            {
                if (board != null) UnityEngine.Object.DestroyImmediate(board);
                if (sign != null) UnityEngine.Object.DestroyImmediate(sign);
            }
        }
    }
}
