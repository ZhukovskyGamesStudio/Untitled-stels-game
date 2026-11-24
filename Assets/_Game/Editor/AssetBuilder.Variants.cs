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

        /// <summary>
        /// What a snack machine sells (Tools/Blender/machines.py vend_*): five spirals on each of five shelves, bottles at the
        /// bottom, then cans, bars, bags; every spiral one of three goods, or sold out — another machine, other goods.
        /// </summary>
        static void VendingGoods(GameObject root, GameObject model)
        {
            if (model == null || !HasModel("vend_bag_0")) return;
            // (the script's SHELVES, COLUMNS and FRONT; importing turns Blender's X and Y round)
            var shelves = new[] { (0.475f, "bottle"), (0.735f, "can"), (0.985f, "bar"), (1.225f, "bag"), (1.455f, "bag") };
            const float first = -0.41f + 0.0615f, step = 0.123f, front = 0.335f;
            var v = root.AddComponent<PropVariants>();
            v.Empty = 0.16f;
            var slots = new List<Transform>();
            for (int s = 0; s < shelves.Length; s++)
                for (int c = 0; c < 5; c++)
                {
                    var slot = new GameObject($"Spiral{s}{c}").transform;
                    slot.SetParent(model.transform, false);
                    slot.localPosition = new Vector3(-(first + step * c), shelves[s].Item1 + 0.002f, front);
                    for (int k = 0; k < 3; k++)
                    {
                        var goods = Model($"vend_{shelves[s].Item2}_{(s * 5 + c * 3 + k * 2 + (s == 4 ? 1 : 0)) % 8}");
                        goods.name = "Goods";
                        goods.transform.SetParent(slot, false);
                        foreach (var r in goods.GetComponentsInChildren<MeshRenderer>())
                        {
                            r.sharedMaterial = M("MachineLit"); // (one mesh a file: Unity names it after the file)
                            r.shadowCastingMode = ShadowCastingMode.Off;
                        }
                        goods.SetActive(false);
                    }
                    slots.Add(slot);
                }
            v.Slots = slots.ToArray();
        }

        /// <summary>
        /// The books of a library trolley (Tools/Blender/rooms2_fun.py book_cart: three shelves, books back to back on both
        /// sides): each side of each shelf a full row, a thinned-out row with books fallen over, a heap lying flat, a row that
        /// has been in a fire, or nothing. The model's own plain books step aside.
        /// </summary>
        static void CartBooks(GameObject root)
        {
            if (!BookModels.All(HasModel) || !HasModel("book_stack")) return;
            var v = root.AddComponent<PropVariants>();
            v.Empty = 0.1f;
            v.PerPart = true;
            v.Palette = Palette(BookColours);
            v.AccentPalette = Palette(new[] { "BookPages" });
            v.Own = root.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Trim")).ToArray();
            foreach (var r in v.Own)
            {
                r.enabled = false;
                foreach (var c in r.GetComponents<Collider>()) Object.DestroyImmediate(c);
            }
            var books = BookModels.Select(f => (file: f, b: ModelBounds(f))).ToList();
            var stack = ModelBounds("book_stack");
            var rng = new System.Random(5207);
            var slots = new List<Transform>();
            float[] tops = { 0.141f, 0.471f, 0.801f };
            const float x0 = -0.35f, x1 = 0.35f, edge = 0.172f;
            for (int s = 0; s < tops.Length; s++)
                foreach (float side in new[] { 1f, -1f })
                {
                    float clear = s < 2 ? 0.27f : 0.3f;
                    var slot = new GameObject($"Side{slots.Count}").transform;
                    slot.SetParent(root.transform, false);
                    slot.localPosition = new Vector3(0f, tops[s], 0f);
                    slot.localRotation = Quaternion.Euler(0f, side > 0f ? 0f : 180f, 0f);
                    for (int row = 0; row < 6; row++) // (full, full, thinned, thinned, a heap — and now and then one that burnt)
                    {
                        var holder = new GameObject($"Row{row}").transform;
                        holder.SetParent(slot, false);
                        bool burnt = row == 5 && s == 1, thin = row is 2 or 3 || (row == 5 && s != 1), heap = row == 4;
                        float x = x0 + (float)rng.NextDouble() * 0.04f;
                        void Put(string file, Bounds b, float lean = 0f, float yaw = 0f, float lift = 0f)
                        {
                            var go = Model(file);
                            go.name = "Book";
                            go.transform.SetParent(holder, false);
                            go.transform.localPosition = new Vector3(x + b.size.x / 2f, lift, edge - 0.012f - b.max.z);
                            go.transform.localRotation = Quaternion.Euler(0f, yaw, lean);
                            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                            {
                                r.shadowCastingMode = ShadowCastingMode.Off;
                                if (!burnt) continue;
                                r.gameObject.name = "Fixed" + r.gameObject.name; // charred: no colour of the palette
                                r.sharedMaterial = M("BookCharred");
                            }
                        }
                        while (x < x1 - 0.05f)
                        {
                            double roll = rng.NextDouble();
                            if (heap)
                            {
                                if (stack.size.x > 0f && x + stack.size.x < x1) { Put("book_stack", stack, 0f, (float)(rng.NextDouble() * 30 - 15)); x += stack.size.x + 0.03f + (float)rng.NextDouble() * 0.08f; }
                                else break;
                                continue;
                            }
                            if ((thin && roll < 0.4) || (burnt && roll < 0.2)) { x += 0.05f + (float)rng.NextDouble() * 0.09f; continue; }
                            var fits = books.Where(k => k.b.size.y < clear && x + k.b.size.x < x1).ToList();
                            if (fits.Count == 0) break;
                            var pick = fits[rng.Next(fits.Count)];
                            // a book fallen against its neighbour now and then; a burnt row has buckled
                            float lean = (thin && roll > 0.8) || (burnt && roll > 0.55) ? -(8f + (float)rng.NextDouble() * 14f) : 0f;
                            Put(pick.file, pick.b, lean, burnt ? (float)(rng.NextDouble() * 10 - 5) : 0f);
                            x += pick.b.size.x + 0.002f + (lean != 0f ? pick.b.size.y * 0.3f : 0f);
                        }
                        holder.gameObject.SetActive(false);
                    }
                    slots.Add(slot);
                }
            v.Slots = slots.ToArray();
        }

        /// <summary>
        /// What stands in the cubes of the low shelf (Tools/Blender/low_shelf.py, the books of variants.py): archive boxes, a
        /// basket, towels, a vase, a row of books, a stack of them, or nothing — two shelves never look the same.
        /// </summary>
        static void CubeThings(GameObject root)
        {
            if (!new[] { "cube_box_0", "cube_box_1", "cube_towels", "cube_vase", "cube_basket" }.All(HasModel)) return;
            const float plinth = 0.07f, board = 0.026f, side = 0.034f, wide = 0.9f, inside = 1.36f - 0.042f - plinth, step = inside / 3f;
            const float cell = (wide - 2f * side - board) / 2f, cx = (board + cell) / 2f + 0.002f, front = 0.46f / 2f - 0.03f;
            var v = root.AddComponent<PropVariants>();
            v.Empty = 0.12f;
            var slots = new List<Transform>();
            var rng = new System.Random(1607);
            void Add(Transform slot, string file, string body, string accent)
            {
                var go = Model(file);
                go.name = "Thing";
                go.transform.SetParent(slot, false);
                Paint(go, body, accent, body);
                MachinePaint(go);
                go.SetActive(false);
            }
            for (int row = 0; row < 3; row++)
                foreach (float x in new[] { -cx, cx })
                {
                    var slot = new GameObject($"Cube{slots.Count}").transform;
                    slot.SetParent(root.transform, false);
                    slot.localPosition = new Vector3(x, plinth + row * step + (row == 0 ? board : board / 2f) - 0.001f, front);
                    Add(slot, "cube_box_0", "Cardboard", "Metal");
                    Add(slot, "cube_box_1", "Cardboard", "Metal");
                    Add(slot, "cube_towels", row == 1 ? "ClothBeige" : "ClothGrey", "Metal");
                    Add(slot, "cube_vase", new[] { "VaseGreen", "VaseCream", "VaseBlue" }[slots.Count % 3], "Metal");
                    Add(slot, "cube_basket", "Wicker", "Wicker");
                    if (BookModels.All(HasModel))
                    {
                        var books = new GameObject("Books").transform;
                        books.SetParent(slot, false);
                        for (float at = -cell / 2f + 0.02f; ;)
                        {
                            string file = BookModels[rng.Next(BookModels.Length)];
                            var b = ModelBounds(file);
                            if (at + b.size.x > cell / 2f - 0.02f - (float)rng.NextDouble() * 0.1f) break;
                            if (b.size.y > step - 0.05f) continue;
                            var book = Model(file);
                            book.name = "Book";
                            book.transform.SetParent(books, false);
                            book.transform.localPosition = new Vector3(at + b.size.x / 2f, 0f, -0.02f - b.max.z);
                            Paint(book, BookColours[rng.Next(BookColours.Length)], "BookPages", "BookPages");
                            foreach (var r in book.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                            at += b.size.x + 0.002f;
                        }
                        books.gameObject.SetActive(false);
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
