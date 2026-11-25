using System.Collections.Generic;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Linus's backpack, on every peer: a pack of its own for each level of the backpack upgrade (Tools/Blender/raccoon.py:
    /// a scruffy sack, the plaid pack, a hiking pack with a bedroll, a frame pack), and everything in the team's stash
    /// strapped round it in its own size — the stash grid wrapped over the pack's sides, top and back
    /// (<see cref="PackWrap"/>): a thing moved on the grid moves on the pack too. Purely visual; rebuilt when the stash or
    /// the level changes.
    /// </summary>
    public class RaccoonPack : MonoBehaviour
    {
        [Tooltip("The pack of each level 0..3 (one shown).")]
        public GameObject[] Levels;
        [Tooltip("Each level's load: the middle of its pack's top, +X to the right seen from behind, +Y up, +Z towards the head.")]
        public Transform[] Loads;
        [Tooltip("Each level's box the stash wraps round, in its load's space: width (x), height (y), length (z), metres.")]
        public Vector3[] Boxes;
        [Tooltip("The load's pieces (each a MeshFilter with a MeshRenderer, hidden until a thing lies there): one per stash cell at most.")]
        public MeshFilter[] Pieces;
        [Tooltip("Each piece's strap (raccoon_strap: Strap_Top, Strap_SideL, Strap_SideR, Metal_Buckle), round the middle of its thing.")]
        public Transform[] Straps;
        [Tooltip("How far the things swing on their straps as Linus moves, degrees (at a run; a little at a walk, none at rest).")]
        [Range(0f, 10f)] public float Sway = 3.5f;
        [Tooltip("Each shape's strap where it goes round the thing's middle (the builder reads the model's vertices: a build " +
                 "cannot): the middle and half the width across, the top there, in the laid thing's space; zero: none (its box).")]
        public Vector4[] StrapFits = new Vector4[0];
        public ItemVisuals Visuals;

        readonly GameObject[] m_Models = new GameObject[TeamStash.MaxWidth * TeamStash.MaxHeight];
        readonly Quaternion[] m_Rest = new Quaternion[TeamStash.MaxWidth * TeamStash.MaxHeight];
        int m_Version = -1, m_Level = -1, m_Shown;
        Vector3 m_LastPos;
        float m_Speed, m_Swing;

        /// <summary>The strap round a thing: this much wider than it, this thick, this wide, metres.</summary>
        const float StrapMargin = 0.012f, StrapThick = 0.006f, StrapWidth = 0.028f;

        /// <summary>
        /// The things swing a little on their straps (every peer, from how fast Linus goes): each about its strap's point on
        /// the face, its own phase, a few degrees at a run, still at rest.
        /// </summary>
        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var p = transform.position;
            float v = Vector3.Distance(p, m_LastPos) / dt;
            m_LastPos = p;
            if (v > 20f) v = 0f; // (a warp)
            m_Speed = Mathf.Lerp(m_Speed, v, 1f - Mathf.Exp(-6f * dt));
            m_Swing += dt * (3f + m_Speed * 2.5f);
            float amp = Sway * Mathf.Clamp01(m_Speed / 3f);
            for (int i = 0; i < m_Shown && i < Pieces.Length; i++)
            {
                if (Pieces[i] == null) continue;
                var (x, z) = PackSway.Angles(m_Swing, i, amp);
                Pieces[i].transform.localRotation = m_Rest[i] * Quaternion.Euler(x, 0f, z);
            }
        }

        /// <summary>The pack's level now (the backpack upgrade).</summary>
        static int LevelNow(GameManager gm) => Mathf.Clamp(gm.Upgrades.Level(CarUpgrade.Kennel), 0, CarUpgrades.MaxLevelled);

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var stash = gm.Stash;
            int level = LevelNow(gm);
            if (level == m_Level && stash.Version == m_Version) return;
            m_Version = stash.Version;
            Show(level, stash.Columns, stash.Rows, stash.Items);
        }

        /// <summary>
        /// The pack of <paramref name="level"/> with <paramref name="items"/> of a grid <paramref name="columns"/> ×
        /// <paramref name="rows"/> wrapped round it (also the editor's pack shots: AssetBuilder's PackShots).
        /// </summary>
        public void Show(int level, int columns, int rows, IReadOnlyList<InventoryEntry> items)
        {
            if (level != m_Level)
            {
                m_Level = level;
                if (Levels != null)
                    for (int i = 0; i < Levels.Length; i++)
                        if (Levels[i] != null) Levels[i].SetActive(i == level);
                var load = Load(level);
                if (load != null && Pieces != null)
                    foreach (var p in Pieces)
                        if (p != null) p.transform.SetParent(load, false);
            }
            if (Pieces == null || Visuals == null) return;
            var box = Boxes != null && level < Boxes.Length ? Boxes[level] : new Vector3(0.3f, 0.25f, 0.3f);
            var laid = PackWrap.Lay(items, columns, rows, new PackWrap.Box(box.x, box.z, box.y), Thickness);
            for (int i = 0; i < Pieces.Length; i++)
                Show(i, i < items.Count ? items[i] : (InventoryEntry?)null, i < laid.Count ? laid[i] : default);
            m_Shown = Mathf.Min(items.Count, Pieces.Length);
        }

        Transform Load(int level) => Loads != null && Loads.Length > 0 ? Loads[Mathf.Clamp(level, 0, Loads.Length - 1)] : null;

        /// <summary>How a thing's own model is laid against the pack (its thinnest side down, its length as on the grid).</summary>
        public struct Rest
        {
            public Quaternion Turn;
            public Vector3 Offset;
            public float Thick;
        }

        static readonly Dictionary<int, Rest> s_Rests = new();

        /// <summary>How thick a shape lies on the pack, metres (its laid model's height).</summary>
        float Thickness(int shape) => RestOf(shape).Thick;

        /// <summary>
        /// How a shape's own model lies on the pack (cached): the models stand as they do on a floor (a canister upright,
        /// 46 cm tall — it stood out of the pack by its height), so each is turned to lie on its thinnest side, its longest
        /// side along the grid's longer way of the shape (a shovel 1 x 4 along the rows), and moved so its middle is over the
        /// spot and its lowest point on the face. A thing without a model (an old piece) lies as its mesh is.
        /// </summary>
        Rest RestOf(int shape)
        {
            if (s_Rests.TryGetValue(shape, out var rest)) return rest;
            var model = Visuals != null && Visuals.Models != null && shape < Visuals.Models.Length ? Visuals.Models[shape] : null;
            var mesh = Visuals != null && Visuals.Meshes != null && shape < Visuals.Meshes.Length ? Visuals.Meshes[shape] : null;
            s_Rests[shape] = rest = RestOf(shape, model, mesh);
            return rest;
        }

        /// <summary>How a shape lies on the pack: its own model laid flat (<see cref="LieFlat"/>), else its piece's mesh as it is.</summary>
        public static Rest RestOf(int shape, GameObject model, Mesh piece)
        {
            var rest = new Rest { Turn = Quaternion.identity, Offset = Vector3.zero, Thick = 0.06f };
            if (model != null && Bounds(model, Matrix4x4.identity, out var lo, out var hi))
            {
                var size = ItemCatalog.Size(ItemCatalog.Shapes[shape].Cells);
                rest.Turn = LieFlat(hi - lo, size.x, size.y);
                Bounds(model, Matrix4x4.Rotate(rest.Turn), out lo, out hi);
                rest.Offset = new Vector3(-(lo.x + hi.x) * 0.5f, -lo.y, -(lo.z + hi.z) * 0.5f);
                rest.Thick = hi.y - lo.y;
            }
            else if (piece != null) rest.Thick = piece.bounds.size.y;
            rest.Thick = Mathf.Clamp(rest.Thick, 0.01f, 0.3f);
            return rest;
        }

        /// <summary>
        /// Editor (it reads the meshes' vertices, which a build cannot): where a laid thing's strap goes — the slice of the
        /// thing round its middle across its length (a hammer: its thin handle, not its head), its middle and half its
        /// width across and its top there, in the laid thing's space (its middle at the origin, lowest point at y = 0).
        /// </summary>
        public static Vector4 StrapFit(GameObject model, Rest rest)
        {
            if (model == null || !Bounds(model, Matrix4x4.Rotate(rest.Turn), out var lo, out var hi)) return Vector4.zero;
            var size = hi - lo;
            bool alongX = size.x >= size.z;                         // (the strap goes across the length: PackSway.StrapAcross)
            float length = alongX ? size.x : size.z, slab = Mathf.Max(0.012f, length * 0.06f);
            var place = Matrix4x4.TRS(rest.Offset, rest.Turn, Vector3.one) * model.transform.worldToLocalMatrix;
            for (int grow = 0; grow < 6; grow++, slab *= 1.8f)
            {
                float a0 = float.MaxValue, a1 = float.MinValue, top = float.MinValue;
                int n = 0;
                foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = f.sharedMesh;
                    if (m == null) continue; // (the editor reads any mesh's vertices)
                    var to = place * f.transform.localToWorldMatrix;
                    foreach (var v in m.vertices)
                    {
                        var p = to.MultiplyPoint3x4(v);
                        if (Mathf.Abs(alongX ? p.x : p.z) > slab) continue;
                        float across = alongX ? p.z : p.x;
                        a0 = Mathf.Min(a0, across);
                        a1 = Mathf.Max(a1, across);
                        top = Mathf.Max(top, p.y);
                        n++;
                    }
                }
                if (n >= 4) return new Vector4((a0 + a1) * 0.5f, (a1 - a0) * 0.5f, top, 1f);
            }
            return Vector4.zero;
        }

        /// <summary>
        /// The turn that lays a box of extents <paramref name="e"/> (its own axes) on its thinnest side, its longest side along
        /// +Z when the shape is taller than wide on the grid (<paramref name="w"/> × <paramref name="h"/> cells), along +X when
        /// wider (pure). A square shape keeps its longest side where it was, as far as it can.
        /// </summary>
        public static Quaternion LieFlat(Vector3 e, int w, int h)
        {
            int thin = e.x <= e.y && e.x <= e.z ? 0 : e.y <= e.z ? 1 : 2;
            int a = (thin + 1) % 3, b = (thin + 2) % 3;
            int along = e[a] >= e[b] ? a : b;
            var up = Axis(thin);
            var length = Axis(along);
            // where the length goes: the grid's longer way (rows: Z, columns: X); square: Z if it was Z or the thin side was, else X
            bool toZ = h > w || (h == w && (along == 2 || (along == 1 && thin == 0)));
            // the inverse turn takes the world's axes to the model's: Y to its thin axis, the length's target to its long axis
            var inverse = toZ ? Quaternion.LookRotation(length, up) : Quaternion.LookRotation(Vector3.Cross(length, up), up);
            return Quaternion.Inverse(inverse);
        }

        static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        /// <summary>
        /// A thing's strap sized to it (in its piece's space, where the thing lies with its middle at the origin and its
        /// lowest point on y = 0, a third of it in the pack): across its shorter side round its middle — over its top, down
        /// both its sides into the pack — a buckle on the top.
        /// </summary>
        void Fit(Transform strap, int shape, bool own)
        {
            Vector3 size;
            var rest = RestOf(shape);
            if (own && Visuals.Models != null && shape < Visuals.Models.Length && Visuals.Models[shape] != null
                && Bounds(Visuals.Models[shape], Matrix4x4.Rotate(rest.Turn), out var lo, out var hi))
                size = hi - lo;
            else
            {
                var mesh = Visuals.Meshes != null && shape < Visuals.Meshes.Length ? Visuals.Meshes[shape] : null;
                size = mesh != null ? mesh.bounds.size : new Vector3(0.1f, 0.05f, 0.1f);
            }
            var (turn, span) = PackSway.StrapAcross(size);
            // the slice it goes round (the builder's StrapFits: a hammer's handle, not its head), else the whole box
            var fit = own && StrapFits != null && shape < StrapFits.Length && StrapFits[shape].w > 0f ? StrapFits[shape] : new Vector4(0f, span * 0.5f, size.y, 1f);
            var hug = PackSway.Hug(fit, rest.Thick, size.x >= size.z);
            strap.localRotation = turn;
            strap.localPosition = hug.Middle;
            float w = hug.HalfWidth * 2f + 2f * StrapMargin, top = hug.Top, low = hug.Low, tall = top - low;
            Part(strap, "Strap_Top", new Vector3(0f, top + StrapThick * 0.5f, 0f), new Vector3(w, StrapThick, StrapWidth));
            Part(strap, "Strap_SideL", new Vector3(-w * 0.5f, low + tall * 0.5f, 0f), new Vector3(StrapThick, tall + StrapThick, StrapWidth));
            Part(strap, "Strap_SideR", new Vector3(w * 0.5f, low + tall * 0.5f, 0f), new Vector3(StrapThick, tall + StrapThick, StrapWidth));
            var buckle = strap.Find("Metal_Buckle");
            if (buckle != null) buckle.localPosition = new Vector3(0f, top + StrapThick, 0f);
        }

        static void Part(Transform strap, string name, Vector3 at, Vector3 size)
        {
            var t = strap.Find(name);
            if (t == null) return;
            t.localPosition = at;
            t.localScale = size;
        }

        /// <summary>A model's box in its root's space turned by <paramref name="turn"/> (its meshes' corners).</summary>
        static bool Bounds(GameObject model, Matrix4x4 turn, out Vector3 lo, out Vector3 hi)
        {
            lo = Vector3.one * float.MaxValue;
            hi = Vector3.one * float.MinValue;
            var toRoot = model.transform.worldToLocalMatrix;
            bool any = false;
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                var m = turn * toRoot * f.transform.localToWorldMatrix;
                var b = f.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var c = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    lo = Vector3.Min(lo, c);
                    hi = Vector3.Max(hi, c);
                    any = true;
                }
            }
            return any;
        }

        void Show(int i, InventoryEntry? entry, PackWrap.Placed at)
        {
            var filter = Pieces[i];
            if (filter == null) return;
            var renderer = filter.GetComponent<MeshRenderer>();
            var model = entry is InventoryEntry m && Visuals.Models != null && m.Shape < Visuals.Models.Length ? Visuals.Models[m.Shape] : null;
            if (i < m_Models.Length && m_Models[i] != null && (model == null || m_Models[i].name != model.name))
            {
                if (Application.isPlaying) Destroy(m_Models[i]); else DestroyImmediate(m_Models[i]);
                m_Models[i] = null;
            }
            if (model != null && i < m_Models.Length && m_Models[i] == null)
            {
                m_Models[i] = Instantiate(model, filter.transform);
                m_Models[i].name = model.name;
            }
            if (model != null && entry is InventoryEntry laidOut && m_Models[i] != null)
            {
                var rest = RestOf(laidOut.Shape);
                m_Models[i].transform.SetLocalPositionAndRotation(rest.Offset, rest.Turn); // (lying on its thinnest side, on the face)
            }
            bool own = i < m_Models.Length && m_Models[i] != null;
            renderer.enabled = entry != null && !own;
            var strap = Straps != null && i < Straps.Length ? Straps[i] : null;
            if (strap != null && strap.gameObject.activeSelf != (entry != null)) strap.gameObject.SetActive(entry != null);
            if (entry is not InventoryEntry e) return;
            filter.transform.SetLocalPositionAndRotation(at.Position, at.Rotation);
            if (i < m_Rest.Length) m_Rest[i] = at.Rotation;
            if (strap != null) Fit(strap, e.Shape, own);
            filter.transform.localScale = at.Scale;
            filter.sharedMesh = Visuals.Meshes[Mathf.Clamp(e.Shape, 0, Visuals.Meshes.Length - 1)];
            renderer.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(e.Color, 0, Visuals.ColorMaterials.Length - 1)];
            VisualStyles.Dress(filter.gameObject);
        }
    }
}
