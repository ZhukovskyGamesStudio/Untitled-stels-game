using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    public enum RoomType { Lobby, OpenSpace, Office, Meeting, Kitchen, Server, Storage, Restroom, Lounge }
    /// <summary>Vent = a wall with a low crawl hole: crouching players fit, robots (2 m) do not.</summary>
    public enum EdgeType { Wall, Door, Open, Vent }
    public enum PropKind { Desk, Chair, ServerRack, Cabinet, MeetingTable, Plant, KitchenCounter, Hatch, Shelf, Boxes, WaterCooler, Whiteboard, Sofa, ToiletStall,
        Partition, Bookshelf, Printer, Fridge, CoffeeMachine, RoundTable, Armchair, CoffeeTable, TvStand, ReceptionDesk,
        TrashBin, CoatRack, Lockers, Pallet, WashBasin, VendingMachine, DeskLamp, FloorLamp,
        PingPongTable, BeanBag, ArcadeMachine, Safe }

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
    /// A staircase in the corner of a room: it starts on <see cref="Floor"/> and arrives on Floor + 1 in the same cell
    /// (where the slab has a hole). <see cref="Corner"/> is (±1, ±1): which corner of the cell it occupies.
    /// </summary>
    public struct Stair
    {
        public int Floor;
        public Vector2Int Cell;
        public Vector2Int Corner;
    }

    /// <summary>Pure data describing a generated office building. Identical on every peer for the same seed.</summary>
    public class LevelLayout
    {
        public int Seed, LevelIndex, Width, Height, Floors;
        /// <summary>Team size the house was generated for (a bigger team gets a bigger house).</summary>
        public int Players = 1;
        public RoomType[,,] Rooms;   // [floor, x, y]
        public bool[,,] Dark;        // lights off: robots' eyes work worse there
        public bool[,,] Flicker;     // broken light
        public EdgeType[,,] EdgesX;  // [floor, x, y]: between (x,y) and (x+1,y): wall at X = (x+1)*Cell
        public EdgeType[,,] EdgesZ;  // [floor, x, y]: between (x,y) and (x,y+1): wall at Z = (y+1)*Cell
        public readonly List<Stair> Stairs = new();
        public readonly List<(Vector2Int cell, Vector2Int dir)> ExteriorDoors = new(); // ground floor only
        public Vector2Int StartCell;
        public Vector3 HatchPosition;
        public readonly List<Vector3> PlayerSpawns = new();
        public readonly List<Vector3> ItemSpots = new();
        public readonly List<Vector3> PatrolPoints = new();
        public readonly List<Pose> CameraSpots = new();
        public readonly List<LaserSpot> LaserSpots = new();
        public readonly List<Pose> AutoDoors = new(); // sliding doors in doorways (local X along the wall)
        public readonly List<RobotSpawn> RobotSpawns = new();
        public readonly List<PropPlacement> Props = new();
        public Bounds BuildingBounds;

        public static float FloorY(int floor) => floor * LevelMetrics.FloorHeight;

        public static int FloorOf(float y) => Mathf.Max(0, Mathf.FloorToInt((y + 0.75f) / LevelMetrics.FloorHeight));

        public Vector3 CellCenter(int x, int y) =>
            new Vector3((x + 0.5f) * LevelMetrics.Cell, 0f, (y + 0.5f) * LevelMetrics.Cell);

        public Vector3 CellCenter(int floor, int x, int y) => CellCenter(x, y) + Vector3.up * FloorY(floor);

        /// <summary>Center of the room containing <paramref name="p"/> (at the height of p's floor).</summary>
        public Vector3 RoomCenterAt(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(p.x / LevelMetrics.Cell), 0, Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(p.z / LevelMetrics.Cell), 0, Height - 1);
            int f = Mathf.Clamp(FloorOf(p.y), 0, Floors - 1);
            return CellCenter(f, x, y);
        }

        public bool IsDarkAt(Vector3 p)
        {
            if (Dark == null) return false;
            int x = Mathf.FloorToInt(p.x / LevelMetrics.Cell), y = Mathf.FloorToInt(p.z / LevelMetrics.Cell);
            int f = FloorOf(p.y);
            if (x < 0 || y < 0 || x >= Width || y >= Height || f >= Floors) return false;
            return Dark[f, x, y];
        }

        /// <summary>Inside the building footprint (gas is everywhere else).</summary>
        public bool IsInside(Vector3 p) =>
            p.x > 0f && p.z > 0f && p.x < Width * LevelMetrics.Cell && p.z < Height * LevelMetrics.Cell;

        /// <summary>Quadrant index (0..3) of a stair corner, matching <see cref="LevelMetrics.Quadrants"/>.</summary>
        public static int QuadrantOf(Vector2Int corner) => corner.x > 0 ? (corner.y > 0 ? 0 : 2) : (corner.y > 0 ? 1 : 3);

        /// <summary>Quadrant of the cell occupied by stairs (going up or arriving from below), or -1.</summary>
        public int StairQuadrant(int floor, Vector2Int cell)
        {
            foreach (var s in Stairs)
                if (s.Cell == cell && (s.Floor == floor || s.Floor + 1 == floor)) return QuadrantOf(s.Corner);
            return -1;
        }

        /// <summary>Rooms reachable from a cell in one step (doors, openings, stairs). Result: (x, y, floor).</summary>
        public IEnumerable<Vector3Int> Neighbours(int floor, Vector2Int c)
        {
            // Walkable connections only (vents are for crouching players, not part of the room graph).
            static bool Pass(EdgeType e) => e == EdgeType.Door || e == EdgeType.Open;
            if (c.x > 0 && Pass(EdgesX[floor, c.x - 1, c.y])) yield return new Vector3Int(c.x - 1, c.y, floor);
            if (c.x < Width - 1 && Pass(EdgesX[floor, c.x, c.y])) yield return new Vector3Int(c.x + 1, c.y, floor);
            if (c.y > 0 && Pass(EdgesZ[floor, c.x, c.y - 1])) yield return new Vector3Int(c.x, c.y - 1, floor);
            if (c.y < Height - 1 && Pass(EdgesZ[floor, c.x, c.y])) yield return new Vector3Int(c.x, c.y + 1, floor);
            foreach (var s in Stairs)
            {
                if (s.Cell != c) continue;
                if (s.Floor == floor) yield return new Vector3Int(c.x, c.y, floor + 1);
                else if (s.Floor + 1 == floor) yield return new Vector3Int(c.x, c.y, floor - 1);
            }
        }
    }
}
