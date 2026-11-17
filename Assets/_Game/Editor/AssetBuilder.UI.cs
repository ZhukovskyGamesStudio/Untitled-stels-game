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
            public RouteSignView RouteSign;
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
            public RouteSignView RouteSign;
            public Text DialNumber;
            public Image DialTick;
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

        static Text Txt(RectTransform rt, string text, int size, TextAnchor anchor, Color color, bool bold = false, bool shadow = true)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiFont;
            SetText(t, text);
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.supportRichText = true;
            t.raycastTarget = false;
            if (shadow)
            {
                // A dark rim all round (not a drop shadow): the text stays readable over the brightest styles.
                var s = rt.gameObject.AddComponent<Outline>();
                s.effectColor = new Color(0, 0, 0, 0.85f);
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
            SetText(b.GetComponentInChildren<Text>(), label);
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
                view.Label = Txt(R(root, "Label", Left, Left, new Vector2(10, 0), new Vector2(200, 30)), "— · 1/4", 14, TextAnchor.MiddleLeft, Color.white);
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
                var t = Txt(R(null, "TeamRow", TopRight, TopRight, Vector2.zero, new Vector2(244, 18)), "P1  ■■", 12, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.8f));
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
                view.Label = Txt(R(root, "Label", Center, Center, new Vector2(0, -21), new Vector2(160, 18)), "P1", 12, TextAnchor.MiddleCenter, Color.white);
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
                view.Title = Txt(R(root, "Title", Left, Left, new Vector2(2, 0), new Vector2(220, 24)), "—", 13, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.9f));
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
                view.Label = Txt(R(root, "Label", Center, Center, new Vector2(0, -14), new Vector2(160, 16)), "P1", 11, TextAnchor.MiddleCenter, Color.white);
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
                view.Label = Txt(R(root, "Label", Center, Center, new Vector2(0, 17), new Vector2(220, 18)), "12", 12, TextAnchor.MiddleCenter, Color.white, true);
                return root.gameObject;
            }).GetComponent<CalloutMarkerView>();


            e.RouteSign = Prefab(ElementsDir + "/RouteSign.prefab", () =>
            {
                // A world-space panel over a sewer tunnel, ~2.0 x 0.45 m, just the two lines it holds: where it leads, how big
                // and guarded that house is (or the countdown while the car stands in that tunnel).
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
                view.Frame = Img(Stretch(go.transform, "Frame"), new Color(1f, 1f, 1f, 0.35f));
                Img(Stretch(view.Frame.rectTransform, "Back", 2f), new Color(0.02f, 0.025f, 0.03f, 0.88f));
                // the house's kind on the left (office, hospital, garage…), its main job (vault / hostage) on the right
                const float ix = 60f, jw = 42f;
                view.KindIcon = Img(R(go.transform, "KindIcon", Left, Left, new Vector2(6, 0), new Vector2(50, 50)), Color.white, Icon("house_office"));
                view.KindIcons = new[] { "house_office", "house_business", "house_datacenter", "house_warehouse", "house_hq", "house_hospital", "house_garage", "house_apartments" }
                    .Select(n => Icon(n)).ToArray();
                if (view.KindIcons.Length != System.Enum.GetValues(typeof(Stealth.Level.HouseKind)).Length)
                    throw new System.InvalidOperationException("RouteSign: one icon per HouseKind");
                view.JobIcon = Img(R(go.transform, "JobIcon", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(38, 38)), new Color(1f, 0.85f, 0.4f), Icon("job_vault"));
                view.VaultIcon = Icon("job_vault");
                view.HostageIcon = Icon("job_hostage");
                view.Title = Txt(R(go.transform, "Title", TopLeft, TopLeft, new Vector2(ix, -3), new Vector2(w - ix - jw - 6, 32)), "—", 24, TextAnchor.MiddleLeft, Color.white, true);
                Img(R(go.transform, "Rule", TopLeft, TopLeft, new Vector2(ix, -36), new Vector2(w - ix - jw - 6, 2)), new Color(1f, 1f, 1f, 0.3f));
                view.Body = Txt(R(go.transform, "Body", TopLeft, TopLeft, new Vector2(ix, -39), new Vector2(w - ix - jw - 6, 24)), "—", 16, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.9f));
                view.Body.resizeTextForBestFit = true; // the main job, the floors and the guards on one line: shrinks to fit
                view.Body.resizeTextMinSize = 10;
                view.Body.resizeTextMaxSize = 16;
                // the countdown takes the body's line (same place, warm colour) instead of a third, mostly empty one
                view.Votes = Txt(R(go.transform, "Votes", TopLeft, TopLeft, new Vector2(ix, -39), new Vector2(w - ix - jw - 6, 24)), "", 16, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.4f), true);
                SetLayerRecursive(go, Stealth.Level.LevelMetrics.WorldTextLayer); // drawn after the style filter: readable in every style
                return go;
            }).GetComponent<RouteSignView>();

            e.DialNumber = Prefab(ElementsDir + "/DialNumber.prefab", () =>
                Txt(R(null, "DialNumber", Center, Center, Vector2.zero, new Vector2(40, 22)), "0", 16, TextAnchor.MiddleCenter, new Color(0.95f, 0.93f, 0.86f), true).gameObject)
                .GetComponent<Text>();
            e.DialTick = Prefab(ElementsDir + "/DialTick.prefab", () =>
                Img(R(null, "DialTick", Center, Center, Vector2.zero, new Vector2(2, 10)), Color.white).gameObject).GetComponent<Image>();

            e.ControlGroup = Prefab(ElementsDir + "/ControlGroup.prefab", () =>
            {
                var t = Txt(R(null, "ControlGroup", TopLeft, TopLeft, Vector2.zero, new Vector2(330, 26)), "—", 15, TextAnchor.LowerLeft, new Color(1f, 0.85f, 0.45f), true);
                Fixed(t, 28);
                return t.gameObject;
            }).GetComponent<Text>();
            return e;
        }

        /// <summary>The F4 cheat panel: its own canvas with a raycaster (the HUD takes no clicks), hidden until opened.</summary>
        static void BuildCheatPanel(UiElements e, Transform content)
        {
            var holder = R(content, "Cheats", Center, Center, Vector2.zero, Vector2.zero);
            holder.gameObject.AddComponent<Canvas>();
            holder.gameObject.AddComponent<GraphicRaycaster>();
            var panel = holder.gameObject.AddComponent<CheatPanel>();
            var root = R(holder, "Panel", Center, Center, new Vector2(0, 0), new Vector2(420, 360));
            Img(root, new Color(0.04f, 0.05f, 0.06f, 0.92f)).raycastTarget = true;
            panel.Root = root.gameObject;
            var col = Column(root, "Column", new Vector2(0, -14), 380, 8f);
            ColumnText(col, "Title", L("cheats.title"), 20, new Color(1f, 0.8f, 0.3f), 30, true, TextAnchor.MiddleCenter);
            panel.KillRobots = ButtonIn(e, col, "KillRobots", L("cheats.kill_robots"), 36);
            panel.Invulnerable = ButtonIn(e, col, "Invulnerable", "", 36);
            panel.Invisible = ButtonIn(e, col, "Invisible", "", 36);
            panel.Quota = ButtonIn(e, col, "Quota", L("cheats.quota"), 36);
            panel.ToHatch = ButtonIn(e, col, "ToHatch", L("cheats.to_hatch"), 36);
            panel.RevealMap = ButtonIn(e, col, "RevealMap", L("cheats.reveal_map"), 36);
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The safe minigame: a rounded panel, a metal dial (bezel, face, ticks and numbers added by the view, a hub with
        /// a grip), a fixed gold pointer, three code windows, a stethoscope of bars, key hints.
        /// </summary>
        static void BuildSafeCrack(UiElements e, Transform content)
        {
            // The view stays active and shows / hides its panel (a component that hides its own object stops updating).
            var holder = R(content, "SafeCrack", Center, Center, Vector2.zero, Vector2.zero);
            var view = holder.gameObject.AddComponent<SafeCrackView>();
            var root = R(holder, "Panel", Center, Center, new Vector2(0, 6), new Vector2(440, 520));
            view.Root = root.gameObject;
            view.Panel = root;
            var gold = new Color(1f, 0.78f, 0.32f);
            // a thin gold edge (the panel's own image) around a dark face
            var edge = Img(root, new Color(gold.r, gold.g, gold.b, 0.45f), S("RoundPanel"));
            edge.type = Image.Type.Sliced;
            var back = Img(Stretch(root, "Back", 1.5f), new Color(0.035f, 0.04f, 0.05f, 0.96f), S("RoundPanel"));
            back.type = Image.Type.Sliced;

            Txt(R(root, "Title", TopCenter, TopCenter, new Vector2(0, -14), new Vector2(400, 26)), L("safe.title"), 17, TextAnchor.MiddleCenter, gold, true);

            // Code windows.
            view.Slots = new Image[3];
            view.SlotTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = R(root, $"Slot{i}", TopCenter, TopCenter, new Vector2((i - 1) * 62f, -66), new Vector2(54, 34));
                view.Slots[i] = Img(slot, new Color(0.08f, 0.09f, 0.1f, 0.9f), S("RoundPanel"));
                view.Slots[i].type = Image.Type.Sliced;
                view.SlotTexts[i] = Txt(Stretch(slot, "Digit"), "", 20, TextAnchor.MiddleCenter, gold, true, shadow: false);
            }

            // The dial.
            var face = R(root, "Dial", Center, Center, new Vector2(0, 10), new Vector2(276, 276));
            Img(R(face, "Shadow", Center, Center, new Vector2(0, -6), new Vector2(300, 300)), new Color(0, 0, 0, 0.6f), S("SoftGlow"));
            view.Ring = Img(R(face, "Ring", Center, Center, Vector2.zero, new Vector2(300, 300)), new Color(0.35f, 1f, 0.5f, 0.8f), S("Ring"));
            Img(R(face, "Bezel", Center, Center, Vector2.zero, new Vector2(276, 276)), new Color(0.82f, 0.82f, 0.86f, 1f), S("DialBezel"));
            Img(R(face, "Face", Center, Center, Vector2.zero, new Vector2(236, 236)), new Color(0.13f, 0.14f, 0.16f, 1f), S("DialFace"));
            view.Dial = R(face, "Turning", Center, Center, Vector2.zero, Vector2.zero);
            view.TickPrefab = e.DialTick;
            view.NumberPrefab = e.DialNumber;
            view.TickRadius = 116f;
            view.NumberRadius = 90f;
            // the hub turns with the dial: a grip bar shows it turning
            Img(R(view.Dial, "Hub", Center, Center, Vector2.zero, new Vector2(84, 84)), new Color(0.3f, 0.31f, 0.34f, 1f), S("Disc"));
            Img(R(view.Dial, "HubSheen", Center, Center, new Vector2(-8, 8), new Vector2(60, 60)), new Color(1f, 1f, 1f, 0.1f), S("SoftGlow"));
            view.Readout = Txt(R(face, "Readout", Center, Center, new Vector2(0, -2), new Vector2(80, 40)), "00", 26, TextAnchor.MiddleCenter, new Color(0.95f, 0.93f, 0.86f), true);
            view.Flash = Img(R(face, "Flash", Center, Center, Vector2.zero, new Vector2(236, 236)), new Color(1f, 0.25f, 0.18f, 0.4f), S("Disc"));
            var pointer = R(face, "Pointer", Center, Center, new Vector2(0, 146), new Vector2(22, 22));
            pointer.localEulerAngles = new Vector3(0, 0, 180);
            Img(pointer, gold, S("MapArrow"));
            Img(R(face, "PointerLine", Center, Center, new Vector2(0, 128), new Vector2(2, 18)), gold);

            // Stethoscope: bars rising towards the right number.
            view.Bars = new Image[16];
            for (int i = 0; i < 16; i++)
            {
                float hgt = 6f + i * 1.6f;
                view.Bars[i] = Img(R(root, $"Bar{i}", BottomCenter, BottomCenter, new Vector2(-120f + i * 16f, 64), new Vector2(10, hgt)), Color.white);
                view.Bars[i].rectTransform.pivot = new Vector2(0.5f, 0f);
            }

            view.Hint = Txt(R(root, "Hint", BottomCenter, BottomCenter, new Vector2(0, 20), new Vector2(420, 22)), "", 12, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.8f));
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The vault's switch and sequence locks: a rounded panel, a title, a row of round lamps with their number keys under
        /// them, the sequence's progress dots, a red flash for a wrong press, the keys to use.
        /// </summary>
        static void BuildVaultLock(Transform content)
        {
            var holder = R(content, "VaultLock", Center, Center, Vector2.zero, Vector2.zero);
            var view = holder.gameObject.AddComponent<VaultLockView>();
            var root = R(holder, "Panel", Center, Center, new Vector2(0, -20), new Vector2(520, 270));
            view.Root = root.gameObject;
            var gold = new Color(1f, 0.78f, 0.32f);
            var edge = Img(root, new Color(gold.r, gold.g, gold.b, 0.45f), S("RoundPanel"));
            edge.type = Image.Type.Sliced;
            var back = Img(Stretch(root, "Back", 1.5f), new Color(0.035f, 0.04f, 0.05f, 0.96f), S("RoundPanel"));
            back.type = Image.Type.Sliced;
            view.Flash = Img(Stretch(root, "Flash", 2f), new Color(1f, 0.25f, 0.18f, 0.35f), S("RoundPanel"));
            view.Flash.type = Image.Type.Sliced;
            view.Title = Txt(R(root, "Title", TopCenter, TopCenter, new Vector2(0, -14), new Vector2(480, 26)), "", 17, TextAnchor.MiddleCenter, gold, true);
            const int lamps = 7;
            view.Lamps = new Image[lamps];
            view.Keys = new Text[lamps];
            for (int i = 0; i < lamps; i++)
            {
                view.Lamps[i] = Img(R(root, $"Lamp{i}", Center, Center, new Vector2(0, 14), new Vector2(62, 62)), new Color(0.16f, 0.17f, 0.19f), S("Disc"));
                view.Keys[i] = Txt(R(root, $"Key{i}", Center, Center, new Vector2(0, -46), new Vector2(40, 28)), (i + 1).ToString(), 18, TextAnchor.MiddleCenter, new Color(0.95f, 0.93f, 0.86f), true);
            }
            const int dots = 8;
            view.Dots = new Image[dots];
            for (int i = 0; i < dots; i++)
                view.Dots[i] = Img(R(root, $"Dot{i}", Center, Center, new Vector2(0, -90), new Vector2(12, 12)), new Color(1f, 1f, 1f, 0.4f), S("Disc"));
            view.Hint = Txt(R(root, "Hint", BottomCenter, BottomCenter, new Vector2(0, 14), new Vector2(480, 22)), "", 12, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.8f));
            root.gameObject.SetActive(false);
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
                ? Txt(R(root, "Caption", TopLeft, TopLeft, new Vector2(6, -2), new Vector2(120, 18)), "1", 11, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.75f))
                : Txt(R(root, "Caption", TopLeft, TopLeft, new Vector2(12, -2), new Vector2(size.x - 24, 30)), "1", 15, TextAnchor.MiddleLeft, Color.white, true);
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

        // ------------------------------------------------------------------ inventory

        /// <summary>A UI picture from <c>Art/Sprites/UI</c> (drawn by Tools/Blender/ui_backpack.py) as a sprite.</summary>
        static Sprite UiSprite(string name)
        {
            var path = $"{SpritesDir}/UI/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"[Builder] {path} missing: run Tools/Blender/ui_backpack.py");
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

        static GameObject BuildInventoryScreen(UiElements e)
        {
            var root = CanvasRoot("Inventory", 5, false);
            var ui = root.AddComponent<InventoryUI>();
            ui.Canvas = (RectTransform)root.transform;
            const float c = InventoryUI.Cell;
            float gw = InventoryLayout.Width * c, gh = InventoryLayout.Height * c;

            // the open backpack (Tools/Blender/ui_backpack.py): the grid lies on its lining, 20 px of backpack around it
            const float pad = 20f;
            ui.Panel = R(root.transform, "Panel", BottomCenter, BottomCenter, new Vector2(0, 13), new Vector2(gw + 2 * pad, gh + 2 * pad + 22));
            ui.GridBackground = Img(R(ui.Panel, "GridBackground", BottomCenter, BottomCenter, Vector2.zero, new Vector2(gw + 2 * pad, gh + 2 * pad)),
                Color.white, UiSprite("backpack"));
            var header = R(ui.Panel, "Header", BottomCenter, BottomCenter, new Vector2(0, gh + 2 * pad), new Vector2(gw + 2 * pad, 22));
            ui.Header = header.gameObject;
            ui.HeaderBackground = Img(header, new Color(0, 0, 0, 0.6f));
            ui.ValueLabel = Txt(R(header, "Value", Left, Left, new Vector2(5, 0), new Vector2(120, 20)), "0", 13, TextAnchor.MiddleLeft, Color.white);

            ui.Grid = R(ui.Panel, "Grid", BottomCenter, TopCenter, new Vector2(0, pad + gh), new Vector2(gw, gh));
            ui.Grid.pivot = TopLeft;
            ui.Grid.anchoredPosition = new Vector2(-gw / 2f, pad + gh);
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

            // the hand slot right of the backpack: one held item (the flashlight, batteries), used with the hand key
            ui.HandSlot = R(ui.Panel, "HandSlot", new Vector2(1, 0), BottomLeft, new Vector2(8, 0), new Vector2(2 * c + 12, 3 * c + 30));
            ui.HandBackground = Img(Stretch(ui.HandSlot, "Background"), Color.white, UiSprite("hand_slot"));
            var hole = R(ui.HandSlot, "Hole", TopCenter, TopCenter, new Vector2(0, -8), new Vector2(2 * c - 4, 3 * c + 30 - 32));
            ui.HandItems = R(hole, "HandItems", Center, TopLeft, Vector2.zero, Vector2.zero);
            ui.HandLabel = Txt(R(ui.HandSlot, "Label", BottomCenter, BottomCenter, new Vector2(0, 2), new Vector2(2 * c + 12, 18)), "", 11, TextAnchor.MiddleCenter, Color.white);

            ui.Ghost = R(root.transform, "DragGhost", BottomLeft, TopLeft, Vector2.zero, Vector2.zero);
            ui.BlockPrefab = e.InventoryBlock;
            ui.HighlightPrefab = e.CellHighlight;
            ui.Tooltip = Txt(R(root.transform, "Tooltip", BottomLeft, TopLeft, Vector2.zero, new Vector2(320, 22)), "", 13, TextAnchor.MiddleLeft, Color.white);
            return root;
        }

        /// <summary>
        /// Team stash above the hatch: a world-space canvas with the stash grid and a "sell" area. Same units as the
        /// inventory (<see cref="InventoryUI.Cell"/> per cell), scaled down to metres.
        /// </summary>
        static GameObject BuildStashBoard(UiElements e)
        {
            const float c = InventoryUI.Cell, pad = 8f, gap = 12f, header = 26f, sellW = 96f, buttonH = 30f;
            float gw = TeamStash.Width * c, gh = TeamStash.Height * c;
            var go = new GameObject("StashBoard", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(pad + gw + gap + sellW + pad, header + gh + pad * 3f + buttonH);
            rt.localScale = Vector3.one * 0.0065f; // ~2.5 m wide
            var board = go.AddComponent<StashBoard>();
            board.Canvas = canvas;
            board.Root = rt;

            Img(Stretch(go.transform, "Background"), new Color(0.02f, 0.03f, 0.05f, 0.7f));
            board.ValueLabel = Txt(R(go.transform, "Title", TopLeft, TopLeft, new Vector2(pad, -2f), new Vector2(gw, header)),
                "0", 16, TextAnchor.MiddleLeft, Color.white, bold: true);
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
            Txt(Stretch(board.SellZone, "Label", 4f), L("stash.sell"), 16, TextAnchor.MiddleCenter, Color.white, bold: true);
            board.StashAllZone = R(go.transform, "StashAll", TopLeft, TopLeft, new Vector2(pad, -(header + gh + pad * 2f)), new Vector2(gw + gap + sellW, buttonH));
            board.StashAllBackground = Img(board.StashAllZone, new Color(0.3f, 0.75f, 1f, 0.45f));
            board.StashAllLabel = Txt(Stretch(board.StashAllZone, "Label", 2f), "G", 15, TextAnchor.MiddleCenter, Color.white, bold: true);
            SetLayerRecursive(go, Stealth.Level.LevelMetrics.WorldTextLayer); // drawn after the style filter: readable in every style
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
            ColumnText(col, "Title", L("menu.game_title"), 30, Color.white, 50, true, TextAnchor.MiddleCenter);
            Spacer(col, 20);
            menu.SteamStatus = ColumnText(col, "SteamStatus", "Steam", 14, Color.white, 22);
            menu.HostSteam = ButtonIn(e, col, "HostSteam", L("menu.host_steam"), 44);
            Spacer(col, 14);
            menu.LocalCaption = ColumnText(col, "LocalCaption", "LAN", 14, Color.white, 22);
            menu.HostLocal = ButtonIn(e, col, "HostLocal", L("menu.host_local"), 36);
            menu.TestRooms = ToggleIn(e, col, "TestRooms", L("menu.test_rooms")); // off: the main game
            menu.ClassicRooms = ToggleIn(e, col, "ClassicRooms", L("menu.classic_rooms")); // off: the room program
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
            menu.Join.GetComponent<LayoutElement>().preferredWidth = 200;
            Spacer(col, 14);
            menu.NetStatus = ColumnText(col, "NetStatus", "", 14, new Color(1f, 0.85f, 0.5f), 22);
            menu.MainSettings = ButtonIn(e, col, "Settings", L("menu.settings"), 30);
            menu.MainControls = ButtonIn(e, col, "Controls", L("controls.title"), 30);
            menu.Quit = ButtonIn(e, col, "Quit", L("menu.quit"), 30);

            // Friends playing right now (Steam): one click to join.
            var friends = R(main, "Friends", TopCenter, TopLeft, new Vector2(60, -196), new Vector2(350, 0));
            var fl = friends.gameObject.AddComponent<VerticalLayoutGroup>();
            fl.spacing = 6;
            fl.childControlWidth = fl.childControlHeight = true;
            fl.childForceExpandWidth = true;
            fl.childForceExpandHeight = false;
            friends.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ColumnText(friends, "Caption", L("friends.caption"), 16, Color.white, 26, true);
            menu.FriendsStatus = ColumnText(friends, "Status", "", 13, dim, 40);
            menu.FriendList = R(friends, "List", Center, Center, Vector2.zero, Vector2.zero);
            var ll = menu.FriendList.gameObject.AddComponent<VerticalLayoutGroup>();
            ll.spacing = 4;
            ll.childControlWidth = ll.childControlHeight = true;
            ll.childForceExpandWidth = true;
            ll.childForceExpandHeight = false;
            menu.FriendRowPrefab = e.FriendRow;
            menu.RefreshFriends = ButtonIn(e, friends, "Refresh", L("friends.refresh"), 28);

            // Pause menu.
            var pause = Stretch(root.transform, "Pause");
            menu.PausePage = pause.gameObject;
            Img(pause, new Color(0, 0, 0, 0.6f)).raycastTarget = true;
            var pc = Column(pause, "Column", new Vector2(-430, -150), 300);
            ColumnText(pc, "Title", L("menu.pause"), 26, Color.white, 44, true, TextAnchor.MiddleCenter);
            menu.Role = ColumnText(pc, "Role", "", 15, Color.white, 26, anchor: TextAnchor.MiddleCenter);
            menu.Invite = ButtonIn(e, pc, "Invite", L("menu.invite"), 36);
            menu.Resume = ButtonIn(e, pc, "Resume", L("menu.resume"), 36);
            menu.Unstuck = ButtonIn(e, pc, "Unstuck", L("menu.unstuck"), 36);
            menu.PauseSettings = ButtonIn(e, pc, "Settings", L("menu.settings"), 36);
            menu.Leave = ButtonIn(e, pc, "Leave", L("menu.leave"), 36);
            BuildControlsPanel(e, menu, root.transform);

            // Settings.
            var settings = Stretch(root.transform, "Settings");
            menu.SettingsPage = settings.gameObject;
            Img(settings, new Color(0.04f, 0.05f, 0.06f, 0.92f)).raycastTarget = true;
            var sc = Column(settings, "Column", new Vector2(0, -84), 460);
            ColumnText(sc, "Title", L("menu.settings"), 26, Color.white, 44, true, TextAnchor.MiddleCenter);
            Spacer(sc, 8);
            menu.SensitivityLabel = ColumnText(sc, "SensitivityLabel", "", 15, Color.white, 22);
            menu.Sensitivity = SliderIn(e, sc, "Sensitivity", 0.02f, 0.4f, false);
            Spacer(sc, 8);
            menu.FovLabel = ColumnText(sc, "FovLabel", "", 15, Color.white, 22);
            menu.Fov = SliderIn(e, sc, "Fov", 60f, 100f, true);
            Spacer(sc, 8);
            menu.VolumeLabel = ColumnText(sc, "VolumeLabel", "", 15, Color.white, 22);
            menu.Volume = SliderIn(e, sc, "Volume", 0f, 1f, false);
            Spacer(sc, 8);
            menu.MusicLabel = ColumnText(sc, "MusicLabel", "", 15, Color.white, 22);
            menu.Music = SliderIn(e, sc, "Music", 0f, 1f, false);
            Spacer(sc, 10);
            menu.InvertY = ToggleIn(e, sc, "InvertY", L("menu.invert_y"));
            menu.HeadBob = ToggleIn(e, sc, "HeadBob", L("menu.head_bob"));
            menu.CrouchToggle = ToggleIn(e, sc, "CrouchToggle", L("menu.crouch_toggle"));
            Spacer(sc, 10);
            menu.Language = ButtonIn(e, sc, "Language", "", 30); // the label names the language: set by GameMenu
            Spacer(sc, 14);
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

        /// <summary>
        /// The studio's splash over everything (its own canvas order): the navy veil, the 1920 x 1080 frame with the name
        /// behind its clip and the wing with its eye (shapes from code, StudioSplash), the sound.
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
        /// Every control of the game in two columns (rows are added by <see cref="ControlsPanel"/> from Controls.All):
        /// right of the pause buttons in a game, over a backdrop with "Done" from the main menu.
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
            Txt(R(rt, "Title", TopLeft, TopLeft, new Vector2(pad, -8), new Vector2(400, 36)), L("controls.title"), 22, TextAnchor.MiddleLeft, Color.white, true);
            // The two columns scroll (mouse wheel or the bar on the right) inside the panel: there are more controls than fit.
            const float viewH = 600f - 46f - 84f;
            var view = R(rt, "Scroll", TopLeft, TopLeft, new Vector2(pad, -46), new Vector2(colW * 2 + gap + 6, viewH));
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
                v.spacing = 3;
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
            var barRt = R(rt, "Scrollbar", TopRight, TopRight, new Vector2(-6, -46), new Vector2(8, viewH));
            Img(barRt, new Color(1, 1, 1, 0.06f)).raycastTarget = true;
            var bar = barRt.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Stretch(Stretch(barRt, "Area"), "Handle");
            bar.handleRect = handle;
            bar.targetGraphic = Img(handle, new Color(1, 1, 1, 0.35f));
            bar.targetGraphic.raycastTarget = true;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            panel.RowPrefab = e.ControlRow;
            panel.GroupPrefab = e.ControlGroup;
            panel.Hint = Txt(R(rt, "Hint", BottomLeft, BottomLeft, new Vector2(pad, 52), new Vector2(colW * 2 + gap, 22)),
                "", 13, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.65f));
            panel.Reset = Instance<Button>(e.Button, rt, "Reset");
            SetText(panel.Reset.GetComponentInChildren<Text>(), L("controls.reset"));
            var rrt = (RectTransform)panel.Reset.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = BottomLeft;
            rrt.anchoredPosition = new Vector2(pad, 14);
            rrt.sizeDelta = new Vector2(220, 32);
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
