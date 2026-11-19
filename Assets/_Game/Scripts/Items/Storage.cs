using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>
    /// Furniture that opens for real (Props/Cabinet, Props/Lockers and the themed rooms' cupboards: wall cabinet, medicine
    /// cabinet, parts cabinet, mail cabinet): drawers slide out, doors swing open on their hinges — every part on its own
    /// (look at it, press E). The parts are the model's
    /// children named <c>BodyDrawer{n}</c> / <c>BodyDoor{n}</c>; where they move is worked out from their meshes, so every
    /// model family works. The server keeps which parts are open (<see cref="World"/>, replicated with the house's
    /// opened set) and what loot is hidden behind them; every peer only animates. Moving parts stay out of the merged
    /// furniture (they carry a property block).
    /// </summary>
    public class Storage : MonoBehaviour
    {
        public static readonly List<Storage> All = new();


        /// <summary>Index in <see cref="LevelLayout.Props"/>: the same on every peer, names the furniture in requests.</summary>
        public int Index { get; private set; } = -1;

        public int PartCount => m_Parts.Count;
        /// <summary>Drawers (they slide) or doors (they swing).</summary>
        public bool Drawers { get; private set; }
        readonly List<Part> m_Parts = new();
        float m_Depth = 0.46f;  // inside, front to back (from the whole piece's bounds)
        static readonly int MovingId = Shader.PropertyToID("_StorageMoving");

        sealed class Part
        {
            public Transform T;
            public Vector3 ClosedPos;
            public Quaternion ClosedRot;
            public Vector3 Hinge;       // doors: the vertical axis at the front edge, world
            public bool RightHinge;     // doors: the right leaf of a pair, hinged on its right edge, swings the other way
            public OpenTween Tween;     // 0 closed .. 1 open (shown)
            public bool Posed;          // shown at least once (the first step snaps)
            public Bounds Closed;       // world bounds when closed
            public Collider[] Colliders;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Awake() => FindParts();

        void FindParts()
        {
            if (m_Parts.Count > 0) return;
            var found = new SortedDictionary<int, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                bool drawer = n.StartsWith("BodyDrawer"), door = n.StartsWith("BodyDoor");
                if (!drawer && !door) continue;
                if (!int.TryParse(n.Substring(drawer ? 10 : 8), out int k) || found.ContainsKey(k)) continue;
                found[k] = t;
                Drawers = drawer;
            }
            foreach (var t in found.Values) m_Parts.Add(new Part { T = t, Colliders = t.GetComponentsInChildren<Collider>(true) });
        }

        /// <summary>Placed in a house: remembers where its parts sit closed (furniture never moves afterwards).</summary>
        public void Configure(int index)
        {
            Index = index;
            FindParts();
            var body = WorldBounds(transform);
            var fwd = transform.forward;
            m_Depth = Mathf.Max(0.1f, Mathf.Abs(body.size.x * fwd.x) + Mathf.Abs(body.size.z * fwd.z) - 0.06f);
            var block = new MaterialPropertyBlock();
            var closed = new List<Bounds>();
            foreach (var p in m_Parts) closed.Add(WorldBounds(p.T));
            var rightHinged = StorageMath.RightHinged(closed, transform.right);
            for (int i = 0; i < m_Parts.Count; i++)
            {
                var p = m_Parts[i];
                p.ClosedPos = p.T.position;
                p.ClosedRot = p.T.rotation;
                p.Closed = closed[i];
                p.RightHinge = !Drawers && rightHinged[i];
                p.Hinge = StorageMath.Hinge(p.Closed, transform.right, transform.forward, p.RightHinge);
                p.Tween = default;
                p.Posed = false;
                foreach (var r in p.T.GetComponentsInChildren<Renderer>(true))
                {
                    r.GetPropertyBlock(block);
                    block.SetFloat(MovingId, 1f); // a property block keeps the part out of the merged furniture: it moves
                    r.SetPropertyBlock(block);
                }
            }
        }

        void Update()
        {
            if (Index < 0) return;
            for (int i = 0; i < m_Parts.Count; i++)
            {
                var p = m_Parts[i];
                float was = p.Tween.Value;
                p.Tween.Step(World.IsOpened(OpenKeys.Storage(Index, i)), Time.deltaTime, StorageMath.OpenSeconds);
                if (p.Posed && p.Tween.Value == was) continue;
                p.Posed = true;
                Pose(p, p.Tween.Smooth);
            }
        }

        void Pose(Part p, float t)
        {
            if (Drawers)
            {
                p.T.SetPositionAndRotation(p.ClosedPos + transform.forward * (StorageMath.DrawerTravel * t), p.ClosedRot);
                return;
            }
            // outwards: a left-hinged door's free edge (its right) swings to the front, a right-hinged one's (its left) too
            var turn = Quaternion.AngleAxis((p.RightHinge ? 1f : -1f) * StorageMath.DoorAngle * t, transform.up);
            p.T.SetPositionAndRotation(p.Hinge + turn * (p.ClosedPos - p.Hinge), turn * p.ClosedRot);
        }

        public bool IsOpen(int part) => Index >= 0 && World.IsOpened(OpenKeys.Storage(Index, part));

        /// <summary>The part a collider belongs to; the carcass itself gives the part closest to <paramref name="point"/>.</summary>
        public int PartOf(Collider c, Vector3 point)
        {
            for (int i = 0; i < m_Parts.Count; i++)
                foreach (var pc in m_Parts[i].Colliders)
                    if (pc == c) return i;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < m_Parts.Count; i++)
            {
                float d = (m_Parts[i].Closed.ClosestPoint(point) - point).sqrMagnitude;
                if (d < bestD) { best = i; bestD = d; }
            }
            return best;
        }

        /// <summary>Where hidden loot appears when a part opens: in the pulled-out drawer, or at the bottom of the locker.</summary>
        public Vector3 LootSpot(int part)
        {
            var b = m_Parts[Mathf.Clamp(part, 0, m_Parts.Count - 1)].Closed;
            return StorageMath.LootSpot(b, transform.forward, transform.position.y, Drawers);
        }

        /// <summary>Where and how an item of this shape is put behind a part so it fits inside (see <see cref="StorageMath.LootPose"/>).</summary>
        public (Vector3 pos, Quaternion rot) LootPose(int part, int shape)
        {
            var b = m_Parts[Mathf.Clamp(part, 0, m_Parts.Count - 1)].Closed;
            return StorageMath.LootPose(ItemCatalog.ModelSize(shape), b, transform.forward, m_Depth, Drawers);
        }

        /// <summary>An item of this shape fits behind this part (a shovel stands only in a tall compartment).</summary>
        public bool Fits(int part, int shape) =>
            part >= 0 && part < m_Parts.Count && StorageMath.Fits(ItemCatalog.ModelSize(shape), m_Parts[part].Closed, transform.forward, m_Depth, Drawers);

        static Bounds WorldBounds(Transform t)
        {
            bool any = false;
            var b = new Bounds(t.position, Vector3.zero);
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var c = mf.transform.TransformPoint(m.center + Vector3.Scale(m.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(c, Vector3.zero); any = true; }
                    else b.Encapsulate(c);
                }
            }
            return b;
        }
    }

    /// <summary>How storage parts move and where hidden loot goes (pure, unit tested).</summary>
    public static class StorageMath
    {
        /// <summary>Seconds to open or close a part.</summary>
        public const float OpenSeconds = 0.35f;
        /// <summary>How far a drawer comes out, metres.</summary>
        public const float DrawerTravel = 0.32f;
        /// <summary>How far a locker door swings, degrees.</summary>
        public const float DoorAngle = 105f;

        /// <summary>
        /// The hinge of a door: the front edge on its left (looking at the furniture from the front, <paramref name="forward"/>
        /// pointing at the viewer) — or on its right for the right leaf of a pair — from the door's world bounds.
        /// </summary>
        public static Vector3 Hinge(Bounds door, Vector3 right, Vector3 forward, bool rightHinge = false)
        {
            right.y = 0; forward.y = 0;
            right.Normalize(); forward.Normalize();
            float halfW = Mathf.Abs(Vector3.Dot(door.extents, Abs(right)));
            float halfD = Mathf.Abs(Vector3.Dot(door.extents, Abs(forward)));
            return door.center + right * (rightHinge ? halfW : -halfW) + forward * halfD;
        }

        /// <summary>
        /// Which doors are the right leaves of pairs (pure): doors at the same height (overlapping over half the shorter one)
        /// form a row; in a row read left to right, every second door of a pair of neighbours (nearly touching) is hinged on
        /// its right, so a two-door cupboard opens from the middle. A lone door, or the odd one out, keeps its hinge left.
        /// </summary>
        public static bool[] RightHinged(IReadOnlyList<Bounds> doors, Vector3 right)
        {
            right.y = 0f;
            right.Normalize();
            var result = new bool[doors.Count];
            var used = new bool[doors.Count];
            for (int i = 0; i < doors.Count; i++)
            {
                if (used[i]) continue;
                var row = new List<int>();
                for (int j = 0; j < doors.Count; j++)
                {
                    float overlap = Mathf.Min(doors[i].max.y, doors[j].max.y) - Mathf.Max(doors[i].min.y, doors[j].min.y);
                    if (!used[j] && overlap > 0.5f * Mathf.Min(doors[i].size.y, doors[j].size.y)) { row.Add(j); used[j] = true; }
                }
                row.Sort((a, b) => Vector3.Dot(doors[a].center, right).CompareTo(Vector3.Dot(doors[b].center, right)));
                for (int k = 0; k + 1 < row.Count; k += 2)
                {
                    var a = doors[row[k]];
                    var b = doors[row[k + 1]];
                    float aRight = Vector3.Dot(a.center, right) + Mathf.Abs(Vector3.Dot(a.extents, Abs(right)));
                    float bLeft = Vector3.Dot(b.center, right) - Mathf.Abs(Vector3.Dot(b.extents, Abs(right)));
                    if (bLeft - aRight < 0.06f) result[row[k + 1]] = true; // neighbours: a pair opening from the middle
                }
            }
            return result;
        }

        /// <summary>
        /// Where loot hidden behind a part appears: in a drawer — on its bottom where it is pulled out to; in a locker — on
        /// the floor of the compartment behind the door.
        /// </summary>
        /// <summary>Size of one cell of a loot item, metres (Tools/Blender/generate_models.py ITEM_CELL).</summary>
        public const float ItemCell = 0.22f;
        /// <summary>Height of a locker's plinth: the compartment floor.</summary>
        public const float LockerFloor = 0.06f;

        /// <summary>
        /// How an item (its cells; the model is centred on them in X/Z, one cell thick in +Y) is put behind a part so it
        /// fits (pure). In a drawer it lies flat, its long side along the drawer's width. In a locker it stands on end on
        /// the compartment floor: the long side up, the short side across the width, the thickness front to back.
        /// </summary>
        public static (Vector3 pos, Quaternion rot) LootPose(Vector2Int[] cells, Bounds part, Vector3 forward, float depth, bool drawer)
        {
            var size = ItemCatalog.Size(cells);
            return LootPose(new Vector3(size.x * ItemCell, ItemCell, size.y * ItemCell), part, forward, depth, drawer);
        }

        /// <summary>
        /// An item of this model size fits behind a part (pure): lying in a drawer — its length across the drawer's width,
        /// its width front to back; on end behind a door — its length up the door's height, its width across the door,
        /// its thickness within the compartment's depth.
        /// </summary>
        public static bool Fits(Vector3 size, Bounds part, Vector3 forward, float depth, bool drawer)
        {
            forward.y = 0;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            float width = Mathf.Abs(part.size.x * right.x) + Mathf.Abs(part.size.z * right.z);
            float along = Mathf.Max(size.x, size.z), across = Mathf.Min(size.x, size.z);
            if (drawer) return along <= width - 0.04f && across <= depth;
            return along <= part.size.y - 0.04f && across <= width - 0.02f && size.y <= depth;
        }

        /// <summary>
        /// As above, for a model of this size (x its length, y its thickness, z its width: <see cref="ItemCatalog.ModelSize"/>);
        /// <paramref name="depth"/> is the compartment's depth behind a door (the item stands on the compartment's floor,
        /// at the bottom of the door, in its middle).
        /// </summary>
        public static (Vector3 pos, Quaternion rot) LootPose(Vector3 size, Bounds part, Vector3 forward, float depth, bool drawer)
        {
            forward.y = 0;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            bool alongX = size.x >= size.z;                     // the long side is the model's X (else its Z)
            float along = Mathf.Max(size.x, size.z);
            float thickness = size.y;
            if (drawer)
            {
                var c = part.center + forward * DrawerTravel;
                var rot = alongX ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.LookRotation(right, Vector3.up);
                return (new Vector3(c.x, part.min.y + 0.04f, c.z), rot);
            }
            // a locker: the long side up; the model's Y (its thickness) points to the front or the back
            var stand = alongX ? Quaternion.LookRotation(right, forward) : Quaternion.LookRotation(Vector3.up, -forward);
            var thick = stand * Vector3.up;
            var mid = part.center - forward * (depth / 2f + 0.01f);
            var pos = new Vector3(mid.x, part.min.y + 0.01f + along / 2f, mid.z) - thick * (thickness / 2f);
            return (pos, stand);
        }

        public static Vector3 LootSpot(Bounds part, Vector3 forward, float floorY, bool drawer)
        {
            forward.y = 0;
            forward.Normalize();
            if (drawer)
            {
                var c = part.center + forward * (DrawerTravel * 0.85f);
                return new Vector3(c.x, part.min.y + 0.08f, c.z);
            }
            var back = part.center - forward * 0.22f;
            return new Vector3(back.x, floorY + 0.14f, back.z);
        }

        /// <summary>
        /// Should this loot item hide in furniture, and behind which part: with <paramref name="chance"/>, a random free part
        /// (one item per part) is taken out of <paramref name="free"/> and returned; otherwise (or with none free) −1.
        /// </summary>
        public static int Hide(List<int> free, System.Random rng, float chance, System.Func<int, bool> fits = null)
        {
            if (free.Count == 0 || rng.NextDouble() >= chance) return -1;
            var ok = fits == null ? free : free.FindAll(k => fits(k));
            if (ok.Count == 0) return -1;
            int key = ok[rng.Next(ok.Count)];
            free.Remove(key);
            return key;
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
