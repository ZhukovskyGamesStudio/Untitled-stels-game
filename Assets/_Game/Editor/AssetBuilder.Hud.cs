using System.Linq;
using Stealth.Game;
using Stealth.Items;
using Stealth.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.EditorTools
{
    /// <summary>The HUD prefab (UI/HUD): overlays, the reticle, the hint stack, the status icons, map and markers.</summary>
    public static partial class AssetBuilder
    {
        // ------------------------------------------------------------------ HUD

        const string IconsDir = SpritesDir + "/Icons";

        /// <summary>A PNG from <c>Art/Sprites/Icons</c> (drawn by Tools/Art/generate_icons.py) as a sprite.</summary>
        static Sprite Icon(string name, Vector4 border = default)
        {
            var path = $"{IconsDir}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.IO.FileNotFoundException($"{path}: run python Tools/Art/generate_icons.py");
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp
                || importer.spriteBorder != border || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static readonly string[] ReticleIconNames =
        {
            null, "ret_hand", "ret_lock", "ret_key", "ret_hack", "ret_revive", "ret_heal", "ret_search", "ret_seat",
            "ret_fuel", "ret_terminal", "ret_craft", "ret_lever", "ret_safe", "ret_crate", "ret_door", "ret_vent",
        };

        /// <summary>A key cap: a 9-sliced cap that grows with the key's name, or a little mouse instead.</summary>
        static KeyCapView KeyCap(Transform parent, string name)
        {
            var root = R(parent, name, Center, Center, Vector2.zero, new Vector2(22, 22));
            var view = root.gameObject.AddComponent<KeyCapView>();
            var h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            var cap = R(root, "Cap", Center, Center, Vector2.zero, new Vector2(22, 22));
            view.Cap = Img(cap, Color.white, Icon("keycap", new Vector4(16, 16, 16, 16)));
            view.Cap.type = Image.Type.Sliced;
            view.Cap.pixelsPerUnitMultiplier = 2.2f; // the 48 px cap drawn at ~22 px
            var ch = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.padding = new RectOffset(6, 6, 1, 3);
            ch.childAlignment = TextAnchor.MiddleCenter;
            ch.childControlWidth = ch.childControlHeight = true;
            ch.childForceExpandWidth = ch.childForceExpandHeight = false; // or the cap reports itself flexible and stretches
            var cle = cap.gameObject.AddComponent<LayoutElement>();
            cle.minWidth = 22;
            cle.minHeight = cle.preferredHeight = 22;
            view.Key = Txt(R(cap, "Key", Center, Center, Vector2.zero, Vector2.zero), "E", 12, TextAnchor.MiddleCenter, Color.white, true, shadow: false);

            var mouse = R(root, "Mouse", Center, Center, Vector2.zero, new Vector2(22, 22));
            view.Mouse = Img(mouse, Color.white, Icon("mouse_lmb"));
            var mle = mouse.gameObject.AddComponent<LayoutElement>();
            mle.minWidth = mle.preferredWidth = 22;
            mle.minHeight = mle.preferredHeight = 22;
            mouse.gameObject.SetActive(false);
            view.Lmb = Icon("mouse_lmb");
            view.Rmb = Icon("mouse_rmb");
            view.Mmb = Icon("mouse_mmb");
            view.Wheel = Icon("mouse_wheel");
            return view;
        }

        /// <summary>One line of the hint stack: [key][key] word, right-aligned, half transparent.</summary>
        static GameObject BuildHintRow()
        {
            var root = R(null, "HintRow", Center, Center, Vector2.zero, new Vector2(200, 24));
            var view = root.gameObject.AddComponent<HintView>();
            view.Group = root.gameObject.AddComponent<CanvasGroup>();
            view.Group.blocksRaycasts = false;
            view.Group.interactable = false;
            var h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4;
            h.childAlignment = TextAnchor.MiddleRight;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            // the row is as wide as the stack (the stack sizes it): its content sits at the right edge
            view.First = KeyCap(root, "Key1");
            view.Second = KeyCap(root, "Key2");
            view.Label = Txt(R(root, "Label", Center, Center, Vector2.zero, Vector2.zero), "—", 14, TextAnchor.MiddleLeft, Color.white);
            view.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var lle = view.Label.gameObject.AddComponent<LayoutElement>();
            lle.minHeight = 22;
            return root.gameObject;
        }

        /// <summary>
        /// The house's clock, top middle (after Risk of Rain's difficulty timer): the time counting up, the clock's second the
        /// next extra robot comes at with a robot's picture, a bar from the last one to the next; the panel tints red.
        /// </summary>
        static void BuildHouseClock(GameHUD hud, Transform content)
        {
            // top left under the house's panel (Quota: 16, -16, 300 x 100), as wide: all the house's news in one column
            var panel = R(content, "HouseClock", TopLeft, TopLeft, new Vector2(16, -120), new Vector2(300, 48));
            hud.ClockPanel = Img(panel, new Color(0.22f, 0.55f, 0.85f, 0.82f));
            Img(Stretch(panel, "Shade", 2f), new Color(0f, 0f, 0f, 0.35f));
            hud.ClockTime = Txt(R(panel, "Time", TopLeft, TopLeft, new Vector2(10, -1), new Vector2(120, 34)), "0:00", 26, TextAnchor.MiddleLeft, Color.white, true);
            Img(R(panel, "Robot", TopRight, TopRight, new Vector2(-74, -6), new Vector2(22, 22)), new Color(1f, 1f, 1f, 0.9f), Icon("robot_guard"));
            hud.ClockNext = Txt(R(panel, "Next", TopRight, TopRight, new Vector2(-10, -1), new Vector2(60, 34)), "3:00", 17, TextAnchor.MiddleRight, Color.white, true);
            hud.ClockCount = Txt(R(panel, "Count", TopRight, TopRight, new Vector2(-100, -4), new Vector2(36, 20)), "", 12, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.75f), true);
            hud.ClockBar = Bar(panel, "ToNext", BottomLeft, BottomLeft, new Vector2(8, 6), new Vector2(284, 5), new Color(0f, 0f, 0f, 0.45f), Color.white, 1f);
            // the marker over the bar's end: where the next robot is on the way
            var marker = R(panel, "Marker", new Vector2(1, 0), Center, new Vector2(-8, 8.5f), new Vector2(8, 8));
            marker.localEulerAngles = new Vector3(0, 0, 45);
            Img(marker, Color.white);
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// The tablet's screen: a world-space canvas with a minimap (what the team has seen, following the player) that
        /// <see cref="GameHUD"/> lays on the screen of the tablet model in the hand (detached from the HUD on start). On the
        /// WorldText layer: drawn after the style filter, readable in every style.
        /// </summary>
        static void BuildTabletScreen(UiElements e, GameHUD hud, Transform root)
        {
            var screen = R(root, "TabletScreen", Center, Center, Vector2.zero, new Vector2(GameHUD.TabletScreenWidth, 260));
            // (its world-space mode is set by GameHUD once detached: a nested canvas's renderMode is its root's — set here, it
            // turned the whole HUD into a world-space canvas)
            screen.gameObject.AddComponent<Canvas>();
            screen.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var map = BuildMap(e, screen, "Map", true, Center, Center, Vector2.zero, new Vector2(GameHUD.TabletScreenWidth, 260), 5f);
            var mr = (RectTransform)map.transform;
            mr.anchorMin = Vector2.zero;
            mr.anchorMax = Vector2.one;
            mr.offsetMin = mr.offsetMax = Vector2.zero;
            map.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.09f, 1f);
            hud.TabletScreen = screen;
            hud.TabletMap = map;
            SetLayerRecursive(screen.gameObject, Stealth.Level.LevelMetrics.WorldTextLayer);
            screen.gameObject.SetActive(false);
        }

        /// <summary>
        /// The hacking terminal's list in the middle of the screen: a title with the charges and up to
        /// <see cref="GameHUD.HackRowsShown"/> rows (kind icon, name, state, the hold's fill), copies of a hidden row.
        /// </summary>
        static void BuildHackList(GameHUD hud, Transform content)
        {
            const float w = 420f, rowH = 28f, head = 40f;
            var panel = R(content, "HackList", Center, Center, new Vector2(0, 20), new Vector2(w, head + GameHUD.HackRowsShown * rowH + 10f));
            Img(panel, new Color(0.02f, 0.05f, 0.04f, 0.88f));
            Img(R(panel, "Rule", TopCenter, TopCenter, new Vector2(0, -head + 4), new Vector2(w - 20, 2)), new Color(0.45f, 1f, 0.55f, 0.5f));
            hud.HackTitle = Txt(R(panel, "Title", TopCenter, TopCenter, new Vector2(0, -4), new Vector2(w - 20, 30)), "—", 17, TextAnchor.MiddleCenter, Color.white, true);
            var rows = R(panel, "Rows", TopLeft, TopLeft, new Vector2(10, -head - 2), new Vector2(w - 20, GameHUD.HackRowsShown * rowH));
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            hud.HackRows = rows;
            var row = R(rows, "Row", TopLeft, TopLeft, Vector2.zero, new Vector2(w - 20, rowH - 2));
            var view = row.gameObject.AddComponent<HackRowView>();
            view.Back = Img(row, new Color(1f, 1f, 1f, 0.04f));
            var fill = Stretch(row, "Fill");
            fill.anchorMax = new Vector2(0f, 1f);
            view.Fill = Img(fill, new Color(1f, 0.85f, 0.4f, 0.25f));
            view.Icon = Img(R(row, "Icon", Left, Left, new Vector2(6, 0), new Vector2(20, 20)), Color.white);
            view.Icon.preserveAspect = true;
            view.Name = Txt(R(row, "Name", Left, Left, new Vector2(34, 0), new Vector2(250, rowH - 2)), "—", 14, TextAnchor.MiddleLeft, Color.white, true);
            view.State = Txt(R(row, "State", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(110, rowH - 2)), "", 13, TextAnchor.MiddleRight, Color.white);
            row.gameObject.SetActive(false);
            hud.HackRowTemplate = view;
            hud.HackKindPictures = RobotKindIcons();
            panel.gameObject.SetActive(false);
            hud.HackList = panel.gameObject;
        }

        /// <summary>The team is up in the house and its clock starts: a big notice across the screen (fades in and out).</summary>
        static void BuildEntryBanner(GameHUD hud, Transform root)
        {
            var band = R(root, "EntryBanner", Center, Center, new Vector2(0, 90), new Vector2(4000, 150));
            hud.EntryBanner = band.gameObject.AddComponent<CanvasGroup>();
            hud.EntryBanner.blocksRaycasts = false;
            hud.EntryBanner.interactable = false;
            Img(band, new Color(0f, 0f, 0f, 0.55f)).raycastTarget = false;
            Img(R(band, "LineTop", TopCenter, TopCenter, Vector2.zero, new Vector2(4000, 3)), new Color(0.45f, 1f, 0.55f, 0.8f));
            Img(R(band, "LineBottom", BottomCenter, BottomCenter, Vector2.zero, new Vector2(4000, 3)), new Color(0.45f, 1f, 0.55f, 0.8f));
            hud.EntryTitle = Txt(R(band, "Title", Center, Center, new Vector2(0, 0), new Vector2(1200, 80)), L("hud.entry.title"), 58, TextAnchor.MiddleCenter, Color.white, true);
            hud.EntrySub = Txt(R(band, "Sub", Center, Center, new Vector2(0, -42), new Vector2(1200, 34)), "", 20, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.8f), true);
            hud.EntrySub.gameObject.SetActive(false); // the notice is the title alone
            band.gameObject.SetActive(false);
        }

        /// <summary>The drive's panel: the last house's titles, a row per player.</summary>
        static void BuildStatsPanel(GameHUD hud, Transform root)
        {
            var panel = R(root, "HouseStats", Center, Center, new Vector2(0, 30), new Vector2(760, 430));
            hud.StatsPanel = panel.gameObject;
            Img(panel, new Color(0.03f, 0.04f, 0.05f, 0.82f)).raycastTarget = false;
            Img(R(panel, "Rule", TopCenter, TopCenter, new Vector2(0, -60), new Vector2(700, 2)), new Color(1f, 0.84f, 0.35f, 0.6f));
            hud.StatsHeader = Txt(R(panel, "Header", TopCenter, TopCenter, new Vector2(0, -12), new Vector2(720, 44)), "—", 26, TextAnchor.MiddleCenter, Color.white, true);
            hud.StatsRows = new StatsRowView[4];
            for (int i = 0; i < 4; i++)
            {
                var row = R(panel, $"Row{i}", TopCenter, TopCenter, new Vector2(0, -74 - i * 88), new Vector2(720, 80));
                Img(row, new Color(1f, 1f, 1f, 0.05f)).raycastTarget = false;
                var view = row.gameObject.AddComponent<StatsRowView>();
                view.Bar = Img(R(row, "Bar", Left, Left, Vector2.zero, new Vector2(6, 80)), Color.white);
                view.Name = Txt(R(row, "Name", TopLeft, TopLeft, new Vector2(18, -4), new Vector2(220, 26)), "Name", 17, TextAnchor.MiddleLeft, Color.white, true);
                view.Numbers = Txt(R(row, "Numbers", BottomLeft, BottomLeft, new Vector2(18, 6), new Vector2(240, 40)), "—", 12, TextAnchor.LowerLeft, Color.white);
                view.Numbers.horizontalOverflow = HorizontalWrapMode.Wrap;
                view.Title = Txt(R(row, "Title", TopLeft, TopLeft, new Vector2(260, -2), new Vector2(450, 40)), "—", 26, TextAnchor.MiddleLeft, Color.white, true);
                view.Did = Txt(R(row, "Did", BottomLeft, BottomLeft, new Vector2(260, 8), new Vector2(450, 26)), "—", 15, TextAnchor.MiddleLeft, Color.white);
                hud.StatsRows[i] = view;
            }
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// The run's start screen: a column per player under their figure in the row (name, the class's tool and the extra
        /// item with arrows, ready), a title, the ready button and who is still choosing. Outside the HUD's content (it fades out).
        /// </summary>
        static void BuildStartScreen(UiElements e, Transform root)
        {
            // the presenter sits on an always-active holder: it switches its child on and off (switching itself off, its
            // Update never ran again — the screen never showed)
            var holder = Stretch(root, "StartScreen");
            // its own canvas with a raycaster: the HUD's takes no clicks (like the cheat panel)
            holder.gameObject.AddComponent<Canvas>();
            holder.gameObject.AddComponent<GraphicRaycaster>();
            var view = holder.gameObject.AddComponent<StartScreen>();
            var screen = Stretch(holder, "Screen");
            view.Root = screen.gameObject;
            view.Canvas = (RectTransform)root;
            view.Visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>(DataDir + "/ItemVisuals.asset");
            Txt(R(screen, "Title", TopCenter, TopCenter, new Vector2(0, -24), new Vector2(900, 44)), L("start.title"), 30, TextAnchor.MiddleCenter, Color.white, true);
            var layer = R(screen, "Slots", BottomLeft, BottomLeft, Vector2.zero, Vector2.zero);
            view.Slots = new StartSlotView[4];
            for (int i = 0; i < 4; i++)
            {
                var col = R(layer, $"Slot{i}", BottomLeft, TopCenter, new Vector2(320 + i * 220, 300), new Vector2(200, 188));
                var slot = col.gameObject.AddComponent<StartSlotView>();
                slot.Rect = col;
                Img(col, new Color(0.03f, 0.04f, 0.05f, 0.78f));
                slot.Name = Txt(R(col, "Name", TopCenter, TopCenter, new Vector2(0, -4), new Vector2(190, 24)), "Player", 16, TextAnchor.MiddleCenter, Color.white, true);
                // the class: its tool's picture between arrows, its name under it
                // (centred pivots: an Image keeping its aspect hugs its pivot — a top pivot pushed the pictures up)
                slot.ClassIcon = Img(R(col, "ClassIcon", TopCenter, Center, new Vector2(0, -58), new Vector2(96, 46)), Color.white);
                slot.ClassIcon.preserveAspect = true;
                slot.ClassPrev = Arrow(e, col, "ClassPrev", new Vector2(-74, -58), "arrow_left");
                slot.ClassNext = Arrow(e, col, "ClassNext", new Vector2(74, -58), "arrow_right");
                slot.ClassName = Txt(R(col, "ClassName", TopCenter, TopCenter, new Vector2(0, -82), new Vector2(196, 20)), "—", 13, TextAnchor.MiddleCenter, Color.white, true);
                slot.ItemIcon = Img(R(col, "ItemIcon", TopCenter, Center, new Vector2(0, -124), new Vector2(96, 38)), Color.white);
                slot.ItemIcon.preserveAspect = true;
                slot.ItemPrev = Arrow(e, col, "ItemPrev", new Vector2(-74, -124), "arrow_left");
                slot.ItemNext = Arrow(e, col, "ItemNext", new Vector2(74, -124), "arrow_right");
                slot.ItemName = Txt(R(col, "ItemName", TopCenter, TopCenter, new Vector2(0, -146), new Vector2(196, 18)), "—", 12, TextAnchor.MiddleCenter, Color.white);
                slot.ItemName.resizeTextForBestFit = true;
                slot.ItemName.resizeTextMinSize = 9;
                slot.ItemName.resizeTextMaxSize = 12;
                slot.ReadyMark = Img(R(col, "ReadyMark", BottomCenter, BottomCenter, new Vector2(0, 0), new Vector2(200, 26)), new Color(1f, 1f, 1f, 0.15f));
                slot.ReadyLabel = Txt(R(col, "ReadyLabel", BottomCenter, BottomCenter, new Vector2(0, 0), new Vector2(196, 26)), "—", 13, TextAnchor.MiddleCenter, Color.white, true);
                view.Slots[i] = slot;
            }
            view.ReadyButton = Instance<Button>(e.Button, screen, "Ready");
            var rb = (RectTransform)view.ReadyButton.transform;
            // bottom right, under the columns of the figures (PickingRules frames the feet above it)
            rb.anchorMin = rb.anchorMax = new Vector2(1, 0);
            rb.pivot = new Vector2(1, 0);
            rb.anchoredPosition = new Vector2(-16, 16);
            rb.sizeDelta = new Vector2(180, 40);
            view.ReadyButtonLabel = view.ReadyButton.GetComponentInChildren<Text>();
            SetText(view.ReadyButtonLabel, L("start.ready"));
            view.Waiting = Txt(R(screen, "Waiting", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-206, 16), new Vector2(200, 40)), "—", 15, TextAnchor.MiddleRight, Color.white);
            screen.gameObject.SetActive(false);
        }

        /// <summary>
        /// A small square arrow button of the start screen: a drawn chevron (Tools/Art/generate_icons.py), lights up in the
        /// team's orange and grows under the mouse (<see cref="UiHover"/>).
        /// </summary>
        static Button Arrow(UiElements e, RectTransform parent, string name, Vector2 pos, string icon)
        {
            var b = Instance<Button>(e.Button, parent, name);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = TopCenter;
            rt.pivot = Center;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(34, 34);
            var colors = b.colors;
            colors.normalColor = new Color(0.2f, 0.22f, 0.26f, 0.95f);
            colors.highlightedColor = new Color(0.95f, 0.55f, 0.2f, 1f);
            colors.pressedColor = new Color(0.7f, 0.38f, 0.12f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.fadeDuration = 0.06f;
            b.colors = colors;
            var t = b.GetComponentInChildren<Text>();
            if (t != null) { t.text = ""; t.enabled = false; } // (a nested prefab instance: its label cannot be removed)
            var glyph = Img(R(rt, "Glyph", Center, Center, Vector2.zero, new Vector2(22, 22)), Color.white, Icon(icon));
            glyph.raycastTarget = false;
            b.gameObject.AddComponent<UiHover>();
            return b;
        }

        static GameObject BuildHud(UiElements e)
        {
            var root = CanvasRoot("HUD", 0, false);
            var hud = root.AddComponent<GameHUD>();
            hud.Canvas = (RectTransform)root.transform;
            var content = Stretch(root.transform, "Content");
            hud.Content = content.gameObject;
            // fades out for the drive scene; it stays interactable: the cheat panel and other buttons live under it
            hud.ContentGroup = content.gameObject.AddComponent<CanvasGroup>();
            var white = new Color(1, 1, 1, 0.8f);

            // Overlays (full screen, hidden until needed).
            var overlays = Stretch(content, "Overlays");
            hud.LowHpVignette = Img(Stretch(overlays, "LowHpVignette"), new Color(0.8f, 0, 0, 0.35f), S("Vignette"));
            hud.HitFlash = Img(Stretch(overlays, "HitFlash"), new Color(1, 0, 0, 0.2f));
            hud.GasVignette = Img(Stretch(overlays, "GasVignette"), new Color(0.4f, 0.95f, 0.3f, 0.5f), S("Vignette"));
            hud.GasTint = Img(Stretch(overlays, "GasTint"), new Color(0.5f, 0.9f, 0.3f, 0.1f));
            hud.SpeedLines = Img(Stretch(overlays, "SpeedLines"), new Color(1f, 0.95f, 0.85f, 0f), UiSprite("speed_lines"));
            hud.SpeedLines.raycastTarget = false;
            hud.SpeedLines.gameObject.SetActive(false);
            hud.LaughText = Txt(R(overlays, "Laugh", TopCenter, TopCenter, new Vector2(0, -216), new Vector2(400, 50)), "HA-HA", 30, TextAnchor.MiddleCenter, new Color(0.8f, 1f, 0.6f, 0.9f), true);
            hud.GasText = Txt(R(overlays, "GasTimer", TopCenter, TopCenter, new Vector2(0, -256), new Vector2(120, 30)), "10", 22, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.6f), true);
            hud.DownedTint = Img(Stretch(overlays, "DownedTint"), new Color(0.25f, 0f, 0f, 0.55f));
            hud.DownedText = Txt(R(overlays, "Downed", TopCenter, TopCenter, new Vector2(0, -90), new Vector2(600, 30)), "—", 18, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.85f), true);
            // In the hatch: peeking out at floor level, the rim of the hatch dark around the view.
            hud.HatchTint = Img(Stretch(overlays, "HatchTint"), new Color(0f, 0f, 0f, 1f), S("Vignette"));
            hud.HatchText = Txt(R(overlays, "HatchText", TopCenter, TopCenter, new Vector2(0, -120), new Vector2(700, 40)), "—", 20, TextAnchor.MiddleCenter, new Color(0.7f, 1f, 0.75f, 0.9f), true);
            hud.RouteCountdown = Txt(R(overlays, "RouteCountdown", Center, Center, new Vector2(0, 120), new Vector2(300, 100)), "3", 72, TextAnchor.MiddleCenter, new Color(0.6f, 1f, 0.7f, 0.9f), true);
            var alarm = Stretch(overlays, "Alarm");
            hud.AlarmFrame = alarm.gameObject;
            var red = new Color(1f, 0.15f, 0.1f, 0.5f);
            hud.AlarmEdges = new[]
            {
                Img(R(alarm, "Top", TopCenter, TopCenter, Vector2.zero, new Vector2(4000, 4)), red),
                Img(R(alarm, "Bottom", BottomCenter, BottomCenter, Vector2.zero, new Vector2(4000, 4)), red),
                Img(R(alarm, "Left", Left, Left, Vector2.zero, new Vector2(4, 4000)), red),
                Img(R(alarm, "Right", new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(4, 4000)), red),
            };
            var tag = R(alarm, "Tag", TopCenter, TopCenter, new Vector2(0, -14), new Vector2(140, 24));
            Img(tag, new Color(0.5f, 0f, 0f, 0.6f));
            hud.AlarmLabel = Txt(Stretch(tag, "Label"), "ALARM · 6", 14, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.8f), true);

            // The reticle: a dot, or the glyph of what E would do; a ring fills while E is held.
            hud.ArcLayer = R(content, "Arcs", Center, Center, Vector2.zero, Vector2.zero);
            hud.ArcPrefab = e.Arc;
            hud.HitArc = Instance<ArcView>(e.Arc.gameObject, hud.ArcLayer, "HitArc");
            hud.ReticleGlyph = Img(R(content, "Reticle", Center, Center, Vector2.zero, new Vector2(5, 5)), white, S("Disc"));
            hud.ReticleGlyph.preserveAspect = true;
            hud.ReticleRing = Img(R(content, "ReticleRing", Center, Center, Vector2.zero, new Vector2(44, 44)), new Color(1, 1, 1, 0.85f), S("Ring"));
            hud.ReticleRing.type = Image.Type.Filled;
            hud.ReticleRing.fillMethod = Image.FillMethod.Radial360;
            hud.ReticleRing.fillOrigin = (int)Image.Origin360.Top;
            hud.ReticleRing.fillClockwise = true;
            hud.ReticleSprites = ReticleIconNames.Select(n => n == null ? S("Disc") : Icon(n)).ToArray();
            hud.Stamina = Bar(content, "Stamina", Center, TopCenter, new Vector2(0, -34), new Vector2(120, 6), new Color(0, 0, 0, 0.45f), white, 0f);

            // The hint stack, bottom right: the keys of the moment, one above the other, the most important at the bottom.
            hud.HintStack = R(content, "Hints", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 18), new Vector2(400, 0));
            var stack = hud.HintStack.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 5;
            stack.childAlignment = TextAnchor.LowerRight;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            hud.HintStack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            hud.HintPrefab = Prefab(ElementsDir + "/HintRow.prefab", BuildHintRow).GetComponent<HintView>();

            // World markers.
            hud.IconLayer = Stretch(content, "RobotIcons");
            hud.SuspicionPrefab = e.SuspicionMarker;
            hud.MarkerLayer = Stretch(content, "TeamMarkers");
            hud.TeamMarkerPrefab = e.TeamMarker;
            hud.CalloutLayer = Stretch(content, "Callouts");
            hud.CalloutPrefab = e.Callout;
            hud.HatchMarker = R(content, "HatchMarker", BottomLeft, Center, new Vector2(640, 360), Vector2.zero);
            var diamond = R(hud.HatchMarker, "Diamond", Center, Center, Vector2.zero, new Vector2(14, 14));
            diamond.localEulerAngles = new Vector3(0, 0, 45);
            hud.HatchDiamond = Img(diamond, new Color(0.45f, 1f, 0.5f, 0.8f));
            hud.HatchLabel = Txt(R(hud.HatchMarker, "Label", Center, Center, new Vector2(0, -19), new Vector2(120, 18)), "12 m", 12, TextAnchor.MiddleCenter, new Color(0.45f, 1f, 0.5f));

            // Bottom left: the eye (how lit = how visible), the speaker (how loud), the hearts under them. No words.
            var status = R(content, "Status", BottomLeft, BottomLeft, new Vector2(18, 16), new Vector2(130, 56));
            hud.Eye = Img(R(status, "Eye", BottomLeft, BottomLeft, new Vector2(0, 24), new Vector2(32, 32)), Color.white, Icon("eye_0"));
            hud.EyeSprites = Enumerable.Range(0, 8).Select(i => Icon($"eye_{i}")).ToArray();
            hud.Speaker = Img(R(status, "Speaker", BottomLeft, BottomLeft, new Vector2(38, 24), new Vector2(32, 32)), Color.white, Icon("noise_0"));
            hud.SpeakerSprites = Enumerable.Range(0, 4).Select(i => Icon($"noise_{i}")).ToArray();
            hud.HeartFull = Icon("heart_full");
            hud.HeartEmpty = Icon("heart_empty");
            hud.Hearts = Enumerable.Range(0, Stealth.Player.HealthState.MaxHp + 1) // + the med table's extra point
                .Select(i => Img(R(status, $"Heart{i}", BottomLeft, BottomLeft, new Vector2(2 + i * 22, 0), new Vector2(20, 20)), new Color(0.95f, 0.25f, 0.25f), hud.HeartFull)).ToArray();
            // The team's hack slots right of the speaker: a blue bolt when free, the grey picture of what is switched off
            // when taken; the leftmost is the next to go (GameHUD.ShowHackSlots)
            hud.HackSlots = Enumerable.Range(0, 4)
                .Select(i => Img(R(status, $"Hack{i}", BottomLeft, BottomLeft, new Vector2(76 + i * 28, 28), new Vector2(26, 26)), Color.white, Icon("ret_hack"))).ToArray();
            foreach (var slot in hud.HackSlots) slot.preserveAspect = true;
            hud.HackFree = Icon("ret_hack");
            // tired arms (the hammer, once per house) right of the hearts
            hud.TiredArms = Img(R(status, "TiredArms", BottomLeft, BottomLeft, new Vector2(72, 0), new Vector2(22, 22)), new Color(1f, 0.75f, 0.35f), Icon("tired_arms"));
            hud.TiredArms.preserveAspect = true;
            hud.TiredArms.gameObject.SetActive(false);
            hud.HackKindIcons = RobotKindIcons();

            // Fuel panel, top left: the house's name, a drop, the tank / what the drive burns, the tasks under it.
            var quota = R(content, "Quota", TopLeft, TopLeft, new Vector2(16, -16), new Vector2(300, 100));
            Img(quota, PanelColor);
            hud.HouseLabel = Txt(R(quota, "House", TopLeft, TopLeft, new Vector2(10, -2), new Vector2(280, 22)), "House", 14, TextAnchor.MiddleLeft, Color.white, true);
            hud.HouseLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            Img(R(quota, "FuelIcon", TopLeft, TopLeft, new Vector2(8, -24), new Vector2(18, 18)), new Color(1f, 0.8f, 0.2f, 0.9f), Icon("ret_fuel"));
            hud.QuotaLabel = Txt(R(quota, "Count", TopLeft, TopLeft, new Vector2(10, -23), new Vector2(280, 20)), "0 / 100", 14, TextAnchor.MiddleRight, Color.white, true);
            hud.QuotaBar = Bar(quota, "Bar", TopLeft, TopLeft, new Vector2(30, -29), new Vector2(200, 8), new Color(0, 0, 0, 0.5f), new Color(1f, 0.8f, 0.2f), 2f);
            hud.GainRect = R(quota, "Gain", TopLeft, Left, new Vector2(GameHUD.GainX, GameHUD.GainY), new Vector2(120, 26));
            hud.GainLabel = Txt(hud.GainRect, "+20", 18, TextAnchor.MiddleLeft, new Color(0.45f, 1f, 0.5f), true);
            hud.ObjectiveLabel = Txt(R(quota, "Objective", TopLeft, TopLeft, new Vector2(10, -44), new Vector2(280, 52)), "", 12, TextAnchor.UpperLeft, Color.white, true); // the task list, a line each
            hud.TerminalLabel = Txt(R(content, "Terminal", BottomCenter, BottomCenter, new Vector2(0, 150), new Vector2(700, 24)), "1 / 3", 15, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.8f, 0.9f), true);
            BuildHackList(hud, content);

            // The team list top right (no minimap there: the map is a terminal's, TerminalKind.Map).
            hud.TeamList = R(content, "Team", TopRight, TopRight, new Vector2(-16, -16), new Vector2(244, 0));
            var team = hud.TeamList.gameObject.AddComponent<VerticalLayoutGroup>();
            team.childControlHeight = true;
            team.childControlWidth = true;
            team.childForceExpandHeight = false;
            team.childAlignment = TextAnchor.UpperRight;
            hud.TeamRowPrefab = e.TeamRow;

            // Banner, run over.
            hud.BannerRect = R(content, "Banner", TopCenter, TopCenter, new Vector2(0, -115), new Vector2(640, 40));
            hud.Banner = hud.BannerRect.gameObject.AddComponent<CanvasGroup>();
            hud.Banner.blocksRaycasts = false;
            Img(hud.BannerRect, new Color(0, 0, 0, 0.5f));
            hud.BannerLabel = Txt(Stretch(hud.BannerRect, "Label"), "—", 16, TextAnchor.MiddleCenter, Color.white, true);

            var over = R(content, "RunOver", TopCenter, TopCenter, new Vector2(0, -274), new Vector2(520, 120));
            Img(over, new Color(0, 0, 0, 0.8f));
            Txt(R(over, "Title", TopCenter, TopCenter, new Vector2(0, -14), new Vector2(520, 40)), L("hud.run_over"), 26, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.35f), true);
            hud.RunOverTotal = Txt(R(over, "Total", TopCenter, TopCenter, new Vector2(0, -56), new Vector2(520, 24)), "0", 16, TextAnchor.MiddleCenter, Color.white);
            hud.RunOverRestart = Txt(R(over, "Restart", TopCenter, TopCenter, new Vector2(0, -84), new Vector2(520, 24)), "R", 15, TextAnchor.MiddleCenter, Color.white);
            hud.RunOver = over.gameObject;

            // The map terminal's map (the whole building, a floor at a time, north-up); the HUD has no minimap.
            hud.BigMap = BuildMap(e, content, "BigMap", false, Center, Center, new Vector2(0, 10), new Vector2(980, 720), 1f);
            hud.BigMap.gameObject.SetActive(false);
            BuildTabletScreen(e, hud, root.transform);
            BuildSafeCrack(e, content);
            BuildVaultLock(content);
            BuildCheatPanel(e, content);
            BuildStylePanel(e, root.transform); // outside Content: F8 works in the menu too, where the HUD is hidden
            BuildHouseClock(hud, content);
            BuildEntryBanner(hud, root.transform);
            BuildStatsPanel(hud, root.transform);
            BuildStartScreen(e, root.transform);
            // the screen goes black between the drive to the next house and the house itself (GameHUD.ShowLoadingFade):
            // over everything, outside Content (which fades out on its own while the drive scene plays)
            hud.LoadingFade = Img(Stretch(root.transform, "LoadingFade"), Color.black);
            hud.LoadingFade.raycastTarget = false;
            return root;
        }
    }
}
