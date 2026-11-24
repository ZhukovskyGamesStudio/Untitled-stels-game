using System.Collections.Generic;
using System.Linq;
using Stealth.Level;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    /// <summary>
    /// Furniture dressed differently every time (<see cref="PropVariants"/>): the shelves get rows of books (Tools/Blender/
    /// variants.py book_*) in many colours, laid out here; the coat stand and the hall hooks get garments on their hooks.
    /// </summary>
    public static partial class AssetBuilder
    {
        static readonly string[] BookModels = { "book_0", "book_1", "book_2", "book_3", "book_4", "book_5" };
        static readonly string[] BookColours = { "BookRed", "BookBlue", "BookGreen", "BookOchre", "BookBrown", "BookBlack", "BookCream", "BookTeal" };
        static readonly string[] ClothColours = { "ClothNavy", "ClothGrey", "ClothBeige", "ClothBlack", "ClothRed", "ClothOlive", "ClothBrown", "ClothDenim" };
        static readonly string[] GarmentModels = { "garment_coat_long", "garment_jacket", "garment_hoodie", "garment_raincoat", "garment_scarf", "garment_bag", "garment_umbrella", "garment_cap" };

        static Material[] Palette(string[] names) => names.Select(M).Where(m => m != null).ToArray();

        /// <summary>
        /// The shelves of a bookcase (its model's Body: boards facing up across its inside), from its own mesh: the height of
        /// each board's top, the clear height over it to the next, the inside's width and depth, in the piece's frame.
        /// </summary>
        static List<(float y, float clear, float x0, float x1, float z0, float z1)> Shelves(GameObject root)
        {
            var tops = new List<(float y, float x0, float x1, float z0, float z1)>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.name.StartsWith("Body") || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh;
                var v = m.vertices;
                var t = m.triangles;
                var toRoot = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var faces = new Dictionary<int, (float x0, float x1, float z0, float z1)>();
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = toRoot.MultiplyPoint3x4(v[t[i]]), b = toRoot.MultiplyPoint3x4(v[t[i + 1]]), c = toRoot.MultiplyPoint3x4(v[t[i + 2]]);
                    var n = Vector3.Cross(b - a, c - a).normalized;
                    if (n.y < 0.95f) continue;
                    int key = Mathf.RoundToInt(a.y * 100f);
                    var box = (Mathf.Min(a.x, b.x, c.x), Mathf.Max(a.x, b.x, c.x), Mathf.Min(a.z, b.z, c.z), Mathf.Max(a.z, b.z, c.z));
                    faces[key] = faces.TryGetValue(key, out var f) ? (Mathf.Min(f.x0, box.Item1), Mathf.Max(f.x1, box.Item2), Mathf.Min(f.z0, box.Item3), Mathf.Max(f.z1, box.Item4)) : box;
                }
                foreach (var kv in faces) tops.Add((kv.Key / 100f, kv.Value.x0, kv.Value.x1, kv.Value.z0, kv.Value.z1));
            }
            tops.Sort((p, q) => p.y.CompareTo(q.y));
            float wide = tops.Count > 0 ? tops.Max(s => s.x1 - s.x0) : 0f;
            var boards = tops.Where(s => s.x1 - s.x0 > wide * 0.8f && s.z1 - s.z0 > 0.18f).ToList();
            var list = new List<(float, float, float, float, float, float)>();
            for (int i = 0; i + 1 < boards.Count; i++)
            {
                var s = boards[i];
                float clear = boards[i + 1].y - s.y - 0.04f; // the next board's underside
                if (clear < 0.2f) continue;
                list.Add((s.y, clear, s.x0 + 0.03f, s.x1 - 0.03f, s.z0, s.z1));
            }
            return list;
        }

        /// <summary>
        /// Rows of books on every shelf of a bookcase prefab (<see cref="PropVariants"/>, four rows a shelf to pick from, each
        /// book its own colour): upright books of mixed sizes spines out, now and then one leaning at the row's end, a gap, a
        /// stack lying flat. The model's own folders/books (Accent) stay for a style that has a bookcase of its own.
        /// </summary>
        static void ShelfBooks(GameObject root)
        {
            if (!BookModels.All(HasModel)) { Debug.LogWarning("[Builder] no book models yet (Tools/Blender/variants.py): the shelves keep their own"); return; }
            var shelves = Shelves(root);
            if (shelves.Count == 0) return;
            var books = BookModels.Select(f => (file: f, b: ModelBounds(f))).ToList();
            var lean = HasModel("book_lean") ? ModelBounds("book_lean") : default;
            var stack = HasModel("book_stack") ? ModelBounds("book_stack") : default;
            var v = root.AddComponent<PropVariants>();
            v.Empty = 0.08f;
            v.PerPart = true;
            v.Palette = Palette(BookColours);
            v.AccentPalette = Palette(new[] { "BookPages" });
            v.Frame = root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name.StartsWith("Body"));
            v.Own = root.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Accent") || r.name.StartsWith("Trim")).ToArray();
            var slots = new List<Transform>();
            var rng = new System.Random(root.name.GetHashCode() & 0x7fffffff);
            for (int s = 0; s < shelves.Count; s++)
            {
                var (y, clear, x0, x1, z0, z1) = shelves[s];
                var slot = new GameObject($"Shelf{s}").transform;
                slot.SetParent(root.transform, false);
                for (int row = 0; row < 4; row++)
                {
                    var holder = new GameObject($"Row{row}").transform;
                    holder.SetParent(slot, false);
                    float x = x0 + (float)rng.NextDouble() * 0.05f;
                    void Put(string file, Bounds b, float yaw = 0f)
                    {
                        var go = Model(file);
                        go.name = "Book";
                        go.transform.SetParent(holder, false);
                        float w = b.size.x;
                        // spine to the front: the book's front (+Z) at the shelf's front edge, a couple of centimetres in
                        go.transform.localPosition = new Vector3(x + w / 2f, y, z1 - 0.02f - b.max.z);
                        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                        x += w + 0.002f;
                    }
                    while (x < x1 - 0.06f)
                    {
                        double roll = rng.NextDouble();
                        if (roll < 0.06 && stack.size.x > 0f && x + stack.size.x < x1 && stack.size.y < clear) { Put("book_stack", stack); x += 0.02f; continue; }
                        if (roll < 0.12) { x += 0.04f + (float)rng.NextDouble() * 0.1f; continue; } // a gap
                        var fits = books.Where(k => k.b.size.y < clear - 0.01f && k.b.size.z < z1 - z0 && x + k.b.size.x < x1).ToList();
                        if (fits.Count == 0) break;
                        var pick = fits[rng.Next(fits.Count)];
                        Put(pick.file, pick.b);
                    }
                    if (lean.size.x > 0f && rng.NextDouble() < 0.5 && lean.size.y < clear && x + lean.size.x < x1) Put("book_lean", lean);
                }
                slots.Add(slot);
            }
            v.Slots = slots.ToArray();
        }

        [System.Serializable] class HookSpec { public string tier; public float[] point; public float[] dir; }
        [System.Serializable] class HolderSpec { public HookSpec[] hooks; }
        [System.Serializable] class Holders { public HolderSpec coat_stand_bare, hall_hooks_bare; }
        [System.Serializable] class VariantContract { public Holders holders; }

        /// <summary>The hooks of a holder model (Tools/Blender/variants_contract.json: Unity axes, metres, from its origin).</summary>
        static HookSpec[] HooksOf(string model)
        {
            var path = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "Blender", "variants_contract.json");
            if (!System.IO.File.Exists(path)) return new HookSpec[0];
            var c = JsonUtility.FromJson<VariantContract>(System.IO.File.ReadAllText(path));
            var h = model == "coat_stand_bare" ? c?.holders?.coat_stand_bare : c?.holders?.hall_hooks_bare;
            return h?.hooks ?? new HookSpec[0];
        }

        /// <summary>
        /// Garments on a coat stand's or a hall rail's hooks (<see cref="PropVariants"/>: each hook one of the garments that fit
        /// it, or nothing, in one of the cloth colours). Long coats only where nothing hangs beside them (every other arm of the
        /// stand, the rail's middle and ends), small things (a scarf, a bag, an umbrella, a cap) anywhere; a rail's upper hooks
        /// a cap at most (whatever hangs below covers them); nothing on the rail reaches the cupboard under it.
        /// </summary>
        static void HookGarments(GameObject root, string holder)
        {
            if (!GarmentModels.All(HasModel)) { Debug.LogWarning("[Builder] no garment models yet (Tools/Blender/variants.py)"); return; }
            var hooks = HooksOf(holder);
            if (hooks.Length == 0) return;
            bool stand = holder == "coat_stand_bare";
            var v = root.AddComponent<PropVariants>();
            v.Empty = stand ? 0.4f : 0.3f;
            v.Palette = Palette(ClothColours);
            var slots = new List<Transform>();
            int upper = 0, lower = 0;
            for (int i = 0; i < hooks.Length; i++)
            {
                var h = hooks[i];
                var at = new Vector3(h.point[0], h.point[1], h.point[2]);
                var dir = new Vector3(h.dir[0], h.dir[1], h.dir[2]);
                string[] fit;
                if (stand)
                    fit = h.tier == "upper"
                        ? (upper++ % 2 == 0 ? new[] { "garment_coat_long", "garment_jacket", "garment_hoodie", "garment_raincoat", "garment_scarf" } : new[] { "garment_scarf", "garment_cap" })
                        : new[] { "garment_bag", "garment_umbrella", "garment_cap" };
                else if (h.tier == "upper") fit = new[] { "garment_cap" };
                else fit = lower++ % 2 == 0 ? new[] { "garment_jacket", "garment_hoodie", "garment_scarf", "garment_bag" } : new[] { "garment_scarf", "garment_cap", "garment_bag" };
                var slot = new GameObject($"Hook{i}").transform;
                slot.SetParent(root.transform, false);
                slot.localPosition = at;
                slot.localRotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward);
                foreach (var file in fit)
                {
                    var g = Model(file);
                    g.name = file;
                    g.transform.SetParent(slot, false);
                    Paint(g, "ClothGrey", "Metal", "Metal");
                    g.SetActive(false);
                }
                slots.Add(slot);
            }
            v.Slots = slots.ToArray();
        }

        /// <summary>A model's bounds in its own frame (origin at its bottom middle for the variants).</summary>
        static Bounds ModelBounds(string file)
        {
            var go = Model(file);
            try
            {
                var b = new Bounds();
                bool any = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                return b;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
