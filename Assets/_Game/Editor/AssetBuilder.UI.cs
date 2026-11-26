using System.Collections.Generic;
using System.Linq;
using Stealth.Audio;
using Stealth.Game;
using Stealth.Items;
using Stealth.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Interface prefabs: small element prefabs (UI/Elements) and the screens composed of them (UI/HUD, UI/Inventory,
    /// UI/Menu, UI/StashBoard in the world above the hatch), all in the paper look of <see cref="UiPalette"/>
    /// (AssetBuilder.Paper). Screens are uGUI canvases laid out for 1280 x 720 and scaled to keep all of that on any screen
    /// shape (CanvasScaler Expand: a 4:3 screen gets more height, an ultrawide more width). The scene holds linked
    /// instances of the screens.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string UiPrefabsDir = PrefabsDir + "/UI";
        const string ElementsDir = UiPrefabsDir + "/Elements";

        struct UiPrefabs
        {
            public GameObject[] Screens;
            public RouteSignView RouteSign;
        }

        struct UiElements
        {
            public GameObject Button, Slider, Toggle, InputField;
            public ArcView Arc;
            public TeamRowView TeamRow;
            public TaskRowView TaskRow;
            public SuspicionMarkerView SuspicionMarker;
            public TeamMarkerView TeamMarker;
            public InventoryBlockView InventoryBlock;
            public FriendLobbyRowView FriendRow;
            public Image CellHighlight;
            public ControlRowView ControlRow;
            public Text ControlGroup;
            public Image MapRect;
            public MapIconView MapIcon;
            public CalloutMarkerView Callout;
            public RouteSignView RouteSign;
            public Text DialNumber;
            public Image DialTick;
        }

        /// <summary>The body face (also for the few world texts built elsewhere, the door numbers).</summary>
        static Font UiFont => FontOf(Face.Sans);

        static readonly Vector2 TopLeft = new(0, 1), TopCenter = new(0.5f, 1), TopRight = new(1, 1), Center = new(0.5f, 0.5f),
            BottomLeft = new(0, 0), BottomCenter = new(0.5f, 0), BottomRight = new(1, 0), Left = new(0, 0.5f), Right = new(1, 0.5f);

        /// <summary>REBUILD_UI=1 rebuilds only the interface prefabs from code (same paths and GUIDs; the scene keeps its instances).</summary>
        static UiPrefabs BuildUi()
        {
            bool force = s_Force;
            if (System.Environment.GetEnvironmentVariable("REBUILD_UI") == "1") s_Force = true;
            try
            {
                var e = BuildUiElements();
                return new UiPrefabs
                {
                    RouteSign = e.RouteSign,
                    Screens = new[]
                    {
                        Prefab(UiPrefabsDir + "/HUD.prefab", () => BuildHud(e)),
                        Prefab(UiPrefabsDir + "/Inventory.prefab", () => BuildInventoryScreen(e)),
                        Prefab(UiPrefabsDir + "/Menu.prefab", () => BuildMenu(e)),
                        Prefab(UiPrefabsDir + "/StashBoard.prefab", () => BuildStashBoard(e)),
                    },
                };
            }
            finally
            {
                s_Force = force;
            }
        }

        // ------------------------------------------------------------------ building blocks

        static RectTransform R(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Stretch(Transform parent, string name, float inset = 0f)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        static Image Img(RectTransform rt, Color color, Sprite sprite = null)
        {
            var i = rt.gameObject.AddComponent<Image>();
            i.color = color;
            i.sprite = sprite;
            i.raycastTarget = false;
            return i;
        }

        /// <summary>
        /// A text in the body face (bold: the heading face); <paramref name="shadow"/> gives it the ink rim of text over
        /// the world. New interface code uses <see cref="Ink"/> (on paper) and <see cref="WorldTxt"/> (over the world).
        /// </summary>
        static Text Txt(RectTransform rt, string text, int size, TextAnchor anchor, Color color, bool bold = false, bool shadow = true)
        {
            var t = Ink(rt, text, size, anchor, bold ? Face.Bold : Face.Sans, color);
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Outline>();
                s.effectColor = UiPalette.Rim;
                s.effectDistance = new Vector2(1, -1);
            }
            return t;
        }

        const char KeyMark = '\u0001';

        /// <summary>A fixed label from the string table: pass <c>L(key)</c> wherever a helper takes a text (it gets a <see cref="LocText"/>).</summary>
        static string L(string key) => KeyMark + key;

        /// <summary>Set a prefab label: a plain text as it is, an <see cref="L"/> key as its Russian text plus a <see cref="LocText"/> that follows the language.</summary>
        static void SetText(Text t, string text)
        {
            if (!string.IsNullOrEmpty(text) && text[0] == KeyMark)
            {
                string key = text.Substring(1);
                t.text = Loc.T(key, Language.Ru);
                var lt = t.GetComponent<LocText>() ?? t.gameObject.AddComponent<LocText>();
                lt.Key = key;
            }
            else t.text = text;
        }

        static GameObject CanvasRoot(string name, int order, bool raycasts)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            // Expand: the whole 1280 x 720 layout always fits (scaled by the tighter side); a taller or wider screen gets
            // the extra room at the edges, where the corner panels are anchored — nothing is cut off on 4:3 or 16:10.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            if (raycasts) go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static void Fixed(Component c, float height, float width = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            if (width > 0f) le.minWidth = le.preferredWidth = width;
        }

        static RectTransform Column(Transform parent, string name, Vector2 pos, float width, float spacing = 6f)
        {
            var rt = R(parent, name, TopCenter, TopCenter, pos, new Vector2(width, 0));
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        /// <summary>A line of a column: ink on paper in the given face.</summary>
        static Text ColumnText(Transform column, string name, string text, int size, float height, Face face = Face.Sans, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            var t = Ink(R(column, name, Center, Center, Vector2.zero, Vector2.zero), text, size, anchor, face, color);
            Fixed(t, height);
            return t;
        }

        /// <summary>A column's heading: capitals and a printed line under them.</summary>
        static Text ColumnTitle(Transform column, string text, int size = 24)
        {
            var t = ColumnText(column, "Title", text, size, size + 18, Face.Caps, TextAnchor.LowerCenter);
            var line = R(column, "TitleRule", Center, Center, Vector2.zero, Vector2.zero);
            Img(line, UiPalette.Ink);
            Fixed(line, 2);
            return t;
        }

        static void Spacer(Transform column, float height) => Fixed(R(column, "Space", Center, Center, Vector2.zero, Vector2.zero), height);

        static T Instance<T>(GameObject prefab, Transform parent, string name) where T : Component => Nested(prefab, parent, name).GetComponent<T>();

        static Button ButtonIn(UiElements e, Transform parent, string name, string label, float height)
        {
            var b = Instance<Button>(e.Button, parent, name);
            SetText(b.GetComponentInChildren<Text>(), label);
            Fixed(b, height);
            return b;
        }

        /// <summary>A paper button's colours: the paper, a highlighter yellow under the mouse, darker when pressed.</summary>
        static ColorBlock PaperButtonColors(Color normal)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.selectedColor = normal;
            colors.highlightedColor = UiPalette.NoteYellow;
            colors.pressedColor = Color.Lerp(UiPalette.NoteYellow, UiPalette.MarkOrange, 0.45f);
            colors.disabledColor = normal.A(0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        // ------------------------------------------------------------------ elements

        static UiElements BuildUiElements()
        {
            var res = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };
            Image Child(GameObject go, string name) => go.GetComponentsInChildren<Image>(true).First(i => i.name == name);

            var e = new UiElements();
            e.Button = Prefab(ElementsDir + "/Button.prefab", () =>
            {
                var go = DefaultControls.CreateButton(res);
                go.name = "Button";
                ((RectTransform)go.transform).sizeDelta = new Vector2(320, 36);
                var img = go.GetComponent<Image>();
                img.sprite = PaperSprite("button", B(8));
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                var b = go.GetComponent<Button>();
                go.AddComponent<UiSound>().Control = b;
                b.colors = PaperButtonColors(UiPalette.Paper);
                var label = go.GetComponentInChildren<Text>();
                label.font = FontOf(Face.Bold);
                label.fontStyle = FontStyle.Normal;
                label.color = UiPalette.Ink;
                label.fontSize = 15;
                label.resizeTextForBestFit = true; // a long word in English or Russian shrinks rather than spills
                label.resizeTextMinSize = 10;
                label.resizeTextMaxSize = 15;
                var lrt = label.rectTransform;
                lrt.offsetMin = new Vector2(8, 2);
                lrt.offsetMax = new Vector2(-8, 0);
                return go;
            });
            e.Slider = Prefab(ElementsDir + "/Slider.prefab", () =>
            {
                // a printed line, a highlighter stroke along it up to the value, a red push pin for the handle
                var go = DefaultControls.CreateSlider(res);
                go.name = "Slider";
                go.AddComponent<UiSound>().Control = go.GetComponent<Slider>();
                var back = Child(go, "Background");
                back.sprite = null;
                back.color = UiPalette.InkSoft.A(0.8f);
                back.rectTransform.anchorMin = new Vector2(0f, 0.45f);
                back.rectTransform.anchorMax = new Vector2(1f, 0.55f);
                var fill = Child(go, "Fill");
                fill.sprite = HighlightSprite;
                fill.type = Image.Type.Sliced;
                fill.color = UiPalette.MarkGreen.A(0.85f);
                var handle = Child(go, "Handle");
                handle.sprite = PaperSprite("pin");
                handle.color = Color.white;
                handle.preserveAspect = true;
                handle.rectTransform.sizeDelta = new Vector2(26, 8);
                return go;
            });
            e.Toggle = Prefab(ElementsDir + "/Toggle.prefab", () =>
            {
                // a form's checkbox ticked in pen
                var go = DefaultControls.CreateToggle(res);
                go.name = "Toggle";
                go.AddComponent<UiSound>().Control = go.GetComponent<Toggle>();
                var box = Child(go, "Background");
                box.sprite = PaperSprite("checkbox");
                box.color = UiPalette.Ink;
                var brt = box.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot = new Vector2(0f, 0.5f);
                brt.anchoredPosition = new Vector2(0f, 0f);
                brt.sizeDelta = new Vector2(20, 20);
                var tick = Child(go, "Checkmark");
                tick.sprite = PaperSprite("tick");
                tick.color = UiPalette.Pen;
                var trt = tick.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
                trt.anchoredPosition = new Vector2(3, 3);
                trt.sizeDelta = new Vector2(24, 24);
                var label = go.GetComponentInChildren<Text>();
                label.font = FontOf(Face.Sans);
                label.color = UiPalette.Ink;
                label.fontSize = 15;
                label.rectTransform.offsetMin = new Vector2(28, label.rectTransform.offsetMin.y);
                return go;
            });
            e.InputField = Prefab(ElementsDir + "/InputField.prefab", () =>
            {
                // a typed-in field of the form: a box, typewriter letters
                var go = DefaultControls.CreateInputField(res);
                go.name = "InputField";
                var img = go.GetComponent<Image>();
                img.sprite = BoxSprite;
                img.type = Image.Type.Sliced;
                img.color = UiPalette.Paper;
                var field = go.GetComponent<InputField>();
                var colors = field.colors;
                colors.highlightedColor = UiPalette.NoteYellow;
                colors.selectedColor = UiPalette.NoteYellow;
                field.colors = colors;
                field.selectionColor = UiPalette.MarkYellow.A(0.6f);
                field.caretColor = UiPalette.Ink;
                foreach (var t in go.GetComponentsInChildren<Text>(true))
                {
                    t.font = FontOf(Face.Mono);
                    t.fontSize = 15;
                    t.color = t.name == "Placeholder" ? UiPalette.InkFaint : UiPalette.Ink;
                }
                return go;
            });

            e.Arc = Prefab(ElementsDir + "/Arc.prefab", () =>
            {
                var root = R(null, "Arc", Center, Center, Vector2.zero, Vector2.zero);
                var arc = R(root, "Shape", Center, Center, new Vector2(0, 92), new Vector2(126, 36));
                var view = root.gameObject.AddComponent<ArcView>();
                view.Rotator = root;
                view.Arc = arc;
                view.Image = Img(arc, Color.white, S("Arc"));
                return root.gameObject;
            }).GetComponent<ArcView>();

            e.SuspicionMarker = Prefab(ElementsDir + "/SuspicionMarker.prefab", () =>
            {
                // Pivot at the bottom centre: the bar sits right over the robot, the "?" / "!" above the bar.
                var root = R(null, "SuspicionMarker", BottomLeft, BottomCenter, Vector2.zero, new Vector2(84, 48));
                var view = root.gameObject.AddComponent<SuspicionMarkerView>();
                view.Root = root;
                view.Bar = Bar(root, "Bar", BottomCenter, BottomCenter, Vector2.zero, new Vector2(70, 9), UiPalette.Rim.A(0.65f), UiPalette.MarkYellow, 2f);
                view.Icon = WorldTxt(R(root, "Icon", BottomCenter, BottomCenter, new Vector2(0, 10), new Vector2(40, 36)), "?", 30, TextAnchor.LowerCenter, Face.Caps, UiPalette.Caution);
                return root.gameObject;
            }).GetComponent<SuspicionMarkerView>();

            e.FriendRow = Prefab(ElementsDir + "/FriendLobbyRow.prefab", () =>
            {
                var root = R(null, "FriendLobbyRow", TopLeft, TopLeft, Vector2.zero, new Vector2(330, 34));
                Img(root, Color.white.A(0.4f));
                Fixed(root.GetComponent<Image>(), 34);
                var view = root.gameObject.AddComponent<FriendLobbyRowView>();
                view.Label = Ink(R(root, "Label", Left, Left, new Vector2(10, 0), new Vector2(200, 30)), "— · 1/4", 14, TextAnchor.MiddleLeft, Face.Hand);
                view.Label.fontSize = 19;
                view.Join = Instance<Button>(e.Button, root, "Join");
                SetText(view.Join.GetComponentInChildren<Text>(), L("friends.join"));
                var jrt = (RectTransform)view.Join.transform;
                jrt.anchorMin = jrt.anchorMax = jrt.pivot = new Vector2(1, 0.5f);
                jrt.anchoredPosition = new Vector2(-4, 0);
                jrt.sizeDelta = new Vector2(100, 28);
                return root.gameObject;
            }).GetComponent<FriendLobbyRowView>();

            e.TeamRow = Prefab(ElementsDir + "/TeamRow.prefab", () =>
            {
                // a line of the roster card: the name in the player's ink, then a state word or the hearts
                var root = R(null, "TeamRow", TopRight, TopRight, Vector2.zero, new Vector2(230, 20));
                Fixed(root, 20);
                var h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 6;
                h.childAlignment = TextAnchor.MiddleRight;
                h.childControlWidth = h.childControlHeight = true;
                h.childForceExpandWidth = h.childForceExpandHeight = false;
                var view = root.gameObject.AddComponent<TeamRowView>();
                view.Name = Ink(R(root, "Name", Center, Center, Vector2.zero, Vector2.zero), "P1", 14, TextAnchor.MiddleRight, Face.Bold);
                view.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                view.State = Ink(R(root, "State", Center, Center, Vector2.zero, Vector2.zero), "", 13, TextAnchor.MiddleRight, Face.Sans, UiPalette.InkSoft);
                view.State.horizontalOverflow = HorizontalWrapMode.Overflow;
                view.Hearts = Enumerable.Range(0, Stealth.Player.HealthState.MaxHp + 1).Select(i =>
                {
                    var heart = Img(R(root, $"Heart{i}", Center, Center, Vector2.zero, new Vector2(14, 14)), UiPalette.Stamp, Icon("heart_full"));
                    heart.preserveAspect = true;
                    Fixed(heart, 14, 14);
                    return heart;
                }).ToArray();
                return root.gameObject;
            }).GetComponent<TeamRowView>();

            e.TaskRow = Prefab(ElementsDir + "/TaskRow.prefab", () =>
            {
                // a line of the task note: a checkbox in pen and the task in handwriting (a long one wraps, the row grows)
                var root = R(null, "TaskRow", TopLeft, TopLeft, Vector2.zero, new Vector2(240, 24));
                var h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 6;
                h.childAlignment = TextAnchor.UpperLeft;
                h.childControlWidth = h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = false;
                var view = root.gameObject.AddComponent<TaskRowView>();
                var boxHolder = R(root, "Check", Center, Center, Vector2.zero, new Vector2(20, 24));
                Fixed(boxHolder, 24, 20);
                view.Box = Img(R(boxHolder, "Box", Center, Center, new Vector2(0, 0), new Vector2(17, 17)), UiPalette.Pen, PaperSprite("checkbox"));
                view.Tick = Img(R(boxHolder, "Tick", Center, Center, new Vector2(3, 4), new Vector2(22, 22)), UiPalette.Pen, PaperSprite("tick"));
                view.Label = Ink(R(root, "Label", Center, Center, Vector2.zero, Vector2.zero), "—", 21, TextAnchor.MiddleLeft, Face.Hand, UiPalette.Pen);
                view.Label.lineSpacing = 0.85f;
                view.Label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                return root.gameObject;
            }).GetComponent<TaskRowView>();

            e.TeamMarker = Prefab(ElementsDir + "/TeamMarker.prefab", () =>
            {
                var root = R(null, "TeamMarker", BottomLeft, Center, Vector2.zero, Vector2.zero);
                var view = root.gameObject.AddComponent<TeamMarkerView>();
                view.Rect = root;
                var cross = R(root, "Cross", Center, Center, Vector2.zero, new Vector2(20, 20));
                view.Cross = cross.gameObject;
                view.CrossBars = new[]
                {
                    Img(R(cross, "Vertical", Center, Center, Vector2.zero, new Vector2(6, 20)), UiPalette.Danger),
                    Img(R(cross, "Horizontal", Center, Center, Vector2.zero, new Vector2(20, 6)), UiPalette.Danger),
                };
                foreach (var bar in view.CrossBars) bar.gameObject.AddComponent<Outline>().effectColor = UiPalette.Rim;
                view.Label = WorldTxt(R(root, "Label", Center, Center, new Vector2(0, -21), new Vector2(180, 18)), "P1", 13, TextAnchor.MiddleCenter);
                return root.gameObject;
            }).GetComponent<TeamMarkerView>();

            e.InventoryBlock = Prefab(ElementsDir + "/InventoryBlock.prefab", () =>
            {
                var root = R(null, "InventoryBlock", TopLeft, TopLeft, Vector2.zero, new Vector2(28, 28));
                var view = root.gameObject.AddComponent<InventoryBlockView>();
                view.Rect = root;
                view.Border = Img(root, Color.gray);
                view.Core = Img(Stretch(root, "Core", 3f), Color.white);
                return root.gameObject;
            }).GetComponent<InventoryBlockView>();

            e.CellHighlight = Prefab(ElementsDir + "/CellHighlight.prefab", () =>
                Img(R(null, "CellHighlight", TopLeft, TopLeft, Vector2.zero, new Vector2(InventoryUI.Cell - 2, InventoryUI.Cell - 2)), UiPalette.MarkGreen.A(0.45f)).gameObject)
                .GetComponent<Image>();

            e.ControlRow = Prefab(ElementsDir + "/ControlRow.prefab", () =>
            {
                // a line of the form: the action, a dotted rule, the key on a paper key
                var root = R(null, "ControlRow", TopLeft, TopLeft, Vector2.zero, new Vector2(330, 26));
                Fixed(root, 26);
                var view = root.gameObject.AddComponent<ControlRowView>();
                view.Title = Ink(R(root, "Title", Left, Left, new Vector2(2, 0), new Vector2(218, 24)), "—", 14, TextAnchor.MiddleLeft);
                view.Title.resizeTextForBestFit = true;
                view.Title.resizeTextMinSize = 10;
                view.Title.resizeTextMaxSize = 14;
                RuleLine(root, BottomLeft, BottomLeft, new Vector2(0, 0), 330, UiPalette.Rule.A(0.7f));
                view.Key = Instance<Button>(e.Button, root, "Key");
                var krt = (RectTransform)view.Key.transform;
                krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(1, 0.5f);
                krt.anchoredPosition = new Vector2(0, 1);
                krt.sizeDelta = new Vector2(104, 22);
                view.KeyLabel = view.Key.GetComponentInChildren<Text>();
                view.KeyLabel.text = "E";
                view.KeyLabel.font = FontOf(Face.Mono);
                view.KeyLabel.resizeTextMaxSize = 13;
                view.KeyLabel.fontSize = 13;
                view.KeyBackground = view.Key.GetComponent<Image>();
                // The key's colour is set by the panel (normal / waiting / taken twice): the button only tints it.
                var colors = view.Key.colors;
                colors.normalColor = colors.selectedColor = Color.white;
                colors.highlightedColor = new Color(0.94f, 0.94f, 0.88f, 1f);
                colors.pressedColor = new Color(0.82f, 0.82f, 0.78f, 1f);
                colors.disabledColor = new Color(1f, 1f, 1f, 0.7f);
                view.Key.colors = colors;
                return root.gameObject;
            }).GetComponent<ControlRowView>();

            e.MapRect = Prefab(ElementsDir + "/MapRect.prefab", () =>
            {
                // Anchored at the map's origin, pivot bottom-left: position = corner, size = extent (set by MapView).
                var rt = R(null, "MapRect", Center, BottomLeft, Vector2.zero, new Vector2(10, 10));
                return Img(rt, Color.white).gameObject;
            }).GetComponent<Image>();

            e.MapIcon = Prefab(ElementsDir + "/MapIcon.prefab", () =>
            {
                var root = R(null, "MapIcon", Center, Center, Vector2.zero, Vector2.zero);
                var view = root.gameObject.AddComponent<MapIconView>();
                view.Rect = root;
                view.Arrow = Img(R(root, "Arrow", Center, Center, Vector2.zero, new Vector2(15, 15)), Color.white, S("MapArrow"));
                view.Dot = Img(R(root, "Dot", Center, Center, Vector2.zero, new Vector2(9, 9)), Color.white, S("Disc"));
                var diamond = R(root, "Diamond", Center, Center, Vector2.zero, new Vector2(9, 9));
                diamond.localEulerAngles = new Vector3(0, 0, 45);
                view.Diamond = Img(diamond, Color.white);
                // the marks are highlighter colours on paper: an ink rim keeps the pale ones (yellow, light blue) in sight
                foreach (var mark in new Graphic[] { view.Dot, view.Diamond })
                {
                    var rim = mark.gameObject.AddComponent<Outline>();
                    rim.effectColor = UiPalette.Ink.A(0.75f);
                    rim.effectDistance = new Vector2(1f, -1f);
                }
                // a robot or a device on the map terminal: the picture of its kind (MapView.KindIcons)
                view.Glyph = Img(R(root, "Glyph", Center, Center, Vector2.zero, new Vector2(18, 18)), Color.white, Icon("robot_guard"));
                view.Glyph.preserveAspect = true;
                view.Label = Ink(R(root, "Label", Center, Center, new Vector2(0, -14), new Vector2(160, 16)), "P1", 12, TextAnchor.MiddleCenter, Face.Bold);
                view.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
                return root.gameObject;
            }).GetComponent<MapIconView>();

            e.Callout = Prefab(ElementsDir + "/CalloutMarker.prefab", () =>
            {
                var root = R(null, "CalloutMarker", BottomLeft, Center, Vector2.zero, Vector2.zero);
                var view = root.gameObject.AddComponent<CalloutMarkerView>();
                view.Rect = root;
                var diamond = R(root, "Diamond", Center, Center, Vector2.zero, new Vector2(13, 13));
                diamond.localEulerAngles = new Vector3(0, 0, 45);
                view.Diamond = Img(diamond, Color.white);
                diamond.gameObject.AddComponent<Outline>().effectColor = UiPalette.Rim;
                Img(R(diamond, "Hole", Center, Center, Vector2.zero, new Vector2(5, 5)), UiPalette.Rim.A(0.6f));
                view.Ring = Img(R(root, "Ring", Center, Center, Vector2.zero, new Vector2(30, 30)), Color.white, S("Ring"));
                view.Label = WorldTxt(R(root, "Label", Center, Center, new Vector2(0, 17), new Vector2(220, 18)), "12", 13, TextAnchor.MiddleCenter);
                return root.gameObject;
            }).GetComponent<CalloutMarkerView>();

            e.RouteSign = Prefab(ElementsDir + "/RouteSign.prefab", () =>
            {
                // A world-space notice by a sewer tunnel, on its stand (Level/Sewer/RouteSignStand): a typed sheet with a
                // coloured border the presenter sets (chosen, voted for, plain), just the two lines it holds — where it
                // leads, how big and guarded that house is (or the countdown while the car stands in that tunnel).
                const float w = 360f, h = 66f;
                var go = new GameObject("RouteSign", typeof(RectTransform));
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(w, h);
                rt.localScale = Vector3.one * 0.0068f; // ~2.6 x 0.95 m: readable from the middle of the hall
                var view = go.AddComponent<RouteSignView>();
                view.Group = go.AddComponent<CanvasGroup>();
                view.Frame = Img(Stretch(go.transform, "Frame"), UiPalette.PaperShade);
                Img(Stretch(view.Frame.rectTransform, "Back", 4f), UiPalette.PaperDim, SheetSprite).type = Image.Type.Sliced;
                // the house's kind on the left (office, hospital, garage…), its main job (vault / hostage) on the right
                const float ix = 62f, jw = 42f;
                view.KindIcon = Img(R(go.transform, "KindIcon", Left, Left, new Vector2(8, 0), new Vector2(48, 48)), Color.white, Icon("house_office"));
                view.KindIcons = new[] { "house_office", "house_business", "house_datacenter", "house_warehouse", "house_hq", "house_hospital", "house_garage", "house_apartments", "house_abandoned", "house_testrooms" }
                    .Select(n => Icon(n)).ToArray();
                if (view.KindIcons.Length != System.Enum.GetValues(typeof(Stealth.Level.HouseKind)).Length)
                    throw new System.InvalidOperationException("RouteSign: one icon per HouseKind");
                view.JobIcon = Img(R(go.transform, "JobIcon", Right, Right, new Vector2(-8, 0), new Vector2(38, 38)), UiPalette.MarkOrange, Icon("job_vault"));
                view.VaultIcon = Icon("job_vault");
                view.HostageIcon = Icon("job_hostage");
                view.Title = Ink(R(go.transform, "Title", TopLeft, TopLeft, new Vector2(ix, -4), new Vector2(w - ix - jw - 8, 30)), "—", 23, TextAnchor.MiddleLeft, Face.Bold);
                view.Title.horizontalOverflow = HorizontalWrapMode.Overflow;
                RuleLine(go.transform, TopLeft, TopLeft, new Vector2(ix, -36), w - ix - jw - 8, UiPalette.Ink.A(0.5f), 1.5f);
                view.Body = Ink(R(go.transform, "Body", TopLeft, TopLeft, new Vector2(ix, -39), new Vector2(w - ix - jw - 8, 24)), "—", 16, TextAnchor.MiddleLeft, Face.Sans, UiPalette.InkSoft);
                view.Body.resizeTextForBestFit = true; // the main job, the floors and the guards on one line: shrinks to fit
                view.Body.resizeTextMinSize = 10;
                view.Body.resizeTextMaxSize = 16;
                // the countdown takes the body's line (same place, stamp red) instead of a third, mostly empty one
                view.Votes = Ink(R(go.transform, "Votes", TopLeft, TopLeft, new Vector2(ix, -39), new Vector2(w - ix - jw - 8, 24)), "", 16, TextAnchor.MiddleLeft, Face.Bold, UiPalette.Stamp);
                view.Votes.resizeTextForBestFit = true;
                view.Votes.resizeTextMinSize = 10;
                view.Votes.resizeTextMaxSize = 16;
                SetLayerRecursive(go, Stealth.Level.LevelMetrics.WorldTextLayer); // drawn after the style filter: readable in every style
                return go;
            }).GetComponent<RouteSignView>();

            e.DialNumber = Prefab(ElementsDir + "/DialNumber.prefab", () =>
                Ink(R(null, "DialNumber", Center, Center, Vector2.zero, new Vector2(40, 22)), "0", 16, TextAnchor.MiddleCenter, Face.Mono).gameObject)
                .GetComponent<Text>();
            e.DialTick = Prefab(ElementsDir + "/DialTick.prefab", () =>
                Img(R(null, "DialTick", Center, Center, Vector2.zero, new Vector2(2, 10)), UiPalette.Ink).gameObject).GetComponent<Image>();

            e.ControlGroup = Prefab(ElementsDir + "/ControlGroup.prefab", () =>
            {
                var t = Ink(R(null, "ControlGroup", TopLeft, TopLeft, Vector2.zero, new Vector2(330, 26)), "—", 15, TextAnchor.LowerLeft, Face.Caps, UiPalette.Stamp);
                Fixed(t, 30);
                return t.gameObject;
            }).GetComponent<Text>();
            return e;
        }

        /// <summary>
        /// The F4 cheat panel: its own canvas with a raycaster (the HUD takes no clicks), hidden until opened; five tabs along
        /// the top, a page each (<see cref="CheatPanel.Tab"/>): the cheats, the locations, the items, the car, the visual style.
        /// </summary>
        static void BuildCheatPanel(UiElements e, Transform content)
        {
            var holder = R(content, "Cheats", Center, Center, Vector2.zero, Vector2.zero);
            holder.gameObject.AddComponent<Canvas>();
            holder.gameObject.AddComponent<GraphicRaycaster>();
            var panel = holder.gameObject.AddComponent<CheatPanel>();
            var root = R(holder, "Panel", Center, Center, Vector2.zero, new Vector2(960, 580));
            Card(root, UiPalette.CarbonYellow).raycastTarget = true;
            Clip(root, new Vector2(0, 0));
            panel.Root = root.gameObject;

            // the tabs along the top
            var tabs = R(root, "Tabs", TopCenter, TopCenter, new Vector2(0, -18), new Vector2(900, 36));
            var row = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            var tabKeys = new[] { "cheats.tab_cheats", "cheats.tab_houses", "cheats.tab_items", "cheats.tab_car", "cheats.tab_visual" };
            if (tabKeys.Length != System.Enum.GetValues(typeof(CheatPanel.Tab)).Length) throw new System.InvalidOperationException("CheatPanel: a label per tab");
            panel.Tabs = tabKeys.Select((k, i) => ButtonIn(e, tabs, "Tab" + (CheatPanel.Tab)i, L(k), 36)).ToArray();
            RuleLine(root, TopCenter, TopCenter, new Vector2(0, -64), 900, UiPalette.Ink, 2f);
            var pages = tabKeys.Select((_, i) => R(root, "Page" + (CheatPanel.Tab)i, TopCenter, TopCenter, new Vector2(0, -76), new Vector2(900, 488))).ToArray();
            panel.Pages = pages.Select(p => p.gameObject).ToArray();

            // 1: the cheats
            var col = Column(pages[0], "Column", Vector2.zero, 420, 7f);
            panel.KillRobots = ButtonIn(e, col, "KillRobots", L("cheats.kill_robots"), 36);
            panel.Invulnerable = ButtonIn(e, col, "Invulnerable", "", 36);
            panel.Invisible = ButtonIn(e, col, "Invisible", "", 36);
            panel.Quota = ButtonIn(e, col, "Quota", L("cheats.quota"), 36);
            panel.ToHatch = ButtonIn(e, col, "ToHatch", L("cheats.to_hatch"), 36);
            panel.RevealMap = ButtonIn(e, col, "RevealMap", L("cheats.reveal_map"), 36);

            // 2: start a house of any kind right now (one button per HouseKind, in its order)
            var houses = CheatGrid(pages[1], "Houses", Vector2.zero, new Vector2(900, 300), new Vector2(288, 40), 3);
            panel.Houses = System.Enum.GetValues(typeof(Stealth.Level.HouseKind)).Cast<Stealth.Level.HouseKind>()
                .Select(k => CheatButton(e, houses, "House" + k, L(Stealth.Level.HouseKinds.NameKey(k)), 17)).ToArray();

            // 3: an item of any kind in front of you (its own rarity), its icon left of its name (set by the panel); no old
            // tetris pieces; the check box over them hides what is only fuel
            var filter = CheatGrid(pages[2], "Filter", Vector2.zero, new Vector2(900, 30), new Vector2(320, 30), 1);
            filter.GetComponent<GridLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            panel.OnlyInteractive = CheatButton(e, filter, "OnlyInteractive", "", 15);
            var items = CheatGrid(pages[2], "Items", new Vector2(0, -38), new Vector2(900, 450), new Vector2(145, 36), 6, 4f);
            var visuals = AssetDatabase.LoadAssetAtPath<ItemVisuals>(DataDir + "/ItemVisuals.asset");
            panel.Items = ItemCatalog.Shapes.Select((s, i) =>
            {
                if (ItemCatalog.IsPiece(i)) return null;
                var b = CheatButton(e, items, "Item_" + s.Id, "", 11);
                var icon = visuals != null && visuals.Icons != null && i < visuals.Icons.Length ? visuals.Icons[i] : null;
                var img = Img(R(b.transform, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(19, 0), new Vector2(30, 30)), Color.white, icon);
                img.preserveAspect = true;
                img.raycastTarget = false;
                var text = b.GetComponentInChildren<Text>();
                text.rectTransform.offsetMin = new Vector2(38, text.rectTransform.offsetMin.y);
                text.alignment = TextAnchor.MiddleLeft;
                return b;
            }).ToArray();

            // 4: the car's upgrades at any level (a row per upgrade: its name, then a button per level 0..3)
            const int levels = Stealth.Vehicles.CarUpgrades.MaxLevelled + 1;
            var upgrades = Column(pages[3], "Upgrades", Vector2.zero, 640, 8f);
            var all = System.Enum.GetValues(typeof(Stealth.Vehicles.CarUpgrade)).Cast<Stealth.Vehicles.CarUpgrade>().ToArray();
            panel.UpgradeRows = new GameObject[all.Length];
            panel.UpgradeLevels = new Button[all.Length * levels];
            foreach (var u in all)
            {
                var line = R(upgrades, u.ToString(), Center, Center, Vector2.zero, Vector2.zero);
                Fixed(line, 36);
                var h = line.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 8;
                h.childControlWidth = h.childControlHeight = true;
                h.childForceExpandWidth = false;
                h.childForceExpandHeight = true;
                var name = Ink(R(line, "Name", Center, Center, Vector2.zero, Vector2.zero), "", 18, TextAnchor.MiddleLeft, Face.Caps);
                SetText(name, L(Stealth.Game.GameManager.UpgradeKey(u)));
                Fixed(name, 36, 280);
                for (int level = 0; level < levels; level++)
                {
                    var b = ButtonIn(e, line, "Level" + level, level.ToString(), 36);
                    Fixed(b, 36, 76);
                    panel.UpgradeLevels[(int)u * levels + level] = b;
                }
                panel.UpgradeRows[(int)u] = line.gameObject;
            }

            // 5: the visual style (the page makes a button per style from the template)
            var styles = pages[4].gameObject.AddComponent<StylePanel>();
            styles.List = CheatGrid(pages[4], "List", Vector2.zero, new Vector2(900, 488), new Vector2(288, 36), 3);
            styles.Template = Instance<Button>(e.Button, pages[4], "Template");
            SetText(styles.Template.GetComponentInChildren<Text>(), "");
            styles.Template.gameObject.SetActive(false);

            for (int i = 1; i < pages.Length; i++) pages[i].gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        /// <summary>A grid of equal buttons on a cheat panel page, top centre.</summary>
        static RectTransform CheatGrid(Transform page, string name, Vector2 pos, Vector2 size, Vector2 cell, int columns, float gap = 8f)
        {
            var rt = R(page, name, TopCenter, TopCenter, pos, size);
            var grid = rt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(gap, gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperCenter;
            return rt;
        }

        /// <summary>A button of a cheat grid: its label shrinks to fit (long item names).</summary>
        static Button CheatButton(UiElements e, Transform grid, string name, string label, int size)
        {
            var b = Instance<Button>(e.Button, grid, name);
            var t = b.GetComponentInChildren<Text>();
            SetText(t, label);
            t.fontSize = size;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 9;
            t.resizeTextMaxSize = size;
            return b;
        }

        // ------------------------------------------------------------------ minigames

        /// <summary>A picture from <c>Art/Sprites/UI/Minigame</c> (Tools/Art/generate_minigame_ui.py), 9-sliced when a border is given.</summary>
        static Sprite MgSprite(string name, int border = 0)
        {
            var path = $"{SpritesDir}/UI/Minigame/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[Builder] {path} missing: run python Tools/Art/generate_minigame_ui.py");
                return null;
            }
            var want = new Vector4(border, border, border, border);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency || importer.spriteBorder != want)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.spriteBorder = want;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Image Sliced(Image i)
        {
            i.type = Image.Type.Sliced;
            return i;
        }

        /// <summary>
        /// A minigame's sheet: a form on a clipboard's clip, a soft shadow under it, a red flash over it for a wrong try,
        /// the title in capitals with a line under it. Returns the sheet, the flash and the title.
        /// </summary>
        static (RectTransform root, Image flash, Text title) MinigamePanel(Transform holder, Vector2 pos, Vector2 size, string title, float titleWidth)
        {
            var (root, _) = Sheet(holder, "Panel", Center, Center, pos, size);
            var flash = Sliced(Img(Stretch(root, "Flash"), UiPalette.Stamp.A(0.3f), SheetSprite));
            Clip(root, new Vector2(0, 0));
            var text = Ink(R(root, "Title", TopCenter, TopCenter, new Vector2(0, -22), new Vector2(titleWidth, 30)), title, 20, TextAnchor.LowerCenter, Face.Caps);
            RuleLine(root, TopCenter, TopCenter, new Vector2(0, -56), size.x - 60f, UiPalette.Ink, 2f);
            return (root, flash, text);
        }

        /// <summary>
        /// The safe minigame on a sheet: three form boxes the numbers are written into, a sticker under each that lights
        /// while that number is listened for; the combination dial printed on a paper disc (ticks and numbers added by the
        /// view) with an ink knob that turns with it, the number typed on its cap, a red pointer; under it a strip of graph
        /// paper where every click of the dial is a pen stroke as tall as the dial is close.
        /// </summary>
        static void BuildSafeCrack(UiElements e, Transform content)
        {
            // The view stays active and shows / hides its panel (a component that hides its own object stops updating).
            var holder = R(content, "SafeCrack", Center, Center, Vector2.zero, Vector2.zero);
            var view = holder.gameObject.AddComponent<SafeCrackView>();
            var all = R(holder, "All", Center, Center, Vector2.zero, Vector2.zero);
            view.Root = all.gameObject;
            var (root, flash, _) = MinigamePanel(all, new Vector2(0, 0), new Vector2(470, 640), L("safe.title"), 400);
            view.Panel = all;
            view.Flash = flash;

            // Code boxes with a sticker under each.
            view.Slots = new Image[3];
            view.SlotGlows = new Image[3];
            view.SlotTexts = new Text[3];
            view.SlotLeds = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = R(root, $"Slot{i}", TopCenter, TopCenter, new Vector2((i - 1) * 86f, -82), new Vector2(70, 46));
                view.Slots[i] = FormBox(slot, UiPalette.Paper);
                var mark = R(slot, "Glow", Center, Center, new Vector2(0, -2), new Vector2(78, 26));
                view.SlotGlows[i] = Sliced(Img(mark, Color.clear, HighlightSprite));
                view.SlotTexts[i] = Ink(Stretch(slot, "Digit"), "", 30, TextAnchor.MiddleCenter, Face.Mono, UiPalette.Pen);
                view.SlotLeds[i] = Img(R(slot, "Led", Center, Center, new Vector2(0, -36), new Vector2(14, 14)), UiPalette.PaperShade, PaperSprite("sticker"));
            }

            // The dial.
            var face = R(root, "Dial", Center, Center, new Vector2(0, -2), new Vector2(262, 262));
            Img(R(face, "Shadow", Center, Center, new Vector2(3, -6), new Vector2(300, 300)), new Color(0, 0, 0, 0.35f), S("SoftGlow"));
            view.Ring = Img(R(face, "Ring", Center, Center, Vector2.zero, new Vector2(300, 300)), UiPalette.Good.A(0.8f), S("Ring"));
            Img(R(face, "Face", Center, Center, Vector2.zero, new Vector2(262, 262)), Color.white, PaperSprite("dial_face"));
            view.Dial = R(face, "Turning", Center, Center, Vector2.zero, Vector2.zero);
            view.TickPrefab = e.DialTick;
            view.NumberPrefab = e.DialNumber;
            view.TickRadius = 124f;
            view.NumberRadius = 88f;
            // the knob turns with the dial (its flutes and notch show it turning)
            Img(R(view.Dial, "Knob", Center, Center, Vector2.zero, new Vector2(118, 118)), Color.white, PaperSprite("dial_knob"));
            view.ReadoutGlow = Img(R(face, "ReadoutGlow", Center, Center, Vector2.zero, new Vector2(84, 56)), UiPalette.MarkYellow.A(0.15f), S("SoftGlow"));
            view.Readout = Ink(R(face, "Readout", Center, Center, new Vector2(0, -1), new Vector2(80, 40)), "00", 30, TextAnchor.MiddleCenter, Face.Mono, UiPalette.Paper);
            view.FaceFlash = Img(R(face, "Flash", Center, Center, Vector2.zero, new Vector2(256, 256)), UiPalette.Stamp.A(0.35f), S("Disc"));
            Img(R(face, "Pointer", Center, Center, new Vector2(0, 144), new Vector2(26, 26)), Color.white, PaperSprite("pointer"));

            // The recorder: graph paper in a box, the pen's trace a chain of short strokes.
            var scope = R(root, "Scope", BottomCenter, BottomCenter, new Vector2(0, 44), new Vector2(330, 86));
            FormBox(scope, UiPalette.Paper);
            var grid = Img(Stretch(scope, "Graph", 1.5f), UiPalette.Rule.A(0.8f), PaperSprite("graph"));
            grid.type = Image.Type.Tiled;
            var trace = R(scope, "Trace", Center, Center, Vector2.zero, new Vector2(310, 72));
            view.Trace = trace;
            const int segments = 56;
            view.TraceGlow = new Image[segments];
            view.TraceLine = new Image[segments];
            for (int i = 0; i < segments; i++)
            {
                view.TraceGlow[i] = Img(R(trace, $"G{i}", Center, Center, Vector2.zero, new Vector2(8, 6)), Color.clear, S("SoftGlow"));
                view.TraceLine[i] = Img(R(trace, $"L{i}", Center, Center, Vector2.zero, new Vector2(8, 2.2f)), UiPalette.Pen);
            }

            view.Hint = Ink(R(root, "Hint", BottomCenter, BottomCenter, new Vector2(0, 12), new Vector2(440, 26)), "", 13, TextAnchor.MiddleCenter, Face.Sans, UiPalette.InkSoft);
            all.gameObject.SetActive(false);
        }

        /// <summary>
        /// The vault's switch and sequence locks on a sheet: a row of round stickers that light up (a highlighter haze round
        /// a lit one), a paper key with the number under each; the sequence's progress as small stickers; a red flash for a
        /// wrong press; the keys to use.
        /// </summary>
        static void BuildVaultLock(Transform content)
        {
            var holder = R(content, "VaultLock", Center, Center, Vector2.zero, Vector2.zero);
            var view = holder.gameObject.AddComponent<VaultLockView>();
            var all = R(holder, "All", Center, Center, Vector2.zero, Vector2.zero);
            view.Root = all.gameObject;
            var (root, flash, title) = MinigamePanel(all, new Vector2(0, -20), new Vector2(600, 340), "", 520);
            view.Flash = flash;
            view.Title = title;
            const int lamps = 7;
            view.Slots = new RectTransform[lamps];
            view.Halos = new Image[lamps];
            view.Lamps = new Image[lamps];
            view.Keys = new Text[lamps];
            for (int i = 0; i < lamps; i++)
            {
                var slot = R(root, $"Lamp{i}", Center, Center, new Vector2(0, 22), Vector2.zero);
                view.Slots[i] = slot;
                view.Halos[i] = Img(R(slot, "Halo", Center, Center, Vector2.zero, new Vector2(120, 120)), UiPalette.MarkYellow.A(0f), S("SoftGlow"));
                Img(R(slot, "Ring", Center, Center, new Vector2(0, -1), new Vector2(74, 74)), UiPalette.InkSoft.A(0.6f), S("Ring"));
                view.Lamps[i] = Img(R(slot, "Lens", Center, Center, Vector2.zero, new Vector2(64, 64)), UiPalette.PaperShade, PaperSprite("sticker"));
                var cap = R(slot, "Cap", Center, Center, new Vector2(0, -72), new Vector2(38, 38));
                Sliced(Img(cap, Color.white, PaperSprite("keycap", new Vector4(10, 13, 10, 10))));
                view.Keys[i] = Ink(R(cap, "Key", Center, Center, new Vector2(0, 2), new Vector2(38, 32)), (i + 1).ToString(), 18, TextAnchor.MiddleCenter, Face.Mono);
            }
            const int dots = 8;
            view.Dots = new Image[dots];
            view.DotGlows = new Image[dots];
            for (int i = 0; i < dots; i++)
            {
                view.DotGlows[i] = Img(R(root, $"DotGlow{i}", Center, Center, new Vector2(0, -122), new Vector2(30, 30)), UiPalette.MarkGreen.A(0f), S("SoftGlow"));
                view.Dots[i] = Img(R(root, $"Dot{i}", Center, Center, new Vector2(0, -122), new Vector2(16, 16)), UiPalette.PaperShade, PaperSprite("sticker"));
            }
            view.Hint = Ink(R(root, "Hint", BottomCenter, BottomCenter, new Vector2(0, 12), new Vector2(560, 24)), "", 13, TextAnchor.MiddleCenter, Face.Sans, UiPalette.InkSoft);
            all.gameObject.SetActive(false);
        }

        /// <summary>
        /// A map: a sheet with a clipped viewport; inside, a pivot and the content in metres with a layer of rectangles
        /// (the plan, drawn in ink on the paper by MapView) and a layer of icons. Its own canvas: redrawing the map does not
        /// rebuild the HUD.
        /// </summary>
        static MapView BuildMap(UiElements e, Transform parent, string name, bool follow, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float scale)
        {
            var root = R(parent, name, anchor, pivot, pos, size);
            root.gameObject.AddComponent<Canvas>();
            if (follow) Img(root, UiPalette.PaperDim);
            else Card(root);
            var view = root.gameObject.AddComponent<MapView>();
            view.FollowPlayer = follow;
            view.Scale = scale;
            float captionH = follow ? 0f : 36f;
            var viewport = Stretch(root, "Viewport", follow ? 4f : 10f);
            viewport.offsetMax = new Vector2(follow ? -4f : -14f, (follow ? -4f : -10f) - captionH);
            if (!follow) viewport.offsetMin = new Vector2(10f, 14f);
            viewport.gameObject.AddComponent<RectMask2D>();
            if (!follow)
            {
                var grid = Img(Stretch(viewport, "Graph"), UiPalette.Rule.A(0.35f), PaperSprite("graph"));
                grid.type = Image.Type.Tiled;
            }
            view.Viewport = viewport;
            view.Pivot = R(viewport, "Pivot", Center, Center, Vector2.zero, Vector2.zero);
            view.Content = R(view.Pivot, "Content", Center, Center, Vector2.zero, Vector2.zero);
            view.RectLayer = R(view.Content, "Rects", Center, Center, Vector2.zero, Vector2.zero);
            view.IconLayer = R(view.Content, "Icons", Center, Center, Vector2.zero, Vector2.zero);
            view.RectPrefab = e.MapRect;
            view.IconPrefab = e.MapIcon;
            view.KindIcons = RobotKindIcons();
            if (follow)
                view.Caption = Ink(R(root, "Caption", TopLeft, TopLeft, new Vector2(6, -2), new Vector2(120, 18)), "1", 12, TextAnchor.MiddleLeft, Face.Bold, UiPalette.InkSoft);
            else
            {
                view.Caption = Ink(R(root, "Caption", TopLeft, TopLeft, new Vector2(16, -8), new Vector2(size.x - 40, 30)), "1", 18, TextAnchor.LowerLeft, Face.Caps);
                RuleLine(root, TopLeft, TopLeft, new Vector2(14, -42), size.x - 32, UiPalette.Ink, 2f);
            }
            return view;
        }

        /// <summary>The pictures of the robots and devices, one per <see cref="Stealth.Level.RobotKind"/> (Tools/Art/generate_icons.py robot_*).</summary>
        static Sprite[] RobotKindIcons() => System.Enum.GetNames(typeof(Stealth.Level.RobotKind))
            .Select(n => Icon("robot_" + n.ToLowerInvariant())).ToArray();

        /// <summary>A plain bar over the world: a dark track, the fill (pivot left, stretched by anchorMax.x).</summary>
        static BarView Bar(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color background, Color fill, float inset)
        {
            var rt = R(parent, name, anchor, pivot, pos, size);
            Img(rt, background);
            var area = Stretch(rt, "Area", inset);
            var f = Stretch(area, "Fill");
            f.anchorMax = new Vector2(0.5f, 1f);
            var view = rt.gameObject.AddComponent<BarView>();
            view.Fill = f;
            view.FillImage = Img(f, fill);
            return view;
        }

        // ------------------------------------------------------------------ inventory

        /// <summary>A UI picture from <c>Art/Sprites/UI</c> as a sprite.</summary>
        static Sprite UiSprite(string name)
        {
            var path = $"{SpritesDir}/UI/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[Builder] {path} missing");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// The backpack as an inventory form: a card on a clip with graph paper where the grid is, the total typed on a
        /// highlighted caption above it; the belt bag and the hand slot are sticky notes stuck beside it.
        /// </summary>
        static GameObject BuildInventoryScreen(UiElements e)
        {
            var root = CanvasRoot("Inventory", 5, false);
            var ui = root.AddComponent<InventoryUI>();
            ui.Canvas = (RectTransform)root.transform;
            const float c = InventoryUI.Cell;
            float gw = InventoryLayout.Width * c, gh = InventoryLayout.Height * c;

            const float pad = 16f;
            ui.Panel = R(root.transform, "Panel", BottomCenter, BottomCenter, new Vector2(0, 13), new Vector2(gw + 2 * pad, gh + 2 * pad + 24));
            ui.GridBackground = Card(R(ui.Panel, "GridBackground", BottomCenter, BottomCenter, Vector2.zero, new Vector2(gw + 2 * pad + 4, gh + 2 * pad + 4)));
            var paper = Img(R(ui.GridBackground.rectTransform, "Graph", TopLeft, TopLeft, new Vector2(pad, -pad), new Vector2(gw, gh)), UiPalette.Rule.A(0.55f), PaperSprite("graph"));
            paper.type = Image.Type.Tiled;
            var header = R(ui.Panel, "Header", BottomCenter, BottomCenter, new Vector2(0, gh + 2 * pad + 2), new Vector2(gw + 2 * pad, 24));
            ui.Header = header.gameObject;
            ui.HeaderBackground = Sliced(Img(R(header, "Mark", Left, Left, new Vector2(2, 0), new Vector2(130, 20)), UiPalette.NoteYellow, HighlightSprite));
            ui.ValueLabel = Ink(R(header, "Value", Left, Left, new Vector2(8, 0), new Vector2(200, 22)), "0", 15, TextAnchor.MiddleLeft, Face.Mono);
            ui.ValueLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            WorldRim(ui.ValueLabel, 0.6f); // the compact backpack lies over the world: a light rim keeps the total readable there

            ui.Grid = R(ui.Panel, "Grid", BottomCenter, TopCenter, new Vector2(0, pad + gh), new Vector2(gw, gh));
            ui.Grid.pivot = TopLeft;
            ui.Grid.anchoredPosition = new Vector2(-gw / 2f, pad + gh + 2);
            var cells = new List<Image>();
            for (int y = 0; y < InventoryLayout.Height; y++)
                for (int x = 0; x < InventoryLayout.Width; x++)
                    if (InventoryLayout.IsCell(x, y))
                        cells.Add(Img(R(ui.Grid, $"Cell_{x}_{y}", TopLeft, TopLeft, new Vector2(x * c + 1, -(y * c + 1)), new Vector2(c - 2, c - 2)), UiPalette.Pen.A(0f)));
            ui.Cells = cells.ToArray();
            ui.ItemLayer = Stretch(ui.Grid, "Items");
            ui.ItemLayer.pivot = TopLeft;
            ui.HighlightLayer = Stretch(ui.Grid, "Highlights");
            ui.HighlightLayer.pivot = TopLeft;

            // the belt bag's 4 x 4 grid left of the backpack (shown while one is worn): in the grid's cell space, columns BeltUi..
            int bs = Stealth.Player.PlayerInventory.BeltSize;
            var belt = R(ui.Grid, "Belt", TopLeft, TopLeft, new Vector2(InventoryUI.BeltUi * c, 0f), new Vector2(bs * c, bs * c));
            belt.pivot = TopLeft;
            belt.anchoredPosition = new Vector2(InventoryUI.BeltUi * c, 0f);
            // the note reaches above the grid for its handwritten label (below it would leave the screen)
            var beltNote = Note(belt, "Background", Center, Center, new Vector2(0, 11), new Vector2(bs * c + 16, bs * c + 40), UiPalette.NoteGreen, 1.2f);
            beltNote.SetAsFirstSibling();
            var beltCells = new List<Image>();
            for (int y = 0; y < bs; y++)
                for (int x = 0; x < bs; x++)
                    beltCells.Add(Img(R(belt, $"Cell_{x}_{y}", TopLeft, TopLeft, new Vector2(x * c + 1, -(y * c + 1)), new Vector2(c - 2, c - 2)), UiPalette.Ink.A(0.06f)));
            var beltLabel = Ink(R(belt, "Label", TopCenter, BottomCenter, new Vector2(0, 1), new Vector2(bs * c, 24)), L("inventory.belt"), 20, TextAnchor.MiddleCenter, Face.Hand, UiPalette.Pen);
            beltLabel.resizeTextForBestFit = true;
            beltLabel.resizeTextMinSize = 12;
            beltLabel.resizeTextMaxSize = 20;
            ui.BeltPanel = belt.gameObject;
            ui.BeltGrid = belt;
            ui.BeltCells = beltCells.ToArray();
            belt.gameObject.SetActive(false);
            // (the items and highlights layers are drawn after it: the grid's own Items/Highlights children come later)
            ui.ItemLayer.SetAsLastSibling();
            ui.HighlightLayer.SetAsLastSibling();

            // the hand slot right of the backpack: one held item (the flashlight, batteries), used with the hand key
            ui.HandSlot = R(ui.Panel, "HandSlot", new Vector2(1, 0), BottomLeft, new Vector2(10, 0), new Vector2(2 * c + 14, 3 * c + 32));
            ui.HandBackground = NoteBack(ui.HandSlot, UiPalette.NoteYellow);
            ui.HandSlot.localEulerAngles = new Vector3(0, 0, -1.5f);
            var hole = R(ui.HandSlot, "Hole", TopCenter, TopCenter, new Vector2(0, -8), new Vector2(2 * c - 4, 3 * c + 32 - 32));
            ui.HandItems = R(hole, "HandItems", Center, TopLeft, Vector2.zero, Vector2.zero);
            ui.HandLabel = Ink(R(ui.HandSlot, "Label", BottomCenter, BottomCenter, new Vector2(0, 1), new Vector2(2 * c + 14, 22)), "", 18, TextAnchor.MiddleCenter, Face.Hand, UiPalette.Pen);

            BuildBenchPanel(e, root.transform, ui);

            ui.Ghost = R(root.transform, "DragGhost", BottomLeft, TopLeft, Vector2.zero, Vector2.zero);
            ui.BlockPrefab = e.InventoryBlock;
            ui.HighlightPrefab = e.CellHighlight;
            ui.Tooltip = Ink(R(root.transform, "Tooltip", BottomLeft, TopLeft, Vector2.zero, new Vector2(340, 22)), "", 14, TextAnchor.MiddleLeft, Face.Sans);
            Slip(ui.Tooltip, true, new Vector2(8, 3)).MinWidth = 40f;
            return root;
        }

        /// <summary>A light ink rim round a text that sits on paper and, at times, over the world.</summary>
        static void WorldRim(Text t, float alpha)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = UiPalette.Paper.A(alpha);
            o.effectDistance = new Vector2(1, -1);
        }

        /// <summary>
        /// The car's workbench above the open backpack (shown while the bench is open, InventoryUI.Bench): a card with its
        /// 4 x 4 grid on graph paper on the left, «combine» right of it a little under the middle with what comes of it
        /// over the button (a picture and a name, or «result unknown»), and an (i) in the corner whose rules show on hover.
        /// </summary>
        static void BuildBenchPanel(UiElements e, Transform root, InventoryUI ui)
        {
            const float c = InventoryUI.Cell, pad = 14f, title = 30f, side = 170f, gap = 14f;
            int n = Stealth.Items.CraftBook.BenchSize;
            float gw = n * c, gh = n * c;
            float w = pad + gw + gap + side + pad, h = title + pad + gh + pad;
            // above the backpack (its panel is ~190 px high, 13 px off the bottom)
            var panel = R(root, "Bench", BottomCenter, BottomCenter, new Vector2(0, 222), new Vector2(w, h));
            Card(Stretch(panel, "Background"));
            Ink(R(panel, "Title", TopLeft, TopLeft, new Vector2(pad, -4), new Vector2(gw + gap + side - 24, 24)), L("bench.title"), 16, TextAnchor.LowerLeft, Face.Caps);
            RuleLine(panel, TopLeft, TopLeft, new Vector2(pad, -title), w - 2 * pad, UiPalette.Ink, 1.5f);

            var grid = R(panel, "Grid", TopLeft, TopLeft, new Vector2(pad, -(title + pad)), new Vector2(gw, gh));
            grid.pivot = TopLeft;
            grid.anchoredPosition = new Vector2(pad, -(title + pad));
            FormBox(R(grid, "Lining", Center, Center, Vector2.zero, new Vector2(gw + 6, gh + 6)), UiPalette.PaperShade);
            var paper = Img(R(grid, "Graph", Center, Center, Vector2.zero, new Vector2(gw, gh)), UiPalette.Rule.A(0.7f), PaperSprite("graph"));
            paper.type = Image.Type.Tiled;
            var cells = new List<Image>();
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    cells.Add(Img(R(grid, $"Cell_{x}_{y}", TopLeft, TopLeft, new Vector2(x * c + 1, -(y * c + 1)), new Vector2(c - 2, c - 2)), UiPalette.Ink.A(0.04f)));
            ui.BenchGrid = grid;
            ui.BenchCells = cells.ToArray();
            ui.BenchItems = Stretch(grid, "Items");
            ui.BenchItems.pivot = TopLeft;
            ui.BenchHighlights = Stretch(grid, "Highlights");
            ui.BenchHighlights.pivot = TopLeft;

            // the right side: the result over the button, the button a little under the middle of the grid
            float sx = pad + gw + gap;
            float mid = -(title + pad + gh / 2f);
            var button = R(panel, "Combine", TopLeft, TopLeft, new Vector2(sx, mid - 14f), new Vector2(side, 36f));
            ui.CombineButton = button;
            ui.CombineBackground = Sliced(Img(Stretch(button, "Background"), UiPalette.NoteOrange, PaperSprite("button", B(8))));
            ui.CombineLabel = Ink(Stretch(button, "Label"), L("bench.combine"), 15, TextAnchor.MiddleCenter, Face.Bold);
            ui.ResultLabel = Ink(R(panel, "Result", TopLeft, BottomLeft, new Vector2(sx, mid - 14f + 4f), new Vector2(side, 36f)), "", 13, TextAnchor.LowerCenter, Face.Sans);
            ui.ResultIcon = Img(R(panel, "ResultIcon", TopLeft, BottomLeft, new Vector2(sx + side / 2f - 20f, mid - 14f + 42f), new Vector2(40f, 40f)), Color.white);
            ui.ResultIcon.preserveAspect = true;
            ui.ResultIcon.raycastTarget = false;
            ui.ResultIcon.gameObject.SetActive(false);

            // the (i): its tip over the panel while hovered
            var info = R(panel, "Info", TopRight, TopRight, new Vector2(-8, -5), new Vector2(20, 20));
            Img(info, Color.white, Icon("info"));
            ui.InfoZone = info;
            var tip = R(panel, "InfoTip", TopRight, BottomRight, new Vector2(0, 8), new Vector2(370, 84));
            Card(Stretch(tip, "Background"), UiPalette.NoteYellow);
            Ink(Stretch(tip, "Text", 10f), L("bench.info"), 13, TextAnchor.MiddleLeft, Face.Sans);
            tip.gameObject.SetActive(false);
            ui.InfoTip = tip.gameObject;
            ui.QuestionIcon = Icon("question");

            ui.BenchPanel = panel.gameObject;
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Team stash over Linus: a world-space clipboard with the stash grid on graph paper and a "burn" sticky note
        /// beside it ("put everything in" is the key in the hint stack). Same units as the inventory
        /// (<see cref="InventoryUI.Cell"/> per cell), scaled to metres; <see cref="StashBoard.Fit"/> sizes the paper to its rows.
        /// </summary>
        static GameObject BuildStashBoard(UiElements e)
        {
            const float c = InventoryUI.Cell, pad = 10f, gap = 12f, header = 30f, sellW = 96f;
            // (built for the biggest box, the backpack's top level: StashBoard.Fit shows the columns and rows the stash has)
            float gw = TeamStash.MaxWidth * c, gh = TeamStash.MaxHeight * c;
            var go = new GameObject("StashBoard", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = StashBoard.Size(new Vector2(pad, -(header + pad)), gw, gh, gap + sellW, pad);
            rt.localScale = Vector3.one * 0.0042f; // ~1.6 m wide with the burn note, 1.15 m without (over Linus: no burning)
            var board = go.AddComponent<StashBoard>();
            board.Canvas = canvas;
            board.Root = rt;
            board.Pad = pad;

            Sliced(Img(Stretch(go.transform, "Background"), UiPalette.PaperDim, SheetSprite));
            board.ValueLabel = Ink(R(go.transform, "Title", TopLeft, TopLeft, new Vector2(pad, -2f), new Vector2(gw, header - 4)),
                "0", 17, TextAnchor.LowerLeft, Face.Mono);
            // «Рюкзак Линуса · N очк.» on one line, as wide as the grid: smaller type on a narrow board, never wrapped or cut
            board.ValueLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            board.ValueLabel.verticalOverflow = VerticalWrapMode.Truncate;
            board.ValueLabel.resizeTextForBestFit = true;
            board.ValueLabel.resizeTextMinSize = 8;
            board.ValueLabel.resizeTextMaxSize = 17;
            board.TitleRule = RuleLine(go.transform, TopLeft, TopLeft, new Vector2(pad, -header + 1), gw + gap + sellW, UiPalette.Ink, 1.5f).rectTransform;
            board.SellSpan = gap + sellW;
            board.Grid = R(go.transform, "Grid", TopLeft, TopLeft, new Vector2(pad, -(header + pad)), new Vector2(gw, gh));
            board.GridBox = R(board.Grid, "Box", Center, Center, Vector2.zero, new Vector2(gw + 4, gh + 4));
            FormBox(board.GridBox, UiPalette.PaperShade);
            var paper = Img(R(board.Grid, "Graph", Center, Center, Vector2.zero, new Vector2(gw, gh)), UiPalette.Rule.A(0.8f), PaperSprite("graph"));
            paper.type = Image.Type.Tiled;
            board.GraphPaper = paper.rectTransform;
            board.BuiltColumns = TeamStash.MaxWidth;
            board.BuiltRows = TeamStash.MaxHeight;
            var cells = new List<Image>();
            for (int y = 0; y < TeamStash.MaxHeight; y++)
                for (int x = 0; x < TeamStash.MaxWidth; x++)
                    cells.Add(Img(R(board.Grid, $"Cell_{x}_{y}", TopLeft, TopLeft, new Vector2(x * c + 1, -(y * c + 1)), new Vector2(c - 2, c - 2)), UiPalette.Ink.A(0.05f)));
            board.Cells = cells.ToArray();
            board.ItemLayer = Stretch(board.Grid, "Items");
            board.ItemLayer.pivot = TopLeft;
            board.HighlightLayer = Stretch(board.Grid, "Highlights");
            board.HighlightLayer.pivot = TopLeft;

            board.SellZone = R(go.transform, "Sell", TopLeft, TopLeft, new Vector2(pad + gw + gap, -(header + pad)), new Vector2(sellW, gh));
            board.SellBackground = Sliced(Img(board.SellZone, UiPalette.NoteYellow, StickySprite));
            var sell = Ink(Stretch(board.SellZone, "Label", 6f), L("stash.sell"), 26, TextAnchor.MiddleCenter, Face.Hand, UiPalette.Pen);
            sell.resizeTextForBestFit = true; // whole words on the narrow note: «сжечь в топливо» never splits a word
            sell.resizeTextMinSize = 12;
            sell.resizeTextMaxSize = 26;
            SetLayerRecursive(go, Stealth.Level.LevelMetrics.WorldTextLayer); // drawn after the style filter: readable in every style
            return go;
        }

        // ------------------------------------------------------------------ menu

        static GameObject BuildMenu(UiElements e)
        {
            var root = CanvasRoot("Menu", 100, true);
            var menu = root.AddComponent<GameMenu>();

            // Main menu: papers on a desk — the form with the ways to play on a clip, the friends on a sticky note.
            var main = Stretch(root.transform, "Main");
            menu.MainPage = main.gameObject;
            var bg = Img(main, UiPalette.Desk);
            bg.raycastTarget = true;
            Img(Stretch(main, "Vignette", -80f), UiPalette.DeskDeep.A(0.9f), S("Vignette"));
            var (sheet, _) = Sheet(main, "Sheet", TopCenter, TopCenter, new Vector2(-190, -54), new Vector2(480, 600));
            sheet.localEulerAngles = new Vector3(0, 0, 0.6f);
            Clip(sheet, new Vector2(0, 0));
            var col = Column(main, "Column", new Vector2(-190, -84), 420);
            ColumnTitle(col, L("menu.game_title"), 32);
            Spacer(col, 12);
            menu.SteamStatus = ColumnText(col, "SteamStatus", "Steam", 14, 22, Face.Caps, color: UiPalette.InkSoft);
            menu.HostSteam = ButtonIn(e, col, "HostSteam", L("menu.host_steam"), 44);
            Spacer(col, 10);
            menu.LocalCaption = ColumnText(col, "LocalCaption", "LAN", 14, 22, Face.Caps, color: UiPalette.InkSoft);
            menu.HostLocal = ButtonIn(e, col, "HostLocal", L("menu.host_local"), 36);
            var row = R(col, "JoinRow", Center, Center, Vector2.zero, Vector2.zero);
            Fixed(row, 34);
            var hr = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hr.spacing = 8;
            hr.childControlWidth = hr.childControlHeight = true;
            hr.childForceExpandWidth = false;
            hr.childForceExpandHeight = true;
            menu.Address = Instance<InputField>(e.InputField, row, "Address");
            menu.Address.text = "127.0.0.1";
            menu.Address.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            menu.Join = ButtonIn(e, row, "Join", L("menu.join_ip"), 34);
            menu.Join.GetComponent<LayoutElement>().preferredWidth = 190;
            Spacer(col, 8);
            menu.NetStatus = ColumnText(col, "NetStatus", "", 14, 22, Face.Bold, color: UiPalette.Stamp);
            menu.MainSettings = ButtonIn(e, col, "Settings", L("menu.settings"), 30);
            menu.MainControls = ButtonIn(e, col, "Controls", L("controls.title"), 30);
            menu.Quit = ButtonIn(e, col, "Quit", L("menu.quit"), 30);

            // Friends playing right now (Steam): one click to join — a sticky note that grows with the list.
            var friends = R(main, "Friends", TopCenter, TopLeft, new Vector2(80, -150), new Vector2(360, 0));
            friends.localEulerAngles = new Vector3(0, 0, -1f);
            NoteBack(friends, UiPalette.NoteYellow);
            var fl = friends.gameObject.AddComponent<VerticalLayoutGroup>();
            fl.padding = new RectOffset(14, 14, 16, 18);
            fl.spacing = 6;
            fl.childControlWidth = fl.childControlHeight = true;
            fl.childForceExpandWidth = true;
            fl.childForceExpandHeight = false;
            friends.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ColumnText(friends, "Caption", L("friends.caption"), 26, 30, Face.Hand, color: UiPalette.Pen);
            menu.FriendsStatus = ColumnText(friends, "Status", "", 14, 40, Face.Sans, color: UiPalette.InkSoft);
            menu.FriendList = R(friends, "List", Center, Center, Vector2.zero, Vector2.zero);
            var ll = menu.FriendList.gameObject.AddComponent<VerticalLayoutGroup>();
            ll.spacing = 4;
            ll.childControlWidth = ll.childControlHeight = true;
            ll.childForceExpandWidth = true;
            ll.childForceExpandHeight = false;
            menu.FriendRowPrefab = e.FriendRow;
            menu.RefreshFriends = ButtonIn(e, friends, "Refresh", L("friends.refresh"), 28);

            // Pause menu: the game dimmed, a short form on the left.
            var pause = Stretch(root.transform, "Pause");
            menu.PausePage = pause.gameObject;
            Img(pause, UiPalette.Veil).raycastTarget = true;
            var (pauseSheet, _) = Sheet(pause, "Sheet", TopCenter, TopCenter, new Vector2(-430, -110), new Vector2(340, 330));
            Clip(pauseSheet, new Vector2(0, 0));
            var pc = Column(pause, "Column", new Vector2(-430, -134), 290);
            ColumnTitle(pc, L("menu.pause"), 26);
            menu.Role = ColumnText(pc, "Role", "", 15, 26, Face.Sans, TextAnchor.MiddleCenter, UiPalette.InkSoft);
            menu.Invite = ButtonIn(e, pc, "Invite", L("menu.invite"), 34);
            menu.Resume = ButtonIn(e, pc, "Resume", L("menu.resume"), 34);
            menu.Unstuck = ButtonIn(e, pc, "Unstuck", L("menu.unstuck"), 34);
            menu.PauseSettings = ButtonIn(e, pc, "Settings", L("menu.settings"), 34);
            menu.Leave = ButtonIn(e, pc, "Leave", L("menu.leave"), 34);
            BuildControlsPanel(e, menu, root.transform);

            // Settings: two sheets side by side, the game's and the voice chat's with the picture's under it (all within 720 high).
            var settings = Stretch(root.transform, "Settings");
            menu.SettingsPage = settings.gameObject;
            Img(settings, UiPalette.Veil.A(0.9f)).raycastTarget = true;
            Sheet(settings, "SheetGame", TopCenter, TopCenter, new Vector2(-250, -40), new Vector2(500, 570));
            var sc = Column(settings, "Column", new Vector2(-250, -60), 440);
            ColumnTitle(sc, L("menu.settings"), 26);
            Spacer(sc, 8);
            menu.SensitivityLabel = ColumnText(sc, "SensitivityLabel", "", 15, 22);
            menu.Sensitivity = SliderIn(e, sc, "Sensitivity", 0.02f, 0.4f, false);
            Spacer(sc, 6);
            menu.FovLabel = ColumnText(sc, "FovLabel", "", 15, 22);
            menu.Fov = SliderIn(e, sc, "Fov", 60f, 100f, true);
            Spacer(sc, 6);
            menu.VolumeLabel = ColumnText(sc, "VolumeLabel", "", 15, 22);
            menu.Volume = SliderIn(e, sc, "Volume", 0f, 1f, false);
            Spacer(sc, 6);
            menu.MusicLabel = ColumnText(sc, "MusicLabel", "", 15, 22);
            menu.Music = SliderIn(e, sc, "Music", 0f, 1f, false);
            Spacer(sc, 10);
            menu.InvertY = ToggleIn(e, sc, "InvertY", L("menu.invert_y"));
            menu.HeadBob = ToggleIn(e, sc, "HeadBob", L("menu.head_bob"));
            menu.CrouchToggle = ToggleIn(e, sc, "CrouchToggle", L("menu.crouch_toggle"));
            Spacer(sc, 10);
            menu.Language = ButtonIn(e, sc, "Language", "", 30); // the label names the language: set by GameMenu
            Spacer(sc, 14);
            BuildVoiceSettings(e, menu, settings);
            var buttons = R(sc, "Buttons", Center, Center, Vector2.zero, Vector2.zero);
            Fixed(buttons, 32);
            var bh = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            bh.spacing = 10;
            bh.childControlWidth = bh.childControlHeight = true;
            bh.childForceExpandWidth = bh.childForceExpandHeight = true;
            menu.Defaults = ButtonIn(e, buttons, "Defaults", L("menu.defaults"), 32);
            menu.Done = ButtonIn(e, buttons, "Done", L("menu.done"), 32);

            pause.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            BuildStudioSplash(root.transform);
            return root;
        }

        /// <summary>The voice chat sheet of the settings page (GameMenu: mode, microphone, sensitivity with a live meter, noise suppression, volume), and the picture under it (smoothing, VSync, lighting, textures).</summary>
        static void BuildVoiceSettings(UiElements e, GameMenu menu, Transform settings)
        {
            var (sheet, _) = Sheet(settings, "SheetVoice", TopCenter, TopCenter, new Vector2(250, -40), new Vector2(500, 672), UiPalette.CarbonYellow);
            sheet.localEulerAngles = new Vector3(0, 0, -0.5f);
            var vc = Column(settings, "Voice", new Vector2(250, -60), 440);
            ColumnTitle(vc, L("menu.voice_title"), 26);
            Spacer(vc, 8);
            menu.VoiceMode = ButtonIn(e, vc, "VoiceMode", "", 30); // the labels are set by GameMenu
            Spacer(vc, 6);
            menu.VoiceDevice = ButtonIn(e, vc, "VoiceDevice", "", 30);
            Spacer(vc, 8);
            menu.MicSensitivityLabel = ColumnText(vc, "MicSensitivityLabel", "", 15, 22);
            menu.MicSensitivity = SliderIn(e, vc, "MicSensitivity", 0f, 1f, false);
            menu.MicLevelLabel = ColumnText(vc, "MicLevelLabel", "", 15, 22);
            menu.MicLevel = SliderIn(e, vc, "MicLevel", 0f, 1f, false);
            menu.MicLevel.interactable = false;
            Spacer(vc, 8);
            menu.NoiseSuppression = ToggleIn(e, vc, "NoiseSuppression", L("menu.voice_suppress"));
            Spacer(vc, 8);
            menu.VoiceVolumeLabel = ColumnText(vc, "VoiceVolumeLabel", "", 15, 22);
            menu.VoiceVolume = SliderIn(e, vc, "VoiceVolume", 0f, 2f, false);
            // the picture: on this sheet, the game's would not fit the screen
            Spacer(vc, 8);
            ColumnTitle(vc, L("menu.picture_title"), 20);
            Spacer(vc, 4);
            menu.Smoothing = ButtonIn(e, vc, "Smoothing", "", 30); // the label names the mode: set by GameMenu
            menu.VSync = ToggleIn(e, vc, "VSync", L("menu.vsync"));
            menu.Lighting = ButtonIn(e, vc, "Lighting", "", 30); // the labels name the choice: set by GameMenu
            menu.Textures = ButtonIn(e, vc, "Textures", "", 30);
            menu.TextureSize = ButtonIn(e, vc, "TextureSize", "", 30);
        }

        /// <summary>
        /// The studio's splash over everything (its own canvas order): the navy veil, the 1920 x 1080 frame with the name
        /// behind its clip and the wing with its eye (shapes from code, StudioSplash), the sound. The studio's own look,
        /// not the game's paper.
        /// </summary>
        static void BuildStudioSplash(Transform parent)
        {
            var root = Stretch(parent, "StudioSplash");
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var splash = root.gameObject.AddComponent<StudioSplash>();
            splash.Veil = Img(root, new Color(0.165f, 0.196f, 0.294f, 1f));
            splash.Veil.raycastTarget = true; // nothing under it is clicked while it plays
            splash.Group = root.gameObject.AddComponent<CanvasGroup>();
            var content = R(root, "Content", Center, Center, Vector2.zero, new Vector2(1920, 1080));
            splash.Content = content;
            const float s = 2.4f;
            var clip = R(content, "Clip", Center, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0, 240 * s));
            clip.gameObject.AddComponent<RectMask2D>();
            splash.Clip = clip;
            splash.WordGroup = clip.gameObject.AddComponent<CanvasGroup>();
            var word = R(clip, "Word", new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(-3.94f * s, (200 - (164 - 3.94f)) * s), new Vector2(900 * 0.394f * s, 212 * 0.394f * s));
            var image = word.gameObject.AddComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SpritesDir + "/Splash/zg_word.png");
            image.raycastTarget = false;
            var wing = R(content, "Wing", Center, Center, new Vector2(700 * s, 0), Vector2.zero);
            splash.Wing = wing;
            Blob Shape(RectTransform at)
            {
                var b = at.gameObject.AddComponent<Blob>();
                b.raycastTarget = false;
                return b;
            }
            splash.Body = Shape(wing);
            splash.Pupil = Shape(R(wing, "Pupil", Center, Center, Vector2.zero, Vector2.zero));
            splash.Glint = Shape(R(wing, "Glint", Center, Center, Vector2.zero, Vector2.zero));
            var sound = root.gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/UI/zg_splash.wav");
            splash.Sound = sound;
        }

        /// <summary>
        /// Every control of the game in two columns of a form (rows are added by <see cref="ControlsPanel"/> from
        /// Controls.All): right of the pause form in a game, over the desk with "Done" from the main menu.
        /// </summary>
        static void BuildControlsPanel(UiElements e, GameMenu menu, Transform root)
        {
            var backdrop = Stretch(root, "ControlsBackdrop");
            Img(backdrop, UiPalette.Desk.A(0.97f)).raycastTarget = true;
            menu.ControlsBackdrop = backdrop.gameObject;
            backdrop.gameObject.SetActive(false);

            const float colW = 330f, gap = 24f, pad = 20f, height = 610f;
            var rt = R(root, "Controls", TopCenter, TopCenter, new Vector2(170, -54), new Vector2(pad * 2 + colW * 2 + gap, height));
            // (the shadow and the paper are children in that order: the panel's own Image would be drawn under its shadow)
            var shade = Stretch(rt, "Shadow", -16f);
            shade.anchoredPosition = new Vector2(3f, -5f);
            Sliced(Img(shade, new Color(0f, 0f, 0f, 0.55f), PaperSprite("shadow", B(24))));
            Sliced(Img(Stretch(rt, "Paper"), UiPalette.Paper, SheetSprite)).raycastTarget = true;
            var panel = rt.gameObject.AddComponent<ControlsPanel>();
            menu.KeysPanel = panel;
            Clip(rt, new Vector2(0, 0));
            Ink(R(rt, "Title", TopLeft, TopLeft, new Vector2(pad, -10), new Vector2(400, 34)), L("controls.title"), 24, TextAnchor.LowerLeft, Face.Caps);
            RuleLine(rt, TopLeft, TopLeft, new Vector2(pad, -46), colW * 2 + gap, UiPalette.Ink, 2f);
            // The two columns scroll (mouse wheel or the bar on the right) inside the sheet: there are more controls than fit.
            const float viewH = height - 54f - 90f;
            var view = R(rt, "Scroll", TopLeft, TopLeft, new Vector2(pad, -54), new Vector2(colW * 2 + gap + 6, viewH));
            Img(view, new Color(0, 0, 0, 0.01f)).raycastTarget = true; // the wheel scrolls anywhere over the list
            view.gameObject.AddComponent<RectMask2D>();
            var content = R(view, "Content", TopLeft, TopLeft, Vector2.zero, new Vector2(colW * 2 + gap, 0));
            var row2 = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row2.spacing = gap;
            row2.childControlWidth = row2.childControlHeight = true;
            row2.childForceExpandWidth = row2.childForceExpandHeight = false;
            row2.childAlignment = TextAnchor.UpperLeft;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.Columns = new RectTransform[2];
            for (int i = 0; i < 2; i++)
            {
                var c = R(content, $"Column{i}", TopLeft, TopLeft, Vector2.zero, new Vector2(colW, 0));
                c.gameObject.AddComponent<LayoutElement>().preferredWidth = colW;
                var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 2;
                v.childControlWidth = v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
                panel.Columns[i] = c;
            }
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var barRt = R(rt, "Scrollbar", TopRight, TopRight, new Vector2(-6, -54), new Vector2(8, viewH));
            Img(barRt, UiPalette.Rule.A(0.4f)).raycastTarget = true;
            var bar = barRt.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Stretch(Stretch(barRt, "Area"), "Handle");
            bar.handleRect = handle;
            bar.targetGraphic = Img(handle, UiPalette.InkSoft.A(0.7f));
            bar.targetGraphic.raycastTarget = true;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            panel.RowPrefab = e.ControlRow;
            panel.GroupPrefab = e.ControlGroup;
            panel.Hint = Ink(R(rt, "Hint", BottomLeft, BottomLeft, new Vector2(pad, 54), new Vector2(colW * 2 + gap, 24)),
                "", 14, TextAnchor.MiddleLeft, Face.Sans, UiPalette.InkSoft);
            panel.Reset = Instance<Button>(e.Button, rt, "Reset");
            SetText(panel.Reset.GetComponentInChildren<Text>(), L("controls.reset"));
            var rrt = (RectTransform)panel.Reset.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = BottomLeft;
            rrt.anchoredPosition = new Vector2(pad, 14);
            rrt.sizeDelta = new Vector2(240, 32);
            menu.ControlsDone = Instance<Button>(e.Button, rt, "Done");
            SetText(menu.ControlsDone.GetComponentInChildren<Text>(), L("menu.done"));
            var drt = (RectTransform)menu.ControlsDone.transform;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(1, 0);
            drt.anchoredPosition = new Vector2(-pad, 14);
            drt.sizeDelta = new Vector2(160, 32);
            rt.gameObject.SetActive(false);
        }

        static Slider SliderIn(UiElements e, Transform parent, string name, float min, float max, bool whole)
        {
            var s = Instance<Slider>(e.Slider, parent, name);
            s.minValue = min;
            s.maxValue = max;
            s.wholeNumbers = whole;
            Fixed(s, 20);
            return s;
        }

        static Toggle ToggleIn(UiElements e, Transform parent, string name, string label)
        {
            var t = Instance<Toggle>(e.Toggle, parent, name);
            SetText(t.GetComponentInChildren<Text>(), label);
            Fixed(t, 24);
            return t;
        }
    }
}
