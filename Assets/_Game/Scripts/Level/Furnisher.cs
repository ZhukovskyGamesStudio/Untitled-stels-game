using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Furniture for rooms of any size and shape (pure, part of <see cref="LevelGenerator"/>). Works on the plan's raster
    /// and the exact footprints of the models: groups stand along the walls (backs to the wall, clear of doorways,
    /// stairs and windows' sills), islands fill the middle of big rooms with aisles around them, small things take
    /// the free corners. Every room type has its own groups (after office planning guides: back-to-back desk
    /// clusters, storage walls, a kitchenette with tables, a lounge corner, cold aisles between racks).
    /// Loot spots ride on the furniture (desk tops, shelves, counters).
    /// </summary>
    public sealed class Furnisher
    {
        /// <summary>Footprints of the models in their own frame: x = local X, y = local Z (front = +Z), metres.</summary>
        static readonly Dictionary<PropKind, Rect> Foot = new()
        {
            [PropKind.Desk] = Rect.MinMaxRect(-0.8f, -0.4f, 0.8f, 0.4f),
            [PropKind.Terminal] = Rect.MinMaxRect(-0.8f, -0.4f, 0.8f, 0.4f),
            [PropKind.Chair] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.ServerRack] = Rect.MinMaxRect(-0.35f, -0.5f, 0.35f, 0.52f),
            [PropKind.Cabinet] = Rect.MinMaxRect(-0.45f, -0.25f, 0.45f, 0.28f),
            [PropKind.MeetingTable] = Rect.MinMaxRect(-1.6f, -0.65f, 1.6f, 0.65f),
            [PropKind.Plant] = Rect.MinMaxRect(-0.35f, -0.35f, 0.35f, 0.35f),
            [PropKind.KitchenCounter] = Rect.MinMaxRect(-1.22f, -0.35f, 1.22f, 0.35f),
            [PropKind.Shelf] = Rect.MinMaxRect(-1.0f, -0.3f, 1.0f, 0.3f),
            [PropKind.Boxes] = Rect.MinMaxRect(-0.78f, -0.33f, 0.3f, 0.3f), // Unity mirrors X on import: the stack leans to −X
            [PropKind.WaterCooler] = Rect.MinMaxRect(-0.17f, -0.17f, 0.17f, 0.17f),
            [PropKind.Whiteboard] = Rect.MinMaxRect(-0.95f, -0.08f, 0.95f, 0.08f),
            [PropKind.Sofa] = Rect.MinMaxRect(-1.1f, -0.43f, 1.1f, 0.43f),
            [PropKind.ToiletStall] = Rect.MinMaxRect(-0.53f, -0.78f, 0.53f, 0.75f),
            [PropKind.Partition] = Rect.MinMaxRect(-0.81f, -0.03f, 0.81f, 0.03f),
            [PropKind.Bookshelf] = Rect.MinMaxRect(-0.5f, -0.18f, 0.5f, 0.18f),
            [PropKind.Printer] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.5f),
            [PropKind.Fridge] = Rect.MinMaxRect(-0.35f, -0.35f, 0.35f, 0.4f),
            [PropKind.CoffeeMachine] = Rect.MinMaxRect(-0.17f, -0.2f, 0.17f, 0.24f),
            [PropKind.RoundTable] = Rect.MinMaxRect(-0.5f, -0.5f, 0.5f, 0.5f),
            [PropKind.Armchair] = Rect.MinMaxRect(-0.44f, -0.38f, 0.44f, 0.38f),
            [PropKind.CoffeeTable] = Rect.MinMaxRect(-0.5f, -0.28f, 0.5f, 0.28f),
            [PropKind.TvStand] = Rect.MinMaxRect(-0.7f, -0.2f, 0.7f, 0.2f),
            [PropKind.ReceptionDesk] = Rect.MinMaxRect(-1.25f, -0.4f, 1.25f, 0.48f),
            [PropKind.TrashBin] = Rect.MinMaxRect(-0.19f, -0.2f, 0.19f, 0.2f),
            [PropKind.CoatRack] = Rect.MinMaxRect(-0.29f, -0.26f, 0.26f, 0.26f),
            [PropKind.Lockers] = Rect.MinMaxRect(-0.5f, -0.25f, 0.5f, 0.28f),
            [PropKind.Pallet] = Rect.MinMaxRect(-0.6f, -0.5f, 0.6f, 0.5f),
            [PropKind.WashBasin] = Rect.MinMaxRect(-0.8f, -0.26f, 0.8f, 0.25f),
            [PropKind.VendingMachine] = Rect.MinMaxRect(-0.45f, -0.4f, 0.45f, 0.42f),
            [PropKind.FloorLamp] = Rect.MinMaxRect(-0.2f, -0.2f, 0.2f, 0.2f),
            [PropKind.PingPongTable] = Rect.MinMaxRect(-1.37f, -0.87f, 1.37f, 0.87f),
            [PropKind.BeanBag] = Rect.MinMaxRect(-0.45f, -0.45f, 0.45f, 0.45f),
            [PropKind.ArcadeMachine] = Rect.MinMaxRect(-0.36f, -0.4f, 0.36f, 0.55f),
            [PropKind.Safe] = Rect.MinMaxRect(-0.4f, -0.35f, 0.4f, 0.41f),
            [PropKind.Hatch] = Rect.MinMaxRect(-0.9f, -0.9f, 0.9f, 0.9f),
            [PropKind.Crate] = Rect.MinMaxRect(-0.4f, -0.3f, 0.4f, 0.3f),
            [PropKind.MedTable] = Rect.MinMaxRect(-0.97f, -0.53f, 0.97f, 0.53f),
            [PropKind.Workbench] = Rect.MinMaxRect(-1.0f, -0.43f, 1.0f, 0.55f),
            [PropKind.Programmer] = Rect.MinMaxRect(-0.99f, -0.5f, 0.99f, 0.42f),
            [PropKind.Cage] = Rect.MinMaxRect(-1.0f, -1.0f, 1.0f, 1.0f),
            // the themed rooms (Tools/Blender/rooms_*.py)
            [PropKind.KitchenSink] = Rect.MinMaxRect(-0.6f, -0.31f, 0.6f, 0.31f),
            [PropKind.Stove] = Rect.MinMaxRect(-0.3f, -0.31f, 0.3f, 0.31f),
            [PropKind.Microwave] = Rect.MinMaxRect(-0.25f, -0.18f, 0.25f, 0.18f),
            [PropKind.WallCabinet] = Rect.MinMaxRect(-0.6f, -0.175f, 0.6f, 0.175f),
            [PropKind.Urinal] = Rect.MinMaxRect(-0.2f, -0.175f, 0.2f, 0.175f),
            [PropKind.MirrorSink] = Rect.MinMaxRect(-0.35f, -0.25f, 0.35f, 0.25f),
            [PropKind.HandDryer] = Rect.MinMaxRect(-0.15f, -0.1f, 0.15f, 0.1f),
            [PropKind.Bench] = Rect.MinMaxRect(-0.8f, -0.2f, 0.8f, 0.2f),
            [PropKind.ShowerStall] = Rect.MinMaxRect(-0.48f, -0.48f, 0.48f, 0.48f),
            [PropKind.Treadmill] = Rect.MinMaxRect(-0.42f, -0.95f, 0.42f, 0.95f),
            [PropKind.ExerciseBike] = Rect.MinMaxRect(-0.28f, -0.55f, 0.28f, 0.55f),
            [PropKind.WeightBench] = Rect.MinMaxRect(-0.8f, -0.65f, 0.8f, 0.65f),
            [PropKind.DumbbellRack] = Rect.MinMaxRect(-0.7f, -0.25f, 0.7f, 0.25f),
            [PropKind.PunchingBag] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.HospitalBed] = Rect.MinMaxRect(-0.48f, -1.05f, 0.48f, 1.05f),
            [PropKind.MedicineCabinet] = Rect.MinMaxRect(-0.45f, -0.2f, 0.45f, 0.2f),
            [PropKind.PrivacyScreen] = Rect.MinMaxRect(-0.8f, -0.15f, 0.8f, 0.15f),
            [PropKind.SleepPod] = Rect.MinMaxRect(-0.55f, -1f, 0.55f, 1f),
            [PropKind.Hammock] = Rect.MinMaxRect(-1.3f, -0.45f, 1.3f, 0.45f),
            [PropKind.TallBookcase] = Rect.MinMaxRect(-0.5f, -0.175f, 0.5f, 0.175f),
            [PropKind.ReadingTable] = Rect.MinMaxRect(-1f, -0.45f, 1f, 0.45f),
            [PropKind.Globe] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.Copier] = Rect.MinMaxRect(-0.48f, -0.33f, 0.48f, 0.33f),
            [PropKind.PaperStack] = Rect.MinMaxRect(-0.4f, -0.3f, 0.4f, 0.3f),
            [PropKind.MailShelf] = Rect.MinMaxRect(-0.6f, -0.2f, 0.6f, 0.2f),
            [PropKind.Shredder] = Rect.MinMaxRect(-0.23f, -0.18f, 0.23f, 0.18f),
            [PropKind.MonitorWall] = Rect.MinMaxRect(-1.2f, -0.15f, 1.2f, 0.15f),
            [PropKind.ConsoleDesk] = Rect.MinMaxRect(-1.2f, -0.45f, 1.2f, 0.45f),
            [PropKind.MixingDesk] = Rect.MinMaxRect(-0.8f, -0.4f, 0.8f, 0.4f),
            [PropKind.MicStand] = Rect.MinMaxRect(-0.25f, -0.25f, 0.25f, 0.25f),
            [PropKind.AcousticPanel] = Rect.MinMaxRect(-0.6f, -0.04f, 0.6f, 0.04f),
            [PropKind.DrumKit] = Rect.MinMaxRect(-0.8f, -0.65f, 0.8f, 0.65f),
            [PropKind.PoolTable] = Rect.MinMaxRect(-1.25f, -0.7f, 1.25f, 0.7f),
            [PropKind.Foosball] = Rect.MinMaxRect(-0.7f, -0.375f, 0.7f, 0.375f),
            [PropKind.DartBoard] = Rect.MinMaxRect(-0.3f, -0.04f, 0.3f, 0.04f),
            [PropKind.BarCounter] = Rect.MinMaxRect(-1f, -0.3f, 1f, 0.3f),
            [PropKind.BarStool] = Rect.MinMaxRect(-0.2f, -0.2f, 0.2f, 0.2f),
            [PropKind.LabBench] = Rect.MinMaxRect(-0.9f, -0.375f, 0.9f, 0.375f),
            [PropKind.PartsCabinet] = Rect.MinMaxRect(-0.45f, -0.225f, 0.45f, 0.225f),
            [PropKind.RobotArm] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.45f),
        };

        /// <summary>A model's footprint in its own frame (x = local X, y = local Z), metres.</summary>
        public static Rect FootprintOf(PropKind kind) => Foot.TryGetValue(kind, out var r) ? r : new Rect(-0.5f, -0.5f, 1f, 1f);

        const float DeskTop = LevelMetrics.DeskTop;
        const float CabinetTop = 1.2f; // the drawer cabinet's top (Tools/Blender/generate_models.py model_cabinet)
        const float WallGap = 0.06f;    // between a back and the wall's face
        const float Aisle = 1.25f;      // free walkway around an island and in front of a wall group (robots are 0.8 m wide)
        static readonly float[] ShelfLevels = { 0.47f, 1.07f, 1.67f };
        /// <summary>The tall bookcase's shelves within reach (Tools/Blender/rooms_paper.py).</summary>
        static readonly float[] BookcaseLevels = { 0.56f, 0.97f, 1.38f };

        readonly LevelLayout m_L;
        readonly HousePlan m_P;
        readonly System.Random m_Rng;
        readonly RectGrid[] m_Taken;   // per floor: placed footprints
        readonly RectGrid[] m_Keep;    // per floor: must stay clear (doorways, stairs, the hatch)

        /// <summary>
        /// Rectangles of a floor in 2 m buckets: "does anything overlap this?" looks only at the buckets the question touches
        /// (a big house has hundreds of pieces per floor; scanning them all for every try was most of the furnishing time).
        /// The answers are exactly those of a full scan.
        /// </summary>
        sealed class RectGrid
        {
            const float Cell = 2f;
            readonly Dictionary<long, List<int>> m_Buckets = new();
            readonly List<Rect> m_Rects = new();
            int m_Stamp;
            readonly List<int> m_Mark = new();

            static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
            static int C(float v) => Mathf.FloorToInt(v / Cell);

            public void Add(Rect r)
            {
                int i = m_Rects.Count;
                m_Rects.Add(r);
                m_Mark.Add(0);
                for (int x = C(r.xMin); x <= C(r.xMax); x++)
                    for (int z = C(r.yMin); z <= C(r.yMax); z++)
                    {
                        if (!m_Buckets.TryGetValue(Key(x, z), out var list)) m_Buckets[Key(x, z)] = list = new List<int>();
                        list.Add(i);
                    }
            }

            public void Clear()
            {
                m_Buckets.Clear();
                m_Rects.Clear();
                m_Mark.Clear();
            }

            public bool Overlaps(Rect q)
            {
                m_Stamp++;
                for (int x = C(q.xMin); x <= C(q.xMax); x++)
                    for (int z = C(q.yMin); z <= C(q.yMax); z++)
                    {
                        if (!m_Buckets.TryGetValue(Key(x, z), out var list)) continue;
                        foreach (int i in list)
                        {
                            if (m_Mark[i] == m_Stamp) continue;
                            m_Mark[i] = m_Stamp;
                            if (m_Rects[i].Overlaps(q)) return true;
                        }
                    }
                return false;
            }

            public bool Contains(Vector2 p)
            {
                if (!m_Buckets.TryGetValue(Key(C(p.x), C(p.y)), out var list)) return false;
                foreach (int i in list) if (m_Rects[i].Contains(p)) return true;
                return false;
            }
        }

        public Furnisher(LevelLayout l, System.Random rng)
        {
            m_L = l;
            m_P = l.Plan;
            m_Rng = rng;
            m_Taken = new RectGrid[m_P.Floors];
            m_Keep = new RectGrid[m_P.Floors];
            for (int f = 0; f < m_P.Floors; f++) { m_Taken[f] = new RectGrid(); m_Keep[f] = new RectGrid(); }
            foreach (var d in l.Doorways)
            {
                // the doorway and a step either side of it stay free (an open passage: the whole wall)
                var along = d.Rotation * Vector3.right;
                var across = d.Rotation * Vector3.forward;
                float half = d.Width / 2f + 0.35f, depth = d.Kind == EdgeType.Open ? 1.0f : 1.35f;
                Keep(d.Floor, d.Position - along * half - across * depth, d.Position + along * half + across * depth);
            }
            foreach (var s in l.Stairs)
            {
                var a = PlanGenerator.StairArea(s, 0f);
                var grown = new Rect(a.x, a.y, a.width, a.height);
                // the approach to the first step and the landing above (towards +u, the room side of the frame)
                var bottom = LevelLayout.StairEnds(s).bottom;
                grown = Union(grown, new Rect(bottom.x - 1.1f, bottom.z - 1.1f, 2.2f, 2.2f));
                m_Keep[s.Floor].Add(Grow(grown, 0.15f));
                if (s.Floor + 1 < m_P.Floors)
                {
                    var top = LevelLayout.StairEnds(s).top;
                    m_Keep[s.Floor + 1].Add(Grow(Union(a, new Rect(top.x - 1.1f, top.z - 1.1f, 2.2f, 2.2f)), 0.15f));
                }
            }
        }

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        static Rect Grow(Rect r, float k) => new(r.x - k, r.y - k, r.width + 2 * k, r.height + 2 * k);

        void Keep(int floor, Vector3 a, Vector3 b) =>
            m_Keep[floor].Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));

        /// <summary>Reserves a spot (the hatch, player spawns) before the furniture comes.</summary>
        public void Reserve(int floor, Vector3 centre, float half) => Keep(floor, centre - new Vector3(half, 0, half), centre + new Vector3(half, 0, half));

        // ------------------------------------------------------------------ placing

        /// <summary>A piece of a group in world space, not yet committed.</summary>
        struct Piece { public PropKind Kind; public Vector3 Pos; public float Yaw; public bool Solid; }

        /// <summary>A group being laid out: pieces and loot spots, committed only if everything fits.</summary>
        sealed class Batch
        {
            public readonly List<Piece> Pieces = new();
            public readonly List<Vector3> Items = new();
            /// <summary>Where workers leave loot (the storage room's shelves).</summary>
            public readonly List<StorageDrop> Drops = new();
            public void Add(PropKind k, Vector3 p, float yaw, bool solid = true) => Pieces.Add(new Piece { Kind = k, Pos = p, Yaw = yaw, Solid = solid });
            public void Item(Vector3 p) => Items.Add(p);
        }

        /// <summary>Axes of a group: U along the wall (or across an island), N into the room; Yaw turns a model's front to N.</summary>
        readonly struct Frame
        {
            public readonly Vector3 O, U, N;
            public readonly float Yaw;
            public Frame(Vector3 o, Vector3 u, Vector3 n) { O = o; U = u; N = n; Yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg; }
            public Vector3 P(float u, float n, float y = 0f) => O + U * u + N * n + Vector3.up * y;
            public float Face => Yaw;
            public float Back => Yaw + 180f;
            public float AlongU => Mathf.Atan2(U.x, U.z) * Mathf.Rad2Deg;
            public float AgainstU => AlongU + 180f;
        }

        /// <summary>The footprint of a model at a pose, as a world rect (yaws are multiples of 90°, near enough).</summary>
        static Rect FootAt(PropKind k, Vector3 p, float yaw)
        {
            var r = Foot.TryGetValue(k, out var f) ? f : Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f);
            var q = Quaternion.Euler(0, yaw, 0);
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var c in new[] { new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMax, 0, r.yMax) })
            {
                var w = p + q * c;
                min = Vector2.Min(min, new Vector2(w.x, w.z));
                max = Vector2.Max(max, new Vector2(w.x, w.z));
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>The rect lies on the floor of <paramref name="room"/> with a margin from its walls.</summary>
        bool OnFloor(int floor, int room, Rect r, float margin)
        {
            var g = Grow(r, margin);
            const float s = HousePlan.Res;
            for (float x = g.xMin + 0.01f; x < g.xMax; x += s * 0.5f)
                for (float z = g.yMin + 0.01f; z < g.yMax; z += s * 0.5f)
                    if (!InRoom(floor, room, x, z)) return false;
            return InRoom(floor, room, g.xMax - 0.01f, g.yMax - 0.01f) && InRoom(floor, room, g.xMax - 0.01f, g.yMin + 0.01f)
                && InRoom(floor, room, g.xMin + 0.01f, g.yMax - 0.01f);
        }

        bool InRoom(int floor, int room, float x, float z)
        {
            var c = HousePlan.CellOf(new Vector2(x, z));
            return m_P.KindAt(floor, c.x, c.y) == CellKind.Room && m_P.IdAt(floor, c.x, c.y) == room;
        }

        bool Clear(int floor, Rect r, float gap)
        {
            var g = Grow(r, gap);
            if (m_Taken[floor].Overlaps(g)) return false;
            if (m_Keep[floor].Overlaps(r)) return false;
            return true;
        }

        /// <summary>Commits a batch if every solid piece stands on the room's floor and clear of everything else.</summary>
        bool TryCommit(PlanRoom room, Batch b, float gap = 0.04f, float aisle = 0f, Vector3? front = null)
        {
            int f = room.Floor;
            if (m_Fridge && b.Pieces.Exists(p => p.Kind == PropKind.Fridge)) return false; // one fridge a kitchen
            var rects = new List<Rect>();
            foreach (var p in b.Pieces)
            {
                // a thing hung on the wall (cupboards, a dart board, foam): nothing to walk into, but never over a doorway
                if (!p.Solid)
                {
                    var hung = FootAt(p.Kind, p.Pos, p.Yaw);
                    if (m_Keep[f].Overlaps(Grow(hung, 0.06f)) || m_Taken[f].Overlaps(Grow(hung, -0.02f))) return false; // a thin thing: a margin round it
                    continue;
                }
                var r = FootAt(p.Kind, p.Pos, p.Yaw);
                if (!OnFloor(f, room.Id, r, 0.12f) || !Clear(f, r, gap)) return false;
                if (front is Vector3 n)
                {
                    // a walkway in front of a group along a wall: nothing placed there already, and still the room's floor
                    var ahead = Rect.MinMaxRect(Mathf.Min(r.xMin, r.xMin + n.x * Aisle), Mathf.Min(r.yMin, r.yMin + n.z * Aisle),
                                                Mathf.Max(r.xMax, r.xMax + n.x * Aisle), Mathf.Max(r.yMax, r.yMax + n.z * Aisle));
                    if (m_Taken[f].Overlaps(ahead)) return false;
                }
                rects.Add(r);
            }
            if (aisle > 0f && rects.Count > 0)
            {
                var all = rects[0];
                foreach (var r in rects) all = Union(all, r);
                if (!Clear(f, Grow(all, aisle), 0f) || !OnFloor(f, room.Id, all, aisle * 0.7f)) return false;
            }
            // pieces of one group may touch each other, but not other groups
            int group = m_Groups++;
            foreach (var p in b.Pieces)
            {
                m_L.Props.Add(new PropPlacement { Kind = p.Kind, Position = p.Pos, RotationY = p.Yaw });
                m_GroupOf.Add(group);
                m_Taken[f].Add(FootAt(p.Kind, p.Pos, p.Yaw)); // hung and standing-on things too: nothing else goes under or into them
            }
            foreach (var i in b.Items) { m_L.ItemSpots.Add(i); m_ItemGroup.Add(group); }
            if (room.Id == m_L.StorageRoom) m_L.StorageDrops.AddRange(b.Drops);
            foreach (var p in b.Pieces)
            {
                m_Fridge |= p.Kind == PropKind.Fridge;
                m_Table |= p.Kind is PropKind.RoundTable or PropKind.BarCounter;
            }
            return true;
        }

        int m_Groups;
        bool[] m_WalkFree;  // the walk grid of the bare building (see Walk), built on the first check
        int[] m_WalkOwner;
        readonly List<int> m_GroupOf = new();    // per prop of the layout: the group it came with (−1: not removable)
        readonly List<int> m_ItemGroup = new();  // per loot spot

        /// <summary>
        /// Under every high vent, on both sides: an ordinary desk against the wall (the players climb it to reach the duct —
        /// furniture that happens to stand there, not a staircase someone built). Never removed by the walkability check;
        /// the space in front of it stays free so nothing blocks the climb.
        /// </summary>
        public void PlaceVentDesks()
        {
            float face = LevelMetrics.WallThickness / 2f;
            float half = Foot[PropKind.Desk].height / 2f;
            foreach (var d in m_L.Doorways)
            {
                if (!d.High) continue;
                var across = d.Rotation * Vector3.forward;
                foreach (float side in new[] { -1f, 1f })
                {
                    var n = across * side;
                    var pos = d.Position + n * (face + WallGap + half);
                    var room = m_P.RoomAt(pos + Vector3.up * 0.1f);
                    if (room == null) continue;
                    float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg; // its front (+Z) to the room, its back to the wall
                    Put(room, PropKind.Desk, pos, yaw);
                    Keep(d.Floor, pos + n * half - d.Rotation * Vector3.right * 0.9f, pos + n * (half + LevelMetrics.VentDeskAisle) + d.Rotation * Vector3.right * 0.9f);
                }
            }
        }

        public void Put(PlanRoom room, PropKind k, Vector3 p, float yaw)
        {
            m_L.Props.Add(new PropPlacement { Kind = k, Position = p, RotationY = yaw });
            m_GroupOf.Add(-1);
            m_Taken[room.Floor].Add(FootAt(k, p, yaw));
        }

        // ------------------------------------------------------------------ walkability

        /// <summary>
        /// The safety net under all the rules above: a walker 0.8 m wide (a robot) must get from the hatch to the middle of
        /// every room. Checked on a 0.25 m grid of each floor: the rooms' floors minus a band along the solid walls and
        /// around the furniture, linked through doorways and open passages and between floors by the stairs. Where rooms
        /// stay out of reach, the furniture groups that wall them off go (with their loot), until everything is reachable.
        /// </summary>
        /// <summary>Furniture groups the last walkability check took out (for tests and logs).</summary>
        public static int LastRemoved { get; private set; }
        public static int LastDroppedItems { get; private set; }
        public static readonly List<string> LastNotes = new();

        /// <summary>Rooms whose middle cannot be walked to from the hatch (walls, doorways, stairs, furniture placed so far).</summary>
        public List<PlanRoom> Unreached()
        {
            var grid = new Walk(m_L, this);
            var reached = grid.Flood();
            var list = new List<PlanRoom>();
            foreach (var r in m_L.Rooms) if (r.Id != m_L.HiddenRoom && !grid.Reached(reached, r.Center)) list.Add(r);
            return list;
        }

        /// <summary>Takes the walk grid of the bare building from another furnisher of the same layout (it is built only once).</summary>
        public void ShareWalk(Furnisher other)
        {
            if (other.m_L != m_L) return;
            m_WalkFree = other.m_WalkFree;
            m_WalkOwner = other.m_WalkOwner;
        }

        public void EnsureWalkable()
        {
            LastRemoved = 0;
            LastDroppedItems = 0;
            LastNotes.Clear();
            for (int round = 0; round < 40; round++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var grid = new Walk(m_L, this);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                var reached = grid.Flood();
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                var lostRooms = new List<PlanRoom>();
                // the walker is a robot: it never gets into the hidden room (players do, through its illusion walls)
                foreach (var r in m_L.Rooms) if (r.Id != m_L.HiddenRoom && !grid.Reached(reached, r.Center)) lostRooms.Add(r);
                long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
                var cut = lostRooms.Count > 0 ? grid.Barrier(reached, lostRooms) : new HashSet<int>();
                long t4 = System.Diagnostics.Stopwatch.GetTimestamp();
                long tick = System.Diagnostics.Stopwatch.Frequency / 1000;
                LevelGenerator.Timing.Grid += (t1 - t0) / tick; LevelGenerator.Timing.Flood += (t2 - t1) / tick;
                LevelGenerator.Timing.Reach += (t3 - t2) / tick; LevelGenerator.Timing.Barrier += (t4 - t3) / tick;
                LevelGenerator.Timing.Rounds++;
                if (cut.Count == 0)
                {
                    foreach (var s in m_L.Stairs)
                        LastNotes.Add($"stair from {s.Floor} room {s.Room} reached {grid.Reached(reached, m_L.Rooms[s.Room].Center)} -> room {s.RoomAbove} reached {grid.Reached(reached, m_L.Rooms[s.RoomAbove].Center)}");
                    foreach (var r in m_L.Rooms)
                        if (!grid.Reached(reached, r.Center)) LastNotes.Add($"lost #{r.Id} {r.Type} f{r.Floor} {r.Center}");
                    // loot nobody can reach (in a niche walled off by furniture) is not left there
                    for (int i = m_L.ItemSpots.Count - 1; i >= 0; i--)
                    {
                        var p = m_L.ItemSpots[i];
                        var floor = new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y)), p.z);
                        bool hidden = m_L.HiddenRoom >= 0 && m_L.Plan.RoomAt(floor + Vector3.up * 0.3f)?.Id == m_L.HiddenRoom;
                        if (!hidden && !grid.Reached(reached, floor)) { m_L.ItemSpots.RemoveAt(i); m_ItemGroup.RemoveAt(i); LastDroppedItems++; }
                    }
                    return;
                }
                LastRemoved += cut.Count;
                RemoveGroups(cut);
            }
        }

        /// <summary>
        /// After <see cref="EnsureWalkable"/>: a kitchen whose run (with its fridge) had to go, because it stood in the way
        /// between doors, gets a fridge of its own back — against any wall, with a walkway in front of it. True if one was
        /// put back (then check the walking again).
        /// </summary>
        public bool RestoreFridges()
        {
            bool any = false;
            foreach (var room in m_L.Rooms)
            {
                if (room.Type != RoomType.Kitchen) continue;
                bool has = false;
                foreach (var p in m_L.Props) has |= p.Kind == PropKind.Fridge && m_P.RoomAt(p.Position + Vector3.up * 0.3f) == room;
                if (has) continue;
                m_Fridge = false;
                int before = m_L.Props.Count;
                AgainstAnyWall(room, PropKind.Fridge, walkway: true);
                any |= m_L.Props.Count > before;
            }
            return any;
        }

        void RemoveGroups(HashSet<int> groups)
        {
            for (int i = m_L.Props.Count - 1; i >= 0; i--)
                if (m_GroupOf[i] >= 0 && groups.Contains(m_GroupOf[i])) { m_L.Props.RemoveAt(i); m_GroupOf.RemoveAt(i); }
            for (int i = m_L.ItemSpots.Count - 1; i >= 0; i--)
                if (groups.Contains(m_ItemGroup[i])) { m_L.ItemSpots.RemoveAt(i); m_ItemGroup.RemoveAt(i); }
            for (int f = 0; f < m_Taken.Length; f++) m_Taken[f].Clear();
            foreach (var p in m_L.Props)
                if (p.Kind is not (PropKind.Partition or PropKind.DeskLamp or PropKind.CoffeeMachine))
                    m_Taken[LevelLayout.FloorOf(p.Position.y)].Add(FootAt(p.Kind, p.Position, p.RotationY));
        }

        /// <summary>The walkable grid of the house for one check.</summary>
        sealed class Walk
        {
            const float S = 0.25f, Agent = 0.4f, Clutter = 0.55f; // a robot's half width; around furniture a little more (the baked navmesh erodes more there)
            readonly LevelLayout m_L;
            readonly int m_NX, m_NZ, m_F;
            readonly bool[] m_Free;
            readonly int[] m_Owner; // the furniture group blocking a cell (−1 none, −2 wall)

            public Walk(LevelLayout l, Furnisher fur)
            {
                m_L = l;
                m_F = l.Floors;
                m_NX = Mathf.CeilToInt(l.SizeX / S);
                m_NZ = Mathf.CeilToInt(l.SizeZ / S);
                // the building itself (floors, walls, stairs) does not change while furniture is taken out: built once per
                // furnisher, copied for every check, and only the furniture is stamped on the copy
                if (fur.m_WalkFree != null)
                {
                    m_Free = (bool[])fur.m_WalkFree.Clone();
                    m_Owner = (int[])fur.m_WalkOwner.Clone();
                    Furniture(l, fur);
                    return;
                }
                m_Free = new bool[m_F * m_NX * m_NZ];
                m_Owner = new int[m_F * m_NX * m_NZ];
                for (int f = 0; f < m_F; f++)
                    for (int x = 0; x < m_NX; x++)
                        for (int z = 0; z < m_NZ; z++)
                        {
                            int i = Index(f, x, z);
                            m_Owner[i] = -1;
                            var c = Center(x, z);
                            m_Free[i] = l.Plan.IsFloor(f, c);
                        }
                // solid walls (openings cut out) with the walker's half width around them
                for (int w = 0; w < l.Plan.Walls.Count; w++)
                {
                    var wall = l.Plan.Walls[w];
                    var cuts = new List<(float a, float b)>();
                    bool open = false;
                    foreach (var o in l.Plan.OpeningsOf(w))
                    {
                        if (o.Kind == EdgeType.Open) open = true;
                        else if (o.Kind == EdgeType.Door) cuts.Add((o.At - LevelMetrics.ClearWidth(o.Width) / 2f, o.At + LevelMetrics.ClearWidth(o.Width) / 2f));
                    }
                    if (open) continue;
                    cuts.Sort();
                    float from = 0f;
                    foreach (var (a, b) in cuts) { Band(wall.Floor, wall.At(from), wall.At(a), LevelMetrics.WallThickness / 2f + Agent, -2); from = b; }
                    Band(wall.Floor, wall.At(from), wall.At(wall.Length), LevelMetrics.WallThickness / 2f + Agent, -2);
                }
                // the stairs' squares (their flights are walked through the links below)
                foreach (var s in l.Stairs)
                {
                    var a = PlanGenerator.StairArea(s, Agent + 0.05f); // the flights and rails, with a robot's half width
                    Box(s.Floor, a, -2);
                    if (s.Floor + 1 < m_F) Box(s.Floor + 1, a, -2);
                }
                fur.m_WalkFree = (bool[])m_Free.Clone();
                fur.m_WalkOwner = (int[])m_Owner.Clone();
                Furniture(l, fur);
            }

            void Furniture(LevelLayout l, Furnisher fur)
            {
                for (int i = 0; i < l.Props.Count; i++)
                {
                    var p = l.Props[i];
                    if (p.Kind is PropKind.Partition or PropKind.DeskLamp or PropKind.CoffeeMachine or PropKind.Hatch or PropKind.Microwave
                        or PropKind.WallCabinet or PropKind.HandDryer or PropKind.AcousticPanel or PropKind.DartBoard) continue; // on a top or up on a wall
                    var r = FootAt(p.Kind, p.Position, p.RotationY);
                    Box(LevelLayout.FloorOf(p.Position.y), Grow(r, Clutter), fur.m_GroupOf[i] >= 0 ? fur.m_GroupOf[i] : -2);
                }
            }

            int Index(int f, int x, int z) => (f * m_NZ + z) * m_NX + x;
            static Vector2 Center(int x, int z) => new((x + 0.5f) * S, (z + 0.5f) * S);
            Vector2Int CellOf(Vector2 p) => new(Mathf.FloorToInt(p.x / S), Mathf.FloorToInt(p.y / S));
            bool In(int f, int x, int z) => f >= 0 && f < m_F && x >= 0 && z >= 0 && x < m_NX && z < m_NZ;

            void Block(int f, int x, int z, int owner)
            {
                if (!In(f, x, z)) return;
                int i = Index(f, x, z);
                if (!m_Free[i] && m_Owner[i] == -2) return;
                m_Free[i] = false;
                if (m_Owner[i] != -2) m_Owner[i] = owner == -2 ? -2 : (m_Owner[i] == -1 ? owner : m_Owner[i]);
            }

            void Box(int f, Rect r, int owner)
            {
                var a = CellOf(r.min);
                var b = CellOf(r.max);
                for (int x = a.x; x <= b.x; x++)
                    for (int z = a.y; z <= b.y; z++)
                        if (r.Overlaps(new Rect(x * S, z * S, S, S))) Block(f, x, z, owner);
            }

            void Band(int f, Vector2 a, Vector2 b, float half, int owner)
            {
                float len = (b - a).magnitude;
                var box = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - half, Mathf.Min(a.y, b.y) - half, Mathf.Max(a.x, b.x) + half, Mathf.Max(a.y, b.y) + half);
                var lo = CellOf(box.min);
                var hi = CellOf(box.max);
                for (int x = lo.x; x <= hi.x; x++)
                    for (int z = lo.y; z <= hi.y; z++)
                    {
                        var c = Center(x, z);
                        float t = len > 1e-4f ? Mathf.Clamp(Vector2.Dot(c - a, (b - a) / len), 0f, len) : 0f;
                        var near = len > 1e-4f ? a + (b - a) / len * t : a;
                        if ((c - near).magnitude < half) Block(f, x, z, owner);
                    }
            }

            /// <summary>Cells reached from the hatch (stairs link their ends on two floors).</summary>
            public bool[] Flood()
            {
                var seen = new bool[m_Free.Length];
                var q = new Queue<int>();
                void Seed(Vector3 p)
                {
                    int f = LevelLayout.FloorOf(p.y);
                    var c = Nearest(f, new Vector2(p.x, p.z));
                    if (c < 0 || seen[c]) return;
                    seen[c] = true;
                    q.Enqueue(c);
                }
                Seed(m_L.HatchPosition + new Vector3(1.6f, 0f, 0f));
                Seed(m_L.HatchPosition - new Vector3(1.6f, 0f, 0f));
                Seed(m_L.HatchPosition + new Vector3(0f, 0f, 1.6f));
                Seed(m_L.HatchPosition - new Vector3(0f, 0f, 1.6f));
                void Visit(int j)
                {
                    if (seen[j] || !m_Free[j]) return;
                    seen[j] = true;
                    q.Enqueue(j);
                }
                bool more = true;
                var linked = new HashSet<int>();
                while (more)
                {
                    while (q.Count > 0)
                    {
                        int i = q.Dequeue();
                        int x = i % m_NX, z = i / m_NX % m_NZ;
                        if (x > 0) Visit(i - 1);
                        if (x < m_NX - 1) Visit(i + 1);
                        if (z > 0) Visit(i - m_NX);
                        if (z < m_NZ - 1) Visit(i + m_NX);
                    }
                    more = false;
                    for (int k = 0; k < m_L.Stairs.Count; k++)
                    {
                        if (linked.Contains(k)) continue;
                        var s = m_L.Stairs[k];
                        var (bottom, top) = LevelLayout.StairEnds(s);
                        int bi = Nearest(s.Floor, new Vector2(bottom.x, bottom.z), s.Room);
                        int ti = Nearest(s.Floor + 1, new Vector2(top.x, top.z), s.RoomAbove);
                        if (bi < 0 || ti < 0) { LastNotes.Add($"stair {k} floor {s.Floor}: bottom {bottom} cell {bi} room {s.Room} space {m_L.Plan.SpaceAt(s.Floor, new Vector2(bottom.x, bottom.z))}, top {top} cell {ti} room {s.RoomAbove} space {m_L.Plan.SpaceAt(s.Floor + 1, new Vector2(top.x, top.z))}"); continue; }
                        if (seen[bi] && !seen[ti]) { seen[ti] = true; q.Enqueue(ti); linked.Add(k); more = true; }
                        else if (seen[ti] && !seen[bi]) { seen[bi] = true; q.Enqueue(bi); linked.Add(k); more = true; }
                    }
                }
                return seen;
            }

            /// <summary>The free cell nearest to a point of a floor (within 1.5 m; of that room when given), or −1.</summary>
            int Nearest(int f, Vector2 p, int room = -1)
            {
                var c = CellOf(p);
                int best = -1;
                float bestD = 1.5f;
                for (int dx = -6; dx <= 6; dx++)
                    for (int dz = -6; dz <= 6; dz++)
                    {
                        if (!In(f, c.x + dx, c.y + dz)) continue;
                        int i = Index(f, c.x + dx, c.y + dz);
                        if (!m_Free[i]) continue;
                        if (room >= 0 && m_L.Plan.SpaceAt(f, Center(c.x + dx, c.y + dz)) != room) continue;
                        float d = (Center(c.x + dx, c.y + dz) - p).magnitude;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                return best;
            }

            public bool Reached(bool[] seen, Vector3 p)
            {
                int f = LevelLayout.FloorOf(p.y);
                int i = Nearest(f, new Vector2(p.x, p.z), m_L.Plan.SpaceAt(f, new Vector2(p.x, p.z)));
                return i >= 0 && seen[i];
            }

            const int Reach = 6; // 1.5 m in cells: how close a blocking group has to be to both sides

            /// <summary>
            /// The furniture between the reached part of the house and the rooms out of reach: groups owning blocked cells
            /// within 1.5 m (Chebyshev, same floor) of both a reached cell and a free cell on the lost side (flooded from
            /// the lost rooms' middles). One flood of the lost side, two summed-area tables, then one look per blocked cell.
            /// </summary>
            public HashSet<int> Barrier(bool[] seen, List<PlanRoom> rooms)
            {
                var found = new HashSet<int>();
                int n = m_Free.Length, plane = m_NX * m_NZ;
                // the lost side: free unreached cells connected to a lost room's middle
                var lost = new bool[n];
                var stack = new Stack<int>();
                foreach (var room in rooms)
                {
                    int start = Nearest(room.Floor, new Vector2(room.Center.x, room.Center.z));
                    if (start < 0 || seen[start] || lost[start]) continue;
                    lost[start] = true;
                    stack.Push(start);
                }
                // and the unreached ends of stairs: furniture can wall off the approach to a flight inside a room that is
                // reached itself (a narrow strip beside the stair), and then the whole floor beyond is lost
                foreach (var s in m_L.Stairs)
                {
                    var (bottom, top) = LevelLayout.StairEnds(s);
                    foreach (int end in new[] { Nearest(s.Floor, new Vector2(bottom.x, bottom.z), s.Room), Nearest(s.Floor + 1, new Vector2(top.x, top.z), s.RoomAbove) })
                    {
                        if (end < 0 || seen[end] || lost[end]) continue;
                        lost[end] = true;
                        stack.Push(end);
                    }
                }
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % m_NX, z = i / m_NX % m_NZ;
                    if (x > 0) Visit(i - 1);
                    if (x < m_NX - 1) Visit(i + 1);
                    if (z > 0) Visit(i - m_NX);
                    if (z < m_NZ - 1) Visit(i + m_NX);
                }
                void Visit(int j)
                {
                    if (!m_Free[j] || seen[j] || lost[j]) return;
                    lost[j] = true;
                    stack.Push(j);
                }
                var nearSeen = Table(seen);
                var nearLost = Table(lost);
                int w = m_NX + 1;
                for (int f = 0; f < m_F; f++)
                {
                    int tb = f * w * (m_NZ + 1);
                    for (int z = 0; z < m_NZ; z++)
                    {
                        int z0 = Mathf.Max(0, z - Reach), z1 = Mathf.Min(m_NZ, z + Reach + 1);
                        for (int x = 0; x < m_NX; x++)
                        {
                            int i = f * plane + z * m_NX + x;
                            if (m_Free[i] || m_Owner[i] < 0 || found.Contains(m_Owner[i])) continue;
                            int x0 = Mathf.Max(0, x - Reach), x1 = Mathf.Min(m_NX, x + Reach + 1);
                            int a = tb + z0 * w + x0, b = tb + z0 * w + x1, c = tb + z1 * w + x0, d = tb + z1 * w + x1;
                            if (nearSeen[d] - nearSeen[b] - nearSeen[c] + nearSeen[a] > 0 &&
                                nearLost[d] - nearLost[b] - nearLost[c] + nearLost[a] > 0) found.Add(m_Owner[i]);
                        }
                    }
                }
                return found;
            }

            /// <summary>Summed-area table of the marked free cells, per floor ((NX + 1) × (NZ + 1) each, zero first row and column).</summary>
            int[] Table(bool[] marked)
            {
                int w = m_NX + 1, h = m_NZ + 1, plane = m_NX * m_NZ;
                var sum = new int[m_F * w * h];
                for (int f = 0; f < m_F; f++)
                {
                    int tb = f * w * h;
                    for (int z = 0; z < m_NZ; z++)
                    {
                        int row = 0;
                        for (int x = 0; x < m_NX; x++)
                        {
                            int i = f * plane + z * m_NX + x;
                            if (marked[i] && m_Free[i]) row++;
                            sum[tb + (z + 1) * w + x + 1] = sum[tb + z * w + x + 1] + row;
                        }
                    }
                }
                return sum;
            }
        }

        float BackOf(PropKind k) => -(Foot.TryGetValue(k, out var f) ? f.yMin : -0.3f) + WallGap;
        float R(float a, float b) => a + (float)m_Rng.NextDouble() * (b - a);
        bool Chance(double p) => m_Rng.NextDouble() < p;

        // ------------------------------------------------------------------ rooms

        /// <summary>Furnishes one room: islands first in big rooms, then the walls, then the corners.</summary>
        /// <summary>A printer stands in this room already: at most one per room (they looked like a row of kiosks).</summary>
        bool m_Printer;
        /// <summary>A fridge stands in this room already: every kitchen gets one, first thing along its walls.</summary>
        bool m_Fridge;
        /// <summary>A table stands in this room already (a kitchen gets one: a small one if the big one did not fit).</summary>
        bool m_Table;

        public void FurnishRoom(PlanRoom room)
        {
            if (room.Area < 4f) return;
            m_Printer = false;
            m_Fridge = false;
            m_Table = false;
            int islands = room.Type switch
            {
                RoomType.OpenSpace => Mathf.RoundToInt(room.Area / 22f),
                RoomType.Server => Mathf.RoundToInt(room.Area / 16f),
                RoomType.Storage => room.Area > 24f ? 1 : 0, // a pallet or so: the middle is the pile's floor
                RoomType.Meeting => 1,
                RoomType.Kitchen => Mathf.Max(1, Mathf.RoundToInt(room.Area / 25f)),
                RoomType.Lounge => Mathf.Max(1, Mathf.RoundToInt(room.Area / 28f)),
                RoomType.Lobby => 2,
                RoomType.Office => room.Area > 30f ? 1 : 0,
                RoomType.Hall => room.Area > 45f ? 1 : 0,
                RoomType.Gym => Mathf.Max(1, Mathf.RoundToInt(room.Area / 24f)),
                RoomType.Library => Mathf.RoundToInt(room.Area / 20f),
                RoomType.CopyRoom => room.Area > 18f ? 1 : 0,
                RoomType.ControlRoom => room.Area > 28f ? 1 : 0,
                RoomType.Lab => Mathf.RoundToInt(room.Area / 24f),
                RoomType.NapRoom => room.Area > 20f ? 1 : 0,
                RoomType.Studio => room.Area > 18f ? 1 : 0,
                RoomType.LockerRoom => Mathf.Max(1, Mathf.RoundToInt(room.Area / 26f)),
                RoomType.Infirmary => room.Area > 22f ? 1 : 0,
                RoomType.GameRoom => Mathf.Max(1, Mathf.RoundToInt(room.Area / 22f)),
                _ => 0,
            };
            Anchor(room);
            Islands(room, islands);
            if (room.Type == RoomType.Kitchen && !m_Table) SmallTable(room);
            Walls(room);
            if (room.Type >= RoomType.Gym) Walls(room); // themed rooms are fuller: a second pass fills the gaps the first left
            Corners(room);
        }

        /// <summary>
        /// A themed room's own piece first, compact enough for a small room (a bed lengthwise along the wall, one pod, the
        /// video wall without its console…), so the room always reads as what it is; the rest comes after it. Tried along
        /// every wall, longest first, with the usual walkway in front, then with a narrower one.
        /// </summary>
        void Anchor(PlanRoom room)
        {
            if (room.Type < RoomType.Gym && room.Type is not (RoomType.Kitchen or RoomType.Restroom)) return;
            var faces = FacesOf(room);
            float y = LevelLayout.FloorY(room.Floor);
            // the best option first (the biggest), smaller ones where doors leave too little wall
            for (int option = 0; option < 3; option++)
                foreach (float walk in new[] { 1.7f, 1.0f })
                    foreach (var (w, n) in faces)
                    {
                        var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                        for (float t = 0.2f; t < w.Length - 0.3f; t += 0.25f)
                        {
                            var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                            var batch = new Batch();
                            float width = AnchorGroup(room, batch, new Frame(face, u, n), option);
                            if (width <= 0f || t + width > w.Length - 0.1f) break;
                            var shift = u * (width / 2f);
                            for (int i = 0; i < batch.Pieces.Count; i++) { var q = batch.Pieces[i]; q.Pos += shift; batch.Pieces[i] = q; }
                            for (int i = 0; i < batch.Items.Count; i++) batch.Items[i] += shift;
                            if (Deep(room, face + shift, n, batch, walk) && TryCommit(room, batch, front: n)) return;
                        }
                    }
            // doors everywhere along the walls: the piece stands free in the room, an aisle round it
            var b = room.Bounds;
            var spots = new List<Vector3>();
            for (float x = b.xMin + 1.2f; x < b.xMax - 1.1f; x += 0.5f)
                for (float z = b.yMin + 1.2f; z < b.yMax - 1.1f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            var c = room.Center;
            spots.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            bool wide = b.width >= b.height;
            foreach (var s in spots)
            {
                // the room's own free-standing group (a table, a bed, a pool table — never a thing that hangs on a wall)
                var f = new Frame(s, wide ? Vector3.right : Vector3.forward, wide ? Vector3.forward : Vector3.left);
                var batch = new Batch();
                Island(room, f, batch);
                if (batch.Pieces.Count > 0 && batch.Pieces.TrueForAll(p => p.Solid) && TryCommit(room, batch, 0.05f, 0.9f)) return;
            }
        }

        /// <summary>
        /// A themed room's signature group against a wall (centred on the frame, u along the wall), option 0 the biggest,
        /// 1 and 2 ever smaller; its width, 0 = no such option.
        /// </summary>
        float AnchorGroup(PlanRoom room, Batch b, Frame f, int option)
        {
            float Single(PropKind k, float w) { b.Add(k, f.P(0, BackOf(k)), f.Face); return w; }
            float Out(PropKind k, float v, float w) { b.Add(k, f.P(0, v), f.Face); return w; }
            // lying along the wall (its side to it): beds, pods
            float Along(PropKind k)
            {
                var r = FootprintOf(k);
                b.Add(k, f.P(0, r.width / 2f + WallGap), f.Face + 90f);
                return r.height + 0.05f;
            }
            switch (room.Type, option)
            {
                case (RoomType.Kitchen, 0):
                case (RoomType.Kitchen, 1):
                {
                    // the kitchen run (or its shorter half): sink, cooker, worktop with its microwave and the coffee machine, cupboards
                    // over it, and the fridge at its end (every kitchen has one); the whole run shifted to centre it
                    bool full = option == 0;
                    float shift = full ? -0.36f : -0.355f;
                    float sink = (full ? -1.52f : -1.22f) + shift, counter = (full ? 0.92f : 0.62f) + shift;
                    b.Add(PropKind.KitchenSink, f.P(sink, BackOf(PropKind.KitchenSink)), f.Face);
                    if (full) b.Add(PropKind.Stove, f.P(-0.62f + shift, BackOf(PropKind.Stove)), f.Face);
                    b.Add(PropKind.Fridge, f.P(counter + 1.22f + 0.37f, BackOf(PropKind.Fridge)), f.Face);
                    b.Add(PropKind.KitchenCounter, f.P(counter, BackOf(PropKind.KitchenCounter)), f.Face);
                    var cx = Quaternion.Euler(0, f.Face, 0) * Vector3.right;
                    b.Add(PropKind.CoffeeMachine, f.P(counter, BackOf(PropKind.KitchenCounter) + 0.05f, 0.93f) + cx * 0.75f, f.Face, solid: false);
                    b.Add(PropKind.WallCabinet, f.P(sink, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    if (full) b.Add(PropKind.WallCabinet, f.P(counter, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    b.Item(f.P(counter, BackOf(PropKind.KitchenCounter), 0.93f));
                    return full ? 5.02f : 4.41f;
                }
                case (RoomType.Kitchen, 2):
                    // the smallest: the sink and the fridge beside it
                    b.Add(PropKind.KitchenSink, f.P(-0.36f, BackOf(PropKind.KitchenSink)), f.Face);
                    b.Add(PropKind.WallCabinet, f.P(-0.36f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    b.Add(PropKind.Fridge, f.P(0.61f, BackOf(PropKind.Fridge)), f.Face);
                    return 1.96f;
                case (RoomType.Restroom, 0):
                    b.Add(PropKind.MirrorSink, f.P(-0.62f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.MirrorSink, f.P(0.18f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.HandDryer, f.P(0.82f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                    return 2.0f;
                case (RoomType.Restroom, 1):
                    b.Add(PropKind.MirrorSink, f.P(-0.22f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.HandDryer, f.P(0.42f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                    return 1.2f;
                case (RoomType.Restroom, 2): return Row(f, b, PropKind.Urinal, 2, 0.7f);
                case (RoomType.Gym, 0): return Single(PropKind.DumbbellRack, 1.5f);
                case (RoomType.Gym, 1): return Single(PropKind.ExerciseBike, 0.65f);
                case (RoomType.Gym, 2): return Out(PropKind.PunchingBag, 0.5f, 0.7f);
                case (RoomType.Library, 0): return Row(f, b, PropKind.TallBookcase, 2, 1.02f);
                case (RoomType.Library, 1): return Single(PropKind.TallBookcase, 1.05f);
                case (RoomType.Library, 2): return Out(PropKind.Globe, 0.4f, 0.7f);
                case (RoomType.CopyRoom, 0): { Single(PropKind.Copier, 1.0f); b.Item(f.P(0.2f, BackOf(PropKind.Copier), 1.05f)); return 1.0f; }
                case (RoomType.CopyRoom, 1): return Single(PropKind.PaperStack, 0.85f);
                case (RoomType.CopyRoom, 2): return Single(PropKind.Shredder, 0.5f);
                case (RoomType.ControlRoom, 0): return Single(PropKind.MonitorWall, 2.45f);
                case (RoomType.ControlRoom, 1): return Single(PropKind.ConsoleDesk, 2.45f);
                case (RoomType.Lab, 0):
                    b.Add(PropKind.LabBench, f.P(0, BackOf(PropKind.LabBench)), f.Face);
                    b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.LabBench) + 0.1f, 0.91f));
                    return 1.85f;
                case (RoomType.Lab, 1): return Single(PropKind.PartsCabinet, 0.95f);
                case (RoomType.Lab, 2): return Single(PropKind.RobotArm, 0.7f);
                case (RoomType.NapRoom, 0): return Along(PropKind.SleepPod);
                case (RoomType.NapRoom, 1): return Out(PropKind.BeanBag, 0.6f, 0.95f);
                case (RoomType.Studio, 0):
                    b.Add(PropKind.AcousticPanel, f.P(-0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false);
                    b.Add(PropKind.AcousticPanel, f.P(0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false);
                    b.Add(PropKind.MicStand, f.P(0, 0.9f), f.Face);
                    return 2.45f;
                case (RoomType.Studio, 1): return Single(PropKind.MixingDesk, 1.65f);
                case (RoomType.Studio, 2): { b.Add(PropKind.AcousticPanel, f.P(0, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); return 1.25f; }
                case (RoomType.LockerRoom, 0): return Row(f, b, PropKind.Lockers, 2, 1.05f);
                case (RoomType.LockerRoom, 1): return Single(PropKind.Lockers, 1.05f);
                case (RoomType.LockerRoom, 2): return Out(PropKind.Bench, 0.3f, 1.65f);
                case (RoomType.Infirmary, 0): { float w = Along(PropKind.HospitalBed); b.Item(f.P(0, 0.5f, 0.66f)); return w; }
                case (RoomType.Infirmary, 1): { Single(PropKind.MedicineCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.MedicineCabinet), 1.65f)); return 0.95f; }
                case (RoomType.Infirmary, 2): return Out(PropKind.PrivacyScreen, 0.3f, 1.65f);
                case (RoomType.GameRoom, 0): return Row(f, b, PropKind.ArcadeMachine, 2, 0.8f);
                case (RoomType.GameRoom, 1): return Single(PropKind.ArcadeMachine, 0.8f);
                case (RoomType.GameRoom, 2): { b.Add(PropKind.DartBoard, f.P(0, BackOf(PropKind.DartBoard)), f.Face, solid: false); return 0.65f; }
            }
            return 0f;
        }

        /// <summary>The walls of a room with the normal into it, longest first (rails, curves and stubs left out).</summary>
        List<(PlanWall w, Vector3 n)> FacesOf(PlanRoom room)
        {
            var list = new List<(PlanWall w, Vector3 n)>();
            foreach (var w in m_P.Walls)
            {
                if (w.Floor != room.Floor || w.Railing || w.Shaped || w.Length < 0.9f) continue;
                if (w.RoomA != room.Id && w.RoomB != room.Id) continue;
                var d = w.Dir;
                var n = new Vector3(-d.y, 0, d.x);
                var mid = w.At(w.Length / 2f);
                if (!InRoom(room.Floor, room.Id, mid.x + n.x * 0.3f, mid.y + n.z * 0.3f)) n = -n;
                if (!InRoom(room.Floor, room.Id, mid.x + n.x * 0.3f, mid.y + n.z * 0.3f)) continue;
                list.Add((w, n));
            }
            list.Sort((p, q) => q.w.Length.CompareTo(p.w.Length));
            return list;
        }

        void Islands(PlanRoom room, int count)
        {
            if (count <= 0) return;
            var spots = new List<Vector3>();
            var b = room.Bounds;
            for (float x = b.xMin + 1.5f; x < b.xMax - 1.4f; x += 0.5f)
                for (float z = b.yMin + 1.5f; z < b.yMax - 1.4f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, LevelLayout.FloorY(room.Floor), z));
            // closest to the centre first, a little shuffled
            var c = room.Center;
            var keys = new Dictionary<Vector3, float>();
            foreach (var s in spots) keys[s] = (s - c).sqrMagnitude + (float)m_Rng.NextDouble() * 6f;
            spots.Sort((p, q) => keys[p].CompareTo(keys[q]));
            int placed = 0;
            foreach (var s in spots)
            {
                if (placed >= count) break;
                bool turn = Chance(0.5);
                var f = new Frame(s, turn ? Vector3.forward : Vector3.right, turn ? Vector3.left : Vector3.forward);
                var batch = new Batch();
                Island(room, f, batch);
                if (batch.Pieces.Count > 0 && TryCommit(room, batch, 0.05f, Aisle)) placed++;
            }
        }

        /// <summary>One island group of the room's type around frame origin.</summary>
        void Island(PlanRoom room, Frame f, Batch b)
        {
            switch (room.Type)
            {
                case RoomType.OpenSpace:
                    if (Chance(0.85)) DeskCluster(f, b);
                    else { b.Add(PropKind.RoundTable, f.P(0, 0), 0); Chairs(f, b, 3); }
                    break;
                case RoomType.Meeting:
                    if (room.Area > 22f && Chance(0.75))
                    {
                        b.Add(PropKind.MeetingTable, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++)
                        {
                            b.Add(PropKind.Chair, f.P(i * 1.1f, 1.05f), f.Back);
                            b.Add(PropKind.Chair, f.P(i * 1.1f, -1.05f), f.Face);
                        }
                        b.Item(f.P(R(-1.2f, 1.2f), R(-0.3f, 0.3f), 0.78f));
                    }
                    else { b.Add(PropKind.RoundTable, f.P(0, 0), 0); Chairs(f, b, 4); b.Item(f.P(0, 0, 0.76f)); }
                    break;
                case RoomType.Kitchen:
                    if (Chance(0.2))
                    {   // a breakfast bar: the counter and its stools on the far side
                        b.Add(PropKind.BarCounter, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.BarStool, f.P(i * 0.62f, 0.62f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), 0, 1.06f));
                        break;
                    }
                    b.Add(PropKind.RoundTable, f.P(0, 0), 0); // the kitchen table, chairs all round
                    Chairs(f, b, 3 + m_Rng.Next(2));
                    b.Item(f.P(R(-0.2f, 0.2f), 0, 0.76f));
                    break;
                case RoomType.Gym:
                    if (Chance(0.55)) { b.Add(PropKind.WeightBench, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0.4f)); }
                    else { b.Add(PropKind.Treadmill, f.P(-0.5f, 0), f.Face); b.Add(PropKind.Treadmill, f.P(0.5f, 0), f.Face); }
                    break;
                case RoomType.Library:
                    if (Chance(0.5))
                    {   // a reading table, chairs along both sides
                        b.Add(PropKind.ReadingTable, f.P(0, 0), f.Face);
                        foreach (float u in new[] { -0.5f, 0.5f })
                        {
                            b.Add(PropKind.Chair, f.P(u, 0.75f), f.Back);
                            b.Add(PropKind.Chair, f.P(u, -0.75f), f.Face);
                        }
                        b.Item(f.P(R(-0.7f, 0.7f), R(-0.2f, 0.2f), 0.77f));
                    }
                    else
                    {   // a double-sided stack of bookcases
                        foreach (float u in new[] { -0.5f, 0.5f })
                        {
                            b.Add(PropKind.TallBookcase, f.P(u, 0.18f), f.Face);
                            b.Add(PropKind.TallBookcase, f.P(u, -0.18f), f.Back);
                        }
                        b.Item(f.P(R(-0.8f, 0.8f), Chance(0.5) ? 0.2f : -0.2f, BookcaseLevels[m_Rng.Next(3)]));
                    }
                    break;
                case RoomType.CopyRoom:
                    b.Add(PropKind.Desk, f.P(0, 0.4f), f.Face);
                    b.Add(PropKind.Desk, f.P(0, -0.4f), f.Back);
                    b.Add(PropKind.PaperStack, f.P(1.3f, 0), f.Face);
                    b.Item(f.P(R(-0.5f, 0.5f), 0.3f, 0.77f));
                    break;
                case RoomType.ControlRoom:
                    b.Add(PropKind.ConsoleDesk, f.P(0, 0), f.Face);
                    b.Add(PropKind.Chair, f.P(-0.6f, 0.85f), f.Back);
                    b.Add(PropKind.Chair, f.P(0.6f, 0.85f), f.Back);
                    b.Item(f.P(R(-0.9f, 0.9f), 0.2f, 0.77f));
                    break;
                case RoomType.Lab:
                    if (Chance(0.6))
                    {   // two benches back to back
                        b.Add(PropKind.LabBench, f.P(0, 0.4f), f.Face);
                        b.Add(PropKind.LabBench, f.P(0, -0.4f), f.Back);
                        b.Add(PropKind.Chair, f.P(R(-0.4f, 0.4f), 1.2f), f.Back);
                        b.Add(PropKind.Chair, f.P(R(-0.4f, 0.4f), -1.2f), f.Face);
                        b.Item(f.P(R(-0.6f, 0.6f), Chance(0.5) ? 0.5f : -0.5f, 0.91f));
                    }
                    else { b.Add(PropKind.RobotArm, f.P(0, 0), f.Face + m_Rng.Next(4) * 90f); b.Item(f.P(0.7f, 0.4f)); }
                    break;
                case RoomType.NapRoom:
                    if (Chance(0.5)) b.Add(PropKind.Hammock, f.P(0, 0), f.Face);
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.BeanBag, f.P(-1.0f, 0.2f), f.Face + 60f);
                        b.Add(PropKind.BeanBag, f.P(1.0f, 0.2f), f.Face - 60f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
                case RoomType.Studio:
                    if (Chance(0.5)) { b.Add(PropKind.DrumKit, f.P(0, 0), f.Face); }
                    else
                    {   // two voices at their mics, facing each other
                        b.Add(PropKind.MicStand, f.P(0, 0.45f), f.Back);
                        b.Add(PropKind.MicStand, f.P(0, -0.45f), f.Face);
                        b.Add(PropKind.BarStool, f.P(0, 1.0f), f.Back);
                        b.Add(PropKind.BarStool, f.P(0, -1.0f), f.Face);
                    }
                    break;
                case RoomType.LockerRoom:
                    b.Add(PropKind.Bench, f.P(0, 0), f.Face);
                    if (Chance(0.5)) b.Item(f.P(R(-0.5f, 0.5f), 0, 0.46f));
                    break;
                case RoomType.Infirmary:
                    b.Add(PropKind.HospitalBed, f.P(0, 0), f.Face);
                    b.Add(PropKind.PrivacyScreen, f.P(1.25f, 0), f.Face + 90f);
                    b.Item(f.P(0, 0.6f, 0.66f));
                    break;
                case RoomType.GameRoom:
                    int game = m_Rng.Next(3);
                    if (game == 0) { b.Add(PropKind.PoolTable, f.P(0, 0), f.Face); b.Item(f.P(R(-0.8f, 0.8f), R(-0.3f, 0.3f), 0.81f)); }
                    else if (game == 1) { b.Add(PropKind.Foosball, f.P(0, 0), f.Face); b.Item(f.P(R(-0.4f, 0.4f), 0, 0.77f)); }
                    else { b.Add(PropKind.PingPongTable, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0, 0.78f)); }
                    break;
                case RoomType.Server: // a cold aisle between two rows of racks
                    for (int i = -1; i <= 1; i++)
                    {
                        b.Add(PropKind.ServerRack, f.P(i * 0.72f, 1.25f), f.Back);
                        b.Add(PropKind.ServerRack, f.P(i * 0.72f, -1.25f), f.Face);
                    }
                    b.Item(f.P(R(-0.8f, 0.8f), 0));
                    break;
                case RoomType.Storage: // a wrapped pallet and a stack of boxes by the pile
                    b.Add(PropKind.Pallet, f.P(0, 0), m_Rng.Next(2) * 90f);
                    b.Add(PropKind.Boxes, f.P(0.2f, 1.2f), f.Face);
                    break;
                case RoomType.Lounge:
                    if (Chance(0.35)) { b.Add(PropKind.PingPongTable, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0, 0.78f)); }
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Sofa, f.P(0, -1.15f), f.Face);
                        b.Add(PropKind.Armchair, f.P(-1.35f, 0.2f), f.Face + 90f);
                        if (Chance(0.6)) b.Add(PropKind.Armchair, f.P(1.35f, 0.2f), f.Face - 90f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
                case RoomType.Lobby:
                    if (b.Pieces.Count == 0 && Chance(0.5))
                    {
                        b.Add(PropKind.ReceptionDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(-0.4f, 0.33f, 1.14f));
                    }
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Armchair, f.P(-1.1f, 0), f.Face + 90f);
                        b.Add(PropKind.Armchair, f.P(1.1f, 0), f.Face - 90f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
                case RoomType.Office:
                case RoomType.Hall:
                    b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                    b.Add(PropKind.Armchair, f.P(0, 0.85f), f.Back);
                    b.Add(PropKind.Armchair, f.P(0, -0.85f), f.Face);
                    b.Item(f.P(0, 0, 0.42f));
                    break;
            }
        }

        /// <summary>Back-to-back cluster of four desks with privacy screens; loot on some of them.</summary>
        void DeskCluster(Frame f, Batch b)
        {
            foreach (float u in new[] { -0.8f, 0.8f })
            {
                b.Add(PropKind.Desk, f.P(u, 0.4f), f.Face);
                b.Add(PropKind.Chair, f.P(u + R(-0.1f, 0.1f), 1.15f), f.Back + R(-12f, 12f));
                b.Add(PropKind.Desk, f.P(u, -0.4f), f.Back);
                b.Add(PropKind.Chair, f.P(u + R(-0.1f, 0.1f), -1.15f), f.Face + R(-12f, 12f));
                b.Add(PropKind.Partition, f.P(u, 0f), f.Face, solid: false);
                if (Chance(0.6)) b.Item(f.P(u + (Chance(0.5) ? -0.5f : 0.5f), Chance(0.5) ? 0.2f : -0.2f, DeskTop + 0.005f));
                if (Chance(0.35)) b.Add(PropKind.DeskLamp, f.P(u + 0.55f, 0.55f, DeskTop), f.Back, solid: false);
            }
        }

        /// <summary>
        /// A kitchen whose middle had no room for the table with chairs all round (its middle stays free for the robots'
        /// patrol stop): a round table with a chair on each of two sides and a narrower walkway, or else the table against a
        /// wall with chairs on its three open sides.
        /// </summary>
        void SmallTable(PlanRoom room)
        {
            if (FreeTable(room)) return;
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in FacesOf(room))
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                for (float t = 1.4f; t < w.Length - 1.4f; t += 0.25f)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    var f = new Frame(face, u, n);
                    var batch = new Batch();
                    var table = f.P(0, 0.5f + WallGap);
                    batch.Add(PropKind.RoundTable, table, 0);
                    foreach (var d in new[] { f.U, -f.U, f.N }) batch.Add(PropKind.Chair, table + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                    batch.Item(table + Vector3.up * 0.76f);
                    if (TryCommit(room, batch, 0.05f, front: n)) return;
                }
            }
        }

        bool FreeTable(PlanRoom room)
        {
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            var spots = new List<Vector3>();
            for (float x = b.xMin + 1.1f; x < b.xMax - 1.0f; x += 0.25f)
                for (float z = b.yMin + 1.1f; z < b.yMax - 1.0f; z += 0.25f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            var c = room.Center;
            spots.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            // the chairs across the room: the robots' stop is along its long side (LevelGenerator.PatrolCentre)
            bool wide = b.width >= b.height;
            // two chairs, else one (a doorway's free floor on one side)
            foreach (int chairs in new[] { 2, 1 })
                foreach (var s in spots)
                    foreach (float side in chairs == 2 ? new[] { 1f } : new[] { 1f, -1f })
                    {
                        var f = new Frame(s, wide ? Vector3.forward : Vector3.right, wide ? Vector3.right : Vector3.forward);
                        var batch = new Batch();
                        batch.Add(PropKind.RoundTable, f.P(0, 0), 0);
                        foreach (var d in chairs == 2 ? new[] { f.U, -f.U } : new[] { f.U * side })
                            batch.Add(PropKind.Chair, f.O + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        batch.Item(f.P(0, 0, 0.76f));
                        // a walkway round it, clear of other furniture (it may cross a doorway's or the patrol stop's free
                        // floor: those are walkways too)
                        var all = FootAt(PropKind.RoundTable, f.O, 0);
                        foreach (var p in batch.Pieces) all = Union(all, FootAt(p.Kind, p.Pos, p.Yaw));
                        if (m_Taken[room.Floor].Overlaps(Grow(all, 0.7f)) || !OnFloor(room.Floor, room.Id, all, 0.5f)) continue;
                        if (TryCommit(room, batch, 0.05f)) return true;
                    }
            return false;
        }

        void Chairs(Frame f, Batch b, int n)
        {
            // around a round table: on its four sides, facing it
            var sides = new[] { -f.N, f.U, f.N, -f.U };
            for (int i = 0; i < Mathf.Min(n, 4); i++)
            {
                var d = sides[i];
                b.Add(PropKind.Chair, f.O + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
            }
        }

        // ------------------------------------------------------------------ walls

        void Walls(PlanRoom room)
        {
            var list = FacesOf(room); // longest walls first: the big pieces get the long walls
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in list)
            {
                if (room.Type == RoomType.Corridor) continue; // corridors stay bare: they are the walkways
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                float t = R(0.15f, 0.9f);
                int fails = 0;
                while (t < w.Length - 0.3f && fails < 12)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    var batch = new Batch();
                    float width = WallGroup(room, batch, face, u, n);
                    if (width <= 0f) break;
                    // the group spans [t, t + width] along the wall: shift its pieces to the group's middle
                    var shift = u * (width / 2f);
                    for (int i = 0; i < batch.Pieces.Count; i++)
                    {
                        var p = batch.Pieces[i];
                        p.Pos += shift;
                        batch.Pieces[i] = p;
                    }
                    for (int i = 0; i < batch.Items.Count; i++) batch.Items[i] += shift;
                    for (int i = 0; i < batch.Drops.Count; i++) { var d = batch.Drops[i]; d.Spot += shift; batch.Drops[i] = d; }
                    if (t + width <= w.Length - 0.1f && Deep(room, face + shift, n, batch) && TryCommit(room, batch, front: n))
                    {
                        t += width + (room.Type == RoomType.Corridor ? R(4f, 10f) : R(0.15f, 1.2f));
                        fails = 0;
                    }
                    else { t += 0.5f; fails++; }
                }
            }
        }

        /// <summary>A corridor keeps 1.6 m of walkway in front of anything along its walls.</summary>
        bool Deep(PlanRoom room, Vector3 face, Vector3 n, Batch b, float walk = 1.7f)
        {
            // the whole footprint, pushed 1.7 m (walk) further into the room, must still be the room's floor (a walkway stays)
            var u = Vector3.Cross(Vector3.up, n);
            foreach (var p in b.Pieces)
            {
                var r = FootAt(p.Kind, p.Pos, p.Yaw);
                foreach (var c in new[] { new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMax, 0, r.yMax) })
                    for (float d = 0f; d <= walk + 0.01f; d += 0.25f)
                    {
                        var q = c + n * d;
                        if (!InRoom(room.Floor, room.Id, q.x, q.z)) return false;
                        if (d > 0.2f && m_Taken[room.Floor].Contains(new Vector2(q.x, q.z))) return false;
                    }
            }
            return true;
        }

        /// <summary>One group along a wall, laid out from the wall's face (u along it from 0 to the returned width, n into the room).</summary>
        float WallGroup(PlanRoom room, Batch b, Vector3 face, Vector3 u, Vector3 n)
        {
            var f = new Frame(face, u, n);
            float Single(PropKind k, float w)
            {
                b.Add(k, f.P(0, BackOf(k)), f.Face);
                return w;
            }
            int roll = m_Rng.Next(100);
            switch (room.Type)
            {
                case RoomType.Corridor:
                    return roll < 35 ? Single(PropKind.WaterCooler, 0.4f) : roll < 65 ? Single(PropKind.TrashBin, 0.45f)
                         : Single(PropKind.Cabinet, 0.95f);                                     // drawers to open instead of a plant
                case RoomType.Hall:
                    return roll < 50 ? Single(PropKind.Plant, 0.75f) // stair halls stay clear (no vending machines: they look like ATMs)
                         : roll < 70 ? Single(PropKind.WaterCooler, 0.4f) : roll < 85 ? Single(PropKind.CoatRack, 0.6f) : Single(PropKind.Armchair, 0.9f);
                case RoomType.Lobby:
                    if (roll < 25) { b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face); b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face); b.Item(f.P(0, 1.35f, 0.42f)); return 2.25f; }
                    if (roll < 45) { b.Add(PropKind.Lockers, f.P(-0.525f, BackOf(PropKind.Lockers)), f.Face); b.Add(PropKind.Lockers, f.P(0.525f, BackOf(PropKind.Lockers)), f.Face); return 2.1f; }
                    if (roll < 60) return Single(PropKind.Plant, 0.75f);                  // no vending machines
                    if (roll < 80) return Single(PropKind.Lockers, 1.05f);                 // lockers to open instead of a plant
                    if (roll < 90) return Single(PropKind.CoatRack, 0.6f);
                    return Single(PropKind.FloorLamp, 0.45f);
                case RoomType.Office:
                    if (roll < 30)
                    {   // a work desk against the wall
                        b.Add(PropKind.Desk, f.P(0, 0.4f + WallGap), f.Face);
                        b.Add(PropKind.Chair, f.P(R(-0.1f, 0.1f), 1.2f), f.Back + R(-15f, 15f));
                        if (Chance(0.6)) b.Add(PropKind.DeskLamp, f.P(0.55f, 0.3f, DeskTop), f.Face, solid: false);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.5f, DeskTop + 0.005f));
                        return 1.65f;
                    }
                    if (roll < 48)
                    {   // the manager's desk: bookcase behind, the desk facing the room, a guest chair
                        b.Add(PropKind.Bookshelf, f.P(0, BackOf(PropKind.Bookshelf)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 0.85f), f.Face);
                        b.Add(PropKind.Desk, f.P(0, 1.6f), f.Back);
                        b.Add(PropKind.Chair, f.P(0, 2.4f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 1.6f, DeskTop + 0.005f));
                        return 1.65f;
                    }
                    if (roll < 62) { Single(PropKind.Safe, 0.85f); b.Item(f.P(0, BackOf(PropKind.Safe), 1.02f)); return 0.85f; }
                    if (roll < 74) { b.Add(PropKind.Bookshelf, f.P(-0.51f, BackOf(PropKind.Bookshelf)), f.Face); b.Add(PropKind.Bookshelf, f.P(0.51f, BackOf(PropKind.Bookshelf)), f.Face); return 2.05f; }
                    if (roll < 84) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 92 && !m_Printer) { m_Printer = true; return Single(PropKind.Printer, 0.65f); }
                    return Single(PropKind.Lockers, 1.05f);
                case RoomType.OpenSpace:
                    if (roll < 10 && !m_Printer) { m_Printer = true; return Single(PropKind.Printer, 0.65f); } // one per room, rarely
                    if (roll < 50) { b.Add(PropKind.Cabinet, f.P(-0.475f, BackOf(PropKind.Cabinet)), f.Face); b.Add(PropKind.Cabinet, f.P(0.475f, BackOf(PropKind.Cabinet)), f.Face); b.Item(f.P(0.4f, BackOf(PropKind.Cabinet), CabinetTop)); return 1.9f; }
                    if (roll < 65) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 80) return Single(PropKind.Cabinet, 0.95f);
                    if (roll < 90) { b.Add(PropKind.Desk, f.P(0, 0.4f + WallGap), f.Face); b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back); b.Item(f.P(0.3f, 0.5f, DeskTop + 0.005f)); return 1.65f; }
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.Meeting:
                    if (roll < 30) { Single(PropKind.TvStand, 1.45f); return 1.45f; }
                    if (roll < 55) { b.Add(PropKind.Whiteboard, f.P(0, 0.14f), f.Face); return 1.95f; }
                    if (roll < 75) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 88) return Single(PropKind.WaterCooler, 0.4f);
                    return Single(PropKind.Cabinet, 0.95f);
                case RoomType.Kitchen:
                    if (!m_Fridge) return Single(PropKind.Fridge, 0.75f); // every kitchen has its fridge
                    if (!m_Table)
                    {   // and its table: by the wall, a chair on each side of it
                        var table = f.P(0, 0.5f + WallGap);
                        b.Add(PropKind.RoundTable, table, 0);
                        foreach (var d in new[] { f.U, -f.U }) b.Add(PropKind.Chair, table + d * 0.95f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        b.Item(table + Vector3.up * 0.76f);
                        return 2.6f;
                    }
                    if (roll < 30)
                    {   // the kitchen run: sink, cooker, worktop with the microwave and the coffee machine, cupboards over it
                        b.Add(PropKind.KitchenSink, f.P(-1.52f, BackOf(PropKind.KitchenSink)), f.Face);
                        b.Add(PropKind.Stove, f.P(-0.62f, BackOf(PropKind.Stove)), f.Face);
                        b.Add(PropKind.KitchenCounter, f.P(0.92f, BackOf(PropKind.KitchenCounter)), f.Face);
                        // the counter has its microwave at its local −X: the coffee machine on the other end
                        var cx = Quaternion.Euler(0, f.Face, 0) * Vector3.right;
                        b.Add(PropKind.CoffeeMachine, f.P(0.92f, BackOf(PropKind.KitchenCounter) + 0.05f, 0.93f) + cx * 0.75f, f.Face, solid: false);
                        b.Add(PropKind.WallCabinet, f.P(-1.52f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Add(PropKind.WallCabinet, f.P(0.92f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Item(f.P(0.9f, BackOf(PropKind.KitchenCounter), 0.93f));
                        return 4.3f;
                    }
                    if (roll < 48)
                    {   // a shorter run: the sink and a worktop
                        b.Add(PropKind.KitchenSink, f.P(-1.22f, BackOf(PropKind.KitchenSink)), f.Face);
                        b.Add(PropKind.KitchenCounter, f.P(0.62f, BackOf(PropKind.KitchenCounter)), f.Face);
                        b.Add(PropKind.WallCabinet, f.P(-1.22f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Item(f.P(0.3f, BackOf(PropKind.KitchenCounter), 0.93f));
                        return 3.7f;
                    }
                    // the fridge is there already (one a kitchen): drawers, the water, the bin
                    if (roll < 66) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 84) return Single(PropKind.WaterCooler, 0.4f);
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.Server:
                    if (roll < 70)
                    {
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.ServerRack, f.P(i * 0.72f, BackOf(PropKind.ServerRack)), f.Face);
                        return 2.2f;
                    }
                    Single(PropKind.Cabinet, 0.95f);
                    b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop));
                    return 0.95f;
                case RoomType.Storage:
                    if (roll < 80)
                    {
                        // shelves all round: loot on the upper boards from the start, the bottom board for the workers
                        b.Add(PropKind.Shelf, f.P(0, BackOf(PropKind.Shelf)), f.Face);
                        if (Chance(0.7)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.Shelf), ShelfLevels[1 + m_Rng.Next(2)]));
                        foreach (float along in new[] { -0.5f, 0.5f })
                            b.Drops.Add(new StorageDrop { Spot = f.P(along, BackOf(PropKind.Shelf), Enemies.CargoMotion.ShelfTop + 0.02f), Facing = -f.N, Shelf = true });
                        return 2.05f;
                    }
                    b.Add(PropKind.Boxes, f.P(-0.2f, 0.45f), f.Face + m_Rng.Next(4) * 90f);
                    return 1.2f;
                case RoomType.Restroom:
                    if (roll < 55)
                    {
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.ToiletStall, f.P(i * 1.06f, BackOf(PropKind.ToiletStall)), f.Face);
                        b.Item(f.P(0, 2.1f));
                        return 3.2f;
                    }
                    if (roll < 75)
                    {   // basins with mirrors, the hand dryer beside them
                        b.Add(PropKind.MirrorSink, f.P(-0.62f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.MirrorSink, f.P(0.18f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.HandDryer, f.P(0.82f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                        if (Chance(0.4)) b.Item(f.P(-0.62f, BackOf(PropKind.MirrorSink), 0.86f));
                        return 2.0f;
                    }
                    if (roll < 90) return Row(f, b, PropKind.Urinal, 3, 0.7f);
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.Lounge:
                    if (roll < 25)
                    {
                        b.Add(PropKind.TvStand, f.P(0, BackOf(PropKind.TvStand)), f.Face);
                        b.Add(PropKind.Armchair, f.P(-0.6f, 2.0f), f.Back);
                        b.Add(PropKind.Armchair, f.P(0.6f, 2.0f), f.Back);
                        return 1.45f;
                    }
                    if (roll < 45)
                    {
                        b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face);
                        b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face);
                        b.Item(f.P(0, BackOf(PropKind.Sofa), 0.46f));
                        b.Item(f.P(0, 1.35f, 0.42f));
                        return 2.25f;
                    }
                    if (roll < 60) return Single(PropKind.ArcadeMachine, 0.8f);
                    if (roll < 72) return Single(PropKind.Plant, 0.75f);                  // no vending machines
                    if (roll < 82) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 92) { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
                    return Single(PropKind.FloorLamp, 0.45f);
                case RoomType.Gym:
                    if (roll < 30) return Row(f, b, PropKind.Treadmill, 2, 0.95f);
                    if (roll < 50) return Row(f, b, PropKind.ExerciseBike, 2, 0.65f);
                    if (roll < 68) { Single(PropKind.DumbbellRack, 1.5f); return 1.5f; }
                    if (roll < 80) { b.Add(PropKind.PunchingBag, f.P(0, 0.75f), f.Face); return 0.8f; }
                    if (roll < 90) return Single(PropKind.WaterCooler, 0.4f);
                    return Single(PropKind.Bench, 1.7f);
                case RoomType.Library:
                    if (roll < 55)
                    {
                        Row(f, b, PropKind.TallBookcase, 2, 1.02f);
                        if (Chance(0.6)) b.Item(f.P(R(-0.8f, 0.8f), BackOf(PropKind.TallBookcase), BookcaseLevels[m_Rng.Next(3)]));
                        return 2.05f;
                    }
                    if (roll < 72)
                    {   // a reading corner: an armchair under a floor lamp
                        b.Add(PropKind.Armchair, f.P(-0.3f, 0.5f), f.Face);
                        b.Add(PropKind.FloorLamp, f.P(0.45f, 0.3f), f.Face);
                        return 1.4f;
                    }
                    if (roll < 82) { b.Add(PropKind.Globe, f.P(0, 0.4f), f.Face + R(-40f, 40f)); return 0.7f; }
                    if (roll < 92) return Row(f, b, PropKind.Bookshelf, 2, 1.02f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.CopyRoom:
                    if (roll < 32) { Single(PropKind.Copier, 1.0f); b.Item(f.P(0.2f, BackOf(PropKind.Copier), 1.05f)); return 1.0f; }
                    if (roll < 54) { Single(PropKind.MailShelf, 1.25f); b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.MailShelf), 0.87f)); return 1.25f; }
                    if (roll < 68) return Single(PropKind.PaperStack, 0.85f);
                    if (roll < 80) return Single(PropKind.Shredder, 0.5f);
                    if (roll < 90) return Single(PropKind.Printer, 0.65f);
                    { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                case RoomType.ControlRoom:
                    if (roll < 50)
                    {   // the video wall, the operators' console in front of it facing it
                        b.Add(PropKind.MonitorWall, f.P(0, BackOf(PropKind.MonitorWall)), f.Face);
                        b.Add(PropKind.ConsoleDesk, f.P(0, 1.55f), f.Face); // its screens face the operators, who face the wall
                        b.Add(PropKind.Chair, f.P(-0.6f, 2.35f), f.Back);
                        b.Add(PropKind.Chair, f.P(0.6f, 2.35f), f.Back);
                        b.Item(f.P(R(-0.9f, 0.9f), 1.45f, 0.77f));
                        return 2.45f;
                    }
                    if (roll < 72) return Row(f, b, PropKind.ServerRack, 2, 0.72f);
                    if (roll < 86) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    return Single(PropKind.WaterCooler, 0.4f);
                case RoomType.Lab:
                    if (roll < 40)
                    {
                        b.Add(PropKind.LabBench, f.P(0, BackOf(PropKind.LabBench)), f.Face);
                        b.Add(PropKind.Chair, f.P(R(-0.3f, 0.3f), 1.2f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.LabBench) + 0.1f, 0.91f));
                        return 1.85f;
                    }
                    if (roll < 60) { Single(PropKind.PartsCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.PartsCabinet), 1.41f)); return 0.95f; }
                    if (roll < 75) { b.Add(PropKind.RobotArm, f.P(0, BackOf(PropKind.RobotArm)), f.Face); return 0.7f; }
                    if (roll < 88) { Single(PropKind.Shelf, 2.05f); if (Chance(0.6)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.Shelf), ShelfLevels[m_Rng.Next(3)])); return 2.05f; }
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.NapRoom:
                    if (roll < 45) { Row(f, b, PropKind.SleepPod, 2, 1.15f); b.Item(f.P(R(-0.6f, 0.6f), 2.35f)); return 2.3f; }
                    if (roll < 65) { b.Add(PropKind.Hammock, f.P(0, 0.55f), f.Face); return 2.65f; }
                    if (roll < 80) { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
                    if (roll < 90) return Single(PropKind.FloorLamp, 0.45f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Studio:
                    if (roll < 35)
                    {
                        b.Add(PropKind.MixingDesk, f.P(0, BackOf(PropKind.MixingDesk)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.MixingDesk) + 0.25f, 0.76f));
                        return 1.65f;
                    }
                    if (roll < 62) { b.Add(PropKind.AcousticPanel, f.P(-0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); b.Add(PropKind.AcousticPanel, f.P(0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); return 2.45f; }
                    if (roll < 76) { b.Add(PropKind.DrumKit, f.P(0, 0.75f), f.Face); return 1.65f; }
                    if (roll < 88) { b.Add(PropKind.MicStand, f.P(0, 0.5f), f.Face); return 0.55f; }
                    return Single(PropKind.Sofa, 2.25f);
                case RoomType.LockerRoom:
                    if (roll < 40) return Row(f, b, PropKind.Lockers, 3, 1.05f);
                    if (roll < 60) return Row(f, b, PropKind.ShowerStall, 2, 0.98f);
                    if (roll < 75)
                    {
                        b.Add(PropKind.MirrorSink, f.P(-0.25f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.HandDryer, f.P(0.45f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                        return 1.2f;
                    }
                    if (roll < 90) { b.Add(PropKind.Bench, f.P(0, 0.35f), f.Face); if (Chance(0.4)) b.Item(f.P(R(-0.5f, 0.5f), 0.35f, 0.46f)); return 1.65f; }
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.Infirmary:
                    if (roll < 35) { Row(f, b, PropKind.HospitalBed, 2, 1.15f); b.Item(f.P(R(-0.5f, 0.5f), 1.2f, 0.66f)); return 2.3f; }
                    if (roll < 55) { Single(PropKind.MedicineCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.MedicineCabinet), 1.65f)); return 0.95f; }
                    if (roll < 70) { b.Add(PropKind.PrivacyScreen, f.P(0, 0.35f), f.Face); return 1.65f; }
                    if (roll < 82) { Single(PropKind.MirrorSink, 0.75f); return 0.75f; }
                    if (roll < 92) return Single(PropKind.Cabinet, 0.95f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.GameRoom:
                    if (roll < 22) { b.Add(PropKind.DartBoard, f.P(0, BackOf(PropKind.DartBoard)), f.Face, solid: false); return 0.65f; }
                    if (roll < 42) return Row(f, b, PropKind.ArcadeMachine, 2, 0.8f);
                    if (roll < 62)
                    {   // the bar with stools in front of it
                        b.Add(PropKind.BarCounter, f.P(0, BackOf(PropKind.BarCounter)), f.Face);
                        b.Add(PropKind.BarStool, f.P(-0.55f, 0.95f), f.Back);
                        b.Add(PropKind.BarStool, f.P(0.55f, 0.95f), f.Back);
                        b.Item(f.P(R(-0.7f, 0.7f), BackOf(PropKind.BarCounter), 1.06f));
                        return 2.05f;
                    }
                    if (roll < 76)
                    {
                        b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face);
                        b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face);
                        b.Item(f.P(0, 1.35f, 0.42f));
                        return 2.25f;
                    }
                    if (roll < 90) return Single(PropKind.VendingMachine, 0.95f);
                    { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
            }
            return 0f;
        }

        /// <summary><paramref name="n"/> pieces side by side along a wall, <paramref name="step"/> apart, backs to it; returns the width.</summary>
        float Row(Frame f, Batch b, PropKind k, int n, float step)
        {
            for (int i = 0; i < n; i++) b.Add(k, f.P((i - (n - 1) / 2f) * step, BackOf(k)), f.Face);
            return n * step + 0.05f;
        }

        // ------------------------------------------------------------------ corners

        /// <summary>Plants, floor lamps, bins and coat racks in the free inner corners of a room.</summary>
        /// <summary>
        /// A piece the room must have and the walls had no run for (a kitchen's fridge): against any bit of wall, its back to
        /// it, wherever it fits.
        /// </summary>
        void AgainstAnyWall(PlanRoom room, PropKind kind, bool walkway = false)
        {
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            for (float x = b.xMin + 0.25f; x < b.xMax; x += 0.25f)
                for (float z = b.yMin + 0.25f; z < b.yMax; z += 0.25f)
                {
                    if (!InRoom(room.Floor, room.Id, x, z)) continue;
                    foreach (var d in dirs)
                    {
                        // the wall right behind it (a model's back sits on BackOf from the wall's face)
                        var behind = new Vector3(x, 0f, z) + d * (BackOf(kind) + 0.15f);
                        if (InRoom(room.Floor, room.Id, behind.x, behind.z)) continue;
                        var batch = new Batch();
                        batch.Add(kind, new Vector3(x, y, z), Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        if (TryCommit(room, batch, 0.05f, front: walkway ? -d : null)) return;
                    }
                }
        }

        void Corners(PlanRoom room)
        {
            if (room.Type == RoomType.Kitchen && !m_Fridge) AgainstAnyWall(room, PropKind.Fridge);
            if (room.Type is RoomType.Corridor or RoomType.Server) return;
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            int placed = 0, max = Mathf.Clamp(Mathf.RoundToInt(room.Area / 25f), 1, 5);
            for (float x = b.xMin + 0.4f; x < b.xMax && placed < max; x += 0.5f)
                for (float z = b.yMin + 0.4f; z < b.yMax && placed < max; z += 0.5f)
                {
                    if (!InRoom(room.Floor, room.Id, x, z)) continue;
                    // a corner: room cells on two sides, a wall within 0.6 m on two perpendicular sides
                    bool wx = !InRoom(room.Floor, room.Id, x - 0.6f, z) || !InRoom(room.Floor, room.Id, x + 0.6f, z);
                    bool wz = !InRoom(room.Floor, room.Id, x, z - 0.6f) || !InRoom(room.Floor, room.Id, x, z + 0.6f);
                    if (!wx || !wz || !Chance(0.35)) continue;
                    var kind = room.Type switch
                    {
                        RoomType.Lounge or RoomType.Lobby => Chance(0.5) ? PropKind.FloorLamp : PropKind.Plant,
                        RoomType.Office => Chance(0.6) ? PropKind.Plant : PropKind.CoatRack,
                        RoomType.Storage => PropKind.Boxes,
                        // the themed rooms' own odds and ends
                        RoomType.Gym => Chance(0.5) ? PropKind.PunchingBag : PropKind.WaterCooler,
                        RoomType.Library => Chance(0.5) ? PropKind.Globe : PropKind.FloorLamp,
                        RoomType.CopyRoom => Chance(0.5) ? PropKind.Shredder : PropKind.PaperStack,
                        RoomType.Lab => Chance(0.5) ? PropKind.RobotArm : PropKind.TrashBin,
                        RoomType.NapRoom => Chance(0.5) ? PropKind.BeanBag : PropKind.FloorLamp,
                        RoomType.Studio => Chance(0.5) ? PropKind.MicStand : PropKind.FloorLamp,
                        RoomType.Infirmary => Chance(0.5) ? PropKind.PrivacyScreen : PropKind.Plant,
                        RoomType.GameRoom => Chance(0.5) ? PropKind.BarStool : PropKind.BeanBag,
                        RoomType.LockerRoom => PropKind.TrashBin,
                        _ => Chance(0.7) ? PropKind.Plant : PropKind.TrashBin,
                    };
                    var p = new Vector3(x, y, z);
                    var batch = new Batch();
                    batch.Add(kind, p, m_Rng.Next(4) * 90f);
                    if (TryCommit(room, batch, 0.05f)) placed++;
                }
        }
    }
}
