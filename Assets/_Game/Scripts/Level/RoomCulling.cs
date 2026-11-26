using System.Collections.Generic;
using System.Threading.Tasks;
using Stealth.Enemies;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The contents of rooms the camera cannot see into are not drawn (rendering only, every peer; ticked by
    /// <see cref="LightBudget"/> on the level's root). A big house looked along its length had nearly all of itself in the
    /// frustum — 40 million triangles and four thousand renderers a frame behind walls. What a room holds: its furniture
    /// drawn merged (<see cref="PropBatches.RoomBatches"/>), the furniture that draws itself (cupboards that open), its
    /// doors, light switches, vent grates and the glowing panels of its lamps. The house's shell — walls, slabs, windows,
    /// the style's decor — and everything that moves (robots, loot, players) is always drawn. Which rooms are seen is
    /// <see cref="RoomSight"/>'s, worked out off the main thread as the camera moves or a door opens or shuts (a shut door
    /// hides the room behind it; one that starts to open shows it within the frame when it is near); a room stays drawn a
    /// moment after it was last seen, and a thing marked through walls (<see cref="MarkedThings"/>) is drawn wherever it is.
    /// </summary>
    public static class RoomCulling
    {
        /// <summary>Off: everything is drawn (the profiling scenario compares).</summary>
        public static bool Enabled = true;

        /// <summary>A room stays drawn this long after it was last seen, seconds.</summary>
        const float Linger = 1f;
        /// <summary>The camera moved this far: the rooms are looked at again, metres.</summary>
        const float MoveStep = 0.3f;
        /// <summary>A jump this far (a teleport, a fall) is looked at within the frame, metres.</summary>
        const float Jump = 3f;
        /// <summary>A door this near that opens or shuts is looked at within the frame, metres.</summary>
        const float DoorNear = 12f;
        /// <summary>The rooms' state is put on their renderers this often, seconds (marks and the linger change it between looks).</summary>
        const float ApplyEvery = 0.25f;

        /// <summary>Root pieces of the house that belong to a room (beside its furniture): by the start of their names.</summary>
        static readonly string[] RoomPieces = { "Door", "LightSwitch", "VentGrate", "Light_" };

        sealed class Room
        {
            public readonly List<Renderer> Batches = new(), Things = new();
            public bool Shown = true;
            public float SeenAt = float.NegativeInfinity;
        }

        /// <summary>A piece that stands in several rooms (a door between two): drawn while any of them is.</summary>
        struct Shared
        {
            public Renderer Renderer;
            public int[] Rooms;
        }

        /// <summary>A door of the house: the plan's opening it stands in and what it is to the eye when shut (<see cref="RoomSight.ShutState"/>).</summary>
        struct Door
        {
            public AutoDoor It;
            public int Opening;
            public byte Shut;
        }

        static readonly List<Shared> s_Shared = new();
        static readonly List<Door> s_Doors = new();
        /// <summary>What stands in each opening of the plan, as the last look had it (the look's thread reads it: written only between looks).</summary>
        static byte[] s_DoorStates;
        static Room[] s_Rooms;
        static RoomSight s_Sight;
        static GameObject s_Root;
        static int s_Version = -1;
        static bool[] s_Seen, s_Next;
        static bool s_All = true;
        static Task<bool> s_Task;
        static Vector3 s_AskedAt;
        static bool s_Asked;
        static float s_NextApply;

        /// <summary>Rooms of the house whose contents are not drawn now / rooms it has.</summary>
        public static int Hidden { get; private set; }
        public static int Rooms => s_Rooms?.Length ?? 0;

        /// <summary>
        /// Once a frame, with the camera's place. A fault here must not take its caller down (the lamps' budget): it is
        /// logged once, everything draws again and the culling stays off.
        /// </summary>
        public static void Tick(Vector3 eye, float now)
        {
            if (s_Broken) return;
            try { Look(eye, now); }
            catch (System.Exception e)
            {
                s_Broken = true;
                Debug.LogException(e);
                try { Forget(true); } catch (System.Exception) { s_Rooms = null; }
            }
        }

        static bool s_Broken;

        static void Look(Vector3 eye, float now)
        {
            var root = ActiveLevel.Root;
            var layout = ActiveLevel.Layout;
            if (root == null || layout == null || layout.Plan == null || !ActiveLevel.Ready || !PropBatches.Complete)
            {
                Forget(root != null && root == s_Root);
                return;
            }
            if (root != s_Root || s_Version != PropBatches.Version)
            {
                Forget(root == s_Root);
                Register(root, layout);
            }
            if (s_Rooms == null) return;
            if (s_Task != null && s_Task.IsCompleted)
            {
                if (s_Task.Status == TaskStatus.RanToCompletion) { (s_Seen, s_Next) = (s_Next, s_Seen); s_All = !s_Task.Result; }
                s_Task = null;
                s_NextApply = 0f;
            }
            float moved = s_Asked ? (eye - s_AskedAt).magnitude : float.MaxValue;
            if (s_Task == null)
            {
                // the doors as they stand now: one that opened or shut asks for a look as moving does
                bool doorMoved = false, doorNear = false;
                foreach (var d in s_Doors)
                {
                    if (d.It == null) continue;
                    byte state = d.It.Shut ? d.Shut : RoomSight.Open;
                    if (s_DoorStates[d.Opening] == state) continue;
                    s_DoorStates[d.Opening] = state;
                    doorMoved = true;
                    doorNear |= (d.It.transform.position - eye).sqrMagnitude < DoorNear * DoorNear;
                }
                if (moved >= MoveStep || doorMoved)
                {
                    s_AskedAt = eye;
                    s_Asked = true;
                    var sight = s_Sight;
                    var into = s_Next;
                    var doors = s_DoorStates;
                    if (moved >= Jump || doorNear)
                    {
                        // a teleport, a fall through a hole, the first look, a door opening by the camera: at once
                        // (a frame of a room without its furniture shows)
                        s_All = !sight.See(eye, into, doors);
                        (s_Seen, s_Next) = (s_Next, s_Seen);
                        s_NextApply = 0f;
                    }
                    else s_Task = Task.Run(() => sight.See(eye, into, doors));
                }
            }
            if (now < s_NextApply) return;
            s_NextApply = now + ApplyEvery;
            Apply(now);
        }

        static void Apply(float now)
        {
            int hidden = 0;
            bool all = !Enabled || s_All;
            for (int i = 0; i < s_Rooms.Length; i++)
            {
                var room = s_Rooms[i];
                if (room == null) continue;
                if (all || s_Seen[i]) room.SeenAt = now;
                bool show = now - room.SeenAt < Linger;
                if (!show) hidden++;
                if (show != room.Shown)
                {
                    room.Shown = show;
                    foreach (var b in room.Batches)
                        if (b != null) b.forceRenderingOff = !show;
                }
                else if (show) continue; // (drawn and was drawn: nothing to do)
                // its things one by one: a thing marked through walls draws wherever it is
                foreach (var t in room.Things)
                {
                    if (t == null) continue;
                    int layer = t.gameObject.layer;
                    bool off = !show && layer != MarkedThings.XrayLayer && layer != Detector.XrayLayer;
                    if (t.forceRenderingOff != off) t.forceRenderingOff = off;
                }
            }
            foreach (var s in s_Shared)
            {
                if (s.Renderer == null) continue;
                bool show = false;
                foreach (int id in s.Rooms) show |= s_Rooms[id] == null || s_Rooms[id].Shown;
                int layer = s.Renderer.gameObject.layer;
                bool off = !show && layer != MarkedThings.XrayLayer && layer != Detector.XrayLayer;
                if (s.Renderer.forceRenderingOff != off) s.Renderer.forceRenderingOff = off;
            }
            Hidden = hidden;
        }

        /// <summary>The house's rooms and what each holds, as it stands merged now.</summary>
        static void Register(GameObject root, LevelLayout layout)
        {
            s_Root = root;
            s_Version = PropBatches.Version;
            var plan = layout.Plan;
            s_Sight = new RoomSight(layout);
            s_Rooms = new Room[plan.Rooms.Count];
            s_Shared.Clear();
            s_Doors.Clear();
            s_DoorStates = new byte[s_Sight.Openings];
            foreach (var door in AutoDoor.All)
            {
                if (door == null || door.Spot < 0 || !door.transform.IsChildOf(root.transform)) continue;
                int opening = s_Sight.OpeningAt(door.transform.position);
                // (the outer doors come after the inner ones and have no look of their own in the layout: glazed, as a street door)
                byte shut = door.Spot < layout.AutoDoors.Count ? RoomSight.ShutState(DoorRules.Of(layout, door.Spot).Kind) : RoomSight.ShutSlot;
                if (opening >= 0 && shut != RoomSight.Open) s_Doors.Add(new Door { It = door, Opening = opening, Shut = shut });
            }
            s_Seen = new bool[s_Rooms.Length];
            s_Next = new bool[s_Rooms.Length];
            s_All = true;
            s_Asked = false;
            s_Task = null;
            s_NextApply = 0f;
            Room Of(int id) => id < 0 || id >= s_Rooms.Length ? null : s_Rooms[id] ??= new Room();
            foreach (var (room, renderer) in PropBatches.RoomBatches)
                if (renderer != null) Of(room)?.Batches.Add(renderer);
            var found = new List<MeshRenderer>();
            // the furniture that draws itself: in the room it stands in
            foreach (var prop in ActiveLevel.Props)
            {
                if (prop == null) continue;
                var room = plan.RoomAt(prop.transform.position + Vector3.up * 0.3f);
                if (room == null) continue;
                prop.GetComponentsInChildren(found);
                foreach (var r in found)
                    if (r.enabled && !r.forceRenderingOff) Of(room.Id).Things.Add(r);
            }
            // doors, switches, grates, lamps: in every room they touch (a door stands between two)
            var t = root.transform;
            var rooms = new HashSet<int>();
            for (int i = 0; i < t.childCount; i++)
            {
                var piece = t.GetChild(i);
                if (!IsRoomPiece(piece.name)) continue;
                piece.GetComponentsInChildren(found);
                rooms.Clear();
                var p = piece.position;
                foreach (var d in Around)
                {
                    var room = plan.RoomAt(p + d);
                    if (room != null) rooms.Add(room.Id);
                }
                if (rooms.Count == 0) continue;
                int[] ids = null;
                foreach (var r in found)
                {
                    if (!r.enabled || r.forceRenderingOff) continue;
                    if (rooms.Count == 1) { foreach (int id in rooms) Of(id).Things.Add(r); continue; }
                    if (ids == null)
                    {
                        ids = new int[rooms.Count];
                        rooms.CopyTo(ids);
                        foreach (int id in ids) Of(id); // (a room of its own state, though it may hold nothing else)
                    }
                    s_Shared.Add(new Shared { Renderer = r, Rooms = ids });
                }
            }
        }

        /// <summary>Where a piece on a wall's line is asked for its rooms: either side of it, both ways.</summary>
        static readonly Vector3[] Around =
        {
            new(0f, 0.3f, 0f), new(0.6f, 0.3f, 0f), new(-0.6f, 0.3f, 0f), new(0f, 0.3f, 0.6f), new(0f, 0.3f, -0.6f),
        };

        static bool IsRoomPiece(string name)
        {
            foreach (var n in RoomPieces)
                if (name.StartsWith(n, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Nothing is known any more; with <paramref name="show"/>, what was hidden draws again first (the house still stands).</summary>
        static void Forget(bool show)
        {
            if (s_Rooms == null) return;
            if (show)
                foreach (var room in s_Rooms)
                {
                    if (room == null) continue;
                    foreach (var b in room.Batches) if (b != null) b.forceRenderingOff = false;
                    foreach (var t in room.Things) if (t != null) t.forceRenderingOff = false;
                }
            if (show)
                foreach (var s in s_Shared) if (s.Renderer != null) s.Renderer.forceRenderingOff = false;
            s_Shared.Clear();
            s_Doors.Clear();
            s_Rooms = null;
            s_Sight = null;
            s_Root = null;
            s_Task = null;
            s_Version = -1;
            Hidden = 0;
        }
    }
}
