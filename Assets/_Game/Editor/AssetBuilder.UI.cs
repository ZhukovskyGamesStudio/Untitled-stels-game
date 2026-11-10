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
    /// Interface prefabs: small element prefabs (UI/Elements) and three screens composed of them
    /// (UI/HUD, UI/Inventory, UI/Menu, UI/StashBoard in the world above the hatch). Screens are uGUI canvases laid out for 1280x720 and scaled with the
    /// window height. The scene holds linked instances of the screens.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string UiPrefabsDir = PrefabsDir + "/UI";
        const string ElementsDir = UiPrefabsDir + "/Elements";

        struct UiPrefabs
        {
            public GameObject[] Screens;
        }

        struct UiElements
        {
            public GameObject Button, Slider, Toggle, InputField;
            public ArcView Arc;
            public Text TeamRow;
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
            public RouteNodeView RouteNode;
            public Image RouteLine;
            public Text DialNumber;
        }

        static Font s_Font;
        static Font UiFont => s_Font != null ? s_Font : s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static readonly Vector2 TopLeft = new(0, 1), TopCenter = new(0.5f, 1), TopRight = new(1, 1), Center = new(0.5f, 0.5f),
            BottomLeft = new(0, 0), BottomCenter = new(0.5f, 0), Left = new(0, 0.5f);

        static readonly Color PanelColor = new(0, 0, 0, 0.45f);

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

        static Text Txt(RectTransform rt, string text, int size, TextAnchor anchor, Color color, bool bold = false, bool shadow = true)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.supportRichText = true;
            t.raycastTarget = false;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.8f);
                s.effectDistance = new Vector2(1, -1);
            }
            return t;
        }

        static GameObject CanvasRoot(string name, int order, bool raycasts)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // laid out for 720p, scaled with the window height
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

        static Text ColumnText(Transform column, string name, string text, int size, Color color, float height, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var t = Txt(R(column, name, Center, Center, Vector2.zero, Vector2.zero), text, size, anchor, color, bold);
            Fixed(t, height);
            return t;
        }

        static void Spacer(Transform column, float height) => Fixed(R(column, "Space", Center, Center, Vector2.zero, Vector2.zero), height);

        static T Instance<T>(GameObject prefab, Transform parent, string name) where T : Component => Nested(prefab, parent, name).GetComponent<T>();

        static Button ButtonIn(UiElements e, Transform parent, string name, string label, float height)
        {
            var b = Instance<Button>(e.Button, parent, name);
            b.GetComponentInChildren<Text>().text = label;
            Fixed(b, height);
            return b;
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
            GameObject Styled(GameObject go)
            {
                foreach (var t in go.GetComponentsInChildren<Text>(true)) t.font = UiFont;
                return go;
            }

            var e = new UiElements();
            e.Button = Prefab(ElementsDir + "/Button.prefab", () =>
            {
                var go = Styled(DefaultControls.CreateButton(res));
                go.name = "Button";
                ((RectTransform)go.transform).sizeDelta = new Vector2(320, 36);
                go.GetComponent<Image>().color = Color.white;
                var b = go.GetComponent<Button>();
                go.AddComponent<UiSound>().Control = b;
                var colors = b.colors;
                colors.normalColor = new Color(0.2f, 0.22f, 0.26f, 0.95f);
                colors.highlightedColor = new Color(0.32f, 0.35f, 0.41f, 1f);
                colors.pressedColor = new Color(0.14f, 0.15f, 0.18f, 1f);
                colors.selectedColor = colors.normalColor;
                colors.disabledColor = new Color(0.2f, 0.22f, 0.26f, 0.4f);
                b.colors = colors;
                var label = go.GetComponentInChildren<Text>();
                label.color = Color.white;
                label.fontSize = 15;
                return go;
            });
            e.Slider = Prefab(ElementsDir + "/Slider.prefab", () =>
            {
                var go = Styled(DefaultControls.CreateSlider(res));
                go.name = "Slider";
                go.AddComponent<UiSound>().Control = go.GetComponent<Slider>();
                return go;
            });
            e.Toggle = Prefab(ElementsDir + "/Toggle.prefab", () =>
            {
                var go = Styled(DefaultControls.CreateToggle(res));
                go.name = "Toggle";
                go.AddComponent<UiSound>().Control = go.GetComponent<Toggle>();
                var label = go.GetComponentInChildren<Text>();
                label.color = Color.white;
                label.fontSize = 14;
                return go;
            });
            e.InputField = Prefab(ElementsDir + "/InputField.prefab", () =>
            {
                var go = Styled(DefaultControls.CreateInputField(res));
                go.name = "InputField";
                foreach (var t in go.GetComponentsInChildren<Text>(true)) t.fontSize = 15;
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
                // Pivot at the bottom centre: the bar sits right over the robot, the icon above the bar.
                var root = R(null, "SuspicionMarker", BottomLeft, BottomCenter, Vector2.zero, new Vector2(84, 44));
                var view = root.gameObject.AddComponent<SuspicionMarkerView>();
                view.Root = root;
                view.Bar = Bar(root, "Bar", BottomCenter, BottomCenter, Vector2.zero, new Vector2(80, 10), new Color(0, 0, 0, 0.6f), new Color(1f, 0.92f, 0.35f), 2f);
                view.Icon = Txt(R(root, "Icon", BottomCenter, BottomCenter, new Vector2(0, 12), new Vector2(40, 32)), "?", 26, TextAnchor.LowerCenter, Color.yellow, true);
                return root.gameObject;
            }).GetComponent<SuspicionMarkerView>();

            e.FriendRow = Prefab(ElementsDir + "/FriendLobbyRow.prefab", () =>
            {
                var root = R(null, "FriendLobbyRow", TopLeft, TopLeft, Vector2.zero, new Vector2(330, 34));
                Img(root, new Color(1, 1, 1, 0.06f));
                Fixed(root.GetComponent<Image>(), 34);
                var view = root.gameObject.AddComponent<FriendLobbyRowView>();
                view.Label = Txt(R(root, "Label", Left, Left, new Vector2(10, 0), new Vector2(200, 30)), "Друг · 1/4", 14, TextAnchor.MiddleLeft, Color.white);
                view.Join = Instance<Button>(e.Button, root, "Join");
                view.Join.GetComponentInChildren<Text>().text = "Войти";
                var jrt = (RectTransform)view.Join.transform;
                jrt.anchorMin = jrt.anchorMax = jrt.pivot = new Vector2(1, 0.5f);
                jrt.anchoredPosition = new Vector2(-4, 0);
                jrt.sizeDelta = new Vector2(100, 28);
                return root.gameObject;
            }).GetComponent<FriendLobbyRowView>();

            e.TeamRow = Prefab(ElementsDir + "/TeamRow.prefab", () =>
            {
                var t = Txt(R(null, "TeamRow", TopRight, TopRight, Vector2.zero, new Vector2(244, 18)), "Игрок  ■■", 12, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.8f));
                Fixed(t, 18);
                return t.gameObject;
            }).GetComponent<Text>();

            e.TeamMarker = Prefab(ElementsDir + "/TeamMarker.prefab", () =>
            {
                var root = R(null, "TeamMarker", BottomLeft, Center, Vector2.zero, Vector2.zero);
                var view = root.gameObject.AddComponent<TeamMarkerView>();
                view.Rect = root;
                var cross = R(root, "Cross", Center, Center, Vector2.zero, new Vector2(20, 20));
                view.Cross = cross.gameObject;
                view.CrossBars = new[]
                {
                    Img(R(cross, "Vertical", Center, Center, Vector2.zero, new Vector2(6, 20)), Color.red),
                    Img(R(cross, "Horizontal", Center, Center, Vector2.zero, new Vector2(20, 6)), Color.red),
                };
                view.Label = Txt(R(root, "Label", Center, Center, new Vector2(0, -21), new Vector2(160, 18)), "Игрок", 12, TextAnchor.MiddleCenter, Color.white);
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
                Img(R(null, "CellHighlight", TopLeft, TopLeft, Vector2.zero, new Vector2(InventoryUI.Cell - 2, InventoryUI.Cell - 2)), new Color(0.3f, 1f, 0.4f, 0.35f)).gameObject)
                .GetComponent<Image>();

            e.ControlRow = Prefab(ElementsDir + "/ControlRow.prefab", () =>
            {
                var root = R(null, "ControlRow", TopLeft, TopLeft, Vector2.zero, new Vector2(330, 24));
                Fixed(root, 24);
                var view = root.gameObject.AddComponent<ControlRowView>();
                view.Title = Txt(R(root, "Title", Left, Left, new Vector2(2, 0), new Vector2(220, 24)), "Действие", 13, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.9f));
                view.Title.horizontalOverflow = HorizontalWrapMode.Wrap;
                view.Title.resizeTextForBestFit = true;
                view.Title.resizeTextMinSize = 10;
                view.Title.resizeTextMaxSize = 13;
                view.Key = Instance<Button>(e.Button, root, "Key");
                var krt = (RectTransform)view.Key.transform;
                krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(1, 0.5f);
                krt.anchoredPosition = Vector2.zero;
                krt.sizeDelta = new Vector2(100, 22);
                view.KeyLabel = view.Key.GetComponentInChildren<Text>();
                view.KeyLabel.text = "E";
                view.KeyLabel.fontSize = 13;
                view.KeyLabel.fontStyle = FontStyle.Bold;
                view.KeyBackground = view.Key.GetComponent<Image>();
                // The key colour is set by the panel (normal / waiting / taken twice): the button only tints it.
                var colors = view.Key.colors;
                colors.normalColor = colors.selectedColor = Color.white;
                colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
                colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
                colors.colorMultiplier = 1.5f;
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
                view.Arrow = Img(R(root, "Arrow", Center, Center, Vector2.zero, new Vector2(14, 14)), Color.white, S("MapArrow"));
                view.Dot = Img(R(root, "Dot", Center, Center, Vector2.zero, new Vector2(9, 9)), Color.white, S("Disc"));
                var diamond = R(root, "Diamond", Center, Center, Vector2.zero, new Vector2(9, 9));
                diamond.localEulerAngles = new Vector3(0, 0, 45);
                view.Diamond = Img(diamond, Color.white);
                view.Label = Txt(R(root, "Label", Center, Center, new Vector2(0, -14), new Vector2(160, 16)), "Игрок", 11, TextAnchor.MiddleCenter, Color.white);
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
                Img(R(diamond, "Hole", Center, Center, Vector2.zero, new Vector2(5, 5)), new Color(0, 0, 0, 0.55f));
                view.Ring = Img(R(root, "Ring", Center, Center, Vector2.zero, new Vector2(30, 30)), Color.white, S("Ring"));
                view.Label = Txt(R(root, "Label", Center, Center, new Vector2(0, 17), new Vector2(220, 18)), "12 м", 12, TextAnchor.MiddleCenter, Color.white, true);
                return root.gameObject;
            }).GetComponent<CalloutMarkerView>();

            e.RouteNode = Prefab(ElementsDir + "/RouteNode.prefab", () =>
            {
                var root = R(null, "RouteNode", BottomLeft, Center, Vector2.zero, new Vector2(46, 46));
                var view = root.gameObject.AddComponent<RouteNodeView>();
                view.Rect = root;
                view.Ring = Img(root, Color.white, S("Disc"));
                view.Ring.raycastTarget = true;
                view.Fill = Img(R(root, "Fill", Center, Center, Vector2.zero, new Vector2(34, 34)), new Color(0, 0, 0, 0.9f), S("Disc"));
                view.Button = root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = view.Ring;
                root.gameObject.AddComponent<UiSound>().Control = view.Button;
                view.Label = Txt(R(root, "Label", Center, TopCenter, new Vector2(0, -26), new Vector2(190, 64)), "Офис", 12, TextAnchor.UpperCenter, Color.white, true);
                view.Votes = Txt(R(root, "Votes", Center, Center, Vector2.zero, new Vector2(60, 30)), "●●", 18, TextAnchor.MiddleCenter, Color.white, true);
                return root.gameObject;
            }).GetComponent<RouteNodeView>();

            e.RouteLine = Prefab(ElementsDir + "/RouteLine.prefab", () =>
                Img(R(null, "RouteLine", BottomLeft, Left, Vector2.zero, new Vector2(10, 3)), Color.white).gameObject).GetComponent<Image>();

            e.DialNumber = Prefab(ElementsDir + "/DialNumber.prefab", () =>
                Txt(R(null, "DialNumber", Center, Center, Vector2.zero, new Vector2(40, 22)), "0", 15, TextAnchor.MiddleCenter, Color.white, true).gameObject)
                .GetComponent<Text>();

            e.ControlGroup = Prefab(ElementsDir + "/ControlGroup.prefab", () =>
            {
                var t = Txt(R(null, "ControlGroup", TopLeft, TopLeft, Vector2.zero, new Vector2(330, 26)), "Группа", 15, TextAnchor.LowerLeft, new Color(1f, 0.85f, 0.45f), true);
                Fixed(t, 28);
                return t.gameObject;
            }).GetComponent<Text>();
            return e;
        }

        // ------------------------------------------------------------------ HUD

        static GameObject BuildHud(UiElements e)
        {
            var root = CanvasRoot("HUD", 0, false);
            var hud = root.AddComponent<GameHUD>();
            hud.Canvas = (RectTransform)root.transform;
            var content = Stretch(root.transform, "Content");
            hud.Content = content.gameObject;
            var white = new Color(1, 1, 1, 0.8f);

            // Overlays (full screen, hidden until needed).
            var overlays = Stretch(content, "Overlays");
            hud.LowHpVignette = Img(Stretch(overlays, "LowHpVignette"), new Color(0.8f, 0, 0, 0.35f), S("Vignette"));
            hud.HitFlash = Img(Stretch(overlays, "HitFlash"), new Color(1, 0, 0, 0.2f));
            hud.GasVignette = Img(Stretch(overlays, "GasVignette"), new Color(0.4f, 0.95f, 0.3f, 0.5f), S("Vignette"));
            hud.GasTint = Img(Stretch(overlays, "GasTint"), new Color(0.5f, 0.9f, 0.3f, 0.1f));
            hud.LaughText = Txt(R(overlays, "Laugh", TopCenter, TopCenter, new Vector2(0, -216), new Vector2(400, 50)), "ХА-ХА-ХА-ХА", 30, TextAnchor.MiddleCenter, new Color(0.8f, 1f, 0.6f, 0.9f), true);
            hud.GasText = Txt(R(overlays, "GasTimer", TopCenter, TopCenter, new Vector2(0, -260), new Vector2(400, 24)), "Весёлый газ", 15, TextAnchor.MiddleCenter, Color.white);
            hud.DownedTint = Img(Stretch(overlays, "DownedTint"), new Color(0.25f, 0f, 0f, 0.55f));
            hud.DownedText = Txt(R(overlays, "Downed", TopCenter, TopCenter, new Vector2(0, -240), new Vector2(900, 100)),
                "Ты выведен из строя\nСоюзник может поднять тебя (удерживать E рядом)", 22, TextAnchor.MiddleCenter, Color.white, true);
            // In the hatch: almost dark, a ring of light from the opening above.
            hud.HatchTint = Img(Stretch(overlays, "HatchTint"), new Color(0f, 0f, 0f, 0.82f));
            Img(Stretch(hud.HatchTint.rectTransform, "Edge"), new Color(0f, 0f, 0f, 1f), S("Vignette"));
            hud.HatchText = Txt(R(overlays, "HatchText", Center, Center, new Vector2(0, 40), new Vector2(900, 100)),
                "Ты в люке", 22, TextAnchor.MiddleCenter, Color.white, true);
            hud.HatchStatus = Txt(R(overlays, "HatchStatus", TopCenter, TopCenter, new Vector2(0, -160), new Vector2(900, 24)),
                "В люке ждут", 15, TextAnchor.MiddleCenter, new Color(0.6f, 1f, 0.65f), true);
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
            hud.AlarmLabel = Txt(Stretch(tag, "Label"), "ТРЕВОГА · 6 c", 14, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.8f), true);

            // Around the crosshair.
            hud.ArcLayer = R(content, "Arcs", Center, Center, Vector2.zero, Vector2.zero);
            hud.ArcPrefab = e.Arc;
            hud.HitArc = Instance<ArcView>(e.Arc.gameObject, hud.ArcLayer, "HitArc");
            var dot = R(content, "CrosshairDot", Center, Center, Vector2.zero, new Vector2(4, 4));
            Img(dot, white);
            hud.CrosshairDot = dot.gameObject;
            var brackets = R(content, "CrosshairBrackets", Center, Center, Vector2.zero, new Vector2(22, 22));
            hud.CrosshairBrackets = brackets.gameObject;
            const float r = 11f, l = 6f, t = 2f;
            foreach (var (sx, sy) in new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) })
            {
                var corner = new Vector2(sx * r, sy * r);
                var piv = new Vector2(sx < 0 ? 0 : 1, sy > 0 ? 1 : 0);
                Img(R(brackets, "H", Center, piv, corner, new Vector2(l, t)), new Color(1, 1, 1, 0.95f));
                Img(R(brackets, "V", Center, piv, corner, new Vector2(t, l)), new Color(1, 1, 1, 0.95f));
            }
            hud.Stamina = Bar(content, "Stamina", Center, TopCenter, new Vector2(0, -34), new Vector2(120, 6), new Color(0, 0, 0, 0.45f), white, 0f);
            hud.Prompt = BuildPrompt(content);

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
            hud.HatchLabel = Txt(R(hud.HatchMarker, "Label", Center, Center, new Vector2(0, -19), new Vector2(120, 18)), "люк · 12 м", 12, TextAnchor.MiddleCenter, new Color(0.45f, 1f, 0.5f));

            // How visible you are (light on you), above the noise meter.
            var lightRow = R(content, "Visibility", BottomCenter, BottomLeft, new Vector2(-30, 156), new Vector2(200, 18));
            hud.LightBar = Bar(lightRow, "Bar", BottomLeft, BottomLeft, new Vector2(0, 5), new Vector2(46, 7), new Color(0, 0, 0, 0.55f), Color.white, 1f);
            hud.LightLabel = Txt(R(lightRow, "Label", BottomLeft, BottomLeft, new Vector2(54, -1), new Vector2(150, 18)), "в тени", 12, TextAnchor.MiddleLeft, Color.white, true);

            // Noise meter above the inventory.
            var noise = R(content, "Noise", BottomCenter, BottomLeft, new Vector2(-30, 134), new Vector2(160, 18));
            hud.NoiseGroup = noise.gameObject.AddComponent<CanvasGroup>();
            hud.NoiseGroup.blocksRaycasts = false;
            hud.NoiseBars = Enumerable.Range(0, 3)
                .Select(i => Img(R(noise, $"Bar{i}", BottomLeft, BottomLeft, new Vector2(i * 9f, 0), new Vector2(6, 6 + i * 5)), Color.white)).ToArray();
            hud.NoiseLabel = Txt(R(noise, "Label", BottomLeft, BottomLeft, new Vector2(32, -2), new Vector2(120, 18)), "слышно", 12, TextAnchor.MiddleLeft, Color.white);

            // Quota panel, top left.
            var quota = R(content, "Quota", TopLeft, TopLeft, new Vector2(16, -16), new Vector2(232, 50));
            Img(quota, PanelColor);
            hud.HouseLabel = Txt(R(quota, "House", TopLeft, TopLeft, new Vector2(10, -2), new Vector2(150, 22)), "Дом 1 · этаж 1", 14, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.75f), true);
            hud.QuotaLabel = Txt(R(quota, "Count", TopLeft, TopLeft, new Vector2(10, -2), new Vector2(212, 22)), "0 / 100", 14, TextAnchor.MiddleRight, Color.white, true);
            hud.QuotaBar = Bar(quota, "Bar", TopLeft, TopLeft, new Vector2(10, -28), new Vector2(212, 10), new Color(0, 0, 0, 0.5f), new Color(1f, 0.8f, 0.2f), 2f);
            hud.GainRect = R(quota, "Gain", TopLeft, Left, new Vector2(240, -23), new Vector2(120, 26));
            hud.GainLabel = Txt(hud.GainRect, "+20", 18, TextAnchor.MiddleLeft, new Color(0.45f, 1f, 0.5f), true);
            hud.QuotaHint = Txt(R(quota, "Hint", TopLeft, TopLeft, new Vector2(10, -52), new Vector2(400, 20)), "Квота набрана — к люку, F: следующий дом", 12, TextAnchor.MiddleLeft, new Color(0.6f, 1f, 0.65f, 0.9f));

            // Health, top right, and the team list under it.
            var hp = R(content, "Health", TopRight, TopRight, new Vector2(-16, -16), new Vector2(180, 40));
            Img(hp, new Color(0, 0, 0, 0.55f));
            Txt(R(hp, "Caption", TopLeft, TopLeft, new Vector2(12, 0), new Vector2(60, 40)), "ХП", 15, TextAnchor.MiddleLeft, Color.white, true);
            hud.HpPips = Enumerable.Range(0, Stealth.Player.HealthState.MaxHp)
                .Select(i => Img(R(hp, $"Pip{i}", TopLeft, TopLeft, new Vector2(56 + i * 54, -10), new Vector2(46, 20)), new Color(0.9f, 0.2f, 0.2f))).ToArray();
            hud.TeamList = R(content, "Team", TopRight, TopRight, new Vector2(-16, -62), new Vector2(244, 0));
            var team = hud.TeamList.gameObject.AddComponent<VerticalLayoutGroup>();
            team.childControlHeight = true;
            team.childControlWidth = true;
            team.childForceExpandHeight = false;
            team.childAlignment = TextAnchor.UpperRight;
            hud.TeamRowPrefab = e.TeamRow;

            // Banner, run over, help.
            hud.BannerRect = R(content, "Banner", TopCenter, TopCenter, new Vector2(0, -115), new Vector2(640, 40));
            hud.Banner = hud.BannerRect.gameObject.AddComponent<CanvasGroup>();
            hud.Banner.blocksRaycasts = false;
            Img(hud.BannerRect, new Color(0, 0, 0, 0.5f));
            hud.BannerLabel = Txt(Stretch(hud.BannerRect, "Label"), "Сообщение", 16, TextAnchor.MiddleCenter, Color.white, true);

            var over = R(content, "RunOver", TopCenter, TopCenter, new Vector2(0, -274), new Vector2(520, 120));
            Img(over, new Color(0, 0, 0, 0.8f));
            Txt(R(over, "Title", TopCenter, TopCenter, new Vector2(0, -14), new Vector2(520, 40)), "Забег окончен", 26, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.35f), true);
            hud.RunOverTotal = Txt(R(over, "Total", TopCenter, TopCenter, new Vector2(0, -56), new Vector2(520, 24)), "Сдано за забег: 0 очк.", 16, TextAnchor.MiddleCenter, Color.white);
            hud.RunOverRestart = Txt(R(over, "Restart", TopCenter, TopCenter, new Vector2(0, -84), new Vector2(520, 24)), "R — начать новый забег", 15, TextAnchor.MiddleCenter, Color.white);
            hud.RunOver = over.gameObject;

            // The help text names the current keys (GameHUD rebuilds it after a rebind).
            hud.HelpLabel = Txt(R(content, "Help", BottomLeft, BottomLeft, new Vector2(16, 12), new Vector2(420, 18)),
                "Esc — меню и всё управление · H — скрыть", 12, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.6f));
            hud.Help = hud.HelpLabel.gameObject;

            // The minimap (bottom right, turns with the view) and the big map (M, the whole floor, north-up).
            hud.Minimap = BuildMap(e, content, "Minimap", true, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), new Vector2(210, 210), 4.5f);
            hud.BigMap = BuildMap(e, content, "BigMap", false, Center, Center, new Vector2(0, 10), new Vector2(760, 600), 1f);
            hud.BigMap.gameObject.SetActive(false);
            BuildSafeCrack(e, content);
            return root;
        }

        /// <summary>The safe minigame: a dial with numbers, a fixed pointer, the stethoscope bar, the digits found.</summary>
        static void BuildSafeCrack(UiElements e, Transform content)
        {
            // The view stays active and shows / hides its panel (a component that hides its own object stops updating).
            var holder = R(content, "SafeCrack", Center, Center, Vector2.zero, Vector2.zero);
            var view = holder.gameObject.AddComponent<SafeCrackView>();
            var root = R(holder, "Panel", Center, Center, new Vector2(0, 10), new Vector2(420, 420));
            view.Root = root.gameObject;
            Img(root, new Color(0.02f, 0.03f, 0.04f, 0.78f));
            Txt(R(root, "Title", TopCenter, TopCenter, new Vector2(0, -8), new Vector2(400, 30)), "Сейф", 20, TextAnchor.MiddleCenter, Color.white, true);
            var face = R(root, "Face", Center, Center, new Vector2(0, 22), new Vector2(250, 250));
            Img(face, new Color(0.16f, 0.17f, 0.19f, 1f), S("Disc"));
            Img(R(face, "Rim", Center, Center, Vector2.zero, new Vector2(250, 250)), new Color(0.7f, 0.72f, 0.75f, 0.9f), S("Ring"));
            view.Dial = R(face, "Dial", Center, Center, Vector2.zero, Vector2.zero);
            view.NumberPrefab = e.DialNumber;
            view.NumberRadius = 98f;
            var pointer = R(face, "Pointer", Center, Center, new Vector2(0, 136), new Vector2(18, 18));
            pointer.localEulerAngles = new Vector3(0, 0, 180);
            Img(pointer, new Color(1f, 0.8f, 0.25f), S("MapArrow"));
            view.Current = Txt(R(face, "Current", Center, Center, Vector2.zero, new Vector2(90, 50)), "00", 34, TextAnchor.MiddleCenter, Color.white, true);
            view.Flash = Img(Stretch(root, "Flash"), new Color(1f, 0.2f, 0.15f, 0.3f));
            view.Listen = Bar(root, "Listen", BottomCenter, BottomCenter, new Vector2(-40, 92), new Vector2(220, 12), new Color(0, 0, 0, 0.6f), Color.white, 2f);
            view.ListenLabel = Txt(R(root, "ListenLabel", BottomCenter, BottomLeft, new Vector2(78, 84), new Vector2(140, 26)), "тихо", 14, TextAnchor.MiddleLeft, Color.white, true);
            view.Digits = Txt(R(root, "Digits", BottomCenter, BottomCenter, new Vector2(0, 58), new Vector2(400, 26)), "Код: ○ ○ ○", 17, TextAnchor.MiddleCenter, Color.white, true);
            view.Hint = Txt(R(root, "Hint", BottomCenter, BottomCenter, new Vector2(0, 8), new Vector2(410, 44)), "Мышь — крутить диск", 12, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.75f));
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The route map in the sewer (inside the menu canvas: it needs the mouse). Columns of houses, lines between
        /// them, the houses the team can vote for.
        /// </summary>
        static void BuildRoute(UiElements e, Transform root)
        {
            var screen = R(root, "Route", Center, Center, Vector2.zero, Vector2.zero).gameObject.AddComponent<RouteScreen>();
            var panel = R(screen.transform, "Panel", Center, Center, new Vector2(0, 10), new Vector2(1040, 600));
            Img(panel, new Color(0.03f, 0.04f, 0.04f, 0.93f)).raycastTarget = true;
            screen.Panel = panel.gameObject;
            screen.Title = Txt(R(panel, "Title", TopLeft, TopLeft, new Vector2(20, -8), new Vector2(1000, 36)), "Канализация", 22, TextAnchor.MiddleLeft, Color.white, true);
            screen.Status = Txt(R(panel, "Status", TopLeft, TopLeft, new Vector2(20, -46), new Vector2(1000, 44)), "Квота", 14, TextAnchor.UpperLeft, Color.white);
            screen.Graph = R(panel, "Graph", BottomLeft, BottomLeft, new Vector2(20, 44), new Vector2(1000, 450));
            screen.LineLayer = R(screen.Graph, "Lines", BottomLeft, BottomLeft, Vector2.zero, Vector2.zero);
            screen.NodePrefab = e.RouteNode;
            screen.LinePrefab = e.RouteLine;
            screen.Hint = Txt(R(panel, "Hint", BottomLeft, BottomLeft, new Vector2(20, 10), new Vector2(1000, 26)), "Клик по дому — голос", 13, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.7f));
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// A map: a panel with a clipped viewport; inside, a pivot (turns with the view on the minimap) and the content in
        /// metres with a layer of rectangles and a layer of icons. Its own canvas: redrawing the map does not rebuild the HUD.
        /// </summary>
        static MapView BuildMap(UiElements e, Transform parent, string name, bool follow, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float scale)
        {
            var root = R(parent, name, anchor, pivot, pos, size);
            root.gameObject.AddComponent<Canvas>();
            Img(root, new Color(0.02f, 0.03f, 0.04f, follow ? 0.62f : 0.85f));
            var view = root.gameObject.AddComponent<MapView>();
            view.FollowPlayer = follow;
            view.Scale = scale;
            float captionH = follow ? 0f : 30f;
            var viewport = Stretch(root, "Viewport", 4f);
            viewport.offsetMax = new Vector2(-4f, -4f - captionH);
            viewport.gameObject.AddComponent<RectMask2D>();
            view.Viewport = viewport;
            view.Pivot = R(viewport, "Pivot", Center, Center, Vector2.zero, Vector2.zero);
            view.Content = R(view.Pivot, "Content", Center, Center, Vector2.zero, Vector2.zero);
            view.RectLayer = R(view.Content, "Rects", Center, Center, Vector2.zero, Vector2.zero);
            view.IconLayer = R(view.Content, "Icons", Center, Center, Vector2.zero, Vector2.zero);
            view.RectPrefab = e.MapRect;
            view.IconPrefab = e.MapIcon;
            view.Caption = follow
                ? Txt(R(root, "Caption", TopLeft, TopLeft, new Vector2(6, -2), new Vector2(120, 18)), "этаж 1", 11, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.75f))
                : Txt(R(root, "Caption", TopLeft, TopLeft, new Vector2(12, -2), new Vector2(size.x - 24, 30)), "Этаж 1", 15, TextAnchor.MiddleLeft, Color.white, true);
            return view;
        }

        /// <summary>Background + fill (pivot left, stretched by anchorMax.x).</summary>
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

        /// <summary>The pill under the crosshair: auto-sized by layout groups to the key and the text.</summary>
        static PromptView BuildPrompt(Transform parent)
        {
            var pill = R(parent, "Prompt", Center, TopCenter, new Vector2(0, -52), new Vector2(200, 30));
            Img(pill, new Color(0, 0, 0, 0.5f));
            var h = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 8, 4, 4);
            h.spacing = 8;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var fit = pill.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var keyBox = R(pill, "Key", Center, Center, Vector2.zero, new Vector2(26, 22));
            Img(keyBox, new Color(1, 1, 1, 0.9f));
            var kh = keyBox.gameObject.AddComponent<HorizontalLayoutGroup>();
            kh.padding = new RectOffset(7, 7, 2, 2);
            kh.childAlignment = TextAnchor.MiddleCenter;
            kh.childControlWidth = kh.childControlHeight = true;
            var kle = keyBox.gameObject.AddComponent<LayoutElement>();
            kle.minWidth = 26;
            kle.minHeight = kle.preferredHeight = 22;
            var key = Txt(R(keyBox, "Label", Center, Center, Vector2.zero, Vector2.zero), "E", 13, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f), true, shadow: false);

            var label = Txt(R(pill, "Text", Center, Center, Vector2.zero, Vector2.zero), "Подсказка", 15, TextAnchor.MiddleLeft, Color.white);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var progress = Bar(pill, "Progress", BottomCenter, TopCenter, new Vector2(0, -2), new Vector2(0, 6), new Color(0, 0, 0, 0.5f), Color.white, 2f);
            var prt = (RectTransform)progress.transform;
            prt.anchorMin = new Vector2(0, 0);
            prt.anchorMax = new Vector2(1, 0);
            prt.sizeDelta = new Vector2(0, 6);
            progress.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var view = pill.gameObject.AddComponent<PromptView>();
            view.KeyBox = keyBox.gameObject;
            view.Key = key;
            view.Label = label;
            view.Progress = progress;
            return view;
        }

        // ------------------------------------------------------------------ inventory

        static GameObject BuildInventoryScreen(UiElements e)
        {
            var root = CanvasRoot("Inventory", 5, false);
            var ui = root.AddComponent<InventoryUI>();
            ui.Canvas = (RectTransform)root.transform;
            const float c = InventoryUI.Cell;
            float gw = InventoryLayout.Width * c, gh = InventoryLayout.Height * c;

            ui.Panel = R(root.transform, "Panel", BottomCenter, BottomCenter, new Vector2(0, 13), new Vector2(gw + 10, gh + 9 + 22));
            ui.GridBackground = Img(R(ui.Panel, "GridBackground", BottomCenter, BottomCenter, Vector2.zero, new Vector2(gw + 10, gh + 9)), new Color(0, 0, 0, 0.6f));
            var header = R(ui.Panel, "Header", BottomCenter, BottomCenter, new Vector2(0, gh + 9), new Vector2(gw + 10, 22));
            ui.Header = header.gameObject;
            ui.HeaderBackground = Img(header, new Color(0, 0, 0, 0.6f));
            ui.ValueLabel = Txt(R(header, "Value", Left, Left, new Vector2(5, 0), new Vector2(120, 20)), "0 очк.", 13, TextAnchor.MiddleLeft, Color.white);
            ui.RotateHint = Txt(R(header, "RotateHint", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-5, 0), new Vector2(120, 20)),
                "R — повернуть", 12, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.6f)).gameObject;

            ui.Grid = R(ui.Panel, "Grid", BottomCenter, TopCenter, new Vector2(0, 5 + gh), new Vector2(gw, gh));
            ui.Grid.pivot = TopLeft;
            ui.Grid.anchoredPosition = new Vector2(-gw / 2f, 5 + gh);
            var cells = new List<Image>();
            for (int y = 0; y < InventoryLayout.Height; y++)
                for (int x = 0; x < InventoryLayout.Width; x++)
                    if (InventoryLayout.IsCell(x, y))
                        cells.Add(Img(R(ui.Grid, $"Cell_{x}_{y}", TopLeft, TopLeft, new Vector2(x * c + 1, -(y * c + 1)), new Vector2(c - 2, c - 2)), new Color(1, 1, 1, 0.09f)));
            ui.Cells = cells.ToArray();
            ui.ItemLayer = Stretch(ui.Grid, "Items");
            ui.ItemLayer.pivot = TopLeft;
            ui.HighlightLayer = Stretch(ui.Grid, "Highlights");
            ui.HighlightLayer.pivot = TopLeft;

            ui.Ghost = R(root.transform, "DragGhost", BottomLeft, TopLeft, Vector2.zero, Vector2.zero);
            ui.BlockPrefab = e.InventoryBlock;
            ui.HighlightPrefab = e.CellHighlight;
            ui.Tooltip = Txt(R(root.transform, "Tooltip", BottomLeft, TopLeft, Vector2.zero, new Vector2(320, 22)), "Подсказка", 13, TextAnchor.MiddleLeft, Color.white);
            return root;
        }

        /// <summary>
        /// Team stash above the hatch: a world-space canvas with the stash grid and a "sell" area. Same units as the
        /// inventory (<see cref="InventoryUI.Cell"/> per cell), scaled down to metres.
        /// </summary>
        static GameObject BuildStashBoard(UiElements e)
        {
            const float c = InventoryUI.Cell, pad = 8f, gap = 12f, header = 26f, sellW = 96f;
            float gw = TeamStash.Width * c, gh = TeamStash.Height * c;
            var go = new GameObject("StashBoard", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(pad + gw + gap + sellW + pad, header + gh + pad * 2f);
            rt.localScale = Vector3.one * 0.0065f; // ~2.5 m wide
            var board = go.AddComponent<StashBoard>();
            board.Canvas = canvas;
            board.Root = rt;

            Img(Stretch(go.transform, "Background"), new Color(0.02f, 0.03f, 0.05f, 0.7f));
            board.ValueLabel = Txt(R(go.transform, "Title", TopLeft, TopLeft, new Vector2(pad, -2f), new Vector2(gw, header)),
                "Склад команды · 0 очк.", 16, TextAnchor.MiddleLeft, Color.white, bold: true);
            board.Grid = R(go.transform, "Grid", TopLeft, TopLeft, new Vector2(pad, -(header + pad)), new Vector2(gw, gh));
            var cells = new List<Image>();
            for (int y = 0; y < TeamStash.Height; y++)
                for (int x = 0; x < TeamStash.Width; x++)
                    cells.Add(Img(R(board.Grid, $"Cell_{x}_{y}", TopLeft, TopLeft, new Vector2(x * c + 1, -(y * c + 1)), new Vector2(c - 2, c - 2)), new Color(1, 1, 1, 0.12f)));
            board.Cells = cells.ToArray();
            board.ItemLayer = Stretch(board.Grid, "Items");
            board.ItemLayer.pivot = TopLeft;
            board.HighlightLayer = Stretch(board.Grid, "Highlights");
            board.HighlightLayer.pivot = TopLeft;

            board.SellZone = R(go.transform, "Sell", TopLeft, TopLeft, new Vector2(pad + gw + gap, -(header + pad)), new Vector2(sellW, gh));
            board.SellBackground = Img(board.SellZone, new Color(1f, 0.75f, 0.1f, 0.45f));
            Txt(Stretch(board.SellZone, "Label", 4f), "ПРОДАТЬ\nна квоту", 16, TextAnchor.MiddleCenter, Color.white, bold: true);
            return go;
        }

        // ------------------------------------------------------------------ menu

        static GameObject BuildMenu(UiElements e)
        {
            var root = CanvasRoot("Menu", 100, true);
            var menu = root.AddComponent<GameMenu>();
            var dim = new Color(1, 1, 1, 0.6f);

            // Main menu.
            var main = Stretch(root.transform, "Main");
            menu.MainPage = main.gameObject;
            var bg = Img(main, new Color(0.06f, 0.07f, 0.08f, 1f));
            bg.raycastTarget = true;
            var col = Column(main, "Column", new Vector2(-190, -100), 440); // with the friends panel the pair is centred
            ColumnText(col, "Title", "АЙТИШНИКИ: ВЫЛАЗКА", 30, Color.white, 50, true, TextAnchor.MiddleCenter);
            ColumnText(col, "Subtitle", "прототип кор-лупа", 14, dim, 24, anchor: TextAnchor.MiddleCenter);
            Spacer(col, 20);
            menu.SteamStatus = ColumnText(col, "SteamStatus", "Steam: …", 14, Color.white, 22);
            menu.HostSteam = ButtonIn(e, col, "HostSteam", "Создать игру через Steam", 44);
            ColumnText(col, "SteamHint", "Друзья заходят через приглашение или «Присоединиться к игре» в Steam.", 13, dim, 36);
            Spacer(col, 14);
            menu.LocalCaption = ColumnText(col, "LocalCaption", "Локальная сеть / тест на одном ПК (UDP, порт 7777):", 14, Color.white, 22);
            menu.HostLocal = ButtonIn(e, col, "HostLocal", "Создать локальную игру", 36);
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
            menu.Join = ButtonIn(e, row, "Join", "Подключиться по IP", 34);
            menu.Join.GetComponent<LayoutElement>().preferredWidth = 200;
            Spacer(col, 14);
            menu.NetStatus = ColumnText(col, "NetStatus", "", 14, new Color(1f, 0.85f, 0.5f), 22);
            menu.MainSettings = ButtonIn(e, col, "Settings", "Настройки", 30);
            menu.MainControls = ButtonIn(e, col, "Controls", "Управление", 30);
            menu.Quit = ButtonIn(e, col, "Quit", "Выход", 30);

            // Friends playing right now (Steam): one click to join.
            var friends = R(main, "Friends", TopCenter, TopLeft, new Vector2(60, -196), new Vector2(350, 0));
            var fl = friends.gameObject.AddComponent<VerticalLayoutGroup>();
            fl.spacing = 6;
            fl.childControlWidth = fl.childControlHeight = true;
            fl.childForceExpandWidth = true;
            fl.childForceExpandHeight = false;
            friends.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ColumnText(friends, "Caption", "Друзья в игре", 16, Color.white, 26, true);
            menu.FriendsStatus = ColumnText(friends, "Status", "Никого из друзей нет в игре", 13, dim, 40);
            menu.FriendList = R(friends, "List", Center, Center, Vector2.zero, Vector2.zero);
            var ll = menu.FriendList.gameObject.AddComponent<VerticalLayoutGroup>();
            ll.spacing = 4;
            ll.childControlWidth = ll.childControlHeight = true;
            ll.childForceExpandWidth = true;
            ll.childForceExpandHeight = false;
            menu.FriendRowPrefab = e.FriendRow;
            menu.RefreshFriends = ButtonIn(e, friends, "Refresh", "Обновить", 28);

            // Pause menu.
            var pause = Stretch(root.transform, "Pause");
            menu.PausePage = pause.gameObject;
            Img(pause, new Color(0, 0, 0, 0.6f)).raycastTarget = true;
            var pc = Column(pause, "Column", new Vector2(-430, -150), 300);
            ColumnText(pc, "Title", "Пауза", 26, Color.white, 44, true, TextAnchor.MiddleCenter);
            menu.Role = ColumnText(pc, "Role", "Ты хост", 15, Color.white, 26, anchor: TextAnchor.MiddleCenter);
            menu.Invite = ButtonIn(e, pc, "Invite", "Пригласить друзей (Steam)", 36);
            menu.Resume = ButtonIn(e, pc, "Resume", "Продолжить", 36);
            menu.PauseSettings = ButtonIn(e, pc, "Settings", "Настройки", 36);
            menu.Leave = ButtonIn(e, pc, "Leave", "Покинуть игру", 36);
            BuildControlsPanel(e, menu, root.transform);

            // Settings.
            var settings = Stretch(root.transform, "Settings");
            menu.SettingsPage = settings.gameObject;
            Img(settings, new Color(0.04f, 0.05f, 0.06f, 0.92f)).raycastTarget = true;
            var sc = Column(settings, "Column", new Vector2(0, -84), 460);
            ColumnText(sc, "Title", "Настройки", 26, Color.white, 44, true, TextAnchor.MiddleCenter);
            Spacer(sc, 8);
            menu.SensitivityLabel = ColumnText(sc, "SensitivityLabel", "Чувствительность мыши", 15, Color.white, 22);
            menu.Sensitivity = SliderIn(e, sc, "Sensitivity", 0.02f, 0.4f, false);
            Spacer(sc, 8);
            menu.FovLabel = ColumnText(sc, "FovLabel", "Поле зрения", 15, Color.white, 22);
            menu.Fov = SliderIn(e, sc, "Fov", 60f, 100f, true);
            Spacer(sc, 8);
            menu.VolumeLabel = ColumnText(sc, "VolumeLabel", "Громкость", 15, Color.white, 22);
            menu.Volume = SliderIn(e, sc, "Volume", 0f, 1f, false);
            Spacer(sc, 8);
            menu.MusicLabel = ColumnText(sc, "MusicLabel", "Музыка", 15, Color.white, 22);
            menu.Music = SliderIn(e, sc, "Music", 0f, 1f, false);
            Spacer(sc, 10);
            menu.InvertY = ToggleIn(e, sc, "InvertY", "Инвертировать мышь по вертикали");
            menu.HeadBob = ToggleIn(e, sc, "HeadBob", "Покачивание камеры при ходьбе");
            menu.CrouchToggle = ToggleIn(e, sc, "CrouchToggle", "Присед переключается, а не удерживается");
            Spacer(sc, 14);
            var buttons = R(sc, "Buttons", Center, Center, Vector2.zero, Vector2.zero);
            Fixed(buttons, 32);
            var bh = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            bh.spacing = 10;
            bh.childControlWidth = bh.childControlHeight = true;
            bh.childForceExpandWidth = bh.childForceExpandHeight = true;
            menu.Defaults = ButtonIn(e, buttons, "Defaults", "По умолчанию", 32);
            menu.Done = ButtonIn(e, buttons, "Done", "Готово", 32);

            pause.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            // Under the pause and settings pages: the menu covers the route map when it opens.
            BuildRoute(e, root.transform);
            root.transform.Find("Route").SetSiblingIndex(0);
            return root;
        }

        /// <summary>
        /// Every control of the game in two columns (rows are added by <see cref="ControlsPanel"/> from Controls.All):
        /// right of the pause buttons in a game, over a backdrop with "Готово" from the main menu.
        /// </summary>
        static void BuildControlsPanel(UiElements e, GameMenu menu, Transform root)
        {
            var backdrop = Stretch(root, "ControlsBackdrop");
            Img(backdrop, new Color(0.04f, 0.05f, 0.06f, 0.97f)).raycastTarget = true;
            menu.ControlsBackdrop = backdrop.gameObject;
            backdrop.gameObject.SetActive(false);

            const float colW = 330f, gap = 24f, pad = 18f;
            var rt = R(root, "Controls", TopCenter, TopCenter, new Vector2(170, -60), new Vector2(pad * 2 + colW * 2 + gap, 600));
            Img(rt, new Color(0.03f, 0.04f, 0.05f, 0.88f)).raycastTarget = true;
            var panel = rt.gameObject.AddComponent<ControlsPanel>();
            menu.KeysPanel = panel;
            Txt(R(rt, "Title", TopLeft, TopLeft, new Vector2(pad, -8), new Vector2(400, 36)), "Управление", 22, TextAnchor.MiddleLeft, Color.white, true);
            panel.Columns = new RectTransform[2];
            for (int i = 0; i < 2; i++)
            {
                var c = R(rt, $"Column{i}", TopLeft, TopLeft, new Vector2(pad + i * (colW + gap), -46), new Vector2(colW, 0));
                var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 3;
                v.childControlWidth = v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
                c.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                panel.Columns[i] = c;
            }
            panel.RowPrefab = e.ControlRow;
            panel.GroupPrefab = e.ControlGroup;
            panel.Hint = Txt(R(rt, "Hint", BottomLeft, BottomLeft, new Vector2(pad, 52), new Vector2(colW * 2 + gap, 22)),
                "Нажми на клавишу, чтобы переназначить", 13, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.65f));
            panel.Reset = Instance<Button>(e.Button, rt, "Reset");
            panel.Reset.GetComponentInChildren<Text>().text = "Сбросить управление";
            var rrt = (RectTransform)panel.Reset.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = BottomLeft;
            rrt.anchoredPosition = new Vector2(pad, 14);
            rrt.sizeDelta = new Vector2(220, 32);
            menu.ControlsDone = Instance<Button>(e.Button, rt, "Done");
            menu.ControlsDone.GetComponentInChildren<Text>().text = "Готово";
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
            t.GetComponentInChildren<Text>().text = label;
            Fixed(t, 24);
            return t;
        }
    }
}
