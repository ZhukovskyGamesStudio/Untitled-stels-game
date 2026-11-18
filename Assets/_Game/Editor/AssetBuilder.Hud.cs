using System.Linq;
using Stealth.Game;
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

        const float MinimapSize = 210f;
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
            "ret_fuel", "ret_terminal", "ret_craft", "ret_lever", "ret_safe", "ret_crate", "ret_door",
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
            // The team's switched-off robots: one plug per slot (lit = a robot is off), right of the speaker.
            hud.HackSlots = Enumerable.Range(0, 4)
                .Select(i => Img(R(status, $"Hack{i}", BottomLeft, BottomLeft, new Vector2(76 + i * 24, 30), new Vector2(22, 22)), Color.white, Icon("ret_hack"))).ToArray();

            // Fuel panel, top left: the house's name, a drop, the tank / what the drive burns, the hunter under it.
            var quota = R(content, "Quota", TopLeft, TopLeft, new Vector2(16, -16), new Vector2(300, 118));
            Img(quota, PanelColor);
            hud.HouseLabel = Txt(R(quota, "House", TopLeft, TopLeft, new Vector2(10, -2), new Vector2(280, 22)), "House", 14, TextAnchor.MiddleLeft, Color.white, true);
            hud.HouseLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            Img(R(quota, "FuelIcon", TopLeft, TopLeft, new Vector2(8, -24), new Vector2(18, 18)), new Color(1f, 0.8f, 0.2f, 0.9f), Icon("ret_fuel"));
            hud.QuotaLabel = Txt(R(quota, "Count", TopLeft, TopLeft, new Vector2(10, -23), new Vector2(280, 20)), "0 / 100", 14, TextAnchor.MiddleRight, Color.white, true);
            hud.QuotaBar = Bar(quota, "Bar", TopLeft, TopLeft, new Vector2(30, -29), new Vector2(200, 8), new Color(0, 0, 0, 0.5f), new Color(1f, 0.8f, 0.2f), 2f);
            hud.GainRect = R(quota, "Gain", TopLeft, Left, new Vector2(GameHUD.GainX, GameHUD.GainY), new Vector2(120, 26));
            hud.GainLabel = Txt(hud.GainRect, "+20", 18, TextAnchor.MiddleLeft, new Color(0.45f, 1f, 0.5f), true);
            hud.HunterLabel = Txt(R(quota, "Hunter", TopLeft, TopLeft, new Vector2(10, -44), new Vector2(280, 20)), "Hunter", 12, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.7f));
            hud.ObjectiveLabel = Txt(R(quota, "Objective", TopLeft, TopLeft, new Vector2(10, -62), new Vector2(280, 52)), "", 12, TextAnchor.UpperLeft, Color.white, true); // the task list, a line each
            hud.TerminalLabel = Txt(R(content, "Terminal", BottomCenter, BottomCenter, new Vector2(0, 150), new Vector2(700, 24)), "1 / 3", 15, TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.8f, 0.9f), true);

            // The team list top right under the minimap.
            hud.TeamList = R(content, "Team", TopRight, TopRight, new Vector2(-16, -16 - MinimapSize - 8), new Vector2(244, 0));
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

            // The minimap (top right) and the big map (M, the whole floor, north-up).
            hud.Minimap = BuildMap(e, content, "Minimap", true, TopRight, TopRight, new Vector2(-16, -16), new Vector2(MinimapSize, MinimapSize), 4.5f);
            hud.BigMap = BuildMap(e, content, "BigMap", false, Center, Center, new Vector2(0, 10), new Vector2(760, 600), 1f);
            hud.BigMap.gameObject.SetActive(false);
            BuildSafeCrack(e, content);
            BuildVaultLock(content);
            BuildCheatPanel(e, content);
            // the screen goes black between the drive to the next house and the house itself (GameHUD.ShowLoadingFade):
            // over everything, outside Content (which fades out on its own while the drive scene plays)
            hud.LoadingFade = Img(Stretch(root.transform, "LoadingFade"), Color.black);
            hud.LoadingFade.raycastTarget = false;
            return root;
        }
    }
}
