using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    public enum RoomType { Lobby, OpenSpace, Office, Meeting, Kitchen, Server, Storage, Restroom, Lounge, Corridor, Hall }
    /// <summary>
    /// Vent = a wall with a low crawl hole: crouching players fit, robots (2 m) do not. Squeeze = a narrow slot at full
    /// height: a player squeezes through sideways, robots (navmesh agents 1 m wide) do not fit.
    /// </summary>
    public enum EdgeType { Wall, Door, Open, Vent, Squeeze }
    public enum PropKind { Desk, Chair, ServerRack, Cabinet, MeetingTable, Plant, KitchenCounter, Hatch, Shelf, Boxes, WaterCooler, Whiteboard, Sofa, ToiletStall,
        Partition, Bookshelf, Printer, Fridge, CoffeeMachine, RoundTable, Armchair, CoffeeTable, TvStand, ReceptionDesk,
        TrashBin, CoatRack, Lockers, Pallet, WashBasin, VendingMachine, DeskLamp, FloorLamp,
        PingPongTable, BeanBag, ArcadeMachine, Safe, Terminal }

    /// <summary>What a security terminal does: show the house's cameras, or hack robots and devices from afar.</summary>
    public enum TerminalKind : byte { Cameras, Hacking }

    public struct PropPlacement
    {
        public PropKind Kind;
        public Vector3 Position;
        public float RotationY;
    }

    public struct RobotSpawn
    {
        public RobotKind Kind;
        public int Floor;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>Laser gate patterns: one low beam, three blinking beams, one moving beam (from the third house on).</summary>
    public enum LaserPattern : byte { Low, Blinking, Moving }

    public struct LaserSpot
    {
        public LaserPattern Kind;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Width;
    }

    /// <summary>
    /// A staircase in the corner of a room: it starts on <see cref="Floor"/> and arrives on Floor + 1 in the same corner
    /// (where the slab has a hole). <see cref="Origin"/> is the corner point (world x/z), <see cref="Corner"/> (±1, ±1)
    /// which corner of the room it is: the stair lies towards −Corner from the origin.
    /// </summary>
    public struct Stair
    {
        public int Floor;
        public Vector2 Origin;
        public Vector2Int Corner;
        /// <summary>The room it starts in and the room it arrives in (plan ids).</summary>
        public int Room, RoomAbove;
    }

    /// <summary>An opening in a wall: a doorway, a vent, a slot, an open passage.</summary>
    public struct Doorway
    {
        public EdgeType Kind;
        /// <summary>Bottom centre on the wall's centre line; the rotation's local X runs along the wall, Z across it.</summary>
        public Vector3 Position;
        public Quaternion Rotation;
        public float Width;
        public int Floor, RoomA, RoomB, Wall;
        public bool Exterior;
        /// <summary>A vent under the ceiling, climbed to by the crates under it (<see cref="PlanOpening.High"/>).</summary>
        public bool High;
    }

    /// <summary>Pure data describing a generated building. Identical on every peer for the same seed.</summary>
    public class LevelLayout
    {
        public int Seed, LevelIndex, Floors;
        /// <summary>Team size the house was generated for (a bigger team gets a bigger house).</summary>
        public int Players = 1;
        /// <summary>The floor plan: rooms of any size and shape, walls, openings, atriums.</summary>
        public HousePlan Plan;
        /// <summary>What the mass of the building is (bar, L, T, U, courtyard, H): for logs and the route panel.</summary>
        public string Mass = "";
        /// <summary>Which variant of the plan was taken (0 = the first; more when earlier ones failed the walk check).</summary>
        public int PlanAttempt;
        /// <summary>What kind of building this is (office, business centre, data centre, warehouse, headquarters).</summary>
        public HouseKind Kind;
        /// <summary>The lobby (plan room id): the hatch is in it.</summary>
        public int LobbyRoom;
        public readonly List<Stair> Stairs = new();
        /// <summary>Every opening of the plan as a pose (doors between rooms, exterior doors, vents, slots, open passages).</summary>
        public readonly List<Doorway> Doorways = new();
        public Vector3 HatchPosition;
        public readonly List<Vector3> PlayerSpawns = new();
        public readonly List<Vector3> ItemSpots = new();
        public readonly List<Vector3> PatrolPoints = new();
        public readonly List<Pose> CameraSpots = new();
        public readonly List<LaserSpot> LaserSpots = new();
        public readonly List<Pose> AutoDoors = new(); // sliding doors in doorways (local X along the wall)
        /// <summary>Indices into <see cref="AutoDoors"/> that are locked: players hack them open, robots pass.</summary>
        public readonly HashSet<int> LockedDoors = new();
        /// <summary>Security terminals: index in <see cref="Props"/> (a desk turned into a console) and what it does.</summary>
        public readonly List<(int prop, TerminalKind kind)> Terminals = new();
        public readonly List<RobotSpawn> RobotSpawns = new();
        public readonly List<PropPlacement> Props = new();
        public Bounds BuildingBounds;

        /// <summary>Size of the building's bounding box, metres (x, z).</summary>
        public float SizeX => Plan != null ? Plan.SizeX : 0f;
        public float SizeZ => Plan != null ? Plan.SizeZ : 0f;

        public IReadOnlyList<PlanRoom> Rooms => Plan.Rooms;
        public PlanRoom Lobby => Plan.Rooms[LobbyRoom];

        /// <summary>Floor area of the whole house in "old rooms" (100 m² each): counts of guards and loot scale with it.</summary>
        public float RoomUnits
        {
            get
            {
                float a = 0f;
                foreach (var r in Plan.Rooms) a += r.Area;
                return a / 100f;
            }
        }

        public static float FloorY(int floor) => floor * LevelMetrics.FloorHeight;

        public static int FloorOf(float y) => Mathf.Max(0, Mathf.FloorToInt((y + 0.75f) / LevelMetrics.FloorHeight));

        /// <summary>The room at <paramref name="p"/> (its floor from p.y), or null between rooms / outside.</summary>
        public PlanRoom RoomAt(Vector3 p) => Plan?.RoomAt(p);

        /// <summary>Center of the room containing <paramref name="p"/> (at the height of p's floor); p itself outside rooms.</summary>
        public Vector3 RoomCenterAt(Vector3 p)
        {
            var r = RoomAt(p);
            return r != null ? r.Center : new Vector3(p.x, FloorY(FloorOf(p.y)), p.z);
        }

        /// <summary>
        /// The rectangular part of the room at <paramref name="p"/> it stands in (world x/z): what a wall crawler walks
        /// around and a ceiling vacuum sweeps. A 10 × 10 m square around p outside rooms.
        /// </summary>
        public Rect RoomRectAt(Vector3 p)
        {
            var r = RoomAt(p);
            if (r == null) return new Rect(p.x - 5f, p.z - 5f, 10f, 10f);
            var q = new Vector2(p.x, p.z);
            foreach (var c in r.Rects)
            {
                var w = new Rect(c.x * HousePlan.Res, c.y * HousePlan.Res, c.width * HousePlan.Res, c.height * HousePlan.Res);
                if (w.Contains(q)) return w;
            }
            return r.Bounds;
        }

        public bool IsDarkAt(Vector3 p)
        {
            var r = RoomAt(p);
            return r != null && r.Dark;
        }

        /// <summary>Inside the building footprint (gas is everywhere else, a courtyard too).</summary>
        public bool IsInside(Vector3 p) => Plan != null && Plan.IsInside(new Vector2(p.x, p.z));

        /// <summary>
        /// Two points see and hear each other across floors only through a void (an atrium): every floor boundary the
        /// straight line crosses has no slab there. On one floor: true.
        /// </summary>
        public bool OpenBetweenFloors(Vector3 a, Vector3 b)
        {
            int fa = FloorOf(a.y), fb = FloorOf(b.y);
            if (fa == fb) return true;
            if (Plan == null) return false;
            int lo = Mathf.Min(fa, fb), hi = Mathf.Max(fa, fb);
            for (int f = lo + 1; f <= hi; f++)
            {
                float y = FloorY(f) - LevelMetrics.SlabThickness / 2f;
                float t = Mathf.Abs(b.y - a.y) < 1e-4f ? 0f : (y - a.y) / (b.y - a.y);
                var p = Vector3.Lerp(a, b, Mathf.Clamp01(t));
                var c = HousePlan.CellOf(new Vector2(p.x, p.z));
                if (Plan.KindAt(f, c.x, c.y) != CellKind.Void) return false;
            }
            return true;
        }

        /// <summary>Rooms reachable from a room in one step (doors, open passages, stairs). Vents are for crouching players only.</summary>
        public IEnumerable<int> Neighbours(int room)
        {
            foreach (var d in Doorways)
            {
                if (d.Kind is not (EdgeType.Door or EdgeType.Open)) continue;
                if (d.RoomA == room && d.RoomB >= 0) yield return d.RoomB;
                else if (d.RoomB == room && d.RoomA >= 0) yield return d.RoomA;
            }
            foreach (var s in Stairs)
            {
                if (s.Room == room) yield return s.RoomAbove;
                else if (s.RoomAbove == room) yield return s.Room;
            }
        }

        /// <summary>Where to stand to walk up a staircase (in front of its first step) and where it arrives on the floor above.</summary>
        public static (Vector3 bottom, Vector3 top) StairEnds(Stair s) =>
            (StairGeometry.ToWorld(s, new Vector3(LevelMetrics.StairSize + 0.6f, 0.05f, (StairGeometry.LaneA0 + StairGeometry.LaneA1) / 2f)),
             StairGeometry.ToWorld(s, new Vector3(LevelMetrics.StairSize + 0.6f, LevelMetrics.FloorHeight + 0.05f, (StairGeometry.LaneB0 + StairGeometry.LaneB1) / 2f)));

        /// <summary>Is a point within <paramref name="margin"/> of a staircase going up from or arriving on <paramref name="floor"/>?</summary>
        public bool NearStair(int floor, Vector3 p, float margin)
        {
            foreach (var s in Stairs)
            {
                if (s.Floor != floor && s.Floor + 1 != floor) continue;
                if (PlanGenerator.StairArea(s, margin).Contains(new Vector2(p.x, p.z))) return true;
            }
            return false;
        }
    }
}
