using Stealth.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The interface's look, as building blocks for the UI prefabs: an office's paperwork — typed forms on off-white
    /// sheets, sticky notes in handwriting, masking tape, binder clips, a red rubber stamp, highlighter marks (colours —
    /// <see cref="UiPalette"/>; sprites — Tools/Art/generate_paper_ui.py; fonts — Art/Fonts). Text on paper is ink with no
    /// rim; text straight over the 3D view is paper-coloured with an ink rim (<see cref="WorldTxt"/>).
    /// </summary>
    public static partial class AssetBuilder
    {
        const string FontsDir = Root + "/Art/Fonts";
        const string PaperDir = SpritesDir + "/UI/Paper";

        /// <summary>The faces of the interface.</summary>
        enum Face
        {
            /// <summary>PT Sans: the printed form's body text.</summary>
            Sans,
            /// <summary>PT Sans Bold: headings, buttons, names.</summary>
            Bold,
            /// <summary>PT Sans Narrow Bold: captions in capitals, stamps.</summary>
            Caps,
            /// <summary>PT Mono: what was typed in — numbers, timers, key names.</summary>
            Mono,
            /// <summary>Caveat: handwriting on the sticky notes.</summary>
            Hand,
        }

        static readonly string[] FaceFiles = { "PTSans-Regular", "PTSans-Bold", "PTSansNarrow-Bold", "PTMono-Regular", "Caveat" };
        static readonly Font[] s_Faces = new Font[5];

        /// <summary>A face's font asset: dynamic, its data in the build, PT Mono behind it for the arrows the others lack.</summary>
        static Font FontOf(Face face)
        {
            int i = (int)face;
            if (s_Faces[i] != null) return s_Faces[i];
            var path = $"{FontsDir}/{FaceFiles[i]}.ttf";
            if (AssetImporter.GetAtPath(path) is not TrueTypeFontImporter importer)
                throw new System.IO.FileNotFoundException($"{path}: the interface fonts (Art/Fonts/CREDITS.md)");
            var mono = face == Face.Mono ? null : FontOf(Face.Mono);
            var fallback = mono != null ? new[] { mono } : new Font[0];
            bool same = importer.fontReferences != null && importer.fontReferences.Length == fallback.Length
                        && (fallback.Length == 0 || importer.fontReferences[0] == mono);
            if (importer.fontTextureCase != FontTextureCase.Dynamic || !importer.includeFontData || !same)
            {
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.includeFontData = true;
                importer.fontReferences = fallback;
                importer.SaveAndReimport();
            }
            return s_Faces[i] = AssetDatabase.LoadAssetAtPath<Font>(path);
        }

        /// <summary>A paper sprite (Art/Sprites/UI/Paper) with its 9-slice border (left, bottom, right, top).</summary>
        static Sprite PaperSprite(string name, Vector4 border = default)
        {
            var path = $"{PaperDir}/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new System.IO.FileNotFoundException($"{path}: run Tools/Art/generate_paper_ui.py");
            var wrap = name is "graph" or "lines" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || !importer.alphaIsTransparency
                || importer.spriteBorder != border || importer.wrapMode != wrap || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.wrapMode = wrap;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // thin ink lines: no blocks
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Vector4 B(float all) => new(all, all, all, all);

        static Sprite CardSprite => PaperSprite("card", new Vector4(8, 12, 12, 8));
        static Sprite SheetSprite => PaperSprite("paper", B(8));
        static Sprite StickySprite => PaperSprite("sticky", B(16));
        static Sprite BoxSprite => PaperSprite("box", B(4));
        static Sprite HighlightSprite => PaperSprite("highlight", new Vector4(10, 0, 10, 0));

        /// <summary>Ink text on paper: no rim, the given face.</summary>
        static Text Ink(RectTransform rt, string text, int size, TextAnchor anchor, Face face = Face.Sans, Color? color = null)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = FontOf(face);
            SetText(t, text);
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? UiPalette.Ink;
            t.fontStyle = FontStyle.Normal;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Text straight over the 3D view: paper-coloured (or the given colour) with an ink rim all round.</summary>
        static Text WorldTxt(RectTransform rt, string text, int size, TextAnchor anchor, Face face = Face.Bold, Color? color = null)
        {
            var t = Ink(rt, text, size, anchor, face, color ?? UiPalette.OnWorld);
            var rim = rt.gameObject.AddComponent<Outline>();
            rim.effectColor = UiPalette.Rim;
            rim.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }

        /// <summary>A HUD card on <paramref name="rt"/> itself (one Image, its small shadow baked in): presenters can show, hide and tint it.</summary>
        static Image Card(RectTransform rt, Color? color = null)
        {
            var i = Img(rt, color ?? UiPalette.Paper, CardSprite);
            i.type = Image.Type.Sliced;
            return i;
        }

        /// <summary>
        /// A sheet lying on something (the menus): a soft shadow and the paper under the content — children added to the
        /// returned rect afterwards lie on the paper. Not for something a presenter hides by its Image.
        /// </summary>
        static (RectTransform root, Image paper) Sheet(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? color = null)
        {
            var root = R(parent, name, anchor, pivot, pos, size);
            var shade = Stretch(root, "Shadow", -16f);
            shade.anchoredPosition = new Vector2(3f, -5f);
            Img(shade, new Color(0f, 0f, 0f, 0.55f), PaperSprite("shadow", B(24))).type = Image.Type.Sliced;
            var paper = Img(Stretch(root, "Paper"), color ?? UiPalette.Paper, SheetSprite);
            paper.type = Image.Type.Sliced;
            return (root, paper);
        }

        /// <summary>A sticky note under the content (a soft shadow, the note's colour, slightly turned).</summary>
        static RectTransform Note(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, float tilt)
        {
            var root = R(parent, name, anchor, pivot, pos, size);
            root.localEulerAngles = new Vector3(0, 0, tilt);
            NoteBack(root, color);
            return root;
        }

        /// <summary>The shadow and the note itself, stretched over <paramref name="root"/> (first children: the content lies on it).</summary>
        static Image NoteBack(RectTransform root, Color color)
        {
            var shade = Stretch(root, "Shadow", -14f);
            shade.anchoredPosition = new Vector2(2f, -4f);
            var s = Img(shade, new Color(0f, 0f, 0f, 0.45f), PaperSprite("shadow", B(24)));
            s.type = Image.Type.Sliced;
            IgnoreLayout(shade);
            var note = Img(Stretch(root, "Note"), color, StickySprite);
            note.type = Image.Type.Sliced;
            IgnoreLayout(note.rectTransform);
            return note;
        }

        static void IgnoreLayout(Component c) => (c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;

        /// <summary>A strip of masking tape over an edge.</summary>
        static Image Tape(Transform parent, Vector2 anchor, Vector2 pos, float width, float angle)
        {
            var rt = R(parent, "Tape", anchor, Center, pos, new Vector2(width, 20));
            rt.localEulerAngles = new Vector3(0, 0, angle);
            var i = Img(rt, new Color(0.93f, 0.9f, 0.8f, 0.9f), PaperSprite("tape", new Vector4(10, 0, 10, 0)));
            i.type = Image.Type.Sliced;
            IgnoreLayout(rt);
            return i;
        }

        /// <summary>A black binder clip biting the top edge.</summary>
        static void Clip(Transform parent, Vector2 pos)
        {
            var rt = R(parent, "Clip", TopCenter, new Vector2(0.5f, 0.35f), pos, new Vector2(48, 44));
            Img(rt, Color.white, PaperSprite("clip"));
            IgnoreLayout(rt);
        }

        /// <summary>A printed line of the form.</summary>
        static Image RuleLine(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float width, Color? color = null, float thickness = 1f)
        {
            var i = Img(R(parent, "Rule", anchor, pivot, pos, new Vector2(width, thickness)), color ?? UiPalette.Rule);
            IgnoreLayout(i);
            return i;
        }

        /// <summary>A caption of the form: capitals, soft ink, a line under it.</summary>
        static Text Caption(Transform parent, string name, Vector2 pos, float width, string text, int size = 14, Color? color = null)
        {
            var t = Ink(R(parent, name, TopLeft, TopLeft, pos, new Vector2(width, size + 8)), text, size, TextAnchor.LowerLeft, Face.Caps, color ?? UiPalette.InkSoft);
            RuleLine(parent, TopLeft, TopLeft, pos + new Vector2(0, -(size + 9)), width);
            return t;
        }

        /// <summary>
        /// A rubber stamp: its worn double frame and the words in capitals, in stamp red, turned a little; a veil of paper
        /// specks over the letters where the ink did not take. Returns the words.
        /// </summary>
        static Text Stamp(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string text, int fontSize, float angle, Color? color = null, Color? paper = null)
        {
            var c = color ?? UiPalette.Stamp;
            var root = R(parent, name, anchor, pivot, pos, size);
            root.localEulerAngles = new Vector3(0, 0, angle);
            Img(root, c, PaperSprite("stamp_frame", B(14))).type = Image.Type.Sliced;
            var words = Ink(Stretch(root, "Words", 8f), text, fontSize, TextAnchor.MiddleCenter, Face.Caps, c);
            words.resizeTextForBestFit = true;
            words.resizeTextMinSize = Mathf.Max(8, fontSize / 2);
            words.resizeTextMaxSize = fontSize;
            var specks = Img(Stretch(root, "Wear", 4f), (paper ?? UiPalette.Paper).A(0.85f), PaperSprite("stamp_ink"));
            specks.type = Image.Type.Tiled;
            return words;
        }

        /// <summary>A form box for a bar, a field or a code window: the fill (tinted) inside a thin ink rim.</summary>
        static Image FormBox(RectTransform rt, Color? fill = null)
        {
            var i = Img(rt, fill ?? UiPalette.Paper, BoxSprite);
            i.type = Image.Type.Sliced;
            return i;
        }

        /// <summary>A bar on paper: a form box, a highlighter stroke filling it (the presenter colours the stroke).</summary>
        static BarView PaperBar(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color fill)
        {
            var rt = R(parent, name, anchor, pivot, pos, size);
            FormBox(rt, UiPalette.PaperShade);
            var area = Stretch(rt, "Area", 1.5f);
            var f = Stretch(area, "Fill");
            f.anchorMax = new Vector2(0.5f, 1f);
            var view = rt.gameObject.AddComponent<BarView>();
            view.Fill = f;
            view.FillImage = Img(f, fill, HighlightSprite);
            view.FillImage.type = Image.Type.Sliced;
            return view;
        }

        /// <summary>A slip of paper behind a text a presenter shows and hides (a sibling before it, see <see cref="UiBacking"/>).</summary>
        static UiBacking Slip(Text text, bool fit, Vector2 pad, Color? color = null)
        {
            var rt = text.rectTransform;
            var slip = R(rt.parent, rt.name + "Slip", rt.anchorMin, rt.pivot, rt.anchoredPosition, rt.sizeDelta);
            slip.anchorMax = rt.anchorMax;
            slip.SetSiblingIndex(rt.GetSiblingIndex());
            Card(slip, color);
            var b = text.gameObject.AddComponent<UiBacking>();
            b.Backing = slip;
            b.Fit = fit;
            b.Pad = pad;
            if (!fit)
            {
                slip.sizeDelta = rt.sizeDelta + 2f * pad;
                slip.anchoredPosition = rt.anchoredPosition + Vector2.Scale(2f * rt.pivot - Vector2.one, pad); // the same pivot, grown by the pad all round
            }
            slip.gameObject.SetActive(text.gameObject.activeSelf);
            return b;
        }
    }
}
