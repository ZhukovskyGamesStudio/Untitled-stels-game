using System.Linq;
using Stealth.Game;
using Stealth.Items;
using Stealth.UI;
using Stealth.Vehicles;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The HUD prefab (UI/HUD) in the paper look: small cards in the corners (the house and its fuel, the clock as a time
    /// card, the task list on a sticky note, the status card, the roster), the reticle and the markers straight over the
    /// world, the key hints as paper keys with a slip each, rubber stamps for the big moments.
    /// </summary>
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

        /// <summary>A key cap: a light paper key (9-sliced, grows with the key's name, typed in PT Mono), or a little mouse instead.</summary>
        static KeyCapView KeyCap(Transform parent, string name)
        {
            var root = R(parent, name, Center, Center, Vector2.zero, new Vector2(24, 24));
            var view = root.gameObject.AddComponent<KeyCapView>();
            var h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            var cap = R(root, "Cap", Center, Center, Vector2.zero, new Vector2(24, 24));
            view.Cap = Img(cap, Color.white, PaperSprite("keycap", new Vector4(10, 13, 10, 10)));
            view.Cap.type = Image.Type.Sliced;
            view.Cap.pixelsPerUnitMultiplier = 1.7f; // the 40 px key drawn at ~24 px
            var ch = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.padding = new RectOffset(7, 7, 1, 4);
            ch.childAlignment = TextAnchor.MiddleCenter;
            ch.childControlWidth = ch.childControlHeight = true;
            ch.childForceExpandWidth = ch.childForceExpandHeight = false; // or the cap reports itself flexible and stretches
            var cle = cap.gameObject.AddComponent<LayoutElement>();
            cle.minWidth = 24;
            cle.minHeight = cle.preferredHeight = 24;
            view.Key = Ink(R(cap, "Key", Center, Center, Vector2.zero, Vector2.zero), "E", 13, TextAnchor.MiddleCenter, Face.Mono);
            view.Key.horizontalOverflow = HorizontalWrapMode.Overflow;

            var mouse = R(root, "Mouse", Center, Center, Vector2.zero, new Vector2(24, 24));
            view.Mouse = Img(mouse, Color.white, Icon("mouse_lmb"));
            var mle = mouse.gameObject.AddComponent<LayoutElement>();
            mle.minWidth = mle.preferredWidth = 24;
            mle.minHeight = mle.preferredHeight = 24;
            mouse.gameObject.SetActive(false);
            view.Lmb = Icon("mouse_lmb");
            view.Rmb = Icon("mouse_rmb");
            view.Mmb = Icon("mouse_mmb");
            view.Wheel = Icon("mouse_wheel");
            return view;
        }

        /// <summary>One line of the hint stack: [key][key] and the word on a paper slip, right-aligned.</summary>
        static GameObject BuildHintRow()
        {
            var root = R(null, "HintRow", Center, Center, Vector2.zero, new Vector2(200, 26));
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
            var tag = R(root, "Tag", Center, Center, Vector2.zero, Vector2.zero);
            Card(tag);
            var th = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.padding = new RectOffset(8, 11, 2, 5); // (the card's shadow is in its right and bottom 4 px)
            th.childControlWidth = th.childControlHeight = true;
            th.childForceExpandWidth = th.childForceExpandHeight = false;
            view.Label = Ink(R(tag, "Label", Center, Center, Vector2.zero, Vector2.zero), "—", 14, TextAnchor.MiddleLeft, Face.Sans);
            view.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var lle = view.Label.gameObject.AddComponent<LayoutElement>();
            lle.minHeight = 19;
            return root.gameObject;
        }

        /// <summary>
        /// The house's clock as a time card under the house's card (after Risk of Rain's difficulty timer): the time
        /// counting up, the clock's second the next extra robot comes at with a robot's picture, a bar from the last one to
        /// the next; the card goes pink, then red, as robots keep coming (UiPalette.HeatPaper).
        /// </summary>
        static void BuildHouseClock(GameHUD hud, Transform column)
        {
            var panel = R(column, "HouseClock", TopLeft, TopLeft, Vector2.zero, new Vector2(300, 50));
            hud.ClockPanel = Card(panel);
            hud.ClockTime = Ink(R(panel, "Time", TopLeft, TopLeft, new Vector2(10, -2), new Vector2(120, 32)), "0:00", 27, TextAnchor.MiddleLeft, Face.Mono);
            Img(R(panel, "Robot", TopRight, TopRight, new Vector2(-78, -7), new Vector2(22, 22)), Color.white, Icon("robot_guard")).preserveAspect = true;
            hud.ClockNext = Ink(R(panel, "Next", TopRight, TopRight, new Vector2(-12, -2), new Vector2(64, 32)), "3:00", 18, TextAnchor.MiddleRight, Face.Mono, UiPalette.InkSoft);
            hud.ClockCount = Ink(R(panel, "Count", TopRight, TopRight, new Vector2(-104, -6), new Vector2(36, 22)), "", 14, TextAnchor.MiddleRight, Face.Bold, UiPalette.Stamp);
            hud.ClockBar = PaperBar(panel, "ToNext", BottomLeft, BottomLeft, new Vector2(10, 9), new Vector2(276, 8), UiPalette.MarkPink);
            // the marker over the bar's end: where the next robot is on the way
            var marker = R(panel, "Marker", BottomRight, Center, new Vector2(-14, 13), new Vector2(8, 8));
            marker.localEulerAngles = new Vector3(0, 0, 45);
            Img(marker, UiPalette.Stamp);
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
            map.GetComponent<Image>().color = UiPalette.PaperDim;
            hud.TabletScreen = screen;
            hud.TabletMap = map;
            SetLayerRecursive(screen.gameObject, Stealth.Level.LevelMetrics.WorldTextLayer);
            screen.gameObject.SetActive(false);
        }

        /// <summary>
        /// The hacking terminal's list in the middle of the screen: a form with the title and the charges and up to
        /// <see cref="GameHUD.HackRowsShown"/> rows (kind icon, name, state, the hold's highlighter fill), copies of a hidden row.
        /// </summary>
        static void BuildHackList(GameHUD hud, Transform content)
        {
            const float w = 440f, rowH = 28f, head = 46f;
            var panel = R(content, "HackList", Center, Center, new Vector2(0, 20), new Vector2(w, head + GameHUD.HackRowsShown * rowH + 14f));
            Card(panel);
            hud.HackTitle = Ink(R(panel, "Title", TopCenter, TopCenter, new Vector2(0, -6), new Vector2(w - 28, 30)), "—", 19, TextAnchor.LowerCenter, Face.Caps);
            RuleLine(panel, TopCenter, TopCenter, new Vector2(0, -head + 6), w - 28, UiPalette.Ink, 2f);
            var rows = R(panel, "Rows", TopLeft, TopLeft, new Vector2(12, -head), new Vector2(w - 28, GameHUD.HackRowsShown * rowH));
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            hud.HackRows = rows;
            var row = R(rows, "Row", TopLeft, TopLeft, Vector2.zero, new Vector2(w - 28, rowH - 2));
            var view = row.gameObject.AddComponent<HackRowView>();
            view.Back = Sliced(Img(row, Color.clear, HighlightSprite));
            var fill = Stretch(row, "Fill");
            fill.anchorMax = new Vector2(0f, 1f);
            view.Fill = Sliced(Img(fill, UiPalette.MarkGreen.A(0.6f), HighlightSprite));
            view.Icon = Img(R(row, "Icon", Left, Left, new Vector2(6, 0), new Vector2(20, 20)), Color.white);
            view.Icon.preserveAspect = true;
            view.Name = Ink(R(row, "Name", Left, Left, new Vector2(34, 0), new Vector2(260, rowH - 2)), "—", 15, TextAnchor.MiddleLeft, Face.Sans);
            view.State = Ink(R(row, "State", Right, Right, new Vector2(-8, 0), new Vector2(110, rowH - 2)), "", 14, TextAnchor.MiddleRight, Face.Mono);
            RuleLine(row, BottomLeft, BottomLeft, new Vector2(0, -1), w - 28, UiPalette.Rule.A(0.6f));
            row.gameObject.SetActive(false);
            hud.HackRowTemplate = view;
            hud.HackKindPictures = RobotKindIcons();
            panel.gameObject.SetActive(false);
            hud.HackList = panel.gameObject;
        }

        /// <summary>The team is up in the house and its clock starts: a big red stamp on a sheet across the screen (fades in and out).</summary>
        static void BuildEntryBanner(GameHUD hud, Transform root)
        {
            var band = R(root, "EntryBanner", Center, Center, new Vector2(0, 110), new Vector2(880, 160));
            hud.EntryBanner = band.gameObject.AddComponent<CanvasGroup>();
            hud.EntryBanner.blocksRaycasts = false;
            hud.EntryBanner.interactable = false;
            var card = R(band, "Card", Center, Center, Vector2.zero, new Vector2(880, 160));
            card.localEulerAngles = new Vector3(0, 0, 1.2f);
            Card(card);
            Tape(card, TopLeft, new Vector2(40, -4), 90, -28);
            Tape(card, TopRight, new Vector2(-40, -4), 90, 26);
            hud.EntryTitle = Stamp(card, "Stamp", Center, Center, new Vector2(0, -2), new Vector2(800, 112), L("hud.entry.title"), 62, -3f);
            hud.EntrySub = Ink(R(card, "Sub", BottomCenter, BottomCenter, new Vector2(0, 6), new Vector2(800, 24)), "", 18, TextAnchor.MiddleCenter, Face.Sans, UiPalette.InkSoft);
            hud.EntrySub.gameObject.SetActive(false); // the notice is the stamp alone
            band.gameObject.SetActive(false);
        }

        /// <summary>The drive's panel: the last house's report — a row per player with their title in handwriting.</summary>
        static void BuildStatsPanel(GameHUD hud, Transform root)
        {
            var panel = R(root, "HouseStats", Center, Center, new Vector2(0, 20), new Vector2(760, 446));
            hud.StatsPanel = panel.gameObject;
            Card(panel);
            Clip(panel, new Vector2(0, 0));
            hud.StatsHeader = Ink(R(panel, "Header", TopCenter, TopCenter, new Vector2(0, -16), new Vector2(720, 36)), "—", 26, TextAnchor.LowerCenter, Face.Caps);
            RuleLine(panel, TopCenter, TopCenter, new Vector2(0, -58), 712, UiPalette.Ink, 2f);
            hud.StatsRows = new StatsRowView[4];
            for (int i = 0; i < 4; i++)
            {
                var row = R(panel, $"Row{i}", TopCenter, TopCenter, new Vector2(0, -68 - i * 92), new Vector2(712, 86));
                var view = row.gameObject.AddComponent<StatsRowView>();
                view.Bar = Img(R(row, "Bar", Left, Left, new Vector2(0, 0), new Vector2(7, 70)), Color.white);
                view.Name = Ink(R(row, "Name", TopLeft, TopLeft, new Vector2(18, -6), new Vector2(230, 26)), "Name", 18, TextAnchor.MiddleLeft, Face.Bold);
                view.Numbers = Ink(R(row, "Numbers", BottomLeft, BottomLeft, new Vector2(18, 8), new Vector2(234, 44)), "—", 13, TextAnchor.LowerLeft, Face.Sans, UiPalette.InkSoft);
                view.Title = Ink(R(row, "Title", TopLeft, TopLeft, new Vector2(262, 0), new Vector2(450, 44)), "—", 34, TextAnchor.MiddleLeft, Face.Hand, UiPalette.Pen);
                view.Title.horizontalOverflow = HorizontalWrapMode.Overflow;
                view.Did = Ink(R(row, "Did", BottomLeft, BottomLeft, new Vector2(262, 10), new Vector2(450, 26)), "—", 15, TextAnchor.MiddleLeft, Face.Sans, UiPalette.InkSoft);
                if (i < 3) RuleLine(row, BottomCenter, BottomCenter, new Vector2(0, -3), 712, UiPalette.Rule);
                hud.StatsRows[i] = view;
            }
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// The run's start screen: an index card per player under their figure in the row (name, the class's tool and the
        /// extra item with arrows, ready), a title, the ready button and who is still choosing. Outside the HUD's content (it fades out).
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
            var title = Ink(R(screen, "Title", TopCenter, TopCenter, new Vector2(0, -24), new Vector2(900, 40)), L("start.title"), 28, TextAnchor.MiddleCenter, Face.Caps);
            Slip(title, true, new Vector2(18, 4));
            var layer = R(screen, "Slots", BottomLeft, BottomLeft, Vector2.zero, Vector2.zero);
            view.Slots = new StartSlotView[4];
            for (int i = 0; i < 4; i++)
            {
                var col = R(layer, $"Slot{i}", BottomLeft, TopCenter, new Vector2(320 + i * 220, 300), new Vector2(204, 198));
                var slot = col.gameObject.AddComponent<StartSlotView>();
                slot.Rect = col;
                Card(col);
                Tape(col, TopCenter, new Vector2(0, -2), 70, (i % 2 == 0 ? -1 : 1) * 4f);
                slot.Name = Ink(R(col, "Name", TopCenter, TopCenter, new Vector2(-2, -10), new Vector2(190, 24)), "Player", 17, TextAnchor.MiddleCenter, Face.Bold);
                RuleLine(col, TopCenter, TopCenter, new Vector2(-2, -36), 176, UiPalette.Rule);
                // the class: its tool's picture between arrows, its name under it
                // (centred pivots: an Image keeping its aspect hugs its pivot — a top pivot pushed the pictures up)
                slot.ClassIcon = Img(R(col, "ClassIcon", TopCenter, Center, new Vector2(-2, -62), new Vector2(96, 46)), Color.white);
                slot.ClassIcon.preserveAspect = true;
                slot.ClassPrev = Arrow(e, col, "ClassPrev", new Vector2(-76, -62), "arrow_left");
                slot.ClassNext = Arrow(e, col, "ClassNext", new Vector2(72, -62), "arrow_right");
                slot.ClassName = Ink(R(col, "ClassName", TopCenter, TopCenter, new Vector2(-2, -86), new Vector2(196, 20)), "—", 14, TextAnchor.MiddleCenter, Face.Bold);
                slot.ItemIcon = Img(R(col, "ItemIcon", TopCenter, Center, new Vector2(-2, -126), new Vector2(96, 38)), Color.white);
                slot.ItemIcon.preserveAspect = true;
                slot.ItemPrev = Arrow(e, col, "ItemPrev", new Vector2(-76, -126), "arrow_left");
                slot.ItemNext = Arrow(e, col, "ItemNext", new Vector2(72, -126), "arrow_right");
                slot.ItemName = Ink(R(col, "ItemName", TopCenter, TopCenter, new Vector2(-2, -148), new Vector2(190, 18)), "—", 13, TextAnchor.MiddleCenter, Face.Sans, UiPalette.InkSoft);
                slot.ItemName.resizeTextForBestFit = true;
                slot.ItemName.resizeTextMinSize = 9;
                slot.ItemName.resizeTextMaxSize = 13;
                slot.ReadyMark = Sliced(Img(R(col, "ReadyMark", BottomCenter, BottomCenter, new Vector2(-2, 10), new Vector2(170, 24)), UiPalette.PaperShade, HighlightSprite));
                slot.ReadyLabel = Ink(R(col, "ReadyLabel", BottomCenter, BottomCenter, new Vector2(-2, 10), new Vector2(190, 24)), "—", 15, TextAnchor.MiddleCenter, Face.Caps);
                view.Slots[i] = slot;
            }
            view.ReadyButton = Instance<Button>(e.Button, screen, "Ready");
            var rb = (RectTransform)view.ReadyButton.transform;
            // bottom right, under the columns of the figures (PickingRules frames the feet above it)
            rb.anchorMin = rb.anchorMax = new Vector2(1, 0);
            rb.pivot = new Vector2(1, 0);
            rb.anchoredPosition = new Vector2(-16, 16);
            rb.sizeDelta = new Vector2(190, 42);
            view.ReadyButtonLabel = view.ReadyButton.GetComponentInChildren<Text>();
            view.ReadyButtonLabel.resizeTextMaxSize = 17;
            view.ReadyButtonLabel.fontSize = 17;
            SetText(view.ReadyButtonLabel, L("start.ready"));
            view.Waiting = Ink(R(screen, "Waiting", BottomRight, BottomRight, new Vector2(-222, 20), new Vector2(260, 34)), "—", 15, TextAnchor.MiddleRight, Face.Sans);
            Slip(view.Waiting, true, new Vector2(10, 2));
            screen.gameObject.SetActive(false);
        }

        /// <summary>
        /// The garage's car upgrade bench, open: a sheet with the title, a close button and an index card per upgrade
        /// (<see cref="CarUpgradeCardView"/>) — the three levelled ones on top, the unique ones under them. Outside the HUD's
        /// content, with its own canvas and raycaster (the HUD's takes no clicks).
        /// </summary>
        static void BuildUpgradeScreen(UiElements e, Transform root)
        {
            var holder = Stretch(root, "UpgradeScreen");
            holder.gameObject.AddComponent<Canvas>();
            holder.gameObject.AddComponent<GraphicRaycaster>();
            var view = holder.gameObject.AddComponent<CarUpgradeScreen>();
            var screen = Stretch(holder, "Screen");
            view.Root = screen.gameObject;
            view.Visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>(DataDir + "/ItemVisuals.asset");
            view.Tick = PaperSprite("tick");
            view.Cross = PaperSprite("cross");
            var (sheet, _) = Sheet(screen, "Sheet", Center, Center, new Vector2(0, -6), new Vector2(1180, 650));
            Clip(sheet, new Vector2(0, 8));
            Ink(R(sheet, "Title", TopCenter, TopCenter, new Vector2(0, -22), new Vector2(700, 40)), L("upgrade.title"), 30, TextAnchor.MiddleCenter, Face.Caps);
            RuleLine(sheet, TopCenter, TopCenter, new Vector2(0, -64), 1100, UiPalette.Ink, 1.5f);
            view.Close = Instance<Button>(e.Button, sheet, "Close");
            var cb = (RectTransform)view.Close.transform;
            cb.anchorMin = cb.anchorMax = new Vector2(1, 1);
            cb.pivot = new Vector2(1, 1);
            cb.anchoredPosition = new Vector2(-18, -18);
            cb.sizeDelta = new Vector2(40, 40);
            var ct = view.Close.GetComponentInChildren<Text>();
            if (ct != null) { ct.text = ""; ct.enabled = false; }
            Img(R(cb, "Glyph", Center, Center, Vector2.zero, new Vector2(22, 22)), UiPalette.Ink, PaperSprite("cross")).raycastTarget = false;
            string[] icons = { "upgrade_engine", "upgrade_trunk", "upgrade_kennel", "upgrade_teleport", "upgrade_planned", "upgrade_planned" };
            view.Cards = new CarUpgradeCardView[icons.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                int col = i % 3, row = i / 3;
                var card = R(sheet, $"Card{i}", Center, Center, new Vector2(-376 + col * 376, 132 - row * 296), new Vector2(356, 272));
                var v = card.gameObject.AddComponent<CarUpgradeCardView>();
                Card(card, row == 0 ? UiPalette.Paper : UiPalette.PaperShade);
                Tape(card, TopCenter, new Vector2(0, -2), 64, (i % 2 == 0 ? -1 : 1) * 3f);
                v.Icon = Img(R(card, "Icon", TopLeft, TopLeft, new Vector2(14, -16), new Vector2(64, 64)), UiPalette.Paper, Icon(icons[i]));
                v.Icon.preserveAspect = true;
                v.Name = Ink(R(card, "Name", TopLeft, TopLeft, new Vector2(90, -16), new Vector2(252, 28)), "—", 21, TextAnchor.MiddleLeft, Face.Bold);
                v.Boxes = new Image[CarUpgrades.MaxLevelled];
                v.Ticks = new Image[CarUpgrades.MaxLevelled];
                for (int b = 0; b < CarUpgrades.MaxLevelled; b++)
                {
                    v.Boxes[b] = Img(R(card, $"Box{b}", TopLeft, TopLeft, new Vector2(92 + b * 32, -50), new Vector2(26, 26)), UiPalette.Ink, PaperSprite("checkbox"));
                    v.Ticks[b] = Img(R(v.Boxes[b].rectTransform, "Tick", Center, Center, new Vector2(2, 3), new Vector2(26, 26)), UiPalette.Good, PaperSprite("tick"));
                }
                RuleLine(card, TopCenter, TopCenter, new Vector2(0, -90), 326, UiPalette.Rule);
                v.What = Ink(R(card, "What", TopLeft, TopLeft, new Vector2(16, -96), new Vector2(326, 40)), "—", 14, TextAnchor.UpperLeft, Face.Sans, UiPalette.InkSoft);
                v.Now = Ink(R(card, "Now", TopLeft, TopLeft, new Vector2(16, -138), new Vector2(326, 20)), "—", 14, TextAnchor.MiddleLeft, Face.Bold);
                var recipe = R(card, "Recipe", TopLeft, TopLeft, new Vector2(16, -158), new Vector2(326, 52));
                v.Recipe = recipe.gameObject;
                Ink(R(recipe, "Need", TopLeft, TopLeft, Vector2.zero, new Vector2(70, 18)), L("upgrade.need"), 13, TextAnchor.MiddleLeft, Face.Caps, UiPalette.InkSoft);
                int things = CarUpgrades.MostThings;
                v.Things = new Image[things];
                v.Marks = new Image[things];
                for (int k = 0; k < things; k++)
                {
                    var spot = R(recipe, $"Thing{k}", TopLeft, TopLeft, new Vector2(74 + k * 60, 0), new Vector2(50, 50));
                    FormBox(spot, UiPalette.PaperShade);
                    v.Things[k] = Img(R(spot, "Picture", Center, Center, Vector2.zero, new Vector2(42, 42)), UiPalette.Paper);
                    v.Things[k].preserveAspect = true;
                    v.Marks[k] = Img(R(spot, "Mark", BottomRight, Center, new Vector2(-4, 6), new Vector2(22, 22)), UiPalette.Good, PaperSprite("tick"));
                }
                v.Raise = Instance<Button>(e.Button, card, "Raise");
                var rb = (RectTransform)v.Raise.transform;
                rb.anchorMin = rb.anchorMax = BottomCenter;
                rb.pivot = BottomCenter;
                rb.anchoredPosition = new Vector2(0, 10);
                rb.sizeDelta = new Vector2(220, 34);
                v.RaiseLabel = v.Raise.GetComponentInChildren<Text>();
                v.RaiseLabel.resizeTextMaxSize = 16;
                v.RaiseLabel.fontSize = 16;
                SetText(v.RaiseLabel, L("upgrade.raise"));
                v.Done = Stamp(card, "Done", BottomCenter, BottomCenter, new Vector2(0, 26), new Vector2(170, 56), L("upgrade.top"), 30, -7f).transform.parent.gameObject;
                var planned = Note(card, "Planned", BottomCenter, BottomCenter, new Vector2(0, 20), new Vector2(190, 70), UiPalette.NoteYellow, 3f);
                Ink(Stretch(planned, "Text", 8f), L("upgrade.planned"), 26, TextAnchor.MiddleCenter, Face.Hand, UiPalette.Pen);
                v.Planned = planned.gameObject;
                view.Cards[i] = v;
            }
            screen.gameObject.SetActive(false);
        }

        /// <summary>
        /// A small square arrow button of the start screen: a paper button with a drawn chevron (Tools/Art/generate_icons.py),
        /// highlighter yellow and a little bigger under the mouse (<see cref="UiHover"/>).
        /// </summary>
        static Button Arrow(UiElements e, RectTransform parent, string name, Vector2 pos, string icon)
        {
            var b = Instance<Button>(e.Button, parent, name);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = TopCenter;
            rt.pivot = Center;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(32, 32);
            var t = b.GetComponentInChildren<Text>();
            if (t != null) { t.text = ""; t.enabled = false; } // (a nested prefab instance: its label cannot be removed)
            var glyph = Img(R(rt, "Glyph", Center, Center, new Vector2(0, 1), new Vector2(20, 20)), Color.white, Icon(icon));
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

            // Overlays (full screen, hidden until needed).
            var overlays = Stretch(content, "Overlays");
            hud.LowHpVignette = Img(Stretch(overlays, "LowHpVignette"), new Color(0.8f, 0, 0, 0.35f), S("Vignette"));
            hud.HitFlash = Img(Stretch(overlays, "HitFlash"), new Color(1, 0, 0, 0.2f));
            hud.GasVignette = Img(Stretch(overlays, "GasVignette"), new Color(0.4f, 0.95f, 0.3f, 0.5f), S("Vignette"));
            hud.GasTint = Img(Stretch(overlays, "GasTint"), new Color(0.5f, 0.9f, 0.3f, 0.1f));
            hud.SpeedLines = Img(Stretch(overlays, "SpeedLines"), new Color(1f, 0.95f, 0.85f, 0f), UiSprite("speed_lines"));
            hud.SpeedLines.raycastTarget = false;
            hud.SpeedLines.gameObject.SetActive(false);
            hud.LaughText = WorldTxt(R(overlays, "Laugh", TopCenter, TopCenter, new Vector2(0, -216), new Vector2(400, 56)), "HA-HA", 44, TextAnchor.MiddleCenter, Face.Hand, UiPalette.MarkGreen);
            hud.GasText = WorldTxt(R(overlays, "GasTimer", TopCenter, TopCenter, new Vector2(0, -262), new Vector2(120, 34)), "10", 28, TextAnchor.MiddleCenter, Face.Mono, UiPalette.MarkGreen);
            hud.DownedTint = Img(Stretch(overlays, "DownedTint"), new Color(0.25f, 0f, 0f, 0.55f));
            hud.DownedText = WorldTxt(R(overlays, "Downed", TopCenter, TopCenter, new Vector2(0, -90), new Vector2(600, 30)), "—", 20, TextAnchor.MiddleCenter);
            // In the hatch: peeking out at floor level, the rim of the hatch dark around the view; the note on a slip.
            hud.HatchTint = Img(Stretch(overlays, "HatchTint"), new Color(0f, 0f, 0f, 1f), S("Vignette"));
            hud.HatchText = Ink(R(overlays, "HatchText", TopCenter, TopCenter, new Vector2(0, -120), new Vector2(760, 30)), "—", 17, TextAnchor.MiddleCenter, Face.Sans);
            Slip(hud.HatchText, true, new Vector2(14, 4));
            hud.RouteCountdown = WorldTxt(R(overlays, "RouteCountdown", Center, Center, new Vector2(0, 120), new Vector2(300, 100)), "3", 84, TextAnchor.MiddleCenter, Face.Mono);
            var alarm = Stretch(overlays, "Alarm");
            hud.AlarmFrame = alarm.gameObject;
            var red = UiPalette.Danger.A(0.5f);
            hud.AlarmEdges = new[]
            {
                Img(R(alarm, "Top", TopCenter, TopCenter, Vector2.zero, new Vector2(8000, 5)), red),
                Img(R(alarm, "Bottom", BottomCenter, BottomCenter, Vector2.zero, new Vector2(8000, 5)), red),
                Img(R(alarm, "Left", Left, Left, Vector2.zero, new Vector2(5, 8000)), red),
                Img(R(alarm, "Right", Right, Right, Vector2.zero, new Vector2(5, 8000)), red),
            };
            var tag = R(alarm, "Tag", TopCenter, TopCenter, new Vector2(0, -12), new Vector2(190, 40));
            Card(tag);
            hud.AlarmLabel = Stamp(tag, "Stamp", Center, Center, new Vector2(-2, 2), new Vector2(170, 30), "ALARM · 6", 17, -2f);

            // The reticle: a dot (with a faint ink shade: it shows on light walls too), or the glyph of what E would do;
            // a ring fills while E is held.
            hud.ArcLayer = R(content, "Arcs", Center, Center, Vector2.zero, Vector2.zero);
            hud.ArcPrefab = e.Arc;
            hud.HitArc = Instance<ArcView>(e.Arc.gameObject, hud.ArcLayer, "HitArc");
            var shade = R(content, "ReticleShade", Center, Center, Vector2.zero, new Vector2(10, 10));
            Img(shade, UiPalette.Rim.A(0.5f), S("Disc"));
            hud.ReticleGlyph = Img(R(content, "Reticle", Center, Center, Vector2.zero, new Vector2(5, 5)), UiPalette.OnWorld, S("Disc"));
            hud.ReticleGlyph.preserveAspect = true;
            hud.ReticleGlyph.gameObject.AddComponent<UiBacking>().Backing = shade;
            hud.ReticleRing = Img(R(content, "ReticleRing", Center, Center, Vector2.zero, new Vector2(44, 44)), UiPalette.OnWorld, S("Ring"));
            hud.ReticleRing.type = Image.Type.Filled;
            hud.ReticleRing.fillMethod = Image.FillMethod.Radial360;
            hud.ReticleRing.fillOrigin = (int)Image.Origin360.Top;
            hud.ReticleRing.fillClockwise = true;
            hud.ReticleSprites = ReticleIconNames.Select(n => n == null ? S("Disc") : Icon(n)).ToArray();
            hud.Stamina = Bar(content, "Stamina", Center, TopCenter, new Vector2(0, -34), new Vector2(110, 5), UiPalette.Rim.A(0.5f), UiPalette.OnWorld, 1f);

            // The hint stack, bottom right: the keys of the moment, one above the other, the most important at the bottom.
            hud.HintStack = R(content, "Hints", BottomRight, BottomRight, new Vector2(-16, 16), new Vector2(420, 0));
            var stack = hud.HintStack.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 4;
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
            hud.HatchDiamond = Img(diamond, UiPalette.Exit);
            diamond.gameObject.AddComponent<Outline>().effectColor = UiPalette.Rim;
            hud.HatchLabel = WorldTxt(R(hud.HatchMarker, "Label", Center, Center, new Vector2(0, -19), new Vector2(120, 18)), "12 m", 13, TextAnchor.MiddleCenter, Face.Bold, UiPalette.Exit);

            // Bottom left, on a card: the eye (how lit = how visible), the speaker (how loud), the team's hack slots, the
            // hearts under them. No words.
            var status = R(content, "Status", BottomLeft, BottomLeft, new Vector2(12, 12), new Vector2(212, 68));
            Card(status).color = UiPalette.Paper.A(0.94f);
            hud.Eye = Img(R(status, "Eye", BottomLeft, BottomLeft, new Vector2(9, 30), new Vector2(32, 32)), Color.white, Icon("eye_0"));
            hud.EyeSprites = Enumerable.Range(0, 8).Select(i => Icon($"eye_{i}")).ToArray();
            hud.Speaker = Img(R(status, "Speaker", BottomLeft, BottomLeft, new Vector2(45, 30), new Vector2(32, 32)), Color.white, Icon("noise_0"));
            hud.SpeakerSprites = Enumerable.Range(0, 4).Select(i => Icon($"noise_{i}")).ToArray();
            hud.HeartFull = Icon("heart_full");
            hud.HeartEmpty = Icon("heart_empty");
            hud.Hearts = Enumerable.Range(0, Stealth.Player.HealthState.MaxHp + 1) // + the med table's extra point
                .Select(i => Img(R(status, $"Heart{i}", BottomLeft, BottomLeft, new Vector2(11 + i * 23, 9), new Vector2(20, 20)), UiPalette.Stamp, hud.HeartFull)).ToArray();
            // The team's hack slots right of the speaker: a blue bolt when free, the grey picture of what is switched off
            // when taken; the leftmost is the next to go (GameHUD.ShowHackSlots)
            RuleLine(status, BottomLeft, BottomLeft, new Vector2(84, 31), 1f, UiPalette.Rule, 30f); // a thin upright line between the self and the team
            hud.HackSlots = Enumerable.Range(0, 4)
                .Select(i => Img(R(status, $"Hack{i}", BottomLeft, BottomLeft, new Vector2(90 + i * 28, 33), new Vector2(26, 26)), Color.white, Icon("ret_hack"))).ToArray();
            foreach (var slot in hud.HackSlots) slot.preserveAspect = true;
            hud.HackFree = Icon("ret_hack");
            // tired arms (the hammer, once per house) right of the hearts
            hud.TiredArms = Img(R(status, "TiredArms", BottomLeft, BottomLeft, new Vector2(84, 8), new Vector2(22, 22)), UiPalette.MarkOrange, Icon("tired_arms"));
            hud.TiredArms.preserveAspect = true;
            hud.TiredArms.gameObject.SetActive(false);
            hud.HackKindIcons = RobotKindIcons();

            // Top left, a column: the house's card (its name, the fuel: a drop, the tank / what the drive burns), the
            // clock's time card under it, the tasks on a sticky note under that (a hidden one leaves no gap).
            var column = R(content, "TopLeft", TopLeft, TopLeft, new Vector2(12, -12), new Vector2(320, 0));
            var cl = column.gameObject.AddComponent<VerticalLayoutGroup>();
            cl.spacing = 8;
            cl.childAlignment = TextAnchor.UpperLeft;
            cl.childControlWidth = cl.childControlHeight = false;
            cl.childForceExpandWidth = cl.childForceExpandHeight = false;
            var quota = R(column, "Quota", TopLeft, TopLeft, Vector2.zero, new Vector2(300, 66));
            Card(quota);
            hud.HouseLabel = Ink(R(quota, "House", TopLeft, TopLeft, new Vector2(12, -6), new Vector2(276, 24)), "House", 17, TextAnchor.LowerLeft, Face.Caps);
            hud.HouseLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            RuleLine(quota, TopLeft, TopLeft, new Vector2(10, -32), 278);
            Img(R(quota, "FuelIcon", TopLeft, TopLeft, new Vector2(9, -37), new Vector2(20, 20)), UiPalette.MarkOrange, Icon("ret_fuel")).preserveAspect = true;
            hud.QuotaLabel = Ink(R(quota, "Count", TopRight, TopRight, new Vector2(-14, -36), new Vector2(84, 22)), "0 / 100", 16, TextAnchor.MiddleRight, Face.Mono);
            hud.QuotaLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            hud.QuotaBar = PaperBar(quota, "Bar", TopLeft, TopLeft, new Vector2(34, -41), new Vector2(166, 12), UiPalette.MarkOrange);
            hud.GainRect = R(quota, "Gain", TopLeft, Left, new Vector2(GameHUD.GainX, GameHUD.GainY), new Vector2(120, 30));
            hud.GainLabel = WorldTxt(hud.GainRect, "+20", 24, TextAnchor.MiddleLeft, Face.Mono, UiPalette.Exit);
            BuildHouseClock(hud, column);
            var tasks = R(column, "Tasks", TopLeft, TopLeft, new Vector2(0, 0), new Vector2(276, 40));
            NoteBack(tasks, UiPalette.NoteYellow);
            tasks.localEulerAngles = new Vector3(0, 0, -1f);
            var tl = tasks.gameObject.AddComponent<VerticalLayoutGroup>();
            tl.padding = new RectOffset(12, 12, 12, 12);
            tl.spacing = 1;
            tl.childControlWidth = tl.childControlHeight = true;
            tl.childForceExpandWidth = true;
            tl.childForceExpandHeight = false;
            tasks.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            hud.ObjectiveNote = tasks.gameObject;
            hud.TaskRows = tasks;
            hud.TaskRowPrefab = e.TaskRow;
            tasks.gameObject.SetActive(false);

            hud.TerminalLabel = Ink(R(content, "Terminal", BottomCenter, BottomCenter, new Vector2(0, 150), new Vector2(700, 24)), "1 / 3", 16, TextAnchor.MiddleCenter, Face.Mono);
            Slip(hud.TerminalLabel, true, new Vector2(12, 4));
            BuildHackList(hud, content);

            // The roster top right: a card with a line per teammate (no minimap there: the map is a terminal's, TerminalKind.Map).
            hud.TeamList = R(content, "Team", TopRight, TopRight, new Vector2(-12, -12), new Vector2(240, 0));
            Card(hud.TeamList);
            var team = hud.TeamList.gameObject.AddComponent<VerticalLayoutGroup>();
            team.padding = new RectOffset(10, 14, 7, 11);
            team.spacing = 2;
            team.childControlHeight = true;
            team.childControlWidth = true;
            team.childForceExpandHeight = false;
            team.childAlignment = TextAnchor.UpperRight;
            var fit = hud.TeamList.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; // as wide as the longest line, growing left from the corner
            hud.TeamRowPrefab = e.TeamRow;
            hud.TeamList.gameObject.SetActive(false);

            // The banner: a typed slip taped across the top.
            hud.BannerRect = R(content, "Banner", TopCenter, TopCenter, new Vector2(0, -115), new Vector2(700, 34));
            hud.Banner = hud.BannerRect.gameObject.AddComponent<CanvasGroup>();
            hud.Banner.blocksRaycasts = false;
            hud.BannerLabel = Ink(Stretch(hud.BannerRect, "Label"), "—", 17, TextAnchor.MiddleCenter, Face.Bold);
            Slip(hud.BannerLabel, true, new Vector2(18, 5)).MinWidth = 160f;

            // Run over: a sheet with a big stamp.
            var over = R(content, "RunOver", TopCenter, TopCenter, new Vector2(0, -250), new Vector2(540, 170));
            Card(over);
            Clip(over, new Vector2(0, 0));
            Stamp(over, "Stamp", TopCenter, TopCenter, new Vector2(0, -24), new Vector2(420, 64), L("hud.run_over"), 34, -2.5f);
            hud.RunOverTotal = Ink(R(over, "Total", TopCenter, TopCenter, new Vector2(0, -98), new Vector2(500, 26)), "0", 17, TextAnchor.MiddleCenter, Face.Mono);
            hud.RunOverRestart = Ink(R(over, "Restart", TopCenter, TopCenter, new Vector2(0, -128), new Vector2(500, 24)), "R", 15, TextAnchor.MiddleCenter, Face.Sans, UiPalette.InkSoft);
            hud.RunOver = over.gameObject;

            // The map terminal's map (the whole building, a floor at a time, north-up); the HUD has no minimap.
            hud.BigMap = BuildMap(e, content, "BigMap", false, Center, Center, new Vector2(0, 6), new Vector2(940, 660), 1f);
            hud.BigMap.gameObject.SetActive(false);
            BuildTabletScreen(e, hud, root.transform);
            BuildSafeCrack(e, content);
            BuildVaultLock(content);
            BuildCheatPanel(e, content);
            BuildEntryBanner(hud, root.transform);
            BuildStatsPanel(hud, root.transform);
            BuildStartScreen(e, root.transform);
            BuildUpgradeScreen(e, root.transform);
            // the screen goes black between the drive to the next house and the house itself (GameHUD.ShowLoadingFade):
            // over everything, outside Content (which fades out on its own while the drive scene plays)
            hud.LoadingFade = Img(Stretch(root.transform, "LoadingFade"), Color.black);
            hud.LoadingFade.raycastTarget = false;
            return root;
        }
    }
}
